using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.Season;
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

        private static readonly Color Mine = new Color(0.94f, 0.7f, 0.16f, 0.16f);

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
                    UiFactory.TableRow(card, 36, widths, new[]
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
                    UiFactory.TableRow(card, 36, widths, new[] { (i + 1).ToString(), name, "<b>" + rows[i].Points.ToString("0") + "</b>", rows[i].Wins.ToString() }, false, mine ? Mine : (Color?)null);
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
            keys.Sort((a, b) =>
            {
                int c = Owns(b, team).CompareTo(Owns(a, team));
                return c != 0 ? c : string.CompareOrdinal(a, b);
            });
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
                UiFactory.TableRow(card, 36, widths, new[] { r.Round.ToString(), r.Date.ToString("dd MMM", Loc.Culture), (i == next ? "<b>" + Loc.Round(r.Name) + "</b>" : Loc.Round(r.Name)), circuit != null ? circuit.ShortName : r.CircuitId, winner }, false, bg,
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
    /// <summary>
    /// The history of the world: champions by championship, each season (podium, rookie of the year, retirements),
    /// records and rivalries, and the Hall of Fame.
    /// </summary>
    public class HistoryScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.history"); } }
        private int _view;
        private string _category;

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var db = Game.Db;
            var cats = new List<ChampionshipCategory>();
            foreach (var c in db.CategoriesOrdered()) if (c.Simulated) cats.Add(c);
            if (_category == null) _category = world.PlayerTeam != null ? world.PlayerTeam.CategoryId : cats[0].Id;

            var head = Ui.Card(list, null);
            Ui.Chips(head, new[] { Loc.T("history.view.champions"), Loc.T("history.view.seasons"), Loc.T("history.view.records"), Loc.T("history.view.hall") }, _view,
                i => { _view = i; Shell.Refresh(); }, 50);
            if (_view != 3)
            {
                var names = new List<string>();
                int sel = 0;
                for (int i = 0; i < cats.Count; i++)
                {
                    names.Add(cats[i].ShortName);
                    if (cats[i].Id == _category) sel = i;
                }
                Ui.Chips(head, names, sel, i => { _category = cats[i].Id; Shell.Refresh(); }, 46);
            }
            var cat = db.GetCategory(_category);
            switch (_view)
            {
                case 1: Seasons(list, world, cat); break;
                case 2: Records(list, world, cat); break;
                case 3: Hall(list, world, db); break;
                default: Champions(list, world, cat); break;
            }
        }

        private void Champions(RectTransform list, WorldState world, ChampionshipCategory cat)
        {
            var card = Ui.Card(list, cat.Name);
            var champs = world.Champions.FindAll(c => c.CategoryId == cat.Id);
            if (champs.Count == 0)
            {
                UiFactory.Paragraph(card, Theme.Dim(Loc.T("history.empty")), Theme.FontSmall - 2);
                return;
            }
            var widths = new[] { 1.2f, 4f, 4f, 1.6f };
            UiFactory.TableRow(card, 40, widths, new[] { Loc.T("col.season"), Loc.T("col.champion"), Loc.T("col.team"), Loc.T("col.points") }, true);
            for (int i = champs.Count - 1; i >= 0; i--)
            {
                var c = champs[i];
                UiFactory.TableRow(card, 36, widths, new[] { c.Season.ToString(), "<b>" + c.RiderName + "</b>", c.TeamName ?? "", c.Points.ToString("0") });
            }
        }

        private void Seasons(RectTransform list, WorldState world, ChampionshipCategory cat)
        {
            var seasons = world.History.FindAll(h => h.CategoryId == cat.Id);
            if (seasons.Count == 0)
            {
                UiFactory.Paragraph(Ui.Card(list, cat.Name), Theme.Dim(Loc.T("history.empty")), Theme.FontSmall - 2);
                return;
            }
            RectTransform left, right;
            Ui.Columns(list, out left, out right);
            for (int i = seasons.Count - 1, k = 0; i >= 0 && k < 8; i--, k++)
            {
                var h = seasons[i];
                var maker = world.GetManufacturer(h.MakerChampionId);
                var card = Ui.Card(k % 2 == 0 ? left : right, h.Season + " · " + cat.ShortName, Loc.T("history.races", h.Races));
                Ui.Line(card, "1º", "<b>" + h.Champion + "</b>  " + Theme.Dim(h.ChampionPoints.ToString("0") + " pts"));
                if (h.Second != null) Ui.Line(card, "2º", h.Second + "  " + Theme.Dim("-" + (h.ChampionPoints - h.SecondPoints).ToString("0")));
                if (h.Third != null) Ui.Line(card, "3º", h.Third);
                if (h.TeamChampion != null) Ui.Line(card, Loc.T("history.teamTitle"), h.TeamChampion);
                if (maker != null) Ui.Line(card, Loc.T("history.makerTitle"), maker.Name);
                if (h.MostWins != null) Ui.Line(card, Loc.T("history.mostWinsSeason"), h.MostWins + "  " + Theme.Dim(h.MostWinsCount.ToString()));
                if (h.Rookie != null) Ui.Line(card, Loc.T("history.rookie"), h.Rookie + "  " + Theme.Dim("P" + h.RookiePosition));
                if (h.Retired.Count > 0) Ui.Note(card, Loc.T("history.retiredList", string.Join(", ", h.Retired.ToArray())));
            }
        }

        private void Records(RectTransform list, WorldState world, ChampionshipCategory cat)
        {
            RectTransform left, right;
            Ui.Columns(list, out left, out right);
            var rc = Ui.Card(left, Loc.T("history.records"), cat.Name);
            CategoryRecords rec;
            world.Records.TryGetValue(cat.Id, out rec);
            if (rec == null || rec.YoungestWinner == null) UiFactory.Paragraph(rc, Theme.Dim(Loc.T("history.noRecords")), Theme.FontSmall - 2);
            else
            {
                Ui.Line(rc, Loc.T("history.youngest"), "<b>" + rec.YoungestWinner.Name + "</b>  " + Theme.Dim(Loc.T("history.age", rec.YoungestWinner.Value.ToString("0.0", Loc.Culture), rec.YoungestWinner.Season)));
                Ui.Line(rc, Loc.T("history.oldest"), "<b>" + rec.OldestWinner.Name + "</b>  " + Theme.Dim(Loc.T("history.age", rec.OldestWinner.Value.ToString("0.0", Loc.Culture), rec.OldestWinner.Season)));
                if (rec.LongestStreak != null)
                    Ui.Line(rc, Loc.T("history.streak"), "<b>" + rec.LongestStreak.Name + "</b>  " + Theme.Dim(Loc.T("history.streakValue", rec.LongestStreak.Value.ToString("0"), rec.LongestStreak.Season)));
            }
            // Career leaders in this championship.
            var leaders = new List<KeyValuePair<Rider, int>>();
            foreach (var r in world.Riders.Values)
            {
                int wins = 0;
                foreach (var h in r.CareerHistory) if (h.CategoryId == cat.Id) wins += h.Wins;
                if (wins > 0) leaders.Add(new KeyValuePair<Rider, int>(r, wins));
            }
            leaders.Sort((a, b) => b.Value.CompareTo(a.Value));
            var lc = Ui.Card(left, Loc.T("history.mostWins"));
            for (int i = 0; i < leaders.Count && i < 6; i++)
                Ui.Line(lc, leaders[i].Key.FullName + (leaders[i].Key.IsRetired ? " " + Theme.Dim(Loc.T("history.retired")) : ""), "<b>" + leaders[i].Value + "</b>");
            if (leaders.Count == 0) UiFactory.Paragraph(lc, Theme.Dim(Loc.T("history.noRecords")), Theme.FontSmall - 2);

            var rivals = Ui.Card(right, Loc.T("history.rivalries"), Loc.T("history.rivalriesSub"));
            var pairs = HistoryService.Rivalries(world, cat.Id);
            foreach (var p in pairs) Ui.Line(rivals, p.A + "  " + Theme.Dim("vs") + "  " + p.B, "<b>" + p.Duels + "</b>");
            if (pairs.Count == 0) UiFactory.Paragraph(rivals, Theme.Dim(Loc.T("history.noRecords")), Theme.FontSmall - 2);
        }

        private void Hall(RectTransform list, WorldState world, Core.Data.GameDatabase db)
        {
            var hall = HistoryService.HallOfFame(world, db);
            var card = Ui.Card(list, Loc.T("history.hall"), Loc.T("history.hallSub"));
            if (hall.Count == 0)
            {
                UiFactory.Paragraph(card, Theme.Dim(Loc.T("history.hallEmpty")), Theme.FontSmall - 2);
                return;
            }
            var w = new[] { 0.7f, 4f, 1.6f, 1.6f, 1.6f, 1.6f };
            UiFactory.TableRow(card, 38, w, new[] { "#", Loc.T("col.rider"), Loc.T("history.col.topTitles"), Loc.T("col.titles"), Loc.T("history.col.topWins"), Loc.T("col.wins") }, true);
            for (int i = 0; i < hall.Count; i++)
            {
                var e = hall[i];
                UiFactory.TableRow(card, 40, w, new[] { (i + 1).ToString(), "<b>" + e.Rider.FullName + "</b>" + (e.Rider.IsRetired ? "" : " " + Theme.Dim(Loc.T("history.active"))),
                    e.TopTitles.ToString(), e.Titles.ToString(), e.TopWins.ToString(), e.Wins.ToString() });
            }
        }
    }
}
