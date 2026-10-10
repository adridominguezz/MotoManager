using System;
using System.Collections.Generic;
using System.IO;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Season;
using MotoManager.Core.World;

namespace MotoManager.Tests
{
    public static class TestData
    {
        private static GameDatabase _db;

        public static string DataRoot
        {
            get
            {
                var dir = AppContext.BaseDirectory;
                while (dir != null)
                {
                    var p = Path.Combine(dir, "Assets", "MotoManager", "Resources", "Data");
                    if (Directory.Exists(p)) return p;
                    dir = Path.GetDirectoryName(dir);
                }
                throw new DirectoryNotFoundException("data root");
            }
        }

        public static GameDatabase Db
        {
            get { return _db ?? (_db = DataLoader.Load(new FileDataSource(DataRoot))); }
        }

        /// <summary>
        /// A complete but invented world built only from rules/categories (real teams and riders arrive in phase 2).
        /// Season 2026 is set as finished, with random standings.
        /// </summary>
        public static WorldState SyntheticWorld(ulong seed)
        {
            var db = Db;
            var world = new WorldState { Season = 2026, MasterSeed = seed, Rng = new DeterministicRandom(seed), CurrentDate = new DateTime(2026, 11, 20) };
            var rng = new DeterministicRandom(seed ^ 0xABCDUL);
            string[] makers = { "alpha", "bravo", "charlie", "delta", "echo" };
            foreach (var m in makers)
                world.Manufacturers[m] = new Manufacturer { Id = m, Name = m.ToUpperInvariant(), EngineeringStrength = rng.Range(45, 85), DevelopmentBudget = rng.Range(1e7, 4e7) };

            foreach (var cat in db.CategoriesOrdered())
            {
                if (!cat.Simulated)
                {
                    for (int i = 0; i < 40; i++)
                    {
                        var jr = RiderGenerator.Generate(world, db, rng, cat.Pyramid, cat.Id, cat.MinAge, cat.MaxEntryAge, 0.3);
                        world.Riders[jr.Id] = jr;
                    }
                    continue;
                }
                var rules = db.GetRules(cat.Id, 2026);
                var bikeClass = db.BikeClasses[rules.BikeClassId];
                foreach (var m in makers)
                {
                    var bike = new Motorcycle { Id = m + "_" + cat.Id, ManufacturerId = m, CategoryId = cat.Id, BikeClassId = bikeClass.Id, Model = m + " " + cat.ShortName };
                    foreach (var c in bikeClass.Components) bike.Components[c] = rng.Range(55, 70);
                    world.Bikes[bike.Id] = bike;
                    world.Manufacturers[m].Programs[cat.Id] = bike.Id;
                }
                int teams = cat.IsCup ? 1 : Math.Min(11, rules.MaxEntries / rules.RidersPerTeam);
                int capacity = TransferMarket.Capacity(cat, rules);
                for (int t = 0; t < teams; t++)
                {
                    string m = makers[t % makers.Length];
                    var team = new Team
                    {
                        Id = cat.Id + "_team" + t.ToString("D2"),
                        Name = cat.ShortName + " Team " + t,
                        CategoryId = cat.Id,
                        ManufacturerId = m,
                        Status = t < makers.Length ? TeamStatus.Factory : TeamStatus.Satellite,
                        Reputation = rng.Range(30, 90),
                        SeasonBudget = rules.Economy.TypicalRiderSalary * 6 + 2e6
                    };
                    var bike = world.Bikes[m + "_" + cat.Id].Clone("bike_" + team.Id);
                    world.Bikes[bike.Id] = bike;
                    team.BikeId = bike.Id;
                    world.Teams[team.Id] = team;
                    for (int s = 0; s < capacity; s++)
                    {
                        double minAge, maxAge;
                        switch (cat.Tier)
                        {
                            case 1: minAge = 21; maxAge = 35; break;
                            case 2: minAge = 18; maxAge = 26; break;
                            case 3: minAge = 16; maxAge = 21; break;
                            default: minAge = 13; maxAge = 16.5; break;
                        }
                        var r = RiderGenerator.Generate(world, db, rng, cat.Pyramid, cat.Id, minAge, Math.Min(maxAge, cat.MaxAge - 0.5), cat.Prestige / 100.0);
                        double target = 40 + cat.Prestige * 0.42 + rng.Range(-6, 6);
                        double gap = target - r.Attributes.Overall;
                        foreach (var n in RiderAttributes.Names)
                            if (n != "Aggression" && n != "Experience") r.Attributes.Set(n, r.Attributes.Get(n) + gap);
                        r.Potential = Math.Max(r.Potential, r.Attributes.Overall + rng.Range(0, 8));
                        r.TeamId = team.Id;
                        r.Reputation = MathX.Clamp(cat.Prestige * 0.6 + rng.Range(-15, 15), 5, 95);
                        r.Contract = new Contract { TeamId = team.Id, ManufacturerId = m, StartSeason = 2025, EndSeason = rng.Chance(0.5) ? 2026 : 2027, SalaryPerSeason = rules.Economy.TypicalRiderSalary };
                        world.Riders[r.Id] = r;
                        team.RiderIds.Add(r.Id);
                    }
                }
            }
            WorldFactory.SetupOrganisations(world, db);
            RandomStandings(world, db, rng);
            return world;
        }

        public static void RandomStandings(WorldState world, GameDatabase db, DeterministicRandom rng)
        {
            foreach (var cat in db.Categories.Values)
            {
                if (!cat.Simulated) continue;
                var champ = new ChampionshipState { CategoryId = cat.Id, Season = world.Season };
                foreach (var r in world.ActiveRidersInCategory(cat.Id))
                {
                    // Better riders score more, with noise.
                    double pts = Math.Max(0, (r.Attributes.Overall - 40) * 6 + rng.Gaussian(0, 40));
                    champ.Riders[r.Id] = new StandingEntry { Id = r.Id, Points = Math.Round(pts) };
                    StandingEntry te;
                    if (!champ.Teams.TryGetValue(r.TeamId, out te)) champ.Teams[r.TeamId] = te = new StandingEntry { Id = r.TeamId };
                    te.Points += Math.Round(pts);
                    var rec = r.GetOrCreateRecord(world.Season, cat.Id, r.TeamId);
                    rec.Points = Math.Round(pts);
                    rec.Starts = 20;
                }
                world.Championships[cat.Id] = champ;
            }
        }
    }
}
