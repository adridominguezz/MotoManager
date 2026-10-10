using System;
using System.Linq;
using MotoManager.Core.Career;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace MotoManager.Tests
{
    public class FinanceTests
    {
        private readonly ITestOutputHelper _out;

        public FinanceTests(ITestOutputHelper output)
        {
            _out = output;
        }

        [Fact]
        public void EveryTeamBelongsToOneOrganisationThatHoldsTheMoney()
        {
            var db = TestData.Db;
            Assert.Empty(db.Validate());
            var world = WorldFactory.Create(db, new NewGameOptions { Seed = 21 });
            foreach (var team in world.Teams.Values)
            {
                var org = world.OrganisationOf(team);
                Assert.NotNull(org);
                Assert.Contains(team.Id, org.TeamIds);
            }
            // Known multi-category structures share one organisation and one cash balance.
            Assert.Equal(world.Teams["intact_moto2"].OrganisationId, world.Teams["intact_moto3"].OrganisationId);
            Assert.Equal(world.Teams["marcvds_moto2"].OrganisationId, world.Teams["marcvds_sbk"].OrganisationId);
            var ducati = world.GetOrganisation("ducati_corse");
            Assert.Equal(OwnerKind.Manufacturer, ducati.Owner);
            Assert.Equal(ducati.TeamIds.Sum(id => world.Teams[id].SeasonBudget), ducati.Cash, 0);
            Assert.Equal(OwnerKind.Organiser, world.OrganisationOf(world.Teams["rookies_cup"]).Owner);
        }

        [Fact]
        public void LedgerExplainsEveryEuroOverASeasonAndSurvivesSaving()
        {
            var db = TestData.Db;
            var game = GameController.NewGame(db, new NewGameOptions { Seed = 22, PlayerTeamId = "trackhouse_motogp" });
            var w = game.World;
            while (!game.IsSeasonOver)
            {
                var ev = game.BeginNextEvent();
                game.CompleteEvent(ev);
            }
            foreach (var org in w.Organisations.Values)
                Assert.Equal(org.SeasonOpeningCash + Accounts.SeasonCashFlow(w, org.Id), org.Cash, 2);

            // Contract bonuses are paid: every team that won a race paid its winner.
            var champ = w.Championships["motogp"];
            var winners = champ.Rounds.SelectMany(r => r.Sessions).Where(s => s.Type == SessionType.Race)
                .Select(s => s.Classification.First(e => e.Position == 1)).ToList();
            foreach (var teamId in winners.Select(e => e.TeamId).Distinct())
            {
                var team = w.Teams[teamId];
                var totals = Accounts.TotalsFor(w, team.OrganisationId, w.Season, teamId);
                Assert.True(totals.ContainsKey(LedgerKind.RiderBonus) && totals[LedgerKind.RiderBonus].Cash < 0, teamId + " paid no bonus");
            }

            // Only the player's organisation keeps line-by-line detail.
            var player = w.PlayerTeam;
            Assert.NotEmpty(Accounts.Entries(w, player.OrganisationId, w.Season));
            Assert.Empty(Accounts.Entries(w, w.Teams["ducati_lenovo"].OrganisationId, w.Season));
            var t = Accounts.TotalsFor(w, player.OrganisationId, w.Season);
            foreach (var kv in t.OrderBy(k => k.Key)) _out.WriteLine(kv.Key.ToString().PadRight(20) + kv.Value.Cash.ToString("N0"));
            _out.WriteLine("spent " + Accounts.SpentThisSeason(w, player).ToString("N0") + " of budget " + player.SeasonBudget.ToString("N0"));

            var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(w));
            foreach (var org in w.Organisations.Values) Assert.Equal(org.Cash, loaded.Organisations[org.Id].Cash, 2);
            Assert.Equal(w.Finance.AppliedKeys.Count, loaded.Finance.AppliedKeys.Count);
            Assert.False(Accounts.TryBegin(loaded, "event|grandprix|0|motogp"), "an event already paid must not be paid again after loading");

            game.EndSeason();
            var summary = w.Finance.Summaries.Last(s => s.OrganisationId == player.OrganisationId);
            Assert.Equal(w.Season - 1, summary.Season);
            Assert.Equal(summary.ClosingCash, w.OrganisationOf(player).SeasonOpeningCash, 2);
            Assert.DoesNotContain(w.Finance.Entries, e => e.Season < w.Season - 1);
            Assert.All(w.Teams.Values.Where(x => db.GetCategory(x.CategoryId).Simulated), x => Assert.True(x.SeasonBudget > 0));
        }

        [Fact]
        public void OwnersCoverShortfallsButNotForThePlayersOwnOrganisation()
        {
            var db = TestData.Db;
            var world = WorldFactory.Create(db, new NewGameOptions { Seed = 23 });
            var ai = world.GetOrganisation("aspar_team");
            var own = world.OrganisationOf(world.Teams["lcr_motogp"]);
            own.Owner = OwnerKind.Player;
            Accounts.Post(world, ai, null, LedgerKind.Other, -ai.Cash - 1000000);
            Accounts.Post(world, own, null, LedgerKind.Other, -own.Cash - 1000000);
            SeasonEndProcessor.OwnerFunding(world, db);
            SeasonEndProcessor.OwnerFunding(world, db); // a second call must not pay twice
            Assert.True(ai.Cash > 0);
            Assert.Equal(-1000000, own.Cash, 2);
            var funding = Accounts.TotalsFor(world, ai.Id, world.Season)[LedgerKind.OwnerFunding].Cash;
            Assert.Equal(ai.Cash + 1000000, funding, 2);
        }
    }
}
