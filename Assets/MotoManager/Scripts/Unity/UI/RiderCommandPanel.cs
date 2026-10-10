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
    /// Pit-wall card for one of the player's riders (bottom corners of the race screen): the riders just ahead and
    /// behind with the gaps, front and rear tyre wear, fuel in laps against the laps left (where there is a tank limit), and the two
    /// orders that matter during a race (pace and attitude) plus handing the rider over to the engineer.
    /// There is no pit call: in bike racing riders only come in on their own for a flag-to-flag swap or a penalty.
    /// </summary>
    public class RiderCommandPanel
    {
        private readonly RaceView _view;
        private readonly RiderRaceState _rider;
        private readonly Text[] _rowPos = new Text[3];
        private readonly Text[] _rowName = new Text[3];
        private readonly Text[] _rowTyre = new Text[3];
        private readonly Text[] _rowGap = new Text[3];
        private readonly Image[] _rowBar = new Image[3];
        private readonly Image[] _rowBg = new Image[3];
        private readonly Text _front;
        private readonly Image _frontBar;
        private readonly Text _fuel;
        private readonly Image _fuelBar;
        private readonly Text _rear;
        private readonly Image _rearBar;
        private readonly Text _status;
        private readonly string _name;
        private readonly Button _pace;
        private readonly Button _mode;
        private readonly Button _auto;

        public RiderCommandPanel(RaceView view, RiderRaceState rider, RectTransform parent)
        {
            _view = view;
            _rider = rider;
            var team = rider.Entry.Team;
            var teamColor = UiFactory.ParseColor(team != null ? team.Colors.Primary : null, Theme.Accent);

            var bg = UiFactory.Panel("Rider " + rider.Entry.Rider.Code, parent, new Color(0.043f, 0.075f, 0.125f, 0.92f), true);
            UiFactory.Stretch(bg.rectTransform);
            var root = bg.rectTransform;

            // Mini standings: ahead, this rider, behind.
            for (int i = 0; i < 3; i++)
            {
                float y1 = 1 - i * 0.12f, y0 = y1 - 0.12f;
                _rowBg[i] = UiFactory.Panel("Row", root, i == 1 ? new Color(1, 1, 1, 0.9f) : new Color(1, 1, 1, 0));
                UiFactory.Place(_rowBg[i].rectTransform, 0, y0, 1, y1);
                Color text = i == 1 ? Theme.Background : Theme.Text;
                _rowPos[i] = UiFactory.Label("Pos", _rowBg[i].transform, "", Theme.FontSmall, TextAnchor.MiddleCenter, text);
                UiFactory.Place(_rowPos[i].rectTransform, 0, 0, 0.1f, 1);
                _rowBar[i] = UiFactory.Panel("Team", _rowBg[i].transform, Color.white);
                UiFactory.Place(_rowBar[i].rectTransform, 0.11f, 0.12f, 0.125f, 0.88f);
                _rowName[i] = UiFactory.Label("Name", _rowBg[i].transform, "", Theme.FontSmall, TextAnchor.MiddleLeft, text);
                _rowName[i].fontStyle = FontStyle.Bold;
                UiFactory.Place(_rowName[i].rectTransform, 0.15f, 0, 0.62f, 1);
                _rowTyre[i] = UiFactory.Label("Tyre", _rowBg[i].transform, "", Theme.FontSmall - 2, TextAnchor.MiddleCenter, text);
                UiFactory.Place(_rowTyre[i].rectTransform, 0.62f, 0, 0.74f, 1);
                _rowGap[i] = UiFactory.Label("Gap", _rowBg[i].transform, "", Theme.FontSmall - 2, TextAnchor.MiddleRight, text);
                UiFactory.Place(_rowGap[i].rectTransform, 0.74f, 0, 0.97f, 1);
            }

            // Number badge in team colour (left); front and rear tyre and fuel (right); orders below.
            var badge = UiFactory.Panel("Badge", root, teamColor, true);
            UiFactory.Place(badge.rectTransform, 0.03f, 0.37f, 0.25f, 0.63f);
            var num = UiFactory.Label("Number", badge.transform, rider.Entry.Rider.Number.ToString(), Theme.FontLarge + 4, TextAnchor.MiddleCenter, Color.white);
            num.fontStyle = FontStyle.Bold;
            num.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            UiFactory.Stretch(num.rectTransform);
            _status = UiFactory.Label("Name", root, rider.Entry.Rider.ShortName, Theme.FontSmall - 6, TextAnchor.MiddleCenter, Theme.TextDim);
            UiFactory.Place(_status.rectTransform, 0.0f, 0.29f, 0.28f, 0.37f);
            _name = rider.Entry.Rider.ShortName;

            bool fuel = view.Sim.Config.FuelLimitLitres > 0;
            // Front tyre, rear tyre (compound and how much is left) and, where there is a tank limit, fuel.
            _front = UiFactory.Label("Front", root, "", Theme.FontSmall, TextAnchor.MiddleLeft);
            _front.supportRichText = true;
            UiFactory.Place(_front.rectTransform, 0.31f, 0.545f, 0.97f, 0.63f);
            _frontBar = Bar(root, 0.31f, 0.525f);
            _rear = UiFactory.Label("Rear", root, "", Theme.FontSmall, TextAnchor.MiddleLeft);
            _rear.supportRichText = true;
            UiFactory.Place(_rear.rectTransform, 0.31f, 0.43f, 0.97f, 0.515f);
            _rearBar = Bar(root, 0.31f, 0.41f);
            _fuel = UiFactory.Label("Fuel", root, "", Theme.FontSmall - 2, TextAnchor.MiddleLeft);
            _fuel.supportRichText = true;
            UiFactory.Place(_fuel.rectTransform, 0.31f, 0.315f, 0.97f, 0.4f);
            _fuelBar = Bar(root, 0.31f, 0.295f);
            _fuel.gameObject.SetActive(fuel);
            _fuelBar.transform.parent.gameObject.SetActive(fuel);

            _pace = UiFactory.Button("Pace", root, "", CyclePace, Theme.PanelLight, Theme.FontSmall - 4);
            UiFactory.Place((RectTransform)_pace.transform, 0.02f, 0.135f, 0.5f, 0.27f, 3);
            _mode = UiFactory.Button("Mode", root, "", CycleMode, Theme.PanelLight, Theme.FontSmall - 4);
            UiFactory.Place((RectTransform)_mode.transform, 0.5f, 0.135f, 0.98f, 0.27f, 3);
            _auto = UiFactory.Button("Auto", root, Loc.T("cmd.auto"), () => Send(new RaceCommand { Type = RaceCommandType.SetAutoManaged, Flag = !_rider.Entry.AutoManaged }), Theme.PanelLight, Theme.FontSmall - 4);
            UiFactory.Place((RectTransform)_auto.transform, 0.02f, 0.005f, 0.98f, 0.135f, 3);
        }

        private static Image Bar(RectTransform root, float xMin, float y)
        {
            var back = UiFactory.Panel("BarBack", root, new Color(1, 1, 1, 0.12f), true);
            UiFactory.Place(back.rectTransform, xMin, y, 0.96f, y + 0.022f);
            var fill = UiFactory.Panel("Fill", back.transform, Theme.Good, true);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(1, 1);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            return fill;
        }

        private void Send(RaceCommand command)
        {
            command.RiderIndex = _rider.Index;
            _view.Sim.Enqueue(command);
        }

        private void CyclePace()
        {
            var next = _rider.Pace == PaceMode.Balanced ? PaceMode.Push : _rider.Pace == PaceMode.Push ? PaceMode.Conserve : PaceMode.Balanced;
            Send(new RaceCommand { Type = RaceCommandType.SetPace, Pace = next });
        }

        private void CycleMode()
        {
            var next = _rider.Mode == RiderMode.Balanced ? RiderMode.Attack : _rider.Mode == RiderMode.Attack ? RiderMode.Defend : RiderMode.Balanced;
            Send(new RaceCommand { Type = RaceCommandType.SetRiderMode, Mode = next });
        }

        private static Color WearColor(TireState t)
        {
            switch (t.Condition)
            {
                case TireCondition.Cold: return new Color32(91, 192, 235, 255);
                case TireCondition.Hot: return Theme.Warning;
                case TireCondition.Overheated: return Theme.Accent;
                case TireCondition.Worn: return new Color32(200, 120, 60, 255);
                default: return Theme.Good;
            }
        }

        private void Row(int i, RiderRaceState r, RiderRaceState self)
        {
            bool show = r != null;
            if (_rowBg[i].gameObject.activeSelf != show) _rowBg[i].gameObject.SetActive(show);
            if (!show) return;
            var team = r.Entry.Team;
            _rowPos[i].text = r.Position.ToString();
            _rowBar[i].color = UiFactory.ParseColor(team != null ? team.Colors.Primary : null, Color.gray);
            _rowName[i].text = r.Entry.Rider.ShortName.ToUpperInvariant();
            _rowTyre[i].text = RaceHud.CompoundLetter(r.Rear.Compound);
            if (i == 1) _rowGap[i].text = "-";
            else
            {
                // Gap relative to this rider: negative = ahead.
                double gap = i == 0 ? -self.GapToAhead : r.GapToAhead;
                _rowGap[i].text = r.Status == RaceStatus.Racing && self.Status == RaceStatus.Racing ? gap.ToString("+0.0;-0.0", Loc.Culture) : "";
            }
        }

        public void Refresh()
        {
            var r = _rider;
            var sim = _view.Sim;
            Row(0, r.Position > 1 ? sim.RiderAtPosition(r.Position - 1) : null, r);
            Row(1, r, r);
            Row(2, sim.RiderAtPosition(r.Position + 1), r);

            double rear = 1 - r.Rear.Wear;
            double front = 1 - r.Front.Wear;
            _front.text = Theme.Dim(Loc.T("card.front")) + "  <b>" + RaceHud.CompoundLetter(r.Front.Compound) + "  " + (front * 100).ToString("0") + "%</b>";
            _frontBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)front), 1);
            _frontBar.color = WearColor(r.Front);
            _rear.text = Theme.Dim(Loc.T("card.rear")) + "  <b>" + RaceHud.CompoundLetter(r.Rear.Compound) + "  " + (rear * 100).ToString("0") + "%</b>";
            _rearBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)rear), 1);
            _rearBar.color = WearColor(r.Rear);

            // Fuel in laps against the laps left (two-stroke era is long gone: every class has a tank limit).
            int lapsLeft = Math.Max(0, sim.TotalLaps - Math.Max(0, r.Lap));
            double limit = sim.Config.FuelLimitLitres;
            if (limit > 0 && r.Lap >= 1)
            {
                double perLap = (limit - r.Fuel) / Math.Max(0.5, r.Lap);
                double lapsOfFuel = perLap > 0 ? r.Fuel / perLap : 99;
                double delta = lapsOfFuel - lapsLeft;
                _fuel.text = Loc.T("card.fuelLaps", lapsOfFuel.ToString("0.0", Loc.Culture)) + "  " +
                             (delta >= 0 ? Theme.GoodText(delta.ToString("+0.0", Loc.Culture)) : Theme.BadText(delta.ToString("0.0", Loc.Culture)));
                _fuelBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)(r.Fuel / limit)), 1);
                _fuelBar.color = delta >= 0 ? Theme.Good : Theme.Bad;
            }
            else
            {
                _fuel.text = limit > 0 ? Loc.T("card.fuelFull") : "";
                _fuelBar.rectTransform.anchorMax = new Vector2(limit > 0 ? 1 : 0, 1);
            }


            string status = r.HasIssue ? Loc.T("cmd.issue") : r.PendingLongLaps > 0 ? Loc.T("cmd.longLap", r.PendingLongLaps) :
                r.Status != RaceStatus.Racing ? Loc.T("race.status." + r.Status) : "";
            _status.text = string.IsNullOrEmpty(status) ? _name : status;
            _status.color = string.IsNullOrEmpty(status) ? Theme.TextDim : Theme.Warning;

            _pace.GetComponentInChildren<Text>().text = "»  " + Loc.T("cmd.pace." + (r.Pace == PaceMode.Conserve ? "save" : r.Pace == PaceMode.Push ? "push" : "normal"));
            _pace.image.color = r.Pace == PaceMode.Push ? Theme.Accent : r.Pace == PaceMode.Conserve ? new Color32(46, 140, 90, 255) : Theme.PanelLight;
            _mode.GetComponentInChildren<Text>().text = "»  " + Loc.T("cmd.mode." + (r.Mode == RiderMode.Attack ? "attack" : r.Mode == RiderMode.Defend ? "defend" : "normal"));
            _mode.image.color = r.Mode == RiderMode.Attack ? Theme.Accent : r.Mode == RiderMode.Defend ? new Color32(52, 110, 200, 255) : Theme.PanelLight;
            _auto.image.color = r.Entry.AutoManaged ? Theme.Good : Theme.PanelLight;
        }
    }
}
