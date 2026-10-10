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
    /// <summary>The bike: component levels and what they add up to on track.</summary>
    public class BikeScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.bike"); } }

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

            var card = Ui.Card(list, bike.Model, (maker != null ? maker.Name + " · " : "") + (bikeClass != null ? bikeClass.Name : ""));
            foreach (var kv in bike.Components)
            {
                double gained;
                bike.SeasonGains.TryGetValue(kv.Key, out gained);
                bool allowed = DevelopmentService.ComponentAllowed(world, Game.Db, team, kv.Key);
                string value = kv.Value.ToString("0") + (gained > 0 ? " " + Theme.GoodText(Fmt.Signed(gained)) : "");
                Ui.Bar(card, Fmt.Component(kv.Key) + (allowed ? "" : "  " + Theme.Dim(Loc.T("bike.frozen"))), kv.Value / 100.0, value, Ui.RatingColor(kv.Value), 38);
            }
            Ui.Note(card, Loc.T("bike.componentsNote"));

            var perf = BikePerformance.Compute(bike, bikeClass);
            var pc = Ui.Card(list, Loc.T("bike.performance"), Loc.T("bike.performanceInfo"));
            PerfAttr[] main = { PerfAttr.TopSpeed, PerfAttr.Acceleration, PerfAttr.Braking, PerfAttr.MidCorner, PerfAttr.Traction, PerfAttr.Reliability };
            foreach (var a in main) Ui.Bar(pc, Loc.T("perf." + a), perf[a] / 100.0, perf[a].ToString("0"), Ui.RatingColor(perf[a]), 36);
            Ui.Expander(pc, "bike.perfAll", Loc.T("bike.allPerformance"), c =>
            {
                foreach (PerfAttr a in Enum.GetValues(typeof(PerfAttr)))
                    if (Array.IndexOf(main, a) < 0) Ui.Bar(c, Loc.T("perf." + a), perf[a] / 100.0, perf[a].ToString("0"), Ui.RatingColor(perf[a]), 34);
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

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;

            var running = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status == ProjectStatus.InProgress);
            int capacity = TeamCapabilities.ProjectCapacity(team);
            var now = Ui.Card(list, Loc.T("rnd.running", running.Count, capacity));
            foreach (var p in running)
            {
                double f = p.DaysWorked / Math.Max(1, p.DurationDays);
                Ui.Bar(now, ProjectName(p), f, (f * 100).ToString("0") + "%", Theme.Good, 38);
                Ui.Note(now, Loc.T("rnd.expected", Fmt.Effects(p.ExpectedEffects)));
            }
            if (running.Count == 0) Ui.Note(now, Loc.T("rnd.none"));

            var start = Ui.Card(list, Loc.T("rnd.available"), Loc.T("rnd.availableInfo"));
            bool full = running.Count >= capacity;
            int shown = 0;
            foreach (var t in DevelopmentService.AvailableProjects(world, Game.Db, team))
            {
                var template = t;
                double cost = DevelopmentService.ProjectCost(world, Game.Db, team, t);
                string detail = Fmt.Effects(t.Effects) + "\n" + Loc.T("rnd.terms", t.DurationDays, (t.Risk * 100).ToString("0"));
                if (full)
                    Ui.Item(start, ProjectName(t.Component, t.Id, t.Name), detail, MathX.FormatMoney(cost));
                else
                    Ui.Item(start, ProjectName(t.Component, t.Id, t.Name), detail, MathX.FormatMoney(cost), UiFactory.Act(Loc.T("rnd.start"), () =>
                    {
                        string error;
                        var p = DevelopmentService.Start(world, Game.Db, team, template, null, out error);
                        Shell.Toast(p != null ? Loc.T("rnd.started", ProjectName(template.Component, template.Id, template.Name)) : error, p != null);
                        Shell.Refresh();
                    }));
                shown++;
            }
            if (full) Ui.Note(start, Loc.T("rnd.full"));
            if (shown == 0) Ui.Note(start, Loc.T("rnd.nothing"));

            var done = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status != ProjectStatus.InProgress);
            if (done.Count > 0)
            {
                var hist = Ui.Card(list, null);
                Ui.Expander(hist, "rnd.history", Loc.T("rnd.history", done.Count), c =>
                {
                    for (int i = done.Count - 1; i >= 0; i--)
                    {
                        var p = done[i];
                        string status = p.Status == ProjectStatus.Completed ? Theme.GoodText(Loc.T("rnd.status.Completed")) : Theme.BadText(Loc.T("rnd.status." + p.Status));
                        Ui.Item(c, ProjectName(p), Fmt.Effects(p.ActualEffects), status);
                    }
                }, Shell.Refresh);
            }
        }
    }

    /// <summary>Facilities: level, what each one does and the cost of the next level.</summary>
    public class FacilitiesPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.facilities"); } }

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var card = Ui.Card(list, Loc.T("facility.title"), Loc.T("facility.note"));
            foreach (FacilityType f in Enum.GetValues(typeof(FacilityType)))
            {
                var type = f;
                int lvl = team.Facility(f);
                string title = Loc.T("facility." + f) + "   " + Stars(lvl);
                if (lvl >= 5)
                {
                    Ui.Item(card, title, Loc.T("facilityinfo." + f), Theme.Dim(Loc.T("facility.max")));
                    continue;
                }
                double cost = FinanceService.FacilityUpgradeCost(world, Game.Db, team, f);
                Ui.Item(card, title, Loc.T("facilityinfo." + f), MathX.FormatMoney(cost), UiFactory.Act(Loc.T("facility.upgradeButton"), () =>
                {
                    bool ok = FinanceService.UpgradeFacility(world, Game.Db, team, type);
                    Shell.Toast(ok ? Loc.T("facility.upgraded", Loc.T("facility." + type)) : Loc.T("facility.noBudget"), ok);
                    Shell.Refresh();
                }));
            }
        }

        private static string Stars(int level)
        {
            return "<color=" + Theme.WarnHex + ">" + new string('●', level) + "</color>" + Theme.Dim(new string('○', Math.Max(0, 5 - level)));
        }
    }
}
