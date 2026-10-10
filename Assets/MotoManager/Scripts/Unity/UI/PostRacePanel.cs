using System;
using System.Collections.Generic;
using System.Text;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Post-race screen: full classification (time, gap, best lap, positions gained, tyres, overtakes, points,
    /// penalties, retirements) and, for the player's riders, "Why did we perform like this?" built from the
    /// race analysis plus best sectors and the circuit profile.
    /// </summary>
    public static class PostRacePanel
    {
        private static int _tab;

        public static RectTransform Build(RaceSimulation sim, RectTransform root, Action onReplay, Action onContinue)
        {
            var panel = UiFactory.Panel("PostRace", root, new Color(0.035f, 0.038f, 0.047f, 0.97f), true);
            var rt = panel.rectTransform;
            UiFactory.Place(rt, 0.02f, 0.03f, 0.98f, 0.97f);
            _tab = 0;
            Render(sim, rt, onReplay, onContinue);
            return rt;
        }

        /// <summary>Tabs: the classification, then one page per player rider with the "why" of the result.</summary>
        private static void Render(RaceSimulation sim, RectTransform rt, Action onReplay, Action onContinue)
        {
            UiFactory.Clear(rt);
            var title = UiFactory.Label("Title", rt, Loc.T("post.title", Txt(sim.Config.SessionName)), Theme.FontLarge - 4, TextAnchor.MiddleLeft);
            UiFactory.RaceTitle(title);
            UiFactory.Place(title.rectTransform, 0.02f, 0.9f, 0.45f, 0.99f);

            var players = new List<RiderRaceState>();
            foreach (var r in sim.Riders) if (r.Entry.IsPlayer) players.Add(r);
            var labels = new List<string> { Loc.T("post.tab.classification") };
            foreach (var r in players) labels.Add(r.Entry.Rider.ShortName);
            float tw = Math.Min(0.16f, 0.52f / labels.Count);
            for (int i = 0; i < labels.Count; i++)
            {
                int idx = i;
                var b = UiFactory.Button("Tab", rt, labels[i], () => { _tab = idx; Render(sim, rt, onReplay, onContinue); },
                    i == _tab ? Theme.Accent : Theme.PanelLight, Theme.FontSmall);
                UiFactory.Place((RectTransform)b.transform, 0.46f + i * tw, 0.905f, 0.46f + (i + 1) * tw, 0.98f, 3);
            }

            var area = UiFactory.Rect("Area", rt);
            UiFactory.Place(area, 0.01f, 0.1f, 0.99f, 0.895f);
            var points = PointsTable(sim);
            if (_tab == 0 || _tab > players.Count) BuildClassification(sim, area, points);
            else RiderPage(sim, area, players[_tab - 1], points);

            var replay = UiFactory.Button("Replay", rt, Loc.T("post.replay"), () => onReplay(), Theme.PanelLight);
            UiFactory.Place((RectTransform)replay.transform, 0.52f, 0.015f, 0.74f, 0.085f, 3);
            var cont = UiFactory.Button("Continue", rt, Loc.T("post.continue"), () => onContinue(), Theme.Accent);
            UiFactory.Place((RectTransform)cont.transform, 0.76f, 0.015f, 0.98f, 0.085f, 3);
        }

        private static string Txt(string session)
        {
            return Loc.Or("session." + session, session);
        }

        private static void RiderPage(RaceSimulation sim, RectTransform area, RiderRaceState r, List<int> points)
        {
            var rep = RaceAnalysis.Analyze(sim, r.Index);
            var list = UiFactory.ScrollList("Rider", area);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            RectTransform left, right;
            Ui.Columns(list, out left, out right);

            int pts = rep.Finish > 0 && rep.Finish - 1 < points.Count && r.Status == RaceStatus.Finished ? points[rep.Finish - 1] : 0;
            var card = Ui.Card(left, r.Entry.Rider.FullName, r.Entry.Team != null ? r.Entry.Team.Name : null);
            string gained = rep.PositionsGained > 0 ? Theme.GoodText("+" + rep.PositionsGained) : rep.PositionsGained < 0 ? Theme.BadText(rep.PositionsGained.ToString()) : "=";
            Ui.Kpis(card,
                Ui.Kpi("P" + rep.Grid, Loc.T("post.k.grid")),
                Ui.Kpi(r.Status == RaceStatus.Finished ? "P" + rep.Finish : Theme.BadText(Loc.T("result.DNF")), Loc.T("post.k.finish")),
                Ui.Kpi(gained, Loc.T("post.k.gained")),
                Ui.Kpi(pts.ToString(), Loc.T("post.k.points")));
            Ui.Kpis(card,
                Ui.Kpi(rep.BestLap > 0 ? MathX.FormatLapTime(rep.BestLap) : "-", Loc.T("post.k.bestLap")),
                Ui.Kpi(rep.TopSpeedKph.ToString("0"), Loc.T("post.k.topSpeed")),
                Ui.Kpi(rep.Overtakes + " / " + rep.TimesPassed, Loc.T("post.k.passes")),
                Ui.Kpi(rep.Errors.ToString(), Loc.T("post.k.errors")));
            if (rep.BestSectors != null)
            {
                var fastest = FastestSectors(sim);
                var w = new float[rep.BestSectors.Length + 1];
                var head = new string[w.Length];
                var mine = new string[w.Length];
                var best = new string[w.Length];
                w[0] = 2.2f;
                head[0] = "";
                mine[0] = Theme.Dim(Loc.T("post.sectorsMine"));
                best[0] = Theme.Dim(Loc.T("post.sectorsBest"));
                for (int i = 0; i < rep.BestSectors.Length; i++)
                {
                    w[i + 1] = 1.4f;
                    head[i + 1] = "S" + (i + 1);
                    bool top = i < fastest.Length && Math.Abs(rep.BestSectors[i] - fastest[i]) < 0.0005;
                    string t = rep.BestSectors[i] >= double.MaxValue ? "-" : rep.BestSectors[i].ToString("0.000", Loc.Culture);
                    mine[i + 1] = top ? "<color=#B266FF>" + t + "</color>" : t;
                    best[i + 1] = i < fastest.Length && fastest[i] < double.MaxValue ? fastest[i].ToString("0.000", Loc.Culture) : "-";
                }
                UiFactory.TableRow(card, 32, w, head, true);
                UiFactory.TableRow(card, 34, w, mine);
                UiFactory.TableRow(card, 34, w, best);
            }

            var why = Ui.Card(right, Loc.T("post.why"));
            Items(why, Loc.T("post.strengths"), rep.Strengths, Theme.GoodHex, "+");
            Items(why, Loc.T("post.weaknesses"), rep.Weaknesses, Theme.BadHex, "–");
        }

        private static void Items(Transform list, string heading, List<AnalysisItem> items, string color, string sign)
        {
            UiFactory.Paragraph(list, "<b>" + heading + "</b>", Theme.FontSmall);
            if (items.Count == 0) Ui.Note(list, Loc.T("post.nothing"));
            foreach (var it in items)
                UiFactory.Paragraph(list, "<color=" + color + "><b>" + sign + "</b></color>  " + Loc.T("analysis." + it.Code + (it.Strength ? "+" : "-"), Math.Abs(it.Value)), Theme.FontSmall - 2);
        }

        private static List<int> PointsTable(RaceSimulation sim)
        {
            foreach (var s in sim.Rules.Weekend.Sessions)
                if (s.Type == sim.Config.SessionType && s.Name == sim.Config.SessionName) return sim.Rules.GetPoints(s.PointsSystem);
            return sim.Rules.GetPoints(sim.Config.SessionType == SessionType.Sprint ? "sprint" : "race");
        }

        private static void BuildClassification(RaceSimulation sim, RectTransform rt, List<int> points)
        {
            var list = UiFactory.ScrollList("Classification", rt, 2);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var w = new[] { 0.7f, 3.6f, 2.6f, 2f, 1.8f, 0.9f, 0.9f, 2.6f };
            UiFactory.TableRow(list, 34, w, new[]
            {
                Loc.T("post.col.pos"), Loc.T("post.col.rider"), Loc.T("post.col.team"), Loc.T("post.col.time"), Loc.T("post.col.best"),
                Loc.T("post.col.gained"), Loc.T("post.col.pts"), Loc.T("post.col.notes")
            }, true);
            foreach (var e in sim.GetClassification())
            {
                var r = Find(sim, e.RiderId);
                if (r == null) continue;
                string time = e.Status == ResultStatus.Finished ? (e.Position == 1 ? MathX.FormatLapTime(e.TotalTime) : MathX.FormatGap(e.GapToWinner)) : Loc.Or("result." + e.Status, e.Status.ToString());
                int gained = e.GridPosition - e.Position;
                string gainedText = e.Status != ResultStatus.Finished ? "" : gained > 0 ? Theme.GoodText("+" + gained) : gained < 0 ? Theme.BadText(gained.ToString()) : "=";
                int pts = e.Status == ResultStatus.Finished && e.Position - 1 < points.Count ? points[e.Position - 1] : 0;
                var notes = new List<string>();
                if (e.Note != null) notes.Add(e.Note);
                if (r.Penalties.Count > 0 && e.Note == null) notes.Add(Loc.T("post.penalty", r.Penalties.Count));
                if (r.HasIssue) notes.Add(Loc.T("post.issue"));
                if (e.FastestLap) notes.Add("<color=#B266FF>" + Loc.T("post.fastestLap") + "</color>");
                var team = r.Entry.Team;
                string pos = e.Position <= 3 && e.Status == ResultStatus.Finished ? "<b>" + e.Position + "</b>" : e.Position.ToString();
                UiFactory.TableRow(list, 36, w, new[]
                {
                    pos, (r.Entry.IsPlayer ? "<b>" : "") + r.Entry.Rider.FullName + (r.Entry.IsPlayer ? "</b>" : ""), team != null ? team.ShortName ?? team.Name : "", time,
                    e.BestLap > 0 ? MathX.FormatLapTime(e.BestLap) : "-", gainedText, pts > 0 ? pts.ToString() : "", string.Join(", ", notes.ToArray())
                }, false, r.Entry.IsPlayer ? new Color(0.94f, 0.7f, 0.16f, 0.16f) : (Color?)null);
            }
        }

        private static double[] FastestSectors(RaceSimulation sim)
        {
            var best = new double[sim.Track.SectorCount];
            for (int i = 0; i < best.Length; i++) best[i] = double.MaxValue;
            foreach (var r in sim.Riders)
            {
                var s = r.Stats.BestSectors;
                if (s == null) continue;
                for (int i = 0; i < best.Length && i < s.Length; i++) best[i] = Math.Min(best[i], s[i]);
            }
            return best;
        }

        private static string Sectors(double[] times, double[] reference)
        {
            var parts = new List<string>();
            for (int i = 0; i < times.Length; i++)
            {
                if (times[i] >= double.MaxValue) { parts.Add("-"); continue; }
                string t = times[i].ToString("0.000", Loc.Culture);
                if (reference != null && i < reference.Length && Math.Abs(times[i] - reference[i]) < 0.0005) t = "<color=#B266FF>" + t + "</color>";
                parts.Add("S" + (i + 1) + " " + t);
            }
            return string.Join("  ", parts.ToArray());
        }

        private static RiderRaceState Find(RaceSimulation sim, string riderId)
        {
            foreach (var r in sim.Riders)
                if (r.Entry.Rider.Id == riderId) return r;
            return null;
        }
    }
}
