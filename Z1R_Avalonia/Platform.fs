module Platform

// Everything that differs between Windows, macOS and Linux goes through this module, so the ported WPF files stay
// platform-neutral. Anything here that is still a stub is listed under "Stubs" in docs/avalonia-port/PROGRESS.md
// and has to be replaced before parity.

open System
open System.Runtime.InteropServices

let isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
let isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
let isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)

// stubs log once per feature, so a missing feature shows up in the console without flooding it
let private reportedStubs = System.Collections.Generic.HashSet<string>()
let stub(feature:string) =
    let isNew = lock reportedStubs (fun () -> reportedStubs.Add(feature))
    if isNew then
        printfn "[platform stub] %s is not implemented on this platform yet" feature

////////////////////////////////////////////////////////////
// files

/// Folder the app was installed to: read-only files that ship with the app (sounds, README, ShowRunCustomImages).
let AppDir() = AppContext.BaseDirectory

/// Folder for files the user or the app writes: settings, HotKeys.txt, crash logs, ShowRunCustom.txt, ExtraIcons, overworld tile cache.
/// On Windows this is the app folder, exactly like the WPF tracker. On macOS a signed .app can't be written to, so it's
/// ~/Library/Application Support/ZoraTracker; on Linux it's $XDG_CONFIG_HOME/ZoraTracker (default ~/.config/ZoraTracker).
/// (The macOS/Linux location is waiting on TheGobOne's sign-off: risk 10 in PLAN.md.)
let UserDataDir() =
    let dir =
        if isWindows then
            AppContext.BaseDirectory
        elif isMacOS then
            IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "ZoraTracker")
        else
            let xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
            let baseDir = if String.IsNullOrEmpty(xdg) then IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config") else xdg
            IO.Path.Combine(baseDir, "ZoraTracker")
    IO.Directory.CreateDirectory(dir) |> ignore
    dir

type Files =
    /// A path to a user file (see UserDataDir). Pass path segments separately; never use backslashes.
    static member User([<ParamArray>] parts:string[]) = IO.Path.Combine(Array.append [| UserDataDir() |] parts)
    /// A path to a file that ships with the app (see AppDir).
    static member App([<ParamArray>] parts:string[]) = IO.Path.Combine(Array.append [| AppDir() |] parts)

/// Open a folder, file or URL with the system's default handler (Explorer / Finder / xdg-open).
let OpenWithShell(target:string) =
    try
        if isWindows then
            Diagnostics.Process.Start(new Diagnostics.ProcessStartInfo(target, UseShellExecute=true)) |> ignore
        elif isMacOS then
            Diagnostics.Process.Start("open", [| target |]) |> ignore
        else
            Diagnostics.Process.Start("xdg-open", [| target |]) |> ignore
    with e ->
        printfn "could not open '%s': %s" target e.Message

/// Path of the running executable, for restarting the app.
let ExecutablePath() = Environment.ProcessPath

////////////////////////////////////////////////////////////
// mouse

module private Win =
    [<DllImport("User32.dll")>]
    extern bool SetCursorPos(int X, int Y)
    [<DllImport("User32.dll")>]
    extern void mouse_event(uint32 dwFlags, int dx, int dy, uint32 cButtons, int dwExtraInfo)
    let MOUSEEVENTF_LEFTDOWN     = 0x02u
    let MOUSEEVENTF_LEFTUP       = 0x04u
    let MOUSEEVENTF_RIGHTDOWN    = 0x08u
    let MOUSEEVENTF_RIGHTUP      = 0x10u
    let MOUSEEVENTF_MIDDLEDOWN   = 0x20u
    let MOUSEEVENTF_MIDDLEUP     = 0x40u
    let MOUSEEVENTF_WHEEL        = 0x800u
    let WHEEL_DELTA = 120

module private Mac =
    [<Struct; StructLayout(LayoutKind.Sequential)>]
    type CGPoint =
        val mutable X : float
        val mutable Y : float
        new(x, y) = { X = x; Y = y }
    [<Literal>]
    let CG = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics"
    [<Literal>]
    let CF = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation"
    [<DllImport(CG)>]
    extern int CGWarpMouseCursorPosition(CGPoint newCursorPosition)
    [<DllImport(CG)>]
    extern int CGAssociateMouseAndMouseCursorPosition(int connected)
    [<DllImport(CG)>]
    extern nativeint CGEventCreate(nativeint source)
    [<DllImport(CG)>]
    extern CGPoint CGEventGetLocation(nativeint ev)
    [<DllImport(CG)>]
    extern nativeint CGEventCreateMouseEvent(nativeint source, uint32 mouseType, CGPoint mouseCursorPosition, uint32 mouseButton)
    // the non-variadic version: variadic P/Invoke isn't safe on Apple Silicon
    [<DllImport(CG)>]
    extern nativeint CGEventCreateScrollWheelEvent2(nativeint source, uint32 units, uint32 wheelCount, int wheel1, int wheel2, int wheel3)
    [<DllImport(CG)>]
    extern void CGEventPost(uint32 tap, nativeint ev)
    [<DllImport(CF)>]
    extern void CFRelease(nativeint cf)
    let kCGHIDEventTap = 0u
    let kCGEventLeftMouseDown, kCGEventLeftMouseUp = 1u, 2u
    let kCGEventRightMouseDown, kCGEventRightMouseUp = 3u, 4u
    let kCGEventMouseMoved = 5u
    let kCGEventOtherMouseDown, kCGEventOtherMouseUp = 25u, 26u
    let kCGMouseButtonLeft, kCGMouseButtonRight, kCGMouseButtonCenter = 0u, 1u, 2u
    let kCGScrollEventUnitLine = 1u
    let cursorLocation() =
        let ev = CGEventCreate(0n)
        let p = CGEventGetLocation(ev)
        CFRelease(ev)
        p
    let post(ev:nativeint) =
        if ev <> 0n then
            CGEventPost(kCGHIDEventTap, ev)
            CFRelease(ev)
    let click(down, up, button) =
        let p = cursorLocation()
        post(CGEventCreateMouseEvent(0n, down, p, button))
        post(CGEventCreateMouseEvent(0n, up, p, button))

