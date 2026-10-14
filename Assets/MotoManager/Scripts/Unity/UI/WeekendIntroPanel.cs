using System;
using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Race;
using MotoManager.Core.Season;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Entering the event (MM4 §1.5 steps 1 and 3): the round ("RONDA X DE Y"), circuit, city and country, the
    /// layout drawn from the circuit's own geometry, the forecast for the main race (as good as the team's data
    /// engineer, the same model as the pre-race screen) and the weekend stepper. CONTINUAR runs practice and
    /// qualifying. Event sponsors (MM4 step 2) do not exist in MotoManager, so there is no such step.
    /// </summary>
    public class WeekendIntroPanel : MonoBehaviour
    {
        private Action _onContinue;
        private Action _onSimulate;

        /// <summary>
        /// <paramref name="onSimulate"/> (optional): simulate the whole event without watching it; the panel then shows
        /// a dark SIMULAR CARRERA block beside CONTINUAR.
        /// </summary>
        public static void Show(WeekendSimulation weekend, EventRecap recap, Action onContinue, Action onSimulate = null)
        {
            var canvas = UiFactory.CreateCanvas("WeekendIntro", 20);
            var p = canvas.gameObject.AddComponent<WeekendIntroPanel>();
            p._onContinue = onContinue;
            p._onSimulate = onSimulate;
            var root = UiFactory.SafeRoot(canvas, Theme.Background);
            RaceUi.HostSheets(canvas.gameObject, root);
            p.Build(root, weekend, recap);
        }

        private void Build(RectTransform root, WeekendSimulation w, EventRecap recap)
        {
            var game = GameSession.Instance != null ? GameSession.Instance.Game : null;
            int number = 0, count = 0;
            if (recap != null)
            {
                number = recap.RoundNumber;
                count = recap.RoundCount;
            }
            else if (game != null) EventRecap.RoundOf(w.World, w.Db, w.CategoryId, w.Round, out number, out count);
            var cat = w.Db.GetCategory(w.CategoryId);
            string catName = cat != null ? cat.Name : w.CategoryId;

            RaceUi.Backdrop(root);
            RectTransform cta;
            RaceUi.TopBar(root, Loc.Round(w.Round.Name), catName, RaceUi.ActionW * (_onSimulate != null ? 2 : 1), out cta);
            if (_onSimulate != null)
            {
                // Simulate (dark, secondary) on the left half, continue (race red, the primary) on the right half.
                var sim = UiFactory.Rect("Simulate", cta);
                UiFactory.Place(sim, 0, 0, 0.5f, 1);
                var b = RaceUi.ActionBlock(sim, Loc.T("ux.race2.simulate"), Simulate);
                b.image.color = Theme.PanelLight;
                foreach (var t in b.GetComponentsInChildren<Text>()) t.color = Theme.Text;
                foreach (var img in b.GetComponentsInChildren<Image>()) if (img != b.image) img.color = Theme.Text;
                var go = UiFactory.Rect("Continue", cta);
                UiFactory.Place(go, 0.5f, 0, 1, 1);
                RaceUi.ActionBlock(go, Loc.T("post.continue"), Continue);
            }
            else RaceUi.ActionBlock(cta, Loc.T("post.continue"), Continue);
            float top = RaceUi.BarH + 8;
            var steps = WeekendStepper.Steps(w);
            WeekendStepper.Strip(root, top, steps, WeekendStepper.StepOf(steps, w.SessionIndex));
            top += WeekendStepper.Height + 12;

            var content = RaceUi.Content(root, top, RaceUi.Pad);
            var cols = Ui.Columns12(content, 7, 5);
            var left = Ui.Rows(cols[0], 0.58f, 0.42f);
            CircuitTile(left[0], w, number, count, steps);
            WeatherTile(left[1], w);
            LayoutTile(cols[1], w);
        }

        /// <summary>Main race session (the last race of the weekend), or null.</summary>
        private static SessionDefinition MainRace(WeekendSimulation w)
        {
            SessionDefinition race = null;
            foreach (var s in w.Rules.Weekend.Sessions) if (s.Type == SessionType.Race) race = s;
            return race;
        }

        /// <summary>"RONDA X DE Y", the circuit in big type, city · country, length and the main race's laps.</summary>
        private static void CircuitTile(RectTransform area, WeekendSimulation w, int number, int count, List<WeekendStep> steps)
        {
            var c = w.Circuit;
            string circuit = Loc.Or("circuit." + c.Id, c.Name);
            var body = Ui.Tile(area, number > 0 ? Loc.T("ux.mm4c.roundOf", number, count) : Loc.T("ux.mm4c.event"), null, null, 0, null,
                () => Ui.Sheet(circuit, list =>
                {
                    foreach (var st in steps)
                        UiFactory.Paragraph(list, "<b>" + Theme.Caps(st.Label) + "</b>  " + Theme.Dim(WeekendStepper.DayText(st.Day)) + "\n" +
                                                  string.Join(" · ", st.Sessions.ToArray()), Theme.TextBody);
                    UiFactory.Paragraph(list, Theme.Dim(Loc.T("ux.mm4c.daysNote")), Theme.TextCaption);
                }));
            var name = RaceUi.Label(body, Theme.Caps(circuit), Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Place(name.rectTransform, 0, 0.56f, 1, 1);
            var place = new List<string>();
            if (!string.IsNullOrEmpty(c.City)) place.Add(c.City);
            if (!string.IsNullOrEmpty(c.Country)) place.Add(c.Country.ToUpperInvariant());
            var where = RaceUi.Label(body, string.Join("  ·  ", place.ToArray()), Theme.TextBody, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(where.rectTransform, 0, 0.34f, 1, 0.56f);
            var facts = new List<string> { "<b>" + (c.LengthM / 1000.0).ToString("0.000", Loc.Culture) + " km</b>" };
            var race = MainRace(w);
            if (race != null) facts.Add(Loc.T("ux.mm4c.raceLaps", Loc.Or("session." + race.Name, race.Name), w.LapsFor(race)));
            var line = RaceUi.Label(body, string.Join("   ", facts.ToArray()), Theme.TextBody, TextAnchor.MiddleLeft, Theme.Text);
            UiFactory.Place(line.rectTransform, 0, 0, 1, 0.32f);
        }

        /// <summary>The main race's outlook: SOL / NUBES / LLUVIA, the rain chance and the temperatures.</summary>
        private static void WeatherTile(RectTransform area, WeekendSimulation w)
        {
            var race = MainRace(w) ?? w.CurrentSession;
            WeatherSetup weather;
            if (race == null || !w.SessionWeather.TryGetValue(race.Id, out weather))
            {
                Ui.Tile(area, Loc.T("ux.race.weather"));
                return;
            }
            var model = new WeatherModel(weather, w.Db.Tuning, 1);
            double skill = 50;
            var team = w.World.PlayerTeam;
            if (team != null) skill = TeamCapabilities.ForecastSkill(w.World, team);
            int laps = w.LapsFor(race);
            double rain = model.ForecastRainProbability(laps * w.Circuit.LengthM / 45.0, skill);
            bool wet = weather.InitialWetness > 0.35;
            string sessionName = Loc.Or("session." + race.Name, race.Name);
            var body = Ui.Tile(area, Loc.T("ux.mm4c.forecast", sessionName), null, null, 0, null,
                () => Ui.Sheet(Loc.T("ux.race.weather"), list =>
                {
                    UiFactory.Paragraph(list, Loc.T(wet ? "pre.wet" : "pre.dry", model.AirTemp.ToString("0"), model.TrackTemp.ToString("0")), Theme.TextBody);
                    UiFactory.Paragraph(list, Loc.T("pre.forecast", (rain * 100).ToString("0")), Theme.TextBody);
                    UiFactory.Paragraph(list, Theme.Dim(Loc.T("ux.mm4c.forecastNote")), Theme.TextCaption);
                }));
            var word = RaceUi.Label(body, Theme.Caps(RaceUi.WeatherWord(wet, rain)), Theme.TextTitle, TextAnchor.MiddleLeft,
                RaceUi.WeatherColor(wet, rain), FontStyle.BoldAndItalic);
            UiFactory.Place(word.rectTransform, 0, 0, 0.32f, 1);
            var chance = RaceUi.Label(body, "<b>" + (rain * 100).ToString("0") + "%</b> <size=" + Theme.TextCaption + ">" +
                Theme.Dim(Loc.T("pre.rainShort")) + "</size>", Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text);
            UiFactory.Place(chance.rectTransform, 0.32f, 0, 0.66f, 1);
            var t = RaceUi.Label(body, Loc.T("ux.race.temps", model.AirTemp.ToString("0"), model.TrackTemp.ToString("0")),
                Theme.TextCaption, TextAnchor.MiddleRight, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(t.rectTransform, 0.66f, 0, 1, 1);
        }

        /// <summary>
        /// The circuit's shape from its simulated geometry (<see cref="TrackGeometry"/>, the same one the 3D track is
        /// built from), ~160 points scaled to fit with the aspect kept; the start/finish marked in race red.
        /// </summary>
        private static void LayoutTile(RectTransform area, WeekendSimulation w)
        {
            var body = Ui.Tile(area, Loc.T("ux.mm4c.layout"));
            TrackGeometry geo = null;
            try { geo = new TrackGeometry(TrackModel.Get(w.Circuit), 10.0); }
            catch (Exception e) { Debug.LogWarning("Circuit layout not drawn: " + e.Message); }
            if (geo == null || geo.X.Length < 8) return;
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            for (int i = 0; i < geo.X.Length; i++)
            {
                minX = Math.Min(minX, geo.X[i]);
                maxX = Math.Max(maxX, geo.X[i]);
                minY = Math.Min(minY, geo.Y[i]);
                maxY = Math.Max(maxY, geo.Y[i]);
            }
            double spanX = Math.Max(1, maxX - minX), spanY = Math.Max(1, maxY - minY);
            var holder = UiFactory.Rect("Layout", body);
            UiFactory.Stretch(holder);
            var fit = holder.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)(spanX / spanY);
            var line = UiFactory.Rect("Track", holder).gameObject.AddComponent<LineGraphic>();
            UiFactory.Stretch(line.rectTransform);
            line.Thickness = 6f;
            line.color = Theme.Text;
            int step = Math.Max(1, geo.X.Length / 160);
            for (int i = 0; i < geo.X.Length; i += step)
                line.Points.Add(new Vector2((float)((geo.X[i] - minX) / spanX), (float)((geo.Y[i] - minY) / spanY)));
            line.Points.Add(new Vector2((float)((geo.X[0] - minX) / spanX), (float)((geo.Y[0] - minY) / spanY)));
            var start = UiFactory.Decor("Start", holder, Theme.Race);
            var p = line.Points[0];
            start.rectTransform.anchorMin = start.rectTransform.anchorMax = p;
            start.rectTransform.sizeDelta = new Vector2(18, 18);
            start.rectTransform.anchoredPosition = Vector2.zero;
            UiFactory.Surface(start);
        }

        private void Simulate()
        {
            Destroy(gameObject);
            if (_onSimulate != null) _onSimulate();
        }

        private void Continue()
        {
            Destroy(gameObject);
            if (_onContinue != null) _onContinue();
        }
    }
}
