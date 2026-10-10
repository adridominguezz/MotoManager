using System.Linq;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using Xunit;

namespace MotoManager.Tests
{
    public class StaffFacilityTests
    {
        private static (GameController, Team) Career(string teamId, ulong seed)
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = seed, PlayerTeamId = teamId });
            return (game, game.World.PlayerTeam);
        }

        [Fact]
        public void TeamsHaveTheStructureOfTheirCategoryAndRealManagersRunTheirTeams()
        {
            var db = TestData.Db;
            var world = WorldFactory.Create(db, new NewGameOptions { Seed = 101 });
            Assert.DoesNotContain(world.Staff.Values, s => s.Role == StaffRole.TeamManager);
            Assert.Equal("Tardozzi", world.GetManager(world.Teams["ducati_lenovo"].ManagerId).LastName);
            var ducati = world.Teams["ducati_lenovo"];
            var roles = ducati.StaffIds.Select(id => world.Staff[id]).ToList();
            Assert.Contains(roles, s => s.Role == StaffRole.TechnicalDirector && s.LastName == "Dall'Igna" || s.Role == StaffRole.TechnicalDirector);
            foreach (var t in world.Teams.Values.Where(t => !db.GetCategory(t.CategoryId).IsCup))
            {
                var staff = t.StaffIds.Select(id => world.Staff[id]).ToList();
                foreach (var riderId in t.RiderIds)
                {
                    Assert.Contains(staff, s => s.Role == StaffRole.RaceEngineer && s.AssignedRiderId == riderId);
                    Assert.Contains(staff, s => s.Role == StaffRole.Mechanic && s.AssignedRiderId == riderId);
                }
                Assert.Contains(staff, s => s.Role == StaffRole.ChiefEngineer);
                Assert.All(staff, s => Assert.True(s.ContractEndSeason >= world.Season));
            }
            Assert.DoesNotContain(world.Teams["aspar_moto3"].StaffIds.Select(id => world.Staff[id]), s => s.Role == StaffRole.TechnicalDirector);
        }

        [Fact]
        public void PeopleAndFacilitiesHaveConcreteEffects()
        {
            var (game, team) = Career("aspar_moto2", 102);
            var w = game.World;
            var rider = team.RiderIds[0];
            var engineer = team.StaffIds.Select(id => w.Staff[id]).First(s => s.Role == StaffRole.RaceEngineer && s.AssignedRiderId == rider);
            engineer.Skill = 40;
            double weak = TeamCapabilities.SetupSupport(w, team, rider);
            engineer.Skill = 90;
            Assert.True(TeamCapabilities.SetupSupport(w, team, rider) > weak);

            team.Facilities[FacilityType.Factory] = 1;
            team.Facilities[FacilityType.Dyno] = 1;
            int capacity = TeamCapabilities.ProjectCapacity(team);
            double engine = TeamCapabilities.DevelopmentSpeed(w, team, "Engine");
            double chassis = TeamCapabilities.DevelopmentSpeed(w, team, "Chassis");
            team.Facilities[FacilityType.Dyno] = 4;
            Assert.True(TeamCapabilities.DevelopmentSpeed(w, team, "Engine") > engine);
            Assert.Equal(chassis, TeamCapabilities.DevelopmentSpeed(w, team, "Chassis"), 6);
            team.Facilities[FacilityType.Factory] = 5;
            Assert.True(TeamCapabilities.ProjectCapacity(team) > capacity);
            Assert.True(TeamCapabilities.DevelopmentSpeed(w, team, "Chassis") > chassis);

            team.Facilities[FacilityType.Hospitality] = 1;
            double f1 = TeamCapabilities.SponsorValueFactor(team);
            team.Facilities[FacilityType.Hospitality] = 5;
            Assert.True(TeamCapabilities.SponsorValueFactor(team) > f1);

            // Facilities cost more to keep the bigger they are.
            var before = FinanceService.SeasonForecast(w, game.Db, team, 22).FacilityUpkeep;
            team.Facilities[FacilityType.WindTunnel] = 5;
            Assert.True(FinanceService.SeasonForecast(w, game.Db, team, 22).FacilityUpkeep > before);
        }

        [Fact]
        public void StaffContractsExpireUnlessRenewedAndCostToEndEarly()
        {
            var (game, team) = Career("aspar_moto2", 103);
            var w = game.World;
            var db = game.Db;
            var staff = team.StaffIds.Select(id => w.Staff[id]).ToList();
            var keep = staff[0];
            var go = staff[1];
            keep.ContractEndSeason = w.Season;
            go.ContractEndSeason = w.Season;
            StaffMarket.Extend(w, db, team, keep, 2);
            Assert.Equal(w.Season + 2, keep.ContractEndSeason);

            var ai = w.Teams["ajo_moto2"];
            foreach (var id in ai.StaffIds) w.Staff[id].ContractEndSeason = w.Season;
            int aiCount = ai.StaffIds.Count;

            StaffService.SeasonEnd(w, db, new DeterministicRandom(4));
            Assert.Contains(keep.Id, team.StaffIds);
            Assert.DoesNotContain(go.Id, team.StaffIds);
            Assert.Null(go.TeamId);
            Assert.True(ai.StaffIds.Count >= aiCount, "AI teams keep their structure, renewing or replacing");

            var other = w.Staff[team.StaffIds.First(id => id != keep.Id)];
            other.ContractEndSeason = w.Season + 2;
            Assert.Equal(other.Salary * 0.5 + other.Salary * 0.5 * 2, StaffMarket.SeveranceCost(w, other), 0);
        }
    }
}
