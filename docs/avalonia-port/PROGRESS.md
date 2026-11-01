# Avalonia port — progress

Caught up to WPF commit: `9292b0b` (v17hotfxi, 2026-10-30)

Parity: **not reached.** The goal is complete feature parity with the WPF tracker (see
[PLAN.md](./PLAN.md#goal-complete-feature-parity)). A file marked `stubbed` or `skipped` is still
a parity gap until its stub is replaced or the gap is approved under "Agreed platform deviations".

## Files

Status: `todo` · `ported` · `stubbed` (temporary) · `skipped` (needs a logged reason) · `F#: to C#` (ported in F# before the
switch to C#; the F# version is still in `Z1R_Avalonia/` and out of the build until its C# conversion lands)

| # | WPF file | Lines | Phase | Status | Notes |
|---|---|---|---|---|---|
| – | Platform.cs (new) | – | 0 | ported | platform layer: user-file location, open-with-shell, reveal-in-file-manager, hidden process start, cursor warp, fake clicks, sounds (see Stubs) |
| – | `Z1R_Avalonia_DrawingShim` (new project) | – | 0 | ported | cross-platform `System.Drawing` (Bitmap, Graphics, Icon, SystemIcons, …) on SkiaSharp; replaced the earlier `Drawing.fs` |
| – | `Z1R_Avalonia_WPFResources` (new project) | – | 1 | ported | the WPF tracker's embedded art, in an assembly named `Z1R_WPF` |
| – | `Z1R_Avalonia_Sync`, `Z1R_Avalonia_SharedInterop` (new projects) | – | 1 | ported | net10 builds of Gob's `Z1R_Sync` and `Z1R_SharedInterop`, from his source files unchanged |
| 1 | Gamepad.fs | 59 | 0 | stubbed (C#) | no gamepad is ever found |
| 2 | Graphics.fs | 1634 | 0 | ported (C#) | sounds stubbed; Linux cursor warp/fake clicks stubbed; sprite sheet checked identical to the F# port |
| 3 | CustomMessageBox.fs | 66 | 0 | ported (C#) | `ShowDialogBlocking()` replaces `Owner`+`ShowDialog()` at call sites; the module is a namespace (see Judgement calls) |
| 4 | OverworldRouteDrawing.fs | 268 | 1 | ported (C#) | straight translation; `drawPathsImpl` takes an `Avalonia.Point` and `whatToCyan` as `Func<int,int,bool>`; `doComputedDrawing` takes `IEnumerable<(int,int,bool)>`/`IEnumerable<Control>` |
| 5 | DungeonRoomState.fs | 259 | 1 | ported (C#) | straight translation on Gob's `CDungeonRoomState` (Z1R_Avalonia_Sync); `CurrentDisplayEx` returns `Control`; module is a namespace, `Changed` is a C# event (see Judgement calls) |
| 6 | HotKeys.fs | 606 | 1 | ported (C#) | parsing and in-window keys complete; global hotkey registration belongs to Program.cs; HotKeys.txt is a user file; key numbers unchanged (see Judgement calls) |
| 7 | Timeline.fs | 331 | 1 | ported (C#) | labels stay unwrapped read-only TextBoxes; module is a namespace, `TimelineID` is a union-shaped C# class (see Judgement calls) |
| 8 | CustomComboBoxes.fs | 1418 | 1 | ported (C#) | update check complete; update install Windows-only (macOS/Linux open the release page, see Stubs); "Open folder" opens the user-data folder; async → `Task` (see Judgement calls) |
| 9 | PieMenus.fs | 915 | 1 | ported (C#) | straight translation; async → `Task`; panels are `(Canvas, Action<IBrush>)`; F# lists → `List<T>`/`IEnumerable<T>` (see Judgement calls) |
| 10 | ShowRunCustom.fs | 225 | 1 | ported (C#) | ShowRunCustom.txt is a user file; relative SHOW/RUN paths resolve against the app folder; process launching and "reveal file" via Platform (see Judgement calls) |
| 11 | Dungeon.fs | 1226 | 1 | ported (C#) | straight translation; co-op host launch real on Windows (Job Object moved to `Platform.StartChildProcessTiedToApp`), stubbed on macOS/Linux until phase 4; `DoorState` is a C# enum (see Judgement calls) |
| 12 | OptionsMenu.fs | 1143 | 1 | ported (C#) | every option wired as in WPF; voice via `Platform.SpeechSynthesizer` (System.Speech on Windows, stubbed on macOS/Linux); events are C# events with `trigger…()`; reset deletes the model's settings file (see Judgement calls) |
| 13 | Views.fs | 610 | 1 | ported (C#) | straight translation (no `VisualBrush` in this file); glow animation tweened on a timer; `SynthesizeANewLocationKnownEvent` returns a C# event holder (see Judgement calls) |
| 14 | DungeonSaveAndLoad.fs | 162 | 1 | ported (C#) | straight translation; save text built exactly as WPF's (InvariantCulture numbers, lowercase booleans); `DrawingLayerIcon` is an abstract record (see Judgement calls) |
| 15 | DungeonHighlightsUI.fs | 200 | 1 | ported (C#) | straight translation; repeating animations tweened on a timer; `blockersHoverEvent` is a `Graphics.SharedEvent<bool>` (see Judgement calls) |
| 16 | DungeonPopups.fs | 406 | 1 | ported (C#) | straight translation; async → `Task`; detail popups return `(hasResult, result)` (see Judgement calls) |
| 17 | DungeonUI.fs | 2554 | 1 | ported (C#) | straight translation; drag-paint via Avalonia drag-and-drop in a nested dispatcher frame; summary-tab previews via `VisualBrush`; room-tile flash tweened on a timer; module events are C# events (see Judgement calls) |
| 18 | SpeechRecognition.fs | 102 | 4 | ported (C#) | recognizer via `Platform.SpeechRecognitionEngine`: System.Speech on Windows (as WPF), stubbed on macOS/Linux; `ConvertSpokenPhraseToMapCell` returns `int?` (see Judgement calls) |
| 19 | OverworldMapTileCustomization.fs | 1545 | 2 | ported (C#) | straight translation; accelerators are `Func<CanvasManager, Canvas, int, int, Task>`; "no hotkeys" reveals HotKeys.txt via `Platform.RevealInFileManager` (see Judgement calls) |
| 20 | LinkRouting.fs | 240 | 2 | ported (C#) | straight translation; `RouteDestination` is an abstract record; async → `Task` (see Judgement calls) |
| 21 | Popouts.fs | 547 | 3 | ported (C#) | straight translation; `PopoutKind` is a C# enum; `doUIUpdateEvent` is a `Graphics.SharedEvent`; window positions in WPF units via ShowRunCustom's helpers; no `RestoreBounds` (see Judgement calls) |
| 22 | OverworldItemGridUI.fs | 1034 | 2 | ported (C#) | straight translation; `ShowLocatorDescriptor` is an abstract record; `MakeItemGrid` returns a 13-element value tuple; `redrawOWCircle` is a `StrongBox`; events are `Graphics.SharedEvent`s (see Judgement calls) |
| 23 | UIComponents.fs | 1536 | 2 | ported (C#) | straight translation; Make* functions return value tuples; waypoint gradient tweened on a timer; magnifier cache in `Platform.Files.User("Magnifier")` (see Judgement calls) |
| 24 | HUDs.fs | 392 | 3 | ported (C#) | straight translation; `#if NOT_RACE_LEGAL` kept, so (as in WPF) nothing calls it; frameless transparent windows; positions in WPF units (see Judgement calls) |
| 25 | Broadcast.fs | 251 | 3 | todo | |
| 26 | UIHelpers.fs | 15 | 3 | todo | |
| 27 | UserCustomLayer.fs | 210 | 3 | todo | |
| 28 | DrawingLayer.fs | 442 | 3 | todo | |
| 29 | Layout.fs | 860 | 2 | todo | |
| 30 | Reminders.fs | 288 | 3 | todo | voice/sound stubbed |
| 31 | MainTrackerTriforcesAndItems.fs | 874 | 2 | todo | |
| 32 | Debouncer.fs | 15 | 3 | todo | |
| 33 | SyncOriginTracker.fs | 22 | 4 | todo | |
| 34 | DungeonCInterop.fs | 24 | 4 | todo | |
| 35 | CoopSync.fs | 934 | 4 | todo | |
| 36 | DungeonModelSync.fs | 79 | 4 | todo | |
| 37 | WPFUI.fs | 4218 | 2 | todo | |
| 38 | Program.fs | 2028 | 2 | todo | global hotkeys, desktop shortcut, console window stubbed |

## Stubs

Temporary; every one has to be replaced before parity.

| Stub | Where | Notes |
|---|---|---|
| Gamepad (left shoulder = push-to-talk) | `Gamepad.fs` | `Initialize()` always reports no gamepad |
| Sound effects (`confirm_speech.wav`, `reminder_clink.wav`) and system beep | `Platform.PlaySound`, `Platform.SystemBeep` | log once, play nothing; volume is still tracked |
| Mouse sonar | `Platform.SetMouseSonar` | only reachable from code WPF has commented out (risk 8) |
| Cursor warping on Linux | `Platform.WarpCursor` | returns false; the app then skips the wait for a mouse-move event |
| Fake clicks and scroll wheel on Linux | `Platform.*MouseClick`, `ScrollWheelRotate*` | log once |
| Co-op host launch (`Z1R_SignalRHost`) on macOS/Linux | `Platform.StartChildProcessTiedToApp` (from `Dungeon.startLocalSignalRHost`) | logs once, starts nothing; today the host executable doesn't ship there, so `startLocalSignalRHost` prints "Host EXE not found" first, as WPF does when it's missing. Co-op is phase 4 |
| Speech synthesis and voice choice on macOS/Linux | `Platform.SpeechSynthesizer` (`OptionsMenu.voice`) | logs once; `Speak` prints the text; lists no voices, so "Change Voice" doesn't appear; `Volume` is kept. Windows uses System.Speech, as WPF (risk 6) |
| Speech recognition on macOS/Linux | `Platform.SpeechRecognitionEngine` (`SpeechRecognition.speechRecognizer`) | prints which engine at startup, logs once on use, never recognizes anything; the grammar is still built. Windows uses System.Speech, as WPF (risk 4) |
| In-app update install on macOS/Linux | `CustomComboBoxes.makeVersionButtonWithBehavior` | "Install update" opens the release page instead (risk 11); `downloadAndStageUpdate` refuses off Windows, so no files are ever replaced there |

Already real, not stubs: speech synthesis and speech recognition on Windows (System.Speech, as in WPF), the co-op host launch on Windows (the same kernel32 Job Object calls as WPF), cursor warping and fake clicks/scrolls on Windows (the same user32 calls as WPF) and macOS
(CoreGraphics; fake input needs Accessibility permission).

## Early risk checks (phase 0)

- **Cursor warping on macOS (risk 3): works, but slow inside the window without Accessibility permission.** Warping
  lands the cursor at exactly the right point (checked against the window position, title bar included). Warping
  from outside the window into it produces a pointer event within ~30 ms. Warping *within* the window doesn't produce
  one, so `SilentlyWarpMouseCursorTo` waits out its 5-tick timeout and blocks for ~185 ms per warp. Arrow-key menu
  navigation would feel sluggish, and hover highlights wouldn't follow the cursor until the mouse moves.
  `Platform.WarpCursor` also posts a mouse-moved event, which fixes this once the app has Accessibility permission
  (not yet tested with permission granted). A fix that needs no permission is to inject a pointer-move into Avalonia
  after the warp (`TopLevel.PlatformImpl.Input` with a `RawPointerEventArgs`; Avalonia marks that API unstable).
  To decide in phase 1, when the menus are ported.
- **Fonts (risk 5): not done yet.** Needs a WPF screenshot to compare against.

## Phase 1 status

All phase 1 files are ported to C# and build. `DungeonPreview.cs` (temporary, not part of the mirror) shows only the dungeon
tracker: run the desktop app with `--dungeon-preview` (add `--screenshot out.png` to save a picture and quit). It sets
things up the way `Program.fs`/`WPFUI.makeAll` do before calling `DungeonUI.makeDungeonTabs`.

Checked so far, by rendering the preview headless (Avalonia.Headless + Skia, which works while the Mac's display is
asleep) and comparing with `doc/screenshots/dt-drag-1-map-step1.png`:
- the summary tab and the level 1 tab render, with all nine levels set up without errors;
- layout, sizes, room grid, sprites, header letters, the item/triforce panel and the colours match the WPF screenshot.
  The other visible differences (GRAB vs MOVE, FQ/SQ vs 1Q/2Q, the big level numeral) are newer WPF changes since
  that screenshot.

Not checked yet (needs a person at the Mac, and ideally Windows next to it): mouse and keyboard interaction (room popups,
drag-paint, hotkeys, cursor warps, MOVE/GRAB, the summary hover previews), save/load against a WPF save file, and the
runtime-test lists in the entries below.

## Judgement calls / needs a human

- **Scroll wheel: whole notches only.** WPF's `MouseWheel` fires once per wheel notch. Mac trackpads send a stream of tiny
  deltas, including while two fingers rest on the pad for a right-click, so a plain right-click on a room opened the
  floor-drop menu (scroll down) before the click arrived. Every wheel handler now goes through `Graphics.wheelNotch`, which
  adds the deltas up per control and acts once per 1.0 (one notch). This also covers sideways scrolling (`Delta.Y = 0`).
  **Test on a Mac** with a trackpad and a mouse; once all wheel handlers are ported, check a fast trackpad flick feels OK.
- **Text sits higher off Windows.** WPF uses Segoe UI's line metrics (ascent 1.08 em). macOS falls back to Helvetica
  (ascent 0.77 em), so glyphs are drawn about 0.3 em higher in their box: about 60px for the 200pt dungeon numeral,
  about 4px for 12pt text. `Graphics.matchWpfTextTop` pads the top by the difference (0 with Segoe UI). Used on the dungeon
  numerals and the summary tab's notes. Small text elsewhere is still slightly high. If that shows anywhere (clipped or
  crowded labels), apply it there too, or pick a bundled default font for every platform (risk 5).
- **Summary tab has no level 9 until the end.** As in WPF, the top-left cell holds the help text, and level 9 replaces it
  once Zelda is rescued. Not a bug.

- **Tab look.** Avalonia's Fluent tabs look nothing like WPF's: underlined text, a border around the tab strip as well
  as the content, and 48px tall. `Graphics.wpfTabItemTemplate` and `wpfTabControlTemplate` rebuild WPF's (Aero2) shape:
  boxed tabs with a 1px #ACACAC border, the selected tab white and raised 2px, light-blue hover, and the tab strip above
  a bordered content area. Installed app-wide by `installAppWideStyles`. **Check on Windows:** in the (older) docs
  screenshot WPF's dungeon tabs are 40px apart; ours are 42px (28px header + 6,2 padding + border).

- **Startup order (for whoever ports Program.cs).** In an F# exe, every file's top-level values and `do` blocks run at
  startup, in compile order. In C#, a static class runs its field initializers only when it's first used. Some WPF files
  rely on running early or in order (e.g. Dungeon's `do` block subscribes to `CanvasManager` events and needs the
  CanvasManager to exist). The ported `Program.cs` has to force every ported class's static initialization at the same
  points WPF reaches them, in compile order (e.g. `RuntimeHelpers.RunClassConstructor(typeof(X).TypeHandle)`), and
  check that nothing touches a class too early (a static initializer that throws leaves the class broken for the rest
  of the run).

- **System.Drawing stays `System.Drawing`.** `System.Drawing.Bitmap` (System.Drawing.Common) only works on Windows in
  .NET, so the `Z1R_Avalonia_DrawingShim` project re-implements the parts the trackers use (`Bitmap`, `Image`,
  `Graphics.FromImage` with Clear/DrawImage/FillEllipse/DrawLine/…, `Pen`, `Brushes`, `Icon`, `SystemIcons`,
  `Imaging.ImageFormat/PixelFormat`, `Drawing2D` enums) in the same namespace, on SkiaSharp. Ported F# code and Gob's
  C# code use `System.Drawing.*` exactly as in WPF. (Phase 0 first used an F#-only `Z1RDrawing` namespace; it was
  replaced so Gob's C# compiles unchanged.) `Save` always writes PNG. Checked by rendering ~140 sprites (including
  Z1R_Sync's monster and floor-drop sprites), the overworld map, and a `Graphics.FromImage` drawing.
- **Gob's `Z1R_Sync` (C#) is needed in phase 1, not phase 4.** The dungeon room model (`CDungeonRoomState`, the
  `RoomType`/`MonsterDetail`/`FloorDropDetail` enums and their sprites) lives there, and `DungeonRoomState.fs` and
  `HotKeys.fs` use it. `Z1R_Avalonia_Sync` compiles Gob's four `.cs` files unchanged for net10, with SignalR client
  10.0.14 in place of 3.1 (8.0.11 had a high-severity advisory, GHSA-334f-vvp4-7g6p). His sprite loaders look for a loaded assembly named exactly `Z1R_WPF`, so
  `Z1R_Avalonia_WPFResources` is an assembly with that name, holding the same embedded PNGs. `Graphics.GetResourceStream`
  reads from it as well. Co-op itself (SignalR, starting `Z1R_SignalRHost.exe`) still has to be made to work in phase 4.
  If Gob adds an icon to `Z1R_WPF.fsproj`, add it to `Z1R_Avalonia_WPFResources.csproj` too.
- **Overworld tile cache** moved from `<exe folder>\Overworld\` to `Platform.UserDataDir()/Overworld/` (same as the exe
  folder on Windows). File names are unchanged. Like System.Drawing's, the `.bmp` files are PNG data.
- **Message box icons.** Windows' own icons can't ship on other systems, so the shim's `SystemIcons` draws look-alikes
  (blue ?, blue i, yellow !, red X). Needs Gob's OK as a platform deviation.
- **Message box text** uses `SelectableTextBlock` instead of a read-only `TextBox`. Avalonia's TextBox measures wrapped
  text a pixel or two too narrow, so the last word drops onto a clipped second line (confirmed with a test). **This
  will hit every read-only wrapped TextBox in later files**; use the same fix there.
- **`CustomMessageBox.ShowDialogBlocking()`.** Avalonia's `ShowDialog` is asynchronous, so the WPF pattern
  `cmb.Owner <- w; cmb.ShowDialog()` becomes `cmb.OwnerWindow <- w; cmb.ShowDialogBlocking()`, which runs a nested
  dispatcher loop like WPF's.
- **Rounded button/checkbox templates** are code templates instead of XAML strings, and react to the same properties
  as WPF's triggers. The content presenter isn't named `PART_ContentPresenter`, because the Fluent theme restyles that
  part on hover. The rounded *text box* keeps the theme's template and overrides its colour resources instead, because
  Avalonia's TextBox template has too many required parts to rebuild safely. Its focused border is 1px, as in WPF.
- **"WPF metrics" styles in `installAppWideStyles`.** Avalonia's Fluent theme gives TextBox a 32px minimum height and
  64px minimum width, CheckBox 32×120 minimums, and different padding. These styles restore WPF-like sizes (TextBox: no
  minimums, 2px side padding; CheckBox: no minimums; Label: 5px padding). Expect to add more as controls are ported, and
  check side by side on Windows.
- **Theme variant must be Light.** WPF's unstyled defaults are black text on white, e.g. `makeButton`'s TextBox text
  is black unless a colour is passed. The ported `Program.fs` has to set `RequestedThemeVariant = Light` (the prototype
  uses Dark).
- **`scaleUpCheckBoxBox`.** Avalonia has no `LayoutTransform` property, so the checkbox template puts its box in a
  `LayoutTransformControl` named `boxScale` that this scales. The box-to-text gap is scaled too, as WPF's transform
  does.
- **Shop-highlight animations** (`TileHighlightRectangle.Make*WithBriefAnimation`) are hand-tweened on a 16 ms timer
  (stroke 12→4, colour cyan→target, 0.5 s). Avalonia's animation system can't animate a shape's stroke brush colour.
  **Needs Gob:** in WPF the finished animation keeps holding the stroke thickness at 4, so any later `MakePale*`
  (2px) or `MakeBoldGreen` (6px) on that tile is silently drawn at 4px. That looks like an accidental WPF quirk, and the
  port doesn't copy it.
- **Label foreground in the message box buttons (needs checking on Windows).** WPF's Label style sets its own (black)
  foreground, so WPF's message box buttons may show black text on the dark rounded button. Avalonia's Label inherits
  the button's orange. Compare with WPF before deciding.
- **Timeline text measuring.** WPF's `FormattedText` takes `NumberSubstitution`, `TextFormattingMode.Display` and
  `PixelsPerDip`; Avalonia's doesn't (it measures in DIPs), so those arguments are dropped. The seed/flags/title
  layout depends on these widths and on the TextBox's own padding (+10), so compare the timeline's top row with WPF.
- **Timeline hover tooltip** (`timeToolTip`) is an editable, unwrapped TextBox with a 3px border, as in WPF. Its border
  colour comes from the theme (WPF's default is a light grey); compare on Windows.
- **C# shapes of F# module values (Graphics.cs sets the pattern for later files).** A `let` whose value needs several
  statements → a `private static make_<name>()` method just above its `public static readonly` field. A tuple `let (a, b, …) =`
  → one private helper field (`_icons7x7`, `_heartFroms`, …) holding the array/value tuple, then one field per name.
  `do` blocks → `private static readonly bool _initN = initN();` at the same position. F# `option` values → nullable
  (`entranceRoomArrowColor` is `System.Drawing.Color?`, `dpi` is `double?`; `makeButton(text, double?, IBrush)` takes null for None).
  Curried `darkenImpl pct bmp` / `darkenKeepAlpha pct bmp` → `(pct, bmp)`. `overworldMapBMPs` returns `Bitmap[,]`;
  `dungeonRoomBmpPairs` is an array of value tuples. Exhaustive F# matches get a `_ => throw` arm for the C# compiler.
- **F# `Event<T>` that other files `Trigger`** (e.g. `Graphics.volumeChanged`) becomes a C# `event Action<T>` plus a public trigger
  method, since a C# event can't be raised from outside its class: WPF's `Graphics.volumeChanged.Trigger(v)` becomes
  `Graphics.triggerVolumeChanged(v)`, and `volumeChanged.Publish.Add(fun v -> …)` becomes `Graphics.volumeChanged += v => …`.
  (F# events of the F# model, e.g. `SaveAndLoad.seedAndFlagsUpdated`, keep their F# type: `.Publish.AddHandler(new FSharpHandler<T>(…))`.)
- **`DragDropSurface<T>`** takes `onStartDrag` as `Action<PointerEventArgs, Action<T>>` (F#: a function of a tuple).
- **`CustomMessageBox` is one class, not a static class.** C# rejects a nested type named like its enclosing class
  (CS0542), so, per PLAN.md, `CustomMessageBox.fs` becomes `class CustomMessageBox : Window` in namespace `Z1R_Avalonia`.
  WPF's `CustomMessageBox.CustomMessageBox(title, icon, text, buttonTexts)` becomes `new CustomMessageBox(title, icon, text, buttonTexts)`;
  `buttonTexts` is an `IEnumerable<string>` (pass an array).
- **`DungeonRoomState` is one class too** (same CS0542 problem as `CustomMessageBox`). The module's own values (`canvasAdd`,
  `cachedTilePairs`, `mkTxt`, `entranceRoomArrowColorBrush`, `scale`, `isDoingDragPaintOffTheMap`, `veryDark`, `fakeUsedTransports`)
  are static members of `class DungeonRoomState`, ahead of the instance members. WPF's `DungeonRoomState.DungeonRoomState` becomes
  `DungeonRoomState`; `DungeonRoomState.FromCSharp(...)` and `DungeonRoomState.mkTxt(...)` read as in WPF.
- **F# events only raised by their own class become C# events** (precedent: `Gamepad.ControllerFailureEvent`). `DungeonRoomState.Changed`
  is `event Action<DungeonRoomState>`: WPF's `drs.Changed.Add(fun _ -> ...)` becomes `drs.Changed += _ => ...`.
- **HotKeys key numbers need no translation.** HotKeys.txt stores keys as `0-9`/`a-z` (turned into WPF `Key` numbers by arithmetic:
  `'a'+44`, `'0'+34`) or `\nnn` (a WPF `Key` number); settings store no keys. Checked by reflection against WPF's `WindowsBase`
  (net472 reference assembly): all 202 `System.Windows.Input.Key` names exist in `Avalonia.Input.Key` with the same numbers (0..172);
  Avalonia only adds keys numbered 10001 and up. So existing HotKeys.txt files work unchanged (tested: `\24` = Up, `\75` = NumPad1).
  Aliased numbers (Return/Enter, Prior/PageUp, …) exist in both; `PrettyKey` shows `Key.ToString()`, so compare a displayed alias name on Windows.
- **HotKeys C# shapes.** The nested `module MyKey` (which callers `open`) is flattened into namespace `Z1R_Avalonia`: `SingleKeyboardModifier`,
  `MyKeyRoutedEventArgs`, `MyKey.MyKeyEvent`, and `c.MyKeyAdd(f)` (an extension on `Control`) keep their names. `SingleKeyboardModifier` and
  `HotKeys.GlobalHotkeyTargets` are C# enums whose members live in `SingleKeyboardModifierExtensions` / `GlobalHotkeyTargetsExtensions`
  (Gob's `RoomType`/`RoomTypeExtensions` pattern): `x.PrettyString()`, `x.AsHotKeyName()`, `x.AsHotKeyDisplay()` are unchanged, but
  `GlobalHotkeyTargets.All` → `GlobalHotkeyTargetsExtensions.All`, `SingleKeyboardModifier.FromHotKeyTxt(s)` → `SingleKeyboardModifierExtensions.FromHotKeyTxt(s)`,
  and `SingleKeyboardModifier.GetCurrent()` → `SingleKeyboardModifierExtensions.GetCurrent(ea.KeyModifiers)` (no global keyboard state in Avalonia).
  Keys are `(SingleKeyboardModifier, Key)` value tuples everywhere (`ea.Key`, `PrettyKey(k)`, `HotKeyProcessor` methods). `HotKeyProcessor<V>.TryGetValue`
  is `bool TryGetValue(k, out V value)` (WPF: returns `'V option`); `AsPrettyHotKeyOpt` returns a string or null.
  `DungeonRoomHotKeyProcessor` holds `HotKeys.DungeonRoomHotKey` (WPF: `Choice<RoomType,MonsterDetail,FloorDropDetail,DoorHotKeyResponse>`), an
  abstract record with cases `OfRoomType(rt)`, `OfMonsterDetail(md)`, `OfFloorDropDetail(fd)`, `OfDoor(dhkr)` for `Choice1Of4`..`Choice4Of4`, each with
  a `Value`; records compare by value like F#'s Choice, which the processor's dictionaries rely on. So WPF's
  `AppendHotKeyToDescription(s, Choice2Of4 ident)` becomes `AppendHotKeyToDescription(s, new HotKeys.DungeonRoomHotKey.OfMonsterDetail(ident))`, and
  `| Choice1Of4 rt ->` becomes `case HotKeys.DungeonRoomHotKey.OfRoomType(var rt):`. WPF's `MyKeyRoutedEventHandler`
  delegate is replaced by `EventHandler<MyKeyRoutedEventArgs>`.
- **HotKeys: element under the mouse (test at runtime).** WPF sends each hotkey to `Mouse.DirectlyOver`, which is app-wide. Avalonia keeps the
  element under the pointer per TopLevel (`IInputRoot.PointerOverElement`), and popups are TopLevels of their own, so `InitializeWindow` uses the
  main window's, falling back to the TopLevel the pointer last moved in (a class handler on `PointerMoved`). Check hotkeys while hovering popup
  menus, and that keys still arrive at the main window while a popup is open. With Alt held, WPF reports the key in `SystemKey`; Avalonia always uses `Key`.
- **HotKeys on macOS: which modifiers? Needs Gob.** As in WPF, only exactly Shift, Ctrl or Alt (Option) count; anything with the Windows key /
  Cmd (`KeyModifiers.Meta`) is `Unsupported`, so Cmd+key never triggers a hotkey. Mac users may expect Cmd where Windows uses Ctrl.
- **HotKeys.txt and HotKeys_BlankSampleTemplate.txt** are written to `Platform.Files.User` (the exe folder on Windows, as before).
- **`Timeline` is one class too** (CS0542). The module's values are statics of `partial class Timeline` (split around `TimelineID`/`TimelineItem`
  to keep the file order), and `TimelineID`/`TimelineItem` are at namespace level. WPF's `Timeline.Timeline(...)` → `new Timeline(...)`,
  `Timeline.TimelineID.X` → `TimelineID.X`, `Timeline.TimelineItem(...)` → `new TimelineItem(...)`; `Timeline.isCurrentlyLoadingASave` is unchanged.
  `TimelineItem` takes its bitmap function as `Func<System.Drawing.Bitmap>`; `Update` takes `IEnumerable<TimelineItem>`.
- **`TimelineID` (an F# union with data cases) is one C# class shaped like a compiled F# union:** `Tag` / `TimelineID.Tags.X`, a static instance
  per plain case (`TimelineID.MagicalSword`), a static method per data case (`TimelineID.LevelBox(i, j)`, `OWHeart(n)`, `CustomIcon(n)`,
  `UserCustom(s)`), case data in `Item1`/`Item2` (ints) and `Text` (UserCustom), and structural `Equals`/`==`/`GetHashCode`/`ToString` like F#'s.
  A WPF match `| Timeline.TimelineID.LevelBox(i,_) ->` becomes `case TimelineID.Tags.LevelBox:` reading `tid.Item1`.
- **Timeline graph sort.** F#'s `Array.sortBy` isn't stable: FSharp.Core implements it as the BCL's `Array.Sort(keys, items)`. Equal timestamps
  matter in `DrawGraph` (the last sample in a tick bucket wins), so it calls that same BCL `Array.Sort(keys, items)`, not a stable sort such as
  LINQ `OrderBy`, which could order ties differently from WPF.
- **CustomComboBoxes: F# async → `Task`.** `DoModalCore`, `DoModal*`, `DoModalMessageBox*`, `DoModalGridSelect`, `DisplayItemComboBox` are `async Task`/`Task<T>`;
  callers `await` them for `let!`/`do!`. `Async.AwaitWaitHandle(wh)` + `SwitchToContext` → a private `AwaitWaitHandle` (thread-pool wait + `TaskCompletionSource`;
  the await resumes on the UI context). **`x |> Async.StartImmediate` → `CustomComboBoxes.StartImmediate(task)`** (an `async void` wrapper, so an escaping exception
  is rethrown on the UI thread as in F#); later files should use it. `Async.SwitchToThreadPool` … `SwitchToContext` → `await Task.Run(...)`. The `ManualResetEvent`s
  callers pass stay `ManualResetEvent`.
- **CustomComboBoxes C# shapes.** `GlobalFlag` is a nested static class: WPF's `open GlobalFlag` … `popupIsActive` → `CustomComboBoxes.GlobalFlag.popupIsActive`.
  `PopupClickBehavior<T>` and `GridClickDismissalWarpReturn` are abstract records with nested cases (as `HotKeys.DungeonRoomHotKey`): `DismissPopupWithResult(r)` →
  `new PopupClickBehavior<T>.DismissPopupWithResult(r)` (`.Value`), `StayPoppedUp` → `new PopupClickBehavior<T>.StayPoppedUp()`, `WarpToCenter` →
  `new GridClickDismissalWarpReturn.WarpToCenter()`, `WarpTo(p)` → `new GridClickDismissalWarpReturn.WarpTo(p)` (`.Pos`). `DoModalGridSelect<TState,TResult>` returns
  `Task<(bool hasResult, TResult result)>` for `'Result option` (TResult may be a value type); its grid argument is the value tuple `(gnc, gnr, gcw, grh)`, elements are
  `(Control, bool, TState)[]`, `redrawTile` is `Action<TState>`, `onClick` is `Func<PointerPressedEventArgs, TState, PopupClickBehavior<TResult>>`, decorations are
  `IEnumerable<(Control, double, double)>`, `who` is a `ManualResetEvent` or null, `opaOpt` a `double?`. `DisplayItemComboBox` returns `(hasResult, (ident, PlayerHas))`.
  `MouseButtonEventArgsToPlayerHas` takes a `PointerPressedEventArgs` (every WPF caller passes a MouseDown's args). `ModalGridSelectBrushes` holds `ISolidColorBrush`es.
  `CanvasManager.AfterCreatePopupCanvas`/`BeforeDismissPopupCanvas` are C# events (`+=`). `boxCurrentBMP(n, null)` for None. `updateAvailable` is `(string, string)?`;
  `updateListeners` holds `Action<(string, string)>`; `fetchLatestRelease` returns null and `fetchReleaseZipUrl`/`downloadAndStageUpdate` return null for None.
- **Popup grids: pointer capture (test at runtime).** Avalonia captures the pointer to the element pressed; WPF doesn't. A press on a grid cell releases that capture,
  so Shift+drag drops on the cell under the mouse and hover highlights follow a held button, as in WPF. The drag bar and resize grip capture explicitly, as in WPF.
- **Popup grids: Shift for Shift+drag** is read from the click's `KeyModifiers` (WPF: global `Keyboard.IsKeyDown`).
- **Popup grids: horizontal scrolling ignored.** Avalonia reports sideways scrolling (trackpads) as a wheel event with `Delta.Y = 0`, which WPF's `MouseWheel` never
  delivers; without a guard it would step the selection. It's ignored.
- **Popup grids: scaling and rotation.** The scaled grid sets `RenderTransformOrigin = TopLeft` (Avalonia's default is the center, WPF's the corner). The lock bar's
  rotated text uses a `LayoutTransformControl` (no `LayoutTransform` in Avalonia). Arrow-key navigation and "jump to mouse" use `TranslatePoint` through the scale
  transform; check they land on the right cell at 1.25x/2x and after resizing.
- **Popup grid cursors.** WPF `SizeNESW`/`SizeNWSE` → Avalonia `BottomLeftCorner`/`BottomRightCorner` (the grip's corner); `Hand`, `Arrow`, `SizeAll` unchanged.
- **Modal message box (CustomComboBoxes).** WPF's type-keyed styles in `b.Resources` → `Styles` on the border (TextBox orange/black, rounded Button). The wrapped
  read-only TextBox is a `SelectableTextBlock` with the style's orange set directly.
- **Escape closes the top popup** through a tunnelling `KeyDown` class handler on `Window` (WPF: `RegisterClassHandler` on `PreviewKeyDown`).
- **"POPUP NOTE" lines** go to the crash log in `Platform.Files.User` (WPF: the exe folder; same place on Windows).
- **Update check uses `HttpClient`.** WPF's `WebClient` and its `ServicePointManager` TLS 1.2 line are obsolete in .NET (SYSLIB0014 warnings); `HttpClient` makes the
  same GitHub API requests with the same User-Agent, and .NET already uses TLS 1.2/1.3. The download reports the same 0-100 percentage.
- **Update install (Windows).** Same PowerShell script, but it relaunches the port's own executable (`Graphics.ExeName`) instead of `Z1R_WPF.exe`, and trims the
  platform separator instead of `'\\'`. **Needs Gob:** it installs the first `.zip` asset of the release, which today is the WPF build; once the Avalonia build ships,
  the release needs a way to tell the two zips apart (risk 11).
- **"Open folder"** on the version button opens `Platform.UserDataDir()` (where HotKeys.txt, settings and the crash log are; the exe folder on Windows, as in WPF)
  through `Platform.OpenWithShell` (WPF: `Explorer.exe`). "Go to website" and the release page also use `OpenWithShell`.
- **PieMenus C# shapes.** Every `…Async` is an `async Task<T>` (`FourWayPieMenu` a `Task`). A panel is `(Canvas, Action<IBrush>)`; `FourWayPieMenu` takes
  `((Canvas, Action<IBrush>), Dock, Action)[]`. Hotkey labels (`keyOpt`) are a string or null. `pieDisabledDocks` is `List<Dock>`, `crossedOutChoices`/`legacyDisabledChoices`
  are `List<string>` (callers assign a new list, as WPF assigns a new F# list). `cheapKeysChangedHook` is `Action`, `shopKeyIndexHook` and `itemPickerDisabledTip` are `Func<int,…>`.
  `ItemPickerAsync` takes `IEnumerable<(int, Control)>`, `IconChoiceBoxAsync` `IEnumerable<(string, Control)>`, `CaveChoiceBoxAsync`/`MessageChoiceBoxAsync` `IEnumerable<string>`.
  `ShopFlyoutAsync` takes `shopItems` as `List<(Bitmap, bool)>[]`, `addItem` as `Func<int, int, (int[], List<(Bitmap, bool)>[])>`, `pickItem` as `Func<Task<int>>`.
- **Pie menu selection uses Avalonia's `Dock` numbers** (`Left=0, Bottom=1, Right=2, Top=3`; WPF: `Left, Top, Right, Bottom`). `currentSelection` only ever compares
  against `(int)Dock.X` inside `FourWayPieMenu`, so nothing changes.
- **Crossed-out cave choices (strikethrough).** Avalonia's TextBox has no `TextDecorations`, so a crossed-out button's read-only TextBox label is replaced by a TextBlock
  with the same text, size, colours and alignment plus `Strikethrough`. Compare its look with WPF.
- **`makeOrangeCheckBox`** builds its template in code (WPF: a XAML string with an `IsChecked` trigger): same 16×16 orange box, same tick path, 8px gap.
- **Shop flyout right-click** (`PreviewMouseRightButtonUp`) is a tunnelling `PointerReleased` handler that checks `InitialPressMouseButton == Right`. `IsMouseOver` → `IsPointerOver`.
- **Shop flyout layout measured before it is in the window (test at runtime).** WPF sizes the box and computes `headerH` and the first button's size (for "Jump mouse to
  shop") from `Measure` before the box is shown, and has no re-centering `SizeChanged` handler here. In Avalonia, a control measured outside the visual tree may not have
  its theme template/styles yet, so the box could be off-centre and the mouse jump could miss the first shop button. Check both.
- **`addGiantRedX`**: WPF sets `StrokeStartLineCap`/`StrokeEndLineCap`; Avalonia has one `StrokeLineCap` (both Round, so the same).
- **ShowRunCustom.txt** is written to `Platform.Files.User` (WPF: the exe folder; same place on Windows). `ShowRunCustom.Line` (an F# union) is an abstract record
  with cases `BLANK()`, `COMMENT(Text)`, `SHOW(File, Ltwh)` (`Ltwh` a nullable `(double, double, double, double)`), `RUN(Command, Args)`; `ParseShowRunCustomFile` returns `List<Line>`;
  `DoShowRunCustom` takes an `Action`; `makeScalable` takes a `Control`.
- **ShowRunCustom relative paths.** WPF opens a relative SHOW/RUN path from the current directory (the exe folder when started normally); the sample lines are
  `.\ShowRunCustomImages\...`, images that ship with the app. A macOS/Linux app's current directory isn't its folder, so relative paths resolve against `Platform.AppDir()`,
  with `\` turned into `/` off Windows (so old files still work). The new template's sample paths are written `./ShowRunCustomImages/...`. Absolute paths are unchanged.
- **ShowRunCustom window positions are kept in WPF units.** WPF's `Left`/`Top` are device-independent; Avalonia's `Position` is in pixels, so the port converts with
  `DesktopScaling` when reading and writing ShowRunCustom.txt and `ShowRunInfoWindowLT`. Saved width/height are Avalonia's client size (WPF: the outer size, borders and
  title bar included), so a SHOW window saved by WPF reopens slightly larger. `LocationChanged` → `PositionChanged`, `ContentRendered` → `Opened`, `Owner` → `Show(owner)`.
  `ResizeMode.CanResizeWithGrip` → `CanResize` (Avalonia draws no resize grip). Check minimized/closing windows don't write junk positions (MINIMIZED_THRESHOLD) on Windows.
- **ShowRunCustom "show the file".** The info/error window opens the file manager with ShowRunCustom.txt selected via `Platform.RevealInFileManager` (Windows: same
  `Explorer.exe /Select,` call; macOS: Finder `open -R`; Linux: opens the folder, since there's no standard way to select a file). RUN of an executable goes through
  `Platform.StartProcessHidden` (same `ProcessStartInfo`); RUN of a URL through `Platform.OpenWithShell`, which logs a failure instead of showing the error window.
  On macOS a `.app` bundle is a folder, so `RUN "/Applications/X.app"` reports "does not exist", as a folder would in WPF. **Needs Gob:** should RUN accept `.app` bundles on macOS?
- **Dungeon C# shapes.** `Dungeon.DoorState` (an F# union with no data) is a C# enum with the same case names and numbers; its members are in
  `DoorStateExtensions` (HotKeys' `SingleKeyboardModifierExtensions` pattern): `s.AsInt()` is unchanged, `Dungeon.DoorState.FromInt(x)` →
  `DoorStateExtensions.FromInt(x)`, `Dungeon.DoorState.FromString(s)` → `DoorStateExtensions.FromString(s)`. `DoorInterop`, `ZeldaFont` and `GrabHelper`
  are nested classes (`Dungeon.DoorInterop.fromCSharp(...)`, `new Dungeon.GrabHelper()` read as in WPF). `Door` takes `redraw` as `Action<DoorState>`;
  `roomChangedCallback`/`SetRoomChangedCallback` use `Action<int,int,int>`; `HDNCP` is a nullable `(TrackerModel.Dungeon, ManualResetEvent)`;
  `GrabHelper.PreviewDrop` returns a `(bool[,], bool[,])` value tuple; `HotKeyAHiddenDungeonLabel(Control, TrackerModel.Dungeon, ManualResetEvent)` takes null
  for None; the three `Hidden…Popup`s are `async Task` and take a `CustomComboBoxes.GridClickDismissalWarpReturn` (callers pass
  `new CustomComboBoxes.GridClickDismissalWarpReturn.WarpTo(p)`). Their grid selects return no result, so `TResult` is `object` (F# leaves it generic).
  The door colour brushes are `IBrush`; `zeldaLetterCache`/`MakeLetterBIInZeldaFont` give an `Avalonia.Media.Imaging.Bitmap`; `ZeldaFont.extraGlyphs` is a `Dictionary<char, string[]>`.
- **Co-op host launch (Dungeon.startLocalSignalRHost).** The Windows Job Object code (kernel32 `CreateJobObject`/`SetInformationJobObject`/
  `AssignProcessToJobObject`, the struct-size retries and the lazy job creation) moved unchanged from Dungeon into `Platform.StartChildProcessTiedToApp(fileName,
  arguments)`, which also builds the same `ProcessStartInfo` (no shell execute, no window) and returns the `Process`. So `Dungeon.signalRJobHandle` and
  `Dungeon.createSignalRJob` no longer exist; nothing else in WPF uses them. The host's path is `Platform.Files.App("Z1R_SignalRHost.exe")` (WPF:
  `AppDomain.CurrentDomain.BaseDirectory`, the same folder). On macOS/Linux the function is a stub returning null (no "Launched" line is printed). **Needs Gob
  (phase 4):** the host executable's name off Windows (no `.exe`), and how it is built and shipped there.
- **`Dungeon`'s `do` block (subscribing to `AfterCreatePopupCanvas`) runs when Dungeon's statics are first initialized (test at runtime).** Like WPF's, it reads
  `CanvasManager.TheOnlyCanvasManager`, so the CanvasManager must exist before anything first reads a `Dungeon` field (C# initializes a class's statics on first
  field access; F# runs a non-last file's top-level code on first access too). If a field is read earlier, the class fails to initialize
  (`TypeInitializationException`), and every later use of `Dungeon` fails. Check once Program.cs/WPFUI.cs are ported.
- **Hidden-dungeon popups (compare on Windows).** The two-line labels ("(Note: in Mixed Quest dungeons,\n7 and 8 may be swapped)" and the "Don't mark
  anything other than '?'" warning) are unwrapped TextBoxes, as in WPF, holding a `\n`; check both lines show, centred. The customizer's two buttons use
  Avalonia's Button theme (WPF: the default Windows button chrome with a 5px border), so their look differs; compare. `Loaded` (used to jump straight into a
  chooser, or to warp the mouse onto the colour button) exists in Avalonia but may fire at a slightly different time from WPF's; check the mouse lands on the
  button and the accelerated choosers open in the right place.
- **Speech synthesis: `Platform.SpeechSynthesizer`.** WPF's `OptionsMenu.voice` (a System.Speech `SpeechSynthesizer`) is a `Platform.SpeechSynthesizer`
  with the members the tracker uses: `Volume`, `VoiceName` (WPF `Voice.Name`), `GetInstalledVoices()` (`Platform.InstalledVoice(Name, Enabled)`; WPF
  `v.VoiceInfo.Name`, `v.Enabled`), `SelectVoice(name)`, `Speak(text)` (synchronous). On Windows it wraps System.Speech from the `System.Speech` NuGet
  package (10.0.0), which builds on every OS; it's only touched behind `OperatingSystem.IsWindows()` (no CA1416 warnings), and off Windows that assembly
  would throw `PlatformNotSupportedException`. Off Windows it's a stub (see Stubs). Reminders' `voice.Volume` / `voice.Speak(text)` read as in WPF.
  A `PreferredVoice` saved on Windows is simply not found elsewhere; `SelectVoice` falls back as in WPF (risk 6). Speech *recognition* isn't in
  OptionsMenu (only the "Listen For Speech"/"Confirmation Sound" checkboxes, wired to `TrackerModelOptions` as in WPF); it comes with SpeechRecognition.fs.
- **OptionsMenu C# shapes.** The twelve `Event<unit>`s are `event Action`s with a `trigger…()` method each (`OptionsMenu.requestRedrawOverworldEvent.Trigger()` →
  `OptionsMenu.triggerRequestRedrawOverworldEvent()`, `….Publish.Add(f)` → `OptionsMenu.requestRedrawOverworldEvent += f`). `data1o`/`data1d`/`data2` are value-tuple
  arrays (`data1o`'s margin is a `Thickness?`). Tour steps are `List<(string[] labels, string title, string body)>`; `runGuidedTour(Core)` take
  `IEnumerable<…>` of those and an `Action`. `closeOptionsPopupHook`/`startBlankRunHook` are `Action`s. `makeOptionsCanvas` returns the `Border`.
- **CheckBox `Checked`/`Unchecked` → `Graphics.onChecked(cb, f)` / `Graphics.onUnchecked(cb, f)`.** Avalonia 11 marks those events obsolete (CS0618);
  the helpers use `IsCheckedChanged` and fire on the same transitions. Later files should use them.
- **`ToolTipService.SetShowDuration` has no Avalonia equivalent:** an Avalonia tooltip stays open as long as the mouse is over its control (WPF hides it
  after the duration; OptionsMenu sets 10 s). Kept as a no-op `setToolTipShowDuration` call so lines still match WPF.
- **Option sliders.** WPF `TickPlacement.Both` → Avalonia `TickPlacement.Outside`. The Fluent slider looks different (and is taller) than WPF's; compare
  the options screen's column heights on Windows.
- **Reset All Settings deletes the model's settings file**, `Z1R_Tracker_settings.json` in `AppDomain.CurrentDomain.BaseDirectory`, because that's where
  `TrackerModelOptions` (shared F# model, not editable here) reads and writes it. On Windows that's `Platform.UserDataDir()` anyway; on macOS it's inside
  the app bundle, which a signed app can't write (see Proposed upstream PRs).
- **"Show Crash Log..."** reveals `Platform.Files.User("Z1R_Tracker_crash_log.txt")` with `Platform.RevealInFileManager`, or opens `Platform.UserDataDir()`
  with `Platform.OpenWithShell` if there's no log yet (WPF: `Explorer.exe` on the exe folder; the same on Windows).
- **Co-op host settings: an unshown `Window`.** As in WPF, `showCoopHostSettingsWindow` creates a `Window` it never shows and `Close()`s it from Launch/Stop.
  An Avalonia `Window` creates its native window on construction, so this costs a hidden native window per open; harmless, kept for parity.
- **Guided tour (test at runtime).** `VisualTreeHelper` walks → `GetVisualChildren()`/`GetVisualParent()`; WPF `IsVisible`/`ActualWidth` →
  `IsEffectivelyVisible`/`Bounds.Width`; `Primitives.ButtonBase` → `Button` (CheckBox/ToggleButton/RadioButton derive from it in Avalonia);
  `TransformToAncestor(...).TransformBounds` → `TransformToVisual` + `TransformToAABB` (a null transform skips the label, as WPF's exception did). Check the
  highlights land on the right controls on both tours, and that the callout placement matches WPF.
- **Views C# shapes.** `Some cm`/`None` → a `CanvasManager` or null (`MakeTriforceDisplayView`, `MakeBoxItemWithExtraDecorations`); `owInstanceOpt` →
  an `OverworldInstance` or null; `computeExtraDecorations…Opt` → a `Func<Point, IEnumerable<(Control, double, double)>>` or null. `MakeBoxItem(WithExtraDecorations)`
  return `(Canvas, Action<int, CustomComboBoxes.GridClickDismissalWarpReturn>)`; `MakeBlockerCore` returns `(Canvas, Func<DungeonBlocker, Canvas>)`.
  `uncollectedTierLetter` returns a string or null. `redrawTriforces`/`redrawBoxes` are `List<Action>`. `hintDashedItemsChanged` is an `event Action`
  with `Views.triggerHintDashedItemsChanged()`. `SynthesizeANewLocationKnownEvent(ev, filter = null)` returns a `Views.NewLocationKnownEvent`
  (`newLocation.Fired += f`); WPF's `Views.SynthesizeANewLocationKnownEvent(ev |> Event.filter p)` (OverworldItemGridUI) becomes
  `SynthesizeANewLocationKnownEvent(ev, p)`. `drawTinyIconIfLocationIsOverworldBlock` takes the location as the model's `Tuple<int,int>`.
  The module's `do` (subscribing `redrawBoxes` to model events) is `_init1`, so it runs when `Views` is first touched (see "Startup order").
- **Views: no `VisualBrush`.** Views.fs (WPF @ 9292b0b) doesn't use one; nothing to translate.
- **Progressive Items glow (compare on Windows).** WPF's `DoubleAnimation` 1→1.9 over 1.5 s with `AutoReverse` on the `ScaleTransform` is tweened by hand
  on a 16 ms `DispatcherTimer` (linear, as WPF's default), like `TileHighlightRectangle`. WPF's `DropShadowEffect(ShadowDepth=0)` →
  `DropShadowDirectionEffect` with the same values; Avalonia's blur radius may not look identical. The z-index raise walks visual parents and checks the
  logical `Parent is Panel`, as WPF does; `Panel.SetZIndex` → `ZIndex`.
- **Item boxes: sideways scrolling ignored**, as in the popup grids (`Delta.Y == 0` would otherwise open the item popup stepping backwards).
- **Item box / triforce clicks.** `MouseDown` with `ChangedButton` → `PointerPressed` with `PointerUpdateKind` (the box reacts to left/middle/right presses only,
  as WPF's `ButtonState = Pressed` check; the triforce's non-middle branch also runs for thumb buttons, as in WPF).
- **DungeonSaveAndLoad C# shapes.** The model classes (`DungeonRoomModel`, `DungeonModel`, `DrawingLayerIconModel`, `JustVersion`, `AllData`) are nested classes
  with the same property names, defaults and declaration order (System.Text.Json and co-op's Newtonsoft map by name and write in declaration order; `IsDefault`
  stays a read-only property, as in F#). `DrawingLayerIcon` (an F# union) is an abstract record with cases `ZTracker(Value)` / `ExtraIcon(Value)`:
  `DrawingLayerIcon.ZTracker k` → `new DungeonSaveAndLoad.DrawingLayerIcon.ZTracker(k)`. `AllDrawingLayerStamps` is a `List<(DrawingLayerIcon, int, int, Image)>`.
  The `Save*` functions return `string[]`, as WPF. Numbers are written with `InvariantCulture` (F#'s `sprintf "%d"` ignores the culture) and booleans as
  `true`/`false`, so save files are byte-for-byte the same as WPF's. **Test at runtime:** load a WPF save file, save it again, and diff.
- **Drawing-layer stamp "half size" check.** WPF counts a stamp as full size when its `RenderTransform` is null or the shared `Transform.Identity`. Avalonia has
  no shared identity transform and a control's `RenderTransform` is null until set, so `SaveDrawingLayer` checks null only. DrawingLayer.cs must set it back to
  null (as WPF does) when un-halving a stamp.
- **`Graphics.SharedEvent<T>` for F# `Event<T>`s passed between files.** WPFUI creates `blockersHoverEvent = new Event<bool>()` and passes it to UIComponents
  (which `Trigger`s it) and DungeonUI → DungeonHighlightsUI (which subscribe). A C# event can't be passed as a value or raised from outside its class, so such
  values are a `Graphics.SharedEvent<T>` (added to Graphics.cs as a port helper): `new Event<bool>()` → `new Graphics.SharedEvent<bool>()`, `ev.Trigger(x)` is
  unchanged, `ev.Publish.Add(f)` → `ev.Fired += f`. Later files (`levelTabSelected : Event<int>` in WPFUI/UIComponents/DungeonUI, …) should use it too.
- **Dungeon "possible continuations" highlights (compare on Windows).** WPF's repeating `DoubleAnimation`s (door rectangles: scale 1→1.3 over 0.75 s,
  auto-reversed, forever; room ellipses: dash offset 0→4.4 over 0.75 s, forever) started with `BeginAnimation(prop, anim)` and removed with
  `BeginAnimation(prop, null)` are tweened by hand on one 16 ms `DispatcherTimer` per animation (linear, as WPF's default), as `TileHighlightRectangle`
  does; removing sets the base value back (scale 1, offset 0), as WPF's does. WPF's `ScaleTransform(CenterX, CenterY)` → `RenderTransformOrigin` at the same
  absolute point. Avalonia may draw the dashed ellipse's dash pattern slightly differently.
- **DungeonPopups C# shapes.** `DoMonsterDetailPopup`/`DoFloorDropDetailPopup` are `Task<(bool hasResult, MonsterDetail/FloorDropDetail result)>` (WPF: `Async<'T option>`;
  DoModalGridSelect's tuple passed through), so DungeonUI's `match mdOpt with Some md ->` becomes `if (r.hasResult) … r.result`. `DoDungeonRoomSelectPopup` is a `Task`
  and takes `usedTransports` as `int[]`, `setNewValue` as `Action<DungeonRoomState>`. `dungeonRoomExplainer` is a `Control`, `setOpacity` an `Action<double>` (one private
  `_dungeonRoomExplainer` tuple field, Graphics' pattern). `THE_DIFF`/`suppressRoomSyncTemporarily` are mutable statics. The room-type grid's click test
  (`ChangedButton` Left/Right and `ButtonState = Pressed`) reads the press's `PointerUpdateKind` (Left/RightButtonPressed). The fire-and-forget
  `async { do! Async.Sleep(250) } |> Async.StartImmediate` (which does nothing else) is `CustomComboBoxes.StartImmediate(Task.Delay(250))`.
- **Dungeon room popups (test at runtime).** They warp the cursor before opening (`SilentlyWarpMouseCursorTo` / `WarpMouseCursorTo`), so they hit the macOS
  warp delay noted under "Early risk checks"; check the cursor lands on the current room type (also after Shift+drag rearranging and moving/resizing the grid)
  and on the entrance-room icons. The captions and the UI-reminder overlay are unwrapped read-only TextBoxes, as in WPF; compare their widths and centring with WPF.
- **DungeonUI C# shapes.** `choice1`..`choice4` build a `HotKeys.DungeonRoomHotKey` (`OfRoomType`/`OfMonsterDetail`/`OfFloorDropDetail`/`OfDoor`); the local copies inside
  `makeDungeonTabs` (WPF: `Choice<…, obj>`, boxing the door response) build the same type, and `justUnmarked` is a nullable `(int, int, DungeonRoomHotKey)`.
  `DoorPosition` is an abstract record (`Horizontal(level, x, y)` / `Vertical(…)`); `DoorChangeInfo` / `RoomChangeInfo` are records with init properties
  (`new DungeonUI.DoorChangeInfo { Level = …, X = …, … }`). `customLevelNameChanged`, `doorChangedEvent`, `roomChangedEvent` are C# events with
  `triggerCustomLevelNameChanged()` (WPFUI calls it), `triggerDoorChanged(info)`, `triggerRoomChanged(info)`; CoopSync's `doorChangedEvent.Publish.Add(f)` → `DungeonUI.doorChangedEvent += f`
  (or `AddDoorChangedListener(f)`). `ganonQuadrant`/`zeldaQuadrant` (F# ref cells) are `int` statics. `AhhGlobalVariables` holds `Action<int>`/`Action`s. `redrawAllRoomsPublic` is an
  `Action` (null for None). `horizontalDoorsPerLevel`/`verticalDoorsPerLevel`/`roomStatesPerLevel`/`setNewValueFunctions` are arrays of 2D arrays (`T[][,]`); `exportFunctionsLarge`
  is `Func<DungeonModel>[]`, `importFunctions` `Action<DungeonModel>[]`. `TrackerLocation` is a C# enum. `FloatHelper.IsBetween` is at namespace level (C# extension methods
  can't be nested). `MakeLocalTrackerPanel` returns `(Canvas, Action, Func<Point>, (Canvas, Action<int, GridClickDismissalWarpReturn>)[])`; `makeOutlineShapes*` return `List<Control>`.
  `makeDungeonTabs` keeps WPF's parameters in order: `(cm, Action<Canvas> layoutF, Func<double> posYF, SharedEvent<int> selectDungeonTabEvent, int TH, Canvas rightwardCanvas,
  SharedEvent<int> levelTabSelected, SharedEvent<bool> blockersHoverEvent, Canvas[] mainTrackerGhostbusters, Func<string, Task> showProgress, Action<int> enterF, Action<int> leaveF)`
  and returns `Task<(Canvas, Point, Border, Func<string[]>, Func<Func<string, Task>, DungeonModel[], Task>)>`.
- **`fortyFiveDegrees` isn't frozen** (Avalonia has no Freeze; one `RotateTransform` is shared, as WPF shares the frozen one). UIComponents/WPFUI use it as a `RenderTransform`:
  Avalonia rotates around the control's centre by default (WPF: its top-left corner), so those callers need `RenderTransformOrigin = TopLeft` to match.
- **Drag-paint (test at runtime, especially on macOS).** WPF's blocking `DragDrop.DoDragDrop(roomCanvas, "L"/"R"/"M", Link)` → Avalonia `DragDrop.DoDragDropAsync(ea, DataTransfer
  with the text, Link)`, waited for in a nested dispatcher frame so the post-drop code (and `DragDropSurface`'s reset) runs after the drop, as in WPF. Rooms get
  `DragDrop.AddDragOverHandler` + `SetAllowDrop` (WPF: `DragOver`, `AllowDrop`) and read the button from `DataTransfer.TryGetText()`. On macOS this is a real OS drag session
  (drag cursor/image may differ, DragOver timing may differ); check painting across rooms with left/right/middle/Shift+left, and that a click (not a drag) still works.
  The drag start reads the buttons from the move's pointer properties and Shift from `ea.KeyModifiers` (WPF: global `Keyboard.IsKeyDown`); door and room clicks read
  `InitialPressMouseButton` (WPF: `ChangedButton`) and Shift from the release's modifiers.
- **Dungeon tabs: WPF metrics for TabControl/TabItem (compare on Windows).** `Graphics.installAppWideStyles` now gives TabItem no minimum size, 6,2 padding and 12px text
  (Fluent: 48px tall, 24px text), and TabControl a 1px #ACACAC border and 2px padding, as WPF's defaults. The tab strip's look (selected-tab indicator, header spacing) still
  differs from WPF; check that the content sits where WPF puts it (`dungeonTabs.Height = content + 30`) and nothing is clipped.
- **Dungeon summary tab: `VisualBrush` previews (test at runtime).** The minis, the hover "midi" preview and the ghostbuster boxes paint other controls with `VisualBrush`, as in
  WPF; the summary tab still parks the level canvases in the 0.0001-opacity `dummyCanvas` so they stay rendered. Avalonia's TileBrush defaults to `Stretch.Uniform` (WPF: Fill), so
  the brushes that relied on WPF's default set `Stretch.Fill`. Check the previews show and update (e.g. LEVEL↔BOARD), and are sized as in WPF.
- **Dungeon summary tab: the small local tracker's 2/3 scale** uses a `LayoutTransformControl` (no `LayoutTransform` in Avalonia), which is what gets docked.
- **Room-change flash** (`animateDungeonRoomTile`): WPF's `ColorAnimation` (alpha 0→180 over 1 s, auto-reversed, on a shared `SolidColorBrush`) is tweened by hand on a 16 ms timer,
  linear; restarting begins again from alpha 0, and it ends holding alpha 0, as WPF's.
- **Custom level name dialog** (right-click the dungeon header): a `Window` shown modally over the header's window (WPF: `Owner = Application.Current.MainWindow`, the same
  window) in a nested dispatcher frame, like `ShowDialogBlocking`. `DialogResult <- true` → `Close(true)`; the Cancel button closes it explicitly (Avalonia's `IsCancel` only
  clicks it). `CharacterCasing.Upper` has no Avalonia equivalent: typed text is upper-cased in a tunnelling `TextInput` handler (pasted text isn't, but the name is
  upper-cased on OK anyway). `Loaded` → `Opened` for focusing the box. The window uses the app's button style; compare its look with WPF.
- **Dungeon tracker small translations.** `levelTab.InvalidateProperty(TabItem.HeaderProperty)` → `InvalidateVisual()` (Avalonia repaints the header's children anyway).
  `ToolTipService.SetInitialShowDelay` → `ToolTip.SetShowDelay`; `SetShowDuration` is the same no-op helper as OptionsMenu's. Sideways scrolling (`Delta.Y = 0`) is ignored on
  doors and rooms (WPF never reports it; it would otherwise step a door or open the floor-drop popup). The vanilla-outline hatch is a `DrawingBrush` with
  `SourceRect`/`DestinationRect` (WPF `Viewbox`/`Viewport`) and one square `LineCap`; compare it. `rainbowBrush` (WPF: angle 90°) runs top to bottom. The hidden-dungeon letter
  button's "Courier New" falls back to another font off Windows (risk 5). `Image.Source.Clone()` for the placeholder icons shares the (immutable) Avalonia bitmap.
- **Avalonia versions pinned** to 11.3.22 (risk 18), with SkiaSharp 2.88.9 (the version Avalonia 11.3 uses).
- **Speech recognition: `Platform.SpeechRecognitionEngine`.** WPF's `SpeechRecognition.speechRecognizer` (a System.Speech `SpeechRecognitionEngine`) is a
  `Platform.SpeechRecognitionEngine` with the members the tracker uses: `LoadGrammar(wakePhrase, choices)` (builds the same `GrammarBuilder(wakePhrase)` +
  `Choices` grammar), `SetInputToDefaultAudioDevice()`, `RecognizeAsyncMultiple()` (WPF `RecognizeAsync(RecognizeMode.Multiple)`; Program.cs calls these two), and
  `event Action<Platform.RecognizedSpeech> SpeechRecognized` (`r.Text`, `r.Confidence`; WPF `r.Result.Text`/`r.Result.Confidence`). On Windows it wraps
  System.Speech.Recognition from the same `System.Speech` NuGet package as synthesis (no new reference). Its constructor prints which engine is used.
  `SpeechRecognitionInstance.ConvertSpokenPhraseToMapCell` returns `int?` (WPF: `int option`); `AttachSpeechRecognizedToApp` takes `Action<string>`;
  `appMainCanvas.Dispatcher.Invoke` → `Dispatcher.UIThread.Invoke` (the canvas parameter is kept, unused). **Test on Windows:** voice commands with and without PTT
  (PTT itself needs the gamepad, still a stub).
- **OverworldMapTileCustomization C# shapes.** `OW_ITEM_GRID_LOCATIONS` is a nested static class; its grid coords are `(int, int)` value tuples and
  `Locate((int, int))` returns `(double, double)`, so `Locate(BOMB_BOX)` reads as in WPF. `MapStateProxy(s)` → `new MapStateProxy(s)`. `overworldAcceleratorTable` is
  `Dictionary<int, Func<CanvasManager, Canvas, int, int, Task>>` (filled by `_init1`..`_init5`, as WPF's top-level `Add`s), `swordCaveAccelerator` returns that `Func`.
  `GetIconBMPAndExtraDecorations` returns `(bool, System.Drawing.Bitmap, List<(Control, double, double)>)`; `computeExtraDecorationArrow` `(Control, double, double)[]`;
  `takenSwordCheck`/`takenProgressiveSwordCheck` `(Canvas, double, double)`; `caveCheckmark` a `Control`. `DoLeftClick` is `Task<(bool, bool)>`, `DoNonShopItemIconPopup`
  `Task<bool>`, `DoRemote/LocalItemComboBox` `Task`. `takeAnyTaken` is `Dictionary<(int, int), (int, bool, int)>`; `twoItemShopBmpCache`/`threeItemShopBmpCache` use value-tuple
  keys; `hintShopColors` is the model's `Dictionary<Tuple<int, int>, int>` (also `shuffledWoodSwordTiles`/`progressiveTileTier` keep `System.Tuple` keys). Hooks:
  `setDecorIconsHook` `Action<int, int, int>`, `goToDungeon` `Action<int>`, `decorBitmapHook` `Func<int, Bitmap>` (null for none), `shopItemBitmapsHook`
  `Func<int, List<(Bitmap, bool)>>`. `armosLeftSideBrightBmp`/`DarkBmp` are `Lazy<Bitmap>` (`.Value` as in F#). `MakeRemainderSummaryDisplay(Ex)` return the `Border`;
  `MakeMappedHotKeysDisplay` returns `(bool noHotKeys, Border)`; its dungeon panel lists `HotKeys.DungeonRoomHotKey` cases (WPF: `Choice1Of4`..`Choice4Of4`).
- **Overworld tile clicks.** The cave-item icons' `MouseDown` (`ButtonState = Pressed` and `ChangedButton` Left/Right) → `PointerPressed` reading `PointerUpdateKind`; the
  non-shop item icon popup's right-click test reads the press's `PointerUpdateKind` (WPF: `ChangedButton = Right`). Tooltip → `ToolTip.SetTip`. The check mark's
  `StrokeStartLineCap`/`StrokeEndLineCap` → one `StrokeLineCap` (both Round).
- **Spot Summary layout (compare on Windows).** `halveGap` measures each `UniformGrid` before it is in the window (as WPF does) to set its `MinWidth`, which sets the whole
  summary's width; the potion-shop "= icon" alignment runs on `LayoutUpdated` with `Bounds.Width` for `ActualWidth` and `TranslatePoint(...).Value` (a null — not in the same
  tree — is swallowed by the same try/catch as WPF's exception). Check the summary's width, the three-column grid, and that the = and potion icon line up.
- **"You have no HotKeys mapped"** opens the file manager with HotKeys.txt selected via `Platform.RevealInFileManager` (WPF: `Explorer.exe /Select,`; same on Windows,
  Finder on macOS, the folder on Linux).
- **LinkRouting C# shapes.** `RouteDestination` (an F# union) is an abstract record with cases `OW_MAP(Spots)` (`List<(int, int)>`), `HINTZONE(Zone, CouldBeLetterDungeon)`,
  `UNMARKEDINSTANCEFUNC(F)` (`Func<int, int, bool>`): WPFUI's `| Some(RouteDestination.OW_MAP(spots)) ->` becomes `case LinkRouting.RouteDestination.OW_MAP(var spots):`.
  `SetupLinkRouting` takes `Action<RouteDestination>`, `Action`, `Func<bool>`, `blockerQueries` as `List<Func<(int, (double, double))?>>` (null for None; UIComponents'
  `MakeBlockers` adds to it), `Action<Canvas, int>`, `Func<bool>`, the sword bitmap, the overworld instance and two `Action<Canvas>`s, and returns
  `(Action stepAnimateLink, Canvas linkIcon, Canvas currentTargetIcon)`. `activatePopup` is an `async Task` started with `CustomComboBoxes.StartImmediate`. `MouseDown`/
  `MouseEnter`/`MouseLeave`/`IsMouseOver` → `PointerPressed`/`PointerEntered`/`PointerExited`/`IsPointerOver`. C# `for` loops share one variable across iterations, so the
  triforce targets copy the loop index for their click closures (F# gives each iteration its own).
- **Link routing popup (compare on Windows).** The explanation is an unwrapped `TextBox` holding `\n`s, as in WPF (check all lines show inside the 444×290 border); the
  crosshair "+" is an 18pt TextBox at (7, 1), as in WPF.
- **Popouts C# shapes.** `PopoutKind` (an F# union with no data) is a C# enum with the same case names (`Popouts.PopoutKind.RemainingItemsPoput`, typo kept). The
  `doUIUpdateEvent` parameters (`makePopout`, `Startup`, the `…Content` functions) are a `Graphics.SharedEvent`, a new non-generic variant of `SharedEvent<T>` for F#'s
  `Event<unit>` (`ev.Trigger()`, `ev.Fired += f`); WPFUI's `doUIUpdateEvent = new Event<unit>()` becomes `new Graphics.SharedEvent()`. `theUIUpdateEvent` is null for None.
  `Initialize(Window, SynchronizationContext)`. ShowRunCustom's `mainWindow`/`leftOf`/`topOf`/`setLeftTop`/`showOwned` port helpers are now public so Popouts can reuse them.
- **Popout windows: positions and sizes (test at runtime).** Left/Top are kept in WPF units (`ShowRunCustom.leftOf`/`setLeftTop`), so the settings strings mean the same.
  Saved sizes are Avalonia's client size (WPF: outer size, frame included), as ShowRunCustom; a size saved by WPF reopens slightly larger. `showHotKeys` keeps WPF's
  `DesiredSize + 16 / + 40` frame allowance, which in Avalonia (client size) leaves a little extra space; compare. Avalonia has no `RestoreBounds`: each popout remembers the
  last bounds seen in the normal state and saves those when minimized. WPF detects "minimized or closing" by `Left < MINIMIZED_THRESHOLD` (Windows moves minimized windows to
  -32000); macOS/Linux don't, so `WindowState == Minimized` counts too. `ContentRendered` → `Opened`; `LocationChanged` → `PositionChanged`; `CanResizeWithGrip` → `CanResize`.
  With no saved position the window opens where Avalonia/the OS puts it (WPF: Windows' default spot); check that it's sensible on each OS.
- **`refocusMainWindow` activates the main window.** WPF's `Window.Focus()` gives the window keyboard focus, which activates it; Avalonia's `Focus()` only moves focus within a
  window, so the port calls `Activate()` then `Focus()`. Check on Windows and macOS that moving/resizing a popout hands the keyboard back to the tracker (hotkeys keep working)
  without side effects (e.g. the main window jumping in front of a popout being dragged).
- **Uncollected Items Ctrl+wheel** is a tunnelling `PointerWheelChanged` handler reading `e.KeyModifiers == Control` and stepping once per `Graphics.wheelNotch`. On macOS,
  Ctrl+scroll is often the system zoom gesture; **Needs Gob:** should Cmd also work there?
- **Shop Prices window.** The price `ComboBox` uses Avalonia's Fluent look (WPF: Windows' dropdown); its black background/orange text are set as in WPF, but the dropdown
  list's colours come from the theme. Compare. The saved size is `Bounds` (client) for WPF's `ActualWidth/Height`.
- **OverworldItemGridUI C# shapes.** `ShowLocatorDescriptor` (an F# union) is an abstract record with cases `DungeonNumber(Value)`, `DungeonIndex(Value)`, `Sword1()`,
  `Sword2()`, `Sword3()`: `ShowLocatorDescriptor.Sword2` → `new OverworldItemGridUI.ShowLocatorDescriptor.Sword2()`, `| ShowLocatorDescriptor.DungeonNumber n ->` →
  `case ShowLocatorDescriptor.DungeonNumber(var n):`. The mutable hooks are `Action`/`Func`s: `showLocatorExactLocation` `Action<int, int>`, `showLocatorExactLocations`
  `Action<List<(int, int)>>`, `showLocatorHintedZone` `Action<HintZone, bool>`, `showLocatorInstanceFunc(All)` `Action<Func<int, int, bool>>`, `addHintZoomHook`
  `Action<Control, int, double>`, `showMarkerItemsLocator` `Action<List<int>>`, `showLocator` `Action<ShowLocatorDescriptor>`, `exportDungeonModelsJsonLines` `Func<string[]>`,
  `hideFeatsOfStrength`/`hideRaftSpots`/`hideFirst/SecondQuestFromMixed`/`setMirroredOverworld` `Action<bool>`. `HintZoneDisplayTextBox` returns `Control`;
  `learnMoreHintDecoration` is a `(Border, double, double)`. `FastHintSelector` is `Task<(bool hasResult, HintZone result)>`; `ApplyFastHintSelectorBehavior(cm, (px, py), Control,
  i, bool, ShowLocatorDescriptor or null, bool)`. `MakeItemGrid(cm, Func<TimelineID, Box, bool, Control> boxItemImpl, List<TimelineItem>, owInstance, Image extrasImage,
  Graphics.SharedEvent resetTimerEvent, bool, Graphics.SharedEvent doUIUpdateEvent, Func<string> makeManualSave)` returns the same 13 values as a value tuple
  (`invokeExtras`, an `Async<unit>`, is a `Func<Task>`; `highlightOpenCavesCB` is null when not standard Hyrule). `MakeFQSQStuff` takes `redrawOWCircle` (WPFUI's F# `ref` of a
  function) as a `StrongBox<Action<int, int>>`, so WPFUI sets `.Value` later as before, and returns `(CheckBox, CheckBox, Button)`. `TrackerModel.SyntheticEventingBool` is built
  with `FuncConvert.FromFunc` (its constructor takes an F# function).
- **Timer text (test at runtime).** `hmsTimeTextBox` and `broadcastTimeTextBox` (42pt, placed by pixel) use `Graphics.matchWpfTextTop`. `hmsTimeTextBox` keeps WPF's
  fixed 56px height; with the extra top padding off Windows, check the digits aren't clipped at the bottom. `niceTimerBox` (WPF `Visibility.Hidden`/`Visible`) uses
  `Opacity` 0/1; it's never hit-test visible, as in WPF.
- **Item grid icons forward clicks (test at runtime).** `rerouteClick` re-raises the icon's `PointerPressed` args on the item box (WPF: re-raises the `MouseDown` args), so
  clicking the ladder/armos/letter/white-sword icon acts like clicking its box. Check left/right/middle clicks on those icons behave as on the boxes, including popups.
- **Item grid input.** Take-any hearts: `MouseLeftButtonDown`/`MouseRightButtonDown` → `PointerPressed` with `PointerUpdateKind`; wheel steps once per `Graphics.wheelNotch`
  (also on the hint boxes' fast hint selector). Spot Summary's `AddHandler(MouseDownEvent, …, true)` → `AddHandler(PointerPressedEvent, …, handledEventsToo: true)`, right button from
  `PointerUpdateKind`. `ToolTipService.SetPlacement(Top)` → `ToolTip.SetPlacement`; `SetShowDuration` is the usual no-op. The alphabetized hint-zone grid sorts the two-letter codes
  ordinally, as F#'s string compare does.

- **UIComponents C# shapes.** `MakeMagnifier(List<Control>, int, Bitmap[,])` returns `(Action<int, int> onMouseForMagnifier, Border, Canvas)`. `MakeLegend(cm, Graphics.SharedEvent)`
  returns `(Canvas, Image, Canvas, Action, Canvas, Canvas, Canvas)` in WPF's order. `itemProgressChanged` is an `Action`, `uncollectMapIcons` an `Action<List<int>>`.
  `MakeItemProgressBar` returns `(Action, Canvas, Button)`; `MakeHintDecoderUI` a `Button`; `hintExplainer` a `Border`. `MakeBlockers(cm, List<Func<(int, (double, double))?>>,
  SharedEvent<int> levelTabSelected, SharedEvent<bool> blockersHoverEvent, Control[], Action<int>, Action<int>)` returns the `Grid`. `MakeZoneOverlay(Canvas, Action, List<Control>)`
  returns `(CheckBox, Action<HintZone, string, double, double> addZoneName, Action<HintZone, bool> changeZoneOpacity, Canvas[,])`. `MakeMouseHoverExplainer` returns `(Button, Canvas)`.
  `legendStartButton` is a `Button`. The file's late `open OverworldMapTileCustomization` is a `using` alias for `OW_ITEM_GRID_LOCATIONS` (a `using static` makes `canvasAdd`/`OMTW` ambiguous).
- **Magnifier tile cache** moved from `<exe folder>\Magnifier\` to `Platform.Files.User("Magnifier", …)`, like the Overworld tile cache; file names unchanged.
- **Magnifier/legend translations (compare on Windows).** The magnified map's `LayoutTransform` (1.02 scale) is a `LayoutTransformControl`. `BlurEffect` has no `KernelType` (Avalonia's
  blur is Gaussian, as WPF's setting). The Start Spot ring and Custom Waypoint icon `ScaleTransform`s and the recorder diamond (`fortyFiveDegrees`) get `RenderTransformOrigin = TopLeft`.
  The Custom Waypoint's `PointAnimationUsingKeyFrames` on the gradient's `EndPoint` (linear, 9 key frames over 4 s, forever) is tweened on a 16 ms timer that never stops, as WPF's
  animation never does. The legend spacing measures text with Avalonia's default font (WPF: `SystemFonts.MessageFontFamily`, which WPF TextBoxes use), so it follows the font
  actually drawn; positions may differ slightly from WPF off Windows.
- **Recorder whistle-order tooltip (test at runtime).** WPF fills a `ToolTip` in its `Opened` event; the port sets the `ToolTip` itself as the tip and fills it in the control's
  `ToolTipOpening` event. Placement Top, show delay 100 ms; `SetShowDuration(60000)` is the usual no-op. Check the tooltip shows the current quest's order.
- **Start Spot / Custom Waypoint placement popup: hotkeys (test at runtime).** WPF's key handler reads `Mouse.GetPosition(element)`; Avalonia has no global mouse position, so the
  popup remembers the last pointer position over the map (cleared when the pointer leaves it). Arrow-key moves and fake clicks work only after the pointer has moved over the map
  (the initial warp to the map's centre produces a move on Windows/macOS, see risk 3).
- **UIComponents input.** Item progress bar `MouseLeftButtonDown`/`MouseRightButtonDown` → one `PointerPressed` handler choosing `onItemClick(1)`/`onItemClick(-1)` by
  `PointerUpdateKind`; blocker boxes' Shift+left-click reads `KeyModifiers` (WPF: `Keyboard.IsKeyDown`); hint decoder and blocker wheels step once per `Graphics.wheelNotch`.
  The blocker "Applies to..." panel's and the Other Hints section's type-keyed `Resources` styles are `Styles` on the same panel. `dash_bracelet`/`dash_raft`
  `Visibility.Collapsed` → `IsVisible`. "Mouse Hover Explainer" (30pt, placed by pixel) uses `Graphics.matchWpfTextTop`.

- **HUDs: unreachable, as in WPF.** WPFUI calls `HUDs.MakeHUDs` only under `#if NOT_RACE_LEGAL`, which neither WPF's build nor ours defines, and inside it the two HUD makers
  are hooked up only under the same `#if`. The C# keeps those `#if`s. `MakeHUDs(cm, Graphics.SharedEvent<VisualBrush> trackerDungeonMoused,
  Graphics.SharedEvent<(DungeonUI.TrackerLocation, int, int)> trackerLocationMoused)`.
- **HUD windows (untested, unreachable).** `WindowStyle.None` + `AllowsTransparency` → `SystemDecorations.None` + `TransparencyLevelHint = Transparent`; `ResizeMode` →
  `CanResize`; `Owner` → `ShowRunCustom.showOwned`; `LocationChanged` → `PositionChanged`; positions through `ShowRunCustom.leftOf`/`topOf`/`setLeftTop` (WPF units).
  WPF reads framed windows' outer `Width`/`Height`; the port reads `FrameSize` (outer) there and sets/saves the client size (`OverlayLocatorWindowLTWH`, as Popouts). The
  `±8`/`-16` fudges (Windows 10's invisible resize borders) are kept as is, so the HUD/minimap placement next to the control/sizer window may be off by a few pixels on
  macOS/Linux. The view rectangles' `VisualBrush` Viewbox (absolute) → `SourceRect` (absolute), `Stretch.None`. The "minimized" check is `Left < MINIMIZED_THRESHOLD` or
  `WindowState == Minimized`, as Popouts.

## Decisions needed from TheGobOne

From the risks in [PLAN.md](./PLAN.md#risks-to-full-parity). Nothing here counts as agreed until
TheGobOne says so.

| Risk | Question | Answer |
|---|---|---|
| 1 | Is X11 the supported Linux setup, and which Wayland limits are acceptable? | |
| 2 | Is a Mac-only alternative to middle-click and the thumb button (e.g. Option-click) OK? | |
| 4 | Is "same voice commands, different speech engine" acceptable? | |
| 5 | Are close-but-not-identical fonts acceptable where Windows fonts can't be used? | |
| 8 | Is our own "show mouse location" ring OK in place of the Windows one? | |
| 9 | Whose Apple Developer account signs and notarises the Mac build? | |
| 10 | Where should user files live on macOS (`~/Library/Application Support/ZoraTracker`)? | |
| – | What's on the cut-over checklist before WPF is retired? | |

## Agreed platform deviations

Differences TheGobOne has explicitly approved. Anything not listed here is a parity gap.

(none yet)

## Proposed upstream PRs to TheGobOne

- **Settings file location.** `TrackerModelOptions.readSettings`/`writeSettings` always use `AppDomain.CurrentDomain.BaseDirectory/Z1R_Tracker_settings.json`.
  A way to set the folder (e.g. a `settingsDirectory` the app sets at startup) would let the cross-platform tracker keep settings in
  `Platform.UserDataDir()` on macOS/Linux, unchanged on Windows. OptionsMenu's "Reset All Settings" would then delete that file.
