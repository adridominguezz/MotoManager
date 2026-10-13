using System;
using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.Race;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// The team sheet with a carousel (MM4 §1.6, docs/MM4_REFERENCIA.md part 2): a modal sheet over any page with the
    /// team name between `‹ ›` arrows that move to the previous / next team of the same category (in standings order,
    /// wrapping), and two text tabs. EQUIPO: championship position and figures, maker / bike level and the riders
    /// (number, name, nationality, age, level as the player's team perceives it, standings position). HISTORIAL: the
    /// team's finished seasons from the archived standings, one season at a time with its own `‹ ›` (older / newer),
    /// the riders it fielded that season (from the riders' career records) and a list of every season to jump to.
    /// Only real data: what the world does not keep (staff, budget of rivals, past bikes) is not shown.
    /// </summary>
    internal static class TeamSheet
    {
        private static string _teamId;
        private static int _view;
        private static int _season;

        /// <summary>Opens the sheet on <paramref name="teamId"/> (keeps the tab; a new team starts at its last season).</summary>
        public static void Open(GameController game, string teamId)
        {
            if (game == null || game.World.GetTeam(teamId) == null) return;
            if (teamId != _teamId) _season = 0;
            _teamId = teamId;
            Show(game);
        }

        /// <summary>The teams of a category: standings order first, then any team not classified yet.</summary>
        private static List<Team> Ring(WorldState world, string categoryId)
        {
            var list = new List<Team>();
            ChampionshipState champ;
            if (categoryId != null && world.Championships.TryGetValue(categoryId, out champ))
                foreach (var e in champ.SortedTeams())
                {
                    var t = world.GetTeam(e.Id);
                    if (t != null && t.CategoryId == categoryId && !list.Contains(t)) list.Add(t);
                }
            if (categoryId != null)
                foreach (var t in world.TeamsInCategory(categoryId))
                    if (!list.Contains(t)) list.Add(t);
            return list;
        }

        private static void Show(GameController game)
        {
            var world = game.World;
            var team = world.GetTeam(_teamId);
            if (team == null) return;
            var ring = Ring(world, team.CategoryId);
            int idx = ring.IndexOf(team);
            if (idx < 0)
            {
                ring.Insert(0, team);
                idx = 0;
            }
            int n = ring.Count;
            var cat = game.Db.GetCategory(team.CategoryId);
            string catName = cat != null ? cat.ShortName : team.CategoryId ?? "-";
            string sub = catName + "  ·  " + (idx + 1) + " / " + n;
            if (team.Id == world.PlayerTeamId) sub += "  ·  " + Theme.AccentText(Loc.T("ux.mm4b.yourTeam"));
            Action prev = null, next = null;
            if (n > 1)
            {
                var p = ring[(idx - 1 + n) % n];
                var q = ring[(idx + 1) % n];
                prev = () => Open(game, p.Id);
                next = () => Open(game, q.Id);
            }

            Ui.Sheet(Loc.T("ux.mm4b.teamSheet"), list =>
            {
                Carousel.Row(list, team.Name, sub, prev, next, 88, UiFactory.ParseColor(team.Colors != null ? team.Colors.Primary : null, Theme.Steel));
                Ui.Segmented(list, new[] { Loc.T("ux.mm4b.tabTeam"), Loc.T("ux.mm4b.tabHistory") }, _view, i =>
                {
                    _view = i;
                    Show(game);
                });
                if (_view == 1) History(list, game, team);
                else Current(list, game, team, cat);
            }, null, null, 0.86f);
        }

        // ================================================================== EQUIPO

        private static void Current(Transform list, GameController game, Team team, ChampionshipCategory cat)
        {
            var world = game.World;
            RectTransform left, right;
            Ui.Columns(list, out left, out right);

            // Championship: position among the classified teams and the figures of the entry.
            ChampionshipState champ;
            world.Championships.TryGetValue(team.CategoryId ?? "", out champ);
            Ui.Section(left, Loc.T("ux.mm4b.championship"), cat != null ? Theme.Caps(cat.ShortName) : null);
            StandingEntry entry = null;
            int pos = 0, of = 0;
            if (champ != null)
            {
                var sorted = champ.SortedTeams();
                of = sorted.Count;
                for (int i = 0; i < sorted.Count; i++)
                    if (sorted[i].Id == team.Id) { entry = sorted[i]; pos = i + 1; }
                Ui.Line(left, Loc.T("col.season"), champ.Season.ToString());
            }
            if (entry != null && champ.Rounds.Count > 0)
            {
                Ui.Line(left, Loc.T("ux.mm4b.position"), "<b>P" + pos + "</b>  " + Theme.Dim("/ " + of), true);
                Ui.Line(left, Loc.T("col.points"), "<b>" + entry.Points.ToString("0") + "</b>");
                Ui.Line(left, Loc.T("col.wins"), entry.Wins.ToString());
                Ui.Line(left, Loc.T("col.podiums"), entry.Podiums.ToString());
                Ui.Line(left, Loc.T("col.poles"), entry.Poles.ToString());
            }
            else Ui.Line(left, Loc.T("ux.mm4b.position"), Theme.Dim(Loc.T("ux.standings.noRounds")));

            // Bike: maker, model, status and the on-track level (mean of the performance values, as on our bike page).
            Ui.Section(left, Loc.T("ux.mm4b.bike"));
            var bike = world.GetBike(team.BikeId);
            var maker = world.GetManufacturer(bike != null ? bike.ManufacturerId : team.ManufacturerId);
            Ui.Line(left, Loc.T("col.maker"), "<b>" + (maker != null ? maker.Name : "-") + "</b>");
            if (bike != null && !string.IsNullOrEmpty(bike.Model)) Ui.Line(left, Loc.T("ux.mm4b.model"), bike.Model + "  " + Theme.Dim(bike.Year.ToString()));
            Ui.Line(left, Loc.T("ux.mm4b.status"), Loc.T("ux.mm4b.status." + team.Status));
            var cls = bike != null ? game.Db.GetBikeClass(bike.BikeClassId) : null;
            if (bike != null && cls != null)
                Ui.Line(left, Loc.T("ux.mm4b.bikeLevel"), Fmt.Rating(BikeScreen.PerformanceScore(BikePerformance.Compute(bike, cls))));
            if (!string.IsNullOrEmpty(team.Country)) Ui.Line(left, Loc.T("ux.mm4b.country"), team.Country.ToUpperInvariant());

            // Riders: number, name + nationality, age, level (perceived by the player's team), standings position.
            var riders = new List<Rider>();
            foreach (var id in team.RiderIds)
            {
                var r = world.GetRider(id);
                if (r != null) riders.Add(r);
            }
            Ui.Section(right, Loc.T("ux.mm4b.riders"), riders.Count.ToString());
            if (riders.Count == 0)
            {
                Ui.Empty(right, UiIcons.Person, Loc.T("ux.mm4b.noRiders"));
                return;
            }
            var widths = new[] { 1f, 4.6f, 1.1f, 1.2f, 1.2f };
            Ui.ListCaptions(right, widths, new[] { "#", Loc.T("col.rider"), Loc.T("col.age"), Loc.T("col.level"), Loc.T("ux.mm4b.pos") });
            for (int i = 0; i < riders.Count; i++)
            {
                var r = riders[i];
                int rp = champ != null && champ.Rounds.Count > 0 ? champ.RiderPosition(r.Id) : 0;
                string name = "<b>" + r.FullName + "</b>" +
                              (string.IsNullOrEmpty(r.Nationality) ? "" : "  " + Theme.Dim("<size=" + Theme.TextCaption + ">" + r.Nationality.ToUpperInvariant() + "</size>"));
                Ui.ListRow(right, widths, new[]
                {
                    r.Number > 0 ? "#" + r.Number : "-", name, r.AgeOn(world.CurrentDate).ToString(),
                    Fmt.Rating(Fmt.Estimate(world, world.PlayerTeamId, r, "RaceSkill")), rp > 0 ? "P" + rp : "-"
                }, i, team.Id == world.PlayerTeamId, null, 64);
            }
            UiFactory.Paragraph(right, Theme.Dim(Loc.T("ux.mm4b.levelNote")), Theme.TextCaption);
        }

        // ================================================================== HISTORIAL

        private class TeamSeason
        {
            public int Season;
            public string CategoryId;
            public int Position, Of;
            public StandingEntry Entry;
        }

        /// <summary>The finished seasons of a team (archived team standings), newest first.</summary>
        private static List<TeamSeason> Seasons(WorldState world, string teamId)
        {
            var list = new List<TeamSeason>();
            foreach (var c in world.ArchivedChampionships)
            {
                if (c == null || c.Teams == null || !c.Teams.ContainsKey(teamId)) continue;
                var sorted = ChampionshipState.Sort(c.Teams);
                int pos = 0;
                for (int i = 0; i < sorted.Count; i++)
                    if (sorted[i].Id == teamId) pos = i + 1;
                list.Add(new TeamSeason { Season = c.Season, CategoryId = c.CategoryId, Position = pos, Of = sorted.Count, Entry = c.Teams[teamId] });
            }
            list.Sort((a, b) =>
            {
                int c = b.Season.CompareTo(a.Season);
                return c != 0 ? c : string.CompareOrdinal(a.CategoryId, b.CategoryId);
            });
            return list;
        }

        private static string Short(GameDatabase db, string categoryId)
        {
            var c = db.GetCategory(categoryId);
            return c != null ? c.ShortName : categoryId ?? "-";
        }

        private static void History(Transform list, GameController game, Team team)
        {
            var world = game.World;
            var seasons = Seasons(world, team.Id);
            if (seasons.Count == 0)
            {
                Ui.Empty(list, UiIcons.Trophy, Loc.T("ux.history.empty"));
                UiFactory.Paragraph(list, Theme.Dim(Loc.T("ux.mm4b.historyNote")), Theme.TextCaption);
                return;
            }
            _season = Mathf.Clamp(_season, 0, seasons.Count - 1);
            var s = seasons[_season];
            string cat = Short(game.Db, s.CategoryId);
            // ‹ goes back in time (older), › forward (newer); no wrap.
            Action older = _season < seasons.Count - 1 ? () => { _season++; Show(game); } : (Action)null;
            Action newer = _season > 0 ? () => { _season--; Show(game); } : (Action)null;
            Carousel.Row(list, s.Season + "  ·  " + cat, Loc.T("ux.mm4b.seasonOf", seasons.Count - _season, seasons.Count), older, newer, 72, null);

            RectTransform left, right;
            Ui.Columns(list, out left, out right);
            Ui.Section(left, Loc.T("ux.mm4b.result"), s.Season.ToString());
            Ui.Line(left, Loc.T("ux.mm4b.position"), "<b>P" + s.Position + "</b>  " + Theme.Dim("/ " + s.Of), true);
            Ui.Line(left, Loc.T("col.points"), "<b>" + s.Entry.Points.ToString("0") + "</b>");
            Ui.Line(left, Loc.T("col.wins"), s.Entry.Wins.ToString());
            Ui.Line(left, Loc.T("col.podiums"), s.Entry.Podiums.ToString());
            Ui.Line(left, Loc.T("col.poles"), s.Entry.Poles.ToString());
            var record = world.Champions.Find(c => c.Season == s.Season && c.CategoryId == s.CategoryId);
            bool teamTitle = (record != null && record.TeamChampionId == team.Id) || s.Position == 1;
            if (teamTitle) Ui.Line(left, Loc.T("history.teamTitle"), Theme.TimingText("<b>" + Loc.T("ux.mm4b.yes") + "</b>"));
            var hist = world.History.Find(h => h.Season == s.Season && h.CategoryId == s.CategoryId);
            if (hist != null && hist.Champion != null) Ui.Line(left, Loc.T("col.champion"), hist.Champion);

            // The riders the team fielded that season (career records of every rider, retired ones included).
            var rows = new List<KeyValuePair<Rider, SeasonRecord>>();
            foreach (var r in world.Riders.Values)
                foreach (var rec in r.CareerHistory)
                    if (rec.Season == s.Season && rec.TeamId == team.Id && rec.CategoryId == s.CategoryId)
                        rows.Add(new KeyValuePair<Rider, SeasonRecord>(r, rec));
            rows.Sort((a, b) =>
            {
                int pa = a.Value.ChampionshipPosition > 0 ? a.Value.ChampionshipPosition : 999;
                int pb = b.Value.ChampionshipPosition > 0 ? b.Value.ChampionshipPosition : 999;
                return pa.CompareTo(pb);
            });
            Ui.Section(right, Loc.T("ux.mm4b.riders"), rows.Count.ToString());
            if (rows.Count == 0) Ui.Empty(right, UiIcons.Person, Loc.T("ux.mm4b.noRiders"));
            else
            {
                var widths = new[] { 5f, 1.2f, 1.4f, 1.2f };
                Ui.ListCaptions(right, widths, new[] { Loc.T("col.rider"), Loc.T("ux.mm4b.pos"), Loc.T("ux.standings.pts"), Loc.T("ux.standings.wins") });
                for (int i = 0; i < rows.Count; i++)
                {
                    var rec = rows[i].Value;
                    Ui.ListRow(right, widths, new[]
                    {
                        rows[i].Key.FullName, rec.ChampionshipPosition > 0 ? "P" + rec.ChampionshipPosition : "-",
                        "<b>" + rec.Points.ToString("0") + "</b>", rec.Wins.ToString()
                    }, i, false, null, 56);
                }
            }

            // Every season, to jump to one.
            Ui.Section(list, Loc.T("ux.mm4b.allSeasons"), seasons.Count.ToString());
            var w = new[] { 1.6f, 2.4f, 1.6f, 1.6f, 1.2f };
            Ui.ListCaptions(list, w, new[] { Loc.T("ux.history.season"), Loc.T("ux.market.category"), Loc.T("ux.mm4b.pos"), Loc.T("ux.standings.pts"), Loc.T("ux.standings.wins") });
            for (int i = 0; i < seasons.Count; i++)
            {
                int k = i;
                var e = seasons[i];
                Wp4Ui.SelectRow(list, w, new[]
                {
                    e.Season.ToString(), Short(game.Db, e.CategoryId), "P" + e.Position + Theme.Dim(" / " + e.Of), "<b>" + e.Entry.Points.ToString("0") + "</b>", e.Entry.Wins.ToString()
                }, i, i == _season, () => { _season = k; Show(game); }, 56);
            }
        }
    }

    /// <summary>
    /// `‹ ›` carousels (MM4 §2): the Back pictogram (and the same turned round) as 56-unit touch targets either side of
    /// a centred label. No new icons.
    /// </summary>
    internal static class Carousel
    {
        /// <summary>A 56×56 arrow centred 28 units in from the left or right edge of <paramref name="parent"/>; disabled without an action.</summary>
        public static UnityEngine.UI.Button Arrow(RectTransform parent, bool right, Action onTap)
        {
            var b = Ui.IconButton(parent, UiIcons.Back, onTap);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(right ? 1 : 0, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(right ? -Ui.Grid.Hit / 2 : Ui.Grid.Hit / 2, 0);
            if (right) rt.localRotation = Quaternion.Euler(0, 0, 180);
            b.gameObject.name = right ? "Next" : "Prev";
            if (onTap == null)
                foreach (var g in b.GetComponentsInChildren<UnityEngine.UI.Graphic>())
                    if (g.gameObject != b.gameObject) g.color = Theme.Alpha(g.color, 0.3f);
            return b;
        }

        /// <summary>
        /// A layout row: ‹ arrow, the CAPS title in the display face (and an optional dim line under it), › arrow. An
        /// optional colour bar under the title marks the entity (team / category colour).
        /// </summary>
        public static RectTransform Row(Transform list, string title, string sub, Action prev, Action next, float height, Color? mark)
        {
            var row = UiFactory.Row(list, Mathf.Max(height, Ui.Grid.Hit));
            Arrow(row, false, prev);
            Arrow(row, true, next);
            var mid = UiFactory.Rect("Title", row);
            UiFactory.Stretch(mid);
            mid.offsetMin = new Vector2(Ui.Grid.Hit + 8, 0);
            mid.offsetMax = new Vector2(-Ui.Grid.Hit - 8, 0);
            bool two = !string.IsNullOrEmpty(sub);
            var t = Ui.Label(mid, Theme.Caps(title ?? ""), Theme.TextTitle, TextAnchor.MiddleCenter, Theme.Text, FontStyle.BoldAndItalic);
            Ui.Fit(t, Theme.TextTitle);
            UiFactory.Place(t.rectTransform, 0, two ? 0.44f : 0, 1, 1);
            if (two)
            {
                var s = Ui.Label(mid, sub, Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);
                UiFactory.Place(s.rectTransform, 0, 0, 1, 0.44f);
            }
            if (mark.HasValue)
            {
                var bar = UiFactory.Decor("Mark", mid, mark.Value);
                bar.rectTransform.anchorMin = new Vector2(0.38f, 0);
                bar.rectTransform.anchorMax = new Vector2(0.62f, 0);
                bar.rectTransform.offsetMin = Vector2.zero;
                bar.rectTransform.offsetMax = new Vector2(0, 4);
            }
            return row;
        }

        /// <summary>
        /// A compact carousel filling <paramref name="area"/> (a control row): ‹ arrow, the label (tap to pick from the
        /// whole list in a sheet), › arrow; on a faint control surface.
        /// </summary>
        public static void Compact(RectTransform area, string label, Action prev, Action next, Action onLabel)
        {
            var bg = UiFactory.Panel("Carousel", area, Theme.Alpha(Color.white, 0.04f));
            UiFactory.Stretch(bg.rectTransform);
            UiFactory.Surface(bg);
            bg.raycastTarget = false;
            Arrow(bg.rectTransform, false, prev);
            Arrow(bg.rectTransform, true, next);
            var hit = UiFactory.Panel("Label", bg.transform, new Color(1, 1, 1, 0f));
            UiFactory.Stretch(hit.rectTransform);
            hit.rectTransform.offsetMin = new Vector2(Ui.Grid.Hit, 0);
            hit.rectTransform.offsetMax = new Vector2(-Ui.Grid.Hit, 0);
            var t = Ui.Label(hit.transform, Theme.Caps(label ?? ""), Theme.TextHeader, TextAnchor.MiddleCenter, Theme.Text, FontStyle.BoldAndItalic);
            Ui.Fit(t, Theme.TextHeader);
            UiFactory.Stretch(t.rectTransform);
            t.raycastTarget = false;
            if (onLabel != null)
            {
                var b = hit.gameObject.AddComponent<UnityEngine.UI.Button>();
                b.targetGraphic = hit;
                UiFactory.PressFeedback(b, hit.color);
                b.onClick.AddListener(() =>
                {
                    GameAudio.ClickSound();
                    onLabel();
                });
            }
            else hit.raycastTarget = false;
        }
    }
}
