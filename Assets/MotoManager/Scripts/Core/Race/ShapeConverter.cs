using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MotoManager.Core.Data;

namespace MotoManager.Core.Race
{
    /// <summary>
    /// Turns a real circuit centre line (e.g. from OpenStreetMap) into the segment layout used by the simulation:
    /// the line is resampled, its curvature smoothed, and runs of low curvature become straights while the rest
    /// become corners with the radius and sweep measured from the real shape.
    /// </summary>
    public static class ShapeConverter
    {
        public const double Spacing = 2.0;
        /// <summary>Curvature below this (1/m) counts as straight: radius over ~400 m is flat out for any bike.</summary>
        public const double StraightCurvature = 1.0 / 400.0;
        public const double SmoothingMeters = 14.0;
        public const double MinRunMeters = 12.0;
        /// <summary>Bends that turn less than this are kinks taken flat out (on coarse lines they are mostly node noise).</summary>
        public const double MinCornerDegrees = 8.0;

        /// <summary>Shape points in metres (lat/lon are projected around the shape's centre).</summary>
        public static List<double[]> ToMeters(CircuitDefinition def)
        {
            var pts = new List<double[]>();
            foreach (var p in def.Shape) pts.Add(Project(def, p));
            return pts;
        }

        /// <summary>Projects one point given in the shape's units into the same metric frame as <see cref="ToMeters"/>.</summary>
        public static double[] Project(CircuitDefinition def, double[] p)
        {
            if (!def.ShapeIsLatLon) return new[] { p[0], p[1] };
            double lat0 = 0, lon0 = 0;
            foreach (var q in def.Shape)
            {
                lat0 += q[0];
                lon0 += q[1];
            }
            lat0 /= def.Shape.Count;
            lon0 /= def.Shape.Count;
            double rad = lat0 * Math.PI / 180;
            double mPerLat = 111132.92 - 559.82 * Math.Cos(2 * rad) + 1.175 * Math.Cos(4 * rad);
            double mPerLon = 111412.84 * Math.Cos(rad) - 93.5 * Math.Cos(3 * rad);
            return new[] { (p[1] - lon0) * mPerLon, (p[0] - lat0) * mPerLat };
        }

        /// <summary>The resampled real centre line and where each segment of the derived layout starts on it.</summary>
        public class ShapePath
        {
            /// <summary>Points every <see cref="Spacing"/> metres along the closed line, the first one on the start line.</summary>
            public List<double[]> Points;
            public double Spacing;
            /// <summary>Distance along <see cref="Points"/> where each layout token (segment) begins.</summary>
            public List<double> TokenStarts;
        }

        public static string ToLayout(CircuitDefinition def)
        {
            return ToLayout(def, null);
        }

        public static string ToLayout(CircuitDefinition def, ShapePath path)
        {
            var pts = ToMeters(def);
            // Drop a duplicated closing point.
            if (pts.Count > 2 && Dist(pts[0], pts[pts.Count - 1]) < 0.5) pts.RemoveAt(pts.Count - 1);

            // Race direction (shoelace area > 0 means anticlockwise in x-right / y-up coordinates).
            double area = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % pts.Count];
                area += a[0] * b[1] - b[0] * a[1];
            }
            bool clockwise = area < 0;
            if (clockwise != def.Clockwise) pts.Reverse();

