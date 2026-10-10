using System;
using System.Threading;

namespace MotoManager.App
{
    /// <summary>
    /// Runs heavy simulation work (loading data, simulating the other categories of an event, season end)
    /// on a worker thread so the UI keeps rendering. Poll <see cref="IsDone"/> from Update.
    /// The core is deterministic and single-threaded: only one task may touch the world at a time.
    /// </summary>
    public class BackgroundTask
    {
        private readonly Thread _thread;
        private volatile bool _done;
        public Exception Error { get; private set; }
        public string Label { get; private set; }
        public bool IsDone { get { return _done; } }

        private BackgroundTask(string label, Action work)
        {
            Label = label;
            _thread = new Thread(() =>
            {
                try
                {
                    work();
                }
                catch (Exception e)
                {
                    Error = e;
                }
                finally
                {
                    _done = true;
                }
            });
            _thread.IsBackground = true;
            // Simulation recursion is shallow but JSON (de)serialisation of big saves likes some stack.
            _thread.Name = "MotoManager " + label;
        }

        public static BackgroundTask Run(string label, Action work)
        {
            var task = new BackgroundTask(label, work);
            task._thread.Start();
            return task;
        }
    }
}
