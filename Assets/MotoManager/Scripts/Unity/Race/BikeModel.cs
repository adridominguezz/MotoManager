using System.Collections.Generic;
using UnityEngine;

namespace MotoManager.App.Race
{
    /// <summary>A team's bike livery: three colours and a template (see <see cref="BikeModel.BuildBike(MeshBuilder,BikeLivery)"/>).</summary>
    public struct BikeLivery
    {
        /// <summary>Main colour: most of the fairing, the leathers' body.</summary>
        public Color32 Primary;
        /// <summary>Second colour: lower part / stripe / blocks, mudguard, winglets, the leathers' arms and thighs.</summary>
        public Color32 Secondary;
        /// <summary>Accent: pinstripes and details.</summary>
        public Color32 Tertiary;
        /// <summary>Template, 0..<see cref="BikeModel.StyleCount"/> - 1.</summary>
        public int Style;

        public BikeLivery(Color32 primary, Color32 secondary, Color32 tertiary, int style)
        {
            Primary = primary;
            Secondary = secondary;
            Tertiary = tertiary;
            Style = style;
        }
    }

    /// <summary>
    /// Procedural GP bike and tucked rider, measured from a reference MotoGP model scaled to a 1.4 m wheelbase: a tall
    /// flat-topped tail unit over twin silencers, a low seat, the tank hump under the rider's chest, side pods with
    /// vents, a deep belly pan, a tall nose with its screen and winglets; split-spoke wheels with brake discs, gold
    /// fork tubes and a carbon swingarm.
    /// Bike space: pivot on the ground under the middle of the wheelbase, forward = +Z, about 2 m long.
    /// The rider is split into a body and two legs (pivoting at the hips) so that leaning can shift the body to the
    /// inside of the corner and push the inner knee out with transforms only (see <see cref="Pose"/>).
    /// Liveries: the fairing outline of every section is cut at the livery's boundaries (so stripes and splits are
    /// crisp hull edges) and painted per triangle from <see cref="StyleCount"/> templates in three colours; vertex
    /// alpha carries the gloss for the MotoManager/BikeLit shader. About 6k triangles: 2k for the bike, where detail
    /// that does not show at race distance is kept flat or left out, and 4k for the sculpted rider (smooth sweeps for
    /// the limbs and helmet, a shaped hull for the torso, suit panels in the livery's colours).
    /// </summary>
    public static class BikeModel
    {
        /// <summary>Rider root (on the seat) in bike space. Rider meshes are built relative to it.</summary>
        public static readonly Vector3 Seat = new Vector3(0, 0.88f, -0.36f);

        // Right-side joints in bike space (the left side is mirrored).
        private static readonly Vector3 HipR = new Vector3(0.125f, 0.93f, -0.36f);
        private static readonly Vector3 KneeR = new Vector3(0.25f, 0.755f, -0.05f);
        private static readonly Vector3 AnkleR = new Vector3(0.2f, 0.475f, -0.32f);
        private static readonly Vector3 HeelR = new Vector3(0.2f, 0.47f, -0.41f);
        private static readonly Vector3 ToeR = new Vector3(0.18f, 0.415f, -0.2f);
        private static readonly Vector3 ShoulderR = new Vector3(0.18f, 1.115f, 0.03f);
        private static readonly Vector3 ElbowR = new Vector3(0.25f, 0.875f, 0.1f);
        private static readonly Vector3 WristR = new Vector3(0.225f, 0.95f, 0.35f);
        private static readonly Vector3 HelmetCentre = new Vector3(0, 1.18f, 0.3f);

        // Fixed colours. Alpha is the gloss read by the bike shader (255 = lacquered paint, 0 = matte rubber).
        private static readonly Color32 Tyre = new Color32(30, 30, 33, 40);
        private static readonly Color32 Tread = new Color32(24, 24, 27, 20);
        private static readonly Color32 Lettering = new Color32(170, 172, 178, 90);
        private static readonly Color32 WheelFace = new Color32(38, 40, 45, 150);
        private static readonly Color32 Steel = new Color32(176, 180, 188, 220);
        private static readonly Color32 Metal = new Color32(100, 104, 112, 200);
        private static readonly Color32 Gold = new Color32(222, 172, 50, 235);
        private static readonly Color32 Dark = new Color32(28, 29, 33, 150);
        private static readonly Color32 Carbon = new Color32(40, 42, 47, 200);
        private static readonly Color32 SeatPad = new Color32(22, 22, 25, 30);
        private static readonly Color32 Plate = new Color32(242, 242, 238, 200);
        private static readonly Color32 Screen = new Color32(52, 64, 82, 255);
        private static readonly Color32 ScreenTop = new Color32(92, 108, 130, 255);
        private static readonly Color32 Titanium = new Color32(170, 158, 144, 220);
        private static readonly Color32 Visor = new Color32(24, 28, 38, 255);

        /// <summary>Number of livery templates (same as Core's TeamColors.StyleCount).</summary>
        public const int StyleCount = 8;

