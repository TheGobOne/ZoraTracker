# ZORATracker review findings (open)

Latest pass (2026-11-27): the unreleased 3.3.1 changes, v3.3..c375297, six read-only agents. Findings 131 and up and the 106, 107 and 111 rechecks come from it. "Confirmed" there means the lines were read again during the merge. Findings from this pass without that word were read only by an agent.

Earlier pass scope: whole repo, with the changes in v3.2..90ddeba checked first. Pass of 2026-11-27, six read-only agents, not every older file section reread (see "Not reviewed"). Line numbers are from 90ddeba and have shifted in files fixed since. "Confirmed" means the lines were read a second time during the merge. "Not rechecked" means only the agent read that code. Nothing was run in the app. 103 and up are new. Co-op is open to all severities (rule lifted 2026-11-27).

## Critical (ordinary desktop use)

None open.

## High (needs a partner, the web build or mixed settings)

- [ ] 106: `Z1R_Avalonia/CoopSync.cs:1046-1092`, `:923-951`, `Z1R_Tracker/TrackerModel.fs:924-926`: Confirmed. A partner that cannot take a map or compass keeps its old value, resends it with a newer version, and our box is cleared with no notice. Happens with a 3.2 or 3.0.4 partner, a web partner, and between two current trackers when only one has Anywhere on (Shuffle Type is not synced). After 4 failures the two trackers stay different. The item menu changes in 3.3.1 make it easier to hit. About 60 minutes.
- [ ] 107: `Z1R_Avalonia/CoopSync.cs:327-331`, `:1048-1049`, `:1071`: Confirmed. A 3.2 partner sends no level tag. If it wins the reconnect tie, or edits that box, our triforce box is untagged and the chosen level loses its credit. About 45 minutes.

## Medium


## Low and cleanup

- [ ] 137: `Z1R_Avalonia/OptionsMenu.cs:658`: no tour stop covers the M/C/Tri Shuffle popout, the level picker or the triforce hotkey. Needs your wording. About 30 minutes.
- [ ] 143: `Z1R_Avalonia/SelfTest.cs:408-412`: the new tour check fails only when all of a stop's labels are missing and covers 4 of 17 stops, so renaming "Popout Windows" passes. About 15 minutes.
- [ ] 144: `Z1R_Avalonia/SelfTest.cs:564-569`: the new coast box test skips `setForPickedLevel`, the new menu row, the pinned layout and the hotkey path. About 30 minutes.
- [ ] 145: `Z1R_Avalonia_Desktop/Z1R_Avalonia_Desktop.csproj:9`: not rechecked, medium confidence. Windows file properties show version 0.0.0.0. Older than 3.3. About 15 minutes.
- [ ] 111: `Z1R_Avalonia/CoopSync.cs:611`, `:620-622`: partly right. A web partner ignores spot items, silently but harmlessly. Its map box half is part of 106. About 20 minutes.

- [ ] 127: `Z1R_Avalonia/CoopTest.cs`: no co-op test covers spots, slots, tagged boxes or a mismatched Shuffle Type. About 45 minutes.
- [ ] 78: `Z1R_Avalonia/Popouts.cs:393`, `:442`, `:878` (the last has moved up by about 24 lines): low confidence, not run. On Mac or Linux the popout may drop behind the main window. Not changed: needs a Mac or Linux machine to see the behavior and test a fix.
- [ ] 50: `Z1R_Avalonia/CoopSync.cs:850-853`: the "unversioned 8" filter also drops a real 8 from a 3.2 partner's save. Left open: both look the same on the wire.

## Before the next release

- [ ] Check the shuffle popout on screen.
- [ ] 146: set the version to 3.3.1 in `GlobalAssemblyInfo.fs:4-6` (`3.3.1.0`, `3.3.1.0`, `3.3.1`) and `Z1R_Avalonia/CustomComboBoxes.cs:1879` (`3.3.1`). Both files must agree: a missed label offers the update forever, a missed assembly file misnames the Mac and Linux zips. About 5 minutes.
- [ ] 147: push c375297 and the version bump before tagging `v3.3.1`. About 2 minutes.
- [ ] 148: write 3.3.1 release notes. Cover: item boxes take maps, compasses and triforces under Anywhere; the pinned bottom row; the popout's Overworld row is gone; the red X clear cell; the tour hover fix and Shift+drag text. `doc/release-notes-3.3.md:1`, `:3` still say draft and not published. About 20 minutes.
- [ ] 149: attach the Windows zip before publishing, since the workflow builds only Mac and Linux. The name needs `CrossPlatform`, for example `ZoraTracker-CrossPlatform-v3.3.1-win-x64.zip`. About 10 minutes.

## Applied, needs a live check

Fixed findings are removed from the lists above. Release desktop and web Debug builds have 0 errors. The Avalonia self-test passed for quest types 0 to 3 on 2026-11-27 (after the item box change). Nothing else here has been run in the app.

