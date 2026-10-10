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

        private double _accumulator;
        private Action<RaceSimulation> _onClosed;
        private bool _resultsShown;
        private RaceSimulation _liveSim;

        public RaceReplay Replay { get; private set; }
        public bool IsReplay { get; private set; }
        private const int MaxStepsPerFrame = 600;

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
            view.Cam.SetMode(CameraMode.Follow, view.Bikes[view.SelectedRider].transform);
            view.Hud = RaceHud.Create(view, title);
            view.Speed = 1;
            return view;
        }

        public void SetSpeed(float speed)
        {
            Speed = speed;
        }

        public void Select(int riderIndex)
        {
            if (riderIndex < 0 || riderIndex >= Bikes.Count) return;
            SelectedRider = riderIndex;
            Cam.SetMode(CameraMode.Follow, Bikes[riderIndex].transform);
        }

        /// <summary>Jumps to the end of the race (headless steps) - used by "skip to results".</summary>
        public void FinishInstantly()
        {
            Sim.RunToEnd();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
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

            float scale = Mathf.Clamp(Cam.Distance / 55f, 1f, 7f);
            double length = Sim.Track.Length;
            for (int i = 0; i < Bikes.Count; i++)
            {
                var r = Sim.Riders[i];
                double delta = r.Distance - r.PrevDistance;
                if (delta < -length * 0.5) delta += length;
                if (delta > length * 0.5) delta -= length;
                double d = r.PrevDistance + delta * alpha;
                Bikes[i].UpdateVisual(Track, d, dt, scale);
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
            if (Cam != null) Destroy(Cam);
            Destroy(gameObject);
            if (_onClosed != null) _onClosed(Sim);
        }
    }
}
