using System.Collections.Generic;
using MotoManager.Core.Race;
using UnityEngine;

namespace MotoManager.App.Race
{
    /// <summary>
    /// Builds the 3D circuit from the simulation's <see cref="TrackGeometry"/>: tarmac, red/white kerbs on every
    /// corner, gravel traps on the outside, start line and grid, pit lane with garages, grandstands.
    /// Everything is generated, so every circuit in the data files gets a view for free.
    /// </summary>
    public class TrackRenderer : MonoBehaviour
    {
        public const float HalfWidth = 6f;
        public const float PitOffset = HalfWidth + 9f;

        public TrackGeometry Geometry { get; private set; }
        public TrackModel Track { get; private set; }
        /// <summary>The line the bikes follow (outside - apex - outside).</summary>
        public RacingLine Line { get; private set; }
        /// <summary>+1: pit lane on the left of the direction of travel (inside of an anticlockwise loop).</summary>
        public int PitSide { get; private set; }

        public static readonly Color32 Grass = new Color32(58, 104, 56, 255);
        private static readonly Color32 Verge = new Color32(70, 122, 64, 255);
        private static readonly Color32 Asphalt = new Color32(62, 64, 70, 255);
        private static readonly Color32 EdgeLine = new Color32(225, 225, 225, 255);
        private static readonly Color32 KerbRed = new Color32(200, 30, 30, 255);
        private static readonly Color32 KerbWhite = new Color32(235, 235, 235, 255);
        private static readonly Color32 Gravel = new Color32(196, 176, 130, 255);
        private static readonly Color32 Paint = new Color32(240, 240, 240, 255);
        private static readonly Color32 PitAsphalt = new Color32(70, 72, 78, 255);
        private static readonly Color32 Wall = new Color32(170, 170, 175, 255);
        private static readonly Color32 Garage = new Color32(90, 96, 110, 255);
        private static readonly Color32 Stand = new Color32(120, 128, 140, 255);

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
            // Unity yaw: 0 = +Z, clockwise positive seen from above. Geometry heading: 0 = +X, anticlockwise.
            return 90f - (float)(h * Mathf.Rad2Deg);
        }

        private void Build(TrackModel track)
        {
            Track = track;
            Geometry = new TrackGeometry(track);
            PitSide = Geometry.Orientation;

            var ground = new MeshBuilder();
            var asphalt = new MeshBuilder();
            var details = new MeshBuilder();

            Line = new RacingLine(Geometry, HalfWidth);

            // One grass field, a lighter mown verge along the circuit (no tiles: they read as blocks from above).
            float pad = 400;
            float minX = (float)Geometry.MinX - pad, maxX = (float)Geometry.MaxX + pad;
            float minZ = (float)Geometry.MinY - pad, maxZ = (float)Geometry.MaxY + pad;
            ground.Quad(new Vector3(minX, 0, minZ), new Vector3(minX, 0, maxZ), new Vector3(maxX, 0, maxZ), new Vector3(maxX, 0, minZ), Grass);

            // Tarmac ribbon, verges and painted edge lines, sampled every 2 m with smooth tangents.
            double step = 2;
            int n = Mathf.CeilToInt((float)(Geometry.GeometricLength / step));
            var left = new List<Vector3>(n + 1);
            var right = new List<Vector3>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                double g = Geometry.GeometricLength * i / n;
                left.Add(PointGeo(g, HalfWidth, 0.03f));
                right.Add(PointGeo(g, -HalfWidth, 0.03f));
            }
            asphalt.Strip(left, right, Asphalt);
            for (int side = -1; side <= 1; side += 2)
            {
                GeoStrip(ground, n, side * HalfWidth, side * (HalfWidth + 14), 0.01f, Verge);
                GeoStrip(details, n, side * (HalfWidth - 0.25), side * (HalfWidth - 0.5), 0.045f, EdgeLine);
            }

            // Kerbs and gravel traps on corners.
            foreach (var seg in track.Segments)
            {
                if (!seg.IsCorner) continue;
                int outside = seg.AngleDeg > 0 ? -1 : 1; // left hander -> outside on the right
                AddEdgeStrip(details, seg.Start - 10, seg.End + 10, outside * HalfWidth, outside * (HalfWidth + 1.3), 0.05f, KerbRed, KerbWhite);
                AddEdgeStrip(details, seg.Start + seg.Length * 0.25, seg.End - seg.Length * 0.25, -outside * HalfWidth, -outside * (HalfWidth + 1.0), 0.05f, KerbRed, KerbWhite);
                double runoff = 8 + 10 * seg.BrakingIntensity;
                AddEdgeStrip(details, seg.Start - 25, seg.End + 20, outside * (HalfWidth + 1.3), outside * (HalfWidth + 1.3 + runoff), 0.02f, Gravel, Gravel);
            }

