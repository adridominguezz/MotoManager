using System;
using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// Standings (UX spec §3.17, MM4 §1.7): the table segmented (underline in the championship colour), then a
    /// timing-tower list at full width: a band in the championship's own colour with "‹ CATEGORY · TABLE ›" (the arrows
    /// go over every simulated category, managed or not; the name opens the whole list) and the round on the right,
    /// column captions (# | name | team | wins | points) and tall rows with big bold text. The player's row is lit in the accent and
    /// pinned under the list whenever it is out of view. A rider or maker row opens a sheet with points, wins, podiums
    /// and poles (a rider's has VER EQUIPO); a team row opens the team sheet (<see cref="TeamSheet"/>). EVOLUCIÓN shows
    /// the points chart.
    /// </summary>
    public class StandingsScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.standings"); } }
        private string _category;
        private int _table;

        private const float RowHeight = 72;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            if (_category == null) _category = world.PlayerTeam != null ? world.PlayerTeam.CategoryId : "motogp";

            // MM4 §1.7: the championship card with ‹ › over every simulated category on the left, the table on the right.
            var cats = ChampNav.Categories(Game.Db);
            if (cats.Count == 0)
            {
                Ui.Empty(Wp4Ui.ListIn(content), UiIcons.Trophy, Loc.T("ux.standings.none"));
                return;
            }
            int sel = cats.FindIndex(c => c.Id == _category);
            if (sel < 0)
            {
                sel = 0;
                _category = cats[0].Id;
            }
            var items = ChampNav.Wrap(cats);
            Action<int> pick = i => { _category = cats[i].Id; Shell.Refresh(); };
            Color champColor = ChampNav.ColorOf(cats[sel]);
            var tabs = Wp4Ui.ListIn(Wp4Ui.TopStrip(content, Ui.Grid.Hit), 0);
            Ui.Segmented(tabs, new[] { Loc.T("standings.riders"), Loc.T("standings.teams"), Loc.T("standings.makers"), Loc.T("standings.evolution") },
                _table, i => { _table = i; Shell.Refresh(); });
            ChampNav.TintTabs(tabs, champColor);
            var area = Wp4Ui.Below(content, Ui.Grid.Hit + Ui.Grid.Gap);

            var category = cats[sel];
            string catName = category.ShortName;
            string tableName = _table == 0 ? Loc.T("standings.riders") : _table == 1 ? Loc.T("standings.teams") : _table == 2 ? Loc.T("standings.makers") : Loc.T("standings.evolution");
            ChampionshipState champ;
            world.Championships.TryGetValue(_category, out champ);
            string round = Loc.T("home.round", champ != null ? champ.Rounds.Count : 0, Game.RoundsInSeason(_category));
            if (champ == null || _table == 3)
            {
                // No table band here: a band of our own carries the navigator, the content goes under it.
                var band = Ui.Band(Wp4Ui.ListIn(Wp4Ui.TopStrip(area, 56), 0), champ == null ? catName : catName + "  ·  " + tableName, round, 56);
                ChampNav.Navigate(band, champColor, items, sel, pick);
                var below = Wp4Ui.Below(area, 56 + Ui.Grid.Gap);
                if (champ == null) Ui.Empty(Wp4Ui.ListIn(below), UiIcons.Trophy, Loc.T("ux.standings.none"));
                else Evolution(below, world, champ);
                return;
            }
            var player = world.PlayerTeam;
            string playerTeam = world.PlayerTeamId;
            int firstMine = -1;
            if (_table == 0)
            {
                var widths = new[] { 1f, 5f, 3.4f, 1.2f, 1.6f };
                var table = Wp4Ui.Table(area, catName + "  ·  " + tableName, round, widths,
                    new[] { "#", Loc.T("col.rider"), Loc.T("col.team"), Loc.T("ux.standings.wins"), Loc.T("ux.standings.pts") }, RowHeight, true);
                ChampNav.Navigate(ChampNav.BandOf(table), champColor, items, sel, pick);
                var rows = champ.SortedRiders();
                string[] pinned = null;
                Action pinTap = null;
                for (int i = 0; i < rows.Count; i++)
                {
                    var e = rows[i];
                    var r = world.GetRider(e.Id);
                    var team = r != null ? world.GetTeam(r.TeamId) : null;
                    bool mine = team != null && team.Id == playerTeam;
                    string name = r != null ? r.FullName : e.Id;
                    var cells = new[]
                    {
                        (i + 1).ToString(), name, team != null ? Theme.Dim(team.ShortName ?? team.Name) : "-", e.Wins.ToString(), "<b>" + e.Points.ToString("0") + "</b>"
                    };
                    string teamId = team != null ? team.Id : null;
                    Action tap = () => EntrySheet(name, team != null ? team.Name : null, e, teamId);
                    Wp4Ui.BigRow(table.List, widths, cells, i, mine, tap, RowHeight);
                    if (mine && firstMine < 0) { firstMine = i; pinned = cells; pinTap = tap; }
                }
                if (rows.Count == 0) Ui.Empty(table.List, null, Loc.T("standings.noRounds"));
                int idx = firstMine;
                if (pinned != null) Wp4Ui.PinRow(table, idx, h => Wp4Ui.BigRow(h, widths, pinned, idx, true, pinTap, RowHeight));
            }
            else
            {
                var rows = _table == 1 ? champ.SortedTeams() : champ.SortedManufacturers();
                var widths = new[] { 1f, 8.4f, 1.2f, 1.6f };
                var table = Wp4Ui.Table(area, catName + "  ·  " + tableName, round, widths,
                    new[] { "#", _table == 1 ? Loc.T("col.team") : Loc.T("col.maker"), Loc.T("ux.standings.wins"), Loc.T("ux.standings.pts") }, RowHeight, true);
                ChampNav.Navigate(ChampNav.BandOf(table), champColor, items, sel, pick);
                string[] pinned = null;
                Action pinTap = null;
                for (int i = 0; i < rows.Count; i++)
                {
                    var e = rows[i];
                    string name = e.Id;
                    if (_table == 1) { var t = world.GetTeam(name); if (t != null) name = t.Name; }
                    else { var mk = world.GetManufacturer(name); if (mk != null) name = mk.Name; }
                    bool mine = _table == 1 ? e.Id == playerTeam : player != null && e.Id == player.ManufacturerId && player.CategoryId == _category;
                    var cells = new[] { (i + 1).ToString(), name, e.Wins.ToString(), "<b>" + e.Points.ToString("0") + "</b>" };
                    string n = name;
                    string id = e.Id;
                    // A team row opens the team sheet (‹ › over the category's teams, history); a maker row its figures.
                    Action tap = _table == 1 ? (Action)(() => TeamSheet.Open(Game, id)) : () => EntrySheet(n, null, e, null);
                    Wp4Ui.BigRow(table.List, widths, cells, i, mine, tap, RowHeight);
                    if (mine && firstMine < 0) { firstMine = i; pinned = cells; pinTap = tap; }
                }
                if (rows.Count == 0) Ui.Empty(table.List, null, Loc.T("standings.noRounds"));
                int idx = firstMine;
                if (pinned != null) Wp4Ui.PinRow(table, idx, h => Wp4Ui.BigRow(h, widths, pinned, idx, true, pinTap, RowHeight));
            }
        }

        /// <summary>The figures of one standings entry; for a rider with a team, VER EQUIPO opens the team sheet.</summary>
        private void EntrySheet(string name, string team, StandingEntry e, string teamId)
        {
            var game = Game;
            UiAct? open = null;
            if (teamId != null) open = UiFactory.Act(Loc.T("ux.mm4b.viewTeam"), () => TeamSheet.Open(game, teamId));
            Ui.Sheet(name, list =>
            {
                if (!string.IsNullOrEmpty(team)) Ui.Line(list, Loc.T("col.team"), "<b>" + team + "</b>");
                Ui.Line(list, Loc.T("col.points"), "<b>" + e.Points.ToString("0") + "</b>", true);
                Ui.Line(list, Loc.T("col.wins"), e.Wins.ToString());
                Ui.Line(list, Loc.T("col.podiums"), e.Podiums.ToString());
                Ui.Line(list, Loc.T("col.poles"), e.Poles.ToString());
            }, null, open, 0.46f);
        }

        /// <summary>Points round by round: the top five and the player's riders.</summary>
        private void Evolution(RectTransform area, WorldState world, ChampionshipState champ)
        {
            var body = Ui.Tile(area, Loc.T("standings.evolution"), null, null, 0, null, Wp4Ui.Info(Loc.T("standings.evolution"), Loc.T("standings.evolutionSub")));
            if (champ.Rounds.Count == 0)
            {
                Ui.Empty(Wp4Ui.ListIn(body), UiIcons.Trophy, Loc.T("ux.standings.noRounds"));
                return;
            }
            var list = Wp4Ui.ScrollIn(body, 8);
            var table = champ.SortedRiders();
            var ids = new List<string>();
            for (int i = 0; i < table.Count && i < 5; i++) ids.Add(table[i].Id);
            var playerTeam = world.PlayerTeam;
            if (playerTeam != null)
                foreach (var id in playerTeam.RiderIds)
                    if (!ids.Contains(id) && champ.Riders.ContainsKey(id)) ids.Add(id);
            var series = new List<ChartSeries>();
            foreach (var id in ids)
            {
                var r = world.GetRider(id);
                var team = r != null ? world.GetTeam(r.TeamId) : null;
                bool mine = playerTeam != null && team == playerTeam;
                var s = new ChartSeries
                {
                    Name = (r != null ? r.ShortName : id) + " " + Theme.Dim(champ.RiderPosition(id) > 0 ? "P" + champ.RiderPosition(id) : ""),
                    Color = mine ? Theme.Timing : Charts.Readable(team != null ? team.Colors.Primary : null, Color.gray),
                    Highlight = mine
                };
                double total = 0;
                s.Values.Add(0);
                foreach (var round in champ.Rounds)
                {
                    foreach (var session in round.Sessions)
                        foreach (var e in session.Classification)
                            if (e.RiderId == id) total += e.Points;
                    s.Values.Add(total);
                }
                series.Add(s);
            }
            float h = Mathf.Max(300, body.rect.height - 110);
            Charts.LineChart(list, h, series, false, v => v.ToString("0"), Loc.T("standings.start"), Loc.T("standings.roundN", champ.Rounds.Count));
        }
    }

    /// <summary>
    /// Calendar (UX spec §3.18, MM4 §1.7): a full-width list whose band, in the championship's colour, is the navigator
    /// "‹ CATEGORY ›" over every simulated category (and any calendar no category uses; the name opens the whole list),
    /// then rows: round block | date | Grand Prix name with the circuit under it | winner of the chosen category. Only the rounds that category races are listed. The
    /// next round is lit with a "PRÓXIMA" pill and scrolled into view; past rounds are dimmed.
    /// </summary>
    public class CalendarScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.calendar"); } }
        /// <summary>The navigator entry: a category id, or a calendar key for a calendar no simulated category uses.</summary>
        private string _entry;
        private const float RowHeight = 72;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            if (world.Calendars.Count == 0)
            {
                Ui.Empty(Wp4Ui.ListIn(content), UiIcons.Flag, Loc.T("ux.calendar.none"));
                return;
            }
            // MM4 §1.7: one navigator entry per simulated category that has a calendar (its own rounds and winners),
            // then any calendar no simulated category uses (its whole calendar, winners of its top category, as before).
            var items = new List<ChampNav.Item>();
            var used = new HashSet<string>();
            foreach (var c in ChampNav.Categories(Game.Db))
                if (c.CalendarGroup != null && world.Calendars.ContainsKey(c.CalendarGroup))
                {
                    items.Add(new ChampNav.Item { Cat = c, Key = c.CalendarGroup, Name = c.Name });
                    used.Add(c.CalendarGroup);
                }
            var orphans = new List<string>(world.Calendars.Keys);
            orphans.Sort(string.CompareOrdinal);
            foreach (var k in orphans)
                if (!used.Contains(k))
                    items.Add(new ChampNav.Item { Key = k, Name = Game.Db.Championships.ContainsKey(k) ? Game.Db.Championships[k].Name : k });
            if (_entry == null && world.PlayerTeam != null) _entry = world.PlayerTeam.CategoryId;
            int sel = items.FindIndex(x => x.Id == _entry);
            if (sel < 0) sel = 0;
            _entry = items[sel].Id;

            var area = content;
            var item = items[sel];
            string key = item.Key;
            var cal = world.Calendars[key];
            var champ = Game.Db.Championships.ContainsKey(key) ? Game.Db.Championships[key] : null;
            int next;
            world.NextRound.TryGetValue(key, out next);
            string cat = item.Cat != null ? item.Cat.Id : champ != null && champ.Categories.Count > 0 ? champ.Categories[0] : null;
            // The rounds this category races (all of them for a calendar without a category); the next one is lit.
            var rounds = new List<int>();
            int nextRow = -1, done = 0;
            for (int i = 0; i < cal.Rounds.Count; i++)
            {
                var rc = cal.Rounds[i].Categories;
                if (item.Cat != null && rc != null && rc.Count > 0 && !rc.Contains(item.Cat.Id)) continue;
                if (i < next) done++;
                else if (nextRow < 0) nextRow = rounds.Count;
                rounds.Add(i);
            }
            var widths = new[] { 1f, 1.6f, 6f, 4f };
            var table = Wp4Ui.Table(area, item.Cat != null ? item.Cat.ShortName : item.Name, Loc.T("home.round", Mathf.Min(done + 1, rounds.Count), rounds.Count), widths,
                new[] { "R", Loc.T("col.date"), Loc.T("ux.calendar.event"), Loc.T("ux.calendar.winner") }, RowHeight, false,
                Loc.T("tab.calendar"), Loc.T("ux.mm4b.calendarInfo"));
            ChampNav.Navigate(ChampNav.BandOf(table), item.Cat != null ? ChampNav.ColorOf(item.Cat) : Theme.Steel, items, sel,
                i => { _entry = items[i].Id; Shell.Refresh(); });
            for (int k = 0; k < rounds.Count; k++)
            {
                int i = rounds[k];
                var r = cal.Rounds[i];
                var circuit = Game.Db.GetCircuit(r.CircuitId);
                bool past = i < next;
                string winner = past ? Winner(world, cat, r.Round) : "";
                string name = "<b>" + Theme.Caps(Loc.Round(r.Name)) + "</b>\n<size=" + Theme.TextCaption + ">" +
                              Theme.Dim(circuit != null ? circuit.Name ?? circuit.ShortName : r.CircuitId) + "</size>";
                string date = Theme.Caps(r.Date.ToString("d MMM", Loc.Culture));
                var row = Ui.ListRow(table.List, widths, new[] { r.Round.ToString(), past ? Theme.Dim(date) : date, name, past ? Theme.Dim(winner) : winner },
                    k, k == nextRow, null, RowHeight);
                foreach (var t in row.GetComponentsInChildren<UnityEngine.UI.Text>())
                    if (t.gameObject.name == "Cell2") t.lineSpacing = 0.95f;
                if (k == nextRow) Wp4Ui.CellPill(row, widths, 3, Loc.T("ux.calendar.next"), Theme.Timing);
            }
            if (rounds.Count == 0) Ui.Empty(table.List, UiIcons.Flag, Loc.T("ux.calendar.none"));
            Wp4Ui.ScrollTo(table, nextRow < 0 ? rounds.Count - 1 : nextRow);
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

    /// <summary>
    /// History (UX spec §3.19): one control row (CAMPEONES | TEMPORADAS | RÉCORDS | SALÓN + the category carousel ‹ ›,
    /// MM4 §1.7; its name opens the whole list). Champions and the Hall of Fame are lists (a champion opens his team's
    /// sheet); seasons a grid of compact cards (tap for the full season); records three cards.
    /// </summary>
    public class HistoryScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.history"); } }
        private int _view;
        private string _category;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var db = Game.Db;
            var cats = new List<ChampionshipCategory>();
            foreach (var c in db.CategoriesOrdered()) if (c.Simulated) cats.Add(c);
            if (_category == null) _category = world.PlayerTeam != null ? world.PlayerTeam.CategoryId : cats[0].Id;

            var top = Ui.Columns12(Wp4Ui.TopStrip(content, Ui.Grid.Hit), 9, 3);
            Ui.Segmented(Wp4Ui.ListIn(top[0], 0), new[] { Loc.T("history.view.champions"), Loc.T("history.view.seasons"), Loc.T("history.view.records"), Loc.T("ux.history.hall") },
                _view, i => { _view = i; Shell.Refresh(); });
            if (_view != 3)
            {
                // MM4 §1.7: ‹ › over every simulated category; the name opens the whole list (the old picker's job).
                int sel = Mathf.Max(0, cats.FindIndex(c => c.Id == _category));
                _category = cats[sel].Id;
                int n = cats.Count;
                Action<int> pick = i => { _category = cats[i].Id; Shell.Refresh(); };
                Carousel.Compact(top[1], cats[sel].ShortName,
                    n > 1 ? () => pick((sel - 1 + n) % n) : (Action)null,
                    n > 1 ? () => pick((sel + 1) % n) : (Action)null,
                    () => ChampNav.PickSheet(ChampNav.Wrap(cats), sel, pick), ChampNav.ColorOf(cats[sel]));
            }
            var area = Wp4Ui.Below(content, Ui.Grid.Hit + Ui.Grid.Gap);
            var cat = db.GetCategory(_category);
            switch (_view)
            {
                case 1: Seasons(area, world, cat); break;
                case 2: Records(area, world, cat); break;
                case 3: Hall(area, world, db); break;
                default: Champions(area, world, cat); break;
            }
        }

        private void Champions(RectTransform area, WorldState world, ChampionshipCategory cat)
        {
            var champs = world.Champions.FindAll(c => c.CategoryId == cat.Id);
            var widths = new[] { 1.4f, 5f, 4.4f, 1.6f };
            var table = Wp4Ui.Table(area, cat.Name, Loc.T("ux.history.titles", champs.Count), widths,
                new[] { Loc.T("ux.history.season"), Loc.T("col.champion"), Loc.T("col.team"), Loc.T("ux.standings.pts") }, 64, false);
            if (champs.Count == 0)
            {
                Ui.Empty(table.List, UiIcons.Trophy, Loc.T("ux.history.empty"));
                return;
            }
            int k = 0;
            for (int i = champs.Count - 1; i >= 0; i--, k++)
            {
                var c = champs[i];
                bool mine = world.PlayerTeamId != null && c.TeamId == world.PlayerTeamId;
                // A champion's row opens his team's sheet (with its history) while the team still exists.
                string teamId = c.TeamId;
                var game = Game;
                Action tap = world.GetTeam(teamId) != null ? () => TeamSheet.Open(game, teamId) : (Action)null;
                Ui.ListRow(table.List, widths, new[] { c.Season.ToString(), c.RiderName, Theme.Dim(c.TeamName ?? ""), "<b>" + c.Points.ToString("0") + "</b>" }, k, mine, tap, 64);
            }
        }

        private void Seasons(RectTransform area, WorldState world, ChampionshipCategory cat)
        {
            var seasons = world.History.FindAll(h => h.CategoryId == cat.Id);
            var list = Wp4Ui.ScrollIn(area, Ui.Grid.Gap);
            if (seasons.Count == 0)
            {
                Ui.Empty(list, UiIcons.Trophy, Loc.T("ux.history.empty"));
                return;
            }
            RectTransform[] cols = null;
            for (int i = seasons.Count - 1, k = 0; i >= 0; i--, k++)
            {
                if (k % 3 == 0) cols = Ui.Columns12(UiFactory.Row(list, 216), 4, 4, 4);
                var h = seasons[i];
                var maker = world.GetManufacturer(h.MakerChampionId);
                string title = h.Season + " · " + cat.ShortName;
                var body = Wp4Ui.ListIn(Ui.Tile(cols[k % 3], title, () => Ui.Sheet(title, l =>
                {
                    Ui.Line(l, "1º", "<b>" + h.Champion + "</b>  " + Theme.Dim(h.ChampionPoints.ToString("0") + " pts"));
                    if (h.Second != null) Ui.Line(l, "2º", h.Second + "  " + Theme.Dim("-" + (h.ChampionPoints - h.SecondPoints).ToString("0")));
                    if (h.Third != null) Ui.Line(l, "3º", h.Third);
                    if (h.TeamChampion != null) Ui.Line(l, Loc.T("history.teamTitle"), h.TeamChampion);
                    if (maker != null) Ui.Line(l, Loc.T("history.makerTitle"), maker.Name);
                    if (h.MostWins != null) Ui.Line(l, Loc.T("history.mostWinsSeason"), h.MostWins + "  " + Theme.Dim(h.MostWinsCount.ToString()));
                    if (h.Rookie != null) Ui.Line(l, Loc.T("history.rookie"), h.Rookie + "  " + Theme.Dim("P" + h.RookiePosition));
                    Ui.Line(l, Loc.T("ux.history.races"), h.Races.ToString());
                    if (h.Retired.Count > 0) UiFactory.Paragraph(l, Theme.Dim(Loc.T("history.retiredList", string.Join(", ", h.Retired.ToArray()))), Theme.TextBody);
                }, null, null, 0.5f)), 4);
                Ui.Line(body, "1º", "<b>" + h.Champion + "</b>");
                Ui.Line(body, "2º", h.Second ?? "-");
                Ui.Line(body, "3º", h.Third ?? "-");
            }
        }

        private void Records(RectTransform area, WorldState world, ChampionshipCategory cat)
        {
            var cols = Ui.Columns12(area, 6, 6);
            var leftRows = Ui.Rows(cols[0], 0.42f, 0.58f);
            var rc = Wp4Ui.ListIn(Ui.Tile(leftRows[0], Loc.T("history.records"), null, Theme.Caps(cat.ShortName)), 2);
            CategoryRecords rec;
            world.Records.TryGetValue(cat.Id, out rec);
            if (rec == null || rec.YoungestWinner == null) Ui.Empty(rc, null, Loc.T("ux.history.noRecords"));
            else
            {
                Ui.Line(rc, Loc.T("history.youngest"), "<b>" + rec.YoungestWinner.Name + "</b>  " + Theme.Dim(Loc.T("history.age", rec.YoungestWinner.Value.ToString("0.0", Loc.Culture), rec.YoungestWinner.Season)));
                Ui.Line(rc, Loc.T("history.oldest"), "<b>" + rec.OldestWinner.Name + "</b>  " + Theme.Dim(Loc.T("history.age", rec.OldestWinner.Value.ToString("0.0", Loc.Culture), rec.OldestWinner.Season)));
                if (rec.LongestStreak != null)
                    Ui.Line(rc, Loc.T("history.streak"), "<b>" + rec.LongestStreak.Name + "</b>  " + Theme.Dim(Loc.T("history.streakValue", rec.LongestStreak.Value.ToString("0"), rec.LongestStreak.Season)));
            }
            var leaders = new List<KeyValuePair<Rider, int>>();
            foreach (var r in world.Riders.Values)
            {
                int wins = 0;
                foreach (var h in r.CareerHistory) if (h.CategoryId == cat.Id) wins += h.Wins;
                if (wins > 0) leaders.Add(new KeyValuePair<Rider, int>(r, wins));
            }
            leaders.Sort((a, b) => b.Value.CompareTo(a.Value));
            var lc = Wp4Ui.ScrollIn(Ui.Tile(leftRows[1], Loc.T("history.mostWins")), 2);
            for (int i = 0; i < leaders.Count && i < 10; i++)
                Ui.Line(lc, leaders[i].Key.FullName + (leaders[i].Key.IsRetired ? " " + Theme.Dim(Loc.T("history.retired")) : ""), "<b>" + leaders[i].Value + "</b>");
            if (leaders.Count == 0) Ui.Empty(lc, null, Loc.T("ux.history.noRecords"));

            var rivals = Wp4Ui.ScrollIn(Ui.Tile(cols[1], Loc.T("history.rivalries"), null, null, 0, null, Wp4Ui.Info(Loc.T("history.rivalries"), Loc.T("history.rivalriesSub"))), 2);
            var pairs = HistoryService.Rivalries(world, cat.Id);
            foreach (var p in pairs) Ui.Line(rivals, p.A + "  " + Theme.Dim("vs") + "  " + p.B, "<b>" + p.Duels + "</b>");
            if (pairs.Count == 0) Ui.Empty(rivals, null, Loc.T("ux.history.noRecords"));
        }

        private void Hall(RectTransform area, WorldState world, GameDatabase db)
        {
            var hall = HistoryService.HallOfFame(world, db);
            var widths = new[] { 1f, 7f, 2f, 2f };
            var table = Wp4Ui.Table(area, Loc.T("history.hall"), null, widths, new[] { "#", Loc.T("col.rider"), Loc.T("col.titles"), Loc.T("col.wins") }, 64, true,
                Loc.T("history.hall"), Loc.T("history.hallSub"));
            if (hall.Count == 0)
            {
                Ui.Empty(table.List, UiIcons.Star, Loc.T("ux.history.hallEmpty"));
                return;
            }
            for (int i = 0; i < hall.Count; i++)
            {
                var e = hall[i];
                Ui.ListRow(table.List, widths, new[] { (i + 1).ToString(), e.Rider.FullName + (e.Rider.IsRetired ? "" : " " + Theme.Dim(Loc.T("history.active"))),
                    "<b>" + e.Titles + "</b>", e.Wins.ToString() }, i, false, () => Ui.Sheet(e.Rider.FullName, l =>
                {
                    Ui.Line(l, Loc.T("history.col.topTitles"), "<b>" + e.TopTitles + "</b>");
                    Ui.Line(l, Loc.T("col.titles"), e.Titles.ToString());
                    Ui.Line(l, Loc.T("history.col.topWins"), "<b>" + e.TopWins + "</b>");
                    Ui.Line(l, Loc.T("col.wins"), e.Wins.ToString());
                    Ui.Line(l, Loc.T("ux.history.status"), e.Rider.IsRetired ? Loc.T("ux.history.retired") : Loc.T("ux.history.active"));
                }, null, null, 0.46f), 64);
            }
        }
    }

    /// <summary>
    /// The championship navigator (MM4 §1.7, docs/MM4_REFERENCIA.md part 2) shared by standings, calendar and history:
    /// every simulated category of the database in pyramid / tier order (managed by the player or not).
    /// </summary>
    internal static class ChampNav
    {
        /// <summary>A navigator entry: a category, or (calendar only) a calendar no simulated category uses.</summary>
        public class Item
        {
            public ChampionshipCategory Cat;
            public string Key;
            public string Name;
            public string Id { get { return Cat != null ? Cat.Id : "cal:" + Key; } }
        }

        public static List<ChampionshipCategory> Categories(GameDatabase db)
        {
            var cats = new List<ChampionshipCategory>();
            foreach (var c in db.CategoriesOrdered()) if (c.Simulated) cats.Add(c);
            return cats;
        }

        public static List<Item> Wrap(List<ChampionshipCategory> cats)
        {
            var items = new List<Item>();
            foreach (var c in cats) items.Add(new Item { Cat = c, Key = c.CalendarGroup, Name = c.Name });
            return items;
        }

        /// <summary>"Gran Premio · Categoría 2 (· Copa)".</summary>
        public static string Sub(ChampionshipCategory cat)
        {
            return Loc.T("ux.mm4b.pyramid." + cat.Pyramid) + "  ·  " + Loc.T("ux.mm4b.tier", cat.Tier) + (cat.IsCup ? "  ·  " + Loc.T("ux.mm4b.cup") : "");
        }

        /// <summary>The championship's own colour (categories.json: MotoGP red, Moto2 blue...).</summary>
        public static Color ColorOf(ChampionshipCategory cat)
        {
            return cat != null ? UiFactory.ParseColor(cat.Color, Theme.Steel) : Theme.Steel;
        }

        /// <summary>The accent band of a <see cref="Wp4Ui.Table"/> (first row of its header strip).</summary>
        public static RectTransform BandOf(Wp4Table table)
        {
            var top = table.Area.Find("Top");
            return top != null && top.childCount > 0 ? top.GetChild(0) as RectTransform : null;
        }

        /// <summary>The selected tab's underline in the championship colour (made readable on the dark page).</summary>
        public static void TintTabs(Transform tabs, Color color)
        {
            Color ink = Theme.Readable(color, Theme.Background, 3f);
            foreach (var img in tabs.GetComponentsInChildren<Image>(true))
                if (img.gameObject.name == "Underline") img.color = ink;
        }

        /// <summary>
        /// Turns a <see cref="Ui.Band"/> into the championship navigator (MM4 §1.7): the band in
        /// <paramref name="color"/> with readable ink, "‹ TITLE ›" on the left (the arrows wrap over every entry; tapping
        /// the title opens the whole list), its right-hand figure (the round) unchanged.
        /// </summary>
        public static void Navigate(RectTransform band, Color color, List<Item> items, int index, Action<int> select)
        {
            if (band == null) return;
            int n = items.Count;
            var bg = band.GetComponent<Image>();
            if (bg != null) bg.color = color;
            Color ink = Theme.OnColor(color);
            foreach (var t in band.GetComponentsInChildren<Text>(true))
                if (t.gameObject.name == "Title" || t.gameObject.name == "Right") t.color = ink;
            Action prev = n > 1 ? () => select((index - 1 + n) % n) : (Action)null;
            Action next = n > 1 ? () => select((index + 1) % n) : (Action)null;
            Carousel.Arrow(band, false, prev, ink);
            var titleRt = band.Find("Title") as RectTransform;
            if (titleRt == null) return;
            var title = titleRt.GetComponent<Text>();
            // The title sits after ‹ and is as wide as its text (at most 900), then ›.
            float x0 = Ui.Grid.Hit + 4;
            float w = Mathf.Clamp(title.preferredWidth + 12, 120, 900);
            titleRt.anchorMin = new Vector2(0, 0);
            titleRt.anchorMax = new Vector2(0, 1);
            titleRt.offsetMin = new Vector2(x0, 0);
            titleRt.offsetMax = new Vector2(x0 + w, 0);
            var right = Carousel.Arrow(band, false, next, ink);
            var rrt = (RectTransform)right.transform;
            rrt.anchoredPosition = new Vector2(x0 + w + Ui.Grid.Hit / 2, 0);
            rrt.localRotation = Quaternion.Euler(0, 0, 180);
            right.gameObject.name = "Next";
            if (n > 1)
            {
                title.raycastTarget = true;
                var b = title.gameObject.AddComponent<Button>();
                b.targetGraphic = title;
                var colors = b.colors;
                colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
                b.colors = colors;
                b.onClick.AddListener(() =>
                {
                    GameAudio.ClickSound();
                    PickSheet(items, index, select);
                });
            }
        }

        /// <summary>A sheet listing every entry (the chosen one lit); a row picks it.</summary>
        public static void PickSheet(List<Item> items, int index, Action<int> select)
        {
            Ui.Sheet(Loc.T("ux.mm4b.all"), list =>
            {
                var widths = new[] { 4f, 4f };
                for (int i = 0; i < items.Count; i++)
                {
                    int k = i;
                    var c = items[i].Cat;
                    Wp4Ui.SelectRow(list, widths, new[] { c != null ? c.ShortName : items[i].Name, c != null ? Theme.Dim(Sub(c)) : "" }, i, i == index, () =>
                    {
                        Ui.CloseSheet();
                        select(k);
                    }, Ui.Grid.Hit);
                }
            }, null, null, 0.46f);
        }
    }
}
