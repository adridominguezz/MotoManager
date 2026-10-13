using System;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// The team assistant: short introduction messages in a speech card ("tap to continue"). Each set of messages is
    /// shown once per device (remembered in PlayerPrefs), so it guides the first steps without getting in the way.
    /// </summary>
    public class Assistant : MonoBehaviour
    {
        private string[] _keys;
        private object[][] _args;
        private int _index;
        private Text _text;
        private Action _done;
        private string _pref;

        public const string Name = "Lucía";

        public static bool Seen(string id)
        {
            try { return PlayerPrefs.GetInt("MotoManager.Tutorial." + id, 0) == 1; } catch (Exception) { return true; }
        }

        /// <summary>Shows the messages (localisation keys) unless this set was already seen.</summary>
        public static void Show(string id, string[] messageKeys, Action done = null, object[][] args = null)
        {
            if (Seen(id) || messageKeys.Length == 0)
            {
                if (done != null) done();
                return;
            }
            var canvas = UiFactory.CreateCanvas("Assistant", 40);
            var a = canvas.gameObject.AddComponent<Assistant>();
            a._keys = messageKeys;
            a._args = args;
            a._done = done;
            a._pref = "MotoManager.Tutorial." + id;
            a.Build((RectTransform)canvas.transform);
        }

        private void Build(RectTransform root)
        {
            // The whole screen is the "next" button.
            var dim = UiFactory.Panel("Dim", root, new Color(0, 0, 0, 0.45f));
            UiFactory.Stretch(dim.rectTransform);
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Next);

            // Pit-radio card: graphite panel with angled corners, an accent strip on top and a light message plate.
            var card = UiFactory.Panel("Card", root, Theme.Panel);
            UiFactory.Surface(card);
            UiFactory.Place(card.rectTransform, 0.24f, 0.08f, 0.76f, 0.38f);
            card.raycastTarget = false;
            UiFactory.DropShadow(card, 0.5f, 5);
            card.gameObject.AddComponent<Outline>().effectColor = Theme.Edge;
            var strip = UiFactory.Decor("Accent", card.transform, Theme.Accent);
            strip.rectTransform.anchorMin = new Vector2(0, 1);
            strip.rectTransform.anchorMax = new Vector2(0.6f, 1);
            strip.rectTransform.offsetMin = new Vector2(0, -4);
            strip.rectTransform.offsetMax = Vector2.zero;

            // Avatar: initial on a dark badge with an accent rim, name and role.
            var avatar = UiFactory.Panel("Avatar", card.transform, Theme.Background);
            UiFactory.Surface(avatar);
            UiFactory.Place(avatar.rectTransform, 0.03f, 0.3f, 0.24f, 0.92f);
            avatar.raycastTarget = false;
            var ring = avatar.gameObject.AddComponent<Outline>();
            ring.effectColor = Theme.Alpha(Theme.Accent, 0.85f);
            ring.effectDistance = new Vector2(2, -2);
            var initial = UiFactory.Label("Initial", avatar.transform, Name.Substring(0, 1), Theme.FontTitle, TextAnchor.MiddleCenter, Theme.Text);
            UiFactory.Style(initial, FontStyle.BoldAndItalic);
            UiFactory.Stretch(initial.rectTransform);
            var who = UiFactory.Label("Who", card.transform, Theme.Dim(Loc.T("assistant.role")) + "\n<b>" + Name + "</b>", Theme.FontSmall, TextAnchor.MiddleCenter, Theme.Text);
            who.supportRichText = true;
            Ui.Fit(who, Theme.FontSmall);
            UiFactory.Place(who.rectTransform, 0.01f, 0.03f, 0.26f, 0.3f);

            var bubble = UiFactory.Panel("Bubble", card.transform, Theme.Plate);
            UiFactory.Surface(bubble);
            UiFactory.Place(bubble.rectTransform, 0.28f, 0.06f, 0.97f, 0.94f);
            bubble.raycastTarget = false;
            _text = UiFactory.Label("Text", bubble.transform, "", Theme.FontNormal - 2, TextAnchor.MiddleLeft, Theme.Ink);
            _text.supportRichText = true;
            Ui.Fit(_text, Theme.FontNormal - 2);
            UiFactory.Place(_text.rectTransform, 0.05f, 0.05f, 0.95f, 0.95f);

            var tap = UiFactory.Label("Tap", root, Theme.Caps(Loc.T("assistant.tap")), Theme.FontNormal, TextAnchor.MiddleCenter, Theme.Timing);
            UiFactory.Style(tap, FontStyle.BoldAndItalic);
            Ui.Fit(tap, Theme.FontNormal);
            tap.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            UiFactory.Place(tap.rectTransform, 0.3f, 0.01f, 0.7f, 0.075f);
            ShowCurrent();
        }

        private void ShowCurrent()
        {
            var args = _args != null && _index < _args.Length ? _args[_index] : null;
            _text.text = args != null ? Loc.T(_keys[_index], args) : Loc.T(_keys[_index]);
        }

        private void Next()
        {
            _index++;
            if (_index < _keys.Length)
            {
                ShowCurrent();
                return;
            }
            try
            {
                PlayerPrefs.SetInt(_pref, 1);
                PlayerPrefs.Save();
            }
            catch (Exception) { }
            Destroy(gameObject);
            if (_done != null) _done();
        }
    }
}
