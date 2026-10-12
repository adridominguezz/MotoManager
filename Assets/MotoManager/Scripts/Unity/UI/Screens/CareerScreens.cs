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
    /// "My career": the manager's money, salary, fame and skills, the current job and what the owners think, the
    /// renewal in the last season of a contract, and the career so far.
    /// </summary>
    public class CareerSummaryPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.career.summary"); } }
        private bool _confirmResign;

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var m = world.PlayerManager;
            if (m == null) return;
            var job = m.Job;
            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);

            var me = Ui.Card(leftCol, m.FullName, Loc.T("background." + m.BackgroundId));
            Ui.Kpis(me,
                Ui.Kpi(MathX.FormatMoney(m.PersonalWealth), Loc.T("mycareer.wealth")),
                Ui.Kpi(job != null && job.Role == ManagerRole.TeamManager ? MathX.FormatMoney(job.SalaryPerSeason) : "-", Loc.T("mycareer.salary")),
                Ui.Kpi(Fmt.Rating(m.Reputation), Loc.T("mycareer.fame")));
            Ui.Bar(me, Loc.T("mycareer.skill.negotiation"), m.Negotiation / 100.0, m.Negotiation.ToString("0"), Ui.RatingColor(m.Negotiation), 32);
            Ui.Bar(me, Loc.T("mycareer.skill.sporting"), m.SportingManagement / 100.0, m.SportingManagement.ToString("0"), Ui.RatingColor(m.SportingManagement), 32);
            Ui.Bar(me, Loc.T("mycareer.skill.finance"), m.Finance / 100.0, m.Finance.ToString("0"), Ui.RatingColor(m.Finance), 32);
            Ui.Bar(me, Loc.T("mycareer.skill.relations"), m.Relations / 100.0, m.Relations.ToString("0"), Ui.RatingColor(m.Relations), 32);
            Ui.Bar(me, Loc.T("mycareer.skill.commercial"), m.Commercial / 100.0, m.Commercial.ToString("0"), Ui.RatingColor(m.Commercial), 32);

            // The job.
            var jobCard = Ui.Card(rightCol, Loc.T("mycareer.job"));
            if (job == null)
            {
                UiFactory.Paragraph(jobCard, "<b>" + Loc.T("mycareer.noJob") + "</b>", Theme.FontNormal);
                Ui.Note(jobCard, Loc.T("mycareer.noJobInfo"));
                UiFactory.ButtonRow(jobCard, 60,
                    UiFactory.Primary(Loc.T("mycareer.seeOffers"), () => Shell.Show<CareerOffersPage>()),
                    UiFactory.Act(Loc.T("mycareer.found"), () => Shell.Show<CareerFoundPage>()));
            }
            else
            {
                var team = world.GetTeam(job.TeamId);
                var org = world.GetOrganisation(job.OrganisationId);
                Ui.Line(jobCard, Loc.T("manager.team"), team != null ? "<b>" + team.Name + "</b>" : "-");
                Ui.Line(jobCard, Loc.T("mycareer.role"), Loc.T("mycareer.role." + job.Role));
                if (job.Role == ManagerRole.TeamManager)
                {
                    Ui.Line(jobCard, Loc.T("manager.until"), job.EndSeason.ToString());
                    Ui.Line(jobCard, Loc.T("mycareer.autonomy"), Loc.T("autonomy." + job.Autonomy));
                    double rel = m.RelationWith(job.OrganisationId);
                    Ui.Line(jobCard, Loc.T("mycareer.relation"), rel >= 10 ? Theme.GoodText(Loc.T("mycareer.relation.good")) : rel <= -10 ? Theme.BadText(Loc.T("mycareer.relation.bad")) : Loc.T("mycareer.relation.neutral"));
                    double conf = ManagerCareer.CurrentConfidence(world, Game.Db);
                    Ui.Bar(jobCard, Loc.T("review.confidence"), conf / 100.0, conf.ToString("0") + "%",
                        conf >= 55 ? Theme.Good : conf >= ManagerCareer.SackBelow + 5 ? Theme.Warning : Theme.Bad, 36);
                    Ui.Note(jobCard, Loc.T(conf >= 55 ? "mycareer.conf.good" : conf >= ManagerCareer.SackBelow + 5 ? "mycareer.conf.mid" : "mycareer.conf.bad"));
                }
                else
                {
                    Ui.Line(jobCard, Loc.T("manager.organisation"), org != null ? org.Name : "-");
                    if (org != null) Ui.Line(jobCard, Loc.T("finance.cash"), Theme.Money(org.Cash));
                }

                // Renewal in the last season.
                if (job.Role == ManagerRole.TeamManager && job.EndSeason <= world.Season)
                {
                    var renewal = ManagerCareer.RenewalOffer(world, Game.Db);
                    var rc = Ui.Card(rightCol, Loc.T("mycareer.renewal"));
                    if (renewal == null) Ui.Note(rc, Theme.WarnText(Loc.T("mycareer.noRenewal")));
                    else
                    {
                        UiFactory.Paragraph(rc, Loc.T("mycareer.renewalOffer", MathX.FormatMoney(renewal.SalaryPerSeason), renewal.Seasons, Loc.T("autonomy." + renewal.Autonomy)), Theme.FontSmall);
                        UiFactory.ButtonRow(rc, 60, UiFactory.Primary(Loc.T("mycareer.acceptRenewal"), () =>
                        {
                            if (ManagerCareer.AcceptRenewal(world, Game.Db)) Shell.Toast(Loc.T("mycareer.renewed", m.Job.EndSeason));
                            Shell.Refresh();
                        }));
                    }
                    Ui.Note(rc, Loc.T("mycareer.renewalNote"));
                }

                UiFactory.ButtonRow(jobCard, 54, UiFactory.Option(_confirmResign ? Loc.T("mycareer.resignConfirm") : Loc.T("mycareer.resign"), _confirmResign, () =>
                {
                    if (!_confirmResign)
                    {
                        _confirmResign = true;
                        Shell.Refresh();
                        return;
                    }
                    _confirmResign = false;
                    ManagerCareer.Resign(world, Game.Db);
                    Shell.Toast(Loc.T("mycareer.resigned"));
                    Shell.Refresh();
                }));
            }

            // Career so far.
            var hist = Ui.Card(rightCol, Loc.T("mycareer.history"));
            var w = new[] { 1.6f, 3.6f, 1.4f, 2.4f };
            UiFactory.TableRow(hist, 32, w, new[] { Loc.T("col.season"), Loc.T("col.team"), Loc.T("manager.category"), Loc.T("mycareer.end") }, true);
            for (int i = m.History.Count - 1; i >= 0 && i >= m.History.Count - 6; i--)
            {
                var h = m.History[i];
                var cat = Game.Db.GetCategory(h.CategoryId);
                string seasons = h.FromSeason == h.ToSeason ? h.FromSeason.ToString() : h.FromSeason + "–" + h.ToSeason;
                UiFactory.TableRow(hist, 34, w, new[] { seasons, h.TeamName ?? "", cat != null ? cat.ShortName : "", string.IsNullOrEmpty(h.EndReason) ? Theme.GoodText(Loc.T("mycareer.current")) : Loc.Or("mycareer.reason." + h.EndReason, h.EndReason) });
            }
        }
    }

    /// <summary>Teams that would hire the manager now; taking one moves the player there straight away.</summary>
    public class CareerOffersPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.career.offers"); } }
        private string _confirm;

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var m = world.PlayerManager;
            if (m == null) return;
            var offers = ManagerCareer.Offers(world, Game.Db);
            var card = Ui.Card(list, Loc.T("mycareer.offersTitle", offers.Count), Loc.T("mycareer.offersInfo"));
            bool breaking = m.Job != null && m.Job.Role == ManagerRole.TeamManager && m.Job.EndSeason > world.Season;
            if (breaking) Ui.Note(card, Theme.WarnText(Loc.T("mycareer.breakNote")));
            if (m.Job != null && m.Job.Role == ManagerRole.OwnerManager) Ui.Note(card, Loc.T("mycareer.ownerNote"));
            foreach (var o in offers)
            {
                var offer = o;
                var t = world.GetTeam(o.TeamId);
                var cat = Game.Db.GetCategory(o.CategoryId);
                if (t == null) continue;
                string detail = (cat != null ? cat.Name : "") + " · " + Loc.T("objective." + o.Objective) + " · " + Loc.T("mycareer.offerTerms", o.Seasons, Loc.T("autonomy." + o.Autonomy), MathX.FormatMoney(o.AssignedBudget));
                bool confirm = _confirm == o.TeamId;
                Ui.Item(card, t.Name + "  " + Theme.Dim(Loc.T("mycareer.teamRep", t.Reputation.ToString("0"))), detail,
                    MathX.FormatMoney(o.SalaryPerSeason) + Theme.Dim("/" + Loc.T("unit.season")),
                    UiFactory.Primary(confirm ? Loc.T("mycareer.acceptConfirm") : Loc.T("mycareer.accept"), () =>
                    {
                        if (_confirm != offer.TeamId)
                        {
                            _confirm = offer.TeamId;
                            Shell.Refresh();
                            return;
                        }
                        _confirm = null;
                        var res = ManagerCareer.ChangeJob(world, Game.Db, offer);
                        Shell.Toast(res.Message, res.Ok);
                        if (res.Ok) Shell.Show<HubScreen>();
                        else Shell.Refresh();
                    }));
            }
            if (offers.Count == 0) Ui.Note(card, Loc.T("mycareer.noOffers"));
        }
    }

    /// <summary>
    /// The organisation the player owns: its money (and what to do in a cash crisis), its teams and who runs each,
    /// hiring a manager for a team the player does not run, taking charge again, moving a team to another category,
    /// selling or closing it.
    /// </summary>
    public class CareerOrgPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.career.org"); } }
        private string _confirm;
        private string _teamId;
        private string _moveCategory;
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
            var list = Page(content);
            var world = Game.World;
            var db = Game.Db;
            var m = world.PlayerManager;
            if (m == null) return;
            var org = ManagerCareer.OwnedOrganisation(world);
            if (org == null)
            {
                var none = Ui.Card(list, Loc.T("tab.career.org"));
                UiFactory.Paragraph(none, Loc.T("mycareer.org.none"), Theme.FontSmall);
                UiFactory.ButtonRow(none, 60, UiFactory.Primary(Loc.T("mycareer.found"), () => Shell.Show<CareerFoundPage>()));
                return;
            }
            var teams = OrganisationService.Teams(world, org);
            Team selected = null;
            foreach (var t in teams) if (t.Id == _teamId) selected = t;
            if (selected == null && teams.Count > 0) selected = teams[0];
            if (selected != null) _teamId = selected.Id;

            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);
            OrgCard(leftCol, world, db, m, org, teams);
            if (selected == null) return;
            TeamCard(leftCol, world, db, m, org, selected);

            var views = new[] { Loc.T("mycareer.org.view.manager"), Loc.T("mycareer.org.view.category"), Loc.T("mycareer.org.view.sell") };
            Ui.Chips(rightCol, views, _view, i => { _view = i; _confirm = null; Shell.Refresh(); });
            if (_view == 1) CategoryCard(rightCol, world, db, selected);
            else if (_view == 2) SellCard(rightCol, world, db, org, selected);
            else HireCard(rightCol, world, db, m, selected);
        }

        private void OrgCard(RectTransform col, WorldState world, Core.Data.GameDatabase db, ManagerProfile m, Organisation org, List<Team> teams)
        {
            double debt = 0;
            foreach (var l in org.Loans) debt += l.Outstanding;
            var oc = Ui.Card(col, org.Name, Loc.T("mycareer.org.owner"));
            Ui.Kpis(oc,
                Ui.Kpi(Theme.Money(org.Cash), Loc.T("finance.cash")),
                Ui.Kpi(debt > 0 ? MathX.FormatMoney(debt) : "-", Loc.T("mycareer.org.debt")),
                Ui.Kpi(MathX.FormatMoney(m.PersonalWealth), Loc.T("mycareer.wealth")));
            if (teams.Count > 1)
            {
                var names = new string[teams.Count];
                int idx = 0;
                for (int i = 0; i < teams.Count; i++)
                {
                    var cat = Game.Db.GetCategory(teams[i].CategoryId);
                    names[i] = cat != null ? cat.ShortName : teams[i].ShortName;
                    if (teams[i].Id == _teamId) idx = i;
                }
                Ui.Chips(oc, names, idx, i => { _teamId = teams[i].Id; _confirm = null; Shell.Refresh(); });
            }
            var alert = OrganisationService.Alert(world, db, org);
            if (alert == CashAlert.None)
            {
                Ui.Note(oc, Loc.T("mycareer.org.info"));
                return;
            }
            // Cash crisis: the ways out.
            Ui.Note(oc, alert == CashAlert.Insolvent ? Theme.BadText(Loc.T("org.crisis.insolvent")) : Theme.WarnText(Loc.T("org.crisis.low")));
            var acts = new List<UiAct>();
            double loan = OrganisationService.EmergencyLoanAmount(world, org);
            if (loan > 0)
                acts.Add(UiFactory.Act(_confirm == "loan" ? Loc.T("org.crisis.loanConfirm", (OrganisationService.EmergencyRate * 100).ToString("0")) : Loc.T("org.crisis.loan", MathX.FormatMoney(loan)), () =>
                {
                    if (!Confirm("loan")) return;
                    Shell.Toast(OrganisationService.TakeEmergencyLoan(world, org) ? Loc.T("org.crisis.loanTaken") : Loc.T("org.crisis.noLoan"));
                    Shell.Refresh();
                }));
            double need = Math.Max(0, -org.Cash) + 500000;
            double inject = Math.Min(m.PersonalWealth, Math.Round(need / 10000) * 10000);
            if (inject > 0)
                acts.Add(UiFactory.Act(_confirm == "inject" ? Loc.T("org.crisis.injectConfirm") : Loc.T("org.crisis.inject", MathX.FormatMoney(inject)), () =>
                {
                    if (!Confirm("inject")) return;
                    OrganisationService.Inject(world, org, inject);
                    Shell.Refresh();
                }));
            if (acts.Count > 0) UiFactory.ButtonRow(oc, 56, acts.ToArray());
            if (alert == CashAlert.Insolvent) Ui.Note(oc, Loc.T("org.crisis.forced"));
        }

        private void TeamCard(RectTransform col, WorldState world, Core.Data.GameDatabase db, ManagerProfile m, Organisation org, Team team)
        {
            var cat = db.GetCategory(team.CategoryId);
            var tc = Ui.Card(col, team.Name, cat != null ? cat.Name : "");
            bool mine = m.Job != null && m.Job.TeamId == team.Id;
            var boss = world.GetManager(team.ManagerId);
            if (mine) Ui.Line(tc, Loc.T("mycareer.org.runBy"), "<b>" + Loc.T("mycareer.org.you") + "</b>");
            else if (boss != null && boss.Job != null)
            {
                Ui.Line(tc, Loc.T("mycareer.org.runBy"), "<b>" + boss.FullName + "</b>  " + Theme.Dim(Loc.T("mycareer.fame") + " " + Fmt.Rating(boss.Reputation)));
                Ui.Line(tc, Loc.T("mycareer.salary"), MathX.FormatMoney(boss.Job.SalaryPerSeason) + Theme.Dim("/" + Loc.T("unit.season")));
            }
            else Ui.Line(tc, Loc.T("mycareer.org.runBy"), "-");
            Ui.Line(tc, Loc.T("org.riders"), team.RiderIds.Count.ToString());
            if (team.PendingCategoryId != null)
            {
                var to = db.GetCategory(team.PendingCategoryId);
                Ui.Line(tc, Loc.T("org.moving"), Theme.WarnText(to != null ? to.Name : team.PendingCategoryId));
            }
            if (mine) return;
            bool breaking = m.Job != null && m.Job.Role == ManagerRole.TeamManager && m.Job.EndSeason > world.Season;
            if (breaking) Ui.Note(tc, Theme.WarnText(Loc.T("mycareer.breakNote")));
            string key = "run:" + team.Id;
            var t = team;
            UiFactory.ButtonRow(tc, 58, UiFactory.Primary(_confirm == key ? Loc.T("mycareer.org.runConfirm", MathX.FormatMoney(ManagerCareer.SeveranceFor(world, boss))) : Loc.T("mycareer.org.run"), () =>
            {
                if (!Confirm(key)) return;
                var res = ManagerCareer.RunOwnTeam(world, Game.Db, t);
                Shell.Toast(res.Message, res.Ok);
                if (res.Ok) Shell.Show<HubScreen>(); else Shell.Refresh();
            }));
        }

        private void HireCard(RectTransform col, WorldState world, Core.Data.GameDatabase db, ManagerProfile m, Team team)
        {
            var hire = Ui.Card(col, Loc.T("mycareer.org.hireTitle"), team.Name);
            if (m.Job != null && m.Job.TeamId == team.Id)
            {
                Ui.Note(hire, Loc.T("mycareer.org.youRunIt"));
                return;
            }
            var current = world.GetManager(team.ManagerId);
            double severance = ManagerCareer.SeveranceFor(world, current);
            if (severance > 0) Ui.Note(hire, Loc.T("mycareer.org.severance", current.FullName, MathX.FormatMoney(severance)));
            var rules = db.GetRules(team.CategoryId, world.Season);
            foreach (var c in ManagerMarket.Candidates(world, db, team, 5))
            {
                var cand = c;
                string key = "hire:" + c.Id;
                string detail = Loc.T("mycareer.skill.sporting") + " " + c.SportingManagement.ToString("0") + " · " + Loc.T("mycareer.skill.negotiation") + " " + c.Negotiation.ToString("0")
                    + " · " + Loc.T("mycareer.skill.finance") + " " + c.Finance.ToString("0") + " · " + Loc.T("mycareer.org.age", world.Season - c.BirthYear);
                Ui.Item(hire, c.FullName + "  " + Theme.Dim(Loc.T("mycareer.fame") + " " + Fmt.Rating(c.Reputation)), detail,
                    MathX.FormatMoney(CareerService.SalaryFor(rules, c, team)) + Theme.Dim("/" + Loc.T("unit.season")),
                    UiFactory.Primary(_confirm == key ? Loc.T("mycareer.org.hireConfirm") : Loc.T("mycareer.org.hire"), () =>
                    {
                        if (!Confirm(key)) return;
                        var res = ManagerCareer.HireForOwnTeam(world, Game.Db, team, cand);
                        Shell.Toast(res.Message, res.Ok);
                        Shell.Refresh();
                    }));
            }
        }

        private void CategoryCard(RectTransform col, WorldState world, Core.Data.GameDatabase db, Team team)
        {
            var card = Ui.Card(col, Loc.T("org.move.title"), Loc.T("org.move.info"));
            if (team.PendingCategoryId != null)
            {
                var to = db.GetCategory(team.PendingCategoryId);
                UiFactory.Paragraph(card, Loc.T("org.move.pending", to != null ? to.Name : ""), Theme.FontSmall);
                UiFactory.ButtonRow(card, 54, UiFactory.Act(Loc.T("org.move.cancel"), () =>
                {
                    var r = OrganisationService.RequestCategoryChange(world, db, team, null, null);
                    Shell.Toast(r.Message, r.Ok);
                    Shell.Refresh();
                }));
                return;
            }
            var org = world.OrganisationOf(team);
            var have = new HashSet<string>();
            foreach (var t in OrganisationService.Teams(world, org)) have.Add(t.CategoryId);
            var w = new[] { 1.6f, 1.8f, 1.8f, 1.8f, 1.6f };
            UiFactory.TableRow(card, 34, w, new[] { Loc.T("career.col.category"), Loc.T("org.move.cost"), Loc.T("org.move.refund"), Loc.T("career.col.verdict"), "" }, true);
            foreach (var cat in db.CategoriesOrdered())
            {
                if (!cat.Simulated || cat.IsCup || have.Contains(cat.Id)) continue;
                var q = OrganisationService.QuoteCategoryChange(world, db, team, cat.Id);
                var v = q.Viability != null ? q.Viability.Verdict : Viability.NotAvailable;
                string verdict = !q.PlaceAvailable ? Theme.BadText(Loc.T("org.move.noPlace")) : v == Viability.Viable ? Theme.GoodText(Loc.T("viability." + v)) : v == Viability.Risky ? Theme.WarnText(Loc.T("viability." + v)) : Theme.BadText(Loc.T("viability." + v));
                var c = cat;
                var row = UiFactory.TableRow(card, 44, w, new[] { cat.ShortName, Theme.Money(-(q.EntryCost + q.Bikes)), Theme.Money(q.OldPlaceRefund), verdict, "" }, false,
                    _moveCategory == cat.Id ? new Color(0.94f, 0.7f, 0.16f, 0.16f) : (Color?)null);
                if (q.PlaceAvailable && (v == Viability.Viable || v == Viability.Risky))
                {
                    var b = UiFactory.Button("Pick", row, Loc.T("career.choose"), () => { _moveCategory = c.Id; Shell.Refresh(); },
                        _moveCategory == cat.Id ? Theme.Accent : Theme.PanelLight, Theme.FontSmall - 6);
                    UiFactory.InColumn(b, w, w.Length - 1);
                }
            }
            if (_moveCategory == null) return;
            var target = db.GetCategory(_moveCategory);
            UiFactory.Paragraph(card, "<b>" + Loc.T("career.supplier", target != null ? target.Name : "") + "</b>", Theme.FontSmall);
            var acts = new List<UiAct>();
            foreach (var maker in CareerService.Suppliers(world, _moveCategory))
            {
                var mk = maker;
                acts.Add(UiFactory.Act(mk.Name, () =>
                {
                    var r = OrganisationService.RequestCategoryChange(world, db, team, _moveCategory, mk.Id);
                    Shell.Toast(r.Message, r.Ok);
                    if (r.Ok) _moveCategory = null;
                    Shell.Refresh();
                }));
            }
            UiFactory.ButtonRow(card, 52, acts.ToArray());
            Ui.Note(card, Loc.T("org.move.note"));
        }

        private void SellCard(RectTransform col, WorldState world, Core.Data.GameDatabase db, Organisation org, Team team)
        {
            bool between = OrganisationService.BetweenSeasons(Game);
            bool distress = org.Cash < 0;
            var v = OrganisationService.Value(world, db, team);
            double price = Math.Round(v.Total * (distress ? OrganisationService.DistressShare : 1) / 1000) * 1000;
            var sell = Ui.Card(col, Loc.T("org.sell.title"), Loc.T("org.sell.info"));
            Ui.Line(sell, Loc.T("org.sell.place"), MathX.FormatMoney(v.GridPlace));
            Ui.Line(sell, Loc.T("org.sell.facilities"), MathX.FormatMoney(v.Facilities));
            Ui.Line(sell, Loc.T("org.sell.equipment"), MathX.FormatMoney(v.Equipment + v.Inventory));
            Ui.Line(sell, Loc.T("org.sell.brand"), MathX.FormatMoney(v.Brand));
            Ui.Line(sell, Loc.T("org.sell.price") + (distress ? " " + Theme.WarnText(Loc.T("org.sell.distress")) : ""), "<b>" + MathX.FormatMoney(price) + "</b>", true);
            if (org.TeamIds.Count == 1) Ui.Note(sell, Loc.T("org.sell.last"));
            if (!between && !distress) Ui.Note(sell, Loc.T("org.e.season"));
            else
            {
                var t = team;
                UiFactory.ButtonRow(sell, 56, UiFactory.Primary(_confirm == "sell" ? Loc.T("org.sell.confirm", MathX.FormatMoney(price)) : Loc.T("org.sell.button"), () =>
                {
                    if (!Confirm("sell")) return;
                    var res = OrganisationService.SellTeam(Game, t);
                    Shell.Toast(res.Message, res.Ok);
                    Session.Save();
                    Shell.Show<HubScreen>();
                }));
            }

            var q = OrganisationService.ClosureCost(world, db, team);
            var close = Ui.Card(col, Loc.T("org.close.title"));
            Ui.Line(close, Loc.T("org.close.riders"), Theme.Money(-q.RiderContracts));
            Ui.Line(close, Loc.T("org.close.staff"), Theme.Money(-q.StaffContracts));
            Ui.Line(close, Loc.T("org.close.refund"), Theme.Money(q.GridPlaceRefund));
            Ui.Line(close, Loc.T("org.close.net"), Theme.Money(q.Net), true);
            if (!between) Ui.Note(close, Loc.T("org.e.season"));
            else
            {
                var t = team;
                UiFactory.ButtonRow(close, 56, UiFactory.Act(_confirm == "close" ? Loc.T("org.close.confirm") : Loc.T("org.close.button"), () =>
                {
                    if (!Confirm("close")) return;
                    var res = OrganisationService.CloseTeam(Game, t);
                    Shell.Toast(res.Message, res.Ok);
                    Session.Save();
                    Shell.Show<HubScreen>();
                }));
            }
        }
    }

    /// <summary>A new team for the organisation the player already owns (between seasons).</summary>
    public class ExpansionState
    {
        public double LoanShare;
        public string Category, Supplier, TeamName = "";
        public string Primary, Secondary;
    }

    /// <summary>Founding a team (between seasons): the team starts empty and in the colours chosen.</summary>
    public class CareerFoundPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.career.found"); } }
        private readonly FoundingState _state = new FoundingState();
        private readonly ExpansionState _exp = new ExpansionState();

        private void BuildExpansion(RectTransform list, WorldState world, Organisation org)
        {
            var db = Game.Db;
            if (!OrganisationService.BetweenSeasons(Game))
            {
                var wait = Ui.Card(list, Loc.T("org.expand.title"));
                UiFactory.Paragraph(wait, Loc.T("org.expand.later"), Theme.FontSmall);
                Ui.Line(wait, Loc.T("finance.cash"), Theme.Money(org.Cash));
                return;
            }
            var terms = OrganisationService.BankTerms(world, org);
            double loan = Math.Round(terms.MaxAmount * _exp.LoanShare / 10000) * 10000;
            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);
            var money = Ui.Card(leftCol, Loc.T("org.expand.title"), Loc.T("org.expand.info"));
            Ui.Line(money, Loc.T("finance.cash"), Theme.Money(org.Cash));
            UiFactory.ButtonRow(money, 50,
                UiFactory.Option(Loc.T("career.loanNone"), _exp.LoanShare == 0, () => { _exp.LoanShare = 0; Shell.Refresh(); }),
                UiFactory.Option(Loc.T("career.loanHalf"), _exp.LoanShare == 0.5, () => { _exp.LoanShare = 0.5; Shell.Refresh(); }),
                UiFactory.Option(Loc.T("career.loanMax"), _exp.LoanShare == 1, () => { _exp.LoanShare = 1; Shell.Refresh(); }));
            Ui.Note(money, Loc.T("career.loanTerms", MathX.FormatMoney(terms.MaxAmount), (terms.Rate * 100).ToString("0.0"), terms.Seasons));
            var w = new[] { 1.6f, 1.8f, 1.6f, 1.6f, 1.6f };
            UiFactory.TableRow(money, 34, w, new[] { Loc.T("career.col.category"), Loc.T("career.col.verdict"), Loc.T("career.col.startup"), Loc.T("career.col.lowest"), "" }, true);
            foreach (var r in OrganisationService.ExpansionOptions(world, db, org, loan))
            {
                var rep = r;
                var cat = db.GetCategory(r.CategoryId);
                string verdict = r.Verdict == Viability.Viable ? Theme.GoodText(Loc.T("viability." + r.Verdict)) : r.Verdict == Viability.Risky ? Theme.WarnText(Loc.T("viability." + r.Verdict)) : Theme.BadText(Loc.T("viability." + r.Verdict));
                var row = UiFactory.TableRow(money, 44, w, new[] { cat.ShortName, verdict, Theme.Money(-r.StartupTotal), Theme.Money(r.MinCashConfirmed), "" }, false,
                    _exp.Category == r.CategoryId ? new Color(0.94f, 0.7f, 0.16f, 0.16f) : (Color?)null);
                if (r.Verdict == Viability.Viable || r.Verdict == Viability.Risky)
                {
                    var b = UiFactory.Button("Pick", row, Loc.T("career.choose"), () => { _exp.Category = rep.CategoryId; _exp.Supplier = null; Shell.Refresh(); },
                        _exp.Category == r.CategoryId ? Theme.Accent : Theme.PanelLight, Theme.FontSmall - 6);
                    UiFactory.InColumn(b, w, w.Length - 1);
                }
            }

            var id = Ui.Card(rightCol, Loc.T("found.identity"));
            if (_exp.Category == null)
            {
                Ui.Note(id, Loc.T("found.pickCategory"));
                return;
            }
            var chosen = db.GetCategory(_exp.Category);
            UiFactory.Paragraph(id, "<b>" + Loc.T("career.supplier", chosen.Name) + "</b>", Theme.FontSmall);
            var acts = new List<UiAct>();
            foreach (var maker in CareerService.Suppliers(world, _exp.Category))
            {
                var mk = maker;
                acts.Add(UiFactory.Option(mk.Name, _exp.Supplier == mk.Id, () => { _exp.Supplier = mk.Id; Shell.Refresh(); }));
            }
            UiFactory.ButtonRow(id, 50, acts.ToArray());
            if (_exp.Primary == null)
            {
                var first = OrganisationService.Teams(world, org);
                _exp.Primary = first.Count > 0 ? first[0].Colors.Primary : "#E10600";
                _exp.Secondary = first.Count > 0 ? first[0].Colors.Secondary : "#FFFFFF";
            }
            UiFactory.Input(id, Loc.T("career.teamName"), _exp.TeamName, v => _exp.TeamName = v);
            FoundingPanel.Swatches(id, Loc.T("found.primary"), _exp.Primary, c => { _exp.Primary = c; Shell.Refresh(); });
            FoundingPanel.Swatches(id, Loc.T("found.secondary"), _exp.Secondary, c => { _exp.Secondary = c; Shell.Refresh(); });
            Ui.Note(id, Loc.T("org.expand.note"));
            if (_exp.Supplier == null) return;
            UiFactory.ButtonRow(id, 64, UiFactory.Primary(Loc.T("org.expand.confirm"), () =>
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
            }));
        }

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var m = world.PlayerManager;
            if (m == null) return;
            var owned = ManagerCareer.OwnedOrganisation(world);
            if (owned != null)
            {
                BuildExpansion(list, world, owned);
                return;
            }
            if (!ManagerCareer.CanFoundNow(world, Game))
            {
                var wait = Ui.Card(list, Loc.T("tab.career.found"));
                UiFactory.Paragraph(wait, Loc.T("mycareer.foundLater"), Theme.FontSmall);
                Ui.Line(wait, Loc.T("mycareer.wealth"), MathX.FormatMoney(m.PersonalWealth));
                return;
            }
            FoundingPanel.Build(list, Game, m, _state, Shell.Refresh, res =>
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
