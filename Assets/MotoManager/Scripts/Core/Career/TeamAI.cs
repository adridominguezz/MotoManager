using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    /// <summary>
    /// Team character and season objectives. Personalities are generated from what a team is (factory or private,
    /// reputation, money, manufacturer, category) and then evolve with results: teams that keep missing their
    /// objective take more risks and may change course. The values are read by the market (who to sign, salary
    /// ceiling, contract length), development (in-season pace, satellite spec gap), the off-season budget plan and
    /// race strategy (attack threshold, tyre choice, staying out in changing conditions).
    /// </summary>
    public static class TeamAI
    {
        // ------------------------------------------------------------------ creation

        /// <summary>Gives every team without one a personality and sets objectives where needed.</summary>
        public static void EnsureAll(WorldState world, GameDatabase db)
        {
            bool created = false;
            foreach (var team in Sorted(world))
            {
                if (team.Personality != null) continue;
                team.Personality = Generate(world, db, team);
                created = true;
            }
            if (created) SetObjectives(world, db);
        }

        public static TeamPersonality Generate(WorldState world, GameDatabase db, Team team)
        {
            var rng = DeterministicRandom.FromSeed(DeterministicRandom.Combine(world.MasterSeed, DeterministicRandom.Hash(team.Id)), "team-personality");
            var cat = db.GetCategory(team.CategoryId);
            var m = world.GetManufacturer(team.ManufacturerId);
            var p = new TeamPersonality();

            double rep = team.Reputation;
            double tier = cat != null ? cat.Tier : 2;
            bool cup = cat != null && cat.IsCup;

            switch (team.Status)
            {
                case TeamStatus.Factory: p.ManufacturerSupport = rng.Range(88, 100); break;
                case TeamStatus.Satellite: p.ManufacturerSupport = rng.Range(52, 72); break;
                default: p.ManufacturerSupport = rng.Range(22, 45); break;
            }

            double engineering = m != null ? m.EngineeringStrength : 55;
            p.TechnicalLevel = team.Status == TeamStatus.Factory
                ? MathX.Clamp(engineering * 0.7 + rep * 0.3 + rng.Gaussian(0, 4), 25, 95)
                : MathX.Clamp(25 + rep * 0.5 + rng.Gaussian(0, 7), 20, 85);

            double money = MathX.Clamp(team.SeasonBudget / 15000000.0, 0, 2);
            p.DevelopmentRate = MathX.Clamp(35 + money * 15 + (team.Status == TeamStatus.Factory ? 12 : 0) + rng.Gaussian(0, 8), 15, 95);

            // Philosophy: weighted by what the team is.
            var weights = new Dictionary<TeamPhilosophy, double>
            {
                { TeamPhilosophy.YoungTalent, cup ? 10 : (1 + (tier >= 2 ? 2.5 : 0.3) + (rep < 50 ? 1 : 0)) * (tier == 1 && team.Status == TeamStatus.Factory ? 0.4 : 1) },
                { TeamPhilosophy.Experience, 1.2 + (rep < 60 ? 0.8 : 0) + (cat != null && cat.Pyramid == Pyramid.Superbike ? 1.2 : 0) },
                { TeamPhilosophy.ManufacturerLoyal, team.Status == TeamStatus.Factory ? 2.5 : team.Status == TeamStatus.Satellite ? 1.2 : 0.2 },
                { TeamPhilosophy.ImmediateResults, 1 + (rep > 65 ? 2 : 0) + (team.Status == TeamStatus.Factory ? 1 : 0) },
                { TeamPhilosophy.LongTerm, 1 + (rep < 55 ? 1 : 0) + (tier >= 2 ? 0.8 : 0) }
            };
            var options = new List<TeamPhilosophy>(weights.Keys);
            options.Sort();
            p.Philosophy = rng.PickWeighted(options, o => weights[o]);

            double risk = 50 + rng.Gaussian(0, 12);
            if (p.Philosophy == TeamPhilosophy.ImmediateResults) risk += 10;
            if (p.Philosophy == TeamPhilosophy.LongTerm || p.Philosophy == TeamPhilosophy.Experience) risk -= 8;
            p.RiskTolerance = p.BaseRiskTolerance = Math.Round(MathX.Clamp(risk, 10, 90), 1);
            p.TechnicalLevel = Math.Round(p.TechnicalLevel, 1);
            p.DevelopmentRate = Math.Round(p.DevelopmentRate, 1);
            p.ManufacturerSupport = Math.Round(p.ManufacturerSupport, 1);
            return p;
        }

        public static TeamPersonality Of(Team team)
        {
            return team != null && team.Personality != null ? team.Personality : Default;
        }

        private static readonly TeamPersonality Default = new TeamPersonality();

        // ------------------------------------------------------------------ objectives

        /// <summary>How strong a team is expected to be before the season (reputation, status, machinery, crew).</summary>
        public static double ExpectedStrength(WorldState world, Team team)
        {
            var p = Of(team);
            var m = world.GetManufacturer(team.ManufacturerId);
            double s = team.Reputation + (m != null ? m.EngineeringStrength * 0.25 : 12) + p.ManufacturerSupport * 0.1 + p.TechnicalLevel * 0.1;
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r != null) s += r.Attributes.Overall * 0.3 / Math.Max(1, team.RiderIds.Count);
            }
            return s;
        }

        /// <summary>Sets each team's objective for the coming season from its expected rank in its category.</summary>
        public static void SetObjectives(WorldState world, GameDatabase db)
        {
            var byCat = new Dictionary<string, List<Team>>();
            foreach (var team in Sorted(world))
            {
                var cat = db.GetCategory(team.CategoryId);
                if (cat == null || !cat.Simulated) continue;
                List<Team> list;
                if (!byCat.TryGetValue(cat.Id, out list)) byCat[cat.Id] = list = new List<Team>();
                list.Add(team);
            }
            foreach (var kv in byCat)
            {
                var cat = db.GetCategory(kv.Key);
                var list = kv.Value;
                var strength = new Dictionary<string, double>();
                foreach (var t in list) strength[t.Id] = ExpectedStrength(world, t);
                list.Sort((a, b) =>
                {
                    int c = strength[b.Id].CompareTo(strength[a.Id]);
                    return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
                });
                int n = list.Count;
                for (int i = 0; i < n; i++)
                {
                    var p = list[i].Personality;
                    if (p == null) continue;
                    double frac = (double)i / Math.Max(1, n);
                    TeamObjective o;
                    if (i == 0) o = TeamObjective.Title;
                    else if (i <= 2 && n > 6) o = TeamObjective.Wins;
                    else if (frac < 0.35) o = TeamObjective.Podiums;
                    else if (frac < 0.6) o = TeamObjective.TopHalf;
                    else o = TeamObjective.Points;
                    // Development-minded teams in lower categories are judged on their riders' progress.
                    if ((cat.IsCup || (cat.Tier >= 2 && p.Philosophy == TeamPhilosophy.YoungTalent)) && o >= TeamObjective.TopHalf) o = TeamObjective.DevelopRiders;
                    p.Objective = o;
                }
            }
        }

        /// <summary>Highest team-championship position that counts as meeting the objective.</summary>
        public static int TargetPosition(TeamObjective o, int teams)
        {
            switch (o)
            {
                case TeamObjective.Title: return 1;
                case TeamObjective.Wins: return Math.Min(teams, 3);
                case TeamObjective.Podiums: return Math.Min(teams, Math.Max(4, (int)Math.Ceiling(teams * 0.35)));
                case TeamObjective.TopHalf: return Math.Max(1, (int)Math.Ceiling(teams * 0.5));
                default: return Math.Max(1, (int)Math.Ceiling(teams * 0.85));
            }
        }

        /// <summary>
        /// End-of-season review (before the market): objective met or missed, pressure on the team's risk appetite,
        /// possible change of philosophy after repeated failure.
        /// </summary>
        public static void SeasonReview(WorldState world, GameDatabase db, DeterministicRandom rng)
        {
            foreach (var team in Sorted(world))
            {
                var p = team.Personality;
                var cat = db.GetCategory(team.CategoryId);
                ChampionshipState champ;
                if (p == null || cat == null || !cat.Simulated || !world.Championships.TryGetValue(cat.Id, out champ) || champ.Season != world.Season) continue;
                var table = champ.SortedTeams();
                int pos = 0;
                for (int i = 0; i < table.Count; i++)
                    if (table[i].Id == team.Id) pos = i + 1;
                if (pos == 0) continue;

                bool met = Met(world, team, p.Objective, pos, table.Count);
                p.LastPosition = pos;
                p.LastObjective = p.Objective;
                p.LastObjectiveMet = met;
                if (met)
                {
                    p.SeasonsMissed = 0;
                    p.RiskTolerance += (p.BaseRiskTolerance - p.RiskTolerance) * 0.5;
                }
                else
                {
                    p.SeasonsMissed++;
                    p.RiskTolerance = Math.Min(95, p.RiskTolerance + 6);
                    if (p.SeasonsMissed >= 2 && rng.Chance(0.35))
                    {
                        var old = p.Philosophy;
                        p.Philosophy = p.Philosophy == TeamPhilosophy.ImmediateResults ? TeamPhilosophy.LongTerm
                            : p.Philosophy == TeamPhilosophy.LongTerm || p.Philosophy == TeamPhilosophy.YoungTalent ? TeamPhilosophy.ImmediateResults
                            : TeamPhilosophy.Experience;
                        if (p.Philosophy == old) p.Philosophy = TeamPhilosophy.ImmediateResults;
                        p.SeasonsMissed = 0;
                        if (cat.Tier == 1 && !team.IsPlayer)
                            world.AddNews(team.Name + " changes direction",
                                "After another season below expectations the team moves to a " + Describe(p.Philosophy) + " approach.", cat.Id);
                    }
                    else if (p.SeasonsMissed >= 2 && cat.Tier == 1 && !team.IsPlayer && p.Objective <= TeamObjective.Podiums)
                    {
                        world.AddNews(team.Name + " under pressure", team.Name + " has missed its objectives two seasons in a row.", cat.Id);
                    }
                }
                p.RiskTolerance = Math.Round(MathX.Clamp(p.RiskTolerance, 5, 95), 1);
            }
        }

        private static bool Met(WorldState world, Team team, TeamObjective o, int pos, int teams)
        {
            if (o != TeamObjective.DevelopRiders) return pos <= TargetPosition(o, teams);
            // A young rider of the team made clear progress (or the team simply did well).
            if (pos <= TargetPosition(TeamObjective.TopHalf, teams)) return true;
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r == null || r.CareerHistory.Count == 0) continue;
                var last = r.CareerHistory[r.CareerHistory.Count - 1];
                if (last.Season == world.Season && r.AgeOn(world.CurrentDate) <= 23 && last.Points > 0) return true;
            }
            return false;
        }

        public static string Describe(TeamPhilosophy p)
        {
            switch (p)
            {
                case TeamPhilosophy.YoungTalent: return "young-talent";
                case TeamPhilosophy.Experience: return "experience-first";
                case TeamPhilosophy.ManufacturerLoyal: return "manufacturer-led";
                case TeamPhilosophy.ImmediateResults: return "results-now";
                default: return "long-term";
            }
        }

        /// <summary>
        /// Off-season budget plan of AI teams: part of the money above the operating floor goes into the technical
        /// department (more for long-term, development-minded teams); unused capability slowly fades.
        /// </summary>
        public static void OffseasonInvestment(WorldState world, GameDatabase db)
        {
            foreach (var team in Sorted(world))
            {
                var p = team.Personality;
                var cat = db.GetCategory(team.CategoryId);
                var org = world.OrganisationOf(team);
                if (p == null || team.IsPlayer || org == null || org.Owner == OwnerKind.Player || cat == null || !cat.Simulated || cat.IsCup) continue;
                var rules = db.GetRules(cat.Id, world.Season);
                double floor = OperatingFloor(rules);
                // The organisation's spare cash is shared between the teams it owns.
                double surplus = org.Cash / Math.Max(1, org.TeamIds.Count) - floor * 1.5;
                double gain = 0;
                if (surplus > 0)
                {
                    double share = 0.15 + p.DevelopmentRate / 100.0 * 0.35;
                    if (p.Philosophy == TeamPhilosophy.LongTerm) share *= 1.25;
                    if (p.Philosophy == TeamPhilosophy.ImmediateResults) share *= 0.8;
                    double invest = surplus * share;
                    Economy.Accounts.Post(world, team, LedgerKind.TechnicalInvestment, -invest, "Technical department", true, false);
                    double unit = Math.Max(100000, rules.Economy.TypicalRiderSalary * 10);
                    gain = Math.Min(3, invest / unit);
                }
                // Capability decays towards what the team's reputation sustains.
                double sustain = 30 + team.Reputation * 0.5;
                if (p.TechnicalLevel > sustain) gain -= 0.8;
                p.TechnicalLevel = Math.Round(MathX.Clamp(p.TechnicalLevel + gain, 15, 98), 1);
            }
        }

        public static double OperatingFloor(CategoryRules rules)
        {
            return rules.Economy.TypicalRiderSalary * 4 + rules.Economy.TravelCostPerRound * 10;
        }

        // ------------------------------------------------------------------ market helpers

        /// <summary>Philosophy and risk adjustments to how a team values a rider (added to the market score).</summary>
        public static double SigningBias(Team team, Rider r, double age, double ability, double growth, double resultsScore, bool sameManufacturer, bool unproven)
        {
            var p = Of(team);
            double s = 0;
            switch (p.Philosophy)
            {
                case TeamPhilosophy.YoungTalent:
                    s += growth * 0.5 - Math.Max(0, age - 24) * 0.6;
                    break;
                case TeamPhilosophy.Experience:
                    s += age >= 25 && age <= 33 ? 2.5 : 0;
                    s -= growth * 0.25;
                    s += resultsScore * 0.3;
                    break;
                case TeamPhilosophy.ManufacturerLoyal:
                    s += sameManufacturer ? 5 : 0;
                    break;
                case TeamPhilosophy.ImmediateResults:
                    s += resultsScore * 0.6 - growth * 0.3 + (ability - 60) * 0.05;
                    break;
                case TeamPhilosophy.LongTerm:
                    s += growth * 0.3 + (age < 25 ? 1.5 : 0);
                    break;
            }
            // Cautious teams avoid unknown quantities; gamblers like them.
            if (unproven) s += (p.RiskTolerance - 50) / 10.0;
            return s;
        }

        /// <summary>Share of the budget a team is prepared to spend on one rider's salary.</summary>
        public static double SalaryCeiling(Team team)
        {
            var p = Of(team);
            double share = 0.25 * (0.7 + p.RiskTolerance / 100.0 * 0.6);
            if (p.Philosophy == TeamPhilosophy.ImmediateResults) share *= 1.15;
            return Math.Max(50000, team.SeasonBudget * share);
        }

        /// <summary>Hard limit on one rider's salary: beyond this the owners refuse to pay.</summary>
        public static double MaxSalary(Team team)
        {
            return SalaryCeiling(team) * 1.6;
        }

        /// <summary>Contract length the team offers (seasons).</summary>
        public static int ContractYears(Team team, double age, DeterministicRandom rng)
        {
            var p = Of(team);
            int min = 1, max = 2;
            if (p.Philosophy == TeamPhilosophy.LongTerm || (p.Philosophy == TeamPhilosophy.YoungTalent && age < 24)) max = 3;
            if (p.Philosophy == TeamPhilosophy.ImmediateResults) max = 2;
            if (age > 32) max = 1;
            return rng.NextInt(min, max + 1);
        }

        /// <summary>Change to the probability of renewing a rider, from the team's character and its season.</summary>
        public static double RenewalBias(Team team, double rank, double age)
        {
            var p = Of(team);
            double b = 0;
            if (!p.LastObjectiveMet) b -= p.Philosophy == TeamPhilosophy.ImmediateResults ? 0.15 : 0.07;
            if (p.Philosophy == TeamPhilosophy.YoungTalent) b += age < 24 ? 0.1 : -0.1;
            if (p.Philosophy == TeamPhilosophy.Experience) b += 0.05;
            if (p.Philosophy == TeamPhilosophy.LongTerm) b += age < 26 ? 0.08 : 0;
            if (p.Philosophy == TeamPhilosophy.ImmediateResults) b -= (rank - 0.3) * 0.2;
            return b;
        }

        // ------------------------------------------------------------------ development helpers

        /// <summary>Multiplier on in-season AI updates (manufacturer support, team engineering, development push).</summary>
        public static double InSeasonDevelopmentFactor(Team team)
        {
            var p = Of(team);
            return (0.2 + p.ManufacturerSupport / 100.0 * 0.8) * (0.7 + p.TechnicalLevel / 100.0 * 0.6);
        }

        /// <summary>Chance per component and event that an AI team brings an update.</summary>
        public static double InSeasonUpdateChance(Team team)
        {
            return 0.15 + Of(team).DevelopmentRate / 100.0 * 0.2;
        }

        /// <summary>How far behind the factory specification a customer team's winter bike is.</summary>
        public static double SpecOffset(Team team)
        {
            if (team.Status == TeamStatus.Factory) return 0;
            double support = team.Personality != null ? team.Personality.ManufacturerSupport : team.Status == TeamStatus.Satellite ? 60 : 33;
            return Math.Round((1 - support / 100.0) * 3.0, 2);
        }

        // ------------------------------------------------------------------ race helpers

        /// <summary>Team risk appetite for a race entry (0-100).</summary>
        public static double RaceRisk(Team team)
        {
            return Of(team).RiskTolerance;
        }

        private static List<Team> Sorted(WorldState world)
        {
            var list = new List<Team>(world.Teams.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }
    }
}
