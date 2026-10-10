using System;
using MotoManager.Core.Common;
using MotoManager.Core.Data;

namespace MotoManager.Core.Race
{
    /// <summary>
    /// In-race decisions for riders managed by the AI (all rivals, plus player riders left on auto).
    /// Only uses information that a real pit wall would have (gaps, tire temps/wear, wetness, fuel).
    /// Decisions are deterministic functions of the race state.
    /// </summary>
    public class RaceAI
    {
        private readonly RaceSimulation _sim;

        public RaceAI(RaceSimulation sim)
        {
            _sim = sim;
        }

        public void OnLap(RiderRaceState r)
        {
            if (!r.Entry.AutoManaged || r.Status != RaceStatus.Racing) return;
            int lapsLeft = _sim.TotalLaps - r.Lap;
            var a = r.Entry.Rider.Attributes;

            // ---- pace
            PaceMode pace = PaceMode.Balanced;
            double projectedWear = r.Lap > 1 ? Math.Max(r.Front.Wear, r.Rear.Wear) / r.Lap * _sim.TotalLaps : 0;
            bool tiresHot = r.Front.Condition == TireCondition.Overheated || r.Rear.Condition == TireCondition.Overheated;
            double fuelAtEnd = _sim.ProjectedFuelAtFinish(r);
            if (tiresHot || projectedWear > 0.95 - a.TireManagement / 1000.0 || (!double.IsNaN(fuelAtEnd) && fuelAtEnd < 0.2))
                pace = PaceMode.Conserve;
            else if (lapsLeft <= 3 && (Math.Abs(r.GapToAhead) < 1.2 + (Career.TeamAI.RaceRisk(r.Entry.Team) - 50) / 50.0 && r.Position > 1))
                pace = PaceMode.Push;
            else if (lapsLeft <= 2)
                pace = PaceMode.Push;
            else if (r.Position == 1 && GapBehind(r) > 3.0 && r.Lap > 3)
                pace = PaceMode.Conserve;
            if (r.Front.Condition == TireCondition.Cold || r.Rear.Condition == TireCondition.Cold)
                pace = pace == PaceMode.Push ? PaceMode.Balanced : pace;
            r.Pace = pace;

            // ---- attitude (rider aggression, tempered or pushed by the team's appetite for risk)
            double risk = Career.TeamAI.RaceRisk(r.Entry.Team);
            double attackThreshold = 0.5 + a.Aggression / 100.0 * 0.6 + (risk - 50) / 100.0 * 0.4;
            if (r.Position > 1 && r.GapToAhead > 0 && r.GapToAhead < attackThreshold) r.Mode = RiderMode.Attack;
            else if (GapBehind(r) < 0.5 && (r.GapToAhead > 1.0 || r.Position == 1)) r.Mode = RiderMode.Defend;
            else r.Mode = RiderMode.Balanced;
        }

        /// <summary>Called a few times per second: weather reaction (pit for wets / slicks).</summary>
        public void Periodic()
        {
            var rules = _sim.Rules;
            bool canSwap = rules.PitLane.AllowsBikeSwap;
            bool canChange = rules.PitLane.AllowsTireChange;
            if (!canSwap && !canChange) return;
            double wet = _sim.Weather.TrackWetness;
            foreach (var r in _sim.Riders)
            {
                if (!r.Entry.AutoManaged || r.Status != RaceStatus.Racing || r.PitRequest != PitRequestType.None) continue;
                if (r.Lap < 0) continue;
                int lapsLeft = _sim.TotalLaps - r.Lap;
                var a = r.Entry.Rider.Attributes;
                bool onSlicks = !r.Rear.Spec.IsWet;
                PitRequestType type = canSwap ? PitRequestType.BikeSwap : PitRequestType.TireChange;
                if (onSlicks)
                {
                    // Brave / wet-skilled riders stay out longer on slicks.
                    // Gamblers stay out on slicks a little longer.
                    double teamRisk = (Career.TeamAI.RaceRisk(r.Entry.Team) - 50) / 100.0;
                    double threshold = 0.32 + (a.RiskManagement < 50 ? 0.05 : 0) + MathX.Centered(a.WetSkill) * -0.04 + (lapsLeft <= 2 ? 0.25 : 0) + teamRisk * 0.08;
                    if (wet > threshold && _sim.Weather.RainIntensity > 0.05)
                    {
                        r.PitRequest = type;
                        r.PitCompound = _sim.Rules.Tires.Has(TireCompound.Rain) ? TireCompound.Rain : TireCompound.Intermediate;
                    }
                }
                else
                {
                    double threshold = 0.2 + MathX.Centered(a.WetSkill) * 0.03;
                    bool rearCooking = r.Rear.Condition == TireCondition.Overheated || r.Rear.Wear > 0.75;
                    if (lapsLeft > 2 && ((wet < threshold && _sim.Weather.RainIntensity < 0.03) || (rearCooking && wet < 0.35)))
                    {
                        r.PitRequest = type;
                        r.PitCompound = TireCompound.Soft;
                        if (!_sim.Rules.Tires.Has(TireCompound.Soft)) r.PitCompound = TireCompound.Medium;
                    }
                }
            }
        }

        private double GapBehind(RiderRaceState r)
        {
            var behind = _sim.RiderAtPosition(r.Position + 1);
            if (behind == null || behind.Status != RaceStatus.Racing) return 99;
            return behind.GapToAhead;
        }

        /// <summary>Pre-race tire choice for a rider given the conditions (used by AI teams and as engineer advice).</summary>
        public static void ChooseTires(CategoryRules rules, double trackTemp, double wetness, double abrasiveness, int laps,
            double tireManagement, out TireCompound front, out TireCompound rear, double teamRisk = 50)
        {
            if (wetness > 0.35)
            {
                front = rules.Tires.Has(TireCompound.Rain) ? TireCompound.Rain : TireCompound.Intermediate;
                rear = front;
                return;
            }
            // Hot / abrasive / long races push towards harder compounds.
            // Teams that like risk lean towards the softer, faster option.
            double score = (trackTemp - 30) / 15.0 + (abrasiveness - 0.5) * 2 + (laps - 20) / 12.0 - (tireManagement - 50) / 60.0 - (teamRisk - 50) / 80.0;
            rear = score > 0.8 ? TireCompound.Hard : score < -0.6 ? TireCompound.Soft : TireCompound.Medium;
            front = score > 1.2 ? TireCompound.Hard : score < -1.0 ? TireCompound.Soft : TireCompound.Medium;
            if (!rules.Tires.Has(rear)) rear = rules.Tires.Has(TireCompound.Medium) ? TireCompound.Medium : rules.Tires.Compounds[0].Compound;
            if (!rules.Tires.Has(front)) front = rules.Tires.Has(TireCompound.Medium) ? TireCompound.Medium : rules.Tires.Compounds[0].Compound;
        }
    }
}
