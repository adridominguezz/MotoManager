using System;
using System.Collections.Generic;
using MotoManager.App.Race;
using MotoManager.Core.Race;
using MotoManager.Core.Season;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Plays the next event: the player's practice and qualifying run on the worker thread, every race/sprint of the
    /// player's category is shown in the race view, then the rest of the event (other categories, economy, news)
    /// is completed in the background.
    /// </summary>
    public class RaceFlow
    {
        private readonly GameSession _session;
        private readonly Action<string> _onDone;
        private readonly GameObject _menuToHide;
        private LiveEvent _event;
        private string _summary = "";
        private bool _qualifyingShown;

        public RaceFlow(GameSession session, GameObject menuToHide, Action<string> onDone)
        {
            _session = session;
            _menuToHide = menuToHide;
            _onDone = onDone;
        }

        public void Start()
        {
            var game = _session.Game;
            _session.Run(Loc.T("busy.weekend"), () =>
            {
                _event = game.BeginNextEvent();
                AdvanceToNextRace();
            }, ShowNextRaceOrFinish);
        }

        /// <summary>Worker thread: runs practice/qualifying/warm-up of the player's weekend until a race is due.</summary>
        private void AdvanceToNextRace()
        {
            var weekend = _event != null ? _event.PlayerWeekend : null;
            if (weekend == null) return;
            while (!weekend.IsComplete && !weekend.CurrentIsRace) weekend.RunCurrentHeadless();
        }

        private void ShowNextRaceOrFinish()
        {
            if (_event == null)
            {
                _onDone(Loc.T("home.seasonOverShort"));
                return;
            }
            var weekend = _event.PlayerWeekend;
            if (weekend != null && !weekend.IsComplete && weekend.CurrentIsRace)
            {
                if (_menuToHide != null) _menuToHide.SetActive(false);
                // First the qualifying result: where we start.
                if (!_qualifyingShown && weekend.QualifyingOrder.Count > 0)
                {
                    _qualifyingShown = true;
                    QualifyingPanel.Show(weekend, Loc.Round(_event.Plan.Round.Name), ShowNextRaceOrFinish);
                    return;
                }
                PreRaceDialog.Show(weekend, () =>
                {
                    var sim = weekend.CreateRaceSimulation(_session.Db.Tuning.VisualTimeStep);
                    string title = Loc.Round(_event.Plan.Round.Name) + " · " + Loc.Or("session." + weekend.CurrentSession.Name, weekend.CurrentSession.Name);
                    RaceView.Create(sim, title, OnRaceClosed);
                });
                return;
            }
            CompleteEvent();
        }

        private void OnRaceClosed(RaceSimulation sim)
        {
            var weekend = _event.PlayerWeekend;
            if (!sim.IsFinished) sim.RunToEnd();
            var result = weekend.CompleteRace(sim);
            if (result.Classification.Count > 0)
            {
                var winner = _session.Game.World.GetRider(result.Classification[0].RiderId);
                _summary += Loc.Or("session." + result.SessionName, result.SessionName) + ": " + (winner != null ? winner.FullName : "?") + "\n";
            }
            _session.Run(Loc.T("busy.nextSession"), AdvanceToNextRace, ShowNextRaceOrFinish);
        }

        private void CompleteEvent()
        {
            var game = _session.Game;
            var ev = _event;
            _session.Run(Loc.T("busy.restOfEvent"), () => game.CompleteEvent(ev), () =>
            {
                if (_menuToHide != null) _menuToHide.SetActive(true);
                _session.Save();
                _onDone("<b>" + Loc.Round(ev.Plan.Round.Name) + "</b>\n" + _summary);
            });
        }
    }

    /// <summary>
    /// Shared pieces of the race-flow panels (pre-race, qualifying, race HUD, post-race, event summary): the 72-unit
    /// top bar with the title on the left and the call to action on the right, bottom bars, team-colour bars in list
    /// rows, number plates and sheet hosting. Built only from the design system (Ui / UiFactory), no new icons.
    /// </summary>
    internal static class RaceUi
    {
        public const float BarH = 72, Pad = 16;

        /// <summary>Full-screen dark backdrop of a race-flow panel.</summary>
        public static Image Backdrop(RectTransform root, float alpha = 1f)
        {
            var bg = UiFactory.Panel("Backdrop", root, Theme.Alpha(Theme.Background, alpha));
            UiFactory.Stretch(bg.rectTransform);
            UiFactory.Gradient(bg, 1.12f, 0.88f);
            return bg;
        }

        /// <summary>A label with best fit 18..size that never drops its line when the box is short.</summary>
        public static Text Label(Transform parent, string text, int size, TextAnchor align, Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var t = Ui.Label(parent, text, size, align, color, style);
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>
        /// The 72-unit bar along the top of <paramref name="root"/>: accent tag, CAPS title (and a dim sub text),
        /// a hairline at its base. With <paramref name="ctaWidth"/> &gt; 0 <paramref name="cta"/> is the slot at the
        /// right end where the panel puts its one primary button.
        /// </summary>
        public static RectTransform TopBar(RectTransform root, string title, string sub, float ctaWidth, out RectTransform cta)
        {
            var bar = UiFactory.Panel("TopBar", root, Theme.Alpha(Theme.Panel, 0.97f));
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(0, -BarH);
            rt.offsetMax = Vector2.zero;
            var line = UiFactory.Decor("Rule", rt, Theme.Alpha(Theme.Accent, 0.7f));
            line.rectTransform.anchorMin = new Vector2(0, 0);
            line.rectTransform.anchorMax = new Vector2(1, 0);
            line.rectTransform.offsetMin = Vector2.zero;
            line.rectTransform.offsetMax = new Vector2(0, 2);
            UiFactory.Gradient(line, Color.white, new Color(1, 1, 1, 0.05f), true);
            var tag = UiFactory.Decor("Tag", rt, Theme.Accent);
            UiFactory.Slant(tag, 3.2f);
            tag.rectTransform.anchorMin = new Vector2(0, 0.24f);
            tag.rectTransform.anchorMax = new Vector2(0, 0.76f);
            tag.rectTransform.offsetMin = new Vector2(Pad, 0);
            tag.rectTransform.offsetMax = new Vector2(Pad + 14, 0);
            string text = "<b>" + Theme.Caps(title ?? "") + "</b>" +
                          (string.IsNullOrEmpty(sub) ? "" : "   <size=" + Theme.TextCaption + ">" + Theme.Dim(Theme.Caps(sub)) + "</size>");
            var t = Label(rt, text, Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Italic);
            UiFactory.DropShadow(t, 0.4f, 1.5f);
            UiFactory.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(Pad + 26, 0);
            t.rectTransform.offsetMax = new Vector2(ctaWidth > 0 ? -ctaWidth - 2 * Pad : -Pad, 0);
            cta = null;
            if (ctaWidth > 0) cta = RightSlot(rt, ctaWidth, 0, 0);
            return rt;
        }

        /// <summary>A slot of <paramref name="width"/> units at the right end of a bar (full height minus insets).</summary>
        public static RectTransform RightSlot(RectTransform bar, float width, float right, float inset = 4)
        {
            var slot = UiFactory.Rect("Slot", bar);
            slot.anchorMin = new Vector2(1, 0);
            slot.anchorMax = new Vector2(1, 1);
            slot.pivot = new Vector2(1, 0.5f);
            slot.offsetMin = new Vector2(-right - width, inset);
            slot.offsetMax = new Vector2(-right, -inset);
            return slot;
        }

        /// <summary>A transparent bar of <paramref name="height"/> units along the bottom of <paramref name="root"/>.</summary>
        public static RectTransform BottomBar(RectTransform root, float height = BarH)
        {
            var bar = UiFactory.Rect("BottomBar", root);
            bar.anchorMin = new Vector2(0, 0);
            bar.anchorMax = new Vector2(1, 0);
            bar.pivot = new Vector2(0.5f, 0);
            bar.offsetMin = new Vector2(Pad, 8);
            bar.offsetMax = new Vector2(-Pad, 8 + height);
            return bar;
        }

        /// <summary>The rect between <paramref name="top"/> and <paramref name="bottom"/> units, inset by the gutter.</summary>
        public static RectTransform Content(RectTransform root, float top, float bottom)
        {
            var area = UiFactory.Rect("Content", root);
            UiFactory.Stretch(area);
            area.offsetMin = new Vector2(Pad, bottom);
            area.offsetMax = new Vector2(-Pad, -top);
            return area;
        }

        public static Color TeamColor(string hex)
        {
            return UiFactory.ParseColor(hex, Theme.Steel);
        }

        /// <summary>A 6-unit team-colour bar in front of the name cell (cell 1) of a <see cref="Ui.ListRow"/>.</summary>
        public static void TeamBar(RectTransform row, float[] widths, Color color)
        {
            float total = 0;
            foreach (var w in widths) total += w;
            if (total <= 0 || widths.Length < 2) return;
            var cells = row.Find("Cells") as RectTransform;
            if (cells == null) return;
            float x = widths[0] / total;
            var bar = UiFactory.Decor("TeamBar", cells, color);
            bar.rectTransform.anchorMin = new Vector2(x, 0.22f);
            bar.rectTransform.anchorMax = new Vector2(x, 0.78f);
            bar.rectTransform.offsetMin = new Vector2(8, 0);
            bar.rectTransform.offsetMax = new Vector2(14, 0);
            var name = cells.Find("Cell1") as RectTransform;
            if (name != null) name.offsetMin = new Vector2(24, name.offsetMin.y);
        }

        /// <summary>The rider's number on a plate in the team colour (chamfered), filling <paramref name="area"/>.</summary>
        public static Image Plate(RectTransform area, int number, Color teamColor, int size)
        {
            var fill = Color.Lerp(teamColor, Theme.Card, 0.25f);
            var plate = UiFactory.Decor("Plate", area, fill);
            UiFactory.Chamfer(plate, 1.8f);
            UiFactory.Gradient(plate, 1.1f, 0.85f);
            UiFactory.Stretch(plate.rectTransform);
            var n = Label(plate.transform, number.ToString(), size, TextAnchor.MiddleCenter, Theme.OnColor(fill), FontStyle.BoldAndItalic);
            UiFactory.Stretch(n.rectTransform);
            n.rectTransform.offsetMin = new Vector2(4, 2);
            n.rectTransform.offsetMax = new Vector2(-4, -2);
            return plate;
        }

        /// <summary>A slanted block with dark type on a light fill (grid slot, position) filling <paramref name="area"/>.</summary>
        public static Image Block(RectTransform area, string text, Color fill, int size)
        {
            var b = UiFactory.Decor("Block", area, fill);
            UiFactory.Slant(b, 2.4f);
            UiFactory.Stretch(b.rectTransform);
            var t = Label(b.transform, text, size, TextAnchor.MiddleCenter, Theme.OnColor(fill), FontStyle.BoldAndItalic);
            UiFactory.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(10, 2);
            t.rectTransform.offsetMax = new Vector2(-10, -2);
            return b;
        }

        /// <summary>A pill anchored at the left-middle of its parent, <paramref name="x"/> units in (no layout group).</summary>
        public static Image PillAt(Transform parent, string text, Color fill, float x, Color? textColor = null)
        {
            var p = Ui.Pill(parent, text, fill, textColor);
            var rt = p.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0);
            return p;
        }

        /// <summary>"SOL", "NUBES" or "LLUVIA" for a forecast (words instead of weather glyphs).</summary>
        public static string WeatherWord(bool wet, double rain)
        {
            return Loc.T(wet || rain > 0.5 ? "ux.race.rain" : rain > 0.2 ? "ux.race.cloud" : "ux.race.sun");
        }

        public static Color WeatherColor(bool wet, double rain)
        {
            return wet || rain > 0.5 ? (Color)new Color32(120, 178, 214, 255) : rain > 0.2 ? Theme.Info : Theme.Timing;
        }

        /// <summary>Rich-text colour tag for <paramref name="c"/>.</summary>
        public static string Hex(Color c)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }

        /// <summary>
        /// Makes <paramref name="root"/> the place where sheets open while <paramref name="owner"/> lives; the previous
        /// host comes back when the owner is destroyed.
        /// </summary>
        public static void HostSheets(GameObject owner, Transform root)
        {
            var scope = owner.AddComponent<RaceSheetScope>();
            // Panels replace each other within a frame (qualifying -> pre-race -> race): chain back to the host that
            // was there before the race flow (the shell), never to a panel that is being destroyed.
            var previous = Ui.SheetHost;
            foreach (var other in RaceSheetScope.Live)
                if (other != null && other.Mine == previous) previous = other.Previous;
            scope.Previous = previous;
            scope.Mine = root;
            Ui.SheetHost = root;
        }
    }

    /// <summary>Restores the previous sheet host when a race-flow panel goes away.</summary>
    internal class RaceSheetScope : MonoBehaviour
    {
        public static readonly List<RaceSheetScope> Live = new List<RaceSheetScope>();
        public Transform Previous;
        public Transform Mine;

        private void Awake()
        {
            Live.Add(this);
        }

        private void OnDestroy()
        {
            Live.Remove(this);
            if (Ui.SheetHost == Mine || Ui.SheetHost == null) Ui.SheetHost = Previous;
        }
    }
}
