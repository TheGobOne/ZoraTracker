---
name: zoratracker-prerelease-review
description: Run the full pre-release code review and fix pass for ZORATracker. Use when the user says "review the code before release", "send the agents to review", "pre-release check", "review since the last release", "fix all the findings", or asks for a 1.0 or beta release check, even if they don't name this skill. Launches five parallel read-only review agents by area, merges their findings into one ranked list, splits fixes across agents by file ownership, then verifies with a Debug build, a self-test in a scratch copy, the co-op two-instance test, and Release and web builds.
---

# ZORATracker pre-release review

Repo: `C:\Users\Ryan\source\repos\ZoraTracker`. Write the name as ZORATracker, one word, in everything you write.

The review is read-only until the user picks what to fix. Reviewing and fixing are separate phases because the user decides which findings are worth the risk before a release.

## Rules that apply to every step

- Commit, push and deploy only when the user asks. Agents never do any of them.
- Never run anything inside `Z1R_Avalonia_Desktop\bin\Release` (the user's live folder), and never run `--selftest` there: it rewrites the user's settings. Test in a copy under the scratchpad.
- The user tests the Release build, so rebuild Release (`dotnet build Z1R_Avalonia_Desktop/Z1R_Avalonia_Desktop.csproj -c Release`) after changes. It fails at the copy step if the tracker is running; ask the user to close it.
- Skip the self-test for small text or color changes. Rebuild Release and stop.
- 4th Sword enables Progressive Items, never the reverse. Reviewers and fixers must check changes against this.
- Tetra is she/her.
- Show draft release notes to the user before publishing anything.
- Web deploy steps are in memory `reference_zoratracker-web-deploy`: publish, scp to `/tmp/zt-web`, then `zt-deploy`.
- Do not delete folders with `Remove-Item -Recurse`: a hook blocks it. Use a new folder name for each scratch copy.

## Phase 1: scope

1. `git tag --sort=-creatordate | head -5` for the last release tag, and `git status --short` for uncommitted work.
2. `git diff --stat <tag>` shows what changed (it includes uncommitted changes). Sort the changed files into the five areas below. Tell each agent the tag so it runs `git diff <tag> -- <files>`.

## Phase 2: five review agents, in parallel

Launch all five in one message with `run_in_background: true`. Use `general-purpose` for four and `UIUX` for the fifth. Every prompt says: read only, no edits, no commits, never touch `bin\Release`, read whole enclosing functions not just hunks, report up to 10 findings most severe first with `file:line`, a one-sentence defect, a concrete failure scenario, severity and confidence, and say plainly when an area looks clean.

| Area | Files |
|---|---|
| Co-op sync | `Z1R_Avalonia/CoopSync.cs`, `CoopTest.cs`, `Z1R_Sync/SyncManager.cs`, `Z1R_SignalRHost/Startup.cs`, co-op receive handlers in `Program.cs` |
| Model, save, flags | `Z1R_Tracker/*.fs`, `DungeonSaveAndLoad.cs`, `FlagPaste.cs`, `SelfTest.cs`, the Paste Flags dialog in `Program.cs` |
| Web build and startup | `Browser.cs`, `Platform.cs`, `Z1R_Avalonia_Browser/wwwroot/index.html` and `main.js`, `#if BROWSER` blocks and the start screen in `Program.cs`, `Broadcast.cs`, `HotKeys.cs` |
| Tracker UI | `Dungeon*.cs`, `OverworldItemGridUI.cs`, `OverworldMapTileCustomization.cs`, `OptionsMenu.cs`, `Popouts.cs`, `Views.cs`, `WPFUI.cs`, `CustomComboBoxes.cs`, and the other UI files in the diff |
| UI/UX start screen | Start screen layout, Paste Flags dialog, Options additions, co-op dot. It builds Debug, copies it to its own scratchpad folder, and takes screenshots there |

Give each agent the context that matters for its area: the ZORA spec at `C:\Users\Ryan\Downloads\zora-flags (1).json` for the flags agent; Boots value 2 means "Off until confirmed"; the web build stores files in localStorage; co-op uses console ids `desktop` and `web` through a SignalR relay.

## Phase 3: merge

When all five report, dedupe, rank, and call `ReportFindings` once with up to 10 findings. Then give the user a short ranked list with rough time estimates, group the rest as "quick fixes", "design work" and "cleanup", and ask what to fix. Anything that changes core merge logic is design work: say so, and offer shipping it labeled beta as the alternative.

## Phase 4: fix agents split by file ownership

Two agents must never edit the same lines. Before launching, write down which files and which line ranges each agent owns (for example: co-op files and `Program.cs` lines for the co-op handlers; model and save files plus the save region of `WPFUI.cs`; web files plus the Paste Flags region of `Program.cs`). Put the ownership list in every prompt, tell agents to re-read and retry if an Edit fails because the file changed, and say that a build error in another agent's file should be retried after a minute, not fixed.

Each fix prompt includes: the findings with approximate line numbers, "verify each defect before changing it and skip with a reason if wrong", the existing comment style (short parenthetical comments, plain English, no em dashes), and old saves must still load. The model/save agent also runs the self-test (below). The co-op agent also runs the co-op test (below). After they finish, call `ReportFindings` again with an `outcome` for every finding.

## Phase 5: verify

Run these yourself on the combined result, in this order:

1. Debug build: `dotnet build Z1R_Avalonia_Desktop/Z1R_Avalonia_Desktop.csproj -c Debug`.
2. Self-test in a copy (skip for small text or color changes):
   - Copy `Z1R_Avalonia_Desktop\bin\Debug\net10.0` to a new folder under the scratchpad, for example `selftest-final-<HHmmss>`.
   - In the copy, delete `*settings*.json` and the recent-loads file.
   - Run `ZoraTracker.CrossPlatform.exe --selftest --quest=0` from the copy with `Start-Process -PassThru` and wait up to about 9 minutes.
   - Read `selftest_report.txt` in the copy: `RESULT: PASS` and 0 failures in every section.
   - A fixed rule can break an older self-test that encoded the old rule. Update the test to the new rule, not the other way round.
3. Co-op test, when co-op code changed: run two copies of the Debug build from the scratchpad, `--cooptest=A` and `--cooptest=B`, both with the same `--cooptest-dir=<shared folder>` and a new `--cooptest-id=<unique run id>` (add `--quest=1`). It uses the production relay unless `--cooptest-relay=<url>` is given. Both sides must pass every step and exit 0. Kill only processes you started, by PID. This test changes one side at a time, so it does not cover simultaneous edits.
4. Release desktop build, then `dotnet build Z1R_Avalonia_Browser/Z1R_Avalonia_Browser.csproj -c Debug` as the web compile check. A full web publish takes 5 to 8 minutes and is only needed when deploying.

## Phase 6: report

State what passed (build, self-test counts, co-op steps) and then a plain list of what was not tested. These always need a person: simultaneous edits from two real trackers, on-screen layout changes, and browser behavior. Give one concrete next action, such as "close the desktop tracker, then test the Release build" or "I can deploy the web build (about 10 minutes) for the two-person test".
