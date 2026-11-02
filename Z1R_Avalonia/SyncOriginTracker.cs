// Ported from Z1R_WPF/SyncOriginTracker.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
// (F#: `module SyncOriginTracker` holding `type SyncOriginTracker`. C# can't nest a class in a class of the same name, so, as PLAN.md says, it's one class
// `SyncOriginTracker`; WPF's `SyncOriginTracker.SyncOriginTracker()` becomes `new SyncOriginTracker()`.)
using System;

namespace Z1R_Avalonia;

public class SyncOriginTracker
{
    private DateTime lastSyncedTime = DateTime.MinValue;
    private bool isApplying = false;

    public void MarkSyncedNow() =>
        lastSyncedTime = DateTime.Now;

    public bool WasChangedByUser(DateTime lastModelChange, int bufferMs) =>
        lastModelChange > lastSyncedTime.AddMilliseconds((double)bufferMs);

    public void MarkSyncStart() =>
        isApplying = true;

    public void MarkSyncEnd() =>
        isApplying = false;

    public bool IsApplyingSync =>
        isApplying;
}