        // Fairing rings, tail -> nose: z, bottom y, top y, half widths (bottom, lower, mid, upper, top), crown.
        // One hull from the tail unit over the seat, tank and side pods to the nose (the screen is a second hull).
        private static readonly float[][] Fairing =
        {
            new[] { -0.87f, 0.94f, 1.05f, 0.05f, 0.08f, 0.1f, 0.11f, 0.1f, 0f },            // 0 tail end (blunt)
            new[] { -0.78f, 0.905f, 1.055f, 0.07f, 0.11f, 0.14f, 0.15f, 0.13f, 0.005f },     // 1 tail unit
            new[] { -0.64f, 0.88f, 1.05f, 0.08f, 0.12f, 0.15f, 0.16f, 0.14f, 0.005f },       // 2
            new[] { -0.53f, 0.86f, 1.04f, 0.08f, 0.12f, 0.15f, 0.155f, 0.13f, 0.005f },      // 3 hump behind the seat
            new[] { -0.47f, 0.72f, 0.87f, 0.075f, 0.11f, 0.14f, 0.145f, 0.12f, 0.005f },     // 4 seat
            new[] { -0.36f, 0.62f, 0.85f, 0.07f, 0.11f, 0.14f, 0.14f, 0.12f, 0.005f },       // 5
            new[] { -0.31f, 0.32f, 0.85f, 0.08f, 0.14f, 0.16f, 0.15f, 0.13f, 0.005f },       // 6 rear of the side panels
            new[] { -0.25f, 0.19f, 0.86f, 0.09f, 0.16f, 0.18f, 0.16f, 0.13f, 0.01f },        // 7
            new[] { -0.15f, 0.165f, 0.92f, 0.1f, 0.19f, 0.21f, 0.18f, 0.14f, 0.015f },       // 8 tank rear
            new[] { -0.04f, 0.155f, 0.975f, 0.11f, 0.22f, 0.25f, 0.2f, 0.15f, 0.02f },       // 9
            new[] { 0.08f, 0.15f, 1.0f, 0.11f, 0.24f, 0.28f, 0.215f, 0.15f, 0.02f },         // 10 side pods, tank hump
            new[] { 0.18f, 0.145f, 0.995f, 0.11f, 0.235f, 0.275f, 0.225f, 0.15f, 0.02f },    // 11
            new[] { 0.3f, 0.15f, 0.965f, 0.105f, 0.21f, 0.265f, 0.235f, 0.15f, 0.015f },     // 12 side vents
            new[] { 0.37f, 0.18f, 0.94f, 0.1f, 0.2f, 0.26f, 0.24f, 0.15f, 0.015f },          // 13 belly pan front
            new[] { 0.43f, 0.5f, 0.95f, 0.12f, 0.21f, 0.255f, 0.24f, 0.16f, 0.01f },         // 14 behind the front wheel
            new[] { 0.5f, 0.6f, 1.0f, 0.12f, 0.2f, 0.245f, 0.235f, 0.165f, 0.01f },          // 15
            new[] { 0.58f, 0.645f, 1.0f, 0.11f, 0.185f, 0.225f, 0.215f, 0.15f, 0.01f },      // 16 winglets
            new[] { 0.67f, 0.675f, 0.935f, 0.09f, 0.145f, 0.175f, 0.17f, 0.12f, 0.005f },    // 17
            new[] { 0.76f, 0.7f, 0.84f, 0.055f, 0.09f, 0.105f, 0.1f, 0.07f, 0f },            // 18
            new[] { 0.84f, 0.72f, 0.775f, 0.02f, 0.03f, 0.035f, 0.03f, 0.02f, 0f }            // 19 nose tip (air intake)
        };

        // Screen rings, back -> front (same layout as the fairing).
        private static readonly float[][] ScreenRings =
        {
            new[] { 0.4f, 0.92f, 1.03f, 0.13f, 0.135f, 0.125f, 0.1f, 0.05f, 0.005f },
            new[] { 0.48f, 0.9f, 1.155f, 0.145f, 0.15f, 0.14f, 0.11f, 0.06f, 0.01f },
            new[] { 0.56f, 0.88f, 1.19f, 0.145f, 0.15f, 0.14f, 0.11f, 0.06f, 0.01f },
            new[] { 0.65f, 0.86f, 1.09f, 0.125f, 0.13f, 0.12f, 0.09f, 0.05f, 0.005f },
            new[] { 0.74f, 0.83f, 0.93f, 0.08f, 0.08f, 0.07f, 0.05f, 0.03f, 0f },
            new[] { 0.81f, 0.79f, 0.81f, 0.03f, 0.03f, 0.025f, 0.02f, 0.01f, 0f }
        };

        public static void BuildBike(MeshBuilder b, Color32 p, Color32 s)
        {
            BuildBike(b, new BikeLivery(p, s, Dark, 0));
        }

        // ------------------------------------------------------------------ fairing and livery

        /// <summary>Half outline of a fairing section (8 points, bottom centre to top centre): flat bottom, a chamfer,
        /// widest a little above mid-height, a rounded shoulder and a narrower top. Contour coordinate c runs 0..7 along it.</summary>
        private static Vector2[] FairingShape(float[] r)
        {
            float yb = r[1], yt = r[2], h = yt - yb;
            return new[]
            {
                new Vector2(0, yb), new Vector2(r[3], yb), new Vector2(Mathf.Lerp(r[3], r[4], 0.72f), yb + 0.16f * h),
                new Vector2(r[4], yb + 0.38f * h), new Vector2(r[5], yb + 0.56f * h), new Vector2(r[6], yb + 0.8f * h),
                new Vector2(r[7], yt), new Vector2(0, yt + r[8])
            };
        }

        /// <summary>Point at contour coordinate <paramref name="c"/>: a Catmull-Rom curve through the outline's points,
        /// so resampling with more bands rounds the section. The ends mirror their neighbour across the centre line,
        /// which keeps the bottom and the crown flat where the two halves meet.</summary>
        private static Vector2 At(Vector2[] pts, float c)
        {
            int n = pts.Length;
            int k = Mathf.Clamp((int)Mathf.Floor(c), 0, n - 2);
            float f = Mathf.Clamp01(c - k);
            var p1 = pts[k];
            var p2 = pts[k + 1];
            var p0 = k > 0 ? pts[k - 1] : new Vector2(-pts[1].x, pts[1].y);
            var p3 = k + 2 < n ? pts[k + 2] : new Vector2(-pts[n - 2].x, pts[n - 2].y);
            float f2 = f * f, f3 = f2 * f;
            var q = (p1 * 2f + (p2 - p0) * f + (p0 * 2f - p1 * 5f + p2 * 4f - p3) * f2 + (p1 * 3f - p0 - p2 * 3f + p3) * f3) * 0.5f;
            return new Vector2(Mathf.Max(0, q.x), q.y);
        }

        private static float Len(Vector2 a, Vector2 b)
        {
            var d = b - a;
            return Mathf.Sqrt(d.x * d.x + d.y * d.y);
        }

        /// <summary>Arc length along the outline up to contour coordinate <paramref name="c"/>.</summary>
        private static float ArcOf(Vector2[] pts, float c)
        {
            float acc = 0;
            for (int k = 0; k + 1 < pts.Length; k++)
            {
                if (c <= k) break;
                float f = Mathf.Min(1, c - k);
                acc += Len(pts[k], pts[k + 1]) * f;
            }
            return acc;
        }

