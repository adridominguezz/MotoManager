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
            var img = UiFactory.Panel("Card", list, color ?? Theme.Card, true);
            var rt = img.rectTransform;
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 16);
            layout.spacing = 8;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            if (title != null)
            {
                var row = UiFactory.Row(rt, subtitle != null ? 70 : 44);
                var t = UiFactory.Label("Title", row, "<b>" + title + "</b>" + (subtitle != null ? "\n" + Theme.Dim("<size=" + (Theme.FontSmall - 2) + ">" + subtitle + "</size>") : ""),
                    Theme.FontNormal, TextAnchor.MiddleLeft);
                t.supportRichText = true;
                UiFactory.Stretch(t.rectTransform);
            }
            return rt;
        }

        /// <summary>Big numbers with a caption (cash, position, points...). Each item: value, caption.</summary>
        public static void Kpis(Transform list, params string[][] items)
        {
            var row = UiFactory.Row(list, 118);
            for (int i = 0; i < items.Length; i++)
            {
                var tile = UiFactory.Panel("Kpi", row, Theme.PanelLight, true);
                UiFactory.Place(tile.rectTransform, (float)i / items.Length, 0, (float)(i + 1) / items.Length, 1, 5);
                var v = UiFactory.Label("Value", tile.transform, items[i][0], Theme.FontLarge - 4, TextAnchor.MiddleCenter);
                v.supportRichText = true;
                UiFactory.Place(v.rectTransform, 0, 0.38f, 1, 0.95f);
                var c = UiFactory.Label("Caption", tile.transform, items[i][1], Theme.FontSmall - 4, TextAnchor.MiddleCenter, Theme.TextDim);
                c.supportRichText = true;
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
            var row = UiFactory.Row(list, height);
            for (int i = 0; i < labels.Count; i++)
            {
                int idx = i;
                var b = UiFactory.Button("Chip", row, labels[i], () => onSelect(idx), i == selected ? Theme.Accent : Theme.PanelLight, Theme.FontSmall - 2);
                UiFactory.Place((RectTransform)b.transform, (float)i / labels.Count, 0, (float)(i + 1) / labels.Count, 1, 4);
            }
        }

        /// <summary>Label, a 0..1 bar and a value on the right.</summary>
        public static void Bar(Transform list, string label, double fraction, string value, Color? color = null, float height = 40)
        {
            var row = UiFactory.Row(list, height);
            var l = UiFactory.Label("Label", row, label, Theme.FontSmall, TextAnchor.MiddleLeft, Theme.TextDim);
            l.supportRichText = true;
            UiFactory.Place(l.rectTransform, 0, 0, 0.36f, 1);
            var back = UiFactory.Panel("Back", row, new Color(1, 1, 1, 0.08f), true);
            UiFactory.Place(back.rectTransform, 0.37f, 0.3f, 0.86f, 0.7f);
            var fill = UiFactory.Panel("Fill", back.transform, color ?? Theme.Good, true);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)fraction), 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var v = UiFactory.Label("Value", row, value, Theme.FontSmall, TextAnchor.MiddleRight);
            v.supportRichText = true;
            UiFactory.Place(v.rectTransform, 0.87f, 0, 1, 1);
        }

        /// <summary>Colour for a 0-100 rating.</summary>
        public static Color RatingColor(double v)
        {
            return v >= 75 ? Theme.Good : v >= 55 ? new Color32(110, 170, 240, 255) : v >= 40 ? Theme.Warning : Theme.Bad;
        }

        /// <summary>
        /// A list entry: bold title, dimmed detail line, a value on the right and up to three buttons.
        /// The detail can be several lines.
        /// </summary>
        public static RectTransform Item(Transform list, string title, string detail, string value = null, params UiAct[] actions)
        {
            var row = UiFactory.Rect("Item", list);
            var bg = row.gameObject.AddComponent<Image>();
            UiFactory.Round(bg);
            bg.color = new Color(1, 1, 1, 0.035f);
            bg.raycastTarget = false;
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 10, 10, 10);
            h.spacing = 10;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = false;
            h.childForceExpandWidth = false;
            row.gameObject.AddComponent<LayoutElement>().minHeight = 68;

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
            var b = UiFactory.Button("Expander", list, (open ? "▼  " : "►  ") + title, () =>
            {
                if (!Open.Remove(key)) Open.Add(key);
                refresh();
            }, new Color(1, 1, 1, 0.05f), Theme.FontSmall);
            b.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
            var label = b.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Theme.TextDim;
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

        /// <summary>Muted note for empty lists and explanations.</summary>
        public static void Note(Transform list, string text)
        {
            UiFactory.Paragraph(list, Theme.Dim(text), Theme.FontSmall - 2);
        }

        /// <summary>Two-column "label: value" line.</summary>
        public static void Line(Transform list, string label, string value, bool strong = false)
        {
            var row = UiFactory.Row(list, 38);
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
            h.spacing = 12;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = false;
            h.childForceExpandWidth = true;
            h.childAlignment = TextAnchor.UpperLeft;
            left = Column(row);
            right = Column(row);
        }

        private static RectTransform Column(Transform parent)
        {
            var c = UiFactory.Rect("Column", parent);
            var v = c.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 12;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            c.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            return c;
        }
    }
}
