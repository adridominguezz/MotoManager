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
    /// The rider market across both pyramids: pick a category and a filter, scout riders to learn their real level,
    /// and open a negotiation. Moves can happen at any time (now or for next season).
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
        private const int PageSize = 12;
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

            var head = Ui.Card(list, null);
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
            Ui.Chips(head, labels, selected, i => { _category = i < cats.Count ? cats[i].Id : FreeRiders; _page = 0; Shell.Refresh(); }, 50);
            Ui.Chips(head, new[] { Loc.T("market.filter.ending"), Loc.T("market.filter.all"), Loc.T("market.filter.scouted") }, _filter, i => { _filter = i; _page = 0; Shell.Refresh(); }, 46);

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
            var card = Ui.Card(list, null);
            var bar = UiFactory.Row(card, 46);
            var info = UiFactory.Label("Info", bar, Loc.T("market.seats", world.Season + 1, ContractService.SeatsFreeNextSeason(world, Game.Db, team), ContractService.SeatsFreeNow(world, Game.Db, team)) +
                "   " + Theme.Dim(Loc.T("market.page", _page + 1, pages, riders.Count)), Theme.FontSmall - 2, TextAnchor.MiddleLeft, Theme.TextDim);
            info.supportRichText = true;
            UiFactory.Place(info.rectTransform, 0.01f, 0, 0.8f, 1);
            var prev = UiFactory.Button("Prev", bar, "◄", () => { _page--; Shell.Refresh(); }, Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)prev.transform, 0.81f, 0, 0.9f, 1, 3);
            prev.interactable = _page > 0;
            var next = UiFactory.Button("Next", bar, "►", () => { _page++; Shell.Refresh(); }, Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)next.transform, 0.91f, 0, 1f, 1, 3);
            next.interactable = _page < pages - 1;

            var w = new[] { 4.2f, 0.9f, 3f, 1.2f, 1.3f, 1.3f, 1.2f, 3.2f };
            UiFactory.TableRow(card, 34, w, new[] { Loc.T("col.rider"), Loc.T("col.age"), Loc.T("col.team"), Loc.T("col.until"), Loc.T("col.level"), Loc.T("col.potential"), Loc.T("col.known"), "" }, true);
            var levels = TransferMarket.CategoryLevels(world, Game.Db);
            for (int i = _page * PageSize; i < riders.Count && i < (_page + 1) * PageSize; i++)
            {
                var r = riders[i];
                var rider = r;
                var t = world.GetTeam(r.TeamId);
                bool scouted = world.Scouting.Exists(sc => sc.TeamId == team.Id && sc.RiderId == r.Id);
                string name = "<b>" + r.FullName + "</b> " + Theme.Dim(r.Nationality);
                if (TransferMarket.NeedsToPay(world, Game.Db, team, r, levels)) name += " " + Theme.GoodText(Loc.T("market.bringsMoney", MathX.FormatMoney(r.PersonalBacking)));
                if (r.FutureContract != null) name += " " + Theme.WarnText(Loc.T("market.signedShort"));
                var row = UiFactory.TableRow(card, 46, w, new[]
                {
                    name, r.AgeOn(date).ToString(), t != null ? (t.ShortName ?? t.Name) : Theme.GoodText(Loc.T("market.free")),
                    r.Contract != null ? r.Contract.EndSeason.ToString() : "-",
                    Fmt.Rating(Fmt.Estimate(world, team.Id, r, "RaceSkill")), Fmt.Attr(world, team.Id, r, "Potential"),
                    (world.GetKnowledge(team.Id, r.Id) * 100).ToString("0") + "%", ""
                });
                var scout = UiFactory.Button("Scout", row, scouted ? Loc.T("market.scouting") : Loc.T("market.scout"), () => ToggleScout(rider), scouted ? Theme.Good : Theme.PanelLight, Theme.FontSmall - 6);
                UiFactory.InColumn(scout, w, w.Length - 1, 0, 2);
                var offer = UiFactory.Button("Offer", row, Loc.T("market.offer"), () => { _neg = new NegotiationState { RiderId = rider.Id }; Shell.Refresh(); }, Theme.Accent, Theme.FontSmall - 6);
                UiFactory.InColumn(offer, w, w.Length - 1, 1, 2);
            }
            if (riders.Count == 0) Ui.Note(card, Loc.T("market.empty"));
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
