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
    /// The bike: component levels on the left (one compact bar each), what they add up to on track on the right
    /// (the main values first, the rest on demand).
    /// </summary>
    public class BikeScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.bike"); } }

        /// <summary>Performance values shown without opening anything; the others are one tap away.</summary>
        private const int MainPerf = 7;

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

            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);
            var card = Ui.Card(leftCol, bike.Model, (maker != null ? maker.Name + " · " : "") + (bikeClass != null ? bikeClass.Name : ""));
            foreach (var kv in bike.Components)
            {
                double gained;
                bike.SeasonGains.TryGetValue(kv.Key, out gained);
                bool allowed = DevelopmentService.ComponentAllowed(world, Game.Db, team, kv.Key);
                string value = kv.Value.ToString("0") + (gained > 0 ? " " + Theme.GoodText(Fmt.Signed(gained)) : "");
                // A frozen component is greyed and marked in a few dim letters, not explained on every line.
                string label = allowed ? Fmt.Component(kv.Key) : Theme.Dim(Fmt.Component(kv.Key) + " · " + Loc.T("bike.frozen"));
                Ui.Bar(card, label, kv.Value / 100.0, value, allowed ? Ui.RatingColor(kv.Value) : (Color)Theme.Steel, 34);
            }
            Ui.Note(card, Loc.T("bike.componentsNote"));

            var perf = BikePerformance.Compute(bike, bikeClass);
            var attrs = (PerfAttr[])Enum.GetValues(typeof(PerfAttr));
            var pc = Ui.Card(rightCol, Loc.T("bike.performance"));
            for (int i = 0; i < attrs.Length && i < MainPerf; i++)
                Ui.Bar(pc, Loc.T("perf." + attrs[i]), perf[attrs[i]] / 100.0, perf[attrs[i]].ToString("0"), Ui.RatingColor(perf[attrs[i]]), 34);
            Ui.Note(pc, Loc.T("bike.performanceInfo"));
            if (attrs.Length > MainPerf)
                Ui.Expander(rightCol, "bike.perf", Loc.T("bike.allPerformance"), inner =>
                {
                    var more = Ui.Card(inner, null);
                    for (int i = MainPerf; i < attrs.Length; i++)
                        Ui.Bar(more, Loc.T("perf." + attrs[i]), perf[attrs[i]] / 100.0, perf[attrs[i]].ToString("0"), Ui.RatingColor(perf[attrs[i]]), 34);
                }, Shell.Refresh);
        }
    }

    /// <summary>R&amp;D: projects running, projects that can be started, and the history.</summary>
    public class ProjectsPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.rnd"); } }

        public static string ProjectName(DevelopmentProject p)
        {
            return ProjectName(p.Component, p.TemplateId, p.Name);
        }

        public static string ProjectName(string component, string templateId, string fallback)
        {
            if (string.IsNullOrEmpty(templateId) || string.IsNullOrEmpty(component)) return fallback;
            string tier = templateId.EndsWith("_inc") ? "inc" : templateId.EndsWith("_major") ? "major" : templateId.EndsWith("_exp") ? "exp" : null;
            return tier == null ? fallback : Fmt.Component(component) + ": " + Loc.T("project." + tier);
        }

        private int _view;
        private string _component;

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;

            var running = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status == ProjectStatus.InProgress);
            var done = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status != ProjectStatus.InProgress);
            int capacity = TeamCapabilities.ProjectCapacity(team);
            Ui.Chips(list, new[] { Loc.T("rnd.viewNew"), Loc.T("rnd.running", running.Count, capacity), Loc.T("rnd.history", done.Count) }, _view, i => { _view = i; Shell.Refresh(); });

            if (_view == 1)
            {
                // One progress bar per project with its expected effect right under it (data, not an explanation).
                var now = Ui.Card(list, null);
                foreach (var p in running)
                {
                    double f = p.DaysWorked / Math.Max(1, p.DurationDays);
                    Ui.Bar(now, "<b>" + ProjectName(p) + "</b>", f, (f * 100).ToString("0") + "%", Theme.Good, 38);
                    UiFactory.Paragraph(now, Theme.Dim(Loc.T("rnd.expected", Fmt.Effects(p.ExpectedEffects))), Theme.FontSmall - 4);
                }
                if (running.Count == 0) Ui.Note(now, Loc.T("rnd.none"));
                return;
            }
            if (_view == 2)
            {
                var hist = Ui.Card(list, null);
                var w = new[] { 5f, 5f, 2f };
                UiFactory.TableRow(hist, 34, w, new[] { Loc.T("rnd.col.project"), Loc.T("rnd.col.effect"), Loc.T("rnd.col.result") }, true);
                for (int i = done.Count - 1; i >= 0; i--)
                {
                    var p = done[i];
                    string status = p.Status == ProjectStatus.Completed ? Theme.GoodText(Loc.T("rnd.status.Completed")) : Theme.BadText(Loc.T("rnd.status." + p.Status));
                    UiFactory.TableRow(hist, 40, w, new[] { ProjectName(p), Fmt.Effects(p.ActualEffects), status });
                }
                if (done.Count == 0) UiFactory.Paragraph(hist, Theme.Dim(Loc.T("rnd.noHistory")), Theme.FontSmall - 2);
                return;
            }

            // New project: pick the component, then one of its projects (most ambitious = more gain, days and risk).
            var available = DevelopmentService.AvailableProjects(world, Game.Db, team);
            var components = new List<string>();
            foreach (var t in available) if (!components.Contains(t.Component)) components.Add(t.Component);
            var start = Ui.Card(list, null);
            if (components.Count == 0)
            {
                Ui.Note(start, Loc.T("rnd.nothing"));
                return;
            }
            if (_component == null || !components.Contains(_component)) _component = components[0];
            var labels = new List<string>();
            foreach (var c in components) labels.Add(Fmt.Component(c));
            Ui.Chips(start, labels, components.IndexOf(_component), i => { _component = components[i]; Shell.Refresh(); }, 52);
            bool full = running.Count >= capacity;
            Ui.Note(start, full ? Theme.WarnText(Loc.T("rnd.full")) : Loc.T("rnd.availableInfo"));
            foreach (var t in available)
            {
                if (t.Component != _component) continue;
                var template = t;
                double cost = DevelopmentService.ProjectCost(world, Game.Db, team, t);
                string detail = Fmt.Effects(t.Effects) + "\n" + Loc.T("rnd.terms", t.DurationDays, (t.Risk * 100).ToString("0"));
                if (full)
                    Ui.Item(start, ProjectName(t.Component, t.Id, t.Name), detail, MathX.FormatMoney(cost));
                else
                    Ui.Item(start, ProjectName(t.Component, t.Id, t.Name), detail, MathX.FormatMoney(cost), UiFactory.Primary(Loc.T("rnd.start"), () =>
                    {
                        string error;
                        var p = DevelopmentService.Start(world, Game.Db, team, template, null, out error);
                        Shell.Toast(p != null ? Loc.T("rnd.started", ProjectName(template.Component, template.Id, template.Name)) : error, p != null);
                        Shell.Refresh();
                    }));
            }
        }
    }

    /// <summary>
    /// Facilities: two columns of compact rows (name, level dots, cost of the next level and one button); what
    /// each facility does opens on demand below.
    /// </summary>
    public class FacilitiesPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.facilities"); } }

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);
            var types = (FacilityType[])Enum.GetValues(typeof(FacilityType));
            int half = (types.Length + 1) / 2;
            for (int idx = 0; idx < types.Length; idx++)
            {
                var f = types[idx];
                var col = idx < half ? leftCol : rightCol;
                var type = f;
                int lvl = team.Facility(f);
                if (lvl >= 5)
                {
                    Ui.Item(col, Loc.T("facility." + f), Stars(lvl), Theme.Dim(Loc.T("facility.max")));
                    continue;
                }
                double cost = FinanceService.FacilityUpgradeCost(world, Game.Db, team, f);
                // The price sits on the button (as in mobile management games), leaving the width to the name.
                Ui.Item(col, Loc.T("facility." + f), Stars(lvl), null, UiFactory.Primary(Loc.T("facility.upgradeButton") + "\n" + MathX.FormatMoney(cost), () =>
                {
                    bool ok = FinanceService.UpgradeFacility(world, Game.Db, team, type);
                    Shell.Toast(ok ? Loc.T("facility.upgraded", Loc.T("facility." + type)) : Loc.T("facility.noBudget"), ok);
                    Shell.Refresh();
                }));
            }

            // What each one is for, on demand, in two columns like the rows above.
            Ui.Expander(list, "facility.info", Loc.T("facility.effect"), inner =>
            {
                RectTransform l, r;
                Ui.Columns(inner, out l, out r);
                var lc = Ui.Card(l, null);
                var rc = Ui.Card(r, null);
                for (int idx = 0; idx < types.Length; idx++)
                    UiFactory.Paragraph(idx < half ? lc : rc, "<b>" + Loc.T("facility." + types[idx]) + "</b>  " + Theme.Dim(Loc.T("facilityinfo." + types[idx])), Theme.FontSmall - 2);
                UiFactory.Paragraph(rc, Theme.Dim(Loc.T("facility.note")), Theme.FontSmall - 4);
            }, Shell.Refresh);
        }

        private static string Stars(int level)
        {
            return "<color=" + Theme.WarnHex + ">" + new string('●', level) + "</color>" + Theme.Dim(new string('○', Math.Max(0, 5 - level)));
        }
    }
}
