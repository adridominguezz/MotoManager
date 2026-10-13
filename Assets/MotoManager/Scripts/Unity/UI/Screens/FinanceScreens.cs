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
    /// Finance summary (UX spec §3.13), one screen: four key figures (cash, budget left, end of season, lowest point),
    /// the budget tile with what can be done about it, and the detail tile (forecast, movements, season plan).
    /// Explanations sit behind the "i" of each tile.
    /// </summary>
    public class FinanceScreen : ScreenBase
    {
        public override string Title { get { return Loc.T("tab.summary"); } }
        private int _view;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var org = world.OrganisationOf(team);
            double spent = Accounts.SpentThisSeason(world, team);
            double left = team.SeasonBudget - spent;
            double used = team.SeasonBudget > 0 ? spent / team.SeasonBudget : 0;
            CashflowForecast fc = org != null ? CashflowForecast.ForOrganisation(world, Game.Db, org) : null;

            var rows = Ui.Rows(content, 140, 418);
            // ---- four key figures
            var tiles = Ui.Columns12(rows[0], 3, 3, 3, 3);
            double cash = org != null ? org.Cash : Accounts.Cash(world, team);
            Figure(tiles[0], Loc.T("ux.fin.cash"), cash, null);
            Figure(tiles[1], Loc.T("ux.fin.left"), left, Loc.T("ux.fin.used", (used * 100).ToString("0")));
            if (fc != null)
            {
                Figure(tiles[2], Loc.T("ux.fin.end"), fc.EndProbable, Loc.T("ux.fin.probable"));
                Figure(tiles[3], Loc.T("ux.fin.min"), fc.MinConfirmed, Loc.T("ux.fin.confirmed"));
            }
            else
            {
                Ui.StatTile(tiles[2], Loc.T("ux.fin.end"), "-");
                Ui.StatTile(tiles[3], Loc.T("ux.fin.min"), "-");
            }

            var cols = Ui.Columns12(rows[1], 5, 7);
            Budget(cols[0], world, team, org, fc, spent, left, used);
            Detail(cols[1], world, team, fc);
        }

        private static void Figure(RectTransform area, string title, double value, string status)
        {
            Ui.StatTile(area, title, MathX.FormatMoney(value), status, null, value < 0 ? Theme.Bad : (Color?)null);
        }

        // ------------------------------------------------------------------ budget

        private void Budget(RectTransform col, WorldState world, Team team, Organisation org, CashflowForecast fc, double spent, double left, double used)
        {
            var info = new List<string>();
            if (org != null) info.Add(Loc.T("finance.organisation", org.Name) + " · " + Loc.T("owner." + org.Owner));
            if (org != null && org.TeamIds.Count > 1)
            {
                var names = new List<string>();
                foreach (var id in org.TeamIds) { var t = world.GetTeam(id); if (t != null) names.Add(t.Name); }
                info.Add(Loc.T("finance.teams") + ": " + string.Join(", ", names.ToArray()));
            }
            if (fc != null && fc.MinConfirmed < 0) info.Add(Loc.T("finance.cashWarning", MathX.FormatMoney(fc.MinConfirmed)));
            info.Add(Loc.T("finance.overBudgetHint"));
            var body = Ui.Tile(col, Loc.T("ux.fin.budget"), null,
                MathX.FormatMoney(spent) + Theme.Dim(" / " + MathX.FormatMoney(team.SeasonBudget)), 0, null,
                ScreenKit.Info(Loc.T("ux.fin.budget"), info.ToArray()));
            var list = ScreenKit.List(body, 8);
            Ui.Gauge(list, Math.Min(1, used), used > 1 ? Theme.Bad : used > 0.85 ? Theme.Warning : Theme.Good);

            // What can be done about the budget: an owner moves money into it; a hired manager asks the board or cuts costs.
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
                var row = UiFactory.Row(list, Ui.Grid.Button);
                var a = UiFactory.Rect("Ask", row);
                UiFactory.Place(a, 0, 0, 0.5f, 1);
                a.offsetMax = new Vector2(-Ui.Grid.Gap / 2, 0);
                Ui.Secondary(a, Loc.T(asked ? "finance.askedBoard" : "finance.askBoard"), () =>
                {
                    var res = FinanceService.RequestBudget(world, team);
                    bool ok = res.Outcome == BudgetRequestOutcome.Granted || res.Outcome == BudgetRequestOutcome.Partial;
                    Shell.Toast(Loc.T("finance.board." + res.Outcome, MathX.FormatMoney(res.Granted)), ok);
                    Shell.Refresh();
                });
                var c = UiFactory.Rect("Cut", row);
                UiFactory.Place(c, 0.5f, 0, 1, 1);
                c.offsetMin = new Vector2(Ui.Grid.Gap / 2, 0);
                Ui.Ghost(c, Loc.T("finance.cutCosts"), () => Shell.Show<StaffPage>());
            }
            if (left < 0) Ui.Alert(list, Loc.T("ux.fin.over"), true);
            if (fc != null && fc.MinConfirmed < 0) Ui.Alert(list, Loc.T("ux.fin.cashLow", MathX.FormatMoney(fc.MinConfirmed)));
        }

        // ------------------------------------------------------------------ detail

        private void Detail(RectTransform col, WorldState world, Team team, CashflowForecast fc)
        {
            string[] views = { Loc.T("finance.view.forecast"), Loc.T("finance.view.actual"), Loc.T("finance.view.plan") };
            if (_view < 0 || _view > 2) _view = 0;
            string note = _view == 0 ? Loc.T("forecast.note") : _view == 2 ? Loc.T("finance.seasonPlan") : Loc.T("finance.actual", world.Season);
            var body = Ui.Tile(col, Loc.T("finance.detail"), null, null, 0, null, ScreenKit.Info(views[_view], note));
            Ui.Segmented(ScreenKit.List(ScreenKit.Top(body, 0, 56)), views, _view, i => { _view = i; Shell.Refresh(); });
            var holder = ScreenKit.Between(body, 64, 0);
            var detail = UiFactory.ScrollList("Detail", holder, 8);
            UiFactory.Stretch((RectTransform)detail.parent.parent);
            var layout = detail.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 4, 8);
            layout.spacing = 4;

            if (_view == 0)
            {
                if (fc == null)
                {
                    Ui.Empty(detail, null, "-");
                    return;
                }
                Ui.Line(detail, Loc.T("forecast.confirmedIncome"), MathX.FormatMoney(fc.ConfirmedIncome));
                Ui.Line(detail, Loc.T("forecast.probableIncome"), MathX.FormatMoney(fc.ProbableIncome));
                Ui.Line(detail, Loc.T("forecast.costs"), "-" + MathX.FormatMoney(fc.Costs));
                Ui.Line(detail, Loc.T("forecast.lowest"), Theme.Money(fc.MinConfirmed));
                Ui.Line(detail, Loc.T("forecast.endConfirmed"), Theme.Money(fc.EndConfirmed));
                Ui.Line(detail, Loc.T("forecast.endProbable"), Theme.Money(fc.EndProbable), true);
            }
            else if (_view == 1)
            {
                var totals = Accounts.TotalsFor(world, team.OrganisationId, world.Season, team.Id);
                var kinds = new List<LedgerKind>(totals.Keys);
                kinds.Sort();
                double sum = 0;
                foreach (var k in kinds)
                {
                    double v = totals[k].Cash;
                    sum += v;
                    Ui.Line(detail, Loc.T("ledger." + k), Theme.Money(v));
                }
                if (kinds.Count == 0) Ui.Empty(detail, null, Loc.T("finance.noMovements"));
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
            }
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
