using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;

namespace MotoManager.Core.Season
{
    /// <summary>The next event on the combined calendar of both championships.</summary>
    public class EventPlan
    {
        public string Group;
        public int RoundIndex;
        public CalendarRound Round;
        public List<string> Categories = new List<string>();
    }

    /// <summary>An event in progress: one weekend per category racing there.</summary>
    public class LiveEvent
    {
        public EventPlan Plan;
        public readonly Dictionary<string, WeekendSimulation> Weekends = new Dictionary<string, WeekendSimulation>();
        /// <summary>Weekend of the player's category, or null if the player's team does not race at this event.</summary>
        public WeekendSimulation PlayerWeekend;
    }

    /// <summary>
    /// Macro game loop shared by the Unity front-end and headless tools:
    /// BeginNextEvent -> (UI drives the player's weekend) -> CompleteEvent -> ... -> EndSeason.
    /// </summary>
    public class GameController
    {
        public readonly GameDatabase Db;
        public WorldState World { get; private set; }

        public GameController(GameDatabase db, WorldState world)
        {
            Db = db;
            World = world;
            // Saves from before team personalities existed.
            TeamAI.EnsureAll(world, db);
        }

        public static GameController NewGame(GameDatabase db, NewGameOptions options)
        {
            return new GameController(db, WorldFactory.Create(db, options));
        }

        public bool IsSeasonOver
        {
            get { return PeekNextEvent() == null; }
        }

        public EventPlan PeekNextEvent()
        {
            EventPlan best = null;
            var groups = new List<string>(World.Calendars.Keys);
            groups.Sort(StringComparer.Ordinal);
            foreach (var group in groups)
            {
                var cal = World.Calendars[group];
                int idx;
                World.NextRound.TryGetValue(group, out idx);
                if (idx >= cal.Rounds.Count) continue;
                var round = cal.Rounds[idx];
                if (best == null || round.Date < best.Round.Date)
                    best = new EventPlan { Group = group, RoundIndex = idx, Round = round };
            }
            if (best == null) return null;
            if (best.Round.Categories != null && best.Round.Categories.Count > 0)
            {
                best.Categories.AddRange(best.Round.Categories);
            }
            else
            {
                ChampionshipDefinition champ;
                if (Db.Championships.TryGetValue(best.Group, out champ))
                    foreach (var c in champ.Categories) best.Categories.Add(c);
            }
            best.Categories.RemoveAll(c =>
            {
                var cat = Db.GetCategory(c);
                return cat == null || !cat.Simulated;
            });
            return best;
        }

        public LiveEvent BeginNextEvent()
        {
            var plan = PeekNextEvent();
            if (plan == null) return null;
            World.CurrentDate = plan.Round.Date;
            var ev = new LiveEvent { Plan = plan };
            var player = World.PlayerTeam;
            foreach (var catId in plan.Categories)
            {
                var weekend = new WeekendSimulation(World, Db, catId, plan.Round);
                ev.Weekends[catId] = weekend;
                if (player != null && player.CategoryId == catId)
                {
                    weekend.HeadlessTimeStep = false;
                    ev.PlayerWeekend = weekend;
                }
            }
            return ev;
        }

        /// <summary>Finishes any session still pending (headless) and applies everything that follows an event.</summary>
        public void CompleteEvent(LiveEvent ev)
        {
            var rng = World.Rng.Fork("event-" + World.Season + "-" + ev.Plan.Group + "-" + ev.Plan.RoundIndex);
            var cats = new List<string>(ev.Weekends.Keys);
            cats.Sort(StringComparer.Ordinal);

            // Days of factory work since the previous event.
            double days = Math.Max(1, (ev.Plan.Round.Date - LastEventDate()).TotalDays);
            World.CurrentDate = ev.Plan.Round.Date;

            foreach (var catId in cats)
            {
                var weekend = ev.Weekends[catId];
                weekend.RunToEnd();
                var champ = ChampionshipService.Get(World, catId);
                champ.Rounds.Add(weekend.Result);
                var rules = Db.GetRules(catId, World.Season);

                // Guarded: an event's money can never be posted twice.
                if (Accounts.TryBegin(World, "event|" + ev.Plan.Group + "|" + ev.Plan.RoundIndex + "|" + catId)) ApplyEconomy(weekend, rules, catId);
                ApplyInjuries(weekend, catId, rng);
                DevelopmentService.AiInSeasonUpdates(World, Db, catId, rng);
                DevelopmentService.ApplyBalanceOfPerformance(World, Db, catId, champ);
                RaceNews(weekend, catId);
            }

            DevelopmentService.Advance(World, Db, days, rng);
            ScoutingService.AfterRound(World, Db);
            World.NextRound[ev.Plan.Group] = ev.Plan.RoundIndex + 1;
            World.EventsCompleted++;
            SponsorMarket.AfterEvent(World, Db, rng);
            ContractService.AiApproaches(World, Db, rng);
            World.LastEventDate = ev.Plan.Round.Date;
        }

        private DateTime LastEventDate()
        {
            if (World.LastEventDate.Year == World.Season) return World.LastEventDate;
            return new DateTime(World.Season, 1, 1);
        }

        public SeasonSummary EndSeason()
        {
            return SeasonEndProcessor.Process(World, Db);
        }

