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
            // A touch lighter at the top so the page has depth, plus faint livery stripes in the section colour.
            UiFactory.Gradient(bg, 1.45f, 0.85f);
            bg.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < 3; i++)
            {
                var band = UiFactory.Decor("Livery", bg.transform, Color.white);
                band.rectTransform.anchorMin = band.rectTransform.anchorMax = new Vector2(0.9f + i * 0.045f, 0.15f);
                band.rectTransform.sizeDelta = new Vector2(70 - i * 18, 1400);
                band.rectTransform.localRotation = Quaternion.Euler(0, 0, -28);
                _livery.Add(band);
            }

            // Header: who and where we are, money, and the main action.
            var header = UiFactory.Panel("Header", root, Theme.Panel);
            UiFactory.Place(header.rectTransform, 0, 0.9f, 1, 1);
            UiFactory.Gradient(header, 1.4f, 0.9f);
            UiFactory.DropShadow(header, 0.5f, 6);
            // Logo: a darker plate with racing stripes behind the wordmark.
            var plate = UiFactory.Panel("LogoPlate", header.transform, Theme.Background);
            UiFactory.Place(plate.rectTransform, 0, 0, 0.14f, 1);
            plate.raycastTarget = false;
            UiFactory.Gradient(plate, 1.3f, 0.8f);
            plate.gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < 3; i++)
            {
                var stripe = UiFactory.Decor("Stripe", plate.transform, i == 1 ? new Color(1, 1, 1, 0.85f) : Theme.Accent);
                stripe.rectTransform.anchorMin = stripe.rectTransform.anchorMax = new Vector2(0.8f + i * 0.09f, 0.5f);
                stripe.rectTransform.sizeDelta = new Vector2(i == 1 ? 6 : 16, 260);
                stripe.rectTransform.localRotation = Quaternion.Euler(0, 0, -24);
            }
            var logo = UiFactory.Label("Logo", plate.transform, "<b>MOTO</b>\n<color=" + Theme.BadHex + "><b>MANAGER</b></color>", Theme.FontSmall + 2, TextAnchor.MiddleLeft, Theme.Text);
            logo.supportRichText = true;
            logo.lineSpacing = 0.85f;
            logo.resizeTextForBestFit = true;
            logo.resizeTextMinSize = Theme.FontSmall - 8;
            logo.resizeTextMaxSize = Theme.FontSmall + 2;
            UiFactory.DropShadow(logo, 0.6f, 2);
            UiFactory.Place(logo.rectTransform, 0.1f, 0.08f, 0.78f, 0.92f);
            // Section-coloured line along the bottom of the header, fading to the right.
            _headerLine = UiFactory.Decor("AccentLine", header.transform, Theme.Accent);
            _headerLine.rectTransform.anchorMin = new Vector2(0, 0);
            _headerLine.rectTransform.anchorMax = new Vector2(1, 0);
            _headerLine.rectTransform.offsetMin = new Vector2(0, 0);
            _headerLine.rectTransform.offsetMax = new Vector2(0, 4);
            UiFactory.Gradient(_headerLine, Color.white, new Color(1, 1, 1, 0.1f), true);
            _header = UiFactory.Label("Team", header.transform, "", Theme.FontNormal + 2, TextAnchor.LowerLeft);
            _header.supportRichText = true;
            UiFactory.DropShadow(_header, 0.45f, 2);
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
            // The main action glows: a light rim and a stronger lift than the other buttons.
            go.gameObject.AddComponent<Outline>().effectColor = new Color(1, 0.75f, 0.75f, 0.25f);
            UiFactory.Gradient(go.image, 1.3f, 0.8f);

            var rail = UiFactory.Panel("Rail", root, Theme.Panel);
            UiFactory.Place(rail.rectTransform, 0, 0, 0.14f, 0.9f);
            UiFactory.Gradient(rail, 1.15f, 0.8f);
            var edge = UiFactory.Decor("Edge", rail.transform, new Color(1, 1, 1, 0.05f));
            edge.rectTransform.anchorMin = new Vector2(1, 0);
            edge.rectTransform.anchorMax = new Vector2(1, 1);
            edge.rectTransform.offsetMin = new Vector2(-2, 0);
            edge.rectTransform.offsetMax = Vector2.zero;
            var layout = rail.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 16, 16);
            layout.spacing = 10;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            _tabs = UiFactory.Rect("Tabs", root);
            UiFactory.Place(_tabs, 0.15f, 0.82f, 0.99f, 0.885f);
            _content = UiFactory.Rect("Content", root);

            _toastBg = UiFactory.Panel("Toast", root, new Color(0.11f, 0.17f, 0.27f, 0.98f), true);
            UiFactory.Place(_toastBg.rectTransform, 0.3f, 0.03f, 0.9f, 0.11f);
            UiFactory.Gradient(_toastBg, 1.25f, 0.85f);
            UiFactory.DropShadow(_toastBg, 0.55f, 6);
            _toastBg.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.08f);
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
            Add("nav.career", new CareerSummaryPage(), new CareerOffersPage(), new CareerOrgPage(), new CareerFoundPage());

            foreach (var s in _sections)
            {
                var section = s;
                var b = UiFactory.Button("Nav", rail.transform, Loc.T(s.TitleKey), () => Show(section), Theme.PanelLight, Theme.FontSmall + 2);
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 86;
                _navButtons.Add(b.GetComponent<Image>());
                // Each section has its own colour: a dot always, and a bar on the left edge when it is open.
                var accent = Theme.SectionAccent(s.TitleKey);
                var label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.rectTransform.offsetMin = new Vector2(46, 0);
                label.rectTransform.offsetMax = new Vector2(-6, 0);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = Theme.FontSmall - 6;
                label.resizeTextMaxSize = Theme.FontSmall + 2;
                _navLabels.Add(label);
                var dot = UiFactory.Decor("Dot", b.transform, accent, true);
                dot.pixelsPerUnitMultiplier = 3.2f;
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0, 0.5f);
                dot.rectTransform.anchoredPosition = new Vector2(26, 0);
                dot.rectTransform.sizeDelta = new Vector2(13, 13);
                var bar = UiFactory.SideStripe(b.transform, accent, 6, 14);
                _navBars.Add(bar);
            }
            Show(_sections[0]);
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
                var own = Theme.SectionAccent(_sections[i].TitleKey);
                _navButtons[i].color = on ? Color.Lerp(Theme.PanelLight, own, 0.45f) : Color.Lerp(Theme.Panel, Theme.PanelLight, 0.55f);
                _navLabels[i].color = on ? Color.white : Color.Lerp(Theme.TextDim, Color.white, 0.25f);
                _navLabels[i].fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
                _navBars[i].gameObject.SetActive(on);
            }
            _headerLine.color = accent;
            foreach (var band in _livery) band.color = Theme.Alpha(accent, 0.045f);
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
            var next = NextOwnEvent();
            _header.text = "<b>" + (team != null ? team.Name : Loc.T("shell.noTeam")) + "</b>" +
                           (team != null ? "    " + Theme.Money(Accounts.Cash(world, team)) : "");
            _subHeader.text = world.CurrentDate.ToString("d MMM yyyy", Loc.Culture) + "  ·  " + Loc.T("shell.season", world.Season) +
                              (next != null ? "  ·  " + Loc.T("shell.next", Loc.Round(next.Round.Name)) : "");
            _continueLabel.text = "<b>" + ContinueLabel() + "</b>   ►";

            UiFactory.Clear(_tabs);
            bool tabs = _section.Pages.Count > 1;
            _tabs.gameObject.SetActive(tabs);
            UiFactory.Place(_content, 0.15f, 0.01f, 0.995f, tabs ? 0.815f : 0.89f);
            if (tabs)
            {
                // A recessed strip holding the tabs; the open tab is lifted, tinted and underlined in the section colour.
                var strip = UiFactory.Decor("TabStrip", _tabs, new Color(0, 0, 0, 0.25f), true);
                UiFactory.Place(strip.rectTransform, 0, 0, _section.Pages.Count * 0.2f, 1);
                strip.rectTransform.offsetMin = new Vector2(-2, -2);
                strip.rectTransform.offsetMax = new Vector2(2, 2);
                var accent = Theme.CurrentAccent;
                for (int i = 0; i < _section.Pages.Count; i++)
                {
                    int idx = i;
                    bool on = i == _section.Current;
                    var b = UiFactory.Button("Tab", _tabs, _section.Pages[i].Title, () =>
                    {
                        _section.Current = idx;
                        Refresh();
                    }, on ? Color.Lerp(Theme.PanelLight, accent, 0.4f) : Theme.Panel, Theme.FontSmall);
                    UiFactory.Place((RectTransform)b.transform, i * 0.2f, 0, i * 0.2f + 0.195f, 1, 3);
                    var label = b.GetComponentInChildren<Text>();
                    label.color = on ? Color.white : Color.Lerp(Theme.TextDim, Color.white, 0.2f);
                    label.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
                    if (!on) continue;
                    var underline = UiFactory.Decor("Underline", b.transform, Color.Lerp(accent, Color.white, 0.2f), true);
                    underline.pixelsPerUnitMultiplier = 8f;
                    underline.rectTransform.anchorMin = new Vector2(0, 0);
                    underline.rectTransform.anchorMax = new Vector2(1, 0);
                    underline.rectTransform.offsetMin = new Vector2(14, 3);
                    underline.rectTransform.offsetMax = new Vector2(-14, 8);
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
