using System;
using System.Collections.Generic;
using MotoManager.Core.Data;

namespace MotoManager.Core.World
{
    /// <summary>A manufacturer is not a team: it can run several programmes (factory, satellite supply, WorldSBK, WorldSSP...).</summary>
    [Serializable]
    public class Manufacturer
    {
        public string Id;
        public string Name;
        public string Country;
        public double Reputation = 60;
        /// <summary>Yearly R&amp;D money across all programmes (euros).</summary>
        public double DevelopmentBudget = 20000000;
        /// <summary>
        /// 0-100: financial muscle of the group behind the racing department (motorcycle sales). Caps how much it
        /// can spend on racing and how hard it can respond to a crisis. Honda and Yamaha are the largest.
        /// </summary>
        public double FinancialStrength = 50;

        /// <summary>Most the group would spend on racing development per season.</summary>
        public double MaxDevelopmentBudget { get { return 15e6 + FinancialStrength * 0.65e6; } }

        /// <summary>0-100: how well the company turns money into lap time.</summary>
        public double EngineeringStrength = 60;
        /// <summary>categoryId -> base bike id the manufacturer supplies in that category.</summary>
        public Dictionary<string, string> Programs = new Dictionary<string, string>();
        public bool HasConcessions;
        /// <summary>categoryId -> development multiplier from the concession tier.</summary>
        public Dictionary<string, double> DevelopmentFactor = new Dictionary<string, double>();
        /// <summary>categoryId -> concession tier name.</summary>
        public Dictionary<string, string> ConcessionTier = new Dictionary<string, string>();

        public double FactorIn(string categoryId)
        {
            double f;
            return categoryId != null && DevelopmentFactor.TryGetValue(categoryId, out f) ? f : 1.0;
        }
        public string Color = "#CCCCCC";
    }

    /// <summary>Who owns an organisation (and therefore who funds it when money runs short).</summary>
    public enum OwnerKind
    {
        /// <summary>Private owners / investors behind an independent structure.</summary>
        Private,
        /// <summary>A manufacturer's racing division (works teams).</summary>
        Manufacturer,
        /// <summary>Owned by the player's manager.</summary>
        Player,
        /// <summary>Run by the championship organiser (one-make cups).</summary>
        Organiser
    }

    /// <summary>
    /// The company that owns one or more teams. It holds all the money (cash, later loans); teams only receive a
    /// season budget allocation to spend against. Every movement of money goes through <c>Accounts.Post</c>.
    /// </summary>
    [Serializable]
    public class Organisation
    {
        public string Id;
        public string Name;
        public OwnerKind Owner = OwnerKind.Private;
        /// <summary>For manufacturer-owned organisations.</summary>
        public string ManufacturerId;
        /// <summary>Manager who owns it (player-founded organisations).</summary>
        public string OwnerManagerId;
        public List<string> TeamIds = new List<string>();
        /// <summary>Liquidity. Only changed by ledger postings.</summary>
        public double Cash;
        /// <summary>Cash at the start of the current season (ledger check: Cash = opening + this season's cash postings).</summary>
        public double SeasonOpeningCash;
        /// <summary>0-100: how far the owners are willing to fund losses.</summary>
        public double OwnerBacking = 60;
        /// <summary>Keeps every ledger line (organisations the player works for or owns); others keep totals only.</summary>
        public bool DetailedLedger;
        public List<Loan> Loans = new List<Loan>();
        /// <summary>Seasons in a row the owners had to put in substantial money (AI organisations): their patience runs out.</summary>
        public int OwnerFundedSeasons;
        /// <summary>Events in a row the organisation has ended with negative cash (player organisations).</summary>
        public int InsolventEvents;
        /// <summary>Units of each product ordered this season by all the organisation's teams (shared production: volume discounts).</summary>
        public Dictionary<string, int> MerchOrdered = new Dictionary<string, int>();
        public string DataConfidence = "generated";
    }

