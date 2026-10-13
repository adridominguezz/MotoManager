using System;
using System.Collections.Generic;
using MotoManager.Core.Race;

namespace MotoManager.App.Race
{
    /// <summary>
    /// Engine-independent plan of everything drawn around a circuit, in track-plane metres (x, y; y is Unity's z):
    /// flat surface quads (tarmac, kerbs, run-offs, verges, pit lane, paddock, water), painted decals, walls
    /// (barriers, tyre walls, flags), boxes (buildings, stands, garages) and trees. <see cref="TrackRenderer"/> turns
    /// it into a handful of merged vertex-coloured meshes. Kept free of UnityEngine so it can be previewed headless.
    /// Kerbs, run-offs and barriers are computed from the simulation's corners and clipped wherever another part of
    /// the circuit (or the inside of a tight bend) is closer than the offset, so nothing is drawn across the track.
    /// </summary>
    public class TrackPlan
    {
        public struct Rgb
        {
            public byte R, G, B;

            public Rgb(int r, int g, int b)
            {
                R = (byte)Clamp(r);
                G = (byte)Clamp(g);
                B = (byte)Clamp(b);
            }

            private static int Clamp(int v)
            {
                return v < 0 ? 0 : v > 255 ? 255 : v;
            }

            public Rgb Shade(int d)
            {
                return new Rgb(R + d, G + d, B + d);
            }

            public static Rgb Lerp(Rgb a, Rgb b, double t)
            {
                return new Rgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
            }
        }

        public enum Layer
        {
            /// <summary>Ground cover lying on the grass (verges, run-offs, kerbs, tarmac, water).</summary>
            Surface,
            /// <summary>Paint on top of the surface (lines, grid, rubber).</summary>
            Decal
        }

        /// <summary>Flat quad a-b-c-d (any winding; the renderer faces it up), at elevation Z.</summary>
        public struct Flat
        {
            public double Ax, Ay, Bx, By, Cx, Cy, Dx, Dy, Z;
            public Rgb Color;
            public Layer Layer;
        }

        /// <summary>Vertical slab from (x1, y1) to (x2, y2): barriers, tyre walls, flags, hoardings.</summary>
        public struct Wall
        {
            public double X1, Y1, X2, Y2, Z, Height, Thickness;
            public Rgb Color, Top;
        }

        /// <summary>Box with its long side (Length) along heading H (radians, 0 = +x, anticlockwise).</summary>
        public struct Block
        {
            public double X, Y, Z, Length, Width, Height, H;
            public Rgb Color;
        }

        public struct TreeSpot
        {
            public double X, Y, Size, Tint;
            /// <summary>0 conifer, 1 broadleaf.</summary>
            public int Kind;
        }

        public const double HalfWidth = 6;
        /// <summary>Pit lane centre from the track centre along the pit straight.</summary>
        public const double PitOffset = HalfWidth + 9;
        public const double KerbWidth = 1.3;
        public const double PaintWidth = 3.0;

        public static readonly Rgb Grass = new Rgb(64, 112, 58);
        public static readonly Rgb VergeA = new Rgb(84, 140, 70);
        public static readonly Rgb VergeB = new Rgb(76, 130, 64);
        public static readonly Rgb Asphalt = new Rgb(64, 66, 72);
        public static readonly Rgb Rubber = new Rgb(52, 53, 58);
        public static readonly Rgb Line = new Rgb(236, 236, 236);
        public static readonly Rgb KerbRed = new Rgb(206, 36, 36);
        public static readonly Rgb KerbWhite = new Rgb(240, 240, 240);
        public static readonly Rgb Gravel = new Rgb(212, 192, 146);
        public static readonly Rgb RunoffAsphalt = new Rgb(122, 125, 131);
        public static readonly Rgb Concrete = new Rgb(176, 176, 172);
        public static readonly Rgb PitAsphalt = new Rgb(84, 86, 92);
        public static readonly Rgb Paddock = new Rgb(104, 106, 112);
        public static readonly Rgb Armco = new Rgb(190, 194, 200);
        public static readonly Rgb Water = new Rgb(64, 138, 196);
        public static readonly Rgb Shore = new Rgb(196, 186, 150);

        public static readonly Rgb[] CrowdColors =
        {
            new Rgb(230, 60, 60), new Rgb(250, 210, 60), new Rgb(60, 140, 230), new Rgb(240, 240, 240),
            new Rgb(70, 190, 110), new Rgb(240, 130, 40), new Rgb(40, 40, 50), new Rgb(180, 80, 200)
        };

        public static readonly Rgb[] AdColors =
        {
            new Rgb(225, 40, 40), new Rgb(30, 90, 200), new Rgb(250, 200, 30), new Rgb(30, 160, 90),
            new Rgb(240, 240, 240), new Rgb(120, 60, 180), new Rgb(240, 120, 30), new Rgb(20, 30, 50)
        };

        public readonly TrackGeometry Geometry;
        public readonly TrackModel Track;
        public readonly RacingLine RacingLine;
        public readonly int PitSide;
        /// <summary>Painted run-off colour of this circuit (blue or green, as on modern circuits).</summary>
        public readonly Rgb Accent;

        public readonly List<Flat> Flats = new List<Flat>();
        public readonly List<Wall> Walls = new List<Wall>();
        public readonly List<Block> Blocks = new List<Block>();
        public readonly List<TreeSpot> Trees = new List<TreeSpot>();
        public double MinX, MinY, MaxX, MaxY;

        // Samples along the lap (simulated distance), shared by every band so the bands meet without gaps.
        private readonly int _n;
        private readonly double[] _d, _g;
        private readonly double _geoLength;
        private readonly double[] _cx, _cy, _nx, _ny, _h;
        // Per side (0 = right, 1 = left): kerb, paint, run-off width and kind, barrier offset, free room.
        private readonly bool[][] _kerb = new bool[2][];
        private readonly double[][] _runoff = new double[2][];
        private readonly bool[][] _gravel = new bool[2][];
        private readonly double[][] _barrier = new double[2][];
        private readonly double[][] _free = new double[2][];
        private readonly double[][] _outer = new double[2][];
        private readonly Random _rng;

