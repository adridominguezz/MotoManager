using System;

namespace MotoManager.Core.World
{
    /// <summary>What a team (its owners / board / manufacturer) expects from a season.</summary>
    public enum TeamObjective
    {
        Title,
        Wins,
        Podiums,
        TopHalf,
        Points,
        /// <summary>Bring young riders on rather than chase results.</summary>
        DevelopRiders
    }

    /// <summary>How a team builds its project: who it signs and how far ahead it looks.</summary>
    public enum TeamPhilosophy
    {
        YoungTalent,
        Experience,
        ManufacturerLoyal,
        ImmediateResults,
        LongTerm
    }

    /// <summary>
    /// Character of a team, used by the AI in the rider market, bike development, budget use and race strategy.
    /// Values are 0-100. The player's team has one too (it drives the defaults of riders left on auto).
    /// </summary>
    [Serializable]
    public class TeamPersonality
    {
        public TeamObjective Objective = TeamObjective.Points;
        public TeamPhilosophy Philosophy = TeamPhilosophy.Experience;
        /// <summary>In-house engineering capability (how much the team adds to the machinery it receives).</summary>
        public double TechnicalLevel = 50;
        /// <summary>How hard and how often it pushes development during the season.</summary>
        public double DevelopmentRate = 50;
        /// <summary>Technical and parts support from the manufacturer (factory ~95, satellite ~60, private ~35).</summary>
        public double ManufacturerSupport = 50;
        /// <summary>Appetite for risk: expensive or unproven signings, aggressive race calls, softer tyres.</summary>
        public double RiskTolerance = 50;
        /// <summary>The character the team returns to when not under pressure.</summary>
        public double BaseRiskTolerance = 50;

        /// <summary>Result of the last evaluated season (team championship position, 0 = none).</summary>
        public int LastPosition;
        public TeamObjective LastObjective = TeamObjective.Points;
        public bool LastObjectiveMet = true;
        /// <summary>Consecutive seasons below expectations.</summary>
        public int SeasonsMissed;
    }
}
