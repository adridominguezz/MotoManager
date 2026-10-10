using System;
using System.Collections.Generic;

namespace MotoManager.Core.Common
{
    /// <summary>
    /// Platform independent PRNG (xorshift128+ seeded through SplitMix64).
    /// Never use System.Random or string.GetHashCode() in simulation code: both are
    /// implementation dependent and would break replays and determinism tests.
    /// </summary>
    [Serializable]
    public sealed class DeterministicRandom
    {
        public ulong S0;
        public ulong S1;

        public DeterministicRandom() : this(0x5EEDUL) { }

        public DeterministicRandom(ulong seed)
        {
            ulong z = seed;
            S0 = SplitMix(ref z);
            S1 = SplitMix(ref z);
            if (S0 == 0 && S1 == 0) S1 = 1;
        }

        public static DeterministicRandom FromSeed(ulong seed, string salt)
        {
            return new DeterministicRandom(seed ^ Hash(salt));
        }

        private static ulong SplitMix(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public ulong NextULong()
        {
            ulong a = S0;
            ulong b = S1;
            S0 = b;
            a ^= a << 23;
            S1 = a ^ b ^ (a >> 17) ^ (b >> 26);
            return S1 + b;
        }

        /// <summary>Uniform in [0,1).</summary>
        public double NextDouble()
        {
            return (NextULong() >> 11) * (1.0 / 9007199254740992.0);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong range = (ulong)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextULong() % range);
        }

        public double Range(double min, double max)
        {
            return min + (max - min) * NextDouble();
        }

        public bool Chance(double probability)
        {
            if (probability <= 0) return false;
            if (probability >= 1) return true;
            return NextDouble() < probability;
        }

        public double Gaussian(double mean = 0, double stdDev = 1)
        {
            double u1 = 1.0 - NextDouble();
            double u2 = NextDouble();
            double n = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return mean + stdDev * n;
        }

        public T Pick<T>(IReadOnlyList<T> items)
        {
            return items[NextInt(0, items.Count)];
        }

        public T PickWeighted<T>(IReadOnlyList<T> items, Func<T, double> weight)
        {
            double total = 0;
            for (int i = 0; i < items.Count; i++) total += Math.Max(0, weight(items[i]));
            if (total <= 0) return items[NextInt(0, items.Count)];
            double r = NextDouble() * total;
            for (int i = 0; i < items.Count; i++)
            {
                r -= Math.Max(0, weight(items[i]));
                if (r <= 0) return items[i];
            }
            return items[items.Count - 1];
        }

        /// <summary>Creates an independent stream. Consumes one value from this stream.</summary>
        public DeterministicRandom Fork(string salt)
        {
            return new DeterministicRandom(NextULong() ^ Hash(salt));
        }

        /// <summary>Stable 64-bit FNV-1a hash (string.GetHashCode is randomised per process in .NET).</summary>
        public static ulong Hash(string text)
        {
            ulong h = 14695981039346656037UL;
            if (text == null) return h;
            for (int i = 0; i < text.Length; i++)
            {
                h ^= text[i];
                h *= 1099511628211UL;
            }
            return h;
        }

        public static ulong Combine(ulong a, ulong b)
        {
            ulong z = a ^ (b + 0x9E3779B97F4A7C15UL + (a << 6) + (a >> 2));
            return SplitMix(ref z);
        }
    }
}