        private const double Cell = 25;
        private readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();
        private readonly List<double[]> _clear = new List<double[]>(); // x, y, radius kept free of trees

        public TrackPlan(TrackGeometry geometry, TrackModel track, RacingLine line, int pitSide)
        {
            Geometry = geometry;
            Track = track;
            RacingLine = line;
            PitSide = pitSide;
            _rng = new Random((int)(Core.Common.DeterministicRandom.Hash(track.CircuitId) & 0x7FFFFFFF));
            Accent = _rng.NextDouble() < 0.5 ? new Rgb(58, 118, 196) : new Rgb(52, 140, 92);

            // Samples are evenly spaced on the drawing (blocks, kerbs and buildings keep their size), each one
            // also knowing its simulated distance (corners are placed by the simulation's segments).
            _geoLength = geometry.GeometricLength;
            _n = Math.Max(64, (int)Math.Ceiling(_geoLength / 2.5));
            _d = new double[_n];
            _g = new double[_n];
            int table = _n * 4;
            var simAt = new double[table + 1];
            var geoAt = new double[table + 1];
            for (int k = 0; k < table; k++)
            {
                simAt[k] = track.Length * k / table;
                geoAt[k] = geometry.ToGeometric(simAt[k]);
            }
            simAt[table] = track.Length;
            geoAt[table] = _geoLength;
            int cursor = 0;
            _cx = new double[_n];
            _cy = new double[_n];
            _nx = new double[_n];
            _ny = new double[_n];
            _h = new double[_n];
            for (int i = 0; i < _n; i++)
            {
                _g[i] = _geoLength * i / _n;
                while (cursor < table - 1 && geoAt[cursor + 1] < _g[i]) cursor++;
                double span = geoAt[cursor + 1] - geoAt[cursor];
                _d[i] = simAt[cursor] + (simAt[cursor + 1] - simAt[cursor]) * (span > 1e-9 ? (_g[i] - geoAt[cursor]) / span : 0);
                double x, y, h;
                geometry.EvaluateGeometric(_g[i], 0, out x, out y, out h);
                _cx[i] = x;
                _cy[i] = y;
                _h[i] = h;
                _nx[i] = -Math.Sin(h); // left normal
                _ny[i] = Math.Cos(h);
                long key = Key(x, y);
                List<int> l;
                if (!_grid.TryGetValue(key, out l)) _grid[key] = l = new List<int>();
                l.Add(i);
            }
            MinX = geometry.MinX - 420;
            MaxX = geometry.MaxX + 420;
            MinY = geometry.MinY - 420;
            MaxY = geometry.MaxY + 420;

            for (int s = 0; s < 2; s++)
            {
                _kerb[s] = new bool[_n];
                _runoff[s] = new double[_n];
                _gravel[s] = new bool[_n];
                _barrier[s] = new double[_n];
                _free[s] = new double[_n];
                _outer[s] = new double[_n];
            }
            MeasureRoom();
            PlanCorners();
            PlanBarriers();
            Surfaces();
            Paint();
            PitComplex();
            Gantry();
            Grandstands();
            Lakes();
            Buildings();
            PlantTrees();
        }

        // ---------------------------------------------------------------- helpers

        private static long Key(double x, double y)
        {
            return ((long)Math.Floor(x / Cell) << 32) ^ (uint)(int)Math.Floor(y / Cell);
        }

        private static int SideIndex(int sign)
        {
            return sign > 0 ? 1 : 0;
        }

        private int Wrap(int i)
        {
            return ((i % _n) + _n) % _n;
        }

        private int IndexAt(double d)
        {
            return Wrap((int)Math.Round(Geometry.ToGeometric(d) / _geoLength * _n));
        }

        /// <summary>Point at sample i, lateral offset (positive = left of the direction of travel).</summary>
        private void At(int i, double lateral, out double x, out double y)
        {
            i = Wrap(i);
            x = _cx[i] + _nx[i] * lateral;
            y = _cy[i] + _ny[i] * lateral;
        }

        /// <summary>Distance to the nearest centre-line sample and its index (search radius ~70 m).</summary>
        public double DistanceToTrack(double x, double y, out int nearest)
        {
            int cx = (int)Math.Floor(x / Cell), cy = (int)Math.Floor(y / Cell);
            double best = double.MaxValue;
            nearest = -1;
            for (int dx = -3; dx <= 3; dx++)
                for (int dy = -3; dy <= 3; dy++)
                {
                    List<int> l;
                    if (!_grid.TryGetValue(((long)(cx + dx) << 32) ^ (uint)(cy + dy), out l)) continue;
                    foreach (int i in l)
                    {
                        double ex = _cx[i] - x, ey = _cy[i] - y;
                        double d2 = ex * ex + ey * ey;
                        if (d2 < best)
                        {
                            best = d2;
                            nearest = i;
                        }
                    }
                }
            return nearest < 0 ? 999 : Math.Sqrt(best);
        }

        private static int CyclicGap(int a, int b, int n)
        {
            int d = Math.Abs(a - b) % n;
            return Math.Min(d, n - d);
        }

        private void Range(double from, double to, Action<int, double> action)
        {
            if (to <= from) return;
            // Simulated distances to positions on the drawing (the range may wrap over the line).
            double g0 = Geometry.ToGeometric(from), g1 = Geometry.ToGeometric(to);
            if (g1 < g0) g1 += _geoLength;
            int a = (int)Math.Floor(g0 / _geoLength * _n), b = (int)Math.Ceiling(g1 / _geoLength * _n);
            for (int k = a; k <= b; k++) action(Wrap(k), b == a ? 0.5 : (double)(k - a) / (b - a));
        }

