using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Draws a polyline through points given in 0..1 coordinates of its own rect (charts). Each segment is a plain
    /// rotated UI Image (no custom mesh, so it renders under any mask or canvas setup); the segments are laid out
    /// again whenever the rect changes size, the points change or <see cref="SetVerticesDirty"/> is called.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class LineGraphic : MonoBehaviour
    {
        public readonly List<Vector2> Points = new List<Vector2>();
        public float Thickness = 3f;
        public bool raycastTarget;

        private Color _color = Color.white;
        private readonly List<Image> _segments = new List<Image>();
        private readonly List<Image> _joints = new List<Image>();
        private Vector2 _builtSize = new Vector2(-1, -1);
        private int _builtCount = -1;
        private bool _dirty = true;

        public RectTransform rectTransform { get { return (RectTransform)transform; } }

        public Color color
        {
            get { return _color; }
            set
            {
                _color = value;
                foreach (var s in _segments) s.color = value;
                foreach (var j in _joints) j.color = value;
            }
        }

        /// <summary>Asks for the segments to be laid out again (kept from the mesh version's API).</summary>
        public void SetVerticesDirty()
        {
            _dirty = true;
        }

        private void OnEnable()
        {
            _dirty = true;
        }

        private void OnRectTransformDimensionsChange()
        {
            _dirty = true;
        }

        private void LateUpdate()
        {
            var size = rectTransform.rect.size;
            if (!_dirty && size == _builtSize && Points.Count == _builtCount) return;
            if (size.x <= 0 || size.y <= 0) return;
            Rebuild(size);
        }

        private Image Piece(List<Image> pool, int index, string name)
        {
            while (pool.Count <= index)
            {
                var rt = UiFactory.Rect(name, transform);
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                var img = rt.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                img.color = _color;
                pool.Add(img);
            }
            var piece = pool[index];
            piece.gameObject.SetActive(true);
            return piece;
        }

        private void Rebuild(Vector2 size)
        {
            _dirty = false;
            _builtSize = size;
            _builtCount = Points.Count;
            int segments = 0, joints = 0;
            for (int i = 0; i < Points.Count; i++)
            {
                var a = new Vector2(Points[i].x * size.x, Points[i].y * size.y);
                // Round-ish joint so thick lines don't show gaps at the corners.
                if (Thickness >= 3f)
                {
                    var j = Piece(_joints, joints++, "Joint");
                    var jr = j.rectTransform;
                    jr.pivot = new Vector2(0.5f, 0.5f);
                    jr.anchoredPosition = a;
                    jr.sizeDelta = new Vector2(Thickness, Thickness);
                    jr.localRotation = Quaternion.Euler(0, 0, 45);
                }
                if (i + 1 >= Points.Count) break;
                var b = new Vector2(Points[i + 1].x * size.x, Points[i + 1].y * size.y);
                var d = b - a;
                float len = d.magnitude;
                if (len < 0.01f) continue;
                var s = Piece(_segments, segments++, "Segment");
                var sr = s.rectTransform;
                sr.pivot = new Vector2(0, 0.5f);
                sr.anchoredPosition = a;
                sr.sizeDelta = new Vector2(len, Thickness);
                sr.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }
            for (int i = segments; i < _segments.Count; i++) _segments[i].gameObject.SetActive(false);
            for (int i = joints; i < _joints.Count; i++) _joints[i].gameObject.SetActive(false);
        }
    }
}
