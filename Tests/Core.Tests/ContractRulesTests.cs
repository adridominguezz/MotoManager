using System.Collections.Generic;
using System.Linq;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace MotoManager.Tests
{
    public class ContractRulesTests
    {
        private readonly ITestOutputHelper _out;

        public ContractRulesTests(ITestOutputHelper output)
        {
            _out = output;
        }

        private static (GameController, Team) Career(string teamId, ulong seed)
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = seed, PlayerTeamId = teamId });
            return (game, game.World.PlayerTeam);
        }

        private static WeekendResult Race(Team team, Rider rider, int position)
        {
            var w = new WeekendResult();
            var race = new SessionResult { Type = SessionType.Race };
            race.Classification.Add(new ResultEntry { RiderId = rider.Id, TeamId = team.Id, Position = position, Status = ResultStatus.Finished });
            w.Sessions.Add(race);
            return w;
        }

        [Fact]
        public void AWinPaysTheWinTierOnly()
        {
            var (game, team) = Career("aspar_moto3", 81);
            var w = game.World;
            var rider = w.GetRider(team.RiderIds[0]);
            var c = rider.Contract;
            c.RaceBonuses = new List<PositionBonus> { new PositionBonus { MaxPosition = 1, Amount = 10000 }, new PositionBonus { MaxPosition = 3, Amount = 4000 }, new PositionBonus { MaxPosition = 10, Amount = 500 } };
            Assert.Equal(10000, Contracts.PayRaceBonuses(w, team, Race(team, rider, 1)));
            Assert.Equal(4000, Contracts.PayRaceBonuses(w, team, Race(team, rider, 2)));
            Assert.Equal(500, Contracts.PayRaceBonuses(w, team, Race(team, rider, 7)));
            Assert.Equal(0, Contracts.PayRaceBonuses(w, team, Race(team, rider, 12)));
        }

        [Fact]
        public void TerminatingEarlyCostsWhatTheContractSays()
        {
            var (game, team) = Career("aspar_moto3", 82);
            var w = game.World;
            var rider = w.GetRider(team.RiderIds[0]);
            rider.Contract.EndSeason = w.Season + 2;
            rider.Contract.SalaryPerSeason = 200000;
            rider.Contract.TerminationShare = 0.5;
            // Before any event: the whole season still to pay plus half of the two following seasons.
            double cost = Contracts.TerminationCost(w, game.Db, rider);
            Assert.Equal(200000 + 0.5 * 200000 * 2, cost, 0);
            double cash = Accounts.Cash(w, team);
            Contracts.Terminate(w, game.Db, team, rider);
            Assert.Equal(cash - cost, Accounts.Cash(w, team), 2);
            Assert.Null(rider.Contract);
            Assert.DoesNotContain(rider.Id, team.RiderIds);
            var record = rider.ContractHistory.Last();
            Assert.Equal(ContractEnd.Released, record.Reason);
            Assert.Equal(cost, record.Fee, 2);
        }

        [Fact]
        public void PlayersRidersCanOnlyBeTakenThroughTheirReleaseClause()
        {
            var (game, team) = Career("aspar_moto3", 83);
            var w = game.World;
            var db = game.Db;
            var rider = w.GetRider(team.RiderIds[0]);
            rider.Contract.EndSeason = w.Season + 2;
            rider.Contract.Clauses.Clear();
            var buyer = w.Teams["ajo_moto3"];
            Assert.Equal(-1, Contracts.FeeToTake(w, db, rider, buyer, w.Season + 1));
            rider.Contract.Clauses.Add(new ContractClause { Type = ClauseType.ReleaseClause, Amount = 750000 });
            Assert.Equal(750000, Contracts.FeeToTake(w, db, rider, buyer, w.Season + 1));

            // An AI team's rider has a negotiated value; a higher-category exit clause makes him free for that move.
            var aiRider = w.GetRider(buyer.RiderIds[0]);
            aiRider.Contract.EndSeason = w.Season + 2;
            aiRider.Contract.Clauses.Clear();
            Assert.True(Contracts.FeeToTake(w, db, aiRider, team, w.Season + 1) > 0);
            aiRider.Contract.Clauses.Add(new ContractClause { Type = ClauseType.ExitForHigherCategory });
            Assert.Equal(0, Contracts.FeeToTake(w, db, aiRider, w.Teams["intact_moto2"], w.Season + 1));
            Assert.True(Contracts.FeeToTake(w, db, aiRider, team, w.Season + 1) > 0, "the exit clause only works upwards");

            // Paying moves the money from buyer to seller and records the transfer.
            double buyerCash = Accounts.Cash(w, buyer), sellerCash = Accounts.Cash(w, team);
            Contracts.PayAndRelease(w, db, rider, buyer, 750000, true);
            Assert.Equal(buyerCash - 750000, Accounts.Cash(w, buyer), 2);
            Assert.Equal(sellerCash + 750000, Accounts.Cash(w, team), 2);
            Assert.Equal(ContractEnd.ReleaseClause, rider.ContractHistory.Last().Reason);
        }

        [Fact]
        public void ClausesActAtSeasonEndInTheAgreedOrder()
        {
            var (game, team) = Career("aspar_moto3", 84);
            var w = game.World;
            var db = game.Db;
            var a = w.GetRider(team.RiderIds[0]);
            var b = w.GetRider(team.RiderIds[1]);
            foreach (var r in new[] { a, b })
            {
                r.Contract.EndSeason = w.Season;
                r.Contract.Clauses.Clear();
                r.FutureContract = null;
            }
            // a: top-3 performance clause; b: a team option the player did not exercise.
            a.Contract.Clauses.Add(new ContractClause { Type = ClauseType.PerformanceExtension, ChampionshipTopN = 3, ExtraSeasons = 1, Salary = 300000 });
            b.Contract.Clauses.Add(new ContractClause { Type = ClauseType.TeamOption, ExtraSeasons = 2 });
            var champ = w.Championships["moto3"];
            champ.Riders[a.Id] = new StandingEntry { Id = a.Id, Points = 500 };
            champ.Riders[b.Id] = new StandingEntry { Id = b.Id, Points = 1 };

            Contracts.EvaluateSeasonEnd(w, db, new DeterministicRandom(1));
            Assert.Equal(w.Season + 1, a.Contract.StartSeason);
            Assert.Equal(w.Season + 1, a.Contract.EndSeason);
            Assert.Equal(300000, a.Contract.SalaryPerSeason);
            Assert.Null(a.Contract.Clause(ClauseType.PerformanceExtension));
            Assert.Equal(ContractEnd.Extended, a.ContractHistory.Last().Reason);
            Assert.Equal(w.Season, b.Contract.EndSeason);

            // Exercised during the season: the option extends the contract.
            b.Contract.Clause(ClauseType.TeamOption).Exercised = true;
            Contracts.EvaluateSeasonEnd(w, db, new DeterministicRandom(1));
            Assert.Equal(w.Season + 2, b.Contract.EndSeason);
            Assert.Equal(ContractEnd.OptionExercised, b.ContractHistory.Last().Reason);
        }

        [Fact]
        public void AFullSeasonKeepsContractsConsistentAndFeesBalanced()
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = 85 });
            var w = game.World;
            while (!game.IsSeasonOver)
            {
                var ev = game.BeginNextEvent();
                game.CompleteEvent(ev);
            }
            int season = w.Season;
            game.EndSeason();
            double fees = w.Finance.Totals.Where(t => t.Season == season && t.Kind == LedgerKind.TransferFee).Sum(t => t.Cash);
            Assert.Equal(0, fees, 2);
            int records = w.Riders.Values.Sum(r => r.ContractHistory.Count);
            var reasons = w.Riders.Values.SelectMany(r => r.ContractHistory).GroupBy(h => h.Reason).Select(g => g.Key + " " + g.Count());
            _out.WriteLine(string.Join(", ", reasons));
            Assert.True(records > 50);
            foreach (var t in w.Teams.Values)
                foreach (var id in t.RiderIds)
                {
                    var r = w.GetRider(id);
                    Assert.NotNull(r.Contract);
                    Assert.NotNull(r.Contract.Id);
                    Assert.True(r.Contract.RaceBonuses.Count > 0);
                }
            Assert.False(Accounts.TryBegin(w, "season-bonus|x") == false);
        }
    }
}
