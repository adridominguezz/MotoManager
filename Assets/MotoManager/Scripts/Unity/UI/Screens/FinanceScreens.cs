using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>Money at a glance: cash, what is left of the budget and where the season is heading; detail on demand.</summary>
    public class FinanceScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.summary"); } }
        private int _view;

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var org = world.OrganisationOf(team);
            double spent = Accounts.SpentThisSeason(world, team);
            double left = team.SeasonBudget - spent;
            CashflowForecast fc = org != null ? CashflowForecast.ForOrganisation(world, Game.Db, org) : null;

            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);
            var top = Ui.Card(leftCol, org != null ? org.Name : team.Name, org != null ? Loc.T("owner." + org.Owner) : null);
            Ui.Kpis(top,
                Ui.Kpi(Theme.Money(org != null ? org.Cash : Accounts.Cash(world, team)), Loc.T("finance.cash")),
                Ui.Kpi(Theme.Money(left), Loc.T("finance.remaining")));
            Ui.Kpis(top, Ui.Kpi(fc != null ? Theme.Money(fc.EndProbable) : "-", Loc.T("finance.endOfSeason")));
            double used = team.SeasonBudget > 0 ? spent / team.SeasonBudget : 0;
            Ui.Bar(top, Loc.T("finance.budgetUsed"), used, MathX.FormatMoney(spent) + Theme.Dim(" / " + MathX.FormatMoney(team.SeasonBudget)),
                used > 1 ? Theme.Bad : used > 0.85 ? Theme.Warning : Theme.Good, 40);
            if (fc != null && fc.MinConfirmed < 0) Ui.Note(top, Theme.WarnText(Loc.T("finance.cashWarning", MathX.FormatMoney(fc.MinConfirmed))));
            if (org != null && org.TeamIds.Count > 1)
            {
                var names = new List<string>();
                foreach (var id in org.TeamIds) { var t = world.GetTeam(id); if (t != null) names.Add(t.Name); }
                Ui.Note(top, Loc.T("finance.teams") + ": " + string.Join(", ", names.ToArray()));
            }

            var detail = Ui.Card(rightCol, null);
            Ui.Chips(detail, new[] { Loc.T("finance.view.forecast"), Loc.T("finance.view.actual"), Loc.T("finance.view.plan") }, _view, i => { _view = i; Shell.Refresh(); }, 52);
            if (_view == 0 && fc != null)
            {
                Ui.Line(detail, Loc.T("forecast.confirmedIncome"), MathX.FormatMoney(fc.ConfirmedIncome));
                Ui.Line(detail, Loc.T("forecast.probableIncome"), MathX.FormatMoney(fc.ProbableIncome));
                Ui.Line(detail, Loc.T("forecast.costs"), "-" + MathX.FormatMoney(fc.Costs));
                Ui.Line(detail, Loc.T("forecast.lowest"), Theme.Money(fc.MinConfirmed));
                Ui.Line(detail, Loc.T("forecast.endConfirmed"), Theme.Money(fc.EndConfirmed));
                Ui.Line(detail, Loc.T("forecast.endProbable"), Theme.Money(fc.EndProbable), true);
                Ui.Note(detail, Loc.T("forecast.note"));
            }
            else if (_view == 1)
            {
                var totals = Accounts.TotalsFor(world, team.OrganisationId, world.Season, team.Id);
                var kinds = new List<LedgerKind>(totals.Keys);
                kinds.Sort();
                double sum = 0;
                var w = new[] { 3f, 1.6f, 3f, 1.6f };
                // Two columns of concepts so a full season fits on screen.
                for (int i = 0; i < kinds.Count; i += 2)
                {
                    var cells = new string[4];
                    for (int k = 0; k < 2 && i + k < kinds.Count; k++)
                    {
                        double v = totals[kinds[i + k]].Cash;
                        sum += v;
                        cells[k * 2] = Theme.Dim(Loc.T("ledger." + kinds[i + k]));
                        cells[k * 2 + 1] = Theme.Money(v);
                    }
                    UiFactory.TableRow(detail, 34, w, cells);
                }
                if (kinds.Count == 0) Ui.Note(detail, Loc.T("finance.noMovements"));
                else Ui.Line(detail, Loc.T("finance.net"), Theme.Money(sum), true);
            }
            else
            {
                var f = FinanceService.SeasonForecast(world, Game.Db, team, Game.RoundsInSeason(team.CategoryId));
                Ui.Line(detail, Loc.T("finance.plan.sponsors"), MathX.FormatMoney(f.SponsorIncome));
                Ui.Line(detail, Loc.T("finance.plan.participation"), MathX.FormatMoney(f.ParticipationIncome));
                Ui.Line(detail, Loc.T("finance.plan.riders"), "-" + MathX.FormatMoney(f.RiderSalaries));
                Ui.Line(detail, Loc.T("finance.plan.staff"), "-" + MathX.FormatMoney(f.StaffSalaries));
                Ui.Line(detail, Loc.T("finance.plan.facilities"), "-" + MathX.FormatMoney(f.FacilityUpkeep));
                Ui.Line(detail, Loc.T("finance.plan.bikes"), "-" + MathX.FormatMoney(f.BikeLease));
                Ui.Line(detail, Loc.T("finance.plan.licences"), "-" + MathX.FormatMoney(f.Licences));
                Ui.Line(detail, Loc.T("finance.plan.travel"), "-" + MathX.FormatMoney(f.Travel));
                Ui.Line(detail, Loc.T("finance.plan.balance"), Theme.Money(f.Balance), true);
                Ui.Note(detail, Loc.T("finance.seasonPlan"));
            }
        }
    }

    /// <summary>Sponsors: current deals and the offers on the table.</summary>
    public class SponsorsPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.sponsors"); } }
        private int _view = -1;

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;

            var offers = SponsorMarket.OffersFor(world, team);
            if (_view < 0) _view = offers.Count > 0 ? 1 : 0;
            Ui.Chips(list, new[] { Loc.T("sponsor.deals", SponsorService.DealsIn(team, world.Season), SponsorService.Slots(Game.Db, team, world.Season)), Loc.T("sponsor.offersTitle", offers.Count) }, _view, i => { _view = i; Shell.Refresh(); });
            if (_view == 0)
            {
                Deals(list, world, team);
                return;
            }
            var oc = Ui.Card(list, null, null);
            Ui.Note(oc, Loc.T("sponsor.offersInfo"));
            if (offers.Count == 0) Ui.Note(oc, Loc.T("sponsor.noOffers"));
            foreach (var o in offers)
            {
                var offer = o;
                string terms = Objectives(o.Objectives);
                var extra = new List<string>();
                extra.Add(Loc.T("sponsor.period", o.StartSeason + (o.Seasons > 1 ? "–" + (o.StartSeason + o.Seasons - 1) : "")));
                if (o.SigningBonus > 0) extra.Add(Loc.T("sponsor.signingBonus", MathX.FormatMoney(o.SigningBonus)));
                extra.Add(Loc.T("sponsor.expiresIn", Math.Max(0, o.ExpiresAfterEvent - world.EventsCompleted + 1)));
                if (o.PenaltyOnMiss > 0) extra.Add(Loc.T("sponsor.penalty", (o.PenaltyOnMiss * 100).ToString("0")));
                if (o.IndustryExclusive) extra.Add(Loc.T("sponsor.exclusive"));
                string detail = Industry(o.Industry) + (o.Renewal ? " · " + Loc.T("sponsor.renewal") : "") + "\n" + string.Join(" · ", extra.ToArray()) +
                                (terms.Length > 0 ? "\n" + terms : "");
                Ui.Item(oc, o.Name, detail, MathX.FormatMoney(o.PaymentPerSeason) + Theme.Dim("/" + Loc.T("unit.season")),
                    UiFactory.Primary(Loc.T("sponsor.accept"), () => { Shell.Toast(SponsorMarket.Accept(world, Game.Db, offer.Id).Message); Shell.Refresh(); }),
                    UiFactory.Act(Loc.T("sponsor.counter"), () => { Shell.Toast(SponsorMarket.Counter(world, Game.Db, offer.Id, 0.1).Message); Shell.Refresh(); }),
                    UiFactory.Act(Loc.T("sponsor.reject"), () => { Shell.Toast(SponsorMarket.Reject(world, offer.Id).Message); Shell.Refresh(); }));
            }

        }

        private void Deals(RectTransform list, WorldState world, Team team)
        {
            var dc = Ui.Card(list, null);
            foreach (var d in team.Sponsors)
            {
                string status = d.StartSeason > world.Season ? Loc.T("sponsor.fromSeason", d.StartSeason)
                    : !SponsorService.Pays(d, team, world.Season) ? Loc.T("sponsor.conditional", d.RequiredCategoryId) : Loc.T("sponsor.active");
                string detail = Industry(d.Industry) + " · " + status + " · " + Loc.T("sponsor.untilSeason", d.EndSeason) + " · " +
                                Loc.T("sponsor.mood", Fmt.Rating(d.Satisfaction));
                string objectives = Objectives(d.Objectives);
                if (objectives.Length > 0) detail += "\n" + objectives;
                Ui.Item(dc, d.Name, detail, MathX.FormatMoney(d.PaymentPerSeason) + Theme.Dim("/" + Loc.T("unit.season")));
            }
            if (team.Sponsors.Count == 0) Ui.Note(dc, Loc.T("sponsor.none"));
        }

        private static string Industry(string industry)
        {
            return string.IsNullOrEmpty(industry) ? "" : Loc.Or("industry." + industry, industry);
        }

        public static string Objectives(List<SponsorObjective> objectives)
        {
            var parts = new List<string>();
            foreach (var o in objectives)
            {
                switch (o.Type)
                {
                    case SponsorObjectiveType.RaceFinishTopN: parts.Add(Loc.T("sobj.top", o.Target, MathX.FormatMoney(o.BonusPerOccurrence))); break;
                    case SponsorObjectiveType.Podiums: parts.Add(Loc.T("sobj.podium", MathX.FormatMoney(o.BonusPerOccurrence))); break;
                    case SponsorObjectiveType.Wins: parts.Add(Loc.T("sobj.win", MathX.FormatMoney(o.BonusPerOccurrence))); break;
                    case SponsorObjectiveType.SeasonPoints: parts.Add(Loc.T("sobj.points", o.Target, MathX.FormatMoney(o.SeasonBonus))); break;
                    case SponsorObjectiveType.ChampionshipPositionTopN: parts.Add(Loc.T("sobj.champ", o.Target, MathX.FormatMoney(o.SeasonBonus))); break;
                    default: parts.Add(Loc.Or("sobj." + o.Type, o.Type.ToString())); break;
                }
            }
            return string.Join(" · ", parts.ToArray());
        }
    }
}
