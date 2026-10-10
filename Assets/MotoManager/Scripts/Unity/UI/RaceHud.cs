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
        private readonly List<Image> _plates = new List<Image>();
        private readonly List<Text> _dotNumbers = new List<Text>();
        private RectTransform _tower;
        private Button _towerButton;
        private RectTransform _leftCard;
        private Text _banner;
        private Image _bannerBg;
        private const string TowerPref = "MotoManager.RaceTower";
        private const string GapPref = "MotoManager.RaceGapMode";
        private Button _gapButton;
        /// <summary>Gaps to the rider ahead (interval) instead of to the leader.</summary>
        private static bool _gapToAhead;
        private readonly List<Image> _speedButtons = new List<Image>();
        private Button _camCircuit, _camFollow, _camSector;
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
                // Plate over the bike (close view): position, name, tyre and gap.
                var plate = UiFactory.Panel("Plate", _tagLayer, new Color(0.93f, 0.95f, 0.97f, 0.95f));
                plate.rectTransform.sizeDelta = new Vector2(330, 38);
                plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = Vector2.zero;
                plate.rectTransform.pivot = new Vector2(0.5f, 0);
                plate.raycastTarget = false;
                var bar = UiFactory.Panel("Team", plate.transform, UiFactory.ParseColor(r.Entry.Team != null ? r.Entry.Team.Colors.Primary : null, Color.gray));
                bar.raycastTarget = false;
                UiFactory.Place(bar.rectTransform, 0.1f, 0.1f, 0.115f, 0.9f);
                var t = UiFactory.Label("Text", plate.transform, "", Theme.FontSmall - 2, TextAnchor.MiddleLeft, new Color32(20, 28, 44, 255));
                t.supportRichText = true;
                UiFactory.Place(t.rectTransform, 0.02f, 0, 0.99f, 1);
                _plates.Add(plate);
                _tags.Add(t);
                // Number inside the far-view dot.
                var n = UiFactory.Label("DotNumber", _tagLayer, "", Theme.FontSmall, TextAnchor.MiddleCenter, Color.white);
                n.fontStyle = FontStyle.Bold;
                n.rectTransform.sizeDelta = new Vector2(48, 30);
                n.rectTransform.anchorMin = n.rectTransform.anchorMax = Vector2.zero;
                n.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
                _dotNumbers.Add(n);
            }

            // Top bar
            var top = UiFactory.Panel("TopBar", _root, new Color(0.043f, 0.075f, 0.125f, 0.85f));
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
            var tower = UiFactory.Panel("Tower", _root, new Color(0.043f, 0.075f, 0.125f, 0.9f), true);
            UiFactory.Place(tower.rectTransform, 0.005f, 0.005f, 0.22f, 0.855f);
            _tower = tower.rectTransform;
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
            var overview = _camCircuit = UiFactory.Button("Overview", _root, Loc.T("race.cam.circuit"), () => _view.Cam.SetMode(CameraMode.Overview), Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)overview.transform, 0.775f, 0.865f, 0.85f, 0.925f, 3);
            var follow = _camFollow = UiFactory.Button("Follow", _root, Loc.T("race.cam.follow"), () => _view.Select(_view.SelectedRider), Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)follow.transform, 0.85f, 0.865f, 0.925f, 0.925f, 3);
            var sector = _camSector = UiFactory.Button("Sector", _root, Loc.T("race.tower.sector"), () => _view.NextSectorView(), Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)sector.transform, 0.925f, 0.865f, 0.995f, 0.925f, 3);
            _towerButton = UiFactory.Button("TowerToggle", _root, Loc.T("race.standings"), () => SetTower(!_tower.gameObject.activeSelf), Theme.PanelLight, Theme.FontSmall);
            UiFactory.Place((RectTransform)_towerButton.transform, 0.005f, 0.865f, 0.11f, 0.925f, 3);
            try { _gapToAhead = PlayerPrefs.GetInt(GapPref, 0) == 1; } catch (System.Exception) { }
            _gapButton = UiFactory.Button("GapMode", _root, "", () =>
            {
                _gapToAhead = !_gapToAhead;
                try { PlayerPrefs.SetInt(GapPref, _gapToAhead ? 1 : 0); } catch (System.Exception) { }
                RefreshTower();
            }, Theme.PanelLight, Theme.FontSmall - 2);
            UiFactory.Place((RectTransform)_gapButton.transform, 0.11f, 0.865f, 0.22f, 0.925f, 3);

            // Event feed
            var feedBg = UiFactory.Panel("Feed", _root, new Color(0.043f, 0.075f, 0.125f, 0.7f));
            UiFactory.Place(feedBg.rectTransform, 0.26f, 0.005f, 0.74f, 0.11f);
            _feed = UiFactory.Label("FeedText", feedBg.transform, "", Theme.FontSmall - 2, TextAnchor.LowerCenter);
            UiFactory.Place(_feed.rectTransform, 0.01f, 0.02f, 0.99f, 0.98f);
            _feed.supportRichText = true;

            // Pit-wall cards for the player's riders (bottom right).
            var players = new List<RiderRaceState>();
            foreach (var r in sim.Riders) if (r.Entry.IsPlayer) players.Add(r);
            _commandArea = UiFactory.Rect("Commands", _root);
            UiFactory.Stretch(_commandArea);
            if (players.Count > 0)
            {
                // First rider bottom left, the others bottom right.
                for (int i = 0; i < players.Count; i++)
                {
                    var area = UiFactory.Rect("Command" + i, _commandArea);
                    if (i == 0) _leftCard = area;
                    else UiFactory.Place(area, 0.755f, 0.005f + 0.37f * (i - 1), 0.995f, 0.37f * i);
                    _panels.Add(new RiderCommandPanel(_view, players[i], area));
                }
                var session = GameSession.Instance;
                if (session != null && session.Game != null && session.Game.World.PlayerTeam != null)
                    _forecasterSkill = Core.Economy.TeamCapabilities.ForecastSkill(session.Game.World, session.Game.World.PlayerTeam);
            }

            // Race-day banner shown while the camera presents the circuit.
            _bannerBg = UiFactory.Panel("Banner", _root, new Color(0, 0, 0, 0));
            _bannerBg.raycastTarget = false;
            UiFactory.Place(_bannerBg.rectTransform, 0.02f, 0.1f, 0.6f, 0.3f);
            var tag = UiFactory.Panel("RaceDay", _bannerBg.transform, new Color32(150, 80, 200, 235), true);
            UiFactory.Place(tag.rectTransform, 0, 0.55f, 0.36f, 0.95f);
            var tagText = UiFactory.Label("Text", tag.transform, Loc.T("race.day"), Theme.FontNormal, TextAnchor.MiddleCenter);
            tagText.fontStyle = FontStyle.Bold;
            UiFactory.Stretch(tagText.rectTransform);
            _banner = UiFactory.Label("Circuit", _bannerBg.transform, "", Theme.FontTitle - 6, TextAnchor.MiddleLeft);
            _banner.fontStyle = FontStyle.Bold;
            _banner.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            UiFactory.Place(_banner.rectTransform, 0, 0, 1, 0.55f);
            _bannerBg.gameObject.SetActive(false);

            bool towerOn = false;
            try { towerOn = PlayerPrefs.GetInt(TowerPref, 0) == 1; } catch (System.Exception) { }
            SetTower(towerOn);
            RefreshTower();
        }

        /// <summary>Shows or hides the full classification (remembered); the left rider card makes room for it.</summary>
        public void SetTower(bool on)
        {
            _tower.gameObject.SetActive(on);
            // With the standings open, the first rider's card moves to the right, above the second one.
            if (_leftCard != null)
            {
                if (on) UiFactory.Place(_leftCard, 0.755f, 0.005f + 0.37f * (_panels.Count - 1), 0.995f, 0.37f * _panels.Count);
                else UiFactory.Place(_leftCard, 0.005f, 0.005f, 0.245f, 0.37f);
            }
            _gapButton.gameObject.SetActive(on);
            _towerButton.image.color = on ? Theme.Accent : Theme.PanelLight;
            try { PlayerPrefs.SetInt(TowerPref, on ? 1 : 0); } catch (System.Exception) { }
        }

        /// <summary>Banner and HUD state while the camera presents the circuit before the start.</summary>
        public void ShowIntro(string circuit, string country, bool on)
        {
            _bannerBg.gameObject.SetActive(on);
            _banner.text = (string.IsNullOrEmpty(country) ? "" : country.ToUpperInvariant() + " · ") + circuit.ToUpperInvariant();
            _commandArea.gameObject.SetActive(!on);
            _tagLayer.gameObject.SetActive(!on);
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
                    _engineerLines.Add("<color=" + color + ">" + Loc.T("feed.engineer") + "</color>  " + m.Text);
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
            _lapText.text = sim.ChequeredFlag ? Loc.T("race.finish") : (sim.Tick == 0 ? Loc.T("race.grid") : Loc.T("race.lap", lap, sim.TotalLaps));
            var w = sim.Weather;
            _conditions.text = Loc.Or("weather." + w.Condition, w.Condition.ToString()) + "  ·  " + Loc.T("race.trackTemp", w.TrackTemp.ToString("0")) + "  ·  " + Loc.T("race.wet", (w.TrackWetness * 100).ToString("0")) +
                               (_forecast >= 0 ? "\n" + Loc.T("race.rainForecast", (_forecast * 100).ToString("0")) : "");
            UpdateFlag(sim);

            for (int i = 0; i < _speedButtons.Count; i++)
                _speedButtons[i].color = Mathf.Approximately(RaceView.Speeds[i], _view.Speed) ? Theme.Accent : Theme.PanelLight;
            var mode = _view.Cam.Mode;
            _camCircuit.image.color = mode == CameraMode.Overview ? Theme.Accent : Theme.PanelLight;
            _camFollow.image.color = mode == CameraMode.Follow ? Theme.Accent : Theme.PanelLight;
            _camSector.image.color = mode == CameraMode.Sector ? Theme.Accent : Theme.PanelLight;

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
            if (sim.ChequeredFlag) { text = Loc.T("flag.chequered"); color = Color.white; }
            else if (sim.WhiteFlag) { text = Loc.T("flag.white"); color = new Color(0.95f, 0.95f, 0.95f); }
            else
            {
                foreach (var r in sim.Riders)
                    if (r.Status == RaceStatus.Crashed) { text = Loc.T("flag.yellow"); color = Theme.Warning; break; }
            }
            if (text == "" && sim.Tick > 0) { text = Loc.T("flag.green"); color = Theme.Good; }
            _flag.text = text;
            _flagBg.color = color;
        }

        private void RefreshTower()
        {
            if (_gapButton != null) _gapButton.GetComponentInChildren<Text>().text = Loc.T(_gapToAhead ? "race.gap.interval" : "race.gap.leader");
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
                case RaceStatus.Crashed: return r.RemountAt > 0 ? "<color=#F0B428>" + Loc.T("tower.crash") + "</color>" : "<color=#E10600>" + Loc.T("tower.out") + "</color>";
                case RaceStatus.Retired: return "<color=#E10600>" + Loc.T("tower.dnf") + "</color>";
                case RaceStatus.Disqualified: return "DSQ";
                case RaceStatus.InPitLane: return "<color=#5BC0EB>" + Loc.T("tower.pit") + "</color>";
            }
            if (position == 0) return r.Status == RaceStatus.Finished ? MathX.FormatLapTime(r.FinishTime) : Loc.T("tower.leader");
            if (r.LapsDown > 0) return Loc.T(r.LapsDown > 1 ? "tower.laps" : "tower.lap", r.LapsDown);
            return MathX.FormatGap(_gapToAhead ? r.GapToAhead : r.GapToLeader);
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
                lines.Insert(0, "<color=#9AA4B2>" + Loc.T("feed.lap", e.Lap) + "</color>  " + e.Detail);
            }
            for (int i = System.Math.Max(0, _engineerLines.Count - 2); i < _engineerLines.Count; i++) lines.Add(_engineerLines[i]);
            _feed.text = string.Join("\n", lines.ToArray());
        }

        private void UpdateTags()
        {
            var cam = _view.Cam.Camera;
            if (cam == null) return;
            var sim = _view.Sim;
            var canvasRect = _root.rect;
            float sx = canvasRect.width / Screen.width, sy = canvasRect.height / Screen.height;
            var leader = sim.RiderAtPosition(1);
            for (int i = 0; i < _tags.Count; i++)
            {
                var bike = _view.Bikes[i];
                var r = bike.State;
                var screen = cam.WorldToScreenPoint(bike.transform.position + Vector3.up * (bike.ShowingMarker ? 0f : 2.2f * bike.transform.localScale.y));
                bool onScreen = bike.Visible && screen.z > 0;

                // Far view: every dot carries its position.
                bool dot = onScreen && bike.ShowingMarker;
                if (_dotNumbers[i].enabled != dot) _dotNumbers[i].enabled = dot;
                if (dot)
                {
                    _dotNumbers[i].text = r.Position.ToString();
                    _dotNumbers[i].rectTransform.anchoredPosition = new Vector2(screen.x * sx, screen.y * sy);
                }

                // Plates for the followed rider, the player's riders and the leader.
                bool wanted = onScreen && (i == _view.SelectedRider || r.Entry.IsPlayer || (leader != null && leader.Index == i));
                if (_plates[i].gameObject.activeSelf != wanted) _plates[i].gameObject.SetActive(wanted);
                if (!wanted) continue;
                string tyre = CompoundLetter(r.Rear.Compound) + " " + ((1 - r.Rear.Wear) * 100).ToString("0") + "%";
                string gap = r.Position == 1 ? Loc.T("tower.leader").ToUpperInvariant() : GapText(sim, r, r.Position - 1);
                _tags[i].text = "<b>" + r.Position + "</b>      <b>" + r.Entry.Rider.ShortName.ToUpperInvariant() + "</b>   " + tyre + "   " + gap;
                _plates[i].color = r.Entry.IsPlayer ? new Color(1f, 0.93f, 0.7f, 0.96f) : new Color(0.93f, 0.95f, 0.97f, 0.95f);
                _plates[i].rectTransform.anchoredPosition = new Vector2(screen.x * sx, screen.y * sy + (bike.ShowingMarker ? 26 : 18));
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
                var bar = UiFactory.Panel("Replay", _root, new Color(0.043f, 0.075f, 0.125f, 0.88f));
                _replayBar = bar.rectTransform;
                UiFactory.Place(_replayBar, 0.7f, 0.005f, 0.995f, 0.12f);
                var label = UiFactory.Label("Label", _replayBar, Loc.T("replay.title"), Theme.FontNormal, TextAnchor.MiddleCenter, Theme.Warning);
                UiFactory.Place(label.rectTransform, 0, 0.55f, 1, 1);
                var back = UiFactory.Button("Back", _replayBar, Loc.T("replay.back"), () => _view.SeekLaps(-1), Theme.PanelLight, Theme.FontSmall);
                UiFactory.Place((RectTransform)back.transform, 0.02f, 0.06f, 0.32f, 0.52f);
                var fwd = UiFactory.Button("Fwd", _replayBar, Loc.T("replay.fwd"), () => _view.SeekLaps(1), Theme.PanelLight, Theme.FontSmall);
                UiFactory.Place((RectTransform)fwd.transform, 0.35f, 0.06f, 0.65f, 0.52f);
                var exit = UiFactory.Button("Exit", _replayBar, Loc.T("replay.results"), () => _view.ExitReplay(), Theme.Accent, Theme.FontSmall);
                UiFactory.Place((RectTransform)exit.transform, 0.68f, 0.06f, 0.98f, 0.52f);
            }
            _replayBar.gameObject.SetActive(true);
            _feedCount = -1;
        }

        public void ExitReplay()
        {
            if (_replayBar != null) _replayBar.gameObject.SetActive(false);
            _commandArea.gameObject.SetActive(true);
            SetTower(_tower.gameObject.activeSelf);
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
