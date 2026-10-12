using System;
using System.Collections.Generic;
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
    public class UniverseTests
    {
        private readonly ITestOutputHelper _out;
        public UniverseTests(ITestOutputHelper o) { _out = o; }

        [Fact]
        public void APlaceLeftFreeIsTakenByANewEntrant()
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = 401 });
            var w = game.World;
            int grid = w.TeamsInCategory("moto2").Count;
            var leaving = w.TeamsInCategory("moto2").First(t => w.OrganisationOf(t).Owner == OwnerKind.Private);
            var org = w.OrganisationOf(leaving);
            org.OwnerFundedSeasons = 99;
            org.OwnerBacking = 0;
            leaving.Reputation = 5;
            game.SimulateRestOfSeason();
            // A season of heavy losses: the owners have to cover them once more.
            Accounts.Post(w, org, null, LedgerKind.Other, -(org.Cash + 50e6), "losses", true, true);
            game.EndSeason();
            Assert.False(w.Teams.ContainsKey(leaving.Id) && w.Teams[leaving.Id].OrganisationId == org.Id && org.TeamIds.Contains(leaving.Id), "the owners gave up");
            Assert.Equal(grid, w.TeamsInCategory("moto2").Count);
            var entrant = w.TeamsInCategory("moto2").FirstOrDefault(t => t.DataConfidence.Contains("new entrant"));
            if (entrant != null)
            {
                Assert.NotNull(w.GetManager(entrant.ManagerId));
                Assert.NotEmpty(entrant.StaffIds);
                Assert.Equal(TransferMarket.Capacity(game.Db.GetCategory("moto2"), game.Db.GetRules("moto2", w.Season), entrant), entrant.RiderIds.Count);
            }
        }

        [Fact(Skip = "long run: 20 seasons (about 15 minutes)")]
        public void TwentySeasonsWithoutCollapseOrInflation()
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = 402 });
            var w = game.World;
            var cats = game.Db.CategoriesOrdered().Where(c => c.Simulated && !c.IsCup).Select(c => c.Id).ToList();
            var grids = cats.ToDictionary(c => c, c => w.TeamsInCategory(c).Count);
            int season0 = w.Season;
            for (int s = 0; s < 20; s++)
            {
                game.SimulateRestOfSeason();
                int news0 = w.News.Count;
                double ownerMoney = 0;
                foreach (var org in w.Organisations.Values)
                {
                    if (org.Owner != OwnerKind.Private) continue;
                    var tot = Accounts.TotalsFor(w, org.Id, w.Season);
                    AccountTotal t;
                    if (tot.TryGetValue(LedgerKind.OwnerFunding, out t)) ownerMoney += t.Cash;
                }
                var summary = game.EndSeason();
                var motogp = w.TeamsInCategory("motogp").ToList();
                double sponsor = motogp.Average(t => t.Sponsors.Sum(d => d.PaymentPerSeason));
                double salary = w.Riders.Values.Where(r => r.Contract != null && r.CategoryId == "motogp").Average(r => r.Contract.SalaryPerSeason);
                double salary3 = w.Riders.Values.Where(r => r.Contract != null && r.CategoryId == "moto3").Average(r => r.Contract.SalaryPerSeason);
                int entrants = w.News.Skip(0).Count(n => n.Headline.Contains("joins") || n.Headline.Contains("se une") || n.Headline.Contains("entra"));
                int negative = w.Organisations.Values.Count(o => o.TeamIds.Count > 0 && o.Cash < 0);
                _out.WriteLine(w.Season + " grids " + string.Join(",", cats.Select(c => w.TeamsInCategory(c).Count)) + " private owner funding " + (ownerMoney / 1e6).ToString("0.0") + "M" +
                               " motogp sponsors/team " + (sponsor / 1e6).ToString("0.00") + "M salary " + (salary / 1e6).ToString("0.00") + "M moto3 salary " + (salary3 / 1e3).ToString("0") + "k negative orgs " + negative +
                               " teams total " + w.Teams.Count + " orgs " + w.Organisations.Values.Count(o => o.TeamIds.Count > 0));
                foreach (var c in cats) Assert.Equal(grids[c], w.TeamsInCategory(c).Count);
            }
        }
    }
}
