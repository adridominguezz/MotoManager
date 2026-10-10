using System;
using System.Collections.Generic;
using MotoManager.Core.Data;

namespace MotoManager.Core.Race
{
    public class EngineerMessage
    {
        /// <summary>Stable key (rider + topic) used to avoid repeating the same advice.</summary>
        public string Key;
        public int RiderIndex;
        public string Text;
        /// <summary>0 info, 1 warning, 2 urgent.</summary>
        public int Severity;
    }

    /// <summary>
    /// What the player's race engineer tells the pit wall. Only uses information a real team has (telemetry of
    /// its own riders, gaps, weather radar with an accuracy that depends on the data engineer).
    /// </summary>
    public static class RaceEngineer
    {
        public static List<EngineerMessage> Advise(RaceSimulation sim, RiderRaceState r, double forecasterSkill)
        {
            var list = new List<EngineerMessage>();
            if (r.Status != RaceStatus.Racing) return list;
            string name = r.Entry.Rider.ShortName;
            int lapsLeft = sim.TotalLaps - r.Lap;
            double wet = sim.Weather.TrackWetness;
            bool onWets = r.Rear.Spec.IsWet;
            bool canPit = sim.Rules.PitLane.AllowsBikeSwap || sim.Rules.PitLane.AllowsTireChange;

            if (!onWets && wet > 0.3)
                Add(list, r, "wet", 2, name + ": the track is wet on slicks" + (canPit ? " - box for wets!" : " - be careful!"));
            else if (onWets && wet < 0.2 && sim.Weather.RainIntensity < 0.03 && lapsLeft > 2)
                Add(list, r, "dry", 1, name + ": the track is drying, the wets will overheat" + (canPit ? " - consider slicks" : ""));

            if (!onWets && sim.Weather.RainIntensity < 0.05)
            {
                double p = sim.ForecastRain(600, forecasterSkill);
                if (p >= 0.6) Add(list, r, "forecast", 1, "Radar: " + (p * 100).ToString("0") + "% chance of rain in the next 10 minutes");
            }

            if (r.Rear.Condition == TireCondition.Overheated || r.Front.Condition == TireCondition.Overheated)
                Add(list, r, "hot", 1, name + ": " + (r.Rear.Condition == TireCondition.Overheated ? "rear" : "front") + " tyre overheating - ease the pace");
            if (r.Lap >= 3 && lapsLeft > 2)
            {
                double projected = Math.Max(r.Front.Wear, r.Rear.Wear) / Math.Max(1, r.Lap) * sim.TotalLaps;
                if (projected > 0.95 && r.Pace != PaceMode.Conserve)
                    Add(list, r, "wear", 1, name + ": tyres will not last at this pace (" + (projected * 100).ToString("0") + "% wear at the flag)");
            }

            double fuel = sim.ProjectedFuelAtFinish(r);
            if (!double.IsNaN(fuel) && fuel < 0.2)
                Add(list, r, "fuel", 2, name + ": fuel critical (" + fuel.ToString("+0.0;-0.0") + " L at the flag) - switch to Save");

            var behind = sim.RiderAtPosition(r.Position + 1);
            if (behind != null && behind.Status == RaceStatus.Racing && behind.GapToAhead > 0 && behind.GapToAhead < 0.4)
                Add(list, r, "attack-" + behind.Index, 0, name + ": " + behind.Entry.Rider.ShortName + " is right behind");

            if (r.PendingLongLaps > 0)
                Add(list, r, "longlap", 1, name + ": Long Lap penalty to serve" + (r.PendingLongLaps > 1 ? " (x" + r.PendingLongLaps + ")" : ""));
            if (r.HasIssue)
                Add(list, r, "issue", 1, name + ": technical problem, bring it home");
            return list;
        }

        private static void Add(List<EngineerMessage> list, RiderRaceState r, string topic, int severity, string text)
        {
            list.Add(new EngineerMessage { Key = r.Index + ":" + topic, RiderIndex = r.Index, Text = text, Severity = severity });
        }
    }
}
