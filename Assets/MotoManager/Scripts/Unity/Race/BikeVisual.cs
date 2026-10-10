using MotoManager.Core.Data;
using MotoManager.Core.Race;
using UnityEngine;

namespace MotoManager.App.Race
{
    /// <summary>
    /// Bike + rider in team colours (rounded low-poly), or a coloured dot when the camera is far away. Purely visual:
    /// it reads the rider's simulated state and turns it into position on the racing line, heading, lean, pitch and
    /// crash poses. Riders out of the race slide off and disappear.
    /// </summary>
    public class BikeVisual : MonoBehaviour
    {
        public RiderRaceState State { get; private set; }

        /// <summary>Points the visual at another simulation's rider (replays / seeking).</summary>
        public void Rebind(RiderRaceState state)
        {
            State = state;
        }
        public Color PrimaryColor { get; private set; }
        public Transform Body { get; private set; }
        /// <summary>Flat marker used in the far view.</summary>
        public Transform Marker { get; private set; }
        /// <summary>False once the rider is out of the race and has been cleared from the track.</summary>
        public bool Visible { get; private set; }
        public bool ShowingMarker { get; private set; }

        /// <summary>Seconds a crashed-out or retired rider stays in view before being cleared.</summary>
        public const double ClearAfter = 4.0;

        private Transform _rider;
        private GameObject _halo;
        private float _lean;
        private float _pitch;
        private float _lateral;
        private float _lineWeight;
        private float _crashSpin;
        private Vector3 _crashOffset;
        private double _outSince = -1;

        public static BikeVisual Create(RiderRaceState state, Transform parent)
        {
            var go = new GameObject("Bike " + state.Entry.Rider.Code);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<BikeVisual>();
            v.State = state;
            v.Build();
            return v;
        }

