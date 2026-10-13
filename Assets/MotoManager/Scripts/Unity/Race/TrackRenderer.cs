using System;
using System.Collections.Generic;
using MotoManager.Core.Race;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotoManager.App.Race
{
    /// <summary>
    /// Builds the 3D circuit from the simulation's <see cref="TrackGeometry"/>. The layout of everything around the
    /// track (tarmac, edge lines, kerbs at the apexes and exits, painted and gravel run-offs on the outside of corners,
    /// mown verges, barriers and tyre walls, start line and grid, pit lane with garages and paddock, grandstands,
    /// lakes, woods and houses) is planned by <see cref="TrackPlan"/>; this class only turns the plan into four merged
    /// vertex-coloured meshes (ground, surfaces, paint, objects) with one cheap shader, so the whole circuit costs a
    /// handful of draw calls on mobile. Everything is generated, so every circuit in the data files gets a view.
    /// </summary>
    public class TrackRenderer : MonoBehaviour
    {
        public const float HalfWidth = (float)TrackPlan.HalfWidth;
        public const float PitOffset = (float)TrackPlan.PitOffset;

        public TrackGeometry Geometry { get; private set; }
        public TrackModel Track { get; private set; }
        /// <summary>The line the bikes follow (outside - apex - outside).</summary>
        public RacingLine Line { get; private set; }
        /// <summary>+1: pit lane on the left of the direction of travel (inside of an anticlockwise loop).</summary>
        public int PitSide { get; private set; }
        public TrackPlan Plan { get; private set; }

        public static readonly Color32 Grass = ToColor(TrackPlan.Grass);

        // Heights of the flat layers; the shader's depth offset keeps them apart from far away as well.
        private const float SurfaceHeight = 0.02f;
        private const float DecalHeight = 0.035f;

        public static TrackRenderer Create(TrackModel track, Transform parent)
        {
            var go = new GameObject("Track " + track.CircuitId);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<TrackRenderer>();
            r.Build(track);
            return r;
        }

        public static Vector3 ToWorld(double x, double y, float height = 0)
        {
            return new Vector3((float)x, height, (float)y);
        }

        public Vector3 Point(double distance, double lateralMeters, float height)
        {
            double x, y, h;
            Geometry.Evaluate(distance, lateralMeters, out x, out y, out h);
            return ToWorld(x, y, height);
        }

        public Vector3 PointGeo(double geo, double lateralMeters, float height)
        {
            double x, y, h;
            Geometry.EvaluateGeometric(geo, lateralMeters, out x, out y, out h);
            return ToWorld(x, y, height);
        }

        public float HeadingDeg(double distance)
        {
            double x, y, h;
            Geometry.Evaluate(distance, 0, out x, out y, out h);
            return YawOf(h);
        }

        /// <summary>Unity yaw: 0 = +Z, clockwise positive seen from above. Geometry heading: 0 = +X, anticlockwise.</summary>
        private static float YawOf(double heading)
        {
            return 90f - (float)(heading * Mathf.Rad2Deg);
        }

        /// <summary>Lateral distance of the pit lane centre from the track centre for a pit-lane progress 0..1.</summary>
        public static double PitLateral(double t)
        {
            return TrackPlan.PitLateral(t);
        }

        private static Color32 ToColor(TrackPlan.Rgb c)
        {
            return new Color32(c.R, c.G, c.B, 255);
        }

        private void Build(TrackModel track)
        {
            Track = track;
            Geometry = new TrackGeometry(track);
            PitSide = Geometry.Orientation;
            Line = new RacingLine(Geometry, HalfWidth);
            Plan = new TrackPlan(Geometry, track, Line, PitSide);

            var ground = new MeshBuilder();
            ground.Quad(new Vector3((float)Plan.MinX, 0, (float)Plan.MinY), new Vector3((float)Plan.MinX, 0, (float)Plan.MaxY),
                        new Vector3((float)Plan.MaxX, 0, (float)Plan.MaxY), new Vector3((float)Plan.MaxX, 0, (float)Plan.MinY), Grass);

            var surface = new MeshBuilder();
            var decals = new MeshBuilder();
            foreach (var f in Plan.Flats)
            {
                bool decal = f.Layer == TrackPlan.Layer.Decal;
                float y = (decal ? DecalHeight : SurfaceHeight) + (float)f.Z;
                FlatQuad(decal ? decals : surface, f, y);
            }

            var objects = new MeshBuilder();
            foreach (var w in Plan.Walls) Wall(objects, w);
            foreach (var b in Plan.Blocks)
            {
                var centre = new Vector3((float)b.X, (float)(b.Z + b.Height / 2), (float)b.Y);
                objects.Box(centre, new Vector3((float)b.Width, (float)b.Height, (float)b.Length), ToColor(b.Color), Quaternion.Euler(0, YawOf(b.H), 0));
            }
            foreach (var t in Plan.Trees) Tree(objects, new Vector3((float)t.X, 0, (float)t.Y), (float)t.Size, (float)t.Tint, t.Kind);

            ground.ToObject("Ground", transform);
            Layered(surface.ToObject("Surfaces", transform), SurfaceMaterial);
            Layered(decals.ToObject("Paint", transform), DecalMaterial);
            objects.ToObject("Scenery", transform);
        }

        private static void Layered(GameObject go, Material material)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null && material != null) mr.sharedMaterial = material;
        }

        private static Material _surfaceMaterial, _decalMaterial;

        /// <summary>Same shader as everything else, pulled towards the camera so flat layers never flicker.</summary>
        private static Material SurfaceMaterial
        {
            get { return _surfaceMaterial ?? (_surfaceMaterial = OffsetMaterial("TrackSurface", -1, -1)); }
        }

        private static Material DecalMaterial
        {
            get { return _decalMaterial ?? (_decalMaterial = OffsetMaterial("TrackPaint", -2, -2)); }
        }

        private static Material OffsetMaterial(string name, float factor, float units)
        {
            var m = new Material(MeshBuilder.SharedMaterial) { name = name };
            if (m.HasProperty("_OffsetFactor")) m.SetFloat("_OffsetFactor", factor);
            if (m.HasProperty("_OffsetUnits")) m.SetFloat("_OffsetUnits", units);
            return m;
        }

        private static void FlatQuad(MeshBuilder mb, TrackPlan.Flat f, float y)
        {
            var a = new Vector3((float)f.Ax, y, (float)f.Ay);
            var b = new Vector3((float)f.Bx, y, (float)f.By);
            var c = new Vector3((float)f.Cx, y, (float)f.Cy);
            var d = new Vector3((float)f.Dx, y, (float)f.Dy);
            var col = ToColor(f.Color);
            // Face up whatever order the plan listed the corners in.
            var n = Vector3.Cross(b - a, c - a);
            if (n.y >= 0) mb.Quad(a, b, c, d, col);
            else mb.Quad(a, d, c, b, col);
        }

        /// <summary>Barrier, tyre wall, flag or gantry beam: a thin double-sided panel or a solid slab with a top.</summary>
        private static void Wall(MeshBuilder mb, TrackPlan.Wall w)
        {
            var p0 = new Vector3((float)w.X1, (float)w.Z, (float)w.Y1);
            var p1 = new Vector3((float)w.X2, (float)w.Z, (float)w.Y2);
            var dir = p1 - p0;
            if (dir.sqrMagnitude < 1e-6f) return;
            var side = Vector3.Cross(Vector3.up, dir).normalized * ((float)w.Thickness * 0.5f);
            var up = Vector3.up * (float)w.Height;
            var col = ToColor(w.Color);
            if (w.Thickness < 0.3)
            {
                mb.Quad(p0, p0 + up, p1 + up, p1, col);
                mb.Quad(p1, p1 + up, p0 + up, p0, col);
                return;
            }
            Vector3 a0 = p0 - side, a1 = p1 - side, b0 = p0 + side, b1 = p1 + side;
            // Faces point away from the centre of the slab (normal = Cross(b - a, d - a)).
            mb.Quad(a1, a1 + up, a0 + up, a0, col);
            mb.Quad(b0, b0 + up, b1 + up, b1, col);
            var top = ToColor(w.Top);
            Vector3 ta0 = a0 + up, ta1 = a1 + up, tb0 = b0 + up, tb1 = b1 + up;
            if (Vector3.Cross(ta1 - ta0, tb0 - ta0).y >= 0) mb.Quad(ta0, ta1, tb1, tb0, top);
            else mb.Quad(ta0, tb0, tb1, ta1, top);
        }

        private static readonly Color32 Trunk = new Color32(95, 70, 50, 255);

        private static void Tree(MeshBuilder mb, Vector3 at, float size, float tint, int kind)
        {
            mb.Tube(at, at + Vector3.up * 2.6f * size, 0.35f * size, 0.25f * size, Trunk, 4, false);
            if (kind == 0)
            {
                // Conifer.
                Color32 leaves = Color32.Lerp(new Color32(36, 92, 44, 255), new Color32(70, 120, 52, 255), tint);
                mb.Loft(new[]
                {
                    new MeshBuilder.Section(1.8f * size, 0, 3.0f * size, 3.0f * size, leaves),
                    new MeshBuilder.Section(5.0f * size, 0, 2.1f * size, 2.1f * size, leaves),
                    new MeshBuilder.Section(9.4f * size, 0, 0.05f, 0.05f, leaves)
                }, 6, Quaternion.Euler(-90, 0, 0), at);
            }
            else
            {
                // Broadleaf: a rounded crown.
                Color32 leaves = Color32.Lerp(new Color32(62, 118, 50, 255), new Color32(112, 152, 62, 255), tint);
                mb.Loft(new[]
                {
                    new MeshBuilder.Section(2.2f * size, 0, 1.6f * size, 1.6f * size, leaves),
                    new MeshBuilder.Section(3.6f * size, 0, 3.4f * size, 3.4f * size, leaves),
                    new MeshBuilder.Section(5.6f * size, 0, 3.6f * size, 3.6f * size, leaves),
                    new MeshBuilder.Section(7.4f * size, 0, 2.2f * size, 2.2f * size, leaves),
                    new MeshBuilder.Section(8.2f * size, 0, 0.3f * size, 0.3f * size, leaves)
                }, 7, Quaternion.Euler(-90, 0, 0), at);
            }
        }
    }
}
