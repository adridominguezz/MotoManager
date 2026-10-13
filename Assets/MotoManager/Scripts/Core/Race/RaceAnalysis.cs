using System;
using System.Collections.Generic;
using MotoManager.Core.Data;

namespace MotoManager.Core.Race
{
    public enum AnalysisCode
    {
        /// <summary>Value: seconds per lap gained (+) or lost (-) on straights (power, top speed, braking).</summary>
        Straights,
        SlowCorners,
        MediumCorners,
        FastCorners,
        /// <summary>Value: km/h above (+) or below (-) the field's average best top speed.</summary>
        TopSpeed,
        /// <summary>Value: seconds per lap of pace lost in the last third versus the first third, relative to the field (+ = better).</summary>
        TireDegradation,
        /// <summary>Value: rear wear at the flag relative to the field, in percentage points (+ = less wear).</summary>
        TireWear,
        /// <summary>Value: overtakes made.</summary>
        Overtaking,
        /// <summary>Value: overtake attempts that did not work.</summary>
        FailedOvertakes,
        /// <summary>Value: times passed.</summary>
        Defending,
        /// <summary>Value: number of mistakes.</summary>
        Mistakes,
        /// <summary>Value: positions gained (+) or lost (-) on the opening lap.</summary>
        Start,
        /// <summary>Value: 1 when a technical problem hurt the race.</summary>
        Reliability,
        /// <summary>Value: number of penalties.</summary>
        Penalties
    }

    public class AnalysisItem
    {
        public AnalysisCode Code;
        public double Value;
        public bool Strength;
        /// <summary>Approximate impact in seconds over the race, used to sort the list.</summary>
        public double Weight;
    }

    public class RiderReport
    {
        public int RiderIndex;
        public int Grid;
        public int Finish;
        public int PositionsGained;
        public double BestLap;
        public double[] BestSectors;
        public double TopSpeedKph;
        public int Overtakes;
        public int TimesPassed;
        public int Errors;
        public int Penalties;
        public bool TechnicalProblem;
        public readonly List<AnalysisItem> Strengths = new List<AnalysisItem>();
        public readonly List<AnalysisItem> Weaknesses = new List<AnalysisItem>();
    }

