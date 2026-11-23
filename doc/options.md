# Options

[Home](../README.md) · [Getting started](getting-started.md) · [Start screen](start-screen.md) · [Main screen](main-screen.md) · [Dungeons](dungeons.md) · [Hotkeys](hotkeys.md) · **Options**

Open **Options** on the start screen or the main screen. Most settings take effect immediately and are remembered. Hover a setting for its tooltip.

- [Overworld settings](#overworld-settings)
- [Dungeon settings](#dungeon-settings)
- [Reminders](#reminders)
- [Auto-save](#auto-save)
- [Other](#other)
- [Co-op](#co-op)

## Overworld settings

| Setting | What it does |
|---|---|
| **Draw routes** | Always shows routing lines when you hover an overworld tile. |
| **Show screen scrolls** | Routing lines assume you can screen scroll. Scrolls are drawn as curved lines. |
| **Highlight nearby** | Highlights the nearest unmarked tiles you can get to when you move the mouse. |
| **Hover on Zones/Coords** | Hovering the Zones or Coords checkbox shows that overlay while the mouse is over it. |
| **Show magnifier** | Shows a magnified view of overworld tiles when you hover. |
| **Use old popups** | Uses the old picture popups instead of button boxes when you mark the wood sword cave and the take-any caves. |

All but **Use old popups** appear only on the standard overworld, not with a map from **Other...** on the start screen.

## Dungeon settings

| Setting | What it does |
|---|---|
| **Dungeon map hint** | Adds a transparent icon to dungeon map rooms to help you guess where a room is. The **Opacity** slider sets how strong it is. |
| **Show dungeon hint icons** | Shows the dim question-mark icons above the dungeons and other items, where you click to pick a location hint. |
| **BOARD instead of LEVEL** | Labels dungeon tabs and headers BOARD-1 to BOARD-9. |
| **Show basement info** | Empty dungeon item boxes suggest whether the item is a basement item or a floor drop. No effect with Hide Dungeon Numbers. |
| **Do door inference** | Marks a green door when you mark a new room, if the way in can be worked out. |
| **Book for helpful hints** | For seeds with the Book To Understand Old Men flag and Helpful hints. Left-click an Old Man Hint room to toggle whether you have read it. |
| **Alphabetize hint zone list** | Lists hint zones alphabetically instead of geographically. |
| **Red edge by OFF tiles** | Draws a red "no bomb" edge on a room side that touches an OFF tile. On by default. |
| **Left-drag auto-inverts** | When you paint a map, a first drag with the left button swaps OffTheMap and Unmarked so you can start painting at once. |
| **Default to NonDescript** | Clicking an Unmarked room marks it NonDescript (empty box) instead of MaybePushBlock (box with two dots). |
| **Dungeon 'Sunglasses'** | Darkens the dungeon colors to reduce contrast. |
| **Reverse scroll wheel** | In dungeon rooms, swaps which scroll direction opens the monster popup and which opens the floor-drop and item popup. |
| **Hide lock and hints** | Hides the tip box and the lock bar on the item, tile, room type, monster and floor drop menus. |

## Reminders

- **Volume** sets how loud voice reminders are. **Disable all** turns every reminder off. You can still read them in the **Log**.
- Each reminder has a **Voice** box (spoken aloud) and a **Visual** box (shown as an icon at the upper right of the Timeline).
- Voice boxes are off and Visual boxes are on by default, on new installs and for settings files that have no voice key (choices already saved in v3.0.4 are kept).
- **Reminder interval (minutes)** sets how often the periodic reminders repeat, from 1 to 60.
- **Change voice** picks which voice speaks. It appears only when more than one voice is installed.

| Reminder | When it fires |
|---|---|
| **Dungeon Feedback** | Dungeons located or completed, triforces obtained, and go-time. |
| **Sword Hearts** | At 4-6 or 10-14 hearts, to consider the white or magical sword. |
| **Coast Item** | You have the ladder, so fetch the coast item. |
| **Recorder Spots** | Periodic count of recorder spots left. |
| **Power Bracelet Spots** | Periodic count of power bracelet spots left. |
| **Boomstick Book** | Periodic, while the boomstick book is available in a shop you found. |
| **Have Magic Key** | Once, when you enter a dungeon with the magic key. |
| **Have Ladder** | Once, when you enter a dungeon with the ladder. |
| **Blockers** | You may have become unblocked on a dungeon you left. |
| **Door Repair Count** | Each time you uncover a door repair charge, with the count so far. |
| **Overworld Overwrites** | Each time you make a destructive change to an overworld mark, in case it was an accident. |
| **Overworld + Extras** | A run started with a ZORA extra set to Random: check the Overworld + Extras panel and turn it on or off once you know. |

The voice uses the built-in Windows voices on Windows. On macOS it uses the `say` command, where the volume slider only mutes or unmutes and the system volume sets the loudness. On Linux it needs `espeak-ng`, `espeak` or `spd-say` installed. Neither the macOS nor the Linux voice has been run on a real machine yet. The browser version cannot speak.

## Auto-save

**Auto-save interval (minutes)** sets how often the tracker saves your run by itself, from 1 to 10. The auto-save is for restoring after a crash.

## Other

| Setting | What it does |
|---|---|
| **Animate tile changes** | Briefly animates the rectangle when you change an overworld spot or a dungeon room type. |
| **Animate shop highlights** | Briefly animates the rectangle when hovering an icon highlights shops on the map. |
| **Remember startup selection** | The boxes you tick under Startup options and ZORA Extras on the start screen come back ticked the next time the tracker starts. |
| **Save on completion** | Saves the full tracker state to a file when you click Zelda to complete the seed. |
| **Check for updates on startup** | Looks on GitHub for a newer release when the tracker starts. Not in the browser version. |
| **Archipelago support** | Adds the AP Item and the AP counter for Archipelago multiworld games. Off by default. The same setting as **Archipelago Support** on the start screen. See [Archipelago Support](start-screen.md#archipelago-support). |
| **Use old sword icons** | Draws the plain old sword pictures instead of the numbered sword icons. Takes effect the next time the tracker starts. |
| **Display seed + flags** | Looks for the seed and flags in the titles of other windows (for example fceux), saves them with your save, and shows them in the bottom corner of the Notes box. |
| **Listen for speech** | Uses the microphone for spoken map commands, such as "tracker set bomb shop" while hovering an unmarked tile. Speech recognition works on Windows only. |
| **Confirmation sound** | With **Listen for speech**, plays a sound when a spoken command updates the tracker. |
| **Broadcast window** | Opens a separate, smaller window for stream capture. It shows the overworld or the dungeon tabs, depending on where your mouse is. |
| **Include overworld magnifier** | With **Broadcast window**, includes the overworld magnifier when it is on screen. It hides some other parts of the window. |
| **Mouse magnifier window** | Opens a resizable window that shows a zoomed view around the mouse. |
| **Hide timer** | Hides the green timer at the upper right. |
| **Race Mode** | Turns off many features that could count as an unfair advantage in a race. It also mutes sound. Turning it off puts your own mute choice back. |

## Co-op

Co-op syncs your tracker with a partner's. The two buttons are in the **Other** column.

### Co-op client settings

Use this to join a server, hosted or your own. To use the hosted server:

1. Click **Co-op client settings**.
2. Enter `https://coop.zoratracker.gg` in **Function App Base**. Leave **Negotiate suffix** (`/api/negotiate`) and **SyncUpdate suffix** (`/api/SyncUpdate`) as they are.
3. Set **Console ID (this instance)**. **Generate GUID** makes a random one.
4. Set **Target console ID (to sync with)** to your partner's console ID. Your partner does the reverse.
5. Tick **Enable co-op sync**, then click **Save**.

**Enable co-op sync** stays unavailable until **Function App Base** is a valid URL. **Enable debug logging** turns on debug logging for co-op sync, for troubleshooting.

Desktop and browser trackers sync with each other. While co-op is on, a status dot shows the connection: gray when it is connecting or not connected, blue when connected and waiting for your partner, and green when your partner is here. Hover it for the text.

### Co-op host settings

This is for hosting your own server on your computer instead of using the hosted one. It is a desktop feature.

- **Enable hosting** and **Port number** (1024 to 65535). The **Launch** button starts the host on that port and turns on co-op sync. **Stop** shuts the host down.

## Crash log and tour

- **Show crash log...** opens the folder that holds `Z1R_Tracker_crash_log.txt`, with the file selected. Attach it when you report a problem.
- **Guided Tour** on the start screen walks through the start screen and then the main screen. In the Options menu on the main screen, it tours the main screen.