        private bool InPitZone(int i, out double t)
        {
            double entry = Track.PitEntry, exit = Track.PitExit + Track.Length;
            double d = _d[i];
            if (d < entry) d += Track.Length;
            t = (d - entry) / (exit - entry);
            return t >= 0 && t <= 1;
        }

        private void AddFlat(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy, Rgb c, Layer layer = Layer.Surface, double z = 0)
        {
            Flats.Add(new Flat { Ax = ax, Ay = ay, Bx = bx, By = by, Cx = cx, Cy = cy, Dx = dx, Dy = dy, Color = c, Layer = layer, Z = z });
        }

        /// <summary>Quad between samples i and i+1 for lateral offsets la..lb (start) and la2..lb2 (end).</summary>
        private void Band(int i, double la, double lb, double la2, double lb2, Rgb c, Layer layer = Layer.Surface)
        {
            if (Math.Abs(lb - la) < 0.02 && Math.Abs(lb2 - la2) < 0.02) return;
            int j = Wrap(i + 1);
            double ax, ay, bx, by, cx, cy, dx, dy;
            At(i, la, out ax, out ay);
            At(i, lb, out bx, out by);
            At(j, lb2, out cx, out cy);
            At(j, la2, out dx, out dy);
            AddFlat(ax, ay, bx, by, cx, cy, dx, dy, c, layer);
        }

        private void Box(double x, double y, double z, double length, double width, double height, double h, Rgb c)
        {
            Blocks.Add(new Block { X = x, Y = y, Z = z, Length = length, Width = width, Height = height, H = h, Color = c });
        }

        private void BoxAt(int i, double lateral, double z, double length, double width, double height, Rgb c)
        {
            double x, y;
            At(i, lateral, out x, out y);
            Box(x, y, z, length, width, height, _h[Wrap(i)], c);
        }

        private void Clear(double x, double y, double r)
        {
            _clear.Add(new[] { x, y, r });
        }

        private bool IsClear(double x, double y, double margin)
        {
            foreach (var c in _clear)
            {
                double dx = c[0] - x, dy = c[1] - y;
                if (dx * dx + dy * dy < (c[2] + margin) * (c[2] + margin)) return false;
            }
            return true;
        }

        private double Hash(int i, int salt)
        {
            uint h = (uint)(i * 73856093) ^ (uint)(salt * 19349663) ^ 0x9E3779B9u;
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFF) / 65535.0;
        }

        // ---------------------------------------------------------------- layout of the circuit edges

        /// <summary>How far out from the centre line each side can go before another part of the circuit is closer.</summary>
        private void MeasureRoom()
        {
            const double Reach = 70, Margin = 1.5;
            for (int i = 0; i < _n; i++)
            {
                double free0 = Reach, free1 = Reach;
                int gx = (int)Math.Floor(_cx[i] / Cell), gy = (int)Math.Floor(_cy[i] / Cell);
                for (int dx = -3; dx <= 3; dx++)
                    for (int dy = -3; dy <= 3; dy++)
                    {
                        List<int> l;
                        if (!_grid.TryGetValue(((long)(gx + dx) << 32) ^ (uint)(gy + dy), out l)) continue;
                        foreach (int q in l)
                        {
                            if (CyclicGap(q, i, _n) <= 2) continue;
                            double wx = _cx[i] - _cx[q], wy = _cy[i] - _cy[q];
                            double w2 = wx * wx + wy * wy;
                            if (w2 > 4 * Reach * Reach) continue;
                            // Point at lateral L on side +-1 is closer to q than to the centre line (minus a margin) once
                            // L (2 side n.w + 2 Margin) < Margin^2 - |w|^2.
                            double nw = _nx[i] * wx + _ny[i] * wy;
                            for (int s = 0; s < 2; s++)
                            {
                                double A = 2 * (s == 1 ? nw : -nw) + 2 * Margin;
                                if (A >= 0) continue;
                                double lat = (w2 - Margin * Margin) / -A - 1;
                                if (s == 1) free1 = Math.Min(free1, lat);
                                else free0 = Math.Min(free0, lat);
                            }
                        }
                    }
                _free[0][i] = Math.Max(HalfWidth, free0);
                _free[1][i] = Math.Max(HalfWidth, free1);
            }
            for (int s = 0; s < 2; s++)
            {
                // A little smoothing so clipped edges do not saw-tooth.
                var f = (double[])_free[s].Clone();
                for (int i = 0; i < _n; i++)
                {
                    double m = f[i];
                    for (int k = -3; k <= 3; k++) m = Math.Min(m, f[Wrap(i + k)] + Math.Abs(k) * 0.8);
                    _free[s][i] = m;
                }
            }
        }

        private void PlanCorners()
        {
            foreach (var seg in Track.Segments)
            {
                if (!seg.IsCorner || Math.Abs(seg.AngleDeg) < 12) continue;
                int inside = seg.AngleDeg > 0 ? 1 : 0; // left hander: inside is the left side
                int outside = 1 - inside;
                double a = seg.Start, L = seg.Length, b = seg.End;
                // Apex kerb on the inside, entry and exit kerbs on the outside.
                double apexFrom = a + L * 0.18, apexTo = b - L * 0.18;
                if (apexTo - apexFrom < 14)
                {
                    double mid = a + L / 2;
                    apexFrom = mid - 7;
                    apexTo = mid + 7;
                }
                Range(apexFrom, apexTo, (i, t) => _kerb[inside][i] = true);
                Range(a - 6, a + L * 0.3, (i, t) => _kerb[outside][i] = true);
                Range(a + L * 0.55, b + 10 + L * 0.15, (i, t) => _kerb[outside][i] = true);

                // Run-off on the outside, widest past the apex where a rider who runs wide ends up.
                double v = seg.ReferenceSpeed;
                double width = Math.Max(10, Math.Min(42, 4 + v * 0.62));
                bool gravel = seg.BrakingIntensity > 0.45 || v < 32;
                double from = a - 12 - L * 0.1, to = b + 25 + L * 0.35;
                Range(from, to, (i, t) =>
                {
                    double env = Smooth(t / 0.3) * Smooth((1 - t) / 0.45);
                    double w = width * env;
                    if (w > _runoff[outside][i])
                    {
                        _runoff[outside][i] = w;
                        _gravel[outside][i] = gravel;
                    }
                });
            }
            // Short apex kerbs that would clip (tight hairpins next to another stretch) stay; anything in the pit
            // zone on the pit side goes, the pit lane is there.
            for (int i = 0; i < _n; i++)
            {
                double t;
                if (InPitZone(i, out t))
                {
                    int ps = SideIndex(PitSide);
                    _kerb[ps][i] = false;
                    _runoff[ps][i] = 0;
                }
            }
        }

