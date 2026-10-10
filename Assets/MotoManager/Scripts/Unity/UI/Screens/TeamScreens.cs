using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    internal static class Fmt
    {
        /// <summary>Attribute as the team perceives it: a value, or a range while the rider is not well known.</summary>
        public static string Attr(WorldState world, string teamId, Rider r, string name)
        {
            double est, min, max;
            ScoutingService.Perceive(world, teamId, r, name, out est, out min, out max);
            if (max - min < 3) return est.ToString("0");
            return min.ToString("0") + "-" + max.ToString("0");
        }

        public static double Estimate(WorldState world, string teamId, Rider r, string name)
        {
            double est, min, max;
            ScoutingService.Perceive(world, teamId, r, name, out est, out min, out max);
            return est;
        }

        public static string Styles(Rider r)
        {
            if (r.Styles.Count == 0) return "-";
            var names = new List<string>();
            foreach (var s in r.Styles) names.Add(Loc.T("style." + s));
            return string.Join(", ", names.ToArray());
        }

        public static string Signed(double v)
        {
            return (v >= 0 ? "+" : "") + v.ToString("0.#", Loc.Culture);
        }

        public static string Rating(double v)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(Ui.RatingColor(v)) + "><b>" + v.ToString("0") + "</b></color>";
        }

        public static string Component(string c)
        {
            return Loc.T("comp." + c);
        }

        public static string Effects(Dictionary<string, double> effects)
        {
            var parts = new List<string>();
            foreach (var kv in effects)
            {
                string s = Component(kv.Key) + " " + Signed(kv.Value);
                parts.Add(kv.Value < 0 ? Theme.WarnText(s) : s);
            }
            return string.Join(", ", parts.ToArray());
        }
    }

    /// <summary>The team's riders: form and key qualities first, every attribute and the contract on demand.</summary>
    public class RidersPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.riders"); } }
        private NegotiationState _neg;
        private string _confirmRelease;

        private static readonly string[] KeyAttributes = { "RaceSkill", "QualifyingSkill", "Consistency", "WetSkill", "Overtaking", "TireManagement" };

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var date = world.CurrentDate;
            NegotiationPanel.Bids(list, Game, Shell.Refresh);

            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r == null) continue;
                var rider = r;
                var card = Ui.Card(list, "#" + r.Number + "  " + r.FullName,
                    r.Nationality + " · " + Loc.T("rider.age", r.AgeOn(date)) + " · " + Loc.T("rider.style", Fmt.Styles(r)));
                Ui.Kpis(card,
                    Ui.Kpi(Fmt.Rating(r.Attributes.Overall), Loc.T("rider.overall")),
                    Ui.Kpi(Fmt.Rating(r.Morale), Loc.T("rider.morale")),
                    Ui.Kpi(r.Contract != null ? r.Contract.EndSeason.ToString() : "-", Loc.T("rider.until")),
                    Ui.Kpi(r.Contract != null ? MathX.FormatMoney(r.Contract.SalaryPerSeason) : "-", Loc.T("rider.salary")));
                foreach (var a in KeyAttributes)
                {
                    double v = Fmt.Estimate(world, team.Id, r, a);
                    Ui.Bar(card, Loc.T("attr." + a), v / 100.0, Fmt.Attr(world, team.Id, r, a), Ui.RatingColor(v), 34);
                }
                Ui.Expander(card, "rider.attrs." + r.Id, Loc.T("rider.allAttributes"), c =>
                {
                    var names = RiderAttributes.Names;
                    var widths = new[] { 3f, 1f, 3f, 1f, 3f, 1f };
                    for (int i = 0; i < names.Length; i += 3)
                    {
                        var cells = new string[6];
                        for (int k = 0; k < 3 && i + k < names.Length; k++)
                        {
                            cells[k * 2] = Theme.Dim(Loc.T("attr." + names[i + k]));
                            cells[k * 2 + 1] = Fmt.Attr(world, team.Id, rider, names[i + k]);
                        }
                        UiFactory.TableRow(c, 34, widths, cells);
                    }
                    if (!string.IsNullOrEmpty(rider.RealHistory)) Ui.Note(c, rider.RealHistory);
                }, Shell.Refresh);
                Ui.Expander(card, "rider.contract." + r.Id, Loc.T("rider.contract"), c =>
                {
                    UiFactory.Paragraph(c, NegotiationPanel.Terms(rider.Contract) + (rider.FutureContract != null ? "\n" + Theme.Dim(Loc.T("terms.next", rider.FutureContract.EndSeason)) : ""), Theme.FontSmall);
                }, Shell.Refresh);

                var acts = new List<KeyValuePair<string, Action>>();
                if (r.FutureContract == null) acts.Add(UiFactory.Act(Loc.T("team.renew"), () => { _neg = new NegotiationState { RiderId = rider.Id }; Shell.Refresh(); }));
                var option = r.Contract != null ? r.Contract.Clause(ClauseType.TeamOption) : null;
                if (option != null && !option.Exercised && r.Contract.EndSeason == world.Season)
                    acts.Add(UiFactory.Act(Loc.T("team.option"), () => { ContractService.ExerciseOption(world, team, rider); Shell.Refresh(); }));
                double cost = Contracts.TerminationCost(world, Game.Db, r);
                bool confirm = _confirmRelease == r.Id;
                acts.Add(UiFactory.Act(Loc.T(confirm ? "team.releaseConfirm" : "team.release", MathX.FormatMoney(cost)), () =>
                {
                    if (_confirmRelease == rider.Id)
                    {
                        Contracts.Terminate(world, Game.Db, team, rider);
                        _confirmRelease = null;
                    }
                    else _confirmRelease = rider.Id;
                    Shell.Refresh();
                }));
                UiFactory.ButtonRow(card, 58, acts.ToArray());
                if (_neg != null && _neg.RiderId == r.Id)
                {
                    var neg = Ui.Card(list, null, null, Theme.PanelLight);
                    NegotiationPanel.Build(neg, Game, _neg, Shell.Refresh, () => _neg = null);
                }
            }
            if (team.RiderIds.Count < 2)
            {
                var card = Ui.Card(list, Loc.T("rider.freeSeat"));
                UiFactory.ButtonRow(card, 60, UiFactory.Act(Loc.T("rider.toMarket"), () => Shell.Show<MarketScreen>()));
            }
        }
    }

    /// <summary>Staff: who works for the team, contracts, and hiring by role.</summary>
    public class StaffPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.staff"); } }
        private StaffRole? _hiringRole;
        private string _confirmFire;

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;

            var staff = new List<StaffMember>();
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (world.Staff.TryGetValue(id, out s)) staff.Add(s);
            }
            staff.Sort((a, b) => a.Role.CompareTo(b.Role));
            double payroll = 0;
            foreach (var s in staff) payroll += s.Salary;

            var card = Ui.Card(list, Loc.T("staff.title"), Loc.T("staff.summary", staff.Count, MathX.FormatMoney(payroll)));
            foreach (var s in staff)
            {
                var member = s;
                var rider = s.AssignedRiderId != null ? world.GetRider(s.AssignedRiderId) : null;
                bool expiring = s.ContractEndSeason <= world.Season;
                string detail = Loc.T("staff.line", s.Experience, MathX.FormatMoney(s.Salary)) + "  ·  " +
                                (expiring ? Theme.WarnText(Loc.T("staff.until", s.ContractEndSeason)) : Loc.T("staff.until", s.ContractEndSeason)) +
                                (rider != null ? "  ·  " + Loc.T("staff.with", rider.ShortName) : "");
                double severance = StaffMarket.SeveranceCost(world, s);
                bool confirm = _confirmFire == s.Id;
                Ui.Item(card, Loc.T("role." + s.Role) + "  ·  " + s.FullName, detail, Fmt.Rating(s.Skill),
                    UiFactory.Act(Loc.T("staff.renew"), () =>
                    {
                        StaffMarket.Extend(world, Game.Db, team, member, 2);
                        Shell.Toast(Loc.T("staff.renewed", member.FullName, member.ContractEndSeason, MathX.FormatMoney(member.Salary)));
                        Shell.Refresh();
                    }),
                    UiFactory.Act(confirm ? Loc.T("staff.fireConfirm", MathX.FormatMoney(severance)) : Loc.T("staff.fire"), () =>
                    {
                        if (_confirmFire == member.Id)
                        {
                            StaffMarket.Fire(world, team, member.Id);
                            _confirmFire = null;
                            Shell.Toast(Loc.T("staff.released", member.FullName, MathX.FormatMoney(severance)));
                        }
                        else _confirmFire = member.Id;
                        Shell.Refresh();
                    }));
            }
            Ui.Note(card, Loc.T("staff.note"));

            var hire = Ui.Card(list, Loc.T("staff.hire"), Loc.T("staff.hireInfo"));
            var roles = new List<StaffRole>();
            foreach (StaffRole role in Enum.GetValues(typeof(StaffRole))) if (role != StaffRole.TeamManager) roles.Add(role);
            for (int row = 0; row < roles.Count; row += 4)
            {
                var labels = new List<string>();
                int selected = -1;
                for (int i = row; i < roles.Count && i < row + 4; i++)
                {
                    labels.Add(Loc.T("role." + roles[i]));
                    if (_hiringRole == roles[i]) selected = i - row;
                }
                int start = row;
                Ui.Chips(hire, labels, selected, idx =>
                {
                    _hiringRole = roles[start + idx];
                    Shell.Refresh();
                }, 52);
            }
            if (!_hiringRole.HasValue) return;
            Ui.Note(hire, Loc.T("roleinfo." + _hiringRole.Value));
            foreach (var c in StaffMarket.Candidates(world, Game.Db, team, _hiringRole.Value))
            {
                var cand = c;
                Ui.Item(hire, c.FullName + "  " + Theme.Dim(c.Nationality), Loc.T("staff.line", c.Experience, MathX.FormatMoney(c.Salary)), Fmt.Rating(c.Skill),
                    UiFactory.Act(Loc.T("staff.hireButton"), () =>
                    {
                        StaffMarket.Hire(world, team, cand);
                        Shell.Toast(Loc.T("staff.hired", cand.FullName, Loc.T("role." + cand.Role)));
                        Shell.Refresh();
                    }));
            }
        }
    }

    /// <summary>The manager: job, contract, wealth and reputation, plus the season objective.</summary>
    public class ManagerPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.manager"); } }

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            var me = world.PlayerManager;
            if (me != null)
            {
                var card = Ui.Card(list, me.FullName, Loc.T("manager.subtitle"));
                Ui.Kpis(card,
                    Ui.Kpi(Fmt.Rating(me.Reputation), Loc.T("manager.reputation")),
                    Ui.Kpi(MathX.FormatMoney(me.PersonalWealth), Loc.T("manager.wealthShort")),
                    Ui.Kpi(me.Job != null ? me.Job.EndSeason.ToString() : "-", Loc.T("manager.until")));
                if (me.Job != null)
                {
                    var org = world.GetOrganisation(me.Job.OrganisationId);
                    string role = me.Job.Role == ManagerRole.OwnerManager ? Loc.T("manager.owner", org != null ? org.Name : "") :
                        Loc.T("manager.employed", org != null ? org.Name : "", me.Job.EndSeason, MathX.FormatMoney(me.Job.SalaryPerSeason), Loc.T("autonomy." + me.Job.Autonomy));
                    UiFactory.Paragraph(card, role, Theme.FontSmall);
                }
            }
            if (team == null) return;
            var pers = team.Personality;
            if (pers != null)
            {
                int teams = world.TeamsInCategory(team.CategoryId).Count;
                var card = Ui.Card(list, Loc.T("objective.title"));
                Ui.Line(card, Loc.T("objective.current"), Loc.T("objective." + pers.Objective) + "  " + Theme.Dim(Loc.T("objective.target", TeamAI.TargetPosition(pers.Objective, teams))));
                if (pers.LastPosition > 0)
                    Ui.Line(card, Loc.T("objective.last", Loc.T("objective." + pers.LastObjective), pers.LastPosition),
                        pers.LastObjectiveMet ? Theme.GoodText(Loc.T("objective.met")) : Theme.BadText(Loc.T("objective.missed")));
            }
            var orgCard = Ui.Card(list, Loc.T("manager.structure"));
            var o = world.OrganisationOf(team);
            Ui.Line(orgCard, Loc.T("manager.team"), team.Name);
            if (o != null)
            {
                Ui.Line(orgCard, Loc.T("manager.organisation"), o.Name);
                Ui.Line(orgCard, Loc.T("finance.owner"), Loc.T("owner." + o.Owner));
            }
            var cat = Game.Db.GetCategory(team.CategoryId);
            Ui.Line(orgCard, Loc.T("manager.category"), cat != null ? cat.Name : team.CategoryId);
            Ui.Line(orgCard, Loc.T("manager.teamReputation"), Fmt.Rating(team.Reputation));
        }
    }
}
