using System;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    /// <summary>
    /// Yearly evolution of a rider: young riders grow towards their (hidden) potential, peak around their personal
    /// peak age and then decline. Racing at a higher level, good results, morale, facilities and staff speed growth up.
    /// </summary>
    public static class RiderDevelopment
    {
        private static readonly string[] Skills =
        {
            "RaceSkill", "QualifyingSkill", "Consistency", "WetSkill", "Overtaking", "Defending", "TireManagement",
            "Braking", "Cornering", "ThrottleControl", "Adaptability", "Starts", "TechnicalFeedback", "RiskManagement"
        };

        public static void EndOfSeason(WorldState world, GameDatabase db, Rider rider, DeterministicRandom rng)
        {
            if (rider.IsRetired) return;
            var date = new DateTime(world.Season, 12, 31);
            double age = rider.AgeOn(date);
            var a = rider.Attributes;
            var cat = db.GetCategory(rider.CategoryId);
            var team = world.GetTeam(rider.TeamId);

            // Season context
            int starts = 0;
            double points = 0;
            foreach (var rec in rider.CareerHistory)
                if (rec.Season == world.Season)
                {
                    starts += rec.Starts;
                    points += rec.Points;
                }
            double levelBonus = cat != null ? (cat.Prestige / 100.0) * 0.8 : 0.2;
            double teamBonus = team != null ? (team.Facility(FacilityType.Simulator) + team.Facility(FacilityType.Academy)) * 0.08 : 0;
            double moraleBonus = (rider.Morale - 60) / 80.0;
            double seatBonus = starts > 0 ? 0.3 : (cat != null && !cat.Simulated ? 0.15 : -0.4);

            double headroom = MathX.Clamp((rider.Potential - a.Overall) / 15.0, -0.5, 1.3);
            double years = rider.PeakAge - age;
            double baseGrowth;
            if (years > 6) baseGrowth = 2.2;
            else if (years > 3) baseGrowth = 1.6;
            else if (years > 0) baseGrowth = 0.8;
            else if (years > -3) baseGrowth = 0.0;
            else if (years > -6) baseGrowth = -0.9;
            else baseGrowth = -1.8 + (years + 6) * 0.25;

            foreach (var name in Skills)
            {
                double v = a.Get(name);
                double g = baseGrowth;
                if (g > 0) g *= Math.Max(0, headroom) * (1 + levelBonus + teamBonus + moraleBonus * 0.5 + seatBonus);
                else g *= 1 - Math.Min(0.5, a.Fitness / 300.0);
                g += rng.Gaussian(0, 1.0);
                a.Set(name, v + g);
            }

            // Physical decline starts earlier; experience and mentality keep growing.
            double fit = age < 29 ? rng.Gaussian(0.8, 1) : rng.Gaussian(-0.4 - (age - 29) * 0.25, 1);
            a.Fitness = MathX.Clamp(a.Fitness + fit, 1, 100);
            a.Experience = MathX.Clamp(a.Experience + 1.5 + starts * 0.35 + rng.Range(0, 1), 1, 100);
            if (age < 34) a.Mentality = MathX.Clamp(a.Mentality + rng.Gaussian(0.6, 1.0), 1, 100);
            if (age > 28) a.Aggression = MathX.Clamp(a.Aggression - rng.Range(0, 0.8), 1, 100);

            // Late bloomers and busts: young riders' potential still moves.
            if (age < 23) rider.Potential = MathX.Clamp(rider.Potential + rng.Gaussian(0, 1.6), 25, 99);
            if (rider.Potential < a.Overall) rider.Potential = a.Overall;

            // Reputation follows results relative to the level of the category.
            double prestige = cat != null ? cat.Prestige : 20;
            var champ = world.Championships.ContainsKey(rider.CategoryId ?? "") ? world.Championships[rider.CategoryId] : null;
            int pos = champ != null ? champ.RiderPosition(rider.Id) : 0;
            double target = prestige * 0.55 + (pos > 0 ? Math.Max(0, 30 - pos * 2.2) : 0) + a.Overall * 0.2;
            rider.Reputation = MathX.Clamp(rider.Reputation * 0.65 + target * 0.35, 1, 100);

            rider.Morale = MathX.Clamp(rider.Morale * 0.6 + 65 * 0.4, 5, 100);
            rider.Personality.Motivation = MathX.Clamp(rider.Personality.Motivation + (pos > 0 && pos <= 3 ? 3 : 0) - (age > 33 ? rng.Range(0, 4) : rng.Range(-1, 1.5)), 5, 100);
            if (rider.InjuryRoundsOut > 0) rider.InjuryRoundsOut = 0;
        }
    }

    /// <summary>
    /// Retirement is never a fixed age: age drives a probability that is moved by results, offers, motivation,
    /// injuries and career length. Young riders stuck without a seat can also quit the sport.
    /// </summary>
    public static class RetirementModel
    {
        public static double Probability(WorldState world, GameDatabase db, Rider rider, bool hasSeatNextSeason)
        {
            double age = rider.AgeOn(new DateTime(world.Season, 12, 31));
            int seasons = 0;
            bool recentTitle = false;
            double recentPoints = 0;
            foreach (var r in rider.CareerHistory)
            {
                seasons++;
                if (r.Season >= world.Season - 1 && r.ChampionshipPosition == 1) recentTitle = true;
                if (r.Season == world.Season) recentPoints += r.Points;
            }
            var cat = db.GetCategory(rider.CategoryId);
            double logit = (age - 38.0) * 0.55;
            logit += hasSeatNextSeason ? -1.6 : 1.0;
            logit += (55 - rider.Personality.Motivation) / 22.0;
            logit -= (rider.Personality.Ambition - 50) / 60.0;
            if (seasons > 16) logit += 0.4;
            if (recentTitle) logit -= 1.2;
            if (rider.InjuryRoundsOut > 2) logit += 0.6;
            if (cat != null && cat.Tier == 1 && recentPoints < 20 && age > 30) logit += 0.5;
            double p = MathX.Logistic(logit);

            // Young riders who never break through.
            if (age >= 18 && age <= 27 && !hasSeatNextSeason && rider.SeasonsWithoutSeat >= 1 && rider.Attributes.Overall < 55)
                p = Math.Max(p, 0.25 + 0.15 * rider.SeasonsWithoutSeat);
            if (cat != null && !cat.Simulated && age > cat.MaxAge) p = Math.Max(p, 0.6);
            return MathX.Clamp01(p);
        }

        public static void Retire(WorldState world, Rider rider, string reason)
        {
            rider.IsRetired = true;
            rider.RetiredSeason = world.Season;
            var team = world.GetTeam(rider.TeamId);
            if (team != null) team.RiderIds.Remove(rider.Id);
            int wins = rider.TotalWins();
            if (rider.Reputation > 45 || wins > 0)
                world.AddNews(rider.FullName + " retires", rider.FullName + " (" + rider.Nationality + ") ends a career with " + wins + " wins and " + rider.TotalPodiums() + " podiums. " + reason, rider.CategoryId, rider.Reputation > 70);
            rider.TeamId = null;
            rider.Contract = null;
        }
    }

    /// <summary>
    /// What the player knows about other riders. Knowledge grows with scouting assignments and by racing in the
    /// same category; displayed values are ranges that narrow as knowledge grows.
    /// </summary>
    public static class ScoutingService
    {
        public static void Perceive(WorldState world, string teamId, Rider rider, string attribute, out double estimate, out double min, out double max)
        {
            double truth = attribute == "Potential" ? rider.Potential : rider.Attributes.Get(attribute);
            double k = rider.TeamId == teamId && teamId != null ? 1.0 : world.GetKnowledge(teamId, rider.Id);
            var vis = attribute == "Potential" ? AttributeVisibility.Hidden : RiderAttributes.GetVisibility(attribute);
            double maxError = vis == AttributeVisibility.Visible ? 6 : vis == AttributeVisibility.Partial ? 16 : 28;
            double error = maxError * (1 - k);
            ulong h = DeterministicRandom.Combine(DeterministicRandom.Hash(teamId ?? "-"), DeterministicRandom.Hash(rider.Id + attribute));
            double noise = ((h >> 11) * (1.0 / 9007199254740992.0)) * 2 - 1;
            estimate = MathX.Clamp(truth + noise * error * 0.6, 1, 100);
            min = MathX.Clamp(estimate - error * 0.6, 1, 100);
            max = MathX.Clamp(estimate + error * 0.6, 1, 100);
        }

        public static bool IsKnown(WorldState world, string teamId, Rider rider, string attribute)
        {
            if (rider.TeamId == teamId) return true;
            var vis = RiderAttributes.GetVisibility(attribute);
            double k = world.GetKnowledge(teamId, rider.Id);
            return vis == AttributeVisibility.Visible || (vis == AttributeVisibility.Partial && k > 0.3) || k > 0.7;
        }

        public static void AfterRound(WorldState world, GameDatabase db)
        {
            var player = world.PlayerTeam;
            if (player == null) return;
            double scout = Economy.StaffService.BestSkill(world, player, StaffRole.Scout, 30);
            double dataCenter = player.Facility(FacilityType.DataCenter);
            foreach (var a in world.Scouting)
            {
                if (a.TeamId != player.Id) continue;
                double k = world.GetKnowledge(player.Id, a.RiderId);
                world.SetKnowledge(player.Id, a.RiderId, k + (0.06 + scout / 900.0 + dataCenter * 0.01) * (1 - k * 0.5));
            }
            // Passive knowledge of riders in the same paddock.
            foreach (var r in world.Riders.Values)
            {
                if (r.IsRetired || r.CategoryId == null) continue;
                bool samePaddock = r.CategoryId == player.CategoryId;
                double gain = samePaddock ? 0.025 : (r.Reputation > 60 ? 0.01 : 0);
                if (gain <= 0) continue;
                double k = world.GetKnowledge(player.Id, r.Id);
                if (k < 0.75) world.SetKnowledge(player.Id, r.Id, k + gain);
            }
        }
    }
}
