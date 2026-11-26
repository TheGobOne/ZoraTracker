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
    private const string fourthSwordOnlyZora = "22.4a";   // (version 22, only add_l4_sword = 1: 1 * 2*2*6*2*2*3 = 288 = "4a" in base 63)
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
            // an all-zero version 22 string must be accepted (the current version, 23, is checked below)
            var (z22, err22) = FlagPaste.decodeZora("22.0");
            if (z22 == null) fail("ZORA flags (version 22): " + err22);
            // the version is plain digits with no leading zero (the spec's ^([1-9][0-9]*)\.): a sign, a space or a leading zero is refused
            foreach (var bad in new[] { "+18.0", "18 .0", "018.0", "0.0", "-1.0" })
                if (FlagPaste.decodeZora(bad).ok != null) fail($"ZORA flags '{bad}' were accepted (the version must be digits with no leading zero)");
            // a newer version is read anyway, with a warning (and its extra digits are not an error: they are fields this tracker does not know)
            var (z24, err24) = FlagPaste.decodeZora("24.1");
            if (z24 == null || z24.Warning == null) fail("ZORA flags (version 24, newer than the tracker) were refused or gave no warning: " + err24);
            // seed file names, every way they can arrive
            {
                var full = "zora-2.0.1-91250lSP91JdxpR4V55maWlQu8UJ3A-20.7KrCiA5uIm16-2011";
                foreach (var name in new[] { full + ".nes", full + ".spoiler.txt", full + ".tracker.json", full, full + "-music.nes", "\"C:\\Users\\x\\Downloads\\" + full + ".nes\"", full + " (1).tracker.json", full + "(1).nes", full + ".NES" })
                {
                    var f = FlagPaste.parseFileName(name);
                    if (f == null) { fail($"file name '{name}' was not read"); continue; }
                    if (f.Version != "2.0.1" || f.Z1R != "91250lSP91JdxpR4V55maWlQu8UJ3A" || f.Zora != "20.7KrCiA5uIm16" || f.Seed != "2011")
                        fail($"file name '{name}' read wrong: {f.Version} {f.Z1R} {f.Zora} {f.Seed}");
                }
                var empty = FlagPaste.parseFileName("zora-2.0.1-8hq4BeR1JXo89BJ2_TFpTP02u8UJ3A--77.nes");
                if (empty == null || empty.Zora != "" || empty.Seed != "77") fail("file name with an empty ZORA string was not read as empty");
                var test = FlagPaste.parseFileName("zora-2.0.1-a1b2c3d-test-8hq4BeR1JXo89BJ2-18.1-5-music");
                if (test == null || test.Version != "2.0.1-a1b2c3d-test" || !test.Music || test.Z1R != "8hq4BeR1JXo89BJ2" || test.Zora != "18.1" || test.Seed != "5") fail("test-channel file name read wrong");
                if (FlagPaste.parseFileName("zora-seed-123.nes") != null) fail("an old-style file name was read as flags");
                if (FlagPaste.parseFileName("AP_123_P1_Link.nes") != null) fail("an Archipelago file name was read as flags");
                if (FlagPaste.newerReleaseWarning("2.0.1") != null || FlagPaste.newerReleaseWarning("2.1.0") == null) fail("newer-release warning wrong");
            }
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
                // version 23 (test channel flags/latest.json): add_triforces_to_pool 3, add_maps_compasses_to_pool 3, bomb_upgrades_in_pool 2
                var r23 = specRadixes.Concat(new[] { 3, 3, 2 }).ToArray();
                var d23 = new int[r23.Length];
                d23[46] = 1;   // add_triforces_to_pool On
                d23[47] = 2;   // add_maps_compasses_to_pool Random
                d23[48] = 1;   // bomb_upgrades_in_pool On
                var n23 = System.Numerics.BigInteger.Zero;
                for (var k = r23.Length - 1; k >= 0; k--) n23 = n23 * r23[k] + d23[k];
                var p23 = "";
                while (n23 > 0) { p23 = alphabet63[(int)(n23 % 63)] + p23; n23 /= 63; }
                var (z23, err23) = FlagPaste.decodeZora("23." + p23);
                if (z23 == null || z23.Warning != null) fail($"ZORA flags (version 23, '23.{p23}') were refused or gave a warning: {err23}");
                if (FlagPaste.decodeZora("22." + p23).error == null) fail($"ZORA flags: '22.{p23}' (version 23 fields set) was accepted as version 22");
            }
            // the string the Paste Flags check below uses for "only the 4th sword": add_l4_sword On (in level 9), everything else Off, Progressive Items included
            var (z4, err4) = FlagPaste.decodeZora(fourthSwordOnlyZora);
            if (z4 == null) fail($"ZORA flags '{fourthSwordOnlyZora}': {err4}");
            else if (z4.Progressive || z4.FourthSword != FlagPaste.Tri.On || z4.FourthInLevel2 || z4.BootsReplaceBoomerangs != FlagPaste.Tri.Off)
                fail($"ZORA flags '{fourthSwordOnlyZora}' should be the 4th sword in level 9 and nothing else");
            // ZORA's documented example (All Swords No Boards): Level 9 Entrance = Level 4 sword is read
            var (zs, errS) = FlagPaste.decodeZora("4.T9Jvg");
            if (zs == null || !zs.Level9SwordEntrance || z4?.Level9SwordEntrance != false) fail($"ZORA flags '4.T9Jvg': the level 4 sword entrance was not read ({errS})");
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
        // the start screen's 4th Sword / Progressive Items rule, through the real checkboxes: 4th Sword checks Progressive Items and holds it on; Progressive Items never checks 4th Sword
        var startProgressive = checkboxes(win).FirstOrDefault(c => label(c) == "Progressive Items");
        var startFourth = checkboxes(win).FirstOrDefault(c => label(c) == "4th Sword");
        var startLegacy = checkboxes(win).FirstOrDefault(c => label(c) == "Legacy Mode");
        var startBoots = checkboxes(win).FirstOrDefault(c => label(c) == "Pegasus Boots");
        var startLevel2 = checkboxes(win).FirstOrDefault(c => label(c) == "Level 2");
        var startLevel9 = checkboxes(win).FirstOrDefault(c => label(c) == "Level 9");
        if (startProgressive == null || startFourth == null || startLegacy == null || startBoots == null || startLevel2 == null || startLevel9 == null)
            fail("start screen: the Progressive Items / 4th Sword / Legacy Mode / Pegasus Boots / Level 2 / Level 9 checkboxes were not all found");
        else if (!stuck)
        {
            var sLegacy = startLegacy.IsChecked;
            guard("start screen 4th Sword rule", () =>
            {
                startLegacy.IsChecked = false;   // (Legacy Mode locks the Extras off: the rule is only checked with it off)
                startFourth.IsChecked = false;
                startProgressive.IsChecked = false;
                click(startProgressive);
                if (startProgressive.IsChecked != true) fail("start screen: clicking Progressive Items did not check it");
                if (startFourth.IsChecked == true) fail("start screen: checking Progressive Items checked 4th Sword");
                startProgressive.IsChecked = false;
                click(startFourth);
                if (startFourth.IsChecked != true) fail("start screen: clicking 4th Sword did not check it");
                if (startProgressive.IsChecked != true) fail("start screen: checking 4th Sword did not check Progressive Items");
                click(startProgressive);
                if (startProgressive.IsChecked != true) fail("start screen: Progressive Items could be unchecked while 4th Sword is checked");
                startFourth.IsChecked = false;
                startProgressive.IsChecked = false;
                startLegacy.IsChecked = sLegacy;
            });
            await settle("start screen 4th Sword rule");
        }
        // Paste Flags: type both strings, Apply, expect it to close and the options to be set (a bad string keeps it open)
        var pasteButton = find("Paste Flags");
        async Task pasteFlags(string z1r, string zora)
        {
            guard("Paste Flags click", () => click(pasteButton));
            await pause(400);
            var root = popupRoot();
            if (root == null) { fail("Paste Flags: the box did not open"); return; }
            var boxes = descendants(root).OfType<TextBox>().Where(t => !t.IsReadOnly).ToList();
            var apply = descendants(root).FirstOrDefault(d => isButtonLike(d) && label(d) == "Apply") as Control;
            if (boxes.Count < 2) fail("Paste Flags: the two text boxes were not found");
            else if (apply == null) fail("Paste Flags: no Apply button");
            else
            {
                boxes[0].Text = z1r;
                boxes[1].Text = zora;
                guard("Paste Flags Apply", () => click(apply));
                await pause(400);
                // (the flags are valid, so Apply closes the box)
                if (popupCount() > 0)
                {
                    fail($"Paste Flags ('{zora}'): Apply did not close the box for valid flags");
                    var leftover = descendants(popupRoot()).FirstOrDefault(d => isButtonLike(d) && (label(d) == "Done" || label(d) == "Close")) as Control;
                    if (leftover != null) { guard("Paste Flags Close", () => click(leftover)); await pause(400); }
                }
            }
        }
        if (pasteButton != null && !stuck && startProgressive != null && startFourth != null && startBoots != null && startLevel2 != null && startLevel9 != null)
        {
            // only the 4th sword set (Progressive Items 0 in the string): Progressive Items must still end up checked, by the 4th Sword rule
            startFourth.IsChecked = false;
            startProgressive.IsChecked = false;
            await pasteFlags("", fourthSwordOnlyZora);
            if (startFourth.IsChecked != true || startLevel9.IsChecked != true || startLevel2.IsChecked == true) fail($"Paste Flags ('{fourthSwordOnlyZora}'): 4th Sword in Level 9 was not checked");
            if (startProgressive.IsChecked != true) fail($"Paste Flags ('{fourthSwordOnlyZora}'): the 4th sword alone did not check Progressive Items");
            await settle("Paste Flags (4th sword only)");
            // both strings (progressive_items 1, the 4th sword in level 2, Boots Random); this one goes last, so the run below starts with the same options as before
            if (!stuck)
            {
                await pasteFlags("GmCbTYod8E8ige6igkDR1ZyxsOpshPXUUTcU5", "10.Km67jtU");
                if (startProgressive.IsChecked != true) fail("Paste Flags: Progressive Items was not checked");
                if (startFourth.IsChecked != true || startLevel2.IsChecked != true || startLevel9.IsChecked == true) fail("Paste Flags: 4th Sword in Level 2 was not checked");
                if (startBoots.IsChecked != null) fail($"Paste Flags: Pegasus Boots (Random in the flags) should be the third state (Off until confirmed), not {startBoots.IsChecked}");
                await settle("Paste Flags");
            }
            // a bad ZORA string: Apply must leave the box open with the error showing
            if (!stuck)
            {
                guard("Paste Flags click (bad flags)", () => click(pasteButton));
                await pause(400);
                var badRoot = popupRoot();
                var badBoxes = badRoot == null ? new List<TextBox>() : descendants(badRoot).OfType<TextBox>().Where(t => !t.IsReadOnly).ToList();
                var badApply = badRoot == null ? null : descendants(badRoot).FirstOrDefault(d => isButtonLike(d) && label(d) == "Apply") as Control;
                if (badBoxes.Count < 2 || badApply == null) fail("Paste Flags (bad flags): the box did not open");
                else
                {
                    badBoxes[1].Text = "not-a-flag-string";
                    guard("Paste Flags Apply (bad flags)", () => click(badApply));
                    await pause(400);
                    if (popupCount() == 0) fail("Paste Flags: Apply closed the box for a bad flag string");
                    else
                    {
                        var closeIt = descendants(popupRoot()).FirstOrDefault(d => isButtonLike(d) && (label(d) == "Done" || label(d) == "Close")) as Control;
                        if (closeIt != null) { guard("Paste Flags Close (bad flags)", () => click(closeIt)); await pause(400); }
                        if (popupCount() > 0) fail("Paste Flags: could not close the box after a bad flag string");
                    }
                }
            }
        }
        else if (pasteButton == null) fail("Paste Flags: no Paste Flags button");
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
            // the save fields added since v2.2.0-beta-23: each is set to a value that is not the default, saved, and read back (or put back the way a real load does)
            guard("newer save fields round trip", () =>
            {
                var pp = TrackerModel.playerProgressAndTakeAnyHearts; var se = TrackerModel.startingItemsAndExtras;
                var sPrices = (int[,])TrackerModel.overworldShopPrices.Clone();
                var sTri = TrackerModel.level9TriforceCount; var sTier = TrackerModel.level9TriggerSwordTier;
                var trig = TrackerModel.level9TriggerBox; var sTrigCell = trig.CellCurrent(); var sTrigHas = trig.PlayerHas();
                var sTaken = OverworldMapTileCustomization.takeAnyTaken.ToList();
                var sCandle = (bool[])OverworldMapTileCustomization.takeAnySlotGaveCandle.Clone();
                var sHearts = Enumerable.Range(0, 4).Select(k => (pp.GetTakeAnyHeart(k), pp.GetTakeAnyKind(k))).ToArray();
                var sBlueCandle = pp.PlayerHasBlueCandle.Value();
                var sTileTiers = TrackerModel.progressiveTileTier.ToList(); var sBoxTiers = TrackerModel.progressiveBoxTier.ToList();
                var sHint = TrackerModel.hintShopColors.ToList();
                var sSword4 = se.PlayerHasSword4.Value(); var sMags = se.PlayerHasMagicalSword.Value();
                var sZf = SaveAndLoad.lastKnownZoraFlags; var sZv = SaveAndLoad.lastKnownZoraVersion;
                string build() => string.Join("\n", SaveAndLoad.BuildSaveLines("", 0, new[] { "}" }, new string[0], "", false, 0, false, false));
                try
                {
                    var sGuesses = SaveAndLoad.IntraHeartGuessesText;
                    SaveAndLoad.IntraHeartGuessesText = () => "d0.0;x1";
                    var guessesRead = DungeonSaveAndLoad.LoadAll(build()).Items.IntraHeartGuesses;
                    SaveAndLoad.IntraHeartGuessesText = sGuesses;
                    if (guessesRead != "d0.0;x1") fail($"newer save fields: the guessed hearts were saved as '{guessesRead}'");
                    TrackerModel.setShopPrice(3, 4, 160);
                    TrackerModel.level9TriforceCount = 5;
                    TrackerModel.level9TriggerSwordTier = 3;
                    var trigCell = sTrigCell == TrackerModel.ITEMS.BOW ? TrackerModel.ITEMS.RAFT : TrackerModel.ITEMS.BOW;
                    if (!trig.AttemptToSet(trigCell, TrackerModel.PlayerHas.SKIPPED)) fail("newer save fields: could not set the level 9 entry item");
                    OverworldMapTileCustomization.takeAnyTaken.Clear();
                    OverworldMapTileCustomization.takeAnyTaken[(5, 6)] = (2, true, 2);
                    for (var k = 0; k < 4; k++) OverworldMapTileCustomization.takeAnySlotGaveCandle[k] = k == 1 || k == 3;
                    // (slots 1 and 3 hold the candle they gave, the way cycling them to the candle on the main screen leaves them)
                    pp.SetTakeAnyKind(0, 0); pp.SetTakeAnyKind(1, 2); pp.SetTakeAnyKind(3, 2);
                    pp.PlayerHasBlueCandle.Set(true);
                    TrackerModel.progressiveTileTier[Tuple.Create(7, 2, TrackerModel.ITEMS.BLUECANDLE)] = 2;
                    TrackerModel.progressiveBoxTier[TrackerModel.sword2Box] = 3;
                    TrackerModel.hintShopColors[Tuple.Create(9, 1)] = 4;
                    se.PlayerHasSword4.Set(!sSword4); se.PlayerHasMagicalSword.Set(!sMags);
                    SaveAndLoad.lastKnownZoraFlags = "22.7KrCiA"; SaveAndLoad.lastKnownZoraVersion = "2.0.1";
                    var d = DungeonSaveAndLoad.LoadAll(build());
                    var it = d.Items;
                    if (it.ShopPrices == null || !it.ShopPrices.Split(';').Contains("3,4,160")) fail($"save round trip: ShopPrices came back as '{it.ShopPrices}'");
                    if (it.Level9TriforceCount != 5) fail($"save round trip: Level9TriforceCount 5 came back as {it.Level9TriforceCount}");
                    if (it.Level9TriggerSwordTier != 3) fail($"save round trip: Level9TriggerSwordTier 3 came back as {it.Level9TriggerSwordTier}");
                    if (it.Level9TriggerBox == null || it.Level9TriggerBox.CellCurrent != trigCell) fail("save round trip: Level9TriggerBox did not come back");
                    if (it.TakeAnyTaken == null || !it.TakeAnyTaken.Split(';').Contains("5,6,2,1,2")) fail($"save round trip: TakeAnyTaken came back as '{it.TakeAnyTaken}'");
                    if (it.TakeAnySlotGaveCandle != "0101") fail($"save round trip: TakeAnySlotGaveCandle 0101 came back as '{it.TakeAnySlotGaveCandle}'");
                    if (it.ProgressiveTileTiers == null || !it.ProgressiveTileTiers.Split(';').Contains($"7,2,{TrackerModel.ITEMS.BLUECANDLE},2")) fail($"save round trip: ProgressiveTileTiers came back as '{it.ProgressiveTileTiers}'");
                    if (it.ProgressiveBoxTiers == null || !it.ProgressiveBoxTiers.Split(';').Contains("Sword2,3")) fail($"save round trip: ProgressiveBoxTiers came back as '{it.ProgressiveBoxTiers}'");
                    if (it.HintShopColors == null || !it.HintShopColors.Split(';').Contains("9,1,4")) fail($"save round trip: HintShopColors came back as '{it.HintShopColors}'");
                    if (d.StartingItemsAndExtras.PlayerHasSword4 != !sSword4 || d.StartingItemsAndExtras.PlayerHasMagicalSword != !sMags) fail("save round trip: the starting 4th sword / magical sword did not come back");
                    if (d.ZoraFlags != "22.7KrCiA" || d.ZoraVersion != "2.0.1") fail($"save round trip: ZORA flags / version came back as '{d.ZoraFlags}' / '{d.ZoraVersion}'");
                    // forget them, then put them back the way a real load does
                    TrackerModel.setShopPrice(3, 4, 0); TrackerModel.level9TriforceCount = 8; TrackerModel.level9TriggerSwordTier = 1;
                    trig.AttemptToSet(-1, TrackerModel.PlayerHas.SKIPPED);
                    OverworldMapTileCustomization.takeAnyTaken.Clear();
                    Array.Clear(OverworldMapTileCustomization.takeAnySlotGaveCandle, 0, 4);
                    for (var k = 0; k < 4; k++) pp.SetTakeAnyKind(k, 0);   // (the slot kinds are not saved: a fresh tracker starts with none)
                    pp.PlayerHasBlueCandle.Set(false);
                    se.PlayerHasSword4.Set(sSword4); se.PlayerHasMagicalSword.Set(sMags);
                    OverworldItemGridUI.ApplyLoadedItemExtrasAndProgress(d);   // (the real load's step: item extras, then player progress, then starting items)
                    // a main screen slot that gave the candle comes back as the candle (not a crossed-out heart), and cycling it away takes the candle back
                    if (pp.GetTakeAnyKind(1) != 2 || pp.GetTakeAnyKind(3) != 2) fail($"save load: the take any slots that gave the candle came back as kinds {pp.GetTakeAnyKind(1)} / {pp.GetTakeAnyKind(3)}, not 2 (the candle)");
                    if (!pp.PlayerHasBlueCandle.Value()) fail("save load: the blue candle given by a take any slot was not put back");
                    OverworldMapTileCustomization.setTakeAnySlotKind(1, 1);
                    if (pp.PlayerHasBlueCandle.Value()) fail("save load: cycling a reloaded take any slot away from the candle did not take the candle back");
                    if (OverworldMapTileCustomization.takeAnySlotGaveCandle[1]) fail("save load: a slot cycled away from the candle still says it gave the candle");
                    if (SaveAndLoad.TakeAnySlotGaveCandleText.Invoke() != "0001") fail($"save load: the next save would write TakeAnySlotGaveCandle '{SaveAndLoad.TakeAnySlotGaveCandleText.Invoke()}', not 0001");
                    OverworldMapTileCustomization.takeAnySlotGaveCandle[1] = true;   // (back to what the save said, for the check below)
                    if (TrackerModel.overworldShopPrices[3, 4] != 160 || TrackerModel.level9TriforceCount != 5 || TrackerModel.level9TriggerSwordTier != 3 || trig.CellCurrent() != trigCell)
                        fail("save load: the shop price / level 9 entry rule was not put back");
                    if (!OverworldMapTileCustomization.takeAnyTaken.TryGetValue((5, 6), out var tk) || tk != (2, true, 2)) fail("save load: the taken take any cave was not put back");
                    if (!OverworldMapTileCustomization.takeAnySlotGaveCandle.SequenceEqual(new[] { false, true, false, true })) fail("save load: which take any slots gave the candle was not put back");
                    if (se.PlayerHasSword4.Value() != !sSword4 || se.PlayerHasMagicalSword.Value() != !sMags) fail("save load: the starting 4th sword / magical sword was not put back");
                    // a beta-23 save has no TakeAnySlotGaveCandle: no slot gave the candle
                    var old = DungeonSaveAndLoad.LoadAll(build().Replace("\"TakeAnySlotGaveCandle\": \"0101\", ", ""));
                    if (old.Items.TakeAnySlotGaveCandle != null) fail("a save without TakeAnySlotGaveCandle should load it as null");
                    OverworldMapTileCustomization.applyLoadedItemExtras(old.Items);
                    if (OverworldMapTileCustomization.takeAnySlotGaveCandle.Any(b => b)) fail("a save without TakeAnySlotGaveCandle should leave every slot as not having given the candle");
                }
                finally
                {
                    for (var i = 0; i <= 15; i++) for (var j = 0; j <= 7; j++) TrackerModel.overworldShopPrices[i, j] = sPrices[i, j];
                    TrackerModel.level9TriforceCount = sTri; TrackerModel.level9TriggerSwordTier = sTier;
                    trig.AttemptToSet(sTrigCell, sTrigHas);
                    OverworldMapTileCustomization.takeAnyTaken.Clear();
                    foreach (var kv in sTaken) OverworldMapTileCustomization.takeAnyTaken[kv.Key] = kv.Value;
                    Array.Copy(sCandle, OverworldMapTileCustomization.takeAnySlotGaveCandle, 4);
                    for (var k = 0; k < 4; k++) pp.RestoreTakeAnySlot(k, sHearts[k].Item1, sHearts[k].Item2);   // (heart value and kind exactly: SetTakeAnyKind would work the heart value out again)
                    pp.PlayerHasBlueCandle.Set(sBlueCandle);
                    TrackerModel.progressiveTileTier.Clear(); foreach (var kv in sTileTiers) TrackerModel.progressiveTileTier[kv.Key] = kv.Value;
                    TrackerModel.progressiveBoxTier.Clear(); foreach (var kv in sBoxTiers) TrackerModel.progressiveBoxTier[kv.Key] = kv.Value;
                    TrackerModel.hintShopColors.Clear(); foreach (var kv in sHint) TrackerModel.hintShopColors[kv.Key] = kv.Value;
                    se.PlayerHasSword4.Set(sSword4); se.PlayerHasMagicalSword.Set(sMags);
                    SaveAndLoad.lastKnownZoraFlags = sZf; SaveAndLoad.lastKnownZoraVersion = sZv;
                }
            });
            // the 4th sword location (the Overworld + Extras checkbox): on mid-run turns Progressive Items on and raises the caps once; none right after turns it back off.
            // Progressive Items never turns the 4th sword on
            guard("4th sword turns progressive items on", () =>
            {
                var dom = TrackerModel.allItemWithHeartShuffleChoiceDomain;
                var l2 = TrackerModel.level2StairBox; var l9 = TrackerModel.GetDungeon(8).ExtraFloorBox;
                var sProg = TrackerModel.ProgressiveItems; var sFourth = TrackerModelOptions.FourthSwordEnabled;
                var sL2 = TrackerModel.level2StairBoxEnabled; var sL9 = TrackerModel.level9FloorBoxEnabled;
                var sL2Cell = l2.CellCurrent(); var sL2Has = l2.PlayerHas(); var sL9Cell = l9.CellCurrent(); var sL9Has = l9.PlayerHas();
                var sStash = OverworldItemGridUI.fourthSwordStashItem; var sStashHas = OverworldItemGridUI.fourthSwordStashHas;
                var sLatch = OverworldItemGridUI.fourthSwordL2WithHiddenNumbers; var sTurned = OverworldItemGridUI.fourthSwordTurnedProgressiveOn;
                var sTileTiers = TrackerModel.progressiveTileTier.ToList(); var sBoxTiers = TrackerModel.progressiveBoxTier.ToList();
                var sLast = (int[])WPFUI.progressiveLastTiers.Clone();
                int swordCap() => dom.MaxUses(TrackerModel.ITEMS.PROGRESSIVESWORD);
                int ringCap() => dom.MaxUses(TrackerModel.ITEMS.BLUERING);
                try
                {
                    // start with no 4th sword and Progressive Items off, as a run started without either
                    l2.Set(-1, TrackerModel.PlayerHas.NO); l9.Set(-1, TrackerModel.PlayerHas.NO);
                    OverworldItemGridUI.fourthSwordStashItem = -1;
                    OverworldItemGridUI.SetFourthSwordLocation(0);
                    TrackerModel.ProgressiveItems = false;
                    OverworldItemGridUI.ApplyProgressiveItemCaps();
                    OverworldItemGridUI.ResetFourthSwordRunState();
                    if (swordCap() != 3 || ringCap() != 1) fail($"4th sword: with Progressive Items off the caps should be 3 swords / 1 ring, not {swordCap()} / {ringCap()}");
                    OverworldItemGridUI.SetFourthSwordLocation(1);
                    if (!TrackerModelOptions.FourthSwordEnabled || !TrackerModel.level2StairBoxEnabled) fail("4th sword: level 2 did not turn the 4th sword on");
                    if (!TrackerModel.ProgressiveItems) fail("4th sword: turning it on mid-run did not turn Progressive Items on");
                    if (swordCap() != 4 || ringCap() != 2) fail($"4th sword: turning it on should raise the caps to 4 swords / 2 rings, not {swordCap()} / {ringCap()}");
                    OverworldItemGridUI.SetFourthSwordLocation(2);   // (moving it to level 9 changes nothing else)
                    if (!TrackerModel.ProgressiveItems || swordCap() != 4 || ringCap() != 2) fail($"4th sword: moving it to level 9 changed Progressive Items or raised the caps again ({swordCap()} / {ringCap()})");
                    if (!TrackerModel.level9FloorBoxEnabled || TrackerModel.level2StairBoxEnabled) fail("4th sword: level 9 did not move the slot");
                    OverworldItemGridUI.SetFourthSwordLocation(0);
                    if (TrackerModelOptions.FourthSwordEnabled || TrackerModel.level2StairBoxEnabled || TrackerModel.level9FloorBoxEnabled) fail("4th sword: none left a 4th sword slot on");
                    if (TrackerModel.ProgressiveItems) fail("4th sword: none right after it turned Progressive Items on (nothing collected since) should turn Progressive Items back off");
                    if (swordCap() != 3 || ringCap() != 1) fail($"4th sword: Progressive Items back off should put the caps back to 3 swords / 1 ring, not {swordCap()} / {ringCap()}");
                    // a progressive item marked in between: Progressive Items stays on
                    OverworldItemGridUI.SetFourthSwordLocation(1);
                    if (!l2.AttemptToSet(TrackerModel.ITEMS.BLUERING, TrackerModel.PlayerHas.YES)) fail("4th sword: could not mark a blue ring in the level 2 slot");
                    OverworldItemGridUI.SetFourthSwordLocation(0);
                    if (!TrackerModel.ProgressiveItems) fail("4th sword: none after a progressive item was collected should leave Progressive Items on");
                    OverworldItemGridUI.fourthSwordStashItem = -1;
                    // Progressive Items on before the 4th sword (from the start screen): none leaves it on.
                    // (that Progressive Items never turns the 4th sword on is checked on the start screen's real checkboxes, before the run starts: the main screen has no Progressive Items control)
                    OverworldItemGridUI.ResetFourthSwordRunState();
                    TrackerModel.ProgressiveItems = true;
                    OverworldItemGridUI.ApplyProgressiveItemCaps();
                    OverworldItemGridUI.SetFourthSwordLocation(2);
                    OverworldItemGridUI.SetFourthSwordLocation(0);
                    if (!TrackerModel.ProgressiveItems) fail("4th sword: none turned off a Progressive Items that was on before the 4th sword");
                }
                finally
                {
                    TrackerModel.ProgressiveItems = sProg;
                    OverworldItemGridUI.ApplyProgressiveItemCaps();
                    TrackerModelOptions.FourthSwordEnabled = sFourth;
                    l2.Set(-1, TrackerModel.PlayerHas.NO); l9.Set(-1, TrackerModel.PlayerHas.NO);
                    TrackerModel.SetLevel2StairBoxEnabled(sL2); TrackerModel.SetLevel9FloorBoxEnabled(sL9);   // (the slots on first, so the boxes have their change handlers when the items go back)
                    l2.AttemptToSet(sL2Cell, sL2Has); l9.AttemptToSet(sL9Cell, sL9Has);
                    OverworldItemGridUI.fourthSwordStashItem = sStash; OverworldItemGridUI.fourthSwordStashHas = sStashHas;
                    OverworldItemGridUI.fourthSwordL2WithHiddenNumbers = sLatch; OverworldItemGridUI.fourthSwordTurnedProgressiveOn = sTurned;
                    TrackerModel.progressiveTileTier.Clear(); foreach (var kv in sTileTiers) TrackerModel.progressiveTileTier[kv.Key] = kv.Value;
                    TrackerModel.progressiveBoxTier.Clear(); foreach (var kv in sBoxTiers) TrackerModel.progressiveBoxTier[kv.Key] = kv.Value;
                    Array.Copy(sLast, WPFUI.progressiveLastTiers, sLast.Length);
                    TrackerModel.recomputePlayerStateSummary();
                }
            });
            // the load's 4th Sword step (the same code a real load runs): an older save without the 4th Sword fields keeps its own Progressive Items setting;
            // a save with the 4th sword on turns Progressive Items on, but not in Legacy Mode; a load forgets an item kept from a turned-off 4th sword slot
            guard("4th sword load step", () =>
            {
                var dom = TrackerModel.allItemWithHeartShuffleChoiceDomain;
                var l2 = TrackerModel.level2StairBox;
                var sProg = TrackerModel.ProgressiveItems; var sFourth = TrackerModelOptions.FourthSwordEnabled; var sLegacy = TrackerModel.LegacyMode;
                var sL2 = TrackerModel.level2StairBoxEnabled; var sL9 = TrackerModel.level9FloorBoxEnabled;
                var sL2Cell = l2.CellCurrent(); var sL2Has = l2.PlayerHas();
                var sStash = OverworldItemGridUI.fourthSwordStashItem; var sStashHas = OverworldItemGridUI.fourthSwordStashHas;
                var sLatch = OverworldItemGridUI.fourthSwordL2WithHiddenNumbers; var sTurned = OverworldItemGridUI.fourthSwordTurnedProgressiveOn;
                string build() => string.Join("\n", SaveAndLoad.BuildSaveLines("", 0, new[] { "}" }, new string[0], "", false, 0, false, false));
                try
                {
                    TrackerModel.LegacyMode = false;
                    // a save with the 4th sword off and Progressive Items off, then made into an older save: no Level2StairBox (saves before 2.2 do not have it)
                    TrackerModel.ProgressiveItems = false;
                    TrackerModel.SetLevel2StairBoxEnabled(false); TrackerModel.SetLevel9FloorBoxEnabled(false);
                    var d = DungeonSaveAndLoad.LoadAll(build());
                    if (d.Items.Level2StairBox == null || d.Overworld.ProgressiveItems != 0) fail("4th sword load: the test save has no level 2 slot, or Progressive Items is not 0");
                    d.Items.Level2StairBox = null;
                    TrackerModel.ProgressiveItems = true;   // (the opposite of the save, so the load has to set it)
                    OverworldItemGridUI.fourthSwordStashItem = TrackerModel.ITEMS.BOW; OverworldItemGridUI.fourthSwordStashHas = TrackerModel.PlayerHas.YES;
                    SaveAndLoad.ApplyLoadedOptions(d.Overworld);   // (a real load applies the options first)
                    OverworldItemGridUI.ApplyLoadedFourthSword(d.Items);
                    if (TrackerModel.ProgressiveItems) fail("4th sword load: an older save with Progressive Items 0 and no 4th Sword fields came back with Progressive Items on");
                    if (!TrackerModelOptions.FourthSwordEnabled) fail("4th sword load: an older save should leave the 4th sword unrestricted");
                    if (OverworldItemGridUI.fourthSwordStashItem != -1) fail("4th sword load: an item kept from a turned-off 4th sword slot survived the load");
                    // a save with the 4th sword in level 2 but Progressive Items 0 (a damaged or hand-edited save): Progressive Items comes on, with its caps
                    OverworldItemGridUI.ApplyProgressiveItemCaps();
                    TrackerModel.SetLevel2StairBoxEnabled(true);
                    var d2 = DungeonSaveAndLoad.LoadAll(build());
                    TrackerModel.SetLevel2StairBoxEnabled(false);
                    SaveAndLoad.ApplyLoadedOptions(d2.Overworld);
                    OverworldItemGridUI.ApplyLoadedFourthSword(d2.Items);
                    if (!TrackerModel.ProgressiveItems) fail("4th sword load: a save with the 4th sword in level 2 did not turn Progressive Items on");
                    if (dom.MaxUses(TrackerModel.ITEMS.PROGRESSIVESWORD) != 4) fail("4th sword load: Progressive Items on did not raise the sword cap to 4");
                    if (!TrackerModel.level2StairBoxEnabled) fail("4th sword load: the level 2 slot did not come back on");
                    // the same save in Legacy Mode: Progressive Items stays off
                    TrackerModel.ProgressiveItems = false;
                    OverworldItemGridUI.ApplyProgressiveItemCaps();
                    TrackerModel.LegacyMode = true;
                    SaveAndLoad.ApplyLoadedOptions(d2.Overworld);
                    OverworldItemGridUI.ApplyLoadedFourthSword(d2.Items);
                    if (TrackerModel.ProgressiveItems) fail("4th sword load: in Legacy Mode a save with the 4th sword on turned Progressive Items on");
                }
                finally
                {
                    TrackerModel.LegacyMode = sLegacy;
                    TrackerModel.ProgressiveItems = sProg;
                    OverworldItemGridUI.ApplyProgressiveItemCaps();
                    TrackerModelOptions.FourthSwordEnabled = sFourth;
                    l2.Set(-1, TrackerModel.PlayerHas.NO);
                    TrackerModel.SetLevel2StairBoxEnabled(sL2); TrackerModel.SetLevel9FloorBoxEnabled(sL9);
                    l2.AttemptToSet(sL2Cell, sL2Has);
                    OverworldItemGridUI.fourthSwordStashItem = sStash; OverworldItemGridUI.fourthSwordStashHas = sStashHas;
                    OverworldItemGridUI.fourthSwordL2WithHiddenNumbers = sLatch; OverworldItemGridUI.fourthSwordTurnedProgressiveOn = sTurned;
                    TrackerModel.recomputePlayerStateSummary();
                }
            });
            guard("level 9 entry item and level 2 staircase slot", () =>
            {
                var trig = TrackerModel.level9TriggerBox;
                var (tCell, tHas) = (trig.CellCurrent(), trig.PlayerHas());
                if (!TrackerModel.GetDungeon(8).HasBeenLocated() && !TrackerModel.GetDungeon(8).PlayerHasTriforce() && trig.AttemptToSet(TrackerModel.ITEMS.WHITESWORD4, TrackerModel.PlayerHas.SKIPPED))
                {
                    if (new TrackerModel.TriforceAndGoSummary().MissingDungeonCount != 1) fail("go time: with a level 9 entry item, an unlocated level 9 was not counted as missing");
                    trig.AttemptToSet(tCell, tHas);
                }
                if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null)) return;
                var d = TrackerModel.GetDungeon(1);
                var (sL2, sL9) = (TrackerModel.level2StairBoxEnabled, TrackerModel.level9FloorBoxEnabled);
                var l2 = TrackerModel.level2StairBox;
                var saved = d.Boxes.Append(l2).Select(b => (b, cell: b.CellCurrent(), has: b.PlayerHas())).ToList();
                var tri = d.PlayerHasTriforce();
                try
                {
                    TrackerModel.SetLevel9FloorBoxEnabled(false); TrackerModel.SetLevel2StairBoxEnabled(true);
                    l2.Set(-1, TrackerModel.PlayerHas.NO);
                    if (!tri) d.ToggleTriforce();
                    if (d.Boxes.All(b => b.AttemptToSet(TrackerModel.ITEMS.HEARTCONTAINER, TrackerModel.PlayerHas.SKIPPED)) && d.IsDimmed)
                        fail("level 2 counted as complete with the 4th sword staircase slot empty");
                }
                finally
                {
                    foreach (var s in saved) s.b.Set(-1, TrackerModel.PlayerHas.NO);
                    foreach (var s in saved) s.b.AttemptToSet(s.cell, s.has);
                    if (d.PlayerHasTriforce() != tri) d.ToggleTriforce();
                    TrackerModel.SetLevel2StairBoxEnabled(sL2); TrackerModel.SetLevel9FloorBoxEnabled(sL9);
                    TrackerModel.recomputePlayerStateSummary();
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
                    // an any road inside a blocked area opens that area only
                    guard("any road bypass", () =>
                    {
                        var ow = new OverworldData.OverworldInstance(OverworldData.OWQuest.FIRST);
                        var saved = (OverworldData.ExtraPBBlocks, OverworldData.ExtraRaftBlocks, OverworldData.DeadWoodsEnabled, OverworldData.LostHillsEnabled, OverworldData.DeadWoodsHintKnown, OverworldData.LostHillsHintKnown);
                        try
                        {
                            OverworldData.ExtraPBBlocks = OverworldData.ExtraRaftBlocks = OverworldData.DeadWoodsEnabled = OverworldData.LostHillsEnabled = true;
                            OverworldData.DeadWoodsHintKnown = OverworldData.LostHillsHintKnown = false;
                            bool blocked(Tuple<int, int> t) => OverworldData.extraPBBlockTiles.Contains(t) ? ow.NeedsPowerBracelet(t.Item1, t.Item2)
                                : OverworldData.deadWoodsTiles.Contains(t) || OverworldData.lostHillsTiles.Contains(t) ? ow.HintLocked(t.Item1, t.Item2)
                                : ow.NeedsRaft(t.Item1, t.Item2);
                            foreach (var opened in OverworldData.anyRoadBypassGroups)
                            {
                                OverworldData.updateAnyRoadBypass(new[] { opened.First() });
                                foreach (var group in OverworldData.anyRoadBypassGroups)
                                    foreach (var t in group)
                                    {
                                        // (a tile with its own bracelet rock still needs the bracelet)
                                        var want = !group.Equals(opened) || (OverworldData.extraPBBlockTiles.Contains(t) && ow.PowerBraceletableBase(t.Item1, t.Item2));
                                        if (blocked(t) != want) fail($"any road at {opened.First()}: tile {t} blocked should be {want}");
                                    }
                            }
                        }
                        finally
                        {
                            (OverworldData.ExtraPBBlocks, OverworldData.ExtraRaftBlocks, OverworldData.DeadWoodsEnabled, OverworldData.LostHillsEnabled, OverworldData.DeadWoodsHintKnown, OverworldData.LostHillsHintKnown) = saved;
                            TrackerModel.recomputeMapStateSummary();   // rebuild the opened set from the map marks
                        }
                    });
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
            // Shops popout: an item added to a shop with no tile waits in the shop, and the first tile of that shop picks it up
            // (its own guard, so a throw here still lets the rooms below be put back)
            guard("shops popout", () =>
            {
                var popout = Popouts.shopsContent(new Window());
                popout.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                popout.Arrange(new Rect(popout.DesiredSize));
                if (popout.DesiredSize.Width < 100.0) fail("shops popout: the window content did not build");
                if (TrackerModel.shopLabelCount(2) == 0)
                {
                    var stash = TrackerModel.shopStash[2];
                    try
                    {
                        TrackerModel.shopStash[2] = -1;
                        WPFUI.addItemToShopGlobal(2, TrackerModel.ITEMS.MEAT);
                        if (!WPFUI.shopItemIdentsGlobal(2).Contains(TrackerModel.ITEMS.MEAT)) fail("shops popout: an item added to a shop with no tile did not show");
                        WPFUI.setShopLabel(2, 2, 2);
                        if (!TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[2, 2]).Contains(TrackerModel.ITEMS.MEAT)) fail("shops popout: the placed shop did not pick up the item");
                        WPFUI.addItemToShopGlobal(2, TrackerModel.ITEMS.MEAT);
                        if (WPFUI.shopItemIdentsGlobal(2).Contains(TrackerModel.ITEMS.MEAT)) fail("shops popout: adding the item again did not remove it from the shop's tile");
                        // two heart containers waiting in the shop both move onto its tile
                        WPFUI.clearAllTileIcons(2, 2);
                        TrackerModel.shopStash[2] = TrackerModel.decorIconsToggle(TrackerModel.decorIconsToggle(-1, TrackerModel.ITEMS.HEARTCONTAINER), TrackerModel.ITEMS.HEARTCONTAINER);
                        WPFUI.setShopLabel(2, 2, 2);
                        if (WPFUI.shopItemIdentsGlobal(2).Count != 2) fail("shops popout: a repeated heart container was dropped when the shop was placed");
                        // leftovers of a full first tile must not land on an empty second tile (it is copied from the first)
                        WPFUI.addItemToShopGlobal(2, TrackerModel.ITEMS.BOW);   // (the tile now holds 3: full)
                        TrackerModel.shopStash[2] = TrackerModel.decorIconsToggle(-1, TrackerModel.ITEMS.MEAT);
                        WPFUI.setShopLabel(5, 2, 2);
                        if (!TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[5, 2]).IsEmpty) fail("shops: a second tile of a full shop got the leftover item");
                    }
                    finally
                    {
                        WPFUI.clearAllTileIcons(5, 2);
                        WPFUI.clearAllTileIcons(2, 2);
                        TrackerModel.shopStash[2] = stash;
                    }
                }
            });
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

        // 6f. both players change the same field: each field has a version (number, writer), the higher one wins on both sides, with no clocks involved
        guard("co-op conflict rule", () =>
        {
            // a higher version wins, whoever wrote it
            if (!CoopSync.incomingWins(3, "a#1", 2, "z#1", false)) fail("co-op: a partner's change with a higher version was ignored");
            if (CoopSync.incomingWins(2, "z#1", 3, "a#1", false)) fail("co-op: a partner's change with a lower version overwrote ours");
            // equal numbers (both changed it at once): the writer text decides, the same way on both sides
            if (!CoopSync.incomingWins(2, "web#1", 2, "desktop#1", false)) fail("co-op: a tie did not go to the higher writer ID");
            if (CoopSync.incomingWins(2, "desktop#1", 2, "web#1", false)) fail("co-op: a tie went to the lower writer ID");
            if (CoopSync.incomingWins(2, "web#1", 2, "desktop#1", false) == CoopSync.incomingWins(2, "desktop#1", 2, "web#1", false))
                fail("co-op: the two sides of a tie did not decide the same way");
            // a stale echo (the partner's message built before our newer change reached them) carries the older version: never taken, even in a snapshot
            if (CoopSync.incomingWins(1, "web#1", 2, "desktop#1", false) || CoopSync.incomingWins(1, "web#1", 2, "desktop#1", true))
                fail("co-op: a stale echo with a lower version undid our change");
            // the same version again changes nothing, but a hello snapshot wins a tie (both at version 0: the hello winner's state is taken)
            if (CoopSync.incomingWins(4, "web#1", 4, "web#1", false)) fail("co-op: a repeat of the same version was taken as a change");
            if (!CoopSync.incomingWins(0, "", 0, "", true)) fail("co-op: a hello snapshot did not win a tie at version 0");
            // a message from an older tracker has no versions: a value it changed since its last message is taken, one it did not change keeps ours
            if (!CoopSync.unversionedTakes(null, "1", "0")) fail("co-op: an older tracker's first message was not taken");
            if (!CoopSync.unversionedTakes("0", "1", "0")) fail("co-op: an older tracker's change was not taken");
            if (CoopSync.unversionedTakes("1", "1", "0")) fail("co-op: an older tracker's unchanged value undid our change");
            if (CoopSync.unversionedTakes("0", "1", "1")) fail("co-op: an older tracker's value equal to ours counted as a change");
            // ...but a value we changed in the last 5 s keeps ours (their message can be a stale echo: ours on, then off, and their "on" arrives after)
            if (CoopSync.unversionedTakes("0", "1", "0", true)) fail("co-op: an older tracker's stale echo undid our change of a moment ago");
            // a new edit is numbered above the counter (the highest version seen from either side), not just above the key's own version:
            // after a reload ours restart at 0, and an edit stamped (1, us) lost to the partner's older (4, them) and was undone
            if (CoopSync.stampNumber(4, 0) != 5 || CoopSync.stampNumber(2, 7) != 8 || CoopSync.stampNumber(0, 0) != 1)
                fail("co-op: a new edit's version is not max(counter, key version) + 1");
            if (!CoopSync.incomingWins(CoopSync.stampNumber(4, 1), "desktop#2", 4, "web#1", false))
                fail("co-op: an edit made after a reload, numbered from the partner's counter, lost to the partner's older edit");
        });
        // 6f (boxes). An item allowed once that moves between two boxes in one message (Bow from level 6 to level 3) must arrive, and when two boxes
        // both want it, the newer version keeps it on both sides. (Level 3 used to refuse it while level 6 still held it; then level 6 was cleared.)
        guard("co-op item box moves", () =>
        {
            var b3 = TrackerModel.GetDungeon(2).Boxes[0]; var b6 = TrackerModel.GetDungeon(5).Boxes[0];
            var k3 = CoopSync.boxKey(b3); var k6 = CoopSync.boxKey(b6);
            var s3 = (b3.CellCurrent(), b3.PlayerHas()); var s6 = (b6.CellCurrent(), b6.PlayerHas());
            var YES = TrackerModel.PlayerHas.YES; var NO = TrackerModel.PlayerHas.NO; var BOW = TrackerModel.ITEMS.BOW;
            SaveAndLoad.Box box(int cell, TrackerModel.PlayerHas ph) => new SaveAndLoad.Box { CellCurrent = cell, PlayerHas = ph.AsInt() };
            if (k3 == null || k6 == null) { fail("co-op: the level 3 or level 6 box has no name"); return; }
            try
            {
                // the move: their message has the Bow in level 3 (listed first) and level 6 empty
                b3.AttemptToSet(-1, NO);
                if (!b6.AttemptToSet(BOW, YES)) { fail("co-op: could not put the Bow in level 6 for the move test"); return; }
                var m = new CoopSync.Merge { Kind = "Items", Versioned = true };
                m.Taken[k3] = (9, "partner#1"); m.Taken[k6] = (9, "partner#1");
                CoopSync.applyBoxes(m, new List<(SaveAndLoad.Box, TrackerModel.Box)> { (box(BOW, YES), b3), (box(-1, NO), b6) });
                if (b3.CellCurrent() != BOW || b6.CellCurrent() != -1 || m.Failed.Count != 0)
                    fail($"co-op: the Bow moved from level 6 to level 3 in one message ended in level 3: {b3.CellCurrent()}, level 6: {b6.CellCurrent()} (should be Bow, empty)");
                // both want it: ours (level 3, version 5 by "a") against theirs (level 6, version 5 by "z"): theirs is newer, our level 3 gives it up
                b6.AttemptToSet(-1, NO);
                CoopSync.testSetVersion(k3, (5, "a#1"));
                var m2 = new CoopSync.Merge { Kind = "Items", Versioned = true };
                m2.Taken[k6] = (5, "z#1");
                CoopSync.applyBoxes(m2, new List<(SaveAndLoad.Box, TrackerModel.Box)> { (box(BOW, NO), b6) });
                if (b6.CellCurrent() != BOW || b3.CellCurrent() != -1 || !m2.Displaced.Contains(k3))
                    fail("co-op: a newer version for the Bow in another box did not move it there");
                // and the other way: theirs is older, so our box keeps it and theirs is marked failed (ours then goes back to them)
                CoopSync.testSetVersion(k6, (5, "z#1"));
                var m3 = new CoopSync.Merge { Kind = "Items", Versioned = true };
                m3.Taken[k3] = (5, "a#1");
                CoopSync.applyBoxes(m3, new List<(SaveAndLoad.Box, TrackerModel.Box)> { (box(BOW, YES), b3) });
                if (b6.CellCurrent() != BOW || b3.CellCurrent() != -1 || !m3.Failed.Contains(k3))
                    fail("co-op: an older version for the Bow in another box took it from our newer one");
            }
            finally
            {
                CoopSync.testSetVersion(k3, null); CoopSync.testSetVersion(k6, null);
                b3.AttemptToSet(-1, NO); b6.AttemptToSet(-1, NO);
                b3.AttemptToSet(s3.Item1, s3.Item2); b6.AttemptToSet(s6.Item1, s6.Item2);
            }
        });
        // 6g. issue 35: a half-upgraded progressive item (the blue candle, two uses with Progressive Items on) can move from one slot to another slot
        // when the first slot lets go. The candle's two uses sit in two boxes, so the stair box can take it only after the floor box gives it up.
        guard("progressive item move within a dungeon", () =>
        {
            var sProg = TrackerModel.ProgressiveItems;
            var a = TrackerModel.GetDungeon(2).Boxes[0]; var s = TrackerModel.GetDungeon(2).ExtraFloorBox; var c = TrackerModel.GetDungeon(5).Boxes[0];
            var sA = (a.CellCurrent(), a.PlayerHas()); var sS = (s.CellCurrent(), s.PlayerHas()); var sC = (c.CellCurrent(), c.PlayerHas());
            var CANDLE = TrackerModel.ITEMS.BLUECANDLE; var YES = TrackerModel.PlayerHas.YES; var NO = TrackerModel.PlayerHas.NO;
            try
            {
                TrackerModel.ProgressiveItems = true;
                OverworldItemGridUI.ApplyProgressiveItemCaps();
                a.Set(-1, NO); s.Set(-1, NO); c.Set(-1, NO);
                if (!a.AttemptToSet(CANDLE, YES) || !c.AttemptToSet(CANDLE, YES)) { fail("progressive item move: could not put the candle in two boxes for the test"); return; }
                if (s.AttemptToSet(CANDLE, YES)) fail("progressive item move: the candle went into a third box while its two uses were already in boxes");
                // the move: the floor slot lets go, then the stair slot takes the candle
                a.Set(-1, NO);
                if (!s.AttemptToSet(CANDLE, YES) || s.CellCurrent() != CANDLE)
                    fail("progressive item move: after its floor slot let go, the stair slot could not take the candle");
            }
            finally
            {
                TrackerModel.ProgressiveItems = sProg;
                OverworldItemGridUI.ApplyProgressiveItemCaps();
                a.Set(sA.Item1, sA.Item2); s.Set(sS.Item1, sS.Item2); c.Set(sC.Item1, sC.Item2);
            }
        });
        // 6h. a partner's 4th Sword and Progressive Items (the Items message's FourthSwordPlace and ProgressiveItemsOn): their 4th Sword turns
        // Progressive Items on here too; their Progressive Items on never turns the 4th Sword on; their 4th Sword back to none undoes Progressive
        // Items only when this run's 4th Sword turned it on; their Progressive Items off is never taken; nothing in Legacy Mode
        guard("co-op 4th sword sync", () =>
        {
            var l2 = TrackerModel.level2StairBox;
            var l9 = TrackerModel.GetDungeon(8).ExtraFloorBox;
            var sProg = TrackerModel.ProgressiveItems; var sFourth = TrackerModelOptions.FourthSwordEnabled; var sLegacy = TrackerModel.LegacyMode;
            var sL2 = TrackerModel.level2StairBoxEnabled; var sL9 = TrackerModel.level9FloorBoxEnabled;
            var sL2Cell = l2.CellCurrent(); var sL2Has = l2.PlayerHas(); var sL9Cell = l9.CellCurrent(); var sL9Has = l9.PlayerHas();
            var sStash = OverworldItemGridUI.fourthSwordStashItem; var sStashHas = OverworldItemGridUI.fourthSwordStashHas;
            var sLatch = OverworldItemGridUI.fourthSwordL2WithHiddenNumbers; var sTurned = OverworldItemGridUI.fourthSwordTurnedProgressiveOn;
            var sTileTiers = TrackerModel.progressiveTileTier.ToList(); var sBoxTiers = TrackerModel.progressiveBoxTier.ToList();
            var sLast = (int[])WPFUI.progressiveLastTiers.Clone();
            var fk = CoopSync.fourthSwordKey; var pk = CoopSync.progressiveKey;
            // a partner's Items message with only these two fields (null: the field is left out), and its merge (both fields newer than ours)
            CoopSync.Merge apply(int? place, bool? prog)
            {
                var payload = new Newtonsoft.Json.Linq.JObject();
                var m = new CoopSync.Merge { Kind = "Items", Versioned = true, Theirs = new Dictionary<string, string>(), CheckAll = true };
                if (place is int p) { payload[CoopSync.FourthSwordField] = p; m.Taken[fk] = (9, "partner#1"); m.Theirs[fk] = p.ToString(); }
                if (prog is bool g) { payload[CoopSync.ProgressiveField] = g; m.Taken[pk] = (9, "partner#1"); m.Theirs[pk] = g ? "1" : "0"; }
                CoopSync.applyFourthSwordFromSync(m, payload);
                return m;
            }
            void startOff()
            {
                l2.Set(-1, TrackerModel.PlayerHas.NO); l9.Set(-1, TrackerModel.PlayerHas.NO);
                OverworldItemGridUI.fourthSwordStashItem = -1;
                OverworldItemGridUI.SetFourthSwordLocation(0);
                TrackerModel.ProgressiveItems = false;
                OverworldItemGridUI.ApplyProgressiveItemCaps();
                OverworldItemGridUI.ResetFourthSwordRunState();
            }
            try
            {
                TrackerModel.LegacyMode = false;
                // what we send: the place and Progressive Items, in their own fields, and as keys of the Items state
                startOff();
                OverworldItemGridUI.SetFourthSwordLocation(2);
                var sent = Newtonsoft.Json.Linq.JObject.Parse(CoopSync.buildItemsJson());
                if ((int?)sent[CoopSync.FourthSwordField] != 2 || (bool?)sent[CoopSync.ProgressiveField] != true)
                    fail($"co-op 4th sword: our Items message has place {sent[CoopSync.FourthSwordField]} and Progressive Items {sent[CoopSync.ProgressiveField]} (should be 2, true)");
                var st = CoopSync.testStateOf("Items");
                if (st == null || !st.TryGetValue(fk, out var stPlace) || stPlace != "2") fail("co-op 4th sword: the Items state has no 4th Sword place of 2 (it would never get a version)");
                // 1. their 4th Sword at level 9, ours none and Progressive Items off: both come on here
                startOff();
                var m1 = apply(2, true);
                if (!TrackerModel.level9FloorBoxEnabled || TrackerModel.level2StairBoxEnabled || !TrackerModelOptions.FourthSwordEnabled)
                    fail("co-op 4th sword: the partner's 4th Sword at level 9 did not put ours at level 9");
                if (!TrackerModel.ProgressiveItems) fail("co-op 4th sword: the partner's 4th Sword did not turn Progressive Items on here");
                // 2. their 4th Sword back to none right after (Progressive Items off there too): ours undoes Progressive Items, as this run's 4th Sword turned it on
                apply(0, false);
                if (TrackerModel.level9FloorBoxEnabled || TrackerModel.level2StairBoxEnabled) fail("co-op 4th sword: the partner's none left a 4th Sword slot on here");
                if (TrackerModel.ProgressiveItems) fail("co-op 4th sword: the partner's none did not undo Progressive Items that our 4th Sword turned on a moment ago");
                // 3. their Progressive Items on alone: ours comes on, the 4th Sword stays none
                startOff();
                apply(null, true);
                if (!TrackerModel.ProgressiveItems) fail("co-op 4th sword: the partner's Progressive Items on did not reach us");
                if (TrackerModel.level2StairBoxEnabled || TrackerModel.level9FloorBoxEnabled || TrackerModelOptions.FourthSwordEnabled)
                    fail("co-op 4th sword: the partner's Progressive Items on turned the 4th Sword on (it must never)");
                // 4. then their 4th Sword at level 2 and back to none: Progressive Items stays on (it was on before the 4th Sword)
                apply(1, true);
                if (!TrackerModel.level2StairBoxEnabled) fail("co-op 4th sword: the partner's 4th Sword at level 2 did not put ours at level 2");
                var m4 = apply(0, false);
                if (!TrackerModel.ProgressiveItems) fail("co-op 4th sword: the partner's none turned off Progressive Items that was on before the 4th Sword");
                // (their Progressive Items off is not taken, and is not checked against ours, so ours is not sent back to turn theirs on)
                if (m4.Theirs.ContainsKey(pk)) fail("co-op 4th sword: the partner's Progressive Items off, not taken, is still checked against ours (ours would be sent back)");
                // 5. an item in our level 2 slot that their message does not set: it moves with the place and counts as ours, so they get it
                startOff();
                OverworldItemGridUI.SetFourthSwordLocation(1);
                if (!l2.AttemptToSet(TrackerModel.ITEMS.BOW, TrackerModel.PlayerHas.YES)) fail("co-op 4th sword: could not mark the Bow in the level 2 slot");
                var m5 = apply(2, true);
                if (l9.CellCurrent() != TrackerModel.ITEMS.BOW || !m5.Displaced.Contains(CoopSync.boxKey(l9)))
                    fail($"co-op 4th sword: the Bow in our level 2 slot did not move to level 9 as an edit of ours (level 9 has {l9.CellCurrent()})");
                // 6. Legacy Mode: nothing is taken, and nothing of it is sent
                startOff();
                TrackerModel.LegacyMode = true;
                var m6 = apply(2, true);
                if (TrackerModel.level9FloorBoxEnabled || TrackerModel.ProgressiveItems) fail("co-op 4th sword: in Legacy Mode the partner's 4th Sword or Progressive Items was taken");
                if (m6.Taken.ContainsKey(fk) || m6.Taken.ContainsKey(pk)) fail("co-op 4th sword: in Legacy Mode the partner's 4th Sword version was kept");
                var sentLegacy = Newtonsoft.Json.Linq.JObject.Parse(CoopSync.buildItemsJson());
                if (sentLegacy[CoopSync.FourthSwordField] != null || sentLegacy[CoopSync.ProgressiveField] != null) fail("co-op 4th sword: in Legacy Mode our Items message carries the 4th Sword fields");
                TrackerModel.LegacyMode = false;
                // 7. an older tracker's message has neither field: nothing changes
                startOff();
                OverworldItemGridUI.SetFourthSwordLocation(1);
                apply(null, null);
                if (!TrackerModel.level2StairBoxEnabled) fail("co-op 4th sword: a message without the 4th Sword fields changed our place");
            }
            finally
            {
                TrackerModel.LegacyMode = sLegacy;
                TrackerModel.ProgressiveItems = sProg;
                OverworldItemGridUI.ApplyProgressiveItemCaps();
                TrackerModelOptions.FourthSwordEnabled = sFourth;
                l2.Set(-1, TrackerModel.PlayerHas.NO); l9.Set(-1, TrackerModel.PlayerHas.NO);
                TrackerModel.SetLevel2StairBoxEnabled(sL2); TrackerModel.SetLevel9FloorBoxEnabled(sL9);
                l2.AttemptToSet(sL2Cell, sL2Has); l9.AttemptToSet(sL9Cell, sL9Has);
                OverworldItemGridUI.fourthSwordStashItem = sStash; OverworldItemGridUI.fourthSwordStashHas = sStashHas;
                OverworldItemGridUI.fourthSwordL2WithHiddenNumbers = sLatch; OverworldItemGridUI.fourthSwordTurnedProgressiveOn = sTurned;
                TrackerModel.progressiveTileTier.Clear(); foreach (var kv in sTileTiers) TrackerModel.progressiveTileTier[kv.Key] = kv.Value;
                TrackerModel.progressiveBoxTier.Clear(); foreach (var kv in sBoxTiers) TrackerModel.progressiveBoxTier[kv.Key] = kv.Value;
                Array.Copy(sLast, WPFUI.progressiveLastTiers, sLast.Length);
                TrackerModel.recomputePlayerStateSummary();
            }
        });
        // 6i. the starting meat, letter and max hearts reach the partner (they had no send of their own), with their versions
        guard("co-op starting items send", () =>
        {
            var sent = new List<string>();
            SyncManager.TestSendHook = (t, p, s) => { if (t == "StartingItems") sent.Add(p); };
            CoopSync.testForceCoop = true;
            var si = TrackerModel.startingItemsAndExtras;
            var sMeat = si.PlayerHasMeat.Value(); var sHearts = si.MaxHeartsDifferential;
            try
            {
                si.PlayerHasMeat.Set(!sMeat);
                var j = sent.Count > 0 ? Newtonsoft.Json.Linq.JObject.Parse(sent[sent.Count - 1]) : null;
                if (j == null) fail("co-op starting items: changing the meat sent no StartingItems message");
                else
                {
                    if ((bool?)j["PlayerHasMeat"] != !sMeat) fail($"co-op starting items: the message has meat {j["PlayerHasMeat"]} (should be {!sMeat})");
                    if (j["SyncV"]?["k"]?["PlayerHasMeat"] == null) fail("co-op starting items: the meat change went without its version");
                }
                sent.Clear();
                si.MaxHeartsDifferential = sHearts + 1;
                CoopSync.checkMaxHeartsChange();   // (what the half second poll calls)
                var h = sent.Count > 0 ? Newtonsoft.Json.Linq.JObject.Parse(sent[sent.Count - 1]) : null;
                if (h == null || (int?)h["MaxHeartsDifferential"] != sHearts + 1) fail($"co-op starting items: a max hearts click sent {(h == null ? "nothing" : "max hearts " + h["MaxHeartsDifferential"])} (should be {sHearts + 1})");
                else if (h["SyncV"]?["k"]?["MaxHeartsDifferential"] == null) fail("co-op starting items: the max hearts change went without its version");
            }
            finally
            {
                si.PlayerHasMeat.Set(sMeat);
                si.MaxHeartsDifferential = sHearts;
                CoopSync.checkMaxHeartsChange();
                SyncManager.TestSendHook = null;
                CoopSync.testForceCoop = false;
            }
        });
        // 6j. version numbers from a partner are bounded: one above 2^40 (or too big for a number) is ignored, and stamping never wraps to a negative number
        guard("co-op version bound", () =>
        {
            if (CoopSync.stampNumber(long.MaxValue, 0) != long.MaxValue || CoopSync.stampNumber(0, long.MaxValue) != long.MaxValue)
                fail("co-op bound: stamping at long.MaxValue did not stay there (it wrapped to a negative number)");
            bool reads(string json) => CoopSync.readVersionNumber(Newtonsoft.Json.Linq.JToken.Parse(json), out _);
            if (!reads("5") || !reads(CoopSync.MaxPartnerVersion.ToString())) fail("co-op bound: a version within the bound was refused");
            if (reads((CoopSync.MaxPartnerVersion + 1).ToString()) || reads(long.MaxValue.ToString()) || reads("123456789012345678901234567890") || reads("-1") || reads("3.5") || reads("\"7\""))
                fail("co-op bound: a version above the bound, negative, or not a whole number was taken");
            // a whole message: its huge counter and huge key version change neither our counter nor the field
            // (the first partner counter heard in a run renumbers our edits made before it, which raises our counter by one per edit:
            // the starting items check above leaves two. Hear a counter of our own first, so only the bad message below can move it.)
            CoopSync.notePartnerClock(CoopSync.maxVersion);
            var before = CoopSync.maxVersion;
            var payload = Newtonsoft.Json.Linq.JObject.Parse(CoopSync.buildHintsJson());
            payload["SyncV"] = Newtonsoft.Json.Linq.JObject.Parse($"{{\"w\":[\"evil#1\"],\"k\":{{\"FireArrowsHint\":[{long.MaxValue},0]}},\"m\":{long.MaxValue}}}");
            var m = CoopSync.beginMerge("Hints", payload, "evil");
            if (CoopSync.maxVersion > CoopSync.MaxPartnerVersion) fail($"co-op bound: a counter of long.MaxValue raised ours above 2^40, to {CoopSync.maxVersion}");
            else if (CoopSync.maxVersion != before) fail($"co-op bound: a counter of long.MaxValue raised ours from {before} to {CoopSync.maxVersion}");
            if (m.Taken.ContainsKey("Hints/FireArrowsHint")) fail("co-op bound: a field with version long.MaxValue was taken");
            CoopSync.notePartnerClock(long.MaxValue);
            if (CoopSync.maxVersion > CoopSync.MaxPartnerVersion) fail($"co-op bound: a hello counter of long.MaxValue raised ours above 2^40, to {CoopSync.maxVersion}");
            else if (CoopSync.maxVersion != before) fail($"co-op bound: a hello counter of long.MaxValue raised ours from {before} to {CoopSync.maxVersion}");
        });
        // 6k. the state cache: a kind's state is parsed again only when its JSON changed, and DungeonMaps is serialized again only when a map changed.
        // It must never miss a change (here a door and a level's big numeral) and must give exactly what an uncached build gives.
        guard("co-op state cache", () =>
        {
            var a = CoopSync.buildDungeonMapsJsonCached();
            var hits = CoopSync.dungeonMapsCacheHits;
            var b = CoopSync.buildDungeonMapsJsonCached();
            if (CoopSync.dungeonMapsCacheHits != hits + 1 || !ReferenceEquals(a, b)) fail("co-op cache: unchanged maps were serialized again");
            if (b != CoopSync.buildDungeonMapsJson()) fail("co-op cache: the cached DungeonMaps differs from a fresh build");
            var door = DungeonUI.horizontalDoorsPerLevel[0][4, 4];
            var sDoor = door.State;
            var fresh = DungeonUI.isFirstTimeClickingAnyRoom[8];
            var sFresh = fresh.Value;
            var sHint = TrackerModel.FireArrowsHintWasGiven;
            try
            {
                door.State = sDoor == Dungeon.DoorState.YES ? Dungeon.DoorState.NO : Dungeon.DoorState.YES;
                var c = CoopSync.buildDungeonMapsJsonCached();
                if (c == b || c != CoopSync.buildDungeonMapsJson()) fail("co-op cache: a door change was missed by the DungeonMaps cache");
                fresh.Value = !sFresh;
                var d = CoopSync.buildDungeonMapsJsonCached();
                if (d == c || d != CoopSync.buildDungeonMapsJson()) fail("co-op cache: a level's big numeral change was missed by the DungeonMaps cache");
                // the parsed state: the same text gives the same state, a change gives a new one with the change in it
                var s1 = CoopSync.testStateOf("Hints"); var s2 = CoopSync.testStateOf("Hints");
                if (s1 == null || !ReferenceEquals(s1, s2)) fail("co-op cache: unchanged hints were parsed again");
                TrackerModel.FireArrowsHintWasGiven = !sHint;
                var s3 = CoopSync.testStateOf("Hints");
                if (s3 == null || ReferenceEquals(s3, s2) || !s3.TryGetValue("Hints/FireArrowsHint", out var v3) || v3 != (!sHint ? "true" : "false"))
                    fail("co-op cache: a hint change was missed by the state cache");
            }
            finally
            {
                door.State = sDoor;
                fresh.Value = sFresh;
                TrackerModel.FireArrowsHintWasGiven = sHint;
            }
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
