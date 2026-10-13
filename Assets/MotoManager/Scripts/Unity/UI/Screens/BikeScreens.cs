using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Race;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// Small layout helpers shared by the development, finance and shop screens (UX work package 3). They only
    /// combine the design-system pieces of <see cref="Ui"/>; nothing here is styled by hand beyond plain rows.
    /// </summary>
    internal static class ScreenKit
    {
        /// <summary>A vertical layout list filling <paramref name="parent"/> (so list-style components can go in a tile body).</summary>
        public static RectTransform List(RectTransform parent, float spacing = 8)
        {
            var rt = UiFactory.Rect("List", parent);
            UiFactory.Stretch(rt);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return rt;
        }

        /// <summary>A rect of fixed height along the top of <paramref name="parent"/>, <paramref name="from"/> units below its top edge.</summary>
        public static RectTransform Top(RectTransform parent, float from, float height)
        {
            var rt = UiFactory.Rect("Top", parent);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(0, -from - height);
            rt.offsetMax = new Vector2(0, -from);
            return rt;
        }

        /// <summary>A rect of fixed height along the bottom of <paramref name="parent"/>, <paramref name="from"/> units above its bottom edge.</summary>
        public static RectTransform Bottom(RectTransform parent, float from, float height)
        {
            var rt = UiFactory.Rect("Bottom", parent);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(0, from);
            rt.offsetMax = new Vector2(0, from + height);
            return rt;
        }

        /// <summary>The part of <paramref name="parent"/> between <paramref name="top"/> units under its top and <paramref name="bottom"/> above its bottom.</summary>
        public static RectTransform Between(RectTransform parent, float top, float bottom)
        {
            var rt = UiFactory.Rect("Middle", parent);
            UiFactory.Stretch(rt);
            rt.offsetMin = new Vector2(0, bottom);
            rt.offsetMax = new Vector2(0, -top);
            return rt;
        }

        /// <summary><paramref name="n"/> equal columns (a gap between them).</summary>
        public static RectTransform[] Cols(RectTransform parent, int n)
        {
            n = Math.Max(1, n);
            var spans = new float[n];
            for (int i = 0; i < n; i++) spans[i] = 1;
            var res = new RectTransform[n];
            for (int i = 0; i < n; i++)
            {
                var rt = UiFactory.Rect("Col" + i, parent);
                rt.anchorMin = new Vector2((float)i / n, 0);
                rt.anchorMax = new Vector2((float)(i + 1) / n, 1);
                rt.offsetMin = new Vector2(i == 0 ? 0 : Ui.Grid.Gap / 2, 0);
                rt.offsetMax = new Vector2(i == n - 1 ? 0 : -Ui.Grid.Gap / 2, 0);
                res[i] = rt;
            }
            return res;
        }

        /// <summary>A label placed by anchors (best fit 18..size).</summary>
        public static Text At(Transform parent, float x0, float y0, float x1, float y1, string text, int size, TextAnchor align,
            Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var t = Ui.Label(parent, text, size, align, color, style);
            UiFactory.Place(t.rectTransform, x0, y0, x1, y1);
            return t;
        }

        /// <summary>An action opening an information sheet with one paragraph per non-empty string.</summary>
        public static Action Info(string title, params string[] paragraphs)
        {
            return () => Ui.Sheet(title, list =>
            {
                foreach (var p in paragraphs)
                    if (!string.IsNullOrEmpty(p)) UiFactory.Paragraph(list, p, Theme.TextBody);
            });
        }

        /// <summary>Makes an image the button of <paramref name="onTap"/> (click sound, press tint).</summary>
        public static void Tap(Image img, Action onTap)
        {
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            b.colors = colors;
            b.onClick.AddListener(() =>
            {
                GameAudio.ClickSound();
                onTap();
            });
        }

        /// <summary>A figure: a dim CAPS caption over a big value (and an optional dim line under it).</summary>
        public static void Figure(RectTransform parent, float x0, float x1, string caption, string value, Color? color = null, string sub = null, int size = Theme.TextKey)
        {
            var area = UiFactory.Rect("Figure", parent);
            UiFactory.Place(area, x0, 0, x1, 1);
            float subH = string.IsNullOrEmpty(sub) ? 0 : 0.22f;
            At(area, 0, 0.74f, 1, 1, Theme.Caps(caption), Theme.TextCaption, TextAnchor.MiddleCenter, Color.Lerp(Theme.TextDim, Color.white, 0.2f), FontStyle.Bold);
            var v = At(area, 0, subH, 1, 0.74f, value, size, TextAnchor.MiddleCenter, color ?? Theme.Text, FontStyle.BoldAndItalic);
            if (subH > 0) At(area, 0, 0, 1, subH, Theme.Caps(sub), Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);
        }

        /// <summary>A small fact: CAPS caption over a bold value, in a horizontal slice of <paramref name="parent"/>.</summary>
        public static void Fact(RectTransform parent, float x0, float x1, string caption, string value)
        {
            var area = UiFactory.Rect("Fact", parent);
            UiFactory.Place(area, x0, 0, x1, 1);
            At(area, 0, 0.52f, 1, 1, Theme.Caps(caption), Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);
            At(area, 0, 0, 1, 0.56f, "<b>" + value + "</b>", Theme.TextBody, TextAnchor.MiddleCenter, Theme.Text);
        }

        /// <summary>A horizontal row of pills (left aligned) in a list.</summary>
        public static RectTransform PillRow(Transform list)
        {
            var row = UiFactory.Row(list, 36);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return row;
        }

        /// <summary>A muted pill for status (low saturation: tinted fill, coloured text).</summary>
        public static Image Tag(Transform parent, string text, Color color)
        {
            return Ui.Pill(parent, text, Theme.Alpha(color, 0.2f), color);
        }

        private const float ChevronW = 44, ActionW = 200;

        /// <summary>
        /// A 64-unit data row without a position block: first cell left and bold, the others centred; the whole row
        /// is the button with <paramref name="onTap"/> (chevron at the right); an optional secondary button at the right.
        /// </summary>
        public static RectTransform DataRow(Transform list, float[] widths, string[] cells, int index, Action onTap = null, UiAct? action = null,
            bool dim = false, float height = 64)
        {
            var row = UiFactory.Row(list, height);
            var bg = row.gameObject.AddComponent<Image>();
            bg.raycastTarget = false;
            bg.color = index % 2 == 0 ? new Color(1, 1, 1, 0.055f) : new Color(1, 1, 1, 0.02f);
            if (onTap != null) Tap(bg, onTap);
            float right = 0;
            if (action.HasValue)
            {
                var a = action.Value;
                var area = UiFactory.Rect("Action", row);
                area.anchorMin = new Vector2(1, 0);
                area.anchorMax = new Vector2(1, 1);
                area.pivot = new Vector2(1, 0.5f);
                area.offsetMin = new Vector2(-ActionW - 8, 4);
                area.offsetMax = new Vector2(-8, -4);
                Ui.Secondary(area, a.Key, a.Value, a.Value != null);
                right = ActionW + 16;
            }
            else if (onTap != null)
            {
                var ch = UiIcons.Chevron(row, Theme.TextDim);
                ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(1, 0.5f);
                ch.rectTransform.sizeDelta = new Vector2(24, 24);
                ch.rectTransform.anchoredPosition = new Vector2(-22, 0);
                right = ChevronW;
            }
            Cells(row, widths, cells, right, false, dim);
            return row;
        }

        /// <summary>Column captions for <see cref="DataRow"/> (same widths; leave room for the action or chevron).</summary>
        public static RectTransform Captions(Transform list, float[] widths, string[] captions, bool action = false, bool chevron = false)
        {
            var row = UiFactory.Row(list, 40);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.5f);
            bg.raycastTarget = false;
            Cells(row, widths, captions, action ? ActionW + 16 : chevron ? ChevronW : 0, true, false);
            return row;
        }

        private static void Cells(RectTransform row, float[] widths, string[] cells, float right, bool header, bool dim)
        {
            var area = UiFactory.Rect("Cells", row);
            UiFactory.Stretch(area);
            area.offsetMax = new Vector2(-right, 0);
            float total = 0, x = 0;
            foreach (var w in widths) total += w;
            if (total <= 0) total = 1;
            for (int i = 0; i < cells.Length && i < widths.Length; i++)
            {
                float x0 = x / total, x1 = (x + widths[i]) / total;
                x += widths[i];
                bool first = i == 0;
                Color c = header ? Color.Lerp(Theme.TextDim, Color.white, 0.15f) : dim ? Theme.TextDim : Theme.Text;
                var t = At(area, x0, 0, x1, 1, header ? Theme.Caps(cells[i] ?? "") : (cells[i] ?? ""), header ? Theme.TextCaption : Theme.TextBody,
                    first ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, c, header || first ? FontStyle.Bold : FontStyle.Normal);
                t.rectTransform.offsetMin = new Vector2(first ? 16 : 4, 0);
                t.rectTransform.offsetMax = new Vector2(-4, 0);
            }
        }
    }

    /// <summary>
    /// "Moto" tab of the development section (UX spec §3.10), one screen without scrolling: our bike (its average
    /// component level big in the rating colour, performance and reliability), how it ranks against every bike of
    /// the category, the R&amp;D and facilities tiles, and one tile per component with its level that opens its
    /// development. Maker, class and the on-track values are behind the "i" of our bike's tile.
    /// </summary>
    public class BikeScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.bike"); } }

        private const int GridRows = 5;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var bike = world.GetBike(team.BikeId);
            if (bike == null) return;
            var bikeClass = Game.Db.GetBikeClass(bike.BikeClassId);
            var maker = world.GetManufacturer(bike.ManufacturerId);
            var perf = BikePerformance.Compute(bike, bikeClass);
            var running = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status == ProjectStatus.InProgress);
            var comps = Components(bike, bikeClass);
            bool twoRows = comps.Count > 6;

            var rows = Ui.Rows(content, twoRows ? 0.47f : 0.52f, twoRows ? 0.53f : 0.48f);
            var top = Ui.Columns12(rows[0], 4, 5, 3);

            // ---- our bike and the rest of the grid
            var grid = GridLevels(world, team);
            int rank = grid.FindIndex(kv => kv.Key == team) + 1;
            string makerName = maker != null ? maker.Name : "";
            OurBike(top[0], world, team, bike, bikeClass, makerName, perf, grid, rank);
            GridTile(top[1], team, grid, rank);

            // ---- R&D and facilities
            var side = Ui.Rows(top[2], 1, 1);
            int capacity = TeamCapabilities.ProjectCapacity(team);
            int free = capacity - running.Count;
            Ui.StatTile(side[0], Loc.T("bike.rndTile"), running.Count + "/" + capacity,
                free > 0 ? Loc.T("bike.rndFree", free) : Theme.WarnText(Loc.T("bike.rndFull")), () => Shell.Show<ProjectsPage>());
            var types = (FacilityType[])Enum.GetValues(typeof(FacilityType));
            double sum = 0;
            foreach (var f in types) sum += team.Facility(f);
            double avg = types.Length > 0 ? sum / types.Length : 0;
            Ui.StatTile(side[1], Loc.T("tab.facilities"), avg.ToString("0.0", Loc.Culture), Loc.T("ux.dev.facAvg", types.Length),
                () => Shell.Show<FacilitiesPage>());

            // ---- components: one tile each (level), tap to develop; at most 6 per row
            if (comps.Count == 0) return;
            int perRow = twoRows ? (comps.Count + 1) / 2 : comps.Count;
            int nRows = (comps.Count + perRow - 1) / perRow;
            var shares = new float[nRows];
            for (int i = 0; i < nRows; i++) shares[i] = 1;
            var compRows = Ui.Rows(rows[1], shares);
            for (int r = 0; r < nRows; r++)
            {
                var cols = ScreenKit.Cols(compRows[r], perRow);
                for (int c = 0; c < perRow; c++)
                {
                    int idx = r * perRow + c;
                    if (idx >= comps.Count) break;
                    ComponentTile(cols[c], world, team, bike, comps[idx], running);
                }
            }
        }

        /// <summary>The components of the bike in the order of its class (unknown ones last).</summary>
        public static List<string> Components(Motorcycle bike, BikeClassDefinition bikeClass)
        {
            var list = new List<string>();
            if (bikeClass != null)
                foreach (var c in bikeClass.Components)
                    if (bike.Components.ContainsKey(c)) list.Add(c);
            foreach (var c in bike.Components.Keys)
                if (!list.Contains(c)) list.Add(c);
            return list;
        }

        /// <summary>The mean level of a bike's components (0-100).</summary>
        public static double AverageLevel(Motorcycle bike)
        {
            if (bike == null || bike.Components.Count == 0) return 0;
            double sum = 0;
            foreach (var c in bike.Components.Keys) sum += bike.Component(c);
            return sum / bike.Components.Count;
        }

        /// <summary>Every team of our category with its bike's average level, best first.</summary>
        private static List<KeyValuePair<Team, double>> GridLevels(WorldState world, Team team)
        {
            var res = new List<KeyValuePair<Team, double>>();
            foreach (var t in world.TeamsInCategory(team.CategoryId))
            {
                var b = world.GetBike(t.BikeId);
                if (b != null) res.Add(new KeyValuePair<Team, double>(t, AverageLevel(b)));
            }
            if (!res.Exists(kv => kv.Key == team)) res.Add(new KeyValuePair<Team, double>(team, AverageLevel(world.GetBike(team.BikeId))));
            res.Sort((a, b) => b.Value.CompareTo(a.Value));
            return res;
        }

        private static string TeamName(Team t)
        {
            return string.IsNullOrEmpty(t.ShortName) ? t.Name : t.ShortName;
        }

        /// <summary>Overall on-track performance: the mean of every value except reliability (shown on its own).</summary>
        public static double PerformanceScore(BikePerformance perf)
        {
            double sum = 0;
            int n = 0;
            foreach (PerfAttr a in Enum.GetValues(typeof(PerfAttr)))
            {
                if (a == PerfAttr.Reliability) continue;
                sum += perf[a];
                n++;
            }
            return n > 0 ? sum / n : 0;
        }

        /// <summary>Reliability of a rider's bike: the bike's own plus his mechanics and the factory (0-100).</summary>
        public static double Reliability(WorldState world, Team team, BikePerformance perf, Rider r)
        {
            double bonus = r != null ? TeamCapabilities.ReliabilityBonus(world, team, r.Id) : 0;
            return MathX.Clamp(perf[PerfAttr.Reliability] + bonus, 0, 100);
        }

        /// <summary>Our bike: the average level big, the gap to the best bike, performance and reliability.</summary>
        private void OurBike(RectTransform area, WorldState world, Team team, Motorcycle bike, BikeClassDefinition bikeClass, string makerName,
            BikePerformance perf, List<KeyValuePair<Team, double>> grid, int rank)
        {
            var body = Ui.Tile(area, Loc.T("ux.dev.ourBike"), null, bike.Model, 0, null, () => BikeSheet(bike, bikeClass, makerName, perf));
            double level = AverageLevel(bike);
            double best = grid.Count > 0 ? grid[0].Value : level;
            ScreenKit.At(body, 0, 0.78f, 0.44f, 1, Theme.Caps(Loc.T("ux.dev.avgLevel")), Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);
            var v = ScreenKit.At(body, 0, 0.2f, 0.44f, 0.8f, level.ToString("0"), Theme.TextDisplay, TextAnchor.MiddleCenter, Ui.RatingColor(level), FontStyle.BoldAndItalic);
            string gap = rank == 1 ? Theme.GoodText(Theme.Caps(Loc.T("ux.dev.best")))
                : Theme.Caps(Loc.T("ux.dev.gap", (best - level).ToString("0.#", Loc.Culture)));
            ScreenKit.At(body, 0, 0, 0.44f, 0.2f, gap, Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);

            var right = UiFactory.Rect("Stats", body);
            UiFactory.Place(right, 0.48f, 0, 1, 1);
            double perfScore = PerformanceScore(perf);
            StatLine(right, 0.54f, 0.96f, Loc.T("bike.perfShort"), perfScore.ToString("0"), perfScore / 100.0, Theme.Perf);
            // Reliability includes each rider's mechanics: the mean over the team's riders.
            double rel = 0;
            int n = 0;
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r == null) continue;
                rel += Reliability(world, team, perf, r);
                n++;
            }
            rel = n > 0 ? rel / n : Reliability(world, team, perf, null);
            StatLine(right, 0.04f, 0.46f, Loc.T("bike.relShort"), rel.ToString("0") + "%", rel / 100.0, Theme.Rel);
        }

        /// <summary>Caption left, value right (white), a thick bar in the figure's colour under them.</summary>
        private static void StatLine(RectTransform parent, float y0, float y1, string caption, string value, double fraction, Color color)
        {
            var row = UiFactory.Rect("Stat", parent);
            UiFactory.Place(row, 0, y0, 1, y1);
            ScreenKit.At(row, 0, 0.36f, 0.62f, 1, Theme.Caps(caption), Theme.TextCaption, TextAnchor.MiddleLeft,
                Color.Lerp(Theme.TextDim, Color.white, 0.25f), FontStyle.Bold);
            ScreenKit.At(row, 0.6f, 0.3f, 1, 1, value, Theme.TextTitle, TextAnchor.MiddleRight, Theme.Text, FontStyle.BoldAndItalic);
            var bar = UiFactory.Rect("Bar", row);
            UiFactory.Place(bar, 0, 0.02f, 1, 0.3f);
            Ui.Gauge(bar, fraction, color);
        }

        /// <summary>
        /// The ranking of the category's bikes by average level: the best ones and ours with its neighbours (ours lit),
        /// the whole list on tap.
        /// </summary>
        private static void GridTile(RectTransform area, Team team, List<KeyValuePair<Team, double>> grid, int rank)
        {
            var body = Ui.Tile(area, Loc.T("ux.dev.grid"), () => GridSheet(team, grid), Loc.T("ux.dev.rank", rank, grid.Count));
            int n = grid.Count;
            if (n == 0) return;
            var shown = new List<int>();
            if (n <= GridRows) for (int i = 0; i < n; i++) shown.Add(i);
            else
            {
                int ours = rank - 1;
                int start = Math.Max(0, Math.Min(ours - 2, n - GridRows));
                for (int i = start; i < start + GridRows; i++) shown.Add(i);
                if (start > 0) shown[0] = 0;
            }
            double hi = grid[0].Value, lo = grid[n - 1].Value;
            double floor = Math.Max(0, lo - Math.Max(3, (hi - lo) * 0.5));
            for (int k = 0; k < shown.Count; k++)
            {
                int i = shown[k];
                bool mine = grid[i].Key == team;
                var row = UiFactory.Rect("Bike" + i, body);
                UiFactory.Place(row, 0, 1 - (float)(k + 1) / GridRows, 1, 1 - (float)k / GridRows);
                if (mine)
                {
                    var bg = UiFactory.Decor("Mine", row, Theme.Alpha(Theme.CurrentAccent, 0.22f));
                    UiFactory.Stretch(bg.rectTransform);
                }
                Color c = mine ? Theme.Text : Color.Lerp(Theme.TextDim, Color.white, 0.2f);
                ScreenKit.At(row, 0, 0, 0.09f, 1, (i + 1).ToString(), Theme.TextCaption, TextAnchor.MiddleCenter, c, FontStyle.Bold);
                ScreenKit.At(row, 0.1f, 0, 0.46f, 1, Theme.Caps(TeamName(grid[i].Key)), Theme.TextCaption, TextAnchor.MiddleLeft, c,
                    mine ? FontStyle.BoldAndItalic : FontStyle.Bold);
                var bar = UiFactory.Rect("Bar", row);
                UiFactory.Place(bar, 0.47f, 0.3f, 0.86f, 0.7f);
                double f = hi > floor ? (grid[i].Value - floor) / (hi - floor) : 1;
                Ui.Gauge(bar, f, mine ? Theme.Timing : Theme.Steel);
                ScreenKit.At(row, 0.87f, 0, 1, 1, "<b>" + grid[i].Value.ToString("0") + "</b>", Theme.TextCaption, TextAnchor.MiddleRight, c);
            }
        }

        private static void GridSheet(Team team, List<KeyValuePair<Team, double>> grid)
        {
            Ui.Sheet(Loc.T("ux.dev.grid"), list =>
            {
                UiFactory.Paragraph(list, Theme.Dim(Loc.T("ux.dev.gridInfo")), Theme.TextCaption);
                var w = new[] { 0.8f, 4f, 1.2f };
                for (int i = 0; i < grid.Count; i++)
                    Ui.ListRow(list, w, new[] { (i + 1).ToString(), TeamName(grid[i].Key), grid[i].Value.ToString("0") }, i, grid[i].Key == team);
            }, null, null, 0.56f);
        }

        /// <summary>The "i" of our bike's tile: maker, class and every on-track value as a bar.</summary>
        private static void BikeSheet(Motorcycle bike, BikeClassDefinition bikeClass, string makerName, BikePerformance perf)
        {
            Ui.Sheet(Loc.T("bike.devTitle"), list =>
            {
                Ui.Line(list, Loc.T("ux.dev.maker"), makerName);
                Ui.Line(list, Loc.T("ux.dev.model"), bike.Model ?? "-");
                if (bikeClass != null) Ui.Line(list, Loc.T("ux.dev.class"), bikeClass.Name ?? bikeClass.Id);
                Ui.Section(list, Loc.T("bike.performance"));
                foreach (PerfAttr a in Enum.GetValues(typeof(PerfAttr)))
                    Ui.Bar(list, Loc.Or("perf." + a, a.ToString()), perf[a] / 100.0, perf[a].ToString("0"), a == PerfAttr.Reliability ? Theme.Rel : Theme.Perf);
                UiFactory.Paragraph(list, Theme.Dim(Loc.T("bike.statsNote")), Theme.TextCaption);
                UiFactory.Paragraph(list, Theme.Dim(Loc.T("bike.componentsNote")), Theme.TextCaption);
            }, null, null, 0.7f);
        }

        private void ComponentTile(RectTransform area, WorldState world, Team team, Motorcycle bike, string comp, List<DevelopmentProject> running)
        {
            bool allowed = DevelopmentService.ComponentAllowed(world, Game.Db, team, comp);
            double level = bike.Component(comp);
            DevelopmentProject project = running.Find(p => p.Component == comp);
            string status = null;
            if (!allowed) status = Loc.T("bike.frozen");
            else if (project != null)
                status = Theme.GoodText(Theme.Caps(Loc.T("bike.inDev", (project.DaysWorked / Math.Max(1, project.DurationDays) * 100).ToString("0"))));
            string target = comp;
            Action open = null;
            if (allowed) open = () =>
            {
                ProjectsPage.Focus = target;
                Shell.Show<ProjectsPage>();
            };
            Ui.StatTile(area, Fmt.Component(comp), level.ToString("0"), status, open, allowed ? Theme.Timing : Theme.TextDim);
        }
    }

    /// <summary>
    /// R&amp;D, one component at a time: big tabs across the top (two rows when the bike has more than five
    /// components); for the chosen component its level, one line on what it does; the ambition tabs with the chosen
    /// project (new level, gain, chance of success) and ONE start button carrying that project's gain, cost and time;
    /// on the right the projects running (progress, cancel with confirmation) and the history.
    /// </summary>
    public class ProjectsPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.rnd"); } }

        /// <summary>Component to show the next time the page is built (set by the bike overview).</summary>
        public static string Focus;

        public static string ProjectName(DevelopmentProject p)
        {
            return ProjectName(p.Component, p.TemplateId, p.Name);
        }

        public static string ProjectName(string component, string templateId, string fallback)
        {
            string tier = Tier(templateId);
            if (tier == null || string.IsNullOrEmpty(component)) return fallback;
            return Fmt.Component(component) + ": " + Loc.T("project." + tier);
        }

        private static string Tier(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return null;
            return templateId.EndsWith("_inc") ? "inc" : templateId.EndsWith("_major") ? "major" : templateId.EndsWith("_exp") ? "exp" : null;
        }

        private static string TierName(DevelopmentTemplate t)
        {
            string tier = Tier(t.Id);
            return tier != null ? Loc.T("project." + tier) : t.Name;
        }

        private string _component;
        private int _tier;
        private string _confirmCancel;

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var bike = world.GetBike(team.BikeId);
            if (bike == null) return;
            var bikeClass = Game.Db.GetBikeClass(bike.BikeClassId);

            var running = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status == ProjectStatus.InProgress);
            var done = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status != ProjectStatus.InProgress);
            int capacity = TeamCapabilities.ProjectCapacity(team);

            // ---- tabs: one per component of the bike (a running one in green), two rows when there are more than 5
            var comps = BikeScreen.Components(bike, bikeClass);
            if (comps.Count == 0) return;
            if (Focus != null && comps.Contains(Focus))
            {
                if (_component != Focus) _tier = 0;
                _component = Focus;
            }
            Focus = null;
            if (_component == null || !comps.Contains(_component)) _component = comps[0];
            int perRow = comps.Count <= 5 ? comps.Count : (comps.Count + 1) / 2;
            for (int start = 0; start < comps.Count; start += perRow)
            {
                var labels = new List<string>();
                int first = start;
                for (int i = start; i < Math.Min(comps.Count, start + perRow); i++)
                {
                    bool busy = running.Exists(p => p.Component == comps[i]);
                    labels.Add(busy ? Theme.GoodText(Fmt.Component(comps[i])) : Fmt.Component(comps[i]));
                }
                int sel = comps.IndexOf(_component) - start;
                Ui.Segmented(list, labels, sel >= 0 && sel < labels.Count ? sel : -1, i =>
                {
                    if (_component != comps[first + i]) _tier = 0;
                    _component = comps[first + i];
                    _confirmCancel = null;
                    Shell.Refresh();
                }, 56);
            }

            RectTransform left, right;
            BikeCards.Split(list, 0.63f, out left, out right);
            BuildComponent(left, world, team, bike, running, capacity);
            BuildRunning(right, world, team, running, done, capacity);
        }

        // ------------------------------------------------------------------ selected component

        private List<DevelopmentTemplate> Templates(Motorcycle bike, string comp)
        {
            var list = new List<DevelopmentTemplate>();
            foreach (var t in Game.Db.DevelopmentTemplates.Values)
            {
                if (t.Component != comp) continue;
                if (t.BikeClasses.Count > 0 && !t.BikeClasses.Contains(bike.BikeClassId)) continue;
                list.Add(t);
            }
            list.Sort((a, b) => a.BaseCost.CompareTo(b.BaseCost));
            return list;
        }

        /// <summary>Why a project cannot start now (null when it can).</summary>
        private string Blocker(WorldState world, Team team, DevelopmentTemplate t, List<DevelopmentProject> running, int capacity, bool allowed)
        {
            if (!allowed) return Loc.T("bike.frozen");
            if (running.Exists(p => p.TemplateId == t.Id)) return Loc.T("rnd.inProgress");
            if (running.Count >= capacity) return Loc.T("rnd.slotsFull");
            if (Accounts.Cash(world, team) < DevelopmentService.ProjectCost(world, Game.Db, team, t)) return Loc.T("rnd.noCash");
            return null;
        }

        private void StartProject(WorldState world, Team team, DevelopmentTemplate template)
        {
            string error;
            var p = DevelopmentService.Start(world, Game.Db, team, template, null, out error);
            Shell.Toast(p != null ? Loc.T("rnd.started", ProjectName(template.Component, template.Id, template.Name)) : error, p != null);
            Shell.Refresh();
        }

        private static double Gain(DevelopmentTemplate t, string comp)
        {
            double g;
            return t.Effects.TryGetValue(comp, out g) ? g : 0;
        }

        private void BuildComponent(RectTransform col, WorldState world, Team team, Motorcycle bike, List<DevelopmentProject> running, int capacity)
        {
            string comp = _component;
            bool allowed = DevelopmentService.ComponentAllowed(world, Game.Db, team, comp);
            double level = bike.Component(comp);
            var templates = Templates(bike, comp);

            // Level badge and one sentence on what the component does.
            var head = UiFactory.Row(col, 100);
            BikeCards.Square(head, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 0), 92, level.ToString("0"),
                allowed ? Theme.Timing : Theme.Steel, Loc.T("bike.levelShort"));
            var info = BikeCards.At(head, 0, 0, 1, 1, "<b>" + Theme.Caps(Fmt.Component(comp)) + "</b>\n" + Theme.Dim(Loc.T("compinfo." + comp)),
                Theme.FontSmall, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Normal, 0);
            info.rectTransform.offsetMin = new Vector2(112, 0);
            info.rectTransform.offsetMax = new Vector2(-8, 0);

            if (!allowed)
            {
                Ui.Alert(col, Loc.T("ux.rnd.frozen"));
                return;
            }
            if (templates.Count == 0)
            {
                Ui.Alert(col, Loc.T("ux.rnd.nothing"));
                return;
            }
            if (_tier < 0 || _tier >= templates.Count) _tier = 0;
            var t = templates[_tier];
            double gain = Gain(t, comp);
            double quality = TeamCapabilities.ProjectQuality(world, team, comp, null);
            double success = MathX.Clamp(1 - t.Risk * 0.45 * (1.2 - quality / 100.0), 0, 1);
            double projectCost = DevelopmentService.ProjectCost(world, Game.Db, team, t);
            string blocker = Blocker(world, team, t, running, capacity, true);

            // The chosen project: ambition tabs, the level it would reach and its two key figures.
            var cardRow = UiFactory.Row(col, 230);
            var card = BikeCards.Plate(cardRow, 0, 0, 1, 1, 0.3f, 0);
            for (int i = 0; i < templates.Count; i++)
            {
                int idx = i;
                bool on = i == _tier;
                bool busy = running.Exists(p => p.TemplateId == templates[i].Id);
                string label = Theme.Caps(TierName(templates[i]));
                var b = UiFactory.Button("Tier", card.transform, busy ? Theme.GoodText(label) : label,
                    () => { _tier = idx; Shell.Refresh(); }, on ? Theme.CurrentAccent : Color.Lerp(Theme.Panel, Theme.PanelLight, 0.55f), Theme.TextCaption);
                UiFactory.Place((RectTransform)b.transform, (float)i / templates.Count, 0.7f, (float)(i + 1) / templates.Count, 1, 2);
                var lbl = b.GetComponentInChildren<Text>();
                lbl.supportRichText = true;
                Ui.Fit(lbl, Theme.TextCaption);
                UiFactory.Style(lbl, FontStyle.Bold);
                lbl.color = on ? Theme.OnAccent : Color.Lerp(Theme.TextDim, Color.white, 0.2f);
            }
            double reach = Math.Min(100, level + gain);
            BikeCards.Square(card.transform, new Vector2(0.17f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, 100, reach.ToString("0"),
                Theme.Timing, Loc.T("rnd.newLevel"));
            BikeCards.StatChip(card.transform, 0.32f, 0.22f, 0.65f, 0.68f, Loc.T("rnd.gain"), Fmt.Signed(gain), BikeCards.PerfColor);
            BikeCards.StatChip(card.transform, 0.65f, 0.22f, 0.98f, 0.68f, Loc.T("rnd.success"), (success * 100).ToString("0") + "%", BikeCards.RelColor);
            var notes = new List<string>();
            var trade = new List<string>();
            foreach (var kv in t.Effects)
                if (kv.Key != comp || kv.Value < 0) trade.Add(Fmt.Component(kv.Key) + " " + Fmt.Signed(kv.Value));
            if (trade.Count > 0) notes.Add(Theme.WarnText(Loc.T("rnd.tradeoff", string.Join(", ", trade.ToArray()))));
            notes.Add(Theme.Dim(Loc.T("rnd.risk", (t.Risk * 100).ToString("0"))));
            var rules = Game.Db.GetRules(team.CategoryId, world.Season);
            if (rules != null && rules.Development.SeasonGainCap > 0)
            {
                double used;
                bike.SeasonGains.TryGetValue(comp, out used);
                double left = Math.Max(0, rules.Development.SeasonGainCap - used);
                if (left < gain) notes.Add(Theme.WarnText(Loc.T("rnd.capLeft", left.ToString("0.#", Loc.Culture))));
            }
            BikeCards.At(card.transform, 0.32f, 0.02f, 1, 0.22f, string.Join("   ", notes.ToArray()), Theme.TextMin, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Normal, 8);

            // The one start button, carrying the selected ambition's gain, cost and time (engineering beside it).
            var bar = UiFactory.Row(col, 84);
            BikeCards.Plate(bar, 0, 0, 0.26f, 1, 0.45f, 2);
            Cell(bar, 0, 0.26f, Loc.T("rnd.engineering"), Fmt.Rating(quality));
            var template = t;
            int days = RealDays(world, team, comp, t.DurationDays);
            BikeCards.BigButton(bar, 0.26f, 0, 1, 1, Loc.T("rnd.startWith", Fmt.Signed(gain), MathX.FormatMoney(projectCost), days), blocker,
                BikeCards.StartColor, () => StartProject(world, team, template), blocker == null);
        }

        private static void Cell(RectTransform bar, float x0, float x1, string caption, string value)
        {
            BikeCards.At(bar, x0, 0.56f, x1, 0.94f, Theme.Caps(caption), Theme.TextMin, TextAnchor.MiddleCenter, Color.Lerp(Theme.TextDim, Color.white, 0.2f), FontStyle.Bold, 6);
            BikeCards.At(bar, x0, 0.06f, x1, 0.6f, "<b>" + value + "</b>", Theme.FontNormal, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Italic, 6);
        }

        /// <summary>Calendar days a project of <paramref name="workDays"/> takes with the team's factory and staff.</summary>
        private static int RealDays(WorldState world, Team team, string comp, double workDays)
        {
            double speed = TeamCapabilities.DevelopmentSpeed(world, team, comp);
            return (int)Math.Ceiling(Math.Max(0, workDays) / Math.Max(0.1, speed));
        }

        // ------------------------------------------------------------------ running and finished

        private void BuildRunning(RectTransform col, WorldState world, Team team, List<DevelopmentProject> running, List<DevelopmentProject> done, int capacity)
        {
            Ui.Section(col, Loc.T("rnd.inProgress"), running.Count + "/" + capacity,
                ScreenKit.Info(Loc.T("rnd.inProgress"), Loc.T("ux.rnd.successInfo"), Loc.T("rnd.availableInfo"), Loc.T("rnd.cancelInfo")));
            foreach (var p in running)
            {
                var project = p;
                var row = UiFactory.Row(col, 154);
                BikeCards.Plate(row, 0, 0, 1, 1, 0.3f, 0);
                double f = p.DaysWorked / Math.Max(1, p.DurationDays);
                BikeCards.At(row, 0, 0.72f, 1, 0.98f, "<b>" + Theme.Caps(ProjectName(p)) + "</b>", Theme.FontSmall, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Italic, 14);
                BikeCards.Gauge(row, 0.04f, 0.56f, 0.78f, 0.68f, f, BikeCards.PerfColor);
                BikeCards.At(row, 0.78f, 0.5f, 1, 0.74f, Theme.GoodText("<b>" + (f * 100).ToString("0") + "%</b>"), Theme.FontNormal, TextAnchor.MiddleRight, Theme.Text, FontStyle.Italic, 14);
                int left = (int)Math.Ceiling(Math.Max(0, p.DurationDays - p.DaysWorked) / Math.Max(0.1, DevelopmentService.Speed(world, team, p)));
                BikeCards.At(row, 0, 0.04f, 0.6f, 0.5f, Loc.T("rnd.remaining", left) + "\n" + Theme.Dim(Fmt.Effects(p.ExpectedEffects)),
                    Theme.TextMin, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Normal, 14);
                bool armed = _confirmCancel == p.Id;
                BikeCards.BigButton(row, 0.6f, 0, 1, 0.5f, armed ? Loc.T("rnd.cancelConfirm") : Loc.T("rnd.cancel"), null,
                    armed ? (Color)Theme.Bad : Color.Lerp(Theme.Bad, Theme.PanelLight, 0.6f), () =>
                    {
                        if (_confirmCancel != project.Id)
                        {
                            _confirmCancel = project.Id;
                            Shell.Refresh();
                            return;
                        }
                        _confirmCancel = null;
                        // No refund: the money is spent; cancelling only frees the slot in the factory.
                        project.Status = ProjectStatus.Cancelled;
                        Shell.Toast(Loc.T("rnd.cancelled", ProjectName(project)), false);
                        Shell.Refresh();
                    }, true, 6);
            }
            int free = capacity - running.Count;
            if (free > 0)
            {
                var row = UiFactory.Row(col, 64);
                BikeCards.Plate(row, 0, 0, 1, 1, 0.18f, 0);
                BikeCards.At(row, 0, 0, 1, 1, Theme.GoodText("<b>" + Theme.Caps(Loc.T("bike.rndFree", free)) + "</b>"), Theme.FontSmall, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Italic);
            }
            else Ui.Alert(col, Loc.T("bike.rndFull"));

            Ui.Expander(col, "rnd.history", Loc.T("rnd.history", done.Count), inner =>
            {
                var hist = Ui.Card(inner, null);
                var w = new[] { 5f, 5f, 2.4f };
                UiFactory.TableRow(hist, 34, w, new[] { Loc.T("rnd.col.project"), Loc.T("rnd.col.effect"), Loc.T("rnd.col.result") }, true);
                for (int i = done.Count - 1; i >= 0; i--)
                {
                    var p = done[i];
                    string status = p.Status == ProjectStatus.Completed ? Theme.GoodText(Loc.T("rnd.status.Completed")) : Theme.BadText(Loc.T("rnd.status." + p.Status));
                    UiFactory.TableRow(hist, 40, w, new[] { ProjectName(p), Fmt.Effects(p.ActualEffects), status });
                }
                if (done.Count == 0) UiFactory.Paragraph(hist, Theme.Dim(Loc.T("rnd.noHistory")), Theme.TextCaption);
            }, Shell.Refresh);
        }
    }
    /// <summary>
    /// "Instalaciones" tab (UX spec §3.12): a 2×5 grid of tiles, one screen. Each tile keeps its level, pips, a
    /// one-line effect and the upgrade price as a quiet pill; a tap opens a sheet with everything it does, the upkeep
    /// the next level adds and the one "upgrade" button.
    /// </summary>
    public class FacilitiesPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.facilities"); } }

        private const int PerRow = 5;
        private const int MaxLevel = 5;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var types = (FacilityType[])Enum.GetValues(typeof(FacilityType));
            double cash = Accounts.Cash(world, team);
            int nRows = (types.Length + PerRow - 1) / PerRow;
            var shares = new float[Math.Max(1, nRows)];
            for (int i = 0; i < shares.Length; i++) shares[i] = 1;
            var rows = Ui.Rows(content, shares);
            for (int r = 0; r < nRows; r++)
            {
                var cols = ScreenKit.Cols(rows[r], PerRow);
                for (int c = 0; c < PerRow; c++)
                {
                    int idx = r * PerRow + c;
                    if (idx >= types.Length) break;
                    FacilityTile(cols[c], world, team, types[idx], cash);
                }
            }
        }

        private void FacilityTile(RectTransform area, WorldState world, Team team, FacilityType type, double cash)
        {
            int lvl = team.Facility(type);
            var target = type;
            var body = Ui.Tile(area, Loc.T("facility." + type), () => FacilitySheet(world, team, target));
            ScreenKit.At(body, 0, 0.64f, 1, 1, "<size=" + Theme.TextCaption + ">" + Theme.Caps(Loc.T("bike.levelShort")) + " </size>" + lvl,
                Theme.TextKey, TextAnchor.MiddleCenter, Theme.Timing, FontStyle.BoldAndItalic);
            var pips = UiFactory.Rect("Pips", body);
            UiFactory.Place(pips, 0.1f, 0.5f, 0.9f, 0.6f);
            Ui.Pips(pips, lvl, MaxLevel);
            ScreenKit.At(body, 0, 0.2f, 1, 0.48f, Loc.T("facilitytip." + type), Theme.TextCaption, TextAnchor.MiddleCenter,
                Color.Lerp(Theme.TextDim, Color.white, 0.25f));
            var pillHost = UiFactory.Rect("Price", body);
            UiFactory.Place(pillHost, 0, 0, 1, 0.18f);
            Image pill;
            if (lvl >= MaxLevel) pill = ScreenKit.Tag(pillHost, Loc.T("facility.max"), Theme.Timing);
            else
            {
                double cost = FinanceService.FacilityUpgradeCost(world, Game.Db, team, type);
                pill = cash >= cost ? ScreenKit.Tag(pillHost, MathX.FormatMoney(cost), Theme.Good)
                    : Ui.Pill(pillHost, MathX.FormatMoney(cost), Theme.Alpha(Theme.Steel, 0.25f), Theme.TextDim);
            }
            pill.rectTransform.anchorMin = pill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            pill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            pill.rectTransform.anchoredPosition = Vector2.zero;
        }

        /// <summary>Everything about one facility and the upgrade button (disabled with the reason when it cannot be paid).</summary>
        private void FacilitySheet(WorldState world, Team team, FacilityType type)
        {
            int lvl = team.Facility(type);
            string name = Loc.T("facility." + type);
            UiAct? primary = null;
            double cost = 0;
            if (lvl < MaxLevel)
            {
                cost = FinanceService.FacilityUpgradeCost(world, Game.Db, team, type);
                bool affordable = Accounts.Cash(world, team) >= cost;
                var target = type;
                primary = affordable
                    ? UiFactory.Primary(Loc.T("facility.upgradeButton") + " " + MathX.FormatMoney(cost), () =>
                    {
                        bool ok = FinanceService.UpgradeFacility(world, Game.Db, team, target);
                        Shell.Toast(ok ? Loc.T("facility.upgraded", Loc.T("facility." + target)) : Loc.T("facility.noBudget"), ok);
                        Shell.Refresh();
                    })
                    : UiFactory.Primary(Loc.T("rnd.noCash") + " · " + MathX.FormatMoney(cost), null);
            }
            var rules = Game.Db.GetRules(team.CategoryId, world.Season);
            double scale = rules != null ? rules.Economy.FacilityCostScale : 1;
            double upkeep = FinanceService.FacilityUpkeepUnit * ((lvl + 1) * (lvl + 1) - lvl * lvl) * scale * TeamCapabilities.CostWeight(type);
            Ui.Sheet(name, list =>
            {
                Ui.Pips((RectTransform)list, lvl, MaxLevel);
                Ui.Line(list, Loc.T("bike.level"), lvl + " / " + MaxLevel, true);
                if (lvl < MaxLevel)
                {
                    Ui.Line(list, Loc.T("facility.upgrade"), MathX.FormatMoney(cost));
                    Ui.Line(list, Loc.T("ux.fac.upkeep"), "+" + MathX.FormatMoney(upkeep));
                }
                else Ui.Line(list, Loc.T("facility.upgrade"), Theme.TimingText(Theme.Caps(Loc.T("facility.max"))));
                UiFactory.Paragraph(list, "<b>" + Loc.T("facilitytip." + type) + "</b>", Theme.TextBody);
                UiFactory.Paragraph(list, Theme.Dim(Loc.T("facility.effect") + ": ") + Loc.T("facilityinfo." + type), Theme.TextBody);
                UiFactory.Paragraph(list, Theme.Dim(Loc.T("facility.note")), Theme.TextCaption);
            }, primary);
        }
    }
}
