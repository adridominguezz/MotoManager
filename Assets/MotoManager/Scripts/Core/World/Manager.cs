using System;
using System.Collections.Generic;

namespace MotoManager.Core.World
{
    /// <summary>How much a manager may decide without asking the board (only for employed managers).</summary>
    public enum Autonomy
    {
        /// <summary>Signings and spending above a threshold need the board's approval.</summary>
        Limited,
        /// <summary>Free within the assigned budget.</summary>
        Standard,
        /// <summary>Free, may move money between areas and ask for more.</summary>
        Full
    }

    public enum ManagerRole
    {
        TeamManager,
        /// <summary>Owner of the organisation who also runs the team.</summary>
        OwnerManager
    }

    /// <summary>The manager's employment: separate from the organisation's money and from rider contracts.</summary>
    [Serializable]
    public class ManagerContract
    {
        public string OrganisationId;
        public string TeamId;
        public ManagerRole Role = ManagerRole.TeamManager;
        public int StartSeason;
        public int EndSeason;
        /// <summary>Paid by the organisation to the manager's personal wealth (0 for owner-managers).</summary>
        public double SalaryPerSeason;
        public Autonomy Autonomy = Autonomy.Standard;
        /// <summary>Budget placed under the manager's responsibility when the contract was signed.</summary>
        public double AssignedBudget;
        /// <summary>0-100: the board's confidence in the manager.</summary>
        public double BoardConfidence = 60;
        /// <summary>Paid if the employer ends the contract early (seasons of salary).</summary>
        public double SeveranceSeasons = 0.5;
        /// <summary>Season in which the contract was last renewed (0 = never).</summary>
        public int RenewedSeason;
    }

    /// <summary>One period of a manager's career, kept forever.</summary>
    [Serializable]
    public class ManagerStint
    {
        public string OrganisationId;
        public string TeamId;
        public string TeamName;
        public string CategoryId;
        public ManagerRole Role;
        public int FromSeason;
        public int ToSeason;
        public string EndReason;
    }

    /// <summary>
    /// A team manager (the player or an AI manager). Independent of teams and organisations: careers move between them.
    /// Skills and reputation shape opportunities and negotiations, never guarantee results.
    /// </summary>
    [Serializable]
    public class ManagerProfile
    {
        public string Id;
        public string FirstName;
        public string LastName;
        public string Nationality;
        public string BackgroundId;
        public bool IsPlayer;
        public int BirthYear = 1985;
        public double Reputation = 30;
        public double Negotiation = 50;
        public double SportingManagement = 50;
        public double Finance = 50;
        public double Relations = 50;
        public double Commercial = 50;
        /// <summary>The manager's own money (salary saved, capital). Only this can be invested in a new organisation.</summary>
        public double PersonalWealth;
        public ManagerContract Job;
        public List<ManagerStint> History = new List<ManagerStint>();
        /// <summary>Standing with each organisation (organisation id, -100 to 100): a broken contract or a sacking is remembered.</summary>
        public Dictionary<string, double> OrgRelations = new Dictionary<string, double>();
        /// <summary>No longer looking for work (AI managers only).</summary>
        public bool Retired;
        public string DataConfidence = "generated";

        public string FullName { get { return FirstName + " " + LastName; } }

        public double RelationWith(string organisationId)
        {
            double v;
            return organisationId != null && OrgRelations.TryGetValue(organisationId, out v) ? v : 0;
        }

        public void ChangeRelation(string organisationId, double delta)
        {
            if (organisationId == null) return;
            double v = RelationWith(organisationId) + delta;
            OrgRelations[organisationId] = Math.Max(-100, Math.Min(100, Math.Round(v, 1)));
        }
    }

    /// <summary>Starting profiles a player can choose (data: managers/backgrounds.json).</summary>
    [Serializable]
    public class ManagerBackground
    {
        public string Id;
        public double Reputation;
        public double Negotiation;
        public double SportingManagement;
        public double Finance;
        public double Relations;
        public double Commercial;
        /// <summary>Personal capital at the start.</summary>
        public double PersonalWealth;
        public string DataConfidence;
    }

    /// <summary>A simple loan: fixed rate, repaid in equal instalments at each event.</summary>
    [Serializable]
    public class Loan
    {
        public string Id;
        public double Principal;
        public double Outstanding;
        /// <summary>Yearly interest rate (0.07 = 7%).</summary>
        public double Rate;
        public int Seasons;
        public int StartSeason;
    }

    /// <summary>A team that would hire the player as its manager.</summary>
    public class JobOffer
    {
        public string TeamId;
        public string OrganisationId;
        public string CategoryId;
        public double SalaryPerSeason;
        public int Seasons;
        public Autonomy Autonomy;
        public double AssignedBudget;
        public TeamObjective Objective;
    }
}
