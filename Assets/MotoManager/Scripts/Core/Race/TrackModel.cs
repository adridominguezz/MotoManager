using System;
using System.Collections.Generic;
using System.Globalization;
using MotoManager.Core.Common;
using MotoManager.Core.Data;

namespace MotoManager.Core.Race
{
    [Serializable]
    public class TrackSegment
    {
        public int Index;
        public SegmentType Type;
        public double Start;
        public double Length;
        /// <summary>Corner radius in metres (0 for straights).</summary>
        public double Radius;
        /// <summary>Signed sweep in degrees: positive = left hander, negative = right hander.</summary>
        public double AngleDeg;
        public int CornerNumber;
        public bool TrackLimits;
        public bool LongLapZone;

        // Derived characteristics (0..1)
        public double BrakingIntensity;
        public double OvertakingPotential;
        public double SlipstreamPotential;
        public double LeanRequirement;
        public double TireStressFront;
        public double TireStressRear;
        /// <summary>Corner speed in m/s for the reference bike, used for derived values and AI heuristics.</summary>
        public double ReferenceSpeed;

        public double End { get { return Start + Length; } }
        public bool IsCorner { get { return Type == SegmentType.Corner; } }
        public bool IsLeft { get { return AngleDeg > 0; } }
    }

    /// <summary>
    /// Segmented representation of a circuit used by the simulation. Built from the layout DSL of a
    /// <see cref="CircuitDefinition"/>. Independent from rendering; <see cref="TrackGeometry"/> builds the 2D shape.
    /// </summary>
    public class TrackModel
    {
        public string CircuitId;
        public CircuitDefinition Definition;
        public readonly List<TrackSegment> Segments = new List<TrackSegment>();
        public double Length;
        public int SectorCount = 4;
        public double PitEntry;
        public double PitExit;
        public double PitLaneLength;
        public double LongLapLossS;
        public int LongLapSegment = -1;

        private const double ReferenceTopSpeed = 95;
        private const double ReferenceLateralG = 1.4;
        private const double ReferenceBrakeG = 1.3;
        private const double ReferenceAccelG = 0.55;

        private static readonly Dictionary<string, TrackModel> Cache = new Dictionary<string, TrackModel>();

        public static TrackModel Get(CircuitDefinition def)
        {
            lock (Cache)
            {
                TrackModel t;
                if (!Cache.TryGetValue(def.Id + "|" + def.Layout + "|" + def.LengthM, out t))
                {
                    t = Build(def);
                    Cache[def.Id + "|" + def.Layout + "|" + def.LengthM] = t;
                }
                return t;
            }
        }

