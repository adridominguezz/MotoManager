using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;

namespace MotoManager.Core.World
{
    public enum ResultStatus
    {
        Finished,
        DNF,
        DNS,
        DSQ
    }

    [Serializable]
    public class ResultEntry
    {
        public string RiderId;
        public string TeamId;
        public string ManufacturerId;
        public int Position;
        public int GridPosition;
        public ResultStatus Status;
        public double TotalTime;
        public double GapToWinner;
        public int Laps;
        public double BestLap;
        public double Points;
        public bool FastestLap;
        public string Note;
    }

    [Serializable]
    public class SessionResult
    {
        public string SessionId;
        public string SessionName;
        public SessionType Type;
        public List<ResultEntry> Classification = new List<ResultEntry>();
        public string WeatherSummary;
        public ulong Seed;
    }

    [Serializable]
    public class WeekendResult
    {
        public int Round;
        public string EventName;
        public string CircuitId;
        public DateTime Date;
        public List<SessionResult> Sessions = new List<SessionResult>();
    }

    [Serializable]
    public class StandingEntry
    {
        public string Id;
        public double Points;
        public int Wins;
        public int Seconds;
        public int Thirds;
        public int Podiums;
        public int Poles;
    }

    [Serializable]
    public class ChampionshipState
    {
        public string CategoryId;
        public int Season;
        public List<WeekendResult> Rounds = new List<WeekendResult>();
        public Dictionary<string, StandingEntry> Riders = new Dictionary<string, StandingEntry>();
        public Dictionary<string, StandingEntry> Teams = new Dictionary<string, StandingEntry>();
        public Dictionary<string, StandingEntry> Manufacturers = new Dictionary<string, StandingEntry>();

        public List<StandingEntry> SortedRiders()
        {
            return Sort(Riders);
        }

        public List<StandingEntry> SortedTeams()
        {
            return Sort(Teams);
        }

        public List<StandingEntry> SortedManufacturers()
        {
            return Sort(Manufacturers);
        }

        public static List<StandingEntry> Sort(Dictionary<string, StandingEntry> table)
        {
            var list = new List<StandingEntry>(table.Values);
            list.Sort((a, b) =>
            {
                int c = b.Points.CompareTo(a.Points);
                if (c != 0) return c;
                c = b.Wins.CompareTo(a.Wins);
                if (c != 0) return c;
                c = b.Seconds.CompareTo(a.Seconds);
                if (c != 0) return c;
                c = b.Thirds.CompareTo(a.Thirds);
                if (c != 0) return c;
                return string.CompareOrdinal(a.Id, b.Id);
            });
            return list;
        }

        public int RiderPosition(string riderId)
        {
            var sorted = SortedRiders();
            for (int i = 0; i < sorted.Count; i++)
                if (sorted[i].Id == riderId) return i + 1;
            return 0;
        }
    }

    [Serializable]
    public class ChampionRecord
    {
        public int Season;
        public string CategoryId;
        public string RiderId;
        public string RiderName;
        public string TeamId;
        public string TeamName;
        public string ManufacturerId;
        public double Points;
        public string ConstructorChampionId;
        public string TeamChampionId;
    }

    [Serializable]
    public class NewsItem
    {
        public DateTime Date;
        public string Headline;
        public string Body;
        public string CategoryId;
        public bool Important;
    }

    public enum ProjectStatus
    {
        InProgress,
        Completed,
        Failed,
        Cancelled
    }

    [Serializable]
    public class DevelopmentProject
    {
        public string Id;
        public string TemplateId;
        public string Name;
        public string TeamId;
        public string BikeId;
        public string Component;
        public Dictionary<string, double> ExpectedEffects = new Dictionary<string, double>();
        public double Cost;
        public int DurationDays;
        public double Risk;
        public string EngineerId;
        public double DaysWorked;
        public ProjectStatus Status;
        public Dictionary<string, double> ActualEffects = new Dictionary<string, double>();
        public DateTime StartDate;
    }

    [Serializable]
    public class ScoutingAssignment
    {
        public string TeamId;
        public string RiderId;
        public string ScoutId;
    }

    /// <summary>The whole persistent universe. Everything needed to continue a save lives here.</summary>
    [Serializable]
    public class WorldState
    {
        public int Version = 1;
        public int Season = 2026;
        public DateTime CurrentDate;
        /// <summary>Date of the last completed event (factory work is measured from here).</summary>
        public DateTime LastEventDate;
        public ulong MasterSeed = 2026;
        public DeterministicRandom Rng = new DeterministicRandom(2026);
        public string PlayerTeamId;
        public int NextId = 1;