        private static double Smooth(double t)
        {
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return t * t * (3 - 2 * t);
        }

        private void PlanBarriers()
        {
            for (int s = 0; s < 2; s++)
            {
                var want = new double[_n];
                for (int i = 0; i < _n; i++)
                {
                    double kerb = _kerb[s][i] ? KerbWidth : 0;
                    // Clip the run-off to the room available.
                    double room = _free[s][i] - HalfWidth - kerb - 3.5;
                    _runoff[s][i] = Math.Max(0, Math.Min(_runoff[s][i], room));
                    double edge = HalfWidth + kerb + (_runoff[s][i] > 0.5 ? _runoff[s][i] + Math.Min(PaintWidth, _runoff[s][i]) : 0);
                    _outer[s][i] = edge;
                    want[i] = Math.Max(HalfWidth + 9, edge + 3);
                }
                // Barriers swing out smoothly around the run-offs.
                var m = new double[_n];
                for (int i = 0; i < _n; i++)
                {
                    double v = 0;
                    for (int k = -8; k <= 8; k++) v = Math.Max(v, want[Wrap(i + k)]);
                    m[i] = v;
                }
                for (int i = 0; i < _n; i++)
                {
                    double sum = 0;
                    for (int k = -6; k <= 6; k++) sum += m[Wrap(i + k)];
                    _barrier[s][i] = Math.Min(sum / 13, _free[s][i] - 0.5);
                }
            }
        }

        // ---------------------------------------------------------------- ground surfaces

        private void Surfaces()
        {
            // Tarmac: the darker rubbered-in racing line in the middle of three panels per sample, each with a faint
            // variation that reads as texture.
            for (int i = 0; i < _n; i++)
            {
                double o0 = 0, o1 = 0;
                if (RacingLine != null)
                {
                    o0 = Math.Max(-HalfWidth + 1.2, Math.Min(HalfWidth - 1.2, RacingLine.OffsetAtGeometric(_g[i])));
                    o1 = Math.Max(-HalfWidth + 1.2, Math.Min(HalfWidth - 1.2, RacingLine.OffsetAtGeometric(_g[Wrap(i + 1)])));
                }
                double[] a = { -HalfWidth, o0 - 0.9, o0 + 0.9, HalfWidth };
                double[] b = { -HalfWidth, o1 - 0.9, o1 + 0.9, HalfWidth };
                for (int k = 0; k < 3; k++)
                {
                    double r = Hash(i / 2, k * 7 + 1);
                    int shade = (int)Math.Round((r - 0.5) * 7) + (Hash(i / 9, k + 40) > 0.86 ? -4 : 0);
                    Band(i, a[k], a[k + 1], b[k], b[k + 1], (k == 1 ? Rubber : Asphalt).Shade(shade / 2));
                }
            }

            for (int s = 0; s < 2; s++)
            {
                int sign = s == 1 ? 1 : -1;
                for (int i = 0; i < _n; i++)
                {
                    int j = Wrap(i + 1);
                    double pt;
                    bool pit = sign == PitSide && InPitZone(i, out pt) && InPitZone(j, out pt);
                    // Lateral stops (start, end of this quad) for kerb, paint, run-off, verge.
                    double k0 = HalfWidth + (_kerb[s][i] ? KerbWidth : 0), k1 = HalfWidth + (_kerb[s][j] ? KerbWidth : 0);
                    // Kerbs: 2.5 m red and white blocks.
                    if (_kerb[s][i] || _kerb[s][j])
                        Band(i, sign * HalfWidth, sign * k0, sign * HalfWidth, sign * k1, (i / 1) % 2 == 0 ? KerbRed : KerbWhite);
                    double r0 = _runoff[s][i], r1 = _runoff[s][j];
                    double p0 = Math.Min(PaintWidth, r0), p1 = Math.Min(PaintWidth, r1);
                    if (r0 > 0.05 || r1 > 0.05)
                    {
                        Band(i, sign * k0, sign * (k0 + p0), sign * k1, sign * (k1 + p1), Accent);
                        var c = _gravel[s][i] ? Gravel.Shade((int)((Hash(i / 3, 90 + s) - 0.5) * 8)) : RunoffAsphalt.Shade((int)((Hash(i / 3, 91 + s) - 0.5) * 6));
                        Band(i, sign * (k0 + p0), sign * (k0 + p0 + r0), sign * (k1 + p1), sign * (k1 + p1 + r1), c);
                    }
                    double o0 = k0 + p0 + r0, o1 = k1 + p1 + r1;
                    double v0 = _barrier[s][i], v1 = _barrier[s][j];
                    if (pit)
                    {
                        // Up to the pit wall only.
                        double t0, t1;
                        InPitZone(i, out t0);
                        InPitZone(j, out t1);
                        v0 = Math.Max(HalfWidth, PitLateral(t0) - 3);
                        v1 = Math.Max(HalfWidth, PitLateral(t1) - 3);
                    }
                    // Mown verge in stripes up to the barrier.
                    if (v0 > o0 + 0.05 || v1 > o1 + 0.05)
                        Band(i, sign * o0, sign * Math.Max(o0, v0), sign * o1, sign * Math.Max(o1, v1), (i / 4) % 2 == 0 ? VergeA : VergeB);
                }
            }
        }

