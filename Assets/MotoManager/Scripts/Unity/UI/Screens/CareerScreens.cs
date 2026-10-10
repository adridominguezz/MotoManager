using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
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
    /// The organisation the player owns: its money and teams, who runs each team, hiring a manager for a team the
    /// player is not running, and taking charge of it again.
    /// </summary>
    public class CareerOrgPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.career.org"); } }
        private string _confirm;

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
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
            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);

            double debt = 0;
            foreach (var l in org.Loans) debt += l.Outstanding;
            var oc = Ui.Card(leftCol, org.Name, Loc.T("mycareer.org.owner"));
            Ui.Kpis(oc,
                Ui.Kpi(Theme.Money(org.Cash), Loc.T("finance.cash")),
                Ui.Kpi(debt > 0 ? MathX.FormatMoney(debt) : "-", Loc.T("mycareer.org.debt")),
                Ui.Kpi(org.TeamIds.Count.ToString(), Loc.T("mycareer.org.teams")));
            Ui.Note(oc, Loc.T("mycareer.org.info"));

            Team delegated = null;
            foreach (var id in org.TeamIds)
            {
                var team = world.GetTeam(id);
                if (team == null) continue;
                var cat = Game.Db.GetCategory(team.CategoryId);
                var tc = Ui.Card(leftCol, team.Name, cat != null ? cat.Name : "");
                bool mine = m.Job != null && m.Job.TeamId == team.Id;
                var boss = world.GetManager(team.ManagerId);
                if (mine)
                {
                    Ui.Line(tc, Loc.T("mycareer.org.runBy"), "<b>" + Loc.T("mycareer.org.you") + "</b>");
                    continue;
                }
                if (delegated == null) delegated = team;
                if (boss != null && boss.Job != null)
                {
                    Ui.Line(tc, Loc.T("mycareer.org.runBy"), "<b>" + boss.FullName + "</b>  " + Theme.Dim(Loc.T("mycareer.fame") + " " + Fmt.Rating(boss.Reputation)));
                    Ui.Line(tc, Loc.T("mycareer.salary"), MathX.FormatMoney(boss.Job.SalaryPerSeason) + Theme.Dim("/" + Loc.T("unit.season")));
                    Ui.Line(tc, Loc.T("manager.until"), boss.Job.EndSeason.ToString());
                }
                else Ui.Line(tc, Loc.T("mycareer.org.runBy"), "-");
                bool breaking = m.Job != null && m.Job.Role == ManagerRole.TeamManager && m.Job.EndSeason > world.Season;
                string key = "run:" + team.Id;
                bool confirm = _confirm == key;
                var t = team;
                if (breaking) Ui.Note(tc, Theme.WarnText(Loc.T("mycareer.breakNote")));
                UiFactory.ButtonRow(tc, 58, UiFactory.Primary(confirm ? Loc.T("mycareer.org.runConfirm", MathX.FormatMoney(ManagerCareer.SeveranceFor(world, boss))) : Loc.T("mycareer.org.run"), () =>
                {
                    if (_confirm != key) { _confirm = key; Shell.Refresh(); return; }
                    _confirm = null;
                    var res = ManagerCareer.RunOwnTeam(world, Game.Db, t);
                    Shell.Toast(res.Message, res.Ok);
                    if (res.Ok) Shell.Show<HubScreen>(); else Shell.Refresh();
                }));
            }

            // Hiring a manager for a team the player does not run.
            var hire = Ui.Card(rightCol, Loc.T("mycareer.org.hireTitle"), delegated != null ? delegated.Name : null);
            if (delegated == null)
            {
                Ui.Note(hire, Loc.T("mycareer.org.youRunAll"));
                return;
            }
            var current = world.GetManager(delegated.ManagerId);
            double severance = ManagerCareer.SeveranceFor(world, current);
            if (severance > 0) Ui.Note(hire, Loc.T("mycareer.org.severance", current.FullName, MathX.FormatMoney(severance)));
            var rules = Game.Db.GetRules(delegated.CategoryId, world.Season);
            var team2 = delegated;
            foreach (var c in ManagerMarket.Candidates(world, Game.Db, delegated))
            {
                var cand = c;
                string key = "hire:" + c.Id;
                bool confirm = _confirm == key;
                string detail = Loc.T("mycareer.skill.sporting") + " " + c.SportingManagement.ToString("0") + " · " + Loc.T("mycareer.skill.negotiation") + " " + c.Negotiation.ToString("0")
                    + " · " + Loc.T("mycareer.skill.finance") + " " + c.Finance.ToString("0") + " · " + Loc.T("mycareer.org.age", world.Season - c.BirthYear);
                Ui.Item(hire, c.FullName + "  " + Theme.Dim(Loc.T("mycareer.fame") + " " + Fmt.Rating(c.Reputation)), detail,
                    MathX.FormatMoney(CareerService.SalaryFor(rules, c, delegated)) + Theme.Dim("/" + Loc.T("unit.season")),
                    UiFactory.Primary(confirm ? Loc.T("mycareer.org.hireConfirm") : Loc.T("mycareer.org.hire"), () =>
                    {
                        if (_confirm != key) { _confirm = key; Shell.Refresh(); return; }
                        _confirm = null;
                        var res = ManagerCareer.HireForOwnTeam(world, Game.Db, team2, cand);
                        Shell.Toast(res.Message, res.Ok);
                        Shell.Refresh();
                    }));
            }
        }
    }

    /// <summary>Founding a team (between seasons): the team starts empty and in the colours chosen.</summary>
    public class CareerFoundPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.career.found"); } }
        private readonly FoundingState _state = new FoundingState();

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var m = world.PlayerManager;
            if (m == null) return;
            if (m.Job != null && m.Job.Role == ManagerRole.OwnerManager)
            {
                var own = Ui.Card(list, Loc.T("tab.career.found"));
                Ui.Note(own, Loc.T("mycareer.alreadyOwner"));
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
                    Shell.Show<HubScreen>();
                }
                else Shell.Refresh();
            });
        }
    }
}
