using System;
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
    public class MerchTests
    {
        private readonly ITestOutputHelper _out;

        public MerchTests(ITestOutputHelper output)
        {
            _out = output;
        }

        private static (GameController, Team) Career(string teamId, ulong seed = 51)
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = seed, PlayerTeamId = teamId });
            return (game, game.World.PlayerTeam);
        }

        private static void Event(WorldState w, GameDatabase db, Team team, int wins = 0, int podiums = 0, ulong seed = 1)
        {
            var rules = db.GetRules(team.CategoryId, w.Season);
            var o = new RaceOutcome { Wins = wins, Podiums = podiums };
            MerchService.AfterEvent(w, db, team, rules, 22, o, new DeterministicRandom(seed));
        }

        [Fact]
        public void FameSellsButWithLimitsAndPriceMatters()
        {
            var (game, team) = Career("aspar_moto3");
            var w = game.World;
            var db = game.Db;
            var shirt = db.Products["tshirt"];
            team.Shop.Fans = 2000;
            team.Shop.Hype = 0;
            double unknown = MerchService.ExpectedUnitsPerEvent(w, db, team, shirt, shirt.ReferencePrice);
            team.Shop.Fans = 400000;
            double famous = MerchService.ExpectedUnitsPerEvent(w, db, team, shirt, shirt.ReferencePrice);
            _out.WriteLine("unknown " + unknown.ToString("0.0") + " famous " + famous.ToString("0.0") + " per event");
            Assert.True(famous > unknown * 5, "a famous team must sell much more");
            Assert.True(famous < unknown * 40, "but not without limit");

            double atRef = MerchService.ExpectedUnitsPerEvent(w, db, team, shirt, shirt.ReferencePrice);
            double expensive = MerchService.ExpectedUnitsPerEvent(w, db, team, shirt, shirt.ReferencePrice * 2);
            double giveaway = MerchService.ExpectedUnitsPerEvent(w, db, team, shirt, shirt.ReferencePrice * 0.1);
            Assert.True(expensive < atRef / 3);
            Assert.True(giveaway <= atRef * MerchService.MaxPriceBoost + 1e-9);

            var table = MerchService.PriceTable(w, db, team, MerchService.AddProduct(w, db, team, "tshirt"));
            Assert.Contains(table, p => p.MarginPerUnit > 0);
            Assert.True(MerchService.MarginPerUnit(w, db, team, shirt, null, shirt.BaseUnitCost * 0.8) < 0, "selling below cost loses money on every unit");
            Assert.True(table.Select(p => p.UnitsPerEvent).Distinct().Count() == table.Count, "the forecast must change with the price");
        }

        [Fact]
        public void StockIsBoughtWithCashAndSalesBringRevenueAndCostOfSales()
        {
            var (game, team) = Career("aspar_moto3");
            var w = game.World;
            var db = game.Db;
            team.Shop.Items.Clear();
            var item = MerchService.AddProduct(w, db, team, "tshirt");
            double cash = Accounts.Cash(w, team);

            var tooBig = MerchService.PreviewOrder(w, db, team, item, (int)(cash / 5));
            Assert.False(tooBig.Affordable);
            Assert.False(MerchService.Order(w, db, team, item, (int)(cash / 5)));

            var preview = MerchService.PreviewOrder(w, db, team, item, 300);
            Assert.True(preview.Affordable);
            Assert.True(MerchService.Order(w, db, team, item, 300));
            Assert.Equal(cash - preview.TotalCost, Accounts.Cash(w, team), 2);
            Assert.Equal(300, item.Stock);
            Assert.Equal(preview.TotalCost, item.StockValue, 2);
            var totals = Accounts.TotalsFor(w, team.OrganisationId, w.Season, team.Id);
            Assert.Equal(0, totals[LedgerKind.MerchStock].Pnl, 2);

            // Demand bigger than stock: everything sells and the rest are lost sales.
            team.Shop.Fans = 300000;
            team.Shop.Hype = 1;
            Event(w, db, team);
            Assert.Equal(0, item.Stock);
            Assert.Equal(300, item.SoldSeason);
            Assert.True(item.LostSalesSeason > 0);
            totals = Accounts.TotalsFor(w, team.OrganisationId, w.Season, team.Id);
            Assert.Equal(item.RevenueSeason, totals[LedgerKind.MerchSales].Cash, 2);
            Assert.Equal(-preview.TotalCost, totals[LedgerKind.MerchCostOfSales].Pnl, 2);
            Assert.Equal(0, totals[LedgerKind.MerchCostOfSales].Cash, 2);
        }

        [Fact]
        public void UnsoldStockKeepsItsValueAndSurvivesSaving()
        {
            var (game, team) = Career("aspar_moto3");
            var w = game.World;
            var db = game.Db;
            team.Shop.Items.Clear();
            var item = MerchService.AddProduct(w, db, team, "jacket");
            MerchService.SetPrice(db, item, 1000); // clamped to the maximum and almost unsellable
            Assert.Equal(MerchService.MaxPrice(db.Products["jacket"]), item.Price);
            MerchService.Order(w, db, team, item, 200);
            double value = item.StockValue;
            Event(w, db, team);
            MerchService.SeasonEnd(w, db);
            Assert.True(item.Stock > 150);
            Assert.True(item.StockValue > 0 && item.StockValue <= value);
            Assert.Equal(item.Stock * item.AverageUnitCost, item.StockValue, 2);

            var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(w));
            var again = loaded.Teams[team.Id].Shop.Items.Single(i => i.Id == item.Id);
            Assert.Equal(item.Stock, again.Stock);
            Assert.Equal(item.StockValue, again.StockValue, 2);

            double cash = Accounts.Cash(w, team);
            double got = MerchService.Liquidate(w, db, team, item);
            Assert.True(got < value, "clearance recovers less than the stock cost");
            Assert.Equal(cash + got, Accounts.Cash(w, team), 2);
            Assert.Equal(0, item.Stock);
        }

        [Fact]
        public void AWinRaisesInterestForAWhile()
        {
            var (game, team) = Career("aspar_moto3");
            var w = game.World;
            var db = game.Db;
            var shirt = db.Products["tshirt"];
            team.Shop.Hype = 0;
            double before = MerchService.ExpectedUnitsPerEvent(w, db, team, shirt, shirt.ReferencePrice);
            Event(w, db, team, wins: 1, podiums: 1);
            double after = MerchService.ExpectedUnitsPerEvent(w, db, team, shirt, shirt.ReferencePrice);
            Assert.True(after > before * 1.15);
            for (int i = 0; i < 8; i++) Event(w, db, team, seed: (ulong)(i + 2));
            double later = MerchService.ExpectedUnitsPerEvent(w, db, team, shirt, shirt.ReferencePrice);
            Assert.True(later < after * 0.9, "the effect fades");
        }

        [Fact]
        public void SeasonOfShopsIsConsistentAcrossTeamsAndOrganisations()
        {
            var (game, team) = Career("aspar_moto3");
            var w = game.World;
            for (int i = 0; i < 6 && !game.IsSeasonOver; i++)
            {
                var ev = game.BeginNextEvent();
                game.CompleteEvent(ev);
            }
            var org = w.GetOrganisation("aspar_team");
            var orgTotals = Accounts.TotalsFor(w, org.Id, w.Season);
            double perTeam = org.TeamIds.Sum(id => Accounts.TotalsFor(w, org.Id, w.Season, id).TryGetValue(LedgerKind.MerchSales, out var t) ? t.Cash : 0);
            Assert.True(orgTotals.ContainsKey(LedgerKind.MerchSales));
            Assert.Equal(orgTotals[LedgerKind.MerchSales].Cash, perTeam, 2);
            foreach (var t in w.Teams.Values)
                foreach (var it in t.Shop.Items)
                {
                    Assert.True(it.Stock >= 0);
                    Assert.Equal(it.Stock == 0 ? 0 : it.StockValue, it.StockValue, 2);
                }
            var famous = w.Teams["ducati_lenovo"];
            var small = w.TeamsInCategory("motogp").OrderBy(x => x.Reputation).First();
            var sf = MerchService.Summary(w, famous);
            var ss = MerchService.Summary(w, small);
            _out.WriteLine(famous.Name + " merch revenue " + sf.Revenue.ToString("N0") + " · " + small.Name + " " + ss.Revenue.ToString("N0"));
            Assert.True(sf.Revenue > ss.Revenue);
            foreach (var o in w.Organisations.Values) Assert.Equal(o.SeasonOpeningCash + Accounts.SeasonCashFlow(w, o.Id), o.Cash, 2);
        }
    }
}
