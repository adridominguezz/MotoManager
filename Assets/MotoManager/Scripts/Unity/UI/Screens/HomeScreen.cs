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
    /// Home (UX spec §3.2, revised with the user): four tiles on one screen, no scrolling. On the left the riders as
    /// the hero tile (number, rating, name, last race, championship) and the next race under it (race = the screen's
    /// only primary, simulate GP and simulate season beside it); on the right staff and bike with big pictograms.
    /// Sponsors and finance are left to their sections (cash is in the top bar). Tiles badge the decisions waiting; the
    /// decisions themselves are listed in the top bar's inbox (<see cref="ManagementShell.InboxSource"/>) and the news
    /// in the top bar's news sheet.
    /// </summary>
    public class HubScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.home"); } }

        /// <summary>Something that needs a decision: its text, where to resolve it and the hub tile it belongs to.</summary>
        private struct Todo
        {
            public string Text;
            public Action Go;
            public string Tile;
        }

        private const string TileRiders = "riders", TileStaff = "staff", TileBike = "bike", TileSponsors = "sponsors", TileFinance = "finance";

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
            // The inbox reads the decisions fresh each time it is drawn (the list changes as the player acts).
            Shell.InboxSource = Inbox;

            // Left (8 columns): the riders as the hero tile, the next race under it. Right (4): staff and bike.
            var cols = Ui.Columns12(content, 8, 4);
            var left = Ui.Rows(cols[0], 0.62f, 0.38f);
            var right = Ui.Rows(cols[1], 1, 1);
            RidersTile(left[0], world, team, Count(todos, TileRiders));
            RaceTile(left[1], world, team);
            StaffTile(right[0], world, team, Count(todos, TileStaff));
            BikeTile(right[1], world, team, Count(todos, TileBike));
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

        /// <summary>
        /// The hero tile: the riders side by side (at most three), each with the race number, the rating, the name,
        /// the last race and the championship place with points (or the injury). A free seat opens the market.
        /// </summary>
        private void RidersTile(RectTransform area, WorldState world, Team team, int badge)
        {
            var body = Ui.Tile(area, Loc.T("hub.riders"), () => Shell.Show<RidersPage>(), null, badge, Theme.Bad);
            ChampionshipState champ;
            world.Championships.TryGetValue(team.CategoryId, out champ);
            int free = Math.Max(0, ContractService.SeatsFreeNow(world, Game.Db, team));
            int seats = Math.Min(3, team.RiderIds.Count + free);
            if (seats == 0) return;
            var spans = new int[seats];
            for (int i = 0; i < seats; i++) spans[i] = 12 / seats;
            var cols = Ui.Columns12(body, spans);
            for (int i = 0; i < seats; i++)
            {
                if (i > 0)
                {
                    var sep = UiFactory.Decor("Sep", cols[i], new Color(1, 1, 1, 0.08f));
                    sep.rectTransform.anchorMin = new Vector2(0, 0.04f);
                    sep.rectTransform.anchorMax = new Vector2(0, 0.96f);
                    sep.rectTransform.offsetMin = new Vector2(-7, 0);
                    sep.rectTransform.offsetMax = new Vector2(-6, 0);
                }
                var r = i < team.RiderIds.Count ? world.GetRider(team.RiderIds[i]) : null;
                if (r == null) FreeSeat(cols[i]);
                else RiderPanel(cols[i], champ, r, seats);
            }
        }

        private void FreeSeat(RectTransform area)
        {
            var hit = UiFactory.Panel("FreeSeat", area, Theme.Alpha(Theme.Warning, 0.08f));
            UiFactory.Stretch(hit.rectTransform);
            UiFactory.SideStripe(hit.transform, Theme.Warning, 6, 0);
            var b = hit.gameObject.AddComponent<Button>();
            b.targetGraphic = hit;
            b.onClick.AddListener(() =>
            {
                GameAudio.ClickSound();
                Shell.Show<MarketScreen>();
            });
            var icon = UiIcons.Icon(hit.transform, UiIcons.Person, Theme.Alpha(Theme.Warning, 0.5f));
            icon.preserveAspect = true;
            UiFactory.Place(icon.rectTransform, 0.2f, 0.42f, 0.8f, 0.92f);
            var t = Ui.Label(hit.transform, Theme.Caps(Loc.T("hub.freeSeat")), Theme.TextTitle, TextAnchor.MiddleCenter, Theme.Warning, FontStyle.BoldAndItalic);
            UiFactory.Place(t.rectTransform, 0, 0.2f, 1, 0.4f);
            var go = Ui.Label(hit.transform, Theme.Caps(Loc.T("ux.riders.toMarket")), Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(go.rectTransform, 0, 0.04f, 1, 0.2f);
        }

        /// <summary>One rider: number and rating on top, the name, then two figures (last race, championship).</summary>
        private static void RiderPanel(RectTransform area, ChampionshipState champ, Rider r, int seats)
        {
            float side = seats <= 2 ? 96 : 76;
            var plate = UiFactory.Decor("Number", area, new Color(0, 0, 0, 0.45f));
            UiFactory.Slant(plate, 2.4f);
            plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = new Vector2(0, 1);
            plate.rectTransform.pivot = new Vector2(0, 1);
            plate.rectTransform.anchoredPosition = new Vector2(0, -4);
            plate.rectTransform.sizeDelta = new Vector2(seats <= 2 ? 104 : 84, 56);
            var num = Ui.Label(plate.transform, r.Number > 0 ? "#" + r.Number : "-", Theme.TextTitle, TextAnchor.MiddleCenter, Color.white, FontStyle.BoldAndItalic);
            UiFactory.Stretch(num.rectTransform);

            var badge = Ui.RatingBadge(area, r.Attributes.Overall, Loc.T("rider.ratingTag"), side);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1, 1);
            badge.rectTransform.pivot = new Vector2(1, 1);
            badge.rectTransform.anchoredPosition = new Vector2(-4, 0);

            var first = Ui.Label(area, r.FirstName ?? "", Theme.TextBody, TextAnchor.LowerLeft, Theme.TextDim, FontStyle.Italic);
            UiFactory.Place(first.rectTransform, 0, 0.5f, 1, 0.62f);
            var name = Ui.Label(area, Theme.Caps(r.LastName), Theme.TextTitle + 8, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Place(name.rectTransform, 0, 0.34f, 1, 0.5f);

            var figures = UiFactory.Rect("Figures", area);
            UiFactory.Place(figures, 0, 0, 1, 0.3f);
            var halves = Ui.Columns12(figures, 6, 6);
            Figure(halves[0], Loc.T("rider.stats.lastRace"), LastRace(champ, r.Id));
            if (r.InjuryRoundsOut > 0)
            {
                var pill = Ui.Pill(halves[1], Loc.T("home.injured", r.InjuryRoundsOut), Theme.Bad);
                pill.rectTransform.anchorMin = pill.rectTransform.anchorMax = new Vector2(0, 0.5f);
                pill.rectTransform.pivot = new Vector2(0, 0.5f);
                pill.rectTransform.anchoredPosition = Vector2.zero;
                return;
            }
            int pos = champ != null ? champ.RiderPosition(r.Id) : 0;
            double pts = 0;
            if (champ != null)
                foreach (var e in champ.SortedRiders())
                    if (e.Id == r.Id) pts = e.Points;
            Figure(halves[1], Loc.T("rider.stats.position"), Theme.Pos(pos) + "  " + Theme.Dim("<size=" + Theme.TextBody + ">" +
                Theme.Caps(Loc.T("home.tile.points", pts.ToString("0"))) + "</size>"));
        }

        /// <summary>A caption over a value in timing yellow (one figure of a rider panel).</summary>
        private static void Figure(RectTransform area, string caption, string value)
        {
            var c = Ui.Label(area, Theme.Caps(caption), Theme.TextCaption, TextAnchor.LowerLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(c.rectTransform, 0, 0.58f, 1, 1);
            var v = Ui.Label(area, value, Theme.TextKey, TextAnchor.MiddleLeft, Theme.Timing, FontStyle.BoldAndItalic);
            UiFactory.Place(v.rectTransform, 0, 0, 1, 0.6f);
        }

        /// <summary>The rider's result in the last race of the championship ("P4", "DNF" or "-").</summary>
        private static string LastRace(ChampionshipState champ, string riderId)
        {
            if (champ == null) return "-";
            string last = "-";
            foreach (var wk in champ.Rounds)
                foreach (var s in wk.Sessions)
                {
                    if (s.Type != SessionType.Race) continue;
                    var e = s.Classification.Find(x => x.RiderId == riderId);
                    if (e == null) continue;
                    last = e.Status != ResultStatus.Finished ? Theme.BadText(Loc.T("result.DNF")) : Theme.Pos(e.Position);
                }
            return last;
        }

        /// <summary>A small tile with a big pictogram on the left, the key number on the right and one status line.</summary>
        private static void IconTile(RectTransform area, string title, string icon, string value, Color? valueColor, string status, Action onTap, int badge)
        {
            var body = Ui.Tile(area, title, onTap, null, badge, Theme.Bad);
            var img = UiIcons.Icon(body, icon, Theme.Alpha(Color.Lerp(Theme.TextDim, Color.white, 0.35f), 0.9f));
            img.preserveAspect = true;
            UiFactory.Place(img.rectTransform, 0, 0.24f, 0.48f, 1);
            var v = Ui.Label(body, value, Theme.TextDisplay + 16, TextAnchor.MiddleCenter, valueColor ?? Theme.Timing, FontStyle.BoldAndItalic);
            UiFactory.Place(v.rectTransform, 0.48f, 0.24f, 1, 1);
            if (string.IsNullOrEmpty(status)) return;
            var s = Ui.Label(body, Theme.Caps(status), Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(s.rectTransform, 0, 0, 1, 0.22f);
        }

        private void StaffTile(RectTransform area, WorldState world, Team team, int badge)
        {
            int missing = MissingStaff(world, team);
            IconTile(area, Loc.T("hub.staff"), UiIcons.Team, team.StaffIds.Count.ToString(), null,
                missing > 0 ? Theme.WarnText(Theme.Caps(Loc.T("hub.staffMissing", missing))) : Loc.T("hub.staffFull"),
                () => Shell.Show<StaffPage>(), badge);
        }

        /// <summary>The bike: the average component level (rating colour) and the R&amp;D projects running.</summary>
        private void BikeTile(RectTransform area, WorldState world, Team team, int badge)
        {
            var bike = world.GetBike(team.BikeId);
            double sum = 0;
            int n = 0;
            if (bike != null)
                foreach (var kv in bike.Components) { sum += kv.Value; n++; }
            double avg = n > 0 ? sum / n : 0;
            int running = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status == ProjectStatus.InProgress).Count;
            IconTile(area, Loc.T("hub.bike"), UiIcons.Bike, n > 0 ? avg.ToString("0") : "-", n > 0 ? Ui.RatingColor(avg) : (Color?)null,
                Loc.T("ux.hub.bikeLevel") + "  ·  " + Loc.T("hub.rnd", running, TeamCapabilities.ProjectCapacity(team)),
                () => Shell.Show<BikeScreen>(), badge);
        }

        // ------------------------------------------------------------------ next race

        /// <summary>
        /// The next race: round and date in the header, the Grand Prix name and the last result, then the three
        /// buttons: race (the screen's primary), simulate the Grand Prix and simulate the season.
        /// </summary>
        private void RaceTile(RectTransform area, WorldState world, Team team)
        {
            var next = Shell.NextOwnEvent();
            bool racing = next != null && next.Categories.Contains(team.CategoryId);
            RectTransform body;
            if (next == null)
            {
                body = Ui.Tile(area, Loc.T("home.seasonOver", world.Season), null, null, 0, null,
                    Wp2Ui.Info(Loc.T("home.seasonOver", world.Season), Loc.T("home.seasonOverInfo")));
            }
            else
            {
                ChampionshipState champ;
                int done = world.Championships.TryGetValue(team.CategoryId, out champ) ? champ.Rounds.Count : 0;
                string right = Theme.Caps(Loc.T("home.round", done + 1, Game.RoundsInSeason(team.CategoryId)) + "  ·  " +
                                          next.Round.Date.ToString("d MMM", Loc.Culture));
                body = Ui.Tile(area, Loc.T("ux.hub.nextRace"), null, right);
            }

            string name = next != null ? Loc.Round(next.Round.Name) : Loc.T("home.seasonOverShort");
            var gp = Ui.Label(body, Theme.Caps(name), Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Place(gp.rectTransform, 0, 0.68f, 1, 1);
            if (!string.IsNullOrEmpty(Shell.LastResult))
            {
                var last = Ui.Label(body, Theme.Dim(Theme.Caps(Loc.T("home.last")) + "  ") + FirstLine(Shell.LastResult), Theme.TextCaption,
                    TextAnchor.MiddleLeft, Theme.Text, FontStyle.Normal);
                UiFactory.Place(last.rectTransform, 0, 0.48f, 1, 0.68f);
            }

            var row = Wp2Ui.BottomStrip(body, Ui.Grid.Cta);
            string label = racing || next == null ? Shell.ContinueShort() : Loc.T("shell.short.simulate");
            Action go = racing || next == null ? (Action)Shell.Continue : Shell.SimulateEvent;
            if (next == null)
            {
                Wp2Ui.Chevron(Ui.PrimaryCTA(Ui.Columns12(row, 6)[0], label, go));
                return;
            }
            var cols = Ui.Columns12(row, 3, 3, 6);
            var simEvent = UiFactory.Rect("SimEvent", cols[0]);
            UiFactory.Place(simEvent, 0, 0.06f, 1, 0.94f);
            Ui.Secondary(simEvent, Loc.T("hub.simEvent"), Shell.SimulateEvent);
            var simSeason = UiFactory.Rect("SimSeason", cols[1]);
            UiFactory.Place(simSeason, 0, 0.06f, 1, 0.94f);
            Ui.Secondary(simSeason, Loc.T("home.simulateSeasonShort"), Shell.SimulateSeason);
            Wp2Ui.Chevron(Ui.PrimaryCTA(cols[2], label, go));
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
            if (missingStaff > 0) todo.Add(Item(Loc.T("home.todo.missingStaff", missingStaff), () => Shell.Show<StaffPage>(), TileStaff));
            double left = team.SeasonBudget - Accounts.SpentThisSeason(world, team);
            if (left < 0) todo.Add(Item(Theme.BadText(Loc.T("home.todo.budget")), () => Shell.Show<FinanceScreen>(), TileFinance));
            int bids = world.TransferOffers.FindAll(o => o.FromTeamId == team.Id).Count;
            if (bids > 0) todo.Add(Item(Loc.T("home.todo.bids", bids), () => Shell.Show<RidersPage>(), TileRiders));
            var offers = SponsorMarket.OffersFor(world, team);
            if (offers.Count > 0) todo.Add(Item(Loc.T("home.todo.sponsors", offers.Count), () => Shell.Show<SponsorsPage>(), TileSponsors));
            int expiring = 0;
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r != null && r.Contract != null && r.Contract.EndSeason <= world.Season && r.FutureContract == null) expiring++;
            }
            if (expiring > 0) todo.Add(Item(Loc.T("home.todo.contracts", expiring), () => Shell.Show<RidersPage>(), TileRiders));
            int seats = ContractService.SeatsFreeNextSeason(world, Game.Db, team);
            if (seats > 0) todo.Add(Item(Loc.T("home.todo.seats", seats, world.Season + 1), () => Shell.Show<MarketScreen>(), TileRiders));
            int staffEnding = 0;
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (world.Staff.TryGetValue(id, out s) && s.ContractEndSeason <= world.Season) staffEnding++;
            }
            if (staffEnding > 0) todo.Add(Item(Loc.T("home.todo.staff", staffEnding), () => Shell.Show<StaffPage>(), TileStaff));
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
                var last = Ui.Label(body, Theme.Dim(Theme.Caps(Loc.T("home.last")) + "  ") + FirstLine(Shell.LastResult), Theme.TextCaption,
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
