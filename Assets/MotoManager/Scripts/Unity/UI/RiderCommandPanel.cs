using System.Collections.Generic;
using MotoManager.App.Race;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Pit-wall card for one of the player's riders: position and gaps, tire wear/temperature, fuel, and the
    /// orders that can be given during the race (pace, attitude, hand over to the engineer). There is no pit call:
    /// in bike racing riders only come in on their own for a flag-to-flag bike swap or a penalty.
    /// Every order goes through the simulation's command queue, so it is logged and replayable.
    /// </summary>
    public class RiderCommandPanel
    {
        private readonly RaceView _view;
        private readonly RiderRaceState _rider;
        private readonly Text _header;
        private readonly Text _gaps;
        private readonly Text _tires;
        private readonly Text _fuel;
        private readonly Image _frontBar;
        private readonly Image _rearBar;
        private readonly List<Image> _paceButtons = new List<Image>();
        private readonly List<Image> _modeButtons = new List<Image>();
        private readonly Image _autoButton;

        private static readonly PaceMode[] Paces = { PaceMode.Conserve, PaceMode.Balanced, PaceMode.Push };
        private static readonly RiderMode[] Modes = { RiderMode.Defend, RiderMode.Balanced, RiderMode.Attack };

        public RiderCommandPanel(RaceView view, RiderRaceState rider, RectTransform parent)
        {
            _view = view;
            _rider = rider;
            var rules = view.Sim.Rules;

            var bg = UiFactory.Panel("Rider " + rider.Entry.Rider.Code, parent, new Color(0.06f, 0.07f, 0.09f, 0.88f));
            UiFactory.Stretch(bg.rectTransform);
            var root = bg.rectTransform;

            _header = UiFactory.Label("Header", root, "", Theme.FontSmall + 2, TextAnchor.MiddleLeft);
            UiFactory.Place(_header.rectTransform, 0.03f, 0.84f, 0.97f, 0.99f);
            _header.supportRichText = true;
            _gaps = UiFactory.Label("Gaps", root, "", Theme.FontSmall - 2, TextAnchor.MiddleLeft, Theme.TextDim);
            UiFactory.Place(_gaps.rectTransform, 0.03f, 0.72f, 0.97f, 0.84f);

            _tires = UiFactory.Label("Tires", root, "", Theme.FontSmall - 2, TextAnchor.MiddleLeft);
            UiFactory.Place(_tires.rectTransform, 0.03f, 0.6f, 0.62f, 0.72f);
            _tires.supportRichText = true;
            _frontBar = Bar(root, 0.64f, 0.665f);
            _rearBar = Bar(root, 0.64f, 0.615f);
            _fuel = UiFactory.Label("Fuel", root, "", Theme.FontSmall - 2, TextAnchor.MiddleLeft, Theme.TextDim);
            UiFactory.Place(_fuel.rectTransform, 0.03f, 0.5f, 0.97f, 0.6f);

            string[] paceLabels = { Loc.T("cmd.pace.save"), Loc.T("cmd.pace.normal"), Loc.T("cmd.pace.push") };
            for (int i = 0; i < 3; i++)
            {
                var pace = Paces[i];
                var b = UiFactory.Button("Pace", root, paceLabels[i], () => Send(new RaceCommand { Type = RaceCommandType.SetPace, Pace = pace }), Theme.PanelLight, Theme.FontSmall - 2);
                UiFactory.Place((RectTransform)b.transform, 0.03f + i * 0.32f, 0.36f, 0.33f + i * 0.32f, 0.48f);
                _paceButtons.Add(b.GetComponent<Image>());
            }
            string[] modeLabels = { Loc.T("cmd.mode.defend"), Loc.T("cmd.mode.normal"), Loc.T("cmd.mode.attack") };
            for (int i = 0; i < 3; i++)
            {
                var mode = Modes[i];
                var b = UiFactory.Button("Mode", root, modeLabels[i], () => Send(new RaceCommand { Type = RaceCommandType.SetRiderMode, Mode = mode }), Theme.PanelLight, Theme.FontSmall - 2);
                UiFactory.Place((RectTransform)b.transform, 0.03f + i * 0.32f, 0.22f, 0.33f + i * 0.32f, 0.34f);
                _modeButtons.Add(b.GetComponent<Image>());
            }

            var auto = UiFactory.Button("Auto", root, Loc.T("cmd.auto"), () => Send(new RaceCommand { Type = RaceCommandType.SetAutoManaged, Flag = !_rider.Entry.AutoManaged }), Theme.PanelLight, Theme.FontSmall - 2);
            UiFactory.Place((RectTransform)auto.transform, 0.03f, 0.03f, 0.97f, 0.19f);
            _autoButton = auto.GetComponent<Image>();
        }

        private static Image Bar(RectTransform root, float xMin, float y)
        {
            var back = UiFactory.Panel("BarBack", root, new Color(1, 1, 1, 0.12f));
            UiFactory.Place(back.rectTransform, xMin, y, 0.97f, y + 0.04f);
            var fill = UiFactory.Panel("Fill", back.transform, Theme.Good);
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

        public void Refresh()
        {
            var r = _rider;
            var sim = _view.Sim;
            string status = r.Status == RaceStatus.Racing ? "P" + r.Position : Loc.T("race.status." + r.Status);
            _header.text = "<b>" + r.Entry.Rider.Number + " " + r.Entry.Rider.ShortName + "</b>   " + status +
                           (r.LastLapTime > 0 ? "   <color=#9AA4B2>" + Loc.T("cmd.last", MathX.FormatLapTime(r.LastLapTime)) + "</color>" : "");
            var behind = sim.RiderAtPosition(r.Position + 1);
            string ahead = r.Position > 1 && r.GapToAhead > 0 ? Loc.T("cmd.ahead", r.GapToAhead.ToString("0.0", Loc.Culture)) : Loc.T("cmd.leading");
            string back = behind != null && behind.Status == RaceStatus.Racing && behind.GapToAhead > 0 ? Loc.T("cmd.behind", behind.GapToAhead.ToString("0.0", Loc.Culture)) : "";
            _gaps.text = ahead + "   " + back + (r.PendingLongLaps > 0 ? "   " + Loc.T("cmd.longLap", r.PendingLongLaps) : "");

            _tires.text = "F " + RaceHud.CompoundLetter(r.Front.Compound) + " " + (r.Front.Wear * 100).ToString("0") + "% " + r.Front.Temperature.ToString("0") + "°" +
                          "   R " + RaceHud.CompoundLetter(r.Rear.Compound) + " " + (r.Rear.Wear * 100).ToString("0") + "% " + r.Rear.Temperature.ToString("0") + "°";
            SetBar(_frontBar, 1 - r.Front.Wear, WearColor(r.Front));
            SetBar(_rearBar, 1 - r.Rear.Wear, WearColor(r.Rear));

            if (sim.Config.FuelLimitLitres > 0)
            {
                double proj = sim.ProjectedFuelAtFinish(r);
                _fuel.text = Loc.T("cmd.fuel", r.Fuel.ToString("0.0", Loc.Culture)) + (double.IsNaN(proj) ? "" : "   " + Loc.T("cmd.fuelAtFlag", proj.ToString("+0.0;-0.0", Loc.Culture)));
                _fuel.color = !double.IsNaN(proj) && proj < 0.2 ? Theme.Accent : Theme.TextDim;
            }
            else
            {
                _fuel.text = r.HasIssue ? Loc.T("cmd.issue") : "";
            }

            for (int i = 0; i < 3; i++)
            {
                _paceButtons[i].color = r.Pace == Paces[i] ? Theme.Accent : Theme.PanelLight;
                _modeButtons[i].color = r.Mode == Modes[i] ? Theme.Accent : Theme.PanelLight;
            }
            _autoButton.color = r.Entry.AutoManaged ? Theme.Good : Theme.PanelLight;
        }

        private static void SetBar(Image fill, double value, Color color)
        {
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)value), 1);
            fill.color = color;
        }
    }
}