            // Start/finish line (chequered) and grid boxes.
            for (int k = 0; k < 8; k++)
            {
                double a = -HalfWidth + k * (2 * HalfWidth / 8);
                double b = a + 2 * HalfWidth / 8;
                for (int row = 0; row < 2; row++)
                {
                    var col = (k + row) % 2 == 0 ? Paint : new Color32(20, 20, 20, 255);
                    double d0 = track.Length - 1 + row, d1 = d0 + 1;
                    details.Quad(Point(d0, a, 0.06f), Point(d1, a, 0.06f), Point(d1, b, 0.06f), Point(d0, b, 0.06f), col);
                }
            }
            for (int slot = 0; slot < 30; slot++)
            {
                int row = slot / 3, colIdx = slot % 3;
                double d = track.Length - (8 + row * 9 + colIdx * 2.5);
                double lat = (colIdx - 1) * 0.55 * HalfWidth;
                details.Quad(Point(d - 0.15, lat - 1, 0.06f), Point(d + 0.15, lat - 1, 0.06f), Point(d + 0.15, lat + 1, 0.06f), Point(d - 0.15, lat + 1, 0.06f), Paint);
            }

            BuildPitLane(details);
            BuildGrandstands(details);

            ground.ToObject("Ground", transform);
            asphalt.ToObject("Tarmac", transform);
            details.ToObject("Details", transform);
        }

        /// <summary>A strip all the way round the drawn loop between two lateral offsets.</summary>
        private void GeoStrip(MeshBuilder mb, int n, double latA, double latB, float height, Color32 color)
        {
            var a = new List<Vector3>(n + 1);
            var b = new List<Vector3>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                double g = Geometry.GeometricLength * i / n;
                a.Add(PointGeo(g, latA, height));
                b.Add(PointGeo(g, latB, height));
            }
            if (latA < latB) mb.Strip(b, a, color);
            else mb.Strip(a, b, color);
        }

        /// <summary>A strip following the track between two lateral offsets, from distance a to b (wraps).</summary>
        private void AddEdgeStrip(MeshBuilder mb, double from, double to, double latA, double latB, float height, Color32 c1, Color32 c2)
        {
            double len = to - from;
            if (len <= 1) return;
            int n = Mathf.Max(2, Mathf.CeilToInt((float)(len / 2.0)));
            var a = new List<Vector3>(n + 1);
            var b = new List<Vector3>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                double d = from + len * i / n;
                a.Add(Point(d, latA, height));
                b.Add(Point(d, latB, height));
            }
            // Keep faces pointing up whichever side we are on.
            if (latA < latB) mb.Strip(b, a, c1, c2, 1);
            else mb.Strip(a, b, c1, c2, 1);
        }

        private void BuildPitLane(MeshBuilder mb)
        {
            double entry = Track.PitEntry, exit = Track.PitExit + Track.Length;
            double span = exit - entry;
            int n = Mathf.Max(8, Mathf.CeilToInt((float)(span / 4)));
            var inner = new List<Vector3>();
            var outer = new List<Vector3>();
            for (int i = 0; i <= n; i++)
            {
                double t = (double)i / n;
                double d = entry + span * t;
                double off = PitLateral(t);
                inner.Add(Point(d, PitSide * (off - 3), 0.035f));
                outer.Add(Point(d, PitSide * (off + 3), 0.035f));
            }
            if (PitSide > 0) mb.Strip(outer, inner, PitAsphalt);
            else mb.Strip(inner, outer, PitAsphalt);

            // Pit wall and garages along the straight part.
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / n;
                if (t < 0.15 || t > 0.85) continue;
                double d = entry + span * t;
                var wall = Point(d, PitSide * (HalfWidth + 3.5), 0.5f);
                mb.Box(wall, new Vector3(0.6f, 1f, 4.2f), Wall, Quaternion.Euler(0, HeadingDeg(d), 0));
                if (i % 3 == 0)
                {
                    var garage = Point(d, PitSide * (PitOffset + 9), 2.5f);
                    mb.Box(garage, new Vector3(10f, 5f, 11f), Garage, Quaternion.Euler(0, HeadingDeg(d), 0));
                }
            }
        }

        /// <summary>Lateral distance of the pit lane centre from the track centre for a pit-lane progress 0..1.</summary>
        public static double PitLateral(double t)
        {
            double ramp = 0.12;
            if (t < ramp) return Mathf.Lerp((float)HalfWidth, (float)PitOffset, (float)(t / ramp));
            if (t > 1 - ramp) return Mathf.Lerp((float)PitOffset, (float)HalfWidth, (float)((t - (1 - ramp)) / ramp));
            return PitOffset;
        }

        private void BuildGrandstands(MeshBuilder mb)
        {
            // Main grandstand opposite the pits, plus stands on the outside of the heaviest braking zones.
            for (double d = -260; d < 140; d += 20)
            {
                double dist = (d + Track.Length) % Track.Length;
                mb.Box(Point(dist, -PitSide * (HalfWidth + 22), 4), new Vector3(8, 8, 19), Stand, Quaternion.Euler(-12 * -PitSide, HeadingDeg(dist), 0));
            }
            foreach (var seg in Track.Segments)
            {
                if (!seg.IsCorner || seg.BrakingIntensity < 0.55) continue;
                int outside = seg.AngleDeg > 0 ? -1 : 1;
                double runoff = 8 + 10 * seg.BrakingIntensity;
                for (double d = seg.Start - 40; d < seg.Start + 20; d += 20)
                    mb.Box(Point(d, outside * (HalfWidth + runoff + 12), 3), new Vector3(7, 6, 19), Stand, Quaternion.Euler(0, HeadingDeg(d), 0));
            }
        }
    }
}
