using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using MotoManager.Core.Data;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// Home, laid out to fit one screen: on the left what comes next and the team; on the right the last result,
    /// what needs a decision (with shortcuts) and the latest headlines.
    /// </summary>
    public class HubScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.home"); } }
        private static int MaxTodo { get { return UiScale.Compact ? 3 : 4; } }
        private static int MaxNews { get { return UiScale.Compact ? 2 : 4; } }

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            RectTransform left, right;
            Ui.Columns(list, out left, out right);

            if (team == null)
            {
                var none = Ui.Card(left, Loc.T("home.noTeam"), Loc.T("home.noTeamInfo"));
                UiFactory.ButtonRow(none, 64,
                    UiFactory.Primary(Loc.T("mycareer.seeOffers"), () => Shell.Show<CareerOffersPage>()),
                    UiFactory.Act(Loc.T("mycareer.found"), () => Shell.Show<CareerFoundPage>()));
            }
            NextEventCard(left, world, team);
            if (team != null) TeamCard(left, world, team);

            if (!string.IsNullOrEmpty(Shell.LastResult))
            {
                var last = Ui.Card(right, Loc.T("home.last"));
                UiFactory.Paragraph(last, FirstLines(Shell.LastResult, UiScale.Compact ? 2 : 4), Theme.FontSmall);
            }
            if (team != null) TodoCard(right, world, team);
            NewsCard(right, world);
        }

        private static string FirstLines(string text, int lines)
        {
            var parts = text.TrimEnd('\n').Split('\n');
            if (parts.Length <= lines) return string.Join("\n", parts);
            return string.Join("\n", parts, 0, lines) + "\n…";
        }

        private void NextEventCard(Transform parent, WorldState world, Team team)
        {
            var next = Shell.NextOwnEvent();
            if (next == null)
            {
                var over = Ui.Card(parent, Loc.T("home.seasonOver", world.Season), Loc.T("home.seasonOverInfo"));
                UiFactory.ButtonRow(over, 72, Game.IsSeasonOver ? UiFactory.Primary(Loc.T("shell.closeSeason"), Shell.CloseSeason) : UiFactory.Primary(Loc.T("shell.finishSeason"), Shell.SimulateSeason));
                return;
            }
            var circuit = Game.Db.GetCircuit(next.Round.CircuitId);
            var cats = new List<string>();
            foreach (var c in next.Categories) cats.Add(Game.Db.GetCategory(c).ShortName);
            bool racing = team != null && next.Categories.Contains(team.CategoryId);
            // Which round of the season this is for the team's championship ("Ronda 3/22").
            string roundTag = "";
            if (team != null && racing)
            {
                ChampionshipState champ;
                int done = world.Championships.TryGetValue(team.CategoryId, out champ) ? champ.Rounds.Count : 0;
                roundTag = Loc.T("home.round", done + 1, Game.RoundsInSeason(team.CategoryId)) + " · ";
            }
            var card = Ui.Card(parent, roundTag + Loc.Round(next.Round.Name),
                circuit.Name + " · " + next.Round.Date.ToString("d MMMM", Loc.Culture) + " · " + string.Join(", ", cats.ToArray()));
            if (racing)
                UiFactory.ButtonRow(card, 72,
                    UiFactory.Primary(Loc.T("shell.goRace"), Shell.Continue),
                    UiFactory.Act(Loc.T("home.simulateEvent"), Shell.SimulateEvent),
                    UiFactory.Act(Loc.T("home.simulateSeasonShort"), Shell.SimulateSeason));
            else
            {
                Ui.Note(card, Loc.T("home.notRacing"));
                UiFactory.ButtonRow(card, 72,
                    UiFactory.Primary(Loc.T("shell.simulate"), Shell.SimulateEvent),
                    UiFactory.Act(Loc.T("home.simulateSeasonShort"), Shell.SimulateSeason));
            }
        }

        private void TeamCard(Transform parent, WorldState world, Team team)
        {
            var card = Ui.Card(parent, Loc.T("home.team"));
            ChampionshipState champ;
            world.Championships.TryGetValue(team.CategoryId, out champ);
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r == null) continue;
                int pos = champ != null ? champ.RiderPosition(id) : 0;
                StandingEntry st = null;
                if (champ != null) champ.Riders.TryGetValue(id, out st);
                string detail = Loc.T("home.riderLine", st != null ? st.Points.ToString("0") : "0", st != null ? st.Wins : 0, st != null ? st.Podiums : 0);
                if (r.InjuryRoundsOut > 0) detail += "  ·  " + Theme.BadText(Loc.T("home.injured", r.InjuryRoundsOut));
                Ui.Item(card, "#" + r.Number + "  " + r.FullName, detail, pos > 0 ? "<size=" + (Theme.FontLarge - 6) + ">P" + pos + "</size>" : "-");
            }
            var pers = team.Personality;
            if (pers != null)
            {
                int teams = world.TeamsInCategory(team.CategoryId).Count;
                Ui.Line(card, Loc.T("objective.title"), Loc.T("objective." + pers.Objective) + " " + Theme.Dim(Loc.T("objective.target", TeamAI.TargetPosition(pers.Objective, teams))));
            }
        }

        /// <summary>Things that need a decision, most urgent first, each with a shortcut.</summary>
        private void TodoCard(Transform parent, WorldState world, Team team)
        {
            var todo = new List<KeyValuePair<string, Action>>();
            int emptyNow = ContractService.SeatsFreeNow(world, Game.Db, team);
            var ownOrg = ManagerCareer.OwnedOrganisation(world);
            var alert = OrganisationService.Alert(world, Game.Db, ownOrg);
            if (alert == CashAlert.Insolvent) todo.Add(Pair(Theme.BadText(Loc.T("home.todo.insolvent")), () => Shell.Show<CareerOrgPage>()));
            else if (alert == CashAlert.Low) todo.Add(Pair(Theme.WarnText(Loc.T("home.todo.lowCash")), () => Shell.Show<CareerOrgPage>()));
            if (emptyNow > 0) todo.Add(Pair(Theme.BadText(Loc.T("home.todo.emptySeats", emptyNow)), () => Shell.Show<MarketScreen>()));
            var job = world.PlayerManager != null ? world.PlayerManager.Job : null;
            if (job != null && job.Role == ManagerRole.TeamManager && job.EndSeason <= world.Season)
                todo.Add(Pair(Theme.WarnText(Loc.T("home.todo.myContract")), () => Shell.Show<CareerSummaryPage>()));
            int missingStaff = 0;
            foreach (StaffRole role in Enum.GetValues(typeof(StaffRole)))
            {
                if (role == StaffRole.TeamManager) continue;
                double best;
                int count;
                if (StaffService.Coverage(world, Game.Db, team, role, out best, out count) == RoleCoverage.Missing) missingStaff++;
            }
            if (missingStaff > 0) todo.Add(Pair(Loc.T("home.todo.missingStaff", missingStaff), () => Shell.Show<StaffPage>()));
            double left = team.SeasonBudget - Accounts.SpentThisSeason(world, team);
            if (left < 0) todo.Add(Pair(Theme.BadText(Loc.T("home.todo.budget")), () => Shell.Show<FinanceScreen>()));
            int bids = world.TransferOffers.FindAll(o => o.FromTeamId == team.Id).Count;
            if (bids > 0) todo.Add(Pair(Loc.T("home.todo.bids", bids), () => Shell.Show<RidersPage>()));
            var offers = SponsorMarket.OffersFor(world, team);
            if (offers.Count > 0) todo.Add(Pair(Loc.T("home.todo.sponsors", offers.Count), () => Shell.Show<SponsorsPage>()));
            int expiring = 0;
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r != null && r.Contract != null && r.Contract.EndSeason <= world.Season && r.FutureContract == null) expiring++;
            }
            if (expiring > 0) todo.Add(Pair(Loc.T("home.todo.contracts", expiring), () => Shell.Show<RidersPage>()));
            int seats = ContractService.SeatsFreeNextSeason(world, Game.Db, team);
            if (seats > 0) todo.Add(Pair(Loc.T("home.todo.seats", seats, world.Season + 1), () => Shell.Show<MarketScreen>()));
            int staffEnding = 0;
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (world.Staff.TryGetValue(id, out s) && s.ContractEndSeason <= world.Season) staffEnding++;
            }
            if (staffEnding > 0) todo.Add(Pair(Loc.T("home.todo.staff", staffEnding), () => Shell.Show<StaffPage>()));
            int running = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status == ProjectStatus.InProgress).Count;
            if (running < TeamCapabilities.ProjectCapacity(team) && DevelopmentService.AvailableProjects(world, Game.Db, team).Count > 0)
                todo.Add(Pair(Loc.T("home.todo.rnd", TeamCapabilities.ProjectCapacity(team) - running), () => Shell.Show<ProjectsPage>()));

            var card = Ui.Card(parent, Loc.T("home.todo"));
            for (int i = 0; i < todo.Count && i < MaxTodo; i++)
                Ui.Item(card, todo[i].Key, null, null, UiFactory.Primary(Loc.T("home.open"), todo[i].Value));
            if (todo.Count > MaxTodo) Ui.Note(card, Loc.T("home.todo.more", todo.Count - MaxTodo));
            if (todo.Count == 0) Ui.Note(card, Loc.T("home.todo.none"));
        }

        private static KeyValuePair<string, Action> Pair(string text, Action go)
        {
            return new KeyValuePair<string, Action>(text, go);
        }

        private void NewsCard(Transform parent, WorldState world)
        {
            var card = Ui.Card(parent, Loc.T("home.news"));
            // Only the team's own world (Grand Prix or Superbike) and general news.
            var team = world.PlayerTeam;
            var mine = team != null ? Game.Db.GetCategory(team.CategoryId) : null;
            int shown = 0;
            foreach (var n in world.News)
            {
                if (shown >= MaxNews) break;
                if (mine != null && n.CategoryId != null)
                {
                    var cat = Game.Db.GetCategory(n.CategoryId);
                    if (cat != null && cat.Pyramid != mine.Pyramid) continue;
                }
                UiFactory.Paragraph(card, Theme.Dim(n.Date.ToString("dd MMM", Loc.Culture)) + "   " + (n.Important ? "<b>" + n.Headline + "</b>" : n.Headline), Theme.FontSmall);
                shown++;
            }
            if (shown == 0) Ui.Note(card, Loc.T("home.noNews"));
        }
    }
}
