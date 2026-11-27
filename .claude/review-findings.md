# ZORATracker review findings (open)

Scope: all work since v3.2 (v3.2..5ab87f6). Latest pass 2026-11-27, six read-only agents. Co-op is open to all severities (rule lifted 2026-11-27). 60 to 81 come from the 2026-11-26 pass (fixed items removed), 82 on from the latest. "Not rechecked" means only the agent read that code. Line numbers are from 5ab87f6. Fixes so far: 45c0a8a, then a second uncommitted pass. Desktop Release and web Debug build with 0 errors; none run in the app, self-test not run.

## Medium

- [ ] 87: `Z1R_Tracker/TrackerModel.fs:1095-1098`: needs your call. Completion ignores which kinds are shuffled: with only Triforces ticked, a level still needs its compass marked; with Anywhere, levels 1-8 need no triforce even when triforces are not shuffled. About 30 minutes.

- [ ] 80: `Z1R_Tracker/TrackerModel.fs:897`: 3.2 and older partners show shop shuffle markers as heart containers. Needs your call: the old build cannot be changed, so the only fix is to stop syncing shop shuffle markers, which would also stop them for current partners.

## Low and cleanup

- [ ] 78: `Z1R_Avalonia/Popouts.cs:838`: on Mac or Linux the popout may drop behind the main window (low confidence, not run).
- [ ] 93: `Z1R_Avalonia/Views.cs:336`, `:437`: not rechecked, needs your call. With triforces shuffled, right click on a header triforce marks it collected. About 10 minutes.
- [ ] 94: `Z1R_Avalonia/CustomComboBoxes.cs:1676`: not rechecked, depends on ZORA seeds. Under Anywhere, coast, armos and white sword boxes cannot record a triforce, map or compass. About 45 minutes.
- [ ] 97: `Z1R_Tracker/TrackerModel.fs:991`: not rechecked. Item progress divides by 20, but Anywhere adds 27 spots, so colors are wrong late in a run. About 10 minutes.
- [ ] 98: `Z1R_Avalonia/MainTrackerTriforcesAndItems.cs:187`, `:227`: not rechecked, needs your call. Item Locations does not list Anywhere spots. About 20 minutes.
- [ ] 100: `Directory.Build.Props`: not verified, predates v3.2. The capital P probably makes the Linux runner skip it. About 5 minutes.

- [ ] 50: `Z1R_Avalonia/CoopSync.cs` (`beginMerge`): the "unversioned 8" filter also drops a real 8 from a 3.2 partner's save. Left open: a real 8 and the old default 8 look the same on the wire.

## Before the next release

- [ ] At ZORA's 2.0.2 release, recheck the format 23 flag table (`Z1R_Avalonia/FlagPaste.cs:65`). `upstream/main` was `8e3a612` on 2026-11-25. The tracker now follows the 11/26 test apworld.
- [ ] Bump the version: `CurrentReleaseLabel` and `GlobalAssemblyInfo.fs` still say 3.2.
- [ ] Run the release workflow by hand on master to check the Mac and Linux builds.

## Applied, needs a live check

- 60, 101: the shuffle window now syncs between two current trackers (3.2 partners are unchanged). Built only; needs live co-op, one Between Levels run and one Anywhere run.
- 52, 81: co-op, built only.
- 68: shuffle window leak, applied on reading only.
- 64, 77, 90: covered by the self-test, which has not been run.
- 72, 88, 91: not run in the app.
- 83, 102: web, not opened in a browser.

## Not reviewed

Running app, live co-op, self-test run, web page in a browser, web Release/AOT, Mac and Linux runs, WPF beyond crash and data loss reading.
