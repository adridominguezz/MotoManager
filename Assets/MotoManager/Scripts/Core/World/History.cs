using System;
using System.Collections.Generic;

namespace MotoManager.Core.World
{
    /// <summary>One championship's season as history remembers it (compact: names are copied, decades stay small).</summary>
    [Serializable]
    public class SeasonHistory
    {
        public int Season;
        public string CategoryId;
        public string ChampionId, Champion;
        public string SecondId, Second;
        public string ThirdId, Third;
        public double ChampionPoints, SecondPoints;
        public string TeamChampion;
        public string MakerChampionId;
        public string RookieId, Rookie;
        public int RookiePosition;
        public string MostWins;
        public int MostWinsCount;
        public int Races;
        /// <summary>Winners who retired at the end of this season.</summary>
        public List<string> Retired = new List<string>();
    }

    /// <summary>A record: who, when and the value (age in years, number of wins in a row...).</summary>
    [Serializable]
    public class HistoryRecord
    {
        public string RiderId;
        public string Name;
        public int Season;
        public double Value;
        public string Event;
    }

    /// <summary>Records and running counters of one championship.</summary>
    [Serializable]
    public class CategoryRecords
    {
        public HistoryRecord YoungestWinner;
        public HistoryRecord OldestWinner;
        public HistoryRecord LongestStreak;
        /// <summary>Current run of consecutive main-race wins.</summary>
        public string StreakRiderId;
        public int StreakCount;
        /// <summary>Times two riders finished first and second ("idA|idB", ids sorted).</summary>
        public Dictionary<string, int> Duels = new Dictionary<string, int>();
    }
}
