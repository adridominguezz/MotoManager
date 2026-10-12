#if UNITY_EDITOR || DEVELOPMENT_BUILD
using MotoManager.App.Race;
using MotoManager.Core.Season;
using UnityEngine;
using UnityEngine.UI;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Testing tools, compiled only into the editor and development builds (never into a release): a small "DBG"
    /// button on the race screen opens weather, crash, failure and tyre controls for the followed rider.
    /// </summary>
    public static class DebugRace
    {
        public static void Attach(RectTransform root, RaceView view)
        {
            GameObject panel = null;
            var toggle = UiFactory.Button("Debug", root, "DBG", () => { if (panel != null) panel.SetActive(!panel.activeSelf); }, new Color(0.5f, 0.1f, 0.5f, 0.85f), Theme.FontSmall - 6);
            UiFactory.Place((RectTransform)toggle.transform, 0.005f, 0.42f, 0.05f, 0.48f);
            var bg = UiFactory.Panel("DebugPanel", root, new Color(0.08f, 0.02f, 0.1f, 0.92f));
            UiFactory.Place(bg.rectTransform, 0.055f, 0.3f, 0.3f, 0.6f);
            panel = bg.gameObject;
            var v = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(8, 8, 8, 8);
            v.spacing = 6;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = true;
            UiFactory.ButtonRow(bg.transform, 44,
                UiFactory.Act("Rain", () => view.Sim.Weather.DebugForceRain(0.6)),
                UiFactory.Act("Dry", () => view.Sim.Weather.DebugForceRain(0)),
                UiFactory.Act("Natural", () => view.Sim.Weather.DebugForceRain(-1)));
            UiFactory.ButtonRow(bg.transform, 44,
                UiFactory.Act("Crash", () => view.Sim.DebugCrash(view.SelectedRider)),
                UiFactory.Act("Failure", () => view.Sim.DebugFailure(view.SelectedRider)),
                UiFactory.Act("Tyres 90%", () => view.Sim.DebugTireWear(view.SelectedRider, 0.9)));
            UiFactory.ButtonRow(bg.transform, 44,
                UiFactory.Act("x16", () => view.SetSpeed(16)),
                UiFactory.Act("x1", () => view.SetSpeed(1)));
            panel.SetActive(false);
        }
    }

    /// <summary>Management testing tools (development builds): money and development.</summary>
    public static class DebugManagement
    {
        public static UiAct[] Actions(GameSession session, System.Action refresh)
        {
            return new[]
            {
                UiFactory.Act("DBG +5M", () => { DebugTools.AddMoney(session.Game.World, 5e6); refresh(); }),
                UiFactory.Act("DBG R&D", () => { DebugTools.CompleteDevelopment(session.Game.World, session.Game.Db); refresh(); })
            };
        }
    }
}
#endif
