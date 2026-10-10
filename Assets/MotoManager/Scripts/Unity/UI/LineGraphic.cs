using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Draws a polyline through points given in 0..1 coordinates of its own rect (charts). One mesh, no textures;
    /// it follows the rect when the layout changes size.
    /// </summary>
    public class LineGraphic : MaskableGraphic
    {
        public readonly List<Vector2> Points = new List<Vector2>();
        public float Thickness = 3f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            for (int i = 0; i + 1 < Points.Count; i++)
            {
                var a = new Vector2(r.xMin + Points[i].x * r.width, r.yMin + Points[i].y * r.height);
                var b = new Vector2(r.xMin + Points[i + 1].x * r.width, r.yMin + Points[i + 1].y * r.height);
                var dir = (b - a).normalized;
                var n = new Vector2(-dir.y, dir.x) * Thickness * 0.5f;
                int k = vh.currentVertCount;
                vh.AddVert(a - n, color, Vector2.zero);
                vh.AddVert(a + n, color, Vector2.zero);
                vh.AddVert(b + n, color, Vector2.zero);
                vh.AddVert(b - n, color, Vector2.zero);
                vh.AddTriangle(k, k + 1, k + 2);
                vh.AddTriangle(k, k + 2, k + 3);
            }
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }
    }
}
