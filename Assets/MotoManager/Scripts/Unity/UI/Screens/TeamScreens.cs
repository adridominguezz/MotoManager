using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

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
    /// The team's riders, one at a time (a selector on top): the rider's level and key qualities on the left, the
    /// contract and its actions on the right. Every attribute opens on demand and the season statistics are a
    /// full page one tap away. Bids from other teams have their own chip; a negotiation takes the whole page.
    /// </summary>
    public class RidersPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.riders"); } }
        private NegotiationState _neg;
        private string _confirmRelease;
        private int _view;
        private bool _stats;

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

            // Season statistics: a full page with a way back.
            if (_stats && _view < riders.Count)
            {
                MobileParts.BackRow(list, Loc.T("career.back"), () => { _stats = false; Shell.Refresh(); });
                StatsView(list, world, team, riders[_view]);
                return;
            }
            _stats = false;
            Ui.Chips(list, labels, _view, i => { _view = i; _confirmRelease = null; Shell.Refresh(); });

            if (_view < riders.Count) RiderView(list, world, team, riders[_view]);
            else if (riders.Count < 2 && _view == riders.Count)
            {
                var card = Ui.Card(list, Loc.T("rider.freeSeat"));
                UiFactory.ButtonRow(card, 56, UiFactory.Primary(Loc.T("nav.market"), () => Shell.Show<MarketScreen>()));
                Ui.Note(card, Loc.T("rider.freeSeatInfo"));
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

            // Four big numbers, the chart on the left, the rest of the season and the career on the right.
            var top = Ui.Card(list, r.FullName, Loc.T("rider.stats.season", world.Season));
            Ui.Kpis(top,
                Ui.Kpi(pos > 0 ? "P" + pos : "-", Loc.T("rider.stats.position")),
                Ui.Kpi(st != null ? st.Points.ToString("0") : "0", Loc.T("rider.stats.points")),
                Ui.Kpi(st != null ? st.Wins.ToString() : "0", Loc.T("rider.stats.wins")),
                Ui.Kpi(st != null ? st.Podiums.ToString() : "0", Loc.T("rider.stats.podiums")));

            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);
            var chart = Ui.Card(leftCol, Loc.T("rider.stats.chart"));
            if (races.Count == 0) Ui.Note(chart, Loc.T("rider.stats.noRaces"));
            else PositionChart(chart, races);

            var season = Ui.Card(rightCol, null);
            Ui.Line(season, Loc.T("rider.stats.lastRace"), last.HasValue ? (last.Value.Out ? Theme.BadText(Loc.T("result.DNF")) : "P" + last.Value.Position) : "-");
            Ui.Line(season, Loc.T("rider.stats.average"), finished > 0 ? ((double)sum / finished).ToString("0.0", Loc.Culture) : "-");
            Ui.Line(season, Loc.T("rider.stats.dnfs"), dnfs.ToString());

            // Career: the category under the season, wins and podiums together.
            var hist = Ui.Card(rightCol, Loc.T("rider.stats.career"));
            var w = new[] { 1.6f, 3f, 0.9f, 1.5f, 1.1f };
            UiFactory.TableRow(hist, 32, w, new[] { Loc.T("col.season"), Loc.T("col.team"), "Pos", Loc.T("col.wins") + " / " + Loc.T("col.podiums"), Loc.T("col.points") }, true);
            int rows = 0;
            for (int i = r.CareerHistory.Count - 1; i >= 0 && rows < 9; i--, rows++)
            {
                var h = r.CareerHistory[i];
                var cat = Game.Db.GetCategory(h.CategoryId);
                UiFactory.TableRow(hist, 40, w, new[]
                {
                    h.Season + "\n" + Theme.Dim(cat != null ? cat.ShortName : h.CategoryId ?? ""), h.TeamName ?? "",
                    h.ChampionshipPosition > 0 ? h.ChampionshipPosition.ToString() : "-", h.Wins + " / " + h.Podiums, h.Points.ToString("0")
                });
            }
            if (r.CareerHistory.Count == 0) UiFactory.Paragraph(hist, Theme.Dim(Loc.T("rider.stats.noHistory")), Theme.FontSmall - 2);
            if (!string.IsNullOrEmpty(r.RealHistory)) Ui.Note(hist, r.RealHistory);
        }

        /// <summary>
        /// Line chart of race positions: higher is better, the points joined by a line. Podiums in gold, silver and
        /// bronze, points finishes in green, the rest in blue; retirements are grey crosses at the bottom.
        /// </summary>
        private static void PositionChart(Transform card, List<RaceDot> races)
        {
            var area = UiFactory.Row(card, 220);
            int field = 1;
            foreach (var d in races) field = Math.Max(field, d.Field);
            const float bottom = 0.14f, top = 0.9f;
            // Guide lines: P1, P5, P10, P15 (points), last.
            foreach (int guide in new[] { 1, 5, 10, 15 })
            {
                if (guide > field) continue;
                float gy = bottom + (top - bottom) * (field - guide) / Math.Max(1f, field - 1);
                var g = UiFactory.Panel("Guide", area, new Color(1, 1, 1, guide == 15 ? 0.14f : 0.06f));
                g.rectTransform.anchorMin = new Vector2(0.06f, gy);
                g.rectTransform.anchorMax = new Vector2(1f, gy);
                g.rectTransform.offsetMin = new Vector2(0, -1);
                g.rectTransform.offsetMax = new Vector2(0, 1);
                var gl = UiFactory.Label("GuideLabel", area, "P" + guide, Theme.FontSmall - 8, TextAnchor.MiddleLeft, Theme.TextDim);
                gl.rectTransform.anchorMin = new Vector2(0, gy - 0.05f);
                gl.rectTransform.anchorMax = new Vector2(0.06f, gy + 0.05f);
                gl.rectTransform.offsetMin = gl.rectTransform.offsetMax = Vector2.zero;
            }
            float wEach = 0.94f / Math.Max(races.Count, 8);
            var line = UiFactory.Rect("Line", area).gameObject.AddComponent<LineGraphic>();
            UiFactory.Stretch(line.rectTransform);
            line.color = new Color(1, 1, 1, 0.45f);
            line.raycastTarget = false;
            for (int i = 0; i < races.Count; i++)
            {
                var d = races[i];
                float x = 0.06f + wEach * (i + 0.5f);
                float y = d.Out ? bottom : bottom + (top - bottom) * (field - d.Position) / Math.Max(1f, field - 1);
                if (!d.Out) line.Points.Add(new Vector2(x, y));
                Color c = d.Out ? new Color(0.45f, 0.48f, 0.55f) : d.Position == 1 ? new Color32(240, 190, 40, 255) : d.Position == 2 ? new Color32(200, 205, 215, 255) :
                    d.Position == 3 ? new Color32(205, 125, 60, 255) : d.Position <= 15 ? Theme.Good : (Color)Theme.Steel;
                var dot = UiFactory.Panel("Dot", area, c, true);
                UiFactory.Round(dot, 4f);
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(x, y);
                dot.rectTransform.sizeDelta = new Vector2(18, 18);
                var label = UiFactory.Label("Pos", area, d.Out ? "X" : d.Position.ToString(), Theme.FontSmall - 6, TextAnchor.LowerCenter, d.Out ? Theme.TextDim : Theme.Text);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(x, y);
                label.rectTransform.sizeDelta = new Vector2(40, 24);
                label.rectTransform.anchoredPosition = new Vector2(0, 20);
                var round = UiFactory.Label("Round", area, d.Label, Theme.FontSmall - 8, TextAnchor.UpperCenter, Theme.TextDim);
                round.rectTransform.anchorMin = new Vector2(x - wEach * 0.5f, 0);
                round.rectTransform.anchorMax = new Vector2(x + wEach * 0.5f, 0.1f);
                round.rectTransform.offsetMin = round.rectTransform.offsetMax = Vector2.zero;
            }
            line.SetVerticesDirty();
        }

        private void RiderView(RectTransform list, WorldState world, Team team, Rider r)
        {
            var date = world.CurrentDate;
            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);

            // Left: who the rider is and how good, at a glance.
            var card = Ui.Card(leftCol, r.FullName, r.Nationality + " · " + Loc.T("rider.age", r.AgeOn(date)));
            Ui.Kpis(card,
                Ui.Kpi(Fmt.Rating(r.Attributes.Overall), Loc.T("rider.overall")),
                Ui.Kpi(Fmt.Rating(r.Morale), Loc.T("rider.morale")),
                Ui.Kpi(Fmt.Attr(world, team.Id, r, "Potential"), Loc.T("col.potential")));
            MobileParts.KeyBars(card, world, team.Id, r);
            Ui.Line(card, Loc.T("rider.style", "").TrimEnd(' ', ':'), Fmt.Styles(r));

            // Right: the contract (salary and end in plain sight, the clauses folded) and what can be done with it.
            var contract = Ui.Card(rightCol, Loc.T("rider.contract"));
            Ui.Line(contract, Loc.T("staff.salary"), r.Contract != null ? MathX.FormatMoney(r.Contract.SalaryPerSeason) : "-", true);
            Ui.Line(contract, Loc.T("rider.until"), r.Contract != null ? r.Contract.EndSeason.ToString() : "-", true);
            if (r.FutureContract != null) Ui.Line(contract, Theme.Dim(Loc.T("terms.next", r.FutureContract.EndSeason)), "");
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
            UiFactory.ButtonRow(contract, 52, acts.ToArray());
            Ui.Note(contract, NegotiationPanel.Terms(r.Contract));

            // Detail one tap away: the season statistics (a page) and every attribute (opens here).
            UiFactory.ButtonRow(rightCol, 50, UiFactory.Act(Loc.T("rider.tab.stats") + "  ►", () => { _stats = true; Shell.Refresh(); }));
            Ui.Expander(rightCol, "rider.attrs", Loc.T("rider.allAttributes"), inner =>
            {
                var attrs = Ui.Card(inner, null);
                MobileParts.AttributeTable(attrs, world, team.Id, rider);
            }, Shell.Refresh);
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
        private string _confirmHire;
        private string _member;
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
            // One person opened: a full page with the actions, and a way back.
            var open = staff.Find(m => m.Id == _member);
            if (open != null)
            {
                MobileParts.BackRow(list, Loc.T("career.back"), () => { _member = null; _confirmFire = null; Shell.Refresh(); });
                Member(list, world, team, open);
                return;
            }
            _member = null;
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

        /// <summary>The staff in one table: role, name, level, salary and contract; each row opens its detail.</summary>
        private void Current(RectTransform list, WorldState world, Team team, List<StaffMember> staff)
        {
            double payroll = 0;
            foreach (var s in staff) payroll += s.Salary;
            var card = Ui.Card(list, Loc.T("staff.title"), Loc.T("staff.summary", staff.Count, MathX.FormatMoney(payroll)));
            var w = new[] { 3.4f, 3.2f, 1f, 1.6f, 1.1f, 1.7f };
            UiFactory.TableRow(card, 34, w, new[] { Loc.T("staff.role"), Loc.T("staff.name"), Loc.T("staff.skill"), Loc.T("staff.salary"), Loc.T("staff.untilCol"), "" }, true);
            foreach (var s in staff)
            {
                var member = s;
                var rider = s.AssignedRiderId != null ? world.GetRider(s.AssignedRiderId) : null;
                bool expiring = s.ContractEndSeason <= world.Season;
                var row = UiFactory.TableRow(card, 42, w, new[]
                {
                    Loc.T("role." + s.Role) + (rider != null ? "  " + Theme.Dim("→ " + rider.ShortName) : ""),
                    s.FullName, Fmt.Rating(s.Skill), MathX.FormatMoney(s.Salary),
                    expiring ? Theme.WarnText(s.ContractEndSeason.ToString()) : s.ContractEndSeason.ToString(), ""
                });
                MobileParts.Tappable(row, () => { _member = member.Id; _confirmFire = null; Shell.Refresh(); });
                var b = UiFactory.Button("Open", row, Loc.T("shop.manage"), () => { _member = member.Id; _confirmFire = null; Shell.Refresh(); }, Theme.PanelLight, Theme.FontSmall - 4);
                UiFactory.InColumn(b, w, w.Length - 1);
            }
            Ui.Note(card, Loc.T("staff.note"));
        }

        /// <summary>One staff member: the figures in big type and the three actions (other rider, renew, release).</summary>
        private void Member(RectTransform list, WorldState world, Team team, StaffMember s)
        {
            var member = s;
            var rider = s.AssignedRiderId != null ? world.GetRider(s.AssignedRiderId) : null;
            bool expiring = s.ContractEndSeason <= world.Season;
            var card = Ui.Card(list, s.FullName, Loc.T("role." + s.Role) + (rider != null ? " · " + Loc.T("staff.with", rider.FullName) : ""));
            Ui.Kpis(card,
                Ui.Kpi(Fmt.Rating(s.Skill), Loc.T("staff.skill")),
                Ui.Kpi(MathX.FormatMoney(s.Salary), Loc.T("staff.salary")),
                Ui.Kpi(expiring ? Theme.WarnText(s.ContractEndSeason.ToString()) : s.ContractEndSeason.ToString(), Loc.T("staff.untilCol")));
            var acts = new List<UiAct>();
            if (StaffService.PerRider(s.Role) && team.RiderIds.Count > 1)
                acts.Add(UiFactory.Act(Loc.T("staff.otherRider"), () =>
                {
                    int idx = team.RiderIds.IndexOf(member.AssignedRiderId);
                    string next = team.RiderIds[(idx + 1) % team.RiderIds.Count];
                    if (StaffService.Assign(world, team, member, next))
                    {
                        var r = world.GetRider(next);
                        Shell.Toast(Loc.T("staff.assigned", member.FullName, r != null ? r.FullName : ""));
                    }
                    Shell.Refresh();
                }));
            acts.Add(UiFactory.Primary(Loc.T("staff.renew"), () =>
            {
                StaffMarket.Extend(world, Game.Db, team, member, 2);
                Shell.Toast(Loc.T("staff.renewed", member.FullName, member.ContractEndSeason, MathX.FormatMoney(member.Salary)));
                Shell.Refresh();
            }));
            double severance = StaffMarket.SeveranceCost(world, s);
            bool confirm = _confirmFire == s.Id;
            acts.Add(UiFactory.Option(confirm ? Loc.T("staff.fireConfirm", MathX.FormatMoney(severance)) : Loc.T("staff.fire"), confirm, () =>
            {
                if (_confirmFire == member.Id)
                {
                    StaffMarket.Fire(world, team, member.Id);
                    _confirmFire = null;
                    _member = null;
                    Shell.Toast(Loc.T("staff.released", member.FullName, MathX.FormatMoney(severance)));
                }
                else _confirmFire = member.Id;
                Shell.Refresh();
            }));
            UiFactory.ButtonRow(card, 56, acts.ToArray());
            Ui.Note(card, Loc.T("roleinfo." + s.Role));
            Ui.Note(card, Loc.T("staff.note"));
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
            // Roles as a two-column grid of tiles (name, then coverage and the best level), most urgent first.
            var rolesCard = Ui.Card(leftCol, null);
            for (int i = 0; i < roles.Count; i += 2)
            {
                var line = UiFactory.Row(rolesCard, UiScale.Touch(44));
                for (int k = 0; k < 2 && i + k < roles.Count; k++)
                {
                    var r = roles[i + k];
                    bool selected = _hiringRole == r;
                    string text = (selected ? "<b>" : "") + Loc.T("role." + r) + (selected ? "</b>" : "") + "\n<size=" + (Theme.FontSmall - 4) + ">" +
                                  Badge(coverage[r]) + Theme.Dim("  ·  ") + (bestSkill[r] >= 0 ? Fmt.Rating(bestSkill[r]) : Theme.Dim("-")) + "</size>";
                    var b = UiFactory.Button("Role", line, text, () => { _hiringRole = r; Shell.Refresh(); },
                        selected ? Theme.Alpha(Theme.CurrentAccent, 0.45f) : Color.Lerp(Theme.Panel, Theme.PanelLight, 0.5f), Theme.FontSmall - 2);
                    var label = b.GetComponentInChildren<Text>();
                    label.supportRichText = true;
                    label.alignment = TextAnchor.MiddleLeft;
                    label.rectTransform.offsetMin = new Vector2(12, 0);
                    UiFactory.Place((RectTransform)b.transform, k * 0.5f, 0, (k + 1) * 0.5f, 1, 3);
                }
            }

            // The chosen role: current level, candidates (name, level and gain, salary, one button); what the role
            // does and the advice are folded.
            var sel = _hiringRole.Value;
            var detail = Ui.Card(rightCol, Loc.T("role." + sel), Badge(coverage[sel]));
            double current = bestSkill[sel];
            var replaces = StaffMarket.WouldReplace(world, team, sel);
            Ui.Note(detail, Loc.T("staff.ceiling", StaffMarket.SkillCeiling(world, Game.Db, team).ToString("0")));
            if (replaces != null) Ui.Note(detail, Theme.WarnText(Loc.T("staff.replaceNote", replaces.FullName, MathX.FormatMoney(StaffMarket.SeveranceCost(world, replaces)))));
            var cw = new[] { 3.2f, 1.5f, 1.5f, 1.8f };
            UiFactory.TableRow(detail, 32, cw, new[] { Loc.T("staff.name"), Loc.T("staff.skill"), Loc.T("staff.salary"), "" }, true);
            int shown = 0;
            foreach (var c in StaffMarket.Candidates(world, Game.Db, team, sel))
            {
                if (shown++ >= 4) break;
                var cand = c;
                string delta = current < 0 ? "" : c.Skill > current ? " " + Theme.GoodText("+" + (c.Skill - current).ToString("0")) : "";
                var row = UiFactory.TableRow(detail, 48, cw, new[]
                {
                    "<b>" + c.FullName + "</b>\n" + Theme.Dim(c.Nationality + " · " + Loc.T("staff.exp") + " " + c.Experience),
                    Fmt.Rating(c.Skill) + delta, MathX.FormatMoney(c.Salary), ""
                });
                bool confirming = _confirmHire == cand.Id && replaces != null;
                var b = UiFactory.Button("Hire", row, confirming
                    ? Loc.T("staff.fireConfirm", MathX.FormatMoney(StaffMarket.SeveranceCost(world, replaces)))
                    : Loc.T("staff.hireButton"), () =>
                {
                    // Replacing someone needs a second tap that shows who goes and the severance.
                    var who = StaffMarket.WouldReplace(world, team, cand.Role);
                    if (who != null && _confirmHire != cand.Id)
                    {
                        _confirmHire = cand.Id;
                        Shell.Refresh();
                        return;
                    }
                    _confirmHire = null;
                    StaffMarket.Hire(world, team, cand);
                    Shell.Toast(who != null ? Loc.T("staff.hiredReplacing", cand.FullName, who.FullName) : Loc.T("staff.hired", cand.FullName, Loc.T("role." + cand.Role)));
                    Shell.Refresh();
                }, confirming ? Theme.CurrentAccent : Theme.Accent, Theme.FontSmall - 4);
                UiFactory.InColumn(b, cw, cw.Length - 1);
            }
            if (replaces != null && _confirmHire != null)
                Ui.Note(detail, Theme.WarnText(Loc.T("staff.hireReplaceConfirm", replaces.LastName, MathX.FormatMoney(StaffMarket.SeveranceCost(world, replaces)))));
            Ui.Line(detail, Loc.T("staff.currentLevel"), current >= 0 ? Fmt.Rating(current) : Theme.GoodText(Loc.T("staff.newRole")));
            Ui.Note(detail, "<b>" + Loc.T("staff.whatFor") + "</b>  " + Loc.T("roleinfo." + sel));
            Ui.Note(detail, "<b>" + Loc.T("staff.impact") + "</b>  " + Loc.T("roleimpact." + sel));
            Ui.Note(detail, Loc.T("staff.advice." + coverage[sel]));
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
                    me.Job != null && me.Job.Role == ManagerRole.OwnerManager ? Ui.Kpi(Loc.T("manager.ownerKpi"), Loc.T("manager.status")) : Ui.Kpi(me.Job != null ? me.Job.EndSeason.ToString() : "-", Loc.T("manager.until")));
                if (me.Job != null)
                {
                    // The salary in plain sight when employed; the full sentence (employer, autonomy) folded.
                    var org = world.GetOrganisation(me.Job.OrganisationId);
                    bool owner = me.Job.Role == ManagerRole.OwnerManager;
                    if (!owner) Ui.Line(card, Loc.T("staff.salary"), MathX.FormatMoney(me.Job.SalaryPerSeason), true);
                    string role = owner ? Loc.T("manager.owner", org != null ? org.Name : "") :
                        Loc.T("manager.employed", org != null ? org.Name : "", me.Job.EndSeason, MathX.FormatMoney(me.Job.SalaryPerSeason), Loc.T("autonomy." + me.Job.Autonomy));
                    Ui.Note(card, role);
                }
            }
            if (team == null) return;
            var pers = team.Personality;
            if (pers != null)
            {
                int teams = world.TeamsInCategory(team.CategoryId).Count;
                var card = Ui.Card(list, Loc.T("objective.title"));
                Ui.Line(card, Loc.T("objective.current"), "<b>" + Loc.T("objective." + pers.Objective) + "</b>");
                UiFactory.Paragraph(card, Theme.Dim(Loc.T("objective.target", TeamAI.TargetPosition(pers.Objective, teams))), Theme.FontSmall - 2);
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
