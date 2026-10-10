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
        public static RectTransform Build(RaceSimulation sim, RectTransform root, Action onReplay, Action onContinue)
        {
            var panel = UiFactory.Panel("PostRace", root, new Color(0.043f, 0.075f, 0.125f, 0.97f));
            var rt = panel.rectTransform;
            UiFactory.Place(rt, 0.02f, 0.03f, 0.98f, 0.97f);

            var title = UiFactory.Label("Title", rt, Loc.T("post.title", sim.Config.SessionName), Theme.FontLarge, TextAnchor.MiddleLeft);
            UiFactory.Place(title.rectTransform, 0.02f, 0.92f, 0.7f, 1f);
            var profile = TrackProfile.Compute(sim.Track);
            var traits = new List<string>();
            foreach (var t in profile.Traits) traits.Add(Loc.T("trait." + t));
            if (traits.Count == 0) traits.Add(Loc.T("trait.Balanced"));
            var prof = UiFactory.Label("Profile", rt, Loc.T("post.profile", string.Join(", ", traits.ToArray())), Theme.FontSmall, TextAnchor.MiddleRight, Theme.TextDim);
            UiFactory.Place(prof.rectTransform, 0.5f, 0.92f, 0.98f, 1f);

            var points = PointsTable(sim);
            BuildClassification(sim, rt, points);
            BuildAnalysis(sim, rt);

            var replay = UiFactory.Button("Replay", rt, Loc.T("post.replay"), () => onReplay(), Theme.PanelLight);
            UiFactory.Place((RectTransform)replay.transform, 0.52f, 0.015f, 0.74f, 0.085f);
            var cont = UiFactory.Button("Continue", rt, Loc.T("post.continue"), () => onContinue(), Theme.Accent);
            UiFactory.Place((RectTransform)cont.transform, 0.76f, 0.015f, 0.98f, 0.085f);
            return rt;
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
            UiFactory.Place((RectTransform)list.parent.parent, 0.01f, 0.1f, 0.6f, 0.91f);
            var w = new[] { 0.8f, 3.6f, 2.2f, 2f, 1.8f, 0.9f, 1.4f, 0.9f, 0.9f, 2.4f };
            UiFactory.TableRow(list, 36, w, new[]
            {
                Loc.T("post.col.pos"), Loc.T("post.col.rider"), Loc.T("post.col.team"), Loc.T("post.col.time"), Loc.T("post.col.best"),
                Loc.T("post.col.gained"), Loc.T("post.col.tyres"), Loc.T("post.col.ovt"), Loc.T("post.col.pts"), Loc.T("post.col.notes")
            }, true);
            foreach (var e in sim.GetClassification())
            {
                var r = Find(sim, e.RiderId);
                if (r == null) continue;
                string time = e.Status == ResultStatus.Finished ? (e.Position == 1 ? MathX.FormatLapTime(e.TotalTime) : MathX.FormatGap(e.GapToWinner)) : e.Status.ToString();
                int gained = e.GridPosition - e.Position;
                string gainedText = e.Status != ResultStatus.Finished ? "" : gained > 0 ? "<color=#3CC878>+" + gained + "</color>" : gained < 0 ? "<color=#E10600>" + gained + "</color>" : "=";
                int pts = e.Status == ResultStatus.Finished && e.Position - 1 < points.Count ? points[e.Position - 1] : 0;
                var notes = new List<string>();
                if (e.Note != null) notes.Add(e.Note);
                if (r.Penalties.Count > 0 && e.Note == null) notes.Add(Loc.T("post.penalty", r.Penalties.Count));
                if (r.HasIssue) notes.Add(Loc.T("post.issue"));
                if (e.FastestLap) notes.Add("<color=#B266FF>FL</color>");
                string tyres = RaceHud.CompoundLetter(r.Front.Compound) + "/" + RaceHud.CompoundLetter(r.Rear.Compound) + " " + (r.Rear.Wear * 100).ToString("0") + "%";
                var team = r.Entry.Team;
                UiFactory.TableRow(list, 38, w, new[]
                {
                    e.Position.ToString(), r.Entry.Rider.FullName, team != null ? team.ShortName ?? team.Name : "", time,
                    e.BestLap > 0 ? MathX.FormatLapTime(e.BestLap) : "-", gainedText, tyres, r.Stats.OvertakesMade.ToString(), pts > 0 ? pts.ToString() : "", string.Join(", ", notes.ToArray())
                }, false, r.Entry.IsPlayer ? new Color(0.94f, 0.7f, 0.16f, 0.16f) : (Color?)null);
            }
        }

        private static void BuildAnalysis(RaceSimulation sim, RectTransform rt)
        {
            var list = UiFactory.ScrollList("Analysis", rt, 4);
            UiFactory.Place((RectTransform)list.parent.parent, 0.61f, 0.1f, 0.99f, 0.91f);
            UiFactory.Heading(list, Loc.T("post.why"));
            var fastest = FastestSectors(sim);
            bool any = false;
            foreach (var r in sim.Riders)
            {
                if (!r.Entry.IsPlayer) continue;
                any = true;
                var rep = RaceAnalysis.Analyze(sim, r.Index);
                UiFactory.Paragraph(list, "<b>" + r.Entry.Rider.FullName + "</b>", Theme.FontNormal);
                UiFactory.Paragraph(list, "<color=#9AA4B2>" + Loc.T("post.summary", rep.Grid, rep.Finish, rep.PositionsGained,
                    MathX.FormatLapTime(rep.BestLap), rep.TopSpeedKph, rep.Overtakes, rep.TimesPassed, rep.Errors) + "</color>", Theme.FontSmall);
                if (rep.BestSectors != null)
                    UiFactory.Paragraph(list, "<color=#9AA4B2>" + Loc.T("post.sectors", Sectors(rep.BestSectors, fastest), Sectors(fastest, null)) + "</color>", Theme.FontSmall - 2);
                Items(list, Loc.T("post.strengths"), rep.Strengths, "#3CC878", "+ ");
                Items(list, Loc.T("post.weaknesses"), rep.Weaknesses, "#E10600", "- ");
            }
            if (!any)
            {
                // Spectating: explain the winner.
                foreach (var r in sim.Riders)
                {
                    if (r.Position != 1) continue;
                    var rep = RaceAnalysis.Analyze(sim, r.Index);
                    UiFactory.Paragraph(list, "<b>" + r.Entry.Rider.FullName + "</b>", Theme.FontNormal);
                    Items(list, Loc.T("post.strengths"), rep.Strengths, "#3CC878", "+ ");
                    Items(list, Loc.T("post.weaknesses"), rep.Weaknesses, "#E10600", "- ");
                }
            }
        }

        private static void Items(RectTransform list, string heading, List<AnalysisItem> items, string color, string prefix)
        {
            var sb = new StringBuilder("<color=#9AA4B2>" + heading + "</color>\n");
            if (items.Count == 0) sb.Append(Loc.T("post.nothing"));
            foreach (var it in items)
                sb.Append("<color=").Append(color).Append(">").Append(prefix).Append("</color>")
                  .Append(Loc.T("analysis." + it.Code + (it.Strength ? "+" : "-"), Math.Abs(it.Value))).Append('\n');
            UiFactory.Paragraph(list, sb.ToString().TrimEnd('\n'), Theme.FontSmall);
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