        // ---------------------------------------------------------------- paint

        private void Paint()
        {
            for (int i = 0; i < _n; i++)
            {
                // White edge lines.
                Band(i, HalfWidth - 0.25, HalfWidth - 0.65, HalfWidth - 0.25, HalfWidth - 0.65, Line, Layer.Decal);
                Band(i, -HalfWidth + 0.25, -HalfWidth + 0.65, -HalfWidth + 0.25, -HalfWidth + 0.65, Line, Layer.Decal);
            }

            // Start/finish line: chequered band then a white line.
            for (int row = 0; row < 2; row++)
                for (int k = 0; k < 10; k++)
                {
                    double a = -HalfWidth + 0.65 + k * (2 * (HalfWidth - 0.65) / 10), b = a + 2 * (HalfWidth - 0.65) / 10;
                    Across(row * 0.7 - 0.7, row * 0.7, a, b, (k + row) % 2 == 0 ? Line : new Rgb(24, 24, 26), Layer.Decal, 0.002);
                }
            // Grid boxes (MotoGP: rows of three, staggered), as in the old renderer so bikes still line up on them.
            for (int slot = 0; slot < 30; slot++)
            {
                int row = slot / 3, col = slot % 3;
                double d = -(8 + row * 9 + col * 2.5);
                double lat = (col - 1) * 0.55 * HalfWidth;
                Across(d + 0.9, d + 1.15, lat - 1.1, lat + 1.1, Line, Layer.Decal, 0.002);
                Across(d - 2.2, d + 1.15, lat - 1.1, lat - 0.9, Line, Layer.Decal, 0.002);
                Across(d - 2.2, d + 1.15, lat + 0.9, lat + 1.1, Line, Layer.Decal, 0.002);
            }
        }

        /// <summary>Rectangle between distances g0..g1 along the drawing and lateral offsets a..b.</summary>
        private void AcrossGeo(double g0, double g1, double a, double b, Rgb c, Layer layer, double z = 0)
        {
            double h;
            double ax, ay, bx, by, cx, cy, dx, dy;
            Geometry.EvaluateGeometric(g0, a, out ax, out ay, out h);
            Geometry.EvaluateGeometric(g0, b, out bx, out by, out h);
            Geometry.EvaluateGeometric(g1, b, out cx, out cy, out h);
            Geometry.EvaluateGeometric(g1, a, out dx, out dy, out h);
            AddFlat(ax, ay, bx, by, cx, cy, dx, dy, c, layer, z);
        }

        /// <summary>Rectangle across the track between simulated distances d0..d1 and lateral offsets a..b.</summary>
        private void Across(double d0, double d1, double a, double b, Rgb c, Layer layer, double z = 0)
        {
            double h;
            double ax, ay, bx, by, cx, cy, dx, dy;
            Geometry.Evaluate(d0, a, out ax, out ay, out h);
            Geometry.Evaluate(d0, b, out bx, out by, out h);
            Geometry.Evaluate(d1, b, out cx, out cy, out h);
            Geometry.Evaluate(d1, a, out dx, out dy, out h);
            AddFlat(ax, ay, bx, by, cx, cy, dx, dy, c, layer, z);
        }

        // ---------------------------------------------------------------- barriers

        private void BarrierWalls()
        {
            var tyreA = new Rgb(214, 40, 40);
            var tyreB = new Rgb(236, 236, 236);
            var tyreTop = new Rgb(40, 40, 44);
            for (int s = 0; s < 2; s++)
            {
                int sign = s == 1 ? 1 : -1;
                for (int i = 0; i < _n; i++)
                {
                    int j = Wrap(i + 1);
                    double t;
                    if (sign == PitSide && (InPitZone(i, out t) || InPitZone(j, out t))) continue;
                    double b0 = _barrier[s][i], b1 = _barrier[s][j];
                    if (b0 < HalfWidth + 2.5 || b1 < HalfWidth + 2.5) continue;
                    double x0, y0, x1, y1;
                    At(i, sign * b0, out x0, out y0);
                    At(j, sign * b1, out x1, out y1);
                    // Tyre walls where there is a run-off in front, armco with advertising elsewhere.
                    bool tyres = _runoff[s][i] > 4 || _runoff[s][Wrap(i - 6)] > 4 || _runoff[s][Wrap(i + 6)] > 4;
                    if (tyres)
                        Walls.Add(new Wall { X1 = x0, Y1 = y0, X2 = x1, Y2 = y1, Height = 1.0, Thickness = 1.4, Color = (i / 2) % 2 == 0 ? tyreA : tyreB, Top = tyreTop });
                    else
                    {
                        var c = (i / 5) % 3 == 0 ? AdColors[(i / 15 + s * 3) % AdColors.Length] : Armco;
                        Walls.Add(new Wall { X1 = x0, Y1 = y0, X2 = x1, Y2 = y1, Height = 0.9, Thickness = 0.25, Color = c, Top = Armco });
                    }
                }
            }
        }

        // ---------------------------------------------------------------- pits

