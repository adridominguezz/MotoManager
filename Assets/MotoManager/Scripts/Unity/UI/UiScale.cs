using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Size of the interface on the device. The layout is designed for 1080 units of height; on a desktop monitor
    /// that is the screen height. A phone held in landscape is only about 7 cm tall, so the same 1080 units would
    /// make text and buttons tiny: there the canvas uses fewer units of height (everything is drawn larger) chosen
    /// from the physical size of the screen, and the player can still pick small / normal / large.
    /// </summary>
    public static class UiScale
    {
        public const float DesignHeight = 1080;
        private const string Pref = "MotoManager.UiScale";
        public static readonly float[] UserScales = { 0.85f, 1f, 1.18f };
        private static readonly List<CanvasScaler> Scalers = new List<CanvasScaler>();
        private static int _user = -1;

        /// <summary>Physical height of the screen in inches (0 when the device does not report its density).</summary>
        public static float ScreenInches
        {
            get
            {
                float dpi = Screen.dpi;
                return dpi > 1 ? Mathf.Min(Screen.width, Screen.height) / dpi : 0;
            }
        }

        /// <summary>A phone or small tablet (touch, small screen): larger text, larger touch targets, safe margins.</summary>
        public static bool Handheld
        {
            get
            {
                float inches = ScreenInches;
                return Application.isMobilePlatform || (inches > 0 && inches < 4.5f);
            }
        }

        /// <summary>0 small, 1 normal, 2 large (remembered on the device).</summary>
        public static int UserLevel
        {
            get
            {
                if (_user < 0)
                {
                    try { _user = Mathf.Clamp(PlayerPrefs.GetInt(Pref, 1), 0, UserScales.Length - 1); } catch (Exception) { _user = 1; }
                }
                return _user;
            }
            set
            {
                _user = Mathf.Clamp(value, 0, UserScales.Length - 1);
                try
                {
                    PlayerPrefs.SetInt(Pref, _user);
                    PlayerPrefs.Save();
                }
                catch (Exception) { }
                Apply();
            }
        }

        /// <summary>
        /// Canvas units of screen height. Desktop: the design height. Handheld: about 2.6 mm for normal text
        /// (28 units), i.e. ~270 units per inch of screen height, never above the design height.
        /// </summary>
        public static float ReferenceHeight
        {
            get
            {
                float h = DesignHeight;
                if (Handheld)
                {
                    float inches = ScreenInches;
                    h = inches > 0 ? Mathf.Clamp(inches * 270f, 700f, DesignHeight) : 760f;
                }
                return h / UserScales[UserLevel];
            }
        }

        /// <summary>Height of a row with buttons: taller on a phone so fingers hit them.</summary>
        public static float Touch(float height)
        {
            return Handheld ? height * 1.2f : height;
        }

        public static void Register(CanvasScaler scaler)
        {
            Scalers.RemoveAll(s => s == null);
            Scalers.Add(scaler);
            scaler.referenceResolution = new Vector2(DesignHeight * 16f / 9f, ReferenceHeight);
        }

        /// <summary>Re-applies the scale to every open canvas (after the player changes the size).</summary>
        public static void Apply()
        {
            Scalers.RemoveAll(s => s == null);
            foreach (var s in Scalers) s.referenceResolution = new Vector2(DesignHeight * 16f / 9f, ReferenceHeight);
        }
    }

    /// <summary>
    /// Keeps its rect inside the part of the screen that is safe to use: away from the camera notch, the rounded
    /// corners and the system bars. On phones a minimum margin is kept on every side even when the system reports
    /// the whole screen as safe (rounded corners are not always reported).
    /// </summary>
    public class SafeArea : MonoBehaviour
    {
        private Rect _applied;
        private Vector2Int _screen;

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            if (_applied != Screen.safeArea || _screen.x != Screen.width || _screen.y != Screen.height) Refresh();
        }

        private void Refresh()
        {
            var rt = transform as RectTransform;
            if (rt == null || Screen.width <= 0 || Screen.height <= 0) return;
            var safe = Screen.safeArea;
            _applied = safe;
            _screen = new Vector2Int(Screen.width, Screen.height);
            float w = Screen.width, h = Screen.height;
            float xMin = safe.xMin / w, xMax = safe.xMax / w, yMin = safe.yMin / h, yMax = safe.yMax / h;
            if (UiScale.Handheld)
            {
                // Rounded corners: a small margin everywhere, a little more at the sides.
                xMin = Mathf.Max(xMin, 0.025f);
                xMax = Mathf.Min(xMax, 0.975f);
                yMin = Mathf.Max(yMin, 0.02f);
                yMax = Mathf.Min(yMax, 0.98f);
            }
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
