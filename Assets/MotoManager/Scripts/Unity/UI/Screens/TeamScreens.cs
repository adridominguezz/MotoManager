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
    /// Small layout helpers of the hub, team, market and negotiation pages (UX WP2) on top of the design system.
    /// Kept here (not in Components.cs) because they are only used by these pages.
    /// </summary>
    internal static class Wp2Ui
    {
        /// <summary>An action that opens a sheet with a title and one paragraph per string (for onInfo arguments).</summary>
        public static Action Info(string title, params string[] paragraphs)
        {
            return () => Ui.Sheet(title, list =>
            {
                foreach (var p in paragraphs)
                    if (!string.IsNullOrEmpty(p)) UiFactory.Paragraph(list, p, Theme.TextBody);
            });
        }

        /// <summary>Turns an anchor-layout rect into a vertical list (rows added top to bottom, 8 apart).</summary>
        public static RectTransform ListIn(RectTransform area, float spacing = 8)
        {
            var v = area.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return area;
        }

        /// <summary>A strip of fixed height along the bottom of <paramref name="area"/>, <paramref name="lift"/> units up.</summary>
        public static RectTransform BottomStrip(RectTransform area, float height, float lift = 0)
        {
            var rt = UiFactory.Rect("Bottom", area);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(0, lift);
            rt.offsetMax = new Vector2(0, lift + height);
            return rt;
        }

        /// <summary>The rest of <paramref name="area"/> above <paramref name="bottom"/> units (and below <paramref name="top"/>).</summary>
        public static RectTransform Above(RectTransform area, float bottom, float top = 0)
        {
            var rt = UiFactory.Rect("Upper", area);
            UiFactory.Stretch(rt);
            rt.offsetMin = new Vector2(0, bottom);
            rt.offsetMax = new Vector2(0, -top);
            return rt;
        }

        /// <summary>A right-pointing chevron at the end of a button (its label makes room).</summary>
        public static void Chevron(Button b)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t == null) return;
            t.rectTransform.offsetMax = new Vector2(-44, t.rectTransform.offsetMax.y);
            var ch = UiIcons.Chevron(b.transform, t.color);
            ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(1, 0.5f);
            ch.rectTransform.sizeDelta = new Vector2(22, 22);
            ch.rectTransform.anchoredPosition = new Vector2(-26, 0);
        }

        /// <summary>
        /// A tile whose header strip carries a back chevron (detail views): the tile surface, the strip (title, right
        /// text, info) and the padded body, which is returned.
        /// </summary>
        public static RectTransform BackTile(RectTransform area, string title, Action onBack, Action onInfo = null, string right = null)
        {
            var body = Ui.Tile(area, null);
            Ui.HeaderStrip(body.parent, title, right, onInfo, onBack);
            body.offsetMax = new Vector2(-16, -Ui.Grid.Header - 12);
            return body;
        }

        /// <summary>A "..." button at the right end of a tile's header strip (the right text moves left).</summary>
        public static Button HeaderMore(RectTransform tileBody, Action onTap)
        {
            var head = tileBody.parent.Find("Head");
            if (head == null) return null;
            var right = head.Find("Right") as RectTransform;
            if (right != null) right.offsetMax -= new Vector2(Ui.Grid.Hit, 0);
            else
            {
                var title = head.Find("Title") as RectTransform;
                if (title != null) title.offsetMax -= new Vector2(Ui.Grid.Hit, 0);
            }
            var b = Ui.IconButton(head, Glyph.More, onTap);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-2, 0);
            return b;
        }

        /// <summary>A pill (32 high) centred in a 56-high transparent touch area that fills <paramref name="area"/>.</summary>
        public static Button TapPill(RectTransform area, string text, Color fill, Action onTap)
        {
            var hit = UiFactory.Panel("PillButton", area, new Color(1, 1, 1, 0));
            UiFactory.Stretch(hit.rectTransform);
            var b = hit.gameObject.AddComponent<Button>();
            var pill = Ui.Pill(hit.transform, text, fill);
            pill.rectTransform.anchorMin = pill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            pill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            b.targetGraphic = pill;
            b.onClick.AddListener(() =>
            {
                GameAudio.ClickSound();
                onTap();
            });
            return b;
        }

        /// <summary>A caption on the left and a segmented control on the rest of a 56-unit row.</summary>
        public static void CaptionedSegmented(Transform list, string caption, IList<string> labels, int selected, Action<int> onSelect)
        {
            var row = UiFactory.Row(list, Ui.Grid.Hit);
            var c = Ui.Label(row, Theme.Caps(caption), Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(c.rectTransform, 0, 0, 0.38f, 1);
            c.rectTransform.offsetMin = new Vector2(4, 0);
            var holder = UiFactory.Rect("Segments", row);
            UiFactory.Place(holder, 0.38f, 0, 1, 1);
            ListIn(holder, 0);
            Ui.Segmented(holder, labels, selected, onSelect);
        }

        /// <summary>A 56-unit page arrow: the back pictogram, turned to point right for "next".</summary>
        public static Button Arrow(Transform parent, bool next, Action onTap, bool enabled)
        {
            var b = Ui.IconButton(parent, UiIcons.Back, enabled ? onTap : null);
            if (next)
            {
                var plate = b.transform.Find("Plate");
                if (plate != null && plate.childCount > 0) plate.GetChild(0).localEulerAngles = new Vector3(0, 0, 180);
            }
            return b;
        }

        /// <summary>Two columns in a layout list with relative widths (e.g. 7 and 5); returns both as lists.</summary>
        public static void Split(Transform list, float leftShare, float rightShare, out RectTransform left, out RectTransform right)
        {
            var row = UiFactory.Rect("Split", list);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = Ui.Grid.Gap;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = false;
            h.childForceExpandWidth = true;
            h.childAlignment = TextAnchor.UpperLeft;
            left = Column(row, leftShare);
            right = Column(row, rightShare);
        }

        private static RectTransform Column(Transform parent, float share)
        {
            var c = UiFactory.Rect("Column", parent);
            ListIn(c, Ui.Grid.Gap);
            var le = c.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = share;
            le.preferredWidth = 0;
            le.minWidth = 0;
            return c;
        }

        /// <summary>The rider's attribute sheet: every attribute (as the team perceives it) and what the key ones do.</summary>
        public static void AttributesSheet(WorldState world, string teamId, Rider r, string extra = null)
        {
            Ui.Sheet(r.FullName, list =>
            {
                MobileParts.AttributeTable(list, world, teamId, r);
                Ui.Section(list, Loc.T("rider.attributesCap"));
                foreach (var a in MobileParts.KeyAttributes)
                    UiFactory.Paragraph(list, "<b>" + Loc.T("attr." + a) + "</b>   " + Loc.T("attrinfo." + a), Theme.TextBody);
                if (!string.IsNullOrEmpty(extra)) UiFactory.Paragraph(list, Theme.Dim(extra), Theme.TextBody);
            }, null, null, 0.7f);
        }
    }

    /// <summary>
    /// The team's riders (UX spec §3.3–§3.5). The overview fills one screen: a card per rider on each side (hero with
    /// the rating, the standout trait and the contract seasons left) and the key attributes compared in the middle,
    /// with the bids, the pager and the way to the market under them. A tap on a rider opens the detail (profile with
    /// the contract actions, attribute tiles and traits; the contract terms and every attribute in sheets); the season
    /// statistics, the bids and a negotiation each take the whole page with a back chevron.
    /// </summary>
    public class RidersPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.riders"); } }
        private NegotiationState _neg;
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
                NegotiationPanel.Bids(Page(content), Game, Shell.Refresh, () => { _bids = false; Shell.Refresh(); });
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
                StatsView(Page(content), world, team, open);
                return;
            }
            Detail(content, world, team, open);
        }

        private void Open(Rider r)
        {
            _detail = r.Id;
            _stats = false;
            Ui.Disarm();
            Shell.Refresh();
        }

        private void CloseDetail()
        {
            _detail = null;
            _stats = false;
            Ui.Disarm();
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

            var cols = Ui.Columns12(content, 4, 4, 4);
            // A free seat carries the page's only primary (the first one when both sides are free).
            bool primary = true;
            if (a != null) RiderCard(cols[0], world, team, a);
            else { FreeSeatCard(cols[0], primary); primary = false; }
            if (b != null) RiderCard(cols[2], world, team, b);
            else if (hasB) FreeSeatCard(cols[2], primary);

            Compare(cols[1], world, team, a, b, bids, seats.Count);
        }

        /// <summary>One rider: hero, standout trait and contract seasons; the whole card opens the detail.</summary>
        private void RiderCard(RectTransform area, WorldState world, Team team, Rider r)
        {
            var rider = r;
            var body = Ui.Tile(area, Loc.T("rider.details"), () => Open(rider));
            TeamCards.Hero(body, 0.56f, 1, r.Number > 0 ? "#" + r.Number : null, r.FirstName, r.LastName, Nation(r), r.Attributes.Overall, Loc.T("rider.ratingTag"));
            string icon, title, line;
            Color color;
            Trait(world, team, r, out icon, out title, out line, out color);
            TeamCards.Highlight(body, 0.29f, 0.53f, icon, title, line, color);
            ContractBlock(body, 0, 0.26f, world, r);
        }

        private static string Nation(Rider r)
        {
            return string.IsNullOrEmpty(r.Nationality) ? "" : r.Nationality.ToUpperInvariant();
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

        /// <summary>"CONTRACT — N seasons left" and the segmented bar (warning colour when it ends this season).</summary>
        private static RectTransform ContractBlock(RectTransform body, float y0, float y1, WorldState world, Rider r)
        {
            var area = UiFactory.Rect("Contract", body);
            UiFactory.Place(area, 0, y0, 1, y1);
            int left = r.Contract != null ? Math.Max(0, r.Contract.EndSeason - world.Season + 1) : 0;
            int total = r.Contract != null ? Math.Max(left, r.Contract.EndSeason - r.Contract.StartSeason + 1) : 1;
            total = Mathf.Clamp(total, 1, 6);
            bool ending = left <= 1 && r.FutureContract == null;
            var cap = Ui.Label(area, Theme.Caps(Loc.T("rider.contractCap")), Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(cap.rectTransform, 0, 0.5f, 0.4f, 1);
            string seasons = r.FutureContract != null ? Theme.GoodText(Loc.T("terms.next", r.FutureContract.EndSeason))
                : left == 1 ? Loc.T("rider.seasonLeft") : Loc.T("rider.seasonsLeft", left);
            var val = Ui.Label(area, Theme.Caps(seasons), Theme.TextBody, TextAnchor.MiddleRight, ending ? Theme.Warning : Theme.Text, FontStyle.Bold);
            UiFactory.Place(val.rectTransform, 0.4f, 0.5f, 1, 1);
            var bar = Ui.Segments(area, total, Math.Min(left, total), ending ? Theme.Warning : Theme.Timing);
            UiFactory.Place(bar, 0, 0.12f, 1, 0.36f);
            return area;
        }

        /// <summary>An empty seat: the warning and the way to the market (the primary of the page).</summary>
        private void FreeSeatCard(RectTransform area, bool primary)
        {
            var body = Ui.Tile(area, Loc.T("rider.freeSeatShort"), null, null, 0, null, null, false, Theme.Warning);
            var img = UiIcons.Icon(body, UiIcons.Person, Theme.Alpha(Theme.TextDim, 0.35f));
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.7f);
            img.rectTransform.sizeDelta = new Vector2(140, 140);
            var t = Ui.Label(body, Theme.Caps(Loc.T("rider.freeSeatShort")), Theme.TextTitle, TextAnchor.MiddleCenter, Theme.Warning, FontStyle.BoldAndItalic);
            UiFactory.Place(t.rectTransform, 0, 0.36f, 1, 0.48f);
            var n = Ui.Label(body, Loc.T("rider.freeSeatInfo"), Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim);
            UiFactory.Place(n.rectTransform, 0, 0.2f, 1, 0.36f);
            var cta = Wp2Ui.BottomStrip(body, Ui.Grid.Cta);
            Action go = () => Shell.Show<MarketScreen>();
            Wp2Ui.Chevron(primary ? Ui.PrimaryCTA(cta, Loc.T("ux.riders.toMarket"), go) : Ui.SecondaryAccent(cta, Loc.T("ux.riders.toMarket"), go));
        }

        /// <summary>
        /// The middle column: the key attributes of the two riders face to face (a yellow edge on the better side),
        /// then the bids and the pager as pills when there are any, the way to the market and the championship
        /// average.
        /// </summary>
        private void Compare(RectTransform mid, WorldState world, Team team, Rider a, Rider b, int bids, int seats)
        {
            var body = Ui.Tile(mid, Loc.T("rider.attributesCap"));
            var head = body.parent.Find("Head");
            if (head != null)
            {
                // The riders' codes at both ends of the strip, so each side reads as its card.
                var title = head.Find("Title") as RectTransform;
                if (title != null)
                {
                    title.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
                    title.offsetMin = new Vector2(90, 0);
                    title.offsetMax = new Vector2(-90, 0);
                }
                var tag = head.Find("Tag");
                if (tag != null) tag.gameObject.SetActive(false);
                var ca = Ui.Label(head, a != null ? a.Code : "-", Theme.TextHeader, TextAnchor.MiddleLeft, Color.white, FontStyle.BoldAndItalic);
                UiFactory.Place(ca.rectTransform, 0, 0, 0.3f, 1);
                ca.rectTransform.offsetMin = new Vector2(16, 0);
                var cb = Ui.Label(head, b != null ? b.Code : "-", Theme.TextHeader, TextAnchor.MiddleRight, Color.white, FontStyle.BoldAndItalic);
                UiFactory.Place(cb.rectTransform, 0.7f, 0, 1, 1);
                cb.rectTransform.offsetMax = new Vector2(-16, 0);
            }

            // From the bottom: the average (48), the market button (64) and, when needed, the pills row (56).
            bool extras = bids > 0 || seats > 2;
            float used = 48 + 8 + Ui.Grid.Button + 8 + (extras ? Ui.Grid.Hit + 4 : 0);

            var attrs = Wp2Ui.Above(body, used);
            var keys = MobileParts.KeyAttributes;
            for (int i = 0; i < keys.Length; i++)
            {
                var row = UiFactory.Rect("Attr", attrs);
                UiFactory.Place(row, 0, 1 - (i + 1f) / keys.Length, 1, 1 - (float)i / keys.Length);
                if (i % 2 == 0)
                {
                    var shade = UiFactory.Decor("Shade", row, new Color(1, 1, 1, 0.035f));
                    UiFactory.Stretch(shade.rectTransform);
                }
                double va = a != null ? Fmt.Estimate(world, team.Id, a, keys[i]) : -1;
                double vb = b != null ? Fmt.Estimate(world, team.Id, b, keys[i]) : -1;
                bool aBetter = va >= 0 && va > vb + 0.5, bBetter = vb >= 0 && vb > va + 0.5;
                var la = Ui.Label(row, a != null ? Fmt.Attr(world, team.Id, a, keys[i]) : "-", Theme.TextTitle, TextAnchor.MiddleLeft,
                    aBetter ? Color.white : Theme.Alpha(Theme.TextDim, 0.9f), FontStyle.BoldAndItalic);
                UiFactory.Place(la.rectTransform, 0, 0, 0.27f, 1);
                la.rectTransform.offsetMin = new Vector2(16, 0);
                var cap = Ui.Label(row, Theme.Caps(Loc.T("attr." + keys[i])), Theme.TextCaption, TextAnchor.MiddleCenter,
                    Color.Lerp(Theme.TextDim, Color.white, 0.25f), FontStyle.Bold);
                UiFactory.Place(cap.rectTransform, 0.27f, 0, 0.73f, 1);
                var lb = Ui.Label(row, b != null ? Fmt.Attr(world, team.Id, b, keys[i]) : "-", Theme.TextTitle, TextAnchor.MiddleRight,
                    bBetter ? Color.white : Theme.Alpha(Theme.TextDim, 0.9f), FontStyle.BoldAndItalic);
                UiFactory.Place(lb.rectTransform, 0.73f, 0, 1, 1);
                lb.rectTransform.offsetMax = new Vector2(-16, 0);
                if (aBetter) TeamCards.Edge(row, Theme.Timing, true, 6);
                if (bBetter) TeamCards.Edge(row, Theme.Timing, false, 6);
            }

            if (extras)
            {
                var pills = Wp2Ui.BottomStrip(body, Ui.Grid.Hit, 48 + 8 + Ui.Grid.Button + 8);
                bool both = bids > 0 && seats > 2;
                var pc = both ? Ui.Columns12(pills, 6, 6) : Ui.Columns12(pills, 12);
                int k = 0;
                if (bids > 0)
                    Wp2Ui.TapPill(pc[k++], Loc.T("rider.bids", bids), Theme.Warning, () => { _bids = true; Shell.Refresh(); });
                if (seats > 2)
                    Wp2Ui.TapPill(pc[k], Loc.T("rider.otherPair", _pair + 1, _pair + 2, seats), Theme.PanelLight, () =>
                    {
                        _pair = _pair + 2 >= seats ? 0 : _pair + 1;
                        Shell.Refresh();
                    });
            }

            // Browse page: the way to the market is secondary (accent outline), never the filled primary.
            var go = Wp2Ui.BottomStrip(body, Ui.Grid.Button, 48 + 8);
            Wp2Ui.Chevron(Ui.SecondaryAccent(go, Loc.T("rider.signRiders"), () => Shell.Show<MarketScreen>()));

            // The championship average, to read the two ratings against.
            double sum = 0;
            int n = 0;
            foreach (var x in world.ActiveRidersInCategory(team.CategoryId)) { sum += x.Attributes.Overall; n++; }
            var avg = Wp2Ui.BottomStrip(body, 48);
            var lbl = Ui.Label(avg, Theme.Caps(Loc.T("rider.categoryAverage")), Theme.TextCaption, TextAnchor.MiddleRight,
                Color.Lerp(Theme.TextDim, Color.white, 0.2f), FontStyle.Bold);
            UiFactory.Place(lbl.rectTransform, 0, 0, 0.74f, 1);
            lbl.rectTransform.offsetMax = new Vector2(-8, 0);
            var sq = Ui.RatingBadge(avg, n > 0 ? sum / n : -1, null, 44);
            sq.rectTransform.anchorMin = sq.rectTransform.anchorMax = new Vector2(0.74f, 0.5f);
            sq.rectTransform.pivot = new Vector2(0, 0.5f);
            sq.rectTransform.anchoredPosition = new Vector2(4, 0);
        }

        // ------------------------------------------------------------------ detail

        /// <summary>
        /// One rider without scrolling: on the left the profile (hero, age · morale · potential, the contract — a tap
        /// shows every term —, the statistics link and the contract actions); on the right the six key attributes
        /// as tiles (every attribute behind the "i") and the traits.
        /// </summary>
        private void Detail(RectTransform content, WorldState world, Team team, Rider r)
        {
            var rider = r;
            var cols = Ui.Columns12(content, 5, 7);
            var body = Wp2Ui.BackTile(cols[0], Loc.T("rider.profileTitle"), CloseDetail);
            TeamCards.Hero(body, 0.62f, 1, r.Number > 0 ? "#" + r.Number : null, r.FirstName, r.LastName, Nation(r), r.Attributes.Overall, Loc.T("rider.ratingTag"));

            string key = Loc.T("rider.age", r.AgeOn(world.CurrentDate)) + "  ·  " + Loc.T("rider.morale") + " " + Fmt.Rating(r.Morale) + "  ·  " +
                         Loc.T("rider.potentialShort") + " " + Fmt.Attr(world, team.Id, r, "Potential");
            var kl = Ui.Label(body, Theme.Caps(key), Theme.TextCaption, TextAnchor.MiddleLeft, Color.Lerp(Theme.TextDim, Color.white, 0.25f), FontStyle.Bold);
            UiFactory.Place(kl.rectTransform, 0, 0.53f, 1, 0.61f);

            // The contract block is a button: the sheet lists every term.
            var hit = UiFactory.Panel("ContractButton", body, new Color(1, 1, 1, 0.04f));
            UiFactory.Place(hit.rectTransform, 0, 0.29f, 1, 0.51f);
            var hb = hit.gameObject.AddComponent<Button>();
            hb.targetGraphic = hit;
            hb.onClick.AddListener(() =>
            {
                GameAudio.ClickSound();
                ContractSheet(world, rider);
            });
            var block = ContractBlock((RectTransform)hit.transform, 0, 1, world, r);
            block.offsetMin = new Vector2(12, 4);
            block.offsetMax = new Vector2(-40, -4);
            var ch = UiIcons.Chevron(hit.transform, Theme.TextDim);
            ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(1, 0.5f);
            ch.rectTransform.sizeDelta = new Vector2(22, 22);
            ch.rectTransform.anchoredPosition = new Vector2(-18, 0);

            var stats = UiFactory.Rect("Stats", body);
            UiFactory.Place(stats, 0.3f, 0.15f, 1, 0.27f);
            Ui.Ghost(stats, Loc.T("ux.riders.seeStats"), () => { _stats = true; Shell.Refresh(); });

            Actions(Wp2Ui.BottomStrip(body, Ui.Grid.Button), world, team, rider);

            // Right: the attribute tiles, then the traits.
            var rows = Ui.Rows(cols[1], 0.6f, 0.4f);
            var attrs = Ui.Tile(rows[0], Loc.T("rider.attributesTitle"), null, null, 0, null,
                () => Wp2Ui.AttributesSheet(world, team.Id, rider));
            var keys = MobileParts.KeyAttributes;
            var lines = Ui.Rows(attrs, 1, 1);
            for (int row = 0; row < 2; row++)
            {
                var cells = Ui.Columns12(lines[row], 4, 4, 4);
                for (int k = 0; k < 3; k++)
                {
                    int idx = row * 3 + k;
                    if (idx >= keys.Length) break;
                    double v = Fmt.Estimate(world, team.Id, r, keys[idx]);
                    Ui.StatTile(cells[k], Loc.T("attr." + keys[idx]), Fmt.Attr(world, team.Id, r, keys[idx]), null, null, Color.white, null, 0, null,
                        Ui.RatingColor(v));
                }
            }
            TraitsTile(rows[1], world, team, r);
        }

        /// <summary>The traits: at most two rows (injury first, then the riding styles); all of them in the sheet.</summary>
        private void TraitsTile(RectTransform area, WorldState world, Team team, Rider r)
        {
            var items = new List<KeyValuePair<string[], Color>>();
            if (r.InjuryRoundsOut > 0)
                items.Add(new KeyValuePair<string[], Color>(new[] { UiIcons.Person, Loc.T("rider.injuredTitle"), Loc.T("home.injured", r.InjuryRoundsOut) }, Theme.Bad));
            foreach (var s in r.Styles)
            {
                if (s == RidingStyle.None) continue;
                items.Add(new KeyValuePair<string[], Color>(new[] { UiIcons.Star, Loc.T("style." + s), Loc.T("styleinfo." + s) }, Theme.Timing));
            }
            if (items.Count == 0)
            {
                string icon, title, text;
                Color color;
                Trait(world, team, r, out icon, out title, out text, out color);
                items.Add(new KeyValuePair<string[], Color>(new[] { icon, title, text }, color));
            }
            var paragraphs = new List<string>();
            foreach (var it in items) paragraphs.Add("<b>" + Theme.Caps(it.Key[1]) + "</b>\n" + it.Key[2]);
            var body = Ui.Tile(area, Loc.T("rider.traits"), null, items.Count > 2 ? "+" + (items.Count - 2) : null, 0, null,
                Wp2Ui.Info(Loc.T("rider.traits"), paragraphs.ToArray()));
            int shown = Math.Min(2, items.Count);
            for (int i = 0; i < shown; i++)
            {
                float y1 = 1 - (float)i / 2, y0 = 1 - (i + 1f) / 2;
                TeamCards.Highlight(body, y0, y1, items[i].Key[0], items[i].Key[1], items[i].Key[2], items[i].Value);
            }
        }

        /// <summary>The contract sheet: every figure as label / value rows and the terms in one sentence.</summary>
        private void ContractSheet(WorldState world, Rider r)
        {
            var c = r.Contract;
            Ui.Sheet(Loc.T("rider.contractCap") + " · " + r.FullName, list =>
            {
                Ui.Line(list, Loc.T("staff.salary"), c != null ? MathX.FormatMoney(c.SalaryPerSeason) : "-", true);
                Ui.Line(list, Loc.T("rider.until"), c != null ? c.EndSeason.ToString() : "-", true);
                if (c != null)
                {
                    double win = c.RaceBonusFor(1);
                    if (win > 0) Ui.Line(list, Loc.T("rider.winBonus"), MathX.FormatMoney(win));
                    if (c.PointBonus > 0) Ui.Line(list, Loc.T("rider.pointBonus"), MathX.FormatMoney(c.PointBonus));
                    var release = c.Clause(ClauseType.ReleaseClause);
                    Ui.Line(list, Loc.T("rider.releaseClause"), release != null ? MathX.FormatMoney(release.Amount) : Theme.Dim(Loc.T("rider.none")));
                    var option = c.Clause(ClauseType.TeamOption);
                    if (option != null) Ui.Line(list, Loc.T("terms.option"), option.Exercised ? Theme.GoodText(Loc.T("terms.optionExercised")) : Loc.T("rider.optionAvailable"));
                    if (c.RiderFunding > 0) Ui.Line(list, Loc.T("rider.funding"), MathX.FormatMoney(c.RiderFunding));
                    Ui.Line(list, Loc.T("rider.terminationCost"), MathX.FormatMoney(Contracts.TerminationCost(world, Game.Db, r)));
                }
                if (r.FutureContract != null)
                    Ui.Line(list, Loc.T("rider.nextContract"), Theme.GoodText(Loc.T("rider.untilLine", r.FutureContract.EndSeason, MathX.FormatMoney(r.FutureContract.SalaryPerSeason))));
                UiFactory.Paragraph(list, Theme.Dim(NegotiationPanel.Terms(c)), Theme.TextCaption);
            });
        }

        /// <summary>
        /// The contract actions on one row: the team option (secondary, when it can be exercised), the release (two-tap
        /// danger showing the cost) and the renewal (the page's primary, right).
        /// </summary>
        private void Actions(RectTransform row, WorldState world, Team team, Rider rider)
        {
            var option = rider.Contract != null ? rider.Contract.Clause(ClauseType.TeamOption) : null;
            bool canOption = option != null && !option.Exercised && rider.Contract.EndSeason == world.Season;
            bool canRenew = rider.FutureContract == null;
            var spans = new List<int>();
            if (canOption) spans.Add(4);
            spans.Add(canOption ? 4 : 6);
            if (canRenew) spans.Add(canOption ? 4 : 6);
            var cols = Ui.Columns12(row, spans.ToArray());
            int k = 0;
            if (canOption)
                Ui.Secondary(cols[k++], Loc.T("team.option"), () =>
                {
                    bool ok = ContractService.ExerciseOption(world, Game.Db, team, rider);
                    Shell.Toast(ok ? Loc.T("team.optionDone", rider.FullName) : Loc.T("team.optionNoSeat"), ok);
                    Shell.Refresh();
                });
            string cost = MathX.FormatMoney(Contracts.TerminationCost(world, Game.Db, rider));
            Ui.Danger(cols[k++], Loc.T("team.release", cost), Loc.T("team.releaseConfirm", cost), "rider.release." + rider.Id, () =>
            {
                Contracts.Terminate(world, Game.Db, team, rider);
                _detail = null;
                Shell.Refresh();
            }, null);
            if (canRenew)
                Ui.PrimaryCTA(cols[k], Loc.T("team.renew"), () => { _neg = new NegotiationState { RiderId = rider.Id }; Shell.Refresh(); });
        }

        // ------------------------------------------------------------------ statistics

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

            // Four big numbers, the chart on the left, the season and the career on the right.
            var top = Ui.Card(list, r.FullName, Theme.Caps(Loc.T("rider.stats.season", world.Season)), null, () => { _stats = false; Shell.Refresh(); });
            Ui.Kpis(top,
                Ui.Kpi(Theme.Pos(pos), Loc.T("rider.stats.position")),
                Ui.Kpi(st != null ? st.Points.ToString("0") : "0", Loc.T("rider.stats.points")),
                Ui.Kpi(st != null ? st.Wins.ToString() : "0", Loc.T("rider.stats.wins")),
                Ui.Kpi(st != null ? st.Podiums.ToString() : "0", Loc.T("rider.stats.podiums")));

            RectTransform leftCol, rightCol;
            Wp2Ui.Split(list, 7, 5, out leftCol, out rightCol);
            var chart = Ui.Card(leftCol, Loc.T("rider.stats.chart"));
            if (races.Count == 0) Ui.Empty(chart, null, Loc.T("rider.stats.noRaces"));
            else PositionChart(chart, races);

            var season = Ui.Card(rightCol, Loc.T("rider.stats.season", world.Season));
            Ui.Line(season, Loc.T("rider.stats.lastRace"), last.HasValue ? (last.Value.Out ? Theme.BadText(Loc.T("result.DNF")) : Theme.Pos(last.Value.Position)) : "-");
            Ui.Line(season, Loc.T("rider.stats.average"), finished > 0 ? ((double)sum / finished).ToString("0.0", Loc.Culture) : "-");
            Ui.Line(season, Loc.T("rider.stats.dnfs"), dnfs.ToString());

            // Career: season (and category), team, position with wins and podiums, points.
            var hist = Ui.Card(rightCol, Loc.T("rider.stats.career"), null,
                string.IsNullOrEmpty(r.RealHistory) ? null : Wp2Ui.Info(Loc.T("rider.stats.career"), r.RealHistory));
            var w = new[] { 1.5f, 2.6f, 2f, 1f };
            Ui.ListCaptions(hist, w, new[] { Loc.T("col.season"), Loc.T("col.team"), "Pos", Loc.T("col.points") });
            int rows = 0;
            for (int i = r.CareerHistory.Count - 1; i >= 0 && rows < 9; i--, rows++)
            {
                var h = r.CareerHistory[i];
                var cat = Game.Db.GetCategory(h.CategoryId);
                UiFactory.TableRow(hist, 48, w, new[]
                {
                    h.Season + "\n" + Theme.Dim("<size=" + Theme.TextMin + ">" + (cat != null ? cat.ShortName : h.CategoryId ?? "") + "</size>"),
                    h.TeamName ?? "",
                    (h.ChampionshipPosition > 0 ? Theme.Pos(h.ChampionshipPosition) : "-") + "\n" +
                    Theme.Dim("<size=" + Theme.TextMin + ">" + Loc.T("ux.stats.winsPodiums", h.Wins, h.Podiums) + "</size>"),
                    h.Points.ToString("0")
                });
            }
            if (r.CareerHistory.Count == 0) Ui.Empty(hist, null, Loc.T("rider.stats.noHistory"));
        }

        /// <summary>
        /// Line chart of race positions: higher is better, the points joined by a line. Podiums in gold, silver and
        /// bronze, points finishes in green, the rest in grey; retirements are crosses at the bottom.
        /// </summary>
        private static void PositionChart(Transform card, List<RaceDot> races)
        {
            var area = UiFactory.Row(card, 260);
            int field = 1;
            foreach (var d in races) field = Math.Max(field, d.Field);
            const float bottom = 0.14f, top = 0.88f;
            // Guide lines: P1, P5, P10, P15 (points), last.
            foreach (int guide in new[] { 1, 5, 10, 15 })
            {
                if (guide > field) continue;
                float gy = bottom + (top - bottom) * (field - guide) / Math.Max(1f, field - 1);
                var g = UiFactory.Panel("Guide", area, new Color(1, 1, 1, guide == 15 ? 0.14f : 0.06f));
                g.raycastTarget = false;
                g.rectTransform.anchorMin = new Vector2(0.07f, gy);
                g.rectTransform.anchorMax = new Vector2(1f, gy);
                g.rectTransform.offsetMin = new Vector2(0, -1);
                g.rectTransform.offsetMax = new Vector2(0, 1);
                var gl = UiFactory.Label("GuideLabel", area, "P" + guide, Theme.TextMin, TextAnchor.MiddleLeft, Theme.TextDim);
                gl.rectTransform.anchorMin = new Vector2(0, gy - 0.06f);
                gl.rectTransform.anchorMax = new Vector2(0.07f, gy + 0.06f);
                gl.rectTransform.offsetMin = gl.rectTransform.offsetMax = Vector2.zero;
                gl.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            float wEach = 0.93f / Math.Max(races.Count, 8);
            var line = UiFactory.Rect("Line", area).gameObject.AddComponent<LineGraphic>();
            UiFactory.Stretch(line.rectTransform);
            line.color = new Color(1, 1, 1, 0.45f);
            line.raycastTarget = false;
            for (int i = 0; i < races.Count; i++)
            {
                var d = races[i];
                float x = 0.07f + wEach * (i + 0.5f);
                float y = d.Out ? bottom : bottom + (top - bottom) * (field - d.Position) / Math.Max(1f, field - 1);
                if (!d.Out) line.Points.Add(new Vector2(x, y));
                Color c = d.Out ? new Color(0.45f, 0.48f, 0.55f) : d.Position == 1 ? new Color32(240, 190, 40, 255) : d.Position == 2 ? new Color32(200, 205, 215, 255) :
                    d.Position == 3 ? new Color32(205, 125, 60, 255) : d.Position <= 15 ? Theme.Good : (Color)Theme.Steel;
                var dot = UiFactory.Panel("Dot", area, c, true);
                dot.raycastTarget = false;
                UiFactory.Round(dot, 4f);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(x, y);
                dot.rectTransform.sizeDelta = new Vector2(18, 18);
                var label = UiFactory.Label("Pos", area, d.Out ? "X" : d.Position.ToString(), Theme.TextMin, TextAnchor.LowerCenter, d.Out ? Theme.TextDim : Theme.Text);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(x, y);
                label.rectTransform.sizeDelta = new Vector2(44, 26);
                label.rectTransform.anchoredPosition = new Vector2(0, 22);
                var round = UiFactory.Label("Round", area, d.Label, Theme.TextMin, TextAnchor.UpperCenter, Theme.TextDim);
                round.horizontalOverflow = HorizontalWrapMode.Overflow;
                round.rectTransform.anchorMin = new Vector2(x - wEach * 0.5f, 0);
                round.rectTransform.anchorMax = new Vector2(x + wEach * 0.5f, 0.1f);
                round.rectTransform.offsetMin = round.rectTransform.offsetMax = Vector2.zero;
            }
            line.SetVerticesDirty();
        }
    }

    /// <summary>
    /// Staff (UX spec §3.7–§3.8): one tile per role filling the screen (the holder's surname and the level in big
    /// timing-yellow type; how well the role is covered is the colour of the left edge), under a strip with the
    /// headcount, the payroll and the hiring ceiling. A tap opens the role: the holders as rows (a tap opens their
    /// actions in a sheet) and the candidates with one secondary hire button each.
    /// </summary>
    public class StaffPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.staff"); } }
        private StaffRole? _role;

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
                RoleDetail(content, world, team, staff, _role.Value);
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
            Ui.Disarm();
            Shell.Refresh();
        }

        // ------------------------------------------------------------------ overview

        private void Overview(RectTransform content, WorldState world, Team team, List<StaffMember> staff)
        {
            // Top strip: headcount and payroll on the left, the hiring ceiling (explained behind "i") on the right.
            double payroll = 0;
            foreach (var s in staff) payroll += s.Salary;
            var band = UiFactory.Rect("Band", content);
            band.anchorMin = new Vector2(0, 1);
            band.anchorMax = new Vector2(1, 1);
            band.pivot = new Vector2(0.5f, 1);
            band.offsetMin = new Vector2(0, -Ui.Grid.Header);
            band.offsetMax = Vector2.zero;
            var bc = Ui.Columns12(band, 8, 4);
            Ui.HeaderStrip(bc[0], Loc.T("staff.summaryShort", staff.Count, MathX.FormatMoney(payroll)));
            double ceiling = StaffMarket.SkillCeiling(world, Game.Db, team);
            var ceil = Ui.HeaderStrip(bc[1], Loc.T("ux.staff.ceiling"), Theme.TimingText("<size=" + Theme.TextHeader + ">" + ceiling.ToString("0") + "</size>"));
            Ui.InfoButton(ceil, Loc.T("ux.staff.ceiling"), Loc.T("staff.ceiling", ceiling.ToString("0")));

            var grid = Wp2Ui.Above(content, 0, Ui.Grid.Header + Ui.Grid.Gap);
            var roles = Roles();
            const int cols = 4;
            int rows = (roles.Count + cols - 1) / cols;
            var shares = new float[rows];
            for (int i = 0; i < rows; i++) shares[i] = 1;
            var lines = Ui.Rows(grid, shares);
            for (int y = 0; y < rows; y++)
            {
                var cells = Ui.Columns12(lines[y], 3, 3, 3, 3);
                for (int x = 0; x < cols; x++)
                {
                    int i = y * cols + x;
                    if (i < roles.Count) RoleTile(cells[x], world, team, staff, roles[i]);
                }
            }
        }

        /// <summary>
        /// One role: the holder's surname and the level in big type; the left edge says how it is covered, the badge
        /// how many contracts end this season. An empty role shows only the way to hire (dimmed when optional).
        /// </summary>
        private void RoleTile(RectTransform area, WorldState world, Team team, List<StaffMember> staff, StaffRole role)
        {
            double best;
            int count;
            var cov = StaffService.Coverage(world, Game.Db, team, role, out best, out count);
            var holders = staff.FindAll(s => s.Role == role);
            int ending = holders.FindAll(s => s.ContractEndSeason <= world.Season).Count;
            var r = role;
            bool optionalEmpty = holders.Count == 0 && cov == RoleCoverage.Optional;
            var body = Ui.Tile(area, Loc.T("role." + role), () =>
            {
                _role = r;
                Ui.Disarm();
                Shell.Refresh();
            }, null, ending, Theme.Warning, null, optionalEmpty, CoverageColor(cov));

            if (holders.Count == 0)
            {
                var hint = Ui.Label(body, Theme.Caps(Loc.T("staff.hireHint")), Theme.TextHeader, TextAnchor.MiddleCenter,
                    optionalEmpty ? Theme.TextDim : Theme.Bad, FontStyle.BoldAndItalic);
                UiFactory.Stretch(hint.rectTransform);
                return;
            }

            var names = new List<string>();
            foreach (var h in holders) names.Add(h.LastName);
            var name = Ui.Label(body, Theme.Caps(string.Join(" / ", names.ToArray())), Theme.TextHeader, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Place(name.rectTransform, 0, 0, 0.64f, 1);
            var lv = Ui.Label(body, best.ToString("0"), Theme.TextKey, TextAnchor.MiddleRight, Theme.Timing, FontStyle.BoldAndItalic);
            UiFactory.DropShadow(lv, 0.4f, 2);
            UiFactory.Place(lv.rectTransform, 0.64f, 0, 1, 1);
        }

        // ------------------------------------------------------------------ role detail

        private void RoleDetail(RectTransform content, WorldState world, Team team, List<StaffMember> staff, StaffRole role)
        {
            double current;
            int count;
            var cov = StaffService.Coverage(world, Game.Db, team, role, out current, out count);
            var cols = Ui.Columns12(content, 5, 7);

            // Left: the role (back chevron, coverage, what it does behind "i") and who holds it now.
            var left = Wp2Ui.BackTile(cols[0], Loc.T("role." + role), Close,
                Wp2Ui.Info(Loc.T("role." + role),
                    "<b>" + Loc.T("staff.whatFor") + "</b>  " + Loc.T("roleinfo." + role),
                    "<b>" + Loc.T("staff.impact") + "</b>  " + Loc.T("roleimpact." + role),
                    Loc.T("staff.advice." + cov)),
                TeamCards.Colored(Theme.Caps(Loc.T("staff.cov." + cov)), CoverageColor(cov)));
            Wp2Ui.ListIn(left);
            var holders = staff.FindAll(s => s.Role == role);
            if (holders.Count == 0) Ui.Empty(left, UiIcons.Person, Loc.T("staff.nobody"));
            foreach (var s in holders) Holder(left, world, team, s);

            // Right: the candidates, under the skill ceiling.
            double ceiling = StaffMarket.SkillCeiling(world, Game.Db, team);
            var cands = Ui.Tile(cols[1], Loc.T("staff.candidates"), null,
                Theme.TimingText(Theme.Caps(Loc.T("staff.ceilingTag", ceiling.ToString("0")))));
            Wp2Ui.ListIn(cands);
            var replaces = StaffMarket.WouldReplace(world, team, role);
            if (replaces != null)
                Ui.Alert(cands, Loc.T("staff.replaceShort", replaces.FullName, MathX.FormatMoney(StaffMarket.SeveranceCost(world, replaces))));
            int shown = 0;
            foreach (var c in StaffMarket.Candidates(world, Game.Db, team, role))
            {
                if (shown >= (replaces != null ? 5 : 6)) break;
                shown++;
                var cand = c;
                string detail = (string.IsNullOrEmpty(c.Nationality) ? "" : c.Nationality.ToUpperInvariant() + " · ") + Loc.T("staff.exp") + " " + c.Experience;
                if (current >= 0 && c.Skill >= current + 0.5)
                    detail += "  " + Theme.GoodText("+" + (c.Skill - current).ToString("0") + " " + Loc.T("staff.vsCurrent"));
                else if (current >= 0) detail += "  " + Loc.T("staff.noBetter");
                Ui.PersonRow(cands, c.Skill, c.FullName, detail, MathX.FormatMoney(c.Salary) + "/" + Loc.T("staff.season"), null,
                    UiFactory.Primary(Loc.T("staff.hireButton"), () => Hire(world, team, cand)));
            }
            if (shown == 0) Ui.Empty(cands, UiIcons.Person, Loc.T("staff.noCandidates"));
        }

        /// <summary>Hires a candidate; when that replaces someone, a sheet confirms it first (with the severance).</summary>
        private void Hire(WorldState world, Team team, StaffMember cand)
        {
            var who = StaffMarket.WouldReplace(world, team, cand.Role);
            Action hire = () =>
            {
                StaffMarket.Hire(world, team, cand);
                Shell.Toast(who != null ? Loc.T("staff.hiredReplacing", cand.FullName, who.FullName) : Loc.T("staff.hired", cand.FullName, Loc.T("role." + cand.Role)));
                Shell.Refresh();
            };
            if (who == null)
            {
                hire();
                return;
            }
            Ui.Sheet(cand.FullName, list =>
            {
                Ui.Alert(list, Loc.T("staff.replaceShort", who.FullName, MathX.FormatMoney(StaffMarket.SeveranceCost(world, who))));
                Ui.Line(list, Loc.T("staff.skill"), cand.Skill.ToString("0") + "  " + Theme.Dim("(" + who.LastName + " " + who.Skill.ToString("0") + ")"));
                Ui.Line(list, Loc.T("staff.salary"), MathX.FormatMoney(cand.Salary) + "/" + Loc.T("staff.season"));
            }, UiFactory.Primary(Loc.T("staff.hireButton"), hire), UiFactory.Act(Loc.T("ux.common.close"), () => { }), 0.5f);
        }

        /// <summary>One holder: level, name and one line (rider, contract end, salary); a tap opens the actions.</summary>
        private void Holder(Transform list, WorldState world, Team team, StaffMember s)
        {
            var member = s;
            var rider = s.AssignedRiderId != null ? world.GetRider(s.AssignedRiderId) : null;
            bool expiring = s.ContractEndSeason <= world.Season;
            string until = Loc.T("staff.until", s.ContractEndSeason);
            string detail = (rider != null ? Loc.T("staff.with", rider.LastName) + " · " : "") + (expiring ? Theme.WarnText(until) : until);
            Ui.PersonRow(list, s.Skill, s.FullName, detail, MathX.FormatMoney(s.Salary) + "/" + Loc.T("staff.season"), () => HolderSheet(world, team, member));
        }

        /// <summary>A holder's actions: other rider (when it applies), release (two taps) and renew (primary).</summary>
        private void HolderSheet(WorldState world, Team team, StaffMember s)
        {
            var member = s;
            var rider = s.AssignedRiderId != null ? world.GetRider(s.AssignedRiderId) : null;
            double severance = StaffMarket.SeveranceCost(world, s);
            Ui.Sheet(s.FullName, list =>
            {
                var l = (RectTransform)list;
                Ui.Line(list, Loc.T("staff.skill"), s.Skill.ToString("0"), true);
                Ui.Line(list, Loc.T("staff.salary"), MathX.FormatMoney(s.Salary) + "/" + Loc.T("staff.season"));
                Ui.Line(list, Loc.T("rider.until"), s.ContractEndSeason <= world.Season ? Theme.WarnText(s.ContractEndSeason.ToString()) : s.ContractEndSeason.ToString());
                Ui.Line(list, Loc.T("staff.exp"), s.Experience.ToString());
                if (rider != null) Ui.Line(list, Loc.T("tab.riders"), rider.FullName);
                if (StaffService.PerRider(s.Role) && team.RiderIds.Count > 1)
                    Ui.Secondary(l, Loc.T("staff.otherRider"), () =>
                    {
                        Ui.CloseSheet();
                        int idx = team.RiderIds.IndexOf(member.AssignedRiderId);
                        string next = team.RiderIds[(idx + 1) % team.RiderIds.Count];
                        if (StaffService.Assign(world, team, member, next))
                        {
                            var r = world.GetRider(next);
                            Shell.Toast(Loc.T("staff.assigned", member.FullName, r != null ? r.FullName : ""));
                        }
                        Shell.Refresh();
                    });
                Ui.Danger(l, Loc.T("staff.release", MathX.FormatMoney(severance)), Loc.T("staff.fireConfirm", MathX.FormatMoney(severance)), "staff.fire." + member.Id, () =>
                {
                    Ui.CloseSheet();
                    StaffMarket.Fire(world, team, member.Id);
                    Shell.Toast(Loc.T("staff.released", member.FullName, MathX.FormatMoney(severance)));
                    Shell.Refresh();
                }, null);
            }, UiFactory.Primary(Loc.T("staff.renew"), () =>
            {
                StaffMarket.Extend(world, Game.Db, team, member, 2);
                Shell.Toast(Loc.T("staff.renewed", member.FullName, member.ContractEndSeason, MathX.FormatMoney(member.Salary)));
                Shell.Refresh();
            }), null, 0.5f);
        }
    }

    /// <summary>
    /// The manager (UX spec §3.9), without scrolling: on the left the manager (reputation, wealth, salary or owner,
    /// contract end; the role sentence behind "i") and the five skills as bars; on the right the season objective
    /// and the team's structure (with the team's reputation).
    /// </summary>
    public class ManagerPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.manager"); } }

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            var me = world.PlayerManager;
            var cols = Ui.Columns12(content, 6, 6);
            if (me != null) ManagerColumn(cols[0], world, me);
            else Ui.Empty(Wp2Ui.ListIn(cols[0]), UiIcons.Person, Loc.T("shell.noTeam"));
            if (team == null) return;

            var rows = Ui.Rows(cols[1], 0.42f, 0.58f);
            var pers = team.Personality;
            if (pers != null)
            {
                int teams = world.TeamsInCategory(team.CategoryId).Count;
                var obj = Ui.Tile(rows[0], Loc.T("objective.title"), null,
                    Theme.Caps(Loc.T("ux.manager.target", TeamAI.TargetPosition(pers.Objective, teams))));
                var big = Ui.Label(obj, Theme.Caps(Loc.T("objective." + pers.Objective)), Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Timing, FontStyle.BoldAndItalic);
                UiFactory.Place(big.rectTransform, 0, 0.5f, 1, 1);
                if (pers.LastPosition > 0)
                {
                    var last = Ui.Label(obj, Theme.Caps(Loc.T("objective.last", Loc.T("objective." + pers.LastObjective), pers.LastPosition)) + "   " +
                                             (pers.LastObjectiveMet ? Theme.GoodText(Theme.Caps(Loc.T("objective.met"))) : Theme.BadText(Theme.Caps(Loc.T("objective.missed")))),
                        Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
                    UiFactory.Place(last.rectTransform, 0, 0, 1, 0.45f);
                }
            }
            var org = Wp2Ui.ListIn(Ui.Tile(pers != null ? rows[1] : cols[1], Loc.T("manager.structure")), 4);
            var o = world.OrganisationOf(team);
            Ui.Line(org, Loc.T("manager.team"), team.Name, true);
            if (o != null)
            {
                Ui.Line(org, Loc.T("manager.organisation"), o.Name);
                Ui.Line(org, Loc.T("finance.owner"), Loc.T("owner." + o.Owner));
            }
            var cat = Game.Db.GetCategory(team.CategoryId);
            Ui.Line(org, Loc.T("manager.category"), cat != null ? cat.Name : team.CategoryId);
            Ui.Line(org, Loc.T("manager.teamReputation"), Fmt.Rating(team.Reputation));
        }

        private void ManagerColumn(RectTransform area, WorldState world, ManagerProfile me)
        {
            var rows = Ui.Rows(area, 0.36f, 0.64f);
            bool owner = me.Job != null && me.Job.Role == ManagerRole.OwnerManager;
            string role = null;
            if (me.Job != null)
            {
                var org = world.GetOrganisation(me.Job.OrganisationId);
                role = owner ? Loc.T("manager.owner", org != null ? org.Name : "") :
                    Loc.T("manager.employed", org != null ? org.Name : "", me.Job.EndSeason, MathX.FormatMoney(me.Job.SalaryPerSeason), Loc.T("autonomy." + me.Job.Autonomy));
            }
            var card = Wp2Ui.ListIn(Ui.Tile(rows[0], me.FirstName + " " + me.LastName,
                null, Theme.Caps(Loc.T("manager.subtitle")), 0, null, role != null ? Wp2Ui.Info(Loc.T("manager.subtitle"), role) : null));
            Ui.Kpis(card,
                Ui.Kpi(me.Reputation.ToString("0"), Loc.T("manager.reputation")),
                Ui.Kpi(MathX.FormatMoney(me.PersonalWealth), Loc.T("manager.wealthShort")),
                owner ? Ui.Kpi(Loc.T("manager.ownerKpi"), Loc.T("manager.status"))
                      : Ui.Kpi(me.Job != null ? MathX.FormatMoney(me.Job.SalaryPerSeason) : "-", Loc.T("staff.salary")),
                Ui.Kpi(me.Job != null ? me.Job.EndSeason.ToString() : "-", Loc.T("manager.until")));

            var skills = Wp2Ui.ListIn(Ui.Tile(rows[1], Loc.T("mycareer.skills")), 6);
            var list = new[]
            {
                new KeyValuePair<string, double>("mycareer.skill.negotiation", me.Negotiation),
                new KeyValuePair<string, double>("mycareer.skill.sporting", me.SportingManagement),
                new KeyValuePair<string, double>("mycareer.skill.finance", me.Finance),
                new KeyValuePair<string, double>("mycareer.skill.relations", me.Relations),
                new KeyValuePair<string, double>("mycareer.skill.commercial", me.Commercial)
            };
            foreach (var sk in list)
                Ui.Bar(skills, Loc.T(sk.Key), sk.Value / 100.0, "<b>" + sk.Value.ToString("0") + "</b>", Ui.RatingColor(sk.Value), 42);
        }
    }
}
