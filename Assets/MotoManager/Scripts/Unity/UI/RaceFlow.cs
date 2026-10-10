using System;
using MotoManager.App.Race;
using MotoManager.Core.Race;
using MotoManager.Core.Season;
using UnityEngine;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Plays the next event: the player's practice and qualifying run on the worker thread, every race/sprint of the
    /// player's category is shown in the race view, then the rest of the event (other categories, economy, news)
    /// is completed in the background.
    /// </summary>
    public class RaceFlow
    {
        private readonly GameSession _session;
        private readonly Action<string> _onDone;
        private readonly GameObject _menuToHide;
        private LiveEvent _event;
        private string _summary = "";

        public RaceFlow(GameSession session, GameObject menuToHide, Action<string> onDone)
        {
            _session = session;
            _menuToHide = menuToHide;
            _onDone = onDone;
        }

        public void Start()
        {
            var game = _session.Game;
            _session.Run("Preparing the weekend", () =>
            {
                _event = game.BeginNextEvent();
                AdvanceToNextRace();
            }, ShowNextRaceOrFinish);
        }

        /// <summary>Worker thread: runs practice/qualifying/warm-up of the player's weekend until a race is due.</summary>
        private void AdvanceToNextRace()
        {
            var weekend = _event != null ? _event.PlayerWeekend : null;
            if (weekend == null) return;
            while (!weekend.IsComplete && !weekend.CurrentIsRace) weekend.RunCurrentHeadless();
        }

        private void ShowNextRaceOrFinish()
        {
            if (_event == null)
            {
                _onDone("The season is over.");
                return;
            }
            var weekend = _event.PlayerWeekend;
            if (weekend != null && !weekend.IsComplete && weekend.CurrentIsRace)
            {
                if (_menuToHide != null) _menuToHide.SetActive(false);
                PreRaceDialog.Show(weekend, () =>
                {
                    var sim = weekend.CreateRaceSimulation(_session.Db.Tuning.VisualTimeStep);
                    string title = _event.Plan.Round.Name + " · " + weekend.CurrentSession.Name;
                    RaceView.Create(sim, title, OnRaceClosed);
                });
                return;
            }
            CompleteEvent();
        }

        private void OnRaceClosed(RaceSimulation sim)
        {
            var weekend = _event.PlayerWeekend;
            if (!sim.IsFinished) sim.RunToEnd();
            var result = weekend.CompleteRace(sim);
            if (result.Classification.Count > 0)
            {
                var winner = _session.Game.World.GetRider(result.Classification[0].RiderId);
                _summary += result.SessionName + ": " + (winner != null ? winner.FullName : "?") + "\n";
            }
            _session.Run("Next session", AdvanceToNextRace, ShowNextRaceOrFinish);
        }

        private void CompleteEvent()
        {
            var game = _session.Game;
            var ev = _event;
            _session.Run("Simulating the rest of the event", () => game.CompleteEvent(ev), () =>
            {
                if (_menuToHide != null) _menuToHide.SetActive(true);
                _session.Save();
                _onDone("<b>" + ev.Plan.Round.Name + "</b>\n" + _summary);
            });
        }
    }
}
