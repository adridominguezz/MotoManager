using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Economy
{
    public class MerchSummary
    {
        public double Revenue;
        public double CostOfSales;
        public double GrossMargin { get { return Revenue - CostOfSales; } }
        public double Operations;
        public double Royalties;
        public double Net { get { return GrossMargin - Operations - Royalties; } }
        /// <summary>Money spent buying stock this season (cash, not an expense).</summary>
        public double StockPurchases;
        public double InventoryValue;
        public int UnitsSold;
        public int LostSales;
    }

    public class OrderPreview
    {
        public int Quantity;
        public double UnitCost;
        public double TotalCost;
        public int StockAfter;
        /// <summary>Units expected to sell in the rest of the season at the current price.</summary>
        public double ExpectedSales;
        public double ExpectedLeftover;
        public double CashAfter;
        public bool Affordable;
        /// <summary>Events of demand the stock will cover.</summary>
        public double CoverEvents;
    }

    public class PricePoint
    {
        public double Price;
        public double UnitsPerEvent;
        public double MarginPerUnit;
        public double ProfitPerEvent;
    }

    /// <summary>
    /// Team shops: catalogue, prices, stock and sales. Demand depends on the fan base (with diminishing returns),
    /// recent excitement, the product, the category's audience and price (each kind of buyer has its own sensitivity,
    /// and cheapness only helps up to a point). All money moves through <see cref="Accounts"/>: buying stock turns cash
    /// into inventory, a sale brings revenue and the cost of the units sold, unsold stock keeps its value.
    /// </summary>
    public static class MerchService
    {
        public const double ManufacturerRoyalty = 0.15;
        /// <summary>At most this multiple of normal demand however low the price.</summary>
        public const double MaxPriceBoost = 2.5;

        public static ProductTemplate Template(GameDatabase db, MerchItem item)
        {
            ProductTemplate t;
            return item != null && db.Products.TryGetValue(item.TemplateId ?? "", out t) ? t : null;
        }

        public static double MinPrice(ProductTemplate t) { return Math.Round(t.ReferencePrice * 0.4, 1); }
        public static double MaxPrice(ProductTemplate t) { return Math.Round(t.ReferencePrice * 2.5, 1); }

        public static bool HasShop(GameDatabase db, WorldState world, Team team)
        {
            var rules = db.GetRules(team.CategoryId, world.Season);
            return rules != null && rules.Economy.MerchAudience > 0;
        }

        /// <summary>Products the team may sell now (rider and event products come later).</summary>
        public static bool Available(GameDatabase db, Team team, ProductTemplate t)
        {
            if (t.Requires != ProductRequirement.None) return false;
            if (t.Identity == ProductIdentity.Rider) return false;
            if (t.Identity == ProductIdentity.Manufacturer) return team.Status == TeamStatus.Factory && team.ManufacturerId != null;
            return true;
        }

        public static List<ProductTemplate> Catalogue(GameDatabase db, Team team)
        {
            var list = new List<ProductTemplate>();
            foreach (var t in db.Products.Values) if (Available(db, team, t)) list.Add(t);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        public static MerchItem Find(Team team, string itemId)
        {
            foreach (var i in team.Shop.Items) if (i.Id == itemId) return i;
            return null;
        }

        // ------------------------------------------------------------------ demand

        public static double FansTarget(WorldState world, GameDatabase db, Team team)
        {
            var rules = db.GetRules(team.CategoryId, world.Season);
            double rep = team.Reputation / 60.0;
            return Math.Round(rules.Economy.FansReference * (0.15 + rep * rep * 0.85));
        }

        /// <summary>Fan base effect: about 1 for an average team, rising with diminishing returns towards 2.</summary>
        public static double FanFactor(double fans, double reference)
        {
            return 2 * fans / (fans + Math.Max(1, reference));
        }

        /// <summary>Demand multiplier of a price (casual buyers are the most price sensitive), capped for cheap prices.</summary>
        public static double PriceFactor(ProductSegment segment, double price, double reference)
        {
            double e = segment == ProductSegment.Casual ? 2.2 : segment == ProductSegment.Fan ? 1.6 : 1.0;
            double ratio = Math.Max(0.05, price / Math.Max(0.01, reference));
            return Math.Min(MaxPriceBoost, Math.Pow(ratio, -e));
        }

        /// <summary>Expected units per event at a given price (no randomness: used for forecasts).</summary>
        public static double ExpectedUnitsPerEvent(WorldState world, GameDatabase db, Team team, ProductTemplate t, double price)
        {
            var rules = db.GetRules(team.CategoryId, world.Season);
            double units = rules.Economy.MerchAudience * FanFactor(team.Shop.Fans, rules.Economy.FansReference) * t.Appeal *
                           (1 + team.Shop.Hype) * PriceFactor(t.Segment, price, t.ReferencePrice);
            if (t.Identity == ProductIdentity.Manufacturer)
            {
                var m = world.GetManufacturer(team.ManufacturerId);
                units *= m != null ? 0.6 + m.Reputation / 100.0 : 1;
            }
            return units;
        }

        /// <summary>Net margin per unit after fulfilment and royalties, at average stock cost (or the base cost if empty).</summary>
        public static double MarginPerUnit(WorldState world, GameDatabase db, Team team, ProductTemplate t, MerchItem item, double price)
        {
            var rules = db.GetRules(team.CategoryId, world.Season);
            double cost = item != null && item.Stock > 0 ? item.AverageUnitCost : t.BaseUnitCost;
            double royalty = t.Identity == ProductIdentity.Manufacturer ? ManufacturerRoyalty : 0;
            return price * (1 - rules.Economy.MerchFulfilmentShare - royalty) - cost;
        }

        /// <summary>Forecast at several prices: how demand, unit margin and profit per event move with the price.</summary>
        public static List<PricePoint> PriceTable(WorldState world, GameDatabase db, Team team, MerchItem item)
        {
            var t = Template(db, item);
            var list = new List<PricePoint>();
            if (t == null) return list;
            foreach (double f in new[] { 0.6, 0.8, 1.0, 1.25, 1.5, 2.0 })
            {
                double price = Math.Round(t.ReferencePrice * f, 1);
                double units = ExpectedUnitsPerEvent(world, db, team, t, price);
                double margin = MarginPerUnit(world, db, team, t, item, price);
                list.Add(new PricePoint { Price = price, UnitsPerEvent = units, MarginPerUnit = margin, ProfitPerEvent = units * margin });
            }
            return list;
        }

        public static int EventsLeft(WorldState world, GameDatabase db, Team team)
        {
            return CashflowForecast.RemainingRounds(world, db, team.CategoryId).Count;
        }

        // ------------------------------------------------------------------ actions (player and AI alike)

        public static MerchItem AddProduct(WorldState world, GameDatabase db, Team team, string templateId, double price = 0)
        {
            ProductTemplate t;
            if (!db.Products.TryGetValue(templateId ?? "", out t) || !Available(db, team, t) || !HasShop(db, world, team)) return null;
            foreach (var i in team.Shop.Items)
                if (i.TemplateId == templateId)
                {
                    i.Active = true;
                    return i;
                }
            var item = new MerchItem { Id = world.NewId("merch"), TemplateId = templateId, Price = price > 0 ? price : t.ReferencePrice };
            item.Price = MathX.Clamp(item.Price, MinPrice(t), MaxPrice(t));
            team.Shop.Items.Add(item);
            return item;
        }

        public static void SetPrice(GameDatabase db, MerchItem item, double price)
        {
            var t = Template(db, item);
            if (t != null) item.Price = Math.Round(MathX.Clamp(price, MinPrice(t), MaxPrice(t)), 2);
        }

        /// <summary>Stops selling (stock is kept and keeps its value).</summary>
        public static void Retire(MerchItem item) { item.Active = false; }

        /// <summary>Unit cost with volume discounts.</summary>
        public static double UnitCost(ProductTemplate t, int quantity)
        {
            double discount = quantity >= 5000 ? 0.2 : quantity >= 2000 ? 0.12 : quantity >= 500 ? 0.05 : 0;
            return Math.Round(t.BaseUnitCost * (1 - discount), 2);
        }

        public static OrderPreview PreviewOrder(WorldState world, GameDatabase db, Team team, MerchItem item, int quantity)
        {
            var t = Template(db, item);
            var p = new OrderPreview { Quantity = quantity };
            if (t == null) return p;
            quantity = Math.Max(quantity, t.MinOrder);
            p.Quantity = quantity;
            p.UnitCost = UnitCost(t, quantity);
            p.TotalCost = Math.Round(p.UnitCost * quantity, 2);
            p.StockAfter = item.Stock + quantity;
            double perEvent = ExpectedUnitsPerEvent(world, db, team, t, item.Price);
            p.ExpectedSales = Math.Min(p.StockAfter, perEvent * EventsLeft(world, db, team));
            p.ExpectedLeftover = Math.Max(0, p.StockAfter - p.ExpectedSales);
            p.CoverEvents = perEvent > 0 ? p.StockAfter / perEvent : double.PositiveInfinity;
            double cash = Accounts.Cash(world, team);
            p.CashAfter = cash - p.TotalCost;
            p.Affordable = p.CashAfter >= 0;
            return p;
        }

        /// <summary>Buys stock: cash becomes inventory (no expense until the units are sold).</summary>
        public static bool Order(WorldState world, GameDatabase db, Team team, MerchItem item, int quantity)
        {
            var p = PreviewOrder(world, db, team, item, quantity);
            var t = Template(db, item);
            if (t == null || !p.Affordable || p.Quantity <= 0) return false;
            Accounts.Post(world, team, LedgerKind.MerchStock, -p.TotalCost, p.Quantity + " x " + t.Type, true, false);
            item.Stock += p.Quantity;
            item.StockValue += p.TotalCost;
            return true;
        }

        /// <summary>Sells the whole stock to a wholesaler at a fraction of its cost (frees space, takes the loss).</summary>
        public static double Liquidate(WorldState world, GameDatabase db, Team team, MerchItem item, double shareOfCost = 0.3)
        {
            if (item.Stock <= 0) return 0;
            double cash = Math.Round(item.StockValue * shareOfCost, 2);
            Accounts.Post(world, team, LedgerKind.MerchSales, cash, "Clearance");
            Accounts.Post(world, team, LedgerKind.MerchCostOfSales, -item.StockValue, "Clearance", false, true);
            item.CostOfSalesSeason += item.StockValue;
            item.CostOfSalesTotal += item.StockValue;
            item.RevenueSeason += cash;
            item.RevenueTotal += cash;
            item.Stock = 0;
            item.StockValue = 0;
            return cash;
        }

        // ------------------------------------------------------------------ events

        /// <summary>
        /// After an event (posted with the event's money, so it can never run twice): excitement from the results,
        /// the fan base drifts, AI shops restock, and each product sells what demand and stock allow.
        /// </summary>
        public static void AfterEvent(WorldState world, GameDatabase db, Team team, CategoryRules rules, int roundsInSeason, RaceOutcome outcome, DeterministicRandom rng)
        {
            if (rules.Economy.MerchAudience <= 0) return;
            var shop = team.Shop;
            shop.Hype = Math.Min(1.5, shop.Hype * 0.8 + 0.25 * outcome.Wins + 0.08 * Math.Max(0, outcome.Podiums - outcome.Wins));
            double target = FansTarget(world, db, team);
            shop.Fans = Math.Round(shop.Fans + (target - shop.Fans) * 0.03 + shop.Fans * 0.004 * shop.Hype);

            if (!team.IsPlayer) AiManage(world, db, team);

            bool open = false;
            foreach (var item in shop.Items)
            {
                var t = Template(db, item);
                if (t == null) continue;
                if (item.Active || item.Stock > 0) open = true;
                if (!item.Active) continue;
                double expected = ExpectedUnitsPerEvent(world, db, team, t, item.Price);
                int demand = (int)Math.Round(expected * rng.Range(0.8, 1.2));
                int sold = Math.Min(demand, item.Stock);
                item.LostSalesSeason += demand - sold;
                item.SalesByEvent.Add(sold);
                if (sold <= 0) continue;
                double unitCost = item.AverageUnitCost;
                double revenue = Math.Round(sold * item.Price, 2);
                double cogs = Math.Round(sold * unitCost, 2);
                item.Stock -= sold;
                item.StockValue = item.Stock > 0 ? Math.Max(0, item.StockValue - cogs) : 0;
                item.SoldSeason += sold;
                item.SoldTotal += sold;
                item.RevenueSeason += revenue;
                item.RevenueTotal += revenue;
                item.CostOfSalesSeason += cogs;
                item.CostOfSalesTotal += cogs;
                Accounts.Post(world, team, LedgerKind.MerchSales, revenue);
                Accounts.Post(world, team, LedgerKind.MerchCostOfSales, -cogs, null, false, true);
                Accounts.Post(world, team, LedgerKind.MerchOperations, -revenue * rules.Economy.MerchFulfilmentShare);
                if (t.Identity == ProductIdentity.Manufacturer) Accounts.Post(world, team, LedgerKind.MerchRoyalty, -revenue * ManufacturerRoyalty);
            }
            if (open) Accounts.Post(world, team, LedgerKind.MerchOperations, -rules.Economy.MerchShopCostPerSeason / Math.Max(1, roundsInSeason));
        }

        /// <summary>Titles bring a lasting jump in interest; season counters restart (stock and its value stay).</summary>
        public static void SeasonEnd(WorldState world, GameDatabase db)
        {
            foreach (var rec in world.Champions)
            {
                if (rec.Season != world.Season) continue;
                var team = world.GetTeam(rec.TeamId);
                if (team == null) continue;
                team.Shop.Hype = Math.Min(1.5, team.Shop.Hype + 0.6);
                team.Shop.Fans = Math.Round(team.Shop.Fans * 1.1);
            }
            foreach (var team in world.Teams.Values)
                foreach (var item in team.Shop.Items)
                {
                    item.SoldSeason = 0;
                    item.LostSalesSeason = 0;
                    item.RevenueSeason = 0;
                    item.CostOfSalesSeason = 0;
                    item.SalesByEvent.Clear();
                }
        }

        /// <summary>
        /// The AI's shop policy: a few best-selling basics at their normal price, restocked to cover the next
        /// events when it can afford it. Same costs and rules as the player.
        /// </summary>
        public static void AiManage(WorldState world, GameDatabase db, Team team)
        {
            if (!HasShop(db, world, team)) return;
            var catalogue = Catalogue(db, team);
            catalogue.Sort((a, b) =>
            {
                int c = b.Appeal.CompareTo(a.Appeal);
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });
            int wanted = team.Reputation > 70 ? 4 : team.Reputation > 40 ? 3 : 2;
            int active = 0;
            foreach (var i in team.Shop.Items) if (i.Active) active++;
            for (int k = 0; k < catalogue.Count && active < wanted; k++)
            {
                bool have = false;
                foreach (var i in team.Shop.Items) if (i.TemplateId == catalogue[k].Id) have = true;
                if (have) continue;
                AddProduct(world, db, team, catalogue[k].Id);
                active++;
            }
            int left = Math.Max(1, EventsLeft(world, db, team));
            foreach (var item in team.Shop.Items)
            {
                var t = Template(db, item);
                if (t == null || !item.Active) continue;
                double perEvent = ExpectedUnitsPerEvent(world, db, team, t, item.Price);
                if (item.Stock >= perEvent * 2) continue;
                int qty = (int)Math.Ceiling(perEvent * Math.Min(6, left));
                if (qty < t.MinOrder / 2) continue;
                var p = PreviewOrder(world, db, team, item, qty);
                if (p.TotalCost < Accounts.Cash(world, team) * 0.05) Order(world, db, team, item, qty);
            }
        }

        /// <summary>Every shop at the start of the game: fan bases and the AI's basic range (the player's team too).</summary>
        public static void InitialShops(WorldState world, GameDatabase db)
        {
            var teams = new List<Team>(world.Teams.Values);
            teams.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var team in teams)
            {
                if (!HasShop(db, world, team)) continue;
                team.Shop.Fans = FansTarget(world, db, team);
                AiManage(world, db, team);
            }
        }

        // ------------------------------------------------------------------ reports

        public static MerchSummary Summary(WorldState world, Team team)
        {
            var s = new MerchSummary();
            var totals = Accounts.TotalsFor(world, team.OrganisationId, world.Season, team.Id);
            AccountTotal t;
            if (totals.TryGetValue(LedgerKind.MerchSales, out t)) s.Revenue = t.Pnl;
            if (totals.TryGetValue(LedgerKind.MerchCostOfSales, out t)) s.CostOfSales = -t.Pnl;
            if (totals.TryGetValue(LedgerKind.MerchOperations, out t)) s.Operations = -t.Pnl;
            if (totals.TryGetValue(LedgerKind.MerchRoyalty, out t)) s.Royalties = -t.Pnl;
            if (totals.TryGetValue(LedgerKind.MerchStock, out t)) s.StockPurchases = -t.Cash;
            foreach (var i in team.Shop.Items)
            {
                s.InventoryValue += i.StockValue;
                s.UnitsSold += i.SoldSeason;
                s.LostSales += i.LostSalesSeason;
            }
            return s;
        }

        /// <summary>Expected net merchandising result per event (probable income for forecasts, never confirmed).</summary>
        public static double ExpectedNetPerEvent(WorldState world, GameDatabase db, Team team)
        {
            var rules = db.GetRules(team.CategoryId, world.Season);
            if (rules == null || rules.Economy.MerchAudience <= 0) return 0;
            double net = 0;
            bool open = false;
            foreach (var item in team.Shop.Items)
            {
                var t = Template(db, item);
                if (t == null || !item.Active) continue;
                open = true;
                net += ExpectedUnitsPerEvent(world, db, team, t, item.Price) * MarginPerUnit(world, db, team, t, item, item.Price);
            }
            int rounds = Math.Max(1, Season.GameController.RoundsInSeason(world, db, team.CategoryId));
            if (open) net -= rules.Economy.MerchShopCostPerSeason / rounds;
            return net;
        }
    }
}
