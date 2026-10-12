using UnityEngine;

namespace MotoManager.App.Race
{
    /// <summary>
    /// Procedural low-poly GP bike and tucked rider. The side profile follows the game's bike icon (sharp nose with
    /// winglets, tall screen, tank hump, seat unit rising to a pointed tail, deep belly pan) at a 1.4 m wheelbase.
    /// Bike space: pivot on the ground under the middle of the wheelbase, forward = +Z, about 2 m long.
    /// The rider is split into a body and two legs (pivoting at the hips) so that leaning can shift the body to the
    /// inside of the corner and push the inner knee out with transforms only (see <see cref="Pose"/>).
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

        private static readonly Color32 Tyre = new Color32(24, 24, 27, 255);
        private static readonly Color32 Rim = new Color32(178, 182, 190, 255);
        private static readonly Color32 WheelFace = new Color32(52, 54, 60, 255);
        private static readonly Color32 Steel = new Color32(192, 194, 200, 255);
        private static readonly Color32 Metal = new Color32(112, 116, 124, 255);
        private static readonly Color32 Gold = new Color32(214, 168, 52, 255);
        private static readonly Color32 Dark = new Color32(30, 30, 34, 255);
        private static readonly Color32 Plate = new Color32(242, 242, 238, 255);
        private static readonly Color32 Screen = new Color32(58, 70, 88, 255);
        private static readonly Color32 ScreenTop = new Color32(96, 112, 134, 255);
        private static readonly Color32 Titanium = new Color32(168, 156, 142, 255);
        private static readonly Color32 Visor = new Color32(28, 32, 42, 255);

        /// <summary>Half outline of a fairing section: flat bottom, widest a little above mid-height, narrower top.</summary>
        private static Vector2[] Shape(float yb, float yt, float wb, float wl, float wm, float wu, float wt, float crown)
        {
            float h = yt - yb;
            return new[]
            {
                new Vector2(0, yb), new Vector2(wb, yb), new Vector2(wl, yb + 0.38f * h), new Vector2(wm, yb + 0.52f * h),
                new Vector2(wu, yb + 0.8f * h), new Vector2(wt, yt), new Vector2(0, yt + crown)
            };
        }

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

        private static MeshBuilder.Ring Ring(float[] r, Color32[] bands)
        {
            return new MeshBuilder.Ring(r[0], Shape(r[1], r[2], r[3], r[4], r[5], r[6], r[7], r[8]), bands);
        }

