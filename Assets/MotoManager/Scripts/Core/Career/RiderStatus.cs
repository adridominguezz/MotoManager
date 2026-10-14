using MotoManager.Core.Common;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    /// <summary>A rider's standing inside his team: lead rider, second rider, or equal status with the team-mate.</summary>
    public enum RiderStatus
    {
        Equal,
        Lead,
        Second
    }

    /// <summary>
    /// The team's pecking order (<see cref="Team.LeadRiderId"/>). One rider may be named lead; the others are then
    /// second riders; no lead means equal status. The effect is on morale only and is tied to the status itself (lead
    /// +5, equal 0, second -5): a change applies the difference, so switching back and forth gains nothing.
    /// </summary>
    public static class RiderStatusRules
    {
        public const double LeadMorale = 5, SecondMorale = -5;

        /// <summary>The status of <paramref name="riderId"/> in <paramref name="team"/> (equal for a stale or empty lead).</summary>
        public static RiderStatus Of(Team team, string riderId)
        {
            string lead = Lead(team);
            if (lead == null || riderId == null || !team.RiderIds.Contains(riderId)) return RiderStatus.Equal;
            return lead == riderId ? RiderStatus.Lead : RiderStatus.Second;
        }

        /// <summary>The lead rider's id while he is still in the team, otherwise null.</summary>
        public static string Lead(Team team)
        {
            if (team == null || string.IsNullOrEmpty(team.LeadRiderId)) return null;
            return team.RiderIds.Contains(team.LeadRiderId) ? team.LeadRiderId : null;
        }

        /// <summary>The morale offset that goes with a status.</summary>
        public static double MoraleOf(RiderStatus s)
        {
            return s == RiderStatus.Lead ? LeadMorale : s == RiderStatus.Second ? SecondMorale : 0;
        }

        /// <summary>
        /// Gives <paramref name="riderId"/> the status <paramref name="status"/>: lead names him; second names his first
        /// team-mate lead (no team-mate: nothing changes); equal clears the order. Every rider of the team gets the
        /// morale difference between his new and old status (clamped 5-100). Returns false when nothing changed.
        /// </summary>
        public static bool Set(WorldState world, Team team, string riderId, RiderStatus status)
        {
            if (team == null || riderId == null || !team.RiderIds.Contains(riderId)) return false;
            string lead = null;
            if (status == RiderStatus.Lead) lead = riderId;
            else if (status == RiderStatus.Second)
            {
                foreach (var id in team.RiderIds)
                    if (id != riderId) { lead = id; break; }
                if (lead == null) return false;
            }
            if (lead == Lead(team)) return false;

            var before = new RiderStatus[team.RiderIds.Count];
            for (int i = 0; i < team.RiderIds.Count; i++) before[i] = Of(team, team.RiderIds[i]);
            team.LeadRiderId = lead;
            for (int i = 0; i < team.RiderIds.Count; i++)
            {
                var r = world.GetRider(team.RiderIds[i]);
                if (r == null) continue;
                double delta = MoraleOf(Of(team, r.Id)) - MoraleOf(before[i]);
                r.Morale = MathX.Clamp(r.Morale + delta, 5, 100);
            }
            return true;
        }
    }
}
