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
            var card = Frame("PersonCard", area);
            if (onAction != null)
            {
                var tap = card.gameObject.AddComponent<Button>();
                tap.targetGraphic = card;
                tap.onClick.AddListener(() => onAction());
            }

            // Hero band: the team colour over the card with big chevrons pointing right, the big figure.
            var hero = Hero(card.transform, team, 1 - HeroShare, 1);
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

        /// <summary>Five stars across <paramref name="area"/>, <paramref name="rating"/> 0-5 (halves dimmed).</summary>
        public static void Stars(RectTransform area, double rating)
        {
            for (int i = 0; i < 5; i++)
            {
                double fill = Math.Max(0, Math.Min(1, rating - i));
                var c = fill >= 0.75 ? Theme.Timing : fill >= 0.25 ? Theme.Alpha(Theme.Timing, 0.55f) : Theme.Alpha(Color.white, 0.14f);
                // Each star in a fifth of the area (at most 28 units), so narrow cards shrink them instead of overlapping.
                var cell = UiFactory.Rect("Star", area);
                UiFactory.Place(cell, i / 5f, 0, (i + 1) / 5f, 1);
                var star = UiIcons.Icon(cell, UiIcons.Star, c);
                star.rectTransform.anchorMin = star.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                star.rectTransform.sizeDelta = new Vector2(28, 28);
                var fit = star.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = 1;
                star.rectTransform.localScale = Vector3.one * 0.9f;
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
        /// A big section card (staff groups, development shortcuts), in the person cards' anatomy: a hero band with
        /// chevrons and the pictogram, the title in CAPS on a dark band, the detail lines, and an accent action bar
        /// at the bottom (<paramref name="action"/>, default "VER"). The whole card is the button.
        /// </summary>
        public static Button Section(RectTransform area, string icon, string title, string detail, Action onTap, int badge = 0, string action = null)
        {
            var card = Frame("SectionCard", area);
            var b = card.gameObject.AddComponent<Button>();
            b.targetGraphic = card;
            b.onClick.AddListener(() =>
            {
                GameAudio.ClickSound();
                onTap();
            });
            // Fixed heights from the bottom (action, detail lines, title band); the hero takes the rest.
            int lines = string.IsNullOrEmpty(detail) ? 0 : detail.Split('\n').Length;
            float actionTop = 12 + ActionHeight, detailH = lines > 0 ? lines * 34 + 12 : 0, titleH = 56;
            float titleBottom = actionTop + 8 + detailH;
            var hero = Hero(card.transform, Theme.Accent, 0, 1);
            hero.rectTransform.offsetMin = new Vector2(0, titleBottom + titleH);
            if (UiIcons.Known(icon))
            {
                var ic = UiIcons.Icon(hero.transform, icon, Theme.Text);
                ic.rectTransform.anchorMin = new Vector2(0.5f, 0.12f);
                ic.rectTransform.anchorMax = new Vector2(0.5f, 0.88f);
                ic.rectTransform.sizeDelta = new Vector2(104, 0);
                var fit = ic.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                fit.aspectRatio = 1;
            }
            var band = UiFactory.Panel("Title", card.transform, Theme.Panel);
            band.raycastTarget = false;
            var brt = band.rectTransform;
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(1, 0);
            brt.pivot = new Vector2(0.5f, 0);
            brt.offsetMin = new Vector2(0, titleBottom);
            brt.offsetMax = new Vector2(0, titleBottom + titleH);
            var t = Ui.Label(band.transform, Theme.Caps(title), Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(20, 0);
            t.rectTransform.offsetMax = new Vector2(-20, 0);
            Ui.Fit(t, Theme.TextTitle);
            if (lines > 0)
            {
                var d = Ui.Label(card.transform, detail, Theme.TextBody, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
                d.supportRichText = true;
                var drt = d.rectTransform;
                drt.anchorMin = new Vector2(0, 0);
                drt.anchorMax = new Vector2(1, 0);
                drt.pivot = new Vector2(0.5f, 0);
                drt.offsetMin = new Vector2(20, actionTop + 8);
                drt.offsetMax = new Vector2(-20, actionTop + 8 + detailH);
                Ui.Fit(d, Theme.TextBody);
            }
            var go = UiFactory.Panel("Action", card.transform, Theme.Accent);
            go.raycastTarget = false;
            var grt = go.rectTransform;
            grt.anchorMin = new Vector2(0, 0);
            grt.anchorMax = new Vector2(1, 0);
            grt.pivot = new Vector2(0.5f, 0);
            grt.offsetMin = new Vector2(16, 12);
            grt.offsetMax = new Vector2(-16, 12 + ActionHeight);
            var gl = Ui.Label(go.transform, Theme.Caps(action ?? Loc.T("ux.cards.open")), Theme.TextHeader, TextAnchor.MiddleCenter, Theme.OnAccent, FontStyle.Bold);
            UiFactory.Stretch(gl.rectTransform);
            Ui.Fit(gl, Theme.TextHeader);
            if (badge > 0)
            {
                var bd = Ui.Badge(card.transform, badge, Theme.Bad);
                bd.rectTransform.anchorMin = bd.rectTransform.anchorMax = new Vector2(1, 1);
                bd.rectTransform.pivot = new Vector2(1, 1);
                bd.rectTransform.anchoredPosition = new Vector2(-12, -12);
            }
            return b;
        }

        /// <summary>The card body: a flat card fill with a 1-unit edge and a soft drop shadow, clipping its content.</summary>
        private static Image Frame(string name, RectTransform area)
        {
            var card = UiFactory.Panel(name, area, Theme.Card);
            UiFactory.Stretch(card.rectTransform);
            card.gameObject.AddComponent<RectMask2D>();
            card.gameObject.AddComponent<Outline>().effectColor = Theme.Edge;
            UiFactory.DropShadow(card, 0.45f, 4);
            return card;
        }

        /// <summary>
        /// The hero band of a card between <paramref name="yMin"/> and <paramref name="yMax"/>: the colour toned down
        /// over the card, three big chevrons pointing right in the same colour (the reference's arrow pattern) and a
        /// 4-unit colour rule at its base.
        /// </summary>
        public static Image Hero(Transform card, Color color, float yMin, float yMax)
        {
            var hero = UiFactory.Decor("Hero", card, Color.Lerp(Theme.Card, color, 0.42f));
            UiFactory.Place(hero.rectTransform, 0, yMin, 1, yMax);
            hero.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < 3; i++) Chevron(hero.transform, Theme.Alpha(Color.Lerp(color, Color.white, 0.15f), 0.16f + i * 0.06f), 0.18f + i * 0.3f);
            var rule = UiFactory.Decor("Rule", hero.transform, color);
            rule.rectTransform.anchorMin = new Vector2(0, 0);
            rule.rectTransform.anchorMax = new Vector2(1, 0);
            rule.rectTransform.offsetMin = Vector2.zero;
            rule.rectTransform.offsetMax = new Vector2(0, 4);
            return hero;
        }

        /// <summary>A big ">" made of two thick bars, centred at <paramref name="x"/> across the band's full height.</summary>
        private static void Chevron(Transform band, Color color, float x)
        {
            for (int k = 0; k < 2; k++)
            {
                var bar = UiFactory.Decor("Chevron", band, color);
                bar.rectTransform.anchorMin = new Vector2(x, k == 0 ? 0.5f : 0f);
                bar.rectTransform.anchorMax = new Vector2(x, k == 0 ? 1f : 0.5f);
                bar.rectTransform.pivot = new Vector2(0.5f, k == 0 ? 0 : 1);
                bar.rectTransform.anchoredPosition = new Vector2(0, 0);
                bar.rectTransform.sizeDelta = new Vector2(56, 60);
                bar.rectTransform.localRotation = Quaternion.Euler(0, 0, k == 0 ? 40 : -40);
            }
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
            }
            return b;
        }
    }
}
