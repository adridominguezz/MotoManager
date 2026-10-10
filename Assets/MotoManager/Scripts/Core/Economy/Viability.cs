using System;
using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Economy
{
    public enum Viability
    {
        /// <summary>Cash stays above the safety reserve all season counting only confirmed income.</summary>
        Viable,
        /// <summary>Only works if probable income (prize money, sponsors still to sign) arrives.</summary>
        Risky,
        /// <summary>Runs out of money even with probable income, or cannot pay the start-up.</summary>
        Unviable,
        /// <summary>The category cannot be entered by a new team (organiser-run cup, full grid).</summary>
        NotAvailable
    }

    public enum ViabilityReason
    {
        OrganiserOnly,
        GridFull,
        CapitalBelowStartup,
        BelowReserveConfirmed,
        CashNegativeConfirmed,
        CashNegativeProbable
    }

    /// <summary>What a new team in a category would cost and whether the money holds for a full season.</summary>
    public class ViabilityReport
    {
        public string CategoryId;
        public Viability Verdict;
        public readonly List<ViabilityReason> Reasons = new List<ViabilityReason>();
        public int Riders;
        public double EntryCost;
        public double Equipment;
        public double BikePurchase;
        public double StartupTotal { get { return EntryCost + Equipment + BikePurchase; } }
        /// <summary>Regular costs of a full season (salaries, staff, upkeep, lease, licences, travel, expected crashes).</summary>
        public double OperatingCost;
        public double ConfirmedIncome;
        public double ProbableIncome;
        public double Reserve;
        public double Capital;
        public double LiquidityAfterStartup { get { return Capital - StartupTotal; } }
        public double MinCashConfirmed;
        public double MinCashProbable;
        public double EndCashConfirmed;
        public double EndCashProbable;
        /// <summary>Extra money needed to become viable (0 when it already is).</summary>
        public double Shortfall;
        public double ExpectedPosition;
        public CostProfile Profile;
        public CashflowForecast Forecast;
    }

    /// <summary>Inputs of a founding or expansion decision.</summary>
    public class TeamPlan
    {
        public string CategoryId;
        /// <summary>Money available for the project (manager's wealth, an existing organisation's cash, loans).</summary>
        public double Capital;
        /// <summary>Reputation the new team starts with (drives what sponsors would pay).</summary>
        public double Reputation = 25;
        /// <summary>Sponsorship already signed for the new team (confirmed income).</summary>
        public double ConfirmedSponsorIncome;
        /// <summary>Organisation that would own the new team (expansion): its other teams' costs are included.</summary>
        public Organisation Organisation;
        /// <summary>Loan included in <see cref="Capital"/>: its instalments and interest are costs of the plan.</summary>
        public double LoanAmount;
        public double LoanRate;
        public int LoanSeasons = 4;
    }

    /// <summary>
    /// Can a new team in a category be started and kept going for a season? Uses the category's economy data and
    /// the same payment rules as running teams. No fixed money thresholds and no manager-level gates.
    /// </summary>
    public static class ViabilityService
    {
        /// <summary>Share of the likely sponsor market a new team counts as probable (never as confirmed).</summary>
        public const double ProbableSponsorShare = 0.5;

        public static ViabilityReport Evaluate(WorldState world, GameDatabase db, TeamPlan plan)
        {
            var cat = db.GetCategory(plan.CategoryId);
            var rules = db.GetRules(plan.CategoryId, world.Season);
            var eco = rules.Economy;
            var rep = new ViabilityReport { CategoryId = plan.CategoryId, Capital = plan.Capital, Riders = rules.RidersPerTeam };

            if (cat == null || !cat.Simulated || eco.EntryModel == EntryModel.OrganiserOnly)
            {
                rep.Verdict = Viability.NotAvailable;
                rep.Reasons.Add(ViabilityReason.OrganiserOnly);
                return rep;
            }
            if (eco.EntryModel == EntryModel.Open && EntriesInCategory(world, cat.Id) + rep.Riders > rules.MaxEntries)
            {
                rep.Verdict = Viability.NotAvailable;
                rep.Reasons.Add(ViabilityReason.GridFull);
                return rep;
            }

            rep.EntryCost = eco.EntryModel == EntryModel.SlotPurchase ? eco.SlotPrice : eco.EntryFee;
            rep.Equipment = eco.StartupEquipmentCost;
            rep.BikePurchase = eco.BikePurchasePerRider * rep.Riders;

            var profile = NewTeamProfile(db, cat, rules);
            profile.SponsorIncome = plan.ConfirmedSponsorIncome;
            rep.Profile = profile;
            var rounds = CashflowForecast.AllRounds(world, db, cat.Id);
            int n = Math.Max(1, rounds.Count);
            rep.ExpectedPosition = Math.Max(1, rules.GridSize * 0.75);
            double probableSponsors = Math.Max(0, SponsorMarketEstimate(world, db, cat, plan.Reputation) - plan.ConfirmedSponsorIncome);

            var flows = new List<CashflowForecast.Flow>();
            DateTime start = rounds.Count > 0 ? rounds[0].Date.AddDays(-30) : new DateTime(world.Season, 2, 1);
            flows.Add(new CashflowForecast.Flow { Date = start, Label = "Start-up", Cost = rep.StartupTotal });
            var teamFlows = CashflowForecast.TeamFlows(profile, rules, rounds, n, rep.ExpectedPosition, probableSponsors, cat.ShortName);
            foreach (var f in teamFlows) rep.OperatingCost += f.Cost;
            flows.AddRange(teamFlows);
            if (plan.LoanAmount > 0)
            {
                // Instalments and interest (interest on the outstanding amount, approximated as straight-line).
                double perRound = plan.LoanAmount / (Math.Max(1, plan.LoanSeasons) * n);
                double outstanding = plan.LoanAmount;
                foreach (var round in rounds)
                {
                    double interest = outstanding * plan.LoanRate / n;
                    flows.Add(new CashflowForecast.Flow { Date = round.Date, Label = "Loan", Cost = perRound + interest });
                    outstanding -= perRound;
                }
            }

            // Expansion: the organisation's existing teams keep costing and earning over the same season.
            if (plan.Organisation != null)
            {
                foreach (var id in plan.Organisation.TeamIds)
                {
                    var t = world.GetTeam(id);
                    var c = t != null ? db.GetCategory(t.CategoryId) : null;
                    if (c == null || !c.Simulated) continue;
                    var r = db.GetRules(c.Id, world.Season);
                    var all = CashflowForecast.AllRounds(world, db, c.Id);
                    flows.AddRange(CashflowForecast.TeamFlows(CostProfile.Of(world, t), r, all, Math.Max(1, all.Count),
                        CashflowForecast.ExpectedPosition(world, t), CashflowForecast.ExpectedSponsorBonuses(t), t.ShortName ?? t.Name));
                }
            }

            var forecast = CashflowForecast.Build(plan.Capital, flows);
            rep.Forecast = forecast;
            rep.ConfirmedIncome = forecast.ConfirmedIncome;
            rep.ProbableIncome = forecast.ProbableIncome;
            rep.MinCashConfirmed = forecast.MinConfirmed;
            rep.MinCashProbable = forecast.MinProbable;
            rep.EndCashConfirmed = forecast.EndConfirmed;
            rep.EndCashProbable = forecast.EndProbable;
            rep.Reserve = Math.Round(rep.OperatingCost * eco.SafetyReserveFraction);
            rep.Shortfall = Math.Max(0, rep.Reserve - rep.MinCashConfirmed);

            if (plan.Capital < rep.StartupTotal) rep.Reasons.Add(ViabilityReason.CapitalBelowStartup);
            if (rep.MinCashConfirmed < rep.Reserve) rep.Reasons.Add(rep.MinCashConfirmed < 0 ? ViabilityReason.CashNegativeConfirmed : ViabilityReason.BelowReserveConfirmed);
            if (rep.MinCashProbable < 0) rep.Reasons.Add(ViabilityReason.CashNegativeProbable);

            if (plan.Capital < rep.StartupTotal || rep.MinCashProbable < 0) rep.Verdict = Viability.Unviable;
            else if (rep.MinCashConfirmed >= rep.Reserve) rep.Verdict = Viability.Viable;
            else rep.Verdict = Viability.Risky;
            return rep;
        }

        /// <summary>Every simulated category, best options first.</summary>
        public static List<ViabilityReport> EvaluateAll(WorldState world, GameDatabase db, double capital, double reputation, Organisation organisation = null)
        {
            var list = new List<ViabilityReport>();
            foreach (var cat in db.CategoriesOrdered())
            {
                if (!cat.Simulated) continue;
                list.Add(Evaluate(world, db, new TeamPlan { CategoryId = cat.Id, Capital = capital, Reputation = reputation, Organisation = organisation }));
            }
            return list;
        }

        /// <summary>Running costs of a new, modest team: riders at a fraction of the typical salary, a basic crew, level-1 facilities.</summary>
        public static CostProfile NewTeamProfile(GameDatabase db, ChampionshipCategory cat, CategoryRules rules)
        {
            var p = new CostProfile { CategoryId = cat.Id, Status = TeamStatus.Independent, Riders = rules.RidersPerTeam };
            p.RiderSalaries = rules.Economy.TypicalRiderSalary * 0.6 * p.Riders;
            var roles = new List<StaffRole> { StaffRole.TeamManager, StaffRole.ChiefEngineer, StaffRole.DataEngineer, StaffRole.TireEngineer };
            for (int i = 0; i < p.Riders; i++) roles.Add(StaffRole.CrewChief);
            foreach (var role in roles) p.StaffSalaries += StaffService.SalaryFor(role, 45) * rules.Economy.StaffSalaryScale;
            p.FacilityLevelSquares = Enum.GetValues(typeof(FacilityType)).Length;
            return p;
        }

        /// <summary>What sponsors still free in the market would likely pay a team of this reputation (two best deals, half counted).</summary>
        public static double SponsorMarketEstimate(WorldState world, GameDatabase db, ChampionshipCategory cat, double reputation)
        {
            var probe = new Team { Id = "_new", CategoryId = cat.Id, Reputation = reputation };
            var engaged = SponsorService.Commitments(world);
            var values = new List<double>();
            foreach (var s in SponsorService.AllTemplates(world, db))
                if (engaged.CanApproach(s, probe.Id) && SponsorService.Interest(world, db, s, probe, cat) > 0.3) values.Add(SponsorService.ValueFor(s, probe, cat));
            values.Sort((a, b) => b.CompareTo(a));
            double total = 0;
            for (int i = 0; i < values.Count && i < 2; i++) total += values[i];
            return total * ProbableSponsorShare;
        }

        private static int EntriesInCategory(WorldState world, string categoryId)
        {
            int n = 0;
            foreach (var t in world.TeamsInCategory(categoryId)) n += t.RiderIds.Count;
            return n;
        }
    }
}
