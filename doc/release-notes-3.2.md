# ZORATracker 3.2 release notes (draft)

Compared against the published 3.1 release. 3.2 is published.

## Discord copy

**ZORATracker 3.2**

**New**
- Shops popout (Popout Windows > Shops, desktop only): the Shop window in its own window, matching the one on the map. It edits shop items but does not place shops on the map.
- Enemy inference (Options, on by default): in levels 1 to 8, marking a Digdogger or Gohma room adds a possible recorder or bow and arrow blocker, if you don't have the item and a blocker slot is free.
- Dim dungeon without hearts (Options, off by default): dims a dungeon once you have its triforce and every item except the heart containers.
- Marking an Any Road inside an area blocked by Extra Power Bracelet Blocks, Extra Raft Blocks, Randomize Dead Woods or Randomize Lost Hills makes that area reachable. The two raft areas count separately.
- Dungeon map hint now shows a chest icon between the rupee and key.
- Paste Flags: Z1R flags set the Level 9 triforce count. If the count is decided per seed, set it yourself.
- Paste Flags: ZORA Expanded Dungeon Locations circles the vanilla dungeon spots, 1st Quest in white and 2nd Quest in orange. Shared spots get a split circle, also from the Mark vanilla dungeon locations buttons.
- Paste Flags: a ZORA Level 9 entrance that needs the level 4 sword sets the 4th sword as the entry item.
- Paste Flags reads ZORA 2.0.2 flags.

**Changes**
- The "Mark vanilla dungeon locations" buttons now circle 1st Quest spots in white and 2nd Quest spots in orange.
- Right click a "Shops in menu" button to open the Shop window (left click with swap L/R on). Left click still places that shop.
- Right click a triforce with no empty basement slot: the Triforce Piece replaces an uncollected basement item. Right click again to restore it (until the tracker closes).
- Intra Item Shuffle: basement item and floor key item hotkeys can use a box holding the tracker's guessed heart.
- "Do door inference" is now "Door inference".
- New installs only: Door inference is on and Draw routes is off.
- "Archipelago Support" is now "Archipelago Items".
- Paste Flags opens near the top with a taller results list.

**Fixes**
- Placing a shop on a tile with item icons no longer replaces them.
- Item and marker hotkeys block the same caves as the tile menu.
- "Go time" no longer skips an unfound Level 9 when it needs fewer than 8 triforces or a custom entry item.
- "Go time" counts dungeon spots locked by the Power Bracelet or a hint as unreachable.
- Level 2 waits for the 4th sword staircase item before showing complete.
- Intra: guessed hearts stay guesses after a load, and no second guessed heart appears when the extra floor box already holds one.
- 1st Quest: hovering the recorder number no longer shows an empty tooltip.
- Co-op: partner circles show up, the Level 9 triforce count is shared, two players editing different shops keep both edits, and door inference no longer adds doors to a partner's rooms.

Thanks to everyone for the bug reports and feedback.

[Download](https://github.com/TheGobOne/ZoraTracker/releases/tag/v3.2) or update via the app:
Please report any bugs here or on [Issues](https://github.com/TheGobOne/ZoraTracker/issues) on GitHub

## Before publishing

- The Download link points to a v3.2 tag that doesn't exist yet.
- "Reads ZORA 2.0.2 flags" follows an unreleased ZORA test build. Recheck it, or cut the line.
- New features and fixes were confirmed by reading code, not by running them in the app.
- The Windows zip must be attached by hand, or in-app update won't work.
