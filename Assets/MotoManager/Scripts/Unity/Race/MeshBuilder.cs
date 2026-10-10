using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotoManager.App.Race
{
    /// <summary>Accumulates coloured geometry and produces a single mesh (one draw call per builder).</summary>
    public class MeshBuilder
    {
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<int> _tris = new List<int>();

        public int VertexCount { get { return _verts.Count; } }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
        {
            // a-b-c-d clockwise seen from the normal side (Unity's front face winding).
            var n = Vector3.Cross(b - a, d - a).normalized;
            int i = _verts.Count;
            _verts.Add(a); _verts.Add(b); _verts.Add(c); _verts.Add(d);
            for (int k = 0; k < 4; k++)
            {
                _normals.Add(n);
                _colors.Add(color);
            }
            _tris.Add(i); _tris.Add(i + 1); _tris.Add(i + 2);
            _tris.Add(i); _tris.Add(i + 2); _tris.Add(i + 3);
        }

        /// <summary>Axis-aligned box in the builder's local space, optionally transformed.</summary>
        public void Box(Vector3 center, Vector3 size, Color32 color, Quaternion? rotation = null)
        {
            var r = rotation ?? Quaternion.identity;
            Vector3 h = size * 0.5f;
            Vector3[] c =
            {
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z)
            };
            for (int i = 0; i < 8; i++) c[i] = center + r * c[i];
            Quad(c[0], c[3], c[2], c[1], color); // back (-z)
            Quad(c[5], c[6], c[7], c[4], color); // front (+z)
            Quad(c[4], c[7], c[3], c[0], color); // left (-x)
            Quad(c[1], c[2], c[6], c[5], color); // right (+x)
            Quad(c[3], c[7], c[6], c[2], color); // top
            Quad(c[4], c[0], c[1], c[5], color); // bottom
        }

        private int Vertex(Vector3 p, Vector3 n, Color32 c)
        {
            _verts.Add(p);
            _normals.Add(n);
            _colors.Add(c);
            return _verts.Count - 1;
        }

        /// <summary>One cross-section of a lofted body: a rounded ring around (0, Y) at Z.</summary>
        public struct Section
        {
            public float Z, Y, HalfWidth, HalfHeight;
            public Color32 Color;

            public Section(float z, float y, float halfWidth, float halfHeight, Color32 color)
            {
                Z = z;
                Y = y;
                HalfWidth = halfWidth;
                HalfHeight = halfHeight;
                Color = color;
            }
        }

        /// <summary>
        /// Smooth body through a list of rounded cross-sections along +Z (fairings, tanks, torsos), with closed ends.
        /// Smooth normals, so it reads as a curved shape instead of a stack of boxes.
        /// </summary>
        public void Loft(IList<Section> sections, int sides = 10, Quaternion? rotation = null, Vector3 offset = default(Vector3))
        {
            var r = rotation ?? Quaternion.identity;
            int start = _verts.Count;
            for (int s = 0; s < sections.Count; s++)
            {
                var sec = sections[s];
                for (int k = 0; k < sides; k++)
                {
                    float a = Mathf.PI * 2 * k / sides;
                    // Superellipse-ish profile: rounder than a box, fuller than an ellipse.
                    float cx = Mathf.Sign(Mathf.Cos(a)) * Mathf.Pow(Mathf.Abs(Mathf.Cos(a)), 0.7f);
                    float cy = Mathf.Sign(Mathf.Sin(a)) * Mathf.Pow(Mathf.Abs(Mathf.Sin(a)), 0.7f);
                    var p = new Vector3(cx * sec.HalfWidth, sec.Y + cy * sec.HalfHeight, sec.Z);
                    var n = new Vector3(Mathf.Cos(a) / Mathf.Max(0.01f, sec.HalfWidth), Mathf.Sin(a) / Mathf.Max(0.01f, sec.HalfHeight), 0).normalized;
                    Vertex(offset + r * p, r * n, sec.Color);
                }
            }
            for (int s = 0; s + 1 < sections.Count; s++)
                for (int k = 0; k < sides; k++)
                {
                    int a = start + s * sides + k, b = start + s * sides + (k + 1) % sides;
                    int c = a + sides, d = b + sides;
                    _tris.Add(a); _tris.Add(b); _tris.Add(d);
                    _tris.Add(a); _tris.Add(d); _tris.Add(c);
                }
            // Caps.
            for (int end = 0; end < 2; end++)
            {
                var sec = sections[end == 0 ? 0 : sections.Count - 1];
                var n = r * (end == 0 ? Vector3.back : Vector3.forward);
                int centre = Vertex(offset + r * new Vector3(0, sec.Y, sec.Z), n, sec.Color);
                int ring = _verts.Count;
                int baseIdx = start + (end == 0 ? 0 : (sections.Count - 1) * sides);
                for (int k = 0; k < sides; k++) Vertex(_verts[baseIdx + k], n, sec.Color);
                for (int k = 0; k < sides; k++)
                {
                    int a = ring + k, b = ring + (k + 1) % sides;
                    if (end == 0) { _tris.Add(centre); _tris.Add(b); _tris.Add(a); }
                    else { _tris.Add(centre); _tris.Add(a); _tris.Add(b); }
                }
            }
        }

        /// <summary>Cylinder whose axis is local X (wheels), smooth sides and flat caps.</summary>
        public void Cylinder(Vector3 center, float radius, float width, Color32 color, int sides = 16)
        {
            float h = width * 0.5f;
            int start = _verts.Count;
            for (int k = 0; k < sides; k++)
            {
                float a = Mathf.PI * 2 * k / sides;
                var n = new Vector3(0, Mathf.Sin(a), Mathf.Cos(a));
                Vertex(center + new Vector3(-h, 0, 0) + n * radius, n, color);
                Vertex(center + new Vector3(h, 0, 0) + n * radius, n, color);
            }
            for (int k = 0; k < sides; k++)
            {
                int a = start + k * 2, b = start + ((k + 1) % sides) * 2;
                _tris.Add(a); _tris.Add(a + 1); _tris.Add(b);
                _tris.Add(b); _tris.Add(a + 1); _tris.Add(b + 1);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                var n = new Vector3(side, 0, 0);
                int centre = Vertex(center + n * h, n, color);
                int ring = _verts.Count;
                for (int k = 0; k < sides; k++)
                {
                    float a = Mathf.PI * 2 * k / sides;
                    Vertex(center + n * h + new Vector3(0, Mathf.Sin(a), Mathf.Cos(a)) * radius, n, color);
                }
                for (int k = 0; k < sides; k++)
                {
                    int a = ring + k, b = ring + (k + 1) % sides;
                    if (side > 0) { _tris.Add(centre); _tris.Add(b); _tris.Add(a); }
                    else { _tris.Add(centre); _tris.Add(a); _tris.Add(b); }
                }
            }
        }

        /// <summary>Low-poly UV sphere (helmets).</summary>
        public void Sphere(Vector3 center, Vector3 radii, Color32 color, int rings = 7, int sides = 12, Color32? visor = null)
        {
            int start = _verts.Count;
            for (int i = 0; i <= rings; i++)
            {
                float v = Mathf.PI * i / rings;
                for (int k = 0; k <= sides; k++)
                {
                    float u = Mathf.PI * 2 * k / sides;
                    var n = new Vector3(Mathf.Sin(v) * Mathf.Sin(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Cos(u));
                    // Visor band at the front, just above the equator.
                    bool isVisor = visor.HasValue && n.z > 0.45f && n.y > -0.15f && n.y < 0.35f;
                    Vertex(center + Vector3.Scale(n, radii), n, isVisor ? visor.Value : color);
                }
            }
            int row = sides + 1;
            for (int i = 0; i < rings; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = start + i * row + k, b = a + 1, c = a + row, d = c + 1;
                    _tris.Add(a); _tris.Add(d); _tris.Add(b);
                    _tris.Add(a); _tris.Add(c); _tris.Add(d);
                }
        }

        /// <summary>Flat disc facing up (map markers).</summary>
        public void Disc(Vector3 center, float radius, Color32 color, int sides = 24)
        {
            int c = Vertex(center, Vector3.up, color);
            int ring = _verts.Count;
            for (int k = 0; k < sides; k++)
            {
                float a = Mathf.PI * 2 * k / sides;
                Vertex(center + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * radius, Vector3.up, color);
            }
            for (int k = 0; k < sides; k++)
            {
                _tris.Add(c); _tris.Add(ring + k); _tris.Add(ring + (k + 1) % sides);
            }
        }

        /// <summary>A strip between two polylines (left and right edges) lying on the ground.</summary>
        public void Strip(IList<Vector3> left, IList<Vector3> right, Color32 color, Color32? alternate = null, int alternateEvery = 0)
        {
            for (int i = 0; i + 1 < left.Count; i++)
            {
                var col = alternate.HasValue && alternateEvery > 0 && (i / alternateEvery) % 2 == 1 ? alternate.Value : color;
                Quad(left[i], left[i + 1], right[i + 1], right[i], col);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Material SharedMaterial
        {
            get
            {
                if (_material == null)
                {
                    var shader = Resources.Load<Shader>("Shaders/VertexColorLit");
                    if (shader == null) shader = Shader.Find("Unlit/Color");
                    _material = new Material(shader) { name = "VertexColorLit", enableInstancing = true };
                }
                return _material;
            }
        }

        private static Material _material;

        public GameObject ToObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = SharedMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }
    }
}
