using System;
using System.Collections.Generic;
using System.Linq;
using MotoManager.Core.Career;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace MotoManager.Tests
{
    public class TeamAITests
    {
        private readonly ITestOutputHelper _out;

        public TeamAITests(ITestOutputHelper output)
        {
            _out = output;
        }

        [Fact]
        public void EveryTeamGetsADeterministicPersonalityAndObjective()
        {
            var db = TestData.Db;
            var a = WorldFactory.Create(db, new NewGameOptions { Seed = 5 });
            var b = WorldFactory.Create(db, new NewGameOptions { Seed = 5 });
            foreach (var team in a.Teams.Values)
            {
                Assert.NotNull(team.Personality);
                var other = b.Teams[team.Id].Personality;
                Assert.Equal(team.Personality.Philosophy, other.Philosophy);
                Assert.Equal(team.Personality.RiskTolerance, other.RiskTolerance);
                Assert.Equal(team.Personality.Objective, other.Objective);
                if (team.Status == TeamStatus.Factory) Assert.True(team.Personality.ManufacturerSupport > 85);
                if (team.Status == TeamStatus.Independent) Assert.True(team.Personality.ManufacturerSupport < 50);
            }
            // Exactly one title favourite per simulated category, and variety of philosophies.
            foreach (var cat in db.Categories.Values.Where(c => c.Simulated))
                Assert.Single(a.TeamsInCategory(cat.Id), t => t.Personality.Objective == TeamObjective.Title);
            Assert.True(a.Teams.Values.Select(t => t.Personality.Philosophy).Distinct().Count() >= 4);
            foreach (var t in a.TeamsInCategory("motogp").OrderBy(t => t.Personality.Objective))
                _out.WriteLine(t.Name.PadRight(32) + t.Personality.Objective + " " + t.Personality.Philosophy + " risk " + t.Personality.RiskTolerance + " tech " + t.Personality.TechnicalLevel);
        }

        [Fact]
        public void PhilosophyChangesWhoATeamWants()
        {
            var db = TestData.Db;
            var world = WorldFactory.Create(db, new NewGameOptions { Seed = 6 });
            var team = world.TeamsInCategory("moto2").First(t => !t.IsPlayer);
            var riders = world.Riders.Values.Where(r => r.CategoryId == "moto2" && !r.IsRetired).ToList();
            var march = new DateTime(world.Season + 1, 3, 1);
            var young = riders.OrderBy(r => r.AgeOn(march)).ThenBy(r => r.Id).First();
            var veteran = riders.OrderByDescending(r => r.AgeOn(march)).ThenBy(r => r.Id).First();

            team.Personality.Philosophy = TeamPhilosophy.YoungTalent;
            double youthTeamGap = TransferMarket.EvaluateForTeam(world, db, team, young) - TransferMarket.EvaluateForTeam(world, db, team, veteran);
            team.Personality.Philosophy = TeamPhilosophy.Experience;
            double experienceTeamGap = TransferMarket.EvaluateForTeam(world, db, team, young) - TransferMarket.EvaluateForTeam(world, db, team, veteran);
            _out.WriteLine("young " + young.FullName + " vs veteran " + veteran.FullName + ": youth team " + youthTeamGap.ToString("0.0") + ", experience team " + experienceTeamGap.ToString("0.0"));
            Assert.True(youthTeamGap > experienceTeamGap + 2);
        }

        [Fact]
        public void RiskTakersChooseSofterTyres()
        {
            var rules = TestData.Db.GetRules("motogp", 2026);
            TireCompound fCautious, rCautious, fBold, rBold;
            RaceAI.ChooseTires(rules, 38, 0, 0.55, 25, 50, out fCautious, out rCautious, 10);
            RaceAI.ChooseTires(rules, 38, 0, 0.55, 25, 50, out fBold, out rBold, 90);
            Assert.True(rBold < rCautious || fBold < fCautious, "bold " + fBold + "/" + rBold + " cautious " + fCautious + "/" + rCautious);
        }

        [Fact]
        public void MissedObjectivesRaisePressureAndSaveKeepsPersonality()
        {
            var db = TestData.Db;
            var game = GameController.NewGame(db, new NewGameOptions { Seed = 8 });
            var before = game.World.Teams.Values.ToDictionary(t => t.Id, t => t.Personality.RiskTolerance);
            while (!game.IsSeasonOver)
            {
                var ev = game.BeginNextEvent();
                game.CompleteEvent(ev);
            }
            game.EndSeason();
            var w = game.World;
            int missed = 0, raised = 0;
            foreach (var t in w.Teams.Values)
            {
                var cat = db.GetCategory(t.CategoryId);
                if (cat == null || !cat.Simulated || t.Personality.LastPosition == 0) continue;
                if (!t.Personality.LastObjectiveMet)
                {
                    missed++;
                    if (t.Personality.RiskTolerance > before[t.Id] || t.Personality.SeasonsMissed == 0) raised++;
                }
            }
            _out.WriteLine("teams that missed their objective: " + missed);
            Assert.True(missed > 0);
            Assert.Equal(missed, raised);

            var json = SaveSystem.Serialize(w);
            var loaded = SaveSystem.Deserialize(json);
            foreach (var t in w.Teams.Values)
            {
                Assert.Equal(t.Personality.Philosophy, loaded.Teams[t.Id].Personality.Philosophy);
                Assert.Equal(t.Personality.Objective, loaded.Teams[t.Id].Personality.Objective);
            }

            // Old saves without personalities get them on load.
            foreach (var t in loaded.Teams.Values) t.Personality = null;
            var resumed = new GameController(db, loaded);
            Assert.All(resumed.World.Teams.Values, t => Assert.NotNull(t.Personality));
        }
    }
}