            // Start at the finish line.
            if (def.ShapeStart != null)
            {
                var target = Project(def, def.ShapeStart);
                int best = 0;
                double bestD = double.MaxValue;
                for (int i = 0; i < pts.Count; i++)
                {
                    double d = Dist(pts[i], target);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = i;
                    }
                }
                var rotated = new List<double[]>();
                for (int i = 0; i < pts.Count; i++) rotated.Add(pts[(best + i) % pts.Count]);
                pts = rotated;
            }

            // Coarse centre lines (a node every 30-60 m, as in most published circuit files) turn every node into a
            // sharp kink; a centripetal Catmull-Rom spline through the nodes spreads each bend over the neighbouring
            // edges as on the real track. Dense lines (OSM, ~10 m) are barely changed.
            var samples = Resample(Spline(pts, Spacing), Spacing);
            int n = samples.Count;
            var heading = new double[n];
            for (int i = 0; i < n; i++)
            {
                var a = samples[i];
                var b = samples[(i + 1) % n];
                heading[i] = Math.Atan2(b[1] - a[1], b[0] - a[0]);
            }
            var curvature = new double[n];
            for (int i = 0; i < n; i++)
            {
                double dh = heading[(i + 1) % n] - heading[i];
                while (dh > Math.PI) dh -= 2 * Math.PI;
                while (dh < -Math.PI) dh += 2 * Math.PI;
                curvature[i] = dh / Spacing;
            }
            // Light smoothing to measure how tight a corner really is, strong smoothing to segment the lap.
            var sharp = Smooth(curvature, (int)Math.Round(4.0 / Spacing));
            curvature = Smooth(curvature, (int)Math.Round(SmoothingMeters / Spacing));

            // Classify each sample: 0 straight, +1 left corner, -1 right corner.
            var kind = new int[n];
            for (int i = 0; i < n; i++)
                kind[i] = Math.Abs(curvature[i]) < StraightCurvature ? 0 : Math.Sign(curvature[i]);

            // Build runs, then absorb runs that are too short into their neighbours.
            var runs = new List<int[]>(); // [kind, start, count]
            for (int i = 0; i < n; i++)
            {
                if (runs.Count > 0 && runs[runs.Count - 1][0] == kind[i]) runs[runs.Count - 1][2]++;
                else runs.Add(new[] { kind[i], i, 1 });
            }
            int minSamples = (int)Math.Ceiling(MinRunMeters / Spacing);
            bool changed = true;
            while (changed && runs.Count > 3)
            {
                changed = false;
                for (int r = 0; r < runs.Count; r++)
                {
                    if (runs[r][2] >= minSamples) continue;
                    int prev = (r - 1 + runs.Count) % runs.Count;
                    runs[prev][2] += runs[r][2];
                    runs.RemoveAt(r);
                    // merge neighbours of the same kind
                    for (int k = 0; k + 1 < runs.Count; k++)
                        if (runs[k][0] == runs[k + 1][0])
                        {
                            runs[k][2] += runs[k + 1][2];
                            runs.RemoveAt(k + 1);
                            k--;
                        }
                    changed = true;
                    break;
                }
            }

            // The lap starts where the first run starts (a short run at the line may have been merged into the last one).
            int origin = runs[0][1];
            var sb = new StringBuilder();
            var starts = new List<double>();
            int firstCorner = -1, lastCorner = -1;
            var tokens = new List<string>();
            Action<string, double> emit = (token, start) =>
            {
                tokens.Add(token);
                starts.Add(start);
            };
            foreach (var run in runs)
            {
                int cursor = run[1];
                double begin = ((cursor - origin + n) % n) * Spacing;
                double length = run[2] * Spacing;
                if (run[0] == 0)
                {
                    emit("S" + length.ToString("0", CultureInfo.InvariantCulture), begin);
                    continue;
                }
                double sweep = 0;
                for (int i = 0; i < run[2]; i++) sweep += curvature[(cursor + i) % n] * Spacing;
                double deg = Math.Abs(sweep) * 180 / Math.PI;
                if (deg < MinCornerDegrees)
                {
                    emit("S" + length.ToString("0", CultureInfo.InvariantCulture), begin);
                    continue;
                }
                // Radius from the tightest half of the corner (smoothing makes corners look wider than
                // they are); the sweep is kept, the spare length goes back to the straights.
                var values = new List<double>();
                for (int i = 0; i < run[2]; i++) values.Add(Math.Abs(sharp[(cursor + i) % n]));
                values.Sort();
                double tight = 0;
                int half = Math.Max(1, values.Count / 2);
                for (int i = values.Count - half; i < values.Count; i++) tight += values[i];
                tight /= half;
                double wide = length / Math.Abs(sweep);
                double radius = Math.Min(wide, Math.Sqrt(wide / Math.Max(tight, 1e-6)));
                double spare = Math.Max(0, length - radius * Math.Abs(sweep));
                // The arc sits where the run actually bends (curvature centroid), the spare length either side of it.
                double wsum = 0, wpos = 0;
                for (int i = 0; i < run[2]; i++)
                {
                    double w = Math.Abs(curvature[(cursor + i) % n]);
                    wsum += w;
                    wpos += w * (i + 0.5) * Spacing;
                }
                double arc = length - spare;
                double before = wsum > 0 ? wpos / wsum - arc / 2 : spare / 2;
                before = Math.Max(0, Math.Min(spare, before));
                double after = spare - before;
                if (before < 1) { after += before; before = 0; }
                if (after < 1) { arc += after; after = 0; }
                if (before > 0) emit("S" + before.ToString("0.#", CultureInfo.InvariantCulture), begin);
                if (firstCorner < 0) firstCorner = tokens.Count;
                lastCorner = tokens.Count;
                emit((sweep > 0 ? "L" : "R") + deg.ToString("0.#", CultureInfo.InvariantCulture) + "@" + radius.ToString("0.#", CultureInfo.InvariantCulture),
                     begin + before);
                if (after > 0) emit("S" + after.ToString("0.#", CultureInfo.InvariantCulture), begin + before + arc);
            }
            // Track limits are policed at the first corner and at the last one onto the main straight, as on most circuits.
            if (firstCorner >= 0) tokens[firstCorner] += ":tl";
            if (lastCorner > firstCorner) tokens[lastCorner] += ":tl";
            for (int i = 0; i < tokens.Count; i++) sb.Append(tokens[i]).Append(i + 1 < tokens.Count ? " " : "");

            if (path != null)
            {
                // The centre line itself (from the start of the lap) and where every layout token begins on it.
                path.Points = new List<double[]>(n);
                for (int i = 0; i < n; i++) path.Points.Add(samples[(origin + i) % n]);
                path.Spacing = Spacing;
                path.TokenStarts = starts;
            }
            return sb.ToString();
        }

        /// <summary>Closed centripetal Catmull-Rom spline through the points, sampled about every <paramref name="step"/> metres.</summary>
        public static List<double[]> Spline(List<double[]> pts, double step)
        {
            var result = new List<double[]>();
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                var p0 = pts[(i - 1 + n) % n];
                var p1 = pts[i];
                var p2 = pts[(i + 1) % n];
                var p3 = pts[(i + 2) % n];
                double len = Dist(p1, p2);
                int count = Math.Max(1, (int)Math.Ceiling(len / step));
                // Knot spacing sqrt(distance) (centripetal): no cusps or overshoot on uneven node spacing.
                double t0 = 0;
                double t1 = t0 + Math.Sqrt(Math.Max(1e-6, Dist(p0, p1)));
                double t2 = t1 + Math.Sqrt(Math.Max(1e-6, len));
                double t3 = t2 + Math.Sqrt(Math.Max(1e-6, Dist(p2, p3)));
                for (int k = 0; k < count; k++)
                {
                    double t = t1 + (t2 - t1) * k / count;
                    var r = new double[2];
                    for (int d = 0; d < 2; d++)
                    {
                        double a1 = (t1 - t) / (t1 - t0) * p0[d] + (t - t0) / (t1 - t0) * p1[d];
                        double a2 = (t2 - t) / (t2 - t1) * p1[d] + (t - t1) / (t2 - t1) * p2[d];
                        double a3 = (t3 - t) / (t3 - t2) * p2[d] + (t - t2) / (t3 - t2) * p3[d];
                        double b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
                        double b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
                        r[d] = (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
                    }
                    result.Add(r);
                }
            }
            return result;
        }

        private static List<double[]> Resample(List<double[]> pts, double spacing)
        {
            var result = new List<double[]>();
            int n = pts.Count;
            double carry = 0;
            for (int i = 0; i < n; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % n];
                double len = Dist(a, b);
                double t = carry;
                while (t < len)
                {
                    double f = t / len;
                    result.Add(new[] { a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f });
                    t += spacing;
                }
                carry = t - len;
            }
            return result;
        }

        private static double[] Smooth(double[] v, int radius)
        {
            if (radius <= 0) return v;
            int n = v.Length;
            var result = new double[n];
            double sigma = radius / 2.0;
            for (int i = 0; i < n; i++)
            {
                double sum = 0, wsum = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    double w = Math.Exp(-k * k / (2 * sigma * sigma));
                    sum += v[((i + k) % n + n) % n] * w;
                    wsum += w;
                }
                result[i] = sum / wsum;
            }
            return result;
        }

        private static double Dist(double[] a, double[] b)
        {
            double dx = a[0] - b[0], dy = a[1] - b[1];
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
