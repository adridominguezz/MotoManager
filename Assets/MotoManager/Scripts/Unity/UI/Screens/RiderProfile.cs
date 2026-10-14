using System;
using System.Collections.Generic;
using System.Linq;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// A rider's profile, after the user's reference (a manager game's driver profile). Top block: the race number on
    /// the team colour, first name over the LAST NAME with the country, the stars, age, rating and morale, ‹ › to the
    /// team-mate; the status in the team (Principal / Igualdad / Secundario, <see cref="RiderStatusRules"/>); and the
    /// contract actions (renew, the page's one primary only when the contract is ending; release with the two-tap
    /// confirmation). Bottom: four tabs (ATRIBUTOS, RASGOS, CONTRATO, HISTORIAL) whose content fills the middle. A
    /// renewal opens the negotiation over the whole page.
    /// </summary>
    public class RiderProfilePage : ScreenBase
    {
        public override string Title { get { return Loc.T("riders2.profile"); } }
        public override bool HiddenTab { get { return true; } }

        /// <summary>The rider to show: set it before <c>Shell.Show&lt;RiderProfilePage&gt;()</c> (the team's first rider when unset).</summary>
        public static string RiderId;
        private int _tab;
        private NegotiationState _neg;
        private const float TopHeight = 196, TabsHeight = 64, StatusWidth = 420, ActionsWidth = 300;
        private static readonly RiderStatus[] Order = { RiderStatus.Lead, RiderStatus.Equal, RiderStatus.Second };

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            var riders = RiderFacts.Riders(world, team);
            var r = riders.Find(x => x.Id == RiderId);
            if (r == null && riders.Count > 0)
            {
                r = riders[0];
                RiderId = r.Id;
            }
            if (r == null)
            {
                Ui.Empty(Page(content), UiIcons.Person, Loc.T("riders2.noRiders"));
                return;
            }
            if (_neg != null && _neg.RiderId != r.Id) _neg = null;
            if (_neg != null)
            {
                NegotiationPanel.Build(Page(content), Game, _neg, Shell.Refresh, () => _neg = null);
                return;
            }

            var top = UiFactory.Rect("Top", content);
            top.anchorMin = new Vector2(0, 1);
            top.anchorMax = new Vector2(1, 1);
            top.pivot = new Vector2(0.5f, 1);
            top.offsetMin = new Vector2(0, -TopHeight);
            top.offsetMax = Vector2.zero;
            Header(top, world, team, r, riders);

            var tabs = Wp2Ui.BottomStrip(content, TabsHeight);
            Cards.BottomTabs(tabs, new[] { Loc.T("riders2.tab.attributes"), Loc.T("riders2.tab.traits"), Loc.T("riders2.tab.contract"), Loc.T("riders2.tab.history") },
                _tab, i =>
                {
                    _tab = i;
                    Ui.Disarm();
                    Shell.Refresh();
                });

            var mid = Wp2Ui.Above(content, TabsHeight + Ui.Grid.Gap, TopHeight + Ui.Grid.Gap);
            switch (_tab)
            {
                case 1: Traits(mid, world, team, r); break;
                case 2: ContractTab(mid, world, team, r); break;
                case 3: History(mid, world, team, r); break;
                default: Attributes(mid, world, team, r); break;
            }
        }

        private void Go(string riderId)
        {
            RiderId = riderId;
            Ui.Disarm();
            Shell.Refresh();
        }

        private static RectTransform Scroll(RectTransform area, string name)
        {
            var list = UiFactory.ScrollList(name, area);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            return list;
        }

        // ------------------------------------------------------------------ top block

        private void Header(RectTransform top, WorldState world, Team team, Rider r, List<Rider> riders)
        {
            var rider = r;
            var bg = UiFactory.Panel("Header", top, Theme.Card);
            UiFactory.Stretch(bg.rectTransform);
            bg.gameObject.AddComponent<RectMask2D>();

            // The number block in the team colour (no portraits: the race number is the face).
            // The header itself wears the hero band (team colour, chevrons) behind everything.
            var band = Cards.Hero(bg.transform, Theme.Accent, 0, 1);
            band.color = Color.Lerp(Theme.Card, Theme.Accent, 0.22f);
            var hero = UiFactory.Decor("Hero", bg.transform, Theme.Accent);
            hero.rectTransform.anchorMin = new Vector2(0, 0);
            hero.rectTransform.anchorMax = new Vector2(0, 1);
            hero.rectTransform.pivot = new Vector2(0, 0.5f);
            hero.rectTransform.sizeDelta = new Vector2(TopHeight, 0);
            hero.rectTransform.anchoredPosition = Vector2.zero;
            var big = Ui.Label(hero.transform, RiderFacts.Hero(r), Theme.TextDisplay, TextAnchor.MiddleCenter, Theme.Alpha(Color.white, 0.92f), FontStyle.BoldAndItalic);
            UiFactory.Stretch(big.rectTransform);
            big.rectTransform.offsetMin = new Vector2(12, 12);
            big.rectTransform.offsetMax = new Vector2(-12, -12);
            big.resizeTextForBestFit = true;
            big.resizeTextMinSize = Theme.TextTitle;
            big.resizeTextMaxSize = 96;
            UiFactory.Style(big, FontStyle.BoldAndItalic);

            // Name block: first name (and ‹ n/N › on the right), country + LAST NAME and the stars, then the figures.
            var info = UiFactory.Rect("Info", bg.transform);
            UiFactory.Stretch(info);
            info.offsetMin = new Vector2(TopHeight + 20, 12);
            info.offsetMax = new Vector2(-(StatusWidth + ActionsWidth + 2 * Ui.Grid.Gap + 8), -10);
            var first = Ui.Label(info, r.FirstName ?? "", Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(first.rectTransform, 0, 0.72f, 0.6f, 1);
            if (riders.Count > 1)
            {
                int idx = riders.IndexOf(r), n = riders.Count;
                string prev = riders[(idx + n - 1) % n].Id, next = riders[(idx + 1) % n].Id;
                var pager = UiFactory.Rect("Pager", info);
                pager.anchorMin = pager.anchorMax = new Vector2(1, 1);
                pager.pivot = new Vector2(1, 1);
                pager.sizeDelta = new Vector2(Ui.Grid.Hit * 2 + 56, Ui.Grid.Hit);
                pager.anchoredPosition = new Vector2(0, 6);
                Carousel.Arrow(pager, false, () => Go(prev));
                Carousel.Arrow(pager, true, () => Go(next));
                var count = Ui.Label(pager, (idx + 1) + "/" + n, Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);
                UiFactory.Stretch(count.rectTransform);
            }
            float lastLeft = 0;
            if (!string.IsNullOrEmpty(r.Nationality))
            {
                var chip = Ui.Pill(info, r.Nationality.ToUpperInvariant(), Theme.PanelLight, Theme.Text);
                chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = new Vector2(0, 0.55f);
                chip.rectTransform.pivot = new Vector2(0, 0.5f);
                chip.rectTransform.anchoredPosition = Vector2.zero;
                lastLeft = 72;
            }
            var last = Ui.Label(info, Theme.Caps(r.LastName ?? ""), Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
            UiFactory.Place(last.rectTransform, 0, 0.38f, 0.66f, 0.72f);
            last.rectTransform.offsetMin = new Vector2(lastLeft, 0);
            Ui.Fit(last, Theme.TextTitle);
            UiFactory.Style(last, FontStyle.BoldAndItalic);
            var stars = UiFactory.Rect("Stars", info);
            UiFactory.Place(stars, 0.66f, 0.38f, 1, 0.72f);
            Cards.Stars(stars, Cards.StarsFor(r.Attributes.Overall));
            string line1 = Loc.T("rider.age", r.AgeOn(world.CurrentDate)) + "  ·  " + Loc.T("riders2.rating") + " " + Fmt.Rating(r.Attributes.Overall);
            string line2 = Loc.T("riders2.morale") + " " + RiderFacts.Morale(r.Morale) +
                           (r.InjuryRoundsOut > 0 ? "  ·  " + Theme.BadText(Loc.T("home.injured", r.InjuryRoundsOut)) : "");
            var lines = Ui.Label(info, line1 + "\n" + line2, Theme.TextCaption, TextAnchor.MiddleLeft, Theme.Text);
            UiFactory.Place(lines.rectTransform, 0, 0, 1, 0.38f);
            lines.supportRichText = true;
            Ui.Fit(lines, Theme.TextCaption);

            // Status in the team: a faint panel with the caption, the three choices and what they do.
            var status = UiFactory.Panel("Status", bg.transform, Theme.Panel);
            status.rectTransform.anchorMin = new Vector2(1, 0);
            status.rectTransform.anchorMax = new Vector2(1, 1);
            status.rectTransform.offsetMin = new Vector2(-(ActionsWidth + 2 * Ui.Grid.Gap + StatusWidth), 12);
            status.rectTransform.offsetMax = new Vector2(-(ActionsWidth + 2 * Ui.Grid.Gap), -12);
            var cap = Ui.Label(status.transform, Theme.Caps(Loc.T("riders2.status")), Theme.TextCaption, TextAnchor.MiddleLeft, Theme.TextDim, FontStyle.Bold);
            UiFactory.Place(cap.rectTransform, 0, 0.72f, 1, 1);
            cap.rectTransform.offsetMin = new Vector2(16, 0);
            var holder = UiFactory.Rect("Segments", status.transform);
            UiFactory.Place(holder, 0, 0.3f, 1, 0.74f);
            holder.offsetMin = new Vector2(4, 0);
            holder.offsetMax = new Vector2(-4, 0);
            Wp2Ui.ListIn(holder, 0);
            var labels = new string[Order.Length];
            for (int i = 0; i < Order.Length; i++) labels[i] = RiderFacts.Status(Order[i]);
            Ui.Segmented(holder, labels, Array.IndexOf(Order, RiderStatusRules.Of(team, r.Id)), i =>
            {
                if (RiderStatusRules.Set(world, team, rider.Id, Order[i]))
                    Shell.Toast(Loc.T("riders2.status.set", rider.FullName, RiderFacts.Status(RiderStatusRules.Of(team, rider.Id))));
                Shell.Refresh();
            });
            var hint = Ui.Label(status.transform, Loc.T("riders2.status.hint"), Theme.TextMin, TextAnchor.MiddleCenter, Theme.TextDim);
            UiFactory.Place(hint.rectTransform, 0, 0, 1, 0.28f);
            hint.rectTransform.offsetMin = new Vector2(12, 0);
            hint.rectTransform.offsetMax = new Vector2(-12, 0);

            // Actions: renew (or the contract already signed) over release.
            var actions = UiFactory.Rect("Actions", bg.transform);
            actions.anchorMin = new Vector2(1, 0);
            actions.anchorMax = new Vector2(1, 1);
            actions.offsetMin = new Vector2(-(ActionsWidth + Ui.Grid.Gap), 12);
            actions.offsetMax = new Vector2(-Ui.Grid.Gap, -12);
            var rows = Ui.Rows(actions, 1, 1);
            if (r.FutureContract == null)
            {
                Action renew = () =>
                {
                    _neg = new NegotiationState { RiderId = rider.Id };
                    Ui.Disarm();
                    Shell.Refresh();
                };
                if (RiderFacts.Expiring(world, r)) Ui.PrimaryCTA(rows[0], Loc.T("riders2.renew"), renew);
                else Ui.SecondaryAccent(rows[0], Loc.T("riders2.renew"), renew);
            }
            else
            {
                var done = Ui.Label(rows[0], Theme.Caps(Loc.T("terms.next", r.FutureContract.EndSeason)), Theme.TextCaption, TextAnchor.MiddleCenter, Theme.Good, FontStyle.Bold);
                UiFactory.Stretch(done.rectTransform);
                Ui.Fit(done, Theme.TextCaption);
            }
            string cost = MathX.FormatMoney(Contracts.TerminationCost(world, Game.Db, r));
            Ui.Danger(rows[1], Loc.T("team.release", cost), Loc.T("team.releaseConfirm", cost), "rider.release." + r.Id, () =>
            {
                Contracts.Terminate(world, Game.Db, team, rider);
                RiderId = null;
                _neg = null;
                Shell.Back();
            }, null);
        }

        // ------------------------------------------------------------------ tabs

        /// <summary>
        /// Every attribute as a cell (three columns): first the rating over the potential, the morale and the
        /// reputation, then the riding attributes. The core keeps a ceiling only for the overall rating.
        /// </summary>
        private void Attributes(RectTransform area, WorldState world, Team team, Rider r)
        {
            var list = Scroll(area, "Attributes");
            var cells = new List<KeyValuePair<string, double[]>>
            {
                new KeyValuePair<string, double[]>(Loc.T("riders2.ratingPotential"), new[] { r.Attributes.Overall, Fmt.Estimate(world, team.Id, r, "Potential") }),
                new KeyValuePair<string, double[]>(Loc.T("riders2.morale"), new[] { r.Morale, 0.0 }),
                new KeyValuePair<string, double[]>(Loc.T("riders2.reputation"), new[] { r.Reputation, 0.0 })
            };
            foreach (var n in RiderAttributes.Names)
                cells.Add(new KeyValuePair<string, double[]>(Loc.Or("ux.attr." + n, Loc.T("attr." + n)), new[] { Fmt.Estimate(world, team.Id, r, n), 0.0 }));
            for (int i = 0; i < cells.Count; i += 3)
            {
                var row = UiFactory.Row(list, 76);
                var cols = Ui.Columns12(row, 4, 4, 4);
                for (int k = 0; k < 3 && i + k < cells.Count; k++)
                    Cards.Attribute(cols[k], cells[i + k].Key, cells[i + k].Value[0], cells[i + k].Value[1], 100);
            }
        }

        /// <summary>The traits (injury, riding styles, or the standout attribute) with what each does, two per row.</summary>
        private void Traits(RectTransform area, WorldState world, Team team, Rider r)
        {
            var list = Scroll(area, "Traits");
            var items = RidersPage.TraitItems(world, team, r);
            for (int i = 0; i < items.Count; i += 2)
            {
                var row = UiFactory.Row(list, 112);
                var cols = Ui.Columns12(row, 6, 6);
                for (int k = 0; k < 2 && i + k < items.Count; k++)
                {
                    var it = items[i + k];
                    var cell = UiFactory.Panel("Cell", cols[k], Theme.Card);
                    UiFactory.Stretch(cell.rectTransform);
                    TeamCards.Highlight(cell.transform, 0, 1, it.Key[0], it.Key[1], it.Key[2], it.Value);
                }
            }
        }

        /// <summary>
        /// Left: every term as label / value lines (bonus tiers, image rights) and the terms sentence. Right: the end
        /// season and salary in big figures, whether it ends now or what is signed next, and the team option.
        /// </summary>
        private void ContractTab(RectTransform area, WorldState world, Team team, Rider r)
        {
            var rider = r;
            var c = r.Contract;
            var cols = Ui.Columns12(area, 7, 5);
            var list = Scroll(cols[0], "Contract");
            RidersPage.ContractLines(list, world, Game.Db, r, l => MoreTerms(l, c));

            var side = Wp2Ui.ListIn(Ui.Tile(cols[1], Loc.T("rider.contractCap")), 8);
            if (c == null)
            {
                Ui.Empty(side, null, Loc.T("riders2.contract.none"));
                return;
            }
            Ui.Kpis(side, Ui.Kpi(c.EndSeason.ToString(), Loc.T("rider.until")), Ui.Kpi(MathX.FormatMoney(c.SalaryPerSeason), Loc.T("staff.salary")));
            if (RiderFacts.Expiring(world, r)) Ui.Alert(side, Loc.T("riders2.contract.expiring"));
            else if (r.FutureContract != null)
                UiFactory.Paragraph(side, Theme.GoodText(Loc.T("rider.nextContract") + ": " +
                    Loc.T("rider.untilLine", r.FutureContract.EndSeason, MathX.FormatMoney(r.FutureContract.SalaryPerSeason))), Theme.TextBody);
            var option = c.Clause(ClauseType.TeamOption);
            if (option != null && !option.Exercised && c.EndSeason == world.Season)
                Ui.Secondary(side, Loc.T("team.option"), () =>
                {
                    bool ok = ContractService.ExerciseOption(world, Game.Db, team, rider);
                    Shell.Toast(ok ? Loc.T("team.optionDone", rider.FullName) : Loc.T("team.optionNoSeat"), ok);
                    Shell.Refresh();
                });
        }

        /// <summary>The terms the contract sheet leaves out: signing bonus, bonus tiers past the win, pole bonus and image rights.</summary>
        private static void MoreTerms(Transform list, Contract c)
        {
            if (c == null) return;
            if (c.SigningBonus > 0) Ui.Line(list, Loc.T("riders2.contract.signing"), MathX.FormatMoney(c.SigningBonus));
            foreach (var b in c.RaceBonuses)
                if (b.MaxPosition > 1 && b.Amount > 0) Ui.Line(list, Loc.T("riders2.contract.raceTier", b.MaxPosition), MathX.FormatMoney(b.Amount));
            foreach (var b in c.ChampionshipBonuses)
                if (b.Amount > 0) Ui.Line(list, Loc.T("riders2.contract.champTier", b.MaxPosition), MathX.FormatMoney(b.Amount));
            if (c.PoleBonus > 0) Ui.Line(list, Loc.T("riders2.contract.pole"), MathX.FormatMoney(c.PoleBonus));
            Ui.Line(list, Loc.T("riders2.contract.image"),
                c.ImageRights ? Loc.T("riders2.contract.imageValue", (c.ImageRightsShare * 100).ToString("0")) : Theme.Dim(Loc.T("rider.none")));
        }

        /// <summary>
        /// Season by season, newest first: year, team (and category), races, points, podiums, wins; the season in
        /// progress is marked (and listed before the first race), a total closes the table, then the real-world
        /// history before the game's start when there is one.
        /// </summary>
        private void History(RectTransform area, WorldState world, Team team, Rider r)
        {
            var list = Scroll(area, "History");
            var w = new[] { 1.1f, 3.6f, 1.2f, 1.2f, 1.1f, 1.2f };
            Ui.ListCaptions(list, w, new[]
            {
                Loc.T("riders2.col.year"), Loc.T("col.team"), Loc.T("riders2.races"), Loc.T("riders2.points"), Loc.T("riders2.podiums"), Loc.T("riders2.wins")
            });
            var recs = r.CareerHistory.ToList();
            if (!recs.Exists(x => x.Season == world.Season))
                recs.Add(new SeasonRecord { Season = world.Season, CategoryId = r.CategoryId ?? team.CategoryId, TeamId = team.Id, TeamName = team.Name });
            int starts = 0, podiums = 0, wins = 0;
            double points = 0;
            foreach (var h in recs.OrderByDescending(x => x.Season))
            {
                starts += h.Starts;
                podiums += h.Podiums;
                wins += h.Wins;
                points += h.Points;
                var t = h.TeamName;
                if (string.IsNullOrEmpty(t) && h.TeamId != null)
                {
                    var tm = world.GetTeam(h.TeamId);
                    if (tm != null) t = tm.Name;
                }
                var cat = h.CategoryId != null ? Game.Db.GetCategory(h.CategoryId) : null;
                string sub = (cat != null ? cat.ShortName : "") + (h.Season == world.Season ? (cat != null ? " · " : "") + Loc.T("riders2.history.current") : "");
                UiFactory.TableRow(list, 52, w, new[]
                {
                    h.Season.ToString(),
                    (string.IsNullOrEmpty(t) ? "-" : t) + (sub.Length > 0 ? "  " + Theme.Dim(sub) : ""),
                    h.Starts.ToString(), h.Points.ToString("0"), h.Podiums.ToString(), h.Wins.ToString()
                });
            }
            UiFactory.TableRow(list, 52, w, new[]
            {
                "<b>" + Loc.T("riders2.total") + "</b>", "", "<b>" + starts + "</b>", "<b>" + points.ToString("0") + "</b>", "<b>" + podiums + "</b>", "<b>" + wins + "</b>"
            }, false, Theme.Alpha(Theme.Ink, 0.35f));
            if (!string.IsNullOrEmpty(r.RealHistory)) UiFactory.Paragraph(list, Theme.Dim(r.RealHistory), Theme.TextCaption);
        }
    }
}
