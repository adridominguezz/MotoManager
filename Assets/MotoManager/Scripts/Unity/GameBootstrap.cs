using MotoManager.App.UI;
using UnityEngine;

namespace MotoManager.App
{
    /// <summary>
    /// Entry point. Runs automatically after the first scene loads, so the project works with an empty scene
    /// (no prefabs or scene wiring to keep in sync with code).
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (GameSession.Instance != null) return;
            Application.runInBackground = false;
            Input.multiTouchEnabled = true;
            var session = GameSession.Create();
            BootScreen.Create(session);
        }
    }
}