        /// <summary>Contour coordinate at arc length <paramref name="arc"/> (clamped to the outline).</summary>
        private static float ContourAt(Vector2[] pts, float arc)
        {
            float acc = 0;
            for (int k = 0; k + 1 < pts.Length; k++)
            {
                float l = Len(pts[k], pts[k + 1]);
                if (acc + l >= arc) return k + (l > 1e-6f ? (arc - acc) / l : 0);
                acc += l;
            }
            return pts.Length - 1;
        }

        /// <summary>Piecewise-linear lookup in (z, value) pairs sorted by z.</summary>
        private static float Curve(float z, float[] zv)
        {
            if (z <= zv[0]) return zv[1];
            for (int i = 2; i + 1 < zv.Length; i += 2)
                if (z <= zv[i]) return Mathf.Lerp(zv[i - 1], zv[i + 1], (z - zv[i - 2]) / (zv[i] - zv[i - 2]));
            return zv[zv.Length - 1];
        }

        // Livery boundaries as (z, contour) curves, tail -> nose.
        private static readonly float[] SplitLow = { -0.87f, 2.6f, -0.6f, 2.3f, -0.45f, 2.4f, -0.3f, 2.9f, 0.1f, 3.0f, 0.45f, 3.5f, 0.84f, 4.5f };
        private static readonly float[] SplitHigh = { -0.87f, 2.6f, -0.6f, 2.3f, -0.45f, 2.6f, -0.3f, 3.85f, 0.84f, 3.85f };
        private static readonly float[] Swoosh = { -0.87f, 5.0f, -0.6f, 5.2f, -0.3f, 4.6f, 0.1f, 3.2f, 0.45f, 2.2f, 0.84f, 1.8f };

        private const float Pinstripe = 0.028f;

        /// <summary>
        /// Every livery cuts each fairing section in three runs along its outline: below the first boundary, a stripe
        /// between the two boundaries, above the second. Boundaries are vertices of the hull, so lines stay crisp.
        /// Returns the two boundaries (contour coordinates) and the bands of the lower and upper runs.
        /// </summary>
        private static void Cuts(int style, float z, Vector2[] pts, out float c1, out float c2, out int below, out int above)
        {
            float total = ArcOf(pts, pts.Length - 1);
            float width = Pinstripe;
            below = 3;
            above = 5;
            switch (style)
            {
                case 0: c1 = Curve(z, SplitLow); break;
                case 1: c1 = Curve(z, SplitHigh); break;
                case 2: c1 = z < -0.45f ? 2.6f : 3.5f; width = 0.085f; break;
                case 3: c1 = Curve(z, Swoosh); width = 0.12f; break;
                case 6: c1 = z < -0.45f ? 3.3f : 3.6f; break;
                case 7:
                    c1 = ContourAt(pts, total - 0.095f);
                    width = 0.026f;
                    below = 6;
                    above = 2;
                    break;
                default: c1 = 2.05f; below = 2; above = 6; break; // 4 blocks, 5 tank band: a low pinstripe over the belly
            }
            c1 = Mathf.Clamp(c1, 0.6f, 6.6f);
            c2 = Mathf.Min(ContourAt(pts, ArcOf(pts, c1) + width), 6.95f);
        }

        /// <summary>Resampled outline: <paramref name="n"/> bands evenly from c0 to c1, snapping to the shape's corners when close.</summary>
        private static void Run(Vector2[] pts, float c0, float c1, int n, List<Vector2> outPts, List<float> outC, bool last)
        {
            float step = (c1 - c0) / n;
            for (int k = 0; k < n + (last ? 1 : 0); k++)
            {
                float c = c0 + step * k;
                if (k > 0 && k < n)
                {
                    float near = Mathf.Round(c);
                    if (Mathf.Abs(near - c) < step * 0.4f && near > c0 + step * 0.3f && near < c1 - step * 0.3f) c = near;
                }
                outPts.Add(At(pts, c));
                outC.Add(c);
            }
        }

        /// <summary>
        /// The bike in a livery: fairing (cut along the livery's boundaries and painted per triangle), screen,
        /// winglets, front mudguard, wheels, brakes, fork and clip-ons, swingarm, engine, footpegs and the twin
        /// silencers under the tail unit. Number plates, vents and the seat pad keep fixed colours on every style.
        /// </summary>
        public static void BuildBike(MeshBuilder b, BikeLivery livery)
        {
            int style = ((livery.Style % StyleCount) + StyleCount) % StyleCount;
            Color32 p = livery.Primary, s = livery.Secondary, t = livery.Tertiary;
            int n = Fairing.Length;
            var rings = new MeshBuilder.Ring[n];
            var contour = new float[n][];
            var zs = new float[n];
            int stripeBand = 0;
            for (int i = 0; i < n; i++)
            {
                var row = Fairing[i];
                var pts = FairingShape(row);
                float c1, c2;
                int below, above;
                Cuts(style, row[0], pts, out c1, out c2, out below, out above);
                var outPts = new List<Vector2>();
                var outC = new List<float>();
                Run(pts, 0, c1, below, outPts, outC, false);
                Run(pts, c1, c2, 1, outPts, outC, false);
                Run(pts, c2, pts.Length - 1, above, outPts, outC, true);
                rings[i] = new MeshBuilder.Ring(row[0], outPts.ToArray(), null);
                contour[i] = outC.ToArray();
                zs[i] = row[0];
                stripeBand = below;
            }

            b.Hull(rings, Dark, Dark, (ring, band, tri) =>
            {
                // Triangle 0 sits nearer ring s and higher in the band, triangle 1 nearer ring s + 1 and lower.
                float u = tri == 0 ? 1f / 3f : 2f / 3f;
                float z = Mathf.Lerp(zs[ring], zs[ring + 1], u);
                float cLo = Mathf.Lerp(contour[ring][band], contour[ring + 1][band], u);
                float cHi = Mathf.Lerp(contour[ring][band + 1], contour[ring + 1][band + 1], u);
                float c = Mathf.Lerp(cLo, cHi, tri == 0 ? 2f / 3f : 1f / 3f);
                float cq = (contour[ring][band] + contour[ring][band + 1] + contour[ring + 1][band] + contour[ring + 1][band + 1]) * 0.25f;
                int region = band < stripeBand ? 0 : band == stripeBand ? 1 : 2;
                var col = Paint(style, p, s, t, region, ring, z, c, cq);
                return Fixed(col, z, c);
            });

            // Side vents: two slanted slots high on each side pod and a gill lower down, as dark plates on the surface.
            for (int side = -1; side <= 1; side += 2)
            {
                Vent(b, side, 0.25f, 0.6f, 0.15f, 0.026f, 24f);
                Vent(b, side, 0.32f, 0.63f, 0.15f, 0.026f, 24f);
                Vent(b, side, 0.12f, 0.33f, 0.022f, 0.12f, 0f);
            }

            var screen = new MeshBuilder.Ring[ScreenRings.Length];
            for (int i = 0; i < ScreenRings.Length; i++)
                screen[i] = new MeshBuilder.Ring(ScreenRings[i][0], ScreenShape(ScreenRings[i]), new[] { Screen, Screen, Screen, Screen, ScreenTop, ScreenTop });
            b.Hull(screen, null, null);

            // Winglets on the nose sides (a swept main plane with an end plate), and the front mudguard, in the second colour.
            Color32 wing = style == 7 ? p : s;
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(new Vector3(side * 0.24f, 0.8f, 0.6f), new Vector3(0.09f, 0.014f, 0.13f), wing, Quaternion.Euler(-8, side * -12, side * -10));
                b.Box(new Vector3(side * 0.283f, 0.79f, 0.59f), new Vector3(0.008f, 0.05f, 0.11f), wing, Quaternion.Euler(-8, 0, 0));
            }
            // Mudguard: short plates along an arc over the tyre (boxes, so the faces light correctly all round).
            float plate = 0.345f * 124f * Mathf.Deg2Rad / 4f * 1.08f;
            for (int k = 0; k < 4; k++)
            {
                float deg = Mathf.Lerp(-60f, 64f, (k + 0.5f) / 4f);
                float a = deg * Mathf.Deg2Rad;
                var at = FrontAxle + new Vector3(0, Mathf.Cos(a), Mathf.Sin(a)) * 0.345f;
                b.Box(at, new Vector3(k == 0 || k == 3 ? 0.125f : 0.14f, 0.014f, plate), s, Quaternion.Euler(deg, 0, 0));
            }

