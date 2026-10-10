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
            // The new team starts empty (the owner hires); the seller's riders are free on the market.
            Assert.Empty(team.RiderIds);
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
            Assert.Equal(gridBefore, w.TeamsInCategory("moto3").Sum(t => t.RiderIds.Count));
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

        private static (GameController, ManagerProfile) Employed(ulong seed)
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = seed });
            var w = game.World;
            var m = CareerService.CreatePlayer(w, game.Db, "Test", "Manager", "ES", "team_staff");
            var offer = CareerService.JobOffers(w, game.Db, m).First();
            Assert.True(CareerService.AcceptJob(w, game.Db, m, offer).Ok);
            return (game, m);
        }

        [Fact]
        public void AContractThatIsNotRenewedEndsAndThePlayerIsLeftWithoutATeam()
        {
            var (game, m) = Employed(201);
            var w = game.World;
            m.Job.EndSeason = w.Season;
            var team = w.PlayerTeam;
            game.SimulateRestOfSeason();
            var summary = game.EndSeason();
            Assert.Equal(JobOutcome.Expired, summary.PlayerReview.Outcome);
            Assert.Null(m.Job);
            Assert.Null(w.PlayerTeam);
            Assert.False(team.IsPlayer);
            Assert.NotNull(w.GetManager(team.ManagerId));
            // The world carries on without the player's team, and offers can be taken.
            game.SimulateUntilEventFor("motogp");
            var offers = ManagerCareer.Offers(w, game.Db);
            if (offers.Count > 0)
            {
                Assert.True(ManagerCareer.ChangeJob(w, game.Db, offers[0]).Ok);
                Assert.NotNull(w.PlayerTeam);
            }
        }

        [Fact]
        public void HappyOwnersOfferARenewalInTheLastSeason()
        {
            var (game, m) = Employed(202);
            var w = game.World;
            m.Job.EndSeason = w.Season;
            m.Job.BoardConfidence = 80;
            var offer = ManagerCareer.RenewalOffer(w, game.Db);
            Assert.NotNull(offer);
            Assert.True(ManagerCareer.AcceptRenewal(w, game.Db));
            Assert.True(m.Job.EndSeason > w.Season);
            Assert.Null(ManagerCareer.RenewalOffer(w, game.Db));
            m.Job.BoardConfidence = 20;
            m.Job.EndSeason = w.Season;
            Assert.Null(ManagerCareer.RenewalOffer(w, game.Db));
        }

        [Fact]
        public void AManagerWhoLosesTheOwnersConfidenceIsSackedWithSeverance()
        {
            var (game, m) = Employed(203);
            var w = game.World;
            m.Job.EndSeason = w.Season + 2;
            m.Job.BoardConfidence = 0;
            var team = w.PlayerTeam;
            team.Personality.Objective = TeamObjective.Title; // almost impossible to meet
            double wealth = m.PersonalWealth;
            game.SimulateRestOfSeason();
            var review = game.EndSeason().PlayerReview;
            if (review.Confidence < ManagerCareer.SackBelow)
            {
                Assert.Equal(JobOutcome.Sacked, review.Outcome);
                Assert.Null(m.Job);
                Assert.True(m.PersonalWealth > wealth);
            }
        }

        [Fact]
        public void AFoundedTeamStartsEmptyAndGetsAFullEntryAtItsFirstEvent()
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = 204 });
            var w = game.World;
            var m = CareerService.CreatePlayer(w, game.Db, "Test", "Owner", "ES", "entrepreneur");
            m.PersonalWealth = 40e6;
            var supplier = CareerService.Suppliers(w, "moto3").First();
            var res = CareerService.Found(w, game.Db, m, new FoundingPlan { CategoryId = "moto3", ManufacturerId = supplier.Id, TeamName = "Empty Racing", Investment = 40e6, PrimaryColor = "#3366FF", SecondaryColor = "#FFCC00" });
            Assert.True(res.Ok, res.Message);
            Assert.Empty(res.Team.RiderIds);
            Assert.Empty(res.Team.StaffIds);
            Assert.Equal("#FFCC00", res.Team.Colors.Secondary);
            game.SimulateUntilEventFor("moto3");
            game.CompleteEvent(game.BeginNextEvent());
            Assert.Equal(2, res.Team.RiderIds.Count);
        }
    }
}
