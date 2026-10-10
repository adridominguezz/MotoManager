using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using MotoManager.Core.Season;
using UnityEngine;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Qualifying result before the race: where the player's riders start (big, on the left) and the full grid with
    /// best times and gaps to pole (right). Qualifying itself is simulated in the background by the weekend.
    /// </summary>
    public class QualifyingPanel : MonoBehaviour
    {
        private Action _onContinue;

        public static void Show(WeekendSimulation weekend, string eventName, Action onContinue)
        {
            var canvas = UiFactory.CreateCanvas("Qualifying", 20);
            var p = canvas.gameObject.AddComponent<QualifyingPanel>();
            p._onContinue = onContinue;
            p.Build((RectTransform)canvas.transform, weekend, eventName);
        }

        private void Build(RectTransform root, WeekendSimulation weekend, string eventName)
        {
            var bg = UiFactory.Panel("Background", root, Theme.Background);
            UiFactory.Stretch(bg.rectTransform);
            var title = UiFactory.Label("Title", root, "<b>" + Loc.T("quali.title") + "</b>  " + Theme.Dim(eventName), Theme.FontLarge - 4, TextAnchor.MiddleLeft);
            title.supportRichText = true;
            UiFactory.Place(title.rectTransform, 0.03f, 0.89f, 0.97f, 0.98f);

            // Best qualifying lap of every rider (all qualifying sessions: Q1/Q2, Superpole...).
            var best = new Dictionary<string, double>();
            foreach (var s in weekend.Result.Sessions)
            {
                if (s.Type != SessionType.Qualifying) continue;
                foreach (var e in s.Classification)
                {
                    if (e.BestLap <= 0) continue;
                    double b;
                    if (!best.TryGetValue(e.RiderId, out b) || e.BestLap < b) best[e.RiderId] = e.BestLap;
                }
            }
            var entries = new Dictionary<string, RaceEntry>();
            foreach (var e in weekend.Entries) entries[e.Rider.Id] = e;
            var order = weekend.QualifyingOrder;
            double pole = order.Count > 0 && best.ContainsKey(order[0]) ? best[order[0]] : 0;

            // Left: the player's riders.
            var left = UiFactory.Rect("Mine", root);
            UiFactory.Place(left, 0.03f, 0.12f, 0.36f, 0.87f);
            var layout = left.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            for (int i = 0; i < order.Count; i++)
            {
                RaceEntry e;
                if (!entries.TryGetValue(order[i], out e) || !e.IsPlayer) continue;
                var card = Ui.Card(left, e.Rider.FullName, e.Team != null ? e.Team.Name : null);
                double t;
                best.TryGetValue(e.Rider.Id, out t);
                Ui.Kpis(card,
                    Ui.Kpi("<size=" + (Theme.FontTitle) + ">P" + (i + 1) + "</size>", Loc.T("quali.grid")),
                    Ui.Kpi(t > 0 ? MathX.FormatLapTime(t) : "-", Loc.T("quali.best")),
                    Ui.Kpi(i == 0 ? Theme.GoodText(Loc.T("quali.pole")) : t > 0 && pole > 0 ? "+" + (t - pole).ToString("0.000", Loc.Culture) : "-", Loc.T("quali.gap")));
                Ui.Note(card, Loc.T("quali.row", (i / 3) + 1));
            }

            // Right: the grid.
            var list = UiFactory.ScrollList("Grid", root, 2);
            UiFactory.Place((RectTransform)list.parent.parent, 0.38f, 0.12f, 0.97f, 0.87f);
            var w = new[] { 0.8f, 4f, 3f, 2f, 1.6f };
            UiFactory.TableRow(list, 34, w, new[] { Loc.T("post.col.pos"), Loc.T("col.rider"), Loc.T("col.team"), Loc.T("quali.best"), Loc.T("quali.gap") }, true);
            for (int i = 0; i < order.Count; i++)
            {
                RaceEntry e;
                if (!entries.TryGetValue(order[i], out e)) continue;
                double t;
                best.TryGetValue(e.Rider.Id, out t);
                string gap = i == 0 ? "" : t > 0 && pole > 0 ? "+" + (t - pole).ToString("0.000", Loc.Culture) : "";
                UiFactory.TableRow(list, 36, w, new[]
                {
                    (i + 1).ToString(), (e.IsPlayer ? "<b>" : "") + e.Rider.FullName + (e.IsPlayer ? "</b>" : ""),
                    e.Team != null ? e.Team.ShortName ?? e.Team.Name : "", t > 0 ? MathX.FormatLapTime(t) : "-", gap
                }, false, e.IsPlayer ? new Color(0.94f, 0.7f, 0.16f, 0.16f) : (i % 3 == 0 ? new Color(1, 1, 1, 0.03f) : (Color?)null));
            }

            var go = UiFactory.Button("Continue", root, Loc.T("quali.continue"), Continue, Theme.Accent, Theme.FontNormal);
            UiFactory.Place((RectTransform)go.transform, 0.7f, 0.02f, 0.97f, 0.1f, 4);
        }

        private void Continue()
        {
            Destroy(gameObject);
            if (_onContinue != null) _onContinue();
        }
    }
}
