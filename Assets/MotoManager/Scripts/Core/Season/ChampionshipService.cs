using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using MotoManager.Core.World;

namespace MotoManager.Core.Season
{
    /// <summary>Points, standings and the per-season career statistics of riders.</summary>
    public static class ChampionshipService
    {
        public static ChampionshipState Get(WorldState world, string categoryId)
        {
            ChampionshipState c;
            if (!world.Championships.TryGetValue(categoryId, out c) || c.Season != world.Season)
            {
                c = new ChampionshipState { CategoryId = categoryId, Season = world.Season };
                world.Championships[categoryId] = c;
            }
            return c;
        }

        public static void ApplySession(WorldState world, GameDatabase db, string categoryId, SessionResult result, RaceSimulation sim)
        {
            var champ = Get(world, categoryId);
            bool mainRace = result.Type == SessionType.Race;
            HistoryService.OnRace(world, categoryId, result, null);
            var bestByManufacturer = new Dictionary<string, ResultEntry>();

            foreach (var r in result.Classification)
            {
                var rider = world.GetRider(r.RiderId);
                if (rider == null) continue;
                var team = world.GetTeam(r.TeamId);

                var st = Entry(champ.Riders, r.RiderId);
                st.Points += r.Points;
                if (r.Status == ResultStatus.Finished)
                {
                    if (r.Position == 1 && mainRace) st.Wins++;
                    if (r.Position == 2 && mainRace) st.Seconds++;
                    if (r.Position == 3 && mainRace) st.Thirds++;
                    if (r.Position <= 3 && mainRace) st.Podiums++;
                }
                if (r.TeamId != null) Entry(champ.Teams, r.TeamId).Points += r.Points;
                if (r.ManufacturerId != null && r.Status == ResultStatus.Finished)
                {
                    ResultEntry best;
                    if (!bestByManufacturer.TryGetValue(r.ManufacturerId, out best) || r.Position < best.Position)
                        bestByManufacturer[r.ManufacturerId] = r;
                }

                var rec = rider.GetOrCreateRecord(world.Season, categoryId, r.TeamId);
                rec.TeamName = team != null ? team.Name : rec.TeamName;
                rec.ManufacturerId = r.ManufacturerId;
                rec.Points += r.Points;
                if (team != null)
                {
                    foreach (var mate in team.RiderIds)
                        if (mate != rider.Id) rec.TeammateId = mate;
                }
                if (mainRace)
                {
                    rec.Starts++;
                    if (r.Status == ResultStatus.Finished)
                    {
                        if (r.Position == 1) rec.Wins++;
                        if (r.Position <= 3) rec.Podiums++;
                        if (r.Position < rec.BestFinish) rec.BestFinish = r.Position;
                    }
                    else
                    {
                        rec.Dnfs++;
                    }
                    if (r.FastestLap) rec.FastestLaps++;
                }
                else if (result.Type == SessionType.Sprint && r.Position == 1 && r.Status == ResultStatus.Finished)
                {
                    rec.SprintWins++;
                }

                // Morale reacts to results relative to expectations (grid position).
                if (r.Status == ResultStatus.Finished)
                {
                    double delta = (r.GridPosition - r.Position) * 0.6 + (r.Position <= 3 ? 2 : 0) + (r.Position == 1 ? 2 : 0);
                    rider.Morale = System.Math.Max(5, System.Math.Min(100, rider.Morale + delta * 0.5));
                }
                else
                {
                    rider.Morale = System.Math.Max(5, rider.Morale - 2);
                }
            }

            if (sim != null)
            {
                foreach (var s in sim.Riders)
                {
                    var rec = s.Entry.Rider.GetOrCreateRecord(world.Season, categoryId, s.Entry.Team != null ? s.Entry.Team.Id : null);
                    rec.Crashes += s.Crashes;
                }
            }

            // Constructors: best placed bike of each manufacturer scores.
            var points = db.GetRules(categoryId, world.Season).GetPoints(ResultPointsSystem(db, categoryId, world.Season, result.SessionId));
            foreach (var kv in bestByManufacturer)
            {
                var m = Entry(champ.Manufacturers, kv.Key);
                if (kv.Value.Position - 1 < points.Count) m.Points += points[kv.Value.Position - 1];
                if (kv.Value.Position == 1 && mainRace) m.Wins++;
            }
        }

        private static string ResultPointsSystem(GameDatabase db, string categoryId, int season, string sessionId)
        {
            foreach (var s in db.GetRules(categoryId, season).Weekend.Sessions)
                if (s.Id == sessionId) return s.PointsSystem;
            return "race";
        }

        private static StandingEntry Entry(Dictionary<string, StandingEntry> table, string id)
        {
            StandingEntry e;
            if (!table.TryGetValue(id, out e))
            {
                e = new StandingEntry { Id = id };
                table[id] = e;
            }
            return e;
        }
    }
}
