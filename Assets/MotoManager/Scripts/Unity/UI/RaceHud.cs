using System.Collections.Generic;
using MotoManager.App.Race;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Race overlay: a single-line top bar (session, lap, flag, conditions, playback speed), the timing tower fixed
    /// on the left (tap a rider to follow), the player's riders stacked on the right, camera modes above them, the
    /// event feed bottom-centre, name tags over the bikes and the final classification.
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
        private RectTransform _feedRect;
        private readonly List<TowerRow> _rows = new List<TowerRow>();
        private readonly List<Text> _tags = new List<Text>();
        private readonly List<Image> _plates = new List<Image>();
        private readonly List<Text> _dotNumbers = new List<Text>();
        private RectTransform _tower;
        private Text _banner;
        private Image _bannerBg;
        // Phone layout: standings on the left, the player's riders on the right, the track in between.
        private const float TowerRight = 0.245f;
        private const float CardLeft = 0.755f;
        private const float TopH = 64f, StripH = 48f, Gap = 8f;
        private const float TowerRowH = 40f;
        private const string GapPref = "MotoManager.RaceGapMode";
        private Text _gapLabel;
        /// <summary>Gaps to the rider ahead (interval) instead of to the leader.</summary>
        private static bool _gapToAhead;
        private readonly List<Segment> _speedButtons = new List<Segment>();
        private readonly List<Segment> _camButtons = new List<Segment>();
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
            public Image Edge;
            public Image TeamBar;
            public Text Position;
            public Image PosBlock;
            public Text Name;
            public Text Gap;
            public Text Tires;
        }

        /// <summary>One segment of a segmented control (speed, camera): lit and underlined when active.</summary>
        private class Segment
        {
            public Image Fill;
            public Image Underline;
            public Text Label;

            public void Set(bool on)
            {
                Fill.color = on ? Theme.PanelLight : Color.Lerp(Theme.Background, Theme.Panel, 0.85f);
                if (Underline.enabled != on) Underline.enabled = on;
                Label.color = on ? Color.white : Color.Lerp(Theme.TextDim, Color.white, 0.2f);
                UiFactory.Style(Label, FontStyle.Bold);
            }
        }

        public static RaceHud Create(RaceView view, string title)
        {
            var canvas = UiFactory.CreateCanvas("RaceHud", 10);
            var hud = canvas.gameObject.AddComponent<RaceHud>();
            hud._view = view;
            hud._root = UiFactory.SafeRoot(canvas);
            RaceUi.HostSheets(canvas.gameObject, hud._root);
            hud.Build(title);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugRace.Attach(hud._root, view);
#endif
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

        /// <summary>A tower label: shrinks to fit (never below 18) and never vanishes when the row is short.</summary>
        private static Text TowerLabel(string name, Transform parent, int size, TextAnchor align, Color color)
        {
            var t = UiFactory.Label(name, parent, "", size, align, color);
            t.supportRichText = true;
            Ui.Fit(t, size);
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>A rect between two width fractions, <paramref name="top"/> units under the top and <paramref name="bottom"/> over the bottom.</summary>
        private static RectTransform Region(Transform parent, string name, float x0, float x1, float top, float bottom)
        {
            var rt = UiFactory.Rect(name, parent);
            rt.anchorMin = new Vector2(x0, 0);
            rt.anchorMax = new Vector2(x1, 1);
            rt.offsetMin = new Vector2(0, bottom);
            rt.offsetMax = new Vector2(0, -top);
            return rt;
        }

        private static Segment MakeSegment(Transform parent, string label, System.Action onTap, float x0, float x1)
        {
            var b = UiFactory.Button("Segment", parent, label, () => onTap(), Color.Lerp(Theme.Background, Theme.Panel, 0.85f), Theme.TextBody);
            UiFactory.Place((RectTransform)b.transform, x0, 0, x1, 1, 3);
            var t = b.GetComponentInChildren<Text>();
            UiFactory.Style(t, FontStyle.Bold);
            Ui.Fit(t, Theme.TextBody);
            var line = UiFactory.Decor("Underline", b.transform, Theme.Accent);
            line.rectTransform.anchorMin = new Vector2(0, 0);
            line.rectTransform.anchorMax = new Vector2(1, 0);
            line.rectTransform.offsetMin = new Vector2(22, 3);
            line.rectTransform.offsetMax = new Vector2(-22, 6);
            var s = new Segment { Fill = b.image, Underline = line, Label = t };
            s.Set(false);
            return s;
        }

        private void Build(string title)
        {
            var sim = _view.Sim;

            // Name tags under everything else.
            // On the whole screen (they follow the riders), behind the HUD that stays in the safe area.
            _tagLayer = UiFactory.Rect("Tags", _root.parent);
            UiFactory.Stretch(_tagLayer);
            _tagLayer.SetAsFirstSibling();
            foreach (var r in sim.Riders)
            {
                // Plate over the bike (close view): position, name, tyre and gap.
                var plate = UiFactory.Panel("Plate", _tagLayer, new Color(0.93f, 0.95f, 0.97f, 0.95f));
                plate.rectTransform.sizeDelta = new Vector2(330, 38);
                plate.rectTransform.anchorMin = plate.rectTransform.anchorMax = Vector2.zero;
                plate.rectTransform.pivot = new Vector2(0.5f, 0);
                plate.raycastTarget = false;
                var bar = UiFactory.Panel("Team", plate.transform, RaceUi.TeamColor(r.Entry.Team != null ? r.Entry.Team.Colors.Primary : null));
                bar.raycastTarget = false;
                UiFactory.Place(bar.rectTransform, 0.1f, 0.1f, 0.115f, 0.9f);
                var t = UiFactory.Label("Text", plate.transform, "", Theme.TextCaption, TextAnchor.MiddleLeft, new Color32(20, 28, 44, 255));
                t.supportRichText = true;
                UiFactory.Place(t.rectTransform, 0.02f, 0, 0.99f, 1);
                _plates.Add(plate);
                _tags.Add(t);
                // Number inside the far-view dot.
                var n = UiFactory.Label("DotNumber", _tagLayer, "", Theme.FontSmall, TextAnchor.MiddleCenter, Color.white);
                UiFactory.Style(n, FontStyle.Bold);
                n.rectTransform.sizeDelta = new Vector2(48, 30);
                n.rectTransform.anchorMin = n.rectTransform.anchorMax = Vector2.zero;
                n.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
                _dotNumbers.Add(n);
            }

            BuildTopBar(title);
            BuildTower(sim);

            // Camera modes (over the riders' cards): circuit, follow the selected rider, fixed sector views.
            var cams = Region(_root, "Cameras", CardLeft, 0.995f, TopH + Gap, 0);
            cams.anchorMin = new Vector2(CardLeft, 1);
            cams.offsetMin = new Vector2(0, -TopH - Gap - StripH);
            _camButtons.Add(MakeSegment(cams, Theme.Caps(Loc.T("race.cam.circuit")), () => _view.Cam.SetMode(CameraMode.Overview), 0, 1 / 3f));
            _camButtons.Add(MakeSegment(cams, Theme.Caps(Loc.T("race.cam.follow")), () => _view.Select(_view.SelectedRider), 1 / 3f, 2 / 3f));
            _camButtons.Add(MakeSegment(cams, Theme.Caps(Loc.T("race.tower.sector")), () => _view.NextSectorView(), 2 / 3f, 1));

            // Event feed, bottom-centre between the tower and the cards.
            var feedBg = UiFactory.Panel("Feed", _root, Theme.Alpha(Theme.Panel, 0.72f));
            _feedRect = feedBg.rectTransform;
            _feedRect.anchorMin = new Vector2(TowerRight + 0.01f, 0);
            _feedRect.anchorMax = new Vector2(CardLeft - 0.01f, 0);
            _feedRect.pivot = new Vector2(0.5f, 0);
            _feedRect.offsetMin = new Vector2(0, Gap);
            _feedRect.offsetMax = new Vector2(0, Gap + 104);
            feedBg.raycastTarget = false;
            feedBg.gameObject.AddComponent<RectMask2D>();
            _feed = UiFactory.Label("FeedText", feedBg.transform, "", Theme.TextCaption, TextAnchor.LowerLeft);
            _feed.verticalOverflow = VerticalWrapMode.Overflow;
            _feed.supportRichText = true;
            UiFactory.Stretch(_feed.rectTransform);
            _feed.rectTransform.offsetMin = new Vector2(14, 6);
            _feed.rectTransform.offsetMax = new Vector2(-14, -6);

            // Pit-wall cards for the player's riders, stacked on the right (the first one on top).
            var players = new List<RiderRaceState>();
            foreach (var r in sim.Riders) if (r.Entry.IsPlayer) players.Add(r);
            _commandArea = Region(_root, "Commands", CardLeft, 0.995f, TopH + Gap + StripH + Gap, Gap);
            if (players.Count > 0)
            {
                float share = players.Count == 1 ? 0.62f : 1f / players.Count;
                for (int i = 0; i < players.Count; i++)
                {
                    var area = UiFactory.Rect("Command" + i, _commandArea);
                    float top = 1 - share * i;
                    UiFactory.Place(area, 0, top - share, 1, top);
                    area.offsetMin = new Vector2(0, i < players.Count - 1 || players.Count == 1 ? Gap / 2 : 0);
                    area.offsetMax = new Vector2(0, i > 0 ? -Gap / 2 : 0);
                    _panels.Add(new RiderCommandPanel(_view, players[i], area));
                }
                var session = GameSession.Instance;
                if (session != null && session.Game != null && session.Game.World.PlayerTeam != null)
                    _forecasterSkill = Core.Economy.TeamCapabilities.ForecastSkill(session.Game.World, session.Game.World.PlayerTeam);
            }

            // Race-day banner shown while the camera presents the circuit.
            _bannerBg = UiFactory.Panel("Banner", _root, new Color(0, 0, 0, 0));
            _bannerBg.raycastTarget = false;
            UiFactory.Place(_bannerBg.rectTransform, 0.03f, 0.16f, 0.7f, 0.4f);
            var tag = UiFactory.Panel("RaceDay", _bannerBg.transform, Theme.Accent);
            tag.raycastTarget = false;
            UiFactory.Slant(tag, 1.6f);
            UiFactory.Place(tag.rectTransform, 0, 0.6f, 0.42f, 0.95f);
            var tagText = RaceUi.Label(tag.transform, Theme.Caps(Loc.T("race.day")), Theme.FontNormal, TextAnchor.MiddleCenter, Theme.OnAccent, FontStyle.BoldAndItalic);
            UiFactory.Stretch(tagText.rectTransform);
            _banner = RaceUi.Label(_bannerBg.transform, "", Theme.FontTitle - 6, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
            _banner.resizeTextMinSize = Theme.FontNormal;
            _banner.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            UiFactory.Place(_banner.rectTransform, 0, 0.2f, 1, 0.6f);
            var session2 = RaceUi.Label(_bannerBg.transform, Theme.Caps(title), Theme.TextHeader, TextAnchor.UpperLeft, Theme.Text, FontStyle.Bold);
            session2.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            UiFactory.Place(session2.rectTransform, 0, 0, 1, 0.2f);
            _bannerBg.gameObject.SetActive(false);

            RefreshTower();
        }

        /// <summary>One line: session, lap, flag, conditions, then the speed segments and skip.</summary>
        private void BuildTopBar(string title)
        {
            var top = UiFactory.Panel("TopBar", _root, Theme.Alpha(Theme.Panel, 0.9f));
            var rt = top.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(0, -TopH);
            rt.offsetMax = Vector2.zero;
            var topLine = UiFactory.Decor("AccentLine", rt, Theme.Alpha(Theme.Accent, 0.7f));
            topLine.rectTransform.anchorMin = new Vector2(0, 0);
            topLine.rectTransform.anchorMax = new Vector2(1, 0);
            topLine.rectTransform.offsetMin = Vector2.zero;
            topLine.rectTransform.offsetMax = new Vector2(0, 2);
            UiFactory.Gradient(topLine, Color.white, new Color(1, 1, 1, 0.05f), true);

            var titleText = RaceUi.Label(rt, Theme.Caps(title), Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(titleText.rectTransform, 0.01f, 0, 0.15f, 1);
            _lapText = RaceUi.Label(rt, "", Theme.TextTitle, TextAnchor.MiddleCenter, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.DropShadow(_lapText, 0.4f, 1.5f);
            UiFactory.Place(_lapText.rectTransform, 0.15f, 0, 0.285f, 1);
            _flagBg = UiFactory.Decor("Flag", rt, new Color(0, 0, 0, 0));
            UiFactory.Slant(_flagBg, 3f);
            UiFactory.Place(_flagBg.rectTransform, 0.29f, 0.2f, 0.37f, 0.8f);
            _flag = RaceUi.Label(_flagBg.transform, "", Theme.TextCaption, TextAnchor.MiddleCenter, Theme.Ink, FontStyle.Bold);
            UiFactory.Stretch(_flag.rectTransform);
            _flag.rectTransform.offsetMin = new Vector2(10, 0);
            _flag.rectTransform.offsetMax = new Vector2(-10, 0);
            _conditions = RaceUi.Label(rt, "", Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(_conditions.rectTransform, 0.378f, 0, 0.625f, 1);

            // Speed controls: pause and 1x-8x as a segmented row (the active one lit), then skip to the end.
            var speeds = UiFactory.Rect("Speeds", rt);
            UiFactory.Place(speeds, 0.63f, 0.1f, 0.93f, 0.9f);
            string[] labels = { "II", "1x", "2x", "4x", "8x" };
            for (int i = 0; i < labels.Length && i < RaceView.Speeds.Length; i++)
            {
                float speed = RaceView.Speeds[i];
                _speedButtons.Add(MakeSegment(speeds, labels[i], () => _view.SetSpeed(speed), (float)i / labels.Length, (float)(i + 1) / labels.Length));
            }
            var skipArea = UiFactory.Rect("SkipArea", rt);
            UiFactory.Place(skipArea, 0.935f, 0.1f, 0.995f, 0.9f);
            var skip = Ui.Secondary(skipArea, ">>|", () => _view.FinishInstantly());
            skip.gameObject.name = "Skip";
        }

        /// <summary>
        /// Timing tower, always on the left: a header with the gap mode, then fixed 40-unit rows (a finger can tap
        /// them, the text always fits) that scroll when the field is longer than the screen.
        /// </summary>
        private void BuildTower(RaceSimulation sim)
        {
            var tower = UiFactory.Panel("Tower", _root, Theme.Alpha(Theme.Panel, 0.9f));
            UiFactory.Chamfer(tower, 3f);
            _tower = Region(_root, "TowerArea", 0.005f, TowerRight, TopH + Gap, Gap);
            tower.rectTransform.SetParent(_tower, false);
            UiFactory.Stretch(tower.rectTransform);
            var head = Ui.HeaderStrip(tower.transform, Loc.T("race.standings"));
            var title = head.Find("Title") as RectTransform;
            if (title != null) title.offsetMax = new Vector2(-170, 0);
            try { _gapToAhead = PlayerPrefs.GetInt(GapPref, 0) == 1; } catch (System.Exception) { }
            var gapArea = RaceUi.RightSlot(head, 160, 6, 6);
            var gapButton = Ui.Secondary(gapArea, "", () =>
            {
                _gapToAhead = !_gapToAhead;
                try { PlayerPrefs.SetInt(GapPref, _gapToAhead ? 1 : 0); } catch (System.Exception) { }
                RefreshTower();
            });
            gapButton.gameObject.name = "GapMode";
            _gapLabel = gapButton.GetComponentInChildren<Text>();
            Ui.Fit(_gapLabel, Theme.TextCaption);

            var viewport = UiFactory.Panel("Viewport", tower.transform, new Color(0, 0, 0, 0));
            UiFactory.Stretch(viewport.rectTransform);
            viewport.rectTransform.offsetMax = new Vector2(0, -Ui.Grid.Header - 2);
            viewport.rectTransform.offsetMin = new Vector2(0, 4);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiFactory.Rect("Content", viewport.transform);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = new Vector2(0, -TowerRowH * sim.Riders.Count);
            content.offsetMax = Vector2.zero;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            for (int i = 0; i < sim.Riders.Count; i++)
            {
                var row = new TowerRow();
                var bg = UiFactory.Panel("Row", content, new Color(1, 1, 1, 0.0f));
                row.Background = bg;
                row.Rect = bg.rectTransform;
                row.Rect.anchorMin = new Vector2(0, 1);
                row.Rect.anchorMax = new Vector2(1, 1);
                row.Rect.offsetMin = new Vector2(0, -TowerRowH * (i + 1));
                row.Rect.offsetMax = new Vector2(0, -TowerRowH * i);
                var button = bg.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                int index = i;
                button.onClick.AddListener(() => OnRowClicked(index));
                row.Edge = UiFactory.SideStripe(row.Rect, Theme.Accent, 4, 0);
                // Position number in a light block with dark type (TV timing tower).
                var posBlock = UiFactory.Decor("PosBlock", row.Rect, Theme.Plate);
                UiFactory.Slant(posBlock, 4f);
                UiFactory.Place(posBlock.rectTransform, 0.02f, 0.1f, 0.15f, 0.9f);
                row.Position = TowerLabel("Pos", posBlock.transform, Theme.TextBody - 2, TextAnchor.MiddleCenter, Theme.Ink);
                UiFactory.Style(row.Position, FontStyle.BoldAndItalic);
                UiFactory.Stretch(row.Position.rectTransform);
                row.PosBlock = posBlock;
                row.TeamBar = UiFactory.Decor("Team", row.Rect, Color.white);
                UiFactory.Place(row.TeamBar.rectTransform, 0.165f, 0.2f, 0.178f, 0.8f);
                row.Name = TowerLabel("Name", row.Rect, Theme.TextBody - 2, TextAnchor.MiddleLeft, Theme.Text);
                UiFactory.Style(row.Name, FontStyle.Bold);
                UiFactory.Place(row.Name.rectTransform, 0.195f, 0, 0.6f, 1);
                row.Gap = TowerLabel("Gap", row.Rect, Theme.TextCaption, TextAnchor.MiddleRight, Theme.TextDim);
                UiFactory.Place(row.Gap.rectTransform, 0.6f, 0, 0.86f, 1);
                row.Tires = TowerLabel("Tires", row.Rect, Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim);
                UiFactory.Place(row.Tires.rectTransform, 0.86f, 0, 1, 1);
                _rows.Add(row);
            }
        }

        /// <summary>Banner and HUD state while the camera presents the circuit before the start.</summary>
        public void ShowIntro(string circuit, string country, bool on)
        {
            _bannerBg.gameObject.SetActive(on);
            _banner.text = (string.IsNullOrEmpty(country) ? "" : country.ToUpperInvariant() + " · ") + circuit.ToUpperInvariant();
            _commandArea.gameObject.SetActive(!on);
            _tagLayer.gameObject.SetActive(!on);
            // The standings appear with the grid, not over the circuit presentation.
            _tower.gameObject.SetActive(!on);
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
                    string color = m.Severity >= 2 ? Theme.BadHex : m.Severity == 1 ? Theme.WarnHex : Theme.InfoHex;
                    _engineerLines.Add("<color=" + color + "><b>" + Loc.T("feed.engineer") + "</b></color>  " + m.Text);
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
            _conditions.text = Theme.Caps(Loc.T("ux.race.cond", Loc.Or("weather." + w.Condition, w.Condition.ToString()), w.TrackTemp.ToString("0"), (w.TrackWetness * 100).ToString("0")) +
                               (_forecast >= 0 ? "  ·  " + Loc.T("ux.race.condRain", (_forecast * 100).ToString("0")) : ""));
            UpdateFlag(sim);

            for (int i = 0; i < _speedButtons.Count; i++)
                _speedButtons[i].Set(Mathf.Approximately(RaceView.Speeds[i], _view.Speed));
            var mode = _view.Cam.Mode;
            if (_camButtons.Count == 3)
            {
                _camButtons[0].Set(mode == CameraMode.Overview);
                _camButtons[1].Set(mode == CameraMode.Follow);
                _camButtons[2].Set(mode == CameraMode.Sector);
            }

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
            else if (sim.WhiteFlag) { text = Loc.T("flag.white"); color = new Color(0.92f, 0.92f, 0.92f); }
            else
            {
                foreach (var r in sim.Riders)
                    if (r.Status == RaceStatus.Crashed) { text = Loc.T("flag.yellow"); color = Theme.Warning; break; }
            }
            if (text == "" && sim.Tick > 0) { text = Loc.T("flag.green"); color = Theme.Good; }
            _flag.text = text;
            _flag.color = Theme.OnColor(color);
            _flagBg.color = color;
        }

        private void RefreshTower()
        {
            if (_gapLabel != null) _gapLabel.text = Theme.Caps(Loc.T(_gapToAhead ? "race.gap.interval" : "race.gap.leader"));
            var sim = _view.Sim;
            for (int p = 0; p < _rows.Count; p++)
            {
                var row = _rows[p];
                var r = sim.RiderAtPosition(p + 1);
                if (r == null) continue;
                var team = r.Entry.Team;
                bool mine = r.Entry.IsPlayer;
                bool selected = r.Index == _view.SelectedRider;
                row.Position.text = (p + 1).ToString();
                row.TeamBar.color = RaceUi.TeamColor(team != null ? team.Colors.Primary : null);
                row.Name.text = r.Entry.Rider.ShortName.ToUpperInvariant() + (r.PendingLongLaps > 0 ? " " + Theme.WarnText("LL") : "");
                row.Name.color = Theme.Text;
                row.Gap.text = GapText(sim, r, p);
                row.Tires.text = CompoundLetter(r.Front.Compound) + "/" + CompoundLetter(r.Rear.Compound);
                row.Tires.color = mine ? Theme.Text : Theme.TextDim;
                // Alternate rows lightly shaded; the player's riders in the accent; the followed rider lit; the leader's block in timing yellow.
                if (mine) row.Background.color = Theme.Alpha(Theme.Accent, selected ? 0.38f : 0.24f);
                else row.Background.color = selected ? new Color(1, 1, 1, 0.14f) : new Color(1, 1, 1, p % 2 == 1 ? 0.035f : 0f);
                if (row.Edge.enabled != mine) row.Edge.enabled = mine;
                row.PosBlock.color = p == 0 ? Theme.Timing : Theme.Plate;
            }
        }

        private static string GapText(RaceSimulation sim, RiderRaceState r, int position)
        {
            switch (r.Status)
            {
                case RaceStatus.Crashed: return r.RemountAt > 0 ? Theme.WarnText(Loc.T("tower.crash")) : Theme.BadText(Loc.T("tower.out"));
                case RaceStatus.Retired: return Theme.BadText(Loc.T("tower.dnf"));
                case RaceStatus.Disqualified: return "DSQ";
                case RaceStatus.InPitLane: return "<color=" + Theme.InfoHex + ">" + Loc.T("tower.pit") + "</color>";
            }
            if (position == 0) return r.Status == RaceStatus.Finished ? MathX.FormatLapTime(r.FinishTime) : Theme.Caps(Loc.T("tower.leader"));
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
                lines.Insert(0, Theme.Dim(Loc.T("feed.lap", e.Lap)) + "  " + e.Detail);
            }
            for (int i = System.Math.Max(0, _engineerLines.Count - 2); i < _engineerLines.Count; i++) lines.Add(_engineerLines[i]);
            _feed.text = string.Join("\n", lines.ToArray());
        }

        private void UpdateTags()
        {
            var cam = _view.Cam.Camera;
            if (cam == null) return;
            var sim = _view.Sim;
            var canvasRect = ((RectTransform)_tagLayer.parent).rect;
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
                _cameraLabel = RaceUi.Label(_root, "", Theme.FontLarge, TextAnchor.MiddleCenter, Theme.Text, FontStyle.BoldAndItalic);
                UiFactory.Place(_cameraLabel.rectTransform, 0.35f, 0.78f, 0.65f, 0.88f);
                _cameraLabel.gameObject.AddComponent<Outline>().effectColor = Color.black;
            }
            _cameraLabel.text = Theme.Caps(text);
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
                // Bottom right: the replay label, a lap back / forward and the way back to the results.
                var bar = UiFactory.Panel("Replay", _root, Theme.Alpha(Theme.Panel, 0.92f));
                UiFactory.Chamfer(bar, 3f);
                _replayBar = bar.rectTransform;
                _replayBar.anchorMin = new Vector2(0.6f, 0);
                _replayBar.anchorMax = new Vector2(0.995f, 0);
                _replayBar.pivot = new Vector2(0.5f, 0);
                _replayBar.offsetMin = new Vector2(0, Gap);
                _replayBar.offsetMax = new Vector2(0, Gap + 124);
                var label = RaceUi.Label(_replayBar, Theme.Caps(Loc.T("replay.title")), Theme.TextHeader, TextAnchor.MiddleLeft, Theme.Warning, FontStyle.BoldAndItalic);
                label.rectTransform.anchorMin = new Vector2(0, 1);
                label.rectTransform.anchorMax = new Vector2(1, 1);
                label.rectTransform.pivot = new Vector2(0.5f, 1);
                label.rectTransform.offsetMin = new Vector2(16, -40);
                label.rectTransform.offsetMax = new Vector2(-16, -4);
                var buttons = UiFactory.Rect("Buttons", _replayBar);
                UiFactory.Stretch(buttons);
                buttons.offsetMin = new Vector2(12, 10);
                buttons.offsetMax = new Vector2(-12, -44);
                var cols = Ui.Columns12(buttons, 3, 3, 6);
                Ui.Secondary(cols[0], Loc.T("replay.back"), () => _view.SeekLaps(-1));
                Ui.Secondary(cols[1], Loc.T("replay.fwd"), () => _view.SeekLaps(1));
                Ui.PrimaryCTA(cols[2], Loc.T("replay.results"), () => _view.ExitReplay());
            }
            _replayBar.gameObject.SetActive(true);
            // The feed keeps clear of the replay bar.
            _feedRect.anchorMax = new Vector2(0.59f, 0);
            _feedCount = -1;
        }

        public void ExitReplay()
        {
            if (_replayBar != null) _replayBar.gameObject.SetActive(false);
            _feedRect.anchorMax = new Vector2(CardLeft - 0.01f, 0);
            _commandArea.gameObject.SetActive(true);
            if (_results != null) _results.gameObject.SetActive(true);
            _feedCount = -1;
        }
    }
}
