using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Shared colours and sizes so every screen looks consistent: a pit-wall / timing-screen look with carbon and
    /// graphite surfaces, white and cool-grey text, and one accent (the player's team colour) used sparingly for the
    /// main action, the selected state and thin highlight strips. Semantic colours (good / warning / bad) are muted
    /// and kept for data only; timing yellow is the single secondary highlight.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Background = new Color32(10, 11, 14, 255);   // carbon black
        public static readonly Color Panel = new Color32(18, 20, 24, 255);        // rails, header, lists
        public static readonly Color Card = new Color32(25, 27, 32, 255);         // content cards (graphite)
        public static readonly Color PanelLight = new Color32(44, 47, 55, 255);   // buttons, tiles
        /// <summary>Brand red of the game (start screen, loading).</summary>
        public static readonly Color Brand = new Color32(229, 56, 59, 255);
        /// <summary>Main action colour: the player's team colour while managing a team, the brand red otherwise.</summary>
        public static Color Accent { get; private set; } = Brand;
        public static readonly Color Text = new Color32(242, 243, 245, 255);
        public static readonly Color TextDim = new Color32(150, 155, 165, 255);
        public static readonly Color Good = new Color32(70, 185, 115, 255);
        public static readonly Color Warning = new Color32(235, 180, 45, 255);
        public static readonly Color Bad = new Color32(220, 55, 55, 255);
        public const int FontSmall = 22;
        public const int FontNormal = 28;
        public const int FontLarge = 40;
        public const int FontTitle = 56;

        public const string DimHex = "#969BA5";
        public const string GoodHex = "#46B973";
        public const string WarnHex = "#EBB42D";
        public const string BadHex = "#DC3737";

        /// <summary>Neutral informative tone (cool steel) for mid-range data.</summary>
        public static readonly Color Info = new Color32(150, 170, 195, 255);
        public static readonly Color Divider = new Color(1, 1, 1, 0.07f);
        /// <summary>Neutral steel grey for details that must not compete with the accent.</summary>
        public static readonly Color Steel = new Color32(118, 124, 136, 255);
        /// <summary>Timing-screen yellow: the one secondary highlight (leader, key numbers).</summary>
        public static readonly Color Timing = new Color32(255, 210, 0, 255);
        public const string TimingHex = "#FFD200";

        // Former per-section colours. The racing look uses a single accent (the team colour), so they are neutral
        // steel now; kept so code naming them still compiles.
        public static readonly Color AccentTeam = Steel;
        public static readonly Color AccentBike = Steel;
        public static readonly Color AccentFinance = Steel;
        public static readonly Color AccentMarket = Steel;
        public static readonly Color AccentCompetition = Steel;
        public static readonly Color AccentCareer = Steel;

        /// <summary>Neutral tones for key-figure tiles: no rainbow, the accent does the highlighting.</summary>
        public static readonly Color[] Palette =
        {
            new Color32(118, 124, 136, 255), new Color32(96, 102, 114, 255), new Color32(136, 142, 154, 255),
        };

        /// <summary>Accent used by cards, chips and tables (the team colour; brand red outside a career).</summary>
        public static Color CurrentAccent = Accent;
        public static string CurrentAccentHex = BadHex;

        /// <summary>The accent colour of a navigation section: every section wears the team colour.</summary>
        public static Color SectionAccent(string key)
        {
            return Accent;
        }

        /// <summary>
        /// The interface takes the player's team colours: the primary (or the secondary when the primary is too grey)
        /// brought into a readable range becomes the main accent. Null colours restore the brand red.
        /// </summary>
        public static void SetTeamColors(string primary, string secondary)
        {
            Color c;
            if (!Readable(primary, out c) && !Readable(secondary, out c)) c = Brand;
            Accent = c;
        }

        private static bool Readable(string hex, out Color c)
        {
            c = Brand;
            Color raw;
            if (string.IsNullOrEmpty(hex) || !ColorUtility.TryParseHtmlString(hex, out raw)) return false;
            float h, s, v;
            Color.RGBToHSV(raw, out h, out s, out v);
            if (s < 0.25f) return false; // white, black, greys: not a usable accent
            c = Color.HSVToRGB(h, Mathf.Clamp(s, 0.55f, 0.9f), Mathf.Clamp(v, 0.62f, 0.9f));
            return true;
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
        public static string TimingText(string s) { return "<color=" + TimingHex + ">" + s + "</color>"; }

        /// <summary>Upper-cases the visible text of a label (racing-graphics titles), leaving rich-text tags untouched.</summary>
        public static string Caps(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            bool tag = false;
            foreach (char ch in s)
            {
                if (ch == '<') tag = true;
                sb.Append(tag ? ch : char.ToUpperInvariant(ch));
                if (ch == '>') tag = false;
            }
            return sb.ToString();
        }

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

        /// <summary>Main actions and the selected option use the accent (team colour); the others stay graphite.</summary>
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
            // Larger on phones (physical size), see UiScale.
            // Landscape phones: the height is always 1080 units and the width follows the screen, so every page
            // can be laid out to fit without scrolling.
            scaler.matchWidthOrHeight = 1f;
            UiScale.Register(scaler);
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        /// <summary>
        /// The part of a canvas that is safe for content (away from the camera notch, rounded corners and system
        /// bars). An optional colour fills the whole screen behind it, so the margins match the screen's background.
        /// </summary>
        public static RectTransform SafeRoot(Canvas canvas, Color? bleed = null)
        {
            var root = (RectTransform)canvas.transform;
            if (bleed.HasValue)
            {
                var fill = Panel("Bleed", root, bleed.Value);
                Stretch(fill.rectTransform);
                fill.raycastTarget = false;
            }
            var safe = Rect("Safe", root);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            return safe;
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

        /// <summary>
        /// Gives an image the shared corner sprite. The default scale keeps corners tight (about 5 units): the racing
        /// look uses crisp, nearly square surfaces rather than bubbly rounded ones.
        /// </summary>
        public static Image Round(Image img, float radiusScale = 4f)
        {
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = radiusScale;
            return img;
        }

        private static Sprite _chamfer, _slant, _checker, _stripes, _carbon;
        private const int ChamferPx = 16, SlantPx = 20;

        private static Texture2D NewTexture(int w, int h, bool repeat, bool point = false)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = point ? FilterMode.Point : FilterMode.Bilinear
            };
        }

        /// <summary>
        /// White rectangle with the top-right and bottom-left corners cut at 45 degrees (9-sliced): the angled-edge
        /// shape of racing graphics, used by cards, buttons and tiles.
        /// </summary>
        public static Sprite ChamferSprite
        {
            get
            {
                if (_chamfer != null) return _chamfer;
                const int size = 64, c = ChamferPx;
                var tex = NewTexture(size, size, false);
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float sum = x + 0.5f + y + 0.5f;
                        float topRight = (2 * size - c - sum) / 1.4142f;   // distance inside the top-right cut
                        float bottomLeft = (sum - c) / 1.4142f;            // distance inside the bottom-left cut
                        float a = Mathf.Clamp01(Mathf.Min(topRight, bottomLeft) + 0.5f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                _chamfer = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(c, c, c, c));
                return _chamfer;
            }
        }

        /// <summary>White parallelogram leaning forward like italic type (9-sliced): tabs, tags, position blocks.</summary>
        public static Sprite SlantSprite
        {
            get
            {
                if (_slant != null) return _slant;
                const int size = 64, s = SlantPx;
                var tex = NewTexture(size, size, false);
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float fx = x + 0.5f, shift = s * (y + 0.5f) / size;
                        float left = fx - shift, right = size - s + shift - fx;
                        float a = Mathf.Clamp01(Mathf.Min(left, right) + 0.5f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                _slant = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(s, 0, s, 0));
                return _slant;
            }
        }

        /// <summary>2x2 chequered-flag tile (white and black), repeated by tiled images.</summary>
        public static Sprite CheckerSprite
        {
            get
            {
                if (_checker != null) return _checker;
                var tex = NewTexture(2, 2, true, true);
                var w = new Color32(255, 255, 255, 255);
                var b = new Color32(0, 0, 0, 255);
                tex.SetPixels32(new[] { w, b, b, w });
                tex.Apply();
                _checker = Sprite.Create(tex, new UnityEngine.Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
                return _checker;
            }
        }

        /// <summary>Diagonal stripes (white on transparent), repeated by tiled images for subtle hazard details.</summary>
        public static Sprite StripesSprite
        {
            get
            {
                if (_stripes != null) return _stripes;
                const int size = 16;
                var tex = NewTexture(size, size, true);
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(((x - y + size * 2) % size) < 6 ? 255 : 0));
                tex.SetPixels32(px);
                tex.Apply();
                _stripes = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
                return _stripes;
            }
        }

        /// <summary>Carbon-fibre weave (white with varying alpha), repeated faintly over backgrounds.</summary>
        public static Sprite CarbonSprite
        {
            get
            {
                if (_carbon != null) return _carbon;
                const int size = 8, cell = 4;
                var tex = NewTexture(size, size, true);
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        bool across = ((x / cell) + (y / cell)) % 2 == 0;
                        float t = ((across ? x : y) % cell + 0.5f) / cell;
                        float shade = 0.25f + 0.75f * Mathf.Sin(t * Mathf.PI);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * shade));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                _carbon = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
                return _carbon;
            }
        }

        /// <summary>Angled corners (top-right and bottom-left cut); a higher scale makes the cut smaller.</summary>
        public static Image Chamfer(Image img, float scale = 2f)
        {
            img.sprite = ChamferSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = scale;
            return img;
        }

        /// <summary>Slanted (forward-leaning) ends; a higher scale makes the slant shallower.</summary>
        public static Image Slant(Image img, float scale = 2f)
        {
            img.sprite = SlantSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = scale;
            return img;
        }

        /// <summary>
        /// A decorative repeated pattern (chequered flag, diagonal stripes, carbon weave) filling its rect, with one
        /// pattern tile measuring <paramref name="tile"/> canvas units.
        /// </summary>
        public static Image Pattern(string name, Transform parent, Sprite sprite, Color color, float tile)
        {
            var img = Decor(name, parent, color);
            img.sprite = sprite;
            img.type = Image.Type.Tiled;
            img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, sprite.rect.width / Mathf.Max(1f, tile));
            return img;
        }

        /// <summary>A strip of chequered flag (squares of <paramref name="square"/> units) anchored to a rect.</summary>
        public static Image Chequered(Transform parent, float xMin, float yMin, float xMax, float yMax, float square, float alpha = 1f)
        {
            var img = Pattern("Chequered", parent, CheckerSprite, new Color(1, 1, 1, alpha), square * 2);
            Place(img.rectTransform, xMin, yMin, xMax, yMax);
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
            // Square-cut strip: thin, hard-edged accent like the livery lines of a timing graphic.
            var img = Decor("Stripe", parent, color);
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
            var img = Panel(name, parent, color ?? Theme.PanelLight);
            // Flat surface with angled corners, a faint top-down shade and a short hard shadow (no gloss); runtime
            // tints keep the shading.
            Chamfer(img, 2.4f);
            Gradient(img, 1.08f, 0.9f);
            DropShadow(img, 0.3f, 2);
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            var label = Label("Label", img.transform, text, size, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            DropShadow(label, 0.35f, 1.5f);
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
            // Rows often carry a button: a little taller on a phone.
            var row = Row(list, header ? height : UiScale.Handheld ? height * 1.12f : height);
            if (background.HasValue || header)
            {
                var img = row.gameObject.AddComponent<Image>();
                // Timing-screen header: a near-black band with a thin accent rule under it.
                img.color = background ?? new Color(0, 0, 0, 0.45f);
                img.raycastTarget = false;
                if (header)
                {
                    var line = Decor("Underline", row, Theme.CurrentAccent);
                    line.rectTransform.anchorMin = new Vector2(0, 0);
                    line.rectTransform.anchorMax = new Vector2(1, 0);
                    line.rectTransform.offsetMin = new Vector2(0, 0);
                    line.rectTransform.offsetMax = new Vector2(0, 2);
                    Gradient(line, Color.white, new Color(1, 1, 1, 0.15f), true);
                }
            }
            else
            {
                // Hairline between rows, like the lines of a timing monitor.
                var hair = Decor("Rule", row, new Color(1, 1, 1, 0.045f));
                hair.rectTransform.anchorMin = new Vector2(0, 0);
                hair.rectTransform.anchorMax = new Vector2(1, 0);
                hair.rectTransform.offsetMin = Vector2.zero;
                hair.rectTransform.offsetMax = new Vector2(0, 1);
            }
            float total = 0;
            foreach (var w in widths) total += w;
            float x = 0;
            for (int i = 0; i < cells.Length && i < widths.Length; i++)
            {
                string cell = cells[i] ?? "";
                // A position number in the first column sits in a white block with dark type (timing tower).
                int pos = !header && i == 0 ? PositionNumber(cell) : 0;
                if (pos > 0)
                {
                    var block = Decor("PosBlock", row, new Color(0.9f, 0.91f, 0.93f, 1f));
                    Slant(block, 3.2f);
                    block.rectTransform.anchorMin = new Vector2(x / total, 0);
                    block.rectTransform.anchorMax = new Vector2(x / total, 1);
                    block.rectTransform.pivot = new Vector2(0, 0.5f);
                    block.rectTransform.offsetMin = new Vector2(4, 4);
                    block.rectTransform.offsetMax = new Vector2(52, -4);
                    var n = Label("Cell" + i, block.transform, pos.ToString(), Theme.FontSmall, TextAnchor.MiddleCenter, new Color32(12, 13, 16, 255));
                    n.fontStyle = FontStyle.BoldAndItalic;
                    Stretch(n.rectTransform);
                    x += widths[i];
                    continue;
                }
                var t = Label("Cell" + i, row, header ? Theme.Caps(cell) : cell, header ? Theme.FontSmall - 2 : Theme.FontSmall,
                    i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, header ? new Color32(176, 181, 190, 255) : (textColor ?? Theme.Text));
                if (header)
                {
                    t.fontStyle = FontStyle.Bold;
                    // Upper-case titles in narrow columns shrink instead of wrapping out of the band.
                    t.resizeTextForBestFit = true;
                    t.resizeTextMinSize = Theme.FontSmall - 8;
                    t.resizeTextMaxSize = Theme.FontSmall - 2;
                }
                t.supportRichText = true;
                Place(t.rectTransform, x / total, 0, (x + widths[i]) / total, 1);
                t.rectTransform.offsetMin = new Vector2(8, 0);
                x += widths[i];
            }
            return row;
        }

        /// <summary>The 1-99 number a cell shows when it is only a number (rich-text tags ignored), otherwise 0.</summary>
        private static int PositionNumber(string cell)
        {
            int n = 0, digits = 0;
            bool tag = false;
            foreach (char ch in cell)
            {
                if (ch == '<') tag = true;
                else if (ch == '>') tag = false;
                else if (!tag)
                {
                    if (ch < '0' || ch > '9' || ++digits > 2) return 0;
                    n = n * 10 + (ch - '0');
                }
            }
            return n;
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
            // Racing-graphic section title: a slanted accent tag, upper-case bold italic type and a rule fading right.
            var bar = Decor("Accent", row, Theme.CurrentAccent);
            Slant(bar, 3f);
            bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = Vector2.zero;
            bar.rectTransform.pivot = Vector2.zero;
            bar.rectTransform.anchoredPosition = new Vector2(2, 10);
            bar.rectTransform.sizeDelta = new Vector2(18, 32);
            var rule = Decor("Rule", row, new Color(1, 1, 1, 0.14f));
            rule.rectTransform.anchorMin = new Vector2(0, 0);
            rule.rectTransform.anchorMax = new Vector2(1, 0);
            rule.rectTransform.offsetMin = new Vector2(0, 4);
            rule.rectTransform.offsetMax = new Vector2(0, 5);
            Gradient(rule, Color.white, new Color(1, 1, 1, 0), true);
            var t = Label("Heading", row, Theme.Caps(text), Theme.FontLarge - 6, TextAnchor.LowerLeft, Theme.Text);
            t.supportRichText = true;
            t.fontStyle = FontStyle.BoldAndItalic;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Theme.FontNormal - 4;
            t.resizeTextMaxSize = Theme.FontLarge - 6;
            DropShadow(t, 0.4f, 2);
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(30, 6);
            return t;
        }

        /// <summary>
        /// Gives a screen title the racing-graphics treatment: upper case, bold italic, and a short slanted accent
        /// bar under it. Call after setting the text.
        /// </summary>
        public static Text RaceTitle(Text t)
        {
            t.supportRichText = true;
            t.text = Theme.Caps(t.text);
            t.fontStyle = FontStyle.BoldAndItalic;
            DropShadow(t, 0.45f, 2);
            var bar = Decor("TitleBar", t.transform, Theme.Accent);
            Slant(bar, 4f);
            float x = t.alignment == TextAnchor.MiddleCenter || t.alignment == TextAnchor.UpperCenter || t.alignment == TextAnchor.LowerCenter ? 0.5f : 0f;
            bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = new Vector2(x, 0);
            bar.rectTransform.pivot = new Vector2(x, 0);
            bar.rectTransform.anchoredPosition = Vector2.zero;
            bar.rectTransform.sizeDelta = new Vector2(120, 5);
            return t;
        }

        /// <summary>A row of buttons sharing the width.</summary>
        public static void ButtonRow(Transform list, float height, params UiAct[] buttons)
        {
            var row = Row(list, UiScale.Touch(height));
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = Button("Button", row, buttons[i].Key, buttons[i].Value, buttons[i].Color, Theme.FontSmall + 2);
                if (buttons[i].Primary) b.GetComponentInChildren<Text>().fontStyle = FontStyle.BoldAndItalic;
                else if (buttons[i].Selected) b.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
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
            var row = Row(list, UiScale.Touch(height));
            var l = Label("Label", row, label, Theme.FontSmall, TextAnchor.MiddleLeft, Theme.TextDim);
            Place(l.rectTransform, 0, 0, 0.3f, 1);
            l.rectTransform.offsetMin = new Vector2(8, 0);
            // Recessed field: darker than the card, with a thin accent rule at the bottom like a form on a pit-wall screen.
            var box = Panel("Input", row, new Color(0, 0, 0, 0.45f), true);
            var focus = Decor("Rule", box.transform, Theme.Alpha(Theme.CurrentAccent, 0.8f));
            focus.rectTransform.anchorMin = new Vector2(0, 0);
            focus.rectTransform.anchorMax = new Vector2(1, 0);
            focus.rectTransform.offsetMin = Vector2.zero;
            focus.rectTransform.offsetMax = new Vector2(0, 2);
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
