using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// The team (home's DETALLES): name, category, bike and reputation, the all-time figures (titles, wins, podiums,
    /// races) and a season-by-season table (category, position, points, wins, podiums, riders). The team's seasons are
    /// rebuilt from the riders' season records, so every season the team ran is there, before the player too.
    /// </summary>
    public class TeamDetailPage : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.team"); } }

        /// <summary>One season of the team: totals of its riders' records and its place among the teams.</summary>
        private class TeamSeason
        {
            public int Season;
            public string CategoryId;
            public double Points;
            public int Starts, Wins, Podiums, Position;
            public readonly List<string> Riders = new List<string>();
        }

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            var list = Page(content);
            if (team == null)
            {
                Ui.Empty(list, UiIcons.Team, Loc.T("shell.noTeam"));
                return;
            }
            var seasons = Seasons(world, team);

            // Header: category, bike maker, country and reputation, the livery editor.
            var cat = Game.Db.GetCategory(team.CategoryId);
            Manufacturer maker;
            Game.Db.Manufacturers.TryGetValue(team.ManufacturerId ?? "", out maker);
            var card = Ui.Card(list, team.Name, cat != null ? cat.ShortName : null);
            Ui.Line(card, Loc.T("ux.team2.bike"), maker != null ? maker.Name : "-");
            Ui.Line(card, Loc.T("ux.team2.country"), string.IsNullOrEmpty(team.Country) ? "-" : team.Country.ToUpperInvariant());
            Ui.Line(card, Loc.T("ux.team2.reputation"), team.Reputation.ToString("0"));
            var edit = UiFactory.Row(card, Ui.Grid.Button);
            var ec = Ui.Columns12(edit, 6, 6);
            Ui.Secondary(ec[0], Loc.T("ux.team2.livery"), () => Shell.Show<LiveryEditorPage>());
            Ui.Secondary(ec[1], Loc.T("ux.team2.manager"), () => Shell.Show<ManagerPage>());

            // All-time figures.
            int titles = 0, wins = 0, podiums = 0, starts = 0;
            foreach (var s in seasons)
            {
                if (s.Position == 1 && s.Season < world.Season) titles++;
                wins += s.Wins;
                podiums += s.Podiums;
                starts += s.Starts;
            }
            Ui.Section(list, Loc.T("ux.team2.allTime"));
            Ui.Kpis(list, Ui.Kpi(titles.ToString(), Loc.T("ux.team2.titles")), Ui.Kpi(wins.ToString(), Loc.T("ux.team2.wins")),
                Ui.Kpi(podiums.ToString(), Loc.T("ux.team2.podiums")), Ui.Kpi(seasons.Count.ToString(), Loc.T("ux.team2.seasons")));

            // Season by season, newest first.
            Ui.Section(list, Loc.T("ux.team2.history"));
            if (seasons.Count == 0)
            {
                Ui.Empty(list, null, Loc.T("ux.team2.noHistory"));
                return;
            }
            var widths = new[] { 0.1f, 0.14f, 0.1f, 0.12f, 0.1f, 0.1f, 0.34f };
            Ui.ListCaptions(list, widths, new[] { Loc.T("ux.team2.col.year"), Loc.T("ux.team2.col.category"), Loc.T("ux.team2.col.pos"),
                Loc.T("ux.team2.col.points"), Loc.T("ux.team2.col.wins"), Loc.T("ux.team2.col.podiums"), Loc.T("ux.team2.col.riders") });
            for (int i = 0; i < seasons.Count; i++)
            {
                var s = seasons[i];
                var c = Game.Db.GetCategory(s.CategoryId);
                Ui.ListRow(list, widths, new[]
                {
                    s.Season.ToString(), c != null ? c.ShortName : s.CategoryId, s.Position > 0 ? Theme.Pos(s.Position) : "-",
                    "<b>" + s.Points.ToString("0") + "</b>", s.Wins.ToString(), s.Podiums.ToString(), string.Join(", ", s.Riders.ToArray())
                }, i, s.Season == world.Season);
            }
        }

        /// <summary>
        /// The team's seasons from every rider's records (the current season from the live championship), with its
        /// place among all teams of that season and category. Newest first.
        /// </summary>
        private static List<TeamSeason> Seasons(WorldState world, Team team)
        {
            // Totals of every team for every (season, category), to rank them.
            var all = new Dictionary<string, TeamSeason>();
            foreach (var r in world.Riders.Values)
                foreach (var rec in r.CareerHistory)
                {
                    if (string.IsNullOrEmpty(rec.TeamId) || rec.Season >= world.Season) continue;
                    string key = rec.Season + "|" + rec.CategoryId + "|" + rec.TeamId;
                    TeamSeason ts;
                    if (!all.TryGetValue(key, out ts))
                    {
                        ts = new TeamSeason { Season = rec.Season, CategoryId = rec.CategoryId };
                        all[key] = ts;
                    }
                    ts.Points += rec.Points;
                    ts.Starts += rec.Starts;
                    ts.Wins += rec.Wins;
                    ts.Podiums += rec.Podiums;
                    if (rec.TeamId == team.Id) ts.Riders.Add(r.LastName);
                }
            var mine = new List<TeamSeason>();
            foreach (var kv in all)
            {
                if (!kv.Key.EndsWith("|" + team.Id)) continue;
                var ts = kv.Value;
                int pos = 1;
                foreach (var other in all.Values)
                    if (other != ts && other.Season == ts.Season && other.CategoryId == ts.CategoryId && other.Points > ts.Points) pos++;
                ts.Position = pos;
                mine.Add(ts);
            }

            // The season in progress, from the championship.
            ChampionshipState champ;
            if (world.Championships.TryGetValue(team.CategoryId, out champ))
            {
                var now = new TeamSeason { Season = world.Season, CategoryId = team.CategoryId };
                var table = champ.SortedTeams();
                for (int i = 0; i < table.Count; i++)
                    if (table[i].Id == team.Id)
                    {
                        now.Position = i + 1;
                        now.Points = table[i].Points;
                    }
                foreach (var id in team.RiderIds)
                {
                    var r = world.GetRider(id);
                    if (r != null) now.Riders.Add(r.LastName);
                }
                // Starts, wins and podiums of the team's entries in this season's races so far.
                foreach (var wk in champ.Rounds)
                    foreach (var s in wk.Sessions)
                    {
                        if (s.Type != SessionType.Race) continue;
                        foreach (var e in s.Classification)
                        {
                            if (e.TeamId != team.Id) continue;
                            now.Starts++;
                            if (e.Status != ResultStatus.Finished) continue;
                            if (e.Position == 1) now.Wins++;
                            if (e.Position <= 3) now.Podiums++;
                        }
                    }
                mine.Add(now);
            }
            mine.Sort((a, b) => b.Season.CompareTo(a.Season));
            return mine;
        }
    }
}
