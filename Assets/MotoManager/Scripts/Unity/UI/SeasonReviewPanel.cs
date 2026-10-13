using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Season;
using UnityEngine;

namespace MotoManager.App.UI
{
    /// <summary>
    /// End-of-season summary (UX spec §3.27), one screen in three columns: TU TEMPORADA (team position, objective,
    /// cash; a row per rider that opens the rider's figures; the budget), LA JUNTA (the mood, the confidence and the
    /// top three reasons, the rest behind "i") and CAMPEONES (each category's champion and a link to the manager offers),
    /// with CONTINUAR as the primary at the bottom right.
    /// </summary>
    public class SeasonReviewPanel : MonoBehaviour
    {
        private Action _done;
        private Transform _previousHost;

        public static void Show(SeasonSummary summary, GameController game, Action done)
        {
            var canvas = UiFactory.CreateCanvas("SeasonReview", 30);
            var p = canvas.gameObject.AddComponent<SeasonReviewPanel>();
            p._done = done;
            p.Build(UiFactory.SafeRoot(canvas, Theme.Background), summary, game);
        }

        private static Color MoodColor(BoardMood m)
        {
            return m == BoardMood.Delighted || m == BoardMood.Satisfied ? Theme.Good : m == BoardMood.Concerned ? Theme.Warning : Theme.Bad;
        }

        private static bool Good(ReviewReason reason)
        {
            return reason == ReviewReason.ObjectiveMet || reason == ReviewReason.WellAboveTarget || reason == ReviewReason.Title ||
                   reason == ReviewReason.RaceWins || reason == ReviewReason.Profit || reason == ReviewReason.WithinBudget;
        }

