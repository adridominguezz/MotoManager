using UnityEngine;

namespace MotoManager.App
{
    /// <summary>
    /// Aims for 60 FPS and drops to a stable 30 on devices that cannot sustain it (and back up when they can),
    /// instead of an irregular frame rate. Also keeps the screen awake during races.
    /// </summary>
    public class FrameRateGovernor : MonoBehaviour
    {
        public int HighFps = 60;
        public int LowFps = 30;
        public float SampleSeconds = 4f;

        private float _elapsed;
        private int _frames;
        private int _target;
        private int _goodSamplesAtLow;

        private void Start()
        {
            QualitySettings.vSyncCount = 0;
            SetTarget(HighFps);
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private void SetTarget(int fps)
        {
            _target = fps;
            Application.targetFrameRate = fps;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            _frames++;
            if (_elapsed < SampleSeconds) return;
            float fps = _frames / _elapsed;
            _elapsed = 0;
            _frames = 0;
            if (_target == HighFps && fps < HighFps * 0.8f)
            {
                SetTarget(LowFps);
                _goodSamplesAtLow = 0;
            }
            else if (_target == LowFps && fps >= LowFps * 0.98f)
            {
                // After a while at a steady 30, try 60 again (e.g. device cooled down, heavy screen closed).
                if (++_goodSamplesAtLow >= 15) SetTarget(HighFps);
            }
        }
    }
}
