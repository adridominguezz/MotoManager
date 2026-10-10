using System.Linq;
using MotoManager.Core.Career;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace MotoManager.Tests
{
    public class PayRiderTests
    {
        private readonly ITestOutputHelper _out;

        public PayRiderTests(ITestOutputHelper output)
        {
            _out = output;
        }

        [Fact]
        public void MoneyMattersToSmallTeamsAndHardlyAtTheTop()
        {
            var db = TestData.Db;
            var world = WorldFactory.Create(db, new NewGameOptions { Seed = 61 });
            var small = world.TeamsInCategory("moto3").OrderBy(t => t.Reputation).First();
            var top = world.Teams["ducati_lenovo"];
            var rider = world.Riders.Values.First(r => r.CategoryId == "moto3" && r.TeamId != null);
            rider.PersonalBacking = 0;
            double smallWithout = TransferMarket.EvaluateForTeam(world, db, small, rider);
            double topWithout = TransferMarket.EvaluateForTeam(world, db, top, rider);
            rider.PersonalBacking = 400000;
            double smallWith = TransferMarket.EvaluateForTeam(world, db, small, rider);
            double topWith = TransferMarket.EvaluateForTeam(world, db, top, rider);
            _out.WriteLine(small.Name + " +" + (smallWith - smallWithout).ToString("0.0") + " · " + top.Name + " +" + (topWith - topWithout).ToString("0.00"));
            Assert.True(smallWith - smallWithout > 4, "a modest Moto3 team should value the money like several points of ability");
            Assert.True(topWith - topWithout < 0.6, "a MotoGP works team barely notices it");
            Assert.Equal(0, TransferMarket.BackingValue(world, db, world.Teams["rookies_cup"], rider));
        }

        [Fact]
        public void TheRidersBackersPayTheTeamEveryEvent()
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = 62, PlayerTeamId = "aspar_moto3" });
            var w = game.World;
            var team = w.PlayerTeam;
            foreach (var id in team.RiderIds) w.GetRider(id).Contract.RiderFunding = 0;
            var rider = w.GetRider(team.RiderIds[0]);
            rider.Contract.RiderFunding = 300000;
            Assert.Equal(300000, CostProfile.Of(w, team).RiderFunding, 2);
            while (true)
            {
                var ev = game.BeginNextEvent();
                game.CompleteEvent(ev);
                if (ev.Weekends.ContainsKey(team.CategoryId)) break;
            }
            var totals = Accounts.TotalsFor(w, team.OrganisationId, w.Season, team.Id);
            int rounds = game.RoundsInSeason(team.CategoryId);
            Assert.Equal(300000.0 / rounds, totals[LedgerKind.RiderFunding].Cash, 2);
        }
    }
}