    /// <summary>
    /// "Why did we perform like this?" Compares a rider with the rest of the field using what happened in the
    /// race: speed by type of track zone (converted to seconds per lap), top speed, tire degradation, overtaking,
    /// mistakes, start, reliability and penalties. Produces codes and values; the UI turns them into text.
    /// </summary>
    public static class RaceAnalysis
    {
        public static RiderReport Analyze(RaceSimulation sim, int riderIndex, int maxItems = 4)
        {
            var r = sim.Riders[riderIndex];
            var st = r.Stats;
            var report = new RiderReport
            {
                RiderIndex = riderIndex,
                Grid = r.Entry.GridPosition,
                Finish = r.Position,
                PositionsGained = r.Entry.GridPosition - r.Position,
                BestLap = r.BestLapTime < double.MaxValue ? r.BestLapTime : 0,
                BestSectors = st.BestSectors,
                TopSpeedKph = st.TopSpeed * 3.6,
                Overtakes = st.OvertakesMade,
                TimesPassed = st.OvertakesSuffered,
                Errors = st.Errors + st.RunOffs,
                Penalties = r.Penalties.Count,
                TechnicalProblem = r.HasIssue || (r.RetireReason != null && r.RetireReason.Contains("failure"))
            };
            var items = new List<AnalysisItem>();
            var others = new List<RiderRaceState>();
            foreach (var o in sim.Riders)
                if (o != r && o.Stats.ZoneTime[0] > 30) others.Add(o);
            int laps = Math.Max(1, sim.TotalLaps);

            // Zone pace: lap distance in each zone x (1/v_field - 1/v_rider) = seconds per lap.
            var track = sim.Track;
            var zoneLength = new double[RaceStats.ZoneCount];
            foreach (var seg in track.Segments) zoneLength[(int)RaceSimulation.ZoneOf(seg)] += seg.Length;
            for (int z = 0; z < RaceStats.ZoneCount; z++)
            {
                double mine = st.ZoneSpeed((TrackZone)z);
                double field = Average(others, o => o.Stats.ZoneSpeed((TrackZone)z));
                if (double.IsNaN(mine) || double.IsNaN(field) || zoneLength[z] < 50) continue;
                double perLap = zoneLength[z] * (1 / field - 1 / mine);
                Add(items, (AnalysisCode)z, perLap, perLap * laps, 0.04);
            }

            double fieldTop = Average(others, o => o.Stats.TopSpeed);
            if (!double.IsNaN(fieldTop) && st.TopSpeed > 0)
            {
                double kph = (st.TopSpeed - fieldTop) * 3.6;
                Add(items, AnalysisCode.TopSpeed, kph, kph * 0.05 * laps, 2.0);
            }

            // Degradation: last third versus first third of the clean laps, compared with the field.
            double myDrop = PaceDrop(st.LapTimes);
            double fieldDrop = Average(others, o => PaceDrop(o.Stats.LapTimes));
            if (!double.IsNaN(myDrop) && !double.IsNaN(fieldDrop))
            {
                double better = fieldDrop - myDrop;
                Add(items, AnalysisCode.TireDegradation, better, better * laps / 3, 0.08);
            }
            double fieldWear = Average(others, o => o.Rear.Wear);
            if (!double.IsNaN(fieldWear))
            {
                double pts = (fieldWear - r.Rear.Wear) * 100;
                Add(items, AnalysisCode.TireWear, pts, pts * 0.05, 6);
            }

            double fieldOvertakes = Average(others, o => o.Stats.OvertakesMade);
            if (st.OvertakesMade >= fieldOvertakes + 2) items.Add(new AnalysisItem { Code = AnalysisCode.Overtaking, Value = st.OvertakesMade, Strength = true, Weight = st.OvertakesMade * 0.4 });
            int failed = st.OvertakesFailed;
            double fieldFailed = Average(others, o => o.Stats.OvertakesFailed);
            if (failed >= 6 && failed > 3 * st.OvertakesMade && failed > fieldFailed * 1.3)
                items.Add(new AnalysisItem { Code = AnalysisCode.FailedOvertakes, Value = failed, Strength = false, Weight = failed * 0.15 });
            double fieldPassed = Average(others, o => o.Stats.OvertakesSuffered);
            if (st.OvertakesSuffered >= fieldPassed + 2) items.Add(new AnalysisItem { Code = AnalysisCode.Defending, Value = st.OvertakesSuffered, Strength = false, Weight = st.OvertakesSuffered * 0.4 });
            else if (st.OvertakesSuffered == 0 && r.Position > 1 && r.Status == RaceStatus.Finished) items.Add(new AnalysisItem { Code = AnalysisCode.Defending, Value = 0, Strength = true, Weight = 0.5 });

            int mistakes = st.Errors + st.RunOffs;
            double fieldErrors = Average(others, o => o.Stats.Errors + o.Stats.RunOffs);
            if (mistakes >= fieldErrors + 2) items.Add(new AnalysisItem { Code = AnalysisCode.Mistakes, Value = mistakes, Strength = false, Weight = mistakes * 0.6 });
            else if (mistakes == 0 && !double.IsNaN(fieldErrors) && fieldErrors >= 1) items.Add(new AnalysisItem { Code = AnalysisCode.Mistakes, Value = 0, Strength = true, Weight = 0.6 });

            if (st.PositionAfterLapOne > 0)
            {
                int gained = r.Entry.GridPosition - st.PositionAfterLapOne;
                if (Math.Abs(gained) >= 2) items.Add(new AnalysisItem { Code = AnalysisCode.Start, Value = gained, Strength = gained > 0, Weight = Math.Abs(gained) * 0.8 });
            }
            if (report.TechnicalProblem) items.Add(new AnalysisItem { Code = AnalysisCode.Reliability, Value = 1, Strength = false, Weight = 50 });
            if (r.Penalties.Count > 0) items.Add(new AnalysisItem { Code = AnalysisCode.Penalties, Value = r.Penalties.Count, Strength = false, Weight = 3 * r.Penalties.Count });

            items.Sort((a, b) => b.Weight.CompareTo(a.Weight));
            foreach (var it in items)
            {
                var list = it.Strength ? report.Strengths : report.Weaknesses;
                if (list.Count < maxItems) list.Add(it);
            }
            return report;
        }

        private static void Add(List<AnalysisItem> items, AnalysisCode code, double value, double weight, double threshold)
        {
            if (Math.Abs(value) < threshold) return;
            items.Add(new AnalysisItem { Code = code, Value = value, Strength = value > 0, Weight = Math.Abs(weight) });
        }

        private static double Average(List<RiderRaceState> riders, Func<RiderRaceState, double> f)
        {
            double sum = 0;
            int n = 0;
            foreach (var r in riders)
            {
                double v = f(r);
                if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                sum += v;
                n++;
            }
            return n == 0 ? double.NaN : sum / n;
        }

