using System;
using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Race
{
    public enum RaceStatus
    {
        Grid,
        Racing,
        InPitLane,
        Crashed,
        Retired,
        Finished,
        Disqualified
    }

    public enum PitRequestType
    {
        None,
        BikeSwap,
        TireChange,
        RideThrough
    }

    /// <summary>One rider entered in a session. Built by the weekend layer from the world state.</summary>
    public class RaceEntry
    {
        public Rider Rider;
        public Team Team;
        public BikePerformance Bike;
        public string ManufacturerId;
        public int GridPosition;
        public TireCompound FrontCompound = TireCompound.Medium;
        public TireCompound RearCompound = TireCompound.Medium;
        /// <summary>Compound fitted to the spare bike / prepared wheels for a stop.</summary>
        public TireCompound SpareCompound = TireCompound.Rain;
        /// <summary>0..1 how well the bike is set up for this circuit (from practice).</summary>
        public double SetupQuality = 0.5;
        /// <summary>Weekend form and circuit affinity (multiplier on limit usage, ~0.99-1.01).</summary>
        public double Form = 1.0;
        public bool IsPlayer;
        /// <summary>When true the AI makes the in-race decisions even for a player rider.</summary>
        public bool AutoManaged = true;
        public int PendingLongLaps;
        public int GridPenaltyPlaces;
        /// <summary>Extra reliability points from the team's mechanics and factory.</summary>
        public double ReliabilityBonus;
        /// <summary>Best set-up the crew can reach over a weekend (crew chief, data, electronics, simulator).</summary>
        public double SetupCeiling = 1.0;
        /// <summary>Tyre wear multiplier from the tyre engineer (below 1 = the tyres last longer).</summary>
        public double TireCare = 1.0;
    }

    public class WeatherSetup
    {
        public double AirTemp = 24;
        /// <summary>0 = clear sky, 1 = overcast.</summary>
        public double CloudCover = 0.3;
        public double InitialWetness;
        public List<RainEvent> RainEvents = new List<RainEvent>();
    }

    public class RainEvent
    {
        public double StartTime;
        public double RampTime = 120;
        public double Duration = 600;
        public double Intensity = 0.6;
    }

    public class RaceConfig
    {
        public CategoryRules Rules;
        public CircuitDefinition Circuit;
        public SimulationTuning Tuning = new SimulationTuning();
        public SessionType SessionType = SessionType.Race;
        public string SessionName = "Race";
        public int Laps = 20;
        /// <summary>Laps run / laps of the full distance (see SimulationTuning.RaceLengthFactor); 1 = full race.</summary>
        public double DistanceScale = 1.0;
        public ulong Seed = 1;
        public double TimeStep = 0.05;
        public WeatherSetup Weather = new WeatherSetup();
        public List<RaceEntry> Entries = new List<RaceEntry>();
        public double FuelLimitLitres;
        /// <summary>Track rubber / evolution grip bonus (fraction).</summary>
        public double TrackEvolution;
        /// <summary>Single rider time trial (practice / qualifying). Disables the start procedure and traffic.</summary>
        public bool TimeTrial;
        /// <summary>Known clean-air lap per rider id (skips the per-race estimate when provided).</summary>
        public Dictionary<string, double> ReferenceLaps;
        /// <summary>Hard limit on simulated time (safety net).</summary>
        public double MaxDurationS = 4 * 3600;
    }

    public enum RaceEventType
    {
        RaceStart,
        Overtake,
        OvertakeFailed,
        Contact,
        Crash,
        Remount,
        Error,
        RunOff,
        MechanicalFailure,
        MechanicalIssue,
        PitIn,
        PitOut,
        TrackLimitsWarning,
        Penalty,
        PenaltyServed,
        JumpStart,
        FastestLap,
        LeaderChange,
        WeatherChange,
        WhiteFlag,
        YellowFlag,
        LastLap,
        Finish,
        Retirement,
        TireWarning,
        FuelWarning,
        CommandApplied
    }

    [Serializable]
    public class RaceEvent
    {
        public double Time;
        public int Lap;
        public RaceEventType Type;
        public int RiderIndex = -1;
        public int OtherRiderIndex = -1;
        public string Detail;
        public bool Important;

        public override string ToString()
        {
            return "[" + Time.ToString("0.0") + "s L" + Lap + "] " + Type + " " + Detail;
        }
    }

    public enum RaceCommandType
    {
        SetPace,
        SetRiderMode,
        RequestPit,
        CancelPit,
        SetAutoManaged
    }

    /// <summary>Player instruction. Logged with the tick it was applied on, so a race can be replayed exactly.</summary>
    [Serializable]
    public class RaceCommand
    {
        public long Tick;
        public int RiderIndex;
        public RaceCommandType Type;
        public PaceMode Pace;
        public RiderMode Mode;
        public PitRequestType Pit;
        public TireCompound Compound;
        public bool Flag;
    }

    public class TireState
    {
        public TireCompoundSpec Spec;
        public double Wear;
        public double Temperature;
        public double DistanceM;

        public TireCompound Compound { get { return Spec.Compound; } }

        public TireCondition Condition
        {
            get
            {
                if (Wear > 0.7) return TireCondition.Worn;
                if (Temperature < Spec.OptimalTempMin - 8) return TireCondition.Cold;
                if (Temperature > Spec.OptimalTempMax + 15) return TireCondition.Overheated;
                if (Temperature > Spec.OptimalTempMax) return TireCondition.Hot;
                return TireCondition.Optimal;
            }
        }
    }

    /// <summary>Kinds of track used to explain where time is gained or lost.</summary>
    public enum TrackZone
    {
        Straights,
        SlowCorners,
        MediumCorners,
        FastCorners
    }

    /// <summary>What happened to one rider during the race (for results and post-race analysis).</summary>
    public class RaceStats
    {
        public const int ZoneCount = 4;
        /// <summary>Clean-lap time and distance per zone (laps with mistakes, pit stops or penalties excluded).</summary>
        public readonly double[] ZoneTime = new double[ZoneCount];
        public readonly double[] ZoneDistance = new double[ZoneCount];
        public double TopSpeed;
        public double[] BestSectors;
        public readonly List<double> LapTimes = new List<double>();
        public readonly List<int> LapPositions = new List<int>();
        /// <summary>Moves started that had to be aborted.</summary>
        public int OvertakesFailed;
        public int OvertakesMade;
        public int OvertakesSuffered;
        public int Errors;
        public int RunOffs;
        public int PositionAfterLapOne;

        public double ZoneSpeed(TrackZone zone)
        {
            int z = (int)zone;
            return ZoneTime[z] > 1 ? ZoneDistance[z] / ZoneTime[z] : double.NaN;
        }
    }

    public class PenaltyRecord
    {
        public PenaltyType Type;
        public InfractionType Infraction;
        public double TimeSeconds;
        public int Positions;
        public bool Served;
        public double IssuedAt;
    }

    /// <summary>Live state of one rider. The renderer only reads these values.</summary>
    public class RiderRaceState
    {
        public int Index;
        public RaceEntry Entry;
        public RaceStatus Status = RaceStatus.Grid;
        public RidingPhase Phase;

        // Position on track
        public int Lap;
        public double Distance;
        public double PrevDistance;
        public int SegmentIndex;
        public double Speed;
        public double PrevSpeed;
        public double LeanDeg;
        public double LateralOffset;
        public double TargetLateral;
        public double PitProgress;
        public double PitStationaryTime;
        public bool PitServiceDone;

        // Timing
        public double LapStartTime;
        public double LastLapTime;
        public double BestLapTime = double.MaxValue;
        public double[] SectorTimes;
        public double SectorStartTime;
        public int CurrentSector;
        public int Position;
        public double GapToLeader;
        public double GapToAhead;
        public int LapsDown;
        public double FinishTime;
        public int CheckpointIndex = -1;
        public double CheckpointTime;

        // Machinery
        public TireState Front = new TireState();
        public TireState Rear = new TireState();
        public double Fuel;
        public double PowerLoss;
        public double HandlingLoss;
        public bool HasIssue;

        // Strategy
        public PaceMode Pace = PaceMode.Balanced;
        public RiderMode Mode = RiderMode.Balanced;
        public PitRequestType PitRequest;
        public TireCompound PitCompound = TireCompound.Rain;
        public bool SpareBikeUsed;

        // Dynamics helpers
        /// <summary>Clean-air lap time of this rider/bike at the start (what the pit wall knows about relative pace).</summary>
        public double ReferenceLap;
        public double LapNoise = 1.0;
        public double ReactionTime;
        public bool Launched;
        public double DelayRemaining;
        public double DelayRate;
        public double CrashTime;
        public CrashType LastCrash;
        public double RemountAt = -1;
        public int Crashes;
        public int Overtakes;
        public bool InjuredInCrash;
        public string RetireReason;
        /// <summary>Why the rider is out, in the player's language (results table).</summary>
        public string RetireNote;

        // Overtake attempt state
        public int AttemptTarget = -1;
        public double AttemptUntilProgress;
        public bool AttemptSucceeds;
        public double StuckTime;
        public int LastInteractionSegment = -1;

        // Penalties
        public int TrackLimitsCount;
        public int PendingLongLaps;
        public bool PendingRideThrough;
        public double TimePenalty;
        public int PositionDrop;
        public readonly List<PenaltyRecord> Penalties = new List<PenaltyRecord>();
        public bool OnLongLap;

        public readonly RaceStats Stats = new RaceStats();

        public double Progress(double lapLength)
        {
            return Lap * lapLength + Distance;
        }

        public bool IsRunning
        {
            get { return Status == RaceStatus.Racing || Status == RaceStatus.InPitLane || Status == RaceStatus.Grid; }
        }

        public bool OnTrack
        {
            get { return Status == RaceStatus.Racing || Status == RaceStatus.Grid; }
        }
    }
}
