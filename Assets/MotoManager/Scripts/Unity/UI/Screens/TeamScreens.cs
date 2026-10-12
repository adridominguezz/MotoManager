using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI.Screens
{
    internal static class Fmt
    {
        /// <summary>Attribute as the team perceives it: a value, or a range while the rider is not well known.</summary>
        public static string Attr(WorldState world, string teamId, Rider r, string name)
        {
            double est, min, max;
            ScoutingService.Perceive(world, teamId, r, name, out est, out min, out max);
            if (max - min < 3) return est.ToString("0");
            return min.ToString("0") + "-" + max.ToString("0");
        }

        public static double Estimate(WorldState world, string teamId, Rider r, string name)
        {
            double est, min, max;
            ScoutingService.Perceive(world, teamId, r, name, out est, out min, out max);
            return est;
        }

        public static string Styles(Rider r)
        {
            if (r.Styles.Count == 0) return "-";
            var names = new List<string>();
            foreach (var s in r.Styles) names.Add(Loc.T("style." + s));
            return string.Join(", ", names.ToArray());
        }

        public static string Signed(double v)
        {
            return (v >= 0 ? "+" : "") + v.ToString("0.#", Loc.Culture);
        }

        public static string Rating(double v)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(Ui.RatingColor(v)) + "><b>" + v.ToString("0") + "</b></color>";
        }

        public static string Component(string c)
        {
            return Loc.T("comp." + c);
        }

        public static string Effects(Dictionary<string, double> effects)
        {
            var parts = new List<string>();
            foreach (var kv in effects)
            {
                string s = Component(kv.Key) + " " + Signed(kv.Value);
                parts.Add(kv.Value < 0 ? Theme.WarnText(s) : s);
            }
            return string.Join(", ", parts.ToArray());
        }
    }


    /// <summary>
    /// The team's riders. The overview fills one screen with no scrolling: a big card per rider on each side (team
    /// colour hero with the rating square, a key line, the rider's standout trait and the contract seasons left) and
    /// the key attributes compared in the middle, with the way to the market under them. A tap on a rider opens the
    /// detail (actions, every attribute tile, traits and contract terms); the season statistics, the bids received
    /// and a negotiation each take the whole page with a way back.
    /// </summary>
    public class RidersPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.riders"); } }
        private NegotiationState _neg;
        private string _confirmRelease;
        private string _detail;
        private bool _stats;
        private bool _bids;
        private int _pair;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            if (_neg != null)
            {
                NegotiationPanel.Build(Page(content), Game, _neg, Shell.Refresh, () => _neg = null);
                return;
            }

            var riders = new List<Rider>();
            foreach (var id in team.RiderIds) { var r = world.GetRider(id); if (r != null) riders.Add(r); }
            int bids = world.TransferOffers.FindAll(o => o.FromTeamId == team.Id).Count;
            if (_bids && bids == 0) _bids = false;
            if (_bids)
            {
                var list = Page(content);
                MobileParts.BackRow(list, Loc.T("career.back"), () => { _bids = false; Shell.Refresh(); });
                NegotiationPanel.Bids(list, Game, Shell.Refresh);
                return;
            }

            var open = _detail != null ? riders.Find(x => x.Id == _detail) : null;
            if (open == null)
            {
                _detail = null;
                _stats = false;
                Overview(content, world, team, riders, bids);
                return;
            }
            if (_stats)
            {
                // Season statistics: a full page with a way back to the rider.
                var list = Page(content);
                MobileParts.BackRow(list, Loc.T("career.back"), () => { _stats = false; Shell.Refresh(); });
                StatsView(list, world, team, open);
                return;
            }
            Detail(content, world, team, open);
        }

        private void Open(Rider r)
        {
            _detail = r.Id;
            _confirmRelease = null;
            _stats = false;
            Shell.Refresh();
        }

        // ------------------------------------------------------------------ overview

        private void Overview(RectTransform content, WorldState world, Team team, List<Rider> riders, int bids)
        {
            // Seats: the riders, then the free seats; with more than two, a pager in the middle shows two at a time.
            var seats = new List<Rider>(riders);
            int free = Math.Max(0, ContractService.SeatsFreeNow(world, Game.Db, team));
            for (int i = 0; i < free; i++) seats.Add(null);
            if (seats.Count == 0) seats.Add(null);
            _pair = Mathf.Clamp(_pair, 0, Math.Max(0, seats.Count - 2));
            var a = seats[_pair];
            bool hasB = seats.Count > _pair + 1;
            var b = hasB ? seats[_pair + 1] : null;

            const float l = 0.355f, r = 0.645f;
            var left = UiFactory.Rect("Left", content);
            UiFactory.Place(left, 0, 0, l, 1);
            var mid = UiFactory.Rect("Middle", content);
            UiFactory.Place(mid, l, 0, r, 1);
            var right = UiFactory.Rect("Right", content);
            UiFactory.Place(right, r, 0, 1, 1);

            if (a != null) RiderCard(left, world, team, a);
            else FreeSeatCard(left);
            if (b != null) RiderCard(right, world, team, b);
            else if (hasB) FreeSeatCard(right);

            Compare(mid, world, team, a, b, bids, seats.Count);
        }

        /// <summary>One rider: hero, key line, standout trait and contract; the whole card opens the detail.</summary>
        private void RiderCard(RectTransform area, WorldState world, Team team, Rider r)
        {
            var rider = r;
            var body = TeamCards.Tile(area, 0, 0, 1, 1, Loc.T("rider.details"), () => Open(rider), Theme.Accent);
            TeamCards.Hero(body, 0.6f, 1, r.Number > 0 ? "#" + r.Number : null, r.FirstName, r.LastName, Nation(r), r.Attributes.Overall, Loc.T("rider.ratingTag"));
            TeamCards.KeyLine(body, 0.5f, 0.6f, KeyLine(world, team, r));
            string icon, title, line;
            Color color;
            Trait(world, team, r, out icon, out title, out line, out color);
            TeamCards.Highlight(body, 0.26f, 0.5f, icon, title, line, color);
            ContractBlock(body, 0, 0.26f, world, r);
        }

        private static string Nation(Rider r)
        {
            return string.IsNullOrEmpty(r.Nationality) ? "" : r.Nationality.ToUpperInvariant();
        }

        private string KeyLine(WorldState world, Team team, Rider r)
        {
            return TeamCards.Key(Loc.T("rider.ageCap"), r.AgeOn(world.CurrentDate).ToString()) + "     " +
                   TeamCards.Key(Loc.T("rider.morale"), Fmt.Rating(r.Morale)) + "     " +
                   TeamCards.Key(Loc.T("rider.potentialShort"), Fmt.Attr(world, team.Id, r, "Potential"));
        }

        /// <summary>
        /// What stands out: an injury first, then the riding style (with what it does in a race), or else the best
        /// key attribute.
        /// </summary>
        private static void Trait(WorldState world, Team team, Rider r, out string icon, out string title, out string line, out Color color)
        {
            if (r.InjuryRoundsOut > 0)
            {
                icon = UiIcons.Person;
                title = Loc.T("rider.injuredTitle");
                line = Loc.T("home.injured", r.InjuryRoundsOut);
                color = Theme.Bad;
                return;
            }
            foreach (var s in r.Styles)
            {
                if (s == RidingStyle.None) continue;
                icon = UiIcons.Star;
                title = Loc.T("style." + s);
                line = Loc.T("styleinfo." + s);
                color = Theme.Timing;
                return;
            }
            string best = MobileParts.KeyAttributes[0];
            double top = -1;
            foreach (var a in MobileParts.KeyAttributes)
            {
                double v = Fmt.Estimate(world, team.Id, r, a);
                if (v > top) { top = v; best = a; }
            }
            icon = UiIcons.Trophy;
            title = Loc.T("rider.strength");
            line = Loc.T("attr." + best) + "  " + Fmt.Rating(top);
            color = Theme.Timing;
        }

        /// <summary>"CONTRACT — N seasons left", the segmented bar and the end and salary under it.</summary>
        private static void ContractBlock(RectTransform body, float y0, float y1, WorldState world, Rider r)
        {
            var area = UiFactory.Rect("Contract", body);
            UiFactory.Place(area, 0, y0, 1, y1);
            int left = r.Contract != null ? Math.Max(0, r.Contract.EndSeason - world.Season + 1) : 0;
            int total = r.Contract != null ? Math.Max(left, r.Contract.EndSeason - r.Contract.StartSeason + 1) : 1;
            total = Mathf.Clamp(total, 1, 6);
            var cap = TeamCards.Text(area, "Caption", Theme.Caps(Loc.T("rider.contractCap")), Theme.FontSmall, 11, TextAnchor.MiddleLeft, Theme.TextDim);
            TeamCards.Place(cap.rectTransform, 0, 0.64f, 0.42f, 0.96f, 16, 4);
            string seasons = left == 1 ? Loc.T("rider.seasonLeft") : Loc.T("rider.seasonsLeft", left);
            var val = TeamCards.Text(area, "Left", "<b>" + seasons + "</b>", Theme.FontSmall + 2, 11, TextAnchor.MiddleRight);
            TeamCards.Place(val.rectTransform, 0.42f, 0.64f, 1, 0.96f, 4, 16);
            bool ending = left <= 1 && r.FutureContract == null;
            var bar = TeamCards.Segments(area, total, Math.Min(left, total), ending ? Theme.Warning : Theme.Timing);
            UiFactory.Place(bar, 0, 0.4f, 1, 0.6f);
            bar.offsetMin = new Vector2(16, 0);
            bar.offsetMax = new Vector2(-16, 0);
            string under = r.Contract != null ? Loc.T("rider.untilLine", r.Contract.EndSeason, MathX.FormatMoney(r.Contract.SalaryPerSeason)) : "-";
            if (r.FutureContract != null) under += "   " + Theme.GoodText(Loc.T("terms.next", r.FutureContract.EndSeason));
            else if (ending) under += "   " + Theme.WarnText(Loc.T("rider.expiring"));
            var u = TeamCards.Text(area, "Under", under, Theme.FontSmall - 2, 11, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Normal);
            TeamCards.Place(u.rectTransform, 0, 0.04f, 1, 0.38f, 16, 16);
        }

        /// <summary>An empty seat: the warning and the big button to the market.</summary>
        private void FreeSeatCard(RectTransform area)
        {
            var body = TeamCards.Tile(area, 0, 0, 1, 1, Loc.T("rider.freeSeatShort"), null, Theme.Warning);
            var img = UiIcons.Icon(body, UiIcons.Person, Theme.Alpha(Theme.TextDim, 0.35f));
            UiFactory.Place(img.rectTransform, 0.25f, 0.5f, 0.75f, 0.92f);
            var t = TeamCards.Text(body, "Free", Theme.Caps(Loc.T("rider.freeSeatShort")), Theme.FontLarge, 16, TextAnchor.MiddleCenter, Theme.Warning,
                FontStyle.BoldAndItalic);
            TeamCards.Place(t.rectTransform, 0, 0.38f, 1, 0.5f, 16, 16);
            var n = TeamCards.Text(body, "Info", Loc.T("rider.freeSeatInfo"), Theme.FontSmall - 2, 11, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Normal);
            TeamCards.Place(n.rectTransform, 0, 0.24f, 1, 0.38f, 20, 20);
            TeamCards.BigButton(body, Loc.T("rider.toMarket") + "  ›", () => Shell.Show<MarketScreen>(), Theme.Accent, 0.04f, 0.03f, 0.96f, 0.22f);
        }

        /// <summary>
        /// The middle column: the key attributes of the two riders face to face (a yellow edge on the better side),
        /// then the bids and the pager when there are any, the big way to the market and the championship average.
        /// </summary>
        private void Compare(RectTransform mid, WorldState world, Team team, Rider a, Rider b, int bids, int seats)
        {
            var body = TeamCards.Tile(mid, 0, 0, 1, 1, Loc.T("rider.attributesCap"), null);
            var head = body.parent.Find("Head");
            if (head != null)
            {
                // The riders' codes at both ends of the strip, so each side reads as its card.
                var title = head.Find("Title") as RectTransform;
                if (title != null)
                {
                    var tt = title.GetComponent<Text>();
                    tt.alignment = TextAnchor.MiddleCenter;
                    title.offsetMin = new Vector2(90, 0);
                    title.offsetMax = new Vector2(-90, 0);
                }
                var tag = head.Find("Tag");
                if (tag != null) tag.gameObject.SetActive(false);
                var ca = TeamCards.Text(head, "CodeA", a != null ? a.Code : "-", Theme.FontNormal, 12, TextAnchor.MiddleLeft, Theme.Accent, FontStyle.BoldAndItalic);
                TeamCards.Place(ca.rectTransform, 0, 0, 0.3f, 1, 14, 0);
                var cb = TeamCards.Text(head, "CodeB", b != null ? b.Code : "-", Theme.FontNormal, 12, TextAnchor.MiddleRight, Theme.Accent, FontStyle.BoldAndItalic);
                TeamCards.Place(cb.rectTransform, 0.7f, 0, 1, 1, 0, 14);
            }

            bool extras = bids > 0 || seats > 2;
            float bottom = extras ? 0.41f : 0.28f;
            var keys = MobileParts.KeyAttributes;
            for (int i = 0; i < keys.Length; i++)
            {
                var row = UiFactory.Rect("Attr", body);
                float y1 = 1 - (1 - bottom) * i / keys.Length, y0 = 1 - (1 - bottom) * (i + 1) / keys.Length;
                UiFactory.Place(row, 0, y0, 1, y1);
                if (i % 2 == 0)
                {
                    var shade = UiFactory.Decor("Shade", row, new Color(1, 1, 1, 0.035f));
                    UiFactory.Stretch(shade.rectTransform);
                }
                double va = a != null ? Fmt.Estimate(world, team.Id, a, keys[i]) : -1;
                double vb = b != null ? Fmt.Estimate(world, team.Id, b, keys[i]) : -1;
                bool aBetter = va >= 0 && va > vb + 0.5, bBetter = vb >= 0 && vb > va + 0.5;
                var la = TeamCards.Text(row, "A", a != null ? Fmt.Attr(world, team.Id, a, keys[i]) : "-", Theme.FontLarge, 14, TextAnchor.MiddleLeft,
                    aBetter ? Color.white : Theme.Alpha(Theme.TextDim, 0.9f), FontStyle.BoldAndItalic);
                TeamCards.Place(la.rectTransform, 0, 0.04f, 0.27f, 0.96f, 18, 0);
                var cap = TeamCards.Text(row, "Caption", Theme.Caps(Loc.T("attr." + keys[i])), Theme.FontSmall - 4, 10, TextAnchor.MiddleCenter,
                    Color.Lerp(Theme.TextDim, Color.white, 0.25f));
                TeamCards.Place(cap.rectTransform, 0.27f, 0.04f, 0.73f, 0.96f, 2, 2);
                var lb = TeamCards.Text(row, "B", b != null ? Fmt.Attr(world, team.Id, b, keys[i]) : "-", Theme.FontLarge, 14, TextAnchor.MiddleRight,
                    bBetter ? Color.white : Theme.Alpha(Theme.TextDim, 0.9f), FontStyle.BoldAndItalic);
                TeamCards.Place(lb.rectTransform, 0.73f, 0.04f, 1, 0.96f, 0, 18);
                if (aBetter) TeamCards.Edge(row, Theme.Timing, true, 6);
                if (bBetter) TeamCards.Edge(row, Theme.Timing, false, 6);
            }

            if (extras)
            {
                bool both = bids > 0 && seats > 2;
                float split = both ? 0.5f : 1;
                if (seats > 2)
                    TeamCards.BigButton(body, Loc.T("rider.otherPair", _pair + 1, _pair + 2, seats) + "  ›", () =>
                    {
                        _pair = _pair + 2 >= seats ? 0 : _pair + 1;
                        Shell.Refresh();
                    }, Theme.PanelLight, 0.03f, 0.27f, 0.03f + 0.94f * split, 0.4f, Theme.FontSmall);
                if (bids > 0)
                    TeamCards.BigButton(body, Loc.T("rider.bids", bids) + "  ›", () => { _bids = true; Shell.Refresh(); },
                        Color.Lerp(Theme.Warning, Theme.PanelLight, 0.35f), both ? 0.5f : 0.03f, 0.27f, 0.97f, 0.4f, Theme.FontSmall);
            }
            TeamCards.BigButton(body, Loc.T("rider.signRiders") + "  ›", () => Shell.Show<MarketScreen>(), Theme.Accent, 0.03f, 0.11f, 0.97f, extras ? 0.265f : 0.275f,
                Theme.FontNormal + 2);

            // The championship average, to read the two ratings against.
            double sum = 0;
            int n = 0;
            foreach (var x in world.ActiveRidersInCategory(team.CategoryId)) { sum += x.Attributes.Overall; n++; }
            var avg = UiFactory.Rect("Average", body);
            UiFactory.Place(avg, 0, 0, 1, 0.11f);
            var lbl = TeamCards.Text(avg, "Label", Theme.Caps(Loc.T("rider.categoryAverage")), Theme.FontSmall - 2, 10, TextAnchor.MiddleRight,
                Color.Lerp(Theme.TextDim, Color.white, 0.2f));
            TeamCards.Place(lbl.rectTransform, 0, 0.05f, 0.72f, 0.95f, 10, 8);
            TeamCards.RatingSquare(avg, n > 0 ? sum / n : -1, null, new Vector2(0.72f, 0.5f), new Vector2(4, 0), 46);
            var sq = avg.Find("Rating") as RectTransform;
            if (sq != null) sq.pivot = new Vector2(0, 0.5f);
        }

        // ------------------------------------------------------------------ detail

        /// <summary>
        /// One rider: on the left the big card (hero, key line, contract seasons) with the contract actions as big
        /// buttons; on the right, scrolling, the attribute tiles, the statistics and every attribute, the traits and
        /// the contract terms.
        /// </summary>
        private void Detail(RectTransform content, WorldState world, Team team, Rider r)
        {
            var rider = r;
            var left = UiFactory.Rect("Left", content);
            UiFactory.Place(left, 0, 0, 0.4f, 1);
            var back = TeamCards.BigButton(left, "‹  " + Loc.T("rider.backToRiders"), () => { _detail = null; _confirmRelease = null; Shell.Refresh(); },
                Theme.PanelLight, 0, 1, 1, 1, Theme.FontSmall + 2);
            var brt = (RectTransform)back.transform;
            brt.offsetMin = new Vector2(5, -66);
            brt.offsetMax = new Vector2(-5, -2);
            back.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;

            var cardArea = UiFactory.Rect("Card", left);
            UiFactory.Stretch(cardArea);
            cardArea.offsetMax = new Vector2(0, -68);
            var body = TeamCards.Tile(cardArea, 0, 0, 1, 1, Loc.T("rider.profileTitle"), null, Theme.Accent);
            TeamCards.Hero(body, 0.62f, 1, r.Number > 0 ? "#" + r.Number : null, r.FirstName, r.LastName, Nation(r), r.Attributes.Overall, Loc.T("rider.ratingTag"));
            TeamCards.KeyLine(body, 0.52f, 0.62f, KeyLine(world, team, r));
            ContractBlock(body, 0.27f, 0.52f, world, r);
            Actions(body, world, team, rider);

            var right = UiFactory.Rect("Right", content);
            UiFactory.Place(right, 0.4f, 0, 1, 1);
            var list = UiFactory.ScrollList("Detail", right);
            UiFactory.Stretch((RectTransform)list.parent.parent);

            TeamCards.Section(list, Loc.T("rider.attributesTitle"));
            var keys = MobileParts.KeyAttributes;
            for (int row = 0; row < 2; row++)
            {
                var line = TeamCards.GridRow(list, UiScale.Touch(116));
                for (int k = 0; k < 3; k++)
                {
                    int idx = row * 3 + k;
                    if (idx >= keys.Length) break;
                    string attr = keys[idx];
                    double v = Fmt.Estimate(world, team.Id, r, attr);
                    TeamCards.ValueTile(line, k / 3f, 0, (k + 1) / 3f, 1, Loc.T("attr." + attr), Fmt.Attr(world, team.Id, r, attr), v,
                        () => Shell.Toast(Loc.T("attr." + attr) + ": " + Loc.T("attrinfo." + attr)));
                }
            }
            UiFactory.ButtonRow(list, 60, UiFactory.Act(Theme.Caps(Loc.T("rider.tab.stats")) + "  ›", () => { _stats = true; Shell.Refresh(); }));
            Ui.Expander(list, "rider.attrs", Loc.T("rider.allAttributes"), inner =>
            {
                var attrs = Ui.Card(inner, null);
                MobileParts.AttributeTable(attrs, world, team.Id, rider);
            }, Shell.Refresh);

            // Traits: the riding styles and what each does in a race.
            var traits = TeamCards.Card(list, Loc.T("rider.traits"));
            int styles = 0;
            foreach (var s in r.Styles)
            {
                if (s == RidingStyle.None) continue;
                styles++;
                TraitRow(traits, UiIcons.Star, Loc.T("style." + s), Loc.T("styleinfo." + s), Theme.Timing);
            }
            if (styles == 0)
            {
                string icon, title, text;
                Color color;
                Trait(world, team, r, out icon, out title, out text, out color);
                TraitRow(traits, icon, title, text, color);
            }
            else if (r.InjuryRoundsOut > 0) TraitRow(traits, UiIcons.Person, Loc.T("rider.injuredTitle"), Loc.T("home.injured", r.InjuryRoundsOut), Theme.Bad);

            // Contract: the figures as label / value rows, the whole sentence folded.
            var c = r.Contract;
            var contract = TeamCards.Card(list, Loc.T("rider.contractCap"));
            Ui.Line(contract, Loc.T("staff.salary"), c != null ? MathX.FormatMoney(c.SalaryPerSeason) : "-", true);
            Ui.Line(contract, Loc.T("rider.until"), c != null ? c.EndSeason.ToString() : "-", true);
            if (c != null)
            {
                double win = c.RaceBonusFor(1);
                if (win > 0) Ui.Line(contract, Loc.T("rider.winBonus"), MathX.FormatMoney(win));
                if (c.PointBonus > 0) Ui.Line(contract, Loc.T("rider.pointBonus"), MathX.FormatMoney(c.PointBonus));
                var release = c.Clause(ClauseType.ReleaseClause);
                Ui.Line(contract, Loc.T("rider.releaseClause"), release != null ? MathX.FormatMoney(release.Amount) : Theme.Dim(Loc.T("rider.none")));
                var option = c.Clause(ClauseType.TeamOption);
                if (option != null) Ui.Line(contract, Loc.T("terms.option"), option.Exercised ? Theme.GoodText(Loc.T("terms.optionExercised")) : Loc.T("rider.optionAvailable"));
                if (c.RiderFunding > 0) Ui.Line(contract, Loc.T("rider.funding"), MathX.FormatMoney(c.RiderFunding));
                Ui.Line(contract, Loc.T("rider.terminationCost"), MathX.FormatMoney(Contracts.TerminationCost(world, Game.Db, r)));
            }
            if (r.FutureContract != null)
                Ui.Line(contract, Loc.T("rider.nextContract"), Theme.GoodText(Loc.T("rider.untilLine", r.FutureContract.EndSeason, MathX.FormatMoney(r.FutureContract.SalaryPerSeason))));
            Ui.Note(contract, NegotiationPanel.Terms(c));
        }

        private static void TraitRow(Transform card, string icon, string title, string text, Color color)
        {
            var row = UiFactory.Row(card, 80);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = Theme.Alpha(color, 0.12f);
            bg.raycastTarget = false;
            TeamCards.Edge(row, color, true, 6);
            var img = UiIcons.Icon(row, icon, color);
            img.rectTransform.anchorMin = new Vector2(0, 0.12f);
            img.rectTransform.anchorMax = new Vector2(0, 0.88f);
            img.rectTransform.offsetMin = new Vector2(16, 0);
            img.rectTransform.offsetMax = new Vector2(76, 0);
            var t = TeamCards.Text(row, "Title", Theme.Caps(title), Theme.FontSmall + 2, 12, TextAnchor.LowerLeft, Color.white, FontStyle.BoldAndItalic);
            TeamCards.Place(t.rectTransform, 0, 0.5f, 1, 0.94f, 90, 10);
            var l = TeamCards.Text(row, "Line", text, Theme.FontSmall - 2, 11, TextAnchor.UpperLeft, Theme.TextDim, FontStyle.Normal);
            TeamCards.Place(l.rectTransform, 0, 0.06f, 1, 0.5f, 90, 10);
        }

        /// <summary>The contract actions as big buttons: negotiate, the team option and the (confirmed) release.</summary>
        private void Actions(RectTransform body, WorldState world, Team team, Rider rider)
        {
            var acts = new List<UiAct>();
            if (rider.FutureContract == null) acts.Add(UiFactory.Primary(Loc.T("team.renew"), () => { _neg = new NegotiationState { RiderId = rider.Id }; Shell.Refresh(); }));
            var option = rider.Contract != null ? rider.Contract.Clause(ClauseType.TeamOption) : null;
            if (option != null && !option.Exercised && rider.Contract.EndSeason == world.Season)
                acts.Add(UiFactory.Act(Loc.T("team.option"), () =>
                {
                    bool ok = ContractService.ExerciseOption(world, Game.Db, team, rider);
                    Shell.Toast(ok ? Loc.T("team.optionDone", rider.FullName) : Loc.T("team.optionNoSeat"), ok);
                    Shell.Refresh();
                }));
            double cost = Contracts.TerminationCost(world, Game.Db, rider);
            bool confirm = _confirmRelease == rider.Id;
            acts.Add(UiFactory.Option(Loc.T(confirm ? "team.releaseConfirm" : "team.release", MathX.FormatMoney(cost)), confirm, () =>
            {
                if (_confirmRelease == rider.Id)
                {
                    Contracts.Terminate(world, Game.Db, team, rider);
                    _confirmRelease = null;
                    _detail = null;
                }
                else _confirmRelease = rider.Id;
                Shell.Refresh();
            }));
            for (int i = 0; i < acts.Count; i++)
            {
                var act = acts[i];
                bool release = i == acts.Count - 1;
                Color color = act.Primary ? Theme.Accent : release ? (confirm ? Theme.Bad : Color.Lerp(Theme.Bad, Theme.PanelLight, 0.55f)) : Theme.PanelLight;
                TeamCards.BigButton(body, act.Key, act.Value, color, (float)i / acts.Count, 0.02f, (float)(i + 1) / acts.Count, 0.25f, Theme.FontSmall + 2);
            }
        }

        /// <summary>Results of one race session for the rider (position, or 0 when out / not started).</summary>
        private struct RaceDot
        {
            public string Label;
            public int Position;
            public bool Out;
            public int Field;
        }

        private void StatsView(RectTransform list, WorldState world, Team team, Rider r)
        {
            ChampionshipState champ;
            world.Championships.TryGetValue(r.CategoryId ?? team.CategoryId, out champ);
            StandingEntry st = null;
            if (champ != null) champ.Riders.TryGetValue(r.Id, out st);
            int pos = champ != null ? champ.RiderPosition(r.Id) : 0;

            // Every race (not sprints) of this season for the chart.
            var races = new List<RaceDot>();
            int dnfs = 0, sum = 0, finished = 0;
            if (champ != null)
                foreach (var wk in champ.Rounds)
                    foreach (var s in wk.Sessions)
                    {
                        if (s.Type != SessionType.Race) continue;
                        var e = s.Classification.Find(x => x.RiderId == r.Id);
                        if (e == null) continue;
                        bool outOf = e.Status != ResultStatus.Finished;
                        if (outOf) dnfs++;
                        else { sum += e.Position; finished++; }
                        races.Add(new RaceDot { Label = wk.Round.ToString(), Position = e.Position, Out = outOf, Field = s.Classification.Count });
                    }
            var last = races.Count > 0 ? races[races.Count - 1] : (RaceDot?)null;

            // Four big numbers, the chart on the left, the rest of the season and the career on the right.
            var top = Ui.Card(list, r.FullName, Loc.T("rider.stats.season", world.Season));
            Ui.Kpis(top,
                Ui.Kpi(pos > 0 ? "P" + pos : "-", Loc.T("rider.stats.position")),
                Ui.Kpi(st != null ? st.Points.ToString("0") : "0", Loc.T("rider.stats.points")),
                Ui.Kpi(st != null ? st.Wins.ToString() : "0", Loc.T("rider.stats.wins")),
                Ui.Kpi(st != null ? st.Podiums.ToString() : "0", Loc.T("rider.stats.podiums")));

            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);
            var chart = Ui.Card(leftCol, Loc.T("rider.stats.chart"));
            if (races.Count == 0) Ui.Note(chart, Loc.T("rider.stats.noRaces"));
            else PositionChart(chart, races);

            var season = Ui.Card(rightCol, null);
            Ui.Line(season, Loc.T("rider.stats.lastRace"), last.HasValue ? (last.Value.Out ? Theme.BadText(Loc.T("result.DNF")) : "P" + last.Value.Position) : "-");
            Ui.Line(season, Loc.T("rider.stats.average"), finished > 0 ? ((double)sum / finished).ToString("0.0", Loc.Culture) : "-");
            Ui.Line(season, Loc.T("rider.stats.dnfs"), dnfs.ToString());

            // Career: the category under the season, wins and podiums together.
            var hist = Ui.Card(rightCol, Loc.T("rider.stats.career"));
            var w = new[] { 1.6f, 3f, 0.9f, 1.5f, 1.1f };
            UiFactory.TableRow(hist, 32, w, new[] { Loc.T("col.season"), Loc.T("col.team"), "Pos", Loc.T("col.wins") + " / " + Loc.T("col.podiums"), Loc.T("col.points") }, true);
            int rows = 0;
            for (int i = r.CareerHistory.Count - 1; i >= 0 && rows < 9; i--, rows++)
            {
                var h = r.CareerHistory[i];
                var cat = Game.Db.GetCategory(h.CategoryId);
                UiFactory.TableRow(hist, 40, w, new[]
                {
                    h.Season + "\n" + Theme.Dim(cat != null ? cat.ShortName : h.CategoryId ?? ""), h.TeamName ?? "",
                    h.ChampionshipPosition > 0 ? h.ChampionshipPosition.ToString() : "-", h.Wins + " / " + h.Podiums, h.Points.ToString("0")
                });
            }
            if (r.CareerHistory.Count == 0) UiFactory.Paragraph(hist, Theme.Dim(Loc.T("rider.stats.noHistory")), Theme.FontSmall - 2);
            if (!string.IsNullOrEmpty(r.RealHistory)) Ui.Note(hist, r.RealHistory);
        }

        /// <summary>
        /// Line chart of race positions: higher is better, the points joined by a line. Podiums in gold, silver and
        /// bronze, points finishes in green, the rest in blue; retirements are grey crosses at the bottom.
        /// </summary>
        private static void PositionChart(Transform card, List<RaceDot> races)
        {
            var area = UiFactory.Row(card, 220);
            int field = 1;
            foreach (var d in races) field = Math.Max(field, d.Field);
            const float bottom = 0.14f, top = 0.9f;
            // Guide lines: P1, P5, P10, P15 (points), last.
            foreach (int guide in new[] { 1, 5, 10, 15 })
            {
                if (guide > field) continue;
                float gy = bottom + (top - bottom) * (field - guide) / Math.Max(1f, field - 1);
                var g = UiFactory.Panel("Guide", area, new Color(1, 1, 1, guide == 15 ? 0.14f : 0.06f));
                g.rectTransform.anchorMin = new Vector2(0.06f, gy);
                g.rectTransform.anchorMax = new Vector2(1f, gy);
                g.rectTransform.offsetMin = new Vector2(0, -1);
                g.rectTransform.offsetMax = new Vector2(0, 1);
                var gl = UiFactory.Label("GuideLabel", area, "P" + guide, Theme.FontSmall - 8, TextAnchor.MiddleLeft, Theme.TextDim);
                gl.rectTransform.anchorMin = new Vector2(0, gy - 0.05f);
                gl.rectTransform.anchorMax = new Vector2(0.06f, gy + 0.05f);
                gl.rectTransform.offsetMin = gl.rectTransform.offsetMax = Vector2.zero;
            }
            float wEach = 0.94f / Math.Max(races.Count, 8);
            var line = UiFactory.Rect("Line", area).gameObject.AddComponent<LineGraphic>();
            UiFactory.Stretch(line.rectTransform);
            line.color = new Color(1, 1, 1, 0.45f);
            line.raycastTarget = false;
            for (int i = 0; i < races.Count; i++)
            {
                var d = races[i];
                float x = 0.06f + wEach * (i + 0.5f);
                float y = d.Out ? bottom : bottom + (top - bottom) * (field - d.Position) / Math.Max(1f, field - 1);
                if (!d.Out) line.Points.Add(new Vector2(x, y));
                Color c = d.Out ? new Color(0.45f, 0.48f, 0.55f) : d.Position == 1 ? new Color32(240, 190, 40, 255) : d.Position == 2 ? new Color32(200, 205, 215, 255) :
                    d.Position == 3 ? new Color32(205, 125, 60, 255) : d.Position <= 15 ? Theme.Good : (Color)Theme.Steel;
                var dot = UiFactory.Panel("Dot", area, c, true);
                UiFactory.Round(dot, 4f);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(x, y);
                dot.rectTransform.sizeDelta = new Vector2(18, 18);
                var label = UiFactory.Label("Pos", area, d.Out ? "X" : d.Position.ToString(), Theme.FontSmall - 6, TextAnchor.LowerCenter, d.Out ? Theme.TextDim : Theme.Text);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(x, y);
                label.rectTransform.sizeDelta = new Vector2(40, 24);
                label.rectTransform.anchoredPosition = new Vector2(0, 20);
                var round = UiFactory.Label("Round", area, d.Label, Theme.FontSmall - 8, TextAnchor.UpperCenter, Theme.TextDim);
                round.rectTransform.anchorMin = new Vector2(x - wEach * 0.5f, 0);
                round.rectTransform.anchorMax = new Vector2(x + wEach * 0.5f, 0.1f);
                round.rectTransform.offsetMin = round.rectTransform.offsetMax = Vector2.zero;
            }
            line.SetVerticesDirty();
        }
    }

    /// <summary>
    /// Staff: one tile per role filling the screen (the role, the holder's surname and the level in big timing-yellow
    /// type; how well the role is covered is only the colour of the left edge). A tap opens the role: the holders as
    /// compact rows whose actions (other rider, renew, release) fold behind one "Options" button, and the candidates
    /// as rows with the level, salary, the gain over the current holder and one hire button each.
    /// </summary>
    public class StaffPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.staff"); } }
        private StaffRole? _role;
        private string _confirmFire;
        private string _confirmHire;
        private string _options;

        private const float StripHeight = 64;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;

            var staff = new List<StaffMember>();
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (world.Staff.TryGetValue(id, out s)) staff.Add(s);
            }
            staff.Sort((a, b) => a.Role.CompareTo(b.Role));
            if (_role.HasValue)
            {
                RoleDetail(Page(content), world, team, staff, _role.Value);
                return;
            }
            Overview(content, world, team, staff);
        }

        private static List<StaffRole> Roles()
        {
            var roles = new List<StaffRole>();
            foreach (StaffRole role in Enum.GetValues(typeof(StaffRole))) if (role != StaffRole.TeamManager) roles.Add(role);
            return roles;
        }

        private static Color CoverageColor(RoleCoverage c)
        {
            switch (c)
            {
                case RoleCoverage.Missing: return Theme.Bad;
                case RoleCoverage.Weak: return Theme.Warning;
                case RoleCoverage.Covered: return Theme.Good;
                default: return Theme.Steel;
            }
        }

        private void Close()
        {
            _role = null;
            _confirmFire = null;
            _confirmHire = null;
            _options = null;
            Shell.Refresh();
        }

        // ------------------------------------------------------------------ overview

        private void Overview(RectTransform content, WorldState world, Team team, List<StaffMember> staff)
        {
            // Top strip, one line: headcount and payroll on the left, the skill ceiling on the right (a tap explains it).
            double payroll = 0;
            foreach (var s in staff) payroll += s.Salary;
            var strip = UiFactory.Rect("Strip", content);
            strip.anchorMin = new Vector2(0, 1);
            strip.anchorMax = new Vector2(1, 1);
            strip.pivot = new Vector2(0.5f, 1);
            strip.offsetMin = new Vector2(0, -StripHeight);
            strip.offsetMax = Vector2.zero;
            var sum = UiFactory.Decor("Summary", strip, new Color(1, 1, 1, 0.05f));
            UiFactory.Place(sum.rectTransform, 0, 0, 0.6f, 1, 5);
            TeamCards.Edge(sum.transform, Theme.Accent, true, 6);
            var st = TeamCards.Text(sum.transform, "Text", Loc.T("staff.summaryShort", staff.Count, MathX.FormatMoney(payroll)),
                Theme.FontNormal, 14, TextAnchor.MiddleLeft);
            TeamCards.Place(st.rectTransform, 0, 0, 1, 1, 22, 10);
            double ceiling = StaffMarket.SkillCeiling(world, Game.Db, team);
            var ceil = UiFactory.Button("Ceiling", strip, "", () => Shell.Toast(Loc.T("staff.ceiling", ceiling.ToString("0"))), new Color(1, 1, 1, 0.05f), Theme.FontNormal);
            ceil.image.sprite = null;
            UiFactory.Place((RectTransform)ceil.transform, 0.6f, 0, 1, 1, 5);
            var cl = ceil.GetComponentInChildren<Text>();
            cl.supportRichText = true;
            cl.fontStyle = FontStyle.BoldAndItalic;
            cl.alignment = TextAnchor.MiddleRight;
            cl.resizeTextMinSize = 14;
            cl.resizeTextMaxSize = Theme.FontNormal;
            cl.text = Theme.TimingText(Theme.Caps(Loc.T("staff.ceilingTag", ceiling.ToString("0")))) + "  " + Theme.Dim("ⓘ");
            cl.rectTransform.offsetMax = new Vector2(-14, 0);

            var grid = UiFactory.Rect("Grid", content);
            UiFactory.Stretch(grid);
            grid.offsetMax = new Vector2(0, -StripHeight);
            var roles = Roles();
            int cols = 4, rows = (roles.Count + cols - 1) / cols;
            for (int i = 0; i < roles.Count; i++)
            {
                int cx = i % cols, cy = i / cols;
                RoleTile(grid, world, team, staff, roles[i], (float)cx / cols, 1 - (float)(cy + 1) / rows, (float)(cx + 1) / cols, 1 - (float)cy / rows);
            }
        }

        /// <summary>
        /// One role: the header, the holder's surname and the level in big type; the left edge says how it is covered.
        /// An empty role shows only the way to hire (dimmed when the role is optional).
        /// </summary>
        private void RoleTile(RectTransform grid, WorldState world, Team team, List<StaffMember> staff, StaffRole role, float x0, float y0, float x1, float y1)
        {
            double best;
            int count;
            var cov = StaffService.Coverage(world, Game.Db, team, role, out best, out count);
            var holders = staff.FindAll(s => s.Role == role);
            int ending = holders.FindAll(s => s.ContractEndSeason <= world.Season).Count;
            var r = role;
            var body = TeamCards.Tile(grid, x0, y0, x1, y1, Loc.T("role." + role), () =>
            {
                _role = r;
                _confirmFire = null;
                _confirmHire = null;
                _options = null;
                Shell.Refresh();
            }, null, ending, Theme.Warning, 4);
            var tile = body.parent;
            var edge = TeamCards.Edge(tile, CoverageColor(cov), true, 7);
            edge.transform.SetAsLastSibling();

            if (holders.Count == 0)
            {
                bool optional = cov == RoleCoverage.Optional;
                if (optional) tile.gameObject.AddComponent<CanvasGroup>().alpha = 0.5f;
                var hint = TeamCards.Text(body, "Hire", Theme.Caps(Loc.T("staff.hireHint")), Theme.FontNormal, 14, TextAnchor.MiddleCenter,
                    optional ? Theme.TextDim : Theme.Bad, FontStyle.BoldAndItalic);
                TeamCards.Place(hint.rectTransform, 0, 0.1f, 1, 0.9f, 16, 12);
                return;
            }

            var names = new List<string>();
            foreach (var h in holders) names.Add(h.LastName);
            var name = TeamCards.Text(body, "Who", Theme.Caps(string.Join(" / ", names.ToArray())), Theme.FontNormal, 14, TextAnchor.MiddleLeft, Theme.Text,
                FontStyle.BoldAndItalic);
            TeamCards.Place(name.rectTransform, 0, 0.08f, 0.62f, 0.92f, 18, 4);
            var lv = TeamCards.Text(body, "Level", best.ToString("0"), Theme.FontTitle, 24, TextAnchor.MiddleRight, Theme.Timing, FontStyle.BoldAndItalic);
            UiFactory.DropShadow(lv, 0.4f, 2);
            TeamCards.Place(lv.rectTransform, 0.62f, 0.04f, 1, 0.96f, 0, 14);
        }

        // ------------------------------------------------------------------ role detail

        private void RoleDetail(RectTransform list, WorldState world, Team team, List<StaffMember> staff, StaffRole role)
        {
            double current;
            int count;
            var cov = StaffService.Coverage(world, Game.Db, team, role, out current, out count);
            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);

            // Left: the role (with the way back in its header) and who holds it now; what the role does folded.
            var card = TeamCards.Card(leftCol, Loc.T("role." + role), TeamCards.Colored(Theme.Caps(Loc.T("staff.cov." + cov)), CoverageColor(cov)), null, Close);
            var holders = staff.FindAll(s => s.Role == role);
            if (holders.Count == 0)
            {
                var none = TeamCards.Text(UiFactory.Row(card, 64), "None", Theme.Caps(Loc.T("staff.nobody")), Theme.FontNormal, 14, TextAnchor.MiddleLeft,
                    cov == RoleCoverage.Missing ? Theme.Bad : Theme.TextDim, FontStyle.BoldAndItalic);
                TeamCards.Place(none.rectTransform, 0, 0, 1, 1, 8, 8);
            }
            foreach (var s in holders) Holder(card, world, team, s);
            Ui.Note(card, "<b>" + Loc.T("staff.whatFor") + "</b>  " + Loc.T("roleinfo." + role) + "\n<b>" + Loc.T("staff.impact") + "</b>  " +
                          Loc.T("roleimpact." + role) + "\n" + Loc.T("staff.advice." + cov));

            // Right: the candidates, under the skill ceiling.
            double ceiling = StaffMarket.SkillCeiling(world, Game.Db, team);
            var cands = TeamCards.Card(rightCol, Loc.T("staff.candidates"), Theme.TimingText(Theme.Caps(Loc.T("staff.ceilingTag", ceiling.ToString("0")))));
            var replaces = StaffMarket.WouldReplace(world, team, role);
            if (replaces != null)
            {
                var warn = TeamCards.Text(UiFactory.Row(cands, 44), "Replaces",
                    Loc.T("staff.replaceShort", replaces.FullName, MathX.FormatMoney(StaffMarket.SeveranceCost(world, replaces))), Theme.FontSmall, 12,
                    TextAnchor.MiddleLeft, Theme.Warning, FontStyle.Normal);
                TeamCards.Place(warn.rectTransform, 0, 0, 1, 1, 4, 4);
            }
            int shown = 0;
            foreach (var c in StaffMarket.Candidates(world, Game.Db, team, role))
            {
                if (shown++ >= 4) break;
                var cand = c;
                string gain = current >= 0 && c.Skill >= current + 0.5 ? TeamCards.Colored("<b>+" + (c.Skill - current).ToString("0") + "</b>", Theme.Good) : null;
                var row = TeamCards.PersonRow(cands, c.Skill, Loc.T("staff.skill"), c.FullName, MathX.FormatMoney(c.Salary) + " / " + Loc.T("staff.season"),
                    gain, 0.3f);
                bool confirming = _confirmHire == cand.Id && replaces != null;
                var b = TeamCards.BigButton(row, confirming
                    ? Loc.T("staff.fireConfirm", MathX.FormatMoney(StaffMarket.SeveranceCost(world, replaces)))
                    : Loc.T("staff.hireButton"), () =>
                {
                    // Replacing someone needs a second tap that shows the severance on the button.
                    var who = StaffMarket.WouldReplace(world, team, cand.Role);
                    if (who != null && _confirmHire != cand.Id)
                    {
                        _confirmHire = cand.Id;
                        Shell.Refresh();
                        return;
                    }
                    _confirmHire = null;
                    StaffMarket.Hire(world, team, cand);
                    Shell.Toast(who != null ? Loc.T("staff.hiredReplacing", cand.FullName, who.FullName) : Loc.T("staff.hired", cand.FullName, Loc.T("role." + cand.Role)));
                    Shell.Refresh();
                }, confirming ? Theme.Bad : Theme.Accent, 0.7f, 0.08f, 1, 0.92f, Theme.FontSmall + 2);
                ((RectTransform)b.transform).offsetMax = new Vector2(-8, -4);
            }
            if (shown == 0) Ui.Note(cands, Loc.T("staff.noCandidates"));
        }

        /// <summary>
        /// One holder: level square, name and one small line (rider, contract end), with a single "Options" button
        /// that unfolds the actions (other rider, renew, release with confirmation) in a row under it.
        /// </summary>
        private void Holder(Transform card, WorldState world, Team team, StaffMember s)
        {
            var member = s;
            var rider = s.AssignedRiderId != null ? world.GetRider(s.AssignedRiderId) : null;
            bool expiring = s.ContractEndSeason <= world.Season;
            string until = Loc.T("staff.until", s.ContractEndSeason);
            string detail = (rider != null ? Loc.T("staff.with", rider.LastName) + " · " : "") + (expiring ? Theme.WarnText(until) : until);
            bool open = _options == s.Id;
            var head = TeamCards.PersonRow(card, s.Skill, Loc.T("staff.skill"), s.FullName, detail, null, 0.3f);
            TeamCards.BigButton(head, open ? Loc.T("staff.closeOptions") : "···  " + Loc.T("staff.options"), () =>
            {
                _options = _options == member.Id ? null : member.Id;
                _confirmFire = null;
                Shell.Refresh();
            }, open ? Theme.PanelLight : Color.Lerp(Theme.PanelLight, Theme.Card, 0.4f), 0.7f, 0.08f, 1, 0.92f, Theme.FontSmall + 2);
            if (!open) return;

            var acts = new List<UiAct>();
            if (StaffService.PerRider(s.Role) && team.RiderIds.Count > 1)
                acts.Add(UiFactory.Act(Loc.T("staff.otherRider"), () =>
                {
                    int idx = team.RiderIds.IndexOf(member.AssignedRiderId);
                    string next = team.RiderIds[(idx + 1) % team.RiderIds.Count];
                    if (StaffService.Assign(world, team, member, next))
                    {
                        var r = world.GetRider(next);
                        Shell.Toast(Loc.T("staff.assigned", member.FullName, r != null ? r.FullName : ""));
                    }
                    Shell.Refresh();
                }));
            acts.Add(UiFactory.Primary(Loc.T("staff.renew"), () =>
            {
                StaffMarket.Extend(world, Game.Db, team, member, 2);
                Shell.Toast(Loc.T("staff.renewed", member.FullName, member.ContractEndSeason, MathX.FormatMoney(member.Salary)));
                Shell.Refresh();
            }));
            double severance = StaffMarket.SeveranceCost(world, s);
            bool confirm = _confirmFire == s.Id;
            acts.Add(UiFactory.Option(confirm ? Loc.T("staff.fireConfirm", MathX.FormatMoney(severance)) : Loc.T("staff.fire"), confirm, () =>
            {
                if (_confirmFire == member.Id)
                {
                    StaffMarket.Fire(world, team, member.Id);
                    _confirmFire = null;
                    _options = null;
                    Shell.Toast(Loc.T("staff.released", member.FullName, MathX.FormatMoney(severance)));
                }
                else _confirmFire = member.Id;
                Shell.Refresh();
            }));
            var row = UiFactory.Row(card, UiScale.Touch(64));
            for (int i = 0; i < acts.Count; i++)
            {
                var act = acts[i];
                bool fire = i == acts.Count - 1;
                Color color = act.Primary ? Theme.Accent : fire ? (confirm ? Theme.Bad : Color.Lerp(Theme.Bad, Theme.PanelLight, 0.55f)) : Theme.PanelLight;
                TeamCards.BigButton(row, act.Key, act.Value, color, (float)i / acts.Count, 0, (float)(i + 1) / acts.Count, 1, Theme.FontSmall);
            }
        }
    }

    /// <summary>
    /// The manager in the same tile language: the hero card (name, reputation square, wealth and contract), the
    /// skills as tiles, the season objective and the team's structure.
    /// </summary>
    public class ManagerPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.manager"); } }

        public override void Build(RectTransform content)
        {
            var page = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            var me = world.PlayerManager;
            RectTransform list, rightCol;
            Ui.Columns(page, out list, out rightCol);
            if (me != null)
            {
                var card = TeamCards.Card(list, Loc.T("manager.subtitle"));
                var hero = UiFactory.Row(card, 150);
                bool owner = me.Job != null && me.Job.Role == ManagerRole.OwnerManager;
                TeamCards.Hero(hero, 0, 1, null, me.FirstName, me.LastName, string.IsNullOrEmpty(me.Nationality) ? "" : me.Nationality.ToUpperInvariant(),
                    me.Reputation, Loc.T("manager.repTag"));
                var keys = UiFactory.Row(card, 52);
                var bg = keys.gameObject.AddComponent<Image>();
                bg.color = new Color(0, 0, 0, 0.45f);
                bg.raycastTarget = false;
                string status = owner ? TeamCards.Key(Loc.T("manager.status"), Loc.T("manager.ownerKpi"))
                    : TeamCards.Key(Loc.T("manager.until"), me.Job != null ? me.Job.EndSeason.ToString() : "-");
                var kt = TeamCards.Text(keys, "Keys", TeamCards.Key(Loc.T("manager.wealthShort"), MathX.FormatMoney(me.PersonalWealth)) + "     " + status,
                    Theme.FontSmall + 2, 11, TextAnchor.MiddleCenter);
                TeamCards.Place(kt.rectTransform, 0, 0, 1, 1, 12, 12);
                if (me.Job != null)
                {
                    // The salary in plain sight when employed; the full sentence (employer, autonomy) folded.
                    var org = world.GetOrganisation(me.Job.OrganisationId);
                    if (!owner) Ui.Line(card, Loc.T("staff.salary"), MathX.FormatMoney(me.Job.SalaryPerSeason), true);
                    string role = owner ? Loc.T("manager.owner", org != null ? org.Name : "") :
                        Loc.T("manager.employed", org != null ? org.Name : "", me.Job.EndSeason, MathX.FormatMoney(me.Job.SalaryPerSeason), Loc.T("autonomy." + me.Job.Autonomy));
                    Ui.Note(card, role);
                }

                // Skills: one tile each, three to a row.
                TeamCards.Section(list, Loc.T("mycareer.skills"));
                var skills = new[]
                {
                    new KeyValuePair<string, double>("mycareer.skill.negotiation", me.Negotiation),
                    new KeyValuePair<string, double>("mycareer.skill.sporting", me.SportingManagement),
                    new KeyValuePair<string, double>("mycareer.skill.finance", me.Finance),
                    new KeyValuePair<string, double>("mycareer.skill.relations", me.Relations),
                    new KeyValuePair<string, double>("mycareer.skill.commercial", me.Commercial)
                };
                for (int i = 0; i < skills.Length; i += 3)
                {
                    var row = TeamCards.GridRow(list, UiScale.Touch(110));
                    for (int k = 0; k < 3 && i + k < skills.Length; k++)
                    {
                        var sk = skills[i + k];
                        TeamCards.ValueTile(row, k / 3f, 0, (k + 1) / 3f, 1, Loc.T(sk.Key), sk.Value.ToString("0"), sk.Value, null);
                    }
                }
            }
            if (team == null) return;
            var pers = team.Personality;
            if (pers != null)
            {
                int teams = world.TeamsInCategory(team.CategoryId).Count;
                var card = TeamCards.Card(rightCol, Loc.T("objective.title"));
                var big = TeamCards.Text(UiFactory.Row(card, 64), "Objective", Theme.Caps(Loc.T("objective." + pers.Objective)), Theme.FontLarge, 14,
                    TextAnchor.MiddleLeft, Theme.Timing, FontStyle.BoldAndItalic);
                TeamCards.Place(big.rectTransform, 0, 0, 1, 1, 6, 6);
                Ui.Line(card, Loc.T("objective.current"), Loc.T("objective.target", TeamAI.TargetPosition(pers.Objective, teams)));
                if (pers.LastPosition > 0)
                    Ui.Line(card, Loc.T("objective.last", Loc.T("objective." + pers.LastObjective), pers.LastPosition),
                        pers.LastObjectiveMet ? Theme.GoodText(Loc.T("objective.met")) : Theme.BadText(Loc.T("objective.missed")));
            }
            var orgCard = TeamCards.Card(rightCol, Loc.T("manager.structure"));
            var o = world.OrganisationOf(team);
            Ui.Line(orgCard, Loc.T("manager.team"), team.Name, true);
            if (o != null)
            {
                Ui.Line(orgCard, Loc.T("manager.organisation"), o.Name);
                Ui.Line(orgCard, Loc.T("finance.owner"), Loc.T("owner." + o.Owner));
            }
            var cat = Game.Db.GetCategory(team.CategoryId);
            Ui.Line(orgCard, Loc.T("manager.category"), cat != null ? cat.Name : team.CategoryId);
            Ui.Line(orgCard, Loc.T("manager.teamReputation"), Fmt.Rating(team.Reputation));
        }
    }
}
