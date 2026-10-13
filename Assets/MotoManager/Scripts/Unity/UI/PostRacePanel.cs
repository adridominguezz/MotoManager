using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Post-race screen: the podium, the full classification (time, gap, best lap, positions gained, points,
    /// penalties, retirements), the gaps chart and, for the player's riders, "Why did we perform like this?" built
    /// from the race analysis plus best sectors.
    /// </summary>
    public static class PostRacePanel
    {
        private static int _tab;
        private static int _riderPick;
        private const int MaxRiderTabs = 2;

        public static RectTransform Build(RaceSimulation sim, RectTransform root, Action onReplay, Action onContinue)
        {
            var panel = UiFactory.Panel("PostRace", root, Theme.Alpha(Theme.Background, 0.97f));
            var rt = panel.rectTransform;
            UiFactory.Stretch(rt);
            _tab = 0;
            _riderPick = 0;
            Render(sim, rt, onReplay, onContinue);
            return rt;
        }

        /// <summary>Tabs: podium, classification, gaps, then one page per player rider with the "why" of the result.</summary>
        private static void Render(RaceSimulation sim, RectTransform rt, Action onReplay, Action onContinue)
        {
            Ui.CloseSheet();
            UiFactory.Clear(rt);
            // Top right: the replay (secondary) and the one way on, CONTINUAR, as the header's slanted block.
            const float replayW = 260;
            RectTransform slot;
            RaceUi.TopBar(rt, Loc.T("post.title", Txt(sim.Config.SessionName)), null, RaceUi.ActionW + replayW + Ui.Grid.Gap, out slot);
            var contArea = UiFactory.Rect("Continue", slot);
            contArea.anchorMin = new Vector2(1, 0);
            contArea.anchorMax = new Vector2(1, 1);
            contArea.pivot = new Vector2(1, 0.5f);
            contArea.offsetMin = new Vector2(-RaceUi.ActionW, 0);
            contArea.offsetMax = Vector2.zero;
            RaceUi.ActionBlock(contArea, Loc.T("post.continue"), () => onContinue());
            var replayArea = UiFactory.Rect("Replay", slot);
            replayArea.anchorMin = new Vector2(0, 0);
            replayArea.anchorMax = new Vector2(0, 1);
            replayArea.pivot = new Vector2(0, 0.5f);
            replayArea.offsetMin = new Vector2(0, 8);
            replayArea.offsetMax = new Vector2(replayW, -8);
            Ui.Secondary(replayArea, Loc.T("post.replay"), () => onReplay());

            // The weekend stepper when this is a session of the player's weekend (race flow).
            float top = RaceUi.BarH + 8;
            var weekend = RaceFlow.ActiveWeekend;
            if (weekend != null && weekend.CurrentSession != null && weekend.CurrentSession.Name == sim.Config.SessionName)
            {
                var steps = WeekendStepper.Steps(weekend);
                WeekendStepper.Strip(rt, top, steps, WeekendStepper.StepOf(steps, weekend.SessionIndex));
                top += WeekendStepper.Height + 8;
            }

            var players = new List<RiderRaceState>();
            foreach (var r in sim.Riders) if (r.Entry.IsPlayer) players.Add(r);
            var labels = new List<string> { Loc.T("ux.race.podium"), Loc.T("post.tab.classification"), Loc.T("post.tab.gaps") };
            bool riderPicker = players.Count > MaxRiderTabs;
            if (riderPicker) labels.Add(Loc.T("ux.race.riders"));
            else foreach (var r in players) labels.Add(r.Entry.Rider.ShortName);
            if (_tab >= labels.Count) _tab = 0;

            var tabs = RaceUi.Content(rt, top, 0);
            tabs.anchorMin = new Vector2(0, 1);
            tabs.offsetMin = new Vector2(RaceUi.Pad, -top - Ui.Grid.Hit);
            var tabList = tabs.gameObject.AddComponent<VerticalLayoutGroup>();
            tabList.childControlHeight = true;
            tabList.childControlWidth = true;
            tabList.childForceExpandWidth = true;
            Ui.Segmented(tabs, labels, _tab, i => { _tab = i; Render(sim, rt, onReplay, onContinue); });

            var area = RaceUi.Content(rt, top + Ui.Grid.Hit + 8, RaceUi.Pad);
            var points = PointsTable(sim);
            switch (_tab)
            {
                case 0: Podium(sim, area, players, points); break;
                case 1: BuildClassification(sim, area, points); break;
                case 2: GapChart(sim, area); break;
                default:
                    int idx = riderPicker ? Mathf.Clamp(_riderPick, 0, players.Count - 1) : _tab - 3;
                    if (idx >= 0 && idx < players.Count)
                    {
                        if (riderPicker)
                        {
                            var names = new List<string>();
                            foreach (var p in players) names.Add(p.Entry.Rider.FullName);
                            var pick = UiFactory.Rect("Pick", area);
                            pick.anchorMin = new Vector2(0, 1);
                            pick.anchorMax = new Vector2(0.4f, 1);
                            pick.pivot = new Vector2(0, 1);
                            pick.offsetMin = new Vector2(0, -Ui.Grid.Hit);
                            pick.offsetMax = Vector2.zero;
                            Ui.Picker(pick, Loc.T("ux.race.riders"), names, idx, i => { _riderPick = i; Render(sim, rt, onReplay, onContinue); });
                            var below = UiFactory.Rect("Page", area);
                            UiFactory.Stretch(below);
                            below.offsetMax = new Vector2(0, -Ui.Grid.Hit - 8);
                            area = below;
                        }
                        RiderPage(sim, area, players[idx], points);
                    }
                    break;
            }
        }

        /// <summary>Series of the gap to the leader lap by lap (podium and the player's riders); null with too few laps.</summary>
        public static List<ChartSeries> GapSeries(RaceSimulation sim, out int laps)
        {
            laps = 0;
            foreach (var r in sim.Riders) laps = Math.Max(laps, r.Stats.LapTimes.Count);
            if (laps < 2) return null;
            // Elapsed race time of each rider at the end of each lap, and the leader's.
            var elapsed = new Dictionary<int, List<double>>();
            var leader = new double[laps];
            for (int l = 0; l < laps; l++) leader[l] = double.MaxValue;
            foreach (var r in sim.Riders)
            {
                var t = new List<double>();
                double sum = 0;
                foreach (var lap in r.Stats.LapTimes)
                {
                    sum += lap;
                    t.Add(sum);
                }
                elapsed[r.Index] = t;
                for (int l = 0; l < t.Count; l++) if (t[l] < leader[l]) leader[l] = t[l];
            }
            var chosen = new List<RiderRaceState>();
            for (int p = 1; p <= 3; p++)
            {
                var r = sim.RiderAtPosition(p);
                if (r != null) chosen.Add(r);
            }
            foreach (var r in sim.Riders) if (r.Entry.IsPlayer && !chosen.Contains(r)) chosen.Add(r);
            var series = new List<ChartSeries>();
            foreach (var r in chosen)
            {
                var team = r.Entry.Team;
                var s = new ChartSeries
                {
                    Name = "P" + r.Position + " " + r.Entry.Rider.ShortName,
                    Color = r.Entry.IsPlayer ? Theme.Timing : Charts.Readable(team != null ? team.Colors.Primary : null, Color.gray),
                    Highlight = r.Entry.IsPlayer
                };
                s.Values.Add(0);
                var t = elapsed[r.Index];
                for (int l = 0; l < laps; l++) s.Values.Add(l < t.Count ? Math.Min(30, t[l] - leader[l]) : double.NaN);
                series.Add(s);
            }
            return series;
        }

        /// <summary>The gaps chart in a card (explanation behind the info button); shared with the event summary.</summary>
        public static void GapCard(Transform list, List<ChartSeries> series, int laps, float height)
        {
            var card = Ui.Card(list, Loc.T("post.gaps"), Loc.T("post.gapsSub"),
                () => Ui.Sheet(Loc.T("post.gaps"), l => UiFactory.Paragraph(l, Loc.T("post.gapsNote"), Theme.TextBody)));
            if (series == null)
            {
                Ui.Empty(card, null, Loc.T("post.gapsNone"));
                return;
            }
            Charts.LineChart(card, height, series, true, v => "+" + v.ToString("0.0", Loc.Culture), Loc.T("post.gapStart"), Loc.T("post.gapLap", laps));
        }

        /// <summary>Gap to the leader lap by lap: the podium and the player's riders.</summary>
        private static void GapChart(RaceSimulation sim, RectTransform area)
        {
            var list = UiFactory.ScrollList("Gaps", area);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            int laps;
            var series = GapSeries(sim, out laps);
            GapCard(list, series, laps, 340);
        }

        private static string Txt(string session)
        {
            return Loc.Or("session." + session, session);
        }

        /// <summary>P2 | P1 | P3 cards (the winner's taller), then a line per player rider outside the podium.</summary>
        private static void Podium(RaceSimulation sim, RectTransform area, List<RiderRaceState> players, List<int> points)
        {
            var cls = sim.GetClassification();
            var podiumArea = UiFactory.Rect("Podium", area);
            UiFactory.Stretch(podiumArea);
            podiumArea.offsetMin = new Vector2(0, 84);
            var cols = Ui.Columns12(podiumArea, 4, 4, 4);
            int[] order = { 1, 0, 2 };
            for (int k = 0; k < 3; k++)
            {
                int p = order[k];
                var col = cols[k];
                if (p >= cls.Count) continue;
                var e = cls[p];
                var r = Find(sim, e.RiderId);
                if (r == null) continue;
                float top = p == 0 ? 1 : p == 1 ? 0.9f : 0.82f;
                var slot = UiFactory.Rect("Step", col);
                UiFactory.Place(slot, 0, 0, 1, top);
                PodiumCard(slot, sim, e, r, p == 0, points);
            }

            // The player's riders: where they finished (podium included, so the line is always there).
            var mine = UiFactory.Rect("Mine", area);
            mine.anchorMin = new Vector2(0, 0);
            mine.anchorMax = new Vector2(1, 0);
            mine.pivot = new Vector2(0.5f, 0);
            mine.offsetMin = Vector2.zero;
            mine.offsetMax = new Vector2(0, 72);
            var bg = UiFactory.Decor("Bg", mine, Theme.Alpha(Theme.Tile, 0.9f));
            UiFactory.Stretch(bg.rectTransform);
            var cap = RaceUi.Label(mine, Loc.T("ux.race.yourRiders"), Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Normal);
            UiFactory.Place(cap.rectTransform, 0, 0, 0.18f, 1);
            cap.rectTransform.offsetMin = new Vector2(16, 0);
            int n = Mathf.Max(1, players.Count);
            for (int i = 0; i < players.Count; i++)
            {
                var r = players[i];
                ResultEntry e = null;
                foreach (var c in cls) if (c.RiderId == r.Entry.Rider.Id) e = c;
                string res = e == null ? "-" : e.Status == ResultStatus.Finished ? Theme.TimingText("<b>P" + e.Position + "</b>") : Theme.BadText(Loc.Or("result." + e.Status, e.Status.ToString()));
                int pts = e != null && e.Status == ResultStatus.Finished && e.Position - 1 < points.Count ? points[e.Position - 1] : 0;
                string gained = e != null && e.Status == ResultStatus.Finished ? "  " + Theme.Signed(e.GridPosition - e.Position) : "";
                var t = RaceUi.Label(mine, res + "  <b>" + Theme.Caps(r.Entry.Rider.ShortName) + "</b>" + gained + "   " + Theme.Dim(pts + " " + Theme.Caps(Loc.T("post.col.pts"))),
                    Theme.TextBody, TextAnchor.MiddleLeft, Theme.Text);
                float w = 0.82f / n;
                UiFactory.Place(t.rectTransform, 0.18f + w * i, 0, 0.18f + w * (i + 1), 1);
            }
        }

        private static void PodiumCard(RectTransform area, RaceSimulation sim, ResultEntry e, RiderRaceState r, bool winner, List<int> points)
        {
            var team = r.Entry.Team;
            var teamColor = RaceUi.TeamColor(team != null ? team.Colors.Primary : null);
            bool mine = r.Entry.IsPlayer;
            var body = Ui.Tile(area, null, null, null, 0, null, null, false, mine ? Theme.Accent : teamColor);
            var posArea = UiFactory.Rect("Pos", body);
            posArea.anchorMin = new Vector2(0.5f, 1);
            posArea.anchorMax = new Vector2(0.5f, 1);
            posArea.pivot = new Vector2(0.5f, 1);
            posArea.sizeDelta = new Vector2(winner ? 160 : 136, winner ? 84 : 72);
            posArea.anchoredPosition = new Vector2(0, -4);
            RaceUi.Block(posArea, "P" + e.Position, winner ? Theme.Timing : Theme.Plate, winner ? Theme.TextDisplay : Theme.TextKey);

            var plateArea = UiFactory.Rect("PlateArea", body);
            plateArea.anchorMin = plateArea.anchorMax = new Vector2(0.5f, 0.5f);
            plateArea.sizeDelta = new Vector2(84, 60);
            plateArea.anchoredPosition = new Vector2(0, 18);
            RaceUi.Plate(plateArea, r.Entry.Rider.Number, teamColor, Theme.TextTitle);

            var first = RaceUi.Label(body, r.Entry.Rider.FirstName, Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(first.rectTransform, 0, 0.3f, 1, 0.4f);
            var last = RaceUi.Label(body, Theme.Caps(r.Entry.Rider.LastName), Theme.TextTitle, TextAnchor.MiddleCenter, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Place(last.rectTransform, 0, 0.18f, 1, 0.31f);
            string teamName = team != null ? team.ShortName ?? team.Name : "";
            var tn = RaceUi.Label(body, teamName, Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Normal);
            UiFactory.Place(tn.rectTransform, 0, 0.1f, 1, 0.18f);
            string time = e.Status != ResultStatus.Finished ? Loc.Or("result." + e.Status, e.Status.ToString()) :
                e.Position == 1 ? MathX.FormatLapTime(e.TotalTime) : MathX.FormatGap(e.GapToWinner);
            int pts = e.Status == ResultStatus.Finished && e.Position - 1 < points.Count ? points[e.Position - 1] : 0;
            var tl = RaceUi.Label(body, "<b>" + time + "</b>   " + Theme.Dim(pts + " " + Theme.Caps(Loc.T("post.col.pts"))), Theme.TextBody, TextAnchor.MiddleCenter, Theme.Text);
            UiFactory.Place(tl.rectTransform, 0, 0, 1, 0.1f);
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
            Ui.Kpis(card,
                Ui.Kpi("P" + rep.Grid, Loc.T("post.k.grid")),
                Ui.Kpi(r.Status == RaceStatus.Finished ? "P" + rep.Finish : Theme.BadText(Loc.T("result.DNF")), Loc.T("post.k.finish")),
                Ui.Kpi(pts.ToString(), Loc.T("post.k.points")),
                Ui.Kpi(rep.BestLap > 0 ? MathX.FormatLapTime(rep.BestLap) : "-", Loc.T("post.k.bestLap")));
            Ui.Line(card, Loc.T("post.k.gained"), Theme.Signed(rep.PositionsGained));
            Ui.Line(card, Loc.T("post.k.topSpeed"), rep.TopSpeedKph.ToString("0"));
            Ui.Line(card, Loc.T("post.k.passes"), rep.Overtakes + " / " + rep.TimesPassed);
            Ui.Line(card, Loc.T("post.k.errors"), rep.Errors.ToString());
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
                    mine[i + 1] = top ? "<color=" + Theme.FastestHex + ">" + t + "</color>" : t;
                    best[i + 1] = i < fastest.Length && fastest[i] < double.MaxValue ? fastest[i].ToString("0.000", Loc.Culture) : "-";
                }
                UiFactory.TableRow(card, 40, w, head, true);
                UiFactory.TableRow(card, 44, w, mine);
                UiFactory.TableRow(card, 44, w, best);
            }

            // "Why?": three strengths and three weaknesses in view, the full analysis behind the info button.
            var why = Ui.Card(right, Loc.T("post.why"), null, () => Ui.Sheet(Loc.T("post.why"), l =>
            {
                Items(l, Loc.T("post.strengths"), rep.Strengths, Theme.GoodHex, "+", int.MaxValue);
                Items(l, Loc.T("post.weaknesses"), rep.Weaknesses, Theme.BadHex, "-", int.MaxValue);
            }));
            Items(why, Loc.T("post.strengths"), rep.Strengths, Theme.GoodHex, "+", 3);
            Items(why, Loc.T("post.weaknesses"), rep.Weaknesses, Theme.BadHex, "-", 3);
        }

        private static void Items(Transform list, string heading, List<AnalysisItem> items, string color, string sign, int max)
        {
            UiFactory.Paragraph(list, "<b>" + Theme.Caps(heading) + "</b>", Theme.TextCaption);
            if (items.Count == 0) UiFactory.Paragraph(list, Theme.Dim(Loc.T("post.nothing")), Theme.TextBody);
            for (int i = 0; i < items.Count && i < max; i++)
            {
                var it = items[i];
                UiFactory.Paragraph(list, "<color=" + color + "><b>" + sign + "</b></color>  " + Loc.T("analysis." + it.Code + (it.Strength ? "+" : "-"), Math.Abs(it.Value)), Theme.TextBody);
            }
            if (items.Count > max) UiFactory.Paragraph(list, Theme.Dim("+" + (items.Count - max) + "  " + Theme.Caps(Loc.T("ux.race.more"))), Theme.TextCaption);
        }

        private static List<int> PointsTable(RaceSimulation sim)
        {
            foreach (var s in sim.Rules.Weekend.Sessions)
                if (s.Type == sim.Config.SessionType && s.Name == sim.Config.SessionName) return sim.Rules.GetPoints(s.PointsSystem);
            return sim.Rules.GetPoints(sim.Config.SessionType == SessionType.Sprint ? "sprint" : "race");
        }

        /// <summary>
        /// The classification as list rows: P | rider (team bar, positions gained) | time or gap | best lap | points |
        /// notes (fastest lap, penalties, technical issue). A finished player row is lit in the accent.
        /// </summary>
        private static void BuildClassification(RaceSimulation sim, RectTransform rt, List<int> points)
        {
            var w = new[] { 0.9f, 4.2f, 2.2f, 2f, 0.9f, 2.4f };
            var head = UiFactory.Rect("Captions", rt);
            head.anchorMin = new Vector2(0, 1);
            head.anchorMax = new Vector2(1, 1);
            head.pivot = new Vector2(0.5f, 1);
            head.offsetMin = new Vector2(0, -40);
            head.offsetMax = Vector2.zero;
            var hl = head.gameObject.AddComponent<VerticalLayoutGroup>();
            hl.childControlHeight = true;
            hl.childControlWidth = true;
            hl.childForceExpandWidth = true;
            Ui.ListCaptions(head, w, new[]
            {
                Loc.T("post.col.pos"), Loc.T("post.col.rider"), Loc.T("post.col.time"), Loc.T("post.col.best"), Loc.T("post.col.pts"), Loc.T("post.col.notes")
            });
            var holder = UiFactory.Rect("Rows", rt);
            UiFactory.Stretch(holder);
            holder.offsetMax = new Vector2(0, -40);
            var list = UiFactory.ScrollList("Classification", holder, 0);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 0;
            layout.padding = new RectOffset(0, 0, 0, 8);
            int index = 0;
            foreach (var e in sim.GetClassification())
            {
                var r = Find(sim, e.RiderId);
                if (r == null) continue;
                bool finished = e.Status == ResultStatus.Finished;
                string time = finished ? (e.Position == 1 ? MathX.FormatLapTime(e.TotalTime) : MathX.FormatGap(e.GapToWinner)) : Theme.BadText(Theme.Caps(Loc.Or("result." + e.Status, e.Status.ToString())));
                int gained = e.GridPosition - e.Position;
                string gainedText = finished && gained != 0 ? "  <size=" + Theme.TextCaption + ">" + Theme.Signed(gained) + "</size>" : "";
                int pts = finished && e.Position - 1 < points.Count ? points[e.Position - 1] : 0;
                var notes = new List<string>();
                if (e.FastestLap) notes.Add("<color=" + Theme.FastestHex + ">" + Theme.Caps(Loc.T("ux.race.fastestLap")) + "</color>");
                if (e.Note != null) notes.Add(Theme.WarnText(e.Note));
                if (r.Penalties.Count > 0 && e.Note == null) notes.Add(Theme.WarnText(Loc.T("post.penalty", r.Penalties.Count)));
                if (r.HasIssue) notes.Add(Theme.WarnText(Loc.T("post.issue")));
                var row = Ui.ListRow(list, w, new[]
                {
                    finished ? e.Position.ToString() : "-", Theme.Caps(r.Entry.Rider.ShortName) + gainedText, time,
                    e.BestLap > 0 ? MathX.FormatLapTime(e.BestLap) : "-", pts > 0 ? "<b>" + pts + "</b>" : Theme.Dim("0"),
                    "<size=" + Theme.TextCaption + ">" + string.Join(" · ", notes.ToArray()) + "</size>"
                }, index++, r.Entry.IsPlayer, null, 56);
                var team = r.Entry.Team;
                RaceUi.TeamBar(row, w, RaceUi.TeamColor(team != null ? team.Colors.Primary : null));
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

        private static RiderRaceState Find(RaceSimulation sim, string riderId)
        {
            foreach (var r in sim.Riders)
                if (r.Entry.Rider.Id == riderId) return r;
            return null;
        }
    }
}
