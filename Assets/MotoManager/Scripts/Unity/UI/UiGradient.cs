using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Multiplies the vertex colours of a graphic by a two-stop gradient (top to bottom, or left to right), so panels
    /// and buttons get depth without extra textures. It multiplies rather than replaces, so a tint set later on the
    /// graphic (selected / unselected states) keeps the same shading. The mesh is only touched when the graphic is
    /// rebuilt (no per-frame cost) and the result still batches with the other UI.
    /// </summary>
    public class UiGradient : BaseMeshEffect
    {
        /// <summary>Multiplier at the top (or left when <see cref="Horizontal"/>); values above 1 brighten.</summary>
        public Color Top = Color.white;
        /// <summary>Multiplier at the bottom (or right when <see cref="Horizontal"/>).</summary>
        public Color Bottom = Color.white;
        public bool Horizontal;

        public void Set(Color top, Color bottom, bool horizontal = false)
        {
            Top = top;
            Bottom = bottom;
            Horizontal = horizontal;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var rect = graphic.rectTransform.rect;
            float min = Horizontal ? rect.xMin : rect.yMin;
            float size = Horizontal ? rect.width : rect.height;
            if (size <= 0.01f) return;
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float t = Mathf.Clamp01(((Horizontal ? v.position.x : v.position.y) - min) / size);
                // t = 0 at the bottom/left; vertical gradients go from Bottom (t=0) to Top (t=1).
                Color m = Horizontal ? Color.LerpUnclamped(Top, Bottom, t) : Color.LerpUnclamped(Bottom, Top, t);
                Color c = v.color;
                v.color = new Color(c.r * m.r, c.g * m.g, c.b * m.b, c.a * m.a);
                vh.SetUIVertex(v, i);
            }
        }
    }
}
