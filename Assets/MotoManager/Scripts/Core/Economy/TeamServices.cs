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

    /// <summary>Sponsor contracts: signing, objectives, bonuses and renewals.</summary>
    public static class SponsorService
    {
        public static void EnsureSponsors(WorldState world, GameDatabase db, Team team, ChampionshipCategory category, DeterministicRandom rng)
        {
            if (team.Sponsors.Count > 0 || category == null || category.IsCup) return;
            var home = new List<SponsorTemplate>();
            foreach (var s in db.Sponsors.Values)
                if (s.HomeTeams.Contains(team.Id) && !IsTaken(world, s.Id)) home.Add(s);
            home.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var s in home) Sign(world, team, s, category, rng);
            var options = Available(world, db, team, category);
            int slots = (category.Tier == 1 ? 3 : 2) - home.Count;
            for (int i = 0; i < slots && options.Count > 0; i++)
            {
                var pick = rng.PickWeighted(options, s => 1.0 / s.Tier);
                options.Remove(pick);
                Sign(world, team, pick, category, rng);
            }
        }

        private static bool IsTaken(WorldState world, string sponsorId)
        {
            foreach (var t in world.Teams.Values)
                foreach (var d in t.Sponsors)
                    if (d.SponsorId == sponsorId) return true;
            return false;
        }

        public static List<SponsorTemplate> Available(WorldState world, GameDatabase db, Team team, ChampionshipCategory category)
        {
            var taken = new HashSet<string>();
            foreach (var t in world.Teams.Values)
                foreach (var d in t.Sponsors) taken.Add(d.SponsorId);
            var list = new List<SponsorTemplate>();
            foreach (var s in db.Sponsors.Values)
            {
                if (taken.Contains(s.Id)) continue;
                if (s.HomeTeams.Count > 0 && !s.HomeTeams.Contains(team.Id) && world.ArchivedChampionships.Count == 0) continue;
                if (s.MinTeamReputation > team.Reputation) continue;
                if (s.Pyramids.Count > 0 && !s.Pyramids.Contains(category.Pyramid)) continue;
                list.Add(s);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        /// <summary>Category prestige scales what a sponsor is willing to pay.</summary>
        public static double ValueFor(SponsorTemplate s, Team team, ChampionshipCategory category)
        {
            double prestige = category != null ? category.Prestige / 100.0 : 0.5;
            return Math.Round(s.PaymentPerSeason * (0.25 + 0.75 * prestige) * (0.7 + team.Reputation / 160.0) / 1000) * 1000;
        }

        public static SponsorDeal Sign(WorldState world, Team team, SponsorTemplate s, ChampionshipCategory category, DeterministicRandom rng)
        {
            var deal = new SponsorDeal
            {
                SponsorId = s.Id,
                Name = s.Name,
                Tier = s.Tier,
                PaymentPerSeason = ValueFor(s, team, category),
                StartSeason = world.Season,
                EndSeason = world.Season + Math.Max(1, s.ContractSeasons) - 1 + (rng != null ? rng.NextInt(0, 2) : 0),
                Objectives = new List<SponsorObjective>(s.Objectives)
            };
            team.Sponsors.Add(deal);
            return deal;
        }

        /// <summary>Called after every race with the team's best result. Returns the bonus earned.</summary>
        public static double EvaluateRace(Team team, int bestPosition, int podiums, int wins, List<string> riderNationalities)
        {
            double total = 0;
            foreach (var deal in team.Sponsors)
            {
                foreach (var o in deal.Objectives)
                {
                    double bonus = 0;
                    switch (o.Type)
                    {
                        case SponsorObjectiveType.RaceFinishTopN:
                            if (bestPosition > 0 && bestPosition <= o.Target) bonus = o.BonusPerOccurrence;
                            break;
                        case SponsorObjectiveType.Podiums:
                            bonus = o.BonusPerOccurrence * podiums;
                            break;
                        case SponsorObjectiveType.Wins:
                            bonus = o.BonusPerOccurrence * wins;
                            break;
                    }
                    if (bonus > 0)
                    {
                        deal.BonusEarnedThisSeason += bonus;
                        deal.Satisfaction = Math.Min(100, deal.Satisfaction + 2);
                        total += bonus;
                    }
                    else if (o.Type == SponsorObjectiveType.RaceFinishTopN)
                    {
                        deal.Satisfaction = Math.Max(0, deal.Satisfaction - 1);
                    }
                }
            }
            return total;
        }

        /// <summary>Season end: season-long objectives, renewals and replacements.</summary>
        public static double EndOfSeason(WorldState world, GameDatabase db, Team team, ChampionshipCategory category, double points, int championshipPosition, DeterministicRandom rng)
        {
            double total = 0;
            for (int i = team.Sponsors.Count - 1; i >= 0; i--)
            {
                var deal = team.Sponsors[i];
                foreach (var o in deal.Objectives)
                {
                    bool met = false;
                    if (o.Type == SponsorObjectiveType.SeasonPoints) met = points >= o.Target;
                    if (o.Type == SponsorObjectiveType.ChampionshipPositionTopN) met = championshipPosition > 0 && championshipPosition <= o.Target;
                    if (met)
                    {
                        total += o.SeasonBonus;
                        deal.Satisfaction = Math.Min(100, deal.Satisfaction + 10);
                    }
                    else if (o.Type == SponsorObjectiveType.SeasonPoints || o.Type == SponsorObjectiveType.ChampionshipPositionTopN)
                    {
                        deal.Satisfaction = Math.Max(0, deal.Satisfaction - 12);
                    }
                }
                deal.BonusEarnedThisSeason = 0;
                if (deal.EndSeason <= world.Season)
                {
                    bool renew = rng.Chance(MathX.Clamp01(deal.Satisfaction / 100.0 + 0.1));
                    if (renew)
                    {
                        deal.EndSeason = world.Season + 1 + rng.NextInt(0, 2);
                        deal.PaymentPerSeason *= 0.9 + deal.Satisfaction / 250.0;
                        deal.Satisfaction = 60;
                    }
                    else
                    {
                        team.Sponsors.RemoveAt(i);
                        if (team.IsPlayer) world.AddNews(deal.Name + " ends its sponsorship", "The partnership with " + team.Name + " will not continue next season.", team.CategoryId, true);
                    }
                }
            }
            if (team.Sponsors.Count == 0) EnsureSponsors(world, db, team, category, rng);
            return total;
        }
    }
}
