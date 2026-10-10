using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;

namespace MotoManager.Core.Season
{
    public class NewGameOptions
    {
        public ulong Seed = 2026;
        public int StartSeason = 2026;
        /// <summary>Team the player manages (null = spectator / headless).</summary>
        public string PlayerTeamId;
        public int GrandPrixPoolSize = 70;
        public int SuperbikePoolSize = 45;
    }

    /// <summary>
    /// Turns the static snapshot (data folders) into a live <see cref="WorldState"/>. Everything the data provides is
    /// used as is; whatever is missing (staff, sponsors, empty seats, even whole teams) is completed procedurally so
    /// that every category always has a full grid.
    /// </summary>
    public static class WorldFactory
    {
        public static WorldState Create(GameDatabase db, NewGameOptions options)
        {
            var world = new WorldState
            {
                Season = options.StartSeason,
                CurrentDate = new DateTime(options.StartSeason, 1, 1),
                MasterSeed = options.Seed,
                Rng = new DeterministicRandom(options.Seed)
            };
            var rng = world.Rng.Fork("world-factory");

            foreach (var m in db.Manufacturers.Values) world.Manufacturers[m.Id] = JsonSettings.DeepClone(m);
            foreach (var b in db.Motorcycles.Values) world.Bikes[b.Id] = JsonSettings.DeepClone(b);
            foreach (var t in db.Teams.Values)
            {
                var team = JsonSettings.DeepClone(t);
                team.RiderIds.Clear();
                world.Teams[team.Id] = team;
            }
            foreach (var r in db.Riders.Values) world.Riders[r.Id] = JsonSettings.DeepClone(r);
            foreach (var s in db.Staff.Values)
            {
                world.Staff[s.Id] = new StaffMember
                {
                    Id = s.Id, FirstName = s.FirstName, LastName = s.LastName, Role = s.Role, Nationality = s.Nationality,
                    BirthYear = s.BirthYear, Skill = s.Skill, Salary = s.Salary, TeamId = s.TeamId
                };
                var team = world.GetTeam(s.TeamId);
                if (team != null && !team.StaffIds.Contains(s.Id)) team.StaffIds.Add(s.Id);
            }

            ApplyContracts(world, db, options.StartSeason);
            foreach (var cat in db.CategoriesOrdered())
            {
                if (!cat.Simulated) continue;
                EnsureManufacturerPrograms(world, db, cat, rng);
                EnsureTeams(world, db, cat, rng);
            }
            AssignTeamBikes(world, db);
            TransferMarket.RebuildRosters(world);
            FillSeats(world, db, rng);
            SeedTalentPools(world, db, rng, options);

            foreach (var team in Sorted(world.Teams.Values))
            {
                var cat = db.GetCategory(team.CategoryId);
                if (team.Facilities.Count == 0)
                {
                    int lvl = (int)MathX.Clamp(Math.Round(team.Reputation / 25.0), 1, 4);
                    foreach (FacilityType f in Enum.GetValues(typeof(FacilityType))) team.Facilities[f] = lvl;
                }
                if (cat != null && cat.IsCup) continue;
                StaffService.EnsureStaff(world, db, team, rng, cat != null ? cat.Prestige / 100.0 : 0.5);
                SponsorService.EnsureSponsors(world, db, team, cat, rng);
            }

            SetupCalendars(world, db, options.StartSeason);
            SetupOrganisations(world, db);
            foreach (var cat in db.Categories.Values)
                if (cat.Simulated) world.Championships[cat.Id] = new ChampionshipState { CategoryId = cat.Id, Season = options.StartSeason };

            TeamAI.EnsureAll(world, db);
            if (options.PlayerTeamId != null) SetPlayerTeam(world, options.PlayerTeamId);
            InitialKnowledge(world);
            world.AddNews("Season " + options.StartSeason + " begins", "The " + options.StartSeason + " grids are set.", null, true);
            return world;
        }

        public static void SetPlayerTeam(WorldState world, string teamId)
        {
            var team = world.GetTeam(teamId);
            if (team == null) throw new ArgumentException("Unknown team " + teamId);
            foreach (var t in world.Teams.Values) t.IsPlayer = false;
            team.IsPlayer = true;
            world.PlayerTeamId = teamId;
            foreach (var o in world.Organisations.Values) o.DetailedLedger = false;
            var org = world.OrganisationOf(team);
            if (org != null) org.DetailedLedger = true;
        }

