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
        public void LiveriesOfOlderSavesGetDefaultsAndChosenOnesRoundTrip()
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = 503, PlayerTeamId = "aspar_moto3" });
            var w = game.World;
            var mine = w.PlayerTeam;
            mine.Colors.Primary = "#1E5AA8";
            mine.Colors.Secondary = "#FFD100";
            mine.Colors.Tertiary = "#F4F4F2";
            mine.Colors.Style = 5;
            var obj = JObject.Parse(SaveSystem.Serialize(w));
            // Older saves have no accent colour or livery template: strip them from every other team.
            foreach (var t in ((JObject)obj["Teams"]).Properties())
            {
                if (t.Name == mine.Id) continue;
                var colors = (JObject)t.Value["Colors"];
                colors.Remove("Tertiary");
                colors.Remove("Style");
            }
            var loaded = SaveSystem.Deserialize(obj.ToString());
            var again = loaded.Teams[mine.Id].Colors;
            Assert.Equal("#F4F4F2", again.TertiaryOrDefault());
            Assert.Equal(5, again.StyleFor(mine.Id));
            foreach (var t in loaded.Teams.Values)
            {
                if (t.Id == mine.Id) continue;
                Assert.Equal(-1, t.Colors.Style);
                int style = t.Colors.StyleFor(t.Id);
                Assert.InRange(style, 0, MotoManager.Core.World.TeamColors.AutoStyles - 1);
                Assert.Equal(style, t.Colors.StyleFor(t.Id));
                var accent = t.Colors.TertiaryOrDefault();
                Assert.False(string.IsNullOrEmpty(accent));
                Assert.NotEqual(accent, t.Colors.Primary, StringComparer.OrdinalIgnoreCase);
            }
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