        private void PitComplex()
        {
            BarrierWalls();
            int ps = SideIndex(PitSide);
            var apron = Concrete;
            var garageWall = new Rgb(226, 222, 212);
            var garageDoor = new Rgb(46, 50, 60);
            var glass = new Rgb(90, 150, 200);
            var roof = new Rgb(236, 234, 228);
            var truck = new Rgb(238, 238, 240);
            var team = AdColors;
            int garageIndex = 0;
            // Samples along the garages, in pit lane order (the pit straight crosses the line, so not index order).
            var row = new List<KeyValuePair<double, int>>();
            for (int i = 0; i < _n; i++)
            {
                int j = Wrap(i + 1);
                double t0, t1;
                if (!InPitZone(i, out t0) || !InPitZone(j, out t1)) continue;
                double off0 = PitLateral(t0), off1 = PitLateral(t1);
                double in0 = Math.Max(HalfWidth, off0 - 3), in1 = Math.Max(HalfWidth, off1 - 3);
                double out0 = Math.Max(in0, off0 + 3), out1 = Math.Max(in1, off1 + 3);
                Band(i, PitSide * in0, PitSide * out0, PitSide * in1, PitSide * out1, PitAsphalt);
                // Pit lane edge line on the garage side.
                Band(i, PitSide * (out0 - 0.5), PitSide * (out0 - 0.25), PitSide * (out1 - 0.5), PitSide * (out1 - 0.25), Line, Layer.Decal);
                bool mid = t0 > 0.15 && t1 < 0.85;
                if (in0 > HalfWidth + 1.5 && in1 > HalfWidth + 1.5)
                {
                    // Pit wall between track and pit lane.
                    double x0, y0, x1, y1;
                    At(i, PitSide * (HalfWidth + 3.4), out x0, out y0);
                    At(j, PitSide * (HalfWidth + 3.4), out x1, out y1);
                    Walls.Add(new Wall { X1 = x0, Y1 = y0, X2 = x1, Y2 = y1, Height = 1.1, Thickness = 0.6, Color = (i / 6) % 2 == 0 ? new Rgb(210, 210, 214) : AdColors[(i / 12) % AdColors.Length], Top = new Rgb(225, 225, 228) });
                }
                if (!mid || _free[ps][i] < PitTotal) continue;
                row.Add(new KeyValuePair<double, int>(t0, i));
                double apronOut = PitOffsetOuter + 5;
                // Working lane (apron) in front of the garages.
                Band(i, PitSide * out0, PitSide * apronOut, PitSide * out1, PitSide * apronOut, apron.Shade((i / 5) % 2 == 0 ? 0 : -6));
                // Garages: one module every 5 samples (12.5 m), team colour stripe painted on the apron.
                if (i % 5 == 0)
                {
                    var tc = team[garageIndex % team.Length];
                    garageIndex++;
                    double gx, gy;
                    At(i + 2, PitSide * (apronOut + 7), out gx, out gy);
                    double h = _h[Wrap(i + 2)];
                    Box(gx, gy, 0, 12.4, 14, 6, h, garageWall);
                    // Door (dark opening) towards the pit lane and a coloured fascia above it.
                    double fx, fy;
                    At(i + 2, PitSide * (apronOut + 0.05), out fx, out fy);
                    Box(fx, fy, 0, 9.5, 0.3, 4.2, h, garageDoor);
                    Box(fx, fy, 4.4, 11.5, 0.4, 1.1, h, tc);
                    AcrossGeo(_g[i] + 0.8, _g[i] + 11.7, PitSide * (out0 + 0.4), PitSide * (apronOut - 0.4), tc.Shade(-30), Layer.Decal, 0.002);
                }
                // Upper floor with glass and a roof overhang over the whole building.
                BoxAt(i, PitSide * (apronOut + 8), 6, 2.6, 16, 3.2, glass);
                BoxAt(i, PitSide * (apronOut + 7.5), 9.2, 2.6, 18, 0.6, roof);
                // Paddock behind with team trucks.
                double pIn = apronOut + 14, pOut = Math.Min(apronOut + 52, _free[ps][i] - 2);
                if (pOut > pIn + 8)
                {
                    Band(i, PitSide * pIn, PitSide * pOut, PitSide * pIn, PitSide * pOut, Paddock);
                    if (i % 7 == 0)
                        for (int r = 0; r < 2; r++)
                        {
                            double lat = pIn + 8 + r * 14;
                            if (lat + 2 > pOut) break;
                            BoxAt(i, PitSide * lat, 0, 14, 2.6, 3.8, truck);
                            BoxAt(i - 3, PitSide * lat, 0, 2.6, 2.6, 3.2, AdColors[(i / 7 + r) % AdColors.Length]);
                        }
                }
            }
            // Flag poles on the pit roof and a control tower closing the garage row at the pit exit end.
            if (row.Count > 0)
            {
                row.Sort((x, y) => x.Key.CompareTo(y.Key));
                for (int k = 0; k < row.Count; k += 10) Flag(row[k].Value, PitSide * (PitOffsetOuter + 6), 9.8, 6, AdColors[(k / 10) % AdColors.Length]);
                int ti = row[row.Count - 1].Value + 7;
                double tx, ty;
                At(ti, PitSide * (PitOffsetOuter + 12), out tx, out ty);
                double th = _h[Wrap(ti)];
                Box(tx, ty, 0, 14, 14, 20, th, new Rgb(228, 226, 220));
                Box(tx, ty, 20, 15, 15, 4, th, glass);
                Box(tx, ty, 24, 16, 16, 0.8, th, roof);
                // Keep trees out of the whole pit and paddock area.
                for (int k = 0; k < row.Count; k += 8)
                {
                    double x, y;
                    At(row[k].Value, PitSide * (PitOffsetOuter + 30), out x, out y);
                    Clear(x, y, 45);
                }
            }
        }

        private const double PitOffsetOuter = PitOffset + 3;

        /// <summary>Lateral distance of the pit lane centre from the track centre for a pit-lane progress 0..1.</summary>
        public static double PitLateral(double t)
        {
            const double ramp = 0.12;
            if (t < ramp) return HalfWidth + (PitOffset - HalfWidth) * (t / ramp);
            if (t > 1 - ramp) return PitOffset + (HalfWidth - PitOffset) * ((t - (1 - ramp)) / ramp);
            return PitOffset;
        }
        private const double PitTotal = PitOffsetOuter + 30;

