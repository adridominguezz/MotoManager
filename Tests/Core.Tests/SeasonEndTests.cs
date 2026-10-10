using System;
using System.Collections.Generic;
using System.Linq;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace MotoManager.Tests
{
    public class SeasonEndTests
    {
        private readonly ITestOutputHelper _out;

        public SeasonEndTests(ITestOutputHelper output)
        {
            _out = output;
        }

        private static void AssertInvariants(WorldState world, GameDatabase db)
        {
            var seen = new HashSet<string>();
            var march = new DateTime(world.Season, 3, 1);
            foreach (var team in world.Teams.Values)
            {
                var cat = db.GetCategory(team.CategoryId);
                var rules = db.GetRules(cat.Id, world.Season);
                Assert.Equal(TransferMarket.Capacity(cat, rules), team.RiderIds.Count);
                var numbers = new HashSet<int>();
                foreach (var id in team.RiderIds)
                {
                    Assert.True(seen.Add(id), "rider " + id + " in two teams");
                    var r = world.GetRider(id);
                    Assert.False(r.IsRetired, "retired rider " + id + " still in a team");
                    Assert.Equal(team.Id, r.TeamId);
                    Assert.Equal(team.CategoryId, r.CategoryId);
                    Assert.NotNull(r.Contract);
                    Assert.True(r.Contract.EndSeason >= world.Season, "expired contract for " + id);
                    int age = r.AgeOn(march);
                    Assert.InRange(age, cat.MinAge, cat.MaxAge);
                }
            }
            foreach (var r in world.Riders.Values)
            {
                if (r.TeamId != null) Assert.Contains(r.Id, world.GetTeam(r.TeamId).RiderIds);
                if (r.IsRetired) Assert.Null(r.TeamId);
            }
            foreach (var cat in db.Categories.Values)
            {
                if (!cat.Simulated) continue;
                var nums = world.ActiveRidersInCategory(cat.Id).Select(r => r.Number).ToList();
                Assert.Equal(nums.Count, nums.Distinct().Count());
            }
        }

        [Fact]
        public void SyntheticWorldIsValidBeforeProcessing()
        {
            var world = TestData.SyntheticWorld(1);
            foreach (var team in world.Teams.Values) Assert.NotEmpty(team.RiderIds);
        }

        [Fact]
        public void OneSeasonEndKeepsGridsFullAndConsistent()
        {
            var db = TestData.Db;
            var world = TestData.SyntheticWorld(42);
            var summary = SeasonEndProcessor.Process(world, db);
            Assert.Equal(2027, world.Season);
            Assert.NotEmpty(summary.Champions);
            Assert.NotEmpty(summary.Market.Moves);
            AssertInvariants(world, db);
            foreach (var cat in db.Categories.Values)
                if (cat.Simulated) Assert.True(world.Championships.ContainsKey(cat.Id));
        }

        [Fact]
        public void TwentySeasonsStayHealthy()
        {
            var db = TestData.Db;
            var world = TestData.SyntheticWorld(7);
            var rng = new DeterministicRandom(99);
            int crossPyramid = 0, fromPool = 0, promotions = 0, retired = 0, generated = 0;
            for (int i = 0; i < 20; i++)
            {
                var s = SeasonEndProcessor.Process(world, db);
                AssertInvariants(world, db);
                retired += s.Retired.Count;
                generated += s.Market.GeneratedRiders;
                foreach (var m in s.Market.Moves)
                {
                    var from = db.GetCategory(m.FromCategoryId);
                    var to = db.GetCategory(m.ToCategoryId);
                    if (from == null || to == null || m.Renewal) continue;
                    if (from.Pyramid != to.Pyramid) crossPyramid++;
                    if (!from.Simulated) fromPool++;
                    if (to.Prestige > from.Prestige) promotions++;
                }
                world.CurrentDate = new DateTime(world.Season, 11, 20);
                TestData.RandomStandings(world, db, rng);
            }
            int active = world.Riders.Values.Count(r => !r.IsRetired);
            _out.WriteLine($"season {world.Season}: active {active}, retired {retired}, generated {generated}, promotions {promotions}, cross-pyramid {crossPyramid}, from pools {fromPool}, champions {world.Champions.Count}");
            foreach (var cat in db.CategoriesOrdered())
            {
                var rs = world.Riders.Values.Where(r => !r.IsRetired && r.CategoryId == cat.Id && (r.TeamId != null || !cat.Simulated)).ToList();
                if (rs.Count == 0) continue;
                _out.WriteLine($"  {cat.Id,-11} riders {rs.Count,3}  overall {rs.Average(r => r.Attributes.Overall):0.0}  age {rs.Average(r => r.AgeOn(new DateTime(world.Season, 3, 1))):0.0}");
            }
            Assert.Equal(2046, world.Season);
            Assert.True(promotions > 50, "riders should climb the ladders");
            Assert.True(fromPool > 20, "talent pools must feed the championships");
            Assert.True(crossPyramid > 0, "some riders should switch between GP and SBK");
            Assert.True(active < 1500, "population must not explode: " + active);
            // Levels keep their hierarchy: MotoGP above Moto2 above Moto3.
            double Level(string c) => world.Riders.Values.Where(r => !r.IsRetired && r.TeamId != null && r.CategoryId == c).Average(r => r.Attributes.Overall);
            Assert.True(Level("motogp") > Level("moto2"));
            Assert.True(Level("moto2") > Level("moto3"));
            Assert.True(Level("worldsbk") > Level("worldssp"));
        }

        [Fact]
        public void SeasonEndIsDeterministic()
        {
            var db = TestData.Db;
            var a = TestData.SyntheticWorld(5);
            var b = TestData.SyntheticWorld(5);
            SeasonEndProcessor.Process(a, db);
            SeasonEndProcessor.Process(b, db);
            Assert.Equal(JsonSettings.Serialize(a), JsonSettings.Serialize(b));
        }
    }
}
