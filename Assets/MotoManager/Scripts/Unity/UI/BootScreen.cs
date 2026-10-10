using System.Collections.Generic;
using System.Text;
using MotoManager.Core.Common;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// First screen: loads the data, starts/continues a career and lets the whole season be simulated headless.
    /// It doubles as an on-device smoke test of the simulation core until the full management UI exists.
    /// </summary>
    public class BootScreen : MonoBehaviour
    {
        private GameSession _session;
        private Text _status;
        private RectTransform _buttons;
        private RectTransform _list;
        private Text _title;
        private string _lastSummary = "";
        private string _shownBusy;

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

            var left = UiFactory.Rect("Left", bg.transform);
            UiFactory.Place(left, 0, 0, 0.36f, 1, 24);
            _title = UiFactory.Label("Title", left, "MOTORCYCLE\nMOTORSPORT MANAGER", Theme.FontTitle, TextAnchor.UpperLeft);
            UiFactory.Place(_title.rectTransform, 0, 0.72f, 1, 1);
            _status = UiFactory.Label("Status", left, "", Theme.FontNormal, TextAnchor.UpperLeft, Theme.TextDim);
            UiFactory.Place(_status.rectTransform, 0, 0.56f, 1, 0.72f);
            _buttons = UiFactory.Rect("Buttons", left);
            UiFactory.Place(_buttons, 0, 0, 1, 0.56f);
            var layout = _buttons.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            _list = UiFactory.ScrollList("List", bg.transform);
            UiFactory.Place((RectTransform)_list.parent.parent, 0.37f, 0.02f, 0.98f, 0.98f);

            _session.DataLoaded += ShowMainMenu;
            _session.GameChanged += ShowCareer;
            _session.LoadData();
        }

        private void OnDestroy()
        {
            if (_session == null) return;
            _session.DataLoaded -= ShowMainMenu;
            _session.GameChanged -= ShowCareer;
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
            else if (_session.LoadError != null) _status.text = "Error loading data: " + _session.LoadError;
        }

        private void AddButton(string text, System.Action action, Color? color = null)
        {
            var b = UiFactory.Button("Button", _buttons, text, action, color);
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 96;
        }

        private void ShowMainMenu()
        {
            UiFactory.Clear(_buttons);
            UiFactory.Clear(_list);
            var db = _session.Db;
            _status.text = db.Riders.Count + " riders, " + db.Teams.Count + " teams, " + db.Circuits.Count + " circuits loaded" +
                           (_session.DataErrors.Count > 0 ? "\n" + _session.DataErrors.Count + " data warnings (see log)" : "");
            AddButton("New career", ShowTeamPicker, Theme.Accent);
            if (SaveManager.Exists(SaveManager.AutoSlot)) AddButton("Continue", () => _session.Continue());
        }

        private void ShowTeamPicker()
        {
            UiFactory.Clear(_list);
            _status.text = "Choose the team you will manage";
            foreach (var cat in _session.Db.CategoriesOrdered())
            {
                if (!cat.Simulated || cat.IsCup) continue;
                var header = UiFactory.Row(_list, 56);
                UiFactory.Stretch(UiFactory.Label("Header", header, cat.Name, Theme.FontLarge, TextAnchor.MiddleLeft, UiFactory.ParseColor(cat.Color, Theme.Accent)).rectTransform);
                var teams = new List<Team>();
                foreach (var t in _session.Db.Teams.Values)
                    if (t.CategoryId == cat.Id) teams.Add(t);
                teams.Sort((a, b) => b.Reputation.CompareTo(a.Reputation));
                foreach (var t in teams)
                {
                    var team = t;
                    var row = UiFactory.Row(_list, 72);
                    var b = UiFactory.Button("Team", row, "", () => _session.NewGame(team.Id, (ulong)System.DateTime.Now.Ticks), UiFactory.ParseColor(team.Colors.Primary, Theme.PanelLight) * 0.55f);
                    UiFactory.Stretch((RectTransform)b.transform);
                    var label = b.GetComponentInChildren<Text>();
                    label.alignment = TextAnchor.MiddleLeft;
                    label.text = "   " + team.Name + "   <color=#9AA4B2>" + team.Status + " · budget " + MathX.FormatMoney(team.SeasonBudget) + "</color>";
                    label.supportRichText = true;
                }
            }
        }

        private void ShowCareer()
        {
            var game = _session.Game;
            if (game == null)
            {
                _status.text = "Could not load the career.";
                return;
            }
            if (game.World.PlayerTeam != null)
            {
                // Full management interface; the boot screen is no longer needed.
                Screens.ManagementShell.Create(_session);
                Destroy(gameObject);
                return;
            }
            UiFactory.Clear(_buttons);
            AddButton("Play next event", PlayNext, Theme.Accent);
            AddButton("Simulate next event", SimulateNext);
            AddButton("Simulate rest of season", SimulateSeason);
            AddButton("Save", () => _session.Save());
            RefreshCareer();
        }

        private void PlayNext()
        {
            if (_session.Game.IsSeasonOver)
            {
                SimulateNext();
                return;
            }
            new RaceFlow(_session, gameObject, summary =>
            {
                _lastSummary = summary;
                RefreshCareer();
            }).Start();
        }

        private void SimulateNext()
        {
            var game = _session.Game;
            string summary = null;
            _session.Run("Simulating", () =>
            {
                if (game.IsSeasonOver)
                {
                    var s = game.EndSeason();
                    summary = "Season " + s.Season + " closed: " + s.Retired.Count + " retirements, " + s.Market.Moves.Count + " market moves.";
                    return;
                }
                var ev = game.BeginNextEvent();
                game.CompleteEvent(ev);
                summary = Summarise(game, ev);
            }, () =>
            {
                _lastSummary = summary;
                RefreshCareer();
            });
        }

        private void SimulateSeason()
        {
            var game = _session.Game;
            _session.Run("Simulating the season", () => game.SimulateRestOfSeason(), () =>
            {
                _lastSummary = "Season simulated.";
                RefreshCareer();
            });
        }

        private static string Summarise(GameController game, LiveEvent ev)
        {
            var sb = new StringBuilder();
            sb.Append("<b>").Append(ev.Plan.Round.Name).Append("</b>  ").Append(ev.Plan.Round.Date.ToString("d MMM yyyy")).Append('\n');
            foreach (var kv in ev.Weekends)
            {
                var cat = game.Db.GetCategory(kv.Key);
                foreach (var s in kv.Value.Result.Sessions)
                {
                    if (s.Classification.Count == 0 || (s.Type != Core.Data.SessionType.Race && s.Type != Core.Data.SessionType.Sprint)) continue;
                    var winner = game.World.GetRider(s.Classification[0].RiderId);
                    sb.Append(cat.ShortName).Append(" ").Append(s.SessionName).Append(": ").Append(winner != null ? winner.FullName : "?").Append('\n');
                }
            }
            return sb.ToString();
        }

        private void RefreshCareer()
        {
            var game = _session.Game;
            var world = game.World;
            var team = world.PlayerTeam;
            var next = game.PeekNextEvent();
            _status.text = (team != null ? team.Name + "\n" + MathX.FormatMoney(Accounts.Cash(world, team)) + "\n" : "") +
                           "Season " + world.Season + (next != null ? "  ·  next: " + next.Round.Name : "  ·  season finished");

            UiFactory.Clear(_list);
            if (!string.IsNullOrEmpty(_lastSummary)) AddText(_lastSummary, 40 + 34 * _lastSummary.Split('\n').Length);
            string catId = team != null ? team.CategoryId : "motogp";
            ChampionshipState champ;
            if (world.Championships.TryGetValue(catId, out champ))
            {
                var table = champ.SortedRiders();
                var sb = new StringBuilder("<b>" + game.Db.GetCategory(catId).Name + " standings</b>\n");
                for (int i = 0; i < table.Count && i < 15; i++)
                {
                    var r = world.GetRider(table[i].Id);
                    bool mine = r != null && team != null && r.TeamId == team.Id;
                    sb.Append(mine ? "<color=#E10600>" : "").Append(i + 1).Append(". ").Append(r != null ? r.FullName : table[i].Id)
                      .Append("  ").Append(table[i].Points.ToString("0")).Append(" pts").Append(mine ? "</color>" : "").Append('\n');
                }
                AddText(sb.ToString(), 60 + 34 * System.Math.Min(15, table.Count));
            }
            var news = new StringBuilder("<b>News</b>\n");
            for (int i = 0; i < world.News.Count && i < 8; i++) news.Append(world.News[i].Date.ToString("dd/MM")).Append("  ").Append(world.News[i].Headline).Append('\n');
            AddText(news.ToString(), 340);
        }

        private void AddText(string text, float height)
        {
            var row = UiFactory.Row(_list, height);
            var label = UiFactory.Label("Text", row, text, Theme.FontSmall + 2, TextAnchor.UpperLeft);
            label.supportRichText = true;
            UiFactory.Stretch(label.rectTransform);
        }
    }
}
