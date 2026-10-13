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
    /// Home (UX spec §3.2): six tiles on one screen, no scrolling. Top row: the riders (6 columns), staff and bike
    /// (3 each); bottom row: sponsors and finance (3 each) and the next race (6) carrying the screen's only primary
    /// button. Each tile shows one key number and one status line, with a badge for the decisions waiting there; the
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

            var rows = Ui.Rows(content, 0.58f, 0.42f);
            var top = Ui.Columns12(rows[0], 6, 3, 3);
            var bottom = Ui.Columns12(rows[1], 3, 3, 6);
            RidersTile(top[0], world, team, Count(todos, TileRiders));
            StaffTile(top[1], world, team, Count(todos, TileStaff));
            BikeTile(top[2], world, team, Count(todos, TileBike));
            SponsorsTile(bottom[0], world, team, Count(todos, TileSponsors));
            FinanceTile(bottom[1], world, team, Count(todos, TileFinance));
            RaceTile(bottom[2], world, team);
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
        /// The riders (at most three rows): number plate, SURNAME and rating; under the name the championship place and
        /// points, or the injury. A free seat is a warning row that opens the market.
        /// </summary>
        private void RidersTile(RectTransform area, WorldState world, Team team, int badge)
        {
            var body = Ui.Tile(area, Loc.T("hub.riders"), () => Shell.Show<RidersPage>(), null, badge, Theme.Bad);
            ChampionshipState champ;
            world.Championships.TryGetValue(team.CategoryId, out champ);
            int free = Math.Max(0, ContractService.SeatsFreeNow(world, Game.Db, team));
            int seats = Math.Min(3, team.RiderIds.Count + free);
            if (seats == 0) return;
            for (int i = 0; i < seats; i++)
            {
                var row = UiFactory.Rect("RiderRow", body);
                UiFactory.Place(row, 0, 1 - (i + 1f) / seats, 1, 1 - (float)i / seats);
                if (i > 0)
                {
                    var sep = UiFactory.Decor("Sep", row, new Color(1, 1, 1, 0.07f));
                    sep.rectTransform.anchorMin = new Vector2(0, 1);
                    sep.rectTransform.anchorMax = new Vector2(1, 1);
                    sep.rectTransform.offsetMin = new Vector2(0, -1);
                    sep.rectTransform.offsetMax = Vector2.zero;
                }
                var r = i < team.RiderIds.Count ? world.GetRider(team.RiderIds[i]) : null;
                if (r == null)
                {
                    FreeSeatRow(row);
                    continue;
                }
                RiderLine(row, champ, r, seats);
            }
        }

        private void FreeSeatRow(RectTransform row)
        {
            var hit = UiFactory.Panel("FreeSeat", row, Theme.Alpha(Theme.Warning, 0.08f));
            UiFactory.Place(hit.rectTransform, 0, 0.12f, 1, 0.88f);
            UiFactory.SideStripe(hit.transform, Theme.Warning, 6, 0);
            var b = hit.gameObject.AddComponent<Button>();
            b.targetGraphic = hit;
            b.onClick.AddListener(() =>
            {
                GameAudio.ClickSound();
                Shell.Show<MarketScreen>();
            });
            var t = Ui.Label(hit.transform, Theme.Caps(Loc.T("hub.freeSeat")), Theme.TextHeader, TextAnchor.MiddleLeft, Theme.Warning, FontStyle.BoldAndItalic);
            UiFactory.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(24, 0);
            t.rectTransform.offsetMax = new Vector2(-48, 0);
            var ch = UiIcons.Chevron(hit.transform, Theme.Warning);
            ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(1, 0.5f);
            ch.rectTransform.sizeDelta = new Vector2(22, 22);
            ch.rectTransform.anchoredPosition = new Vector2(-24, 0);
        }

        private static void RiderLine(RectTransform row, ChampionshipState champ, Rider r, int seats)
        {
            // Number plate: a dark slanted block with the race number.
            var plate = UiFactory.Decor("Number", row, new Color(0, 0, 0, 0.45f));
            UiFactory.Slant(plate, 2.4f);
            plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = new Vector2(0, 0.5f);
            plate.rectTransform.pivot = new Vector2(0, 0.5f);
            plate.rectTransform.anchoredPosition = Vector2.zero;
            plate.rectTransform.sizeDelta = new Vector2(76, 48);
            var num = Ui.Label(plate.transform, r.Number > 0 ? r.Number.ToString() : "-", Theme.TextTitle, TextAnchor.MiddleCenter, Color.white, FontStyle.BoldAndItalic);
            UiFactory.Stretch(num.rectTransform);

            float side = seats <= 2 ? 72 : 60;
            var badge = Ui.RatingBadge(row, r.Attributes.Overall, null, side);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1, 0.5f);
            badge.rectTransform.pivot = new Vector2(1, 0.5f);
            badge.rectTransform.anchoredPosition = Vector2.zero;

            var name = Ui.Label(row, Theme.Caps(r.LastName), Theme.TextTitle, TextAnchor.LowerLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Place(name.rectTransform, 0, 0.5f, 1, 0.92f);
            name.rectTransform.offsetMin = new Vector2(92, 0);
            name.rectTransform.offsetMax = new Vector2(-side - 16, 0);

            if (r.InjuryRoundsOut > 0)
            {
                var pill = Ui.Pill(row, Loc.T("home.injured", r.InjuryRoundsOut), Theme.Bad);
                pill.rectTransform.anchorMin = pill.rectTransform.anchorMax = new Vector2(0, 0.3f);
                pill.rectTransform.pivot = new Vector2(0, 0.5f);
                pill.rectTransform.anchoredPosition = new Vector2(92, 0);
                return;
            }
            int pos = champ != null ? champ.RiderPosition(r.Id) : 0;
            double pts = 0;
            if (champ != null)
                foreach (var e in champ.SortedRiders())
                    if (e.Id == r.Id) pts = e.Points;
            string line = (pos > 0 ? Theme.Pos(pos) + "  ·  " : "") + Loc.T("home.tile.points", pts.ToString("0"));
            var sub = Ui.Label(row, Theme.Caps(line), Theme.TextCaption, TextAnchor.UpperLeft, Color.Lerp(Theme.TextDim, Color.white, 0.2f), FontStyle.Bold);
            UiFactory.Place(sub.rectTransform, 0, 0.08f, 1, 0.48f);
            sub.rectTransform.offsetMin = new Vector2(92, 0);
            sub.rectTransform.offsetMax = new Vector2(-side - 16, 0);
        }

        private void StaffTile(RectTransform area, WorldState world, Team team, int badge)
        {
            int missing = MissingStaff(world, team);
            Ui.StatTile(area, Loc.T("hub.staff"), team.StaffIds.Count.ToString(),
                missing > 0 ? Theme.WarnText(Theme.Caps(Loc.T("hub.staffMissing", missing))) : Loc.T("hub.staffFull"),
                () => Shell.Show<StaffPage>(), null, UiIcons.Team, badge);
        }

        private void BikeTile(RectTransform area, WorldState world, Team team, int badge)
        {
            var bike = world.GetBike(team.BikeId);
            double sum = 0;
            int n = 0;
            if (bike != null)
                foreach (var kv in bike.Components) { sum += kv.Value; n++; }
            double avg = n > 0 ? sum / n : 0;
            int running = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status == ProjectStatus.InProgress).Count;
            Ui.StatTile(area, Loc.T("hub.bike"), n > 0 ? avg.ToString("0") : "-", Loc.T("hub.rnd", running, TeamCapabilities.ProjectCapacity(team)),
                () => Shell.Show<BikeScreen>(), n > 0 ? Ui.RatingColor(avg) : (Color?)null, UiIcons.Bike, badge);
        }

        private void SponsorsTile(RectTransform area, WorldState world, Team team, int badge)
        {
            int deals = SponsorService.DealsIn(team, world.Season);
            int slots = SponsorService.Slots(Game.Db, team, world.Season);
            int offers = SponsorMarket.OffersFor(world, team).Count;
            Ui.StatTile(area, Loc.T("hub.sponsors"), deals + "/" + slots,
                offers > 0 ? Theme.TimingText(Theme.Caps(Loc.T("hub.offers", offers))) : Loc.T("hub.noOffers"),
                () => Shell.Show<SponsorsPage>(), null, UiIcons.Sponsor, badge);
        }

        /// <summary>The money left in the season budget (the cash is already in the top bar).</summary>
        private void FinanceTile(RectTransform area, WorldState world, Team team, int badge)
        {
            double left = team.SeasonBudget - Accounts.SpentThisSeason(world, team);
            Ui.StatTile(area, Loc.T("hub.finance"), MathX.FormatMoney(left), Loc.T("ux.hub.budgetLeft"),
                () => Shell.Show<FinanceScreen>(), left < 0 ? Theme.Bad : (Color?)null, UiIcons.Finance, badge);
        }

        // ------------------------------------------------------------------ next race

        /// <summary>
        /// The next race: round and date in the header, the Grand Prix name, the last result in one line and the
        /// screen's primary button (race, simulate, or finish/close the season). The quick simulations sit behind
        /// the "..." button.
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
                Wp2Ui.HeaderMore(body, MoreSheet);
            }

            string name = next != null ? Loc.Round(next.Round.Name) : Loc.T("home.seasonOverShort");
            var gp = Ui.Label(body, Theme.Caps(name), Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Place(gp.rectTransform, 0, 0.66f, 1, 1);
            if (!string.IsNullOrEmpty(Shell.LastResult))
            {
                var last = Ui.Label(body, Theme.Dim(Theme.Caps(Loc.T("home.last")) + "  ") + FirstLine(Shell.LastResult), Theme.TextCaption,
                    TextAnchor.MiddleLeft, Theme.Text, FontStyle.Normal);
                UiFactory.Place(last.rectTransform, 0, 0.46f, 1, 0.66f);
            }

            var cta = Wp2Ui.BottomStrip(body, Ui.Grid.Cta);
            string label = racing || next == null ? Shell.ContinueShort() : Loc.T("shell.short.simulate");
            var b = Ui.PrimaryCTA(cta, label, racing || next == null ? (Action)Shell.Continue : Shell.SimulateEvent);
            Wp2Ui.Chevron(b);
        }

        /// <summary>The "..." sheet of the race tile: the quick simulations and the last result in full.</summary>
        private void MoreSheet()
        {
            Ui.Sheet(Loc.T("home.more"), list =>
            {
                var l = (RectTransform)list;
                Ui.Secondary(l, Loc.T("hub.simEvent"), () =>
                {
                    Ui.CloseSheet();
                    Shell.SimulateEvent();
                });
                Ui.Secondary(l, Loc.T("home.simulateSeasonShort"), () =>
                {
                    Ui.CloseSheet();
                    Shell.SimulateSeason();
                });
                if (!string.IsNullOrEmpty(Shell.LastResult))
                {
                    Ui.Section(list, Loc.T("home.last"));
                    UiFactory.Paragraph(list, Shell.LastResult, Theme.TextBody);
                }
            }, null, null, 0.5f);
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
