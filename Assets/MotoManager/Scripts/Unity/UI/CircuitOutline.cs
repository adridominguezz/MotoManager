using MotoManager.Core.Data;
using MotoManager.Core.Race;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// A circuit's real line drawn flat in a rect (home's next race, calendar): the centre line from
    /// <see cref="TrackGeometry"/> fitted with its aspect kept, and a short accent mark on the finish line.
    /// </summary>
    public static class CircuitOutline
    {
        private const int MaxPoints = 260;

        public static RectTransform Draw(RectTransform area, CircuitDefinition circuit, Color color, float thickness = 5)
        {
            var holder = UiFactory.Rect("Circuit", area);
            UiFactory.Stretch(holder);
            if (circuit == null) return holder;
            TrackGeometry geo;
            try { geo = new TrackGeometry(TrackModel.Get(circuit)); }
            catch (System.Exception) { return holder; }
            double w = geo.MaxX - geo.MinX, h = geo.MaxY - geo.MinY;
            if (w <= 0 || h <= 0) return holder;

            // A square-ish box kept to the line's aspect inside the area.
            var box = UiFactory.Rect("Box", holder);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            var fit = box.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)(w / h);

            var line = box.gameObject.AddComponent<LineGraphic>();
            line.Thickness = thickness;
            line.color = color;
            int n = geo.PointCount;
            int step = Mathf.Max(1, n / MaxPoints);
            for (int i = 0; i < n; i += step)
                line.Points.Add(new Vector2((float)((geo.X[i] - geo.MinX) / w), (float)((geo.Y[i] - geo.MinY) / h)));
            line.Points.Add(line.Points[0]);
            line.SetVerticesDirty();

            // Finish line: a short accent bar across the track at the first point.
            var mark = UiFactory.Decor("Finish", box, Theme.Accent);
            mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = line.Points[0];
            mark.rectTransform.sizeDelta = new Vector2(thickness * 4, thickness * 1.4f);
            mark.rectTransform.localRotation = Quaternion.Euler(0, 0, (float)(geo.Heading[0] * Mathf.Rad2Deg) + 90);
            return holder;
        }
    }
}
