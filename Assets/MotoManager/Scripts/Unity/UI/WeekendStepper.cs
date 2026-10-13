using System;
using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.Season;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>What a step of the weekend stepper stands for.</summary>
    internal enum StepKind
    {
        Practice,
        Qualifying,
        Sprint,
        Race
    }

    /// <summary>
    /// One step of the weekend stepper: consecutive sessions of the same kind (free practice 1 + practice, Q1 + Q2)
    /// or one sprint / race. <see cref="First"/>..<see cref="Last"/> are indexes in the rules' session list; a warm-up
    /// belongs to the step that follows it (it is run just before that race).
    /// </summary>
    internal class WeekendStep
    {
        public StepKind Kind;
        public string Label;
        public DateTime Day;
        public int First, Last;
        public readonly List<string> Sessions = new List<string>();
    }

    /// <summary>
    /// The weekend stepper (MM4 §1.5 step 3): ENTRENAMIENTOS → CLASIFICACIÓN → SPRINT (only when the category has one)
    /// → CARRERA, each with its day and a done / current / next state. Built from the real session list of the
    /// category rules (<see cref="WeekendSimulation.Rules"/>), never hard-coded.
    /// </summary>
    internal static class WeekendStepper
    {
        public const float Height = 60;

        public static List<WeekendStep> Steps(WeekendSimulation w)
        {
            return w == null ? new List<WeekendStep>() : Steps(w.Rules, w.Round != null ? w.Round.Date : DateTime.MinValue);
        }

        /// <summary>
        /// Groups the sessions into steps. Days: the rules have no day per session, so they follow the calendar date of
        /// the round (race day) with the championship's usual Friday–Sunday pattern: practice two days before,
        /// qualifying the day before, the last race on race day; with two or more races/sprints the first of them is
        /// on the day before (MotoGP sprint, WorldSBK race 1) and the rest on race day.
        /// </summary>
        public static List<WeekendStep> Steps(CategoryRules rules, DateTime raceDay)
        {
            var steps = new List<WeekendStep>();
            if (rules == null || rules.Weekend == null) return steps;
            var sessions = rules.Weekend.Sessions;
            int pending = -1;
            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                if (s.Type == SessionType.WarmUp)
                {
                    if (pending < 0) pending = i;
                    continue;
                }
                var kind = s.Type == SessionType.Practice ? StepKind.Practice :
                    s.Type == SessionType.Qualifying ? StepKind.Qualifying :
                    s.Type == SessionType.Sprint ? StepKind.Sprint : StepKind.Race;
                string name = Loc.Or("session." + s.Name, s.Name);
                var last = steps.Count > 0 ? steps[steps.Count - 1] : null;
                if (last != null && pending < 0 && last.Kind == kind && (kind == StepKind.Practice || kind == StepKind.Qualifying))
                {
                    last.Last = i;
                    last.Sessions.Add(name);
                    continue;
                }
                var step = new WeekendStep { Kind = kind, First = pending >= 0 ? pending : i, Last = i };
                if (pending >= 0)
                    for (int k = pending; k < i; k++) step.Sessions.Add(Loc.Or("session." + sessions[k].Name, sessions[k].Name));
                step.Sessions.Add(name);
                pending = -1;
                steps.Add(step);
            }
            if (pending >= 0 && steps.Count > 0) steps[steps.Count - 1].Last = sessions.Count - 1;

            int races = 0, sprints = 0;
            foreach (var st in steps)
            {
                if (st.Kind == StepKind.Race) races++;
                if (st.Kind == StepKind.Sprint) sprints++;
            }
            var raceSteps = new List<WeekendStep>();
            foreach (var st in steps)
            {
                switch (st.Kind)
                {
                    case StepKind.Practice: st.Label = Loc.T("ux.mm4c.step.practice"); break;
                    case StepKind.Qualifying: st.Label = Loc.T("ux.mm4c.step.quali"); break;
                    case StepKind.Sprint: st.Label = sprints > 1 ? Last(st) : Loc.T("ux.mm4c.step.sprint"); break;
                    default: st.Label = races > 1 ? Last(st) : Loc.T("ux.mm4c.step.race"); break;
                }
                if (st.Kind == StepKind.Sprint || st.Kind == StepKind.Race) raceSteps.Add(st);
                else st.Day = raceDay.AddDays(st.Kind == StepKind.Practice ? -2 : -1);
            }
            for (int k = 0; k < raceSteps.Count; k++)
                raceSteps[k].Day = raceSteps.Count >= 2 && k == 0 ? raceDay.AddDays(-1) : raceDay;
            // Never a step on an earlier day than the one before it.
            for (int k = 1; k < steps.Count; k++)
                if (steps[k].Day < steps[k - 1].Day) steps[k].Day = steps[k - 1].Day;
            return steps;
        }

        private static string Last(WeekendStep st)
        {
            return st.Sessions.Count > 0 ? st.Sessions[st.Sessions.Count - 1] : "";
        }

        /// <summary>The step that holds session <paramref name="sessionIndex"/> (steps.Count once the weekend is over).</summary>
        public static int StepOf(List<WeekendStep> steps, int sessionIndex)
        {
            for (int i = 0; i < steps.Count; i++)
                if (sessionIndex >= steps[i].First && sessionIndex <= steps[i].Last) return i;
            for (int i = 0; i < steps.Count; i++)
                if (sessionIndex < steps[i].First) return i;
            return steps.Count;
        }

        /// <summary>The last qualifying step (or the current one when the category has none).</summary>
        public static int QualifyingStep(List<WeekendStep> steps, int sessionIndex)
        {
            for (int i = steps.Count - 1; i >= 0; i--)
                if (steps[i].Kind == StepKind.Qualifying) return i;
            return StepOf(steps, sessionIndex);
        }

        /// <summary>"VIE 14" in the player's language.</summary>
        public static string DayText(DateTime d)
        {
            if (d == DateTime.MinValue) return "";
            string day = Loc.Culture.DateTimeFormat.GetAbbreviatedDayName(d.DayOfWeek).TrimEnd('.');
            return Theme.Caps(day) + " " + d.Day;
        }

        /// <summary>
        /// The stepper as a <see cref="Height"/>-unit strip <paramref name="top"/> units below the top of
        /// <paramref name="root"/> (gutters at the sides). Steps before <paramref name="current"/> are done, that one is
        /// current, the rest are next; current = steps.Count marks the whole weekend done.
        /// </summary>
        public static RectTransform Strip(RectTransform root, float top, List<WeekendStep> steps, int current)
        {
            var area = RaceUi.Content(root, top, 0);
            area.anchorMin = new Vector2(0, 1);
            area.offsetMin = new Vector2(RaceUi.Pad, -top - Height);
            area.gameObject.name = "WeekendStepper";
            Draw(area, steps, current);
            return area;
        }

        public static void Draw(RectTransform area, List<WeekendStep> steps, int current)
        {
            int n = steps.Count;
            if (n == 0) return;
            const float arrow = 30;
            for (int i = 0; i < n; i++)
            {
                var st = steps[i];
                bool done = i < current, now = i == current;
                var cell = UiFactory.Rect("Step" + (i + 1), area);
                cell.anchorMin = new Vector2((float)i / n, 0);
                cell.anchorMax = new Vector2((float)(i + 1) / n, 1);
                cell.offsetMin = new Vector2(i > 0 ? arrow / 2 : 0, 0);
                cell.offsetMax = new Vector2(i < n - 1 ? -arrow / 2 : 0, 0);
                var bg = UiFactory.Decor("Bg", cell, now ? Theme.Alpha(Theme.Race, 0.18f) : Theme.Alpha(Theme.Tile, done ? 0.9f : 0.55f));
                UiFactory.Surface(bg);
                UiFactory.Stretch(bg.rectTransform);
                var bar = UiFactory.Decor("State", cell, done ? Theme.Good : now ? Theme.Race : Theme.Alpha(Theme.Steel, 0.45f));
                bar.rectTransform.anchorMin = new Vector2(0, 1);
                bar.rectTransform.anchorMax = new Vector2(1, 1);
                bar.rectTransform.offsetMin = new Vector2(0, -4);
                bar.rectTransform.offsetMax = Vector2.zero;

                string number = (i + 1).ToString("00");
                var label = Ui.Label(cell, Theme.Dim(number) + "  " + Theme.Caps(st.Label), 22, TextAnchor.MiddleLeft,
                    done || now ? Theme.Text : Theme.TextDim, FontStyle.Bold);
                UiFactory.Place(label.rectTransform, 0, 0.44f, 1, 0.94f);
                label.rectTransform.offsetMin = new Vector2(12, 0);
                label.rectTransform.offsetMax = new Vector2(-8, 0);
                string state = done ? Theme.GoodText(Loc.T("ux.mm4c.state.done")) :
                    now ? "<b>" + Theme.TimingText(Loc.T("ux.mm4c.state.now")) + "</b>" : "";
                string day = DayText(st.Day);
                string caption = day + (state.Length > 0 ? (day.Length > 0 ? "  ·  " : "") + state : "");
                var cap = Ui.Label(cell, caption, Theme.TextMin, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
                UiFactory.Place(cap.rectTransform, 0, 0.04f, 1, 0.46f);
                cap.rectTransform.offsetMin = new Vector2(12, 0);
                cap.rectTransform.offsetMax = new Vector2(-8, 0);

                if (i < n - 1)
                {
                    var ch = UiIcons.Chevron(area, done ? Theme.Good : Theme.TextDim);
                    ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2((float)(i + 1) / n, 0.5f);
                    ch.rectTransform.sizeDelta = new Vector2(18, 18);
                    ch.rectTransform.anchoredPosition = Vector2.zero;
                }
            }
        }
    }
}
