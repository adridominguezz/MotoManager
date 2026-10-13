using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// Finance summary in plain words, one screen: the money available now (one big number) with one sentence on
    /// where the season is heading; this season's income minus expenses so far; and the budget as "spent X of Y"
    /// with what can be done about it. Forecast, movements and the season plan are behind one "see detail" link.
    /// </summary>
    public class FinanceScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.summary"); } }

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var org = world.OrganisationOf(team);
            CashflowForecast fc = org != null ? CashflowForecast.ForOrganisation(world, Game.Db, org) : null;

            var rows = Ui.Rows(content, 1, 1);
            var top = Ui.Columns12(rows[0], 5, 7);
            Cash(top[0], world, team, org, fc);
            Year(top[1], world, team);
            Budget(rows[1], world, team, org, fc);
        }

        // ------------------------------------------------------------------ cash now

        private static string Gp(CashflowPoint p)
        {
            string l = p.Label ?? "";
            int i = l.LastIndexOf(" · ", StringComparison.Ordinal);
            return i >= 0 ? l.Substring(i + 3) : l;
        }

        private void Cash(RectTransform area, WorldState world, Team team, Organisation org, CashflowForecast fc)
        {
            double cash = org != null ? org.Cash : Accounts.Cash(world, team);
            var body = Ui.Tile(area, Loc.T("ux.fin.cash"), null, null, 0, null, ScreenKit.Info(Loc.T("ux.fin.cash"), Loc.T("ux.fin.cashInfo")));
            var v = ScreenKit.At(body, 0, 0.5f, 1, 1, MathX.FormatMoney(cash), Theme.TextDisplay, TextAnchor.MiddleCenter,
                cash < 0 ? Theme.Bad : Theme.Text, FontStyle.BoldAndItalic);
            ScreenKit.At(body, 0, 0.36f, 1, 0.5f, Theme.Caps(Loc.T("ux.fin.cashCaption")), Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);

            // One sentence on where the season is heading.
            string status;
            Color color;
            CashflowPoint broke = null, dip = null;
            if (fc != null)
                foreach (var p in fc.Points)
                {
                    if (broke == null && p.Probable < 0) broke = p;
                    if (dip == null && p.Confirmed < 0) dip = p;
                }
            if (fc == null)
            {
                status = "";
                color = Theme.TextDim;
            }
            else if (broke != null)
            {
                status = Loc.T("ux.fin.bad", Gp(broke));
                color = Theme.Bad;
            }
            else if (fc.EndProbable < 0)
            {
                status = Loc.T("ux.fin.badEnd", MathX.FormatMoney(fc.EndProbable));
                color = Theme.Bad;
            }
            else if (dip != null)
            {
                status = Loc.T("ux.fin.warn", Gp(dip));
                color = Theme.Warning;
            }
            else
            {
                status = Loc.T("ux.fin.good", MathX.FormatMoney(fc.EndProbable));
                color = Theme.Good;
            }
            ScreenKit.At(body, 0, 0, 1, 0.34f, "<b>" + status + "</b>", Theme.TextBody, TextAnchor.MiddleCenter, color);
        }

        // ------------------------------------------------------------------ this season so far

        private void Year(RectTransform area, WorldState world, Team team)
        {
            double income = 0, expenses = 0;
            var totals = Accounts.TotalsFor(world, team.OrganisationId, world.Season, team.Id);
            foreach (var kv in totals)
            {
                if (kv.Value.Cash >= 0) income += kv.Value.Cash;
                else expenses -= kv.Value.Cash;
            }
            double result = income - expenses;
            var body = Ui.Tile(area, Loc.T("ux.fin.year", world.Season), null, null, 0, null,
                ScreenKit.Info(Loc.T("ux.fin.year", world.Season), Loc.T("ux.fin.yearInfo")));
            double max = Math.Max(1, Math.Max(income, expenses));
            MoneyBar(body, 0.68f, 1, Loc.T("ux.fin.income"), income, income / max, Theme.Good);
            MoneyBar(body, 0.36f, 0.68f, Loc.T("ux.fin.expenses"), -expenses, expenses / max, Theme.Steel);
            ScreenKit.At(body, 0, 0, 0.5f, 0.32f, Theme.Caps(Loc.T("ux.fin.result")), Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            ScreenKit.At(body, 0.5f, 0, 1, 0.32f, "<b>" + (result >= 0 ? "+" : "") + MathX.FormatMoney(result) + "</b>", Theme.TextTitle, TextAnchor.MiddleRight,
                result >= 0 ? Theme.Good : Theme.Bad, FontStyle.Italic);
        }

        /// <summary>Caption, money on the right and a bar under them.</summary>
        private static void MoneyBar(RectTransform parent, float y0, float y1, string caption, double value, double fraction, Color color)
        {
            var row = UiFactory.Rect("MoneyBar", parent);
            UiFactory.Place(row, 0, y0, 1, y1);
            ScreenKit.At(row, 0, 0.4f, 0.5f, 1, Theme.Caps(caption), Theme.TextCaption, TextAnchor.MiddleLeft, Color.Lerp(Theme.TextDim, Color.white, 0.25f), FontStyle.Bold);
            ScreenKit.At(row, 0.5f, 0.4f, 1, 1, "<b>" + MathX.FormatMoney(value) + "</b>", Theme.TextBody, TextAnchor.MiddleRight, Theme.Text);
            var bar = UiFactory.Rect("Bar", row);
            UiFactory.Place(bar, 0, 0.06f, 1, 0.36f);
            Ui.Gauge(bar, fraction, color);
        }

        // ------------------------------------------------------------------ budget

        private void Budget(RectTransform area, WorldState world, Team team, Organisation org, CashflowForecast fc)
        {
            double spent = Accounts.SpentThisSeason(world, team);
            double left = team.SeasonBudget - spent;
            double used = team.SeasonBudget > 0 ? spent / team.SeasonBudget : 0;
            var body = Ui.Tile(area, Loc.T("ux.fin.budgetTitle"), null, null, 0, null,
                ScreenKit.Info(Loc.T("ux.fin.budgetTitle"), Loc.T("ux.fin.budgetInfo"), Loc.T("finance.overBudgetHint")));

            // Left: spent of budget, one bar, what is left.
            var info = UiFactory.Rect("Spent", body);
            UiFactory.Place(info, 0, 0, 0.52f, 1);
            ScreenKit.At(info, 0, 0.62f, 1, 0.92f, Loc.T("ux.fin.spentOf", "<b>" + MathX.FormatMoney(spent) + "</b>", "<b>" + MathX.FormatMoney(team.SeasonBudget) + "</b>"),
                Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text);
            var bar = UiFactory.Rect("Bar", info);
            UiFactory.Place(bar, 0, 0.42f, 1, 0.56f);
            Ui.Gauge(bar, Math.Min(1, used), used > 1 ? Theme.Bad : used > 0.85 ? Theme.Warning : Theme.Good);
            ScreenKit.At(info, 0, 0.12f, 1, 0.38f, left >= 0 ? Loc.T("ux.fin.leftOk", MathX.FormatMoney(left)) : Theme.BadText(Loc.T("ux.fin.leftOver", MathX.FormatMoney(-left))),
                Theme.TextBody, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);

            // Right: what can be done (an owner moves money into it; a hired manager asks the board or cuts costs),
            // and the link to every detail.
            var actions = UiFactory.Rect("Actions", body);
            UiFactory.Place(actions, 0.56f, 0, 1, 1);
            var list = ScreenKit.List(ScreenKit.Between(actions, 0, Ui.Grid.Hit + 8), 8);
            if (FinanceService.PlayerSetsBudget(world, team))
            {
                double cover = Math.Max(team.SeasonBudget, spent + (fc != null ? Math.Max(0, fc.Costs) / Math.Max(1, org != null ? org.TeamIds.Count : 1) : 0));
                Ui.Stepper(list, Loc.T("finance.setBudget"), MathX.FormatMoney(team.SeasonBudget),
                    () => { FinanceService.SetSeasonBudget(world, team, team.SeasonBudget * 0.9); Shell.Refresh(); },
                    () => { FinanceService.SetSeasonBudget(world, team, team.SeasonBudget * 1.1); Shell.Refresh(); });
                Ui.Secondary(list, Loc.T("finance.coverBudget"), () =>
                {
                    FinanceService.SetSeasonBudget(world, team, cover);
                    Shell.Toast(Loc.T("finance.budgetSet", MathX.FormatMoney(team.SeasonBudget)), true);
                    Shell.Refresh();
                });
            }
            else if (left < 0 || used > 0.85)
            {
                var job = world.PlayerManager != null ? world.PlayerManager.Job : null;
                bool asked = job != null && job.BudgetRequestSeason == world.Season;
                Ui.Secondary(list, Loc.T(asked ? "finance.askedBoard" : "finance.askBoard"), () =>
                {
                    var res = FinanceService.RequestBudget(world, team);
                    bool ok = res.Outcome == BudgetRequestOutcome.Granted || res.Outcome == BudgetRequestOutcome.Partial;
                    Shell.Toast(Loc.T("finance.board." + res.Outcome, MathX.FormatMoney(res.Granted)), ok);
                    Shell.Refresh();
                });
                Ui.Secondary(list, Loc.T("finance.cutCosts"), () => Shell.Show<StaffPage>());
            }
            Ui.Ghost(ScreenKit.Bottom(actions, 0, Ui.Grid.Hit), Loc.T("ux.fin.detail"), () => DetailSheet(0));
        }

        // ------------------------------------------------------------------ detail sheet

        /// <summary>Forecast, movements and the season plan, plus the organisation (reopened on view change).</summary>
        private void DetailSheet(int view)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var org = world.OrganisationOf(team);
            CashflowForecast fc = org != null ? CashflowForecast.ForOrganisation(world, Game.Db, org) : null;
            string[] views = { Loc.T("finance.view.forecast"), Loc.T("finance.view.actual"), Loc.T("finance.view.plan") };
            Ui.Sheet(Loc.T("ux.fin.detailTitle"), list =>
            {
                Ui.Segmented(list, views, view, i => DetailSheet(i));
                if (view == 0)
                {
                    if (fc == null)
                    {
                        Ui.Empty(list, null, "-");
                        return;
                    }
                    Ui.Line(list, Loc.T("forecast.confirmedIncome"), MathX.FormatMoney(fc.ConfirmedIncome));
                    Ui.Line(list, Loc.T("forecast.probableIncome"), MathX.FormatMoney(fc.ProbableIncome));
                    Ui.Line(list, Loc.T("forecast.costs"), "-" + MathX.FormatMoney(fc.Costs));
                    Ui.Line(list, Loc.T("forecast.lowest"), Theme.Money(fc.MinConfirmed));
                    Ui.Line(list, Loc.T("forecast.endConfirmed"), Theme.Money(fc.EndConfirmed));
                    Ui.Line(list, Loc.T("forecast.endProbable"), Theme.Money(fc.EndProbable), true);
                    UiFactory.Paragraph(list, Theme.Dim(Loc.T("forecast.note")), Theme.TextCaption);
                }
                else if (view == 1)
                {
                    var totals = Accounts.TotalsFor(world, team.OrganisationId, world.Season, team.Id);
                    var kinds = new List<LedgerKind>(totals.Keys);
                    kinds.Sort();
                    double sum = 0;
                    foreach (var k in kinds)
                    {
                        double v = totals[k].Cash;
                        sum += v;
                        Ui.Line(list, Loc.T("ledger." + k), Theme.Money(v));
                    }
                    if (kinds.Count == 0) Ui.Empty(list, null, Loc.T("finance.noMovements"));
                    else Ui.Line(list, Loc.T("finance.net"), Theme.Money(sum), true);
                }
                else
                {
                    var f = FinanceService.SeasonForecast(world, Game.Db, team, Game.RoundsInSeason(team.CategoryId));
                    Ui.Line(list, Loc.T("finance.plan.sponsors"), MathX.FormatMoney(f.SponsorIncome));
                    Ui.Line(list, Loc.T("finance.plan.participation"), MathX.FormatMoney(f.ParticipationIncome));
                    Ui.Line(list, Loc.T("finance.plan.riders"), "-" + MathX.FormatMoney(f.RiderSalaries));
                    Ui.Line(list, Loc.T("finance.plan.staff"), "-" + MathX.FormatMoney(f.StaffSalaries));
                    Ui.Line(list, Loc.T("finance.plan.facilities"), "-" + MathX.FormatMoney(f.FacilityUpkeep));
                    Ui.Line(list, Loc.T("finance.plan.bikes"), "-" + MathX.FormatMoney(f.BikeLease));
                    Ui.Line(list, Loc.T("finance.plan.licences"), "-" + MathX.FormatMoney(f.Licences));
                    Ui.Line(list, Loc.T("finance.plan.travel"), "-" + MathX.FormatMoney(f.Travel));
                    Ui.Line(list, Loc.T("finance.plan.balance"), Theme.Money(f.Balance), true);
                    UiFactory.Paragraph(list, Theme.Dim(Loc.T("finance.seasonPlan")), Theme.TextCaption);
                }
                if (org != null)
                {
                    Ui.Section(list, Loc.T("finance.organisation", org.Name));
                    Ui.Line(list, Loc.T("finance.owner"), Loc.T("owner." + org.Owner));
                    if (org.TeamIds.Count > 1)
                    {
                        var names = new List<string>();
                        foreach (var id in org.TeamIds) { var t = world.GetTeam(id); if (t != null) names.Add(t.Name); }
                        Ui.Line(list, Loc.T("finance.teams"), string.Join(", ", names.ToArray()));
                    }
                }
            }, null, null, 0.7f);
        }
    }

    /// <summary>
    /// Sponsors (UX spec §3.14): contracts and offers as tiles (money per season, a status pill); a deal opens a
    /// sheet with its conditions, an offer opens its page with the one "accept" button.
    /// </summary>
    public class SponsorsPage : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.sponsors"); } }
        private int _view = -1;
        private string _offer;
        private const int PerRow = 4;
        private const float TileHeight = 200;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;

            var offers = SponsorMarket.OffersFor(world, team);
            var open = offers.Find(o => o.Id == _offer);
            if (open != null)
            {
                Offer(content, world, open);
                return;
            }
            _offer = null;
            var list = Page(content);
            if (_view < 0) _view = offers.Count > 0 ? 1 : 0;
            int slots = SponsorService.Slots(Game.Db, team, world.Season);
            int deals = SponsorService.DealsIn(team, world.Season);
            Ui.Segmented(list, new[] { Loc.T("ux.spo.deals", deals, slots), Loc.T("ux.spo.offers", offers.Count) }, _view, i => { _view = i; Shell.Refresh(); });
            if (_view == 0) Deals(list, world, team, Math.Max(0, slots - deals));
            else Offers(list, world, offers);
        }

        // ------------------------------------------------------------------ tiles

        /// <summary>Tile areas in rows of four (200 high) in a page list.</summary>
        private static List<RectTransform> Grid(Transform list, int count)
        {
            var res = new List<RectTransform>();
            RectTransform[] cols = null;
            for (int i = 0; i < count; i++)
            {
                if (i % PerRow == 0) cols = ScreenKit.Cols(UiFactory.Row(list, TileHeight), PerRow);
                res.Add(cols[i % PerRow]);
            }
            return res;
        }

        /// <summary>The money of a deal or offer, big, with "/season" under it; pills along the bottom.</summary>
        private static RectTransform MoneyBody(RectTransform body, double perSeason)
        {
            ScreenKit.At(body, 0, 0.5f, 1, 1, MathX.FormatMoney(perSeason), Theme.TextKey, TextAnchor.MiddleCenter, Theme.Timing, FontStyle.BoldAndItalic);
            ScreenKit.At(body, 0, 0.32f, 1, 0.5f, Theme.Caps("/ " + Loc.T("unit.season")), Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);
            var pills = UiFactory.Rect("Pills", body);
            UiFactory.Place(pills, 0, 0, 1, 0.28f);
            return ScreenKit.PillRow(ScreenKit.List(pills));
        }

        private void Deals(RectTransform list, WorldState world, Team team, int freeSlots)
        {
            if (team.Sponsors.Count == 0 && freeSlots == 0)
            {
                Ui.Empty(list, UiIcons.Sponsor, Loc.T("sponsor.none"));
                return;
            }
            var areas = Grid(list, team.Sponsors.Count + freeSlots);
            for (int i = 0; i < team.Sponsors.Count; i++)
            {
                var d = team.Sponsors[i];
                var deal = d;
                Color statusColor;
                string status = Status(world, team, d, out statusColor);
                var body = Ui.Tile(areas[i], d.Name, () => DealSheet(world, team, deal));
                var pills = MoneyBody(body, d.PaymentPerSeason);
                pills.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                ScreenKit.Tag(pills, status, statusColor);
                // Mood as a dot in the rating colour.
                var dot = UiFactory.Decor("Mood", pills, Ui.RatingColor(d.Satisfaction));
                UiFactory.Round(dot, 1.2f);
                var le = dot.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = 18;
                le.preferredHeight = le.minHeight = 18;
            }
            for (int i = team.Sponsors.Count; i < areas.Count; i++)
            {
                var body = Ui.Tile(areas[i], Loc.T("ux.spo.freeSlot"), () => { _view = 1; Shell.Refresh(); }, null, 0, null, null, true);
                ScreenKit.At(body, 0, 0, 1, 1, Glyph.Plus, Theme.TextDisplay, TextAnchor.MiddleCenter, Theme.Alpha(Theme.TextDim, 0.6f), FontStyle.Bold);
            }
        }

        private static string Status(WorldState world, Team team, SponsorDeal d, out Color color)
        {
            if (d.StartSeason > world.Season)
            {
                color = Theme.Info;
                return Loc.T("sponsor.fromSeason", d.StartSeason);
            }
            if (!SponsorService.Pays(d, team, world.Season))
            {
                color = Theme.Warning;
                return Loc.T("sponsor.conditional", d.RequiredCategoryId);
            }
            color = Theme.Good;
            return Loc.T("sponsor.active");
        }

        private static void DealSheet(WorldState world, Team team, SponsorDeal d)
        {
            Color c;
            string status = Status(world, team, d, out c);
            Ui.Sheet(d.Name, list =>
            {
                Ui.Line(list, Loc.T("sponsor.col.perSeason"), MathX.FormatMoney(d.PaymentPerSeason), true);
                if (!string.IsNullOrEmpty(d.Industry)) Ui.Line(list, Loc.T("ux.spo.industry"), Industry(d.Industry));
                Ui.Line(list, Loc.T("sponsor.col.status"), status);
                Ui.Line(list, Loc.T("sponsor.col.until"), d.EndSeason.ToString());
                Ui.Line(list, Loc.T("sponsor.col.mood"), Fmt.Rating(d.Satisfaction));
                if (d.BonusEarnedThisSeason > 0) Ui.Line(list, Loc.T("ux.spo.bonus"), MathX.FormatMoney(d.BonusEarnedThisSeason));
                var objectives = ObjectiveList(d.Objectives);
                if (objectives.Count > 0)
                {
                    Ui.Section(list, Loc.T("sponsor.col.objectives"));
                    foreach (var o in objectives) Ui.Line(list, o, "");
                }
                if (d.PenaltyOnMiss > 0) Ui.Alert(list, Loc.T("sponsor.penalty", (d.PenaltyOnMiss * 100).ToString("0")));
                if (d.IndustryExclusive) Ui.Alert(list, Loc.T("sponsor.exclusive"));
            });
        }

        private void Offers(RectTransform list, WorldState world, List<SponsorOffer> offers)
        {
            Ui.Section(list, Loc.T("sponsor.offersInfo"), null, ScreenKit.Info(Loc.T("ux.spo.offersTitle"), Loc.T("sponsor.noOffers")));
            if (offers.Count == 0)
            {
                Ui.Empty(list, UiIcons.Sponsor, Loc.T("ux.spo.noOffers"));
                return;
            }
            var areas = Grid(list, offers.Count);
            for (int i = 0; i < offers.Count; i++)
            {
                var o = offers[i];
                var offer = o;
                int expires = Math.Max(0, o.ExpiresAfterEvent - world.EventsCompleted + 1);
                var body = Ui.Tile(areas[i], o.Name, () => { _offer = offer.Id; Shell.Refresh(); },
                    o.Renewal ? Theme.Caps(Loc.T("sponsor.renewal")) : Theme.Caps(Industry(o.Industry)));
                var pills = MoneyBody(body, o.PaymentPerSeason);
                pills.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                ScreenKit.Tag(pills, Loc.T("ux.spo.seasons", o.Seasons), Theme.Info);
                ScreenKit.Tag(pills, Loc.T("ux.spo.expires", expires), expires <= 1 ? Theme.Warning : Theme.Steel);
            }
        }

        // ------------------------------------------------------------------ offer page

        /// <summary>One offer: the money as key figures, every condition on the right, the three answers at the bottom.</summary>
        private void Offer(RectTransform content, WorldState world, SponsorOffer o)
        {
            var offer = o;
            var cols = Ui.Columns12(content, 6, 6);
            var leftList = ScreenKit.List(cols[0], Ui.Grid.Gap);
            string right = Industry(o.Industry) + (o.Renewal ? " · " + Loc.T("sponsor.renewal") : "");
            var card = Ui.Card(leftList, o.Name, Theme.Caps(right), ScreenKit.Info(o.Name, Loc.T("sponsor.offersInfo"), Loc.T("sponsor.noOffers")),
                () => { _offer = null; Shell.Refresh(); });
            Ui.Kpis(card,
                Ui.Kpi(MathX.FormatMoney(o.PaymentPerSeason), Loc.T("sponsor.col.perSeason")),
                Ui.Kpi(o.Seasons.ToString(), Loc.T("sponsor.col.seasons")),
                Ui.Kpi(o.SigningBonus > 0 ? MathX.FormatMoney(o.SigningBonus) : "-", Loc.T("sponsor.col.signing")));
            if (o.PenaltyOnMiss > 0) Ui.Alert(card, Loc.T("sponsor.penalty", (o.PenaltyOnMiss * 100).ToString("0")));
            if (o.IndustryExclusive) Ui.Alert(card, Loc.T("sponsor.exclusive"));
            var buttons = UiFactory.Row(card, Ui.Grid.Cta);
            var rej = UiFactory.Rect("Reject", buttons);
            UiFactory.Place(rej, 0, 0.06f, 0.28f, 0.94f);
            Ui.Ghost(rej, Loc.T("sponsor.reject"), () =>
            {
                var res = SponsorMarket.Reject(world, offer.Id);
                Shell.Toast(res.Message, res.Ok);
                _offer = null;
                Shell.Refresh();
            });
            var counter = UiFactory.Rect("Counter", buttons);
            UiFactory.Place(counter, 0.3f, 0.06f, 0.62f, 0.94f);
            Ui.Secondary(counter, Loc.T("sponsor.counter"), () =>
            {
                var res = SponsorMarket.Counter(world, Game.Db, offer.Id, 0.1);
                Shell.Toast(res.Message, res.Ok);
                Shell.Refresh();
            });
            var accept = UiFactory.Rect("Accept", buttons);
            UiFactory.Place(accept, 0.64f, 0, 1, 1);
            Ui.PrimaryCTA(accept, Loc.T("sponsor.accept"), () =>
            {
                var res = SponsorMarket.Accept(world, Game.Db, offer.Id);
                Shell.Toast(res.Message, res.Ok);
                _offer = null;
                Shell.Refresh();
            });

            var rightList = ScreenKit.List(cols[1], Ui.Grid.Gap);
            var terms = Ui.Card(rightList, Loc.T("sponsor.col.objectives"));
            Ui.Line(terms, Loc.T("sponsor.col.seasons"), o.StartSeason + (o.Seasons > 1 ? "–" + (o.StartSeason + o.Seasons - 1) : ""));
            Ui.Line(terms, Loc.T("sponsor.col.expires"), Loc.T("sponsor.events", Math.Max(0, o.ExpiresAfterEvent - world.EventsCompleted + 1)));
            foreach (var line in ObjectiveList(o.Objectives)) Ui.Line(terms, line, "");
        }

        private static string Industry(string industry)
        {
            return string.IsNullOrEmpty(industry) ? "" : Loc.Or("industry." + industry, industry);
        }

        public static string Objectives(List<SponsorObjective> objectives)
        {
            return string.Join(" · ", ObjectiveList(objectives).ToArray());
        }

        private static List<string> ObjectiveList(List<SponsorObjective> objectives)
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
            return parts;
        }
    }
}
