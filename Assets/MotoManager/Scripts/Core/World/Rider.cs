using System;
using System.Collections.Generic;
using MotoManager.Core.Data;

namespace MotoManager.Core.World
{
    /// <summary>All rider skills on a 0-100 scale. Not all of them are shown to the player (see scouting).</summary>
    [Serializable]
    public class RiderAttributes
    {
        public double RaceSkill = 50;
        public double QualifyingSkill = 50;
        public double Consistency = 50;
        public double WetSkill = 50;
        public double Overtaking = 50;
        public double Defending = 50;
        public double TireManagement = 50;
        public double Braking = 50;
        public double Cornering = 50;
        public double ThrottleControl = 50;
        public double Adaptability = 50;
        public double Fitness = 50;
        public double Experience = 50;
        public double Aggression = 50;
        public double RiskManagement = 50;
        public double Mentality = 50;
        public double TechnicalFeedback = 50;
        public double Starts = 50;

        public static readonly string[] Names =
        {
            "RaceSkill", "QualifyingSkill", "Consistency", "WetSkill", "Overtaking", "Defending", "TireManagement",
            "Braking", "Cornering", "ThrottleControl", "Adaptability", "Fitness", "Experience", "Aggression",
            "RiskManagement", "Mentality", "TechnicalFeedback", "Starts"
        };

        /// <summary>Aggression is a trait rather than a quality, so it is excluded from the overall rating.</summary>
        public double Overall
        {
            get
            {
                return RaceSkill * 0.18 + QualifyingSkill * 0.08 + Consistency * 0.09 + WetSkill * 0.04 + Overtaking * 0.07 +
                       Defending * 0.05 + TireManagement * 0.07 + Braking * 0.09 + Cornering * 0.09 + ThrottleControl * 0.08 +
                       Adaptability * 0.03 + Fitness * 0.04 + Experience * 0.03 + RiskManagement * 0.03 + Mentality * 0.03;
            }
        }

        public double Get(string name)
        {
            switch (name)
            {
                case "RaceSkill": return RaceSkill;
                case "QualifyingSkill": return QualifyingSkill;
                case "Consistency": return Consistency;
                case "WetSkill": return WetSkill;
                case "Overtaking": return Overtaking;
                case "Defending": return Defending;
                case "TireManagement": return TireManagement;
                case "Braking": return Braking;
                case "Cornering": return Cornering;
                case "ThrottleControl": return ThrottleControl;
                case "Adaptability": return Adaptability;
                case "Fitness": return Fitness;
                case "Experience": return Experience;
                case "Aggression": return Aggression;
                case "RiskManagement": return RiskManagement;
                case "Mentality": return Mentality;
                case "TechnicalFeedback": return TechnicalFeedback;
                case "Starts": return Starts;
            }
            return 0;
        }

        public void Set(string name, double value)
        {
            value = Math.Max(1, Math.Min(100, value));
            switch (name)
            {
                case "RaceSkill": RaceSkill = value; break;
                case "QualifyingSkill": QualifyingSkill = value; break;
                case "Consistency": Consistency = value; break;
                case "WetSkill": WetSkill = value; break;
                case "Overtaking": Overtaking = value; break;
                case "Defending": Defending = value; break;
                case "TireManagement": TireManagement = value; break;
                case "Braking": Braking = value; break;
                case "Cornering": Cornering = value; break;
                case "ThrottleControl": ThrottleControl = value; break;
                case "Adaptability": Adaptability = value; break;
                case "Fitness": Fitness = value; break;
                case "Experience": Experience = value; break;
                case "Aggression": Aggression = value; break;
                case "RiskManagement": RiskManagement = value; break;
                case "Mentality": Mentality = value; break;
                case "TechnicalFeedback": TechnicalFeedback = value; break;
                case "Starts": Starts = value; break;
            }
        }

        /// <summary>How the player learns about each attribute. Hidden ones are only revealed by scouting / working with the rider.</summary>
        public static AttributeVisibility GetVisibility(string name)
        {
            switch (name)
            {
                case "RaceSkill":
                case "QualifyingSkill":
                case "Overtaking":
                case "Braking":
                case "Cornering":
                case "WetSkill":
                case "Experience":
                    return AttributeVisibility.Visible;
                case "Consistency":
                case "TireManagement":
                case "Defending":
                case "ThrottleControl":
                case "Fitness":
                case "Starts":
                    return AttributeVisibility.Partial;
                default:
                    return AttributeVisibility.Hidden;
            }
        }

        public RiderAttributes Clone()
        {
            return (RiderAttributes)MemberwiseClone();
        }
    }

    [Serializable]
    public class Personality
    {
        public double Ambition = 50;
        public double Loyalty = 50;
        public double Temperament = 50;
        public double Motivation = 70;
        /// <summary>Preference for the Grand Prix ladder (100) versus Superbikes (0).</summary>
        public double GrandPrixPreference = 60;
    }

