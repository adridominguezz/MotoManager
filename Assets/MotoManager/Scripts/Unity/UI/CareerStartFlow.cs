using System;
using System.Collections.Generic;
using MotoManager.Core.Career;
using MotoManager.Core.Common;
using MotoManager.Core.Season;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Starting a career (UX spec §3.29): a header with the steps (PERFIL > CAMINO > EMPLEO / FUNDAR) and the manager's
    /// money, the step itself, and a bottom bar with "‹ VOLVER" on the left and the primary on the right. Profile: name,
    /// nationality and a grid of backgrounds (all their figures behind "i"). Path: two big tiles. Jobs: a tile per team
    /// that would hire this profile, the terms and ACEPTAR in a sheet. Founding: <see cref="FoundingPanel"/>. Business
    /// rules live in <see cref="CareerService"/>; this only presents them.
    /// </summary>
    public class CareerStartFlow
    {
        private enum Step { Profile, Path, Jobs, Founding }

        private readonly GameSession _session;
        private readonly GameController _game;
        private readonly RectTransform _area;
        private readonly Action _exit;
        private Step _step = Step.Profile;

        private string _first = "Alex", _last = "Rider", _nation = "ES", _background = "team_staff";
        private ManagerProfile _manager;
        private FoundingState _found = new FoundingState();

        /// <param name="area">The rect the flow fills (rebuilt on every step).</param>
        /// <param name="exit">Back from the first step (the start screen's menu).</param>
        public CareerStartFlow(GameSession session, GameController game, RectTransform area, Action exit)
        {
            _session = session;
            _game = game;
            _area = area;
            _exit = exit;
        }

        private WorldState World { get { return _game.World; } }

        public void Render()
        {
            Ui.CloseSheet();
            UiFactory.Clear(_area);
            Header();
            var content = Wp4Ui.Below(_area, 64 + Ui.Grid.Gap, Ui.Grid.Cta + Ui.Grid.Gap);
            var footer = Ui.Columns12(Wp4Ui.BottomStrip(_area, Ui.Grid.Cta), 3, 5, 4);
            switch (_step)
            {
                case Step.Profile: Profile(content, footer); break;
                case Step.Path: Path(content, footer); break;
                case Step.Jobs: Jobs(content, footer); break;
                case Step.Founding: Founding(content, footer); break;
            }
        }

        /// <summary>The steps, the current one bright, and the manager's money on the right.</summary>
        private void Header()
        {
            var bar = Wp4Ui.TopStrip(_area, 64);
            var title = Ui.Label(bar, Theme.Caps(Loc.T("career.new")), Theme.TextTitle, TextAnchor.UpperLeft, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Place(title.rectTransform, 0, 0.4f, 0.6f, 1);
            string third = _step == Step.Founding ? Loc.T("ux.start.stepFound") : _step == Step.Jobs ? Loc.T("ux.start.stepJob") : Loc.T("ux.start.stepJobOrFound");
            var names = new[] { Loc.T("ux.start.stepProfile"), Loc.T("ux.start.stepPath"), third };
            int at = _step == Step.Profile ? 0 : _step == Step.Path ? 1 : 2;
            var parts = new List<string>();
            for (int i = 0; i < names.Length; i++)
                parts.Add(i == at ? "<b>" + Theme.Caps(names[i]) + "</b>" : Theme.Dim(Theme.Caps(names[i])));
            var steps = Ui.Label(bar, string.Join(Theme.Dim("  " + Glyph.Chevron + "  "), parts.ToArray()), Theme.TextCaption, TextAnchor.LowerLeft, Theme.Text, FontStyle.Normal);
            UiFactory.Place(steps.rectTransform, 0, 0, 0.6f, 0.42f);
            if (_manager == null) return;
            var money = Ui.Label(bar, Theme.Dim(Theme.Caps(_manager.FullName)) + "\n<b>" + Loc.T("career.wealthNow", MathX.FormatMoney(_manager.PersonalWealth)) + "</b>",
                Theme.TextBody, TextAnchor.MiddleRight, Theme.Text, FontStyle.Normal);
            UiFactory.Place(money.rectTransform, 0.6f, 0, 1, 1);
        }

        // ------------------------------------------------------------------ profile

        private void Profile(RectTransform content, RectTransform[] footer)
        {
            var cols = Ui.Columns12(content, 4, 8);
            var me = Wp4Ui.ListIn(Ui.Tile(cols[0], Loc.T("career.profile"), null, null, 0, null, Wp4Ui.Info(Loc.T("career.profile"), Loc.T("career.profile.status"))), 8);
            UiFactory.Input(me, Loc.T("career.firstName"), _first, v => _first = v, 56);
            UiFactory.Input(me, Loc.T("career.lastName"), _last, v => _last = v, 56);
            var nations = new List<string>(_game.Db.NamePools.Keys);
            nations.Sort(StringComparer.Ordinal);
            Wp4Ui.CaptionedPicker(me, Loc.T("ux.start.nationality"), nations, nations.IndexOf(_nation), i => { _nation = nations[i]; Render(); });

            var grid = Ui.Tile(cols[1], Loc.T("career.background"));
            var list = new List<ManagerBackground>(_game.Db.ManagerBackgrounds.Values);
            list.Sort((a, b) => a.PersonalWealth.CompareTo(b.PersonalWealth));
            int perRow = 3, rowsN = Math.Max(1, (list.Count + perRow - 1) / perRow);
            var shares = new float[rowsN];
            for (int i = 0; i < rowsN; i++) shares[i] = 1;
            var rows = Ui.Rows(grid, shares);
            for (int i = 0; i < list.Count; i++)
            {
                var bg = list[i];
                var cell = Ui.Columns12(rows[i / perRow], 4, 4, 4)[i % perRow];
                BackgroundTile(cell, bg, _background == bg.Id);
            }

            Wp4Ui.BackLink(footer[0], Loc.T("career.back"), _exit);
            Wp4Ui.Chevron(Ui.PrimaryCTA(footer[2], Loc.T("career.next"), () =>
            {
                _manager = CareerService.CreatePlayer(World, _game.Db, _first, _last, _nation, _background);
                _step = Step.Path;
                Render();
            }));
        }

        private void BackgroundTile(RectTransform area, ManagerBackground bg, bool chosen)
        {
            string name = Loc.T("background." + bg.Id);
            var body = Ui.Tile(area, name, () => { _background = bg.Id; Render(); }, null, 0, null, () => Ui.Sheet(name, l =>
            {
                UiFactory.Paragraph(l, Loc.T("background." + bg.Id + ".desc"), Theme.TextBody);
                Ui.Line(l, Loc.T("career.wealth"), "<b>" + MathX.FormatMoney(bg.PersonalWealth) + "</b>", true);
                Ui.Line(l, Loc.T("mycareer.fame"), bg.Reputation.ToString("0"));
                Ui.Line(l, Loc.T("mycareer.skill.negotiation"), bg.Negotiation.ToString("0"));
                Ui.Line(l, Loc.T("mycareer.skill.sporting"), bg.SportingManagement.ToString("0"));
                Ui.Line(l, Loc.T("mycareer.skill.finance"), bg.Finance.ToString("0"));
                Ui.Line(l, Loc.T("mycareer.skill.relations"), bg.Relations.ToString("0"));
                Ui.Line(l, Loc.T("mycareer.skill.commercial"), bg.Commercial.ToString("0"));
            }, null, null, 0.5f), false, chosen ? Theme.CurrentAccent : (Color?)null);
            var v = Ui.Label(body, MathX.FormatMoney(bg.PersonalWealth), Theme.TextKey, TextAnchor.MiddleCenter, chosen ? Theme.Timing : Color.Lerp(Theme.Timing, Theme.TextDim, 0.4f), FontStyle.BoldAndItalic);
            UiFactory.Place(v.rectTransform, 0, 0.42f, 1, 1);
            var skills = new List<KeyValuePair<string, double>>
            {
                new KeyValuePair<string, double>("career.neg", bg.Negotiation),
                new KeyValuePair<string, double>("career.sport", bg.SportingManagement),
                new KeyValuePair<string, double>("career.fin", bg.Finance),
                new KeyValuePair<string, double>("career.rel", bg.Relations),
                new KeyValuePair<string, double>("career.com", bg.Commercial)
            };
            skills.Sort((a, b) => b.Value.CompareTo(a.Value));
            var pills = UiFactory.Rect("Pills", body);
            UiFactory.Place(pills, 0, 0, 1, 0.38f);
            var h = pills.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            for (int i = 0; i < 2 && i < skills.Count; i++)
                Ui.Pill(pills, Loc.T(skills[i].Key) + " " + skills[i].Value.ToString("0"), Theme.PanelLight);
        }

        // ------------------------------------------------------------------ path

        private void Path(RectTransform content, RectTransform[] footer)
        {
            var cols = Ui.Columns12(content, 6, 6);
            PathTile(cols[0], Loc.T("career.findJob"), Loc.T("ux.start.jobLine"), Loc.T("career.path.employed"), UiIcons.Career, () => { _step = Step.Jobs; Render(); });
            PathTile(cols[1], Loc.T("career.foundTeam"), Loc.T("ux.start.foundLine"), Loc.T("career.path.found"), UiIcons.Flag, () =>
            {
                _found = new FoundingState();
                _step = Step.Founding;
                Render();
            });
            Wp4Ui.BackLink(footer[0], Loc.T("career.back"), () => { _step = Step.Profile; Render(); });
        }

        private static void PathTile(RectTransform area, string title, string line, string info, string icon, Action go)
        {
            var body = Ui.Tile(area, title, go, null, 0, null, Wp4Ui.Info(title, info));
            var img = UiIcons.Icon(body, icon, Theme.Alpha(Theme.TextDim, 0.7f));
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.6f);
            img.rectTransform.sizeDelta = new Vector2(140, 140);
            var t = Ui.Label(body, Theme.Caps(line), Theme.TextHeader, TextAnchor.MiddleCenter, Theme.Text, FontStyle.Bold);
            UiFactory.Place(t.rectTransform, 0, 0.08f, 1, 0.28f);
        }

        // ------------------------------------------------------------------ jobs

        private void Jobs(RectTransform content, RectTransform[] footer)
        {
            var offers = CareerService.JobOffers(World, _game.Db, _manager);
            var list = Wp4Ui.ScrollIn(content, Ui.Grid.Gap);
            Ui.Section(list, Loc.T("career.jobs"), Theme.Caps(Loc.T("career.jobs.status", offers.Count)),
                Wp4Ui.Info(Loc.T("career.jobs"), Loc.T("career.jobs.note"), offers.Count == 0 ? Loc.T("career.jobs.none") : null));
            if (offers.Count == 0) Ui.Empty(list, UiIcons.Career, Loc.T("ux.start.noJobs"));
            RectTransform[] cols = null;
            int k = 0;
            foreach (var o in offers)
            {
                var offer = o;
                var team = World.GetTeam(o.TeamId);
                if (team == null) continue;
                var org = World.GetOrganisation(o.OrganisationId);
                var cat = _game.Db.GetCategory(o.CategoryId);
                if (k % 3 == 0) cols = Ui.Columns12(UiFactory.Row(list, 200), 4, 4, 4);
                var body = Ui.Tile(cols[k % 3], team.Name, () => Ui.Sheet(team.Name, l =>
                {
                    Ui.Line(l, Loc.T("career.col.category"), cat != null ? cat.Name : o.CategoryId);
                    Ui.Line(l, Loc.T("career.col.objective"), Loc.T("objective." + offer.Objective));
                    Ui.Line(l, Loc.T("career.col.salary"), "<b>" + MathX.FormatMoney(offer.SalaryPerSeason) + "</b>", true);
                    Ui.Line(l, Loc.T("career.col.budget"), MathX.FormatMoney(offer.AssignedBudget));
                    Ui.Line(l, Loc.T("career.col.autonomy"), Loc.T("autonomy." + offer.Autonomy));
                    if (org != null) Ui.Line(l, Loc.T("finance.owner"), Loc.T("owner." + org.Owner));
                    Ui.Line(l, Loc.T("ux.career.seasons"), offer.Seasons.ToString());
                }, UiFactory.Primary(Loc.T("career.accept"), () =>
                {
                    var r = CareerService.AcceptJob(World, _game.Db, _manager, offer);
                    if (r.Ok) _session.StartCareer(_game);
                }), null, 0.5f), cat != null ? cat.ShortName : null);
                var v = Ui.Label(body, MathX.FormatMoney(o.SalaryPerSeason), Theme.TextKey, TextAnchor.MiddleCenter, Theme.Timing, FontStyle.BoldAndItalic);
                UiFactory.Place(v.rectTransform, 0, 0.36f, 1, 1);
                var s = Ui.Label(body, Theme.Caps(Loc.T("objective." + o.Objective) + " · " + Loc.T("ux.career.perSeason")), Theme.TextCaption, TextAnchor.MiddleCenter, Theme.TextDim, FontStyle.Bold);
                UiFactory.Place(s.rectTransform, 0, 0, 1, 0.34f);
                k++;
            }
            Wp4Ui.BackLink(footer[0], Loc.T("career.back"), () => { _step = Step.Path; Render(); });
        }

        // ------------------------------------------------------------------ founding

        private void Founding(RectTransform content, RectTransform[] footer)
        {
            FoundingPanel.Build(content, _game, _manager, _found, Render, res =>
            {
                if (res.Ok)
                {
                    Screens.TeamSetupPage.OpenOnStart = true;
                    _session.StartCareer(_game);
                }
                else Render();
            });
            Wp4Ui.BackLink(footer[0], Loc.T("career.back"), () => { _step = Step.Path; _found.Category = null; Render(); });
        }
    }
}
