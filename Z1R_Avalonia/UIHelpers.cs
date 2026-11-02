// Ported from Z1R_WPF/UIHelpers.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
using System;
using Avalonia.Threading;

namespace Z1R_Avalonia;

public static class UIHelpers
{
    // (F#: `recentThunk` is a `(unit -> unit) option`; here an Action, null for None)
    public class NotTooFrequently
    {
        private Action recentThunk = null;
        public NotTooFrequently(TimeSpan timespan)
        {
            var timer = new DispatcherTimer();
            timer.Interval = timespan;
            timer.Tick += (_, _) =>
            {
                if (recentThunk != null)
                {
                    recentThunk();
                    recentThunk = null;
                }
            };
            timer.Start();
        }
        public void SendThunk(Action thunk) =>
            recentThunk = thunk;
    }
}
