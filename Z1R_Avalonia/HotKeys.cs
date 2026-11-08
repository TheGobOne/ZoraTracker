// Ported from Z1R_WPF/HotKeys.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// Key numbers: HotKeys.txt stores keys as 0-9/a-z (turned into a key by arithmetic on the WPF System.Windows.Input.Key numbers,
// 'a'+44 = Key.A, '0'+34 = Key.D0) or as \nnn, a WPF Key number. Avalonia.Input.Key has every WPF Key name with the same number
// (checked by reflection against WPF's WindowsBase: all 202 names, 0..172, identical; Avalonia only adds keys numbered 10001 and up),
// so the numbers are used unchanged and existing HotKeys.txt files keep working. Settings don't store keys.
//
// Global hotkey registration (WPF: RegisterHotKey in Program.fs) is done in Program.cs, not here.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Z1R_Tracker.Models.Z1R_TrackerInterop;

namespace Z1R_Avalonia;


////////////////////////////////////////////////////////////
// Main Window listens for KeyDown, then sends RoutedEvent to element under the mouse

// (F#: `module MyKey` nested in HotKeys, which callers `open`. Its types live directly in namespace Z1R_Avalonia, so callers use the
// same unqualified names. SingleKeyboardModifier is a C# enum; its members are extension methods / statics in SingleKeyboardModifierExtensions,
// following Gob's RoomType/RoomTypeExtensions pattern.)
public enum SingleKeyboardModifier  // represents the 0 or 1 modifier keys currently help (or 'Unsupported' if 2 or more held at once)
{
    NoModifier,
    Shift,
    Ctrl,
    // Windows,     // key not supported unless someone asks for it
    Alt,
    Unsupported,
}
public static class SingleKeyboardModifierExtensions
{
    // (WPF reads the global Input.Keyboard.Modifiers; Avalonia has no global keyboard state, so the caller passes the event's KeyModifiers.
    // KeyModifiers.Meta (the Windows key / Cmd on macOS) plays the part of WPF's ModifierKeys.Windows.)
    public static SingleKeyboardModifier GetCurrent(KeyModifiers modifiers)
    {
        modifiers = Platform.CmdAsCtrl(modifiers);   // (port: on macOS Cmd counts as Ctrl)
        if (modifiers == KeyModifiers.Shift)
            return SingleKeyboardModifier.Shift;
        else if (modifiers == KeyModifiers.Control)
            return SingleKeyboardModifier.Ctrl;
        else if (modifiers == KeyModifiers.Alt)
            return SingleKeyboardModifier.Alt;
        else if (modifiers == KeyModifiers.None)
            return SingleKeyboardModifier.NoModifier;
        else
            return SingleKeyboardModifier.Unsupported;
    }
    public static SingleKeyboardModifier FromHotKeyTxt(string s)
    {
        if (s == "")
            return SingleKeyboardModifier.NoModifier;
        else if (s == "SHIFT")
            return SingleKeyboardModifier.Shift;
        else if (s == "CTRL")
            return SingleKeyboardModifier.Ctrl;
        else if (s == "ALT")
            return SingleKeyboardModifier.Alt;
        else throw new Exception("impossible SingleKeyboardModifier.FromHotKeyTxt");
    }
    public static string PrettyString(this SingleKeyboardModifier skm) =>
        skm switch
        {
            SingleKeyboardModifier.NoModifier => "",
            SingleKeyboardModifier.Shift => "Shift ",
            SingleKeyboardModifier.Ctrl => "Ctrl ",
            SingleKeyboardModifier.Alt => "Alt ",
            SingleKeyboardModifier.Unsupported => throw new Exception("impossible SingleKeyboardModifier.Pretty"),
            _ => throw new Exception("impossible SingleKeyboardModifier.Pretty"),
        };
}

// (WPF: `type MyKeyRoutedEventHandler = delegate of obj * MyKeyRoutedEventArgs -> unit`. Avalonia's typed routed events take an
// EventHandler<MyKeyRoutedEventArgs> instead, so that delegate type isn't needed.)
public class MyKeyRoutedEventArgs : RoutedEventArgs
{
    private readonly SingleKeyboardModifier skm;
    private readonly Key k;
    public MyKeyRoutedEventArgs(SingleKeyboardModifier skm, Key k) : base(MyKey.MyKeyEvent)
    {
        this.skm = skm;
        this.k = k;
    }
    public (SingleKeyboardModifier, Key) Key => (skm, k);
}
public class MyKey
{
    private static readonly RoutedEvent<MyKeyRoutedEventArgs> myKeyEvent = RoutedEvent.Register<MyKey, MyKeyRoutedEventArgs>("ZTrackerMyKey", RoutingStrategies.Bubble);
    public static RoutedEvent<MyKeyRoutedEventArgs> MyKeyEvent => myKeyEvent;
}

public static class MyKeyExtensions
{
    // (WPF: an extension on UIElement)
    public static void MyKeyAdd(this Control c, Action<MyKeyRoutedEventArgs> f) => c.AddHandler(MyKey.MyKeyEvent, new EventHandler<MyKeyRoutedEventArgs>((o, ea) => f(ea)));
}

public static class HotKeys
{
    public static Key convertAlpha_NumToKey(char ch)
    {
        if (ch >= 'a' && ch <= 'z')
            return (Key)((int)ch - (int)'a' + 44);
        else if (ch >= 'A' && ch <= 'Z')
            return (Key)((int)ch - (int)'A' + 44);
        else if (ch == '_')
            return Key.OemMinus;  // Minus and Underscore on same key on typical keyboard; only handling this as _ is part of \w regex
        else if (ch >= '0' && ch <= '9')
            return (Key)((int)ch - (int)'0' + 34);
        else
            throw new Exception($"bad input to convertAlpha_NumToKey '{ch}'");
    }

    public static readonly Dictionary<(SingleKeyboardModifier, Key), int> boundHotKeyUniverse = new Dictionary<(SingleKeyboardModifier, Key), int>();
    private static readonly bool _init1 = init1();
    private static bool init1()
    {
        // the overworld circles label ui uses 0-9 and a-z keys, only part of ui not rebindable by hotkeys, so ensure it exists so those keys get processed
        for (char c = '0'; c <= '9'; c++)
            boundHotKeyUniverse.Add((SingleKeyboardModifier.NoModifier, convertAlpha_NumToKey(c)), 0);
        for (char c = 'a'; c <= 'z'; c++)
            boundHotKeyUniverse.Add((SingleKeyboardModifier.NoModifier, convertAlpha_NumToKey(c)), 0);
        return true;
    }


