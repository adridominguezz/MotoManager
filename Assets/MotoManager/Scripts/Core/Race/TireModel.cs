using System;
using MotoManager.Core.Common;
using MotoManager.Core.Data;

namespace MotoManager.Core.Race
{
    /// <summary>
    /// Contextual tire model: grip depends on compound, temperature window, wear and surface water;
    /// wear and heat depend on the phase of the corner, the rider, the bike, the asphalt and the pace.
    /// </summary>
    public static class TireModel
    {
        public static void Fit(TireState tire, TireCompoundSpec spec, double startTemperature)
        {
            tire.Spec = spec;
            tire.Wear = 0;
            tire.DistanceM = 0;
            tire.Temperature = startTemperature;
        }

        public static double Grip(TireState tire, double wetness, SimulationTuning tuning)
        {
            var spec = tire.Spec;
            double g = spec.Grip;

            double t = tire.Temperature;
            if (t < spec.OptimalTempMin)
                g *= 1 - tuning.ColdGripLoss * MathX.Clamp01((spec.OptimalTempMin - t) / 45.0);
            else if (t > spec.OptimalTempMax)
                g *= 1 - tuning.OverheatGripLoss * MathX.Clamp01((t - spec.OptimalTempMax) / 30.0);

            double w = tire.Wear;
            // Softer rubber gives more grip when new and falls away as it wears; a hard compound starts slower but
            // keeps its grip, so it can be pushed at the end of the race.
            g *= 1 - tuning.WearGripLoss * spec.WearSensitivity * Math.Pow(w, 1.5);
            if (w > 0.9) g *= 1 - (w - 0.9) * 1.2;

            double wet = Math.Pow(MathX.Clamp01(wetness), 0.8);
            g *= spec.DryGrip + (spec.WetGrip - spec.DryGrip) * wet;
            return Math.Max(0.15, g);
        }

        /// <summary>Front/rear stress for a riding phase (0..~1.6).</summary>
        public static void PhaseStress(RidingPhase phase, TrackSegment seg, out double front, out double rear)
        {
            switch (phase)
            {
                case RidingPhase.Braking:
                    front = 0.7 + 0.8 * seg.BrakingIntensity;
                    rear = 0.25;
                    break;
                case RidingPhase.CornerEntry:
                    front = 1.0 + 0.4 * seg.TireStressFront;
                    rear = 0.5;
                    break;
                case RidingPhase.MidCorner:
                    front = 0.7 + 0.5 * seg.TireStressFront;
                    rear = 0.7 + 0.5 * seg.TireStressRear;
                    break;
                case RidingPhase.CornerExit:
                    front = 0.4;
                    rear = 1.0 + 0.6 * seg.TireStressRear;
                    break;
                default:
                    front = 0.15;
                    rear = 0.35;
                    break;
            }
        }

        public static void Update(TireState tire, double stress, double distance, double dt, double speed, double trackTemp,
            double wetness, double abrasiveness, double riderWearFactor, double bikeWearFactor, double paceHeat,
            double wearScale, SimulationTuning tuning)
        {
            var spec = tire.Spec;
            // Temperature (first order towards an equilibrium)
            double speedFactor = MathX.Clamp(speed / 55.0, 0.15, 1.3);
            double teq = trackTemp + tuning.HeatScale * (0.35 + 0.65 * stress) * speedFactor * paceHeat * spec.Heat
                         - tuning.WetCooling * wetness * (spec.IsWet ? 0.55 : 1.0);
            double tau = Math.Max(5, spec.WarmupTime);
            tire.Temperature += (teq - tire.Temperature) * Math.Min(1, dt / tau);

            // Wear
            double tempWear = 1.0;
            var cond = tire.Condition;
            if (cond == TireCondition.Overheated) tempWear = 1.7;
            else if (cond == TireCondition.Hot) tempWear = 1.25;
            else if (cond == TireCondition.Cold) tempWear = 1.15;
            double surface = 0.55 + 0.9 * abrasiveness;
            double dryPenalty = 1 + (spec.DryWearMultiplier - 1) * (1 - MathX.Clamp01(wetness * 1.6));
            double wetRelief = spec.IsWet ? 1.0 : 1 - 0.5 * MathX.Clamp01(wetness);
            double dw = tuning.BaseWearPerMeter * distance * spec.WearRate * stress * surface * riderWearFactor *
                        bikeWearFactor * tempWear * dryPenalty * wetRelief * wearScale;
            tire.Wear = Math.Min(1.0, tire.Wear + dw);
            tire.DistanceM += distance;
        }
    }
}
