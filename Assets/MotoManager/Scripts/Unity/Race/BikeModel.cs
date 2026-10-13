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
    /// Procedural low-poly GP bike and tucked rider. The side profile follows the game's bike icon (sharp nose with
    /// winglets, tall screen, tank hump, seat unit rising to a pointed tail, deep belly pan) at a 1.4 m wheelbase.
    /// Bike space: pivot on the ground under the middle of the wheelbase, forward = +Z, about 2 m long.
    /// The rider is split into a body and two legs (pivoting at the hips) so that leaning can shift the body to the
    /// inside of the corner and push the inner knee out with transforms only (see <see cref="Pose"/>).
    /// Liveries: the fairing outline of every section is cut at the livery's boundaries (so stripes and splits are
    /// crisp hull edges) and painted per triangle from <see cref="StyleCount"/> templates in three colours; vertex
    /// alpha carries the gloss for the MotoManager/BikeLit shader. About 1.67k triangles with the rider.
    /// </summary>
    public static class BikeModel
    {
        /// <summary>Rider root (on the seat) in bike space. Rider meshes are built relative to it.</summary>
        public static readonly Vector3 Seat = new Vector3(0, 0.88f, -0.36f);

        // Right-side joints in bike space (the left side is mirrored).
        private static readonly Vector3 HipR = new Vector3(0.12f, 0.94f, -0.36f);
        private static readonly Vector3 KneeR = new Vector3(0.25f, 0.79f, -0.01f);
        private static readonly Vector3 AnkleR = new Vector3(0.185f, 0.48f, -0.33f);
        private static readonly Vector3 HeelR = new Vector3(0.19f, 0.49f, -0.43f);
        private static readonly Vector3 ToeR = new Vector3(0.175f, 0.43f, -0.21f);
        private static readonly Vector3 ShoulderR = new Vector3(0.165f, 1.07f, 0.0f);
        private static readonly Vector3 ElbowR = new Vector3(0.23f, 0.9f, 0.13f);
        private static readonly Vector3 WristR = new Vector3(0.21f, 0.93f, 0.35f);

        // Fixed colours. Alpha is the gloss read by the bike shader (255 = lacquered paint, 0 = matte rubber).
        private static readonly Color32 Tyre = new Color32(30, 30, 33, 40);
        private static readonly Color32 Tread = new Color32(24, 24, 27, 20);
        private static readonly Color32 Lettering = new Color32(170, 172, 178, 90);
        private static readonly Color32 WheelFace = new Color32(38, 40, 45, 150);
        private static readonly Color32 Steel = new Color32(176, 180, 188, 220);
        private static readonly Color32 Metal = new Color32(100, 104, 112, 200);
        private static readonly Color32 Gold = new Color32(222, 172, 50, 235);
        private static readonly Color32 Dark = new Color32(28, 29, 33, 150);
        private static readonly Color32 SeatPad = new Color32(22, 22, 25, 30);
        private static readonly Color32 Plate = new Color32(242, 242, 238, 200);
        private static readonly Color32 Screen = new Color32(52, 64, 82, 255);
        private static readonly Color32 ScreenTop = new Color32(92, 108, 130, 255);
        private static readonly Color32 Titanium = new Color32(170, 158, 144, 220);
        private static readonly Color32 Visor = new Color32(24, 28, 38, 255);

        /// <summary>Number of livery templates (same as Core's TeamColors.StyleCount).</summary>
        public const int StyleCount = 8;

        // Fairing rings, tail tip -> nose: z, bottom y, top y, half widths (bottom, lower, mid, upper, top), crown.
        private static readonly float[][] Fairing =
        {
            new[] { -0.93f, 0.995f, 1.025f, 0.01f, 0.015f, 0.02f, 0.02f, 0.015f, 0f },     // 0 pointed tail
            new[] { -0.8f, 0.88f, 1.02f, 0.03f, 0.045f, 0.06f, 0.065f, 0.05f, 0.005f },   // 1
            new[] { -0.65f, 0.79f, 1.015f, 0.04f, 0.065f, 0.095f, 0.1f, 0.085f, 0.01f },  // 2 seat hump
            new[] { -0.5f, 0.7f, 0.87f, 0.05f, 0.085f, 0.115f, 0.12f, 0.11f, 0.01f },     // 3 seat
            new[] { -0.35f, 0.62f, 0.85f, 0.06f, 0.1f, 0.13f, 0.13f, 0.12f, 0.01f },      // 4
            new[] { -0.31f, 0.3f, 0.845f, 0.08f, 0.14f, 0.16f, 0.15f, 0.13f, 0.01f },     // 5 rear of the side panels
            new[] { -0.26f, 0.16f, 0.845f, 0.09f, 0.16f, 0.18f, 0.16f, 0.13f, 0.01f },    // 6
            new[] { -0.15f, 0.14f, 0.92f, 0.1f, 0.18f, 0.21f, 0.19f, 0.14f, 0.015f },     // 7 tank rear
            new[] { -0.05f, 0.14f, 1.0f, 0.1f, 0.19f, 0.225f, 0.21f, 0.15f, 0.02f },      // 8 tank hump
            new[] { 0.15f, 0.14f, 0.99f, 0.11f, 0.2f, 0.235f, 0.22f, 0.15f, 0.02f },      // 9
            new[] { 0.34f, 0.15f, 0.96f, 0.12f, 0.21f, 0.235f, 0.22f, 0.15f, 0.015f },    // 10 belly pan front
            new[] { 0.4f, 0.36f, 0.95f, 0.13f, 0.21f, 0.23f, 0.21f, 0.15f, 0.015f },      // 11 behind the front wheel
            new[] { 0.46f, 0.6f, 0.935f, 0.12f, 0.2f, 0.22f, 0.2f, 0.14f, 0.015f },       // 12
            new[] { 0.54f, 0.62f, 0.92f, 0.12f, 0.19f, 0.21f, 0.19f, 0.13f, 0.01f },      // 13 winglets
            new[] { 0.66f, 0.64f, 0.885f, 0.1f, 0.15f, 0.17f, 0.16f, 0.11f, 0.01f },      // 14
            new[] { 0.76f, 0.68f, 0.84f, 0.07f, 0.1f, 0.11f, 0.1f, 0.07f, 0.005f },       // 15
            new[] { 0.82f, 0.71f, 0.79f, 0.035f, 0.05f, 0.055f, 0.05f, 0.035f, 0f }       // 16 nose tip (air intake)
        };

        // Screen rings, back -> front (same layout as the fairing).
        private static readonly float[][] ScreenRings =
        {
            new[] { 0.37f, 1.0f, 1.13f, 0.14f, 0.14f, 0.13f, 0.1f, 0.05f, 0.005f },
            new[] { 0.48f, 0.9f, 1.165f, 0.145f, 0.145f, 0.13f, 0.1f, 0.05f, 0.005f },
            new[] { 0.62f, 0.86f, 1.0f, 0.13f, 0.13f, 0.12f, 0.09f, 0.05f, 0.005f },
            new[] { 0.74f, 0.82f, 0.855f, 0.06f, 0.06f, 0.05f, 0.04f, 0.02f, 0f }
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

        private static Vector2 At(Vector2[] pts, float c)
        {
            int k = Mathf.Clamp((int)Mathf.Floor(c), 0, pts.Length - 2);
            float f = Mathf.Clamp01(c - k);
            return pts[k] + (pts[k + 1] - pts[k]) * f;
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
        private static readonly float[] SplitLow = { -0.93f, 2.6f, -0.65f, 2.3f, -0.45f, 2.4f, -0.3f, 3.0f, 0.1f, 3.25f, 0.45f, 4.0f, 0.82f, 4.7f };
        private static readonly float[] SplitHigh = { -0.93f, 2.6f, -0.65f, 2.3f, -0.45f, 2.6f, -0.3f, 3.85f, 0.82f, 3.85f };
        private static readonly float[] Swoosh = { -0.93f, 5.0f, -0.6f, 5.2f, -0.3f, 4.6f, 0.1f, 3.2f, 0.45f, 2.2f, 0.82f, 1.8f };

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
            above = 4;
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
                    above = 1;
                    break;
                default: c1 = 2.05f; below = 2; above = 5; break; // 4 blocks, 5 tank band: a low pinstripe over the belly
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
        /// winglets, front mudguard, wheels with sidewall lettering, brakes, fork, swingarm, engine block and the
        /// twin exhaust under the tail. Number plates and the seat pad keep fixed colours on every style.
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

            b.Hull(rings, null, Dark, (ring, band, tri) =>
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
                return Fixed(col, ring, c);
            });

            var screen = new MeshBuilder.Ring[ScreenRings.Length];
            for (int i = 0; i < ScreenRings.Length; i++)
                screen[i] = new MeshBuilder.Ring(ScreenRings[i][0], ScreenShape(ScreenRings[i]), new[] { Screen, Screen, Screen, Screen, ScreenTop, ScreenTop });
            b.Hull(screen, Screen, null);

            // Winglets on the nose sides, and the front mudguard over the wheel, in the second colour.
            Color32 wing = style == 7 ? p : s;
            for (int side = -1; side <= 1; side += 2)
                b.Box(new Vector3(side * 0.225f, 0.74f, 0.58f), new Vector3(0.075f, 0.016f, 0.15f), wing, Quaternion.Euler(-6, 0, side * -14));
            var guard = new List<MeshBuilder.Section>();
            for (int k = 0; k <= 4; k++)
            {
                float a = Mathf.Lerp(-58f, 62f, k / 4f) * Mathf.Deg2Rad;
                guard.Add(new MeshBuilder.Section(0.7f + Mathf.Sin(a) * 0.335f, 0.3f + Mathf.Cos(a) * 0.335f, 0.07f, 0.016f, s));
            }
            b.Loft(guard, 4);

            // Wheels: dark tyre with rounded shoulders and a lettered sidewall, dark face, brake discs.
            Wheel(b, new Vector3(0, 0.3f, 0.7f), 0.3f, 0.12f);
            Wheel(b, new Vector3(0, 0.318f, -0.7f), 0.318f, 0.17f);
            for (int side = -1; side <= 1; side += 2)
            {
                b.DiscX(new Vector3(side * 0.072f, 0.3f, 0.7f), 0.115f, 0.18f, side, Steel, WheelSides);
                b.Box(new Vector3(side * 0.08f, 0.42f, 0.58f), new Vector3(0.035f, 0.1f, 0.06f), Gold, Quaternion.Euler(-40, 0, 0));
            }

            // Fork: two legs along a 24 degree rake, gold lower tubes and carbon-black upper legs.
            var rake = new Vector3(0, Mathf.Cos(24 * Mathf.Deg2Rad), -Mathf.Sin(24 * Mathf.Deg2Rad));
            for (int side = -1; side <= 1; side += 2)
            {
                var axle = new Vector3(side * 0.095f, 0.3f, 0.7f);
                b.Tube(axle - rake * 0.03f, axle + rake * 0.25f, 0.03f, 0.028f, Gold, 5, false);
                b.Tube(axle + rake * 0.24f, axle + rake * 0.62f, 0.032f, 0.032f, Dark, 5, false);
            }
            b.Tube(new Vector3(-0.11f, 0.3f, 0.7f), new Vector3(0.11f, 0.3f, 0.7f), 0.022f, 0.022f, Metal, 4, false);

            // Carbon swingarm, rear axle, and the dark engine / frame block behind the side panels.
            for (int side = -1; side <= 1; side += 2)
                b.Box(new Vector3(side * 0.11f, 0.38f, -0.44f), new Vector3(0.04f, 0.085f, 0.56f), Dark, Quaternion.Euler(-13.4f, 0, 0));
            b.Tube(new Vector3(-0.13f, 0.318f, -0.7f), new Vector3(0.13f, 0.318f, -0.7f), 0.022f, 0.022f, Metal, 4, false);
            b.Box(new Vector3(0, 0.5f, -0.33f), new Vector3(0.18f, 0.34f, 0.12f), Dark);

            // Twin exhaust: two titanium pipes rising under the tail unit, ending just short of its tip.
            for (int k = -1; k <= 1; k += 2)
                b.Tube(new Vector3(0.075f, 0.6f + k * 0.03f, -0.46f), new Vector3(0.04f + k * 0.035f, 0.82f + k * 0.012f, -0.87f), 0.03f, 0.033f, Titanium, 6, true, Dark);
        }

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

        /// <summary>Parts every livery keeps: nose intake, number plates, seat pad, side vents, carbon belly.</summary>
        private static Color32 Fixed(Color32 col, int ring, float c)
        {
            if (ring >= 15) return c > 4.7f ? Plate : Dark;                // air intake at the nose tip
            if (ring == 14 && c > 4.7f) return Plate;                      // front number plate
            if (ring == 1 && c > 2.9f && c < 6.1f) return Plate;           // tail number plate
            if (ring >= 3 && ring <= 5 && c > 5.05f) return SeatPad;       // seat pad
            if (ring == 9 && c > 3.05f && c < 3.95f) return Dark;          // side vent (cut-out)
            if (ring == 10 && c > 3.2f && c < 3.8f) return Dark;
            if (ring >= 5 && ring <= 10 && c < 1) return Color32.Lerp(col, Dark, 0.55f); // carbon belly pan
            return col;
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

        private static void Wheel(MeshBuilder b, Vector3 c, float r, float w)
        {
            // Sidewall (with a dashed band of lettering), rounded shoulders, tread.
            var profile = new[]
            {
                new Vector2(0.7f * r, -0.38f * w), new Vector2(0.87f * r, -0.5f * w),
                new Vector2(r, -0.24f * w), new Vector2(r, 0.24f * w),
                new Vector2(0.87f * r, 0.5f * w), new Vector2(0.7f * r, 0.38f * w)
            };
            b.Lathe(c, profile, new[] { Tyre, Tyre, Tread, Tyre, Tyre }, WheelSides, (band, seg) =>
            {
                if (band != 0 && band != 4) return null;
                // Lettering: four light dashes around the sidewall, the rest plain rubber.
                return seg % 3 == 1 ? Lettering : Tyre;
            });
            b.DiscX(c + new Vector3(0.38f * w, 0, 0), 0, 0.7f * r, 1, WheelFace, WheelSides);
            b.DiscX(c - new Vector3(0.38f * w, 0, 0), 0, 0.7f * r, -1, WheelFace, WheelSides);
        }

        private const int WheelSides = 12;

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
        /// Rider torso, head and arms in the livery's leathers: body and forearms in the main colour, the aero hump
        /// and upper arms in the second colour, gloves in the accent; the helmet colour is given.
        /// </summary>
        public static void BuildRiderBody(MeshBuilder b, BikeLivery livery, Color32 helmet)
        {
            var o = -Seat;
            Color32 p = Leather(livery.Primary), s = Leather(livery.Secondary), t = Leather(livery.Tertiary);
            b.Loft(new[]
            {
                new MeshBuilder.Section(-0.48f, 0.97f, 0.09f, 0.07f, s),
                new MeshBuilder.Section(-0.42f, 1.0f, 0.16f, 0.11f, p),
                new MeshBuilder.Section(-0.26f, 1.07f, 0.18f, 0.12f, p),
                new MeshBuilder.Section(-0.08f, 1.11f, 0.185f, 0.11f, p),
                new MeshBuilder.Section(0.03f, 1.08f, 0.185f, 0.09f, p),
                new MeshBuilder.Section(0.12f, 1.05f, 0.07f, 0.05f, Dark)
            }, 8, null, o);
            // Aero hump on the back of the leathers, behind the helmet.
            b.Loft(new[]
            {
                new MeshBuilder.Section(-0.16f, 1.18f, 0.03f, 0.02f, s),
                new MeshBuilder.Section(-0.04f, 1.2f, 0.075f, 0.05f, s),
                new MeshBuilder.Section(0.06f, 1.19f, 0.06f, 0.05f, s),
                new MeshBuilder.Section(0.1f, 1.16f, 0.03f, 0.03f, s)
            }, 6, null, o);
            // Helmet: chin almost on the tank, visor facing forward.
            b.Sphere(new Vector3(0, 1.12f, 0.2f) + o, new Vector3(0.125f, 0.12f, 0.15f), helmet, 5, 10, Visor);
            for (int side = -1; side <= 1; side += 2)
            {
                var sh = Mirror(ShoulderR, side) + o;
                var el = Mirror(ElbowR, side) + o;
                var wr = Mirror(WristR, side) + o;
                b.Tube(sh, el, 0.06f, 0.05f, s, 6, false);
                b.Tube(el, wr, 0.05f, 0.04f, p, 6, false);
                b.Tube(wr - (wr - el).normalized * 0.01f, wr + new Vector3(0, 0, 0.06f), 0.045f, 0.04f, t, 5); // glove on the clip-on
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

        /// <summary>One leg in the livery's leathers: thigh in the second colour, shin in the main one, accent knee.</summary>
        public static void BuildLeg(MeshBuilder b, int side, BikeLivery livery)
        {
            Color32 p = Leather(livery.Primary), s = Leather(livery.Secondary), t = Leather(livery.Tertiary);
            var hip = Mirror(HipR, side);
            var knee = Mirror(KneeR, side) - hip;
            var ankle = Mirror(AnkleR, side) - hip;
            b.Tube(Vector3.zero, knee, 0.075f, 0.06f, s, 6, false);
            b.Tube(knee, ankle, 0.058f, 0.045f, p, 6, false);
            b.Sphere(knee, new Vector3(0.062f, 0.062f, 0.062f), t, 2, 5);
            b.Box(knee + new Vector3(side * 0.055f, -0.02f, 0.0f), new Vector3(0.03f, 0.06f, 0.09f), Dark); // knee slider
            b.Tube(Mirror(HeelR, side) - hip, Mirror(ToeR, side) - hip, 0.05f, 0.035f, Dark, 6);
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
