using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI
{
    /// <summary>Choices of a team being founded (kept while the screen is rebuilt).</summary>
    public class FoundingState
    {
        public double InvestShare = 1, LoanShare;
        public string Category, Supplier, TeamName = "";
        public string Primary = "#E10600", Secondary = "#FFFFFF";
        public string Message;
        public bool SponsorsGenerated;
    }

    /// <summary>
    /// Founding a team: money in (own wealth and an optional loan), the categories the money can sustain, the bike
    /// supplier, the name and the team colours. The team starts empty: the owner signs riders and staff.
    /// </summary>
    public static class FoundingPanel
    {
        public static readonly string[] Colors =
        {
            "#E10600", "#F26B1D", "#F5C518", "#2DBE60", "#00A3A3", "#1E88E5", "#283593", "#7B3FBF", "#D81B60", "#FFFFFF", "#9E9E9E", "#111111"
        };

        public static void Build(RectTransform list, GameController game, ManagerProfile m, FoundingState st, Action refresh, Action<CareerResult> founded)
        {
            var world = game.World;
            var db = game.Db;
            if (!st.SponsorsGenerated)
            {
                st.SponsorsGenerated = true;
                world.SponsorOffers.RemoveAll(o => o.TeamId == CareerService.FoundingProbeId);
                CareerService.FoundingSponsorOffers(world, db, m, world.Rng.Fork("founding-sponsors-" + world.Season));
            }
            double wealth = m.PersonalWealth;
            double invest = Math.Round(wealth * st.InvestShare);
            var terms = CareerService.LoanTermsFor(m, invest);
            double loan = Math.Round(terms.MaxAmount * st.LoanShare / 10000) * 10000;

            RectTransform leftCol, rightCol;
            Ui.Columns(list, out leftCol, out rightCol);

            // Money.
            var money = Ui.Card(leftCol, Loc.T("career.capital"), Loc.T("career.found.status", MathX.FormatMoney(invest + loan)));
            UiFactory.ButtonRow(money, 50,
                UiFactory.Option(Loc.T("career.invest", "25%"), st.InvestShare == 0.25, () => { st.InvestShare = 0.25; refresh(); }),
                UiFactory.Option(Loc.T("career.invest", "50%"), st.InvestShare == 0.5, () => { st.InvestShare = 0.5; refresh(); }),
                UiFactory.Option(Loc.T("career.invest", "75%"), st.InvestShare == 0.75, () => { st.InvestShare = 0.75; refresh(); }),
                UiFactory.Option(Loc.T("career.invest", "100%"), st.InvestShare == 1, () => { st.InvestShare = 1; refresh(); }));
            UiFactory.ButtonRow(money, 50,
                UiFactory.Option(Loc.T("career.loanNone"), st.LoanShare == 0, () => { st.LoanShare = 0; refresh(); }),
                UiFactory.Option(Loc.T("career.loanHalf"), st.LoanShare == 0.5, () => { st.LoanShare = 0.5; refresh(); }),
                UiFactory.Option(Loc.T("career.loanMax"), st.LoanShare == 1, () => { st.LoanShare = 1; refresh(); }));
            Ui.Line(money, Loc.T("career.ownMoney"), MathX.FormatMoney(invest) + " / " + MathX.FormatMoney(wealth));
            Ui.Line(money, Loc.T("career.loan"), MathX.FormatMoney(loan));
            Ui.Note(money, Loc.T("career.loanTerms", MathX.FormatMoney(terms.MaxAmount), (terms.Rate * 100).ToString("0.0"), terms.Seasons));

            // Categories the money can sustain.
            var cats = Ui.Card(leftCol, Loc.T("career.categories"));
            var w = new[] { 1.6f, 1.8f, 1.6f, 1.6f, 1.6f };
            UiFactory.TableRow(cats, 34, w, new[] { Loc.T("career.col.category"), Loc.T("career.col.verdict"), Loc.T("career.col.startup"), Loc.T("career.col.lowest"), "" }, true);
            foreach (var r in CareerService.FoundingOptions(world, db, m, invest, loan))
            {
                var rep = r;
                var cat = db.GetCategory(r.CategoryId);
                string verdict = r.Verdict == Viability.Viable ? Theme.GoodText(Loc.T("viability." + r.Verdict)) : r.Verdict == Viability.Risky ? Theme.WarnText(Loc.T("viability." + r.Verdict)) : Theme.BadText(Loc.T("viability." + r.Verdict));
                bool selectable = r.Verdict == Viability.Viable || r.Verdict == Viability.Risky;
                var row = UiFactory.TableRow(cats, 44, w, new[] { cat.ShortName, verdict, Theme.Money(-r.StartupTotal), Theme.Money(r.MinCashConfirmed), "" }, false,
                    st.Category == r.CategoryId ? new Color(0.94f, 0.7f, 0.16f, 0.16f) : (Color?)null);
                if (selectable)
                {
                    var b = UiFactory.Button("Pick", row, Loc.T("career.choose"), () => { st.Category = rep.CategoryId; st.Supplier = null; refresh(); },
                        st.Category == r.CategoryId ? Theme.Accent : Theme.PanelLight, Theme.FontSmall - 6);
                    UiFactory.InColumn(b, w, w.Length - 1);
                }
            }

            // Identity: supplier, name, colours.
            var id = Ui.Card(rightCol, Loc.T("found.identity"));
            if (st.Category == null)
            {
                Ui.Note(id, Loc.T("found.pickCategory"));
            }
            else
            {
                var cat = db.GetCategory(st.Category);
                UiFactory.Paragraph(id, "<b>" + Loc.T("career.supplier", cat.Name) + "</b>", Theme.FontSmall);
                var acts = new List<UiAct>();
                foreach (var maker in CareerService.Suppliers(world, st.Category))
                {
                    var mk = maker;
                    acts.Add(UiFactory.Option(mk.Name, st.Supplier == mk.Id, () => { st.Supplier = mk.Id; refresh(); }));
                }
                UiFactory.ButtonRow(id, 50, acts.ToArray());
            }
            UiFactory.Input(id, Loc.T("career.teamName"), st.TeamName, v => st.TeamName = v);
            Swatches(id, Loc.T("found.primary"), st.Primary, c => { st.Primary = c; refresh(); });
            Swatches(id, Loc.T("found.secondary"), st.Secondary, c => { st.Secondary = c; refresh(); });
            // Preview: a number board in the chosen livery.
            var preview = UiFactory.Row(id, 70);
            var plate = UiFactory.Panel("Plate", preview, UiFactory.ParseColor(st.Primary, Color.red), true);
            UiFactory.Place(plate.rectTransform, 0.3f, 0.05f, 0.7f, 0.95f);
            var num = UiFactory.Label("Number", plate.transform, (string.IsNullOrEmpty(st.TeamName.Trim()) ? m.LastName + " Racing" : st.TeamName.Trim()).ToUpperInvariant(),
                Theme.FontSmall, TextAnchor.MiddleCenter, UiFactory.ParseColor(st.Secondary, Color.white));
            num.fontStyle = FontStyle.Bold;
            UiFactory.Stretch(num.rectTransform);
            Ui.Note(id, Loc.T("found.emptyTeam"));

            if (st.Category != null && st.Supplier != null)
            {
                UiFactory.ButtonRow(id, 64, UiFactory.Primary(Loc.T("career.confirmFound"), () =>
                {
                    string name = string.IsNullOrEmpty(st.TeamName.Trim()) ? m.LastName + " Racing" : st.TeamName.Trim();
                    var res = CareerService.Found(world, db, m, new FoundingPlan
                    {
                        CategoryId = st.Category, ManufacturerId = st.Supplier, TeamName = name, Investment = invest, Loan = loan,
                        PrimaryColor = st.Primary, SecondaryColor = st.Secondary
                    });
                    st.Message = res.Ok ? null : res.Message;
                    founded(res);
                }));
            }
            if (!string.IsNullOrEmpty(st.Message)) UiFactory.Paragraph(id, Theme.WarnText(st.Message), Theme.FontSmall);
        }

        public static void Swatches(Transform card, string label, string selected, Action<string> pick)
        {
            var row = UiFactory.Row(card, 48);
            var l = UiFactory.Label("Label", row, label, Theme.FontSmall - 2, TextAnchor.MiddleLeft, Theme.TextDim);
            UiFactory.Place(l.rectTransform, 0, 0, 0.22f, 1);
            float w = 0.78f / Colors.Length;
            for (int i = 0; i < Colors.Length; i++)
            {
                string hex = Colors[i];
                bool on = string.Equals(hex, selected, StringComparison.OrdinalIgnoreCase);
                var b = UiFactory.Button("Swatch", row, on ? "●" : "", () => pick(hex), UiFactory.ParseColor(hex, Color.gray), Theme.FontSmall - 4);
                var t = b.GetComponentInChildren<UnityEngine.UI.Text>();
                t.color = hex == "#FFFFFF" || hex == "#F5C518" ? Color.black : Color.white;
                UiFactory.Place((RectTransform)b.transform, 0.22f + i * w, 0.08f, 0.22f + (i + 1) * w, 0.92f, 3);
            }
        }
    }
}
