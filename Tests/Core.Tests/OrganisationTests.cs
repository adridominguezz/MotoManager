using System;
using System.Linq;
using MotoManager.Core.Career;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace MotoManager.Tests
{
    public class OrganisationTests
    {
        private readonly ITestOutputHelper _out;

        public OrganisationTests(ITestOutputHelper output)
        {
            _out = output;
        }

        private static (GameController, ManagerProfile, Team) Owner(ulong seed, double wealth = 40e6)
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = seed });
            var w = game.World;
            var m = CareerService.CreatePlayer(w, game.Db, "Test", "Owner", "ES", "entrepreneur");
            m.PersonalWealth = wealth;
            var supplier = CareerService.Suppliers(w, "moto3").First();
            var res = CareerService.Found(w, game.Db, m, new FoundingPlan { CategoryId = "moto3", ManufacturerId = supplier.Id, TeamName = "Owner Racing", Investment = wealth });
            Assert.True(res.Ok, res.Message);
            return (game, m, res.Team);
        }

        [Fact]
        public void AnOrganisationOpensASecondTeamOnlyIfTheMoneyHolds()
        {
            var (game, m, first) = Owner(301);
            var w = game.World;
            var org = OrganisationService.PlayerOrganisation(w);
            var options = OrganisationService.ExpansionOptions(w, game.Db, org, 0);
            Assert.DoesNotContain(options, o => o.CategoryId == "moto3");
            foreach (var o in options) _out.WriteLine(o.CategoryId + " " + o.Verdict + " min cash " + o.MinCashProbable.ToString("0"));

            // An expansion that would drain the cash is refused.
            Accounts.Post(w, org, null, LedgerKind.Other, -30e6, "test", true, false);
            Assert.Equal(Viability.Unviable, OrganisationService.ExpansionOptions(w, game.Db, org, 0).First(o => o.CategoryId == "motogp").Verdict);
            var motogp = OrganisationService.Expand(game, new ExpansionPlan { CategoryId = "motogp", ManufacturerId = CareerService.Suppliers(w, "motogp").First().Id });
            Assert.False(motogp.Ok);
            Accounts.Post(w, org, null, LedgerKind.Other, 30e6, "test", true, false);

            var viable = options.FirstOrDefault(o => o.Verdict == Viability.Viable || o.Verdict == Viability.Risky);
            Assert.NotNull(viable);
            double cash = org.Cash;
            var res = OrganisationService.Expand(game, new ExpansionPlan { CategoryId = viable.CategoryId, ManufacturerId = CareerService.Suppliers(w, viable.CategoryId).First().Id, TeamName = "Owner Two" });
            Assert.True(res.Ok, res.Message);
            Assert.Equal(2, org.TeamIds.Count);
            Assert.True(org.Cash < cash);
            // The player runs the first team; a manager runs the new one, with a basic staff.
            Assert.Equal(first, w.PlayerTeam);
            var boss = w.GetManager(res.Team.ManagerId);
            Assert.NotNull(boss);
            Assert.False(boss.IsPlayer);
            Assert.NotEmpty(res.Team.StaffIds);
            // It gets a full entry at its first event and races.
            game.SimulateUntilEventFor(viable.CategoryId);
            game.CompleteEvent(game.BeginNextEvent());
            Assert.True(res.Team.RiderIds.Count > 0);
            // Not twice in the same category, and not during the season.
            Assert.False(OrganisationService.Expand(game, new ExpansionPlan { CategoryId = "worldssp", ManufacturerId = CareerService.Suppliers(w, "worldssp").First().Id }).Ok);
        }

        [Fact]
        public void ATeamMovesToAnotherCategoryAtTheEndOfTheSeason()
        {
            var (game, m, team) = Owner(302, 60e6);
            var w = game.World;
            var db = game.Db;
            var supplier = CareerService.Suppliers(w, "moto2").First();
            var q = OrganisationService.QuoteCategoryChange(w, db, team, "moto2");
            _out.WriteLine("move cost " + q.Net + " verdict " + q.Viability.Verdict);
            var req = OrganisationService.RequestCategoryChange(w, db, team, "moto2", supplier.Id);
            Assert.True(req.Ok, req.Message);
            int moto2Teams = w.TeamsInCategory("moto2").Count();
            int moto3Teams = w.TeamsInCategory("moto3").Count();
            game.SimulateRestOfSeason();
            var contracted = team.RiderIds.Select(w.GetRider).Where(r => r.Contract != null && r.Contract.EndSeason > w.Season).ToList();
            game.EndSeason();
            Assert.Equal("moto2", team.CategoryId);
            Assert.Equal(supplier.Id, team.ManufacturerId);
            Assert.Equal("moto2", w.GetBike(team.BikeId).CategoryId);
            Assert.Equal(moto2Teams, w.TeamsInCategory("moto2").Count());
            Assert.Equal(moto3Teams - 1, w.TeamsInCategory("moto3").Count());
            Assert.All(contracted, r => Assert.Equal("moto2", r.CategoryId));
            Assert.Null(team.PendingCategoryId);
        }

        [Fact]
        public void SellingTheOnlyTeamWindsUpTheOrganisationAndPaysTheOwner()
        {
            var (game, m, team) = Owner(303);
            var w = game.World;
            var org = OrganisationService.PlayerOrganisation(w);
            var value = OrganisationService.Value(w, game.Db, team);
            Assert.True(value.Total > 0);
            double wealth = m.PersonalWealth;
            double cash = org.Cash;
            double debt = org.Loans.Sum(l => l.Outstanding);
            var res = OrganisationService.SellTeam(game, team);
            Assert.True(res.Ok, res.Message);
            Assert.NotEqual(org.Id, team.OrganisationId);
            Assert.True(w.Teams.ContainsKey(team.Id), "the team keeps racing under its new owners");
            Assert.Null(w.PlayerTeam);
            Assert.Null(m.Job);
            Assert.NotNull(w.GetManager(team.ManagerId));
            Assert.Null(OrganisationService.PlayerOrganisation(w));
            Assert.Equal(wealth + cash + Math.Round(value.Total / 1000) * 1000 - debt, m.PersonalWealth, 0);
        }

        [Fact]
        public void AnOrganisationInTheRedIsForcedToSellAtTheEndOfTheSeason()
        {
            var (game, m, team) = Owner(304);
            var w = game.World;
            var org = OrganisationService.PlayerOrganisation(w);
            Accounts.Post(w, org, null, LedgerKind.Other, -(org.Cash + 2e6), "test loss", true, true);
            Assert.Equal(CashAlert.Insolvent, OrganisationService.Alert(w, game.Db, org));
            // An emergency loan is available once.
            Assert.True(OrganisationService.EmergencyLoanAmount(w, org) > 0);
            Assert.True(OrganisationService.TakeEmergencyLoan(w, org));
            Assert.False(OrganisationService.TakeEmergencyLoan(w, org));
            // ...but here the losses go on: the cash is still negative at the end of the season.
            game.SimulateRestOfSeason();
            if (org.Cash >= 0) Accounts.Post(w, org, null, LedgerKind.Other, -(org.Cash + 5e6), "test loss", true, true);
            double rep = m.Reputation;
            game.EndSeason();
            Assert.NotEqual(org.Id, team.OrganisationId);
            Assert.Null(w.PlayerTeam);
            Assert.True(m.Reputation < rep);
            Assert.Contains(w.News, n => n.Headline.Contains(team.Name));
        }
    }
}
