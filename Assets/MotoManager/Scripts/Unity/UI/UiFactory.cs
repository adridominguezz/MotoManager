using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>Shared colours and sizes so every screen looks consistent (dark navy base, one accent colour).</summary>
    public static class Theme
    {
        public static readonly Color Background = new Color32(11, 19, 32, 255);   // #0B1320
        public static readonly Color Panel = new Color32(18, 28, 46, 255);        // rails, header, lists
        public static readonly Color Card = new Color32(23, 35, 57, 255);         // content cards
        public static readonly Color PanelLight = new Color32(32, 47, 74, 255);   // buttons, tiles
        public static readonly Color Accent = new Color32(229, 56, 59, 255);
        public static readonly Color Text = new Color32(238, 242, 247, 255);
        public static readonly Color TextDim = new Color32(142, 154, 176, 255);
        public static readonly Color Good = new Color32(60, 200, 120, 255);
        public static readonly Color Warning = new Color32(240, 180, 40, 255);
        public static readonly Color Bad = new Color32(229, 56, 59, 255);
        public const int FontSmall = 22;
        public const int FontNormal = 28;
        public const int FontLarge = 40;
        public const int FontTitle = 56;

        public const string DimHex = "#8E9AB0";
        public const string GoodHex = "#3CC878";
        public const string WarnHex = "#F0B428";
        public const string BadHex = "#E5383B";

        public static readonly Color Info = new Color32(110, 170, 240, 255);
        public static readonly Color Divider = new Color(1, 1, 1, 0.07f);

        // Accent per section of the management interface (rail dot, tab underline, card wash, selected chips).
        // Mid-dark tones so white bold text stays readable on them; the brand red stays the main action colour.
        public static readonly Color AccentTeam = new Color32(47, 128, 237, 255);         // azure
        public static readonly Color AccentBike = new Color32(232, 105, 42, 255);         // orange
        public static readonly Color AccentFinance = new Color32(34, 168, 102, 255);      // emerald
        public static readonly Color AccentMarket = new Color32(132, 86, 232, 255);       // violet
        public static readonly Color AccentCompetition = new Color32(217, 154, 22, 255);  // gold

        /// <summary>Colours cycled by key-figure tiles so a dashboard row is not monochrome.</summary>
        public static readonly Color[] Palette =
        {
            new Color32(47, 128, 237, 255), new Color32(34, 168, 102, 255), new Color32(217, 154, 22, 255),
            new Color32(132, 86, 232, 255), new Color32(232, 105, 42, 255), new Color32(26, 178, 196, 255),
        };

        /// <summary>Accent of the section being shown (set by the management shell; brand red elsewhere).</summary>
        public static Color CurrentAccent = Accent;
        public static string CurrentAccentHex = BadHex;

        /// <summary>The accent colour of a navigation section ("nav.team", "nav.bike"...).</summary>
        public static Color SectionAccent(string key)
        {
            switch (key)
            {
                case "nav.team": return AccentTeam;
                case "nav.bike": return AccentBike;
                case "nav.finance": return AccentFinance;
                case "nav.market": return AccentMarket;
                case "nav.competition": return AccentCompetition;
                default: return Accent;
            }
        }

        /// <summary>Makes <paramref name="c"/> the accent used by cards, chips and tables built from now on.</summary>
        public static void SetAccent(Color c)
        {
            CurrentAccent = c;
            CurrentAccentHex = "#" + ColorUtility.ToHtmlStringRGB(c);
        }

        /// <summary>Text in the current section accent, lightened so it reads on the dark panels.</summary>
        public static string AccentText(string s)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(CurrentAccent, Color.white, 0.25f)) + ">" + s + "</color>";
        }

        /// <summary>Colour <paramref name="c"/> with another alpha.</summary>
        public static Color Alpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        public static string Dim(string s) { return "<color=" + DimHex + ">" + s + "</color>"; }
        public static string GoodText(string s) { return "<color=" + GoodHex + ">" + s + "</color>"; }
        public static string WarnText(string s) { return "<color=" + WarnHex + ">" + s + "</color>"; }
        public static string BadText(string s) { return "<color=" + BadHex + ">" + s + "</color>"; }
        public static string Bold(string s) { return "<b>" + s + "</b>"; }

        /// <summary>Money in red when negative.</summary>
        public static string Money(double v)
        {
            string m = Core.Common.MathX.FormatMoney(v);
            return v < 0 ? BadText(m) : m;
        }
    }

    /// <summary>A button in a row: its label, what it does, and whether it is the main action or the selected option.</summary>
    public struct UiAct
    {
        public string Key;
        public Action Value;
        public bool Primary;
        public bool Selected;

        /// <summary>Main actions use the brand red; a selected option takes the colour of the current section.</summary>
        public Color Color { get { return Primary ? Theme.Accent : Selected ? Theme.CurrentAccent : Theme.PanelLight; } }
    }

    /// <summary>
    /// Builds uGUI hierarchies from code (no prefabs or scenes to merge). Layout is anchor based and the canvas
    /// scales with the screen, so phones and tablets get the same proportions.
    /// </summary>
    public static class UiFactory
    {
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                // Unity 2022.2+ ships LegacyRuntime.ttf; older versions Arial.ttf.
                try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { }
                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (Exception) { }
                }
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            // Landscape phones: the height is always 1080 units and the width follows the screen, so every page
            // can be laid out to fit without scrolling.
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.Object.DontDestroyOnLoad(es);
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchors a rect to a fraction of its parent (0..1 coordinates) with an optional pixel inset.</summary>
        public static RectTransform Place(RectTransform rt, float xMin, float yMin, float xMax, float yMax, float inset = 0)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            return Place(rt, 0, 0, 1, 1);
        }

        public static Image Panel(string name, Transform parent, Color color, bool rounded = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded) Round(img);
            return img;
        }

        private static Sprite _rounded;

        /// <summary>White rounded rectangle (9-sliced) generated once, so panels and buttons get soft corners.</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (_rounded != null) return _rounded;
                const int size = 64, r = 20;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, r, size - r), cy = Mathf.Clamp(y + 0.5f, r, size - r);
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        byte a = (byte)(255 * Mathf.Clamp01(r - d + 0.5f));
                        px[y * size + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                _rounded = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return _rounded;
            }
        }

        public static Image Round(Image img, float radiusScale = 1.6f)
        {
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = radiusScale;
            return img;
        }

        /// <summary>Vertical shading by brightness multipliers (above 1 lightens the top, below 1 darkens the bottom).</summary>
        public static UiGradient Gradient(Graphic g, float top, float bottom)
        {
            return Gradient(g, new Color(top, top, top, 1), new Color(bottom, bottom, bottom, 1));
        }

        /// <summary>Two-stop colour multiplier over a graphic (vertical, or left to right when horizontal).</summary>
        public static UiGradient Gradient(Graphic g, Color top, Color bottom, bool horizontal = false)
        {
            var grad = g.GetComponent<UiGradient>();
            if (grad == null) grad = g.gameObject.AddComponent<UiGradient>();
            grad.Set(top, bottom, horizontal);
            return grad;
        }

        /// <summary>A soft offset shadow under a panel, button or text (fades with the graphic's own alpha).</summary>
        public static Shadow DropShadow(Graphic g, float alpha = 0.35f, float distance = 4)
        {
            var sh = g.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, alpha);
            sh.effectDistance = new Vector2(0, -distance);
            sh.useGraphicAlpha = true;
            return sh;
        }

        /// <summary>A decorative image: ignores clicks and layout groups, so it can sit inside any container.</summary>
        public static Image Decor(string name, Transform parent, Color color, bool rounded = false)
        {
            var img = Panel(name, parent, color, rounded);
            img.raycastTarget = false;
            img.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return img;
        }

        /// <summary>Thin accent bar on the left edge of a container (cards, list items, headings).</summary>
        public static Image SideStripe(Transform parent, Color color, float width = 5, float inset = 12)
        {
            var img = Decor("Stripe", parent, color, true);
            img.pixelsPerUnitMultiplier = 6f;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.offsetMin = new Vector2(0, inset);
            rt.offsetMax = new Vector2(width, -inset);
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size = Theme.FontNormal, TextAnchor align = TextAnchor.MiddleLeft, Color? color = null)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? Theme.Text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(string name, Transform parent, string text, Action onClick, Color? color = null, int size = Theme.FontNormal)
        {
            var img = Panel(name, parent, color ?? Theme.PanelLight, true);
            // Lit from above with a soft shadow below: buttons read as pressable, and runtime tints keep the shading.
            Gradient(img, 1.2f, 0.84f);
            DropShadow(img, 0.4f, 3);
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            var label = Label("Label", img.transform, text, size, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            DropShadow(label, 0.3f, 1.5f);
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        /// <summary>Vertical scroll list. Add children to the returned content transform.</summary>
        public static RectTransform ScrollList(string name, Transform parent, float spacing = 6)
        {
            var bg = Panel(name, parent, new Color(0, 0, 0, 0));
            var scroll = bg.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;

            var viewport = Rect("Viewport", bg.transform);
            Stretch(viewport);
            var vpImage = viewport.gameObject.AddComponent<Image>();
            vpImage.color = Color.white;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = Math.Max(spacing, 12);
            layout.padding = new RectOffset(12, 12, 12, 24);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        /// <summary>A fixed-height row for scroll lists.</summary>
        public static RectTransform Row(Transform list, float height)
        {
            var rt = Rect("Row", list);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return rt;
        }

        /// <summary>
        /// A table row: cells laid out by relative widths. Returns the row so callers can add buttons.
        /// Use rich text in cells for colour accents.
        /// </summary>
        public static RectTransform TableRow(Transform list, float height, float[] widths, string[] cells, bool header = false, Color? background = null, Color? textColor = null)
        {
            var row = Row(list, height);
            if (background.HasValue || header)
            {
                var img = row.gameObject.AddComponent<Image>();
                img.color = background ?? Theme.Alpha(Theme.CurrentAccent, 0.16f);
                img.raycastTarget = false;
                if (header)
                {
                    Round(img, 3f);
                    // Accent underline so the column titles read as a header, not as another row.
                    var line = Decor("Underline", row, Theme.Alpha(Theme.CurrentAccent, 0.7f));
                    line.rectTransform.anchorMin = new Vector2(0, 0);
                    line.rectTransform.anchorMax = new Vector2(1, 0);
                    line.rectTransform.offsetMin = new Vector2(4, 0);
                    line.rectTransform.offsetMax = new Vector2(-4, 2);
                }
            }
            float total = 0;
            foreach (var w in widths) total += w;
            float x = 0;
            for (int i = 0; i < cells.Length && i < widths.Length; i++)
            {
                var t = Label("Cell" + i, row, cells[i] ?? "", header ? Theme.FontSmall - 2 : Theme.FontSmall,
                    i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, header ? new Color32(186, 198, 218, 255) : (textColor ?? Theme.Text));
                if (header) t.fontStyle = FontStyle.Bold;
                t.supportRichText = true;
                Place(t.rectTransform, x / total, 0, (x + widths[i]) / total, 1);
                t.rectTransform.offsetMin = new Vector2(8, 0);
                x += widths[i];
            }
            return row;
        }

        /// <summary>Wrapped text whose height follows its content (no truncation on narrow screens).</summary>
        public static Text Paragraph(Transform list, string text, int size = Theme.FontSmall, float height = 0)
        {
            var row = Rect("Paragraph", list);
            var layout = row.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            if (height > 0)
            {
                var le = row.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = height;
                le.minHeight = height;
            }
            var t = Label("Text", row, text, size, TextAnchor.UpperLeft);
            t.supportRichText = true;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.1f;
            return t;
        }

        public static Text Heading(Transform list, string text)
        {
            var row = Row(list, 58);
            var bar = Decor("Accent", row, Theme.CurrentAccent, true);
            bar.pixelsPerUnitMultiplier = 6f;
            bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = Vector2.zero;
            bar.rectTransform.pivot = Vector2.zero;
            bar.rectTransform.anchoredPosition = new Vector2(4, 8);
            bar.rectTransform.sizeDelta = new Vector2(6, 34);
            var t = Label("Heading", row, text, Theme.FontLarge - 6, TextAnchor.LowerLeft, Theme.Text);
            t.supportRichText = true;
            t.fontStyle = FontStyle.Bold;
            DropShadow(t, 0.45f, 2);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(22, 4);
            return t;
        }

        /// <summary>A row of buttons sharing the width.</summary>
        public static void ButtonRow(Transform list, float height, params UiAct[] buttons)
        {
            var row = Row(list, height);
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = Button("Button", row, buttons[i].Key, buttons[i].Value, buttons[i].Color, Theme.FontSmall + 2);
                if (buttons[i].Primary || buttons[i].Selected) b.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
                Place((RectTransform)b.transform, (float)i / buttons.Length, 0, (float)(i + 1) / buttons.Length, 1, 4);
            }
        }

        /// <summary>
        /// Places a button inside one column of a table row (the same widths as the row), optionally sharing the
        /// column with other buttons, so it never covers the text of the other columns.
        /// </summary>
        public static void InColumn(Component button, float[] widths, int column, int part = 0, int parts = 1, float inset = 3)
        {
            float total = 0, start = 0;
            for (int i = 0; i < widths.Length; i++)
            {
                if (i < column) start += widths[i];
                total += widths[i];
            }
            float x0 = start / total, x1 = (start + widths[column]) / total;
            float w = (x1 - x0) / parts;
            Place((RectTransform)button.transform, x0 + w * part, 0.08f, x0 + w * (part + 1), 0.92f, inset);
        }

        /// <summary>A single-line text field in a list row.</summary>
        public static InputField Input(Transform list, string label, string value, Action<string> onChanged, float height = 64)
        {
            var row = Row(list, height);
            var l = Label("Label", row, label, Theme.FontSmall, TextAnchor.MiddleLeft, Theme.TextDim);
            Place(l.rectTransform, 0, 0, 0.3f, 1);
            l.rectTransform.offsetMin = new Vector2(8, 0);
            var box = Panel("Input", row, Theme.PanelLight, true);
            Gradient(box, 0.8f, 1.1f);
            Place(box.rectTransform, 0.3f, 0.1f, 1, 0.9f);
            var text = Label("Text", box.transform, value ?? "", Theme.FontNormal, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(12, 0);
            text.supportRichText = false;
            text.raycastTarget = true;
            var field = box.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.text = value ?? "";
            field.characterLimit = 40;
            field.onValueChanged.AddListener(v => { if (onChanged != null) onChanged(v); });
            return field;
        }

        public static UiAct Act(string label, Action action)
        {
            return new UiAct { Key = label, Value = action };
        }

        /// <summary>The main action of a row (highlighted).</summary>
        public static UiAct Primary(string label, Action action)
        {
            return new UiAct { Key = label, Value = action, Primary = true };
        }

        /// <summary>One option of a group; highlighted when it is the current choice.</summary>
        public static UiAct Option(string label, bool selected, Action action)
        {
            return new UiAct { Key = label, Value = action, Selected = selected };
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            Color c;
            return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out c) ? c : fallback;
        }
    }
}
