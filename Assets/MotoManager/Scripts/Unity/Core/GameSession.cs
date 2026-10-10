using System;
using System.Collections.Generic;
using MotoManager.Core.Data;
using MotoManager.Core.Season;
using UnityEngine;

namespace MotoManager.App
{
    /// <summary>
    /// Lives for the whole app. Owns the static database and the current game, and serialises access to them:
    /// heavy work runs on one background task at a time while the UI shows progress.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        public GameDatabase Db { get; private set; }
        public GameController Game { get; private set; }
        public List<string> DataErrors { get; private set; }
        public string LoadError { get; private set; }

        public bool IsBusy { get { return _task != null; } }
        public string BusyLabel { get { return _task != null ? _task.Label : null; } }

        public event Action DataLoaded;
        public event Action GameChanged;

        private BackgroundTask _task;
        private Action _onDone;
        private Action<Exception> _onError;

        public static GameSession Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("GameSession");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameSession>();
            go.AddComponent<FrameRateGovernor>();
            UI.Loc.Install();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void LoadData()
        {
            if (Db != null || IsBusy) return;
            // Resources must be read on the main thread; JSON parsing happens on the worker.
            var source = new ResourcesDataSource().Preload();
            GameDatabase db = null;
            List<string> errors = null;
            Run(UI.Loc.T("busy.loading"), () =>
            {
                db = DataLoader.Load(source);
                errors = db.Validate();
            }, () =>
            {
                Db = db;
                DataErrors = errors;
                foreach (var e in errors) Debug.LogWarning("Data: " + e);
                if (DataLoaded != null) DataLoaded();
            }, e => LoadError = e.Message);
        }

        public void NewGame(string playerTeamId, ulong seed)
        {
            GameController game = null;
            Run(UI.Loc.T("busy.creating"), () =>
            {
                game = GameController.NewGame(Db, new NewGameOptions { Seed = seed, PlayerTeamId = playerTeamId });
            }, () => SetGame(game));
        }

        /// <summary>
        /// Creates the 2026 world with nobody managing yet: the career start screens use it to show real job offers,
        /// sponsors and category costs before the player commits.
        /// </summary>
        public void PrepareCareer(ulong seed, Action<GameController> ready)
        {
            GameController game = null;
            Run(UI.Loc.T("busy.creating"), () =>
            {
                game = GameController.NewGame(Db, new NewGameOptions { Seed = seed });
            }, () => ready(game));
        }

        /// <summary>The player has a team (hired or founded): the career begins.</summary>
        public void StartCareer(GameController game)
        {
            SetGame(game);
        }

        public void Continue()
        {
            GameController game = null;
            Run(UI.Loc.T("busy.loadingSave"), () =>
            {
                var world = SaveManager.Load(SaveManager.AutoSlot);
                if (world != null) game = new GameController(Db, world);
            }, () => SetGame(game));
        }

        public void Save()
        {
            if (Game == null) return;
            var world = Game.World;
            Run(UI.Loc.T("busy.saving"), () => SaveManager.Write(SaveManager.AutoSlot, SaveManager.Serialize(world)), null);
        }

        /// <summary>Runs work that touches the world on the background thread; callbacks come back on the main thread.</summary>
        public bool Run(string label, Action work, Action onDone, Action<Exception> onError = null)
        {
            if (_task != null)
            {
                Debug.LogWarning("Busy with " + _task.Label + ", ignoring " + label);
                return false;
            }
            _onDone = onDone;
            _onError = onError;
            _task = BackgroundTask.Run(label, work);
            _taskStarted = Time.unscaledTime;
            return true;
        }

        private void SetGame(GameController game)
        {
            Game = game;
            if (GameChanged != null) GameChanged();
        }

        private float _taskStarted;

        private void Update()
        {
            // Loading screen for anything that takes a moment (saving runs quietly).
            if (_task != null && !_task.IsDone && _task.Label != UI.Loc.T("busy.saving") && Time.unscaledTime - _taskStarted > 0.25f && !UI.LoadingOverlay.IsShown)
                UI.LoadingOverlay.Show(_task.Label);
            if (_task == null || !_task.IsDone) return;
            UI.LoadingOverlay.Hide();
            var task = _task;
            var done = _onDone;
            var error = _onError;
            _task = null;
            _onDone = null;
            _onError = null;
            if (task.Error != null)
            {
                Debug.LogException(task.Error);
                if (error != null) error(task.Error);
                return;
            }
            if (done != null) done();
        }

        private void OnApplicationPause(bool paused)
        {
            // Android may kill a paused app: keep the career safe.
            if (paused && Game != null && !IsBusy)
                SaveManager.Write(SaveManager.AutoSlot, SaveManager.Serialize(Game.World));
        }
    }
}
