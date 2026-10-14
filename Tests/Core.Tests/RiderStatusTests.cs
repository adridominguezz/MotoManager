using MotoManager.Core.Career;
using MotoManager.Core.Season;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MotoManager.Tests
{
    public class RiderStatusTests
    {
        [Fact]
        public void NamingALeadRiderMovesMoraleAndSwitchingBackGainsNothing()
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = 611, PlayerTeamId = "aspar_moto3" });
            var w = game.World;
            var team = w.PlayerTeam;
            Assert.True(team.RiderIds.Count >= 2);
            var a = w.GetRider(team.RiderIds[0]);
            var b = w.GetRider(team.RiderIds[1]);
            a.Morale = 60;
            b.Morale = 60;
            Assert.Equal(RiderStatus.Equal, RiderStatusRules.Of(team, a.Id));

            Assert.True(RiderStatusRules.Set(w, team, a.Id, RiderStatus.Lead));
            Assert.Equal(RiderStatus.Lead, RiderStatusRules.Of(team, a.Id));
            Assert.Equal(RiderStatus.Second, RiderStatusRules.Of(team, b.Id));
            Assert.Equal(65, a.Morale, 3);
            Assert.Equal(55, b.Morale, 3);
            // Same order again: no change.
            Assert.False(RiderStatusRules.Set(w, team, b.Id, RiderStatus.Second));

            // The other way round: the swing is the full difference between the statuses.
            Assert.True(RiderStatusRules.Set(w, team, a.Id, RiderStatus.Second));
            Assert.Equal(b.Id, team.LeadRiderId);
            Assert.Equal(55, a.Morale, 3);
            Assert.Equal(65, b.Morale, 3);

            // Equal status: back where they started.
            Assert.True(RiderStatusRules.Set(w, team, b.Id, RiderStatus.Equal));
            Assert.Null(team.LeadRiderId);
            Assert.Equal(60, a.Morale, 3);
            Assert.Equal(60, b.Morale, 3);

            // A lead who left the team leaves everyone equal.
            team.LeadRiderId = "nobody";
            Assert.Equal(RiderStatus.Equal, RiderStatusRules.Of(team, a.Id));
        }

        [Fact]
        public void SavesWithoutTheLeadRiderLoadWithEqualStatus()
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = 613, PlayerTeamId = "aspar_moto3" });
            var w = game.World;
            RiderStatusRules.Set(w, w.PlayerTeam, w.PlayerTeam.RiderIds[0], RiderStatus.Lead);
            var obj = JObject.Parse(SaveSystem.Serialize(w));
            foreach (var t in ((JObject)obj["Teams"]).Properties()) ((JObject)t.Value).Remove("LeadRiderId");
            var loaded = SaveSystem.Deserialize(obj.ToString());
            var team = loaded.Teams[w.PlayerTeam.Id];
            Assert.Null(team.LeadRiderId);
            Assert.Equal(RiderStatus.Equal, RiderStatusRules.Of(team, team.RiderIds[0]));
            // And a save with it keeps it.
            var kept = SaveSystem.Deserialize(SaveSystem.Serialize(w)).Teams[w.PlayerTeam.Id];
            Assert.Equal(w.PlayerTeam.RiderIds[0], kept.LeadRiderId);
        }
    }
}
