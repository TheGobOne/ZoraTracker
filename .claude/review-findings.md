# ZORATracker review findings (open)

Scope: all work since v3.2 (v3.2..d7c7e20). Six read-only agents, 2026-11-26. Fixed items were removed from this list. Commits: `1d2e081` (Critical and High fixes), `ed89633` (quick fixes), `533e944` (status update). Release and web Debug build with 0 errors. Self-test and live save-and-reload not run.

## Critical and High

- [ ] 60: `Z1R_Avalonia/CoopSync.cs:1001`, `Z1R_Tracker/TrackerModel.fs:904`: co-op. An item in the host's Anywhere spot uses up its cap. The partner marks the same item in a dungeon box, the host cannot apply it, resends its empty box, and the partner's box is cleared. Spots sit outside the synced box list on purpose, so the fix is a design change. About 45 minutes.
- [ ] 64: `Z1R_Avalonia/SelfTest.cs:532`, `:551`: the shuffle test uses the AP Item, which has 0 uses while Archipelago Items is off (the default). The test fails, skips its restores and the 4th Sword slot tests, and leaves Anywhere on. About 10 minutes.

## Medium

- [ ] 66: `Z1R_Avalonia/Popouts.cs:527`: the shuffle window's picker skips the main menu's rules. It offers the 4th sword with 4th Sword off, and items you already have. About 20 minutes.
- [ ] 68: `Z1R_Avalonia/Popouts.cs:602`: each open of the shuffle window probably leaves a set of box views subscribed, so redraw cost grows (inferred from Avalonia behavior, not run). About 5 minutes.

## Low and cleanup

- [ ] 71: `Z1R_Avalonia/DungeonUI.cs:459`: the Map hotkey does nothing visible when shuffle gives the map, and can clear the level's own map. About 5 minutes.
- [ ] 72: `Z1R_Tracker/TrackerModel.fs:2407`, `Popouts.cs:474`: picking the same triforce and level again moves its timeline time to now. About 15 minutes.
- [ ] 74: `Z1R_Avalonia/Program.cs:3085`: Paste Flags ignores Random shuffle flags when deciding to untick Legacy Mode. About 5 minutes.
- [ ] 75: `Z1R_Avalonia/Program.cs:3136`: Paste Flags ticks a shuffle box without saying a Shuffle Type is needed. About 5 minutes.
- [ ] 76: `Z1R_Avalonia/OptionsMenu.cs:786`: a tour Continue blocked by the Shuffle Type box leaves the main tour pending, so it opens after a later Start or Load. About 10 minutes.
- [ ] 77: `Z1R_Avalonia/SelfTest.cs:544`: the test zeroes marker counts instead of restoring them; line 555 can pass without testing; `:576` calls the caps twice. About 10 minutes.
- [ ] 78: `Z1R_Avalonia/Popouts.cs:838`: on Mac or Linux the popout may drop behind the main window (low confidence, not run).

## Deferred: co-op Medium and Low

Only Critical and High co-op findings get fixed (decided 2026-11-25).

- [ ] 50: `Z1R_Avalonia/CoopSync.cs` (`beginMerge`): the "unversioned 8" filter also drops a real 8 from a 3.2 partner's save.
- [ ] 52: `Z1R_Avalonia/CoopSync.cs` (`endMerge`): a normal per-shop merge still counts as a failed apply.
- [ ] 80: `Z1R_Tracker/TrackerModel.fs:897`: 3.2 and older partners show shop shuffle markers as heart containers.
- [ ] 81: `Z1R_Avalonia/Program.cs:2232`: host startup options that arrive while the Shuffle Type box is up are dropped.

## Before the next release

- [ ] At ZORA's 2.0.2 release, recheck the format 23 flag table (`Z1R_Avalonia/FlagPaste.cs:65`). Released ZORA is still 2.0.1 / 22; the table matches the test build.

## Not reviewed

Running app, live co-op, self-test run, web page in a browser, web Release/AOT, Mac and Linux runs, WPF beyond crash and data loss reading.
