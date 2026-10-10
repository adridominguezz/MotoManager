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
        TeamManager,
        ChiefEngineer,
        CrewChief,
        DataEngineer,
        TireEngineer,
        Aerodynamicist,
        Designer,
        Scout,
        Mechanic
    }

    public enum FacilityType
    {
        Workshop,
        DesignOffice,
        Simulator,
        DataCenter,
        Academy,
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
