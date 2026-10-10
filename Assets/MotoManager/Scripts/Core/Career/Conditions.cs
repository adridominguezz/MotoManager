using System;
using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    /// <summary>
    /// Sporting results that objectives, bonuses and contract clauses can refer to. One evaluator serves sponsors,
    /// rider contracts and the board, so the same result always counts the same way.
    /// </summary>
    public enum ConditionMetric
    {
        /// <summary>Race: a finish in the top N (threshold = N).</summary>
        RaceFinishTopN,
        Wins,
        Podiums,
        Poles,
        FastestLaps,
        /// <summary>Season: championship points of at least the threshold.</summary>
        Points,
        /// <summary>Season: championship position N or better (threshold = N).</summary>
        ChampionshipPositionTopN
    }

    /// <summary>What a team (or a rider) achieved in one event's races.</summary>
    public class RaceOutcome
    {
        public int BestPosition;
        public int TopNFinishes(int n)
        {
            int c = 0;
            foreach (var p in Positions) if (p <= n) c++;
            return c;
        }
        public readonly List<int> Positions = new List<int>();
        public int Wins;
        public int Podiums;
        public int FastestLaps;
        public int Poles;
    }

    /// <summary>What a team (or a rider) achieved over a season.</summary>
    public class SeasonOutcome
    {
        public double Points;
        public int ChampionshipPosition;
        public int Wins;
        public int Podiums;
        public int Poles;
    }

    public static class ConditionEvaluator
    {
        /// <summary>How many times a per-race condition was achieved at an event (0 if not).</summary>
        public static int CountInRace(ConditionMetric metric, double threshold, RaceOutcome o)
        {
            switch (metric)
            {
                case ConditionMetric.RaceFinishTopN: return o.BestPosition > 0 && o.BestPosition <= threshold ? 1 : 0;
                case ConditionMetric.Wins: return o.Wins;
                case ConditionMetric.Podiums: return o.Podiums;
                case ConditionMetric.FastestLaps: return o.FastestLaps;
                case ConditionMetric.Poles: return o.Poles;
                default: return 0;
            }
        }

        /// <summary>Whether a season-long condition was met.</summary>
        public static bool MetInSeason(ConditionMetric metric, double threshold, SeasonOutcome o)
        {
            switch (metric)
            {
                case ConditionMetric.Points: return o.Points >= threshold;
                case ConditionMetric.ChampionshipPositionTopN: return o.ChampionshipPosition > 0 && o.ChampionshipPosition <= threshold;
                case ConditionMetric.Wins: return o.Wins >= threshold;
                case ConditionMetric.Podiums: return o.Podiums >= threshold;
                case ConditionMetric.Poles: return o.Poles >= threshold;
                default: return false;
            }
        }

        /// <summary>Sponsor objective types map onto the shared metrics (null for types that are not results).</summary>
        public static ConditionMetric? FromSponsorObjective(SponsorObjectiveType type)
        {
            switch (type)
            {
                case SponsorObjectiveType.RaceFinishTopN: return ConditionMetric.RaceFinishTopN;
                case SponsorObjectiveType.Wins: return ConditionMetric.Wins;
                case SponsorObjectiveType.Podiums: return ConditionMetric.Podiums;
                case SponsorObjectiveType.SeasonPoints: return ConditionMetric.Points;
                case SponsorObjectiveType.ChampionshipPositionTopN: return ConditionMetric.ChampionshipPositionTopN;
                default: return null;
            }
        }

        public static bool IsSeasonObjective(SponsorObjectiveType type)
        {
            return type == SponsorObjectiveType.SeasonPoints || type == SponsorObjectiveType.ChampionshipPositionTopN;
        }

        /// <summary>A team's results in the main races of a weekend (sprints do not count for race objectives).</summary>
        public static RaceOutcome TeamRace(WeekendResult weekend, string teamId)
        {
            var o = new RaceOutcome();
            SessionResult lastQualifying = null;
            foreach (var session in weekend.Sessions)
            {
                if (session.Type == SessionType.Qualifying) lastQualifying = session;
                if (session.Type != SessionType.Race) continue;
                foreach (var e in session.Classification)
                {
                    if (e.TeamId != teamId || e.Status != ResultStatus.Finished) continue;
                    o.Positions.Add(e.Position);
                    if (o.BestPosition == 0 || e.Position < o.BestPosition) o.BestPosition = e.Position;
                    if (e.Position <= 3) o.Podiums++;
                    if (e.Position == 1) o.Wins++;
                    if (e.FastestLap) o.FastestLaps++;
                }
            }
            // Pole: winner of the last qualifying session (Q2 / Superpole).
            if (lastQualifying != null && lastQualifying.Classification.Count > 0 && lastQualifying.Classification[0].TeamId == teamId) o.Poles++;
            return o;
        }

        /// <summary>A team's season from its championship (team standings).</summary>
        public static SeasonOutcome TeamSeason(ChampionshipState champ, string teamId)
        {
            var o = new SeasonOutcome();
            if (champ == null) return o;
            var table = champ.SortedTeams();
            for (int i = 0; i < table.Count; i++)
            {
                if (table[i].Id != teamId) continue;
                o.ChampionshipPosition = i + 1;
                o.Points = table[i].Points;
                o.Wins = table[i].Wins;
                o.Podiums = table[i].Podiums;
                o.Poles = table[i].Poles;
            }
            return o;
        }
    }
}
