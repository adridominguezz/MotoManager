using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Full-screen loading screen for long tasks (loading data, creating the world, simulating): title, a progress
    /// bar and rotating paddock messages. The work runs on the worker thread, so the bar shows elapsed time easing
    /// towards the end rather than a measured percentage, and jumps to 100% when the work is done.
    /// </summary>
    public class LoadingOverlay : MonoBehaviour
    {
        private static LoadingOverlay _current;
        private Text _message;
        private Text _percent;
        private Image _fill;
        private Text _task;
        private float _start;
        private float _doneAt = -1;
        private int _tip = -1;
        private const int Tips = 10;

        public static bool IsShown { get { return _current != null; } }

        public static void Show(string task)
        {
            if (_current == null)
            {
                var canvas = UiFactory.CreateCanvas("Loading", 60);
                _current = canvas.gameObject.AddComponent<LoadingOverlay>();
                _current.Build(UiFactory.SafeRoot(canvas, Theme.Background));
            }
            _current._task.text = task;
            _current._doneAt = -1;
        }

        /// <summary>Completes the bar and fades out.</summary>
        public static void Hide()
        {
            if (_current != null && _current._doneAt < 0) _current._doneAt = Time.unscaledTime;
        }

        private void Build(RectTransform root)
        {
            _start = Time.unscaledTime;
            // Carbon background with a faint weave, lighter at the top.
            var bg = UiFactory.Panel("Background", root, Theme.Background);
            UiFactory.Stretch(bg.rectTransform);
            UiFactory.Gradient(bg, 1.35f, 0.85f);
            var weave = UiFactory.Pattern("Carbon", root, UiFactory.CarbonSprite, new Color(1, 1, 1, 0.03f), 10);
            UiFactory.Stretch(weave.rectTransform);
            // A chequered flag band sweeping diagonally across the right side, with a thin accent line beside it.
            var flag = UiFactory.Pattern("Chequered", root, UiFactory.CheckerSprite, new Color(1, 1, 1, 0.9f), 56);
            flag.rectTransform.anchorMin = flag.rectTransform.anchorMax = new Vector2(0.84f, 0.55f);
            flag.rectTransform.sizeDelta = new Vector2(168, 2600);
            flag.rectTransform.localRotation = Quaternion.Euler(0, 0, -24);
            UiFactory.Gradient(flag, new Color(1, 1, 1, 0.35f), Color.white);
            var line = UiFactory.Decor("AccentLine", root, Theme.Accent);
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0.84f, 0.55f);
            line.rectTransform.sizeDelta = new Vector2(10, 2600);
            line.rectTransform.anchoredPosition = new Vector2(-120, 0);
            line.rectTransform.localRotation = Quaternion.Euler(0, 0, -24);
            var title = UiFactory.Label("Title", root, "<b>MOTORCYCLE</b>\n<size=" + (Theme.FontTitle + 30) + "><b>MOTORSPORT</b></size>\n<color=#" +
                ColorUtility.ToHtmlStringRGB(Theme.Accent) + "><b>MANAGER</b></color>",
                Theme.FontLarge, TextAnchor.MiddleLeft, Theme.Text);
            title.supportRichText = true;
            UiFactory.Style(title, FontStyle.Italic);
            var lift = UiFactory.DropShadow(title, 0.6f, 4);
            lift.effectDistance = new Vector2(3, -4);
            UiFactory.Place(title.rectTransform, 0.06f, 0.35f, 0.66f, 0.85f);
            // Thin rule under the wordmark, fading to the right.
            var rule = UiFactory.Decor("Rule", root, new Color(1, 1, 1, 0.25f));
            UiFactory.Place(rule.rectTransform, 0.06f, 0.33f, 0.5f, 0.33f);
            rule.rectTransform.offsetMax = new Vector2(0, 2);
            UiFactory.Gradient(rule, Color.white, new Color(1, 1, 1, 0), true);

            var bar = UiFactory.Panel("Bar", root, new Color(0.02f, 0.02f, 0.025f, 0.96f));
            UiFactory.Place(bar.rectTransform, 0, 0, 1, 0.16f);
            UiFactory.Gradient(bar, 1.2f, 0.9f);
            var rim = UiFactory.Decor("Rim", bar.transform, Theme.Accent);
            rim.rectTransform.anchorMin = new Vector2(0, 1);
            rim.rectTransform.anchorMax = new Vector2(1, 1);
            rim.rectTransform.offsetMin = new Vector2(0, -3);
            rim.rectTransform.offsetMax = Vector2.zero;
            _message = UiFactory.Label("Message", bar.transform, "", Theme.FontNormal, TextAnchor.MiddleCenter, Color.white);
            UiFactory.Style(_message, FontStyle.BoldAndItalic);
            UiFactory.Place(_message.rectTransform, 0.1f, 0.45f, 0.82f, 0.95f);
            // The percentage in timing-screen yellow.
            _percent = UiFactory.Label("Percent", bar.transform, "", Theme.FontNormal, TextAnchor.MiddleRight, Theme.Timing);
            UiFactory.Style(_percent, FontStyle.BoldAndItalic);
            UiFactory.Place(_percent.rectTransform, 0.82f, 0.45f, 0.95f, 0.95f);
            var back = UiFactory.Panel("Track", bar.transform, new Color(1, 1, 1, 0.1f));
            UiFactory.Place(back.rectTransform, 0.05f, 0.28f, 0.95f, 0.36f);
            _fill = UiFactory.Panel("Fill", back.transform, Theme.Accent);
            UiFactory.Gradient(_fill, new Color(0.75f, 0.75f, 0.75f, 1), Color.white, true);
            _fill.rectTransform.anchorMin = Vector2.zero;
            _fill.rectTransform.offsetMin = _fill.rectTransform.offsetMax = Vector2.zero;
            // Sector marks along the track, like a lap progress bar.
            for (int k = 1; k < 10; k++)
            {
                var tick = UiFactory.Decor("Tick", back.transform, new Color(0, 0, 0, 0.6f));
                tick.rectTransform.anchorMin = new Vector2(k * 0.1f, 0);
                tick.rectTransform.anchorMax = new Vector2(k * 0.1f, 1);
                tick.rectTransform.offsetMin = new Vector2(-1, 0);
                tick.rectTransform.offsetMax = new Vector2(1, 0);
            }
            _task = UiFactory.Label("Task", bar.transform, "", Theme.TextMin, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.6f));
            UiFactory.Place(_task.rectTransform, 0.05f, 0.02f, 0.95f, 0.26f);
        }

        private void Update()
        {
            float t = Time.unscaledTime - _start;
            float p = 0.95f * (1 - Mathf.Exp(-t / 2.5f));
            if (_doneAt >= 0) p = Mathf.Lerp(p, 1, Mathf.Clamp01((Time.unscaledTime - _doneAt) / 0.3f));
            _fill.rectTransform.anchorMax = new Vector2(p, 1);
            _percent.text = Mathf.RoundToInt(p * 100) + "%";
            int tip = (int)(t / 1.8f) % Tips;
            if (tip != _tip)
            {
                _tip = tip;
                _message.text = Loc.T("load.tip." + (tip + 1)).ToUpperInvariant();
            }
            if (_doneAt >= 0 && Time.unscaledTime - _doneAt > 0.45f)
            {
                if (_current == this) _current = null;
                Destroy(gameObject);
            }
        }
    }
}
