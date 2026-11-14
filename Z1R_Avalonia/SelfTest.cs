// Port of Z1R_WPF/SelfTest.fs.
// Background self-test: `ZoraTracker.CrossPlatform.exe --selftest --quest=N` (N = 0 1st, 1 2nd, 2 Mixed 1Q, 3 Mixed 2Q).
// It runs on the tracker's own UI thread: it clicks controls by calling their click code and raises pointer events on whatever is under a grid of
// points, so it never touches the real mouse or keyboard. Run it from a COPY of the release folder (it changes settings), see selftest.ps1 -Port.
// Writes selftest_report.txt next to the exe, then exits. A crash shows up as a new Z1R_Tracker_crash_log.txt entry.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Z1R_Sync;

namespace Z1R_Avalonia;

public static class SelfTest
{
    private static readonly string[] args = Environment.GetCommandLineArgs();
    public static readonly bool isSelfTest = args.Contains("--selftest");
    private static readonly int questArg =
        args.Where(a => a.StartsWith("--quest=")).Select(a => int.Parse(a.Substring(8))).DefaultIfEmpty(2).First();
    private static readonly string reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest_report.txt");
    private static int failures = 0;
    private static int popupsSeen = 0;   // popups the test opened and closed (a test that never opens one is not testing much)
    private static int clicks = 0;
    private static readonly Dictionary<string, int> hitsByType = new Dictionary<string, int>();
    private static readonly Dictionary<string, int> popupsByType = new Dictionary<string, int>();
    private static void log(string s) { try { File.AppendAllText(reportPath, s + "\n"); } catch { } }
    private static void fail(string s) { failures++; log("FAIL: " + s); }

    private static IEnumerable<Visual> descendants(Visual d) => d.GetVisualDescendants();

    private static string label(Visual e)
    {
        if (e is ContentControl cc)
        {
            switch (cc.Content)
            {
                case string s: return s.Trim();
                case TextBox t: return (t.Text ?? "").Trim();
                case TextBlock t: return (t.Text ?? "").Trim();
            }
        }
        return "";
    }

    // Avalonia has no ButtonBase: a Button, or a ToggleButton (check boxes), is what the WPF test calls a ButtonBase
    private static bool isButtonLike(Visual d) => d is Button || d is ToggleButton;
    private static MethodInfo findOnClick(Type t)
    {
        for (var x = t; x != null; x = x.BaseType)
        {
            var m = x.GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
            if (m != null) return m;
        }
        return null;
    }
    private static void click(Control b)
    {
        clicks++;
        findOnClick(b.GetType()).Invoke(b, null);
    }

    // runs an action; an exception it throws is a finding, not the end of the test
    private static void guard(string what, Action f)
    {
        try { f(); }
        catch (Exception e0)
        {
            var e = e0 is TargetInvocationException t && t.InnerException != null ? t.InnerException : e0;
            var lines = string.Join(" | ", e.ToString().Split('\n').Take(4));
            fail($"{what}: {lines}");
        }
    }

    private static readonly Avalonia.Input.Pointer mouse = new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, true);
    private static void raisePointer(Control target, bool press, MouseButton button)
    {
        var root = TopLevel.GetTopLevel(target);
        var pos = target.TranslatePoint(new Point(target.Bounds.Width / 2.0, target.Bounds.Height / 2.0), root) ?? new Point(0, 0);
        var mods = button == MouseButton.Left ? RawInputModifiers.LeftMouseButton : RawInputModifiers.RightMouseButton;
        var kind = button == MouseButton.Left
            ? (press ? PointerUpdateKind.LeftButtonPressed : PointerUpdateKind.LeftButtonReleased)
            : (press ? PointerUpdateKind.RightButtonPressed : PointerUpdateKind.RightButtonReleased);
        var props = new PointerPointProperties(press ? mods : RawInputModifiers.None, kind);
        var ts = (ulong)Environment.TickCount;
        if (press) target.RaiseEvent(new PointerPressedEventArgs(target, mouse, root, pos, ts, props, KeyModifiers.None));
        else target.RaiseEvent(new PointerReleasedEventArgs(target, mouse, root, pos, ts, props, KeyModifiers.None, button));
    }
    private static void raiseClick(Control target, MouseButton button) { raisePointer(target, true, button); raisePointer(target, false, button); }
    private static void raiseWheel(Control target, double delta)
    {
        var root = TopLevel.GetTopLevel(target);
        var pos = target.TranslatePoint(new Point(target.Bounds.Width / 2.0, target.Bounds.Height / 2.0), root) ?? new Point(0, 0);
        var props = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other);
        target.RaiseEvent(new PointerWheelEventArgs(target, mouse, root, pos, (ulong)Environment.TickCount, props, KeyModifiers.None, new Vector(0, delta)));
    }

    private static int popupCount() { try { return CustomComboBoxes.CanvasManager.TheOnlyCanvasManager.PopupCanvasStack.Count; } catch { return 0; } }
    private static Visual popupRoot() { try { return CustomComboBoxes.CanvasManager.TheOnlyCanvasManager.PopupCanvasStack.Peek(); } catch { return null; } }

    // buttons the test must not press (they open file dialogs, other programs, web pages, or windows that need a person)
    private static readonly string[] avoid = { "save", "load", "hotkey", "popout", "site", "discord", "exit", "restart", "update", "quit", "download", "import", "export", "guided tour", "v2.", "-b1", "log" };
    private static bool isAvoided(string text) { var t = text.ToLowerInvariant(); return avoid.Any(a => t.Contains(a)); }
    private static readonly string[] questButtons = { "1st Quest", "2nd Quest", "Mixed - 1Q", "Mixed - 2Q", "Randomized Quest", "Other..." };

    private static IList<Window> allWindows() =>
#if BROWSER
        new List<Window>();   // (the browser build has no desktop lifetime or real windows; `Window` there is BrowserWindow)
