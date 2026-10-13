using System;
using MotoManager.App.Race;
using MotoManager.Core.World;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// "Editar equipo": the team's livery on a turning bike in a small studio (3D showcase in a render texture, drag
    /// to turn), the team name over its top-left corner and the screen's only primary, APLICAR, at its top-right.
    /// Under it, text tabs PREESTABLECIDOS / PERSONALIZADOS and the "‹ ESTILO N ›" carousel of livery templates;
    /// at the bottom either 8 preset palettes (three colours each, the active one ticked) or, for personalised
    /// colours, the three colour slots (main, second, accent) over a row of 16 swatches. Everything previews live;
    /// APLICAR saves the colours, template and name to the team (the race and the interface use them).
    /// Reached from the organisation page ("EDITAR EQUIPO") and from the team setup's identity step.
    /// </summary>
    public class LiveryEditorPage : ScreenBase
    {
        public override string Title { get { return Loc.T("livery.title"); } }
        public override bool HiddenTab { get { return true; } }

        /// <summary>Team to edit, set by the page opening the editor (null: the player's team).</summary>
        public static string PendingTeamId;

        /// <summary>Preset palettes: main, second, accent.</summary>
        public static readonly string[][] Presets =
        {
            new[] { "#F2F4F7", "#1F2F5C", "#F28C28" },
            new[] { "#D7141A", "#F4F4F2", "#15161A" },
            new[] { "#FFD100", "#15161A", "#E10600" },
            new[] { "#00A3A3", "#15161A", "#C5E000" },
            new[] { "#15161A", "#F26B1D", "#F4F4F2" },
            new[] { "#1E5AA8", "#FFD100", "#F4F4F2" },
            new[] { "#5B2A86", "#E0457B", "#0F1020" },
            new[] { "#1E8E3E", "#F4F4F2", "#15161A" }
        };

        /// <summary>The personalised colours (a swatch grid, no colour wheel).</summary>
        public static readonly string[] Swatches =
        {
            "#F4F4F2", "#B8BCC4", "#5A5F6A", "#15161A", "#E10600", "#9E1B32", "#F26B1D", "#F5C518",
            "#C5E000", "#2DBE60", "#00A3A3", "#5FB7E5", "#1E88E5", "#1F2F5C", "#7B3FBF", "#E0457B"
        };

        private const float BottomHeight = 112;

        private string _teamId;
        private readonly string[] _colors = new string[3];
        private int _style;
        private string _name;
        private int _tab;
        private int _slot;
        private LiveryShowcaseView _view;

        private Team Load(WorldState world)
        {
            string id = PendingTeamId ?? _teamId;
            var team = id != null ? world.GetTeam(id) : null;
            if (team == null) team = world.PlayerTeam;
            if (team == null) return null;
            if (PendingTeamId != null || _teamId != team.Id)
            {
                _teamId = team.Id;
                Reset(team);
            }
            PendingTeamId = null;
            return team;
        }

        private void Reset(Team team)
        {
            _colors[0] = team.Colors.Primary;
            _colors[1] = team.Colors.Secondary;
            _colors[2] = team.Colors.TertiaryOrDefault();
            _style = team.Colors.StyleFor(team.Id);
            _name = team.Name;
            _tab = PresetIndex() >= 0 ? 0 : 1;
        }

        private BikeLivery Draft
        {
            get
            {
                return new BikeLivery(UiFactory.ParseColor(_colors[0], Color.red), UiFactory.ParseColor(_colors[1], Color.white),
                    UiFactory.ParseColor(_colors[2], Color.black), _style);
            }
        }

        private static bool Eq(string a, string b)
        {
            return string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
        }

        private int PresetIndex()
        {
            for (int i = 0; i < Presets.Length; i++)
                if (Eq(Presets[i][0], _colors[0]) && Eq(Presets[i][1], _colors[1]) && Eq(Presets[i][2], _colors[2])) return i;
            return -1;
        }

        private bool Dirty(Team team)
        {
            var c = team.Colors;
            return !Eq(c.Primary, _colors[0]) || !Eq(c.Secondary, _colors[1]) || !Eq(c.TertiaryOrDefault(), _colors[2])
                || c.StyleFor(team.Id) != _style || (_name ?? "").Trim() != team.Name;
        }

        public override void Build(RectTransform content)
        {
            var world = Game.World;
            var team = Load(world);
            if (team == null)
            {
                var c = Ui.Columns12(content, 3, 6);
                Ui.Empty(Wp4Ui.ListIn(Ui.Tile(c[1], Loc.T("livery.title"))), UiIcons.Team, Loc.T("setup.noTeam"));
                return;
            }

            // Showcase: the 3D studio fills the top of the page, on a surface with the system's small radius.
            var stageArea = Wp4Ui.Below(content, 0, BottomHeight + Ui.Grid.Hit + Ui.Grid.Gap * 2);
            var frame = UiFactory.Panel("Stage", stageArea, Theme.Panel);
            UiFactory.Surface(frame);
            UiFactory.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _view = LiveryShowcaseView.Create(frame.rectTransform, Draft, BikeVisual.HelmetOf(Draft, team.Id));
            var hint = Ui.Label(frame.transform, Loc.T("livery.dragHint"), Theme.TextCaption, TextAnchor.LowerCenter, Theme.TextDim);
            UiFactory.Place(hint.rectTransform, 0.3f, 0, 0.7f, 0);
            hint.rectTransform.offsetMin = new Vector2(0, 8);
            hint.rectTransform.offsetMax = new Vector2(0, 40);
            hint.raycastTarget = false;
            UiFactory.BottomRule(frame.transform);

            // Over the showcase: the team name (top-left) and APLICAR (top-right).
            var top = Ui.Columns12(Wp4Ui.TopStrip(frame.rectTransform, Ui.Grid.Cta + 24), 5, 4, 3);
            var nameHost = Wp4Ui.ListIn(top[0], 0);
            ((VerticalLayoutGroup)nameHost.GetComponent<LayoutGroup>()).padding = new RectOffset(16, 0, 16, 0);
            UiFactory.Input(nameHost, Loc.T("career.teamName"), _name, v => _name = v, 64);
            var applyHost = UiFactory.Rect("Apply", top[2]);
            UiFactory.Stretch(applyHost);
            applyHost.offsetMin = new Vector2(0, 12);
            applyHost.offsetMax = new Vector2(-16, -12);
            bool nameOk = (_name ?? "").Trim().Length >= 2;
            Ui.PrimaryCTA(applyHost, Loc.T("livery.apply"), () => Apply(world, team), null, Dirty(team) && nameOk);

            // Tabs and the style carousel.
            var mid = Wp4Ui.BottomStrip(content, BottomHeight + Ui.Grid.Hit + Ui.Grid.Gap);
            var midRow = Wp4Ui.TopStrip(mid, Ui.Grid.Hit);
            var mc = Ui.Columns12(midRow, 7, 5);
            Ui.Segmented(Wp4Ui.ListIn(mc[0], 0), new[] { Loc.T("livery.presets"), Loc.T("livery.custom") }, _tab, i => { _tab = i; Shell.Refresh(); });
            Carousel(mc[1]);

            var bottom = Wp4Ui.BottomStrip(content, BottomHeight);
            if (_tab == 0) PresetRow(bottom);
            else CustomRows(bottom);
        }

        private void Apply(WorldState world, Team team)
        {
            string name = (_name ?? "").Trim();
            team.Colors.Primary = _colors[0];
            team.Colors.Secondary = _colors[1];
            team.Colors.Tertiary = _colors[2];
            team.Colors.Style = _style;
            if (name.Length >= 2 && name != team.Name)
            {
                team.Name = name;
                team.ShortName = name;
                var org = world.OrganisationOf(team);
                if (org != null && org.Owner == OwnerKind.Player && org.TeamIds.Count == 1) org.Name = name;
            }
            Shell.Toast(Loc.T("livery.applied"));
            Shell.Refresh();
        }

        /// <summary>Changes the draft and updates the preview (rebuilds the page for the selection marks).</summary>
        private void Changed()
        {
            Shell.Refresh();
        }

        private void Carousel(RectTransform area)
        {
            var cols = Ui.Columns12(area, 3, 6, 3);
            Arrow(cols[0], 0, () => { _style = (_style + TeamColors.StyleCount - 1) % TeamColors.StyleCount; Changed(); });
            Arrow(cols[2], 180, () => { _style = (_style + 1) % TeamColors.StyleCount; Changed(); });
            var label = Ui.Label(cols[1], Theme.Caps(Loc.T("livery.style", _style + 1)), 24, TextAnchor.UpperCenter, Theme.Text, FontStyle.BoldAndItalic);
            UiFactory.Place(label.rectTransform, 0, 0.42f, 1, 1);
            var name = Ui.Label(cols[1], Loc.T("livery.style." + _style), Theme.TextMin, TextAnchor.LowerCenter, Theme.TextDim);
            UiFactory.Place(name.rectTransform, 0, 0, 1, 0.46f);
        }

        /// <summary>A small-radius outline box with a chevron (the carousel's arrows).</summary>
        private static void Arrow(RectTransform area, float degrees, Action onTap)
        {
            var b = UiFactory.Button("Arrow", area, "", onTap, Theme.Alpha(Color.white, 0.05f), 22);
            UiFactory.Control(b.image);
            UiFactory.Ring(b.transform, Theme.Stroke);
            UiFactory.Stretch((RectTransform)b.transform);
            var ch = UiIcons.Chevron(b.transform, Theme.Text, degrees);
            ch.rectTransform.anchorMin = ch.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ch.rectTransform.sizeDelta = new Vector2(26, 26);
        }

        private void PresetRow(RectTransform area)
        {
            int active = PresetIndex();
            int n = Presets.Length;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                var cell = UiFactory.Rect("Preset", area);
                UiFactory.Place(cell, (float)i / n, 0, (float)(i + 1) / n, 1);
                cell.offsetMin = new Vector2(i == 0 ? 0 : 6, 8);
                cell.offsetMax = new Vector2(i == n - 1 ? 0 : -6, -8);
                var b = UiFactory.Button("Palette", cell, "", () =>
                {
                    Array.Copy(Presets[idx], _colors, 3);
                    Changed();
                }, Theme.Tile, 22);
                UiFactory.Surface(b.image);
                UiFactory.Stretch((RectTransform)b.transform);
                for (int k = 0; k < 3; k++)
                {
                    var chip = UiFactory.Decor("Colour", b.transform, UiFactory.ParseColor(Presets[i][k], Color.gray));
                    UiFactory.Place(chip.rectTransform, k / 3f, 0, (k + 1) / 3f, 1);
                    chip.rectTransform.offsetMin = new Vector2(k == 0 ? 8 : 2, 8);
                    chip.rectTransform.offsetMax = new Vector2(k == 2 ? -8 : -2, -8);
                }
                if (i != active) continue;
                UiFactory.Ring(b.transform, Theme.Text);
                Tick(b.transform);
            }
        }

        /// <summary>The active choice's mark: a small light tag with the check glyph at the top-right corner.</summary>
        private static void Tick(Transform parent)
        {
            var tag = Ui.Pill(parent, Glyph.Check, Theme.Text, Theme.Ink);
            var rt = tag.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(4, 6);
        }

        private void CustomRows(RectTransform area)
        {
            var rows = Ui.Rows(area, 1, 1);
            var slots = new[] { Loc.T("livery.slot.primary"), Loc.T("livery.slot.secondary"), Loc.T("livery.slot.accent") };
            var slotRow = Wp4Ui.ListIn(rows[0], 0);
            Ui.Segmented(slotRow, slots, _slot, i => { _slot = i; Shell.Refresh(); });
            // A colour chip before each slot's label.
            int k = 0;
            foreach (Transform seg in slotRow.GetChild(0))
            {
                if (seg.name != "Segment" || k > 2) continue;
                var t = seg.GetComponentInChildren<Text>();
                t.rectTransform.offsetMin = new Vector2(48, t.rectTransform.offsetMin.y);
                var chip = UiFactory.Decor("Chip", seg, UiFactory.ParseColor(_colors[k], Color.gray));
                UiFactory.Control(chip);
                chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = new Vector2(0, 0.5f);
                chip.rectTransform.pivot = new Vector2(0, 0.5f);
                chip.rectTransform.anchoredPosition = new Vector2(12, 3);
                chip.rectTransform.sizeDelta = new Vector2(28, 28);
                UiFactory.Ring(chip.transform, Theme.Stroke);
                k++;
            }

            int n = Swatches.Length;
            for (int i = 0; i < n; i++)
            {
                string hex = Swatches[i];
                var cell = UiFactory.Rect("Swatch", rows[1]);
                UiFactory.Place(cell, (float)i / n, 0, (float)(i + 1) / n, 1);
                cell.offsetMin = new Vector2(i == 0 ? 0 : 3, 0);
                cell.offsetMax = new Vector2(i == n - 1 ? 0 : -3, 0);
                var b = UiFactory.Button("Swatch", cell, "", () =>
                {
                    _colors[_slot] = hex;
                    Changed();
                }, UiFactory.ParseColor(hex, Color.gray), 22);
                UiFactory.Control(b.image);
                UiFactory.Stretch((RectTransform)b.transform);
                if (Eq(hex, _colors[_slot])) UiFactory.Ring(b.transform, Theme.Text);
                else UiFactory.Ring(b.transform, Theme.Edge);
            }
        }
    }
}