    /// <summary>A bonus tier: paid when a result is at position <see cref="MaxPosition"/> or better.</summary>
    [Serializable]
    public class PositionBonus
    {
        public int MaxPosition;
        public double Amount;
    }

    public enum ClauseType
    {
        /// <summary>Any team may free the rider by paying <see cref="ContractClause.Amount"/> to the current team.</summary>
        ReleaseClause,
        /// <summary>Automatic extension when a season objective is met in the contract's last season.</summary>
        PerformanceExtension,
        /// <summary>The team may extend the contract before it expires (decided at the end of the last season).</summary>
        TeamOption,
        /// <summary>The rider may extend the contract before it expires.</summary>
        RiderOption,
        /// <summary>The rider may leave without a fee for a team in a more prestigious category.</summary>
        ExitForHigherCategory,
        /// <summary>The rider may leave at the end of a season in which the team missed its objective.</summary>
        ExitIfTeamMissesObjective
    }

    /// <summary>A contract clause with real effects (evaluated by the contract rules, never just text).</summary>
    [Serializable]
    public class ContractClause
    {
        public ClauseType Type;
        /// <summary>Release clause price.</summary>
        public double Amount;
        /// <summary>Seasons added by an extension or option.</summary>
        public int ExtraSeasons = 1;
        /// <summary>Salary of the added seasons (0 = keep the current salary).</summary>
        public double Salary;
        /// <summary>Performance extension: championship position needed (the rider's position).</summary>
        public int ChampionshipTopN;
        /// <summary>Set when the team has exercised its option during the last season.</summary>
        public bool Exercised;
    }

    [Serializable]
    public class Contract
    {
        public string Id;
        public string TeamId;
        public string ManufacturerId;
        public int StartSeason;
        public int EndSeason;
        public double SalaryPerSeason;
        /// <summary>Paid once when the contract is signed.</summary>
        public double SigningBonus;
        /// <summary>Per race (main races): only the best matching tier is paid (a win does not also pay the podium tier).</summary>
        public List<PositionBonus> RaceBonuses = new List<PositionBonus>();
        /// <summary>At season end by championship position: only the best matching tier.</summary>
        public List<PositionBonus> ChampionshipBonuses = new List<PositionBonus>();
        /// <summary>At season end, per championship point.</summary>
        public double PointBonus;
        public double PoleBonus;
        public List<ContractClause> Clauses = new List<ContractClause>();
        /// <summary>Share of the salary of the remaining full seasons owed if the team ends the contract early.</summary>
        public double TerminationShare = 0.5;
        public bool FactoryBacked;
        /// <summary>Money the rider's backers pay the team per season while the contract runs.</summary>
        public double RiderFunding;
        /// <summary>Set when the contract was cut short by an agreed transfer (recorded when it ends).</summary>
        public bool EndsByTransfer;

        public ContractClause Clause(ClauseType type)
        {
            foreach (var c in Clauses) if (c.Type == type) return c;
            return null;
        }

        public double RaceBonusFor(int position)
        {
            double best = 0;
            foreach (var b in RaceBonuses) if (position <= b.MaxPosition && b.Amount > best) best = b.Amount;
            return best;
        }

        public double ChampionshipBonusFor(int position)
        {
            double best = 0;
            foreach (var b in ChampionshipBonuses) if (position > 0 && position <= b.MaxPosition && b.Amount > best) best = b.Amount;
            return best;
        }
    }

    /// <summary>A contract offer under negotiation (not stored: it becomes a Contract when signed).</summary>
    public class ContractProposal
    {
        public double Salary;
        public int Seasons = 2;
        /// <summary>The rider joins now (this season) instead of next season.</summary>
        public bool StartNow;
        public double SigningBonus;
        public double WinBonus;
        public double PodiumBonus;
        /// <summary>0 = no release clause.</summary>
        public double ReleaseClause;
        public bool TeamOption;
        /// <summary>Championship position that triggers an automatic one-season extension (0 = none).</summary>
        public int PerformanceExtensionTopN;
        public bool ExitForHigherCategory;
    }

    public enum TransferStatus
    {
        Pending,
        Accepted,
        Rejected
    }

    /// <summary>One team's offer to another for a rider under contract.</summary>
    [Serializable]
    public class TransferOffer
    {
        public string Id;
        public string RiderId;
        public string FromTeamId;
        public string ToTeamId;
        public double Fee;
        /// <summary>The rider would move now rather than next season.</summary>
        public bool StartNow;
        public TransferStatus Status;
        public int ExpiresAfterEvent;
    }

    /// <summary>Clubs agreed a fee: the buyer may now sign the rider (who still has to agree personal terms).</summary>
    [Serializable]
    public class TransferAgreement
    {
        public string RiderId;
        public string BuyerTeamId;
        public double Fee;
        public bool StartNow;
        public int ExpiresAfterEvent;
    }

