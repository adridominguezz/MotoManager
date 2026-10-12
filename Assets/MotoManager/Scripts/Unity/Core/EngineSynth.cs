using System;
using UnityEngine;

namespace MotoManager.App
{
    /// <summary>
    /// A motorcycle engine synthesised in real time on the audio thread. Each cylinder firing sends a short, smooth
    /// pressure pulse (an uneven "big bang" order gives the growl) into an exhaust pipe modelled as a waveguide: a
    /// delay line the length of the pipe with an inverting, damped reflection at the open end. The pipe's comb of
    /// resonances is what turns a pulse train into the "brap" of a real exhaust. Intake roar (filtered noise that
    /// follows the throttle), overrun crackle, a muffler low-pass that opens with the throttle and gentle saturation
    /// finish it. <see cref="Pitch"/> shifts everything (Doppler of a passing bike). The game thread only sets
    /// <see cref="Rpm"/>, <see cref="Throttle"/>, <see cref="Pitch"/> and <see cref="Volume"/>.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class EngineSynth : MonoBehaviour
    {
        public volatile float Rpm = 4000;
        public volatile float Throttle = 0.5f;
        public volatile float Volume;
        public volatile float Pitch = 1f;
        public int Cylinders = 4;
        /// <summary>Fraction of the 4-stroke cycle at which each cylinder fires.</summary>
        public float[] FiringOrder = { 0f, 0.25f, 0.5f, 0.75f };
        /// <summary>Exhaust length in metres (longer = deeper).</summary>
        public float PipeLength = 0.85f;
        public float Character = 1f;
        public int Seed = 1;

        private const int MaxDelay = 2048;
        private int _rate = 44100;
        private double _cyclePhase;
        private float _rpmNow = 4000, _thrNow = 0.5f, _volNow, _pitchNow = 1;
        private uint _noise = 22222;
        private readonly float[] _pipe = new float[MaxDelay];
        private int _write;
        private float _loopLp, _muffler, _muffler2, _intakeLp, _intakeBand, _dc, _prevOut, _pop;
        // Pulse state: samples left and length of the current pressure pulse.
        private float _pulsePos = -1, _pulseLen = 1, _pulseAmp;
        private float _strengthJitter = 1;

        private void Awake()
        {
            _rate = AudioSettings.outputSampleRate;
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
            float targetRpm = Rpm, targetThr = Throttle, targetVol = Volume, targetPitch = Pitch;
            float invRate = 1f / _rate;
            // Pipe round trip: twice the length at the speed of sound in hot exhaust gas (~520 m/s).
            float delay = Mathf.Clamp(2f * PipeLength / 520f * _rate, 8f, MaxDelay - 2);
            for (int i = 0; i < data.Length; i += channels)
            {
                // Smooth parameter changes (revs move fast, but never step).
                _rpmNow += (targetRpm - _rpmNow) * 0.002f;
                _thrNow += (targetThr - _thrNow) * 0.0012f;
                _volNow += (targetVol - _volNow) * 0.001f;
                _pitchNow += (targetPitch - _pitchNow) * 0.002f;

                // One 4-stroke cycle = two crank revolutions.
                double cycleHz = _rpmNow / 120.0 * _pitchNow;
                double prev = _cyclePhase;
                _cyclePhase += cycleHz * invRate;
                if (_cyclePhase >= 1) _cyclePhase -= 1;
                for (int c = 0; c < FiringOrder.Length && c < Cylinders; c++)
                {
                    double at = FiringOrder[c];
                    bool crossed = prev <= _cyclePhase ? (prev < at && _cyclePhase >= at) : (prev < at || _cyclePhase >= at);
                    if (!crossed) continue;
                    // A new pressure pulse: about a third of the gap between firings, louder under load.
                    float gap = (float)(_rate / (cycleHz * Cylinders));
                    _pulseLen = Mathf.Clamp(gap * 0.38f, 6f, 600f);
                    _pulsePos = 0;
                    _strengthJitter = 0.94f + 0.12f * (Noise() * 0.5f + 0.5f);
                    _pulseAmp = (0.35f + 0.65f * _thrNow) * _strengthJitter * (c % 2 == 0 ? 1f : 0.93f);
                    // Overrun: unburnt fuel pops in the exhaust when the throttle is shut at high revs.
                    if (_thrNow < 0.2f && _rpmNow > 8000 && Noise() > 0.93f) _pop = 0.6f + 0.4f * (Noise() * 0.5f + 0.5f);
                }
                float pulse = 0;
                if (_pulsePos >= 0)
                {
                    // Raised-cosine pressure pulse with a little combustion roughness.
                    float x = _pulsePos / _pulseLen;
                    pulse = _pulseAmp * 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * x)) * (1f + 0.25f * Noise() * _thrNow);
                    _pulsePos += 1;
                    if (_pulsePos >= _pulseLen) _pulsePos = -1;
                }

                // Exhaust waveguide: what comes back from the open end, damped and inverted, adds to the new pulse.
                float readPos = _write - delay;
                if (readPos < 0) readPos += MaxDelay;
                int i0 = (int)readPos;
                float frac = readPos - i0;
                float back = _pipe[i0] * (1 - frac) + _pipe[(i0 + 1) % MaxDelay] * frac;
                _loopLp += (back - _loopLp) * 0.45f;
                float inPipe = pulse - 0.62f * _loopLp;
                _pipe[_write] = inPipe;
                _write = (_write + 1) % MaxDelay;
                // Radiated sound ~ change of pressure at the outlet.
                float radiated = inPipe - _prevOut;
                _prevOut = inPipe;

                // Muffler: two one-pole low-passes that open with the throttle.
                float cutoff = 900f + 3200f * _thrNow + 0.12f * (float)(cycleHz * Cylinders);
                float k = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff * invRate);
                _muffler += (radiated * 6f + inPipe * 0.8f - _muffler) * k;
                _muffler2 += (_muffler - _muffler2) * k;

