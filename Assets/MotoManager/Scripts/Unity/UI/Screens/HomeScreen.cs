using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// Home: what comes next and the one button to get there, the team at a glance, what needs the manager's
    /// attention (with a shortcut to each) and the latest news. Everything else lives in its own section.
    /// </summary>
    public class HubScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.home"); } }

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            var next = Game.PeekNextEvent();

            if (!string.IsNullOrEmpty(Shell.LastResult))
            {
                var last = Ui.Card(list, Loc.T("home.last"));
                UiFactory.Paragraph(last, Shell.LastResult, Theme.FontSmall + 2);
            }

            // Next event and the main action.
            if (next != null)
            {
                var circuit = Game.Db.GetCircuit(next.Round.CircuitId);
                var cats = new List<string>();
                foreach (var c in next.Categories) cats.Add(Game.Db.GetCategory(c).ShortName);
                bool racing = team != null && next.Categories.Contains(team.CategoryId);
                var card = Ui.Card(list, next.Round.Name,
                    circuit.Name + " · " + circuit.Country + " · " + next.Round.Date.ToString("d MMMM", Loc.Culture) + "  ·  " + string.Join(", ", cats.ToArray()));
                if (!racing) Ui.Note(card, Loc.T("home.notRacing"));
                if (racing)
                    UiFactory.ButtonRow(card, 76, UiFactory.Act(Loc.T("shell.goRace"), Shell.Play), UiFactory.Act(Loc.T("home.simulateEvent"), Shell.SimulateEvent));
                else
                    UiFactory.ButtonRow(card, 76, UiFactory.Act(Loc.T("shell.simulate"), Shell.SimulateEvent), UiFactory.Act(Loc.T("home.simulateSeason"), Shell.SimulateSeason));
                if (racing)
                    Ui.Expander(card, "home.more", Loc.T("home.more"), c => UiFactory.ButtonRow(c, 60, UiFactory.Act(Loc.T("home.simulateSeason"), Shell.SimulateSeason)), Shell.Refresh);
            }
            else
            {
                var card = Ui.Card(list, Loc.T("home.seasonOver", world.Season), Loc.T("home.seasonOverInfo"));
                UiFactory.ButtonRow(card, 76, UiFactory.Act(Loc.T("shell.closeSeason"), Shell.CloseSeason));
            }

            RectTransform left, right;
            Ui.Columns(list, out left, out right);
            if (team != null)
            {
                TeamCard(left, world, team);
                TodoCard(right, world, team);
            }
            NewsCard(list, world);
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

        /// <summary>Things that need a decision, each with a shortcut.</summary>
        private void TodoCard(Transform parent, WorldState world, Team team)
        {
            var card = Ui.Card(parent, Loc.T("home.todo"));
            int items = 0;
            var go = Loc.T("home.open");

            var offers = SponsorMarket.OffersFor(world, team);
            if (offers.Count > 0)
            {
                items++;
                Ui.Item(card, Loc.T("home.todo.sponsors", offers.Count), Loc.T("home.todo.sponsorsInfo"), null, UiFactory.Act(go, () => Shell.Show<SponsorsPage>()));
            }
            int bids = world.TransferOffers.FindAll(o => o.FromTeamId == team.Id).Count;
            if (bids > 0)
            {
                items++;
                Ui.Item(card, Loc.T("home.todo.bids", bids), Loc.T("home.todo.bidsInfo"), null, UiFactory.Act(go, () => Shell.Show<RidersPage>()));
            }
            var expiring = new List<string>();
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r != null && r.Contract != null && r.Contract.EndSeason <= world.Season && r.FutureContract == null) expiring.Add(r.ShortName);
            }
            if (expiring.Count > 0)
            {
                items++;
                Ui.Item(card, Loc.T("home.todo.contracts", expiring.Count), string.Join(", ", expiring.ToArray()), null, UiFactory.Act(go, () => Shell.Show<RidersPage>()));
            }
            int seats = ContractService.SeatsFreeNextSeason(world, Game.Db, team);
            if (seats > 0)
            {
                items++;
                Ui.Item(card, Loc.T("home.todo.seats", seats, world.Season + 1), Loc.T("home.todo.seatsInfo"), null, UiFactory.Act(go, () => Shell.Show<MarketScreen>()));
            }
            int staffEnding = 0;
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (world.Staff.TryGetValue(id, out s) && s.ContractEndSeason <= world.Season) staffEnding++;
            }
            if (staffEnding > 0)
            {
                items++;
                Ui.Item(card, Loc.T("home.todo.staff", staffEnding), Loc.T("home.todo.staffInfo"), null, UiFactory.Act(go, () => Shell.Show<StaffPage>()));
            }
            int running = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status == ProjectStatus.InProgress).Count;
            if (running < TeamCapabilities.ProjectCapacity(team) && DevelopmentService.AvailableProjects(world, Game.Db, team).Count > 0)
            {
                items++;
                Ui.Item(card, Loc.T("home.todo.rnd", TeamCapabilities.ProjectCapacity(team) - running), Loc.T("home.todo.rndInfo"), null, UiFactory.Act(go, () => Shell.Show<ProjectsPage>()));
            }
            double left = team.SeasonBudget - Accounts.SpentThisSeason(world, team);
            if (left < 0)
            {
                items++;
                Ui.Item(card, Theme.BadText(Loc.T("home.todo.budget")), Loc.T("home.todo.budgetInfo", Core.Common.MathX.FormatMoney(-left)), null, UiFactory.Act(go, () => Shell.Show<FinanceScreen>()));
            }
            if (items == 0) Ui.Note(card, Loc.T("home.todo.none"));
        }

        private void NewsCard(Transform list, WorldState world)
        {
            var card = Ui.Card(list, Loc.T("home.news"));
            int shown = 0;
            foreach (var n in world.News)
            {
                if (shown++ >= 6) break;
                NewsLine(card, n);
            }
            if (world.News.Count == 0) Ui.Note(card, Loc.T("home.noNews"));
            if (world.News.Count > 6)
                Ui.Expander(card, "home.allNews", Loc.T("home.moreNews"), c =>
                {
                    for (int i = 6; i < world.News.Count && i < 40; i++) NewsLine(c, world.News[i]);
                }, Shell.Refresh);
        }

        private static void NewsLine(Transform parent, NewsItem n)
        {
            UiFactory.Paragraph(parent, Theme.Dim(n.Date.ToString("dd MMM", Loc.Culture)) + "   " + (n.Important ? "<b>" + n.Headline + "</b>" : n.Headline), Theme.FontSmall);
        }
    }
}
