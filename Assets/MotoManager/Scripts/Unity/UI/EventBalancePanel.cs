using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>One concept of the event balance: what moved this round and the season so far (P&amp;L).</summary>
    public class BalanceRow
    {
        public LedgerKind Kind;
        public double Round, Year;
    }

    /// <summary>
    /// What the closing screens of an event need, recorded around the event from real data only: the championship
    /// tables before it (positions and points, to show the moves) and the team's ledger totals per kind before and
    /// after it (<see cref="Accounts.TotalsFor"/>, the same totals as the finance page). "This round" is the difference
    /// of the totals between the start of the event and its end; "year to date" is the season total after it.
    /// Capture/Finish only read the world, so they can run on the worker thread.
    /// </summary>
    public class EventRecap
    {
        public string CategoryId, CategoryName, EventName;
        public int RoundNumber, RoundCount;
        internal List<WeekendStep> Steps = new List<WeekendStep>();
        public readonly Dictionary<string, int> RiderPosBefore = new Dictionary<string, int>();
        public readonly Dictionary<string, double> RiderPtsBefore = new Dictionary<string, double>();
        public readonly Dictionary<string, int> TeamPosBefore = new Dictionary<string, int>();
        public readonly Dictionary<string, double> TeamPtsBefore = new Dictionary<string, double>();
        public string OrgId, TeamId;
        public int Season;
        public double CashBefore, CashAfter;
        public readonly List<BalanceRow> Rows = new List<BalanceRow>();
        public double RoundTotal, YearTotal;
        public bool Finished;
        private readonly Dictionary<LedgerKind, double> _pnlBefore = new Dictionary<LedgerKind, double>();

        public string RoundText { get { return RoundNumber > 0 ? Loc.T("ux.mm4c.roundOf", RoundNumber, RoundCount) : ""; } }

        public static EventRecap Capture(GameController game, WeekendSimulation w)
        {
            if (game == null || w == null) return null;
            var world = game.World;
            var cat = game.Db.GetCategory(w.CategoryId);
            var r = new EventRecap
            {
                CategoryId = w.CategoryId, CategoryName = cat != null ? cat.Name : w.CategoryId,
                EventName = Loc.Round(w.Round.Name), Steps = WeekendStepper.Steps(w), Season = world.Season
            };
            RoundOf(world, game.Db, w.CategoryId, w.Round, out r.RoundNumber, out r.RoundCount);
            ChampionshipState champ;
            if (world.Championships.TryGetValue(w.CategoryId, out champ))
            {
                Snapshot(champ.SortedRiders(), r.RiderPosBefore, r.RiderPtsBefore);
                Snapshot(champ.SortedTeams(), r.TeamPosBefore, r.TeamPtsBefore);
            }
            var team = world.PlayerTeam;
            if (team != null && team.CategoryId == w.CategoryId && world.OrganisationOf(team) != null)
            {
                r.TeamId = team.Id;
                r.OrgId = team.OrganisationId;
                r.CashBefore = Accounts.Cash(world, team);
                foreach (var kv in Accounts.TotalsFor(world, r.OrgId, r.Season, r.TeamId)) r._pnlBefore[kv.Key] = kv.Value.Pnl;
            }
            return r;
        }

        /// <summary>After the event is completed (points and money applied): the balance rows.</summary>
        public void Finish(GameController game)
        {
            Finished = true;
            if (TeamId == null || game == null) return;
            var world = game.World;
            var team = world.GetTeam(TeamId);
            if (team == null) return;
            CashAfter = Accounts.Cash(world, team);
            var after = Accounts.TotalsFor(world, OrgId, Season, TeamId);
            var kinds = new HashSet<LedgerKind>(after.Keys);
            foreach (var k in _pnlBefore.Keys) kinds.Add(k);
            Rows.Clear();
            RoundTotal = YearTotal = 0;
            foreach (var k in kinds)
            {
                AccountTotal a;
                double year = after.TryGetValue(k, out a) ? a.Pnl : 0;
                double before;
                _pnlBefore.TryGetValue(k, out before);
                double round = year - before;
                // Only concepts that exist for this team (money moved this season).
                if (Math.Abs(year) < 0.5 && Math.Abs(round) < 0.5) continue;
                Rows.Add(new BalanceRow { Kind = k, Round = round, Year = year });
                RoundTotal += round;
                YearTotal += year;
            }
            // The biggest movements of the round first, then the season's.
            Rows.Sort((x, y) =>
            {
                int c = Math.Abs(y.Round).CompareTo(Math.Abs(x.Round));
                return c != 0 ? c : Math.Abs(y.Year).CompareTo(Math.Abs(x.Year));
            });
        }

        private static void Snapshot(List<StandingEntry> table, Dictionary<string, int> pos, Dictionary<string, double> pts)
        {
            for (int i = 0; i < table.Count; i++)
            {
                pos[table[i].Id] = i + 1;
                pts[table[i].Id] = table[i].Points;
            }
        }

        /// <summary>Round number of <paramref name="round"/> for the category and rounds it races in the season.</summary>
        public static void RoundOf(WorldState world, GameDatabase db, string categoryId, CalendarRound round, out int number, out int count)
        {
            number = count = 0;
            var cat = db.GetCategory(categoryId);
            CalendarDefinition cal;
            if (cat == null || round == null || !world.Calendars.TryGetValue(cat.CalendarGroup ?? "", out cal)) return;
            foreach (var r in cal.Rounds)
            {
                if (r.Categories != null && r.Categories.Count > 0 && !r.Categories.Contains(categoryId)) continue;
                count++;
                if (number == 0 && (ReferenceEquals(r, round) || (r.Round == round.Round && r.Date == round.Date))) number = count;
            }
        }

        /// <summary>
        /// The closing sequence after the event (MM4 §1.5 e/h): updated championship standings, then the event balance
        /// (when the player runs a team in the category), then <paramref name="done"/>.
        /// </summary>
        public static void ShowClosing(EventRecap recap, Action done)
        {
            if (recap == null || !recap.Finished)
            {
                if (done != null) done();
                return;
            }
            EventStandingsPanel.Show(recap, () =>
            {
                if (recap.TeamId != null) EventBalancePanel.Show(recap, done);
                else if (done != null) done();
            });
        }

        /// <summary>Money as rich text: positive green with "+", negative red, zero dim.</summary>
        public static string SignedMoney(double v)
        {
            if (Math.Abs(v) < 0.5) return Theme.Dim(MathX.FormatMoney(0));
            return v > 0 ? Theme.GoodText("+" + MathX.FormatMoney(v)) : Theme.BadText(MathX.FormatMoney(v));
        }
    }

    /// <summary>
    /// After the event: the championship of the player's category updated with this round (MM4 §1.5 e), riders or
    /// teams (text toggle), with the move of each position and the points scored this round. CONTINUAR is the one
    /// primary (slanted block at the top right).
    /// </summary>
    public class EventStandingsPanel : MonoBehaviour
    {
        private EventRecap _r;
        private Action _done;
        private RectTransform _area;
        private RectTransform _tabs;
        private static int _tab;

        public static void Show(EventRecap recap, Action done)
        {
            var canvas = UiFactory.CreateCanvas("EventStandings", 30);
            var p = canvas.gameObject.AddComponent<EventStandingsPanel>();
            p._r = recap;
            p._done = done;
            var root = UiFactory.SafeRoot(canvas, Theme.Background);
            RaceUi.HostSheets(canvas.gameObject, root);
            p.Build(root);
        }

        private void Build(RectTransform root)
        {
            RaceUi.Backdrop(root);
            RectTransform cta;
            RaceUi.TopBar(root, Loc.T("ux.mm4c.standings"), _r.CategoryName + (_r.RoundText.Length > 0 ? " · " + _r.RoundText : ""), RaceUi.ActionW, out cta);
            RaceUi.ActionBlock(cta, Loc.T("post.continue"), Close);
            float top = RaceUi.BarH + 8;
            WeekendStepper.Strip(root, top, _r.Steps, _r.Steps.Count);
            top += WeekendStepper.Height + 8;
            _tabs = RaceUi.Content(root, top, 0);
            _tabs.anchorMin = new Vector2(0, 1);
            _tabs.offsetMin = new Vector2(RaceUi.Pad, -top - Ui.Grid.Hit);
            _area = RaceUi.Content(root, top + Ui.Grid.Hit + 8, RaceUi.Pad);
            Render();
        }

        private void Render()
        {
            Ui.CloseSheet();
            UiFactory.Clear(_tabs);
            var holder = UiFactory.Rect("Tabs", _tabs);
            UiFactory.Place(holder, 0, 0, 0.5f, 1);
            var v = holder.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandWidth = true;
            Ui.Segmented(holder, new[] { Loc.T("ux.mm4c.riders"), Loc.T("ux.mm4c.teams") }, _tab, i => { _tab = i; Render(); });

            UiFactory.Clear(_area);
            var game = GameSession.Instance != null ? GameSession.Instance.Game : null;
            ChampionshipState champ = null;
            if (game != null) game.World.Championships.TryGetValue(_r.CategoryId, out champ);
            if (champ == null)
            {
                var list = UiFactory.ScrollList("Empty", _area);
                UiFactory.Stretch((RectTransform)list.parent.parent);
                Ui.Empty(Ui.Card(list, _r.CategoryName), null, Loc.T("ux.mm4c.noStandings"));
                return;
            }
            Table(game.World, _tab == 0 ? champ.SortedRiders() : champ.SortedTeams(), _tab == 0);
        }

        /// <summary>Pos | rider (team) or team | move since the round before | points this round | points.</summary>
        private void Table(WorldState world, List<StandingEntry> table, bool riders)
        {
            var w = new[] { 1f, 6f, 1.4f, 1.6f, 1.6f };
            var head = UiFactory.Rect("Captions", _area);
            head.anchorMin = new Vector2(0, 1);
            head.anchorMax = new Vector2(1, 1);
            head.pivot = new Vector2(0.5f, 1);
            head.offsetMin = new Vector2(0, -40);
            head.offsetMax = Vector2.zero;
            var hl = head.gameObject.AddComponent<VerticalLayoutGroup>();
            hl.childControlHeight = true;
            hl.childControlWidth = true;
            hl.childForceExpandWidth = true;
            Ui.ListCaptions(head, w, new[]
            {
                Loc.T("post.col.pos"), Loc.T(riders ? "col.rider" : "ux.mm4c.team"), Loc.T("ux.mm4c.move"), Loc.T("ux.mm4c.thisRound"), Loc.T("post.col.pts")
            });
            var holder = UiFactory.Rect("Rows", _area);
            UiFactory.Stretch(holder);
            holder.offsetMax = new Vector2(0, -40);
            var list = UiFactory.ScrollList("Standings", holder, 0);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var layout = list.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 0;
            layout.padding = new RectOffset(0, 0, 0, 8);
            var posBefore = riders ? _r.RiderPosBefore : _r.TeamPosBefore;
            var ptsBefore = riders ? _r.RiderPtsBefore : _r.TeamPtsBefore;
            for (int i = 0; i < table.Count; i++)
            {
                var e = table[i];
                Team team;
                string name;
                if (riders)
                {
                    var rider = world.GetRider(e.Id);
                    team = rider != null && rider.TeamId != null ? world.GetTeam(rider.TeamId) : null;
                    name = (rider != null ? rider.FullName : e.Id) +
                           (team != null ? "  <size=" + Theme.TextMin + ">" + Theme.Dim(team.ShortName ?? team.Name) + "</size>" : "");
                }
                else
                {
                    team = world.GetTeam(e.Id);
                    name = (team != null ? team.Name : e.Id);
                }
                int before;
                string move = posBefore.TryGetValue(e.Id, out before) && before > 0 ? Theme.Signed(before - (i + 1)) : Theme.Dim("-");
                double prev;
                ptsBefore.TryGetValue(e.Id, out prev);
                double gained = e.Points - prev;
                string round = gained > 0.01 ? Theme.GoodText("+" + gained.ToString("0", Loc.Culture)) : Theme.Dim("0");
                bool mine = team != null && team.IsPlayer;
                var row = Ui.ListRow(list, w, new[]
                {
                    (i + 1).ToString(), name, move, round, "<b>" + e.Points.ToString("0", Loc.Culture) + "</b>"
                }, i, mine, null, 56);
                RaceUi.TeamBar(row, w, RaceUi.TeamColor(team != null ? team.Colors.Primary : null));
            }
        }

        private void Close()
        {
            Destroy(gameObject);
            if (_done != null) _done();
        }
    }

    /// <summary>
    /// After the event: the team's money (MM4 §1.5 h). Cash, the result of the round and of the season so far in stat
    /// tiles; the breakdown by ledger concept in two columns, this round / year to date (positives green, negatives
    /// red); only concepts with money this season. CONTINUAR goes back to management.
    /// </summary>
    public class EventBalancePanel : MonoBehaviour
    {
        private Action _done;

        public static void Show(EventRecap recap, Action done)
        {
            var canvas = UiFactory.CreateCanvas("EventBalance", 30);
            var p = canvas.gameObject.AddComponent<EventBalancePanel>();
            p._done = done;
            var root = UiFactory.SafeRoot(canvas, Theme.Background);
            RaceUi.HostSheets(canvas.gameObject, root);
            p.Build(root, recap);
        }

        private void Build(RectTransform root, EventRecap r)
        {
            RaceUi.Backdrop(root);
            RectTransform cta;
            RaceUi.TopBar(root, Loc.T("ux.mm4c.balance"), r.EventName + (r.RoundText.Length > 0 ? " · " + r.RoundText : ""), RaceUi.ActionW, out cta);
            RaceUi.ActionBlock(cta, Loc.T("post.continue"), Close);
            float top = RaceUi.BarH + 8;
            WeekendStepper.Strip(root, top, r.Steps, r.Steps.Count);
            top += WeekendStepper.Height + 12;
            var content = RaceUi.Content(root, top, RaceUi.Pad);
            var cols = Ui.Columns12(content, 4, 8);

            var tiles = Ui.Rows(cols[0], 1, 1, 1);
            Ui.StatTile(tiles[0], Loc.T("ux.mm4c.cash"), MathX.FormatMoney(r.CashAfter), Loc.T("ux.mm4c.cashMove", MathX.FormatMoney(r.CashBefore)),
                null, r.CashAfter < 0 ? (Color?)Theme.Bad : null);
            Ui.StatTile(tiles[1], Loc.T("ux.mm4c.roundResult"), MathX.FormatMoney(r.RoundTotal), Loc.T("ux.mm4c.roundResultSub"),
                null, r.RoundTotal < 0 ? Theme.Bad : Theme.Good);
            Ui.StatTile(tiles[2], Loc.T("ux.mm4c.yearResult"), MathX.FormatMoney(r.YearTotal), Loc.T("ux.mm4c.yearResultSub", r.Season),
                null, r.YearTotal < 0 ? Theme.Bad : Theme.Good);

            var list = UiFactory.ScrollList("Balance", cols[1]);
            UiFactory.Stretch((RectTransform)list.parent.parent);
            var card = Ui.Card(list, Loc.T("ux.mm4c.breakdown"), null,
                () => Ui.Sheet(Loc.T("ux.mm4c.breakdown"), l => UiFactory.Paragraph(l, Loc.T("ux.mm4c.balanceNote"), Theme.TextBody)));
            if (r.Rows.Count == 0)
            {
                Ui.Empty(card, null, Loc.T("ux.mm4c.noMoney"));
                return;
            }
            var w = new[] { 5f, 2.4f, 2.4f };
            UiFactory.TableRow(card, 44, w, new[] { Loc.T("ux.mm4c.concept"), Loc.T("ux.mm4c.thisRound"), Loc.T("ux.mm4c.yearToDate") }, true);
            Section(card, w, r.Rows, true);
            Section(card, w, r.Rows, false);
            UiFactory.TableRow(card, 52, w, new[]
            {
                "<b>" + Theme.Caps(Loc.T("ux.mm4c.total")) + "</b>", "<b>" + EventRecap.SignedMoney(r.RoundTotal) + "</b>", "<b>" + EventRecap.SignedMoney(r.YearTotal) + "</b>"
            }, false, Theme.Alpha(Theme.Ink, 0.35f));
        }

        /// <summary>Income concepts (season total positive) or expense concepts, under a dim heading.</summary>
        private static void Section(RectTransform card, float[] w, List<BalanceRow> rows, bool income)
        {
            bool any = false;
            foreach (var row in rows)
            {
                bool isIncome = row.Year > 0 || (row.Year == 0 && row.Round > 0);
                if (isIncome != income) continue;
                if (!any)
                {
                    UiFactory.TableRow(card, 40, w, new[] { Theme.Caps(Loc.T(income ? "ux.mm4c.income" : "ux.mm4c.expenses")), "", "" }, true);
                    any = true;
                }
                UiFactory.TableRow(card, 44, w, new[]
                {
                    Loc.Or("ledger." + row.Kind, row.Kind.ToString()), EventRecap.SignedMoney(row.Round), EventRecap.SignedMoney(row.Year)
                });
            }
        }

        private void Close()
        {
            Destroy(gameObject);
            if (_done != null) _done();
        }
    }
}
