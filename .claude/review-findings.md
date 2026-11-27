# ZORATracker review findings (open)

Scope: all work since v3.2 (v3.2..5ab87f6). Latest pass 2026-11-27, six read-only agents. Co-op is open to all severities (rule lifted 2026-11-27). 60 to 81 come from the 2026-11-26 pass (fixed items removed), 82 on from the latest. "Not rechecked" means only the agent read that code. Line numbers are from 5ab87f6. Quick fixes on 2026-11-27 (uncommitted) cleared 64, 66, 68, 71, 76, 77, 81, 82, 84, 86, 89, 92, 95, 96, 99 and most of 83: Release and web Debug build with 0 errors, none run in the app, self-test not run. The review itself ran only a web Debug build.

## Critical and High

- [ ] 60: `Z1R_Avalonia/CoopSync.cs:1001`, `Z1R_Tracker/TrackerModel.fs:904`: co-op. An item in the host's Anywhere spot uses up its cap. The partner marks the same item in a dungeon box, the host cannot apply it, resends its empty box, and the partner's box is cleared. Spots sit outside the synced box list on purpose, so the fix is a design change. About 45 minutes.

## Medium

- [ ] 87: `Z1R_Tracker/TrackerModel.fs:1095-1098`: needs your call. Completion ignores which kinds are shuffled: with only Triforces ticked, a level still needs its compass marked; with Anywhere, levels 1-8 need no triforce even when triforces are not shuffled. About 30 minutes.
- [ ] 88: `Z1R_Tracker/TrackerModel.fs:2482`, `Z1R_Avalonia/MainTrackerTriforcesAndItems.cs:466`: not rechecked. Anywhere spot items get no timeline entry and do not light the upper grid boxes (blue ring, wood arrow, blue candle, swords). About 30 minutes.
- [ ] 90: `Z1R_Tracker/SaveAndLoad.fs:28`: not rechecked. A ZORA-made tracker file has no `Shuffle` key, so loading it forces shuffle off even with the boxes ticked. Old saves must still load as off. About 20 minutes.

- [ ] 80: `Z1R_Tracker/TrackerModel.fs:897`: 3.2 and older partners show shop shuffle markers as heart containers.
- [ ] 101: `Z1R_Tracker/TrackerModel.fs:901`: Between Levels window slots are in no co-op message, so the partner's triforce count and level completion fall behind. Shop markers do sync.

## Low and cleanup

- [ ] 72: `Z1R_Tracker/TrackerModel.fs:2407`, `Popouts.cs:473`: picking the same triforce and level again moves its timeline time to now. About 15 minutes.
- [ ] 78: `Z1R_Avalonia/Popouts.cs:838`: on Mac or Linux the popout may drop behind the main window (low confidence, not run).
- [ ] 83: `Z1R_Tracker/SaveAndLoad.fs:506`, `:536`: web only, not rechecked. A desktop shuffle save loaded on web still restores the hidden spots, which can use up an item's cap, and window slots marked "?" light their own row's level. Shuffle itself now stays off. About 20 minutes.
- [ ] 91: `Z1R_Avalonia/OverworldItemGridUI.cs:1033`: not rechecked. Reset Tracker leaves collected shop triforce, map and compass markers, so the level stays lit. About 10 minutes.
- [ ] 93: `Z1R_Avalonia/Views.cs:336`, `:437`: not rechecked, needs your call. With triforces shuffled, right click on a header triforce marks it collected. About 10 minutes.
- [ ] 94: `Z1R_Avalonia/CustomComboBoxes.cs:1676`: not rechecked, depends on ZORA seeds. Under Anywhere, coast, armos and white sword boxes cannot record a triforce, map or compass. About 45 minutes.
- [ ] 97: `Z1R_Tracker/TrackerModel.fs:991`: not rechecked. Item progress divides by 20, but Anywhere adds 27 spots, so colors are wrong late in a run. About 10 minutes.
- [ ] 98: `Z1R_Avalonia/MainTrackerTriforcesAndItems.cs:187`, `:227`: not rechecked, needs your call. Item Locations does not list Anywhere spots. About 20 minutes.
- [ ] 100: `Directory.Build.Props`: not verified, predates v3.2. The capital P probably makes the Linux runner skip it. About 5 minutes.

- [ ] 102: `Z1R_Avalonia/Program.cs` (Paste Flags): web only. A paste still reports the hidden shuffle boxes as checked, though Start unticks them. About 5 minutes.
- [ ] 50: `Z1R_Avalonia/CoopSync.cs` (`beginMerge`): the "unversioned 8" filter also drops a real 8 from a 3.2 partner's save.
- [ ] 52: `Z1R_Avalonia/CoopSync.cs` (`endMerge`): a normal per-shop merge still counts as a failed apply.

## Before the next release

- [ ] At ZORA's 2.0.2 release, recheck the format 23 flag table (`Z1R_Avalonia/FlagPaste.cs:65`). `upstream/main` was `8e3a612` on 2026-11-25. The newer test build differs from the tracker (85).
- [ ] Bump the version: `CurrentReleaseLabel` and `GlobalAssemblyInfo.fs` still say 3.2.
- [ ] Run the release workflow by hand on master to check the Mac and Linux builds.

## Not reviewed

Running app, live co-op, self-test run, web page in a browser, web Release/AOT, Mac and Linux runs, WPF beyond crash and data loss reading.