        private void Build()
        {
            var team = State.Entry.Team;
            var primary = UI.UiFactory.ParseColor(team != null ? team.Colors.Primary : null, new Color(0.8f, 0.1f, 0.1f));
            var secondary = UI.UiFactory.ParseColor(team != null ? team.Colors.Secondary : null, Color.white);
            PrimaryColor = primary;
            // Helmet colour is personal (stable per rider).
            float hue = (float)(Core.Common.DeterministicRandom.Hash(State.Entry.Rider.Id) % 360UL) / 360f;
            Color32 helmet = Color.HSVToRGB(hue, 0.55f, 0.95f);
            var tyre = new Color32(22, 22, 25, 255);
            var metal = new Color32(120, 124, 132, 255);
            var visor = new Color32(30, 34, 44, 255);
            Color32 p = primary;
            Color32 s = secondary;
            Color32 leathers = Color.Lerp(primary, Color.black, 0.4f);

            // Bike: pivot at the contact patch line so leaning rotates around the tyres.
            Body = new GameObject("Body").transform;
            Body.SetParent(transform, false);
            var bike = new MeshBuilder();
            bike.Cylinder(new Vector3(0, 0.3f, 0.7f), 0.3f, 0.12f, tyre);
            bike.Cylinder(new Vector3(0, 0.3f, 0.7f), 0.15f, 0.14f, metal, 10);
            bike.Cylinder(new Vector3(0, 0.3f, -0.68f), 0.31f, 0.18f, tyre);
            bike.Cylinder(new Vector3(0, 0.3f, -0.68f), 0.15f, 0.2f, metal, 10);
            bike.Box(new Vector3(0, 0.58f, 0.62f), new Vector3(0.16f, 0.52f, 0.06f), metal, Quaternion.Euler(-24, 0, 0)); // forks
            bike.Box(new Vector3(0, 0.38f, -0.38f), new Vector3(0.2f, 0.06f, 0.55f), metal, Quaternion.Euler(-8, 0, 0)); // swingarm
            bike.Loft(new[]
            {
                new MeshBuilder.Section(-0.98f, 0.9f, 0.04f, 0.04f, s),
                new MeshBuilder.Section(-0.8f, 0.9f, 0.11f, 0.08f, s),
                new MeshBuilder.Section(-0.5f, 0.86f, 0.15f, 0.11f, s),
                new MeshBuilder.Section(-0.3f, 0.76f, 0.16f, 0.17f, p),
                new MeshBuilder.Section(0.05f, 0.7f, 0.21f, 0.27f, p),
                new MeshBuilder.Section(0.42f, 0.7f, 0.22f, 0.26f, p),
                new MeshBuilder.Section(0.66f, 0.76f, 0.16f, 0.2f, s),
                new MeshBuilder.Section(0.84f, 0.8f, 0.05f, 0.08f, s)
            }, 12);
            // Screen.
            bike.Loft(new[]
            {
                new MeshBuilder.Section(0.38f, 1.0f, 0.13f, 0.03f, visor),
                new MeshBuilder.Section(0.62f, 0.93f, 0.1f, 0.025f, visor)
            }, 8);
            bike.ToObject("BikeMesh", Body);

            _rider = new GameObject("Rider").transform;
            _rider.SetParent(Body, false);
            var rider = new MeshBuilder();
            rider.Loft(new[]
            {
                new MeshBuilder.Section(-0.5f, 1.0f, 0.16f, 0.12f, leathers),
                new MeshBuilder.Section(-0.2f, 1.1f, 0.2f, 0.14f, leathers),
                new MeshBuilder.Section(0.1f, 1.14f, 0.19f, 0.12f, p),
                new MeshBuilder.Section(0.28f, 1.12f, 0.13f, 0.09f, leathers)
            }, 10);
            rider.Sphere(new Vector3(0, 1.24f, 0.4f), new Vector3(0.15f, 0.14f, 0.17f), helmet, 7, 12, visor);
            for (int side = -1; side <= 1; side += 2)
            {
                rider.Box(new Vector3(side * 0.2f, 1.03f, 0.42f), new Vector3(0.08f, 0.08f, 0.36f), leathers, Quaternion.Euler(30, 0, 0)); // arms
                rider.Box(new Vector3(side * 0.22f, 0.9f, -0.24f), new Vector3(0.11f, 0.11f, 0.42f), leathers, Quaternion.Euler(22, side * -8, 0)); // thighs
                rider.Box(new Vector3(side * 0.24f, 0.72f, -0.2f), new Vector3(0.09f, 0.09f, 0.32f), leathers, Quaternion.Euler(-55, 0, 0)); // shins
            }
            rider.ToObject("RiderMesh", _rider);

            // Far-view marker: a dot in team colour with a dark rim, plus a halo for the player's riders.
            Marker = new GameObject("Marker").transform;
            Marker.SetParent(transform, false);
            var dot = new MeshBuilder();
            // Dark rim, a ring in the second team colour, and a plain centre where the HUD writes the position.
            dot.Disc(new Vector3(0, 0.6f, 0), 1.32f, new Color32(15, 15, 18, 255));
            dot.Disc(new Vector3(0, 0.61f, 0), 1.2f, s);
            dot.Disc(new Vector3(0, 0.62f, 0), 1.02f, p);
            dot.ToObject("Dot", Marker);
            var halo = new MeshBuilder();
            halo.Disc(new Vector3(0, 0.55f, 0), 1.65f, new Color32(255, 200, 40, 255));
            _halo = halo.ToObject("Halo", Marker);
            _halo.SetActive(State.Entry.IsPlayer);
            Marker.gameObject.SetActive(false);
            Visible = true;
        }

        private bool IsOut
        {
            get
            {
                var s = State;
                return s.Status == RaceStatus.Retired || s.Status == RaceStatus.Disqualified
                    || (s.Status == RaceStatus.Crashed && s.RemountAt < 0);
            }
        }

        private bool IsDown
        {
            get
            {
                var s = State;
                return s.Status == RaceStatus.Crashed || (s.Status == RaceStatus.Retired && s.RetireReason != null && s.RetireReason.StartsWith("Crash"));
            }
        }