    public enum ContractEnd
    {
        Expired,
        Renewed,
        Extended,
        OptionExercised,
        Released,
        ReleaseClause,
        Transfer,
        ExitClause,
        Retired,
        TeamWithdrew
    }

    /// <summary>One contract in a rider's history, kept forever.</summary>
    [Serializable]
    public class ContractRecord
    {
        public string TeamId;
        public string TeamName;
        public string CategoryId;
        public int StartSeason;
        public int EndSeason;
        public double SalaryPerSeason;
        public ContractEnd Reason;
        /// <summary>Transfer fee, release clause or severance involved (0 if none).</summary>
        public double Fee;
    }

    /// <summary>One season of a rider's career, kept forever.</summary>
    [Serializable]
    public class SeasonRecord
    {
        public int Season;
        public string CategoryId;
        public string TeamId;
        public string TeamName;
        public string ManufacturerId;
        public string TeammateId;
        public int Starts;
        public int Wins;
        public int Podiums;
        public int Poles;
        public int FastestLaps;
        public int SprintWins;
        public int Dnfs;
        public int Crashes;
        public double Points;
        public int ChampionshipPosition;
        public int BestFinish = 99;
        public double Salary;
    }

    [Serializable]
    public class Rider
    {
        public string Id;
        public string FirstName;
        public string LastName;
        public int Number;
        public DateTime DateOfBirth;
        public string Nationality;
        public string CategoryId;
        public string TeamId;
        public RiderAttributes Attributes = new RiderAttributes();
        /// <summary>Hidden ceiling for the overall rating (0-100).</summary>
        public double Potential = 60;
        /// <summary>Age at which the rider will peak (hidden). Some peak early, some late.</summary>
        public double PeakAge = 28;
        public double Reputation = 30;
        public double Morale = 60;
        public List<RidingStyle> Styles = new List<RidingStyle>();
        public Personality Personality = new Personality();
        public Contract Contract;
        /// <summary>Signed deal that starts next season (applied by the market at season end).</summary>
        public Contract FutureContract;
        public List<SeasonRecord> CareerHistory = new List<SeasonRecord>();
        public List<ContractRecord> ContractHistory = new List<ContractRecord>();
        public List<string> Titles = new List<string>();
        /// <summary>Free text summary of real-world history before the snapshot.</summary>
        public string RealHistory;
        public int InjuryRoundsOut;
        public bool IsRetired;
        public int RetiredSeason;
        public bool Generated;
        /// <summary>
        /// Money per season the rider's personal sponsors / family put into the team that gives him a seat (0 = none).
        /// Only invented riders have it: real riders' private deals are not public.
        /// </summary>
        public double PersonalBacking;
        public string DataConfidence = "approximate";
        /// <summary>Seasons without a seat (unattached).</summary>
        public int SeasonsWithoutSeat;

        public string FullName { get { return FirstName + " " + LastName; } }

        public string ShortName
        {
            get { return (string.IsNullOrEmpty(FirstName) ? "" : FirstName.Substring(0, 1) + ". ") + LastName; }
        }

        public string Code
        {
            get
            {
                string ln = (LastName ?? "XXX").Replace(" ", "").ToUpperInvariant();
                return ln.Length >= 3 ? ln.Substring(0, 3) : ln.PadRight(3, 'X');
            }
        }

        public RidingStyle StyleFlags
        {
            get
            {
                RidingStyle s = RidingStyle.None;
                for (int i = 0; i < Styles.Count; i++) s |= Styles[i];
                return s;
            }
        }

        public bool HasStyle(RidingStyle style)
        {
            return (StyleFlags & style) != 0;
        }

        public int AgeOn(DateTime date)
        {
            int age = date.Year - DateOfBirth.Year;
            if (date.Month < DateOfBirth.Month || (date.Month == DateOfBirth.Month && date.Day < DateOfBirth.Day)) age--;
            return age;
        }

        public SeasonRecord GetOrCreateRecord(int season, string categoryId, string teamId)
        {
            for (int i = CareerHistory.Count - 1; i >= 0; i--)
            {
                var r = CareerHistory[i];
                if (r.Season == season && r.CategoryId == categoryId && r.TeamId == teamId) return r;
            }
            var rec = new SeasonRecord { Season = season, CategoryId = categoryId, TeamId = teamId };
            CareerHistory.Add(rec);
            return rec;
        }

        public int TotalWins()
        {
            int w = 0;
            for (int i = 0; i < CareerHistory.Count; i++) w += CareerHistory[i].Wins;
            return w;
        }

        public int TotalPodiums()
        {
            int w = 0;
            for (int i = 0; i < CareerHistory.Count; i++) w += CareerHistory[i].Podiums;
            return w;
        }
    }
}
