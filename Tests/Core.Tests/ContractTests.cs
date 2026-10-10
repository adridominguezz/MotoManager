using System.Linq;
using MotoManager.Core.Career;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using Xunit;

namespace MotoManager.Tests
{
    public class ContractTests
    {
        private static (GameController game, Team team) NewCareer(string teamId)
        {
            var db = TestData.Db;
            var game = GameController.NewGame(db, new NewGameOptions { Seed = 12, PlayerTeamId = teamId });
            return (game, game.World.GetTeam(teamId));
        }

        [Fact]
        public void PlayerCanSignARiderForNextSeason()
        {
            var (game, team) = NewCareer("lcr_motogp");
            var world = game.World;
            // Free a seat: one of our riders' deals ends and he is not renewed.
            var leaving = world.GetRider(team.RiderIds[0]);
            leaving.Contract.EndSeason = world.Season;
            var target = world.Riders.Values.First(r => r.CategoryId == "moto2" && r.Contract != null && r.Contract.EndSeason == world.Season && !r.IsRetired);
            int freeBefore = ContractService.SeatsFreeNextSeason(world, game.Db, team);
            Assert.True(freeBefore >= 1);
            var low = ContractService.Offer(world, game.Db, team, target, 1000, 2);
            Assert.False(low.Accepted);
            Assert.True(low.AskingSalary > 1000);
            var ok = ContractService.Offer(world, game.Db, team, target, low.AskingSalary * 1.2, 2);
            Assert.True(ok.Accepted, ok.Message);
            Assert.NotNull(target.FutureContract);
            Assert.Equal(freeBefore - 1, ContractService.SeatsFreeNextSeason(world, game.Db, team));

            game.SimulateRestOfSeason();
            game.EndSeason();
            Assert.Equal(team.Id, target.TeamId);
            Assert.Equal("motogp", target.CategoryId);
            Assert.Contains(target.Id, team.RiderIds);
            Assert.Equal(2, team.RiderIds.Count);
        }

        [Fact]
        public void RenewalIsANewContractFromNextSeason()
        {
            var (game, team) = NewCareer("ducati_lenovo");
            var world = game.World;
            var rider = world.GetRider(team.RiderIds[0]);
            var current = rider.Contract;
            double salaryNow = current.SalaryPerSeason;
            double ask = ContractService.AskingSalary(world, game.Db, team, rider);
            var res = ContractService.Offer(world, game.Db, team, rider, ask, 3);
            Assert.True(res.Accepted, res.Message);
            // This season's terms are not rewritten.
            Assert.Same(current, rider.Contract);
            Assert.Equal(salaryNow, rider.Contract.SalaryPerSeason);
            Assert.NotNull(rider.FutureContract);
            Assert.Equal(world.Season + 1, rider.FutureContract.StartSeason);
            Assert.Equal(world.Season + 3, rider.FutureContract.EndSeason);
            Assert.Equal(team.Id, rider.FutureContract.TeamId);
        }

        [Fact]
        public void StaffCanBeHiredAndFired()
        {
            var (game, team) = NewCareer("trackhouse_motogp");
            var world = game.World;
            var candidates = StaffMarket.Candidates(world, game.Db, team, StaffRole.DataEngineer);
            Assert.Equal(5, candidates.Count);
            double budget = Accounts.Cash(world, team);
            var hired = StaffMarket.Hire(world, team, candidates[0]);
            Assert.Contains(hired.Id, team.StaffIds);
            Assert.Equal(1, team.StaffIds.Count(id => world.Staff[id].Role == StaffRole.DataEngineer));
            Assert.True(Accounts.Cash(world, team) < budget);
            var forecast = FinanceService.SeasonForecast(world, game.Db, team, 22);
            Assert.True(forecast.Income > 0 && forecast.Expenses > 0);
        }
    }
}
