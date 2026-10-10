using System.Linq;
using MotoManager.Core.Career;
using MotoManager.Core.Data;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using Xunit;

namespace MotoManager.Tests
{
    public class GameLoopTests
    {
        [Fact]
        public void DataValidates()
        {
            Assert.Empty(TestData.Db.Validate());
        }

        [Fact]
        public void NewGameHasFullGridsCalendarsAndChampionships()
        {
            var db = TestData.Db;
            var world = WorldFactory.Create(db, new NewGameOptions { Seed = 3 });
            foreach (var cat in db.Categories.Values.Where(c => c.Simulated))
            {
                var rules = db.GetRules(cat.Id, 2026);
                var teams = world.TeamsInCategory(cat.Id);
                Assert.NotEmpty(teams);
                foreach (var t in teams)
                {
                    Assert.Equal(TransferMarket.Capacity(cat, rules, t), t.RiderIds.Count);
                    Assert.NotNull(world.GetBike(t.BikeId));
                }
                int seats = teams.Sum(t => t.RiderIds.Count);
                Assert.True(seats >= (cat.IsCup ? rules.MaxEntries : rules.GridSize), cat.Id + " grid too small: " + seats);
                Assert.True(world.Championships.ContainsKey(cat.Id));
            }
            Assert.True(world.Calendars.ContainsKey("grandprix"));
            Assert.True(world.Calendars.ContainsKey("superbike"));
            Assert.True(world.Riders.Values.Count(r => r.CategoryId == "junior_gp") > 20);
        }

        [Fact]
        public void EventsFollowTheCombinedCalendar()
        {
            var db = TestData.Db;
            var game = GameController.NewGame(db, new NewGameOptions { Seed = 4 });
            var first = game.PeekNextEvent();
            // WorldSBK opens at Phillip Island before the MotoGP season starts.
            Assert.Equal("superbike", first.Group);
            Assert.Contains("worldsbk", first.Categories);
            Assert.Contains("worldssp", first.Categories);
            var ev = game.BeginNextEvent();
            game.CompleteEvent(ev);
            var champ = game.World.Championships["worldsbk"];
            Assert.Single(champ.Rounds);
            // Race 1 + Superpole Race + Race 2
            Assert.Equal(3, champ.Rounds[0].Sessions.Count(s => s.Type == SessionType.Race || s.Type == SessionType.Sprint));
            Assert.True(champ.SortedRiders()[0].Points > 0);
            var next = game.PeekNextEvent();
            Assert.Equal("grandprix", next.Group);
            Assert.DoesNotContain("rookies", next.Categories);
        }

        [Fact]
        public void SameSeedSameResultsAndSaveRoundTrip()
        {
            var db = TestData.Db;
            var a = GameController.NewGame(db, new NewGameOptions { Seed = 11 });
            var b = GameController.NewGame(db, new NewGameOptions { Seed = 11 });
            for (int i = 0; i < 2; i++)
            {
                a.CompleteEvent(a.BeginNextEvent());
                b.CompleteEvent(b.BeginNextEvent());
            }
            string ja = SaveSystem.Serialize(a.World);
            Assert.Equal(ja, SaveSystem.Serialize(b.World));

            // Continue from a reloaded save: must match continuing the original.
            var c = new GameController(db, SaveSystem.Deserialize(ja));
            a.CompleteEvent(a.BeginNextEvent());
            c.CompleteEvent(c.BeginNextEvent());
            Assert.Equal(SaveSystem.Serialize(a.World), SaveSystem.Serialize(c.World));
        }

        [Fact]
        public void PlayerWeekendCanBeDrivenRaceByRace()
        {
            var db = TestData.Db;
            var world = WorldFactory.Create(db, new NewGameOptions { Seed = 8 });
            var team = world.TeamsInCategory("worldsbk").First();
            WorldFactory.SetPlayerTeam(world, team.Id);
            var game = new GameController(db, world);
            var ev = game.BeginNextEvent();
            Assert.NotNull(ev.PlayerWeekend);
            var weekend = ev.PlayerWeekend;
            int races = 0;
            while (!weekend.IsComplete)
            {
                if (weekend.CurrentIsRace)
                {
                    var sim = weekend.CreateRaceSimulation(db.Tuning.VisualTimeStep);
                    Assert.Contains(sim.Riders, r => r.Entry.IsPlayer && !r.Entry.AutoManaged);
                    while (!sim.IsFinished) sim.Step();
                    weekend.CompleteRace(sim);
                    races++;
                }
                else weekend.RunCurrentHeadless();
            }
            game.CompleteEvent(ev);
            Assert.Equal(3, races);
        }

        [Fact]
        public void OtherChampionshipsRunInTheBackgroundUntilThePlayersNextEvent()
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = 77, PlayerTeamId = "aspar_moto2" });
            var own = game.PeekNextEventFor("moto2");
            Assert.NotNull(own);
            Assert.Contains("moto2", own.Categories);
            game.SimulateUntilEventFor("moto2");
            var next = game.PeekNextEvent();
            Assert.Contains("moto2", next.Categories);
            Assert.Equal(own.Round.Date, next.Round.Date);
            // Calling it again does nothing: the next event is already ours.
            Assert.Equal(0, game.SimulateUntilEventFor("moto2"));
        }
    }
}
