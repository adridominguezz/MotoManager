using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>Standings of any category: riders, teams and constructors.</summary>
    public class StandingsScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.standings"); } }
        private string _category;
        private int _table;

        private static readonly Color Mine = new Color(0.9f, 0.22f, 0.23f, 0.2f);

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            if (_category == null) _category = world.PlayerTeam != null ? world.PlayerTeam.CategoryId : "motogp";

            var head = Ui.Card(list, null);
            var cats = new List<ChampionshipCategory>();
            foreach (var c in Game.Db.CategoriesOrdered()) if (c.Simulated) cats.Add(c);
            var labels = new List<string>();
            int sel = 0;
            for (int i = 0; i < cats.Count; i++)
            {
                labels.Add(cats[i].ShortName);
                if (cats[i].Id == _category) sel = i;
            }
            Ui.Chips(head, labels, sel, i => { _category = cats[i].Id; Shell.Refresh(); });
            Ui.Chips(head, new[] { Loc.T("standings.riders"), Loc.T("standings.teams"), Loc.T("standings.makers") }, _table, i => { _table = i; Shell.Refresh(); }, 50);

            ChampionshipState champ;
            if (!world.Championships.TryGetValue(_category, out champ)) return;
            var card = Ui.Card(list, null);
            var playerTeam = world.PlayerTeamId;
            if (_table == 0)
            {
                var widths = new[] { 0.8f, 5f, 3.6f, 1.6f, 1.2f, 1.2f, 1.2f };
                UiFactory.TableRow(card, 40, widths, new[] { "#", Loc.T("col.rider"), Loc.T("col.team"), Loc.T("col.points"), Loc.T("col.wins"), Loc.T("col.podiums"), Loc.T("col.poles") }, true);
                var rows = champ.SortedRiders();
                for (int i = 0; i < rows.Count; i++)
                {
                    var r = world.GetRider(rows[i].Id);
                    var team = r != null ? world.GetTeam(r.TeamId) : null;
                    bool mine = team != null && team.Id == playerTeam;
                    UiFactory.TableRow(card, 44, widths, new[]
                    {
                        (i + 1).ToString(), r != null ? "<b>" + r.FullName + "</b> " + Theme.Dim(r.Nationality) : rows[i].Id,
                        team != null ? team.ShortName ?? team.Name : "-", "<b>" + rows[i].Points.ToString("0") + "</b>", rows[i].Wins.ToString(), rows[i].Podiums.ToString(), rows[i].Poles.ToString()
                    }, false, mine ? Mine : (Color?)null);
                }
            }
            else
            {
                var rows = _table == 1 ? champ.SortedTeams() : champ.SortedManufacturers();
                var widths = new[] { 0.8f, 7f, 2f, 2f };
                UiFactory.TableRow(card, 40, widths, new[] { "#", _table == 1 ? Loc.T("col.team") : Loc.T("col.maker"), Loc.T("col.points"), Loc.T("col.wins") }, true);
                for (int i = 0; i < rows.Count; i++)
                {
                    string name = rows[i].Id;
                    if (_table == 1) { var t = world.GetTeam(name); if (t != null) name = t.Name; }
                    else { var m = world.GetManufacturer(name); if (m != null) name = m.Name; }
                    bool mine = _table == 1 && rows[i].Id == playerTeam;
                    UiFactory.TableRow(card, 44, widths, new[] { (i + 1).ToString(), name, "<b>" + rows[i].Points.ToString("0") + "</b>", rows[i].Wins.ToString() }, false, mine ? Mine : (Color?)null);
                }
            }
        }
    }

    /// <summary>Both calendars with dates, circuits and the winners of past rounds.</summary>
    public class CalendarScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.calendar"); } }
        private int _championship;

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var keys = new List<string>(world.Calendars.Keys);
            if (keys.Count == 0) return;
            // The player's championship first.
            var team = world.PlayerTeam;
            keys.Sort((a, b) => Owns(b, team).CompareTo(Owns(a, team)));
            var labels = new List<string>();
            foreach (var k in keys) labels.Add(Game.Db.Championships.ContainsKey(k) ? Game.Db.Championships[k].Name : k);
            _championship = Mathf.Clamp(_championship, 0, keys.Count - 1);
            var head = Ui.Card(list, null);
            Ui.Chips(head, labels, _championship, i => { _championship = i; Shell.Refresh(); });

            string key = keys[_championship];
            var cal = world.Calendars[key];
            var champ = Game.Db.Championships.ContainsKey(key) ? Game.Db.Championships[key] : null;
            var card = Ui.Card(list, null);
            var widths = new[] { 0.7f, 1.6f, 5f, 3.6f, 4.6f };
            UiFactory.TableRow(card, 40, widths, new[] { "#", Loc.T("col.date"), Loc.T("col.event"), Loc.T("col.circuit"), Loc.T("col.winner") }, true);
            int next;
            world.NextRound.TryGetValue(key, out next);
            string topCat = champ != null && champ.Categories.Count > 0 ? champ.Categories[0] : null;
            for (int i = 0; i < cal.Rounds.Count; i++)
            {
                var r = cal.Rounds[i];
                var circuit = Game.Db.GetCircuit(r.CircuitId);
                string winner = i < next ? Winner(world, topCat, r.Round) : "";
                Color? bg = i == next ? new Color(0.94f, 0.7f, 0.16f, 0.18f) : (Color?)null;
                UiFactory.TableRow(card, 44, widths, new[] { r.Round.ToString(), r.Date.ToString("dd MMM", Loc.Culture), (i == next ? "<b>" + Loc.Round(r.Name) + "</b>" : Loc.Round(r.Name)), circuit != null ? circuit.ShortName : r.CircuitId, winner }, false, bg,
                    i < next ? Theme.TextDim : Theme.Text);
            }
        }

        private int Owns(string championshipId, Team team)
        {
            if (team == null || !Game.Db.Championships.ContainsKey(championshipId)) return 0;
            return Game.Db.Championships[championshipId].Categories.Contains(team.CategoryId) ? 1 : 0;
        }

        private static string Winner(WorldState world, string category, int round)
        {
            ChampionshipState cs;
            if (category == null || !world.Championships.TryGetValue(category, out cs)) return "";
            foreach (var w in cs.Rounds)
                if (w.Round == round)
                    foreach (var s in w.Sessions)
                        if (s.Type == SessionType.Race && s.Classification.Count > 0)
                        {
                            var rider = world.GetRider(s.Classification[0].RiderId);
                            return rider != null ? rider.FullName : "";
                        }
            return "";
        }
    }

    /// <summary>Roll of honour since 2026, plus all-time leaders.</summary>
    public class HistoryScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.history"); } }

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            if (world.Champions.Count == 0)
            {
                var empty = Ui.Card(list, Loc.T("history.title"));
                Ui.Note(empty, Loc.T("history.empty"));
            }
            foreach (var cat in Game.Db.CategoriesOrdered())
            {
                if (!cat.Simulated) continue;
                var champs = world.Champions.FindAll(c => c.CategoryId == cat.Id);
                if (champs.Count == 0) continue;
                var card = Ui.Card(list, cat.Name);
                var widths = new[] { 1.2f, 5f, 5f, 2f };
                UiFactory.TableRow(card, 40, widths, new[] { Loc.T("col.season"), Loc.T("col.champion"), Loc.T("col.team"), Loc.T("col.points") }, true);
                for (int i = champs.Count - 1; i >= 0; i--)
                {
                    var c = champs[i];
                    UiFactory.TableRow(card, 42, widths, new[] { c.Season.ToString(), "<b>" + c.RiderName + "</b>", c.TeamName ?? "", c.Points.ToString("0") });
                }
            }

            var all = new List<Rider>(world.Riders.Values);
            all.Sort((a, b) => b.TotalWins().CompareTo(a.TotalWins()));
            if (all.Count == 0 || all[0].TotalWins() == 0) return;
            var top = Ui.Card(list, Loc.T("history.mostWins"));
            var w2 = new[] { 5f, 2f, 2f, 2f };
            UiFactory.TableRow(top, 40, w2, new[] { Loc.T("col.rider"), Loc.T("col.wins"), Loc.T("col.podiums"), Loc.T("col.titles") }, true);
            for (int i = 0; i < all.Count && i < 15; i++)
            {
                var r = all[i];
                if (r.TotalWins() == 0) break;
                UiFactory.TableRow(top, 42, w2, new[] { r.FullName + (r.IsRetired ? " " + Theme.Dim(Loc.T("history.retired")) : ""), r.TotalWins().ToString(), r.TotalPodiums().ToString(), r.Titles.Count.ToString() });
            }
        }
    }
}
