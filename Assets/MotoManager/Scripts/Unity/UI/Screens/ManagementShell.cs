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
        private float _toastUntil;

        /// <summary>Result of the last simulated or played event, shown on the home page.</summary>
        public string LastResult = "";

        public static ManagementShell Create(GameSession session)
        {
            var canvas = UiFactory.CreateCanvas("Management", 5);
            var shell = canvas.gameObject.AddComponent<ManagementShell>();
            shell.Session = session;
            shell.Build((RectTransform)canvas.transform);
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

            // Header: who and where we are, money, and the main action.
            var header = UiFactory.Panel("Header", root, Theme.Panel);
            UiFactory.Place(header.rectTransform, 0, 0.9f, 1, 1);
            var logo = UiFactory.Label("Logo", header.transform, "<b>MOTO</b>MANAGER", Theme.FontNormal, TextAnchor.MiddleCenter, Theme.Accent);
            logo.supportRichText = true;
            UiFactory.Place(logo.rectTransform, 0, 0, 0.14f, 1);
            _header = UiFactory.Label("Team", header.transform, "", Theme.FontNormal + 2, TextAnchor.LowerLeft);
            _header.supportRichText = true;
            UiFactory.Place(_header.rectTransform, 0.155f, 0.48f, 0.56f, 0.96f);
            _subHeader = UiFactory.Label("Date", header.transform, "", Theme.FontSmall - 2, TextAnchor.UpperLeft, Theme.TextDim);
            _subHeader.supportRichText = true;
            UiFactory.Place(_subHeader.rectTransform, 0.155f, 0.04f, 0.56f, 0.48f);
            _busy = UiFactory.Label("Busy", header.transform, "", Theme.FontSmall, TextAnchor.MiddleRight, Theme.Warning);
            UiFactory.Place(_busy.rectTransform, 0.3f, 0, 0.56f, 1);
            var menu = UiFactory.Button("Menu", header.transform, Loc.T("shell.menu"), ExitToMenu, Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)menu.transform, 0.57f, 0.16f, 0.665f, 0.84f, 4);
            var save = UiFactory.Button("Save", header.transform, Loc.T("shell.save"), () => { Session.Save(); Toast(Loc.T("shell.saved")); }, Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)save.transform, 0.67f, 0.16f, 0.77f, 0.84f, 4);
            var go = UiFactory.Button("Continue", header.transform, "", Continue, Theme.Accent, Theme.FontNormal);
            UiFactory.Place((RectTransform)go.transform, 0.78f, 0.12f, 0.99f, 0.88f, 4);
            _continueLabel = go.GetComponentInChildren<Text>();
            _continueLabel.supportRichText = true;

            var rail = UiFactory.Panel("Rail", root, Theme.Panel);
            UiFactory.Place(rail.rectTransform, 0, 0, 0.14f, 0.9f);
            var layout = rail.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 16, 16);
            layout.spacing = 10;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            _tabs = UiFactory.Rect("Tabs", root);
            UiFactory.Place(_tabs, 0.15f, 0.82f, 0.99f, 0.885f);
            _content = UiFactory.Rect("Content", root);

            _toastBg = UiFactory.Panel("Toast", root, new Color(0.09f, 0.14f, 0.22f, 0.97f), true);
            UiFactory.Place(_toastBg.rectTransform, 0.3f, 0.03f, 0.9f, 0.11f);
            _toast = UiFactory.Label("ToastText", _toastBg.transform, "", Theme.FontNormal - 2, TextAnchor.MiddleCenter);
            _toast.supportRichText = true;
            UiFactory.Stretch(_toast.rectTransform);
            _toastBg.gameObject.SetActive(false);

            Add("nav.home", new HubScreen());
            Add("nav.team", new RidersPage(), new StaffPage(), new ManagerPage());
            Add("nav.bike", new BikeScreen(), new ProjectsPage(), new FacilitiesPage());
            Add("nav.finance", new FinanceScreen(), new SponsorsPage(), new ShopScreen());
            Add("nav.market", new MarketScreen());
            Add("nav.competition", new StandingsScreen(), new CalendarScreen(), new HistoryScreen());

            foreach (var s in _sections)
            {
                var section = s;
                var b = UiFactory.Button("Nav", rail.transform, Loc.T(s.TitleKey), () => Show(section), Theme.PanelLight, Theme.FontSmall + 2);
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 86;
                _navButtons.Add(b.GetComponent<Image>());
            }
            Show(_sections[0]);
            Assistant.Show("home", new[] { "tut.home.1", "tut.home.2", "tut.home.3", "tut.home.4", "tut.home.5" });
        }

        public void Show(Section section)
        {
            _section = section;
            for (int i = 0; i < _sections.Count; i++) _navButtons[i].color = _sections[i] == section ? Theme.Accent : Theme.PanelLight;
            Refresh();
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
            var next = NextOwnEvent();
            _header.text = "<b>" + (team != null ? team.Name : Loc.T("shell.spectator")) + "</b>" +
                           (team != null ? "    " + Theme.Money(Accounts.Cash(world, team)) : "");
            _subHeader.text = world.CurrentDate.ToString("d MMM yyyy", Loc.Culture) + "  ·  " + Loc.T("shell.season", world.Season) +
                              (next != null ? "  ·  " + Loc.T("shell.next", Loc.Round(next.Round.Name)) : "");
            _continueLabel.text = "<b>" + ContinueLabel() + "</b>";

            UiFactory.Clear(_tabs);
            bool tabs = _section.Pages.Count > 1;
            _tabs.gameObject.SetActive(tabs);
            UiFactory.Place(_content, 0.15f, 0.01f, 0.995f, tabs ? 0.815f : 0.89f);
            if (tabs)
            {
                for (int i = 0; i < _section.Pages.Count; i++)
                {
                    int idx = i;
                    var b = UiFactory.Button("Tab", _tabs, _section.Pages[i].Title, () =>
                    {
                        _section.Current = idx;
                        Refresh();
                    }, i == _section.Current ? Theme.Accent : Theme.PanelLight, Theme.FontSmall);
                    UiFactory.Place((RectTransform)b.transform, i * 0.2f, 0, i * 0.2f + 0.195f, 1, 3);
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
            Session.Run(Loc.T("busy.event"), () =>
            {
                if (team != null) game.SimulateUntilEventFor(team.CategoryId);
                var ev = game.BeginNextEvent();
                if (ev == null) return;
                if (ev.PlayerWeekend != null) ev.PlayerWeekend.HeadlessTimeStep = true;
                game.CompleteEvent(ev);
                summary = Loc.T("home.simulated", Loc.Round(ev.Plan.Round.Name));
            }, () =>
            {
                LastResult = summary;
                Session.Save();
                Refresh();
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
            Session.Run(Loc.T("busy.closing"), () =>
            {
                var s = game.EndSeason();
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