        private void Flag(int i, double lateral, double z, double pole, Rgb c)
        {
            double x, y;
            At(i, lateral, out x, out y);
            double h = _h[Wrap(i)];
            Box(x, y, z, 0.25, 0.25, pole, h, new Rgb(220, 220, 224));
            double fx = Math.Cos(h), fy = Math.Sin(h);
            Walls.Add(new Wall { X1 = x, Y1 = y, X2 = x + fx * 2.6, Y2 = y + fy * 2.6, Z = z + pole - 1.7, Height = 1.6, Thickness = 0.05, Color = c, Top = c });
        }

        /// <summary>Start lights gantry over the line.</summary>
        private void Gantry()
        {
            int i = IndexAt(-3);
            double h = _h[i];
            double span = HalfWidth + 2.2;
            var steel = new Rgb(70, 74, 84);
            BoxAt(i, span, 0, 1.0, 1.0, 7.5, steel);
            BoxAt(i, -span, 0, 1.0, 1.0, 7.5, steel);
            double x0, y0, x1, y1;
            At(i, span, out x0, out y0);
            At(i, -span, out x1, out y1);
            Walls.Add(new Wall { X1 = x0, Y1 = y0, X2 = x1, Y2 = y1, Z = 6.2, Height = 1.3, Thickness = 0.8, Color = steel, Top = steel });
            for (int k = 0; k < 5; k++)
            {
                double lat = (k - 2) * 1.6;
                double x, y;
                At(i, lat, out x, out y);
                Box(x - Math.Cos(h) * 0.45, y - Math.Sin(h) * 0.45, 6.35, 0.2, 0.9, 0.9, h, new Rgb(150, 20, 20));
            }
        }

        // ---------------------------------------------------------------- grandstands

        private void Grandstands()
        {
            int opp = -PitSide;
            // Main grandstand opposite the pits along the main straight.
            Stand(-230, 70, opp, true);
            // Stands on the outside of the heaviest braking zones (hairpins and first corner).
            var picks = new List<TrackSegment>();
            foreach (var seg in Track.Segments)
                if (seg.IsCorner && Math.Abs(seg.AngleDeg) > 50 && seg.Start > 250 && seg.Start < Track.Length - 400) picks.Add(seg);
            picks.Sort((a, b) => (b.BrakingIntensity * Math.Abs(b.AngleDeg)).CompareTo(a.BrakingIntensity * Math.Abs(a.AngleDeg)));
            int placed = 0;
            var used = new List<double>();
            foreach (var seg in picks)
            {
                if (placed >= 3) break;
                bool near = false;
                foreach (var u in used) if (Math.Abs(u - seg.Start) < 500) near = true;
                if (near) continue;
                int outside = seg.AngleDeg > 0 ? -1 : 1;
                if (Stand(seg.Start - 70, seg.Start + 10, outside, false) > 2)
                {
                    placed++;
                    used.Add(seg.Start);
                }
            }
        }

        /// <summary>Stepped grandstand modules between two distances on one side; returns how many were placed.</summary>
        private int Stand(double from, double to, int sign, bool roofed)
        {
            int s = SideIndex(sign);
            int count = 0;
            var body = new Rgb(150, 156, 168);
            var roof = new Rgb(232, 232, 236);
            const double module = 12.5;
            double gFrom = Geometry.ToGeometric(from), gTo = Geometry.ToGeometric(to);
            if (gTo < gFrom) gTo += _geoLength;
            for (double g = gFrom; g < gTo; g += module)
            {
                int i = Wrap((int)Math.Round((g + module / 2) / _geoLength * _n));
                double t;
                if (sign == PitSide && InPitZone(i, out t)) continue;
                double barrier = 0;
                for (int k = -6; k <= 6; k++) barrier = Math.Max(barrier, _barrier[s][Wrap(i + k)]);
                double front = barrier + 4;
                const double depth = 16;
                bool room = true;
                for (int k = -6; k <= 6; k += 3) if (_free[s][Wrap(i + k)] < front + depth + 3) room = false;
                if (!room) continue;
                double h = _h[i];
                // Four tiers rising away from the track, each with a row of spectators.
                for (int tier = 0; tier < 4; tier++)
                {
                    double lat = front + 2 + tier * 3.6;
                    double z = 1.2 + tier * 1.9;
                    BoxAt(i, sign * lat, 0, module - 0.6, 3.6, z, body.Shade(tier * 6));
                    for (int c = 0; c < 8; c++)
                    {
                        double along = (c - 3.5) * 1.5;
                        double cx2 = _cx[i] + Math.Cos(h) * along + _nx[i] * sign * lat;
                        double cy2 = _cy[i] + Math.Sin(h) * along + _ny[i] * sign * lat;
                        var col = CrowdColors[(int)(Hash(i * 8 + c, tier) * CrowdColors.Length) % CrowdColors.Length];
                        Box(cx2, cy2, z, 1.2, 1.6, 0.7, h, col);
                    }
                }
                // Back wall and roof.
                BoxAt(i, sign * (front + depth + 0.6), 0, module - 0.6, 1.2, roofed ? 13 : 9, body.Shade(-10));
                // Canopy over the upper tiers only, so the crowd still shows from the usual high camera.
                if (roofed) BoxAt(i, sign * (front + depth * 0.78), 12.6, module, depth * 0.5, 0.6, roof);
                double px, py;
                At(i, sign * (front + depth / 2), out px, out py);
                Clear(px, py, 16);
                if (count % 2 == 0) Flag(i, sign * (front + depth + 1.6), roofed ? 13 : 9, 5, AdColors[(i / 3) % AdColors.Length]);
                count++;
            }
            return count;
        }

        // ---------------------------------------------------------------- nature

        private double U()
        {
            return _rng.NextDouble();
        }

