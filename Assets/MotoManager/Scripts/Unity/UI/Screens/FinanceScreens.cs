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

            var top = Ui.Card(list, org != null ? org.Name : team.Name, org != null ? Loc.T("owner." + org.Owner) : null);
            Ui.Kpis(top,
                Ui.Kpi(Theme.Money(org != null ? org.Cash : Accounts.Cash(world, team)), Loc.T("finance.cash")),
                Ui.Kpi(Theme.Money(left), Loc.T("finance.remaining")),
                Ui.Kpi(fc != null ? Theme.Money(fc.EndProbable) : "-", Loc.T("finance.endOfSeason")));
            double used = team.SeasonBudget > 0 ? spent / team.SeasonBudget : 0;
            Ui.Bar(top, Loc.T("finance.budgetUsed"), used, MathX.FormatMoney(spent) + Theme.Dim(" / " + MathX.FormatMoney(team.SeasonBudget)),
                used > 1 ? Theme.Bad : used > 0.85 ? Theme.Warning : Theme.Good, 40);
            if (fc != null && fc.MinConfirmed < 0) Ui.Note(top, Theme.WarnText(Loc.T("finance.cashWarning", MathX.FormatMoney(fc.MinConfirmed))));

            var detail = Ui.Card(list, Loc.T("finance.detail"));
            if (fc != null)
                Ui.Expander(detail, "fin.forecast", Loc.T("forecast.title"), c =>
                {
                    Ui.Line(c, Loc.T("forecast.confirmedIncome"), MathX.FormatMoney(fc.ConfirmedIncome));
                    Ui.Line(c, Loc.T("forecast.probableIncome"), MathX.FormatMoney(fc.ProbableIncome));
                    Ui.Line(c, Loc.T("forecast.costs"), "-" + MathX.FormatMoney(fc.Costs));
                    Ui.Line(c, Loc.T("forecast.lowest"), Theme.Money(fc.MinConfirmed));
                    Ui.Line(c, Loc.T("forecast.endConfirmed"), Theme.Money(fc.EndConfirmed));
                    Ui.Line(c, Loc.T("forecast.endProbable"), Theme.Money(fc.EndProbable), true);
                    Ui.Note(c, Loc.T("forecast.note"));
                }, Shell.Refresh);

            var totals = Accounts.TotalsFor(world, team.OrganisationId, world.Season, team.Id);
            if (totals.Count > 0)
                Ui.Expander(detail, "fin.actual", Loc.T("finance.actual", world.Season), c =>
                {
                    var kinds = new List<LedgerKind>(totals.Keys);
                    kinds.Sort();
                    double sum = 0;
                    foreach (var k in kinds)
                    {
                        double v = totals[k].Cash;
                        sum += v;
                        Ui.Line(c, Loc.T("ledger." + k), Theme.Money(v));
                    }
                    Ui.Line(c, Loc.T("finance.net"), Theme.Money(sum), true);
                }, Shell.Refresh);

            var f = FinanceService.SeasonForecast(world, Game.Db, team, Game.RoundsInSeason(team.CategoryId));
            Ui.Expander(detail, "fin.season", Loc.T("finance.seasonPlan"), c =>
            {
                Ui.Line(c, Loc.T("finance.plan.sponsors"), MathX.FormatMoney(f.SponsorIncome));
                Ui.Line(c, Loc.T("finance.plan.participation"), MathX.FormatMoney(f.ParticipationIncome));
                Ui.Line(c, Loc.T("finance.plan.riders"), "-" + MathX.FormatMoney(f.RiderSalaries));
                Ui.Line(c, Loc.T("finance.plan.staff"), "-" + MathX.FormatMoney(f.StaffSalaries));
                Ui.Line(c, Loc.T("finance.plan.facilities"), "-" + MathX.FormatMoney(f.FacilityUpkeep));
                Ui.Line(c, Loc.T("finance.plan.bikes"), "-" + MathX.FormatMoney(f.BikeLease));
                Ui.Line(c, Loc.T("finance.plan.licences"), "-" + MathX.FormatMoney(f.Licences));
                Ui.Line(c, Loc.T("finance.plan.travel"), "-" + MathX.FormatMoney(f.Travel));
                Ui.Line(c, Loc.T("finance.plan.balance"), Theme.Money(f.Balance), true);
            }, Shell.Refresh);
            if (org != null && org.TeamIds.Count > 1)
            {
                var names = new List<string>();
                foreach (var id in org.TeamIds) { var t = world.GetTeam(id); if (t != null) names.Add(t.Name); }
                Ui.Note(detail, Loc.T("finance.teams") + ": " + string.Join(", ", names.ToArray()));
            }
        }
    }

    /// <summary>Sponsors: current deals and the offers on the table.</summary>
    public class SponsorsPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.sponsors"); } }

        public override void Build(RectTransform content)
        {
            var list = Page(content);
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;

            var offers = SponsorMarket.OffersFor(world, team);
            var oc = Ui.Card(list, Loc.T("sponsor.offersTitle", offers.Count), Loc.T("sponsor.offersInfo"));
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
                    UiFactory.Act(Loc.T("sponsor.accept"), () => { Shell.Toast(SponsorMarket.Accept(world, Game.Db, offer.Id).Message); Shell.Refresh(); }),
                    UiFactory.Act(Loc.T("sponsor.counter"), () => { Shell.Toast(SponsorMarket.Counter(world, Game.Db, offer.Id, 0.1).Message); Shell.Refresh(); }),
                    UiFactory.Act(Loc.T("sponsor.reject"), () => { Shell.Toast(SponsorMarket.Reject(world, offer.Id).Message); Shell.Refresh(); }));
            }

            var dc = Ui.Card(list, Loc.T("sponsor.deals", SponsorService.DealsIn(team, world.Season), SponsorService.Slots(Game.Db, team, world.Season)));
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
