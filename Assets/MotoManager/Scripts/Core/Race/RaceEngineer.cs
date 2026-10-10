using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
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
                Add(list, r, "wet", 2, (canPit ? Txt.F("eng.wetSwap", "{0}: the track is wet on slicks - expect him to come in for the wet bike", name) : Txt.F("eng.wet", "{0}: the track is wet on slicks - be careful!", name)));
            else if (onWets && wet < 0.2 && sim.Weather.RainIntensity < 0.03 && lapsLeft > 2)
                Add(list, r, "dry", 1, Txt.F("eng.drying", "{0}: the track is drying, the wets will overheat - save them", name));

            if (!onWets && sim.Weather.RainIntensity < 0.05)
            {
                double p = sim.ForecastRain(600, forecasterSkill);
                if (p >= 0.6) Add(list, r, "forecast", 1, Txt.F("eng.radar", "Radar: {0}% chance of rain in the next 10 minutes", (p * 100).ToString("0")));
            }

            if (r.Rear.Condition == TireCondition.Overheated || r.Front.Condition == TireCondition.Overheated)
                Add(list, r, "hot", 1, (r.Rear.Condition == TireCondition.Overheated ? Txt.F("eng.hotRear", "{0}: rear tyre overheating - ease the pace", name) : Txt.F("eng.hotFront", "{0}: front tyre overheating - ease the pace", name)));
            if (r.Lap >= 3 && lapsLeft > 2)
            {
                double projected = Math.Max(r.Front.Wear, r.Rear.Wear) / Math.Max(1, r.Lap) * sim.TotalLaps;
                if (projected > 0.95 && r.Pace != PaceMode.Conserve)
                    Add(list, r, "wear", 1, Txt.F("eng.wear", "{0}: tyres will not last at this pace ({1}% wear at the flag)", name, (projected * 100).ToString("0")));
            }

            double fuel = sim.ProjectedFuelAtFinish(r);
            if (!double.IsNaN(fuel) && fuel < 0.2)
                Add(list, r, "fuel", 2, Txt.F("eng.fuel", "{0}: fuel critical ({1} L at the flag) - switch to Save", name, fuel.ToString("+0.0;-0.0", Txt.Culture)));

            var behind = sim.RiderAtPosition(r.Position + 1);
            if (behind != null && behind.Status == RaceStatus.Racing && behind.GapToAhead > 0 && behind.GapToAhead < 0.4)
                Add(list, r, "attack-" + behind.Index, 0, Txt.F("eng.behind", "{0}: {1} is right behind", name, behind.Entry.Rider.ShortName));

            if (r.PendingLongLaps > 0)
                Add(list, r, "longlap", 1, Txt.F("eng.longLap", "{0}: Long Lap penalty to serve (x{1})", name, r.PendingLongLaps));
            if (r.HasIssue)
                Add(list, r, "issue", 1, Txt.F("eng.issue", "{0}: technical problem, bring it home", name));
            return list;
        }

        private static void Add(List<EngineerMessage> list, RiderRaceState r, string topic, int severity, string text)
        {
            list.Add(new EngineerMessage { Key = r.Index + ":" + topic, RiderIndex = r.Index, Text = text, Severity = severity });
        }
    }
}
