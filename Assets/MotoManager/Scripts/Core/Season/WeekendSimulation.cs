using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using MotoManager.Core.World;

namespace MotoManager.Core.Season
{
    public class TireChoice
    {
        public TireCompound Front;
        public TireCompound Rear;
    }

    /// <summary>
    /// One category at one event: runs the sessions defined by the category rules in order. Sessions can be
    /// simulated headless, or (for races) handed out as a <see cref="RaceSimulation"/> that the UI steps and
    /// renders before handing it back with <see cref="CompleteRace"/>.
    /// </summary>
    public class WeekendSimulation
    {
        public readonly WorldState World;
        public readonly GameDatabase Db;
        public readonly string CategoryId;
        public readonly CategoryRules Rules;
        public readonly CalendarRound Round;
        public readonly CircuitDefinition Circuit;
        public readonly WeekendResult Result;
        public readonly List<RaceEntry> Entries = new List<RaceEntry>();
        public readonly Dictionary<string, TireChoice> PlayerTireChoices = new Dictionary<string, TireChoice>();
        public readonly Dictionary<string, WeatherSetup> SessionWeather = new Dictionary<string, WeatherSetup>();
        public int SessionIndex { get; private set; }
        public bool IsComplete { get { return SessionIndex >= Rules.Weekend.Sessions.Count; } }
        public bool HeadlessTimeStep = true;
        /// <summary>Crashes / injuries / failures accumulated over the weekend (for the economy and the medical report).</summary>
        public readonly Dictionary<string, int> CrashesByRider = new Dictionary<string, int>();
        public readonly List<string> InjuredRiders = new List<string>();

        private readonly ulong _seed;
        private readonly DeterministicRandom _rng;
        private List<string> _practiceOrder = new List<string>();
        private List<string> _qualifyingOrder = new List<string>();
        private readonly Dictionary<string, List<ResultEntry>> _raceResults = new Dictionary<string, List<ResultEntry>>();
        private double _trackEvolution;

        public WeekendSimulation(WorldState world, GameDatabase db, string categoryId, CalendarRound round)
        {
            World = world;
            Db = db;
            CategoryId = categoryId;
            Rules = db.GetRules(categoryId, world.Season);
            Round = round;
            Circuit = db.GetCircuit(round.CircuitId);
            _seed = DeterministicRandom.Combine(world.MasterSeed, DeterministicRandom.Hash(world.Season + "|" + categoryId + "|" + round.Round));
            _rng = new DeterministicRandom(_seed);
            Result = new WeekendResult { Round = round.Round, EventName = round.Name, CircuitId = round.CircuitId, Date = round.Date };
            BuildEntries();
            foreach (var s in Rules.Weekend.Sessions)
                SessionWeather[s.Id] = WeatherModel.Generate(Circuit, round.Date, _rng.Fork("weather-" + s.Id), Math.Max(600, s.DurationMinutes * 60));
        }

        public SessionDefinition CurrentSession
        {
            get { return IsComplete ? null : Rules.Weekend.Sessions[SessionIndex]; }
        }

        public int LapsFor(SessionDefinition s)
        {
            int laps;
            if (Circuit.LapsOverride != null && Circuit.LapsOverride.TryGetValue(CategoryId, out laps) && s.Type == SessionType.Race) return laps;
            double km = Rules.RaceDistanceKm * s.DistanceFraction;
            return Math.Max(3, (int)Math.Round(km * 1000 / Circuit.LengthM));
        }

        private void BuildEntries()
        {
            foreach (var team in World.TeamsInCategory(CategoryId))
            {
                var bike = World.GetBike(team.BikeId);
                var bikeClass = Db.GetBikeClass(bike != null ? bike.BikeClassId : Rules.BikeClassId);
                var perf = bike != null ? BikePerformance.Compute(bike, bikeClass) : BikePerformance.Average();
                foreach (var riderId in team.RiderIds)
                {
                    var rider = World.GetRider(riderId);
                    if (rider == null || rider.IsRetired || rider.InjuryRoundsOut > 0) continue;
                    var entry = new RaceEntry
                    {
                        Rider = rider,
                        Team = team,
                        Bike = perf,
                        ManufacturerId = team.ManufacturerId,
                        IsPlayer = team.IsPlayer,
                        AutoManaged = !team.IsPlayer,
                        SetupQuality = InitialSetup(rider, team),
                        Form = WeekendForm(rider)
                    };
                    Entries.Add(entry);
                }
            }
            Entries.Sort((a, b) => string.CompareOrdinal(a.Rider.Id, b.Rider.Id));
        }

