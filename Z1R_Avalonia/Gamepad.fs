module Gamepad

// STUB: the WPF tracker reads the gamepad with SharpDX.DirectInput (Windows only). The left shoulder button is push-to-talk
// for speech recognition. Until a cross-platform gamepad reader is added (planned: SDL via Silk.NET), no gamepad is ever found.

open System

let mutable private LeftShoulderButtonIsDown = 0

let mutable LeftShoulderButtonMostRecentRelease = DateTime.Now   // lack of threadsafety might make it inaccurate/stale, but that's ok

let IsLeftShoulderButtonDown() =
    System.Threading.Volatile.Read(&LeftShoulderButtonIsDown) = 1

let ControllerFailureEvent = new Event<exn>()

let Initialize() =
    Platform.stub "gamepad"
    printfn "gamepad not found"
    false
