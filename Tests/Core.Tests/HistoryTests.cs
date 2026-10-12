using System.Linq;
using MotoManager.Core.Season;
using Xunit;

namespace MotoManager.Tests
{
    public class HistoryTests
    {
        [Fact]
        public void ASeasonLeavesItsPodiumRecordsAndRivalries()
        {
            var game = GameController.NewGame(TestData.Db, new NewGameOptions { Seed = 403 });
            var w = game.World;
            game.SimulateRestOfSeason();
            game.EndSeason();
            var gp = w.History.FirstOrDefault(h => h.CategoryId == "motogp");
            Assert.NotNull(gp);
            Assert.NotNull(gp.Champion);
            Assert.NotNull(gp.Second);
            Assert.True(gp.ChampionPoints >= gp.SecondPoints);
            Assert.NotNull(gp.Rookie);
            var rec = HistoryService.Records(w, "motogp");
            Assert.NotNull(rec.YoungestWinner);
            Assert.True(rec.YoungestWinner.Value <= rec.OldestWinner.Value);
            Assert.True(rec.LongestStreak.Value >= 1);
            Assert.NotEmpty(HistoryService.Rivalries(w, "motogp"));
            var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(w));
            Assert.Equal(w.History.Count, loaded.History.Count);
            Assert.Equal(rec.YoungestWinner.Name, loaded.Records["motogp"].YoungestWinner.Name);
        }
    }
}
