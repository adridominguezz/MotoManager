using System;
using System.Collections.Generic;
using MotoManager.App.UI;
using MotoManager.Core.Race;
using UnityEngine;

namespace MotoManager.App.Race
{
    /// <summary>
    /// Plays a <see cref="RaceSimulation"/> on screen. The simulation always advances in fixed steps; the playback
    /// speed only changes how many steps run per frame, so 1x/2x/4x/8x and pauses never change the result.
    /// Bikes are drawn between the last two simulated states (interpolation) for smooth motion at any speed.
    /// </summary>
    public class RaceView : MonoBehaviour
    {
        public static readonly float[] Speeds = { 0, 1, 2, 4, 8 };

        public RaceSimulation Sim { get; private set; }
        public TrackRenderer Track { get; private set; }
        public RaceCamera Cam { get; private set; }
        public RaceHud Hud { get; private set; }
        public readonly List<BikeVisual> Bikes = new List<BikeVisual>();
        public float Speed { get; private set; }
        public int SelectedRider { get; private set; }
        private double _topSpeedSeen = 60;

        private double _accumulator;
        private Action<RaceSimulation> _onClosed;
        private bool _resultsShown;
        private RaceSimulation _liveSim;

        public RaceReplay Replay { get; private set; }
        public bool IsReplay { get; private set; }
        private const int MaxStepsPerFrame = 600;
        /// <summary>Camera distance beyond which riders are drawn as dots.</summary>
        public const float MarkerDistance = 260f;

        public static RaceView Create(RaceSimulation sim, string title, Action<RaceSimulation> onClosed)
        {
            var go = new GameObject("RaceView");
            var view = go.AddComponent<RaceView>();
            view.Sim = sim;
            view._liveSim = sim;
            view.Replay = RaceReplay.Begin(sim);
            view._onClosed = onClosed;
            view.Track = TrackRenderer.Create(sim.Track, go.transform);
            foreach (var r in sim.Riders) view.Bikes.Add(BikeVisual.Create(r, go.transform));

            var g = view.Track.Geometry;
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3((float)g.MinX, 0, (float)g.MinY), new Vector3((float)g.MaxX, 0, (float)g.MaxY));
            view.Cam = RaceCamera.Create(bounds);

            view.SelectedRider = 0;
            for (int i = 0; i < sim.Riders.Count; i++)
                if (sim.Riders[i].Entry.IsPlayer)
                {
                    view.SelectedRider = i;
                    break;
                }
            view.Hud = RaceHud.Create(view, title);
            GameAudio.RaceStart();
            if (sim.Tick == 0) view.StartIntro();
            else
            {
                view.Cam.SetMode(CameraMode.Follow, view.Bikes[view.SelectedRider].transform);
                view.Speed = 1;
            }
            return view;
        }

        public void SetSpeed(float speed)
        {
            if (InIntro) EndIntro();
            Speed = speed;
        }

        // ------------------------------------------------------------------ race-day presentation

        public const float IntroOrbit = 5f, IntroDescent = 2.5f;
        private float _introTime = -1;
        public bool InIntro { get { return _introTime >= 0; } }

        /// <summary>Orbit over the circuit with the race-day banner, then down to the grid. A tap skips it.</summary>
        public void StartIntro()
        {
            _introTime = 0;
            Speed = 0;
            Cam.SetMode(CameraMode.Intro);
            var c = Sim.Config.Circuit;
            Hud.ShowIntro(c != null ? c.Name : Sim.Track.CircuitId, c != null ? c.Country : "", true);
        }

        public void EndIntro()
        {
            if (!InIntro) return;
            _introTime = -1;
            Hud.ShowIntro("", "", false);
            Cam.SetMode(CameraMode.Follow, Bikes[SelectedRider].transform);
            Speed = 1;
            if (!Assistant.Seen("race"))
            {
                Speed = 0;
                Assistant.Show("race", new[] { "tut.race.1", "tut.race.2", "tut.race.3", "tut.race.4" }, () => Speed = 1);
            }
        }

        private void UpdateIntro(float dt)
        {
            _introTime += dt;
            if (_introTime > IntroOrbit && Cam.Mode == CameraMode.Intro) Cam.SetMode(CameraMode.Follow, Bikes[SelectedRider].transform);
            bool tap = Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
            if (_introTime > IntroOrbit + IntroDescent || (tap && _introTime > 0.4f)) EndIntro();
        }

        public void Select(int riderIndex)
        {
            if (riderIndex < 0 || riderIndex >= Bikes.Count) return;
            SelectedRider = riderIndex;
            Cam.SetMode(CameraMode.Follow, Bikes[riderIndex].transform);
        }

        private int _sector = -1;