    public static bool suspendHotKeys = false;   // set while a popup wants the raw keys itself (e.g. the Dead Woods / Lost Hills hint entry uses the arrow keys, which may be bound to cursor movement)
    // (WPF: Input.Mouse.DirectlyOver, the element under the mouse anywhere in the app. Avalonia keeps that per TopLevel
    // (IInputRoot.PointerOverElement), and a popup is a TopLevel of its own, so this also remembers the TopLevel the pointer last moved in.)
    private static TopLevel lastPointerTopLevel = null;
    private static Point lastPointerPos = new Point();   // where the pointer last was in lastPointerTopLevel
    private static IInputElement mouseDirectlyOver(Window w)
    {
        // (WPF re-evaluates Mouse.DirectlyOver whenever the layout changes; Avalonia's PointerOverElement is only refreshed lazily, so right after a popup opens
        // under a still mouse it can still be the tile underneath. A fast second hotkey (P P for the potion shop) then went to the tile, which ignores keys while a
        // popup is up, and was lost. While a popup is up, hit-test at the pointer's last position instead.)
        if (CustomComboBoxes.GlobalFlag.popupIsActive && lastPointerTopLevel != null)
        {
            try
            {
                var fresh = lastPointerTopLevel.InputHitTest(lastPointerPos);
                if (fresh != null) return fresh;
            }
            catch { }
        }
#if BROWSER
        // (browser build: the window is a control in the page, and the page is the input root)
        var root = TopLevel.GetTopLevel(w);
        var x = ((IInputRoot)root)?.PointerOverElement;
        if (x == null && lastPointerTopLevel != null && lastPointerTopLevel != root)
            x = ((IInputRoot)lastPointerTopLevel).PointerOverElement;
#else
        var x = ((IInputRoot)w).PointerOverElement;
        if (x == null && lastPointerTopLevel != null && lastPointerTopLevel != w)
            x = ((IInputRoot)lastPointerTopLevel).PointerOverElement;
#endif
        return x;
    }
    public static void InitializeWindow(Window w, TextBox notesTextBox)
    {
        w.Focusable = true;
        InputElement.PointerMovedEvent.AddClassHandler<TopLevel>((tl, pe) => { lastPointerTopLevel = tl; lastPointerPos = pe.GetPosition(tl); }, RoutingStrategies.Tunnel, true);
        // (WPF PreviewKeyDown: a tunnelling handler)
        w.AddHandler(InputElement.KeyDownEvent, new EventHandler<KeyEventArgs>((_, ea) =>
        {
            var x = mouseDirectlyOver(w) as Interactive;
            if (x != null && !suspendHotKeys && !(notesTextBox.IsFocused))
            {
                switch (SingleKeyboardModifierExtensions.GetCurrent(ea.KeyModifiers))
                {
                    case SingleKeyboardModifier.Unsupported:
                        break;
                    case SingleKeyboardModifier.Alt:
                        // only if the user might want the app to process this key...
                        // (WPF: with Alt held, the key is in ea.SystemKey and ea.Key is Key.System; Avalonia always reports the key in ea.Key)
                        if (boundHotKeyUniverse.ContainsKey((SingleKeyboardModifier.Alt, ea.Key)))
                        {
                            // ...handle events so they don't bubble out of the app (otherwise e.g. Alt-mostanything will result in system beep)
                            ea.Handled = true;
                            // Alt causes Key to be read from SystemKey
                            var ea2 = new MyKeyRoutedEventArgs(SingleKeyboardModifier.Alt, ea.Key);
                            x.RaiseEvent(ea2);
                        }
                        // ... but do let un-bound keys exit the app, so if the user has a global windows hotkey alt-x or something, the app won't steal it if it's not in HotKeys.txt
                        break;
                    case var skm:
                        if (boundHotKeyUniverse.ContainsKey((skm, ea.Key)))
                        {
                            ea.Handled = true;
                            var ea2 = new MyKeyRoutedEventArgs(skm, ea.Key);
                            x.RaiseEvent(ea2);
                            // a key nobody took while a popup is opening (the popup is not in place yet): try once more a moment later, on whatever is under the pointer then
                            if (!ea2.Handled && CustomComboBoxes.GlobalFlag.popupIsActive)
                            {
                                var key = ea.Key;
                                Avalonia.Threading.DispatcherTimer.RunOnce(() =>
                                {
                                    if (suspendHotKeys || !CustomComboBoxes.GlobalFlag.popupIsActive) return;   // (popup already gone: the key was meant for it, not for the tile)
                                    if (mouseDirectlyOver(w) is Interactive y) y.RaiseEvent(new MyKeyRoutedEventArgs(skm, key));
                                }, TimeSpan.FromMilliseconds(60));
                            }
                        }
                        break;
                }
            }
        }), RoutingStrategies.Tunnel);
    }

    ////////////////////////////////////////////////////////////

    public class UserError : Exception
    {
        public UserError(string msg) : base(msg) { }
    }

    private static (string, DoorHotKeyResponse)[] make_AllDoors()
    {
        var r = new List<(string, DoorHotKeyResponse)>();
        foreach (var (dd, s) in new[] { (DoorDirection.West, "West"), (DoorDirection.East, "East"),
                                        (DoorDirection.North, "North"), (DoorDirection.South, "South") })
        {
            r.Add(($"{s}DoorIncrement", new DoorHotKeyResponse(dd, DoorAction.Increment)));
            r.Add(($"{s}DoorDecrement", new DoorHotKeyResponse(dd, DoorAction.Decrement)));
        }
        return r.ToArray();
    }
    public static readonly (string, DoorHotKeyResponse)[] AllDoors = make_AllDoors();
    private static string[] make_AllDungeonRoomNames()
    {
        var r = new List<string>();
        foreach (var x in RoomTypeExtensions.All())
            r.Add("DungeonRoom_" + x.AsHotKeyName());
        foreach (var x in MonsterDetailExtensions.All())
            r.Add("DungeonRoom_" + x.AsHotKeyName());
        foreach (var x in FloorDropDetailExtensions.All())
            r.Add("DungeonRoom_" + x.AsHotKeyName());
        foreach (var (x, _) in AllDoors)
            r.Add("DungeonRoom_" + x);
        return r.ToArray();
    }
    public static readonly string[] AllDungeonRoomNames = make_AllDungeonRoomNames();
    // always Bottom/Left/Top/Right order, how PieMenus code works
    public static readonly string[] TakeAnyNames = {
        "Contextual_TakeAny_None",
        "Contextual_TakeAny_Potion",
        "Contextual_TakeAny_Candle",
        "Contextual_TakeAny_Heart",
        };
    // the take-any cave's button box (Took Heart / Took Candle / Took Potion / Took Nothing)
    public static readonly string[] TakeAnyBoxNames = {
        "Contextual_TakeAnyBox_None",
        "Contextual_TakeAnyBox_Potion",
        "Contextual_TakeAnyBox_Candle",
        "Contextual_TakeAnyBox_Heart",
        };
    // the Shop flyout (opened from the overworld tile menu): potion shop, then shops 1-4
    public static readonly string[] ShopNames = {
        "Contextual_Shop_Potion",
        "Contextual_Shop_1",
        "Contextual_Shop_2",
        "Contextual_Shop_3",
        "Contextual_Shop_4",
        };
    public static readonly string[] TakeThisNames = {
        "Contextual_TakeThis_None",
        "Contextual_TakeThis_Candle",
        "Contextual_TakeThis_Sword",
        "Contextual_TakeThis_ShuffledItem",
        "Contextual_TakeThis_SwordIcon",
        "Contextual_TakeThis_Letter",
    };