        private void Build(RectTransform root, SeasonSummary summary, GameController game)
        {
            _previousHost = Ui.SheetHost;
            Ui.SheetHost = root;
            var bg = UiFactory.Panel("Background", root, Theme.Background);
            UiFactory.Stretch(bg.rectTransform);
            bg.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            UiFactory.Glow(bg.transform, 0.32f, 1f, 1500, 0.16f);
            var review = summary.PlayerReview;
            var world = game.World;
            var team = review != null ? world.GetTeam(review.TeamId) : null;

            var page = UiFactory.Rect("Page", root);
            UiFactory.Stretch(page);
            page.offsetMin = new Vector2(Ui.Grid.Gutter, Ui.Grid.Gutter);
            page.offsetMax = new Vector2(-Ui.Grid.Gutter, -Ui.Grid.Gutter);

            // Top bar (72): title and team on the left.
            var bar = Wp4Ui.TopStrip(page, 72);
            var title = Ui.Label(bar, Theme.Caps(Loc.T("review.title", review != null ? review.Season : summary.Season)), Theme.TextTitle, TextAnchor.MiddleLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Place(title.rectTransform, 0, 0.3f, 1, 1);
            if (team != null)
            {
                var sub = Ui.Label(bar, Theme.Caps(team.Name), Theme.TextCaption, TextAnchor.UpperLeft, Theme.TextDim, FontStyle.Bold);
                UiFactory.Place(sub.rectTransform, 0, 0, 1, 0.32f);
            }

            var body = Wp4Ui.Below(page, 72 + Ui.Grid.Gap, Ui.Grid.Cta + Ui.Grid.Gap);
            var cols = Ui.Columns12(body, 4, 4, 4);
            if (review != null)
            {
                SeasonTile(cols[0], review, game);
                BoardTile(cols[1], review, world);
            }
            else
            {
                Ui.Empty(Wp4Ui.ListIn(Ui.Tile(cols[0], Loc.T("review.yourSeason"))), UiIcons.Career, Loc.T("ux.review.noTeam"));
                Ui.Empty(Wp4Ui.ListIn(Ui.Tile(cols[1], Loc.T("ux.review.board"))), null, "-");
            }
            ChampionsTile(cols[2], summary, review, game);

            var cta = Ui.Columns12(Wp4Ui.BottomStrip(page, Ui.Grid.Cta), 8, 4);
            Wp4Ui.Chevron(Ui.PrimaryCTA(cta[1], Loc.T("ux.review.continue"), Close));
        }

        private void SeasonTile(RectTransform area, PlayerSeasonReview review, GameController game)
        {
            var cat = game.Db.GetCategory(review.CategoryId);
            var body = Ui.Tile(area, Loc.T("review.yourSeason"), null, cat != null ? Theme.Caps(cat.ShortName) : null);
            var list = Wp4Ui.ListIn(body, 4);
            Ui.Kpis(list,
                Ui.Kpi(review.TeamPosition > 0 ? "P" + review.TeamPosition + Theme.Dim("/" + review.TeamsInCategory) : "-", Loc.T("ux.review.position")),
                Ui.Kpi(review.ObjectiveMet ? Theme.GoodText(Loc.T("ux.review.met")) : Theme.BadText(Loc.T("ux.review.missed")),
                    Loc.T("ux.review.target", review.TargetPosition)),
                Ui.Kpi(Theme.Money(review.NetCash), Loc.T("review.cash")));
            int shown = 0;
            foreach (var r in review.Riders)
            {
                if (shown++ >= 4) break;
                var rr = r;
                Ui.ListRow(list, new[] { 1f }, new[] { "<b>" + r.Name + "</b>   " + Theme.TimingText("P" + r.Position) }, shown, false, () => Ui.Sheet(rr.Name, l =>
                {
                    Ui.Line(l, Loc.T("ux.review.position"), "<b>P" + rr.Position + "</b>");
                    Ui.Line(l, Loc.T("col.points"), rr.Points.ToString("0"));
                    Ui.Line(l, Loc.T("col.wins"), rr.Wins.ToString());
                    Ui.Line(l, Loc.T("col.podiums"), rr.Podiums.ToString());
                }, null, null, 0.42f), 56);
            }
            Ui.Line(list, Loc.T("review.budget"), MathX.FormatMoney(review.BudgetSpent) + Theme.Dim(" / " + MathX.FormatMoney(review.BudgetAllocated)));
            var head = body.parent.Find("Head");
            if (head != null) Ui.InfoButton(head, Loc.T("review.yourSeason"),
                Loc.T("objective." + review.Objective) + " (" + Loc.T("review.target", review.TargetPosition) + ")",
                Loc.T("review.teamPosition") + ": " + (review.TeamPosition > 0 ? "P" + review.TeamPosition + " / " + review.TeamsInCategory : "-"));
        }

        private void BoardTile(RectTransform area, PlayerSeasonReview review, Core.World.WorldState world)
        {
            var team = world.GetTeam(review.TeamId);
            var org = team != null ? world.OrganisationOf(team) : null;
            var all = new List<string>();
            foreach (var reason in review.Reasons) all.Add((Good(reason) ? Theme.GoodText("+") : Theme.BadText("−")) + "  " + Loc.T("review.reason." + reason));
            var body = Ui.Tile(area, Loc.T("ux.review.board"), null, org != null ? Theme.Caps(org.Name) : null, 0, null,
                all.Count > 3 ? Wp4Ui.Info(Loc.T("review.board", org != null ? org.Name : ""), all.ToArray()) : null);
            var list = Wp4Ui.ListIn(body, 6);
            var mood = Ui.Label(UiFactory.Row(list, 64), Theme.Caps(Loc.T("review.mood." + review.Mood)), Theme.TextTitle, TextAnchor.MiddleLeft, MoodColor(review.Mood), FontStyle.BoldAndItalic);
            UiFactory.Stretch(mood.rectTransform);
            mood.rectTransform.offsetMin = new Vector2(4, 0);
            Ui.Bar(list, Loc.T("review.confidence"), review.Confidence / 100.0, "<b>" + review.Confidence + "%</b>",
                review.Confidence >= 55 ? Theme.Good : review.Confidence >= 35 ? Theme.Warning : Theme.Bad, 44);
            for (int i = 0; i < all.Count && i < 3; i++)
            {
                var t = Ui.Label(UiFactory.Row(list, 52), all[i], Theme.TextBody, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Normal);
                UiFactory.Stretch(t.rectTransform);
                t.rectTransform.offsetMin = new Vector2(4, 0);
            }
        }

        private void ChampionsTile(RectTransform area, SeasonSummary summary, PlayerSeasonReview review, GameController game)
        {
            var world = game.World;
            var body = Ui.Tile(area, Loc.T("review.champions"));
            bool offers = review != null;
            var list = Wp4Ui.ScrollIn(Wp4Ui.Below(body, 0, offers ? Ui.Grid.Hit + 4 : 0), 2);
            foreach (var c in summary.Champions)
            {
                var cat = game.Db.GetCategory(c.CategoryId);
                Ui.Line(list, cat != null ? cat.ShortName : c.CategoryId, "<b>" + c.RiderName + "</b>");
            }
            if (summary.Champions.Count == 0) Ui.Empty(list, UiIcons.Trophy, "-");
            if (!offers) return;
            Ui.Ghost(Wp4Ui.BottomStrip(body, Ui.Grid.Hit), Loc.T("ux.review.offers", review.Offers.Count), () => Ui.Sheet(Loc.T("review.offers"), l =>
            {
                if (review.Offers.Count == 0) Ui.Empty(l, UiIcons.Career, Loc.T("review.noOffers"));
                foreach (var o in review.Offers)
                {
                    var t = world.GetTeam(o.TeamId);
                    var c = game.Db.GetCategory(o.CategoryId);
                    Ui.Item(l, t != null ? t.Name : o.TeamId, (c != null ? c.ShortName + " · " : "") + Loc.T("objective." + o.Objective),
                        MathX.FormatMoney(o.SalaryPerSeason) + Theme.Dim("/" + Loc.T("unit.season")));
                }
                if (review.Offers.Count > 0) UiFactory.Paragraph(l, Theme.Dim(Loc.T("review.offersNote")), Theme.TextBody);
            }, null, null, 0.56f));
        }

        private void Close()
        {
            Ui.CloseSheet();
            Ui.SheetHost = _previousHost;
            Destroy(gameObject);
            if (_done != null) _done();
        }
    }
}
