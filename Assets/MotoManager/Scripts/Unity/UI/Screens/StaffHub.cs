using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>The three staff groups of the staff section.</summary>
    public enum StaffGroup
    {
        Engineers,
        Mechanics,
        Strategists
    }

    /// <summary>
    /// Staff roles by group, and the positions of a group for a team (one per role, one per rider for the per-rider
    /// roles, plus anyone hired beyond that).
    /// <para>
    /// Mapping (every hireable role belongs to exactly one group):
    /// Engineers (design and development of the bike): technical director, chief engineer, electronics engineer,
    /// aerodynamicist, tyre engineer.
    /// Mechanics (the box crew of each rider): lead mechanic and race engineer (the crew chief who runs the rider's
    /// garage and set-up on the weekend), one of each per rider.
    /// Strategists (reading the race and the people around it): data analyst (set-up data, weather and race
    /// forecasts), scout, rider coach, physio and marketing. Any role added later falls here until it is mapped.
    /// </para>
    /// </summary>
    internal static class StaffGroups
    {
        public static readonly StaffGroup[] All = { StaffGroup.Engineers, StaffGroup.Mechanics, StaffGroup.Strategists };

        public static StaffGroup Of(StaffRole role)
        {
            switch (role)
            {
                case StaffRole.TechnicalDirector:
                case StaffRole.ChiefEngineer:
                case StaffRole.ElectronicsEngineer:
                case StaffRole.Aerodynamicist:
                case StaffRole.TireEngineer:
                    return StaffGroup.Engineers;
                case StaffRole.Mechanic:
                case StaffRole.RaceEngineer:
                    return StaffGroup.Mechanics;
                default:
                    return StaffGroup.Strategists;
            }
        }

        /// <summary>The roles of a group in enum order (the team manager is not staff).</summary>
        public static List<StaffRole> Roles(StaffGroup group)
        {
            var roles = new List<StaffRole>();
            foreach (StaffRole role in Enum.GetValues(typeof(StaffRole)))
                if (role != StaffRole.TeamManager && Of(role) == group) roles.Add(role);
            return roles;
        }

        public static string Icon(StaffGroup group)
        {
            switch (group)
            {
                case StaffGroup.Engineers: return UiIcons.Development;
                case StaffGroup.Mechanics: return UiIcons.Bike;
                default: return UiIcons.Flag;
            }
        }

        public static string Name(StaffGroup group)
        {
            return Loc.T("staff2.group." + group);
        }

        /// <summary>One position: its role, its holder (null when vacant) and whether a team of the category needs it.</summary>
        public class Position
        {
            public StaffRole Role;
            public StaffMember Holder;
            public bool Required;
            /// <summary>The rider this position works with (per-rider roles), or null.</summary>
            public Rider Rider;
        }

        /// <summary>
        /// The positions of a group: for each role as many as its holders, at least one (one per rider for race
        /// engineers and mechanics, as many as the market lets the team hold); the first ones are the category's
        /// standard ones. Per-rider positions follow the team's rider order.
        /// </summary>
        public static List<Position> Positions(WorldState world, GameDatabase db, Team team, StaffGroup group)
        {
            var list = new List<Position>();
            int riders = Math.Max(1, Math.Min(team.RiderIds.Count, 4));
            var standard = StaffService.StandardRoles(db, team, riders);
            foreach (var role in Roles(group))
            {
                var holders = new List<StaffMember>();
                foreach (var id in team.StaffIds)
                {
                    StaffMember s;
                    if (world.Staff.TryGetValue(id, out s) && s.Role == role) holders.Add(s);
                }
                int needed = 0;
                foreach (var r in standard) if (r == role) needed++;
                bool perRider = StaffService.PerRider(role);
                int slots = Math.Max(holders.Count, perRider ? Math.Max(1, team.RiderIds.Count) : 1);
                // Per-rider roles: one slot per rider, in the team's order, holding whoever works with that rider.
                var taken = new HashSet<StaffMember>();
                for (int i = 0; i < slots; i++)
                {
                    var p = new Position { Role = role, Required = i < needed };
                    if (perRider && i < team.RiderIds.Count)
                    {
                        string riderId = team.RiderIds[i];
                        p.Rider = world.GetRider(riderId);
                        p.Holder = holders.Find(h => h.AssignedRiderId == riderId && !taken.Contains(h));
                    }
                    if (p.Holder != null) taken.Add(p.Holder);
                    list.Add(p);
                }
                // Holders not tied to a slot's rider fill the empty slots, then the rest in order.
                foreach (var h in holders)
                {
                    if (taken.Contains(h)) continue;
                    var free = list.Find(p => p.Role == role && p.Holder == null);
                    if (free == null) break;
                    free.Holder = h;
                    taken.Add(h);
                }
            }
            return list;
        }

        /// <summary>Problems of a group: standard positions nobody holds and contracts ending this season.</summary>
        public static int Problems(WorldState world, List<Position> positions, out int vacant, out int ending)
        {
            vacant = 0;
            ending = 0;
            foreach (var p in positions)
            {
                if (p.Holder == null) { if (p.Required) vacant++; }
                else if (p.Holder.ContractEndSeason <= world.Season) ending++;
            }
            return vacant + ending;
        }
    }

    /// <summary>
    /// Staff hub: a strip with the headcount, the payroll and the hiring ceiling, then one big card per group
    /// (engineers, mechanics, strategists) with its positions held, average level and a badge with its problems
    /// (vacant standard positions, contracts ending this season). A card opens <see cref="StaffGroupPage"/>.
    /// </summary>
    public class StaffHubPage : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.staff"); } }

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;

            // Top strip: headcount and payroll on the left, the hiring ceiling (explained behind "i") on the right.
            int people = 0;
            double payroll = 0;
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (!world.Staff.TryGetValue(id, out s)) continue;
                people++;
                payroll += s.Salary;
            }
            var band = UiFactory.Rect("Band", content);
            band.anchorMin = new Vector2(0, 1);
            band.anchorMax = new Vector2(1, 1);
            band.pivot = new Vector2(0.5f, 1);
            band.offsetMin = new Vector2(0, -Ui.Grid.Header);
            band.offsetMax = Vector2.zero;
            var bc = Ui.Columns12(band, 8, 4);
            Ui.HeaderStrip(bc[0], Loc.T("staff.summaryShort", people, MathX.FormatMoney(payroll)));
            double ceiling = StaffMarket.SkillCeiling(world, Game.Db, team);
            var ceil = Ui.HeaderStrip(bc[1], Loc.T("ux.staff.ceiling"), Theme.TimingText("<size=" + Theme.TextHeader + ">" + ceiling.ToString("0") + "</size>"));
            Ui.InfoButton(ceil, Loc.T("ux.staff.ceiling"), Loc.T("staff.ceiling", ceiling.ToString("0")));

            // Bottom: the way to the full list of roles (every role, coverage and candidates on one screen).
            var bottom = Wp2Ui.BottomStrip(content, Ui.Grid.Hit);
            var link = UiFactory.Rect("All", bottom);
            UiFactory.Place(link, 0.6f, 0, 1, 1);
            Ui.Ghost(link, Loc.T("staff2.allRoles"), () => Shell.Show<StaffPage>());

            var area = Wp2Ui.Above(content, Ui.Grid.Hit + Ui.Grid.Gap, Ui.Grid.Header + Ui.Grid.Gap);
            var cols = Ui.Columns12(area, 4, 4, 4);
            for (int i = 0; i < StaffGroups.All.Length; i++)
            {
                var group = StaffGroups.All[i];
                var positions = StaffGroups.Positions(world, Game.Db, team, group);
                int vacant, ending;
                int problems = StaffGroups.Problems(world, positions, out vacant, out ending);
                Cards.Section(cols[i], StaffGroups.Icon(group), StaffGroups.Name(group), Summary(world, positions), () =>
                {
                    StaffGroupPage.Selected = group;
                    Shell.Show<StaffGroupPage>();
                }, problems, Loc.T("staff2.seePositions"));
            }
        }

        /// <summary>
        /// Three lines: positions held of all the group's positions, the holders' average level (rating colour) and
        /// the problems (vacant standard positions, contracts ending this season).
        /// </summary>
        private static string Summary(WorldState world, List<StaffGroups.Position> positions)
        {
            int held = 0;
            double sum = 0;
            foreach (var p in positions)
            {
                if (p.Holder == null) continue;
                held++;
                sum += p.Holder.Skill;
            }
            int vacant, ending;
            StaffGroups.Problems(world, positions, out vacant, out ending);
            string level = held > 0 ? "<color=#" + ColorUtility.ToHtmlStringRGB(Ui.RatingColor(sum / held)) + ">" + (sum / held).ToString("0") + "</color>" : "-";
            return Theme.Dim(Loc.T("staff2.line.positions")) + "  " + held + "/" + positions.Count + "\n" +
                   Theme.Dim(Loc.T("staff2.line.level")) + "  " + level + "\n" +
                   (vacant + ending > 0 ? Theme.WarnText(Loc.T("staff2.line.problems", vacant, ending)) : Theme.Dim(Loc.T("staff2.line.ok")));
        }
    }
}