        private static void ApplyContracts(WorldState world, GameDatabase db, int season)
        {
            foreach (var c in db.Contracts)
            {
                var r = world.GetRider(c.RiderId);
                if (r == null) continue;
                r.TeamId = c.TeamId;
                r.Contract = new Contract
                {
                    TeamId = c.TeamId, ManufacturerId = c.ManufacturerId, StartSeason = c.StartSeason, EndSeason = c.EndSeason,
                    SalaryPerSeason = c.SalaryPerSeason, FactoryBacked = c.FactoryBacked
                };
                // Real bonus terms are not public: standard estimated bonuses.
                TransferMarket.StandardBonuses(r.Contract);
            }
            foreach (var r in Sorted(world.Riders.Values))
            {
                var team = world.GetTeam(r.TeamId);
                if (team == null)
                {
                    r.TeamId = null;
                    continue;
                }
                r.CategoryId = team.CategoryId;
                if (r.Contract == null)
                {
                    var rules = db.GetRules(team.CategoryId, season);
                    r.Contract = new Contract
                    {
                        TeamId = team.Id, ManufacturerId = team.ManufacturerId, StartSeason = season, EndSeason = season + (int)(DeterministicRandom.Hash(r.Id) % 2),
                        SalaryPerSeason = TransferMarket.SalaryDemand(r, rules), FactoryBacked = team.Status == TeamStatus.Factory
                    };
                    TransferMarket.StandardBonuses(r.Contract);
                }
            }
        }

        /// <summary>Every category needs at least one manufacturer bike to race.</summary>
        private static void EnsureManufacturerPrograms(WorldState world, GameDatabase db, ChampionshipCategory cat, DeterministicRandom rng)
        {
            // Programs declared implicitly by motorcycles in data.
            foreach (var b in Sorted(world.Bikes.Values))
            {
                var m = world.GetManufacturer(b.ManufacturerId);
                if (m != null && b.CategoryId == cat.Id && !m.Programs.ContainsKey(cat.Id)) m.Programs[cat.Id] = b.Id;
            }
            foreach (var m in world.Manufacturers.Values)
                if (m.Programs.ContainsKey(cat.Id)) return;

            var rules = db.GetRules(cat.Id, world.Season);
            var bikeClass = db.GetBikeClass(rules.BikeClassId);
            int count = cat.IsCup ? 1 : 4;
            for (int i = 0; i < count; i++)
            {
                var m = new Manufacturer
                {
                    Id = "gen_" + cat.Id + "_" + i,
                    Name = (cat.IsCup ? cat.ShortName + " Cup" : cat.ShortName + " Constructor " + (char)('A' + i)),
                    Country = "IT",
                    Reputation = 40 + rng.Range(0, 30),
                    EngineeringStrength = rng.Range(45, 75),
                    DevelopmentBudget = 5e6 * (cat.Prestige / 50.0)
                };
                var bike = new Motorcycle { Id = m.Id + "_bike", ManufacturerId = m.Id, CategoryId = cat.Id, BikeClassId = bikeClass.Id, Model = m.Name, Year = world.Season };
                foreach (var c in bikeClass.Components) bike.Components[c] = cat.IsCup ? 60 : rng.Range(55, 68);
                if (bikeClass.ProductionBaseShare > 0)
                    foreach (PerfAttr a in Enum.GetValues(typeof(PerfAttr))) bike.ProductionBase[a] = rng.Range(50, 70);
                m.Programs[cat.Id] = bike.Id;
                world.Manufacturers[m.Id] = m;
                world.Bikes[bike.Id] = bike;
            }
        }

