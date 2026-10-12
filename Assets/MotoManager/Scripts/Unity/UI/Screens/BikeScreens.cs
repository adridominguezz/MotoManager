using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Race;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// "Bike development" overview, laid out like the development hub of a mobile motorsport manager: a wide card
    /// with both riders' bikes (number, rating, surname, performance in green and reliability in purple) around a
    /// big bike pictogram, the R&amp;D and facilities tiles on the right, and one tile per bike component (its
    /// level big in timing yellow) that opens its development. The full on-track values stay one tap away.
    /// </summary>
    public class BikeScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.bike"); } }

        private const float TopHeight = 252;
        private const float ComponentHeight = 128;

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var bike = world.GetBike(team.BikeId);
            if (bike == null) return;
            var bikeClass = Game.Db.GetBikeClass(bike.BikeClassId);
            var maker = world.GetManufacturer(bike.ManufacturerId);
            var perf = BikePerformance.Compute(bike, bikeClass);
            var running = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status == ProjectStatus.InProgress);

            // ---- top: the bikes of both riders, R&D and facilities
            var top = UiFactory.Row(list, TopHeight);
            string right = (maker != null ? maker.Name + " · " : "") + (bikeClass != null ? bikeClass.Name : "");
            var body = BikeCards.Tile(top, 0, 0, 0.75f, 1, Loc.T("bike.devTitle"), null, null, null, right, true);
            double perfScore = PerformanceScore(perf);
            for (int i = 0; i < 2; i++)
            {
                var r = i < team.RiderIds.Count ? world.GetRider(team.RiderIds[i]) : null;
                RiderHalf(body, i == 0 ? 0 : 0.6f, i == 0 ? 0.4f : 1, i, r, world, team, perf, perfScore);
            }
            BikeCentre(body, bike);
            RndTile(top, team, running);
            FacilitiesTile(top, team);

            // ---- components: one tile each, tap to develop
            var comps = Components(bike, bikeClass);
            int perRow = comps.Count <= 4 ? Math.Max(1, comps.Count) : (comps.Count + 1) / 2;
            RectTransform row = null;
            for (int i = 0; i < comps.Count; i++)
            {
                if (i % perRow == 0) row = UiFactory.Row(list, ComponentHeight);
                int col = i % perRow;
                ComponentTile(row, (float)col / perRow, (float)(col + 1) / perRow, world, team, bike, comps[i], running);
            }

            // ---- every on-track value, on demand
            var attrs = (PerfAttr[])Enum.GetValues(typeof(PerfAttr));
            Ui.Expander(list, "bike.perf", Loc.T("bike.performance"), inner =>
            {
                RectTransform l, rr;
                Ui.Columns(inner, out l, out rr);
                var lc = Ui.Card(l, null);
                var rc = Ui.Card(rr, null);
                int half = (attrs.Length + 1) / 2;
                for (int i = 0; i < attrs.Length; i++)
                    Ui.Bar(i < half ? lc : rc, Loc.T("perf." + attrs[i]), perf[attrs[i]] / 100.0, perf[attrs[i]].ToString("0"), Ui.RatingColor(perf[attrs[i]]), 34);
                Ui.Note(lc, Loc.T("bike.performanceInfo"));
                Ui.Note(rc, Loc.T("bike.componentsNote"));
                Ui.Note(rc, Loc.T("bike.statsNote"));
            }, Shell.Refresh);
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
                BikeCards.At(half, 0, 0.3f, 1, 0.7f, Theme.WarnText("<b>" + Theme.Caps(Loc.T("hub.freeSeat")) + "</b>") + "  " + Theme.Dim("›"),
                    Theme.FontNormal, TextAnchor.MiddleCenter, Theme.Text);
                var go = UiFactory.Button("FreeSeat", half, "", () => Shell.Show<MarketScreen>(), new Color(1, 1, 1, 0.001f));
                UiFactory.Stretch((RectTransform)go.transform);
                return;
            }
            // Identity: number badge, rating square, surname and the bike number.
            var badge = BikeCards.NumberBadge(half, 0, 0.6f, 0, 0.94f, r.Number).rectTransform;
            badge.offsetMin = new Vector2(14, 0);
            badge.offsetMax = new Vector2(88, 0);
            double overall = r.Attributes.Overall;
            BikeCards.Square(half, new Vector2(0, 0.77f), new Vector2(0, 0.5f), new Vector2(96, 0), 62, overall.ToString("0"), Ui.RatingColor(overall));
            var name = BikeCards.At(half, 0, 0.77f, 1, 0.98f, "<b>" + Theme.Caps(r.LastName ?? r.FullName) + "</b>", Theme.FontNormal + 2,
                TextAnchor.MiddleLeft, Theme.Text, FontStyle.Italic, 0);
            name.rectTransform.offsetMin = new Vector2(170, 0);
            name.rectTransform.offsetMax = new Vector2(-10, 0);
            var moto = BikeCards.At(half, 0, 0.56f, 1, 0.78f, Theme.Caps(Loc.T("bike.machine", (index + 1).ToString("00"))), Theme.FontLarge - 2,
                TextAnchor.MiddleLeft, Color.Lerp(Theme.TextDim, Color.white, 0.35f), FontStyle.BoldAndItalic, 0);
            moto.rectTransform.offsetMin = new Vector2(170, 0);
            moto.rectTransform.offsetMax = new Vector2(-10, 0);

            // The two key figures, big, with a thick bar each.
            BikeCards.StatRow(half, 0.29f, 0.54f, Loc.T("bike.perfShort"), perfScore.ToString("0"), perfScore / 100.0, BikeCards.PerfColor);
            double rel = Reliability(world, team, perf, r);
            BikeCards.StatRow(half, 0.03f, 0.28f, Loc.T("bike.relShort"), rel.ToString("0") + "%", rel / 100.0, BikeCards.RelColor);
        }

        private static void BikeCentre(RectTransform body, Motorcycle bike)
        {
            var mid = UiFactory.Rect("Bike", body);
            UiFactory.Place(mid, 0.4f, 0, 0.6f, 1);
            var glow = UiFactory.Decor("Glow", mid, Theme.Alpha(Theme.Accent, 0.16f));
            UiFactory.Round(glow, 6f);
            UiFactory.Place(glow.rectTransform, 0.04f, 0.3f, 0.96f, 0.94f);
            var icon = UiIcons.Icon(mid, UiIcons.Bike, Color.Lerp(Theme.Accent, Color.white, 0.35f));
            UiFactory.Place(icon.rectTransform, 0.02f, 0.32f, 0.98f, 0.94f);
            var sep = UiFactory.Decor("Sep", mid, new Color(1, 1, 1, 0.07f));
            sep.rectTransform.anchorMin = new Vector2(0, 0.06f);
            sep.rectTransform.anchorMax = new Vector2(0, 0.94f);
            sep.rectTransform.offsetMax = new Vector2(1, 0);
            var sep2 = UiFactory.Decor("Sep", mid, new Color(1, 1, 1, 0.07f));
            sep2.rectTransform.anchorMin = new Vector2(1, 0.06f);
            sep2.rectTransform.anchorMax = new Vector2(1, 0.94f);
            sep2.rectTransform.offsetMin = new Vector2(-1, 0);
            BikeCards.At(mid, 0, 0.04f, 1, 0.3f, "<b>" + Theme.Caps(bike.Model ?? "") + "</b>\n" + Theme.Dim(bike.Year.ToString()), Theme.FontSmall,
                TextAnchor.MiddleCenter, Theme.Text, FontStyle.Italic, 6);
        }

        private void RndTile(RectTransform top, Team team, List<DevelopmentProject> running)
        {
            int capacity = TeamCapabilities.ProjectCapacity(team);
            string badge = running.Count > 0 ? Loc.T("bike.rndBadge", running.Count) : null;
            var body = BikeCards.Tile(top, 0.75f, 0.5f, 1, 1, Loc.T("bike.rndTile"), () => Shell.Show<ProjectsPage>(), badge, Theme.Timing);
            BikeCards.At(body, 0, 0.36f, 1, 1, Theme.TimingText(running.Count + "<size=" + Theme.FontNormal + ">/" + capacity + "</size>"),
                Theme.FontTitle - 8, TextAnchor.MiddleCenter, Theme.Text, FontStyle.BoldAndItalic);
            int free = capacity - running.Count;
            BikeCards.At(body, 0, 0.02f, 1, 0.38f, free > 0 ? Theme.GoodText(Theme.Caps(Loc.T("bike.rndFree", free))) : Theme.WarnText(Theme.Caps(Loc.T("bike.rndFull"))),
                Theme.FontSmall - 2, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
        }

        private void FacilitiesTile(RectTransform top, Team team)
        {
            var types = (FacilityType[])Enum.GetValues(typeof(FacilityType));
            double sum = 0;
            foreach (var f in types) sum += team.Facility(f);
            double avg = types.Length > 0 ? sum / types.Length : 0;
            var body = BikeCards.Tile(top, 0.75f, 0, 1, 0.5f, Loc.T("tab.facilities"), () => Shell.Show<FacilitiesPage>());
            BikeCards.At(body, 0, 0.36f, 1, 1, Theme.TimingText("<size=" + Theme.FontSmall + ">" + Theme.Caps(Loc.T("bike.levelShort")) + " </size>" + avg.ToString("0.0", Loc.Culture)),
                Theme.FontTitle - 8, TextAnchor.MiddleCenter, Theme.Text, FontStyle.BoldAndItalic);
            BikeCards.At(body, 0, 0.02f, 1, 0.38f, Theme.Caps(Loc.T("bike.facAvg", types.Length)), Theme.FontSmall - 2, TextAnchor.MiddleCenter,
                Color.Lerp(Theme.TextDim, Color.white, 0.25f), FontStyle.Bold);
        }

        private void ComponentTile(RectTransform row, float x0, float x1, WorldState world, Team team, Motorcycle bike, string comp, List<DevelopmentProject> running)
        {
            bool allowed = DevelopmentService.ComponentAllowed(world, Game.Db, team, comp);
            double level = bike.Component(comp);
            double gained;
            bike.SeasonGains.TryGetValue(comp, out gained);
            DevelopmentProject project = null;
            foreach (var p in running) if (p.Component == comp) { project = p; break; }
            string target = comp;
            // The status sits in the body (a header badge would squeeze the name on a narrow tile).
            var body = BikeCards.Tile(row, x0, 0, x1, 1, Fmt.Component(comp), () =>
            {
                ProjectsPage.Focus = target;
                Shell.Show<ProjectsPage>();
            }, null, null, null, false, !allowed);
            string caption = "<size=" + (Theme.FontSmall - 2) + ">" + Theme.Caps(Loc.T("bike.level")) + "  </size>";
            BikeCards.At(body, 0, 0.36f, 1, 1, caption + level.ToString("0"), Theme.FontTitle - 6, TextAnchor.MiddleCenter,
                allowed ? Theme.Timing : Theme.TextDim, FontStyle.BoldAndItalic);
            string status;
            if (!allowed) status = Theme.Dim(Theme.Caps(Loc.T("bike.frozen")));
            else if (project != null) status = Theme.GoodText(Theme.Caps(Loc.T("bike.inDev", (project.DaysWorked / Math.Max(1, project.DurationDays) * 100).ToString("0"))));
            else if (gained > 0) status = Theme.GoodText(Loc.T("bike.seasonGain", Fmt.Signed(gained)));
            else status = Theme.Dim(Theme.Caps(Loc.T("bike.tapToDevelop")));
            BikeCards.At(body, 0, 0.08f, 1, 0.38f, status, Theme.FontSmall - 2, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold, 8);
            BikeCards.Gauge(body, 0, 0, 1, 0.045f, level / 100.0, allowed ? Ui.RatingColor(level) : (Color)Theme.Steel);
        }
    }

    /// <summary>
    /// R&amp;D, one component at a time: big tabs across the top; for the chosen component its level, one line on
    /// what it does and the quick "upgrade part" button; the chosen project (gain, chance of success, cost,
    /// engineering, time) with the big start button; on the right the projects running (progress, cancel) and the
    /// history.
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

            // ---- tabs: one per component of the bike
            var comps = BikeScreen.Components(bike, bikeClass);
            if (comps.Count == 0) return;
            if (Focus != null && comps.Contains(Focus))
            {
                if (_component != Focus) _tier = 0;
                _component = Focus;
            }
            Focus = null;
            if (_component == null || !comps.Contains(_component)) _component = comps[0];
            var labels = new List<string>();
            foreach (var c in comps)
            {
                bool busy = running.Exists(p => p.Component == c);
                labels.Add(Fmt.Component(c) + (busy ? "  " + Theme.GoodText("●") : ""));
            }
            Ui.Chips(list, labels, comps.IndexOf(_component), i =>
            {
                if (_component != comps[i]) _tier = 0;
                _component = comps[i];
                _confirmCancel = null;
                Shell.Refresh();
            }, 56);

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

            // Level badge, one sentence, and the quick upgrade (the safe, cheapest project).
            var head = UiFactory.Row(col, 100);
            BikeCards.Square(head, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 0), 92, level.ToString("0"),
                allowed ? Theme.Timing : Theme.Steel, Loc.T("bike.levelShort"));
            var info = BikeCards.At(head, 0, 0, 0.6f, 1, "<b>" + Theme.Caps(Fmt.Component(comp)) + "</b>\n" + Theme.Dim(Loc.T("compinfo." + comp)),
                Theme.FontSmall, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Normal, 0);
            info.rectTransform.offsetMin = new Vector2(112, 0);
            info.rectTransform.offsetMax = new Vector2(-8, 0);
            if (templates.Count > 0)
            {
                var quick = templates[0];
                string why = Blocker(world, team, quick, running, capacity, allowed);
                double cost = DevelopmentService.ProjectCost(world, Game.Db, team, quick);
                BikeCards.BigButton(head, 0.6f, 0.06f, 1, 0.94f, Loc.T("rnd.upgrade", Fmt.Signed(Gain(quick, comp))),
                    why ?? MathX.FormatMoney(cost) + " · " + Loc.T("rnd.days", RealDays(world, team, comp, quick.DurationDays)),
                    BikeCards.UpgradeColor, () => StartProject(world, team, quick), why == null);
            }

            if (!allowed)
            {
                Ui.Note(col, Theme.WarnText(Loc.T("rnd.frozenInfo")));
                return;
            }
            if (templates.Count == 0)
            {
                Ui.Note(col, Theme.WarnText(Loc.T("rnd.nothing")));
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
                var b = UiFactory.Button("Tier", card.transform, Theme.Caps(TierName(templates[i])) + (busy ? "  " + Theme.GoodText("●") : ""),
                    () => { _tier = idx; Shell.Refresh(); }, on ? Theme.CurrentAccent : Color.Lerp(Theme.Panel, Theme.PanelLight, 0.55f), Theme.FontSmall - 2);
                UiFactory.Slant(b.image, 2.6f);
                UiFactory.Place((RectTransform)b.transform, (float)i / templates.Count, 0.7f, (float)(i + 1) / templates.Count, 1, 2);
                var lbl = b.GetComponentInChildren<UnityEngine.UI.Text>();
                lbl.supportRichText = true;
                lbl.resizeTextMinSize = 12;
                lbl.fontStyle = on ? FontStyle.BoldAndItalic : FontStyle.Bold;
                if (!on) lbl.color = Color.Lerp(Theme.TextDim, Color.white, 0.2f);
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
            BikeCards.At(card.transform, 0.32f, 0.02f, 1, 0.22f, string.Join("   ", notes.ToArray()), Theme.FontSmall - 4, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Normal, 8);

            // Cost, engineering and time, and the big start button.
            var bar = UiFactory.Row(col, 80);
            BikeCards.Plate(bar, 0, 0, 0.64f, 1, 0.45f, 2);
            Cell(bar, 0, 0.2f, Loc.T("rnd.cost"), Theme.Money(projectCost));
            Cell(bar, 0.2f, 0.42f, Loc.T("rnd.engineering"), Fmt.Rating(quality));
            Cell(bar, 0.42f, 0.64f, Loc.T("rnd.time"), Loc.T("rnd.days", RealDays(world, team, comp, t.DurationDays)));
            var template = t;
            BikeCards.BigButton(bar, 0.64f, 0, 1, 1, Loc.T("rnd.start"), blocker, BikeCards.StartColor, () => StartProject(world, team, template), blocker == null);
        }

        private static void Cell(RectTransform bar, float x0, float x1, string caption, string value)
        {
            BikeCards.At(bar, x0, 0.56f, x1, 0.94f, Theme.Caps(caption), Theme.FontSmall - 4, TextAnchor.MiddleCenter, Color.Lerp(Theme.TextDim, Color.white, 0.2f), FontStyle.Bold, 6);
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
            Ui.Band(col, Loc.T("rnd.inProgress"), running.Count + "/" + capacity, 52);
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
                    Theme.FontSmall - 4, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Normal, 14);
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
            else Ui.Note(col, Theme.WarnText(Loc.T("rnd.full")));
            // Explanations, folded behind one "more information" button.
            Ui.Note(col, Loc.T("rnd.successInfo"));
            Ui.Note(col, Loc.T("rnd.availableInfo"));
            if (running.Count > 0) Ui.Note(col, Loc.T("rnd.cancelInfo"));

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
                if (done.Count == 0) UiFactory.Paragraph(hist, Theme.Dim(Loc.T("rnd.noHistory")), Theme.FontSmall - 2);
            }, Shell.Refresh);
        }
    }

    /// <summary>
    /// Facilities as a grid of tiles (five per row, one screen): the caps name, the level big in timing yellow
    /// with pips, one line on what it does and a big "upgrade" button with the price (greyed with the reason when
    /// the cash is short). The longer explanations stay folded behind ⓘ.
    /// </summary>
    public class FacilitiesPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.facilities"); } }

        private const int PerRow = 5;
        private const int MaxLevel = 5;

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var types = (FacilityType[])Enum.GetValues(typeof(FacilityType));
            double cash = Accounts.Cash(world, team);
            RectTransform row = null;
            for (int idx = 0; idx < types.Length; idx++)
            {
                if (idx % PerRow == 0) row = UiFactory.Row(list, 246);
                int col = idx % PerRow;
                FacilityTile(row, (float)col / PerRow, (float)(col + 1) / PerRow, world, team, types[idx], cash);
            }

            Ui.Expander(list, "facility.info", "ⓘ  " + Loc.T("facility.effect"), inner =>
            {
                RectTransform l, r;
                Ui.Columns(inner, out l, out r);
                var lc = Ui.Card(l, null);
                var rc = Ui.Card(r, null);
                int half = (types.Length + 1) / 2;
                for (int idx = 0; idx < types.Length; idx++)
                    UiFactory.Paragraph(idx < half ? lc : rc, "<b>" + Loc.T("facility." + types[idx]) + "</b>  " + Theme.Dim(Loc.T("facilitylong." + types[idx])), Theme.FontSmall - 2);
                UiFactory.Paragraph(rc, Theme.Dim(Loc.T("facility.note")), Theme.FontSmall - 4);
            }, Shell.Refresh);
        }

        private void FacilityTile(RectTransform row, float x0, float x1, WorldState world, Team team, FacilityType type, double cash)
        {
            int lvl = team.Facility(type);
            var body = BikeCards.Tile(row, x0, 0, x1, 1, Loc.T("facility." + type));
            BikeCards.At(body, 0, 0.68f, 1, 1, Theme.TimingText("<size=" + Theme.FontSmall + ">" + Theme.Caps(Loc.T("bike.level")) + " </size>" + lvl),
                Theme.FontLarge + 4, TextAnchor.MiddleCenter, Theme.Text, FontStyle.BoldAndItalic);
            BikeCards.Pips(body, 0.12f, 0.585f, 0.88f, 0.665f, lvl, MaxLevel);
            BikeCards.At(body, 0, 0.38f, 1, 0.57f, Theme.Dim(Loc.T("facilityinfo." + type)), Theme.FontSmall - 4, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Normal, 8);
            if (lvl >= MaxLevel)
            {
                BikeCards.At(body, 0, 0.02f, 1, 0.37f, Theme.TimingText("<b>" + Theme.Caps(Loc.T("facility.max")) + "</b>"), Theme.FontSmall, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Italic);
                return;
            }
            double cost = FinanceService.FacilityUpgradeCost(world, Game.Db, team, type);
            bool affordable = cash >= cost;
            var target = type;
            BikeCards.BigButton(body, 0, 0, 1, 0.38f, Loc.T("facility.upgradeCost", MathX.FormatMoney(cost)), affordable ? null : Loc.T("rnd.noCash"),
                BikeCards.UpgradeColor, () =>
                {
                    bool ok = FinanceService.UpgradeFacility(world, Game.Db, team, target);
                    Shell.Toast(ok ? Loc.T("facility.upgraded", Loc.T("facility." + target)) : Loc.T("facility.noBudget"), ok);
                    Shell.Refresh();
                }, affordable, 4);
        }
    }
}