    // (F#: a [<RequireQualifiedAccess>] union of plain cases. A C# enum, with AsHotKeyName/AsHotKeyDisplay/All in GlobalHotkeyTargetsExtensions
    // below (extension methods can't live in a nested class), following Gob's RoomType/RoomTypeExtensions pattern.)
    public enum GlobalHotkeyTargets
    {
        ToggleMagicalSword,
        ToggleWoodSword,
        ToggleBoomBook,
        ToggleBlueCandle,
        ToggleWoodArrow,
        ToggleBlueRing,
        ToggleBombs,
        ToggleGannon,
        ToggleZelda,
        DungeonTab1,
        DungeonTab2,
        DungeonTab3,
        DungeonTab4,
        DungeonTab5,
        DungeonTab6,
        DungeonTab7,
        DungeonTab8,
        DungeonTab9,
        DungeonTabS,
        MoveCursorRight,
        MoveCursorLeft,
        MoveCursorUp,
        MoveCursorDown,
        ToggleCursorOverworldOrDungeon,
        LeftClick,
        MiddleClick,
        RightClick,
        ScrollUp,
        ScrollDown,
    }

    public static string pad(string s, int n) => s.Length < n ? s + new string(' ', n - s.Length) : s;
    // Note to self; NumPad . and + are called Decimal and Add, not OemPeriod or OemPlus.  NumPad 'enter' is not supported, sadly.
    // the dungeon-item marker icons that can be placed on an overworld tile (same set, same order as the overworld tile menu); hovering a tile and pressing a bound key toggles that icon on the tile
    public static readonly int[] OverworldMarkerItems =
        new[] { TrackerModel.ITEMS.LADDER, TrackerModel.ITEMS.POWERBRACELET, TrackerModel.ITEMS.RECORDER, TrackerModel.ITEMS.RAFT, TrackerModel.ITEMS.BOW,
                TrackerModel.ITEMS.BLUERING, TrackerModel.ITEMS.REDRING, TrackerModel.ITEMS.WOODARROW, TrackerModel.ITEMS.SILVERARROW, TrackerModel.ITEMS.WAND,
                TrackerModel.ITEMS.KEY, TrackerModel.ITEMS.BLUECANDLE, TrackerModel.ITEMS.REDCANDLE, TrackerModel.ITEMS.BOOMERANG, TrackerModel.ITEMS.MAGICBOOMERANG,
                TrackerModel.ITEMS.BOOKSHIELD, TrackerModel.ITEMS.SHIELD, TrackerModel.ITEMS.MEAT, TrackerModel.DECOR_BOMB, TrackerModel.ITEMS.OLDKEY,
                TrackerModel.ITEMS.BLUEPOTION, TrackerModel.ITEMS.LETTER, TrackerModel.ITEMS.HEARTCONTAINER, TrackerModel.ITEMS.BOMBUPGRADE,
                TrackerModel.ITEMS.WHITESWORD, TrackerModel.ITEMS.MAGICALSWORD };
    // 'BookOrShield' is split: the first item slot is now 'Book' (it still displays as the Magical Shield in a boomstick seed); the separate 'Shield' item keeps its own name.
    // (the old 'Item_BookOrShield' name is still accepted in an existing HotKeys.txt)
    // the Item_X hotkeys also work when hovering an overworld tile (toggling that marker icon); the bomb marker is not an item, so it alone gets its own 'OverworldMarker_Bomb' entry
    public static readonly int CLEAR_TILE_HOTKEY = 2000;   // pseudo-ident (in the overworld marker hotkey table): hovering a tile and pressing this key clears it completely (marker icons, caves, everything), like the red X in the tile menu
    public static readonly int[] OverworldMarkerOwnHotKeys = new[] { TrackerModel.DECOR_BOMB, CLEAR_TILE_HOTKEY };
    public static string ItemHotKeyName(int i) => i == TrackerModel.ITEMS.BOOKSHIELD ? "Book" : TrackerModel.ITEMS.AsHotKeyName(i);
    public static string OverworldMarkerHotKeyName(int ident) => ident == CLEAR_TILE_HOTKEY ? "Overworld_ClearTile" : "OverworldMarker_" + (ident == TrackerModel.DECOR_BOMB ? "Bomb" : ItemHotKeyName(ident));
    public static void MakeDefaultHotKeyFile(string filename, bool isSample)
    {
        var lines = new List<string>();
        if (isSample)
        {
            lines.Add("# DON'T EDIT THIS FILE.  This is just a blank sample template, to show what HotKeys are available.");
            lines.Add("# To make changes to your own HotKeys, you should edit HotKeys.txt.");
            lines.Add("");
        }
        // (the URL below documents WPF's Key numbers; Avalonia's are the same, see the top of this file)
        foreach (var line in string.Format(@"# {0} HotKeys

# General form is 'SelectorName = key'
#  - key can be 0-9 or a-z, or alternately of the form \nnn where nnn is the numeric key code from
#        https://docs.microsoft.com/en-us/dotnet/api/system.windows.input.key#fields
#    (so for example \75 should be used as the key name for NumPad1)
#  - key can optionally be preceded by ""SHIFT "" or ""CTRL "" or ""ALT ""
#    (so for example ""SHIFT 4"" means holding shift while pressing the '4' key)
#  - SelectorName can be any of the list below
# You can leave the key blank to not bind a hotkey to that selector

# Blank lines, or lines that start with '#', are comments and are ignored by the parser
# All other lines must have the 'General form' syntax, though whitespace is optional and ignored

    ", OverworldData.ProgramNameString).Split('\n'))
            lines.Add(line.Replace("\r", ""));
        // items
        lines.Add("# ITEMS - these hotkey bindings take effect when mouse-hovering an item box (in the dungeons or the overworld item area),");
        lines.Add("# and also when mouse-hovering an overworld map tile, where they toggle that item's marker icon on the tile (up to 3 per tile)");
        lines.Add("# Note that Book is the first item slot (it shows as the Magical Shield in a boomstick seed); Shield is a separate item. (The old name Item_BookOrShield still works as Item_Book.)");
        {
            var n = Enumerable.Range(0, TrackerModel.ITEMS.NumBuiltIn).Select(i => ItemHotKeyName(i).Length + 5).Max();
            // (F# sorts strings ordinally)
            foreach (var i in Enumerable.Range(0, TrackerModel.ITEMS.NumBuiltIn).OrderBy(i => ItemHotKeyName(i).ToLowerInvariant(), StringComparer.Ordinal))
                lines.Add(pad("Item_" + ItemHotKeyName(i), n) + " = ");
            lines.Add(pad("Item_Nothing", n) + " = ");
            lines.Add("");
        }
        lines.Add("# OVERWORLD - these hotkey bindings take effect when mouse-hovering an overworld map tile");
        lines.Add("# Note that Level1-Level8 refer to dungeons A-H if using the 'Hide Dungeon Numbers' flag setting");
        {
            var n = Enumerable.Range(0, TrackerModel.dummyOverworldTiles.Length).Select(i => TrackerModel.MapSquareChoiceDomainHelper.AsHotKeyName(i).Length + 10).Max();
            for (int i = 0; i <= TrackerModel.dummyOverworldTiles.Length - 1; i++)
                lines.Add(pad("Overworld_" + TrackerModel.MapSquareChoiceDomainHelper.AsHotKeyName(i), n) + " = ");
            lines.Add(pad("Overworld_Nothing", n) + " = ");
            lines.Add("");
        }
        lines.Add("# OVERWORLD MARKER ICONS - the Item_... hotkeys above also toggle marker icons while hovering an overworld tile;");
        lines.Add("# the bomb marker is not an item, so it has its own entry here");
        {
            var n = OverworldMarkerOwnHotKeys.Select(i => OverworldMarkerHotKeyName(i).Length).Max() + 5;
            foreach (var ident in OverworldMarkerOwnHotKeys.OrderBy(i => OverworldMarkerHotKeyName(i).ToLowerInvariant(), StringComparer.Ordinal))
                lines.Add(pad(OverworldMarkerHotKeyName(ident), n) + " = ");
            lines.Add("");
        }
        lines.Add("# BLOCKERS - these hotkey bindings take effect when mouse-hovering a blocker box");
        {
            var n = TrackerModel.DungeonBlocker.All.Select(b => b.AsHotKeyName().Length).Max();
            foreach (var b in TrackerModel.DungeonBlocker.All)
            {
                var s = b.AsHotKeyName();
                lines.Add(pad(s, (s.Contains("Maybe") || s.Contains("Nothing") ? n : n - 6)) + " = ");
            }
            lines.Add("");
        }
        lines.Add("# DUNGEON ROOMS - these hotkey bindings take effect when mouse-hovering a room in a dungeon");
        foreach (var x in AllDungeonRoomNames)
        {
            if (x.Contains("MonsterDetail_Bow"))
                lines.Add("# Note that MonsterDetail_Bow actually means MonsterDetail_Gohma, but the old Bow name is still used instead, for HotKeys.txt backward compatibility");
            var n = x.Contains("RoomType") ? 42 : x.Contains("MonsterDetail") ? 38 : x.Contains("FloorDropDetail") ? 40 : 30;
            lines.Add(pad(x, n) + " = ");
        }
        lines.Add("");
        lines.Add("# HINT ZONES - these hotkey bindings take effect when mouse-hovering a hint zone box (above triforces in non-hidden-dungeon-numbers)");
        {
            var n = TrackerModel.HintZone.All.Select(hz => hz.AsHotKeyName().Length).Max();
            foreach (var hz in TrackerModel.HintZone.All)
                lines.Add(pad(hz.AsHotKeyName(), n) + " = ");
            lines.Add("");
        }
        lines.Add("# CONTEXTUAL CHOICES - these hotkey bindings only take effect when the corresponding menus are on-screen");
        {
            var n = TakeAnyNames.Concat(TakeAnyBoxNames).Concat(ShopNames).Concat(TakeThisNames).Select(x => x.Length).Max();
            foreach (var x in TakeAnyNames)
                lines.Add(pad(x, n) + " = ");
            foreach (var x in TakeAnyBoxNames)
                lines.Add(pad(x, n) + " = ");
            foreach (var x in ShopNames)
                lines.Add(pad(x, n) + " = ");
            foreach (var x in TakeThisNames)
                lines.Add(pad(x, n) + " = ");
            lines.Add("");
        }
        lines.Add("# GLOBAL - these hotkey bindings take effect anywhere, and cannot conflict with any other non-contextuals");
        foreach (var x in GlobalHotkeyTargetsExtensions.All)
        {
            var s = x.AsHotKeyName();
            var n = s.Contains("Toggle") ? 25 : (s.Contains("Move") || s.Contains("Click") || s.Contains("Scroll")) ? 22 : 0;
            lines.Add(pad("Global_" + s, n) + " = ");
        }
        lines.Add("");
        System.IO.File.WriteAllLines(filename, lines);
    }

    // (each entry: name, the key if one is bound (WPF: an option), and (lineNumber, filename))
    public static List<(string, (SingleKeyboardModifier, Key)?, (int, string))> ParseHotKeyDataFile(string filename)
    {
        var lines = System.IO.File.ReadAllLines(filename);
        var commentRegex = new System.Text.RegularExpressions.Regex("^#.*$", System.Text.RegularExpressions.RegexOptions.None);
        var emptyLineRegex = new System.Text.RegularExpressions.Regex(@"^\s*$", System.Text.RegularExpressions.RegexOptions.None);
        var dataRegex = new System.Text.RegularExpressions.Regex(@"^\s*(\w+)\s*=\s*(SHIFT|CTRL|ALT|)\s*(\w)?\s*$", System.Text.RegularExpressions.RegexOptions.None);
        var data2Regex = new System.Text.RegularExpressions.Regex(@"^\s*(\w+)\s*=\s*(SHIFT|CTRL|ALT|)\s*\\(\d+)\s*$", System.Text.RegularExpressions.RegexOptions.None);
        var data = new List<(string, (SingleKeyboardModifier, Key)?, (int, string))>();
        var lineNumber = 1;
        foreach (var line in lines)
        {
            if (commentRegex.IsMatch(line) || emptyLineRegex.IsMatch(line))
            {
                // skip
            }
            else
            {
                var m = dataRegex.Match(line);
                if (!m.Success)
                {
                    var m2 = data2Regex.Match(line);
                    if (!m2.Success)
                        throw new UserError($"Error parsing '{filename}', line {lineNumber}:\n    {line.Trim()}\nEach line should look like  Name = KEY  (optionally SHIFT, CTRL or ALT before the key), or start with # for a comment");
                    else
                    {
                        var name = m2.Groups[1].Value;
                        // (\nnn is a WPF Key number, which is the same Avalonia Key number)
                        (SingleKeyboardModifier, Key)? value = m2.Groups.Count > 2 ? (SingleKeyboardModifierExtensions.FromHotKeyTxt(m2.Groups[2].Value), (Key)int.Parse(m2.Groups[3].Value, CultureInfo.InvariantCulture)) : null;
                        data.Add((name, value, (lineNumber, filename)));
                    }

                }
                else
                {
                    // Groups.[0] is the whole match
                    var name = m.Groups[1].Value;
                    (SingleKeyboardModifier, Key)? value = m.Groups.Count > 2 && m.Groups[3].Value.Length > 0 ? (SingleKeyboardModifierExtensions.FromHotKeyTxt(m.Groups[2].Value), convertAlpha_NumToKey(m.Groups[3].Value[0])) : null;
                    data.Add((name, value, (lineNumber, filename)));
                }
            }
            lineNumber = lineNumber + 1;
        }
        return data;
    }

    ////////////////////////////////////////////////////////////

    private static (SingleKeyboardModifier, Key)[] make_keyUniverse()
    {
        var r = new List<(SingleKeyboardModifier, Key)>();
        for (int i = 1; i <= 172; i++) // it appears Input.Key does not range outside 1 to 172
            foreach (var j in new[] { SingleKeyboardModifier.NoModifier, SingleKeyboardModifier.Shift, SingleKeyboardModifier.Ctrl, SingleKeyboardModifier.Alt })
                r.Add((j, (Key)i));
        return r.ToArray();
    }
    public static readonly (SingleKeyboardModifier, Key)[] keyUniverse = make_keyUniverse();
    // (WPF: PrettyKey(skm, key), always called with a key tuple)
    public static string PrettyKey((SingleKeyboardModifier, Key) k)
    {
        var (skm, key) = k;
        var s = key.ToString();
        if (System.Text.RegularExpressions.Regex.IsMatch(s, @"D\d"))  // the 'name' of 0-9 across top keyboard is D0-D9
            return $"{skm.PrettyString()}{s[1]}";
        else
            return skm.PrettyString() + s;
    }
    // (keys are (SingleKeyboardModifier, Key) value tuples throughout; WPF's `TryGetValue k : 'V option` is the usual C# `bool TryGetValue(k, out V)`)
    public class HotKeyProcessor<V>
    {
        private readonly string contextName;
        private readonly Dictionary<(SingleKeyboardModifier, Key), V> table = new Dictionary<(SingleKeyboardModifier, Key), V>();
        private readonly Dictionary<V, List<(SingleKeyboardModifier, Key)>> stateToKeys = new Dictionary<V, List<(SingleKeyboardModifier, Key)>>();  // caching
        private readonly Dictionary<V, string> cachedDescriptions = new Dictionary<V, string>();  // hotkeys never change during runtime, so first lookup is always the permanent description
        public HotKeyProcessor(string contextName) { this.contextName = contextName; }
        public string ContextName => contextName;
        public bool TryGetValue((SingleKeyboardModifier, Key) k, out V value) => table.TryGetValue(k, out value);
        public bool ContainsKey((SingleKeyboardModifier, Key) k) => table.ContainsKey(k);
        public (SingleKeyboardModifier, Key)[] Keys() => table.Keys.ToArray();
        public bool TryAdd((SingleKeyboardModifier, Key) k, V v)
        {
            if (table.ContainsKey(k))
                return false;
            else
            {
                table.Add(k, v);
                return true;
            }
        }
        public List<(SingleKeyboardModifier, Key)> StateToKeys(V state)
        {
            if (!(stateToKeys.ContainsKey(state)))
            {
                var r = new List<(SingleKeyboardModifier, Key)>();
                foreach (var k in keyUniverse)
                {
                    if (this.TryGetValue(k, out var x))
                    {
                        if (EqualityComparer<V>.Default.Equals(x, state)) r.Add(k);
                    }
                }
                stateToKeys.Add(state, r);
                return r;
            }
            else
                return stateToKeys[state];
        }
        // (WPF: returns a string option; here null for None)
        public string AsPrettyHotKeyOpt(V state)
        {
            var keys = this.StateToKeys(state);
            if (keys.Count > 0)
                return $"HotKey = {PrettyKey(keys[0])}";
            else
                return null;
        }
        public string AppendHotKeyToDescription(string desc, V state)
        {
            if (cachedDescriptions.TryGetValue(state, out var r0))
                return r0;
            else
            {
                string make_r()
                {
                    var keys = this.StateToKeys(state);
                    if (keys.Count > 0)
                        return $"{desc}\nHotKey = {PrettyKey(keys[0])}";
                    else
                        return desc;
                }
                var r = make_r();
                cachedDescriptions.Add(state, r);
                return r;
            }
        }
    }

    // area specific
    public static readonly HotKeyProcessor<int> ItemHotKeyProcessor = new HotKeyProcessor<int>("Item");
    public static readonly HotKeyProcessor<int> OverworldHotKeyProcessor = new HotKeyProcessor<int>("Overworld");
    public static readonly HotKeyProcessor<int> OverworldMarkerHotKeyProcessor = new HotKeyProcessor<int>("OverworldMarker");
    public static readonly HotKeyProcessor<TrackerModel.DungeonBlocker> BlockerHotKeyProcessor = new HotKeyProcessor<TrackerModel.DungeonBlocker>("Blocker");
    // (F#: Choice<RoomType,MonsterDetail,FloorDropDetail,DoorHotKeyResponse>, i.e. Choice1Of4 .. Choice4Of4. Records, so they compare by value
    // as the F# Choice did; that matters because HotKeyProcessor looks values up in dictionaries. Match with
    // `case DungeonRoomHotKey.OfRoomType(var rt):` etc.)
    public abstract record DungeonRoomHotKey
    {
        private DungeonRoomHotKey() { }
        public sealed record OfRoomType(RoomType Value) : DungeonRoomHotKey;                    // Choice1Of4
        public sealed record OfMonsterDetail(MonsterDetail Value) : DungeonRoomHotKey;          // Choice2Of4
        public sealed record OfFloorDropDetail(FloorDropDetail Value) : DungeonRoomHotKey;      // Choice3Of4
        public sealed record OfDoor(DoorHotKeyResponse Value) : DungeonRoomHotKey;              // Choice4Of4
    }
    public static readonly HotKeyProcessor<DungeonRoomHotKey> DungeonRoomHotKeyProcessor = new HotKeyProcessor<DungeonRoomHotKey>("DungeonRoom");
    public static readonly HotKeyProcessor<TrackerModel.HintZone> HintZoneHotKeyProcessor = new HotKeyProcessor<TrackerModel.HintZone>("HintZone");
    // contextual
    public static readonly HotKeyProcessor<int> TakeAnyHotKeyProcessor = new HotKeyProcessor<int>("TakeAny");
    public static readonly HotKeyProcessor<int> TakeThisHotKeyProcessor = new HotKeyProcessor<int>("TakeThis");
    public static readonly HotKeyProcessor<int> TakeAnyBoxHotKeyProcessor = new HotKeyProcessor<int>("TakeAnyBox");
    public static readonly HotKeyProcessor<int> ShopHotKeyProcessor = new HotKeyProcessor<int>("Shop");
    // global
    public static readonly HotKeyProcessor<GlobalHotkeyTargets> GlobalHotKeyProcessor = new HotKeyProcessor<GlobalHotkeyTargets>("Global");

    // (WPF: next to the exe. These are user files, see Platform.Files.User.)
    public static readonly string HotKeyFilename = Platform.Files.User("HotKeys.txt");
    public static readonly string HotKeyBlankSampleFilename = Platform.Files.User("HotKeys_BlankSampleTemplate.txt");

    public static void PopulateHotKeyTables()
    {
        {
            var filename = HotKeyBlankSampleFilename;
            // the blank sample template holds no user data, so regenerate it every launch (keeps it current with new hotkeys)
            try { MakeDefaultHotKeyFile(filename, true); } catch { }
        }
        {
            var filename = HotKeyFilename;
            if (!(System.IO.File.Exists(filename)))
                MakeDefaultHotKeyFile(filename, false);
            var data = ParseHotKeyDataFile(filename);
            foreach (var (name, chOpt, (lineNumber, filename_)) in data)   // (C#: filename_ because it can't shadow the outer filename)
            {
                void Add<T>(HotKeyProcessor<T> hkp, (SingleKeyboardModifier, Key)? keyOpt, T x)
                {
                    if (keyOpt == null)
                    {
                        // Foo=    means Foo is not bound to a hotkey on this line
                    }
                    else
                    {
                        var key = keyOpt.Value;
                        if (!(hkp.TryAdd(key, x)))
                            throw new UserError($"Keyboard key '{PrettyKey(key)}' given multiple meanings for '{hkp.ContextName}' context; second occurrence at line {lineNumber} of '{filename_}'");
                        else
                        {
                            if (!(boundHotKeyUniverse.ContainsKey(key)))
                                boundHotKeyUniverse.Add(key, 0);
                        }
                    }
                }
                switch (name)
                {
                    case "Item_Nothing":          Add(ItemHotKeyProcessor, chOpt, -1); break;
                    case "Overworld_Nothing":     Add(OverworldHotKeyProcessor, chOpt, -1); break;
                    case "Overworld_PotionShop":  Add(OverworldHotKeyProcessor, chOpt, TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP); break;   // old name of Overworld_Shop
                    default:
                        {
                            var found = false;
                            foreach (var b in TrackerModel.DungeonBlocker.All)
                                if (name == b.AsHotKeyName())
                                {
                                    Add(BlockerHotKeyProcessor, chOpt, b);
                                    found = true;
                                }
                            if (!found)
                                foreach (var hz in TrackerModel.HintZone.All)
                                    if (name == hz.AsHotKeyName())
                                    {
                                        Add(HintZoneHotKeyProcessor, chOpt, hz);
                                        found = true;
                                    }
                            if (!found)
                                for (int i = 0; i <= TrackerModel.ITEMS.NumBuiltIn - 1; i++)
                                    if (name == "Item_" + ItemHotKeyName(i) || (i == TrackerModel.ITEMS.BOOKSHIELD && name == "Item_BookOrShield"))
                                    {
                                        Add(ItemHotKeyProcessor, chOpt, i);
                                        found = true;
                                    }
                            if (!found)
                                for (int i = 0; i <= TrackerModel.dummyOverworldTiles.Length - 1; i++)
                                    if (name == "Overworld_" + TrackerModel.MapSquareChoiceDomainHelper.AsHotKeyName(i))
                                    {
                                        Add(OverworldHotKeyProcessor, chOpt, i);
                                        found = true;
                                    }
                            if (!found)
                                foreach (var ident in OverworldMarkerItems.Append(CLEAR_TILE_HOTKEY))   // (all names still accepted, for files written by pre-release test builds; they work as extra marker bindings)
                                    if (name == OverworldMarkerHotKeyName(ident))
                                    {
                                        Add(OverworldMarkerHotKeyProcessor, chOpt, ident);
                                        found = true;
                                    }
                            if (!found)
                            {
                                foreach (var x in RoomTypeExtensions.All())
                                    if (name == "DungeonRoom_" + x.AsHotKeyName())
                                    {
                                        Add(DungeonRoomHotKeyProcessor, chOpt, new DungeonRoomHotKey.OfRoomType(x));
                                        found = true;
                                    }
                                foreach (var x in MonsterDetailExtensions.All())
                                    if (name == "DungeonRoom_" + x.AsHotKeyName())
                                    {
                                        Add(DungeonRoomHotKeyProcessor, chOpt, new DungeonRoomHotKey.OfMonsterDetail(x));
                                        found = true;
                                    }
                                foreach (var x in FloorDropDetailExtensions.All())
                                    if (name == "DungeonRoom_" + x.AsHotKeyName())
                                    {
                                        Add(DungeonRoomHotKeyProcessor, chOpt, new DungeonRoomHotKey.OfFloorDropDetail(x));
                                        found = true;
                                    }
                                foreach (var (x, dhkr) in AllDoors)
                                    if (name == "DungeonRoom_" + x)
                                    {
                                        Add(DungeonRoomHotKeyProcessor, chOpt, new DungeonRoomHotKey.OfDoor(dhkr));
                                        found = true;
                                    }
                            }
                            if (!found)
                                for (int i = 0; i <= 3; i++)
                                    if (name == TakeAnyNames[i])
                                    {
                                        Add(TakeAnyHotKeyProcessor, chOpt, i);
                                        found = true;
                                    }
                            if (!found)
                                for (int i = 0; i <= 3; i++)
                                    if (name == TakeAnyBoxNames[i])
                                    {
                                        Add(TakeAnyBoxHotKeyProcessor, chOpt, i);
                                        found = true;
                                    }
                            if (!found)
                                for (int i = 0; i <= ShopNames.Length - 1; i++)
                                    if (name == ShopNames[i])
                                    {
                                        Add(ShopHotKeyProcessor, chOpt, i);
                                        found = true;
                                    }
                            if (!found)
                                for (int i = 0; i <= TakeThisNames.Length - 1; i++)
                                    if (name == TakeThisNames[i])
                                    {
                                        Add(TakeThisHotKeyProcessor, chOpt, i);
                                        found = true;
                                    }
                            if (!found)
                                foreach (var x in GlobalHotkeyTargetsExtensions.All)
                                    if (name == "Global_" + x.AsHotKeyName())
                                    {
                                        Add(GlobalHotKeyProcessor, chOpt, x);
                                        found = true;
                                    }
                            if (!found)
                                throw new UserError($"Bad name '{name}' specified in '{filename_}', line {lineNumber}\nCheck the spelling against the names in HotKeys_BlankSampleTemplate.txt");
                            break;
                        }
                }
            }
        }
        // the overworld tile and overworld marker bindings apply in the same place (hovering a tile), so they can't share a key
        foreach (var k in OverworldMarkerHotKeyProcessor.Keys())
            if (OverworldHotKeyProcessor.ContainsKey(k))
                throw new UserError($"Keyboard key '{PrettyKey(k)}' is bound both to an 'Overworld_...' entry and to an 'OverworldMarker_...' entry");
        // global conflict check
        foreach (var k in GlobalHotKeyProcessor.Keys())
        {
            void error(string kind)
            {
                GlobalHotKeyProcessor.TryGetValue(k, out var target);
                var msg = $"Global hotkey '{PrettyKey(k)}' was bound as '{"Global_" + target.AsHotKeyName()}', but that key was also bound to {kind} entry";
                throw new UserError(msg);
            }
            if (ItemHotKeyProcessor.ContainsKey(k))
                error("an 'Item_...'");
            if (OverworldHotKeyProcessor.ContainsKey(k))
                error("an 'Overworld_...'");
            if (OverworldMarkerHotKeyProcessor.ContainsKey(k))
                error("an 'OverworldMarker_...'");
            if (BlockerHotKeyProcessor.ContainsKey(k))
                error("a 'Blocker_...'");
            // don't bother with contextual hotkey conflicts, they're ok
            if (DungeonRoomHotKeyProcessor.ContainsKey(k))
                error("a 'DungeonRoom_...'");
        }
    }
}

// (F#: members of the GlobalHotkeyTargets union, which sits above `pad` in HotKeys. Extension methods have to be in a top-level class.)
public static class GlobalHotkeyTargetsExtensions
{
    public static string AsHotKeyName(this HotKeys.GlobalHotkeyTargets x) =>
        x switch
        {
            HotKeys.GlobalHotkeyTargets.ToggleMagicalSword => "ToggleMagicalSword",
            HotKeys.GlobalHotkeyTargets.ToggleWoodSword    => "ToggleWoodSword",
            HotKeys.GlobalHotkeyTargets.ToggleBoomBook     => "ToggleBoomBook",
            HotKeys.GlobalHotkeyTargets.ToggleBlueCandle   => "ToggleBlueCandle",
            HotKeys.GlobalHotkeyTargets.ToggleWoodArrow    => "ToggleWoodArrow",
            HotKeys.GlobalHotkeyTargets.ToggleBlueRing     => "ToggleBlueRing",
            HotKeys.GlobalHotkeyTargets.ToggleBombs        => "ToggleBombs",
            HotKeys.GlobalHotkeyTargets.ToggleGannon       => "ToggleGannon",
            HotKeys.GlobalHotkeyTargets.ToggleZelda        => "ToggleZelda",
            HotKeys.GlobalHotkeyTargets.DungeonTab1        => "DungeonTab1",
            HotKeys.GlobalHotkeyTargets.DungeonTab2        => "DungeonTab2",
            HotKeys.GlobalHotkeyTargets.DungeonTab3        => "DungeonTab3",
            HotKeys.GlobalHotkeyTargets.DungeonTab4        => "DungeonTab4",
            HotKeys.GlobalHotkeyTargets.DungeonTab5        => "DungeonTab5",
            HotKeys.GlobalHotkeyTargets.DungeonTab6        => "DungeonTab6",
            HotKeys.GlobalHotkeyTargets.DungeonTab7        => "DungeonTab7",
            HotKeys.GlobalHotkeyTargets.DungeonTab8        => "DungeonTab8",
            HotKeys.GlobalHotkeyTargets.DungeonTab9        => "DungeonTab9",
            HotKeys.GlobalHotkeyTargets.DungeonTabS        => "DungeonTabS",
            HotKeys.GlobalHotkeyTargets.MoveCursorLeft     => "MoveCursorLeft",
            HotKeys.GlobalHotkeyTargets.MoveCursorRight    => "MoveCursorRight",
            HotKeys.GlobalHotkeyTargets.MoveCursorUp       => "MoveCursorUp",
            HotKeys.GlobalHotkeyTargets.MoveCursorDown     => "MoveCursorDown",
            HotKeys.GlobalHotkeyTargets.ToggleCursorOverworldOrDungeon => "ToggleCursorOverworldOrDungeon",
            HotKeys.GlobalHotkeyTargets.LeftClick          => "LeftClick",
            HotKeys.GlobalHotkeyTargets.MiddleClick        => "MiddleClick",
            HotKeys.GlobalHotkeyTargets.RightClick         => "RightClick",
            HotKeys.GlobalHotkeyTargets.ScrollUp           => "ScrollUp",
            HotKeys.GlobalHotkeyTargets.ScrollDown         => "ScrollDown",
            _ => throw new Exception("impossible GlobalHotkeyTargets"),
        };
    // (WPF: returns FrameworkElement)
    public static Control AsHotKeyDisplay(this HotKeys.GlobalHotkeyTargets x)
    {
        Control mkTxt(string s) =>
            new TextBox { Background = Brushes.Black, Foreground = Brushes.White,
                          Text = s, IsReadOnly = true, IsHitTestVisible = false, HorizontalContentAlignment = HorizontalAlignment.Center,
                          HorizontalAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0), Padding = new Thickness(0.0) };
        Control tab(int level)
        {
            var labelChar = level == 9 ? '9' : level == 10 ? 'S' : TrackerModel.IsHiddenDungeonNumbers.Invoke(null) ? (char)((int)'A' - 1 + level) : (char)((int)'0' + level);
            return mkTxt($"Tab{labelChar}");
        }
        return x switch
        {
            HotKeys.GlobalHotkeyTargets.ToggleMagicalSword => Graphics.BMPtoImage(Graphics.ow_sword3_item_bmp),
            HotKeys.GlobalHotkeyTargets.ToggleWoodSword    => Graphics.BMPtoImage(Graphics.ow_sword1_item_bmp),
            HotKeys.GlobalHotkeyTargets.ToggleBoomBook     => Graphics.BMPtoImage(Graphics.boom_book_bmp),
            HotKeys.GlobalHotkeyTargets.ToggleBlueCandle   => Graphics.BMPtoImage(Graphics.blue_candle_bmp),
            HotKeys.GlobalHotkeyTargets.ToggleWoodArrow    => Graphics.BMPtoImage(Graphics.wood_arrow_bmp),
            HotKeys.GlobalHotkeyTargets.ToggleBlueRing     => Graphics.BMPtoImage(Graphics.blue_ring_bmp),
            HotKeys.GlobalHotkeyTargets.ToggleBombs        => Graphics.BMPtoImage(Graphics.bomb_bmp),
            HotKeys.GlobalHotkeyTargets.ToggleGannon       => Graphics.BMPtoImage(Graphics.ganon_bmp),
            HotKeys.GlobalHotkeyTargets.ToggleZelda        => Graphics.BMPtoImage(Graphics.zelda_bmp),
            HotKeys.GlobalHotkeyTargets.DungeonTab1        => tab(1),
            HotKeys.GlobalHotkeyTargets.DungeonTab2        => tab(2),
            HotKeys.GlobalHotkeyTargets.DungeonTab3        => tab(3),
            HotKeys.GlobalHotkeyTargets.DungeonTab4        => tab(4),
            HotKeys.GlobalHotkeyTargets.DungeonTab5        => tab(5),
            HotKeys.GlobalHotkeyTargets.DungeonTab6        => tab(6),
            HotKeys.GlobalHotkeyTargets.DungeonTab7        => tab(7),
            HotKeys.GlobalHotkeyTargets.DungeonTab8        => tab(8),
            HotKeys.GlobalHotkeyTargets.DungeonTab9        => tab(9),
            HotKeys.GlobalHotkeyTargets.DungeonTabS        => tab(10),
            HotKeys.GlobalHotkeyTargets.MoveCursorLeft     => mkTxt("←"),
            HotKeys.GlobalHotkeyTargets.MoveCursorRight    => mkTxt("→"),
            HotKeys.GlobalHotkeyTargets.MoveCursorUp       => mkTxt("↑"),
            HotKeys.GlobalHotkeyTargets.MoveCursorDown     => mkTxt("↓"),
            HotKeys.GlobalHotkeyTargets.ToggleCursorOverworldOrDungeon => mkTxt("O/D"),
            HotKeys.GlobalHotkeyTargets.LeftClick          => mkTxt("LMB"),
            HotKeys.GlobalHotkeyTargets.MiddleClick        => mkTxt("MMB"),
            HotKeys.GlobalHotkeyTargets.RightClick         => mkTxt("RMB"),
            HotKeys.GlobalHotkeyTargets.ScrollUp           => mkTxt("SWU"),
            HotKeys.GlobalHotkeyTargets.ScrollDown         => mkTxt("SWD"),
            _ => throw new Exception("impossible GlobalHotkeyTargets"),
        };
    }
    // (F#: static member GlobalHotkeyTargets.All)
    public static readonly HotKeys.GlobalHotkeyTargets[] All = {
        HotKeys.GlobalHotkeyTargets.ToggleMagicalSword,
        HotKeys.GlobalHotkeyTargets.ToggleWoodSword,
        HotKeys.GlobalHotkeyTargets.ToggleBoomBook,
        HotKeys.GlobalHotkeyTargets.ToggleBlueCandle,
        HotKeys.GlobalHotkeyTargets.ToggleWoodArrow,
        HotKeys.GlobalHotkeyTargets.ToggleBlueRing,
        HotKeys.GlobalHotkeyTargets.ToggleBombs,
        HotKeys.GlobalHotkeyTargets.ToggleGannon,
        HotKeys.GlobalHotkeyTargets.ToggleZelda,
        HotKeys.GlobalHotkeyTargets.DungeonTab1,
        HotKeys.GlobalHotkeyTargets.DungeonTab2,
        HotKeys.GlobalHotkeyTargets.DungeonTab3,
        HotKeys.GlobalHotkeyTargets.DungeonTab4,
        HotKeys.GlobalHotkeyTargets.DungeonTab5,
        HotKeys.GlobalHotkeyTargets.DungeonTab6,
        HotKeys.GlobalHotkeyTargets.DungeonTab7,
        HotKeys.GlobalHotkeyTargets.DungeonTab8,
        HotKeys.GlobalHotkeyTargets.DungeonTab9,
        HotKeys.GlobalHotkeyTargets.DungeonTabS,
        HotKeys.GlobalHotkeyTargets.MoveCursorLeft,
        HotKeys.GlobalHotkeyTargets.MoveCursorRight,
        HotKeys.GlobalHotkeyTargets.MoveCursorUp,
        HotKeys.GlobalHotkeyTargets.MoveCursorDown,
        HotKeys.GlobalHotkeyTargets.ToggleCursorOverworldOrDungeon,
        HotKeys.GlobalHotkeyTargets.LeftClick,
        HotKeys.GlobalHotkeyTargets.MiddleClick,
        HotKeys.GlobalHotkeyTargets.RightClick,
        HotKeys.GlobalHotkeyTargets.ScrollUp,
        HotKeys.GlobalHotkeyTargets.ScrollDown,
        };
}
