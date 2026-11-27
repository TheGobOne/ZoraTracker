# ZORATracker review findings (open)

Scope: whole repo, with the changes in v3.2..90ddeba checked first. Pass of 2026-11-27, six read-only agents, not every older file section reread (see "Not reviewed"). Line numbers are from 90ddeba and have shifted in files fixed since. "Confirmed" means the lines were read a second time during the merge. "Not rechecked" means only the agent read that code. Nothing was run in the app. 103 and up are new. Co-op is open to all severities (rule lifted 2026-11-27).

## Critical (ordinary desktop use)

None open.

## High (needs a partner, the web build or mixed settings)

- [ ] 106: `Z1R_Avalonia/CoopSync.cs:2088`, `:1037-1048`: not rechecked. A 3.2 or 3.0.4 partner cannot apply item ids 49/50, resends its old value, and our map or compass box is cleared with no notice. Two current trackers may do the same if only one has Anywhere on (not checked). About 60 minutes.
- [ ] 107: `Z1R_Avalonia/CoopSync.cs:327-331`, `:1038`, `:1060`, `Z1R_Avalonia/Program.cs:1111`: not rechecked, medium-high confidence. A 3.2 partner sends no level tag, so a reconnect or box edit untags our box and can mark the wrong level's triforce. About 45 minutes.
- [ ] 111: `Z1R_Avalonia/CoopSync.cs:611`: not rechecked, medium confidence. A web partner in an Anywhere game ignores spot items and boxed maps with no notice. About 20 minutes.

## Medium


## Low and cleanup

- [ ] 123: `Z1R_Avalonia/Popouts.cs:489`, `:555`: not rechecked, low confidence. After a popout click, map hotkeys may do nothing until the main window is clicked. About 15 minutes.
- [ ] 124: `Z1R_Avalonia/MainTrackerTriforcesAndItems.cs:626-627`: not rechecked. 27 hidden spot views redraw all session. About 15 minutes.
- [ ] 127: `Z1R_Avalonia/CoopTest.cs`: no co-op test covers spots, slots or tagged boxes. About 45 minutes.
- [ ] 78: `Z1R_Avalonia/Popouts.cs:393`, `:442`, `:878`: low confidence, not run. On Mac or Linux the popout may drop behind the main window.
- [ ] 50: `Z1R_Avalonia/CoopSync.cs:850-853`: the "unversioned 8" filter also drops a real 8 from a 3.2 partner's save. Left open: both look the same on the wire.

## Before the next release

- [ ] Check the shuffle popout on screen.
- [ ] At ZORA's 2.0.2 release, recheck the format 23 flag table (`Z1R_Avalonia/FlagPaste.cs:65`). It matches the current test build; local `upstream/main` (not fetched) is still `8e3a612`.
- [ ] Bump the version: `CurrentReleaseLabel` and `GlobalAssemblyInfo.fs` still say 3.2.
- [ ] Run the release workflow by hand on master to check the Mac and Linux builds.

## Applied, needs a live check

Fixed findings are removed from the lists above. Release desktop and web Debug builds have 0 errors. Nothing here has been run, and the self-test has not been run.

- 87, 97, 116: level completion and item progress under shuffle.
- 103, 104, 105, 113, 118: item boxes and menus. Check on screen.
- 108: web refuses a desktop save that uses Shuffle Type Anywhere (error box, nothing loaded). Not opened in a browser. Web saves never write Anywhere, so they are unaffected.
- 112: flags that pick different shuffle types clear both Shuffle Type ticks, so Start asks for one.
- 125: a ZORA-made file reads its shuffle settings from its ZORA flags on load (desktop only). Two different types load as Between Levels. Not run in the app.
- 94: under Anywhere, the M/C/Tri Shuffle window has an Overworld row with the coast, armos and white sword boxes, which take a triforce, map or compass with a level. Check on screen. The main tracker's item menu is unchanged.
- 80: the co-op hello and Overworld messages say the sender knows the fairy and shuffle shop markers. A partner that does not is sent none, and its tiles keep ours. Needs a live session with a 3.2 partner. A tile already holding 3 icons of theirs drops ours. An older partner stays flagged until our next reconnect, and while flagged the new markers do not sync to current partners either.
- 115: the header Triforce flag saves only the header flag, not a triforce held in a box. Save files written before this fix keep the old flag, so they still count the level after the box is cleared.
- 114, 119, 121: crash recovery, old save load, guessed heart in a spot.
- 109, 130: web, not opened in a browser.
- 110, 126: co-op, needs a live session.
- 117, 122: Item Locations on the 1Q/2Q toggle (a partner's toggle still does not rebuild it), hotkey descriptions.
- 93: right-click on a header triforce with triforces shuffled now shows the reminder and does not toggle collected.
- 128, 129: self-test code. 128 is partly done: level 5 map and triforce toggles are still restored only on success.
- 60, 101, 52, 81: co-op fixes, hold on reading. Needs live co-op, one Between Levels run and one Anywhere run.
- 68: shuffle window leak, fixed on reading.
- 64, 77, 90: covered by the self-test, which has not been run.
- 72, 88, 91: not run in the app.
- 102: web, not opened in a browser.

Closed: 100 (`Directory.Build.Props` casing has no visible effect beyond DLL metadata on Linux). 98 (Item Locations and Anywhere spots): decided no. 120 (Anywhere box scroll): decided intended, the box keeps "?" until a level is picked.

## Not reviewed

Running app, live co-op, self-test run, desktop build by the agents, web page in a browser, web Release/AOT, Mac and Linux builds, the web build's 68 warnings, WPF. Not reread in full: `Z1R_Sync`, `Z1R_Avalonia_Sync`, SignalR hosts, `CoopTest.cs`, parts of `CoopSync.cs`, `WPFUI.cs`, `OverworldItemGridUI.cs`, `OverworldMapTileCustomization.cs`, `Graphics.cs`, `PieMenus.cs`, `DrawingLayer.cs`, `Program.cs` and `SelfTest.cs`.
