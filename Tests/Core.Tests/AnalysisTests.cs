using System.Linq;
using MotoManager.Core.Race;
using Xunit;
using Xunit.Abstractions;

namespace MotoManager.Tests
{
    public class AnalysisTests
    {
        private readonly ITestOutputHelper _out;

        public AnalysisTests(ITestOutputHelper output)
        {
            _out = output;
        }

        [Fact]
        public void PostRaceAnalysisExplainsResults()
        {
            var sim = new RaceSimulation(RaceTests.MakeRace("motogp", "mugello", 31, 0.1));
            sim.RunToEnd();
            int withItems = 0;
            foreach (var r in sim.Riders.OrderBy(x => x.Position))
            {
                var rep = RaceAnalysis.Analyze(sim, r.Index);
                if (rep.Strengths.Count + rep.Weaknesses.Count > 0) withItems++;
                Assert.True(rep.Strengths.All(i => i.Strength) && rep.Weaknesses.All(i => !i.Strength));
                if (r.Position <= 3 || r.Position >= 20)
                    _out.WriteLine($"P{r.Position} {r.Entry.Rider.ShortName} ({r.Entry.Team.ShortName}) grid {rep.Grid} top {rep.TopSpeedKph:0} ovt {rep.Overtakes}/{rep.TimesPassed} err {rep.Errors}\n   + " +
                                   string.Join(", ", rep.Strengths.Select(i => i.Code + " " + i.Value.ToString("0.00"))) + "\n   - " +
                                   string.Join(", ", rep.Weaknesses.Select(i => i.Code + " " + i.Value.ToString("0.00"))));
                Assert.Equal(r.Stats.LapTimes.Count, r.Stats.LapPositions.Count);
            }
            Assert.True(withItems > sim.Riders.Count / 2, "most riders should get an explanation");
            var winner = sim.Riders.First(x => x.Position == 1);
            Assert.True(winner.Stats.BestSectors != null && winner.Stats.BestSectors.All(t => t > 1 && t < 200));
        }

        [Fact]
        public void CircuitProfilesDifferAndComeFromGeometry()
        {
            var all = TestData.Db.Circuits.Values.OrderBy(c => c.Id).Select(c => (c.Id, p: TrackProfile.Compute(TrackModel.Get(c)))).ToList();
            foreach (var (id, p) in all)
                _out.WriteLine($"{id,-14} straights {p.StraightShare:0.00} longest {p.LongestStraight:0} braking {p.HeavyBrakingZones} slow {p.SlowCornerShare:0.00} fast {p.FastCornerShare:0.00} -> {string.Join(", ", p.Traits)}");
            Assert.True(all.Select(x => string.Join(",", x.p.Traits)).Distinct().Count() >= 8, "circuits should not all look the same");
            Assert.Contains(TrackTrait.TopSpeed, all.First(x => x.Id == "mugello").p.Traits);
        }
    }
}