        public static void BuildBike(MeshBuilder b, Color32 p, Color32 s)
        {
            Color32 belly = Color32.Lerp(p, Dark, 0.5f);

            // Fairing: livery bands per span (bottom, lower, stripe, upper, shoulder, top).
            var rings = new MeshBuilder.Ring[Fairing.Length];
            for (int i = 0; i < Fairing.Length; i++)
            {
                var bands = new[] { p, p, s, p, p, p };
                if (i >= 5 && i <= 10) bands[0] = belly;                       // deep belly pan
                if (i >= 3 && i <= 5) { bands[5] = Dark; bands[4] = Dark; }    // seat pad
                if (i == 1) { bands[2] = Plate; bands[3] = Plate; bands[4] = Plate; } // tail number plate
                if (i == 14) { bands[3] = Plate; bands[4] = Plate; bands[5] = Plate; } // nose number plate
                if (i == 15) { bands[0] = Dark; bands[1] = Dark; bands[3] = Plate; bands[4] = Plate; bands[5] = Plate; }
                if (i == 16) bands = new[] { Dark, Dark, Dark, Dark, Dark, Dark };
                rings[i] = Ring(Fairing[i], bands);
            }
            b.Hull(rings, null, Dark); // dark front cap = air intake; the pointed tail needs no cap

            var screen = new MeshBuilder.Ring[ScreenRings.Length];
            for (int i = 0; i < ScreenRings.Length; i++)
                screen[i] = Ring(ScreenRings[i], new[] { Screen, Screen, Screen, Screen, ScreenTop, ScreenTop });
            b.Hull(screen, Screen, null);

            // Winglets on the nose sides, in the second team colour.
            for (int side = -1; side <= 1; side += 2)
                b.Box(new Vector3(side * 0.235f, 0.76f, 0.57f), new Vector3(0.08f, 0.018f, 0.16f), s, Quaternion.Euler(-6, 0, side * -12));

            // Wheels: tyre with rounded shoulders, a lighter rim lip and a dark wheel face.
            Wheel(b, new Vector3(0, 0.3f, 0.7f), 0.3f, 0.12f);
            Wheel(b, new Vector3(0, 0.318f, -0.7f), 0.318f, 0.17f);

            // Front brake discs and calipers.
            for (int side = -1; side <= 1; side += 2)
            {
                b.DiscX(new Vector3(side * 0.072f, 0.3f, 0.7f), 0.115f, 0.18f, side, Steel, WheelSides);
                b.Box(new Vector3(side * 0.08f, 0.42f, 0.58f), new Vector3(0.035f, 0.1f, 0.06f), Gold, Quaternion.Euler(-40, 0, 0));
            }

            // Fork: two legs along a 24 degree rake, dark sliders and gold stanchions.
            var rake = new Vector3(0, Mathf.Cos(24 * Mathf.Deg2Rad), -Mathf.Sin(24 * Mathf.Deg2Rad));
            for (int side = -1; side <= 1; side += 2)
            {
                var axle = new Vector3(side * 0.095f, 0.3f, 0.7f);
                b.Tube(axle - rake * 0.03f, axle + rake * 0.25f, 0.032f, 0.03f, Dark, 5, false);
                b.Tube(axle + rake * 0.24f, axle + rake * 0.62f, 0.024f, 0.024f, Gold, 5, false);
            }
            b.Tube(new Vector3(-0.11f, 0.3f, 0.7f), new Vector3(0.11f, 0.3f, 0.7f), 0.022f, 0.022f, Metal, 4, false);

            // Swingarm (both sides), rear axle, and the dark engine / frame block behind the side panels.
            for (int side = -1; side <= 1; side += 2)
                b.Box(new Vector3(side * 0.11f, 0.38f, -0.44f), new Vector3(0.04f, 0.085f, 0.56f), Metal, Quaternion.Euler(-13.4f, 0, 0));
            b.Tube(new Vector3(-0.13f, 0.318f, -0.7f), new Vector3(0.13f, 0.318f, -0.7f), 0.022f, 0.022f, Metal, 4, false);
            b.Box(new Vector3(0, 0.5f, -0.33f), new Vector3(0.18f, 0.34f, 0.12f), Dark);

            // Exhaust: silencer rising from behind the side panel to under the tail, on the right.
            b.Tube(new Vector3(0.115f, 0.32f, -0.17f), new Vector3(0.145f, 0.58f, -0.76f), 0.042f, 0.055f, Titanium, 6, true, Dark);
        }

        private static void Wheel(MeshBuilder b, Vector3 c, float r, float w)
        {
            var profile = new[]
            {
                new Vector2(0.68f * r, -0.3f * w), new Vector2(0.79f * r, -0.4f * w), new Vector2(0.93f * r, -0.5f * w),
                new Vector2(r, -0.22f * w), new Vector2(r, 0.22f * w),
                new Vector2(0.93f * r, 0.5f * w), new Vector2(0.79f * r, 0.4f * w), new Vector2(0.68f * r, 0.3f * w)
            };
            b.Lathe(c, profile, new[] { Rim, Tyre, Tyre, Tyre, Tyre, Tyre, Rim }, WheelSides);
            b.DiscX(c + new Vector3(0.3f * w, 0, 0), 0, 0.68f * r, 1, WheelFace, WheelSides);
            b.DiscX(c - new Vector3(0.3f * w, 0, 0), 0, 0.68f * r, -1, WheelFace, WheelSides);
        }

        private const int WheelSides = 10;

        private static Vector3 Mirror(Vector3 v, int side)
        {
            return new Vector3(v.x * side, v.y, v.z);
        }

        /// <summary>Rider torso, head and arms, relative to <see cref="Seat"/>. Leathers in the team colours.</summary>
        public static void BuildRiderBody(MeshBuilder b, Color32 p, Color32 s, Color32 helmet)
        {
            var o = -Seat;
            b.Loft(new[]
            {
                new MeshBuilder.Section(-0.48f, 0.97f, 0.09f, 0.07f, p),
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
                b.Tube(wr - (wr - el).normalized * 0.01f, wr + new Vector3(0, 0, 0.06f), 0.045f, 0.04f, Dark, 5); // glove on the clip-on
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
            var hip = Mirror(HipR, side);
            var knee = Mirror(KneeR, side) - hip;
            var ankle = Mirror(AnkleR, side) - hip;
            b.Tube(Vector3.zero, knee, 0.075f, 0.06f, p, 6, false);
            b.Tube(knee, ankle, 0.058f, 0.045f, p, 6, false);
            b.Sphere(knee, new Vector3(0.062f, 0.062f, 0.062f), p, 2, 5);
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
