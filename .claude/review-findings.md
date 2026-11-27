# ZORATracker review findings (open)

Scope: whole repo, with the changes in v3.2..90ddeba checked first. Pass of 2026-11-27, six read-only agents, not every older file section reread (see "Not reviewed"). Line numbers are from 90ddeba. "Confirmed" means the lines were read a second time during the merge. "Not rechecked" means only the agent read that code. Nothing was run in the app. 103 and up are new. Co-op is open to all severities (rule lifted 2026-11-27).

## Critical (ordinary desktop use)

- [x] 103: `Z1R_Avalonia/CustomComboBoxes.cs:1719-1721`: fixed on reading, Release build 0 errors, not run. A box holding a map or compass now shows it as the menu's current cell, so a second click keeps it.
- [x] 104: `Z1R_Avalonia/OverworldItemGridUI.cs:1242-1255`, `Z1R_Avalonia/Views.cs:319-347`: fixed on reading, Release build 0 errors, not run. Moving the 4th Sword slot and restoring a replaced item keep the level tag.
- [x] 87: `Z1R_Tracker/TrackerModel.fs:1113-1116`: fixed on reading, Release build 0 errors, not run, self-test not run. A level's compass is required only when compasses are shuffled. Under Anywhere, the triforce is waived and a spot is required only for shuffled kinds. The map is still required under Between Levels and Anywhere, as before.

## High (needs a partner, the web build or mixed settings)

- [ ] 106: `Z1R_Avalonia/CoopSync.cs:2088`, `:1037-1048`: not rechecked. A 3.2 or 3.0.4 partner cannot apply item ids 49/50, resends its old value, and our map or compass box is cleared with no notice. Two current trackers may do the same if only one has Anywhere on (not checked). About 60 minutes.
- [ ] 107: `Z1R_Avalonia/CoopSync.cs:327-331`, `:1038`, `:1060`, `Z1R_Avalonia/Program.cs:1111`: not rechecked, medium-high confidence. A 3.2 partner sends no level tag, so a reconnect or box edit untags our box and can mark the wrong level's triforce. About 45 minutes.
- [ ] 105: `Z1R_Avalonia/Views.cs:818-827`: confirmed. Moving a triforce matches one tagged for another level. Anywhere with triforces not shuffled: placing a Triforce Piece on level 5's basement box removes the level 3 tagged piece from level 5's floor box. About 5 minutes.
- [ ] 113: `Z1R_Avalonia/Views.cs:847-849`, `Z1R_Avalonia/Popouts.cs:549-556`: not rechecked, medium confidence (assumes the menu stays open). Open the item menu, set that item in another box (popout or co-op partner), then pick it: `box.Set` throws and the app closes. About 10 minutes.
- [ ] 112: `Z1R_Avalonia/Program.cs:3153-3166`: not rechecked. Flags with different shuffle types for triforces and maps keep the old tick, so Start proceeds with the wrong type and maps may not fit item boxes. About 15 minutes for the prompt.
- [ ] 108: `Z1R_Avalonia/WPFUI.cs:591`, `:6126`, `Z1R_Tracker/SaveAndLoad.fs:39`: not rechecked. Loading a desktop Anywhere save on web drops spot items, boxed maps and compasses and tagged triforces, and a web save makes the loss permanent. Reopens part of 83. About 20 minutes to refuse such saves.
- [ ] 109: `Z1R_Avalonia/WPFUI.cs:594`: not rechecked. Web load clears collected window slots with no chosen level, including kinds that were not shuffled. About 10 minutes.
- [ ] 110: `Z1R_Avalonia/CoopSync.cs:630-634`: not rechecked. A partner's Between Levels slots are applied when our shuffle is off (always on web), crediting maps. About 10 minutes.
- [ ] 111: `Z1R_Avalonia/CoopSync.cs:611`: not rechecked, medium confidence. A web partner in an Anywhere game ignores spot items and boxed maps with no notice. About 20 minutes.

## Medium

- [ ] 116: `Z1R_Tracker/TrackerModel.fs:1109`: confirmed. `boxDone` counts a triforce tagged for another level once this level's header is on, so the level can complete early. About 5 minutes.
- [ ] 114: `Z1R_Avalonia/Program.cs:3546`, `:3598`: not rechecked, predates v3.2. Crash recovery with a remembered Item Shuffle Off start can leave preview boxes holding heart uses, so hearts fail to load and the next autosave overwrites the good one. About 10 minutes.
- [ ] 115: `Z1R_Tracker/SaveAndLoad.fs:52`, `:374`: not rechecked, predates v3.2. A triforce held in an item box is also saved as the header flag, so clearing the box after a load leaves the level counted. About 15 minutes.
- [ ] 97: `Z1R_Tracker/TrackerModel.fs:999`, `:1007`: not rechecked. Item progress divides by 20, but Anywhere adds 27 spots, so it reaches 100% early. About 10 minutes.
- [ ] 80: `Z1R_Avalonia/CoopSync.cs:1490-1501`: needs your call. 3.2 partners show shop shuffle markers as heart containers; fairy id 102 probably too (unconfirmed). Fix is to stop sending those ids to old partners. About 30 minutes.
- [ ] 117: `Z1R_Avalonia/MainTrackerTriforcesAndItems.cs:332-345`: not rechecked, predates v3.2. Item Locations does not rebuild on the 1Q/2Q dungeon toggle. About 10 minutes.

## Low and cleanup

