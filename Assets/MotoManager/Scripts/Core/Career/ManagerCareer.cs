using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Season;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    /// <summary>What happened to the player's job at the end of the season.</summary>
    public enum JobOutcome
    {
        None,
        Continues,
        Renewed,
        Expired,
        Sacked
    }

    /// <summary>
    /// The player's career as a manager between seasons and during them: the owners' confidence, the renewal offer
    /// in the last year of a contract, the sack, contracts that run out, changing jobs and founding a team.
    /// The manager's own money is personal wealth (outside any organisation).
    /// </summary>
    public static class ManagerCareer
    {
        /// <summary>Below this confidence at the end of a season an employed manager is sacked.</summary>
        public const double SackBelow = 30;
        /// <summary>From this confidence the owners offer a renewal in the last season of the contract.</summary>
        public const double RenewFrom = 45;

        /// <summary>The player's job right now (null when without a team).</summary>
        public static ManagerContract Job(WorldState world)
        {
            var m = world.PlayerManager;
            return m != null ? m.Job : null;
        }

        /// <summary>
        /// The owners' confidence now: last season's view, moved by where the team stands against this season's
        /// target (each place above the target adds, each place below takes away).
        /// </summary>
        public static double CurrentConfidence(WorldState world, GameDatabase db)
        {
            var m = world.PlayerManager;
            if (m == null || m.Job == null) return 0;
            if (m.Job.Role == ManagerRole.OwnerManager) return 100;
            var team = world.GetTeam(m.Job.TeamId);
            if (team == null) return m.Job.BoardConfidence;
            ChampionshipState champ;
            if (!world.Championships.TryGetValue(team.CategoryId, out champ) || world.EventsCompleted == 0) return m.Job.BoardConfidence;
            var teams = champ.SortedTeams();
            int pos = 0;
            for (int i = 0; i < teams.Count; i++) if (teams[i].Id == team.Id) pos = i + 1;
            if (pos == 0) return m.Job.BoardConfidence;
            int target = TeamAI.TargetPosition(TeamAI.Of(team).Objective, teams.Count);
            return MathX.Clamp(m.Job.BoardConfidence + (target - pos) * 3, 0, 100);
        }

        /// <summary>
        /// In the last season of an employee's contract, the owners' offer to stay (null if they are not happy enough,
        /// if the contract is not ending, or if the player owns the team).
        /// </summary>
        public static JobOffer RenewalOffer(WorldState world, GameDatabase db)
        {
            var m = world.PlayerManager;
            if (m == null || m.Job == null || m.Job.Role != ManagerRole.TeamManager || m.Job.EndSeason > world.Season) return null;
            var team = world.GetTeam(m.Job.TeamId);
            if (team == null) return null;
            double conf = CurrentConfidence(world, db);
            if (conf < RenewFrom) return null;
            double raise = 1 + (conf - 50) / 250.0;
            return new JobOffer
            {
                TeamId = team.Id, OrganisationId = team.OrganisationId, CategoryId = team.CategoryId,
                SalaryPerSeason = Math.Round(m.Job.SalaryPerSeason * Math.Max(0.95, raise) / 1000) * 1000,
                Seasons = conf >= 70 ? 3 : 2, Autonomy = conf >= 75 && m.Job.Autonomy == Autonomy.Limited ? Autonomy.Standard : m.Job.Autonomy,
                AssignedBudget = team.SeasonBudget, Objective = TeamAI.Of(team).Objective
            };
        }

        /// <summary>Signs the renewal: the contract runs on from next season with the new terms.</summary>
        public static bool AcceptRenewal(WorldState world, GameDatabase db)
        {
            var offer = RenewalOffer(world, db);
            var m = world.PlayerManager;
            if (offer == null) return false;
            m.Job.EndSeason = world.Season + offer.Seasons;
            m.Job.SalaryPerSeason = offer.SalaryPerSeason;
            m.Job.Autonomy = offer.Autonomy;
            m.Job.RenewedSeason = world.Season;
            var team = world.GetTeam(m.Job.TeamId);
            world.AddNews(Txt.F("news.managerRenews", "{0} renews as manager of {1}", m.FullName, team != null ? team.Name : ""),
                Txt.F("news.contractUntil", "Contract until {0}.", m.Job.EndSeason), team != null ? team.CategoryId : null, true);
            return true;
        }

        /// <summary>Offers from other teams (the current one excluded).</summary>
        public static List<JobOffer> Offers(WorldState world, GameDatabase db)
        {
            var m = world.PlayerManager;
            var list = new List<JobOffer>();
            if (m == null) return list;
            foreach (var o in CareerService.JobOffers(world, db, m))
                if (m.Job == null || o.TeamId != m.Job.TeamId) list.Add(o);
            return list;
        }

        /// <summary>
        /// Takes another job now. Walking out of a running contract costs some reputation; an owner keeps owning
        /// his organisation (an AI manager runs that team).
        /// </summary>
        public static CareerResult ChangeJob(WorldState world, GameDatabase db, JobOffer offer)
        {
            var m = world.PlayerManager;
            if (m == null) return new CareerResult { Message = "" };
            bool breaking = m.Job != null && m.Job.Role == ManagerRole.TeamManager && m.Job.EndSeason > world.Season;
            var old = m.Job != null ? world.GetTeam(m.Job.TeamId) : null;
            var result = CareerService.AcceptJob(world, db, m, offer);
            if (!result.Ok) return result;
            if (breaking) m.Reputation = MathX.Clamp(m.Reputation - 3, 0, 100);
            m.Job.BoardConfidence = 60;
            if (old != null) CareerService.EnsureAiManagers(world, db, world.Rng.Fork("manager-" + old.Id + "-" + world.Season));
            return result;
        }

        /// <summary>Leaves the current job (the team gets an AI manager). The player follows the championships without a team.</summary>
        public static void Resign(WorldState world, GameDatabase db)
        {
            var m = world.PlayerManager;
            if (m == null || m.Job == null) return;
            bool breaking = m.Job.Role == ManagerRole.TeamManager && m.Job.EndSeason > world.Season;
            var team = world.GetTeam(m.Job.TeamId);
            CareerService.EndJob(world, m, "Resigned");
            if (breaking) m.Reputation = MathX.Clamp(m.Reputation - 3, 0, 100);
            LeaveTeam(world, db, team);
        }

        private static void LeaveTeam(WorldState world, GameDatabase db, Team team)
        {
            if (team != null) team.IsPlayer = false;
            if (world.PlayerTeamId == (team != null ? team.Id : null)) world.PlayerTeamId = null;
            if (team != null)
            {
                var org = world.OrganisationOf(team);
                if (org != null && org.Owner != OwnerKind.Player) org.DetailedLedger = false;
                CareerService.EnsureAiManagers(world, db, world.Rng.Fork("manager-" + team.Id + "-" + world.Season));
            }
        }

        /// <summary>
        /// A new team can only be founded between seasons (all events of the season run, or none yet), so it
        /// enters a championship from its first round.
        /// </summary>
        public static bool CanFoundNow(WorldState world, GameController game)
        {
            var m = world.PlayerManager;
            if (m == null) return false;
            if (m.Job != null && m.Job.Role == ManagerRole.OwnerManager) return false;
            return world.EventsCompleted == 0 || game.IsSeasonOver;
        }

        /// <summary>
        /// End of season for the player's job: confidence and reputation from the season review, then the contract:
        /// it runs on, ends (not renewed) or the manager is sacked. Fills the review with the outcome.
        /// </summary>
        public static void SeasonEnd(WorldState world, GameDatabase db, PlayerSeasonReview review)
        {
            var m = world.PlayerManager;
            if (m == null || review == null) return;
            double rep = 0;
            if (review.ObjectiveMet) rep += 3; else rep -= 2;
            if (review.Reasons.Contains(ReviewReason.Title)) rep += 6;
            else if (review.Reasons.Contains(ReviewReason.RaceWins)) rep += 1.5;
            if (review.Reasons.Contains(ReviewReason.WellAboveTarget)) rep += 2;
            if (review.Reasons.Contains(ReviewReason.FarBelowTarget)) rep -= 2;
            if (review.Reasons.Contains(ReviewReason.BigLoss)) rep -= 1.5;

            var job = m.Job;
            if (job == null)
            {
                review.Outcome = JobOutcome.None;
                return;
            }
            job.BoardConfidence = MathX.Clamp(job.BoardConfidence * 0.4 + review.Confidence * 0.6, 0, 100);
            review.Confidence = (int)Math.Round(job.BoardConfidence);
            var team = world.GetTeam(job.TeamId);
            if (job.Role == ManagerRole.OwnerManager)
            {
                review.Outcome = JobOutcome.Continues;
            }
            else if (job.BoardConfidence < SackBelow && job.EndSeason > world.Season)
            {
                double severance = Math.Round(job.SalaryPerSeason * job.SeveranceSeasons);
                m.PersonalWealth += severance;
                review.Severance = severance;
                review.Outcome = JobOutcome.Sacked;
                rep -= 4;
                CareerService.EndJob(world, m, "Sacked");
                world.AddNews(Txt.F("news.managerSacked", "{0} sacks its manager {1}", team != null ? team.Name : "", m.FullName), "", team != null ? team.CategoryId : null, true);
                LeaveTeam(world, db, team);
            }
            else if (job.EndSeason <= world.Season)
            {
                review.Outcome = JobOutcome.Expired;
                CareerService.EndJob(world, m, "ContractEnded");
                world.AddNews(Txt.F("news.managerLeaves", "{0} leaves {1}: contract not renewed", m.FullName, team != null ? team.Name : ""), "", team != null ? team.CategoryId : null, true);
                LeaveTeam(world, db, team);
            }
            else review.Outcome = job.RenewedSeason == world.Season ? JobOutcome.Renewed : JobOutcome.Continues;
            review.Mood = review.Confidence >= 75 ? BoardMood.Delighted : review.Confidence >= 55 ? BoardMood.Satisfied : review.Confidence >= 35 ? BoardMood.Concerned : BoardMood.Unhappy;
            m.Reputation = MathX.Clamp(m.Reputation + rep, 0, 100);
            review.ReputationChange = rep;
        }
    }
}
