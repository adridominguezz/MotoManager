using System.Collections.Generic;
using System.Text;
using MotoManager.App.Race;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Race overlay: timing tower (tap a rider to follow), lap counter, conditions and flags, playback speed,
    /// camera modes, event feed, name tags over the bikes and the final classification.
    /// </summary>
    public class RaceHud : MonoBehaviour
    {
        private RaceView _view;
        private RectTransform _root;
        private Text _lapText;
        private Text _conditions;
        private Text _flag;
        private Image _flagBg;
        private Text _feed;
        private readonly List<TowerRow> _rows = new List<TowerRow>();
        private readonly List<Text> _tags = new List<Text>();
        private readonly List<Image> _speedButtons = new List<Image>();
        private RectTransform _tagLayer;
        private RectTransform _results;
        private float _towerTimer;
        private int _feedCount = -1;
        private readonly List<RiderCommandPanel> _panels = new List<RiderCommandPanel>();
        private readonly Dictionary<string, double> _adviceShownAt = new Dictionary<string, double>();
        private readonly List<string> _engineerLines = new List<string>();
        private float _adviceTimer;
        private double _forecasterSkill = 50;
        private float _forecast = -1;

        private class TowerRow
        {
            public RectTransform Rect;
            public Image Background;
            public Image TeamBar;
            public Text Position;
            public Text Name;
            public Text Gap;
            public Text Tires;
        }

        public static RaceHud Create(RaceView view, string title)
        {
            var canvas = UiFactory.CreateCanvas("RaceHud", 10);
            var hud = canvas.gameObject.AddComponent<RaceHud>();
            hud._view = view;
            hud._root = (RectTransform)canvas.transform;
            hud.Build(title);
            return hud;
        }

        public static bool PointerOverUi(Vector2 screenPos)
        {
            if (EventSystem.current == null) return false;
            var data = new PointerEventData(EventSystem.current) { position = screenPos };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            return hits.Count > 0;
        }

        private void Build(string title)
        {
            var sim = _view.Sim;

            // Name tags under everything else.
            _tagLayer = UiFactory.Rect("Tags", _root);
            UiFactory.Stretch(_tagLayer);
            foreach (var r in sim.Riders)
            {
                var t = UiFactory.Label("Tag", _tagLayer, r.Entry.Rider.Number + " " + r.Entry.Rider.Code, Theme.FontSmall - 2, TextAnchor.MiddleCenter);
                t.rectTransform.sizeDelta = new Vector2(120, 30);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = Vector2.zero;
                var outline = t.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0, 0, 0, 0.9f);
                if (r.Entry.IsPlayer) t.color = Theme.Warning;
                _tags.Add(t);
            }

            // Top bar
            var top = UiFactory.Panel("TopBar", _root, new Color(0.06f, 0.07f, 0.09f, 0.85f));
            UiFactory.Place(top.rectTransform, 0, 0.93f, 1, 1);
            var titleText = UiFactory.Label("Title", top.transform, title, Theme.FontNormal, TextAnchor.MiddleLeft);
            UiFactory.Place(titleText.rectTransform, 0.01f, 0, 0.3f, 1);
            _lapText = UiFactory.Label("Lap", top.transform, "", Theme.FontLarge, TextAnchor.MiddleCenter);
            UiFactory.Place(_lapText.rectTransform, 0.3f, 0, 0.45f, 1);
            _conditions = UiFactory.Label("Conditions", top.transform, "", Theme.FontSmall, TextAnchor.MiddleLeft, Theme.TextDim);
            UiFactory.Place(_conditions.rectTransform, 0.45f, 0, 0.66f, 1);
            _flagBg = UiFactory.Panel("Flag", top.transform, new Color(0, 0, 0, 0));
            UiFactory.Place(_flagBg.rectTransform, 0.66f, 0.1f, 0.74f, 0.9f);
            _flag = UiFactory.Label("FlagText", _flagBg.transform, "", Theme.FontSmall, TextAnchor.MiddleCenter, Color.black);
            UiFactory.Stretch(_flag.rectTransform);

            // Speed controls
            string[] labels = { "II", "1x", "2x", "4x", "8x" };
            for (int i = 0; i < labels.Length; i++)
            {
                float speed = RaceView.Speeds[i];
                var b = UiFactory.Button("Speed" + labels[i], top.transform, labels[i], () => _view.SetSpeed(speed), Theme.PanelLight, Theme.FontSmall);
                UiFactory.Place((RectTransform)b.transform, 0.745f + i * 0.042f, 0.1f, 0.783f + i * 0.042f, 0.9f);
                _speedButtons.Add(b.GetComponent<Image>());
            }
            var skip = UiFactory.Button("Skip", top.transform, ">>|", () => _view.FinishInstantly(), Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)skip.transform, 0.957f, 0.1f, 0.995f, 0.9f);

            // Timing tower
            var tower = UiFactory.Panel("Tower", _root, new Color(0.06f, 0.07f, 0.09f, 0.8f));
            UiFactory.Place(tower.rectTransform, 0.005f, 0.2f, 0.215f, 0.92f);
            float rowH = 1f / Mathf.Max(sim.Riders.Count, 18);
            for (int i = 0; i < sim.Riders.Count; i++)
            {
                var row = new TowerRow();
                var bg = UiFactory.Panel("Row", tower.transform, new Color(1, 1, 1, 0.0f));
                row.Background = bg;
                row.Rect = bg.rectTransform;
                UiFactory.Place(row.Rect, 0, 1 - rowH * (i + 1), 1, 1 - rowH * i);
                var button = bg.gameObject.AddComponent<Button>();
                int index = i;
                button.onClick.AddListener(() => OnRowClicked(index));
                row.Position = UiFactory.Label("Pos", row.Rect, "", Theme.FontSmall, TextAnchor.MiddleRight);
                UiFactory.Place(row.Position.rectTransform, 0, 0, 0.12f, 1);
                row.TeamBar = UiFactory.Panel("Team", row.Rect, Color.white);
                UiFactory.Place(row.TeamBar.rectTransform, 0.14f, 0.15f, 0.16f, 0.85f);
                row.Name = UiFactory.Label("Name", row.Rect, "", Theme.FontSmall, TextAnchor.MiddleLeft);
                UiFactory.Place(row.Name.rectTransform, 0.18f, 0, 0.55f, 1);
                row.Gap = UiFactory.Label("Gap", row.Rect, "", Theme.FontSmall - 2, TextAnchor.MiddleRight, Theme.TextDim);
                UiFactory.Place(row.Gap.rectTransform, 0.5f, 0, 0.84f, 1);
                row.Tires = UiFactory.Label("Tires", row.Rect, "", Theme.FontSmall - 4, TextAnchor.MiddleCenter, Theme.TextDim);
                UiFactory.Place(row.Tires.rectTransform, 0.85f, 0, 1, 1);
                _rows.Add(row);
            }

            // Camera buttons
            var overview = UiFactory.Button("Overview", _root, "Circuit", () => _view.Cam.SetMode(CameraMode.Overview), Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)overview.transform, 0.005f, 0.13f, 0.1f, 0.19f);
            var follow = UiFactory.Button("Follow", _root, "Follow", () => _view.Select(_view.SelectedRider), Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)follow.transform, 0.11f, 0.13f, 0.215f, 0.19f);

            // Event feed
            var feedBg = UiFactory.Panel("Feed", _root, new Color(0.06f, 0.07f, 0.09f, 0.7f));
            UiFactory.Place(feedBg.rectTransform, 0.005f, 0.005f, 0.45f, 0.12f);
            _feed = UiFactory.Label("FeedText", feedBg.transform, "", Theme.FontSmall - 2, TextAnchor.LowerLeft);
            UiFactory.Place(_feed.rectTransform, 0.01f, 0.02f, 0.99f, 0.98f);
            _feed.supportRichText = true;

            // Pit-wall cards for the player's riders (bottom right).
            var players = new List<RiderRaceState>();
            foreach (var r in sim.Riders) if (r.Entry.IsPlayer) players.Add(r);
            if (players.Count > 0)
            {
                float h = 0.44f / players.Count;
                for (int i = 0; i < players.Count; i++)
                {
                    var area = UiFactory.Rect("Command" + i, _root);
                    UiFactory.Place(area, 0.7f, 0.005f + h * (players.Count - 1 - i), 0.995f, 0.005f + h * (players.Count - i) - 0.006f);
                    _panels.Add(new RiderCommandPanel(_view, players[i], area));
                }
                var session = GameSession.Instance;
                if (session != null && session.Game != null && session.Game.World.PlayerTeam != null)
                    _forecasterSkill = Core.Economy.StaffService.BestSkill(session.Game.World, session.Game.World.PlayerTeam, StaffRole.DataEngineer, 45);
            }

            RefreshTower();
        }

        private void UpdateAdvice()
        {
            var sim = _view.Sim;
            bool added = false;
            foreach (var r in sim.Riders)
            {
                if (!r.Entry.IsPlayer) continue;
                foreach (var m in RaceEngineer.Advise(sim, r, _forecasterSkill))
                {
                    double last;
                    if (_adviceShownAt.TryGetValue(m.Key, out last) && sim.Time - last < 30) continue;
                    _adviceShownAt[m.Key] = sim.Time;
                    string color = m.Severity >= 2 ? "#E10600" : m.Severity == 1 ? "#F0B428" : "#5BC0EB";
                    _engineerLines.Add("<color=" + color + ">ENG</color>  " + m.Text);
                    if (_engineerLines.Count > 3) _engineerLines.RemoveAt(0);
                    added = true;
                }
            }
            if (added) RefreshFeed();
            _forecast = (float)sim.ForecastRain(600, _forecasterSkill);
        }

        private void OnRowClicked(int position)
        {
            var r = _view.Sim.RiderAtPosition(position + 1);
            if (r != null) _view.Select(r.Index);
        }

        private void Update()
        {
            var sim = _view.Sim;
            int lap = Mathf.Clamp(sim.LeaderLap + 1, 1, sim.TotalLaps);
            _lapText.text = sim.ChequeredFlag ? "FINISH" : (sim.Tick == 0 ? "GRID" : "LAP " + lap + "/" + sim.TotalLaps);
            var w = sim.Weather;
            _conditions.text = w.Condition + "  ·  track " + w.TrackTemp.ToString("0") + "°C  ·  wet " + (w.TrackWetness * 100).ToString("0") + "%" +
                               (_forecast >= 0 ? "\nRain in 10': " + (_forecast * 100).ToString("0") + "%" : "");
            UpdateFlag(sim);

            for (int i = 0; i < _speedButtons.Count; i++)
                _speedButtons[i].color = Mathf.Approximately(RaceView.Speeds[i], _view.Speed) ? Theme.Accent : Theme.PanelLight;

            _towerTimer -= Time.unscaledDeltaTime;
            if (_towerTimer <= 0)
            {
                _towerTimer = 0.25f;
                RefreshTower();
                foreach (var panel in _panels) panel.Refresh();
            }
            _adviceTimer -= Time.unscaledDeltaTime;
            if (_adviceTimer <= 0)
            {
                _adviceTimer = 1f;
                UpdateAdvice();
            }
            if (sim.Events.Count != _feedCount) RefreshFeed();
            UpdateTags();
        }

        private void UpdateFlag(RaceSimulation sim)
        {
            string text = "";
            Color color = new Color(0, 0, 0, 0);
            if (sim.ChequeredFlag) { text = "CHEQUERED"; color = Color.white; }
            else if (sim.WhiteFlag) { text = "WHITE FLAG"; color = new Color(0.95f, 0.95f, 0.95f); }
            else
            {
                foreach (var r in sim.Riders)
                    if (r.Status == RaceStatus.Crashed) { text = "YELLOW"; color = Theme.Warning; break; }
            }
            if (text == "" && sim.Tick > 0) { text = "GREEN"; color = Theme.Good; }
            _flag.text = text;
            _flagBg.color = color;
        }

        private void RefreshTower()
        {
            var sim = _view.Sim;
            for (int p = 0; p < _rows.Count; p++)
            {
                var row = _rows[p];
                var r = sim.RiderAtPosition(p + 1);
                if (r == null) continue;
                var team = r.Entry.Team;
                row.Position.text = (p + 1).ToString();
                row.TeamBar.color = UiFactory.ParseColor(team != null ? team.Colors.Primary : null, Color.gray);
                row.Name.text = r.Entry.Rider.Code + (r.PendingLongLaps > 0 ? " <color=#F0B428>LL</color>" : "");
                row.Name.supportRichText = true;
                row.Name.color = r.Entry.IsPlayer ? Theme.Warning : Theme.Text;
                row.Gap.text = GapText(sim, r, p);
                row.Tires.text = CompoundLetter(r.Front.Compound) + "/" + CompoundLetter(r.Rear.Compound);
                row.Background.color = r.Index == _view.SelectedRider ? new Color(1, 1, 1, 0.12f) : new Color(1, 1, 1, 0);
            }
        }

        private static string GapText(RaceSimulation sim, RiderRaceState r, int position)
        {
            switch (r.Status)
            {
                case RaceStatus.Crashed: return r.RemountAt > 0 ? "<color=#F0B428>CRASH</color>" : "<color=#E10600>OUT</color>";
                case RaceStatus.Retired: return "<color=#E10600>DNF</color>";
                case RaceStatus.Disqualified: return "DSQ";
                case RaceStatus.InPitLane: return "<color=#5BC0EB>PIT</color>";
            }
            if (position == 0) return r.Status == RaceStatus.Finished ? MathX.FormatLapTime(r.FinishTime) : "Leader";
            if (r.LapsDown > 0) return "+" + r.LapsDown + " lap" + (r.LapsDown > 1 ? "s" : "");
            return MathX.FormatGap(r.GapToLeader);
        }

        public static string CompoundLetter(TireCompound c)
        {
            switch (c)
            {
                case TireCompound.Soft: return "S";
                case TireCompound.Medium: return "M";
                case TireCompound.Hard: return "H";
                case TireCompound.Intermediate: return "I";
                default: return "W";
            }
        }

        private void RefreshFeed()
        {
            var sim = _view.Sim;
            _feedCount = sim.Events.Count;
            var lines = new List<string>();
            for (int i = sim.Events.Count - 1; i >= 0 && lines.Count < 4 - System.Math.Min(2, _engineerLines.Count); i--)
            {
                var e = sim.Events[i];
                if (!e.Important) continue;
                lines.Insert(0, "<color=#9AA4B2>L" + e.Lap + "</color>  " + e.Detail);
            }
            for (int i = System.Math.Max(0, _engineerLines.Count - 2); i < _engineerLines.Count; i++) lines.Add(_engineerLines[i]);
            _feed.text = string.Join("\n", lines.ToArray());
        }

        private void UpdateTags()
        {
            var cam = _view.Cam.Camera;
            if (cam == null) return;
            var canvasRect = _root.rect;
            for (int i = 0; i < _tags.Count; i++)
            {
                var bike = _view.Bikes[i].transform;
                var screen = cam.WorldToScreenPoint(bike.position + Vector3.up * 3f * bike.localScale.y);
                bool visible = screen.z > 0 && _view.Bikes[i].Body.gameObject.activeSelf;
                if (_tags[i].enabled != visible) _tags[i].enabled = visible;
                if (!visible) continue;
                // Screen -> canvas units (the canvas scales with the screen).
                float sx = canvasRect.width / Screen.width, sy = canvasRect.height / Screen.height;
                _tags[i].rectTransform.anchoredPosition = new Vector2(screen.x * sx, screen.y * sy + 14);
            }
        }

        public void ShowResults()
        {
            if (_results != null) return;
            var sim = _view.Sim;
            var panel = UiFactory.Panel("Results", _root, new Color(0.06f, 0.07f, 0.09f, 0.95f));
            _results = panel.rectTransform;
            UiFactory.Place(_results, 0.25f, 0.06f, 0.75f, 0.9f);
            var title = UiFactory.Label("Title", _results, sim.Config.SessionName + " - Results", Theme.FontLarge, TextAnchor.MiddleCenter);
            UiFactory.Place(title.rectTransform, 0, 0.9f, 1, 1);
            var list = UiFactory.ScrollList("List", _results);
            UiFactory.Place((RectTransform)list.parent.parent, 0.03f, 0.12f, 0.97f, 0.89f);
            foreach (var e in sim.GetClassification())
            {
                var r = FindRider(sim, e.RiderId);
                var row = UiFactory.Row(list, 40);
                string time = e.Status == ResultStatus.Finished ? (e.Position == 1 ? MathX.FormatLapTime(e.TotalTime) : MathX.FormatGap(e.GapToWinner)) : e.Status + (e.Note != null ? " (" + e.Note + ")" : "");
                var label = UiFactory.Label("Row", row, e.Position + ".  " + r.Entry.Rider.FullName + "  <color=#9AA4B2>" + (r.Entry.Team != null ? r.Entry.Team.ShortName ?? r.Entry.Team.Name : "") + "</color>", Theme.FontSmall, TextAnchor.MiddleLeft, r.Entry.IsPlayer ? Theme.Warning : Theme.Text);
                label.supportRichText = true;
                UiFactory.Place(label.rectTransform, 0, 0, 0.72f, 1);
                var t = UiFactory.Label("Time", row, time, Theme.FontSmall, TextAnchor.MiddleRight, Theme.TextDim);
                UiFactory.Place(t.rectTransform, 0.72f, 0, 1, 1);
            }
            var cont = UiFactory.Button("Continue", _results, "Continue", () => _view.Close(), Theme.Accent);
            UiFactory.Place((RectTransform)cont.transform, 0.3f, 0.02f, 0.7f, 0.1f);
        }

        private static RiderRaceState FindRider(RaceSimulation sim, string riderId)
        {
            foreach (var r in sim.Riders)
                if (r.Entry.Rider.Id == riderId) return r;
            return sim.Riders[0];
        }
    }
}
