# Avalonia port — plan

This work happens in the `tetraly/ZoraTracker` fork on the `avalonia-port` branch, with
TheGobOne's blessing. TheGobOne keeps developing `Z1R_WPF` as usual; this port follows him.

- Pinned WPF baseline: `9292b0b` (v17hotfxi, 2026-10-30)
- Progress, decisions and guesses are logged in [`PROGRESS.md`](./PROGRESS.md)

## Goal: complete feature parity

The goal is **complete feature parity** between the cross-platform Avalonia tracker
(`Z1R_Avalonia`) and the Windows WPF tracker (`Z1R_WPF`), so that the WPF version can be retired
and a single UI ships on Windows, macOS and Linux.

"Complete parity" means:

- **Every feature** of the WPF tracker, as of the latest WPF commit we've caught up to, exists and
  works in the Avalonia tracker: every window, menu, popup, option, hotkey, ZORA Extra,
  reminder, HUD, broadcast view and co-op feature. Nothing is dropped because it's hard.
- **Behaviour matches.** Clicks, hotkeys, mouse movement, menu placement, timing and defaults work
  the way they do in WPF. The WPF tracker is the reference; the old Avalonia prototype is not.
- **Look matches.** Same layout, sizes, sprites and colours, judged side by side against WPF.
- **Files are compatible.** Save files, settings, `HotKeys.txt`, `ShowRunCustom.txt` and the
  ExtraIcons folder written for the WPF tracker work unchanged in the Avalonia tracker.
- **It holds on every platform:** Windows, macOS (Apple Silicon and Intel) and Linux.

Stubs are a **temporary** step to keep the build moving. Parity isn't reached until every stub
has been replaced.

