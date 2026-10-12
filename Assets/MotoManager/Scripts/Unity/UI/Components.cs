using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Higher-level building blocks for the management screens: cards, key figures, chips, bars, list items and
    /// sections that open on demand. Screens show the essentials first and keep the detail one tap away.
    /// </summary>
    public static class Ui
    {
        private static readonly HashSet<string> Open = new HashSet<string>();

        /// <summary>A rounded card with an optional title and subtitle; add rows to the returned transform.</summary>
        public static RectTransform Card(Transform list, string title = null, string subtitle = null, Color? color = null)
        {
            var img = UiFactory.Panel("Card", list, color ?? Theme.Card);
            var rt = img.rectTransform;
            // Flat graphite panel with angled corners (top right, bottom left), a barely-there shade and a short
            // hard shadow (only on opaque cards, where it cannot show through).
            UiFactory.Chamfer(img, 1.4f);
            UiFactory.Gradient(img, 1.05f, 0.95f);
            if (img.color.a > 0.95f) UiFactory.DropShadow(img, 0.35f, 3);
            // Fine highlight along the top edge, stopping before the angled corner.
            var rim = UiFactory.Decor("Rim", rt, new Color(1, 1, 1, 0.06f));
            rim.rectTransform.anchorMin = new Vector2(0, 1);
            rim.rectTransform.anchorMax = new Vector2(1, 1);
            rim.rectTransform.offsetMin = new Vector2(0, -1);
            rim.rectTransform.offsetMax = new Vector2(-16, 0);
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            bool compact = UiScale.Compact;
            layout.padding = compact ? new RectOffset(12, 12, 8, 10) : new RectOffset(18, 18, 14, 16);
            layout.spacing = compact ? 5 : 8;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            if (title != null)
            {
                // Compact: the subtitle sits on the same line, right-aligned, to save a line on small screens.
                bool inline = compact && subtitle != null;
                var row = UiFactory.Row(rt, inline ? 42 : subtitle != null ? 70 : 44);
                // Faint diagonal hatching in the top-right corner of the title band (a livery / pit-board detail).
                var hatch = UiFactory.Pattern("Hatch", row, UiFactory.StripesSprite, new Color(1, 1, 1, 0.045f), 18);
                hatch.rectTransform.anchorMin = new Vector2(1, 0);
                hatch.rectTransform.anchorMax = new Vector2(1, 1);
                hatch.rectTransform.pivot = new Vector2(1, 0.5f);
                hatch.rectTransform.offsetMin = new Vector2(-150, 0);
                hatch.rectTransform.offsetMax = new Vector2(-14, 0);
                UiFactory.Gradient(hatch, new Color(1, 1, 1, 0), Color.white, true);
                // Upper-case bold italic title (racing graphics), the subtitle in plain dimmed type below.
                var t = UiFactory.Label("Title", row, "<b>" + Theme.Caps(title) + "</b>" + (subtitle != null && !inline ? "\n" + Theme.Dim("<i><size=" + (Theme.FontSmall - 2) + ">" + subtitle + "</size></i>") : ""),
                    Theme.FontNormal, TextAnchor.MiddleLeft);
                t.supportRichText = true;
                t.fontStyle = FontStyle.Italic;
                // Upper case is wider: a long title shrinks a little rather than being cut on narrow cards.
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = Theme.FontSmall - 2;
                t.resizeTextMaxSize = Theme.FontNormal;
                UiFactory.DropShadow(t, 0.35f, 1.5f);
                UiFactory.Stretch(t.rectTransform);
                t.rectTransform.offsetMin = new Vector2(26, 0);
                if (inline)
                {
                    t.rectTransform.anchorMax = new Vector2(0.6f, 1);
                    var st = UiFactory.Label("Subtitle", row, "<i>" + subtitle + "</i>", Theme.FontSmall - 2, TextAnchor.MiddleRight, Theme.TextDim);
                    st.supportRichText = true;
                    st.resizeTextForBestFit = true;
                    st.resizeTextMinSize = Theme.FontSmall - 8;
                    st.resizeTextMaxSize = Theme.FontSmall - 2;
                    UiFactory.Place(st.rectTransform, 0.6f, 0, 1, 1);
                }
                // Slanted accent tag beside the title and an accent rule under the title block fading to the right.
                var tab = UiFactory.Decor("TitleAccent", row, Theme.CurrentAccent);
                UiFactory.Slant(tab, 3.2f);
                UiFactory.Place(tab.rectTransform, 0, 0.22f, 0, 0.78f);
                tab.rectTransform.offsetMin = new Vector2(-4, 0);
                tab.rectTransform.offsetMax = new Vector2(12, 0);
                if (subtitle != null && !inline)
                {
                    tab.rectTransform.anchorMin = new Vector2(0, 0.56f);
                    tab.rectTransform.anchorMax = new Vector2(0, 0.92f);
                }
                var hair = UiFactory.Decor("Divider", row, Theme.Alpha(Theme.CurrentAccent, 0.8f));
                hair.rectTransform.anchorMin = new Vector2(0, 0);
                hair.rectTransform.anchorMax = new Vector2(1, 0);
                hair.rectTransform.offsetMin = new Vector2(-18, -4);
                hair.rectTransform.offsetMax = new Vector2(0, -2);
                UiFactory.Gradient(hair, Color.white, new Color(1, 1, 1, 0), true);
            }
            return rt;
        }

        /// <summary>Big numbers with a caption (cash, position, points...). Each item: value, caption.</summary>
        public static void Kpis(Transform list, params string[][] items)
        {
            var row = UiFactory.Row(list, UiScale.Compact ? 88 : 118);
            for (int i = 0; i < items.Length; i++)
            {
                // Timing-screen tile: a near-black recessed body, a short accent speed stripe on the top-left edge
                // (the first tile carries the accent, the others neutral steel) and big bold italic figures.
                var tile = UiFactory.Panel("Kpi", row, Color.Lerp(Theme.Background, Theme.Panel, 0.6f));
                UiFactory.Chamfer(tile, 2.4f);
                UiFactory.Place(tile.rectTransform, (float)i / items.Length, 0, (float)(i + 1) / items.Length, 1, 5);
                UiFactory.Gradient(tile, 1.08f, 0.94f);
                tile.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.06f);
                var cap = UiFactory.Decor("Cap", tile.transform, i == 0 ? Theme.CurrentAccent : Theme.Palette[(i - 1) % Theme.Palette.Length]);
                UiFactory.Slant(cap, 4f);
                cap.rectTransform.anchorMin = new Vector2(0, 1);
                cap.rectTransform.anchorMax = new Vector2(0.42f, 1);
                cap.rectTransform.offsetMin = new Vector2(10, -5);
                cap.rectTransform.offsetMax = new Vector2(0, -1);
                var v = UiFactory.Label("Value", tile.transform, items[i][0], Theme.FontLarge - 4, TextAnchor.MiddleCenter);
                v.supportRichText = true;
                v.fontStyle = FontStyle.BoldAndItalic;
                UiFactory.DropShadow(v, 0.45f, 2);
                v.resizeTextForBestFit = true;
                v.resizeTextMinSize = Theme.FontSmall - 6;
                v.resizeTextMaxSize = Theme.FontLarge - 4;
                UiFactory.Place(v.rectTransform, 0, 0.38f, 1, 0.95f);
                var c = UiFactory.Label("Caption", tile.transform, Theme.Caps(items[i][1]), Theme.FontSmall - 4, TextAnchor.MiddleCenter, Color.Lerp(Theme.TextDim, Color.white, 0.2f));
                c.supportRichText = true;
                c.resizeTextForBestFit = true;
                c.resizeTextMinSize = Theme.FontSmall - 10;
                c.resizeTextMaxSize = Theme.FontSmall - 4;
                UiFactory.Place(c.rectTransform, 0.04f, 0.05f, 0.96f, 0.42f);
            }
        }

        public static string[] Kpi(string value, string caption)
        {
            return new[] { value, caption };
        }

        /// <summary>Row of selectable pills (categories, filters, tabs inside a page).</summary>
        public static void Chips(Transform list, IList<string> labels, int selected, Action<int> onSelect, float height = 56)
        {
            var row = UiFactory.Row(list, UiScale.Touch(height));
            for (int i = 0; i < labels.Count; i++)
            {
                int idx = i;
                bool on = i == selected;
                // Slanted segments like the tabs of a timing screen: the selected one filled with the accent, bold
                // italic; the others dark graphite with grey type.
                var b = UiFactory.Button("Chip", row, labels[i], () => onSelect(idx), on ? Theme.CurrentAccent : Color.Lerp(Theme.Panel, Theme.PanelLight, 0.55f), Theme.FontSmall - 2);
                UiFactory.Slant(b.image, 2.6f);
                UiFactory.Place((RectTransform)b.transform, (float)i / labels.Count, 0, (float)(i + 1) / labels.Count, 1, 3);
                var text = b.GetComponentInChildren<Text>();
                text.rectTransform.offsetMin = new Vector2(8, 0);
                text.rectTransform.offsetMax = new Vector2(-8, 0);
                if (on) text.fontStyle = FontStyle.BoldAndItalic;
                else text.color = Color.Lerp(Theme.TextDim, Color.white, 0.2f);
            }
        }

        /// <summary>Label, a 0..1 bar and a value on the right.</summary>
        public static void Bar(Transform list, string label, double fraction, string value, Color? color = null, float height = 40)
        {
            var row = UiFactory.Row(list, height);
            // The label shrinks to fit on one line rather than being cut ("Gestión de neumáticos").
            var l = UiFactory.Label("Label", row, label, Theme.FontSmall, TextAnchor.MiddleLeft, Theme.TextDim);
            l.supportRichText = true;
            l.horizontalOverflow = HorizontalWrapMode.Wrap;
            l.resizeTextForBestFit = true;
            l.resizeTextMinSize = Theme.FontSmall - 8;
            l.resizeTextMaxSize = Theme.FontSmall;
            UiFactory.Place(l.rectTransform, 0, 0, 0.45f, 1);
            // Flat, square-cut gauge: a dark track with quarter ticks and a solid fill brightening towards its tip
            // (like a rev / telemetry bar).
            var back = UiFactory.Panel("Back", row, new Color(0, 0, 0, 0.45f));
            UiFactory.Place(back.rectTransform, 0.46f, 0.3f, 0.86f, 0.7f);
            var fill = UiFactory.Panel("Fill", back.transform, color ?? Theme.Good);
            UiFactory.Gradient(fill, new Color(0.8f, 0.8f, 0.8f, 1), Color.white, true);
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
            var v = UiFactory.Label("Value", row, value, Theme.FontSmall, TextAnchor.MiddleRight);
            v.supportRichText = true;
            UiFactory.Place(v.rectTransform, 0.87f, 0, 1, 1);
        }

        /// <summary>Colour for a 0-100 rating.</summary>
        public static Color RatingColor(double v)
        {
            return v >= 75 ? Theme.Good : v >= 55 ? Theme.Info : v >= 40 ? Theme.Warning : Theme.Bad;
        }

        /// <summary>
        /// A list entry: bold title, dimmed detail line, a value on the right and up to three buttons.
        /// The detail can be several lines.
        /// </summary>
        public static RectTransform Item(Transform list, string title, string detail, string value = null, params UiAct[] actions)
        {
            var row = UiFactory.Rect("Item", list);
            var bg = row.gameObject.AddComponent<Image>();
            UiFactory.Chamfer(bg, 3f);
            bg.color = new Color(1, 1, 1, 0.045f);
            bg.raycastTarget = false;
            // Flat row with a thin square steel marker on the left edge (timing-tower entry); the accent stays for
            // titles, selection and the main actions.
            UiFactory.SideStripe(row, Theme.Steel, 3, 10);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 10, 10, 10);
            h.spacing = 10;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = false;
            h.childForceExpandWidth = false;
            row.gameObject.AddComponent<LayoutElement>().minHeight = UiScale.Compact ? 56 : 68;

            // Text takes the free width and wraps; its height sets the row height.
            var t = UiFactory.Label("Text", row, "<b>" + title + "</b>" + (string.IsNullOrEmpty(detail) ? "" : "\n" + Theme.Dim("<size=" + (Theme.FontSmall - 2) + ">" + detail + "</size>")),
                Theme.FontSmall + 2, TextAnchor.MiddleLeft);
            t.supportRichText = true;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var tl = t.gameObject.AddComponent<LayoutElement>();
            tl.flexibleWidth = 1;
            tl.preferredWidth = 10;
            if (value != null)
            {
                var v = UiFactory.Label("Value", row, value, Theme.FontSmall + 2, TextAnchor.MiddleRight);
                v.supportRichText = true;
                v.verticalOverflow = VerticalWrapMode.Overflow;
                var vl = v.gameObject.AddComponent<LayoutElement>();
                vl.preferredWidth = 210;
                vl.minWidth = 150;
            }
            for (int i = 0; i < actions.Length; i++)
            {
                var b = UiFactory.Button("Action", row, actions[i].Key, actions[i].Value, actions[i].Color, Theme.FontSmall - 4);
                var bl = b.gameObject.AddComponent<LayoutElement>();
                bl.preferredWidth = 190;
                bl.minWidth = 140;
                bl.preferredHeight = 56;
                bl.minHeight = 56;
            }
            return row;
        }

        /// <summary>
        /// A section that stays closed until tapped (detail on demand). Open state is remembered per key while the
        /// game runs, so rebuilding a screen keeps what the player opened.
        /// </summary>
        public static void Expander(Transform list, string key, string title, Action<Transform> build, Action refresh)
        {
            bool open = Open.Contains(key);
            // The arrow takes the accent; an open section is lit a little more than a closed one.
            var b = UiFactory.Button("Expander", list, Theme.AccentText(open ? "▼" : "►") + "  " + title, () =>
            {
                if (!Open.Remove(key)) Open.Add(key);
                refresh();
            }, new Color(1, 1, 1, open ? 0.08f : 0.05f), Theme.FontSmall);
            b.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
            var label = b.GetComponentInChildren<Text>();
            label.supportRichText = true;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = open ? Theme.Text : Color.Lerp(Theme.TextDim, Color.white, 0.15f);
            label.fontStyle = open ? FontStyle.Bold : FontStyle.Normal;
            label.rectTransform.offsetMin = new Vector2(16, 0);
            if (!open) return;
            var inner = UiFactory.Rect("ExpanderContent", list);
            var layout = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 0, 0, 4);
            layout.spacing = 6;
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
        /// Muted note for empty lists and explanations. On small screens explanations are folded behind one
        /// "more information" button per card (warnings stay visible), so the essentials fit.
        /// </summary>
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
                    var toggle = UiFactory.Button("InfoToggle", list, Theme.Dim("ⓘ  " + Loc.T("ui.moreInfo")), () =>
                    {
                        holder.gameObject.SetActive(!holder.gameObject.activeSelf);
                        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)list);
                    }, new Color(1, 1, 1, 0.03f), Theme.FontSmall - 4);
                    toggle.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                    toggle.GetComponentInChildren<Text>().supportRichText = true;
                    toggle.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
                    // The button comes first, the folded text right under it.
                    toggle.transform.SetSiblingIndex(holder.GetSiblingIndex());
                    block = holder;
                }
                UiFactory.Paragraph(block, Theme.Dim(text), Theme.FontSmall - 2);
                return;
            }
            UiFactory.Paragraph(list, Theme.Dim(text), Theme.FontSmall - 2);
        }

        private static bool IsAlert(string text)
        {
            return text != null && (text.Contains(Theme.WarnHex) || text.Contains(Theme.BadHex));
        }

        /// <summary>Two-column "label: value" line.</summary>
        public static void Line(Transform list, string label, string value, bool strong = false)
        {
            var row = UiFactory.Row(list, UiScale.Compact ? 34 : 38);
            var l = UiFactory.Label("Label", row, strong ? "<b>" + label + "</b>" : label, Theme.FontSmall, TextAnchor.MiddleLeft, strong ? Theme.Text : Theme.TextDim);
            l.supportRichText = true;
            UiFactory.Place(l.rectTransform, 0, 0, 0.62f, 1);
            l.rectTransform.offsetMin = new Vector2(6, 0);
            var v = UiFactory.Label("Value", row, strong ? "<b>" + value + "</b>" : value, Theme.FontSmall, TextAnchor.MiddleRight);
            v.supportRichText = true;
            UiFactory.Place(v.rectTransform, 0.62f, 0, 1, 1);
            v.rectTransform.offsetMax = new Vector2(-6, 0);
        }

        /// <summary>Two cards side by side (wide screens); returns both containers.</summary>
        public static void Columns(Transform list, out RectTransform left, out RectTransform right)
        {
            var row = UiFactory.Rect("Columns", list);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = UiScale.Compact ? 8 : 12;
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
            v.spacing = UiScale.Compact ? 8 : 12;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            // Equal halves whatever the content: without a fixed preferred width, a column holding a long line of
            // text would claim almost all the width and squeeze the other one into a sliver.
            var le = c.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.preferredWidth = 0;
            le.minWidth = 0;
            return c;
        }
    }
}