            // Wheels: rounded tyres with lettered sidewalls, dark split-spoke rims, brake discs and gold calipers.
            Wheel(b, FrontAxle, FrontRadius, 0.125f);
            Wheel(b, RearAxle, RearRadius, 0.19f);
            for (int side = -1; side <= 1; side += 2)
            {
                b.DiscX(FrontAxle + new Vector3(side * 0.07f, 0, 0), 0.115f, 0.165f, side, Steel, WheelSides);
                b.Box(FrontAxle + new Vector3(side * 0.08f, 0.12f, -0.09f), new Vector3(0.03f, 0.11f, 0.05f), Gold, Quaternion.Euler(-38, 0, 0));
            }
            b.DiscX(RearAxle + new Vector3(-0.1f, 0, 0), 0.06f, 0.11f, -1, Steel, WheelSides);
            b.Box(RearAxle + new Vector3(-0.11f, 0.06f, 0.09f), new Vector3(0.026f, 0.05f, 0.08f), Gold, Quaternion.Euler(30, 0, 0));

            // Fork: two legs along a 24 degree rake, carbon-black lower legs to the axle, gold tubes above them up to
            // the fairing, triple clamp and clip-ons with dark grips and levers.
            var rake = new Vector3(0, Mathf.Cos(24 * Mathf.Deg2Rad), -Mathf.Sin(24 * Mathf.Deg2Rad));
            for (int side = -1; side <= 1; side += 2)
            {
                var axle = FrontAxle + new Vector3(side * 0.1f, 0, 0);
                b.Tube(axle - rake * 0.035f, axle + rake * 0.2f, 0.03f, 0.03f, Carbon, 6, true);
                b.Tube(axle + rake * 0.19f, axle + rake * 0.42f, 0.026f, 0.026f, Gold, 6, false);
                b.Tube(axle + rake * 0.41f, axle + rake * 0.7f, 0.031f, 0.031f, Dark, 6, false);
                var clamp = axle + rake * 0.67f;
                b.Tube(new Vector3(side * 0.09f, clamp.y, clamp.z), new Vector3(side * 0.2f, clamp.y - 0.016f, clamp.z - 0.022f), 0.012f, 0.012f, Metal, 4, false);
                b.Tube(new Vector3(side * 0.2f, clamp.y - 0.016f, clamp.z - 0.022f), new Vector3(side * 0.31f, clamp.y - 0.026f, clamp.z - 0.037f), 0.02f, 0.02f, Dark, 5, true);
                b.Box(new Vector3(side * 0.25f, clamp.y - 0.005f, clamp.z + 0.06f), new Vector3(0.1f, 0.012f, 0.025f), Metal, Quaternion.Euler(0, side * 12, 0));
            }
            var top = FrontAxle + rake * 0.67f;
            b.Box(new Vector3(0, top.y, top.z), new Vector3(0.24f, 0.03f, 0.07f), Metal, Quaternion.Euler(-24, 0, 0));
            b.Tube(FrontAxle - new Vector3(0.13f, 0, 0), FrontAxle + new Vector3(0.13f, 0, 0), 0.02f, 0.02f, Metal, 4, false);

            // Carbon swingarm (deep at the pivot, tapering to the axle), rear axle, engine block and frame spars
            // behind the side panels, the shock and the footpegs.
            for (int side = -1; side <= 1; side += 2)
            {
                b.Loft(new[]
                {
                    new MeshBuilder.Section(-0.72f, RearAxle.y, 0.025f, 0.035f, Carbon),
                    new MeshBuilder.Section(-0.45f, 0.38f, 0.03f, 0.06f, Carbon),
                    new MeshBuilder.Section(-0.24f, 0.44f, 0.035f, 0.075f, Carbon)
                }, 6, null, new Vector3(side * 0.115f, 0, 0));
                var peg = new Vector3(side * 0.17f, 0.43f, -0.38f);
                b.Tube(peg - new Vector3(side * 0.06f, 0, 0), peg + new Vector3(side * 0.04f, 0, 0), 0.012f, 0.012f, Metal, 4, true);
            }
            b.Tube(RearAxle - new Vector3(0.14f, 0, 0), RearAxle + new Vector3(0.14f, 0, 0), 0.02f, 0.02f, Metal, 4, false);
            b.Box(new Vector3(0, 0.46f, -0.2f), new Vector3(0.2f, 0.36f, 0.3f), Dark, Quaternion.Euler(-12, 0, 0));
            b.Tube(new Vector3(0, 0.5f, -0.33f), new Vector3(0, 0.78f, -0.46f), 0.03f, 0.03f, Gold, 4, false);

