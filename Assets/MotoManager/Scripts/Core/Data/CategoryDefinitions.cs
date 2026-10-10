using System;
using System.Collections.Generic;

namespace MotoManager.Core.Data
{
    /// <summary>
    /// A championship category (MotoGP, Moto2, WorldSBK, ...). Pure data: the category says where it sits in its
    /// pyramid and which rule set applies. All behaviour lives in <see cref="CategoryRules"/>.
    /// </summary>
    [Serializable]
    public class ChampionshipCategory
    {
        public string Id;
        public string Name;
        public string ShortName;
        public Pyramid Pyramid;
        /// <summary>1 = top of the pyramid.</summary>
        public int Tier;
        /// <summary>Rules id per season. The most recent season &lt;= requested season is used.</summary>
        public Dictionary<int, string> RulesBySeason = new Dictionary<int, string>();
        /// <summary>Calendar ids (by season) this category races on.</summary>
        public string CalendarGroup;
        /// <summary>Categories whose riders naturally move up to this one (used as hints by the market, never as hard rules).</summary>
        public List<string> FeederCategories = new List<string>();
        /// <summary>False for talent pools (national / junior series) that exist in the world but are not raced on screen.</summary>
        public bool Simulated = true;
        /// <summary>Youth cups run one bike spec for everybody and riders are not tied to real teams.</summary>
        public bool IsCup;
        public int MinAge = 16;
        public int MaxEntryAge = 99;
        public int MaxAge = 99;
        /// <summary>Market prestige 0-100. Drives rider preference.</summary>
        public double Prestige = 50;
        public string Color = "#FFFFFF";

        public string GetRulesId(int season)
        {
            string best = null;
            int bestSeason = int.MinValue;
            foreach (var kv in RulesBySeason)
            {
                if (kv.Key <= season && kv.Key > bestSeason)
                {
                    best = kv.Value;
                    bestSeason = kv.Key;
                }
            }
            if (best == null)
            {
                foreach (var kv in RulesBySeason)
                {
                    if (best == null || kv.Key < bestSeason)
                    {
                        best = kv.Value;
                        bestSeason = kv.Key;
                    }
                }
            }
            return best;
        }
    }

    /// <summary>
    /// Everything the simulation needs to know about a category's regulations. The race engine only ever
    /// queries these values; it never branches on the category id.
    /// </summary>
    [Serializable]
    public class CategoryRules
    {
        public string Id;
        public string CategoryId;
        public int Season;
        public string BikeClassId;

        public EngineRules Engine = new EngineRules();
        public ChassisRules Chassis = new ChassisRules();
        public PhysicsBaseline Physics = new PhysicsBaseline();
        public FuelRules Fuel = new FuelRules();
        public TireRules Tires = new TireRules();
        public ElectronicsRules Electronics = new ElectronicsRules();
        public AeroRules Aerodynamics = new AeroRules();
        public PitLaneRules PitLane = new PitLaneRules();
        public QualifyingRules Qualifying = new QualifyingRules();
        public WeekendFormat Weekend = new WeekendFormat();
        public Dictionary<string, List<int>> PointsSystems = new Dictionary<string, List<int>>();
        public PenaltyRules Penalties = new PenaltyRules();
        public DevelopmentRules Development = new DevelopmentRules();
        public TestingRules Testing = new TestingRules();
        public EconomyRules Economy = new EconomyRules();

        public int MaxEntries = 24;
        /// <summary>Usual number of full-time entries; used to complete grids when real data is missing.</summary>
        public int GridSize = 22;
        public int RidersPerTeam = 2;
        /// <summary>Target race distance used when a circuit does not specify the number of laps.</summary>
        public double RaceDistanceKm = 110;
        /// <summary>How strong the tow is in this category (Moto3 bikes live in the slipstream).</summary>
        public double SlipstreamFactor = 1.0;
        /// <summary>How much rider skill differences matter compared with machinery (0..2).</summary>
        public double RiderInfluence = 1.0;
        public bool AllowsRemount = true;
        public List<string> Notes = new List<string>();

        public List<int> GetPoints(string system)
        {
            List<int> table;
            if (system != null && PointsSystems.TryGetValue(system, out table)) return table;
            if (PointsSystems.TryGetValue("race", out table)) return table;
            return new List<int>();
        }
    }

    [Serializable]
    public class EngineRules
    {
        public string Type = "Prototype";
        public int DisplacementCc = 1000;
        public int MaxCylinders = 4;
        public double ApproxPowerKw = 210;
        /// <summary>Engines allowed per rider per season (0 = unlimited / spec lease).</summary>
        public int EnginesPerSeason = 0;
        /// <summary>Single supplier engine (e.g. Moto2 Triumph). Engine development is then forbidden.</summary>
        public bool SpecEngine;
        public string SpecSupplier;
        public double RevLimit = 18000;
        public bool DevelopmentFrozen;
    }

