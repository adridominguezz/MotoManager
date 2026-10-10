using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    public class MarketMove
    {
        public string RiderId;
        public string FromTeamId;
        public string FromCategoryId;
        public string ToTeamId;
        public string ToCategoryId;
        public bool Renewal;
        public bool Generated;
        public bool Buyout;
    }

    public class MarketReport
    {
        public readonly List<MarketMove> Moves = new List<MarketMove>();
        public readonly List<string> Released = new List<string>();
        public int GeneratedRiders;
    }

    /// <summary>
    /// End-of-season rider market across both pyramids. Nothing forces a fixed career path: teams rank
    /// candidates by ability, growth, age, reputation, cost and ties to their manufacturer; riders accept a seat
    /// only if it beats what they have (category prestige, team standing, factory status, GP/SBK preference).
    /// Seats are filled from the most prestigious down, so a promotion opens a vacancy below that is filled later:
    /// MotoGP -> Moto2 -> Moto3 -> Rookies -> new talent, or WorldSBK -> WorldSSP -> junior pool.
    /// </summary>
    public static class TransferMarket
    {
        private class Seat
        {
            public Team Team;
            public ChampionshipCategory Category;
            public CategoryRules Rules;
            public double Prestige;
        }

        public static MarketReport Run(WorldState world, GameDatabase db, DeterministicRandom rng)
        {
            var report = new MarketReport();
            int season = world.Season;
            int next = season + 1;
            var nextDate = new DateTime(next, 3, 1);

            // 0. Pre-signed deals for next season (player offers accepted during the year).
            foreach (var r in SortedRiders(world))
            {
                if (r.IsRetired || r.FutureContract == null) continue;
                var c = r.FutureContract;
                r.FutureContract = null;
                var team = world.GetTeam(c.TeamId);
                if (team == null || c.StartSeason != next) continue;
                var old = world.GetTeam(r.TeamId);
                if (old != null) old.RiderIds.Remove(r.Id);
                Contracts.Close(world, r, r.TeamId == team.Id ? ContractEnd.Renewed : (r.Contract != null && r.Contract.EndsByTransfer ? ContractEnd.Transfer : ContractEnd.Expired));
                report.Moves.Add(new MarketMove { RiderId = r.Id, FromTeamId = r.TeamId, FromCategoryId = r.CategoryId, ToTeamId = team.Id, ToCategoryId = team.CategoryId, Renewal = r.TeamId == team.Id });
                r.TeamId = team.Id;
                r.CategoryId = team.CategoryId;
                r.Contract = c;
                if (!team.RiderIds.Contains(r.Id)) team.RiderIds.Add(r.Id);
                if (NumberTaken(world, r)) r.Number = RiderGenerator.FreeNumber(world, team.CategoryId, rng);
            }

            // 1. Age limits and expired contracts.
            foreach (var r in SortedRiders(world))
            {
                if (r.IsRetired) continue;
                var cat = db.GetCategory(r.CategoryId);
                if (cat != null && r.TeamId != null && r.AgeOn(nextDate) > cat.MaxAge)
                    Release(world, r, report);
            }

            var levels = CategoryLevels(world, db);

            // 2. Renewals.
            foreach (var r in SortedRiders(world))
            {
                if (r.IsRetired || r.TeamId == null || r.Contract == null || r.Contract.EndSeason > season) continue;
                var team = world.GetTeam(r.TeamId);
                var cat = db.GetCategory(r.CategoryId);
                if (team == null || cat == null)
                {
                    Release(world, r, report);
                    continue;
                }
                if (TeamWantsRenewal(world, db, team, cat, r, rng) && RiderWantsRenewal(world, db, cat, r, rng))
                {
                    var rules = db.GetRules(cat.Id, season);
                    // A rider who has become good enough for the seat no longer pays for it.
                    Contracts.Close(world, r, ContractEnd.Renewed);
                    r.Contract = NewContract(team, r, next, TeamAI.ContractYears(team, r.AgeOn(nextDate), rng), AgreedSalary(team, r, rules),
                        FundingFor(world, db, team, r, levels));
                    report.Moves.Add(new MarketMove { RiderId = r.Id, FromTeamId = team.Id, ToTeamId = team.Id, FromCategoryId = cat.Id, ToCategoryId = cat.Id, Renewal = true });
                }
                else
                {
                    Release(world, r, report);
                }
            }

            // 3. Seats to fill, most prestigious first.
            var seats = new List<Seat>();
            foreach (var team in SortedTeams(world))
            {
                var cat = db.GetCategory(team.CategoryId);
                if (cat == null || !cat.Simulated) continue;
                var rules = db.GetRules(cat.Id, season);
                int capacity = Capacity(cat, rules, team);
                int committed = CommittedCount(world, team, season);
                for (int i = committed; i < capacity; i++) seats.Add(MakeSeat(team, cat, rules));
            }
            seats.Sort(CompareSeats);

            // 4. Fill. Top-category seats are matched first with riders choosing among the teams that want them
            // (bike, team, money); the rest cascades down, most prestigious seat first.
            var matched = TopCategoryMatching(world, db, seats, nextDate, season, levels);
            var reserved = new HashSet<string>();
            foreach (var r in matched.Values) reserved.Add(r.Id);
            for (int s = 0; s < seats.Count; s++)
            {
                var seat = seats[s];
                Rider best = null;
                double bestScore = double.MinValue;
                if (matched.TryGetValue(seat, out best))
                {
                    reserved.Remove(best.Id);
                    bestScore = 0;
                }
                else foreach (var r in SortedRiders(world))
                {
                    if (reserved.Contains(r.Id)) continue;
                    if (!Eligible(world, db, seat, r, nextDate, season)) continue;
                    if (!RiderAccepts(world, db, seat, r, season, nextDate)) continue;
                    double score = TeamScore(world, db, seat, r, nextDate, levels) + rng.Gaussian(0, 1.5);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = r;
                    }
                }

                var move = new MarketMove { ToTeamId = seat.Team.Id, ToCategoryId = seat.Category.Id };
                if (best == null)
                {
                    best = GenerateFor(world, db, seat, rng, levels);
                    move.Generated = true;
                    report.GeneratedRiders++;
                }
                else
                {
                    move.FromTeamId = best.TeamId;
                    move.FromCategoryId = best.CategoryId;
                    // Buyout: the rider still had a deal elsewhere -> the fee is paid and that seat opens, filled later.
                    if (IsCommitted(best, season) && best.TeamId != null && best.TeamId != seat.Team.Id)
                    {
                        move.Buyout = true;
                        var oldTeam = world.GetTeam(best.TeamId);
                        var oldCat = oldTeam != null ? db.GetCategory(oldTeam.CategoryId) : null;
                        double fee = Math.Max(0, Contracts.FeeToTake(world, db, best, seat.Team, next));
                        Contracts.PayAndRelease(world, db, best, seat.Team, fee, Contracts.ReleaseClause(best) > 0 && fee == Contracts.ReleaseClause(best));
                        if (oldTeam != null && oldCat != null && oldCat.Simulated)
                        {
                            var vacancy = MakeSeat(oldTeam, oldCat, db.GetRules(oldCat.Id, season));
                            InsertSeat(seats, s + 1, vacancy);
                        }
                    }
                }
                move.RiderId = best.Id;
                Sign(world, db, seat, best, next, rng, levels);
                report.Moves.Add(move);
            }

            // 5. Riders left without a seat.
            foreach (var r in SortedRiders(world))
            {
                if (r.IsRetired) continue;
                var cat = db.GetCategory(r.CategoryId);
                bool pool = cat != null && !cat.Simulated;
                if (r.TeamId == null && !pool) r.SeasonsWithoutSeat++;
                else if (r.TeamId != null) r.SeasonsWithoutSeat = 0;
            }

            RebuildRosters(world);
            ReportNews(world, db, report);
            return report;
        }

        // ------------------------------------------------------------------ top-category matching

        /// <summary>
        /// Deferred acceptance for the open seats of the top categories: each rider approaches the seats he likes
        /// best (bike, team, money, preferences), each seat keeps the rider its team rates highest. The most
        /// prestigious team therefore no longer gets first pick: a star goes where the offer suits him, and a
        /// manufacturer that builds a better bike or pays more can attract him. Deterministic.
        /// </summary>
        private static Dictionary<Seat, Rider> TopCategoryMatching(WorldState world, GameDatabase db, List<Seat> seats, DateTime nextDate, int season, Dictionary<string, double> levels)
        {
            var held = new Dictionary<Seat, Rider>();
            var top = new List<Seat>();
            foreach (var s in seats) if (s.Category.Tier == 1 && !s.Category.IsCup) top.Add(s);
            if (top.Count == 0) return held;

            var prefs = new Dictionary<string, List<Seat>>();
            var score = new Dictionary<Seat, Dictionary<string, double>>();
            foreach (var s in top) score[s] = new Dictionary<string, double>();
            var riders = new List<Rider>();
            foreach (var r in SortedRiders(world))
            {
                var acceptable = new List<KeyValuePair<Seat, double>>();
                foreach (var s in top)
                {
                    if (!Eligible(world, db, s, r, nextDate, season) || !RiderAccepts(world, db, s, r, season, nextDate)) continue;
                    acceptable.Add(new KeyValuePair<Seat, double>(s, OfferUtility(world, s, r)));
                    // Fixed per pair noise so the result does not depend on the order of proposals.
                    var noise = new DeterministicRandom(DeterministicRandom.Combine(world.MasterSeed, DeterministicRandom.Hash(season + "|" + s.Team.Id + "|" + r.Id)));
                    score[s][r.Id] = TeamScore(world, db, s, r, nextDate, levels) + noise.Gaussian(0, 1.5);
                }
                if (acceptable.Count == 0) continue;
                acceptable.Sort((a, b) =>
                {
                    int c = b.Value.CompareTo(a.Value);
                    return c != 0 ? c : string.CompareOrdinal(a.Key.Team.Id, b.Key.Team.Id);
                });
                var list = new List<Seat>();
                foreach (var kv in acceptable) list.Add(kv.Key);
                prefs[r.Id] = list;
                riders.Add(r);
            }

            // Only riders a team would actually want (above the level it would otherwise accept) take part.
            var next = new Dictionary<string, int>();
            var queue = new Queue<Rider>();
            riders.Sort((a, b) =>
            {
                int c = b.Attributes.Overall.CompareTo(a.Attributes.Overall);
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });
            foreach (var r in riders)
            {
                next[r.Id] = 0;
                queue.Enqueue(r);
            }
            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                var list = prefs[r.Id];
                while (next[r.Id] < list.Count)
                {
                    var s = list[next[r.Id]++];
                    Rider current;
                    double mine = score[s][r.Id];
                    double level;
                    if (levels.TryGetValue(s.Category.Id, out level) && r.Attributes.Overall < level - 10) continue;
                    if (!held.TryGetValue(s, out current))
                    {
                        held[s] = r;
                        break;
                    }
                    if (mine > score[s][current.Id])
                    {
                        held[s] = r;
                        queue.Enqueue(current);
                        break;
                    }
                }
            }
            return held;
        }

        /// <summary>A seat's appeal to a rider including the money the team can pay him.</summary>
        private static double OfferUtility(WorldState world, Seat seat, Rider r)
        {
            double demand = Math.Max(1, SalaryDemand(r, seat.Rules));
            double pay = AgreedSalary(seat.Team, r, seat.Rules) / demand;
            return SeatUtility(world, seat.Category, seat.Team, r) + (pay - 1) * 15;
        }

        // ------------------------------------------------------------------ evaluation

        public static double SalaryDemand(Rider r, CategoryRules rules)
        {
            double typical = rules != null ? rules.Economy.TypicalRiderSalary : 100000;
            if (typical <= 0) return 0;
            return Math.Round(typical * Math.Exp((r.Reputation - 55) / 20.0) / 1000) * 1000;
        }

        /// <summary>
        /// Salary an AI deal settles at: the rider's demand, but never more than the team can pay out of its season
        /// budget. Riders whose demand exceeds every budget accept the best offer available rather than no seat.
        /// </summary>
        public static double AgreedSalary(Team team, Rider r, CategoryRules rules)
        {
            return Math.Round(Math.Min(SalaryDemand(r, rules), TeamAI.MaxSalary(team)) / 1000) * 1000;
        }

        /// <summary>
        /// How much a team values the money a rider brings: relative to its budget, more for modest and cash-strapped
        /// teams. A rider bringing a fifth of a Moto3 team's budget can beat a clearly faster one; the same money means
        /// little to a MotoGP works team.
        /// </summary>
        public static double BackingValue(WorldState world, GameDatabase db, Team team, Rider r, Dictionary<string, double> levels = null)
        {
            if (team.SeasonBudget <= 0 || !NeedsToPay(world, db, team, r, levels)) return 0;
            double need = 1.4 - team.Reputation / 100.0;
            var org = world.OrganisationOf(team);
            var rules = db.GetRules(team.CategoryId, world.Season);
            if (org != null && rules != null && org.Cash < TeamAI.OperatingFloor(rules)) need += 0.4;
            return Math.Min(10, r.PersonalBacking / team.SeasonBudget * 40 * Math.Max(0.2, need));
        }

        /// <summary>
        /// The level a team expects for one of its seats: the category's average, a little more for top teams and a
        /// little less for modest ones.
        /// </summary>
        public static double MeritLevel(GameDatabase db, Team team, Dictionary<string, double> levels)
        {
            double level;
            if (!levels.TryGetValue(team.CategoryId ?? "", out level))
            {
                var cat = db.GetCategory(team.CategoryId);
                level = 40 + (cat != null ? cat.Prestige : 50) * 0.42;
            }
            return level + (team.Reputation - 50) * 0.08;
        }

        /// <summary>
        /// A rider pays for a seat only when his ability alone would not earn it and he has backers. Once he is good
        /// enough the money stops mattering (and is not asked for). Cups select on talent: nobody pays.
        /// </summary>
        public static bool NeedsToPay(WorldState world, GameDatabase db, Team team, Rider r, Dictionary<string, double> levels = null)
        {
            if (r.PersonalBacking <= 0) return false;
            var cat = db.GetCategory(team.CategoryId);
            if (cat == null || cat.IsCup) return false;
            return r.Attributes.Overall < MeritLevel(db, team, levels ?? CategoryLevels(world, db));
        }

        /// <summary>What the rider's backers pay the team per season under a new contract.</summary>
        public static double FundingFor(WorldState world, GameDatabase db, Team team, Rider r, Dictionary<string, double> levels = null)
        {
            return NeedsToPay(world, db, team, r, levels) ? r.PersonalBacking : 0;
        }

        /// <summary>Average overall of the riders racing in each category (what a team expects for the level).</summary>
        public static Dictionary<string, double> CategoryLevels(WorldState world, GameDatabase db)
        {
            var sum = new Dictionary<string, double>();
            var count = new Dictionary<string, int>();
            foreach (var r in world.Riders.Values)
            {
                if (r.IsRetired || r.CategoryId == null) continue;
                double s;
                int c;
                sum.TryGetValue(r.CategoryId, out s);
                count.TryGetValue(r.CategoryId, out c);
                sum[r.CategoryId] = s + r.Attributes.Overall;
                count[r.CategoryId] = c + 1;
            }
            var levels = new Dictionary<string, double>();
            foreach (var cat in db.Categories.Values)
            {
                int c;
                levels[cat.Id] = count.TryGetValue(cat.Id, out c) && c >= 5 ? sum[cat.Id] / c : 40 + cat.Prestige * 0.42;
            }
            return levels;
        }

        private static bool TeamWantsRenewal(WorldState world, GameDatabase db, Team team, ChampionshipCategory cat, Rider r, DeterministicRandom rng)
        {
            var champ = world.Championships.ContainsKey(cat.Id) ? world.Championships[cat.Id] : null;
            int pos = champ != null ? champ.RiderPosition(r.Id) : 0;
            int field = champ != null ? Math.Max(1, champ.Riders.Count) : 20;
            double rank = pos > 0 ? (double)pos / field : 0.6;
            double age = r.AgeOn(new DateTime(world.Season + 1, 3, 1));
            double p = 0.85 - rank * 0.7 + (team.Status == TeamStatus.Factory ? -0.1 : 0) + (age < 24 ? 0.1 : 0) - (age > 34 ? 0.15 : 0)
                       + TeamAI.RenewalBias(team, rank, age);
            if (age > cat.MaxAge - 1) p = 0;
            return rng.Chance(MathX.Clamp(p, 0.05, 0.95));
        }

        private static bool RiderWantsRenewal(WorldState world, GameDatabase db, ChampionshipCategory cat, Rider r, DeterministicRandom rng)
        {
            // Riders clearly too good for their category want to step up.
            double best = 0;
            foreach (var c in db.Categories.Values)
                if (c.Simulated && c.Prestige > cat.Prestige) best = Math.Max(best, c.Prestige);
            if (best <= 0) return rng.Chance(0.5 + r.Personality.Loyalty / 200.0);
            double readiness = (r.Attributes.Overall - (40 + cat.Prestige * 0.42)) / 10.0;
            double leave = MathX.Clamp(readiness * (0.3 + r.Personality.Ambition / 150.0), 0, 0.9);
            return !rng.Chance(leave * (1.2 - r.Personality.Loyalty / 100.0));
        }

        private static bool Eligible(WorldState world, GameDatabase db, Seat seat, Rider r, DateTime nextDate, int season)
        {
            if (r.IsRetired || r.InjuryRoundsOut > 6) return false;
            double age = r.AgeOn(nextDate);
            var cat = seat.Category;
            if (age < cat.MinAge || age > cat.MaxAge) return false;
            bool newcomer = r.CategoryId != cat.Id || r.TeamId == null;
            if (newcomer && age > cat.MaxEntryAge) return false;
            if (IsCommitted(r, season))
            {
                if (r.TeamId == seat.Team.Id) return false;
                // Under contract for next season: only through a fee the buyer can pay (release clause or agreed
                // value), or an exit clause. The player's riders go only through their release clause.
                double fee = Contracts.FeeToTake(world, db, r, seat.Team, season + 1);
                if (fee < 0 || !Contracts.CanPay(world, seat.Team, fee)) return false;
            }
            // Talent-pool riders need a stepping stone before a top category, unless already at that level.
            var origin = db.GetCategory(r.CategoryId);
            if (cat.Tier == 1 && origin != null && !origin.Simulated && r.Attributes.Overall < 40 + cat.Prestige * 0.42 - 5) return false;
            if (cat.IsCup)
            {
                // Cups recruit youngsters from the talent pool / their own grid.
                var from = db.GetCategory(r.CategoryId);
                if (from != null && from.Simulated && from.Id != cat.Id) return false;
            }
            return true;
        }

        /// <summary>
        /// How attractive a seat is to a rider: category prestige, team standing, factory status, GP/SBK preference
        /// and how competitive the bike has just been (riders follow the machine that is winning).
        /// </summary>
        public static double SeatUtility(WorldState world, ChampionshipCategory cat, Team team, Rider r)
        {
            double u = cat.Prestige + team.Reputation * 0.3 + (team.Status == TeamStatus.Factory ? 6 : 0);
            double pref = (r.Personality.GrandPrixPreference - 50) * 0.3;
            u += cat.Pyramid == Pyramid.GrandPrix ? pref : -pref;
            u += (BikeAppeal(world, cat.Id, team) - 0.5) * 16;
            return u;
        }

        /// <summary>
        /// 0-1: how good the team's machinery is reckoned to be in the paddock: mostly the bike itself compared with
        /// the best in the category, partly the manufacturer's recent results.
        /// </summary>
        public static double BikeAppeal(WorldState world, string categoryId, Team team)
        {
            var bike = world.GetBike(team.BikeId);
            double mine = bike != null && bike.Components.Count > 0 ? Average(bike) : 0;
            double best = 0;
            foreach (var t in world.TeamsInCategory(categoryId))
            {
                var b = world.GetBike(t.BikeId);
                if (b != null && b.Components.Count > 0) best = Math.Max(best, Average(b));
            }
            double machine = best > 0 ? MathX.Clamp01(1 - (best - mine) / 8.0) : 0.5;
            double results = 0.5;
            ChampionshipState champ;
            if (team.ManufacturerId != null && world.Championships.TryGetValue(categoryId ?? "", out champ) && champ.Rounds.Count >= 3)
            {
                var table = champ.SortedManufacturers();
                results = 0.2;
                if (table.Count > 0 && table[0].Points > 0)
                    foreach (var e in table)
                        if (e.Id == team.ManufacturerId) results = MathX.Clamp01(e.Points / table[0].Points);
            }
            return machine * 0.7 + results * 0.3;
        }

        private static double Average(Motorcycle b)
        {
            double sum = 0;
            foreach (var v in b.Components.Values) sum += v;
            return sum / b.Components.Count;
        }

        private static bool RiderAccepts(WorldState world, GameDatabase db, Seat seat, Rider r, int season, DateTime nextDate)
        {
            double offer = SeatUtility(world, seat.Category, seat.Team, r);
            var current = db.GetCategory(r.CategoryId);
            var currentTeam = world.GetTeam(r.TeamId);
            if (IsCommitted(r, season) && current != null && currentTeam != null)
                return offer > SeatUtility(world, current, currentTeam, r) + 8;
            if (current != null && current.Simulated)
            {
                // Established riders would rather stop than drop far down the ladder.
                double drop = current.Prestige - seat.Category.Prestige;
                double age = r.AgeOn(nextDate);
                if (drop > 45 && age > 27) return false;
                if (drop > 25 && r.Reputation > 70) return false;
            }
            return true;
        }

        private static double TeamScore(WorldState world, GameDatabase db, Seat seat, Rider r, DateTime nextDate, Dictionary<string, double> levels)
        {
            double age = r.AgeOn(nextDate);
            var cat = seat.Category;
            double ability = r.Attributes.Overall;
            double youth = MathX.Clamp((27 - age) / 10.0, 0, 1);
            double growth = Math.Max(0, r.Potential - ability) * youth;
            double score;
            if (cat.IsCup) score = r.Potential * 0.7 + ability * 0.3;
            else score = ability + growth * (cat.Tier == 1 ? 0.35 : 0.6) + r.Reputation * 0.12;

            // Proven results count: championship position in the season just finished.
            var champ = world.Championships.ContainsKey(r.CategoryId ?? "") ? world.Championships[r.CategoryId] : null;
            double results = 0;
            if (champ != null)
            {
                int pos = champ.RiderPosition(r.Id);
                var from = db.GetCategory(r.CategoryId);
                if (pos > 0 && from != null) results = Math.Max(0, 8 - pos) * (from.Prestige / 100.0) * 1.5;
            }
            score += results;

            if (cat.Pyramid == Pyramid.GrandPrix && age > 32) score -= (age - 32) * 1.5;
            // Feeder categories are for riders on the way up.
            if (cat.Tier >= 2 && age > 26) score -= (age - 26) * 1.2;
            if (cat.Pyramid == Pyramid.Superbike && age > 35) score -= (age - 35) * 1.5;

            var from2 = db.GetCategory(r.CategoryId);
            // Cup riders normally move on to the category the cup feeds (Rookies -> Moto3). Skipping a step is
            // possible but rare: only an exceptional rider overcomes this.
            if (from2 != null && from2.IsCup && from2.Id != cat.Id && !cat.FeederCategories.Contains(from2.Id)) score -= 10;
            if (from2 != null && from2.Pyramid != cat.Pyramid && r.Reputation < 75) score -= 3;
            bool sameManufacturer = r.Contract != null && r.Contract.ManufacturerId != null && r.Contract.ManufacturerId == seat.Team.ManufacturerId;
            if (sameManufacturer) score += 3;
            if (r.TeamId == seat.Team.Id) score += 2;

            // The team's character: young talent, experience, manufacturer ties, results now or long-term project.
            bool unproven = r.CareerHistory.Count == 0 || (from2 != null && !from2.Simulated);
            score += TeamAI.SigningBias(seat.Team, r, age, ability, growth, results, sameManufacturer, unproven);

            // Money the rider brings: decisive for small teams in small categories, irrelevant at the top.
            score += BackingValue(world, db, seat.Team, r, levels);

            if (IsCommitted(r, nextDate.Year - 1) && r.TeamId != seat.Team.Id)
            {
                double fee = Math.Max(0, Contracts.FeeToTake(world, db, r, seat.Team, nextDate.Year));
                score -= fee / Math.Max(1, seat.Team.SeasonBudget) * 20;
            }

            double demand = SalaryDemand(r, seat.Rules);
            double affordable = TeamAI.SalaryCeiling(seat.Team);
            if (demand > affordable) score -= (demand / affordable - 1) * 5;

            // Sub-par riders for the level are unattractive.
            double level;
            if (levels.TryGetValue(cat.Id, out level) && ability < level - 12) score -= (level - 12 - ability);
            return score;
        }

        /// <summary>
        /// Riders available now who are eligible for a team's seat and would accept it, best first by the team's
        /// own valuation (used when a new team needs riders straight away).
        /// </summary>
        public static List<Rider> FreeCandidates(WorldState world, GameDatabase db, Team team, int count)
        {
            var cat = db.GetCategory(team.CategoryId);
            var seat = MakeSeat(team, cat, db.GetRules(cat.Id, world.Season));
            var date = new DateTime(world.Season, 3, 1);
            var levels = CategoryLevels(world, db);
            var scored = new List<KeyValuePair<Rider, double>>();
            foreach (var r in SortedRiders(world))
            {
                if (r.TeamId != null || r.FutureContract != null) continue;
                if (!Eligible(world, db, seat, r, date, world.Season - 1) || !RiderAccepts(world, db, seat, r, world.Season - 1, date)) continue;
                scored.Add(new KeyValuePair<Rider, double>(r, TeamScore(world, db, seat, r, date, levels)));
            }
            scored.Sort((a, b) =>
            {
                int c = b.Value.CompareTo(a.Value);
                return c != 0 ? c : string.CompareOrdinal(a.Key.Id, b.Key.Id);
            });
            var list = new List<Rider>();
            for (int i = 0; i < scored.Count && i < count; i++) list.Add(scored[i].Key);
            return list;
        }

        /// <summary>
        /// An AI team that loses a rider mid-season fills the seat at once: the best free rider who accepts, or a
        /// newcomer if nobody is available.
        /// </summary>
        public static void FillVacancyNow(WorldState world, GameDatabase db, Team team)
        {
            var cat = db.GetCategory(team.CategoryId);
            var rules = db.GetRules(cat.Id, world.Season);
            int capacity = Capacity(cat, rules, team);
            var rng = world.Rng.Fork("vacancy-" + team.Id + "-" + world.EventsCompleted);
            foreach (var r in FreeCandidates(world, db, team, capacity - team.RiderIds.Count)) SignNow(world, db, team, r, world.Season);
            while (team.RiderIds.Count < capacity)
            {
                double level;
                CategoryLevels(world, db).TryGetValue(cat.Id, out level);
                var r = RiderGenerator.GenerateForCategory(world, db, rng, cat, level);
                world.Riders[r.Id] = r;
                SignNow(world, db, team, r, world.Season);
            }
        }

        /// <summary>Signs a rider for a team from now until the end of the given season (immediate seat).</summary>
        public static void SignNow(WorldState world, GameDatabase db, Team team, Rider r, int lastSeason)
        {
            Contracts.Close(world, r, ContractEnd.Expired);
            var rules = db.GetRules(team.CategoryId, world.Season);
            var old = world.GetTeam(r.TeamId);
            if (old != null) old.RiderIds.Remove(r.Id);
            r.TeamId = team.Id;
            r.CategoryId = team.CategoryId;
            r.SeasonsWithoutSeat = 0;
            r.Contract = NewContract(team, r, world.Season, lastSeason - world.Season + 1, AgreedSalary(team, r, rules), FundingFor(world, db, team, r));
            if (!team.RiderIds.Contains(r.Id)) team.RiderIds.Add(r.Id);
            if (r.Number <= 0 || NumberTaken(world, r)) r.Number = RiderGenerator.FreeNumber(world, team.CategoryId, world.Rng.Fork("number-" + r.Id));
            StaffService.AssignToRiders(world, team);
            if (old != null && old != team) StaffService.AssignToRiders(world, old);
        }

        /// <summary>How much a team values a rider for one of its seats next season (for tools and tests).</summary>
        public static double EvaluateForTeam(WorldState world, GameDatabase db, Team team, Rider r)
        {
            var cat = db.GetCategory(team.CategoryId);
            var seat = MakeSeat(team, cat, db.GetRules(cat.Id, world.Season));
            return TeamScore(world, db, seat, r, new DateTime(world.Season + 1, 3, 1), CategoryLevels(world, db));
        }

        // ------------------------------------------------------------------ mutations

        private static void Sign(WorldState world, GameDatabase db, Seat seat, Rider r, int next, DeterministicRandom rng, Dictionary<string, double> levels)
        {
            Contracts.Close(world, r, ContractEnd.Expired);
            var oldTeam = world.GetTeam(r.TeamId);
            if (oldTeam != null) oldTeam.RiderIds.Remove(r.Id);
            r.TeamId = seat.Team.Id;
            r.CategoryId = seat.Category.Id;
            r.SeasonsWithoutSeat = 0;
            int years = seat.Category.IsCup ? 1 : TeamAI.ContractYears(seat.Team, r.AgeOn(new DateTime(next, 3, 1)), rng);
            r.Contract = NewContract(seat.Team, r, next, years, AgreedSalary(seat.Team, r, seat.Rules), FundingFor(world, db, seat.Team, r, levels));
            if (!seat.Team.RiderIds.Contains(r.Id)) seat.Team.RiderIds.Add(r.Id);
            if (r.Number <= 0 || NumberTaken(world, r)) r.Number = RiderGenerator.FreeNumber(world, seat.Category.Id, rng);
        }

        private static Contract NewContract(Team team, Rider r, int start, int years, double salary, double funding)
        {
            var c = new Contract
            {
                Id = "ctr_" + r.Id + "_" + team.Id + "_" + start,
                RiderFunding = funding,
                TeamId = team.Id,
                ManufacturerId = team.ManufacturerId,
                StartSeason = start,
                EndSeason = start + Math.Max(1, years) - 1,
                SalaryPerSeason = salary,
                FactoryBacked = team.Status == TeamStatus.Factory
            };
            Contracts.StandardTerms(c);
            // Valued riders get a release clause their team sets high.
            if (salary > 0) c.Clauses.Add(new ContractClause { Type = ClauseType.ReleaseClause, Amount = Math.Round(salary * Math.Max(1, years) * 2.5 / 1000) * 1000 });
            return c;
        }

        /// <summary>Standard race bonuses (estimated: real terms are not public) for contracts that have none.</summary>
        public static void StandardBonuses(Contract c, string riderId)
        {
            if (c == null || c.RaceBonuses.Count > 0) return;
            if (c.Id == null) c.Id = "ctr_init_" + riderId + "_" + c.TeamId;
            Contracts.StandardTerms(c);
        }

        private static void Release(WorldState world, Rider r, MarketReport report)
        {
            Contracts.Close(world, r, ContractEnd.Expired);
            var team = world.GetTeam(r.TeamId);
            if (team != null) team.RiderIds.Remove(r.Id);
            r.TeamId = null;
            r.Contract = null;
            report.Released.Add(r.Id);
        }

        private static Rider GenerateFor(WorldState world, GameDatabase db, Seat seat, DeterministicRandom rng, Dictionary<string, double> levels)
        {
            double level;
            levels.TryGetValue(seat.Category.Id, out level);
            var r = RiderGenerator.GenerateForCategory(world, db, rng, seat.Category, level);
            world.Riders[r.Id] = r;
            return r;
        }

        private static void InsertSeat(List<Seat> seats, int from, Seat seat)
        {
            int i = from;
            while (i < seats.Count && CompareSeats(seats[i], seat) <= 0) i++;
            seats.Insert(i, seat);
        }

        private static Seat MakeSeat(Team team, ChampionshipCategory cat, CategoryRules rules)
        {
            return new Seat { Team = team, Category = cat, Rules = rules, Prestige = cat.Prestige + team.Reputation * 0.1 };
        }

        private static int CompareSeats(Seat a, Seat b)
        {
            int c = b.Prestige.CompareTo(a.Prestige);
            return c != 0 ? c : string.CompareOrdinal(a.Team.Id, b.Team.Id);
        }

        public static int Capacity(ChampionshipCategory cat, CategoryRules rules, Team team = null)
        {
            if (team != null && team.RiderSlots > 0) return team.RiderSlots;
            return cat.IsCup ? rules.MaxEntries : rules.RidersPerTeam;
        }

        public static bool IsCommitted(Rider r, int season)
        {
            return !r.IsRetired && r.TeamId != null && r.Contract != null && r.Contract.EndSeason > season;
        }

        private static int CommittedCount(WorldState world, Team team, int season)
        {
            int n = 0;
            foreach (var r in world.Riders.Values)
                if (r.TeamId == team.Id && IsCommitted(r, season)) n++;
            return n;
        }

        private static bool NumberTaken(WorldState world, Rider rider)
        {
            foreach (var r in world.Riders.Values)
                if (r != rider && !r.IsRetired && r.TeamId != null && r.CategoryId == rider.CategoryId && r.Number == rider.Number) return true;
            return false;
        }

        public static void RebuildRosters(WorldState world)
        {
            foreach (var t in world.Teams.Values) t.RiderIds.Clear();
            foreach (var r in SortedRiders(world))
            {
                if (r.IsRetired || r.TeamId == null) continue;
                var t = world.GetTeam(r.TeamId);
                if (t == null)
                {
                    r.TeamId = null;
                    continue;
                }
                t.RiderIds.Add(r.Id);
                r.CategoryId = t.CategoryId;
            }
        }

        private static List<Rider> SortedRiders(WorldState world)
        {
            var list = new List<Rider>(world.Riders.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        private static List<Team> SortedTeams(WorldState world)
        {
            var list = new List<Team>(world.Teams.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        private static void ReportNews(WorldState world, GameDatabase db, MarketReport report)
        {
            foreach (var m in report.Moves)
            {
                if (m.Renewal || m.Generated) continue;
                var r = world.GetRider(m.RiderId);
                var to = world.GetTeam(m.ToTeamId);
                var toCat = db.GetCategory(m.ToCategoryId);
                var fromCat = db.GetCategory(m.FromCategoryId);
                if (r == null || to == null || toCat == null) continue;
                bool notable = r.Reputation > 55 || toCat.Tier == 1 || to.IsPlayer || (fromCat != null && fromCat.Pyramid != toCat.Pyramid);
                if (!notable) continue;
                string headline = fromCat != null && fromCat.Id != toCat.Id
                    ? Txt.F("news.stepUp", "{0} moves from {1} to {2}", r.FullName, fromCat.ShortName, toCat.ShortName)
                    : Txt.F("news.joins", "{0} joins {1}", r.FullName, to.Name);
                world.AddNews(headline, Txt.F("news.signsBody", "{0} signs with {1} for {2}.", r.FullName, to.Name, world.Season + 1), toCat.Id, toCat.Tier == 1 || to.IsPlayer);
            }
        }
    }
}
