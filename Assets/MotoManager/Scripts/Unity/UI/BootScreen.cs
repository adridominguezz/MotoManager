using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// First screen (UX spec §3.28): loads the data; on the left (5 columns) the title, a status caption and the actions
    /// (CONTINUAR as the primary when there is an autosave, CARGAR opening the save slots in a sheet, NUEVA CARRERA);
    /// on the right the welcome card while there is no save. Language, text size and audio sit behind the settings
    /// button at the top right. A new career runs <see cref="CareerStartFlow"/> over the whole screen; once a career is
    /// running the management interface takes over.
    /// </summary>
    public class BootScreen : MonoBehaviour
    {
        private GameSession _session;
        private RectTransform _page;
        private Text _status;
        private RectTransform _buttons;
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

        private void Build(RectTransform root)
        {
            Theme.SetTeamColors(null, null);
            // Outside a career the accent is the brand red.
            Theme.SetAccent(Theme.Accent);
            Ui.SheetHost = root;
            var bg = UiFactory.Panel("Background", root, Theme.Background);
            UiFactory.Stretch(bg.rectTransform);
            UiFactory.Gradient(bg, 1.3f, 0.88f);
            var weave = UiFactory.Pattern("Carbon", bg.transform, UiFactory.CarbonSprite, new Color(1, 1, 1, 0.025f), 10);
            UiFactory.Stretch(weave.rectTransform);

            _page = UiFactory.Rect("Page", root);
            UiFactory.Stretch(_page);
            _page.offsetMin = new Vector2(Ui.Grid.Gutter, Ui.Grid.Gutter);
            _page.offsetMax = new Vector2(-Ui.Grid.Gutter, -Ui.Grid.Gutter);
            BuildLeft(Ui.Columns12(_page, 5)[0]);

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
            if (_session == null || _status == null) return;
            string busy = _session.BusyLabel;
            if (busy != _shownBusy)
            {
                _shownBusy = busy;
                if (_buttons != null)
                    foreach (var b in _buttons.GetComponentsInChildren<Button>()) b.interactable = busy == null;
            }
            if (busy != null) _status.text = Theme.Caps(busy) + new string('.', 1 + (int)(Time.unscaledTime * 2) % 3);
            else if (_session.LoadError != null) _status.text = Theme.BadText(Loc.T("boot.loadError", _session.LoadError));
        }

        /// <summary>The left panel: livery, the title, the status caption and an empty button list.</summary>
        private void BuildLeft(RectTransform area)
        {
            var left = UiFactory.Panel("Left", area, Color.Lerp(Theme.Background, Theme.Panel, 0.7f));
            UiFactory.Stretch(left.rectTransform);
            UiFactory.Gradient(left, 1.12f, 0.9f);
            UiFactory.DropShadow(left, 0.5f, 0).effectDistance = new Vector2(6, 0);
            var livery = UiFactory.Rect("Livery", left.transform);
            UiFactory.Stretch(livery);
            livery.gameObject.AddComponent<RectMask2D>();
            var flag = UiFactory.Pattern("Chequered", livery, UiFactory.CheckerSprite, new Color(1, 1, 1, 0.05f), 40);
            flag.rectTransform.anchorMin = flag.rectTransform.anchorMax = new Vector2(0.88f, 0.3f);
            flag.rectTransform.sizeDelta = new Vector2(80, 1600);
            flag.rectTransform.localRotation = Quaternion.Euler(0, 0, -24);
            var edge = UiFactory.Decor("Edge", left.transform, Theme.Accent);
            edge.rectTransform.anchorMin = new Vector2(1, 0);
            edge.rectTransform.anchorMax = new Vector2(1, 1);
            edge.rectTransform.offsetMin = new Vector2(-3, 0);
            edge.rectTransform.offsetMax = Vector2.zero;
            UiFactory.Gradient(edge, Color.white, new Color(1, 1, 1, 0.15f));

            var title = Ui.Label(left.transform, "<b>MOTORCYCLE</b>\nMOTORSPORT\n<color=#" + ColorUtility.ToHtmlStringRGB(Theme.Accent) + "><b>MANAGER</b></color>",
                Theme.TextDisplay, TextAnchor.UpperLeft, Theme.Text, FontStyle.Italic);
            UiFactory.DropShadow(title, 0.6f, 3);
            UiFactory.Place(title.rectTransform, 0, 0.6f, 1, 1);
            title.rectTransform.offsetMin = new Vector2(32, 0);
            title.rectTransform.offsetMax = new Vector2(-24, -32);
            _status = Ui.Label(left.transform, "", Theme.TextCaption, TextAnchor.UpperLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(_status.rectTransform, 0, 0.44f, 1, 0.6f);
            _status.rectTransform.offsetMin = new Vector2(32, 0);
            _status.rectTransform.offsetMax = new Vector2(-24, 0);

            _buttons = UiFactory.Rect("Buttons", left.transform);
            UiFactory.Place(_buttons, 0, 0, 1, 0.44f);
            _buttons.offsetMin = new Vector2(32, 24);
            _buttons.offsetMax = new Vector2(-24, 0);
            var v = _buttons.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 12;
            v.childAlignment = TextAnchor.LowerCenter;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
        }

        private void ShowMainMenu()
        {
            _menuShown = true;
            Ui.CloseSheet();
            UiFactory.Clear(_page);
            _status = null;
            _buttons = null;
            _shownBusy = "";
            var cols = Ui.Columns12(_page, 5, 7);
            BuildLeft(cols[0]);
            var db = _session.Db;
            _status.text = Theme.Caps(Loc.T("boot.loaded", db.Riders.Count, db.Teams.Count, db.Circuits.Count));

            bool autosave = SaveManager.Exists(SaveManager.AutoSlot);
            bool anySlot = false;
            foreach (var s in SaveManager.Slots) if (SaveManager.Exists(s)) anySlot = true;
            if (_session.DataErrors.Count > 0) Ui.Alert(_buttons, Loc.T("boot.warnings", _session.DataErrors.Count));
            if (autosave) Ui.PrimaryCTA((Transform)_buttons, Loc.T("boot.continue"), () => _session.Continue());
            if (anySlot) Ui.Secondary(_buttons, Loc.T("boot.load"), ShowSlots);
            System.Action newCareer = () => _session.PrepareCareer((ulong)System.DateTime.Now.Ticks, game =>
            {
                _menuShown = false;
                Ui.CloseSheet();
                UiFactory.Clear(_page);
                _status = null;
                _buttons = null;
                new CareerStartFlow(_session, game, _page, ShowMainMenu).Render();
            });
            if (autosave) Ui.Secondary(_buttons, Loc.T("career.new"), newCareer);
            else Ui.PrimaryCTA((Transform)_buttons, Loc.T("career.new"), newCareer);

            // Settings: top right of the screen.
            var settings = Ui.IconButton(_page, UiIcons.Menu, OpenSettings);
            var srt = (RectTransform)settings.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(1, 1);
            srt.pivot = new Vector2(1, 1);
            srt.anchoredPosition = Vector2.zero;

            // Welcome card only before the first save.
            if (autosave || anySlot) return;
            var right = Wp4Ui.Below(cols[1], Ui.Grid.Hit + Ui.Grid.Gap);
            var intro = Ui.Tile(right, Loc.T("boot.welcome"), null, Theme.Caps(Loc.T("boot.welcomeSub")));
            var list = Wp4Ui.ScrollIn(intro, 8);
            UiFactory.Paragraph(list, Loc.T("boot.intro"), Theme.TextBody);
        }

        /// <summary>Language, text size, music and sound.</summary>
        private void OpenSettings()
        {
            Ui.Sheet(Loc.T("ux.boot.settings"), list =>
            {
                string[] langs = { "es", "en" };
                Wp4Ui.CaptionedSegmented(list, Loc.T("ux.boot.language"), new[] { "Español", "English" }, Loc.Language == "en" ? 1 : 0, i =>
                {
                    Loc.SetLanguage(langs[i]);
                    OpenSettings();
                });
                Wp4Ui.CaptionedSegmented(list, Loc.T("ux.boot.textSize"), new[] { Loc.T("boot.size.small"), Loc.T("boot.size.normal"), Loc.T("boot.size.large") },
                    UiScale.UserLevel, i =>
                    {
                        UiScale.UserLevel = i;
                        ShowMainMenu();
                        OpenSettings();
                    });
                var onOff = new[] { Loc.T("ux.boot.on"), Loc.T("ux.boot.off") };
                Wp4Ui.CaptionedSegmented(list, Loc.T("audio.music"), onOff, GameAudio.MusicOn ? 0 : 1, i => { GameAudio.MusicOn = i == 0; OpenSettings(); });
                Wp4Ui.CaptionedSegmented(list, Loc.T("audio.sound"), onOff, GameAudio.SfxOn ? 0 : 1, i => { GameAudio.SfxOn = i == 0; OpenSettings(); });
            }, null, null, 0.56f);
        }

        /// <summary>The manual save slots, each with what it holds, to load one.</summary>
        private void ShowSlots()
        {
            Ui.Sheet(Loc.T("boot.load"), list =>
            {
                foreach (var slot in SaveManager.Slots)
                {
                    var meta = SaveManager.ReadMeta(slot);
                    string name = Loc.T("save.slot", slot.Substring(slot.Length - 1));
                    if (meta == null)
                    {
                        Ui.Item(list, Theme.Dim(name), Theme.Dim(Loc.T("save.empty")));
                        continue;
                    }
                    var sl = slot;
                    Ui.Item(list, name + "  " + Theme.Dim(meta.Team), Loc.T("save.meta", meta.Manager, meta.Season, meta.SavedAt), null,
                        UiFactory.Act(Loc.T("save.loadButton"), () =>
                        {
                            Ui.CloseSheet();
                            _session.Load(sl);
                        }));
                }
            }, null, null, 0.62f);
        }

        private void ShowCareer()
        {
            if (_session.Game == null)
            {
                if (!_menuShown && _session.Db != null) ShowMainMenu();
                if (_status != null) _status.text = Theme.BadText(Loc.T("boot.loadFailed"));
                return;
            }
            Ui.CloseSheet();
            // The management interface takes over; the boot screen is no longer needed.
            Screens.ManagementShell.Create(_session);
            Destroy(gameObject);
        }
    }
}