        public Dictionary<string, Rider> Riders = new Dictionary<string, Rider>();
        public Dictionary<string, Team> Teams = new Dictionary<string, Team>();
        public Dictionary<string, Manufacturer> Manufacturers = new Dictionary<string, Manufacturer>();
        public Dictionary<string, Motorcycle> Bikes = new Dictionary<string, Motorcycle>();
        public Dictionary<string, StaffMember> Staff = new Dictionary<string, StaffMember>();

        /// <summary>categoryId -> current season championship.</summary>
        public Dictionary<string, ChampionshipState> Championships = new Dictionary<string, ChampionshipState>();
        /// <summary>calendar group -> active calendar for the season (copied and re-dated each year).</summary>
        public Dictionary<string, CalendarDefinition> Calendars = new Dictionary<string, CalendarDefinition>();
        /// <summary>calendar group -> index of the next round to run.</summary>
        public Dictionary<string, int> NextRound = new Dictionary<string, int>();

        public List<ChampionRecord> Champions = new List<ChampionRecord>();
        /// <summary>Archived championships of previous seasons (kept compact).</summary>
        public List<ChampionshipState> ArchivedChampionships = new List<ChampionshipState>();
        public List<NewsItem> News = new List<NewsItem>();
        public List<DevelopmentProject> Projects = new List<DevelopmentProject>();
        public List<ScoutingAssignment> Scouting = new List<ScoutingAssignment>();
        /// <summary>teamId -> (riderId -> knowledge 0..1). Controls how precisely attributes are displayed.</summary>
        public Dictionary<string, Dictionary<string, double>> ScoutingKnowledge = new Dictionary<string, Dictionary<string, double>>();
        public List<string> FreeAgentPool = new List<string>();
        public Dictionary<string, Organisation> Organisations = new Dictionary<string, Organisation>();
        public FinanceState Finance = new FinanceState();

        public string NewId(string prefix)
        {
            return prefix + "_" + (NextId++).ToString("D5");
        }

        public Rider GetRider(string id)
        {
            Rider r;
            return id != null && Riders.TryGetValue(id, out r) ? r : null;
        }

        public Team GetTeam(string id)
        {
            Team t;
            return id != null && Teams.TryGetValue(id, out t) ? t : null;
        }

        public Motorcycle GetBike(string id)
        {
            Motorcycle b;
            return id != null && Bikes.TryGetValue(id, out b) ? b : null;
        }

        public Manufacturer GetManufacturer(string id)
        {
            Manufacturer m;
            return id != null && Manufacturers.TryGetValue(id, out m) ? m : null;
        }

        public Team PlayerTeam { get { return GetTeam(PlayerTeamId); } }

        public Organisation GetOrganisation(string id)
        {
            Organisation o;
            return id != null && Organisations.TryGetValue(id, out o) ? o : null;
        }

        public Organisation OrganisationOf(Team team)
        {
            return team != null ? GetOrganisation(team.OrganisationId) : null;
        }

        public List<Team> TeamsInCategory(string categoryId)
        {
            var list = new List<Team>();
            foreach (var t in Teams.Values)
                if (t.CategoryId == categoryId) list.Add(t);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        public List<Rider> ActiveRidersInCategory(string categoryId)
        {
            var list = new List<Rider>();
            foreach (var r in Riders.Values)
                if (!r.IsRetired && r.CategoryId == categoryId && r.TeamId != null) list.Add(r);
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return list;
        }

        public void AddNews(string headline, string body, string categoryId = null, bool important = false)
        {
            News.Insert(0, new NewsItem { Date = CurrentDate, Headline = headline, Body = body, CategoryId = categoryId, Important = important });
            if (News.Count > 300) News.RemoveRange(300, News.Count - 300);
        }

        public double GetKnowledge(string teamId, string riderId)
        {
            Dictionary<string, double> map;
            double k;
            if (teamId != null && ScoutingKnowledge.TryGetValue(teamId, out map) && map.TryGetValue(riderId, out k)) return k;
            return 0;
        }

        public void SetKnowledge(string teamId, string riderId, double value)
        {
            Dictionary<string, double> map;
            if (!ScoutingKnowledge.TryGetValue(teamId, out map))
            {
                map = new Dictionary<string, double>();
                ScoutingKnowledge[teamId] = map;
            }
            map[riderId] = MathX.Clamp01(value);
        }
    }
}
