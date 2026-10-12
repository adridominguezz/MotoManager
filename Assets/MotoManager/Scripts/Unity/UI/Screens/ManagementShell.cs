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
    /// The career interface, laid out like a mobile motorsport manager: a slim icon rail on the left (the ☰ menu on
    /// top, then one big pictogram per section), a slim top bar (back, team, resource pills and the main action),
    /// tabs for the pages of a section, and the active page. Long simulations run on the session's worker thread
    /// while a progress label is shown.
    /// </summary>
    public class ManagementShell : MonoBehaviour
    {
        public GameSession Session { get; private set; }
        private readonly List<Section> _sections = new List<Section>();
        private readonly List<Image> _navButtons = new List<Image>();
        private RectTransform _content;
        private RectTransform _tabs;
        private RectTransform _pills;
        private RectTransform _info;
        private Button _back;
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
        private readonly List<Image> _navIcons = new List<Image>();
        private readonly List<Image> _livery = new List<Image>();
        private Image _headerLine;
        /// <summary>Where the back button returns to: pages opened from another page (hub tiles, shortcuts).</summary>
        private readonly List<KeyValuePair<Section, int>> _history = new List<KeyValuePair<Section, int>>();

        /// <summary>Height of the top bar and of the tab strip, in canvas units (touch-sized on a phone).</summary>
        public const float TopBarHeight = 84;
        private const float TabHeight = 62;
        /// <summary>Width of the main action button in the top bar.</summary>
        private const float GoWidth = 250;
        private const float PillsWidth = 470;

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

        /// <summary>The pictogram of each rail section.</summary>
        private static string IconOf(string key)
        {
            switch (key)
            {
                case "nav.home": return UiIcons.Home;
                case "nav.team": return UiIcons.Team;
                case "nav.bike": return UiIcons.Development;
                case "nav.finance": return UiIcons.Finance;
                case "nav.market": return UiIcons.Market;
                case "nav.competition": return UiIcons.Trophy;
                case "nav.career": return UiIcons.Career;
            }
            return UiIcons.Flag;
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

            // ---- Top bar: back, the team, resource pills and the main action.
            var header = UiFactory.Panel("TopBar", root, Color.Lerp(Theme.Background, Theme.Panel, 0.55f));
            var hrt = header.rectTransform;
            hrt.anchorMin = new Vector2(RailWidth, 1);
            hrt.anchorMax = new Vector2(1, 1);
            hrt.pivot = new Vector2(0.5f, 1);
            hrt.offsetMin = new Vector2(0, -TopBarHeight);
            hrt.offsetMax = Vector2.zero;
            UiFactory.Gradient(header, 1.15f, 0.95f);
            UiFactory.DropShadow(header, 0.45f, 3);
            _headerLine = UiFactory.Decor("AccentLine", header.transform, Theme.Accent);
            _headerLine.rectTransform.anchorMin = new Vector2(0, 0);
            _headerLine.rectTransform.anchorMax = new Vector2(1, 0);
            _headerLine.rectTransform.offsetMin = new Vector2(0, 0);
            _headerLine.rectTransform.offsetMax = new Vector2(0, 3);
            UiFactory.Gradient(_headerLine, Color.white, new Color(1, 1, 1, 0.05f), true);

            // Back: a red slanted block with a chevron, only inside a sub-page.
            _back = UiFactory.Button("Back", header.transform, "", Back, Theme.Brand, Theme.FontNormal);
            UiFactory.Slant(_back.image, 2.4f);
            var brt = (RectTransform)_back.transform;
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(0, 1);
            brt.pivot = new Vector2(0, 0.5f);
            brt.offsetMin = new Vector2(6, 10);
            brt.offsetMax = new Vector2(92, -10);
            var chevron = UiIcons.Icon(_back.transform, UiIcons.Back, Color.white);
            UiFactory.Place(chevron.rectTransform, 0.2f, 0.18f, 0.8f, 0.82f);

            // Team name (bold italic caps) and one small info line under it.
            _info = UiFactory.Rect("Info", header.transform);
            _header = UiFactory.Label("Team", _info, "", Theme.FontNormal + 2, TextAnchor.LowerLeft);
            _header.supportRichText = true;
            _header.fontStyle = FontStyle.BoldAndItalic;
            _header.resizeTextForBestFit = true;
            _header.resizeTextMinSize = Theme.FontSmall - 4;
            _header.resizeTextMaxSize = Theme.FontNormal + 2;
            UiFactory.DropShadow(_header, 0.45f, 2);
            UiFactory.Place(_header.rectTransform, 0, 0.46f, 1, 0.96f);
            _subHeader = UiFactory.Label("Date", _info, "", Theme.FontSmall - 4, TextAnchor.UpperLeft, Theme.TextDim);
            _subHeader.supportRichText = true;
            _subHeader.resizeTextForBestFit = true;
            _subHeader.resizeTextMinSize = Theme.FontSmall - 10;
            _subHeader.resizeTextMaxSize = Theme.FontSmall - 4;
            UiFactory.Place(_subHeader.rectTransform, 0, 0.06f, 1, 0.46f);
            _busy = UiFactory.Label("Busy", _info, "", Theme.FontSmall - 4, TextAnchor.UpperRight, Theme.Warning);
            _busy.alignment = TextAnchor.UpperLeft;
            UiFactory.Place(_busy.rectTransform, 0, 0.06f, 1, 0.46f);

            // Resource pills (cash, reputation, championship), right-aligned before the main action.
            _pills = UiFactory.Rect("Pills", header.transform);
            _pills.anchorMin = new Vector2(1, 0);
            _pills.anchorMax = new Vector2(1, 1);
            _pills.pivot = new Vector2(1, 0.5f);
            _pills.offsetMin = new Vector2(-GoWidth - 16 - PillsWidth, 12);
            _pills.offsetMax = new Vector2(-GoWidth - 16, -12);
            var ph = _pills.gameObject.AddComponent<HorizontalLayoutGroup>();
            ph.spacing = 8;
            ph.childAlignment = TextAnchor.MiddleRight;
            ph.childControlWidth = true;
            ph.childControlHeight = true;
            ph.childForceExpandWidth = false;
            ph.childForceExpandHeight = true;

            // The main action: a big slanted accent block ("CARRERA ›").
            var go = UiFactory.Button("Continue", header.transform, "", Continue, Theme.Accent, Theme.FontNormal + 2);
            var grt = (RectTransform)go.transform;
            grt.anchorMin = new Vector2(1, 0);
            grt.anchorMax = new Vector2(1, 1);
            grt.pivot = new Vector2(1, 0.5f);
            grt.offsetMin = new Vector2(-GoWidth - 6, 8);
            grt.offsetMax = new Vector2(-6, -8);
            _continueLabel = go.GetComponentInChildren<Text>();
            _continueLabel.supportRichText = true;
            _continueLabel.fontStyle = FontStyle.BoldAndItalic;
            _continueLabel.resizeTextForBestFit = true;
            _continueLabel.resizeTextMinSize = Theme.FontSmall - 4;
            _continueLabel.resizeTextMaxSize = Theme.FontNormal + 2;
            _continueLabel.rectTransform.offsetMin = new Vector2(18, 0);
            _continueLabel.rectTransform.offsetMax = new Vector2(-18, 0);
            UiFactory.Slant(go.image, 2f);
            go.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.14f);
            UiFactory.Gradient(go.image, 1.15f, 0.85f);

            // ---- Icon rail: the ☰ menu in the top cell, then one big pictogram per section with a tiny caption.
            var rail = UiFactory.Panel("Rail", root, Color.Lerp(Theme.Background, Theme.Panel, 0.7f));
            UiFactory.Place(rail.rectTransform, 0, 0, RailWidth, 1);
            UiFactory.Gradient(rail, 1.08f, 0.9f);
            UiFactory.DropShadow(rail, 0.4f, 2);
            var edge = UiFactory.Decor("Edge", rail.transform, new Color(1, 1, 1, 0.06f));
            edge.rectTransform.anchorMin = new Vector2(1, 0);
            edge.rectTransform.anchorMax = new Vector2(1, 1);
            edge.rectTransform.offsetMin = new Vector2(-2, 0);
            edge.rectTransform.offsetMax = Vector2.zero;
            var more = UiFactory.Button("MenuButton", rail.transform, "", ToggleMenu, new Color(0, 0, 0, 0.35f), Theme.FontLarge);
            more.image.sprite = null;
            var mrt = (RectTransform)more.transform;
            mrt.anchorMin = new Vector2(0, 1);
            mrt.anchorMax = new Vector2(1, 1);
            mrt.pivot = new Vector2(0.5f, 1);
            mrt.offsetMin = new Vector2(0, -TopBarHeight);
            mrt.offsetMax = Vector2.zero;
            var menuIcon = UiIcons.Icon(more.transform, UiIcons.Menu, Theme.Text);
            UiFactory.Place(menuIcon.rectTransform, 0.25f, 0.22f, 0.75f, 0.78f);
            var nav = UiFactory.Rect("Nav", rail.transform);
            UiFactory.Stretch(nav);
            nav.offsetMax = new Vector2(0, -TopBarHeight - 4);
            nav.offsetMin = new Vector2(0, 4);
            var layout = nav.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 2, 0, 0);
            layout.spacing = 2;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            _tabs = UiFactory.Rect("Tabs", root);
            _tabs.anchorMin = new Vector2(RailWidth, 1);
            _tabs.anchorMax = new Vector2(1, 1);
            _tabs.pivot = new Vector2(0.5f, 1);
            _tabs.offsetMin = new Vector2(12, -TopBarHeight - 6 - TabHeight);
            _tabs.offsetMax = new Vector2(-12, -TopBarHeight - 6);
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
                // A rail entry: the open section is a lighter full-height block with an accent edge bar, a white
                // pictogram and caption; the others are flat with grey ones.
                var b = UiFactory.Button("Nav", nav, "", () =>
                {
                    _history.Clear();
                    Show(section);
                }, Color.clear, Theme.FontSmall);
                b.image.sprite = null;
                b.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
                _navButtons.Add(b.image);
                var icon = UiIcons.Icon(b.transform, IconOf(s.TitleKey), Theme.TextDim);
                UiFactory.Place(icon.rectTransform, 0.18f, 0.36f, 0.82f, 0.9f);
                _navIcons.Add(icon);
                var label = b.GetComponentInChildren<Text>();
                label.text = Theme.Caps(Loc.T(s.TitleKey));
                label.alignment = TextAnchor.MiddleCenter;
                UiFactory.Place(label.rectTransform, 0, 0.04f, 1, 0.36f);
                label.rectTransform.offsetMin = new Vector2(4, 0);
                label.rectTransform.offsetMax = new Vector2(-4, 0);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 10;
                label.resizeTextMaxSize = Theme.FontSmall - 6;
                _navLabels.Add(label);
                var bar = UiFactory.SideStripe(b.transform, Theme.Accent, 6, 0);
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
                _navButtons[i].color = on ? Theme.PanelLight : Color.clear;
                _navLabels[i].color = on ? Color.white : Color.Lerp(Theme.TextDim, Color.white, 0.1f);
                _navLabels[i].fontStyle = on ? FontStyle.BoldAndItalic : FontStyle.Bold;
                _navIcons[i].color = on ? Color.white : Theme.Alpha(Theme.TextDim, 0.85f);
                _navBars[i].color = accent;
                _navBars[i].gameObject.SetActive(on);
            }
            _headerLine.color = accent;
            for (int i = 0; i < _livery.Count; i++) _livery[i].color = Theme.Alpha(accent, i == 0 ? 0.05f : 0.08f);
            Refresh();
        }

        /// <summary>Back to the page this one was opened from, or to the hub.</summary>
        public void Back()
        {
            if (_history.Count > 0)
            {
                var last = _history[_history.Count - 1];
                _history.RemoveAt(_history.Count - 1);
                last.Key.Current = last.Value;
                Show(last.Key);
                return;
            }
            Show(_sections[0]);
        }

        private RectTransform _menuPanel;
        /// <summary>Width of the icon rail (a slim strip: the room goes to the content).</summary>
        public static float RailWidth { get { return 0.095f; } }

        /// <summary>Menu (☰ at the top of the rail): save, save slots, back to the start screen, sound, text size.</summary>
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
            // Beside the ☰ button, at the top of the screen: rows that always fit a phone screen.
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(RailWidth, 1);
            prt.anchorMax = new Vector2(Mathf.Min(0.99f, RailWidth + 0.42f), 1);
            prt.pivot = new Vector2(0, 1);
            prt.offsetMin = new Vector2(6, -8);
            prt.offsetMax = new Vector2(0, -8);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 12, 12);
            v.spacing = 8;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            UiFactory.ButtonRow(panel.transform, 60,
                UiFactory.Act(Loc.T("shell.save"), () => { Session.Save(); Toast(Loc.T("shell.saved")); ToggleMenu(); }),
                UiFactory.Act(Loc.T("shell.menu"), () => { ToggleMenu(); ExitToMenu(); }));
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

        /// <summary>
        /// Opens the section and tab holding a page of this type. The page it was opened from is remembered, so the
        /// back button returns there.
        /// </summary>
        public void Show<T>() where T : ScreenBase
        {
            foreach (var s in _sections)
                for (int i = 0; i < s.Pages.Count; i++)
                    if (s.Pages[i] is T)
                    {
                        if (_section != null && !(_section == s && _section.Current == i))
                        {
                            _history.Add(new KeyValuePair<Section, int>(_section, _section.Current));
                            if (_history.Count > 20) _history.RemoveAt(0);
                        }
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

        /// <summary>Rebuilds the top bar and the current page (call after anything changes the world).</summary>
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

            // Top bar: back (inside a sub-page), the team and one info line, the pills and the main action.
            bool sub = _section != _sections[0];
            _back.gameObject.SetActive(sub);
            _info.anchorMin = new Vector2(0, 0);
            _info.anchorMax = new Vector2(1, 1);
            _info.offsetMin = new Vector2(sub ? 106 : 18, 0);
            _info.offsetMax = new Vector2(-GoWidth - 24 - (team != null ? PillsWidth : 0), 0);
            var next = NextOwnEvent();
            _header.text = Theme.Caps(team != null ? team.Name : Loc.T("shell.noTeam"));
            string page = sub ? Theme.Caps(_section.Pages[Mathf.Clamp(_section.Current, 0, _section.Pages.Count - 1)].Title) + "  ·  " : "";
            _subHeader.text = page + world.CurrentDate.ToString("d MMM yyyy", Loc.Culture) +
                              (next != null ? "  ·  " + Loc.Round(next.Round.Name) : "  ·  " + Loc.T("shell.season", world.Season));
            _continueLabel.text = Theme.Caps(ContinueShort()) + "  ›";
            BuildPills(world, team);

            UiFactory.Clear(_tabs);
            bool tabs = _section.Pages.Count > 1;
            _tabs.gameObject.SetActive(tabs);
            _content.anchorMin = new Vector2(RailWidth, 0);
            _content.anchorMax = new Vector2(1, 1);
            _content.offsetMin = new Vector2(8, 6);
            _content.offsetMax = new Vector2(-8, -TopBarHeight - (tabs ? TabHeight + 10 : 6));
            if (tabs)
            {
                // Tabs: slanted graphite segments with upper-case labels; the open tab is lighter, bold italic,
                // with an accent bar along its base. A thin rule runs under the whole strip.
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
                    UiFactory.Place((RectTransform)b.transform, i * 0.2f, 0, i * 0.2f + 0.195f, 1, 2);
                    var label = b.GetComponentInChildren<Text>();
                    label.color = on ? Color.white : Color.Lerp(Theme.TextDim, Color.white, 0.2f);
                    label.fontStyle = on ? FontStyle.BoldAndItalic : FontStyle.Bold;
                    label.rectTransform.offsetMin = new Vector2(12, 0);
                    label.rectTransform.offsetMax = new Vector2(-12, 0);
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

        /// <summary>Compact resource pills: cash, reputation and the team's championship position.</summary>
        private void BuildPills(Core.World.WorldState world, Core.World.Team team)
        {
            UiFactory.Clear(_pills);
            if (team == null) return;
            Core.World.ChampionshipState champ;
            int pos = 0;
            double pts = 0;
            if (world.Championships.TryGetValue(team.CategoryId, out champ))
            {
                var table = champ.SortedTeams();
                for (int i = 0; i < table.Count; i++)
                    if (table[i].Id == team.Id) { pos = i + 1; pts = table[i].Points; }
            }
            Pill(Loc.T("home.tile.cash"), Theme.Money(Accounts.Cash(world, team)), null, 170, () => Show<FinanceScreen>());
            Pill(Loc.T("shell.rep"), team.Reputation.ToString("0"), UiIcons.Star, 130, null);
            Pill(Loc.T("home.tile.championship"), (pos > 0 ? "P" + pos : "-") + "  " + Theme.Dim(Loc.T("home.tile.points", pts.ToString("0"))), null, 160, () => Show<StandingsScreen>());
        }

        private void Pill(string caption, string value, string icon, float width, System.Action onTap)
        {
            var b = UiFactory.Button("Pill", _pills, "", onTap, new Color(0, 0, 0, 0.4f), Theme.FontSmall);
            UiFactory.Slant(b.image, 3f);
            b.interactable = onTap != null;
            var colors = b.colors;
            colors.disabledColor = Color.white;
            b.colors = colors;
            b.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.07f);
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width * 0.7f;
            var label = b.GetComponentInChildren<Text>();
            label.supportRichText = true;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = "<size=" + (Theme.FontSmall - 8) + ">" + Theme.Dim(Theme.Caps(caption)) + "</size>\n<b>" + value + "</b>";
            label.lineSpacing = 0.9f;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Theme.FontSmall - 8;
            label.resizeTextMaxSize = Theme.FontSmall + 2;
            label.rectTransform.offsetMin = new Vector2(icon != null ? 40 : 12, 0);
            label.rectTransform.offsetMax = new Vector2(-12, 0);
            if (icon != null)
            {
                var img = UiIcons.Icon(b.transform, icon, Theme.Timing);
                img.rectTransform.anchorMin = new Vector2(0, 0.5f);
                img.rectTransform.anchorMax = new Vector2(0, 0.5f);
                img.rectTransform.pivot = new Vector2(0, 0.5f);
                img.rectTransform.anchoredPosition = new Vector2(14, 0);
                img.rectTransform.sizeDelta = new Vector2(28, 28);
            }
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

        /// <summary>One or two words for the main action ("Carrera", "Simular", "Cerrar temp.").</summary>
        public string ContinueShort()
        {
            if (Session.Game.World.PlayerTeam == null) return Session.Game.IsSeasonOver ? Loc.T("shell.short.close") : Loc.T("shell.short.simulate");
            if (NextOwnEvent() != null) return Loc.T("shell.short.race");
            return Session.Game.IsSeasonOver ? Loc.T("shell.short.close") : Loc.T("shell.short.finish");
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
            // The progress label takes the info line's place while it runs.
            _subHeader.enabled = busy == null;
            if (_toastBg.gameObject.activeSelf && Time.unscaledTime > _toastUntil) _toastBg.gameObject.SetActive(false);
        }
    }
}
