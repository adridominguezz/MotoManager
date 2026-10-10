using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Career
{
    /// <summary>
    /// Creates new riders so the universe never runs dry. Nationalities follow per-pyramid talent weights
    /// (Spain and Italy feed the Grand Prix ladder, the UK and Australia lean towards Superbikes...),
    /// potential follows a skewed distribution (few stars, many journeymen), and current ability depends on age.
    /// </summary>
    public static class RiderGenerator
    {
        private static readonly string[] Attr = RiderAttributes.Names;

        /// <summary>Fraction of potential reached at a given age (before individual variation).</summary>
        public static double Maturity(double age)
        {
            if (age <= 13) return 0.5;
            if (age >= 27) return 1.0;
            return 0.5 + 0.5 * MathX.SmoothStep(13, 27, age);
        }

        public static double RollPotential(DeterministicRandom rng, double quality)
        {
            // quality 0..1 shifts the distribution (academies, scouting events). Skewed: stars are rare.
            double u = rng.NextDouble();
            double p = 42 + 40 * Math.Pow(u, 1.8) + quality * 12 + rng.Gaussian(0, 4);
            return MathX.Clamp(p, 30, 99);
        }

        public static Rider Generate(WorldState world, GameDatabase db, DeterministicRandom rng, Pyramid pyramid, string categoryId,
            double minAge, double maxAge, double quality)
        {
            var pool = PickPool(db, rng, pyramid);
            var rider = new Rider
            {
                Id = world.NewId("gen"),
                Generated = true,
                CategoryId = categoryId,
                Nationality = pool != null ? pool.Nationality : "ES",
                DataConfidence = "generated"
            };
            rider.FirstName = pool != null && pool.FirstNames.Count > 0 ? rng.Pick(pool.FirstNames) : "Alex";
            rider.LastName = pool != null && pool.LastNames.Count > 0 ? rng.Pick(pool.LastNames) : "Rider";
            double age = rng.Range(minAge, maxAge);
            var reference = world.CurrentDate == default(DateTime) ? new DateTime(world.Season, 1, 1) : world.CurrentDate;
            rider.DateOfBirth = reference.AddDays(-age * 365.25).Date;

            rider.Potential = RollPotential(rng, quality);
            rider.PeakAge = MathX.Clamp(rng.Gaussian(27.5, 2.2), 23, 33);
            double current = rider.Potential * Maturity(age) * rng.Range(0.94, 1.03);
            FillAttributes(rider.Attributes, current, age, rng);
            rider.Reputation = MathX.Clamp(current * 0.35 - 5 + rng.Range(0, 8), 1, 40);
            rider.Morale = rng.Range(55, 80);
            rider.Personality = new Personality
            {
                Ambition = MathX.Clamp(rng.Gaussian(60, 18), 5, 100),
                Loyalty = MathX.Clamp(rng.Gaussian(50, 18), 5, 100),
                Temperament = MathX.Clamp(rng.Gaussian(50, 20), 5, 100),
                Motivation = MathX.Clamp(rng.Gaussian(78, 12), 20, 100),
                GrandPrixPreference = MathX.Clamp(pyramid == Pyramid.GrandPrix ? rng.Gaussian(72, 15) : rng.Gaussian(40, 18), 0, 100)
            };
            rider.Styles = DeriveStyles(rider.Attributes, rng);
            rider.Number = FreeNumber(world, categoryId, rng);
            // Some riders arrive with backers who pay for a seat (common in the smaller categories).
            if (rng.Chance(0.3)) rider.PersonalBacking = Math.Round(250000 * Math.Exp(rng.Gaussian(0, 0.7)) / 10000) * 10000;
            return rider;
        }

        /// <summary>
        /// A rider arriving from a series we do not simulate: age fitting the tier and ability close to the grid level
        /// (<paramref name="gridLevel"/> = average overall of the category, 0 = estimate from prestige). Not added to the world.
        /// </summary>
        public static Rider GenerateForCategory(WorldState world, GameDatabase db, DeterministicRandom rng, ChampionshipCategory cat, double gridLevel)
        {
            double minAge = Math.Max(cat.MinAge, 13), maxAge;
            switch (cat.Tier)
            {
                case 1: minAge = Math.Max(minAge, 20); maxAge = 26; break;
                case 2: minAge = Math.Max(minAge, cat.Pyramid == Pyramid.GrandPrix ? 18 : 17); maxAge = 23; break;
                case 3: minAge = Math.Max(minAge, 16); maxAge = 19; break;
                default: maxAge = Math.Min(cat.MaxEntryAge, minAge + 3); break;
            }
            maxAge = Math.Min(maxAge, Math.Min(cat.MaxEntryAge, cat.MaxAge - 0.5));
            if (maxAge < minAge) maxAge = minAge + 0.5;
            var r = Generate(world, db, rng, cat.Pyramid, cat.Id, minAge, maxAge, cat.Prestige / 100.0);
            if (gridLevel <= 0) gridLevel = 40 + cat.Prestige * 0.42;
            double target = gridLevel - rng.Range(3, 8);
            double gap = target - r.Attributes.Overall;
            // Unknown newcomers start below the established level of the grid (never above it).
            if (gap > 0 || r.Attributes.Overall > gridLevel - 1)
            {
                foreach (var name in Attr)
                    if (name != "Aggression" && name != "Experience") r.Attributes.Set(name, r.Attributes.Get(name) + gap);
                r.Potential = Math.Max(r.Potential, r.Attributes.Overall + rng.Range(0, 10));
            }
            r.Reputation = MathX.Clamp(cat.Prestige * 0.4 + rng.Range(-5, 10), 1, 60);
            return r;
        }

        public static void FillAttributes(RiderAttributes a, double level, double age, DeterministicRandom rng)
        {
            foreach (var name in Attr)
            {
                double v = level + rng.Gaussian(0, 7);
                a.Set(name, v);
            }
            a.Experience = MathX.Clamp((age - 12) * 4.5 + rng.Gaussian(0, 5), 1, 100);
            a.Aggression = MathX.Clamp(rng.Gaussian(55, 15), 10, 95);
            a.Mentality = MathX.Clamp(level * 0.6 + age + rng.Gaussian(0, 8), 5, 100);
        }

        public static List<RidingStyle> DeriveStyles(RiderAttributes a, DeterministicRandom rng)
        {
            var candidates = new List<KeyValuePair<RidingStyle, double>>
            {
                new KeyValuePair<RidingStyle, double>(RidingStyle.LateBraker, a.Braking - a.Cornering),
                new KeyValuePair<RidingStyle, double>(RidingStyle.CornerSpecialist, a.Cornering - a.Braking),
                new KeyValuePair<RidingStyle, double>(RidingStyle.TireSaver, a.TireManagement - a.Aggression),
                new KeyValuePair<RidingStyle, double>(RidingStyle.AggressiveRider, a.Aggression - 60),
                new KeyValuePair<RidingStyle, double>(RidingStyle.SmoothRider, a.ThrottleControl - a.Aggression),
                new KeyValuePair<RidingStyle, double>(RidingStyle.WetSpecialist, a.WetSkill - a.RaceSkill),
                new KeyValuePair<RidingStyle, double>(RidingStyle.QualifyingSpecialist, a.QualifyingSkill - a.RaceSkill),
                new KeyValuePair<RidingStyle, double>(RidingStyle.RaceSpecialist, a.RaceSkill - a.QualifyingSkill),
                new KeyValuePair<RidingStyle, double>(RidingStyle.DefensiveRider, a.Defending - a.Overtaking),
                new KeyValuePair<RidingStyle, double>(RidingStyle.StraightLineSpecialist, a.Braking + a.Starts - 2 * a.Cornering)
            };
            candidates.Sort((x, y) => y.Value.CompareTo(x.Value));
            var styles = new List<RidingStyle>();
            int count = rng.Chance(0.4) ? 2 : 1;
            for (int i = 0; i < candidates.Count && styles.Count < count; i++)
                if (candidates[i].Value > 4) styles.Add(candidates[i].Key);
            return styles;
        }

        private static NamePool PickPool(GameDatabase db, DeterministicRandom rng, Pyramid pyramid)
        {
            var pools = new List<NamePool>(db.NamePools.Values);
            if (pools.Count == 0) return null;
            pools.Sort((a, b) => string.CompareOrdinal(a.Nationality, b.Nationality));
            return rng.PickWeighted(pools, p => pyramid == Pyramid.GrandPrix ? p.WeightGrandPrix : p.WeightSuperbike);
        }

        public static int FreeNumber(WorldState world, string categoryId, DeterministicRandom rng)
        {
            var used = new HashSet<int>();
            foreach (var r in world.Riders.Values)
                if (!r.IsRetired && r.CategoryId == categoryId) used.Add(r.Number);
            for (int i = 0; i < 200; i++)
            {
                int n = rng.NextInt(2, 100);
                if (!used.Contains(n)) return n;
            }
            return rng.NextInt(100, 999);
        }
    }
}
