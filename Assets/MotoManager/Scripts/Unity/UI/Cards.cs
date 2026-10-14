using System;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// The person and section cards of the restructured screens (riders, staff, development), after the user's
    /// reference captures in our palette: a hero band in the team colour with two slanted livery stripes, the first
    /// name small over the LAST NAME big, a country chip, stars, a block of label / value rows and a full-width
    /// action at the bottom (accent fill) with an optional square secondary beside it. Square corners, flat fills.
    /// </summary>
    public static class Cards
    {
        public const float HeroShare = 0.36f, ActionHeight = 64, RowHeight = 44;

        /// <summary>
        /// A person card filling <paramref name="area"/>. Returns the body rect between the name block and the
        /// action (fill it with <see cref="Rows"/> or anything else). <paramref name="heroText"/> is the big figure
        /// drawn in the hero band (race number, initials, role); <paramref name="rating"/> 0-5 stars (negative: none).
        /// </summary>
        public static RectTransform Person(RectTransform area, Color team, string heroText, string first, string last,
            string country, double rating, string corner, string action, Action onAction, string secondIcon = null, Action onSecond = null)
        {
            var card = UiFactory.Panel("PersonCard", area, Theme.Card);
            UiFactory.Stretch(card.rectTransform);
            card.gameObject.AddComponent<RectMask2D>();
            if (onAction != null)
            {
                var tap = card.gameObject.AddComponent<Button>();
                tap.targetGraphic = card;
                tap.onClick.AddListener(() => onAction());
            }

            // Hero band: team colour fading to the card, two slanted translucent stripes, the big figure.
            var hero = UiFactory.Decor("Hero", card.transform, team);
            UiFactory.Place(hero.rectTransform, 0, 1 - HeroShare, 1, 1);
            UiFactory.Gradient(hero, Theme.Alpha(team, 0.85f), Theme.Alpha(team, 0.25f));
            for (int i = 0; i < 2; i++)
            {
                var s = UiFactory.Decor("Stripe", hero.transform, Theme.Alpha(Color.white, i == 0 ? 0.06f : 0.1f));
                s.rectTransform.anchorMin = s.rectTransform.anchorMax = new Vector2(0.62f + i * 0.14f, 0.5f);
                s.rectTransform.sizeDelta = new Vector2(i == 0 ? 70 : 22, 600);
                s.rectTransform.localRotation = Quaternion.Euler(0, 0, -30);
            }
            if (!string.IsNullOrEmpty(heroText))
            {
                var big = Ui.Label(hero.transform, heroText, Theme.TextDisplay, TextAnchor.MiddleCenter, Theme.Alpha(Color.white, 0.92f), FontStyle.BoldAndItalic);
                UiFactory.Stretch(big.rectTransform);
                big.rectTransform.offsetMin = new Vector2(16, 8);
                big.rectTransform.offsetMax = new Vector2(-16, -8);
                big.resizeTextForBestFit = true;
                big.resizeTextMinSize = Theme.TextTitle;
                big.resizeTextMaxSize = 96;
                UiFactory.Style(big, FontStyle.BoldAndItalic);
            }

            // Name block: first name, country chip + LAST NAME, stars and the corner text (age...) on the right.
            float nameTop = 1 - HeroShare, nameBottom = nameTop - 0.2f;
            var name = UiFactory.Panel("Name", card.transform, Theme.Panel);
            UiFactory.Place(name.rectTransform, 0, nameBottom, 1, nameTop);
            var firstLabel = Ui.Label(name.transform, first ?? "", Theme.TextCaption, TextAnchor.LowerLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(firstLabel.rectTransform, 0, 0.55f, 0.62f, 1);
            firstLabel.rectTransform.offsetMin = new Vector2(20, 0);
            float lastLeft = 20;
            if (!string.IsNullOrEmpty(country))
            {
                var chip = Ui.Pill(name.transform, country.ToUpperInvariant(), Theme.PanelLight, Theme.Text);
                chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = new Vector2(0, 0.28f);
                chip.rectTransform.pivot = new Vector2(0, 0.5f);
                chip.rectTransform.sizeDelta = new Vector2(52, 30);
                chip.rectTransform.anchoredPosition = new Vector2(20, 0);
                lastLeft = 82;
            }
            var lastLabel = Ui.Label(name.transform, Theme.Caps(last ?? ""), Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
            UiFactory.Place(lastLabel.rectTransform, 0, 0, 0.66f, 0.56f);
            lastLabel.rectTransform.offsetMin = new Vector2(lastLeft, 0);
            Ui.Fit(lastLabel, Theme.TextTitle);
            UiFactory.Style(lastLabel, FontStyle.BoldAndItalic);
            if (rating >= 0)
            {
                var stars = UiFactory.Rect("Stars", name.transform);
                UiFactory.Place(stars, 0.6f, 0.55f, 1, 1);
                stars.offsetMax = new Vector2(-16, -4);
                Stars(stars, rating);
            }
            if (!string.IsNullOrEmpty(corner))
            {
                var c = Ui.Label(name.transform, corner, Theme.TextCaption, TextAnchor.MiddleRight, Theme.TextDim, FontStyle.Bold);
                UiFactory.Place(c.rectTransform, 0.6f, 0, 1, 0.55f);
                c.rectTransform.offsetMax = new Vector2(-18, 0);
                c.supportRichText = true;
            }

            // Action row at the bottom: accent block (and a square secondary).
            float bottom = 0;
            if (!string.IsNullOrEmpty(action) && onAction != null)
            {
                bottom = ActionHeight + 16;
                var row = UiFactory.Rect("Actions", card.transform);
                row.anchorMin = new Vector2(0, 0);
                row.anchorMax = new Vector2(1, 0);
                row.pivot = new Vector2(0.5f, 0);
                row.offsetMin = new Vector2(16, 12);
                row.offsetMax = new Vector2(-16, 12 + ActionHeight);
                float second = onSecond != null ? ActionHeight * 1.6f : 0;
                var b = UiFactory.Button("Action", row, Theme.Caps(action), onAction, Theme.Accent, Theme.TextHeader);
                var brt = (RectTransform)b.transform;
                UiFactory.Stretch(brt);
                brt.offsetMax = new Vector2(-(second > 0 ? second + 10 : 0), 0);
                var t = b.GetComponentInChildren<Text>();
                t.color = Theme.OnAccent;
                Ui.Fit(t, Theme.TextHeader);
                if (onSecond != null)
                {
                    var s = UiFactory.Button("Second", row, UiIcons.Known(secondIcon) ? "" : (secondIcon ?? ""), onSecond, Theme.PanelLight, Theme.TextHeader);
                    var srt = (RectTransform)s.transform;
                    srt.anchorMin = new Vector2(1, 0);
                    srt.anchorMax = new Vector2(1, 1);
                    srt.pivot = new Vector2(1, 0.5f);
                    srt.sizeDelta = new Vector2(second, 0);
                    srt.anchoredPosition = Vector2.zero;
                    if (UiIcons.Known(secondIcon))
                    {
                        var ic = UiIcons.Icon(s.transform, secondIcon, Theme.Text);
                        ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                        ic.rectTransform.sizeDelta = new Vector2(36, 36);
                    }
                }
            }

            var body = UiFactory.Rect("Body", card.transform);
            body.anchorMin = new Vector2(0, 0);
            body.anchorMax = new Vector2(1, nameBottom);
            body.offsetMin = new Vector2(16, bottom + 4);
            body.offsetMax = new Vector2(-16, -8);
            return body;
        }

        /// <summary>
        /// Label / value rows stacked from the top of <paramref name="body"/> (alternating fills). A value colour of
        /// null uses the text colour.
        /// </summary>
        public static void Rows(RectTransform body, string[] labels, string[] values, Color?[] colors = null)
        {
            int n = labels.Length;
            float h = n > 0 ? 1f / n : 1;
            for (int i = 0; i < n; i++)
            {
                var row = UiFactory.Panel("Row", body, i % 2 == 0 ? Theme.Panel : Theme.Alpha(Theme.Panel, 0.4f));
                UiFactory.Place(row.rectTransform, 0, 1 - h * (i + 1), 1, 1 - h * i);
                row.rectTransform.offsetMin = new Vector2(0, 2);
                var l = Ui.Label(row.transform, labels[i], Theme.TextCaption, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
                UiFactory.Stretch(l.rectTransform);
                l.rectTransform.offsetMin = new Vector2(16, 0);
                l.rectTransform.offsetMax = new Vector2(-110, 0);
                Ui.Fit(l, Theme.TextCaption);
                var v = Ui.Label(row.transform, values[i], Theme.TextBody, TextAnchor.MiddleRight,
                    colors != null && i < colors.Length && colors[i].HasValue ? colors[i].Value : Theme.Text, FontStyle.Bold);
                UiFactory.Stretch(v.rectTransform);
                v.rectTransform.offsetMax = new Vector2(-16, 0);
                v.supportRichText = true;
                UiFactory.Style(v, FontStyle.BoldAndItalic);
            }
        }

        /// <summary>Five stars right-aligned in <paramref name="area"/>, <paramref name="rating"/> 0-5 (halves dimmed).</summary>
        public static void Stars(RectTransform area, double rating)
        {
            for (int i = 0; i < 5; i++)
            {
                double fill = Math.Max(0, Math.Min(1, rating - i));
                var c = fill >= 0.75 ? Theme.Timing : fill >= 0.25 ? Theme.Alpha(Theme.Timing, 0.55f) : Theme.Alpha(Color.white, 0.14f);
                var star = UiIcons.Icon(area, UiIcons.Star, c);
                star.rectTransform.anchorMin = star.rectTransform.anchorMax = new Vector2(1, 0.5f);
                star.rectTransform.pivot = new Vector2(1, 0.5f);
                star.rectTransform.sizeDelta = new Vector2(28, 28);
                star.rectTransform.anchoredPosition = new Vector2(-(4 - i) * 30, 0);
            }
        }

        /// <summary>Stars for a 0-100 rating (40 = one star, 90 = five).</summary>
        public static double StarsFor(double rating)
        {
            return Math.Max(0.5, Math.Min(5, (rating - 40) / 10.0 + 0.5));
        }

        /// <summary>
        /// A bottom tab bar (profile sections): equal flat blocks, the selected one lighter with an accent outline.
        /// </summary>
        public static void BottomTabs(RectTransform area, string[] labels, int selected, Action<int> onSelect)
        {
            int n = labels.Length;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                bool on = i == selected;
                var b = UiFactory.Button("Tab", area, Theme.Caps(labels[i]), () => onSelect(idx), on ? Theme.PanelLight : Theme.Panel, Theme.TextHeader);
                var rt = (RectTransform)b.transform;
                UiFactory.Place(rt, (float)i / n, 0, (float)(i + 1) / n, 1);
                rt.offsetMin = new Vector2(i == 0 ? 0 : 6, 0);
                rt.offsetMax = new Vector2(i == n - 1 ? 0 : -6, 0);
                var t = b.GetComponentInChildren<Text>();
                t.color = on ? Theme.Text : Theme.TextDim;
                Ui.Fit(t, Theme.TextHeader);
                if (on)
                {
                    var o = b.gameObject.AddComponent<Outline>();
                    o.effectColor = Theme.Accent;
                    o.effectDistance = new Vector2(3, -3);
                    var bar = UiFactory.Decor("Bar", b.transform, Theme.Accent);
                    bar.rectTransform.anchorMin = new Vector2(0, 0);
                    bar.rectTransform.anchorMax = new Vector2(1, 0);
                    bar.rectTransform.offsetMin = Vector2.zero;
                    bar.rectTransform.offsetMax = new Vector2(0, 4);
                }
            }
        }

        /// <summary>
        /// An attribute cell: the name on the left, "value / max" on the right (value big), a thin bar underneath
        /// showing the value (coloured by level) over the reachable maximum (dim). <paramref name="up"/> marks a boosted
        /// attribute with an accent arrow-less dot.
        /// </summary>
        public static void Attribute(RectTransform area, string label, double value, double max, double scale, bool up = false)
        {
            var cell = UiFactory.Panel("Attr", area, Theme.Card);
            UiFactory.Stretch(cell.rectTransform);
            var l = Ui.Label(cell.transform, label, Theme.TextBody, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
            UiFactory.Place(l.rectTransform, 0, 0.2f, 0.66f, 1);
            l.rectTransform.offsetMin = new Vector2(16, 0);
            Ui.Fit(l, Theme.TextBody);
            var v = Ui.Label(cell.transform, "<size=" + Theme.TextTitle + "><b>" + value.ToString("0") + "</b></size>" +
                (max > 0 ? Theme.Dim(" / " + max.ToString("0")) : ""), Theme.TextCaption, TextAnchor.MiddleRight, Theme.Text, FontStyle.Normal);
            UiFactory.Place(v.rectTransform, 0.5f, 0.2f, 1, 1);
            v.rectTransform.offsetMax = new Vector2(-16, 0);
            v.supportRichText = true;
            UiFactory.Style(v, FontStyle.BoldAndItalic);
            if (up)
            {
                var dot = UiFactory.Decor("Up", cell.transform, Theme.Good);
                UiFactory.Round(dot, 1.6f);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.6f);
                dot.rectTransform.sizeDelta = new Vector2(12, 12);
            }
            float frac = scale > 0 ? Mathf.Clamp01((float)(value / scale)) : 0;
            float maxFrac = scale > 0 && max > 0 ? Mathf.Clamp01((float)(max / scale)) : 0;
            var track = UiFactory.Decor("Track", cell.transform, Theme.Alpha(Color.white, 0.06f));
            track.rectTransform.anchorMin = new Vector2(0, 0);
            track.rectTransform.anchorMax = new Vector2(1, 0);
            track.rectTransform.offsetMin = new Vector2(16, 8);
            track.rectTransform.offsetMax = new Vector2(-16, 12);
            if (maxFrac > 0)
            {
                var reach = UiFactory.Decor("Max", track.transform, Theme.Alpha(Color.white, 0.18f));
                UiFactory.Place(reach.rectTransform, 0, 0, maxFrac, 1);
            }
            var fillBar = UiFactory.Decor("Fill", track.transform, Ui.RatingColor(scale > 0 ? value / scale * 100 : 0));
            UiFactory.Place(fillBar.rectTransform, 0, 0, frac, 1);
        }

        /// <summary>
        /// A big section card (staff groups, development shortcuts): a pictogram, the title in CAPS, one line of
        /// detail and a chevron; the whole card is the button.
        /// </summary>
        public static Button Section(RectTransform area, string icon, string title, string detail, Action onTap, int badge = 0)
        {
            var b = UiFactory.Button("SectionCard", area, "", onTap, Theme.Card, Theme.TextBody);
            UiFactory.Stretch((RectTransform)b.transform);
            var band = UiFactory.Decor("Band", b.transform, Theme.Accent);
            band.rectTransform.anchorMin = new Vector2(0, 1);
            band.rectTransform.anchorMax = new Vector2(1, 1);
            band.rectTransform.offsetMin = new Vector2(0, -6);
            band.rectTransform.offsetMax = Vector2.zero;
            if (UiIcons.Known(icon))
            {
                var ic = UiIcons.Icon(b.transform, icon, Theme.Text);
                ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0.5f, 0.66f);
                ic.rectTransform.sizeDelta = new Vector2(96, 96);
            }
            var t = b.GetComponentInChildren<Text>();
            t.text = "<b>" + Theme.Caps(title) + "</b>" + (string.IsNullOrEmpty(detail) ? "" : "\n<size=" + Theme.TextCaption + ">" + Theme.Dim(detail) + "</size>");
            t.supportRichText = true;
            t.alignment = TextAnchor.UpperCenter;
            UiFactory.Place(t.rectTransform, 0, 0.06f, 1, 0.42f);
            t.rectTransform.offsetMin = new Vector2(12, 0);
            t.rectTransform.offsetMax = new Vector2(-12, 0);
            Ui.Fit(t, Theme.TextHeader);
            if (badge > 0)
            {
                var bd = Ui.Badge(b.transform, badge, Theme.Bad);
                bd.rectTransform.anchorMin = bd.rectTransform.anchorMax = new Vector2(1, 1);
                bd.rectTransform.pivot = new Vector2(1, 1);
                bd.rectTransform.anchoredPosition = new Vector2(-12, -16);
                bd.rectTransform.sizeDelta = new Vector2(34, 26);
            }
            return b;
        }

        /// <summary>A wide secondary button centred at the bottom of a page ("Encontrar nuevos ...").</summary>
        public static Button Find(RectTransform area, string label, Action onTap, int count = 0)
        {
            var b = UiFactory.Button("Find", area, Theme.Caps(label), onTap, Theme.PanelLight, Theme.TextHeader);
            UiFactory.Stretch((RectTransform)b.transform);
            var t = b.GetComponentInChildren<Text>();
            t.color = Theme.Text;
            Ui.Fit(t, Theme.TextHeader);
            if (count > 0)
            {
                var bd = Ui.Badge(b.transform, count, Theme.Accent);
                bd.rectTransform.anchorMin = bd.rectTransform.anchorMax = new Vector2(1, 0.5f);
                bd.rectTransform.pivot = new Vector2(1, 0.5f);
                bd.rectTransform.anchoredPosition = new Vector2(-16, 0);
                bd.rectTransform.sizeDelta = new Vector2(40, 32);
            }
            return b;
        }
    }
}
