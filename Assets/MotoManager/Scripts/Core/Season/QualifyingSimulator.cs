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
    /// Time-attack sessions (practice, qualifying, Superpole).
    ///
    /// Every rider's clean lap is simulated once with the full race physics (bike, rider, tires, track temperature,
    /// wetness, track evolution). Each attempt in the session then varies around that lap: lap-to-lap consistency,
    /// traffic that ruins a lap, a tow that improves it (stronger where the slipstream matters), mistakes and crashes
    /// whose odds rise when pushing on a wet or cold track. This keeps qualifying a consequence of the simulation
    /// while costing a fraction of simulating every out-lap.
    /// </summary>
    public static class QualifyingSimulator
    {
        public static List<LapRun> RunSession(List<RaceEntry> entries, CategoryRules rules, CircuitDefinition circuit,
            SimulationTuning tuning, WeatherSetup weather, ulong seed, SessionType type, int runs, int flyingLapsPerRun,
            double evolution, Dictionary<string, double> cleanLapCache = null)
        {
            var results = new List<LapRun>();
            var track = TrackModel.Get(circuit);
            int corners = Math.Max(1, track.CornerCount);
            bool wet = weather.InitialWetness > 0.35 || (weather.RainEvents.Count > 0 && weather.RainEvents[0].StartTime < 0);
            foreach (var entry in entries)
            {
                var run = new LapRun { Entry = entry };
                var rng = new DeterministicRandom(DeterministicRandom.Combine(seed, DeterministicRandom.Hash(entry.Rider.Id)));
                double clean = CleanLap(entry, rules, circuit, tuning, weather, type, evolution, wet, cleanLapCache);
                var a = entry.Rider.Attributes;
                double sd = tuning.ConsistencyNoise * (1.6 - a.Consistency / 100.0) * (wet ? 1.6 : 1.0);
                double push = type == SessionType.Qualifying ? 1.0 : 0.6;
                double risk = (0.7 + a.Aggression / 100.0 * 0.6) * (1.5 - a.RiskManagement / 100.0) * (wet ? tuning.WetCrashMultiplier : 1.0) * push;
                double crashPerLap = tuning.BaseCrashChance * corners * 0.8 * risk * (type == SessionType.Qualifying ? tuning.PushRisk : 1.0);
                double errorPerLap = tuning.BaseErrorChance * corners * 0.5 * Math.Pow(risk, 0.6) * (1.5 - a.Consistency / 100.0);

                for (int k = 0; k < runs && !run.Crashed; k++)
                {
                    double runEvolution = 1 - 0.0025 * k; // the track keeps rubbering in
                    for (int lap = 0; lap < flyingLapsPerRun; lap++)
                    {
                        run.Laps++;
                        if (rng.Chance(crashPerLap))
                        {
                            run.Crashed = true;
                            run.Note = "Crashed";
                            break;
                        }
                        double t = clean * runEvolution * (1 + Math.Abs(rng.Gaussian(0, sd)));
                        if (rng.Chance(errorPerLap)) t += rng.Range(0.3, 1.5);
                        double roll = rng.NextDouble();
                        if (roll < 0.18) t += rng.Range(0.15, 0.9);
                        else if (roll < 0.18 + 0.15 * rules.SlipstreamFactor) t -= rng.Range(0.03, 0.12) * rules.SlipstreamFactor;
                        // Tires peak on the first flying lap.
                        if (lap > 0) t += 0.08 * lap;
                        if (t < run.BestLap) run.BestLap = t;
                    }
                }
                results.Add(run);
            }
            results.Sort((x, y) =>
            {
                int c = x.BestLap.CompareTo(y.BestLap);
                return c != 0 ? c : string.CompareOrdinal(x.Entry.Rider.Id, y.Entry.Rider.Id);
            });
            return results;
        }

        public static double CleanLap(RaceEntry entry, CategoryRules rules, CircuitDefinition circuit, SimulationTuning tuning,
            WeatherSetup weather, SessionType type, double evolution, bool wet, Dictionary<string, double> cache)
        {
            string key = entry.Rider.Id + "|" + type + "|" + (wet ? "wet" : "dry") + "|" + Math.Round(evolution, 4);
            double lap;
            if (cache != null && cache.TryGetValue(key, out lap)) return lap;
            var e = new RaceEntry
            {
                Rider = entry.Rider, Team = entry.Team, Bike = entry.Bike, ManufacturerId = entry.ManufacturerId,
                SetupQuality = entry.SetupQuality, Form = entry.Form, GridPosition = 1
            };
            if (wet)
            {
                e.FrontCompound = rules.Tires.Has(TireCompound.Rain) ? TireCompound.Rain : TireCompound.Intermediate;
                e.RearCompound = e.FrontCompound;
            }
            else
            {
                var compound = type == SessionType.Practice && rules.Tires.Has(TireCompound.Medium) ? TireCompound.Medium : rules.Qualifying.PreferredCompound;
                e.FrontCompound = compound;
                e.RearCompound = compound;
            }
            var cfg = new RaceConfig { Rules = rules, Circuit = circuit, Tuning = tuning, Weather = weather, TrackEvolution = evolution };
            lap = RaceSimulation.EstimateLap(cfg, e, type);
            if (cache != null) cache[key] = lap;
            return lap;
        }
    }
}