        /// <summary>
        /// Riders are not machines: weekend form varies, some tracks suit a rider better than others (stable per
        /// rider/circuit pair) and morale matters. Keeps dominant riders beatable.
        /// </summary>
        private double WeekendForm(Rider rider)
        {
            ulong h = DeterministicRandom.Combine(DeterministicRandom.Hash(rider.Id), DeterministicRandom.Hash(Circuit.Id));
            double affinity = ((h >> 11) * (1.0 / 9007199254740992.0) * 2 - 1) * 0.004;
            double consistency = rider.Attributes.Consistency;
            double form = _rng.Gaussian(0, 0.004 * (1.5 - consistency / 100.0));
            double morale = (rider.Morale - 60) / 100.0 * 0.003;
            return 1 + MathX.Clamp(affinity + form + morale, -0.015, 0.015);
        }

        private double InitialSetup(Rider rider, Team team)
        {
            double crew = 50;
            foreach (var sid in team.StaffIds)
            {
                StaffMember s;
                if (World.Staff.TryGetValue(sid, out s) && s.Role == StaffRole.CrewChief) crew = Math.Max(crew, s.Skill);
            }
            double known = 0;
            foreach (var rec in rider.CareerHistory) if (rec.CategoryId == CategoryId) known += 0.05;
            return MathX.Clamp01(0.2 + rider.Attributes.TechnicalFeedback / 400.0 + crew / 500.0 + team.Facility(FacilityType.Simulator) * 0.03 + Math.Min(0.15, known));
        }

        // ------------------------------------------------------------------ session flow

        public bool CurrentIsRace
        {
            get
            {
                var s = CurrentSession;
                return s != null && (s.Type == SessionType.Race || s.Type == SessionType.Sprint);
            }
        }

        /// <summary>Runs the current session headless and moves to the next one.</summary>
        public SessionResult RunCurrentHeadless()
        {
            var s = CurrentSession;
            if (s == null) return null;
            if (s.Type == SessionType.Race || s.Type == SessionType.Sprint)
            {
                var sim = CreateRaceSimulation(HeadlessTimeStep ? Db.Tuning.BackgroundTimeStep : Db.Tuning.VisualTimeStep);
                sim.RunToEnd();
                return CompleteRace(sim);
            }
            SessionResult res;
            switch (s.Type)
            {
                case SessionType.Qualifying:
                    res = RunQualifying(s);
                    break;
                case SessionType.WarmUp:
                    res = RunWarmUp(s);
                    break;
                default:
                    res = RunPractice(s);
                    break;
            }
            Result.Sessions.Add(res);
            SessionIndex++;
            return res;
        }

        public void RunToEnd()
        {
            while (!IsComplete) RunCurrentHeadless();
        }

        private SessionResult RunPractice(SessionDefinition s)
        {
            var weather = SessionWeather[s.Id];
            var runs = QualifyingSimulator.RunSession(Entries, Rules, Circuit, Db.Tuning, weather, _rng.NextULong(), SessionType.Practice, 1, 2, _trackEvolution, 0.1);
            _trackEvolution += 0.002;
            // Practice improves the set-up: better feedback and crew chiefs converge faster.
            foreach (var e in Entries)
            {
                double gain = 0.12 + e.Rider.Attributes.TechnicalFeedback / 600.0 + e.Rider.Attributes.Adaptability / 1000.0;
                e.SetupQuality = MathX.Clamp01(e.SetupQuality + gain * (1 - e.SetupQuality));
            }
            _practiceOrder = new List<string>();
            foreach (var r in runs) _practiceOrder.Add(r.Entry.Rider.Id);
            return ToSessionResult(s, runs, weather);
        }

        private SessionResult RunWarmUp(SessionDefinition s)
        {
            foreach (var e in Entries) e.SetupQuality = MathX.Clamp01(e.SetupQuality + 0.04 * (1 - e.SetupQuality));
            return new SessionResult { SessionId = s.Id, SessionName = s.Name, Type = s.Type, WeatherSummary = "-" };
        }

