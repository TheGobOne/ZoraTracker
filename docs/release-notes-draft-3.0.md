# ZORATracker 3.0 (draft, not published)

## GitHub notes

### New: use the tracker in your browser
- ZORATracker now runs in the browser at https://zoratracker.gg. Nothing to install.
- Saves and settings stay in your browser between visits. If you open the tracker in two tabs, only one tab saves, and the other tab tells you so.
- Updates load on your next visit.

### Co-op, rebuilt
- A hosted co-op server runs at https://coop.zoratracker.gg. Enter it in Co-op client settings. You don't need to host anything yourself.
- Desktop and browser trackers sync with each other.
- Each field now has its own version number, so the newest edit wins on both sides, including after a restart or reconnect.
- More syncs now: overworld tile extras (sword icons, bomb droppers, shop prices, hint shop colors, wood sword tiles, duplicates, circles), progressive box tiers, notes, and hints.
- The host's start-screen options go to a partner who is still on the start screen.
- A dot next to the ZORA logo shows co-op status: connecting, connected, or partner here.
- A two-tracker co-op test is built in. It ran against every sync type before this release.

### Paste Flags
- Paste a seed file name, or drop the seed file on the dialog, and both flag strings fill in.
- Dropping a tracker save fills in its ZORA flags.
- Your ZORA flags are saved with the save file and come back when you load it.
- The dialog shows what each flag did, and a Done button closes it when you're finished.

### Fixes and polish
- More than 50 bug fixes and improvements, from three full code reviews. They cover co-op, saves, the web version, Paste Flags, and the tracker UI.
- The 4th Sword option is in Overworld + Extras during a run, and it shows its location: L9, L2, or none.
- Turning the 4th Sword on also turns on Progressive Items. Turning Progressive Items on does not turn on the 4th Sword.
- Thanks to Tetra (tetraly), who wrote the Avalonia port this release is built on.
- "Boots Replace Boomerangs" is now "Pegasus Boots".
- The name is written ZORATracker everywhere.

### Mac users
The app is now ZORATracker.app. Your saves are in a Saves folder inside the old ZORA Tracker.app (right-click it, choose Show Package Contents, then open Contents, then MacOS). Copy that Saves folder into the new app's Contents/MacOS folder before you delete the old app.

## Discord

```
**ZORATracker 3.0**

**New: use the tracker in your browser**
• Runs at https://zoratracker.gg, with nothing to install.
• Saves and settings stay in your browser. Only one tab saves at a time.

**Co-op, rebuilt**
• Hosted co-op server: https://coop.zoratracker.gg. Nothing to host yourself.
• Desktop and browser trackers sync with each other.
• New sync: each field has its own version, so the newest edit wins on both sides, even after a restart or reconnect.
• More syncs now: tile extras, shop prices, box tiers, notes, hints and more.
• The host's start-screen options go to your partner.
• Status dot next to the ZORA logo: connecting, connected, partner here.

**Paste Flags**
• Paste or drop a seed file name to fill both flag strings.
• Your ZORA flags are saved with the save file.

**Fixes and polish**
• More than 50 bug fixes and improvements, from three full code reviews.
• The 4th Sword option is in Overworld + Extras during a run, and shows its location: L9, L2, or none.
• Turning on the 4th Sword also turns on Progressive Items. The reverse does not happen.
• Thanks to Tetra (tetraly), who wrote the Avalonia port this release is built on.
• "Boots Replace Boomerangs" is now "Pegasus Boots".

**Mac users:** your saves are inside the old ZORA Tracker.app (Show Package Contents, Contents, MacOS, Saves). Copy that Saves folder into the new app's Contents/MacOS folder before you delete the old app.

Download or update via the app.
Please report any bugs here or on Issues on Github: https://github.com/TheGobOne/ZoraTracker/issues
```
