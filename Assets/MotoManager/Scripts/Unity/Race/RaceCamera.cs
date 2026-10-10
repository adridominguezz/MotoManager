using UnityEngine;

namespace MotoManager.App.Race
{
    public enum CameraMode
    {
        Follow,
        Overview,
        Free
    }

    /// <summary>
    /// Elevated three-quarter camera. Follow keeps a rider in frame, Overview frames the whole circuit,
    /// Free is driven by touch (one finger pans, two fingers pinch-zoom; mouse drag/wheel in the editor).
    /// </summary>
    public class RaceCamera : MonoBehaviour
    {
        public CameraMode Mode = CameraMode.Overview;
        public Transform Target;
        public float Pitch = 55f;
        public float FollowDistance = 70f;
        public float MinDistance = 25f;
        public float MaxDistance = 2500f;

        private Camera _camera;
        private Vector3 _focus;
        private float _distance = 400;
        private float _yaw;
        private Bounds _trackBounds;
        private Vector2 _lastPointer;
        private bool _dragging;
        private float _lastPinch;

        public Camera Camera { get { return _camera; } }
        public float Distance { get { return _distance; } }

        public static RaceCamera Create(Bounds trackBounds)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(40, 70, 44, 255);
            cam.fieldOfView = 40;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 8000f;
            var rc = cam.gameObject.GetComponent<RaceCamera>() ?? cam.gameObject.AddComponent<RaceCamera>();
            rc._camera = cam;
            rc._trackBounds = trackBounds;
            rc._focus = trackBounds.center;
            rc._distance = rc.OverviewDistance();
            return rc;
        }

        public void SetMode(CameraMode mode, Transform target = null)
        {
            Mode = mode;
            if (target != null) Target = target;
            if (mode == CameraMode.Follow) _distance = FollowDistance;
        }

        private float OverviewDistance()
        {
            float size = Mathf.Max(_trackBounds.size.x, _trackBounds.size.z * (_camera != null ? _camera.aspect : 1.6f) / 1.6f);
            return Mathf.Clamp(size * 1.15f, 200, MaxDistance);
        }

        private void LateUpdate()
        {
            HandleInput();
            float dt = Time.unscaledDeltaTime;
            Vector3 targetFocus = _focus;
            float targetDistance = _distance;
            if (Mode == CameraMode.Follow && Target != null)
            {
                targetFocus = Target.position;
                // Rotate slowly behind the rider so corners read naturally.
                float yaw = Target.eulerAngles.y;
                _yaw = Mathf.LerpAngle(_yaw, yaw, 1 - Mathf.Exp(-dt * 0.8f));
            }
            else if (Mode == CameraMode.Overview)
            {
                targetFocus = _trackBounds.center;
                targetDistance = OverviewDistance();
                _yaw = Mathf.LerpAngle(_yaw, 0, 1 - Mathf.Exp(-dt * 2));
            }
            _focus = Vector3.Lerp(_focus, targetFocus, 1 - Mathf.Exp(-dt * (Mode == CameraMode.Follow ? 8 : 3)));
            _distance = Mathf.Lerp(_distance, targetDistance, 1 - Mathf.Exp(-dt * 4));
            var rot = Quaternion.Euler(Pitch, _yaw, 0);
            transform.position = _focus - rot * Vector3.forward * _distance;
            transform.rotation = rot;
        }

        private void HandleInput()
        {
            // Two-finger pinch zoom.
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0);
                var b = Input.GetTouch(1);
                float d = Vector2.Distance(a.position, b.position);
                if (b.phase == TouchPhase.Began) _lastPinch = d;
                else if (_lastPinch > 0)
                {
                    _distance = Mathf.Clamp(_distance * _lastPinch / Mathf.Max(1, d), MinDistance, MaxDistance);
                    _lastPinch = d;
                    if (Mode == CameraMode.Overview) Mode = CameraMode.Free;
                }
                _dragging = false;
                return;
            }
            _lastPinch = 0;

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                _distance = Mathf.Clamp(_distance * (1 - wheel * 0.1f), MinDistance, MaxDistance);
                if (Mode == CameraMode.Overview) Mode = CameraMode.Free;
            }

            bool down = Input.touchCount == 1 || Input.GetMouseButton(0);
            Vector2 pos = Input.touchCount == 1 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
            if (down && !UI.RaceHud.PointerOverUi(pos))
            {
                if (_dragging)
                {
                    Vector2 delta = pos - _lastPointer;
                    if (delta.sqrMagnitude > 4 && Mode != CameraMode.Free) Mode = CameraMode.Free;
                    if (Mode == CameraMode.Free)
                    {
                        float k = _distance / Screen.height * 1.2f;
                        var right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
                        var fwd = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
                        _focus -= (right * delta.x + fwd * delta.y) * k;
                    }
                }
                _dragging = true;
                _lastPointer = pos;
            }
            else
            {
                _dragging = false;
            }
        }
    }
}
