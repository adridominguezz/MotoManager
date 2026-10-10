using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Economy
{
    /// <summary>
    /// Sponsor deals: what a sponsor is worth to a team, signing, per-race and season objectives, penalties and
    /// renewals. Sponsors are independent of teams; offers come from <see cref="SponsorMarket"/>.
    /// </summary>
    public static class SponsorService
    {
        /// <summary>Real sponsors from the data or sponsors invented during the game.</summary>
        public static SponsorTemplate GetTemplate(WorldState world, GameDatabase db, string id)
        {
            SponsorTemplate s;
            if (id == null) return null;
            if (db.Sponsors.TryGetValue(id, out s)) return s;
            return world.Sponsors.TryGetValue(id, out s) ? s : null;
        }

        public static List<SponsorTemplate> AllTemplates(WorldState world, GameDatabase db)
        {
            var list = new List<SponsorTemplate>(db.Sponsors.Values);
            list.AddRange(world.Sponsors.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        /// <summary>Sponsors already backing a team or with a pending offer.</summary>
        public static HashSet<string> Engaged(WorldState world)
        {
            var set = new HashSet<string>();
            foreach (var t in world.Teams.Values)
                foreach (var d in t.Sponsors) set.Add(d.SponsorId);
            foreach (var o in world.SponsorOffers) set.Add(o.SponsorId);
            return set;
        }

        /// <summary>Weight of a category for a sponsor (its weighted preference list, or its pyramid).</summary>
        public static double CategoryWeight(SponsorTemplate s, ChampionshipCategory category)
        {
            if (category == null) return 0;
            if (s.Pyramids.Count > 0 && !s.Pyramids.Contains(category.Pyramid)) return 0;
            if (s.CategoryPreference.Count > 0)
            {
                double w;
                return s.CategoryPreference.TryGetValue(category.Id, out w) ? w : 0;
            }
            // Brands without an explicit list pay according to the category's audience.
            return 0.25 + 0.75 * category.Prestige / 100.0;
        }

        /// <summary>What a sponsor would pay a team per season.</summary>
        public static double ValueFor(SponsorTemplate s, Team team, ChampionshipCategory category)
        {
            return Math.Round(s.PaymentPerSeason * CategoryWeight(s, category) * (0.7 + team.Reputation / 160.0) / 1000) * 1000;
        }

        /// <summary>
        /// How keen a sponsor is on a team (0 = not at all): category fit, reputation against its requirement,
        /// nationality ties and current form.
        /// </summary>
        public static double Interest(WorldState world, GameDatabase db, SponsorTemplate s, Team team, ChampionshipCategory category)
        {
            double weight = CategoryWeight(s, category);
            if (weight <= 0) return 0;
            if (s.HomeTeams.Count > 0 && !s.HomeTeams.Contains(team.Id) && world.ArchivedChampionships.Count == 0) return 0;
            double rep = team.Reputation >= s.MinTeamReputation ? 1 : Math.Max(0, 1 - (s.MinTeamReputation - team.Reputation) / 12.0);
            if (rep <= 0) return 0;
            double nat = 1;
            if (s.PreferredNationalities.Count > 0)
            {
                if (s.PreferredNationalities.Contains(team.Country)) nat = 1.3;
                foreach (var id in team.RiderIds)
                {
                    var r = world.GetRider(id);
                    if (r != null && s.PreferredNationalities.Contains(r.Nationality)) nat = 1.5;
                }
            }
            double form = 1;
            ChampionshipState champ;
            if (world.Championships.TryGetValue(team.CategoryId ?? "", out champ) && champ.Rounds.Count >= 3)
            {
                var o = ConditionEvaluator.TeamSeason(champ, team.Id);
                int n = Math.Max(1, champ.Teams.Count);
                if (o.ChampionshipPosition > 0) form = 0.75 + 0.5 * (1 - (o.ChampionshipPosition - 1.0) / n);
            }
            return weight * rep * nat * form;
        }

        public static int Slots(GameDatabase db, Team team, int season)
        {
            var rules = db.GetRules(team.CategoryId, season);
            return rules != null ? rules.Economy.SponsorSlots : 3;
        }

        /// <summary>Deals that will still be running in a season (signed for it or carried over).</summary>
        public static int DealsIn(Team team, int season)
        {
            int n = 0;
            foreach (var d in team.Sponsors)
                if (d.StartSeason <= season && d.EndSeason >= season) n++;
            return n;
        }

        public static bool IndustryConflict(Team team, string industry, bool exclusive, int fromSeason, int toSeason)
        {
            if (string.IsNullOrEmpty(industry)) return false;
            foreach (var d in team.Sponsors)
            {
                if (d.Industry != industry || d.EndSeason < fromSeason || d.StartSeason > toSeason) continue;
                if (exclusive || d.IndustryExclusive) return true;
            }
            return false;
        }

        /// <summary>A deal pays only in its seasons and, if conditional, only while its condition holds.</summary>
        public static bool Pays(SponsorDeal d, Team team, int season)
        {
            if (d.StartSeason > season || d.EndSeason < season) return false;
            return d.RequiredCategoryId == null || d.RequiredCategoryId == team.CategoryId;
        }

        /// <summary>Turns an accepted offer into a deal; the signing bonus is paid now (once, even if called twice).</summary>
        public static SponsorDeal Sign(WorldState world, Team team, SponsorOffer offer)
        {
            var deal = new SponsorDeal
            {
                SponsorId = offer.SponsorId,
                Name = offer.Name,
                Tier = offer.Tier,
                Industry = offer.Industry,
                PaymentPerSeason = offer.PaymentPerSeason,
                StartSeason = offer.StartSeason,
                EndSeason = offer.StartSeason + Math.Max(1, offer.Seasons) - 1,
                Objectives = new List<SponsorObjective>(offer.Objectives),
                RequiredCategoryId = offer.RequiredCategoryId,
                PenaltyOnMiss = offer.PenaltyOnMiss,
                IndustryExclusive = offer.IndustryExclusive
            };
            team.Sponsors.Add(deal);
            bool conditionMet = offer.RequiredCategoryId == null || offer.RequiredCategoryId == team.CategoryId;
            if (offer.SigningBonus > 0 && conditionMet && Accounts.TryBegin(world, "sponsor-sign|" + offer.Id))
                Accounts.Post(world, team, LedgerKind.Sponsorship, offer.SigningBonus, offer.Name + " signing");
            return deal;
        }

        /// <summary>Per-race objectives after an event. Returns the bonus earned (posted by the caller with the event).</summary>
        public static double EvaluateRace(WorldState world, Team team, RaceOutcome outcome)
        {
            double total = 0;
            foreach (var deal in team.Sponsors)
            {
                if (!Pays(deal, team, world.Season)) continue;
                foreach (var o in deal.Objectives)
                {
                    var metric = ConditionEvaluator.FromSponsorObjective(o.Type);
                    if (metric == null || ConditionEvaluator.IsSeasonObjective(o.Type) || o.BonusPerOccurrence <= 0) continue;
                    int times = ConditionEvaluator.CountInRace(metric.Value, o.Target, outcome);
                    if (times > 0)
                    {
                        double bonus = o.BonusPerOccurrence * times;
                        deal.BonusEarnedThisSeason += bonus;
                        deal.Satisfaction = Math.Min(100, deal.Satisfaction + 2);
                        total += bonus;
                    }
                    else if (o.Type == SponsorObjectiveType.RaceFinishTopN)
                    {
                        deal.Satisfaction = Math.Max(0, deal.Satisfaction - 1);
                    }
                }
            }
            return total;
        }

        /// <summary>
        /// Season end for one team: season objectives (bonus or penalty), then expiring deals. AI teams renew with
        /// sponsors that are happy; the player receives a renewal offer to accept or not. Returns the bonus earned.
        /// </summary>
        public static double EndOfSeason(WorldState world, GameDatabase db, Team team, ChampionshipCategory category, SeasonOutcome outcome, DeterministicRandom rng)
        {
            double total = 0;
            for (int i = team.Sponsors.Count - 1; i >= 0; i--)
            {
                var deal = team.Sponsors[i];
                if (!Pays(deal, team, world.Season)) continue;
                bool missed = false;
                foreach (var o in deal.Objectives)
                {
                    if (!ConditionEvaluator.IsSeasonObjective(o.Type)) continue;
                    var metric = ConditionEvaluator.FromSponsorObjective(o.Type).Value;
                    if (ConditionEvaluator.MetInSeason(metric, o.Target, outcome))
                    {
                        total += o.SeasonBonus;
                        deal.Satisfaction = Math.Min(100, deal.Satisfaction + 10);
                    }
                    else
                    {
                        missed = true;
                        deal.Satisfaction = Math.Max(0, deal.Satisfaction - 12);
                    }
                }
                deal.BonusEarnedThisSeason = 0;
                if (missed && deal.PenaltyOnMiss > 0 && deal.EndSeason > world.Season)
                {
                    deal.PaymentPerSeason = Math.Round(deal.PaymentPerSeason * (1 - deal.PenaltyOnMiss) / 1000) * 1000;
                    if (team.IsPlayer)
                        world.AddNews(deal.Name + " cuts its payment", "Objectives were missed: " + MathX.FormatMoney(deal.PaymentPerSeason) + " per season from now on.", team.CategoryId, true);
                }
                if (deal.EndSeason > world.Season) continue;

                // The deal ends now.
                team.Sponsors.RemoveAt(i);
                double keen = MathX.Clamp01(deal.Satisfaction / 100.0 + 0.1);
                if (!rng.Chance(keen))
                {
                    if (team.IsPlayer) world.AddNews(deal.Name + " ends its sponsorship", "The partnership with " + team.Name + " will not continue next season.", team.CategoryId, true);
                    continue;
                }
                var offer = SponsorMarket.RenewalOffer(world, db, team, deal, rng);
                if (offer == null) continue;
                if (team.IsPlayer) world.SponsorOffers.Add(offer);
                else Sign(world, team, offer);
            }
            return total;
        }
    }
}
