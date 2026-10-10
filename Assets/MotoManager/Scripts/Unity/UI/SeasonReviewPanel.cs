using System;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Season;
using UnityEngine;

namespace MotoManager.App.UI
{
    /// <summary>
    /// End-of-season summary: how the team did against its objective, the riders, the money, what the owners think
    /// (and why), which teams would like to hire the manager, and the champions.
    /// </summary>
    public class SeasonReviewPanel : MonoBehaviour
    {
        private Action _done;

        public static void Show(SeasonSummary summary, GameController game, Action done)
        {
            var canvas = UiFactory.CreateCanvas("SeasonReview", 30);
            var p = canvas.gameObject.AddComponent<SeasonReviewPanel>();
            p._done = done;
            p.Build((RectTransform)canvas.transform, summary, game);
        }

        private static string MoodColor(BoardMood m)
        {
            return m == BoardMood.Delighted || m == BoardMood.Satisfied ? Theme.GoodHex : m == BoardMood.Concerned ? Theme.WarnHex : Theme.BadHex;
        }

        private void Build(RectTransform root, SeasonSummary summary, GameController game)
        {
            var bg = UiFactory.Panel("Background", root, Theme.Background);
            UiFactory.Stretch(bg.rectTransform);
            var review = summary.PlayerReview;
            var world = game.World;
            var team = review != null ? world.GetTeam(review.TeamId) : null;
            var title = UiFactory.Label("Title", root, "<b>" + Loc.T("review.title", summary.Season) + "</b>" + (team != null ? "   " + Theme.Dim(team.Name) : ""), Theme.FontLarge - 2, TextAnchor.MiddleLeft);
            title.supportRichText = true;
            UiFactory.Place(title.rectTransform, 0.03f, 0.89f, 0.97f, 0.98f);

            var list = UiFactory.ScrollList("Review", root);
            UiFactory.Place((RectTransform)list.parent.parent, 0.02f, 0.11f, 0.98f, 0.88f);
            RectTransform left, right;
            Ui.Columns(list, out left, out right);

            if (review != null)
            {
                var cat = game.Db.GetCategory(review.CategoryId);
                var season = Ui.Card(left, Loc.T("review.yourSeason"), cat != null ? cat.Name : null);
                Ui.Kpis(season,
                    Ui.Kpi(review.TeamPosition > 0 ? "P" + review.TeamPosition + Theme.Dim("/" + review.TeamsInCategory) : "-", Loc.T("review.teamPosition")),
                    Ui.Kpi(review.ObjectiveMet ? Theme.GoodText(Loc.T("objective.met")) : Theme.BadText(Loc.T("objective.missed")),
                        Loc.T("objective." + review.Objective) + " (" + Loc.T("review.target", review.TargetPosition) + ")"),
                    Ui.Kpi(Theme.Money(review.NetCash), Loc.T("review.cash")));
                foreach (var r in review.Riders)
                    Ui.Item(season, r.Name, Loc.T("home.riderLine", r.Points.ToString("0"), r.Wins, r.Podiums), "<size=" + (Theme.FontLarge - 6) + ">P" + r.Position + "</size>");
                Ui.Line(season, Loc.T("review.budget"), Core.Common.MathX.FormatMoney(review.BudgetSpent) + Theme.Dim(" / " + Core.Common.MathX.FormatMoney(review.BudgetAllocated)));

                var org = team != null ? world.OrganisationOf(team) : null;
                var board = Ui.Card(right, Loc.T("review.board", org != null ? org.Name : ""));
                UiFactory.Paragraph(board, "<size=" + (Theme.FontLarge - 4) + "><b><color=" + MoodColor(review.Mood) + ">" + Loc.T("review.mood." + review.Mood) + "</color></b></size>", Theme.FontNormal);
                Ui.Bar(board, Loc.T("review.confidence"), review.Confidence / 100.0, review.Confidence + "%",
                    review.Confidence >= 55 ? Theme.Good : review.Confidence >= 35 ? Theme.Warning : Theme.Bad, 38);
                foreach (var reason in review.Reasons)
                {
                    bool good = reason == ReviewReason.ObjectiveMet || reason == ReviewReason.WellAboveTarget || reason == ReviewReason.Title ||
                                reason == ReviewReason.RaceWins || reason == ReviewReason.Profit || reason == ReviewReason.WithinBudget;
                    UiFactory.Paragraph(board, (good ? Theme.GoodText("+") : Theme.BadText("–")) + "  " + Loc.T("review.reason." + reason), Theme.FontSmall);
                }

                var offers = Ui.Card(right, Loc.T("review.offers"));
                if (review.Offers.Count == 0) Ui.Note(offers, Loc.T("review.noOffers"));
                for (int i = 0; i < review.Offers.Count && i < 3; i++)
                {
                    var o = review.Offers[i];
                    var t = world.GetTeam(o.TeamId);
                    var c = game.Db.GetCategory(o.CategoryId);
                    Ui.Item(offers, t != null ? t.Name : o.TeamId, (c != null ? c.ShortName + " · " : "") + Loc.T("objective." + o.Objective),
                        Core.Common.MathX.FormatMoney(o.SalaryPerSeason) + Theme.Dim("/" + Loc.T("unit.season")));
                }
                if (review.Offers.Count > 0) Ui.Note(offers, Loc.T("review.offersNote"));
            }

            var champs = Ui.Card(left, Loc.T("review.champions"));
            foreach (var c in summary.Champions)
            {
                var cat = game.Db.GetCategory(c.CategoryId);
                Ui.Line(champs, cat != null ? cat.Name : c.CategoryId, "<b>" + c.RiderName + "</b>");
            }
            if (summary.Champions.Count == 0) Ui.Note(champs, "-");

            var go = UiFactory.Button("Continue", root, Loc.T("review.continue"), Close, Theme.Accent, Theme.FontNormal);
            UiFactory.Place((RectTransform)go.transform, 0.7f, 0.02f, 0.97f, 0.1f, 4);
        }

        private void Close()
        {
            Destroy(gameObject);
            if (_done != null) _done();
        }
    }
}
