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
        /// <summary>The transfer fee the player is about to bid (stepper value).</summary>
        public double FeeOffer;
    }

    /// <summary>
    /// The contract negotiation (UX spec §3.6): on the left the offer (salary stepper ±5%, length, start, the other
    /// clauses in a sheet, the rider's situation), on the right what it costs before confirming, the transfer fee
    /// when a fee must be agreed first, the rider's answer and the single PROPONER primary. The back chevron of the
    /// left card closes it. Rules are in <see cref="ContractService"/>.
    /// </summary>
    public static class NegotiationPanel
    {
        private static double Round(double v)
        {
            return Math.Round(v / 1000) * 1000;
        }

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

            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);

            // Left: the offer.
            var left = Ui.Card(leftCol, Loc.T("neg.title", r.FullName), null, null, () => { Ui.CloseSheet(); close(); refresh(); });
            Ui.Stepper(left, Loc.T("neg.salary"), MathX.FormatMoney(p.Salary),
                () => { p.Salary = Round(p.Salary * 0.95); refresh(); },
                () => { p.Salary = Round(p.Salary * 1.05); refresh(); });
            Wp2Ui.CaptionedSegmented(left, Loc.T("ux.neg.seasons"), new[] { "1", "2", "3", "4" },
                Math.Max(0, Math.Min(3, p.Seasons - 1)), i => { p.Seasons = i + 1; refresh(); });
            if (!renewal)
                Wp2Ui.CaptionedSegmented(left, Loc.T("ux.neg.start"), new[] { Loc.T("ux.neg.now"), Loc.T("ux.neg.next") },
                    p.StartNow ? 0 : 1, i => { p.StartNow = i == 0; refresh(); });
            Action clauses = null;
            clauses = () => ClausesSheet(p, refresh, clauses);
            Wp2Ui.Chevron(Ui.Secondary(left, Loc.T("ux.neg.clauses", ActiveClauses(p)), clauses));
            string status = owner == null ? Loc.T("neg.free") :
                Loc.T("neg.contract", owner.Name, r.Contract != null ? r.Contract.EndSeason : world.Season, MathX.FormatMoney(r.Contract != null ? r.Contract.SalaryPerSeason : 0));
            UiFactory.Paragraph(left, Theme.Dim(status), Theme.TextCaption);

            // Right: what it costs, the fee to agree, the answer and the proposal.
            double fee = renewal ? 0 : ContractService.FeeNeeded(world, db, team, r, p.StartNow);
            bool needAgreement = fee < 0;
            var right = Ui.Card(rightCol, Loc.T("neg.cost.title", c.FirstSeason, c.LastSeason), null,
                needAgreement ? Wp2Ui.Info(Loc.T("ux.neg.fee"), Loc.T("neg.needAgreement", owner != null ? owner.Name : "")) : null);
            Ui.Kpis(right,
                Ui.Kpi(MathX.FormatMoney(ContractService.AskingSalary(world, db, team, r)), Loc.T("neg.askingShort")),
                Ui.Kpi(MathX.FormatMoney(c.GuaranteedTotal), Loc.T("neg.cost.guaranteed")),
                Ui.Kpi(Theme.Money(c.CashAfter), Loc.T("neg.cost.cashAfter")));
            Ui.Line(right, Loc.T("neg.cost.bonuses"), "≈ " + MathX.FormatMoney(c.ExpectedBonusesPerSeason) + " / " + Loc.T("neg.season"));
            if (c.SigningBonus > 0) Ui.Line(right, Loc.T("neg.cost.signing"), MathX.FormatMoney(c.SigningBonus));
            if (c.TransferFee > 0) Ui.Line(right, Loc.T("neg.cost.fee"), MathX.FormatMoney(c.TransferFee));
            else if (fee > 0) Ui.Line(right, Loc.T("neg.cost.fee"), MathX.FormatMoney(fee));
            if (needAgreement)
            {
                // A fee must be agreed with the rider's team first: a stepper on the bid and its own (secondary) button.
                Ui.Alert(right, Loc.T("ux.neg.needAgreement", owner != null ? owner.Name : ""));
                double baseFee = s.TransferAsking > 0 ? s.TransferAsking
                    : Contracts.TransferValue(world, db, r, p.StartNow ? world.Season : world.Season + 1) * (p.StartNow ? 1.25 : 1);
                if (s.FeeOffer <= 0) s.FeeOffer = Round(baseFee * 0.9);
                Ui.Stepper(right, Loc.T("ux.neg.fee"), MathX.FormatMoney(s.FeeOffer),
                    () => { s.FeeOffer = Math.Max(0, Round(s.FeeOffer * 0.95)); refresh(); },
                    () => { s.FeeOffer = Round(Math.Max(1000, s.FeeOffer) * 1.05); refresh(); });
                Ui.Secondary(right, Loc.T("ux.neg.offerFee", MathX.FormatMoney(s.FeeOffer)), () =>
                {
                    var a = ContractService.ProposeTransfer(world, db, team, r, s.FeeOffer, p.StartNow);
                    s.Message = a.Message;
                    if (a.Asking > 0) s.TransferAsking = a.Asking;
                    refresh();
                });
            }
            if (!string.IsNullOrEmpty(s.Message)) UiFactory.Paragraph(right, "<b>" + s.Message + "</b>", Theme.TextBody);

            var row = UiFactory.Row(right, Ui.Grid.Cta);
            var cols = Ui.Columns12(row, 6, 6);
            if (s.Counter != null)
            {
                var counter = s.Counter;
                Ui.Secondary(cols[0], Loc.T("neg.useCounter", MathX.FormatMoney(counter.Salary)), () => { s.Proposal = counter; s.Counter = null; refresh(); });
            }
            Ui.PrimaryCTA(cols[1], Loc.T("neg.propose"), () =>
            {
                var res = ContractService.Propose(world, db, team, r, p);
                s.Message = res.Message;
                s.Counter = res.Counter;
                if (res.Accepted) close();
                refresh();
            });
        }

        private static int ActiveClauses(ContractProposal p)
        {
            int n = 0;
            if (p.SigningBonus > 0) n++;
            if (p.WinBonus > 0) n++;
            if (p.ReleaseClause > 0) n++;
            if (p.TeamOption) n++;
            if (p.PerformanceExtensionTopN > 0) n++;
            if (p.ExitForHigherCategory) n++;
            return n;
        }

        /// <summary>
        /// The other clauses, one row each: a tap cycles or switches the clause (accent outline = included), then the
        /// page and the sheet are rebuilt with the new terms.
        /// </summary>
        private static void ClausesSheet(ContractProposal p, Action refresh, Action reopen)
        {
            Action<Action> apply = change =>
            {
                change();
                refresh();
                reopen();
            };
            Ui.Sheet(Loc.T("ux.neg.clausesTitle"), l =>
            {
                var list = (RectTransform)l;
                Clause(list, Loc.T("neg.signing", MathX.FormatMoney(p.SigningBonus)), p.SigningBonus > 0, () => apply(() =>
                {
                    p.SigningBonus = p.SigningBonus <= 0 ? Round(p.Salary * 0.1) : p.SigningBonus < p.Salary * 0.15 ? Round(p.Salary * 0.25) : 0;
                }));
                Clause(list, Loc.T("neg.bonuses", MathX.FormatMoney(p.WinBonus), MathX.FormatMoney(p.PodiumBonus)), p.WinBonus > 0, () => apply(() =>
                {
                    double level = p.WinBonus / Math.Max(1, p.Salary);
                    double next = level < 0.04 ? 0.05 : level < 0.09 ? 0.12 : 0.02;
                    p.WinBonus = Round(p.Salary * next);
                    p.PodiumBonus = Round(p.WinBonus * 0.4);
                }));
                Clause(list, p.ReleaseClause > 0 ? Loc.T("neg.release", MathX.FormatMoney(p.ReleaseClause)) : Loc.T("neg.noRelease"), p.ReleaseClause > 0, () => apply(() =>
                {
                    double ratio = p.ReleaseClause / Math.Max(1, p.Salary * p.Seasons);
                    p.ReleaseClause = p.ReleaseClause <= 0 ? Round(p.Salary * p.Seasons * 1.5) : ratio < 2 ? Round(p.Salary * p.Seasons * 4) : 0;
                }));
                Clause(list, Loc.T("neg.teamOption"), p.TeamOption, () => apply(() => p.TeamOption = !p.TeamOption));
                Clause(list, Loc.T("neg.perf", p.PerformanceExtensionTopN > 0 ? p.PerformanceExtensionTopN : 5), p.PerformanceExtensionTopN > 0,
                    () => apply(() => p.PerformanceExtensionTopN = p.PerformanceExtensionTopN > 0 ? 0 : 5));
                Clause(list, Loc.T("neg.exit"), p.ExitForHigherCategory, () => apply(() => p.ExitForHigherCategory = !p.ExitForHigherCategory));
            }, null, null, 0.5f);
        }

        private static void Clause(RectTransform list, string label, bool on, Action tap)
        {
            if (on) Ui.SecondaryAccent(list, label, tap);
            else Ui.Secondary(list, label, tap);
        }

        /// <summary>
        /// Offers from AI teams for the player's riders: one row each (rider and buyer, expiry, fee, ACEPTAR); a tap
        /// on the row opens a sheet to accept or reject it. The back chevron returns to the riders.
        /// </summary>
        public static void Bids(RectTransform list, GameController game, Action refresh, Action back)
        {
            var world = game.World;
            var team = world.PlayerTeam;
            var card = Ui.Card(list, Loc.T("bids.title"), null, Wp2Ui.Info(Loc.T("bids.title"), Loc.T("bids.note")), back);
            int shown = 0;
            foreach (var o in new List<TransferOffer>(world.TransferOffers))
            {
                var offer = o;
                var r = world.GetRider(o.RiderId);
                var buyer = world.GetTeam(o.ToTeamId);
                if (r == null || buyer == null || (team != null && o.FromTeamId != team.Id)) continue;
                shown++;
                string expires = Loc.T("sponsor.expiresIn", Math.Max(0, o.ExpiresAfterEvent - world.EventsCompleted + 1));
                Action accept = () => { ContractService.AcceptBid(world, game.Db, offer.Id); refresh(); };
                Action reject = () => { ContractService.RejectBid(world, offer.Id); refresh(); };
                Ui.PersonRow(card, r.Attributes.Overall, r.FullName + "  " + Theme.Dim("> " + buyer.Name), expires, MathX.FormatMoney(o.Fee),
                    () => Ui.Sheet(r.FullName, l =>
                    {
                        Ui.Line(l, Loc.T("col.team"), buyer.Name, true);
                        Ui.Line(l, Loc.T("neg.cost.fee"), MathX.FormatMoney(offer.Fee), true);
                        Ui.Line(l, Loc.T("ux.bids.expires"), expires);
                        UiFactory.Paragraph(l, Theme.Dim(Loc.T("bids.note")), Theme.TextCaption);
                    }, UiFactory.Primary(Loc.T("bids.accept"), accept), UiFactory.Act(Loc.T("bids.reject"), reject), 0.5f),
                    UiFactory.Act(Loc.T("bids.accept"), accept));
            }
            if (shown == 0) Ui.Empty(card, null, Loc.T("ux.bids.none"));
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
