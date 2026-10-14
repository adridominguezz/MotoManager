using System;
using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// "Desarrollo" hub, one screen without scrolling: on the left the bike's components as attribute cells (the
    /// facility bonus included and shown beside the name); on the right the bike's on-track score against every bike
    /// of the category (rank, ours, average and best, one column per bike with ours lit, the full list on tap) and
    /// the two big buttons to R&amp;D and facilities.
    /// </summary>
    public class DevelopmentPage : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.bike"); } }

        /// <summary>Most attribute rows the table splits its height into (fewer components leave room under them).</summary>
        private const int MinTableRows = 4;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var bike = world.GetBike(team.BikeId);
            if (bike == null) return;
            var bikeClass = Game.Db.GetBikeClass(bike.BikeClassId);

            var cols = Ui.Columns12(content, 7, 5);
            Attributes(cols[0], world, team, bike, bikeClass);
            var right = Ui.Rows(cols[1], 0.56f, 0.44f);
            Comparison(right[0], team, GridScores(world, Game.Db, team));
            Shortcuts(right[1], world, team);
        }

        /// <summary>A facility bonus as "+1,25" (two decimals at most, so quarter points are not rounded away).</summary>
        public static string Points(double v)
        {
            return (v >= 0 ? "+" : "") + v.ToString("0.##", Loc.Culture);
        }

        // ------------------------------------------------------------------ attributes

        private void Attributes(RectTransform area, WorldState world, Team team, Motorcycle bike, BikeClassDefinition bikeClass)
        {
            var body = Ui.Tile(area, Loc.T("dev2.attrs"), null, bike.Model);
            var comps = BikeScreen.Components(bike, bikeClass);
            if (comps.Count == 0) return;
            const float noteShare = 0.09f;
            int rows = (comps.Count + 1) / 2;
            float rowShare = (1 - noteShare) / Math.Max(rows, MinTableRows);
            bool anyBonus = false;
            for (int i = 0; i < comps.Count; i++)
            {
                string c = comps[i];
                double bonus = TeamCapabilities.ComponentBonus(world, Game.Db, team, c);
                double value = TeamCapabilities.EffectiveComponent(world, Game.Db, team, bike, c);
                if (bonus > 0) anyBonus = true;
                int r = i / 2, col = i % 2;
                float top = 1 - r * rowShare;
                var cell = Ui.Area(body, col * 0.5f, top - rowShare, (col + 1) * 0.5f, top);
                string label = Fmt.Component(c) + (bonus > 0 ? "  " + Theme.GoodText(Loc.T("dev2.facBonus", Points(bonus))) : "");
                Cards.Attribute(cell, label, value, 0, 100);
            }
            ScreenKit.At(body, 0, 0, 1, noteShare, Theme.Dim(Loc.T(anyBonus ? "dev2.attrsNote" : "dev2.attrsNoteNone")), Theme.TextMin,
                TextAnchor.MiddleLeft, Theme.Text);
        }

        // ------------------------------------------------------------------ the grid

        /// <summary>Every team of our category with its bike's on-track score (facilities included), best first.</summary>
        public static List<KeyValuePair<Team, double>> GridScores(WorldState world, GameDatabase db, Team team)
        {
            var res = new List<KeyValuePair<Team, double>>();
            foreach (var t in world.TeamsInCategory(team.CategoryId))
            {
                var b = world.GetBike(t.BikeId);
                if (b == null) continue;
                res.Add(new KeyValuePair<Team, double>(t, Score(world, db, t, b)));
            }
            if (!res.Exists(kv => kv.Key == team))
            {
                var own = world.GetBike(team.BikeId);
                if (own != null) res.Add(new KeyValuePair<Team, double>(team, Score(world, db, team, own)));
            }
            res.Sort((a, b) =>
            {
                int c = b.Value.CompareTo(a.Value);
                return c != 0 ? c : string.CompareOrdinal(a.Key.Id, b.Key.Id);
            });
            return res;
        }

        private static double Score(WorldState world, GameDatabase db, Team t, Motorcycle b)
        {
            return BikeScreen.PerformanceScore(TeamCapabilities.Performance(world, db, t, b, db.GetBikeClass(b.BikeClassId)));
        }

        private static string TeamName(Team t)
        {
            return string.IsNullOrEmpty(t.ShortName) ? t.Name : t.ShortName;
        }

        /// <summary>Rank in the header; ours, the average and the best as figures; one column per bike, ours lit.</summary>
        private static void Comparison(RectTransform area, Team team, List<KeyValuePair<Team, double>> grid)
        {
            int n = grid.Count;
            int rank = grid.FindIndex(kv => kv.Key == team) + 1;
            var body = Ui.Tile(area, Loc.T("dev2.grid"), () => GridSheet(team, grid), n > 0 && rank > 0 ? Loc.T("ux.dev.rank", rank, n) : null);
            if (n == 0 || rank == 0) return;
            double ours = grid[rank - 1].Value, best = grid[0].Value, sum = 0;
            foreach (var kv in grid) sum += kv.Value;
            double avg = sum / n;

            var figs = UiFactory.Rect("Figures", body);
            UiFactory.Place(figs, 0, 0.5f, 1, 1);
            ScreenKit.Figure(figs, 0, 0.34f, Loc.T("dev2.ours"), ours.ToString("0"), Theme.Timing);
            ScreenKit.Figure(figs, 0.34f, 0.67f, Loc.T("dev2.avg"), avg.ToString("0"));
            ScreenKit.Figure(figs, 0.67f, 1, Loc.T("dev2.best"), best.ToString("0"), null,
                rank == 1 ? Loc.T("dev2.youLead") : Loc.T("dev2.gap", (best - ours).ToString("0.#", Loc.Culture)));

            // One column per bike, best on the left; the scale starts a little under the worst so gaps read.
            var chart = UiFactory.Rect("Chart", body);
            UiFactory.Place(chart, 0, 0, 1, 0.46f);
            double hi = grid[0].Value, lo = grid[n - 1].Value;
            double floor = Math.Max(0, lo - Math.Max(3, (hi - lo) * 0.5));
            var baseLine = UiFactory.Decor("Base", chart, Theme.Alpha(Color.white, 0.14f));
            UiFactory.Place(baseLine.rectTransform, 0, 0, 1, 0);
            baseLine.rectTransform.offsetMax = new Vector2(0, 2);
            for (int i = 0; i < n; i++)
            {
                bool mine = grid[i].Key == team;
                float f = hi > floor ? (float)((grid[i].Value - floor) / (hi - floor)) : 1;
                var col = UiFactory.Decor(mine ? "Mine" : "Bike", chart, mine ? Theme.Timing : Theme.Alpha(Theme.Steel, 0.55f));
                UiFactory.Place(col.rectTransform, (float)i / n, 0, (float)(i + 1) / n, Mathf.Clamp(f, 0.04f, 1));
                col.rectTransform.offsetMin = new Vector2(2, 2);
                col.rectTransform.offsetMax = new Vector2(-2, 0);
            }
        }

        private static void GridSheet(Team team, List<KeyValuePair<Team, double>> grid)
        {
            Ui.Sheet(Loc.T("dev2.grid"), list =>
            {
                UiFactory.Paragraph(list, Theme.Dim(Loc.T("dev2.gridInfo")), Theme.TextCaption);
                var w = new[] { 0.8f, 4f, 1.2f };
                for (int i = 0; i < grid.Count; i++)
                    Ui.ListRow(list, w, new[] { (i + 1).ToString(), TeamName(grid[i].Key), grid[i].Value.ToString("0") }, i, grid[i].Key == team);
            }, null, null, 0.56f);
        }

        // ------------------------------------------------------------------ R&D and facilities

        private void Shortcuts(RectTransform area, WorldState world, Team team)
        {
            var cols = ScreenKit.Cols(area, 2);
            var running = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status == ProjectStatus.InProgress);
            int capacity = TeamCapabilities.ProjectCapacity(team);
            int free = Math.Max(0, capacity - running.Count);
            // A free slot only needs attention when there is something the regulations let us develop.
            bool canDevelop = free > 0 && DevelopmentService.AvailableProjects(world, Game.Db, team).Count > 0;
            string rnd = running.Count == 0 ? Loc.T("dev2.rnd.none") : Loc.T(running.Count == 1 ? "dev2.rnd.one" : "dev2.rnd.many", running.Count);
            if (canDevelop) rnd += " · " + Loc.T(free == 1 ? "dev2.rnd.free1" : "dev2.rnd.freeN", free);
            Cards.Section(cols[0], UiIcons.Development, Loc.T("tab.rnd"), rnd, () => Shell.Show<ProjectsPage>(), canDevelop ? free : 0);

            var types = (FacilityType[])Enum.GetValues(typeof(FacilityType));
            double cash = Accounts.Cash(world, team), sum = 0;
            int affordable = 0;
            foreach (var f in types)
            {
                int lvl = team.Facility(f);
                sum += lvl;
                if (lvl < FacilitiesPage.MaxLevel && cash >= FinanceService.FacilityUpgradeCost(world, Game.Db, team, f)) affordable++;
            }
            string fac = Loc.T("dev2.fac.avg", (sum / Math.Max(1, types.Length)).ToString("0.0", Loc.Culture));
            if (affordable > 0) fac += " · " + Loc.T(affordable == 1 ? "dev2.fac.afford1" : "dev2.fac.affordN", affordable);
            Cards.Section(cols[1], UiIcons.Home, Loc.T("tab.facilities"), fac, () => Shell.Show<FacilitiesPage>());
        }
    }
}
