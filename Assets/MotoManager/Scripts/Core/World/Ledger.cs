using System;
using System.Collections.Generic;
using Newtonsoft.Json;

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
        Licence,
        EntryFee,
        SlotPurchase,
        Equipment,
        BikePurchase,
        ManagerSalary,
        /// <summary>Paid to end a rider's contract early.</summary>
        RiderSeverance,
        /// <summary>Paid or received for a rider under contract (release clause or agreed fee).</summary>
        TransferFee,
        RiderSigningBonus,
        /// <summary>Capital the owner puts into the organisation (not income).</summary>
        OwnerInvestment,
        LoanDrawdown,
        LoanRepayment,
        Interest,
        /// <summary>Money received for selling a grid place.</summary>
        SlotSale,
        /// <summary>Money a rider's personal sponsors pay for his seat.</summary>
        RiderFunding,
        MerchSales,
        /// <summary>Cost of the units sold (not a cash movement: the cash left when the stock was bought).</summary>
        MerchCostOfSales,
        /// <summary>Buying or producing stock (cash out; it becomes inventory, not an expense).</summary>
        MerchStock,
        MerchOperations,
        MerchRoyalty,
        Other,
        /// <summary>Riders' share of the sales of products with their name (image rights).</summary>
        RiderImageRights,
        /// <summary>Marketing campaigns for the shop.</summary>
        MerchMarketing,
        /// <summary>A team sold to another organisation (price received or paid).</summary>
        TeamSale,
        /// <summary>Money the owner takes out of the organisation (when it is wound up).</summary>
        OwnerWithdrawal
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

    /// <summary>
    /// Running total per organisation, team, season and kind (kept for every organisation, current and previous
    /// season). Short property names keep saves small: there are many of these.
    /// </summary>
    [Serializable]
    public class AccountTotal
    {
        [JsonProperty("s")] public int Season;
        [JsonProperty("o")] public string OrganisationId;
        [JsonProperty("t")] public string TeamId;
        [JsonProperty("k")] public LedgerKind Kind;
        [JsonProperty("c")] public double Cash;
        [JsonProperty("p")] public double Pnl;
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
