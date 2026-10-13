using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// Small building blocks shared by the team, bike, finance and market pages on a phone: a back row for
    /// detail views, a compact stepper (label and value with small +/- buttons on one row) and the rider attribute
    /// summaries used by the rider profile and the market.
    /// </summary>
    internal static class MobileParts
    {
        /// <summary>The rider qualities shown as bars in a profile; the rest stay one tap deeper.</summary>
        public static readonly string[] KeyAttributes = { "RaceSkill", "QualifyingSkill", "Consistency", "WetSkill", "Overtaking", "TireManagement" };

        /// <summary>
        /// Back button for a full-page detail view, plus up to two extra actions on the same row. Obsolete in new
        /// layouts: put the back chevron in the card's header strip (Ui.Card onBack).
        /// </summary>
        public static void BackRow(Transform list, string label, System.Action back, params UiAct[] extra)
        {
            var row = UiFactory.Row(list, Ui.Grid.Hit);
            int n = extra.Length + 1;
            var back0 = UiFactory.Rect("Back", row);
            UiFactory.Place(back0, 0, 0, 1f / n, 1, 3);
            var b = Ui.Secondary(back0, label, back);
            var t = b.GetComponentInChildren<UnityEngine.UI.Text>();
            t.rectTransform.offsetMin = new Vector2(44, 2);
            var icon = UiIcons.Icon(b.transform, UiIcons.Back, Theme.Text);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, 0.5f);
            icon.rectTransform.sizeDelta = new Vector2(22, 22);
            icon.rectTransform.anchoredPosition = new Vector2(26, 0);
            for (int i = 0; i < extra.Length; i++)
            {
                var a = UiFactory.Rect("Action", row);
                UiFactory.Place(a, (float)(i + 1) / n, 0, (float)(i + 2) / n, 1, 3);
                if (extra[i].Primary || extra[i].Selected) Ui.SecondaryAccent(a, extra[i].Key, extra[i].Value);
                else Ui.Secondary(a, extra[i].Key, extra[i].Value);
            }
        }

        /// <summary>
        /// A value with step buttons (salary, price...). Two steps are the minus and plus of <see cref="Ui.Stepper"/>;
        /// other counts keep every step as a secondary button on the row (no action is dropped). New code calls
        /// Ui.Stepper (±5% per tap) directly.
        /// </summary>
        public static void Stepper(Transform list, string label, string value, params UiAct[] steps)
        {
            if (steps.Length == 2)
            {
                Ui.Stepper(list, label, value, steps[0].Value, steps[1].Value);
                return;
            }
            var row = UiFactory.Row(list, Ui.Grid.Row);
            var l = UiFactory.Label("Label", row, Theme.Dim(Theme.Caps(label)) + "\n<b>" + value + "</b>", Theme.FontSmall, TextAnchor.MiddleLeft);
            l.supportRichText = true;
            Ui.Fit(l, Theme.FontSmall);
            UiFactory.Place(l.rectTransform, 0, 0, 0.4f, 1);
            l.rectTransform.offsetMin = new Vector2(4, 0);
            for (int i = 0; i < steps.Length; i++)
            {
                float w = 0.6f / steps.Length;
                var a = UiFactory.Rect("Step", row);
                UiFactory.Place(a, 0.4f + w * i, 0, 0.4f + w * (i + 1), 1, 3);
                Ui.Secondary(a, steps[i].Key, steps[i].Value);
            }
        }

        /// <summary>Bars for the key qualities of a rider, as the team perceives them.</summary>
        public static void KeyBars(Transform card, WorldState world, string teamId, Rider r)
        {
            foreach (var a in KeyAttributes)
            {
                double v = Fmt.Estimate(world, teamId, r, a);
                Ui.Bar(card, Loc.T("attr." + a), v / 100.0, Fmt.Attr(world, teamId, r, a), Ui.RatingColor(v), 40);
            }
        }

        /// <summary>Every attribute in two pairs of columns (name, value).</summary>
        public static void AttributeTable(Transform card, WorldState world, string teamId, Rider r)
        {
            var names = RiderAttributes.Names;
            var widths = new[] { 3f, 1f, 3f, 1f };
            for (int i = 0; i < names.Length; i += 2)
            {
                var cells = new string[4];
                for (int k = 0; k < 2 && i + k < names.Length; k++)
                {
                    cells[k * 2] = Theme.Dim(Loc.T("attr." + names[i + k]));
                    cells[k * 2 + 1] = Fmt.Attr(world, teamId, r, names[i + k]);
                }
                UiFactory.TableRow(card, 40, widths, cells);
            }
        }

        /// <summary>Makes a whole table row tappable (a list entry that opens its detail).</summary>
        public static void Tappable(RectTransform row, System.Action onTap)
        {
            var img = row.GetComponent<UnityEngine.UI.Image>();
            if (img == null)
            {
                img = row.gameObject.AddComponent<UnityEngine.UI.Image>();
                img.color = new Color(1, 1, 1, 0.001f);
            }
            img.raycastTarget = true;
            var b = row.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() =>
            {
                GameAudio.ClickSound();
                onTap();
            });
        }
    }
}
