using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using MotoManager.Core.Data;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// Home (restructure, user spec): top left the team (name, category, DETALLES to the team page), top right the
    /// decisions waiting and the news; bottom left the next race (round of the season, country, the circuit's real
    /// line; tapping it opens the calendar) with the two simulations, bottom right a mini standings table (tapping it
    /// opens the standings). Racing is the header's main action.
    /// </summary>
    public class HubScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.home"); } }

        /// <summary>Something that needs a decision: its text and where to resolve it.</summary>
        private struct Todo
        {
            public string Text;
            public Action Go;
            public string Tile;
        }

        private const string TileRiders = "riders", TileStaff = "staff", TileBike = "bike", TileSponsors = "sponsors", TileFinance = "finance";
        private const int MaxTodos = 3, StandingRows = 6;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null)
            {
                Shell.InboxSource = null;
                BuildWithoutTeam(content, world);
                return;
            }
            var todos = Todos(world, team);
            Shell.InboxSource = Inbox;

            var cols = Ui.Columns12(content, 7, 5);
            var left = Ui.Rows(cols[0], 0.36f, 0.64f);
            var right = Ui.Rows(cols[1], 0.36f, 0.64f);
            TeamTile(left[0], world, team);
            NoticesTile(right[0], todos);
            RaceTile(left[1], world, team);
            StandingsTile(right[1], world, team);
        }

        private List<KeyValuePair<string, Action>> Inbox()
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return null;
            var items = new List<KeyValuePair<string, Action>>();
            foreach (var t in Todos(world, team)) items.Add(new KeyValuePair<string, Action>(t.Text, t.Go));
            return items;
        }

        // ------------------------------------------------------------------ tiles

        /// <summary>The team: a team-colour edge, the name in the display face, category and bike, and DETALLES.</summary>
        private void TeamTile(RectTransform area, WorldState world, Team team)
        {
            var body = Ui.Tile(area, null, () => Shell.Show<TeamDetailPage>(), null, 0, null, null, false, Theme.Accent);
            var name = Ui.Label(body, Theme.Caps(team.Name), Theme.TextKey, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Place(name.rectTransform, 0, 0.5f, 0.68f, 1);
            Ui.Fit(name, Theme.TextKey);
            var cat = Game.Db.GetCategory(team.CategoryId);
            Manufacturer maker;
            Game.Db.Manufacturers.TryGetValue(team.ManufacturerId ?? "", out maker);
            string line = (cat != null ? cat.ShortName : "") + (maker != null ? "  ·  " + maker.Name : "");
            var me = world.PlayerManager;
            if (me != null) line += "\n" + Loc.T("ux.home2.manager", me.FullName);
            var sub = Ui.Label(body, line, Theme.TextCaption, TextAnchor.UpperLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(sub.rectTransform, 0, 0.14f, 0.68f, 0.5f);
            Ui.Fit(sub, Theme.TextCaption);
            var btn = UiFactory.Rect("Details", body);
            UiFactory.Place(btn, 0.7f, 0.22f, 1, 0.78f);
            Ui.Secondary(btn, Loc.T("ux.home2.details"), () => Shell.Show<TeamDetailPage>());
        }

        /// <summary>The decisions waiting (the most urgent three, each a shortcut) and the news button.</summary>
        private void NoticesTile(RectTransform area, List<Todo> todos)
        {
            var body = Ui.Tile(area, Loc.T("ux.inbox.title"), todos.Count > MaxTodos ? Shell.ShowInbox : (Action)null,
                todos.Count > 0 ? todos.Count.ToString() : null, 0);
            var list = UiFactory.Rect("List", body);
            UiFactory.Place(list, 0, 0, 0.74f, 1);
            var v = list.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.spacing = 2;
            if (todos.Count == 0)
            {
                var ok = Ui.Label(list, Loc.T("hub.allClear"), Theme.TextBody, TextAnchor.MiddleLeft, Theme.Good, FontStyle.Bold);
                ok.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
            }
            for (int i = 0; i < todos.Count && i < MaxTodos; i++)
            {
                var row = Ui.ListRow(list, new[] { 1f }, new[] { todos[i].Text }, i, false, todos[i].Go, 44);
                foreach (var t in row.GetComponentsInChildren<Text>()) Ui.Fit(t, Theme.TextCaption);
            }
            var news = UiFactory.Rect("News", body);
            UiFactory.Place(news, 0.77f, 0.3f, 1, 0.7f);
            Ui.Ghost(news, Loc.T("ux.shell.newsShort"), Shell.ShowNews);
        }

        /// <summary>
        /// The next race: round of the season in the header, the circuit's line on the left, the country, Grand Prix
        /// and date on the right, the last result at the bottom. The tile opens the calendar; simulating the race is offered once CARRERA is pressed.
        /// </summary>
        private void RaceTile(RectTransform area, WorldState world, Team team)
        {
            var next = Shell.NextOwnEvent();
            if (next == null)
            {
                var over = Ui.Tile(area, Loc.T("home.seasonOver", world.Season), () => Shell.Show<CalendarScreen>(), null, 0, null,
                    Wp2Ui.Info(Loc.T("home.seasonOver", world.Season), Loc.T("home.seasonOverInfo")));
                var t = Ui.Label(over, Theme.Caps(Loc.T("home.seasonOverShort")), Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
                UiFactory.Place(t.rectTransform, 0, 0.6f, 1, 1);
                return;
            }
            ChampionshipState champ;
            int done = world.Championships.TryGetValue(team.CategoryId, out champ) ? champ.Rounds.Count : 0;
            string right = Theme.Caps(Loc.T("home.round", done + 1, Game.RoundsInSeason(team.CategoryId)));
            var body = Ui.Tile(area, Loc.T("ux.hub.nextRace"), () => Shell.Show<CalendarScreen>(), right);
            var circuit = Game.Db.GetCircuit(next.Round.CircuitId);

            var map = UiFactory.Rect("Map", body);
            UiFactory.Place(map, 0, 0.12f, 0.46f, 1);
            map.offsetMax = new Vector2(-12, -4);
            CircuitOutline.Draw(map, circuit, Theme.Text, 5);

            var info = UiFactory.Rect("Info", body);
            UiFactory.Place(info, 0.48f, 0.12f, 1, 1);
            if (circuit != null && !string.IsNullOrEmpty(circuit.Country))
            {
                var chip = Ui.Pill(info, circuit.Country.ToUpperInvariant(), Theme.PanelLight, Theme.Text);
                chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = new Vector2(0, 1);
                chip.rectTransform.pivot = new Vector2(0, 1);
                chip.rectTransform.sizeDelta = new Vector2(56, 32);
                chip.rectTransform.anchoredPosition = new Vector2(0, -6);
            }
            var gp = Ui.Label(info, Theme.Caps(Loc.Round(next.Round.Name)), Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Place(gp.rectTransform, 0, 0.42f, 1, 0.8f);
            Ui.Fit(gp, Theme.TextTitle);
            string detail = (circuit != null ? circuit.ShortName + "  ·  " + (circuit.LengthM / 1000.0).ToString("0.000", Loc.Culture) + " km\n" : "") +
                            Theme.Caps(next.Round.Date.ToString("d MMMM", Loc.Culture));
            var d = Ui.Label(info, detail, Theme.TextCaption, TextAnchor.UpperLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(d.rectTransform, 0, 0, 1, 0.42f);
            if (!string.IsNullOrEmpty(Shell.LastResult))
            {
                var last = Ui.Label(body, Theme.Dim(Loc.T("home.last") + "  ") + FirstLine(Shell.LastResult), Theme.TextMin,
                    TextAnchor.MiddleLeft, Theme.Text, FontStyle.Normal);
                UiFactory.Place(last.rectTransform, 0, 0, 1, 0.12f);
                Ui.Fit(last, Theme.TextMin);
            }

        }

        /// <summary>
        /// The riders' championship, top rows plus the team's riders when they are further down. The tile opens the
        /// standings.
        /// </summary>
        private void StandingsTile(RectTransform area, WorldState world, Team team)
        {
            var cat = Game.Db.GetCategory(team.CategoryId);
            var body = Ui.Tile(area, Loc.T("ux.home2.standings"), () => Shell.Show<StandingsScreen>(), cat != null ? cat.ShortName : null);
            ChampionshipState champ;
            if (!world.Championships.TryGetValue(team.CategoryId, out champ))
            {
                Ui.Empty(body, null, Loc.T("ux.home2.noStandings"));
                return;
            }
            var table = champ.SortedRiders();
            var shown = new List<int>();
            for (int i = 0; i < table.Count && shown.Count < StandingRows; i++) shown.Add(i);
            // The team's riders replace the last rows when they are further down.
            for (int i = StandingRows; i < table.Count; i++)
                if (team.RiderIds.Contains(table[i].Id) && shown.Count > 0)
                {
                    int slot = shown.Count - 1;
                    while (slot > 0 && team.RiderIds.Contains(table[shown[slot]].Id)) slot--;
                    shown[slot] = i;
                    shown.Sort();
                }
            var list = UiFactory.Rect("Table", body);
            UiFactory.Stretch(list);
            var v = list.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            float h = Mathf.Floor(Mathf.Max(40, (area.rect.height > 0 ? area.rect.height - Ui.Grid.Header - 32 : 300) / StandingRows));
            for (int k = 0; k < shown.Count; k++)
            {
                var e = table[shown[k]];
                var r = world.GetRider(e.Id);
                bool mine = team.RiderIds.Contains(e.Id);
                Ui.ListRow(list, new[] { 0.14f, 0.62f, 0.24f },
                    new[] { Theme.Pos(shown[k] + 1), r != null ? Theme.Caps(r.LastName) : e.Id, "<b>" + e.Points.ToString("0") + "</b>" },
                    k, mine, null, h);
            }
        }

        // ------------------------------------------------------------------ decisions

        private static int Count(List<Todo> todos, string tile)
        {
            int n = 0;
            foreach (var t in todos) if (t.Tile == tile) n++;
            return n;
        }

        private int MissingStaff(WorldState world, Team team)
        {
            int missing = 0;
            foreach (StaffRole role in Enum.GetValues(typeof(StaffRole)))
            {
                if (role == StaffRole.TeamManager) continue;
                double best;
                int count;
                if (StaffService.Coverage(world, Game.Db, team, role, out best, out count) == RoleCoverage.Missing) missing++;
            }
            return missing;
        }

        /// <summary>Things that need a decision, most urgent first, each with a shortcut and its hub tile.</summary>
        private List<Todo> Todos(WorldState world, Team team)
        {
            var todo = new List<Todo>();
            int emptyNow = ContractService.SeatsFreeNow(world, Game.Db, team);
            var ownOrg = ManagerCareer.OwnedOrganisation(world);
            var alert = OrganisationService.Alert(world, Game.Db, ownOrg);
            if (alert == CashAlert.Insolvent) todo.Add(Item(Theme.BadText(Loc.T("home.todo.insolvent")), () => Shell.Show<CareerOrgPage>(), TileFinance));
            else if (alert == CashAlert.Low) todo.Add(Item(Theme.WarnText(Loc.T("home.todo.lowCash")), () => Shell.Show<CareerOrgPage>(), TileFinance));
            if (emptyNow > 0) todo.Add(Item(Theme.BadText(Loc.T("home.todo.emptySeats", emptyNow)), () => Shell.Show<MarketScreen>(), TileRiders));
            var job = world.PlayerManager != null ? world.PlayerManager.Job : null;
            if (job != null && job.Role == ManagerRole.TeamManager && job.EndSeason <= world.Season)
                todo.Add(Item(Theme.WarnText(Loc.T("home.todo.myContract")), () => Shell.Show<CareerSummaryPage>(), null));
            int missingStaff = MissingStaff(world, team);
            if (missingStaff > 0) todo.Add(Item(Loc.T("home.todo.missingStaff", missingStaff), () => Shell.Show<StaffHubPage>(), TileStaff));
            double left = team.SeasonBudget - Accounts.SpentThisSeason(world, team);
            if (left < 0) todo.Add(Item(Theme.BadText(Loc.T("home.todo.budget")), () => Shell.Show<FinanceScreen>(), TileFinance));
            int bids = world.TransferOffers.FindAll(o => o.FromTeamId == team.Id).Count;
            if (bids > 0) todo.Add(Item(Loc.T("home.todo.bids", bids), () => Shell.Show<RidersHubPage>(), TileRiders));
            var offers = SponsorMarket.OffersFor(world, team);
            if (offers.Count > 0) todo.Add(Item(Loc.T("home.todo.sponsors", offers.Count), () => Shell.Show<SponsorsPage>(), TileSponsors));
            int expiring = 0;
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r != null && r.Contract != null && r.Contract.EndSeason <= world.Season && r.FutureContract == null) expiring++;
            }
            if (expiring > 0) todo.Add(Item(Loc.T("home.todo.contracts", expiring), () => Shell.Show<RidersHubPage>(), TileRiders));
            int seats = ContractService.SeatsFreeNextSeason(world, Game.Db, team);
            if (seats > 0) todo.Add(Item(Loc.T("home.todo.seats", seats, world.Season + 1), () => Shell.Show<MarketScreen>(), TileRiders));
            int staffEnding = 0;
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (world.Staff.TryGetValue(id, out s) && s.ContractEndSeason <= world.Season) staffEnding++;
            }
            if (staffEnding > 0) todo.Add(Item(Loc.T("home.todo.staff", staffEnding), () => Shell.Show<StaffHubPage>(), TileStaff));
            int running = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status == ProjectStatus.InProgress).Count;
            if (running < TeamCapabilities.ProjectCapacity(team) && DevelopmentService.AvailableProjects(world, Game.Db, team).Count > 0)
                todo.Add(Item(Loc.T("home.todo.rnd", TeamCapabilities.ProjectCapacity(team) - running), () => Shell.Show<ProjectsPage>(), TileBike));
            return todo;
        }

        private static Todo Item(string text, Action go, string tile)
        {
            return new Todo { Text = text, Go = go, Tile = tile };
        }

        private static string FirstLine(string text)
        {
            var parts = text.TrimEnd('\n').Split('\n');
            return parts.Length > 0 ? parts[0] : text;
        }

        // ------------------------------------------------------------------ without a team

        /// <summary>
        /// Between jobs: on the left the job search (see offers = the primary, found a team), on the right the next
        /// event with the two simulations and the last result. The news are in the top bar.
        /// </summary>
        private void BuildWithoutTeam(RectTransform content, WorldState world)
        {
            var cols = Ui.Columns12(content, 6, 6);
            var none = Ui.Tile(cols[0], Loc.T("home.noTeam"), null, null, 0, null, Wp2Ui.Info(Loc.T("home.noTeam"), Loc.T("home.noTeamInfo")));
            var icon = UiIcons.Icon(none, UiIcons.Career, Theme.Alpha(Theme.TextDim, 0.4f));
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.62f);
            icon.rectTransform.sizeDelta = new Vector2(120, 120);
            var line = Ui.Label(none, Theme.Caps(Loc.T("ux.hub.noTeamLine")), Theme.TextHeader, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(line.rectTransform, 0, 0.26f, 1, 0.42f);
            var buttons = Wp2Ui.BottomStrip(none, Ui.Grid.Cta);
            var bc = Ui.Columns12(buttons, 5, 7);
            Ui.Secondary(bc[0], Loc.T("mycareer.found"), () => Shell.Show<CareerFoundPage>());
            Wp2Ui.Chevron(Ui.PrimaryCTA(bc[1], Loc.T("mycareer.seeOffers"), () => Shell.Show<CareerOffersPage>()));

            var next = Shell.NextOwnEvent();
            RectTransform body;
            if (next == null)
            {
                body = Ui.Tile(cols[1], Loc.T("home.seasonOver", world.Season), null, null, 0, null,
                    Wp2Ui.Info(Loc.T("home.seasonOver", world.Season), Loc.T("home.seasonOverInfo")));
                var t = Ui.Label(body, Theme.Caps(Loc.T("home.seasonOverShort")), Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
                UiFactory.Place(t.rectTransform, 0, 0.62f, 1, 1);
            }
            else
            {
                var cats = new List<string>();
                foreach (var c in next.Categories) cats.Add(Game.Db.GetCategory(c).ShortName);
                body = Ui.Tile(cols[1], Loc.T("home.next"), null, Theme.Caps(next.Round.Date.ToString("d MMM", Loc.Culture)) + " · " + string.Join(", ", cats.ToArray()));
                var t = Ui.Label(body, Theme.Caps(Loc.Round(next.Round.Name)), Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
                UiFactory.Place(t.rectTransform, 0, 0.62f, 1, 1);
            }
            if (!string.IsNullOrEmpty(Shell.LastResult))
            {
                var last = Ui.Label(body, Theme.Dim(Loc.T("home.last") + "  ") + FirstLine(Shell.LastResult), Theme.TextCaption,
                    TextAnchor.MiddleLeft, Theme.Text, FontStyle.Normal);
                UiFactory.Place(last.rectTransform, 0, 0.44f, 1, 0.62f);
            }
            var row = Wp2Ui.BottomStrip(body, Ui.Grid.Button);
            if (next == null)
            {
                var c = Ui.Columns12(row, 12);
                if (Game.IsSeasonOver) Ui.Secondary(c[0], Loc.T("shell.closeSeason"), Shell.CloseSeason);
                else Ui.Secondary(c[0], Loc.T("shell.finishSeason"), Shell.SimulateSeason);
                return;
            }
            var sc = Ui.Columns12(row, 6, 6);
            Ui.Secondary(sc[0], Loc.T("shell.simulate"), Shell.SimulateEvent);
            Ui.Secondary(sc[1], Loc.T("home.simulateSeasonShort"), Shell.SimulateSeason);
        }
    }
}