        private SessionResult RunQualifying(SessionDefinition s)
        {
            var weather = SessionWeather[s.Id];
            var q = Rules.Qualifying;
            List<LapRun> final;
            if (q.Format == QualifyingFormat.Knockout && Entries.Count > q.DirectToFinal + q.AdvanceFromFirst)
            {
                var direct = new List<RaceEntry>();
                var first = new List<RaceEntry>();
                var practice = _practiceOrder.Count > 0 ? _practiceOrder : null;
                var ordered = new List<RaceEntry>(Entries);
                if (practice != null) ordered.Sort((a, b) => practice.IndexOf(a.Rider.Id).CompareTo(practice.IndexOf(b.Rider.Id)));
                for (int i = 0; i < ordered.Count; i++)
                    (i < q.DirectToFinal ? direct : first).Add(ordered[i]);
                var q1 = QualifyingSimulator.RunSession(first, Rules, Circuit, Db.Tuning, weather, _rng.NextULong(), SessionType.Qualifying, q.Runs, q.FlyingLapsPerRun, _trackEvolution, 0.1);
                var finalists = new List<RaceEntry>(direct);
                for (int i = 0; i < q.AdvanceFromFirst && i < q1.Count; i++) finalists.Add(q1[i].Entry);
                var q2 = QualifyingSimulator.RunSession(finalists, Rules, Circuit, Db.Tuning, weather, _rng.NextULong(), SessionType.Qualifying, q.Runs, q.FlyingLapsPerRun, _trackEvolution + 0.002, 0.1);
                final = new List<LapRun>(q2);
                for (int i = q.AdvanceFromFirst; i < q1.Count; i++) final.Add(q1[i]);
            }
            else
            {
                final = QualifyingSimulator.RunSession(Entries, Rules, Circuit, Db.Tuning, weather, _rng.NextULong(), SessionType.Qualifying, q.Runs, q.FlyingLapsPerRun, _trackEvolution, 0.1);
            }
            _trackEvolution += 0.003;
            _qualifyingOrder = new List<string>();
            foreach (var r in final) _qualifyingOrder.Add(r.Entry.Rider.Id);
            var res = ToSessionResult(s, final, weather);
            if (final.Count > 0 && final[0].BestLap < double.MaxValue)
            {
                var poleRider = final[0].Entry.Rider;
                var rec = poleRider.GetOrCreateRecord(World.Season, CategoryId, poleRider.TeamId);
                rec.Poles++;
                StandingEntry st;
                var champ = ChampionshipService.Get(World, CategoryId);
                if (!champ.Riders.TryGetValue(poleRider.Id, out st)) champ.Riders[poleRider.Id] = st = new StandingEntry { Id = poleRider.Id };
                st.Poles++;
            }
            return res;
        }

        private SessionResult ToSessionResult(SessionDefinition s, List<LapRun> runs, WeatherSetup weather)
        {
            var res = new SessionResult { SessionId = s.Id, SessionName = s.Name, Type = s.Type, WeatherSummary = weather.InitialWetness > 0.3 ? "Wet" : weather.RainEvents.Count > 0 ? "Mixed" : "Dry" };
            double best = runs.Count > 0 ? runs[0].BestLap : 0;
            for (int i = 0; i < runs.Count; i++)
            {
                var r = runs[i];
                bool time = r.BestLap < double.MaxValue;
                res.Classification.Add(new ResultEntry
                {
                    RiderId = r.Entry.Rider.Id,
                    TeamId = r.Entry.Team.Id,
                    ManufacturerId = r.Entry.ManufacturerId,
                    Position = i + 1,
                    Status = time ? ResultStatus.Finished : ResultStatus.DNS,
                    BestLap = time ? r.BestLap : 0,
                    GapToWinner = time ? r.BestLap - best : 0,
                    Laps = r.Laps,
                    Note = r.Note
                });
                if (r.Crashed) Count(CrashesByRider, r.Entry.Rider.Id);
            }
            return res;
        }