        /// <summary>Cycles the fixed camera through the sectors of the circuit.</summary>
        public void NextSectorView()
        {
            int count = Mathf.Max(1, Sim.Track.SectorCount);
            _sector = (_sector + 1) % count;
            double len = Sim.Track.Length / count;
            double mid = len * (_sector + 0.5);
            // Frame the sector: centre of its points, distance from its extent.
            var min = new Vector3(float.MaxValue, 0, float.MaxValue);
            var max = new Vector3(float.MinValue, 0, float.MinValue);
            for (int i = 0; i <= 10; i++)
            {
                var p = Track.Point(len * _sector + len * i / 10, 0, 0);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            var size = max - min;
            Cam.ShowSector(Track.Point(mid, 0, 0) * 0.3f + (min + max) * 0.35f, Mathf.Max(size.x, size.z) * 1.3f + 60);
            Hud.ShowCameraLabel(Loc.T("race.camera.sector", _sector + 1));
        }

        /// <summary>Jumps to the end of the race (headless steps) - used by "skip to results".</summary>
        public void FinishInstantly()
        {
            Sim.RunToEnd();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (InIntro) UpdateIntro(dt);
            double step = Sim.Config.TimeStep;
            if (!Sim.IsFinished && Speed > 0)
            {
                _accumulator += dt * Speed;
                int steps = 0;
                while (_accumulator >= step && steps < MaxStepsPerFrame && !Sim.IsFinished)
                {
                    Sim.Step();
                    _accumulator -= step;
                    steps++;
                }
                if (steps == MaxStepsPerFrame) _accumulator = 0; // device too slow for this speed: drop time, never results
            }
            float alpha = Sim.IsFinished ? 1f : Mathf.Clamp01((float)(_accumulator / step));

            // Far away the bikes become dots (constant size on screen); close up, real bikes.
            bool markers = Cam.Distance > MarkerDistance;
            float scale = markers ? Cam.Distance * 0.014f : Mathf.Clamp(Cam.Distance / 55f, 1f, 4f);
            double length = Sim.Track.Length;
            for (int i = 0; i < Bikes.Count; i++)
            {
                var r = Sim.Riders[i];
                double delta = r.Distance - r.PrevDistance;
                if (delta < -length * 0.5) delta += length;
                if (delta > length * 0.5) delta -= length;
                double d = r.PrevDistance + delta * alpha;
                Bikes[i].UpdateVisual(Track, d, dt, scale, markers, Sim.Time, i == SelectedRider);
            }

            // Sound: the followed bike's engine (its revs), the field and the crowd.
            int running = 0;
            for (int i = 0; i < Sim.Riders.Count; i++)
            {
                var rr = Sim.Riders[i];
                if (rr.Status == RaceStatus.Racing) running++;
                if (rr.Speed > _topSpeedSeen) _topSpeedSeen = rr.Speed;
            }
            var followed = Sim.Riders[SelectedRider];
            GameAudio.RaceUpdate(followed.Speed, _topSpeedSeen, Mathf.Clamp01(1f - Cam.Distance / 450f), running, Speed <= 0 || Sim.IsFinished);

            // The followed rider is out of the race: follow the next of the player's riders, or the leader.
            if (Cam.Mode == CameraMode.Follow && !Bikes[SelectedRider].Visible)
            {
                int next = -1;
                for (int i = 0; i < Bikes.Count; i++)
                    if (Bikes[i].Visible && Sim.Riders[i].Entry.IsPlayer) { next = i; break; }
                if (next < 0)
                {
                    var leader = Sim.RiderAtPosition(1);
                    if (leader != null && Bikes[leader.Index].Visible) next = leader.Index;
                }
                if (next >= 0) Select(next);
            }

            if (Sim.IsFinished && !_resultsShown && !IsReplay)
            {
                _resultsShown = true;
                Replay.Finish(Sim);
                Hud.ShowResults();
            }
        }

        /// <summary>Re-simulates the finished race from the start (deterministic replay).</summary>
        public void StartReplay()
        {
            IsReplay = true;
            UseSimulation(Replay.CreateSimulation());
            Speed = 2;
            Hud.EnterReplay();
        }

        public void ExitReplay()
        {
            IsReplay = false;
            UseSimulation(_liveSim);
            Hud.ExitReplay();
        }

        /// <summary>Jumps by whole laps in the replay (re-simulates headless up to the target tick).</summary>
        public void SeekLaps(int laps)
        {
            if (!IsReplay) return;
            double lap = Sim.Riders.Count > 0 && Sim.Riders[0].ReferenceLap > 0 ? Sim.Riders[0].ReferenceLap : 100;
            long ticksPerLap = (long)(lap / Sim.Config.TimeStep);
            long target = Math.Max(0, Math.Min(Replay.TotalTicks, Sim.Tick + laps * ticksPerLap));
            UseSimulation(Replay.CreateAt(target));
        }

        private void UseSimulation(RaceSimulation sim)
        {
            Sim = sim;
            _accumulator = 0;
            for (int i = 0; i < Bikes.Count; i++) Bikes[i].Rebind(sim.Riders[i]);
        }

        public void Close()
        {
            if (IsReplay) UseSimulation(_liveSim);
            Sim = _liveSim;
            Destroy(Hud.gameObject);
            GameAudio.RaceEnd();
            if (Cam != null) Destroy(Cam);
            Destroy(gameObject);
            if (_onClosed != null) _onClosed(Sim);
        }
    }
}
