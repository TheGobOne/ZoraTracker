// Two-tracker co-op test. Run two copies of the tracker, from two different folders, through the relay:
//   ZoraTracker.CrossPlatform.exe --cooptest=A --cooptest-dir=<shared folder> --cooptest-id=<run id> --quest=1
//   ZoraTracker.CrossPlatform.exe --cooptest=B --cooptest-dir=<shared folder> --cooptest-id=<run id> --quest=1
// (--quest=1 is the 1st Quest, 2 the 2nd Quest. Both sides must use the same quest and run id.)
// Each side starts a run, turns co-op on in code (not in the settings file), and waits for the other. Then A makes a scripted list of changes,
// one step at a time, through the same model calls the click handlers use, and after each step writes its normalized save state to
// step-N-A.json. B polls its own state until it matches or 15 s pass, and appends PASS or FAIL (with a line diff) to cooptest_report.txt.
// Then B makes a second list of changes (mostly undoing and changing A's) and A checks. Failures do not stop the run.
// Exit code: 0 every check this side made passed, 1 a check failed, 2 the harness itself failed (no connection, the other side never answered, the 10 minute limit).
// Each side keeps its settings in <dir>/settings-A or <dir>/settings-B, so the two never write the same settings file.
// The step files and the report go in <dir>/run-<run id>; use a new run id for each run. --cooptest-relay=<url> (or the environment variable
// ZORATRACKER_COOPTEST_RELAY) picks another relay; the default is https://coop.zoratracker.gg.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.FSharp.Core;
using Z1R_Sync;
using Z1R_Tracker.Models.Z1R_TrackerInterop;

namespace Z1R_Avalonia;

public static class CoopTest
{
    private static readonly string[] args = Environment.GetCommandLineArgs();
    private static string argValue(string name) => args.Where(a => a.StartsWith(name + "=")).Select(a => a.Substring(name.Length + 1)).FirstOrDefault();

    public static readonly string role = argValue("--cooptest");   // "A" or "B"
    public static readonly bool isActive = role == "A" || role == "B";
    private static readonly string dir = argValue("--cooptest-dir") ?? "";
    private static readonly string runId = argValue("--cooptest-id") ?? "cooptest";
    private static readonly int quest = int.TryParse(argValue("--quest"), out var q) && q == 2 ? 2 : 1;
    private static string other => role == "A" ? "B" : "A";

    /// The settings folder for this side (Program uses it in place of the normal one).
    public static string settingsDir => Path.Combine(dir, "settings-" + role);

