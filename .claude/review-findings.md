# ZORATracker review findings

Scope: all work since v3.2 (v3.2..d7c7e20). Six read-only agents, 2026-11-26. Findings come from reading code. Nothing was reproduced in the running app. 57-60, 63 and 64 were checked against the source after the review. The web Debug build compiles with 0 errors. Numbers 1-56 are from earlier reviews. Shuffle is not in the released 3.2, so the shuffle findings affect unreleased code only.

## Status

Last updated 2026-11-26. Commits: `1d2e081` (Critical and High fixes), `ed89633` (quick fixes 69, 70, 79). Release and web Debug build with 0 errors. Self-test and live save-and-reload not run.

- Critical: 57, 58, 59 fixed. 60 open (co-op: spots sit outside the synced box list on purpose, so the fix is a design change).
- High: 61, 62, 63, 65 fixed. 64 open (self-test uses the AP Item, which is 0 uses by default). 62 keeps shop markers as intended.
- Medium and Low: 69, 70, 79 fixed. Rest open.

## Decided by the user

- 58: a Shuffle Type with no shuffle box ticked is ignored. Fixed.
- 65: a dungeon box may not hold a triforce while triforces are shuffled. Fixed.
- `Z1R_Avalonia/Popouts.cs:541`: an ordinary item picked in the shuffle window starts uncollected. Intended. No change.

## Critical

- [x] 57: `Z1R_Avalonia/OverworldItemGridUI.cs:1037`: Reset Tracker tests `PlayerHasTriforce()` (includes shuffle credit) but toggles the level's own flag. A level lit only by a slot, spot or shop marker gets its triforce turned on by Reset, and it is saved. About 5 minutes.
- [x] 58: `Z1R_Avalonia/Program.cs:2431`, `Z1R_Tracker/TrackerModel.fs:1095`: a Shuffle Type applies with both shuffle boxes off (Paste Flags unticks only the boxes; remembered selection keeps the type). Completion then needs maps, compasses and spots, no notice shows, and no level completes. About 10 minutes.
- [x] 59: `Z1R_Avalonia/MainTrackerTriforcesAndItems.cs:1089`: web only. The start screen offers shuffle, but the M/C/Tri Shuffle window is hidden. Levels cannot complete. About 20 minutes.
- [ ] 60: `Z1R_Avalonia/CoopSync.cs:1001`, `Z1R_Tracker/TrackerModel.fs:904`: co-op. An item in the host's Anywhere spot uses up its cap. The partner marks the same item in a dungeon box, the host cannot apply it, resends its empty box, and the partner's box is cleared. Agent rated High, medium confidence on how often. About 45 minutes.

## High

- [x] 61: `Z1R_Avalonia/Views.cs:438`, `WPFUI.cs:4132`, `OverworldItemGridUI.cs:897`, `DungeonUI.cs:363`: clicking a triforce lit only by shuffle credit changes the hidden own flag with no visible change. The level can then dim as complete, and stays lit after the credit is removed. About 20 minutes.
- [x] 62: `Z1R_Avalonia/OverworldItemGridUI.cs:1033`: Reset Tracker does not clear the window's collected slots or shop shuffle markers, so map, compass and triforce credit survive a reset. Marker part may be intended. About 15 minutes.
- [x] 63: `Z1R_Avalonia/Program.cs:2170`: the ZORA Extras check-all box ticks both Shuffle Types, which leaves the experimental Anywhere mode on and the master box half-checked. About 5 minutes.
- [ ] 64: `Z1R_Avalonia/SelfTest.cs:532`, `:551`: the shuffle test uses the AP Item, which has 0 uses while Archipelago Items is off (the default). The test fails, skips its restores and the 4th Sword slot tests, and leaves Anywhere on. About 10 minutes.
- [x] 65: `Z1R_Avalonia/Views.cs:913`, `MainTrackerTriforcesAndItems.cs:1249`, `CustomComboBoxes.cs:1769`: with shuffle on, the hotkey, header right click and main menu can still put a triforce in a dungeon box, which credits that dungeon. The shuffle window forbids this. Needs your call. About 30 minutes.

## Medium

