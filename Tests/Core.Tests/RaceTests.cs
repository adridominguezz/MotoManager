using System;
using System.Collections.Generic;
using System.Linq;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace MotoManager.Tests
{
    public class RaceTests
    {
        private readonly ITestOutputHelper _out;
        private static WorldState _world;

        public RaceTests(ITestOutputHelper output)
        {
            _out = output;
        }

        private static WorldState World
        {
            get { return _world ?? (_world = WorldFactory.Create(TestData.Db, new NewGameOptions { Seed = 1 })); }
        }

        /// <summary>A race with the real 2026 grid of a category.</summary>
        public static RaceConfig MakeRace(string categoryId, string circuitId, ulong seed, double dt, int laps = 0, WeatherSetup weather = null)
        {
            var db = TestData.Db;
            var rules = db.GetRules(categoryId, 2026);
            var circuit = db.Circuits[circuitId];
            var cfg = new RaceConfig
            {
                Rules = rules, Circuit = circuit, Tuning = db.Tuning, Seed = seed, TimeStep = dt,
                Laps = laps > 0 ? laps : (int)Math.Round(rules.RaceDistanceKm * 1000 / circuit.LengthM),
                FuelLimitLitres = rules.Fuel.TankLimitLitres,
                Weather = weather ?? new WeatherSetup { AirTemp = circuit.AverageAirTempC }
            };
            int grid = 1;
            foreach (var team in World.TeamsInCategory(categoryId))
            {
                var bike = World.GetBike(team.BikeId);
                var perf = BikePerformance.Compute(bike, db.GetBikeClass(bike.BikeClassId));
                foreach (var id in team.RiderIds)
                {
                    TireCompound f, r;
                    RaceAI.ChooseTires(rules, circuit.AverageAirTempC + 10, cfg.Weather.InitialWetness, circuit.Abrasiveness, cfg.Laps, 60, out f, out r);
                    cfg.Entries.Add(new RaceEntry
                    {
                        Rider = World.GetRider(id), Team = team, Bike = perf, ManufacturerId = team.ManufacturerId,
                        GridPosition = grid++, FrontCompound = f, RearCompound = r, SetupQuality = 0.7,
                        SpareCompound = rules.Tires.Has(TireCompound.Rain) ? TireCompound.Rain : TireCompound.Intermediate
                    });
                }
            }
            return cfg;
        }

        private static string Fingerprint(RaceSimulation sim)
        {
            return string.Join(";", sim.GetClassification().Select(r => r.RiderId + ":" + r.Status + ":" + r.TotalTime.ToString("0.000"))) +
                   "|" + sim.Events.Count + "|" + sim.Tick;
        }

        [Fact]
        public void SameSeedSameRace()
        {
            var a = new RaceSimulation(MakeRace("motogp", "jerez", 77, 0.1));
            var b = new RaceSimulation(MakeRace("motogp", "jerez", 77, 0.1));
            a.RunToEnd();
            b.RunToEnd();
            Assert.Equal(Fingerprint(a), Fingerprint(b));
            var c = new RaceSimulation(MakeRace("motogp", "jerez", 78, 0.1));
            c.RunToEnd();
            Assert.NotEqual(Fingerprint(a), Fingerprint(c));
        }

        [Fact]
        public void ReplayFromCommandLogReproducesTheRace()
        {
            var live = new RaceSimulation(MakeRace("motogp", "assen", 5, 0.1));
            while (!live.IsFinished)
            {
                if (live.Tick == 300) live.Enqueue(new RaceCommand { RiderIndex = 0, Type = RaceCommandType.SetPace, Pace = PaceMode.Push });
                if (live.Tick == 900) live.Enqueue(new RaceCommand { RiderIndex = 3, Type = RaceCommandType.SetRiderMode, Mode = RiderMode.Attack });
                if (live.Tick == 1500) live.Enqueue(new RaceCommand { RiderIndex = 5, Type = RaceCommandType.RequestPit, Pit = PitRequestType.BikeSwap, Compound = TireCompound.Soft });
                live.Step();
            }
            Assert.Equal(3, live.CommandLog.Count);
            var replay = new RaceSimulation(MakeRace("motogp", "assen", 5, 0.1), live.CommandLog);
            replay.RunToEnd();
            Assert.Equal(Fingerprint(live), Fingerprint(replay));
        }

        [Fact]
        public void LapTimeDoesNotDependOnTimeStep()
        {
            double Lap(double dt)
            {
                var cfg = MakeRace("motogp", "mugello", 3, dt);
                return RaceSimulation.EstimateLap(cfg, cfg.Entries[0]);
            }
            double fine = Lap(0.05), coarse = Lap(0.2);
            _out.WriteLine($"0.05s: {fine:0.000}  0.2s: {coarse:0.000}");
            Assert.InRange(coarse / fine, 0.995, 1.005);
        }

        [Fact]
        public void CircuitsCloseAndMatchRealLapTimes()
        {
            var db = TestData.Db;
            foreach (var c in db.Circuits.Values)
            {
                var track = TrackModel.Get(c);
                var geo = new TrackGeometry(track);
                Assert.True(geo.ResidualError / track.Length < 0.07, c.Id + " geometry residual " + geo.ResidualError);
                Assert.InRange(Math.Abs(track.Length - c.LengthM), 0, 1);
                double reference;
                if (!c.ReferenceLaps.TryGetValue("motogp", out reference)) continue;
                var cfg = MakeRace("motogp", c.Id, 1, 0.05);
                // Average rider (overall ~75) on an average bike: the reference race pace.
                var rider = new Rider { Id = "ref" };
                foreach (var n in RiderAttributes.Names) rider.Attributes.Set(n, 75);
                rider.Attributes.Aggression = 50;
                var bike = BikePerformance.Average();
                for (int i = 0; i < bike.Values.Length; i++) bike.Values[i] = 65;
                var e = new RaceEntry { Rider = rider, Bike = bike, SetupQuality = 0.6, FrontCompound = TireCompound.Medium, RearCompound = TireCompound.Medium };
                double lap = RaceSimulation.EstimateLap(cfg, e);
                Assert.True(Math.Abs(lap / reference - 1) < 0.03, c.Id + ": " + lap.ToString("0.00") + " vs " + reference);
            }
        }

        [Fact]
        public void IncidentFuelAndTireRatesAreRealistic()
        {
            int races = 10, crashes = 0, failures = 0, finishers = 0, outOfFuel = 0, overtakes = 0;
            double rearWear = 0;
            int wearSamples = 0;
            string[] tracks = { "mugello", "jerez", "sepang", "assen", "lusail" };
            for (int i = 0; i < races; i++)
            {
                var sim = new RaceSimulation(MakeRace("motogp", tracks[i % tracks.Length], (ulong)(1000 + i), 0.2));
                sim.RunToEnd();
                crashes += sim.Events.Count(e => e.Type == RaceEventType.Crash);
                failures += sim.Events.Count(e => e.Type == RaceEventType.MechanicalFailure);
                overtakes += sim.Events.Count(e => e.Type == RaceEventType.Overtake);
                foreach (var r in sim.Riders)
                {
                    if (r.Status != RaceStatus.Finished) continue;
                    finishers++;
                    rearWear += r.Rear.Wear;
                    wearSamples++;
                    if (r.Fuel <= 0) outOfFuel++;
                }
            }
            double crashesPerRace = (double)crashes / races, failuresPerRace = (double)failures / races;
            _out.WriteLine($"crashes/race {crashesPerRace:0.0}, failures/race {failuresPerRace:0.00}, finishers/race {(double)finishers / races:0.0}, overtakes/race {(double)overtakes / races:0}, rear wear {rearWear / wearSamples:0.00}, out of fuel {outOfFuel}");
            Assert.InRange(crashesPerRace, 1.0, 7.0);
            Assert.InRange(failuresPerRace, 0.05, 1.5);
            Assert.InRange((double)finishers / races, 14, 22);
            Assert.InRange(rearWear / wearSamples, 0.25, 0.85);
            Assert.Equal(0, outOfFuel);
        }

        [Fact]
        public void WeatherChangesGraduallyAndForecastsAreProbabilities()
        {
            var setup = new WeatherSetup { AirTemp = 20, CloudCover = 0.8 };
            setup.RainEvents.Add(new RainEvent { StartTime = 300, RampTime = 120, Duration = 900, Intensity = 0.8 });
            var w = new WeatherModel(setup, TestData.Db.Tuning, 9);
            double prev = w.TrackWetness, maxStep = 0;
            var forecast = w.ForecastRainProbability(600, 60);
            for (int i = 0; i < 2400 * 5; i++)
            {
                w.Update(0.2);
                maxStep = Math.Max(maxStep, Math.Abs(w.TrackWetness - prev));
                prev = w.TrackWetness;
                if (Math.Abs(w.Time - 900) < 0.1) Assert.True(w.TrackWetness > 0.4, "track should be wet during rain");
            }
            Assert.True(maxStep < 0.005, "wetness jumped " + maxStep);
            Assert.InRange(forecast, 0.05, 0.95);
            Assert.True(w.TrackWetness < 0.2, "track should dry after the rain: " + w.TrackWetness);
        }

        [Fact]
        public void BikeSwapOnlyWhereTheRulesAllowIt()
        {
            var gp = new RaceSimulation(MakeRace("motogp", "jerez", 1, 0.2, 3));
            var m2 = new RaceSimulation(MakeRace("moto2", "jerez", 1, 0.2, 3));
            foreach (var sim in new[] { gp, m2 })
            {
                sim.Enqueue(new RaceCommand { RiderIndex = 0, Type = RaceCommandType.RequestPit, Pit = PitRequestType.BikeSwap, Compound = TireCompound.Rain });
                sim.Step();
            }
            Assert.Equal(PitRequestType.BikeSwap, gp.Riders[0].PitRequest);
            Assert.Equal(PitRequestType.None, m2.Riders[0].PitRequest);
            Assert.False(TestData.Db.GetRules("moto2", 2026).PitLane.AllowsBikeSwap);
        }

        [Fact]
        public void RainMidRaceTriggersFlagToFlagStops()
        {
            var weather = new WeatherSetup { AirTemp = 18, CloudCover = 0.9 };
            weather.RainEvents.Add(new RainEvent { StartTime = 500, RampTime = 60, Duration = 5000, Intensity = 0.9 });
            var sim = new RaceSimulation(MakeRace("motogp", "assen", 21, 0.2, 0, weather));
            sim.RunToEnd();
            int stops = sim.Events.Count(e => e.Type == RaceEventType.PitIn);
            int onWets = sim.Riders.Count(r => r.Status == RaceStatus.Finished && r.Rear.Spec.IsWet);
            _out.WriteLine($"pit stops {stops}, finishers on wets {onWets}, white flag {sim.WhiteFlag}");
            Assert.True(sim.WhiteFlag);
            Assert.True(stops >= 10, "most riders should swap bikes");
            Assert.True(onWets >= 10);
        }

        [Fact]
        public void JumpStartsArePenalisedAndServed()
        {
            var cfg = MakeRace("motogp", "jerez", 4, 0.2, 8);
            cfg.Tuning = JsonSettings.DeepClone(TestData.Db.Tuning);
            cfg.Tuning.JumpStartChance = 1.0;
            var sim = new RaceSimulation(cfg);
            sim.RunToEnd();
            Assert.Contains(sim.Events, e => e.Type == RaceEventType.JumpStart);
            Assert.Contains(sim.Riders, r => r.Penalties.Any(p => p.Infraction == InfractionType.JumpStart && p.Type == PenaltyType.DoubleLongLap));
            Assert.Contains(sim.Events, e => e.Type == RaceEventType.PenaltyServed);
        }

        [Fact]
        public void PointsFollowTheCategoryTables()
        {
            var db = TestData.Db;
            var game = GameController.NewGame(db, new NewGameOptions { Seed = 2 });
            game.CompleteEvent(game.BeginNextEvent()); // WorldSBK round 1
            var round = game.World.Championships["worldsbk"].Rounds[0];
            var race1 = round.Sessions.First(s => s.SessionId == "r1");
            var spr = round.Sessions.First(s => s.SessionId == "spr");
            Assert.Equal(25, race1.Classification[0].Points);
            Assert.Equal(12, spr.Classification[0].Points);
            Assert.All(race1.Classification.Where(r => r.Status != ResultStatus.Finished), r => Assert.Equal(0, r.Points));
        }
    }
}
