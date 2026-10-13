using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// The design system of the management and race screens (docs/UX_REVIEW.md §1–§2). Everything new is built from
    /// these pieces; screens never style buttons, tiles or headers by hand.
    ///
    /// API note
    /// - "area" parameters are anchor-layout rects the component FILLS (use <see cref="Area"/>, <see cref="Columns12"/>,
    ///   <see cref="Rows"/> to cut them). When an "area" is in fact a layout list (it has a LayoutGroup), buttons add a
    ///   row of their own height instead, so passing a card body works too.
    /// - "list" parameters are layout parents (scroll lists, card bodies): the component adds a row.
    /// - Sizes come from <see cref="Grid"/> and the Theme type scale (Theme.TextDisplay..TextMin); 18 is the floor.
    /// - Exactly 0 or 1 <see cref="PrimaryCTA"/> per screen; rows are tapped, never given filled-accent buttons.
    /// - Explanations go behind <see cref="InfoButton"/> (a <see cref="Sheet"/>); visible warnings use
    ///   <see cref="Alert"/>; empty lists use <see cref="Empty"/>. <see cref="Note"/> is obsolete.
    /// - There are no pictograms beyond the existing UiIcons: any icon argument that is not a known UiIcons name is
    ///   drawn as plain text (see <see cref="Glyph"/>: "i", "...", "X", "+", "-").
    /// </summary>
    public static class Ui
    {
        private static readonly HashSet<string> Open = new HashSet<string>();

        /// <summary>Layout tokens in canvas units (§1.1).</summary>
        public static class Grid
        {
            public const float Gutter = 16, Gap = 12, Header = 48, Row = 64, Button = 64, Cta = 72, Hit = 56;
        }

        // ================================================================== text helpers

        /// <summary>Best fit between 18 (the floor) and <paramref name="max"/>.</summary>
        public static Text Fit(Text t, int max)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = Mathf.Max(max, Theme.TextMin);
            t.resizeTextMinSize = Theme.TextMin;
            return t;
        }

        /// <summary>A rich-text label that shrinks to fit (never below 18). Caller places it.</summary>
        public static Text Label(Transform parent, string text, int size, TextAnchor align, Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var t = UiFactory.Label("Text", parent, text, size, align, color ?? Theme.Text);
            t.supportRichText = true;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return Fit(t, size);
        }

        private static Text Txt(Transform parent, string name, string text, int size, TextAnchor align, Color color, FontStyle style)
        {
            var t = Label(parent, text, size, align, color, style);
            t.gameObject.name = name;
            return t;
        }

        private static bool IsList(Transform t)
        {
            return t != null && t.GetComponent<LayoutGroup>() != null;
        }

        /// <summary>The rect a control fills: the area itself, or a new row when the area is a layout list.</summary>
        private static RectTransform Host(RectTransform area, float height)
        {
            return IsList(area) ? UiFactory.Row(area, height) : area;
        }

        private static void Tappable(Image img, Action onTap)
        {
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = Color.white;
            b.colors = colors;
            b.onClick.AddListener(() =>
            {
                GameAudio.ClickSound();
                onTap();
            });
        }

        /// <summary>An icon (known UiIcons name) or, for any other string, the string as bold text. Caller places it.</summary>
        private static Graphic Symbol(Transform parent, string icon, Color color, int textSize)
        {
            if (UiIcons.Known(icon)) return UiIcons.Icon(parent, icon, color);
            var t = UiFactory.Label("Glyph", parent, icon ?? "", textSize, TextAnchor.MiddleCenter, color);
            t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // ================================================================== layout

        /// <summary>A child rect anchored to a fraction of <paramref name="parent"/>, inset by half a gap.</summary>
        public static RectTransform Area(RectTransform parent, float x0, float y0, float x1, float y1)
        {
            var rt = UiFactory.Rect("Area", parent);
            return UiFactory.Place(rt, x0, y0, x1, y1, Grid.Gap / 2);
        }

        /// <summary>
        /// Splits <paramref name="parent"/> horizontally by 12-column spans (a gap between columns). Spans summing to
        /// less than 12 leave the rest of the width empty on the right; more than 12 are scaled to fit.
        /// </summary>
        public static RectTransform[] Columns12(RectTransform parent, params int[] spans)
        {
            int total = 0;
            foreach (var s in spans) total += Mathf.Max(0, s);
            total = Mathf.Max(12, total);
            var res = new RectTransform[spans.Length];
            int acc = 0;
            for (int i = 0; i < spans.Length; i++)
            {
                var rt = UiFactory.Rect("Col" + i, parent);
                float a0 = (float)acc / total;
                acc += Mathf.Max(0, spans[i]);
                float a1 = (float)acc / total;
                rt.anchorMin = new Vector2(a0, 0);
                rt.anchorMax = new Vector2(a1, 1);
                rt.offsetMin = new Vector2(i == 0 ? 0 : Grid.Gap / 2, 0);
                rt.offsetMax = new Vector2(a1 >= 0.999f ? 0 : -Grid.Gap / 2, 0);
                res[i] = rt;
            }
            return res;
        }

        /// <summary>Splits <paramref name="parent"/> vertically, top to bottom, by relative shares (a gap between rows).</summary>
        public static RectTransform[] Rows(RectTransform parent, params float[] shares)
        {
            float total = 0;
            foreach (var s in shares) total += Mathf.Max(0, s);
            if (total <= 0) total = 1;
            var res = new RectTransform[shares.Length];
            float acc = 0;
            for (int i = 0; i < shares.Length; i++)
            {
                var rt = UiFactory.Rect("Row" + i, parent);
                float top = 1 - acc / total;
                acc += Mathf.Max(0, shares[i]);
                float bottom = 1 - acc / total;
                rt.anchorMin = new Vector2(0, bottom);
                rt.anchorMax = new Vector2(1, top);
                rt.offsetMin = new Vector2(0, i == shares.Length - 1 ? 0 : Grid.Gap / 2);
                rt.offsetMax = new Vector2(0, i == 0 ? 0 : -Grid.Gap / 2);
                res[i] = rt;
            }
            return res;
        }

        // ================================================================== tiles and headers

        /// <summary>
        /// A 48-unit header strip along the top of <paramref name="parent"/>: an optional back chevron, a slanted tag,
        /// the CAPS title, then from the right end: info "i", chevron, count badge and the right-hand text. Filled
        /// strips (<paramref name="fill"/>) use readable ink on the fill. Children are named Back, Tag, Title, Right,
        /// Badge, Chevron and Info. Returns the strip.
        /// </summary>
        public static RectTransform HeaderStrip(Transform parent, string title, string right = null, Action onInfo = null,
            Action onBack = null, Color? fill = null, int badge = 0, Color? badgeColor = null, bool chevron = false)
        {
            var head = UiFactory.Decor("Head", parent, fill ?? Theme.HeaderFill);
            var rt = head.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(0, -Grid.Header);
            rt.offsetMax = Vector2.zero;
            bool filled = fill.HasValue && fill.Value.a > 0.6f;
            Color ink = filled ? Theme.OnColor(fill.Value) : Color.white;
            if (filled) UiFactory.Gradient(head, 1.1f, 0.9f);

            float left = 0;
            if (onBack != null)
            {
                var back = IconButton(head.transform, UiIcons.Back, onBack, ink);
                back.gameObject.name = "Back";
                var brt = (RectTransform)back.transform;
                brt.anchorMin = brt.anchorMax = new Vector2(0, 0.5f);
                brt.pivot = new Vector2(0, 0.5f);
                brt.anchoredPosition = new Vector2(0, 0);
                left = Grid.Hit;
            }
            var tag = UiFactory.Decor("Tag", head.transform, filled ? ink : Theme.Accent);
            UiFactory.Slant(tag, 3.2f);
            tag.rectTransform.anchorMin = new Vector2(0, 0.22f);
            tag.rectTransform.anchorMax = new Vector2(0, 0.78f);
            tag.rectTransform.offsetMin = new Vector2(left + 10, 0);
            tag.rectTransform.offsetMax = new Vector2(left + 24, 0);

            // From the right end leftwards.
            float cursor = 8;
            if (onInfo != null)
            {
                var info = InfoIcon(head.transform, onInfo, ink);
                var irt = (RectTransform)info.transform;
                irt.anchorMin = irt.anchorMax = new Vector2(1, 0.5f);
                irt.pivot = new Vector2(1, 0.5f);
                irt.anchoredPosition = new Vector2(-2, 0);
                cursor = Grid.Hit + 4;
            }
            if (chevron)
            {
                var ch = UiIcons.Chevron(head.transform, filled ? ink : Theme.TextDim);
                ch.gameObject.name = "Chevron";
                ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(1, 0.5f);
                ch.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                ch.rectTransform.sizeDelta = new Vector2(22, 22);
                ch.rectTransform.anchoredPosition = new Vector2(-cursor - 11, 0);
                cursor += 30;
            }
            if (badge > 0)
            {
                var b = Badge(head.transform, badge, badgeColor ?? Theme.Bad);
                b.rectTransform.anchoredPosition = new Vector2(-cursor, 0);
                cursor += 50;
            }
            var t = Txt(head.transform, "Title", Theme.Caps(title ?? ""), Theme.TextHeader, TextAnchor.MiddleLeft, ink, FontStyle.BoldAndItalic);
            UiFactory.DropShadow(t, 0.35f, 1.5f);
            UiFactory.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(left + 34, 0);
            t.rectTransform.offsetMax = new Vector2(-cursor - 4, 0);
            if (!string.IsNullOrEmpty(right))
            {
                t.rectTransform.anchorMax = new Vector2(0.55f, 1);
                t.rectTransform.offsetMax = new Vector2(-4, 0);
                var r = Txt(head.transform, "Right", right, Theme.TextCaption, TextAnchor.MiddleRight,
                    filled ? ink : Color.Lerp(Theme.TextDim, Color.white, 0.3f), FontStyle.Bold);
                UiFactory.Place(r.rectTransform, 0.55f, 0, 1, 1);
                r.rectTransform.offsetMax = new Vector2(-cursor - 4, 0);
            }
            return rt;
        }

        /// <summary>A small rounded count (decisions waiting). Anchored to the right middle of its parent.</summary>
        public static Image Badge(Transform parent, int count, Color color)
        {
            var dot = UiFactory.Decor("Badge", parent, color);
            UiFactory.Round(dot, 1.2f);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(1, 0.5f);
            dot.rectTransform.pivot = new Vector2(1, 0.5f);
            dot.rectTransform.anchoredPosition = new Vector2(-8, 0);
            dot.rectTransform.sizeDelta = new Vector2(42, 32);
            var n = UiFactory.Label("Count", dot.transform, count > 99 ? "99+" : count.ToString(), Theme.TextCaption, TextAnchor.MiddleCenter, Theme.OnColor(color));
            n.fontStyle = FontStyle.Bold;
            UiFactory.Stretch(n.rectTransform);
            return dot;
        }

        /// <summary>
        /// A tile filling <paramref name="area"/>: tile surface, a header strip (chevron when tappable, badge, info),
        /// optional coloured left edge. The whole tile is the button when <paramref name="onTap"/> is set. Returns the
        /// padded body under the header (16 at the sides, 12 top and bottom).
        /// </summary>
        public static RectTransform Tile(RectTransform area, string title, Action onTap = null, string right = null,
            int badge = 0, Color? badgeColor = null, Action onInfo = null, bool dim = false, Color? edge = null, Color? headerFill = null)
        {
            var bg = UiFactory.Panel("Tile", area, dim ? Color.Lerp(Theme.Card, Theme.Tile, 0.4f) : Theme.Tile);
            bg.raycastTarget = onTap != null;
            UiFactory.Stretch(bg.rectTransform);
            UiFactory.Gradient(bg, 1.08f, 0.92f);
            bg.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.06f);
            if (onTap != null) Tappable(bg, onTap);
            float edgeW = 0;
            if (edge.HasValue)
            {
                UiFactory.SideStripe(bg.transform, edge.Value, 6, 0);
                edgeW = 6;
            }
            if (title != null)
            {
                var head = HeaderStrip(bg.transform, title, right, onInfo, null, headerFill, badge, badgeColor, onTap != null);
                if (edgeW > 0) head.offsetMin = new Vector2(edgeW, head.offsetMin.y);
                if (dim)
                {
                    var t = head.Find("Title");
                    if (t != null) t.GetComponent<Text>().color = Theme.TextDim;
                    var tag = head.Find("Tag");
                    if (tag != null) tag.GetComponent<Image>().color = Theme.Steel;
                }
            }
            else if (badge > 0)
            {
                var b = Badge(bg.transform, badge, badgeColor ?? Theme.Bad);
                b.rectTransform.anchorMin = b.rectTransform.anchorMax = new Vector2(1, 1);
                b.rectTransform.pivot = new Vector2(1, 1);
                b.rectTransform.anchoredPosition = new Vector2(-8, -8);
            }
            var body = UiFactory.Rect("Body", bg.transform);
            UiFactory.Stretch(body);
            body.offsetMin = new Vector2(16 + edgeW, 12);
            body.offsetMax = new Vector2(-16, title != null ? -Grid.Header - 12 : -12);
            return body;
        }

        /// <summary>
        /// A tile with one key number (TextKey, timing yellow unless <paramref name="valueColor"/>) and at most one
        /// CAPS status line; an optional pictogram (or text symbol) above the number. Returns the body.
        /// </summary>
        public static RectTransform StatTile(RectTransform area, string title, string value, string status = null,
            Action onTap = null, Color? valueColor = null, string icon = null, int badge = 0, Action onInfo = null, Color? edge = null)
        {
            var body = Tile(area, title, onTap, null, badge, null, onInfo, false, edge, null);
            float statusH = string.IsNullOrEmpty(status) ? 0 : 0.3f;
            float top = 1;
            if (!string.IsNullOrEmpty(icon))
            {
                top = 0.64f;
                var holder = UiFactory.Rect("IconHolder", body);
                UiFactory.Place(holder, 0, top, 1, 1);
                var g = Symbol(holder, icon, Color.Lerp(Theme.TextDim, Color.white, 0.3f), Theme.TextTitle);
                g.rectTransform.anchorMin = g.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                g.rectTransform.sizeDelta = new Vector2(48, 48);
            }
            var v = Txt(body, "Value", value ?? "", Theme.TextKey, TextAnchor.MiddleCenter, valueColor ?? Theme.Timing, FontStyle.BoldAndItalic);
            UiFactory.DropShadow(v, 0.4f, 2);
            UiFactory.Place(v.rectTransform, 0, statusH, 1, top);
            if (statusH > 0)
            {
                var s = Txt(body, "Status", Theme.Caps(status), Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);
                UiFactory.Place(s.rectTransform, 0, 0, 1, statusH);
            }
            return body;
        }

        /// <summary>
        /// A card for layout lists: card surface, a header strip (title, right text, info, back chevron) and a body
        /// padded 16 on every side. Returns the body; add rows to it.
        /// </summary>
        public static RectTransform Card(Transform list, string title = null, string right = null, Action onInfo = null, Action onBack = null)
        {
            return CardCore(list, title, right, onInfo, onBack, null);
        }

        /// <summary><see cref="Card"/> with a coloured header strip (team pages).</summary>
        internal static RectTransform CardCore(Transform list, string title, string right, Action onInfo, Action onBack, Color? fill)
        {
            var stack = Stack(list);
            stack.gameObject.name = "Card";
            var bg = stack.gameObject.AddComponent<Image>();
            bg.color = Theme.Card;
            bg.raycastTarget = false;
            UiFactory.Gradient(bg, 1.04f, 0.96f);
            stack.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.05f);
            if (title != null)
            {
                var row = UiFactory.Row(stack, Grid.Header);
                HeaderStrip(row, title, right, onInfo, onBack, fill);
            }
            var body = UiFactory.Rect("Body", stack);
            var v = body.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 16, 16);
            v.spacing = 8;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return body;
        }

        /// <summary>A header strip alone in a layout list (a section title). Returns the row.</summary>
        public static RectTransform Section(Transform list, string title, string right = null, Action onInfo = null)
        {
            var row = UiFactory.Row(list, Grid.Header);
            HeaderStrip(row, title, right, onInfo);
            return row;
        }

        // ================================================================== buttons

        private enum Look { Primary, Secondary, SecondaryAccent, Ghost }

        private static Button MakeButton(Transform parent, string name, string label, Action onTap, Look look, bool enabled, string sub = null)
        {
            Color fill;
            switch (look)
            {
                case Look.Primary: fill = enabled ? Theme.Accent : Color.Lerp(Theme.PanelLight, Theme.Card, 0.3f); break;
                case Look.SecondaryAccent: fill = Color.Lerp(Theme.Background, Theme.Panel, 0.8f); break;
                case Look.Ghost: fill = new Color(1, 1, 1, 0f); break;
                default: fill = enabled ? Theme.PanelLight : Color.Lerp(Theme.PanelLight, Theme.Card, 0.5f); break;
            }
            int size = look == Look.Primary ? 28 : 22;
            var b = UiFactory.Button(name, parent, "", enabled ? onTap : null, fill, size);
            if (look == Look.Primary) UiFactory.Slant(b.image, 2.2f);
            if (look == Look.SecondaryAccent) b.gameObject.AddComponent<Outline>().effectColor = Theme.Alpha(Theme.Accent, enabled ? 1f : 0.35f);
            if (look == Look.SecondaryAccent) b.gameObject.GetComponent<Outline>().effectDistance = new Vector2(2, -2);
            b.interactable = enabled && onTap != null;
            var colors = b.colors;
            colors.disabledColor = Color.white;
            b.colors = colors;
            var t = b.GetComponentInChildren<Text>();
            t.supportRichText = true;
            Color text;
            switch (look)
            {
                case Look.Primary: text = enabled ? Theme.OnAccent : Theme.TextDim; break;
                case Look.SecondaryAccent: text = enabled ? Color.Lerp(Theme.Accent, Color.white, 0.25f) : Theme.TextDim; break;
                case Look.Ghost: text = Color.Lerp(Theme.TextDim, Color.white, 0.15f); break;
                default: text = enabled ? Color.white : Theme.TextDim; break;
            }
            t.color = text;
            t.fontStyle = look == Look.Primary ? FontStyle.BoldAndItalic : FontStyle.Bold;
            t.text = Theme.Caps(label ?? "") + (string.IsNullOrEmpty(sub) ? "" : "\n<size=" + Theme.TextCaption + ">" + sub + "</size>");
            if (!string.IsNullOrEmpty(sub)) t.lineSpacing = 0.9f;
            Fit(t, size);
            t.rectTransform.offsetMin = new Vector2(look == Look.Primary ? 22 : 12, 2);
            t.rectTransform.offsetMax = new Vector2(look == Look.Primary ? -22 : -12, -2);
            return b;
        }

        private static Button Filled(RectTransform area, float height, string name, string label, Action onTap, Look look, bool enabled, string sub = null)
        {
            var host = Host(area, height);
            var b = MakeButton(host, name, label, onTap, look, enabled, sub);
            UiFactory.Stretch((RectTransform)b.transform);
            return b;
        }

        /// <summary>
        /// The one main action of a screen: accent fill, slanted, OnAccent bold italic CAPS 28 and an optional 20-unit
        /// sub-line (cost · days). Disabled: graphite, and <paramref name="sub"/> says what blocks it.
        /// </summary>
        public static Button PrimaryCTA(RectTransform area, string label, Action onTap, string sub = null, bool enabled = true)
        {
            return Filled(area, Grid.Cta, "PrimaryCTA", label, onTap, Look.Primary, enabled, sub);
        }

        /// <summary><see cref="PrimaryCTA(RectTransform,string,Action,string,bool)"/> as a 72-unit row of a layout list.</summary>
        public static Button PrimaryCTA(Transform list, string label, Action onTap, string sub = null, bool enabled = true)
        {
            return PrimaryCTA(UiFactory.Row(list, Grid.Cta), label, onTap, sub, enabled);
        }

        /// <summary>A secondary button (graphite fill, white bold CAPS 22) filling the area (64 high in a list).</summary>
        public static Button Secondary(RectTransform area, string label, Action onTap, bool enabled = true)
        {
            return Filled(area, Grid.Button, "Secondary", label, onTap, Look.Secondary, enabled);
        }

        /// <summary>
        /// Secondary with an accent outline and accent text (dark fill): the shell's race button on sub-pages, browse
        /// pages' "go" buttons, hire buttons in lists. Never counts as the screen's primary.
        /// </summary>
        public static Button SecondaryAccent(RectTransform area, string label, Action onTap, bool enabled = true)
        {
            return Filled(area, Grid.Button, "SecondaryAccent", label, onTap, Look.SecondaryAccent, enabled);
        }

        /// <summary>A link: no fill, dim CAPS text and a chevron at the right end (56 high in a list).</summary>
        public static Button Ghost(RectTransform area, string label, Action onTap)
        {
            var b = Filled(area, Grid.Hit, "Ghost", label, onTap, Look.Ghost, true);
            var t = b.GetComponentInChildren<Text>();
            t.alignment = TextAnchor.MiddleRight;
            t.rectTransform.offsetMax = new Vector2(-36, -2);
            var ch = UiIcons.Chevron(b.transform, t.color);
            ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(1, 0.5f);
            ch.rectTransform.sizeDelta = new Vector2(20, 20);
            ch.rectTransform.anchoredPosition = new Vector2(-18, 0);
            return b;
        }

        private static string _armed;

        /// <summary>The key of the destructive button waiting for its confirming tap, or null.</summary>
        public static string ArmedKey { get { return _armed; } }

        /// <summary>Cancels a pending two-tap confirmation (the shell calls it on page change and sheet close).</summary>
        public static void Disarm()
        {
            _armed = null;
        }

        /// <summary>
        /// A destructive action with a built-in two-tap confirmation: Bad at 45% showing <paramref name="label"/>; the
        /// first tap arms it (full Bad, <paramref name="confirmLabel"/>, restyled in place, then
        /// <paramref name="refresh"/> if given); the second runs <paramref name="onConfirm"/>. One armed key at a time
        /// (static), cleared by <see cref="Disarm"/>, <see cref="CloseSheet"/> and page changes.
        /// </summary>
        public static Button Danger(RectTransform area, string label, string confirmLabel, string armKey, Action onConfirm, Action refresh)
        {
            var host = Host(area, Grid.Button);
            bool armed = _armed != null && _armed == armKey;
            var b = UiFactory.Button("Danger", host, "", null, armed ? Theme.Bad : Theme.Alpha(Theme.Bad, 0.45f), 22);
            UiFactory.Stretch((RectTransform)b.transform);
            var t = b.GetComponentInChildren<Text>();
            t.supportRichText = true;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            t.text = Theme.Caps(armed ? confirmLabel : label);
            Fit(t, 22);
            t.rectTransform.offsetMin = new Vector2(12, 2);
            t.rectTransform.offsetMax = new Vector2(-12, -2);
            b.interactable = onConfirm != null;
            b.onClick.AddListener(() =>
            {
                GameAudio.ClickSound();
                if (_armed != null && _armed == armKey)
                {
                    _armed = null;
                    onConfirm();
                    return;
                }
                _armed = armKey;
                b.image.color = Theme.Bad;
                t.text = Theme.Caps(confirmLabel);
                if (refresh != null) refresh();
            });
            return b;
        }

        /// <summary>
        /// A 56×56 touch target with a 40-unit visual: a known UiIcons name as its pictogram, any other string as bold
        /// text (Glyph.More "...", Glyph.Close "X"...). Anchored at its parent's centre; the caller positions it.
        /// </summary>
        public static Button IconButton(Transform parent, string icon, Action onTap, Color? tint = null)
        {
            var hit = UiFactory.Panel("IconButton", parent, new Color(1, 1, 1, 0f));
            var rt = hit.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Grid.Hit, Grid.Hit);
            var plate = UiFactory.Decor("Plate", hit.transform, new Color(1, 1, 1, 0.08f));
            UiFactory.Chamfer(plate, 3f);
            plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            plate.rectTransform.sizeDelta = new Vector2(40, 40);
            var g = Symbol(plate.transform, icon, tint ?? Theme.Text, Theme.TextHeader);
            g.rectTransform.anchorMin = g.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            g.rectTransform.sizeDelta = new Vector2(28, 28);
            var b = hit.gameObject.AddComponent<Button>();
            b.targetGraphic = plate;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
            colors.pressedColor = new Color(2f, 2f, 2f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            b.colors = colors;
            if (onTap != null) b.onClick.AddListener(() =>
            {
                GameAudio.ClickSound();
                onTap();
            });
            else b.interactable = false;
            return b;
        }

        /// <summary>The round "i" (36 visual, 56 hit), centred; caller positions.</summary>
        private static Button InfoIcon(Transform parent, Action onTap, Color color)
        {
            var hit = UiFactory.Panel("Info", parent, new Color(1, 1, 1, 0f));
            var rt = hit.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Grid.Hit, Grid.Hit);
            var ring = UiFactory.Decor("Ring", hit.transform, Theme.Alpha(color, 0.16f));
            UiFactory.Round(ring, 1.1f);
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ring.rectTransform.sizeDelta = new Vector2(36, 36);
            ring.gameObject.AddComponent<Outline>().effectColor = Theme.Alpha(color, 0.5f);
            var t = UiFactory.Label("I", ring.transform, Glyph.Info, Theme.TextHeader, TextAnchor.MiddleCenter, color);
            t.fontStyle = FontStyle.BoldAndItalic;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.Stretch(t.rectTransform);
            var b = hit.gameObject.AddComponent<Button>();
            b.targetGraphic = ring;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
            colors.pressedColor = new Color(2f, 2f, 2f, 1f);
            b.colors = colors;
            b.onClick.AddListener(() =>
            {
                GameAudio.ClickSound();
                onTap();
            });
            return b;
        }

        // ================================================================== info and sheets

        /// <summary>
        /// The "i" at the right end of a header strip: opens a <see cref="Sheet"/> with <paramref name="title"/> and one
        /// paragraph per string. Moves the strip's right text, badge and chevron left to make room.
        /// </summary>
        public static Button InfoButton(Transform headerStrip, string title, params string[] paragraphs)
        {
            var paras = paragraphs ?? new string[0];
            foreach (Transform child in headerStrip)
            {
                var crt = child as RectTransform;
                if (crt == null) continue;
                switch (child.name)
                {
                    case "Right":
                    case "Title":
                        if (child.name == "Title" && headerStrip.Find("Right") != null) break;
                        crt.offsetMax -= new Vector2(Grid.Hit, 0);
                        break;
                    case "Badge":
                    case "Chevron":
                    case "Info":
                        crt.anchoredPosition -= new Vector2(Grid.Hit, 0);
                        break;
                }
            }
            var b = InfoIcon(headerStrip, () => Sheet(title, list =>
            {
                foreach (var p in paras)
                    if (!string.IsNullOrEmpty(p)) UiFactory.Paragraph(list, p, Theme.TextBody);
            }), Color.white);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-2, 0);
            return b;
        }

        /// <summary>
        /// Where sheets open: the shell's safe root, or each race-flow panel's root. When unset (or destroyed) the
        /// top-most canvas is used (its "Safe" child when it has one).
        /// </summary>
        public static Transform SheetHost;
        private static GameObject _sheet;

        /// <summary>True while a sheet is open.</summary>
        public static bool SheetOpen { get { return _sheet != null; } }

        private static Transform ResolveHost()
        {
            if (SheetHost != null && SheetHost.gameObject.activeInHierarchy) return SheetHost;
            Canvas best = null;
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>())
            {
                if (!c.isRootCanvas || !c.gameObject.activeInHierarchy) continue;
                if (best == null || c.sortingOrder > best.sortingOrder) best = c;
            }
            if (best == null) return null;
            var safe = best.transform.Find("Safe");
            return safe != null ? safe : best.transform;
        }

        /// <summary>
        /// A modal sheet: shade at 60% (tap closes), a centred panel <paramref name="width"/> of the host wide and as
        /// tall as its content (at most 85%, then it scrolls), a header strip with a close button, the body built by
        /// <paramref name="build"/> into a vertical list padded 16, and optional buttons at the bottom right (primary
        /// right, secondary to its left; both close the sheet before running). Only one sheet at a time: opening one
        /// closes the previous. Returns the body list.
        /// </summary>
        public static RectTransform Sheet(string title, Action<Transform> build, UiAct? primary = null, UiAct? secondary = null, float width = 0.62f)
        {
            CloseSheet();
            var host = ResolveHost();
            if (host == null) return null;
            var shade = UiFactory.Panel("Sheet", host, new Color(0, 0, 0, 0.6f));
            UiFactory.Stretch(shade.rectTransform);
            var close = shade.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(CloseSheet);
            _sheet = shade.gameObject;

            var panel = UiFactory.Panel("SheetPanel", shade.transform, Theme.Card);
            UiFactory.Gradient(panel, 1.05f, 0.95f);
            UiFactory.DropShadow(panel, 0.6f, 6);
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.1f);
            float w = Mathf.Clamp(width, 0.2f, 0.98f);
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f - w / 2, 0.5f);
            prt.anchorMax = new Vector2(0.5f + w / 2, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(0, 400);

            var head = HeaderStrip(panel.transform, title, null, null, null, null);
            var x = IconButton(head, Glyph.Close, CloseSheet);
            x.gameObject.name = "Close";
            var xrt = (RectTransform)x.transform;
            xrt.anchorMin = xrt.anchorMax = new Vector2(1, 0.5f);
            xrt.pivot = new Vector2(1, 0.5f);
            xrt.anchoredPosition = new Vector2(-2, 0);
            var headTitle = head.Find("Title") as RectTransform;
            if (headTitle != null) headTitle.offsetMax = new Vector2(-Grid.Hit - 8, 0);

            bool buttons = primary.HasValue || secondary.HasValue;
            float footer = buttons ? Grid.Cta + 16 : 0;
            var scroll = UiFactory.Rect("Scroll", panel.transform);
            UiFactory.Stretch(scroll);
            scroll.offsetMin = new Vector2(0, footer);
            scroll.offsetMax = new Vector2(0, -Grid.Header);
            var list = UiFactory.ScrollList("SheetList", scroll, 8);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 8;
            if (build != null) build(list);

            if (buttons)
            {
                var bar = UiFactory.Rect("Buttons", panel.transform);
                bar.anchorMin = new Vector2(0, 0);
                bar.anchorMax = new Vector2(1, 0);
                bar.pivot = new Vector2(0.5f, 0);
                bar.offsetMin = new Vector2(16, 8);
                bar.offsetMax = new Vector2(-16, 8 + Grid.Cta);
                if (primary.HasValue)
                {
                    var p = primary.Value;
                    var area = UiFactory.Rect("Primary", bar);
                    UiFactory.Place(area, 0.55f, 0, 1, 1);
                    PrimaryCTA(area, p.Key, () =>
                    {
                        CloseSheet();
                        if (p.Value != null) p.Value();
                    }, null, p.Value != null);
                }
                if (secondary.HasValue)
                {
                    var s = secondary.Value;
                    var area = UiFactory.Rect("Secondary", bar);
                    UiFactory.Place(area, primary.HasValue ? 0.17f : 0.55f, 0.06f, primary.HasValue ? 0.53f : 1, 0.94f);
                    Secondary(area, s.Key, () =>
                    {
                        CloseSheet();
                        if (s.Value != null) s.Value();
                    }, s.Value != null);
                }
            }
            var fitter = panel.gameObject.AddComponent<SheetFitter>();
            fitter.Content = list;
            fitter.Chrome = Grid.Header + footer;
            fitter.Fit();
            shade.transform.SetAsLastSibling();
            return list;
        }

        /// <summary>Closes the open sheet (if any) and cancels a pending two-tap confirmation.</summary>
        public static void CloseSheet()
        {
            _armed = null;
            if (_sheet == null) return;
            _sheet.SetActive(false);
            UnityEngine.Object.Destroy(_sheet);
            _sheet = null;
        }

        // ================================================================== data atoms

        /// <summary>
        /// A 32-unit slanted pill (CAPS 20 bold), as wide as its text. In a layout row it is a layout element; elsewhere
        /// it sizes itself and the caller sets its anchors / position.
        /// </summary>
        public static Image Pill(Transform parent, string text, Color fill, Color? textColor = null)
        {
            var pill = UiFactory.Panel("Pill", parent, fill);
            pill.raycastTarget = false;
            UiFactory.Slant(pill, 3.2f);
            var h = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 16, 2, 2);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            var le = pill.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 32;
            le.minHeight = 32;
            var t = UiFactory.Label("Text", pill.transform, Theme.Caps(text ?? ""), Theme.TextCaption, TextAnchor.MiddleCenter,
                textColor ?? (fill.a < 0.5f ? Color.white : Theme.OnColor(fill)));
            t.supportRichText = true;
            t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (!IsList(parent))
            {
                var fit = pill.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                pill.rectTransform.sizeDelta = new Vector2(pill.rectTransform.sizeDelta.x, 32);
            }
            return pill;
        }

        /// <summary>
        /// The rating in a coloured square (rating colour, ink number) with a small CAPS tag under it ("VAL") when the
        /// square is at least 72 units. Centred in its parent with <paramref name="side"/> units; caller repositions.
        /// </summary>
        public static Image RatingBadge(Transform parent, double value, string tag, float side)
        {
            bool has = value >= 0;
            return SquareCore(parent, has ? value.ToString("0") : "-", tag, has ? RatingColor(value) : Theme.PanelLight, side);
        }

        /// <summary>A coloured square with a big number and an optional tag (shared by RatingBadge and BikeCards.Square).</summary>
        internal static Image SquareCore(Transform parent, string value, string tag, Color fill, float side)
        {
            var sq = UiFactory.Decor("Rating", parent, fill);
            UiFactory.Gradient(sq, 1.08f, 0.9f);
            UiFactory.DropShadow(sq, 0.35f, 2);
            sq.rectTransform.anchorMin = sq.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            sq.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            sq.rectTransform.sizeDelta = new Vector2(side, side);
            Color ink = Theme.OnColor(fill);
            bool withTag = !string.IsNullOrEmpty(tag) && side >= 72;
            int size = Mathf.RoundToInt(Mathf.Clamp(side * (withTag ? 0.46f : 0.54f), Theme.TextMin, Theme.TextDisplay));
            var n = Txt(sq.transform, "Value", value, size, TextAnchor.MiddleCenter, ink, FontStyle.Bold);
            UiFactory.Place(n.rectTransform, 0, withTag ? 0.3f : 0, 1, 1);
            if (withTag)
            {
                var t = Txt(sq.transform, "Tag", Theme.Caps(tag), Theme.TextMin, TextAnchor.MiddleCenter, Theme.Alpha(ink, 0.8f), FontStyle.Bold);
                t.verticalOverflow = VerticalWrapMode.Overflow;
                UiFactory.Place(t.rectTransform, 0, 0.02f, 1, 0.32f);
            }
            return sq;
        }

        /// <summary>
        /// One row of slanted segments (in-page choices), at most 5: the selected one lighter, bold italic, with a
        /// 4-unit accent underline (never a filled accent). Height at least 56.
        /// </summary>
        public static void Segmented(Transform list, IList<string> labels, int selected, Action<int> onSelect, float height = 56)
        {
            if (labels.Count > 5) Debug.LogWarning("Ui.Segmented: " + labels.Count + " segments (max 5); use Ui.Picker");
            var row = UiFactory.Row(list, Mathf.Max(height, Grid.Hit));
            int n = Mathf.Max(1, labels.Count);
            for (int i = 0; i < labels.Count; i++)
            {
                int idx = i;
                bool on = i == selected;
                var b = UiFactory.Button("Segment", row, Theme.Caps(labels[i]), () => { if (onSelect != null) onSelect(idx); },
                    on ? Theme.PanelLight : Color.Lerp(Theme.Background, Theme.Panel, 0.85f), 22);
                UiFactory.Slant(b.image, 2.6f);
                UiFactory.Place((RectTransform)b.transform, (float)i / n, 0, (float)(i + 1) / n, 1, 3);
                var t = b.GetComponentInChildren<Text>();
                t.supportRichText = true;
                t.fontStyle = on ? FontStyle.BoldAndItalic : FontStyle.Bold;
                t.color = on ? Color.white : Color.Lerp(Theme.TextDim, Color.white, 0.2f);
                Fit(t, 22);
                t.rectTransform.offsetMin = new Vector2(14, 0);
                t.rectTransform.offsetMax = new Vector2(-14, 0);
                if (!on) continue;
                var line = UiFactory.Decor("Underline", b.transform, Theme.CurrentAccent);
                UiFactory.Slant(line, 6f);
                line.rectTransform.anchorMin = new Vector2(0, 0);
                line.rectTransform.anchorMax = new Vector2(1, 0);
                line.rectTransform.offsetMin = new Vector2(4, 0);
                line.rectTransform.offsetMax = new Vector2(-6, 4);
            }
        }

        /// <summary>
        /// A pill showing the selected option and a down chevron ("MOTO2 v"); a tap opens a sheet titled
        /// <paramref name="caption"/> listing every option (tap one to choose it). Fills the area (56 high in a list).
        /// </summary>
        public static Button Picker(RectTransform area, string caption, IList<string> options, int selected, Action<int> onSelect)
        {
            var host = Host(area, Grid.Hit);
            string current = options != null && selected >= 0 && selected < options.Count ? options[selected] : "-";
            var b = UiFactory.Button("Picker", host, Theme.Caps(current), () =>
            {
                Sheet(caption, list =>
                {
                    for (int i = 0; i < options.Count; i++)
                    {
                        int idx = i;
                        bool on = i == selected;
                        var row = ListRow(list, new[] { 1f }, new[] { "  " + (on ? "<b>" + options[i] + "</b>" : options[i]) }, i, on, () =>
                        {
                            CloseSheet();
                            if (onSelect != null) onSelect(idx);
                        });
                        row.gameObject.name = "Option";
                    }
                }, null, null, 0.42f);
            }, Color.Lerp(Theme.Background, Theme.Panel, 0.9f), 22);
            UiFactory.Slant(b.image, 3f);
            b.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.14f);
            UiFactory.Stretch((RectTransform)b.transform);
            var t = b.GetComponentInChildren<Text>();
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            Fit(t, 22);
            t.rectTransform.offsetMin = new Vector2(16, 0);
            t.rectTransform.offsetMax = new Vector2(-44, 0);
            var caret = UiIcons.Chevron(b.transform, Theme.Text, 90);
            caret.rectTransform.anchorMin = caret.rectTransform.anchorMax = new Vector2(1, 0.5f);
            caret.rectTransform.sizeDelta = new Vector2(20, 20);
            caret.rectTransform.anchoredPosition = new Vector2(-26, 0);
            return b;
        }

        /// <summary>[−] VALUE [+] on a 64-unit row, the caption on the left (salary, price, budget).</summary>
        public static void Stepper(Transform list, string label, string value, Action minus, Action plus)
        {
            var row = UiFactory.Row(list, Grid.Row);
            var l = Txt(row, "Label", Theme.Caps(label ?? ""), Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(l.rectTransform, 0, 0, 0.38f, 1);
            l.rectTransform.offsetMin = new Vector2(4, 0);
            var m = MakeButton(row, "Minus", Glyph.Minus, minus, Look.Secondary, minus != null);
            var mrt = (RectTransform)m.transform;
            mrt.anchorMin = new Vector2(0.38f, 0);
            mrt.anchorMax = new Vector2(0.38f, 1);
            mrt.pivot = new Vector2(0, 0.5f);
            mrt.offsetMin = new Vector2(0, 4);
            mrt.offsetMax = new Vector2(Grid.Button, -4);
            m.GetComponentInChildren<Text>().fontSize = Theme.TextTitle;
            Fit(m.GetComponentInChildren<Text>(), Theme.TextTitle);
            var p = MakeButton(row, "Plus", Glyph.Plus, plus, Look.Secondary, plus != null);
            var prt = (RectTransform)p.transform;
            prt.anchorMin = new Vector2(1, 0);
            prt.anchorMax = new Vector2(1, 1);
            prt.pivot = new Vector2(1, 0.5f);
            prt.offsetMin = new Vector2(-Grid.Button, 4);
            prt.offsetMax = new Vector2(0, -4);
            Fit(p.GetComponentInChildren<Text>(), Theme.TextTitle);
            var v = Txt(row, "Value", "<b>" + value + "</b>", Theme.TextBody, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Normal);
            UiFactory.Place(v.rectTransform, 0.38f, 0, 1, 1);
            v.rectTransform.offsetMin = new Vector2(Grid.Button + 8, 0);
            v.rectTransform.offsetMax = new Vector2(-Grid.Button - 8, 0);
        }

        /// <summary>
        /// A 64-unit list row (standings, rankings, any table): alternate shading by <paramref name="index"/>, the first
        /// cell as a slanted position block, the second left-aligned bold, the rest centred (24, rich text). The
        /// player's row (<paramref name="mine"/>) is lit in the accent with an edge bar. With <paramref name="onTap"/>
        /// the whole row is the button and a chevron sits at the right. At most 4 data columns + position.
        /// </summary>
        public static RectTransform ListRow(Transform list, float[] widths, string[] cells, int index, bool mine, Action onTap = null, float height = 64)
        {
            var row = UiFactory.Row(list, height);
            var bg = row.gameObject.AddComponent<Image>();
            bg.raycastTarget = false;
            bg.color = mine ? Theme.Alpha(Theme.CurrentAccent, 0.32f) : index % 2 == 0 ? new Color(1, 1, 1, 0.055f) : new Color(1, 1, 1, 0.02f);
            if (mine)
            {
                UiFactory.Gradient(bg, Color.white, new Color(1, 1, 1, 0.45f), true);
                UiFactory.SideStripe(row, Theme.CurrentAccent, 6, 0);
            }
            if (onTap != null)
            {
                Tappable(bg, onTap);
                var ch = UiIcons.Chevron(row, Theme.TextDim);
                ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(1, 0.5f);
                ch.rectTransform.sizeDelta = new Vector2(24, 24);
                ch.rectTransform.anchoredPosition = new Vector2(-22, 0);
            }
            var area = UiFactory.Rect("Cells", row);
            UiFactory.Stretch(area);
            area.offsetMax = new Vector2(onTap != null ? -44 : 0, 0);
            float total = 0, x = 0;
            foreach (var w in widths) total += w;
            if (total <= 0) total = 1;
            bool posBlock = widths.Length > 1;
            for (int i = 0; i < cells.Length && i < widths.Length; i++)
            {
                float x0 = x / total, x1 = (x + widths[i]) / total;
                x += widths[i];
                if (i == 0 && posBlock)
                {
                    var block = UiFactory.Decor("Pos", area, mine ? Color.white : index < 3 ? new Color(0.9f, 0.91f, 0.93f, 1f) : Theme.PanelLight);
                    UiFactory.Slant(block, 3.2f);
                    UiFactory.Place(block.rectTransform, x0, 0, x1, 1);
                    block.rectTransform.offsetMin = new Vector2(10, 8);
                    block.rectTransform.offsetMax = new Vector2(-2, -8);
                    var n = Txt(block.transform, "Cell0", cells[0] ?? "", Theme.TextBody, TextAnchor.MiddleCenter,
                        mine || index < 3 ? Theme.Ink : Theme.Text, FontStyle.BoldAndItalic);
                    UiFactory.Stretch(n.rectTransform);
                    continue;
                }
                bool name = i == 1 || !posBlock;
                var t = Txt(area, "Cell" + i, cells[i] ?? "", Theme.TextBody, name ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter,
                    Theme.Text, name ? FontStyle.Bold : FontStyle.Normal);
                UiFactory.Place(t.rectTransform, x0, 0, x1, 1);
                t.rectTransform.offsetMin = new Vector2(name ? 14 : 4, 0);
                t.rectTransform.offsetMax = new Vector2(-4, 0);
            }
            return row;
        }

        /// <summary>Former overload (height as the sixth argument): same as ListRow without a tap.</summary>
        public static RectTransform ListRow(Transform list, float[] widths, string[] cells, int index, bool mine, float height)
        {
            return ListRow(list, widths, cells, index, mine, null, height);
        }

        /// <summary>
        /// A 72-unit row for people (staff, candidates, bids): rating badge | NAME (bold 24) over a dim detail line |
        /// figure (bold, 160 wide) | one secondary button (200 wide, accent outline when the act is Primary). The row
        /// is tappable with <paramref name="onTap"/> (chevron when there is no button).
        /// </summary>
        public static RectTransform PersonRow(Transform list, double level, string name, string detail, string figure, Action onTap = null, UiAct? action = null)
        {
            const float h = 72;
            var row = UiFactory.Row(list, h);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = new Color(1, 1, 1, 0.045f);
            bg.raycastTarget = false;
            if (onTap != null) Tappable(bg, onTap);
            var badge = RatingBadge(row, level, null, 56);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0, 0.5f);
            badge.rectTransform.pivot = new Vector2(0, 0.5f);
            badge.rectTransform.anchoredPosition = new Vector2(8, 0);
            float right = 8;
            if (action.HasValue)
            {
                var a = action.Value;
                var b = MakeButton(row, "Action", a.Key, a.Value, a.Primary || a.Selected ? Look.SecondaryAccent : Look.Secondary, a.Value != null);
                var brt = (RectTransform)b.transform;
                brt.anchorMin = new Vector2(1, 0.5f);
                brt.anchorMax = new Vector2(1, 0.5f);
                brt.pivot = new Vector2(1, 0.5f);
                brt.sizeDelta = new Vector2(200, Grid.Hit);
                brt.anchoredPosition = new Vector2(-8, 0);
                right += 212;
            }
            else if (onTap != null)
            {
                var ch = UiIcons.Chevron(row, Theme.TextDim);
                ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(1, 0.5f);
                ch.rectTransform.sizeDelta = new Vector2(24, 24);
                ch.rectTransform.anchoredPosition = new Vector2(-22, 0);
                right += 36;
            }
            if (figure != null)
            {
                var f = Txt(row, "Figure", "<b>" + figure + "</b>", Theme.TextBody, TextAnchor.MiddleRight, Theme.Text, FontStyle.Normal);
                f.rectTransform.anchorMin = new Vector2(1, 0);
                f.rectTransform.anchorMax = new Vector2(1, 1);
                f.rectTransform.pivot = new Vector2(1, 0.5f);
                f.rectTransform.offsetMin = new Vector2(-right - 160, 0);
                f.rectTransform.offsetMax = new Vector2(-right, 0);
                right += 168;
            }
            var n = Txt(row, "Name", "<b>" + (name ?? "") + "</b>", Theme.TextBody, TextAnchor.LowerLeft, Theme.Text, FontStyle.Normal);
            UiFactory.Place(n.rectTransform, 0, 0.5f, 1, 0.94f);
            n.rectTransform.offsetMin = new Vector2(80, 0);
            n.rectTransform.offsetMax = new Vector2(-right, 0);
            var d = Txt(row, "Detail", detail ?? "", Theme.TextCaption, TextAnchor.UpperLeft, Theme.TextDim, FontStyle.Normal);
            UiFactory.Place(d.rectTransform, 0, 0.06f, 1, 0.5f);
            d.rectTransform.offsetMin = new Vector2(80, 0);
            d.rectTransform.offsetMax = new Vector2(-right, 0);
            return row;
        }

        /// <summary>
        /// An empty state centred in a list: a dim pictogram (known UiIcons name) or text symbol at 64, one CAPS line at
        /// 22 and an optional secondary button.
        /// </summary>
        public static void Empty(Transform list, string icon, string text, UiAct? action = null)
        {
            var box = UiFactory.Rect("Empty", list);
            var v = box.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 24, 24);
            v.spacing = 8;
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            if (!string.IsNullOrEmpty(icon))
            {
                var iconRow = UiFactory.Row(box, 64);
                var g = Symbol(iconRow, icon, Theme.Alpha(Theme.TextDim, 0.6f), Theme.TextDisplay);
                g.rectTransform.anchorMin = g.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                g.rectTransform.sizeDelta = new Vector2(64, 64);
            }
            var textRow = UiFactory.Row(box, 40);
            var t = Txt(textRow, "Text", Theme.Caps(text ?? ""), 22, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);
            UiFactory.Stretch(t.rectTransform);
            if (action.HasValue)
            {
                var a = action.Value;
                var bRow = UiFactory.Row(box, Grid.Button);
                var area = UiFactory.Rect("Action", bRow);
                UiFactory.Place(area, 0.3f, 0, 0.7f, 1);
                if (a.Primary) PrimaryCTA(area, a.Key, a.Value, null, a.Value != null);
                else Secondary(area, a.Key, a.Value, a.Value != null);
            }
        }

        /// <summary>A visible one-line warning (Warning colour, or Bad) with a coloured edge and a "!" block.</summary>
        public static void Alert(Transform list, string text, bool bad = false)
        {
            Color c = bad ? Theme.Bad : Theme.Warning;
            var row = UiFactory.Row(list, Grid.Hit);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = Theme.Alpha(c, 0.13f);
            bg.raycastTarget = false;
            UiFactory.SideStripe(row, c, 6, 0);
            var mark = UiFactory.Decor("Mark", row, c);
            UiFactory.Slant(mark, 3f);
            mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = new Vector2(0, 0.5f);
            mark.rectTransform.pivot = new Vector2(0, 0.5f);
            mark.rectTransform.anchoredPosition = new Vector2(16, 0);
            mark.rectTransform.sizeDelta = new Vector2(34, 32);
            var g = UiFactory.Label("Glyph", mark.transform, Glyph.Warning, Theme.TextHeader, TextAnchor.MiddleCenter, Theme.OnColor(c));
            g.fontStyle = FontStyle.Bold;
            UiFactory.Stretch(g.rectTransform);
            var t = Txt(row, "Text", text ?? "", 22, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
            UiFactory.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(62, 0);
            t.rectTransform.offsetMax = new Vector2(-12, 0);
        }

        /// <summary>A thick square-cut gauge filling the area: dark track, quarter ticks, a fill brightening to its tip.</summary>
        public static void Gauge(RectTransform area, double fraction, Color color)
        {
            var host = Host(area, 24);
            var back = UiFactory.Decor("Gauge", host, new Color(0, 0, 0, 0.5f));
            UiFactory.Stretch(back.rectTransform);
            var fill = UiFactory.Decor("Fill", back.transform, color);
            UiFactory.Gradient(fill, new Color(0.75f, 0.75f, 0.75f, 1), Color.white, true);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)fraction), 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            for (int k = 1; k < 4; k++)
            {
                var tick = UiFactory.Decor("Tick", back.transform, new Color(0, 0, 0, 0.55f));
                tick.rectTransform.anchorMin = new Vector2(k * 0.25f, 0);
                tick.rectTransform.anchorMax = new Vector2(k * 0.25f, 1);
                tick.rectTransform.offsetMin = new Vector2(-1, 0);
                tick.rectTransform.offsetMax = new Vector2(1, 0);
            }
        }

        /// <summary>Level pips filling the area: <paramref name="level"/> of <paramref name="max"/> lit in timing yellow.</summary>
        public static void Pips(RectTransform area, int level, int max)
        {
            var holder = Host(area, 24);
            max = Mathf.Max(1, max);
            for (int i = 0; i < max; i++)
            {
                var p = UiFactory.Decor("Pip", holder, i < level ? Theme.Timing : new Color(1, 1, 1, 0.12f));
                UiFactory.Slant(p, 4f);
                UiFactory.Place(p.rectTransform, (float)i / max, 0, (float)(i + 1) / max, 1);
                p.rectTransform.offsetMin = new Vector2(3, 0);
                p.rectTransform.offsetMax = new Vector2(-3, 0);
            }
        }

        /// <summary>A segmented bar (contract seasons): <paramref name="filled"/> of <paramref name="total"/> lit. Caller places it.</summary>
        public static RectTransform Segments(Transform parent, int total, int filled, Color on)
        {
            var bar = UiFactory.Rect("Segments", parent);
            total = Mathf.Max(1, total);
            const float gap = 0.012f;
            for (int i = 0; i < total; i++)
            {
                var seg = UiFactory.Decor("Segment", bar, i < filled ? on : new Color(1, 1, 1, 0.1f));
                UiFactory.Slant(seg, 4f);
                float x0 = (float)i / total, x1 = (float)(i + 1) / total;
                UiFactory.Place(seg.rectTransform, x0 + (i > 0 ? gap : 0), 0, x1 - (i < total - 1 ? gap : 0), 1);
            }
            return bar;
        }

        // ================================================================== existing pieces (restyled)

        /// <summary>Key figures in a row (at most 4): tile look, CAPS caption strip on top, the value in timing yellow.</summary>
        public static void Kpis(Transform list, params string[][] items)
        {
            if (items.Length > 4) Debug.LogWarning("Ui.Kpis: " + items.Length + " figures (max 4)");
            var row = UiFactory.Row(list, 104);
            for (int i = 0; i < items.Length; i++)
            {
                var tile = UiFactory.Panel("Kpi", row, Theme.Tile);
                tile.raycastTarget = false;
                UiFactory.Chamfer(tile, 2.4f);
                UiFactory.Place(tile.rectTransform, (float)i / items.Length, 0, (float)(i + 1) / items.Length, 1, 4);
                UiFactory.Gradient(tile, 1.06f, 0.94f);
                var head = UiFactory.Decor("Head", tile.transform, Theme.HeaderFill);
                head.rectTransform.anchorMin = new Vector2(0, 1);
                head.rectTransform.anchorMax = new Vector2(1, 1);
                head.rectTransform.pivot = new Vector2(0.5f, 1);
                head.rectTransform.offsetMin = new Vector2(0, -36);
                head.rectTransform.offsetMax = new Vector2(-10, 0);
                var c = Txt(head.transform, "Caption", Theme.Caps(items[i].Length > 1 ? items[i][1] : ""), Theme.TextCaption, TextAnchor.MiddleCenter,
                    Color.Lerp(Theme.TextDim, Color.white, 0.25f), FontStyle.Bold);
                UiFactory.Stretch(c.rectTransform);
                c.rectTransform.offsetMin = new Vector2(8, 0);
                c.rectTransform.offsetMax = new Vector2(-4, 0);
                var v = Txt(tile.transform, "Value", items[i].Length > 0 ? items[i][0] : "", Theme.TextKey, TextAnchor.MiddleCenter, Theme.Timing, FontStyle.BoldAndItalic);
                UiFactory.DropShadow(v, 0.45f, 2);
                UiFactory.Stretch(v.rectTransform);
                v.rectTransform.offsetMin = new Vector2(8, 4);
                v.rectTransform.offsetMax = new Vector2(-8, -38);
            }
        }

        public static string[] Kpi(string value, string caption)
        {
            return new[] { value, caption };
        }

        /// <summary>Former name of <see cref="Segmented"/> (same look).</summary>
        public static void Chips(Transform list, IList<string> labels, int selected, Action<int> onSelect, float height = 56)
        {
            Segmented(list, labels, selected, onSelect, height);
        }

        /// <summary>Label, a 0..1 bar and a value on the right.</summary>
        public static void Bar(Transform list, string label, double fraction, string value, Color? color = null, float height = 40)
        {
            var row = UiFactory.Row(list, height);
            var l = Txt(row, "Label", label, Theme.FontSmall, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Normal);
            UiFactory.Place(l.rectTransform, 0, 0, 0.45f, 1);
            var back = UiFactory.Rect("BarArea", row);
            UiFactory.Place(back, 0.46f, 0.3f, 0.86f, 0.7f);
            Gauge(back, fraction, color ?? Theme.Good);
            var v = Txt(row, "Value", value, Theme.FontSmall, TextAnchor.MiddleRight, Theme.Text, FontStyle.Normal);
            UiFactory.Place(v.rectTransform, 0.87f, 0, 1, 1);
        }

        /// <summary>Colour for a 0-100 rating.</summary>
        public static Color RatingColor(double v)
        {
            return v >= 75 ? Theme.Good : v >= 55 ? Theme.Info : v >= 40 ? Theme.Warning : Theme.Bad;
        }

        /// <summary>
        /// A list entry (at least 64 high): bold title, dim detail line(s), a value on the right and up to three
        /// buttons, all secondary (accent outline for the act flagged Primary or Selected).
        /// </summary>
        public static RectTransform Item(Transform list, string title, string detail, string value = null, params UiAct[] actions)
        {
            var row = UiFactory.Rect("Item", list);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = new Color(1, 1, 1, 0.045f);
            bg.raycastTarget = false;
            UiFactory.SideStripe(row, Theme.Steel, 3, 10);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 8, 8, 8);
            h.spacing = 12;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = false;
            h.childForceExpandWidth = false;
            row.gameObject.AddComponent<LayoutElement>().minHeight = Grid.Row;

            var t = UiFactory.Label("Text", row, "<b>" + title + "</b>" + (string.IsNullOrEmpty(detail) ? "" : "\n" + Theme.Dim("<size=" + Theme.TextCaption + ">" + detail + "</size>")),
                Theme.TextBody, TextAnchor.MiddleLeft);
            t.supportRichText = true;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var tl = t.gameObject.AddComponent<LayoutElement>();
            tl.flexibleWidth = 1;
            tl.preferredWidth = 10;
            if (value != null)
            {
                var v = UiFactory.Label("Value", row, value, Theme.TextBody, TextAnchor.MiddleRight);
                v.supportRichText = true;
                v.verticalOverflow = VerticalWrapMode.Overflow;
                var vl = v.gameObject.AddComponent<LayoutElement>();
                vl.preferredWidth = 210;
                vl.minWidth = 150;
            }
            for (int i = 0; i < actions.Length; i++)
            {
                var a = actions[i];
                var b = MakeButton(row, "Action", a.Key, a.Value, a.Primary || a.Selected ? Look.SecondaryAccent : Look.Secondary, a.Value != null);
                var bl = b.gameObject.AddComponent<LayoutElement>();
                bl.preferredWidth = 190;
                bl.minWidth = 140;
                bl.preferredHeight = Grid.Hit;
                bl.minHeight = Grid.Hit;
            }
            return row;
        }

        /// <summary>
        /// A section that stays closed until tapped (for user data lists only, not explanations). Open state is
        /// remembered per key while the game runs.
        /// </summary>
        public static void Expander(Transform list, string key, string title, Action<Transform> build, Action refresh)
        {
            bool open = Open.Contains(key);
            var b = UiFactory.Button("Expander", list, title, () =>
            {
                if (!Open.Remove(key)) Open.Add(key);
                refresh();
            }, new Color(1, 1, 1, open ? 0.08f : 0.05f), Theme.FontSmall);
            b.gameObject.AddComponent<LayoutElement>().preferredHeight = Grid.Hit;
            var label = b.GetComponentInChildren<Text>();
            label.supportRichText = true;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = open ? Theme.Text : Color.Lerp(Theme.TextDim, Color.white, 0.15f);
            label.fontStyle = open ? FontStyle.Bold : FontStyle.Normal;
            label.rectTransform.offsetMin = new Vector2(48, 0);
            var ch = UiIcons.Chevron(b.transform, Theme.CurrentAccent, open ? 90 : 180);
            ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(0, 0.5f);
            ch.rectTransform.sizeDelta = new Vector2(20, 20);
            ch.rectTransform.anchoredPosition = new Vector2(24, 0);
            if (!open) return;
            var inner = UiFactory.Rect("ExpanderContent", list);
            var layout = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 0, 0, 4);
            layout.spacing = 8;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            build(inner);
        }

        public static bool IsOpen(string key)
        {
            return Open.Contains(key);
        }

        public static void SetOpen(string key, bool open)
        {
            if (open) Open.Add(key);
            else Open.Remove(key);
        }

        /// <summary>
        /// Muted note for empty lists and explanations, folded behind a "more information" button when the card already
        /// has content (warnings stay visible). Obsolete: explanations go behind <see cref="InfoButton"/>, warnings are
        /// <see cref="Alert"/>s and empty lists <see cref="Empty"/>.
        /// </summary>
        [Obsolete("Use InfoButton/Alert/Empty")]
        public static void Note(Transform list, string text)
        {
            if (UiScale.Compact && !IsAlert(text) && list.childCount > 1)
            {
                var block = list.Find("InfoBlock");
                if (block == null)
                {
                    var holder = UiFactory.Rect("InfoBlock", list);
                    var v = holder.gameObject.AddComponent<VerticalLayoutGroup>();
                    v.spacing = 4;
                    v.childControlHeight = true;
                    v.childControlWidth = true;
                    v.childForceExpandHeight = false;
                    v.childForceExpandWidth = true;
                    holder.gameObject.SetActive(false);
                    var toggle = UiFactory.Button("InfoToggle", list, Theme.Dim("(i)  " + Loc.T("ui.moreInfo")), () =>
                    {
                        holder.gameObject.SetActive(!holder.gameObject.activeSelf);
                        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)list);
                    }, new Color(1, 1, 1, 0.03f), Theme.TextMin);
                    toggle.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                    toggle.GetComponentInChildren<Text>().supportRichText = true;
                    toggle.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
                    toggle.transform.SetSiblingIndex(holder.GetSiblingIndex());
                    block = holder;
                }
                UiFactory.Paragraph(block, Theme.Dim(text), Theme.TextCaption);
                return;
            }
            UiFactory.Paragraph(list, Theme.Dim(text), Theme.TextCaption);
        }

        private static bool IsAlert(string text)
        {
            return text != null && (text.Contains(Theme.WarnHex) || text.Contains(Theme.BadHex));
        }

        /// <summary>Two-column "label: value" line (44 high, 24).</summary>
        public static void Line(Transform list, string label, string value, bool strong = false)
        {
            var row = UiFactory.Row(list, 44);
            var l = Txt(row, "Label", strong ? "<b>" + label + "</b>" : label, Theme.TextBody, TextAnchor.MiddleLeft, strong ? Theme.Text : Theme.TextDim, FontStyle.Normal);
            UiFactory.Place(l.rectTransform, 0, 0, 0.62f, 1);
            l.rectTransform.offsetMin = new Vector2(4, 0);
            var v = Txt(row, "Value", strong ? "<b>" + value + "</b>" : value, Theme.TextBody, TextAnchor.MiddleRight, Theme.Text, FontStyle.Normal);
            UiFactory.Place(v.rectTransform, 0.62f, 0, 1, 1);
            v.rectTransform.offsetMax = new Vector2(-4, 0);
        }

        /// <summary>A vertical stack with no gaps (a table's band, captions and rows read as one block).</summary>
        public static RectTransform Stack(Transform list, float spacing = 0)
        {
            var rt = UiFactory.Rect("Stack", list);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return rt;
        }

        /// <summary>Accent band on top of a big list (standings): CAPS bold italic title and an optional figure right.</summary>
        public static RectTransform Band(Transform list, string title, string right = null, float height = 56)
        {
            var row = UiFactory.Row(list, height);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = Theme.CurrentAccent;
            bg.raycastTarget = false;
            UiFactory.Gradient(bg, 1.1f, 0.9f);
            Color ink = Theme.OnColor(Theme.CurrentAccent);
            var t = Txt(row, "Title", "<b>" + Theme.Caps(title) + "</b>", Theme.FontNormal, TextAnchor.MiddleLeft, ink, FontStyle.Italic);
            UiFactory.DropShadow(t, 0.3f, 1.5f);
            UiFactory.Place(t.rectTransform, 0, 0, right != null ? 0.65f : 1, 1);
            t.rectTransform.offsetMin = new Vector2(18, 0);
            if (right != null)
            {
                var r = Txt(row, "Right", "<b>" + right + "</b>", Theme.FontSmall, TextAnchor.MiddleRight, ink, FontStyle.Normal);
                UiFactory.Place(r.rectTransform, 0.65f, 0, 1, 1);
                r.rectTransform.offsetMax = new Vector2(-16, 0);
            }
            return row;
        }

        /// <summary>Column captions under a <see cref="Band"/>: dim CAPS 20 on a dark strip.</summary>
        public static RectTransform ListCaptions(Transform list, float[] widths, string[] captions, float height = 40)
        {
            var row = UiFactory.Row(list, Mathf.Max(height, 36));
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.5f);
            bg.raycastTarget = false;
            float total = 0, x = 0;
            foreach (var w in widths) total += w;
            if (total <= 0) total = 1;
            for (int i = 0; i < captions.Length && i < widths.Length; i++)
            {
                var c = Txt(row, "Caption" + i, Theme.Caps(captions[i] ?? ""), Theme.TextCaption,
                    i <= 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, Color.Lerp(Theme.TextDim, Color.white, 0.15f), FontStyle.Bold);
                UiFactory.Place(c.rectTransform, x / total, 0, (x + widths[i]) / total, 1);
                c.rectTransform.offsetMin = new Vector2(i == 0 ? 14 : 8, 0);
                x += widths[i];
            }
            return row;
        }

        /// <summary>Two cards side by side (wide screens); returns both containers.</summary>
        public static void Columns(Transform list, out RectTransform left, out RectTransform right)
        {
            var row = UiFactory.Rect("Columns", list);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = Grid.Gap;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = false;
            h.childForceExpandWidth = true;
            h.childAlignment = TextAnchor.UpperLeft;
            row.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            left = Column(row);
            right = Column(row);
        }

        private static RectTransform Column(Transform parent)
        {
            var c = UiFactory.Rect("Column", parent);
            var v = c.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = Grid.Gap;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            // Equal halves whatever the content.
            var le = c.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.preferredWidth = 0;
            le.minWidth = 0;
            return c;
        }
    }

    /// <summary>Sizes an open sheet to its content (at most 85% of its host), for the first frames after it opens.</summary>
    public class SheetFitter : MonoBehaviour
    {
        public RectTransform Content;
        public float Chrome;
        private int _frames;

        private void LateUpdate()
        {
            Fit();
            if (++_frames > 3) enabled = false;
        }

        public void Fit()
        {
            if (Content == null) return;
            var panel = (RectTransform)transform;
            var parent = panel.parent as RectTransform;
            float maxH = parent != null && parent.rect.height > 0 ? parent.rect.height * 0.85f : 600;
            LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
            float h = Mathf.Max(Content.rect.height, LayoutUtility.GetPreferredHeight(Content)) + Chrome;
            panel.sizeDelta = new Vector2(0, Mathf.Clamp(h, 160, maxH));
        }
    }
}