    [Serializable]
    public class TeamColors
    {
        /// <summary>Number of bike livery templates (see the race view's BikeModel).</summary>
        public const int StyleCount = 8;
        /// <summary>Templates used when a team has not chosen one: the two-tone splits with an accent pinstripe.</summary>
        public const int AutoStyles = 2;

        /// <summary>Main colour: most of the fairing and the leathers.</summary>
        public string Primary = "#CC0000";
        /// <summary>Second colour: the lower or nose part of the livery, stripes, the leathers' arms.</summary>
        public string Secondary = "#FFFFFF";
        /// <summary>Accent colour: pinstripes and details. Empty (older saves, data teams): derived from the other two.</summary>
        public string Tertiary;
        /// <summary>Livery template, 0..StyleCount-1; -1 (older saves, data teams): chosen per team, see <see cref="StyleFor"/>.</summary>
        public int Style = -1;

        private static readonly string[] AccentCandidates = { "#15161A", "#F4F4F2", "#F28C28", "#E10600", "#F5C518", "#1E88E5", "#2DBE60" };

        /// <summary>The accent colour: the chosen one, or the candidate that stands out best against both team colours.</summary>
        public string TertiaryOrDefault()
        {
            if (!string.IsNullOrEmpty(Tertiary)) return Tertiary;
            double lp = Luminance(Primary, 0.2), ls = Luminance(Secondary, 1);
            string best = AccentCandidates[0];
            double bestScore = -1;
            foreach (var c in AccentCandidates)
            {
                double lc = Luminance(c, 0.5);
                double score = Math.Min(Contrast(lc, lp), Contrast(lc, ls));
                if (score > bestScore + 0.05) { bestScore = score; best = c; }
            }
            return best;
        }

        /// <summary>The livery template of a team: the chosen one, or a stable two-tone split for teams that never chose.</summary>
        public int StyleFor(string teamId)
        {
            if (Style >= 0 && Style < StyleCount) return Style;
            return (int)(Common.DeterministicRandom.Hash(teamId ?? "") % (ulong)AutoStyles);
        }

        private static double Contrast(double a, double b)
        {
            return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        }

        /// <summary>WCAG relative luminance of a "#RRGGBB" colour (<paramref name="fallback"/> when it cannot be read).</summary>
        public static double Luminance(string hex, double fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            string h = hex.TrimStart('#');
            if (h.Length < 6) return fallback;
            int rgb;
            if (!int.TryParse(h.Substring(0, 6), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out rgb)) return fallback;
            return 0.2126 * Channel((rgb >> 16) & 255) + 0.7152 * Channel((rgb >> 8) & 255) + 0.0722 * Channel(rgb & 255);
        }

        private static double Channel(int v)
        {
            double c = v / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }

    [Serializable]
    public class SponsorDeal
    {
        public string SponsorId;
        public string Name;
        public int Tier;
        public double PaymentPerSeason;
        public int StartSeason;
        public int EndSeason;
        public double Satisfaction = 60;
        public List<SponsorObjective> Objectives = new List<SponsorObjective>();
        public double BonusEarnedThisSeason;
        public string Industry;
        /// <summary>Conditional deal: pays nothing until the team races in this category.</summary>
        public string RequiredCategoryId;
        /// <summary>Cut applied to next season's payment when season objectives are missed.</summary>
        public double PenaltyOnMiss;
        public bool IndustryExclusive = true;
    }

    /// <summary>
    /// A sponsor's proposal to a team. Not money: nothing is counted until it is accepted, and conditional parts
    /// only once their condition is met.
    /// </summary>
    [Serializable]
    public class SponsorOffer
    {
        public string Id;
        public string SponsorId;
        public string TeamId;
        public string Name;
        public string Industry;
        public int Tier;
        public double PaymentPerSeason;
        public double SigningBonus;
        public int Seasons = 1;
        /// <summary>First season of the deal (next season for renewals and offers made at season end).</summary>
        public int StartSeason;
        public List<SponsorObjective> Objectives = new List<SponsorObjective>();
        public double PenaltyOnMiss;
        public bool IndustryExclusive = true;
        public string RequiredCategoryId;
        public bool Renewal;
        /// <summary>Last event (count of events completed by the world) at which it can be accepted.</summary>
        public int ExpiresAfterEvent;
        /// <summary>How many times the team has pushed for better terms (sponsors lose patience).</summary>
        public int Counters;
    }

