using System.Collections.Generic;
using System.Linq;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace MotoManager.Tests
{
    public class SponsorTests
    {
        private readonly ITestOutputHelper _out;

        public SponsorTests(ITestOutputHelper output)
        {
            _out = output;
        }

        private static (GameController, Team) Career(string teamId, ulong seed)
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = seed, PlayerTeamId = teamId });
            return (game, game.World.PlayerTeam);
        }

        private static SponsorOffer OfferFrom(WorldState world, GameDatabase db, Team team, string industry, double payment, double signing, string requiredCategory = null)
        {
            var template = SponsorService.AllTemplates(world, db).First(s => s.Generated && s.CategoryPreference.ContainsKey(team.CategoryId));
            var o = SponsorMarket.MakeOffer(world, db, team, db.GetCategory(team.CategoryId), template, world.Season, new DeterministicRandom(1), false);
            o.Industry = industry;
            o.PaymentPerSeason = payment;
            o.SigningBonus = signing;
            o.RequiredCategoryId = requiredCategory;
            o.SponsorId = "test_" + o.Id;
            world.SponsorOffers.Add(o);
            return o;
        }

        private static void FreeSlots(Team team)
        {
            team.Sponsors.Clear();
        }

        [Fact]
        public void AnOfferIsNotMoneyUntilSignedAndTheSigningBonusIsPaidOnce()
        {
            var (game, team) = Career("aspar_moto3", 41);
            var w = game.World;
            FreeSlots(team);
            double cash = Accounts.Cash(w, team);
            var offer = OfferFrom(w, game.Db, team, "Test industry", 400000, 50000);
            Assert.Equal(cash, Accounts.Cash(w, team));
            Assert.Equal(0, FinanceService.SponsorIncomePerSeason(w, team));

            Assert.True(SponsorMarket.Accept(w, game.Db, offer.Id).Ok);
            Assert.Equal(cash + 50000, Accounts.Cash(w, team), 2);
            Assert.False(SponsorMarket.Accept(w, game.Db, offer.Id).Ok);
            Assert.Equal(cash + 50000, Accounts.Cash(w, team), 2);
            Assert.Equal(400000, FinanceService.SponsorIncomePerSeason(w, team), 2);
        }

        [Fact]
        public void ConditionalSponsorshipPaysNothingUntilTheConditionHolds()
        {
            var (game, team) = Career("aspar_moto3", 42);
            var w = game.World;
            FreeSlots(team);
            double cash = Accounts.Cash(w, team);
            var offer = OfferFrom(w, game.Db, team, "Test industry", 900000, 100000, "moto2");
            Assert.True(SponsorMarket.Accept(w, game.Db, offer.Id).Ok);
            Assert.Equal(cash, Accounts.Cash(w, team), 2);
            Assert.Equal(0, FinanceService.SponsorIncomePerSeason(w, team));
            Assert.Equal(0, CostProfile.Of(w, team).SponsorIncome);
            var ev = game.BeginNextEvent();
            game.CompleteEvent(ev);
            var totals = Accounts.TotalsFor(w, team.OrganisationId, w.Season, team.Id);
            Assert.False(totals.ContainsKey(LedgerKind.Sponsorship));
        }

        [Fact]
        public void IndustryExclusivityAndSlotsAreEnforced()
        {
            var (game, team) = Career("aspar_moto3", 43);
            var w = game.World;
            FreeSlots(team);
            var a = OfferFrom(w, game.Db, team, "Energy drink", 300000, 0);
            var b = OfferFrom(w, game.Db, team, "Energy drink", 350000, 0);
            a.IndustryExclusive = true;
            Assert.True(SponsorMarket.Accept(w, game.Db, a.Id).Ok);
            var r = SponsorMarket.Accept(w, game.Db, b.Id);
            Assert.False(r.Ok);
            int slots = SponsorService.Slots(game.Db, team, w.Season);
            for (int i = 0; i < slots + 2; i++) SponsorMarket.Accept(w, game.Db, OfferFrom(w, game.Db, team, "Industry " + i, 100000, 0).Id);
            Assert.Equal(slots, SponsorService.DealsIn(team, w.Season));
        }

        [Fact]
        public void ObjectivesFitWhatTheTeamCanDo()
        {
            var db = TestData.Db;
            var world = WorldFactory.Create(db, new NewGameOptions { Seed = 44 });
            var template = new SponsorTemplate
            {
                Id = "probe", Name = "Probe", PaymentPerSeason = 1000000,
                Objectives = new List<SponsorObjective>
                {
                    new SponsorObjective { Type = SponsorObjectiveType.RaceFinishTopN, Target = 1, BonusPerOccurrence = 10000 },
                    new SponsorObjective { Type = SponsorObjectiveType.ChampionshipPositionTopN, Target = 1, SeasonBonus = 100000 }
                }
            };
            var top = world.Teams["ducati_lenovo"];
            var small = world.TeamsInCategory("motogp").OrderBy(t => TeamAI.ExpectedStrength(world, t)).First();
            var forTop = SponsorMarket.FitObjectives(world, db, top, template, 1000000);
            var forSmall = SponsorMarket.FitObjectives(world, db, small, template, 1000000);
            _out.WriteLine(small.Name + ": top " + forSmall[0].Target + ", champ " + forSmall[1].Target + " · " + top.Name + ": top " + forTop[0].Target + ", champ " + forTop[1].Target);
            Assert.True(forSmall[0].Target > forTop[0].Target);
            Assert.True(forSmall[1].Target > 3, "a modest team must not be asked to win the championship");
            Assert.True(forTop[1].Target <= 3);
        }

        [Fact]
        public void TheMarketKeepsTeamsSponsoredAndOffersExpire()
        {
            var (game, team) = Career("lcr_motogp", 45);
            var w = game.World;
            FreeSlots(team); // a team with full slots gets no approaches
            Assert.All(w.Sponsors.Values, s => Assert.True(s.Generated && s.DataConfidence.Contains("fictional")));
            int playerOffers = 0;
            var seen = new HashSet<string>();
            while (!game.IsSeasonOver)
            {
                var ev = game.BeginNextEvent();
                game.CompleteEvent(ev);
                foreach (var o in SponsorMarket.OffersFor(w, team)) if (seen.Add(o.Id)) playerOffers++;
                Assert.All(w.SponsorOffers, o => Assert.True(o.ExpiresAfterEvent >= w.EventsCompleted));
            }
            var cats = game.Db.Categories.Values.Where(c => c.Simulated && !c.IsCup).Select(c => c.Id).ToHashSet();
            var aiTeams = w.Teams.Values.Where(t => !t.IsPlayer && cats.Contains(t.CategoryId)).ToList();
            double avgDeals = aiTeams.Average(t => SponsorService.DealsIn(t, w.Season));
            _out.WriteLine("player offers " + playerOffers + ", AI deals per team " + avgDeals.ToString("0.0"));
            Assert.True(playerOffers >= 1);
            Assert.True(avgDeals >= 1.5);
            foreach (var t in w.Teams.Values)
            {
                var industries = t.Sponsors.Where(d => SponsorService.Pays(d, t, w.Season) && d.IndustryExclusive).GroupBy(d => d.Industry);
                Assert.All(industries, g => Assert.Single(g));
            }
        }

        [Fact]
        public void ASponsorCanBackSeveralTeamsButNeverTheSameTeamTwice()
        {
            var db = TestData.Db;
            var world = WorldFactory.Create(db, new NewGameOptions { Seed = 47 });
            var redbull = world.Teams.Values.Where(t => t.Sponsors.Any(d => d.SponsorId == "redbull")).Select(t => t.Id).ToList();
            _out.WriteLine("Red Bull backs: " + string.Join(", ", redbull));
            Assert.True(redbull.Count >= 3);
            Assert.True(redbull.Count <= SponsorService.MaxTeams(db.Sponsors["redbull"]));
            foreach (var t in world.Teams.Values)
                Assert.Equal(t.Sponsors.Count, t.Sponsors.Select(d => d.SponsorId).Distinct().Count());
            var c = SponsorService.Commitments(world);
            Assert.False(c.CanApproach(db.Sponsors["redbull"], redbull[0]));
        }

        [Fact]
        public void MissedObjectivesCostMoneyWhenTheDealSaysSo()
        {
            var (game, team) = Career("aspar_moto3", 46);
            var w = game.World;
            FreeSlots(team);
            var offer = OfferFrom(w, game.Db, team, "Test industry", 500000, 0);
            offer.Seasons = 2;
            offer.PenaltyOnMiss = 0.2;
            offer.Objectives = new List<SponsorObjective> { new SponsorObjective { Type = SponsorObjectiveType.ChampionshipPositionTopN, Target = 1, SeasonBonus = 100000 } };
            Assert.True(SponsorMarket.Accept(w, game.Db, offer.Id).Ok);
            var deal = team.Sponsors.Single();
            var cat = game.Db.GetCategory(team.CategoryId);
            double bonus = SponsorService.EndOfSeason(w, game.Db, team, cat, new SeasonOutcome { ChampionshipPosition = 9, Points = 50 }, new DeterministicRandom(3));
            Assert.Equal(0, bonus);
            Assert.Equal(400000, deal.PaymentPerSeason, 2);
            double met = SponsorService.EndOfSeason(w, game.Db, team, cat, new SeasonOutcome { ChampionshipPosition = 1, Points = 300 }, new DeterministicRandom(3));
            Assert.Equal(100000, met, 2);
        }
    }
}
