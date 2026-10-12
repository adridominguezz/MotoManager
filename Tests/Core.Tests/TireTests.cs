using System;
using System.Linq;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using MotoManager.Core.Season;
using Xunit;
using Xunit.Abstractions;

namespace MotoManager.Tests
{
    public class TireTests
    {
        private readonly ITestOutputHelper _out;
        public TireTests(ITestOutputHelper o) { _out = o; }

        public static double Wear, FrontWear;

        public static double[] LapProfile(string cat, TireCompound c, ulong seed, Action<RiderRaceState> setup = null)
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = seed });
            game.SimulateUntilEventFor(cat);
            var ev = game.BeginNextEvent();
            var w = ev.Weekends[cat];
            while (!w.IsComplete && !(w.CurrentSession.Type == SessionType.Race)) w.RunCurrentHeadless();
            var sim = w.CreateRaceSimulation(0.2);
            foreach (var r in sim.Riders)
            {
                TireModel.Fit(r.Front, sim.Rules.Tires.Get(c), r.Front.Temperature);
                TireModel.Fit(r.Rear, sim.Rules.Tires.Get(c), r.Rear.Temperature);
                if (setup != null) setup(r);
            }
            sim.RunToEnd();
            Wear = sim.Riders.Where(r => r.Status == RaceStatus.Finished).Average(r => r.Rear.Wear);
            FrontWear = sim.Riders.Where(r => r.Status == RaceStatus.Finished).Average(r => r.Front.Wear);
            int laps = sim.TotalLaps;
            var res = new double[laps];
            for (int l = 0; l < laps; l++)
            {
                var t = sim.Riders.Where(r => r.Stats.LapTimes.Count > l).Select(r => r.Stats.LapTimes[l]).OrderBy(x => x).ToList();
                res[l] = t.Count > 0 ? t[t.Count / 2] : 0;
            }
            return res;
        }

        [Fact]
        public void SoftsAreQuickEarlyHardsLastAndPushingWearsTheTyres()
        {
            var soft = LapProfile("motogp", TireCompound.Soft, 4);
            var medium = LapProfile("motogp", TireCompound.Medium, 4);
            var hard = LapProfile("motogp", TireCompound.Hard, 4);
            Func<double[], double> early = p => p.Skip(1).Take(3).Average();
            Func<double[], double> late = p => p.Skip(p.Length - 3).Average();
            _out.WriteLine("early S/M/H " + early(soft) + " " + early(medium) + " " + early(hard));
            _out.WriteLine("late  S/M/H " + late(soft) + " " + late(medium) + " " + late(hard));
            Assert.True(early(soft) < early(medium) && early(medium) < early(hard), "softs are quickest on fresh tyres");
            Assert.True(late(hard) < late(medium) && late(medium) < late(soft), "hards are quickest at the end");
            Assert.True(Math.Abs(soft.Sum() - hard.Sum()) < 20, "no compound dominates a full race distance");

            var push = LapProfile("motogp", TireCompound.Medium, 4, r => { r.Entry.AutoManaged = false; r.Pace = PaceMode.Push; });
            double pushWear = Wear;
            var save = LapProfile("motogp", TireCompound.Medium, 4, r => { r.Entry.AutoManaged = false; r.Pace = PaceMode.Conserve; });
            double saveWear = Wear;
            Assert.True(early(push) < early(medium) - 0.3 && early(save) > early(medium) + 0.3, "pace orders are clearly felt");
            Assert.True(pushWear > saveWear * 1.6, "pushing wears the tyres much faster than saving them");
        }
    }
}