            // Twin silencers under the tail unit, fed by a header pipe from under the engine.
            for (int side = -1; side <= 1; side += 2)
            {
                var front = new Vector3(side * 0.12f, 0.69f, -0.5f);
                b.Tube(front, new Vector3(side * 0.085f, 0.87f, -0.85f), 0.042f, 0.048f, Titanium, 7, true, Dark);
                b.Tube(new Vector3(side * 0.07f, 0.24f, -0.06f), new Vector3(side * 0.11f, 0.3f, -0.3f), 0.03f, 0.03f, Titanium, 5, false);
                b.Tube(new Vector3(side * 0.11f, 0.3f, -0.3f), front + new Vector3(0, 0, 0.02f), 0.03f, 0.036f, Titanium, 5, false);
            }
        }

        private static readonly Vector3 FrontAxle = new Vector3(0, 0.305f, 0.7f);
        private static readonly Vector3 RearAxle = new Vector3(0, 0.32f, -0.7f);
        private const float FrontRadius = 0.305f;
        private const float RearRadius = 0.32f;

        /// <summary>The colour of a fairing triangle for a livery template, before the fixed parts.</summary>
        private static Color32 Paint(int style, Color32 p, Color32 s, Color32 t, int region, int ring, float z, float c, float cq)
        {
            switch (style)
            {
                case 0:
                case 1:
                    return region == 0 ? s : region == 1 ? t : p;
                case 2:
                case 3:
                    if (region == 1) return s;
                    return c < 1 ? t : p;
                case 4:
                    if (region == 0) return s;
                    if (region == 1) return t;
                    // Blocks: whole quads alternate over the side panels.
                    if (z > -0.42f && z < 0.5f && cq < 5.6f)
                        return (ring + (int)Mathf.Floor(cq * 1.2f)) % 2 == 0 ? s : p;
                    return p;
                case 5:
                    if (region == 0) return s;
                    if (region == 1) return t;
                    return z > -0.2f && z < 0.16f ? s : p;
                case 6:
                    if (region == 1) return t;
                    // Stepped fade: the boundary leans forward as it rises.
                    return z < -0.42f + 0.085f * c ? s : p;
                default:
                    if (region == 1) return t;
                    if (region == 2) return s;
                    return c < 1 ? s : p;
            }
        }

        /// <summary>Parts every livery keeps: nose intake, number plates, seat pad, carbon belly.</summary>
        private static Color32 Fixed(Color32 col, float z, float c)
        {
            if (z > 0.79f) return c < 3.6f ? Dark : col;                               // air intake under the nose tip
            if (z > 0.6f && z < 0.72f && c > 3.7f && c < 5.2f) return Plate;          // front number plate on the nose sides
            if (z < -0.74f && c > 2.9f && c < 6.1f) return Plate;                     // tail number plate
            if (z > -0.47f && z < -0.25f && c > 5.05f) return SeatPad;                // seat pad
            if (z > -0.31f && z < 0.37f && c < 1) return Color32.Lerp(col, Dark, 0.55f); // carbon belly pan
            return col;
        }

        /// <summary>Half width of the fairing hull at (z, y), on its outer side (0 outside its height range).</summary>
        private static float SurfaceX(float z, float y)
        {
            int i = 0;
            while (i + 2 < Fairing.Length && Fairing[i + 1][0] < z) i++;
            float f = Mathf.Clamp01((z - Fairing[i][0]) / (Fairing[i + 1][0] - Fairing[i][0]));
            return Mathf.Lerp(OutlineX(FairingShape(Fairing[i]), y), OutlineX(FairingShape(Fairing[i + 1]), y), f);
        }

        private static float OutlineX(Vector2[] pts, float y)
        {
            float best = 0;
            var prev = At(pts, 0);
            for (float c = 0.1f; c <= pts.Length - 1 + 1e-3f; c += 0.1f)
            {
                var q = At(pts, c);
                if ((prev.y - y) * (q.y - y) <= 0 && Mathf.Abs(q.y - prev.y) > 1e-5f)
                    best = Mathf.Max(best, Mathf.Lerp(prev.x, q.x, (y - prev.y) / (q.y - prev.y)));
                prev = q;
            }
            return best;
        }

