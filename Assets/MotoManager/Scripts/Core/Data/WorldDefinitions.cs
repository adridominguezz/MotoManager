using System;
using System.Collections.Generic;

namespace MotoManager.Core.Data
{
    /// <summary>
    /// Describes a family of motorcycles (MotoGP prototype, WorldSBK production-derived, Moto2 spec-engine...).
    /// It lists the components that exist for that kind of bike and how they translate into performance.
    /// Different classes use different weights, so a WorldSBK bike is not "a MotoGP bike with other numbers".
    /// </summary>
    [Serializable]
    public class BikeClassDefinition
    {
        public string Id;
        public string Name;
        public List<string> Components = new List<string>();
        /// <summary>PerfAttr -> (component -> weight). Weights of an attribute are normalised at runtime.</summary>
        public Dictionary<PerfAttr, Dictionary<string, double>> PerformanceWeights = new Dictionary<PerfAttr, Dictionary<string, double>>();
        /// <summary>
        /// Fraction of each attribute that comes from the homologated road bike rather than from race development
        /// (0 for prototypes). Production-derived bikes inherit strengths and weaknesses of the street model.
        /// </summary>
        public double ProductionBaseShare;
        public string Description;
    }

    [Serializable]
    public class CircuitDefinition
    {
        public string Id;
        public string Name;
        public string ShortName;
        public string Country;
        public string City;
        public double LengthM;
        /// <summary>
        /// Layout DSL starting at the finish line, e.g. "S650 R90@45 S200 L60@80:tl".
        /// S&lt;m&gt; = straight, L/R&lt;deg&gt;@&lt;radius m&gt; = corner. Flags after ':' -
        /// tl = monitored for track limits, ll = long lap loop location.
        /// Straights are rescaled so the total equals <see cref="LengthM"/>.
        /// </summary>
        public string Layout;
        /// <summary>
        /// Optional real centre line of the circuit: [x, y] in metres, or [lat, lon] when <see cref="ShapeIsLatLon"/>.
        /// When present (and <see cref="Layout"/> is empty) the simulation segments are derived from it, so the
        /// physics and the drawing follow the real track. The first point is moved to <see cref="ShapeStart"/> if given.
        /// </summary>
        public List<double[]> Shape;
        public bool ShapeIsLatLon;
        /// <summary>Finish line position in the same units as <see cref="Shape"/> (optional).</summary>
        public double[] ShapeStart;
        /// <summary>Race direction: true if the circuit runs clockwise (the shape is reversed if needed).</summary>
        public bool Clockwise = true;
        /// <summary>Calibration factor applied to every corner radius (lap time tuning without editing the layout).</summary>
        public double RadiusScale = 1.0;
        /// <summary>0..1 surface abrasiveness (tire wear).</summary>
        public double Abrasiveness = 0.5;
        /// <summary>0..1 baseline surface grip.</summary>
        public double SurfaceGrip = 0.5;
        public double AverageAirTempC = 24;
        /// <summary>Probability that some rain appears during a race day.</summary>
        public double RainChance = 0.15;
        public double PitEntryBeforeLineM = 250;
        public double PitExitAfterLineM = 150;
        public double PitLaneLengthM = 0;
        public double LongLapLossS = 0;
        /// <summary>Number of timing sectors.</summary>
        public int Sectors = 4;
        public Dictionary<string, int> LapsOverride = new Dictionary<string, int>();
        /// <summary>Typical real race-pace lap (seconds) per category; used by balance tests, never by the simulation.</summary>
        public Dictionary<string, double> ReferenceLaps = new Dictionary<string, double>();
        public string DataConfidence = "approximate";
    }

    [Serializable]
    public class CalendarDefinition
    {
        public string Id;
        public string Group;
        public int Season;
        public List<CalendarRound> Rounds = new List<CalendarRound>();
        public string DataConfidence = "approximate";
    }

    [Serializable]
    public class CalendarRound
    {
        public int Round;
        public string Name;
        public string CircuitId;
        public DateTime Date;
        /// <summary>Categories racing at this event. Empty = every category of the calendar group.</summary>
        public List<string> Categories = new List<string>();
    }

    [Serializable]
    public class SponsorTemplate
    {
        public string Id;
        public string Name;
        public string Industry;
        /// <summary>1 = title sponsor level, 3 = small partner.</summary>
        public int Tier = 3;
        public double PaymentPerSeason = 500000;
        public double MinTeamReputation;
        public List<Pyramid> Pyramids = new List<Pyramid>();
        public List<string> PreferredNationalities = new List<string>();
        /// <summary>Teams this sponsor backs in the 2026 snapshot (signed first, unavailable to others while signed).</summary>
        public List<string> HomeTeams = new List<string>();
        public List<SponsorObjective> Objectives = new List<SponsorObjective>();
        public int ContractSeasons = 2;
    }

    public enum SponsorObjectiveType
    {
        RaceFinishTopN,
        SeasonPoints,
        Wins,
        Podiums,
        ChampionshipPositionTopN,
        RiderNationality
    }

    [Serializable]
    public class SponsorObjective
    {
        public SponsorObjectiveType Type;
        public double Target;
        public double BonusPerOccurrence;
        public double SeasonBonus;
        public string Description;
    }

    [Serializable]
    public class NamePool
    {
        public string Nationality;
        public string CountryName;
        /// <summary>Relative talent production per pyramid. Spain/Italy produce lots of GP riders, UK lots of SBK riders...</summary>
        public double WeightGrandPrix = 1;
        public double WeightSuperbike = 1;
        public List<string> FirstNames = new List<string>();
        public List<string> LastNames = new List<string>();
    }

    [Serializable]
    public class DevelopmentTemplate
    {
        public string Id;
        public string Name;
        public string Component;
        /// <summary>Bike classes this project applies to (empty = all).</summary>
        public List<string> BikeClasses = new List<string>();
        public Dictionary<string, double> Effects = new Dictionary<string, double>();
        public double BaseCost = 400000;
        public int DurationDays = 30;
        /// <summary>0..1: variance and failure probability.</summary>
        public double Risk = 0.2;
        public string Description;
    }

    [Serializable]
    public class StaffTemplate
    {
        public string Id;
        public string FirstName;
        public string LastName;
        public StaffRole Role;
        public string Nationality;
        public int BirthYear = 1980;
        public double Skill = 60;
        public double Salary = 200000;
        public string TeamId;
    }
}
