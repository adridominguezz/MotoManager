using System;
using System.Linq;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace MotoManager.Tests
{
    public class BalanceExperiment
    {
        private readonly ITestOutputHelper _out;
        public BalanceExperiment(ITestOutputHelper o) { _out = o; }

        [Fact(Skip = "experiment")]
        public void PlayerEconomy()
        {
            foreach (var teamId in new[] { "aspar_moto3", "aspar_moto2", "gresini_motogp", "aruba_ducati" })
            {
                var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = 11, PlayerTeamId = teamId });
                var w = game.World;
                var team = w.PlayerTeam;
                if (team == null) { _out.WriteLine("no team " + teamId); continue; }
                double cash0 = w.OrganisationOf(team).Cash;
                while (!game.IsSeasonOver)
                {
                    foreach (var o in SponsorMarket.OffersFor(w, team).ToList()) SponsorMarket.Accept(w, game.Db, o.Id);
                    var ev = game.BeginNextEvent();
                    game.CompleteEvent(ev);
                }
                var tot = Accounts.TotalsFor(w, team.OrganisationId, w.Season, team.Id);
                double pnl = tot.Values.Sum(t => t.Pnl);
                _out.WriteLine(teamId + " budget " + team.SeasonBudget.ToString("N0") + " P&L " + pnl.ToString("N0") + " cash " + cash0.ToString("N0") + " -> " + w.OrganisationOf(team).Cash.ToString("N0") + " orgTeams " + w.OrganisationOf(team).TeamIds.Count);
                foreach (var kv in tot.OrderBy(k => k.Value.Pnl)) if (Math.Abs(kv.Value.Pnl) > 1) _out.WriteLine("   " + kv.Key + " " + kv.Value.Pnl.ToString("N0"));
            }
        }

        [Fact(Skip = "experiment")]
        public void FoundedTeamEconomy()
        {
            foreach (var cat in new[] { "moto3", "moto2" })
            {
                var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = 12 });
                var w = game.World;
                var m = MotoManager.Core.Career.CareerService.CreatePlayer(w, game.Db, "T", "O", "ES", "entrepreneur");
                double invest = m.PersonalWealth * 0.75;
                var sup = MotoManager.Core.Career.CareerService.Suppliers(w, cat).First();
                var res = MotoManager.Core.Career.CareerService.Found(w, game.Db, m, new MotoManager.Core.Career.FoundingPlan { CategoryId = cat, ManufacturerId = sup.Id, TeamName = "X", Investment = invest });
                if (!res.Ok) { _out.WriteLine(cat + " " + res.Message); continue; }
                var team = res.Team;
                var org = w.OrganisationOf(team);
                double cash0 = org.Cash;
                int offers = 0;
                while (!game.IsSeasonOver)
                {
                    foreach (var o in SponsorMarket.OffersFor(w, team).ToList()) { offers++; SponsorMarket.Accept(w, game.Db, o.Id); }
                    game.CompleteEvent(game.BeginNextEvent());
                }
                var tot = Accounts.TotalsFor(w, org.Id, w.Season, team.Id);
                _out.WriteLine(cat + " founded rep " + team.Reputation + " wealth " + m.PersonalWealth.ToString("N0") + " invest " + invest.ToString("N0") + " P&L " + tot.Values.Sum(t => t.Pnl).ToString("N0") + " cash " + cash0.ToString("N0") + " -> " + org.Cash.ToString("N0") + " offers " + offers + " sponsors " + team.Sponsors.Count);
                foreach (var kv in tot.OrderBy(k => k.Value.Pnl)) if (Math.Abs(kv.Value.Pnl) > 1) _out.WriteLine("   " + kv.Key + " " + kv.Value.Pnl.ToString("N0"));
            }
        }

        [Fact(Skip = "experiment")]
        public void FactoryVersusSatellite()
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = 5 });
            var w = game.World;
            for (int s = 0; s < 8; s++)
            {
                game.SimulateRestOfSeason();
                game.EndSeason();
                var lines = w.TeamsInCategory("motogp").Select(t => new
                {
                    t.Name, t.Status, Skill = t.RiderIds.Select(w.GetRider).Where(r => r != null).DefaultIfEmpty().Average(r => r == null ? 0 : r.Attributes.RaceSkill)
                }).OrderByDescending(x => x.Skill).ToList();
                _out.WriteLine("season " + w.Season + ": " + string.Join(" | ", lines.Take(6).Select(x => x.Name + "(" + x.Status + ") " + x.Skill.ToString("0"))));
                foreach (var sat in w.TeamsInCategory("motogp").Where(t => t.Status != TeamStatus.Factory))
                {
                    var fac = w.TeamsInCategory("motogp").FirstOrDefault(t => t.Status == TeamStatus.Factory && t.ManufacturerId == sat.ManufacturerId);
                    if (fac == null) continue;
                    Func<Team, double> sk = t => t.RiderIds.Select(w.GetRider).Where(r => r != null).DefaultIfEmpty().Average(r => r == null ? 0 : r.Attributes.RaceSkill);
                    if (sk(sat) > sk(fac) + 1) _out.WriteLine("   INVERTED " + sat.Name + " " + sk(sat).ToString("0") + " > " + fac.Name + " " + sk(fac).ToString("0"));
                }
            }
        }
    }
}
