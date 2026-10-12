using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>State of one negotiation shown in a screen (kept while the screen is rebuilt).</summary>
    public class NegotiationState
    {
        public string RiderId;
        public ContractProposal Proposal;
        public string Message;
        public ContractProposal Counter;
        public double TransferAsking;
    }

    /// <summary>
    /// The contract negotiation panel: route to the rider (free, release clause, fee to agree), the terms, what they
    /// cost before confirming, and the rider's answer. Rules are in <see cref="ContractService"/>.
    /// </summary>
    public static class NegotiationPanel
    {
        private static string Money(double v)
        {
            return Theme.Money(v);
        }

        /// <summary>
        /// Full-page negotiation in two columns: on the left what the rider asks against the offer (big numbers),
        /// the salary stepper, the length and the start, with the other clauses folded; on the right the route
        /// (transfer fee), the cost, the rider's answer and the proposal button. "◄" closes it.
        /// </summary>
        public static void Build(RectTransform list, GameController game, NegotiationState s, Action refresh, Action close)
        {
            var world = game.World;
            var db = game.Db;
            var team = world.PlayerTeam;
            var r = world.GetRider(s.RiderId);
            if (team == null || r == null) return;
            if (s.Proposal == null) s.Proposal = ContractService.DefaultProposal(world, db, team, r);
            var p = s.Proposal;
            bool renewal = r.TeamId == team.Id;
            var owner = world.GetTeam(r.TeamId);
            var c = ContractService.Cost(world, db, team, r, p);

            UiFactory.ButtonRow(list, 50, UiFactory.Act("◄  " + Loc.T("neg.close"), () => { close(); refresh(); }));
            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);
            string status = owner == null ? Loc.T("neg.free") :
                Loc.T("neg.contract", owner.Name, r.Contract != null ? r.Contract.EndSeason : world.Season, MathX.FormatMoney(r.Contract != null ? r.Contract.SalaryPerSeason : 0));
            var left = Ui.Card(leftCol, Loc.T("neg.title", r.FullName));
            Ui.Kpis(left,
                Ui.Kpi(MathX.FormatMoney(ContractService.AskingSalary(world, db, team, r)), Loc.T("neg.askingShort")),
                Ui.Kpi(MathX.FormatMoney(c.GuaranteedTotal), Loc.T("neg.cost.guaranteed")),
                Ui.Kpi(Money(c.CashAfter), Loc.T("neg.cost.cashAfter")));
            MobileParts.Stepper(left, Loc.T("neg.salary"), MathX.FormatMoney(p.Salary),
                UiFactory.Act("-10%", () => { p.Salary = Math.Round(p.Salary * 0.9 / 1000) * 1000; refresh(); }),
                UiFactory.Act("-5%", () => { p.Salary = Math.Round(p.Salary * 0.95 / 1000) * 1000; refresh(); }),
                UiFactory.Act("+5%", () => { p.Salary = Math.Round(p.Salary * 1.05 / 1000) * 1000; refresh(); }),
                UiFactory.Act("+10%", () => { p.Salary = Math.Round(p.Salary * 1.1 / 1000) * 1000; refresh(); }));
            Ui.Chips(left, new[] { Loc.T("neg.seasonsN", 1), Loc.T("neg.seasonsN", 2), Loc.T("neg.seasonsN", 3), Loc.T("neg.seasonsN", 4) },
                Math.Max(0, Math.Min(3, p.Seasons - 1)), i => { p.Seasons = i + 1; refresh(); }, 46);
            if (!renewal)
                Ui.Chips(left, new[] { Loc.T("neg.startNow"), Loc.T("neg.nextSeason") }, p.StartNow ? 0 : 1, i => { p.StartNow = i == 0; refresh(); }, 46);
            Ui.Note(left, status);

            // The other clauses, one tap away: each button cycles or switches one clause.
            Ui.Expander(leftCol, "neg.clauses", Loc.T("shop.col.terms"), inner =>
            {
                var clauses = Ui.Card(inner, null);
                UiFactory.ButtonRow(clauses, 50,
                    UiFactory.Act(Loc.T("neg.signing", MathX.FormatMoney(p.SigningBonus)), () => { p.SigningBonus = p.SigningBonus <= 0 ? Math.Round(p.Salary * 0.1 / 1000) * 1000 : p.SigningBonus < p.Salary * 0.15 ? Math.Round(p.Salary * 0.25 / 1000) * 1000 : 0; refresh(); }),
                    UiFactory.Act(Loc.T("neg.bonuses", MathX.FormatMoney(p.WinBonus), MathX.FormatMoney(p.PodiumBonus)), () =>
                    {
                        double level = p.WinBonus / Math.Max(1, p.Salary);
                        double next = level < 0.04 ? 0.05 : level < 0.09 ? 0.12 : 0.02;
                        p.WinBonus = Math.Round(p.Salary * next / 1000) * 1000;
                        p.PodiumBonus = Math.Round(p.WinBonus * 0.4 / 1000) * 1000;
                        refresh();
                    }),
                    UiFactory.Act(p.ReleaseClause > 0 ? Loc.T("neg.release", MathX.FormatMoney(p.ReleaseClause)) : Loc.T("neg.noRelease"), () =>
                    {
                        double ratio = p.ReleaseClause / Math.Max(1, p.Salary * p.Seasons);
                        p.ReleaseClause = p.ReleaseClause <= 0 ? Math.Round(p.Salary * p.Seasons * 1.5 / 1000) * 1000 : ratio < 2 ? Math.Round(p.Salary * p.Seasons * 4 / 1000) * 1000 : 0;
                        refresh();
                    }));
                UiFactory.ButtonRow(clauses, 50,
                    UiFactory.Option(Loc.T("neg.teamOption"), p.TeamOption, () => { p.TeamOption = !p.TeamOption; refresh(); }),
                    UiFactory.Option(Loc.T("neg.perf", p.PerformanceExtensionTopN > 0 ? p.PerformanceExtensionTopN : 5), p.PerformanceExtensionTopN > 0, () => { p.PerformanceExtensionTopN = p.PerformanceExtensionTopN > 0 ? 0 : 5; refresh(); }),
                    UiFactory.Option(Loc.T("neg.exit"), p.ExitForHigherCategory, () => { p.ExitForHigherCategory = !p.ExitForHigherCategory; refresh(); }));
            }, refresh);

            var right = Ui.Card(rightCol, Loc.T("neg.cost.title", c.FirstSeason, c.LastSeason));
            // Route: is a fee needed for the requested start? The four possible bids sit on one stepper row.
            if (!renewal)
            {
                double fee = ContractService.FeeNeeded(world, db, team, r, p.StartNow);
                if (fee < 0)
                {
                    Ui.Note(right, Theme.WarnText(Loc.T("neg.needAgreement", owner != null ? owner.Name : "")));
                    double baseFee = s.TransferAsking > 0 ? s.TransferAsking : Contracts.TransferValue(world, db, r, p.StartNow ? world.Season : world.Season + 1) * (p.StartNow ? 1.25 : 1);
                    var acts = new List<UiAct>();
                    foreach (double f in new[] { 0.7, 0.9, 1.0, 1.2 })
                    {
                        double amount = Math.Round(baseFee * f / 1000) * 1000;
                        acts.Add(UiFactory.Act(MathX.FormatMoney(amount), () =>
                        {
                            var a = ContractService.ProposeTransfer(world, db, team, r, amount, p.StartNow);
                            s.Message = a.Message;
                            if (a.Asking > 0) s.TransferAsking = a.Asking;
                            refresh();
                        }));
                    }
                    MobileParts.Stepper(right, Loc.T("neg.offerFee", ""), MathX.FormatMoney(baseFee), acts.ToArray());
                }
                else if (fee > 0) Ui.Note(right, Loc.T("neg.fee", MathX.FormatMoney(fee)));
            }

            // What it costs, before confirming, and the rider's answer.
            Ui.Line(right, Loc.T("neg.cost.bonuses"), "≈ " + MathX.FormatMoney(c.ExpectedBonusesPerSeason) + " / " + Loc.T("neg.season"));
            if (c.SigningBonus > 0) Ui.Line(right, Loc.T("neg.cost.signing"), MathX.FormatMoney(c.SigningBonus));
            if (c.TransferFee > 0) Ui.Line(right, Loc.T("neg.cost.fee"), MathX.FormatMoney(c.TransferFee));
            if (!string.IsNullOrEmpty(s.Message)) UiFactory.Paragraph(right, "<b>" + s.Message + "</b>", Theme.FontSmall);
            var buttons = new List<UiAct>
            {
                UiFactory.Primary(Loc.T("neg.propose"), () =>
                {
                    var res = ContractService.Propose(world, db, team, r, p);
                    s.Message = res.Message;
                    s.Counter = res.Counter;
                    if (res.Accepted) close();
                    refresh();
                })
            };
            if (s.Counter != null) buttons.Add(UiFactory.Act(Loc.T("neg.useCounter", MathX.FormatMoney(s.Counter.Salary)), () => { s.Proposal = s.Counter; s.Counter = null; refresh(); }));
            UiFactory.ButtonRow(right, 56, buttons.ToArray());
        }

        /// <summary>Offers from AI teams for the player's riders: one row each (who, for whom, fee, two answers).</summary>
        public static void Bids(RectTransform list, GameController game, Action refresh)
        {
            var world = game.World;
            if (world.TransferOffers.Count == 0) return;
            var card = Ui.Card(list, Loc.T("bids.title"));
            foreach (var o in new List<TransferOffer>(world.TransferOffers))
            {
                var offer = o;
                var r = world.GetRider(o.RiderId);
                var buyer = world.GetTeam(o.ToTeamId);
                if (r == null || buyer == null) continue;
                Ui.Item(card, r.FullName + "  " + Theme.Dim("→ " + buyer.Name), Loc.T("sponsor.expiresIn", Math.Max(0, o.ExpiresAfterEvent - world.EventsCompleted + 1)),
                    "<b>" + MathX.FormatMoney(o.Fee) + "</b>",
                    UiFactory.Primary(Loc.T("bids.accept"), () => { ContractService.AcceptBid(world, game.Db, offer.Id); refresh(); }),
                    UiFactory.Act(Loc.T("bids.reject"), () => { ContractService.RejectBid(world, offer.Id); refresh(); }));
            }
            Ui.Note(card, Loc.T("bids.note"));
        }

        /// <summary>One line with the contract's main terms.</summary>
        public static string Terms(Contract c)
        {
            if (c == null) return "-";
            var parts = new List<string> { Loc.T("terms.until", c.EndSeason, MathX.FormatMoney(c.SalaryPerSeason)) };
            double win = c.RaceBonusFor(1);
            if (win > 0) parts.Add(Loc.T("terms.win", MathX.FormatMoney(win)));
            foreach (var cl in c.Clauses)
            {
                switch (cl.Type)
                {
                    case ClauseType.ReleaseClause: parts.Add(Loc.T("terms.release", MathX.FormatMoney(cl.Amount))); break;
                    case ClauseType.TeamOption: parts.Add(Loc.T(cl.Exercised ? "terms.optionExercised" : "terms.option")); break;
                    case ClauseType.RiderOption: parts.Add(Loc.T("terms.riderOption")); break;
                    case ClauseType.PerformanceExtension: parts.Add(Loc.T("terms.perf", cl.ChampionshipTopN)); break;
                    case ClauseType.ExitForHigherCategory: parts.Add(Loc.T("terms.exit")); break;
                    case ClauseType.ExitIfTeamMissesObjective: parts.Add(Loc.T("terms.exitObjective")); break;
                }
            }
            if (c.RiderFunding > 0) parts.Add(Loc.T("terms.funding", MathX.FormatMoney(c.RiderFunding)));
            return string.Join(" · ", parts.ToArray());
        }
    }
}
