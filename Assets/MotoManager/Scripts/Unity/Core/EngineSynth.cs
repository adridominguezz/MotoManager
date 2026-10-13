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
        /// <summary>Length of a combustion pulse as a fraction of the gap between firings (short = sharper, raspier).</summary>
        public float PulseFraction = 0.38f;
        /// <summary>Reflection at the open end of the pipe and damping inside it.</summary>
        public float Reflection = 0.62f, LoopDamping = 0.45f;
        /// <summary>Muffler low-pass: base cut-off and how much it opens with the throttle (Hz).</summary>
        public float MufflerBase = 900f, MufflerThrottle = 3200f;
        /// <summary>Mix of the outlet's pressure change (bright) and pressure (body).</summary>
        public float Radiation = 6f, Direct = 0.8f;
        public float IntakeLevel = 0.35f, Roughness = 0.25f;
        /// <summary>Final low-pass (Hz): the distance and the fairing take the fizz off.</summary>
        public float FinalCutoff = 20000f;
        /// <summary>
        /// What keeps it from sounding synthetic: firing time and strength vary a little from one stroke to the
        /// next, a low "body" (crankcase and airbox thump) under the exhaust, and asymmetric saturation that adds
        /// even harmonics (warmth) instead of the odd ones of a symmetric clip (metallic).
        /// </summary>
        public float TimingJitter = 0.02f, StrengthJitter = 0.15f, Body = 0.5f, Warmth = 0.25f;
        public float Character = 1f;
        public int Seed = 1;

        private const int MaxDelay = 2048;
        private int _rate = 44100;
        private double _cyclePhase;
        private float _rpmNow = 4000, _thrNow = 0.5f, _volNow, _pitchNow = 1;
        private uint _noise = 22222;
        private readonly float[] _pipe = new float[MaxDelay];
        private int _write;
        private float _final1, _final2;
        private float _loopLp, _muffler, _muffler2, _intakeLp, _intakeBand, _dc, _prevOut, _pop;
        // Pulse state: samples left and length of the current pressure pulse.
        private float _pulsePos = -1, _pulseLen = 1, _pulseAmp;
        private float _strengthJitter = 1;
        private float _pulseWait, _body, _dcOut;

        /// <summary>
        /// The engine of a championship: a Moto3 single (one firing every two turns, sharp pulses, open short pipe:
        /// tuned against a recording of a Honda Moto3), the Moto2 Triumph triple, a big-bang MotoGP four and the
        /// superbike / supersport fours.
        /// </summary>
        public void Apply(EngineProfile p)
        {
            Cylinders = p.Firing.Length;
            FiringOrder = p.Firing;
            PulseFraction = p.PulseFraction;
            PipeLength = p.PipeLength;
            Reflection = p.Reflection;
            LoopDamping = p.LoopDamping;
            MufflerBase = p.MufflerBase;
            MufflerThrottle = p.MufflerThrottle;
            Radiation = p.Radiation;
            Direct = p.Direct;
            IntakeLevel = p.IntakeLevel;
            Roughness = p.Roughness;
            FinalCutoff = p.FinalCutoff;
            TimingJitter = p.TimingJitter;
            StrengthJitter = p.StrengthJitter;
            Body = p.Body;
            Warmth = p.Warmth;
        }

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
            float kFinal = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Min(FinalCutoff, _rate * 0.45f) * invRate);
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
                    _pulseLen = Mathf.Clamp(gap * PulseFraction, 3f, 900f);
                    _pulsePos = 0;
                    // A combustion is never exactly on time: start it a fraction of the gap late.
                    _pulseWait = TimingJitter * gap * (Noise() * 0.5f + 0.5f);
                    _strengthJitter = 1f - StrengthJitter + 2f * StrengthJitter * (Noise() * 0.5f + 0.5f);
                    _pulseAmp = (0.35f + 0.65f * _thrNow) * _strengthJitter * (c % 2 == 0 ? 1f : 0.93f);
                    // Overrun: unburnt fuel pops in the exhaust when the throttle is shut at high revs.
                    if (_thrNow < 0.2f && _rpmNow > 8000 && Noise() > 0.93f) _pop = 0.6f + 0.4f * (Noise() * 0.5f + 0.5f);
                }
                float pulse = 0;
                if (_pulseWait > 0) _pulseWait -= 1;
                else if (_pulsePos >= 0)
                {
                    // Raised-cosine pressure pulse with a little combustion roughness.
                    float x = _pulsePos / _pulseLen;
                    pulse = _pulseAmp * 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * x)) * (1f + Roughness * Noise() * _thrNow);
                    _pulsePos += 1;
                    if (_pulsePos >= _pulseLen) _pulsePos = -1;
                }

                // Exhaust waveguide: what comes back from the open end, damped and inverted, adds to the new pulse.
                float readPos = _write - delay;
                if (readPos < 0) readPos += MaxDelay;
                int i0 = (int)readPos;
                float frac = readPos - i0;
                float back = _pipe[i0] * (1 - frac) + _pipe[(i0 + 1) % MaxDelay] * frac;
                _loopLp += (back - _loopLp) * LoopDamping;
                float inPipe = pulse - Reflection * _loopLp;
                _pipe[_write] = inPipe;
                _write = (_write + 1) % MaxDelay;
                // Radiated sound ~ change of pressure at the outlet.
                float radiated = inPipe - _prevOut;
                _prevOut = inPipe;

                // Muffler: two one-pole low-passes that open with the throttle.
                float cutoff = MufflerBase + MufflerThrottle * _thrNow + 0.12f * (float)(cycleHz * Cylinders);
                float k = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff * invRate);
                _muffler += (radiated * Radiation + inPipe * Direct - _muffler) * k;
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

                // Body: the pressure in the pipe low-passed near the firing rate.
                _body += (inPipe - _body) * (1f - Mathf.Exp(-2f * Mathf.PI * (float)(cycleHz * Cylinders) * 1.5f * invRate));
                float s = (_muffler2 * 1.3f + intake * IntakeLevel + crackle * 0.4f + _body * Body) * Character;
                _final1 += (s - _final1) * kFinal;
                _final2 += (_final1 - _final2) * kFinal;
                s = _final2;
                // Remove DC and saturate gently.
                _dc += (s - _dc) * 0.0008f;
                float drive = (s - _dc) * 1.3f;
                drive += Warmth * drive * drive;
                float shaped = (float)Math.Tanh(drive);
                _dcOut += (shaped - _dcOut) * 0.0008f;
                s = (shaped - _dcOut) * 0.8f * _volNow;
                for (int ch = 0; ch < channels; ch++) data[i + ch] = s;
            }
        }
    }

    /// <summary>Sound character and rev range of a championship's engines.</summary>
    public class EngineProfile
    {
        public float[] Firing = { 0f, 0.09f, 0.5f, 0.59f };
        public float PulseFraction = 0.38f, PipeLength = 0.85f, Reflection = 0.62f, LoopDamping = 0.45f;
        public float MufflerBase = 900f, MufflerThrottle = 3200f, Radiation = 6f, Direct = 0.8f;
        public float IntakeLevel = 0.35f, Roughness = 0.25f, FinalCutoff = 20000f;
        public float TimingJitter = 0.02f, StrengthJitter = 0.15f, Body = 0.5f, Warmth = 0.25f;
        public float IdleRpm = 4500f, MaxRpm = 17500f;

        public static EngineProfile For(string categoryId)
        {
            switch (categoryId)
            {
                case "moto3":
                case "rookies":
                case "junior_gp":
                    // Fitted to the harmonic envelope and band energy of a Honda NSF250RW recording.
                    return new EngineProfile
                    {
                        Firing = new[] { 0f }, PulseFraction = 0.155f, PipeLength = 0.51f, Reflection = 0.15f, LoopDamping = 0.69f,
                        MufflerBase = 800f, MufflerThrottle = 3470f, Radiation = 5.5f, Direct = 0.8f, IntakeLevel = 1.5f,
                        Roughness = 0.64f, FinalCutoff = 2900f, IdleRpm = 7000f, MaxRpm = 13500f
                    };
                case "moto2":
                case "junior_sbk":
                    // Triumph 765 triple, even 240° firing.
                    return new EngineProfile
                    {
                        Firing = new[] { 0f, 1f / 3f, 2f / 3f }, PulseFraction = 0.24f, PipeLength = 0.65f, Reflection = 0.4f, LoopDamping = 0.55f,
                        MufflerBase = 850f, MufflerThrottle = 3400f, Radiation = 5f, Direct = 0.8f, IntakeLevel = 0.9f,
                        Roughness = 0.45f, FinalCutoff = 3800f, IdleRpm = 5000f, MaxRpm = 14000f
                    };
                case "worldssp":
                    return new EngineProfile
                    {
                        Firing = new[] { 0f, 0.25f, 0.5f, 0.75f }, PulseFraction = 0.3f, PipeLength = 0.75f, IntakeLevel = 0.7f, Radiation = 5f,
                        Roughness = 0.35f, FinalCutoff = 4500f, IdleRpm = 5000f, MaxRpm = 16000f
                    };
                case "worldsbk":
                    return new EngineProfile
                    {
                        Firing = new[] { 0f, 0.25f, 0.5f, 0.75f }, PulseFraction = 0.32f, PipeLength = 0.8f, IntakeLevel = 0.6f, Radiation = 5f,
                        Roughness = 0.35f, FinalCutoff = 4800f, IdleRpm = 4500f, MaxRpm = 15500f
                    };
                default:
                    // MotoGP: big-bang four (two pairs of close firings), the deepest and loudest.
                    return new EngineProfile { IntakeLevel = 0.6f, Roughness = 0.35f, Radiation = 5f, FinalCutoff = 5000f, IdleRpm = 5000f, MaxRpm = 18000f };
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
        private double _smoothSpeed = -1;
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
            // Fast playback: on/off throttle many times a second sounds like stutter; keep it nearer a steady pull.
            wanted = Mathf.Lerp(wanted, 0.8f, 1f - 1f / Mathf.Sqrt(playback));
            // Throttle moves at human speed whatever the playback.
            Throttle = Mathf.Lerp(Throttle, wanted, Mathf.Clamp01(dt * 4f));
            // At 4x / 8x a whole corner passes in a fraction of a second: follow a smoothed speed (the longer the
            // faster the playback) so the engine sweeps like a lap seen at normal pace instead of fluttering.
            if (_smoothSpeed < 0) _smoothSpeed = speed;
            float tau = 0.05f * playback;
            _smoothSpeed += (speed - _smoothSpeed) * (1 - Math.Exp(-dt / Math.Max(0.01f, tau)));
            speed = _smoothSpeed;
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
                    _shiftCooldown = ShiftGap * Mathf.Sqrt(playback);
                }
                else if (Gear > 1 && rpmFor(Gear - 1) < MaxRpm * 0.85f && (r < MaxRpm * 0.62f || accel < -4))
                {
                    Gear--;
                    _shiftCooldown = ShiftGap * 0.6f * Mathf.Sqrt(playback);
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