        /// <summary>
        /// Called every frame by the race view with the interpolated distance along the lap. <paramref name="markers"/>
        /// switches to the far-view dot; <paramref name="time"/> is the simulation clock.
        /// </summary>
        public void UpdateVisual(TrackRenderer track, double distance, float dt, float scale, bool markers, double time, bool selected)
        {
            var s = State;

            // Out of the race: leave the track and disappear after a moment (replays can bring the rider back).
            if (IsOut)
            {
                if (_outSince < 0 || time < _outSince) _outSince = time;
            }
            else _outSince = -1;
            bool visible = _outSince < 0 || time - _outSince < ClearAfter;
            if (visible != Visible)
            {
                Visible = visible;
                Body.gameObject.SetActive(visible && !markers);
                Marker.gameObject.SetActive(visible && markers);
            }
            if (!visible) return;
            if (markers != ShowingMarker)
            {
                ShowingMarker = markers;
                Body.gameObject.SetActive(!markers);
                Marker.gameObject.SetActive(markers);
            }

            // Lateral position: the racing line plus the simulation's own offset (overtakes, defending, run-offs).
            bool racing = s.Status == RaceStatus.Racing || s.Status == RaceStatus.Finished;
            bool launched = s.Lap > 0 || s.Speed > 25;
            _lineWeight = Mathf.MoveTowards(_lineWeight, racing && launched ? 1f : 0f, dt * 0.5f);
            float line = (float)track.Line.OffsetAt(distance) * _lineWeight;
            float targetLateral = line + (float)s.LateralOffset * TrackRenderer.HalfWidth;
            if (racing) targetLateral = Mathf.Clamp(targetLateral, -TrackRenderer.HalfWidth * 0.92f, TrackRenderer.HalfWidth * 0.92f);
            if (s.Status == RaceStatus.InPitLane)
            {
                double t = s.PitProgress / System.Math.Max(1, track.Track.PitLaneLength);
                targetLateral = track.PitSide * (float)TrackRenderer.PitLateral(t);
            }
            _lateral = Mathf.Lerp(_lateral, targetLateral, 1 - Mathf.Exp(-dt * 6));

            var pos = track.Point(distance, _lateral, 0);
            float heading = track.HeadingDeg(distance);
            transform.localPosition = pos;
            transform.localRotation = Quaternion.Euler(0, heading, 0);
            transform.localScale = Vector3.one * scale;

            if (markers)
            {
                _halo.SetActive(s.Entry.IsPlayer || selected);
                // Fade out while being cleared.
                float k = _outSince < 0 ? 1f : Mathf.Clamp01((float)(ClearAfter - (time - _outSince)));
                Marker.localScale = Vector3.one * Mathf.Max(0.01f, k);
                return;
            }

            // Lean: the simulation's lean angle (positive = left), smoothed.
            float targetLean = (float)s.LeanDeg;
            // Pitch: nose dives under braking, lifts slightly on corner exit / straights while accelerating.
            float targetPitch = 0;
            if (s.Phase == RidingPhase.Braking || (s.Speed < s.PrevSpeed - 0.3)) targetPitch = 4f;
            else if (s.Phase == RidingPhase.CornerExit || s.Phase == RidingPhase.Straight && s.Speed > s.PrevSpeed + 0.05) targetPitch = -2f;

            if (IsDown)
            {
                // Bike slides on its side and spins; the rider tumbles away from it.
                targetLean = 85f * (s.LastCrash == CrashType.HighSide ? -1 : 1) * Mathf.Sign(s.LeanDeg == 0 ? 1f : (float)s.LeanDeg);
                if (s.Speed > 1) _crashSpin += dt * (float)s.Speed * 6f;
                _crashOffset = Vector3.Lerp(_crashOffset, new Vector3(1.4f, -0.6f, 2.2f), dt * 2);
                targetPitch = 0;
            }
            else
            {
                _crashSpin = Mathf.Lerp(_crashSpin, 0, dt * 4);
                _crashOffset = Vector3.Lerp(_crashOffset, Vector3.zero, dt * 4);
            }

            _lean = Mathf.Lerp(_lean, targetLean, 1 - Mathf.Exp(-dt * 10));
            _pitch = Mathf.Lerp(_pitch, targetPitch, 1 - Mathf.Exp(-dt * 6));
            // Unity: positive rotation around +Z tilts +Y towards -X, i.e. to the rider's left.
            Body.localRotation = Quaternion.Euler(_pitch, _crashSpin, _lean);
            _rider.localPosition = _crashOffset;
            // Sink into the gravel while being cleared.
            float sink = _outSince < 0 ? 0 : Mathf.Clamp01((float)((time - _outSince - (ClearAfter - 1.5)) / 1.5));
            Body.localPosition = new Vector3(0, -1.6f * sink, 0);
        }
    }
}
