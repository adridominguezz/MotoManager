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
    /// </summary>
    internal static class BikeCards
    {
        public const float Head = 46;
        /// <summary>Performance figures (green, like the reference game).</summary>
        public static readonly Color PerfColor = new Color32(70, 200, 110, 255);
        /// <summary>Reliability figures (purple / magenta).</summary>
        public static readonly Color RelColor = new Color32(196, 92, 226, 255);
        /// <summary>The "build / start" call to action (blue).</summary>
        public static readonly Color StartColor = new Color32(36, 122, 226, 255);
        /// <summary>The quick "upgrade part" call to action (green).</summary>
        public static readonly Color UpgradeColor = new Color32(52, 168, 92, 255);
        public static readonly Color Ink = new Color32(12, 13, 16, 255);

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
            Image bg;
            Color body = Color.Lerp(Theme.Card, Theme.PanelLight, dim ? 0.1f : 0.28f);
            if (onTap != null)
            {
                var b = UiFactory.Button("Tile", parent, "", onTap, body, Theme.FontSmall);
                bg = b.image;
                var label = b.GetComponentInChildren<Text>();
                if (label != null) label.gameObject.SetActive(false);
            }
            else
            {
                bg = UiFactory.Panel("Tile", parent, body);
            }
            bg.sprite = null;
            UiFactory.Gradient(bg, 1.1f, 0.88f);
            bg.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.06f);
            UiFactory.Place(bg.rectTransform, x0, y0, x1, y1, 5);

            var head = UiFactory.Decor("Head", bg.transform, accentHead ? Theme.Accent : new Color(0, 0, 0, 0.38f));
            head.rectTransform.anchorMin = new Vector2(0, 1);
            head.rectTransform.anchorMax = new Vector2(1, 1);
            head.rectTransform.pivot = new Vector2(0.5f, 1);
            head.rectTransform.offsetMin = new Vector2(0, -Head);
            head.rectTransform.offsetMax = Vector2.zero;
            if (accentHead)
            {
                UiFactory.Gradient(head, 1.12f, 0.86f);
                var hatch = UiFactory.Pattern("Hatch", head.transform, UiFactory.StripesSprite, new Color(1, 1, 1, 0.1f), 18);
                UiFactory.Place(hatch.rectTransform, 0.62f, 0, 1, 1);
                UiFactory.Gradient(hatch, new Color(1, 1, 1, 0), Color.white, true);
            }
            var tag = UiFactory.Decor("Tag", head.transform, accentHead ? Color.white : dim ? (Color)Theme.Steel : Theme.Accent);
            UiFactory.Slant(tag, 3.2f);
            tag.rectTransform.anchorMin = new Vector2(0, 0.2f);
            tag.rectTransform.anchorMax = new Vector2(0, 0.8f);
            tag.rectTransform.offsetMin = new Vector2(10, 0);
            tag.rectTransform.offsetMax = new Vector2(26, 0);

            float rightSpace = badge != null ? 170 : right != null ? 260 : onTap != null ? 26 : 10;
            var t = Text(head.transform, Theme.Caps(title), Theme.FontSmall + 2, TextAnchor.MiddleLeft, dim ? Theme.TextDim : Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(36, 0);
            t.rectTransform.offsetMax = new Vector2(-rightSpace, 0);
            if (right != null)
            {
                var r = Text(head.transform, right, Theme.FontSmall - 2, TextAnchor.MiddleRight, accentHead ? new Color(1, 1, 1, 0.85f) : Theme.TextDim, FontStyle.Italic);
                UiFactory.Place(r.rectTransform, 1, 0, 1, 1);
                r.rectTransform.offsetMin = new Vector2(-(rightSpace - 6), 0);
                r.rectTransform.offsetMax = new Vector2(-12, 0);
            }
            if (badge != null)
            {
                var dot = UiFactory.Decor("Badge", head.transform, badgeColor ?? Theme.Bad);
                UiFactory.Slant(dot, 2.4f);
                dot.rectTransform.anchorMin = new Vector2(1, 0.14f);
                dot.rectTransform.anchorMax = new Vector2(1, 0.86f);
                dot.rectTransform.pivot = new Vector2(1, 0.5f);
                dot.rectTransform.offsetMin = new Vector2(-160, 0);
                dot.rectTransform.offsetMax = new Vector2(-8, 0);
                var n = Text(dot.transform, Theme.Caps(badge), Theme.FontSmall - 2, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
                UiFactory.Stretch(n.rectTransform);
                n.rectTransform.offsetMin = new Vector2(10, 0);
                n.rectTransform.offsetMax = new Vector2(-10, 0);
            }
            else if (onTap != null && right == null)
            {
                var arrow = Text(head.transform, "›", Theme.FontNormal, TextAnchor.MiddleRight, Theme.TextDim, FontStyle.Normal);
                UiFactory.Stretch(arrow.rectTransform);
                arrow.rectTransform.offsetMax = new Vector2(-10, 0);
            }

            var bodyRt = UiFactory.Rect("Body", bg.transform);
            UiFactory.Stretch(bodyRt);
            bodyRt.offsetMax = new Vector2(0, -Head);
            return bodyRt;
        }

        /// <summary>
        /// A label that shrinks to fit (never cut): rich text, best fit between a small floor and
        /// <paramref name="size"/>. Its rect must be at least as tall as the floor.
        /// </summary>
        public static Text Text(Transform parent, string text, int size, TextAnchor align, Color color, FontStyle style = FontStyle.Normal)
        {
            var t = UiFactory.Label("Text", parent, text, size, align, color);
            t.supportRichText = true;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Min(size, 12);
            t.resizeTextMaxSize = size;
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
            var sq = UiFactory.Decor("Square", parent, color);
            UiFactory.Gradient(sq, 1.1f, 0.88f);
            sq.rectTransform.anchorMin = sq.rectTransform.anchorMax = anchor;
            sq.rectTransform.pivot = pivot;
            sq.rectTransform.anchoredPosition = pos;
            sq.rectTransform.sizeDelta = new Vector2(side, side);
            int size = Mathf.RoundToInt(Mathf.Clamp(side * (caption != null ? 0.48f : 0.56f), 16, Theme.FontTitle));
            var v = Text(sq.transform, value, size, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            UiFactory.Place(v.rectTransform, 0, caption != null ? 0.3f : 0, 1, 1);
            if (caption != null)
            {
                var c = Text(sq.transform, Theme.Caps(caption), Mathf.Max(12, Mathf.RoundToInt(side * 0.17f)), TextAnchor.MiddleCenter, new Color(0, 0, 0, 0.75f), FontStyle.Bold);
                UiFactory.Place(c.rectTransform, 0, 0.04f, 1, 0.34f);
            }
            return sq;
        }

        /// <summary>The small slanted race-number plate in the team colour ("#55"), clearly a dorsal, not a rating.</summary>
        public static Image NumberBadge(Transform parent, float x0, float y0, float x1, float y1, int number)
        {
            var badge = UiFactory.Decor("Number", parent, Theme.Accent);
            UiFactory.Slant(badge, 2.4f);
            UiFactory.Place(badge.rectTransform, x0, y0, x1, y1);
            var num = Text(badge.transform, "#" + (number > 0 ? number.ToString() : "-"), Theme.FontNormal, TextAnchor.MiddleCenter, Color.white, FontStyle.BoldAndItalic);
            UiFactory.DropShadow(num, 0.4f, 1.5f);
            UiFactory.Stretch(num.rectTransform);
            num.rectTransform.offsetMin = new Vector2(6, 0);
            num.rectTransform.offsetMax = new Vector2(-6, 0);
            return badge;
        }

        /// <summary>A thick square-cut gauge filling a rect (dark track, coloured fill brightening to its tip).</summary>
        public static void Gauge(Transform parent, float x0, float y0, float x1, float y1, double fraction, Color color)
        {
            var back = UiFactory.Decor("Gauge", parent, new Color(0, 0, 0, 0.5f));
            UiFactory.Place(back.rectTransform, x0, y0, x1, y1);
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
            var c = At(row, 0, 0.36f, 0.6f, 1, Theme.Caps(caption), Theme.FontSmall - 2, TextAnchor.MiddleLeft, Color.Lerp(Theme.TextDim, Color.white, 0.25f), FontStyle.Bold, 0);
            c.rectTransform.offsetMin = Vector2.zero;
            var v = At(row, 0.55f, 0.2f, 1, 1, value, Theme.FontLarge, TextAnchor.MiddleRight, color, FontStyle.BoldAndItalic, 0);
            UiFactory.DropShadow(v, 0.45f, 2);
            Gauge(row, 0, 0.06f, 0.62f, 0.3f, fraction, color);
        }

        /// <summary>Level pips: filled squares in timing yellow, empty ones dim.</summary>
        public static void Pips(Transform parent, float x0, float y0, float x1, float y1, int level, int max)
        {
            var holder = UiFactory.Rect("Pips", parent);
            UiFactory.Place(holder, x0, y0, x1, y1);
            for (int i = 0; i < max; i++)
            {
                var p = UiFactory.Decor("Pip", holder, i < level ? Theme.Timing : new Color(1, 1, 1, 0.12f));
                UiFactory.Slant(p, 4f);
                UiFactory.Place(p.rectTransform, (float)i / max, 0, (float)(i + 1) / max, 1);
                p.rectTransform.offsetMin = new Vector2(3, 0);
                p.rectTransform.offsetMax = new Vector2(-3, 0);
            }
        }

        /// <summary>
        /// A large coloured action button (bold italic caps, optional second line). Disabled buttons stay visible,
        /// greyed, with the reason in the second line.
        /// </summary>
        public static Button BigButton(Transform parent, float x0, float y0, float x1, float y1, string text, string sub, Color color, Action onTap, bool enabled = true, float inset = 4)
        {
            var b = UiFactory.Button("BigButton", parent, "", onTap, enabled ? color : Color.Lerp(Theme.PanelLight, Theme.Card, 0.3f), Theme.FontNormal);
            UiFactory.Slant(b.image, 2.2f);
            UiFactory.Gradient(b.image, 1.16f, 0.84f);
            b.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, enabled ? 0.16f : 0.05f);
            UiFactory.Place((RectTransform)b.transform, x0, y0, x1, y1, inset);
            b.interactable = enabled && onTap != null;
            var colors = b.colors;
            colors.disabledColor = Color.white;
            b.colors = colors;
            var label = b.GetComponentInChildren<Text>();
            label.supportRichText = true;
            label.text = "<b>" + Theme.Caps(text) + "</b>" + (string.IsNullOrEmpty(sub) ? "" : "\n<size=" + (Theme.FontSmall - 4) + ">" + sub + "</size>");
            label.fontStyle = FontStyle.Italic;
            label.color = enabled ? Color.white : Theme.TextDim;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = Theme.FontNormal;
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
            At(chip.transform, 0, 0.6f, 1, 0.98f, Theme.Caps(caption), Theme.FontSmall - 4, TextAnchor.MiddleCenter, Color.Lerp(Theme.TextDim, Color.white, 0.2f), FontStyle.Bold, 6);
            var v = At(chip.transform, 0, 0.06f, 1, 0.64f, value, Theme.FontLarge, TextAnchor.MiddleCenter, color, FontStyle.BoldAndItalic, 6);
            UiFactory.DropShadow(v, 0.45f, 2);
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