                // Intake roar: band of noise around 1.1 kHz, only with the throttle open.
                float n = Noise();
                _intakeLp += (n - _intakeLp) * 0.25f;
                _intakeBand += ((n - _intakeLp) - _intakeBand) * 0.2f;
                float intake = _intakeBand * _thrNow * _thrNow * (0.25f + pulse);

                float crackle = 0;
                if (_pop > 0.01f)
                {
                    crackle = Noise() * _pop * (Noise() > 0.6f ? 1f : 0.2f);
                    _pop *= 0.9985f;
                }

                float s = (_muffler2 * 1.3f + intake * 0.35f + crackle * 0.4f) * Character;
                // Remove DC and saturate gently.
                _dc += (s - _dc) * 0.0008f;
                s = (float)Math.Tanh((s - _dc) * 1.3f) * 0.8f * _volNow;
                for (int ch = 0; ch < channels; ch++) data[i + ch] = s;
            }
        }
    }

    /// <summary>
    /// Turns a bike's speed into revs and gear (six gears, up-shift near the limiter, down-shift when braking) and
    /// a throttle position from its acceleration. Races are usually watched at 4x or 8x: the bike's speed then
    /// changes several times faster than in reality, so the revs follow it at the rate a real engine could (rise
    /// and fall limited per real second), shifts are spaced out in real time and acceleration is measured in race
    /// time. The result sounds like a bike lapping, not a recording played fast.
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
        private float _since, _shiftCooldown, _shiftDip;
        private const float RiseRate = 9000f, FallRate = 14000f, ShiftGap = 0.35f;

        public void Update(double speed, double topSpeed, float dt, float playback = 1f)
        {
            playback = Mathf.Max(1f, playback);
            // The simulation moves in steps: measure the acceleration between real changes of speed, in race time.
            _since += dt;
            if (Math.Abs(speed - _lastSpeed) > 1e-6 || _since > 0.5f)
            {
                _accel = _since > 0 ? (speed - _lastSpeed) / (_since * playback) : 0;
                _lastSpeed = speed;
                _since = 0;
            }
            double accel = _accel;
            float wanted = accel > 0.3 ? 1f : accel < -1.5 ? 0f : 0.45f;
            // Throttle moves at human speed whatever the playback.
            Throttle = Mathf.Lerp(Throttle, wanted, Mathf.Clamp01(dt * 4f));
            double top = Math.Max(40, topSpeed);
            float rpmFor(int g) => (float)(MaxRpm * Math.Min(1.05, speed / (top * _top[g - 1])));
            _shiftCooldown = Mathf.Max(0, _shiftCooldown - dt);
            float r = rpmFor(Gear);
            if (_shiftCooldown <= 0)
            {
                if (r > MaxRpm * 0.97f && Gear < 6 && Rpm > MaxRpm * 0.9f)
                {
                    Gear++;
                    _shiftDip = 0.08f;
                    _shiftCooldown = ShiftGap;
                }
                else if (Gear > 1 && rpmFor(Gear - 1) < MaxRpm * 0.85f && (r < MaxRpm * 0.62f || accel < -4))
                {
                    Gear--;
                    _shiftCooldown = ShiftGap * 0.6f;
                }
            }
            float target = Mathf.Max(IdleRpm, rpmFor(Gear));
            if (_shiftDip > 0)
            {
                _shiftDip = Mathf.Max(0, _shiftDip - dt);
                target *= 0.88f;
            }
            // Revs can only rise and fall so fast in real time.
            float step = (target > Rpm ? RiseRate : FallRate) * dt;
            Rpm = Mathf.Clamp(Mathf.MoveTowards(Rpm, target, step), IdleRpm, MaxRpm * 1.02f);
        }
    }
}
