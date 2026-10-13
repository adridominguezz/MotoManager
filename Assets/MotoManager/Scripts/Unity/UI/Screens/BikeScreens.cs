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
            UiFactory.DropShadow(v, 0.4f, 2);
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
    /// "Moto" tab of the development section (UX spec §3.10), one screen without scrolling: a wide tile with each
    /// rider's bike (surname, performance in green, reliability in purple) around the bike pictogram, the R&amp;D and
    /// facilities tiles on the right, and one tile per component with its level that opens its development.
    /// Maker, class and the on-track performance values are behind the "i" of the bike tile.
    /// </summary>
    public class BikeScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.bike"); } }

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

            var rows = Ui.Rows(content, twoRows ? 0.45f : 0.52f, twoRows ? 0.55f : 0.48f);
            var top = Ui.Columns12(rows[0], 9, 3);

            // ---- the bikes of both riders
            string makerName = maker != null ? maker.Name : "";
            var body = Ui.Tile(top[0], Loc.T("bike.devTitle"), null, bike.Model, 0, null, () => BikeSheet(bike, bikeClass, makerName, perf));
            double perfScore = PerformanceScore(perf);
            for (int i = 0; i < 2; i++)
            {
                var r = i < team.RiderIds.Count ? world.GetRider(team.RiderIds[i]) : null;
                RiderHalf(body, i == 0 ? 0 : 0.62f, i == 0 ? 0.38f : 1, i, r, world, team, perf, perfScore);
            }
            BikeCentre(body, makerName);

            // ---- R&D and facilities
            var side = Ui.Rows(top[1], 1, 1);
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

        private void RiderHalf(RectTransform body, float x0, float x1, int index, Rider r, WorldState world, Team team, BikePerformance perf, double perfScore)
        {
            var half = UiFactory.Rect("Rider" + (index + 1), body);
            UiFactory.Place(half, x0, 0, x1, 1);
            if (r == null)
            {
                var go = UiFactory.Rect("FreeSeat", half);
                UiFactory.Place(go, 0.05f, 0.3f, 0.95f, 0.7f);
                Ui.SecondaryAccent(go, Loc.T("hub.freeSeat"), () => Shell.Show<MarketScreen>());
                return;
            }
            // Surname and "MOTO 01" only: the rider's rating and race number live on the riders' pages.
            ScreenKit.At(half, 0, 0.76f, 1, 1, "<b>" + Theme.Caps(r.LastName ?? r.FullName) + "</b>", Theme.TextTitle,
                TextAnchor.MiddleLeft, Theme.Text, FontStyle.Italic);
            ScreenKit.At(half, 0, 0.6f, 1, 0.76f, Theme.Caps(Loc.T("bike.machine", (index + 1).ToString("00"))), Theme.TextCaption,
                TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            StatLine(half, 0.3f, 0.56f, Loc.T("bike.perfShort"), perfScore.ToString("0"), perfScore / 100.0, Theme.Perf);
            double rel = Reliability(world, team, perf, r);
            StatLine(half, 0f, 0.26f, Loc.T("bike.relShort"), rel.ToString("0") + "%", rel / 100.0, Theme.Rel);
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

        private static void BikeCentre(RectTransform body, string makerName)
        {
            var mid = UiFactory.Rect("Bike", body);
            UiFactory.Place(mid, 0.4f, 0, 0.6f, 1);
            var icon = UiIcons.Icon(mid, UiIcons.Bike, Color.Lerp(Theme.TextDim, Color.white, 0.35f));
            icon.preserveAspect = true;
            UiFactory.Place(icon.rectTransform, 0.06f, 0.3f, 0.94f, 0.96f);
            ScreenKit.At(mid, 0, 0.04f, 1, 0.26f, "<b>" + Theme.Caps(makerName) + "</b>", Theme.TextCaption, TextAnchor.MiddleCenter,
                Theme.TextDim, FontStyle.Italic);
        }

        /// <summary>The "i" of the bike tile: maker, class and every on-track value as a bar.</summary>
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
    /// "I+D" tab (UX spec §3.11): the components on the left (level, tap to choose), the chosen component in the
    /// middle (ambition, the level it would reach, gain, chance of success, cost and time) with ONE start button that
    /// carries the selected ambition, and the projects running on the right (tap one for its effects and to cancel it).
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

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var bike = world.GetBike(team.BikeId);
            if (bike == null) return;
            var bikeClass = Game.Db.GetBikeClass(bike.BikeClassId);

            var running = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status == ProjectStatus.InProgress);
            var done = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status != ProjectStatus.InProgress);
            int capacity = TeamCapabilities.ProjectCapacity(team);

            var comps = BikeScreen.Components(bike, bikeClass);
            if (comps.Count == 0) return;
            if (Focus != null && comps.Contains(Focus))
            {
                if (_component != Focus) _tier = 0;
                _component = Focus;
            }
            Focus = null;
            if (_component == null || !comps.Contains(_component)) _component = comps[0];

            var cols = Ui.Columns12(content, 3, 6, 3);
            ComponentList(cols[0], world, team, bike, comps, running);
            BuildComponent(cols[1], world, team, bike, running, capacity);
            BuildRunning(cols[2], world, team, running, done, capacity);
        }

        // ------------------------------------------------------------------ component list

        private void ComponentList(RectTransform col, WorldState world, Team team, Motorcycle bike, List<string> comps, List<DevelopmentProject> running)
        {
            // One row per component (up to 8 fit the column without scrolling): level block, name, state.
            int slots = Math.Max(comps.Count, 8);
            var w = new[] { 1.1f, 2.6f };
            for (int i = 0; i < comps.Count; i++)
            {
                string c = comps[i];
                bool allowed = DevelopmentService.ComponentAllowed(world, Game.Db, team, c);
                var project = running.Find(p => p.Component == c);
                string name = "<b>" + Theme.Caps(Fmt.Component(c)) + "</b>";
                if (!allowed) name = Theme.Dim(name) + "\n<size=" + Theme.TextMin + ">" + Theme.Dim(Theme.Caps(Loc.T("bike.frozen"))) + "</size>";
                else if (project != null)
                    name += "\n<size=" + Theme.TextMin + ">" + Theme.GoodText(Theme.Caps(Loc.T("bike.inDev", (project.DaysWorked / Math.Max(1, project.DurationDays) * 100).ToString("0")))) + "</size>";
                string target = c;
                // index + 4 keeps the level blocks graphite (ListRow lights the first three like a podium).
                var row = Ui.ListRow(col, w, new[] { bike.Component(c).ToString("0"), name }, i + 4, c == _component, () =>
                {
                    if (_component != target) _tier = 0;
                    _component = target;
                    Shell.Refresh();
                });
                float y1 = 1 - (float)i / slots, y0 = 1 - (float)(i + 1) / slots;
                UiFactory.Place(row, 0, y0, 1, y1, 3);
            }
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
        private string Blocker(WorldState world, Team team, DevelopmentTemplate t, List<DevelopmentProject> running, int capacity)
        {
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
            if (_tier < 0 || _tier >= templates.Count) _tier = 0;
            var t = templates.Count > 0 ? templates[_tier] : null;
            double quality = TeamCapabilities.ProjectQuality(world, team, comp, null);

            // Season cap left (explained behind the "i", flagged as a pill when it limits the gain).
            double capLeft = -1;
            var rules = Game.Db.GetRules(team.CategoryId, world.Season);
            if (rules != null && rules.Development.SeasonGainCap > 0)
            {
                double used;
                bike.SeasonGains.TryGetValue(comp, out used);
                capLeft = Math.Max(0, rules.Development.SeasonGainCap - used);
            }
            var info = new List<string> { Loc.T("compinfo." + comp) };
            if (t != null) info.Add(Loc.T("rnd.risk", (t.Risk * 100).ToString("0")));
            if (capLeft >= 0) info.Add(Loc.T("rnd.capLeft", capLeft.ToString("0.#", Loc.Culture)));
            info.Add(Loc.T("ux.rnd.successInfo"));
            info.Add(Loc.T("rnd.availableInfo"));

            var body = Ui.Tile(col, Fmt.Component(comp), null, Theme.Caps(Loc.T("bike.levelShort")) + " " + level.ToString("0"), 0, null,
                ScreenKit.Info(Fmt.Component(comp), info.ToArray()));
            if (!allowed || t == null)
            {
                var empty = ScreenKit.List(ScreenKit.Between(body, 60, 60));
                empty.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                Ui.Empty(empty, null, Loc.T(!allowed ? "ux.rnd.frozen" : "ux.rnd.nothing"));
                return;
            }

            // Ambition: one segmented row (a running ambition is shown in green).
            var labels = new List<string>();
            foreach (var x in templates)
            {
                string n = TierName(x);
                labels.Add(running.Exists(p => p.TemplateId == x.Id) ? Theme.GoodText(n) : n);
            }
            Ui.Segmented(ScreenKit.List(ScreenKit.Top(body, 0, 56)), labels, _tier, i => { _tier = i; Shell.Refresh(); });
            ScreenKit.At(ScreenKit.Top(body, 64, 34), 0, 0, 1, 1, Theme.Dim(Loc.T("compinfo." + comp)), Theme.TextCaption, TextAnchor.MiddleLeft);

            double gain = Gain(t, comp);
            double success = MathX.Clamp(1 - t.Risk * 0.45 * (1.2 - quality / 100.0), 0, 1);
            double projectCost = DevelopmentService.ProjectCost(world, Game.Db, team, t);
            string blocker = Blocker(world, team, t, running, capacity);
            int days = RealDays(world, team, comp, t.DurationDays);
            double reach = Math.Min(100, level + gain);

            // Trade-offs and the season cap, only when they apply.
            var warnings = new List<string>();
            foreach (var kv in t.Effects)
                if (kv.Key != comp || kv.Value < 0) warnings.Add(Fmt.Component(kv.Key) + " " + Fmt.Signed(kv.Value));
            if (capLeft >= 0 && capLeft < gain) warnings.Add(Loc.T("rnd.capLeft", capLeft.ToString("0.#", Loc.Culture)));
            float bottomBar = Ui.Grid.Cta + 4;
            float pills = warnings.Count > 0 ? 48 : 0;
            if (pills > 0)
            {
                var row = ScreenKit.PillRow(ScreenKit.List(ScreenKit.Bottom(body, bottomBar + 8, 36)));
                foreach (var wtext in warnings) ScreenKit.Tag(row, wtext, Theme.Warning);
            }

            // The three figures of the chosen ambition.
            var figs = ScreenKit.Between(body, 106, bottomBar + pills + 8);
            ScreenKit.Figure(figs, 0, 0.36f, Loc.T("rnd.newLevel"), reach.ToString("0"), Theme.Timing,
                Loc.T("ux.rnd.from", level.ToString("0")), Theme.TextDisplay);
            ScreenKit.Figure(figs, 0.36f, 0.68f, Loc.T("rnd.gain"), Theme.Signed(gain));
            ScreenKit.Figure(figs, 0.68f, 1f, Loc.T("rnd.success"), (success * 100).ToString("0") + "%");

            // Cost, engineering and time beside the ONE start button, which names the selected ambition.
            var bar = ScreenKit.Bottom(body, 0, Ui.Grid.Cta);
            var facts = UiFactory.Rect("Facts", bar);
            UiFactory.Place(facts, 0, 0, 0.52f, 1);
            ScreenKit.Fact(facts, 0, 0.34f, Loc.T("rnd.cost"), MathX.FormatMoney(projectCost));
            ScreenKit.Fact(facts, 0.34f, 0.66f, Loc.T("rnd.engineering"), Fmt.Rating(quality));
            ScreenKit.Fact(facts, 0.66f, 1, Loc.T("rnd.time"), Loc.T("rnd.days", days));
            var cta = UiFactory.Rect("Start", bar);
            UiFactory.Place(cta, 0.54f, 0, 1, 1);
            var template = t;
            Ui.PrimaryCTA(cta, Loc.T("rnd.start"), () => StartProject(world, team, template),
                blocker ?? Theme.Caps(TierName(t)) + " · " + Fmt.Signed(gain), blocker == null);
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
            var body = Ui.Tile(col, Loc.T("rnd.inProgress"), null, running.Count + "/" + capacity, 0, null,
                ScreenKit.Info(Loc.T("rnd.inProgress"), Loc.T("rnd.cancelInfo"), Loc.T("rnd.full")));
            var list = ScreenKit.List(ScreenKit.Between(body, 0, Ui.Grid.Hit + 8), 8);
            foreach (var p in running) RunningRow(list, world, team, p);
            int free = capacity - running.Count;
            var slot = UiFactory.Row(list, 56);
            var slotBg = slot.gameObject.AddComponent<Image>();
            slotBg.raycastTarget = false;
            slotBg.color = new Color(1, 1, 1, 0.025f);
            slot.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.08f);
            ScreenKit.At(slot, 0, 0, 1, 1, free > 0 ? Theme.Caps(Loc.T("bike.rndFree", free)) : Theme.WarnText(Theme.Caps(Loc.T("bike.rndFull"))),
                Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);

            var ghost = ScreenKit.Bottom(body, 0, Ui.Grid.Hit);
            Ui.Ghost(ghost, Loc.T("ux.rnd.history", done.Count), () => HistorySheet(done));
        }

        private void RunningRow(RectTransform list, WorldState world, Team team, DevelopmentProject p)
        {
            var project = p;
            var row = UiFactory.Row(list, 104);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = new Color(1, 1, 1, 0.045f);
            double f = p.DaysWorked / Math.Max(1, p.DurationDays);
            int left = (int)Math.Ceiling(Math.Max(0, p.DurationDays - p.DaysWorked) / Math.Max(0.1, DevelopmentService.Speed(world, team, p)));
            ScreenKit.Tap(bg, () => ProjectSheet(project, f, left));
            var inner = UiFactory.Rect("Inner", row);
            UiFactory.Stretch(inner);
            inner.offsetMin = new Vector2(12, 8);
            inner.offsetMax = new Vector2(-40, -6);
            ScreenKit.At(inner, 0, 0.62f, 1, 1, "<b>" + Theme.Caps(ProjectName(p)) + "</b>", Theme.TextCaption, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Italic);
            var gauge = UiFactory.Rect("Gauge", inner);
            UiFactory.Place(gauge, 0, 0.38f, 0.74f, 0.56f);
            Ui.Gauge(gauge, f, Theme.Perf);
            ScreenKit.At(inner, 0.74f, 0.3f, 1, 0.64f, "<b>" + (f * 100).ToString("0") + "%</b>", Theme.TextBody, TextAnchor.MiddleRight);
            ScreenKit.At(inner, 0, 0, 1, 0.34f, Theme.Caps(Loc.T("rnd.remaining", left)), Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            var ch = UiIcons.Chevron(row, Theme.TextDim);
            ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(1, 0.5f);
            ch.rectTransform.sizeDelta = new Vector2(22, 22);
            ch.rectTransform.anchoredPosition = new Vector2(-20, 0);
        }

        /// <summary>A running project: progress, time left, expected effects and the two-tap cancel.</summary>
        private void ProjectSheet(DevelopmentProject project, double f, int left)
        {
            Ui.Sheet(ProjectName(project), list =>
            {
                Ui.Line(list, Loc.T("ux.rnd.progress"), (f * 100).ToString("0") + "%");
                Ui.Line(list, Loc.T("rnd.time"), Loc.T("rnd.remaining", left));
                Ui.Line(list, Loc.T("ux.rnd.expected"), Fmt.Effects(project.ExpectedEffects));
                UiFactory.Paragraph(list, Theme.Dim(Loc.T("rnd.cancelInfo")), Theme.TextCaption);
                Ui.Danger((RectTransform)list, Loc.T("rnd.cancel"), Loc.T("rnd.cancelConfirm"), "rnd.cancel." + project.Id, () =>
                {
                    // No refund: the money is spent; cancelling only frees the slot in the factory.
                    project.Status = ProjectStatus.Cancelled;
                    Ui.CloseSheet();
                    Shell.Toast(Loc.T("rnd.cancelled", ProjectName(project)), false);
                    Shell.Refresh();
                }, null);
            });
        }

        private static void HistorySheet(List<DevelopmentProject> done)
        {
            Ui.Sheet(Loc.T("ux.rnd.history", done.Count), list =>
            {
                if (done.Count == 0)
                {
                    Ui.Empty(list, null, Loc.T("rnd.noHistory"));
                    return;
                }
                var w = new[] { 4.6f, 4.4f, 2.2f };
                ScreenKit.Captions(list, w, new[] { Loc.T("rnd.col.project"), Loc.T("rnd.col.effect"), Loc.T("rnd.col.result") });
                int n = 0;
                for (int i = done.Count - 1; i >= 0; i--)
                {
                    var p = done[i];
                    string status = p.Status == ProjectStatus.Completed ? Theme.GoodText(Loc.T("rnd.status.Completed")) : Theme.BadText(Loc.T("rnd.status." + p.Status));
                    ScreenKit.DataRow(list, w, new[] { ProjectName(p), Fmt.Effects(p.ActualEffects), Theme.Caps(status) }, n++);
                }
            }, null, null, 0.78f);
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
