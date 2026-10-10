using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
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

            // 1. Age limits and expired contracts.
            foreach (var r in SortedRiders(world))
            {
                if (r.IsRetired) continue;
                var cat = db.GetCategory(r.CategoryId);
                if (cat != null && r.TeamId != null && r.AgeOn(nextDate) > cat.MaxAge)
                    Release(world, r, report);
            }

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
                    r.Contract = NewContract(team, r, next, rng.NextInt(1, 3), SalaryDemand(r, rules));
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

            // 4. Fill.
            var levels = CategoryLevels(world, db);
            for (int s = 0; s < seats.Count; s++)
            {
                var seat = seats[s];
                Rider best = null;
                double bestScore = double.MinValue;
                foreach (var r in SortedRiders(world))
                {
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
                    // Buyout: the rider still had a deal elsewhere -> that seat opens and is filled later.
                    if (IsCommitted(best, season) && best.TeamId != null && best.TeamId != seat.Team.Id)
                    {
                        move.Buyout = true;
                        var oldTeam = world.GetTeam(best.TeamId);
                        var oldCat = oldTeam != null ? db.GetCategory(oldTeam.CategoryId) : null;
                        if (oldTeam != null && oldCat != null && oldCat.Simulated)
                        {
                            var vacancy = MakeSeat(oldTeam, oldCat, db.GetRules(oldCat.Id, season));
                            InsertSeat(seats, s + 1, vacancy);
                        }
                    }
                }
                move.RiderId = best.Id;
                Sign(world, db, seat, best, next, rng);
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

        // ------------------------------------------------------------------ evaluation

        public static double SalaryDemand(Rider r, CategoryRules rules)
        {
            double typical = rules != null ? rules.Economy.TypicalRiderSalary : 100000;
            if (typical <= 0) return 0;
            return Math.Round(typical * Math.Exp((r.Reputation - 55) / 15.0) / 1000) * 1000;
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
            double p = 0.85 - rank * 0.7 + (team.Status == TeamStatus.Factory ? -0.1 : 0) + (age < 24 ? 0.1 : 0) - (age > 34 ? 0.15 : 0);
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
                // Only a clear step up justifies breaking an existing deal.
                var current = db.GetCategory(r.CategoryId);
                if (current == null || cat.Prestige < current.Prestige + 10) return false;
                var currentTeam = world.GetTeam(r.TeamId);
                if (currentTeam != null && currentTeam.IsPlayer) return false;
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

        private static double SeatUtility(ChampionshipCategory cat, Team team, Rider r)
        {
            double u = cat.Prestige + team.Reputation * 0.3 + (team.Status == TeamStatus.Factory ? 6 : 0);
            double pref = (r.Personality.GrandPrixPreference - 50) * 0.3;
            u += cat.Pyramid == Pyramid.GrandPrix ? pref : -pref;
            return u;
        }

        private static bool RiderAccepts(WorldState world, GameDatabase db, Seat seat, Rider r, int season, DateTime nextDate)
        {
            double offer = SeatUtility(seat.Category, seat.Team, r);
            var current = db.GetCategory(r.CategoryId);
            var currentTeam = world.GetTeam(r.TeamId);
            if (IsCommitted(r, season) && current != null && currentTeam != null)
                return offer > SeatUtility(current, currentTeam, r) + 8;
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
            if (champ != null)
            {
                int pos = champ.RiderPosition(r.Id);
                var from = db.GetCategory(r.CategoryId);
                if (pos > 0 && from != null) score += Math.Max(0, 8 - pos) * (from.Prestige / 100.0) * 1.5;
            }

            if (cat.Pyramid == Pyramid.GrandPrix && age > 32) score -= (age - 32) * 1.5;
            if (cat.Pyramid == Pyramid.Superbike && age > 35) score -= (age - 35) * 1.5;

            var from2 = db.GetCategory(r.CategoryId);
            if (from2 != null && from2.Pyramid != cat.Pyramid && r.Reputation < 75) score -= 3;
            if (r.Contract != null && r.Contract.ManufacturerId != null && r.Contract.ManufacturerId == seat.Team.ManufacturerId) score += 3;
            if (r.TeamId == seat.Team.Id) score += 2;

            double demand = SalaryDemand(r, seat.Rules);
            double affordable = Math.Max(50000, seat.Team.Budget * 0.25);
            if (demand > affordable) score -= (demand / affordable - 1) * 5;

            // Sub-par riders for the level are unattractive.
            double level;
            if (levels.TryGetValue(cat.Id, out level) && ability < level - 12) score -= (level - 12 - ability);
            return score;
        }

        // ------------------------------------------------------------------ mutations

        private static void Sign(WorldState world, GameDatabase db, Seat seat, Rider r, int next, DeterministicRandom rng)
        {
            var oldTeam = world.GetTeam(r.TeamId);
            if (oldTeam != null) oldTeam.RiderIds.Remove(r.Id);
            r.TeamId = seat.Team.Id;
            r.CategoryId = seat.Category.Id;
            r.SeasonsWithoutSeat = 0;
            int years = seat.Category.IsCup ? 1 : rng.NextInt(1, 3);
            r.Contract = NewContract(seat.Team, r, next, years, SalaryDemand(r, seat.Rules));
            if (!seat.Team.RiderIds.Contains(r.Id)) seat.Team.RiderIds.Add(r.Id);
            if (r.Number <= 0 || NumberTaken(world, r)) r.Number = RiderGenerator.FreeNumber(world, seat.Category.Id, rng);
        }

        private static Contract NewContract(Team team, Rider r, int start, int years, double salary)
        {
            return new Contract
            {
                TeamId = team.Id,
                ManufacturerId = team.ManufacturerId,
                StartSeason = start,
                EndSeason = start + Math.Max(1, years) - 1,
                SalaryPerSeason = salary,
                WinBonus = Math.Round(salary * 0.05),
                PodiumBonus = Math.Round(salary * 0.02),
                FactoryBacked = team.Status == TeamStatus.Factory
            };
        }

        private static void Release(WorldState world, Rider r, MarketReport report)
        {
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
                    ? r.FullName + " moves from " + fromCat.ShortName + " to " + toCat.ShortName
                    : r.FullName + " joins " + to.Name;
                world.AddNews(headline, r.FullName + " signs with " + to.Name + " for " + (world.Season + 1) + ".", toCat.Id, toCat.Tier == 1 || to.IsPlayer);
            }
        }
    }
}
