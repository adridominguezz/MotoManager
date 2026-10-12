using System;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// The building blocks of the team pages (riders, staff, manager) in the hub's tile language: tappable tiles with
    /// an upper-case header strip, coloured rating squares with a small tag, segmented contract bars, attribute tiles
    /// with a coloured edge and big bold buttons. Fixed-layout pieces take anchors; the "card" pieces go in scroll
    /// lists (layout groups).
    /// </summary>
    internal static class TeamCards
    {
        public const float Head = 50;
        public static readonly Color Ink = new Color32(12, 13, 16, 255);

        // ------------------------------------------------------------------ text

        /// <summary>A rich-text label that shrinks to fit (never cut), bold by default.</summary>
        public static Text Text(Transform parent, string name, string text, int max, int min, TextAnchor align, Color? color = null,
            FontStyle style = FontStyle.Bold)
        {
            var t = UiFactory.Label(name, parent, text, max, align, color ?? Theme.Text);
            t.supportRichText = true;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(10, Mathf.Min(min, max));
            t.resizeTextMaxSize = max;
            return t;
        }

        /// <summary>Places a rect by anchors with horizontal paddings (left, right) in canvas units.</summary>
        public static RectTransform Place(RectTransform rt, float x0, float y0, float x1, float y1, float padLeft, float padRight)
        {
            UiFactory.Place(rt, x0, y0, x1, y1);
            rt.offsetMin = new Vector2(padLeft, 0);
            rt.offsetMax = new Vector2(-padRight, 0);
            return rt;
        }

        public static string Hex(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }

        public static string Colored(string s, Color c)
        {
            return "<color=" + Hex(c) + ">" + s + "</color>";
        }

        // ------------------------------------------------------------------ fixed-layout pieces

        /// <summary>
        /// A header strip along the top of <paramref name="parent"/>: dark (or filled with <paramref name="fill"/>),
        /// a slanted accent tag, the upper-case title, an optional chevron and an optional count badge.
        /// </summary>
        public static Image Strip(Transform parent, string title, Color? fill = null, bool chevron = false, int badge = 0, Color? badgeColor = null,
            float height = Head)
        {
            var head = UiFactory.Decor("Head", parent, fill ?? new Color(0, 0, 0, 0.38f));
            head.rectTransform.anchorMin = new Vector2(0, 1);
            head.rectTransform.anchorMax = new Vector2(1, 1);
            head.rectTransform.pivot = new Vector2(0.5f, 1);
            head.rectTransform.offsetMin = new Vector2(0, -height);
            head.rectTransform.offsetMax = Vector2.zero;
            if (fill.HasValue)
            {
                UiFactory.Gradient(head, 1.12f, 0.86f);
                var hatch = UiFactory.Pattern("Hatch", head.transform, UiFactory.StripesSprite, new Color(1, 1, 1, 0.09f), 18);
                UiFactory.Place(hatch.rectTransform, 0.6f, 0, 1, 1);
                UiFactory.Gradient(hatch, new Color(1, 1, 1, 0), Color.white, true);
            }
            var tag = UiFactory.Decor("Tag", head.transform, fill.HasValue ? Color.white : Theme.Accent);
            UiFactory.Slant(tag, 3.2f);
            tag.rectTransform.anchorMin = new Vector2(0, 0.2f);
            tag.rectTransform.anchorMax = new Vector2(0, 0.8f);
            tag.rectTransform.offsetMin = new Vector2(10, 0);
            tag.rectTransform.offsetMax = new Vector2(26, 0);
            float right = badge > 0 ? 64 : chevron ? 40 : 12;
            var t = Text(head.transform, "Title", Theme.Caps(title), Theme.FontSmall + 2, Theme.FontSmall - 10, TextAnchor.MiddleLeft, Color.white,
                FontStyle.BoldAndItalic);
            UiFactory.DropShadow(t, 0.35f, 1.5f);
            Place(t.rectTransform, 0, 0, 1, 1, 36, right);
            if (chevron && badge <= 0)
            {
                var arrow = UiFactory.Label("Arrow", head.transform, "›", Theme.FontLarge, TextAnchor.MiddleRight, fill.HasValue ? Color.white : Theme.TextDim);
                arrow.fontStyle = FontStyle.Bold;
                Place(arrow.rectTransform, 0, 0, 1, 1, 0, 12);
            }
            if (badge > 0) Badge(head.transform, badge, badgeColor ?? Theme.Bad);
            return head;
        }

        /// <summary>A small rounded count on the right of a header strip.</summary>
        public static void Badge(Transform head, int count, Color color)
        {
            var dot = UiFactory.Decor("Badge", head, color);
            UiFactory.Round(dot, 1.2f);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(1, 0.5f);
            dot.rectTransform.pivot = new Vector2(1, 0.5f);
            dot.rectTransform.anchoredPosition = new Vector2(-10, 0);
            dot.rectTransform.sizeDelta = new Vector2(42, 34);
            var n = UiFactory.Label("Count", dot.transform, count.ToString(), Theme.FontSmall, TextAnchor.MiddleCenter, color == Theme.Bad ? Color.white : Ink);
            n.fontStyle = FontStyle.Bold;
            UiFactory.Stretch(n.rectTransform);
        }

        /// <summary>
        /// A big tile (graphite body, header strip, the whole tile tappable when <paramref name="onTap"/> is set),
        /// placed by anchors. Returns the body under the header.
        /// </summary>
        public static RectTransform Tile(Transform parent, float x0, float y0, float x1, float y1, string title, Action onTap,
            Color? strip = null, int badge = 0, Color? badgeColor = null, float inset = 5)
        {
            Graphic bg;
            if (onTap != null)
            {
                var b = UiFactory.Button("Tile", parent, "", onTap, Color.Lerp(Theme.Card, Theme.PanelLight, 0.28f), Theme.FontSmall);
                b.image.sprite = null;
                bg = b.image;
                var label = b.GetComponentInChildren<Text>();
                if (label != null) label.gameObject.SetActive(false);
            }
            else bg = UiFactory.Panel("Tile", parent, Color.Lerp(Theme.Card, Theme.PanelLight, 0.28f));
            UiFactory.Gradient(bg, 1.1f, 0.88f);
            bg.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.06f);
            UiFactory.Place(bg.rectTransform, x0, y0, x1, y1, inset);
            if (title != null) Strip(bg.transform, title, strip, onTap != null, badge, badgeColor);
            var body = UiFactory.Rect("Body", bg.transform);
            UiFactory.Stretch(body);
            body.offsetMax = new Vector2(0, title != null ? -Head : 0);
            return body;
        }

        /// <summary>
        /// The overall rating in a coloured square: the number in dark bold type and a small tag under it ("VAL").
        /// Anchored at <paramref name="anchor"/> of the parent, <paramref name="side"/> units square.
        /// </summary>
        public static Image RatingSquare(Transform parent, double value, string tag, Vector2 anchor, Vector2 position, float side)
        {
            var sq = UiFactory.Decor("Rating", parent, value >= 0 ? Ui.RatingColor(value) : Theme.PanelLight);
            UiFactory.Gradient(sq, 1.08f, 0.9f);
            UiFactory.DropShadow(sq, 0.35f, 2);
            sq.rectTransform.anchorMin = sq.rectTransform.anchorMax = anchor;
            sq.rectTransform.pivot = anchor;
            sq.rectTransform.anchoredPosition = position;
            sq.rectTransform.sizeDelta = new Vector2(side, side);
            var n = Text(sq.transform, "Value", value >= 0 ? value.ToString("0") : "-", (int)(side * 0.52f), 14, TextAnchor.MiddleCenter,
                value >= 0 ? Ink : Theme.Text, FontStyle.Bold);
            UiFactory.Stretch(n.rectTransform);
            if (!string.IsNullOrEmpty(tag) && side >= 56)
            {
                n.rectTransform.offsetMin = new Vector2(2, side * 0.22f);
                var t = Text(sq.transform, "Tag", Theme.Caps(tag), (int)Mathf.Max(12, side * 0.16f), 10, TextAnchor.MiddleCenter, Theme.Alpha(Ink, 0.8f));
                UiFactory.Place(t.rectTransform, 0, 0, 1, 0, 0);
                t.rectTransform.offsetMin = new Vector2(2, 3);
                t.rectTransform.offsetMax = new Vector2(-2, side * 0.27f);
            }
            return sq;
        }

        /// <summary>A segmented bar (contract seasons): <paramref name="filled"/> of <paramref name="total"/> lit.</summary>
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

        /// <summary>A thin vertical bar on the left or right edge (who is better, the level of a value).</summary>
        public static Image Edge(Transform parent, Color color, bool left, float width = 6)
        {
            var e = UiFactory.Decor("Edge", parent, color);
            e.rectTransform.anchorMin = new Vector2(left ? 0 : 1, 0);
            e.rectTransform.anchorMax = new Vector2(left ? 0 : 1, 1);
            e.rectTransform.pivot = new Vector2(left ? 0 : 1, 0.5f);
            e.rectTransform.offsetMin = new Vector2(left ? 0 : -width, 0);
            e.rectTransform.offsetMax = new Vector2(left ? width : 0, 0);
            return e;
        }

        /// <summary>A big bold upper-case button placed by anchors.</summary>
        public static Button BigButton(Transform parent, string text, Action onTap, Color color, float x0, float y0, float x1, float y1,
            int size = Theme.FontNormal, float inset = 4)
        {
            var b = UiFactory.Button("Big", parent, Theme.Caps(text), onTap, color, size);
            UiFactory.Place((RectTransform)b.transform, x0, y0, x1, y1, inset);
            var t = b.GetComponentInChildren<Text>();
            t.supportRichText = true;
            t.fontStyle = FontStyle.BoldAndItalic;
            t.resizeTextMinSize = Theme.FontSmall - 10;
            t.resizeTextMaxSize = size;
            t.rectTransform.offsetMin = new Vector2(10, 4);
            t.rectTransform.offsetMax = new Vector2(-10, -4);
            return b;
        }

        /// <summary>
        /// The team-colour hero panel of a person: a slanted number block, the first name small, the SURNAME big and
        /// bold, a line under it (nationality...) and the rating square on the right.
        /// </summary>
        public static void Hero(Transform parent, float y0, float y1, string number, string first, string last, string under, double rating, string tag)
        {
            var panel = UiFactory.Decor("Hero", parent, Theme.Accent);
            UiFactory.Place(panel.rectTransform, 0, y0, 1, y1);
            UiFactory.Gradient(panel, new Color(1, 1, 1, 0.95f), new Color(0.45f, 0.45f, 0.45f, 0.4f), true);
            var hatch = UiFactory.Pattern("Hatch", panel.transform, UiFactory.StripesSprite, new Color(1, 1, 1, 0.07f), 20);
            UiFactory.Place(hatch.rectTransform, 0.45f, 0, 1, 1);
            UiFactory.Gradient(hatch, new Color(1, 1, 1, 0), Color.white, true);
            const float side = 104;
            RatingSquare(panel.transform, rating, tag, new Vector2(1, 0.5f), new Vector2(-16, 0), side);
            float right = side + 30;
            if (!string.IsNullOrEmpty(number))
            {
                var block = UiFactory.Decor("Number", panel.transform, new Color(0, 0, 0, 0.5f));
                UiFactory.Slant(block, 2.4f);
                block.rectTransform.anchorMin = block.rectTransform.anchorMax = new Vector2(0, 1);
                block.rectTransform.pivot = new Vector2(0, 1);
                block.rectTransform.anchoredPosition = new Vector2(14, -12);
                block.rectTransform.sizeDelta = new Vector2(88, 40);
                var n = Text(block.transform, "N", number, Theme.FontNormal, 14, TextAnchor.MiddleCenter, Color.white, FontStyle.BoldAndItalic);
                UiFactory.Stretch(n.rectTransform);
            }
            var f = Text(panel.transform, "First", first ?? "", Theme.FontNormal, 12, TextAnchor.LowerLeft, new Color(1, 1, 1, 0.9f), FontStyle.Italic);
            UiFactory.DropShadow(f, 0.45f, 1.5f);
            Place(f.rectTransform, 0, 0.5f, 1, 0.7f, 20, right);
            var l = Text(panel.transform, "Last", Theme.Caps(last ?? ""), Theme.FontTitle, 16, TextAnchor.MiddleLeft, Color.white, FontStyle.BoldAndItalic);
            UiFactory.DropShadow(l, 0.5f, 2);
            Place(l.rectTransform, 0, 0.2f, 1, 0.52f, 16, right);
            if (!string.IsNullOrEmpty(under))
            {
                var u = Text(panel.transform, "Under", under, Theme.FontSmall, 12, TextAnchor.MiddleLeft, new Color(1, 1, 1, 0.85f));
                UiFactory.DropShadow(u, 0.45f, 1.5f);
                Place(u.rectTransform, 0, 0.02f, 1, 0.21f, 20, right);
            }
        }

        /// <summary>A dark key line ("EDAD 27  MORAL 64 ..."), placed by anchors.</summary>
        public static Text KeyLine(Transform parent, float y0, float y1, string text)
        {
            var bg = UiFactory.Decor("KeyLine", parent, new Color(0, 0, 0, 0.45f));
            UiFactory.Place(bg.rectTransform, 0, y0, 1, y1);
            var t = Text(bg.transform, "Text", text, Theme.FontSmall + 2, 12, TextAnchor.MiddleCenter);
            Place(t.rectTransform, 0, 0, 1, 1, 14, 14);
            return t;
        }

        /// <summary>Caption and value as rich text for a key line.</summary>
        public static string Key(string caption, string value)
        {
            return Theme.Dim("<size=" + (Theme.FontSmall - 4) + ">" + Theme.Caps(caption) + "</size>") + " <b>" + value + "</b>";
        }

        /// <summary>A highlighted box with a big pictogram, a caps title and one line of effect, placed by anchors.</summary>
        public static void Highlight(Transform parent, float y0, float y1, string icon, string title, string line, Color color)
        {
            var box = UiFactory.Decor("Trait", parent, Theme.Alpha(color, 0.13f));
            UiFactory.Place(box.rectTransform, 0, y0, 1, y1);
            box.rectTransform.offsetMin = new Vector2(10, 4);
            box.rectTransform.offsetMax = new Vector2(-10, -4);
            Edge(box.transform, color, true, 6);
            var img = UiIcons.Icon(box.transform, icon, color);
            UiFactory.Place(img.rectTransform, 0.03f, 0.12f, 0.2f, 0.88f);
            var t = Text(box.transform, "Title", Theme.Caps(title), Theme.FontNormal, 12, TextAnchor.LowerLeft, Color.white, FontStyle.BoldAndItalic);
            UiFactory.Place(t.rectTransform, 0.22f, 0.5f, 1, 0.94f);
            t.rectTransform.offsetMax = new Vector2(-10, 0);
            var l = Text(box.transform, "Line", line, Theme.FontSmall - 2, 11, TextAnchor.UpperLeft, Color.Lerp(Theme.TextDim, Color.white, 0.3f), FontStyle.Normal);
            UiFactory.Place(l.rectTransform, 0.22f, 0.06f, 1, 0.5f);
            l.rectTransform.offsetMax = new Vector2(-10, -2);
        }

        /// <summary>
        /// An attribute tile: the caption in caps on a dark strip with a small ⓘ, one big number and a coloured
        /// left edge by value. A tap runs <paramref name="onInfo"/> (an explanation).
        /// </summary>
        public static void ValueTile(Transform parent, float x0, float y0, float x1, float y1, string caption, string value, double level, Action onInfo)
        {
            var b = UiFactory.Button("ValueTile", parent, "", onInfo, Color.Lerp(Theme.Card, Theme.PanelLight, 0.35f), Theme.FontSmall);
            b.image.sprite = null;
            UiFactory.Gradient(b.image, 1.08f, 0.9f);
            UiFactory.Place((RectTransform)b.transform, x0, y0, x1, y1, 4);
            b.interactable = onInfo != null;
            var colors = b.colors;
            colors.disabledColor = Color.white;
            b.colors = colors;
            var label = b.GetComponentInChildren<Text>();
            if (label != null) label.gameObject.SetActive(false);
            var head = UiFactory.Decor("Head", b.transform, new Color(0, 0, 0, 0.38f));
            UiFactory.Place(head.rectTransform, 0, 0.64f, 1, 1);
            var c = Text(head.transform, "Caption", Theme.Caps(caption), Theme.FontSmall - 2, 10, TextAnchor.MiddleLeft, Color.Lerp(Theme.TextDim, Color.white, 0.35f));
            Place(c.rectTransform, 0, 0, 1, 1, 16, onInfo != null ? 34 : 8);
            if (onInfo != null)
            {
                var i = UiFactory.Label("Info", head.transform, "ⓘ", Theme.FontSmall, TextAnchor.MiddleRight, Theme.TextDim);
                Place(i.rectTransform, 0, 0, 1, 1, 0, 8);
            }
            Edge(b.transform, level >= 0 ? Ui.RatingColor(level) : Theme.Steel, true, 7);
            var v = Text(b.transform, "Value", value, Theme.FontTitle - 6, 16, TextAnchor.MiddleCenter, Color.white, FontStyle.BoldAndItalic);
            UiFactory.DropShadow(v, 0.4f, 2);
            UiFactory.Place(v.rectTransform, 0, 0.02f, 1, 0.64f);
        }

        // ------------------------------------------------------------------ scroll-list pieces

        /// <summary>
        /// A card for scroll lists: a header strip (caps title, optional right text) over a padded body. Returns the
        /// body; add rows (Ui.Line, rows, buttons) to it.
        /// </summary>
        public static RectTransform Card(Transform list, string title, string right = null, Color? strip = null)
        {
            var stack = Ui.Stack(list);
            var bg = stack.gameObject.AddComponent<Image>();
            bg.color = Color.Lerp(Theme.Card, Theme.PanelLight, 0.2f);
            bg.raycastTarget = false;
            UiFactory.Gradient(bg, 1.06f, 0.92f);
            stack.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.05f);
            if (title != null)
            {
                var row = UiFactory.Row(stack, Head);
                var head = Strip(row, title, strip, false, 0, null, Head);
                if (right != null)
                {
                    var r = Text(row, "Right", right, Theme.FontSmall, 11, TextAnchor.MiddleRight);
                    Place(r.rectTransform, 0.5f, 0, 1, 1, 0, 14);
                    var t = head.transform.Find("Title");
                    if (t != null) ((RectTransform)t).anchorMax = new Vector2(0.55f, 1);
                }
            }
            var body = UiFactory.Rect("Body", stack);
            var v = body.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = UiScale.Compact ? new RectOffset(12, 12, 8, 12) : new RectOffset(16, 16, 12, 16);
            v.spacing = UiScale.Compact ? 6 : 8;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return body;
        }

        /// <summary>A section header for scroll lists (a strip on its own, no card under it).</summary>
        public static RectTransform Section(Transform list, string title, string right = null)
        {
            var row = UiFactory.Row(list, Head);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.001f);
            bg.raycastTarget = false;
            var head = Strip(row, title, null, false, 0, null, Head);
            if (right != null)
            {
                var r = Text(row, "Right", right, Theme.FontSmall, 11, TextAnchor.MiddleRight);
                Place(r.rectTransform, 0.5f, 0, 1, 1, 0, 14);
                var t = head.transform.Find("Title");
                if (t != null) ((RectTransform)t).anchorMax = new Vector2(0.55f, 1);
            }
            return row;
        }

        /// <summary>A grid of tiles inside a scroll list: one row of <paramref name="height"/> units per line.</summary>
        public static RectTransform GridRow(Transform list, float height)
        {
            return UiFactory.Row(list, height);
        }

        /// <summary>
        /// A big list row for people (staff holders, candidates): rating square on the left, the name and a detail
        /// line, an optional figure, and the row returned so a button can be placed on the right.
        /// </summary>
        public static RectTransform PersonRow(Transform list, double level, string tag, string name, string detail, string figure, float buttonShare,
            float height = 92)
        {
            var row = UiFactory.Row(list, height);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = new Color(1, 1, 1, 0.045f);
            bg.raycastTarget = false;
            float side = height - 16;
            RatingSquare(row, level, tag, new Vector2(0, 0.5f), new Vector2(8, 0), side);
            float textRight = buttonShare + (figure != null ? 0.2f : 0);
            var n = Text(row, "Name", "<b>" + name + "</b>", Theme.FontNormal, 12, TextAnchor.LowerLeft, Theme.Text, FontStyle.Normal);
            UiFactory.Place(n.rectTransform, 0, 0.48f, 1 - textRight, 0.94f);
            n.rectTransform.offsetMin = new Vector2(side + 22, 0);
            n.rectTransform.offsetMax = new Vector2(-6, 0);
            var d = Text(row, "Detail", detail ?? "", Theme.FontSmall - 2, 11, TextAnchor.UpperLeft, Theme.TextDim, FontStyle.Normal);
            UiFactory.Place(d.rectTransform, 0, 0.06f, 1 - textRight, 0.48f);
            d.rectTransform.offsetMin = new Vector2(side + 22, 0);
            d.rectTransform.offsetMax = new Vector2(-6, -2);
            if (figure != null)
            {
                var f = Text(row, "Figure", figure, Theme.FontNormal, 12, TextAnchor.MiddleCenter);
                UiFactory.Place(f.rectTransform, 1 - textRight, 0, 1 - buttonShare, 1);
                f.rectTransform.offsetMin = new Vector2(4, 0);
                f.rectTransform.offsetMax = new Vector2(-4, 0);
            }
            return row;
        }
    }
}
