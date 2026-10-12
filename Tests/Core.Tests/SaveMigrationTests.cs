using System;
using System.Linq;
using MotoManager.Core.Season;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MotoManager.Tests
{
    public class SaveMigrationTests
    {
        [Fact]
        public void AVersionOneSaveLoadsAndIsBroughtUpToDate()
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = 501, PlayerTeamId = "aspar_moto3" });
            game.CompleteEvent(game.BeginNextEvent());
            var w = game.World;
            // A save as version 1 wrote it: no history, records or grid targets.
            var obj = JObject.Parse(SaveSystem.Serialize(w));
            obj["Version"] = 1;
            obj.Remove("History");
            obj.Remove("Records");
            obj.Remove("GridTargets");
            var loaded = SaveSystem.Deserialize(obj.ToString());
            Assert.NotNull(loaded.History);
            Assert.NotNull(loaded.Records);
            Assert.Equal(w.TeamsInCategory("motogp").Count, loaded.GridTargets["motogp"]);
            Assert.Equal(w.Riders.Count, loaded.Riders.Count);
            Assert.Equal(w.CurrentDate, loaded.CurrentDate);
            Assert.Equal(w.OrganisationOf(w.PlayerTeam).Cash, loaded.Organisations[w.PlayerTeam.OrganisationId].Cash, 2);
            // Saved again it is a current-version save, and the game goes on from it.
            Assert.Equal(SaveSystem.CurrentVersion, (int)JObject.Parse(SaveSystem.Serialize(loaded))["Version"]);
            var again = new GameController(TestData.Db, loaded);
            again.CompleteEvent(again.BeginNextEvent());
        }

        [Fact]
        public void ASaveFromANewerVersionIsRefused()
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = 502 });
            var obj = JObject.Parse(SaveSystem.Serialize(game.World));
            obj["Version"] = SaveSystem.CurrentVersion + 1;
            Assert.Throws<FormatException>(() => SaveSystem.Deserialize(obj.ToString()));
        }
    }
}
