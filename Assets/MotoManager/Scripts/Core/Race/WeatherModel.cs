using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;

namespace MotoManager.Core.Race
{
    /// <summary>
    /// Rain intensity follows a hidden timeline; the track wetness integrates it gradually (it never flips from
    /// dry to wet in one step) and dries with temperature and traffic.
    /// </summary>
    public class WeatherModel
    {
        private readonly WeatherSetup _setup;
        private readonly SimulationTuning _tuning;
        private readonly ulong _forecastSeed;

        public double Time { get; private set; }
        public double RainIntensity { get; private set; }
        public double TrackWetness { get; private set; }
        public double AirTemp { get; private set; }
        public double TrackTemp { get; private set; }
        public WeatherCondition Condition { get; private set; }

        public WeatherModel(WeatherSetup setup, SimulationTuning tuning, ulong seed)
        {
            _setup = setup;
            _tuning = tuning;
            _forecastSeed = seed ^ 0xF0F0C457UL;
            TrackWetness = MathX.Clamp01(setup.InitialWetness);
            AirTemp = setup.AirTemp;
            Update(0);
        }

        public static WeatherSetup Generate(CircuitDefinition circuit, DateTime date, DeterministicRandom rng, double sessionDurationS)
        {
            var setup = new WeatherSetup();
            // Seasonal temperature swing (northern hemisphere peak in July, southern in January).
            bool southern = circuit.Country == "AU" || circuit.Country == "AR" || circuit.Country == "BR" || circuit.Country == "ID";
            double phase = Math.Cos((date.DayOfYear - 196) / 365.0 * 2 * Math.PI);
            double seasonal = southern ? -phase : phase;
            setup.AirTemp = circuit.AverageAirTempC + seasonal * 4 + rng.Gaussian(0, 3);
            setup.CloudCover = MathX.Clamp01(rng.Range(0, 0.7) + circuit.RainChance);
            if (rng.Chance(circuit.RainChance))
            {
                setup.CloudCover = MathX.Clamp01(setup.CloudCover + 0.3);
                // Either already wet at the start or rain arrives during the session.
                if (rng.Chance(0.4))
                {
                    setup.InitialWetness = rng.Range(0.4, 1.0);
                    if (rng.Chance(0.6))
                    {
                        setup.RainEvents.Add(new RainEvent
                        {
                            StartTime = -300,
                            RampTime = 60,
                            Duration = rng.Range(300, sessionDurationS),
                            Intensity = rng.Range(0.3, 0.9)
                        });
                    }
                }
                else
                {
                    setup.RainEvents.Add(new RainEvent
                    {
                        StartTime = rng.Range(0.1, 0.8) * sessionDurationS,
                        RampTime = rng.Range(60, 240),
                        Duration = rng.Range(240, 1500),
                        Intensity = rng.Range(0.2, 1.0)
                    });
                }
            }
            return setup;
        }

        public double RainAt(double t)
        {
            double r = 0;
            foreach (var e in _setup.RainEvents)
            {
                double local = t - e.StartTime;
                if (local < 0 || local > e.Duration + e.RampTime) continue;
                double up = MathX.SmoothStep(0, e.RampTime, local);
                double down = 1 - MathX.SmoothStep(e.Duration, e.Duration + e.RampTime, local);
                r = Math.Max(r, e.Intensity * Math.Min(up, down));
            }
            return r;
        }

        public void Update(double dt, double traffic = 0)
        {
            Time += dt;
            RainIntensity = RainAt(Time);
            double sun = (1 - _setup.CloudCover) * (1 - Math.Min(1, RainIntensity * 3));
            AirTemp = _setup.AirTemp - RainIntensity * 3;
            double targetTrack = AirTemp + 4 + 14 * sun - 6 * TrackWetness;
            TrackTemp = dt <= 0 ? targetTrack : MathX.MoveTowards(TrackTemp, targetTrack, dt * 0.02);

            if (dt > 0)
            {
                double wetting = RainIntensity * _tuning.WettingRate * (1 - TrackWetness) * dt;
                double dryFactor = Math.Max(0.2, TrackTemp / 30.0) * (1 + 0.6 * MathX.Clamp01(traffic));
                double drying = (1 - Math.Min(1, RainIntensity * 4)) * _tuning.DryingRate * dryFactor * dt * (0.3 + TrackWetness);
                TrackWetness = MathX.Clamp01(TrackWetness + wetting - drying);
            }
            Condition = Classify();
        }

        private WeatherCondition Classify()
        {
            if (RainIntensity > 0.65) return WeatherCondition.HeavyRain;
            if (RainIntensity > 0.3) return WeatherCondition.Rain;
            if (RainIntensity > 0.04) return WeatherCondition.LightRain;
            if (TrackWetness > 0.45) return WeatherCondition.Wet;
            if (TrackWetness > 0.05) return WeatherCondition.Drying;
            return _setup.CloudCover > 0.55 ? WeatherCondition.Cloudy : WeatherCondition.Dry;
        }

        /// <summary>
        /// Probability of rain within the next <paramref name="horizonS"/> seconds as reported by the team.
        /// It is noisy (more so far ahead and with a weaker data engineer) and never gives exact timings.
        /// Uses hashed noise so querying it never disturbs the simulation's random stream.
        /// </summary>
        public double ForecastRainProbability(double horizonS, double forecasterSkill0To100)
        {
            bool truth = false;
            double firstRain = double.MaxValue;
            for (double t = Time; t <= Time + horizonS; t += 15)
            {
                if (RainAt(t) > 0.05)
                {
                    truth = true;
                    firstRain = t - Time;
                    break;
                }
            }
            ulong h = DeterministicRandom.Combine(_forecastSeed, (ulong)(Time / 30.0));
            h = DeterministicRandom.Combine(h, (ulong)horizonS);
            double noise = ((h >> 11) * (1.0 / 9007199254740992.0)) * 2 - 1;
            double accuracy = MathX.Clamp(0.55 + forecasterSkill0To100 / 300.0 - horizonS / 7200.0, 0.3, 0.92);
            double p = truth ? accuracy : 1 - accuracy;
            if (truth && firstRain > horizonS * 0.6) p -= 0.1;
            p += noise * (1 - accuracy) * 0.6;
            if (RainIntensity > 0.05) p = Math.Max(p, 0.85);
            return MathX.Clamp(Math.Round(p * 20) / 20.0, 0.05, 0.95);
        }

        public string Describe()
        {
            return Condition + ", air " + AirTemp.ToString("0") + "°C, track " + TrackTemp.ToString("0") + "°C, wetness " + (TrackWetness * 100).ToString("0") + "%";
        }
    }
}
