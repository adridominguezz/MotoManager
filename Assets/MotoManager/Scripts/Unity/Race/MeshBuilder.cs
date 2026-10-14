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

        public int TriangleCount { get { return _tris.Count / 3; } }

        /// <summary>
        /// One cross-section of a <see cref="Hull"/>: the right half of a closed outline at Z, listed from the bottom
        /// centre (x = 0) up to the top centre (x = 0). The left half is mirrored. <see cref="Bands"/>[i] colours the
        /// strip between Half[i] and Half[i + 1] on both sides, from this ring to the next one.
        /// </summary>
        public struct Ring
        {
            public float Z;
            public Vector2[] Half;
            public Color32[] Bands;

            public Ring(float z, Vector2[] half, Color32[] bands)
            {
                Z = z;
                Half = half;
                Bands = bands;
            }
        }

        /// <summary>
        /// Symmetric body skinned through shaped cross-sections along +Z (fairings, seat units, screens). Normals are
        /// smooth, but colour bands keep crisp edges (liveries, number plates) because each band has its own vertices.
        /// All rings must have the same number of points. The end rings are closed with flat caps (null leaves that end open).
        /// </summary>
        public void Hull(IList<Ring> rings, Color32? backCap, Color32? frontCap)
        {
            Hull(rings, backCap, frontCap, null);
        }

        /// <summary>
        /// <see cref="Hull(IList{Ring},Color32?,Color32?)"/> with per-triangle colours: <paramref name="paint"/>(ring s,
        /// band i, triangle t) colours the two triangles of the quad between rings s and s + 1 and points i and i + 1
        /// (t = 0: the triangle along ring s's edge and the upper corner of ring s + 1; t = 1: the one along ring s + 1's
        /// lower edge), so liveries can step diagonally across a quad. Null uses the rings' bands.
        /// </summary>
        public void Hull(IList<Ring> rings, Color32? backCap, Color32? frontCap, System.Func<int, int, int, Color32> paint)
        {
            int n = rings[0].Half.Length;
            int count = rings.Count;
            // Smooth normals of the right half, from the ring tangent and the tangent along Z.
            var normals = new Vector3[count, n];
            for (int s = 0; s < count; s++)
                for (int i = 0; i < n; i++)
                {
                    var prev = rings[s].Half[Mathf.Max(0, i - 1)];
                    var next = rings[s].Half[Mathf.Min(n - 1, i + 1)];
                    var tr = new Vector3(next.x - prev.x, next.y - prev.y, 0);
                    // On the centre line the ring tangent is horizontal (mirror symmetry).
                    if (i == 0) tr = new Vector3(1, 0, 0) * Mathf.Max(0.0001f, next.x);
                    if (i == n - 1) tr = new Vector3(-1, 0, 0) * Mathf.Max(0.0001f, prev.x);
                    int s0 = Mathf.Max(0, s - 1), s1 = Mathf.Min(count - 1, s + 1);
                    var a = rings[s0].Half[i];
                    var b = rings[s1].Half[i];
                    var tz = new Vector3(b.x - a.x, b.y - a.y, rings[s1].Z - rings[s0].Z);
                    var nn = Vector3.Cross(tr, tz);
                    normals[s, i] = nn.sqrMagnitude > 1e-12f ? nn.normalized : Vector3.up;
                }
            for (int side = -1; side <= 1; side += 2)
            {
                for (int s = 0; s + 1 < count; s++)
                {
                    var r0 = rings[s];
                    var r1 = rings[s + 1];
                    for (int i = 0; i + 1 < n; i++)
                    {
                        Color32 col, col2;
                        if (paint != null) { col = paint(s, i, 0); col2 = paint(s, i, 1); }
                        else col = col2 = r0.Bands[Mathf.Min(i, r0.Bands.Length - 1)];
                        int a = HullVertex(r0, s, i, side, normals, col);
                        int b = HullVertex(r0, s, i + 1, side, normals, col);
                        int d = HullVertex(r1, s + 1, i + 1, side, normals, col);
                        int a2 = a, d2 = d;
                        if (!Same(col, col2))
                        {
                            a2 = HullVertex(r0, s, i, side, normals, col2);
                            d2 = HullVertex(r1, s + 1, i + 1, side, normals, col2);
                        }
                        int c = HullVertex(r1, s + 1, i, side, normals, col2);
                        if (side > 0) { _tris.Add(a); _tris.Add(b); _tris.Add(d); _tris.Add(a2); _tris.Add(d2); _tris.Add(c); }
                        else { _tris.Add(a); _tris.Add(d); _tris.Add(b); _tris.Add(a2); _tris.Add(c); _tris.Add(d2); }
                    }
                }
            }
            if (backCap.HasValue) HullCap(rings[0], false, backCap.Value);
            if (frontCap.HasValue) HullCap(rings[count - 1], true, frontCap.Value);
        }

        private static bool Same(Color32 x, Color32 y)
        {
            return x.r == y.r && x.g == y.g && x.b == y.b && x.a == y.a;
        }

        private int HullVertex(Ring r, int s, int i, int side, Vector3[,] normals, Color32 col)
        {
            var p = r.Half[i];
            var nn = normals[s, i];
            return Vertex(new Vector3(p.x * side, p.y, r.Z), new Vector3(nn.x * side, nn.y, nn.z), col);
        }

        private void HullCap(Ring r, bool front, Color32 col)
        {
            int n = r.Half.Length;
            var nn = front ? Vector3.forward : Vector3.back;
            float cy = 0;
            for (int i = 0; i < n; i++) cy += r.Half[i].y;
            int centre = Vertex(new Vector3(0, cy / n, r.Z), nn, col);
            int start = _verts.Count;
            // Outline: right half bottom -> top, then the left half top -> bottom (skipping the shared centre points).
            for (int i = 0; i < n; i++) Vertex(new Vector3(r.Half[i].x, r.Half[i].y, r.Z), nn, col);
            for (int i = n - 2; i >= 1; i--) Vertex(new Vector3(-r.Half[i].x, r.Half[i].y, r.Z), nn, col);
            int m = _verts.Count - start;
            for (int k = 0; k < m; k++)
            {
                int a = start + k, b = start + (k + 1) % m;
                if (front) { _tris.Add(centre); _tris.Add(a); _tris.Add(b); }
                else { _tris.Add(centre); _tris.Add(b); _tris.Add(a); }
            }
        }

        /// <summary>
        /// Surface of revolution around the local X axis (tyres, rims). <paramref name="profile"/> lists (radius, x)
        /// points; <paramref name="colors"/>[i] colours the band between profile[i] and profile[i + 1].
        /// The profile should run from -X to +X along the outside so the faces point outwards.
        /// </summary>
        public void Lathe(Vector3 center, IList<Vector2> profile, IList<Color32> colors, int sides = 12, System.Func<int, int, Color32?> segment = null)
        {
            int n = profile.Count;
            var pn = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                var t = profile[Mathf.Min(n - 1, i + 1)] - profile[Mathf.Max(0, i - 1)];
                // Tangent (dr, dx) -> outward normal (dx, -dr) in (radius, x).
                pn[i] = new Vector2(t.y, -t.x).normalized;
            }
            for (int i = 0; i + 1 < n; i++)
            {
                var col = colors[Mathf.Min(i, colors.Count - 1)];
                if (segment != null && segment(i, 0).HasValue)
                {
                    LatheBand(center, profile, pn, i, sides, k => segment(i, k) ?? col);
                    continue;
                }
                int start = _verts.Count;
                for (int k = 0; k <= sides; k++)
                {
                    float a = Mathf.PI * 2 * k / sides;
                    float sa = Mathf.Sin(a), ca = Mathf.Cos(a);
                    for (int j = 0; j < 2; j++)
                    {
                        var p = profile[i + j];
                        var nn = pn[i + j];
                        Vertex(center + new Vector3(p.y, p.x * sa, p.x * ca), new Vector3(nn.y, nn.x * sa, nn.x * ca), col);
                    }
                }
                for (int k = 0; k < sides; k++)
                {
                    int a = start + k * 2, b = a + 2;
                    _tris.Add(a); _tris.Add(a + 1); _tris.Add(b);
                    _tris.Add(b); _tris.Add(a + 1); _tris.Add(b + 1);
                }
            }
        }

        /// <summary>One band of a lathe with its own colour per segment (sidewall lettering, dashes).</summary>
        private void LatheBand(Vector3 center, IList<Vector2> profile, Vector2[] pn, int i, int sides, System.Func<int, Color32> color)
        {
            for (int k = 0; k < sides; k++)
            {
                var col = color(k);
                int start = _verts.Count;
                for (int e = 0; e < 2; e++)
                {
                    float a = Mathf.PI * 2 * (k + e) / sides;
                    float sa = Mathf.Sin(a), ca = Mathf.Cos(a);
                    for (int j = 0; j < 2; j++)
                    {
                        var p = profile[i + j];
                        var nn = pn[i + j];
                        Vertex(center + new Vector3(p.y, p.x * sa, p.x * ca), new Vector3(nn.y, nn.x * sa, nn.x * ca), col);
                    }
                }
                _tris.Add(start); _tris.Add(start + 1); _tris.Add(start + 2);
                _tris.Add(start + 2); _tris.Add(start + 1); _tris.Add(start + 3);
            }
        }

        /// <summary>Flat ring (or disc when <paramref name="inner"/> is 0) in the YZ plane facing +X or -X (wheel faces, brake discs).</summary>
        public void DiscX(Vector3 center, float inner, float outer, int facing, Color32 color, int sides = 12)
        {
            var nn = new Vector3(facing, 0, 0);
            int start = _verts.Count;
            for (int k = 0; k < sides; k++)
            {
                float a = Mathf.PI * 2 * k / sides;
                var dir = new Vector3(0, Mathf.Sin(a), Mathf.Cos(a));
                Vertex(center + dir * outer, nn, color);
                if (inner > 0) Vertex(center + dir * inner, nn, color);
            }
            if (inner > 0)
            {
                for (int k = 0; k < sides; k++)
                {
                    int o0 = start + k * 2, i0 = o0 + 1, o1 = start + ((k + 1) % sides) * 2, i1 = o1 + 1;
                    if (facing > 0) { _tris.Add(o0); _tris.Add(i0); _tris.Add(o1); _tris.Add(o1); _tris.Add(i0); _tris.Add(i1); }
                    else { _tris.Add(o0); _tris.Add(o1); _tris.Add(i0); _tris.Add(o1); _tris.Add(i1); _tris.Add(i0); }
                }
            }
            else
            {
                int c = Vertex(center, nn, color);
                for (int k = 0; k < sides; k++)
                {
                    int a = start + k, b = start + (k + 1) % sides;
                    if (facing > 0) { _tris.Add(c); _tris.Add(b); _tris.Add(a); }
                    else { _tris.Add(c); _tris.Add(a); _tris.Add(b); }
                }
            }
        }

        /// <summary>Tapered tube between two points (fork legs, limbs, exhausts), smooth sides and flat end caps.</summary>
        public void Tube(Vector3 from, Vector3 to, float radiusFrom, float radiusTo, Color32 color, int sides = 6, bool caps = true, Color32? endCap = null)
        {
            var axis = to - from;
            var d = axis.normalized;
            var u = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var v = Vector3.Cross(u, d);
            int start = _verts.Count;
            for (int k = 0; k < sides; k++)
            {
                float a = Mathf.PI * 2 * k / sides;
                var dir = u * Mathf.Cos(a) + v * Mathf.Sin(a);
                Vertex(from + dir * radiusFrom, dir, color);
                Vertex(to + dir * radiusTo, dir, color);
            }
            for (int k = 0; k < sides; k++)
            {
                int a = start + k * 2, b = start + ((k + 1) % sides) * 2;
                _tris.Add(a); _tris.Add(a + 1); _tris.Add(b);
                _tris.Add(b); _tris.Add(a + 1); _tris.Add(b + 1);
            }
            for (int end = 0; caps && end < 2; end++)
            {
                var nn = end == 0 ? -d : d;
                var c = end == 0 ? from : to;
                float r = end == 0 ? radiusFrom : radiusTo;
                var col = end == 1 && endCap.HasValue ? endCap.Value : color;
                int centre = Vertex(c, nn, col);
                int ring = _verts.Count;
                for (int k = 0; k < sides; k++)
                {
                    float a = Mathf.PI * 2 * k / sides;
                    Vertex(c + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * r, nn, col);
                }
                for (int k = 0; k < sides; k++)
                {
                    int a = ring + k, b = ring + (k + 1) % sides;
                    if (end == 0) { _tris.Add(centre); _tris.Add(a); _tris.Add(b); }
                    else { _tris.Add(centre); _tris.Add(b); _tris.Add(a); }
                }
            }
        }

        /// <summary>
        /// Smooth limb along a path (arms, legs, gloves, boots): an elliptical section of half axes radii[i] around each
        /// path point, its frame carried along the path without twisting (the first frame's first axis is
        /// cross(tangent, <paramref name="up"/>)). <paramref name="paint"/>(segment i, outward direction) colours the
        /// quad between sections i and i + 1, so panels and stripes keep crisp edges; vertices are shared where the
        /// colours agree. Caps close the ends unless the limb disappears into another part.
        /// </summary>
        public void Sweep(IList<Vector3> path, IList<Vector2> radii, int sides, System.Func<int, Vector3, Color32> paint, Vector3 up, bool capStart = true, bool capEnd = true)
        {
            int n = path.Count;
            var t = new Vector3[n];
            var u = new Vector3[n];
            var v = new Vector3[n];
            for (int i = 0; i < n; i++) t[i] = (path[Mathf.Min(n - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
            u[0] = Vector3.Cross(t[0], up);
            if (u[0].sqrMagnitude < 1e-8f) u[0] = Vector3.Cross(t[0], Vector3.right);
            u[0] = u[0].normalized;
            for (int i = 1; i < n; i++) u[i] = (u[i - 1] - t[i] * Vector3.Dot(u[i - 1], t[i])).normalized;
            for (int i = 0; i < n; i++) v[i] = Vector3.Cross(t[i], u[i]);

            var shared = new Dictionary<long, int>();
            System.Func<int, int, Color32, int> at = (i, k, col) =>
            {
                k %= sides;
                long key = ((long)(i * sides + k) << 32) | (uint)(col.r | col.g << 8 | col.b << 16 | col.a << 24);
                int idx;
                if (shared.TryGetValue(key, out idx)) return idx;
                float a = Mathf.PI * 2 * k / sides;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                var r = radii[i];
                var p = path[i] + u[i] * (ca * r.x) + v[i] * (sa * r.y);
                var nn = (u[i] * (ca / Mathf.Max(0.001f, r.x)) + v[i] * (sa / Mathf.Max(0.001f, r.y))).normalized;
                idx = Vertex(p, nn, col);
                shared[key] = idx;
                return idx;
            };
            for (int i = 0; i + 1 < n; i++)
                for (int k = 0; k < sides; k++)
                {
                    float am = Mathf.PI * 2 * (k + 0.5f) / sides;
                    var dir = ((u[i] + u[i + 1]) * Mathf.Cos(am) + (v[i] + v[i + 1]) * Mathf.Sin(am)).normalized;
                    var col = paint(i, dir);
                    int a = at(i, k, col), b = at(i, k + 1, col), c = at(i + 1, k, col), d = at(i + 1, k + 1, col);
                    _tris.Add(a); _tris.Add(b); _tris.Add(d);
                    _tris.Add(a); _tris.Add(d); _tris.Add(c);
                }
            for (int end = 0; end < 2; end++)
            {
                if (end == 0 ? !capStart : !capEnd) continue;
                int i = end == 0 ? 0 : n - 1;
                var nn = end == 0 ? -t[0] : t[n - 1];
                var col = paint(end == 0 ? 0 : n - 2, nn);
                int centre = Vertex(path[i], nn, col);
                int ring = _verts.Count;
                for (int k = 0; k < sides; k++)
                {
                    float a = Mathf.PI * 2 * k / sides;
                    Vertex(path[i] + u[i] * (Mathf.Cos(a) * radii[i].x) + v[i] * (Mathf.Sin(a) * radii[i].y), nn, col);
                }
                for (int k = 0; k < sides; k++)
                {
                    int a = ring + k, b = ring + (k + 1) % sides;
                    if (end == 0) { _tris.Add(centre); _tris.Add(b); _tris.Add(a); }
                    else { _tris.Add(centre); _tris.Add(a); _tris.Add(b); }
                }
            }
        }

        /// <summary>Flat quad facing <paramref name="normal"/>: centre plus/minus the two half-extent axes (vents, spokes, plates).</summary>
        public void Panel(Vector3 center, Vector3 halfA, Vector3 halfB, Vector3 normal, Color32 color)
        {
            Vector3 a = center - halfA - halfB, b = center + halfA - halfB, c = center + halfA + halfB, d = center - halfA + halfB;
            if (Vector3.Dot(Vector3.Cross(b - a, d - a), normal) < 0) Quad(a, d, c, b, color);
            else Quad(a, b, c, d, color);
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
            return ToObject(name, parent, null);
        }

        /// <summary>The mesh as a child object drawn with <paramref name="material"/> (null: the shared vertex-colour material).</summary>
        public GameObject ToObject(string name, Transform parent, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material != null ? material : SharedMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }
    }
}
