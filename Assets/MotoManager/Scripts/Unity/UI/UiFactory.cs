using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>Shared colours and sizes so every screen looks consistent.</summary>
    public static class Theme
    {
        public static readonly Color Background = new Color32(14, 17, 22, 255);
        public static readonly Color Panel = new Color32(24, 29, 37, 255);
        public static readonly Color PanelLight = new Color32(34, 41, 52, 255);
        public static readonly Color Accent = new Color32(225, 6, 0, 255);
        public static readonly Color Text = new Color32(235, 238, 242, 255);
        public static readonly Color TextDim = new Color32(150, 160, 175, 255);
        public static readonly Color Good = new Color32(60, 200, 120, 255);
        public static readonly Color Warning = new Color32(240, 180, 40, 255);
        public const int FontSmall = 22;
        public const int FontNormal = 28;
        public const int FontLarge = 40;
        public const int FontTitle = 56;
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
            scaler.matchWidthOrHeight = 0.5f;
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

        public static Image Panel(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
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
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            var label = Label("Label", img.transform, text, size, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        /// <summary>Vertical scroll list. Add children to the returned content transform.</summary>
        public static RectTransform ScrollList(string name, Transform parent, float spacing = 6)
        {
            var bg = Panel(name, parent, Theme.Panel);
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
            layout.spacing = spacing;
            layout.padding = new RectOffset(8, 8, 8, 8);
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
