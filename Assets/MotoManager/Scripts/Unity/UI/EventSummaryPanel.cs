using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Race;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

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
        private RectTransform _tabs;
        private int _tab;
        private readonly List<SessionResult> _sessions = new List<SessionResult>();

        public static void Show(EventDigest digest, Action done)
        {
            var canvas = UiFactory.CreateCanvas("EventSummary", 30);
            var p = canvas.gameObject.AddComponent<EventSummaryPanel>();
            p._d = digest;
            p._done = done;
            var root = UiFactory.SafeRoot(canvas, Theme.Background);
            RaceUi.HostSheets(canvas.gameObject, root);
            p.Build(root);
        }

        private void Build(RectTransform root)
        {
            RaceUi.Backdrop(root);
            RectTransform none;
            RaceUi.TopBar(root, Loc.T("summary.title", _d.EventName), _d.CategoryName + " · " + _d.CircuitName, 0, out none);
            foreach (var ss in _d.Sessions)
                if (ss.Type != SessionType.Practice && ss.Type != SessionType.WarmUp) _sessions.Add(ss);

            _tabs = RaceUi.Content(root, RaceUi.BarH + 8, 0);
            _tabs.anchorMin = new Vector2(0, 1);
            _tabs.offsetMin = new Vector2(RaceUi.Pad, -RaceUi.BarH - 8 - Ui.Grid.Hit);

            _area = RaceUi.Content(root, RaceUi.BarH + 8 + Ui.Grid.Hit + 8, RaceUi.BarH + 16);
            var bottom = RaceUi.BottomBar(root);
            Ui.PrimaryCTA(RaceUi.RightSlot(bottom, 380, 0, 0), Loc.T("post.continue"), Close);
            Render();
        }

        private void Close()
        {
            Destroy(gameObject);
            if (_done != null) _done();
        }

        /// <summary>Tab labels: the team, each session (not practice), the gaps chart.</summary>
        private List<string> TabNames()
        {
            var names = new List<string> { Loc.T("summary.tab.team") };
            foreach (var ss in _sessions) names.Add(Loc.Or("session." + ss.SessionName, ss.SessionName));
            if (_d.Gaps != null) names.Add(Loc.T("post.tab.gaps"));
            return names;
        }

        private void Render()
        {
            Ui.CloseSheet();
            // Tabs are rebuilt too, so the selected one shows. More than five: the team segment plus a picker.
            UiFactory.Clear(_tabs);
            var names = TabNames();
            if (names.Count <= 5)
            {
                var holder = UiFactory.Rect("Tabs", _tabs);
                UiFactory.Stretch(holder);
                var v = holder.gameObject.AddComponent<VerticalLayoutGroup>();
                v.childControlHeight = true;
                v.childControlWidth = true;
                v.childForceExpandWidth = true;
                Ui.Segmented(holder, names, _tab, i => { _tab = i; Render(); });
            }
            else
            {
                var segArea = UiFactory.Rect("Team", _tabs);
                UiFactory.Place(segArea, 0, 0, 0.3f, 1);
                var v = segArea.gameObject.AddComponent<VerticalLayoutGroup>();
                v.childControlHeight = true;
                v.childControlWidth = true;
                v.childForceExpandWidth = true;
                Ui.Segmented(segArea, new[] { names[0] }, _tab == 0 ? 0 : -1, i => { _tab = 0; Render(); });
                var pickArea = UiFactory.Rect("Sessions", _tabs);
                UiFactory.Place(pickArea, 0.31f, 0, 0.62f, 1);
                var rest = names.GetRange(1, names.Count - 1);
                Ui.Picker(pickArea, Loc.T("ux.race.session"), rest, _tab - 1, i => { _tab = i + 1; Render(); });
            }

            UiFactory.Clear(_area);
            var list = UiFactory.ScrollList("Summary", _area);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            if (_tab == 0) TeamPage(list);
            else if (_d.Gaps != null && _tab == _sessions.Count + 1) PostRacePanel.GapCard(list, _d.Gaps, _d.GapLaps, 330);
            else if (_tab - 1 < _sessions.Count) SessionPage(list, _sessions[_tab - 1]);
        }

        private static string Analysis(AnalysisItem it)
        {
            return Loc.T("analysis." + it.Code + (it.Strength ? "+" : "-"), Math.Abs(it.Value));
        }

        private void TeamPage(RectTransform list)
        {
            if (_d.Riders.Count == 0)
            {
                Ui.Empty(Ui.Card(list, _d.TeamName), UiIcons.Team, Loc.T("summary.noRiders"));
                return;
            }
            RectTransform left, right;
            Ui.Columns(list, out left, out right);
            foreach (var r in _d.Riders)
            {
                var rd = r;
                var rep = r.Report;
                Action info = null;
                if (rep != null && rep.Strengths.Count + rep.Weaknesses.Count > 0)
                    info = () => Ui.Sheet(rd.Name, l =>
                    {
                        UiFactory.Paragraph(l, "<b>" + Theme.Caps(Loc.T("post.strengths")) + "</b>", Theme.TextCaption);
                        foreach (var it in rep.Strengths) UiFactory.Paragraph(l, Theme.GoodText("<b>+</b>") + "  " + Analysis(it), Theme.TextBody);
                        UiFactory.Paragraph(l, "<b>" + Theme.Caps(Loc.T("post.weaknesses")) + "</b>", Theme.TextCaption);
                        foreach (var it in rep.Weaknesses) UiFactory.Paragraph(l, Theme.BadText("<b>-</b>") + "  " + Analysis(it), Theme.TextBody);
                    });
                var card = Ui.Card(left, r.Name, null, info);
                // Qualifying, the races merged into one figure ("P5 / P3"), points.
                var kpis = new List<string[]> { Ui.Kpi(r.Qualifying > 0 ? "P" + r.Qualifying : "-", Loc.T("summary.k.quali")) };
                if (r.Results.Count > 0)
                {
                    var values = new List<string>();
                    var captions = new List<string>();
                    foreach (var res in r.Results)
                    {
                        values.Add(res.Value);
                        captions.Add(res.Key);
                    }
                    kpis.Add(Ui.Kpi(string.Join(" / ", values.ToArray()), string.Join(" / ", captions.ToArray())));
                }
                kpis.Add(Ui.Kpi(r.Points.ToString("0"), Loc.T("post.k.points")));
                Ui.Kpis(card, kpis.ToArray());
                Ui.Line(card, Loc.T("summary.championship"), Move(r.ChampBefore, r.ChampAfter));
                if (rep != null)
                {
                    Ui.Line(card, Loc.T("post.k.bestLap"), rep.BestLap > 0 ? MathX.FormatLapTime(rep.BestLap) : "-");
                    Ui.Line(card, Loc.T("post.k.passes"), rep.Overtakes + " / " + rep.TimesPassed);
                    Ui.Line(card, Loc.T("post.k.gained"), Theme.Signed(rep.PositionsGained));
                    // One strength and one weakness in view; the rest behind the card's info button.
                    if (rep.Strengths.Count > 0) UiFactory.Paragraph(card, Theme.GoodText("<b>+</b>") + "  " + Analysis(rep.Strengths[0]), Theme.TextBody);
                    if (rep.Weaknesses.Count > 0) UiFactory.Paragraph(card, Theme.BadText("<b>-</b>") + "  " + Analysis(rep.Weaknesses[0]), Theme.TextBody);
                }
            }
            var team = Ui.Card(right, _d.TeamName, null, () => Ui.Sheet(_d.TeamName, l =>
            {
                UiFactory.Paragraph(l, Loc.T("summary.moneyNote"), Theme.TextBody);
                UiFactory.Paragraph(l, Loc.T("summary.watchNext"), Theme.TextBody);
            }));
            double pts = 0;
            foreach (var r in _d.Riders) pts += r.Points;
            Ui.Kpis(team,
                Ui.Kpi(pts.ToString("0"), Loc.T("post.k.points")),
                Ui.Kpi(_d.TeamAfter > 0 ? "P" + _d.TeamAfter : "-", Loc.T("summary.teamChamp")),
                Ui.Kpi(Theme.Money(_d.CashAfter - _d.CashBefore), Loc.T("summary.money")));
            Ui.Line(team, Loc.T("summary.teamChamp"), Move(_d.TeamBefore, _d.TeamAfter));
        }

        /// <summary>"P6  +2" (green when up, red when down), "P6" when unchanged, "-" without a position.</summary>
        private static string Move(int before, int after)
        {
            if (after <= 0) return "-";
            string now = "<b>P" + after + "</b>";
            if (before <= 0 || before == after) return now;
            return Theme.Dim("P" + before + "  >  ") + now + "  " + Theme.Signed(before - after);
        }

        /// <summary>A session as list rows: races P | rider | grid | gap | pts; others P | rider | best | gap.</summary>
        private void SessionPage(RectTransform list, SessionResult s)
        {
            var card = Ui.Card(list, Loc.Or("session." + s.SessionName, s.SessionName), s.WeatherSummary);
            bool race = s.Type == SessionType.Race || s.Type == SessionType.Sprint;
            var w = race ? new[] { 1f, 5f, 1.4f, 2.4f, 1.2f } : new[] { 1f, 5f, 2.4f, 2.4f };
            Ui.ListCaptions(card, w, race
                ? new[] { Loc.T("summary.col.pos"), Loc.T("col.rider"), Loc.T("summary.col.grid"), Loc.T("summary.col.gap"), Loc.T("summary.col.pts") }
                : new[] { Loc.T("summary.col.pos"), Loc.T("col.rider"), Loc.T("post.k.bestLap"), Loc.T("summary.col.gap") });
            var world = GameSession.Instance != null && GameSession.Instance.Game != null ? GameSession.Instance.Game.World : null;
            double pole = 0;
            foreach (var e in s.Classification) if (e.BestLap > 0 && (pole == 0 || e.BestLap < pole)) pole = e.BestLap;
            int index = 0;
            foreach (var e in s.Classification)
            {
                bool mine = _d.Find(e.RiderId) != null;
                var rider = world != null ? world.GetRider(e.RiderId) : null;
                var team = world != null ? world.GetTeam(e.TeamId) : null;
                string name = Theme.Caps(rider != null ? rider.FullName : e.RiderId);
                string gap;
                if (race) gap = e.Status != ResultStatus.Finished ? Theme.BadText(Theme.Caps(Loc.T("result." + e.Status))) : e.Position == 1 ? MathX.FormatLapTime(e.TotalTime) : MathX.FormatGap(e.GapToWinner);
                else gap = e.BestLap > 0 && pole > 0 && e.BestLap > pole ? "+" + (e.BestLap - pole).ToString("0.000", Loc.Culture) : "";
                var cells = race
                    ? new[] { e.Status == ResultStatus.Finished ? e.Position.ToString() : "-", name, e.GridPosition > 0 ? e.GridPosition.ToString() : "", gap, e.Points > 0 ? "<b>" + e.Points.ToString("0") + "</b>" : "" }
                    : new[] { e.Position.ToString(), name, e.BestLap > 0 ? MathX.FormatLapTime(e.BestLap) : "-", Theme.Dim(gap) };
                var row = Ui.ListRow(card, w, cells, index++, mine, null, 56);
                RaceUi.TeamBar(row, w, RaceUi.TeamColor(team != null ? team.Colors.Primary : null));
            }
        }
    }
}