#else
        (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows?.ToList() ?? new List<Window>();
#endif

    private static async Task run(Window win)
    {
        async Task pause(int ms) { await Task.Delay(ms); }
        var stuck = false;
        // buttons like the popout ones open separate windows: close every window except the main one (and keep it from sitting on screen meanwhile)
        var initialWindows = allWindows();   // (the ones that exist before the test starts are left alone)
        void closeExtraWindows()
        {
            foreach (var w in allWindows())
                if (w.IsVisible && !initialWindows.Any(i => ReferenceEquals(i, w)))
                    try { w.Close(); } catch { }
        }
        // closes every popup that is open; a popup that will not close is a failure (the gray-screen kind), and ends the test
        async Task settle(string what)
        {
            if (popupCount() > 0) popupsSeen++;
            var tries = 0;
            while (popupCount() > 0 && tries < 10)
            {
                if (!CustomComboBoxes.DismissTopModal()) tries = 10; else tries++;
                await pause(120);
            }
            closeExtraWindows();
            if (popupCount() > 0 || CustomComboBoxes.GlobalFlag.popupIsActive)
            {
                fail($"a popup was left open after: {what}");
                stuck = true;
            }
        }
        Control find(string text) => descendants(win).FirstOrDefault(d => isButtonLike(d) && label(d) == text) as Control;
        List<CheckBox> checkboxes(Visual root) => descendants(root).OfType<CheckBox>().ToList();
        List<Button> buttons(Visual root) => descendants(root).OfType<Button>().ToList();
        async Task cycle(string what, CheckBox cb)
        {
            for (var i = 1; i <= (cb.IsThreeState ? 4 : 3); i++)
                if (!stuck)
                {
                    guard($"{what} checkbox '{label(cb)}' click {i}", () => click(cb));
                    await settle($"clicking checkbox '{label(cb)}'");
                }
        }

        log($"ZORATracker selftest (port), quest {questArg}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        TrackerModelOptions.SuppressAudioForSelfTest = true;   // hundreds of clicks fire real reminders: keep the run quiet
        // the test right-clicks everything: swallow the built-in Cut / Copy / Paste menus, which pop up at the screen's top-left because the test window sits off-screen
        Control.ContextRequestedEvent.AddClassHandler<Control>((_, ea) => ea.Handled = true, RoutingStrategies.Tunnel);

        // 0. flag decoding (no UI)
        guard("flag decode", () =>
        {
            var (p, err1) = FlagPaste.decodeZ1R("GmCbTYod8E8ige6igkDR1ZyxsOpshPXUUTcU5");
            if (p == null) fail("Z1R flags: " + err1);
            else if (p.Quest != 2) fail("Z1R flags did not decode to Mixed 1Q");
            var (z, err2) = FlagPaste.decodeZora("10.Km67jtU");
            if (z == null) fail("ZORA flags: " + err2);
            else if (!(z.Progressive && z.FourthSword == FlagPaste.Tri.On && z.FourthInLevel2 && z.BootsReplaceBoomerangs == FlagPaste.Tri.Random)) fail("ZORA flags decoded wrong");
            // a version 18 string (the newest fields sit above the ones the tracker reads, so it must still decode and leave them unread)
            var (z18, err18) = FlagPaste.decodeZora("18.1!5I5HHHX0dg");
            if (z18 == null) fail("ZORA flags (version 18): " + err18);
            else if (!(z18.Progressive && z18.FourthSword == FlagPaste.Tri.On && z18.BootsReplaceBoomerangs == FlagPaste.Tri.On)) fail("ZORA flags (version 18) decoded wrong");
            // the spec's current ZORA string version is 22 (fields 42..45 added in 19..22): an all-zero version 22 string must be accepted, and 23 refused
            var (z22, err22) = FlagPaste.decodeZora("22.0");
            if (z22 == null) fail("ZORA flags (version 22): " + err22);
            if (FlagPaste.decodeZora("23.0").error == null) fail("ZORA flags (version 23, newer than the spec) were accepted");
            // "22.0" passes whatever the field table holds, so also build a version 22 string with the four newest fields set.
            // The radixes below are copied from zora-flags.json (spec 1.0, current_version 22), in position order, apart from FlagPaste's own table.
            {
                var specRadixes = new[] { 2, 2, 6, 2, 2,  3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,  2, 2, 4, 2, 3, 3, 3, 3, 3, 3, 2, 4, 3, 4, 4, 4, 4, 2, 3, 3, 8, 8, 2, 3,  3, 2, 3, 3 };
                var digits = new int[specRadixes.Length];
                digits[3] = 1;    // progressive_items On
                digits[6] = 1;    // add_l4_sword On
                digits[42] = 2;   // universal_drops_v2 Random (version 19)
                digits[43] = 1;   // detailed_spoiler_log On (version 20)
                digits[44] = 1;   // wizzrobes_shoot_wallmasters On (version 21)
                digits[45] = 2;   // item_counter Random (version 22)
                var n = System.Numerics.BigInteger.Zero;
                for (var k = specRadixes.Length - 1; k >= 0; k--) n = n * specRadixes[k] + digits[k];
                const string alphabet63 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!";
                var payload = "";
                while (n > 0) { payload = alphabet63[(int)(n % 63)] + payload; n /= 63; }
                var (z22b, err22b) = FlagPaste.decodeZora("22." + payload);
                if (z22b == null) fail($"ZORA flags (version 22, newest fields set, '22.{payload}'): {err22b}");
                else if (!(z22b.Progressive && z22b.FourthSword == FlagPaste.Tri.On)) fail($"ZORA flags (version 22, '22.{payload}') decoded wrong");
                // the same payload read as version 21 leaves item_counter over: it must be refused, not read as other flags
                if (FlagPaste.decodeZora("21." + payload).error == null) fail($"ZORA flags: '21.{payload}' (a version 22 field set) was accepted as version 21");
            }
            // '_' is an alias for '!' (flags copied from a seed file name): both spellings must decode the same
            var (pa, _e1) = FlagPaste.decodeZ1R("8hq4BeR1JXo89BJ2!TFpTP02u8UJ3A");
            var (pb, _e2) = FlagPaste.decodeZ1R("8hq4BeR1JXo89BJ2_TFpTP02u8UJ3A");
            if (pa == null || pb == null || pa.Quest != pb.Quest || pa.ItemShuffle != pb.ItemShuffle || pa.Mirror != pb.Mirror || pa.HideNumbers != pb.HideNumbers) fail("Z1R flags with '_' did not decode like the same flags with '!'");
            var (za, _e3) = FlagPaste.decodeZora("18.1!5I5HHHX0dg");
            var (zb, _e4) = FlagPaste.decodeZora("18.1_5I5HHHX0dg");
            if (za == null || zb == null || za.Progressive != zb.Progressive || za.FourthSword != zb.FourthSword) fail("ZORA flags with '_' did not decode like the same flags with '!'");
        });
        log("flag decode done");

        // 1. start screen: every checkbox through all its states
        foreach (var cb in checkboxes(win))
            if (!stuck && !isAvoided(label(cb))) await cycle("start screen", cb);
        log($"start screen checkboxes done ({failures} failures so far)");
        // start screen buttons that open things (not the ones that start the tracker)
        foreach (var b in buttons(win))
        {
            var text = label(b);
            if (!stuck && text != "" && !isAvoided(text) && !questButtons.Contains(text) && text != "Paste Flags")
            {
                guard($"start screen button '{text}'", () => click(b));
                await pause(200);
                await settle($"start screen button '{text}'");
            }
        }
        // the Guided Tour: every start screen stop (Paste Flags included) must find its anchors, or the tour silently leaves it out
        // (the settings panel's stops are only on screen while it is raised, so those are not checked here)
        var tourPanelOnly = new[] { "Overworld settings", "Dungeon settings", "Reminders", "Auto-save", "Other options", "Co-Op and crash log" };
        foreach (var title in OptionsMenu.startScreenTourMissingStops(win))
            if (!tourPanelOnly.Contains(title)) fail($"Guided Tour: the start screen stop '{title}' has no anchor on screen");
        if (!OptionsMenu.startScreenTourSteps.Any(s => s.title == "Paste Flags")) fail("Guided Tour: no Paste Flags stop");
        // Paste Flags: type both strings, Apply, expect it to close and the options to be set
        var pasteButton = find("Paste Flags");
        if (pasteButton != null && !stuck)
        {
            guard("Paste Flags click", () => click(pasteButton));
            await pause(400);
            var root = popupRoot();
            if (root == null) fail("Paste Flags: the box did not open");
            else
            {
                var boxes = descendants(root).OfType<TextBox>().Where(t => !t.IsReadOnly).ToList();
                var apply = descendants(root).FirstOrDefault(d => isButtonLike(d) && label(d) == "Apply") as Control;
                if (boxes.Count < 2) fail("Paste Flags: the two text boxes were not found");
                else if (apply == null) fail("Paste Flags: no Apply button");
                else
                {
                    boxes[0].Text = "GmCbTYod8E8ige6igkDR1ZyxsOpshPXUUTcU5";
                    boxes[1].Text = "10.Km67jtU";
                    guard("Paste Flags Apply", () => click(apply));
                    await pause(400);
                    if (popupCount() > 0) fail("Paste Flags: Apply did not close the box");
                    var progressive = checkboxes(win).FirstOrDefault(c => label(c).StartsWith("Progressive"));
                    if (progressive == null) fail("Paste Flags: no Progressive Items checkbox");
                    else if (progressive.IsChecked != true) fail("Paste Flags: Progressive Items was not checked");
                }
            }
            await settle("Paste Flags");
        }
        log($"start screen done ({failures} failures so far)");

        // 2. start the tracker
        var questLabel = new[] { "1st Quest", "2nd Quest", "Mixed - 1Q", "Mixed - 2Q" }[questArg];
        var questButton = find(questLabel);
        if (questButton == null || stuck) fail($"no '{questLabel}' start button");
        else guard($"start '{questLabel}'", () => click(questButton));
        var waited = 0;
        while (find("Overworld + Extras") == null && waited < 60) { await pause(500); waited++; }
        if (find("Overworld + Extras") == null)
        {
            fail("the main screen never appeared");
            stuck = true;
        }
        else
        {
            await pause(1500);
            await settle("tracker start");
            log($"tracker started ({failures} failures so far)");
            // a save remembers the Heart Shuffle mode (Off / Intra / Full), so a reload does not follow the start screen
            guard("heart shuffle save round trip", () =>
            {
                var saved = TrackerModel.HeartShuffleMode;
                try
                {
                    foreach (var m in new[] { 0, 1, 2 })
                    {
                        TrackerModel.HeartShuffleMode = m;
                        var json = "{ " + string.Join("\n", SaveAndLoad.SaveOverworld("")) + " }";
                        var d = DungeonSaveAndLoad.LoadAll(json);
                        if (d.Overworld.HeartShuffleMode != m) fail($"Heart Shuffle mode {m} saved and loaded back as {d.Overworld.HeartShuffleMode}");
                    }
                    var old = DungeonSaveAndLoad.LoadAll("{ \"Overworld\": { \"Quest\": 2 } }");
                    if (old.Overworld.HeartShuffleMode != -1) fail("a save without a Heart Shuffle mode should load as -1");
                }
                finally { TrackerModel.HeartShuffleMode = saved; }
            });
            // a save keeps every ZORA option, not just On / Off: Boots and the four Extra / Randomize options each come back Off / On / ? (Random), as do Progressive Items,
            // the 4th Sword, Mirrored Overworld, the custom LEVEL name, the Dead Woods / Lost Hills hint letters, dungeon 9's items and the notes. Nothing is clicked or written to the Saves folder.
            guard("ZORA options save round trip", () =>
            {
                var sBoots = TrackerModel.BootsReplaceBoomerangs; var sBootsR = TrackerModel.BootsRandom; var sProg = TrackerModel.ProgressiveItems;
                var sPB = OverworldData.ExtraPBBlocks; var sRaft = OverworldData.ExtraRaftBlocks; var sDW = OverworldData.DeadWoodsEnabled; var sLH = OverworldData.LostHillsEnabled;
                var sPBR = OverworldData.ExtraPBRandom; var sRaftR = OverworldData.ExtraRaftRandom; var sDWR = OverworldData.DeadWoodsRandom; var sLHR = OverworldData.LostHillsRandom;
                var sDWH = OverworldData.DeadWoodsHint; var sLHH = OverworldData.LostHillsHint; var sDWK = OverworldData.DeadWoodsHintKnown; var sLHK = OverworldData.LostHillsHintKnown;
                var sName = TrackerModel.CustomLevelName; var sMirror = TrackerModel.MirrorOverworld; var sL2 = TrackerModel.level2StairBoxEnabled; var sL9 = TrackerModel.level9FloorBoxEnabled;
                var box9 = TrackerModel.GetDungeon(8).Boxes[0];
                var sBox9Cell = box9.CellCurrent(); var sBox9Has = box9.PlayerHas();
                string build(string notes) => string.Join("\n", SaveAndLoad.BuildSaveLines(notes, 0, new[] { "}" }, new string[0], "", false, 0, false, false));
                try
                {
                    // states: 0 = Off, 1 = On, 2 = ? (Random, counts as On for the extra blocks and as Off for Boots)
                    void setModel(int pb, int raft, int dw, int lh, int boots, int prog)
                    {
                        OverworldData.ExtraPBBlocks = pb != 0; OverworldData.ExtraPBRandom = pb == 2;
                        OverworldData.ExtraRaftBlocks = raft != 0; OverworldData.ExtraRaftRandom = raft == 2;
                        OverworldData.DeadWoodsEnabled = dw != 0; OverworldData.DeadWoodsRandom = dw == 2;
                        OverworldData.LostHillsEnabled = lh != 0; OverworldData.LostHillsRandom = lh == 2;
                        TrackerModel.BootsReplaceBoomerangs = boots == 1; TrackerModel.BootsRandom = boots == 2;
                        TrackerModel.ProgressiveItems = prog == 1;
                    }
                    int st(bool e, bool r) => r ? 2 : e ? 1 : 0;
                    (int, int, int, int, int, int) readModel() =>
                        (st(OverworldData.ExtraPBBlocks, OverworldData.ExtraPBRandom), st(OverworldData.ExtraRaftBlocks, OverworldData.ExtraRaftRandom),
                         st(OverworldData.DeadWoodsEnabled, OverworldData.DeadWoodsRandom), st(OverworldData.LostHillsEnabled, OverworldData.LostHillsRandom),
                         TrackerModel.BootsRandom ? 2 : TrackerModel.BootsReplaceBoomerangs ? 1 : 0, TrackerModel.ProgressiveItems ? 1 : 0);
                    var bad = 0;
                    for (var i = 0; i <= 80; i++)
                    {
                        int pb = i % 3, raft = (i / 3) % 3, dw = (i / 9) % 3, lh = (i / 27) % 3, boots = (i / 2) % 3, prog = i % 2;
                        string dwHint = i % 2 == 0 ? "UDLR" : "", lhHint = i % 2 == 0 ? "" : "LLRR";
                        setModel(pb, raft, dw, lh, boots, prog);
                        OverworldData.DeadWoodsHint = dwHint;
                        OverworldData.LostHillsHint = lhHint;
                        var d = DungeonSaveAndLoad.LoadAll(build("notes"));
                        // forget everything (the opposite of what was saved), then load it back the way a real load does
                        setModel((pb + 1) % 3, (raft + 1) % 3, (dw + 1) % 3, (lh + 1) % 3, (boots + 1) % 3, 1 - prog);
                        OverworldData.DeadWoodsHint = "ZZ";
                        OverworldData.LostHillsHint = "ZZ";
                        SaveAndLoad.ApplyLoadedOptions(d.Overworld);
                        var want = (pb, raft, dw, lh, boots, prog);
                        if (readModel() != want && bad < 5)
                        {
                            bad++;
                            fail($"save round trip: (Extra PB, Raft, Dead Woods, Lost Hills, Boots, Progressive) = {want} (0 = Off, 1 = On, 2 = ?) came back as {readModel()}");
                        }
                        else if ((OverworldData.DeadWoodsHint != dwHint || OverworldData.LostHillsHint != lhHint || OverworldData.DeadWoodsHintKnown != (dwHint != "") || OverworldData.LostHillsHintKnown != (lhHint != "")) && bad < 5)
                        {
                            bad++;
                            fail("save round trip: the Dead Woods / Lost Hills hint letters did not come back");
                        }
                    }
                    // an older save (no Random bits, no boots, no progressive items, no hint letters): those keep whatever is set, and the old On / Off bits still load
                    var old = DungeonSaveAndLoad.LoadAll("{ \"Overworld\": { \"Quest\": 2, \"ExtraBlocks\": 5 } }");
                    if (old.Overworld.ExtraBlocksRandom != -1 || old.Overworld.BootsReplaceBoomerangs != -1 || old.Overworld.ProgressiveItems != -1 || old.Overworld.DeadWoodsHint != null)
                        fail("an older save should load the new fields as -1 / null");
                    setModel(0, 2, 0, 2, 1, 1);
                    OverworldData.DeadWoodsHint = "UD";
                    SaveAndLoad.ApplyLoadedOptions(old.Overworld);
                    if (readModel() != (1, 0, 1, 0, 1, 1)) fail($"an older save (ExtraBlocks 5) loaded wrong: {readModel()}");
                    if (OverworldData.DeadWoodsHint != "UD") fail("an older save should leave the hint letters alone");
                    // Mirrored Overworld, the custom LEVEL name (an empty name must come back empty, not as null), the notes
                    TrackerModel.MirrorOverworld = true;
                    TrackerModel.CustomLevelName = "ZORA";
                    var notes = "line one\n\"quoted\" and a \\ backslash\ttab\nlast line";
                    var d1 = DungeonSaveAndLoad.LoadAll(build(notes));
                    if (!d1.Overworld.MirrorOverworld) fail("save round trip: Mirrored Overworld (on) came back off");
                    if (d1.Items.CustomLevelName != "ZORA") fail($"save round trip: the custom LEVEL name came back as '{d1.Items.CustomLevelName}'");
                    if (d1.Notes != notes) fail("save round trip: the notes came back different");
                    TrackerModel.MirrorOverworld = false;
                    TrackerModel.CustomLevelName = "";
                    var d2 = DungeonSaveAndLoad.LoadAll(build(""));
                    if (d2.Overworld.MirrorOverworld) fail("save round trip: Mirrored Overworld (off) came back on");
                    if (d2.Items.CustomLevelName == null || d2.Items.CustomLevelName != "") fail("save round trip: an empty custom LEVEL name should come back empty, not missing");
                    // 4th Sword: its two slots (level 2 staircase / level 9 floor check) say whether it was on, and where
                    foreach (var (l2, l9) in new[] { (false, false), (true, false), (false, true) })
                    {
                        TrackerModel.level2StairBoxEnabled = l2;
                        TrackerModel.level9FloorBoxEnabled = l9;
                        var d3 = DungeonSaveAndLoad.LoadAll(build(""));
                        if (d3.Items.Level2StairBoxEnabled != l2 || d3.Items.Level9FloorBoxEnabled != l9 || SaveAndLoad.FourthSwordWasOn(d3.Items) != (l2 || l9))
                            fail($"save round trip: 4th Sword slots (level 2 {l2}, level 9 {l9}) came back wrong");
                    }
                    // dungeon 9's items: an item and its got-it state are written, and put back by the load
                    var wantCell = sBox9Cell == TrackerModel.ITEMS.BOW ? TrackerModel.ITEMS.RAFT : TrackerModel.ITEMS.BOW;
                    if (!box9.AttemptToSet(wantCell, TrackerModel.PlayerHas.YES)) fail("save round trip: could not put a test item in a dungeon 9 box");
                    else
                    {
                        var d4 = DungeonSaveAndLoad.LoadAll(build(""));
                        var b4 = d4.Items.Dungeons[8].Boxes[0];
                        if (b4.CellCurrent != box9.CellCurrent() || b4.PlayerHas != box9.PlayerHas().AsInt()) fail("save round trip: a dungeon 9 item box was not written to the save");
                        box9.AttemptToSet(TrackerModel.ITEMS.LADDER, TrackerModel.PlayerHas.NO);
                        if (!b4.TryApply(box9) || box9.CellCurrent() != wantCell || box9.PlayerHas().AsInt() != TrackerModel.PlayerHas.YES.AsInt())
                            fail("save round trip: a dungeon 9 item box did not come back");
                    }
                }
                finally
                {
                    OverworldData.ExtraPBBlocks = sPB; OverworldData.ExtraRaftBlocks = sRaft; OverworldData.DeadWoodsEnabled = sDW; OverworldData.LostHillsEnabled = sLH;
                    OverworldData.ExtraPBRandom = sPBR; OverworldData.ExtraRaftRandom = sRaftR; OverworldData.DeadWoodsRandom = sDWR; OverworldData.LostHillsRandom = sLHR;
                    OverworldData.DeadWoodsHint = sDWH; OverworldData.LostHillsHint = sLHH; OverworldData.DeadWoodsHintKnown = sDWK; OverworldData.LostHillsHintKnown = sLHK;
                    TrackerModel.BootsReplaceBoomerangs = sBoots; TrackerModel.BootsRandom = sBootsR; TrackerModel.ProgressiveItems = sProg;
                    TrackerModel.CustomLevelName = sName; TrackerModel.MirrorOverworld = sMirror;
                    TrackerModel.level2StairBoxEnabled = sL2; TrackerModel.level9FloorBoxEnabled = sL9;
                    box9.AttemptToSet(sBox9Cell, sBox9Has);
                }
            });
            log($"ZORA options save round trip done ({failures} failures so far)");
        }

        if (!stuck)
        {
            // 3a. every ordinary button on the main screen
            foreach (var b in buttons(win))
            {
                var text = label(b);
                if (!stuck && text != "" && !isAvoided(text))
                {
                    guard($"button '{text}'", () => click(b));
                    await pause(200);
                    // inside whatever opened: every checkbox through its states, then close it
                    var root = popupRoot();
                    if (popupCount() > 0 && root != null)
                        foreach (var cb in checkboxes(root))
                            if (!stuck && !isAvoided(label(cb))) await cycle($"'{text}' popup", cb);
                    await settle($"button '{text}'");
                }
            }
            log($"main screen buttons done ({failures} failures so far)");

            // 3b. a click, right click and wheel on whatever is under each point of a grid over the window
            var content = (Visual)win.Content;
            var w = (int)win.Bounds.Width; var h = (int)win.Bounds.Height;
            var step = 20;
            var n = 0;
            log($"grid starts: popupIsActive={CustomComboBoxes.GlobalFlag.popupIsActive} popups={popupCount()} window {w}x{h}");
            for (var y = step / 2; y < h; y += step)
            {
                for (var x = step / 2; x < w; x += step)
                {
                    if (stuck) continue;
                    Visual hit = null;
                    try { hit = content.GetVisualAt(new Point(x, y)); } catch { }
                    var target = hit?.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault();
                    if (target == null) continue;
                    n++;
                    var tname = target.GetType().Name;
                    var where = $"({x},{y}) {tname}";
                    hitsByType[tname] = hitsByType.GetValueOrDefault(tname) + 1;
                    async Task afterwards(string what)
                    {
                        if (popupCount() > 0)
                        {
                            popupsByType[tname] = popupsByType.GetValueOrDefault(tname) + 1;
                            await pause(80);
                            await settle(what);
                        }
                    }
                    // (three left clicks: an unmarked overworld tile first marks X, then DontCare, and only then opens its popup)
                    for (var k = 1; k <= 3; k++)
                        if (!stuck)
                        {
                            guard($"left click {k} {where}", () => raiseClick(target, MouseButton.Left));
                            await afterwards($"left click {k} {where}");
                        }
                    guard("right click " + where, () => raiseClick(target, MouseButton.Right));
                    await afterwards("right click " + where);
                    guard("wheel " + where, () => { raiseWheel(target, 120); raiseWheel(target, -120); });
                    await afterwards("wheel " + where);
                }
                await pause(1);   // (let the window breathe between rows)
            }
            // 3c. the overworld tile canvases themselves (48x33, one per tile): left click each 4 times, right click once
            var tiles = descendants(content).OfType<Canvas>().Where(c => c.Width == 48.0 && c.Height == 33.0 && c.IsHitTestVisible).ToList();
            log($"overworld tile canvases found: {tiles.Count} (popupIsActive={CustomComboBoxes.GlobalFlag.popupIsActive})");
            var tilePopups = 0;
            foreach (var t in tiles)
            {
                if (stuck) break;
                for (var k = 1; k <= 4; k++)
                    if (!stuck)
                    {
                        guard("tile left click", () => raiseClick(t, MouseButton.Left));
                        await pause(5);
                        if (popupCount() > 0) { tilePopups++; await pause(80); await settle("tile left click"); }
                    }
                guard("tile right click", () => raiseClick(t, MouseButton.Right));
                await pause(5);
                if (popupCount() > 0) { tilePopups++; await pause(80); await settle("tile right click"); }
            }
            log($"tile clicks done: {tilePopups} popups opened");
            log($"grid done: {n} points");
            log("grid targets (type: points, popups opened): " + string.Join(" | ", hitsByType.OrderByDescending(kv => kv.Value).Take(12).Select(kv => $"{kv.Key}: {kv.Value}, {popupsByType.GetValueOrDefault(kv.Key)}")));

            // 3d. Overworld + Extras: what the checkboxes actually do (not just that they can be clicked)
            var anyRoads = new[] { (TrackerModel.MapSquareChoiceDomainHelper.WARP_1, 10, 7), (TrackerModel.MapSquareChoiceDomainHelper.WARP_2, 14, 1),
                                   (TrackerModel.MapSquareChoiceDomainHelper.WARP_3, 4, 2), (TrackerModel.MapSquareChoiceDomainHelper.WARP_4, 10, 4) };
            var owExtras = stuck ? null : find("Overworld + Extras");
            if (owExtras != null)
            {
                guard("Overworld + Extras open", () => click(owExtras));
                await pause(400);
                var root = popupRoot();
                if (root == null) fail("Overworld + Extras: the window did not open");
                else
                {
                    CheckBox cbNamed(string text) => checkboxes(root).FirstOrDefault(c => label(c).StartsWith(text));
                    async Task clickTo(CheckBox cb, bool? target)
                    {
                        var tries = 0;
                        while (cb.IsChecked != target && tries < 4)
                        {
                            guard("Overworld + Extras checkbox", () => click(cb));
                            tries++;
                            await pause(50);
                        }
                    }
                    // mirror: the checkbox flips the map and the model, and back
                    var mirrorCb = cbNamed("Mirrored Overworld");
                    if (mirrorCb == null) fail("Overworld + Extras: no Mirrored Overworld checkbox");
                    else
                    {
                        var before = TrackerModel.MirrorOverworld;
                        guard("mirror click", () => click(mirrorCb));
                        await pause(300);
                        if (TrackerModel.MirrorOverworld == before) fail("Mirrored Overworld: clicking the box did not flip the map");
                        guard("mirror click back", () => click(mirrorCb));
                        await pause(300);
                        if (TrackerModel.MirrorOverworld != before) fail("Mirrored Overworld: clicking again did not flip it back");
                    }
                    // Extra PB blocks: Off removes the vanilla any roads, On brings them back, then the box shows ? (Random, counts as On)
                    var pbCb = cbNamed("Extra Power Bracelet Blocks");
                    if (pbCb == null) fail("Overworld + Extras: no Extra Power Bracelet Blocks checkbox");
                    else
                    {
                        await clickTo(pbCb, false);
                        if (OverworldData.ExtraPBBlocks || OverworldData.ExtraPBRandom) fail("Extra PB Off: the model is not Off");
                        foreach (var (state, col, row) in anyRoads)
                            if (TrackerModel.overworldMapMarks[col - 1, row].Current() == state) fail($"Extra PB Off: the any road at {col},{row} is still marked");
                        await clickTo(pbCb, true);
                        if (!OverworldData.ExtraPBBlocks || OverworldData.ExtraPBRandom) fail("Extra PB On: the model is not On");
                        foreach (var (state, col, row) in anyRoads)
                        {
                            var cur = TrackerModel.overworldMapMarks[col - 1, row].Current();
                            if (cur == -1 && !TrackerModel.owInstance.AlwaysEmpty(col - 1, row)) fail($"Extra PB On: the any road at {col},{row} was not brought back");
                        }
                        await clickTo(pbCb, null);
                        if (!OverworldData.ExtraPBBlocks || !OverworldData.ExtraPBRandom) fail("Extra PB ?: Random should be set and count as On");
                        // Random: the maybe-blocked outlines show until the bracelet is owned, then they are gone
                        var without = OverworldData.maybeBlockedTiles(false, false);
                        var withPB = OverworldData.maybeBlockedTiles(true, false);
                        if (!OverworldData.extraPBBlockTiles.All(t => without.Contains(t))) fail("Extra PB ?: the maybe-blocked tiles are missing");
                        if (OverworldData.extraPBBlockTiles.Any(t => withPB.Contains(t))) fail("Extra PB ?: the outlines stay after the bracelet is owned");
                        await clickTo(pbCb, false);
                    }
                    // the other three that can be Random: Random sets the flag and counts as On; Off clears both
                    foreach (var name in new[] { "Extra Raft Blocks", "Randomize Dead Woods", "Randomize Lost Hills" })
                    {
                        var cb = cbNamed(name);
                        if (cb == null) fail($"Overworld + Extras: no '{name}' checkbox");
                        else
                        {
                            await clickTo(cb, null);
                            if (cb.IsChecked.HasValue) fail($"{name}: could not reach the ? state");
                            if (OverworldData.maybeBlockedTiles(false, false).IsEmpty) fail($"{name} ?: no maybe-blocked tiles");
                            await clickTo(cb, false);
                        }
                    }
                    // Boots: Off, On, or ? (the flags left it to chance: counts as Off, the boomerangs stay, until it is confirmed)
                    var bootsCb = cbNamed("Pegasus Boots");
                    if (bootsCb == null) fail("Overworld + Extras: no Boots checkbox");
                    else if (!bootsCb.IsThreeState) fail("Pegasus Boots should have a ? (Random) state");
                    else
                    {
                        await clickTo(bootsCb, true);
                        if (!TrackerModel.BootsReplaceBoomerangs || TrackerModel.BootsRandom) fail("Boots On: the model is not On");
                        await clickTo(bootsCb, null);
                        if (TrackerModel.BootsReplaceBoomerangs || !TrackerModel.BootsRandom) fail("Boots ?: should be Random and count as Off until confirmed");
                        await clickTo(bootsCb, false);
                        if (TrackerModel.BootsReplaceBoomerangs || TrackerModel.BootsRandom) fail("Boots Off: the model is not Off");
                    }
                    if (!OverworldData.maybeBlockedTiles(false, false).IsEmpty) fail("everything is Off but tiles are still maybe-blocked");
                    // the Close button closes the window
                    var closeBtn = descendants(root).FirstOrDefault(d => isButtonLike(d) && label(d) == "Close") as Control;
                    if (closeBtn == null) fail("Overworld + Extras: no Close button");
                    else
                    {
                        guard("Overworld + Extras Close", () => click(closeBtn));
                        await pause(300);
                        if (popupCount() > 0) fail("Overworld + Extras: the Close button did not close the window");
                    }
                }
                await settle("Overworld + Extras checks");
            }
            log($"overworld + extras checks done ({failures} failures so far)");
        }

        // dungeon + shop checks: in-process, they call the same SetNewValue / helpers the clicks and hotkeys end up in (nothing is clicked, nothing is heard)
        guard("dungeon and shop checks", () =>
        {
            var rooms = DungeonUI.masterRoomStatesPublic;
            if (rooms == null) throw new Exception("the dungeon rooms were not built");
            TrackerModelOptions.ShowOffTileEdges.Value = true;
            void set(int x, int y, Z1R_Tracker.Models.Z1R_TrackerInterop.RoomType rt)
            {
                var f = DungeonUI.setNewValueFunctions[0][x, y];
                if (f == null) throw new Exception($"no SetNewValue for dungeon 1 room ({x},{y})");
                var r = rooms[0][x, y].Clone();
                r.RoomType = rt;
                f(r);
            }
            Z1R_Tracker.Models.Z1R_TrackerInterop.RoomType typeAt(int x, int y) => rooms[0][x, y].RoomType;
            Dungeon.Door hDoor(int x, int y) => DungeonUI.horizontalDoorsPerLevel[0][x, y];
            Dungeon.Door vDoor(int x, int y) => DungeonUI.verticalDoorsPerLevel[0][x, y];
            var marked = Z1R_Tracker.Models.Z1R_TrackerInterop.RoomType.StartEnterFromS;
            var off = Z1R_Tracker.Models.Z1R_TrackerInterop.RoomType.OffTheMap;
            var unmarked = Z1R_Tracker.Models.Z1R_TrackerInterop.RoomType.Unmarked;
            // 1. a room set OFF removes a hand-placed 'no' wall between it and a marked neighbour
            set(3, 3, marked);
            hDoor(3, 3).State = Dungeon.DoorState.NO;
            set(4, 3, off);
            if (hDoor(3, 3).State != Dungeon.DoorState.UNKNOWN) fail("OFF room: the 'no' wall toward the marked neighbour was not removed");
            // 2. a third copy of a transport is refused and leaves the room untouched; after unmarking one, a third fits
            var t1 = Z1R_Tracker.Models.Z1R_TrackerInterop.RoomType.Transport1;
            set(0, 6, t1);
            set(2, 6, t1);
            set(4, 6, t1);
            if (typeAt(4, 6) != unmarked) fail("transport: a third copy was placed");
            set(0, 6, unmarked);
            set(4, 6, t1);
            if (typeAt(4, 6) != t1) fail("transport: after unmarking one copy, the third could not be placed");
            // 3. an Old Man (hint) room puts an unbombable wall on its top side, and taking the room away takes the wall back
            set(6, 5, Z1R_Tracker.Models.Z1R_TrackerInterop.RoomType.OldManHint);
            if (vDoor(6, 4).State != Dungeon.DoorState.NO) fail("old man room: no top wall was placed");
            set(6, 5, unmarked);
            if (vDoor(6, 4).State != Dungeon.DoorState.UNKNOWN) fail("old man room: the top wall stayed after the room was removed");
            // 4. shops: a numbered shop label goes away with the tile's icons; only secrets / door repair / money making game replace a shop
            if (!WPFUI.tileTypeReplacesShop(TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET)) fail("shop: a secret should replace a shop");
            if (WPFUI.tileTypeReplacesShop(TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER)) fail("shop: the letter cave should not replace a shop");
            WPFUI.setShopLabel(2, 2, 1);
            if (TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[2, 2]) != 1) fail("shop: the SHOP 1 label was not set");
            WPFUI.clearAllTileIcons(2, 2);
            if (TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[2, 2]) != 0) fail("shop: clearing the tile left the shop label");
            // put everything back
            foreach (var (x, y) in new[] { (3, 3), (4, 3), (0, 6), (2, 6), (4, 6), (6, 5) }) { try { set(x, y, unmarked); } catch { } }
            hDoor(3, 3).State = Dungeon.DoorState.UNKNOWN;
        });
        log($"dungeon and shop checks done ({failures} failures so far)");

        // 5. equations19's crash: a box taken out of the tree (its cell was rebuilt under the pointer) gets PointerEntered.
        // The hover highlight used to throw on the null position; it must now just hide.
        guard("hover on a box that left the tree", () =>
        {
            // (the highlight is already attached to the main canvas at startup)
            var orphan = new Border { Width = 34, Height = 34 };   // never added to the window: no common ancestor with the main canvas
            Views.appMainCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(orphan);
            orphan.RaiseEvent(new PointerEventArgs(InputElement.PointerEnteredEvent, orphan, mouse, orphan, new Point(), (ulong)Environment.TickCount,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None));
        });
        // 5b. the same with the real boxes: take each small canvas on the main screen out of its parent, send it PointerEntered / PointerExited
        // (every hover handler runs on a control that left the tree), then put it back. This is what hit equations19 a second time (MakeBoxItemWithExtraDecorations).
        guard("hover on real boxes that left the tree", () =>
        {
            var tested = 0;
            foreach (var c in descendants(win).OfType<Canvas>().Where(x => x.Bounds.Width >= 20 && x.Bounds.Width <= 80 && x.Bounds.Height >= 20 && x.Bounds.Height <= 80).ToList())
            {
                if (!(c.Parent is Panel p)) continue;
                var idx = p.Children.IndexOf(c);
                if (idx < 0) continue;
                p.Children.RemoveAt(idx);
                try
                {
                    foreach (var ev in new[] { InputElement.PointerEnteredEvent, InputElement.PointerExitedEvent })
                        c.RaiseEvent(new PointerEventArgs(ev, c, mouse, c, new Point(), (ulong)Environment.TickCount, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None));
                    tested++;
                }
                finally { p.Children.Insert(idx, c); }
            }
            log($"hover on real boxes that left the tree: {tested} boxes tried");
            if (tested < 20) fail($"hover on real boxes: only {tested} boxes found to try");
        });
        log($"hover on a removed box checked ({failures} failures so far)");

        // 6. co-op logic, in-process (no network, no second tracker): the pieces whose bugs only showed up between two players
        guard("co-op checks", () =>
        {
            // 6a. a message identical to the one just before it is a duplicate; a state that comes back (A, B, A) is a real change and must get through
            var seq = new[] { "A", "B", "A", "A" };
            var expect = new[] { false, false, false, true };
            for (int i = 0; i < seq.Length; i++)
                if (CoopSync.alreadyReceived("SelfTestRepeat", seq[i]) != expect[i])
                    fail($"co-op: message {i + 1} of A,B,A,A was judged {(expect[i] ? "new" : "a duplicate")} wrongly (the third, A again after B, must be applied)");
            // 6b. repainting a door must not send anything to the partner; changing one sends exactly one message
            var sent = 0;
            Action<DungeonUI.DoorChangeInfo> count = _ => sent++;
            DungeonUI.doorChangedEvent += count;
            try
            {
                var doors = DungeonUI.horizontalDoorsPerLevel[0];
                for (int x = 0; x < 7; x++) for (int y = 0; y < 8; y++) doors[x, y].Redraw();
                if (sent != 0) fail($"co-op: redrawing 56 unchanged doors sent {sent} door messages (should be 0)");
                var before = doors[5, 5].State;
                sent = 0;
                doors[5, 5].State = before == Dungeon.DoorState.YES ? Dungeon.DoorState.NO : Dungeon.DoorState.YES;
                if (sent != 1) fail($"co-op: changing one door sent {sent} door messages (should be exactly 1)");
                doors[5, 5].State = before;
            }
            finally { DungeonUI.doorChangedEvent -= count; }
            // 6c. a burst of different updates trips the anti-spam mute, and the update that trips it is dropped (the caller then has to schedule a retry)
            CoopSync.autoMutedUntil = DateTime.MinValue;
            var results = Enumerable.Range(0, CoopSync.syncBurstLimit + 1).Select(i => CoopSync.shouldSendUpdate("SelfTestBurst", "payload " + i)).ToList();
            if (!results.Contains(false)) fail("co-op: a burst of updates never tripped the auto-mute");
            if (CoopSync.autoMutedUntil <= DateTime.UtcNow) fail("co-op: the auto-mute was not set after a burst");
            CoopSync.autoMutedUntil = DateTime.MinValue;
            // 6d. Race Mode forces mute on and turning it off puts the user's own choice back (here: muted before, so still muted)
            var sMuted = TrackerModelOptions.IsMuted; var sRace = TrackerModelOptions.RaceMode.Value;
            try
            {
                // (the button clicks above can leave Race Mode on: start from off, as a user would before choosing to mute)
                TrackerModelOptions.RaceMode.Value = false; OptionsMenu.triggerRaceModeChanged();
                TrackerModelOptions.IsMuted = true;
                TrackerModelOptions.RaceMode.Value = true; OptionsMenu.triggerRaceModeChanged();
                TrackerModelOptions.RaceMode.Value = false; OptionsMenu.triggerRaceModeChanged();
                if (!TrackerModelOptions.IsMuted) fail("co-op/options: Race Mode off unmuted a user who had muted before turning it on");
            }
            finally
            {
                TrackerModelOptions.RaceMode.Value = sRace; OptionsMenu.triggerRaceModeChanged();
                TrackerModelOptions.IsMuted = sMuted;
            }
        });
        log($"co-op checks done ({failures} failures so far)");

        // 6f. both players change the same room within 5 s: the later change wins on both sides (they used to ignore each other and stay different)
        guard("co-op conflict rule", () =>
        {
            var key = "SelfTestConflict";
            CoopSync.recordSend(key);
            var later = DateTime.UtcNow.Ticks + TimeSpan.TicksPerSecond;
            var earlier = DateTime.UtcNow.Ticks - 2 * TimeSpan.TicksPerSecond;
            if (CoopSync.localEditWins(key, 5000, later, "partner")) fail("co-op: a partner's later change to the same room was ignored");
            if (!CoopSync.localEditWins(key, 5000, earlier, "partner")) fail("co-op: a partner's earlier change to the same room overwrote ours");
            if (CoopSync.localEditWins("SelfTestNeverSent", 5000, earlier, "partner")) fail("co-op: a partner's change was ignored though we never changed that room");
        });
        // 6g. duplicate links go into the save
        guard("duplicate links in the save", () =>
        {
            var had = WPFUI.dupLinks.ContainsKey((15, 7));
            WPFUI.linkDuplicate((15, 7), (14, 6));
            try
            {
                var text = SaveAndLoad.DupLinksText?.Invoke() ?? "";
                if (!text.Split(';').Contains("15,7,14,6")) fail($"save: duplicate link 15,7 -> 14,6 missing from the save text '{text}'");
            }
            finally { if (!had) WPFUI.dupLinks.Remove((15, 7)); }
        });

        // 6e. a DungeonMaps update dropped by the anti-spam mute must be sent again once the mute ends (no network: the send hook catches it)
        {
            var maps = new List<string>();
            var savedLast = CoopSync.lastSentDungeonMapsPayload;
            SyncManager.TestSendHook = (t, p, s) => { if (t == "DungeonMaps") lock (maps) maps.Add(p); };
            CoopSync.testForceCoop = true;
            try
            {
                CoopSync.autoMutedUntil = DateTime.MinValue;
                CoopSync.lastSentDungeonMapsPayload = "";
                for (int i = 0; i <= CoopSync.syncBurstLimit; i++) CoopSync.shouldSendUpdate("SelfTestBurst2", "payload " + i);   // trips the mute
                if (CoopSync.autoMutedUntil <= DateTime.UtcNow) fail("co-op: could not trip the auto-mute for the retry test");
                CoopSync.sendDungeonMapsUpdate("selftest");
                await pause(600);   // (200 ms debounce plus the send task)
                int whileMuted; lock (maps) whileMuted = maps.Count;
                if (whileMuted != 0) fail($"co-op: a DungeonMaps update was sent {whileMuted} time(s) while muted");
                await pause(2200);   // (mute 1000 ms, retry after +100 ms, then the debounce again)
                int afterwards; lock (maps) afterwards = maps.Count;
                if (afterwards != 1) fail($"co-op: the DungeonMaps update dropped by the mute was sent {afterwards} time(s) afterwards (should be exactly 1)");
            }
            finally
            {
                SyncManager.TestSendHook = null;
                CoopSync.testForceCoop = false;
                CoopSync.autoMutedUntil = DateTime.MinValue;
                CoopSync.lastSentDungeonMapsPayload = savedLast;
            }
        }
        log($"co-op retry check done ({failures} failures so far)");

        closeExtraWindows();
        log($"{clicks} button/checkbox clicks, {popupsSeen} popups opened and closed");
        log(failures == 0 ? "RESULT: PASS" : $"RESULT: FAIL ({failures})");
        (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(failures == 0 ? 0 : 1);
    }

    public static void arm()
    {
        try { File.Delete(reportPath); } catch { }
        // any window the test opens (popouts and so on) goes off the screen and out of the taskbar as soon as it loads
        Control.LoadedEvent.AddClassHandler<Window>((w, _) =>
        {
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Position = new PixelPoint(-4000, 0);
            w.ShowInTaskbar = false;
        });
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
                w.WindowStartupLocation = WindowStartupLocation.Manual;
                w.Position = new PixelPoint(-4000, 0);   // off the screen, so it does not get in the way
                w.ShowInTaskbar = false;
                run(w).ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        log("FAIL: the test itself broke: " + task.Exception);
                        Dispatcher.UIThread.Post(() => (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(2));
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        };
        t.Start();
    }
}
