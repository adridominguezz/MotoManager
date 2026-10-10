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

        public override void Build(RectTransform content)
        {
            var list = UiFactory.ScrollList("Team", content);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var date = world.CurrentDate;

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
                if (r.Contract != null && r.Contract.EndSeason <= world.Season)
                {
                    var rider = r;
                    double ask = ContractService.AskingSalary(world, Game.Db, team, rider);
                    UiFactory.ButtonRow(list, 70,
                        UiFactory.Act("Renew 2 years (" + MathX.FormatMoney(ask) + "/season)", () => Renew(rider, ask, 2)),
                        UiFactory.Act("Renew 1 year", () => Renew(rider, ask * 1.05, 1)));
                }
            }

            UiFactory.Heading(list, "Staff");
            var sw = new[] { 3f, 4f, 1.2f, 2f, 1.5f };
            UiFactory.TableRow(list, 38, sw, new[] { "Role", "Name", "Skill", "Salary", "" }, true);
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
                var row = UiFactory.TableRow(list, 46, sw, new[] { s.Role.ToString(), s.FullName + " <color=#9AA4B2>" + s.Nationality + "</color>", s.Skill.ToString("0"), MathX.FormatMoney(s.Salary), "" });
                var fire = UiFactory.Button("Fire", row, "Release", () =>
                {
                    StaffMarket.Fire(world, team, member.Id);
                    Shell.Toast(member.FullName + " released (severance " + MathX.FormatMoney(StaffMarket.SeveranceCost(member)) + ")");
                    Shell.Refresh();
                }, Theme.PanelLight, Theme.FontSmall - 4);
                UiFactory.Place((RectTransform)fire.transform, 0.87f, 0.08f, 1f, 0.92f);
            }

            UiFactory.Heading(list, "Hire");
            var roles = new[] { StaffRole.ChiefEngineer, StaffRole.CrewChief, StaffRole.DataEngineer, StaffRole.TireEngineer, StaffRole.Designer, StaffRole.Aerodynamicist, StaffRole.Scout };
            var roleRow = UiFactory.Row(list, 60);
            for (int i = 0; i < roles.Length; i++)
            {
                var role = roles[i];
                var b = UiFactory.Button(role.ToString(), roleRow, role.ToString(), () =>
                {
                    _hiringRole = role;
                    Shell.Refresh();
                }, _hiringRole == role ? Theme.Accent : Theme.PanelLight, Theme.FontSmall - 4);
                UiFactory.Place((RectTransform)b.transform, (float)i / roles.Length, 0, (float)(i + 1) / roles.Length, 1, 3);
            }
            if (_hiringRole.HasValue)
            {
                foreach (var c in StaffMarket.Candidates(world, Game.Db, team, _hiringRole.Value))
                {
                    var cand = c;
                    var row = UiFactory.TableRow(list, 46, sw, new[] { c.Role.ToString(), c.FullName + " <color=#9AA4B2>" + c.Nationality + "</color>", c.Skill.ToString("0"), MathX.FormatMoney(c.Salary), "" });
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

        private void Renew(Rider rider, double salary, int years)
        {
            var res = ContractService.Offer(Game.World, Game.Db, Game.World.PlayerTeam, rider, salary, years);
            Shell.Toast(res.Message, res.Accepted);
            Shell.Refresh();
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

            UiFactory.Heading(list, "Start a project  <color=#9AA4B2>(design office: " + (1 + team.Facility(FacilityType.DesignOffice)) + " at once)</color>");
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

            UiFactory.Heading(list, "Facilities");
            var fw = new[] { 4f, 1.5f, 3f, 2f };
            UiFactory.TableRow(list, 38, fw, new[] { "Facility", "Level", "Upgrade cost", "" }, true);
            foreach (FacilityType f in Enum.GetValues(typeof(FacilityType)))
            {
                var type = f;
                int lvl = team.Facility(f);
                var row = UiFactory.TableRow(list, 44, fw, new[] { f.ToString(), lvl + "/5", lvl >= 5 ? "-" : MathX.FormatMoney(FinanceService.FacilityUpgradeCost(team, f)), "" });
                if (lvl >= 5) continue;
                var b = UiFactory.Button("Upgrade", row, "Upgrade", () =>
                {
                    bool ok = FinanceService.UpgradeFacility(world, team, type);
                    Shell.Toast(ok ? type + " upgraded" : "Not enough budget", ok);
                    Shell.Refresh();
                }, Theme.PanelLight, Theme.FontSmall - 4);
                UiFactory.Place((RectTransform)b.transform, 0.8f, 0.08f, 1f, 0.92f);
            }
            UiFactory.Paragraph(list, "<color=#9AA4B2>Workshop: development speed · Design office: parallel projects · Simulator: set-up and rider learning · Data center: forecasts and scouting · Academy: young riders' growth</color>");
        }
    }

    /// <summary>Season forecast, sponsors and their objectives, new sponsor deals.</summary>
    public class FinanceScreen : ScreenBase
    {
        public override string Title { get { return "Finances"; } }

        public override void Build(RectTransform content)
        {
            var list = UiFactory.ScrollList("Finance", content);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var f = FinanceService.SeasonForecast(world, Game.Db, team, Game.RoundsInSeason(team.CategoryId));

            UiFactory.Heading(list, "Budget  " + MathX.FormatMoney(team.Budget));
            var w = new[] { 5f, 3f };
            UiFactory.TableRow(list, 38, w, new[] { "Season forecast", "" }, true);
            UiFactory.TableRow(list, 40, w, new[] { "Sponsors", MathX.FormatMoney(f.SponsorIncome) });
            UiFactory.TableRow(list, 40, w, new[] { "Participation income", MathX.FormatMoney(f.ParticipationIncome) });
            UiFactory.TableRow(list, 40, w, new[] { "Rider salaries", "-" + MathX.FormatMoney(f.RiderSalaries) });
            UiFactory.TableRow(list, 40, w, new[] { "Staff salaries", "-" + MathX.FormatMoney(f.StaffSalaries) });
            UiFactory.TableRow(list, 40, w, new[] { "Facilities upkeep", "-" + MathX.FormatMoney(f.FacilityUpkeep) });
            UiFactory.TableRow(list, 40, w, new[] { "Bike lease", "-" + MathX.FormatMoney(f.BikeLease) });
            UiFactory.TableRow(list, 40, w, new[] { "Travel", "-" + MathX.FormatMoney(f.Travel) });
            UiFactory.TableRow(list, 44, w, new[] { "<b>Balance before prizes and bonuses</b>", "<b>" + (f.Balance >= 0 ? "" : "<color=#E10600>") + MathX.FormatMoney(f.Balance) + (f.Balance >= 0 ? "" : "</color>") + "</b>" });

            UiFactory.Heading(list, "Sponsors");
            var sw = new[] { 3f, 1f, 2f, 1.5f, 1.5f, 5f };
            UiFactory.TableRow(list, 38, sw, new[] { "Sponsor", "Tier", "Per season", "Until", "Mood", "Objectives" }, true);
            foreach (var d in team.Sponsors)
                UiFactory.TableRow(list, 44, sw, new[] { d.Name, d.Tier.ToString(), MathX.FormatMoney(d.PaymentPerSeason), d.EndSeason.ToString(), d.Satisfaction.ToString("0"), Objectives(d.Objectives) });

            int slots = 4 - team.Sponsors.Count;
            var cat = Game.Db.GetCategory(team.CategoryId);
            if (slots > 0 && cat != null && !cat.IsCup)
            {
                UiFactory.Heading(list, "Available sponsors  <color=#9AA4B2>(" + slots + " free slots)</color>");
                foreach (var s in SponsorService.Available(world, Game.Db, team, cat))
                {
                    var template = s;
                    var row = UiFactory.TableRow(list, 44, sw, new[] { s.Name + " <color=#9AA4B2>" + s.Industry + "</color>", s.Tier.ToString(), MathX.FormatMoney(SponsorService.ValueFor(s, team, cat)), s.ContractSeasons + "y", "", Objectives(s.Objectives) });
                    var b = UiFactory.Button("Sign", row, "Sign", () =>
                    {
                        SponsorService.Sign(world, team, template, cat, null);
                        Shell.Toast(template.Name + " signed");
                        Shell.Refresh();
                    }, Theme.Accent, Theme.FontSmall - 4);
                    UiFactory.Place((RectTransform)b.transform, 0.9f, 0.08f, 1f, 0.92f);
                }
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
            UiFactory.Paragraph(list, "Free seats in your team for " + (world.Season + 1) + ": <b>" + free + "</b>.  Offers are made at the rider's asking salary for 2 seasons; accepted deals start next season. " +
                                      "Values are what your scouts know (ranges narrow with scouting).", Theme.FontSmall);

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
            var w = new[] { 4f, 1f, 1f, 3f, 1.5f, 1.5f, 1.4f, 1.2f, 2.4f };
            UiFactory.TableRow(list, 38, w, new[] { "Rider", "Age", "Nat", "Team", "Race", "Potential", "Until", "Known", "" }, true);
            int shown = 0;
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
                    r.Contract != null ? r.Contract.EndSeason.ToString() : "-", (world.GetKnowledge(team.Id, r.Id) * 100).ToString("0") + "%", ""
                });
                var scout = UiFactory.Button("Scout", row, scouted ? "Scouting" : "Scout", () => ToggleScout(rider), scouted ? Theme.Good : Theme.PanelLight, Theme.FontSmall - 6);
                UiFactory.Place((RectTransform)scout.transform, 0.83f, 0.08f, 0.91f, 0.92f);
                var offer = UiFactory.Button("Offer", row, "Offer", () => MakeOffer(rider), Theme.Accent, Theme.FontSmall - 6);
                UiFactory.Place((RectTransform)offer.transform, 0.92f, 0.08f, 1f, 0.92f);
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
            var world = Game.World;
            var team = world.PlayerTeam;
            double ask = ContractService.AskingSalary(world, Game.Db, team, r);
            var res = ContractService.Offer(world, Game.Db, team, r, ask, 2);
            Shell.Toast(res.Message, res.Accepted);
            Shell.Refresh();
        }
    }
}
