using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Economy
{
    /// <summary>Generates and evaluates team personnel.</summary>
    public static class StaffService
    {
        public static StaffMember Generate(WorldState world, GameDatabase db, DeterministicRandom rng, StaffRole role, string teamId, double level, double salaryScale = 1.0)
        {
            var pools = new List<NamePool>(db.NamePools.Values);
            pools.Sort((a, b) => string.CompareOrdinal(a.Nationality, b.Nationality));
            var pool = pools.Count > 0 ? rng.PickWeighted(pools, p => p.WeightGrandPrix + p.WeightSuperbike) : null;
            double skill = MathX.Clamp(level + rng.Gaussian(0, 8), 20, 98);
            return new StaffMember
            {
                Id = world.NewId("staff"),
                FirstName = pool != null && pool.FirstNames.Count > 0 ? rng.Pick(pool.FirstNames) : "Sam",
                LastName = pool != null && pool.LastNames.Count > 0 ? rng.Pick(pool.LastNames) : "Engineer",
                Nationality = pool != null ? pool.Nationality : "IT",
                Role = role,
                BirthYear = world.Season - rng.NextInt(28, 62),
                Skill = skill,
                Salary = Math.Round(SalaryFor(role, skill) * salaryScale / 1000) * 1000,
                TeamId = teamId
            };
        }

        public static double SalaryFor(StaffRole role, double skill)
        {
            double baseSalary;
            switch (role)
            {
                case StaffRole.TeamManager: baseSalary = 250000; break;
                case StaffRole.ChiefEngineer: baseSalary = 320000; break;
                case StaffRole.CrewChief: baseSalary = 220000; break;
                case StaffRole.Designer: baseSalary = 200000; break;
                case StaffRole.Aerodynamicist: baseSalary = 210000; break;
                case StaffRole.DataEngineer: baseSalary = 140000; break;
                case StaffRole.TireEngineer: baseSalary = 120000; break;
                case StaffRole.Scout: baseSalary = 90000; break;
                default: baseSalary = 70000; break;
            }
            return Math.Round(baseSalary * (0.4 + Math.Pow(skill / 70.0, 2.2)) / 1000) * 1000;
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

        public static void EnsureStaff(WorldState world, GameDatabase db, Team team, DeterministicRandom rng, double scale)
        {
            var have = new HashSet<StaffRole>();
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (world.Staff.TryGetValue(id, out s)) have.Add(s.Role);
            }
            double level = 35 + team.Reputation * 0.45 * scale;
            double pay = SalaryScale(world, db, team);
            var needed = new List<StaffRole> { StaffRole.TeamManager, StaffRole.ChiefEngineer, StaffRole.DataEngineer, StaffRole.TireEngineer, StaffRole.Scout };
            foreach (var role in needed)
            {
                if (have.Contains(role)) continue;
                var s = Generate(world, db, rng, role, team.Id, level, pay);
                world.Staff[s.Id] = s;
                team.StaffIds.Add(s.Id);
            }
            int crewChiefs = 0;
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (world.Staff.TryGetValue(id, out s) && s.Role == StaffRole.CrewChief) crewChiefs++;
            }
            int wanted = Math.Max(1, Math.Min(team.RiderIds.Count, 4));
            for (int i = crewChiefs; i < wanted; i++)
            {
                var s = Generate(world, db, rng, StaffRole.CrewChief, team.Id, level, pay);
                world.Staff[s.Id] = s;
                team.StaffIds.Add(s.Id);
            }
        }
    }

    /// <summary>Hiring and firing personnel. Candidates for a role are stable within a season.</summary>
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
                list.Add(s);
            }
            list.Sort((a, b) => b.Skill.CompareTo(a.Skill));
            return list;
        }

        /// <summary>Severance: half of a season's salary.</summary>
        public static double SeveranceCost(StaffMember s)
        {
            return Math.Round(s.Salary * 0.5);
        }

        public static void Fire(WorldState world, Team team, string staffId)
        {
            StaffMember s;
            if (!world.Staff.TryGetValue(staffId, out s) || s.TeamId != team.Id) return;
            Accounts.Post(world, team, LedgerKind.Severance, -SeveranceCost(s), s.FullName);
            team.StaffIds.Remove(staffId);
            s.TeamId = null;
            world.AddNews(s.FullName + " leaves " + team.Name, s.Role + " released.", team.CategoryId);
        }

        /// <summary>Hires a candidate; for single-holder roles the current holder is released.</summary>
        public static StaffMember Hire(WorldState world, Team team, StaffMember candidate)
        {
            if (candidate.Role != StaffRole.CrewChief && candidate.Role != StaffRole.Mechanic)
            {
                foreach (var id in new List<string>(team.StaffIds))
                {
                    StaffMember s;
                    if (world.Staff.TryGetValue(id, out s) && s.Role == candidate.Role) Fire(world, team, id);
                }
            }
            var hired = new StaffMember
            {
                Id = world.NewId("staff"), FirstName = candidate.FirstName, LastName = candidate.LastName, Role = candidate.Role,
                Nationality = candidate.Nationality, BirthYear = candidate.BirthYear, Skill = candidate.Skill, Salary = candidate.Salary, TeamId = team.Id
            };
            world.Staff[hired.Id] = hired;
            team.StaffIds.Add(hired.Id);
            Accounts.Post(world, team, LedgerKind.StaffSigning, -Math.Round(hired.Salary * 0.1), hired.FullName);
            world.AddNews(hired.FullName + " joins " + team.Name, "New " + hired.Role + ".", team.CategoryId);
            return hired;
        }
    }
}
