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
                BackgroundTile(cell, bg, list, _background == bg.Id);
            }

            Wp4Ui.BackLink(footer[0], Loc.T("career.back"), _exit);
            Wp4Ui.Chevron(Ui.PrimaryCTA(footer[2], Loc.T("career.next"), () =>
            {
                _manager = CareerService.CreatePlayer(World, _game.Db, _first, _last, _nation, _background);
                _step = Step.Path;
                Render();
            }));
        }

        /// <summary>The figures of a background compared by: the five skills, reputation and money (log scale).</summary>
        private static readonly string[] Traits = { "neg", "sport", "fin", "rel", "com", "rep", "wealth" };

        private static double TraitValue(ManagerBackground bg, string trait)
        {
            switch (trait)
            {
                case "neg": return bg.Negotiation;
                case "sport": return bg.SportingManagement;
                case "fin": return bg.Finance;
                case "rel": return bg.Relations;
                case "com": return bg.Commercial;
                case "rep": return bg.Reputation;
                default: return Math.Log10(Math.Max(1, bg.PersonalWealth));
            }
        }

        /// <summary>
        /// The strong and the weak point of a background: the figure that stands out most above, and most below, what
        /// the other backgrounds offer (each figure measured against the spread of all of them).
        /// </summary>
        private static void StrongWeak(ManagerBackground bg, List<ManagerBackground> all, out string strong, out string weak)
        {
            double best = double.MinValue, worst = double.MaxValue;
            strong = weak = Traits[0];
            foreach (var trait in Traits)
            {
                double sum = 0, sq = 0;
                foreach (var o in all) sum += TraitValue(o, trait);
                double mean = sum / Math.Max(1, all.Count);
                foreach (var o in all) sq += (TraitValue(o, trait) - mean) * (TraitValue(o, trait) - mean);
                double sd = Math.Sqrt(sq / Math.Max(1, all.Count));
                double z = sd > 1e-6 ? (TraitValue(bg, trait) - mean) / sd : 0;
                if (z > best) { best = z; strong = trait; }
                if (z < worst) { worst = z; weak = trait; }
            }
        }

        private void BackgroundTile(RectTransform area, ManagerBackground bg, List<ManagerBackground> all, bool chosen)
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
            var v = Ui.Label(body, MathX.FormatMoney(bg.PersonalWealth), Theme.TextKey, TextAnchor.MiddleLeft, chosen ? Theme.Timing : Color.Lerp(Theme.Timing, Theme.TextDim, 0.4f), FontStyle.BoldAndItalic);
            UiFactory.Place(v.rectTransform, 0, 0.56f, 1, 1);
            string strong, weak;
            StrongWeak(bg, all, out strong, out weak);
            var s = Ui.Label(body, Theme.GoodText("<b>+</b>  " + Loc.T("ux.start.strong." + strong)), Theme.TextBody, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
            UiFactory.Place(s.rectTransform, 0, 0.28f, 1, 0.56f);
            var w = Ui.Label(body, Theme.BadText("<b>−</b>  " + Loc.T("ux.start.weak." + weak)), Theme.TextBody, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
            UiFactory.Place(w.rectTransform, 0, 0, 1, 0.28f);
        }

        // ------------------------------------------------------------------ path

        private void Path(RectTransform content, RectTransform[] footer)
        {
            var offers = CareerService.JobOffers(World, _game.Db, _manager);
            double bestSalary = 0;
            foreach (var o in offers) bestSalary = Math.Max(bestSalary, o.SalaryPerSeason);
            var cols = Ui.Columns12(content, 6, 6);
            Action jobs = () => { _step = Step.Jobs; Render(); };
            Action found = () =>
            {
                _found = new FoundingState();
                _step = Step.Founding;
                Render();
            };
            PathCard(cols[0], Loc.T("career.findJob"), Loc.T("ux.start.jobLine"), Loc.T("career.path.employed"), UiIcons.Career, Theme.Info,
                Loc.T("ux.start.riskLow"), Theme.Good, new[]
                {
                    offers.Count > 0 ? Loc.T("ux.start.job.offers", offers.Count) : Loc.T("ux.start.job.noOffers"),
                    bestSalary > 0 ? Loc.T("ux.start.job.salaryUpTo", MathX.FormatMoney(bestSalary)) : Loc.T("ux.start.job.salary"),
                    Loc.T("ux.start.job.board")
                }, Loc.T("mycareer.seeOffers"), jobs);
            PathCard(cols[1], Loc.T("career.foundTeam"), Loc.T("ux.start.foundLine"), Loc.T("career.path.found"), UiIcons.Flag, Theme.Accent,
                Loc.T("ux.start.riskHigh"), Theme.Bad, new[]
                {
                    Loc.T("ux.start.found.money", MathX.FormatMoney(_manager.PersonalWealth)),
                    Loc.T("ux.start.found.control"),
                    Loc.T("ux.start.found.risk")
                }, Loc.T("mycareer.found"), found);
            Wp4Ui.BackLink(footer[0], Loc.T("career.back"), () => { _step = Step.Profile; Render(); });
        }

        /// <summary>
        /// One way into the career as a big card: a hero band in its own colour (gradient, the pictogram large and
        /// faint, the title in big caps over one line), a risk pill, three short facts and its own primary button. The
        /// whole card is tappable; the long explanation is behind "i".
        /// </summary>
        private static void PathCard(RectTransform area, string title, string line, string info, string icon, Color tint,
            string risk, Color riskColor, string[] facts, string cta, Action go)
        {
            var card = UiFactory.Panel("PathCard", area, Theme.Tile);
            UiFactory.Stretch(card.rectTransform);
            UiFactory.Surface(card);
            card.gameObject.AddComponent<Outline>().effectColor = Theme.Edge;
            UiFactory.BottomRule(card.transform, Theme.Race);
            var tap = card.gameObject.AddComponent<Button>();
            tap.targetGraphic = card;
            UiFactory.PressFeedback(tap, card.color);
            tap.onClick.AddListener(() =>
            {
                GameAudio.ClickSound();
                go();
            });

            // Hero band.
            var hero = UiFactory.Rect("Hero", card.transform);
            UiFactory.Place(hero, 0, 0.56f, 1, 1);
            hero.gameObject.AddComponent<RectMask2D>();
            var band = UiFactory.Decor("Band", hero, Theme.Alpha(tint, 0.9f));
            UiFactory.Stretch(band.rectTransform);
            UiFactory.Gradient(band, Color.white, new Color(1, 1, 1, 0.18f), true);
            var shade = UiFactory.Decor("Shade", hero, Color.white);
            UiFactory.Stretch(shade.rectTransform);
            UiFactory.Gradient(shade, new Color(0, 0, 0, 0f), new Color(0, 0, 0, 0.35f));
            var stripe = UiFactory.Decor("Stripe", hero, Theme.Alpha(Color.white, 0.12f));
            stripe.rectTransform.anchorMin = stripe.rectTransform.anchorMax = new Vector2(0.78f, 0.5f);
            stripe.rectTransform.sizeDelta = new Vector2(18, 600);
            stripe.rectTransform.localRotation = Quaternion.Euler(0, 0, -24);
            var img = UiIcons.Icon(hero, icon, Theme.Alpha(Color.white, 0.28f));
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(1, 0.5f);
            img.rectTransform.pivot = new Vector2(1, 0.5f);
            img.rectTransform.anchoredPosition = new Vector2(-28, -6);
            img.rectTransform.sizeDelta = new Vector2(190, 190);
            var t = Ui.Label(hero, Theme.Caps(title), Theme.TextDisplay, TextAnchor.LowerLeft, Color.white, FontStyle.BoldAndItalic);
            UiFactory.Place(t.rectTransform, 0, 0.36f, 0.78f, 1);
            t.rectTransform.offsetMin = new Vector2(28, 0);
            t.rectTransform.offsetMax = new Vector2(0, -12);
            var sub = Ui.Label(hero, Theme.Caps(line), Theme.TextHeader, TextAnchor.UpperLeft, Theme.Alpha(Color.white, 0.88f), FontStyle.Bold);
            UiFactory.Place(sub.rectTransform, 0, 0.06f, 0.78f, 0.36f);
            sub.rectTransform.offsetMin = new Vector2(28, 0);
            var ib = Ui.IconButton(hero, Glyph.Info, Wp4Ui.Info(title, info), Color.white);
            var irt = (RectTransform)ib.transform;
            irt.anchorMin = irt.anchorMax = new Vector2(1, 1);
            irt.pivot = new Vector2(1, 1);
            irt.anchoredPosition = new Vector2(-6, -6);

            // Body: risk, facts and the button.
            var body = UiFactory.Rect("Body", card.transform);
            UiFactory.Place(body, 0, 0, 1, 0.56f);
            body.offsetMin = new Vector2(28, 20);
            body.offsetMax = new Vector2(-24, -16);
            var pill = Ui.Pill(body, risk, Theme.Alpha(riskColor, 0.85f));
            pill.rectTransform.anchorMin = pill.rectTransform.anchorMax = new Vector2(0, 1);
            pill.rectTransform.pivot = new Vector2(0, 1);
            pill.rectTransform.anchoredPosition = Vector2.zero;
            var factsArea = Wp4Ui.Below(body, 44, Ui.Grid.Cta + 12);
            for (int i = 0; i < facts.Length; i++)
            {
                var row = UiFactory.Rect("Fact", factsArea);
                UiFactory.Place(row, 0, 1 - (i + 1f) / facts.Length, 1, 1 - (float)i / facts.Length);
                var mark = UiFactory.Decor("Mark", row, tint);
                UiFactory.Slant(mark, 3f);
                mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = new Vector2(0, 0.5f);
                mark.rectTransform.pivot = new Vector2(0, 0.5f);
                mark.rectTransform.sizeDelta = new Vector2(12, 26);
                var f = Ui.Label(row, facts[i], Theme.TextBody, TextAnchor.MiddleLeft, Theme.Text, FontStyle.Bold);
                UiFactory.Stretch(f.rectTransform);
                f.rectTransform.offsetMin = new Vector2(28, 0);
            }
            var bc = Ui.Columns12(Wp4Ui.BottomStrip(body, Ui.Grid.Cta), 4, 8);
            Wp4Ui.Chevron(Ui.PrimaryCTA(bc[1], cta, go));
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
