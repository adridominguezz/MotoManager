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
    public class CareerTests
    {
        private readonly ITestOutputHelper _out;

        public CareerTests(ITestOutputHelper output)
        {
            _out = output;
        }

        private static GameController Fresh(ulong seed)
        {
            return GameController.NewGame(TestData.Db, new NewGameOptions { Seed = seed });
        }

        private static void RunUntilRaced(GameController game, string categoryId)
        {
            while (true)
            {
                var ev = game.BeginNextEvent();
                game.CompleteEvent(ev);
                if (ev.Weekends.ContainsKey(categoryId)) return;
            }
        }

        [Fact]
        public void EveryTeamHasAnAiManagerAndANewcomerFindsWorkInTheSmallerCategories()
        {
            var game = Fresh(71);
            var w = game.World;
            Assert.All(w.Teams.Values, t => Assert.NotNull(w.GetManager(t.ManagerId)));
            var m = CareerService.CreatePlayer(w, game.Db, "Ana", "García", "ES", "team_staff");
            var offers = CareerService.JobOffers(w, game.Db, m);
            foreach (var o in offers) _out.WriteLine(w.Teams[o.TeamId].Name + " (" + o.CategoryId + ") salary " + o.SalaryPerSeason + " " + o.Autonomy);
            Assert.NotEmpty(offers);
            Assert.DoesNotContain(offers, o => o.CategoryId == "motogp" && w.Teams[o.TeamId].Status == TeamStatus.Factory);
            var famous = CareerService.CreatePlayer(w, game.Db, "Big", "Name", "IT", "former_rider");
            famous.Reputation = 90;
            var better = CareerService.JobOffers(w, game.Db, famous);
            Assert.True(offers.Count <= CareerService.MaxOffers);
            Assert.True(better.Max(o => w.Teams[o.TeamId].Reputation) > offers.Max(o => w.Teams[o.TeamId].Reputation), "a famous manager is wanted by bigger teams");
        }

        [Fact]
        public void AnEmployedManagerRunsTheTeamButDoesNotOwnIt()
        {
            var game = Fresh(72);
            var w = game.World;
            var m = CareerService.CreatePlayer(w, game.Db, "Ana", "García", "ES", "team_staff");
            var offer = CareerService.JobOffers(w, game.Db, m).Last();
            var team = w.Teams[offer.TeamId];
            var previous = w.GetManager(team.ManagerId);
            var org = w.OrganisationOf(team);
            var owner = org.Owner;
            Assert.True(CareerService.AcceptJob(w, game.Db, m, offer).Ok);
            Assert.Equal(team, w.PlayerTeam);
            Assert.Equal(m.Id, team.ManagerId);
            Assert.Null(previous.Job);
            Assert.Equal(owner, org.Owner);
            Assert.NotEqual(OwnerKind.Player, org.Owner);
            Assert.Null(org.OwnerManagerId);

            double wealth = m.PersonalWealth;
            RunUntilRaced(game, team.CategoryId);
            int rounds = game.RoundsInSeason(team.CategoryId);
            Assert.Equal(wealth + Math.Round(offer.SalaryPerSeason / rounds, 2), m.PersonalWealth, 2);
            var totals = Accounts.TotalsFor(w, org.Id, w.Season, team.Id);
            Assert.Equal(-Math.Round(offer.SalaryPerSeason / rounds, 2), totals[LedgerKind.ManagerSalary].Cash, 2);
        }

        [Fact]
        public void WithoutCapitalNoTeamCanBeFounded()
        {
            var game = Fresh(73);
            var w = game.World;
            var m = CareerService.CreatePlayer(w, game.Db, "Ana", "García", "ES", "team_staff");
            var options = CareerService.FoundingOptions(w, game.Db, m, m.PersonalWealth, CareerService.LoanTermsFor(m, m.PersonalWealth).MaxAmount);
            Assert.All(options, o => Assert.True(o.Verdict == Viability.Unviable || o.Verdict == Viability.NotAvailable));
            var r = CareerService.Found(w, game.Db, m, new FoundingPlan { CategoryId = "moto3", ManufacturerId = "ktm", TeamName = "Dream Team", Investment = m.PersonalWealth });
            Assert.False(r.Ok);
            Assert.Null(w.PlayerTeam);
        }

        [Fact]
        public void AnEntrepreneurFoundsATeamOnlyWhereTheMoneyHolds()
        {
            var game = Fresh(74);
            var w = game.World;
            var db = game.Db;
            var m = CareerService.CreatePlayer(w, db, "Ana", "García", "ES", "entrepreneur");
            CareerService.FoundingSponsorOffers(w, db, m, new MotoManager.Core.Common.DeterministicRandom(5));
            double invest = m.PersonalWealth;
            double loan = CareerService.LoanTermsFor(m, invest).MaxAmount;
            var options = CareerService.FoundingOptions(w, db, m, invest, loan);
            foreach (var o in options)
                _out.WriteLine(o.CategoryId.PadRight(9) + o.Verdict.ToString().PadRight(13) + " start-up " + (o.StartupTotal / 1e6).ToString("0.0") + "M  min cash " + (o.MinCashConfirmed / 1e6).ToString("0.0") + "M");
            Assert.Equal(Viability.Unviable, options.Single(o => o.CategoryId == "motogp").Verdict);
            var viable = options.First(o => o.Verdict == Viability.Viable && o.CategoryId == "moto3");

            int gridBefore = w.TeamsInCategory("moto3").Sum(t => t.RiderIds.Count);
            int teamsBefore = w.TeamsInCategory("moto3").Count;
            var r = CareerService.Found(w, db, m, new FoundingPlan { CategoryId = "moto3", ManufacturerId = "ktm", TeamName = "García Racing", Investment = 6000000, Loan = 1000000 });
            Assert.True(r.Ok, r.Message);
            var team = r.Team;
            var org = w.OrganisationOf(team);
            Assert.Equal(OwnerKind.Player, org.Owner);
            Assert.Equal(m.Id, org.OwnerManagerId);
            Assert.Equal(team, w.PlayerTeam);
            Assert.Equal(teamsBefore, w.TeamsInCategory("moto3").Count);
            Assert.Equal(gridBefore, w.TeamsInCategory("moto3").Sum(t => t.RiderIds.Count));
            Assert.Equal(18000000 - 6000000, m.PersonalWealth, 2);
            Assert.Equal(org.SeasonOpeningCash + Accounts.SeasonCashFlow(w, org.Id), org.Cash, 2);
            var totals = Accounts.TotalsFor(w, org.Id, w.Season);
            Assert.Equal(-db.GetRules("moto3", 2026).Economy.SlotPrice, totals[LedgerKind.SlotPurchase].Cash, 2);
            Assert.True(org.Cash < 7000000);
            Assert.Single(org.Loans);
            Assert.DoesNotContain(w.SponsorOffers, o => o.TeamId == CareerService.FoundingProbeId);
            Assert.All(team.Sponsors, d => Assert.True(SponsorService.Pays(d, team, w.Season)));

            // The season runs with the new team; loans are serviced, no owner bail-out.
            double outstanding = org.Loans[0].Outstanding;
            RunUntilRaced(game, "moto3");
            Assert.True(org.Loans[0].Outstanding < outstanding);
            Assert.True(Accounts.TotalsFor(w, org.Id, w.Season)[LedgerKind.Interest].Cash < 0);
            while (!game.IsSeasonOver)
            {
                var ev = game.BeginNextEvent();
                game.CompleteEvent(ev);
            }
            Assert.True(w.Championships["moto3"].Teams.ContainsKey(team.Id));
            game.EndSeason();
            Assert.False(Accounts.TotalsFor(w, org.Id, w.Season - 1).ContainsKey(LedgerKind.OwnerFunding));
            var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(w));
            Assert.Equal(m.PersonalWealth, loaded.PlayerManager.PersonalWealth, 2);
            Assert.Equal(org.Loans.Count, loaded.Organisations[org.Id].Loans.Count);
        }
    }
}
