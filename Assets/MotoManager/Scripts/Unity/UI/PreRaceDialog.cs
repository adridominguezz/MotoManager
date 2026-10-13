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
        private readonly List<Action> _slots = new List<Action>();

        public static PreRaceDialog Show(WeekendSimulation weekend, Action onStart)
        {
            var canvas = UiFactory.CreateCanvas("PreRace", 20);
            var dialog = canvas.gameObject.AddComponent<PreRaceDialog>();
            dialog._weekend = weekend;
            dialog._onStart = onStart;
            var root = UiFactory.SafeRoot(canvas, Theme.Background);
            RaceUi.HostSheets(canvas.gameObject, root);
            dialog.Build(root);
            return dialog;
        }

        /// <summary>
        /// Phone layout in the style of mobile racing managers: a 72-unit top bar with the event, laps and the one
        /// big start button; the circuit and the weather in two tiles; one large card per rider (number, name, grid
        /// slot) with the front and rear tyre drawn in the compound colour; a tap opens the tyre picker.
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

            RaceUi.Backdrop(root);
            RectTransform cta;
            string sessionName = Loc.Or("session." + session.Name, session.Name);
            RaceUi.TopBar(root, Loc.Round(w.Round.Name), sessionName + " · " + Loc.T("pre.laps", laps), RaceUi.ActionW, out cta);
            // The one primary: the slanted block of the header (it starts the session, so it keeps its own label).
            RaceUi.ActionBlock(cta, Loc.T("ux.race.start"), StartRace);
            float top = RaceUi.BarH + 8;
            var steps = WeekendStepper.Steps(w);
            WeekendStepper.Strip(root, top, steps, WeekendStepper.StepOf(steps, w.SessionIndex));
            top += WeekendStepper.Height + 12;

            const float infoH = 132;
            var info = RaceUi.Content(root, top, 0);
            info.anchorMin = new Vector2(0, 1);
            info.offsetMin = new Vector2(RaceUi.Pad, -top - infoH);
            var cols = Ui.Columns12(info, 7, 5);
            TrackTile(cols[0], w);
            WeatherTile(cols[1], model, wet, rain);

            // One big card per rider, side by side.
            var players = new List<RaceEntry>();
            foreach (var e in w.Entries) if (e.IsPlayer) players.Add(e);
            var cards = RaceUi.Content(root, top + infoH + Ui.Grid.Gap, RaceUi.Pad);
            if (players.Count == 0) return;
            var spans = new int[players.Count];
            for (int i = 0; i < spans.Length; i++) spans[i] = 12 / Mathf.Max(1, players.Count);
            var areas = Ui.Columns12(cards, spans);
            for (int i = 0; i < players.Count; i++)
            {
                var e = players[i];
                TireCompound front, rear;
                RaceAI.ChooseTires(rules, model.TrackTemp, weather.InitialWetness, w.Circuit.Abrasiveness, w.FullLapsFor(session), e.Rider.Attributes.TireManagement, out front, out rear);
                var choice = new TireChoice { Front = front, Rear = rear };
                _choices[e.Rider.Id] = choice;
                int grid = w.QualifyingOrder.IndexOf(e.Rider.Id) + 1;
                RiderCard(areas[i], e, grid, rules, choice, front, rear);
            }
            RefreshSlots();
        }

        /// <summary>The circuit: name, its two main traits as pills; every trait in the info sheet.</summary>
        private static void TrackTile(RectTransform area, WeekendSimulation w)
        {
            var profile = TrackProfile.Compute(TrackModel.Get(w.Circuit));
            var traits = new List<string>();
            foreach (var t in profile.Traits) traits.Add(Loc.T("trait." + t));
            if (traits.Count == 0) traits.Add(Loc.T("trait.Balanced"));
            string circuit = Loc.Or("circuit." + w.Circuit.Id, w.Circuit.Name);
            var all = new List<string>();
            foreach (var t in traits) all.Add("·  " + t);
            var body = Ui.Tile(area, circuit, null, null, 0, null,
                () => Ui.Sheet(circuit, list =>
                {
                    foreach (var t in all) UiFactory.Paragraph(list, t, Theme.TextBody);
                }));
            float x = 0;
            for (int i = 0; i < traits.Count && i < 2; i++)
            {
                var p = RaceUi.PillAt(body, traits[i], Theme.PanelLight, x, Theme.Text);
                LayoutRebuilder.ForceRebuildLayoutImmediate(p.rectTransform);
                x += Mathf.Max(120, p.rectTransform.rect.width) + Ui.Grid.Gap;
            }
            if (traits.Count > 2) RaceUi.PillAt(body, "+" + (traits.Count - 2), new Color(1, 1, 1, 0.06f), x, Theme.TextDim);
        }

        /// <summary>Weather in words (SOL / NUBES / LLUVIA), the rain chance and the temperatures.</summary>
        private static void WeatherTile(RectTransform area, WeatherModel model, bool wet, double rain)
        {
            string temps = Loc.T(wet ? "pre.wet" : "pre.dry", model.AirTemp.ToString("0"), model.TrackTemp.ToString("0"));
            string forecast = Loc.T("pre.forecast", (rain * 100).ToString("0"));
            var body = Ui.Tile(area, Loc.T("ux.race.weather"), null, null, 0, null,
                () => Ui.Sheet(Loc.T("ux.race.weather"), list =>
                {
                    UiFactory.Paragraph(list, temps, Theme.TextBody);
                    UiFactory.Paragraph(list, forecast, Theme.TextBody);
                }));
            var word = RaceUi.Label(body, Theme.Caps(RaceUi.WeatherWord(wet, rain)), Theme.TextTitle, TextAnchor.MiddleLeft,
                RaceUi.WeatherColor(wet, rain), FontStyle.BoldAndItalic);
            UiFactory.Place(word.rectTransform, 0, 0, 0.3f, 1);
            var chance = RaceUi.Label(body, "<b>" + (rain * 100).ToString("0") + "%</b> <size=" + Theme.TextCaption + ">" +
                Theme.Dim(Loc.T("pre.rainShort")) + "</size>", Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text);
            UiFactory.Place(chance.rectTransform, 0.3f, 0, 0.66f, 1);
            var t = RaceUi.Label(body, Loc.T("ux.race.temps", model.AirTemp.ToString("0"), model.TrackTemp.ToString("0")),
                Theme.TextCaption, TextAnchor.MiddleRight, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(t.rectTransform, 0.66f, 0, 1, 1);
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

        /// <summary>
        /// A tyre drawn from the shared rounded sprite (dark rubber, the compound band, the rim, the hub), centred in
        /// its parent with <paramref name="size"/> units. Returns the band to colour.
        /// </summary>
        public static Image Tyre(Transform parent, float size)
        {
            var tyre = UiFactory.Decor("Rubber", parent, TyreDark);
            UiFactory.Round(tyre, 0.12f);
            var tr = tyre.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(size, size);
            var band = UiFactory.Decor("Band", tr, Color.white);
            UiFactory.Round(band, 0.15f);
            band.rectTransform.anchorMin = band.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            band.rectTransform.sizeDelta = new Vector2(size * 0.73f, size * 0.73f);
            var rim = UiFactory.Decor("Rim", band.rectTransform, TyreDark);
            UiFactory.Round(rim, 0.2f);
            rim.rectTransform.anchorMin = rim.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rim.rectTransform.sizeDelta = new Vector2(size * 0.6f, size * 0.6f);
            var hub = UiFactory.Decor("Hub", rim.rectTransform, Theme.Steel);
            UiFactory.Round(hub, 0.4f);
            hub.rectTransform.anchorMin = hub.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            hub.rectTransform.sizeDelta = new Vector2(size * 0.19f, size * 0.19f);
            return band;
        }

        private void RiderCard(RectTransform area, RaceEntry e, int grid, CategoryRules rules, TireChoice choice, TireCompound adviceFront, TireCompound adviceRear)
        {
            var team = e.Team;
            var teamColor = RaceUi.TeamColor(team != null ? team.Colors.Primary : null);
            var body = Ui.Tile(area, null, null, null, 0, null, null, false, teamColor);

            // Head: number plate, first name over the surname, grid slot in timing yellow.
            var head = UiFactory.Rect("Head", body);
            head.anchorMin = new Vector2(0, 1);
            head.anchorMax = new Vector2(1, 1);
            head.pivot = new Vector2(0.5f, 1);
            head.offsetMin = new Vector2(0, -92);
            head.offsetMax = Vector2.zero;
            var plateArea = UiFactory.Rect("PlateArea", head);
            plateArea.anchorMin = new Vector2(0, 0);
            plateArea.anchorMax = new Vector2(0, 1);
            plateArea.pivot = new Vector2(0, 0.5f);
            plateArea.offsetMin = new Vector2(0, 4);
            plateArea.offsetMax = new Vector2(104, -4);
            RaceUi.Plate(plateArea, e.Rider.Number, teamColor, Theme.TextDisplay);
            var first = RaceUi.Label(head, e.Rider.FirstName, Theme.TextCaption, TextAnchor.LowerLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Stretch(first.rectTransform);
            first.rectTransform.anchorMin = new Vector2(0, 0.58f);
            first.rectTransform.offsetMin = new Vector2(120, 0);
            first.rectTransform.offsetMax = new Vector2(-150, 0);
            var last = RaceUi.Label(head, Theme.Caps(e.Rider.LastName), Theme.FontLarge, TextAnchor.UpperLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Stretch(last.rectTransform);
            last.rectTransform.anchorMax = new Vector2(1, 0.6f);
            last.rectTransform.offsetMin = new Vector2(120, 0);
            last.rectTransform.offsetMax = new Vector2(-150, 0);
            var gridCap = RaceUi.Label(head, Loc.T("post.k.grid"), Theme.TextCaption, TextAnchor.LowerCenter, Theme.TextDim, FontStyle.Normal);
            gridCap.rectTransform.anchorMin = new Vector2(1, 0.62f);
            gridCap.rectTransform.anchorMax = new Vector2(1, 1);
            gridCap.rectTransform.pivot = new Vector2(1, 0.5f);
            gridCap.rectTransform.offsetMin = new Vector2(-136, 0);
            gridCap.rectTransform.offsetMax = new Vector2(0, 0);
            var gridArea = UiFactory.Rect("Grid", head);
            gridArea.anchorMin = new Vector2(1, 0);
            gridArea.anchorMax = new Vector2(1, 0.62f);
            gridArea.pivot = new Vector2(1, 0.5f);
            gridArea.offsetMin = new Vector2(-136, 2);
            gridArea.offsetMax = new Vector2(0, 0);
            RaceUi.Block(gridArea, Theme.Pos(grid), Theme.Timing, Theme.TextKey);

            var rule = UiFactory.Decor("Rule", body, Theme.Divider);
            rule.rectTransform.anchorMin = new Vector2(0, 1);
            rule.rectTransform.anchorMax = new Vector2(1, 1);
            rule.rectTransform.offsetMin = new Vector2(0, -101);
            rule.rectTransform.offsetMax = new Vector2(0, -99);

            // Front and rear tyre: a tap opens the picker.
            var tyres = UiFactory.Rect("Tyres", body);
            UiFactory.Stretch(tyres);
            tyres.offsetMin = new Vector2(0, 56);
            tyres.offsetMax = new Vector2(0, -112);
            var halves = Ui.Columns12(tyres, 6, 6);
            string surname = e.Rider.LastName;
            TyreSlot(halves[0], Loc.T("pre.front"), surname, rules, () => choice.Front, c => choice.Front = c, adviceFront);
            TyreSlot(halves[1], Loc.T("pre.rear"), surname, rules, () => choice.Rear, c => choice.Rear = c, adviceRear);

            // The engineer's call as a pill (details in the info sheet).
            var foot = UiFactory.Rect("Foot", body);
            foot.anchorMin = new Vector2(0, 0);
            foot.anchorMax = new Vector2(1, 0);
            foot.pivot = new Vector2(0.5f, 0);
            foot.offsetMin = Vector2.zero;
            foot.offsetMax = new Vector2(0, 44);
            string advice = RaceHud.CompoundLetter(adviceFront) + "/" + RaceHud.CompoundLetter(adviceRear);
            RaceUi.PillAt(foot, Loc.T("ux.race.engineer", advice), Theme.PanelLight, 0, Theme.Text);
            var tip = RaceUi.Label(foot, Loc.T("ux.race.tapToChange"), Theme.TextCaption, TextAnchor.MiddleRight, Theme.TextDim, FontStyle.Normal);
            UiFactory.Place(tip.rectTransform, 0.45f, 0, 1, 1);
        }

        private void TyreSlot(RectTransform area, string label, string surname, CategoryRules rules, Func<TireCompound> get, Action<TireCompound> set, TireCompound advice)
        {
            var slot = UiFactory.Button("Tyre " + label, area, "", () => OpenPicker(label, surname, rules, get, set, advice),
                Color.Lerp(Theme.Background, Theme.Panel, 0.85f));
            UiFactory.Stretch((RectTransform)slot.transform);
            slot.gameObject.AddComponent<Outline>().effectColor = Theme.Edge;
            var st = (RectTransform)slot.transform;
            var cap = RaceUi.Label(st, label, Theme.TextCaption, TextAnchor.UpperLeft, Theme.TextDim, FontStyle.Normal);
            UiFactory.Stretch(cap.rectTransform);
            cap.rectTransform.offsetMin = new Vector2(16, 0);
            cap.rectTransform.offsetMax = new Vector2(-48, -10);
            var ch = UiIcons.Chevron(st, Theme.TextDim);
            ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(1, 1);
            ch.rectTransform.sizeDelta = new Vector2(22, 22);
            ch.rectTransform.anchoredPosition = new Vector2(-24, -22);
            // The tyre fills the left part, the compound name the right.
            var holder = UiFactory.Rect("TyreHolder", st);
            UiFactory.Place(holder, 0, 0.02f, 0.5f, 0.8f);
            var band = Tyre(holder, 150);
            var fit = holder.gameObject.AddComponent<TyreFit>();
            fit.Tyre = (RectTransform)band.transform.parent;
            var compound = RaceUi.Label(st, "", Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Place(compound.rectTransform, 0.5f, 0.3f, 1, 0.8f);
            compound.rectTransform.offsetMax = new Vector2(-12, 0);
            var letter = RaceUi.Label(st, "", Theme.TextCaption, TextAnchor.UpperLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(letter.rectTransform, 0.5f, 0.06f, 1, 0.3f);
            letter.rectTransform.offsetMax = new Vector2(-12, 0);
            _slots.Add(() =>
            {
                var c = get();
                band.color = CompoundColor(c);
                compound.text = Theme.Caps(Loc.T("tyre." + c));
                letter.text = Theme.Caps(Hint(rules, c)) + (c == advice ? "  " + Theme.GoodText(Theme.Caps(Loc.T("ux.race.recommended"))) : "");
            });
        }

        /// <summary>One line on what a compound is for (relative to the others allowed by the rules).</summary>
        private static string Hint(CategoryRules rules, TireCompound c)
        {
            var spec = rules.Tires.Get(c);
            if (spec.IsWet) return Loc.T(c == TireCompound.Intermediate ? "ux.race.hint.inter" : "ux.race.hint.wet");
            double min = double.MaxValue, max = double.MinValue;
            int dry = 0;
            foreach (var s in rules.Tires.Compounds)
            {
                if (s.IsWet) continue;
                dry++;
                min = Math.Min(min, s.WearRate);
                max = Math.Max(max, s.WearRate);
            }
            if (dry <= 1 || max - min < 1e-6) return Loc.T("ux.race.hint.balanced");
            if (spec.WearRate <= min + 1e-6) return Loc.T("ux.race.hint.durable");
            if (spec.WearRate >= max - 1e-6) return Loc.T("ux.race.hint.grip");
            return Loc.T("ux.race.hint.balanced");
        }

        /// <summary>The tyre picker: one row per compound the rules allow, the active one marked.</summary>
        private void OpenPicker(string label, string surname, CategoryRules rules, Func<TireCompound> get, Action<TireCompound> set, TireCompound advice)
        {
            Ui.Sheet(label + " · " + surname, list =>
            {
                foreach (var spec in rules.Tires.Compounds)
                {
                    var c = spec.Compound;
                    bool active = c == get();
                    Action pick = () =>
                    {
                        set(c);
                        Ui.CloseSheet();
                        RefreshSlots();
                    };
                    var row = UiFactory.Row(list, 96);
                    var bg = row.gameObject.AddComponent<Image>();
                    bg.color = active ? Theme.Alpha(Theme.Accent, 0.18f) : new Color(1, 1, 1, 0.045f);
                    var b = row.gameObject.AddComponent<Button>();
                    b.targetGraphic = bg;
                    b.onClick.AddListener(() =>
                    {
                        GameAudio.ClickSound();
                        pick();
                    });
                    if (active) UiFactory.SideStripe(row, Theme.Accent, 4, 0);
                    var holder = UiFactory.Rect("Tyre", row);
                    holder.anchorMin = new Vector2(0, 0);
                    holder.anchorMax = new Vector2(0, 1);
                    holder.pivot = new Vector2(0, 0.5f);
                    holder.offsetMin = new Vector2(16, 0);
                    holder.offsetMax = new Vector2(96, 0);
                    Tyre(holder, 76).color = CompoundColor(c);
                    var name = RaceUi.Label(row, Theme.Caps(Loc.T("tyre." + c)), Theme.TextHeader, TextAnchor.LowerLeft, Theme.Text, FontStyle.BoldAndItalic);
                    UiFactory.Place(name.rectTransform, 0, 0.5f, 0.62f, 0.92f);
                    name.rectTransform.offsetMin = new Vector2(112, 0);
                    string sub = Theme.Caps(Hint(rules, c)) + (c == advice ? "  " + Theme.GoodText(Theme.Caps(Loc.T("ux.race.recommended"))) : "");
                    var hint = RaceUi.Label(row, sub, Theme.TextCaption, TextAnchor.UpperLeft, Theme.TextDim, FontStyle.Bold);
                    UiFactory.Place(hint.rectTransform, 0, 0.08f, 0.62f, 0.5f);
                    hint.rectTransform.offsetMin = new Vector2(112, 0);
                    var act = UiFactory.Rect("Action", row);
                    UiFactory.Place(act, 0.64f, 0.16f, 1, 0.84f);
                    act.offsetMax = new Vector2(-12, 0);
                    if (active)
                    {
                        var p = Ui.Pill(act, Loc.T("ux.race.active"), Theme.Accent);
                        p.rectTransform.anchorMin = p.rectTransform.anchorMax = new Vector2(1, 0.5f);
                        p.rectTransform.pivot = new Vector2(1, 0.5f);
                        p.rectTransform.anchoredPosition = Vector2.zero;
                    }
                    else Ui.Secondary(act, Loc.T("ux.race.select"), pick);
                }
            }, null, null, 0.56f);
        }

        private void RefreshSlots()
        {
            foreach (var s in _slots) s();
        }

        private void StartRace()
        {
            foreach (var kv in _choices) _weekend.PlayerTireChoices[kv.Key] = kv.Value;
            Destroy(gameObject);
            if (_onStart != null) _onStart();
        }
    }

    /// <summary>Keeps a drawn tyre as large as its holder allows (square, 92% of the smaller side).</summary>
    internal class TyreFit : MonoBehaviour
    {
        public RectTransform Tyre;
        private int _frames;

        private void LateUpdate()
        {
            if (Tyre == null) return;
            var r = ((RectTransform)transform).rect;
            float side = Mathf.Max(32, Mathf.Min(r.width, r.height) * 0.92f);
            Tyre.sizeDelta = new Vector2(side, side);
            float k = side;
            var band = Tyre.Find("Band") as RectTransform;
            if (band != null)
            {
                band.sizeDelta = new Vector2(k * 0.73f, k * 0.73f);
                var rim = band.Find("Rim") as RectTransform;
                if (rim != null)
                {
                    rim.sizeDelta = new Vector2(k * 0.6f, k * 0.6f);
                    var hub = rim.Find("Hub") as RectTransform;
                    if (hub != null) hub.sizeDelta = new Vector2(k * 0.19f, k * 0.19f);
                }
            }
            if (++_frames > 3) enabled = false;
        }
    }
}
