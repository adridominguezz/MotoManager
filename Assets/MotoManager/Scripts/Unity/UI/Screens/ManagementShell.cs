using System.Collections.Generic;
using System.Text;
using MotoManager.Core.Common;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
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

        /// <summary>
        /// A page left out of the section's tab strip (wizard steps such as team setup). It is still reached with
        /// <see cref="ManagementShell.Show{T}"/> from a button.
        /// </summary>
        public virtual bool HiddenTab { get { return false; } }

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
    /// The career interface (UX spec §3.1): a fixed 128-unit icon rail on the left (the menu on top, then one
    /// pictogram per section), a 72-unit top bar (MM4 §1.2: slanted back block off the hub, page title and date, cash /
    /// reputation / championship blocks, inbox and news buttons, the one main action as a slanted block), up to 4 tabs (56) for the pages of a section, and the active page
    /// in the content rect (1440×570 with tabs, 1440×634 without on a 1600×730 safe area). Sheets open over the safe
    /// root (<see cref="Ui.SheetHost"/>). Long simulations run on the session's worker thread while a progress label
    /// replaces the date.
    /// </summary>
    public class ManagementShell : MonoBehaviour
    {
        public GameSession Session { get; private set; }
        private readonly List<Section> _sections = new List<Section>();
        private readonly List<Image> _navButtons = new List<Image>();
        private RectTransform _root;
        private RectTransform _content;
        private RectTransform _tabs;
        private RectTransform _right;
        private RectTransform _info;
        private Button _back;
        /// <summary>Holder of the header's one contextual action (MM4 §1.2), rebuilt on every refresh.</summary>
        private RectTransform _action;
        private Text _header;
        private Text _subHeader;
        private Text _busy;
        private Section _section;
        private string _shownBusy;
        private readonly List<Button> _disabledForBusy = new List<Button>();
        private Text _toast;
        private Image _toastBg;
        private Image _toastStripe;
        private float _toastUntil;
        private readonly List<Text> _navLabels = new List<Text>();
        private readonly List<Image> _navBars = new List<Image>();
        private readonly List<Image> _navIcons = new List<Image>();
        private readonly List<Image> _livery = new List<Image>();
        private Image _headerLine;
        /// <summary>Where the back button returns to: pages opened from another page (hub tiles, shortcuts).</summary>
        private readonly List<KeyValuePair<Section, int>> _history = new List<KeyValuePair<Section, int>>();

        /// <summary>Height of the top bar, in canvas units.</summary>
        public const float TopBarHeight = 72;
        /// <summary>Width of the icon rail, in canvas units.</summary>
        public const float RailUnits = 128;
        private const float TabHeight = 56, TabGap = 8, Gutter = Ui.Grid.Gutter;
        private const float ActionWidth = 232, BackWidth = 96, PillWidth = 176, RepWidth = 120, WordWidth = 128;
        /// <summary>Lean of the header's slanted blocks (back, main action): slant-sprite pixels-per-unit multiplier.</summary>
        private const float BlockSlant = 0.7f;
        private const int MaxTabs = 4, MaxNews = 12;

        /// <summary>Result of the last simulated or played event, shown on the home page.</summary>
        public string LastResult = "";

        /// <summary>
        /// The decisions waiting (text, where to resolve it), set by the hub page. The top-bar inbox shows their count
        /// and lists them in a sheet; a row closes the sheet and runs its action. Null or empty: nothing waiting.
        /// </summary>
        public System.Func<List<KeyValuePair<string, System.Action>>> InboxSource;

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
            _root = root;
            Ui.SheetHost = root;
            var bg = UiFactory.Panel("Background", root, Theme.Background);
            UiFactory.Stretch(bg.rectTransform);
            // Near-black floor: a very faint weave, a soft dark-red glow at the top (the hero glow of the
            // championship's pages) and two thin diagonal livery lines in the team colour at the far right.
            bg.gameObject.AddComponent<RectMask2D>();
            var weave = UiFactory.Pattern("Carbon", bg.transform, UiFactory.CarbonSprite, new Color(1, 1, 1, 0.018f), 10);
            UiFactory.Stretch(weave.rectTransform);
            UiFactory.Gradient(weave, Color.white, new Color(1, 1, 1, 0.3f));
            UiFactory.Glow(bg.transform, 0.32f, 1f, 1500, 0.16f);
            for (int i = 0; i < 2; i++)
            {
                var band = UiFactory.Decor("Livery", bg.transform, Color.white);
                band.rectTransform.anchorMin = band.rectTransform.anchorMax = new Vector2(0.93f + i * 0.03f, 0.15f);
                band.rectTransform.sizeDelta = new Vector2(i == 0 ? 26 : 8, 1400);
                band.rectTransform.localRotation = Quaternion.Euler(0, 0, -28);
                _livery.Add(band);
            }

            BuildTopBar(root);
            var nav = BuildRail(root);

            _tabs = UiFactory.Rect("Tabs", root);
            _tabs.anchorMin = new Vector2(0, 1);
            _tabs.anchorMax = new Vector2(1, 1);
            _tabs.pivot = new Vector2(0.5f, 1);
            _tabs.offsetMin = new Vector2(RailUnits + Gutter, -TopBarHeight - TabGap - TabHeight);
            _tabs.offsetMax = new Vector2(-Gutter, -TopBarHeight - TabGap);
            _content = UiFactory.Rect("Content", root);

            // Toast: bottom centre of the content, 640 × 64.
            _toastBg = UiFactory.Panel("Toast", root, Theme.Alpha(Theme.Panel, 0.98f));
            UiFactory.Surface(_toastBg);
            var trt = _toastBg.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0);
            trt.pivot = new Vector2(0.5f, 0);
            trt.sizeDelta = new Vector2(640, 64);
            trt.anchoredPosition = new Vector2(RailUnits / 2, 24);
            UiFactory.DropShadow(_toastBg, 0.5f, 4);
            _toastBg.gameObject.AddComponent<Outline>().effectColor = Theme.Edge;
            _toastStripe = UiFactory.SideStripe(_toastBg.transform, Theme.Good, 4, 12);
            _toast = Ui.Label(_toastBg.transform, "", Theme.TextBody, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            UiFactory.Stretch(_toast.rectTransform);
            _toast.rectTransform.offsetMin = new Vector2(20, 4);
            _toast.rectTransform.offsetMax = new Vector2(-16, -4);
            _toastBg.gameObject.SetActive(false);

            Add("nav.home", new HubScreen());
            Add("nav.team", new RidersPage(), new StaffPage(), new ManagerPage());
            Add("nav.bike", new BikeScreen(), new ProjectsPage(), new FacilitiesPage());
            Add("nav.finance", new FinanceScreen(), new SponsorsPage(), new ShopScreen());
            Add("nav.market", new MarketScreen());
            Add("nav.competition", new StandingsScreen(), new CalendarScreen(), new HistoryScreen());
            Add("nav.career", new CareerSummaryPage(), new CareerOffersPage(), new CareerOrgPage(), new TeamSetupPage(), new CareerFoundPage(), new LiveryEditorPage());

            foreach (var s in _sections) BuildNavCell(nav, s);
            Show(_sections[0]);
            if (TeamSetupPage.OpenOnStart)
            {
                TeamSetupPage.OpenOnStart = false;
                Show<TeamSetupPage>();
            }
            Assistant.Show("home", new[] { "tut.home.1", "tut.home.2", "tut.home.3", "tut.home.4", "tut.home.5" });
        }

        // ------------------------------------------------------------------ top bar

        private void BuildTopBar(RectTransform root)
        {
            var header = UiFactory.Panel("TopBar", root, Theme.Chrome);
            var hrt = header.rectTransform;
            hrt.anchorMin = new Vector2(0, 1);
            hrt.anchorMax = new Vector2(1, 1);
            hrt.pivot = new Vector2(0.5f, 1);
            hrt.offsetMin = new Vector2(RailUnits, -TopBarHeight);
            hrt.offsetMax = Vector2.zero;
            _headerLine = UiFactory.Decor("AccentLine", header.transform, Theme.Accent);
            _headerLine.rectTransform.anchorMin = new Vector2(0, 0);
            _headerLine.rectTransform.anchorMax = new Vector2(1, 0);
            _headerLine.rectTransform.offsetMin = new Vector2(0, 0);
            _headerLine.rectTransform.offsetMax = new Vector2(0, 2);
            UiFactory.Gradient(_headerLine, Color.white, new Color(1, 1, 1, 0.05f), true);

            // Back (every page but the hub, MM4 §1.2): a dark slanted block the full height of the bar at its left
            // end, with the Back pictogram (a left chevron).
            _back = UiFactory.Button("Back", header.transform, "", Back, Theme.PanelLight, Theme.TextBody);
            UiFactory.Slant(_back.image, BlockSlant);
            var brt = (RectTransform)_back.transform;
            brt.anchorMin = new Vector2(0, 0);
            brt.anchorMax = new Vector2(0, 1);
            brt.pivot = new Vector2(0, 0.5f);
            brt.offsetMin = new Vector2(-12, 0);
            brt.offsetMax = new Vector2(BackWidth - 12, 0);
            var backIcon = UiIcons.Icon(_back.transform, UiIcons.Back, Theme.Text);
            backIcon.rectTransform.anchorMin = backIcon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            backIcon.rectTransform.anchoredPosition = new Vector2(4, 0);
            backIcon.rectTransform.sizeDelta = new Vector2(32, 32);

            // Title (team on the hub, page on sub-pages) over the date.
            _info = UiFactory.Rect("Info", header.transform);
            UiFactory.Stretch(_info);
            _header = Ui.Label(_info, "", Theme.TextTitle, TextAnchor.LowerLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Place(_header.rectTransform, 0, 0.42f, 1, 0.98f);
            _subHeader = Ui.Label(_info, "", Theme.TextCaption, TextAnchor.UpperLeft, Theme.TextDim, FontStyle.Normal);
            UiFactory.Place(_subHeader.rectTransform, 0, 0.04f, 1, 0.42f);
            _busy = Ui.Label(_info, "", Theme.TextCaption, TextAnchor.UpperLeft, Theme.Warning, FontStyle.Bold);
            UiFactory.Place(_busy.rectTransform, 0, 0.04f, 1, 0.42f);

            // Far right: the one main action, a slanted block the full height of the bar (MM4 §1.2).
            _action = UiFactory.Rect("Action", header.transform);
            _action.anchorMin = new Vector2(1, 0);
            _action.anchorMax = new Vector2(1, 1);
            _action.pivot = new Vector2(1, 0.5f);
            _action.offsetMin = new Vector2(-ActionWidth, 0);
            _action.offsetMax = Vector2.zero;

            // Before it: resource blocks, inbox and news, laid out right to left (rebuilt on refresh).
            _right = UiFactory.Rect("Right", header.transform);
            _right.anchorMin = new Vector2(1, 0);
            _right.anchorMax = new Vector2(1, 1);
            _right.pivot = new Vector2(1, 0.5f);
            _right.offsetMin = new Vector2(0, 0);
            _right.offsetMax = new Vector2(-ActionWidth - 16, 0);
            var h = _right.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childAlignment = TextAnchor.MiddleRight;
            h.childControlWidth = true;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            _right.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private RectTransform BuildRail(RectTransform root)
        {
            var rail = UiFactory.Panel("Rail", root, Theme.Chrome);
            var rrt = rail.rectTransform;
            rrt.anchorMin = new Vector2(0, 0);
            rrt.anchorMax = new Vector2(0, 1);
            rrt.pivot = new Vector2(0, 0.5f);
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = new Vector2(RailUnits, 0);
            var edge = UiFactory.Decor("Edge", rail.transform, Theme.Divider);
            edge.rectTransform.anchorMin = new Vector2(1, 0);
            edge.rectTransform.anchorMax = new Vector2(1, 1);
            edge.rectTransform.offsetMin = new Vector2(-2, 0);
            edge.rectTransform.offsetMax = Vector2.zero;
            // The menu: the top cell, level with the top bar.
            var more = UiFactory.Button("MenuButton", rail.transform, "", OpenMenu, Theme.Alpha(Color.white, 0.03f), Theme.FontLarge);
            more.image.sprite = null;
            var mrt = (RectTransform)more.transform;
            mrt.anchorMin = new Vector2(0, 1);
            mrt.anchorMax = new Vector2(1, 1);
            mrt.pivot = new Vector2(0.5f, 1);
            mrt.offsetMin = new Vector2(0, -TopBarHeight);
            mrt.offsetMax = Vector2.zero;
            var menuIcon = UiIcons.Icon(more.transform, UiIcons.Menu, Theme.Text);
            menuIcon.rectTransform.anchorMin = menuIcon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            menuIcon.rectTransform.sizeDelta = new Vector2(44, 44);
            var nav = UiFactory.Rect("Nav", rail.transform);
            UiFactory.Stretch(nav);
            nav.offsetMax = new Vector2(0, -TopBarHeight - 8);
            nav.offsetMin = new Vector2(0, 4);
            var layout = nav.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 2, 0, 0);
            layout.spacing = 2;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            return nav;
        }

        /// <summary>
        /// A rail cell: the open section is a lighter block with a 6-unit accent bar, a white 48-unit pictogram and an
        /// 18-unit CAPS caption; the others are flat and grey.
        /// </summary>
        private void BuildNavCell(RectTransform nav, Section s)
        {
            var section = s;
            var b = UiFactory.Button("Nav", nav, "", () =>
            {
                _history.Clear();
                Show(section);
            }, Color.clear, Theme.TextMin);
            b.image.sprite = null;
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.flexibleHeight = 1;
            le.minHeight = 80;
            _navButtons.Add(b.image);
            var icon = UiIcons.Icon(b.transform, IconOf(s.TitleKey), Theme.TextDim);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 1);
            icon.rectTransform.pivot = new Vector2(0.5f, 1);
            icon.rectTransform.anchoredPosition = new Vector2(0, -6);
            icon.rectTransform.sizeDelta = new Vector2(48, 48);
            _navIcons.Add(icon);
            var label = b.GetComponentInChildren<Text>();
            label.text = Theme.Caps(Loc.Or("ux.rail." + s.TitleKey.Substring(4), Loc.T(s.TitleKey)));
            label.alignment = TextAnchor.LowerCenter;
            label.resizeTextForBestFit = false;
            label.fontSize = Theme.TextMin;
            label.lineSpacing = 0.85f;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0, 0);
            lrt.anchorMax = new Vector2(1, 0);
            lrt.pivot = new Vector2(0.5f, 0);
            lrt.offsetMin = new Vector2(2, 6);
            lrt.offsetMax = new Vector2(-2, 46);
            _navLabels.Add(label);
            _navBars.Add(UiFactory.SideStripe(b.transform, Theme.Accent, 6, 0));
        }

        public void Show(Section section)
        {
            Ui.CloseSheet();
            Ui.Disarm();
            _section = section;
            var accent = Theme.SectionAccent(section.TitleKey);
            Theme.SetAccent(accent);
            for (int i = 0; i < _sections.Count; i++)
            {
                bool on = _sections[i] == section;
                _navButtons[i].color = on ? Theme.PanelLight : Color.clear;
                _navLabels[i].color = on ? Theme.Text : Color.Lerp(Theme.TextDim, Theme.Text, 0.1f);
                UiFactory.Style(_navLabels[i], FontStyle.Bold);
                _navIcons[i].color = on ? Theme.Text : Theme.Alpha(Theme.TextDim, 0.85f);
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

        /// <summary>Former width of the rail as a fraction of the screen (the rail is now a fixed 128 units).</summary>
        [System.Obsolete("The rail is a fixed RailUnits (128) wide")]
        public static float RailWidth { get { return 0.08f; } }

        // ------------------------------------------------------------------ menu, inbox, news

        /// <summary>The menu sheet: save, save slots, music and sound, text size and the way back to the start screen.</summary>
        public void OpenMenu()
        {
            Ui.Sheet(Loc.T("shell.menu"), list =>
            {
                Ui.PrimaryCTA(list, Loc.T("shell.save"), () =>
                {
                    Ui.CloseSheet();
                    Session.Save();
                    Toast(Loc.T("shell.saved"));
                });
                // Manual save slots (the autosave runs after every event); a dot marks a slot in use.
                var slotRow = UiFactory.Row(list, Ui.Grid.Button);
                var spans = new int[SaveManager.Slots.Length];
                for (int i = 0; i < spans.Length; i++) spans[i] = Mathf.Max(1, 12 / Mathf.Max(1, spans.Length));
                var cols = Ui.Columns12(slotRow, spans);
                for (int i = 0; i < SaveManager.Slots.Length; i++)
                {
                    string slot = SaveManager.Slots[i];
                    string n = slot.Substring(slot.Length - 1);
                    bool used = SaveManager.ReadMeta(slot) != null;
                    var b = Ui.Secondary(cols[i], Loc.T("save.saveTo", n), () =>
                    {
                        Ui.CloseSheet();
                        Session.Save(slot, () => Toast(Loc.T("save.saved", n)));
                    });
                    if (!used) continue;
                    var dot = UiFactory.Decor("Used", b.transform, Theme.Good);
                    UiFactory.Round(dot, 1.6f);
                    dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0, 0.5f);
                    dot.rectTransform.sizeDelta = new Vector2(12, 12);
                    dot.rectTransform.anchoredPosition = new Vector2(18, 0);
                }
                // Music and sound: on = accent outline, off = plain.
                var audioRow = UiFactory.Row(list, Ui.Grid.Button);
                var audio = Ui.Columns12(audioRow, 6, 6);
                AudioToggle(audio[0], Loc.T("audio.music"), GameAudio.MusicOn, () => GameAudio.MusicOn = !GameAudio.MusicOn);
                AudioToggle(audio[1], Loc.T("audio.sound"), GameAudio.SfxOn, () => GameAudio.SfxOn = !GameAudio.SfxOn);
                Ui.Section(list, Loc.T("ux.menu.textSize"));
                Ui.Segmented(list, new[] { "A-", "A", "A+" }, UiScale.UserLevel, SetTextSize);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UiFactory.ButtonRow(list, 56, DebugManagement.Actions(Session, () => { Ui.CloseSheet(); Refresh(); }));
#endif
                var exitRow = UiFactory.Row(list, Ui.Grid.Button);
                Ui.Danger(exitRow, Loc.T("ux.menu.exit"), Loc.T("ux.menu.exitConfirm"), "shell.exit", () =>
                {
                    Ui.CloseSheet();
                    ExitToMenu();
                }, null);
            }, null, null, 0.5f);
        }

        private void AudioToggle(RectTransform area, string label, bool on, System.Action toggle)
        {
            string text = label + ": " + Loc.T(on ? "ux.common.on" : "ux.common.off");
            System.Action tap = () =>
            {
                toggle();
                OpenMenu();
            };
            if (on) Ui.SecondaryAccent(area, text, tap);
            else Ui.Secondary(area, text, tap);
        }

        private void SetTextSize(int level)
        {
            UiScale.UserLevel = level;
            Refresh();
            OpenMenu();
        }

        /// <summary>The decisions waiting, from <see cref="InboxSource"/> (never null).</summary>
        public List<KeyValuePair<string, System.Action>> InboxItems()
        {
            List<KeyValuePair<string, System.Action>> items = null;
            if (InboxSource != null) items = InboxSource();
            return items ?? new List<KeyValuePair<string, System.Action>>();
        }

        /// <summary>The inbox sheet: one tappable row per decision waiting (the row resolves it).</summary>
        public void ShowInbox()
        {
            var items = InboxItems();
            Ui.Sheet(Loc.T("ux.inbox.title"), list =>
            {
                if (items.Count == 0)
                {
                    Ui.Empty(list, Glyph.Check, Loc.T("hub.allClear"));
                    return;
                }
                for (int i = 0; i < items.Count; i++)
                {
                    var go = items[i].Value;
                    Ui.ListRow(list, new[] { 1f }, new[] { items[i].Key }, i, false, go != null ? () =>
                    {
                        Ui.CloseSheet();
                        go();
                    } : (System.Action)null);
                }
            });
        }

        /// <summary>The news of the team's own world (Grand Prix or Superbike) and general news, newest first.</summary>
        public IEnumerable<NewsItem> OwnNews()
        {
            var game = Session.Game;
            if (game == null) yield break;
            var team = game.World.PlayerTeam;
            var mine = team != null ? game.Db.GetCategory(team.CategoryId) : null;
            foreach (var n in game.World.News)
            {
                if (mine != null && n.CategoryId != null)
                {
                    var cat = game.Db.GetCategory(n.CategoryId);
                    if (cat != null && cat.Pyramid != mine.Pyramid) continue;
                }
                yield return n;
            }
        }

        /// <summary>The news sheet: "29 MAR  headline" rows (important ones bold with an accent edge).</summary>
        public void ShowNews()
        {
            Ui.Sheet(Loc.T("home.news"), list =>
            {
                int shown = 0;
                foreach (var n in OwnNews())
                {
                    if (shown >= MaxNews) break;
                    var p = UiFactory.Paragraph(list, Theme.Dim(Theme.Caps(n.Date.ToString("dd MMM", Loc.Culture))) + "   " +
                        (n.Important ? "<b>" + n.Headline + "</b>" : n.Headline), Theme.TextBody);
                    var row = p.transform.parent;
                    var layout = row.GetComponent<VerticalLayoutGroup>();
                    if (layout != null) layout.padding = new RectOffset(16, 8, 8, 8);
                    if (n.Important) UiFactory.SideStripe(row, Theme.Accent, 4, 4);
                    shown++;
                }
                if (shown == 0) Ui.Empty(list, null, Loc.T("home.noNews"));
            });
        }

        // ------------------------------------------------------------------ pages

        /// <summary>
        /// Opens the section and tab holding a page of this type (hidden tabs included). The page it was opened from
        /// is remembered, so the back button returns there.
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

        /// <summary>Short confirmation at the bottom of the screen (result of an action; not for explanations).</summary>
        public void Toast(string message, bool good = true)
        {
            _toast.text = message;
            _toast.color = good ? Theme.Text : Theme.Warning;
            _toastStripe.color = good ? Theme.Good : Theme.Warning;
            _toastBg.gameObject.SetActive(true);
            _toastBg.transform.SetAsLastSibling();
            _toastUntil = Time.unscaledTime + 4f;
        }

        private ScreenBase CurrentPage
        {
            get { return _section.Pages[Mathf.Clamp(_section.Current, 0, _section.Pages.Count - 1)]; }
        }

        /// <summary>Rebuilds the tabs, the current page and the top bar (call after anything changes the world).</summary>
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

            // Tabs: the visible pages (at most 4 slots wide), equal width.
            UiFactory.Clear(_tabs);
            var visible = new List<int>();
            for (int i = 0; i < _section.Pages.Count; i++)
                if (!_section.Pages[i].HiddenTab) visible.Add(i);
            bool tabs = visible.Count > 1;
            _tabs.gameObject.SetActive(tabs);
            if (tabs) BuildTabs(visible);
            _content.anchorMin = Vector2.zero;
            _content.anchorMax = Vector2.one;
            _content.offsetMin = new Vector2(RailUnits + Gutter, Gutter);
            _content.offsetMax = new Vector2(-Gutter, -TopBarHeight - TabGap - (tabs ? TabHeight + TabGap : 0));

            UiFactory.Clear(_content);
            CurrentPage.Build(_content);
            // After the page: the hub sets the inbox source while building.
            BuildTopBarContent(world, team);
        }

        private void BuildTabs(List<int> visible)
        {
            int slots = Mathf.Max(MaxTabs, visible.Count);
            for (int k = 0; k < visible.Count; k++)
            {
                int idx = visible[k];
                bool on = idx == _section.Current;
                var b = UiFactory.Button("Tab", _tabs, Theme.Caps(_section.Pages[idx].Title), () =>
                {
                    Ui.CloseSheet();
                    Ui.Disarm();
                    _section.Current = idx;
                    Refresh();
                }, Color.clear, 22);
                UiFactory.Place((RectTransform)b.transform, (float)k / slots, 0, (float)(k + 1) / slots, 1);
                var brt = (RectTransform)b.transform;
                brt.offsetMin = new Vector2(k == 0 ? 0 : 4, 0);
                brt.offsetMax = new Vector2(-4, 0);
                // Section tabs as plain text: selected light bold over a short thick underline, the others dim.
                Ui.TextTab(b, on, 22);
            }
        }

        /// <summary>
        /// Top bar (MM4 §1.2, see docs/MM4_REFERENCIA.md "Implementación"): the slanted back block on every page but
        /// the hub, the title (team on the hub, page elsewhere) over the date, the resource blocks (cash, manager
        /// reputation, championship), AVISOS and NOTICIAS, and the one main action as a slanted block at the right
        /// end: always the race action (<see cref="ContinueShort"/> / <see cref="Continue"/>), race red when it is the
        /// screen's only primary, a dark block when the page shows its own primary CTA.
        /// </summary>
        private void BuildTopBarContent(WorldState world, Team team)
        {
            bool hub = _section == _sections[0];
            _back.gameObject.SetActive(!hub);
            _header.text = Theme.Caps(hub ? (team != null ? team.Name : Loc.T("shell.noTeam")) : CurrentPage.Title);
            _subHeader.text = Theme.Caps(world.CurrentDate.ToString("d MMM yyyy", Loc.Culture));

            UiFactory.Clear(_right);
            BuildPills(world, team);
            var inbox = InboxItems();
            if (team != null || inbox.Count > 0)
            {
                var ib = TopIcon(Loc.T("ux.shell.inboxShort"), ShowInbox);
                ib.gameObject.name = "Inbox";
                if (inbox.Count > 0)
                {
                    var badge = Ui.Badge(ib.transform, inbox.Count, Theme.Bad);
                    badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1, 1);
                    badge.rectTransform.pivot = new Vector2(1, 1);
                    badge.rectTransform.anchoredPosition = new Vector2(4, 2);
                    badge.rectTransform.sizeDelta = new Vector2(34, 26);
                }
            }
            var news = TopIcon(Loc.T("ux.shell.newsShort"), ShowNews);
            news.gameObject.name = "News";
            BuildAction(!PageHasPrimary());
            LayoutRebuilder.ForceRebuildLayoutImmediate(_right);
            float rightWidth = LayoutUtility.GetPreferredWidth(_right) + ActionWidth + 16;
            _info.offsetMin = new Vector2(hub ? Gutter : BackWidth - 12 + Gutter, 0);
            _info.offsetMax = new Vector2(-rightWidth - 16, 0);
        }

        /// <summary>
        /// The header's main action: a slanted block the full height of the bar with the race action's short label
        /// and a chevron. <paramref name="primary"/>: race-red fill (the screen's one primary); otherwise a dark block
        /// with team-colour text, so a page's own primary CTA stays the only red button on screen.
        /// </summary>
        private void BuildAction(bool primary)
        {
            UiFactory.Clear(_action);
            var fill = primary ? Theme.Action : Theme.PanelLight;
            var ink = primary ? Theme.OnAction : Theme.AccentInk;
            var b = UiFactory.Button("MainAction", _action, Theme.Caps(ContinueShort()), Continue, fill, 28);
            UiFactory.Slant(b.image, BlockSlant);
            UiFactory.Stretch((RectTransform)b.transform);
            var t = b.GetComponentInChildren<Text>();
            t.color = ink;
            Ui.Fit(t, 28);
            t.rectTransform.offsetMin = new Vector2(36, 2);
            t.rectTransform.offsetMax = new Vector2(-44, -2);
            var ch = UiIcons.Chevron(b.transform, ink);
            ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(1, 0.5f);
            ch.rectTransform.sizeDelta = new Vector2(22, 22);
            ch.rectTransform.anchoredPosition = new Vector2(-30, 0);
        }

        /// <summary>
        /// Whether the page just built shows its own red primary (a <see cref="Ui.PrimaryCTA"/> or a race-red big
        /// button): then the header action steps down to the dark block (one primary per screen, MM4 §2).
        /// </summary>
        private bool PageHasPrimary()
        {
            var red = Theme.Action;
            foreach (var b in _content.GetComponentsInChildren<Button>())
            {
                var img = b.image;
                if (img == null) continue;
                var c = img.color;
                if (Mathf.Abs(c.r - red.r) < 0.02f && Mathf.Abs(c.g - red.g) < 0.02f && Mathf.Abs(c.b - red.b) < 0.02f && c.a > 0.9f)
                    return true;
            }
            return false;
        }

        /// <summary>A 56-unit top-bar button: a known pictogram, or a short word.</summary>
        private Button TopIcon(string icon, System.Action onTap)
        {
            Button b;
            if (UiIcons.Known(icon))
            {
                b = Ui.IconButton(_right, icon, onTap);
            }
            else
            {
                var holder = UiFactory.Rect("Word", _right);
                holder.sizeDelta = new Vector2(WordWidth, Ui.Grid.Hit);
                // A plain text button (no box), like the reference app's top bar.
                b = UiFactory.Button("Word", holder, Theme.Caps(icon), onTap, Color.clear, 22);
                UiFactory.Stretch((RectTransform)b.transform);
                Ui.TextTab(b, false, 22);
                b.GetComponentInChildren<Text>().color = Theme.Text;
                var hle = holder.gameObject.AddComponent<LayoutElement>();
                hle.preferredWidth = WordWidth;
                return b;
            }
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = Ui.Grid.Hit;
            return b;
        }

        /// <summary>
        /// Resource blocks: cash (→ finance; red when negative, MM4 §2, via Theme.Money), the manager's reputation (→ manager page) and
        /// the team's championship position and points (→ standings). Without a team only the reputation shows.
        /// </summary>
        private void BuildPills(WorldState world, Team team)
        {
            var me = world.PlayerManager;
            if (team == null)
            {
                if (me != null) Pill(Loc.T("ux.mm4.rep"), me.Reputation.ToString("0"), () => Show<ManagerPage>(), RepWidth);
                return;
            }
            ChampionshipState champ;
            int pos = 0;
            double pts = 0;
            if (world.Championships.TryGetValue(team.CategoryId, out champ))
            {
                var table = champ.SortedTeams();
                for (int i = 0; i < table.Count; i++)
                    if (table[i].Id == team.Id) { pos = i + 1; pts = table[i].Points; }
            }
            Pill(Loc.T("home.tile.cash"), Theme.Money(Accounts.Cash(world, team)), () => Show<FinanceScreen>());
            if (me != null) Pill(Loc.T("ux.mm4.rep"), me.Reputation.ToString("0"), () => Show<ManagerPage>(), RepWidth);
            Pill(Loc.T("home.tile.championship"), Theme.Pos(pos) + " · " + Loc.T("home.tile.points", pts.ToString("0")), () => Show<StandingsScreen>());
        }

        private void Pill(string caption, string value, System.Action onTap, float width = PillWidth)
        {
            // A flat text block (caption over value) with a thin divider on its left, no box.
            var b = UiFactory.Button("Pill", _right, "", onTap, Color.clear, Theme.TextBody);
            ((RectTransform)b.transform).sizeDelta = new Vector2(width, Ui.Grid.Hit);
            b.image.sprite = null;
            var divider = UiFactory.Decor("Divider", b.transform, Theme.Alpha(Color.white, 0.16f));
            divider.rectTransform.anchorMin = new Vector2(0, 0.18f);
            divider.rectTransform.anchorMax = new Vector2(0, 0.82f);
            divider.rectTransform.offsetMin = Vector2.zero;
            divider.rectTransform.offsetMax = new Vector2(1, 0);
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minWidth = width * 0.7f;
            var label = b.GetComponentInChildren<Text>();
            label.supportRichText = true;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = "<size=" + Theme.TextMin + ">" + Theme.Dim(caption) + "</size>\n<b>" + value + "</b>";
            label.lineSpacing = 0.85f;
            Ui.Fit(label, Theme.TextBody);
            label.rectTransform.offsetMin = new Vector2(14, 0);
            label.rectTransform.offsetMax = new Vector2(-14, 0);
            b.targetGraphic = label;
            var colors = b.colors;
            colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            b.colors = colors;
        }

        /// <summary>Saves and goes back to the start screen (continue, new career, language).</summary>
        public void ExitToMenu()
        {
            Ui.CloseSheet();
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
                if (busy != null)
                {
                    // Lock what is tappable now, and only that: buttons disabled on purpose stay disabled afterwards.
                    foreach (var b in GetComponentsInChildren<Button>())
                    {
                        if (!b.interactable) continue;
                        b.interactable = false;
                        _disabledForBusy.Add(b);
                    }
                }
                else
                {
                    foreach (var b in _disabledForBusy)
                        if (b != null) b.interactable = true;
                    _disabledForBusy.Clear();
                }
            }
            _busy.text = busy != null ? busy + new string('.', 1 + (int)(Time.unscaledTime * 2) % 3) : "";
            // The progress label takes the date's place while it runs.
            _subHeader.enabled = busy == null;
            if (_toastBg.gameObject.activeSelf && Time.unscaledTime > _toastUntil) _toastBg.gameObject.SetActive(false);
        }
    }
}
