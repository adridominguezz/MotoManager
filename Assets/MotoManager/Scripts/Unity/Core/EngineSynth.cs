using System;
using UnityEngine;

namespace MotoManager.App
{
    /// <summary>
    /// A motorcycle engine synthesised in real time on the audio thread: combustion pulses at the firing rate of the
    /// current revs (an uneven "big bang" firing order gives the growl), shaped by exhaust and airbox resonances,
    /// with combustion rasp that grows with the throttle, crackles on the overrun and soft saturation. The game
    /// thread only sets <see cref="Rpm"/>, <see cref="Throttle"/> and <see cref="Volume"/>.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class EngineSynth : MonoBehaviour
    {
        public volatile float Rpm = 4000;
        public volatile float Throttle = 0.5f;
        public volatile float Volume;
        /// <summary>Firing events per crank revolution / 2 (4-stroke): 4 cylinders = 2 per revolution.</summary>
        public int Cylinders = 4;
        /// <summary>Uneven firing (fraction of the cycle at which each cylinder fires).</summary>
        public float[] FiringOrder = { 0f, 0.25f, 0.5f, 0.75f };
        public float Character = 1f;
        public int Seed = 1;

        private int _rate = 44100;
        private double _cyclePhase;
        private float _rpmNow = 4000, _thrNow = 0.5f, _volNow;
        private System.Random _rng;
        private uint _noise = 22222;
        // Resonant filters (state variable): exhaust, airbox, body.
        private Svf _exhaust, _airbox, _body;
        private float _dc;
        private float _pop;
        private int _cylIndex;
        private float _pulse;

        private struct Svf
        {
            public float Low, Band;

            /// <summary>Band-pass output of a Chamberlin state-variable filter.</summary>
            public float Process(float input, float f, float q)
            {
                Low += f * Band;
                float high = input - Low - q * Band;
                Band += f * high;
                return Band;
            }
        }

        private void Awake()
        {
            _rate = AudioSettings.outputSampleRate;
            _rng = new System.Random(Seed);
            _noise = (uint)(12345 + Seed * 7919);
            // The source plays a silent clip: the audio is written in OnAudioFilterRead.
            var src = GetComponent<AudioSource>();
            src.clip = AudioClip.Create("EngineCarrier", _rate, 1, _rate, false);
            src.loop = true;
            src.spatialBlend = 0;
            src.volume = 1;
        }

        private float Noise()
        {
            _noise ^= _noise << 13;
            _noise ^= _noise >> 17;
            _noise ^= _noise << 5;
            return (_noise & 0xFFFFFF) / 8388608f - 1f;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float targetRpm = Rpm, targetThr = Throttle, targetVol = Volume;
            float invRate = 1f / _rate;
            for (int i = 0; i < data.Length; i += channels)
            {
                // Smooth parameter changes (revs move fast, but never step).
                _rpmNow += (targetRpm - _rpmNow) * 0.0025f;
                _thrNow += (targetThr - _thrNow) * 0.0015f;
                _volNow += (targetVol - _volNow) * 0.001f;

                // One 4-stroke cycle = two crank revolutions.
                double cycleHz = _rpmNow / 120.0;
                double prev = _cyclePhase;
                _cyclePhase += cycleHz * invRate;
                if (_cyclePhase >= 1) _cyclePhase -= 1;

                // A cylinder fires when the cycle passes its firing point: a sharp combustion pulse.
                for (int c = 0; c < FiringOrder.Length && c < Cylinders; c++)
                {
                    double at = FiringOrder[c];
                    bool crossed = prev <= _cyclePhase ? (prev < at && _cyclePhase >= at) : (prev < at || _cyclePhase >= at);
                    if (!crossed) continue;
                    // Each cylinder a little different, louder under load.
                    float strength = (0.55f + 0.45f * _thrNow) * (0.85f + 0.3f * (float)_rng.NextDouble()) * (c % 2 == 0 ? 1f : 0.9f);
                    _pulse += strength;
                    _cylIndex = c;
                    // Overrun: unburnt fuel pops in the exhaust when the throttle is shut at high revs.
                    if (_thrNow < 0.15f && _rpmNow > 7000 && _rng.NextDouble() < 0.025) _pop = 0.9f + (float)_rng.NextDouble() * 0.6f;
                }
                // Pulse decays quickly (a few milliseconds, shorter at high revs).
                float tau = Mathf.Clamp(0.012f * 4000f / Mathf.Max(1000f, _rpmNow), 0.0012f, 0.006f);
                _pulse *= Mathf.Exp(-invRate / tau);

                // Combustion rasp: noise gated by the pulses, brighter with throttle.
                float rasp = Noise() * _pulse * (0.25f + 0.55f * _thrNow);
                float excite = _pulse + rasp;

                // Resonances: exhaust pipe follows the revs a little, airbox and body are fixed.
                float firing = (float)(cycleHz * Cylinders);
                float fEx = 2f * Mathf.Sin(Mathf.PI * Mathf.Clamp(firing * 1.6f + 180f, 80f, 4000f) * invRate);
                float fAir = 2f * Mathf.Sin(Mathf.PI * 720f * invRate);
                float fBody = 2f * Mathf.Sin(Mathf.PI * 1650f * invRate);
                float ex = _exhaust.Process(excite, fEx, 0.35f);
                float air = _airbox.Process(excite, fAir, 0.6f) * (0.3f + 0.7f * _thrNow);
                float body = _body.Process(rasp, fBody, 0.8f) * 0.5f;

                // Overrun crackle: a short burst of noise.
                float crackle = 0;
                if (_pop > 0.01f)
                {
                    crackle = Noise() * _pop;
                    _pop *= 0.992f;
                }

                float s = ex * 1.1f + air * 0.6f + body * 0.35f + excite * 0.15f + crackle * 0.5f;
                s *= Character;
                // Remove DC and saturate gently.
                _dc += (s - _dc) * 0.001f;
                s = (float)Math.Tanh((s - _dc) * 1.6f) * _volNow;

                for (int ch = 0; ch < channels; ch++) data[i + ch] = s;
            }
        }
    }

