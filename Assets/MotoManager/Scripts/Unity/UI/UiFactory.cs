using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Shared colours and sizes so every screen looks consistent (docs/VISUAL_STYLE.md). The look follows the
    /// championship's own broadcast/web style: near-black surfaces with clean light text, one strong race red for the
    /// main action and live/next status, the player's team colour as identity (own row, row edges, header tags, tab
    /// and rail markers), heavy upright display type for titles and big numbers, and a clean sans in sentence case for
    /// body and captions. Semantic colours (good / warning / bad / info) are kept for data only; timing yellow is the
    /// key-number highlight.
    /// </summary>
    public static class Theme
    {
        // ---- Neutrals: near-black with a faint cool cast (never pure #000 surfaces behind text)
        /// <summary>Page floor.</summary>
        public static readonly Color Background = new Color32(8, 9, 11, 255);
        /// <summary>Navigation chrome (top bar, rail): the deepest black, like the site's navigation bar.</summary>
        public static readonly Color Chrome = new Color32(3, 3, 4, 255);
        /// <summary>Rails, header, lists.</summary>
        public static readonly Color Panel = new Color32(17, 19, 23, 255);
        /// <summary>Content cards and sheets.</summary>
        public static readonly Color Card = new Color32(23, 25, 30, 255);
        /// <summary>Raised controls (secondary buttons, selected tabs).</summary>
        public static readonly Color PanelLight = new Color32(42, 45, 51, 255);

        /// <summary>Race red: the primary call to action and live / up-next status (independent of the team colour).</summary>
        public static readonly Color Race = new Color32(216, 15, 10, 255);
        /// <summary>Brand red of the game (start screen, loading) = race red.</summary>
        public static readonly Color Brand = Race;
        /// <summary>Identity accent: the player's team colour while managing a team, the brand red otherwise.</summary>
        public static Color Accent { get; private set; } = Brand;
        /// <summary>Fill of the one primary action of a screen (race red).</summary>
        public static Color Action { get { return Race; } }

        /// <summary>Body text: off-white (not pure white) for less glare on the black surfaces.</summary>
        public static readonly Color Text = new Color32(244, 245, 247, 255);
        /// <summary>Secondary text: 6.9:1 on tiles, 7.4:1 on cards.</summary>
        public static readonly Color TextDim = new Color32(163, 168, 177, 255);

        // ---- Semantic set (fills); the *Hex strings are the text variants, all ≥ 5:1 on cards
        public static readonly Color Good = new Color32(70, 185, 115, 255);
        /// <summary>Attention: amber-orange, kept clearly apart from timing yellow (key numbers).</summary>
        public static readonly Color Warning = new Color32(242, 159, 69, 255);
        public static readonly Color Bad = new Color32(220, 55, 55, 255);
        // Legacy sizes (kept for compatibility). Never subtract from FontSmall: 18 is the floor (TextMin).
        public const int FontSmall = 22;
        public const int FontNormal = 28;
        public const int FontLarge = 40;
        public const int FontTitle = 56;

        // ---- Type scale (UX spec §1.2). "Display" = the heavy wide display face (UiFactory.DisplayFont).
        /// <summary>Hero number (rating, P1, cash in a hero tile): display face.</summary>
        public const int TextDisplay = 56;
        /// <summary>The one key number of a tile: display face, timing yellow by default.</summary>
        public const int TextKey = 44;
        /// <summary>Page / hero names: display face, caps.</summary>
        public const int TextTitle = 32;
        /// <summary>Tile and card header strips: display face, caps (CTA labels: sans bold caps 28).</summary>
        public const int TextHeader = 24;
        /// <summary>List cells and label/value lines: sans.</summary>
        public const int TextBody = 24;
        /// <summary>Captions under numbers, column captions, status lines: sans, sentence case, dim.</summary>
        public const int TextCaption = 20;
        /// <summary>Absolute floor for any font size and any resizeTextMinSize.</summary>
        public const int TextMin = 18;

        /// <summary>Dark ink for text on light fills (rating badges, position plates, light accents).</summary>
        public static readonly Color Ink = new Color32(10, 11, 13, 255);
        /// <summary>Surface of every tile.</summary>
        public static readonly Color Tile = new Color32(28, 31, 36, 255);
        /// <summary>Header strip over a tile or card: a faint darker band (ink at 30%), no heavy black slab.</summary>
        public static readonly Color HeaderFill = new Color(10 / 255f, 11 / 255f, 13 / 255f, 0.3f);
        /// <summary>Light plate behind dark numbers (rider number badges, position blocks of the race tower).</summary>
        public static readonly Color Plate = new Color32(236, 238, 241, 255);
        /// <summary>Bike performance figures (green).</summary>
        public static readonly Color Perf = new Color32(70, 200, 110, 255);
        /// <summary>Bike reliability figures (purple).</summary>
        public static readonly Color Rel = new Color32(196, 92, 226, 255);
        /// <summary>Fastest-lap / personal-best purple (timing-screen convention).</summary>
        public static readonly Color Fastest = new Color32(178, 102, 255, 255);

        /// <summary>Text colour on an accent fill: ink on light team colours, light text otherwise.</summary>
        public static Color OnAccent { get; private set; } = Color.white;

        /// <summary>Text colour on the race-red action fill.</summary>
        public static Color OnAction { get { return OnColor(Race); } }

        /// <summary>WCAG relative luminance of a colour (sRGB).</summary>
        public static float Luminance(Color c)
        {
            return 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
        }

        private static float Lin(float v)
        {
            return v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>WCAG contrast ratio between two opaque colours (1..21).</summary>
        public static float Contrast(Color a, Color b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        /// <summary>
        /// Readable text colour on a fill, by measured contrast: light text while it reaches 3.6:1 (labels on fills are
        /// bold and large), otherwise whichever of light text and ink contrasts more.
        /// </summary>
        public static Color OnColor(Color fill)
        {
            float light = Contrast(Text, fill);
            if (light >= 3.6f) return Text;
            return Contrast(Ink, fill) > light ? Ink : Text;
        }

        /// <summary>
        /// <paramref name="c"/> lightened just enough to read as text on <paramref name="surface"/> (at least
        /// <paramref name="ratio"/>:1), so dark team colours still work as accent text.
        /// </summary>
        public static Color Readable(Color c, Color surface, float ratio = 4.5f)
        {
            c.a = 1;
            for (int i = 0; i < 20 && Contrast(c, surface) < ratio; i++) c = Color.Lerp(c, Color.white, 0.12f);
            return c;
        }

        /// <summary>The current accent as text on cards (≥ 4.5:1).</summary>
        public static Color AccentInk { get { return Readable(CurrentAccent, Card); } }

        /// <summary>A signed delta as rich text: "+4" green, "−0,5" red (Unicode minus), "0" dim.</summary>
        public static string Signed(double v, int decimals = 0)
        {
            decimals = Mathf.Clamp(decimals, 0, 6);
            string fmt = "F" + decimals;
            string abs = Math.Abs(v).ToString(fmt, Loc.Culture);
            double rounded = Math.Round(Math.Abs(v), decimals);
            if (rounded == 0) return Dim(0.0.ToString(fmt, Loc.Culture));
            return v > 0 ? GoodText("+" + abs) : BadText("−" + abs);
        }

        /// <summary>A position as "P3", or "-" when there is none.</summary>
        public static string Pos(int p)
        {
            return p > 0 ? "P" + p : "-";
        }

        public const string DimHex = "#A3A8B1";
        public const string GoodHex = "#46B973";
        public const string WarnHex = "#F29F45";
        /// <summary>Bad as text: lighter than the Bad fill so it reads on cards (5.9:1; the fill gives 3.8:1).</summary>
        public const string BadHex = "#F26B6B";
        /// <summary>Pit lane / neutral information as text.</summary>
        public const string InfoHex = "#8FB4D4";
        /// <summary>Fastest lap as text.</summary>
        public const string FastestHex = "#B266FF";

        /// <summary>Neutral informative tone (cool steel) for mid-range data.</summary>
        public static readonly Color Info = new Color32(150, 170, 195, 255);
        /// <summary>Hairline between rows and sections (white 7%).</summary>
        public static readonly Color Divider = new Color(1, 1, 1, 0.07f);
        /// <summary>1-unit edge of raised surfaces (tiles, cards, sheets): white 8%.</summary>
        public static readonly Color Edge = new Color(1, 1, 1, 0.08f);
        /// <summary>Thin outline of secondary (outline) buttons: white 38%, 3:1 against the surfaces.</summary>
        public static readonly Color Stroke = new Color(1, 1, 1, 0.38f);
        /// <summary>Neutral steel grey for details that must not compete with the accent.</summary>
        public static readonly Color Steel = new Color32(122, 128, 139, 255);
        /// <summary>Timing-screen yellow: the key-number highlight (leader, key numbers).</summary>
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
            new Color32(122, 128, 139, 255), new Color32(98, 104, 115, 255), new Color32(140, 146, 157, 255),
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
        /// brought into a readable range becomes the identity accent. Null colours restore the brand red.
        /// </summary>
        public static void SetTeamColors(string primary, string secondary)
        {
            Color c;
            if (!Readable(primary, out c) && !Readable(secondary, out c)) c = Brand;
            Accent = c;
            OnAccent = OnColor(c);
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
            OnAccent = OnColor(c);
        }

        /// <summary>Text in the current accent, lightened until it reads on the dark cards (≥ 4.5:1).</summary>
        public static string AccentText(string s)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(AccentInk) + ">" + s + "</color>";
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

        /// <summary>Upper-cases the visible text of a label (titles, buttons, tags), leaving rich-text tags untouched.</summary>
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

        /// <summary>Main actions use the race-red action fill, the selected option the team colour; the others graphite.</summary>
        public Color Color { get { return Primary ? Theme.Action : Selected ? Theme.CurrentAccent : Theme.PanelLight; } }
    }

    /// <summary>
    /// Builds uGUI hierarchies from code (no prefabs or scenes to merge). Layout is anchor based and the canvas
    /// scales with the screen, so phones and tablets get the same proportions.
    /// </summary>
    public static class UiFactory
    {
        private static Font _font, _builtin, _display;
        private static bool _displayLoaded;
        private static Font[] _styles;

        /// <summary>
        /// The text face: Titillium Web (OFL, Resources/Fonts), a clean technical sans for body, labels and buttons.
        /// Its Bold, Italic and Bold Italic files share the family name, so Unity picks them for the matching
        /// FontStyle (it synthesises the style if a file is missing). Falls back to Unity's built-in font.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                try
                {
                    _font = Resources.Load<Font>("Fonts/TitilliumWeb-Regular");
                    // Keep the style files loaded alongside the regular one.
                    _styles = new[]
                    {
                        Resources.Load<Font>("Fonts/TitilliumWeb-Bold"), Resources.Load<Font>("Fonts/TitilliumWeb-Italic"),
                        Resources.Load<Font>("Fonts/TitilliumWeb-BoldItalic")
                    };
                }
                catch (Exception) { }
                if (_font == null) _font = BuiltinFont;
                return _font;
            }
        }

        /// <summary>
        /// The display face: Russo One (OFL), heavy, wide and upright, for titles, header strips and big numbers
        /// (positions, points, key figures, rider numbers). Null when it is missing (callers then use bold text).
        /// </summary>
        public static Font DisplayFont
        {
            get
            {
                if (_displayLoaded) return _display;
                _displayLoaded = true;
                try { _display = Resources.Load<Font>("Fonts/RussoOne-Regular"); } catch (Exception) { }
                return _display;
            }
        }

        private static Font BuiltinFont
        {
            get
            {
                if (_builtin != null) return _builtin;
                // Unity 2022.2+ ships LegacyRuntime.ttf; older versions Arial.ttf.
                try { _builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { }
                if (_builtin == null)
                {
                    try { _builtin = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (Exception) { }
                }
                return _builtin;
            }
        }

        /// <summary>
        /// Applies a type role. <see cref="FontStyle.BoldAndItalic"/> is the "display" role across the screens (titles,
        /// hero names, key numbers): it is drawn in the upright display face. Every other style uses the text face.
        /// </summary>
        public static Text Style(Text t, FontStyle style)
        {
            if (t == null) return null;
            if (style == FontStyle.BoldAndItalic && DisplayFont != null)
            {
                t.font = DisplayFont;
                t.fontStyle = FontStyle.Normal;
            }
            else
            {
                t.font = Font;
                t.fontStyle = style;
            }
            return t;
        }

        /// <summary>Sets <paramref name="t"/> in the display face (titles, big numbers).</summary>
        public static Text Display(Text t)
        {
            return Style(t, FontStyle.BoldAndItalic);
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

        /// <summary>Radius of surfaces (tiles, cards, sheets, badges): about 6 units, soft but not bubbly.</summary>
        public const float SurfaceRadius = 3.4f;

        /// <summary>
        /// Fully rounded ends (a capsule): buttons, segmented toggles and the top-bar pills. The corner radius is larger
        /// than half of any control height, and uGUI scales sliced borders down to fit, so the ends are always round.
        /// </summary>
        public static Image Capsule(Image img)
        {
            return Round(img, 0.45f);
        }

        private static Sprite _ring;

        /// <summary>
        /// A rounded-rectangle outline (about 1.3 px stroke, 9-sliced, hollow): the thin border of outline pill buttons.
        /// Same corner geometry as <see cref="RoundedSprite"/>, so it lines up with <see cref="Capsule"/> fills.
        /// </summary>
        public static Sprite RingSprite
        {
            get
            {
                if (_ring != null) return _ring;
                const int size = 64, r = 20;
                const float stroke = 1.3f;
                var tex = NewTexture(size, size, false);
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, r, size - r), cy = Mathf.Clamp(y + 0.5f, r, size - r);
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        // Distance to the outer edge of the rounded rect, then a band of `stroke` inside it.
                        float edge = r - d;
                        float a = Mathf.Clamp01(edge + 0.5f) * Mathf.Clamp01(stroke - edge + 0.5f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                _ring = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return _ring;
            }
        }

        /// <summary>
        /// A thin capsule outline stretched over <paramref name="parent"/> (outline pill buttons, pickers). Decorative:
        /// the parent keeps the clicks and the press tint.
        /// </summary>
        public static Image Ring(Transform parent, Color color)
        {
            var img = Decor("Ring", parent, color);
            img.sprite = RingSprite;
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
            img.pixelsPerUnitMultiplier = 0.45f;
            Stretch(img.rectTransform);
            return img;
        }

        /// <summary>The surface shape: a small even radius on every corner.</summary>
        public static Image Surface(Image img)
        {
            return Round(img, SurfaceRadius);
        }

        /// <summary>
        /// Press feedback that shows on any fill: dark and translucent fills light up (alpha raised too), light and
        /// saturated fills darken. No hover tint (it sticks after a tap on touch screens).
        /// </summary>
        public static void PressFeedback(Selectable s, Color fill)
        {
            var colors = s.colors;
            bool dark = fill.a < 0.6f || Theme.Luminance(fill) < 0.18f;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = dark ? new Color(1.7f, 1.7f, 1.7f, 2.6f) : new Color(0.74f, 0.74f, 0.74f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            colors.fadeDuration = 0.06f;
            s.colors = colors;
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

        /// <summary>
        /// The red slash that opens a title ("/ SECTION"): a thin forward-leaning bar. Caller positions it.
        /// </summary>
        public static Image Slash(Transform parent, Color color)
        {
            var img = Decor("Slash", parent, color);
            Slant(img, 2.2f);
            return img;
        }

        /// <summary>
        /// A thin rule along the bottom edge of a card or tile (2 units, race red by default): the closing line of the
        /// championship's content cards.
        /// </summary>
        public static Image BottomRule(Transform parent, Color? color = null, float height = 2)
        {
            var img = Decor("BottomRule", parent, color ?? Theme.Race);
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0, height);
            return img;
        }

        private static Sprite _glow;

        /// <summary>A soft radial glow (white centre fading to transparent), tinted by its Image.</summary>
        public static Sprite GlowSprite
        {
            get
            {
                if (_glow != null) return _glow;
                const int size = 128;
                var tex = NewTexture(size, size, false);
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                        float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                        float a = 1 - d;
                        a = a * a * (3 - 2 * a); // smooth falloff, no visible edge
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                _glow = Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
                return _glow;
            }
        }

        /// <summary>
        /// A faint dark-red radial glow behind hero and background areas, centred at (<paramref name="x"/>,
        /// <paramref name="y"/>) of the parent and <paramref name="size"/> units wide. Decorative, ignores clicks.
        /// </summary>
        public static Image Glow(Transform parent, float x, float y, float size, float alpha = 0.14f)
        {
            var img = Decor("Glow", parent, Theme.Alpha(Theme.Race, alpha));
            img.sprite = GlowSprite;
            img.type = Image.Type.Simple;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(x, y);
            img.rectTransform.sizeDelta = new Vector2(size, size * 0.7f);
            img.transform.SetAsFirstSibling();
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
            // 18 units is the floor for any text (UX spec §1.2).
            t.fontSize = Mathf.Max(size, Theme.TextMin);
            t.alignment = align;
            t.color = color ?? Theme.Text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(string name, Transform parent, string text, Action onClick, Color? color = null, int size = Theme.FontNormal)
        {
            var fill = color ?? Theme.PanelLight;
            var img = Panel(name, parent, fill);
            // Flat capsule (no gloss, no shadow), bold sans label; the press state lights dark fills and darkens
            // bright ones so the feedback shows on every fill.
            Capsule(img);
            var button = img.gameObject.AddComponent<Button>();
            PressFeedback(button, fill);
            var label = Label("Label", img.transform, text, size, TextAnchor.MiddleCenter);
            Style(label, FontStyle.Bold);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(10, 0);
            label.rectTransform.offsetMax = new Vector2(-10, 0);
            // On a narrow phone a long caption shrinks (and may take two lines) instead of being cut.
            label.resizeTextForBestFit = true;
            label.resizeTextMaxSize = Mathf.Max(size, Theme.TextMin);
            label.resizeTextMinSize = Mathf.Min(label.resizeTextMaxSize, Mathf.Max(Theme.TextMin, size - 12));
            if (onClick != null) button.onClick.AddListener(() =>
            {
                GameAudio.ClickSound();
                onClick();
            });
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
            var row = Row(list, header ? height : UiScale.Compact ? height * 1.12f : height);
            if (background.HasValue || header)
            {
                var img = row.gameObject.AddComponent<Image>();
                // Table header: a faint darker band closed by a hairline (minimal borders, no coloured rule).
                img.color = background ?? Theme.Alpha(Theme.Ink, 0.35f);
                img.raycastTarget = false;
                if (header)
                {
                    var line = Decor("Underline", row, Theme.Alpha(Color.white, 0.14f));
                    line.rectTransform.anchorMin = new Vector2(0, 0);
                    line.rectTransform.anchorMax = new Vector2(1, 0);
                    line.rectTransform.offsetMin = new Vector2(0, 0);
                    line.rectTransform.offsetMax = new Vector2(0, 1);
                }
            }
            else
            {
                // Hairline between rows, like the lines of a timing monitor.
                var hair = Decor("Rule", row, Theme.Divider);
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
                // A position number in the first column is set big in the display face (standings style).
                int pos = !header && i == 0 ? PositionNumber(cell) : 0;
                if (pos > 0)
                {
                    var block = Decor("PosBlock", row, Color.clear);
                    block.rectTransform.anchorMin = new Vector2(x / total, 0);
                    block.rectTransform.anchorMax = new Vector2(x / total, 1);
                    block.rectTransform.pivot = new Vector2(0, 0.5f);
                    block.rectTransform.offsetMin = new Vector2(4, 4);
                    block.rectTransform.offsetMax = new Vector2(52, -4);
                    var n = Label("Cell" + i, block.transform, pos.ToString(), Theme.TextBody, TextAnchor.MiddleCenter, Theme.Text);
                    Display(n);
                    Stretch(n.rectTransform);
                    x += widths[i];
                    continue;
                }
                // Column captions in sentence case, bold and dim (as in the championship's own tables).
                var t = Label("Cell" + i, row, cell, header ? Theme.TextCaption : Theme.FontSmall,
                    i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, header ? Theme.TextDim : (textColor ?? Theme.Text));
                if (header)
                {
                    Style(t, FontStyle.Bold);
                    // Captions in narrow columns shrink instead of wrapping out of the band.
                    t.resizeTextForBestFit = true;
                    t.resizeTextMinSize = Theme.TextMin;
                    t.resizeTextMaxSize = Theme.TextCaption;
                }
                t.supportRichText = true;
                if (!header && UiScale.Compact)
                {
                    // Narrow columns on a phone: text shrinks a little rather than spilling into the next column.
                    t.resizeTextForBestFit = true;
                    t.resizeTextMinSize = Theme.TextMin;
                    t.resizeTextMaxSize = Theme.FontSmall;
                }
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
            // Section title: the red slash, heavy upright display caps and a plain hairline.
            var bar = Slash(row, Theme.Race);
            bar.rectTransform.anchorMin = bar.rectTransform.anchorMax = Vector2.zero;
            bar.rectTransform.pivot = Vector2.zero;
            bar.rectTransform.anchoredPosition = new Vector2(2, 12);
            bar.rectTransform.sizeDelta = new Vector2(14, 28);
            var rule = Decor("Rule", row, Theme.Divider);
            rule.rectTransform.anchorMin = new Vector2(0, 0);
            rule.rectTransform.anchorMax = new Vector2(1, 0);
            rule.rectTransform.offsetMin = new Vector2(0, 4);
            rule.rectTransform.offsetMax = new Vector2(0, 5);
            var t = Label("Heading", row, Theme.Caps(text), Theme.FontLarge - 6, TextAnchor.LowerLeft, Theme.Text);
            t.supportRichText = true;
            Display(t);
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Theme.FontNormal - 4;
            t.resizeTextMaxSize = Theme.FontLarge - 6;
            Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(30, 6);
            return t;
        }

        /// <summary>
        /// Gives a screen title the racing-graphics treatment: upper case in the heavy display face, and a short
        /// slanted red bar under it. Call after setting the text.
        /// </summary>
        public static Text RaceTitle(Text t)
        {
            t.supportRichText = true;
            t.text = Theme.Caps(t.text);
            Display(t);
            var bar = Decor("TitleBar", t.transform, Theme.Race);
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
                var label = b.GetComponentInChildren<Text>();
                if (buttons[i].Primary || buttons[i].Selected) label.color = Theme.OnColor(buttons[i].Color);
                Style(label, FontStyle.Bold);
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
            // Recessed field: darker than the card, a 1-unit edge and a thin team-colour rule at the bottom.
            var box = Panel("Input", row, Theme.Alpha(Theme.Ink, 0.55f));
            Surface(box);
            box.gameObject.AddComponent<Outline>().effectColor = Theme.Edge;
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
    /// <summary>
    /// Plain-text symbols for the controls that have no pictogram (the game keeps only its existing icons). They use
    /// ASCII so every device font has them. Pass them wherever a component takes an icon name.
    /// </summary>
    public static class Glyph
    {
        public const string Info = "i", Close = "X", More = "...", Plus = "+", Minus = "-", Warning = "!", Check = "OK",
            Chevron = ">", Back = "<", Down = "v";
    }

    /// <summary>
    /// Single-colour pictograms drawn from code (home, riders, bike, money, transfers, trophy, career...), so the
    /// navigation and the hub do not depend on symbol glyphs the device font may lack. Each is a white, softly
    /// anti-aliased sprite tinted by its Image.
    /// </summary>
    public static class UiIcons
    {
        public const string Home = "home", Team = "team", Person = "person", Bike = "bike", Finance = "finance",
            Market = "market", Trophy = "trophy", Career = "career", Menu = "menu", Back = "back", Star = "star",
            Sponsor = "sponsor", Flag = "flag", Development = "development";

        private const int Size = 96, Samples = 3;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Drawn = new HashSet<string>
        {
            Home, Team, Person, Bike, Finance, Market, Trophy, Career, Menu, Back, Star, Sponsor, Flag, Development
        };

        /// <summary>
        /// True for a name that has a pictogram (designed PNG or drawn shape). Components that take an icon name
        /// (<see cref="Ui.IconButton"/>, <see cref="Ui.Empty"/>, <see cref="Ui.StatTile"/>) draw any other string as
        /// plain text instead, so text symbols from <see cref="Glyph"/> ("i", "...", "X") work everywhere.
        /// </summary>
        public static bool Known(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (Drawn.Contains(name) || Cache.ContainsKey(name)) return true;
            return Resources.Load<Texture2D>("Icons/" + name) != null;
        }

        /// <summary>
        /// The Back chevron turned to point right (<paramref name="degrees"/> = 180), down (90) or up (-90): the
        /// chevron of tappable rows and tiles, the caret of pickers.
        /// </summary>
        public static Image Chevron(Transform parent, Color color, float degrees = 180)
        {
            var img = Icon(parent, Back, color);
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, degrees);
            return img;
        }

        /// <summary>The sprite of a named pictogram (an empty square for an unknown name).</summary>
        public static Sprite Get(string name)
        {
            Sprite s;
            if (Cache.TryGetValue(name, out s) && s != null) return s;
            // Designed icons (white on transparent, Resources/Icons/<name>.png) win over the drawn fallbacks.
            var art = Resources.Load<Texture2D>("Icons/" + name);
            if (art != null)
            {
                s = Sprite.Create(art, new UnityEngine.Rect(0, 0, art.width, art.height), new Vector2(0.5f, 0.5f), 100);
                Cache[name] = s;
                return s;
            }
            if (name == Development) name = Bike;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < Samples; sy++)
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            float u = (x + (sx + 0.5f) / Samples) / Size, v = (y + (sy + 0.5f) / Samples) / Size;
                            if (Inside(name, u, v)) hits++;
                        }
                    px[y * Size + x] = new Color32(255, 255, 255, (byte)(255 * hits / (Samples * Samples)));
                }
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new UnityEngine.Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100);
            Cache[name] = s;
            return s;
        }

        /// <summary>A decorative pictogram image (keeps its square shape inside any rect).</summary>
        public static Image Icon(Transform parent, string name, Color color)
        {
            var img = UiFactory.Decor("Icon", parent, color);
            img.sprite = Get(name);
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            return img;
        }

        private static bool Inside(string name, float u, float v)
        {
            switch (name)
            {
                case Home:
                    return (Tri(u, v, 0.08f, 0.5f, 0.5f, 0.9f, 0.92f, 0.5f) || Box(u, v, 0.2f, 0.1f, 0.8f, 0.52f)) && !Box(u, v, 0.42f, 0.1f, 0.58f, 0.34f);
                case Team:
                    return Circle(u, v, 0.33f, 0.66f, 0.14f) || Circle(u, v, 0.7f, 0.68f, 0.11f) ||
                           (Ellipse(u, v, 0.33f, 0.12f, 0.25f, 0.34f) && v >= 0.12f) ||
                           (Ellipse(u, v, 0.72f, 0.16f, 0.2f, 0.3f) && v >= 0.16f && !Ellipse(u, v, 0.33f, 0.12f, 0.31f, 0.4f));
                case Person:
                    return Circle(u, v, 0.5f, 0.68f, 0.17f) || (Ellipse(u, v, 0.5f, 0.1f, 0.32f, 0.38f) && v >= 0.1f);
                case Bike:
                    return Ring(u, v, 0.2f, 0.28f, 0.14f, 0.07f) || Ring(u, v, 0.8f, 0.28f, 0.14f, 0.07f) ||
                           Seg(u, v, 0.2f, 0.28f, 0.42f, 0.46f, 0.07f) || Box(u, v, 0.4f, 0.24f, 0.6f, 0.44f) ||
                           Seg(u, v, 0.3f, 0.54f, 0.64f, 0.54f, 0.12f) || Seg(u, v, 0.8f, 0.28f, 0.66f, 0.64f, 0.07f) ||
                           Seg(u, v, 0.6f, 0.68f, 0.76f, 0.72f, 0.07f);
                case Finance:
                    return Box(u, v, 0.12f, 0.14f, 0.3f, 0.46f) || Box(u, v, 0.41f, 0.14f, 0.59f, 0.66f) ||
                           Box(u, v, 0.7f, 0.14f, 0.88f, 0.86f) || Box(u, v, 0.06f, 0.06f, 0.94f, 0.1f);
                case Market:
                    return Seg(u, v, 0.12f, 0.66f, 0.7f, 0.66f, 0.09f) || Tri(u, v, 0.66f, 0.82f, 0.92f, 0.66f, 0.66f, 0.5f) ||
                           Seg(u, v, 0.88f, 0.34f, 0.3f, 0.34f, 0.09f) || Tri(u, v, 0.34f, 0.5f, 0.08f, 0.34f, 0.34f, 0.18f);
                case Trophy:
                    return Box(u, v, 0.28f, 0.62f, 0.72f, 0.9f) || (Circle(u, v, 0.5f, 0.62f, 0.22f) && v < 0.62f) ||
                           (Ring(u, v, 0.27f, 0.7f, 0.11f, 0.06f) && u < 0.3f) || (Ring(u, v, 0.73f, 0.7f, 0.11f, 0.06f) && u > 0.7f) ||
                           Box(u, v, 0.45f, 0.2f, 0.55f, 0.42f) || Box(u, v, 0.28f, 0.08f, 0.72f, 0.2f);
                case Career:
                    return (Box(u, v, 0.08f, 0.14f, 0.92f, 0.68f) && !Box(u, v, 0.08f, 0.42f, 0.92f, 0.46f)) ||
                           (Box(u, v, 0.34f, 0.68f, 0.66f, 0.84f) && !Box(u, v, 0.43f, 0.68f, 0.57f, 0.77f));
                case Menu:
                    return Seg(u, v, 0.18f, 0.25f, 0.82f, 0.25f, 0.1f) || Seg(u, v, 0.18f, 0.5f, 0.82f, 0.5f, 0.1f) || Seg(u, v, 0.18f, 0.75f, 0.82f, 0.75f, 0.1f);
                case Back:
                    return Seg(u, v, 0.64f, 0.86f, 0.3f, 0.5f, 0.15f) || Seg(u, v, 0.3f, 0.5f, 0.64f, 0.14f, 0.15f);
                case Star:
                    return StarShape(u, v);
                case Sponsor:
                    return Poly(u, v, new[] { 0.06f, 0.5f, 0.34f, 0.84f, 0.94f, 0.84f, 0.94f, 0.16f, 0.34f, 0.16f }) && !Circle(u, v, 0.32f, 0.5f, 0.08f);
                case Flag:
                    if (Seg(u, v, 0.14f, 0.06f, 0.14f, 0.94f, 0.07f)) return true;
                    if (!Box(u, v, 0.2f, 0.42f, 0.92f, 0.92f)) return false;
                    return ((int)((u - 0.2f) / 0.12f) + (int)((v - 0.42f) / 0.125f)) % 2 == 0;
            }
            return false;
        }

        private static bool Box(float u, float v, float x0, float y0, float x1, float y1)
        {
            return u >= x0 && u <= x1 && v >= y0 && v <= y1;
        }

        private static bool Circle(float u, float v, float cx, float cy, float r)
        {
            return (u - cx) * (u - cx) + (v - cy) * (v - cy) <= r * r;
        }

        private static bool Ellipse(float u, float v, float cx, float cy, float rx, float ry)
        {
            float a = (u - cx) / rx, b = (v - cy) / ry;
            return a * a + b * b <= 1;
        }

        private static bool Ring(float u, float v, float cx, float cy, float r, float w)
        {
            float d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));
            return Mathf.Abs(d - r) <= w * 0.5f;
        }

        private static bool Seg(float u, float v, float ax, float ay, float bx, float by, float w)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((u - ax) * dx + (v - ay) * dy) / Mathf.Max(1e-6f, dx * dx + dy * dy));
            float px = ax + t * dx - u, py = ay + t * dy - v;
            return px * px + py * py <= w * w * 0.25f;
        }

        private static bool Tri(float u, float v, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (u - bx) * (ay - by) - (ax - bx) * (v - by);
            float d2 = (u - cx) * (by - cy) - (bx - cx) * (v - cy);
            float d3 = (u - ax) * (cy - ay) - (cx - ax) * (v - ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        /// <summary>Even-odd point in polygon; <paramref name="xy"/> holds x0, y0, x1, y1...</summary>
        private static bool Poly(float u, float v, float[] xy)
        {
            bool inside = false;
            int n = xy.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = xy[i * 2], yi = xy[i * 2 + 1], xj = xy[j * 2], yj = xy[j * 2 + 1];
                if ((yi > v) != (yj > v) && u < (xj - xi) * (v - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        private static float[] _star;

        private static bool StarShape(float u, float v)
        {
            if (_star == null)
            {
                _star = new float[20];
                for (int i = 0; i < 10; i++)
                {
                    float r = i % 2 == 0 ? 0.46f : 0.19f;
                    float a = Mathf.PI / 2 + i * Mathf.PI / 5;
                    _star[i * 2] = 0.5f + r * Mathf.Cos(a);
                    _star[i * 2 + 1] = 0.47f + r * Mathf.Sin(a);
                }
            }
            return Poly(u, v, _star);
        }
    }
}