The only allowed exceptions are where an operating system makes identical behaviour impossible
(see [Risks](#risks-to-full-parity)). Each one needs a platform equivalent that TheGobOne has
explicitly approved, recorded under "Agreed platform deviations" in PROGRESS.md. Without his
sign-off it is a parity gap, not an exception.

The browser (WebAssembly) build is **not** part of the parity goal. It stays buildable, but
browsers can't do things like global hotkeys or cursor warping, so it's best effort.

## Language: C#

The cross-platform tracker is written in **C#**, not F#. C# is more common and easier to maintain, and the original
author only chose F# because it was a language he wanted to learn. Concretely:

- **New code is C#.** `Z1R_Avalonia` (the ported UI), the launchers and the support projects are C#.
- **Shared F# libraries stay F# for now, used unchanged:** the tracker model (`Z1R_Tracker_Core`, which compiles
  Gob's `Z1R_Tracker/*.fs`) and `Z1R_SharedInterop`. The WPF tracker uses them too, and Gob keeps changing them, so a C#
  copy would drift. Once WPF is retired they can be converted to C#.
- `Z1R_Avalonia_Prototype` (the old F# prototype) is no longer part of the desktop app. Only the browser (WebAssembly) host still starts it; it's dropped once that host starts the ported tracker.

**Mirroring an F# file in C#.** Each `Z1R_WPF/X.fs` becomes `Z1R_Avalonia/X.cs`, so a WPF diff can still be found and
applied:
- `module X` → `public static class X` in namespace `Z1R_Avalonia`, holding the same members **in the same order**,
  with the same names (including the original camelCase; they can be renamed once WPF is retired) and the same comments.
- Types defined inside the module → nested classes of `X`, so `X.SomeType` keeps its name. **Exception:** when a module
  `X` defines a type also named `X` (e.g. `DungeonRoomState.DungeonRoomState`), C# can't nest a class in a class of
  the same name. Then there's one class `X` at namespace level holding both the type's members and the module's values
  (as statics); the module's other types also go at namespace level. WPF's `X.X` becomes `X`, and `X.value` stays
  `X.value`. Don't turn `X` into a namespace.
- **Write C# that a C# developer would expect.** Don't use F#-only library types (`FSharpOption`, `FSharpChoice`,
  `FSharpEvent`, `FSharpList`, `FSharpFunc`) in our own code. Use the C# equivalent instead: `bool TryGetValue(key, out
  value)` or nullable for options, an enum or a small class hierarchy for unions and Choice types, C# `event`s (with a
  public `Raise…`/`Trigger…` method where other classes fire them), arrays/`List<T>`, and `Func`/`Action`. The only
  exceptions are values that come from or go to the F# model (`Z1R_Tracker_Core`), which keep their F# types.
- Top-level `let` values → `public static readonly` fields (`public static` for `let mutable`). C# runs static field
  initializers in textual order, as F# runs top-level bindings. A top-level `do` block becomes a private static method
  run from a field initializer at the same position (`private static readonly bool _init1 = init1();`), because a static
  constructor would run after all the fields instead.
- Functions → static methods. Curried or partially applied functions → normal parameters, or `Func<>`/`Action<>` where
  they're passed around. Tuples → C# value tuples. Our own `option` values → null or nullable. Model values from F#
  keep their F# types (`FSharpOption`, `FSharpList`, …).
- `async { }` → `async Task`. `Async.AwaitEvent`, `Async.StartImmediate` etc. → `TaskCompletionSource`, `await`,
  `Dispatcher.UIThread`; keep the same ordering and threading.
- Pattern matches → `switch` statements or expressions with the same cases in the same order.

## Ground rules

1. **Mirror WPF file-for-file.** Each `Z1R_WPF/X.fs` becomes `Z1R_Avalonia/X.cs` (see "Language: C#"), with the
   same names, the same members in the same order, and the same layout constants. Translate;
   don't redesign. This keeps TheGobOne's future diffs mappable onto the port and makes the port
   reviewable side by side.
2. **Port in the compile order of `Z1R_WPF.fsproj`.** C# doesn't care about file order, but F# files can only use
   files above them, so porting in that order means everything a file uses already exists.
3. **Never edit `Z1R_WPF/` or `Z1R_Tracker/` on this branch.** Those belong to TheGobOne. If shared
   logic needs to move into `Z1R_Tracker_Core`, record it in PROGRESS.md as a proposed upstream
   PR instead of doing it here.
4. **Build after every file:**
   `dotnet build Z1R_Avalonia_Desktop/Z1R_Avalonia_Desktop.csproj`
   A file isn't done until the whole solution builds with no new errors.
5. **One commit per ported file** (or per tightly coupled group), with a message like
   `port: DungeonUI.fs → DungeonUI.cs (WPF @ 9292b0b)`.
6. **Stub, don't guess, on platform-specific features** (see the table below). Stubs go behind a
   small interface in `Z1R_Avalonia/Platform.cs`, compile, do nothing (or log), and are listed in
   PROGRESS.md. Every stub is temporary and has to be replaced before parity.
7. **Never quietly drop or simplify a feature.** If something can't be ported faithfully, port as
   much as possible and log exactly what's missing in PROGRESS.md.
8. **Log every judgement call** in PROGRESS.md: anything translated loosely, any WPF behaviour
   with no direct Avalonia equivalent, anything that needs a human to look at it.
9. **No Windows-only assumptions.** Build paths with `Path.Combine` (never `\\`), resolve user
   files through one helper in `Platform.cs` (see "Where user files live" in Risks), and don't
   hard-code fonts that only exist on Windows.
10. **Keep the existing prototype buildable until it's replaced.** It lives in `Z1R_Avalonia_Prototype` (F#). The ported
    `Program.cs`/`WPFUI.cs` are the desktop app's entry point; the prototype is only used by the browser host now. Once that
    host starts the ported tracker, drop the prototype from the build.

## WPF → Avalonia translation cheat-sheet

| WPF | Avalonia |
|---|---|
| `System.Windows.Controls.*` | `Avalonia.Controls.*` |
| `System.Windows.Media.*` | `Avalonia.Media.*` |
| `Canvas.SetLeft/SetTop` | same |
| `MouseDown` / `MouseUp` / `MouseMove` | `PointerPressed` / `PointerReleased` / `PointerMoved` |
| `MouseEnter` / `MouseLeave` | `PointerEntered` / `PointerExited` |
| `MouseWheel` | `PointerWheelChanged` |
| `PreviewMouseDown`, `PreviewKeyDown`, … (tunnelling) | `AddHandler(..., RoutingStrategies.Tunnel)` |
| `e.ChangedButton = MouseButton.Right` | `e.GetCurrentPoint(ctrl).Properties.IsRightButtonPressed` (on press); `e.InitialPressMouseButton` (on release) |
| `MouseButton.Middle`, `XButton1` | `IsMiddleButtonPressed`, `IsXButton1Pressed` |
| `Visibility.Collapsed/Hidden` | `IsVisible = false` (Hidden has no direct equivalent: use `Opacity = 0` + `IsHitTestVisible = false`) |
| `BitmapSource` / `WriteableBitmap` | `Avalonia.Media.Imaging.Bitmap` / `WriteableBitmap` |
| `RenderTargetBitmap` | `Avalonia.Media.Imaging.RenderTargetBitmap` |
| `VisualBrush` | `VisualBrush` |
| `BlurEffect`, `DropShadowEffect` | `BlurEffect`, `DropShadowEffect` (Avalonia 11) |
| `DrawingVisual` | a custom control overriding `Render` |
| `ToolTip = x` | `ToolTip.SetTip(ctrl, x)` |
| `Dispatcher.Invoke` | `Dispatcher.UIThread.Post` / `InvokeAsync` |
| `RenderOptions.SetBitmapScalingMode(..., NearestNeighbor)` | `RenderOptions.SetBitmapInterpolationMode(..., BitmapInterpolationMode.None)` |
| `Window.Topmost`, `AllowsTransparency` | `Topmost`, `TransparencyLevelHint` + `Background = Transparent` |
| `Popup` | `Avalonia.Controls.Primitives.Popup` |
| `Microsoft.Win32.OpenFileDialog` | `TopLevel.StorageProvider.OpenFilePickerAsync` |
| `System.Windows.Forms.Screen` | `Window.Screens` |
| embedded resource PNGs | `AvaloniaResource` in the fsproj, loaded via `avares://Z1R_Avalonia/Assets/...` (see `Z1R_Avalonia_Prototype`) |

## Platform-specific features

Everything in this table has to work on every platform before parity. The "initial port" column
is only about how to keep the build moving at first.

| Feature | WPF source | Initial port | Final solution |
|---|---|---|---|
| Sprite/bitmap processing (`System.Drawing.Bitmap`, `GetPixel/SetPixel/LockBits`, ~236 uses in `Graphics.fs`) | `Graphics.fs`, `OverworldMapTileCustomization.fs`, `UIComponents.fs`, `WPFUI.fs`, … | **Port for real** to SkiaSharp (`SKBitmap`), converting to Avalonia `Bitmap` at the edge. This is phase 0. | — |
| Cursor warping (`SetCursorPos`): menus open under the mouse, keyboard/gamepad navigation of menus moves the cursor | `Graphics.fs`; used throughout `CustomComboBoxes.fs`, `DungeonPopups.fs`, … | Stub (`Platform.WarpCursor`) | `CGWarpMouseCursorPosition` on macOS, `XWarpPointer` on X11 |
| Fake mouse clicks and scrolls from hotkeys (`mouse_event`: left/middle/right click, wheel) | `Graphics.fs`; used by `CustomComboBoxes.fs`, `UIComponents.fs`, `OverworldItemGridUI.fs`, `WPFUI.fs` | Stub | `CGEventPost` on macOS, XTest on X11 |
| "Show mouse location" ring (`SystemParametersInfo` mouse sonar + `SendInput` Ctrl press) | `Graphics.fs` | Stub | Draw our own ring in a transparent overlay window (there's no system feature on macOS/Linux) |
| Global hotkeys (`RegisterHotKey`) | `Program.fs`, `HotKeys.fs` | Port the in-window hotkeys; stub global registration | SharpHook |
| Speech recognition (`System.Speech`), push-to-talk on the gamepad shoulder button | `SpeechRecognition.fs`, `OptionsMenu.fs` | Stub | Vosk with a restricted grammar, or a native helper per OS |
| Speech synthesis, voice choice (`SpeechSynthesizer`, `SelectVoice`) | `OptionsMenu.fs`, `Reminders.fs` | Stub (log the text) | `AVSpeechSynthesizer` on macOS, speech-dispatcher/espeak on Linux |
| Sound effects (`MediaPlayer`, `.wav`) and system beep (`SystemSounds.Asterisk`) | `Graphics.fs`, `Reminders.fs` | Stub | A small cross-platform audio library; bundle a sound for the beep |
| Gamepad (`SharpDX.DirectInput`) | `Gamepad.fs`, `SpeechRecognition.fs` | Stub | SDL via Silk.NET |
| Co-op sync: SignalR client, plus a local `Z1R_SignalRHost.exe` server tied to the app with a Windows Job Object | `CoopSync.fs`, `Dungeon.fs`, `Dungeon*Sync.fs`, `Z1R_Sync`, `Z1R_SignalRHost` | Port last | Retarget client and host to net10; ship the host per OS or run it in-process |
| Desktop shortcut (`IShellLink` `.lnk`) | `Program.fs` | Skip | macOS: the `.app` in Applications; Linux: a `.desktop` file |
| Opening folders/files/URLs (`Explorer.exe`, `Process.Start`) | `CustomComboBoxes.fs`, `ShowRunCustom.fs` | Port | `open` on macOS, `xdg-open` on Linux, via `Platform.cs` |
| Self-updater: downloads the release `.zip` and replaces the Windows build | `CustomComboBoxes.fs` | Port the "new version available" check; stub install | Per-OS updates (e.g. Velopack), with Mac/Linux builds published in releases |
| Console window hiding (`GetConsoleWindow`, `ShowWindow`) | `Program.fs` | Skip on non-Windows | — |

## Risks to full parity

Ordered from most to least likely to stop parity. Items marked **needs Gob** need TheGobOne's
decision or sign-off.

### Could make identical behaviour impossible on some platforms

1. **Linux under Wayland blocks core interactions.** Wayland deliberately stops apps from moving
   the cursor, sending fake clicks, grabbing global hotkeys, choosing their own window positions
   and staying on top. Avalonia currently runs on Linux through X11, so under Wayland it goes
   through XWayland, where cursor warping and global hotkeys are limited or don't work at all
   (to be confirmed by testing). Plain X11 sessions should be fine.
   *Mitigation:* treat X11 as the supported Linux setup and test on major Wayland desktops early.
   Where Wayland blocks something, the Linux deviation needs Gob's approval. **Needs Gob.**
2. **Mac trackpads have no middle button or thumb buttons.** The WPF tracker uses middle-click
   in about 10 places (including switching GRAB/MOVE) and the mouse thumb button (`XButton1`).
   The port will support these on a real mouse, but Mac laptop users without one can't reach
   those features.
   *Mitigation:* add a Mac-only alternative (e.g. Option-click for middle-click). That's an
   addition rather than a change, but it still needs Gob's approval. **Needs Gob.**
3. **macOS permissions for mouse and keyboard control.** Fake clicks and global hotkeys need the
   user to grant Accessibility / Input Monitoring permission in System Settings. Cursor warping
   probably doesn't, but macOS briefly ignores the mouse right after a warp unless that's turned
   off, which can make warp-heavy menus feel sluggish (to be confirmed). Without proper code
   signing (see 9), macOS forgets the permission after every update.
   *Mitigation:* a first-run explanation of the permission; test warp responsiveness in phase 0.
4. **Speech recognition can't be identical.** `System.Speech` recognises a fixed grammar offline.
   macOS speech recognition doesn't support strict grammars and needs a permission; Vosk does
   support them offline but needs a bundled model (~50 MB) and will recognise differently.
   Commands can work the same; accuracy and timing will differ.
   *Mitigation:* use the same command grammar everywhere and test with real players. **Needs Gob**
   to accept "same commands, different engine".
5. **Text can't look pixel-identical.** WPF uses Segoe UI by default plus Consolas and Courier
   New. They aren't installed on macOS/Linux, and Consolas can't legally be bundled. Substitute
   fonts have different widths, which can overflow tight layouts, and text rendering differs
   between systems. Sprites and pixel art can match exactly; text can only match closely.
   *Mitigation:* bundle open fonts with similar metrics and check every text-heavy screen side by
   side. **Needs Gob** to accept close-but-not-identical text.
6. **Voices differ per OS.** The reminder voice and the voice-picker list come from the OS, so
   the voices themselves differ, and a `PreferredVoice` saved on Windows won't exist on a Mac.
   *Mitigation:* fall back to the system default when the saved voice is missing.
7. **"Always on top" over full-screen apps on macOS.** HUDs and the minimap overlay are always-on-
   top transparent windows. On macOS, a normal always-on-top window doesn't appear over another
   app's full-screen Space (e.g. an emulator in full screen) without a native window setting
   Avalonia doesn't expose.
   *Mitigation:* a small native call per window on macOS; test with emulators in full screen.
8. **The "show mouse location" feature is a Windows system setting.** The tracker turns on the
   Windows mouse-sonar ring. macOS and Linux have nothing equivalent, so we'd draw our own, which
   will look a bit different. **Needs Gob.**

### Hard, but solvable

9. **Mac code signing and notarisation.** Without an Apple Developer ID ($99/year), Mac users get
   Gatekeeper warnings on first launch and lose their Accessibility permission after each update.
   Someone has to own the account and the signing keys. **Needs Gob** (whose account?).
10. **Where user files live.** The WPF tracker keeps settings, `HotKeys.txt`, crash logs,
    `ShowRunCustom.txt`, the ExtraIcons folder and the co-op host next to the `.exe`. Inside a
    signed macOS `.app` those files can't be written (and editing them breaks the signature), so
    on macOS they have to live in `~/Library/Application Support/ZoraTracker`. Players who edit
    `HotKeys.txt` will find it in a different place, and the docs need updating.
    *Mitigation:* an "open settings folder" button and a one-time import of existing files.
    **Needs Gob** to agree the location.
11. **The self-updater only knows the Windows `.zip`.** Mac and Linux need their own update
    mechanism, and the release process has to publish Mac and Linux builds.
12. **Co-op runs a local server program.** It launches `Z1R_SignalRHost.exe` (built on
    netcoreapp3.1, which is out of support) and ties it to the tracker with a Windows Job Object.
    The host must be retargeted and shipped for each OS, or run inside the tracker.
13. **Windows-only paths in the code**, e.g. `Overworld\quest.%d…` and `TrimEnd('\\')`. Easy to
    fix, easy to miss; rule 9 and a grep sweep at each phase cover it.
14. **Performance on low-end machines.** The original author's notes already include tips for
    "very low-end machines". Avalonia renders differently from WPF and needs measuring, not
    assuming.

### Process risks

15. **Moving target.** TheGobOne ships a beta almost every day, often ~1,000 lines a week, all in
    WPF. If catching up falls behind, parity keeps slipping.
    *Mitigation:* the mirrored structure plus the catch-up routine below; catch up at least once a
    week.
16. **No automated tests.** The repo has no test suite, so parity can only be checked by hand
    against the WPF tracker.
    *Mitigation:* add a few automated checks where it's cheap, such as save files written by WPF
    loading in Avalonia and the settings round-trip. Use a written checklist and side-by-side
    screenshots for the rest.
17. **Checking against the original needs Windows.** The main developer here is on a Mac, so
    comparing behaviour needs a Windows VM (Windows 11 ARM in UTM or Parallels) or TheGobOne
    testing builds.
18. **Unpinned dependency versions.** `Avalonia 11.3.*` floats; pin exact versions so builds are
    reproducible.

## Phases

F# compile order decides the **porting** order: a file can't be ported until everything above it
in `Z1R_WPF.fsproj` is ported (or stubbed). The phases describe when each part should **work and
be checked**. In practice:

- Phase 1 pulls in some files grouped under phase 2 (see below).
- `WPFUI.fs` and `Program.fs` come last in compile order, after files grouped under phases 3
  and 4. Those files therefore get a compiling port during phase 2, with their platform parts
  stubbed. Phases 3 and 4 are where they're finished and checked.

**Phase 0 — foundations**
- `Platform.cs` (new): the stub interfaces above, plus the user-file location helper.
- `Graphics.fs`: real port to SkiaSharp + Avalonia, including sprite-sheet slicing,
  black-to-transparent and recolouring. Add every PNG from `Z1R_WPF/icons` as an `AvaloniaResource`.
- `CustomMessageBox.fs`, `Gamepad.fs` (stub).
- Early risk checks: cursor warping on macOS (risk 3), and a font comparison (risk 5).

**Phase 1 — dungeon tracker + save/load** (first point where real runs are trackable)
- `DungeonRoomState.fs`, `Dungeon.fs`, `DungeonSaveAndLoad.fs`, `DungeonHighlightsUI.fs`,
  `DungeonPopups.fs`, `DungeonUI.fs`.
- Compile order wins over phase grouping: everything above `DungeonUI.fs` in `Z1R_WPF.fsproj`
  (`OverworldRouteDrawing`, `HotKeys`, `Timeline`, `CustomComboBoxes`, `PieMenus`,
  `ShowRunCustom`, `OptionsMenu`, `Views`) has to be ported first, so phase 1 pulls those in.
- To see the dungeon tracker before `WPFUI.fs` is ported, add a temporary dungeon-only window
  to `Prototype.fs`.

**Phase 2 — main tracker**
- `OverworldMapTileCustomization.fs`, `LinkRouting.fs`, `OverworldItemGridUI.fs`,
  `UIComponents.fs`, `Layout.fs`, `MainTrackerTriforcesAndItems.fs`, `WPFUI.fs`, `Program.fs`.

**Phase 3 — extra windows and polish**
- `Popouts.fs`, `HUDs.fs`, `Broadcast.fs`, `UIHelpers.fs`, `UserCustomLayer.fs`,
  `DrawingLayer.fs`, `Reminders.fs`, `Debouncer.fs`.

**Phase 4 — platform features and co-op** (needs a human and real machines)
- Replace every stub. Port `SpeechRecognition.fs`, `SyncOriginTracker.fs`,
  `DungeonCInterop.fs`, `CoopSync.fs`, `DungeonModelSync.fs`; retarget `Z1R_Sync` and
  `Z1R_SignalRHost` to net10.

**Phase 5 — ship**
- macOS `.app` bundle (osx-arm64 + osx-x64), signing and notarisation, Linux build, per-OS
  auto-update, testing on each OS.

## Done criteria

Per file:
- Builds with the rest of the solution.
- Every top-level function in the WPF file has a counterpart (or a logged reason it doesn't).
- Layout constants match the WPF file.

Parity (all of these, before WPF can be retired):
- Every feature, window, menu, option, hotkey and ZORA Extra in the WPF tracker works in the
  Avalonia tracker.
- No stubs remain.
- Save files, settings, `HotKeys.txt`, `ShowRunCustom.txt` and ExtraIcons written for the WPF
  tracker work unchanged.
- Side-by-side screenshots against WPF look right to TheGobOne.
- Co-op works between any mix of Windows, macOS and Linux players.
- Acceptable performance on a low-end machine.
- Tested on Windows, macOS and Linux.
- Every remaining difference is listed under "Agreed platform deviations" in PROGRESS.md and
  approved by TheGobOne.

## Catching up with TheGobOne

When TheGobOne releases:
1. `git fetch gob`
2. `git log --stat <last caught-up commit>..gob/master -- Z1R_WPF Z1R_Tracker`
3. Merge `gob/master` into `avalonia-port` (shared model files come along for free).
4. For each changed WPF file that's already ported, apply the same change to its Avalonia
   counterpart.
5. Update the caught-up commit in PROGRESS.md.
