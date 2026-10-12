using System.Collections.Generic;
using System.Text;
using MotoManager.Core.Common;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI.Screens
{
    /// <summary>A management page hosted by the shell (one at a time, rebuilt when shown or refreshed).</summary>
    public abstract class ScreenBase
    {
        public abstract string Title { get; }
        protected ManagementShell Shell;
        protected GameSession Session { get { return Shell.Session; } }
        protected GameController Game { get { return Shell.Session.Game; } }

        public void Attach(ManagementShell shell)
        {
            Shell = shell;
        }

        /// <summary>Builds the page into <paramref name="content"/> (cleared beforehand).</summary>
        public abstract void Build(RectTransform content);

        /// <summary>A full-size scroll list for the page.</summary>
        protected RectTransform Page(RectTransform content)
        {
            var list = UiFactory.ScrollList(GetType().Name, content);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            return list;
        }
    }

    /// <summary>One entry of the navigation rail, with its pages shown as tabs.</summary>
    public class Section
    {
        public string TitleKey;
        public readonly List<ScreenBase> Pages = new List<ScreenBase>();
        public int Current;
    }

    /// <summary>
    /// The career interface: a navigation rail with six sections, a header with the team, money, date and the
    /// main action ("Continue"), tabs for the pages of a section, and the active page. Long simulations run on the
    /// session's worker thread while a progress label is shown.
    /// </summary>
    public class ManagementShell : MonoBehaviour
    {
        public GameSession Session { get; private set; }
        private readonly List<Section> _sections = new List<Section>();
        private readonly List<Image> _navButtons = new List<Image>();
        private RectTransform _content;
        private RectTransform _tabs;
        private Text _header;
        private Text _subHeader;
        private Text _busy;
        private Text _continueLabel;
        private Section _section;
        private string _shownBusy;
        private Text _toast;
        private Image _toastBg;
        private Image _toastStripe;
        private float _toastUntil;
        // Purely visual parts recoloured when the section changes.
        private readonly List<Text> _navLabels = new List<Text>();
        private readonly List<Image> _navBars = new List<Image>();
        private readonly List<Image> _navDots = new List<Image>();
        private Text _logo;
        private readonly List<Image> _livery = new List<Image>();
        private Image _headerLine;

        /// <summary>Result of the last simulated or played event, shown on the home page.</summary>
        public string LastResult = "";

        public static ManagementShell Create(GameSession session)
        {
            var t = session.Game != null ? session.Game.World.PlayerTeam : null;
            Theme.SetTeamColors(t != null ? t.Colors.Primary : null, t != null ? t.Colors.Secondary : null);
            var canvas = UiFactory.CreateCanvas("Management", 5);
            var shell = canvas.gameObject.AddComponent<ManagementShell>();
            shell.Session = session;
            shell.Build(UiFactory.SafeRoot(canvas, Theme.Background));
            GameAudio.Menu();
            return shell;
        }

        private Section Add(string titleKey, params ScreenBase[] pages)
        {
            var s = new Section { TitleKey = titleKey };
            foreach (var p in pages)
            {
                p.Attach(this);
                s.Pages.Add(p);
            }
            _sections.Add(s);
            return s;
        }

        private void Build(RectTransform root)
        {
            var bg = UiFactory.Panel("Background", root, Theme.Background);
            UiFactory.Stretch(bg.rectTransform);
            // Carbon floor: a very faint weave over the near-black, a touch lighter at the top, and two thin
            // diagonal livery lines in the team colour at the far right.
            UiFactory.Gradient(bg, 1.25f, 0.9f);
            bg.gameObject.AddComponent<RectMask2D>();
            var weave = UiFactory.Pattern("Carbon", bg.transform, UiFactory.CarbonSprite, new Color(1, 1, 1, 0.022f), 10);
            UiFactory.Stretch(weave.rectTransform);
            UiFactory.Gradient(weave, Color.white, new Color(1, 1, 1, 0.3f));
            for (int i = 0; i < 2; i++)
            {
                var band = UiFactory.Decor("Livery", bg.transform, Color.white);
                band.rectTransform.anchorMin = band.rectTransform.anchorMax = new Vector2(0.93f + i * 0.03f, 0.15f);
                band.rectTransform.sizeDelta = new Vector2(i == 0 ? 26 : 8, 1400);
                band.rectTransform.localRotation = Quaternion.Euler(0, 0, -28);
                _livery.Add(band);
            }

            // Header: who and where we are, money, and the main action.
            var header = UiFactory.Panel("Header", root, Color.Lerp(Theme.Background, Theme.Panel, 0.5f));
            UiFactory.Place(header.rectTransform, 0, 0.9f, 1, 1);
            UiFactory.Gradient(header, 1.15f, 0.95f);
            UiFactory.DropShadow(header, 0.45f, 3);
            // Logo: a black plate with an italic wordmark and a chequered-flag edge.
            var plate = UiFactory.Panel("LogoPlate", header.transform, new Color(0.02f, 0.02f, 0.025f, 1));
            UiFactory.Place(plate.rectTransform, 0, 0, 0.14f, 1);
            plate.raycastTarget = false;
            plate.gameObject.AddComponent<RectMask2D>();
            var flag = UiFactory.Chequered(plate.transform, 0.84f, 0, 1, 1, 9, 0.9f);
            UiFactory.Gradient(flag, new Color(1, 1, 1, 0.15f), Color.white, true);
            var logo = _logo = UiFactory.Label("Logo", plate.transform, LogoText(), Theme.FontSmall + 2, TextAnchor.MiddleLeft, Theme.Text);
            logo.supportRichText = true;
            logo.fontStyle = FontStyle.Italic;
            logo.lineSpacing = 0.85f;
            logo.resizeTextForBestFit = true;
            logo.resizeTextMinSize = Theme.FontSmall - 8;
            logo.resizeTextMaxSize = Theme.FontSmall + 2;
            UiFactory.DropShadow(logo, 0.6f, 2);
            UiFactory.Place(logo.rectTransform, 0.1f, 0.08f, 0.8f, 0.92f);
            // Faint hatching in the free middle of the header, behind the date line.
            var hatch = UiFactory.Pattern("Hatch", header.transform, UiFactory.StripesSprite, new Color(1, 1, 1, 0.03f), 20);
            UiFactory.Place(hatch.rectTransform, 0.42f, 0, 0.565f, 1);
            UiFactory.Gradient(hatch, new Color(1, 1, 1, 0), Color.white, true);
            // Team-coloured line along the bottom of the header, fading to the right.
            _headerLine = UiFactory.Decor("AccentLine", header.transform, Theme.Accent);
            _headerLine.rectTransform.anchorMin = new Vector2(0, 0);
            _headerLine.rectTransform.anchorMax = new Vector2(1, 0);
            _headerLine.rectTransform.offsetMin = new Vector2(0, 0);
            _headerLine.rectTransform.offsetMax = new Vector2(0, 3);
            UiFactory.Gradient(_headerLine, Color.white, new Color(1, 1, 1, 0.05f), true);
            _header = UiFactory.Label("Team", header.transform, "", Theme.FontNormal + 2, TextAnchor.LowerLeft);
            _header.supportRichText = true;
            _header.fontStyle = FontStyle.Italic;
            UiFactory.DropShadow(_header, 0.45f, 2);
            UiFactory.Place(_header.rectTransform, 0.155f, 0.48f, 0.56f, 0.96f);
            _subHeader = UiFactory.Label("Date", header.transform, "", Theme.FontSmall - 2, TextAnchor.UpperLeft, Theme.TextDim);
            _subHeader.supportRichText = true;
            UiFactory.Place(_subHeader.rectTransform, 0.155f, 0.04f, 0.56f, 0.48f);
            _busy = UiFactory.Label("Busy", header.transform, "", Theme.FontSmall, TextAnchor.MiddleRight, Theme.Warning);
            UiFactory.Place(_busy.rectTransform, 0.3f, 0, 0.56f, 1);
            bool compact = UiScale.Compact;
            if (compact)
            {
                // Small screens: no logo plate, the team line starts at the edge, and Save / Menu / text size live
                // in one menu button.
                plate.gameObject.SetActive(false);
                hatch.gameObject.SetActive(false);
                UiFactory.Place(_header.rectTransform, 0.1f, 0.48f, 0.66f, 0.96f);
                UiFactory.Place(_subHeader.rectTransform, 0.1f, 0.04f, 0.66f, 0.48f);
                UiFactory.Place(_busy.rectTransform, 0.3f, 0, 0.66f, 1);
                var more = UiFactory.Button("MenuButton", header.transform, "☰", ToggleMenu, Theme.PanelLight, Theme.FontLarge);
                UiFactory.Place((RectTransform)more.transform, 0.005f, 0.1f, 0.09f, 0.9f, 4);
            }
            else
            {
                var menu = UiFactory.Button("Menu", header.transform, Loc.T("shell.menu"), ExitToMenu, Theme.PanelLight, Theme.FontSmall);
                UiFactory.Place((RectTransform)menu.transform, 0.57f, 0.16f, 0.665f, 0.84f, 4);
                var save = UiFactory.Button("Save", header.transform, Loc.T("shell.save"), () => { Session.Save(); Toast(Loc.T("shell.saved")); }, Theme.PanelLight, Theme.FontSmall);
                UiFactory.Place((RectTransform)save.transform, 0.67f, 0.16f, 0.77f, 0.84f, 4);
            }
            var go = UiFactory.Button("Continue", header.transform, "", Continue, Theme.Accent, Theme.FontNormal);
            UiFactory.Place((RectTransform)go.transform, compact ? 0.68f : 0.78f, 0.12f, 0.99f, 0.88f, 4);
            _continueLabel = go.GetComponentInChildren<Text>();
            _continueLabel.supportRichText = true;
            _continueLabel.fontStyle = FontStyle.Italic;
            // The main action is a slanted accent block (like a "GO" board), with a thin light rim.
            UiFactory.Slant(go.image, 2f);
            go.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.12f);
            UiFactory.Gradient(go.image, 1.12f, 0.88f);

            var rail = UiFactory.Panel("Rail", root, Color.Lerp(Theme.Background, Theme.Panel, 0.75f));
            UiFactory.Place(rail.rectTransform, 0, 0, RailWidth, 0.9f);
            UiFactory.Gradient(rail, 1.08f, 0.9f);
            var edge = UiFactory.Decor("Edge", rail.transform, new Color(1, 1, 1, 0.06f));
            edge.rectTransform.anchorMin = new Vector2(1, 0);
            edge.rectTransform.anchorMax = new Vector2(1, 1);
            edge.rectTransform.offsetMin = new Vector2(-2, 0);
            edge.rectTransform.offsetMax = Vector2.zero;
            var layout = rail.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = compact ? new RectOffset(6, 6, 8, 8) : new RectOffset(12, 12, 16, 16);
            layout.spacing = compact ? 5 : 10;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            // Compact: the sections share the rail's height instead of overflowing it.
            layout.childForceExpandHeight = compact;

            _tabs = UiFactory.Rect("Tabs", root);
            UiFactory.Place(_tabs, RailWidth + 0.01f, 0.82f, 0.99f, 0.885f);
            _content = UiFactory.Rect("Content", root);

            _toastBg = UiFactory.Panel("Toast", root, Theme.Alpha(Theme.Panel, 0.98f));
            UiFactory.Chamfer(_toastBg, 1.6f);
            UiFactory.Place(_toastBg.rectTransform, 0.3f, 0.03f, 0.9f, 0.11f);
            UiFactory.Gradient(_toastBg, 1.1f, 0.92f);
            UiFactory.DropShadow(_toastBg, 0.5f, 4);
            _toastBg.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.1f);
            _toastStripe = UiFactory.SideStripe(_toastBg.transform, Theme.Good, 6, 14);
            _toast = UiFactory.Label("ToastText", _toastBg.transform, "", Theme.FontNormal - 2, TextAnchor.MiddleCenter);
            _toast.supportRichText = true;
            UiFactory.DropShadow(_toast, 0.4f, 1.5f);
            UiFactory.Stretch(_toast.rectTransform);
            _toastBg.gameObject.SetActive(false);

            Add("nav.home", new HubScreen());
            Add("nav.team", new RidersPage(), new StaffPage(), new ManagerPage());
            Add("nav.bike", new BikeScreen(), new ProjectsPage(), new FacilitiesPage());
            Add("nav.finance", new FinanceScreen(), new SponsorsPage(), new ShopScreen());
            Add("nav.market", new MarketScreen());
            Add("nav.competition", new StandingsScreen(), new CalendarScreen(), new HistoryScreen());
            Add("nav.career", new CareerSummaryPage(), new CareerOffersPage(), new CareerOrgPage(), new TeamSetupPage(), new CareerFoundPage());

            foreach (var s in _sections)
            {
                var section = s;
                var b = UiFactory.Button("Nav", rail.transform, Loc.T(s.TitleKey), () => Show(section), Theme.PanelLight, Theme.FontSmall + 2);
                var navLe = b.gameObject.AddComponent<LayoutElement>();
                navLe.preferredHeight = compact ? 50 : 86;
                if (compact) navLe.flexibleHeight = 1;
                _navButtons.Add(b.GetComponent<Image>());
                // Pit-board style entries: upper-case labels, a small slanted marker (accent when open, steel
                // otherwise) and an accent bar on the left edge of the open section.
                var label = b.GetComponentInChildren<Text>();
                label.text = Theme.Caps(label.text);
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(compact ? 34 : 46, 0);
                label.rectTransform.offsetMax = new Vector2(-6, 0);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = Theme.FontSmall - 6;
                label.resizeTextMaxSize = Theme.FontSmall + 2;
                _navLabels.Add(label);
                var dot = UiFactory.Decor("Dot", b.transform, Theme.Steel);
                UiFactory.Slant(dot, 5f);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0, 0.5f);
                dot.rectTransform.anchoredPosition = new Vector2(compact ? 18 : 26, 0);
                dot.rectTransform.sizeDelta = new Vector2(14, 20);
                _navDots.Add(dot);
                var bar = UiFactory.SideStripe(b.transform, Theme.Accent, 5, 9);
                _navBars.Add(bar);
            }
            Show(_sections[0]);
            if (TeamSetupPage.OpenOnStart)
            {
                TeamSetupPage.OpenOnStart = false;
                Show<TeamSetupPage>();
            }
            Assistant.Show("home", new[] { "tut.home.1", "tut.home.2", "tut.home.3", "tut.home.4", "tut.home.5" });
        }

        public void Show(Section section)
        {
            _section = section;
            var accent = Theme.SectionAccent(section.TitleKey);
            Theme.SetAccent(accent);
            for (int i = 0; i < _sections.Count; i++)
            {
                bool on = _sections[i] == section;
                // Open section: a lighter graphite plate, white bold italic label, accent marker and edge bar.
                _navButtons[i].color = on ? Theme.PanelLight : Color.Lerp(Theme.Background, Theme.Panel, 0.9f);
                _navLabels[i].color = on ? Color.white : Color.Lerp(Theme.TextDim, Color.white, 0.2f);
                _navLabels[i].fontStyle = on ? FontStyle.BoldAndItalic : FontStyle.Normal;
                _navDots[i].color = on ? accent : Theme.Alpha(Theme.Steel, 0.6f);
                _navBars[i].color = accent;
                _navBars[i].gameObject.SetActive(on);
            }
            _headerLine.color = accent;
            if (_logo != null) _logo.text = LogoText();
            for (int i = 0; i < _livery.Count; i++) _livery[i].color = Theme.Alpha(accent, i == 0 ? 0.05f : 0.08f);
            Refresh();
        }

        private RectTransform _menuPanel;
        /// <summary>Width of the navigation rail (narrower on the phone layout: more room for content).</summary>
        private static float RailWidth { get { return UiScale.Compact ? 0.115f : 0.14f; } }

        /// <summary>Compact header menu: save, back to the start screen, text size.</summary>
        private void ToggleMenu()
        {
            if (_menuPanel != null)
            {
                Destroy(_menuPanel.gameObject);
                _menuPanel = null;
                return;
            }
            var root = (RectTransform)_toastBg.transform.parent;
            var shade = UiFactory.Panel("MenuShade", root, new Color(0, 0, 0, 0.5f));
            UiFactory.Stretch(shade.rectTransform);
            shade.gameObject.AddComponent<Button>().onClick.AddListener(ToggleMenu);
            _menuPanel = shade.rectTransform;
            var panel = UiFactory.Panel("Menu", shade.transform, Theme.Panel);
            UiFactory.Chamfer(panel, 1.6f);
            UiFactory.Place(panel.rectTransform, 0.01f, 0.35f, 0.36f, 0.89f);
            var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 12, 12);
            v.spacing = 8;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = true;
            UiFactory.ButtonRow(panel.transform, 60, UiFactory.Act(Loc.T("shell.save"), () => { Session.Save(); Toast(Loc.T("shell.saved")); ToggleMenu(); }));
            UiFactory.ButtonRow(panel.transform, 60, UiFactory.Act(Loc.T("shell.menu"), () => { ToggleMenu(); ExitToMenu(); }));
            // Manual save slots (the autosave runs after every event).
            var slots = new List<UiAct>();
            foreach (var slot in SaveManager.Slots)
            {
                var sl = slot;
                var meta = SaveManager.ReadMeta(slot);
                slots.Add(UiFactory.Act(Loc.T("save.saveTo", slot.Substring(slot.Length - 1)) + (meta != null ? " ●" : ""), () =>
                {
                    ToggleMenu();
                    Session.Save(sl, () => Toast(Loc.T("save.saved", sl.Substring(sl.Length - 1))));
                }));
            }
            UiFactory.ButtonRow(panel.transform, 60, slots.ToArray());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UiFactory.ButtonRow(panel.transform, 60, DebugManagement.Actions(Session, () => { ToggleMenu(); Refresh(); }));
