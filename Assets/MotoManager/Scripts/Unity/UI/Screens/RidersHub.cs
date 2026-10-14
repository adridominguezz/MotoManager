using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI.Screens
{
    /// <summary>The figures of a rider's current season and the words for his morale and status (riders hub and profile).</summary>
    internal static class RiderFacts
    {
        public struct Season
        {
            public int Races, Wins, Podiums, Position;
            public double Points;
        }

        /// <summary>The team's riders in seat order.</summary>
        public static List<Rider> Riders(WorldState world, Team team)
        {
            var list = new List<Rider>();
            if (team == null) return list;
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r != null) list.Add(r);
            }
            return list;
        }

        /// <summary>
        /// This season so far: races started from the career records, points, wins and podiums from the standings
        /// (the records when the championship has none) and the championship position (0 = not classified).
        /// </summary>
        public static Season Current(WorldState world, Team team, Rider r)
        {
            var s = new Season();
            foreach (var rec in r.CareerHistory)
            {
                if (rec.Season != world.Season) continue;
                s.Races += rec.Starts;
                s.Wins += rec.Wins;
                s.Podiums += rec.Podiums;
                s.Points += rec.Points;
            }
            ChampionshipState champ;
            string cat = r.CategoryId ?? (team != null ? team.CategoryId : null);
            if (cat != null && world.Championships.TryGetValue(cat, out champ) && champ != null)
            {
                StandingEntry st;
                if (champ.Riders.TryGetValue(r.Id, out st))
                {
                    s.Points = st.Points;
                    s.Wins = st.Wins;
                    s.Podiums = st.Podiums;
                }
                s.Position = champ.RiderPosition(r.Id);
            }
            return s;
        }

        /// <summary>Morale as a word and the figure, in its level colour ("Alta 72").</summary>
        public static string Morale(double m)
        {
            string key = m >= 75 ? "high" : m >= 55 ? "good" : m >= 40 ? "low" : "veryLow";
            return "<color=#" + ColorUtility.ToHtmlStringRGB(Ui.RatingColor(m)) + ">" + Loc.T("riders2.morale." + key) + " " + m.ToString("0") + "</color>";
        }

        public static string Status(RiderStatus s)
        {
            return Loc.T("riders2.status." + s);
        }

        /// <summary>The contract ends this season and nothing is signed for the next one.</summary>
        public static bool Expiring(WorldState world, Rider r)
        {
            return r.Contract != null && r.Contract.EndSeason <= world.Season && r.FutureContract == null;
        }

        /// <summary>The big figure of the hero band: the race number, or the three-letter code without one.</summary>
        public static string Hero(Rider r)
        {
            return r.Number > 0 ? r.Number.ToString() : r.Code;
        }

        /// <summary>
        /// Tags over the top-left of a hero band: the status when there is a pecking order, and the injury.
        /// </summary>
        public static void Tags(Transform hero, Team team, Rider r)
        {
            if (hero == null) return;
            var status = RiderStatusRules.Of(team, r.Id);
            if (status == RiderStatus.Equal && r.InjuryRoundsOut <= 0) return;
            var row = UiFactory.Rect("Tags", hero);
            row.anchorMin = new Vector2(0, 1);
            row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0, 1);
            row.offsetMin = new Vector2(14, -46);
            row.offsetMax = new Vector2(-14, -14);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childAlignment = TextAnchor.UpperLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            if (status != RiderStatus.Equal) Ui.Pill(row, Status(status), Theme.Alpha(Theme.Ink, 0.6f), Theme.Text);
            if (r.InjuryRoundsOut > 0) Ui.Pill(row, Loc.T("riders2.injured", r.InjuryRoundsOut), Theme.Bad);
        }
    }

    /// <summary>
    /// The riders hub: one person card per seat (normally two side by side): the race number in the hero band, the
    /// name, country, age and stars, then the rating, morale and this season's figures, and DETALLES to the profile.
    /// A free seat is a card of its own with the way to the market (the page's one primary). Under the cards the wide
    /// BUSCAR PILOTOS opens the market (no longer on the rail), beside the bids for our riders when there are any.
    /// </summary>
    public class RidersHubPage : ScreenBase
    {
        public override string Title { get { return Loc.T("nav.riders"); } }
        private bool _bids;

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null)
            {
                Ui.Empty(Page(content), UiIcons.Person, Loc.T("home.noTeam"));
                return;
            }
            int bids = world.TransferOffers.FindAll(o => o.FromTeamId == team.Id).Count;
            if (_bids && bids == 0) _bids = false;
            if (_bids)
            {
                NegotiationPanel.Bids(Page(content), Game, Shell.Refresh, () => { _bids = false; Shell.Refresh(); });
                return;
            }

            // Bottom: the way to the market, and the bids beside it.
            var bottom = Wp2Ui.BottomStrip(content, Ui.Grid.Button);
            Action market = () => Shell.Show<MarketScreen>();
            if (bids > 0)
            {
                var bc = Ui.Columns12(bottom, 8, 4);
                Cards.Find(bc[0], Loc.T("riders2.find"), market);
                Wp2Ui.Chevron(Ui.SecondaryAccent(bc[1], Loc.T("rider.bids", bids), () => { _bids = true; Shell.Refresh(); }));
            }
            else Cards.Find(bottom, Loc.T("riders2.find"), market);

            // The seats: riders, then the free ones (at most four cards).
            var riders = RiderFacts.Riders(world, team);
            int free = Math.Max(0, ContractService.SeatsFreeNow(world, Game.Db, team));
            int seats = Math.Max(1, Math.Min(4, riders.Count + free));
            var area = Wp2Ui.Above(content, Ui.Grid.Button + Ui.Grid.Gap);
            RectTransform[] cols;
            if (seats == 1) cols = new[] { Ui.Columns12(area, 3, 6, 3)[1] };
            else if (seats == 2) cols = Ui.Columns12(area, 6, 6);
            else if (seats == 3) cols = Ui.Columns12(area, 4, 4, 4);
            else cols = Ui.Columns12(area, 3, 3, 3, 3);
            bool primary = true;
            for (int i = 0; i < seats; i++)
            {
                if (i < riders.Count) RiderCard(cols[i], world, team, riders[i], seats <= 2);
                else
                {
                    FreeSeat(cols[i], primary);
                    primary = false;
                }
            }
        }

        /// <summary>
        /// One rider: the person card and its rows. Wide cards (one or two seats) take eight rows in two columns;
        /// narrow ones the four that matter most.
        /// </summary>
        private void RiderCard(RectTransform area, WorldState world, Team team, Rider r, bool wide)
        {
            var rider = r;
            var body = Cards.Person(area, Theme.Accent, RiderFacts.Hero(r), r.FirstName, r.LastName, r.Nationality,
                Cards.StarsFor(r.Attributes.Overall), Loc.T("rider.age", r.AgeOn(world.CurrentDate)), Loc.T("riders2.details"), () =>
                {
                    RiderProfilePage.RiderId = rider.Id;
                    Shell.Show<RiderProfilePage>();
                });
            RiderFacts.Tags(body.parent.Find("Hero"), team, r);

            var s = RiderFacts.Current(world, team, r);
            string contract = r.Contract != null ? r.Contract.EndSeason.ToString() : "-";
            if (RiderFacts.Expiring(world, r)) contract = Theme.WarnText(contract);
            if (!wide)
            {
                Cards.Rows(body,
                    new[] { Loc.T("riders2.morale"), Loc.T("riders2.races"), Loc.T("riders2.winsPodiums"), Loc.T("riders2.champ") },
                    new[] { RiderFacts.Morale(r.Morale), s.Races.ToString(), s.Wins + " / " + s.Podiums, Theme.Pos(s.Position) + " · " + s.Points.ToString("0") });
                return;
            }
            var left = UiFactory.Rect("Left", body);
            UiFactory.Place(left, 0, 0, 0.5f, 1);
            left.offsetMax = new Vector2(-6, 0);
            var right = UiFactory.Rect("Right", body);
            UiFactory.Place(right, 0.5f, 0, 1, 1);
            right.offsetMin = new Vector2(6, 0);
            Cards.Rows(left,
                new[] { Loc.T("riders2.rating"), Loc.T("riders2.morale"), Loc.T("riders2.races"), Loc.T("riders2.points") },
                new[] { Fmt.Rating(r.Attributes.Overall), RiderFacts.Morale(r.Morale), s.Races.ToString(), s.Points.ToString("0") });
            Cards.Rows(right,
                new[] { Loc.T("riders2.wins"), Loc.T("riders2.podiums"), Loc.T("riders2.champ"), Loc.T("riders2.contract") },
                new[] { s.Wins.ToString(), s.Podiums.ToString(), Theme.Pos(s.Position), contract });
        }

        /// <summary>An empty seat, card-sized: the warning and the way to the market.</summary>
        private void FreeSeat(RectTransform area, bool primary)
        {
            var card = UiFactory.Panel("FreeSeat", area, Theme.Card);
            UiFactory.Stretch(card.rectTransform);
            TeamCards.Edge(card.transform, Theme.Warning, true, 6);
            var band = UiFactory.Decor("Hero", card.transform, Theme.Alpha(Theme.Warning, 0.06f));
            UiFactory.Place(band.rectTransform, 0, 1 - Cards.HeroShare, 1, 1);
            var img = UiIcons.Icon(band.transform, UiIcons.Person, Theme.Alpha(Theme.TextDim, 0.35f));
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            img.rectTransform.sizeDelta = new Vector2(120, 120);
            var t = Ui.Label(card.transform, Theme.Caps(Loc.T("rider.freeSeatShort")), Theme.TextTitle, TextAnchor.MiddleCenter, Theme.Warning, FontStyle.BoldAndItalic);
            UiFactory.Place(t.rectTransform, 0, 0.42f, 1, 0.58f);
            var n = Ui.Label(card.transform, Loc.T("rider.freeSeatInfo"), Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim);
            UiFactory.Place(n.rectTransform, 0, 0.24f, 1, 0.42f);
            n.rectTransform.offsetMin = new Vector2(24, 0);
            n.rectTransform.offsetMax = new Vector2(-24, 0);
            var cta = Wp2Ui.BottomStrip(card.rectTransform, Ui.Grid.Cta, 12);
            cta.offsetMin = new Vector2(16, cta.offsetMin.y);
            cta.offsetMax = new Vector2(-16, cta.offsetMax.y);
            Action go = () => Shell.Show<MarketScreen>();
            Wp2Ui.Chevron(primary ? Ui.PrimaryCTA(cta, Loc.T("ux.riders.toMarket"), go) : Ui.SecondaryAccent(cta, Loc.T("ux.riders.toMarket"), go));
        }
    }
}