    [Serializable]
    public class Team
    {
        public string Id;
        public string Name;
        public string ShortName;
        public string ManufacturerId;
        public string CategoryId;
        public TeamStatus Status = TeamStatus.Independent;
        /// <summary>The bike this team races (each team owns its own instance, cloned from the manufacturer spec).</summary>
        public string BikeId;
        public List<string> RiderIds = new List<string>();
        /// <summary>Number of riders this team fields (0 = category default). WorldSBK has single-rider teams.</summary>
        public int RiderSlots;
        public string Country;
        /// <summary>Organisation that owns the team (and its money).</summary>
        public string OrganisationId;
        /// <summary>Spending allocation for the current season. Not money: the cash belongs to the organisation.</summary>
        public double SeasonBudget = 10000000;
        public double Reputation = 50;
        public TeamColors Colors = new TeamColors();
        public Dictionary<FacilityType, int> Facilities = new Dictionary<FacilityType, int>();
        public List<string> StaffIds = new List<string>();
        public List<SponsorDeal> Sponsors = new List<SponsorDeal>();
        public bool IsPlayer;
        public string DataConfidence = "approximate";
        /// <summary>AI character (generated when missing from the data).</summary>
        public TeamPersonality Personality;
        /// <summary>The manager running the team (player or AI).</summary>
        public string ManagerId;
        /// <summary>Official shop and fan base.</summary>
        public TeamShop Shop = new TeamShop();
        /// <summary>Category the team moves to at the end of the season (null = stays), and its bike supplier there.</summary>
        public string PendingCategoryId;
        public string PendingSupplierId;

        public int Facility(FacilityType type)
        {
            int lvl;
            return Facilities.TryGetValue(type, out lvl) ? lvl : 1;
        }
    }

    [Serializable]
    public class Motorcycle
    {
        public string Id;
        public string ManufacturerId;
        public string CategoryId;
        public string BikeClassId;
        public string Model;
        public int Year = 2026;
        /// <summary>Component levels 0-100 (Engine, Chassis, Aerodynamics...). Names come from the bike class.</summary>
        public Dictionary<string, double> Components = new Dictionary<string, double>();
        /// <summary>For production-derived bikes: quality of the homologated road bike per attribute (0-100).</summary>
        public Dictionary<PerfAttr, double> ProductionBase = new Dictionary<PerfAttr, double>();
        /// <summary>Accumulated development this season per component (for season caps).</summary>
        public Dictionary<string, double> SeasonGains = new Dictionary<string, double>();

        public double Component(string name)
        {
            double v;
            return Components.TryGetValue(name, out v) ? v : 50;
        }

        public Motorcycle Clone(string newId)
        {
            var m = (Motorcycle)MemberwiseClone();
            m.Id = newId;
            m.Components = new Dictionary<string, double>(Components);
            m.ProductionBase = new Dictionary<PerfAttr, double>(ProductionBase);
            m.SeasonGains = new Dictionary<string, double>();
            return m;
        }
    }

    [Serializable]
    public class StaffMember
    {
        public string Id;
        public string FirstName;
        public string LastName;
        public StaffRole Role;
        public string Nationality;
        public int BirthYear;
        public double Skill = 55;
        public double Salary = 150000;
        public string TeamId;
        /// <summary>Years working in the paddock.</summary>
        public int Experience = 5;
        public double Reputation = 40;
        /// <summary>Last season of the current contract.</summary>
        public int ContractEndSeason;
        /// <summary>Race engineers and lead mechanics work with one rider.</summary>
        public string AssignedRiderId;
        public string DataConfidence = "generated";

        public string FullName { get { return FirstName + " " + LastName; } }
    }
}
