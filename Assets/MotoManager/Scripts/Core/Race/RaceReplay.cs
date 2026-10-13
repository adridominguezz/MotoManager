using System.Collections.Generic;

namespace MotoManager.Core.Race
{
    /// <summary>
    /// A race replay is not a recording: it is the initial configuration plus the player's command log.
    /// Because the simulation is deterministic, re-simulating reproduces every overtake and crash exactly,
    /// at a cost of a few kilobytes. Seeking backwards simply re-simulates (headless) up to the wanted tick.
    /// </summary>
    public class RaceReplay
    {
        private readonly RaceConfig _config;
        private readonly bool[] _autoManaged;
        private readonly int[] _longLaps;
        private readonly List<RaceCommand> _commands;

        public long TotalTicks { get; private set; }
        public string Title;

        private RaceReplay(RaceConfig config, bool[] autoManaged, int[] longLaps, List<RaceCommand> commands, long ticks)
        {
            _config = config;
            _autoManaged = autoManaged;
            _longLaps = longLaps;
            _commands = commands;
            TotalTicks = ticks;
        }

        /// <summary>Remembers the starting state of a simulation (call before the first Step).</summary>
        public static RaceReplay Begin(RaceSimulation sim)
        {
            // Own copy of the configuration: the weekend reuses entries for the next session.
            var src = sim.Config;
            var config = new RaceConfig
            {
                Rules = src.Rules, Circuit = src.Circuit, Tuning = src.Tuning, SessionType = src.SessionType, SessionName = src.SessionName,
                Laps = src.Laps, DistanceScale = src.DistanceScale, Seed = src.Seed, TimeStep = src.TimeStep, Weather = src.Weather, FuelLimitLitres = src.FuelLimitLitres,
                TrackEvolution = src.TrackEvolution, TimeTrial = src.TimeTrial, MaxDurationS = src.MaxDurationS,
                ReferenceLaps = src.ReferenceLaps != null ? new Dictionary<string, double>(src.ReferenceLaps) : null
            };
            var auto = new bool[src.Entries.Count];
            var longLaps = new int[src.Entries.Count];
            for (int i = 0; i < src.Entries.Count; i++)
            {
                var e = src.Entries[i];
                config.Entries.Add(new RaceEntry
                {
                    Rider = e.Rider, Team = e.Team, Bike = e.Bike, ManufacturerId = e.ManufacturerId, GridPosition = e.GridPosition,
                    FrontCompound = e.FrontCompound, RearCompound = e.RearCompound, SpareCompound = e.SpareCompound,
                    SetupQuality = e.SetupQuality, SetupCeiling = e.SetupCeiling, TireCare = e.TireCare, Form = e.Form, IsPlayer = e.IsPlayer, AutoManaged = e.AutoManaged,
                    PendingLongLaps = e.PendingLongLaps, GridPenaltyPlaces = e.GridPenaltyPlaces, ReliabilityBonus = e.ReliabilityBonus
                });
                auto[i] = e.AutoManaged;
                longLaps[i] = e.PendingLongLaps;
            }
            return new RaceReplay(config, auto, longLaps, new List<RaceCommand>(), 0);
        }

        /// <summary>Stores the commands the player gave once the race is over.</summary>
        public void Finish(RaceSimulation sim)
        {
            _commands.Clear();
            _commands.AddRange(sim.CommandLog);
            TotalTicks = sim.Tick;
        }

        /// <summary>A fresh simulation that will replay the race exactly.</summary>
        public RaceSimulation CreateSimulation()
        {
            for (int i = 0; i < _config.Entries.Count; i++)
            {
                _config.Entries[i].AutoManaged = _autoManaged[i];
                _config.Entries[i].PendingLongLaps = _longLaps[i];
            }
            var copy = new List<RaceCommand>();
            foreach (var c in _commands)
                copy.Add(new RaceCommand { Tick = c.Tick, RiderIndex = c.RiderIndex, Type = c.Type, Pace = c.Pace, Mode = c.Mode, Pit = c.Pit, Compound = c.Compound, Flag = c.Flag });
            return new RaceSimulation(_config, copy);
        }

        /// <summary>A replay simulation already advanced to <paramref name="tick"/> (headless).</summary>
        public RaceSimulation CreateAt(long tick)
        {
            var sim = CreateSimulation();
            while (sim.Tick < tick && !sim.IsFinished) sim.Step();
            return sim;
        }

        public int CommandCount { get { return _commands.Count; } }
    }
}