        private void Lakes()
        {
            // A lake or two in some of the open ground (more likely on big circuits).
            int lakes = U() < 0.55 ? 1 + (U() < 0.35 ? 1 : 0) : 0;
            for (int l = 0, tries = 0; l < lakes && tries < 60; tries++)
            {
                double x = MinX + 300 + U() * (MaxX - MinX - 600), y = MinY + 300 + U() * (MaxY - MinY - 600);
                int nearest;
                double dist = DistanceToTrack(x, y, out nearest);
                if (dist < 85 || !IsClear(x, y, 60)) continue;
                double radius = Math.Min(110, dist - 55) * (0.6 + U() * 0.4);
                if (radius < 25) continue;
                const int sides = 28;
                var rr = new double[sides];
                double p1 = U() * 6.28, p2 = U() * 6.28;
                for (int k = 0; k < sides; k++)
                {
                    double a = Math.PI * 2 * k / sides;
                    rr[k] = radius * (1 + 0.18 * Math.Sin(2 * a + p1) + 0.1 * Math.Sin(3 * a + p2));
                }
                for (int k = 0; k < sides; k++)
                {
                    double a0 = Math.PI * 2 * k / sides, a1 = Math.PI * 2 * (k + 1) / sides;
                    int k1 = (k + 1) % sides;
                    double c0 = Math.Cos(a0), s0 = Math.Sin(a0), c1 = Math.Cos(a1), s1 = Math.Sin(a1);
                    // Sandy shore ring, then water.
                    AddFlat(x + c0 * rr[k], y + s0 * rr[k], x + c0 * (rr[k] + 4), y + s0 * (rr[k] + 4), x + c1 * (rr[k1] + 4), y + s1 * (rr[k1] + 4), x + c1 * rr[k1], y + s1 * rr[k1], Shore);
                    AddFlat(x, y, x + c0 * rr[k], y + s0 * rr[k], x + c1 * rr[k1], y + s1 * rr[k1], x + c1 * rr[k1], y + s1 * rr[k1], Water);
                }
                Clear(x, y, radius * 1.3 + 8);
                l++;
            }
        }

        private void PlantTrees()
        {
            double area = (MaxX - MinX) * (MaxY - MinY);
            int clusters = (int)Math.Max(40, Math.Min(170, area / 28000));
            int count = 0;
            for (int c = 0; c < clusters && count < 1500; c++)
            {
                double cx = MinX + U() * (MaxX - MinX), cy = MinY + U() * (MaxY - MinY);
                int n = 6 + _rng.Next(18);
                int kind = U() < 0.5 ? 0 : 1;
                for (int k = 0; k < n; k++)
                {
                    double x = cx + (U() + U() + U() - 1.5) * 46, y = cy + (U() + U() + U() - 1.5) * 46;
                    if (!TreeOk(x, y)) continue;
                    Trees.Add(new TreeSpot { X = x, Y = y, Size = 0.75 + U() * 0.8, Tint = U(), Kind = U() < 0.8 ? kind : 1 - kind });
                    count++;
                }
            }
            // Rows of trees lining the outside of long straights, a little back from the barriers.
            for (int i = 0; i < _n && count < 1800; i += 6)
            {
                var seg = Track.SegmentAt(_d[i]);
                if (seg.IsCorner || seg.Length < 200) continue;
                for (int s = 0; s < 2; s++)
                {
                    if (Hash(i / 30, 300 + s) < 0.55) continue;
                    double lat = _barrier[s][i] + 12 + Hash(i, 301 + s) * 6;
                    double x, y;
                    At(i, (s == 1 ? 1 : -1) * lat, out x, out y);
                    if (!TreeOk(x, y)) continue;
                    Trees.Add(new TreeSpot { X = x, Y = y, Size = 0.8 + Hash(i, 302) * 0.5, Tint = Hash(i, 303), Kind = 1 });
                    count++;
                }
            }
        }

        private bool TreeOk(double x, double y)
        {
            int nearest;
            double dist = DistanceToTrack(x, y, out nearest);
            if (dist < 30) return false;
            if (nearest >= 0 && dist < 75)
            {
                // Which side of the track, and is it beyond that side's barrier and run-off?
                double side = (x - _cx[nearest]) * _nx[nearest] + (y - _cy[nearest]) * _ny[nearest];
                int s = side > 0 ? 1 : 0;
                if (dist < Math.Max(_barrier[s][nearest], _outer[s][nearest]) + 7) return false;
                double t;
                if ((side > 0 ? 1 : -1) == PitSide && InPitZone(nearest, out t) && dist < PitTotal + 25) return false;
            }
            return IsClear(x, y, 4);
        }

        private void Buildings()
        {
            for (int b = 0; b < 40; b++)
            {
                double x = MinX + U() * (MaxX - MinX), y = MinY + U() * (MaxY - MinY);
                int nearest;
                if (DistanceToTrack(x, y, out nearest) < 110 || !IsClear(x, y, 30)) continue;
                double yaw = U() * Math.PI / 2;
                if (U() < 0.3)
                {
                    double w = 14 + U() * 18, d = 14 + U() * 18, h = 10 + U() * 26;
                    var wall = Rgb.Lerp(new Rgb(205, 208, 216), new Rgb(130, 165, 205), U());
                    Box(x, y, 0, w, d, h, yaw, wall);
                    Box(x, y, h, w * 0.9, d * 0.9, 0.8, yaw, new Rgb(235, 235, 240));
                }
                else
                {
                    int houses = 3 + _rng.Next(4);
                    for (int k = 0; k < houses; k++)
                    {
                        double hx = x + (U() - 0.5) * 44, hy = y + (U() - 0.5) * 44;
                        if (DistanceToTrack(hx, hy, out nearest) < 90) continue;
                        double hyaw = yaw + (U() - 0.5) * 0.35;
                        Box(hx, hy, 0, 9, 7, 5, hyaw, new Rgb(238, 236, 228));
                        Box(hx, hy, 5, 9.6, 7.6, 1.4, hyaw, new Rgb(205, 90, 70));
                    }
                }
                Clear(x, y, 34);
            }
        }
    }
}
