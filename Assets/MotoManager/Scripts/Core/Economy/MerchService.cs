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
        /// <summary>Riders' share of their products.</summary>
        public double ImageRights;
        public double Marketing;
        public double Net { get { return GrossMargin - Operations - Royalties - ImageRights - Marketing; } }
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
        /// <summary>Units of the same product already ordered this season by the organisation's teams (count for the discount).</summary>
        public int SharedVolume;
        /// <summary>Part of the production paid by the sponsor of a co-branded product.</summary>
        public double SponsorContribution;
    }

    /// <summary>Why a special product cannot be sold now.</summary>
    public enum ProductLock
    {
        None,
        NoImageRights,
        NoMainSponsor,
        NeedsWin,
        NeedsTitle,
        WorksTeamOnly
    }

    /// <summary>A product the team could sell: the template, for whom (rider, sponsor) and whether it is unlocked.</summary>
    public class ProductOption
    {
        public ProductTemplate Template;
        public string RiderId;
        public string SponsorId;
        public ProductLock Lock;
    }

    public class CampaignForecast
    {
        public CampaignType Type;
        public double Cost;
        public double Boost;
        public int Events;
        public double ExtraUnits;
        public double ExtraProfit;
        public double Net { get { return ExtraProfit - Cost; } }
        public bool Running;
        public bool Affordable;
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

        /// <summary>Share of production a sponsor pays for a co-branded product.</summary>
        public const double SponsorProductionShare = 0.3;

        /// <summary>Products the team could sell now, and the special ones it cannot sell yet (with the reason).</summary>
        public static List<ProductOption> Offerings(WorldState world, GameDatabase db, Team team)
        {
            var list = new List<ProductOption>();
            var templates = new List<ProductTemplate>(db.Products.Values);
            templates.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var t in templates)
            {
                if (t.Identity == ProductIdentity.Rider)
                {
                    bool any = false;
                    foreach (var id in team.RiderIds)
                    {
                        var r = world.GetRider(id);
                        if (r == null || r.Contract == null || !r.Contract.ImageRights) continue;
                        any = true;
                        list.Add(new ProductOption { Template = t, RiderId = id });
                    }
                    if (!any) list.Add(new ProductOption { Template = t, Lock = ProductLock.NoImageRights });
                    continue;
                }
                if (t.Identity == ProductIdentity.Sponsor)
                {
                    bool any = false;
                    foreach (var d in team.Sponsors)
                    {
                        if (!MainSponsor(world, team, d)) continue;
                        any = true;
                        list.Add(new ProductOption { Template = t, SponsorId = d.SponsorId });
                    }
                    if (!any) list.Add(new ProductOption { Template = t, Lock = ProductLock.NoMainSponsor });
                    continue;
                }
                list.Add(new ProductOption { Template = t, Lock = LockOf(world, team, t) });
            }
            return list;
        }

        private static ProductLock LockOf(WorldState world, Team team, ProductTemplate t)
        {
            if (t.Identity == ProductIdentity.Manufacturer && (team.Status != TeamStatus.Factory || team.ManufacturerId == null)) return ProductLock.WorksTeamOnly;
            if (t.Requires == ProductRequirement.Win && !(team.Shop.EventsSinceWin >= 0 && (t.EditionEvents <= 0 || team.Shop.EventsSinceWin < t.EditionEvents))) return ProductLock.NeedsWin;
            if (t.Requires == ProductRequirement.Title && !TitleHolder(world, team)) return ProductLock.NeedsTitle;
            return ProductLock.None;
        }

        /// <summary>A sponsor big enough to put its name on products (title or main sponsor, paying now).</summary>
        public static bool MainSponsor(WorldState world, Team team, SponsorDeal d)
        {
            return d != null && d.Tier <= 2 && SponsorService.Pays(d, team, world.Season);
        }

        /// <summary>The team, or one of its riders, won a title last season: champion editions sell all this season.</summary>
        public static bool TitleHolder(WorldState world, Team team)
        {
            foreach (var rec in world.Champions)
            {
                if (rec.Season != world.Season - 1) continue;
                if (rec.TeamId == team.Id || rec.TeamChampionId == team.Id || (rec.RiderId != null && team.RiderIds.Contains(rec.RiderId))) return true;
            }
            return false;
        }

        /// <summary>Whether a product in the shop may still be sold (a rider who left, a sponsor gone, an edition that ran out).</summary>
        public static bool StillAvailable(WorldState world, GameDatabase db, Team team, MerchItem item)
        {
            var t = Template(db, item);
            if (t == null) return false;
            if (t.Identity == ProductIdentity.Rider)
            {
                var r = world.GetRider(item.RiderId);
                return r != null && team.RiderIds.Contains(r.Id) && r.Contract != null && r.Contract.ImageRights;
            }
            if (t.Identity == ProductIdentity.Sponsor)
            {
                foreach (var d in team.Sponsors) if (d.SponsorId == item.SponsorId && MainSponsor(world, team, d)) return true;
                return false;
            }
            return LockOf(world, team, t) == ProductLock.None;
        }

        /// <summary>Unlocked products the team does not sell yet.</summary>
        public static List<ProductOption> Catalogue(WorldState world, GameDatabase db, Team team)
        {
            var list = new List<ProductOption>();
            foreach (var o in Offerings(world, db, team))
                if (o.Lock == ProductLock.None && FindSame(team, o.Template.Id, o.RiderId, o.SponsorId) == null) list.Add(o);
            return list;
        }

        public static MerchItem FindSame(Team team, string templateId, string riderId, string sponsorId)
        {
            foreach (var i in team.Shop.Items)
                if (i.TemplateId == templateId && i.RiderId == riderId && i.SponsorId == sponsorId) return i;
            return null;
        }

        /// <summary>How much a rider sells: stars sell several times more than an unknown name.</summary>
        public static double RiderAppeal(Rider r)
        {
            if (r == null) return 0;
            return 0.2 + 1.3 * Math.Pow(MathX.Clamp(r.Reputation, 0, 100) / 100.0, 1.5);
        }

        /// <summary>Riders' share of the revenue of a product (0 if it is not a rider product).</summary>
        public static double ImageRightsShare(WorldState world, MerchItem item)
        {
            if (item == null || item.RiderId == null) return 0;
            var r = world.GetRider(item.RiderId);
            return r != null && r.Contract != null ? r.Contract.ImageRightsShare : 0;
        }

        /// <summary>Extra demand from the campaigns running now.</summary>
        public static double CampaignBoost(Team team)
        {
            double b = 0;
            foreach (var c in team.Shop.Campaigns) if (c.EventsLeft > 0) b += c.Boost;
            return b;
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
        public static double ExpectedUnitsPerEvent(WorldState world, GameDatabase db, Team team, ProductTemplate t, double price, string riderId = null)
        {
            var rules = db.GetRules(team.CategoryId, world.Season);
            double units = rules.Economy.MerchAudience * FanFactor(team.Shop.Fans, rules.Economy.FansReference) * t.Appeal *
                           (1 + team.Shop.Hype) * (1 + CampaignBoost(team)) * PriceFactor(t.Segment, price, t.ReferencePrice);
            if (t.Identity == ProductIdentity.Rider) units *= riderId != null ? RiderAppeal(world.GetRider(riderId)) : 1;
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
            double cost = item != null && item.Stock > 0 ? item.AverageUnitCost : t.BaseUnitCost * (t.Identity == ProductIdentity.Sponsor ? 1 - SponsorProductionShare : 1);
            double royalty = (t.Identity == ProductIdentity.Manufacturer ? ManufacturerRoyalty : 0) + ImageRightsShare(world, item);
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
                double units = ExpectedUnitsPerEvent(world, db, team, t, price, item.RiderId);
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

        public static MerchItem AddProduct(WorldState world, GameDatabase db, Team team, string templateId, double price = 0, string riderId = null, string sponsorId = null)
        {
            ProductTemplate t;
            if (!db.Products.TryGetValue(templateId ?? "", out t) || !HasShop(db, world, team)) return null;
            ProductOption option = null;
            // Without a rider or sponsor given, the first one that fits.
            foreach (var o in Offerings(world, db, team))
                if (option == null && o.Template.Id == templateId && o.Lock == ProductLock.None && (riderId == null || o.RiderId == riderId) && (sponsorId == null || o.SponsorId == sponsorId))
                    option = o;
            if (option == null) return null;
            riderId = option.RiderId;
            sponsorId = option.SponsorId;
            var existing = FindSame(team, templateId, riderId, sponsorId);
            if (existing != null)
            {
                existing.Active = true;
                return existing;
            }
            var item = new MerchItem { Id = world.NewId("merch"), TemplateId = templateId, RiderId = riderId, SponsorId = sponsorId, Price = price > 0 ? price : t.ReferencePrice };
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
        /// <summary>
        /// Unit cost with volume discounts. Units the organisation's other teams already ordered this season count for
        /// the discount (shared production), but each team only pays for its own units.
        /// </summary>
        public static double UnitCost(ProductTemplate t, int quantity, int sharedVolume = 0)
        {
            int volume = quantity + Math.Max(0, sharedVolume);
            double discount = volume >= 5000 ? 0.2 : volume >= 2000 ? 0.12 : volume >= 500 ? 0.05 : 0;
            return Math.Round(t.BaseUnitCost * (1 - discount), 2);
        }

        public static int SharedVolume(WorldState world, Team team, string templateId)
        {
            var org = world.OrganisationOf(team);
            int n;
            return org != null && org.MerchOrdered.TryGetValue(templateId ?? "", out n) ? n : 0;
        }

        public static OrderPreview PreviewOrder(WorldState world, GameDatabase db, Team team, MerchItem item, int quantity)
        {
            var t = Template(db, item);
            var p = new OrderPreview { Quantity = quantity };
            if (t == null) return p;
            quantity = Math.Max(quantity, t.MinOrder);
            p.Quantity = quantity;
            p.SharedVolume = SharedVolume(world, team, t.Id);
            double unit = UnitCost(t, quantity, p.SharedVolume);
            if (t.Identity == ProductIdentity.Sponsor)
            {
                p.SponsorContribution = Math.Round(unit * SponsorProductionShare * quantity, 2);
                unit = Math.Round(unit * (1 - SponsorProductionShare), 2);
            }
            p.UnitCost = unit;
            p.TotalCost = Math.Round(p.UnitCost * quantity, 2);
            p.StockAfter = item.Stock + quantity;
            double perEvent = ExpectedUnitsPerEvent(world, db, team, t, item.Price, item.RiderId);
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
            var org = world.OrganisationOf(team);
            if (org != null) org.MerchOrdered[t.Id] = SharedVolume(world, team, t.Id) + p.Quantity;
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

        // ------------------------------------------------------------------ campaigns

        /// <summary>
        /// Base cost (for a MotoGP-sized audience; smaller categories pay less), extra demand and duration in events.
        /// Estimated values.
        /// </summary>
        public static void CampaignSpec(CampaignType type, out double baseCost, out double boost, out int events)
        {
            switch (type)
            {
                case CampaignType.FanEvent: baseCost = 150000; boost = 0.4; events = 2; break;
                case CampaignType.RetailPartner: baseCost = 260000; boost = 0.2; events = 6; break;
                default: baseCost = 60000; boost = 0.25; events = 3; break;
            }
        }

        /// <summary>
        /// What a campaign would cost and bring: extra units of the products on sale at their current price (stock
        /// permitting) over its events, against its cost. A campaign can lose money.
        /// </summary>
        public static CampaignForecast ForecastCampaign(WorldState world, GameDatabase db, Team team, CampaignType type)
        {
            double baseCost, boost;
            int events;
            CampaignSpec(type, out baseCost, out boost, out events);
            var rules = db.GetRules(team.CategoryId, world.Season);
            double scale = Math.Pow(Math.Max(0, rules.Economy.MerchAudience) / 1000.0, 0.7);
            var f = new CampaignForecast { Type = type, Boost = boost, Events = Math.Min(events, Math.Max(0, EventsLeft(world, db, team))), Cost = Math.Round(baseCost * scale / 1000) * 1000 };
            foreach (var c in team.Shop.Campaigns) if (c.Type == type && c.EventsLeft > 0) f.Running = true;
            double running = 1 + CampaignBoost(team);
            foreach (var item in team.Shop.Items)
            {
                var t = Template(db, item);
                if (t == null || !item.Active) continue;
                double perEvent = ExpectedUnitsPerEvent(world, db, team, t, item.Price, item.RiderId);
                double extra = perEvent / running * boost * f.Events;
                // Only units in stock can be sold (with what normal demand will already take).
                extra = Math.Max(0, Math.Min(extra, item.Stock - perEvent * f.Events));
                f.ExtraUnits += extra;
                f.ExtraProfit += extra * MarginPerUnit(world, db, team, t, item, item.Price);
            }
            f.Affordable = Accounts.Cash(world, team) >= f.Cost;
            return f;
        }

        /// <summary>Pays and starts a campaign (one of each kind at a time; a fan event also wins some fans).</summary>
        public static bool LaunchCampaign(WorldState world, GameDatabase db, Team team, CampaignType type)
        {
            var f = ForecastCampaign(world, db, team, type);
            if (f.Running || !f.Affordable || f.Events <= 0 || !HasShop(db, world, team)) return false;
            Accounts.Post(world, team, LedgerKind.MerchMarketing, -f.Cost, type.ToString());
            team.Shop.Campaigns.Add(new MerchCampaign { Type = type, Cost = f.Cost, Boost = f.Boost, EventsLeft = f.Events });
            if (type == CampaignType.FanEvent) team.Shop.Fans = Math.Round(team.Shop.Fans * 1.02);
            return true;
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
            if (outcome.Wins > 0) shop.EventsSinceWin = 0;
            else if (shop.EventsSinceWin >= 0) shop.EventsSinceWin++;
            // Products that can no longer be sold (rider gone, sponsor gone, edition over) leave the shop; stock stays.
            foreach (var item in shop.Items)
                if (item.Active && !StillAvailable(world, db, team, item)) item.Active = false;
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
                double expected = ExpectedUnitsPerEvent(world, db, team, t, item.Price, item.RiderId);
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
                double rights = ImageRightsShare(world, item);
                if (rights > 0) Accounts.Post(world, team, LedgerKind.RiderImageRights, -Math.Round(revenue * rights, 2), world.GetRider(item.RiderId).FullName);
                if (t.Identity == ProductIdentity.Sponsor)
                    foreach (var d in team.Sponsors)
                        if (d.SponsorId == item.SponsorId) d.Satisfaction = Math.Min(100, d.Satisfaction + Math.Min(2, sold / 250.0));
            }
            foreach (var c in shop.Campaigns) c.EventsLeft--;
            shop.Campaigns.RemoveAll(c => c.EventsLeft <= 0);
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
            foreach (var org in world.Organisations.Values) org.MerchOrdered.Clear();
            foreach (var team in world.Teams.Values) team.Shop.Campaigns.Clear();
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
            // Best sellers first: a star rider's products and fresh special editions sell more than the basics.
            var catalogue = Catalogue(world, db, team);
            Func<ProductOption, double> score = o => o.Template.Appeal * (o.RiderId != null ? RiderAppeal(world.GetRider(o.RiderId)) : 1) *
                                                     (o.Template.Requires == ProductRequirement.Win || o.Template.Requires == ProductRequirement.Title ? 2 : 1);
            catalogue.Sort((a, b) =>
            {
                int c = score(b).CompareTo(score(a));
                return c != 0 ? c : string.CompareOrdinal(a.Template.Id + a.RiderId + a.SponsorId, b.Template.Id + b.RiderId + b.SponsorId);
            });
            int wanted = team.Reputation > 70 ? 4 : team.Reputation > 40 ? 3 : 2;
            int active = 0;
            foreach (var i in team.Shop.Items) if (i.Active) active++;
            for (int k = 0; k < catalogue.Count; k++)
            {
                var o = catalogue[k];
                bool special = o.Template.Requires == ProductRequirement.Win || o.Template.Requires == ProductRequirement.Title || (o.RiderId != null && score(o) >= 0.5);
                if (active >= wanted && !(special && active < wanted + 2)) continue;
                if (score(o) < 0.1) continue;
                if (AddProduct(world, db, team, o.Template.Id, 0, o.RiderId, o.SponsorId) != null) active++;
            }
            int left = Math.Max(1, EventsLeft(world, db, team));
            foreach (var item in team.Shop.Items)
            {
                var t = Template(db, item);
                if (t == null || !item.Active) continue;
                double perEvent = ExpectedUnitsPerEvent(world, db, team, t, item.Price, item.RiderId);
                if (item.Stock >= perEvent * 2) continue;
                int qty = (int)Math.Ceiling(perEvent * Math.Min(6, left));
                if (qty < t.MinOrder / 2) continue;
                var p = PreviewOrder(world, db, team, item, qty);
                if (p.TotalCost < Accounts.Cash(world, team) * 0.05) Order(world, db, team, item, qty);
            }
            // A campaign only when the forecast says it pays and the money is clearly there.
            if (team.Reputation > 50 && Accounts.Cash(world, team) > 2e6)
            {
                var f = ForecastCampaign(world, db, team, CampaignType.Social);
                if (!f.Running && f.Net > f.Cost * 0.3) LaunchCampaign(world, db, team, CampaignType.Social);
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
            if (totals.TryGetValue(LedgerKind.RiderImageRights, out t)) s.ImageRights = -t.Pnl;
            if (totals.TryGetValue(LedgerKind.MerchMarketing, out t)) s.Marketing = -t.Pnl;
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
                net += ExpectedUnitsPerEvent(world, db, team, t, item.Price, item.RiderId) * MarginPerUnit(world, db, team, t, item, item.Price);
            }
            int rounds = Math.Max(1, Season.GameController.RoundsInSeason(world, db, team.CategoryId));
            if (open) net -= rules.Economy.MerchShopCostPerSeason / rounds;
            return net;
        }
    }
}
