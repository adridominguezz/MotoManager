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
    /// Thin wrappers over the design system (<see cref="Ui"/>): new code calls Ui.* directly.
    /// </summary>
    internal static class TeamCards
    {
        public const float Head = Ui.Grid.Header;
        public static readonly Color Ink = Theme.Ink;

        // ------------------------------------------------------------------ text

        /// <summary>A rich-text label that shrinks to fit (never cut), bold by default.</summary>
        public static Text Text(Transform parent, string name, string text, int max, int min, TextAnchor align, Color? color = null,
            FontStyle style = FontStyle.Bold)
        {
            var t = UiFactory.Label(name, parent, text, max, align, color ?? Theme.Text);
            t.supportRichText = true;
            UiFactory.Style(t, style);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = Mathf.Max(max, Theme.TextMin);
            t.resizeTextMinSize = Theme.TextMin;
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
            var head = Ui.HeaderStrip(parent, title, null, null, null, fill, badge, badgeColor, chevron);
            if (Math.Abs(height - Head) > 0.01f) head.offsetMin = new Vector2(0, -height);
            return head.GetComponent<Image>();
        }

        /// <summary>A small rounded count on the right of a header strip.</summary>
        public static void Badge(Transform head, int count, Color color)
        {
            Ui.Badge(head, count, color);
        }

        /// <summary>
        /// A big tile (graphite body, header strip, the whole tile tappable when <paramref name="onTap"/> is set),
        /// placed by anchors. Returns the body under the header.
        /// </summary>
        public static RectTransform Tile(Transform parent, float x0, float y0, float x1, float y1, string title, Action onTap,
            Color? strip = null, int badge = 0, Color? badgeColor = null, float inset = 5)
        {
            var area = UiFactory.Rect("TileArea", parent);
            UiFactory.Place(area, x0, y0, x1, y1, inset);
            var body = Ui.Tile(area, title, onTap, null, badge, badgeColor, null, false, null, strip);
            // Callers lay out the whole body under the header themselves.
            body.offsetMin = Vector2.zero;
            body.offsetMax = new Vector2(0, title != null ? -Head : 0);
            return body;
        }

        /// <summary>
        /// The overall rating in a coloured square: the number in dark bold type and a small tag under it ("VAL").
        /// Anchored at <paramref name="anchor"/> of the parent, <paramref name="side"/> units square.
        /// </summary>
        public static Image RatingSquare(Transform parent, double value, string tag, Vector2 anchor, Vector2 position, float side)
        {
            var sq = Ui.RatingBadge(parent, value, tag, side);
            sq.rectTransform.anchorMin = sq.rectTransform.anchorMax = anchor;
            sq.rectTransform.pivot = anchor;
            sq.rectTransform.anchoredPosition = position;
            return sq;
        }

        /// <summary>A segmented bar (contract seasons): <paramref name="filled"/> of <paramref name="total"/> lit.</summary>
        public static RectTransform Segments(Transform parent, int total, int filled, Color on)
        {
            return Ui.Segments(parent, total, filled, on);
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
            UiFactory.Style(t, FontStyle.BoldAndItalic);
            Ui.Fit(t, size);
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
                var n = Text(block.transform, "N", number, Theme.FontNormal, Theme.TextMin, TextAnchor.MiddleCenter, Color.white, FontStyle.BoldAndItalic);
                UiFactory.Stretch(n.rectTransform);
            }
            Color ink = Theme.OnAccent;
            var f = Text(panel.transform, "First", first ?? "", Theme.FontNormal, Theme.TextMin, TextAnchor.LowerLeft, Theme.Alpha(ink, 0.9f), FontStyle.Italic);
            Place(f.rectTransform, 0, 0.5f, 1, 0.7f, 20, right);
            var l = Text(panel.transform, "Last", Theme.Caps(last ?? ""), Theme.FontTitle, Theme.TextMin, TextAnchor.MiddleLeft, ink, FontStyle.BoldAndItalic);
            Place(l.rectTransform, 0, 0.2f, 1, 0.52f, 16, right);
            if (!string.IsNullOrEmpty(under))
            {
                var u = Text(panel.transform, "Under", under, Theme.FontSmall, Theme.TextMin, TextAnchor.MiddleLeft, Theme.Alpha(ink, 0.85f));
                Place(u.rectTransform, 0, 0.02f, 1, 0.21f, 20, right);
            }
        }

        /// <summary>A dark key line ("EDAD 27  MORAL 64 ..."), placed by anchors.</summary>
        public static Text KeyLine(Transform parent, float y0, float y1, string text)
        {
            var bg = UiFactory.Decor("KeyLine", parent, new Color(0, 0, 0, 0.45f));
            UiFactory.Place(bg.rectTransform, 0, y0, 1, y1);
            var t = Text(bg.transform, "Text", text, Theme.FontSmall + 2, Theme.TextMin, TextAnchor.MiddleCenter);
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
            var t = Text(box.transform, "Title", Theme.Caps(title), Theme.FontNormal, Theme.TextMin, TextAnchor.LowerLeft, Color.white, FontStyle.BoldAndItalic);
            UiFactory.Place(t.rectTransform, 0.22f, 0.5f, 1, 0.94f);
            t.rectTransform.offsetMax = new Vector2(-10, 0);
            var l = Text(box.transform, "Line", line, Theme.TextCaption, Theme.TextMin, TextAnchor.UpperLeft, Color.Lerp(Theme.TextDim, Color.white, 0.3f), FontStyle.Normal);
            UiFactory.Place(l.rectTransform, 0.22f, 0.06f, 1, 0.5f);
            l.rectTransform.offsetMax = new Vector2(-10, -2);
        }

        /// <summary>
        /// An attribute tile: the caption in caps on a dark strip with a small "i", one big number and a coloured
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
            var c = Text(head.transform, "Caption", Theme.Caps(caption), Theme.TextCaption, Theme.TextMin, TextAnchor.MiddleLeft, Color.Lerp(Theme.TextDim, Color.white, 0.35f));
            Place(c.rectTransform, 0, 0, 1, 1, 16, onInfo != null ? 34 : 8);
            if (onInfo != null)
            {
                var i = UiFactory.Label("Info", head.transform, "(" + Glyph.Info + ")", Theme.TextCaption, TextAnchor.MiddleRight, Theme.TextDim);
                UiFactory.Style(i, FontStyle.BoldAndItalic);
                Place(i.rectTransform, 0, 0, 1, 1, 0, 8);
            }
            Edge(b.transform, level >= 0 ? Ui.RatingColor(level) : Theme.Steel, true, 7);
            var v = Text(b.transform, "Value", value, Theme.FontTitle - 6, Theme.TextMin, TextAnchor.MiddleCenter, Color.white, FontStyle.BoldAndItalic);
            UiFactory.Place(v.rectTransform, 0, 0.02f, 1, 0.64f);
        }

        // ------------------------------------------------------------------ scroll-list pieces

        /// <summary>
        /// A card for scroll lists: a header strip (caps title, optional right text) over a padded body. Returns the
        /// body; add rows (Ui.Line, rows, buttons) to it.
        /// </summary>
        public static RectTransform Card(Transform list, string title, string right = null, Color? strip = null, Action back = null)
        {
            return Ui.CardCore(list, title, right, null, back, strip);
        }

        /// <summary>Former size of the card back button (now the header strip's back chevron, 56 wide).</summary>
        public const float BackSide = Ui.Grid.Hit;

        /// <summary>A section header for scroll lists (a strip on its own, no card under it).</summary>
        public static RectTransform Section(Transform list, string title, string right = null)
        {
            return Ui.Section(list, title, right);
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
            var n = Text(row, "Name", "<b>" + name + "</b>", Theme.TextBody, Theme.TextMin, TextAnchor.LowerLeft, Theme.Text, FontStyle.Normal);
            UiFactory.Place(n.rectTransform, 0, 0.48f, 1 - textRight, 0.94f);
            n.rectTransform.offsetMin = new Vector2(side + 22, 0);
            n.rectTransform.offsetMax = new Vector2(-6, 0);
            var d = Text(row, "Detail", detail ?? "", Theme.TextCaption, Theme.TextMin, TextAnchor.UpperLeft, Theme.TextDim, FontStyle.Normal);
            UiFactory.Place(d.rectTransform, 0, 0.06f, 1 - textRight, 0.48f);
            d.rectTransform.offsetMin = new Vector2(side + 22, 0);
            d.rectTransform.offsetMax = new Vector2(-6, -2);
            if (figure != null)
            {
                var f = Text(row, "Figure", figure, Theme.TextBody, Theme.TextMin, TextAnchor.MiddleCenter);
                UiFactory.Place(f.rectTransform, 1 - textRight, 0, 1 - buttonShare, 1);
                f.rectTransform.offsetMin = new Vector2(4, 0);
                f.rectTransform.offsetMax = new Vector2(-4, 0);
            }
            return row;
        }
    }
}
