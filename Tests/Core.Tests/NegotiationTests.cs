using System.Linq;
using MotoManager.Core.Career;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace MotoManager.Tests
{
    public class NegotiationTests
    {
        private readonly ITestOutputHelper _out;

        public NegotiationTests(ITestOutputHelper output)
        {
            _out = output;
        }

        private static (GameController, Team) Career(string teamId, ulong seed)
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = seed, PlayerTeamId = teamId });
            return (game, game.World.PlayerTeam);
        }

        /// <summary>A rider of another team in the same category whose contract ends this season (free for next season).</summary>
        private static Rider Expiring(WorldState w, Team team)
        {
            var r = w.Riders.Values.Where(x => x.CategoryId == team.CategoryId && x.TeamId != null && x.TeamId != team.Id && !x.IsRetired)
                .OrderBy(x => x.Id).First();
            r.Contract.EndSeason = w.Season;
            r.FutureContract = null;
            return r;
        }

        [Fact]
        public void ARiderCountersALowOfferAndSignsWhatHeAskedFor()
        {
            var (game, team) = Career("aspar_moto2", 91);
            var w = game.World;
            var db = game.Db;
            var me = w.GetRider(team.RiderIds[0]);
            me.Contract.EndSeason = w.Season; // a seat frees up for next season
            var r = Expiring(w, team);
            var p = ContractService.DefaultProposal(w, db, team, r);
            p.Salary *= 0.5;
            var answer = ContractService.Propose(w, db, team, r, p);
            Assert.False(answer.Accepted);
            Assert.NotNull(answer.Counter);
            Assert.True(answer.Counter.Salary > p.Salary);
            _out.WriteLine(answer.Message);
            var signed = ContractService.Propose(w, db, team, r, answer.Counter);
            Assert.True(signed.Accepted, signed.Message);
            Assert.Equal(team.Id, r.FutureContract.TeamId);
            Assert.Equal(w.Season + 1, r.FutureContract.StartSeason);
        }

        [Fact]
        public void RidersLoseTheirPatience()
        {
            var (game, team) = Career("aspar_moto2", 92);
            var w = game.World;
            var db = game.Db;
            w.GetRider(team.RiderIds[0]).Contract.EndSeason = w.Season;
            var r = Expiring(w, team);
            var p = ContractService.DefaultProposal(w, db, team, r);
            p.Salary = 1000;
            for (int i = 0; i < ContractService.Patience; i++) Assert.NotNull(ContractService.Propose(w, db, team, r, p).Counter);
            var last = ContractService.Propose(w, db, team, r, p);
            Assert.Null(last.Counter);
            Assert.Contains("stopped", last.Message);
        }

        [Fact]
        public void AmbitiousRidersValueBonusesMore()
        {
            var (game, team) = Career("ducati_lenovo", 93);
            var w = game.World;
            var db = game.Db;
            var r = w.GetRider(team.RiderIds[0]);
            var p = ContractService.DefaultProposal(w, db, team, r);
            p.WinBonus *= 5;
            r.Personality.Ambition = 90;
            double ambitious = ContractService.RiderValue(w, db, team, r, p);
            r.Personality.Ambition = 10;
            double cautious = ContractService.RiderValue(w, db, team, r, p);
            Assert.True(ambitious > cautious);
            var cost = ContractService.Cost(w, db, team, r, p);
            Assert.True(cost.ExpectedBonusesPerSeason > 0);
            Assert.Equal(p.Salary * p.Seasons, cost.GuaranteedTotal, 2);
        }

        [Fact]
        public void ARiderUnderContractNeedsAnAgreementAndCanMoveNow()
        {
            var (game, team) = Career("aspar_moto2", 94);
            var w = game.World;
            var db = game.Db;
            var seller = w.Teams["ajo_moto2"];
            var target = w.GetRider(seller.RiderIds[0]);
            target.Contract.EndSeason = w.Season + 1;
            target.Contract.Clauses.Clear();
            var p = ContractService.DefaultProposal(w, db, team, target);
            p.StartNow = true;
            p.Salary *= 1.5;

            // Full team: a seat must be freed first.
            Assert.Contains("No free seat", ContractService.Propose(w, db, team, target, p).Message);
            var mine = w.GetRider(team.RiderIds[0]);
            double cost = Contracts.TerminationCost(w, db, mine);
            Contracts.Terminate(w, db, team, mine);
            Assert.True(cost > 0);
            // No agreement yet, no release clause: not possible.
            Assert.Contains("agree a transfer", ContractService.Propose(w, db, team, target, p).Message);
            var low = ContractService.ProposeTransfer(w, db, team, target, 1000, true);
            Assert.Equal(TransferStatus.Rejected, low.Status);
            Assert.True(low.Asking > 1000);
            var ok = ContractService.ProposeTransfer(w, db, team, target, low.Asking, true);
            Assert.Equal(TransferStatus.Accepted, ok.Status);


            double sellerCash = Accounts.Cash(w, seller);
            var signed = ContractService.Propose(w, db, team, target, p);
            Assert.True(signed.Accepted, signed.Message);
            Assert.Equal(team.Id, target.TeamId);
            Assert.Equal(w.Season, target.Contract.StartSeason);
            Assert.Equal(sellerCash + low.Asking, Accounts.Cash(w, seller), 2);
            Assert.Equal(ContractEnd.Transfer, target.ContractHistory.Last().Reason);
            // The seller filled the seat straight away.
            Assert.Equal(TransferMarket.Capacity(db.GetCategory("moto2"), db.GetRules("moto2", w.Season), seller), seller.RiderIds.Count);
        }

        [Fact]
        public void AcceptingABidMovesTheRiderAtTheEndOfTheSeason()
        {
            var (game, team) = Career("aspar_moto2", 95);
            var w = game.World;
            var db = game.Db;
            var r = w.GetRider(team.RiderIds[0]);
            r.Contract.EndSeason = w.Season + 2;
            var buyer = w.Teams["ducati_lenovo"];
            w.TransferOffers.Add(new TransferOffer { Id = "bid1", RiderId = r.Id, FromTeamId = team.Id, ToTeamId = buyer.Id, Fee = 2000000, ExpiresAfterEvent = 99 });
            double cash = Accounts.Cash(w, team);
            Assert.True(ContractService.AcceptBid(w, db, "bid1"));
            Assert.Equal(cash + 2000000, Accounts.Cash(w, team), 2);
            Assert.Equal(team.Id, r.TeamId); // he finishes the season here
            Assert.Equal(buyer.Id, r.FutureContract.TeamId);
            while (!game.IsSeasonOver)
            {
                var ev = game.BeginNextEvent();
                game.CompleteEvent(ev);
            }
            game.EndSeason();
            Assert.Equal(buyer.Id, r.TeamId);
            Assert.Contains(r.ContractHistory, h => h.Reason == ContractEnd.Transfer && h.TeamId == team.Id);
        }

        [Fact]
        public void TeamOptionsCanOnlyBeExercisedInTheLastSeason()
        {
            var (game, team) = Career("aspar_moto2", 96);
            var w = game.World;
            var r = w.GetRider(team.RiderIds[0]);
            r.Contract.Clauses.Add(new ContractClause { Type = ClauseType.TeamOption, ExtraSeasons = 1 });
            r.Contract.EndSeason = w.Season + 1;
            Assert.False(ContractService.ExerciseOption(w, team, r));
            r.Contract.EndSeason = w.Season;
            Assert.True(ContractService.ExerciseOption(w, team, r));
            Assert.True(r.Contract.Clause(ClauseType.TeamOption).Exercised);
        }
    }
}
