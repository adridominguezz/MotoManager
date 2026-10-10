using System;

namespace MotoManager.Core.Race
{
    /// <summary>
    /// The line riders follow on the drawn circuit: the minimum-curvature path within the track limits (outside on
    /// entry, apex on the inside, outside again on exit). Pure geometry for the renderer; the simulation keeps its
    /// own lateral offsets for overtakes and run-offs, which are added on top of this line.
    /// Solved by box-constrained coordinate descent on the squared second differences, coarse to fine.
    /// </summary>
    public class RacingLine
    {
        private readonly TrackGeometry _geo;
        private readonly double[] _offset;
        private readonly double _spacing;

        /// <summary>Largest distance from the centre line the racing line uses (metres).</summary>
        public readonly double Limit;

        public RacingLine(TrackGeometry geo, double halfWidth, double margin = 1.4)
        {
            _geo = geo;
            Limit = Math.Max(0, halfWidth - margin);
            double[] previous = null;
            double previousSpacing = 0;
            foreach (double target in new[] { 24.0, 12.0, 6.0 })
            {
                int n = Math.Max(16, (int)Math.Round(geo.GeometricLength / target));
                double h = geo.GeometricLength / n;
                var cx = new double[n];
                var cy = new double[n];
                var nx = new double[n];
                var ny = new double[n];
                var o = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double x, y, heading;
                    geo.EvaluateGeometric(i * h, 0, out x, out y, out heading);
                    cx[i] = x;
                    cy[i] = y;
                    nx[i] = -Math.Sin(heading);
                    ny[i] = Math.Cos(heading);
                    if (previous != null) o[i] = Sample(previous, previousSpacing, i * h);
                }
                Solve(cx, cy, nx, ny, o, previous == null ? 800 : 500);
                previous = o;
                previousSpacing = h;
            }
            _offset = previous;
            _spacing = previousSpacing;
        }

        private void Solve(double[] cx, double[] cy, double[] nx, double[] ny, double[] o, int iterations)
        {
            int n = o.Length;
            for (int it = 0; it < iterations; it++)
            {
                for (int i = 0; i < n; i++)
                {
                    int a = (i - 2 + n) % n, b = (i - 1 + n) % n, c = (i + 1) % n, d = (i + 2) % n;
                    // Point that zeroes the local bending energy, projected on this sample's normal.
                    double qx = (4 * (cx[b] + nx[b] * o[b] + cx[c] + nx[c] * o[c]) - (cx[a] + nx[a] * o[a] + cx[d] + nx[d] * o[d])) / 6;
                    double qy = (4 * (cy[b] + ny[b] * o[b] + cy[c] + ny[c] * o[c]) - (cy[a] + ny[a] * o[a] + cy[d] + ny[d] * o[d])) / 6;
                    double v = (qx - cx[i]) * nx[i] + (qy - cy[i]) * ny[i];
                    o[i] = v > Limit ? Limit : v < -Limit ? -Limit : v;
                }
            }
        }

        private static double Sample(double[] values, double spacing, double g)
        {
            int n = values.Length;
            double f = g / spacing;
            int i = (int)Math.Floor(f);
            double t = f - i;
            i = ((i % n) + n) % n;
            return values[i] + (values[(i + 1) % n] - values[i]) * t;
        }

        /// <summary>Offset of the line from the centre (metres, positive = left) at a distance along the drawn loop.</summary>
        public double OffsetAtGeometric(double g)
        {
            g %= _geo.GeometricLength;
            if (g < 0) g += _geo.GeometricLength;
            return Sample(_offset, _spacing, g);
        }

        /// <summary>Offset of the line at a simulated distance.</summary>
        public double OffsetAt(double distance)
        {
            return OffsetAtGeometric(_geo.ToGeometric(distance));
        }
    }
}