- [ ] 66: `Z1R_Avalonia/Popouts.cs:527`: the shuffle window's picker skips the main menu's rules. It offers the 4th sword with 4th Sword off, and items you already have. About 20 minutes.
- [ ] 67: `Z1R_WPF/WPFUI.fs:4106`: WPF loads a save with Anywhere spots without applying them, then saves them empty (data loss, needs an Avalonia save opened in WPF). About 5 minutes.
- [ ] 68: `Z1R_Avalonia/Popouts.cs:602`: each open of the shuffle window probably leaves a set of box views subscribed, so redraw cost grows (inferred from Avalonia behavior, not run). About 5 minutes.
- [x] 69: `Z1R_Avalonia/WPFUI.cs:434`: the shop picker's disabled tip is never reset. In Legacy Mode the shuffle window picker then greys out most items and the red X. About 5 minutes.
- [x] 70: `Z1R_Avalonia/WPFUI.cs:1134`: web only. The shuffle notice has no browser guard and throws. The Race Mode notice has the same older defect. About 5 minutes.

## Low and cleanup

- [ ] 71: `Z1R_Avalonia/DungeonUI.cs:459`: the Map hotkey does nothing visible when shuffle gives the map, and can clear the level's own map. About 5 minutes.
- [ ] 72: `Z1R_Tracker/TrackerModel.fs:2407`, `Popouts.cs:474`: picking the same triforce and level again moves its timeline time to now. About 15 minutes.
- [ ] 73: `Z1R_WPF/MainTrackerTriforcesAndItems.fs:357`, `OverworldItemGridUI.fs:731`, `WPFUI.fs:381`: WPF toggles the own triforce flag after testing the credited value (needs a shuffle save opened in WPF). About 10 minutes.
- [ ] 74: `Z1R_Avalonia/Program.cs:3085`: Paste Flags ignores Random shuffle flags when deciding to untick Legacy Mode. About 5 minutes.
- [ ] 75: `Z1R_Avalonia/Program.cs:3136`: Paste Flags ticks a shuffle box without saying a Shuffle Type is needed. About 5 minutes.
- [ ] 76: `Z1R_Avalonia/OptionsMenu.cs:786`: a tour Continue blocked by the Shuffle Type box leaves the main tour pending, so it opens after a later Start or Load. About 10 minutes.
- [ ] 77: `Z1R_Avalonia/SelfTest.cs:544`: the test zeroes marker counts instead of restoring them; line 555 can pass without testing; `:576` calls the caps twice. About 10 minutes.
- [ ] 78: `Z1R_Avalonia/Popouts.cs:838`: on Mac or Linux the popout may drop behind the main window (low confidence, not run).
- [x] 79: `doc/release-notes-3.2.md:3`: the committed draft says 3.2 is not published. It is. About 2 minutes.

## Deferred: co-op Medium and Low

Only Critical and High co-op findings get fixed (decided 2026-11-25).

- [ ] 50: `Z1R_Avalonia/CoopSync.cs` (`beginMerge`): the "unversioned 8" filter also drops a real 8 from a 3.2 partner's save.
- [ ] 52: `Z1R_Avalonia/CoopSync.cs` (`endMerge`): a normal per-shop merge still counts as a failed apply.
- [ ] 80: `Z1R_Tracker/TrackerModel.fs:897`: 3.2 and older partners show shop shuffle markers as heart containers.
- [ ] 81: `Z1R_Avalonia/Program.cs:2232`: host startup options that arrive while the Shuffle Type box is up are dropped.

## Before the next release

- [ ] At ZORA's 2.0.2 release, recheck the format 23 flag table (`Z1R_Avalonia/FlagPaste.cs:65`). Released ZORA is still 2.0.1 / 22; the table matches the test build.

## Closed

- [x] 1-49, 51, 53-56: fixed before 3.2.
- [x] 3.2 release steps: published with the Windows zip.

## Looked clean

- 3.1 and 3.2 saves load. New save fields and spot tiers round trip; load order is right.
- New item ids 49-76 do not collide, and array bounds hold.
- 4th Sword enables Progressive Items, not the reverse.
- Version strings match v3.2; build, workflow and platform files are unchanged.
- Co-op triforce, map and box tier sync.
- No headless run hangs on the new popups.
- Mirrored overworld, marker bitmaps, popout column layout.

## Not reviewed

Running app, live co-op, self-test run, web page in a browser, web Release/AOT, Mac and Linux runs, WPF beyond crash and data loss reading.
