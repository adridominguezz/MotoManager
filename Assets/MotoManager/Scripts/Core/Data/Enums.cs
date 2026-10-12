using System;

namespace MotoManager.Core.Data
{
    /// <summary>The two competition ecosystems of the universe.</summary>
    public enum Pyramid
    {
        GrandPrix,
        Superbike
    }

    public enum TeamStatus
    {
        Factory,
        Satellite,
        Independent
    }

    [Flags]
    public enum RidingStyle
    {
        None = 0,
        LateBraker = 1 << 0,
        SmoothRider = 1 << 1,
        AggressiveRider = 1 << 2,
        TireSaver = 1 << 3,
        CornerSpecialist = 1 << 4,
        StraightLineSpecialist = 1 << 5,
        WetSpecialist = 1 << 6,
        QualifyingSpecialist = 1 << 7,
        RaceSpecialist = 1 << 8,
        DefensiveRider = 1 << 9
    }

    public enum TireCompound
    {
        Soft,
        Medium,
        Hard,
        Intermediate,
        Rain
    }

    public enum TireCondition
    {
        Cold,
        Optimal,
        Hot,
        Overheated,
        Worn
    }

    public enum SegmentType
    {
        Straight,
        Corner
    }

    /// <summary>What the bike is doing right now. Used by physics, wear, incidents and the renderer.</summary>
    public enum RidingPhase
    {
        Straight,
        Braking,
        CornerEntry,
        MidCorner,
        CornerExit
    }

    public enum WeatherCondition
    {
        Dry,
        Cloudy,
        LightRain,
        Rain,
        HeavyRain,
        Drying,
        Wet
    }

    public enum SessionType
    {
        Practice,
        Qualifying,
        WarmUp,
        Sprint,
        Race
    }

    public enum QualifyingFormat
    {
        SingleSession,
        Knockout
    }

    public enum GridSource
    {
        Qualifying,
        /// <summary>WorldSBK race 2: the top N of the previous race fill the front rows, the rest come from qualifying.</summary>
        QualifyingWithPreviousRaceTop
    }

    public enum PenaltyType
    {
        Warning,
        LongLap,
        DoubleLongLap,
        RideThrough,
        TimePenalty,
        PositionDrop,
        GridPenalty,
        BackOfGrid,
        Disqualification
    }

    public enum InfractionType
    {
        TrackLimits,
        LastLapTrackLimits,
        JumpStart,
        PitLaneSpeeding,
        UnsafeRelease,
        IrresponsibleRiding,
        FuelLimit
    }

    public enum CrashType
    {
        LowSide,
        HighSide,
        FrontEndLoss,
        RearEndLoss,
        ContactCrash,
        RunOff
    }

    public enum RiderErrorType
    {
        LockUp,
        OutBraked,
        RunWide,
        LostLine,
        Wheelspin,
        LostConcentration,
        PressureMistake
    }

    public enum FailureType
    {
        Engine,
        Electronics,
        Brakes,
        Transmission,
        Cooling,
        Suspension
    }

    public enum PaceMode
    {
        Conserve,
        Balanced,
        Push
    }

    public enum RiderMode
    {
        Defend,
        Balanced,
        Attack
    }

    public enum StaffRole
    {
        /// <summary>Only used in the data to name a team's real manager: managers are ManagerProfiles, not staff.</summary>
        TeamManager,
        /// <summary>Leads the technical programme: development speed and the quality of every project.</summary>
        TechnicalDirector,
        /// <summary>Runs the engineering group: development and the base set-up of the bikes.</summary>
        ChiefEngineer,
        /// <summary>Crew chief of one rider: weekend set-up and race-day calls.</summary>
        RaceEngineer,
        /// <summary>Electronics strategy and development.</summary>
        ElectronicsEngineer,
        /// <summary>Data analysis: set-up work and weather / race forecasts.</summary>
        DataAnalyst,
        /// <summary>Tyre choice and tyre-related set-up.</summary>
        TireEngineer,
        /// <summary>Aerodynamic development (categories with aero freedom).</summary>
        Aerodynamicist,
        /// <summary>Crew mechanics (one lead per rider): reliability of the bikes.</summary>
        Mechanic,
        /// <summary>Finding and assessing riders.</summary>
        Scout,
        /// <summary>Medical care: recovery from injuries.</summary>
        Physio,
        /// <summary>Rider coaching: development of the team's riders.</summary>
        RiderCoach,
        /// <summary>Runs the official shop (range, prices, stock) and makes it sell a little more.</summary>
        Marketing
    }

    public enum FacilityType
    {
        /// <summary>Workshop and production: development speed, parallel projects, bike preparation.</summary>
        Factory,
        /// <summary>Engine test benches: engine and power projects.</summary>
        Dyno,
        /// <summary>Aerodynamic projects.</summary>
        WindTunnel,
        /// <summary>Electronics projects and weekend electronics set-up.</summary>
        ElectronicsLab,
        /// <summary>Set-up preparation and rider learning.</summary>
        Simulator,
        /// <summary>Rider fitness and training: development and injury recovery.</summary>
        RiderTrainingCenter,
        /// <summary>Scouting network.</summary>
        ScoutingCenter,
        /// <summary>Data infrastructure: forecasts and set-up analysis.</summary>
        DataCenter,
        /// <summary>Young riders' development.</summary>
        Academy,
        /// <summary>Guests and partners: what sponsors pay and how keen they are.</summary>
        Hospitality
    }

    /// <summary>Derived performance attributes of a motorcycle (0-100), computed from components by the bike class model.</summary>
    public enum PerfAttr
    {
        Power,
        TopSpeed,
        Acceleration,
        Braking,
        CornerEntry,
        MidCorner,
        CornerExit,
        Traction,
        Stability,
        TireWear,
        FuelEfficiency,
        Reliability,
        WetPerformance
    }

    public enum AttributeVisibility
    {
        Visible,
        Partial,
        Hidden
    }
}