        /// <summary>Simulates all remaining events of the season headless (player weekend included).</summary>
        public void SimulateRestOfSeason(Action<LiveEvent> afterEvent = null)
        {
            while (!IsSeasonOver)
            {
                var ev = BeginNextEvent();
                if (ev.PlayerWeekend != null) ev.PlayerWeekend.HeadlessTimeStep = true;
                CompleteEvent(ev);
                if (afterEvent != null) afterEvent(ev);
            }
        }

        public int RoundsInSeason(string categoryId)
        {
            return RoundsInSeason(World, Db, categoryId);
        }

        public static int RoundsInSeason(WorldState world, GameDatabase db, string categoryId)
        {
            var cat = db.GetCategory(categoryId);
            if (cat == null) return 0;
            CalendarDefinition cal;
            if (!world.Calendars.TryGetValue(cat.CalendarGroup ?? "", out cal)) return 0;
            int n = 0;
            foreach (var r in cal.Rounds)
                if (r.Categories == null || r.Categories.Count == 0 || r.Categories.Contains(categoryId)) n++;
            return n;
        }

        private void ApplyEconomy(WeekendSimulation weekend, CategoryRules rules, string catId)
        {
            int rounds = Math.Max(1, RoundsInSeason(catId));
            foreach (var team in World.TeamsInCategory(catId))
            {
                int crashes = 0;
                foreach (var id in team.RiderIds)
                {
                    int c;
                    if (weekend.CrashesByRider.TryGetValue(id, out c)) crashes += c;
                }
                double prize = 0;
                foreach (var session in weekend.Result.Sessions)
                {
                    if (session.Type != SessionType.Race) continue;
                    foreach (var e in session.Classification)
                        if (e.TeamId == team.Id && e.Status == ResultStatus.Finished && e.Position - 1 < rules.Economy.PrizeMoneyByPosition.Count)
                            prize += rules.Economy.PrizeMoneyByPosition[e.Position - 1];
                }
                Contracts.PayRaceBonuses(World, team, weekend.Result);
                var outcome = ConditionEvaluator.TeamRace(weekend.Result, team.Id);
                double bonus = SponsorService.EvaluateRace(World, team, outcome);
                FinanceService.ApplyRound(World, Db, team, rounds, rules, crashes, prize, bonus);
                CareerService.PaySalary(World, team, rounds);
                var org = World.OrganisationOf(team);
                if (org != null && org.Loans.Count > 0 && org.TeamIds.Count > 0 && org.TeamIds[0] == team.Id) CareerService.ServiceLoans(World, org, rounds);
                var shopRng = new DeterministicRandom(DeterministicRandom.Combine(World.MasterSeed, DeterministicRandom.Hash("merch|" + World.Season + "|" + weekend.Round.Round + "|" + team.Id)));
                MerchService.AfterEvent(World, Db, team, rules, rounds, outcome, shopRng);
            }
        }

        private void ApplyInjuries(WeekendSimulation weekend, string catId, DeterministicRandom rng)
        {
            foreach (var r in World.ActiveRidersInCategory(catId))
                if (r.InjuryRoundsOut > 0 && !weekend.InjuredRiders.Contains(r.Id)) r.InjuryRoundsOut--;
            foreach (var id in weekend.InjuredRiders)
            {
                var r = World.GetRider(id);
                if (r == null) continue;
                r.InjuryRoundsOut = rng.NextInt(1, 4);
                r.Morale = Math.Max(5, r.Morale - 10);
                var cat = Db.GetCategory(catId);
                World.AddNews(r.FullName + " injured", r.FullName + " is expected to miss " + r.InjuryRoundsOut + " round(s).", catId,
                    (cat != null && cat.Tier == 1) || (World.PlayerTeam != null && r.TeamId == World.PlayerTeamId));
            }
        }

        private void RaceNews(WeekendSimulation weekend, string catId)
        {
            var cat = Db.GetCategory(catId);
            bool playerCat = World.PlayerTeam != null && World.PlayerTeam.CategoryId == catId;
            if (cat == null || (cat.Tier > 1 && !playerCat)) return;
            foreach (var s in weekend.Result.Sessions)
            {
                if (s.Type != SessionType.Race || s.Classification.Count == 0) continue;
                var winner = World.GetRider(s.Classification[0].RiderId);
                if (winner == null) continue;
                World.AddNews(winner.FullName + " wins the " + weekend.Round.Name + (s.SessionName != null && weekend.Result.Sessions.Count > 4 ? " (" + s.SessionName + ")" : ""),
                    cat.Name + ": " + winner.FullName + " takes victory at " + weekend.Circuit.ShortName + ".", catId, cat.Tier == 1);
            }
        }
    }

    /// <summary>JSON save games with a format version.</summary>
    public static class SaveSystem
    {
        public const int CurrentVersion = 1;

        public static string Serialize(WorldState world)
        {
            world.Version = CurrentVersion;
            return JsonSettings.Serialize(world);
        }

        public static WorldState Deserialize(string json)
        {
            var world = JsonSettings.Deserialize<WorldState>(json);
            if (world == null) throw new FormatException("Empty save");
            if (world.Version > CurrentVersion) throw new FormatException("Save from a newer version (" + world.Version + ")");
            return world;
        }
    }
}
