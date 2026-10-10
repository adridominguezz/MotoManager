using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI.Screens
{
    /// <summary>A management screen hosted by the shell (one at a time, rebuilt when shown or refreshed).</summary>
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

        /// <summary>Builds the screen into <paramref name="content"/> (cleared beforehand).</summary>
        public abstract void Build(RectTransform content);
    }

    /// <summary>
    /// The career interface: a navigation rail on the left, a header with team, money and date, and the
    /// active screen. Long simulations run on the session's worker thread while a progress label is shown.
    /// </summary>
    public class ManagementShell : MonoBehaviour
    {
        public GameSession Session { get; private set; }
        private readonly List<ScreenBase> _screens = new List<ScreenBase>();
        private readonly List<Image> _navButtons = new List<Image>();
        private RectTransform _content;
        private Text _header;
        private Text _busy;
        private ScreenBase _current;
        private string _shownBusy;
        private Text _toast;
        private Image _toastBg;
        private float _toastUntil;

        public static ManagementShell Create(GameSession session)
        {
            var canvas = UiFactory.CreateCanvas("Management", 5);
            var shell = canvas.gameObject.AddComponent<ManagementShell>();
            shell.Session = session;
            shell.Build((RectTransform)canvas.transform);
            return shell;
        }

        public void Register(ScreenBase screen)
        {
            screen.Attach(this);
            _screens.Add(screen);
        }

        private void Build(RectTransform root)
        {
            var bg = UiFactory.Panel("Background", root, Theme.Background);
            UiFactory.Stretch(bg.rectTransform);

            var header = UiFactory.Panel("Header", root, Theme.Panel);
            UiFactory.Place(header.rectTransform, 0, 0.92f, 1, 1);
            _header = UiFactory.Label("HeaderText", header.transform, "", Theme.FontNormal, TextAnchor.MiddleLeft);
            UiFactory.Place(_header.rectTransform, 0.16f, 0, 0.8f, 1);
            _header.supportRichText = true;
            _busy = UiFactory.Label("Busy", header.transform, "", Theme.FontSmall, TextAnchor.MiddleRight, Theme.Warning);
            UiFactory.Place(_busy.rectTransform, 0.6f, 0, 0.99f, 1);
            var logo = UiFactory.Label("Logo", header.transform, "MMM", Theme.FontLarge, TextAnchor.MiddleCenter, Theme.Accent);
            UiFactory.Place(logo.rectTransform, 0, 0, 0.15f, 1);

            var rail = UiFactory.Panel("Rail", root, Theme.Panel);
            UiFactory.Place(rail.rectTransform, 0, 0, 0.15f, 0.915f);
            var layout = rail.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 12, 12);
            layout.spacing = 8;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            _content = UiFactory.Rect("Content", root);
            UiFactory.Place(_content, 0.155f, 0.005f, 0.995f, 0.915f);

            _toastBg = UiFactory.Panel("Toast", root, new Color(0.1f, 0.12f, 0.15f, 0.95f));
            UiFactory.Place(_toastBg.rectTransform, 0.3f, 0.02f, 0.9f, 0.1f);
            _toast = UiFactory.Label("ToastText", _toastBg.transform, "", Theme.FontNormal, TextAnchor.MiddleCenter);
            UiFactory.Stretch(_toast.rectTransform);
            _toastBg.gameObject.SetActive(false);

            Register(new HubScreen());
            Register(new TeamScreen());
            Register(new BikeScreen());
            Register(new FinanceScreen());
            Register(new MarketScreen());
            Register(new StandingsScreen());
            Register(new CalendarScreen());
            Register(new HistoryScreen());

            foreach (var s in _screens)
            {
                var screen = s;
                var b = UiFactory.Button("Nav " + s.Title, rail.transform, s.Title, () => Show(screen), Theme.PanelLight, Theme.FontSmall + 2);
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 78;
                _navButtons.Add(b.GetComponent<Image>());
            }
            Show(_screens[0]);
        }

        public void Show(ScreenBase screen)
        {
            _current = screen;
            for (int i = 0; i < _screens.Count; i++) _navButtons[i].color = _screens[i] == screen ? Theme.Accent : Theme.PanelLight;
            Refresh();
        }

        public void Show<T>() where T : ScreenBase
        {
            foreach (var s in _screens)
                if (s is T)
                {
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

        /// <summary>Rebuilds the header and the current screen (call after anything changes the world).</summary>
        public void Refresh()
        {
            var game = Session.Game;
            if (game == null) return;
            var world = game.World;
            var team = world.PlayerTeam;
            var next = game.PeekNextEvent();
            _header.text = "<b>" + (team != null ? team.Name : "Spectator") + "</b>   " +
                           (team != null ? MathX.FormatMoney(Accounts.Cash(world, team)) + "   " : "") +
                           "<color=#9AA4B2>" + world.CurrentDate.ToString("d MMM yyyy") + "  ·  season " + world.Season +
                           (next != null ? "  ·  next: " + next.Round.Name : "") + "</color>";
            UiFactory.Clear(_content);
            if (_current != null) _current.Build(_content);
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
