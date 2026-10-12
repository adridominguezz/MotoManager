using System;
using UnityEngine;

namespace MotoManager.App
{
    /// <summary>
    /// All the game's sound, synthesised at start (no audio files): a looping menu theme, the race soundscape (the
    /// followed bike's engine, whose pitch follows its speed and gear, the rest of the field and the crowd) and a
    /// soft click for buttons. Music and effects can be switched off (remembered on the device).
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        private const int Rate = 22050;
        private const string MusicPref = "MotoManager.Music";
        private const string SfxPref = "MotoManager.Sfx";

        private static GameAudio _instance;
        private AudioSource _music, _engine, _field, _crowd, _ui;
        private AudioClip _click;
        private bool _racing;
        private float _targetPitch = 1, _targetEngine, _targetField;

        public static GameAudio Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("GameAudio");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<GameAudio>();
                    _instance.Build();
                }
                return _instance;
            }
        }

        public static bool MusicOn
        {
            get { return Pref(MusicPref); }
            set { SetPref(MusicPref, value); if (_instance != null) _instance.ApplyMusic(); }
        }

        public static bool SfxOn
        {
            get { return Pref(SfxPref); }
            set { SetPref(SfxPref, value); if (_instance != null && !value) _instance.StopRace(); }
        }

        private static bool Pref(string key)
        {
            try { return PlayerPrefs.GetInt(key, 1) == 1; } catch (Exception) { return true; }
        }

        private static void SetPref(string key, bool on)
        {
            try
            {
                PlayerPrefs.SetInt(key, on ? 1 : 0);
                PlayerPrefs.Save();
            }
            catch (Exception) { }
        }

        private AudioSource Source(bool loop, float volume)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = 0;
            s.volume = volume;
            return s;
        }

        private void Build()
        {
            _music = Source(true, 0.35f);
            _music.clip = MenuTheme();
            _engine = Source(true, 0);
            _engine.clip = EngineLoop(140, 0.9f);
            _field = Source(true, 0);
            _field.clip = EngineLoop(97, 0.5f);
            _crowd = Source(true, 0);
            _crowd.clip = CrowdLoop();
            _ui = Source(false, 0.5f);
            _click = Click();
            ApplyMusic();
        }

        private void ApplyMusic()
        {
            if (_music == null) return;
            if (MusicOn && !_racing)
            {
                if (!_music.isPlaying) _music.Play();
            }
            else _music.Stop();
        }

        /// <summary>Menu theme on (called by the management screens).</summary>
        public static void Menu()
        {
            var a = Instance;
            a.StopRace();
            a.ApplyMusic();
        }

        public static void ClickSound()
        {
            if (!SfxOn) return;
            var a = Instance;
            a._ui.PlayOneShot(a._click, 0.6f);
        }

        /// <summary>The race soundscape starts (menu music fades out).</summary>
        public static void RaceStart()
        {
            var a = Instance;
            a._racing = true;
            a._music.Stop();
            if (!SfxOn) return;
            foreach (var s in new[] { a._engine, a._field, a._crowd })
            {
                s.volume = 0;
                if (!s.isPlaying) s.Play();
            }
        }

        public static void RaceEnd()
        {
            if (_instance == null) return;
            _instance.StopRace();
            _instance.ApplyMusic();
        }

        /// <summary>
        /// Each frame of a race: the followed bike's speed (m/s, its top speed in the category for the gear steps),
        /// how close the camera is (0 far, 1 close) and how many bikes are running.
        /// </summary>
        public static void RaceUpdate(double speed, double topSpeed, float closeness, int running, bool paused)
        {
            if (_instance == null || !_instance._racing || !SfxOn) return;
            var a = _instance;
            if (paused || running == 0)
            {
                a._targetEngine = 0;
                a._targetField = 0;
                return;
            }
            // Six gears: within each the revs climb, then drop at the shift.
            double v = Math.Max(0, Math.Min(1, speed / Math.Max(30, topSpeed)));
            double gears = 6;
            double inGear = (v * gears) % 1.0;
            int gear = (int)Math.Min(gears - 1, v * gears);
            double revs = 0.55 + 0.45 * inGear + gear * 0.04;
            a._targetPitch = (float)(0.6 + revs * 1.1);
            a._targetEngine = Mathf.Lerp(0.08f, 0.55f, closeness);
            a._targetField = Mathf.Clamp01(0.12f + running * 0.012f) * Mathf.Lerp(1f, 0.6f, closeness);
        }

        private void StopRace()
        {
            _racing = false;
            if (_engine == null) return;
            _engine.Stop();
            _field.Stop();
            _crowd.Stop();
        }

        private void Update()
        {
            if (!_racing || _engine == null) return;
            float k = Mathf.Clamp01(Time.unscaledDeltaTime * 6);
            _engine.pitch = Mathf.Lerp(_engine.pitch, _targetPitch, k);
            _engine.volume = Mathf.Lerp(_engine.volume, _targetEngine, k);
            _field.volume = Mathf.Lerp(_field.volume, _targetField, k);
            _field.pitch = 0.95f + 0.1f * Mathf.PerlinNoise(Time.unscaledTime * 0.3f, 0);
            _crowd.volume = Mathf.Lerp(_crowd.volume, _targetField * 0.5f, k * 0.5f);
        }

        // ------------------------------------------------------------------ synthesis

        /// <summary>
        /// An engine at idle frequency <paramref name="baseHz"/>: firing pulses rich in harmonics with a little
        /// roughness; playback pitch makes the revs.
        /// </summary>
        private static AudioClip EngineLoop(float baseHz, float roughness)
        {
            // Whole number of cycles so the loop is seamless.
            int cycles = (int)(baseHz * 1.0f);
            int n = (int)Math.Round(Rate * cycles / baseHz);
            var data = new float[n];
            var rng = new System.Random(7);
            double lp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                double ph = (t * baseHz) % 1.0;
                // Firing pulse train + odd harmonics + a sub-harmonic rumble (twin / four-cylinder character).
                double pulse = Math.Exp(-ph * 9) * 2 - 0.25;
                double harm = 0.5 * Math.Sin(2 * Math.PI * baseHz * t) + 0.25 * Math.Sin(2 * Math.PI * baseHz * 2 * t) +
                              0.18 * Math.Sin(2 * Math.PI * baseHz * 3 * t + 0.6) + 0.22 * Math.Sin(Math.PI * baseHz * t);
                double noise = (rng.NextDouble() * 2 - 1) * roughness;
                lp += (noise - lp) * 0.08;
                data[i] = (float)Math.Max(-1, Math.Min(1, (pulse * 0.35 + harm * 0.5 + lp * 0.5) * 0.6));
            }
            var clip = AudioClip.Create("Engine" + baseHz, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Distant crowd: filtered noise with slow swells.</summary>
        private static AudioClip CrowdLoop()
        {
            int n = Rate * 6;
            var data = new float[n];
            var rng = new System.Random(11);
            double a = 0, b = 0;
            for (int i = 0; i < n; i++)
            {
                double white = rng.NextDouble() * 2 - 1;
                a += (white - a) * 0.05;
                b += (a - b) * 0.2;
                double t = (double)i / n;
                double swell = 0.6 + 0.4 * Math.Sin(2 * Math.PI * t) * Math.Sin(2 * Math.PI * 3 * t);
                data[i] = (float)(b * 2.5 * swell);
            }
            // Cross-fade the ends so the loop does not click.
            int fade = Rate / 4;
            for (int i = 0; i < fade; i++)
            {
                float w = (float)i / fade;
                data[i] = data[i] * w + data[n - fade + i] * (1 - w);
            }
            var clip = AudioClip.Create("Crowd", n - fade, 1, Rate, false);
            var trimmed = new float[n - fade];
            Array.Copy(data, trimmed, n - fade);
            clip.SetData(trimmed, 0);
            return clip;
        }

        private static AudioClip Click()
        {
            int n = Rate / 30;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                data[i] = (float)(Math.Sin(2 * Math.PI * 1800 * t) * Math.Exp(-t * 180) * 0.5);
            }
            var clip = AudioClip.Create("Click", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// A driving, racing-flavoured loop (A minor, 112 bpm, 8 bars): pulsing bass, a pad on the chords, a
        /// simple arpeggio and light drums.
        /// </summary>
        private static AudioClip MenuTheme()
        {
            const double bpm = 112;
            double beat = 60.0 / bpm;
            int bars = 8;
            int n = (int)(Rate * beat * 4 * bars);
            var data = new float[n];
            // Am - F - C - G, two bars each.
            double[][] chords =
            {
                new[] { 220.00, 261.63, 329.63 }, new[] { 174.61, 220.00, 261.63 },
                new[] { 261.63, 329.63, 392.00 }, new[] { 196.00, 246.94, 293.66 }
            };
            double[] roots = { 110.00, 87.31, 130.81, 98.00 };
            var rng = new System.Random(3);
            double hatLp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                int beatIndex = (int)(t / beat);
                int chord = (beatIndex / 8) % 4;
                double inBeat = (t % beat) / beat;
                double eighth = (t % (beat / 2)) / (beat / 2);

                // Bass: eighth-note pulse on the root (saw-ish), slight decay per note.
                double root = roots[chord];
                double saw = 2 * ((t * root) % 1.0) - 1;
                double bass = (0.6 * saw + 0.4 * Math.Sin(2 * Math.PI * root * t)) * Math.Exp(-eighth * 3) * 0.32;

                // Pad: soft chord, slow attack at each chord change.
                double chordT = (t % (beat * 8)) / (beat * 8);
                double padEnv = Math.Min(1, chordT * 6) * (1 - 0.3 * chordT);
                double pad = 0;
                foreach (var f in chords[chord]) pad += Math.Sin(2 * Math.PI * f * t) + 0.3 * Math.Sin(2 * Math.PI * f * 2.003 * t);
                pad *= 0.045 * padEnv;

                // Arpeggio: sixteenths over the chord an octave up.
                int step16 = (int)(t / (beat / 4));
                double f16 = chords[chord][step16 % 3] * 2;
                double in16 = (t % (beat / 4)) / (beat / 4);
                double arp = Math.Sign(Math.Sin(2 * Math.PI * f16 * t)) * Math.Exp(-in16 * 6) * 0.035;

                // Drums: kick on every beat, snare on 2 and 4, closed hat on off-beats.
                double kick = Math.Sin(2 * Math.PI * (55 + 90 * Math.Exp(-inBeat * 30)) * inBeat * beat) * Math.Exp(-inBeat * 12) * 0.45;
                double noise = rng.NextDouble() * 2 - 1;
                hatLp += (noise - hatLp) * 0.6;
                double hatNoise = noise - hatLp;
                double snare = (beatIndex % 2 == 1) ? noise * Math.Exp(-inBeat * 18) * 0.18 : 0;
                double hat = eighth > 0 && ((int)(t / (beat / 2)) % 2 == 1) ? hatNoise * Math.Exp(-eighth * 25) * 0.08 : 0;

                double mix = bass + pad + arp + kick + snare + hat;
                data[i] = (float)Math.Max(-1, Math.Min(1, mix * 0.8));
            }
            var clip = AudioClip.Create("MenuTheme", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