#endif
            UiFactory.ButtonRow(panel.transform, 60,
                UiFactory.Option(Loc.T("audio.music"), GameAudio.MusicOn, () => { GameAudio.MusicOn = !GameAudio.MusicOn; ToggleMenu(); ToggleMenu(); }),
                UiFactory.Option(Loc.T("audio.sound"), GameAudio.SfxOn, () => { GameAudio.SfxOn = !GameAudio.SfxOn; ToggleMenu(); ToggleMenu(); }));
            UiFactory.ButtonRow(panel.transform, 60,
                UiFactory.Option("A-", UiScale.UserLevel == 0, () => SetTextSize(0)),
                UiFactory.Option("A", UiScale.UserLevel == 1, () => SetTextSize(1)),
                UiFactory.Option("A+", UiScale.UserLevel == 2, () => SetTextSize(2)));
            _menuPanel.SetAsLastSibling();
        }

        private void SetTextSize(int level)
        {
            UiScale.UserLevel = level;
            ToggleMenu();
            Refresh();
        }

        /// <summary>The wordmark: "MOTO" in white, "MANAGER" in the team colour.</summary>
        private static string LogoText()
        {
            return "<b>MOTO</b>\n<color=#" + ColorUtility.ToHtmlStringRGB(Color.Lerp(Theme.Accent, Color.white, 0.1f)) + "><b>MANAGER</b></color>";
        }

        /// <summary>Opens the section and tab holding a page of this type.</summary>
        public void Show<T>() where T : ScreenBase
        {
            foreach (var s in _sections)
                for (int i = 0; i < s.Pages.Count; i++)
                    if (s.Pages[i] is T)
                    {
                        s.Current = i;
                        Show(s);
                        return;
                    }
        }

        /// <summary>Short message at the bottom of the screen (result of an action).</summary>
        public void Toast(string message, bool good = true)
        {
            _toast.text = message;
            _toast.color = good ? Theme.Text : Theme.Warning;
            _toastStripe.color = good ? Theme.Good : Theme.Warning;
            _toastBg.gameObject.SetActive(true);
            _toastBg.transform.SetAsLastSibling();
            _toastUntil = Time.unscaledTime + 4f;
        }

        /// <summary>Rebuilds the header and the current page (call after anything changes the world).</summary>
        public void Refresh()
        {
            var game = Session.Game;
            if (game == null) return;
            var world = game.World;
            var team = world.PlayerTeam;
            // The interface wears the team's colours (brand red without a team).
            Theme.SetTeamColors(team != null ? team.Colors.Primary : null, team != null ? team.Colors.Secondary : null);
            if (_section != null) Theme.SetAccent(Theme.SectionAccent(_section.TitleKey));
            _headerLine.color = Theme.Accent;
            _logo.text = LogoText();
            var next = NextOwnEvent();
            _header.text = "<b>" + (team != null ? team.Name : Loc.T("shell.noTeam")) + "</b>" +
                           (team != null ? "    " + Theme.Money(Accounts.Cash(world, team)) : "");
            _subHeader.text = world.CurrentDate.ToString("d MMM yyyy", Loc.Culture) + "  ·  " + Loc.T("shell.season", world.Season) +
                              (next != null ? "  ·  " + Loc.T("shell.next", Loc.Round(next.Round.Name)) : "");
            _continueLabel.text = "<b>" + ContinueLabel() + "</b>   ►";

            UiFactory.Clear(_tabs);
            bool tabs = _section.Pages.Count > 1;
            _tabs.gameObject.SetActive(tabs);
            UiFactory.Place(_content, RailWidth + 0.01f, 0.01f, 0.995f, tabs ? 0.815f : 0.89f);
            if (tabs)
            {
                // Timing-screen tabs: slanted graphite segments with upper-case labels; the open tab is lighter,
                // bold italic, with an accent bar along its base. A thin rule runs under the whole strip.
                var rule = UiFactory.Decor("TabRule", _tabs, new Color(1, 1, 1, 0.08f));
                rule.rectTransform.anchorMin = new Vector2(0, 0);
                rule.rectTransform.anchorMax = new Vector2(1, 0);
                rule.rectTransform.offsetMin = new Vector2(0, -3);
                rule.rectTransform.offsetMax = new Vector2(0, -2);
                UiFactory.Gradient(rule, Color.white, new Color(1, 1, 1, 0), true);
                var accent = Theme.CurrentAccent;
                for (int i = 0; i < _section.Pages.Count; i++)
                {
                    int idx = i;
                    bool on = i == _section.Current;
                    var b = UiFactory.Button("Tab", _tabs, Theme.Caps(_section.Pages[i].Title), () =>
                    {
                        _section.Current = idx;
                        Refresh();
                    }, on ? Theme.PanelLight : Color.Lerp(Theme.Background, Theme.Panel, 0.85f), Theme.FontSmall);
                    UiFactory.Slant(b.image, 2.4f);
                    UiFactory.Place((RectTransform)b.transform, i * 0.2f, 0, i * 0.2f + 0.195f, 1, 3);
                    var label = b.GetComponentInChildren<Text>();
                    label.color = on ? Color.white : Color.Lerp(Theme.TextDim, Color.white, 0.2f);
                    label.fontStyle = on ? FontStyle.BoldAndItalic : FontStyle.Normal;
                    label.rectTransform.offsetMin = new Vector2(10, 0);
                    label.rectTransform.offsetMax = new Vector2(-10, 0);
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = Theme.FontSmall - 6;
                    label.resizeTextMaxSize = Theme.FontSmall;
                    if (!on) continue;
                    var underline = UiFactory.Decor("Underline", b.transform, accent);
                    UiFactory.Slant(underline, 6f);
                    underline.rectTransform.anchorMin = new Vector2(0, 0);
                    underline.rectTransform.anchorMax = new Vector2(1, 0);
                    underline.rectTransform.offsetMin = new Vector2(4, 0);
                    underline.rectTransform.offsetMax = new Vector2(-6, 5);
                }
            }
            UiFactory.Clear(_content);
            _section.Pages[Mathf.Clamp(_section.Current, 0, _section.Pages.Count - 1)].Build(_content);
        }

        /// <summary>Saves and goes back to the start screen (continue, new career, language).</summary>
        public void ExitToMenu()
        {
            Session.Save();
            BootScreen.Create(Session);
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ main action

        /// <summary>The next event of the player's own championship (other championships run in the background).</summary>
        public EventPlan NextOwnEvent()
        {
            var game = Session.Game;
            var team = game.World.PlayerTeam;
            return team != null ? game.PeekNextEventFor(team.CategoryId) : game.PeekNextEvent();
        }

        private string ContinueLabel()
        {
            if (Session.Game.World.PlayerTeam == null) return Session.Game.IsSeasonOver ? Loc.T("shell.closeSeason") : Loc.T("shell.simulate");
            if (NextOwnEvent() != null) return Loc.T("shell.goRace");
            return Session.Game.IsSeasonOver ? Loc.T("shell.closeSeason") : Loc.T("shell.finishSeason");
        }

        /// <summary>
        /// The one button that moves the career forward: race the team's next event (simulating other championships'
        /// events before it in the background), or finish and close the season.
        /// </summary>
        public void Continue()
        {
            var game = Session.Game;
            if (game.World.PlayerTeam == null)
            {
                // Without a team the season goes on event by event while the manager looks for a job.
                if (game.IsSeasonOver) CloseSeason();
                else SimulateEvent();
                return;
            }
            if (NextOwnEvent() == null)
            {
                if (game.IsSeasonOver) CloseSeason();
                else SimulateSeason();
                return;
            }
            CatchUp(Play);
        }

        /// <summary>Runs the other championships' events up to the team's next one, then continues.</summary>
        private void CatchUp(System.Action then)
        {
            var game = Session.Game;
            var team = game.World.PlayerTeam;
            var next = game.PeekNextEvent();
            if (team == null || next == null || next.Categories.Contains(team.CategoryId))
            {
                then();
                return;
            }
            Session.Run(Loc.T("busy.otherEvents"), () => game.SimulateUntilEventFor(team.CategoryId), () =>
            {
                Refresh();
                then();
            });
        }

        public void Play()
        {
            new RaceFlow(Session, gameObject, summary =>
            {
                LastResult = summary;
                Refresh();
            }).Start();
        }

        /// <summary>Simulates the team's next event without watching it (and the other championships' before it).</summary>
        public void SimulateEvent()
        {
            var game = Session.Game;
            var team = game.World.PlayerTeam;
            string summary = "";
            EventDigest digest = null;
            Session.Run(Loc.T("busy.event"), () =>
            {
                if (team != null) game.SimulateUntilEventFor(team.CategoryId);
                var ev = game.BeginNextEvent();
                if (ev == null) return;
                // The team's weekend is recorded session by session for the summary.
                var w = ev.PlayerWeekend;
                if (w != null)
                {
                    w.HeadlessTimeStep = true;
                    digest = EventDigest.Run(game, w);
                }
                game.CompleteEvent(ev);
                if (digest != null) digest.Finish(game, w.CategoryId);
                summary = Loc.T("home.simulated", Loc.Round(ev.Plan.Round.Name));
            }, () =>
            {
                LastResult = summary;
                Session.Save();
                Refresh();
                if (digest != null) EventSummaryPanel.Show(digest, Refresh);
            });
        }

        public void SimulateSeason()
        {
            var game = Session.Game;
            Session.Run(Loc.T("busy.season"), () => game.SimulateRestOfSeason(), () =>
            {
                LastResult = Loc.T("home.seasonSimulated");
                Session.Save();
                Refresh();
            });
        }

        public void CloseSeason()
        {
            var game = Session.Game;
            string summary = "";
            Core.Career.SeasonSummary closed = null;
            Session.Run(Loc.T("busy.closing"), () =>
            {
                var s = game.EndSeason();
                closed = s;
                var sb = new StringBuilder("<b>" + Loc.T("home.closed", s.Season) + "</b>\n");
                foreach (var c in s.Champions)
                    sb.Append(Loc.T("home.champion", game.Db.GetCategory(c.CategoryId).ShortName, c.RiderName)).Append('\n');
                sb.Append(Loc.T("home.closedStats", s.Retired.Count, s.Market.Moves.Count, s.YouthIntake));
                summary = sb.ToString();
            }, () =>
            {
                LastResult = summary;
                Session.Save();
                Refresh();
                if (closed != null) SeasonReviewPanel.Show(closed, game, Refresh);
            });
        }

        private void Update()
        {
            string busy = Session.BusyLabel;
            if (busy != _shownBusy)
            {
                _shownBusy = busy;
                foreach (var b in GetComponentsInChildren<Button>()) b.interactable = busy == null;
            }
            _busy.text = busy != null ? busy + new string('.', 1 + (int)(Time.unscaledTime * 2) % 3) : "";
            if (_toastBg.gameObject.activeSelf && Time.unscaledTime > _toastUntil) _toastBg.gameObject.SetActive(false);
        }
    }
}
