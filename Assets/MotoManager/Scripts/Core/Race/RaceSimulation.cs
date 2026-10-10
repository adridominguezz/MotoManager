using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Race
{
    /// <summary>
    /// Deterministic micro simulation of a session. Fixed time step; everything random comes from a seeded stream.
    /// Given the same <see cref="RaceConfig"/> (seed, entries, weather) and the same command log the result is
    /// identical, which is what replays, tests and balance tooling rely on.
    ///
    /// The simulation knows nothing about rendering. <see cref="Riders"/> exposes the per-rider state
    /// (distance, speed, lean, phase, tires, status...) and <see cref="Events"/> the race log.
    /// </summary>
    public class RaceSimulation
    {
        public readonly RaceConfig Config;
        public readonly CategoryRules Rules;
        public readonly SimulationTuning Tuning;
        public readonly TrackModel Track;
        public readonly WeatherModel Weather;
        public readonly List<RiderRaceState> Riders = new List<RiderRaceState>();
        public readonly List<RaceEvent> Events = new List<RaceEvent>();
        public readonly List<RaceCommand> CommandLog = new List<RaceCommand>();

        public double Time { get; private set; }
        public long Tick { get; private set; }
        public bool IsFinished { get; private set; }
        public bool ChequeredFlag { get; private set; }
        public bool WhiteFlag { get; private set; }
        public int LeaderLap { get; private set; }
        public double FastestLap = double.MaxValue;
        public int FastestLapRider = -1;
        public int TotalLaps { get { return Config.Laps; } }

        private readonly DeterministicRandom _rng;
        private readonly List<RaceCommand> _scheduled = new List<RaceCommand>();
        private readonly List<RaceCommand> _incoming = new List<RaceCommand>();
        private readonly int[] _order;
        private readonly double[] _yellowUntil;
        private readonly double _mass;
        private readonly double _cpSpacing;
        private readonly int _cpPerLap;
        private readonly double[] _firstPassage;
        private readonly double[][] _passage;
        private readonly RaceAI _ai;
        private WeatherCondition _lastCondition;
        private int _leaderIndex = -1;
        private bool _lastLapAnnounced;
        private double _maxLean;

        public RaceSimulation(RaceConfig config, IList<RaceCommand> replayCommands = null)
        {
            Config = config;
            Rules = config.Rules;
            Tuning = config.Tuning ?? new SimulationTuning();
            Track = TrackModel.Get(config.Circuit);
            _rng = new DeterministicRandom(config.Seed);
            Weather = new WeatherModel(config.Weather, Tuning, config.Seed);
            _lastCondition = Weather.Condition;
            _maxLean = Math.Tan(Rules.Physics.MaxLeanDeg * Math.PI / 180.0);

            var ch = Rules.Chassis;
            _mass = ch.MinCombinedWeightKg > 0 ? Math.Max(ch.MinCombinedWeightKg, ch.MinWeightKg + ch.AverageRiderKg) : ch.MinWeightKg + ch.AverageRiderKg;

            _cpPerLap = Math.Max(8, (int)Math.Ceiling(Track.Length / 100.0));
            _cpSpacing = Track.Length / _cpPerLap;
            int totalCp = (config.Laps + 3) * _cpPerLap;
            _firstPassage = new double[totalCp];
            for (int i = 0; i < totalCp; i++) _firstPassage[i] = double.NaN;
            _passage = new double[config.Entries.Count][];
            _yellowUntil = new double[Track.Segments.Count];

            var entries = new List<RaceEntry>(config.Entries);
            entries.Sort((a, b) => a.GridPosition.CompareTo(b.GridPosition));
            double warmers = Math.Max(Rules.Tires.WarmerTemperature, Weather.TrackTemp);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var r = new RiderRaceState { Index = i, Entry = e, SectorTimes = new double[Track.SectorCount] };
                TireModel.Fit(r.Front, CompoundFor(e.FrontCompound), warmers);
                TireModel.Fit(r.Rear, CompoundFor(e.RearCompound), warmers);
                r.PitCompound = e.SpareCompound;
                r.PendingLongLaps = e.PendingLongLaps;
                r.Fuel = config.FuelLimitLitres;
                _passage[i] = new double[totalCp];
                if (config.TimeTrial)
                {
                    r.Status = RaceStatus.Racing;
                    r.Lap = -1;
                    r.Distance = Track.PitExit;
                    r.Speed = 25;
                    r.LapStartTime = double.NaN;
                }
                else
                {
                    int row = i / 3;
                    int col = i % 3;
                    r.Lap = -1;
                    r.Distance = Track.Length - (8 + row * 9 + col * 2.5);
                    r.LateralOffset = (col - 1) * 0.55;
                    r.TargetLateral = r.LateralOffset;
                    r.Status = RaceStatus.Grid;
                }
                r.PrevDistance = r.Distance;
                r.SegmentIndex = Track.SegmentIndexAt(r.Distance);
                Riders.Add(r);
            }
            _order = new int[Riders.Count];
            for (int i = 0; i < _order.Length; i++) _order[i] = i;
            _ai = new RaceAI(this);

            if (replayCommands != null) _scheduled.AddRange(replayCommands);

            if (!config.TimeTrial)
            {
                foreach (var r in Riders) r.ReferenceLap = EstimateLap(config, r.Entry);
                SetupStart();
            }
        }

        /// <summary>One clean flying lap on the race tires, used for relative pace (overtaking decisions, AI).</summary>
        public static double EstimateLap(RaceConfig config, RaceEntry entry)
        {
            var cfg = new RaceConfig
            {
                Rules = config.Rules,
                Circuit = config.Circuit,
                Tuning = config.Tuning,
                SessionType = SessionType.Practice,
                Laps = 1,
                Seed = config.Seed,
                TimeStep = 0.2,
                Weather = config.Weather,
                TimeTrial = true,
                TrackEvolution = config.TrackEvolution
            };
            cfg.Entries.Add(new RaceEntry
            {
                Rider = entry.Rider, Team = entry.Team, Bike = entry.Bike, ManufacturerId = entry.ManufacturerId,
                FrontCompound = entry.FrontCompound, RearCompound = entry.RearCompound, SetupQuality = entry.SetupQuality, Form = entry.Form, GridPosition = 1
            });
            var sim = new RaceSimulation(cfg);
            var st = sim.Riders[0];
            st.LapNoise = 1;
            sim._noiseless = true;
            sim.RunToEnd();
            return st.BestLapTime < double.MaxValue ? st.BestLapTime : config.Circuit.LengthM / 40.0;
        }

        private bool _noiseless;

        public TireCompoundSpec CompoundFor(TireCompound compound)
        {
            if (Rules.Tires.Has(compound)) return Rules.Tires.Get(compound);
            // Fallback to the closest available compound.
            if (compound == TireCompound.Rain || compound == TireCompound.Intermediate)
            {
                foreach (var c in Rules.Tires.Compounds) if (c.IsWet) return c;
            }
            foreach (var c in Rules.Tires.Compounds) if (!c.IsWet) return c;
            return Rules.Tires.Compounds[0];
        }

        private void SetupStart()
        {
            AddEvent(RaceEventType.RaceStart, -1, -1, Config.SessionName + " start - " + Weather.Describe(), true);
            foreach (var r in Riders)
            {
                var a = r.Entry.Rider.Attributes;
                r.ReactionTime = 0.17 + Math.Abs(_rng.Gaussian(0, 0.07)) * (1.6 - a.Starts / 100.0);
                double jump = Tuning.JumpStartChance * (1 + MathX.Centered(a.Aggression)) * (1.3 - a.Mentality / 100.0);
                if (_rng.Chance(jump))
                {
                    r.ReactionTime = 0.02;
                    AddEvent(RaceEventType.JumpStart, r.Index, -1, r.Entry.Rider.ShortName + " jumped the start", true);
                    ApplyInfraction(r, InfractionType.JumpStart);
                }
            }
        }

        // ------------------------------------------------------------------ commands

        /// <summary>Queue a player command; it is applied (and logged) on the next tick.</summary>
        public void Enqueue(RaceCommand command)
        {
            _incoming.Add(command);
        }

        private void ApplyCommands()
        {
            for (int i = 0; i < _incoming.Count; i++)
            {
                _incoming[i].Tick = Tick;
                _scheduled.Add(_incoming[i]);
            }
            _incoming.Clear();
            for (int i = 0; i < _scheduled.Count; i++)
            {
                var c = _scheduled[i];
                if (c.Tick != Tick) continue;
                ApplyCommand(c);
                CommandLog.Add(c);
            }
            _scheduled.RemoveAll(c => c.Tick <= Tick);
        }

        private void ApplyCommand(RaceCommand c)
        {
            if (c.RiderIndex < 0 || c.RiderIndex >= Riders.Count) return;
            var r = Riders[c.RiderIndex];
            switch (c.Type)
            {
                case RaceCommandType.SetPace:
                    r.Pace = c.Pace;
                    break;
                case RaceCommandType.SetRiderMode:
                    r.Mode = c.Mode;
                    break;
                case RaceCommandType.RequestPit:
                    if (c.Pit == PitRequestType.BikeSwap && !Rules.PitLane.AllowsBikeSwap) return;
                    if (c.Pit == PitRequestType.TireChange && !Rules.PitLane.AllowsTireChange) return;
                    r.PitRequest = c.Pit;
                    r.PitCompound = c.Compound;
                    break;
                case RaceCommandType.CancelPit:
                    r.PitRequest = PitRequestType.None;
                    break;
                case RaceCommandType.SetAutoManaged:
                    r.Entry.AutoManaged = c.Flag;
                    break;
            }
            AddEvent(RaceEventType.CommandApplied, r.Index, -1, r.Entry.Rider.ShortName + ": " + c.Type + " " + (c.Type == RaceCommandType.SetPace ? c.Pace.ToString() : c.Type == RaceCommandType.SetRiderMode ? c.Mode.ToString() : c.Type == RaceCommandType.RequestPit ? c.Pit + " (" + c.Compound + ")" : ""), false);
        }

        // ------------------------------------------------------------------ main loop

        public void Step()
        {
            if (IsFinished) return;
            Tick++;
            double dt = Config.TimeStep;
            Time += dt;
            ApplyCommands();

            Weather.Update(dt, RunningShare());
            if (Weather.Condition != _lastCondition)
            {
                AddEvent(RaceEventType.WeatherChange, -1, -1, "Conditions: " + Weather.Condition, true);
                if (!WhiteFlag && Weather.RainIntensity > 0.04 && (Rules.PitLane.AllowsBikeSwap || Rules.PitLane.AllowsTireChange) && !Config.TimeTrial)
                {
                    WhiteFlag = true;
                    AddEvent(RaceEventType.WhiteFlag, -1, -1, "White flag: riders may pit to change " + (Rules.PitLane.AllowsBikeSwap ? "bikes" : "tires"), true);
                }
                _lastCondition = Weather.Condition;
            }

            SortOrder();
            for (int k = 0; k < _order.Length; k++)
            {
                var r = Riders[_order[k]];
                UpdateRider(r, k, dt);
            }
            UpdateTiming();
            if (Tick % 20 == 0) _ai.Periodic();
            CheckFinish();
        }

        /// <summary>Runs until the session ends. Returns the number of ticks simulated.</summary>
        public long RunToEnd()
        {
            long start = Tick;
            while (!IsFinished) Step();
            return Tick - start;
        }

        private double RunningShare()
        {
            int n = 0;
            foreach (var r in Riders) if (r.OnTrack) n++;
            return Riders.Count == 0 ? 0 : (double)n / Riders.Count;
        }

        private void SortOrder()
        {
            double L = Track.Length;
            // insertion sort: the order barely changes between ticks
            for (int i = 1; i < _order.Length; i++)
            {
                int idx = _order[i];
                double key = SortKey(Riders[idx], L);
                int j = i - 1;
                while (j >= 0 && SortKey(Riders[_order[j]], L) < key)
                {
                    _order[j + 1] = _order[j];
                    j--;
                }
                _order[j + 1] = idx;
            }
        }

        private double SortKey(RiderRaceState r, double L)
        {
            if (r.Status == RaceStatus.Finished) return 1e12 - r.FinishTime * 1000 + r.Lap * L * 1e-3;
            if (r.Status == RaceStatus.Retired || r.Status == RaceStatus.Disqualified) return -1e9 + r.Progress(L);
            return r.Progress(L);
        }

        private RiderRaceState FindAhead(int orderIndex)
        {
            for (int k = orderIndex - 1; k >= 0; k--)
            {
                var o = Riders[_order[k]];
                if (o.Status == RaceStatus.Racing || o.Status == RaceStatus.Grid) return o;
                if (o.Status == RaceStatus.Finished) return null;
            }
            return null;
        }

        private RiderRaceState FindBehind(int orderIndex)
        {
            for (int k = orderIndex + 1; k < _order.Length; k++)
            {
                var o = Riders[_order[k]];
                if (o.Status == RaceStatus.Racing) return o;
            }
            return null;
        }

        // ------------------------------------------------------------------ per rider update

        private void UpdateRider(RiderRaceState r, int orderIndex, double dt)
        {
            r.PrevDistance = r.Distance;
            r.PrevSpeed = r.Speed;
            switch (r.Status)
            {
                case RaceStatus.Grid:
                    if (Time >= r.ReactionTime)
                    {
                        r.Status = RaceStatus.Racing;
                        UpdateRacing(r, orderIndex, dt);
                    }
                    return;
                case RaceStatus.Racing:
                    UpdateRacing(r, orderIndex, dt);
                    return;
                case RaceStatus.InPitLane:
                    UpdatePitLane(r, dt);
                    return;
                case RaceStatus.Crashed:
                    UpdateCrashed(r, dt);
                    return;
                case RaceStatus.Finished:
                    // Slow-down lap for the renderer.
                    r.Speed = MathX.MoveTowards(r.Speed, 25, dt * 4);
                    AdvanceFree(r, r.Speed * dt);
                    return;
                default:
                    if (r.Speed > 0)
                    {
                        r.Speed = Math.Max(0, r.Speed - dt * 8);
                        AdvanceFree(r, r.Speed * dt);
                        r.LateralOffset = MathX.MoveTowards(r.LateralOffset, 1.6 * Math.Sign(r.LateralOffset == 0 ? 1 : r.LateralOffset), dt * 0.6);
                    }
                    return;
            }
        }

        private void AdvanceFree(RiderRaceState r, double move)
        {
            r.Distance += move;
            if (r.Distance >= Track.Length)
            {
                r.Distance -= Track.Length;
                if (r.Status != RaceStatus.Finished) r.Lap++;
            }
            r.SegmentIndex = Track.SegmentIndexAt(r.Distance);
            r.LeanDeg = MathX.MoveTowards(r.LeanDeg, 0, 60 * Config.TimeStep);
        }

        private void UpdateRacing(RiderRaceState r, int orderIndex, double dt)
        {
            var seg = Track.Segments[r.SegmentIndex];
            double L = Track.Length;
            double wet = Weather.TrackWetness;
            double gf = TireModel.Grip(r.Front, wet, Tuning);
            double gr = TireModel.Grip(r.Rear, wet, Tuning);
            double usage = Usage(r, orderIndex);

            RiderRaceState ahead = Config.TimeTrial ? null : FindAhead(orderIndex);
            double myProgress = r.Progress(L);

            // --- slipstream
            double dragMult = 1;
            if (ahead != null && !seg.IsCorner)
            {
                double gap = ahead.Lap * L + ahead.PrevDistance - myProgress;
                if (gap > 0 && gap < Tuning.SlipstreamRange)
                {
                    double tow = Tuning.SlipstreamDragReduction * Rules.SlipstreamFactor * (0.35 + 0.65 * seg.SlipstreamPotential);
                    dragMult -= tow * (1 - gap / Tuning.SlipstreamRange);
                }
            }

            double aBrake = BrakeDecel(r, gf, usage);
            double target = TargetSpeed(r, seg, gf, gr, usage, aBrake);
            double v = r.Speed;
            bool braking = false;
            if (v > target + 0.01)
            {
                double nv = Math.Max(target, v - aBrake * dt);
                braking = (v - nv) / dt > 2.0;
                v = nv;
            }
            else
            {
                bool exit = seg.IsCorner && (r.Distance - seg.Start) / seg.Length > 0.6;
                double a = Accel(r, v, gr, dragMult, exit);
                if (Time - r.ReactionTime < 3 && !Config.TimeTrial)
                    a *= 0.8 + 0.2 * r.Entry.Rider.Attributes.Starts / 100.0;
                v = a >= 0 ? Math.Min(target, v + a * dt) : Math.Max(0, v + a * dt);
            }
            v = Math.Max(0, v);

            double move = (r.Speed + v) * 0.5 * dt;
            if (r.DelayRemaining > 0)
            {
                double k = r.DelayRate <= 0 ? 0.5 : r.DelayRate;
                move *= 1 - k;
                r.DelayRemaining -= dt * k;
                if (r.DelayRemaining <= 0) r.TargetLateral = 0;
            }
            r.Speed = v;

            // --- traffic
            if (ahead != null)
                move = ResolveTraffic(r, ahead, seg, myProgress, move, gf, gr, target, orderIndex);

            // --- advance along the track (may cross several segments and the line)
            double newDistance = r.Distance + move;
            bool crossedPitEntry = !Config.TimeTrial && r.Distance < Track.PitEntry && newDistance >= Track.PitEntry && r.Lap >= 0;
            while (newDistance >= Track.Segments[r.SegmentIndex].End)
            {
                int next = r.SegmentIndex + 1;
                if (next >= Track.Segments.Count)
                {
                    newDistance -= L;
                    r.Distance = newDistance;
                    r.SegmentIndex = 0;
                    OnCrossLine(r);
                    if (r.Status != RaceStatus.Racing) return;
                }
                else
                {
                    r.SegmentIndex = next;
                }
                OnEnterSegment(r, Track.Segments[r.SegmentIndex], orderIndex);
                if (r.Status != RaceStatus.Racing) return;
            }
            r.Distance = newDistance;

            if (crossedPitEntry && (r.PitRequest != PitRequestType.None || r.PendingRideThrough) && !ChequeredFlag)
            {
                EnterPitLane(r);
                return;
            }

            // --- phase, lean, lateral
            seg = Track.Segments[r.SegmentIndex];
            double frac = (r.Distance - seg.Start) / seg.Length;
            if (seg.IsCorner)
            {
                if (braking && frac < 0.4) r.Phase = RidingPhase.CornerEntry;
                else if (frac < 0.3) r.Phase = RidingPhase.CornerEntry;
                else if (frac < 0.65) r.Phase = RidingPhase.MidCorner;
                else r.Phase = RidingPhase.CornerExit;
                double lean = Math.Atan(v * v / (MathX.G * seg.Radius * LineRadiusFactor)) * 180 / Math.PI;
                lean = Math.Min(lean, Rules.Physics.MaxLeanDeg);
                double ramp = frac < 0.2 ? 0.5 + frac * 2.5 : (frac > 0.8 ? 0.5 + (1 - frac) * 2.5 : 1);
                double targetLean = lean * ramp * Math.Sign(seg.AngleDeg);
                r.LeanDeg = MathX.MoveTowards(r.LeanDeg, targetLean, 160 * dt);
            }
            else
            {
                r.Phase = braking ? RidingPhase.Braking : RidingPhase.Straight;
                r.LeanDeg = MathX.MoveTowards(r.LeanDeg, 0, 160 * dt);
            }
            if (r.OnLongLap && !seg.LongLapZone && seg.Index != Track.LongLapSegment) r.OnLongLap = false;
            if (r.AttemptTarget < 0 && r.DelayRemaining <= 0 && r.TargetLateral != 0 && (seg.IsCorner || seg.End - r.Distance < 120))
                r.TargetLateral = 0;
            double lateralTarget = r.OnLongLap ? 1.5 * -Math.Sign(seg.AngleDeg == 0 ? 1 : seg.AngleDeg) : r.TargetLateral;
            r.LateralOffset = MathX.MoveTowards(r.LateralOffset, lateralTarget, dt * 0.8);

            // --- machinery
            UpdateMachinery(r, seg, move, dt);
            UpdateCheckpoints(r);
        }

        public double LineRadiusFactor { get { return 1.3; } }

        private double Usage(RiderRaceState r, int orderIndex)
        {
            double u = r.LapNoise;
            if (r.Pace == PaceMode.Push) u *= Tuning.PushUsage;
            else if (r.Pace == PaceMode.Conserve) u *= Tuning.ConserveUsage;
            u *= 0.994 + 0.006 * MathX.Clamp01(r.Entry.SetupQuality);
            u *= r.Entry.Form;
            if (Config.SessionType == SessionType.Qualifying)
                u *= 1 + 0.004 * MathX.Centered(r.Entry.Rider.Attributes.QualifyingSkill) + (r.Entry.Rider.HasStyle(RidingStyle.QualifyingSpecialist) ? 0.002 : 0);
            else
                u *= 1 + 0.003 * MathX.Centered(r.Entry.Rider.Attributes.RaceSkill) * Rules.RiderInfluence + (r.Entry.Rider.HasStyle(RidingStyle.RaceSpecialist) ? 0.0015 : 0);
            if (r.Mode == RiderMode.Defend && !Config.TimeTrial)
            {
                var behind = FindBehind(orderIndex);
                if (behind != null && r.Progress(Track.Length) - behind.Progress(Track.Length) < 20) u *= 1 - Tuning.DefendCornerLoss;
            }
            u *= 1 - r.HandlingLoss;
            return u;
        }

        private double CornerSpeed(RiderRaceState r, TrackSegment seg, double gf, double gr, double usage)
        {
            var a = r.Entry.Rider.Attributes;
            var bike = r.Entry.Bike;
            double wet = Weather.TrackWetness;
            double grip = 0.55 * Math.Min(gf, gr) + 0.45 * (gf + gr) * 0.5;
            double spread = Tuning.BikeAttributeSpread * Rules.Physics.BikeSpread;
            double bikeF = MathX.AttrFactor(bike[PerfAttr.MidCorner] * 0.6 + bike[PerfAttr.CornerEntry] * 0.4, spread);
            if (wet > 0.2) bikeF *= MathX.AttrFactor(bike[PerfAttr.WetPerformance], spread * wet);
            double skill = a.Cornering + (a.WetSkill - a.Cornering) * MathX.Clamp01(wet * 1.3);
            double riderF = MathX.AttrFactor(skill, Tuning.RiderCorneringSpread * Rules.RiderInfluence);
            if (r.Entry.Rider.HasStyle(RidingStyle.CornerSpecialist)) riderF *= 1.004;
            if (wet > 0.3 && r.Entry.Rider.HasStyle(RidingStyle.WetSpecialist)) riderF *= 1.01;
            double surface = (0.97 + 0.06 * Config.Circuit.SurfaceGrip) * (1 + Config.TrackEvolution);
            double mu = Rules.Physics.LateralGrip * grip * bikeF * riderF * usage * surface;
            mu = Math.Min(mu, _maxLean);
            double radius = seg.Radius * LineRadiusFactor;
            return Math.Sqrt(mu * MathX.G * radius);
        }

        private double TopSpeed(RiderRaceState r)
        {
            return Rules.Physics.TopSpeedKph / 3.6 * MathX.AttrFactor(r.Entry.Bike[PerfAttr.TopSpeed], Tuning.TopSpeedSpread * Rules.Physics.BikeSpread);
        }

        private double BrakeDecel(RiderRaceState r, double gf, double usage)
        {
            var a = r.Entry.Rider.Attributes;
            double bikeF = MathX.AttrFactor(r.Entry.Bike[PerfAttr.Braking], Tuning.BikeAttributeSpread * Rules.Physics.BikeSpread);
            double riderF = MathX.AttrFactor(a.Braking, Tuning.RiderBrakingSpread * Rules.RiderInfluence);
            if (r.Entry.Rider.HasStyle(RidingStyle.LateBraker)) riderF *= 1.015;
            double grip = 0.35 + 0.65 * gf;
            return Rules.Physics.BrakingG * MathX.G * grip * bikeF * riderF * usage;
        }

        private double Accel(RiderRaceState r, double v, double gr, double dragMult, bool cornerExit)
        {
            var bike = r.Entry.Bike;
            var a = r.Entry.Rider.Attributes;
            double spread = Rules.Physics.BikeSpread;
            double power = Rules.Engine.ApproxPowerKw * 1000 * MathX.AttrFactor(bike[PerfAttr.Power], Tuning.PowerSpread * spread) * (1 - r.PowerLoss);
            if (r.Pace == PaceMode.Conserve && Config.FuelLimitLitres > 0) power *= 0.985;
            if (Config.FuelLimitLitres > 0 && r.Fuel <= 0) power *= 0.15;
            double vmax = TopSpeed(r);
            double c = power / (vmax * vmax * vmax);
            double aPower = power / (_mass * Math.Max(v, 8));
            double throttle = a.ThrottleControl + (bike[PerfAttr.Traction] - a.ThrottleControl) * Rules.Electronics.AssistLevel * 0.5;
            double traction = Rules.Physics.TractionG * MathX.G * (0.25 + 0.75 * gr) *
                              MathX.AttrFactor((bike[PerfAttr.Traction] + bike[PerfAttr.Acceleration]) * 0.5, Tuning.BikeAttributeSpread * spread) *
                              MathX.AttrFactor(throttle, Tuning.RiderThrottleSpread * Rules.RiderInfluence);
            if (cornerExit)
            {
                traction *= 0.78 * MathX.AttrFactor(bike[PerfAttr.CornerExit], Tuning.BikeAttributeSpread * spread);
            }
            double drag = c * dragMult * v * v / _mass;
            return Math.Min(aPower, traction) - drag;
        }

        private double TargetSpeed(RiderRaceState r, TrackSegment seg, double gf, double gr, double usage, double aBrake)
        {
            double vTop = TopSpeed(r);
            double limit = vTop;
            double v = r.Speed;
            if (seg.IsCorner)
            {
                double vc = CornerSpeed(r, seg, gf, gr, usage);
                double frac = (r.Distance - seg.Start) / seg.Length;
                var next = Track.Next(seg);
                if (frac > 0.6 && !next.IsCorner) vc *= 1 + 0.35 * (frac - 0.6) / 0.4;
                limit = Math.Min(limit, vc);
            }
            double look = v * v / (2 * aBrake) + 30;
            double dist = seg.End - r.Distance;
            int idx = seg.Index;
            for (int guard = 0; guard < 12 && dist < look; guard++)
            {
                idx = (idx + 1) % Track.Segments.Count;
                var s = Track.Segments[idx];
                if (s.IsCorner)
                {
                    double vc = CornerSpeed(r, s, gf, gr, usage);
                    double allowed = Math.Sqrt(vc * vc + 2 * aBrake * Math.Max(0, dist));
                    if (allowed < limit) limit = allowed;
                }
                dist += s.Length;
            }
            return limit;
        }

        // ------------------------------------------------------------------ traffic & overtaking

        private double ResolveTraffic(RiderRaceState r, RiderRaceState ahead, TrackSegment seg, double myProgress, double move,
            double gf, double gr, double target, int orderIndex)
        {
            double L = Track.Length;
            double aheadProgress = ahead.Progress(L);
            double newProgress = myProgress + move;
            double minGap = Tuning.MinFollowGap;

            // Blue flags: lapped riders move out of the way.
            if (aheadProgress - myProgress < L * 0.5 && ahead.Lap < r.Lap) return move;
            if (ahead.Status == RaceStatus.Grid) return Math.Min(move, Math.Max(0, aheadProgress - minGap * 0.5 - myProgress));

            if (r.AttemptTarget == ahead.Index && r.AttemptSucceeds)
            {
                if (myProgress > r.AttemptUntilProgress)
                {
                    r.AttemptTarget = -1;
                    r.AttemptSucceeds = false;
                    r.TargetLateral = 0;
                }
                else
                {
                    return move;
                }
            }

            double gapAfter = aheadProgress - newProgress;
            if (gapAfter >= minGap) return move;

            // Side by side on a straight (start, slipstream passes): no blocking while lines differ.
            if (!seg.IsCorner && Math.Abs(r.LateralOffset - ahead.LateralOffset) > 0.4) return move;

            int key = r.Lap * 1000 + seg.Index;
            if (r.LastInteractionSegment != key)
            {
                r.LastInteractionSegment = key;
                if (TryStartOvertake(r, ahead, seg, gf, gr, target))
                    return move;
            }

            // Stuck behind: match the rider in front.
            r.StuckTime += Config.TimeStep;
            double allowed = Math.Max(0, aheadProgress - minGap - myProgress);
            if (allowed < move)
            {
                move = allowed;
                r.Speed = Math.Min(r.Speed, ahead.Speed);
            }
            return move;
        }

        private bool TryStartOvertake(RiderRaceState r, RiderRaceState ahead, TrackSegment seg, double gf, double gr, double target)
        {
            if (_yellowUntil[seg.Index] > Time) return false;
            var fa = r.Entry.Rider.Attributes;
            var la = ahead.Entry.Rider.Attributes;
            // Relative clean-air pace (fraction of a lap) plus current tire state: a clearly faster rider tries more and succeeds more.
            double paceGap = r.ReferenceLap > 0 && ahead.ReferenceLap > 0 ? (ahead.ReferenceLap - r.ReferenceLap) / ahead.ReferenceLap : 0;
            double desire = (r.Mode == RiderMode.Attack ? 1.4 : r.Mode == RiderMode.Defend ? 0.55 : 1.0) *
                            (0.7 + fa.Aggression / 100.0 * 0.6) * (1 + Math.Min(1, r.StuckTime / 25.0)) *
                            MathX.Clamp(1 + paceGap * 250, 0.4, 3.0);
            if (r.Pace == PaceMode.Conserve) desire *= 0.6;
            double pStart = Tuning.OvertakeBaseChance * (0.25 + seg.OvertakingPotential) * desire;
            if (!_rng.Chance(pStart)) return false;

            double aheadGf = TireModel.Grip(ahead.Front, Weather.TrackWetness, Tuning);
            double aheadGr = TireModel.Grip(ahead.Rear, Weather.TrackWetness, Tuning);
            double skill = (fa.Overtaking - la.Defending) / 50.0 * 0.8;
            double paceAdv = (target - ahead.Speed) / Math.Max(10, ahead.Speed) * 5;
            double tireAdv = ((gf + gr) - (aheadGf + aheadGr)) * 3;
            double defence = (ahead.Mode == RiderMode.Defend ? -0.5 : 0) + (ahead.Entry.Rider.HasStyle(RidingStyle.DefensiveRider) ? -0.25 : 0);
            double technique = 0;
            if (seg.IsCorner && seg.BrakingIntensity > 0.3)
                technique += (fa.Braking - la.Braking) / 100.0 + (r.Entry.Rider.HasStyle(RidingStyle.LateBraker) ? 0.3 : 0);
            else if (!seg.IsCorner)
                technique += (r.Entry.Bike[PerfAttr.TopSpeed] - ahead.Entry.Bike[PerfAttr.TopSpeed]) / 40.0 * (0.5 + seg.SlipstreamPotential * Rules.SlipstreamFactor);
            double pSuccess = MathX.Logistic(-0.7 + skill + paceAdv + paceGap * 300 + tireAdv + defence + technique + seg.OvertakingPotential * 1.3);

            double attackRisk = (r.Mode == RiderMode.Attack ? Tuning.AttackRisk : 1.0) * (0.6 + fa.Aggression / 100.0) * (1.4 - fa.RiskManagement / 100.0);
            if (_rng.Chance(pSuccess))
            {
                r.AttemptTarget = ahead.Index;
                r.AttemptSucceeds = true;
                double window = seg.Length + Track.Next(seg).Length + 40;
                r.AttemptUntilProgress = r.Progress(Track.Length) + window;
                double side = seg.IsCorner ? Math.Sign(seg.AngleDeg) : (Track.Next(seg).AngleDeg >= 0 ? 1 : -1);
                r.TargetLateral = 0.55 * side;
                ahead.TargetLateral = -0.25 * side;
                if (_rng.Chance(Tuning.ContactChance * 0.35 * attackRisk)) Contact(r, ahead);
                return true;
            }

            AddEvent(RaceEventType.OvertakeFailed, r.Index, ahead.Index, r.Entry.Rider.ShortName + " tries a move on " + ahead.Entry.Rider.ShortName + " but has to back out", false);
            Delay(r, _rng.Range(0.05, 0.25), 0.4);
            if (_rng.Chance(Tuning.ContactChance * attackRisk)) Contact(r, ahead);
            return false;
        }

        private void Contact(RiderRaceState attacker, RiderRaceState victim)
        {
            AddEvent(RaceEventType.Contact, attacker.Index, victim.Index, "Contact between " + attacker.Entry.Rider.ShortName + " and " + victim.Entry.Rider.ShortName, true);
            bool victimDown = false;
            if (_rng.Chance(Tuning.ContactCrashChance * 0.6))
            {
                Crash(victim, CrashType.ContactCrash, "taken out by " + attacker.Entry.Rider.ShortName);
                victimDown = true;
            }
            else
            {
                Delay(victim, _rng.Range(0.3, 1.2), 0.5);
            }
            if (_rng.Chance(Tuning.ContactCrashChance * 0.4))
            {
                Crash(attacker, CrashType.ContactCrash, "contact with " + victim.Entry.Rider.ShortName);
            }
            else
            {
                Delay(attacker, _rng.Range(0.2, 0.9), 0.5);
                if (victimDown) ApplyInfraction(attacker, InfractionType.IrresponsibleRiding);
            }
        }

        // ------------------------------------------------------------------ segments & incidents

        private void OnEnterSegment(RiderRaceState r, TrackSegment seg, int orderIndex)
        {
            if (seg.Index == Track.LongLapSegment && r.PendingLongLaps > 0 && r.Lap >= 0)
            {
                r.PendingLongLaps--;
                r.OnLongLap = true;
                double loss = Track.LongLapLossS > 0 ? Track.LongLapLossS : Rules.PitLane.LongLapLossS;
                Delay(r, loss, 0.45);
                AddEvent(RaceEventType.PenaltyServed, r.Index, -1, r.Entry.Rider.ShortName + " takes the Long Lap", true);
            }
            if (!seg.IsCorner || _noiseless) return;
            RollIncidents(r, seg, orderIndex);
        }

        private void RollIncidents(RiderRaceState r, TrackSegment seg, int orderIndex)
        {
            var a = r.Entry.Rider.Attributes;
            double wet = Weather.TrackWetness;
            double m = 1;
            if (r.Pace == PaceMode.Push) m *= Tuning.PushRisk;
            else if (r.Pace == PaceMode.Conserve) m *= Tuning.ConserveRisk;
            if (r.Mode == RiderMode.Attack) m *= Tuning.AttackRisk;
            m *= 0.7 + a.Aggression / 100.0 * 0.6;
            m *= 1.5 - a.RiskManagement / 100.0;
            m *= 1.3 - a.Consistency / 200.0;
            var fc = r.Front.Condition;
            var rc = r.Rear.Condition;
            bool cold = fc == TireCondition.Cold || rc == TireCondition.Cold;
            if (cold) m *= Tuning.ColdTireCrashMultiplier;
            if (fc == TireCondition.Overheated || rc == TireCondition.Overheated) m *= 1.6;
            m *= 1 + 3 * Math.Max(0, Math.Max(r.Front.Wear, r.Rear.Wear) - 0.6);
            bool slicks = !r.Front.Spec.IsWet || !r.Rear.Spec.IsWet;
            if (slicks && wet > 0.25) m *= 1 + (Tuning.WrongTireCrashMultiplier - 1) * MathX.Clamp01((wet - 0.25) / 0.6);
            m *= 1 + (Tuning.WetCrashMultiplier - 1) * wet * (1.4 - a.WetSkill / 100.0);
            if (r.Lap <= 0 && !Config.TimeTrial) m *= 1.5;
            if (r.AttemptSucceeds) m *= 1.4;
            m *= 1 + r.HandlingLoss * 10;
            double severity = 0.4 + seg.BrakingIntensity * 0.6 + seg.LeanRequirement * 0.5;

            double crashP = Tuning.BaseCrashChance * m * severity;
            if (_rng.Chance(crashP))
            {
                Crash(r, PickCrashType(r, seg, wet, cold), null);
                return;
            }

            double errP = Tuning.BaseErrorChance * Math.Pow(m, 0.6) * (1.5 - a.Consistency / 100.0) * severity;
            bool pressure = false;
            if (!Config.TimeTrial)
            {
                var behind = FindBehind(orderIndex);
                pressure = behind != null && r.Progress(Track.Length) - behind.Progress(Track.Length) < 15;
                if (pressure) errP *= 1.25 * (1.3 - a.Mentality / 100.0);
            }
            if (_rng.Chance(errP))
            {
                RiderError(r, seg, pressure);
                return;
            }

            // Track limits on monitored corners even without a big mistake.
            if (seg.TrackLimits)
            {
                double tl = 0.0012 * (r.Pace == PaceMode.Push ? 2.5 : r.Pace == PaceMode.Conserve ? 0.4 : 1) * (0.6 + a.Aggression / 100.0);
                if (_rng.Chance(tl)) TrackLimits(r);
            }
        }

        private CrashType PickCrashType(RiderRaceState r, TrackSegment seg, double wet, bool cold)
        {
            double highside = (r.Rear.Condition == TireCondition.Cold ? 3 : 1) * (1.3 - r.Entry.Rider.Attributes.ThrottleControl / 100.0) * (1 + r.Rear.Wear) * (1 - wet * 0.6);
            double front = 1.5 + seg.BrakingIntensity * 2 + wet * 2;
            double low = 2 + wet * 2;
            double rear = 0.8 + r.Rear.Wear * 2;
            double runoff = 0.6 + seg.BrakingIntensity;
            var types = new[] { CrashType.HighSide, CrashType.FrontEndLoss, CrashType.LowSide, CrashType.RearEndLoss, CrashType.RunOff };
            var weights = new[] { highside, front, low, rear, runoff };
            double total = 0;
            foreach (var w in weights) total += w;
            double pick = _rng.NextDouble() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                pick -= weights[i];
                if (pick <= 0) return types[i];
            }
            return CrashType.LowSide;
        }

        private void RiderError(RiderRaceState r, TrackSegment seg, bool pressure)
        {
            RiderErrorType type;
            double loss;
            if (pressure && _rng.Chance(0.35))
            {
                type = RiderErrorType.PressureMistake;
                loss = _rng.Range(0.3, 1.0);
            }
            else if (seg.BrakingIntensity > 0.45 && _rng.Chance(0.6))
            {
                type = _rng.Chance(0.5) ? RiderErrorType.LockUp : RiderErrorType.OutBraked;
                loss = type == RiderErrorType.LockUp ? _rng.Range(0.2, 0.7) : _rng.Range(0.6, 1.8);
            }
            else
            {
                double pick = _rng.NextDouble();
                if (pick < 0.35) { type = RiderErrorType.RunWide; loss = _rng.Range(0.4, 1.3); }
                else if (pick < 0.6) { type = RiderErrorType.LostLine; loss = _rng.Range(0.15, 0.5); }
                else if (pick < 0.85) { type = RiderErrorType.Wheelspin; loss = _rng.Range(0.15, 0.45); }
                else { type = RiderErrorType.LostConcentration; loss = _rng.Range(0.2, 0.6); }
            }

            // A big mistake can become a trip through the gravel.
            if ((type == RiderErrorType.OutBraked || type == RiderErrorType.RunWide) && _rng.Chance(0.12))
            {
                double gravel = _rng.Range(2.5, 7.0);
                Delay(r, gravel, 0.65);
                r.TargetLateral = -1.4 * Math.Sign(seg.AngleDeg == 0 ? 1 : seg.AngleDeg);
                AddEvent(RaceEventType.RunOff, r.Index, -1, r.Entry.Rider.ShortName + " runs off at turn " + seg.CornerNumber + " (" + gravel.ToString("0.0") + "s lost)", true);
                return;
            }
            Delay(r, loss, 0.5);
            if (type == RiderErrorType.RunWide || type == RiderErrorType.OutBraked)
                r.TargetLateral = -0.6 * Math.Sign(seg.AngleDeg == 0 ? 1 : seg.AngleDeg);
            AddEvent(RaceEventType.Error, r.Index, -1, r.Entry.Rider.ShortName + ": " + ErrorText(type) + " at turn " + seg.CornerNumber, loss > 0.6);
            if (seg.TrackLimits && (type == RiderErrorType.RunWide || type == RiderErrorType.OutBraked)) TrackLimits(r);
        }

        private static string ErrorText(RiderErrorType t)
        {
            switch (t)
            {
                case RiderErrorType.LockUp: return "locks the front";
                case RiderErrorType.OutBraked: return "outbrakes himself";
                case RiderErrorType.RunWide: return "runs wide";
                case RiderErrorType.LostLine: return "misses the line";
                case RiderErrorType.Wheelspin: return "big wheelspin on exit";
                case RiderErrorType.LostConcentration: return "loses concentration";
                case RiderErrorType.PressureMistake: return "makes a mistake under pressure";
            }
            return t.ToString();
        }

        private void Delay(RiderRaceState r, double seconds, double rate)
        {
            r.DelayRemaining += seconds;
            r.DelayRate = Math.Max(r.DelayRate, rate);
        }

        private void Crash(RiderRaceState r, CrashType type, string cause)
        {
            if (r.Status != RaceStatus.Racing) return;
            var seg = Track.Segments[r.SegmentIndex];
            if (type == CrashType.RunOff)
            {
                double loss = _rng.Range(3, 9);
                Delay(r, loss, 0.7);
                r.TargetLateral = -1.5 * Math.Sign(seg.AngleDeg == 0 ? 1 : seg.AngleDeg);
                AddEvent(RaceEventType.RunOff, r.Index, -1, r.Entry.Rider.ShortName + " goes straight on at turn " + seg.CornerNumber + " and rejoins", true);
                return;
            }
            r.Status = Config.TimeTrial ? RaceStatus.Retired : RaceStatus.Crashed;
            r.CrashTime = Time;
            r.LastCrash = type;
            r.Crashes++;
            r.AttemptTarget = -1;
            r.AttemptSucceeds = false;
            r.PitRequest = PitRequestType.None;
            double outward = -Math.Sign(seg.AngleDeg == 0 ? (r.LateralOffset >= 0 ? -1 : 1) : seg.AngleDeg);
            r.TargetLateral = 1.8 * outward;
            _yellowUntil[seg.Index] = Time + 25;
            if (_rng.Chance(type == CrashType.HighSide ? 0.16 : 0.035)) r.InjuredInCrash = true;

            string where = seg.IsCorner ? "turn " + seg.CornerNumber : "the straight";
            AddEvent(RaceEventType.Crash, r.Index, -1, r.Entry.Rider.ShortName + " crashes at " + where + " (" + type + (cause != null ? ", " + cause : "") + ")", true);
            AddEvent(RaceEventType.YellowFlag, r.Index, -1, "Yellow flag at " + where, false);

            double remount;
            switch (type)
            {
                case CrashType.LowSide: remount = 0.55; break;
                case CrashType.FrontEndLoss: remount = 0.5; break;
                case CrashType.RearEndLoss: remount = 0.35; break;
                case CrashType.ContactCrash: remount = 0.3; break;
                default: remount = 0.08; break;
            }
            if (r.InjuredInCrash) remount *= 0.2;
            remount *= 0.6 + r.Entry.Rider.Attributes.Mentality / 250.0;
            if (!Config.TimeTrial && Rules.AllowsRemount && !ChequeredFlag && _rng.Chance(remount))
            {
                r.RemountAt = Time + _rng.Range(15, 45);
            }
            else
            {
                r.RemountAt = -1;
                r.RetireReason = "Crash (" + type + ")";
            }
        }

        private void UpdateCrashed(RiderRaceState r, double dt)
        {
            r.Speed = Math.Max(0, r.Speed - dt * 14);
            double move = r.Speed * dt;
            r.Distance += move;
            if (r.Distance >= Track.Length)
            {
                r.Distance -= Track.Length;
                r.Lap++;
            }
            r.SegmentIndex = Track.SegmentIndexAt(r.Distance);
            r.LateralOffset = MathX.MoveTowards(r.LateralOffset, r.TargetLateral, dt * 1.2);
            r.LeanDeg = MathX.MoveTowards(r.LeanDeg, 90 * Math.Sign(r.LeanDeg == 0 ? 1 : r.LeanDeg), dt * 300);
            if (r.RemountAt > 0 && Time >= r.RemountAt)
            {
                r.Status = RaceStatus.Racing;
                r.RemountAt = -1;
                r.Speed = 0;
                r.LeanDeg = 0;
                r.TargetLateral = 0;
                r.PowerLoss += _rng.Range(0.0, 0.02);
                r.HandlingLoss += _rng.Range(0.003, 0.02);
                r.Front.Temperature -= 15;
                r.Rear.Temperature -= 15;
                AddEvent(RaceEventType.Remount, r.Index, -1, r.Entry.Rider.ShortName + " picks the bike up and rejoins", true);
            }
            else if (r.RemountAt < 0 && Time - r.CrashTime > 4)
            {
                r.Status = RaceStatus.Retired;
                AddEvent(RaceEventType.Retirement, r.Index, -1, r.Entry.Rider.ShortName + " is out of the " + Config.SessionName.ToLowerInvariant(), true);
            }
        }

        // ------------------------------------------------------------------ penalties

        private void TrackLimits(RiderRaceState r)
        {
            if (Config.TimeTrial) return;
            bool lastLap = r.Lap == Config.Laps - 1;
            if (lastLap && Rules.Penalties.Get(InfractionType.LastLapTrackLimits) != null)
            {
                ApplyInfraction(r, InfractionType.LastLapTrackLimits);
                return;
            }
            r.TrackLimitsCount++;
            int warnings = Math.Max(1, Rules.Penalties.TrackLimitsWarnings);
            if (r.TrackLimitsCount % warnings == 0)
                ApplyInfraction(r, InfractionType.TrackLimits);
            else
                AddEvent(RaceEventType.TrackLimitsWarning, r.Index, -1, r.Entry.Rider.ShortName + " track limits warning (" + r.TrackLimitsCount + ")", r.Entry.IsPlayer);
        }

        public void ApplyInfraction(RiderRaceState r, InfractionType infraction)
        {
            var def = Rules.Penalties.Get(infraction);
            if (def == null) return;
            var p = new PenaltyRecord { Type = def.Type, Infraction = infraction, TimeSeconds = def.TimeSeconds, Positions = def.Positions, IssuedAt = Time };
            r.Penalties.Add(p);
            switch (def.Type)
            {
                case PenaltyType.LongLap: r.PendingLongLaps += 1; break;
                case PenaltyType.DoubleLongLap: r.PendingLongLaps += 2; break;
                case PenaltyType.RideThrough: r.PendingRideThrough = true; break;
                case PenaltyType.TimePenalty: r.TimePenalty += def.TimeSeconds; break;
                case PenaltyType.PositionDrop: r.PositionDrop += Math.Max(1, def.Positions); break;
                case PenaltyType.Disqualification:
                    r.Status = RaceStatus.Disqualified;
                    r.RetireReason = "Disqualified (" + infraction + ")";
                    break;
            }
            AddEvent(RaceEventType.Penalty, r.Index, -1, r.Entry.Rider.ShortName + ": " + Describe(def) + " for " + infraction, true);
        }

        private static string Describe(PenaltyDefinition def)
        {
            switch (def.Type)
            {
                case PenaltyType.TimePenalty: return def.TimeSeconds.ToString("0") + "s time penalty";
                case PenaltyType.PositionDrop: return "drop " + Math.Max(1, def.Positions) + " position(s)";
                case PenaltyType.DoubleLongLap: return "Double Long Lap";
                case PenaltyType.LongLap: return "Long Lap";
                case PenaltyType.RideThrough: return "Ride Through";
            }
            return def.Type.ToString();
        }

        // ------------------------------------------------------------------ pit lane

        private void EnterPitLane(RiderRaceState r)
        {
            r.Status = RaceStatus.InPitLane;
            r.PitProgress = 0;
            r.PitServiceDone = false;
            r.AttemptTarget = -1;
            r.AttemptSucceeds = false;
            if (r.PitRequest == PitRequestType.BikeSwap)
                r.PitStationaryTime = Rules.PitLane.BikeSwapTimeS + _rng.Range(0, 2);
            else if (r.PitRequest == PitRequestType.TireChange)
                r.PitStationaryTime = Rules.PitLane.TireChangeTimeS + _rng.Range(-2, 5);
            else
                r.PitStationaryTime = 0;
            AddEvent(RaceEventType.PitIn, r.Index, -1, r.Entry.Rider.ShortName + " into the pit lane" + (r.PitRequest == PitRequestType.BikeSwap ? " to swap bikes" : r.PitRequest == PitRequestType.TireChange ? " for tires" : " (ride through)"), true);
        }

        private void UpdatePitLane(RiderRaceState r, double dt)
        {
            double limit = Rules.PitLane.SpeedLimitKph / 3.6;
            double mid = Track.PitLaneLength * 0.5;
            bool needsService = !r.PitServiceDone && r.PitStationaryTime > 0;
            if (needsService && r.PitProgress >= mid)
            {
                r.Speed = 0;
                r.PitStationaryTime -= dt;
                if (r.PitStationaryTime <= 0) ServicePit(r);
            }
            else
            {
                r.Speed = MathX.MoveTowards(r.Speed, limit, dt * 9);
                r.PitProgress += r.Speed * dt;
            }
            r.LeanDeg = MathX.MoveTowards(r.LeanDeg, 0, dt * 60);
            r.LateralOffset = MathX.MoveTowards(r.LateralOffset, 0, dt);

            // Map pit progress onto the main track so ordering and timing keep working.
            double mainSpan = (Track.Length - Track.PitEntry) + Track.PitExit;
            double t = MathX.Clamp01(r.PitProgress / Track.PitLaneLength);
            double mapped = Track.PitEntry + t * mainSpan;
            int lapBefore = r.Lap;
            if (mapped >= Track.Length)
            {
                if (r.Distance >= Track.PitEntry - 1)
                {
                    r.Lap = lapBefore + 1;
                    r.Distance = mapped - Track.Length;
                    OnCrossLine(r);
                    if (r.Status == RaceStatus.Finished) return;
                }
                else
                {
                    r.Distance = mapped - Track.Length;
                }
            }
            else
            {
                r.Distance = mapped;
            }
            r.SegmentIndex = Track.SegmentIndexAt(r.Distance);
            UpdateCheckpoints(r);

            if (r.PitProgress >= Track.PitLaneLength)
            {
                r.Status = RaceStatus.Racing;
                r.Distance = Track.PitExit;
                r.SegmentIndex = Track.SegmentIndexAt(r.Distance);
                r.Speed = limit;
                r.PitRequest = PitRequestType.None;
                if (r.PendingRideThrough && r.PitStationaryTime <= 0)
                {
                    r.PendingRideThrough = false;
                    AddEvent(RaceEventType.PenaltyServed, r.Index, -1, r.Entry.Rider.ShortName + " serves the ride through", true);
                }
                AddEvent(RaceEventType.PitOut, r.Index, -1, r.Entry.Rider.ShortName + " leaves the pit lane on " + r.Rear.Compound, true);
                if (_rng.Chance(0.004)) ApplyInfraction(r, InfractionType.PitLaneSpeeding);
                foreach (var o in Riders)
                {
                    if (o == r || o.Status != RaceStatus.Racing) continue;
                    double d = o.Distance - r.Distance;
                    if (Math.Abs(d) < 15 && _rng.Chance(0.25))
                    {
                        ApplyInfraction(r, InfractionType.UnsafeRelease);
                        break;
                    }
                }
            }
        }

        private void ServicePit(RiderRaceState r)
        {
            r.PitServiceDone = true;
            var spec = CompoundFor(r.PitCompound);
            double warm = Math.Max(Rules.Tires.WarmerTemperature, Weather.TrackTemp);
            TireModel.Fit(r.Front, spec, warm);
            TireModel.Fit(r.Rear, spec, warm);
            if (r.PitRequest == PitRequestType.BikeSwap)
            {
                r.SpareBikeUsed = true;
                r.HandlingLoss = 0;
                r.PowerLoss = Math.Min(r.PowerLoss, 0.01);
            }
            if (r.PendingRideThrough) r.PendingRideThrough = false;
        }

        // ------------------------------------------------------------------ machinery

        private void UpdateMachinery(RiderRaceState r, TrackSegment seg, double move, double dt)
        {
            var a = r.Entry.Rider.Attributes;
            var bike = r.Entry.Bike;
            double fs, rs;
            TireModel.PhaseStress(r.Phase, seg, out fs, out rs);
            double riderWear = 1 + (50 - a.TireManagement) / 150.0 + (a.Aggression - 50) / 300.0;
            if (r.Entry.Rider.HasStyle(RidingStyle.TireSaver)) riderWear -= 0.08;
            if (r.Entry.Rider.HasStyle(RidingStyle.AggressiveRider)) riderWear += 0.06;
            if (r.Entry.Rider.HasStyle(RidingStyle.SmoothRider)) riderWear -= 0.04;
            if (r.Pace == PaceMode.Push) riderWear *= Tuning.PushWear;
            else if (r.Pace == PaceMode.Conserve) riderWear *= Tuning.ConserveWear;
            double bikeWear = 1 + (50 - bike[PerfAttr.TireWear]) / 200.0;
            double paceHeat = r.Pace == PaceMode.Push ? 1.06 : r.Pace == PaceMode.Conserve ? 0.94 : 1.0;
            double wet = Weather.TrackWetness;
            double scale = Rules.Tires.WearScale;
            TireModel.Update(r.Front, fs, move, dt, r.Speed, Weather.TrackTemp, wet, Config.Circuit.Abrasiveness, riderWear, bikeWear, paceHeat, scale, Tuning);
            TireModel.Update(r.Rear, rs, move, dt, r.Speed, Weather.TrackTemp, wet, Config.Circuit.Abrasiveness, riderWear, bikeWear, paceHeat, scale, Tuning);

            if (Config.FuelLimitLitres > 0)
            {
                double throttle = r.Phase == RidingPhase.Straight ? 1.35 : r.Phase == RidingPhase.CornerExit ? 1.1 : r.Phase == RidingPhase.Braking ? 0.15 : 0.6;
                double pace = r.Pace == PaceMode.Push ? 1.05 : r.Pace == PaceMode.Conserve ? 0.92 : 1.0;
                double eff = 1.04 - 0.08 * bike[PerfAttr.FuelEfficiency] / 100.0;
                r.Fuel -= Rules.Fuel.ConsumptionPerKm / 1000.0 * move * throttle * pace * eff;
                if (r.Fuel < 0) r.Fuel = 0;
            }

            if (!Config.TimeTrial)
            {
                double rel = bike[PerfAttr.Reliability];
                double hazard = Tuning.BaseFailureRatePerHour / 3600.0 * Math.Max(0.15, 1 + (50 - rel) / 25.0) * (r.Pace == PaceMode.Push ? 1.3 : 1.0) * dt;
                if (_rng.Chance(hazard)) MechanicalProblem(r);
            }
        }

        private void MechanicalProblem(RiderRaceState r)
        {
            var types = new[] { FailureType.Engine, FailureType.Electronics, FailureType.Brakes, FailureType.Transmission, FailureType.Cooling, FailureType.Suspension };
            var weights = new[] { 0.28, 0.25, 0.08, 0.17, 0.1, 0.12 };
            double pick = _rng.NextDouble();
            FailureType type = types[0];
            for (int i = 0; i < weights.Length; i++)
            {
                pick -= weights[i];
                if (pick <= 0) { type = types[i]; break; }
            }
            bool terminal = _rng.Chance(type == FailureType.Engine || type == FailureType.Transmission ? 0.8 : 0.45);
            if (terminal)
            {
                r.Status = RaceStatus.Retired;
                r.RetireReason = type + " failure";
                r.TargetLateral = 1.6;
                AddEvent(RaceEventType.MechanicalFailure, r.Index, -1, r.Entry.Rider.ShortName + " retires: " + type.ToString().ToLowerInvariant() + " failure", true);
            }
            else
            {
                r.HasIssue = true;
                if (type == FailureType.Engine || type == FailureType.Electronics || type == FailureType.Cooling) r.PowerLoss += _rng.Range(0.02, 0.07);
                else r.HandlingLoss += _rng.Range(0.005, 0.02);
                AddEvent(RaceEventType.MechanicalIssue, r.Index, -1, r.Entry.Rider.ShortName + " has a " + type.ToString().ToLowerInvariant() + " problem and loses pace", true);
            }
        }

        // ------------------------------------------------------------------ timing

        private void OnCrossLine(RiderRaceState r)
        {
            if (r.Lap == -1 && !Config.TimeTrial)
            {
                // Crossing the line for the first time after the start (grid is behind the line).
                r.Lap = 0;
                r.SectorStartTime = Time;
                r.CurrentSector = 0;
                return;
            }
            if (r.Status != RaceStatus.InPitLane) r.Lap++;
            bool valid = !double.IsNaN(r.LapStartTime);
            if (Config.TimeTrial && r.Lap == 0)
            {
                // End of the out lap.
                r.LapStartTime = Time;
                r.SectorStartTime = Time;
                r.CurrentSector = 0;
                NewLapNoise(r);
                return;
            }
            if (valid)
            {
                double lapTime = Time - r.LapStartTime;
                r.LastLapTime = lapTime;
                if (lapTime < r.BestLapTime) r.BestLapTime = lapTime;
                if (lapTime < FastestLap && r.Lap > 1 || (Config.TimeTrial && lapTime < FastestLap))
                {
                    FastestLap = lapTime;
                    FastestLapRider = r.Index;
                    if (!Config.TimeTrial)
                        AddEvent(RaceEventType.FastestLap, r.Index, -1, "Fastest lap: " + r.Entry.Rider.ShortName + " " + MathX.FormatLapTime(lapTime), false);
                }
            }
            r.LapStartTime = Time;
            r.SectorStartTime = Time;
            r.CurrentSector = 0;
            NewLapNoise(r);

            if (Config.TimeTrial)
            {
                if (r.Lap >= Config.Laps)
                {
                    r.Status = RaceStatus.Finished;
                    r.FinishTime = Time;
                }
                return;
            }

            if (r.Lap > LeaderLap) LeaderLap = r.Lap;
            if (ChequeredFlag || r.Lap >= Config.Laps)
            {
                if (!ChequeredFlag)
                {
                    ChequeredFlag = true;
                    AddEvent(RaceEventType.Finish, r.Index, -1, "Chequered flag! " + r.Entry.Rider.ShortName + " wins the " + Config.SessionName.ToLowerInvariant(), true);
                }
                r.Status = RaceStatus.Finished;
                r.FinishTime = Time;
                r.TargetLateral = 0;
                return;
            }
            if (!_lastLapAnnounced && r.Lap == Config.Laps - 1)
            {
                _lastLapAnnounced = true;
                AddEvent(RaceEventType.LastLap, r.Index, -1, "Last lap!", true);
            }
            if (r.PendingLongLaps > 0 && r.Lap >= Config.Laps - 1)
            {
                // Not served in time: converted to a time penalty.
                double loss = (Track.LongLapLossS > 0 ? Track.LongLapLossS : Rules.PitLane.LongLapLossS) * 1.3;
                r.TimePenalty += loss * r.PendingLongLaps;
                r.PendingLongLaps = 0;
            }
            _ai.OnLap(r);
        }

        private void NewLapNoise(RiderRaceState r)
        {
            if (_noiseless)
            {
                r.LapNoise = 1;
                return;
            }
            double c = r.Entry.Rider.Attributes.Consistency;
            r.LapNoise = 1 + _rng.Gaussian(0, Tuning.ConsistencyNoise * (1.6 - c / 100.0));
            r.LapNoise = MathX.Clamp(r.LapNoise, 0.985, 1.008);
        }

        private void UpdateCheckpoints(RiderRaceState r)
        {
            double progress = r.Progress(Track.Length);
            if (progress < 0) return;
            int g = (int)(progress / _cpSpacing);
            int max = _firstPassage.Length - 1;
            if (g > max) g = max;
            while (r.CheckpointIndex < g)
            {
                r.CheckpointIndex++;
                r.CheckpointTime = Time;
                _passage[r.Index][r.CheckpointIndex] = Time;
                if (double.IsNaN(_firstPassage[r.CheckpointIndex])) _firstPassage[r.CheckpointIndex] = Time;
            }
            int sector = Track.SectorAt(r.Distance);
            if (sector != r.CurrentSector && sector == r.CurrentSector + 1)
            {
                r.SectorTimes[r.CurrentSector] = Time - r.SectorStartTime;
                r.SectorStartTime = Time;
                r.CurrentSector = sector;
            }
            else if (sector == 0 && r.CurrentSector == Track.SectorCount - 1)
            {
                r.SectorTimes[r.CurrentSector] = Time - r.SectorStartTime;
            }
        }

        private void UpdateTiming()
        {
            SortOrder();
            int pos = 1;
            RiderRaceState prev = null;
            double L = Track.Length;
            for (int k = 0; k < _order.Length; k++)
            {
                var r = Riders[_order[k]];
                r.Position = pos++;
                if (r.CheckpointIndex >= 0 && !double.IsNaN(_firstPassage[r.CheckpointIndex]))
                    r.GapToLeader = r.Status == RaceStatus.Finished && k > 0 && Riders[_order[0]].Status == RaceStatus.Finished
                        ? r.FinishTime - Riders[_order[0]].FinishTime
                        : r.CheckpointTime - _firstPassage[r.CheckpointIndex];
                else
                    r.GapToLeader = 0;
                if (prev != null && r.CheckpointIndex >= 0 && prev.CheckpointIndex >= r.CheckpointIndex)
                    r.GapToAhead = r.CheckpointTime - _passage[prev.Index][r.CheckpointIndex];
                else
                    r.GapToAhead = 0;
                var leader = Riders[_order[0]];
                r.LapsDown = (int)Math.Floor((leader.Progress(L) - r.Progress(L)) / L);
                if (r.LapsDown < 0) r.LapsDown = 0;
                prev = r;
            }
            if (!Config.TimeTrial && _order.Length > 0)
            {
                int leaderIdx = _order[0];
                if (leaderIdx != _leaderIndex && Time > 1)
                {
                    if (_leaderIndex >= 0 && !ChequeredFlag)
                        AddEvent(RaceEventType.LeaderChange, leaderIdx, _leaderIndex, Riders[leaderIdx].Entry.Rider.ShortName + " takes the lead", true);
                    _leaderIndex = leaderIdx;
                }
            }
            // Overtake detection for the log (position swaps between running riders).
            for (int k = 1; k < _order.Length; k++)
            {
                var r = Riders[_order[k - 1]];
                if (r.AttemptTarget >= 0 && r.AttemptSucceeds)
                {
                    var victim = Riders[r.AttemptTarget];
                    if (r.Progress(L) > victim.Progress(L) + 1)
                    {
                        r.Overtakes++;
                        AddEvent(RaceEventType.Overtake, r.Index, victim.Index, r.Entry.Rider.ShortName + " passes " + victim.Entry.Rider.ShortName + " for P" + r.Position, r.Position <= 5 || r.Entry.IsPlayer || victim.Entry.IsPlayer);
                        r.AttemptTarget = -1;
                        r.AttemptSucceeds = false;
                        r.StuckTime = 0;
                        r.TargetLateral = 0;
                        victim.TargetLateral = 0;
                    }
                }
            }
        }

        private void CheckFinish()
        {
            if (Time > Config.MaxDurationS)
            {
                IsFinished = true;
                return;
            }
            if (Config.TimeTrial)
            {
                foreach (var r in Riders)
                    if (r.Status == RaceStatus.Racing) return;
                IsFinished = true;
                return;
            }
            if (!ChequeredFlag) return;
            foreach (var r in Riders)
            {
                if (r.Status == RaceStatus.Racing || r.Status == RaceStatus.InPitLane || r.Status == RaceStatus.Grid) return;
                if (r.Status == RaceStatus.Crashed && r.RemountAt > 0) return;
            }
            // Riders still lying in the gravel are retirements.
            foreach (var r in Riders)
                if (r.Status == RaceStatus.Crashed) r.Status = RaceStatus.Retired;
            IsFinished = true;
        }

        public void AddEvent(RaceEventType type, int rider, int other, string detail, bool important)
        {
            int lap = rider >= 0 && rider < Riders.Count ? Math.Max(0, Riders[rider].Lap + 1) : LeaderLap + 1;
            Events.Add(new RaceEvent { Time = Time, Lap = lap, Type = type, RiderIndex = rider, OtherRiderIndex = other, Detail = detail, Important = important });
        }

        // ------------------------------------------------------------------ results

        /// <summary>Final classification (points are assigned by the weekend layer).</summary>
        public List<ResultEntry> GetClassification()
        {
            double L = Track.Length;
            var finished = new List<RiderRaceState>();
            var others = new List<RiderRaceState>();
            var dsq = new List<RiderRaceState>();
            foreach (var r in Riders)
            {
                if (r.Status == RaceStatus.Finished) finished.Add(r);
                else if (r.Status == RaceStatus.Disqualified) dsq.Add(r);
                else others.Add(r);
            }
            finished.Sort((a, b) =>
            {
                int c = b.Lap.CompareTo(a.Lap);
                if (c != 0) return c;
                return (a.FinishTime + a.TimePenalty).CompareTo(b.FinishTime + b.TimePenalty);
            });
            // Position drops (e.g. last lap track limits).
            for (int i = finished.Count - 1; i >= 0; i--)
            {
                var r = finished[i];
                if (r.PositionDrop <= 0) continue;
                int target = Math.Min(finished.Count - 1, i + r.PositionDrop);
                finished.RemoveAt(i);
                finished.Insert(target, r);
            }
            others.Sort((a, b) => b.Progress(L).CompareTo(a.Progress(L)));

            var list = new List<ResultEntry>();
            double winnerTime = finished.Count > 0 ? finished[0].FinishTime + finished[0].TimePenalty : 0;
            int pos = 1;
            foreach (var r in finished)
            {
                list.Add(new ResultEntry
                {
                    RiderId = r.Entry.Rider.Id,
                    TeamId = r.Entry.Team != null ? r.Entry.Team.Id : null,
                    ManufacturerId = r.Entry.ManufacturerId,
                    Position = pos++,
                    GridPosition = r.Entry.GridPosition,
                    Status = ResultStatus.Finished,
                    TotalTime = r.FinishTime + r.TimePenalty,
                    GapToWinner = r.FinishTime + r.TimePenalty - winnerTime,
                    Laps = Math.Min(r.Lap, Config.Laps),
                    BestLap = r.BestLapTime < double.MaxValue ? r.BestLapTime : 0,
                    FastestLap = r.Index == FastestLapRider,
                    Note = r.TimePenalty > 0 ? "+" + r.TimePenalty.ToString("0.0") + "s penalty" : null
                });
            }
            foreach (var r in others)
            {
                list.Add(new ResultEntry
                {
                    RiderId = r.Entry.Rider.Id,
                    TeamId = r.Entry.Team != null ? r.Entry.Team.Id : null,
                    ManufacturerId = r.Entry.ManufacturerId,
                    Position = pos++,
                    GridPosition = r.Entry.GridPosition,
                    Status = ResultStatus.DNF,
                    Laps = Math.Max(0, r.Lap),
                    BestLap = r.BestLapTime < double.MaxValue ? r.BestLapTime : 0,
                    Note = r.RetireReason
                });
            }
            foreach (var r in dsq)
            {
                list.Add(new ResultEntry
                {
                    RiderId = r.Entry.Rider.Id,
                    TeamId = r.Entry.Team != null ? r.Entry.Team.Id : null,
                    ManufacturerId = r.Entry.ManufacturerId,
                    Position = pos++,
                    GridPosition = r.Entry.GridPosition,
                    Status = ResultStatus.DSQ,
                    Laps = Math.Max(0, r.Lap),
                    Note = r.RetireReason
                });
            }
            return list;
        }

        public RiderRaceState RiderAtPosition(int position)
        {
            foreach (var r in Riders) if (r.Position == position) return r;
            return null;
        }

        public IReadOnlyList<int> Order { get { return _order; } }

        public double ForecastRain(double horizonS, double skill)
        {
            return Weather.ForecastRainProbability(horizonS, skill);
        }

        /// <summary>Projected fuel at the flag (litres) based on consumption so far. NaN if not applicable.</summary>
        public double ProjectedFuelAtFinish(RiderRaceState r)
        {
            if (Config.FuelLimitLitres <= 0 || r.Lap < 1) return double.NaN;
            double used = Config.FuelLimitLitres - r.Fuel;
            double done = r.Progress(Track.Length);
            if (done <= 0) return double.NaN;
            double perM = used / done;
            return r.Fuel - perM * (Config.Laps * Track.Length - done);
        }
    }
}
