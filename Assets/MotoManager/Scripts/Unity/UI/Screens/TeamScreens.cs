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
        private int _riderTab;

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

            if (_view < riders.Count)
            {
                Ui.Chips(list, new[] { Loc.T("rider.tab.profile"), Loc.T("rider.tab.stats") }, _riderTab, i => { _riderTab = i; Shell.Refresh(); }, 46);
                if (_riderTab == 0) RiderView(list, world, team, riders[_view]);
                else StatsView(list, world, team, riders[_view]);
            }
            else if (riders.Count < 2 && _view == riders.Count)
            {
                var card = Ui.Card(list, Loc.T("rider.freeSeat"), Loc.T("rider.freeSeatInfo"));
                UiFactory.ButtonRow(card, 64, UiFactory.Primary(Loc.T("rider.toMarket"), () => Shell.Show<MarketScreen>()));
            }
            else NegotiationPanel.Bids(list, Game, Shell.Refresh);
        }

        /// <summary>Results of one race session for the rider (position, or 0 when out / not started).</summary>
        private struct RaceDot
        {
            public string Label;
            public int Position;
            public bool Out;
            public int Field;
        }

        private void StatsView(RectTransform list, WorldState world, Team team, Rider r)
        {
            ChampionshipState champ;
            world.Championships.TryGetValue(r.CategoryId ?? team.CategoryId, out champ);
            StandingEntry st = null;
            if (champ != null) champ.Riders.TryGetValue(r.Id, out st);
            int pos = champ != null ? champ.RiderPosition(r.Id) : 0;

            // Every race (not sprints) of this season for the chart.
            var races = new List<RaceDot>();
            int dnfs = 0, sum = 0, finished = 0;
            if (champ != null)
                foreach (var wk in champ.Rounds)
                    foreach (var s in wk.Sessions)
                    {
                        if (s.Type != SessionType.Race) continue;
                        var e = s.Classification.Find(x => x.RiderId == r.Id);
                        if (e == null) continue;
                        bool outOf = e.Status != ResultStatus.Finished;
                        if (outOf) dnfs++;
                        else { sum += e.Position; finished++; }
                        races.Add(new RaceDot { Label = wk.Round.ToString(), Position = e.Position, Out = outOf, Field = s.Classification.Count });
                    }
            var last = races.Count > 0 ? races[races.Count - 1] : (RaceDot?)null;

            var top = Ui.Card(list, r.FullName, Loc.T("rider.stats.season", world.Season));
            Ui.Kpis(top,
                Ui.Kpi(pos > 0 ? "P" + pos : "-", Loc.T("rider.stats.position")),
                Ui.Kpi(st != null ? st.Points.ToString("0") : "0", Loc.T("rider.stats.points")),
                Ui.Kpi(last.HasValue ? (last.Value.Out ? Theme.BadText(Loc.T("result.DNF")) : "P" + last.Value.Position) : "-", Loc.T("rider.stats.lastRace")),
                Ui.Kpi(st != null ? st.Wins.ToString() : "0", Loc.T("rider.stats.wins")),
                Ui.Kpi(st != null ? st.Podiums.ToString() : "0", Loc.T("rider.stats.podiums")),
                Ui.Kpi(finished > 0 ? ((double)sum / finished).ToString("0.0", Loc.Culture) : "-", Loc.T("rider.stats.average")),
                Ui.Kpi(dnfs.ToString(), Loc.T("rider.stats.dnfs")));

            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);
            var chart = Ui.Card(leftCol, Loc.T("rider.stats.chart"));
            if (races.Count == 0) Ui.Note(chart, Loc.T("rider.stats.noRaces"));
            else PositionChart(chart, races);

            var hist = Ui.Card(rightCol, Loc.T("rider.stats.career"));
            var w = new[] { 1.1f, 1.4f, 3f, 0.9f, 0.9f, 0.9f, 1.1f };
            UiFactory.TableRow(hist, 32, w, new[] { Loc.T("col.season"), Loc.T("manager.category"), Loc.T("col.team"), "Pos", Loc.T("col.wins"), Loc.T("col.podiums"), Loc.T("col.points") }, true);
            int rows = 0;
            for (int i = r.CareerHistory.Count - 1; i >= 0 && rows < 9; i--, rows++)
            {
                var h = r.CareerHistory[i];
                var cat = Game.Db.GetCategory(h.CategoryId);
                UiFactory.TableRow(hist, 34, w, new[]
                {
                    h.Season.ToString(), cat != null ? cat.ShortName : h.CategoryId ?? "", h.TeamName ?? "",
                    h.ChampionshipPosition > 0 ? h.ChampionshipPosition.ToString() : "-", h.Wins.ToString(), h.Podiums.ToString(), h.Points.ToString("0")
                });
            }
            if (r.CareerHistory.Count == 0) Ui.Note(hist, Loc.T("rider.stats.noHistory"));
            if (!string.IsNullOrEmpty(r.RealHistory)) Ui.Note(hist, r.RealHistory);
        }

        /// <summary>Bar chart of race positions: taller is better; podiums in gold/silver/bronze, points in green.</summary>
        private static void PositionChart(Transform card, List<RaceDot> races)
        {
            var area = UiFactory.Row(card, 260);
            int field = 1;
            foreach (var d in races) field = Math.Max(field, d.Field);
            float wEach = 1f / Math.Max(races.Count, 12);
            for (int i = 0; i < races.Count; i++)
            {
                var d = races[i];
                float h = d.Out ? 0.04f : Mathf.Lerp(0.08f, 1f, (float)(field - d.Position) / Math.Max(1, field - 1));
                Color c = d.Out ? new Color(0.45f, 0.48f, 0.55f) : d.Position == 1 ? new Color32(240, 190, 40, 255) : d.Position == 2 ? new Color32(200, 205, 215, 255) :
                    d.Position == 3 ? new Color32(205, 125, 60, 255) : d.Position <= 15 ? Theme.Good : new Color32(70, 110, 160, 255);
                var bar = UiFactory.Panel("Bar", area, c, true);
                bar.rectTransform.anchorMin = new Vector2(i * wEach + wEach * 0.15f, 0.14f);
                bar.rectTransform.anchorMax = new Vector2((i + 1) * wEach - wEach * 0.15f, 0.14f + 0.72f * h);
                bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
                var label = UiFactory.Label("Pos", area, d.Out ? "X" : d.Position.ToString(), Theme.FontSmall - 6, TextAnchor.LowerCenter);
                label.rectTransform.anchorMin = new Vector2(i * wEach, 0.14f + 0.72f * h);
                label.rectTransform.anchorMax = new Vector2((i + 1) * wEach, 0.14f + 0.72f * h + 0.12f);
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                var round = UiFactory.Label("Round", area, d.Label, Theme.FontSmall - 8, TextAnchor.UpperCenter, Theme.TextDim);
                round.rectTransform.anchorMin = new Vector2(i * wEach, 0);
                round.rectTransform.anchorMax = new Vector2((i + 1) * wEach, 0.13f);
                round.rectTransform.offsetMin = round.rectTransform.offsetMax = Vector2.zero;
            }
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
                acts.Add(UiFactory.Act(Loc.T("team.option"), () =>
                {
                    bool ok = ContractService.ExerciseOption(world, Game.Db, team, rider);
                    Shell.Toast(ok ? Loc.T("team.optionDone", rider.FullName) : Loc.T("team.optionNoSeat"), ok);
                    Shell.Refresh();
                }));
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

    /// <summary>
    /// Staff: the current people (a table, with renewals, releases and which rider each engineer and mechanic works
    /// with) or hiring, where every role says what it does, how it is covered and what a candidate would add.
    /// </summary>
    public class StaffPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.staff"); } }
        private StaffRole? _hiringRole;
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
            int missing = 0;
            foreach (var role in Roles())
            {
                double best;
                int count;
                if (StaffService.Coverage(world, Game.Db, team, role, out best, out count) == RoleCoverage.Missing) missing++;
            }
            string hire = Loc.T("staff.viewHire") + (missing > 0 ? "  (" + Loc.T("staff.missingCount", missing) + ")" : "");
            Ui.Chips(list, new[] { Loc.T("staff.viewTeam", staff.Count), hire }, _view, i => { _view = i; Shell.Refresh(); });
            if (_view == 0) Current(list, world, team, staff);
            else Hire(list, world, team);
        }

        private static List<StaffRole> Roles()
        {
            var roles = new List<StaffRole>();
            foreach (StaffRole role in Enum.GetValues(typeof(StaffRole))) if (role != StaffRole.TeamManager) roles.Add(role);
            return roles;
        }

        private void Current(RectTransform list, WorldState world, Team team, List<StaffMember> staff)
        {
            double payroll = 0;
            foreach (var s in staff) payroll += s.Salary;
            var card = Ui.Card(list, null);
            Ui.Note(card, Loc.T("staff.summary", staff.Count, MathX.FormatMoney(payroll)) + " · " + Loc.T("staff.noteShort"));
            var w = new[] { 3.4f, 3.2f, 0.9f, 1.5f, 1.1f, 4.4f };
            UiFactory.TableRow(card, 34, w, new[] { Loc.T("staff.role"), Loc.T("staff.name"), Loc.T("staff.skill"), Loc.T("staff.salary"), Loc.T("staff.untilCol"), "" }, true);
            foreach (var s in staff)
            {
                var member = s;
                var rider = s.AssignedRiderId != null ? world.GetRider(s.AssignedRiderId) : null;
                bool expiring = s.ContractEndSeason <= world.Season;
                var row = UiFactory.TableRow(card, 46, w, new[]
                {
                    Loc.T("role." + s.Role) + (rider != null ? "  " + Theme.Dim("→ " + rider.ShortName) : ""),
                    s.FullName, Fmt.Rating(s.Skill), MathX.FormatMoney(s.Salary),
                    expiring ? Theme.WarnText(s.ContractEndSeason.ToString()) : s.ContractEndSeason.ToString(), ""
                });
                bool perRider = StaffService.IsPerRider(s.Role) && team.RiderIds.Count > 1;
                int parts = perRider ? 3 : 2;
                int part = 0;
                if (perRider)
                {
                    var swap = UiFactory.Button("Assign", row, Loc.T("staff.otherRider"), () =>
                    {
                        int idx = team.RiderIds.IndexOf(member.AssignedRiderId);
                        string next = team.RiderIds[(idx + 1) % team.RiderIds.Count];
                        if (StaffService.Assign(world, team, member, next))
                        {
                            var r = world.GetRider(next);
                            Shell.Toast(Loc.T("staff.assigned", member.FullName, r != null ? r.FullName : ""));
                        }
                        Shell.Refresh();
                    }, Theme.PanelLight, Theme.FontSmall - 6);
                    UiFactory.InColumn(swap, w, w.Length - 1, part++, parts);
                }
                double severance = StaffMarket.SeveranceCost(world, s);
                bool confirm = _confirmFire == s.Id;
                var renew = UiFactory.Button("Renew", row, Loc.T("staff.renew"), () =>
                {
                    StaffMarket.Extend(world, Game.Db, team, member, 2);
                    Shell.Toast(Loc.T("staff.renewed", member.FullName, member.ContractEndSeason, MathX.FormatMoney(member.Salary)));
                    Shell.Refresh();
                }, Theme.PanelLight, Theme.FontSmall - 6);
                UiFactory.InColumn(renew, w, w.Length - 1, part++, parts);
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
                UiFactory.InColumn(fire, w, w.Length - 1, part, parts);
            }
        }

        private static int Urgency(RoleCoverage c)
        {
            return c == RoleCoverage.Missing ? 0 : c == RoleCoverage.Weak ? 1 : c == RoleCoverage.Covered ? 2 : 3;
        }

        private static string Badge(RoleCoverage c)
        {
            switch (c)
            {
                case RoleCoverage.Missing: return Theme.BadText(Loc.T("staff.cov.Missing"));
                case RoleCoverage.Weak: return Theme.WarnText(Loc.T("staff.cov.Weak"));
                case RoleCoverage.Covered: return Theme.GoodText(Loc.T("staff.cov.Covered"));
                default: return Theme.Dim(Loc.T("staff.cov.Optional"));
            }
        }

        private void Hire(RectTransform list, WorldState world, Team team)
        {
            // Roles, most urgent first.
            var roles = Roles();
            var coverage = new Dictionary<StaffRole, RoleCoverage>();
            var bestSkill = new Dictionary<StaffRole, double>();
            foreach (var role in roles)
            {
                double best;
                int count;
                coverage[role] = StaffService.Coverage(world, Game.Db, team, role, out best, out count);
                bestSkill[role] = best;
            }
            roles.Sort((a, b) =>
            {
                int c = Urgency(coverage[a]).CompareTo(Urgency(coverage[b]));
                return c != 0 ? c : a.CompareTo(b);
            });
            if (!_hiringRole.HasValue) _hiringRole = roles[0];

            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);
            var rolesCard = Ui.Card(leftCol, null);
            var w = new[] { 3.6f, 2.2f, 1f };
            foreach (var role in roles)
            {
                var r = role;
                bool selected = _hiringRole == role;
                var row = UiFactory.TableRow(rolesCard, 42, w, new[]
                {
                    (selected ? "<b>" : "") + Loc.T("role." + role) + (selected ? "</b>" : ""), Badge(coverage[role]),
                    bestSkill[role] >= 0 ? Fmt.Rating(bestSkill[role]) : "-"
                }, false, selected ? new Color(0.9f, 0.22f, 0.23f, 0.22f) : (Color?)null);
                var pick = row.gameObject.AddComponent<UnityEngine.UI.Button>();
                pick.onClick.AddListener(() => { _hiringRole = r; Shell.Refresh(); });
                var img = row.GetComponent<UnityEngine.UI.Image>();
                if (img == null)
                {
                    img = row.gameObject.AddComponent<UnityEngine.UI.Image>();
                    img.color = new Color(1, 1, 1, 0.001f);
                }
                img.raycastTarget = true;
                pick.targetGraphic = img;
            }

            var sel = _hiringRole.Value;
            var detail = Ui.Card(rightCol, Loc.T("role." + sel), Badge(coverage[sel]));
            UiFactory.Paragraph(detail, "<b>" + Loc.T("staff.whatFor") + "</b>  " + Loc.T("roleinfo." + sel), Theme.FontSmall);
            UiFactory.Paragraph(detail, "<b>" + Loc.T("staff.impact") + "</b>  " + Loc.T("roleimpact." + sel), Theme.FontSmall);
            Ui.Note(detail, Loc.T("staff.advice." + coverage[sel]));
            double current = bestSkill[sel];
            int shown = 0;
            foreach (var c in StaffMarket.Candidates(world, Game.Db, team, sel))
            {
                if (shown++ >= 4) break;
                var cand = c;
                string delta = current < 0 ? Theme.GoodText(Loc.T("staff.newRole")) :
                    c.Skill > current ? Theme.GoodText("+" + (c.Skill - current).ToString("0") + " " + Loc.T("staff.vsCurrent")) : Theme.Dim(Loc.T("staff.noBetter"));
                Ui.Item(detail, c.FullName + "  " + Theme.Dim(c.Nationality), Loc.T("staff.line", c.Experience, MathX.FormatMoney(c.Salary)) + "\n" + delta, Fmt.Rating(c.Skill),
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
