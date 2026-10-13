using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// The rider market across both pyramids (UX spec §3.16): one control row (category picker, filter segments, the
    /// free seats with the rules behind "i", the pager), then a page of tappable rows (rank, rider with at most one
    /// tag, age, team and contract end, level, potential). A row opens the profile (scouting and the offer); a negotiation
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
        /// <summary>Rows per page: what fits on the screen without scrolling.</summary>
        private const int PageSize = 7;
        private int _page;

        private static readonly float[] Widths = { 0.9f, 4.4f, 0.9f, 3f, 1.2f, 1.4f };

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null)
            {
                Ui.Empty(list, UiIcons.Market, Loc.T("home.noTeam"));
                return;
            }
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
                Profile(list, world, team, open);
                return;
            }
            _rider = null;

            var riders = Riders(world, team);
            int pages = Math.Max(1, (riders.Count + PageSize - 1) / PageSize);
            _page = Math.Max(0, Math.Min(_page, pages - 1));
            Controls(list, world, team, riders.Count, pages);

            // The list: rank, rider, age, team and contract end, level, potential; the row opens the rider.
            var table = Ui.Stack(list);
            Ui.ListCaptions(table, Widths, new[] { "#", Loc.T("col.rider"), Loc.T("col.age"), Loc.T("col.team"), Loc.T("col.level"), Loc.T("col.potential") });
            var levels = TransferMarket.CategoryLevels(world, Game.Db);
            for (int i = _page * PageSize; i < riders.Count && i < (_page + 1) * PageSize; i++)
            {
                var r = riders[i];
                var rider = r;
                var t = world.GetTeam(r.TeamId);
                string name = "<b>" + r.FullName + "</b>  " + Theme.Dim("<size=" + Theme.TextCaption + ">" + (r.Nationality ?? "").ToUpperInvariant() + "</size>");
                string tag = null;
                if (r.FutureContract != null) tag = Theme.WarnText(Loc.T("ux.market.tagSigned"));
                else if (TransferMarket.NeedsToPay(world, Game.Db, team, r, levels)) tag = Theme.GoodText(Loc.T("ux.market.tagMoney"));
                else if (world.Scouting.Exists(sc => sc.TeamId == team.Id && sc.RiderId == r.Id)) tag = Theme.Dim(Loc.T("ux.market.tagScouted"));
                if (tag != null) name += "  <size=" + Theme.TextCaption + "><b>" + Theme.Caps(tag) + "</b></size>";
                string club = (t != null ? (t.ShortName ?? t.Name) : Theme.GoodText(Loc.T("market.free"))) +
                              (r.Contract != null ? "  " + Theme.Dim(r.Contract.EndSeason.ToString()) : "");
                Ui.ListRow(table, Widths, new[]
                {
                    (i + 1).ToString(), name, r.AgeOn(world.CurrentDate).ToString(), club,
                    Fmt.Rating(Fmt.Estimate(world, team.Id, r, "RaceSkill")), Fmt.Attr(world, team.Id, r, "Potential")
                }, i - _page * PageSize, false, () => { _rider = rider.Id; Shell.Refresh(); });
            }
            if (riders.Count == 0) Ui.Empty(list, UiIcons.Market, Loc.T("market.empty"));
        }

        /// <summary>The riders of the chosen category (or free) and filter, best estimated level first.</summary>
        private List<Rider> Riders(WorldState world, Team team)
        {
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
            return riders;
        }

        /// <summary>One row: category picker, filter, free seats (rules behind "i") and the pager.</summary>
        private void Controls(RectTransform list, WorldState world, Team team, int count, int pages)
        {
            var row = UiFactory.Row(list, Ui.Grid.Hit);
            var cols = Ui.Columns12(row, 2, 5, 3, 2);

            var cats = Game.Db.CategoriesOrdered();
            var labels = new List<string>();
            int selected = 0;
            for (int i = 0; i < cats.Count; i++)
            {
                labels.Add(cats[i].ShortName);
                if (cats[i].Id == _category) selected = i;
            }
            // Last option: riders without a team in any category.
            labels.Add(Loc.T("market.free"));
            if (_category == FreeRiders) selected = labels.Count - 1;
            Ui.Picker(cols[0], Loc.T("ux.market.category"), labels, selected, i =>
            {
                _category = i < cats.Count ? cats[i].Id : FreeRiders;
                _page = 0;
                Shell.Refresh();
            });

            Wp2Ui.ListIn(cols[1], 0);
            Ui.Segmented(cols[1], new[] { Loc.T("market.filter.ending"), Loc.T("market.filter.all"), Loc.T("market.filter.scouted") }, _filter,
                i => { _filter = i; _page = 0; Shell.Refresh(); });

            int now = ContractService.SeatsFreeNow(world, Game.Db, team);
            int next = ContractService.SeatsFreeNextSeason(world, Game.Db, team);
            var seats = Ui.Label(cols[2], Theme.Caps(Loc.T("ux.market.seats", now, next, world.Season + 1)), Theme.TextCaption, TextAnchor.MiddleRight,
                now > 0 ? Theme.Warning : Color.Lerp(Theme.TextDim, Color.white, 0.2f), FontStyle.Bold);
            UiFactory.Stretch(seats.rectTransform);
            seats.rectTransform.offsetMax = new Vector2(-Ui.Grid.Hit - 4, 0);
            var info = Ui.IconButton(cols[2], Glyph.Info, Wp2Ui.Info(Loc.T("market.title"),
                Loc.T("market.seats", world.Season + 1, next, now), Loc.T("market.ridersInfo")));
            var irt = (RectTransform)info.transform;
            irt.anchorMin = irt.anchorMax = new Vector2(1, 0.5f);
            irt.pivot = new Vector2(1, 0.5f);
            irt.anchoredPosition = Vector2.zero;

            var prev = Wp2Ui.Arrow(cols[3], false, () => { _page--; Shell.Refresh(); }, _page > 0);
            var prt = (RectTransform)prev.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0, 0.5f);
            prt.pivot = new Vector2(0, 0.5f);
            prt.anchoredPosition = Vector2.zero;
            var page = Ui.Label(cols[3], (_page + 1) + "/" + pages, Theme.TextBody, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            UiFactory.Stretch(page.rectTransform);
            page.rectTransform.offsetMin = new Vector2(Ui.Grid.Hit, 0);
            page.rectTransform.offsetMax = new Vector2(-Ui.Grid.Hit, 0);
            var nextB = Wp2Ui.Arrow(cols[3], true, () => { _page++; Shell.Refresh(); }, _page < pages - 1);
            var nrt = (RectTransform)nextB.transform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(1, 0.5f);
            nrt.pivot = new Vector2(1, 0.5f);
            nrt.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// A market rider: on the left the name (back chevron, every attribute behind "i"), level, potential and how
        /// well known, and the key qualities as bars; on the right the situation, an alert when already signed, and
        /// the two actions (scout = secondary toggle, make an offer = the primary).
        /// </summary>
        private void Profile(RectTransform list, WorldState world, Team team, Rider r)
        {
            var rider = r;
            var t = world.GetTeam(r.TeamId);
            bool scouted = world.Scouting.Exists(sc => sc.TeamId == team.Id && sc.RiderId == r.Id);
            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);
            var card = Ui.Card(leftCol, r.FullName, (r.Nationality ?? "").ToUpperInvariant() + " · " + Theme.Caps(Loc.T("rider.age", r.AgeOn(world.CurrentDate))),
                () => Wp2Ui.AttributesSheet(world, team.Id, rider, Loc.T("market.ridersInfo")),
                () => { _rider = null; Shell.Refresh(); });
            Ui.Kpis(card,
                Ui.Kpi(Fmt.Estimate(world, team.Id, r, "RaceSkill").ToString("0"), Loc.T("col.level")),
                Ui.Kpi(Fmt.Attr(world, team.Id, r, "Potential"), Loc.T("col.potential")),
                Ui.Kpi((world.GetKnowledge(team.Id, r.Id) * 100).ToString("0") + "%", Loc.T("col.known")));
            MobileParts.KeyBars(card, world, team.Id, r);

            var sit = Ui.Card(rightCol, Loc.T("ux.market.situation"));
            Ui.Line(sit, Loc.T("col.team"), t != null ? t.Name : Theme.GoodText(Loc.T("market.free")), true);
            if (t != null) Ui.Ghost(UiFactory.Row(sit, Ui.Grid.Hit), Loc.T("ux.mm4b.viewTeam"), () => TeamSheet.Open(Game, t.Id));
            Ui.Line(sit, Loc.T("col.until"), r.Contract != null ? r.Contract.EndSeason.ToString() : "-");
            Ui.Line(sit, Loc.T("rider.style", "").TrimEnd(' ', ':'), Fmt.Styles(r));
            if (TransferMarket.NeedsToPay(world, Game.Db, team, r, TransferMarket.CategoryLevels(world, Game.Db)))
                Ui.Line(sit, Loc.T("market.brings"), Theme.GoodText(MathX.FormatMoney(r.PersonalBacking)));
            if (r.FutureContract != null) Ui.Alert(sit, Loc.T("market.signed"));
            var row = UiFactory.Row(sit, Ui.Grid.Cta);
            var cols = Ui.Columns12(row, 5, 7);
            Action scout = () => ToggleScout(rider);
            if (scouted) Ui.SecondaryAccent(cols[0], Loc.T("market.scouting"), scout);
            else Ui.Secondary(cols[0], Loc.T("market.scout"), scout);
            Wp2Ui.Chevron(Ui.PrimaryCTA(cols[1], Loc.T("ux.market.makeOffer"), () => { _neg = new NegotiationState { RiderId = rider.Id }; Shell.Refresh(); }));
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
