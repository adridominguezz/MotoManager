using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.World;
using UnityEngine.UI;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// The rider market across both pyramids: pick a category (or the free riders) and a filter, then a short page
    /// of riders with their level. Each rider opens a profile with scouting and the negotiation; a negotiation
    /// takes the whole page. Moves can happen at any time (now or for next season).
    /// </summary>
    public class MarketScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.market"); } }
        private string _category;
        private const string FreeRiders = "__free";
        /// <summary>Another screen asked to open the negotiation with this rider (team setup).</summary>
        public static string PendingRiderId;
        private int _filter;
        private NegotiationState _neg;
        private string _rider;
        /// <summary>Rows per page: what fits on a phone without scrolling.</summary>
        private const int PageSize = 6;
        private int _page;

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            if (_category == null) _category = team.CategoryId;
            if (PendingRiderId != null)
            {
                _neg = new NegotiationState { RiderId = PendingRiderId };
                PendingRiderId = null;
            }
            if (_neg != null)
            {
                NegotiationPanel.Build(list, Game, _neg, Shell.Refresh, () => _neg = null);
                return;
            }
            var open = _rider != null ? world.GetRider(_rider) : null;
            if (open != null)
            {
                MobileParts.BackRow(list, Loc.T("career.back"), () => { _rider = null; Shell.Refresh(); });
                Profile(list, world, team, open);
                return;
            }
            _rider = null;

            var cats = Game.Db.CategoriesOrdered();
            var labels = new List<string>();
            int selected = 0;
            for (int i = 0; i < cats.Count; i++)
            {
                labels.Add(cats[i].ShortName);
                if (cats[i].Id == _category) selected = i;
            }
            // Last chip: riders without a team in any category.
            labels.Add(Loc.T("market.free"));
            if (_category == FreeRiders) selected = labels.Count - 1;
            Ui.Chips(list, labels, selected, i => { _category = i < cats.Count ? cats[i].Id : FreeRiders; _page = 0; Shell.Refresh(); }, 46);

            var date = world.CurrentDate;
            var riders = new List<Rider>();
            foreach (var r in world.Riders.Values)
            {
                if (r.IsRetired || r.TeamId == team.Id) continue;
                if (_category == FreeRiders ? r.TeamId != null || r.Contract != null : r.CategoryId != _category) continue;
                bool scouted = world.Scouting.Exists(s => s.TeamId == team.Id && s.RiderId == r.Id);
                if (_filter == 0 && r.Contract != null && r.Contract.EndSeason > world.Season) continue;
                if (_filter == 2 && !scouted) continue;
                riders.Add(r);
            }
            riders.Sort((a, b) => Fmt.Estimate(world, team.Id, b, "RaceSkill").CompareTo(Fmt.Estimate(world, team.Id, a, "RaceSkill")));
            int pages = Math.Max(1, (riders.Count + PageSize - 1) / PageSize);
            _page = Math.Max(0, Math.Min(_page, pages - 1));

            // Filter on the left, page arrows on the right of the same band.
            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);
            Ui.Chips(leftCol, new[] { Loc.T("market.filter.ending"), Loc.T("market.filter.all"), Loc.T("market.filter.scouted") }, _filter, i => { _filter = i; _page = 0; Shell.Refresh(); }, 46);
            var bar = UiFactory.Row(rightCol, UiScale.Touch(46));
            var info = UiFactory.Label("Info", bar, Loc.T("market.page", _page + 1, pages, riders.Count), Theme.FontSmall - 2, TextAnchor.MiddleLeft, Theme.TextDim);
            info.supportRichText = true;
            info.resizeTextForBestFit = true;
            info.resizeTextMinSize = Theme.FontSmall - 8;
            info.resizeTextMaxSize = Theme.FontSmall - 2;
            UiFactory.Place(info.rectTransform, 0.02f, 0, 0.6f, 1);
            var prev = UiFactory.Button("Prev", bar, "◄", () => { _page--; Shell.Refresh(); }, Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)prev.transform, 0.6f, 0, 0.8f, 1, 3);
            prev.interactable = _page > 0;
            var next = UiFactory.Button("Next", bar, "►", () => { _page++; Shell.Refresh(); }, Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)next.transform, 0.8f, 0, 1f, 1, 3);
            next.interactable = _page < pages - 1;

            // The list: who, age, team (and contract end), level, potential; one button opens the rider.
            var card = Ui.Card(list, null);
            var w = new[] { 4.4f, 0.9f, 2.8f, 1.2f, 1.3f, 1.6f };
            UiFactory.TableRow(card, 32, w, new[] { Loc.T("col.rider"), Loc.T("col.age"), Loc.T("col.team"), Loc.T("col.level"), Loc.T("col.potential"), "" }, true);
            var levels = TransferMarket.CategoryLevels(world, Game.Db);
            for (int i = _page * PageSize; i < riders.Count && i < (_page + 1) * PageSize; i++)
            {
                var r = riders[i];
                var rider = r;
                var t = world.GetTeam(r.TeamId);
                bool scouted = world.Scouting.Exists(sc => sc.TeamId == team.Id && sc.RiderId == r.Id);
                string name = (scouted ? Theme.GoodText("● ") : "") + "<b>" + r.FullName + "</b> " + Theme.Dim(r.Nationality);
                if (TransferMarket.NeedsToPay(world, Game.Db, team, r, levels)) name += " " + Theme.GoodText(Loc.T("market.bringsMoney", MathX.FormatMoney(r.PersonalBacking)));
                if (r.FutureContract != null) name += " " + Theme.WarnText(Loc.T("market.signedShort"));
                var row = UiFactory.TableRow(card, 44, w, new[]
                {
                    name, r.AgeOn(date).ToString(),
                    (t != null ? (t.ShortName ?? t.Name) : Theme.GoodText(Loc.T("market.free"))) + (r.Contract != null ? "  " + Theme.Dim(r.Contract.EndSeason.ToString()) : ""),
                    Fmt.Rating(Fmt.Estimate(world, team.Id, r, "RaceSkill")), Fmt.Attr(world, team.Id, r, "Potential"), ""
                });
                MobileParts.Tappable(row, () => { _rider = rider.Id; Shell.Refresh(); });
                var b = UiFactory.Button("Open", row, Loc.T("home.open"), () => { _rider = rider.Id; Shell.Refresh(); }, Theme.Accent, Theme.FontSmall - 4);
                UiFactory.InColumn(b, w, w.Length - 1);
            }
            if (riders.Count == 0) UiFactory.Paragraph(card, Theme.Dim(Loc.T("market.empty")), Theme.FontSmall - 2);
            Ui.Note(card, Loc.T("market.seats", world.Season + 1, ContractService.SeatsFreeNextSeason(world, Game.Db, team), ContractService.SeatsFreeNow(world, Game.Db, team)));
            Ui.Note(card, Loc.T("market.ridersInfo"));
        }

        /// <summary>
        /// A market rider: level, potential and how well known in big type, the key qualities as bars, the
        /// situation and the two actions (scout, negotiate); every attribute on demand.
        /// </summary>
        private void Profile(RectTransform list, WorldState world, Team team, Rider r)
        {
            var rider = r;
            var t = world.GetTeam(r.TeamId);
            bool scouted = world.Scouting.Exists(sc => sc.TeamId == team.Id && sc.RiderId == r.Id);
            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);
            var card = Ui.Card(leftCol, r.FullName, r.Nationality + " · " + Loc.T("rider.age", r.AgeOn(world.CurrentDate)));
            Ui.Kpis(card,
                Ui.Kpi(Fmt.Rating(Fmt.Estimate(world, team.Id, r, "RaceSkill")), Loc.T("col.level")),
                Ui.Kpi(Fmt.Attr(world, team.Id, r, "Potential"), Loc.T("col.potential")),
                Ui.Kpi((world.GetKnowledge(team.Id, r.Id) * 100).ToString("0") + "%", Loc.T("col.known")));
            MobileParts.KeyBars(card, world, team.Id, r);

            var sit = Ui.Card(rightCol, null);
            Ui.Line(sit, Loc.T("col.team"), t != null ? t.Name : Theme.GoodText(Loc.T("market.free")));
            Ui.Line(sit, Loc.T("col.until"), r.Contract != null ? r.Contract.EndSeason.ToString() : "-");
            Ui.Line(sit, Loc.T("rider.style", "").TrimEnd(' ', ':'), Fmt.Styles(r));
            if (TransferMarket.NeedsToPay(world, Game.Db, team, r, TransferMarket.CategoryLevels(world, Game.Db)))
                Ui.Line(sit, Loc.T("market.brings"), Theme.GoodText(MathX.FormatMoney(r.PersonalBacking)));
            if (r.FutureContract != null) Ui.Note(sit, Theme.WarnText(Loc.T("market.signed")));
            UiFactory.ButtonRow(sit, 56,
                UiFactory.Option(scouted ? Loc.T("market.scouting") : Loc.T("market.scout"), scouted, () => ToggleScout(rider)),
                UiFactory.Primary(Loc.T("market.offer"), () => { _neg = new NegotiationState { RiderId = rider.Id }; Shell.Refresh(); }));
            Ui.Note(sit, Loc.T("market.ridersInfo"));
            Ui.Expander(rightCol, "market.attrs", Loc.T("rider.allAttributes"), inner =>
            {
                var attrs = Ui.Card(inner, null);
                MobileParts.AttributeTable(attrs, world, team.Id, rider);
            }, Shell.Refresh);
        }

        private void ToggleScout(Rider r)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            int idx = world.Scouting.FindIndex(s => s.TeamId == team.Id && s.RiderId == r.Id);
            if (idx >= 0) world.Scouting.RemoveAt(idx);
            else world.Scouting.Add(new ScoutingAssignment { TeamId = team.Id, RiderId = r.Id });
            Shell.Refresh();
        }
    }
}
