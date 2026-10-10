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
        private RectTransform _commandArea;
        private RectTransform _replayBar;
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
            UiFactory.Place((RectTransform)overview.transform, 0.005f, 0.13f, 0.07f, 0.19f);
            var follow = UiFactory.Button("Follow", _root, "Follow", () => _view.Select(_view.SelectedRider), Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)follow.transform, 0.075f, 0.13f, 0.14f, 0.19f);
            var sector = UiFactory.Button("Sector", _root, Loc.T("race.tower.sector"), () => _view.NextSectorView(), Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)sector.transform, 0.145f, 0.13f, 0.215f, 0.19f);

            // Event feed
            var feedBg = UiFactory.Panel("Feed", _root, new Color(0.06f, 0.07f, 0.09f, 0.7f));
            UiFactory.Place(feedBg.rectTransform, 0.005f, 0.005f, 0.45f, 0.12f);
            _feed = UiFactory.Label("FeedText", feedBg.transform, "", Theme.FontSmall - 2, TextAnchor.LowerLeft);
            UiFactory.Place(_feed.rectTransform, 0.01f, 0.02f, 0.99f, 0.98f);
            _feed.supportRichText = true;

            // Pit-wall cards for the player's riders (bottom right).
            var players = new List<RiderRaceState>();
            foreach (var r in sim.Riders) if (r.Entry.IsPlayer) players.Add(r);
            _commandArea = UiFactory.Rect("Commands", _root);
            UiFactory.Stretch(_commandArea);
            if (players.Count > 0)
            {
                float h = 0.44f / players.Count;
                for (int i = 0; i < players.Count; i++)
                {
                    var area = UiFactory.Rect("Command" + i, _commandArea);
                    UiFactory.Place(area, 0.7f, 0.005f + h * (players.Count - 1 - i), 0.995f, 0.005f + h * (players.Count - i) - 0.006f);
                    _panels.Add(new RiderCommandPanel(_view, players[i], area));
                }
                var session = GameSession.Instance;
                if (session != null && session.Game != null && session.Game.World.PlayerTeam != null)
                    _forecasterSkill = Core.Economy.TeamCapabilities.ForecastSkill(session.Game.World, session.Game.World.PlayerTeam);
            }

            RefreshTower();
        }

        private void UpdateAdvice()
        {
            if (_view.IsReplay) return;
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
            if (_cameraLabel != null && _cameraLabel.enabled && Time.unscaledTime > _cameraLabelUntil) _cameraLabel.enabled = false;
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
                string teamName = team != null ? (team.ShortName ?? team.Name) : "";
                if (teamName.Length > 10) teamName = teamName.Substring(0, 10);
                row.Name.text = r.Entry.Rider.Code + " <size=" + (Theme.FontSmall - 6) + "><color=#9AA4B2>" + teamName + "</color></size>" + (r.PendingLongLaps > 0 ? " <color=#F0B428>LL</color>" : "");
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

        private Text _cameraLabel;
        private float _cameraLabelUntil;

        public void ShowCameraLabel(string text)
        {
            if (_cameraLabel == null)
            {
                _cameraLabel = UiFactory.Label("CameraLabel", _root, "", Theme.FontLarge, TextAnchor.MiddleCenter, Theme.Text);
                UiFactory.Place(_cameraLabel.rectTransform, 0.35f, 0.82f, 0.65f, 0.9f);
                _cameraLabel.gameObject.AddComponent<Outline>().effectColor = Color.black;
            }
            _cameraLabel.text = text;
            _cameraLabel.enabled = true;
            _cameraLabelUntil = Time.unscaledTime + 2f;
        }

        public void ShowResults()
        {
            if (_results != null) return;
            _results = PostRacePanel.Build(_view.Sim, _root, () => _view.StartReplay(), () => _view.Close());
        }

        public void EnterReplay()
        {
            if (_results != null) _results.gameObject.SetActive(false);
            _commandArea.gameObject.SetActive(false);
            if (_replayBar == null)
            {
                var bar = UiFactory.Panel("Replay", _root, new Color(0.06f, 0.07f, 0.09f, 0.88f));
                _replayBar = bar.rectTransform;
                UiFactory.Place(_replayBar, 0.7f, 0.005f, 0.995f, 0.12f);
                var label = UiFactory.Label("Label", _replayBar, "REPLAY", Theme.FontNormal, TextAnchor.MiddleCenter, Theme.Warning);
                UiFactory.Place(label.rectTransform, 0, 0.55f, 1, 1);
                var back = UiFactory.Button("Back", _replayBar, "-1 lap", () => _view.SeekLaps(-1), Theme.PanelLight, Theme.FontSmall);
                UiFactory.Place((RectTransform)back.transform, 0.02f, 0.06f, 0.32f, 0.52f);
                var fwd = UiFactory.Button("Fwd", _replayBar, "+1 lap", () => _view.SeekLaps(1), Theme.PanelLight, Theme.FontSmall);
                UiFactory.Place((RectTransform)fwd.transform, 0.35f, 0.06f, 0.65f, 0.52f);
                var exit = UiFactory.Button("Exit", _replayBar, "Results", () => _view.ExitReplay(), Theme.Accent, Theme.FontSmall);
                UiFactory.Place((RectTransform)exit.transform, 0.68f, 0.06f, 0.98f, 0.52f);
            }
            _replayBar.gameObject.SetActive(true);
            _feedCount = -1;
        }

        public void ExitReplay()
        {
            if (_replayBar != null) _replayBar.gameObject.SetActive(false);
            _commandArea.gameObject.SetActive(true);
            if (_results != null) _results.gameObject.SetActive(true);
            _feedCount = -1;
        }

        private static RiderRaceState FindRider(RaceSimulation sim, string riderId)
        {
            foreach (var r in sim.Riders)
                if (r.Entry.Rider.Id == riderId) return r;
            return sim.Riders[0];
        }
    }
}
