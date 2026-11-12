# 2.2.0-beta-23 (draft, not published)

## GitHub notes

### New: Archipelago support (Avalonia port)
- New **AP Item** for Archipelago multiworld games: an item that belongs to another world. Place it on any tile or box. There is no placement limit and it has no logic effect.
- The AP Item has its own row in the overworld tile menus, the dungeon item menus and the shop picker.
- **Archipelago Support** checkbox on the startup screen (under Startup Options) and in Options. It is off by default and the two checkboxes stay in sync. With it off, the AP Item is hidden.
- AP counter next to the Zelda icon in the item grid. It counts AP Items you have collected. Hover it to highlight every tile that holds an AP Item.

### Fixes
- Fixed a crash when marking a hinted item location and then pressing the hotkey at the same place (reported by equations19). Fixed in the port and in WPF.
- Hint Decoder: long zone names shrink to fit instead of being cut off (Linux).

### Startup screen
- Paste Flags moved under Startup Options. The custom level name moved under ZORA Extras. Both cards have two extra rows and are re-centered.

## Discord

```
**ZORATracker 2.2.0-beta-23**

**New: Archipelago support (Avalonia port)**
• New **AP Item** for items that belong to another world. Unlimited placements, no logic effect.
• AP Item has its own row in the overworld and dungeon menus and the shop picker.
• New **Archipelago Support** checkbox on the startup screen and in Options (off by default).
• AP counter next to the Zelda icon. It counts collected AP Items, and hovering it highlights every tile with one.

**Fixes**
• Crash when marking a hinted item location, then pressing the hotkey at the same place (fixed in port and WPF).
• Hint Decoder: long zone names no longer cut off (Linux).

**Startup screen**
• Paste Flags and the custom level name swapped sides, with two extra rows each and re-centered.

Download: https://github.com/TheGobOne/ZoraTracker/releases/latest or update via the app.
Please report any bugs on GitHub Issues: https://github.com/TheGobOne/ZoraTracker/issues
```