    /// <summary>
    /// Turns a bike's speed into revs and gear (six gears, up-shift near the limiter, down-shift when braking) and
    /// a throttle position from its acceleration.
    /// </summary>
    public class Gearbox
    {
        public float IdleRpm = 4500;
        public float MaxRpm = 17500;
        public int Gear = 1;
        public float Rpm = 4500;
        public float Throttle;
        private readonly float[] _top = { 0.30f, 0.44f, 0.58f, 0.72f, 0.86f, 1.0f };
        private double _lastSpeed, _accel;
        private float _since;
        private float _shiftDip;

        public void Update(double speed, double topSpeed, float dt)
        {
            // The simulation moves in steps: measure the acceleration between real changes of speed.
            _since += dt;
            if (Math.Abs(speed - _lastSpeed) > 1e-6 || _since > 0.5f)
            {
                _accel = _since > 0 ? (speed - _lastSpeed) / _since : 0;
                _lastSpeed = speed;
                _since = 0;
            }
            double accel = _accel;
            Throttle = Mathf.Lerp(Throttle, accel > 0.3 ? 1f : accel < -1.5 ? 0f : 0.45f, Mathf.Clamp01(dt * 6));
            double top = Math.Max(40, topSpeed);
            float rpmFor(int g) => (float)(MaxRpm * Math.Min(1.05, speed / (top * _top[g - 1])));
            float r = rpmFor(Gear);
            if (r > MaxRpm * 0.97f && Gear < 6)
            {
                Gear++;
                _shiftDip = 0.08f;
            }
            else if (Gear > 1 && rpmFor(Gear - 1) < MaxRpm * 0.85f && (r < MaxRpm * 0.62f || accel < -4))
            {
                Gear--;
            }
            r = rpmFor(Gear);
            // A brief dip at each up-shift, the clutch-less blip.
            if (_shiftDip > 0) _shiftDip = Math.Max(0, _shiftDip - dt);
            Rpm = Mathf.Max(IdleRpm, r * (1f - (_shiftDip > 0 ? 0.12f : 0f)));
        }
    }
}
