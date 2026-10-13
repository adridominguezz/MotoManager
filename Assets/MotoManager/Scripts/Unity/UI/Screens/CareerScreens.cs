using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// "My career" summary (UX spec §3.20), one screen: on the left the manager (money, salary, fame; the career so far
    /// behind a link) over the five skills; on the right the job (at most five lines and the owners' confidence, the
    /// advice behind "i", resigning behind "...") and, in the last season of a contract, the renewal with the screen's
    /// primary. Without a job: see offers (primary) or found a team.
    /// </summary>
    public class CareerSummaryPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.career.summary"); } }

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var m = world.PlayerManager;
            if (m == null) return;
            var job = m.Job;
            var cols = Ui.Columns12(content, 6, 6);
            var left = Ui.Rows(cols[0], 0.46f, 0.54f);

            var me = Wp4Ui.ListIn(Ui.Tile(left[0], m.FullName, null, Theme.Caps(Loc.T("background." + m.BackgroundId))), 8);
            Ui.Kpis(me,
                Ui.Kpi(MathX.FormatMoney(m.PersonalWealth), Loc.T("mycareer.wealth")),
                Ui.Kpi(job != null && job.Role == ManagerRole.TeamManager ? MathX.FormatMoney(job.SalaryPerSeason) : "-", Loc.T("mycareer.salary")),
                Ui.Kpi(Fmt.Rating(m.Reputation), Loc.T("mycareer.fame")));
            Ui.Ghost(me, Loc.T("mycareer.history"), () => HistorySheet(m));

            var skills = Wp4Ui.ListIn(Ui.Tile(left[1], Loc.T("mycareer.skills")), 4);
            Skill(skills, "mycareer.skill.negotiation", m.Negotiation);
            Skill(skills, "mycareer.skill.sporting", m.SportingManagement);
            Skill(skills, "mycareer.skill.finance", m.Finance);
            Skill(skills, "mycareer.skill.relations", m.Relations);
            Skill(skills, "mycareer.skill.commercial", m.Commercial);

            if (job == null)
            {
                NoJob(cols[1]);
                return;
            }
            bool renewalDue = job.Role == ManagerRole.TeamManager && job.EndSeason <= world.Season;
            var right = renewalDue ? Ui.Rows(cols[1], 0.62f, 0.38f) : new[] { cols[1] };
            JobTile(right[0], world, m, job);
            if (renewalDue) RenewalTile(right[1], world, m);
        }

        private static void Skill(Transform list, string key, double v)
        {
            Ui.Bar(list, Loc.T(key), v / 100.0, "<b>" + v.ToString("0") + "</b>", Ui.RatingColor(v), 44);
        }

        private void NoJob(RectTransform area)
        {
            var body = Ui.Tile(area, Loc.T("mycareer.job"), null, null, 0, null, Wp4Ui.Info(Loc.T("mycareer.job"), Loc.T("mycareer.noJob"), Loc.T("mycareer.noJobInfo")));
            Ui.Empty(Wp4Ui.ListIn(Wp4Ui.Below(body, 0, Ui.Grid.Cta + 8)), UiIcons.Career, Loc.T("ux.career.noJob"));
            var bc = Ui.Columns12(Wp4Ui.BottomStrip(body, Ui.Grid.Cta), 5, 7);
            Ui.Secondary(bc[0], Loc.T("mycareer.found"), () => Shell.Show<CareerFoundPage>());
            Wp4Ui.Chevron(Ui.PrimaryCTA(bc[1], Loc.T("mycareer.seeOffers"), () => Shell.Show<CareerOffersPage>()));
        }

        private void JobTile(RectTransform area, WorldState world, ManagerProfile m, ManagerContract job)
        {
            var team = world.GetTeam(job.TeamId);
            var org = world.GetOrganisation(job.OrganisationId);
            bool hired = job.Role == ManagerRole.TeamManager;
            double conf = hired ? ManagerCareer.CurrentConfidence(world, Game.Db) : 0;
            string advice = hired ? Loc.T(conf >= 55 ? "mycareer.conf.good" : conf >= ManagerCareer.SackBelow + 5 ? "mycareer.conf.mid" : "mycareer.conf.bad") : null;
            var body = Ui.Tile(area, Loc.T("mycareer.job"), null, null, 0, null,
                Wp4Ui.Info(Loc.T("mycareer.job"), advice, Loc.T("mycareer.role." + job.Role), Loc.T("ux.career.resignInfo")));
            // Resigning: a small two-tap button in the header's right corner (left of the "i").
            var head = body.parent.Find("Head") as RectTransform;
            if (head != null)
            {
                var title = head.Find("Title") as RectTransform;
                if (title != null) title.offsetMax -= new Vector2(212, 0);
                var holder = UiFactory.Rect("Resign", head);
                holder.anchorMin = holder.anchorMax = new Vector2(1, 0.5f);
                holder.pivot = new Vector2(1, 0.5f);
                holder.sizeDelta = new Vector2(200, 40);
                holder.anchoredPosition = new Vector2(-Ui.Grid.Hit - 8, 0);
                var b = Ui.Danger(holder, Loc.T("mycareer.resign"), Loc.T("mycareer.resignConfirm"), "career.resign", () =>
                {
                    ManagerCareer.Resign(world, Game.Db);
                    Shell.Toast(Loc.T("mycareer.resigned"));
                    Shell.Refresh();
                }, null);
                var t = b.GetComponentInChildren<UnityEngine.UI.Text>();
                if (t != null) Ui.Fit(t, Theme.TextCaption);
            }
            var list2 = Wp4Ui.ListIn(body, 2);
            Ui.Line(list2, Loc.T("manager.team"), team != null ? "<b>" + team.Name + "</b>" : "-");
            Ui.Line(list2, Loc.T("mycareer.role"), Loc.T("mycareer.role." + job.Role));
            if (hired)
            {
                Ui.Line(list2, Loc.T("manager.until"), job.EndSeason.ToString());
                Ui.Line(list2, Loc.T("mycareer.autonomy"), Loc.T("autonomy." + job.Autonomy));
                double rel = m.RelationWith(job.OrganisationId);
                Ui.Line(list2, Loc.T("mycareer.relation"), rel >= 10 ? Theme.GoodText(Loc.T("mycareer.relation.good")) : rel <= -10 ? Theme.BadText(Loc.T("mycareer.relation.bad")) : Loc.T("mycareer.relation.neutral"));
                Ui.Bar(list2, Loc.T("review.confidence"), conf / 100.0, "<b>" + conf.ToString("0") + "%</b>",
                    conf >= 55 ? Theme.Good : conf >= ManagerCareer.SackBelow + 5 ? Theme.Warning : Theme.Bad, 44);
            }
            else
            {
                Ui.Line(list2, Loc.T("manager.organisation"), org != null ? org.Name : "-");
                if (org != null) Ui.Line(list2, Loc.T("finance.cash"), Theme.Money(org.Cash));
            }
        }

        private void RenewalTile(RectTransform area, WorldState world, ManagerProfile m)
        {
            var renewal = ManagerCareer.RenewalOffer(world, Game.Db);
            var body = Ui.Tile(area, Loc.T("mycareer.renewal"), null, null, 0, null, Wp4Ui.Info(Loc.T("mycareer.renewal"),
                renewal != null ? Loc.T("mycareer.renewalOffer", MathX.FormatMoney(renewal.SalaryPerSeason), renewal.Seasons, Loc.T("autonomy." + renewal.Autonomy)) : Loc.T("mycareer.noRenewal"),
                Loc.T("mycareer.renewalNote")));
            var list = Wp4Ui.ListIn(Wp4Ui.Below(body, 0, renewal != null ? Ui.Grid.Cta + 8 : 0), 4);
            if (renewal == null)
            {
                Ui.Alert(list, Loc.T("ux.career.noRenewal"));
                return;
            }
            Ui.Line(list, Loc.T("mycareer.salary"), "<b>" + MathX.FormatMoney(renewal.SalaryPerSeason) + "</b>  " +
                Theme.Dim(Loc.T("ux.career.renewalTerms", renewal.Seasons, Loc.T("autonomy." + renewal.Autonomy))));
            var bc = Ui.Columns12(Wp4Ui.BottomStrip(body, Ui.Grid.Cta), 4, 8);
            Ui.PrimaryCTA(bc[1], Loc.T("mycareer.acceptRenewal"), () =>
            {
                if (ManagerCareer.AcceptRenewal(world, Game.Db)) Shell.Toast(Loc.T("mycareer.renewed", m.Job.EndSeason));
                Shell.Refresh();
            });
        }

        private void HistorySheet(ManagerProfile m)
        {
            Ui.Sheet(Loc.T("mycareer.history"), list =>
            {
                if (m.History.Count == 0) Ui.Empty(list, UiIcons.Career, Loc.T("ux.career.noHistory"));
                for (int i = m.History.Count - 1; i >= 0; i--)
                {
                    var h = m.History[i];
                    var cat = Game.Db.GetCategory(h.CategoryId);
                    string seasons = h.FromSeason == h.ToSeason ? h.FromSeason.ToString() : h.FromSeason + "–" + h.ToSeason;
                    string end = string.IsNullOrEmpty(h.EndReason) ? Theme.GoodText(Loc.T("mycareer.current")) : Loc.Or("mycareer.reason." + h.EndReason, h.EndReason);
                    Ui.Item(list, seasons + "   " + (h.TeamName ?? ""), (cat != null ? cat.ShortName + " · " : "") + end);
                }
            }, null, null, 0.56f);
        }
    }

    /// <summary>
    /// Job offers (UX spec §3.20): a tile per team that would hire the manager now (team, category, salary as the key
    /// number, objective). A tile opens the full terms with ACEPTAR (two taps when it breaks a running contract).
    /// </summary>
    public class CareerOffersPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.career.offers"); } }

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var m = world.PlayerManager;
            if (m == null) return;
            var offers = ManagerCareer.Offers(world, Game.Db);
            bool breaking = m.Job != null && m.Job.Role == ManagerRole.TeamManager && m.Job.EndSeason > world.Season;
            bool owner = m.Job != null && m.Job.Role == ManagerRole.OwnerManager;
            Ui.Section(list, Loc.T("mycareer.offersTitle", offers.Count), null,
                Wp4Ui.Info(Loc.T("mycareer.offersTitle", offers.Count), Loc.T("mycareer.offersInfo"), owner ? Loc.T("mycareer.ownerNote") : null,
                    breaking ? Loc.T("mycareer.breakNote") : null, offers.Count == 0 ? Loc.T("mycareer.noOffers") : null));
            if (breaking) Ui.Alert(list, Loc.T("ux.career.breakAlert"));
            if (offers.Count == 0)
            {
                Ui.Empty(list, UiIcons.Career, Loc.T("ux.career.noOffers"));
                return;
            }
            RectTransform[] cols = null;
            int k = 0;
            foreach (var o in offers)
            {
                var offer = o;
                var t = world.GetTeam(o.TeamId);
                if (t == null) continue;
                var cat = Game.Db.GetCategory(o.CategoryId);
                if (k % 3 == 0) cols = Ui.Columns12(UiFactory.Row(list, 200), 4, 4, 4);
                var body = Ui.Tile(cols[k % 3], t.Name, () => OfferSheet(world, t, cat, offer, breaking), cat != null ? cat.ShortName : null);
                var v = Ui.Label(body, MathX.FormatMoney(o.SalaryPerSeason), Theme.TextKey, TextAnchor.MiddleCenter, Theme.Timing, FontStyle.BoldAndItalic);
                UiFactory.Place(v.rectTransform, 0, 0.36f, 1, 1);
                var s = Ui.Label(body, Theme.Caps(Loc.T("objective." + o.Objective) + " · " + Loc.T("ux.career.perSeason")), Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);
                UiFactory.Place(s.rectTransform, 0, 0, 1, 0.34f);
                k++;
            }
        }

        private void OfferSheet(WorldState world, Team t, Core.Data.ChampionshipCategory cat, JobOffer offer, bool breaking)
        {
            Ui.Sheet(t.Name, list =>
            {
                Ui.Line(list, Loc.T("manager.category"), cat != null ? cat.Name : offer.CategoryId);
                Ui.Line(list, Loc.T("career.col.objective"), Loc.T("objective." + offer.Objective));
                Ui.Line(list, Loc.T("mycareer.salary"), "<b>" + MathX.FormatMoney(offer.SalaryPerSeason) + "</b>", true);
                Ui.Line(list, Loc.T("ux.career.seasons"), offer.Seasons.ToString());
                Ui.Line(list, Loc.T("mycareer.autonomy"), Loc.T("autonomy." + offer.Autonomy));
                Ui.Line(list, Loc.T("career.col.budget"), MathX.FormatMoney(offer.AssignedBudget));
                Ui.Line(list, Loc.T("ux.career.teamRep"), t.Reputation.ToString("0"));
                var org = world.GetOrganisation(offer.OrganisationId);
                if (org != null) Ui.Line(list, Loc.T("finance.owner"), Loc.T("owner." + org.Owner));
                Action accept = () =>
                {
                    Ui.CloseSheet();
                    var res = ManagerCareer.ChangeJob(world, Game.Db, offer);
                    Shell.Toast(res.Message, res.Ok);
                    if (res.Ok) Shell.Show<HubScreen>();
                    else Shell.Refresh();
                };
                if (breaking)
                {
                    UiFactory.Paragraph(list, Theme.WarnText(Loc.T("mycareer.breakNote")), Theme.TextBody);
                    Wp4Ui.TwoTapPrimary(UiFactory.Row(list, Ui.Grid.Cta), Loc.T("mycareer.accept"), Loc.T("mycareer.acceptConfirm"), accept);
                }
                else Ui.PrimaryCTA(list, Loc.T("mycareer.accept"), accept);
            }, null, null, 0.5f);
        }
    }

    /// <summary>
    /// The organisation the player owns (UX spec §3.20): on the left its money (cash, debt, the manager's wealth), the
    /// team picker, the cash-crisis alert and its ways out, and behind "..." the team setup and founding another team;
    /// on the right one view at a time: who runs the team, hiring a manager, moving category, selling, closing.
    /// </summary>
    public class CareerOrgPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.career.org"); } }
        private string _confirm;
        private string _teamId;
        private int _view;

        private bool Confirm(string key)
        {
            if (_confirm == key) { _confirm = null; return true; }
            _confirm = key;
            Shell.Refresh();
            return false;
        }

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var db = Game.Db;
            var m = world.PlayerManager;
            if (m == null) return;
            var org = ManagerCareer.OwnedOrganisation(world);
            if (org == null)
            {
                var c = Ui.Columns12(content, 3, 6);
                var body = Ui.Tile(c[1], Loc.T("tab.career.org"), null, null, 0, null, Wp4Ui.Info(Loc.T("tab.career.org"), Loc.T("mycareer.org.none")));
                Ui.Empty(Wp4Ui.ListIn(body), UiIcons.Career, Loc.T("ux.org.none"), UiFactory.Primary(Loc.T("mycareer.found"), () => Shell.Show<CareerFoundPage>()));
                return;
            }
            var teams = OrganisationService.Teams(world, org);
            Team selected = null;
            foreach (var t in teams) if (t.Id == _teamId) selected = t;
            if (selected == null && teams.Count > 0) selected = teams[0];
            if (selected != null) _teamId = selected.Id;

            var cols = Ui.Columns12(content, 5, 7);
            OrgTile(cols[0], world, db, m, org, teams);
            if (selected == null) return;
            var views = new[] { Loc.T("ux.org.view.run"), Loc.T("ux.org.view.hire"), Loc.T("ux.org.view.move"), Loc.T("ux.org.view.sell"), Loc.T("ux.org.view.close") };
            Ui.Segmented(Wp4Ui.ListIn(Wp4Ui.TopStrip(cols[1], Ui.Grid.Hit), 0), views, _view, i => { _view = i; _confirm = null; Shell.Refresh(); });
            var area = Wp4Ui.Below(cols[1], Ui.Grid.Hit + Ui.Grid.Gap);
            switch (_view)
            {
                case 1: HireView(area, world, db, m, selected); break;
                case 2: MoveView(area, world, db, selected); break;
                case 3: SellView(area, world, db, org, selected); break;
                case 4: CloseView(area, world, db, selected); break;
                default: RunView(area, world, db, m, selected); break;
            }
        }

        private void OrgTile(RectTransform area, WorldState world, Core.Data.GameDatabase db, ManagerProfile m, Organisation org, List<Team> teams)
        {
            double debt = 0;
            foreach (var l in org.Loans) debt += l.Outstanding;
            var alert = OrganisationService.Alert(world, db, org);
            var body = Ui.Tile(area, org.Name, null, Theme.Caps(Loc.T("mycareer.org.owner")), 0, null, Wp4Ui.Info(org.Name, Loc.T("mycareer.org.info"),
                alert == CashAlert.Insolvent ? Loc.T("org.crisis.insolvent") : alert == CashAlert.Low ? Loc.T("org.crisis.low") : null,
                alert == CashAlert.Insolvent ? Loc.T("org.crisis.forced") : null));
            Wp4Ui.HeaderMore(body, () => Ui.Sheet(org.Name, list =>
            {
                var l = (RectTransform)list;
                Wp4Ui.Chevron(Ui.Secondary(l, Loc.T("ux.org.setup"), () => { Ui.CloseSheet(); Shell.Show<TeamSetupPage>(); }));
                Wp4Ui.Chevron(Ui.Secondary(l, Loc.T("livery.edit"), () =>
                {
                    Ui.CloseSheet();
                    LiveryEditorPage.PendingTeamId = _teamId;
                    Shell.Show<LiveryEditorPage>();
                }));
                Wp4Ui.Chevron(Ui.Secondary(l, Loc.T("ux.org.foundAnother"), () => { Ui.CloseSheet(); Shell.Show<CareerFoundPage>(); }));
            }, null, null, 0.46f));
            var list2 = Wp4Ui.ListIn(body, 8);
            Ui.Kpis(list2,
                Ui.Kpi(Theme.Money(org.Cash), Loc.T("finance.cash")),
                Ui.Kpi(debt > 0 ? MathX.FormatMoney(debt) : "-", Loc.T("mycareer.org.debt")),
                Ui.Kpi(MathX.FormatMoney(m.PersonalWealth), Loc.T("mycareer.wealth")));
            if (teams.Count > 1)
            {
                var names = new List<string>();
                int idx = 0;
                for (int i = 0; i < teams.Count; i++)
                {
                    var cat = db.GetCategory(teams[i].CategoryId);
                    names.Add(teams[i].Name + (cat != null ? " · " + cat.ShortName : ""));
                    if (teams[i].Id == _teamId) idx = i;
                }
                Wp4Ui.CaptionedPicker(list2, Loc.T("manager.team"), names, idx, i => { _teamId = teams[i].Id; _confirm = null; Shell.Refresh(); });
            }
            if (alert == CashAlert.None) return;
            Ui.Alert(list2, Loc.T(alert == CashAlert.Insolvent ? "ux.org.insolvent" : "ux.org.low"), alert == CashAlert.Insolvent);
            double loan = OrganisationService.EmergencyLoanAmount(world, org);
            double need = Math.Max(0, -org.Cash) + 500000;
            double inject = Math.Min(m.PersonalWealth, Math.Round(need / 10000) * 10000);
            if (loan <= 0 && inject <= 0) return;
            var row = UiFactory.Row(list2, Ui.Grid.Button);
            var bc = loan > 0 && inject > 0 ? Ui.Columns12(row, 6, 6) : new[] { row };
            int k = 0;
            if (loan > 0)
                Wp4Ui.TwoTapSecondary(bc[k++], Loc.T("org.crisis.loan", MathX.FormatMoney(loan)), Loc.T("org.crisis.loanConfirm", (OrganisationService.EmergencyRate * 100).ToString("0")), () =>
                {
                    Shell.Toast(OrganisationService.TakeEmergencyLoan(world, org) ? Loc.T("org.crisis.loanTaken") : Loc.T("org.crisis.noLoan"));
                    Shell.Refresh();
                });
            if (inject > 0)
                Wp4Ui.TwoTapSecondary(bc[k], Loc.T("org.crisis.inject", MathX.FormatMoney(inject)), Loc.T("org.crisis.injectConfirm"), () =>
                {
                    OrganisationService.Inject(world, org, inject);
                    Shell.Refresh();
                });
        }

        /// <summary>Who runs the team, and taking charge of it again (the primary, two taps).</summary>
        private void RunView(RectTransform area, WorldState world, Core.Data.GameDatabase db, ManagerProfile m, Team team)
        {
            var cat = db.GetCategory(team.CategoryId);
            bool mine = m.Job != null && m.Job.TeamId == team.Id;
            bool breaking = m.Job != null && m.Job.Role == ManagerRole.TeamManager && m.Job.EndSeason > world.Season;
            var body = Ui.Tile(area, team.Name, null, cat != null ? Theme.Caps(cat.Name) : null, 0, null,
                breaking && !mine ? Wp4Ui.Info(team.Name, Loc.T("mycareer.breakNote")) : null);
            var list = Wp4Ui.ListIn(Wp4Ui.Below(body, 0, mine ? 0 : Ui.Grid.Cta + 8), 4);
            var boss = world.GetManager(team.ManagerId);
            if (mine) Ui.Line(list, Loc.T("mycareer.org.runBy"), "<b>" + Loc.T("mycareer.org.you") + "</b>");
            else if (boss != null && boss.Job != null)
            {
                Ui.Line(list, Loc.T("mycareer.org.runBy"), "<b>" + boss.FullName + "</b>  " + Theme.Dim(Loc.T("mycareer.fame") + " " + Fmt.Rating(boss.Reputation)));
                Ui.Line(list, Loc.T("mycareer.salary"), MathX.FormatMoney(boss.Job.SalaryPerSeason));
            }
            else Ui.Line(list, Loc.T("mycareer.org.runBy"), "-");
            Ui.Line(list, Loc.T("org.riders"), team.RiderIds.Count.ToString());
            if (team.PendingCategoryId != null)
            {
                var to = db.GetCategory(team.PendingCategoryId);
                Ui.Line(list, Loc.T("org.moving"), Theme.WarnText(to != null ? to.Name : team.PendingCategoryId));
            }
            var edited = team;
            Wp4Ui.Chevron(Ui.Secondary(list, Loc.T("livery.edit"), () =>
            {
                LiveryEditorPage.PendingTeamId = edited.Id;
                Shell.Show<LiveryEditorPage>();
            }));
            if (mine) return;
            if (breaking) Ui.Alert(list, Loc.T("ux.career.breakAlert"));
            var t = team;
            var bc = Ui.Columns12(Wp4Ui.BottomStrip(body, Ui.Grid.Cta), 4, 8);
            Wp4Ui.TwoTapPrimary(bc[1], Loc.T("mycareer.org.run"), Loc.T("mycareer.org.runConfirm", MathX.FormatMoney(ManagerCareer.SeveranceFor(world, boss))), () =>
            {
                var res = ManagerCareer.RunOwnTeam(world, Game.Db, t);
                Shell.Toast(res.Message, res.Ok);
                if (res.Ok) Shell.Show<HubScreen>(); else Shell.Refresh();
            });
        }

        private void HireView(RectTransform area, WorldState world, Core.Data.GameDatabase db, ManagerProfile m, Team team)
        {
            var current = world.GetManager(team.ManagerId);
            double severance = ManagerCareer.SeveranceFor(world, current);
            bool mine = m.Job != null && m.Job.TeamId == team.Id;
            var body = Ui.Tile(area, Loc.T("mycareer.org.hireTitle"), null, Theme.Caps(team.Name), 0, null,
                Wp4Ui.Info(Loc.T("mycareer.org.hireTitle"), mine ? Loc.T("mycareer.org.youRunIt") : null,
                    severance > 0 && current != null ? Loc.T("mycareer.org.severance", current.FullName, MathX.FormatMoney(severance)) : null));
            if (mine)
            {
                Ui.Empty(Wp4Ui.ListIn(body), UiIcons.Person, Loc.T("ux.org.youRunIt"));
                return;
            }
            var list = Wp4Ui.ScrollIn(body, 6);
            if (severance > 0) Ui.Alert(list, Loc.T("ux.org.severance", MathX.FormatMoney(severance)));
            var rules = db.GetRules(team.CategoryId, world.Season);
            foreach (var c in ManagerMarket.Candidates(world, db, team, 5))
            {
                var cand = c;
                string key = "hire:" + c.Id;
                string detail = Loc.T("ux.org.candidate", c.SportingManagement.ToString("0"), c.Negotiation.ToString("0"), c.Finance.ToString("0"), world.Season - c.BirthYear);
                Ui.PersonRow(list, c.Reputation, c.FullName, detail, MathX.FormatMoney(CareerService.SalaryFor(rules, c, team)), null,
                    UiFactory.Act(_confirm == key ? Loc.T("mycareer.org.hireConfirm") : Loc.T("mycareer.org.hire"), () =>
                    {
                        if (!Confirm(key)) return;
                        var res = ManagerCareer.HireForOwnTeam(world, Game.Db, team, cand);
                        Shell.Toast(res.Message, res.Ok);
                        Shell.Refresh();
                    }));
            }
        }

        private void MoveView(RectTransform area, WorldState world, Core.Data.GameDatabase db, Team team)
        {
            var body = Ui.Tile(area, Loc.T("org.move.title"), null, null, 0, null, Wp4Ui.Info(Loc.T("org.move.title"), Loc.T("org.move.info"), Loc.T("org.move.note")));
            if (team.PendingCategoryId != null)
            {
                var to = db.GetCategory(team.PendingCategoryId);
                var l = Wp4Ui.ListIn(body, 8);
                Ui.Line(l, Loc.T("org.moving"), "<b>" + (to != null ? to.Name : team.PendingCategoryId) + "</b>");
                Ui.Secondary(l, Loc.T("org.move.cancel"), () =>
                {
                    var r = OrganisationService.RequestCategoryChange(world, db, team, null, null);
                    Shell.Toast(r.Message, r.Ok);
                    Shell.Refresh();
                });
                return;
            }
            var org = world.OrganisationOf(team);
            var have = new HashSet<string>();
            foreach (var t in OrganisationService.Teams(world, org)) have.Add(t.CategoryId);
            var w = new[] { 2.4f, 2f, 2f, 2.6f };
            Wp4Ui.Captions(Wp4Ui.TopStrip(body, 40), w, new[] { Loc.T("career.col.category"), Loc.T("org.move.cost"), Loc.T("org.move.refund"), Loc.T("career.col.verdict") }, true);
            var list = Wp4Ui.ScrollIn(Wp4Ui.Below(body, 44), 4);
            int i = 0;
            foreach (var cat in db.CategoriesOrdered())
            {
                if (!cat.Simulated || cat.IsCup || have.Contains(cat.Id)) continue;
                var q = OrganisationService.QuoteCategoryChange(world, db, team, cat.Id);
                var v = q.Viability != null ? q.Viability.Verdict : Viability.NotAvailable;
                bool selectable = q.PlaceAvailable && (v == Viability.Viable || v == Viability.Risky);
                var c = cat;
                var row = Wp4Ui.SelectRow(list, w, new[] { "<b>" + cat.ShortName + "</b>", Theme.Money(-(q.EntryCost + q.Bikes)), Theme.Money(q.OldPlaceRefund), "" }, i++, false, () =>
                    Ui.Sheet(Loc.T("org.move.title") + " · " + c.ShortName, l =>
                    {
                        Ui.Line(l, Loc.T("org.move.cost"), Theme.Money(-(q.EntryCost + q.Bikes)));
                        Ui.Line(l, Loc.T("org.move.refund"), Theme.Money(q.OldPlaceRefund));
                        Ui.Line(l, Loc.T("career.col.verdict"), !q.PlaceAvailable ? Theme.BadText(Loc.T("org.move.noPlace")) : Loc.T("viability." + v));
                        if (q.Viability != null) FoundingPanel.ReportLines(l, q.Viability);
                        if (!selectable) return;
                        Ui.Section(l, Loc.T("career.supplier", c.Name), null, Wp4Ui.Info(Loc.T("org.move.title"), Loc.T("org.move.note")));
                        foreach (var maker in CareerService.Suppliers(world, c.Id))
                        {
                            var mk = maker;
                            Ui.Secondary((RectTransform)l, mk.Name, () =>
                            {
                                Ui.CloseSheet();
                                var r = OrganisationService.RequestCategoryChange(world, db, team, c.Id, mk.Id);
                                Shell.Toast(r.Message, r.Ok);
                                Shell.Refresh();
                            });
                        }
                    }, null, null, 0.56f));
                Wp4Ui.CellPill(row, w, 3, !q.PlaceAvailable ? Loc.T("org.move.noPlace") : Loc.T("viability." + v),
                    !q.PlaceAvailable ? Theme.Bad : FoundingPanel.VerdictColor(v));
            }
            if (i == 0) Ui.Empty(list, null, Loc.T("ux.found.noCategories"));
        }

        private void SellView(RectTransform area, WorldState world, Core.Data.GameDatabase db, Organisation org, Team team)
        {
            bool between = OrganisationService.BetweenSeasons(Game);
            bool distress = org.Cash < 0;
            var v = OrganisationService.Value(world, db, team);
            double price = Math.Round(v.Total * (distress ? OrganisationService.DistressShare : 1) / 1000) * 1000;
            var body = Ui.Tile(area, Loc.T("org.sell.title"), null, null, 0, null, Wp4Ui.Info(Loc.T("org.sell.title"), Loc.T("org.sell.info"),
                org.TeamIds.Count == 1 ? Loc.T("org.sell.last") : null, !between && !distress ? Loc.T("org.e.season") : null));
            bool can = between || distress;
            var list = Wp4Ui.ListIn(Wp4Ui.Below(body, 0, can ? Ui.Grid.Button + 8 : 0), 2);
            Ui.Line(list, Loc.T("org.sell.place"), MathX.FormatMoney(v.GridPlace));
            Ui.Line(list, Loc.T("org.sell.facilities"), MathX.FormatMoney(v.Facilities));
            Ui.Line(list, Loc.T("org.sell.equipment"), MathX.FormatMoney(v.Equipment + v.Inventory));
            Ui.Line(list, Loc.T("org.sell.brand"), MathX.FormatMoney(v.Brand));
            Ui.Line(list, Loc.T("org.sell.price") + (distress ? " " + Theme.WarnText(Loc.T("org.sell.distress")) : ""), "<b>" + MathX.FormatMoney(price) + "</b>", true);
            if (org.TeamIds.Count == 1) Ui.Alert(list, Loc.T("ux.org.sellLast"));
            if (!can)
            {
                Ui.Alert(list, Loc.T("org.e.season"));
                return;
            }
            var t = team;
            var bc = Ui.Columns12(Wp4Ui.BottomStrip(body, Ui.Grid.Button), 6, 6);
            Ui.Danger(bc[1], Loc.T("org.sell.button"), Loc.T("org.sell.confirm", MathX.FormatMoney(price)), "org.sell." + t.Id, () =>
            {
                var res = OrganisationService.SellTeam(Game, t);
                Shell.Toast(res.Message, res.Ok);
                Session.Save();
                Shell.Show<HubScreen>();
            }, null);
        }

        private void CloseView(RectTransform area, WorldState world, Core.Data.GameDatabase db, Team team)
        {
            bool between = OrganisationService.BetweenSeasons(Game);
            var q = OrganisationService.ClosureCost(world, db, team);
            var body = Ui.Tile(area, Loc.T("org.close.title"));
            var list = Wp4Ui.ListIn(Wp4Ui.Below(body, 0, between ? Ui.Grid.Button + 8 : 0), 2);
            Ui.Line(list, Loc.T("org.close.riders"), Theme.Money(-q.RiderContracts));
            Ui.Line(list, Loc.T("org.close.staff"), Theme.Money(-q.StaffContracts));
            Ui.Line(list, Loc.T("org.close.refund"), Theme.Money(q.GridPlaceRefund));
            Ui.Line(list, Loc.T("org.close.net"), Theme.Money(q.Net), true);
            if (!between)
            {
                Ui.Alert(list, Loc.T("org.e.season"));
                return;
            }
            var t = team;
            var bc = Ui.Columns12(Wp4Ui.BottomStrip(body, Ui.Grid.Button), 6, 6);
            Ui.Danger(bc[1], Loc.T("org.close.button"), Loc.T("org.close.confirm"), "org.close." + t.Id, () =>
            {
                var res = OrganisationService.CloseTeam(Game, t);
                Shell.Toast(res.Message, res.Ok);
                Session.Save();
                Shell.Show<HubScreen>();
            }, null);
        }
    }

    /// <summary>A new team for the organisation the player already owns (between seasons).</summary>
    public class ExpansionState
    {
        public double LoanShare;
        public string Category, Supplier, TeamName = "";
        public string Primary, Secondary;
    }

    /// <summary>
    /// Founding a team (between seasons; reached from the career summary, the hub and the organisation's "..."): the
    /// founding panel, or for an owner the expansion of the organisation with the same layout.
    /// </summary>
    public class CareerFoundPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.career.found"); } }
        public override bool HiddenTab { get { return true; } }
        private readonly FoundingState _state = new FoundingState();
        private readonly ExpansionState _exp = new ExpansionState();

        private void BuildExpansion(RectTransform content, WorldState world, Organisation org)
        {
            var db = Game.Db;
            if (!OrganisationService.BetweenSeasons(Game))
            {
                var c = Ui.Columns12(content, 3, 6);
                var wait = Ui.Tile(c[1], Loc.T("org.expand.title"), null, null, 0, null, Wp4Ui.Info(Loc.T("org.expand.title"), Loc.T("org.expand.later")));
                var l = Wp4Ui.ListIn(wait);
                Ui.Empty(l, UiIcons.Flag, Loc.T("ux.org.expandLater"));
                Ui.Line(l, Loc.T("finance.cash"), Theme.Money(org.Cash));
                return;
            }
            var terms = OrganisationService.BankTerms(world, org);
            double loan = Math.Round(terms.MaxAmount * _exp.LoanShare / 10000) * 10000;
            var cols = Ui.Columns12(content, 6, 6);
            const float capH = 216;
            var money = Wp4Ui.ListIn(Ui.Tile(Wp4Ui.TopStrip(cols[0], capH), Loc.T("org.expand.title"), null, null, 0, null,
                Wp4Ui.Info(Loc.T("org.expand.title"), Loc.T("org.expand.info"),
                    Loc.T("career.loan") + ": <b>" + MathX.FormatMoney(loan) + "</b>  " + Loc.T("career.loanTerms", MathX.FormatMoney(terms.MaxAmount), (terms.Rate * 100).ToString("0.0"), terms.Seasons))), 8);
            FoundingPanel.LoanSegmented(money, _exp.LoanShare, s => { _exp.LoanShare = s; Shell.Refresh(); });
            Ui.Line(money, Theme.Caps(Loc.T("ux.found.total")), "<b>" + Theme.Money(org.Cash + loan) + "</b>  " +
                Theme.Dim(Loc.T("ux.found.cashSplit", MathX.FormatMoney(org.Cash), MathX.FormatMoney(loan))), true);
            var reports = OrganisationService.ExpansionOptions(world, db, org, loan);
            FoundingPanel.CategoryTile(Wp4Ui.Below(cols[0], capH + Ui.Grid.Gap), db, reports, _exp.Category, id =>
            {
                _exp.Category = id;
                _exp.Supplier = null;
                Shell.Refresh();
            });
            if (_exp.Primary == null)
            {
                var first = OrganisationService.Teams(world, org);
                _exp.Primary = first.Count > 0 ? first[0].Colors.Primary : "#E10600";
                _exp.Secondary = first.Count > 0 ? first[0].Colors.Secondary : "#FFFFFF";
            }
            ViabilityReport chosen = null;
            foreach (var r in reports) if (r.CategoryId == _exp.Category) chosen = r;
            FoundingPanel.IdentityTile(cols[1], world, db, _exp.Category, _exp.Supplier, s => { _exp.Supplier = s; Shell.Refresh(); },
                _exp.TeamName, v => _exp.TeamName = v, org.Name, _exp.Primary, c => { _exp.Primary = c; Shell.Refresh(); }, _exp.Secondary, c => { _exp.Secondary = c; Shell.Refresh(); },
                chosen, null, Loc.T("org.expand.confirm"), () =>
                {
                    var res = OrganisationService.Expand(Game, new ExpansionPlan
                    {
                        CategoryId = _exp.Category, ManufacturerId = _exp.Supplier, TeamName = _exp.TeamName, Loan = loan, PrimaryColor = _exp.Primary, SecondaryColor = _exp.Secondary
                    });
                    Shell.Toast(res.Message, res.Ok);
                    if (res.Ok)
                    {
                        _exp.Category = null;
                        Session.Save();
                        Shell.Show<CareerOrgPage>();
                    }
                    else Shell.Refresh();
                }, Loc.T("org.expand.note"));
        }

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var m = world.PlayerManager;
            if (m == null) return;
            var owned = ManagerCareer.OwnedOrganisation(world);
            if (owned != null)
            {
                BuildExpansion(content, world, owned);
                return;
            }
            if (!ManagerCareer.CanFoundNow(world, Game))
            {
                var c = Ui.Columns12(content, 3, 6);
                var wait = Ui.Tile(c[1], Loc.T("tab.career.found"), null, null, 0, null, Wp4Ui.Info(Loc.T("tab.career.found"), Loc.T("mycareer.foundLater")));
                var l = Wp4Ui.ListIn(wait);
                Ui.Empty(l, UiIcons.Flag, Loc.T("ux.career.foundLater"));
                Ui.Line(l, Loc.T("mycareer.wealth"), MathX.FormatMoney(m.PersonalWealth));
                return;
            }
            FoundingPanel.Build(content, Game, m, _state, Shell.Refresh, res =>
            {
                if (res.Ok)
                {
                    Shell.Toast(Loc.T("mycareer.founded", res.Team.Name));
                    Session.Save();
                    Shell.Show<TeamSetupPage>();
                }
                else Shell.Refresh();
            });
        }
    }
}
