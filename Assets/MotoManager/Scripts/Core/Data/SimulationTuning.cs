using System;

namespace MotoManager.Core.Data
{
    /// <summary>
    /// Balance constants for the race engine. Defaults are calibrated by the test-suite (lap times, crash rates,
    /// tire degradation). They can be overridden from data/simulation/tuning.json without recompiling.
    /// </summary>
    [Serializable]
    public class SimulationTuning
    {
        // --- Time steps
        public double VisualTimeStep = 0.05;
        public double BackgroundTimeStep = 0.2;

        // --- Rider/bike influence on the physical limits (fractions at attribute 100 vs 50)
        public double RiderCorneringSpread = 0.035;
        public double RiderBrakingSpread = 0.05;
        public double RiderThrottleSpread = 0.04;
        public double BikeAttributeSpread = 0.03;
        public double PowerSpread = 0.045;
        public double TopSpeedSpread = 0.025;
        /// <summary>Lap-to-lap variation for a rider with Consistency 50 (fraction of limit).</summary>
        public double ConsistencyNoise = 0.004;

        // --- Pace / mode effects on limit usage
        public double PushUsage = 1.02;
        public double ConserveUsage = 0.978;
        public double PushWear = 1.45;
        public double ConserveWear = 0.68;
        public double PushRisk = 1.8;
        public double ConserveRisk = 0.55;
        public double AttackRisk = 1.35;
        public double DefendCornerLoss = 0.004;

        // --- Tires
        public double BaseWearPerMeter = 1.1e-5;
        public double WearGripLoss = 0.10;
        public double ColdGripLoss = 0.22;
        public double OverheatGripLoss = 0.12;
        public double HeatScale = 85;
        public double WetCooling = 42;

        // --- Weather
        public double WettingRate = 0.012;
        public double DryingRate = 0.0035;

        // --- Incidents (per corner pass, average rider, balanced pace, dry)
        public double BaseCrashChance = 0.0004;
        public double BaseErrorChance = 0.008;
        public double ContactChance = 0.015;
        public double ContactCrashChance = 0.2;
        public double WrongTireCrashMultiplier = 25;
        public double ColdTireCrashMultiplier = 4;
        public double WetCrashMultiplier = 2.5;

        // --- Overtaking
        public double SlipstreamDragReduction = 0.16;
        public double SlipstreamRange = 35;
        public double MinFollowGap = 2.8;
        public double OvertakeBaseChance = 0.25;

        // --- Reliability: failures per bike per race hour at Reliability 50
        public double BaseFailureRatePerHour = 0.07;

        /// <summary>
        /// Races and sprints run this fraction of their real distance so a weekend is quick to watch. Tyre wear,
        /// fuel use and failure risk per lap are scaled by the inverse, so a race ends with the same wear, fuel and
        /// risk as the full distance (the strategy stays the real one, only shorter).
        /// </summary>
        public double RaceLengthFactor = 0.5;

        // --- Start
        public double JumpStartChance = 0.002;
    }
}