- [ ] 118: `Z1R_Avalonia/Views.cs:342-355`: not rechecked. Header right click can pick an uncollected tagged triforce, take the click and change nothing. About 5 minutes.
- [ ] 93: `Z1R_Avalonia/Views.cs:336`, `:437`: not rechecked, needs your call. With triforces shuffled, right click on a header triforce marks it collected. About 10 minutes.
- [ ] 94: `Z1R_Avalonia/CustomComboBoxes.cs:1676-1689`, `Z1R_Avalonia/Views.cs:838`: not rechecked, depends on ZORA seeds. Under Anywhere, coast, armos and white sword boxes cannot record a triforce, map or compass. About 45 minutes.
- [x] 119: `Z1R_Avalonia/OverworldItemGridUI.cs:1307-1315`: fixed on reading, Release build 0 errors, self-test not run. Your call: the 4th sword never appears with Progressive Items off. A pre-option save with Progressive Items off now loads with the 4th sword off. Items already marked in its boxes stay. About 10 minutes.
- [ ] 120: `Z1R_Tracker/TrackerModel.fs:830-843`: not rechecked, medium confidence. Scrolling a box under Anywhere may stop on a map or compass with no level. About 15 minutes.
- [ ] 121: `Z1R_Tracker/SaveAndLoad.fs:279-300`: not rechecked. A guessed heart in a shuffle spot reloads as a user-placed heart. About 10 minutes.
- [ ] 98: `Z1R_Avalonia/MainTrackerTriforcesAndItems.cs:186-191`, `:226-231`: needs your call. Item Locations does not list Anywhere spots. About 20 minutes.
- [ ] 125: `Z1R_Tracker/SaveAndLoad.fs:504-513`: not rechecked, may be intended. A ZORA-made tracker file ignores its `ZoraFlags` for shuffle settings. About 30 minutes.
- [ ] 122: `Z1R_Avalonia/HotKeys.cs:562-579`: not rechecked, predates v3.2. Item descriptions are cached, so Book keeps its name after switching to boomstick. About 10 minutes.
- [ ] 123: `Z1R_Avalonia/Popouts.cs:489`, `:555`: not rechecked, low confidence. After a popout click, map hotkeys may do nothing until the main window is clicked. About 15 minutes.
- [ ] 124: `Z1R_Avalonia/MainTrackerTriforcesAndItems.cs:626-627`: not rechecked. 27 hidden spot views redraw all session. About 15 minutes.
- [ ] 126: `Z1R_Avalonia/CoopSync.cs:2246-2273`: not rechecked. Shuffle window edits do not count as local state, so a late joiner gets no snapshot. About 10 minutes.
- [ ] 127: `Z1R_Avalonia/CoopTest.cs`: no co-op test covers spots, slots or tagged boxes. About 45 minutes.
- [ ] 128: `Z1R_Avalonia/SelfTest.cs:437-441`, `:631-648`: not rechecked. The shuffle flags are now restored. The self-test still does not restore the tagged boxes after a mid-block failure. About 10 minutes.
- [ ] 129: `Z1R_Avalonia/Program.cs:2698-2700`, `Z1R_Avalonia/LoadDump.cs:57`: not rechecked, predates v3.2. A failed self-test startup writes to a null path and hides the report. About 2 minutes.
- [ ] 130: `Z1R_Avalonia/Program.cs:3099`: not rechecked. Web Paste Flags does not say that shuffle is untracked. About 5 minutes.
- [ ] 78: `Z1R_Avalonia/Popouts.cs:393`, `:442`, `:878`: low confidence, not run. On Mac or Linux the popout may drop behind the main window.
- [ ] 50: `Z1R_Avalonia/CoopSync.cs:850-853`: the "unversioned 8" filter also drops a real 8 from a 3.2 partner's save. Left open: both look the same on the wire.

## Before the next release

- [x] Fix 103 and 104 before release notes say that any dungeon box takes a map, compass or triforce. Fixed, still to be checked on screen.
- [ ] Check the shuffle popout on screen.
- [ ] At ZORA's 2.0.2 release, recheck the format 23 flag table (`Z1R_Avalonia/FlagPaste.cs:65`). It matches the current test build; local `upstream/main` (not fetched) is still `8e3a612`.
- [ ] Bump the version: `CurrentReleaseLabel` and `GlobalAssemblyInfo.fs` still say 3.2.
- [ ] Run the release workflow by hand on master to check the Mac and Linux builds.

## Applied, needs a live check

- 60, 101, 52, 81: co-op fixes, hold on reading. Needs live co-op, one Between Levels run and one Anywhere run.
- 68: shuffle window leak, fixed on reading.
- 64, 77, 90: covered by the self-test, which has not been run.
- 72, 88, 91: not run in the app.
- 102: web, not opened in a browser.

Closed: 100 (`Directory.Build.Props` casing has no visible effect beyond DLL metadata on Linux).

## Not reviewed

Running app, live co-op, self-test run, desktop build by the agents, web page in a browser, web Release/AOT, Mac and Linux builds, the web build's 68 warnings, WPF. Not reread in full: `Z1R_Sync`, `Z1R_Avalonia_Sync`, SignalR hosts, `CoopTest.cs`, parts of `CoopSync.cs`, `WPFUI.cs`, `OverworldItemGridUI.cs`, `OverworldMapTileCustomization.cs`, `Graphics.cs`, `PieMenus.cs`, `DrawingLayer.cs`, `Program.cs` and `SelfTest.cs`.
