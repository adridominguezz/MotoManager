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
    /// Before each race of the player's category: grid positions, weather outlook (probabilistic, as good as the
    /// team's data engineer) and the front/rear tire choice for each rider, with the engineer's recommendation.
    /// </summary>
    public class PreRaceDialog : MonoBehaviour
    {
        private WeekendSimulation _weekend;
        private Action _onStart;
        private readonly Dictionary<string, TireChoice> _choices = new Dictionary<string, TireChoice>();
        private readonly List<KeyValuePair<Image, Func<bool>>> _toggles = new List<KeyValuePair<Image, Func<bool>>>();

        public static PreRaceDialog Show(WeekendSimulation weekend, Action onStart)
        {
            var canvas = UiFactory.CreateCanvas("PreRace", 20);
            var dialog = canvas.gameObject.AddComponent<PreRaceDialog>();
            dialog._weekend = weekend;
            dialog._onStart = onStart;
            dialog.Build(UiFactory.SafeRoot(canvas));
            return dialog;
        }

        private void Build(RectTransform root)
        {
            var w = _weekend;
            var session = w.CurrentSession;
            var rules = w.Rules;
            int laps = w.LapsFor(session);
            var weather = w.SessionWeather[session.Id];
            var model = new WeatherModel(weather, w.Db.Tuning, 1);
            double skill = 50;
            var team = w.World.PlayerTeam;
            if (team != null) skill = TeamCapabilities.ForecastSkill(w.World, team);
            double raceSeconds = laps * w.Circuit.LengthM / 45.0;
            double rain = model.ForecastRainProbability(raceSeconds, skill);

            var bg = UiFactory.Panel("Dim", root, new Color(0, 0, 0, 0.75f));
            UiFactory.Stretch(bg.rectTransform);
            var panel = UiFactory.Panel("Panel", root, Theme.Panel);
            UiFactory.Chamfer(panel, 1.2f);
            UiFactory.Gradient(panel, 1.06f, 0.94f);
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.08f);
            UiFactory.Place(panel.rectTransform, 0.18f, 0.1f, 0.82f, 0.9f);
            var p = panel.rectTransform;

            var title = UiFactory.Label("Title", p, Loc.Round(w.Round.Name) + " · " + Loc.Or("session." + session.Name, session.Name) + " · " + Loc.T("pre.laps", laps), Theme.FontLarge, TextAnchor.MiddleCenter);
            UiFactory.RaceTitle(title);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = Theme.FontNormal - 2;
            title.resizeTextMaxSize = Theme.FontLarge;
            UiFactory.Place(title.rectTransform, 0.02f, 0.88f, 0.98f, 0.98f);
            string conditions = Loc.T(weather.InitialWetness > 0.35 ? "pre.wet" : "pre.dry", model.AirTemp.ToString("0"), model.TrackTemp.ToString("0")) +
                                "\n" + Loc.T("pre.forecast", (rain * 100).ToString("0"));
            var profile = TrackProfile.Compute(TrackModel.Get(w.Circuit));
            var traits = new List<string>();
            foreach (var t in profile.Traits) traits.Add(Loc.T("trait." + t));
            if (traits.Count == 0) traits.Add(Loc.T("trait.Balanced"));
            conditions += "\n" + Loc.T("post.profile", string.Join(", ", traits.ToArray()));
            var cond = UiFactory.Label("Conditions", p, conditions, Theme.FontNormal, TextAnchor.MiddleCenter, Theme.TextDim);
            UiFactory.Place(cond.rectTransform, 0.02f, 0.72f, 0.98f, 0.88f);

            var players = new List<RaceEntry>();
            foreach (var e in w.Entries) if (e.IsPlayer) players.Add(e);
            float rowH = 0.6f / Mathf.Max(1, players.Count);
            for (int i = 0; i < players.Count; i++)
            {
                var e = players[i];
                TireCompound front, rear;
                RaceAI.ChooseTires(rules, model.TrackTemp, weather.InitialWetness, w.Circuit.Abrasiveness, laps, e.Rider.Attributes.TireManagement, out front, out rear);
                var choice = new TireChoice { Front = front, Rear = rear };
                _choices[e.Rider.Id] = choice;

                float yTop = 0.72f - rowH * i;
                int grid = w.QualifyingOrder.IndexOf(e.Rider.Id) + 1;
                var name = UiFactory.Label("Rider", p, "<b>" + e.Rider.FullName + "</b>   " + Loc.T("pre.grid", grid > 0 ? "P" + grid : "-") +
                                                       "   " + Theme.Dim(Loc.T("pre.engineer", RaceHud.CompoundLetter(front) + "/" + RaceHud.CompoundLetter(rear))), Theme.FontNormal, TextAnchor.MiddleLeft);
                name.supportRichText = true;
                UiFactory.Place(name.rectTransform, 0.04f, yTop - rowH * 0.35f, 0.96f, yTop);
                AddCompoundRow(p, Loc.T("pre.front"), rules, yTop - rowH * 0.68f, yTop - rowH * 0.38f, () => choice.Front, c => choice.Front = c);
                AddCompoundRow(p, Loc.T("pre.rear"), rules, yTop - rowH * 0.98f, yTop - rowH * 0.7f, () => choice.Rear, c => choice.Rear = c);
            }

            var start = UiFactory.Button("Start", p, Loc.T("pre.start"), StartRace, Theme.Accent);
            UiFactory.Place((RectTransform)start.transform, 0.3f, 0.02f, 0.7f, 0.1f);
            RefreshToggles();
        }

        private void AddCompoundRow(RectTransform p, string label, CategoryRules rules, float yMin, float yMax, Func<TireCompound> get, Action<TireCompound> set)
        {
            var l = UiFactory.Label(label, p, label, Theme.FontSmall, TextAnchor.MiddleLeft, Theme.TextDim);
            UiFactory.Place(l.rectTransform, 0.06f, yMin, 0.2f, yMax);
            var compounds = rules.Tires.Compounds;
            for (int i = 0; i < compounds.Count; i++)
            {
                var c = compounds[i].Compound;
                var b = UiFactory.Button(c.ToString(), p, Loc.T("tyre." + c), () =>
                {
                    set(c);
                    RefreshToggles();
                }, Theme.PanelLight, Theme.FontSmall);
                float x0 = 0.2f + i * 0.15f;
                UiFactory.Place((RectTransform)b.transform, x0, yMin, x0 + 0.14f, yMax);
                _toggles.Add(new KeyValuePair<Image, Func<bool>>(b.GetComponent<Image>(), () => get() == c));
            }
        }

        private void RefreshToggles()
        {
            foreach (var t in _toggles) t.Key.color = t.Value() ? Theme.Accent : Theme.PanelLight;
        }

        private void StartRace()
        {
            foreach (var kv in _choices) _weekend.PlayerTireChoices[kv.Key] = kv.Value;
            Destroy(gameObject);
            if (_onStart != null) _onStart();
        }
    }
}