Fixed 2026-11-27, build only (Release desktop, 0 errors), not run in the app:
- 131: the Anywhere item grid saves its layout under its own key (`ItemBoxAnywhere`, `CustomComboBoxes.cs` layoutKey).
- 132: a map or compass used up in another slot of the same dungeon moves from there, keeping its level (`Views.cs`, was: level picker then beep).
- 138: the Shift+drag tip says the pinned bottom row stays put under Anywhere.
- 133: the Anywhere tour text says the Triforce/map/compass row sits above custom icons, not on the bottom row.
- 134: the tour says Jump to mouse is at the top of tile menus and along the bottom of the item menu; uncheck Hide lock and hints to see it.
- 135: the "Triforce in the basement" stop says it does not apply when triforces are shuffled, and its demo is skipped then.
- 136: the tour's version-button anchor is now the prefix "v" (was "v2.", which never matched). Not checked in the app; "v" could match another control that starts with v.
- 139: a level-tagged item in the box beside the sword shows its level tag.
- 140: the shuffle popout asks a level for a Triforce Piece only when triforces are shuffled.
- 141: picking a level that another box already holds for the same item now beeps and changes nothing (item menus, hotkey, popout). Checked in the UI only, so saves and co-op apply as sent.
- 142: closed on reading, no code change. The web build refuses every save with Shuffle Type Anywhere (108), the only way a save carries a tagged triforce. A co-op partner sending one is part of 106.

Accepted without an on-screen check on 2026-11-27: the item box level picker, the triforce hotkey, and the M/C/Tri Shuffle popout with its Overworld row removed.

- c375297, tour button hover: confirmed in a headless run of the tour, not seen in the app. The "Turn Extras on/off" pointer got the same fix and was not exercised.
- ee9eb42, main-screen tour check: never run. The longer "Moving and resizing menus" text box has not been seen on screen.
- 87, 97, 116: level completion and item progress under shuffle.
- 103, 104, 105, 113, 118: item boxes and menus. Check on screen.
- 108: web refuses a desktop save that uses Shuffle Type Anywhere (error box, nothing loaded). Not opened in a browser. Web saves never write Anywhere, so they are unaffected.
- 112: flags that pick different shuffle types clear both Shuffle Type ticks, so Start asks for one.
- 125: a ZORA-made file reads its shuffle settings from its ZORA flags on load (desktop only). Two different types load as Between Levels. Not run in the app.
- 94: under Anywhere, every item box (dungeon and overworld) takes a map, compass or shuffled triforce from the item menu or a hotkey and asks which level it is for (hotkeys: triforce only, as maps and compasses have no binding). The M/C/Tri Shuffle window has no Overworld row. Check on screen.
- 80: the co-op hello and Overworld messages say the sender knows the fairy and shuffle shop markers. A partner that does not is sent none, and its tiles keep ours. Needs a live session with a 3.2 partner. A tile already holding 3 icons of theirs drops ours. An older partner stays flagged until our next reconnect, and while flagged the new markers do not sync to current partners either.
- 115: the header Triforce flag saves only the header flag, not a triforce held in a box. Save files written before this fix keep the old flag, so they still count the level after the box is cleared.
- 114, 119, 121: crash recovery, old save load, guessed heart in a spot.
- 109, 130: web, not opened in a browser.
- 110, 126: co-op, needs a live session.
- 117, 122: Item Locations on the 1Q/2Q toggle (a partner's toggle still does not rebuild it), hotkey descriptions.
- 93: right-click on a header triforce with triforces shuffled now shows the reminder and does not toggle collected.
- 128, 129: self-test code. 128 now also restores level 5's map and triforce toggles after a failure. Self-test passed on 2026-11-27.
- 123: a click in the M/C/Tri Shuffle window that redraws it now hands the keyboard back to the main window. Cause found on reading, not reproduced. Check on screen.
- 124: the 27 Anywhere spots keep their timeline and upper grid links without building hidden box views. Check the timeline in an Anywhere run.
- 60, 101, 52, 81: co-op fixes, hold on reading. Needs live co-op, one Between Levels run and one Anywhere run.
- 68: shuffle window leak, fixed on reading.
- 64, 77, 90: covered by the self-test, which passed on 2026-11-27.
- 72, 88, 91: not run in the app.
- 102: web, not opened in a browser.

Closed: 100 (`Directory.Build.Props` casing has no visible effect beyond DLL metadata on Linux). 98 (Item Locations and Anywhere spots): decided no. 120 (Anywhere box scroll): decided intended, the box keeps "?" until a level is picked.

Cleared 2026-11-27: the format 23 flag table recheck. Local `upstream/main` is `9d086e9` "ZORA 2.0.2" and the 49 ZORA fields and `specZoraVersion` match (agent check).

## Not reviewed

Latest pass: nothing run on screen, no self-test, no live co-op, no desktop build by the agents, web Release/AOT, Mac and Linux at runtime, the in-app update end to end, WPF, and files unchanged since v3.3 beyond callers of changed code. The web Debug build passed with 0 errors and 68 warnings, none on changed lines.

Earlier pass:

Running app, live co-op, self-test run, desktop build by the agents, web page in a browser, web Release/AOT, Mac and Linux builds, the web build's 68 warnings, WPF. Not reread in full: `Z1R_Sync`, `Z1R_Avalonia_Sync`, SignalR hosts, `CoopTest.cs`, parts of `CoopSync.cs`, `WPFUI.cs`, `OverworldItemGridUI.cs`, `OverworldMapTileCustomization.cs`, `Graphics.cs`, `PieMenus.cs`, `DrawingLayer.cs`, `Program.cs` and `SelfTest.cs`.
