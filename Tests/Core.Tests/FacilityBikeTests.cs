using System;
using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Race;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using Xunit;

namespace MotoManager.Tests
{
    /// <summary>Facilities show on the bike itself: each linked facility level lifts its component.</summary>
    public class FacilityBikeTests
    {
        private static (GameController, Team, Motorcycle, BikeClassDefinition) Career(string teamId, ulong seed)
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = seed, PlayerTeamId = teamId });
            var team = game.World.PlayerTeam;
            var bike = game.World.GetBike(team.BikeId);
            return (game, team, bike, TestData.Db.GetBikeClass(bike.BikeClassId));
        }

        [Fact]
        public void WindTunnelUpgradeRaisesOnlyTheAerodynamics()
        {
            var (game, team, bike, cls) = Career("aspar_moto2", 211);
            var w = game.World;
            var db = TestData.Db;
            foreach (FacilityType f in Enum.GetValues(typeof(FacilityType))) team.Facilities[f] = 2;
            bike.Components["Aerodynamics"] = 60;

            var before = TeamCapabilities.Performance(w, db, team, bike, cls);
            double aeroBefore = TeamCapabilities.EffectiveComponent(w, db, team, bike, "Aerodynamics");
            double chassisBefore = TeamCapabilities.EffectiveComponent(w, db, team, bike, "Chassis");

            Accounts.Post(w, team, LedgerKind.OwnerFunding, 1e9, "test", true, false);
            Assert.True(FinanceService.UpgradeFacility(w, db, team, FacilityType.WindTunnel));
            Assert.Equal(3, team.Facility(FacilityType.WindTunnel));

            var after = TeamCapabilities.Performance(w, db, team, bike, cls);
            Assert.Equal(aeroBefore + TeamCapabilities.FacilityComponentBonusPerLevel,
                TeamCapabilities.EffectiveComponent(w, db, team, bike, "Aerodynamics"), 6);
            Assert.Equal(chassisBefore, TeamCapabilities.EffectiveComponent(w, db, team, bike, "Chassis"), 6);
            // The bike's own level is untouched: the bonus is computed, not written into the component.
            Assert.Equal(60, bike.Component("Aerodynamics"), 6);

            // Attributes that use the aerodynamics improve; the rest stay exactly as they were.
            Dictionary<string, double> weights;
            foreach (PerfAttr a in Enum.GetValues(typeof(PerfAttr)))
            {
                bool aero = cls.PerformanceWeights.TryGetValue(a, out weights) && weights.ContainsKey("Aerodynamics");
                if (aero) Assert.True(after[a] > before[a], a + " should improve");
                else Assert.Equal(before[a], after[a], 9);
            }
        }

        [Fact]
        public void FacilityBonusIsModestAndOnlyOnDevelopableComponents()
        {
            Assert.Equal(0, TeamCapabilities.FacilityComponentBonus(1));
            Assert.InRange(TeamCapabilities.FacilityComponentBonus(5), 4, 6.5);
            Assert.Equal(FacilityType.WindTunnel, TeamCapabilities.BikeFacilityFor("Aerodynamics"));
            Assert.Equal(FacilityType.Dyno, TeamCapabilities.BikeFacilityFor("Engine"));
            Assert.Equal(FacilityType.ElectronicsLab, TeamCapabilities.BikeFacilityFor("Electronics"));
            Assert.Equal(FacilityType.Factory, TeamCapabilities.BikeFacilityFor("Chassis"));
            Assert.Null(TeamCapabilities.BikeFacilityFor("Suspension"));

            // Moto3 freezes the engine: a better dyno does not change it.
            var (game, team, bike, cls) = Career("aspar_moto3", 212);
            team.Facilities[FacilityType.Dyno] = 5;
            Assert.Equal(0, TeamCapabilities.ComponentBonus(game.World, TestData.Db, team, "Engine"));
            team.Facilities[FacilityType.WindTunnel] = 5;
            Assert.True(TeamCapabilities.ComponentBonus(game.World, TestData.Db, team, "Aerodynamics") > 0);
        }

        [Fact]
        public void AiTeamsRaceWithTheirFacilityBonus()
        {
            var (game, team, bike, cls) = Career("aspar_moto2", 213);
            var w = game.World;
            foreach (var t in w.TeamsInCategory(team.CategoryId))
            {
                if (t.IsPlayer) continue;
                var b = w.GetBike(t.BikeId);
                var c = TestData.Db.GetBikeClass(b.BikeClassId);
                t.Facilities[FacilityType.WindTunnel] = 4;
                var with = TeamCapabilities.Performance(w, TestData.Db, t, b, c);
                var plain = BikePerformance.Compute(b, c);
                Assert.True(with[PerfAttr.TopSpeed] > plain[PerfAttr.TopSpeed]);
                return;
            }
        }
    }
}
