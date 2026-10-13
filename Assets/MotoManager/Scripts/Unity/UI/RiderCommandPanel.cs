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
    /// Pit-wall card for one of the player's riders (stacked on the right of the race screen), kept compact: number,
    /// name, status and position in the header; one small line with the gaps to the riders ahead and behind (the
    /// tower shows who they are); front and rear tyre in one row (compound and life, a thin bar beside each); last and
    /// best lap on one line, with fuel only when a tank limit makes it matter; the two race orders as cycling
    /// controls ("PACE  NORMAL ›", "ATTITUDE  NORMAL ›"); and handing the rider over to the engineer as a text button.
    /// A manual order holds until "leave it to the engineer".
    /// There is no pit call: in bike racing riders only come in on their own for a flag-to-flag swap or a penalty.
    /// </summary>
    public class RiderCommandPanel
    {
        private readonly RaceView _view;
        private readonly RiderRaceState _rider;
        private readonly Text _position;
        private readonly Text _name;
        private readonly Text _gaps;
        private readonly Text _front;
        private readonly Image _frontBar;
        private readonly Text _rear;
        private readonly Image _rearBar;
        private readonly Text _laps;
        private readonly Text _fuel;
        private readonly Image _statusPill;
        private readonly Text _statusText;
        private readonly Cycle _pace;
        private readonly Cycle _mode;
        private readonly Button _auto;
        private readonly Text _autoText;

        private static readonly PaceMode[] Paces = { PaceMode.Conserve, PaceMode.Balanced, PaceMode.Push };
        private static readonly RiderMode[] Modes = { RiderMode.Defend, RiderMode.Balanced, RiderMode.Attack };

        /// <summary>A cycling order control: caption, the current value and a chevron; a tap moves to the next value.</summary>
        private class Cycle
        {
            public Text Value;
            public Image Mark;
            public string[] Labels;
        }

        public RiderCommandPanel(RaceView view, RiderRaceState rider, RectTransform parent)
        {
            _view = view;
            _rider = rider;
            var team = rider.Entry.Team;
            var teamColor = RaceUi.TeamColor(team != null ? team.Colors.Primary : null);

            var bg = UiFactory.Panel("Rider " + rider.Entry.Rider.Code, parent, Theme.Alpha(Theme.Chrome, 0.88f));
            UiFactory.Surface(bg);
            UiFactory.Stretch(bg.rectTransform);
            UiFactory.SideStripe(bg.transform, teamColor, 4, 0);
            var root = UiFactory.Rect("Body", bg.transform);
            UiFactory.Stretch(root);
            root.offsetMin = new Vector2(14, 6);
            root.offsetMax = new Vector2(-10, -4);

            // Vertical bands (fractions of the card) so one or two stacked cards both fit.
            const float headBottom = 0.8f, gapsBottom = 0.69f, tyresBottom = 0.57f, lapsBottom = 0.46f, ordersBottom = 0.15f;

            // Header: number plate, name, status pill, position.
            var head = UiFactory.Rect("Head", root);
            UiFactory.Place(head, 0, headBottom, 1, 1);
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

            // One small line: the gaps (thousandths) to the riders just ahead and behind.
            _gaps = Line(root, gapsBottom, headBottom, TextAnchor.MiddleLeft);

            // Front and rear tyre in one row: "Del. M 82%" with a thin bar beside it (never under the text).
            var tyres = UiFactory.Rect("Tyres", root);
            UiFactory.Place(tyres, 0, tyresBottom, 1, gapsBottom);
            _front = TyreGauge(tyres, 0, 0.49f, out _frontBar);
            _rear = TyreGauge(tyres, 0.51f, 1, out _rearBar);

            // Last and best lap on the left; fuel on the right, only when it matters.
            _laps = Line(root, lapsBottom, tyresBottom, TextAnchor.MiddleLeft);
            _laps.rectTransform.anchorMax = new Vector2(0.64f, tyresBottom);
            _fuel = Line(root, lapsBottom, tyresBottom, TextAnchor.MiddleRight);
            _fuel.rectTransform.anchorMin = new Vector2(0.64f, lapsBottom);

            // Orders: two cycling controls, then the engineer as a text button.
            float orderH = (lapsBottom - ordersBottom) / 2;
            _pace = BuildCycle(root, Loc.T("ux.race.pace"), lapsBottom - orderH, lapsBottom,
                new[] { Loc.T("ux.race.pace.save"), Loc.T("ux.race.pace.normal"), Loc.T("ux.race.pace.push") },
                () => Send(new RaceCommand { Type = RaceCommandType.SetPace, Pace = Paces[(Index(Paces, _rider.Pace) + 1) % Paces.Length] }));
            _mode = BuildCycle(root, Loc.T("ux.race.mode"), ordersBottom, lapsBottom - orderH,
                new[] { Loc.T("ux.race.mode.defend"), Loc.T("ux.race.mode.normal"), Loc.T("ux.race.mode.attack") },
                () => Send(new RaceCommand { Type = RaceCommandType.SetRiderMode, Mode = Modes[(Index(Modes, _rider.Mode) + 1) % Modes.Length] }));
            var autoArea = UiFactory.Rect("AutoArea", root);
            UiFactory.Place(autoArea, 0, 0, 1, ordersBottom);
            _auto = UiFactory.Button("Auto", autoArea, Theme.Caps(Loc.T("cmd.auto")),
                () => Send(new RaceCommand { Type = RaceCommandType.SetAutoManaged, Flag = !_rider.Entry.AutoManaged }), Color.clear, Theme.TextCaption);
            UiFactory.Stretch((RectTransform)_auto.transform);
            Ui.TextTab(_auto, false, Theme.TextCaption);
            _autoText = _auto.GetComponentInChildren<Text>();
            _autoText.rectTransform.offsetMin = new Vector2(4, 0);
        }

        private static int Index<T>(T[] values, T value)
        {
            int i = Array.IndexOf(values, value);
            return i < 0 ? 1 : i;
        }

        /// <summary>A one-line caption-size label filling a horizontal band of the card.</summary>
        private static Text Line(RectTransform root, float y0, float y1, TextAnchor align)
        {
            var t = RaceUi.Label(root, "", Theme.TextCaption, align, Theme.Text);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Place(t.rectTransform, 0, y0, 1, y1);
            t.rectTransform.offsetMax = new Vector2(-4, 0);
            return t;
        }

        /// <summary>
        /// The tyre text on the left of its half and a thin bar (6 units, vertically centred) on the right, with a gap
        /// between them, so the bar never reads as an underline.
        /// </summary>
        private static Text TyreGauge(RectTransform area, float x0, float x1, out Image fill)
        {
            float split = x0 + (x1 - x0) * 0.58f;
            var t = RaceUi.Label(area, "", Theme.TextCaption, TextAnchor.MiddleLeft, Theme.Text);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.Place(t.rectTransform, x0, 0, split, 1);
            var back = UiFactory.Decor("BarBack", area, new Color(1, 1, 1, 0.12f));
            back.rectTransform.anchorMin = new Vector2(split, 0.5f);
            back.rectTransform.anchorMax = new Vector2(x1, 0.5f);
            back.rectTransform.offsetMin = new Vector2(8, -3);
            back.rectTransform.offsetMax = new Vector2(-8, 3);
            fill = UiFactory.Decor("Fill", back.transform, Theme.Good);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(1, 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            return t;
        }

        /// <summary>
        /// "CAPTION   VALUE ›" in a band of the card: the whole row is one small-radius dark button; a tap runs
        /// <paramref name="onTap"/> (the next value). A short underline under the value: accent for the player's order,
        /// steel while the engineer decides.
        /// </summary>
        private static Cycle BuildCycle(RectTransform root, string caption, float y0, float y1, string[] labels, Action onTap)
        {
            var row = UiFactory.Rect("Order", root);
            UiFactory.Place(row, 0, y0, 1, y1);
            row.offsetMin = new Vector2(0, 3);
            row.offsetMax = new Vector2(0, -3);
            var b = UiFactory.Button("Order", row, "", onTap, Theme.Alpha(Color.white, 0.06f), Theme.TextCaption);
            UiFactory.Stretch((RectTransform)b.transform);
            var cap = b.GetComponentInChildren<Text>();
            cap.text = Theme.Caps(caption);
            cap.alignment = TextAnchor.MiddleLeft;
            cap.color = Theme.TextDim;
            UiFactory.Style(cap, FontStyle.Bold);
            Ui.Fit(cap, Theme.TextCaption);
            UiFactory.Place(cap.rectTransform, 0, 0, 0.42f, 1);
            cap.rectTransform.offsetMin = new Vector2(12, 0);
            var value = RaceUi.Label(b.transform, "", Theme.TextBody, TextAnchor.MiddleRight, Theme.Text, FontStyle.Bold);
            // Never wraps: one line, shrinking (not below 18) to fit.
            value.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.Place(value.rectTransform, 0.42f, 0, 1, 1);
            value.rectTransform.offsetMax = new Vector2(-38, 0);
            Ui.Fit(value, Theme.TextBody);
            var chevron = UiIcons.Chevron(b.transform, Theme.TextDim);
            chevron.rectTransform.anchorMin = chevron.rectTransform.anchorMax = new Vector2(1, 0.5f);
            chevron.rectTransform.sizeDelta = new Vector2(18, 18);
            chevron.rectTransform.anchoredPosition = new Vector2(-20, 0);
            var mark = UiFactory.Decor("Underline", value.transform, Theme.Accent);
            mark.rectTransform.anchorMin = new Vector2(0.55f, 0);
            mark.rectTransform.anchorMax = new Vector2(1, 0);
            mark.rectTransform.offsetMin = new Vector2(0, 2);
            mark.rectTransform.offsetMax = new Vector2(0, 5);
            return new Cycle { Value = value, Mark = mark, Labels = labels };
        }

        private static void SetCycle(Cycle c, int active, bool auto)
        {
            c.Value.text = Theme.Caps(c.Labels[Mathf.Clamp(active, 0, c.Labels.Length - 1)]);
            c.Value.color = auto ? Theme.TextDim : Theme.Text;
            c.Mark.color = auto ? Theme.Steel : Theme.Accent;
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
                case TireCondition.Cold: return new Color32(143, 180, 212, 255);
                case TireCondition.Hot: return Theme.Warning;
                case TireCondition.Overheated: return Theme.Bad;
                case TireCondition.Worn: return new Color32(200, 120, 60, 255);
                default: return Theme.Good;
            }
        }

        private static string Gap(double seconds)
        {
            return seconds.ToString("+0.000", Loc.Culture);
        }

        public void Refresh()
        {
            var r = _rider;
            var sim = _view.Sim;
            _position.text = Theme.Pos(r.Position);

            // Gaps to the riders just ahead and behind (the tower names them).
            var ahead = r.Position > 1 ? sim.RiderAtPosition(r.Position - 1) : null;
            var behind = sim.RiderAtPosition(r.Position + 1);
            string gaps = "";
            if (r.Status == RaceStatus.Racing)
            {
                if (ahead != null) gaps += Theme.Dim("P" + ahead.Position) + " <b>" + Gap(r.GapToAhead) + "</b>";
                if (behind != null && behind.Status == RaceStatus.Racing)
                    gaps += (gaps.Length > 0 ? "     " : "") + Theme.Dim("P" + behind.Position) + " <b>" + Gap(behind.GapToAhead) + "</b>";
            }
            _gaps.text = gaps;

            double rear = 1 - r.Rear.Wear;
            double front = 1 - r.Front.Wear;
            _front.text = Theme.Dim(Loc.T("card.front")) + " <b>" + RaceHud.CompoundLetter(r.Front.Compound) + " " + (front * 100).ToString("0") + "%</b>";
            _frontBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)front), 1);
            _frontBar.color = WearColor(r.Front);
            _rear.text = Theme.Dim(Loc.T("card.rear")) + " <b>" + RaceHud.CompoundLetter(r.Rear.Compound) + " " + (rear * 100).ToString("0") + "%</b>";
            _rearBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)rear), 1);
            _rearBar.color = WearColor(r.Rear);

            // Fuel only with a tank limit, once there is a consumption to judge, and only when it is tight
            // (less than a lap of margin): then the laps it lasts and the margin, red when it falls short.
            int lapsLeft = Math.Max(0, sim.TotalLaps - Math.Max(0, r.Lap));
            double limit = sim.Config.FuelLimitLitres;
            string fuel = "";
            if (limit > 0 && r.Lap >= 1)
            {
                double perLap = (limit - r.Fuel) / Math.Max(0.5, r.Lap);
                double lapsOfFuel = perLap > 0 ? r.Fuel / perLap : 99;
                double delta = lapsOfFuel - lapsLeft;
                if (delta < 1)
                    fuel = Theme.Dim(Loc.T("ux.race.fuel")) + " <b>" + Loc.T("card.fuelLaps", lapsOfFuel.ToString("0.0", Loc.Culture)) + "</b> " +
                           (delta >= 0 ? Theme.GoodText(delta.ToString("+0.0", Loc.Culture)) : Theme.BadText(delta.ToString("0.0", Loc.Culture)));
            }
            _fuel.text = fuel;

            string last = r.LastLapTime > 0 ? MathX.FormatLapTime(r.LastLapTime) : "-";
            string best = r.BestLapTime < double.MaxValue ? MathX.FormatLapTime(r.BestLapTime) : "-";
            bool fastest = r.BestLapTime < double.MaxValue && Math.Abs(r.BestLapTime - sim.FastestLap) < 0.0005;
            _laps.text = Theme.Dim(Loc.T("card.lastLap")) + " <b>" + last + "</b>   " + Theme.Dim(Loc.T("card.bestLap")) + " <b>" +
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
            SetCycle(_pace, Index(Paces, r.Pace), auto);
            SetCycle(_mode, Index(Modes, r.Mode), auto);
            _autoText.text = Theme.Caps(Loc.T(auto ? "ux.race.engineerOn" : "cmd.auto"));
            _autoText.color = auto ? Theme.Good : Theme.TextDim;
        }
    }
}
