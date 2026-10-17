namespace Z1R_SharedInterop

type TrackerOptionsBridge =
    static let mutable bookForHelpfulHints = fun () -> false
    static let mutable doDoorInference = fun () -> false

    // Called by Z1R_Tracker at startup
    static member Initialize(bookHintsFunc: unit -> bool, doorInferFunc: unit -> bool) =
        bookForHelpfulHints <- bookHintsFunc
        doDoorInference <- doorInferFunc

    // Called by C# code in Z1R_Sync
    static member BookForHelpfulHints() = bookForHelpfulHints()
    static member DoDoorInference() = doDoorInference()

module RoomInteropBridge =

    open System

    type ApplyRoomStateFromSyncDelegate = delegate of int * int * int * bool * string * string * string * bool -> unit

    let mutable applyRoomStateFromSync: ApplyRoomStateFromSyncDelegate = null
