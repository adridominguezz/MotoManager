using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    /// <summary>
    /// The job market of the AI managers: the owners judge their manager every winter (renewal, contract that runs
    /// out, sacking), managers gain experience, age and retire, and vacant teams hire from the managers out of work,
    /// tempt a successful manager away from a smaller team or give a newcomer a chance.
    /// Organisations owned by the player keep their manager until the player decides otherwise.
    /// </summary>
    public static class ManagerMarket
    {
        /// <summary>Age from which managers out of work may retire (and those in work at the end of a contract).</summary>
        public const int RetirementAge = 64;

        /// <summary>Managers out of work and still looking, best known first.</summary>
        public static List<ManagerProfile> FreeAgents(WorldState world)
        {
            var list = new List<ManagerProfile>();
            foreach (var m in world.Managers.Values)
                if (!m.IsPlayer && !m.Retired && m.Job == null) list.Add(m);
            list.Sort((a, b) =>
            {
                int c = b.Reputation.CompareTo(a.Reputation);
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });
            return list;
        }

        /// <summary>
        /// Managers an owner can choose from for a team: those out of work, topped up with a few newcomers looking for
        /// their first job when the pool is thin.
        /// </summary>
        public static List<ManagerProfile> Candidates(WorldState world, GameDatabase db, Team team, int count = 6)
        {
            var free = FreeAgents(world);
            if (free.Count < count)
            {
                var rng = world.Rng.Fork("manager-candidates-" + team.Id + "-" + world.Season + "-" + free.Count);
                for (int i = free.Count; i < count; i++) CareerService.GenerateAiManager(world, db, rng, team.Reputation * rng.Range(0.6, 1.1));
                free = FreeAgents(world);
            }
            string orgId = team.OrganisationId;
            free.Sort((a, b) =>
            {
                int c = Standing(b, orgId).CompareTo(Standing(a, orgId));
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });
            if (free.Count > count) free.RemoveRange(count, free.Count - count);
            return free;
        }

        /// <summary>How attractive a manager looks to a board (reputation first, then sporting management and relations).</summary>
        public static double Standing(ManagerProfile m, string organisationId)
        {
            return m.Reputation + m.SportingManagement * 0.25 + m.Relations * 0.15 + m.RelationWith(organisationId) * 0.3;
        }

        /// <summary>What a board expects of its manager (same scale as <see cref="Standing"/>).</summary>
        public static double Required(GameDatabase db, Team team)
        {
            var cat = db.GetCategory(team.CategoryId);
            return (cat != null ? cat.Prestige : 50) * 0.35 + team.Reputation * 0.45 + (team.Status == TeamStatus.Factory ? 10 : 0);
        }

        /// <summary>
        /// End of season for every AI manager: the owners' verdict on the season, experience and reputation, then the
        /// contract (runs on, renewed, not renewed or sacked), retirements and the hiring round for the vacancies.
        /// Runs after the teams' own season review (it reads whether each team met its objective).
        /// </summary>
        public static void SeasonEnd(WorldState world, GameDatabase db, DeterministicRandom rng)
        {
            int season = world.Season;
            foreach (var team in SortedTeams(world))
            {
                var m = world.GetManager(team.ManagerId);
                if (m == null || m.IsPlayer || m.Job == null || m.Job.TeamId != team.Id) continue;
                var cat = db.GetCategory(team.CategoryId);
                var org = world.OrganisationOf(team);
                bool playerOwned = org != null && org.Owner == OwnerKind.Player;
                var p = team.Personality;
                bool judged = p != null && p.LastPosition > 0 && cat != null && cat.Simulated;
                if (judged)
                {
                    int teams = TeamsIn(world, team.CategoryId);
                    int target = TeamAI.TargetPosition(p.LastObjective, teams);
                    double verdict = MathX.Clamp(60 + (target - p.LastPosition) * 4 + (p.LastObjectiveMet ? 8 : -8), 0, 100);
                    m.Job.BoardConfidence = Math.Round(MathX.Clamp(m.Job.BoardConfidence * 0.4 + verdict * 0.6, 0, 100), 1);
                    double rep = p.LastObjectiveMet ? 2 : -1.5;
                    if (p.LastPosition == 1) rep += 4;
                    m.Reputation = Math.Round(MathX.Clamp(m.Reputation + rep, 1, 99), 1);
                    if (org != null) m.ChangeRelation(org.Id, p.LastObjectiveMet ? 4 : -2);
                }
                Experience(world, m, rng);

                if (playerOwned)
                {
                    // The player owns the team: the manager stays until the owner replaces him.
                    if (m.Job.EndSeason <= season) m.Job.EndSeason = season + 1;
                    continue;
                }
                bool tier1 = cat != null && cat.Tier == 1;
                bool sackable = judged && !p.LastObjectiveMet && m.Job.BoardConfidence < ManagerCareer.SackBelow;
                if (sackable && m.Job.EndSeason > season)
                {
                    double severance = Math.Round(m.Job.SalaryPerSeason * m.Job.SeveranceSeasons);
                    if (severance > 0)
                    {
                        Accounts.Post(world, team, LedgerKind.Severance, -severance, m.FullName);
                        m.PersonalWealth += severance;
                    }
                    if (org != null) m.ChangeRelation(org.Id, -10);
                    m.Reputation = Math.Round(MathX.Clamp(m.Reputation - 3, 1, 99), 1);
                    CareerService.EndJob(world, m, "Sacked");
                    if (Visible(world, cat)) world.AddNews(Txt.F("news.aiManagerSacked", "{0} sacks its manager {1}", team.Name, m.FullName), "", team.CategoryId, tier1);
                    continue;
                }
                if (m.Job.EndSeason > season) continue;
                int age = season + 1 - m.BirthYear;
                if (age >= RetirementAge && rng.Chance(0.35 + (age - RetirementAge) * 0.1))
                {
                    CareerService.EndJob(world, m, "Retired");
                    m.Retired = true;
                    if (Visible(world, cat)) world.AddNews(Txt.F("news.managerRetires", "{0} retires", m.FullName),
                        Txt.F("news.managerRetiresBody", "The manager of {0} ends his career.", team.Name), team.CategoryId);
                    continue;
                }
                double keep = m.Job.BoardConfidence >= ManagerCareer.RenewFrom ? 0.85 : 0.2;
                if (rng.Chance(keep))
                {
                    m.Job.EndSeason = season + (m.Job.BoardConfidence >= 70 ? 3 : 2);
                    m.Job.RenewedSeason = season;
                    if (org != null) m.ChangeRelation(org.Id, 5);
                }
                else
                {
                    CareerService.EndJob(world, m, "ContractEnded");
                    if (Visible(world, cat)) world.AddNews(Txt.F("news.aiManagerLeaves", "{0} and {1} part ways", team.Name, m.FullName),
                        Txt.F("news.aiManagerLeavesBody", "His contract is not renewed."), team.CategoryId);
                }
            }

            // Out of work for too long, or too old: some stop looking.
            foreach (var m in FreeAgents(world))
            {
                int age = season + 1 - m.BirthYear;
                int idle = m.History.Count > 0 ? season - m.History[m.History.Count - 1].ToSeason : 0;
                if ((age >= RetirementAge && rng.Chance(0.5)) || idle >= 3 || (idle >= 1 && m.Reputation < 15 && rng.Chance(0.4)))
                    m.Retired = true;
            }

            FillVacancies(world, db, rng, true, season + 1);
        }

        /// <summary>
        /// Gives every team without a manager one (the player's own team excluded). The most prestigious jobs are filled
        /// first. Candidates: managers out of work and, between seasons, successful managers of smaller teams (their old
        /// team then looks for someone too). Without a suitable candidate the board gives a newcomer a chance.
        /// </summary>
        public static void FillVacancies(WorldState world, GameDatabase db, DeterministicRandom rng, bool poach, int startSeason = 0)
        {
            var queue = new List<Team>();
            foreach (var t in SortedTeams(world))
                if (NeedsManager(world, t)) queue.Add(t);
            Sort(db, queue);
            int guard = 0;
            while (queue.Count > 0 && guard++ < 500)
            {
                var team = queue[0];
                queue.RemoveAt(0);
                if (!NeedsManager(world, team)) continue;
                var org = world.OrganisationOf(team);
                string orgId = org != null ? org.Id : null;
                double required = Required(db, team);
                ManagerProfile best = null;
                double bestScore = double.MinValue;
                foreach (var c in FreeAgents(world))
                {
                    if (c.RelationWith(orgId) <= -20) continue;
                    double s = Standing(c, orgId);
                    if (s < required * 0.75) continue;
                    s += rng.Gaussian(0, 4);
                    if (s > bestScore) { best = c; bestScore = s; }
                }
                Team released = null;
                if (poach)
                {
                    foreach (var other in SortedTeams(world))
                    {
                        var c = world.GetManager(other.ManagerId);
                        if (c == null || c.IsPlayer || c.Job == null || other == team) continue;
                        var otherOrg = world.OrganisationOf(other);
                        if (otherOrg != null && (otherOrg.Owner == OwnerKind.Player || otherOrg.Id == orgId)) continue;
                        // A clear step up, for someone the owners there are happy with.
                        if (Required(db, other) + 8 > required || c.Job.BoardConfidence < 55 || c.RelationWith(orgId) <= -20) continue;
                        double s = Standing(c, orgId);
                        if (s < required * 0.9 || !rng.Chance(0.4)) continue;
                        s += rng.Gaussian(0, 4);
                        if (s > bestScore) { best = c; bestScore = s; released = other; }
                    }
                }
                if (best == null)
                {
                    best = CareerService.GenerateAiManager(world, db, rng, team.Reputation);
                    released = null;
                }
                else if (released == null || best.Job == null || best.Job.TeamId != released.Id) released = null;
                if (released != null)
                {
                    var oldOrg = world.OrganisationOf(released);
                    if (oldOrg != null) best.ChangeRelation(oldOrg.Id, -5);
                }
                CareerService.Employ(world, db, best, team, team.Reputation > 70 || (org != null && org.Owner == OwnerKind.Manufacturer) ? Autonomy.Limited : Autonomy.Standard,
                    2 + rng.NextInt(0, 2), -1, startSeason);
                var cat = db.GetCategory(team.CategoryId);
                if (world.EventsCompleted > 0 || startSeason > 0)
                    if (Visible(world, cat))
                        world.AddNews(Txt.F("news.aiManagerHired", "{0} appoints {1} as manager", team.Name, best.FullName),
                            released != null ? Txt.F("news.aiManagerHiredFrom", "He leaves {0} for the new job.", released.Name) : "", team.CategoryId, cat != null && cat.Tier == 1);
                if (released != null && !queue.Contains(released))
                {
                    queue.Add(released);
                    Sort(db, queue);
                }
            }
        }

        /// <summary>Managers learn on the job; past sixty the edge slowly fades.</summary>
        public static void Experience(WorldState world, ManagerProfile m, DeterministicRandom rng)
        {
            int age = world.Season + 1 - m.BirthYear;
            double grow = age < 60 ? 1 : -0.5;
            m.SportingManagement = Grow(m.SportingManagement, grow * rng.Range(0.3, 1.2));
            m.Negotiation = Grow(m.Negotiation, grow * rng.Range(0.2, 0.9));
            m.Finance = Grow(m.Finance, grow * rng.Range(0.1, 0.8));
            m.Relations = Grow(m.Relations, grow * rng.Range(0.2, 0.9));
            m.Commercial = Grow(m.Commercial, grow * rng.Range(0.1, 0.7));
        }

        private static double Grow(double v, double d)
        {
            // Harder to improve near the top.
            if (d > 0) d *= (100 - v) / 60.0;
            return Math.Round(MathX.Clamp(v + d, 1, 99), 1);
        }

        private static bool NeedsManager(WorldState world, Team team)
        {
            if (team.IsPlayer && world.PlayerManager != null && world.PlayerManager.Job != null && world.PlayerManager.Job.TeamId == team.Id) return false;
            var m = world.GetManager(team.ManagerId);
            return m == null || m.Job == null || m.Job.TeamId != team.Id;
        }

        private static bool Visible(WorldState world, ChampionshipCategory cat)
        {
            if (cat == null || !cat.Simulated) return false;
            var player = world.PlayerTeam;
            return cat.Tier == 1 || (player != null && player.CategoryId == cat.Id);
        }

        private static int TeamsIn(WorldState world, string categoryId)
        {
            int n = 0;
            foreach (var t in world.Teams.Values) if (t.CategoryId == categoryId) n++;
            return Math.Max(1, n);
        }

        private static void Sort(GameDatabase db, List<Team> teams)
        {
            teams.Sort((a, b) =>
            {
                int c = Required(db, b).CompareTo(Required(db, a));
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });
        }

        private static List<Team> SortedTeams(WorldState world)
        {
            var list = new List<Team>(world.Teams.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }
    }
}
