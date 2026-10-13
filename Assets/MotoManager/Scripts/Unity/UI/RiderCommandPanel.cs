using System;
using MotoManager.App.Race;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Pit-wall card for one of the player's riders (stacked on the right of the race screen): number, name,
    /// position and status in the header; the riders just ahead and behind with the gaps; front and rear tyre wear;
    /// last and best lap; fuel in laps against the laps left (only where there is a tank limit); and the two orders
    /// that matter during a race (pace and attitude) as always-visible segmented rows, plus handing the rider over
    /// to the engineer. A manual order holds until "leave it to the engineer".
    /// There is no pit call: in bike racing riders only come in on their own for a flag-to-flag swap or a penalty.
    /// </summary>
    public class RiderCommandPanel
    {
        private readonly RaceView _view;
        private readonly RiderRaceState _rider;
        private readonly Text[] _rowPos = new Text[2];
        private readonly Text[] _rowName = new Text[2];
        private readonly Text[] _rowTyre = new Text[2];
        private readonly Text[] _rowGap = new Text[2];
        private readonly Image[] _rowBar = new Image[2];
        private readonly RectTransform[] _rowRect = new RectTransform[2];
        private readonly Text _position;
        private readonly Text _name;
        private readonly Text _front;
        private readonly Image _frontBar;
        private readonly Text _rear;
        private readonly Image _rearBar;
        private readonly Text _fuel;
        private readonly Image _fuelBar;
        private readonly Text _laps;
        private readonly Image _statusPill;
        private readonly Text _statusText;
        private readonly Choice[] _pace = new Choice[3];
        private readonly Choice[] _mode = new Choice[3];
        private readonly Button _auto;
        private readonly Text _autoText;

        private static readonly PaceMode[] Paces = { PaceMode.Conserve, PaceMode.Balanced, PaceMode.Push };
        private static readonly RiderMode[] Modes = { RiderMode.Defend, RiderMode.Balanced, RiderMode.Attack };

        /// <summary>One option of a segmented order row.</summary>
        private struct Choice
        {
            public Image Fill;
            public Image Underline;
            public Text Label;
        }

        public RiderCommandPanel(RaceView view, RiderRaceState rider, RectTransform parent)
        {
            _view = view;
            _rider = rider;
            var team = rider.Entry.Team;
            var teamColor = RaceUi.TeamColor(team != null ? team.Colors.Primary : null);
            bool fuel = view.Sim.Config.FuelLimitLitres > 0;

            var bg = UiFactory.Panel("Rider " + rider.Entry.Rider.Code, parent, Theme.Alpha(Theme.Panel, 0.93f));
            UiFactory.Chamfer(bg, 3f);
            UiFactory.Stretch(bg.rectTransform);
            UiFactory.SideStripe(bg.transform, teamColor, 5, 0);
            var root = UiFactory.Rect("Body", bg.transform);
            UiFactory.Stretch(root);
            root.offsetMin = new Vector2(12, 6);
            root.offsetMax = new Vector2(-10, -4);

            // Vertical bands (fractions of the card) so one or two stacked cards both fit.
            float headTop = 1, headBottom = 0.85f;
            float rowsBottom = fuel ? 0.69f : 0.67f;
            float tyresBottom = fuel ? 0.6f : 0.565f;
            float lapsBottom = fuel ? 0.52f : 0.475f;
            float fuelBottom = 0.445f;
            float ordersTop = fuel ? 0.44f : 0.465f;

            // Header: number plate, name, status pill, position.
            var head = UiFactory.Rect("Head", root);
            UiFactory.Place(head, 0, headBottom, 1, headTop);
            var plateArea = UiFactory.Rect("PlateArea", head);
            plateArea.anchorMin = new Vector2(0, 0.06f);
            plateArea.anchorMax = new Vector2(0, 0.94f);
            plateArea.pivot = new Vector2(0, 0.5f);
            plateArea.offsetMin = Vector2.zero;
            plateArea.offsetMax = new Vector2(64, 0);
            RaceUi.Plate(plateArea, rider.Entry.Rider.Number, teamColor, Theme.TextTitle);
            var name = _name = RaceUi.Label(head, Theme.Caps(rider.Entry.Rider.ShortName), Theme.TextHeader, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Place(name.rectTransform, 0, 0, 0.76f, 1);
            name.rectTransform.offsetMin = new Vector2(76, 0);
            _statusPill = UiFactory.Decor("Status", head, Theme.Warning);
            UiFactory.Slant(_statusPill, 3f);
            UiFactory.Place(_statusPill.rectTransform, 0.36f, 0.14f, 0.76f, 0.86f);
            _statusText = RaceUi.Label(_statusPill.transform, "", Theme.TextCaption, TextAnchor.MiddleCenter, Theme.Ink, FontStyle.Bold);
            UiFactory.Stretch(_statusText.rectTransform);
            _statusText.rectTransform.offsetMin = new Vector2(10, 0);
            _statusText.rectTransform.offsetMax = new Vector2(-10, 0);
            _position = RaceUi.Label(head, "", Theme.TextTitle, TextAnchor.MiddleRight, Theme.Timing, FontStyle.BoldAndItalic);
            UiFactory.Place(_position.rectTransform, 0.76f, 0, 1, 1);
            _position.rectTransform.offsetMax = new Vector2(-4, 0);

            // The riders just ahead and behind, with the gap to this rider (thousandths).
            for (int i = 0; i < 2; i++)
            {
                float h = (headBottom - rowsBottom) / 2;
                float y1 = headBottom - i * h, y0 = y1 - h;
                var row = UiFactory.Rect(i == 0 ? "Ahead" : "Behind", root);
                UiFactory.Place(row, 0, y0, 1, y1);
                _rowRect[i] = row;
                var shade = UiFactory.Decor("Shade", row, new Color(1, 1, 1, i == 0 ? 0.05f : 0.025f));
                UiFactory.Stretch(shade.rectTransform);
                _rowPos[i] = RaceUi.Label(row, "", Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.BoldAndItalic);
                UiFactory.Place(_rowPos[i].rectTransform, 0, 0, 0.14f, 1);
                _rowBar[i] = UiFactory.Decor("Team", row, Color.white);
                UiFactory.Place(_rowBar[i].rectTransform, 0.15f, 0.2f, 0.165f, 0.8f);
                _rowName[i] = RaceUi.Label(row, "", Theme.TextCaption, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
                UiFactory.Place(_rowName[i].rectTransform, 0.19f, 0, 0.6f, 1);
                _rowTyre[i] = RaceUi.Label(row, "", Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim);
                UiFactory.Place(_rowTyre[i].rectTransform, 0.6f, 0, 0.7f, 1);
                _rowGap[i] = RaceUi.Label(row, "", Theme.TextCaption, TextAnchor.MiddleRight, Theme.Text, FontStyle.Bold);
                UiFactory.Place(_rowGap[i].rectTransform, 0.7f, 0, 1, 1);
                _rowGap[i].rectTransform.offsetMax = new Vector2(-6, 0);
            }

            // Front and rear tyre side by side: compound, how much is left, a bar.
            var tyres = UiFactory.Rect("Tyres", root);
            UiFactory.Place(tyres, 0, tyresBottom, 1, rowsBottom - 0.01f);
            _front = Gauge(tyres, 0, 0.49f, out _frontBar);
            _rear = Gauge(tyres, 0.51f, 1, out _rearBar);

            // Last and best lap (the best in purple when it is the fastest of the race).
            _laps = RaceUi.Label(root, "", Theme.TextCaption, TextAnchor.MiddleLeft, Theme.Text);
            UiFactory.Place(_laps.rectTransform, 0, lapsBottom, 1, tyresBottom);

            // Fuel in laps against the laps left, only with a tank limit.
            var fuelRow = UiFactory.Rect("Fuel", root);
            UiFactory.Place(fuelRow, 0, fuelBottom, 1, lapsBottom);
            _fuel = Gauge(fuelRow, 0, 1, out _fuelBar);
            fuelRow.gameObject.SetActive(fuel);

            // Orders: pace and attitude, state always visible; then the engineer.
            float orderH = (ordersTop - 0.16f) / 2;
            BuildChoices(root, Loc.T("ux.race.pace"), ordersTop - orderH, ordersTop, _pace,
                new[] { Loc.T("ux.race.pace.save"), Loc.T("ux.race.pace.normal"), Loc.T("ux.race.pace.push") },
                i => Send(new RaceCommand { Type = RaceCommandType.SetPace, Pace = Paces[i] }));
            BuildChoices(root, Loc.T("ux.race.mode"), ordersTop - 2 * orderH, ordersTop - orderH, _mode,
                new[] { Loc.T("ux.race.mode.defend"), Loc.T("ux.race.mode.normal"), Loc.T("ux.race.mode.attack") },
                i => Send(new RaceCommand { Type = RaceCommandType.SetRiderMode, Mode = Modes[i] }));
            var autoArea = UiFactory.Rect("AutoArea", root);
            UiFactory.Place(autoArea, 0, 0, 1, 0.15f);
            autoArea.offsetMin = new Vector2(0, 2);
            _auto = Ui.Secondary(autoArea, Loc.T("cmd.auto"), () => Send(new RaceCommand { Type = RaceCommandType.SetAutoManaged, Flag = !_rider.Entry.AutoManaged }));
            _autoText = _auto.GetComponentInChildren<Text>();
        }

        /// <summary>A label ("DEL. M 82%") over a thin bar in the left/right part of <paramref name="area"/>.</summary>
        private static Text Gauge(RectTransform area, float x0, float x1, out Image fill)
        {
            var t = RaceUi.Label(area, "", Theme.TextCaption, TextAnchor.MiddleLeft, Theme.Text);
            UiFactory.Place(t.rectTransform, x0, 0.3f, x1, 1);
            var back = UiFactory.Decor("BarBack", area, new Color(1, 1, 1, 0.1f));
            UiFactory.Place(back.rectTransform, x0, 0.04f, x1, 0.24f);
            back.rectTransform.offsetMax = new Vector2(-4, 0);
            fill = UiFactory.Decor("Fill", back.transform, Theme.Good);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(1, 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            return t;
        }

        /// <summary>Caption on the left, three segments: the active one lit with an accent underline.</summary>
        private static void BuildChoices(RectTransform root, string caption, float y0, float y1, Choice[] into, string[] labels, Action<int> onPick)
        {
            var row = UiFactory.Rect("Order", root);
            UiFactory.Place(row, 0, y0, 1, y1);
            row.offsetMin = new Vector2(0, 2);
            row.offsetMax = new Vector2(0, -2);
            var cap = RaceUi.Label(row, Theme.Caps(caption), Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(cap.rectTransform, 0, 0, 0.22f, 1);
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                float x0 = 0.22f + 0.26f * i, x1 = x0 + 0.26f;
                var b = UiFactory.Button("Option", row, Theme.Caps(labels[i]), () => onPick(idx), Color.Lerp(Theme.Background, Theme.Panel, 0.85f), Theme.TextCaption);
                UiFactory.Place((RectTransform)b.transform, x0, 0, x1, 1, 2);
                var t = b.GetComponentInChildren<Text>();
                UiFactory.Style(t, FontStyle.Bold);
                Ui.Fit(t, Theme.TextCaption);
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.rectTransform.offsetMin = new Vector2(10, 0);
                t.rectTransform.offsetMax = new Vector2(-10, 0);
                var line = UiFactory.Decor("Underline", b.transform, Theme.Accent);
                line.rectTransform.anchorMin = new Vector2(0.25f, 0);
                line.rectTransform.anchorMax = new Vector2(0.75f, 0);
                line.rectTransform.offsetMin = new Vector2(0, 3);
                line.rectTransform.offsetMax = new Vector2(0, 7);
                into[i] = new Choice { Fill = b.image, Underline = line, Label = t };
            }
        }

        /// <summary>Lights the active option: accent underline for the player's order, steel when the engineer decides.</summary>
        private static void SetChoices(Choice[] row, int active, bool auto)
        {
            for (int i = 0; i < row.Length; i++)
            {
                bool on = i == active;
                row[i].Fill.color = Theme.Alpha(Theme.Chrome, on ? 0.92f : 0.75f);
                row[i].Underline.enabled = on;
                row[i].Underline.color = auto ? Theme.Steel : Theme.Accent;
                row[i].Label.color = on ? Theme.Text : Theme.TextDim;
            }
        }

        private void Send(RaceCommand command)
        {
            command.RiderIndex = _rider.Index;
            _view.Sim.Enqueue(command);
        }

        private static Color WearColor(TireState t)
        {
            switch (t.Condition)
            {
                case TireCondition.Cold: return new Color32(120, 178, 214, 255);
                case TireCondition.Hot: return Theme.Warning;
                case TireCondition.Overheated: return Theme.Bad;
                case TireCondition.Worn: return new Color32(200, 120, 60, 255);
                default: return Theme.Good;
            }
        }

        private void Row(int i, RiderRaceState r, RiderRaceState self)
        {
            bool show = r != null;
            if (_rowRect[i].gameObject.activeSelf != show) _rowRect[i].gameObject.SetActive(show);
            if (!show) return;
            var team = r.Entry.Team;
            _rowPos[i].text = "P" + r.Position;
            _rowBar[i].color = RaceUi.TeamColor(team != null ? team.Colors.Primary : null);
            _rowName[i].text = r.Entry.Rider.ShortName.ToUpperInvariant();
            _rowTyre[i].text = RaceHud.CompoundLetter(r.Rear.Compound);
            // Gap relative to this rider: negative = ahead.
            double gap = i == 0 ? -self.GapToAhead : r.GapToAhead;
            _rowGap[i].text = r.Status == RaceStatus.Racing && self.Status == RaceStatus.Racing ? gap.ToString("+0.000;-0.000", Loc.Culture) : "";
        }

        public void Refresh()
        {
            var r = _rider;
            var sim = _view.Sim;
            _position.text = Theme.Pos(r.Position);
            Row(0, r.Position > 1 ? sim.RiderAtPosition(r.Position - 1) : null, r);
            Row(1, sim.RiderAtPosition(r.Position + 1), r);

            double rear = 1 - r.Rear.Wear;
            double front = 1 - r.Front.Wear;
            _front.text = Theme.Dim(Theme.Caps(Loc.T("card.front"))) + " <b>" + RaceHud.CompoundLetter(r.Front.Compound) + " " + (front * 100).ToString("0") + "%</b>";
            _frontBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)front), 1);
            _frontBar.color = WearColor(r.Front);
            _rear.text = Theme.Dim(Theme.Caps(Loc.T("card.rear"))) + " <b>" + RaceHud.CompoundLetter(r.Rear.Compound) + " " + (rear * 100).ToString("0") + "%</b>";
            _rearBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)rear), 1);
            _rearBar.color = WearColor(r.Rear);

            // Fuel in laps against the laps left.
            int lapsLeft = Math.Max(0, sim.TotalLaps - Math.Max(0, r.Lap));
            double limit = sim.Config.FuelLimitLitres;
            if (limit > 0 && r.Lap >= 1)
            {
                double perLap = (limit - r.Fuel) / Math.Max(0.5, r.Lap);
                double lapsOfFuel = perLap > 0 ? r.Fuel / perLap : 99;
                double delta = lapsOfFuel - lapsLeft;
                _fuel.text = Theme.Dim(Theme.Caps(Loc.T("ux.race.fuel"))) + " <b>" + Loc.T("card.fuelLaps", lapsOfFuel.ToString("0.0", Loc.Culture)) + "</b>  " +
                             (delta >= 0 ? Theme.GoodText(delta.ToString("+0.0", Loc.Culture)) : Theme.BadText(delta.ToString("0.0", Loc.Culture)));
                _fuelBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)(r.Fuel / limit)), 1);
                _fuelBar.color = delta >= 0 ? Theme.Good : Theme.Bad;
            }
            else
            {
                _fuel.text = limit > 0 ? Theme.Dim(Theme.Caps(Loc.T("ux.race.fuel"))) + " " + Loc.T("card.fuelFull") : "";
                _fuelBar.rectTransform.anchorMax = new Vector2(limit > 0 ? 1 : 0, 1);
            }

            string last = r.LastLapTime > 0 ? MathX.FormatLapTime(r.LastLapTime) : "-";
            string best = r.BestLapTime < double.MaxValue ? MathX.FormatLapTime(r.BestLapTime) : "-";
            bool fastest = r.BestLapTime < double.MaxValue && Math.Abs(r.BestLapTime - sim.FastestLap) < 0.0005;
            _laps.text = Theme.Dim(Theme.Caps(Loc.T("card.lastLap"))) + " <b>" + last + "</b>   " + Theme.Dim(Theme.Caps(Loc.T("card.bestLap"))) + " <b>" +
                         (fastest ? "<color=" + Theme.FastestHex + ">" + best + "</color>" : best) + "</b>";

            string status = r.HasIssue ? Loc.T("cmd.issue") : r.PendingLongLaps > 0 ? Loc.T("cmd.longLap", r.PendingLongLaps) :
                r.Status != RaceStatus.Racing ? Loc.T("race.status." + r.Status) : "";
            bool hasStatus = !string.IsNullOrEmpty(status);
            if (_statusPill.gameObject.activeSelf != hasStatus) _statusPill.gameObject.SetActive(hasStatus);
            _name.rectTransform.anchorMax = new Vector2(hasStatus ? 0.36f : 0.76f, 1);
            _statusText.text = Theme.Caps(status);
            _statusPill.color = r.Status == RaceStatus.Crashed || r.Status == RaceStatus.Retired || r.Status == RaceStatus.Disqualified ? Theme.Bad : Theme.Warning;
            _statusText.color = Theme.OnColor(_statusPill.color);

            bool auto = r.Entry.AutoManaged;
            SetChoices(_pace, Array.IndexOf(Paces, r.Pace), auto);
            SetChoices(_mode, Array.IndexOf(Modes, r.Mode), auto);
            _auto.image.color = auto ? Theme.Alpha(Theme.Good, 0.4f) : Theme.PanelLight;
            _autoText.text = Theme.Caps(Loc.T(auto ? "ux.race.engineerOn" : "cmd.auto"));
        }
    }
}
