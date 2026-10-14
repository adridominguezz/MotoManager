using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotoManager.App.Race
{
    /// <summary>
    /// The detailed bike and rider (one mesh) made from the user's 3D model: two levels of detail baked offline into
    /// Resources/Models (bike_lo: ~9k triangles for the race, bike_hi: ~30k for the livery showcase), each with its
    /// own base texture and a mask the MotoManager/BikeTextured shader uses to repaint the livery in the team's
    /// colours. Meshes are in bike space (pivot on the ground mid-wheelbase, forward +Z, 1.4 m wheelbase) like
    /// <see cref="BikeModel"/>, so they drop into the same rig. When the files or the shader are missing the game
    /// keeps the procedural bike.
    /// <para>Mesh file (.bytes, little endian): "MMBK", version, vertex count, index count, positions (3 floats),
    /// normals (3 floats), uvs (2 floats), indices (uint16 below 65536 vertices, else uint32).</para>
    /// </summary>
    public static class RealBike
    {
        public const string MeshPrefix = "RealBike";
        private static readonly Dictionary<bool, Mesh> Meshes = new Dictionary<bool, Mesh>();
        private static readonly Dictionary<string, Material> RaceMaterials = new Dictionary<string, Material>();
        private static Shader _shader;
        private static bool _checked, _available;

        /// <summary>True when both meshes, their textures and the shader are present.</summary>
        public static bool Available
        {
            get
            {
                if (_checked) return _available;
                _checked = true;
                _shader = Shader.Find("MotoManager/BikeTextured");
                if (_shader == null) _shader = Resources.Load<Shader>("Shaders/BikeTextured");
                _available = _shader != null && _shader.isSupported && Get(false) != null && Get(true) != null &&
                             Resources.Load<Texture2D>("Models/bike_lo_base") != null && Resources.Load<Texture2D>("Models/bike_hi_base") != null;
                return _available;
            }
        }

        /// <summary>The race mesh (<paramref name="detailed"/> false) or the showcase mesh; shared, never destroy it.</summary>
        public static Mesh Get(bool detailed)
        {
            Mesh m;
            if (Meshes.TryGetValue(detailed, out m)) return m;
            m = null;
            var asset = Resources.Load<TextAsset>(detailed ? "Models/bike_hi" : "Models/bike_lo");
            if (asset != null)
            {
                try { m = Parse(asset.bytes, MeshPrefix + (detailed ? "Hi" : "Lo")); }
                catch (Exception e) { Debug.LogWarning("Bike model not loaded: " + e.Message); }
            }
            Meshes[detailed] = m;
            return m;
        }

        private static Mesh Parse(byte[] data, string name)
        {
            if (data.Length < 16 || data[0] != 'M' || data[1] != 'M' || data[2] != 'B' || data[3] != 'K') throw new FormatException("not a bike mesh");
            int version = BitConverter.ToInt32(data, 4), vc = BitConverter.ToInt32(data, 8), ic = BitConverter.ToInt32(data, 12);
            if (version != 1 || vc <= 0 || ic <= 0) throw new FormatException("unsupported bike mesh");
            int o = 16;
            var pos = new Vector3[vc];
            var nrm = new Vector3[vc];
            var uv = new Vector2[vc];
            for (int i = 0; i < vc; i++, o += 12) pos[i] = new Vector3(BitConverter.ToSingle(data, o), BitConverter.ToSingle(data, o + 4), BitConverter.ToSingle(data, o + 8));
            for (int i = 0; i < vc; i++, o += 12) nrm[i] = new Vector3(BitConverter.ToSingle(data, o), BitConverter.ToSingle(data, o + 4), BitConverter.ToSingle(data, o + 8));
            for (int i = 0; i < vc; i++, o += 8) uv[i] = new Vector2(BitConverter.ToSingle(data, o), BitConverter.ToSingle(data, o + 4));
            var idx = new int[ic];
            bool wide = vc >= 65536;
            for (int i = 0; i < ic; i++)
            {
                if (wide) { idx[i] = BitConverter.ToInt32(data, o); o += 4; }
                else { idx[i] = BitConverter.ToUInt16(data, o); o += 2; }
            }
            var mesh = new Mesh { name = name };
            if (wide) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = pos;
            mesh.normals = nrm;
            mesh.uv = uv;
            mesh.triangles = idx;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        /// <summary>
        /// A material painting the livery's colours (main → the fairing's main areas and leathers, second → the
        /// dark areas, accent → the stripes). <paramref name="template"/> lends its tint, highlight and rim (the
        /// showcase's dimmed reflection). Race materials are cached per livery; others are new (destroy them).
        /// </summary>
        public static Material MaterialFor(BikeLivery livery, bool detailed, Material template = null)
        {
            string key = null;
            if (!detailed && template == null)
            {
                key = ColorUtility.ToHtmlStringRGB(livery.Primary) + ColorUtility.ToHtmlStringRGB(livery.Secondary) + ColorUtility.ToHtmlStringRGB(livery.Tertiary);
                Material cached;
                if (RaceMaterials.TryGetValue(key, out cached) && cached != null) return cached;
            }
            var m = new Material(_shader) { name = MeshPrefix + (key != null ? "Race" : "Showcase") };
            m.SetTexture("_MainTex", Resources.Load<Texture2D>(detailed ? "Models/bike_hi_base" : "Models/bike_lo_base"));
            m.SetTexture("_MaskTex", Resources.Load<Texture2D>(detailed ? "Models/bike_hi_mask" : "Models/bike_lo_mask"));
            m.SetColor("_Primary", livery.Primary);
            m.SetColor("_Secondary", livery.Secondary);
            m.SetColor("_Tertiary", livery.Tertiary);
            if (template != null)
            {
                if (template.HasProperty("_Tint")) m.SetColor("_Tint", template.GetColor("_Tint"));
                if (template.HasProperty("_Spec")) m.SetFloat("_Spec", template.GetFloat("_Spec"));
                if (template.HasProperty("_Rim")) m.SetFloat("_Rim", template.GetFloat("_Rim"));
            }
            if (key != null) RaceMaterials[key] = m;
            return m;
        }

        /// <summary>The mesh object under <paramref name="parent"/> (shared mesh, the given material).</summary>
        public static GameObject Build(Transform parent, bool detailed, Material material)
        {
            var go = new GameObject("BikeMesh");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Get(detailed);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }
    }
}
