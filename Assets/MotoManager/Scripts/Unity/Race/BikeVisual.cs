using MotoManager.Core.Data;
using MotoManager.Core.Race;
using UnityEngine;

namespace MotoManager.App.Race
{
    /// <summary>
    /// Low-poly bike + rider built from boxes in team colours. Purely visual: it reads the rider's simulated state
    /// and turns it into position, heading, lean (corners), pitch (braking / acceleration) and crash poses.
    /// </summary>
    public class BikeVisual : MonoBehaviour
    {
        public RiderRaceState State { get; private set; }
        public Color PrimaryColor { get; private set; }
        public Transform Body { get; private set; }

        private Transform _rider;
        private float _lean;
        private float _pitch;
        private float _lateral;
        private float _crashSpin;
        private Vector3 _crashOffset;

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
            var helmet = Color.HSVToRGB(hue, 0.55f, 0.95f);
            var black = new Color32(25, 25, 28, 255);
            var leathers = Color.Lerp(primary, Color.black, 0.35f);

            // Bike body, pivot at the contact patch line so leaning rotates around the tyres.
            Body = new GameObject("Body").transform;
            Body.SetParent(transform, false);
            var bike = new MeshBuilder();
            bike.Box(new Vector3(0, 0.32f, 0.68f), new Vector3(0.14f, 0.64f, 0.64f), black);       // front wheel
            bike.Box(new Vector3(0, 0.32f, -0.68f), new Vector3(0.2f, 0.64f, 0.64f), black);       // rear wheel
            bike.Box(new Vector3(0, 0.72f, 0.15f), new Vector3(0.5f, 0.5f, 1.3f), primary);       // fairing
            bike.Box(new Vector3(0, 0.98f, 0.65f), new Vector3(0.36f, 0.22f, 0.35f), secondary);  // screen / nose
            bike.Box(new Vector3(0, 0.92f, -0.62f), new Vector3(0.34f, 0.2f, 0.55f), secondary);  // tail
            bike.ToObject("BikeMesh", Body);

            _rider = new GameObject("Rider").transform;
            _rider.SetParent(Body, false);
            var rider = new MeshBuilder();
            rider.Box(new Vector3(0, 1.12f, -0.18f), new Vector3(0.46f, 0.42f, 0.62f), leathers, Quaternion.Euler(28, 0, 0)); // torso
            rider.Box(new Vector3(0, 1.36f, 0.18f), new Vector3(0.3f, 0.3f, 0.32f), helmet);                                   // helmet
            rider.Box(new Vector3(0.24f, 0.8f, -0.3f), new Vector3(0.16f, 0.4f, 0.5f), leathers);                             // legs
            rider.Box(new Vector3(-0.24f, 0.8f, -0.3f), new Vector3(0.16f, 0.4f, 0.5f), leathers);
            rider.ToObject("RiderMesh", _rider);
        }

        /// <summary>Called every frame by the race view with the interpolated distance along the lap.</summary>
        public void UpdateVisual(TrackRenderer track, double distance, float dt, float scale)
        {
            var s = State;
            float targetLateral = (float)s.LateralOffset * TrackRenderer.HalfWidth;
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

            // Lean: the simulation's lean angle (positive = left), smoothed.
            float targetLean = (float)s.LeanDeg;
            // Pitch: nose dives under braking, lifts slightly on corner exit / straights while accelerating.
            float targetPitch = 0;
            if (s.Phase == RidingPhase.Braking || (s.Speed < s.PrevSpeed - 0.3)) targetPitch = 4f;
            else if (s.Phase == RidingPhase.CornerExit || s.Phase == RidingPhase.Straight && s.Speed > s.PrevSpeed + 0.05) targetPitch = -2f;

            if (s.Status == RaceStatus.Crashed || (s.Status == RaceStatus.Retired && s.RetireReason != null && s.RetireReason.StartsWith("Crash")))
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

            bool hidden = s.Status == RaceStatus.Retired && s.Speed <= 0.01 && s.RetireReason != null && !s.RetireReason.StartsWith("Crash");
            if (Body.gameObject.activeSelf == hidden) Body.gameObject.SetActive(!hidden);
        }
    }
}
