using System;
using System.Collections.Generic;
using MotoManager.Core.World;

namespace MotoManager.Core.Data
{
    /// <summary>
    /// Static content loaded from the data folders. Entities that change during a career (riders, teams,
    /// manufacturers, bikes) are only the initial 2026 snapshot here; the live versions are in <see cref="WorldState"/>.
    /// </summary>
    public class GameDatabase
    {
        public readonly Dictionary<string, ChampionshipDefinition> Championships = new Dictionary<string, ChampionshipDefinition>();
        public readonly Dictionary<string, ChampionshipCategory> Categories = new Dictionary<string, ChampionshipCategory>();
        public readonly Dictionary<string, CategoryRules> Rules = new Dictionary<string, CategoryRules>();
        public readonly Dictionary<string, BikeClassDefinition> BikeClasses = new Dictionary<string, BikeClassDefinition>();
        public readonly Dictionary<string, CircuitDefinition> Circuits = new Dictionary<string, CircuitDefinition>();
        public readonly Dictionary<string, CalendarDefinition> Calendars = new Dictionary<string, CalendarDefinition>();
        public readonly Dictionary<string, Manufacturer> Manufacturers = new Dictionary<string, Manufacturer>();
        public readonly Dictionary<string, Motorcycle> Motorcycles = new Dictionary<string, Motorcycle>();
        public readonly Dictionary<string, Team> Teams = new Dictionary<string, Team>();
        public readonly Dictionary<string, Rider> Riders = new Dictionary<string, Rider>();
        public readonly List<ContractDefinition> Contracts = new List<ContractDefinition>();
        public readonly Dictionary<string, StaffTemplate> Staff = new Dictionary<string, StaffTemplate>();
        public readonly Dictionary<string, SponsorTemplate> Sponsors = new Dictionary<string, SponsorTemplate>();
        public readonly Dictionary<string, NamePool> NamePools = new Dictionary<string, NamePool>();
        public readonly Dictionary<string, DevelopmentTemplate> DevelopmentTemplates = new Dictionary<string, DevelopmentTemplate>();
        public SimulationTuning Tuning = new SimulationTuning();

        public CategoryRules GetRules(string categoryId, int season)
        {
            ChampionshipCategory cat;
            if (!Categories.TryGetValue(categoryId, out cat)) throw new KeyNotFoundException("Unknown category " + categoryId);
            string rulesId = cat.GetRulesId(season);
            CategoryRules rules;
            if (rulesId == null || !Rules.TryGetValue(rulesId, out rules))
                throw new KeyNotFoundException("No rules for " + categoryId + " season " + season);
            return rules;
        }

        public ChampionshipCategory GetCategory(string id)
        {
            ChampionshipCategory c;
            return id != null && Categories.TryGetValue(id, out c) ? c : null;
        }

        public BikeClassDefinition GetBikeClass(string id)
        {
            BikeClassDefinition c;
            return id != null && BikeClasses.TryGetValue(id, out c) ? c : null;
        }

        public CircuitDefinition GetCircuit(string id)
        {
            CircuitDefinition c;
            return id != null && Circuits.TryGetValue(id, out c) ? c : null;
        }

        public List<ChampionshipCategory> CategoriesOrdered()
        {
            var list = new List<ChampionshipCategory>(Categories.Values);
            list.Sort((a, b) =>
            {
                int c = a.Pyramid.CompareTo(b.Pyramid);
                if (c != 0) return c;
                c = a.Tier.CompareTo(b.Tier);
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });
            return list;
        }

        /// <summary>Checks cross references so that broken data is reported at load time, not mid-season.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            foreach (var c in Categories.Values)
            {
                foreach (var kv in c.RulesBySeason)
                    if (!Rules.ContainsKey(kv.Value)) errors.Add("Category " + c.Id + " references missing rules " + kv.Value);
                if (c.Simulated && !Championships.ContainsKey(c.CalendarGroup ?? ""))
                    errors.Add("Category " + c.Id + " references missing championship " + c.CalendarGroup);
            }
            foreach (var r in Rules.Values)
            {
                if (!BikeClasses.ContainsKey(r.BikeClassId ?? "")) errors.Add("Rules " + r.Id + " references missing bike class " + r.BikeClassId);
                if (r.Tires.Compounds.Count == 0) errors.Add("Rules " + r.Id + " has no tire compounds");
            }
            foreach (var cal in Calendars.Values)
                foreach (var round in cal.Rounds)
                {
                    if (!Circuits.ContainsKey(round.CircuitId ?? "")) errors.Add("Calendar " + cal.Id + " round " + round.Round + " references missing circuit " + round.CircuitId);
                    foreach (var catId in round.Categories)
                        if (!Categories.ContainsKey(catId)) errors.Add("Calendar " + cal.Id + " references missing category " + catId);
                }
            foreach (var m in Motorcycles.Values)
            {
                if (!Manufacturers.ContainsKey(m.ManufacturerId ?? "")) errors.Add("Motorcycle " + m.Id + " references missing manufacturer " + m.ManufacturerId);
                if (!BikeClasses.ContainsKey(m.BikeClassId ?? "")) errors.Add("Motorcycle " + m.Id + " references missing bike class " + m.BikeClassId);
            }
            foreach (var t in Teams.Values)
            {
                if (!Categories.ContainsKey(t.CategoryId ?? "")) errors.Add("Team " + t.Id + " references missing category " + t.CategoryId);
                if (!Motorcycles.ContainsKey(t.BikeId ?? "")) errors.Add("Team " + t.Id + " references missing motorcycle " + t.BikeId);
                if (t.ManufacturerId != null && !Manufacturers.ContainsKey(t.ManufacturerId)) errors.Add("Team " + t.Id + " references missing manufacturer " + t.ManufacturerId);
            }
            foreach (var r in Riders.Values)
            {
                if (r.TeamId != null && !Teams.ContainsKey(r.TeamId)) errors.Add("Rider " + r.Id + " references missing team " + r.TeamId);
                if (r.CategoryId != null && !Categories.ContainsKey(r.CategoryId)) errors.Add("Rider " + r.Id + " references missing category " + r.CategoryId);
            }
            foreach (var c in Contracts)
            {
                if (!Riders.ContainsKey(c.RiderId ?? "")) errors.Add("Contract references missing rider " + c.RiderId);
                if (!Teams.ContainsKey(c.TeamId ?? "")) errors.Add("Contract references missing team " + c.TeamId);
            }
            foreach (var circuit in Circuits.Values)
            {
                try
                {
                    Race.TrackModel.Build(circuit);
                }
                catch (Exception e)
                {
                    errors.Add("Circuit " + circuit.Id + ": " + e.Message);
                }
            }
            return errors;
        }
    }

    [Serializable]
    public class ChampionshipDefinition
    {
        public string Id;
        public string Name;
        public Pyramid Pyramid;
        public List<string> Categories = new List<string>();
    }

    [Serializable]
    public class ContractDefinition
    {
        public string RiderId;
        public string TeamId;
        public string ManufacturerId;
        public int StartSeason;
        public int EndSeason;
        public double SalaryPerSeason;
        public bool FactoryBacked;
        public string DataConfidence = "approximate";
    }
}
