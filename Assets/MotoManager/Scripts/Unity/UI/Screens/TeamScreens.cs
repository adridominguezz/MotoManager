using System;
using System.Collections.Generic;
using System.Text;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Race;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    internal static class Fmt
    {
        public static string Attr(WorldState world, string teamId, Rider r, string name)
        {
            double est, min, max;
            ScoutingService.Perceive(world, teamId, r, name, out est, out min, out max);
            if (max - min < 3) return est.ToString("0");
            return min.ToString("0") + "-" + max.ToString("0");
        }

        public static string Styles(Rider r)
        {
            if (r.Styles.Count == 0) return "-";
            var names = new List<string>();
            foreach (var s in r.Styles) names.Add(s.ToString());
            return string.Join(", ", names.ToArray());
        }

        public static string Signed(double v)
        {
            return (v >= 0 ? "+" : "") + v.ToString("0.#");
        }
    }

    /// <summary>Riders (attributes, styles, contracts, renewals) and staff (hire / fire).</summary>
    public class TeamScreen : ScreenBase
    {
        public override string Title { get { return "Team"; } }
        private StaffRole? _hiringRole;
        private NegotiationState _neg;
        private string _confirmRelease;

        public override void Build(RectTransform content)
        {
            var list = UiFactory.ScrollList("Team", content);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var date = world.CurrentDate;

            var me = world.PlayerManager;
            if (me != null && me.Job != null)
            {
                UiFactory.Heading(list, Loc.T("manager.title", me.FullName));
                var org = world.GetOrganisation(me.Job.OrganisationId);
                string role = me.Job.Role == ManagerRole.OwnerManager ? Loc.T("manager.owner", org != null ? org.Name : "") :
                    Loc.T("manager.employed", org != null ? org.Name : "", me.Job.EndSeason, MathX.FormatMoney(me.Job.SalaryPerSeason), Loc.T("autonomy." + me.Job.Autonomy));
                UiFactory.Paragraph(list, role + "\n<color=#9AA4B2>" + Loc.T("manager.wealth", MathX.FormatMoney(me.PersonalWealth), me.Reputation.ToString("0")) + "</color>", Theme.FontSmall + 2);
            }

            var pers = team.Personality;
            if (pers != null)
            {
                int teams = world.TeamsInCategory(team.CategoryId).Count;
                string objective = Loc.T("objective." + pers.Objective) + "  <color=#9AA4B2>" + Loc.T("objective.target", TeamAI.TargetPosition(pers.Objective, teams)) + "</color>";
                if (pers.LastPosition > 0)
                    objective += "\n<color=#9AA4B2>" + Loc.T("objective.last", Loc.T("objective." + pers.LastObjective), pers.LastPosition) + "</color> " +
                                 (pers.LastObjectiveMet ? "<color=#3CC878>" + Loc.T("objective.met") + "</color>" : "<color=#E10600>" + Loc.T("objective.missed") + "</color>");
                UiFactory.Heading(list, Loc.T("objective.title"));
                UiFactory.Paragraph(list, objective, Theme.FontSmall + 2);
            }

            UiFactory.Heading(list, "Riders");
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r == null) continue;
                var sb = new StringBuilder();
                sb.Append("<b>").Append(r.Number).Append("  ").Append(r.FullName).Append("</b>   ").Append(r.Nationality).Append(" · ").Append(r.AgeOn(date)).Append(" years")
                  .Append(" · overall ").Append(r.Attributes.Overall.ToString("0")).Append(" · morale ").Append(r.Morale.ToString("0"))
                  .Append("\n<color=#9AA4B2>Style: ").Append(Fmt.Styles(r)).Append("   Contract: until ").Append(r.Contract != null ? r.Contract.EndSeason.ToString() : "-")
                  .Append(", ").Append(r.Contract != null ? MathX.FormatMoney(r.Contract.SalaryPerSeason) : "-").Append("/season</color>");
                if (!string.IsNullOrEmpty(r.RealHistory)) sb.Append("\n<color=#9AA4B2>").Append(r.RealHistory).Append("</color>");
                UiFactory.Paragraph(list, sb.ToString(), Theme.FontSmall + 2);

                var names = RiderAttributes.Names;
                var widths = new[] { 3f, 1f, 3f, 1f, 3f, 1f };
                for (int i = 0; i < names.Length; i += 3)
                {
                    var cells = new string[6];
                    for (int k = 0; k < 3 && i + k < names.Length; k++)
                    {
                        cells[k * 2] = "<color=#9AA4B2>" + names[i + k] + "</color>";
                        cells[k * 2 + 1] = Fmt.Attr(world, team.Id, r, names[i + k]);
                    }
                    UiFactory.TableRow(list, 34, widths, cells);
                }
                {
                    var rider = r;
                    UiFactory.Paragraph(list, "<color=#9AA4B2>" + NegotiationPanel.Terms(r.Contract) + (r.FutureContract != null ? "  ·  " + Loc.T("terms.next", r.FutureContract.EndSeason) : "") + "</color>", Theme.FontSmall - 2);
                    var acts = new List<KeyValuePair<string, System.Action>>();
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
                    UiFactory.ButtonRow(list, 56, acts.ToArray());
                }
                if (_neg != null && _neg.RiderId == r.Id) NegotiationPanel.Build(list, Game, _neg, Shell.Refresh, () => _neg = null);
            }
            NegotiationPanel.Bids(list, Game, Shell.Refresh);

            UiFactory.Heading(list, Loc.T("staff.title"));
            var sw = new[] { 3f, 3.6f, 1f, 1f, 1.8f, 1.2f, 3f };
            UiFactory.TableRow(list, 38, sw, new[] { Loc.T("staff.role"), Loc.T("staff.name"), Loc.T("staff.skill"), Loc.T("staff.exp"), Loc.T("staff.salary"), Loc.T("staff.until"), "" }, true);
            var staff = new List<StaffMember>();
            foreach (var id in team.StaffIds)
            {
                StaffMember s;
                if (world.Staff.TryGetValue(id, out s)) staff.Add(s);
            }
            staff.Sort((a, b) => a.Role.CompareTo(b.Role));
            foreach (var s in staff)
            {
                var member = s;
                var rider = s.AssignedRiderId != null ? world.GetRider(s.AssignedRiderId) : null;
                bool expiring = s.ContractEndSeason <= world.Season;
                var row = UiFactory.TableRow(list, 46, sw, new[]
                {
                    Loc.T("role." + s.Role) + (rider != null ? " <color=#9AA4B2>· " + rider.ShortName + "</color>" : ""),
                    s.FullName + " <color=#9AA4B2>" + s.Nationality + "</color>", s.Skill.ToString("0"), s.Experience.ToString(), MathX.FormatMoney(s.Salary),
                    (expiring ? "<color=#F0B428>" : "") + s.ContractEndSeason + (expiring ? "</color>" : ""), ""
                });
                var renew = UiFactory.Button("Renew", row, Loc.T("staff.renew"), () =>
                {
                    StaffMarket.Extend(world, Game.Db, team, member, 2);
                    Shell.Toast(Loc.T("staff.renewed", member.FullName, member.ContractEndSeason, MathX.FormatMoney(member.Salary)));
                    Shell.Refresh();
                }, Theme.PanelLight, Theme.FontSmall - 6);
                UiFactory.Place((RectTransform)renew.transform, 0.78f, 0.08f, 0.885f, 0.92f);
                double severance = StaffMarket.SeveranceCost(world, s);
                var fire = UiFactory.Button("Fire", row, Loc.T("staff.release", MathX.FormatMoney(severance)), () =>
                {
                    StaffMarket.Fire(world, team, member.Id);
                    Shell.Toast(Loc.T("staff.released", member.FullName, MathX.FormatMoney(severance)));
                    Shell.Refresh();
                }, Theme.PanelLight, Theme.FontSmall - 6);
                UiFactory.Place((RectTransform)fire.transform, 0.89f, 0.08f, 1f, 0.92f);
            }

            UiFactory.Paragraph(list, "<color=#9AA4B2>" + Loc.T("staff.note") + "</color>", Theme.FontSmall - 2);
            UiFactory.Heading(list, Loc.T("staff.hire"));
            var roles = new List<StaffRole>();
            foreach (StaffRole role in Enum.GetValues(typeof(StaffRole))) if (role != StaffRole.TeamManager) roles.Add(role);
            for (int row0 = 0; row0 < roles.Count; row0 += 6)
            {
                var roleRow = UiFactory.Row(list, 56);
                for (int i = row0; i < roles.Count && i < row0 + 6; i++)
                {
                    var role = roles[i];
                    var b = UiFactory.Button(role.ToString(), roleRow, Loc.T("role." + role), () =>
                    {
                        _hiringRole = role;
                        Shell.Refresh();
                    }, _hiringRole == role ? Theme.Accent : Theme.PanelLight, Theme.FontSmall - 6);
                    UiFactory.Place((RectTransform)b.transform, (float)(i - row0) / 6, 0, (float)(i - row0 + 1) / 6, 1, 3);
                }
            }
            if (_hiringRole.HasValue) UiFactory.Paragraph(list, "<color=#9AA4B2>" + Loc.T("roleinfo." + _hiringRole.Value) + "</color>", Theme.FontSmall - 2);
            if (_hiringRole.HasValue)
            {
                foreach (var c in StaffMarket.Candidates(world, Game.Db, team, _hiringRole.Value))
                {
                    var cand = c;
                    var row = UiFactory.TableRow(list, 46, sw, new[] { Loc.T("role." + c.Role), c.FullName + " <color=#9AA4B2>" + c.Nationality + "</color>", c.Skill.ToString("0"), c.Experience.ToString(), MathX.FormatMoney(c.Salary), (world.Season + 1).ToString(), "" });
                    var hire = UiFactory.Button("Hire", row, "Hire", () =>
                    {
                        StaffMarket.Hire(world, team, cand);
                        Shell.Toast(cand.FullName + " hired as " + cand.Role);
                        Shell.Refresh();
                    }, Theme.Accent, Theme.FontSmall - 4);
                    UiFactory.Place((RectTransform)hire.transform, 0.87f, 0.08f, 1f, 0.92f);
                }
            }
        }

    }

    /// <summary>Bike components, derived performance, R&amp;D projects and facilities.</summary>
    public class BikeScreen : ScreenBase
    {
        public override string Title { get { return "Bike & R&D"; } }

        public override void Build(RectTransform content)
        {
            var list = UiFactory.ScrollList("Bike", content);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var bike = world.GetBike(team.BikeId);
            if (bike == null) return;
            var rules = Game.Db.GetRules(team.CategoryId, world.Season);
            var bikeClass = Game.Db.GetBikeClass(bike.BikeClassId);

            UiFactory.Heading(list, bike.Model + "  <color=#9AA4B2>" + (bikeClass != null ? bikeClass.Name : "") + "</color>");
            var cw = new[] { 3f, 1.2f, 1.5f, 3f };
            UiFactory.TableRow(list, 38, cw, new[] { "Component", "Level", "This season", "Development" }, true);
            foreach (var kv in bike.Components)
            {
                double gained;
                bike.SeasonGains.TryGetValue(kv.Key, out gained);
                bool allowed = DevelopmentService.ComponentAllowed(world, Game.Db, team, kv.Key);
                UiFactory.TableRow(list, 40, cw, new[] { kv.Key, kv.Value.ToString("0.0"), gained > 0 ? Fmt.Signed(gained) : "", allowed ? "<color=#3CC878>allowed</color>" : "<color=#9AA4B2>frozen by the rules</color>" });
            }
            var perf = BikePerformance.Compute(bike, bikeClass);
            var sb = new StringBuilder("<color=#9AA4B2>Performance:</color>  ");
            foreach (PerfAttr a in Enum.GetValues(typeof(PerfAttr))) sb.Append(a).Append(' ').Append(perf[a].ToString("0")).Append("   ");
            UiFactory.Paragraph(list, sb.ToString(), Theme.FontSmall);

            UiFactory.Heading(list, "Projects");
            var pw = new[] { 5f, 3f, 2f };
            bool any = false;
            foreach (var p in world.Projects)
            {
                if (p.TeamId != team.Id) continue;
                any = true;
                string status = p.Status == ProjectStatus.InProgress
                    ? (100 * p.DaysWorked / Math.Max(1, p.DurationDays)).ToString("0") + "%"
                    : p.Status.ToString();
                var effects = new List<string>();
                foreach (var kv in (p.Status == ProjectStatus.InProgress ? p.ExpectedEffects : p.ActualEffects)) effects.Add(kv.Key + " " + Fmt.Signed(kv.Value));
                UiFactory.TableRow(list, 42, pw, new[] { p.Name, string.Join(", ", effects.ToArray()), status });
            }
            if (!any) UiFactory.Paragraph(list, "<color=#9AA4B2>No projects yet.</color>");

            UiFactory.Heading(list, Loc.T("bike.projects", TeamCapabilities.ProjectCapacity(team)));
            var aw = new[] { 5f, 3.5f, 1.6f, 1.2f, 1.2f, 1.6f };
            UiFactory.TableRow(list, 38, aw, new[] { "Project", "Effect", "Cost", "Days", "Risk", "" }, true);
            foreach (var t in DevelopmentService.AvailableProjects(world, Game.Db, team))
            {
                var template = t;
                double cost = DevelopmentService.ProjectCost(world, Game.Db, team, t);
                var effects = new List<string>();
                foreach (var kv in t.Effects) effects.Add((kv.Value < 0 ? "<color=#F0B428>" : "") + kv.Key + " " + Fmt.Signed(kv.Value) + (kv.Value < 0 ? "</color>" : ""));
                var row = UiFactory.TableRow(list, 44, aw, new[] { t.Name, string.Join(", ", effects.ToArray()), MathX.FormatMoney(cost), t.DurationDays.ToString(), (t.Risk * 100).ToString("0") + "%", "" });
                var b = UiFactory.Button("Start", row, "Start", () =>
                {
                    string error;
                    var p = DevelopmentService.Start(world, Game.Db, team, template, null, out error);
                    Shell.Toast(p != null ? "Project started: " + template.Name : error, p != null);
                    Shell.Refresh();
                }, Theme.Accent, Theme.FontSmall - 4);
                UiFactory.Place((RectTransform)b.transform, 0.88f, 0.08f, 1f, 0.92f);
            }

            UiFactory.Heading(list, Loc.T("facility.title"));
            var fw = new[] { 3f, 1f, 6f, 2f, 1.8f };
            UiFactory.TableRow(list, 38, fw, new[] { Loc.T("facility.name"), Loc.T("facility.level"), Loc.T("facility.effect"), Loc.T("facility.upgrade"), "" }, true);
            foreach (FacilityType f in Enum.GetValues(typeof(FacilityType)))
            {
                var type = f;
                int lvl = team.Facility(f);
                var row = UiFactory.TableRow(list, 50, fw, new[] { Loc.T("facility." + f), lvl + "/5", "<color=#9AA4B2>" + Loc.T("facilityinfo." + f) + "</color>", lvl >= 5 ? "-" : MathX.FormatMoney(FinanceService.FacilityUpgradeCost(world, Game.Db, team, f)), "" });
                if (lvl >= 5) continue;
                var b = UiFactory.Button("Upgrade", row, "Upgrade", () =>
                {
                    bool ok = FinanceService.UpgradeFacility(world, Game.Db, team, type);
                    Shell.Toast(ok ? type + " upgraded" : "Not enough budget", ok);
                    Shell.Refresh();
                }, Theme.PanelLight, Theme.FontSmall - 4);
                UiFactory.Place((RectTransform)b.transform, 0.87f, 0.08f, 1f, 0.92f);
            }
            UiFactory.Paragraph(list, "<color=#9AA4B2>" + Loc.T("facility.note") + "</color>", Theme.FontSmall - 2);
        }
    }

    /// <summary>Season forecast, sponsors and their objectives, new sponsor deals.</summary>
    public class FinanceScreen : ScreenBase
    {
        private static string Money(double v)
        {
            return (v < 0 ? "<color=#E10600>" : "") + MathX.FormatMoney(v) + (v < 0 ? "</color>" : "");
        }

        public override string Title { get { return "Finances"; } }

        public override void Build(RectTransform content)
        {
            var list = UiFactory.ScrollList("Finance", content);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var f = FinanceService.SeasonForecast(world, Game.Db, team, Game.RoundsInSeason(team.CategoryId));

            var w = new[] { 5f, 3f };
            var org = world.OrganisationOf(team);
            if (org != null)
            {
                UiFactory.Heading(list, Loc.T("finance.organisation", org.Name));
                UiFactory.TableRow(list, 40, w, new[] { Loc.T("finance.cash"), MathX.FormatMoney(org.Cash) });
                UiFactory.TableRow(list, 40, w, new[] { Loc.T("finance.owner"), Loc.T("owner." + org.Owner) });
                if (org.TeamIds.Count > 1)
                {
                    var names = new List<string>();
                    foreach (var id in org.TeamIds) { var t = world.GetTeam(id); if (t != null) names.Add(t.Name); }
                    UiFactory.TableRow(list, 40, w, new[] { Loc.T("finance.teams"), string.Join(", ", names.ToArray()) });
                }
            }
            double spent = Accounts.SpentThisSeason(world, team);
            UiFactory.Heading(list, Loc.T("finance.teamBudget", team.Name));
            UiFactory.TableRow(list, 40, w, new[] { Loc.T("finance.allocated"), MathX.FormatMoney(team.SeasonBudget) });
            UiFactory.TableRow(list, 40, w, new[] { Loc.T("finance.spent"), MathX.FormatMoney(spent) });
            double left = team.SeasonBudget - spent;
            UiFactory.TableRow(list, 40, w, new[] { Loc.T("finance.remaining"), (left < 0 ? "<color=#E10600>" : "") + MathX.FormatMoney(left) + (left < 0 ? "</color>" : "") });

            // Where the cash is heading until the end of the season (estimate, not a guarantee).
            if (org != null)
            {
                var fc = CashflowForecast.ForOrganisation(world, Game.Db, org);
                UiFactory.TableRow(list, 38, w, new[] { Loc.T("forecast.title"), "" }, true);
                UiFactory.TableRow(list, 40, w, new[] { Loc.T("forecast.confirmedIncome"), MathX.FormatMoney(fc.ConfirmedIncome) });
                UiFactory.TableRow(list, 40, w, new[] { Loc.T("forecast.probableIncome"), MathX.FormatMoney(fc.ProbableIncome) });
                UiFactory.TableRow(list, 40, w, new[] { Loc.T("forecast.costs"), "-" + MathX.FormatMoney(fc.Costs) });
                UiFactory.TableRow(list, 40, w, new[] { Loc.T("forecast.lowest"), Money(fc.MinConfirmed) });
                UiFactory.TableRow(list, 40, w, new[] { Loc.T("forecast.endConfirmed"), Money(fc.EndConfirmed) });
                UiFactory.TableRow(list, 40, w, new[] { Loc.T("forecast.endProbable"), Money(fc.EndProbable) });
                UiFactory.Paragraph(list, "<color=#9AA4B2>" + Loc.T("forecast.note") + "</color>", Theme.FontSmall - 2);
            }

            // What has actually moved this season, by concept (from the ledger).
            var totals = Accounts.TotalsFor(world, team.OrganisationId, world.Season, team.Id);
            if (totals.Count > 0)
            {
                UiFactory.TableRow(list, 38, w, new[] { Loc.T("finance.actual", world.Season), "" }, true);
                var kinds = new List<LedgerKind>(totals.Keys);
                kinds.Sort();
                foreach (var k in kinds)
                {
                    double v = totals[k].Cash;
                    UiFactory.TableRow(list, 40, w, new[] { Loc.T("ledger." + k), (v < 0 ? "<color=#E10600>" : "") + MathX.FormatMoney(v) + (v < 0 ? "</color>" : "") });
                }
            }

            UiFactory.TableRow(list, 38, w, new[] { "Season forecast", "" }, true);
            UiFactory.TableRow(list, 40, w, new[] { "Sponsors", MathX.FormatMoney(f.SponsorIncome) });
            UiFactory.TableRow(list, 40, w, new[] { "Participation income", MathX.FormatMoney(f.ParticipationIncome) });
            UiFactory.TableRow(list, 40, w, new[] { "Rider salaries", "-" + MathX.FormatMoney(f.RiderSalaries) });
            UiFactory.TableRow(list, 40, w, new[] { "Staff salaries", "-" + MathX.FormatMoney(f.StaffSalaries) });
            UiFactory.TableRow(list, 40, w, new[] { "Facilities upkeep", "-" + MathX.FormatMoney(f.FacilityUpkeep) });
            UiFactory.TableRow(list, 40, w, new[] { "Bike lease", "-" + MathX.FormatMoney(f.BikeLease) });
            UiFactory.TableRow(list, 40, w, new[] { "Licences", "-" + MathX.FormatMoney(f.Licences) });
            UiFactory.TableRow(list, 40, w, new[] { "Travel", "-" + MathX.FormatMoney(f.Travel) });
            UiFactory.TableRow(list, 44, w, new[] { "<b>Balance before prizes and bonuses</b>", "<b>" + (f.Balance >= 0 ? "" : "<color=#E10600>") + MathX.FormatMoney(f.Balance) + (f.Balance >= 0 ? "" : "</color>") + "</b>" });

            UiFactory.Heading(list, Loc.T("sponsor.deals", SponsorService.DealsIn(team, world.Season), SponsorService.Slots(Game.Db, team, world.Season)));
            var sw = new[] { 3f, 2f, 2f, 1.5f, 1.2f, 5f };
            UiFactory.TableRow(list, 38, sw, new[] { Loc.T("sponsor.col.sponsor"), Loc.T("sponsor.col.perSeason"), Loc.T("sponsor.col.status"), Loc.T("sponsor.col.until"), Loc.T("sponsor.col.mood"), Loc.T("sponsor.col.objectives") }, true);
            foreach (var d in team.Sponsors)
            {
                string status = d.StartSeason > world.Season ? Loc.T("sponsor.fromSeason", d.StartSeason)
                    : !SponsorService.Pays(d, team, world.Season) ? Loc.T("sponsor.conditional", d.RequiredCategoryId) : Loc.T("sponsor.active");
                UiFactory.TableRow(list, 44, sw, new[] { d.Name + " <color=#9AA4B2>" + d.Industry + "</color>", MathX.FormatMoney(d.PaymentPerSeason), status, d.EndSeason.ToString(), d.Satisfaction.ToString("0"), Objectives(d.Objectives) });
            }

            // Offers are not money until accepted.
            var offers = SponsorMarket.OffersFor(world, team);
            UiFactory.Heading(list, Loc.T("sponsor.offers", offers.Count));
            if (offers.Count == 0) UiFactory.Paragraph(list, "<color=#9AA4B2>" + Loc.T("sponsor.noOffers") + "</color>", Theme.FontSmall);
            var ow = new[] { 3f, 2f, 2f, 1.5f, 1.2f, 5f };
            if (offers.Count > 0)
                UiFactory.TableRow(list, 38, ow, new[] { Loc.T("sponsor.col.sponsor"), Loc.T("sponsor.col.perSeason"), Loc.T("sponsor.col.signing"), Loc.T("sponsor.col.seasons"), Loc.T("sponsor.col.expires"), Loc.T("sponsor.col.objectives") }, true);
            foreach (var o in offers)
            {
                var offer = o;
                string terms = Objectives(o.Objectives);
                if (o.PenaltyOnMiss > 0) terms += " · " + Loc.T("sponsor.penalty", (o.PenaltyOnMiss * 100).ToString("0"));
                if (o.IndustryExclusive) terms += " · " + Loc.T("sponsor.exclusive");
                UiFactory.TableRow(list, 44, ow, new[] { o.Name + " <color=#9AA4B2>" + o.Industry + (o.Renewal ? " · " + Loc.T("sponsor.renewal") : "") + "</color>",
                    MathX.FormatMoney(o.PaymentPerSeason), MathX.FormatMoney(o.SigningBonus), o.StartSeason + (o.Seasons > 1 ? "–" + (o.StartSeason + o.Seasons - 1) : ""),
                    Loc.T("sponsor.events", Math.Max(0, o.ExpiresAfterEvent - world.EventsCompleted + 1)), terms });
                UiFactory.ButtonRow(list, 52,
                    UiFactory.Act(Loc.T("sponsor.accept"), () => { Shell.Toast(SponsorMarket.Accept(world, Game.Db, offer.Id).Message); Shell.Refresh(); }),
                    UiFactory.Act(Loc.T("sponsor.counter"), () => { Shell.Toast(SponsorMarket.Counter(world, Game.Db, offer.Id, 0.1).Message); Shell.Refresh(); }),
                    UiFactory.Act(Loc.T("sponsor.reject"), () => { Shell.Toast(SponsorMarket.Reject(world, offer.Id).Message); Shell.Refresh(); }));
            }
        }

        private static string Objectives(List<SponsorObjective> objectives)
        {
            var parts = new List<string>();
            foreach (var o in objectives)
            {
                switch (o.Type)
                {
                    case SponsorObjectiveType.RaceFinishTopN: parts.Add("top " + o.Target + ": " + MathX.FormatMoney(o.BonusPerOccurrence) + "/race"); break;
                    case SponsorObjectiveType.Podiums: parts.Add("podium: " + MathX.FormatMoney(o.BonusPerOccurrence)); break;
                    case SponsorObjectiveType.Wins: parts.Add("win: " + MathX.FormatMoney(o.BonusPerOccurrence)); break;
                    case SponsorObjectiveType.SeasonPoints: parts.Add(o.Target + " pts: " + MathX.FormatMoney(o.SeasonBonus)); break;
                    case SponsorObjectiveType.ChampionshipPositionTopN: parts.Add("champ. top " + o.Target + ": " + MathX.FormatMoney(o.SeasonBonus)); break;
                    default: parts.Add(o.Type.ToString()); break;
                }
            }
            return string.Join(" · ", parts.ToArray());
        }
    }

    /// <summary>Scouting and transfer offers for next season, across both pyramids and the talent pools.</summary>
    public class MarketScreen : ScreenBase
    {
        public override string Title { get { return "Market"; } }
        private string _category = "moto2";
        private bool _onlyFree = true;
        private NegotiationState _neg;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var tabs = UiFactory.Rect("Tabs", content);
            UiFactory.Place(tabs, 0, 0.92f, 1, 1);
            var cats = Game.Db.CategoriesOrdered();
            for (int i = 0; i < cats.Count; i++)
            {
                var cat = cats[i];
                var b = UiFactory.Button(cat.Id, tabs, cat.ShortName, () =>
                {
                    _category = cat.Id;
                    Shell.Refresh();
                }, cat.Id == _category ? Theme.Accent : Theme.PanelLight, Theme.FontSmall - 4);
                UiFactory.Place((RectTransform)b.transform, (float)i / (cats.Count + 1), 0, (float)(i + 1) / (cats.Count + 1), 1, 3);
            }
            var toggle = UiFactory.Button("Free", tabs, _onlyFree ? "Contract ending" : "All riders", () =>
            {
                _onlyFree = !_onlyFree;
                Shell.Refresh();
            }, Theme.PanelLight, Theme.FontSmall - 4);
            UiFactory.Place((RectTransform)toggle.transform, (float)cats.Count / (cats.Count + 1), 0, 1, 1, 3);

            var list = UiFactory.ScrollList("Market", content);
            UiFactory.Place((RectTransform)list.parent.parent, 0, 0, 1, 0.91f);
            int free = ContractService.SeatsFreeNextSeason(world, Game.Db, team);
            UiFactory.Paragraph(list, Loc.T("market.seats", world.Season + 1, free, ContractService.SeatsFreeNow(world, Game.Db, team)), Theme.FontSmall);
            NegotiationPanel.Bids(list, Game, Shell.Refresh);
            if (_neg != null) NegotiationPanel.Build(list, Game, _neg, Shell.Refresh, () => _neg = null);

            var date = world.CurrentDate;
            var riders = new List<Rider>();
            foreach (var r in world.Riders.Values)
            {
                if (r.IsRetired || r.CategoryId != _category || r.TeamId == team.Id) continue;
                if (_onlyFree && r.Contract != null && r.Contract.EndSeason > world.Season) continue;
                riders.Add(r);
            }
            riders.Sort((a, b) =>
            {
                double ea, eb, x, y;
                ScoutingService.Perceive(world, team.Id, a, "RaceSkill", out ea, out x, out y);
                ScoutingService.Perceive(world, team.Id, b, "RaceSkill", out eb, out x, out y);
                return eb.CompareTo(ea);
            });
            var w = new[] { 4f, 1f, 1f, 3f, 1.5f, 1.5f, 1.4f, 1.2f, 1.6f, 2.4f };
            UiFactory.TableRow(list, 38, w, new[] { "Rider", "Age", "Nat", "Team", "Race", "Potential", "Until", "Known", Loc.T("market.brings"), "" }, true);
            int shown = 0;
            var levels = TransferMarket.CategoryLevels(world, Game.Db);
            foreach (var r in riders)
            {
                if (shown++ >= 60) break;
                var rider = r;
                var t = world.GetTeam(r.TeamId);
                bool scouted = world.Scouting.Exists(s => s.TeamId == team.Id && s.RiderId == r.Id);
                var row = UiFactory.TableRow(list, 44, w, new[]
                {
                    r.FullName + (r.FutureContract != null ? " <color=#9AA4B2>(signed)</color>" : ""), r.AgeOn(date).ToString(), r.Nationality,
                    t != null ? t.ShortName ?? t.Name : "<color=#9AA4B2>free</color>", Fmt.Attr(world, team.Id, r, "RaceSkill"), Fmt.Attr(world, team.Id, r, "Potential"),
                    r.Contract != null ? r.Contract.EndSeason.ToString() : "-", (world.GetKnowledge(team.Id, r.Id) * 100).ToString("0") + "%",
                    TransferMarket.NeedsToPay(world, Game.Db, team, r, levels) ? "<color=#3CC878>" + MathX.FormatMoney(r.PersonalBacking) + "</color>" : "", ""
                });
                var scout = UiFactory.Button("Scout", row, scouted ? "Scouting" : "Scout", () => ToggleScout(rider), scouted ? Theme.Good : Theme.PanelLight, Theme.FontSmall - 6);
                UiFactory.Place((RectTransform)scout.transform, 0.885f, 0.08f, 0.94f, 0.92f);
                var offer = UiFactory.Button("Offer", row, "Offer", () => MakeOffer(rider), Theme.Accent, Theme.FontSmall - 6);
                UiFactory.Place((RectTransform)offer.transform, 0.945f, 0.08f, 1f, 0.92f);
            }
        }

        private void ToggleScout(Rider r)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            int idx = world.Scouting.FindIndex(s => s.TeamId == team.Id && s.RiderId == r.Id);
            if (idx >= 0) world.Scouting.RemoveAt(idx);
            else world.Scouting.Add(new ScoutingAssignment { TeamId = team.Id, RiderId = r.Id });
            Shell.Refresh();
        }

        private void MakeOffer(Rider r)
        {
            _neg = new NegotiationState { RiderId = r.Id };
            Shell.Refresh();
        }
    }
}
