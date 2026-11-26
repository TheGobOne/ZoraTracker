# ZORATracker review findings

Scope: everything going out in 3.2 (v3.1..974f69b). Six read-only agents, 2026-11-25. Findings come from reading code. Nothing was reproduced in the running app. Numbers 1-30 are from the earlier review (v3.1..c4ddd43).

Fixes for 3, 5, 7, 19, 21, 25, 31-49, 51 and 53-56 are uncommitted. Desktop Release and web Debug build with 0 errors. None are tested in the running app or in live co-op, and the self-test has not been run. WPF does not build with `dotnet build` (MSB4020 at `Z1R_WPF.fsproj:69`).

## Before tagging v3.2

- [ ] Commit the fixes, then push the local commits. A v3.2 release made on GitHub now would build old code labeled 3.1.
- [ ] Attach `ZoraTracker-CrossPlatform-v3.2-win-x64.zip` by hand. The workflow builds only Mac and Linux, and Windows Install update needs the zip.
- [ ] At ZORA's 2.0.2 release, recheck the format 23 flag table. It follows the unreleased test build.
- [ ] Release notes: door inference on and draw routes off apply to new installs only. The Level 9 triforce count does not reach 3.0.4 or 3.1 co-op partners.

## Low (deferred: co-op)

Co-op is used very little, so only Critical and High findings there get fixed (decided 2026-11-25).

- [ ] 50: `Z1R_Avalonia/CoopSync.cs` (`beginMerge`): the "unversioned 8" filter also drops a real 8 from a 3.2 partner's save. Telling 3.1 from 3.2 apart needs a new marker in the co-op payload.
- [ ] 52: `Z1R_Avalonia/CoopSync.cs` (`endMerge`): a normal per-shop merge still counts as a failed apply, adding log noise and an extra round. The whole-array `ShopStash` / `CheapKeyShops` key is compared even when shops merge one by one. Likely fix: skip that comparison for arrays in `m.PerIndex`.

## Closed

- [x] 1: Header right click no longer replaces a collected basement item.
- [x] 2: Stash and cheap key merge holds between two 3.2 trackers.
- [x] 3: A stash item moved onto a partner's tile is sent.
- [x] 4: First shop tile keeps its own icons.
- [x] 5: Restored Intra heart guesses are registered (see 38).
- [x] 6: Level 9 count syncs for edits mid-run.
- [x] 7: Pasted Level 9 count and circles reach a partner, at the lowest version.
- [x] 8: C08 values 9-12 handled.
- [x] 9: ZORA format 23 tables read.
- [x] 10: Dimming no longer changes `IsComplete`.
- [x] 11: Dimming covers the extra floor box.
- [x] 12: Bracelet and hint locked spots count as unreachable.
- [x] 13: Level 9 entry count, triforce path.
- [x] 14: Heart-first order with all hearts used.
- [x] 15: Enemy inference no longer upgrades the maybe blocker.
- [x] 16: "Shops in menu" hover text matches intent.
- [x] 17: No door inference on partner room changes.
- [x] 18: Version shown is 3.2.
- [x] 19: Pasted circles can be taken back after reopening the paste dialog.
- [x] 20: Shuffle options removed for 3.2 (974f69b). Saves and remembered settings with the old keys load.
- [x] 21: Shop, item and marker hotkeys follow the tile menu's blocked caves.
- [x] 22: Map hotkeys work after a Shops popout click (see 48).
- [x] 23: Progressive boomerang mirror refresh.
- [x] 24: Shops popout hidden on web.
- [x] 25: Hand circle edits redraw the shared dungeon spots.
- [x] 26: Side strip chest follows the option.
- [x] 27: Recorder tip restored.
- [x] 28: Any Road bypass refresh.
- [x] 29: Self-test restores state.
- [x] 30: Version strings say 3.2.
- [x] 31: Leftover stash items no longer go onto a second shop tile.
- [x] 32: Floor key item gesture no longer overwrites a heart guess when a box is free.
- [x] 33: Stash rule runs after the partner's stash is applied.
- [x] 34: A circle received from a partner is drawn.
- [x] 35: ZORA's level 4 sword Level 9 entrance is read.
- [x] 36: Go time counts an unlocated Level 9 when an entry item is set.
- [x] 37: Level 2 completion counts the 4th sword staircase box.
- [x] 38: Saves record which hearts are guesses, so a marked heart stays yours after a load. Older saves still work them out, so 38 can happen with those.
- [x] 39: A guessed heart in the extra floor box is taken back after loading a save that records it (not older saves).
- [x] 40: Shuffle line removed from the start screen docs.
- [x] 41: Paste Flags docs cover the Level 9 count, circles and entry item.
- [x] 42: Download steps no longer name v3.0.0 zips.
- [x] 43: A paste with no Level 9 count takes back an earlier paste's count.
- [x] 44: "Dim dungeon without hearts" redraws when toggled.
- [x] 45: ZORA Extras has its two spare rows again, as in 3.1. Not looked at in the app.
- [x] 46: Taking the Triforce Piece back out by header right click puts back the item it replaced (this session only).
- [x] 47: Collected items moved from the shop window to a tile by placing the shop check their item box.
- [x] 48: The Shops popout stays in front after the main window takes the keyboard back. Not tried in the app; Avalonia source checked for Windows only.
- [x] 49: Bomb dropper hover text and comments name the right colors.
- [x] 51: A stash merged onto a tile is resent.
- [x] 53: Docs row for "Dim dungeon without hearts".
- [x] 54: Enemy inference docs state its conditions.
- [x] 55: New defaults for door inference and draw routes documented.
- [x] 56: Version button docs say it shows the About text.

## Looked clean

- Version strings and the update check (3.0.4 and 3.1 see v3.2 as newer). Tag a hotfix as v3.2.1; a dash suffix is not offered as an update.
- v3.1 saves load unchanged.
- Any Road rule, mirrored overworld with split circles, minimap chest.
- 4th Sword enables Progressive Items, not the reverse.
- No leftover Shuffle code.

## Not reviewed

WPF, OverworldRouting.fs, self-test, live co-op, start screen and options popup layout, web Release/AOT, Mac and Linux builds.
