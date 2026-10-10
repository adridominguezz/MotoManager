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
        /// <summary>Project name in the player's language (component + ambition), falling back to the data name.</summary>
        public static string ProjectLabel(DevelopmentProject p)
        {
            string id = p.TemplateId ?? "";
            string tier = id.EndsWith("_inc") ? "inc" : id.EndsWith("_major") ? "major" : id.EndsWith("_exp") ? "exp" : null;
            if (tier == null || string.IsNullOrEmpty(p.Component)) return p.Name;
            string english = tier == "inc" ? "incremental update" : tier == "major" ? "new specification" : "experimental concept";
            return Txt.F("projname", "{0}: {1}", Txt.Name("comp." + p.Component, p.Component), Txt.Name("project." + tier, english));
        }

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
                error = Txt.F("rnd.e.frozen", "The regulations do not allow developing {0}.", Txt.Name("comp." + t.Component, t.Component));
                return null;
            }
            double cost = ProjectCost(world, db, team, t);
            if (Accounts.Cash(world, team) < cost)
            {
                error = Txt.F("rnd.e.budget", "Not enough budget ({0} needed).", MathX.FormatMoney(cost));
                return null;
            }
            int active = 0;
            foreach (var p in world.Projects)
                if (p.TeamId == team.Id && p.Status == ProjectStatus.InProgress) active++;
            int capacity = TeamCapabilities.ProjectCapacity(team);
            if (active >= capacity)
            {
                error = Txt.F("rnd.e.capacity", "The design office can only run {0} projects at once.", capacity);
                return null;
            }
            Accounts.Post(world, team, LedgerKind.Development, -cost, t.Name);
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
            return TeamCapabilities.DevelopmentSpeed(world, team, p.Component);
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
            double eng = TeamCapabilities.ProjectQuality(world, team, p.Component, p.EngineerId);
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
                    // Concession tier of the manufacturer (more testing and freedom for those behind).
                    var maker = world.GetManufacturer(team.ManufacturerId);
                    if (maker != null) value *= maker.FactorIn(team.CategoryId);
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
                foreach (var kv in p.ActualEffects) parts.Add(Txt.Name("comp." + kv.Key, kv.Key) + " " + (kv.Value >= 0 ? "+" : "") + kv.Value.ToString("0.0", Txt.Culture));
                string name = ProjectLabel(p);
                world.AddNews(failed ? Txt.F("news.projectFailed", "Project failed: {0}", name) : Txt.F("news.projectDone", "Project completed: {0}", name),
                    failed ? Txt.F("news.projectFailedBody", "The new parts did not deliver. {0}", string.Join(", ", parts)) : string.Join(", ", parts), team.CategoryId, true);
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
                double strength = m.EngineeringStrength / 100.0 * Career.TeamAI.InSeasonDevelopmentFactor(team) * m.FactorIn(categoryId);
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

        /// <summary>
        /// Concessions from the constructors' standings. With tiers in the rules each manufacturer gets the tier its
        /// share of the leader's points earns (development multiplier, possibly unfrozen engines); without tiers the
        /// classic rule applies (below 35% of the leader: concessions).
        /// </summary>
        public static void UpdateConcessions(WorldState world, GameDatabase db)
        {
            var cats = new List<string>(world.Championships.Keys);
            cats.Sort(StringComparer.Ordinal);
            foreach (var catId in cats)
            {
                var champ = world.Championships[catId];
                var rules = db.GetRules(catId, world.Season);
                if (rules == null || !rules.Development.Concessions) continue;
                var table = champ.SortedManufacturers();
                double max = table.Count > 0 ? Math.Max(1, table[0].Points) : 1;
                var points = new Dictionary<string, double>();
                foreach (var e in table) points[e.Id] = e.Points;
                foreach (var m in world.Manufacturers.Values)
                {
                    if (!m.Programs.ContainsKey(catId)) continue;
                    double p;
                    double share = points.TryGetValue(m.Id, out p) ? p / max : 0;
                    var tiers = rules.Development.ConcessionTiers;
                    if (tiers.Count == 0)
                    {
                        m.HasConcessions = share < 0.35;
                        m.DevelopmentFactor[catId] = 1.0;
                        continue;
                    }
                    var tier = tiers[tiers.Count - 1];
                    foreach (var t in tiers)
                        if (share >= t.MinShare) { tier = t; break; }
                    m.DevelopmentFactor[catId] = tier.DevelopmentFactor;
                    m.ConcessionTier[catId] = tier.Name;
                    m.HasConcessions = tier.Unfreezes;
                }
            }
        }

        /// <summary>
        /// How manufacturers react to a season: those left behind in their flagship category may invest and hire
        /// engineers; dominant ones may lose key people to rivals; everything drifts back towards the middle over
        /// time. Fortunes change through these decisions, not through scripted turns.
        /// </summary>
        public static void ManufacturerSeasonReview(WorldState world, GameDatabase db, DeterministicRandom rng)
        {
            var makers = new List<Manufacturer>(world.Manufacturers.Values);
            makers.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var m in makers)
            {
                ChampionshipCategory flagship = null;
                foreach (var catId in m.Programs.Keys)
                {
                    var c = db.GetCategory(catId);
                    if (c != null && c.Simulated && !c.IsCup && (flagship == null || c.Prestige > flagship.Prestige)) flagship = c;
                }
                if (flagship == null) continue;
                ChampionshipState champ;
                if (!world.Championships.TryGetValue(flagship.Id, out champ) || champ.Rounds.Count == 0) continue;
                var table = champ.SortedManufacturers();
                double max = table.Count > 0 ? Math.Max(1, table[0].Points) : 1;
                double share = 0;
                bool leader = false;
                for (int i = 0; i < table.Count; i++)
                    if (table[i].Id == m.Id)
                    {
                        share = table[i].Points / max;
                        leader = i == 0;
                    }

                double before = m.EngineeringStrength;
                // Big groups can afford to react to a bad season; small ones often cannot.
                if (share < 0.6 && rng.Chance(0.2 + m.FinancialStrength / 200.0))
                {
                    // Restructuring: more money and new engineers.
                    m.DevelopmentBudget = Math.Min(m.MaxDevelopmentBudget, m.DevelopmentBudget * rng.Range(1.1, 1.3));
                    m.EngineeringStrength += rng.Range(1, 4) * (0.5 + m.FinancialStrength / 100.0);
                    // ...and a bigger budget for its works team, to sign the riders it needs.
                    foreach (var t in world.TeamsInCategory(flagship.Id))
                        if (t.ManufacturerId == m.Id && t.Status == TeamStatus.Factory && !t.IsPlayer)
                            t.SeasonBudget = Math.Round(Math.Max(t.SeasonBudget, Math.Min(t.SeasonBudget * 1.2, m.MaxDevelopmentBudget)));
                    if (flagship.Tier == 1)
                        world.AddNews(Txt.F("news.makerUp", "{0} steps up its racing programme", m.Name), Txt.F("news.makerUpBody", "{0} increases its budget and hires engineers after a difficult {1}.", m.Name, world.Season), flagship.Id);
                }
                else if (leader && rng.Chance(0.5))
                {
                    // Success attracts rivals' offers to key engineers.
                    m.EngineeringStrength -= rng.Range(0.5, 2.5);
                }
                // Long-term drift towards the middle and some unpredictability.
                m.EngineeringStrength += (68 - m.EngineeringStrength) * 0.05 + rng.Gaussian(0, 1.2);
                m.EngineeringStrength = Math.Round(MathX.Clamp(m.EngineeringStrength, 35, 95), 1);
                if (m.DevelopmentBudget > m.MaxDevelopmentBudget * 0.7) m.DevelopmentBudget *= 0.97;
                if (flagship.Tier == 1 && m.EngineeringStrength - before <= -3)
                    world.AddNews(Txt.F("news.makerLoses", "{0} loses key engineers", m.Name), Txt.F("news.makerLosesBody", "Rivals have recruited part of {0}'s technical staff.", m.Name), flagship.Id);
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
                world.AddNews(Txt.F("news.balancing", "{0} balancing: {1} loses engine performance", categoryId.ToUpperInvariant(), m != null ? m.Name : leader),
                    Txt.F("news.balancingBody", "The organiser reduces the rev limit of the dominant manufacturer."), categoryId);
            }
        }

        /// <summary>
        /// Winter development of manufacturer base specifications, then every team receives its new bike.
        /// Factory teams get the full spec, satellites a slightly older one; the player keeps part of their own work.
        /// </summary>
        public static void WinterDevelopment(WorldState world, GameDatabase db, DeterministicRandom rng)
        {
            UpdateConcessions(world, db);
            var resetNews = new HashSet<string>();

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
                    // A new technical era: much of the accumulated advantage is lost and everyone starts again
                    // around what their engineering department can do.
                    var nextRules = db.GetRules(kv.Key, world.Season + 1);
                    double reset = nextRules != null && nextRules.Id != rules.Id ? nextRules.Development.RegulationReset : 0;
                    if (reset > 0)
                    {
                        foreach (var comp in new List<string>(bike.Components.Keys))
                        {
                            if (comp == "SpecEngine") continue;
                            double target = 50 + m.EngineeringStrength * 0.4 + rng.Gaussian(0, 3);
                            bike.Components[comp] = MathX.Clamp(bike.Components[comp] + (target - bike.Components[comp]) * reset, 1, 99);
                        }
                        if (resetNews.Add(kv.Key) && catDef != null)
                            world.AddNews(Txt.F("news.newEra", "{0} enters a new technical era", catDef.Name), Txt.F("news.newEraBody", "New regulations from {0}: the established order is reshuffled.", world.Season + 1), kv.Key, true);
                    }
                    double factor = m.FactorIn(kv.Key);
                    foreach (var comp in new List<string>(bike.Components.Keys))
                    {
                        bool frozen = comp == "Engine" && rules.Engine.DevelopmentFrozen && !concessions;
                        if (comp == "SpecEngine" || frozen) continue;
                        double level = bike.Components[comp];
                        double gain = rng.Gaussian(1.2, 1.2) * (m.EngineeringStrength / 70.0) * money * factor * (100 - level) / 30.0;
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
