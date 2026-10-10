using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Economy
{
    /// <summary>Generates, pays and develops team personnel.</summary>
    public enum RoleCoverage
    {
        Missing,
        Weak,
        Covered,
        Optional
    }

    public static class StaffService
    {
        public static StaffMember Generate(WorldState world, GameDatabase db, DeterministicRandom rng, StaffRole role, string teamId, double level, double salaryScale = 1.0)
        {
            var pools = new List<NamePool>(db.NamePools.Values);
            pools.Sort((a, b) => string.CompareOrdinal(a.Nationality, b.Nationality));
            var pool = pools.Count > 0 ? rng.PickWeighted(pools, p => p.WeightGrandPrix + p.WeightSuperbike) : null;
            double skill = MathX.Clamp(level + rng.Gaussian(0, 8), 20, 98);
            int experience = (int)MathX.Clamp(Math.Round(skill / 5 + rng.Gaussian(0, 3)), 0, 35);
            var s = new StaffMember
            {
                Id = world.NewId("staff"),
                FirstName = pool != null && pool.FirstNames.Count > 0 ? rng.Pick(pool.FirstNames) : "Sam",
                LastName = pool != null && pool.LastNames.Count > 0 ? rng.Pick(pool.LastNames) : "Engineer",
                Nationality = pool != null ? pool.Nationality : "IT",
                Role = role,
                BirthYear = world.Season - Math.Max(24, 24 + experience + rng.NextInt(0, 12)),
                Skill = skill,
                Experience = experience,
                Reputation = Math.Round(MathX.Clamp(skill * 0.7 + experience * 0.8 + rng.Gaussian(0, 5), 5, 95)),
                TeamId = teamId,
                ContractEndSeason = world.Season + rng.NextInt(0, 3),
                DataConfidence = "generated (fictional person)"
            };
            s.Salary = Math.Round(SalaryFor(role, skill, experience) * salaryScale / 1000) * 1000;
            return s;
        }

        /// <summary>Market salary of a role at the MotoGP reference (scaled per category by the caller).</summary>
        public static double SalaryFor(StaffRole role, double skill, int experience = 8)
        {
            double baseSalary;
            switch (role)
            {
                case StaffRole.TechnicalDirector: baseSalary = 900000; break;
                case StaffRole.ChiefEngineer: baseSalary = 320000; break;
                case StaffRole.RaceEngineer: baseSalary = 220000; break;
                case StaffRole.Aerodynamicist: baseSalary = 210000; break;
                case StaffRole.ElectronicsEngineer: baseSalary = 200000; break;
                case StaffRole.DataAnalyst: baseSalary = 140000; break;
                case StaffRole.TireEngineer: baseSalary = 120000; break;
                case StaffRole.RiderCoach: baseSalary = 110000; break;
                case StaffRole.Scout: baseSalary = 90000; break;
                case StaffRole.Mechanic: baseSalary = 90000; break;
                case StaffRole.Physio: baseSalary = 80000; break;
                default: baseSalary = 100000; break;
            }
            double exp = MathX.Clamp(0.8 + experience / 50.0, 0.8, 1.3);
            return Math.Round(baseSalary * (0.4 + Math.Pow(skill / 70.0, 2.2)) * exp / 1000) * 1000;
        }

        /// <summary>Staff pay level of the team's category (a Moto3 crew is paid far less than a MotoGP one).</summary>
        public static double SalaryScale(WorldState world, GameDatabase db, Team team)
        {
            var rules = team != null ? db.GetRules(team.CategoryId, world.Season) : null;
            return rules != null ? rules.Economy.StaffSalaryScale : 1.0;
        }

        public static double BestSkill(WorldState world, Team team, StaffRole role, double fallback = 40)
        {
            double best = -1;
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (world.Staff.TryGetValue(id, out s) && s.Role == role && s.Skill > best) best = s.Skill;
            }
            return best < 0 ? fallback : best;
        }

        /// <summary>Roles held by one person per rider (crew chief, lead mechanic).</summary>
        public static bool PerRider(StaffRole role)
        {
            return role == StaffRole.RaceEngineer || role == StaffRole.Mechanic;
        }

        /// <summary>
        /// The structure a team of its category normally has. Top categories have the full technical department;
        /// smaller teams a lean crew. Teams may hire more.
        /// </summary>
        public static List<StaffRole> StandardRoles(GameDatabase db, Team team, int riders)
        {
            var cat = db.GetCategory(team.CategoryId);
            var roles = new List<StaffRole> { StaffRole.ChiefEngineer, StaffRole.DataAnalyst, StaffRole.TireEngineer };
            if (cat != null && cat.Prestige >= 80)
            {
                // Prototype top class: the full technical department.
                roles.Add(StaffRole.TechnicalDirector);
                roles.Add(StaffRole.ElectronicsEngineer);
                roles.Add(StaffRole.Scout);
                roles.Add(StaffRole.Physio);
                roles.Add(StaffRole.RiderCoach);
                roles.Add(StaffRole.Aerodynamicist);
            }
            else if (cat != null && cat.Tier == 1)
            {
                // Production-based top class: a leaner structure.
                roles.Add(StaffRole.ElectronicsEngineer);
                roles.Add(StaffRole.Scout);
            }
            else if (team.Reputation > 40) roles.Add(StaffRole.Scout);
            for (int i = 0; i < riders; i++)
            {
                roles.Add(StaffRole.RaceEngineer);
                roles.Add(StaffRole.Mechanic);
            }
            return roles;
        }

        public static void EnsureStaff(WorldState world, GameDatabase db, Team team, DeterministicRandom rng, double scale)
        {
            double level = 35 + team.Reputation * 0.45 * scale;
            double pay = SalaryScale(world, db, team);
            var have = new Dictionary<StaffRole, int>();
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (!world.Staff.TryGetValue(id, out s)) continue;
                int n;
                have.TryGetValue(s.Role, out n);
                have[s.Role] = n + 1;
            }
            int riders = Math.Max(1, Math.Min(team.RiderIds.Count, 4));
            foreach (var role in StandardRoles(db, team, riders))
            {
                int n;
                have.TryGetValue(role, out n);
                if (n > 0)
                {
                    have[role] = n - 1;
                    continue;
                }
                var s = Generate(world, db, rng, role, team.Id, level, pay);
                world.Staff[s.Id] = s;
                team.StaffIds.Add(s.Id);
            }
            AssignToRiders(world, team);
        }

        /// <summary>
        /// Puts a race engineer or mechanic with another rider of the team. If someone in the same role already works
        /// with that rider, the two swap.
        /// </summary>
        public static bool Assign(WorldState world, Team team, StaffMember member, string riderId)
        {
            if (member == null || !PerRider(member.Role) || !team.RiderIds.Contains(riderId) || member.AssignedRiderId == riderId) return false;
            foreach (var id in team.StaffIds)
            {
                StaffMember other;
                if (!world.Staff.TryGetValue(id, out other) || other == member || other.Role != member.Role) continue;
                if (other.AssignedRiderId == riderId) other.AssignedRiderId = member.AssignedRiderId;
            }
            member.AssignedRiderId = riderId;
            return true;
        }

        /// <summary>
        /// How well a role is covered compared with what a team of this category normally has: Missing (standard role
        /// nobody does), Weak (below the usual level), Covered, or Optional (not standard here, not hired).
        /// </summary>
        public static RoleCoverage Coverage(WorldState world, GameDatabase db, Team team, StaffRole role, out double bestSkill, out int count)
        {
            bestSkill = -1;
            count = 0;
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (!world.Staff.TryGetValue(id, out s) || s.Role != role) continue;
                count++;
                bestSkill = Math.Max(bestSkill, s.Skill);
            }
            int riders = Math.Max(1, Math.Min(team.RiderIds.Count, 4));
            int needed = 0;
            foreach (var r in StandardRoles(db, team, riders)) if (r == role) needed++;
            double usual = 35 + team.Reputation * 0.45;
            if (count < needed) return RoleCoverage.Missing;
            if (count == 0) return RoleCoverage.Optional;
            if (bestSkill < usual - 8) return RoleCoverage.Weak;
            return RoleCoverage.Covered;
        }

        /// <summary>
        /// Keeps every team's race engineers and mechanics with riders who are still in the team (after transfers,
        /// releases, retirements and the winter market): anyone whose rider left moves to a rider without one.
        /// </summary>
        public static void SyncAll(WorldState world)
        {
            var ids = new List<string>(world.Teams.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (var id in ids) AssignToRiders(world, world.Teams[id]);
        }

        /// <summary>Race engineers and lead mechanics are paired with riders (in order).</summary>
        public static void AssignToRiders(WorldState world, Team team)
        {
            foreach (var role in new[] { StaffRole.RaceEngineer, StaffRole.Mechanic })
            {
                var free = new List<StaffMember>();
                var taken = new HashSet<string>();
                foreach (var id in team.StaffIds)
                {
                    StaffMember s;
                    if (!world.Staff.TryGetValue(id, out s) || s.Role != role) continue;
                    if (s.AssignedRiderId != null && team.RiderIds.Contains(s.AssignedRiderId) && taken.Add(s.AssignedRiderId)) continue;
                    s.AssignedRiderId = null;
                    free.Add(s);
                }
                free.Sort((a, b) => b.Skill.CompareTo(a.Skill));
                foreach (var riderId in team.RiderIds)
                {
                    if (taken.Contains(riderId) || free.Count == 0) continue;
                    free[0].AssignedRiderId = riderId;
                    taken.Add(riderId);
                    free.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Season end for personnel: experience and skill evolve, reputation follows the team's results, contracts
        /// expire. AI teams renew or replace; the player's staff leave unless renewed.
        /// </summary>
        public static void SeasonEnd(WorldState world, GameDatabase db, DeterministicRandom rng)
        {
            var ids = new List<string>(world.Staff.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                var s = world.Staff[id];
                if (s.TeamId == null) continue;
                var team = world.GetTeam(s.TeamId);
                if (team == null)
                {
                    s.TeamId = null;
                    continue;
                }
                int age = world.Season - s.BirthYear;
                s.Experience++;
                double growth = (s.Experience < 12 ? 1.0 : 0.2) - (age > 60 ? 1.5 : 0) + rng.Gaussian(0, 0.8);
                s.Skill = Math.Round(MathX.Clamp(s.Skill + growth, 15, 99), 1);
                s.Reputation = Math.Round(MathX.Clamp(s.Reputation * 0.85 + team.Reputation * 0.15, 5, 99));
                if (s.ContractEndSeason > world.Season) continue;
                if (team.IsPlayer)
                {
                    StaffMarket.Leave(world, team, s, Txt.F("staff.reason.ended", "contract ended"));
                    continue;
                }
                // AI: keep good people (on a fresh deal), let others go and hire a replacement.
                if (age < 66 && rng.Chance(0.7 + (s.Skill - 50) / 200.0)) StaffMarket.Extend(world, db, team, s, 2);
                else StaffMarket.Leave(world, team, s, Txt.F("staff.reason.ended", "contract ended"));
            }
            var teams = new List<Team>(world.Teams.Values);
            teams.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var t in teams)
            {
                var cat = db.GetCategory(t.CategoryId);
                if (t.IsPlayer || cat == null || cat.IsCup) continue;
                EnsureStaff(world, db, t, rng, cat.Prestige / 100.0);
            }
        }
    }

    /// <summary>Hiring, renewing and releasing personnel. Candidates for a role are stable for a few events.</summary>
    public static class StaffMarket
    {
        public static List<StaffMember> Candidates(WorldState world, GameDatabase db, Team team, StaffRole role, int count = 5)
        {
            int played = 0;
            foreach (var kv in world.NextRound) played += kv.Value;
            // New faces every few events, stable in between.
            var rng = new DeterministicRandom(DeterministicRandom.Combine(world.MasterSeed, DeterministicRandom.Hash(world.Season + "|" + team.Id + "|" + role + "|" + played / 4)));
            var list = new List<StaffMember>();
            for (int i = 0; i < count; i++)
            {
                double level = 35 + i * 12 + rng.Range(-5, 5);
                var s = StaffService.Generate(world, db, rng, role, null, level, StaffService.SalaryScale(world, db, team));
                s.Id = "cand_" + team.Id + "_" + role + "_" + i;
                s.ContractEndSeason = world.Season + 1;
                list.Add(s);
            }
            list.Sort((a, b) => b.Skill.CompareTo(a.Skill));
            return list;
        }

        /// <summary>Releasing early: the rest of this season's salary and half of the following seasons.</summary>
        public static double SeveranceCost(WorldState world, StaffMember s)
        {
            double future = Math.Max(0, s.ContractEndSeason - world.Season) * s.Salary * 0.5;
            return Math.Round(s.Salary * 0.5 + future);
        }

        public static void Fire(WorldState world, Team team, string staffId)
        {
            StaffMember s;
            if (!world.Staff.TryGetValue(staffId, out s) || s.TeamId != team.Id) return;
            Accounts.Post(world, team, LedgerKind.Severance, -SeveranceCost(world, s), s.FullName);
            Leave(world, team, s, Txt.F("staff.reason.released", "released"));
        }

        public static void Leave(WorldState world, Team team, StaffMember s, string reason)
        {
            team.StaffIds.Remove(s.Id);
            s.TeamId = null;
            s.AssignedRiderId = null;
            if (team.IsPlayer) world.AddNews(Txt.F("news.leaves", "{0} leaves {1}", s.FullName, team.Name), Txt.F("news.staffLeavesBody", "{0} ({1}).", Txt.Name("role." + s.Role, s.Role.ToString()), reason), team.CategoryId, true);
        }

        /// <summary>A new contract for someone already in the team, at today's market salary for him.</summary>
        public static void Extend(WorldState world, GameDatabase db, Team team, StaffMember s, int seasons)
        {
            s.ContractEndSeason = Math.Max(s.ContractEndSeason, world.Season) + Math.Max(1, seasons);
            s.Salary = Math.Round(StaffService.SalaryFor(s.Role, s.Skill, s.Experience) * StaffService.SalaryScale(world, db, team) / 1000) * 1000;
        }

        /// <summary>
        /// Hires a candidate on a two-season contract (signing fee 10% of a season). For roles held by one person the
        /// current holder is released; per-rider roles replace the weakest holder only when the crew is full.
        /// </summary>
        /// <summary>
        /// Who a new hire in this role would replace (the weakest holder) when the role is already full: one per
        /// rider for race engineers and mechanics, one for every other role. Null when there is room.
        /// </summary>
        public static StaffMember WouldReplace(WorldState world, Team team, StaffRole role)
        {
            var holders = new List<StaffMember>();
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (world.Staff.TryGetValue(id, out s) && s.Role == role) holders.Add(s);
            }
            int limit = StaffService.PerRider(role) ? Math.Max(1, team.RiderIds.Count) : 1;
            if (holders.Count < limit) return null;
            holders.Sort((a, b) =>
            {
                int c = a.Skill.CompareTo(b.Skill);
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });
            return holders[0];
        }

        /// <summary>Hires the candidate; if the role is full, the weakest holder is released (severance paid) and the new person takes over their rider.</summary>
        public static StaffMember Hire(WorldState world, Team team, StaffMember candidate, GameDatabase db = null)
        {
            var replaced = WouldReplace(world, team, candidate.Role);
            string rider = replaced != null ? replaced.AssignedRiderId : null;
            if (replaced != null) Fire(world, team, replaced.Id);
            var hired = new StaffMember
            {
                Id = world.NewId("staff"), FirstName = candidate.FirstName, LastName = candidate.LastName, Role = candidate.Role,
                Nationality = candidate.Nationality, BirthYear = candidate.BirthYear, Skill = candidate.Skill, Salary = candidate.Salary, TeamId = team.Id,
                Experience = candidate.Experience, Reputation = candidate.Reputation, ContractEndSeason = world.Season + 1, DataConfidence = candidate.DataConfidence
            };
            world.Staff[hired.Id] = hired;
            team.StaffIds.Add(hired.Id);
            if (rider != null && team.RiderIds.Contains(rider)) hired.AssignedRiderId = rider;
            StaffService.AssignToRiders(world, team);
            Accounts.Post(world, team, LedgerKind.StaffSigning, -Math.Round(hired.Salary * 0.1), hired.FullName);
            world.AddNews(Txt.F("news.joins", "{0} joins {1}", hired.FullName, team.Name), Txt.F("news.staffJoinsBody", "New {0}.", Txt.Name("role." + hired.Role, hired.Role.ToString())), team.CategoryId);
            return hired;
        }
    }
}
