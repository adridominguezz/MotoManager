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
    /// alpha carries the gloss for the MotoManager/BikeLit shader. About 5k triangles with the rider.
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
        private static readonly Vector3 ElbowR = new Vector3(0.25f, 0.88f, 0.15f);
        private static readonly Vector3 WristR = new Vector3(0.22f, 0.93f, 0.37f);
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
            new[] { 0.24f, 0.145f, 0.985f, 0.11f, 0.225f, 0.27f, 0.23f, 0.15f, 0.02f },      // 12 side vents
            new[] { 0.3f, 0.15f, 0.965f, 0.105f, 0.21f, 0.265f, 0.235f, 0.15f, 0.015f },     // 13
            new[] { 0.37f, 0.18f, 0.94f, 0.1f, 0.2f, 0.26f, 0.24f, 0.15f, 0.015f },          // 14 belly pan front
            new[] { 0.43f, 0.5f, 0.95f, 0.12f, 0.21f, 0.255f, 0.24f, 0.16f, 0.01f },         // 15 behind the front wheel
            new[] { 0.5f, 0.6f, 1.0f, 0.12f, 0.2f, 0.245f, 0.235f, 0.165f, 0.01f },          // 16
            new[] { 0.58f, 0.645f, 1.0f, 0.11f, 0.185f, 0.225f, 0.215f, 0.15f, 0.01f },      // 17 winglets
            new[] { 0.67f, 0.675f, 0.935f, 0.09f, 0.145f, 0.175f, 0.17f, 0.12f, 0.005f },    // 18
            new[] { 0.76f, 0.7f, 0.84f, 0.055f, 0.09f, 0.105f, 0.1f, 0.07f, 0f },            // 19
            new[] { 0.84f, 0.72f, 0.775f, 0.02f, 0.03f, 0.035f, 0.03f, 0.02f, 0f }            // 20 nose tip (air intake)
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
            below = 4;
            above = 6;
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
                    below = 8;
                    above = 2;
                    break;
                default: c1 = 2.05f; below = 3; above = 7; break; // 4 blocks, 5 tank band: a low pinstripe over the belly
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
            b.Hull(screen, Screen, null);

            // Winglets on the nose sides (a swept main plane with an end plate), and the front mudguard, in the second colour.
            Color32 wing = style == 7 ? p : s;
            for (int side = -1; side <= 1; side += 2)
            {
                b.Box(new Vector3(side * 0.24f, 0.8f, 0.6f), new Vector3(0.09f, 0.014f, 0.13f), wing, Quaternion.Euler(-8, side * -12, side * -10));
                b.Box(new Vector3(side * 0.283f, 0.79f, 0.59f), new Vector3(0.008f, 0.05f, 0.11f), wing, Quaternion.Euler(-8, 0, 0));
            }
            // Mudguard: short plates along an arc over the tyre (boxes, so the faces light correctly all round).
            float plate = 0.345f * 124f * Mathf.Deg2Rad / 5f * 1.08f;
            for (int k = 0; k < 5; k++)
            {
                float deg = Mathf.Lerp(-60f, 64f, (k + 0.5f) / 5f);
                float a = deg * Mathf.Deg2Rad;
                var at = FrontAxle + new Vector3(0, Mathf.Cos(a), Mathf.Sin(a)) * 0.345f;
                b.Box(at, new Vector3(0.14f - 0.012f * Mathf.Abs(k - 2), 0.014f, plate), s, Quaternion.Euler(deg, 0, 0));
            }

            // Wheels: rounded tyres with lettered sidewalls, dark split-spoke rims, brake discs and gold calipers.
            Wheel(b, FrontAxle, FrontRadius, 0.125f);
            Wheel(b, RearAxle, RearRadius, 0.19f);
            for (int side = -1; side <= 1; side += 2)
            {
                b.DiscX(FrontAxle + new Vector3(side * 0.07f, 0, 0), 0.115f, 0.165f, side, Steel, WheelSides);
                b.DiscX(FrontAxle + new Vector3(side * 0.066f, 0, 0), 0.115f, 0.165f, -side, Steel, WheelSides);
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
                b.Tube(axle - rake * 0.035f, axle + rake * 0.2f, 0.03f, 0.03f, Carbon, 8, true);
                b.Tube(axle + rake * 0.19f, axle + rake * 0.42f, 0.026f, 0.026f, Gold, 8, false);
                b.Tube(axle + rake * 0.41f, axle + rake * 0.66f, 0.031f, 0.031f, Dark, 6, true);
                var clamp = axle + rake * 0.62f;
                b.Tube(new Vector3(side * 0.09f, clamp.y, clamp.z), new Vector3(side * 0.3f, clamp.y - 0.025f, clamp.z - 0.035f), 0.012f, 0.012f, Metal, 5, true);
                b.Tube(new Vector3(side * 0.2f, clamp.y - 0.016f, clamp.z - 0.022f), new Vector3(side * 0.31f, clamp.y - 0.026f, clamp.z - 0.037f), 0.02f, 0.02f, Dark, 6, true);
                b.Box(new Vector3(side * 0.25f, clamp.y - 0.005f, clamp.z + 0.06f), new Vector3(0.1f, 0.012f, 0.025f), Metal, Quaternion.Euler(0, side * 12, 0));
            }
            var top = FrontAxle + rake * 0.62f;
            b.Box(new Vector3(0, top.y, top.z), new Vector3(0.24f, 0.03f, 0.07f), Metal, Quaternion.Euler(-24, 0, 0));
            b.Tube(FrontAxle - new Vector3(0.13f, 0, 0), FrontAxle + new Vector3(0.13f, 0, 0), 0.02f, 0.02f, Metal, 6, true);

            // Carbon swingarm (deep at the pivot, tapering to the axle), rear axle, engine block and frame spars
            // behind the side panels, the shock and the footpegs with their heel plates.
            for (int side = -1; side <= 1; side += 2)
            {
                b.Loft(new[]
                {
                    new MeshBuilder.Section(-0.72f, RearAxle.y, 0.025f, 0.035f, Carbon),
                    new MeshBuilder.Section(-0.45f, 0.38f, 0.03f, 0.06f, Carbon),
                    new MeshBuilder.Section(-0.24f, 0.44f, 0.035f, 0.075f, Carbon)
                }, 6, null, new Vector3(side * 0.115f, 0, 0));
                var peg = new Vector3(side * 0.17f, 0.43f, -0.38f);
                b.Tube(peg - new Vector3(side * 0.06f, 0, 0), peg + new Vector3(side * 0.04f, 0, 0), 0.012f, 0.012f, Metal, 5, true);
                b.Box(new Vector3(side * 0.125f, 0.48f, -0.4f), new Vector3(0.012f, 0.13f, 0.12f), Metal, Quaternion.Euler(20, 0, 0));
            }
            b.Tube(RearAxle - new Vector3(0.14f, 0, 0), RearAxle + new Vector3(0.14f, 0, 0), 0.02f, 0.02f, Metal, 6, true);
            b.Box(new Vector3(0, 0.46f, -0.2f), new Vector3(0.2f, 0.36f, 0.3f), Dark, Quaternion.Euler(-12, 0, 0));
            b.Tube(new Vector3(0, 0.5f, -0.33f), new Vector3(0, 0.78f, -0.46f), 0.03f, 0.03f, Gold, 6, false);

            // Twin silencers under the tail unit, fed by a header pipe from under the engine.
            for (int side = -1; side <= 1; side += 2)
            {
                var front = new Vector3(side * 0.12f, 0.69f, -0.5f);
                b.Tube(front, new Vector3(side * 0.085f, 0.87f, -0.85f), 0.042f, 0.048f, Titanium, 8, true, Dark);
                b.Tube(new Vector3(side * 0.07f, 0.24f, -0.06f), new Vector3(side * 0.11f, 0.3f, -0.3f), 0.03f, 0.03f, Titanium, 6, false);
                b.Tube(new Vector3(side * 0.11f, 0.3f, -0.3f), front + new Vector3(0, 0, 0.02f), 0.03f, 0.036f, Titanium, 6, false);
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

        /// <summary>A dark vent slot of height h and length l on the fairing side at (z, y), tilted forward by tilt degrees.</summary>
        private static void Vent(MeshBuilder b, int side, float z, float y, float h, float l, float tilt)
        {
            float x = SurfaceX(z, y);
            // Follow the surface: yaw from the hull's taper along Z, roll from its slope with height.
            float yaw = Mathf.Atan2(SurfaceX(z + 0.03f, y) - SurfaceX(z - 0.03f, y), 0.06f) * Mathf.Rad2Deg;
            float roll = Mathf.Atan2(SurfaceX(z, y + 0.03f) - SurfaceX(z, y - 0.03f), 0.06f) * Mathf.Rad2Deg;
            b.Box(new Vector3(side * (x + 0.002f), y, z), new Vector3(0.008f, h, l), Dark, Quaternion.Euler(0, side * yaw, -side * roll) * Quaternion.Euler(tilt, 0, 0));
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
        /// eight split spokes and the hub.
        /// </summary>
        private static void Wheel(MeshBuilder b, Vector3 c, float r, float w)
        {
            var profile = new[]
            {
                new Vector2(0.7f * r, -0.36f * w), new Vector2(0.86f * r, -0.5f * w), new Vector2(0.97f * r, -0.4f * w),
                new Vector2(r, -0.15f * w), new Vector2(r, 0.15f * w), new Vector2(0.97f * r, 0.4f * w),
                new Vector2(0.86f * r, 0.5f * w), new Vector2(0.7f * r, 0.36f * w)
            };
            b.Lathe(c, profile, new[] { Tyre, Tyre, Tyre, Tread, Tyre, Tyre, Tyre }, WheelSides, (band, seg) =>
            {
                if (band != 0 && band != 6) return null;
                // Lettering: light dashes around the sidewall, the rest plain rubber.
                return seg % 4 == 1 ? Lettering : Tyre;
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
                b.Box(c + d * ((r0 + r1) * 0.5f), new Vector3(0.016f, 0.018f, r1 - r0), WheelFace, Quaternion.Euler(-deg, 0, 0));
            }
            b.Tube(c - new Vector3(0.3f * w, 0, 0), c + new Vector3(0.3f * w, 0, 0), 0.07f, 0.07f, WheelFace, 8, true, Metal);
        }

        private const int WheelSides = 16;

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

        /// <summary>
        /// Rider torso, head and arms in the livery's leathers: body and forearms in the main colour, seat panel,
        /// aero hump, shoulders and upper arms in the second colour, gloves in the accent; the helmet colour is given.
        /// </summary>
        public static void BuildRiderBody(MeshBuilder b, BikeLivery livery, Color32 helmet)
        {
            var o = -Seat;
            Color32 p = Leather(livery.Primary), s = Leather(livery.Secondary), t = Leather(livery.Tertiary);
            // Torso: seat on the pad, back rising to the shoulders, chest flat on the tank.
            b.Loft(new[]
            {
                new MeshBuilder.Section(-0.5f, 0.94f, 0.08f, 0.06f, s),
                new MeshBuilder.Section(-0.45f, 0.965f, 0.15f, 0.1f, s),
                new MeshBuilder.Section(-0.36f, 1.0f, 0.17f, 0.125f, p),
                new MeshBuilder.Section(-0.22f, 1.05f, 0.18f, 0.135f, p),
                new MeshBuilder.Section(-0.08f, 1.1f, 0.19f, 0.13f, p),
                new MeshBuilder.Section(0.04f, 1.125f, 0.19f, 0.125f, p),
                new MeshBuilder.Section(0.13f, 1.12f, 0.15f, 0.1f, p),
                new MeshBuilder.Section(0.2f, 1.1f, 0.07f, 0.06f, Dark)
            }, 12, null, o);
            // Aero hump on the back of the leathers, peaking just behind the helmet.
            b.Loft(new[]
            {
                new MeshBuilder.Section(-0.16f, 1.19f, 0.03f, 0.02f, s),
                new MeshBuilder.Section(-0.04f, 1.225f, 0.085f, 0.05f, s),
                new MeshBuilder.Section(0.07f, 1.245f, 0.075f, 0.055f, s),
                new MeshBuilder.Section(0.15f, 1.21f, 0.035f, 0.03f, s)
            }, 8, null, o);
            // Helmet: chin almost on the tank, visor facing forward, an aero spoiler on the back of the shell.
            b.Sphere(HelmetCentre + o, new Vector3(0.13f, 0.135f, 0.155f), helmet, 7, 14, Visor);
            b.Loft(new[]
            {
                new MeshBuilder.Section(0.13f, 1.255f, 0.015f, 0.012f, helmet),
                new MeshBuilder.Section(0.18f, 1.3f, 0.07f, 0.025f, helmet),
                new MeshBuilder.Section(0.25f, 1.315f, 0.05f, 0.012f, helmet)
            }, 6, null, o);
            for (int side = -1; side <= 1; side += 2)
            {
                var sh = Mirror(ShoulderR, side) + o;
                var el = Mirror(ElbowR, side) + o;
                var wr = Mirror(WristR, side) + o;
                b.Sphere(sh, new Vector3(0.068f, 0.068f, 0.075f), s, 4, 8);
                b.Tube(sh, el, 0.062f, 0.05f, s, 8, false);
                b.Sphere(el, new Vector3(0.05f, 0.05f, 0.05f), s, 3, 8);
                b.Tube(el, wr, 0.05f, 0.04f, p, 8, false);
                // Glove wrapped round the grip.
                b.Tube(wr - (wr - el).normalized * 0.015f, wr + new Vector3(side * 0.012f, -0.008f, 0.075f), 0.044f, 0.036f, t, 6);
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
        /// One leg in the livery's leathers: thigh in the main colour, shin in the second one, accent knee armour with
        /// the slider on its outside, and the boot (tall cuff in the main colour, toe slider) on the footpeg.
        /// </summary>
        public static void BuildLeg(MeshBuilder b, int side, BikeLivery livery)
        {
            Color32 p = Leather(livery.Primary), s = Leather(livery.Secondary), t = Leather(livery.Tertiary);
            var hip = Mirror(HipR, side);
            var knee = Mirror(KneeR, side) - hip;
            var ankle = Mirror(AnkleR, side) - hip;
            var heel = Mirror(HeelR, side) - hip;
            var toe = Mirror(ToeR, side) - hip;
            b.Tube(Vector3.zero, knee, 0.085f, 0.066f, p, 8, false);
            b.Sphere(knee, new Vector3(0.068f, 0.068f, 0.072f), t, 4, 8);
            b.Box(knee + new Vector3(side * 0.06f, -0.025f, 0.01f), new Vector3(0.025f, 0.055f, 0.1f), Dark, Quaternion.Euler(-30, 0, 0)); // knee slider
            var shin = ankle - knee;
            b.Tube(knee, ankle - shin.normalized * 0.12f, 0.064f, 0.048f, s, 8, false);
            // Boot: tall cuff in the main colour up the shin, dark foot from heel to toe, toe slider on the outside.
            b.Tube(ankle - shin.normalized * 0.13f, ankle + new Vector3(0, -0.01f, -0.02f), 0.05f, 0.053f, p, 8, false);
            b.Tube(heel, toe, 0.055f, 0.036f, Dark, 8);
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
