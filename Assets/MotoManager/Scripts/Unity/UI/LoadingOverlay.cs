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
                _current.Build((RectTransform)canvas.transform);
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
            var bg = UiFactory.Panel("Background", root, new Color32(222, 228, 240, 255));
            UiFactory.Stretch(bg.rectTransform);
            UiFactory.Gradient(bg, 1.06f, 0.9f);
            // Diagonal colour bands on the right, like a livery.
            for (int i = 0; i < 4; i++)
            {
                var band = UiFactory.Panel("Band", root, i % 2 == 0 ? new Color32(60, 70, 200, 255) : new Color32(90, 120, 230, 255));
                band.rectTransform.anchorMin = band.rectTransform.anchorMax = new Vector2(0.78f + i * 0.08f, 0.5f);
                band.rectTransform.sizeDelta = new Vector2(150 - i * 20, 2600);
                band.rectTransform.localRotation = Quaternion.Euler(0, 0, -28);
            }
            var title = UiFactory.Label("Title", root, "<b>MOTORCYCLE</b>\n<size=" + (Theme.FontTitle + 30) + "><b>MOTORSPORT</b></size>\n<color=#E5383B><b>MANAGER</b></color>",
                Theme.FontLarge, TextAnchor.MiddleCenter, new Color32(18, 36, 80, 255));
            title.supportRichText = true;
            var lift = UiFactory.DropShadow(title, 0.18f, 4);
            lift.effectDistance = new Vector2(3, -4);
            UiFactory.Place(title.rectTransform, 0.05f, 0.35f, 0.6f, 0.85f);

            var bar = UiFactory.Panel("Bar", root, new Color32(18, 36, 80, 235));
            UiFactory.Place(bar.rectTransform, 0, 0, 1, 0.16f);
            UiFactory.Gradient(bar, 1.35f, 0.85f);
            var rim = UiFactory.Decor("Rim", bar.transform, Theme.Accent);
            rim.rectTransform.anchorMin = new Vector2(0, 1);
            rim.rectTransform.anchorMax = new Vector2(1, 1);
            rim.rectTransform.offsetMin = new Vector2(0, -5);
            rim.rectTransform.offsetMax = Vector2.zero;
            _message = UiFactory.Label("Message", bar.transform, "", Theme.FontNormal, TextAnchor.MiddleCenter, Color.white);
            _message.fontStyle = FontStyle.Bold;
            UiFactory.Place(_message.rectTransform, 0.1f, 0.45f, 0.82f, 0.95f);
            _percent = UiFactory.Label("Percent", bar.transform, "", Theme.FontNormal, TextAnchor.MiddleRight, Color.white);
            _percent.fontStyle = FontStyle.Bold;
            UiFactory.Place(_percent.rectTransform, 0.82f, 0.45f, 0.95f, 0.95f);
            var back = UiFactory.Panel("Track", bar.transform, new Color(1, 1, 1, 0.15f), true);
            UiFactory.Place(back.rectTransform, 0.05f, 0.28f, 0.95f, 0.36f);
            _fill = UiFactory.Panel("Fill", back.transform, new Color32(250, 210, 40, 255), true);
            UiFactory.Gradient(_fill, 1.25f, 0.8f);
            _fill.rectTransform.anchorMin = Vector2.zero;
            _fill.rectTransform.offsetMin = _fill.rectTransform.offsetMax = Vector2.zero;
            _task = UiFactory.Label("Task", bar.transform, "", Theme.FontSmall - 4, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.6f));
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
