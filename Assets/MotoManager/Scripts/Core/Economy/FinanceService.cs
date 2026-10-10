using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Economy
{
    /// <summary>
    /// What a team costs and earns on a regular basis. Built from a real team, or from the plan of a team that does
    /// not exist yet (founding, expansion), so payments and forecasts use exactly the same rules.
    /// </summary>
    public class CostProfile
    {
        public string CategoryId;
        public TeamStatus Status = TeamStatus.Independent;
        public int Riders = 2;
        public double RiderSalaries;
        public double StaffSalaries;
        /// <summary>Sum over facilities of level squared (upkeep grows with the square of the level).</summary>
        public double FacilityLevelSquares;
        /// <summary>Signed sponsorship per season.</summary>
        public double SponsorIncome;
        /// <summary>Money riders' backers pay per season.</summary>
        public double RiderFunding;

        public static CostProfile Of(WorldState world, Team team)
        {
            var p = new CostProfile
            {
                CategoryId = team.CategoryId,
                Status = team.Status,
                Riders = team.RiderIds.Count,
                RiderSalaries = FinanceService.RiderSalaries(world, team),
                StaffSalaries = FinanceService.StaffSalaries(world, team),
                SponsorIncome = FinanceService.SponsorIncomePerSeason(world, team),
                RiderFunding = FinanceService.RiderFunding(world, team)
            };
            foreach (FacilityType f in Enum.GetValues(typeof(FacilityType)))
            {
                int lvl = team.Facility(f);
                p.FacilityLevelSquares += lvl * lvl * TeamCapabilities.CostWeight(f);
            }
            return p;
        }
    }

    public class FinanceSummary
    {
        public double SponsorIncome;
        public double ParticipationIncome;
        public double RiderSalaries;
        public double StaffSalaries;
        public double FacilityUpkeep;
        public double BikeLease;
        public double Licences;
        public double Travel;
        public double Income { get { return SponsorIncome + ParticipationIncome; } }
        public double Expenses { get { return RiderSalaries + StaffSalaries + FacilityUpkeep + BikeLease + Licences + Travel; } }
        public double Balance { get { return Income - Expenses; } }
    }

    /// <summary>
    /// Regular income and costs of teams. Everything is posted through <see cref="Accounts"/>; the same item list
    /// feeds the per-event payments, the season forecast and the cash-flow projection.
    /// </summary>
    public static class FinanceService
    {
        /// <summary>Upkeep of one facility level step at the MotoGP reference (scaled per category).</summary>
        public const double FacilityUpkeepUnit = 40000;
        public const double FacilityUpgradeUnit = 400000;

        public static double SeasonSalaries(WorldState world, Team team)
        {
            return RiderSalaries(world, team) + StaffSalaries(world, team);
        }

        public static double RiderSalaries(WorldState world, Team team)
        {
            double total = 0;
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r != null && r.Contract != null && r.Contract.TeamId == team.Id) total += r.Contract.SalaryPerSeason;
            }
            return total;
        }

        public static double RiderFunding(WorldState world, Team team)
        {
            double total = 0;
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r != null && r.Contract != null && r.Contract.TeamId == team.Id) total += r.Contract.RiderFunding;
            }
            return total;
        }

        public static double StaffSalaries(WorldState world, Team team)
        {
            double total = 0;
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (world.Staff.TryGetValue(id, out s)) total += s.Salary;
            }
            return total;
        }

        /// <summary>Season payments of the deals that pay this season (conditional deals only once their condition holds).</summary>
        public static double SponsorIncomePerSeason(WorldState world, Team team)
        {
            double total = 0;
            foreach (var d in team.Sponsors)
                if (SponsorService.Pays(d, team, world.Season)) total += d.PaymentPerSeason;
            return total;
        }

        public static double FacilityUpkeep(CostProfile p, CategoryRules rules)
        {
            return FacilityUpkeepUnit * p.FacilityLevelSquares * rules.Economy.FacilityCostScale;
        }

        public static double BikeLease(CostProfile p, CategoryRules rules)
        {
            return p.Status == TeamStatus.Factory ? 0 : rules.Economy.BikeLeasePerSeason * p.Riders;
        }

        /// <summary>Regular items of a whole season (income positive, costs negative), excluding travel.</summary>
        public static List<KeyValuePair<LedgerKind, double>> SeasonItems(CostProfile p, CategoryRules rules)
        {
            return new List<KeyValuePair<LedgerKind, double>>
            {
                new KeyValuePair<LedgerKind, double>(LedgerKind.Sponsorship, p.SponsorIncome),
                new KeyValuePair<LedgerKind, double>(LedgerKind.RiderFunding, p.RiderFunding),
                new KeyValuePair<LedgerKind, double>(LedgerKind.ParticipationIncome, rules.Economy.ParticipationIncomePerSeason),
                new KeyValuePair<LedgerKind, double>(LedgerKind.RiderSalary, -p.RiderSalaries),
                new KeyValuePair<LedgerKind, double>(LedgerKind.StaffSalary, -p.StaffSalaries),
                new KeyValuePair<LedgerKind, double>(LedgerKind.FacilityUpkeep, -FacilityUpkeep(p, rules)),
                new KeyValuePair<LedgerKind, double>(LedgerKind.BikeLease, -BikeLease(p, rules)),
                new KeyValuePair<LedgerKind, double>(LedgerKind.Licence, -rules.Economy.LicenceFeePerSeason)
            };
        }

        /// <summary>Regular items paid at each event: the season items spread over the rounds, plus travel.</summary>
        public static List<KeyValuePair<LedgerKind, double>> RoundItems(CostProfile p, CategoryRules rules, int roundsInSeason)
        {
            double share = 1.0 / Math.Max(1, roundsInSeason);
            var list = new List<KeyValuePair<LedgerKind, double>>();
            foreach (var kv in SeasonItems(p, rules)) list.Add(new KeyValuePair<LedgerKind, double>(kv.Key, kv.Value * share));
            list.Add(new KeyValuePair<LedgerKind, double>(LedgerKind.Travel, -rules.Economy.TravelCostPerRound));
            return list;
        }

        /// <summary>Per-event cash flow for a team: its share of season income/costs plus event specific items.</summary>
        public static double ApplyRound(WorldState world, GameDatabase db, Team team, int roundsInSeason, CategoryRules rules, int crashes, double prize, double sponsorBonus)
        {
            var org = world.OrganisationOf(team);
            double before = org != null ? org.Cash : 0;
            foreach (var kv in RoundItems(CostProfile.Of(world, team), rules, roundsInSeason)) Accounts.Post(world, team, kv.Key, kv.Value);
            Accounts.Post(world, team, LedgerKind.PrizeMoney, prize);
            Accounts.Post(world, team, LedgerKind.SponsorBonus, sponsorBonus);
            Accounts.Post(world, team, LedgerKind.CrashRepair, -crashes * rules.Economy.CrashRepairCost);
            return (org != null ? org.Cash : 0) - before;
        }

        /// <summary>Prize money a team can expect per event when its riders finish around the given positions.</summary>
        public static double ExpectedPrizePerEvent(CategoryRules rules, double expectedPosition, int riders)
        {
            var table = rules.Economy.PrizeMoneyByPosition;
            if (table.Count == 0) return 0;
            double total = 0;
            for (int i = 0; i < riders; i++)
            {
                // Spread the riders a little around the expected position.
                int pos = (int)Math.Round(expectedPosition + (i - (riders - 1) / 2.0) * 2);
                if (pos >= 1 && pos <= table.Count) total += table[pos - 1];
            }
            return total;
        }

        /// <summary>
        /// Spending allocation for the season. It is the owners' policy, not a mirror of the costs: last season's
        /// allocation, nudged up when the team met its objective and down when it did not, never below the bare
        /// cost of attending the season. Spending above it is possible but shows as an overspend.
        /// </summary>
        public static double PlanSeasonBudget(WorldState world, GameDatabase db, Team team, int rounds)
        {
            var rules = db.GetRules(team.CategoryId, world.Season);
            double minimum = rules.Economy.TravelCostPerRound * rounds + rules.Economy.TypicalRiderSalary * Math.Max(1, team.RiderIds.Count);
            double trend = team.Personality == null ? 1.0 : team.Personality.LastObjectiveMet ? 1.03 : 0.96;
            return Math.Round(Math.Max(minimum, team.SeasonBudget * trend) / 1000) * 1000;
        }

        /// <summary>Expected season income and costs before race prizes, bonuses and crash damage.</summary>
        public static FinanceSummary SeasonForecast(WorldState world, GameDatabase db, Team team, int rounds)
        {
            return Summarise(CostProfile.Of(world, team), db.GetRules(team.CategoryId, world.Season), rounds);
        }

        public static FinanceSummary Summarise(CostProfile p, CategoryRules rules, int rounds)
        {
            var f = new FinanceSummary { Travel = rules.Economy.TravelCostPerRound * rounds };
            foreach (var kv in SeasonItems(p, rules))
            {
                switch (kv.Key)
                {
                    case LedgerKind.Sponsorship: f.SponsorIncome = kv.Value; break;
                    case LedgerKind.ParticipationIncome: f.ParticipationIncome = kv.Value; break;
                    case LedgerKind.RiderSalary: f.RiderSalaries = -kv.Value; break;
                    case LedgerKind.StaffSalary: f.StaffSalaries = -kv.Value; break;
                    case LedgerKind.FacilityUpkeep: f.FacilityUpkeep = -kv.Value; break;
                    case LedgerKind.BikeLease: f.BikeLease = -kv.Value; break;
                    case LedgerKind.Licence: f.Licences = -kv.Value; break;
                }
            }
            return f;
        }

        public static double FacilityUpgradeCost(WorldState world, GameDatabase db, Team team, FacilityType type)
        {
            int lvl = team.Facility(type);
            var rules = db.GetRules(team.CategoryId, world.Season);
            return Math.Round(FacilityUpgradeUnit * Math.Pow(lvl + 1, 2) * rules.Economy.FacilityCostScale * TeamCapabilities.CostWeight(type) / 1000) * 1000;
        }

        public static bool UpgradeFacility(WorldState world, GameDatabase db, Team team, FacilityType type)
        {
            int lvl = team.Facility(type);
            if (lvl >= 5) return false;
            double cost = FacilityUpgradeCost(world, db, team, type);
            if (Accounts.Cash(world, team) < cost) return false;
            Accounts.Post(world, team, LedgerKind.FacilityInvestment, -cost, type + " level " + (lvl + 1), true, false);
            team.Facilities[type] = lvl + 1;
            if (team.IsPlayer) world.AddNews(type + " upgraded to level " + (lvl + 1), "Investment of " + MathX.FormatMoney(cost) + ".", team.CategoryId);
            return true;
        }
    }
}