    [Serializable]
    public class ChassisRules
    {
        public double MinWeightKg = 157;
        /// <summary>When &gt; 0 the minimum weight applies to bike + rider (Moto2/Moto3/WSSP style).</summary>
        public double MinCombinedWeightKg;
        public double AverageRiderKg = 66;
        public bool ProductionDerived;
        public bool HomologatedFrame;
    }

    /// <summary>Physical baseline for the class; bike and rider attributes move around these values.</summary>
    [Serializable]
    public class PhysicsBaseline
    {
        public double TopSpeedKph = 345;
        /// <summary>Lateral g a typical bike/rider pair reaches mid corner on optimal dry slicks.</summary>
        public double LateralGrip = 1.45;
        public double BrakingG = 1.40;
        /// <summary>Longitudinal acceleration ceiling (wheelie/traction limited).</summary>
        public double TractionG = 0.95;
        public double MaxLeanDeg = 64;
        /// <summary>Overall mechanical spread between best and worst bike in a field (fraction).</summary>
        public double BikeSpread = 1.0;
    }

    [Serializable]
    public class FuelRules
    {
        /// <summary>0 = fuel is not a strategic constraint.</summary>
        public double TankLimitLitres;
        public double SprintTankLimitLitres;
        /// <summary>Litres per km at balanced pace for an average bike.</summary>
        public double ConsumptionPerKm = 0.18;
    }

    [Serializable]
    public class TireRules
    {
        public string Supplier = "Michelin";
        public List<TireCompoundSpec> Compounds = new List<TireCompoundSpec>();
        /// <summary>Tires come out of warmers at this temperature (&lt;= track temp means no warmers).</summary>
        public double WarmerTemperature = 80;
        public double WearScale = 1.0;
        public int FrontAllocation = 8;
        public int RearAllocation = 9;

        public TireCompoundSpec Get(TireCompound compound)
        {
            for (int i = 0; i < Compounds.Count; i++)
                if (Compounds[i].Compound == compound) return Compounds[i];
            return Compounds.Count > 0 ? Compounds[0] : TireCompoundSpec.DefaultMedium();
        }

        public bool Has(TireCompound compound)
        {
            for (int i = 0; i < Compounds.Count; i++)
                if (Compounds[i].Compound == compound) return true;
            return false;
        }
    }

    [Serializable]
    public class TireCompoundSpec
    {
        public TireCompound Compound;
        public bool IsWet;
        /// <summary>Peak grip multiplier relative to the baseline slick.</summary>
        public double Grip = 1.0;
        public double WearRate = 1.0;
        public double OptimalTempMin = 85;
        public double OptimalTempMax = 115;
        /// <summary>Seconds of the first-order heating model; lower = warms faster.</summary>
        public double WarmupTime = 35;
        /// <summary>Heat generation multiplier.</summary>
        public double Heat = 1.0;
        /// <summary>Grip multiplier on a fully wet track.</summary>
        public double WetGrip = 0.4;
        /// <summary>Grip multiplier on a fully dry track (rain tires lose grip and destroy themselves in the dry).</summary>
        public double DryGrip = 1.0;
        public double DryWearMultiplier = 1.0;

        public static TireCompoundSpec DefaultMedium()
        {
            return new TireCompoundSpec { Compound = TireCompound.Medium };
        }
    }

    [Serializable]
    public class ElectronicsRules
    {
        public bool SpecEcu = true;
        public bool TractionControl = true;
        public bool LaunchControl = true;
        public bool RideHeightDevices;
        /// <summary>How much the electronics attribute can compensate rider throttle control (0..1).</summary>
        public double AssistLevel = 0.5;
    }

    [Serializable]
    public class AeroRules
    {
        public bool Allowed = true;
        public int UpdatesPerSeason = 1;
        public double Influence = 1.0;
    }

    [Serializable]
    public class PitLaneRules
    {
        public double SpeedLimitKph = 60;
        public bool AllowsBikeSwap;
        public bool AllowsTireChange;
        public bool AllowsRefuel;
        public double BikeSwapTimeS = 6;
        public double TireChangeTimeS = 28;
        public bool MandatoryPitStop;
        public double LongLapLossS = 2.8;
    }

    [Serializable]
    public class QualifyingRules
    {
        public QualifyingFormat Format = QualifyingFormat.Knockout;
        /// <summary>Knockout: number of riders seeded directly into Q2 (from practice).</summary>
        public int DirectToFinal = 10;
        public int AdvanceFromFirst = 2;
        public double SessionMinutes = 15;
        public int FlyingLapsPerRun = 2;
        public int Runs = 2;
        public TireCompound PreferredCompound = TireCompound.Soft;
    }