/// Moves the mouse cursor. (x, y) are screen coordinates in physical pixels, as returned by Avalonia's PointToScreen;
/// desktopScaling is the window's DesktopScaling. Returns false where cursor warping isn't supported (no warp happened).
let WarpCursor(x:int, y:int, desktopScaling:float) =
    if isWindows then
        Win.SetCursorPos(x, y) |> ignore
        true
    elif isMacOS then
        // Avalonia reports macOS screen coordinates in pixels; CoreGraphics wants points (top-left origin, like Avalonia)
        let p = Mac.CGPoint(float x / desktopScaling, float y / desktopScaling)
        Mac.CGWarpMouseCursorPosition(p) |> ignore
        // re-associating the mouse cancels the ~0.25s in which macOS ignores mouse movement after a warp
        Mac.CGAssociateMouseAndMouseCursorPosition(1) |> ignore
        // a warp doesn't produce a mouse-moved event on macOS, but the app waits for one (to update hover state);
        // posting one works only once the user has granted Accessibility permission, and is silently dropped otherwise
        Mac.post(Mac.CGEventCreateMouseEvent(0n, Mac.kCGEventMouseMoved, p, Mac.kCGMouseButtonLeft))
        true
    else
        stub "cursor warping (Linux: XWarpPointer)"
        false

// Fake mouse input, sent at the cursor's current position (hotkeys can act as mouse buttons and the scroll wheel).
// On macOS these need Accessibility permission, without which they're silently dropped.
let LeftMouseClick() =
    if isWindows then Win.mouse_event(Win.MOUSEEVENTF_LEFTDOWN ||| Win.MOUSEEVENTF_LEFTUP, 0, 0, 0u, 0)
    elif isMacOS then Mac.click(Mac.kCGEventLeftMouseDown, Mac.kCGEventLeftMouseUp, Mac.kCGMouseButtonLeft)
    else stub "fake mouse clicks (Linux: XTest)"
let RightMouseClick() =
    if isWindows then Win.mouse_event(Win.MOUSEEVENTF_RIGHTDOWN ||| Win.MOUSEEVENTF_RIGHTUP, 0, 0, 0u, 0)
    elif isMacOS then Mac.click(Mac.kCGEventRightMouseDown, Mac.kCGEventRightMouseUp, Mac.kCGMouseButtonRight)
    else stub "fake mouse clicks (Linux: XTest)"
let MiddleMouseClick() =
    if isWindows then Win.mouse_event(Win.MOUSEEVENTF_MIDDLEDOWN ||| Win.MOUSEEVENTF_MIDDLEUP, 0, 0, 0u, 0)
    elif isMacOS then Mac.click(Mac.kCGEventOtherMouseDown, Mac.kCGEventOtherMouseUp, Mac.kCGMouseButtonCenter)
    else stub "fake mouse clicks (Linux: XTest)"
let ScrollWheelRotateUp() =
    if isWindows then Win.mouse_event(Win.MOUSEEVENTF_WHEEL, 0, 0, uint32 Win.WHEEL_DELTA, 0)
    elif isMacOS then Mac.post(Mac.CGEventCreateScrollWheelEvent2(0n, Mac.kCGScrollEventUnitLine, 1u, 1, 0, 0))
    else stub "fake scroll wheel (Linux: XTest)"
let ScrollWheelRotateDown() =
    if isWindows then Win.mouse_event(Win.MOUSEEVENTF_WHEEL, 0, 0, uint32 (-Win.WHEEL_DELTA), 0)
    elif isMacOS then Mac.post(Mac.CGEventCreateScrollWheelEvent2(0n, Mac.kCGScrollEventUnitLine, 1u, -1, 0, 0))
    else stub "fake scroll wheel (Linux: XTest)"

/// The Windows "show location of pointer when I press Ctrl" setting (mouse sonar). Only referenced from code the WPF
/// tracker has commented out; risk 8 in PLAN.md.
let SetMouseSonar(_enable:bool) =
    stub "mouse sonar"
    0

////////////////////////////////////////////////////////////
// sound

/// Plays a .wav file from the app folder. volume is 0.0-1.0.
let PlaySound(fileName:string, _volume:float) =
    stub (sprintf "sound playback (%s)" fileName)

/// The system "asterisk" sound (WPF: SystemSounds.Asterisk).
let SystemBeep() =
    stub "system beep"
