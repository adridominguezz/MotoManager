using System;
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
            BuildBoards(details);
            var scenery = new MeshBuilder();
            BuildScenery(scenery);

            ground.ToObject("Ground", transform);
            scenery.ToObject("Scenery", transform);
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

        private static readonly Color32[] CrowdColors =
        {
            new Color32(230, 60, 60, 255), new Color32(250, 210, 60, 255), new Color32(60, 140, 230, 255), new Color32(240, 240, 240, 255),
            new Color32(70, 190, 110, 255), new Color32(240, 130, 40, 255), new Color32(40, 40, 50, 255), new Color32(180, 80, 200, 255)
        };

        /// <summary>Spectators: small coloured blocks in rows on top of a stand.</summary>
        private void Crowd(MeshBuilder mb, double d, double lateral, float height)
        {
            var rng = new System.Random((int)(d * 7919) ^ (int)(lateral * 131));
            float heading = HeadingDeg(d);
            for (int row = 0; row < 3; row++)
                for (int k = 0; k < 9; k++)
                {
                    double along = d + (k - 4) * 2.0;
                    double lat = lateral + Math.Sign(lateral) * (row * 2.0 - 2.0);
                    var c = CrowdColors[rng.Next(CrowdColors.Length)];
                    mb.Box(Point(along, lat, height + row * 0.6f), new Vector3(1.1f, 1.0f, 1.3f), c, Quaternion.Euler(0, heading, 0));
                }
        }

        private static readonly Color32[] BoardColors =
        {
            new Color32(225, 40, 40, 255), new Color32(30, 90, 200, 255), new Color32(250, 200, 30, 255), new Color32(30, 160, 90, 255),
            new Color32(240, 240, 240, 255), new Color32(120, 60, 180, 255), new Color32(240, 120, 30, 255), new Color32(20, 30, 50, 255)
        };

        /// <summary>Advertising boards along the track: close on the straights, behind the gravel at corners.</summary>
        private void BuildBoards(MeshBuilder mb)
        {
            foreach (var seg in Track.Segments)
            {
                int outside = seg.AngleDeg > 0 ? -1 : 1;
                double runoff = seg.IsCorner ? 1.3 + 8 + 10 * seg.BrakingIntensity + 1.5 : 4.5;
                for (int side = -1; side <= 1; side += 2)
                {
                    // Keep the pit side of the main straight clear (pit wall and garages are there).
                    double lat = seg.IsCorner ? (side == outside ? runoff : 3.5) : 4.5;
                    double from = seg.Start, to = seg.End;
                    for (double d = from; d < to - 0.5; d += 6)
                    {
                        double e = Math.Min(to, d + 6);
                        if (side == PitSide && InPitZone(d)) continue;
                        int panel = (int)(d / 12) + (side > 0 ? 3 : 0);
                        var col = BoardColors[((panel % BoardColors.Length) + BoardColors.Length) % BoardColors.Length];
                        var a0 = Point(d, side * (HalfWidth + lat), 0);
                        var a1 = Point(e, side * (HalfWidth + lat), 0);
                        var up = Vector3.up * 1.1f;
                        mb.Quad(a0, a0 + up, a1 + up, a1, col);
                        mb.Quad(a1, a1 + up, a0 + up, a0, col);
                    }
                }
            }
        }

        private bool InPitZone(double d)
        {
            double entry = Track.PitEntry, exit = Track.PitExit;
            return d >= entry - 20 || d <= exit + 20;
        }

        /// <summary>
        /// Woods, buildings and houses around the circuit, kept clear of the track, run-offs and stands. Generated from
        /// the circuit id, so every visit to a circuit looks the same.
        /// </summary>
        private void BuildScenery(MeshBuilder mb)
        {
            // Coarse grid of centre-line samples for "how far from the track" checks.
            const float cell = 40f;
            var grid = new Dictionary<long, List<Vector2>>();
            for (double g = 0; g < Geometry.GeometricLength; g += 8)
            {
                var p = PointGeo(g, 0, 0);
                long key = Key(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.z / cell));
                List<Vector2> l;
                if (!grid.TryGetValue(key, out l)) grid[key] = l = new List<Vector2>();
                l.Add(new Vector2(p.x, p.z));
            }
            Func<float, float, float> distance = (x, z) =>
            {
                int cx = Mathf.FloorToInt(x / cell), cz = Mathf.FloorToInt(z / cell);
                float best = float.MaxValue;
                for (int dx = -2; dx <= 2; dx++)
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        List<Vector2> l;
                        if (!grid.TryGetValue(Key(cx + dx, cz + dz), out l)) continue;
                        foreach (var q in l) best = Math.Min(best, (q - new Vector2(x, z)).sqrMagnitude);
                    }
                return best == float.MaxValue ? 999f : Mathf.Sqrt(best);
            };

            var rng = new System.Random((int)(Core.Common.DeterministicRandom.Hash(Track.CircuitId) & 0x7FFFFFFF));
            float minX = (float)Geometry.MinX - 260, maxX = (float)Geometry.MaxX + 260;
            float minZ = (float)Geometry.MinY - 260, maxZ = (float)Geometry.MaxY + 260;
            Func<float> u = () => (float)rng.NextDouble();

            // Woods: clusters of trees.
            int clusters = Mathf.Clamp((int)((maxX - minX) * (maxZ - minZ) / 40000f), 25, 110);
            int trees = 0;
            for (int c = 0; c < clusters && trees < 1100; c++)
            {
                float cx = Mathf.Lerp(minX, maxX, u()), cz = Mathf.Lerp(minZ, maxZ, u());
                int n = 6 + rng.Next(16);
                for (int k = 0; k < n; k++)
                {
                    float x = cx + (u() + u() + u() - 1.5f) * 40f, z = cz + (u() + u() + u() - 1.5f) * 40f;
                    if (distance(x, z) < 52) continue;
                    Tree(mb, new Vector3(x, 0, z), 0.7f + u() * 0.8f, u());
                    trees++;
                }
            }

            // Buildings and houses further out.
            for (int b = 0; b < 40; b++)
            {
                float x = Mathf.Lerp(minX, maxX, u()), z = Mathf.Lerp(minZ, maxZ, u());
                if (distance(x, z) < 95) continue;
                float yaw = u() * 90;
                if (u() < 0.3f)
                {
                    float w = 14 + u() * 18, d = 14 + u() * 18, h = 10 + u() * 26;
                    var wall = Color32.Lerp(new Color32(200, 205, 215, 255), new Color32(120, 160, 205, 255), u());
                    mb.Box(new Vector3(x, h / 2, z), new Vector3(w, h, d), wall, Quaternion.Euler(0, yaw, 0));
                    mb.Box(new Vector3(x, h + 0.4f, z), new Vector3(w * 0.9f, 0.8f, d * 0.9f), new Color32(235, 235, 240, 255), Quaternion.Euler(0, yaw, 0));
                }
                else
                {
                    // A few houses together: white walls, red roofs.
                    for (int hsi = 0; hsi < 3 + rng.Next(4); hsi++)
                    {
                        var pos = new Vector3(x + (u() - 0.5f) * 40, 0, z + (u() - 0.5f) * 40);
                        if (distance(pos.x, pos.z) < 80) continue;
                        float hy = yaw + (u() - 0.5f) * 20;
                        mb.Box(pos + Vector3.up * 2.5f, new Vector3(7, 5, 9), new Color32(238, 236, 228, 255), Quaternion.Euler(0, hy, 0));
                        mb.Box(pos + Vector3.up * 5.6f, new Vector3(7.6f, 1.4f, 9.6f), new Color32(205, 90, 70, 255), Quaternion.Euler(0, hy, 0));
                    }
                }
            }
        }

        private static long Key(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        private static void Tree(MeshBuilder mb, Vector3 at, float size, float tint)
        {
            var trunk = new Color32(95, 70, 50, 255);
            Color32 leaves = Color32.Lerp(new Color32(40, 100, 45, 255), new Color32(85, 135, 55, 255), tint);
            mb.Box(at + Vector3.up * 1.4f * size, new Vector3(0.7f, 2.8f, 0.7f) * size, trunk);
            mb.Loft(new[]
            {
                new MeshBuilder.Section(2.0f * size, 0, 3.2f * size, 3.2f * size, leaves),
                new MeshBuilder.Section(5.0f * size, 0, 2.3f * size, 2.3f * size, leaves),
                new MeshBuilder.Section(8.0f * size, 0, 1.1f * size, 1.1f * size, leaves),
                new MeshBuilder.Section(9.6f * size, 0, 0.05f, 0.05f, leaves)
            }, 7, Quaternion.Euler(-90, 0, 0), at);
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
                Crowd(mb, dist, -PitSide * (HalfWidth + 22), 8.2f);
            }
            foreach (var seg in Track.Segments)
            {
                if (!seg.IsCorner || seg.BrakingIntensity < 0.55) continue;
                int outside = seg.AngleDeg > 0 ? -1 : 1;
                double runoff = 8 + 10 * seg.BrakingIntensity;
                for (double d = seg.Start - 40; d < seg.Start + 20; d += 20)
                {
                    mb.Box(Point(d, outside * (HalfWidth + runoff + 12), 3), new Vector3(7, 6, 19), Stand, Quaternion.Euler(0, HeadingDeg(d), 0));
                    Crowd(mb, d, outside * (HalfWidth + runoff + 12), 6.2f);
                }
            }
        }
    }
}