        public static TrackModel Build(CircuitDefinition def)
        {
            if (string.IsNullOrEmpty(def.Layout)) throw new FormatException("empty layout");
            var model = new TrackModel { CircuitId = def.Id, Definition = def, SectorCount = Math.Max(1, def.Sectors) };
            var tokens = def.Layout.Split(new[] { ' ', '\t', '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries);
            int corner = 0;
            foreach (var raw in tokens)
            {
                string token = raw;
                string flags = "";
                int colon = token.IndexOf(':');
                if (colon >= 0)
                {
                    flags = token.Substring(colon + 1).ToLowerInvariant();
                    token = token.Substring(0, colon);
                }
                var seg = new TrackSegment { Index = model.Segments.Count };
                char kind = char.ToUpperInvariant(token[0]);
                if (kind == 'S')
                {
                    seg.Type = SegmentType.Straight;
                    seg.Length = ParseNumber(token.Substring(1), raw);
                }
                else if (kind == 'L' || kind == 'R')
                {
                    int at = token.IndexOf('@');
                    if (at < 0) throw new FormatException("corner token without radius: " + raw);
                    double angle = ParseNumber(token.Substring(1, at - 1), raw);
                    double radius = ParseNumber(token.Substring(at + 1), raw);
                    if (radius <= 0 || angle <= 0) throw new FormatException("invalid corner: " + raw);
                    seg.Type = SegmentType.Corner;
                    seg.Radius = radius;
                    seg.AngleDeg = kind == 'L' ? angle : -angle;
                    seg.Length = radius * angle * Math.PI / 180.0;
                    seg.CornerNumber = ++corner;
                }
                else
                {
                    throw new FormatException("unknown token: " + raw);
                }
                if (flags.Contains("tl")) seg.TrackLimits = true;
                if (flags.Contains("ll")) seg.LongLapZone = true;
                model.Segments.Add(seg);
            }
            if (model.Segments.Count < 3) throw new FormatException("layout needs at least 3 segments");

            // Rescale straights so the total matches the official length.
            double cornerLen = 0, straightLen = 0;
            foreach (var s in model.Segments)
            {
                if (s.IsCorner) cornerLen += s.Length;
                else straightLen += s.Length;
            }
            if (def.LengthM > 0)
            {
                double target = def.LengthM - cornerLen;
                if (target < 50 || straightLen <= 0)
                    throw new FormatException("corners (" + cornerLen.ToString("0") + "m) exceed official length " + def.LengthM);
                double k = target / straightLen;
                if (k < 0.5 || k > 2.0)
                    throw new FormatException("layout straights need scaling x" + k.ToString("0.00", CultureInfo.InvariantCulture) + " - check the layout");
                foreach (var s in model.Segments)
                    if (!s.IsCorner) s.Length *= k;
            }

            double d = 0;
            foreach (var s in model.Segments)
            {
                s.Start = d;
                d += s.Length;
            }
            model.Length = d;
            model.PitEntry = model.Length - Math.Max(50, def.PitEntryBeforeLineM);
            model.PitExit = Math.Max(30, def.PitExitAfterLineM);
            model.PitLaneLength = def.PitLaneLengthM > 0 ? def.PitLaneLengthM : (def.PitEntryBeforeLineM + def.PitExitAfterLineM) * 1.05;
            model.LongLapLossS = def.LongLapLossS;
            for (int i = 0; i < model.Segments.Count; i++)
                if (model.Segments[i].LongLapZone) model.LongLapSegment = i;
            if (model.LongLapSegment < 0)
            {
                // Default: the slowest corner in the second half of the lap.
                double best = double.MaxValue;
                foreach (var s in model.Segments)
                    if (s.IsCorner && s.Start > model.Length * 0.4 && s.Radius < best)
                    {
                        best = s.Radius;
                        model.LongLapSegment = s.Index;
                    }
            }
            model.ComputeDerived();
            return model;
        }

        private static double ParseNumber(string s, string token)
        {
            double v;
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                throw new FormatException("invalid number in token " + token);
            return v;
        }

        private void ComputeDerived()
        {
            int n = Segments.Count;
            foreach (var s in Segments)
            {
                s.ReferenceSpeed = s.IsCorner
                    ? Math.Min(ReferenceTopSpeed, Math.Sqrt(ReferenceLateralG * MathX.G * s.Radius))
                    : ReferenceTopSpeed;
                if (s.IsCorner)
                {
                    double v = s.ReferenceSpeed;
                    double lean = Math.Atan(v * v / (MathX.G * s.Radius)) * 180 / Math.PI;
                    s.LeanRequirement = MathX.Clamp01(lean / 60.0);
                }
            }

            // Approach speed into each segment: accelerate along the preceding straight(s) from the previous corner.
            for (int i = 0; i < n; i++)
            {
                var s = Segments[i];
                double straightBefore = 0;
                double vPrev = 0;
                int j = (i - 1 + n) % n;
                int guard = 0;
                while (!Segments[j].IsCorner && guard++ < n)
                {
                    straightBefore += Segments[j].Length;
                    j = (j - 1 + n) % n;
                }
                vPrev = Segments[j].ReferenceSpeed;
                double vApproach = Math.Min(ReferenceTopSpeed, Math.Sqrt(vPrev * vPrev + 2 * ReferenceAccelG * MathX.G * straightBefore));
                double straightAfter = 0;
                int k = (i + 1) % n;
                guard = 0;
                while (!Segments[k].IsCorner && guard++ < n)
                {
                    straightAfter += Segments[k].Length;
                    k = (k + 1) % n;
                }

                if (s.IsCorner)
                {
                    bool previousIsCorner = Segments[(i - 1 + n) % n].IsCorner;
                    double vEntryFrom = previousIsCorner ? Segments[(i - 1 + n) % n].ReferenceSpeed : vApproach;
                    s.BrakingIntensity = MathX.Clamp01((vEntryFrom - s.ReferenceSpeed) / Math.Max(1, vEntryFrom) * 1.6);
                    double sweep = Math.Abs(s.AngleDeg) / 180.0;
                    s.OvertakingPotential = MathX.Clamp01(s.BrakingIntensity * 0.75 + Math.Min(1, straightBefore / 900.0) * 0.35 - (previousIsCorner ? 0.25 : 0));
                    s.TireStressFront = MathX.Clamp(sweep * (0.6 + s.BrakingIntensity) + s.LeanRequirement * 0.3, 0.1, 1.6);
                    s.TireStressRear = MathX.Clamp(sweep * (0.6 + Math.Min(1, straightAfter / 500.0)) + s.LeanRequirement * 0.3, 0.1, 1.6);
                    s.SlipstreamPotential = 0;
                }
                else
                {
                    s.SlipstreamPotential = MathX.Clamp01((s.Length + straightBefore) / 900.0);
                    s.OvertakingPotential = MathX.Clamp01(s.Length / 1300.0);
                    s.TireStressFront = 0.15;
                    s.TireStressRear = 0.35;
                }
            }
        }

        public int SegmentIndexAt(double distance)
        {
            distance = Wrap(distance);
            int lo = 0, hi = Segments.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (Segments[mid].Start <= distance) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        public TrackSegment SegmentAt(double distance)
        {
            return Segments[SegmentIndexAt(distance)];
        }

        public int SectorAt(double distance)
        {
            int s = (int)(Wrap(distance) / (Length / SectorCount));
            return Math.Min(SectorCount - 1, Math.Max(0, s));
        }

        public double Wrap(double distance)
        {
            distance %= Length;
            if (distance < 0) distance += Length;
            return distance;
        }

        public TrackSegment Next(TrackSegment s)
        {
            return Segments[(s.Index + 1) % Segments.Count];
        }

        public int CornerCount
        {
            get
            {
                int c = 0;
                foreach (var s in Segments) if (s.IsCorner) c++;
                return c;
            }
        }
    }

    /// <summary>
    /// 2D shape of the circuit for rendering (pure maths, no engine dependency).
    /// Corners keep their simulated arc length and (rescaled) sweep so bikes lean exactly where the simulation
    /// says they are cornering. Closure errors of hand-authored layouts are absorbed by stretching or shrinking
    /// straights with a weighted least-squares solve, and any small residue is spread along the lap.
    /// </summary>
    public class TrackGeometry
    {
        public readonly double Spacing;
        public readonly double[] X;
        public readonly double[] Y;
        public readonly double[] Heading;
        /// <summary>Geometric length of the drawn loop (can differ slightly from the simulated length).</summary>
        public readonly double GeometricLength;
        public readonly double Length;
        /// <summary>+1 when the circuit runs anticlockwise (mostly left turns), -1 for clockwise.</summary>
        public readonly int Orientation;
        public double MinX, MinY, MaxX, MaxY;
        /// <summary>Raw closure error before any correction, in metres (diagnostic for layout authoring).</summary>
        public readonly double ClosureError;
        /// <summary>Residual error after straight adjustment (spread along the lap).</summary>
        public readonly double ResidualError;

        private readonly TrackModel _track;
        private readonly double[] _geoStart;
        private readonly double[] _geoLength;

        public TrackGeometry(TrackModel track, double spacing = 2.0)
        {
            _track = track;
            Length = track.Length;
            int n = track.Segments.Count;

            double totalTurn = 0;
            foreach (var s in track.Segments) totalTurn += s.AngleDeg;
            Orientation = totalTurn >= 0 ? 1 : -1;
            double angleScale = Math.Abs(totalTurn) > 1 ? 360.0 * Orientation / totalTurn : 1;

            // Heading at the start of every segment and straight directions.
            var headings = new double[n];
            var sweep = new double[n];
            double h = 0;
            for (int i = 0; i < n; i++)
            {
                headings[i] = h;
                var s = track.Segments[i];
                sweep[i] = s.IsCorner ? s.AngleDeg * angleScale * Math.PI / 180.0 : 0;
                h += sweep[i];
            }

            _geoLength = new double[n];
            for (int i = 0; i < n; i++) _geoLength[i] = track.Segments[i].Length;

            double ex, ey;
            EndPoint(track, headings, sweep, _geoLength, out ex, out ey);
            ClosureError = Math.Sqrt(ex * ex + ey * ey);

            // Weighted least squares on straight lengths: minimise sum(d_i^2 / l_i) s.t. sum(d_i * u_i) = -e
            for (int iter = 0; iter < 3; iter++)
            {
                double a11 = 0, a12 = 0, a22 = 0;
                for (int i = 0; i < n; i++)
                {
                    if (track.Segments[i].IsCorner) continue;
                    double ux = Math.Cos(headings[i]), uy = Math.Sin(headings[i]);
                    double l = _geoLength[i];
                    a11 += l * ux * ux;
                    a12 += l * ux * uy;
                    a22 += l * uy * uy;
                }
                double det = a11 * a22 - a12 * a12;
                if (Math.Abs(det) < 1e-9) break;
                double lx = (-ex * a22 + ey * a12) / det;
                double ly = (-ey * a11 + ex * a12) / det;
                for (int i = 0; i < n; i++)
                {
                    if (track.Segments[i].IsCorner) continue;
                    double ux = Math.Cos(headings[i]), uy = Math.Sin(headings[i]);
                    double l = _geoLength[i];
                    double d = l * (ux * lx + uy * ly);
                    double min = track.Segments[i].Length * 0.35;
                    _geoLength[i] = Math.Max(min, l + d);
                }
                EndPoint(track, headings, sweep, _geoLength, out ex, out ey);
            }
            ResidualError = Math.Sqrt(ex * ex + ey * ey);

            _geoStart = new double[n];
            double total = 0;
            for (int i = 0; i < n; i++)
            {
                _geoStart[i] = total;
                total += _geoLength[i];
            }
            GeometricLength = total;

            int count = Math.Max(16, (int)Math.Ceiling(total / spacing));
            Spacing = total / count;
            X = new double[count + 1];
            Y = new double[count + 1];
            Heading = new double[count + 1];

            double x = 0, y = 0;
            int seg = 0;
            for (int k = 0; k <= count; k++)
            {
                double g = k * Spacing;
                while (seg < n - 1 && g > _geoStart[seg] + _geoLength[seg]) seg++;
                // integrate from the segment start analytically
                double local = Math.Min(_geoLength[seg], Math.Max(0, g - _geoStart[seg]));
                double sx, sy;
                SegmentStart(seg, headings, sweep, out sx, out sy);
                double hs = headings[seg];
                double sw = sweep[seg];
                if (Math.Abs(sw) < 1e-9)
                {
                    x = sx + Math.Cos(hs) * local;
                    y = sy + Math.Sin(hs) * local;
                    Heading[k] = hs;
                }
                else
                {
                    double radius = _geoLength[seg] / sw;
                    double ang = sw * local / _geoLength[seg];
                    x = sx + radius * (Math.Sin(hs + ang) - Math.Sin(hs));
                    y = sy - radius * (Math.Cos(hs + ang) - Math.Cos(hs));
                    Heading[k] = hs + ang;
                }
                X[k] = x;
                Y[k] = y;
            }
            // Spread the residual.
            double rx = X[count] - X[0];
            double ry = Y[count] - Y[0];
            for (int k = 0; k <= count; k++)
            {
                double t = (double)k / count;
                X[k] -= rx * t;
                Y[k] -= ry * t;
            }

            MinX = MinY = double.MaxValue;
            MaxX = MaxY = double.MinValue;
            for (int k = 0; k <= count; k++)
            {
                MinX = Math.Min(MinX, X[k]);
                MaxX = Math.Max(MaxX, X[k]);
                MinY = Math.Min(MinY, Y[k]);
                MaxY = Math.Max(MaxY, Y[k]);
            }
        }

        private double[] _segStartX;
        private double[] _segStartY;

        private void SegmentStart(int seg, double[] headings, double[] sweep, out double x, out double y)
        {
            if (_segStartX == null)
            {
                int n = headings.Length;
                _segStartX = new double[n];
                _segStartY = new double[n];
                double cx = 0, cy = 0;
                for (int i = 0; i < n; i++)
                {
                    _segStartX[i] = cx;
                    _segStartY[i] = cy;
                    Advance(headings[i], sweep[i], _geoLength[i], ref cx, ref cy);
                }
            }
            x = _segStartX[seg];
            y = _segStartY[seg];
        }

        private static void Advance(double heading, double sweep, double length, ref double x, ref double y)
        {
            if (Math.Abs(sweep) < 1e-9)
            {
                x += Math.Cos(heading) * length;
                y += Math.Sin(heading) * length;
            }
            else
            {
                double radius = length / sweep;
                x += radius * (Math.Sin(heading + sweep) - Math.Sin(heading));
                y -= radius * (Math.Cos(heading + sweep) - Math.Cos(heading));
            }
        }

        private static void EndPoint(TrackModel track, double[] headings, double[] sweep, double[] lengths, out double x, out double y)
        {
            x = 0;
            y = 0;
            for (int i = 0; i < lengths.Length; i++) Advance(headings[i], sweep[i], lengths[i], ref x, ref y);
        }

        public int PointCount { get { return X.Length; } }

        /// <summary>Converts a simulated distance into a distance along the drawn polyline.</summary>
        public double ToGeometric(double distance)
        {
            distance %= Length;
            if (distance < 0) distance += Length;
            int seg = _track.SegmentIndexAt(distance);
            var s = _track.Segments[seg];
            double f = s.Length > 0 ? (distance - s.Start) / s.Length : 0;
            return _geoStart[seg] + f * _geoLength[seg];
        }

        /// <summary>Position on track for a simulated distance. lateral &gt; 0 moves to the left of the direction of travel.</summary>
        public void Evaluate(double distance, double lateral, out double x, out double y, out double heading)
        {
            EvaluateGeometric(ToGeometric(distance), lateral, out x, out y, out heading);
        }

        public void EvaluateGeometric(double g, double lateral, out double x, out double y, out double heading)
        {
            g %= GeometricLength;
            if (g < 0) g += GeometricLength;
            double f = g / Spacing;
            int i = (int)f;
            int last = X.Length - 1;
            if (i >= last) i = last - 1;
            double t = f - i;
            x = X[i] + (X[i + 1] - X[i]) * t;
            y = Y[i] + (Y[i + 1] - Y[i]) * t;
            double dx = X[i + 1] - X[i];
            double dy = Y[i + 1] - Y[i];
            heading = Math.Atan2(dy, dx);
            if (lateral != 0)
            {
                x += -Math.Sin(heading) * lateral;
                y += Math.Cos(heading) * lateral;
            }
        }
    }
}
