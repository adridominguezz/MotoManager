using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.World;
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
        private int _filter;
        private NegotiationState _neg;
        private const int PageSize = 30;

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            if (_category == null) _category = team.CategoryId;

            var head = Ui.Card(list, Loc.T("market.title"), Loc.T("market.seats", world.Season + 1, ContractService.SeatsFreeNextSeason(world, Game.Db, team), ContractService.SeatsFreeNow(world, Game.Db, team)));
            var cats = Game.Db.CategoriesOrdered();
            var labels = new List<string>();
            int selected = 0;
            for (int i = 0; i < cats.Count; i++)
            {
                labels.Add(cats[i].ShortName);
                if (cats[i].Id == _category) selected = i;
            }
            Ui.Chips(head, labels, selected, i => { _category = cats[i].Id; Shell.Refresh(); });
            Ui.Chips(head, new[] { Loc.T("market.filter.ending"), Loc.T("market.filter.all"), Loc.T("market.filter.scouted") }, _filter, i => { _filter = i; Shell.Refresh(); }, 50);

            NegotiationPanel.Bids(list, Game, Shell.Refresh);
            if (_neg != null)
            {
                var neg = Ui.Card(list, null, null, Theme.PanelLight);
                NegotiationPanel.Build(neg, Game, _neg, Shell.Refresh, () => _neg = null);
            }

            var date = world.CurrentDate;
            var riders = new List<Rider>();
            foreach (var r in world.Riders.Values)
            {
                if (r.IsRetired || r.CategoryId != _category || r.TeamId == team.Id) continue;
                bool scouted = world.Scouting.Exists(s => s.TeamId == team.Id && s.RiderId == r.Id);
                if (_filter == 0 && r.Contract != null && r.Contract.EndSeason > world.Season) continue;
                if (_filter == 2 && !scouted) continue;
                riders.Add(r);
            }
            riders.Sort((a, b) => Fmt.Estimate(world, team.Id, b, "RaceSkill").CompareTo(Fmt.Estimate(world, team.Id, a, "RaceSkill")));

            var card = Ui.Card(list, Loc.T("market.riders", riders.Count), Loc.T("market.ridersInfo"));
            var levels = TransferMarket.CategoryLevels(world, Game.Db);
            int shown = 0;
            foreach (var r in riders)
            {
                if (shown++ >= PageSize) break;
                var rider = r;
                var t = world.GetTeam(r.TeamId);
                bool scouted = world.Scouting.Exists(s => s.TeamId == team.Id && s.RiderId == r.Id);
                var parts = new List<string>
                {
                    Loc.T("rider.age", r.AgeOn(date)) + " · " + r.Nationality,
                    t != null ? (t.ShortName ?? t.Name) + " · " + Loc.T("rider.untilShort", r.Contract != null ? r.Contract.EndSeason : world.Season) : Theme.GoodText(Loc.T("market.free")),
                    Loc.T("market.known", (world.GetKnowledge(team.Id, r.Id) * 100).ToString("0"))
                };
                if (TransferMarket.NeedsToPay(world, Game.Db, team, r, levels)) parts.Add(Theme.GoodText(Loc.T("market.bringsMoney", MathX.FormatMoney(r.PersonalBacking))));
                if (r.FutureContract != null) parts.Add(Theme.WarnText(Loc.T("market.signed")));
                string value = Loc.T("market.level", Fmt.Rating(Fmt.Estimate(world, team.Id, r, "RaceSkill"))) + "\n" +
                               Theme.Dim(Loc.T("market.potential", Fmt.Attr(world, team.Id, r, "Potential")));
                Ui.Item(card, r.FullName, string.Join("  ·  ", parts.ToArray()), value,
                    UiFactory.Act(Loc.T("market.offer"), () => { _neg = new NegotiationState { RiderId = rider.Id }; Shell.Refresh(); }),
                    UiFactory.Act(scouted ? Loc.T("market.scouting") : Loc.T("market.scout"), () => ToggleScout(rider)));
            }
            if (riders.Count == 0) Ui.Note(card, Loc.T("market.empty"));
            else if (riders.Count > PageSize) Ui.Note(card, Loc.T("market.more", riders.Count - PageSize));
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
