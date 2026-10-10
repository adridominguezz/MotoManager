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
            screen.Build(canvas.transform);
            return screen;
        }

        private void Build(Transform root)
        {
            var bg = UiFactory.Panel("Background", root, Theme.Background);
            UiFactory.Stretch(bg.rectTransform);

            var left = UiFactory.Panel("Left", bg.transform, Theme.Panel);
            UiFactory.Place(left.rectTransform, 0, 0, 0.36f, 1);
            var title = UiFactory.Label("Title", left.transform, "<b>MOTORCYCLE</b>\nMOTORSPORT\n<color=" + Theme.BadHex + ">MANAGER</color>", Theme.FontTitle, TextAnchor.UpperLeft);
            title.supportRichText = true;
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
            var row = UiFactory.Row(_buttons, 64);
            string[] langs = { "es", "en" };
            string[] names = { "Español", "English" };
            for (int i = 0; i < 2; i++)
            {
                string lang = langs[i];
                var b = UiFactory.Button("Lang", row, names[i], () => Loc.SetLanguage(lang), Loc.Language == lang ? Theme.Accent : Theme.PanelLight, Theme.FontSmall);
                UiFactory.Place((RectTransform)b.transform, i * 0.5f, 0, i * 0.5f + 0.5f, 1, 4);
            }

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
