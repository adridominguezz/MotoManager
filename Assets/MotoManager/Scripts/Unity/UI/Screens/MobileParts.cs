using MotoManager.Core.World;
using UnityEngine;

namespace MotoManager.App.UI.Screens
{
    /// <summary>
    /// Small building blocks shared by the team, bike, finance and market pages on a phone: a "◄" back row for
    /// detail views, a compact stepper (label and value with small +/- buttons on one row) and the rider attribute
    /// summaries used by the rider profile and the market.
    /// </summary>
    internal static class MobileParts
    {
        /// <summary>The rider qualities shown as bars in a profile; the rest stay one tap deeper.</summary>
        public static readonly string[] KeyAttributes = { "RaceSkill", "QualifyingSkill", "Consistency", "WetSkill", "Overtaking", "TireManagement" };

        /// <summary>Back button for a full-page detail view, plus up to two extra actions on the same row.</summary>
        public static void BackRow(Transform list, string label, System.Action back, params UiAct[] extra)
        {
            var acts = new UiAct[extra.Length + 1];
            acts[0] = UiFactory.Act("◄  " + label, back);
            for (int i = 0; i < extra.Length; i++) acts[i + 1] = extra[i];
            UiFactory.ButtonRow(list, 50, acts);
        }

        /// <summary>
        /// One row: a label with its value on the left and small step buttons on the right (salary, price...).
        /// Keeps a value and its adjustments together instead of a separate row of buttons.
        /// </summary>
        public static void Stepper(Transform list, string label, string value, params UiAct[] steps)
        {
            var row = UiFactory.Row(list, UiScale.Touch(48));
            var l = UiFactory.Label("Label", row, Theme.Dim(label) + "\n<b>" + value + "</b>", Theme.FontSmall, TextAnchor.MiddleLeft);
            l.supportRichText = true;
            l.resizeTextForBestFit = true;
            l.resizeTextMinSize = Theme.FontSmall - 8;
            l.resizeTextMaxSize = Theme.FontSmall;
            UiFactory.Place(l.rectTransform, 0, 0, 0.4f, 1);
            l.rectTransform.offsetMin = new Vector2(6, 0);
            for (int i = 0; i < steps.Length; i++)
            {
                var b = UiFactory.Button("Step", row, steps[i].Key, steps[i].Value, steps[i].Color, Theme.FontSmall);
                float w = 0.6f / steps.Length;
                UiFactory.Place((RectTransform)b.transform, 0.4f + w * i, 0, 0.4f + w * (i + 1), 1, 3);
            }
        }

        /// <summary>Bars for the key qualities of a rider, as the team perceives them.</summary>
        public static void KeyBars(Transform card, WorldState world, string teamId, Rider r)
        {
            foreach (var a in KeyAttributes)
            {
                double v = Fmt.Estimate(world, teamId, r, a);
                Ui.Bar(card, Loc.T("attr." + a), v / 100.0, Fmt.Attr(world, teamId, r, a), Ui.RatingColor(v), 34);
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
                UiFactory.TableRow(card, 30, widths, cells);
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
