# Getting started

[Home](../README.md) · **Getting started** · [Start screen](start-screen.md) · [Main screen](main-screen.md) · [Dungeons](dungeons.md) · [Hotkeys](hotkeys.md) · [Options](options.md)

ZORATracker 3.0 runs in a browser or as a desktop app on Windows, macOS and Linux. Pick one.

## Install

Every desktop zip is on the [Releases page](https://github.com/TheGobOne/ZoraTracker/releases/latest).

| Where | Steps |
|---|---|
| Browser | Open [zoratracker.gg](https://zoratracker.gg). Nothing to install. |
| Windows | Download `ZoraTracker-CrossPlatform-v3.0.0-win-x64.zip`. Unzip it into a folder you want to keep, then run `ZoraTracker.CrossPlatform.exe`. |
| macOS | Download `ZoraTracker-CrossPlatform-v3.0.0-macOS-arm64.zip` (Apple silicon) or `ZoraTracker-CrossPlatform-v3.0.0-macOS-x64.zip` (Intel). Unzip it and move `ZORATracker.app` to Applications. |
| Linux | Download `ZoraTracker-CrossPlatform-v3.0.0-linux-x64.zip`, unzip it and run `./ZoraTracker.CrossPlatform`. |

Windows may warn about an unknown app the first time. Choose **More info**, then **Run anyway**. If your antivirus quarantines `ZoraTracker.CrossPlatform.exe`, restore it from the quarantine list.

The Mac app is signed ad hoc and is not notarized. The first time, right-click `ZORATracker.app` and choose **Open**. If you are upgrading, delete the old `ZORA Tracker.app` first.

Testing: Windows and the browser are the main targets. Linux has minimal testing. Co-op has no official testing on macOS or Linux.

### The browser version

- Your saves and settings stay in the browser.
- Only one tab saves at a time. If you open the tracker in a second tab, that tab shows a notice.
- Each manual save is also downloaded as a file. Load it again with **Load Save** on the start screen.
- The page updates on your next visit.

### Where your files go

| Platform | Settings, `HotKeys.txt`, crash log | Save files |
|---|---|---|
| Windows | The folder you unzipped to | `Saves` folder in that folder |
| macOS | `~/Library/Application Support/ZoraTracker` | `Saves` folder next to the program |
| Linux | `~/.config/ZoraTracker`, or `$XDG_CONFIG_HOME/ZoraTracker` if that variable is set | `Saves` folder next to the program |

The version button on the start screen has an **Open folder** button that opens the settings folder.

## Your first run

1. On the [start screen](start-screen.md), click **Paste Flags**, paste the Z1R and ZORA flag strings from the ZORA site (or a seed file name, or drop the seed file on the dialog), and click **Apply**.
2. Check the startup options your seed uses (Item Shuffle, Mirrored Overworld, and so on).
3. Click the overworld your seed uses (**1st Quest**, **2nd Quest**, **Mixed - 1Q**, **Mixed - 2Q** or **Randomized Quest**). The tracker starts.
4. Mark things as you find them. Click a map tile, an item box or a dungeon room to open its menu.

Click **Guided Tour** on the start screen at any time for a walkthrough of both screens.

## Saving and loading

- **Save** (top right of the main screen) writes a save file.
- **Load Save** on the start screen resumes any save file. The three most recent saves are listed under **Recently loaded saves**.
- **Auto-save** runs on its own. Set the interval in [Options](options.md).
- A loaded save restores its own Item Shuffle, Hide Dungeon Numbers and overworld settings, so the start screen choices are ignored.
- A save also holds your ZORA flags, so they come back when you load it.

## Updating

- **Browser:** the page updates on your next visit.
- **Windows:** the tracker checks for new releases when it starts (turn this off with **Check for updates on startup** in [Options](options.md)). The version button at the bottom of the start screen glows green when an update is available. Click it, then **Install update**. The tracker downloads the new version, installs it over the current folder, and restarts. Your hotkeys, settings and custom images are not touched. A game in progress is discarded, so save first.
- **macOS and Linux:** the version button glows the same way, but **Install update** does not install anything there. It opens the release page, and you download the new zip yourself.

## Co-op

To track with a partner, enter `https://coop.zoratracker.gg` in **Co-op client settings**. Desktop and browser trackers sync with each other. See [Options](options.md#co-op).

## When something breaks

On desktop, open **Options** and click **Show crash log...** to open the folder with the crash log selected. Attach the log when you [open an issue](https://github.com/TheGobOne/ZoraTracker/issues).
