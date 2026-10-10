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
        /// <summary>0-100: how well the company turns money into lap time.</summary>
        public double EngineeringStrength = 60;
        /// <summary>categoryId -> base bike id the manufacturer supplies in that category.</summary>
        public Dictionary<string, string> Programs = new Dictionary<string, string>();
        public bool HasConcessions;
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
        public string DataConfidence = "generated";
    }

    [Serializable]
    public class TeamColors
    {
        public string Primary = "#CC0000";
        public string Secondary = "#FFFFFF";
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

        public string FullName { get { return FirstName + " " + LastName; } }
    }
}
