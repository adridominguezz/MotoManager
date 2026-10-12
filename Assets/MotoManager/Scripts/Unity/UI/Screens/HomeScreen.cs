using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using MotoManager.Core.Data;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// Home: a hub of large tappable tiles that fits one screen. A slim strip on top carries the most urgent
    /// decision and the latest headline; below, the riders tile, staff, bike, sponsors and finance tiles (one big
    /// figure and one status line each, with a badge when something there needs a decision) and the big slanted
    /// race call-to-action. Every tile opens its screen.
    /// </summary>
    public class HubScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.home"); } }
        private static int MaxNews { get { return UiScale.Compact ? 6 : 8; } }

        private const float StripHeight = 60;
        private const float TileHead = 46;
        private bool _newsOpen;

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
                BuildWithoutTeam(content, world);
                return;
            }
            var todos = Todos(world, team);
            Strip(content, world, todos);

            var grid = UiFactory.Rect("Grid", content);
            UiFactory.Stretch(grid);
            grid.offsetMax = new Vector2(0, -StripHeight - 4);

            RidersTile(grid, world, team, Count(todos, TileRiders));
            StaffTile(grid, world, team, Count(todos, TileStaff));
            BikeTile(grid, world, team, Count(todos, TileBike));
            SponsorsTile(grid, world, team, Count(todos, TileSponsors));
            FinanceTile(grid, world, team, Count(todos, TileFinance));
            RaceCall(grid, world, team);

            if (_newsOpen) NewsOverlay(content, world);
        }

        // ------------------------------------------------------------------ top strip

        /// <summary>
        /// One line on top: the most urgent decision (tap to resolve it) on the left, the latest headline (tap for the
        /// news) on the right. Without decisions, the last result takes the left side.
        /// </summary>
        private void Strip(RectTransform content, WorldState world, List<Todo> todos)
        {
            var strip = UiFactory.Rect("Strip", content);
            strip.anchorMin = new Vector2(0, 1);
            strip.anchorMax = new Vector2(1, 1);
            strip.pivot = new Vector2(0.5f, 1);
            strip.offsetMin = new Vector2(0, -StripHeight);
            strip.offsetMax = Vector2.zero;

            string left;
            Action leftGo = null;
            bool alert = todos.Count > 0;
            if (alert)
            {
                left = "<b>" + todos[0].Text + "</b>" + (todos.Count > 1 ? "   " + Theme.Dim("+" + (todos.Count - 1)) : "");
                leftGo = todos[0].Go;
            }
            else if (!string.IsNullOrEmpty(Shell.LastResult)) left = FirstLine(Shell.LastResult);
            else left = Theme.Dim(Loc.T("hub.allClear"));
            var l = StripButton(strip, 0, 0.55f, left, alert ? (Color)Theme.Warning : Theme.Steel, alert ? "!" : null, leftGo);
            if (alert) l.GetComponentInChildren<Text>().color = Theme.Text;

            string headline = LatestHeadline(world);
            StripButton(strip, 0.55f, 1, headline != null ? headline : Theme.Dim(Loc.T("home.noNews")), Theme.Steel, null,
                () => { _newsOpen = true; Shell.Refresh(); });
        }

        private static Button StripButton(RectTransform strip, float x0, float x1, string text, Color mark, string glyph, Action onTap)
        {
            var b = UiFactory.Button("StripItem", strip, "", onTap, new Color(1, 1, 1, 0.05f), Theme.FontSmall);
            b.image.sprite = null;
            UiFactory.Place((RectTransform)b.transform, x0, 0, x1, 1, 3);
            b.interactable = onTap != null;
            var colors = b.colors;
            colors.disabledColor = Color.white;
            b.colors = colors;
            var block = UiFactory.Decor("Mark", b.transform, mark);
            UiFactory.Slant(block, 3f);
            block.rectTransform.anchorMin = new Vector2(0, 0);
            block.rectTransform.anchorMax = new Vector2(0, 1);
            block.rectTransform.pivot = new Vector2(0, 0.5f);
            block.rectTransform.offsetMin = new Vector2(6, 8);
            block.rectTransform.offsetMax = new Vector2(glyph != null ? 46 : 18, -8);
            if (glyph != null)
            {
                var g = UiFactory.Label("Glyph", block.transform, glyph, Theme.FontNormal, TextAnchor.MiddleCenter, new Color32(12, 13, 16, 255));
                g.fontStyle = FontStyle.Bold;
                UiFactory.Stretch(g.rectTransform);
            }
            var label = b.GetComponentInChildren<Text>();
            label.supportRichText = true;
            label.alignment = TextAnchor.MiddleLeft;
            label.text = text + (onTap != null ? "  " + Theme.Dim("›") : "");
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Theme.FontSmall - 8;
            label.resizeTextMaxSize = Theme.FontSmall;
            label.rectTransform.offsetMin = new Vector2(glyph != null ? 58 : 30, 0);
            label.rectTransform.offsetMax = new Vector2(-12, 0);
            return b;
        }

        private string LatestHeadline(WorldState world)
        {
            foreach (var n in OwnNews(world)) return n.Headline;
            return null;
        }

        /// <summary>The news of the team's own world (Grand Prix or Superbike) and general news, newest first.</summary>
        private IEnumerable<NewsItem> OwnNews(WorldState world)
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
                yield return n;
            }
        }

        /// <summary>The latest headlines over the hub; a tap anywhere closes them.</summary>
        private void NewsOverlay(RectTransform content, WorldState world)
        {
            var shade = UiFactory.Panel("NewsShade", content, new Color(0, 0, 0, 0.6f));
            UiFactory.Stretch(shade.rectTransform);
            shade.gameObject.AddComponent<Button>().onClick.AddListener(() => { _newsOpen = false; Shell.Refresh(); });
            var holder = UiFactory.Rect("News", shade.transform);
            UiFactory.Place(holder, 0.12f, 0.04f, 0.88f, 0.96f);
            var list = UiFactory.ScrollList("NewsList", holder);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var card = Ui.Card(list, Loc.T("home.news"));
            int shown = 0;
            foreach (var n in OwnNews(world))
            {
                if (shown >= MaxNews) break;
                UiFactory.Paragraph(card, Theme.Dim(n.Date.ToString("dd MMM", Loc.Culture)) + "   " + (n.Important ? "<b>" + n.Headline + "</b>" : n.Headline), Theme.FontSmall);
                shown++;
            }
            if (shown == 0) Ui.Note(card, Loc.T("home.noNews"));
            UiFactory.ButtonRow(card, 56, UiFactory.Act(Loc.T("hub.close"), () => { _newsOpen = false; Shell.Refresh(); }));
        }

        // ------------------------------------------------------------------ tiles

        /// <summary>
        /// A big tappable tile: graphite body, a dark header strip with an accent tag and the upper-case title, and a
        /// red badge with the number of decisions waiting there. Returns the body under the header.
        /// </summary>
        private static RectTransform Tile(RectTransform grid, float x0, float y0, float x1, float y1, string title, int badge, Action onTap)
        {
            var b = UiFactory.Button("Tile", grid, "", onTap, Color.Lerp(Theme.Card, Theme.PanelLight, 0.28f), Theme.FontSmall);
            b.image.sprite = null;
            UiFactory.Gradient(b.image, 1.1f, 0.88f);
            b.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.06f);
            UiFactory.Place((RectTransform)b.transform, x0, y0, x1, y1, 5);
            var head = UiFactory.Decor("Head", b.transform, new Color(0, 0, 0, 0.38f));
            head.rectTransform.anchorMin = new Vector2(0, 1);
            head.rectTransform.anchorMax = new Vector2(1, 1);
            head.rectTransform.pivot = new Vector2(0.5f, 1);
            head.rectTransform.offsetMin = new Vector2(0, -TileHead);
            head.rectTransform.offsetMax = Vector2.zero;
            var tag = UiFactory.Decor("Tag", head.transform, Theme.Accent);
            UiFactory.Slant(tag, 3.2f);
            tag.rectTransform.anchorMin = new Vector2(0, 0.2f);
            tag.rectTransform.anchorMax = new Vector2(0, 0.8f);
            tag.rectTransform.offsetMin = new Vector2(10, 0);
            tag.rectTransform.offsetMax = new Vector2(26, 0);
            var t = UiFactory.Label("Title", head.transform, Theme.Caps(title), Theme.FontSmall + 2, TextAnchor.MiddleLeft, Theme.Text);
            t.fontStyle = FontStyle.BoldAndItalic;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Theme.FontSmall - 8;
            t.resizeTextMaxSize = Theme.FontSmall + 2;
            UiFactory.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(36, 0);
            t.rectTransform.offsetMax = new Vector2(badge > 0 ? -58 : -26, 0);
            var arrow = UiFactory.Label("Arrow", head.transform, "›", Theme.FontNormal, TextAnchor.MiddleRight, Theme.TextDim);
            UiFactory.Stretch(arrow.rectTransform);
            arrow.rectTransform.offsetMax = new Vector2(-10, 0);
            if (badge > 0)
            {
                arrow.gameObject.SetActive(false);
                var dot = UiFactory.Decor("Badge", head.transform, Theme.Bad);
                UiFactory.Round(dot, 1.2f);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(1, 0.5f);
                dot.rectTransform.pivot = new Vector2(1, 0.5f);
                dot.rectTransform.anchoredPosition = new Vector2(-10, 0);
                dot.rectTransform.sizeDelta = new Vector2(38, 34);
                var n = UiFactory.Label("Count", dot.transform, badge.ToString(), Theme.FontSmall, TextAnchor.MiddleCenter, Color.white);
                n.fontStyle = FontStyle.Bold;
                UiFactory.Stretch(n.rectTransform);
            }
            var body = UiFactory.Rect("Body", b.transform);
            UiFactory.Stretch(body);
            body.offsetMax = new Vector2(0, -TileHead);
            return body;
        }

        /// <summary>The usual tile content: a pictogram, one big figure beside it and one status line under them.</summary>
        private static void Figure(RectTransform body, string icon, string value, string status)
        {
            var img = UiIcons.Icon(body, icon, Theme.Alpha(Color.Lerp(Theme.Accent, Color.white, 0.25f), 0.85f));
            UiFactory.Place(img.rectTransform, 0.06f, 0.38f, 0.36f, 0.92f);
            var v = UiFactory.Label("Value", body, value, Theme.FontTitle, TextAnchor.MiddleCenter, Theme.Text);
            v.supportRichText = true;
            v.fontStyle = FontStyle.BoldAndItalic;
            v.resizeTextForBestFit = true;
            v.resizeTextMinSize = Theme.FontNormal - 4;
            v.resizeTextMaxSize = Theme.FontTitle;
            UiFactory.DropShadow(v, 0.45f, 2);
            UiFactory.Place(v.rectTransform, 0.38f, 0.36f, 0.97f, 0.94f);
            var s = UiFactory.Label("Status", body, status, Theme.FontSmall, TextAnchor.MiddleCenter, Color.Lerp(Theme.TextDim, Color.white, 0.25f));
            s.supportRichText = true;
            s.fontStyle = FontStyle.Bold;
            s.resizeTextForBestFit = true;
            s.resizeTextMinSize = Theme.FontSmall - 8;
            s.resizeTextMaxSize = Theme.FontSmall;
            UiFactory.Place(s.rectTransform, 0.04f, 0.06f, 0.96f, 0.34f);
            var rule = UiFactory.Decor("Rule", body, new Color(1, 1, 1, 0.06f));
            UiFactory.Place(rule.rectTransform, 0.06f, 0.35f, 0.94f, 0.35f);
            rule.rectTransform.offsetMax = new Vector2(0, 1);
        }

        /// <summary>The riders tile: each rider with number, name, championship place, morale bar and rating.</summary>
        private void RidersTile(RectTransform grid, WorldState world, Team team, int badge)
        {
            var body = Tile(grid, 0, 0.44f, 0.5f, 1, Loc.T("hub.riders"), badge, () => Shell.Show<RidersPage>());
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
                    sep.rectTransform.anchorMin = new Vector2(0.03f, 1);
                    sep.rectTransform.anchorMax = new Vector2(0.97f, 1);
                    sep.rectTransform.offsetMin = new Vector2(0, -1);
                    sep.rectTransform.offsetMax = Vector2.zero;
                }
                var r = i < team.RiderIds.Count ? world.GetRider(team.RiderIds[i]) : null;
                if (r == null)
                {
                    var empty = UiFactory.Label("Free", row, Theme.WarnText("<b>" + Theme.Caps(Loc.T("hub.freeSeat")) + "</b>") + "  " + Theme.Dim("›"),
                        Theme.FontNormal, TextAnchor.MiddleLeft);
                    empty.supportRichText = true;
                    UiFactory.Stretch(empty.rectTransform);
                    empty.rectTransform.offsetMin = new Vector2(22, 0);
                    continue;
                }
                RiderLine(row, world, champ, r);
            }
        }

        private static void RiderLine(RectTransform row, WorldState world, ChampionshipState champ, Rider r)
        {
            // Number badge: a slanted accent block with the race number.
            var badge = UiFactory.Decor("Number", row, Theme.Accent);
            UiFactory.Slant(badge, 2.4f);
            badge.rectTransform.anchorMin = new Vector2(0, 0.2f);
            badge.rectTransform.anchorMax = new Vector2(0, 0.8f);
            badge.rectTransform.pivot = new Vector2(0, 0.5f);
            badge.rectTransform.offsetMin = new Vector2(14, 0);
            badge.rectTransform.offsetMax = new Vector2(92, 0);
            var num = UiFactory.Label("N", badge.transform, r.Number > 0 ? r.Number.ToString() : "-", Theme.FontLarge - 4, TextAnchor.MiddleCenter, Color.white);
            num.fontStyle = FontStyle.BoldAndItalic;
            num.resizeTextForBestFit = true;
            num.resizeTextMinSize = Theme.FontSmall;
            num.resizeTextMaxSize = Theme.FontLarge - 4;
            UiFactory.DropShadow(num, 0.4f, 1.5f);
            UiFactory.Stretch(num.rectTransform);

            // Overall rating in a coloured square on the right.
            double overall = r.Attributes.Overall;
            var square = UiFactory.Decor("Rating", row, Ui.RatingColor(overall));
            square.rectTransform.anchorMin = new Vector2(1, 0.5f);
            square.rectTransform.anchorMax = new Vector2(1, 0.5f);
            square.rectTransform.pivot = new Vector2(1, 0.5f);
            square.rectTransform.anchoredPosition = new Vector2(-14, 0);
            float side = Mathf.Min(84, Mathf.Max(48, row.rect.height > 0 ? row.rect.height * 0.62f : 72));
            square.rectTransform.sizeDelta = new Vector2(side, side);
            var rating = UiFactory.Label("Value", square.transform, overall.ToString("0"), Theme.FontLarge - 4, TextAnchor.MiddleCenter, new Color32(12, 13, 16, 255));
            rating.fontStyle = FontStyle.Bold;
            UiFactory.Stretch(rating.rectTransform);

            // Name, championship place and a thin morale bar.
            var name = UiFactory.Label("Name", row, "<b>" + r.FullName + "</b>", Theme.FontNormal + 2, TextAnchor.LowerLeft, Theme.Text);
            name.supportRichText = true;
            name.fontStyle = FontStyle.Italic;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = Theme.FontSmall - 4;
            name.resizeTextMaxSize = Theme.FontNormal + 2;
            UiFactory.Place(name.rectTransform, 0, 0.5f, 1, 0.9f);
            name.rectTransform.offsetMin = new Vector2(106, 0);
            name.rectTransform.offsetMax = new Vector2(-112, 0);
            int pos = champ != null ? champ.RiderPosition(r.Id) : 0;
            double pts = 0;
            if (champ != null)
                foreach (var e in champ.SortedRiders())
                    if (e.Id == r.Id) pts = e.Points;
            string line = (pos > 0 ? "<b>P" + pos + "</b>  " : "") + Loc.T("home.tile.points", pts.ToString("0")) +
                          (r.InjuryRoundsOut > 0 ? "   " + Theme.BadText("+ " + Loc.T("home.injured", r.InjuryRoundsOut)) : "");
            var sub = UiFactory.Label("Sub", row, line, Theme.FontSmall - 2, TextAnchor.UpperLeft, Theme.TextDim);
            sub.supportRichText = true;
            sub.resizeTextForBestFit = true;
            sub.resizeTextMinSize = Theme.FontSmall - 8;
            sub.resizeTextMaxSize = Theme.FontSmall - 2;
            UiFactory.Place(sub.rectTransform, 0, 0.22f, 1, 0.5f);
            sub.rectTransform.offsetMin = new Vector2(106, 0);
            sub.rectTransform.offsetMax = new Vector2(-112, 0);
            var track = UiFactory.Decor("Morale", row, new Color(0, 0, 0, 0.5f));
            track.rectTransform.anchorMin = new Vector2(0, 0.14f);
            track.rectTransform.anchorMax = new Vector2(1, 0.14f);
            track.rectTransform.offsetMin = new Vector2(106, -3);
            track.rectTransform.offsetMax = new Vector2(-112, 3);
            var fill = UiFactory.Decor("Fill", track.transform, Ui.RatingColor(r.Morale));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)(r.Morale / 100.0)), 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        }

        private void StaffTile(RectTransform grid, WorldState world, Team team, int badge)
        {
            var body = Tile(grid, 0.5f, 0.44f, 0.75f, 1, Loc.T("hub.staff"), badge, () => Shell.Show<StaffPage>());
            int missing = MissingStaff(world, team);
            Figure(body, UiIcons.Person, team.StaffIds.Count.ToString(),
                missing > 0 ? Theme.WarnText(Theme.Caps(Loc.T("hub.staffMissing", missing))) : Theme.Caps(Loc.T("hub.staffFull")));
        }

        private void BikeTile(RectTransform grid, WorldState world, Team team, int badge)
        {
            var body = Tile(grid, 0.75f, 0.44f, 1, 1, Loc.T("hub.bike"), badge, () => Shell.Show<BikeScreen>());
            var bike = world.GetBike(team.BikeId);
            double sum = 0;
            int n = 0;
            if (bike != null)
                foreach (var kv in bike.Components) { sum += kv.Value; n++; }
            double avg = n > 0 ? sum / n : 0;
            int running = world.Projects.FindAll(p => p.TeamId == team.Id && p.Status == ProjectStatus.InProgress).Count;
            Figure(body, UiIcons.Bike, n > 0 ? "<color=#" + ColorUtility.ToHtmlStringRGB(Ui.RatingColor(avg)) + ">" + avg.ToString("0") + "</color>" : "-",
                Theme.Caps(Loc.T("hub.rnd", running, TeamCapabilities.ProjectCapacity(team))));
        }

        private void SponsorsTile(RectTransform grid, WorldState world, Team team, int badge)
        {
            var body = Tile(grid, 0, 0, 0.25f, 0.44f, Loc.T("hub.sponsors"), badge, () => Shell.Show<SponsorsPage>());
            int deals = SponsorService.DealsIn(team, world.Season);
            int slots = SponsorService.Slots(Game.Db, team, world.Season);
            int offers = SponsorMarket.OffersFor(world, team).Count;
            Figure(body, UiIcons.Sponsor, deals + "<size=" + Theme.FontNormal + ">/" + slots + "</size>",
                offers > 0 ? Theme.TimingText(Theme.Caps(Loc.T("hub.offers", offers))) : Theme.Caps(Loc.T("hub.noOffers")));
        }

        private void FinanceTile(RectTransform grid, WorldState world, Team team, int badge)
        {
            var body = Tile(grid, 0.25f, 0, 0.5f, 0.44f, Loc.T("hub.finance"), badge, () => Shell.Show<FinanceScreen>());
            var org = world.OrganisationOf(team);
            double cash = org != null ? org.Cash : Accounts.Cash(world, team);
            double left = team.SeasonBudget - Accounts.SpentThisSeason(world, team);
            string budget = Theme.Caps(Loc.T("hub.budget", Core.Common.MathX.FormatMoney(left)));
            Figure(body, UiIcons.Finance, Theme.Money(cash), left < 0 ? Theme.BadText(budget) : budget);
        }

        // ------------------------------------------------------------------ race call-to-action

        /// <summary>
        /// Bottom right: the big slanted "RACE ›" block with the round and the event, and the two quick simulations
        /// beside it. After the last round it closes (or finishes) the season.
        /// </summary>
        private void RaceCall(RectTransform grid, WorldState world, Team team)
        {
            var area = UiFactory.Rect("Race", grid);
            UiFactory.Place(area, 0.5f, 0, 1, 0.44f, 5);
            var next = Shell.NextOwnEvent();
            bool racing = next != null && next.Categories.Contains(team.CategoryId);
            float split = next != null ? 0.34f : 0;
            if (next != null)
            {
                var sims = UiFactory.Rect("Sims", area);
                UiFactory.Place(sims, 0, 0, split, 1);
                var a = UiFactory.Button("SimEvent", sims, Theme.Caps(Loc.T("hub.simEvent")), Shell.SimulateEvent, Theme.PanelLight, Theme.FontSmall);
                UiFactory.Place((RectTransform)a.transform, 0, 0.5f, 1, 1, 3);
                Bold(a);
                var s = UiFactory.Button("SimSeason", sims, Theme.Caps(Loc.T("home.simulateSeasonShort")), Shell.SimulateSeason, Theme.PanelLight, Theme.FontSmall);
                UiFactory.Place((RectTransform)s.transform, 0, 0, 1, 0.5f, 3);
                Bold(s);
            }

            var go = UiFactory.Button("RaceCall", area, "", racing || next == null ? (Action)Shell.Continue : Shell.SimulateEvent, Theme.Accent, Theme.FontTitle);
            UiFactory.Slant(go.image, 1.2f);
            UiFactory.Gradient(go.image, 1.18f, 0.82f);
            go.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.16f);
            UiFactory.Place((RectTransform)go.transform, split, 0, 1, 1, 3);
            ((RectTransform)go.transform).offsetMin = new Vector2(split > 0 ? 14 : 3, 3);
            var stripes = UiFactory.Pattern("Hatch", go.transform, UiFactory.StripesSprite, new Color(1, 1, 1, 0.1f), 22);
            UiFactory.Place(stripes.rectTransform, 0.62f, 0.08f, 0.9f, 0.92f);
            UiFactory.Gradient(stripes, new Color(1, 1, 1, 0), Color.white, true);

            string top, bottom;
            if (next == null)
            {
                top = Theme.Caps(Loc.T("home.seasonOver", world.Season));
                bottom = "";
            }
            else
            {
                ChampionshipState champ;
                int done = world.Championships.TryGetValue(team.CategoryId, out champ) ? champ.Rounds.Count : 0;
                top = Theme.Caps(Loc.T("home.round", done + 1, Game.RoundsInSeason(team.CategoryId)) + "  ·  " + next.Round.Date.ToString("d MMM", Loc.Culture));
                bottom = Theme.Caps(Loc.Round(next.Round.Name));
            }
            var main = go.GetComponentInChildren<Text>();
            main.text = Theme.Caps(racing || next == null ? Shell.ContinueShort() : Loc.T("shell.short.simulate")) + "  ›";
            main.fontStyle = FontStyle.BoldAndItalic;
            main.alignment = TextAnchor.MiddleLeft;
            main.resizeTextForBestFit = true;
            main.resizeTextMinSize = Theme.FontNormal;
            main.resizeTextMaxSize = Theme.FontTitle + 8;
            UiFactory.Place(main.rectTransform, 0, 0.3f, 1, 0.72f);
            main.rectTransform.offsetMin = new Vector2(40, 0);
            main.rectTransform.offsetMax = new Vector2(-24, 0);
            var t = UiFactory.Label("Round", go.transform, top, Theme.FontSmall, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.85f));
            t.fontStyle = FontStyle.Bold;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Theme.FontSmall - 8;
            t.resizeTextMaxSize = Theme.FontSmall;
            UiFactory.Place(t.rectTransform, 0, 0.72f, 1, 0.94f);
            t.rectTransform.offsetMin = new Vector2(44, 0);
            t.rectTransform.offsetMax = new Vector2(-24, 0);
            var e = UiFactory.Label("Event", go.transform, "<b>" + bottom + "</b>", Theme.FontNormal, TextAnchor.MiddleLeft, Color.white);
            e.supportRichText = true;
            e.fontStyle = FontStyle.Italic;
            e.resizeTextForBestFit = true;
            e.resizeTextMinSize = Theme.FontSmall - 6;
            e.resizeTextMaxSize = Theme.FontNormal;
            UiFactory.DropShadow(e, 0.35f, 1.5f);
            UiFactory.Place(e.rectTransform, 0, 0.06f, 1, 0.3f);
            e.rectTransform.offsetMin = new Vector2(28, 0);
            e.rectTransform.offsetMax = new Vector2(-30, 0);
        }

        private static void Bold(Button b)
        {
            var t = b.GetComponentInChildren<Text>();
            t.fontStyle = FontStyle.BoldAndItalic;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Theme.FontSmall - 8;
            t.resizeTextMaxSize = Theme.FontSmall;
            t.rectTransform.offsetMin = new Vector2(10, 0);
            t.rectTransform.offsetMax = new Vector2(-10, 0);
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

        private static string FirstLines(string text, int lines)
        {
            var parts = text.TrimEnd('\n').Split('\n');
            if (parts.Length <= lines) return string.Join("\n", parts);
            return string.Join("\n", parts, 0, lines) + "\n…";
        }

        // ------------------------------------------------------------------ without a team

        /// <summary>Between jobs: the job search, the next event (simulated) and the news.</summary>
        private void BuildWithoutTeam(RectTransform content, WorldState world)
        {
            var list = Page(content);
            RectTransform left, right;
            Ui.Columns(list, out left, out right);
            var none = Ui.Card(left, Loc.T("home.noTeam"), Loc.T("home.noTeamInfo"));
            UiFactory.ButtonRow(none, 64,
                UiFactory.Primary(Loc.T("mycareer.seeOffers"), () => Shell.Show<CareerOffersPage>()),
                UiFactory.Act(Loc.T("mycareer.found"), () => Shell.Show<CareerFoundPage>()));
            NextEventCard(left, world);
            if (!string.IsNullOrEmpty(Shell.LastResult))
            {
                var last = Ui.Card(left, Loc.T("home.last"));
                UiFactory.Paragraph(last, FirstLines(Shell.LastResult, 2), Theme.FontSmall);
            }
            var card = Ui.Card(right, Loc.T("home.news"));
            int shown = 0;
            foreach (var n in OwnNews(world))
            {
                if (shown >= MaxNews) break;
                UiFactory.Paragraph(card, Theme.Dim(n.Date.ToString("dd MMM", Loc.Culture)) + "   " + (n.Important ? "<b>" + n.Headline + "</b>" : n.Headline), Theme.FontSmall);
                shown++;
            }
            if (shown == 0) Ui.Note(card, Loc.T("home.noNews"));
        }

        private void NextEventCard(Transform parent, WorldState world)
        {
            var next = Shell.NextOwnEvent();
            if (next == null)
            {
                var over = Ui.Card(parent, Loc.T("home.seasonOver", world.Season), Loc.T("home.seasonOverInfo"));
                UiFactory.ButtonRow(over, 72, Game.IsSeasonOver ? UiFactory.Primary(Loc.T("shell.closeSeason"), Shell.CloseSeason) : UiFactory.Primary(Loc.T("shell.finishSeason"), Shell.SimulateSeason));
                return;
            }
            var cats = new List<string>();
            foreach (var c in next.Categories) cats.Add(Game.Db.GetCategory(c).ShortName);
            var card = Ui.Card(parent, Loc.T("home.next"), next.Round.Date.ToString("d MMMM", Loc.Culture) + " · " + string.Join(", ", cats.ToArray()));
            var big = UiFactory.Label("Event", UiFactory.Row(card, 64), "<b><i>" + Loc.Round(next.Round.Name).ToUpperInvariant() + "</i></b>", Theme.FontLarge, TextAnchor.MiddleLeft, Theme.Text);
            big.supportRichText = true;
            big.resizeTextForBestFit = true;
            big.resizeTextMinSize = Theme.FontNormal - 4;
            big.resizeTextMaxSize = Theme.FontLarge;
            UiFactory.Stretch(big.rectTransform);
            UiFactory.ButtonRow(card, 72,
                UiFactory.Primary(Loc.T("shell.simulate"), Shell.SimulateEvent),
                UiFactory.Act(Loc.T("home.simulateSeasonShort"), Shell.SimulateSeason));
        }
    }
}