        /// <summary>A dark vent slot (a flat panel) of height h and length l on the fairing side at (z, y), tilted forward by tilt degrees.</summary>
        private static void Vent(MeshBuilder b, int side, float z, float y, float h, float l, float tilt)
        {
            float x = SurfaceX(z, y);
            // Follow the surface: yaw from the hull's taper along Z, roll from its slope with height.
            float yaw = Mathf.Atan2(SurfaceX(z + 0.03f, y) - SurfaceX(z - 0.03f, y), 0.06f) * Mathf.Rad2Deg;
            float roll = Mathf.Atan2(SurfaceX(z, y + 0.03f) - SurfaceX(z, y - 0.03f), 0.06f) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0, side * yaw, -side * roll) * Quaternion.Euler(tilt, 0, 0);
            var n = rot * new Vector3(side, 0, 0);
            b.Panel(new Vector3(side * x, y, z) + n * 0.003f, rot * new Vector3(0, h * 0.5f, 0), rot * new Vector3(0, 0, l * 0.5f), n, Dark);
        }

        /// <summary>Half outline of a screen section: flat bottom, widest a little above mid-height, narrower top.</summary>
        private static Vector2[] ScreenShape(float[] r)
        {
            float yb = r[1], yt = r[2], h = yt - yb;
            return new[]
            {
                new Vector2(0, yb), new Vector2(r[3], yb), new Vector2(r[4], yb + 0.38f * h), new Vector2(r[5], yb + 0.52f * h),
                new Vector2(r[6], yb + 0.8f * h), new Vector2(r[7], yt), new Vector2(0, yt + r[8])
            };
        }

        /// <summary>
        /// Tyre (sidewall with a dashed band of lettering, rounded shoulders, tread), the rim's flanges and barrel,
        /// eight split spokes (flat panels) and the hub.
        /// </summary>
        private static void Wheel(MeshBuilder b, Vector3 c, float r, float w)
        {
            var profile = new[]
            {
                new Vector2(0.7f * r, -0.36f * w), new Vector2(0.88f * r, -0.5f * w), new Vector2(r, -0.2f * w),
                new Vector2(r, 0.2f * w), new Vector2(0.88f * r, 0.5f * w), new Vector2(0.7f * r, 0.36f * w)
            };
            b.Lathe(c, profile, new[] { Tyre, Tyre, Tread, Tyre, Tyre }, WheelSides, (band, seg) =>
            {
                if (band != 0 && band != 4) return null;
                // Lettering: light dashes around the sidewall, the rest plain rubber.
                return seg % 7 == 1 || seg % 7 == 4 ? Lettering : Tyre;
            });
            float rim = 0.6f * r;
            b.DiscX(c + new Vector3(0.35f * w, 0, 0), rim, 0.71f * r, 1, WheelFace, WheelSides);
            b.DiscX(c - new Vector3(0.35f * w, 0, 0), rim, 0.71f * r, -1, WheelFace, WheelSides);
            b.Lathe(c, new[] { new Vector2(rim, 0.35f * w), new Vector2(rim, -0.35f * w) }, new[] { WheelFace }, WheelSides);
            for (int k = 0; k < 8; k++)
            {
                // Split spokes: pairs that join at the hub.
                float deg = k * 45f + (k % 2 == 0 ? -7f : 7f);
                var d = new Vector3(0, Mathf.Sin(deg * Mathf.Deg2Rad), Mathf.Cos(deg * Mathf.Deg2Rad));
                float r0 = 0.07f, r1 = rim + 0.005f;
                // Flat spokes: one panel on each face (edges are invisible at race distance).
                var mid = c + d * ((r0 + r1) * 0.5f);
                var across = new Vector3(0, d.z, -d.y) * 0.009f;
                for (int face = -1; face <= 1; face += 2)
                    b.Panel(mid + new Vector3(face * 0.008f, 0, 0), d * ((r1 - r0) * 0.5f), across, new Vector3(face, 0, 0), WheelFace);
            }
            b.Tube(c - new Vector3(0.3f * w, 0, 0), c + new Vector3(0.3f * w, 0, 0), 0.07f, 0.07f, WheelFace, 6, true, Metal);
        }

        private const int WheelSides = 14;

        private static Vector3 Mirror(Vector3 v, int side)
        {
            return new Vector3(v.x * side, v.y, v.z);
        }

        /// <summary>Rider torso, head and arms, relative to <see cref="Seat"/>. Leathers in the team colours.</summary>
        public static void BuildRiderBody(MeshBuilder b, Color32 p, Color32 s, Color32 helmet)
        {
            BuildRiderBody(b, new BikeLivery(p, s, Dark, 0), helmet);
        }

        /// <summary>Leather sheen: livery colours with a soft highlight (see the bike shader's gloss).</summary>
        private static Color32 Leather(Color32 c)
        {
            return new Color32(c.r, c.g, c.b, 90);
        }

        // Torso rings, seat -> collar: z, bottom y, top y, crown, half widths (bottom, belly, waist, ribs, shoulder blades, top).
        private static readonly float[][] Torso =
        {
            new[] { -0.49f, 0.93f, 1.0f, 0f, 0.03f, 0.06f, 0.075f, 0.075f, 0.065f, 0.04f },          // seat end
            new[] { -0.465f, 0.88f, 1.05f, 0.005f, 0.06f, 0.12f, 0.145f, 0.15f, 0.135f, 0.08f },
            new[] { -0.42f, 0.86f, 1.09f, 0.01f, 0.08f, 0.15f, 0.17f, 0.17f, 0.155f, 0.1f },         // hips on the seat
            new[] { -0.34f, 0.855f, 1.135f, 0.01f, 0.09f, 0.16f, 0.18f, 0.18f, 0.165f, 0.11f },
            new[] { -0.25f, 0.865f, 1.17f, 0.012f, 0.09f, 0.165f, 0.19f, 0.195f, 0.175f, 0.115f },  // waist
            new[] { -0.15f, 0.895f, 1.2f, 0.015f, 0.09f, 0.17f, 0.2f, 0.205f, 0.185f, 0.12f },
            new[] { -0.05f, 0.93f, 1.23f, 0.02f, 0.09f, 0.17f, 0.205f, 0.21f, 0.19f, 0.12f },        // ribs
            new[] { 0.04f, 0.96f, 1.255f, 0.025f, 0.085f, 0.165f, 0.2f, 0.21f, 0.19f, 0.115f },      // shoulder blades, aero hump
            new[] { 0.11f, 0.98f, 1.265f, 0.02f, 0.08f, 0.15f, 0.18f, 0.19f, 0.17f, 0.1f },
            new[] { 0.17f, 1.0f, 1.24f, 0.01f, 0.065f, 0.12f, 0.14f, 0.145f, 0.13f, 0.08f },        // chest on the tank
            new[] { 0.215f, 1.03f, 1.18f, 0.005f, 0.04f, 0.07f, 0.085f, 0.085f, 0.075f, 0.05f }     // collar
        };

        private const int TorsoBands = 20;

        /// <summary>
        /// Rounded joint for a limb sweep: a quadratic Bezier from <paramref name="d"/> before the corner (along
        /// <paramref name="inDir"/>) to d after it (along <paramref name="outDir"/>), so the sections never fold over on
        /// the inside of the bend. Adds steps + 1 points, radii blending from r0 to r1.
        /// </summary>
        private static void Bend(List<Vector3> path, List<Vector2> radii, Vector3 corner, Vector3 inDir, Vector3 outDir, float d, Vector2 r0, Vector2 r1, int steps)
        {
            var p0 = corner - inDir * d;
            var p2 = corner + outDir * d;
            for (int k = 0; k <= steps; k++)
            {
                float u = (float)k / steps;
                path.Add(p0 * ((1 - u) * (1 - u)) + corner * (2 * u * (1 - u)) + p2 * (u * u));
                radii.Add(r0 + (r1 - r0) * u);
            }
        }

        /// <summary>Half outline of a torso section (8 points, bottom centre to top centre, contour 0..7).</summary>
        private static Vector2[] TorsoShape(float[] r)
        {
            float yb = r[1], yt = r[2], h = yt - yb;
            return new[]
            {
                new Vector2(0, yb), new Vector2(r[4], yb), new Vector2(r[5], yb + 0.18f * h), new Vector2(r[6], yb + 0.42f * h),
                new Vector2(r[7], yb + 0.66f * h), new Vector2(r[8], yb + 0.86f * h), new Vector2(r[9], yt), new Vector2(0, yt + r[3])
            };
        }

        /// <summary>
        /// Rider torso, head and arms in the livery's leathers, like a race suit: main colour over the back, shoulders
        /// and upper arms, second colour on the flanks, elbows and forearms with an accent line along the panel edges,
        /// dark gloves with accent knuckles. The helmet colour is given; its centre stripe is the livery's accent.
        /// </summary>
        public static void BuildRiderBody(MeshBuilder b, BikeLivery livery, Color32 helmet)
        {
            var o = -Seat;
            Color32 p = Leather(livery.Primary), s = Leather(livery.Secondary), t = Leather(livery.Tertiary);

            // Torso: hips on the seat, back rising to the aero hump between the shoulder blades, chest on the tank.
            var rings = new MeshBuilder.Ring[Torso.Length];
            for (int i = 0; i < Torso.Length; i++)
            {
                var shape = TorsoShape(Torso[i]);
                var half = new Vector2[TorsoBands + 1];
                for (int k = 0; k <= TorsoBands; k++) half[k] = At(shape, 7f * k / TorsoBands) + new Vector2(0, o.y);
                rings[i] = new MeshBuilder.Ring(Torso[i][0] + o.z, half, null);
            }
            b.Hull(rings, s, Dark, (ring, band, tri) =>
            {
                float z = Torso[ring][0];
                if (z < -0.47f || band < 9) return s;                     // seat panel, flanks and belly
                return band == 9 ? t : p;                                 // accent line along the flank panel, back
            });
            // Neck (balaclava) between the collar and the helmet.
            b.Loft(new[]
            {
                new MeshBuilder.Section(0.17f, 1.12f, 0.055f, 0.05f, Dark),
                new MeshBuilder.Section(0.25f, 1.09f, 0.05f, 0.045f, Dark)
            }, 8, null, o);

            // Helmet: an egg tilted forward and down (chin almost on the tank), visor on the front, accent centre stripe,
            // and the aero spoiler on the back of the shell.
            var axis = new Vector3(0, -0.6f, 0.8f);
            var hPath = new List<Vector3>();
            var hRadii = new List<Vector2>();
            const int hn = 16;
            for (int k = 0; k < hn; k++)
            {
                float u = -0.97f * Mathf.Cos(Mathf.PI * k / (hn - 1));
                float r = Mathf.Sqrt(Mathf.Max(0.0f, 1 - u * u));
                hPath.Add(HelmetCentre + axis * (0.175f * u) + o);
                hRadii.Add(new Vector2(0.13f * r + 0.004f, 0.14f * r + 0.004f));
            }
            var stripe = new Color32(livery.Tertiary.r, livery.Tertiary.g, livery.Tertiary.b, helmet.a);
            b.Sweep(hPath, hRadii, 18, (i, dir) =>
            {
                if (i >= hn / 2 - 1 && dir.z > 0.2f && dir.y > -0.25f && dir.y < 0.5f) return Visor;
                if (Mathf.Abs(dir.x) < 0.16f && dir.y > 0.1f && i < hn - 3) return stripe;
                return helmet;
            }, Vector3.up);
            b.Loft(new[]
            {
                new MeshBuilder.Section(0.12f, 1.22f, 0.015f, 0.012f, helmet),
                new MeshBuilder.Section(0.17f, 1.29f, 0.065f, 0.022f, helmet),
                new MeshBuilder.Section(0.23f, 1.325f, 0.05f, 0.012f, helmet)
            }, 8, null, o);

            for (int side = -1; side <= 1; side += 2)
            {
                var sh = Mirror(ShoulderR, side) + o;
                var el = Mirror(ElbowR, side) + o;
                var wr = Mirror(WristR, side) + o;
                var up = (el - sh).normalized;
                var fore = (wr - el).normalized;
                var outward = new Vector3(side, 0, 0);
                // Arm: deltoid out of the torso, upper arm, a rounded elbow, forearm tapering into the glove's cuff.
                var path = new List<Vector3> { sh - up * 0.06f, sh - up * 0.035f, sh, Vector3.Lerp(sh, el, 0.35f) };
                var radii = new List<Vector2> { new Vector2(0.03f, 0.03f), new Vector2(0.064f, 0.062f), new Vector2(0.075f, 0.072f), new Vector2(0.066f, 0.062f) };
                Bend(path, radii, el, up, fore, 0.085f, new Vector2(0.058f, 0.055f), new Vector2(0.052f, 0.047f), 4);
                path.Add(Vector3.Lerp(el, wr, 0.6f)); radii.Add(new Vector2(0.046f, 0.042f));
                path.Add(Vector3.Lerp(el, wr, 0.85f)); radii.Add(new Vector2(0.04f, 0.036f));
                path.Add(wr); radii.Add(new Vector2(0.036f, 0.032f));
                b.Sweep(path, radii, 16, (i, dir) =>
                {
                    float outer = Vector3.Dot(dir, outward);
                    if (i < 2) return p;                                       // shoulder
                    if (i < 5) return outer > 0.45f ? s : outer > 0.1f ? t : p;  // upper arm: outer panel with an accent edge
                    if (i < 8) return s;                                       // elbow
                    return dir.y > 0.75f ? t : s;                              // forearm with an accent line on top
                }, Vector3.forward, true, false);
                // Elbow slider on the outside of the elbow.
                b.Sphere(el + new Vector3(side * 0.045f, -0.01f, -0.02f), new Vector3(0.018f, 0.035f, 0.038f), Dark, 3, 8);

                // Glove: cuff, back of the hand on top of the grip, knuckles, fingers wrapped round the front and under.
                var grip = new Vector3(side * 0.245f, 0.897f, 0.4f) + o;
                var gPath = new[]
                {
                    wr - fore * 0.02f, wr + fore * 0.02f, grip + new Vector3(0, 0.04f, -0.005f), grip + new Vector3(0, 0.03f, 0.035f),
                    grip + new Vector3(0, -0.005f, 0.045f), grip + new Vector3(0, -0.035f, 0.02f)
                };
                var gRadii = new[]
                {
                    new Vector2(0.04f, 0.035f), new Vector2(0.043f, 0.032f), new Vector2(0.046f, 0.028f), new Vector2(0.044f, 0.024f),
                    new Vector2(0.041f, 0.02f), new Vector2(0.037f, 0.018f)
                };
                b.Sweep(gPath, gRadii, 10, (i, dir) => i == 2 && dir.y > 0.3f ? t : Dark, Vector3.up, false, true);
            }
        }

        /// <summary>Hip joint of one leg relative to <see cref="Seat"/> (side: -1 left, +1 right).</summary>
        public static Vector3 HipLocal(int side)
        {
            return Mirror(HipR, side) - Seat;
        }

        /// <summary>One leg, relative to its hip joint: thigh, knee slider, shin and boot on the footpeg.</summary>
        public static void BuildLeg(MeshBuilder b, int side, Color32 p, Color32 s)
        {
            BuildLeg(b, side, new BikeLivery(p, p, p, 0));
        }

        /// <summary>
        /// One leg in the livery's leathers, relative to its hip joint: thigh in the main colour with a second-colour
        /// panel down its outside (accent edges), knee and shin in the second colour with the knee slider on the outside,
        /// tall boot in the main colour, dark foot on the footpeg with a toe slider.
        /// </summary>
        public static void BuildLeg(MeshBuilder b, int side, BikeLivery livery)
        {
            Color32 p = Leather(livery.Primary), s = Leather(livery.Secondary), t = Leather(livery.Tertiary);
            var hip = Mirror(HipR, side);
            var knee = Mirror(KneeR, side) - hip;
            var ankle = Mirror(AnkleR, side) - hip;
            var heel = Mirror(HeelR, side) - hip;
            var toe = Mirror(ToeR, side) - hip;
            var thigh = knee.normalized;
            var shin = (ankle - knee).normalized;
            var outward = new Vector3(side, 0, 0);
            var path = new List<Vector3> { thigh * -0.07f, thigh * -0.04f, Vector3.zero, knee * 0.3f, knee * 0.55f };
            var radii = new List<Vector2> { new Vector2(0.04f, 0.04f), new Vector2(0.08f, 0.082f), new Vector2(0.09f, 0.09f), new Vector2(0.092f, 0.084f), new Vector2(0.083f, 0.076f) };
            Bend(path, radii, knee, thigh, shin, 0.14f, new Vector2(0.074f, 0.068f), new Vector2(0.063f, 0.059f), 4);
            float[] shinAt = { 0.45f, 0.62f, 0.75f, 0.9f, 1f };
            Vector2[] shinR = { new Vector2(0.06f, 0.056f), new Vector2(0.055f, 0.052f), new Vector2(0.058f, 0.056f), new Vector2(0.054f, 0.052f), new Vector2(0.05f, 0.05f) };
            for (int k = 0; k < shinAt.Length; k++)
            {
                path.Add(Vector3.Lerp(knee, ankle, shinAt[k]));
                radii.Add(shinR[k]);
            }
            path.Add(ankle + new Vector3(0, -0.03f, 0));
            radii.Add(new Vector2(0.048f, 0.048f));
            b.Sweep(path, radii, 16, (i, dir) =>
            {
                float outer = Vector3.Dot(dir, outward);
                if (i < 5) return outer > 0.45f ? s : outer > 0.1f ? t : p; // thigh: outer panel with accent edges
                if (i < 12) return s;                                     // knee and shin
                return p;                                                 // boot
            }, Vector3.forward, true, false);
            // Knee slider puck on the outside of the knee.
            b.Sphere(knee + new Vector3(side * 0.066f, -0.01f, 0), new Vector3(0.018f, 0.045f, 0.05f), Dark, 4, 10);
            // Foot: heel cup to toe, dark, with the toe slider on the outside.
            var foot = new[]
            {
                heel + new Vector3(0, 0.005f, -0.02f), heel + new Vector3(0, -0.005f, 0.03f), Vector3.Lerp(heel, toe, 0.5f),
                toe - (toe - heel).normalized * 0.03f, toe
            };
            var footR = new[]
            {
                new Vector2(0.04f, 0.05f), new Vector2(0.045f, 0.055f), new Vector2(0.045f, 0.045f), new Vector2(0.04f, 0.032f),
                new Vector2(0.03f, 0.02f)
            };
            b.Sweep(foot, footR, 12, (i, dir) => Dark, Vector3.up);
            b.Box(toe + new Vector3(side * 0.03f, -0.005f, -0.03f), new Vector3(0.012f, 0.03f, 0.06f), Steel);
        }

        /// <summary>
        /// Hanging-off pose for a bike lean angle in degrees (positive = leaning left): the body moves to the inside and
        /// rolls a little further in, and the inner leg swings out around the hip-ankle line so the boot stays on the peg.
        /// </summary>
        public static void Pose(float leanDeg, out Vector3 riderPosition, out Quaternion riderRotation, out Quaternion leftLeg, out Quaternion rightLeg)
        {
            float t = Mathf.Clamp01((Mathf.Abs(leanDeg) - 6f) / 40f);
            t = t * t * (3 - 2 * t);
            float dir = leanDeg >= 0 ? 1f : -1f; // +1: corner on the left (-X)
            riderPosition = Seat + new Vector3(-dir * 0.12f * t, -0.035f * t, -0.02f * t);
            riderRotation = Quaternion.Euler(0, -dir * 6f * t, dir * 14f * t);
            float knee = 30f * t;
            var axisL = Mirror(AnkleR, -1) - Mirror(HipR, -1);
            var axisR = AnkleR - HipR;
            // Rotating about the (mostly downward) hip-ankle axis: positive angles swing the knee to -X.
            leftLeg = dir > 0 ? Quaternion.AngleAxis(knee, axisL) : Quaternion.AngleAxis(-4f * t, axisL);
            rightLeg = dir < 0 ? Quaternion.AngleAxis(-knee, axisR) : Quaternion.AngleAxis(4f * t, axisR);
        }
    }
}
