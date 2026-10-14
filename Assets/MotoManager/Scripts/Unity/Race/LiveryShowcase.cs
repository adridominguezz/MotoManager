using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotoManager.App.Race
{
    /// <summary>
    /// The livery editor's 3D showcase: a bike and rider on a turntable in a small dark studio (two side panels with
    /// light bars, a light ring on the floor and a dimmed mirror image below the floor line as the reflection), filmed
    /// by its own camera into a render texture that the page shows in a <see cref="RawImage"/>. It turns slowly on
    /// its own and follows horizontal drags. One stage at a time, far below the world origin; it removes itself when
    /// no view uses it any more (pages are rebuilt on every refresh, so views come and go).
    /// </summary>
    public class LiveryShowcase : MonoBehaviour
    {
        private static LiveryShowcase _stage;

        /// <summary>Texture aspect: wide, so the page can crop it to the area it has.</summary>
        public const float Aspect = 4f;
        private const int TextureHeight = 420;
        private static readonly Vector3 Origin = new Vector3(0, -500, 0);

        public RenderTexture Texture { get; private set; }
        private Camera _camera;
        private Transform _turntable;
        private Transform _bike;
        private Transform _reflection;
        private Material _reflectionMaterial;
        private int _views;
        private float _yaw = 215f;
        private float _idle = 10f;
        private BikeLivery _shown;
        private bool _hasShown;

        /// <summary>The stage (created on first use); call <see cref="Release"/> when the view goes away.</summary>
        public static LiveryShowcase Acquire()
        {
            if (_stage == null)
            {
                var go = new GameObject("LiveryShowcase");
                _stage = go.AddComponent<LiveryShowcase>();
                _stage.Build();
            }
            _stage._views++;
            return _stage;
        }

        public void Release()
        {
            _views--;
        }

        private void Build()
        {
            transform.position = Origin;
            Texture = new RenderTexture(Mathf.RoundToInt(TextureHeight * Aspect), TextureHeight, 16) { name = "LiveryShowcase", antiAliasing = 2 };
            Texture.Create();

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(transform, false);
            _camera = camGo.AddComponent<Camera>();
            _camera.targetTexture = Texture;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color32(12, 15, 24, 255);
            _camera.fieldOfView = 21f;
            _camera.nearClipPlane = 0.2f;
            _camera.farClipPlane = 25f;
            _camera.depth = -50;
            camGo.transform.localPosition = new Vector3(0, 1.15f, -4.6f);
            camGo.transform.LookAt(transform.position + new Vector3(0, 0.5f, 0));

            _reflectionMaterial = new Material(BikeVisual.BikeMaterial) { name = "BikeReflection" };
            _reflectionMaterial.SetColor("_Tint", new Color(0.26f, 0.29f, 0.38f, 1));
            _reflectionMaterial.SetFloat("_Spec", 0.15f);
            _reflectionMaterial.SetFloat("_Rim", 0.05f);

            _turntable = new GameObject("Turntable").transform;
            _turntable.SetParent(transform, false);

            // Studio: angled side panels with light bars, a light ring on the floor; mirrored below as the reflection.
            var studio = new MeshBuilder();
            BuildStudio(studio);
            studio.ToObject("Studio", transform, BikeVisual.BikeMaterial);
            var studioMirror = studio.ToObject("StudioReflection", transform, _reflectionMaterial);
            studioMirror.transform.localScale = new Vector3(1, -1, 1);
            var ring = new MeshBuilder();
            FloorRing(ring, 1.55f, 1.6f, new Color32(150, 170, 210, 255));
            FloorRing(ring, 2.4f, 2.43f, new Color32(70, 82, 110, 255));
            ring.ToObject("FloorRing", transform);
        }

        private static void BuildStudio(MeshBuilder b)
        {
            var panel = new Color32(28, 34, 50, 120);
            var light = new Color32(235, 240, 255, 255);
            for (int side = -1; side <= 1; side += 2)
            {
                var rot = Quaternion.Euler(0, side * -38f, 0);
                var centre = new Vector3(side * 3.1f, 1.1f, 1.6f);
                b.Box(centre, new Vector3(0.08f, 2.2f, 2.6f), panel, rot);
                b.Box(centre + rot * new Vector3(-side * 0.06f, 0.55f, 0), new Vector3(0.04f, 0.05f, 1.6f), light, rot);
                b.Box(centre + rot * new Vector3(-side * 0.06f, -0.75f, 0), new Vector3(0.04f, 0.05f, 1.6f), light, rot);
            }
            b.Box(new Vector3(0, 1.6f, 4.2f), new Vector3(4.4f, 3.2f, 0.1f), new Color32(18, 22, 34, 60));
        }

        private static void FloorRing(MeshBuilder b, float inner, float outer, Color32 color)
        {
            const int n = 64;
            for (int k = 0; k < n; k++)
            {
                float a0 = Mathf.PI * 2 * k / n, a1 = Mathf.PI * 2 * (k + 1) / n;
                var d0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0));
                var d1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                b.Quad(d0 * inner + Vector3.up * 0.002f, d0 * outer + Vector3.up * 0.002f, d1 * outer + Vector3.up * 0.002f, d1 * inner + Vector3.up * 0.002f, color);
            }
        }

        /// <summary>Shows a livery (rebuilds the bike only when it changed).</summary>
        public void Show(BikeLivery livery, Color32 helmet)
        {
            if (_hasShown && Same(livery, _shown)) return;
            _shown = livery;
            _hasShown = true;
            if (_bike != null) DestroyRig(_bike);
            if (_reflection != null) DestroyRig(_reflection);
            _bike = Rig("Bike", livery, helmet, BikeVisual.BikeMaterial);
            _reflection = Rig("BikeReflection", livery, helmet, _reflectionMaterial);
            _reflection.localScale = new Vector3(1, -1, 1);
        }

        private Transform Rig(string name, BikeLivery livery, Color32 helmet, Material material)
        {
            var root = new GameObject(name).transform;
            root.SetParent(_turntable, false);
            Transform rider, legL, legR;
            BikeVisual.BuildRig(root, livery, helmet, material, out rider, out legL, out legR, true);
            return root;
        }

        private static void DestroyRig(Transform root)
        {
            // The detailed model's meshes are shared (never destroyed); its showcase materials are per rig.
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && !mf.sharedMesh.name.StartsWith(RealBike.MeshPrefix)) Destroy(mf.sharedMesh);
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
                if (mr.sharedMaterial != null && mr.sharedMaterial.name == RealBike.MeshPrefix + "Showcase") Destroy(mr.sharedMaterial);
            Destroy(root.gameObject);
        }

        private static bool Same(BikeLivery a, BikeLivery b)
        {
            return a.Style == b.Style && Eq(a.Primary, b.Primary) && Eq(a.Secondary, b.Secondary) && Eq(a.Tertiary, b.Tertiary);
        }

        private static bool Eq(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b;
        }

        /// <summary>Turns the bike by a horizontal drag (canvas units); the automatic turn pauses for a moment.</summary>
        public void Drag(float dx)
        {
            _yaw -= dx * 0.35f;
            _idle = 0;
        }

        private void Update()
        {
            if (_views <= 0)
            {
                Destroy(gameObject);
                return;
            }
            _idle += Time.unscaledDeltaTime;
            if (_idle > 2.5f) _yaw += Time.unscaledDeltaTime * 12f;
            _turntable.localRotation = Quaternion.Euler(0, _yaw, 0);
        }

        private void OnDestroy()
        {
            if (_stage == this) _stage = null;
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && !mf.sharedMesh.name.StartsWith(RealBike.MeshPrefix)) Destroy(mf.sharedMesh);
            foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
                if (mr.sharedMaterial != null && mr.sharedMaterial.name == RealBike.MeshPrefix + "Showcase") Destroy(mr.sharedMaterial);
            if (_reflectionMaterial != null) Destroy(_reflectionMaterial);
            if (_camera != null) _camera.targetTexture = null;
            if (Texture != null)
            {
                Texture.Release();
                Destroy(Texture);
            }
        }
    }

    /// <summary>
    /// The RawImage showing the <see cref="LiveryShowcase"/>: crops the wide texture to its own aspect, turns the bike
    /// on horizontal drags and releases the stage when it is destroyed.
    /// </summary>
    public class LiveryShowcaseView : MonoBehaviour, IDragHandler, IBeginDragHandler
    {
        private LiveryShowcase _stage;
        private RawImage _image;

        public static LiveryShowcaseView Create(RectTransform area, BikeLivery livery, Color32 helmet)
        {
            var img = UI.UiFactory.Rect("Showcase", area).gameObject.AddComponent<RawImage>();
            UI.UiFactory.Stretch(img.rectTransform);
            var v = img.gameObject.AddComponent<LiveryShowcaseView>();
            v._image = img;
            v._stage = LiveryShowcase.Acquire();
            img.texture = v._stage.Texture;
            img.raycastTarget = true;
            v._stage.Show(livery, helmet);
            v.Crop();
            return v;
        }

        public void Show(BikeLivery livery, Color32 helmet)
        {
            if (_stage != null) _stage.Show(livery, helmet);
        }

        private void OnRectTransformDimensionsChange()
        {
            Crop();
        }

        /// <summary>Shows the middle of the texture at the image's own aspect (no stretching).</summary>
        private void Crop()
        {
            if (_image == null) return;
            var r = _image.rectTransform.rect;
            if (r.width <= 1 || r.height <= 1) return;
            float aspect = r.width / r.height;
            if (aspect < LiveryShowcase.Aspect)
            {
                float w = aspect / LiveryShowcase.Aspect;
                _image.uvRect = new Rect((1 - w) / 2, 0, w, 1);
            }
            else
            {
                float h = LiveryShowcase.Aspect / aspect;
                _image.uvRect = new Rect(0, (1 - h) * 0.55f, 1, h);
            }
        }

        public void OnBeginDrag(PointerEventData e)
        {
        }

        public void OnDrag(PointerEventData e)
        {
            if (_stage == null) return;
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null && canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
            _stage.Drag(e.delta.x / scale);
        }

        private void OnDestroy()
        {
            if (_stage != null) _stage.Release();
            _stage = null;
        }
    }
}
