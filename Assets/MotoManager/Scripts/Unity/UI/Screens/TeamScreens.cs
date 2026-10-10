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

    /// <summary>
    /// The team's riders, one at a time (a selector on top, nothing hidden below): form and key qualities on the
    /// left, every attribute and the contract on the right. Bids from other teams have their own tab; a
    /// negotiation takes the whole page.
    /// </summary>
    public class RidersPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.riders"); } }
        private NegotiationState _neg;
        private string _confirmRelease;
        private int _view;

        private static readonly string[] KeyAttributes = { "RaceSkill", "QualifyingSkill", "Consistency", "WetSkill", "Overtaking", "TireManagement" };

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            if (_neg != null)
            {
                NegotiationPanel.Build(list, Game, _neg, Shell.Refresh, () => _neg = null);
                return;
            }

            var riders = new List<Rider>();
            foreach (var id in team.RiderIds) { var r = world.GetRider(id); if (r != null) riders.Add(r); }
            int bids = world.TransferOffers.FindAll(o => o.FromTeamId == team.Id).Count;
            var labels = new List<string>();
            foreach (var r in riders) labels.Add("#" + r.Number + " " + r.ShortName);
            if (riders.Count < 2) labels.Add(Loc.T("rider.freeSeatShort"));
            if (bids > 0) labels.Add(Loc.T("rider.bids", bids));
            _view = Mathf.Clamp(_view, 0, labels.Count - 1);
            Ui.Chips(list, labels, _view, i => { _view = i; _confirmRelease = null; Shell.Refresh(); });

            if (_view < riders.Count) RiderView(list, world, team, riders[_view]);
            else if (riders.Count < 2 && _view == riders.Count)
            {
                var card = Ui.Card(list, Loc.T("rider.freeSeat"), Loc.T("rider.freeSeatInfo"));
                UiFactory.ButtonRow(card, 64, UiFactory.Primary(Loc.T("rider.toMarket"), () => Shell.Show<MarketScreen>()));
            }
            else NegotiationPanel.Bids(list, Game, Shell.Refresh);
        }

        private void RiderView(RectTransform list, WorldState world, Team team, Rider r)
        {
            var date = world.CurrentDate;
            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);

            var card = Ui.Card(leftCol, r.FullName, r.Nationality + " · " + Loc.T("rider.age", r.AgeOn(date)) + " · " + Loc.T("rider.style", Fmt.Styles(r)));
            Ui.Kpis(card,
                Ui.Kpi(Fmt.Rating(r.Attributes.Overall), Loc.T("rider.overall")),
                Ui.Kpi(Fmt.Rating(r.Morale), Loc.T("rider.morale")),
                Ui.Kpi(r.Contract != null ? r.Contract.EndSeason.ToString() : "-", Loc.T("rider.until")));
            foreach (var a in KeyAttributes)
            {
                double v = Fmt.Estimate(world, team.Id, r, a);
                Ui.Bar(card, Loc.T("attr." + a), v / 100.0, Fmt.Attr(world, team.Id, r, a), Ui.RatingColor(v), 34);
            }

            var attrs = Ui.Card(rightCol, Loc.T("rider.allAttributes"));
            var names = RiderAttributes.Names;
            var widths = new[] { 3f, 1f, 3f, 1f };
            for (int i = 0; i < names.Length; i += 2)
            {
                var cells = new string[4];
                for (int k = 0; k < 2 && i + k < names.Length; k++)
                {
                    cells[k * 2] = Theme.Dim(Loc.T("attr." + names[i + k]));
                    cells[k * 2 + 1] = Fmt.Attr(world, team.Id, r, names[i + k]);
                }
                UiFactory.TableRow(attrs, 30, widths, cells);
            }

            var contract = Ui.Card(rightCol, Loc.T("rider.contract"));
            UiFactory.Paragraph(contract, NegotiationPanel.Terms(r.Contract) + (r.FutureContract != null ? "\n" + Theme.Dim(Loc.T("terms.next", r.FutureContract.EndSeason)) : ""), Theme.FontSmall - 2);
            var rider = r;
            var acts = new List<UiAct>();
            if (r.FutureContract == null) acts.Add(UiFactory.Primary(Loc.T("team.renew"), () => { _neg = new NegotiationState { RiderId = rider.Id }; Shell.Refresh(); }));
            var option = r.Contract != null ? r.Contract.Clause(ClauseType.TeamOption) : null;
            if (option != null && !option.Exercised && r.Contract.EndSeason == world.Season)
                acts.Add(UiFactory.Act(Loc.T("team.option"), () => { ContractService.ExerciseOption(world, team, rider); Shell.Refresh(); }));
            double cost = Contracts.TerminationCost(world, Game.Db, r);
            bool confirm = _confirmRelease == r.Id;
            acts.Add(UiFactory.Option(Loc.T(confirm ? "team.releaseConfirm" : "team.release", MathX.FormatMoney(cost)), confirm, () =>
            {
                if (_confirmRelease == rider.Id)
                {
                    Contracts.Terminate(world, Game.Db, team, rider);
                    _confirmRelease = null;
                }
                else _confirmRelease = rider.Id;
                Shell.Refresh();
            }));
            UiFactory.ButtonRow(contract, 58, acts.ToArray());
        }
    }

    /// <summary>Staff: the current people (a table) or hiring by role, as two views of the page.</summary>
    public class StaffPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.staff"); } }
        private StaffRole _hiringRole = StaffRole.RaceEngineer;
        private string _confirmFire;
        private int _view;

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
            Ui.Chips(list, new[] { Loc.T("staff.viewTeam", staff.Count), Loc.T("staff.viewHire") }, _view, i => { _view = i; Shell.Refresh(); });
            if (_view == 0) Current(list, world, team, staff);
            else Hire(list, world, team);
        }

        private void Current(RectTransform list, WorldState world, Team team, List<StaffMember> staff)
        {
            double payroll = 0;
            foreach (var s in staff) payroll += s.Salary;
            var card = Ui.Card(list, null);
            Ui.Note(card, Loc.T("staff.summary", staff.Count, MathX.FormatMoney(payroll)) + " · " + Loc.T("staff.noteShort"));
            var w = new[] { 3.2f, 3.6f, 1f, 1.6f, 1.4f, 3.2f };
            UiFactory.TableRow(card, 34, w, new[] { Loc.T("staff.role"), Loc.T("staff.name"), Loc.T("staff.skill"), Loc.T("staff.salary"), Loc.T("staff.untilCol"), "" }, true);
            foreach (var s in staff)
            {
                var member = s;
                var rider = s.AssignedRiderId != null ? world.GetRider(s.AssignedRiderId) : null;
                bool expiring = s.ContractEndSeason <= world.Season;
                var row = UiFactory.TableRow(card, 46, w, new[]
                {
                    Loc.T("role." + s.Role) + (rider != null ? " " + Theme.Dim(rider.ShortName) : ""),
                    s.FullName, Fmt.Rating(s.Skill), MathX.FormatMoney(s.Salary),
                    expiring ? Theme.WarnText(s.ContractEndSeason.ToString()) : s.ContractEndSeason.ToString(), ""
                });
                double severance = StaffMarket.SeveranceCost(world, s);
                bool confirm = _confirmFire == s.Id;
                var renew = UiFactory.Button("Renew", row, Loc.T("staff.renew"), () =>
                {
                    StaffMarket.Extend(world, Game.Db, team, member, 2);
                    Shell.Toast(Loc.T("staff.renewed", member.FullName, member.ContractEndSeason, MathX.FormatMoney(member.Salary)));
                    Shell.Refresh();
                }, Theme.PanelLight, Theme.FontSmall - 6);
                UiFactory.InColumn(renew, w, w.Length - 1, 0, 2);
                var fire = UiFactory.Button("Fire", row, confirm ? Loc.T("staff.fireConfirm", MathX.FormatMoney(severance)) : Loc.T("staff.fire"), () =>
                {
                    if (_confirmFire == member.Id)
                    {
                        StaffMarket.Fire(world, team, member.Id);
                        _confirmFire = null;
                        Shell.Toast(Loc.T("staff.released", member.FullName, MathX.FormatMoney(severance)));
                    }
                    else _confirmFire = member.Id;
                    Shell.Refresh();
                }, confirm ? Theme.Accent : Theme.PanelLight, Theme.FontSmall - 6);
                UiFactory.InColumn(fire, w, w.Length - 1, 1, 2);
            }
        }

        private void Hire(RectTransform list, WorldState world, Team team)
        {
            var roles = new List<StaffRole>();
            foreach (StaffRole role in Enum.GetValues(typeof(StaffRole))) if (role != StaffRole.TeamManager) roles.Add(role);
            var card = Ui.Card(list, null);
            for (int row = 0; row < roles.Count; row += 6)
            {
                var labels = new List<string>();
                int selected = -1;
                for (int i = row; i < roles.Count && i < row + 6; i++)
                {
                    labels.Add(Loc.T("role." + roles[i]));
                    if (_hiringRole == roles[i]) selected = i - row;
                }
                int start = row;
                Ui.Chips(card, labels, selected, idx => { _hiringRole = roles[start + idx]; Shell.Refresh(); }, 50);
            }
            Ui.Note(card, Loc.T("roleinfo." + _hiringRole));
            int shown = 0;
            foreach (var c in StaffMarket.Candidates(world, Game.Db, team, _hiringRole))
            {
                if (shown++ >= 5) break;
                var cand = c;
                Ui.Item(card, c.FullName + "  " + Theme.Dim(c.Nationality), Loc.T("staff.line", c.Experience, MathX.FormatMoney(c.Salary)), Fmt.Rating(c.Skill),
                    UiFactory.Primary(Loc.T("staff.hireButton"), () =>
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
            var page = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            var me = world.PlayerManager;
            RectTransform list, rightCol;
            Ui.Columns(page, out list, out rightCol);
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
            var orgCard = Ui.Card(rightCol, Loc.T("manager.structure"));
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
