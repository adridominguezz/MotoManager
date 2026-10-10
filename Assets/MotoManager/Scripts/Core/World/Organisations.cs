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
        public string Country;
        public double Budget = 10000000;
        public double Reputation = 50;
        public TeamColors Colors = new TeamColors();
        public Dictionary<FacilityType, int> Facilities = new Dictionary<FacilityType, int>();
        public List<string> StaffIds = new List<string>();
        public List<SponsorDeal> Sponsors = new List<SponsorDeal>();
        public bool IsPlayer;
        public string DataConfidence = "approximate";

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
