module SyncOriginTracker

open System

type SyncOriginTracker() =
    let mutable lastSyncedTime = DateTime.MinValue
    let mutable isApplying = false

    member _.MarkSyncedNow() =
        lastSyncedTime <- DateTime.Now

    member _.WasChangedByUser(lastModelChange: DateTime, bufferMs: int) =
        lastModelChange > lastSyncedTime.AddMilliseconds(float bufferMs)

    member _.MarkSyncStart() =
        isApplying <- true

    member _.MarkSyncEnd() =
        isApplying <- false

    member _.IsApplyingSync =
        isApplying
