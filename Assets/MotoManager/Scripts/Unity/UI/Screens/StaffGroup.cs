using System;
using System.Collections.Generic;
using MotoManager.Core.Common;
using MotoManager.Core.Data;
using MotoManager.Core.Economy;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// One staff group (<see cref="Selected"/>): a person card per position (three side by side, scrolling sideways
    /// when there are more). A held position shows the holder (role initials in the hero band, name, country, age,
    /// stars from the level, then level, experience, rider or reputation, salary and contract end) with DETALLES,
    /// which opens the full sheet (renew, release, other rider). A vacant one shows its coverage, the candidates and
    /// CONTRATAR, which opens the candidates of that role. At the bottom ENCONTRAR PERSONAL opens the candidates of
    /// every role of the group.
    /// </summary>
    public class StaffGroupPage : ScreenBase
    {
        /// <summary>The group shown (set by the hub before opening the page).</summary>
        public static StaffGroup Selected = StaffGroup.Engineers;

        private const int Visible = 3;

        public override string Title { get { return StaffGroups.Name(Selected); } }
        public override bool HiddenTab { get { return true; } }

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = world.PlayerTeam;
            if (team == null) return;
            var group = Selected;
            var roles = StaffGroups.Roles(group);
            var positions = StaffGroups.Positions(world, Game.Db, team, group);
            double ceiling = StaffMarket.SkillCeiling(world, Game.Db, team);

            // Header: the group, the hiring ceiling and (behind "i") the roles it gathers.
            var names = new List<string>();
            foreach (var r in roles) names.Add(Loc.T("role." + r));
            var head = Ui.HeaderStrip(content, StaffGroups.Name(group), Theme.TimingText(Loc.T("staff.ceilingTag", ceiling.ToString("0"))));
            Ui.InfoButton(head, StaffGroups.Name(group), Loc.T("staff2.groupInfo." + group), string.Join(", ", names.ToArray()) + ".",
                Loc.T("staff.ceiling", ceiling.ToString("0")));

            // Bottom: find staff for the group (badge: candidates available).
            int available = 0;
            foreach (var r in roles) available += StaffMarket.Candidates(world, Game.Db, team, r).Count;
            var bottom = Wp2Ui.BottomStrip(content, Ui.Grid.Cta);
            var find = UiFactory.Rect("Find", bottom);
            UiFactory.Place(find, 0.2f, 0, 0.8f, 1);
            Cards.Find(find, Loc.T("staff2.find"), () => Market(world, team, roles, StaffGroups.Name(group)), available);

            // The cards: three across; more scroll sideways.
            var area = Wp2Ui.Above(content, Ui.Grid.Cta + Ui.Grid.Gap * 2, Ui.Grid.Header + Ui.Grid.Gap);
            var holder = positions.Count > Visible ? Strip(area, positions.Count) : area;
            for (int i = 0; i < positions.Count; i++)
            {
                var cell = UiFactory.Rect("Position", holder);
                if (positions.Count > Visible) UiFactory.Place(cell, (float)i / positions.Count, 0, (float)(i + 1) / positions.Count, 1);
                else
                {
                    // Fewer than three: same width as with three, centred.
                    float left = 0.5f - positions.Count / (2f * Visible);
                    UiFactory.Place(cell, left + (float)i / Visible, 0, left + (float)(i + 1) / Visible, 1);
                }
                cell.offsetMin = new Vector2(Ui.Grid.Gap / 2, 0);
                cell.offsetMax = new Vector2(-Ui.Grid.Gap / 2, 0);
                var p = positions[i];
                if (p.Holder != null) HeldCard(cell, world, team, p);
                else VacantCard(cell, world, team, p, ceiling);
            }
        }

        /// <summary>A sideways scroll in <paramref name="area"/> whose content is <paramref name="count"/> thirds wide.</summary>
        private static RectTransform Strip(RectTransform area, int count)
        {
            var bg = UiFactory.Panel("Cards", area, Color.clear);
            UiFactory.Stretch(bg.rectTransform);
            var scroll = bg.gameObject.AddComponent<ScrollRect>();
            scroll.vertical = false;
            scroll.horizontal = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            var viewport = UiFactory.Rect("Viewport", bg.transform);
            UiFactory.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var strip = UiFactory.Rect("Content", viewport);
            strip.anchorMin = new Vector2(0, 0);
            strip.anchorMax = new Vector2((float)count / Visible, 1);
            strip.pivot = new Vector2(0, 0.5f);
            strip.offsetMin = strip.offsetMax = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = strip;
            return strip;
        }

        /// <summary>Role caption over the hero band ("MECÁNICO JEFE · SURNAME" for a rider's crew).</summary>
        private static void RoleCaption(RectTransform body, StaffGroups.Position p)
        {
            var hero = body.parent != null ? body.parent.Find("Hero") : null;
            if (hero == null) return;
            string text = Loc.T("role." + p.Role) + (p.Rider != null ? " · " + p.Rider.LastName : "");
            var t = Ui.Label(hero, Theme.Caps(text), Theme.TextCaption, TextAnchor.UpperLeft, Theme.Alpha(Color.white, 0.9f), FontStyle.Bold);
            UiFactory.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(16, 0);
            t.rectTransform.offsetMax = new Vector2(-16, -10);
            Ui.Fit(t, Theme.TextCaption);
        }

        /// <summary>Initials of the role's name (its words of three letters or more): "DT", "JI", "MJ".</summary>
        private static string Initials(StaffRole role)
        {
            string s = "";
            foreach (var w in Loc.T("role." + role).Split(' '))
                if (w.Length > 2 && s.Length < 2) s += char.ToUpperInvariant(w[0]);
            return s.Length > 0 ? s : role.ToString().Substring(0, 1);
        }

        private void HeldCard(RectTransform area, WorldState world, Team team, StaffGroups.Position p)
        {
            var s = p.Holder;
            bool ending = s.ContractEndSeason <= world.Season;
            string age = s.BirthYear > 0 ? Loc.T("staff2.years", world.Season - s.BirthYear) : null;
            Action details = () => StaffActions.HolderSheet(Shell, world, team, s, true);
            var body = Cards.Person(area, Theme.Accent, Initials(p.Role), s.FirstName, s.LastName, s.Nationality,
                Cards.StarsFor(s.Skill), age, Loc.T("staff2.details"), details);
            RoleCaption(body, p);

            var labels = new List<string> { Loc.T("staff.skill"), Loc.T("staff2.experience") };
            var values = new List<string> { s.Skill.ToString("0"), Loc.T("staff2.years", s.Experience) };
            var colors = new List<Color?> { Ui.RatingColor(s.Skill), null };
            if (p.Rider != null)
            {
                labels.Add(Loc.T("staff2.rider"));
                values.Add(p.Rider.LastName);
            }
            else
            {
                labels.Add(Loc.T("staff2.reputation"));
                values.Add(s.Reputation.ToString("0"));
            }
            colors.Add(null);
            labels.Add(Loc.T("staff.salary"));
            values.Add(MathX.FormatMoney(s.Salary));
            colors.Add(null);
            labels.Add(Loc.T("staff2.contract"));
            values.Add(ending ? Loc.T("staff2.endsNow") : s.ContractEndSeason.ToString());
            colors.Add(ending ? Theme.Warning : (Color?)null);
            Cards.Rows(body, labels.ToArray(), values.ToArray(), colors.ToArray());
        }

        private void VacantCard(RectTransform area, WorldState world, Team team, StaffGroups.Position p, double ceiling)
        {
            var role = p.Role;
            var cands = StaffMarket.Candidates(world, Game.Db, team, role);
            Action hire = () => Market(world, team, new List<StaffRole> { role }, Loc.T("role." + role));
            // Corner: a standard position nobody holds is missing (warning); any other vacancy is optional (dim).
            var body = Cards.Person(area, Theme.PanelLight, Initials(role), "", Loc.T("staff2.vacant"), null, -1,
                p.Required ? Theme.WarnText(Loc.T("staff.cov.Missing")) : Loc.T("staff.cov.Optional"), Loc.T("staff.hire"), hire);
            RoleCaption(body, p);
            var best = cands.Count > 0 ? cands[0] : null;
            Cards.Rows(body,
                new[] { Loc.T("staff.candidates"), Loc.T("staff2.bestCandidate"), Loc.T("staff.salary"), Loc.T("ux.staff.ceiling") },
                new[]
                {
                    cands.Count.ToString(), best != null ? best.Skill.ToString("0") : "-",
                    best != null ? MathX.FormatMoney(best.Salary) : "-", ceiling.ToString("0")
                },
                new Color?[] { null, best != null ? Ui.RatingColor(best.Skill) : (Color?)null, null, Theme.Timing });
        }

        /// <summary>
        /// The staff market for <paramref name="roles"/> in a sheet: per role, who a new hire would replace and the
        /// candidates (people already in the team are left out) with their hire buttons.
        /// </summary>
        private void Market(WorldState world, Team team, List<StaffRole> roles, string title)
        {
            double ceiling = StaffMarket.SkillCeiling(world, Game.Db, team);
            Ui.Sheet(Loc.T("staff2.find") + " · " + title, list =>
            {
                Ui.Line(list, Loc.T("ux.staff.ceiling"), Theme.TimingText(ceiling.ToString("0")), true);
                foreach (var role in roles)
                {
                    double current;
                    int count;
                    StaffService.Coverage(world, Game.Db, team, role, out current, out count);
                    if (roles.Count > 1) Ui.Band(list, Loc.T("role." + role), current >= 0 ? Loc.T("staff.currentLevel") + " " + current.ToString("0") : Loc.T("staff2.vacant"));
                    var replaces = StaffMarket.WouldReplace(world, team, role);
                    if (replaces != null)
                        Ui.Alert(list, Loc.T("staff.replaceShort", replaces.FullName, MathX.FormatMoney(StaffMarket.SeveranceCost(world, replaces))));
                    int shown = StaffActions.Candidates(list, Shell, world, team, role, roles.Count > 1 ? 3 : 6, current);
                    if (shown == 0) Ui.Empty(list, UiIcons.Person, Loc.T("staff.noCandidates"));
                }
            }, null, null, 0.72f);
        }
    }
}
