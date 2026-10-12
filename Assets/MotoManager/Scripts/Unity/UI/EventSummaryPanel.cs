using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Race;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI
{
    /// <summary>One of the player's riders over a quick-simulated weekend.</summary>
    public class RiderDigest
    {
        public string RiderId;
        public string Name;
        public int Qualifying;
        /// <summary>Session name → result ("P5", "DNF") and points.</summary>
        public readonly List<KeyValuePair<string, string>> Results = new List<KeyValuePair<string, string>>();
        public double Points;
        public int ChampBefore, ChampAfter;
        /// <summary>Analysis of the main race (null if none was run).</summary>
        public RiderReport Report;
        public bool Finished;
    }

    /// <summary>The player's category weekend, recorded while it is simulated without watching.</summary>
    public class EventDigest
    {
        public string EventName;
        public string CircuitName;
        public string CategoryName;
        public string TeamName;
        public readonly List<SessionResult> Sessions = new List<SessionResult>();
        public readonly List<RiderDigest> Riders = new List<RiderDigest>();
        public int TeamBefore, TeamAfter;
        /// <summary>Gap to the leader lap by lap in the main race (null if none).</summary>
        public List<ChartSeries> Gaps;
        public int GapLaps;
        public double CashBefore, CashAfter;

        /// <summary>
        /// Runs the player's weekend session by session (same steps as a headless weekend), keeping each race
        /// simulation long enough to analyse the player's riders. Call <see cref="Finish"/> after the event completes.
        /// </summary>
        public static EventDigest Run(GameController game, WeekendSimulation w)
        {
            var world = game.World;
            var team = world.PlayerTeam;
            var d = new EventDigest
            {
                EventName = Loc.Round(w.Round.Name), CircuitName = w.Circuit != null ? w.Circuit.Name : "",
                CategoryName = game.Db.GetCategory(w.CategoryId).Name, TeamName = team != null ? team.Name : ""
            };
            ChampionshipState champ;
            world.Championships.TryGetValue(w.CategoryId, out champ);
            if (team != null)
            {
                d.CashBefore = Accounts.Cash(world, team);
                d.TeamBefore = Position(champ != null ? champ.SortedTeams() : null, team.Id);
                foreach (var id in team.RiderIds)
                {
                    var r = world.GetRider(id);
                    if (r != null) d.Riders.Add(new RiderDigest { RiderId = id, Name = r.FullName, ChampBefore = Position(champ != null ? champ.SortedRiders() : null, id) });
                }
            }
            while (!w.IsComplete)
            {
                var s = w.CurrentSession;
                SessionResult res;
                if (s.Type == SessionType.Race || s.Type == SessionType.Sprint)
                {
                    var sim = w.CreateRaceSimulation(game.Db.Tuning.BackgroundTimeStep);
                    sim.RunToEnd();
                    res = w.CompleteRace(sim);
                    if (s.Type == SessionType.Race) d.Gaps = PostRacePanel.GapSeries(sim, out d.GapLaps);
                    if (s.Type == SessionType.Race)
                        foreach (var rs in sim.Riders)
                        {
                            var rd = d.Find(rs.Entry.Rider != null ? rs.Entry.Rider.Id : null);
                            if (rd != null) rd.Report = RaceAnalysis.Analyze(sim, rs.Index, 3);
                        }
                }
                else res = w.RunCurrentHeadless();
                if (res == null) break;
                d.Sessions.Add(res);
                foreach (var e in res.Classification)
                {
                    var rd = d.Find(e.RiderId);
                    if (rd == null) continue;
                    if (res.Type == SessionType.Qualifying) rd.Qualifying = e.Position;
                    if (res.Type == SessionType.Race || res.Type == SessionType.Sprint)
                    {
                        rd.Results.Add(new KeyValuePair<string, string>(Loc.Or("session." + res.SessionName, res.SessionName), Place(e)));
                        rd.Points += e.Points;
                        if (res.Type == SessionType.Race) rd.Finished = e.Status == ResultStatus.Finished;
                    }
                }
            }
            return d;
        }

        /// <summary>Standings and money after the event (championship points are applied when the event completes).</summary>
        public void Finish(GameController game, string categoryId)
        {
            var world = game.World;
            var team = world.PlayerTeam;
            ChampionshipState champ;
            world.Championships.TryGetValue(categoryId, out champ);
            if (team == null || champ == null) return;
            CashAfter = Accounts.Cash(world, team);
            TeamAfter = Position(champ.SortedTeams(), team.Id);
            foreach (var r in Riders) r.ChampAfter = Position(champ.SortedRiders(), r.RiderId);
        }

        public RiderDigest Find(string riderId)
        {
            foreach (var r in Riders) if (r.RiderId == riderId) return r;
            return null;
        }

        public static string Place(ResultEntry e)
        {
            return e.Status == ResultStatus.Finished ? "P" + e.Position : Loc.T("result." + e.Status);
        }

        private static int Position(List<StandingEntry> table, string id)
        {
            if (table == null) return 0;
            for (int i = 0; i < table.Count; i++) if (table[i].Id == id) return i + 1;
            return 0;
        }
    }

    /// <summary>
    /// After a quick simulation: how the weekend went for the team (qualifying, races, points, championship moves,
    /// money and why the main race went as it did) and the classifications of each session.
    /// </summary>
    public class EventSummaryPanel : MonoBehaviour
    {
        private EventDigest _d;
        private Action _done;
        private RectTransform _area;
        private RectTransform _tabList;
        private int _tab;

        public static void Show(EventDigest digest, Action done)
        {
            var canvas = UiFactory.CreateCanvas("EventSummary", 30);
            var p = canvas.gameObject.AddComponent<EventSummaryPanel>();
            p._d = digest;
            p._done = done;
            p.Build(UiFactory.SafeRoot(canvas, Theme.Background));
        }

        private void Build(RectTransform root)
        {
            var bg = UiFactory.Panel("Background", root, Theme.Background);
            UiFactory.Stretch(bg.rectTransform);
            var title = UiFactory.Label("Title", root, "<b>" + Loc.T("summary.title", _d.EventName) + "</b>", Theme.FontLarge, TextAnchor.MiddleLeft, Theme.Text);
            title.supportRichText = true;
            UiFactory.Place(title.rectTransform, 0.02f, 0.9f, 0.7f, 0.99f);
            var sub = UiFactory.Label("Sub", root, _d.CategoryName + " · " + _d.CircuitName, Theme.FontSmall, TextAnchor.MiddleRight, Theme.TextDim);
            UiFactory.Place(sub.rectTransform, 0.6f, 0.9f, 0.98f, 0.99f);

            var tabs = UiFactory.Rect("Tabs", root);
            UiFactory.Place(tabs, 0.02f, 0.8f, 0.98f, 0.89f);
            _tabList = UiFactory.ScrollList("TabRow", tabs, 0);
            UiFactory.Stretch((RectTransform)_tabList.parent.parent);

            _area = UiFactory.Rect("Area", root);
            UiFactory.Place(_area, 0.02f, 0.1f, 0.98f, 0.79f);
            var cont = UiFactory.Button("Continue", root, Loc.T("post.continue"), Close, Theme.Accent);
            UiFactory.Place((RectTransform)cont.transform, 0.76f, 0.015f, 0.98f, 0.085f, 3);
            Render();
        }

        private void Close()
        {
            Destroy(gameObject);
            if (_done != null) _done();
        }

        private void Render()
        {
            // Tabs are rebuilt too, so the selected one shows.
            UiFactory.Clear(_tabList);
            var names = new List<string> { Loc.T("summary.tab.team") };
            foreach (var ss in _d.Sessions)
                if (ss.Type != SessionType.Practice && ss.Type != SessionType.WarmUp) names.Add(Loc.Or("session." + ss.SessionName, ss.SessionName));
            if (_d.Gaps != null) names.Add(Loc.T("post.tab.gaps"));
            Ui.Chips(_tabList, names, _tab, i => { _tab = i; Render(); }, 56);
            UiFactory.Clear(_area);
            var list = UiFactory.ScrollList("Summary", _area);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            int sessionTabs = 0;
            foreach (var ss in _d.Sessions) if (ss.Type != SessionType.Practice && ss.Type != SessionType.WarmUp) sessionTabs++;
            if (_tab == 0) TeamPage(list);
            else if (_d.Gaps != null && _tab == sessionTabs + 1)
            {
                var card = Ui.Card(list, Loc.T("post.gaps"), Loc.T("post.gapsSub"));
                Charts.LineChart(card, 330, _d.Gaps, true, v => "+" + v.ToString("0.0", Loc.Culture), Loc.T("post.gapStart"), Loc.T("post.gapLap", _d.GapLaps));
            }
            else
            {
                int k = 0;
                foreach (var s in _d.Sessions)
                {
                    if (s.Type == SessionType.Practice || s.Type == SessionType.WarmUp) continue;
                    if (++k == _tab) SessionPage(list, s);
                }
            }
        }

        private void TeamPage(RectTransform list)
        {
            if (_d.Riders.Count == 0)
            {
                Ui.Note(Ui.Card(list, _d.TeamName), Loc.T("summary.noRiders"));
                return;
            }
            RectTransform left, right;
            Ui.Columns(list, out left, out right);
            foreach (var r in _d.Riders)
            {
                var card = Ui.Card(left, r.Name);
                var kpis = new List<string[]> { Ui.Kpi(r.Qualifying > 0 ? "P" + r.Qualifying : "-", Loc.T("summary.k.quali")) };
                foreach (var res in r.Results) kpis.Add(Ui.Kpi(res.Value, res.Key));
                kpis.Add(Ui.Kpi(r.Points.ToString("0"), Loc.T("post.k.points")));
                Ui.Kpis(card, kpis.ToArray());
                Ui.Line(card, Loc.T("summary.championship"), Move(r.ChampBefore, r.ChampAfter));
                if (r.Report != null)
                {
                    var rep = r.Report;
                    string gained = rep.PositionsGained > 0 ? Theme.GoodText("+" + rep.PositionsGained) : rep.PositionsGained < 0 ? Theme.BadText(rep.PositionsGained.ToString()) : "=";
                    Ui.Line(card, Loc.T("post.k.gained"), gained);
                    Ui.Line(card, Loc.T("post.k.bestLap"), rep.BestLap > 0 ? MathX.FormatLapTime(rep.BestLap) : "-");
                    Ui.Line(card, Loc.T("post.k.passes"), rep.Overtakes + " / " + rep.TimesPassed);
                    foreach (var it in rep.Strengths)
                        UiFactory.Paragraph(card, Theme.GoodText("<b>+</b>") + "  " + Loc.T("analysis." + it.Code + "+", Math.Abs(it.Value)), Theme.FontSmall - 2);
                    foreach (var it in rep.Weaknesses)
                        UiFactory.Paragraph(card, Theme.BadText("<b>–</b>") + "  " + Loc.T("analysis." + it.Code + "-", Math.Abs(it.Value)), Theme.FontSmall - 2);
                }
            }
            var team = Ui.Card(right, _d.TeamName);
            double pts = 0;
            foreach (var r in _d.Riders) pts += r.Points;
            Ui.Kpis(team,
                Ui.Kpi(pts.ToString("0"), Loc.T("post.k.points")),
                Ui.Kpi(_d.TeamAfter > 0 ? "P" + _d.TeamAfter : "-", Loc.T("summary.teamChamp")),
                Ui.Kpi(Theme.Money(_d.CashAfter - _d.CashBefore), Loc.T("summary.money")));
            Ui.Line(team, Loc.T("summary.teamChamp"), Move(_d.TeamBefore, _d.TeamAfter));
            Ui.Note(team, Loc.T("summary.moneyNote"));
            Ui.Note(team, Loc.T("summary.watchNext"));
        }

        private static string Move(int before, int after)
        {
            if (after <= 0) return "-";
            string now = "<b>P" + after + "</b>";
            if (before <= 0 || before == after) return now;
            return now + "  " + (after < before ? Theme.GoodText("▲" + (before - after)) : Theme.BadText("▼" + (after - before)));
        }

        private void SessionPage(RectTransform list, SessionResult s)
        {
            var card = Ui.Card(list, Loc.Or("session." + s.SessionName, s.SessionName), s.WeatherSummary);
            bool race = s.Type == SessionType.Race || s.Type == SessionType.Sprint;
            var w = race ? new[] { 0.8f, 3.4f, 3f, 1f, 2f, 1f } : new[] { 0.8f, 3.4f, 3f, 2f, 2f, 0.1f };
            UiFactory.TableRow(card, 34, w, race
                ? new[] { Loc.T("summary.col.pos"), Loc.T("col.rider"), Loc.T("col.team"), Loc.T("summary.col.grid"), Loc.T("summary.col.gap"), Loc.T("summary.col.pts") }
                : new[] { Loc.T("summary.col.pos"), Loc.T("col.rider"), Loc.T("col.team"), Loc.T("post.k.bestLap"), Loc.T("summary.col.gap"), "" }, true);
            var world = GameSession.Instance != null && GameSession.Instance.Game != null ? GameSession.Instance.Game.World : null;
            double pole = 0;
            foreach (var e in s.Classification) if (e.BestLap > 0 && (pole == 0 || e.BestLap < pole)) pole = e.BestLap;
            foreach (var e in s.Classification)
            {
                bool mine = _d.Find(e.RiderId) != null;
                var rider = world != null ? world.GetRider(e.RiderId) : null;
                var team = world != null ? world.GetTeam(e.TeamId) : null;
                string name = rider != null ? rider.FullName : e.RiderId;
                string gap;
                if (race) gap = e.Status != ResultStatus.Finished ? Theme.BadText(Loc.T("result." + e.Status)) : e.Position == 1 ? MathX.FormatLapTime(e.TotalTime) : "+" + MathX.FormatGap(e.GapToWinner);
                else gap = e.BestLap > 0 && pole > 0 && e.BestLap > pole ? "+" + (e.BestLap - pole).ToString("0.000", Loc.Culture) : "";
                var cells = race
                    ? new[] { e.Status == ResultStatus.Finished ? e.Position.ToString() : "-", name, team != null ? team.Name : "", e.GridPosition > 0 ? e.GridPosition.ToString() : "", gap, e.Points > 0 ? e.Points.ToString("0") : "" }
                    : new[] { e.Position.ToString(), name, team != null ? team.Name : "", e.BestLap > 0 ? MathX.FormatLapTime(e.BestLap) : "-", gap, "" };
                UiFactory.TableRow(card, 34, w, cells, false, mine ? Theme.Alpha(Theme.Timing, 0.16f) : (Color?)null);
            }
        }
    }
}