        /// <summary>Builds the simulation for the current race/sprint session (grid, tires, weather, fuel).</summary>
        public RaceSimulation CreateRaceSimulation(double timeStep)
        {
            var s = CurrentSession;
            if (s == null || (s.Type != SessionType.Race && s.Type != SessionType.Sprint))
                throw new InvalidOperationException("Current session is not a race");
            var weather = SessionWeather[s.Id];
            int laps = LapsFor(s);
            var cfg = new RaceConfig
            {
                Rules = Rules,
                Circuit = Circuit,
                Tuning = Db.Tuning,
                SessionType = s.Type,
                SessionName = s.Name,
                Laps = laps,
                Seed = DeterministicRandom.Combine(_seed, DeterministicRandom.Hash(s.Id)),
                TimeStep = timeStep,
                Weather = weather,
                TrackEvolution = _trackEvolution,
                FuelLimitLitres = s.Type == SessionType.Sprint && Rules.Fuel.SprintTankLimitLitres > 0 ? Rules.Fuel.SprintTankLimitLitres : Rules.Fuel.TankLimitLitres
            };
            var grid = GridOrder(s);
            var weatherModel = new WeatherModel(weather, Db.Tuning, cfg.Seed);
            for (int i = 0; i < grid.Count; i++)
            {
                var e = grid[i];
                e.GridPosition = i + 1;
                TireCompound front, rear;
                TireChoice choice;
                if (e.IsPlayer && PlayerTireChoices.TryGetValue(e.Rider.Id, out choice))
                {
                    front = choice.Front;
                    rear = choice.Rear;
                }
                else
                {
                    RaceAI.ChooseTires(Rules, weatherModel.TrackTemp, weather.InitialWetness, Circuit.Abrasiveness, laps, e.Rider.Attributes.TireManagement, out front, out rear);
                }
                e.FrontCompound = front;
                e.RearCompound = rear;
                e.SpareCompound = weather.InitialWetness > 0.35 ? TireCompound.Soft : (Rules.Tires.Has(TireCompound.Rain) ? TireCompound.Rain : TireCompound.Intermediate);
                cfg.Entries.Add(e);
            }
            return new RaceSimulation(cfg);
        }

        private List<RaceEntry> GridOrder(SessionDefinition s)
        {
            var byId = new Dictionary<string, RaceEntry>();
            foreach (var e in Entries) byId[e.Rider.Id] = e;
            var order = new List<string>();
            if (s.GridSource == GridSource.QualifyingWithPreviousRaceTop && s.PreviousRaceSessionId != null && _raceResults.ContainsKey(s.PreviousRaceSessionId))
            {
                var prev = _raceResults[s.PreviousRaceSessionId];
                for (int i = 0; i < prev.Count && order.Count < s.GridFromPreviousRaceTop; i++)
                    if (prev[i].Status == ResultStatus.Finished) order.Add(prev[i].RiderId);
            }
            foreach (var id in _qualifyingOrder) if (!order.Contains(id)) order.Add(id);
            foreach (var e in Entries) if (!order.Contains(e.Rider.Id)) order.Add(e.Rider.Id);
            var grid = new List<RaceEntry>();
            foreach (var id in order)
            {
                RaceEntry e;
                if (byId.TryGetValue(id, out e)) grid.Add(e);
            }
            // Grid penalties
            for (int i = grid.Count - 1; i >= 0; i--)
            {
                var e = grid[i];
                if (e.GridPenaltyPlaces <= 0) continue;
                int target = Math.Min(grid.Count - 1, i + e.GridPenaltyPlaces);
                grid.RemoveAt(i);
                grid.Insert(target, e);
                e.GridPenaltyPlaces = 0;
            }
            return grid;
        }

        /// <summary>Takes a finished race simulation, assigns points and records statistics.</summary>
        public SessionResult CompleteRace(RaceSimulation sim)
        {
            var s = CurrentSession;
            var classification = sim.GetClassification();
            var points = Rules.GetPoints(s.PointsSystem);
            foreach (var r in classification)
            {
                if (r.Status == ResultStatus.Finished && r.Position - 1 < points.Count) r.Points = points[r.Position - 1];
            }
            var res = new SessionResult
            {
                SessionId = s.Id,
                SessionName = s.Name,
                Type = s.Type,
                Classification = classification,
                WeatherSummary = sim.Weather.Describe(),
                Seed = sim.Config.Seed
            };
            Result.Sessions.Add(res);
            _raceResults[s.Id] = classification;

            foreach (var st in sim.Riders)
            {
                if (st.Crashes > 0) Count(CrashesByRider, st.Entry.Rider.Id, st.Crashes);
                if (st.InjuredInCrash && !InjuredRiders.Contains(st.Entry.Rider.Id)) InjuredRiders.Add(st.Entry.Rider.Id);
                // Long laps not served carry over? No: unserved penalties became time. Jump-start/IR may carry to next race.
            }
            ChampionshipService.ApplySession(World, Db, CategoryId, res, sim);
            _trackEvolution += 0.002;
            SessionIndex++;
            return res;
        }

        private static void Count(Dictionary<string, int> map, string key, int n = 1)
        {
            int v;
            map.TryGetValue(key, out v);
            map[key] = v + n;
        }

        public List<string> QualifyingOrder { get { return _qualifyingOrder; } }
    }
}
