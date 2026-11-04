# Hotkeys

[Home](../README.md) · [Getting started](getting-started.md) · [Start screen](start-screen.md) · [Main screen](main-screen.md) · [Dungeons](dungeons.md) · **Hotkeys** · [Options](options.md)

Hotkeys let you mark items, tiles, rooms and doors from the keyboard, and even drive the mouse. You can play most of the app without touching the mouse.

## Set them up

1. Open `HotKeys.txt` in the tracker's folder with any text editor.
2. Find the selector you want and type a key after the `=`.
3. Save the file and restart the tracker.

The **Hotkeys** button on the main screen shows the keys you have mapped, as a cheat sheet.

`HotKeys_BlankSampleTemplate.txt` lists every selector available. Do not edit it; copy lines from it into `HotKeys.txt`.

```
Item_BlueRing   = r
Overworld_Level1 = 1
Global_DungeonTab1 = SHIFT 1
```

- A key is `0-9`, `a-z`, or `\nnn` where `nnn` is a [WPF key code](https://learn.microsoft.com/dotnet/api/system.windows.input.key#fields) (for example `\75` is NumPad1).
- Add `SHIFT `, `CTRL ` or `ALT ` before the key for a modifier.
- Leave a key blank to bind nothing. Lines starting with `#` are comments.

## Where each hotkey works

| Prefix | Works when |
|---|---|
| `Item_` | Hovering an item box, or an overworld tile, where it toggles that item's marker on the tile (up to 3 per tile). |
| `Overworld_` | Hovering an overworld tile. Level1-Level8 mean dungeons A-H when Hide Dungeon Numbers is on. |
| `Blocker_` | Hovering a blocker box. |
| `DungeonRoom_` | Hovering a dungeon room: room type, doors, monsters, floor drops and more. |
| `Global_` | Anywhere. Toggle swords, candles and rings, switch dungeon tabs, or move and click the mouse. |

Menus that are open on screen also have their own contextual hotkeys.

## Play without the mouse

Map the `Global_MoveCursor...` keys to move the mouse across grids (the overworld, dungeon rooms and more), and `Global_LeftClick`, `Global_MiddleClick`, `Global_RightClick`, `Global_ScrollUp` and `Global_ScrollDown` to click and scroll. `Global_ToggleCursorOverworldOrDungeon` jumps the cursor between the overworld and the dungeon.

## Shop tiles

Hovering a shop tile, the Letter, White Sword and Magical Sword item hotkeys place that item in the shop.
