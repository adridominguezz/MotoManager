using System;
using System.Collections.Generic;
using MotoManager.Core.World;

namespace MotoManager.Core.Economy
{
    /// <summary>
    /// The single entry point for money. Every income or expense of every team (player and AI alike) is posted
    /// here: the organisation's cash changes, the movement is totalled per team/season/kind and, for organisations
    /// the player is involved with, kept line by line. Operations that could run twice (an event processed again,
    /// a button pressed twice, a clause evaluated after a reload) are guarded with keys.
    /// </summary>
    public static class Accounts
    {
        /// <summary>Seasons of per-kind totals kept (older seasons survive as yearly summaries).</summary>
        public const int TotalsSeasonsKept = 2;

        // ------------------------------------------------------------------ idempotency

        /// <summary>
        /// Marks an operation as applied. Returns false when it was applied before (the caller must then skip it).
        /// Keys are scoped to the season so they can be pruned.
        /// </summary>
        public static bool TryBegin(WorldState world, string key)
        {
            return world.Finance.AppliedKeys.Add(world.Season + "|" + key);
        }

        public static bool WasApplied(WorldState world, string key)
        {
            return world.Finance.AppliedKeys.Contains(world.Season + "|" + key);
        }

        // ------------------------------------------------------------------ posting

        /// <summary>Posts a movement for a team (money goes to/from its organisation).</summary>
        public static void Post(WorldState world, Team team, LedgerKind kind, double amount, string note = null, bool cash = true, bool pnl = true)
        {
            var org = world.OrganisationOf(team);
            if (org == null) throw new InvalidOperationException("Team " + (team != null ? team.Id : "null") + " has no organisation");
            Post(world, org, team.Id, kind, amount, note, cash, pnl);
        }

        /// <summary>Posts a movement for an organisation (teamId null for organisation-level items).</summary>
        public static void Post(WorldState world, Organisation org, string teamId, LedgerKind kind, double amount, string note = null, bool cash = true, bool pnl = true)
        {
            if (amount == 0 || double.IsNaN(amount)) return;
            amount = Math.Round(amount, 2);
            if (cash) org.Cash += amount;

            var total = FindTotal(world, world.Season, org.Id, teamId, kind, true);
            if (cash) total.Cash += amount;
            if (pnl) total.Pnl += amount;

            if (org.DetailedLedger)
            {
                world.Finance.Entries.Add(new LedgerEntry
                {
                    Date = world.CurrentDate, Season = world.Season, OrganisationId = org.Id, TeamId = teamId,
                    Kind = kind, Amount = amount, Cash = cash, Pnl = pnl, Note = note
                });
            }
        }

        private static AccountTotal FindTotal(WorldState world, int season, string orgId, string teamId, LedgerKind kind, bool create)
        {
            var list = world.Finance.Totals;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var t = list[i];
                if (t.Season == season && t.Kind == kind && t.OrganisationId == orgId && t.TeamId == teamId) return t;
            }
            if (!create) return null;
            var n = new AccountTotal { Season = season, OrganisationId = orgId, TeamId = teamId, Kind = kind };
            list.Add(n);
            return n;
        }

        // ------------------------------------------------------------------ queries

        public static double Cash(WorldState world, Team team)
        {
            var org = world.OrganisationOf(team);
            return org != null ? org.Cash : 0;
        }

        /// <summary>Money a team has spent this season (all cash outflows attributed to it).</summary>
        public static double SpentThisSeason(WorldState world, Team team)
        {
            double spent = 0;
            foreach (var t in world.Finance.Totals)
                if (t.Season == world.Season && t.TeamId == team.Id && t.Cash < 0) spent -= t.Cash;
            return spent;
        }

        /// <summary>What is left of the team's season allocation (can be negative when overspent).</summary>
        public static double BudgetRemaining(WorldState world, Team team)
        {
            return team.SeasonBudget - SpentThisSeason(world, team);
        }

        /// <summary>Totals by kind for an organisation (optionally one team) in a season.</summary>
        public static Dictionary<LedgerKind, AccountTotal> TotalsFor(WorldState world, string orgId, int season, string teamId = null)
        {
            var map = new Dictionary<LedgerKind, AccountTotal>();
            foreach (var t in world.Finance.Totals)
            {
                if (t.Season != season || t.OrganisationId != orgId) continue;
                if (teamId != null && t.TeamId != teamId) continue;
                AccountTotal acc;
                if (!map.TryGetValue(t.Kind, out acc)) map[t.Kind] = acc = new AccountTotal { Season = season, OrganisationId = orgId, TeamId = teamId, Kind = t.Kind };
                acc.Cash += t.Cash;
                acc.Pnl += t.Pnl;
            }
            return map;
        }

        /// <summary>Net cash movement of an organisation this season (Cash should equal opening + this).</summary>
        public static double SeasonCashFlow(WorldState world, string orgId)
        {
            double sum = 0;
            foreach (var t in world.Finance.Totals)
                if (t.Season == world.Season && t.OrganisationId == orgId) sum += t.Cash;
            return sum;
        }

        public static List<LedgerEntry> Entries(WorldState world, string orgId, int season)
        {
            var list = new List<LedgerEntry>();
            foreach (var e in world.Finance.Entries)
                if (e.OrganisationId == orgId && e.Season == season) list.Add(e);
            return list;
        }

        // ------------------------------------------------------------------ season close

        /// <summary>
        /// Called once the season's last money has moved (before the roll-over): writes the yearly summaries and
        /// prunes detailed lines, old totals and old keys.
        /// </summary>
        public static void CloseSeason(WorldState world)
        {
            int season = world.Season;
            var orgs = new List<Organisation>(world.Organisations.Values);
            orgs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var org in orgs)
            {
                var summary = new OrganisationSeasonSummary { Season = season, OrganisationId = org.Id, OpeningCash = org.SeasonOpeningCash, ClosingCash = org.Cash };
                foreach (var t in world.Finance.Totals)
                {
                    if (t.Season != season || t.OrganisationId != org.Id) continue;
                    if (t.Kind == LedgerKind.OwnerFunding) summary.OwnerFunding += t.Cash;
                    else if (t.Pnl > 0) summary.Income += t.Pnl;
                    else summary.Expenses -= t.Pnl;
                }
                world.Finance.Summaries.Add(summary);
                org.SeasonOpeningCash = org.Cash;
            }
            world.Finance.Entries.RemoveAll(e => e.Season < season);
            world.Finance.Totals.RemoveAll(t => t.Season <= season - TotalsSeasonsKept);
            var keep = new HashSet<string>();
            string current = season + "|";
            foreach (var k in world.Finance.AppliedKeys)
                if (k.StartsWith(current, StringComparison.Ordinal)) keep.Add(k);
            world.Finance.AppliedKeys = keep;
        }
    }
}
