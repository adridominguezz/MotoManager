using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;

namespace MotoManager.Core.Season
{
    /// <summary>
    /// Shortcuts for testing the game (the panel that calls them only exists in development builds). Every change
    /// still goes through the normal paths: money is posted to the ledger, projects resolve as usual.
    /// </summary>
    public static class DebugTools
    {
        public static void AddMoney(WorldState world, double amount)
        {
            var team = world.PlayerTeam;
            var org = team != null ? world.OrganisationOf(team) : null;
            if (org == null) return;
            Accounts.Post(world, org, team.Id, LedgerKind.OwnerInvestment, amount, "Debug", true, false);
        }

        /// <summary>The player's development projects finish now (success or failure decided as usual).</summary>
        public static int CompleteDevelopment(WorldState world, GameDatabase db)
        {
            var team = world.PlayerTeam;
            if (team == null) return 0;
            int n = 0;
            foreach (var p in world.Projects)
                if (p.TeamId == team.Id && p.Status == ProjectStatus.InProgress)
                {
                    p.DaysWorked = p.DurationDays;
                    n++;
                }
            if (n > 0) DevelopmentService.Advance(world, db, 0, world.Rng.Fork("debug-dev-" + world.Season + "-" + world.EventsCompleted));
            return n;
        }
    }
}
