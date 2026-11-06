// Ported from Z1R_WPF/Debouncer.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
// (F#: `module Debouncer` holding `type Debouncer`. C# can't nest a class in a class of the same name, so, as PLAN.md says, it's one class `Debouncer`;
// WPF's `Debouncer.Debouncer(ms)` becomes `new Debouncer(ms)`.)
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Z1R_Avalonia;

public class Debouncer
{
    private readonly int delayMs;
    private CancellationTokenSource currentCts = null;   // (F#: an option; null for None)
    public Debouncer(int delayMs) { this.delayMs = delayMs; }

    public void Trigger(Action action)
    {
        currentCts?.Cancel(); currentCts?.Dispose();   // (disposed so the timers behind cancelled waits are released)
        var newCts = new CancellationTokenSource();
        currentCts = newCts;

        // (F#: `async { ... } |> Async.Start`, which runs on the thread pool, as Task.Run does)
        Task.Run(async () =>
        {
            await Task.Delay(delayMs);
            if (!newCts.IsCancellationRequested)
                action();
        });
    }
}
