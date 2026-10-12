using System;
using System.Collections.Generic;
using System.Text;
using MotoManager.Core.Common;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// The team's official shop: results of the season, the products on sale with their economics, and for the
    /// selected product a price forecast and stock orders with their consequences shown before confirming.
    /// </summary>
    public class ShopScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("shop.title"); } }
        private string _selected;
        private int _view;

        private static string Money(double v)
        {
            return Theme.Money(v);
        }

        private static string Name(ProductTemplate t)
        {
            return t != null ? Loc.T("product." + t.Type) : "?";
        }

        /// <summary>Product name with the rider or sponsor it carries.</summary>
        private static string Name(WorldState world, ProductTemplate t, string riderId, string sponsorId)
        {
            string n = Name(t);
            var r = riderId != null ? world.GetRider(riderId) : null;
            if (r != null) n += " · " + r.LastName;
            if (sponsorId != null)
            {
                var team = world.PlayerTeam;
                if (team != null) foreach (var d in team.Sponsors) if (d.SponsorId == sponsorId) n += " · " + d.Name;
            }
            return n;
        }

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var db = Game.Db;
            var team = world.PlayerTeam;
            if (team == null) return;
            if (!MerchService.HasShop(db, world, team))
            {
                var none = Ui.Card(list, Loc.T("shop.title"));
                Ui.Note(none, Loc.T("shop.none"));
                return;
            }
            var shop = team.Shop;

            // A product being managed takes the whole page.
            var selected = MerchService.Find(team, _selected);
            if (selected != null)
            {
                UiFactory.ButtonRow(list, 56, UiFactory.Act("◄  " + Loc.T("shop.back"), () => { _selected = null; Shell.Refresh(); }));
                BuildDetail(list, world, db, team, selected);
                return;
            }

            var add = MerchService.Catalogue(world, db, team);
            Ui.Chips(list, new[] { Loc.T("shop.view.summary"), Loc.T("shop.view.products", shop.Items.Count), Loc.T("shop.view.catalogue", add.Count), Loc.T("shop.view.campaigns", shop.Campaigns.Count) }, _view, i => { _view = i; Shell.Refresh(); });
            if (_view == 1) Products(list, world, db, team);
            else if (_view == 2)
            {
                var cat = Ui.Card(list, null);
                Catalogue(cat, world, db, team, add);
                Ui.Note(cat, Loc.T("shop.note"));
            }
            else if (_view == 3) Campaigns(list, world, db, team);
            else Summary(list, world, team);
        }

        private void Summary(RectTransform list, WorldState world, Team team)
        {
            var shop = team.Shop;
            var s = MerchService.Summary(world, team);
            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);
            var summary = Ui.Card(leftCol, Loc.T("shop.season", world.Season));
            Ui.Kpis(summary,
                Ui.Kpi(shop.Fans.ToString("N0", Loc.Culture), Loc.T("shop.fans")),
                Ui.Kpi(Money(s.Revenue), Loc.T("shop.revenue")),
                Ui.Kpi(Money(s.Net), Loc.T("shop.net")));
            if (shop.Hype > 0.05) Ui.Note(summary, Theme.WarnText(Loc.T("shop.hype", (shop.Hype * 100).ToString("0"))));
            if (MerchService.HasMarketing(world, team)) Ui.Note(summary, Theme.GoodText(Loc.T("shop.marketingRuns", (MerchService.MarketingBoost(world, team) * 100).ToString("0"))));
            else Ui.Note(summary, Loc.T("shop.marketingHint"));
            var c = Ui.Card(rightCol, Loc.T("shop.account"));
            {
                Ui.Line(c, Loc.T("shop.revenue"), Money(s.Revenue));
                Ui.Line(c, Loc.T("shop.cogs"), Money(-s.CostOfSales));
                Ui.Line(c, Loc.T("shop.gross"), Money(s.GrossMargin), true);
                Ui.Line(c, Loc.T("shop.operations"), Money(-s.Operations));
                if (s.Royalties > 0) Ui.Line(c, Loc.T("shop.royalties"), Money(-s.Royalties));
                if (s.ImageRights > 0) Ui.Line(c, Loc.T("shop.imageRights"), Money(-s.ImageRights));
                if (s.Marketing > 0) Ui.Line(c, Loc.T("shop.marketing"), Money(-s.Marketing));
                Ui.Line(c, Loc.T("shop.net"), Money(s.Net), true);
                Ui.Line(c, Loc.T("shop.stockSpend"), Money(-s.StockPurchases));
                Ui.Line(c, Loc.T("shop.inventory"), MathX.FormatMoney(s.InventoryValue));
                Ui.Line(c, Loc.T("shop.units"), s.UnitsSold.ToString("N0", Loc.Culture) + (s.LostSales > 0 ? "  " + Theme.BadText(Loc.T("shop.lost", s.LostSales.ToString("N0", Loc.Culture))) : ""));
            }
        }

        private void Products(RectTransform list, WorldState world, Core.Data.GameDatabase db, Team team)
        {
            var shop = team.Shop;
            // Products on sale, most profitable first.
            var items = new List<MerchItem>(shop.Items);
            items.Sort((a, b) => (b.RevenueSeason - b.CostOfSalesSeason).CompareTo(a.RevenueSeason - a.CostOfSalesSeason));
            var productsCard = Ui.Card(list, null);
            Ui.Note(productsCard, Loc.T("shop.productsInfo"));
            var pw = new[] { 3f, 1.4f, 1.4f, 1.6f, 1.2f, 1.2f, 1.8f, 1.5f, 1.6f };
            UiFactory.TableRow(productsCard, 38, pw, new[] { Loc.T("shop.col.product"), Loc.T("shop.col.price"), Loc.T("shop.col.cost"), Loc.T("shop.col.margin"), Loc.T("shop.col.stock"),
                Loc.T("shop.col.sold"), Loc.T("shop.col.profit"), Loc.T("shop.col.demand"), "" }, true);
            foreach (var item in items)
            {
                var t = MerchService.Template(db, item);
                if (t == null) continue;
                var it = item;
                double cost = item.Stock > 0 ? item.AverageUnitCost : t.BaseUnitCost;
                double margin = MerchService.MarginPerUnit(world, db, team, t, item, item.Price);
                double demand = MerchService.ExpectedUnitsPerEvent(world, db, team, t, item.Price, item.RiderId);
                var row = UiFactory.TableRow(productsCard, 44, pw, new[]
                {
                    Name(world, t, item.RiderId, item.SponsorId) + (item.Active ? "" : " <color=#9AA4B2>(" + Loc.T("shop.retired") + ")</color>"),
                    MathX.FormatMoney(item.Price), MathX.FormatMoney(cost), Money(margin), item.Stock.ToString("N0", Loc.Culture),
                    item.SoldSeason.ToString("N0", Loc.Culture), Money(item.RevenueSeason - item.CostOfSalesSeason), demand.ToString("0", Loc.Culture), ""
                }, false, _selected == item.Id ? new Color(0.94f, 0.7f, 0.16f, 0.16f) : (Color?)null);
                var b = UiFactory.Button("Select", row, Loc.T("shop.manage"), () => { _selected = it.Id; Shell.Refresh(); }, Theme.PanelLight, Theme.FontSmall - 4);
                UiFactory.InColumn(b, pw, pw.Length - 1);
            }

        }

        private void Catalogue(Transform list, WorldState world, Core.Data.GameDatabase db, Team team, List<ProductOption> add)
        {
            var cw = new[] { 3.4f, 1.4f, 1.4f, 1.4f, 2.2f, 1.6f };
            UiFactory.TableRow(list, 38, cw, new[] { Loc.T("shop.col.product"), Loc.T("shop.col.refPrice"), Loc.T("shop.col.cost"), Loc.T("shop.col.demand"), Loc.T("shop.col.terms"), "" }, true);
            foreach (var o in add)
            {
                var opt = o;
                var t = o.Template;
                var row = UiFactory.TableRow(list, 44, cw, new[] { Name(world, t, o.RiderId, o.SponsorId),
                    MathX.FormatMoney(t.ReferencePrice), MathX.FormatMoney(t.BaseUnitCost), MerchService.ExpectedUnitsPerEvent(world, db, team, t, t.ReferencePrice, o.RiderId).ToString("0", Loc.Culture), Terms(world, t, o.RiderId), "" });
                var b = UiFactory.Button("Add", row, Loc.T("shop.add"), () =>
                {
                    var it = MerchService.AddProduct(world, db, team, opt.Template.Id, 0, opt.RiderId, opt.SponsorId);
                    if (it != null) _selected = it.Id;
                    Shell.Refresh();
                }, Theme.Accent, Theme.FontSmall - 4);
                UiFactory.InColumn(b, cw, cw.Length - 1);
            }
            // Special products not available yet, with what unlocks them.
            var seen = new HashSet<string>();
            foreach (var o in MerchService.Offerings(world, db, team))
            {
                if (o.Lock == ProductLock.None || !seen.Add(o.Template.Id)) continue;
                UiFactory.TableRow(list, 40, cw, new[] { "<color=#9AA4B2>" + Name(o.Template) + "</color>", "", "", "", "<color=#9AA4B2>" + Loc.T("shop.lock." + o.Lock) + "</color>", "" });
            }
        }

        private static string Terms(WorldState world, ProductTemplate t, string riderId)
        {
            if (t.Identity == ProductIdentity.Manufacturer) return Loc.T("shop.royalty");
            if (t.Identity == ProductIdentity.Rider)
            {
                var r = world.GetRider(riderId);
                return Loc.T("shop.terms.rider", r != null && r.Contract != null ? (r.Contract.ImageRightsShare * 100).ToString("0") : "-");
            }
            if (t.Identity == ProductIdentity.Sponsor) return Loc.T("shop.terms.sponsor", (MerchService.SponsorProductionShare * 100).ToString("0"));
            if (t.Requires == ProductRequirement.Win) return Loc.T("shop.terms.edition", t.EditionEvents);
            if (t.Requires == ProductRequirement.Title) return Loc.T("shop.terms.title");
            return "";
        }

        private void Campaigns(RectTransform list, WorldState world, Core.Data.GameDatabase db, Team team)
        {
            var card = Ui.Card(list, Loc.T("shop.campaigns"), Loc.T("shop.campaignsInfo"));
            var cw = new[] { 3f, 1.6f, 1.4f, 1.4f, 1.8f, 1.8f, 1.8f };
            UiFactory.TableRow(card, 38, cw, new[] { Loc.T("shop.col.campaign"), Loc.T("shop.col.total"), Loc.T("shop.col.boost"), Loc.T("shop.col.events"), Loc.T("shop.col.extraProfit"), Loc.T("shop.col.result"), "" }, true);
            foreach (CampaignType type in Enum.GetValues(typeof(CampaignType)))
            {
                var ty = type;
                var f = MerchService.ForecastCampaign(world, db, team, type);
                string status = "";
                foreach (var c in team.Shop.Campaigns) if (c.Type == type) status = Loc.T("shop.campaignRunning", c.EventsLeft);
                var row = UiFactory.TableRow(card, 46, cw, new[]
                {
                    "<b>" + Loc.T("shop.campaign." + type) + "</b>" + (status.Length > 0 ? "\n" + Theme.GoodText(status) : ""),
                    MathX.FormatMoney(f.Cost), "+" + (f.Boost * 100).ToString("0") + "%", f.Events.ToString(), Money(f.ExtraProfit), Money(f.Net), ""
                });
                if (!f.Running && f.Events > 0)
                {
                    var b = UiFactory.Button("Launch", row, Loc.T("shop.launch"), () =>
                    {
                        bool ok = MerchService.LaunchCampaign(world, db, team, ty);
                        Shell.Toast(ok ? Loc.T("shop.launched") : Loc.T("shop.cannotAfford"), ok);
                        Shell.Refresh();
                    }, f.Affordable ? (f.Net >= 0 ? Theme.Accent : Theme.PanelLight) : Theme.PanelLight, Theme.FontSmall - 4);
                    UiFactory.InColumn(b, cw, cw.Length - 1);
                }
            }
            Ui.Note(card, Loc.T("shop.campaignsNote"));
        }

        private void BuildDetail(Transform page, WorldState world, Core.Data.GameDatabase db, Team team, MerchItem item)
        {
            var t = MerchService.Template(db, item);
            RectTransform leftCol, rightCol;
            Ui.Columns(page, out leftCol, out rightCol);
            var list = Ui.Card(leftCol, Name(world, t, item.RiderId, item.SponsorId), Loc.T("shop.priceRange", MathX.FormatMoney(MerchService.MinPrice(t)), MathX.FormatMoney(MerchService.MaxPrice(t))));

            UiFactory.ButtonRow(list, 50,
                UiFactory.Act("-10%", () => { MerchService.SetPrice(db, item, item.Price * 0.9); Shell.Refresh(); }),
                UiFactory.Act("-5%", () => { MerchService.SetPrice(db, item, item.Price * 0.95); Shell.Refresh(); }),
                UiFactory.Act("+5%", () => { MerchService.SetPrice(db, item, item.Price * 1.05); Shell.Refresh(); }),
                UiFactory.Act("+10%", () => { MerchService.SetPrice(db, item, item.Price * 1.1); Shell.Refresh(); }),
                UiFactory.Act(item.Active ? Loc.T("shop.retire") : Loc.T("shop.reactivate"), () => { item.Active = !item.Active && MerchService.StillAvailable(world, db, team, item); Shell.Refresh(); }));

            // Price forecast (an estimate, not a promise).
            var tw = new[] { 2f, 2f, 2f, 2.4f, 1.6f };
            UiFactory.TableRow(list, 36, tw, new[] { Loc.T("shop.col.price"), Loc.T("shop.col.demand"), Loc.T("shop.col.margin"), Loc.T("shop.col.profitEvent"), "" }, true);
            foreach (var p in MerchService.PriceTable(world, db, team, item))
            {
                var pp = p;
                bool current = Math.Abs(p.Price - item.Price) < 0.01;
                var row = UiFactory.TableRow(list, 40, tw, new[] { MathX.FormatMoney(p.Price) + (current ? " ◄" : ""), p.UnitsPerEvent.ToString("0", Loc.Culture), Money(p.MarginPerUnit), Money(p.ProfitPerEvent), "" });
                var b = UiFactory.Button("Use", row, Loc.T("shop.use"), () => { MerchService.SetPrice(db, item, pp.Price); Shell.Refresh(); }, Theme.PanelLight, Theme.FontSmall - 4);
                UiFactory.InColumn(b, tw, tw.Length - 1);
            }

            list = Ui.Card(rightCol, Loc.T("shop.order"));
            // Sales by event this season.
            if (item.SalesByEvent.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var n in item.SalesByEvent) sb.Append(n).Append("  ");
                UiFactory.Paragraph(list, "<color=#9AA4B2>" + Loc.T("shop.byEvent") + "</color> " + sb, Theme.FontSmall);
            }

            // Stock orders: consequences shown before confirming.
            int left = MerchService.EventsLeft(world, db, team);
            double perEvent = MerchService.ExpectedUnitsPerEvent(world, db, team, t, item.Price, item.RiderId);
            string terms = Terms(world, t, item.RiderId);
            if (terms.Length > 0) Ui.Note(list, terms);
            if (!item.Active && !MerchService.StillAvailable(world, db, team, item)) Ui.Note(list, Theme.WarnText(Loc.T("shop.offSale")));
            int shared = MerchService.SharedVolume(world, team, t.Id);
            if (shared > 0) Ui.Note(list, Loc.T("shop.shared", shared.ToString("N0", Loc.Culture)));
            var sizes = new List<int> { t.MinOrder, (int)Math.Ceiling(perEvent * 4), (int)Math.Ceiling(perEvent * Math.Max(1, left)) };
            sizes.Sort();
            var ow = new[] { 1.6f, 1.6f, 2f, 1.6f, 1.6f, 2f, 1.6f };
            UiFactory.TableRow(list, 36, ow, new[] { Loc.T("shop.col.units"), Loc.T("shop.col.unitCost"), Loc.T("shop.col.total"), Loc.T("shop.col.cover"), Loc.T("shop.col.leftover"), Loc.T("shop.col.cashAfter"), "" }, true);
            int last = -1;
            foreach (int q in sizes)
            {
                if (q <= 0 || q == last) continue;
                last = q;
                var p = MerchService.PreviewOrder(world, db, team, item, q);
                var row = UiFactory.TableRow(list, 42, ow, new[]
                {
                    p.Quantity.ToString("N0", Loc.Culture), MathX.FormatMoney(p.UnitCost), MathX.FormatMoney(p.TotalCost),
                    double.IsInfinity(p.CoverEvents) ? "-" : Loc.T("shop.events", p.CoverEvents.ToString("0", Loc.Culture)),
                    (p.ExpectedLeftover > p.Quantity * 0.3 ? "<color=#F0B428>" : "") + p.ExpectedLeftover.ToString("N0", Loc.Culture) + (p.ExpectedLeftover > p.Quantity * 0.3 ? "</color>" : ""),
                    Money(p.CashAfter), ""
                });
                int qty = p.Quantity;
                var b = UiFactory.Button("Order", row, Loc.T("shop.confirm"), () =>
                {
                    Shell.Toast(MerchService.Order(world, db, team, item, qty) ? Loc.T("shop.ordered", qty) : Loc.T("shop.cannotAfford"));
                    Shell.Refresh();
                }, p.Affordable ? Theme.Accent : Theme.PanelLight, Theme.FontSmall - 4);
                UiFactory.InColumn(b, ow, ow.Length - 1);
            }
            if (item.Stock > 0)
                UiFactory.ButtonRow(list, 50, UiFactory.Act(Loc.T("shop.liquidate", MathX.FormatMoney(item.StockValue * 0.3)), () =>
                {
                    MerchService.Liquidate(world, db, team, item);
                    Shell.Refresh();
                }));
        }
    }
}
