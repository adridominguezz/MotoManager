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
            p.Build(UiFactory.SafeRoot(canvas, Theme.Background), weekend, eventName);
        }

        private void Build(RectTransform root, WeekendSimulation weekend, string eventName)
        {
            var bg = UiFactory.Panel("Background", root, Theme.Background);
            UiFactory.Stretch(bg.rectTransform);
            var title = UiFactory.Label("Title", root, "<b>" + Loc.T("quali.title") + "</b>  " + Theme.Dim(eventName), Theme.FontLarge - 4, TextAnchor.MiddleLeft);
            UiFactory.RaceTitle(title);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = Theme.FontSmall;
            title.resizeTextMaxSize = Theme.FontLarge - 4;
            UiFactory.Place(title.rectTransform, 0.03f, 0.88f, 0.62f, 0.99f);

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
            UiFactory.Place(left, 0.02f, 0.02f, 0.36f, 0.86f);
            var layout = left.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            for (int i = 0; i < order.Count; i++)
            {
                RaceEntry e;
                if (!entries.TryGetValue(order[i], out e) || !e.IsPlayer) continue;
                double t;
                best.TryGetValue(e.Rider.Id, out t);
                BigCard(left, e, i + 1, t > 0 ? MathX.FormatLapTime(t) : "-",
                    i == 0 ? Theme.GoodText(Loc.T("quali.pole")) : t > 0 && pole > 0 ? "+" + (t - pole).ToString("0.000", Loc.Culture) : "-");
            }

            // Right: the grid.
            var list = UiFactory.ScrollList("Grid", root, 2);
            UiFactory.Place((RectTransform)list.parent.parent, 0.38f, 0.02f, 0.99f, 0.86f);
            var w = new[] { 0.8f, 4f, 3f, 2f, 1.6f };
            UiFactory.TableRow(list, 34, w, new[] { Loc.T("post.col.pos"), Loc.T("col.rider"), Loc.T("col.team"), Loc.T("quali.best"), Loc.T("quali.gap") }, true);
            for (int i = 0; i < order.Count; i++)
            {
                RaceEntry e;
                if (!entries.TryGetValue(order[i], out e)) continue;
                double t;
                best.TryGetValue(e.Rider.Id, out t);
                string gap = i == 0 ? "" : t > 0 && pole > 0 ? "+" + (t - pole).ToString("0.000", Loc.Culture) : "";
                UiFactory.TableRow(list, 46, w, new[]
                {
                    (i + 1).ToString(), (e.IsPlayer ? "<b>" : "") + e.Rider.FullName + (e.IsPlayer ? "</b>" : ""),
                    e.Team != null ? e.Team.ShortName ?? e.Team.Name : "", t > 0 ? MathX.FormatLapTime(t) : "-", gap
                }, false, e.IsPlayer ? new Color(0.94f, 0.7f, 0.16f, 0.16f) : (i % 3 == 0 ? new Color(1, 1, 1, 0.03f) : (Color?)null));
            }

            var go = UiFactory.Button("Continue", root, Theme.Caps(Loc.T("quali.continue")) + "  ›", Continue, Theme.Accent, Theme.FontLarge - 4);
            UiFactory.Slant(go.image, 2.4f);
            go.GetComponentInChildren<UnityEngine.UI.Text>().fontStyle = FontStyle.BoldAndItalic;
            UiFactory.Place((RectTransform)go.transform, 0.64f, 0.885f, 0.99f, 0.99f, 2);
        }

        /// <summary>A rider's qualifying in one big card: grid slot in a timing-yellow block, name, best lap and gap.</summary>
        private static void BigCard(RectTransform parent, RaceEntry e, int grid, string time, string gap)
        {
            var card = UiFactory.Panel("Rider", parent, Theme.Panel);
            UiFactory.Chamfer(card, 1.4f);
            card.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 190;
            var teamColor = UiFactory.ParseColor(e.Team != null ? e.Team.Colors.Primary : null, Theme.Accent);
            var stripe = UiFactory.Panel("Team", card.transform, teamColor);
            UiFactory.Place(stripe.rectTransform, 0, 0, 0.02f, 1);
            var pos = UiFactory.Panel("Grid", card.transform, Theme.Timing);
            UiFactory.Slant(pos, 2.4f);
            UiFactory.Place(pos.rectTransform, 0.05f, 0.3f, 0.36f, 0.9f);
            var posText = UiFactory.Label("P", pos.transform, grid + "º", Theme.FontTitle, TextAnchor.MiddleCenter, new Color32(12, 13, 16, 255));
            posText.fontStyle = FontStyle.BoldAndItalic;
            posText.resizeTextForBestFit = true;
            posText.resizeTextMinSize = Theme.FontNormal;
            posText.resizeTextMaxSize = Theme.FontTitle;
            UiFactory.Stretch(posText.rectTransform);
            var first = UiFactory.Label("First", card.transform, e.Rider.FirstName, Theme.FontSmall - 4, TextAnchor.LowerLeft, Theme.TextDim);
            UiFactory.Place(first.rectTransform, 0.4f, 0.62f, 0.98f, 0.92f);
            var last = UiFactory.Label("Last", card.transform, Theme.Caps(e.Rider.LastName), Theme.FontLarge, TextAnchor.UpperLeft);
            last.fontStyle = FontStyle.BoldAndItalic;
            last.resizeTextForBestFit = true;
            last.resizeTextMinSize = Theme.FontSmall;
            last.resizeTextMaxSize = Theme.FontLarge;
            UiFactory.Place(last.rectTransform, 0.4f, 0.3f, 0.98f, 0.62f);
            var line = UiFactory.Label("Time", card.transform, "<b>" + time + "</b>   " + gap, Theme.FontSmall, TextAnchor.MiddleLeft);
            line.supportRichText = true;
            line.resizeTextForBestFit = true;
            line.resizeTextMinSize = Theme.FontSmall - 8;
            line.resizeTextMaxSize = Theme.FontSmall;
            UiFactory.Place(line.rectTransform, 0.05f, 0.04f, 0.98f, 0.28f);
        }

        private void Continue()
        {
            Destroy(gameObject);
            if (_onContinue != null) _onContinue();
        }
    }
}
