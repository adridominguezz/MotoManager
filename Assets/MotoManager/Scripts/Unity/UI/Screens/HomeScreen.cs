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
        private static int MaxTodo { get { return UiScale.Compact ? 2 : 4; } }
        private static int MaxNews { get { return UiScale.Compact ? 2 : 4; } }

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            RectTransform left, right;
            Ui.Columns(list, out left, out right);

            // Left: what comes next (the main action) and the last result.
            if (team == null)
            {
                var none = Ui.Card(left, Loc.T("home.noTeam"), Loc.T("home.noTeamInfo"));
                UiFactory.ButtonRow(none, 64,
                    UiFactory.Primary(Loc.T("mycareer.seeOffers"), () => Shell.Show<CareerOffersPage>()),
                    UiFactory.Act(Loc.T("mycareer.found"), () => Shell.Show<CareerFoundPage>()));
            }
            NextEventCard(left, world, team);
            if (!string.IsNullOrEmpty(Shell.LastResult))
            {
                var last = Ui.Card(left, Loc.T("home.last"));
                UiFactory.Paragraph(last, FirstLines(Shell.LastResult, 2), Theme.FontSmall);
            }
            Headline(left, world);

            // Right: the team at a glance (tiles that open their section) and what needs a decision.
            if (team != null)
            {
                Tiles(right, world, team);
                TodoCard(right, world, team);
            }
            else NewsCard(right, world);
        }

        /// <summary>Four big tiles: team championship, cash, riders and to-dos; each opens its screen.</summary>
        private void Tiles(Transform parent, WorldState world, Team team)
        {
            ChampionshipState champ;
            world.Championships.TryGetValue(team.CategoryId, out champ);
            int teamPos = 0;
            double teamPts = 0;
            if (champ != null)
            {
                var table = champ.SortedTeams();
                for (int i = 0; i < table.Count; i++)
                    if (table[i].Id == team.Id) { teamPos = i + 1; teamPts = table[i].Points; }
            }
            var positions = new List<string>();
            foreach (var id in team.RiderIds)
            {
                int pos = champ != null ? champ.RiderPosition(id) : 0;
                var r = world.GetRider(id);
                positions.Add((pos > 0 ? "P" + pos : "-") + (r != null && r.InjuryRoundsOut > 0 ? "<color=" + Theme.BadHex + ">+</color>" : ""));
            }
            var pers = team.Personality;
            int teams = world.TeamsInCategory(team.CategoryId).Count;
            string target = pers != null ? Loc.T("home.tile.target", TeamAI.TargetPosition(pers.Objective, teams)) : "";
            var org = world.OrganisationOf(team);
            double cash = org != null ? org.Cash : Accounts.Cash(world, team);

            var row1 = UiFactory.Row(parent, 132);
            Tile(row1, 0, 0.5f, Loc.T("home.tile.championship"), teamPos > 0 ? "P" + teamPos : "-", Loc.T("home.tile.points", teamPts.ToString("0")) + "  " + Theme.Dim(target), () => Shell.Show<StandingsScreen>());
            Tile(row1, 0.5f, 1, Loc.T("home.tile.cash"), Theme.Money(cash), Loc.T("home.tile.cashSub"), () => Shell.Show<FinanceScreen>());
            var row2 = UiFactory.Row(parent, 132);
            Tile(row2, 0, 0.5f, Loc.T("home.tile.riders"), positions.Count > 0 ? string.Join("  ", positions.ToArray()) : "-", Loc.T("home.tile.ridersSub"), () => Shell.Show<RidersPage>());
            var todo = Todos(world, team);
            Tile(row2, 0.5f, 1, Loc.T("home.todo"), todo.Count.ToString(), todo.Count > 0 ? Loc.T("home.tile.todoSub") : Loc.T("home.todo.none"), todo.Count > 0 ? todo[0].Value : (Action)(() => Shell.Show<RidersPage>()));
        }

        private static void Tile(RectTransform row, float x0, float x1, string caption, string value, string sub, Action onTap)
        {
            var b = UiFactory.Button("Tile", row, "", onTap, Color.Lerp(Theme.Card, Theme.PanelLight, 0.35f), Theme.FontSmall);
            UiFactory.Place((RectTransform)b.transform, x0, 0, x1, 1, 5);
            UiFactory.Chamfer(b.image, 1.6f);
            var accent = UiFactory.Decor("Accent", b.transform, Theme.Accent);
            UiFactory.Slant(accent, 4f);
            accent.rectTransform.anchorMin = new Vector2(0, 1);
            accent.rectTransform.anchorMax = new Vector2(0.35f, 1);
            accent.rectTransform.offsetMin = new Vector2(10, -5);
            accent.rectTransform.offsetMax = new Vector2(0, -1);
            var cap = UiFactory.Label("Caption", b.transform, Theme.Caps(caption), Theme.FontSmall - 4, TextAnchor.UpperLeft, Theme.TextDim);
            cap.fontStyle = FontStyle.Bold;
            UiFactory.Place(cap.rectTransform, 0.06f, 0.66f, 0.96f, 0.92f);
            var v = UiFactory.Label("Value", b.transform, value, Theme.FontLarge, TextAnchor.MiddleLeft, Theme.Text);
            v.supportRichText = true;
            v.fontStyle = FontStyle.BoldAndItalic;
            v.resizeTextForBestFit = true;
            v.resizeTextMinSize = Theme.FontSmall;
            v.resizeTextMaxSize = Theme.FontLarge;
            UiFactory.Place(v.rectTransform, 0.06f, 0.28f, 0.96f, 0.7f);
            var s = UiFactory.Label("Sub", b.transform, sub, Theme.FontSmall - 4, TextAnchor.LowerLeft, Theme.TextDim);
            s.supportRichText = true;
            s.resizeTextForBestFit = true;
            s.resizeTextMinSize = Theme.FontSmall - 10;
            s.resizeTextMaxSize = Theme.FontSmall - 4;
            UiFactory.Place(s.rectTransform, 0.06f, 0.06f, 0.96f, 0.3f);
        }

        /// <summary>One line: the latest headline of the team's world (the full list is in Competition › News).</summary>
        private void Headline(Transform parent, WorldState world)
        {
            var team = world.PlayerTeam;
            var mine = team != null ? Game.Db.GetCategory(team.CategoryId) : null;
            foreach (var n in world.News)
            {
                if (mine != null && n.CategoryId != null)
                {
                    var cat = Game.Db.GetCategory(n.CategoryId);
                    if (cat != null && cat.Pyramid != mine.Pyramid) continue;
                }
                var card = Ui.Card(parent, Loc.T("home.news"));
                UiFactory.Paragraph(card, Theme.Dim(n.Date.ToString("dd MMM", Loc.Culture)) + "   " + "<b>" + n.Headline + "</b>", Theme.FontSmall);
                return;
            }
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
            // Hero card: the round, the event in big type and one obvious action; the shortcuts underneath.
            var card = Ui.Card(parent, roundTag.TrimEnd(' ', '·').Length > 0 ? roundTag.TrimEnd(' ', '·') : Loc.T("home.next"),
                next.Round.Date.ToString("d MMMM", Loc.Culture) + " · " + string.Join(", ", cats.ToArray()));
            var big = UiFactory.Label("Event", UiFactory.Row(card, 64), "<b><i>" + Loc.Round(next.Round.Name).ToUpperInvariant() + "</i></b>", Theme.FontLarge, TextAnchor.MiddleLeft, Theme.Text);
            big.supportRichText = true;
            big.resizeTextForBestFit = true;
            big.resizeTextMinSize = Theme.FontNormal - 4;
            big.resizeTextMaxSize = Theme.FontLarge;
            UiFactory.Stretch(big.rectTransform);
            Ui.Line(card, Loc.T("home.circuit"), circuit.Name);
            if (racing)
            {
                UiFactory.ButtonRow(card, 76, UiFactory.Primary(Loc.T("shell.goRace"), Shell.Continue));
                UiFactory.ButtonRow(card, 54,
                    UiFactory.Act(Loc.T("home.simulateEvent"), Shell.SimulateEvent),
                    UiFactory.Act(Loc.T("home.simulateSeasonShort"), Shell.SimulateSeason));
            }
            else
            {
                Ui.Note(card, Loc.T("home.notRacing"));
                UiFactory.ButtonRow(card, 72,
                    UiFactory.Primary(Loc.T("shell.simulate"), Shell.SimulateEvent),
                    UiFactory.Act(Loc.T("home.simulateSeasonShort"), Shell.SimulateSeason));
            }
        }

        /// <summary>Things that need a decision, most urgent first, each with a shortcut.</summary>
        private List<KeyValuePair<string, Action>> Todos(WorldState world, Team team)
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
            return todo;
        }

        private void TodoCard(Transform parent, WorldState world, Team team)
        {
            var todo = Todos(world, team);
            if (todo.Count == 0) return;
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
