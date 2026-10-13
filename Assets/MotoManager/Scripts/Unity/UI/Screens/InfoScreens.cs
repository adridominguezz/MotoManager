using System;
using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// Standings (UX spec §3.17): one row of controls (table segmented + category picker), then a timing-tower list:
    /// a band with the category and round, column captions (# | name | team | wins | points) and tall rows with big
    /// bold text. The player's row is lit in the accent and pinned under the list whenever it is out of view. A row
    /// opens a sheet with points, wins, podiums and poles. EVOLUCIÓN shows the points chart.
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

            var cats = new List<ChampionshipCategory>();
            foreach (var c in Game.Db.CategoriesOrdered()) if (c.Simulated) cats.Add(c);
            var labels = new List<string>();
            int sel = 0;
            for (int i = 0; i < cats.Count; i++)
            {
                labels.Add(cats[i].ShortName);
                if (cats[i].Id == _category) sel = i;
            }
            var top = Ui.Columns12(Wp4Ui.TopStrip(content, Ui.Grid.Hit), 9, 3);
            Ui.Segmented(Wp4Ui.ListIn(top[0], 0), new[] { Loc.T("standings.riders"), Loc.T("standings.teams"), Loc.T("standings.makers"), Loc.T("standings.evolution") },
                _table, i => { _table = i; Shell.Refresh(); });
            Ui.Picker(top[1], Loc.T("ux.market.category"), labels, sel, i => { _category = cats[i].Id; Shell.Refresh(); });
            var area = Wp4Ui.Below(content, Ui.Grid.Hit + Ui.Grid.Gap);

            ChampionshipState champ;
            if (!world.Championships.TryGetValue(_category, out champ))
            {
                Ui.Empty(Wp4Ui.ListIn(area), UiIcons.Trophy, Loc.T("ux.standings.none"));
                return;
            }
            if (_table == 3)
            {
                Evolution(area, world, champ);
                return;
            }
            var player = world.PlayerTeam;
            string playerTeam = world.PlayerTeamId;
            var category = Game.Db.GetCategory(_category);
            string catName = category != null ? category.ShortName : _category;
            string tableName = _table == 0 ? Loc.T("standings.riders") : _table == 1 ? Loc.T("standings.teams") : Loc.T("standings.makers");
            string round = Loc.T("home.round", champ.Rounds.Count, Game.RoundsInSeason(_category));
            int firstMine = -1;
            if (_table == 0)
            {
                var widths = new[] { 1f, 5f, 3.4f, 1.2f, 1.6f };
                var table = Wp4Ui.Table(area, catName + "  ·  " + tableName, round, widths,
                    new[] { "#", Loc.T("col.rider"), Loc.T("col.team"), Loc.T("ux.standings.wins"), Loc.T("ux.standings.pts") }, RowHeight, true);
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
                    Action tap = () => EntrySheet(name, team != null ? team.Name : null, e);
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
                    Action tap = () => EntrySheet(n, null, e);
                    Wp4Ui.BigRow(table.List, widths, cells, i, mine, tap, RowHeight);
                    if (mine && firstMine < 0) { firstMine = i; pinned = cells; pinTap = tap; }
                }
                if (rows.Count == 0) Ui.Empty(table.List, null, Loc.T("standings.noRounds"));
                int idx = firstMine;
                if (pinned != null) Wp4Ui.PinRow(table, idx, h => Wp4Ui.BigRow(h, widths, pinned, idx, true, pinTap, RowHeight));
            }
        }

        /// <summary>The figures of one standings entry.</summary>
        private static void EntrySheet(string name, string team, StandingEntry e)
        {
            Ui.Sheet(name, list =>
            {
                if (!string.IsNullOrEmpty(team)) Ui.Line(list, Loc.T("col.team"), "<b>" + team + "</b>");
                Ui.Line(list, Loc.T("col.points"), "<b>" + e.Points.ToString("0") + "</b>", true);
                Ui.Line(list, Loc.T("col.wins"), e.Wins.ToString());
                Ui.Line(list, Loc.T("col.podiums"), e.Podiums.ToString());
                Ui.Line(list, Loc.T("col.poles"), e.Poles.ToString());
            }, null, null, 0.46f);
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
    /// Calendar (UX spec §3.18): the championship (segmented, a picker beyond four), then a list: round block | date |
    /// Grand Prix name with the circuit under it | winner. The next round is lit with a "PRÓXIMA" pill and scrolled
    /// into view; past rounds are dimmed.
    /// </summary>
    public class CalendarScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.calendar"); } }
        private int _championship;
        private const float RowHeight = 72;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var keys = new List<string>(world.Calendars.Keys);
            if (keys.Count == 0)
            {
                Ui.Empty(Wp4Ui.ListIn(content), UiIcons.Flag, Loc.T("ux.calendar.none"));
                return;
            }
            var team = world.PlayerTeam;
            keys.Sort((a, b) =>
            {
                int c = Owns(b, team).CompareTo(Owns(a, team));
                return c != 0 ? c : string.CompareOrdinal(a, b);
            });
            var labels = new List<string>();
            foreach (var k in keys) labels.Add(Game.Db.Championships.ContainsKey(k) ? Game.Db.Championships[k].Name : k);
            _championship = Mathf.Clamp(_championship, 0, keys.Count - 1);
            var strip = Wp4Ui.TopStrip(content, Ui.Grid.Hit);
            if (keys.Count > 4)
                Ui.Picker(Ui.Columns12(strip, 4)[0], Loc.T("ux.calendar.championship"), labels, _championship, i => { _championship = i; Shell.Refresh(); });
            else
                Ui.Segmented(Wp4Ui.ListIn(Ui.Columns12(strip, Mathf.Min(12, 3 * keys.Count))[0], 0), labels, _championship, i => { _championship = i; Shell.Refresh(); });
            var area = Wp4Ui.Below(content, Ui.Grid.Hit + Ui.Grid.Gap);

            string key = keys[_championship];
            var cal = world.Calendars[key];
            var champ = Game.Db.Championships.ContainsKey(key) ? Game.Db.Championships[key] : null;
            int next;
            world.NextRound.TryGetValue(key, out next);
            var widths = new[] { 1f, 1.6f, 6f, 4f };
            var table = Wp4Ui.Table(area, labels[_championship], Loc.T("home.round", Mathf.Min(next + 1, cal.Rounds.Count), cal.Rounds.Count), widths,
                new[] { "R", Loc.T("col.date"), Loc.T("ux.calendar.event"), Loc.T("ux.calendar.winner") }, RowHeight, false,
                Loc.T("tab.calendar"), Loc.T("ux.calendar.info"));
            string topCat = champ != null && champ.Categories.Count > 0 ? champ.Categories[0] : null;
            for (int i = 0; i < cal.Rounds.Count; i++)
            {
                var r = cal.Rounds[i];
                var circuit = Game.Db.GetCircuit(r.CircuitId);
                bool past = i < next;
                string winner = past ? Winner(world, topCat, r.Round) : "";
                string name = "<b>" + Theme.Caps(Loc.Round(r.Name)) + "</b>\n<size=" + Theme.TextCaption + ">" +
                              Theme.Dim(circuit != null ? circuit.Name ?? circuit.ShortName : r.CircuitId) + "</size>";
                string date = Theme.Caps(r.Date.ToString("d MMM", Loc.Culture));
                var row = Ui.ListRow(table.List, widths, new[] { r.Round.ToString(), past ? Theme.Dim(date) : date, name, past ? Theme.Dim(winner) : winner },
                    i, i == next, null, RowHeight);
                foreach (var t in row.GetComponentsInChildren<UnityEngine.UI.Text>())
                    if (t.gameObject.name == "Cell2") t.lineSpacing = 0.95f;
                if (i == next) Wp4Ui.CellPill(row, widths, 3, Loc.T("ux.calendar.next"), Theme.Timing);
            }
            Wp4Ui.ScrollTo(table, next);
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

    /// <summary>
    /// History (UX spec §3.19): one control row (CAMPEONES | TEMPORADAS | RÉCORDS | SALÓN + category picker). Champions
    /// and the Hall of Fame are lists; seasons a grid of compact cards (tap for the full season); records three cards.
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
                var names = new List<string>();
                int sel = 0;
                for (int i = 0; i < cats.Count; i++)
                {
                    names.Add(cats[i].ShortName);
                    if (cats[i].Id == _category) sel = i;
                }
                Ui.Picker(top[1], Loc.T("ux.market.category"), names, sel, i => { _category = cats[i].Id; Shell.Refresh(); });
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
                Ui.ListRow(table.List, widths, new[] { c.Season.ToString(), c.RiderName, Theme.Dim(c.TeamName ?? ""), "<b>" + c.Points.ToString("0") + "</b>" }, k, mine, null, 64);
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
}