    [Serializable]
    public class WeekendFormat
    {
        public List<SessionDefinition> Sessions = new List<SessionDefinition>();
    }

    [Serializable]
    public class SessionDefinition
    {
        public string Id;
        public string Name;
        public SessionType Type;
        /// <summary>Fraction of the full race distance (sprint = 0.5).</summary>
        public double DistanceFraction = 1.0;
        public string PointsSystem = "race";
        public GridSource GridSource = GridSource.Qualifying;
        /// <summary>For QualifyingWithPreviousRaceTop: how many riders take their grid slot from the previous race.</summary>
        public int GridFromPreviousRaceTop;
        public string PreviousRaceSessionId;
        public double DurationMinutes = 45;
        public bool UseSprintFuelLimit;
    }

    [Serializable]
    public class PenaltyRules
    {
        public int TrackLimitsWarnings = 3;
        public Dictionary<InfractionType, PenaltyDefinition> Infractions = new Dictionary<InfractionType, PenaltyDefinition>();
        public double LongLapPenaltyWindowLaps = 3;

        public PenaltyDefinition Get(InfractionType type)
        {
            PenaltyDefinition p;
            return Infractions.TryGetValue(type, out p) ? p : null;
        }
    }

    [Serializable]
    public class PenaltyDefinition
    {
        public PenaltyType Type;
        public double TimeSeconds;
        public int Positions;
    }

    [Serializable]
    public class DevelopmentRules
    {
        /// <summary>Bike components that may be developed during the season.</summary>
        public List<string> AllowedComponents = new List<string>();
        /// <summary>Upper bound on component level gains per season (0 = no cap).</summary>
        public double SeasonGainCap = 0;
        /// <summary>Cost multiplier applied to all development projects.</summary>
        public double CostMultiplier = 1.0;
        public bool Concessions;
        /// <summary>Production categories: the organiser adjusts engine performance of dominant / struggling manufacturers.</summary>
        public bool BalanceOfPerformance;
        public double BalanceStep = 1.0;
    }

    [Serializable]
    public class TestingRules
    {
        public int PrivateTestDays = 0;
        public bool TestRidersAllowed = true;
    }

    /// <summary>How a new team can enter a category.</summary>
    public enum EntryModel
    {
        /// <summary>Entries accepted up to the grid limit against an entry fee.</summary>
        Open,
        /// <summary>Closed grid: a place must be bought from a team that leaves.</summary>
        SlotPurchase,
        /// <summary>Only the organiser runs teams (one-make cups): managers can be hired, teams cannot be founded.</summary>
        OrganiserOnly
    }

    /// <summary>
    /// Money rules of a category. Values are estimates (real figures are not public) and marked as such in the data.
    /// </summary>
    [Serializable]
    public class EconomyRules
    {
        public double ParticipationIncomePerSeason = 0;
        public double TravelCostPerRound = 150000;
        public double CrashRepairCost = 60000;
        /// <summary>Not used by the simulation yet (engines are included in the bike lease).</summary>
        public double EngineCost = 0;
        /// <summary>Yearly bike lease per rider for teams that are not the manufacturer's own.</summary>
        public double BikeLeasePerSeason = 0;
        public List<double> PrizeMoneyByPosition = new List<double>();
        public double TypicalRiderSalary = 500000;

        public EntryModel EntryModel = EntryModel.Open;
        /// <summary>One-off registration for a new entry (Open categories).</summary>
        public double EntryFee = 0;
        /// <summary>One-off price of a grid place (SlotPurchase categories).</summary>
        public double SlotPrice = 0;
        /// <summary>Yearly licences and registration paid by every team.</summary>
        public double LicenceFeePerSeason = 0;
        /// <summary>One-off equipment for a new team: trucks, tools, spares, garage and hospitality basics.</summary>
        public double StartupEquipmentCost = 0;
        /// <summary>One-off bikes and spares per rider bought by a new team (on top of any lease).</summary>
        public double BikePurchasePerRider = 0;
        /// <summary>Scale of facility upkeep and upgrade costs relative to the MotoGP reference.</summary>
        public double FacilityCostScale = 1.0;
        /// <summary>Scale of staff salaries relative to the MotoGP reference.</summary>
        public double StaffSalaryScale = 1.0;
        /// <summary>Cash a team should keep in reserve, as a share of its season operating cost.</summary>
        public double SafetyReserveFraction = 0.15;
        /// <summary>Expected crashes per rider and event (for forecasts of repair costs).</summary>
        public double ExpectedCrashesPerRiderEvent = 0.3;
        public string DataConfidence = "estimated";
    }
}
