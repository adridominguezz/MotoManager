using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// First screen: loads the data, starts a new career or continues the saved one, and lets the player pick the
    /// language. Once a career is running the management interface takes over.
    /// </summary>
    public class BootScreen : MonoBehaviour
    {
        private GameSession _session;
        private Text _status;
        private RectTransform _buttons;
        private RectTransform _list;
        private string _shownBusy;
        private bool _menuShown;

        public static BootScreen Create(GameSession session)
        {
            var canvas = UiFactory.CreateCanvas("BootCanvas");
            var screen = canvas.gameObject.AddComponent<BootScreen>();
            screen._session = session;
            screen.Build(UiFactory.SafeRoot(canvas, Theme.Background));
            GameAudio.Menu();
            return screen;
        }

        private void Build(Transform root)
        {
            Theme.SetTeamColors(null, null);
            // Outside a career the accent is the brand red.
            Theme.SetAccent(Theme.Accent);
            var bg = UiFactory.Panel("Background", root, Theme.Background);
            UiFactory.Stretch(bg.rectTransform);
            UiFactory.Gradient(bg, 1.3f, 0.88f);
            var weave = UiFactory.Pattern("Carbon", bg.transform, UiFactory.CarbonSprite, new Color(1, 1, 1, 0.025f), 10);
            UiFactory.Stretch(weave.rectTransform);

            var left = UiFactory.Panel("Left", bg.transform, Color.Lerp(Theme.Background, Theme.Panel, 0.7f));
            UiFactory.Place(left.rectTransform, 0, 0, 0.36f, 1);
            UiFactory.Gradient(left, 1.12f, 0.9f);
            UiFactory.DropShadow(left, 0.5f, 0).effectDistance = new Vector2(6, 0);
            // A chequered band and a thin accent line slanting behind the menu, clipped to the panel, and an accent
            // edge on its right side.
            var livery = UiFactory.Rect("Livery", left.transform);
            UiFactory.Stretch(livery);
            livery.gameObject.AddComponent<RectMask2D>();
            var flag = UiFactory.Pattern("Chequered", livery, UiFactory.CheckerSprite, new Color(1, 1, 1, 0.07f), 40);
            flag.rectTransform.anchorMin = flag.rectTransform.anchorMax = new Vector2(0.86f, 0.3f);
            flag.rectTransform.sizeDelta = new Vector2(80, 1600);
            flag.rectTransform.localRotation = Quaternion.Euler(0, 0, -24);
            var band = UiFactory.Decor("Band", livery, Theme.Alpha(Theme.Accent, 0.35f));
            band.rectTransform.anchorMin = band.rectTransform.anchorMax = new Vector2(0.86f, 0.3f);
            band.rectTransform.anchoredPosition = new Vector2(-70, 0);
            band.rectTransform.sizeDelta = new Vector2(6, 1600);
            band.rectTransform.localRotation = Quaternion.Euler(0, 0, -24);
            var edge = UiFactory.Decor("Edge", left.transform, Theme.Accent);
            edge.rectTransform.anchorMin = new Vector2(1, 0);
            edge.rectTransform.anchorMax = new Vector2(1, 1);
            edge.rectTransform.offsetMin = new Vector2(-3, 0);
            edge.rectTransform.offsetMax = Vector2.zero;
            UiFactory.Gradient(edge, Color.white, new Color(1, 1, 1, 0.15f));
            var title = UiFactory.Label("Title", left.transform, "<b>MOTORCYCLE</b>\nMOTORSPORT\n<color=#" + ColorUtility.ToHtmlStringRGB(Theme.Accent) + "><b>MANAGER</b></color>", Theme.FontTitle, TextAnchor.UpperLeft);
            title.supportRichText = true;
            title.fontStyle = FontStyle.Italic;
            UiFactory.DropShadow(title, 0.6f, 3);
            UiFactory.Place(title.rectTransform, 0.08f, 0.66f, 0.95f, 0.95f);
            _status = UiFactory.Label("Status", left.transform, "", Theme.FontSmall + 2, TextAnchor.UpperLeft, Theme.TextDim);
            _status.supportRichText = true;
            UiFactory.Place(_status.rectTransform, 0.08f, 0.54f, 0.95f, 0.66f);
            _buttons = UiFactory.Rect("Buttons", left.transform);
            UiFactory.Place(_buttons, 0.08f, 0.04f, 0.92f, 0.54f);
            var layout = _buttons.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            _list = UiFactory.ScrollList("List", bg.transform);
            UiFactory.Place((RectTransform)_list.parent.parent, 0.37f, 0.02f, 0.98f, 0.98f);

            _session.DataLoaded += ShowMainMenu;
            _session.GameChanged += ShowCareer;
            Loc.LanguageChanged += OnLanguageChanged;
            // Coming back from a career: the data is already loaded.
            if (_session.Db != null) ShowMainMenu();
            else _session.LoadData();
        }

        private void OnDestroy()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            if (_session == null) return;
            _session.DataLoaded -= ShowMainMenu;
            _session.GameChanged -= ShowCareer;
        }

        private void OnLanguageChanged()
        {
            if (_menuShown) ShowMainMenu();
        }

        private void Update()
        {
            if (_session == null) return;
            string busy = _session.BusyLabel;
            if (busy != _shownBusy)
            {
                _shownBusy = busy;
                foreach (var b in _buttons.GetComponentsInChildren<Button>()) b.interactable = busy == null;
            }
            if (busy != null) _status.text = busy + new string('.', 1 + (int)(Time.unscaledTime * 2) % 3);
            else if (_session.LoadError != null) _status.text = Loc.T("boot.loadError", _session.LoadError);
        }

        private void AddButton(string text, System.Action action, Color? color = null, float height = 100)
        {
            var b = UiFactory.Button("Button", _buttons, text, action, color);
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }

        private void ShowMainMenu()
        {
            _menuShown = true;
            UiFactory.Clear(_buttons);
            UiFactory.Clear(_list);
            var db = _session.Db;
            _status.text = Loc.T("boot.loaded", db.Riders.Count, db.Teams.Count, db.Circuits.Count) +
                           (_session.DataErrors.Count > 0 ? "\n" + Loc.T("boot.warnings", _session.DataErrors.Count) : "");
            if (SaveManager.Exists(SaveManager.AutoSlot)) AddButton(Loc.T("boot.continue"), () => _session.Continue(), Theme.Accent);
            AddButton(Loc.T("career.new"), () => _session.PrepareCareer((ulong)System.DateTime.Now.Ticks, game =>
            {
                _menuShown = false;
                UiFactory.Clear(_buttons);
                new CareerStartFlow(_session, game, _list, _status).Render();
            }), SaveManager.Exists(SaveManager.AutoSlot) ? Theme.PanelLight : Theme.Accent);

            // Language.
            var row = UiFactory.Row(_buttons, UiScale.Touch(64));
            string[] langs = { "es", "en" };
            string[] names = { "Español", "English" };
            for (int i = 0; i < 2; i++)
            {
                string lang = langs[i];
                var b = UiFactory.Button("Lang", row, names[i], () => Loc.SetLanguage(lang), Loc.Language == lang ? Theme.Accent : Theme.PanelLight, Theme.FontSmall);
                UiFactory.Place((RectTransform)b.transform, i * 0.5f, 0, i * 0.5f + 0.5f, 1, 4);
            }

            // Interface size (phones start larger; the player can adjust).
            var sizes = UiFactory.Row(_buttons, UiScale.Touch(64));
            string[] sizeKeys = { "boot.size.small", "boot.size.normal", "boot.size.large" };
            for (int i = 0; i < sizeKeys.Length; i++)
            {
                int level = i;
                var b = UiFactory.Button("Size", sizes, Loc.T(sizeKeys[i]), () => { UiScale.UserLevel = level; ShowMainMenu(); },
                    UiScale.UserLevel == level ? Theme.Accent : Theme.PanelLight, Theme.FontSmall);
                UiFactory.Place((RectTransform)b.transform, i / 3f, 0, (i + 1) / 3f, 1, 4);
            }

            // Music and sound.
            var audio = UiFactory.Row(_buttons, UiScale.Touch(64));
            var mus = UiFactory.Button("Music", audio, Loc.T("audio.music") + (GameAudio.MusicOn ? "  ✓" : ""), () => { GameAudio.MusicOn = !GameAudio.MusicOn; ShowMainMenu(); },
                GameAudio.MusicOn ? Theme.Accent : Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)mus.transform, 0, 0, 0.5f, 1, 4);
            var sfx = UiFactory.Button("Sound", audio, Loc.T("audio.sound") + (GameAudio.SfxOn ? "  ✓" : ""), () => { GameAudio.SfxOn = !GameAudio.SfxOn; ShowMainMenu(); },
                GameAudio.SfxOn ? Theme.Accent : Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)sfx.transform, 0.5f, 0, 1, 1, 4);

            var intro = Ui.Card(_list, Loc.T("boot.welcome"), Loc.T("boot.welcomeSub"));
            UiFactory.Paragraph(intro, Loc.T("boot.intro"), Theme.FontSmall + 2);
        }

        private void ShowCareer()
        {
            if (_session.Game == null)
            {
                _status.text = Loc.T("boot.loadFailed");
                return;
            }
            // The management interface takes over; the boot screen is no longer needed.
            Screens.ManagementShell.Create(_session);
            Destroy(gameObject);
        }
    }
}
