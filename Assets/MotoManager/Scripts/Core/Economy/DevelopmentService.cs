using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Economy
{
    /// <summary>
    /// Bike development. Player projects have cost, duration, expected result, risk and an assigned engineer;
    /// results vary and many projects carry trade-offs. AI manufacturers develop their base specifications,
    /// and production categories are periodically re-balanced by the organiser.
    /// </summary>
    public static class DevelopmentService
    {
        public static bool ComponentAllowed(WorldState world, GameDatabase db, Team team, string component)
        {
            var rules = db.GetRules(team.CategoryId, world.Season);
            var manufacturer = world.GetManufacturer(team.ManufacturerId);
            bool concessions = rules.Development.Concessions && manufacturer != null && manufacturer.HasConcessions;
            var bike = world.GetBike(team.BikeId);
            var bikeClass = db.GetBikeClass(bike != null ? bike.BikeClassId : rules.BikeClassId);
            if (bikeClass == null || !bikeClass.Components.Contains(component)) return false;
            if (concessions) return true;
            if (rules.Development.AllowedComponents.Contains(component)) return true;
            return false;
        }

        public static List<DevelopmentTemplate> AvailableProjects(WorldState world, GameDatabase db, Team team)
        {
            var bike = world.GetBike(team.BikeId);
            var list = new List<DevelopmentTemplate>();
            if (bike == null) return list;
            foreach (var t in db.DevelopmentTemplates.Values)
            {
                if (t.BikeClasses.Count > 0 && !t.BikeClasses.Contains(bike.BikeClassId)) continue;
                if (!ComponentAllowed(world, db, team, t.Component)) continue;
                bool active = false;
                foreach (var p in world.Projects)
                    if (p.TeamId == team.Id && p.TemplateId == t.Id && p.Status == ProjectStatus.InProgress) active = true;
                if (!active) list.Add(t);
            }
            list.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.Component, b.Component);
                return c != 0 ? c : a.BaseCost.CompareTo(b.BaseCost);
            });
            return list;
        }

        public static double ProjectCost(WorldState world, GameDatabase db, Team team, DevelopmentTemplate t)
        {
            var rules = db.GetRules(team.CategoryId, world.Season);
            var bike = world.GetBike(team.BikeId);
            double level = bike != null ? bike.Component(t.Component) : 50;
            return Math.Round(t.BaseCost * Math.Max(0.05, rules.Development.CostMultiplier) * (0.75 + level / 200.0) / 1000) * 1000;
        }

        public static DevelopmentProject Start(WorldState world, GameDatabase db, Team team, DevelopmentTemplate t, string engineerId, out string error)
        {
            error = null;
            if (!ComponentAllowed(world, db, team, t.Component))
            {
                error = "The regulations do not allow developing " + t.Component + ".";
                return null;
            }
            double cost = ProjectCost(world, db, team, t);
            if (team.Budget < cost)
            {
                error = "Not enough budget (" + MathX.FormatMoney(cost) + " needed).";
                return null;
            }
            int active = 0;
            foreach (var p in world.Projects)
                if (p.TeamId == team.Id && p.Status == ProjectStatus.InProgress) active++;
            int capacity = 1 + team.Facility(FacilityType.DesignOffice);
            if (active >= capacity)
            {
                error = "The design office can only run " + capacity + " projects at once.";
                return null;
            }
            team.Budget -= cost;
            var project = new DevelopmentProject
            {
                Id = world.NewId("proj"),
                TemplateId = t.Id,
                Name = t.Name,
                TeamId = team.Id,
                BikeId = team.BikeId,
                Component = t.Component,
                ExpectedEffects = new Dictionary<string, double>(t.Effects),
                Cost = cost,
                DurationDays = t.DurationDays,
                Risk = t.Risk,
                EngineerId = engineerId,
                StartDate = world.CurrentDate,
                Status = ProjectStatus.InProgress
            };
            world.Projects.Add(project);
            return project;
        }

        public static double Speed(WorldState world, Team team, DevelopmentProject p)
        {
            double eng = 50;
            StaffMember s;
            if (p.EngineerId != null && world.Staff.TryGetValue(p.EngineerId, out s)) eng = s.Skill;
            else eng = StaffService.BestSkill(world, team, StaffRole.ChiefEngineer, 45);
            return 0.6 + eng / 200.0 + team.Facility(FacilityType.Workshop) * 0.07;
        }

        public static void Advance(WorldState world, GameDatabase db, double days, DeterministicRandom rng)
        {
            foreach (var p in world.Projects)
            {
                if (p.Status != ProjectStatus.InProgress) continue;
                var team = world.GetTeam(p.TeamId);
                if (team == null)
                {
                    p.Status = ProjectStatus.Cancelled;
                    continue;
                }
                p.DaysWorked += days * Speed(world, team, p);
                if (p.DaysWorked >= p.DurationDays) Resolve(world, db, team, p, rng);
            }
            if (world.Projects.Count > 200) world.Projects.RemoveRange(0, world.Projects.Count - 200);
        }

        private static void Resolve(WorldState world, GameDatabase db, Team team, DevelopmentProject p, DeterministicRandom rng)
        {
            var bike = world.GetBike(team.BikeId);
            double eng = 50;
            StaffMember s;
            if (p.EngineerId != null && world.Staff.TryGetValue(p.EngineerId, out s)) eng = s.Skill;
            double failChance = p.Risk * 0.45 * (1.2 - eng / 100.0);
            bool failed = rng.Chance(failChance);
            p.ActualEffects = new Dictionary<string, double>();
            foreach (var kv in p.ExpectedEffects)
            {
                double value = kv.Value;
                if (value > 0)
                {
                    if (failed) value = 0;
                    else value *= MathX.Clamp(1 + rng.Gaussian(0, p.Risk * 0.5) + (eng - 50) / 250.0, 0.2, 1.8);
                }
                else
                {
                    // Trade-offs always arrive; good engineers limit them.
                    value *= MathX.Clamp(1 + rng.Gaussian(0, 0.2) - (eng - 50) / 200.0, 0.3, 1.6);
                }
                p.ActualEffects[kv.Key] = Math.Round(value, 1);
            }
            if (bike != null) ApplyEffects(world, db, team, bike, p.ActualEffects);
            p.Status = failed ? ProjectStatus.Failed : ProjectStatus.Completed;
            if (team.IsPlayer)
            {
                var parts = new List<string>();
                foreach (var kv in p.ActualEffects) parts.Add(kv.Key + " " + (kv.Value >= 0 ? "+" : "") + kv.Value.ToString("0.0"));
                world.AddNews(failed ? "Project failed: " + p.Name : "Project completed: " + p.Name,
                    failed ? "The new parts did not deliver. " + string.Join(", ", parts) : string.Join(", ", parts), team.CategoryId, true);
            }
        }

        public static void ApplyEffects(WorldState world, GameDatabase db, Team team, Motorcycle bike, Dictionary<string, double> effects)
        {
            var rules = db.GetRules(team.CategoryId, world.Season);
            foreach (var kv in effects)
            {
                double delta = kv.Value;
                if (delta > 0 && rules.Development.SeasonGainCap > 0)
                {
                    double used;
                    bike.SeasonGains.TryGetValue(kv.Key, out used);
                    delta = Math.Min(delta, Math.Max(0, rules.Development.SeasonGainCap - used));
                    bike.SeasonGains[kv.Key] = used + delta;
                }
                if (!bike.Components.ContainsKey(kv.Key)) continue;
                bike.Components[kv.Key] = MathX.Clamp(bike.Components[kv.Key] + delta, 1, 100);
            }
        }

        /// <summary>
        /// Mid-season factory updates for AI teams (small and incremental, bigger for well funded manufacturers).
        /// </summary>
        public static void AiInSeasonUpdates(WorldState world, GameDatabase db, string categoryId, DeterministicRandom rng)
        {
            var rules = db.GetRules(categoryId, world.Season);
            foreach (var team in world.TeamsInCategory(categoryId))
            {
                if (team.IsPlayer) continue;
                var bike = world.GetBike(team.BikeId);
                var m = world.GetManufacturer(team.ManufacturerId);
                if (bike == null || m == null) continue;
                bool concessions = rules.Development.Concessions && m.HasConcessions;
                // Manufacturer support, the team's own engineering and its development push (see TeamAI).
                double strength = m.EngineeringStrength / 100.0 * Career.TeamAI.InSeasonDevelopmentFactor(team);
                double chance = Career.TeamAI.InSeasonUpdateChance(team);
                foreach (var comp in new List<string>(bike.Components.Keys))
                {
                    if (!concessions && !rules.Development.AllowedComponents.Contains(comp)) continue;
                    if (!rng.Chance(chance)) continue;
                    double level = bike.Components[comp];
                    double gain = rng.Range(0, 1.0) * strength * (100 - level) / 35.0;
                    ApplyEffects(world, db, team, bike, new Dictionary<string, double> { { comp, Math.Round(gain, 2) } });
                }
            }
        }

        /// <summary>Organiser balancing in production categories: rev-limit style adjustments of the engine.</summary>
        public static void ApplyBalanceOfPerformance(WorldState world, GameDatabase db, string categoryId, ChampionshipState champ)
        {
            var rules = db.GetRules(categoryId, world.Season);
            if (!rules.Development.BalanceOfPerformance || champ.Rounds.Count < 3) return;
            var table = ChampionshipState.Sort(champ.Manufacturers);
            if (table.Count < 2) return;
            string leader = table[0].Id;
            string last = table[table.Count - 1].Id;
            if (table[0].Points < table[1].Points * 1.25) leader = null;
            foreach (var team in world.TeamsInCategory(categoryId))
            {
                var bike = world.GetBike(team.BikeId);
                if (bike == null || !bike.Components.ContainsKey("ProductionEngine")) continue;
                if (team.ManufacturerId == leader) bike.Components["ProductionEngine"] = Math.Max(20, bike.Components["ProductionEngine"] - rules.Development.BalanceStep);
                if (team.ManufacturerId == last) bike.Components["ProductionEngine"] = Math.Min(100, bike.Components["ProductionEngine"] + rules.Development.BalanceStep * 0.5);
            }
            if (leader != null)
            {
                var m = world.GetManufacturer(leader);
                world.AddNews(categoryId.ToUpperInvariant() + " balancing: " + (m != null ? m.Name : leader) + " loses engine performance",
                    "The organiser reduces the rev limit of the dominant manufacturer.", categoryId);
            }
        }

        /// <summary>
        /// Winter development of manufacturer base specifications, then every team receives its new bike.
        /// Factory teams get the full spec, satellites a slightly older one; the player keeps part of their own work.
        /// </summary>
        public static void WinterDevelopment(WorldState world, GameDatabase db, DeterministicRandom rng)
        {
            // Concessions from constructor standings (MotoGP-style).
            foreach (var champ in world.Championships.Values)
            {
                var rules = db.GetRules(champ.CategoryId, world.Season);
                if (!rules.Development.Concessions) continue;
                var table = champ.SortedManufacturers();
                double max = table.Count > 0 ? Math.Max(1, table[0].Points) : 1;
                foreach (var entry in table)
                {
                    var m = world.GetManufacturer(entry.Id);
                    if (m != null) m.HasConcessions = entry.Points / max < 0.35;
                }
            }

            var baseBikes = new Dictionary<string, Motorcycle>();
            foreach (var m in world.Manufacturers.Values)
            {
                foreach (var kv in m.Programs)
                {
                    var bike = world.GetBike(kv.Value);
                    if (bike == null) continue;
                    var rules = db.GetRules(kv.Key, world.Season);
                    bool concessions = rules.Development.Concessions && m.HasConcessions;
                    // The group budget mostly funds the top-class programme; feeder programmes are far closer.
                    var catDef = db.GetCategory(kv.Key);
                    double money = MathX.Clamp(m.DevelopmentBudget / 30000000.0, 0.2, 1.6);
                    if (catDef != null && catDef.Tier > 1) money = 0.85 + 0.15 * money;
                    double cap = rules.Development.SeasonGainCap > 0 ? rules.Development.SeasonGainCap * 0.6 : double.MaxValue;
                    foreach (var comp in new List<string>(bike.Components.Keys))
                    {
                        bool frozen = comp == "Engine" && rules.Engine.DevelopmentFrozen && !concessions;
                        if (comp == "SpecEngine" || frozen) continue;
                        double level = bike.Components[comp];
                        double gain = rng.Gaussian(1.2, 1.2) * (m.EngineeringStrength / 70.0) * money * (100 - level) / 30.0;
                        if (concessions) gain += rng.Range(0.5, 2.0);
                        gain = Math.Min(gain, cap);
                        bike.Components[comp] = MathX.Clamp(level + gain, 1, 99);
                    }
                    bike.Year = world.Season + 1;
                    bike.SeasonGains.Clear();
                    baseBikes[m.Id + "|" + kv.Key] = bike;
                }
            }

            foreach (var team in world.Teams.Values)
            {
                Motorcycle baseBike;
                if (team.ManufacturerId == null || !baseBikes.TryGetValue(team.ManufacturerId + "|" + team.CategoryId, out baseBike)) continue;
                var current = world.GetBike(team.BikeId);
                var updated = baseBike.Clone(team.BikeId ?? world.NewId("bike"));
                double offset = Career.TeamAI.SpecOffset(team);
                foreach (var comp in new List<string>(updated.Components.Keys))
                {
                    double v = updated.Components[comp] - offset;
                    if (team.IsPlayer && current != null)
                    {
                        // Keep part of the player's own in-house development.
                        double own = current.Component(comp) - v;
                        if (own > 0) v += own * 0.5;
                    }
                    updated.Components[comp] = MathX.Clamp(v, 1, 99);
                }
                team.BikeId = updated.Id;
                world.Bikes[updated.Id] = updated;
            }
        }
    }
}
