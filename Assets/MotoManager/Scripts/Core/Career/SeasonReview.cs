using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    public enum BoardMood
    {
        Delighted,
        Satisfied,
        Concerned,
        Unhappy
    }

    /// <summary>Why the organisation sees the season as it does (the UI turns codes into sentences).</summary>
    public enum ReviewReason
    {
        ObjectiveMet,
        ObjectiveMissed,
        WellAboveTarget,
        FarBelowTarget,
        Title,
        RaceWins,
        Profit,
        SmallLoss,
        BigLoss,
        OverBudget,
        WithinBudget
    }

    public class ReviewRider
    {
        public string RiderId;
        public string Name;
        public int Position;
        public double Points;
        public int Wins;
        public int Podiums;
    }

    /// <summary>The player's season in a nutshell, as the owners see it, plus who would like to hire the manager.</summary>
    public class PlayerSeasonReview
    {
        public int Season;
        public string TeamId;
        public string CategoryId;
        public int TeamPosition;
        public int TeamsInCategory;
        public TeamObjective Objective;
        public int TargetPosition;
        public bool ObjectiveMet;
        public readonly List<ReviewRider> Riders = new List<ReviewRider>();
        /// <summary>Organisation cash flow over the season (all ledger cash movements).</summary>
        public double NetCash;
        public double BudgetAllocated;
        public double BudgetSpent;
        /// <summary>0-100: the owners' confidence in the manager after this season.</summary>
        public int Confidence;
        public BoardMood Mood;
        public readonly List<ReviewReason> Reasons = new List<ReviewReason>();
        public readonly List<JobOffer> Offers = new List<JobOffer>();
        /// <summary>What happened to the manager's job (continues, renewed, contract ended, sacked).</summary>
        public JobOutcome Outcome;
        public double Severance;
        public double ReputationChange;
    }

    public static class SeasonReviewService
    {
        /// <summary>
        /// Builds the review of the player's team. Call at season end after the championships are final and the
        /// team objectives have been reviewed, before the books are closed.
        /// </summary>
        public static PlayerSeasonReview Build(WorldState world, GameDatabase db)
        {
            var team = world.PlayerTeam;
            if (team == null) return null;
            var review = new PlayerSeasonReview { Season = world.Season, TeamId = team.Id, CategoryId = team.CategoryId };
            ChampionshipState champ;
            world.Championships.TryGetValue(team.CategoryId, out champ);
            var p = TeamAI.Of(team);
            if (champ != null)
            {
                var teams = champ.SortedTeams();
                review.TeamsInCategory = teams.Count;
                for (int i = 0; i < teams.Count; i++) if (teams[i].Id == team.Id) review.TeamPosition = i + 1;
                foreach (var e in champ.SortedRiders())
                {
                    var r = world.GetRider(e.Id);
                    if (r == null || r.TeamId != team.Id) continue;
                    review.Riders.Add(new ReviewRider { RiderId = r.Id, Name = r.FullName, Position = champ.RiderPosition(r.Id), Points = e.Points, Wins = e.Wins, Podiums = e.Podiums });
                }
            }
            review.Objective = p.LastObjective;
            review.ObjectiveMet = p.LastObjectiveMet;
            review.TargetPosition = TeamAI.TargetPosition(p.LastObjective, Math.Max(1, review.TeamsInCategory));

            foreach (var kv in Accounts.TotalsFor(world, team.OrganisationId, world.Season)) review.NetCash += kv.Value.Cash;
            review.BudgetAllocated = team.SeasonBudget;
            review.BudgetSpent = Accounts.SpentThisSeason(world, team);

            // The owners' view: the objective first, then how far from the target, results and money.
            double score = 50;
            review.Reasons.Add(review.ObjectiveMet ? ReviewReason.ObjectiveMet : ReviewReason.ObjectiveMissed);
            score += review.ObjectiveMet ? 22 : -18;
            if (review.TeamPosition > 0)
            {
                int diff = review.TargetPosition - review.TeamPosition;
                if (diff >= 3) { score += 10; review.Reasons.Add(ReviewReason.WellAboveTarget); }
                else if (diff <= -4) { score -= 12; review.Reasons.Add(ReviewReason.FarBelowTarget); }
            }
            bool title = review.Riders.Exists(r => r.Position == 1);
            int wins = 0;
            foreach (var r in review.Riders) wins += r.Wins;
            if (title) { score += 15; review.Reasons.Add(ReviewReason.Title); }
            else if (wins > 0) { score += 5; review.Reasons.Add(ReviewReason.RaceWins); }
            double scale = Math.Max(1, review.BudgetAllocated);
            if (review.NetCash >= 0) { score += 8; review.Reasons.Add(ReviewReason.Profit); }
            else if (review.NetCash > -0.15 * scale) { score -= 3; review.Reasons.Add(ReviewReason.SmallLoss); }
            else { score -= 14; review.Reasons.Add(ReviewReason.BigLoss); }
            if (review.BudgetSpent > review.BudgetAllocated * 1.05) { score -= 8; review.Reasons.Add(ReviewReason.OverBudget); }
            else review.Reasons.Add(ReviewReason.WithinBudget);

            review.Confidence = (int)Math.Round(MathX.Clamp(score, 0, 100));
            review.Mood = review.Confidence >= 75 ? BoardMood.Delighted : review.Confidence >= 55 ? BoardMood.Satisfied : review.Confidence >= 35 ? BoardMood.Concerned : BoardMood.Unhappy;
            return review;
        }

        /// <summary>Teams interested in the player's manager after the season (best first).</summary>
        public static void AddOffers(WorldState world, GameDatabase db, PlayerSeasonReview review)
        {
            var m = world.PlayerManager;
            if (review == null || m == null) return;
            review.Offers.AddRange(CareerService.JobOffers(world, db, m));
        }
    }
}
