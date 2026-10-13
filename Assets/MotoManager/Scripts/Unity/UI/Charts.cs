using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>One line of a chart.</summary>
    public class ChartSeries
    {
        public string Name;
        public Color Color = Color.white;
        /// <summary>Values by x step; NaN leaves a gap (a rider who retired).</summary>
        public List<double> Values = new List<double>();
        public bool Highlight;
    }

    /// <summary>
    /// Line charts for the management and race screens: a dark plot with guide lines, the value scale on the left,
    /// x labels underneath, one polyline per series (the player's thicker) and a legend.
    /// </summary>
    public static class Charts
    {
        /// <summary>
        /// Adds a chart of the given height to a list. <paramref name="invert"/> puts the lowest value at the top
        /// (gaps to the leader, positions). <paramref name="format"/> formats the scale values.
        /// </summary>
        public static RectTransform LineChart(Transform list, float height, List<ChartSeries> series, bool invert, Func<double, string> format, string xFirst, string xLast)
        {
            double min = double.MaxValue, max = double.MinValue;
            int steps = 0;
            foreach (var s in series)
            {
                steps = Math.Max(steps, s.Values.Count);
                foreach (var v in s.Values)
                {
                    if (double.IsNaN(v)) continue;
                    min = Math.Min(min, v);
                    max = Math.Max(max, v);
                }
            }
            if (min == double.MaxValue) { min = 0; max = 1; }
            if (max - min < 1e-6) max = min + 1;

            var row = UiFactory.Row(list, height);
            var plot = UiFactory.Panel("Plot", row, new Color(0, 0, 0, 0.35f));
            UiFactory.Place(plot.rectTransform, 0.08f, 0.12f, 1, 1);
            plot.raycastTarget = false;
            // Guide lines and scale.
            for (int g = 0; g <= 4; g++)
            {
                float y = g / 4f;
                var line = UiFactory.Decor("Guide", plot.transform, new Color(1, 1, 1, g == 0 || g == 4 ? 0.12f : 0.05f));
                line.rectTransform.anchorMin = new Vector2(0, y);
                line.rectTransform.anchorMax = new Vector2(1, y);
                line.rectTransform.offsetMin = new Vector2(0, -1);
                line.rectTransform.offsetMax = new Vector2(0, 1);
                double value = invert ? max - (max - min) * y : min + (max - min) * y;
                var label = UiFactory.Label("Scale", row, format(value), Theme.TextMin, TextAnchor.MiddleRight, Theme.TextDim);
                float yy = 0.12f + y * 0.88f;
                UiFactory.Place(label.rectTransform, 0, yy - 0.07f, 0.075f, yy + 0.07f);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
            }
            var first = UiFactory.Label("X0", row, xFirst, Theme.TextMin, TextAnchor.UpperLeft, Theme.TextDim);
            first.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.Place(first.rectTransform, 0.08f, 0, 0.4f, 0.11f);
            var last = UiFactory.Label("X1", row, xLast, Theme.TextMin, TextAnchor.UpperRight, Theme.TextDim);
            last.verticalOverflow = VerticalWrapMode.Overflow;
            UiFactory.Place(last.rectTransform, 0.6f, 0, 1, 0.11f);

            // Lines: the highlighted ones last so they sit on top.
            var ordered = new List<ChartSeries>(series);
            ordered.Sort((a, b) => a.Highlight.CompareTo(b.Highlight));
            foreach (var s in ordered)
            {
                var g = UiFactory.Rect("Series", plot.transform).gameObject.AddComponent<LineGraphic>();
                UiFactory.Stretch(g.rectTransform);
                g.raycastTarget = false;
                g.color = s.Color;
                g.Thickness = s.Highlight ? 5f : 2.5f;
                for (int i = 0; i < s.Values.Count; i++)
                {
                    double v = s.Values[i];
                    if (double.IsNaN(v)) break;
                    float x = steps > 1 ? (float)i / (steps - 1) : 0.5f;
                    float y = (float)((v - min) / (max - min));
                    if (invert) y = 1 - y;
                    g.Points.Add(new Vector2(x, Mathf.Clamp01(y)));
                }
                g.SetVerticesDirty();
            }

            // Legend: coloured marks and names (up to two rows).
            var legend = UiFactory.Row(list, series.Count > 4 ? 72 : 36);
            int perRow = Math.Min(4, Math.Max(1, series.Count));
            for (int i = 0; i < series.Count && i < 8; i++)
            {
                var s = series[i];
                int col = i % perRow, line = i / perRow;
                float x0 = (float)col / perRow, x1 = (float)(col + 1) / perRow;
                float y1 = series.Count > 4 ? (line == 0 ? 1f : 0.5f) : 1f, y0 = series.Count > 4 ? y1 - 0.5f : 0f;
                var mark = UiFactory.Decor("Mark", legend, s.Color);
                UiFactory.Place(mark.rectTransform, x0, y0 + (y1 - y0) * 0.35f, x0 + 0.02f, y0 + (y1 - y0) * 0.65f);
                var t = UiFactory.Label("Name", legend, s.Highlight ? "<b>" + s.Name + "</b>" : s.Name, Theme.TextCaption, TextAnchor.MiddleLeft, s.Highlight ? Theme.Text : Theme.TextDim);
                t.supportRichText = true;
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = Theme.TextMin;
                t.resizeTextMaxSize = Theme.TextCaption;
                UiFactory.Place(t.rectTransform, x0 + 0.025f, y0, x1 - 0.01f, y1);
            }
            return row;
        }

        /// <summary>A colour that reads on the dark plot (team colours that are too dark or grey are lifted).</summary>
        public static Color Readable(string hex, Color fallback)
        {
            Color c = UiFactory.ParseColor(hex, fallback);
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            if (s < 0.2f) return Color.Lerp(c, Color.white, 0.6f);
            return Color.HSVToRGB(h, Mathf.Clamp(s, 0.45f, 0.95f), Mathf.Clamp(v, 0.7f, 1f));
        }
    }
}
