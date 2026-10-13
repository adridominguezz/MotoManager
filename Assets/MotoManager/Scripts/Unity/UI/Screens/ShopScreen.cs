using System;
using System.Collections.Generic;
using System.Text;
using MotoManager.Core.Common;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// The team's official shop (UX spec §3.15): summary, products, catalogue and campaigns as one segmented row;
    /// lists of at most four columns where the row is tapped (or carries one secondary button); a product's page
    /// with its figures, a price stepper and the price forecast / stock orders, every explanation behind an "i".
    /// </summary>
    public class ShopScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("shop.title"); } }
        private string _selected;
        private int _view;
        private int _detailView;

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

        private static string Small(string s)
        {
            return "<size=" + Theme.TextMin + ">" + s + "</size>";
        }

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var db = Game.Db;
            var team = world.PlayerTeam;
            if (team == null) return;
            if (!MerchService.HasShop(db, world, team))
            {
                var none = Ui.Card(Page(content), Loc.T("shop.title"), null, ScreenKit.Info(Loc.T("shop.title"), Loc.T("shop.none")));
                Ui.Empty(none, null, Loc.T("ux.shop.none"));
                return;
            }
            var shop = team.Shop;

            // A product being managed takes the whole page.
            var selected = MerchService.Find(team, _selected);
            if (selected != null)
            {
                BuildDetail(content, world, db, team, selected);
                return;
            }
            _selected = null;

            var list = Page(content);
            var add = MerchService.Catalogue(world, db, team);
            Ui.Segmented(list, new[] { Loc.T("shop.view.summary"), Loc.T("shop.view.products", shop.Items.Count), Loc.T("shop.view.catalogue", add.Count),
                Loc.T("shop.view.campaigns", shop.Campaigns.Count) }, _view, i => { _view = i; Shell.Refresh(); });
            if (_view == 1) Products(list, world, db, team);
            else if (_view == 2) Catalogue(list, world, db, team, add);
            else if (_view == 3) Campaigns(list, world, db, team);
            else Summary(list, world, team);
        }

        // ------------------------------------------------------------------ summary

        private void Summary(RectTransform list, WorldState world, Team team)
        {
            var shop = team.Shop;
            var s = MerchService.Summary(world, team);
            var tiles = Ui.Columns12(UiFactory.Row(list, 140), 4, 4, 4);
            Ui.StatTile(tiles[0], Loc.T("shop.fans"), shop.Fans.ToString("N0", Loc.Culture));
            Ui.StatTile(tiles[1], Loc.T("shop.revenue"), MathX.FormatMoney(s.Revenue), Loc.T("shop.season", world.Season));
            Ui.StatTile(tiles[2], Loc.T("shop.net"), MathX.FormatMoney(s.Net), null, null, s.Net < 0 ? Theme.Bad : (Color?)null);

            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);
            var c = Ui.Card(leftCol, Loc.T("shop.account"), Loc.T("shop.season", world.Season));
            Ui.Line(c, Loc.T("shop.revenue"), Money(s.Revenue));
            Ui.Line(c, Loc.T("shop.cogs"), Money(-s.CostOfSales));
            Ui.Line(c, Loc.T("shop.gross"), Money(s.GrossMargin), true);
            Ui.Line(c, Loc.T("shop.operations"), Money(-s.Operations));
            if (s.Royalties > 0) Ui.Line(c, Loc.T("shop.royalties"), Money(-s.Royalties));
            if (s.ImageRights > 0) Ui.Line(c, Loc.T("shop.imageRights"), Money(-s.ImageRights));
            if (s.Marketing > 0) Ui.Line(c, Loc.T("shop.marketing"), Money(-s.Marketing));
            Ui.Line(c, Loc.T("shop.net"), Money(s.Net), true);

            bool marketing = MerchService.HasMarketing(world, team);
            var sales = Ui.Card(rightCol, Loc.T("ux.shop.sales"), null, ScreenKit.Info(Loc.T("ux.shop.sales"),
                marketing ? Loc.T("shop.marketingRuns", (MerchService.MarketingBoost(world, team) * 100).ToString("0")) : Loc.T("shop.marketingHint"),
                Loc.T("shop.note")));
            Ui.Line(sales, Loc.T("shop.units"), s.UnitsSold.ToString("N0", Loc.Culture));
            if (s.LostSales > 0) Ui.Line(sales, Loc.T("ux.shop.lost"), Theme.BadText(s.LostSales.ToString("N0", Loc.Culture)));
            Ui.Line(sales, Loc.T("ux.shop.stockSpend"), Money(-s.StockPurchases));
            Ui.Line(sales, Loc.T("ux.shop.inventory"), MathX.FormatMoney(s.InventoryValue));
            if (shop.Hype > 0.05 || marketing)
            {
                var pills = ScreenKit.PillRow(sales);
                if (shop.Hype > 0.05) ScreenKit.Tag(pills, Loc.T("ux.shop.hype", (shop.Hype * 100).ToString("0")), Theme.Good);
                if (marketing) ScreenKit.Tag(pills, Loc.T("ux.shop.marketingOn", (MerchService.MarketingBoost(world, team) * 100).ToString("0")), Theme.Good);
            }
        }

        // ------------------------------------------------------------------ products

        private void Products(RectTransform list, WorldState world, Core.Data.GameDatabase db, Team team)
        {
            var shop = team.Shop;
            // Products on sale, most profitable first.
            var items = new List<MerchItem>(shop.Items);
            items.Sort((a, b) => (b.RevenueSeason - b.CostOfSalesSeason).CompareTo(a.RevenueSeason - a.CostOfSalesSeason));
            Ui.Section(list, Loc.T("shop.products"), null, ScreenKit.Info(Loc.T("shop.products"), Loc.T("ux.shop.productsInfo"), Loc.T("shop.note")));
            if (items.Count == 0)
            {
                Ui.Empty(list, null, Loc.T("ux.shop.noProducts"), UiFactory.Act(Loc.T("shop.view.catalogue", MerchService.Catalogue(world, db, team).Count), () => { _view = 2; Shell.Refresh(); }));
                return;
            }
            var w = new[] { 3.6f, 1.4f, 1.2f, 1.6f };
            ScreenKit.Captions(list, w, new[] { Loc.T("shop.col.product"), Loc.T("shop.col.price"), Loc.T("shop.col.stock"), Loc.T("ux.shop.profit") }, false, true);
            int n = 0;
            foreach (var item in items)
            {
                var t = MerchService.Template(db, item);
                if (t == null) continue;
                var it = item;
                string name = "<b>" + Name(world, t, item.RiderId, item.SponsorId) + "</b>" + (item.Active ? "" : "  " + Small(Theme.Caps(Loc.T("shop.retired"))));
                ScreenKit.DataRow(list, w, new[]
                {
                    name, MathX.FormatMoney(item.Price), item.Stock.ToString("N0", Loc.Culture), Money(item.RevenueSeason - item.CostOfSalesSeason)
                }, n++, () => { _selected = it.Id; Shell.Refresh(); }, null, !item.Active);
            }
        }

        // ------------------------------------------------------------------ catalogue

        private void Catalogue(RectTransform list, WorldState world, Core.Data.GameDatabase db, Team team, List<ProductOption> add)
        {
            Ui.Section(list, Loc.T("shop.catalogue"), null, ScreenKit.Info(Loc.T("shop.catalogue"), Loc.T("ux.shop.catalogueInfo"), Loc.T("shop.note")));
            var w = new[] { 3.6f, 1.6f, 1.6f };
            if (add.Count == 0) Ui.Empty(list, null, Loc.T("ux.shop.noCatalogue"));
            else ScreenKit.Captions(list, w, new[] { Loc.T("shop.col.product"), Loc.T("shop.col.refPrice"), Loc.T("shop.col.demand") }, true);
            int n = 0;
            foreach (var o in add)
            {
                var opt = o;
                var t = o.Template;
                double demand = MerchService.ExpectedUnitsPerEvent(world, db, team, t, t.ReferencePrice, o.RiderId);
                Action addIt = () =>
                {
                    var it = MerchService.AddProduct(world, db, team, opt.Template.Id, 0, opt.RiderId, opt.SponsorId);
                    if (it != null) _selected = it.Id;
                    Shell.Refresh();
                };
                string name = Name(world, t, o.RiderId, o.SponsorId);
                ScreenKit.DataRow(list, w, new[] { "<b>" + name + "</b>", MathX.FormatMoney(t.ReferencePrice), demand.ToString("0", Loc.Culture) }, n++,
                    () => Ui.Sheet(name, sheet =>
                    {
                        Ui.Line(sheet, Loc.T("shop.col.refPrice"), MathX.FormatMoney(t.ReferencePrice));
                        Ui.Line(sheet, Loc.T("shop.col.cost"), MathX.FormatMoney(t.BaseUnitCost));
                        Ui.Line(sheet, Loc.T("shop.col.demand"), demand.ToString("0", Loc.Culture));
                        string terms = Terms(world, t, opt.RiderId);
                        if (terms.Length > 0) Ui.Line(sheet, Loc.T("shop.col.terms"), terms);
                    }, UiFactory.Primary(Loc.T("shop.add"), addIt)),
                    UiFactory.Act(Loc.T("shop.add"), addIt));
            }
            // Special products not available yet, with what unlocks them: one link to a sheet.
            var locked = new List<ProductOption>();
            var seen = new HashSet<string>();
            foreach (var o in MerchService.Offerings(world, db, team))
                if (o.Lock != ProductLock.None && seen.Add(o.Template.Id)) locked.Add(o);
            if (locked.Count > 0)
                Ui.Ghost(list, Loc.T("ux.shop.locked", locked.Count), () => Ui.Sheet(Loc.T("ux.shop.lockedTitle"), sheet =>
                {
                    var lw = new[] { 3f, 4f };
                    for (int i = 0; i < locked.Count; i++)
                        ScreenKit.DataRow(sheet, lw, new[] { "<b>" + Name(locked[i].Template) + "</b>", Theme.Dim(Loc.T("shop.lock." + locked[i].Lock)) }, i);
                }, null, null, 0.7f));
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

        // ------------------------------------------------------------------ campaigns

        private void Campaigns(RectTransform list, WorldState world, Core.Data.GameDatabase db, Team team)
        {
            Ui.Section(list, Loc.T("shop.campaigns"), null, ScreenKit.Info(Loc.T("shop.campaigns"), Loc.T("shop.campaignsInfo"), Loc.T("shop.campaignsNote")));
            var w = new[] { 3f, 1.5f, 1.5f, 1.6f };
            ScreenKit.Captions(list, w, new[] { Loc.T("shop.col.campaign"), Loc.T("shop.col.total"), Loc.T("shop.col.boost"), Loc.T("shop.col.result") }, true);
            int n = 0;
            foreach (CampaignType type in Enum.GetValues(typeof(CampaignType)))
            {
                var ty = type;
                var f = MerchService.ForecastCampaign(world, db, team, type);
                string running = null;
                foreach (var c in team.Shop.Campaigns) if (c.Type == type) running = Loc.T("shop.campaignRunning", c.EventsLeft);
                string name = Loc.T("shop.campaign." + type);
                UiAct action;
                if (running != null) action = UiFactory.Act(running, null);
                else if (f.Running || f.Events <= 0) action = UiFactory.Act(Loc.T("shop.launch"), null);
                else action = UiFactory.Act(Loc.T("shop.launch"), f.Affordable ? (Action)(() =>
                {
                    bool ok = MerchService.LaunchCampaign(world, db, team, ty);
                    Shell.Toast(ok ? Loc.T("shop.launched") : Loc.T("shop.cannotAfford"), ok);
                    Shell.Refresh();
                }) : null);
                ScreenKit.DataRow(list, w, new[]
                {
                    "<b>" + name + "</b>", MathX.FormatMoney(f.Cost), "+" + (f.Boost * 100).ToString("0") + "%", Money(f.Net)
                }, n++, () => Ui.Sheet(name, sheet =>
                {
                    Ui.Line(sheet, Loc.T("shop.col.total"), MathX.FormatMoney(f.Cost));
                    Ui.Line(sheet, Loc.T("shop.col.boost"), "+" + (f.Boost * 100).ToString("0") + "%");
                    Ui.Line(sheet, Loc.T("shop.col.events"), f.Events.ToString());
                    Ui.Line(sheet, Loc.T("shop.col.extraProfit"), Money(f.ExtraProfit));
                    Ui.Line(sheet, Loc.T("shop.col.result"), Money(f.Net), true);
                    if (running != null) Ui.Line(sheet, Loc.T("sponsor.col.status"), Theme.GoodText(running));
                    else if (!f.Affordable && f.Events > 0) Ui.Alert(sheet, Loc.T("shop.cannotAfford"));
                }), action);
            }
        }

        // ------------------------------------------------------------------ one product

        /// <summary>
        /// One product, one screen: figures, price stepper and actions on the left; on the right the price forecast or
        /// the stock orders (one secondary button per row, consequences shown before confirming).
        /// </summary>
        private void BuildDetail(RectTransform content, WorldState world, Core.Data.GameDatabase db, Team team, MerchItem item)
        {
            var t = MerchService.Template(db, item);
            var it = item;
            var cols = Ui.Columns12(content, 5, 7);
            var leftList = ScreenKit.List(cols[0], Ui.Grid.Gap);

            var info = new List<string>();
            if (item.SalesByEvent.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var n in item.SalesByEvent) sb.Append(n).Append("  ");
                info.Add(Loc.T("shop.byEvent") + " " + sb);
            }
            string terms = Terms(world, t, item.RiderId);
            if (terms.Length > 0) info.Add(terms);
            int shared = MerchService.SharedVolume(world, team, t.Id);
            if (shared > 0) info.Add(Loc.T("shop.shared", shared.ToString("N0", Loc.Culture)));
            if (!item.Active && !MerchService.StillAvailable(world, db, team, item)) info.Add(Loc.T("shop.offSale"));
            info.Add(Loc.T("shop.note"));
            string name = Name(world, t, item.RiderId, item.SponsorId);
            var card = Ui.Card(leftList, name, Loc.T("shop.priceRange", MathX.FormatMoney(MerchService.MinPrice(t)), MathX.FormatMoney(MerchService.MaxPrice(t))),
                ScreenKit.Info(name, info.ToArray()), () => { _selected = null; Shell.Refresh(); });
            double perEvent = MerchService.ExpectedUnitsPerEvent(world, db, team, t, item.Price, item.RiderId);
            Ui.Kpis(card,
                Ui.Kpi(item.Stock.ToString("N0", Loc.Culture), Loc.T("shop.col.stock")),
                Ui.Kpi(MathX.FormatMoney(MerchService.MarginPerUnit(world, db, team, t, item, item.Price)), Loc.T("shop.col.margin")),
                Ui.Kpi(perEvent.ToString("0", Loc.Culture), Loc.T("shop.col.demand")));
            Ui.Stepper(card, Loc.T("shop.col.price"), MathX.FormatMoney(item.Price),
                () => { MerchService.SetPrice(db, it, it.Price * 0.95); Shell.Refresh(); },
                () => { MerchService.SetPrice(db, it, it.Price * 1.05); Shell.Refresh(); });
            Ui.Line(card, Loc.T("shop.col.cost"), MathX.FormatMoney(item.Stock > 0 ? item.AverageUnitCost : t.BaseUnitCost));
            if (!item.Active && !MerchService.StillAvailable(world, db, team, item)) Ui.Alert(card, Loc.T("ux.shop.offSale"));
            else if (!item.Active) Ui.Alert(card, Loc.T("ux.shop.retiredAlert"));

            var buttons = UiFactory.Row(card, Ui.Grid.Button);
            var retire = UiFactory.Rect("Retire", buttons);
            UiFactory.Place(retire, 0, 0, item.Stock > 0 ? 0.48f : 1, 1);
            Ui.Secondary(retire, it.Active ? Loc.T("shop.retire") : Loc.T("shop.reactivate"), () =>
            {
                it.Active = !it.Active && MerchService.StillAvailable(world, db, team, it);
                Shell.Refresh();
            });
            if (item.Stock > 0)
            {
                var liq = UiFactory.Rect("Liquidate", buttons);
                UiFactory.Place(liq, 0.52f, 0, 1, 1);
                Ui.Danger(liq, Loc.T("ux.shop.liquidate", MathX.FormatMoney(item.StockValue * 0.3)), Loc.T("ux.shop.liquidateConfirm"), "shop.liquidate." + it.Id, () =>
                {
                    MerchService.Liquidate(world, db, team, it);
                    Shell.Refresh();
                }, null);
            }

            // ---- right: price forecast or stock orders
            string[] views = { Loc.T("shop.col.price"), Loc.T("shop.order") };
            if (_detailView < 0 || _detailView > 1) _detailView = 0;
            var body = Ui.Tile(cols[1], views[_detailView], null, null, 0, null, ScreenKit.Info(views[_detailView], Loc.T("shop.note")));
            Ui.Segmented(ScreenKit.List(ScreenKit.Top(body, 0, 56)), views, _detailView, i => { _detailView = i; Shell.Refresh(); });
            var holder = ScreenKit.Between(body, 64, 0);
            var right = UiFactory.ScrollList("Table", holder, 4);
            UiFactory.Stretch((RectTransform)right.parent.parent);
            var layout = right.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 8);
            layout.spacing = 4;

            if (_detailView == 0)
            {
                // Price forecast (an estimate, not a promise).
                var tw = new[] { 1.6f, 1.6f, 1.6f, 2f };
                ScreenKit.Captions(right, tw, new[] { Loc.T("shop.col.price"), Loc.T("shop.col.demand"), Loc.T("shop.col.margin"), Loc.T("shop.col.profitEvent") }, true);
                int n = 0;
                foreach (var p in MerchService.PriceTable(world, db, team, item))
                {
                    var pp = p;
                    bool current = Math.Abs(p.Price - item.Price) < 0.01;
                    ScreenKit.DataRow(right, tw, new[]
                    {
                        current ? Theme.TimingText(MathX.FormatMoney(p.Price)) : MathX.FormatMoney(p.Price),
                        p.UnitsPerEvent.ToString("0", Loc.Culture), Money(p.MarginPerUnit), Money(p.ProfitPerEvent)
                    }, n++, null, current ? UiFactory.Act(Loc.T("ux.shop.current"), null)
                        : UiFactory.Act(Loc.T("shop.use"), () => { MerchService.SetPrice(db, it, pp.Price); Shell.Refresh(); }));
                }
                return;
            }

            // Stock orders: what each size costs, how long it lasts and the cash left, before confirming.
            int left = MerchService.EventsLeft(world, db, team);
            var sizes = new List<int> { t.MinOrder, (int)Math.Ceiling(perEvent * 4), (int)Math.Ceiling(perEvent * Math.Max(1, left)) };
            sizes.Sort();
            var ow = new[] { 1.6f, 1.6f, 1.8f, 1.8f };
            ScreenKit.Captions(right, ow, new[] { Loc.T("shop.col.units"), Loc.T("shop.col.total"), Loc.T("shop.col.cover"), Loc.T("shop.col.cashAfter") }, true);
            int last = -1, row = 0;
            foreach (int q in sizes)
            {
                if (q <= 0 || q == last) continue;
                last = q;
                var p = MerchService.PreviewOrder(world, db, team, item, q);
                bool much = p.ExpectedLeftover > p.Quantity * 0.3;
                string leftover = Loc.T("shop.leftoverShort", p.ExpectedLeftover.ToString("N0", Loc.Culture));
                int qty = p.Quantity;
                ScreenKit.DataRow(right, ow, new[]
                {
                    "<b>" + p.Quantity.ToString("N0", Loc.Culture) + "</b>\n" + Small(Theme.Dim("@ " + MathX.FormatMoney(p.UnitCost))),
                    MathX.FormatMoney(p.TotalCost),
                    (double.IsInfinity(p.CoverEvents) ? "-" : Loc.T("shop.events", p.CoverEvents.ToString("0", Loc.Culture))) + "\n" + Small(much ? Theme.WarnText(leftover) : Theme.Dim(leftover)),
                    Money(p.CashAfter)
                }, row++, null, UiFactory.Act(Loc.T("shop.confirm"), p.Affordable ? (Action)(() =>
                {
                    Shell.Toast(MerchService.Order(world, db, team, it, qty) ? Loc.T("shop.ordered", qty) : Loc.T("shop.cannotAfford"));
                    Shell.Refresh();
                }) : null), false, 72);
            }
        }
    }
}
