using System;
using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Season
{
    /// <summary>A rider in the Hall of Fame with the numbers that put him there.</summary>
    public class HallOfFameEntry
    {
        public Rider Rider;
        public int TopTitles;
        public int Titles;
        public int TopWins;
        public int Wins;
        public int Podiums;
        public double Score;
    }

    /// <summary>A rivalry: two riders who keep finishing first and second.</summary>
    public class Rivalry
    {
        public string CategoryId;
        public string A, B;
        public int Duels;
    }

    /// <summary>
    /// The world's memory: each season's podiums, champions and rookie of the year, the records of every
    /// championship (youngest and oldest winners, longest winning streak) and rivalries; and the Hall of Fame,
    /// worked out from the riders' careers.
    /// </summary>
    public static class HistoryService
    {
        private const int MaxDuels = 60;

        public static CategoryRecords Records(WorldState world, string categoryId)
        {
            CategoryRecords r;
            if (!world.Records.TryGetValue(categoryId, out r))
            {
                r = new CategoryRecords();
                world.Records[categoryId] = r;
            }
            return r;
        }

        /// <summary>After a main race: winners' ages, winning streaks and first-and-second duels.</summary>
        public static void OnRace(WorldState world, string categoryId, SessionResult result, string eventName)
        {
            if (result.Type != SessionType.Race) return;
            ResultEntry first = null, second = null;
            foreach (var e in result.Classification)
            {
                if (e.Status != ResultStatus.Finished) continue;
                if (e.Position == 1) first = e;
                else if (e.Position == 2) second = e;
            }
            if (first == null) return;
            var rec = Records(world, categoryId);
            var winner = world.GetRider(first.RiderId);
            if (winner != null)
            {
                double age = Math.Round((world.CurrentDate - winner.DateOfBirth).TotalDays / 365.25, 2);
                if (rec.YoungestWinner == null || age < rec.YoungestWinner.Value)
                    rec.YoungestWinner = new HistoryRecord { RiderId = winner.Id, Name = winner.FullName, Season = world.Season, Value = age, Event = eventName };
                if (rec.OldestWinner == null || age > rec.OldestWinner.Value)
                    rec.OldestWinner = new HistoryRecord { RiderId = winner.Id, Name = winner.FullName, Season = world.Season, Value = age, Event = eventName };
            }
            if (rec.StreakRiderId == first.RiderId) rec.StreakCount++;
            else
            {
                rec.StreakRiderId = first.RiderId;
                rec.StreakCount = 1;
            }
            if (winner != null && (rec.LongestStreak == null || rec.StreakCount > rec.LongestStreak.Value))
                rec.LongestStreak = new HistoryRecord { RiderId = winner.Id, Name = winner.FullName, Season = world.Season, Value = rec.StreakCount, Event = eventName };
            if (second != null)
            {
                string key = string.CompareOrdinal(first.RiderId, second.RiderId) < 0 ? first.RiderId + "|" + second.RiderId : second.RiderId + "|" + first.RiderId;
                int n;
                rec.Duels.TryGetValue(key, out n);
                rec.Duels[key] = n + 1;
                if (rec.Duels.Count > MaxDuels * 2) Prune(rec.Duels);
            }
        }

        private static void Prune(Dictionary<string, int> duels)
        {
            var list = new List<KeyValuePair<string, int>>(duels);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            duels.Clear();
            for (int i = 0; i < list.Count && i < MaxDuels; i++) duels[list[i].Key] = list[i].Value;
        }

        /// <summary>End of season (before the championships are archived): one line of history per championship.</summary>
        public static void SeasonEnd(WorldState world, GameDatabase db, ICollection<string> retiredIds)
        {
            foreach (var cat in db.CategoriesOrdered())
            {
                ChampionshipState champ;
                if (!cat.Simulated || !world.Championships.TryGetValue(cat.Id, out champ) || champ.Season != world.Season) continue;
                var riders = champ.SortedRiders();
                if (riders.Count == 0 || riders[0].Points <= 0) continue;
                var h = new SeasonHistory { Season = world.Season, CategoryId = cat.Id, Races = champ.Rounds.Count };
                Set(world, riders, 0, out h.ChampionId, out h.Champion);
                Set(world, riders, 1, out h.SecondId, out h.Second);
                Set(world, riders, 2, out h.ThirdId, out h.Third);
                h.ChampionPoints = riders[0].Points;
                h.SecondPoints = riders.Count > 1 ? riders[1].Points : 0;
                var teams = champ.SortedTeams();
                if (teams.Count > 0)
                {
                    var t = world.GetTeam(teams[0].Id);
                    h.TeamChampion = t != null ? t.Name : teams[0].Id;
                }
                var makers = champ.SortedManufacturers();
                if (makers.Count > 0) h.MakerChampionId = makers[0].Id;

                // Most wins, rookie of the year (best newcomer to the championship).
                for (int i = 0; i < riders.Count; i++)
                {
                    var r = world.GetRider(riders[i].Id);
                    if (r == null) continue;
                    if (riders[i].Wins > h.MostWinsCount)
                    {
                        h.MostWinsCount = riders[i].Wins;
                        h.MostWins = r.FullName;
                    }
                    if (h.RookieId == null && IsRookie(r, cat.Id, world.Season))
                    {
                        h.RookieId = r.Id;
                        h.Rookie = r.FullName;
                        h.RookiePosition = i + 1;
                    }
                }
                if (retiredIds != null)
                    foreach (var id in retiredIds)
                    {
                        var r = world.GetRider(id);
                        if (r == null || r.CategoryId != cat.Id) continue;
                        int wins = 0;
                        foreach (var rec in r.CareerHistory) if (rec.CategoryId == cat.Id) wins += rec.Wins;
                        if (wins > 0 && h.Retired.Count < 5) h.Retired.Add(r.FullName);
                    }
                world.History.Add(h);
            }
        }

        private static void Set(WorldState world, List<StandingEntry> table, int i, out string id, out string name)
        {
            id = null;
            name = null;
            if (i >= table.Count) return;
            id = table[i].Id;
            var r = world.GetRider(id);
            name = r != null ? r.FullName : id;
        }

        private static bool IsRookie(Rider r, string categoryId, int season)
        {
            foreach (var rec in r.CareerHistory)
                if (rec.CategoryId == categoryId && rec.Season < season) return false;
            return true;
        }

        /// <summary>The pairs who finished first and second most often in a championship.</summary>
        public static List<Rivalry> Rivalries(WorldState world, string categoryId, int count = 5)
        {
            var list = new List<Rivalry>();
            CategoryRecords rec;
            if (!world.Records.TryGetValue(categoryId, out rec)) return list;
            foreach (var kv in rec.Duels)
            {
                var ids = kv.Key.Split('|');
                if (ids.Length != 2) continue;
                var a = world.GetRider(ids[0]);
                var b = world.GetRider(ids[1]);
                list.Add(new Rivalry { CategoryId = categoryId, A = a != null ? a.FullName : ids[0], B = b != null ? b.FullName : ids[1], Duels = kv.Value });
            }
            list.Sort((x, y) => y.Duels.CompareTo(x.Duels));
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            return list;
        }

        /// <summary>
        /// The greats: riders with two or more titles in a top class, twenty or more top-class wins, or three titles
        /// in any championship. Ordered by a score that weighs top-class titles most.
        /// </summary>
        public static List<HallOfFameEntry> HallOfFame(WorldState world, GameDatabase db, int count = 20)
        {
            var list = new List<HallOfFameEntry>();
            foreach (var r in world.Riders.Values)
            {
                var e = new HallOfFameEntry { Rider = r };
                foreach (var rec in r.CareerHistory)
                {
                    var cat = db.GetCategory(rec.CategoryId);
                    bool top = cat != null && cat.Tier == 1 && !cat.IsCup;
                    e.Wins += rec.Wins;
                    e.Podiums += rec.Podiums;
                    if (top) e.TopWins += rec.Wins;
                    if (rec.ChampionshipPosition == 1)
                    {
                        e.Titles++;
                        if (top) e.TopTitles++;
                    }
                }
                if (e.TopTitles >= 2 || e.TopWins >= 20 || e.Titles >= 3)
                {
                    e.Score = e.TopTitles * 10 + (e.Titles - e.TopTitles) * 3 + e.TopWins * 0.6 + e.Wins * 0.2 + e.Podiums * 0.05;
                    list.Add(e);
                }
            }
            list.Sort((a, b) =>
            {
                int c = b.Score.CompareTo(a.Score);
                return c != 0 ? c : string.CompareOrdinal(a.Rider.Id, b.Rider.Id);
            });
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            return list;
        }
    }
}
