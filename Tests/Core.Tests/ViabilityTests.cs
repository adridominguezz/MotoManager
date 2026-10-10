using System;
using System.Collections.Generic;
using System.Linq;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace MotoManager.Tests
{
    public class ViabilityTests
    {
        private readonly ITestOutputHelper _out;

        public ViabilityTests(ITestOutputHelper output)
        {
            _out = output;
        }

        private static readonly LedgerKind[] Regular =
        {
            LedgerKind.Sponsorship, LedgerKind.ParticipationIncome, LedgerKind.RiderSalary, LedgerKind.StaffSalary,
            LedgerKind.FacilityUpkeep, LedgerKind.BikeLease, LedgerKind.Licence, LedgerKind.Travel
        };

        [Fact]
        public void ForecastUsesTheSameRulesAsRealPayments()
        {
            var db = TestData.Db;
            var game = GameController.NewGame(db, new NewGameOptions { Seed = 31, PlayerTeamId = "aspar_moto3" });
            var w = game.World;
            var team = w.PlayerTeam;
            var rules = db.GetRules(team.CategoryId, w.Season);
            var rounds = CashflowForecast.RemainingRounds(w, db, team.CategoryId);
            int total = game.RoundsInSeason(team.CategoryId);
            var expected = new Dictionary<LedgerKind, double>();
            foreach (var kv in FinanceService.RoundItems(CostProfile.Of(w, team), rules, total))
                expected[kv.Key] = kv.Value * rounds.Count;

            var forecast = CashflowForecast.ForOrganisation(w, db, w.OrganisationOf(team));
            Assert.Equal(w.OrganisationOf(team).Cash, forecast.StartCash);
            Assert.True(forecast.Points.Count >= rounds.Count);

            while (!game.IsSeasonOver)
            {
                var ev = game.BeginNextEvent();
                game.CompleteEvent(ev);
            }
            var actual = Accounts.TotalsFor(w, team.OrganisationId, w.Season, team.Id);
            foreach (var k in Regular)
            {
                double a = actual.ContainsKey(k) ? actual[k].Cash : 0;
                double e = expected.ContainsKey(k) ? expected[k] : 0;
                Assert.True(Math.Abs(a - e) < 1 + Math.Abs(e) * 1e-6, k + ": forecast " + e + " actual " + a);
            }
        }

        [Fact]
        public void EntryCostAloneDoesNotMakeACategoryViable()
        {
            var db = TestData.Db;
            var world = WorldFactory.Create(db, new NewGameOptions { Seed = 32 });

            var probe = ViabilityService.Evaluate(world, db, new TeamPlan { CategoryId = "moto3", Capital = 0 });
            double startup = probe.StartupTotal;
            Assert.True(startup > 0);

            // Enough to pay the start-up, not to run the season.
            var tight = ViabilityService.Evaluate(world, db, new TeamPlan { CategoryId = "moto3", Capital = startup * 1.05 });
            Assert.Equal(Viability.Unviable, tight.Verdict);
            Assert.Contains(ViabilityReason.CashNegativeConfirmed, tight.Reasons);
            Assert.True(tight.Shortfall > 0);

            // With the shortfall covered the same plan becomes viable.
            var funded = ViabilityService.Evaluate(world, db, new TeamPlan { CategoryId = "moto3", Capital = startup * 1.05 + tight.Shortfall + 1 });
            Assert.Equal(Viability.Viable, funded.Verdict);
            Assert.True(funded.MinCashConfirmed >= funded.Reserve);

            // Between the two: the plan only holds if probable income arrives.
            double mid = startup * 1.05 + tight.Shortfall * 0.5;
            var risky = ViabilityService.Evaluate(world, db, new TeamPlan { CategoryId = "moto3", Capital = mid });
            Assert.NotEqual(Viability.Viable, risky.Verdict);

            Assert.Equal(Viability.NotAvailable, ViabilityService.Evaluate(world, db, new TeamPlan { CategoryId = "rookies", Capital = 1e9 }).Verdict);

            foreach (var r in ViabilityService.EvaluateAll(world, db, 8000000, 30))
                _out.WriteLine(r.CategoryId.PadRight(9) + r.Verdict.ToString().PadRight(13) + " start-up " + (r.StartupTotal / 1e6).ToString("0.00") + "M  operating " +
                               (r.OperatingCost / 1e6).ToString("0.00") + "M  confirmed " + (r.ConfirmedIncome / 1e6).ToString("0.00") + "M  probable " +
                               (r.ProbableIncome / 1e6).ToString("0.00") + "M  min cash " + (r.MinCashConfirmed / 1e6).ToString("0.00") + "M  " + string.Join(",", r.Reasons));
        }

        [Fact]
        public void ExpansionCountsTheTeamsAlreadyOwned()
        {
            var db = TestData.Db;
            var world = WorldFactory.Create(db, new NewGameOptions { Seed = 33 });
            var org = world.GetOrganisation("aspar_team");
            var alone = ViabilityService.Evaluate(world, db, new TeamPlan { CategoryId = "worldssp", Capital = org.Cash });
            var withOrg = ViabilityService.Evaluate(world, db, new TeamPlan { CategoryId = "worldssp", Capital = org.Cash, Organisation = org });
            Assert.Equal(alone.OperatingCost, withOrg.OperatingCost, 2);
            Assert.True(withOrg.MinCashConfirmed < alone.MinCashConfirmed, "existing teams' costs must weigh on an expansion");
        }
    }
}
