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

        private static string Money(double v)
        {
            return (v < 0 ? "<color=#E10600>" : "") + MathX.FormatMoney(v) + (v < 0 ? "</color>" : "");
        }

        private static string Name(ProductTemplate t)
        {
            return t != null ? Loc.T("product." + t.Type) : "?";
        }

        public override void Build(RectTransform content)
        {
            var list = UiFactory.ScrollList("Shop", content);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var world = Game.World;
            var db = Game.Db;
            var team = world.PlayerTeam;
            if (team == null) return;
            if (!MerchService.HasShop(db, world, team))
            {
                UiFactory.Paragraph(list, Loc.T("shop.none"), Theme.FontNormal);
                return;
            }
            var shop = team.Shop;

            // Season so far.
            var s = MerchService.Summary(world, team);
            UiFactory.Heading(list, Loc.T("shop.season", world.Season));
            var w = new[] { 5f, 3f };
            UiFactory.TableRow(list, 40, w, new[] { Loc.T("shop.fans"), shop.Fans.ToString("N0", Loc.Culture) + (shop.Hype > 0.05 ? "  <color=#F0B428>" + Loc.T("shop.hype", (shop.Hype * 100).ToString("0")) + "</color>" : "") });
            UiFactory.TableRow(list, 40, w, new[] { Loc.T("shop.revenue"), Money(s.Revenue) });
            UiFactory.TableRow(list, 40, w, new[] { Loc.T("shop.cogs"), Money(-s.CostOfSales) });
            UiFactory.TableRow(list, 40, w, new[] { "<b>" + Loc.T("shop.gross") + "</b>", "<b>" + Money(s.GrossMargin) + "</b>" });
            UiFactory.TableRow(list, 40, w, new[] { Loc.T("shop.operations"), Money(-s.Operations) });
            if (s.Royalties > 0) UiFactory.TableRow(list, 40, w, new[] { Loc.T("shop.royalties"), Money(-s.Royalties) });
            UiFactory.TableRow(list, 40, w, new[] { "<b>" + Loc.T("shop.net") + "</b>", "<b>" + Money(s.Net) + "</b>" });
            UiFactory.TableRow(list, 40, w, new[] { Loc.T("shop.stockSpend"), Money(-s.StockPurchases) });
            UiFactory.TableRow(list, 40, w, new[] { Loc.T("shop.inventory"), MathX.FormatMoney(s.InventoryValue) });
            UiFactory.TableRow(list, 40, w, new[] { Loc.T("shop.units"), s.UnitsSold.ToString("N0", Loc.Culture) + (s.LostSales > 0 ? "  <color=#E10600>" + Loc.T("shop.lost", s.LostSales.ToString("N0", Loc.Culture)) + "</color>" : "") });

            // Products on sale, most profitable first.
            var items = new List<MerchItem>(shop.Items);
            items.Sort((a, b) => (b.RevenueSeason - b.CostOfSalesSeason).CompareTo(a.RevenueSeason - a.CostOfSalesSeason));
            UiFactory.Heading(list, Loc.T("shop.products"));
            var pw = new[] { 3f, 1.4f, 1.4f, 1.6f, 1.2f, 1.2f, 1.8f, 1.5f, 1.6f };
            UiFactory.TableRow(list, 38, pw, new[] { Loc.T("shop.col.product"), Loc.T("shop.col.price"), Loc.T("shop.col.cost"), Loc.T("shop.col.margin"), Loc.T("shop.col.stock"),
                Loc.T("shop.col.sold"), Loc.T("shop.col.profit"), Loc.T("shop.col.demand"), "" }, true);
            foreach (var item in items)
            {
                var t = MerchService.Template(db, item);
                if (t == null) continue;
                var it = item;
                double cost = item.Stock > 0 ? item.AverageUnitCost : t.BaseUnitCost;
                double margin = MerchService.MarginPerUnit(world, db, team, t, item, item.Price);
                double demand = MerchService.ExpectedUnitsPerEvent(world, db, team, t, item.Price);
                var row = UiFactory.TableRow(list, 44, pw, new[]
                {
                    Name(t) + (item.Active ? "" : " <color=#9AA4B2>(" + Loc.T("shop.retired") + ")</color>"),
                    MathX.FormatMoney(item.Price), MathX.FormatMoney(cost), Money(margin), item.Stock.ToString("N0", Loc.Culture),
                    item.SoldSeason.ToString("N0", Loc.Culture), Money(item.RevenueSeason - item.CostOfSalesSeason), demand.ToString("0", Loc.Culture), ""
                }, false, _selected == item.Id ? new Color(0.94f, 0.7f, 0.16f, 0.16f) : (Color?)null);
                var b = UiFactory.Button("Select", row, Loc.T("shop.manage"), () => { _selected = it.Id; Shell.Refresh(); }, Theme.PanelLight, Theme.FontSmall - 4);
                UiFactory.Place((RectTransform)b.transform, 0.88f, 0.08f, 1f, 0.92f);
            }

            var selected = MerchService.Find(team, _selected);
            if (selected != null) BuildDetail(list, world, db, team, selected);

            // What else could be sold.
            var catalogue = MerchService.Catalogue(db, team);
            var add = new List<ProductTemplate>();
            foreach (var t in catalogue)
            {
                bool have = false;
                foreach (var i in shop.Items) if (i.TemplateId == t.Id) have = true;
                if (!have) add.Add(t);
            }
            if (add.Count > 0)
            {
                UiFactory.Heading(list, Loc.T("shop.catalogue"));
                var cw = new[] { 3f, 1.6f, 1.6f, 2f, 1.6f };
                UiFactory.TableRow(list, 38, cw, new[] { Loc.T("shop.col.product"), Loc.T("shop.col.refPrice"), Loc.T("shop.col.cost"), Loc.T("shop.col.demand"), "" }, true);
                foreach (var t in add)
                {
                    var tpl = t;
                    var row = UiFactory.TableRow(list, 44, cw, new[] { Name(t) + (t.Identity == ProductIdentity.Manufacturer ? " <color=#9AA4B2>" + Loc.T("shop.royalty") + "</color>" : ""),
                        MathX.FormatMoney(t.ReferencePrice), MathX.FormatMoney(t.BaseUnitCost), MerchService.ExpectedUnitsPerEvent(world, db, team, t, t.ReferencePrice).ToString("0", Loc.Culture), "" });
                    var b = UiFactory.Button("Add", row, Loc.T("shop.add"), () =>
                    {
                        var it = MerchService.AddProduct(world, db, team, tpl.Id);
                        if (it != null) _selected = it.Id;
                        Shell.Refresh();
                    }, Theme.Accent, Theme.FontSmall - 4);
                    UiFactory.Place((RectTransform)b.transform, 0.84f, 0.08f, 1f, 0.92f);
                }
            }
            UiFactory.Paragraph(list, "<color=#9AA4B2>" + Loc.T("shop.note") + "</color>", Theme.FontSmall - 2);
        }

        private void BuildDetail(RectTransform list, WorldState world, Core.Data.GameDatabase db, Team team, MerchItem item)
        {
            var t = MerchService.Template(db, item);
            UiFactory.Heading(list, Name(t) + "  <color=#9AA4B2>" + Loc.T("shop.priceRange", MathX.FormatMoney(MerchService.MinPrice(t)), MathX.FormatMoney(MerchService.MaxPrice(t))) + "</color>");

            UiFactory.ButtonRow(list, 50,
                UiFactory.Act("-10%", () => { MerchService.SetPrice(db, item, item.Price * 0.9); Shell.Refresh(); }),
                UiFactory.Act("-5%", () => { MerchService.SetPrice(db, item, item.Price * 0.95); Shell.Refresh(); }),
                UiFactory.Act("+5%", () => { MerchService.SetPrice(db, item, item.Price * 1.05); Shell.Refresh(); }),
                UiFactory.Act("+10%", () => { MerchService.SetPrice(db, item, item.Price * 1.1); Shell.Refresh(); }),
                UiFactory.Act(item.Active ? Loc.T("shop.retire") : Loc.T("shop.reactivate"), () => { item.Active = !item.Active; Shell.Refresh(); }));

            // Price forecast (an estimate, not a promise).
            var tw = new[] { 2f, 2f, 2f, 2.4f };
            UiFactory.TableRow(list, 36, tw, new[] { Loc.T("shop.col.price"), Loc.T("shop.col.demand"), Loc.T("shop.col.margin"), Loc.T("shop.col.profitEvent") }, true);
            foreach (var p in MerchService.PriceTable(world, db, team, item))
            {
                var pp = p;
                bool current = Math.Abs(p.Price - item.Price) < 0.01;
                var row = UiFactory.TableRow(list, 40, tw, new[] { MathX.FormatMoney(p.Price) + (current ? " ◀" : ""), p.UnitsPerEvent.ToString("0", Loc.Culture), Money(p.MarginPerUnit), Money(p.ProfitPerEvent) });
                var b = UiFactory.Button("Use", row, Loc.T("shop.use"), () => { MerchService.SetPrice(db, item, pp.Price); Shell.Refresh(); }, Theme.PanelLight, Theme.FontSmall - 4);
                UiFactory.Place((RectTransform)b.transform, 0.86f, 0.08f, 1f, 0.92f);
            }

            // Sales by event this season.
            if (item.SalesByEvent.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var n in item.SalesByEvent) sb.Append(n).Append("  ");
                UiFactory.Paragraph(list, "<color=#9AA4B2>" + Loc.T("shop.byEvent") + "</color> " + sb, Theme.FontSmall);
            }

            // Stock orders: consequences shown before confirming.
            int left = MerchService.EventsLeft(world, db, team);
            double perEvent = MerchService.ExpectedUnitsPerEvent(world, db, team, t, item.Price);
            var sizes = new List<int> { t.MinOrder, (int)Math.Ceiling(perEvent * 4), (int)Math.Ceiling(perEvent * Math.Max(1, left)) };
            sizes.Sort();
            UiFactory.Heading(list, Loc.T("shop.order"));
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
                UiFactory.Place((RectTransform)b.transform, 0.86f, 0.08f, 1f, 0.92f);
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
