using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    public class SeasonSummary
    {
        public int Season;
        public readonly List<ChampionRecord> Champions = new List<ChampionRecord>();
        public readonly List<string> Retired = new List<string>();
        public MarketReport Market;
        public int YouthIntake;
        /// <summary>The player's season as the owners see it, with the offers for the manager (null without a team).</summary>
        public PlayerSeasonReview PlayerReview;
    }

    /// <summary>
    /// Everything that happens between the last race of a season and the first of the next one, in order:
    /// titles, sponsors, rider evolution, retirements, winter development, the market, youth intake and the
    /// roll-over of calendars and championships.
    /// </summary>
    public static class SeasonEndProcessor
    {
        /// <summary>New riders entering the non-simulated talent pools every winter.</summary>
        public static int GrandPrixYouthIntake = 24;
        public static int SuperbikeYouthIntake = 12;

        public static SeasonSummary Process(WorldState world, GameDatabase db)
        {
            int season = world.Season;
            var rng = world.Rng.Fork("season-end-" + season);
            var summary = new SeasonSummary { Season = season };

            FinalizeChampionships(world, db, summary);
            TeamAI.SeasonReview(world, db, rng);
            summary.PlayerReview = SeasonReviewService.Build(world, db);
            ManagerCareer.SeasonEnd(world, db, summary.PlayerReview);
            OrganisationService.SeasonEnd(world, db);
            ManagerMarket.SeasonEnd(world, db, rng.Fork("managers"));
            Contracts.PaySeasonBonuses(world, db);
            SponsorsAndReputation(world, db, rng);
            TeamAI.OffseasonInvestment(world, db);

            foreach (var r in SortedRiders(world)) RiderDevelopment.EndOfSeason(world, db, r, rng);
            // Riders' backers change their minds over the years.
            foreach (var r in SortedRiders(world))
            {
                if (r.IsRetired || !r.Generated) continue;
                if (r.PersonalBacking > 0) r.PersonalBacking = rng.Chance(0.08) ? 0 : Math.Round(r.PersonalBacking * rng.Range(0.85, 1.15) / 10000) * 10000;
                else if (r.AgeOn(new DateTime(world.Season + 1, 3, 1)) < 22 && rng.Chance(0.03)) r.PersonalBacking = Math.Round(200000 * Math.Exp(rng.Gaussian(0, 0.6)) / 10000) * 10000;
            }

            // Retirements before the market: riders whose deal ends (or who have none) consider stopping;
            // riders under contract only stop rarely (very old / unmotivated).
            foreach (var r in SortedRiders(world))
            {
                if (r.IsRetired) continue;
                bool committed = TransferMarket.IsCommitted(r, season);
                double p = RetirementModel.Probability(world, db, r, committed);
                if (committed) p *= 0.35;
                if (rng.Chance(p))
                {
                    RetirementModel.Retire(world, r, committed ? Txt.F("retire.surprise", "A surprise decision with a contract still running.") : "");
                    summary.Retired.Add(r.Id);
                }
            }

            // Contract clauses (extensions, options, exit clauses) act before the market opens.
            Contracts.EvaluateSeasonEnd(world, db, rng);
            DevelopmentService.ManufacturerSeasonReview(world, db, rng);
            DevelopmentService.WinterDevelopment(world, db, rng);
            OrganisationService.ApplyCategoryChanges(world, db);
            summary.Market = TransferMarket.Run(world, db, rng);
            // Personnel: experience, reputation and contracts (after the riders are settled for next season).
            StaffService.SeasonEnd(world, db, rng);
            // Winter sponsor approaches for next season (AI teams decide straight away, the player has time).
            SponsorMarket.AfterEvent(world, db, rng, true);
            SponsorMarket.AfterEvent(world, db, rng, true);

            // Riders who found no seat may give up.
            foreach (var r in SortedRiders(world))
            {
                if (r.IsRetired || r.TeamId != null) continue;
                var cat = db.GetCategory(r.CategoryId);
                bool pool = cat != null && !cat.Simulated;
                double p = RetirementModel.Probability(world, db, r, pool);
                if (rng.Chance(p))
                {
                    RetirementModel.Retire(world, r, Txt.F("retire.noSeat", "No competitive seat available."));
                    summary.Retired.Add(r.Id);
                }
            }

            summary.YouthIntake = YouthIntake(world, db, rng);
            Accounts.CloseSeason(world);
            MerchService.SeasonEnd(world, db);
            RollOver(world, db);
            TransferMarket.RebuildRosters(world);
            AssignSeasonBudgets(world, db);
            SponsorMarket.SeasonRefresh(world, db, rng);
            TeamAI.EnsureAll(world, db);
            TeamAI.SetObjectives(world, db);
            SeasonReviewService.AddOffers(world, db, summary.PlayerReview);
            return summary;
        }

        private static void FinalizeChampionships(WorldState world, GameDatabase db, SeasonSummary summary)
        {
            var cats = new List<string>(world.Championships.Keys);
            cats.Sort(StringComparer.Ordinal);
            foreach (var catId in cats)
            {
                var champ = world.Championships[catId];
                if (champ.Season != world.Season) continue;
                var cat = db.GetCategory(catId);
                var riders = champ.SortedRiders();
                for (int i = 0; i < riders.Count; i++)
                {
                    var r = world.GetRider(riders[i].Id);
                    if (r == null) continue;
                    foreach (var rec in r.CareerHistory)
                        if (rec.Season == world.Season && rec.CategoryId == catId) rec.ChampionshipPosition = i + 1;
                }
                if (riders.Count == 0 || riders[0].Points <= 0) continue;
                var champion = world.GetRider(riders[0].Id);
                var team = champion != null ? world.GetTeam(champion.TeamId) : null;
                var teams = champ.SortedTeams();
                var makers = champ.SortedManufacturers();
                var record = new ChampionRecord
                {
                    Season = world.Season,
                    CategoryId = catId,
                    RiderId = riders[0].Id,
                    RiderName = champion != null ? champion.FullName : riders[0].Id,
                    TeamId = team != null ? team.Id : null,
                    TeamName = team != null ? team.Name : null,
                    ManufacturerId = team != null ? team.ManufacturerId : null,
                    Points = riders[0].Points,
                    TeamChampionId = teams.Count > 0 ? teams[0].Id : null,
                    ConstructorChampionId = makers.Count > 0 ? makers[0].Id : null
                };
                world.Champions.Add(record);
                summary.Champions.Add(record);
                if (champion != null)
                {
                    champion.Titles.Add(world.Season + " " + (cat != null ? cat.Name : catId));
                    champion.Reputation = Math.Min(100, champion.Reputation + 8);
                    champion.Morale = 95;
                }
                world.AddNews(Txt.F("news.champion", "{0} is the {1} {2} World Champion", record.RiderName, world.Season, cat != null ? cat.Name : catId),
                    team != null ? Txt.F("news.championBodyTeam", "{0} wins the title with {1} points for {2}.", record.RiderName, record.Points.ToString("0"), team.Name) : Txt.F("news.championBody", "{0} wins the title with {1} points.", record.RiderName, record.Points.ToString("0")), catId, true);
            }
        }

        private static void SponsorsAndReputation(WorldState world, GameDatabase db, DeterministicRandom rng)
        {
            var teams = new List<Team>(world.Teams.Values);
            teams.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var team in teams)
            {
                var cat = db.GetCategory(team.CategoryId);
                ChampionshipState champ;
                world.Championships.TryGetValue(team.CategoryId ?? "", out champ);
                double points = 0;
                int pos = 0;
                int field = 1;
                if (champ != null && champ.Season == world.Season)
                {
                    var table = champ.SortedTeams();
                    field = Math.Max(1, table.Count);
                    for (int i = 0; i < table.Count; i++)
                        if (table[i].Id == team.Id)
                        {
                            pos = i + 1;
                            points = table[i].Points;
                        }
                }
                double bonus = SponsorService.EndOfSeason(world, db, team, cat, ConditionEvaluator.TeamSeason(champ != null && champ.Season == world.Season ? champ : null, team.Id), rng);
                Accounts.Post(world, team, LedgerKind.SponsorBonus, bonus, "Season objectives");
                if (pos > 0)
                {
                    double target = 100 - (pos - 1) * 80.0 / field;
                    team.Reputation = MathX.Clamp(team.Reputation * 0.75 + target * 0.25, 5, 100);
                }
            }
            OwnerFunding(world, db);
        }

        /// <summary>
        /// Owners (private investors, manufacturers, the cup organiser) cover their organisation's cash shortfall up
        /// to the minimum needed to operate. It is posted as income from the owner, never created silently. An
        /// organisation owned by the player's manager gets nothing: the player must finance it.
        /// </summary>
        public static void OwnerFunding(WorldState world, GameDatabase db)
        {
            var orgs = new List<Organisation>(world.Organisations.Values);
            orgs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var org in orgs)
            {
                if (org.Owner == OwnerKind.Player) continue;
                double floor = 0;
                foreach (var id in org.TeamIds)
                {
                    var team = world.GetTeam(id);
                    var cat = team != null ? db.GetCategory(team.CategoryId) : null;
                    if (cat == null || !cat.Simulated) continue;
                    floor += TeamAI.OperatingFloor(db.GetRules(cat.Id, world.Season));
                }
                if (org.Cash < floor && Accounts.TryBegin(world, "owner-funding|" + org.Id))
                    Accounts.Post(world, org, null, LedgerKind.OwnerFunding, floor - org.Cash, "Owners cover the shortfall");
            }
        }

        /// <summary>Season spending allocations for the new season (owners approve them from the expected costs).</summary>
        public static void AssignSeasonBudgets(WorldState world, GameDatabase db)
        {
            var teams = new List<Team>(world.Teams.Values);
            teams.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var team in teams)
            {
                var cat = db.GetCategory(team.CategoryId);
                if (cat == null || !cat.Simulated) continue;
                team.SeasonBudget = FinanceService.PlanSeasonBudget(world, db, team, Math.Max(1, Season.GameController.RoundsInSeason(world, db, cat.Id)));
            }
        }

        private static int YouthIntake(WorldState world, GameDatabase db, DeterministicRandom rng)
        {
            int total = 0;
            foreach (var cat in db.CategoriesOrdered())
            {
                if (cat.Simulated) continue;
                int n = cat.Pyramid == Pyramid.GrandPrix ? GrandPrixYouthIntake : SuperbikeYouthIntake;
                double minAge = cat.MinAge, maxAge = Math.Min(cat.MaxEntryAge, cat.MinAge + 3);
                for (int i = 0; i < n; i++)
                {
                    var r = RiderGenerator.Generate(world, db, rng, cat.Pyramid, cat.Id, minAge, maxAge, 0.3);
                    world.Riders[r.Id] = r;
                    total++;
                }
            }
            return total;
        }

        private static void RollOver(WorldState world, GameDatabase db)
        {
            int next = world.Season + 1;

            // Archive championships compactly (standings only) to keep saves small over decades.
            foreach (var champ in world.Championships.Values)
            {
                if (champ.Season != world.Season) continue;
                world.ArchivedChampionships.Add(new ChampionshipState
                {
                    CategoryId = champ.CategoryId,
                    Season = champ.Season,
                    Riders = champ.Riders,
                    Teams = champ.Teams,
                    Manufacturers = champ.Manufacturers
                });
            }
            world.Championships.Clear();

            var groups = new List<string>(world.Calendars.Keys);
            groups.Sort(StringComparer.Ordinal);
            foreach (var group in groups)
            {
                var current = world.Calendars[group];
                CalendarDefinition nextCal = null;
                foreach (var c in db.Calendars.Values)
                    if (c.Group == group && c.Season == next) nextCal = JsonSettings.DeepClone(c);
                if (nextCal == null)
                {
                    nextCal = JsonSettings.DeepClone(current);
                    nextCal.Id = group + "_" + next;
                    nextCal.Season = next;
                    foreach (var round in nextCal.Rounds)
                    {
                        // Same weekday one year later.
                        var d = round.Date.AddDays(364);
                        if (d.Year == next - 1) d = d.AddDays(7);
                        round.Date = d;
                    }
                }
                world.Calendars[group] = nextCal;
                world.NextRound[group] = 0;
            }

            foreach (var cat in db.Categories.Values)
                if (cat.Simulated) world.Championships[cat.Id] = new ChampionshipState { CategoryId = cat.Id, Season = next };

            foreach (var r in world.Riders.Values) r.InjuryRoundsOut = 0;
            world.NegotiationAttempts.Clear();
            world.TransferOffers.Clear();
            world.TransferAgreements.Clear();
            world.Projects.RemoveAll(p => p.Status != ProjectStatus.InProgress);
            world.Season = next;
            world.CurrentDate = new DateTime(next, 1, 1);
        }

        private static List<Rider> SortedRiders(WorldState world)
        {
            var list = new List<Rider>(world.Riders.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }
    }
}
