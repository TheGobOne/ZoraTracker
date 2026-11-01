# Avalonia port — progress

Caught up to WPF commit: `9292b0b` (v17hotfxi, 2026-10-30)

Parity: **not reached.** The goal is complete feature parity with the WPF tracker (see
[PLAN.md](./PLAN.md#goal-complete-feature-parity)). A file marked `stubbed` or `skipped` is still
a parity gap until its stub is replaced or the gap is approved under "Agreed platform deviations".

## Files

Status: `todo` · `ported` · `stubbed` (temporary) · `skipped` (needs a logged reason)

| # | WPF file | Lines | Phase | Status | Notes |
|---|---|---|---|---|---|
| – | Platform.fs (new) | – | 0 | todo | |
| 1 | Gamepad.fs | 59 | 0 | todo | stub |
| 2 | Graphics.fs | 1634 | 0 | todo | SkiaSharp rewrite; cursor warp, fake clicks, mouse-location ring and sounds stubbed |
| 3 | CustomMessageBox.fs | 66 | 0 | todo | |
| 4 | OverworldRouteDrawing.fs | 268 | 1 | todo | |
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

(none yet)

## Judgement calls / needs a human

(none yet)

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
