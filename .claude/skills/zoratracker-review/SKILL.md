---
name: zoratracker-review
description: Send read-only review agents over the whole ZORATracker repo and merge their findings into one ranked list in a .md file. Use when the user says "review the repo", "send the agents", "check the whole repo", "audit ZORATracker", or asks what is broken or risky across the code, even if they don't name this skill. Reviews only. It never edits, commits, builds a release, or fixes findings.
---

# ZORATracker review

Repo: `C:\Users\Ryan\code\ZoraTracker`. Write the name as ZORATracker, one word, in everything you write. Do not work in `C:\Users\Ryan\source\repos\ZoraTracker`.

This skill only reads and reports. Fixing is a separate step the user starts after reading the findings.

## Rules for every agent

- Read only. No edits, no commits, no pushes, no builds in `Z1R_Avalonia_Desktop\bin\Release` (the user's live folder), and never run `--selftest` there.
- WPF (`Z1R_WPF`, `Z1R_Tracker\Z1R_WPF`) is bugfix only. Review it only for crashes and data loss. New features belong in Avalonia only.
- 4th Sword must enable Progressive Items, and Progressive Items must not enable 4th Sword. Check any change near those options against this.
- Tetra is she/her.
- Read whole enclosing functions, not only the changed lines.
- Report up to 10 findings, most severe first. Each has `file:line`, a one-sentence defect, a concrete failure scenario (inputs or state, then the wrong result), and severity and confidence. Say plainly when an area looks clean.
- Writing: short plain sentences, no em dashes, no puffery.

## Step 1: scope

1. `git status --short` and `git tag --sort=-creatordate | head -3`.
2. `git diff --stat <last tag>` to see what changed since the last release. Give each agent the tag so it can run `git diff <tag> -- <its files>`. A full-repo review reads all of its files; the diff is where to look first.

## Step 2: launch the agents

Launch all in one message with `run_in_background: true`, using `general-purpose`. Give each its files and the context below.

| Agent | Files | Context |
|---|---|---|
| 1. Tracker model | `Z1R_Tracker\*.fs` (not the WPF folder), `Z1R_Tracker_Core` | Item rules, progressive items and the 4th sword, Level 9 entry (triforce count or an entry item), shop prices and take-any state, hidden dungeon numbers. |
| 2. Overworld and main tracker UI | `Z1R_Avalonia\WPFUI.cs`, `OverworldItemGridUI.cs`, `OverworldMapTileCustomization.cs`, `MainTrackerTriforcesAndItems.cs`, `DrawingLayer.cs`, `Graphics.cs`, `PieMenus.cs` | Overworld circles and markers, item boxes, Triforce placement, mirrored overworld, redraw after load. |
| 3. Dungeon and options UI | `Dungeon*.cs`, `OptionsMenu.cs`, `CustomComboBoxes.cs`, `Views.cs`, `HotKeys.cs`, `Reminders.cs`, `Broadcast.cs` | Dungeon map rooms and hints, options that gate drawing, hotkey loading, broadcast window. |
| 4. Co-op and sync | `CoopSync.cs`, `CoopTest.cs`, `Z1R_Sync`, `Z1R_Avalonia_Sync`, `Z1R_SignalRHost*`, co-op handlers in `Program.cs` | Older peers (for example 3.0.4) receiving new item ids or fields, message formats, simultaneous edits. |
| 5. Saves, flags and loading | `FlagPaste.cs`, `DungeonSaveAndLoad.cs`, `LoadDump.cs`, `SelfTest.cs`, the Paste Flags dialog and startup in `Program.cs`, settings code in `Z1R_Tracker\TrackerModelOptions.fs` | Old saves must still load. The ZORA flag tables must match ZORA's `zora/flags` source (`C:\Users\Ryan\code\zora`, remote `upstream`). `specZoraVersion` must be current. |
| 6. Build, web and release | `Z1R_Avalonia_Browser`, `.github\workflows`, `Directory.Build.Props`, `GlobalAssemblyInfo.fs`, `CurrentReleaseLabel` in `CustomComboBoxes.cs`, `Browser.cs`, `Platform.cs` | The web build still compiles (`dotnet build Z1R_Avalonia_Browser/Z1R_Avalonia_Browser.csproj -c Debug`), Mac and Linux builds, version strings match the tag, `#if BROWSER` blocks. This agent may run that one web Debug build and nothing else. |

## Step 3: merge and write the findings file

1. When all agents report, dedupe and rank.
2. Write `C:\Users\Ryan\code\ZoraTracker\.claude\review-findings.md`, grouped by priority: Critical (crash, data loss, wrong tracking), High, Medium, Low and cleanup. Each finding is one numbered line with a checkbox `- [ ]`, then `file:line`, the defect, and the failure scenario.
3. Keep a link to that file in the session rail, and update it (tick `- [x]`, or remove the line) whenever a finding is fixed or cleared.
4. Do not commit the file.

## Step 4: report

Reply with the top five findings (one line each, with a time estimate to fix), the counts per priority, and the file link. State which areas looked clean and what was not reviewed. End with one concrete next action, for example "say which findings to fix first". Do not start fixing.
