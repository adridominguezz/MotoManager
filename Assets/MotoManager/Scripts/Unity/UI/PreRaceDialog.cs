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

        /// <summary>
        /// Phone layout in the style of mobile racing managers: a top bar with the circuit and laps and one big start
        /// button; the weather on the right; one large card per rider (number, name, grid slot) with the front and
        /// rear tyre shown as coloured tyres that change compound with a tap.
        /// </summary>
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
            bool wet = weather.InitialWetness > 0.35;

            var bg = UiFactory.Panel("Backdrop", root, new Color(0.05f, 0.06f, 0.08f, 0.94f));
            UiFactory.Stretch(bg.rectTransform);
            UiFactory.Gradient(bg, 1.15f, 0.8f);

            // Top bar: event and session, laps, start.
            var bar = UiFactory.Panel("Bar", root, new Color(0.02f, 0.025f, 0.035f, 0.95f));
            UiFactory.Place(bar.rectTransform, 0, 0.86f, 1, 1);
            var name = UiFactory.Label("Event", bar.transform, Theme.Caps(Loc.Round(w.Round.Name)), Theme.FontLarge, TextAnchor.MiddleLeft);
            UiFactory.RaceTitle(name);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = Theme.FontSmall;
            name.resizeTextMaxSize = Theme.FontLarge;
            UiFactory.Place(name.rectTransform, 0.02f, 0.05f, 0.42f, 0.95f);
            var lapsBlock = UiFactory.Panel("Laps", bar.transform, new Color(1, 1, 1, 0.07f));
            UiFactory.Place(lapsBlock.rectTransform, 0.43f, 0.12f, 0.6f, 0.88f);
            var lapsText = UiFactory.Label("Text", lapsBlock.transform, Theme.Caps(Loc.T("pre.laps", laps)), Theme.FontNormal, TextAnchor.MiddleCenter);
            lapsText.fontStyle = FontStyle.BoldAndItalic;
            lapsText.resizeTextForBestFit = true;
            lapsText.resizeTextMinSize = Theme.FontSmall - 6;
            lapsText.resizeTextMaxSize = Theme.FontNormal;
            UiFactory.Stretch(lapsText.rectTransform);
            var start = UiFactory.Button("Start", bar.transform, Theme.Caps(Loc.T("pre.start")) + "  ›", StartRace, Theme.Accent, Theme.FontLarge);
            UiFactory.Slant(start.image, 2.4f);
            start.GetComponentInChildren<Text>().fontStyle = FontStyle.BoldAndItalic;
            UiFactory.Place((RectTransform)start.transform, 0.64f, 0.1f, 0.995f, 0.9f);

            // Weather: big symbol, rain chance and track temperature; the track's character underneath.
            var wx = UiFactory.Panel("Weather", root, Theme.Panel);
            UiFactory.Chamfer(wx, 1.6f);
            UiFactory.Place(wx.rectTransform, 0.6f, 0.62f, 0.995f, 0.84f);
            var icon = UiFactory.Label("Icon", wx.transform, wet || rain > 0.5 ? "☂" : rain > 0.2 ? "☁" : "☀", Theme.FontTitle, TextAnchor.MiddleCenter, wet || rain > 0.5 ? new Color(0.36f, 0.75f, 0.92f) : Theme.Timing);
            UiFactory.Place(icon.rectTransform, 0, 0, 0.24f, 1);
            var rainText = UiFactory.Label("Rain", wx.transform, "<b>" + (rain * 100).ToString("0") + "%</b> " + Theme.Dim(Loc.T("pre.rainShort")), Theme.FontNormal, TextAnchor.LowerLeft);
            rainText.supportRichText = true;
            rainText.resizeTextForBestFit = true;
            rainText.resizeTextMinSize = Theme.FontSmall - 6;
            rainText.resizeTextMaxSize = Theme.FontNormal;
            UiFactory.Place(rainText.rectTransform, 0.26f, 0.5f, 0.98f, 0.95f);
            var temps = UiFactory.Label("Temps", wx.transform, Loc.T(wet ? "pre.wet" : "pre.dry", model.AirTemp.ToString("0"), model.TrackTemp.ToString("0")), Theme.FontSmall - 4, TextAnchor.UpperLeft, Theme.TextDim);
            temps.resizeTextForBestFit = true;
            temps.resizeTextMinSize = Theme.FontSmall - 10;
            temps.resizeTextMaxSize = Theme.FontSmall - 4;
            UiFactory.Place(temps.rectTransform, 0.26f, 0.05f, 0.98f, 0.48f);

            var profile = TrackProfile.Compute(TrackModel.Get(w.Circuit));
            var traits = new List<string>();
            foreach (var t in profile.Traits) traits.Add(Loc.T("trait." + t));
            if (traits.Count == 0) traits.Add(Loc.T("trait.Balanced"));
            var track = UiFactory.Label("Track", root, Theme.Caps(Loc.Or("circuit." + w.Circuit.Id, w.Circuit.Name)) + "\n" + Theme.Dim(string.Join(" · ", traits.ToArray())), Theme.FontNormal, TextAnchor.MiddleLeft);
            track.supportRichText = true;
            track.fontStyle = FontStyle.Bold;
            track.resizeTextForBestFit = true;
            track.resizeTextMinSize = Theme.FontSmall - 6;
            track.resizeTextMaxSize = Theme.FontNormal;
            UiFactory.Place(track.rectTransform, 0.02f, 0.62f, 0.58f, 0.84f);

            // One big card per rider, side by side.
            var players = new List<RaceEntry>();
            foreach (var e in w.Entries) if (e.IsPlayer) players.Add(e);
            int n = Mathf.Max(1, players.Count);
            float cw = 0.99f / n;
            for (int i = 0; i < players.Count; i++)
            {
                var e = players[i];
                TireCompound front, rear;
                RaceAI.ChooseTires(rules, model.TrackTemp, weather.InitialWetness, w.Circuit.Abrasiveness, laps, e.Rider.Attributes.TireManagement, out front, out rear);
                var choice = new TireChoice { Front = front, Rear = rear };
                _choices[e.Rider.Id] = choice;
                int grid = w.QualifyingOrder.IndexOf(e.Rider.Id) + 1;
                var card = UiFactory.Panel("Rider", root, Theme.Panel);
                UiFactory.Chamfer(card, 1.4f);
                UiFactory.Place(card.rectTransform, 0.005f + cw * i, 0.02f, cw * (i + 1) - 0.005f, 0.6f);
                RiderCard((RectTransform)card.transform, e, grid, rules, choice, RaceHud.CompoundLetter(front) + "/" + RaceHud.CompoundLetter(rear));
            }
            RefreshToggles();
        }

        private static readonly Color TyreDark = new Color(0.09f, 0.09f, 0.1f, 1f);

        public static Color CompoundColor(TireCompound c)
        {
            switch (c)
            {
                case TireCompound.Soft: return new Color(0.88f, 0.12f, 0.12f);
                case TireCompound.Medium: return new Color(0.96f, 0.78f, 0.1f);
                case TireCompound.Hard: return new Color(0.92f, 0.93f, 0.95f);
                case TireCompound.Intermediate: return new Color(0.2f, 0.75f, 0.3f);
                default: return new Color(0.2f, 0.5f, 0.95f);
            }
        }

        private void RiderCard(RectTransform card, RaceEntry e, int grid, CategoryRules rules, TireChoice choice, string advice)
        {
            var team = e.Team;
            var teamColor = UiFactory.ParseColor(team != null ? team.Colors.Primary : null, Theme.Accent);
            // Header strip in team colour: number, first name over the surname, grid slot in timing yellow.
            var head = UiFactory.Panel("Head", card, Color.Lerp(teamColor, Theme.Panel, 0.55f));
            UiFactory.Place(head.rectTransform, 0, 0.66f, 1, 1);
            UiFactory.Gradient(head, Color.white, new Color(1, 1, 1, 0.6f), true);
            var num = UiFactory.Panel("Number", head.transform, teamColor);
            UiFactory.Chamfer(num, 1.6f);
            UiFactory.Place(num.rectTransform, 0.02f, 0.1f, 0.2f, 0.9f);
            var numText = UiFactory.Label("N", num.transform, e.Rider.Number.ToString(), Theme.FontTitle, TextAnchor.MiddleCenter, Color.white);
            numText.fontStyle = FontStyle.BoldAndItalic;
            numText.resizeTextForBestFit = true;
            numText.resizeTextMinSize = Theme.FontNormal;
            numText.resizeTextMaxSize = Theme.FontTitle;
            numText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            UiFactory.Stretch(numText.rectTransform);
            var first = UiFactory.Label("First", head.transform, e.Rider.FirstName, Theme.FontSmall - 2, TextAnchor.LowerLeft, Theme.TextDim);
            UiFactory.Place(first.rectTransform, 0.23f, 0.55f, 0.72f, 0.95f);
            var last = UiFactory.Label("Last", head.transform, Theme.Caps(e.Rider.LastName), Theme.FontLarge, TextAnchor.UpperLeft);
            last.fontStyle = FontStyle.BoldAndItalic;
            last.resizeTextForBestFit = true;
            last.resizeTextMinSize = Theme.FontSmall;
            last.resizeTextMaxSize = Theme.FontLarge;
            UiFactory.Place(last.rectTransform, 0.23f, 0.05f, 0.72f, 0.58f);
            var gridBox = UiFactory.Panel("Grid", head.transform, Theme.Timing);
            UiFactory.Slant(gridBox, 2.4f);
            UiFactory.Place(gridBox.rectTransform, 0.74f, 0.2f, 0.98f, 0.8f);
            var gridText = UiFactory.Label("P", gridBox.transform, grid > 0 ? "P" + grid : "-", Theme.FontLarge, TextAnchor.MiddleCenter, new Color32(12, 13, 16, 255));
            gridText.fontStyle = FontStyle.BoldAndItalic;
            gridText.resizeTextForBestFit = true;
            gridText.resizeTextMinSize = Theme.FontSmall;
            gridText.resizeTextMaxSize = Theme.FontLarge;
            UiFactory.Stretch(gridText.rectTransform);

            // Front and rear tyre: tap to change compound.
            TyreSlot(card, Loc.T("pre.front"), rules, 0.02f, 0.49f, () => choice.Front, c => choice.Front = c);
            TyreSlot(card, Loc.T("pre.rear"), rules, 0.51f, 0.98f, () => choice.Rear, c => choice.Rear = c);
            var adv = UiFactory.Label("Advice", card, Loc.T("pre.engineer", advice), Theme.FontSmall - 6, TextAnchor.MiddleCenter, Theme.TextDim);
            adv.resizeTextForBestFit = true;
            adv.resizeTextMinSize = Theme.FontSmall - 12;
            adv.resizeTextMaxSize = Theme.FontSmall - 6;
            UiFactory.Place(adv.rectTransform, 0.02f, 0.0f, 0.98f, 0.1f);
        }

        private void TyreSlot(RectTransform card, string label, CategoryRules rules, float x0, float x1, Func<TireCompound> get, Action<TireCompound> set)
        {
            var compounds = rules.Tires.Compounds;
            var slot = UiFactory.Button("Tyre " + label, card, "", () =>
            {
                // Next compound allowed by the rules.
                int idx = 0;
                for (int i = 0; i < compounds.Count; i++) if (compounds[i].Compound == get()) idx = i;
                set(compounds[(idx + 1) % compounds.Count].Compound);
                RefreshToggles();
            }, new Color(1, 1, 1, 0.05f));
            UiFactory.Place((RectTransform)slot.transform, x0, 0.11f, x1, 0.64f, 4);
            var st = (RectTransform)slot.transform;
            // A tyre: dark rubber, a coloured band for the compound, the dark rim inside.
            var tyre = UiFactory.Panel("Rubber", st, TyreDark);
            UiFactory.Round(tyre, 0.12f);
            tyre.raycastTarget = false;
            var tr = tyre.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.24f, 0.5f);
            tr.sizeDelta = new Vector2(104, 104);
            var band = UiFactory.Panel("Band", tr, Color.white);
            UiFactory.Round(band, 0.15f);
            band.raycastTarget = false;
            band.rectTransform.anchorMin = band.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            band.rectTransform.sizeDelta = new Vector2(76, 76);
            var rim = UiFactory.Panel("Rim", band.rectTransform, TyreDark);
            UiFactory.Round(rim, 0.2f);
            rim.raycastTarget = false;
            rim.rectTransform.anchorMin = rim.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rim.rectTransform.sizeDelta = new Vector2(62, 62);
            var hub = UiFactory.Panel("Hub", rim.rectTransform, Theme.Timing);
            UiFactory.Round(hub, 0.4f);
            hub.raycastTarget = false;
            hub.rectTransform.anchorMin = hub.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            hub.rectTransform.sizeDelta = new Vector2(20, 20);
            var cap = UiFactory.Label("Label", st, Theme.Caps(label), Theme.FontSmall - 6, TextAnchor.LowerLeft, Theme.TextDim);
            UiFactory.Place(cap.rectTransform, 0.48f, 0.55f, 0.98f, 0.92f);
            var compound = UiFactory.Label("Compound", st, "", Theme.FontNormal, TextAnchor.UpperLeft);
            compound.fontStyle = FontStyle.BoldAndItalic;
            compound.resizeTextForBestFit = true;
            compound.resizeTextMinSize = Theme.FontSmall - 6;
            compound.resizeTextMaxSize = Theme.FontNormal;
            UiFactory.Place(compound.rectTransform, 0.48f, 0.12f, 0.98f, 0.55f);
            var change = UiFactory.Label("Change", st, "›", Theme.FontSmall - 4, TextAnchor.UpperRight, Theme.TextDim);
            UiFactory.Place(change.rectTransform, 0.8f, 0.6f, 0.97f, 0.95f);
            _slots.Add(() =>
            {
                var c = get();
                band.color = CompoundColor(c);
                compound.text = Theme.Caps(Loc.T("tyre." + c));
            });
        }

        private readonly List<Action> _slots = new List<Action>();

        private void RefreshToggles()
        {
            foreach (var t in _toggles) t.Key.color = t.Value() ? Theme.Accent : Theme.PanelLight;
            foreach (var s in _slots) s();
        }

        private void StartRace()
        {
            foreach (var kv in _choices) _weekend.PlayerTireChoices[kv.Key] = kv.Value;
            Destroy(gameObject);
            if (_onStart != null) _onStart();
        }
    }
}
