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
