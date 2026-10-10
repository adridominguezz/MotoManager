using System;
using System.Collections.Generic;

namespace MotoManager.Core.World
{
    public enum LedgerKind
    {
        Sponsorship,
        SponsorBonus,
        PrizeMoney,
        ParticipationIncome,
        OwnerFunding,
        RiderSalary,
        RiderBonus,
        StaffSalary,
        StaffSigning,
        Severance,
        FacilityUpkeep,
        FacilityInvestment,
        BikeLease,
        Travel,
        CrashRepair,
        Development,
        TechnicalInvestment,
        Other
    }

    /// <summary>One movement of money. Cash entries change the organisation's cash; P&amp;L entries count as income/expense.</summary>
    [Serializable]
    public class LedgerEntry
    {
        public DateTime Date;
        public int Season;
        public string OrganisationId;
        public string TeamId;
        public LedgerKind Kind;
        public double Amount;
        public bool Cash = true;
        public bool Pnl = true;
        public string Note;
    }

    /// <summary>Running total per organisation, team, season and kind (kept for every organisation).</summary>
    [Serializable]
    public class AccountTotal
    {
        public int Season;
        public string OrganisationId;
        public string TeamId;
        public LedgerKind Kind;
        public double Cash;
        public double Pnl;
    }

    /// <summary>Compact yearly result of an organisation, kept forever.</summary>
    [Serializable]
    public class OrganisationSeasonSummary
    {
        public int Season;
        public string OrganisationId;
        public double OpeningCash;
        public double ClosingCash;
        public double Income;
        public double Expenses;
        public double OwnerFunding;
    }

    [Serializable]
    public class FinanceState
    {
        /// <summary>Detailed lines for organisations with a detailed ledger (current and previous season).</summary>
        public List<LedgerEntry> Entries = new List<LedgerEntry>();
        /// <summary>Totals for every organisation (last few seasons).</summary>
        public List<AccountTotal> Totals = new List<AccountTotal>();
        public List<OrganisationSeasonSummary> Summaries = new List<OrganisationSeasonSummary>();
        /// <summary>
        /// Keys of operations already applied ("season|..."): replaying an event or an action never posts twice.
        /// </summary>
        public HashSet<string> AppliedKeys = new HashSet<string>();
    }
}
