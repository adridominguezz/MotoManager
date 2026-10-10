using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Race;

namespace MotoManager.Core.Season
{
    public class LapRun
    {
        public RaceEntry Entry;
        public double BestLap = double.MaxValue;
        public bool Crashed;
        public string Note;
        public int Laps;
    }

    /// <summary>
    /// Time-attack sessions (practice, qualifying, Superpole). Each rider's runs are simulated with the same micro
    /// physics as the race (tires, weather, errors and crashes), then traffic and tows are applied: a lap can be
    /// ruined by a slow rider or improved by following a faster one. Track rubber improves grip during the session.
    /// </summary>
    public static class QualifyingSimulator
    {
        public static List<LapRun> RunSession(List<RaceEntry> entries, CategoryRules rules, CircuitDefinition circuit,
            SimulationTuning tuning, WeatherSetup weather, ulong seed, SessionType type, int runs, int flyingLapsPerRun,
            double baseEvolution, double timeStep)
        {
            var results = new List<LapRun>();
            var rng = new DeterministicRandom(seed);
            foreach (var entry in entries)
            {
                var run = new LapRun { Entry = entry };
                var riderRng = new DeterministicRandom(DeterministicRandom.Combine(seed, DeterministicRandom.Hash(entry.Rider.Id)));
                for (int k = 0; k < runs && !run.Crashed; k++)
                {
                    double evolution = baseEvolution + 0.004 * k;
                    var cfg = new RaceConfig
                    {
                        Rules = rules,
                        Circuit = circuit,
                        Tuning = tuning,
                        SessionType = type,
                        SessionName = type.ToString(),
                        Laps = flyingLapsPerRun,
                        Seed = riderRng.NextULong(),
                        TimeStep = timeStep,
                        Weather = weather,
                        TimeTrial = true,
                        TrackEvolution = evolution
                    };
                    var e = Clone(entry);
                    e.GridPosition = 1;
                    // Time attack on the softest dry compound (or wets when the track is wet).
                    if (weather.InitialWetness > 0.35 || weather.RainEvents.Count > 0 && weather.RainEvents[0].StartTime < 0)
                    {
                        e.FrontCompound = rules.Tires.Has(TireCompound.Rain) ? TireCompound.Rain : TireCompound.Intermediate;
                        e.RearCompound = e.FrontCompound;
                    }
                    else
                    {
                        e.FrontCompound = rules.Qualifying.PreferredCompound;
                        e.RearCompound = rules.Qualifying.PreferredCompound;
                        if (type == SessionType.Practice)
                        {
                            e.FrontCompound = rules.Tires.Has(TireCompound.Medium) ? TireCompound.Medium : e.FrontCompound;
                            e.RearCompound = e.FrontCompound;
                        }
                    }
                    cfg.Entries.Add(e);
                    var sim = new RaceSimulation(cfg);
                    sim.RunToEnd();
                    var st = sim.Riders[0];
                    run.Laps += Math.Max(0, st.Lap);
                    if (st.Status == RaceStatus.Retired)
                    {
                        run.Crashed = true;
                        run.Note = "Crashed";
                    }
                    if (st.BestLapTime < double.MaxValue)
                    {
                        double lap = st.BestLapTime;
                        // Traffic / tow
                        double roll = rng.NextDouble();
                        if (roll < 0.2) lap += rng.Range(0.15, 0.9);
                        else if (roll < 0.2 + 0.15 * rules.SlipstreamFactor) lap -= rng.Range(0.03, 0.12) * rules.SlipstreamFactor;
                        if (lap < run.BestLap) run.BestLap = lap;
                    }
                }
                results.Add(run);
            }
            results.Sort((a, b) =>
            {
                int c = a.BestLap.CompareTo(b.BestLap);
                return c != 0 ? c : string.CompareOrdinal(a.Entry.Rider.Id, b.Entry.Rider.Id);
            });
            return results;
        }

        private static RaceEntry Clone(RaceEntry e)
        {
            return new RaceEntry
            {
                Rider = e.Rider,
                Team = e.Team,
                Bike = e.Bike,
                ManufacturerId = e.ManufacturerId,
                FrontCompound = e.FrontCompound,
                RearCompound = e.RearCompound,
                SetupQuality = e.SetupQuality,
                Form = e.Form,
                IsPlayer = e.IsPlayer,
                AutoManaged = true
            };
        }
    }
}
