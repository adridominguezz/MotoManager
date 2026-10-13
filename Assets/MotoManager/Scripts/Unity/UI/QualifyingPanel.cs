using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using MotoManager.Core.Season;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Qualifying result before the race: where the player's riders start (big cards, left) and the full grid with
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
            var root = UiFactory.SafeRoot(canvas, Theme.Background);
            RaceUi.HostSheets(canvas.gameObject, root);
            p.Build(root, weekend, eventName);
        }

        private void Build(RectTransform root, WeekendSimulation weekend, string eventName)
        {
            RaceUi.Backdrop(root);
            RectTransform cta;
            RaceUi.TopBar(root, Loc.T("quali.title"), eventName, 400, out cta);
            Ui.PrimaryCTA(cta, Loc.T("ux.race.toRace"), Continue);

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

            var content = RaceUi.Content(root, RaceUi.BarH + 12, RaceUi.Pad);
            var cols = Ui.Columns12(content, 5, 7);

            // Left: the player's riders, one big card each.
            var mine = new List<int>();
            for (int i = 0; i < order.Count; i++)
            {
                RaceEntry e;
                if (entries.TryGetValue(order[i], out e) && e.IsPlayer) mine.Add(i);
            }
            if (mine.Count > 0)
            {
                var shares = new float[Mathf.Max(2, mine.Count)];
                for (int k = 0; k < shares.Length; k++) shares[k] = 1;
                var slots = Ui.Rows(cols[0], shares);
                for (int k = 0; k < mine.Count; k++)
                {
                    int i = mine[k];
                    var e = entries[order[i]];
                    double t;
                    best.TryGetValue(e.Rider.Id, out t);
                    string gap = i == 0 ? Theme.GoodText(Loc.T("quali.pole")) : t > 0 && pole > 0 ? "+" + (t - pole).ToString("0.000", Loc.Culture) : "-";
                    BigCard(slots[k], e, i + 1, t > 0 ? MathX.FormatLapTime(t) : "-", gap, order.Count);
                }
            }

            // Right: the grid (P | rider | best lap | gap to pole).
            var gridBody = Ui.Tile(cols[1], Loc.T("ux.race.grid"), null, order.Count + " " + Theme.Caps(Loc.T("ux.race.riders")));
            gridBody.offsetMin = new Vector2(0, 0);
            gridBody.offsetMax = new Vector2(0, -Ui.Grid.Header);
            var w = new[] { 1f, 5f, 2.6f, 2f };
            var captions = UiFactory.Rect("Captions", gridBody);
            captions.anchorMin = new Vector2(0, 1);
            captions.anchorMax = new Vector2(1, 1);
            captions.pivot = new Vector2(0.5f, 1);
            captions.offsetMin = new Vector2(0, -40);
            captions.offsetMax = Vector2.zero;
            var capList = captions.gameObject.AddComponent<VerticalLayoutGroup>();
            capList.childControlHeight = true;
            capList.childControlWidth = true;
            capList.childForceExpandWidth = true;
            Ui.ListCaptions(captions, w, new[] { Loc.T("post.col.pos"), Loc.T("col.rider"), Loc.T("quali.best"), Loc.T("quali.gap") });
            var listHolder = UiFactory.Rect("Rows", gridBody);
            UiFactory.Stretch(listHolder);
            listHolder.offsetMax = new Vector2(0, -40);
            var list = UiFactory.ScrollList("Grid", listHolder, 0);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 0;
            layout.padding = new RectOffset(0, 0, 0, 8);
            for (int i = 0; i < order.Count; i++)
            {
                RaceEntry e;
                if (!entries.TryGetValue(order[i], out e)) continue;
                double t;
                best.TryGetValue(e.Rider.Id, out t);
                string gap = i == 0 ? "" : t > 0 && pole > 0 ? "+" + (t - pole).ToString("0.000", Loc.Culture) : "";
                var row = Ui.ListRow(list, w, new[]
                {
                    (i + 1).ToString(), Theme.Caps(e.Rider.FullName), t > 0 ? MathX.FormatLapTime(t) : "-", Theme.Dim(gap)
                }, i, e.IsPlayer, null, 56);
                RaceUi.TeamBar(row, w, RaceUi.TeamColor(e.Team != null ? e.Team.Colors.Primary : null));
            }
        }

        /// <summary>A rider's qualifying in one big card: grid slot in a timing-yellow block, name, row, best lap and gap.</summary>
        private static void BigCard(RectTransform area, RaceEntry e, int grid, string time, string gap, int field)
        {
            var teamColor = RaceUi.TeamColor(e.Team != null ? e.Team.Colors.Primary : null);
            var body = Ui.Tile(area, null, null, null, 0, null, null, false, teamColor);
            var slot = UiFactory.Rect("Slot", body);
            slot.anchorMin = new Vector2(0, 0.12f);
            slot.anchorMax = new Vector2(0, 0.88f);
            slot.pivot = new Vector2(0, 0.5f);
            slot.offsetMin = new Vector2(0, 0);
            slot.offsetMax = new Vector2(176, 0);
            RaceUi.Block(slot, Theme.Pos(grid), Theme.Timing, Theme.TextDisplay);

            var plateArea = UiFactory.Rect("PlateArea", body);
            plateArea.anchorMin = new Vector2(1, 1);
            plateArea.anchorMax = new Vector2(1, 1);
            plateArea.pivot = new Vector2(1, 1);
            plateArea.sizeDelta = new Vector2(76, 56);
            plateArea.anchoredPosition = Vector2.zero;
            RaceUi.Plate(plateArea, e.Rider.Number, teamColor, Theme.TextTitle);

            var first = RaceUi.Label(body, e.Rider.FirstName, Theme.TextCaption, TextAnchor.LowerLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(first.rectTransform, 0, 0.72f, 1, 1);
            first.rectTransform.offsetMin = new Vector2(196, 0);
            first.rectTransform.offsetMax = new Vector2(-88, 0);
            var last = RaceUi.Label(body, Theme.Caps(e.Rider.LastName), Theme.FontLarge, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.DropShadow(last, 0.4f, 2);
            UiFactory.Place(last.rectTransform, 0, 0.42f, 1, 0.74f);
            last.rectTransform.offsetMin = new Vector2(196, 0);
            last.rectTransform.offsetMax = new Vector2(-8, 0);
            var line = RaceUi.Label(body, "<b>" + time + "</b>   " + gap, Theme.TextBody, TextAnchor.MiddleLeft, Theme.Text);
            UiFactory.Place(line.rectTransform, 0, 0.18f, 1, 0.42f);
            line.rectTransform.offsetMin = new Vector2(196, 0);
            int row = (grid - 1) / 3 + 1;
            var rowText = RaceUi.Label(body, Theme.Caps(Loc.T("quali.row", row)), Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(rowText.rectTransform, 0, 0, 1, 0.18f);
            rowText.rectTransform.offsetMin = new Vector2(196, 0);
        }

        private void Continue()
        {
            Destroy(gameObject);
            if (_onContinue != null) _onContinue();
        }
    }
}
