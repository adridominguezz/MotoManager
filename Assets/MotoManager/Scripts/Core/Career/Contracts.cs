using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    /// <summary>
    /// The rules of rider contracts, in one place for the player, the AI and the season processing:
    /// standard terms, bonuses (only the best tier of a result is paid), clauses and their evaluation order,
    /// termination costs, release clauses and transfer fees, and the contract history of every rider.
    ///
    /// Season-end order: results → season bonuses → performance extensions → team options → rider options →
    /// exit clauses → market. One clause at most extends a contract; a used clause disappears.
    /// </summary>
    public static class Contracts
    {
        /// <summary>Usual terms for a deal: win and podium bonuses, title and top-3 bonuses, half of future salary on early release.</summary>
        public static void StandardTerms(Contract c)
        {
            double s = c.SalaryPerSeason;
            c.RaceBonuses = new List<PositionBonus>
            {
                new PositionBonus { MaxPosition = 1, Amount = Math.Round(s * 0.05) },
                new PositionBonus { MaxPosition = 3, Amount = Math.Round(s * 0.02) }
            };
            c.ChampionshipBonuses = new List<PositionBonus>
            {
                new PositionBonus { MaxPosition = 1, Amount = Math.Round(s * 0.25) },
                new PositionBonus { MaxPosition = 3, Amount = Math.Round(s * 0.1) }
            };
            c.TerminationShare = 0.5;
        }

        public static string NewId(WorldState world)
        {
            return world.NewId("ctr");
        }

        // ------------------------------------------------------------------ history

        /// <summary>Closes the rider's current contract into his history.</summary>
        public static void Close(WorldState world, GameDatabase db, Rider r, ContractEnd reason, double fee = 0)
        {
            Close(world, r, reason, fee);
        }

        public static void Close(WorldState world, Rider r, ContractEnd reason, double fee = 0)
        {
            var c = r.Contract;
            if (c == null) return;
            var team = world.GetTeam(c.TeamId);
            r.ContractHistory.Add(new ContractRecord
            {
                TeamId = c.TeamId,
                TeamName = team != null ? team.Name : c.TeamId,
                CategoryId = team != null ? team.CategoryId : r.CategoryId,
                StartSeason = c.StartSeason,
                EndSeason = Math.Min(c.EndSeason, world.Season),
                SalaryPerSeason = c.SalaryPerSeason,
                Reason = reason,
                Fee = fee
            });
        }

        // ------------------------------------------------------------------ bonuses

        /// <summary>Race bonuses of a weekend (main races only) and the pole bonus. Returns the total paid.</summary>
        public static double PayRaceBonuses(WorldState world, Team team, WeekendResult weekend)
        {
            double total = 0;
            SessionResult lastQualifying = null;
            foreach (var session in weekend.Sessions)
            {
                if (session.Type == SessionType.Qualifying) lastQualifying = session;
                if (session.Type != SessionType.Race) continue;
                foreach (var e in session.Classification)
                {
                    if (e.TeamId != team.Id || e.Status != ResultStatus.Finished) continue;
                    var r = world.GetRider(e.RiderId);
                    if (r == null || r.Contract == null || r.Contract.TeamId != team.Id) continue;
                    double bonus = r.Contract.RaceBonusFor(e.Position);
                    if (bonus <= 0) continue;
                    Accounts.Post(world, team, LedgerKind.RiderBonus, -bonus, r.FullName + " P" + e.Position);
                    total += bonus;
                }
            }
            if (lastQualifying != null && lastQualifying.Classification.Count > 0)
            {
                var pole = lastQualifying.Classification[0];
                var r = world.GetRider(pole.RiderId);
                if (pole.TeamId == team.Id && r != null && r.Contract != null && r.Contract.TeamId == team.Id && r.Contract.PoleBonus > 0)
                {
                    Accounts.Post(world, team, LedgerKind.RiderBonus, -r.Contract.PoleBonus, r.FullName + " pole");
                    total += r.Contract.PoleBonus;
                }
            }
            return total;
        }

        /// <summary>Championship and per-point bonuses at season end (once per rider and season).</summary>
        public static void PaySeasonBonuses(WorldState world, GameDatabase db)
        {
            foreach (var r in Sorted(world))
            {
                var c = r.Contract;
                if (c == null || r.IsRetired) continue;
                var team = world.GetTeam(c.TeamId);
                ChampionshipState champ;
                if (team == null || !world.Championships.TryGetValue(team.CategoryId ?? "", out champ) || champ.Season != world.Season) continue;
                int pos = champ.RiderPosition(r.Id);
                StandingEntry entry;
                double points = champ.Riders.TryGetValue(r.Id, out entry) ? entry.Points : 0;
                double bonus = c.ChampionshipBonusFor(pos) + c.PointBonus * points;
                if (bonus > 0 && Accounts.TryBegin(world, "season-bonus|" + r.Id))
                    Accounts.Post(world, team, LedgerKind.RiderBonus, -bonus, r.FullName + " season bonus");
            }
        }

        // ------------------------------------------------------------------ clauses at season end

        /// <summary>
        /// Clauses that act at the end of the season (before the market): extensions, options and exit clauses.
        /// AI teams decide their options like they decide renewals; the player's team option counts only if he
        /// exercised it during the season.
        /// </summary>
        public static void EvaluateSeasonEnd(WorldState world, GameDatabase db, DeterministicRandom rng)
        {
            int season = world.Season;
            foreach (var r in Sorted(world))
            {
                var c = r.Contract;
                if (c == null || r.IsRetired) continue;
                var team = world.GetTeam(c.TeamId);
                if (team == null) continue;
                ChampionshipState champ;
                world.Championships.TryGetValue(team.CategoryId ?? "", out champ);
                int pos = champ != null ? champ.RiderPosition(r.Id) : 0;

                if (c.EndSeason == season && r.FutureContract == null)
                {
                    ContractClause used = null;
                    var perf = c.Clause(ClauseType.PerformanceExtension);
                    var teamOption = c.Clause(ClauseType.TeamOption);
                    var riderOption = c.Clause(ClauseType.RiderOption);
                    if (perf != null && pos > 0 && pos <= perf.ChampionshipTopN) used = perf;
                    else if (teamOption != null && (team.IsPlayer ? teamOption.Exercised : AiExercisesOption(world, db, team, r, rng))) used = teamOption;
                    else if (riderOption != null && RiderExercisesOption(world, db, r, riderOption, rng)) used = riderOption;
                    if (used != null)
                    {
                        Extend(world, db, r, used);
                        continue;
                    }
                }

                // The team missed its objective: the rider may walk away at the end of the season.
                var exit = c.Clause(ClauseType.ExitIfTeamMissesObjective);
                if (exit != null && c.EndSeason > season && team.Personality != null && !team.Personality.LastObjectiveMet && RiderWantsOut(world, db, r))
                {
                    Close(world, db, r, ContractEnd.ExitClause);
                    team.RiderIds.Remove(r.Id);
                    r.TeamId = null;
                    r.Contract = null;
                    world.AddNews(r.FullName + " leaves " + team.Name, "He uses his contract's exit clause after a disappointing season.", team.CategoryId, team.IsPlayer);
                }
            }
        }

        /// <summary>Applies an extension clause: the current contract is closed and a new one runs from next season.</summary>
        public static void Extend(WorldState world, GameDatabase db, Rider r, ContractClause used)
        {
            var old = r.Contract;
            Close(world, db, r, used.Type == ClauseType.PerformanceExtension ? ContractEnd.Extended : ContractEnd.OptionExercised);
            var c = JsonClone(old);
            c.Id = NewId(world);
            c.StartSeason = world.Season + 1;
            c.EndSeason = world.Season + Math.Max(1, used.ExtraSeasons);
            if (used.Salary > 0) c.SalaryPerSeason = used.Salary;
            c.SigningBonus = 0;
            c.Clauses.RemoveAll(x => x.Type == used.Type);
            r.Contract = c;
            var team = world.GetTeam(c.TeamId);
            if (team != null && (team.IsPlayer || r.Reputation > 55))
                world.AddNews(r.FullName + " stays with " + team.Name, "Contract extended until " + c.EndSeason + (used.Type == ClauseType.PerformanceExtension ? " after meeting his performance clause." : " (option exercised)."), team.CategoryId, team.IsPlayer);
        }

        private static bool AiExercisesOption(WorldState world, GameDatabase db, Team team, Rider r, DeterministicRandom rng)
        {
            var levels = TransferMarket.CategoryLevels(world, db);
            double level;
            levels.TryGetValue(team.CategoryId ?? "", out level);
            return r.Attributes.Overall >= level - 2 && rng.Chance(0.75);
        }

        private static bool RiderExercisesOption(WorldState world, GameDatabase db, Rider r, ContractClause option, DeterministicRandom rng)
        {
            var team = world.GetTeam(r.Contract.TeamId);
            var rules = db.GetRules(team.CategoryId, world.Season);
            double salary = option.Salary > 0 ? option.Salary : r.Contract.SalaryPerSeason;
            double demand = TransferMarket.SalaryDemand(r, rules);
            return salary >= demand * 0.9 || rng.Chance(r.Personality.Loyalty / 150.0);
        }

        private static bool RiderWantsOut(WorldState world, GameDatabase db, Rider r)
        {
            var levels = TransferMarket.CategoryLevels(world, db);
            double level;
            levels.TryGetValue(r.CategoryId ?? "", out level);
            return r.Attributes.Overall >= level || r.Personality.Ambition > 65;
        }

        // ------------------------------------------------------------------ ending contracts early

        /// <summary>
        /// What ending a contract now costs the team: the rest of this season's salary plus the agreed share of the
        /// remaining full seasons.
        /// </summary>
        public static double TerminationCost(WorldState world, GameDatabase db, Rider r)
        {
            var c = r.Contract;
            if (c == null) return 0;
            if (c.StartSeason > world.Season) return Math.Round(c.SalaryPerSeason * c.TerminationShare * (c.EndSeason - c.StartSeason + 1));
            var team = world.GetTeam(c.TeamId);
            int rounds = team != null ? Math.Max(1, Season.GameController.RoundsInSeason(world, db, team.CategoryId)) : 1;
            int left = team != null ? CashflowForecast.RemainingRounds(world, db, team.CategoryId).Count : 0;
            double thisSeason = c.SalaryPerSeason * left / rounds;
            double future = c.SalaryPerSeason * c.TerminationShare * Math.Max(0, c.EndSeason - world.Season);
            return Math.Round(thisSeason + future);
        }

        /// <summary>The team ends the contract unilaterally and pays the termination cost.</summary>
        public static double Terminate(WorldState world, GameDatabase db, Team team, Rider r)
        {
            if (r.Contract == null || r.Contract.TeamId != team.Id) return 0;
            double cost = TerminationCost(world, db, r);
            if (Accounts.TryBegin(world, "terminate|" + r.Contract.Id))
                Accounts.Post(world, team, LedgerKind.RiderSeverance, -cost, r.FullName);
            Close(world, db, r, ContractEnd.Released, cost);
            team.RiderIds.Remove(r.Id);
            r.TeamId = null;
            r.Contract = null;
            world.AddNews(team.Name + " releases " + r.FullName, "Termination cost: " + MathX.FormatMoney(cost) + ".", team.CategoryId, team.IsPlayer);
            return cost;
        }

        // ------------------------------------------------------------------ moving riders under contract

        /// <summary>
        /// What a team wants to let a contracted rider go: the salary still owed to him and his weight in the team.
        /// Nothing when his deal ends this season and the move is for next season (he is free then).
        /// </summary>
        public static double TransferValue(WorldState world, GameDatabase db, Rider r, int fromSeason)
        {
            var c = r.Contract;
            if (c == null || c.EndSeason < fromSeason) return 0;
            var levels = TransferMarket.CategoryLevels(world, db);
            double level;
            levels.TryGetValue(r.CategoryId ?? "", out level);
            double importance = MathX.Clamp(0.6 + (r.Attributes.Overall - level) / 10.0, 0.3, 2.0);
            int seasons = c.EndSeason - fromSeason + 1;
            return Math.Round(c.SalaryPerSeason * seasons * importance / 1000) * 1000;
        }

        public static double ReleaseClause(Rider r)
        {
            var c = r.Contract != null ? r.Contract.Clause(ClauseType.ReleaseClause) : null;
            return c != null ? c.Amount : 0;
        }

        /// <summary>
        /// The fee a buyer must pay to take a rider still under contract for <paramref name="fromSeason"/>:
        /// 0 if the contract does not cover it or an exit clause applies, the release clause if there is one,
        /// otherwise the negotiated value. Returns -1 if the move is not possible (the player's riders cannot be taken
        /// without his consent unless a release clause is paid).
        /// </summary>
        public static double FeeToTake(WorldState world, GameDatabase db, Rider r, Team buyer, int fromSeason)
        {
            var c = r.Contract;
            if (c == null || c.EndSeason < fromSeason) return 0;
            var current = world.GetTeam(c.TeamId);
            var curCat = current != null ? db.GetCategory(current.CategoryId) : null;
            var newCat = db.GetCategory(buyer.CategoryId);
            if (c.Clause(ClauseType.ExitForHigherCategory) != null && curCat != null && newCat != null && newCat.Prestige > curCat.Prestige) return 0;
            double clause = ReleaseClause(r);
            if (clause > 0) return clause;
            if (current != null && current.IsPlayer) return -1;
            return TransferValue(world, db, r, fromSeason);
        }

        /// <summary>Whether the buyer can pay a fee: from its cash, within half of its season budget.</summary>
        public static bool CanPay(WorldState world, Team buyer, double fee)
        {
            return fee <= 0 || (fee <= Accounts.Cash(world, buyer) && fee <= buyer.SeasonBudget * 0.5);
        }

        /// <summary>Pays a transfer fee or release clause from the buyer to the rider's current team and closes his contract.</summary>
        public static void PayAndRelease(WorldState world, GameDatabase db, Rider r, Team buyer, double fee, bool releaseClause)
        {
            var seller = world.GetTeam(r.Contract != null ? r.Contract.TeamId : null);
            if (fee > 0 && seller != null)
            {
                Accounts.Post(world, buyer, LedgerKind.TransferFee, -fee, r.FullName, true, false);
                Accounts.Post(world, seller, LedgerKind.TransferFee, fee, r.FullName, true, false);
            }
            Close(world, db, r, fee > 0 ? (releaseClause ? ContractEnd.ReleaseClause : ContractEnd.Transfer) : ContractEnd.ExitClause, fee);
            if (seller != null)
            {
                seller.RiderIds.Remove(r.Id);
                if (fee > 0) world.AddNews(r.FullName + " leaves " + seller.Name + " for " + buyer.Name, (releaseClause ? "Release clause" : "Transfer fee") + ": " + MathX.FormatMoney(fee) + ".", buyer.CategoryId, seller.IsPlayer || buyer.IsPlayer);
            }
            r.TeamId = null;
            r.Contract = null;
        }

        // ------------------------------------------------------------------ helpers

        private static Contract JsonClone(Contract c)
        {
            return JsonSettings.DeepClone(c);
        }

        private static List<Rider> Sorted(WorldState world)
        {
            var list = new List<Rider>(world.Riders.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }
    }
}
