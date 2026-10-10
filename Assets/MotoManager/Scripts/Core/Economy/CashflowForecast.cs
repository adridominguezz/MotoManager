using System;
using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.World;

namespace MotoManager.Core.Economy
{
    public class CashflowPoint
    {
        public DateTime Date;
        public string Label;
        /// <summary>Cash counting only confirmed income (signed deals, participation) and all expected costs.</summary>
        public double Confirmed;
        /// <summary>Cash also counting probable income (expected prize money, sponsor bonuses, sponsors likely to sign).</summary>
        public double Probable;
    }

    /// <summary>
    /// Projection of an organisation's cash through the remaining events of the season. Costs are always counted;
    /// income is split into confirmed and probable so the player can see what the plan depends on. Built from the
    /// same item list that pays real events (<see cref="FinanceService.RoundItems"/>), so forecast and reality agree.
    /// </summary>
    public class CashflowForecast
    {
        public double StartCash;
        public readonly List<CashflowPoint> Points = new List<CashflowPoint>();
        public double ConfirmedIncome;
        public double ProbableIncome;
        public double Costs;

        public double MinConfirmed { get { return Min(true); } }
        public double MinProbable { get { return Min(false); } }
        public double EndConfirmed { get { return Points.Count > 0 ? Points[Points.Count - 1].Confirmed : StartCash; } }
        public double EndProbable { get { return Points.Count > 0 ? Points[Points.Count - 1].Probable : StartCash; } }

        private double Min(bool confirmed)
        {
            double m = StartCash;
            foreach (var p in Points) m = Math.Min(m, confirmed ? p.Confirmed : p.Probable);
            return m;
        }

        /// <summary>One dated movement before it is folded into the running balances.</summary>
        public class Flow
        {
            public DateTime Date;
            public string Label;
            public double Confirmed;
            public double Probable;
            public double Cost;
        }

        public static CashflowForecast Build(double startCash, List<Flow> flows)
        {
            var f = new CashflowForecast { StartCash = startCash };
            flows.Sort((a, b) =>
            {
                int c = a.Date.CompareTo(b.Date);
                return c != 0 ? c : string.CompareOrdinal(a.Label, b.Label);
            });
            double confirmed = startCash, probable = startCash;
            foreach (var fl in flows)
            {
                confirmed += fl.Confirmed - fl.Cost;
                probable += fl.Confirmed + fl.Probable - fl.Cost;
                f.ConfirmedIncome += fl.Confirmed;
                f.ProbableIncome += fl.Probable;
                f.Costs += fl.Cost;
                f.Points.Add(new CashflowPoint { Date = fl.Date, Label = fl.Label, Confirmed = confirmed, Probable = probable });
            }
            return f;
        }

        /// <summary>
        /// Flows of one team (real or planned) over a list of event dates: regular items, expected crash repairs
        /// (costs) and expected prize money and sponsor bonuses (probable income).
        /// </summary>
        public static List<Flow> TeamFlows(CostProfile p, CategoryRules rules, List<CalendarRound> rounds, int roundsInSeason,
            double expectedPosition, double probableSponsorPerSeason, string label)
        {
            var flows = new List<Flow>();
            double probablePerRound = FinanceService.ExpectedPrizePerEvent(rules, expectedPosition, p.Riders) + probableSponsorPerSeason / Math.Max(1, roundsInSeason);
            double crash = rules.Economy.CrashRepairCost * rules.Economy.ExpectedCrashesPerRiderEvent * p.Riders;
            foreach (var round in rounds)
            {
                var fl = new Flow { Date = round.Date, Label = label + " · " + round.Name, Probable = probablePerRound, Cost = crash };
                foreach (var kv in FinanceService.RoundItems(p, rules, roundsInSeason))
                {
                    if (kv.Value >= 0) fl.Confirmed += kv.Value;
                    else fl.Cost -= kv.Value;
                }
                flows.Add(fl);
            }
            return flows;
        }

        /// <summary>Events of a category still to be run this season.</summary>
        public static List<CalendarRound> RemainingRounds(WorldState world, GameDatabase db, string categoryId)
        {
            var list = new List<CalendarRound>();
            var cat = db.GetCategory(categoryId);
            CalendarDefinition cal;
            if (cat == null || !world.Calendars.TryGetValue(cat.CalendarGroup ?? "", out cal)) return list;
            int next;
            world.NextRound.TryGetValue(cat.CalendarGroup, out next);
            for (int i = next; i < cal.Rounds.Count; i++)
            {
                var r = cal.Rounds[i];
                if (r.Categories == null || r.Categories.Count == 0 || r.Categories.Contains(categoryId)) list.Add(r);
            }
            return list;
        }

        /// <summary>All rounds of a category in the current calendar (used to plan a season that has not started).</summary>
        public static List<CalendarRound> AllRounds(WorldState world, GameDatabase db, string categoryId)
        {
            var list = new List<CalendarRound>();
            var cat = db.GetCategory(categoryId);
            CalendarDefinition cal;
            if (cat == null || !world.Calendars.TryGetValue(cat.CalendarGroup ?? "", out cal)) return list;
            foreach (var r in cal.Rounds)
                if (r.Categories == null || r.Categories.Count == 0 || r.Categories.Contains(categoryId)) list.Add(r);
            return list;
        }

        /// <summary>Rest-of-season projection for an organisation and all its teams.</summary>
        public static CashflowForecast ForOrganisation(WorldState world, GameDatabase db, Organisation org)
        {
            var flows = new List<Flow>();
            foreach (var id in org.TeamIds)
            {
                var team = world.GetTeam(id);
                var cat = team != null ? db.GetCategory(team.CategoryId) : null;
                if (cat == null || !cat.Simulated) continue;
                var rules = db.GetRules(cat.Id, world.Season);
                int total = Math.Max(1, Season.GameController.RoundsInSeason(world, db, cat.Id));
                flows.AddRange(TeamFlows(CostProfile.Of(world, team), rules, RemainingRounds(world, db, cat.Id), total,
                    ExpectedPosition(world, team), ExpectedSponsorBonuses(team), team.ShortName ?? team.Name));
            }
            return Build(org.Cash, flows);
        }

        /// <summary>Expected finishing position of a team's riders, from its expected strength rank in the category.</summary>
        public static double ExpectedPosition(WorldState world, Team team)
        {
            var teams = world.TeamsInCategory(team.CategoryId);
            double mine = Career.TeamAI.ExpectedStrength(world, team);
            int better = 0;
            foreach (var t in teams)
                if (t != team && Career.TeamAI.ExpectedStrength(world, t) > mine) better++;
            int riders = Math.Max(1, team.RiderIds.Count);
            return 1 + better * riders + (riders - 1) / 2.0;
        }

        /// <summary>Half of the season bonuses the team's sponsors offer: earned only if objectives are met.</summary>
        public static double ExpectedSponsorBonuses(Team team)
        {
            double total = 0;
            foreach (var d in team.Sponsors)
                if (d.RequiredCategoryId == null || d.RequiredCategoryId == team.CategoryId)
                    foreach (var o in d.Objectives) total += o.SeasonBonus * 0.5;
            return total;
        }
    }
}