        /// <summary>Median lap of the last third minus median lap of the first third (laps after the opening one).</summary>
        public static double PaceDrop(List<double> laps)
        {
            if (laps.Count < 6) return double.NaN;
            int third = laps.Count / 3;
            return Median(laps, laps.Count - third, laps.Count) - Median(laps, 1, 1 + third);
        }

        private static double Median(List<double> laps, int from, int to)
        {
            var part = new List<double>();
            for (int i = from; i < to && i < laps.Count; i++) part.Add(laps[i]);
            if (part.Count == 0) return double.NaN;
            part.Sort();
            return part[part.Count / 2];
        }
    }

    public enum TrackTrait
    {
        TopSpeed,
        HeavyBraking,
        Technical,
        FastFlowing,
        Slipstream,
        TireWear,
        Overtaking
    }

    /// <summary>
    /// What a circuit rewards, derived from its geometry and surface (never a fixed per-circuit table):
    /// share of the lap on straights, longest straight, number of heavy braking zones, slow versus fast corners,
    /// abrasiveness and overtaking opportunities.
    /// </summary>
    public class TrackProfile
    {
        public double StraightShare;
        public double LongestStraight;
        public int HeavyBrakingZones;
        public double SlowCornerShare;
        public double FastCornerShare;
        public double Abrasiveness;
        public double OvertakingScore;
        public readonly List<TrackTrait> Traits = new List<TrackTrait>();

        public static TrackProfile Compute(TrackModel track)
        {
            var p = new TrackProfile { Abrasiveness = track.Definition != null ? track.Definition.Abrasiveness : 0.5 };
            double straight = 0, slow = 0, fast = 0, run = 0;
            int corners = 0;
            // Walk the lap from the first corner so the main straight counts as one run across the finish line.
            int first = track.Segments.FindIndex(x => x.IsCorner);
            var lap = new List<TrackSegment>();
            for (int i = 0; i < track.Segments.Count; i++) lap.Add(track.Segments[(Math.Max(0, first) + i) % track.Segments.Count]);
            foreach (var s in lap)
            {
                if (!s.IsCorner)
                {
                    straight += s.Length;
                    run += s.Length;
                    if (run > p.LongestStraight) p.LongestStraight = run;
                    continue;
                }
                run = 0;
                corners++;
                var zone = RaceSimulation.ZoneOf(s);
                if (zone == TrackZone.SlowCorners) slow += s.Length;
                if (zone == TrackZone.FastCorners) fast += s.Length;
                if (s.BrakingIntensity > 0.55) p.HeavyBrakingZones++;
                p.OvertakingScore += s.OvertakingPotential;
            }
            double cornerLen = Math.Max(1, track.Length - straight);
            p.StraightShare = straight / track.Length;
            p.SlowCornerShare = slow / cornerLen;
            p.FastCornerShare = fast / cornerLen;
            p.OvertakingScore /= Math.Max(1, corners);

            // Each trait gets a score relative to its threshold; only the three clearest ones are reported.
            double km = track.Length / 1000.0;
            double longStraights = 0;
            double acc = 0;
            foreach (var s in lap)
            {
                if (!s.IsCorner) { acc += s.Length; continue; }
                if (acc > 300) longStraights += acc;
                acc = 0;
            }
            if (acc > 300) longStraights += acc;
            var scores = new List<KeyValuePair<TrackTrait, double>>
            {
                new KeyValuePair<TrackTrait, double>(TrackTrait.TopSpeed, Math.Max(p.LongestStraight / 950.0, longStraights / track.Length / 0.5)),
                new KeyValuePair<TrackTrait, double>(TrackTrait.HeavyBraking, p.HeavyBrakingZones / km / 2.0),
                new KeyValuePair<TrackTrait, double>(TrackTrait.Technical, Math.Max(corners / km / 3.6, p.SlowCornerShare / 0.45)),
                new KeyValuePair<TrackTrait, double>(TrackTrait.FastFlowing, p.FastCornerShare / 0.35),
                new KeyValuePair<TrackTrait, double>(TrackTrait.Slipstream, p.LongestStraight / 1000.0),
                new KeyValuePair<TrackTrait, double>(TrackTrait.TireWear, p.Abrasiveness / 0.6),
                new KeyValuePair<TrackTrait, double>(TrackTrait.Overtaking, p.OvertakingScore / 0.55)
            };
            scores.Sort((x, y) => y.Value.CompareTo(x.Value));
            foreach (var kv in scores)
                if (kv.Value >= 1.0 && p.Traits.Count < 3) p.Traits.Add(kv.Key);
            return p;
        }
    }
}
