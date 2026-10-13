using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>Choices of a team being founded (kept while the screen is rebuilt).</summary>
    public class FoundingState
    {
        public double InvestShare = 0.5, LoanShare;
        public string Category, Supplier, TeamName = "";
        public string Primary = "#E10600", Secondary = "#FFFFFF";
        public string Message;
        public bool SponsorsGenerated;
    }

    /// <summary>
    /// Founding a team (UX spec §3.20), on one screen: on the left the CAPITAL tile (how much of the manager's money
    /// goes in, the loan, the total) over the categories that money can sustain (a row per category with its verdict
    /// and start-up cost; a viable row selects it, the others explain why not); on the right the team's identity
    /// (bike supplier, name, two colour rows) with the screen's only primary, FUNDAR. Loan terms, founding sponsors and
    /// the full forecast of the chosen category sit behind "i" buttons. The team starts empty: the owner signs riders
    /// and staff. Used by the career page and by the new-career flow.
    /// </summary>
    public static class FoundingPanel
    {
        public static readonly string[] Colors =
        {
            "#E10600", "#F26B1D", "#F5C518", "#2DBE60", "#00A3A3", "#1E88E5", "#283593", "#7B3FBF", "#D81B60", "#FFFFFF", "#9E9E9E", "#111111"
        };

        private const float CapitalHeight = 268;

        /// <summary>Builds the founding screen into <paramref name="area"/> (an anchor-layout rect it fills).</summary>
        public static void Build(RectTransform area, GameController game, ManagerProfile m, FoundingState st, Action refresh, Action<CareerResult> founded)
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
            var reports = CareerService.FoundingOptions(world, db, m, invest, loan);

            var cols = Ui.Columns12(area, 6, 6);
            var capArea = Wp4Ui.TopStrip(cols[0], CapitalHeight);
            var cap = Wp4Ui.ListIn(Ui.Tile(capArea, Loc.T("career.capital"), null, null, 0, null, Wp4Ui.Info(Loc.T("career.capital"),
                Loc.T("career.ownMoney") + ": <b>" + MathX.FormatMoney(invest) + "</b> / " + MathX.FormatMoney(wealth),
                Loc.T("career.wealthLeft") + ": <b>" + MathX.FormatMoney(wealth - invest) + "</b>",
                Loc.T("career.loan") + ": <b>" + MathX.FormatMoney(loan) + "</b>  " + Loc.T("career.loanTerms", MathX.FormatMoney(terms.MaxAmount), (terms.Rate * 100).ToString("0.0"), terms.Seasons),
                SponsorsText(world, db))));
            var investShares = new[] { 0.25, 0.5, 0.75, 1.0 };
            Wp4Ui.CaptionedSegmented(cap, Loc.T("ux.found.invest"), new[] { "25%", "50%", "75%", "100%" }, Array.IndexOf(investShares, st.InvestShare),
                i => { st.InvestShare = investShares[i]; refresh(); });
            LoanSegmented(cap, st.LoanShare, s => { st.LoanShare = s; refresh(); });
            Ui.Line(cap, Theme.Caps(Loc.T("ux.found.total")), "<b>" + MathX.FormatMoney(invest + loan) + "</b>  " +
                Theme.Dim(Loc.T("ux.found.split", MathX.FormatMoney(invest), MathX.FormatMoney(loan))), true);

            CategoryTile(Wp4Ui.Below(cols[0], CapitalHeight + Ui.Grid.Gap), db, reports, st.Category, id =>
            {
                st.Category = id;
                st.Supplier = null;
                refresh();
            });

            ViabilityReport chosen = null;
            foreach (var r in reports) if (r.CategoryId == st.Category) chosen = r;
            string fallbackName = m.LastName + " Racing";
            IdentityTile(cols[1], world, db, st.Category, st.Supplier, s => { st.Supplier = s; refresh(); },
                st.TeamName, v => st.TeamName = v, fallbackName, st.Primary, c => { st.Primary = c; refresh(); }, st.Secondary, c => { st.Secondary = c; refresh(); },
                chosen, st.Message, Loc.T("career.confirmFound"), () =>
                {
                    string name = string.IsNullOrEmpty(st.TeamName.Trim()) ? fallbackName : st.TeamName.Trim();
                    var res = CareerService.Found(world, db, m, new FoundingPlan
                    {
                        CategoryId = st.Category, ManufacturerId = st.Supplier, TeamName = name, Investment = invest, Loan = loan,
                        PrimaryColor = st.Primary, SecondaryColor = st.Secondary
                    });
                    st.Message = res.Ok ? null : res.Message;
                    founded(res);
                }, Loc.T("found.emptyTeam"));
        }

        /// <summary>The founding sponsors (each tied to a category) as one paragraph for an info sheet.</summary>
        private static string SponsorsText(WorldState world, Core.Data.GameDatabase db)
        {
            var lines = new List<string>();
            foreach (var o in world.SponsorOffers)
            {
                if (o.TeamId != CareerService.FoundingProbeId) continue;
                var cat = db.GetCategory(o.RequiredCategoryId);
                lines.Add("<b>" + o.Name + "</b> " + Theme.Dim(o.Industry) + "  " +
                          Loc.T("career.sponsorTerms", MathX.FormatMoney(o.PaymentPerSeason), o.Seasons, cat != null ? cat.ShortName : o.RequiredCategoryId));
            }
            return "<b>" + Loc.T("career.sponsors") + "</b>\n" + (lines.Count > 0 ? string.Join("\n", lines.ToArray()) : Theme.Dim(Loc.T("career.noSponsors")));
        }

        /// <summary>Caption "PRÉSTAMO" and the three loan choices (none, half, the most the bank lends).</summary>
        public static void LoanSegmented(Transform list, double share, Action<double> pick)
        {
            var shares = new[] { 0.0, 0.5, 1.0 };
            Wp4Ui.CaptionedSegmented(list, Loc.T("ux.found.loan"), new[] { Loc.T("ux.found.loanNone"), Loc.T("ux.found.loanHalf"), Loc.T("ux.found.loanMax") },
                Array.IndexOf(shares, share), i => pick(shares[i]));
        }

        /// <summary>A verdict as a coloured pill text.</summary>
        public static Color VerdictColor(Viability v)
        {
            return v == Viability.Viable ? Theme.Good : v == Viability.Risky ? Theme.Warning : Theme.Bad;
        }

        /// <summary>
        /// The categories tile: CATEGORÍA | VEREDICTO | ARRANQUE rows. A viable or risky row selects the category (lit
        /// in the accent once chosen); the others open a sheet with the reasons and the forecast.
        /// </summary>
        public static void CategoryTile(RectTransform area, Core.Data.GameDatabase db, List<ViabilityReport> reports, string selected, Action<string> select,
            string title = null, Func<ViabilityReport, string> extra = null)
        {
            var body = Ui.Tile(area, title ?? Loc.T("career.categories"), null, null, 0, null,
                Wp4Ui.Info(title ?? Loc.T("career.categories"), Loc.T("ux.found.categoriesInfo")));
            var widths = new[] { 3f, 3.2f, 3f };
            Wp4Ui.Captions(Wp4Ui.TopStrip(body, 40), widths, new[] { Loc.T("career.col.category"), Loc.T("career.col.verdict"), Loc.T("career.col.startup") }, true);
            var list = Wp4Ui.ScrollIn(Wp4Ui.Below(body, 44), 4);
            if (reports.Count == 0)
            {
                Ui.Empty(list, null, Loc.T("ux.found.noCategories"));
                return;
            }
            for (int i = 0; i < reports.Count; i++)
            {
                var r = reports[i];
                var cat = db.GetCategory(r.CategoryId);
                bool selectable = r.Verdict == Viability.Viable || r.Verdict == Viability.Risky;
                var rep = r;
                string name = cat != null ? cat.ShortName : r.CategoryId;
                var row = Wp4Ui.SelectRow(list, widths, new[] { "<b>" + name + "</b>", "", Theme.Money(-r.StartupTotal) }, i, selected == r.CategoryId, selectable
                    ? (Action)(() => select(rep.CategoryId))
                    : () => ReportSheet(name, rep, extra));
                Wp4Ui.CellPill(row, widths, 1, Loc.T("viability." + r.Verdict), VerdictColor(r.Verdict));
            }
        }

        /// <summary>Every figure of a category's forecast, its reasons and what is missing.</summary>
        public static void ReportSheet(string title, ViabilityReport r, Func<ViabilityReport, string> extra = null)
        {
            Ui.Sheet(title, list => ReportLines(list, r, extra), null, null, 0.56f);
        }

        public static void ReportLines(Transform list, ViabilityReport r, Func<ViabilityReport, string> extra = null)
        {
            var pill = Ui.Pill(UiFactory.Row(list, 40), Loc.T("viability." + r.Verdict), VerdictColor(r.Verdict));
            pill.rectTransform.anchorMin = pill.rectTransform.anchorMax = new Vector2(0, 0.5f);
            pill.rectTransform.pivot = new Vector2(0, 0.5f);
            pill.rectTransform.anchoredPosition = new Vector2(4, 0);
            Ui.Line(list, Loc.T("career.col.startup"), Theme.Money(-r.StartupTotal));
            Ui.Line(list, Loc.T("career.col.operating"), Theme.Money(-r.OperatingCost));
            Ui.Line(list, Loc.T("career.col.confirmed"), MathX.FormatMoney(r.ConfirmedIncome));
            Ui.Line(list, Loc.T("career.col.probable"), MathX.FormatMoney(r.ProbableIncome));
            Ui.Line(list, Loc.T("career.col.lowest"), Theme.Money(r.MinCashConfirmed));
            Ui.Line(list, Loc.T("career.col.reserve"), MathX.FormatMoney(r.Reserve));
            UiFactory.Paragraph(list, Loc.T("career.summary", MathX.FormatMoney(r.StartupTotal), MathX.FormatMoney(r.LiquidityAfterStartup),
                MathX.FormatMoney(r.EndCashConfirmed), MathX.FormatMoney(r.EndCashProbable)), Theme.TextBody);
            if (r.Reasons.Count > 0)
            {
                var reasons = new List<string>();
                foreach (var reason in r.Reasons) reasons.Add(Loc.T("reason." + reason));
                string missing = r.Shortfall > 0 && r.Verdict != Viability.NotAvailable ? " " + Loc.T("career.shortfall", MathX.FormatMoney(r.Shortfall)) : "";
                UiFactory.Paragraph(list, Theme.Dim(string.Join(" · ", reasons.ToArray()) + missing), Theme.TextBody);
            }
            if (r.Verdict == Viability.Risky) UiFactory.Paragraph(list, Theme.WarnText(Loc.T("career.riskyWarning")), Theme.TextBody);
            if (extra != null)
            {
                string e = extra(r);
                if (!string.IsNullOrEmpty(e)) UiFactory.Paragraph(list, e, Theme.TextBody);
            }
        }

        /// <summary>
        /// The identity tile: bike supplier (segmented, or a picker beyond five), team name, two colour rows, and at the
        /// bottom a plate in the livery next to the primary button (disabled until category and supplier are chosen).
        /// </summary>
        public static void IdentityTile(RectTransform area, WorldState world, Core.Data.GameDatabase db, string category, string supplier, Action<string> pickSupplier,
            string teamName, Action<string> nameChanged, string fallbackName, string primary, Action<string> pickPrimary, string secondary, Action<string> pickSecondary,
            ViabilityReport report, string message, string ctaLabel, Action confirm, string info)
        {
            var cat = category != null ? db.GetCategory(category) : null;
            Action onInfo = report != null
                ? (Action)(() => Ui.Sheet(Loc.T("found.identity"), l =>
                {
                    UiFactory.Paragraph(l, info, Theme.TextBody);
                    Ui.Section(l, cat != null ? cat.Name : category);
                    ReportLines(l, report);
                }, null, null, 0.56f))
                : Wp4Ui.Info(Loc.T("found.identity"), info);
            var body = Ui.Tile(area, Loc.T("found.identity"), null, cat != null ? Theme.Caps(cat.Name) : null, 0, null, onInfo);
            var bottom = Wp4Ui.BottomStrip(body, Ui.Grid.Cta);
            var list = Wp4Ui.ListIn(Wp4Ui.Below(body, 0, Ui.Grid.Cta + 8), 8);
            if (category == null)
            {
                Ui.Empty(list, UiIcons.Bike, Loc.T("ux.found.pickCategory"));
            }
            else
            {
                var makers = CareerService.Suppliers(world, category);
                var names = new List<string>();
                int sel = -1;
                for (int i = 0; i < makers.Count; i++)
                {
                    names.Add(makers[i].Name);
                    if (makers[i].Id == supplier) sel = i;
                }
                if (makers.Count <= 5)
                    Wp4Ui.CaptionedSegmented(list, Loc.T("setup.supplier"), names, sel, i => pickSupplier(makers[i].Id));
                else
                    Wp4Ui.CaptionedPicker(list, Loc.T("setup.supplier"), names, sel, i => pickSupplier(makers[i].Id), Loc.T("ux.found.pickSupplier"));
            }
            UiFactory.Input(list, Loc.T("career.teamName"), teamName, nameChanged, 56);
            Swatches(list, Loc.T("found.primary"), primary, pickPrimary);
            Swatches(list, Loc.T("found.secondary"), secondary, pickSecondary);
            if (!string.IsNullOrEmpty(message)) Ui.Alert(list, message, true);
            else if (report != null && report.Verdict == Viability.Risky) Ui.Alert(list, Loc.T("ux.found.risky"));

            var bc = Ui.Columns12(bottom, 5, 7);
            Plate(bc[0], string.IsNullOrEmpty((teamName ?? "").Trim()) ? fallbackName : teamName.Trim(), primary, secondary);
            bool ready = category != null && supplier != null;
            string blocker = category == null ? Loc.T("ux.found.needCategory") : supplier == null ? Loc.T("ux.found.needSupplier") : null;
            Ui.PrimaryCTA(bc[1], ctaLabel, ready ? confirm : null, blocker, ready);
        }

        /// <summary>The team name on a plate in its colours.</summary>
        public static void Plate(RectTransform area, string name, string primary, string secondary)
        {
            var plate = UiFactory.Panel("Plate", area, UiFactory.ParseColor(primary, Color.red));
            plate.raycastTarget = false;
            UiFactory.Slant(plate, 2.4f);
            UiFactory.Stretch(plate.rectTransform);
            plate.rectTransform.offsetMin = new Vector2(0, 6);
            plate.rectTransform.offsetMax = new Vector2(0, -6);
            var t = Ui.Label(plate.transform, Theme.Caps(name), Theme.TextHeader, TextAnchor.MiddleCenter, UiFactory.ParseColor(secondary, Color.white), FontStyle.BoldAndItalic);
            UiFactory.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(18, 0);
            t.rectTransform.offsetMax = new Vector2(-18, 0);
        }

        /// <summary>A caption and twelve colour swatches on a 56-unit row; the chosen one is framed and full size.</summary>
        public static void Swatches(Transform card, string label, string selected, Action<string> pick)
        {
            var row = UiFactory.Row(card, Ui.Grid.Hit);
            var l = Ui.Label(row, Theme.Caps(label), Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(l.rectTransform, 0, 0, 0.26f, 1);
            l.rectTransform.offsetMin = new Vector2(4, 0);
            float w = 0.74f / Colors.Length;
            for (int i = 0; i < Colors.Length; i++)
            {
                string hex = Colors[i];
                bool on = string.Equals(hex, selected, StringComparison.OrdinalIgnoreCase);
                var b = UiFactory.Button("Swatch", row, "", () => pick(hex), UiFactory.ParseColor(hex, Color.gray), Theme.TextMin);
                UiFactory.Place((RectTransform)b.transform, 0.26f + i * w, 0, 0.26f + (i + 1) * w, 1, on ? 2 : 8);
                if (!on) continue;
                var o = b.gameObject.AddComponent<Outline>();
                o.effectColor = Color.white;
                o.effectDistance = new Vector2(3, -3);
            }
        }
    }

    /// <summary>A sortable-looking table: an accent band, column captions and a scroll list of rows (WP4 screens).</summary>
    internal class Wp4Table
    {
        public RectTransform Area, List;
        public ScrollRect Scroll;
        public float RowHeight, Spacing = 2;
    }

    /// <summary>Small layout helpers shared by the competition, career, boot, start and review screens (WP4).</summary>
    internal static class Wp4Ui
    {
        /// <summary>An action opening a sheet with a title and one paragraph per string.</summary>
        public static Action Info(string title, params string[] paragraphs)
        {
            return () => Ui.Sheet(title, list =>
            {
                foreach (var p in paragraphs)
                    if (!string.IsNullOrEmpty(p)) UiFactory.Paragraph(list, p, Theme.TextBody);
            });
        }

        /// <summary>Turns an anchor-layout rect into a vertical list.</summary>
        public static RectTransform ListIn(RectTransform area, float spacing = 8)
        {
            var v = area.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return area;
        }

        /// <summary>A vertical scroll list filling <paramref name="area"/> with no side padding.</summary>
        public static RectTransform ScrollIn(RectTransform area, float spacing = 8, int padBottom = 8)
        {
            var list = UiFactory.ScrollList("List", area, spacing);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var v = list.GetComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(0, 0, 0, padBottom);
            return list;
        }

        /// <summary>A strip of fixed height along the top of <paramref name="area"/>.</summary>
        public static RectTransform TopStrip(RectTransform area, float height)
        {
            var rt = UiFactory.Rect("Top", area);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(0, -height);
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>A strip of fixed height along the bottom of <paramref name="area"/>.</summary>
        public static RectTransform BottomStrip(RectTransform area, float height)
        {
            var rt = UiFactory.Rect("Bottom", area);
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0, height);
            return rt;
        }

        /// <summary>The rest of <paramref name="area"/> below <paramref name="top"/> units (and above <paramref name="bottom"/>).</summary>
        public static RectTransform Below(RectTransform area, float top, float bottom = 0)
        {
            var rt = UiFactory.Rect("Rest", area);
            UiFactory.Stretch(rt);
            rt.offsetMin = new Vector2(0, bottom);
            rt.offsetMax = new Vector2(0, -top);
            return rt;
        }

        /// <summary>A right chevron at the end of a button (its label makes room).</summary>
        public static void Chevron(Button b)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t == null) return;
            t.rectTransform.offsetMax = new Vector2(-44, t.rectTransform.offsetMax.y);
            var ch = UiIcons.Chevron(b.transform, t.color);
            ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(1, 0.5f);
            ch.rectTransform.sizeDelta = new Vector2(22, 22);
            ch.rectTransform.anchoredPosition = new Vector2(-26, 0);
        }

        /// <summary>A link going back: a left chevron and dim CAPS text, no fill (56 high).</summary>
        public static Button BackLink(RectTransform area, string label, Action onTap)
        {
            var b = Ui.Ghost(area, label, onTap);
            var t = b.GetComponentInChildren<Text>();
            t.alignment = TextAnchor.MiddleLeft;
            t.rectTransform.offsetMin = new Vector2(44, 2);
            t.rectTransform.offsetMax = new Vector2(-12, -2);
            foreach (Transform child in b.transform)
            {
                var img = child.GetComponent<Image>();
                if (img == null || child == t.transform) continue;
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0, 0.5f);
                img.rectTransform.localEulerAngles = Vector3.zero;
                img.rectTransform.anchoredPosition = new Vector2(20, 0);
            }
            return b;
        }

        /// <summary>A "..." button at the right end of a tile's header strip (the right text and title make room).</summary>
        public static Button HeaderMore(RectTransform tileBody, Action onTap)
        {
            var head = tileBody.parent.Find("Head");
            if (head == null) return null;
            foreach (Transform child in head)
            {
                var crt = child as RectTransform;
                if (crt == null) continue;
                if (child.name == "Right" || (child.name == "Title" && head.Find("Right") == null)) crt.offsetMax -= new Vector2(Ui.Grid.Hit, 0);
                else if (child.name == "Info" || child.name == "Badge" || child.name == "Chevron") crt.anchoredPosition -= new Vector2(Ui.Grid.Hit, 0);
            }
            var b = Ui.IconButton(head, Glyph.More, onTap);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-2, 0);
            return b;
        }

        /// <summary>A caption on the left and a segmented control on the rest of a 56-unit row.</summary>
        public static void CaptionedSegmented(Transform list, string caption, IList<string> labels, int selected, Action<int> onSelect)
        {
            var row = UiFactory.Row(list, Ui.Grid.Hit);
            var c = Ui.Label(row, Theme.Caps(caption), Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(c.rectTransform, 0, 0, 0.26f, 1);
            c.rectTransform.offsetMin = new Vector2(4, 0);
            var holder = UiFactory.Rect("Segments", row);
            UiFactory.Place(holder, 0.26f, 0, 1, 1);
            ListIn(holder, 0);
            Ui.Segmented(holder, labels, selected, onSelect);
        }

        /// <summary>A caption on the left and a picker pill on the rest of a 56-unit row.</summary>
        public static void CaptionedPicker(Transform list, string caption, IList<string> options, int selected, Action<int> onSelect, string none = null)
        {
            var row = UiFactory.Row(list, Ui.Grid.Hit);
            var c = Ui.Label(row, Theme.Caps(caption), Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(c.rectTransform, 0, 0, 0.26f, 1);
            c.rectTransform.offsetMin = new Vector2(4, 0);
            var holder = UiFactory.Rect("Picker", row);
            UiFactory.Place(holder, 0.26f, 0, 1, 1);
            var b = Ui.Picker(holder, caption, options, selected, onSelect);
            if (selected < 0 && none != null) b.GetComponentInChildren<Text>().text = Theme.Caps(none);
        }

        /// <summary>
        /// Two-tap confirmation on a button built by the caller: the first tap swaps the label for
        /// <paramref name="confirm"/>, the second runs <paramref name="run"/>. The arming is lost when the page rebuilds.
        /// </summary>
        public static Action TwoTap(Func<Button> button, string confirm, Action run)
        {
            bool armed = false;
            return () =>
            {
                if (armed)
                {
                    run();
                    return;
                }
                armed = true;
                var b = button();
                var t = b != null ? b.GetComponentInChildren<Text>() : null;
                if (t != null) t.text = Theme.Caps(confirm);
                if (b != null && b.image != null && b.gameObject.name != "PrimaryCTA") b.image.color = Color.Lerp(b.image.color, Theme.Warning, 0.35f);
            };
        }

        /// <summary>The screen's primary button with a two-tap confirmation.</summary>
        public static Button TwoTapPrimary(RectTransform area, string label, string confirm, Action run, string sub = null)
        {
            Button b = null;
            b = Ui.PrimaryCTA(area, label, TwoTap(() => b, confirm, run), sub);
            return b;
        }

        /// <summary>A secondary button with a two-tap confirmation.</summary>
        public static Button TwoTapSecondary(RectTransform area, string label, string confirm, Action run)
        {
            Button b = null;
            b = Ui.Secondary(area, label, TwoTap(() => b, confirm, run));
            return b;
        }

        /// <summary>Column captions (dim CAPS 20 on a dark strip) filling <paramref name="area"/>; leaves room for row chevrons.</summary>
        public static void Captions(RectTransform area, float[] widths, string[] captions, bool chevron)
        {
            var bg = UiFactory.Panel("Captions", area, new Color(0, 0, 0, 0.5f));
            bg.raycastTarget = false;
            UiFactory.Stretch(bg.rectTransform);
            float total = 0, x = 0;
            foreach (var w in widths) total += w;
            if (total <= 0) total = 1;
            var inner = UiFactory.Rect("Inner", bg.transform);
            UiFactory.Stretch(inner);
            inner.offsetMax = new Vector2(chevron ? -44 : 0, 0);
            for (int i = 0; i < captions.Length && i < widths.Length; i++)
            {
                var c = Ui.Label(inner, Theme.Caps(captions[i] ?? ""), Theme.TextCaption, i <= 1 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter,
                    Color.Lerp(Theme.TextDim, Color.white, 0.15f), FontStyle.Bold);
                UiFactory.Place(c.rectTransform, x / total, 0, (x + widths[i]) / total, 1);
                c.rectTransform.offsetMin = new Vector2(i == 0 ? 14 : 8, 0);
                c.rectTransform.offsetMax = new Vector2(-4, 0);
                x += widths[i];
            }
        }

        /// <summary>
        /// A table filling <paramref name="area"/>: the accent band (title, right figure, optional info), the column
        /// captions and a scroll list for <see cref="BigRow"/>s.
        /// </summary>
        public static Wp4Table Table(RectTransform area, string title, string right, float[] widths, string[] captions, float rowHeight, bool tappable,
            string infoTitle = null, params string[] info)
        {
            var bg = UiFactory.Panel("Table", area, Theme.Card);
            bg.raycastTarget = false;
            UiFactory.Stretch(bg.rectTransform);
            UiFactory.Surface(bg);
            bg.gameObject.AddComponent<Outline>().effectColor = Theme.Edge;
            var head = ListIn(TopStrip(bg.rectTransform, 56 + 40), 0);
            var band = Ui.Band(head, title, right, 56);
            if (info != null && info.Length > 0) Ui.InfoButton(band, infoTitle ?? title, info);
            Captions(UiFactory.Row(head, 40), widths, captions, tappable);
            var list = ScrollIn(Below(bg.rectTransform, 56 + 40 + 2), 2, 8);
            return new Wp4Table
            {
                Area = bg.rectTransform, List = list, Scroll = list.parent.parent.GetComponent<ScrollRect>(), RowHeight = rowHeight
            };
        }

        /// <summary>A tall list row: <see cref="Ui.ListRow(Transform,float[],string[],int,bool,Action,float)"/> with the position, name and last cell at 28.</summary>
        public static RectTransform BigRow(Transform list, float[] widths, string[] cells, int index, bool mine, Action onTap, float height)
        {
            var row = Ui.ListRow(list, widths, cells, index, mine, onTap, height);
            foreach (var t in row.GetComponentsInChildren<Text>())
            {
                string n = t.gameObject.name;
                if (n == "Cell0" || n == "Cell1" || (cells.Length > 2 && n == "Cell" + (cells.Length - 1)))
                {
                    t.fontSize = 28;
                    Ui.Fit(t, 28);
                }
            }
            return row;
        }

        /// <summary>
        /// Pins a copy of the player's row (built by <paramref name="build"/>) under the table whenever the real row is
        /// not fully in view, like a timing tower.
        /// </summary>
        public static void PinRow(Wp4Table table, int index, Action<Transform> build)
        {
            if (index < 0) return;
            float h = table.RowHeight;
            var pin = UiFactory.Rect("Pinned", table.Area);
            pin.anchorMin = new Vector2(0, 0);
            pin.anchorMax = new Vector2(1, 0);
            pin.pivot = new Vector2(0.5f, 0);
            pin.offsetMin = Vector2.zero;
            pin.offsetMax = new Vector2(0, h + 6);
            var shade = pin.gameObject.AddComponent<Image>();
            shade.color = Theme.Card;
            shade.raycastTarget = false;
            var line = UiFactory.Decor("Edge", pin, Theme.Alpha(Theme.CurrentAccent, 0.8f));
            line.rectTransform.anchorMin = new Vector2(0, 1);
            line.rectTransform.anchorMax = new Vector2(1, 1);
            line.rectTransform.offsetMin = new Vector2(0, -2);
            line.rectTransform.offsetMax = Vector2.zero;
            var holder = UiFactory.Rect("Row", pin);
            UiFactory.Stretch(holder);
            holder.offsetMax = new Vector2(0, -4);
            build(holder);
            foreach (Transform child in holder) UiFactory.Stretch((RectTransform)child);
            var v = table.List.GetComponent<VerticalLayoutGroup>();
            if (v != null) v.padding = new RectOffset(0, 0, 0, (int)(h + 16));
            var viewport = (RectTransform)table.List.parent;
            Func<bool> hidden = () =>
            {
                float vh = viewport.rect.height;
                if (vh <= 1) return index >= 7;
                float y = table.List.anchoredPosition.y;
                float top = index * (h + table.Spacing), bottom = top + h;
                return top < y - 1 || bottom > y + vh - (h + 6);
            };
            pin.gameObject.SetActive(hidden());
            table.Scroll.onValueChanged.AddListener(_ =>
            {
                if (pin != null) pin.gameObject.SetActive(hidden());
            });
        }

        /// <summary>Scrolls the table so row <paramref name="index"/> is near the top (one row of context above it).</summary>
        public static void ScrollTo(Wp4Table table, int index)
        {
            if (index <= 1) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(table.List);
            var viewport = (RectTransform)table.List.parent;
            float max = Mathf.Max(0, table.List.rect.height - viewport.rect.height);
            float y = (index - 1) * (table.RowHeight + table.Spacing);
            table.List.anchoredPosition = new Vector2(table.List.anchoredPosition.x, Mathf.Clamp(y, 0, max));
        }

        /// <summary>
        /// A 64-unit selectable row (no position block): cells left (first) and centred, alternate shade, the chosen row
        /// lit in the accent with an edge bar; the whole row is the button with a chevron.
        /// </summary>
        public static RectTransform SelectRow(Transform list, float[] widths, string[] cells, int index, bool selected, Action onTap, float height = 64)
        {
            var row = UiFactory.Row(list, height);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = selected ? Theme.Alpha(Theme.CurrentAccent, 0.32f) : index % 2 == 0 ? new Color(1, 1, 1, 0.055f) : new Color(1, 1, 1, 0.02f);
            if (selected) UiFactory.SideStripe(row, Theme.CurrentAccent, 6, 0);
            if (onTap != null)
            {
                var b = row.gameObject.AddComponent<Button>();
                b.targetGraphic = bg;
                b.onClick.AddListener(() =>
                {
                    GameAudio.ClickSound();
                    onTap();
                });
                var ch = UiIcons.Chevron(row, Theme.TextDim);
                ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(1, 0.5f);
                ch.rectTransform.sizeDelta = new Vector2(24, 24);
                ch.rectTransform.anchoredPosition = new Vector2(-22, 0);
            }
            else bg.raycastTarget = false;
            var area = UiFactory.Rect("Cells", row);
            UiFactory.Stretch(area);
            area.offsetMax = new Vector2(onTap != null ? -44 : 0, 0);
            float total = 0, x = 0;
            foreach (var w in widths) total += w;
            if (total <= 0) total = 1;
            for (int i = 0; i < cells.Length && i < widths.Length; i++)
            {
                var t = Ui.Label(area, cells[i] ?? "", Theme.TextBody, i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, Theme.Text,
                    i == 0 ? FontStyle.Bold : FontStyle.Normal);
                t.gameObject.name = "Cell" + i;
                UiFactory.Place(t.rectTransform, x / total, 0, (x + widths[i]) / total, 1);
                t.rectTransform.offsetMin = new Vector2(i == 0 ? 16 : 4, 0);
                t.rectTransform.offsetMax = new Vector2(-4, 0);
                x += widths[i];
            }
            return row;
        }

        /// <summary>A pill centred in column <paramref name="column"/> of a row built with the same widths.</summary>
        public static Image CellPill(RectTransform row, float[] widths, int column, string text, Color fill)
        {
            var cells = row.Find("Cells") as RectTransform ?? row;
            float total = 0, x0 = 0;
            for (int i = 0; i < widths.Length; i++)
            {
                if (i < column) x0 += widths[i];
                total += widths[i];
            }
            float c = (x0 + widths[column] / 2) / total;
            var pill = Ui.Pill(cells, text, Theme.Alpha(fill, 0.85f));
            pill.rectTransform.anchorMin = pill.rectTransform.anchorMax = new Vector2(c, 0.5f);
            pill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            pill.rectTransform.anchoredPosition = Vector2.zero;
            return pill;
        }
    }
}
