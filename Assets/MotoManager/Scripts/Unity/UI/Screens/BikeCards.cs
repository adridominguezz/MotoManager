using System;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// Building blocks of the development section (bike, R&amp;D, facilities), in the look of the home hub: big
    /// graphite tiles with a dark caps header strip and an accent tag, coloured rating squares, big figures, thick
    /// stat bars and large coloured buttons. Everything is anchored inside fixed-height rows of a scroll list, so a
    /// page fits one phone screen and only scrolls when the player opens something long.
    /// Thin wrappers over the design system (<see cref="Ui"/>): new code calls Ui.* directly.
    /// </summary>
    internal static class BikeCards
    {
        public const float Head = Ui.Grid.Header;
        /// <summary>Performance figures (green, like the reference game).</summary>
        public static readonly Color PerfColor = Theme.Perf;
        /// <summary>Reliability figures (purple / magenta).</summary>
        public static readonly Color RelColor = Theme.Rel;
        /// <summary>The "build / start" call to action (blue).</summary>
        public static readonly Color StartColor = new Color32(36, 122, 226, 255);
        /// <summary>The quick "upgrade part" call to action (green).</summary>
        public static readonly Color UpgradeColor = new Color32(52, 168, 92, 255);
        public static readonly Color Ink = Theme.Ink;

        public static string Hex(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }

        public static string Colored(string s, Color c)
        {
            return "<color=" + Hex(c) + ">" + s + "</color>";
        }

        /// <summary>
        /// A big tile placed by anchors: graphite body, a header strip (dark, or the accent when
        /// <paramref name="accentHead"/>) with a slanted tag, the caps title, an optional dim text on the right and an
        /// optional coloured text badge. Tappable when <paramref name="onTap"/> is given. Returns the body.
        /// </summary>
        public static RectTransform Tile(RectTransform parent, float x0, float y0, float x1, float y1, string title,
            Action onTap = null, string badge = null, Color? badgeColor = null, string right = null, bool accentHead = false, bool dim = false)
        {
            var area = UiFactory.Rect("TileArea", parent);
            UiFactory.Place(area, x0, y0, x1, y1, Ui.Grid.Gap / 2);
            var body = Ui.Tile(area, title, onTap, badge != null ? null : right, 0, null, null, dim, null, accentHead ? Theme.Accent : (Color?)null);
            if (badge != null && title != null)
            {
                var head = body.parent.Find("Head");
                if (head != null)
                {
                    var pill = Ui.Pill(head, badge, badgeColor ?? Theme.Bad);
                    var prt = pill.rectTransform;
                    prt.anchorMin = prt.anchorMax = new Vector2(1, 0.5f);
                    prt.pivot = new Vector2(1, 0.5f);
                    prt.anchoredPosition = new Vector2(onTap != null ? -40 : -8, 0);
                    var t = head.Find("Title") as RectTransform;
                    if (t != null) t.offsetMax = new Vector2(-170, 0);
                }
            }
            // Callers lay out the whole body under the header themselves.
            body.offsetMin = Vector2.zero;
            body.offsetMax = new Vector2(0, title != null ? -Head : 0);
            return body;
        }

        /// <summary>
        /// A label that shrinks to fit (never cut): rich text, best fit between a small floor and
        /// <paramref name="size"/>. Its rect must be at least as tall as the floor.
        /// </summary>
        public static Text Text(Transform parent, string text, int size, TextAnchor align, Color color, FontStyle style = FontStyle.Normal)
        {
            var t = UiFactory.Label("Text", parent, text, size, align, color);
            t.supportRichText = true;
            UiFactory.Style(t, style);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.resizeTextForBestFit = true;
            Ui.Fit(t, size);
            return t;
        }

        /// <summary>A label anchored to a fraction of <paramref name="parent"/> with horizontal padding.</summary>
        public static Text At(Transform parent, float x0, float y0, float x1, float y1, string text, int size, TextAnchor align, Color color,
            FontStyle style = FontStyle.Normal, float pad = 12)
        {
            var t = Text(parent, text, size, align, color, style);
            UiFactory.Place(t.rectTransform, x0, y0, x1, y1);
            t.rectTransform.offsetMin = new Vector2(pad, 0);
            t.rectTransform.offsetMax = new Vector2(-pad, 0);
            return t;
        }

        /// <summary>A coloured square with a big dark number (rating, level) centred on (x, y) of its parent.</summary>
        public static Image Square(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, float side, string value, Color color, string caption = null)
        {
            var sq = Ui.SquareCore(parent, value, caption, color, side);
            sq.rectTransform.anchorMin = sq.rectTransform.anchorMax = anchor;
            sq.rectTransform.pivot = pivot;
            sq.rectTransform.anchoredPosition = pos;
            return sq;
        }

        /// <summary>The small slanted race-number plate in the team colour ("#55"), clearly a dorsal, not a rating.</summary>
        public static Image NumberBadge(Transform parent, float x0, float y0, float x1, float y1, int number)
        {
            var badge = UiFactory.Decor("Number", parent, Theme.Accent);
            UiFactory.Slant(badge, 2.4f);
            UiFactory.Place(badge.rectTransform, x0, y0, x1, y1);
            var num = Text(badge.transform, "#" + (number > 0 ? number.ToString() : "-"), Theme.FontNormal, TextAnchor.MiddleCenter, Color.white, FontStyle.BoldAndItalic);
            UiFactory.Stretch(num.rectTransform);
            num.rectTransform.offsetMin = new Vector2(6, 0);
            num.rectTransform.offsetMax = new Vector2(-6, 0);
            return badge;
        }

        /// <summary>A thick square-cut gauge filling a rect (dark track, coloured fill brightening to its tip).</summary>
        public static void Gauge(Transform parent, float x0, float y0, float x1, float y1, double fraction, Color color)
        {
            var area = UiFactory.Rect("GaugeArea", parent);
            UiFactory.Place(area, x0, y0, x1, y1);
            Ui.Gauge(area, fraction, color);
        }

        /// <summary>
        /// A big stat line in a band of its parent: the caps caption on the left, the figure big on the right in
        /// <paramref name="color"/>, and a thick bar under them.
        /// </summary>
        public static void StatRow(Transform parent, float y0, float y1, string caption, string value, double fraction, Color color, float pad = 14)
        {
            var row = UiFactory.Rect("Stat", parent);
            UiFactory.Place(row, 0, y0, 1, y1);
            row.offsetMin = new Vector2(pad, 0);
            row.offsetMax = new Vector2(-pad, 0);
            var c = At(row, 0, 0.36f, 0.6f, 1, Theme.Caps(caption), Theme.TextCaption, TextAnchor.MiddleLeft, Color.Lerp(Theme.TextDim, Color.white, 0.25f), FontStyle.Bold, 0);
            c.rectTransform.offsetMin = Vector2.zero;
            var v = At(row, 0.55f, 0.2f, 1, 1, value, Theme.FontLarge, TextAnchor.MiddleRight, color, FontStyle.BoldAndItalic, 0);
            Gauge(row, 0, 0.06f, 0.62f, 0.3f, fraction, color);
        }

        /// <summary>Level pips: filled squares in timing yellow, empty ones dim.</summary>
        public static void Pips(Transform parent, float x0, float y0, float x1, float y1, int level, int max)
        {
            var area = UiFactory.Rect("PipsArea", parent);
            UiFactory.Place(area, x0, y0, x1, y1);
            Ui.Pips(area, level, max);
        }

        /// <summary>
        /// A large coloured action button (flat capsule, bold caps, optional second line). Disabled buttons stay
        /// visible, greyed, with the reason in the second line.
        /// </summary>
        public static Button BigButton(Transform parent, float x0, float y0, float x1, float y1, string text, string sub, Color color, Action onTap, bool enabled = true, float inset = 4)
        {
            var b = UiFactory.Button("BigButton", parent, "", enabled ? onTap : null, enabled ? color : Color.Lerp(Theme.PanelLight, Theme.Card, 0.3f), Theme.FontNormal);
            UiFactory.Place((RectTransform)b.transform, x0, y0, x1, y1, inset);
            b.interactable = enabled && onTap != null;
            var colors = b.colors;
            colors.disabledColor = Color.white;
            b.colors = colors;
            var label = b.GetComponentInChildren<Text>();
            label.supportRichText = true;
            label.text = "<b>" + Theme.Caps(text) + "</b>" + (string.IsNullOrEmpty(sub) ? "" : "\n<size=" + Theme.TextCaption + ">" + sub + "</size>");
            UiFactory.Style(label, FontStyle.Normal);
            label.color = enabled ? Theme.OnColor(color) : Theme.TextDim;
            Ui.Fit(label, Theme.FontNormal);
            label.rectTransform.offsetMin = new Vector2(18, 2);
            label.rectTransform.offsetMax = new Vector2(-18, -2);
            return b;
        }

        /// <summary>A dark recessed chip with a caps caption over a big coloured figure.</summary>
        public static void StatChip(Transform parent, float x0, float y0, float x1, float y1, string caption, string value, Color color)
        {
            var chip = UiFactory.Decor("Chip", parent, new Color(0, 0, 0, 0.42f));
            UiFactory.Chamfer(chip, 2.4f);
            UiFactory.Place(chip.rectTransform, x0, y0, x1, y1, 4);
            var edge = UiFactory.Decor("Edge", chip.transform, color);
            edge.rectTransform.anchorMin = new Vector2(0, 0);
            edge.rectTransform.anchorMax = new Vector2(1, 0);
            edge.rectTransform.offsetMin = new Vector2(10, 0);
            edge.rectTransform.offsetMax = new Vector2(-10, 4);
            At(chip.transform, 0, 0.6f, 1, 0.98f, Theme.Caps(caption), Theme.TextMin, TextAnchor.MiddleCenter, Color.Lerp(Theme.TextDim, Color.white, 0.2f), FontStyle.Bold, 6);
            var v = At(chip.transform, 0, 0.06f, 1, 0.64f, value, Theme.FontLarge, TextAnchor.MiddleCenter, color, FontStyle.BoldAndItalic, 6);
        }

        /// <summary>A plain dark panel anchored in its parent (sections inside a row).</summary>
        public static Image Plate(Transform parent, float x0, float y0, float x1, float y1, float alpha = 0.32f, float inset = 5)
        {
            var p = UiFactory.Decor("Plate", parent, new Color(0, 0, 0, alpha));
            UiFactory.Chamfer(p, 2f);
            UiFactory.Place(p.rectTransform, x0, y0, x1, y1, inset);
            return p;
        }

        /// <summary>Two columns side by side with relative widths (the left one takes <paramref name="leftShare"/>).</summary>
        public static void Split(Transform list, float leftShare, out RectTransform left, out RectTransform right)
        {
            var row = UiFactory.Rect("Split", list);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = false;
            h.childForceExpandWidth = false;
            h.childAlignment = TextAnchor.UpperLeft;
            left = Column(row, leftShare);
            right = Column(row, 1 - leftShare);
        }

        private static RectTransform Column(Transform parent, float share)
        {
            var c = UiFactory.Rect("Column", parent);
            var v = c.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            var le = c.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = share;
            le.preferredWidth = 0;
            le.minWidth = 0;
            return c;
        }
    }
}
