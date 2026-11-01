# Avalonia port — progress

Caught up to WPF commit: `9292b0b` (v17hotfxi, 2026-10-30)

Parity: **not reached.** The goal is complete feature parity with the WPF tracker (see
[PLAN.md](./PLAN.md#goal-complete-feature-parity)). A file marked `stubbed` or `skipped` is still
a parity gap until its stub is replaced or the gap is approved under "Agreed platform deviations".

## Files

Status: `todo` · `ported` · `stubbed` (temporary) · `skipped` (needs a logged reason)

| # | WPF file | Lines | Phase | Status | Notes |
|---|---|---|---|---|---|
| – | Platform.fs (new) | – | 0 | ported | platform layer: user-file location, open-with-shell, cursor warp, fake clicks, sounds (see Stubs) |
| – | Drawing.fs (new) | – | 0 | ported | `Z1RDrawing.Bitmap`/`Icon`/`SystemIcons`: cross-platform stand-ins for the System.Drawing types, on SkiaSharp |
| 1 | Gamepad.fs | 59 | 0 | stubbed | no gamepad is ever found |
| 2 | Graphics.fs | 1634 | 0 | ported | sounds stubbed; Linux cursor warp/fake clicks stubbed |
| 3 | CustomMessageBox.fs | 66 | 0 | ported | `ShowDialogBlocking()` replaces `Owner`+`ShowDialog()` at call sites |
| 4 | OverworldRouteDrawing.fs | 268 | 1 | ported | straight translation; `drawPathsImpl` takes an `Avalonia.Point` |
| 5 | DungeonRoomState.fs | 259 | 1 | todo | |
| 6 | HotKeys.fs | 606 | 1 | todo | global hotkeys stubbed |
| 7 | Timeline.fs | 331 | 1 | todo | |
| 8 | CustomComboBoxes.fs | 1418 | 1 | todo | update install, opening folders stubbed |
| 9 | PieMenus.fs | 915 | 1 | todo | |
| 10 | ShowRunCustom.fs | 225 | 1 | todo | |
| 11 | Dungeon.fs | 1226 | 1 | todo | co-op host launch (Job Object) stubbed until phase 4 |
| 12 | OptionsMenu.fs | 1143 | 1 | todo | |
| 13 | Views.fs | 610 | 1 | todo | |
| 14 | DungeonSaveAndLoad.fs | 162 | 1 | todo | |
| 15 | DungeonHighlightsUI.fs | 200 | 1 | todo | |
| 16 | DungeonPopups.fs | 406 | 1 | todo | |
| 17 | DungeonUI.fs | 2554 | 1 | todo | |
| 18 | SpeechRecognition.fs | 102 | 4 | todo | stub in phase 2 (compile order), real in phase 4 |
| 19 | OverworldMapTileCustomization.fs | 1545 | 2 | todo | |
| 20 | LinkRouting.fs | 240 | 2 | todo | |
| 21 | Popouts.fs | 547 | 3 | todo | |
| 22 | OverworldItemGridUI.fs | 1034 | 2 | todo | |
| 23 | UIComponents.fs | 1536 | 2 | todo | |
| 24 | HUDs.fs | 392 | 3 | todo | |
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

Already real, not stubs: cursor warping and fake clicks/scrolls on Windows (the same user32 calls as WPF) and macOS
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

## Judgement calls / needs a human

- **System.Drawing → `Z1RDrawing.Bitmap`.** `System.Drawing.Bitmap` only works on Windows in .NET, so `Drawing.fs`
  provides a stand-in with the same members, backed by a plain ARGB array (decoding/encoding through SkiaSharp). The
  ported files only change `System.Drawing.Bitmap` to `Z1RDrawing.Bitmap`; `System.Drawing.Color` is cross-platform and
  stays. Overloaded constructors (`new Bitmap(stream)`, `(filename)`, `(src)`, `(src, size)`) match System.Drawing's.
  Checked by rendering a sheet of ~150 sprites and the full overworld map: all correct.
- **Overworld tile cache** moved from `<exe folder>\Overworld\` to `Platform.UserDataDir()/Overworld/` (same as the exe
  folder on Windows). File names are unchanged. Like System.Drawing's, the `.bmp` files are PNG data.
- **Message box icons.** Windows' own icons can't ship on other systems, so `Z1RDrawing.SystemIcons` draws look-alikes
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
- **Avalonia versions pinned** to 11.3.22 (risk 18), with SkiaSharp 2.88.9 (the version Avalonia 11.3 uses).

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

(none yet)