        private static void EnsureTeams(WorldState world, GameDatabase db, ChampionshipCategory cat, DeterministicRandom rng)
        {
            var rules = db.GetRules(cat.Id, world.Season);
            int existingSeats = 0;
            foreach (var t in world.Teams.Values)
                if (t.CategoryId == cat.Id) existingSeats += TransferMarket.Capacity(cat, rules, t);
            int target = cat.IsCup ? rules.MaxEntries : rules.GridSize;
            if (cat.IsCup && existingSeats > 0) return;
            var makers = new List<Manufacturer>();
            foreach (var m in Sorted(world.Manufacturers.Values))
                if (m.Programs.ContainsKey(cat.Id)) makers.Add(m);
            int index = 0;
            while (existingSeats < target)
            {
                var m = makers[index % makers.Count];
                var team = new Team
                {
                    Id = "gen_" + cat.Id + "_team" + index.ToString("D2"),
                    Name = cat.IsCup ? cat.Name : m.Name + " Racing " + (index + 1),
                    ShortName = cat.IsCup ? cat.ShortName : m.Name,
                    CategoryId = cat.Id,
                    ManufacturerId = m.Id,
                    Status = index < makers.Count ? TeamStatus.Factory : TeamStatus.Satellite,
                    BikeId = m.Programs[cat.Id],
                    Country = m.Country,
                    Reputation = MathX.Clamp(cat.Prestige * 0.5 + rng.Range(0, 30), 10, 90),
                    SeasonBudget = rules.Economy.TypicalRiderSalary * 5 + rules.Economy.TravelCostPerRound * 22,
                    DataConfidence = "generated"
                };
                team.Colors.Primary = Palette[index % Palette.Length];
                world.Teams[team.Id] = team;
                existingSeats += TransferMarket.Capacity(cat, rules, team);
                index++;
            }
        }

        private static readonly string[] Palette = { "#D7263D", "#1B998B", "#2E86AB", "#F46036", "#C5D86D", "#6A4C93", "#FFC914", "#3D5A80", "#E0777D", "#22223B", "#9BC53D", "#5BC0EB", "#FA7921", "#2D3047" };

        /// <summary>Each team races its own instance of its manufacturer's bike (so development can diverge).</summary>
        private static void AssignTeamBikes(WorldState world, GameDatabase db)
        {
            foreach (var team in Sorted(world.Teams.Values))
            {
                var baseBike = world.GetBike(team.BikeId);
                if (baseBike == null)
                {
                    var m = world.GetManufacturer(team.ManufacturerId);
                    string id;
                    if (m != null && m.Programs.TryGetValue(team.CategoryId, out id)) baseBike = world.GetBike(id);
                }
                if (baseBike == null) continue;
                var bike = baseBike.Clone("bike_" + team.Id);
                if (team.Status != TeamStatus.Factory)
                {
                    double offset = team.Status == TeamStatus.Satellite ? 1.2 : 2.0;
                    foreach (var c in new List<string>(bike.Components.Keys)) bike.Components[c] = Math.Max(1, bike.Components[c] - offset);
                }
                world.Bikes[bike.Id] = bike;
                team.BikeId = bike.Id;
                if (team.ManufacturerId == null) team.ManufacturerId = baseBike.ManufacturerId;
            }
        }

        private static void FillSeats(WorldState world, GameDatabase db, DeterministicRandom rng)
        {
            var levels = TransferMarket.CategoryLevels(world, db);
            foreach (var team in Sorted(world.Teams.Values))
            {
                var cat = db.GetCategory(team.CategoryId);
                if (cat == null || !cat.Simulated) continue;
                var rules = db.GetRules(cat.Id, world.Season);
                int capacity = TransferMarket.Capacity(cat, rules, team);
                double level;
                levels.TryGetValue(cat.Id, out level);
                while (team.RiderIds.Count < capacity)
                {
                    var r = RiderGenerator.GenerateForCategory(world, db, rng, cat, level);
                    // Spread the generated grid over the usual age range of the category, not only rookies.
                    r.TeamId = team.Id;
                    r.Contract = new Contract
                    {
                        TeamId = team.Id, ManufacturerId = team.ManufacturerId, StartSeason = world.Season, EndSeason = world.Season + rng.NextInt(0, 2),
                        SalaryPerSeason = TransferMarket.SalaryDemand(r, rules), FactoryBacked = team.Status == TeamStatus.Factory
                    };
                    TransferMarket.StandardBonuses(r.Contract);
                    world.Riders[r.Id] = r;
                    team.RiderIds.Add(r.Id);
                }
            }
        }