    // The files the two sides exchange go in a folder of their own for each run id, so files from an earlier run cannot pass as this run's.
    // (Each side also deletes the files it writes itself when it starts, in case a run id is used again; it never deletes the other side's.)
    private static string runDir => Path.Combine(dir, "run-" + string.Concat(runId.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)));

    // The relay: --cooptest-relay=<url>, else the ZORATRACKER_COOPTEST_RELAY environment variable, else the public relay.
    private static readonly string relay =
        argValue("--cooptest-relay") ?? (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZORATRACKER_COOPTEST_RELAY")) ? null : Environment.GetEnvironmentVariable("ZORATRACKER_COOPTEST_RELAY").Trim())
        ?? "https://coop.zoratracker.gg";
    private static readonly TimeSpan overallLimit = TimeSpan.FromMinutes(10);
    private static int failures = 0, passes = 0;

    private static string reportPath => Path.Combine(runDir, "cooptest_report.txt");
    private static void report(string s)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {role}: {s}\n";
        Console.Write(line);
        for (int tries = 0; tries < 20; tries++)   // (both sides append to the same file)
        {
            try { File.AppendAllText(reportPath, line); return; }
            catch (IOException) { System.Threading.Thread.Sleep(25); }
            catch (Exception) { return; }
        }
    }

    private static void writeFile(string name, string text)
    {
        var path = Path.Combine(runDir, name);
        var tmp = path + "." + role + ".tmp";
        File.WriteAllText(tmp, text);
        File.Move(tmp, path, true);   // (the other side never sees a half-written file)
    }
    private static bool exists(string name) => File.Exists(Path.Combine(runDir, name));
    private static string readFile(string name) { try { return File.ReadAllText(Path.Combine(runDir, name)); } catch { return null; } }

    /// Deletes what this side writes (ready, finished, its own steps, its checks of the other side's steps, its temp files).
    private static void clearOwnFiles()
    {
        var mine = new[] { $"ready-{role}.txt", $"finished-{role}.txt", $"step-*-{role}.json", $"step-*-{other}.json.done", $"*.{role}.tmp" };
        foreach (var pattern in mine)
            foreach (var f in Directory.GetFiles(runDir, pattern))
            {
                // (Windows matches "step-*-A.json" against "step-1-A.json.done" too: keep the other side's .done files)
                var name = Path.GetFileName(f);
                if (pattern.EndsWith(".json") && name.EndsWith(".done")) continue;
                try { File.Delete(f); } catch (Exception) { }
            }
    }

    private static void exit(int code, string why)
    {
        report($"EXIT {code}: {why} ({passes} passed, {failures} failed)");
        try { File.WriteAllText(Path.Combine(runDir, $"finished-{role}.txt"), code.ToString()); } catch { }
        Environment.Exit(code);   // (the app's own shutdown path always returns 0)
    }

    // ---- the state that is compared ----

    /// The tracker's save, without what legitimately differs between two trackers: times, the timeline, which dungeon tab is open.
    public static string snapshot()
    {
        var lines = SaveAndLoad.BuildSaveLines(OverworldItemGridUI.notesTextBox?.Text ?? "", 0, DungeonUI.exportDungeonModelsJsonLines(), DungeonSaveAndLoad.SaveDrawingLayer(),
                                               Graphics.alternativeOverworldMapFilename ?? "", Graphics.shouldInitiallyHideOverworldMap, 0, false, false);
        var keep = new List<string>();
        foreach (var l0 in lines.SelectMany(l => l.Split('\n')))
        {
            var l = l0.TrimEnd();
            if (l.Contains("\"Timeline\": [")) break;   // (the timeline is last: its times differ, so it and the rest are left out)
            if (l.Contains("\"TimeInSeconds\"") || l.Contains("\"DungeonTabSelected\"") || l.Contains("\"OverworldSpotsRemainingOverTime\"") || l.Contains("\"CurrentRecorderDestinationIndex\""))
                continue;
            keep.Add(l);
        }
        return string.Join("\n", keep);
    }

    /// The notes, compared apart from the rest and reported as INFO, never as a failure, until it is decided whether notes sync.
    public static string notesLine() => System.Text.Json.JsonSerializer.Serialize(OverworldItemGridUI.notesTextBox?.Text ?? "");
    private const string notesSeparator = "\n#NOTES ";

    private static string diff(string expected, string actual)
    {
        var e = expected.Split('\n'); var a = actual.Split('\n');
        var sb = new System.Text.StringBuilder();
        var n = 0;
        for (int k = 0; k < Math.Max(e.Length, a.Length) && n < 40; k++)
        {
            var el = k < e.Length ? e[k] : "<none>";
            var al = k < a.Length ? a[k] : "<none>";
            if (el != al)
            {
                sb.Append($"      line {k + 1}\n        {other}: {el.Trim()}\n        {role}: {al.Trim()}\n");
                n++;
            }
        }
        if (e.Length != a.Length) sb.Append($"      ({other} has {e.Length} lines, {role} has {a.Length}; after the first difference the lines may be shifted)\n");
        return sb.ToString();
    }

    // ---- start-up helpers (as the self-test does it: call the button's own click code) ----

    private static string label(Visual e) => e is ContentControl cc ? (cc.Content switch { string s => s.Trim(), TextBlock t => (t.Text ?? "").Trim(), TextBox t => (t.Text ?? "").Trim(), _ => "" }) : "";
    private static void click(Control b)
    {
        for (var t = b.GetType(); t != null; t = t.BaseType)
        {
            var m = t.GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
            if (m != null) { m.Invoke(b, null); return; }
        }
    }
    private static Control find(Window w, string text) =>
        w.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => (c is Button || c is ToggleButton) && c.IsEffectivelyVisible && label(c) == text);

    // ---- the scripted changes ----

    private static List<(int, int)> free;   // overworld tiles that can be marked, in a fixed order (the same on both sides: same quest)
    private static (int, int) T(int k) => free[k % free.Count];
    private static readonly TrackerModel.PlayerHas YES = TrackerModel.PlayerHas.YES, NO = TrackerModel.PlayerHas.NO;

    private static void setTile((int, int) t, int v)
    {
        if (!TrackerModel.overworldMapMarks[t.Item1, t.Item2].AttemptToSet(v))
            report($"  (note: tile {t} refused value {v}; the step goes on)");
        WPFUI.redrawOwTileHook(t.Item1, t.Item2);
    }
    private static void decor((int, int) t, Func<int, int> f)
    {
        WPFUI.setDecorIcons(t.Item1, t.Item2, f(TrackerModel.owSwordIcons[t.Item1, t.Item2]));
        WPFUI.redrawOwTileHook(t.Item1, t.Item2);
    }
    private static void room(int level, int x, int y, Action<DungeonRoomState> change)
    {
        var cur = DungeonUI.masterRoomStatesPublic[level - 1][x, y];
        var rs = DungeonRoomState.FromCSharp(cur.CSharp);   // (a copy, as the room popup makes)
        rs.Level = level; rs.X = x; rs.Y = y;
        change(rs);
        DungeonUI.setNewValueFunctions[level - 1][x, y](rs);
    }

    private static List<(string name, Action act)> steps(int phase)
    {
        const int ARROW_SHOP = 16, HINT_SHOP = 32;
        var d3 = T(2); var shop = T(ARROW_SHOP); var hint = T(HINT_SHOP); var dup = T(38); var bomb = T(24); var circ = T(25); var start = T(26);
        var s = new List<(string, Action)>();
        if (phase == 0)
        {
            s.Add(("initial state (no change)", () => { }));
            s.Add(("tiles: dungeons 1-9", () => { for (int k = 0; k <= 8; k++) setTile(T(k), k); }));
            s.Add(("tiles: warps and sword caves", () => { for (int k = 9; k <= 15; k++) setTile(T(k), k); }));
            s.Add(("tiles: item shops", () => { for (int k = 16; k <= 23; k++) setTile(T(k), k); }));
            s.Add(("tiles: secrets, letter, armos, hint shop, take any, potion shop, dark X, personal", () => { for (int k = 24; k <= 37; k++) setTile(T(k), k); }));
            s.Add(("dungeon 3 tile with a magical sword icon", () => decor(d3, p => TrackerModel.decorIconsToggle(p, TrackerModel.ITEMS.MAGICALSWORD))));
            s.Add(("collected checkmark on that icon", () => decor(d3, p => TrackerModel.decorToggleCollected(p, FSharpOption<int>.Some(TrackerModel.ITEMS.MAGICALSWORD)))));
            s.Add(("SHOP 2 label and a blue ring on the arrow shop", () => { WPFUI.setShopLabel(shop.Item1, shop.Item2, 2); decor(shop, p => TrackerModel.decorIconsToggle(p, TrackerModel.ITEMS.BLUERING)); }));
            s.Add(("second shop item (extra data) on the arrow shop", () =>
            {
                TrackerModel.setOverworldMapExtraData(shop.Item1, shop.Item2, TrackerModel.MapSquareChoiceDomainHelper.SHOP, TrackerModel.MapSquareChoiceDomainHelper.ToItem(17));
                WPFUI.redrawOwTileHook(shop.Item1, shop.Item2);
            }));
            s.Add(("bomb dropper", () => { TrackerModel.owBombDroppers[bomb.Item1, bomb.Item2] = true; TrackerModel.forceUpdate(); WPFUI.redrawOwTileHook(bomb.Item1, bomb.Item2); }));
            s.Add(("shop price 80", () => TrackerModel.setShopPrice(shop.Item1, shop.Item2, 80)));
            s.Add(("hint shop color", () => { TrackerModel.hintShopColors[Tuple.Create(hint.Item1, hint.Item2)] = 2; WPFUI.redrawOwTileHook(hint.Item1, hint.Item2); }));
            s.Add(("duplicate the dungeon 3 markers onto another tile", () =>
            {
                WPFUI.setDecorIcons(dup.Item1, dup.Item2, TrackerModel.decorEntriesPack(TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[d3.Item1, d3.Item2])));
                WPFUI.linkDuplicate(d3, dup);
                WPFUI.copyTileTiers(d3, dup);
                WPFUI.redrawOwTileHook(dup.Item1, dup.Item2);
            }));
            s.Add(("circle on a tile", () => { TrackerModel.overworldMapCircles[circ.Item1, circ.Item2] = 1; TrackerModel.forceUpdate(); WPFUI.redrawOwTileHook(circ.Item1, circ.Item2); }));
            s.Add(("start spot", () => { TrackerModel.startIconX = start.Item1; TrackerModel.startIconY = start.Item2; TrackerModel.forceUpdate(); }));
            s.Add(("item boxes: bow, heart, white sword box", () =>
            {
                TrackerModel.GetDungeon(0).Boxes[0].Set(TrackerModel.ITEMS.BOW, YES);
                TrackerModel.GetDungeon(1).Boxes[0].Set(TrackerModel.ITEMS.HEARTCONTAINER, NO);
                TrackerModel.sword2Box.Set(TrackerModel.ITEMS.HEARTCONTAINER, YES);
            }));
            s.Add(("progressive sword in a box (tier)", () => TrackerModel.GetDungeon(2).Boxes[0].Set(TrackerModel.ITEMS.PROGRESSIVESWORD, YES)));
            s.Add(("dungeon 1 rooms", () =>
            {
                room(1, 1, 6, r => { r.RoomType = RoomType.NonDescript; r.IsComplete = true; });
                room(1, 2, 6, r => r.RoomType = RoomType.ItemBasement);
            }));
            s.Add(("dungeon 1 doors", () =>
            {
                DungeonUI.horizontalDoorsPerLevel[0][1, 6].State = Dungeon.DoorState.YES;
                DungeonUI.verticalDoorsPerLevel[0][2, 5].State = Dungeon.DoorState.NO;
            }));
            s.Add(("room completed", () => room(1, 2, 6, r => r.IsComplete = true)));
            s.Add(("player progress: bombs, wood sword", () =>
            {
                TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBombs.Set(true);
                TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword.Set(true);
            }));
            s.Add(("starting items: bow", () => TrackerModel.startingItemsAndExtras.PlayerHasBow.Set(true)));
            s.Add(("dungeon 1 triforce", () => { if (!TrackerModel.GetDungeon(0).PlayerHasTriforce()) TrackerModel.GetDungeon(0).ToggleTriforce(); }));
            s.Add(("notes", () => OverworldItemGridUI.notesTextBox.Text = "co-op test note from A"));
        }
        else
        {
            s.Add(("clear dungeon tiles 1-4, move dungeon 1", () => { for (int k = 0; k <= 3; k++) setTile(T(k), -1); setTile(T(39), 0); }));
            s.Add(("un-collect the checkmark", () => decor(d3, p => TrackerModel.decorToggleCollected(p, FSharpOption<int>.Some(TrackerModel.ITEMS.MAGICALSWORD)))));
            s.Add(("remove the magical sword icon", () => decor(d3, p => TrackerModel.decorIconsToggle(p, TrackerModel.ITEMS.MAGICALSWORD))));
            s.Add(("SHOP 3 label, remove the blue ring", () => { WPFUI.setShopLabel(shop.Item1, shop.Item2, 3); decor(shop, p => TrackerModel.decorIconsToggle(p, TrackerModel.ITEMS.BLUERING)); }));
            s.Add(("bomb dropper off", () => { TrackerModel.owBombDroppers[bomb.Item1, bomb.Item2] = false; TrackerModel.forceUpdate(); WPFUI.redrawOwTileHook(bomb.Item1, bomb.Item2); }));
            s.Add(("shop price 120", () => TrackerModel.setShopPrice(shop.Item1, shop.Item2, 120)));
            s.Add(("hint shop color changed", () => { TrackerModel.hintShopColors[Tuple.Create(hint.Item1, hint.Item2)] = 1; WPFUI.redrawOwTileHook(hint.Item1, hint.Item2); }));
            s.Add(("remove the duplicate", () => { WPFUI.setDecorIcons(dup.Item1, dup.Item2, -1); WPFUI.unlinkDuplicateTile(dup.Item1, dup.Item2); WPFUI.redrawOwTileHook(dup.Item1, dup.Item2); }));
            s.Add(("circle off", () => { TrackerModel.overworldMapCircles[circ.Item1, circ.Item2] = 0; TrackerModel.forceUpdate(); WPFUI.redrawOwTileHook(circ.Item1, circ.Item2); }));
            s.Add(("start spot moved", () => { TrackerModel.startIconX = T(27).Item1; TrackerModel.startIconY = T(27).Item2; TrackerModel.forceUpdate(); }));
            s.Add(("item boxes changed", () =>
            {
                TrackerModel.GetDungeon(0).Boxes[0].Set(-1, NO);
                TrackerModel.GetDungeon(1).Boxes[0].Set(TrackerModel.ITEMS.HEARTCONTAINER, YES);
                TrackerModel.sword2Box.Set(TrackerModel.ITEMS.LADDER, NO);
            }));
            s.Add(("second progressive sword (tier 2)", () => TrackerModel.GetDungeon(3).Boxes[0].Set(TrackerModel.ITEMS.PROGRESSIVESWORD, YES)));
            s.Add(("dungeon 1 room changed", () => room(1, 1, 6, r => { r.RoomType = RoomType.MaybePushBlock; r.IsComplete = false; })));
            s.Add(("dungeon 1 doors changed", () =>
            {
                DungeonUI.horizontalDoorsPerLevel[0][1, 6].State = Dungeon.DoorState.PURPLE;
                DungeonUI.verticalDoorsPerLevel[0][2, 5].State = Dungeon.DoorState.UNKNOWN;
            }));
            s.Add(("player progress off", () => TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBombs.Set(false)));
            s.Add(("starting items: bow off", () => TrackerModel.startingItemsAndExtras.PlayerHasBow.Set(false)));
            s.Add(("dungeon 1 triforce off", () => { if (TrackerModel.GetDungeon(0).PlayerHasTriforce()) TrackerModel.GetDungeon(0).ToggleTriforce(); }));
            s.Add(("notes changed", () => OverworldItemGridUI.notesTextBox.Text = "co-op test note from B"));
        }
        return s;
    }

    // ---- the run ----

    private static async Task waitFor(Func<bool> cond, TimeSpan limit, int everyMs = 250)
    {
        var until = DateTime.UtcNow + limit;
        while (!cond() && DateTime.UtcNow < until) await Task.Delay(everyMs);
    }

    private static async Task run(Window win)
    {
        report($"co-op test {runId}, quest {quest}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        TrackerModelOptions.SuppressAudioForSelfTest = true;

        // 1. start a run (Progressive Items on, so tiers are covered)
        var progressive = win.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault(c => label(c).StartsWith("Progressive"));
        if (progressive != null) progressive.IsChecked = true;
        var questButton = find(win, quest == 2 ? "2nd Quest" : "1st Quest");
        if (questButton == null) { exit(2, "no quest start button on the start screen"); return; }
        click(questButton);
        await waitFor(() => find(win, "Overworld + Extras") != null, TimeSpan.FromSeconds(90), 500);
        if (find(win, "Overworld + Extras") == null) { exit(2, "the main screen never appeared"); return; }
        await waitFor(() => CoopSync.startupBaselineDone, TimeSpan.FromSeconds(30));
        await Task.Delay(1500);

        // 2. co-op on, in code
        TrackerModelOptions.CoopSyncOptions.FunctionAppBase = relay;
        TrackerModelOptions.CoopSyncOptions.baseNegotiateUrl = "/api/negotiate";
        TrackerModelOptions.CoopSyncOptions.baseSyncUpdateUrl = "/api/SyncUpdate";
        TrackerModelOptions.CoopSyncOptions.MyConsoleId = $"{runId}-{role}";
        TrackerModelOptions.CoopSyncOptions.TargetConsoleId = $"{runId}-{other}";
        TrackerModelOptions.CoopSyncOptions.SetEnableCoop(true);
        await waitFor(() => SyncManager.IsConnected, TimeSpan.FromSeconds(60), 500);
        if (!SyncManager.IsConnected) { exit(2, $"could not connect to {relay}"); return; }
        report("connected");

        // 3. both ready (the hello exchange happens on connect; give it a moment)
        free = new List<(int, int)>();
        for (int j = 0; j <= 7; j++)
            for (int i = 0; i <= 15; i++)
                if (!TrackerModel.owInstance.AlwaysEmpty(i, j) && TrackerModel.overworldMapMarks[i, j].Current() == -1) free.Add((i, j));
        writeFile($"ready-{role}.txt", DateTime.UtcNow.ToString("o"));
        await waitFor(() => exists($"ready-{other}.txt"), TimeSpan.FromSeconds(180), 500);
        if (!exists($"ready-{other}.txt")) { exit(2, $"{other} never got ready"); return; }
        await Task.Delay(4000);
        report($"both ready ({free.Count} free overworld tiles)");

        // 4. A acts and B checks, then B acts and A checks
        var n = 0;
        for (int phase = 0; phase <= 1; phase++)
        {
            var actor = phase == 0 ? "A" : "B";
            foreach (var (name, act) in steps(phase))
            {
                n++;
                var stepFile = $"step-{n}-{actor}.json";
                if (role == actor)
                {
                    try { act(); }
                    catch (Exception e) { report($"  (note: step {n} '{name}' threw: {e.GetType().Name}: {e.Message})"); }
                    await Task.Delay(2500);   // (the senders debounce for up to about 0.7 s)
                    writeFile(stepFile, snapshot() + notesSeparator + notesLine());
                    await waitFor(() => exists(stepFile + ".done"), TimeSpan.FromSeconds(60), 250);
                    if (!exists(stepFile + ".done")) { exit(2, $"{other} never checked step {n}"); return; }
                }
                else
                {
                    await waitFor(() => exists(stepFile), TimeSpan.FromSeconds(90), 250);
                    var expectedAll = readFile(stepFile);
                    if (expectedAll == null) { exit(2, $"{actor} never wrote step {n}"); return; }
                    var cut = expectedAll.IndexOf(notesSeparator, StringComparison.Ordinal);
                    var expected = cut < 0 ? expectedAll : expectedAll.Substring(0, cut);
                    var expectedNotes = cut < 0 ? null : expectedAll.Substring(cut + notesSeparator.Length);
                    var ours = snapshot();
                    var until = DateTime.UtcNow + TimeSpan.FromSeconds(15);
                    while (ours != expected && DateTime.UtcNow < until)
                    {
                        await Task.Delay(500);
                        ours = snapshot();
                    }
                    if (ours == expected) { passes++; report($"PASS step {n} ({actor} -> {role}): {name}"); }
                    else { failures++; report($"FAIL step {n} ({actor} -> {role}): {name}\n{diff(expected, ours)}"); }
                    if (expectedNotes != null && expectedNotes != notesLine())
                        report($"INFO step {n} ({actor} -> {role}): notes differ (not compared until notes sync is decided): {other} {expectedNotes}, {role} {notesLine()}");
                    writeFile(stepFile + ".done", ours == expected ? "pass" : "fail");
                }
            }
        }
        exit(failures == 0 ? 0 : 1, "done");
    }

    public static void arm()
    {
        if (string.IsNullOrEmpty(dir)) { Console.WriteLine("--cooptest needs --cooptest-dir=<shared folder>"); Environment.Exit(2); }
        Directory.CreateDirectory(runDir);
        clearOwnFiles();   // (stale files from an earlier run with this id gave false passes)
        // the 10 minute limit for this side
        DispatcherTimer.RunOnce(() => exit(2, "the 10 minute limit was reached"), overallLimit);
        var started = false;
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0) };
        t.Tick += (_, _) =>
        {
            if (started) return;
            var w = Graphics.theWindow;
            if (w != null && w.IsLoaded && w.Content != null)
            {
                started = true;
                t.Stop();
                run(w).ContinueWith(task =>
                {
                    if (task.IsFaulted) exit(2, "the test itself broke: " + task.Exception?.GetBaseException());
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        };
        t.Start();
    }
}
