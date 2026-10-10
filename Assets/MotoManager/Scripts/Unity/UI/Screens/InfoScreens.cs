using System.Collections.Generic;
using System.Text;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>Home: next event, actions, the team at a glance and the news.</summary>
    public class HubScreen : ScreenBase
    {
        public override string Title { get { return "Home"; } }
        private static string _lastResult = "";

        public override void Build(RectTransform content)
        {
            var list = UiFactory.ScrollList("Hub", content);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var world = Game.World;
            var team = world.PlayerTeam;
            var next = Game.PeekNextEvent();

            if (next != null)
            {
                var circuit = Game.Db.GetCircuit(next.Round.CircuitId);
                UiFactory.Heading(list, next.Round.Name);
                var cats = new List<string>();
                foreach (var c in next.Categories) cats.Add(Game.Db.GetCategory(c).ShortName);
                UiFactory.Paragraph(list, circuit.Name + " (" + circuit.Country + ") · " + (circuit.LengthM / 1000).ToString("0.000") + " km · " +
                                          next.Round.Date.ToString("d MMMM yyyy") + "\n<color=#9AA4B2>" + string.Join(", ", cats.ToArray()) + "</color>", Theme.FontNormal);
                bool racing = team != null && next.Categories.Contains(team.CategoryId);
                UiFactory.ButtonRow(list, 90,
                    UiFactory.Act(racing ? "Go to the event" : "Next event (your team does not race)", Play),
                    UiFactory.Act("Simulate event", SimulateEvent),
                    UiFactory.Act("Simulate to season end", SimulateSeason));
            }
            else
            {
                UiFactory.Heading(list, "Season " + world.Season + " is over");
                UiFactory.ButtonRow(list, 90, UiFactory.Act("Close the season (market, retirements, new calendar)", CloseSeason));
            }
            if (!string.IsNullOrEmpty(_lastResult)) UiFactory.Paragraph(list, _lastResult, Theme.FontSmall + 2);

            if (team != null)
            {
                UiFactory.Heading(list, "Your team");
                ChampionshipState champ;
                world.Championships.TryGetValue(team.CategoryId, out champ);
                var widths = new[] { 4f, 2f, 2f, 2f, 2f };
                UiFactory.TableRow(list, 40, widths, new[] { "Rider", "Position", "Points", "Wins", "Morale" }, true);
                foreach (var id in team.RiderIds)
                {
                    var r = world.GetRider(id);
                    if (r == null) continue;
                    int pos = champ != null ? champ.RiderPosition(id) : 0;
                    StandingEntry st = null;
                    if (champ != null) champ.Riders.TryGetValue(id, out st);
                    UiFactory.TableRow(list, 46, widths, new[]
                    {
                        r.Number + "  " + r.FullName + (r.InjuryRoundsOut > 0 ? "  <color=#E10600>injured</color>" : ""),
                        pos > 0 ? "P" + pos : "-", st != null ? st.Points.ToString("0") : "0", st != null ? st.Wins.ToString() : "0", r.Morale.ToString("0")
                    });
                }
            }

            UiFactory.Heading(list, "News");
            int shown = 0;
            foreach (var n in world.News)
            {
                if (shown++ >= 25) break;
                string color = n.Important ? "#EBEEF2" : "#9AA4B2";
                UiFactory.Paragraph(list, "<color=#9AA4B2>" + n.Date.ToString("dd MMM") + "</color>  <color=" + color + ">" + n.Headline + "</color>");
            }
            UiFactory.ButtonRow(list, 70, UiFactory.Act("Save career", () => Session.Save()));
        }

        private void Play()
        {
            Shell.gameObject.SetActive(true);
            new RaceFlow(Session, Shell.gameObject, summary =>
            {
                _lastResult = summary;
                Shell.Refresh();
            }).Start();
        }

        private void SimulateEvent()
        {
            var game = Game;
            string summary = "";
            Session.Run("Simulating the event", () =>
            {
                var ev = game.BeginNextEvent();
                game.CompleteEvent(ev);
                summary = "<b>" + ev.Plan.Round.Name + "</b> simulated.";
            }, () =>
            {
                _lastResult = summary;
                Session.Save();
                Shell.Refresh();
            });
        }

        private void SimulateSeason()
        {
            var game = Game;
            Session.Run("Simulating the season", () => game.SimulateRestOfSeason(), () =>
            {
                _lastResult = "Season simulated to the end.";
                Session.Save();
                Shell.Refresh();
            });
        }

        private void CloseSeason()
        {
            var game = Game;
            string summary = "";
            Session.Run("Closing the season", () =>
            {
                var s = game.EndSeason();
                var sb = new StringBuilder("<b>Season " + s.Season + " closed</b>\n");
                foreach (var c in s.Champions)
                    sb.Append(game.Db.GetCategory(c.CategoryId).ShortName).Append(" champion: ").Append(c.RiderName).Append('\n');
                sb.Append(s.Retired.Count).Append(" retirements · ").Append(s.Market.Moves.Count).Append(" market moves · ").Append(s.YouthIntake).Append(" new young riders");
                summary = sb.ToString();
            }, () =>
            {
                _lastResult = summary;
                Session.Save();
                Shell.Refresh();
            });
        }
    }

    /// <summary>Standings of any category: riders, teams and constructors.</summary>
    public class StandingsScreen : ScreenBase
    {
        public override string Title { get { return "Standings"; } }
        private string _category;
        private int _table;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            if (_category == null) _category = world.PlayerTeam != null ? world.PlayerTeam.CategoryId : "motogp";

            var tabs = UiFactory.Rect("Tabs", content);
            UiFactory.Place(tabs, 0, 0.92f, 1, 1);
            var cats = new List<ChampionshipCategory>();
            foreach (var c in Game.Db.CategoriesOrdered()) if (c.Simulated) cats.Add(c);
            for (int i = 0; i < cats.Count; i++)
            {
                var cat = cats[i];
                var b = UiFactory.Button(cat.Id, tabs, cat.ShortName, () =>
                {
                    _category = cat.Id;
                    Shell.Refresh();
                }, cat.Id == _category ? Theme.Accent : Theme.PanelLight, Theme.FontSmall);
                UiFactory.Place((RectTransform)b.transform, (float)i / (cats.Count + 3), 0, (float)(i + 1) / (cats.Count + 3), 1, 3);
            }
            string[] tables = { "Riders", "Teams", "Makers" };
            for (int i = 0; i < 3; i++)
            {
                int t = i;
                var b = UiFactory.Button("Table" + i, tabs, tables[i], () =>
                {
                    _table = t;
                    Shell.Refresh();
                }, i == _table ? Theme.Accent : Theme.PanelLight, Theme.FontSmall);
                UiFactory.Place((RectTransform)b.transform, (float)(cats.Count + i) / (cats.Count + 3), 0, (float)(cats.Count + i + 1) / (cats.Count + 3), 1, 3);
            }

            var list = UiFactory.ScrollList("Table", content);
            UiFactory.Place((RectTransform)list.parent.parent, 0, 0, 1, 0.91f);
            ChampionshipState champ;
            if (!world.Championships.TryGetValue(_category, out champ)) return;
            var playerTeam = world.PlayerTeamId;
            if (_table == 0)
            {
                var widths = new[] { 1f, 5f, 4f, 2f, 1.5f, 1.5f, 1.5f };
                UiFactory.TableRow(list, 40, widths, new[] { "Pos", "Rider", "Team", "Points", "Wins", "Podiums", "Poles" }, true);
                var rows = champ.SortedRiders();
                for (int i = 0; i < rows.Count; i++)
                {
                    var r = world.GetRider(rows[i].Id);
                    var team = r != null ? world.GetTeam(r.TeamId) : null;
                    bool mine = team != null && team.Id == playerTeam;
                    UiFactory.TableRow(list, 42, widths, new[]
                    {
                        (i + 1).ToString(), r != null ? r.FullName + " <color=#9AA4B2>" + r.Nationality + "</color>" : rows[i].Id,
                        team != null ? team.ShortName ?? team.Name : "-", rows[i].Points.ToString("0"), rows[i].Wins.ToString(), rows[i].Podiums.ToString(), rows[i].Poles.ToString()
                    }, false, mine ? new Color(0.88f, 0.02f, 0, 0.18f) : (Color?)null);
                }
            }
            else
            {
                var rows = _table == 1 ? champ.SortedTeams() : champ.SortedManufacturers();
                var widths = new[] { 1f, 7f, 2f, 2f };
                UiFactory.TableRow(list, 40, widths, new[] { "Pos", _table == 1 ? "Team" : "Manufacturer", "Points", "Wins" }, true);
                for (int i = 0; i < rows.Count; i++)
                {
                    string name = rows[i].Id;
                    if (_table == 1) { var t = world.GetTeam(name); if (t != null) name = t.Name; }
                    else { var m = world.GetManufacturer(name); if (m != null) name = m.Name; }
                    bool mine = _table == 1 && rows[i].Id == playerTeam;
                    UiFactory.TableRow(list, 42, widths, new[] { (i + 1).ToString(), name, rows[i].Points.ToString("0"), rows[i].Wins.ToString() }, false,
                        mine ? new Color(0.88f, 0.02f, 0, 0.18f) : (Color?)null);
                }
            }
        }
    }

    /// <summary>Both calendars with dates, circuits and the winners of past rounds.</summary>
    public class CalendarScreen : ScreenBase
    {
        public override string Title { get { return "Calendar"; } }

        public override void Build(RectTransform content)
        {
            var list = UiFactory.ScrollList("Calendar", content);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var world = Game.World;
            var widths = new[] { 0.8f, 2f, 5f, 4f, 6f };
            foreach (var kv in world.Calendars)
            {
                var champ = Game.Db.Championships.ContainsKey(kv.Key) ? Game.Db.Championships[kv.Key] : null;
                UiFactory.Heading(list, champ != null ? champ.Name : kv.Key);
                UiFactory.TableRow(list, 40, widths, new[] { "#", "Date", "Event", "Circuit", "Winner (top class)" }, true);
                int next;
                world.NextRound.TryGetValue(kv.Key, out next);
                string topCat = champ != null && champ.Categories.Count > 0 ? champ.Categories[0] : null;
                for (int i = 0; i < kv.Value.Rounds.Count; i++)
                {
                    var r = kv.Value.Rounds[i];
                    var circuit = Game.Db.GetCircuit(r.CircuitId);
                    string winner = "";
                    ChampionshipState cs;
                    if (i < next && topCat != null && world.Championships.TryGetValue(topCat, out cs))
                        foreach (var w in cs.Rounds)
                            if (w.Round == r.Round)
                                foreach (var s in w.Sessions)
                                    if (s.Type == SessionType.Race && s.Classification.Count > 0)
                                    {
                                        var rider = world.GetRider(s.Classification[0].RiderId);
                                        winner = rider != null ? rider.FullName : "";
                                        break;
                                    }
                    Color? bg = i == next ? new Color(0.94f, 0.7f, 0.16f, 0.18f) : (Color?)null;
                    UiFactory.TableRow(list, 42, widths, new[] { r.Round.ToString(), r.Date.ToString("dd MMM"), r.Name, circuit != null ? circuit.ShortName : r.CircuitId, winner }, false, bg,
                        i < next ? Theme.TextDim : Theme.Text);
                }
            }
        }
    }

    /// <summary>Roll of honour since 2026, plus all-time leaders.</summary>
    public class HistoryScreen : ScreenBase
    {
        public override string Title { get { return "History"; } }

        public override void Build(RectTransform content)
        {
            var list = UiFactory.ScrollList("History", content);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var world = Game.World;
            if (world.Champions.Count == 0) UiFactory.Paragraph(list, "No season completed yet. Champions will be listed here.", Theme.FontNormal);
            foreach (var cat in Game.Db.CategoriesOrdered())
            {
                if (!cat.Simulated) continue;
                var champs = world.Champions.FindAll(c => c.CategoryId == cat.Id);
                if (champs.Count == 0) continue;
                UiFactory.Heading(list, cat.Name);
                var widths = new[] { 1.2f, 5f, 5f, 2f };
                UiFactory.TableRow(list, 40, widths, new[] { "Season", "Champion", "Team", "Points" }, true);
                for (int i = champs.Count - 1; i >= 0; i--)
                {
                    var c = champs[i];
                    UiFactory.TableRow(list, 42, widths, new[] { c.Season.ToString(), c.RiderName, c.TeamName ?? "", c.Points.ToString("0") });
                }
            }

            UiFactory.Heading(list, "Most wins since 2026");
            var all = new List<Rider>(world.Riders.Values);
            all.Sort((a, b) => b.TotalWins().CompareTo(a.TotalWins()));
            var w2 = new[] { 5f, 2f, 2f, 3f };
            UiFactory.TableRow(list, 40, w2, new[] { "Rider", "Wins", "Podiums", "Titles" }, true);
            for (int i = 0; i < all.Count && i < 15; i++)
            {
                var r = all[i];
                if (r.TotalWins() == 0) break;
                UiFactory.TableRow(list, 42, w2, new[] { r.FullName + (r.IsRetired ? " <color=#9AA4B2>(retired)</color>" : ""), r.TotalWins().ToString(), r.TotalPodiums().ToString(), r.Titles.Count.ToString() });
            }
        }
    }
}