        private static void SeedTalentPools(WorldState world, GameDatabase db, DeterministicRandom rng, NewGameOptions options)
        {
            foreach (var cat in db.CategoriesOrdered())
            {
                if (cat.Simulated) continue;
                int n = cat.Pyramid == Pyramid.GrandPrix ? options.GrandPrixPoolSize : options.SuperbikePoolSize;
                for (int i = 0; i < n; i++)
                {
                    var r = RiderGenerator.Generate(world, db, rng, cat.Pyramid, cat.Id, cat.MinAge, cat.MaxEntryAge, 0.35);
                    world.Riders[r.Id] = r;
                }
            }
        }

        /// <summary>
        /// Organisations from the data (known multi-team structures and manufacturers' works divisions); every other
        /// team gets its own. Initial cash is the sum of the season budgets of the teams it owns.
        /// </summary>
        public static void SetupOrganisations(WorldState world, GameDatabase db)
        {
            foreach (var def in Sorted(db.Organisations.Values))
            {
                var org = JsonSettings.DeepClone(def);
                org.TeamIds.RemoveAll(id => world.GetTeam(id) == null);
                if (org.TeamIds.Count == 0) continue;
                world.Organisations[org.Id] = org;
                foreach (var id in org.TeamIds) world.GetTeam(id).OrganisationId = org.Id;
            }
            foreach (var team in Sorted(world.Teams.Values))
            {
                if (world.GetOrganisation(team.OrganisationId) != null) continue;
                var cat = db.GetCategory(team.CategoryId);
                var org = new Organisation
                {
                    Id = "org_" + team.Id,
                    Name = team.Name,
                    Owner = cat != null && cat.IsCup ? OwnerKind.Organiser : OwnerKind.Private,
                    TeamIds = new List<string> { team.Id },
                    OwnerBacking = MathX.Clamp(30 + team.Reputation * 0.5, 20, 90),
                    DataConfidence = "generated"
                };
                world.Organisations[org.Id] = org;
                team.OrganisationId = org.Id;
            }
            foreach (var org in Sorted(world.Organisations.Values))
            {
                double cash = 0;
                foreach (var id in org.TeamIds) cash += world.GetTeam(id).SeasonBudget;
                org.Cash = org.SeasonOpeningCash = cash;
            }
        }

        private static void SetupCalendars(WorldState world, GameDatabase db, int season)
        {
            foreach (var champ in db.Championships.Values)
            {
                CalendarDefinition best = null;
                foreach (var c in db.Calendars.Values)
                    if (c.Group == champ.Id && c.Season <= season && (best == null || c.Season > best.Season)) best = c;
                if (best == null) continue;
                var cal = JsonSettings.DeepClone(best);
                if (cal.Season != season)
                {
                    foreach (var r in cal.Rounds) r.Date = r.Date.AddYears(season - cal.Season);
                    cal.Season = season;
                }
                cal.Rounds.Sort((a, b) => a.Date.CompareTo(b.Date));
                world.Calendars[champ.Id] = cal;
                world.NextRound[champ.Id] = 0;
            }
        }

        private static void InitialKnowledge(WorldState world)
        {
            var player = world.PlayerTeam;
            if (player == null) return;
            foreach (var r in world.Riders.Values)
            {
                double k = r.TeamId == player.Id ? 1.0 : (r.CategoryId == player.CategoryId ? 0.35 : 0.1) + r.Reputation / 400.0;
                world.SetKnowledge(player.Id, r.Id, k);
            }
        }

        private static List<T> Sorted<T>(IEnumerable<T> items) where T : class
        {
            var list = new List<T>(items);
            list.Sort((a, b) => string.CompareOrdinal(IdOf(a), IdOf(b)));
            return list;
        }

        private static string IdOf(object o)
        {
            var r = o as Rider; if (r != null) return r.Id;
            var t = o as Team; if (t != null) return t.Id;
            var m = o as Manufacturer; if (m != null) return m.Id;
            var b = o as Motorcycle; if (b != null) return b.Id;
            var g = o as Organisation; if (g != null) return g.Id;
            return o.ToString();
        }
    }
}
