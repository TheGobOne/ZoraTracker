// Ported from Z1R_WPF/DungeonUI.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
// F# model events (TrackerModel's IEvent<_>s) keep their F# type; `e.Add(f)` becomes `onEvent(e, f)` (as Views.cs does).
// `makeDungeonTabs` is an `async Task<...>` (F#: `async { }`); WPF's `let!` becomes `await`.
// The F# `for` loops whose bodies make closures copy the loop variable into a fresh local first (`var level = level_;`), because a C# `for`
// variable is shared by all iterations while an F# one is fresh each time.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using Z1R_Sync;
using Z1R_Tracker.Models;
using Z1R_Tracker.Models.Z1R_TrackerInterop;

namespace Z1R_Avalonia;

public static class DungeonUI
{
    private static void onEvent<T>(IEvent<FSharpHandler<T>, T> e, Action<T> f) => e.AddHandler(new FSharpHandler<T>((_, x) => f(x)));

    public static void canvasAdd(Canvas c, Control item, double left, double top) => Graphics.canvasAdd(c, item, left, top);

    ////////////////////////

    // (F#: Choice<RoomType, MonsterDetail, FloorDropDetail, DoorHotKeyResponse>, which is HotKeys.DungeonRoomHotKey in the port)
    public static HotKeys.DungeonRoomHotKey choice1(RoomType v) => new HotKeys.DungeonRoomHotKey.OfRoomType(v);
    public static HotKeys.DungeonRoomHotKey choice2(MonsterDetail v) => new HotKeys.DungeonRoomHotKey.OfMonsterDetail(v);
    public static HotKeys.DungeonRoomHotKey choice3(FloorDropDetail v) => new HotKeys.DungeonRoomHotKey.OfFloorDropDetail(v);
    public static HotKeys.DungeonRoomHotKey choice4(DoorHotKeyResponse v) => new HotKeys.DungeonRoomHotKey.OfDoor(v);


    // level 9: which quarter of the dungeon Ganon / Zelda are in (0=NW, 1=NE, 2=SE, 3=SW, -1=unknown); this session only
    // (F#: `ref -1` cells; `.Value` reads/writes become the field)
    public static int ganonQuadrant = -1;
    public static int zeldaQuadrant = -1;

    public static class AhhGlobalVariables
    {
        public static Action<int> showShopLocatorInstanceFunc = (int _item) => { };
        public static Action hideLocator = () => { };
        public static Action resetDungeonsForRouters = () => { };
    }

    public const int TH = 24; // text height   (port: const, as F# compiles this constant, so reading it does not run the static initializers; Program.cs needs it before the CanvasManager exists)

    // (F#: `module FloatHelper` with a `System.Double` extension member. C# extension methods must live in a top-level static class,
    // so `FloatHelper` is at namespace level, at the end of this file.)

    //new code here
    // (F# union with data cases → an abstract record with nested cases, as HotKeys.DungeonRoomHotKey)
    public abstract record DoorPosition
    {
        private DoorPosition() { }
        public sealed record Horizontal(int level, int x, int y) : DoorPosition;
        public sealed record Vertical(int level, int x, int y) : DoorPosition;
    }

    // (F# records → C# records, which also compare by value; built with `new DoorChangeInfo { Level = ..., ... }` as F#'s `{ Level = ...; ... }`)
    public sealed record DoorChangeInfo
    {
        public int Level { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public bool IsHorizontal { get; init; }
        public Dungeon.DoorState NewState { get; init; }
    }

    // raised when the user changes the custom LEVEL/BOARD name from the dungeon header
    // (F#: `Event<unit>`; WPFUI triggers it too, so: `DungeonUI.customLevelNameChanged.Trigger()` → `DungeonUI.triggerCustomLevelNameChanged()`)
    public static event Action customLevelNameChanged;
    public static void triggerCustomLevelNameChanged() => customLevelNameChanged?.Invoke();
    // (F#: `Event<DoorChangeInfo>`; CoopSync's `doorChangedEvent.Publish.Add(f)` → `DungeonUI.doorChangedEvent += f`)
    public static event Action<DoorChangeInfo> doorChangedEvent;
    public static void triggerDoorChanged(DoorChangeInfo args) => doorChangedEvent?.Invoke(args);

    public static void AddDoorChangedListener(Action<DoorChangeInfo> callback) =>
        doorChangedEvent += callback;

    public sealed record RoomChangeInfo
    {
        public int Level { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public CDungeonRoomState NewState { get; init; }
    }

    public static event Action<RoomChangeInfo> roomChangedEvent;
    public static void triggerRoomChanged(RoomChangeInfo args) => roomChangedEvent?.Invoke(args);

    public static void AddRoomChangedListener(Action<RoomChangeInfo> callback) =>
        roomChangedEvent += callback;


    public static readonly Dungeon.Door[][,] horizontalDoorsPerLevel =
        Enumerable.Range(0, 9).Select(_ => new Dungeon.Door[7, 8]).ToArray();  // [X, Y]

    public static readonly Dungeon.Door[][,] verticalDoorsPerLevel =
        Enumerable.Range(0, 9).Select(_ => new Dungeon.Door[8, 7]).ToArray();  // [X, Y]

    private static DungeonRoomState[,] make_8x8RoomStates()
    {
        var a = new DungeonRoomState[8, 8];
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                a[x, y] = new DungeonRoomState();
        return a;
    }
    public static readonly DungeonRoomState[][,] roomStatesPerLevel =
        Enumerable.Range(0, 9).Select(_ => make_8x8RoomStates()).ToArray();


    public static Dungeon.Door getHorizontalDoor(int level, int x, int y) => horizontalDoorsPerLevel[level - 1][x, y];
    public static Dungeon.Door getVerticalDoor(int level, int x, int y) => verticalDoorsPerLevel[level - 1][x, y];

    public static DungeonRoomState getSyncedRoom(int level, int x, int y) => roomStatesPerLevel[level - 1][x, y];

    public static DungeonRoomState[][,] masterRoomStatesPublic = null;   // the live rooms of the 9 dungeon maps (set when the dungeon UI is built); the selftest reads these
    public static Action redrawAllRoomsPublic = null;   // (F#: `(unit -> unit) option`; null for None)

    public static readonly Action<DungeonRoomState>[][,] setNewValueFunctions =
                Enumerable.Range(0, 9).Select(_ => new Action<DungeonRoomState>[8, 8]).ToArray();

    public static readonly TrackerModel.Eventing<bool>[] isFirstTimeClickingAnyRoom = Enumerable.Range(0, 9).Select(_ => new TrackerModel.Eventing<bool>(true)).ToArray();



    public static System.Drawing.Bitmap createSizedCenteredIcon(System.Drawing.Bitmap bmp, int xOffset, int yOffset, int height, int scale, int shiftRight)
    {
        var width = 8;
        var (iconW, iconH) = (width * scale, height * scale);
        var (targetW, targetH) = (39, 27);  // tile size
        var dx = (targetW - iconW) / 2 + shiftRight;
        var dy = (targetH - iconH) / 2;
        var result = new System.Drawing.Bitmap(targetW, targetH);
        for (int x = 0; x <= targetW - 1; x++)
            for (int y = 0; y <= targetH - 1; y++)
                result.SetPixel(x, y, System.Drawing.Color.Transparent);
        for (int sx = 0; sx <= width - 1; sx++)
            for (int sy = 0; sy <= height - 1; sy++)
            {
                var color = bmp.GetPixel(xOffset + sx, yOffset + sy);
                for (int mx = 0; mx <= scale - 1; mx++)
                    for (int my = 0; my <= scale - 1; my++)
                    {
                        var x = dx + sx * scale + mx;
                        var y = dy + sy * scale + my;
                        if (x >= 0 && x < targetW && y >= 0 && y < targetH)
                            result.SetPixel(x, y, color);
                    }
            }
        return result;
    }


    private static System.Drawing.Bitmap[] make_rowBmp()
    {
        var full = Dungeon.MakeLoZMinimapDisplayBmp(new bool[8, 8], '?'); // dummy data
        var scale = 3;  // Scale each icon to 16x8 → 32x16 (approx. 75% of 39x27 tile)
        return new[] {
            // Rupee top/bottom (Y: 8–15)
            createSizedCenteredIcon(full, 72, 8, 4, scale, 2),
            createSizedCenteredIcon(full, 72, 12, 4, scale, 2),
            // Empty rows
            new System.Drawing.Bitmap(39, 27),
            new System.Drawing.Bitmap(39, 27),
            // Bomb top/bottom (Y: 24–31) — NOW rows 4/5
            createSizedCenteredIcon(full, 72, 24, 4, scale, 2),
            createSizedCenteredIcon(full, 72, 28, 4, scale, 2),
            // Key top/bottom (Y: 32–39) — NOW rows 6/7
            createSizedCenteredIcon(full, 72, 32, 4, scale, 2),
            createSizedCenteredIcon(full, 72, 36, 4, scale, 2),
        };
    }
    public static readonly System.Drawing.Bitmap[] rowBmp = make_rowBmp();



    public static readonly System.Drawing.Bitmap placeholder_row0_bmp = rowBmp[0];
    public static readonly System.Drawing.Bitmap placeholder_row1_bmp = rowBmp[1];
    public static readonly System.Drawing.Bitmap placeholder_row2_bmp = rowBmp[2];
    public static readonly System.Drawing.Bitmap placeholder_row3_bmp = rowBmp[3];
    public static readonly System.Drawing.Bitmap placeholder_row4_bmp = rowBmp[4];
    public static readonly System.Drawing.Bitmap placeholder_row5_bmp = rowBmp[5];
    public static readonly System.Drawing.Bitmap placeholder_row6_bmp = rowBmp[6];
    public static readonly System.Drawing.Bitmap placeholder_row7_bmp = rowBmp[7];



    //will adjust this to represent different icons based on the location
    public static readonly Image placeholderIcon = Graphics.BMPtoImage(Graphics.zi_alt_bomb_bmp);
    public static readonly Image[] placeholderIconsByRow =
        new[] {
            Graphics.BMPtoImage(placeholder_row0_bmp),
            Graphics.BMPtoImage(placeholder_row1_bmp),
            Graphics.BMPtoImage(placeholder_row2_bmp),  // Unused, optional dummy
            Graphics.BMPtoImage(placeholder_row3_bmp),  // Unused, optional dummy
            Graphics.BMPtoImage(placeholder_row4_bmp),
            Graphics.BMPtoImage(placeholder_row5_bmp),
            Graphics.BMPtoImage(placeholder_row6_bmp),
            Graphics.BMPtoImage(placeholder_row7_bmp),
        };
    private static bool init1()
    {
        placeholderIcon.Stretch = Stretch.Uniform;
        placeholderIcon.Opacity = 0.3;  // Adjust as needed
        return true;
    }
    private static readonly bool _init1 = init1();

    public static void wireRoomChangeEventsForSync(DungeonRoomState[][,] gridArray)
    {
        for (int level_ = 0; level_ <= 8; level_++)
        {
            var level = level_;
            var grid = gridArray[level];
            for (int x_ = 0; x_ <= 7; x_++)
                for (int y_ = 0; y_ <= 7; y_++)
                {
                    var (x, y) = (x_, y_);
                    var room = grid[x, y];

                    room.Changed += _ =>
                    {
                        if (!(TrackerModelOptions.isCurrentlyApplyingRemoteUpdate()))
                            RoomSyncBridge.OnRoomChanged.Invoke(level + 1, x, y, room.CSharp);
                    };
                }
        }
    }


    // (F#: `(cm, (_i,_j,hk), boxViewsAndActivations, dungeonIndex, returnPos)`; hk is a `DungeonRoomHotKey` or null for None)
    public static void unmarkRemarkBehavior(CustomComboBoxes.CanvasManager cm, (int, int, HotKeys.DungeonRoomHotKey) ijhk, (Canvas, Action<int, CustomComboBoxes.GridClickDismissalWarpReturn>)[] boxViewsAndActivations, int dungeonIndex, Point returnPos)
    {
        var (_i, _j, hk) = ijhk;
        var dungeon = TrackerModel.GetDungeon(dungeonIndex);
        void blockerLogic(TrackerModel.DungeonBlocker maybe)
        {
            var full = maybe.HardCanonical();
            if (dungeonIndex == 8)
                Graphics.ErrorBeepWithReminderLogText("Blockers cannot be marked for dungeon 9");
            else
            {
                // NOTE: this logic ignores Blockers' AppliesTo info
                int firstFull = -1, firstMaybe = -1, firstEmpty = -1;
                for (int bi = 0; bi <= TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON - 1; bi++)
                {
                    var b = TrackerModel.DungeonBlockersContainer.GetDungeonBlocker(dungeonIndex, bi);
                    if (b.Equals(full))
                    {
                        if (firstFull == -1) firstFull = bi;
                    }
                    else if (b.Equals(maybe))
                    {
                        if (firstMaybe == -1) firstMaybe = bi;
                    }
                    else if (b.Equals(TrackerModel.DungeonBlocker.NOTHING))
                    {
                        if (firstEmpty == -1) firstEmpty = bi;
                    }
                }
                if (firstFull != -1)
                { } // there is already a hard block marked, do nothing
                else if (firstMaybe != -1)
                    // change maybe to full
                    TrackerModel.DungeonBlockersContainer.SetDungeonBlocker(dungeonIndex, firstMaybe, full);
                else if (firstEmpty != -1)
                    // mark a maybe
                    TrackerModel.DungeonBlockersContainer.SetDungeonBlocker(dungeonIndex, firstEmpty, maybe);
                else
                    // there was no prior blocker of this kind, but there's no empty box to mark one
                    Graphics.ErrorBeepWithReminderLogText($"There are no empty blocker boxes to mark a {full.AsHotKeyName()} in this dungeon");
            }
        }
        void activateBox(int boxIndex)
        {
            var (view, activate) = boxViewsAndActivations[boxIndex];
            var pos = view.TranslatePoint(new Point(15.0, 15.0), cm.AppMainCanvas).Value;
            Graphics.WarpMouseCursorTo(pos);
            activate(0, new CustomComboBoxes.GridClickDismissalWarpReturn.WarpTo(returnPos));
        }
        var boxes = dungeon.Boxes;
        // level 2's optional extra staircase slot is activated as one more box, after the regular ones
        // (for dungeons 1-8 the extra floor box's activation is the very last entry)
        var extraIdx = dungeonIndex < 8 ? boxViewsAndActivations.Length - 1 : -1;
        var hasL2Stair = dungeonIndex == 1 && !TrackerModel.IsHiddenDungeonNumbers.Invoke(null) && TrackerModel.level2StairBoxEnabled && boxViewsAndActivations.Length > boxes.Length + (extraIdx >= 0 ? 1 : 0);
        TrackerModel.Box boxAt(int i) => i < boxes.Length ? boxes[i] : TrackerModel.level2StairBox;
        List<int> make_validBoxIndices()
        {
            if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
            {
                var lc = dungeon.LabelChar;
                var twoItemDungeons = TrackerModel.IsSecondQuestDungeons ? "123567" : "234567";
                if (twoItemDungeons.Contains(lc.ToString()))
                    return new List<int> { 0, 1 };     // filter out ghostbustered third box
                else
                    return Enumerable.Range(0, boxes.Length).ToList();
            }
            else
            {
                var l = Enumerable.Range(0, boxes.Length).ToList();
                if (hasL2Stair) l.Add(boxes.Length);
                return l;
            }
        }
        var validBoxIndices = make_validBoxIndices();
        switch (hk)
        {
            case HotKeys.DungeonRoomHotKey.OfRoomType { Value: RoomType.ItemBasement }:
                {
                    // find the bottommost empty basement, if it exists, otherwise just choose bottommost non-ghostbustered box
                    var idxs = Enumerable.Reverse(validBoxIndices).ToList();
                    var r = -1;
                    foreach (var i in idxs)
                        if (r == -1 && boxAt(i).CurrentlyHasBasementStair && boxAt(i).CellCurrent() == -1)
                            r = i;
                    if (r == -1)
                        r = idxs[0];
                    // r is now the index of the box we want
                    activateBox(r);
                    break;
                }
            case HotKeys.DungeonRoomHotKey.OfRoomType { Value: RoomType.Chevy }:
            case HotKeys.DungeonRoomHotKey.OfRoomType { Value: RoomType.CircleMoat }:
            case HotKeys.DungeonRoomHotKey.OfRoomType { Value: RoomType.DoubleMoat }:
            case HotKeys.DungeonRoomHotKey.OfRoomType { Value: RoomType.LavaMoat }:
            case HotKeys.DungeonRoomHotKey.OfRoomType { Value: RoomType.RightMoat }:
            case HotKeys.DungeonRoomHotKey.OfRoomType { Value: RoomType.Tee }:
            case HotKeys.DungeonRoomHotKey.OfRoomType { Value: RoomType.TopMoat }:
                blockerLogic(TrackerModel.DungeonBlocker.MAYBE_LADDER);
                break;
            case HotKeys.DungeonRoomHotKey.OfRoomType { Value: RoomType.HungryGoriyaMeatBlock }:
                blockerLogic(TrackerModel.DungeonBlocker.MAYBE_BAIT);
                break;
            case HotKeys.DungeonRoomHotKey.OfRoomType { Value: RoomType.LifeOrMoney }:
                blockerLogic(TrackerModel.DungeonBlocker.MAYBE_MONEY);
                break;

            case HotKeys.DungeonRoomHotKey.OfMonsterDetail { Value: MonsterDetail.Bow }:
                blockerLogic(TrackerModel.DungeonBlocker.MAYBE_BOW_AND_ARROW);
                break;
            case HotKeys.DungeonRoomHotKey.OfMonsterDetail { Value: MonsterDetail.Digdogger }:
                blockerLogic(TrackerModel.DungeonBlocker.MAYBE_RECORDER);
                break;
            case HotKeys.DungeonRoomHotKey.OfMonsterDetail { Value: MonsterDetail.Dodongo }:
                blockerLogic(TrackerModel.DungeonBlocker.MAYBE_BOMB);
                break;

            case HotKeys.DungeonRoomHotKey.OfFloorDropDetail { Value: FloorDropDetail.Triforce }:
                dungeon.ToggleTriforce();
                break;
            case HotKeys.DungeonRoomHotKey.OfFloorDropDetail { Value: FloorDropDetail.OtherKeyItem }:
                {
                    // find the topmost empty non-basement, if it exists, otherwise just choose topmost box
                    var idxs = validBoxIndices;
                    var r = -1;
                    foreach (var i in idxs)
                        if (r == -1 && !(boxAt(i).CurrentlyHasBasementStair) && boxAt(i).CellCurrent() == -1)
                            r = i;
                    if (r == -1)
                    {
                        if (dungeon.TriforceIsInItemSlot && extraIdx >= 0 && dungeon.ExtraFloorBox.CellCurrent() == -1)
                            r = extraIdx;   // the triforce is in an item slot, so the floor drop goes in the extra floor box rather than replacing a regular box
                        else
                            r = idxs[0];
                    }
                    // r is now the index of the box we want
                    activateBox(r);
                    break;
                }
            case HotKeys.DungeonRoomHotKey.OfFloorDropDetail { Value: FloorDropDetail.Heart }:
                {
                    const string noHeartsMsg = "There are no Heart Containers remaining in the item pool; cannot mark another";
                    // the dungeon's extra floor box (where the floor drop goes once the triforce sits in an item slot)
                    var extra = dungeon.ExtraFloorBox;
                    bool extraIsUsable() => dungeonIndex < 8 && (extra.CellCurrent() == -1 || (extra.CellCurrent() == TrackerModel.ITEMS.HEARTCONTAINER && extra.PlayerHas().Equals(TrackerModel.PlayerHas.NO)));
                    void markExtra()
                    {
                        if (extra.CellCurrent() == TrackerModel.ITEMS.HEARTCONTAINER)
                            extra.SetPlayerHas(TrackerModel.PlayerHas.YES);
                        else if (!extra.AttemptToSet(TrackerModel.ITEMS.HEARTCONTAINER, TrackerModel.PlayerHas.YES))
                            Graphics.ErrorBeepWithReminderLogText(noHeartsMsg);
                    }
                    // if there is a playerHas=NO floor heart, then YES it...
                    var r = -1;
                    foreach (var i in validBoxIndices)
                        if (r == -1 && !(boxAt(i).CurrentlyHasBasementStair) && boxAt(i).CellCurrent() == TrackerModel.ITEMS.HEARTCONTAINER && boxAt(i).PlayerHas().Equals(TrackerModel.PlayerHas.NO))
                            r = i;
                    if (r != -1)
                        boxAt(r).SetPlayerHas(TrackerModel.PlayerHas.YES);
                    else if (dungeon.TriforceIsInItemSlot && extraIsUsable())
                        // the triforce is in an item slot, so the floor drop is the extra floor box
                        markExtra();
                    else
                    {
                        // otherwise, if there is an empty non-basement box, fill it with a heart
                        r = -1;
                        foreach (var i in validBoxIndices)
                            if (r == -1 && !(boxAt(i).CurrentlyHasBasementStair) && boxAt(i).CellCurrent() == -1)
                                r = i;
                        if (r != -1)
                        {
                            if (!(boxAt(r).AttemptToSet(TrackerModel.ITEMS.HEARTCONTAINER, TrackerModel.PlayerHas.YES)))
                                Graphics.ErrorBeepWithReminderLogText(noHeartsMsg);
                        }
                        else
                        {
                            // no free floor box: a floor heart then means the triforce piece must be in an (empty) stair slot, so infer that and put the heart in the extra floor box
                            // (under Intra Heart Shuffle an uncollected heart in a stair slot is only the tracker's guess for "the one heart of this dungeon", and a floor heart disproves it)
                            var emptyStair = validBoxIndices.FindIndex(i =>
                            {
                                var b = boxAt(i);
                                return b.CurrentlyHasBasementStair
                                    && (b.CellCurrent() == -1
                                        || (TrackerModel.HeartShuffleMode == 1 && b.CellCurrent() == TrackerModel.ITEMS.HEARTCONTAINER && b.PlayerHas().Equals(TrackerModel.PlayerHas.NO)));
                            });
                            if (emptyStair >= 0 && extraIsUsable())
                            {
                                if (boxAt(validBoxIndices[emptyStair]).AttemptToSet(TrackerModel.ITEMS.TRIFORCE, TrackerModel.PlayerHas.NO))
                                    markExtra();
                                else
                                    Graphics.ErrorBeepWithReminderLogText("Could not infer the triforce location; cannot mark a floor heart");
                            }
                            else
                                Graphics.ErrorBeepWithReminderLogText("There were no floor-drop item boxes remaining in this dungeon, to mark a floor-heart pickup");
                        }
                    }
                    break;
                }
            case HotKeys.DungeonRoomHotKey.OfFloorDropDetail { Value: FloorDropDetail.Map }:
                // toggle has-map checkbox
                dungeon.PlayerHasMapOfThisDungeon = !dungeon.PlayerHasMapOfThisDungeon;
                break;
            default: break;
        }
    }

    public static StackPanel MakeSmallLocalTrackerPanel(int dungeonIndex, Canvas ghostBuster)
    {
        var sp = new StackPanel { Orientation = Orientation.Vertical };
        // color/number canvas
        if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
        {
            var colorCanvas = Views.MakeColorNumberCanvasForHDN(dungeonIndex);
            sp.Children.Add(colorCanvas);
        }
        else
            sp.Children.Add(new DockPanel { Height = 28.0 });
        // triforce
        var dungeonView = dungeonIndex < 8 ? Views.MakeTriforceDisplayView(null, dungeonIndex, null) : Views.MakeLevel9View(null);
        sp.Children.Add(dungeonView);
        // item boxes
        var d = TrackerModel.GetDungeon(dungeonIndex);
        foreach (var box in d.Boxes)
        {
            var c = new Canvas { Width = 30.0, Height = 30.0 };
            var (view, _activate) = Views.MakeBoxItemWithExtraDecorations(null, box, false, null);
            canvasAdd(c, view, 0.0, 0.0);
            sp.Children.Add(c);
            if (dungeonIndex != 8 && TrackerModel.IsHiddenDungeonNumbers.Invoke(null) && sp.Children.Count == 5)
            {
                // (Avalonia's TileBrush defaults to Stretch.Uniform; WPF's to Fill)
                var r = new Rectangle { Width = 30.0, Height = 30.0, Fill = new VisualBrush(ghostBuster) { Stretch = Stretch.Fill }, IsHitTestVisible = false };
                canvasAdd(c, r, 0.0, 0.0);
            }
        }
        return sp;
    }

    // the triforce and item inset
    // (returns (panel, unhighlight, posIn, boxViewsAndActivations), as WPF's tuple)
    public static (Canvas, Action, Func<Point>, (Canvas, Action<int, CustomComboBoxes.GridClickDismissalWarpReturn>)[]) MakeLocalTrackerPanel(CustomComboBoxes.CanvasManager cm, Point pos, double sunglasses, int level, Canvas ghostBuster, Func<Point> posDestinationWhenMoveCursorLeftF)
    {
        var dungeonIndex = level - 1;
        var yellow = Graphics.freeze(new SolidColorBrush(Color.FromArgb((byte)(sunglasses * 255.0), Colors.Yellow.R, Colors.Yellow.G, Colors.Yellow.B)));
        var sp = new StackPanel { Orientation = Orientation.Vertical, Opacity = sunglasses };
        var BT = 3.0;
        var SPM = 3.0;
        var border = new Border { Child = sp, BorderThickness = new Thickness(BT), BorderBrush = Brushes.DimGray, Background = Brushes.Black };
        var interiorHighlightCanvas = new Canvas();
        interiorHighlightCanvas.Children.Add(border);
        var y = 0.0;
        void AddCursorBehaviorTo(Control e, bool canDown, bool canUp)
        {
            y = y + 30.0;
            e.MyKeyAdd(ea =>
            {
                if (HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key, out var target))
                {
                    switch (target)
                    {
                        case HotKeys.GlobalHotkeyTargets.MoveCursorDown:
                            ea.Handled = true;
                            if (canDown)
                            {
                                var pos = e.TranslatePoint(new Point(Views.IDEAL_BOX_MOUSE_X, Views.IDEAL_BOX_MOUSE_Y + 30.0), cm.AppMainCanvas).Value;
                                Graphics.NavigationallyWarpMouseCursorTo(pos);
                            }
                            break;
                        case HotKeys.GlobalHotkeyTargets.MoveCursorUp:
                            ea.Handled = true;
                            if (canUp)
                            {
                                var pos = e.TranslatePoint(new Point(Views.IDEAL_BOX_MOUSE_X, Views.IDEAL_BOX_MOUSE_Y - 30.0), cm.AppMainCanvas).Value;
                                Graphics.NavigationallyWarpMouseCursorTo(pos);
                            }
                            break;
                        case HotKeys.GlobalHotkeyTargets.MoveCursorLeft:
                            ea.Handled = true;
                            Graphics.NavigationallyWarpMouseCursorTo(posDestinationWhenMoveCursorLeftF());
                            break;
                        case HotKeys.GlobalHotkeyTargets.MoveCursorRight:
                            ea.Handled = true;   // just to prevent mouse escape (allow user to 'hammer right' in the dungeon area to get to this panel
                            break;
                        default: break;
                    }
                }
            });
        }
        // draw triforce (or label if 9) and N boxes, populated as now
        // color/number canvas
        if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
        {
            var colorCanvas = Views.MakeColorNumberCanvasForHDN(dungeonIndex);
            sp.Children.Add(colorCanvas);
        }
        // triforce
        var dungeonView = dungeonIndex < 8 ? Views.MakeTriforceDisplayView(cm, dungeonIndex, null) : Views.MakeLevel9View(null);
        AddCursorBehaviorTo(dungeonView, true, false);
        var headerCell = new Canvas { Width = 30.0, Height = 30.0 };   // holds the numbered triforce, or (when a Triforce Piece is placed in an item slot) the extra floor item box
        headerCell.Children.Add(dungeonView);
        sp.Children.Add(headerCell);
        // item boxes
        var d = TrackerModel.GetDungeon(dungeonIndex);
        var slotCells = new List<Canvas>();
        (Canvas, Action<int, CustomComboBoxes.GridClickDismissalWarpReturn>)[] make_boxViewsAndActivations()
        {
            var r = new List<(Canvas, Action<int, CustomComboBoxes.GridClickDismissalWarpReturn>)>();
            foreach (var box in d.Boxes)
            {
                var c = new Canvas { Width = 30.0, Height = 30.0 };
                slotCells.Add(c);
                var (view, activate) = Views.MakeBoxItem(cm, box);
                AddCursorBehaviorTo(view, !(object.ReferenceEquals(box, d.Boxes[d.Boxes.Length - 1])), true);
                canvasAdd(c, view, 0.0, 0.0);
                sp.Children.Add(c);
                if (level != 9 && TrackerModel.IsHiddenDungeonNumbers.Invoke(null) && sp.Children.Count == 5)
                {
                    var rr = new Rectangle { Width = 30.0, Height = 30.0, Fill = new VisualBrush(ghostBuster) { Stretch = Stretch.Fill }, IsHitTestVisible = false };
                    canvasAdd(c, rr, 0.0, 0.0);
                }
                r.Add((view, activate));
            }
            return r.ToArray();
        }
        var regularBoxViewsAndActivations = make_boxViewsAndActivations();
        // mirror the main tracker: when a Triforce Piece is placed in a dungeon item slot, the numbered triforce moves into that slot and the header becomes an extra floor item box
        (Canvas, Action<int, CustomComboBoxes.GridClickDismissalWarpReturn>)? extraFloorViewAndActivate = dungeonIndex < 8 ? Views.MakeBoxItem(cm, d.ExtraFloorBox) : null;
        if (dungeonIndex < 8)
        {
            var extraFloorView = extraFloorViewAndActivate.Value.Item1;
            void refreshTriforceHeaderOrSlot()
            {
                headerCell.Children.Clear();
                foreach (var sc in slotCells) sc.Children.Remove(dungeonView);
                var k = Array.FindIndex(d.Boxes, b => b.CellCurrent() == TrackerModel.ITEMS.TRIFORCE);
                if (k >= 0 && k < 3 && k < slotCells.Count)
                {
                    headerCell.Children.Add(extraFloorView);
                    slotCells[k].Children.Add(dungeonView);
                }
                else
                    headerCell.Children.Add(dungeonView);
            }
            foreach (var b in d.Boxes)
                onEvent(b.Changed, _ => refreshTriforceHeaderOrSlot());
            refreshTriforceHeaderOrSlot();
            // like the main tracker: while the triforce sits in an item slot, right click on it opens that slot's item menu (left click checks the triforce, middle click puts the X on it)
            // (WPF PreviewMouseDown: a tunnelling handler; `ChangedButton = Right` is the press's PointerUpdateKind)
            dungeonView.AddHandler(InputElement.PointerPressedEvent, new EventHandler<PointerPressedEventArgs>((_, ea) =>
            {
                var slotK = slotCells.FindIndex(sc => object.ReferenceEquals(dungeonView.Parent, sc));
                if (slotK >= 0 && ea.GetCurrentPoint(dungeonView).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed && slotK < regularBoxViewsAndActivations.Length)
                {
                    ea.Handled = true;
                    if (!CustomComboBoxes.GlobalFlag.popupIsActive) regularBoxViewsAndActivations[slotK].Item2(0, new CustomComboBoxes.GridClickDismissalWarpReturn.WarpToCenter());
                }
            }), RoutingStrategies.Tunnel);
        }
        // the optional extra staircase item slot of level 2, as on the main tracker
        // (its activation is appended after the regular boxes, so the box hotkeys can target it)
        var boxViewsAndActivations = regularBoxViewsAndActivations;
        if (level == 2 && !(TrackerModel.IsHiddenDungeonNumbers.Invoke(null)) && TrackerModel.level2StairBoxEnabled)
        {
            var c = new Canvas { Width = 30.0, Height = 30.0 };
            var (l2view, l2activate) = Views.MakeBoxItem(cm, TrackerModel.level2StairBox);
            canvasAdd(c, l2view, 0.0, 0.0);
            sp.Children.Add(c);
            boxViewsAndActivations = regularBoxViewsAndActivations.Append((l2view, l2activate)).ToArray();
        }
        // the extra floor box (the header, while the triforce sits in an item slot) is activated last, so the box hotkeys can target it
        if (extraFloorViewAndActivate.HasValue)
            boxViewsAndActivations = boxViewsAndActivations.Append(extraFloorViewAndActivate.Value).ToArray();
        // the optional level 9 floor item check (4th Sword start option), as on the main tracker
        if (level == 9 && TrackerModel.level9FloorBoxEnabled)
        {
            var c = new Canvas { Width = 30.0, Height = 30.0 };
            canvasAdd(c, Views.MakeBoxItem(cm, d.ExtraFloorBox).Item1, 0.0, 0.0);
            sp.Children.Add(c);
        }
        sp.Margin = new Thickness(SPM);
        // dynamic highlight
        // (level 2's optional staircase slot and level 9's optional floor item slot make the column one slot taller on the main tracker)
        var extraSlots = (level == 2 && !(TrackerModel.IsHiddenDungeonNumbers.Invoke(null)) && TrackerModel.level2StairBoxEnabled && !TrackerModel.level9FloorBoxEnabled ? 1 : 0) + (level == 9 && TrackerModel.level9FloorBoxEnabled ? 1 : 0);
        var (line, triangle) = Graphics.makeArrow(30.0 * (double)dungeonIndex + 15.0, 36.0 + (double)(d.Boxes.Length + 1 + extraSlots) * 30.0, pos.X + 21.0, pos.Y - 3.0, yellow);
        var rect = new Rectangle { Width = 36.0, Height = 6.0 + (double)(d.Boxes.Length + 1 + extraSlots + (TrackerModel.IsHiddenDungeonNumbers.Invoke(null) ? 1 : 0)) * 30.0, Stroke = yellow, StrokeThickness = 3.0 };
        line.Opacity = 0.0;
        triangle.Opacity = 0.0;
        rect.Opacity = 0.0;
        var arrowCanvas = new Canvas();   // (F#: `c`; renamed because C# doesn't allow it next to the loops' `c` above)
        canvasAdd(arrowCanvas, line, 0.0, 0.0);
        canvasAdd(arrowCanvas, triangle, 0.0, 0.0);
        canvasAdd(arrowCanvas, rect, 30.0 * (double)dungeonIndex - 3.0, 27.0 - (TrackerModel.IsHiddenDungeonNumbers.Invoke(null) ? 30.0 : 0.0));
        void highlight()
        {
            if (arrowCanvas.Parent == null)
                canvasAdd(cm.AppMainCanvas, arrowCanvas, 0.0, 0.0);
            line.Opacity = 1.0;
            triangle.Opacity = 1.0;
            rect.Opacity = 1.0;
            border.BorderBrush = yellow;
        }
        void unhighlight()
        {
            cm.AppMainCanvas.Children.Remove(arrowCanvas);
            line.Opacity = 0.0;
            triangle.Opacity = 0.0;
            rect.Opacity = 0.0;
            border.BorderBrush = Brushes.DimGray;
        }
        sp.PointerEntered += (_, _) => highlight();
        sp.PointerExited += (_, _) => unhighlight();
        return (interiorHighlightCanvas, unhighlight, () => dungeonView.TranslatePoint(new Point(Views.IDEAL_BOX_MOUSE_X, Views.IDEAL_BOX_MOUSE_Y), cm.AppMainCanvas).Value, boxViewsAndActivations);
    }

    // (WPF freezes it; Avalonia transforms can't be frozen, and one instance can still be shared)
    public static readonly RotateTransform fortyFiveDegrees = new RotateTransform(45.0);
    public static List<Control> makeOutlineShapesImpl(string[] quest)
    {
        var outlines = new List<Control>();
        var color = Brushes.MediumPurple;
        DrawingBrush make_hatchBrush()
        {
            var myGeometryGroup = new GeometryGroup();
            myGeometryGroup.Children.Add(new LineGeometry(new Point(0.0, 0.0), new Point(9.0, 0.0)));
            myGeometryGroup.Children.Add(new LineGeometry(new Point(0.0, 9.0), new Point(0.0, 0.0)));
            // (WPF: StartLineCap/EndLineCap Square; Avalonia's Pen has one LineCap)
            var p = new Pen { Brush = color, Thickness = 3.0, LineCap = PenLineCap.Square };
            var myDrawing = new GeometryDrawing { Brush = null, Pen = p, Geometry = myGeometryGroup };
            // (WPF Viewbox/Viewport in absolute units → Avalonia SourceRect/DestinationRect)
            return new DrawingBrush { Drawing = myDrawing, SourceRect = new RelativeRect(0.0, 0.0, 9.0, 9.0, RelativeUnit.Absolute), DestinationRect = new RelativeRect(0.0, 0.0, 9.0, 9.0, RelativeUnit.Absolute),
                              TileMode = TileMode.Tile, Stretch = Stretch.UniformToFill, Transform = fortyFiveDegrees };
        }
        var hatchBrush = make_hatchBrush();
        // fixed dungeon drawing outlines - vertical segments
        for (int i = 0; i <= 6; i++)
            for (int j = 0; j <= 7; j++)
                if (quest[j][i] != quest[j][i + 1])
                {
                    var s = new Line { StartPoint = new Point((double)(i * (39 + 12) + 39 + 12 / 2), (double)(TH + j * (27 + 12) - 12 / 2)), EndPoint = new Point((double)(i * (39 + 12) + 39 + 12 / 2), (double)(TH + j * (27 + 12) + 27 + 12 / 2)),
                                    Stroke = color, StrokeThickness = 3.0, IsHitTestVisible = false };
                    outlines.Add(s);
                }
        // fixed dungeon drawing outlines - horizontal segments
        for (int i = 0; i <= 7; i++)
            for (int j = 0; j <= 6; j++)
                if (quest[j][i] != quest[j + 1][i])
                {
                    var s = new Line { StartPoint = new Point((double)(i * (39 + 12) - 12 / 2), (double)(TH + (j + 1) * (27 + 12) - 12 / 2)), EndPoint = new Point((double)(i * (39 + 12) + 39 + 12 / 2), (double)(TH + (j + 1) * (27 + 12) - 12 / 2)),
                                    Stroke = color, StrokeThickness = 3.0, IsHitTestVisible = false };
                    outlines.Add(s);
                }
        // fixed dungeon drawing outlines - off-map (non-)rooms
        for (int i = 0; i <= 7; i++)
            for (int j = 0; j <= 7; j++)
                if (quest[j][i] != 'X')
                {
                    var s = new Rectangle { Width = (double)(39 + 12), Height = (double)(27 + 12), Fill = hatchBrush, IsHitTestVisible = false, Opacity = 0.45 };
                    Canvas.SetLeft(s, (double)(i * (39 + 12) - 12 / 2));
                    Canvas.SetTop(s, (double)(TH + j * (27 + 12) - 12 / 2));
                    outlines.Add(s);
                }
        return outlines;
    }
    public static List<Control> makeFirstQuestOutlineShapes(int dungeonNumber) => makeOutlineShapesImpl(DungeonData.firstQuest[dungeonNumber]);
    public static List<Control> makeSecondQuestOutlineShapes(int dungeonNumber) => makeOutlineShapesImpl(DungeonData.secondQuest[dungeonNumber]);

    public static readonly Func<DungeonSaveAndLoad.DungeonModel>[] exportFunctionsLarge = Enumerable.Repeat<Func<DungeonSaveAndLoad.DungeonModel>>(() => new DungeonSaveAndLoad.DungeonModel(), 9).ToArray();
    public static readonly Action<DungeonSaveAndLoad.DungeonModel>[] importFunctions = Enumerable.Repeat<Action<DungeonSaveAndLoad.DungeonModel>>(_ => { }, 9).ToArray();
    public static string[] exportDungeonModelsJsonLines() => DungeonSaveAndLoad.SaveAllDungeons(exportFunctionsLarge.Select(f => f()).ToArray());

    ////////////////////////

    public static RoomType defaultRoom() => TrackerModelOptions.DefaultRoomPreferNonDescriptToMaybePushBlock.Value ? RoomType.NonDescript : RoomType.MaybePushBlock;

    public enum TrackerLocation
    {
        OVERWORLD,
        DUNGEON,
    }

    public static TabControl theDungeonTabControl = null;
    public static readonly FontFamily HFF = new FontFamily("Courier New");   // (not installed on macOS/Linux; Avalonia falls back to another font, see PLAN.md risk 5)
    private static LinearGradientBrush make_rainbowBrush()
    {
        var gsc = new GradientStops();
        gsc.Add(new GradientStop(Colors.Red, 0.0));
        gsc.Add(new GradientStop(Colors.Orange, 0.2));
        gsc.Add(new GradientStop(Colors.Yellow, 0.4));
        gsc.Add(new GradientStop(Colors.LightGreen, 0.6));
        gsc.Add(new GradientStop(Colors.LightBlue, 0.8));
        gsc.Add(new GradientStop(Colors.MediumPurple, 1.0));
        // (WPF's LinearGradientBrush(gsc, 90.) runs from (0,0) to (cos 90°, sin 90°) = (0,1): top to bottom)
        return new LinearGradientBrush { GradientStops = gsc, StartPoint = new RelativePoint(0.0, 0.0, RelativeUnit.Relative), EndPoint = new RelativePoint(0.0, 1.0, RelativeUnit.Relative) };
    }
    public static readonly LinearGradientBrush rainbowBrush = make_rainbowBrush();

    // (port helper: WPF's Cursors.Hand is a shared static cursor; an Avalonia cursor is made on demand, after the app has started)
    private static Cursor _handCursor = null;
    private static Cursor handCursor() => _handCursor ??= new Cursor(StandardCursorType.Hand);

    // (F#: makeDungeonTabs(cm, layoutF, posYF, selectDungeonTabEvent, TH, rightwardCanvas, levelTabSelected, blockersHoverEvent, mainTrackerGhostbusters, showProgress,
    //  contentCanvasMouseEnterFunc, contentCanvasMouseLeaveFunc) = async { ... return dungeonTabsWholeCanvas, point, grabModeTextBlock, exportDungeonModelsJsonLines, importDungeonModels }.
    //  Same parameters in the same order; the F# events passed in are Graphics.SharedEvent<T>s; showProgress is `string -> Async<unit>` → Func<string, Task>.)
    public static async Task<(Canvas, Point, Border, Func<string[]>, Func<Func<string, Task>, DungeonSaveAndLoad.DungeonModel[], Task>)> makeDungeonTabs(
                    CustomComboBoxes.CanvasManager cm, Action<Canvas> layoutF, Func<double> posYF, Graphics.SharedEvent<int> selectDungeonTabEvent,
                    int TH, Canvas rightwardCanvas, Graphics.SharedEvent<int> levelTabSelected, Graphics.SharedEvent<bool> blockersHoverEvent,
                    Canvas[] mainTrackerGhostbusters, Func<string, Task> showProgress, Action<int> contentCanvasMouseEnterFunc, Action<int> contentCanvasMouseLeaveFunc)
    {
        await showProgress($"begin makeDungeonTabs");
        var dungeonTabsWholeCanvas = new Canvas { Height = (double)(2 * TH + 3 + 27 * 8 + 12 * 7 + 3 + 6) };  // need to set height, as caller uses it
        layoutF(dungeonTabsWholeCanvas);

        rightwardCanvas.Height = dungeonTabsWholeCanvas.Height;
        var outlineDrawingCanvases = Enumerable.Range(0, 9).Select(_ => new Canvas()).ToArray(); // where we draw non-shapes-dungeons overlays
        var currentOutlineDisplayState = new int[9];   // 0=nothing, 1-9 = FQ, 10-18 = SQ
        void doVanillaOutlineRedraw(Canvas canvasToRedraw, int state)
        {
            canvasToRedraw.Children.Clear();
            if (state >= 1 && state <= 9)
                foreach (var s in makeFirstQuestOutlineShapes(state - 1))
                    canvasToRedraw.Children.Add(s);
            if (state >= 10 && state <= 18)
                foreach (var s in makeSecondQuestOutlineShapes(state - 10))
                    canvasToRedraw.Children.Add(s);
        }
        var grabHelper = new Dungeon.GrabHelper();
        var grabModeTextBlock =
            new Border { BorderThickness = new Thickness(2.0), BorderBrush = Brushes.LightGray,
                        Child = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 16.0, Foreground = Brushes.Black, Background = Brushes.Gray, IsHitTestVisible = false,
                                            Text = "You are now in 'move mode', which can be used to move any set of dungeon rooms and doors.\n\nTo abort move mode, click again on 'MOVE' in the dungeon tracker.\n\nLeft click each room you want to move (click again to un-pick it); picked rooms turn orange.  When you have picked them all, right click to 'cut' them out - they will follow your cursor, with a preview of where they would land.  Left click to 'drop' them on the map.  Adjacent doors come along for the ride.\n\nUpon completion, you will be prompted to keep changes or undo them, so you can experiment." }
            };
        var moveModeText = ((TextBlock)grabModeTextBlock.Child).Text;
        var oldGrabModeText = "You are now in 'grab mode', which can be used to move an entire segment of dungeon rooms and doors at once.\n\nTo abort grab mode, click again on 'GRAB' in the dungeon tracker. (Middle click it to switch to 'MOVE', where you pick the rooms one by one.)\n\nTo move a segment, first click any marked room, to pick up that room and all contiguous rooms.  Then click again on a new location to 'drop' the segment you grabbed.  After grabbing, hovering the mouse shows a preview of where you would drop.  This behaves like 'cut and paste', and adjacent doors will come along for the ride.\n\nUpon completion, you will be prompted to keep changes or undo them, so you can experiment.";
        var dungeonTabs = new TabControl { FontSize = 12.0, Background = Brushes.Black };
        theDungeonTabControl = dungeonTabs;
        //let masterRoomStates = Array.init 9 (fun _ -> Array2D.init 8 8 (fun _ _ -> DungeonRoomState()))
        DungeonRoomState[][,] make_masterRoomStates()
        {
            var arr = new DungeonRoomState[9][,];
            for (int level = 0; level < 9; level++)
            {
                arr[level] = new DungeonRoomState[8, 8];
                for (int x = 0; x < 8; x++)
                    for (int y = 0; y < 8; y++)
                    {
                        var room = new DungeonRoomState();
                        room.CSharp.X = x;
                        room.CSharp.Y = y;
                        room.CSharp.Level = level + 1; // 1-based Level
                        arr[level][x, y] = room;
                    }
            }


            // Sync event hook for every room
            for (int level = 0; level <= 8; level++)
                for (int x = 0; x <= 7; x++)
                    for (int y = 0; y <= 7; y++)
                    {
                        var room = arr[level][x, y];
                        room.CSharp.Changed += (_, _) =>
                            TrackerModelOptions.DebugConfig.Log("[RoomSync] Changed triggered for L%d (%d,%d) — ID %s");
                    }


            return arr;
        }
        var masterRoomStates = make_masterRoomStates();
        masterRoomStatesPublic = masterRoomStates;

        var masterRedrawAllRooms = Enumerable.Range(0, 9).Select(_ => (Action)(() => { })).ToArray();

        OptionsMenu.displayIconsInDungeonMapOptionChanged += () =>
        {
            for (int i = 0; i <= 8; i++)
                masterRedrawAllRooms[i]();
        };

        // make the whole canvas
        canvasAdd(dungeonTabsWholeCanvas, dungeonTabs, 0.0, 0.0);

        var isHoveringFQSQ = new TrackerModel.Eventing<bool>(false);

        if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
        {
            var button = new Button { Content = new TextBox { FontSize = 12.0, Foreground = Brushes.Orange, Background = Brushes.Black, BorderThickness = new Thickness(0.0),
                                                        Text = "FQ/SQ", IsReadOnly = true, IsHitTestVisible = false, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center },
                                            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
                                            BorderThickness = new Thickness(1.0), Margin = new Thickness(0.0), Padding = new Thickness(0.0) };
            canvasAdd(dungeonTabsWholeCanvas, button, 405.0, 0.0);

            TextBox mkTxt(string txt, IBrush color) =>
                new TextBox { Width = 50.0, Height = 30.0, FontSize = 15.0, Foreground = color, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false,
                            BorderThickness = new Thickness(0.0), Text = txt, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };

            button.PointerEntered += (_, _) => isHoveringFQSQ.Value = true;
            button.PointerExited += (_, _) => isHoveringFQSQ.Value = false;
            button.Click += (_, _) =>
            {
                if (!CustomComboBoxes.GlobalFlag.popupIsActive && !(dungeonTabs.SelectedIndex == 9))  // no behavior on summary tab
                {
                    CustomComboBoxes.GlobalFlag.popupIsActive = true;

                    var ST = CustomComboBoxes.borderThickness;
                    var SI = dungeonTabs.SelectedIndex;
                    var roomStates = masterRoomStates[SI];
                    bool isCompatible(int q, int l)
                    {
                        var quest = q == 1 ? DungeonData.firstQuest : DungeonData.secondQuest;
                        var ok = true;
                        for (int x = 0; x <= 7; x++)
                            for (int y = 0; y <= 7; y++)
                                if (!(roomStates[x, y].IsEmpty) && quest[l - 1][y][x] != 'X')
                                    ok = false;
                        return ok;
                    }
                    IBrush chooseColor(int q, int l)
                    {
                        var compat = isCompatible(q, l);
                        if (compat)
                        {
                            var anotherHasUsed = false;
                            for (int i = 0; i <= 8; i++)
                                if (i != dungeonTabs.SelectedIndex)
                                {
                                    if (TrackerModel.GetDungeon(i).LabelChar == l.ToString()[0])
                                        anotherHasUsed = true;
                                    var usedState = currentOutlineDisplayState[i];
                                    var usedLevel = usedState > 9 ? usedState - 9 : usedState;
                                    if (usedLevel == l)
                                        anotherHasUsed = true;
                                }
                            return anotherHasUsed ? Brushes.Yellow : Brushes.Lime;
                        }
                        else
                            return Brushes.Red;
                    }
                    var pos = outlineDrawingCanvases[SI].TranslatePoint(new Point(), cm.AppMainCanvas).Value;
                    var tileCanvas = new Canvas { Width = (double)(39 * 8 + 12 * 7), Height = (double)(TH + 27 * 8 + 12 * 7) };
                    var gridElementsSelectablesAndIDsList = new List<(Control, bool, int)>();
                    gridElementsSelectablesAndIDsList.Add(((Control)mkTxt("none", Brushes.Lime), true, 0));
                    for (int l = 1; l <= 9; l++)
                        gridElementsSelectablesAndIDsList.Add(((Control)mkTxt($"1Q{l}", chooseColor(1, l)), true, l));
                    gridElementsSelectablesAndIDsList.Add(((Control)mkTxt("none", Brushes.Lime), true, 0));    // two 'none's just to make the grid look nicer
                    for (int l = 1; l <= 9; l++)
                        gridElementsSelectablesAndIDsList.Add(((Control)mkTxt($"2Q{l}", chooseColor(2, l)), true, l + 9));
                    var gridElementsSelectablesAndIDs = gridElementsSelectablesAndIDsList.ToArray();
                    var originalStateIndex =
                        currentOutlineDisplayState[SI] >= 10
                            ? 1 + currentOutlineDisplayState[SI]   // skip over the extra 'none'
                            : currentOutlineDisplayState[SI];
                    var activationDelta = 0;
                    var (gnc, gnr, gcw, grh) = (5, 4, 50, 30);
                    var (gx, gy) = (tileCanvas.Width + ST, 0.0);
                    void redrawTile(int state) =>
                        doVanillaOutlineRedraw(tileCanvas, state);
                    CustomComboBoxes.PopupClickBehavior<int> onClick(PointerPressedEventArgs _ea, int state) => new CustomComboBoxes.PopupClickBehavior<int>.DismissPopupWithResult(state);
                    var extraDecorations = new (Control, double, double)[] {
                        ((Control)new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(3.0), Child =
                            new TextBox { FontSize = 15.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), Margin = new Thickness(3.0),
                                        Text = "Choose a vanilla dungeon outline to draw\non this dungeon map tab.\n\n" +
                                                "Green selections are compatible with your\ncurrently marked rooms. Yellow selections\n" +
                                                "are compatible, but indicate a dungeon\nnumber already in use by another tab.\n\n" +
                                                "Choose 'none' to remove outline." }
                            }, (double)gx, (double)gnr * (2.0 * ST + (double)grh) + 2.0 * ST)
                        };
                    var brushes = CustomComboBoxes.ModalGridSelectBrushes.Defaults();
                    outlineDrawingCanvases[SI].Children.Clear();  // remove current outline; the tileCanvas is transparent, and seeing the old one is bad. restored later
                    async Task work()
                    {
                        var r = await CustomComboBoxes.DoModalGridSelect<int, int>(cm, pos.X, pos.Y, tileCanvas, gridElementsSelectablesAndIDs, originalStateIndex, activationDelta, (gnc, gnr, gcw, grh),
                                        (double)gcw / 2.0, (double)grh / 2.0, gx, gy, redrawTile, onClick, extraDecorations, brushes, new CustomComboBoxes.GridClickDismissalWarpReturn.NoWarp(), null, "VanillaDungeonOutline", null);
                        switch (r)
                        {
                            case (true, var state):
                                currentOutlineDisplayState[SI] = state;
                                doVanillaOutlineRedraw(outlineDrawingCanvases[SI], currentOutlineDisplayState[SI]);
                                if (TrackerModel.GetDungeon(SI).LabelChar == '?')
                                {
                                    if (state >= 1 && state <= 8)
                                        TrackerModel.GetDungeon(SI).LabelChar = (char)((int)'0' + state);
                                    if (state >= 10 && state <= 17)
                                        TrackerModel.GetDungeon(SI).LabelChar = (char)((int)'1' + state - 10);
                                }
                                var appearsToBe2qFrontHalf = false;
                                for (int i = 0; i <= 7; i++)
                                    if (currentOutlineDisplayState[i] == 10 || currentOutlineDisplayState[i] == 13)   // 2Q1 or 2Q4
                                        appearsToBe2qFrontHalf = true;
                                TrackerModel.IsSecondQuestDungeons = appearsToBe2qFrontHalf;
                                OptionsMenu.triggerSecondQuestDungeonsOptionChanged();
                                break;
                            case (false, _): break;
                        }
                        CustomComboBoxes.GlobalFlag.popupIsActive = false;
                    }
                    CustomComboBoxes.StartImmediate(work());
                }
            };
        }
        else
        {
            var fqcb = new Button { Content = new TextBox { Text = "1Q", FontSize = 12.0, Background = Brushes.Black, Foreground = Brushes.Orange, BorderThickness = new Thickness(0.0), IsReadOnly = true, IsHitTestVisible = false, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center }, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            ToolTip.SetTip(fqcb, "Show vanilla first quest dungeon outlines");
            var sqcb = new Button { Content = new TextBox { Text = "2Q", FontSize = 12.0, Background = Brushes.Black, Foreground = Brushes.Orange, BorderThickness = new Thickness(0.0), IsReadOnly = true, IsHitTestVisible = false, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center }, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            ToolTip.SetTip(sqcb, "Show vanilla second quest dungeon outlines");
            foreach (var button in new[] { fqcb, sqcb })
            {
                button.PointerEntered += (_, _) => isHoveringFQSQ.Value = true;
                button.PointerExited += (_, _) => isHoveringFQSQ.Value = false;
            }
            void turnOnQuestMap(int SI, int delta)
            {
                currentOutlineDisplayState[SI] = SI + delta;
                doVanillaOutlineRedraw(outlineDrawingCanvases[SI], currentOutlineDisplayState[SI]);
            }
            void turnOffQuestMap(int SI)
            {
                outlineDrawingCanvases[SI].Children.Clear();
                currentOutlineDisplayState[SI] = 0;
            }
            void pressQuestButton(int SI, bool isfq)
            {
                var delta = isfq ? 1 : 10;
                if (currentOutlineDisplayState[SI] != SI + delta) turnOnQuestMap(SI, delta);
                else turnOffQuestMap(SI);
            }
            fqcb.Click += (_, _) =>
            {
                var SI = dungeonTabs.SelectedIndex;
                if (SI == 9)                                        // pressing FQ on summary tab turns them all on or all off
                {
                    if (currentOutlineDisplayState[0] != 1)
                        for (int d = 0; d <= 8; d++) turnOnQuestMap(d, 1);
                    else
                        for (int d = 0; d <= 8; d++) turnOffQuestMap(d);
                }
                else
                {
                    if (SI < 6)    // update front half
                        for (int i = 0; i <= 5; i++)
                            pressQuestButton(i, true);
                    else            // update back half
                        for (int i = 6; i <= 8; i++)
                            pressQuestButton(i, true);
                }
            };
            canvasAdd(dungeonTabsWholeCanvas, fqcb, 402.0, 3.0);
            sqcb.Click += (_, _) =>
            {
                var SI = dungeonTabs.SelectedIndex;
                if (SI == 9)                                        // pressing SQ on summary tab turns them all on or all off
                {
                    if (currentOutlineDisplayState[0] != 10)
                        for (int d = 0; d <= 8; d++) turnOnQuestMap(d, 10);
                    else
                        for (int d = 0; d <= 8; d++) turnOffQuestMap(d);
                }
                else
                {
                    if (SI < 6)    // update front half
                        for (int i = 0; i <= 5; i++)
                            pressQuestButton(i, false);
                    else            // update back half
                        for (int i = 6; i <= 8; i++)
                            pressQuestButton(i, false);
                }
            };
            canvasAdd(dungeonTabsWholeCanvas, sqcb, 426.0, 3.0);
        }

        var levelTabs = new TabItem[9];
        var contentCanvasesWithBadYOffsets = new Canvas[9];
        var contentCanvases = new Canvas[9];
        var dummyCanvas = new Canvas { Opacity = 0.0001, IsHitTestVisible = false };  // a kludge to help work around TabControl unloading tabs when not selected
        var localDungeonTrackerPanelWidth = 42.0;
        //let exportFunctions = Array.create 9 (fun () -> new DungeonSaveAndLoad.DungeonModel())
        //let importFunctions = Array.create 9 (fun _ -> ())
        //let isFirstTimeClickingAnyRoom = Array.init 9 (fun _ -> new TrackerModel.Eventing<bool>(true))
        TextBox makeNumeral(char labelChar)
        {
            var n = new TextBox { Foreground = Brushes.Magenta, Background = Brushes.Transparent, Text = $"{labelChar}", IsReadOnly = true, IsHitTestVisible = false, FontSize = 200.0, Opacity = 0.25,
                            Height = (double)(27 * 8 + 12 * 7), Width = (double)(39 * 8 + 12 * 7), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.Bold,
                            HorizontalContentAlignment = HorizontalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0), Padding = new Thickness(0.0) };
            Graphics.matchWpfTextTop(n);   // (non-Windows fonts draw the digit too high; see PROGRESS.md)
            return n;
        }
        char getLabelChar(int level) => level == 9 ? '9' : TrackerModel.IsHiddenDungeonNumbers.Invoke(null) ? (char)((int)'A' - 1 + level) : (char)((int)'0' + level);
        //let mutable justUnmarked = None
        (int, int, HotKeys.DungeonRoomHotKey)? justUnmarked = null;
        // (F#: these local choice1..choice4 build a Choice<RoomType, MonsterDetail, FloorDropDetail, obj>, boxing the door response; in the port both they and
        // the module-level ones build a HotKeys.DungeonRoomHotKey, which compares by value the same way)
        HotKeys.DungeonRoomHotKey choice1(RoomType v) => new HotKeys.DungeonRoomHotKey.OfRoomType(v);
        HotKeys.DungeonRoomHotKey choice2(MonsterDetail v) => new HotKeys.DungeonRoomHotKey.OfMonsterDetail(v);
        HotKeys.DungeonRoomHotKey choice3(FloorDropDetail v) => new HotKeys.DungeonRoomHotKey.OfFloorDropDetail(v);
        HotKeys.DungeonRoomHotKey choice4(DoorHotKeyResponse v) => new HotKeys.DungeonRoomHotKey.OfDoor(v);
        for (int level_ = 1; level_ <= 9; level_++)
        {
            var level = level_;
            var levelTab = new TabItem { Background = Brushes.Black, Foreground = Brushes.Black };
            dungeonTabs.Items.Add(levelTab);
            levelTabs[level - 1] = levelTab;
            var labelChar = getLabelChar(level);
            var header = new TextBox { Width = 13.0, Background = Brushes.Black, Foreground = Brushes.White, Text = $"{labelChar}", IsReadOnly = true, IsHitTestVisible = false,
                                        HorizontalContentAlignment = HorizontalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0), Padding = new Thickness(0.0) };
            var baitMeatImage = Graphics.BMPtoImage(Graphics.bait_bmp);
            var baitMeatCheckmarkHeaderCanvas = new Canvas { Opacity = 0.0 };
            var bombUpgradeMarkHeaderCanvas = new Canvas { Width = 5.0, Height = 5.0, Background = Brushes.DodgerBlue, Opacity = 0.0 };
            var oldManUnreadHeaderCanvas = new Canvas { Width = 5.0, Height = 5.0, Background = Brushes.Red, Opacity = 0.0 };
            {
                // bait meat
                var baitMeatHeaderCanvas = new Canvas { Width = 8.0, Height = 16.0, ClipToBounds = true };
                baitMeatImage.Opacity = 0.0;
                baitMeatImage.Stretch = Stretch.Uniform;
                baitMeatImage.Height = 14.0;
                canvasAdd(baitMeatHeaderCanvas, baitMeatImage, -6.0, 1.0);
                // checkmark over bait meat
                var line1 = new Line { StartPoint = new Point(1.0, 10.0), EndPoint = new Point(4.0, 13.0), Stroke = Brushes.Lime, StrokeThickness = 2.5 };
                baitMeatCheckmarkHeaderCanvas.Children.Add(line1);
                var line2 = new Line { StartPoint = new Point(4.0, 13.0), EndPoint = new Point(8.0, 4.0), Stroke = Brushes.Lime, StrokeThickness = 2.5 };
                baitMeatCheckmarkHeaderCanvas.Children.Add(line2);
                canvasAdd(baitMeatHeaderCanvas, baitMeatCheckmarkHeaderCanvas, 0.0, 0.0);
                // BU/OM dots
                var headerSp = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Black, Width = 28.0 };
                headerSp.Children.Add(baitMeatHeaderCanvas);
                headerSp.Children.Add(header);
                var headerInfo = new StackPanel { Orientation = Orientation.Vertical, Width = 7.0 };
                headerInfo.Children.Add(new Canvas { Width = 3.0, Height = 2.0 });
                headerInfo.Children.Add(bombUpgradeMarkHeaderCanvas);
                headerInfo.Children.Add(new Canvas { Width = 3.0, Height = 2.0 });
                headerInfo.Children.Add(oldManUnreadHeaderCanvas);
                headerSp.Children.Add(headerInfo);
                headerSp.PointerEntered += (_, _) => contentCanvasMouseEnterFunc(level);
                headerSp.PointerExited += (_, _) => contentCanvasMouseLeaveFunc(level);
                levelTab.Header = headerSp;
            }
            onEvent(TrackerModel.GetDungeon(level - 1).HiddenDungeonColorOrLabelChanged, t =>
            {
                var (color, _) = t;
                header.Background = Graphics.freeze(new SolidColorBrush(Graphics.makeColor(color)));
                header.Foreground = Graphics.isBlackGoodContrast(color) ? Brushes.Black : Brushes.White;
            });
            var tileSunglasses = 0.75;
            var contentCanvasWithBadYOffset = new Canvas { Height = (double)(TH + 3 + 27 * 8 + 12 * 7 + 3), Width = (double)(3 + 39 * 8 + 12 * 7 + 3) + localDungeonTrackerPanelWidth, Background = Brushes.Black };
            levelTab.Content = contentCanvasWithBadYOffset;
            var contentCanvas = new Canvas { Width = contentCanvasWithBadYOffset.Width, Height = contentCanvasWithBadYOffset.Height, Background = Brushes.Black };
            contentCanvas.ClipToBounds = true;  // TODO does this have any bad side effects? useful so that 'midi' works properly, but suggests something was drawing off to the right
            if (false) // we don't do this now; a SelectionChanged will do it later during startup, and doing it twice throws an exception
#pragma warning disable CS0162 // (unreachable, as in WPF)
                canvasAdd(contentCanvasWithBadYOffset, contentCanvas, 0.0, 1.0);   // so that all content is at multiples of 3, so that 2/3 view does not look blurry
#pragma warning restore CS0162
            // rupee/blank/key/bomb row highlighter
            Action<int?> make_highlightRow()
            {
                var bmp = Dungeon.MakeLoZMinimapDisplayBmp(new bool[8, 8], '?');
                var rupeeKeyBomb = new System.Drawing.Bitmap(8, 32);
                for (int i = 0; i <= 7; i++)
                    for (int j = 0; j <= 31; j++)
                        rupeeKeyBomb.SetPixel(i, j, bmp.GetPixel(72 + i, 8 + j));
                var img = Graphics.BMPtoImage(rupeeKeyBomb);
                img.Width = 16.0;
                img.Height = 64.0;
                img.Stretch = Stretch.UniformToFill;
                RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None);
                canvasAdd(contentCanvas, img, contentCanvas.Width - 20.0, 3.0);
                var rowHighlightGrid = Graphics.makeGrid(1, 8, 20, 8);
                var rowHighlighter = new Canvas { Width = 16.0, Height = 8.0, Background = Brushes.Gray, Opacity = 0.0 };
                Graphics.gridAdd(rowHighlightGrid, rowHighlighter, 0, 0);
                canvasAdd(contentCanvas, rowHighlightGrid, contentCanvas.Width - 40.0, 3.0);
                void highlightRow(int? rowOpt)
                {
                    if (rowOpt is int r) { rowHighlighter.Opacity = 1.0; Grid.SetRow(rowHighlighter, r); }
                    else rowHighlighter.Opacity = 0.0;
                }
                return highlightRow;
            }
            var highlightRow = make_highlightRow();
            var oldManCount = 0;
            var oldManCountTB = new TextBox { IsHitTestVisible = false, IsReadOnly = true, BorderThickness = new Thickness(0.0), FontSize = 16.0, Margin = new Thickness(0.0), Width = 45.0,
                                            HorizontalContentAlignment = HorizontalAlignment.Left, Foreground = Brushes.Orange, Background = Brushes.Transparent, Focusable = false };
            var oldManBorder = new Canvas { Width = 44.0, Height = 16.0, Background = Brushes.Black };
            canvasAdd(oldManBorder, Graphics.BMPtoImage(Graphics.old_man_bmp), 2.0, 0.0);
            canvasAdd(oldManBorder, oldManCountTB, 18.0, -3.0);
            ToolTip.SetTip(oldManBorder, "'Old Man Count' - the number of 'old men'\n(NPC-with-hint/Bomb-Upgrade/Hungry-Goriya/\nLife-or-Money) rooms you have marked, and\nthe total number expected in this dungeon");
            setToolTipShowDuration(oldManBorder, 8000);
            void updateOldManCountText()
            {
                if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
                {
                    if (TrackerModel.GetDungeon(level - 1).LabelChar != '?')
                    {
                        var i = (int)(TrackerModel.GetDungeon(level - 1).LabelChar) - (int)('1');
                        oldManCountTB.Text = $"{oldManCount}/{TrackerModel.GetOldManCount(i)}";
                    }
                    else
                        oldManCountTB.Text = $"{oldManCount}";
                }
                else
                    oldManCountTB.Text = $"{oldManCount}/{TrackerModel.GetOldManCount(level - 1)}";
            }
            updateOldManCountText();
            OptionsMenu.secondQuestDungeonsOptionChanged += () => updateOldManCountText();
            if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
                onEvent(TrackerModel.GetDungeon(level - 1).HiddenDungeonColorOrLabelChanged, _ => updateOldManCountText());
            canvasAdd(contentCanvas, oldManBorder, contentCanvas.Width - 44.0, 69.0);
            // local dungeon tracker
            var (LD_X, LD_Y) = (contentCanvas.Width - localDungeonTrackerPanelWidth, 90.0);
            var pos = new Point(0.0 + LD_X, posYF() + LD_Y);  // appMainCanvas coords where the local tracker panel will be placed
            Canvas localDungeonTrackerPanel = null;
            Func<Point> localDungeonTrackerPanelPosToCursorRightToF = () => new Point();
            var boxViewsAndActivations = new (Canvas, Action<int, CustomComboBoxes.GridClickDismissalWarpReturn>)[0];
            {
                void PopulateLocalDungeonTrackerPanel()
                {
                    Point posDestinationWhenMoveCursorLeft() => contentCanvas.TranslatePoint(new Point(3.0 + 39.0 * 7.5 + 12.0 * 7.0, (double)(TH) + 3.0 + 27.0 * 4.5 + 12.0 * 4.0), cm.AppMainCanvas).Value;
                    var (ldtp, unhighlight, posIn, bvaa) =
                        MakeLocalTrackerPanel(cm, pos, tileSunglasses, level, (level == 9 ? null : mainTrackerGhostbusters[level - 1]), posDestinationWhenMoveCursorLeft);
                    boxViewsAndActivations = bvaa;
                    localDungeonTrackerPanelPosToCursorRightToF = posIn;
                    if (localDungeonTrackerPanel != null)
                        contentCanvas.Children.Remove(localDungeonTrackerPanel);  // remove old one
                    localDungeonTrackerPanel = ldtp;
                    canvasAdd(contentCanvas, localDungeonTrackerPanel, LD_X, LD_Y); // add new one
                    // don't remove the old 'unhighlight' event listener - this is technically a leak, but unless you toggle '2nd quest dungeons' button a million times in one session, it won't matter
                    dungeonTabs.SelectionChanged += (_, _) => unhighlight();  // an extra safeguard, since the highlight is not a popup, but just a crazy mark over the main canvas
                }
                OptionsMenu.secondQuestDungeonsOptionChanged += () => PopulateLocalDungeonTrackerPanel();
                if (level == 2)
                    onEvent(TrackerModel.level2StairBoxEnabledChanged.Publish, _ => PopulateLocalDungeonTrackerPanel());
                if (level == 9)
                    onEvent(TrackerModel.level9FloorBoxEnabledChanged.Publish, _ => PopulateLocalDungeonTrackerPanel());
                PopulateLocalDungeonTrackerPanel();
            }
            // main dungeon content
            contentCanvas.PointerEntered += (_, _) => contentCanvasMouseEnterFunc(level);
            contentCanvas.PointerExited += (_, ea) =>
            {
                // only hide the locator if we "actually" left
                // MouseLeave fires
                //  - when we click a room, redraw() causes the element we are Entered to be removed from the tree and replaced by another, even though we never left the bounds of the enclosing canvas
                //  - when we right-click a room, as the mouse is warped outside the canvas in the popup
                // avoid spuriously changing the overworld locator (which is expensive and visually distracting) in these cases
                var pos = ea.GetPosition(contentCanvas);
                if ((pos.X < 0.0 || pos.X > contentCanvas.Bounds.Width) || (pos.Y < 0.0 || pos.Y > contentCanvas.Bounds.Height))
                    if (cm.PopupCanvasStack.Count == 0)
                        contentCanvasMouseLeaveFunc(level);
            };
            var dungeonCanvas = new Canvas { Height = (double)(TH + 27 * 8 + 12 * 7), Width = (double)(39 * 8 + 12 * 7), Background = Brushes.Black };
            var dungeonHeaderCanvas = new Canvas { Height = (double)(TH), Width = (double)(39 * 8 + 12 * 7) };         // draw e.g. BOARD-5 here
            dungeonHeaderCanvas.ClipToBounds = true;
            canvasAdd(dungeonCanvas, dungeonHeaderCanvas, 0.0, 0.0);
            var dungeonBodyCanvas = new Canvas { Height = (double)(27 * 8 + 12 * 7), Width = (double)(39 * 8 + 12 * 7), Background = Brushes.Black };  // draw e.g. rooms here; has background color to be a surface to see all mouse interactions
            // Clip: allow e.g. mouse highlight box slightly off left edge, or gleeok head slightly off top, but don't allow e.g. OFF to go way over right edge into side panel etc.
            dungeonBodyCanvas.Clip = new RectangleGeometry(new Rect(-3.0, -3.0, dungeonBodyCanvas.Width + 6.0, dungeonBodyCanvas.Height + 6.0));
            canvasAdd(dungeonCanvas, dungeonBodyCanvas, 0.0, (double)TH);
            var dungeonBodyHighlightCanvas = new Canvas { Height = (double)(27 * 8 + 12 * 7), Width = (double)(39 * 8 + 12 * 7) };  // draw e.g. blocker highlights here
            dungeonBodyHighlightCanvas.ClipToBounds = true;
            canvasAdd(dungeonCanvas, dungeonBodyHighlightCanvas, 0.0, (double)TH);
            var numeral = makeNumeral(labelChar);
            void showNumeral()
            {
                numeral.Opacity = 0.7;
                // (F#: Async.Sleep(1000) on the thread pool, then SwitchToContext; awaiting Task.Delay resumes on the UI thread)
                async Task work()
                {
                    await Task.Delay(1000);
                    numeral.Opacity = isFirstTimeClickingAnyRoom[level - 1].Value ? 0.25 : 0.0;
                }
                CustomComboBoxes.StartImmediate(work());
            }
            var dungeonSourceHighlightCanvas = new Canvas { Height = (double)(TH + 27 * 8 + 12 * 7), Width = (double)(39 * 8 + 12 * 7) };  // draw grab-source highlights here
            var dungeonHighlightCanvas = new Canvas { Height = (double)(TH + 27 * 8 + 12 * 7), Width = (double)(39 * 8 + 12 * 7) };  // draw grab highlights here
            canvasAdd(contentCanvas, dungeonCanvas, 3.0, 3.0);
            canvasAdd(contentCanvas, dungeonSourceHighlightCanvas, 3.0, 3.0);
            canvasAdd(contentCanvas, dungeonHighlightCanvas, 3.0, 3.0);
            // level 9 only: dashed borders around the 4x4-room quadrant that Ganon (light blue) and Zelda (pink) are in
            var gzBorderCanvas = new Canvas { Width = (double)(39 * 8 + 12 * 7), Height = (double)(TH + 27 * 8 + 12 * 7), IsHitTestVisible = false };
            canvasAdd(contentCanvas, gzBorderCanvas, 3.0, 3.0);
            void redrawGanonZeldaBorders()
            {
                gzBorderCanvas.Children.Clear();
                // quadrant: 0=NW, 1=NE, 2=SE, 3=SW
                foreach (var (q, color, dashOffset) in new[] { (ganonQuadrant, Color.FromRgb(120, 200, 255), 0.0), (zeldaQuadrant, Color.FromRgb(255, 120, 200), 3.0) })
                {
                    if (q >= 0)
                    {
                        var col0 = (q == 1 || q == 2) ? 4 : 0;
                        var row0 = (q == 2 || q == 3) ? 4 : 0;
                        var (w, h) = ((double)(4 * 39 + 3 * 12), (double)(4 * 27 + 3 * 12));
                        var r = new Rectangle { Width = w + 8.0, Height = h + 8.0, Stroke = new SolidColorBrush(color), StrokeThickness = 2.5, StrokeDashArray = new AvaloniaList<double>(new[] { 3.0, 3.0 }), StrokeDashOffset = dashOffset, IsHitTestVisible = false };
                        canvasAdd(gzBorderCanvas, r, (double)(col0 * (39 + 12)) - 4.0, (double)(TH + row0 * (27 + 12)) - 4.0);
                    }
                }
            }
            if (level == 9)
            {
                redrawGanonZeldaBorders();
                // half Ganon, half Zelda icon above the MOVE button: click to say which quadrant each of them is in
                System.Drawing.Bitmap make_halfBmp()
                {
                    var r = new System.Drawing.Bitmap(21, 21);
                    for (int x = 0; x <= 20; x++)
                        for (int y = 0; y <= 20; y++)
                            r.SetPixel(x, y, (x <= 10 ? Graphics.ganon_bmp : Graphics.zelda_bmp).GetPixel(x, y));
                    return r;
                }
                var halfBmp = make_halfBmp();
                // the button's border is dashed light blue on the Ganon (left) half and dashed pink on the Zelda (right) half
                var (bw, bh) = ((double)(13 * 3), (double)(TH + 1));
                var gzFace = new Canvas { Width = bw, Height = bh, Background = Brushes.Black };
                var gzImg = Graphics.BMPtoImage(halfBmp);
                canvasAdd(gzFace, gzImg, (bw - 21.0) / 2.0, (bh - 21.0) / 2.0);
                Polyline halfBorder(Color color, (double, double)[] pts) =>
                    new Polyline { Points = pts.Select(p => new Point(p.Item1, p.Item2)).ToList(), Stroke = new SolidColorBrush(color), StrokeThickness = 2.0,
                                        StrokeDashArray = new AvaloniaList<double>(new[] { 3.0, 2.0 }), IsHitTestVisible = false };
                var m = 1.0;
                gzFace.Children.Add(halfBorder(Color.FromRgb(120, 200, 255), new[] { (bw / 2.0, m), (m, m), (m, bh - m), (bw / 2.0, bh - m) }));
                gzFace.Children.Add(halfBorder(Color.FromRgb(255, 120, 200), new[] { (bw / 2.0, m), (bw - m, m), (bw - m, bh - m), (bw / 2.0, bh - m) }));
                var gzButton = new Button { Width = bw, Height = bh, Content = gzFace, Background = Brushes.Black, BorderThickness = new Thickness(0.0), Margin = new Thickness(0.0), Padding = new Thickness(0.0), Focusable = false };
                Control bigIcon(System.Drawing.Bitmap bmp)
                {
                    var im = Graphics.BMPtoImage(bmp);
                    im.Width = 42.0;
                    im.Height = 42.0;
                    im.Stretch = Stretch.Uniform;
                    return im;
                }
                Control dirText(string t) => new TextBlock { Text = t, Foreground = Brushes.Orange, FontSize = 24.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                gzButton.Click += (_, _) =>
                {
                    if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                    {
                        CustomComboBoxes.GlobalFlag.popupIsActive = true;
                        async Task work()
                        {
                            var who = await PieMenus.IconChoiceBoxAsync(cm, "", new[] { ("ganon", bigIcon(Graphics.ganon_bmp)), ("zelda", bigIcon(Graphics.zelda_bmp)) });
                            if (!(who == null))
                            {
                                var dir = await PieMenus.IconChoiceBoxAsync(cm, "", new[] { ("NW", dirText("NW")), ("NE", dirText("NE")), ("SE", dirText("SE")), ("SW", dirText("SW")), ("none", dirText("Clear")) });
                                if (!(dir == null))
                                {
                                    var q = dir switch { "NW" => 0, "NE" => 1, "SE" => 2, "SW" => 3, _ => -1 };
                                    if (who == "ganon") ganonQuadrant = q; else zeldaQuadrant = q;
                                    redrawGanonZeldaBorders();
                                }
                            }
                            CustomComboBoxes.GlobalFlag.popupIsActive = false;
                        }
                        CustomComboBoxes.StartImmediate(work());
                    }
                };
                canvasAdd(contentCanvas, gzButton, LD_X + 1.5, LD_Y + 168.0 - (double)(TH + 1) - 1.0);
            }

            contentCanvasesWithBadYOffsets[level - 1] = contentCanvasWithBadYOffset;
            contentCanvases[level - 1] = contentCanvas;
            if (level == 1) // just set this once
                dungeonTabs.Height = contentCanvas.Height + 30.0;

            // doors, rooms, and dragging prep
            var skipRedrawInsideCurrentImport = false;
            var redrawAllDoorFuncs = new List<Action>();
            void redrawAllDoors() { if (!skipRedrawInsideCurrentImport) foreach (var f in redrawAllDoorFuncs) f(); }
            var roomRedrawFuncs = new List<Action>(64);
            void redrawAllRooms() { if (!skipRedrawInsideCurrentImport) foreach (var f in roomRedrawFuncs) f(); }
            redrawAllRoomsPublic = redrawAllRooms;
            var roomCanvas = new Canvas();
            Point centerOf(double x, double y) => // center of room at x,y; but can be floats so e.g. x+0.5,y is an adjacent door
                roomCanvas.TranslatePoint(new Point(x * 51.0 + 13.0 * 3.0 / 2.0, y * 39.0 + 9.0 * 3.0 / 2.0), cm.AppMainCanvas).Value;
            Action showMinimaps = () => { };
            Action invertOffTheMapWithUnmarked = () => { };
            var hasEverInvertedOrDragged = false;
            var roomDragDrop = new Graphics.DragDropSurface<string>(dungeonBodyCanvas, (ea, initiatorFunc) =>
            {
                var whichButtonStr = "";
                // (WPF: ea.LeftButton = Pressed etc. on the mouse-move args, and Keyboard.IsKeyDown(Left/RightShift); Avalonia reads both from the event)
                var props = ea.GetCurrentPoint(dungeonBodyCanvas).Properties;
                var shiftIsDown = ea.KeyModifiers.HasFlag(KeyModifiers.Shift);
                if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                {
                    // drag and drop to quickly 'paint' rooms
                    if (!grabHelper.IsGrabMode)  // cannot initiate a drag in grab mode
                    {
                        if (props.IsLeftButtonPressed)
                        {
                            if (shiftIsDown)
                                whichButtonStr = "M";  // shift-left-click behaves like middle-click
                            else
                                whichButtonStr = "L";
                        }
                        else if (props.IsRightButtonPressed)
                        {
                            if (shiftIsDown)
                            { }  // shift-right-click is not an action
                            else
                                whichButtonStr = "R";
                        }
                        else if (props.IsMiddleButtonPressed)
                            whichButtonStr = "M";
                    }
                }
                if (whichButtonStr != "")
                {
                    if (whichButtonStr == "L" && !(hasEverInvertedOrDragged) && TrackerModelOptions.LeftClickDragAutoInverts.Value)
                        invertOffTheMapWithUnmarked();  // left-drag will auto-invert, to start painting on-map locations to an off-the-map background
                    hasEverInvertedOrDragged = true; // you might right drag to unpaint, and unpaint too much, and then left drag to paint back in, don't want to invert then
                    initiatorFunc(whichButtonStr);
                    if (whichButtonStr == "R" || whichButtonStr == "L")
                    {
                        // make OffTheMap rooms show a 'grid' to make it easier to draw in empty space
                        DungeonRoomState.isDoingDragPaintOffTheMap = true;
                        redrawAllRooms();
                    }
                    showMinimaps();
                    // (WPF: DragDrop.DoDragDrop(roomCanvas, whichButtonStr, DragDropEffects.Link), which blocks until the drop. Avalonia's DoDragDropAsync returns a Task
                    // (on macOS the OS drag runs asynchronously), so a nested dispatcher frame waits for it, as CustomMessageBox.ShowDialogBlocking does; see PROGRESS.md)
                    var data = new DataTransfer();
                    data.Add(DataTransferItem.CreateText(whichButtonStr));
                    var dragTask = DragDrop.DoDragDropAsync(ea, data, DragDropEffects.Link);
                    if (!dragTask.IsCompleted)
                    {
                        var frame = new DispatcherFrame();
                        dragTask.ContinueWith(_ => Dispatcher.UIThread.Post(() => frame.Continue = false));
                        Dispatcher.UIThread.PushFrame(frame);
                    }
                    // DoDragDrop is blocking, so we can do post-drop effects here
                    DungeonRoomState.isDoingDragPaintOffTheMap = false;
                    redrawAllRooms();
                    redrawAllDoors();
                    rightwardCanvas.Children.Clear(); // remove the shown minimaps
                }
            });
            // some room stuff that doors need to know about
            var highlightBrush = Dungeon.highlight;   // (F#: `highlight`; renamed because C# doesn't allow the room loop's `highlight` function below to shadow it)
            var roomHighlightOutline = new Rectangle { Width = (double)(13 * 3) + 4.0, Height = (double)(9 * 3) + 4.0, Stroke = highlightBrush, StrokeThickness = 1.5, Fill = Brushes.Transparent, IsHitTestVisible = false, Opacity = 0.0 };
            // doors
            var (LL, RR, UU, DD) = (HotKeys.GlobalHotkeyTargets.MoveCursorLeft, HotKeys.GlobalHotkeyTargets.MoveCursorRight,
                                    HotKeys.GlobalHotkeyTargets.MoveCursorUp, HotKeys.GlobalHotkeyTargets.MoveCursorDown);
            void installDoorBehavior(Dungeon.Door door, Canvas doorCanvas, (int, int, HotKeys.GlobalHotkeyTargets) a, (int, int, HotKeys.GlobalHotkeyTargets) b)
            {
                var (ai, aj, adir) = a;
                var (bi, bj, _bdir) = b;
                if (adir != RR && adir != DD)
                    throw new Exception("must be called with RR,LL or DD,UU in that order");
                // (WPF: the surface's MouseUp args, `ChangedButton`; Avalonia: the release's InitialPressMouseButton. Shift is read from the event, not the global keyboard)
                roomDragDrop.RegisterClickable(doorCanvas, ea =>
                {
                    if (!grabHelper.IsGrabMode)  // cannot interact with doors in grab mode
                    {
                        var shiftIsDown = ea.KeyModifiers.HasFlag(KeyModifiers.Shift);
                        if (ea.InitialPressMouseButton == MouseButton.Left)
                        {
                            if (shiftIsDown)
                                door.Prev();
                            else
                            {
                                if (door.State != Dungeon.DoorState.YES)
                                    door.State = Dungeon.DoorState.YES;
                                else
                                    door.State = Dungeon.DoorState.UNKNOWN;
                            }
                        }
                        else if (ea.InitialPressMouseButton == MouseButton.Right)
                        {
                            if (shiftIsDown)
                                door.Next();
                            else
                            {
                                if (door.State != Dungeon.DoorState.NO)
                                    door.State = Dungeon.DoorState.NO;
                                else
                                    door.State = Dungeon.DoorState.UNKNOWN;
                            }
                        }
                        else if (ea.InitialPressMouseButton == MouseButton.Middle)
                        {
                            if (door.State != Dungeon.DoorState.YELLOW)
                                door.State = Dungeon.DoorState.YELLOW;
                            else
                                door.State = Dungeon.DoorState.UNKNOWN;
                        }
                        else if (ea.InitialPressMouseButton == MouseButton.XButton1)
                        {
                            // mouse button 4 toggles the purple door
                            if (door.State != Dungeon.DoorState.PURPLE)
                                door.State = Dungeon.DoorState.PURPLE;
                            else
                                door.State = Dungeon.DoorState.UNKNOWN;
                        }
                        ea.Handled = true;
                    }
                }, _ => { });
                doorCanvas.PointerWheelChanged += (s, ea) =>
                {
                    var notch = Graphics.wheelNotch(s, ea);
                    if (notch == 0) return;   // (whole notches only: trackpads send tiny deltas, and sideways scrolling, which WPF's MouseWheel never reports; see PROGRESS.md)
                    if (!grabHelper.IsGrabMode)  // cannot interact with doors in grab mode
                    {
                        if (notch < 0) door.Next(); else door.Prev();
                    }
                };
                doorCanvas.MyKeyAdd(ea =>
                {
                    // two of the arrow keys should still work
                    if (HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key, out var target))
                    {
                        switch (target)
                        {
                            case HotKeys.GlobalHotkeyTargets.MoveCursorRight:
                                ea.Handled = true;
                                if (adir == RR)
                                    Graphics.NavigationallyWarpMouseCursorTo(centerOf((double)bi, (double)bj));
                                break;
                            case HotKeys.GlobalHotkeyTargets.MoveCursorLeft:
                                ea.Handled = true;
                                if (adir == RR)
                                    Graphics.NavigationallyWarpMouseCursorTo(centerOf((double)ai, (double)aj));
                                break;
                            case HotKeys.GlobalHotkeyTargets.MoveCursorDown:
                                ea.Handled = true;
                                if (adir == DD)
                                    Graphics.NavigationallyWarpMouseCursorTo(centerOf((double)bi, (double)bj));
                                break;
                            case HotKeys.GlobalHotkeyTargets.MoveCursorUp:
                                ea.Handled = true;
                                if (adir == DD)
                                    Graphics.NavigationallyWarpMouseCursorTo(centerOf((double)ai, (double)aj));
                                break;
                            default: break;
                        }
                    }
                });
            }
            roomDragDrop.RegisterClickable(dungeonBodyCanvas, _ => { }, _ => { });  // you can start a drag from the empty space between doors/rooms on the canvas
            // horizontal doors
            var unknown = Dungeon.unknown;
            var no = Dungeon.no;
            var yes = Dungeon.yes;
            var yellow = Dungeon.yellow;
            var purple = Dungeon.purple;
            //RPT Test change
            //let horizontalDoors = Array2D.zeroCreate 7 8
            Dungeon.Door[,] make_horizontalDoors()
            {
                TrackerModelOptions.CoopSyncOptions.IsBulkDoorInit = true;
                var doors = new Dungeon.Door[7, 8];
                for (int i_ = 0; i_ < 7; i_++)
                    for (int j_ = 0; j_ < 8; j_++)
                    {
                        var (i, j) = (i_, j_);
                        var currentState = Dungeon.DoorInterop.fromCSharp(CDungeonModelStore.HorizontalDoors[level - 1][j, i]);
                        void redraw(Dungeon.DoorState state) =>
                            triggerDoorChanged(new DoorChangeInfo
                            {
                                Level = level,
                                X = i,
                                Y = j,
                                IsHorizontal = true,
                                NewState = state,
                            });
                        doors[i, j] = new Dungeon.Door(currentState, redraw, level, i, j, true);
                    }
                TrackerModelOptions.CoopSyncOptions.IsBulkDoorInit = false;
                return doors;
            }
            var horizontalDoors = make_horizontalDoors();






            var hDoorHighlightOutline = new Rectangle { Width = 12.0, Height = 16.0, Stroke = highlightBrush, StrokeThickness = 2.0, Fill = Brushes.Transparent, IsHitTestVisible = false, Opacity = 0.0 };
            var hDoorCanvas = new Canvas();  // nesting canvases improves perf
            dungeonBodyCanvas.Children.Add(hDoorCanvas);
            for (int i_ = 0; i_ <= 6; i_++)
                for (int j_ = 0; j_ <= 7; j_++)
                {
                    var (i, j) = (i_, j_);
                    var d = new Canvas { Width = 12.0, Height = 16.0, Background = Brushes.Black };
                    var rect = new Rectangle { Width = 12.0, Height = 15.0, Stroke = unknown, StrokeThickness = 2.0, Fill = unknown };
                    var line = new Line { StartPoint = new Point(6.0, -6.0), EndPoint = new Point(6.0, 21.0), StrokeThickness = 3.0, Stroke = no, Opacity = 0.0 };
                    d.Children.Add(rect);
                    var door = new Dungeon.Door(Dungeon.DoorState.UNKNOWN, newState =>
                    {
                        // Redraw UI
                        switch (newState)
                        {
                            case Dungeon.DoorState.YES: rect.Stroke = yes; rect.Fill = yes; rect.Opacity = 1.0; line.Opacity = 0.0; break;
                            case Dungeon.DoorState.NO: rect.Opacity = 0.0; line.Opacity = 1.0; if (line.Parent == null) d.Children.Add(line); break;
                            case Dungeon.DoorState.YELLOW: rect.Stroke = yellow; rect.Fill = yellow; rect.Opacity = 1.0; line.Opacity = 0.0; break;
                            case Dungeon.DoorState.PURPLE: rect.Stroke = purple; rect.Fill = purple; rect.Opacity = 1.0; line.Opacity = 0.0; break;
                            case Dungeon.DoorState.UNKNOWN:
                                rect.Stroke = unknown; rect.Fill = unknown; rect.Opacity = 1.0; line.Opacity = 0.0;
                                if ((masterRoomStates[level - 1][i, j].RoomType.IsOffMap()) || (masterRoomStates[level - 1][i + 1, j].RoomType.IsOffMap()))
                                {
                                    rect.Stroke = Brushes.Black;
                                    rect.Fill = Brushes.Black;
                                }
                                break;
                        }

                        // Fire door change event
                        triggerDoorChanged(new DoorChangeInfo
                        {
                            Level = level,
                            X = i,
                            Y = j,
                            IsHorizontal = true,
                            NewState = newState,
                        });
                    }, level, i, j, true);

                    horizontalDoors[i, j] = door;
                    horizontalDoorsPerLevel[level - 1][i, j] = door;
                    canvasAdd(hDoorCanvas, d, (double)(i * (39 + 12) + 39), (double)(j * (27 + 12) + 6));
                    installDoorBehavior(door, d, (i, j, RR), (i + 1, j, LL));
                    d.PointerEntered += (_, _) =>
                    {
                        if (!CustomComboBoxes.GlobalFlag.popupIsActive && !grabHelper.IsGrabMode)
                        {
                            Canvas.SetLeft(hDoorHighlightOutline, (double)(i * (39 + 12) + 39));
                            Canvas.SetTop(hDoorHighlightOutline, (double)(j * (27 + 12) + 6));
                            hDoorHighlightOutline.Opacity = Dungeon.highlightOpacity;
                        }
                    };
                    d.PointerExited += (_, _) => hDoorHighlightOutline.Opacity = 0.0;
                    redrawAllDoorFuncs.Add(() => door.Redraw());
                }

            canvasAdd(dungeonBodyCanvas, hDoorHighlightOutline, 0.0, 0.0);
            // vertical doors
            var vDoorHighlightOutline = new Rectangle { Width = 24.0, Height = 12.0, Stroke = highlightBrush, StrokeThickness = 2.0, Fill = Brushes.Transparent, IsHitTestVisible = false, Opacity = 0.0 };
            //RPT replacing below
            Dungeon.Door[,] make_verticalDoors()
            {
                TrackerModelOptions.CoopSyncOptions.IsBulkDoorInit = true;
                var doors = new Dungeon.Door[8, 7];
                for (int i_ = 0; i_ < 8; i_++)
                    for (int j_ = 0; j_ < 7; j_++)
                    {
                        var (i, j) = (i_, j_);
                        var currentState = Dungeon.DoorInterop.fromCSharp(CDungeonModelStore.VerticalDoors[level - 1][j, i]);
                        void redraw(Dungeon.DoorState state) =>
                            triggerDoorChanged(new DoorChangeInfo
                            {
                                Level = level,
                                X = i,
                                Y = j,
                                IsHorizontal = false,
                                NewState = state,
                            });
                        doors[i, j] = new Dungeon.Door(currentState, redraw, level, i, j, false);
                    }
                TrackerModelOptions.CoopSyncOptions.IsBulkDoorInit = false;
                return doors;
            }
            var verticalDoors = make_verticalDoors();




            var vDoorCanvas = new Canvas();  // nesting canvases improves perf
            dungeonBodyCanvas.Children.Add(vDoorCanvas);
            for (int i_ = 0; i_ <= 7; i_++)
                for (int j_ = 0; j_ <= 6; j_++)
                {
                    var (i, j) = (i_, j_);
                    var d = new Canvas { Width = 24.0, Height = 12.0, Background = Brushes.Black };
                    var rect = new Rectangle { Width = 21.0, Height = 12.0, Stroke = unknown, StrokeThickness = 2.0, Fill = unknown };
                    var line = new Line { StartPoint = new Point(-9.0, 6.0), EndPoint = new Point(30.0, 6.0), StrokeThickness = 3.0, Stroke = no, Opacity = 0.0 };
                    d.Children.Add(rect);
                    var door = new Dungeon.Door(Dungeon.DoorState.UNKNOWN, newState =>
                    {
                        // Redraw UI
                        switch (newState)
                        {
                            case Dungeon.DoorState.YES: rect.Stroke = yes; rect.Fill = yes; rect.Opacity = 1.0; line.Opacity = 0.0; break;
                            case Dungeon.DoorState.NO: rect.Opacity = 0.0; line.Opacity = 1.0; if (line.Parent == null) d.Children.Add(line); break;
                            case Dungeon.DoorState.YELLOW: rect.Stroke = yellow; rect.Fill = yellow; rect.Opacity = 1.0; line.Opacity = 0.0; break;
                            case Dungeon.DoorState.PURPLE: rect.Stroke = purple; rect.Fill = purple; rect.Opacity = 1.0; line.Opacity = 0.0; break;
                            case Dungeon.DoorState.UNKNOWN:
                                rect.Stroke = unknown; rect.Fill = unknown; rect.Opacity = 1.0; line.Opacity = 0.0;
                                if (masterRoomStates[level - 1][i, j].RoomType.IsOffMap() || masterRoomStates[level - 1][i, j + 1].RoomType.IsOffMap())
                                {
                                    rect.Stroke = Brushes.Black;
                                    rect.Fill = Brushes.Black;
                                }
                                break;
                        }

                        // 🔁 Fire event here
                        triggerDoorChanged(new DoorChangeInfo
                        {
                            Level = level,
                            X = i,
                            Y = j,
                            IsHorizontal = false,
                            NewState = newState,
                        });
                    }, level, i, j, false);

                    verticalDoors[i, j] = door;
                    verticalDoorsPerLevel[level - 1][i, j] = door;
                    canvasAdd(vDoorCanvas, d, (double)(i * (39 + 12) + 9), (double)(j * (27 + 12) + 27));
                    installDoorBehavior(door, d, (i, j, DD), (i, j + 1, UU));
                    d.PointerEntered += (_, _) =>
                    {
                        if (!CustomComboBoxes.GlobalFlag.popupIsActive && !grabHelper.IsGrabMode)
                        {
                            Canvas.SetLeft(vDoorHighlightOutline, (double)(i * (39 + 12) + 8));
                            Canvas.SetTop(vDoorHighlightOutline, (double)(j * (27 + 12) + 27));
                            vDoorHighlightOutline.Opacity = Dungeon.highlightOpacity;
                        }
                    };
                    d.PointerExited += (_, _) => vDoorHighlightOutline.Opacity = 0.0;
                    redrawAllDoorFuncs.Add(() => door.Redraw());
                }
            canvasAdd(dungeonBodyCanvas, vDoorHighlightOutline, 0.0, 0.0);
            // for room animation, later
            var backRoomHighlightTile = new Rectangle { Width = (double)(13 * 3) + 6.0, Height = (double)(9 * 3) + 6.0, StrokeThickness = 3.0, Opacity = 1.0, IsHitTestVisible = false };
            canvasAdd(dungeonBodyCanvas, backRoomHighlightTile, 0.0, 0.0);
            // rooms
            var roomCanvases = new Canvas[8, 8];
            var roomStates = masterRoomStates[level - 1];
            var roomIsCircled = new bool[8, 8];
            var roomCircles = new Ellipse[8, 8];
            var usedTransports = new int[9]; // slot 0 unused
            void updateHeaderCanvases()
            {
                baitMeatImage.Opacity = 0.0;
                baitMeatCheckmarkHeaderCanvas.Opacity = 0.0;
                bombUpgradeMarkHeaderCanvas.Opacity = 0.0;
                oldManUnreadHeaderCanvas.Opacity = 0.0;
                for (int i = 0; i <= 7; i++)
                    for (int j = 0; j <= 7; j++)
                    {
                        var rs = roomStates[i, j];
                        if (rs.RoomType == RoomType.HungryGoriyaMeatBlock)
                        {
                            baitMeatImage.Opacity = 1.0;
                            if (!(rs.IsComplete))
                                baitMeatCheckmarkHeaderCanvas.Opacity = 0.0;
                            else
                                baitMeatCheckmarkHeaderCanvas.Opacity = 1.0;
                        }
                        if (rs.RoomType == RoomType.BombUpgrade && !(rs.IsComplete))
                            bombUpgradeMarkHeaderCanvas.Opacity = 1.0;
                        if (TrackerModelOptions.BookForHelpfulHints.Value && rs.RoomType == RoomType.OldManHint && !(rs.IsComplete))
                            oldManUnreadHeaderCanvas.Opacity = 1.0;
                    }
                // (WPF: levelTab.InvalidateProperty(TabItem.HeaderProperty), to repaint the header; Avalonia has no InvalidateProperty, and repaints the
                // header's children when their opacity changes anyway)
                levelTab.InvalidateVisual();
            }
            OptionsMenu.bookForHelpfulHintsOptionChanged += () =>
            {
                redrawAllRooms();        // old man rooms might be complete or incomplete, re-color them to show actual backing state
                updateHeaderCanvases();  // and add/remove red dot as appropriate
            };
            // (the "I have the map" checkbox that used to sit here is gone; the MOVE button below takes its place)
            // minimap-draw-er
            var hoverCanvas = new Canvas { Width = 26.0, Height = 26.0, Background = Brushes.Black, IsHitTestVisible = true };
            var minimini = Graphics.BMPtoImage(Dungeon.MakeMiniMiniMapBmp());
            minimini.Width = 24.0;
            minimini.Height = 24.0;
            minimini.Stretch = Stretch.UniformToFill;
            minimini.Margin = new Thickness(1.0);
            var miniBorder = new Border { Child = minimini, BorderThickness = new Thickness(1.0), BorderBrush = Brushes.Gray };
            canvasAdd(hoverCanvas, miniBorder, 0.0, 0.0);
            canvasAdd(contentCanvas, hoverCanvas, LD_X + 8.0, LD_Y + 191.0);
            showMinimaps = () =>
            {
                Image make(bool[,] markedRooms)
                {
                    var bmp = Dungeon.MakeLoZMinimapDisplayBmp(markedRooms, TrackerModel.IsHiddenDungeonNumbers.Invoke(null) ? '?' : (char)(level + (int)'0'));
                    var img = Graphics.BMPtoImage(bmp);
                    img.Width = 240.0;
                    img.Height = 120.0;
                    img.Stretch = Stretch.UniformToFill;
                    RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None);
                    return img;
                }
                bool[,] map(Func<DungeonRoomState, bool> f)
                {
                    var r = new bool[8, 8];
                    for (int x = 0; x < 8; x++) for (int y = 0; y < 8; y++) r[x, y] = f(roomStates[x, y]);
                    return r;
                }
                var normal = make(map(s => !(s.IsEmpty)));
                var i = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black };
                var anyOff = false;
                foreach (var s in roomStates) if (s.RoomType == RoomType.OffTheMap) anyOff = true;
                if (anyOff)
                {
                    var inverse = make(map(s => !(s.RoomType == RoomType.OffTheMap)));
                    i.Children.Add(new TextBox { FontSize = 14.0, Foreground = Brushes.DarkGray, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false,
                                                BorderThickness = new Thickness(0.0), Margin = new Thickness(0.0, 0.0, 0.0, 6.0),
                                                Text = "Full map, minus any Off-The-Map\nmarks you have painted:"
                                                });
                    i.Children.Add(inverse);
                    i.Children.Add(new DockPanel { Height = 2.0 });
                    i.Children.Add(new TextBox { FontSize = 14.0, Foreground = Brushes.DarkGray, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false,
                                                BorderThickness = new Thickness(0.0), Margin = new Thickness(0.0, 6.0, 0.0, 6.0),
                                                Text = "Your room marks:"
                                                });
                }
                i.Children.Add(normal);
                i.Children.Add(new DockPanel { Height = 2.0 });   // this, and a lot of fine-tuning of pixel margins, is to make 2/3 version also look good
                var b = new Border { Child = new Border { Child = i, BorderThickness = new Thickness(10.0, 4.0, 8.0, 8.0), BorderBrush = Brushes.Black }, BorderThickness = new Thickness(2.0), BorderBrush = Brushes.Gray };
                Canvas.SetBottom(b, 0.0);
                Canvas.SetLeft(b, 0.0);
                rightwardCanvas.Children.Clear();
                rightwardCanvas.Children.Add(b);
            };
            hoverCanvas.PointerEntered += (_, _) =>
                showMinimaps();
            hoverCanvas.PointerExited += (_, _) =>
                rightwardCanvas.Children.Clear();
            // toggler to invert Unmarked versus OffTheMap rooms
            {
                var dp = new DockPanel { LastChildFill = true, Background = Brushes.Black };
                var i1 = Graphics.BMPtoImage((Z1R_Tracker.Models.Z1R_TrackerInterop.RoomType.OffTheMap).CompletedBI());
                i1.Stretch = Stretch.Uniform; i1.StretchDirection = StretchDirection.Both; i1.Width = 13.0; i1.Height = System.Double.NaN;
                dp.Children.Add(i1);
                var i2 = Graphics.BMPtoImage(RoomType.Unmarked.CompletedBI());
                i2.Stretch = Stretch.Uniform; i2.StretchDirection = StretchDirection.Both; i2.Width = 13.0; i2.Height = System.Double.NaN;
                dp.Children.Add(i2);
                DockPanel.SetDock(i1, Dock.Left);
                DockPanel.SetDock(i2, Dock.Right);
                var g = new Grid();
                var c = new Canvas { Width = 8.0, Height = 8.0 };
                var slash = new Line { StartPoint = new Point(0.0, 8.0), EndPoint = new Point(8.0, 0.0), Stroke = Brushes.Orange, StrokeThickness = 2.0 };
                c.Children.Add(slash);
                g.Children.Add(c);
                dp.Children.Add(g);
                var b = new Border { Child = dp, BorderThickness = new Thickness(1.0), BorderBrush = Brushes.DarkGray, Width = 40.0, Height = 16.0 };
                b.PointerEntered += (_, _) => b.BorderBrush = Brushes.DarkCyan;
                b.PointerExited += (_, _) => b.BorderBrush = Brushes.DarkGray;
                invertOffTheMapWithUnmarked = () =>
                {
                    hasEverInvertedOrDragged = true;
                    for (int x = 0; x <= 7; x++)
                        for (int y = 0; y <= 7; y++)
                        {
                            if (roomStates[x, y].RoomType.IsNotMarked())
                                roomStates[x, y].RoomType = RoomType.OffTheMap;
                            else if (roomStates[x, y].RoomType == RoomType.OffTheMap)
                                roomStates[x, y].RoomType = RoomType.Unmarked;
                        }
                };
                b.PointerPressed += (_, _) =>
                {
                    if (grabHelper.IsGrabMode)
                    { }
                    else
                    {
                        invertOffTheMapWithUnmarked();
                        redrawAllRooms();
                        redrawAllDoors();
                    }
                };
                canvasAdd(contentCanvas, b, LD_X + 2.0, LD_Y + 191.0 + 32.0);
            }
            // grab button for this tab
            var grabTB = new TextBox { FontSize = (double)(TH - 12), Foreground = Brushes.Gray, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false,
                                    Text = (TrackerModelOptions.OldStyleGrab.Value ? "GRAB" : "MOVE"), BorderThickness = new Thickness(0.0), Margin = new Thickness(0.0), Padding = new Thickness(0.0),
                                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            var grabButton = new Button { Width = (double)(13 * 3), Height = (double)(TH - 3), Content = grabTB, Background = Brushes.Black, BorderThickness = new Thickness(2.0),
                                        Margin = new Thickness(0.0), Padding = new Thickness(0.0), Focusable = false,
                                        HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            void grabRedraw()
            {
                if (grabHelper.IsGrabMode)
                {
                    grabTB.Foreground = Brushes.White;
                    grabTB.Background = Brushes.DarkOrange;
                    dungeonTabs.Cursor = handCursor();
                    ((TextBlock)grabModeTextBlock.Child).Text = (TrackerModelOptions.OldStyleGrab.Value ? oldGrabModeText : moveModeText);
                    grabModeTextBlock.Opacity = 1.0;
                }
                else
                {
                    grabHelper.Abort();
                    dungeonHighlightCanvas.Children.Clear();
                    dungeonSourceHighlightCanvas.Children.Clear();
                    grabTB.Foreground = Brushes.Gray;
                    grabTB.Background = Brushes.Black;
                    dungeonTabs.Cursor = null;
                    grabModeTextBlock.Opacity = 0.0;
                }
            }
            canvasAdd(contentCanvas, grabButton, LD_X + 1.5, LD_Y + 168.0);   // right-hand column, where the map checkbox used to be
            grabButton.Click += (_, _) =>
            {
                grabHelper.ToggleGrabMode();
                grabRedraw();
            };
            ToolTip.SetTip(grabButton, "Middle click: switch MOVE / GRAB\nMOVE: right click cuts, left click drops. GRAB: picks up connected rooms");
            // middle click switches between the new MOVE and the old GRAB; the choice is remembered
            // (WPF PreviewMouseDown: a tunnelling handler)
            grabButton.AddHandler(InputElement.PointerPressedEvent, new EventHandler<PointerPressedEventArgs>((_, ea) =>
            {
                if (ea.GetCurrentPoint(grabButton).Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonPressed)
                {
                    ea.Handled = true;
                    if (grabHelper.IsGrabMode)
                        grabHelper.ToggleGrabMode();   // leave the current mode first (grabRedraw aborts any carried rooms)
                    TrackerModelOptions.OldStyleGrab.Value = !TrackerModelOptions.OldStyleGrab.Value;
                    grabTB.Text = (TrackerModelOptions.OldStyleGrab.Value ? "GRAB" : "MOVE");
                    grabRedraw();
                    TrackerModelOptions.writeSettings();
                }
            }), RoutingStrategies.Tunnel);
            dungeonTabs.SelectionChanged += (_, ea) =>
            {
                try
                {
                    var x = ea.AddedItems[0];
                    if (object.ReferenceEquals(x, levelTab))
                    {
                        dummyCanvas.Children.Clear();
                        canvasAdd(contentCanvasWithBadYOffset, contentCanvas, 0.0, 1.0);   // re-parent the content which may have been deparented by summary tab; y=1 so that all content is at multiples of 3, so that 2/3 view does not look blurry
                        levelTabSelected.Trigger(level);
                        showNumeral();
                    }
                }
                catch { }
                // the tab has already changed, kill the current grab
                if (grabHelper.IsGrabMode)
                    grabHelper.Abort();
                // and always draw (even if changing to), as it was unable to repaint when disabled earlier
                grabRedraw();
            };




            Z1R_SharedInterop.RoomInteropBridge.applyRoomStateFromSync =
                new Z1R_SharedInterop.RoomInteropBridge.ApplyRoomStateFromSyncDelegate((level, x, y, isComplete, roomTypeStr, monsterDetailStr, floorDropDetailStr, floorDropAppearsBright) =>
                {
                    if (level >= 1 && level <= 9 && x >= 0 && x <= 7 && y >= 0 && y <= 7)
                    {
                        var newRoom = new DungeonRoomState();
                        newRoom.IsComplete = isComplete;
                        newRoom.RoomType = RoomTypeExtensions.Parse(roomTypeStr);
                        newRoom.MonsterDetail = MonsterDetailExtensions.Parse(monsterDetailStr);
                        newRoom.FloorDropDetail = FloorDropDetailExtensions.Parse(floorDropDetailStr);
                        newRoom.FloorDropAppearsBright = floorDropAppearsBright;
                        newRoom.X = x;
                        newRoom.Y = y;
                        newRoom.Level = level;
                        // This will also trigger redraw for that cell
                        setNewValueFunctions[level - 1][x, y](newRoom);
                        isFirstTimeClickingAnyRoom[level - 1].Value = false;
                    }

                });


            var backgroundColorCanvas = new Canvas { Width = (double)(51 * 6 + 12), Height = (double)(TH) };
            canvasAdd(dungeonHeaderCanvas, backgroundColorCanvas, 0.0, 0.0);
            onEvent(TrackerModel.GetDungeon(level - 1).HiddenDungeonColorOrLabelChanged, t =>
                backgroundColorCanvas.Background = Graphics.freeze(new SolidColorBrush(Graphics.makeColor(t.Item1))));
            Action<int, int> animateDungeonRoomTile = (_, _) => { };
            var highlightColumnCanvases = Enumerable.Range(0, 8).Select(_ => new Canvas { Background = Brushes.White, Width = 51.0, Height = (double)TH, Opacity = 0.0 }).ToArray();
            void highlightColumn(int? colOpt)
            {
                for (int i = 0; i <= 7; i++)
                    highlightColumnCanvases[i].Opacity = 0.0;
                if (colOpt is int c) highlightColumnCanvases[c].Opacity = 0.3;
            }
            canvasAdd(dungeonBodyCanvas, roomHighlightOutline, 0.0, 0.0);
            var roomCirclesCanvas = new Canvas();
            dungeonBodyCanvas.Children.Add(roomCanvas);
            dungeonBodyCanvas.Children.Add(roomCirclesCanvas);
            var headerButtons = new List<Button>();   // dungeon-letter chooser buttons, so a header redraw (custom name change) can move them
            void makeLabelButton(int i)
            {
                var tb = new TextBox { Width = (double)(13 * 3 - 16), Height = (double)(TH + 8), FontSize = (double)(TH + 8), Foreground = Brushes.Black, Background = rainbowBrush, IsReadOnly = true, IsHitTestVisible = false,
                                        Text = $"{labelChar}", BorderThickness = new Thickness(0.0), FontFamily = HFF, FontWeight = FontWeight.Bold,
                                        HorizontalContentAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var tbc = new Canvas { Width = tb.Width, Height = (double)(TH - 4), ClipToBounds = true };
                canvasAdd(tbc, tb, 0.0, -8.0);
                var button = new Button { Height = (double)(TH), Content = tbc, BorderThickness = new Thickness(2.0), Margin = new Thickness(0.0), Padding = new Thickness(0.0), BorderBrush = Brushes.White };
                canvasAdd(dungeonCanvas, button, (double)(i * 51) + 6.0, 0.0);
                headerButtons.Add(button);
                button.Click += (_, _) =>
                {
                    if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                    {
                        CustomComboBoxes.GlobalFlag.popupIsActive = true;
                        var pos = tb.TranslatePoint(new Point(tb.Width / 2.0, tb.Height / 2.0), cm.AppMainCanvas).Value;
                        async Task work()
                        {
                            await Dungeon.HiddenDungeonCustomizerPopup(cm, level - 1, TrackerModel.GetDungeon(level - 1).Color, TrackerModel.GetDungeon(level - 1).LabelChar, false, true, pos);
                            CustomComboBoxes.GlobalFlag.popupIsActive = false;
                        }
                        CustomComboBoxes.StartImmediate(work());
                    }
                };
            }
            // header text: 'LEVEL-n' / 'BOARD-n', or the custom name (up to 7 chars; if fewer than 7, a dash follows, then the dungeon number); redrawn whenever the name or option changes
            var headerElems = new List<Control>();
            void drawHeader()
            {
                foreach (var e in headerElems) dungeonHeaderCanvas.Children.Remove(e);
                headerElems.Clear();
                foreach (var b in headerButtons) dungeonCanvas.Children.Remove(b);
                headerButtons.Clear();
                string make_customName()
                {
                    var n = TrackerModel.CustomLevelName;
                    if (n == "") return ""; else if (n.Length >= 7) return n.Substring(0, 7); else return (n + "-").PadRight(7, ' ');   // 7 chars: the 7th replaces the dash; otherwise name, dash, then blank
                }
                var customName = make_customName();
                int make_customNumberSlot()
                {
                    var n = TrackerModel.CustomLevelName;
                    return n.Length >= 7 ? 7 : n.Length + 1;
                }
                var customNumberSlot = make_customNumberSlot();
                for (int i_ = 0; i_ <= 7; i_++)
                {
                    var i = i_;
                    if (i != 7)
                    {
                        Image makeLetter(Func<Bitmap> bmpFunc)
                        {
                            var bi = bmpFunc();
                            var img = Graphics.BItoImage(bi);
                            img.Width = (double)TH;
                            img.Height = (double)TH;
                            img.Stretch = Stretch.UniformToFill;
                            RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None);
                            return img;
                        }
                        void addLetterAndHighlight(Image img)
                        {
                            dungeonHeaderCanvas.Children.Remove(highlightColumnCanvases[i]);
                            canvasAdd(dungeonHeaderCanvas, img, (double)(i * 51) + 9.0, 0.0);
                            canvasAdd(dungeonHeaderCanvas, highlightColumnCanvases[i], (double)(i * 51) - 6.0, 0.0);
                            headerElems.Add(img);
                        }
                        if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
                        {
                            if (i != 6 || labelChar == '9' || customName != "")
                            {
                                var img = makeLetter(() => Dungeon.MakeLetterBIInZeldaFont((customName != "" ? customName : (TrackerModelOptions.BOARDInsteadOfLEVEL.Value ? "BOARD" : "LEVEL") + "-9").Substring(i, 1)[0],
                                                                                                    Graphics.isBlackGoodContrast(TrackerModel.GetDungeon(level - 1).Color)));
                                if (!(customName != "" && i == customNumberSlot))   // that slot holds the dungeon-letter button instead
                                    addLetterAndHighlight(img);
                            }
                            else
                                makeLabelButton(i);
                        }
                        else
                        {
                            Bitmap bmpFunc() => Dungeon.MakeLetterBIInZeldaFont((customName != "" ? customName : $"{(TrackerModelOptions.BOARDInsteadOfLEVEL.Value ? "BOARD" : "LEVEL")}-{level} ").Substring(i, 1)[0], false);
                            addLetterAndHighlight(makeLetter(bmpFunc));
                        }
                    }
                }
                if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null) && customName != "" && labelChar != '9')
                    makeLabelButton(customNumberSlot);   // the dungeon-letter chooser sits right after the name's dash
                else if (customName != "")
                {
                    // dungeon number goes right after the name's dash
                    var numImg = Graphics.BItoImage(Dungeon.MakeLetterBIInZeldaFont(labelChar, false));
                    numImg.Width = (double)TH;
                    numImg.Height = (double)TH;
                    numImg.Stretch = Stretch.UniformToFill;
                    RenderOptions.SetBitmapInterpolationMode(numImg, BitmapInterpolationMode.None);
                    canvasAdd(dungeonHeaderCanvas, numImg, (double)(customNumberSlot * 51) + 9.0, 0.0);
                    headerElems.Add(numImg);
                }
            }
            drawHeader();
            OptionsMenu.BOARDInsteadOfLEVELOptionChanged += () => drawHeader();
            customLevelNameChanged += () => drawHeader();
            if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
                onEvent(TrackerModel.GetDungeon(level - 1).HiddenDungeonColorOrLabelChanged, _ =>
                {
                    if (Graphics.isBlackGoodContrast(TrackerModel.GetDungeon(level - 1).Color))
                    {
                        for (int i = 0; i <= 7; i++)
                            highlightColumnCanvases[i].Background = Brushes.Black;
                    }
                    else
                    {
                        for (int i = 0; i <= 7; i++)
                            highlightColumnCanvases[i].Background = Brushes.White;
                    }
                    drawHeader();
                });
            // right-click the header text to type a custom name (also settable on the start screen)
            ToolTip.SetTip(dungeonHeaderCanvas, "Right-click to set a custom name");
            ToolTip.SetShowDelay(dungeonHeaderCanvas, 400);
            // (WPF MouseRightButtonDown: a press of the right button)
            dungeonHeaderCanvas.PointerPressed += (_, ea) =>
            {
                if (ea.GetCurrentPoint(dungeonHeaderCanvas).Properties.PointerUpdateKind != PointerUpdateKind.RightButtonPressed) return;
                ea.Handled = true;
                // (WPF: Owner = Application.Current.MainWindow; here the window this header is in, which is the main window)
#if BROWSER
                var owner = BrowserWindow.Of(dungeonHeaderCanvas);
#else
                var owner = TopLevel.GetTopLevel(dungeonHeaderCanvas) as Window;
#endif
                var w = new Window { Title = "Custom level name", SizeToContent = SizeToContent.WidthAndHeight, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                                    ShowInTaskbar = false };
                var sp = new StackPanel { Margin = new Thickness(12.0) };
                sp.Children.Add(new TextBlock { Text = "Name to show instead of LEVEL / BOARD (up to 7 characters).\nLeave blank to go back to the default.", Margin = new Thickness(0.0, 0.0, 0.0, 8.0) });
                var tb = new TextBox { Text = TrackerModel.CustomLevelName, MaxLength = 7, Width = 140.0, HorizontalAlignment = HorizontalAlignment.Left };
                // (WPF: CharacterCasing = Upper. Avalonia's TextBox has no CharacterCasing, so typed text is upper-cased as it arrives)
                tb.AddHandler(InputElement.TextInputEvent, new EventHandler<TextInputEventArgs>((_, tea) => { if (tea.Text != null) tea.Text = tea.Text.ToUpper(); }), RoutingStrategies.Tunnel);
                sp.Children.Add(tb);
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0.0, 10.0, 0.0, 0.0) };
                var ok = new Button { Content = " OK ", IsDefault = true, Margin = new Thickness(0.0, 0.0, 6.0, 0.0) };
                var cancel = new Button { Content = " Cancel ", IsCancel = true };
                buttons.Children.Add(ok);
                buttons.Children.Add(cancel);
                sp.Children.Add(buttons);
                w.Content = sp;
                ok.Click += (_, _) =>
                {
                    TrackerModel.CustomLevelName = (tb.Text ?? "").Trim().ToUpperInvariant();
                    if (TrackerModel.CustomLevelName != "")   // (remembered for next time, along with the start screen choices)
                    {
                        TrackerModelOptions.SavedCustomLevelName = TrackerModel.CustomLevelName;
                        TrackerModelOptions.writeSettings();
                    }
                    triggerCustomLevelNameChanged();
                    w.Close(true);   // (WPF: DialogResult <- true, which closes the dialog)
                };
                // (WPF: IsCancel closes the dialog on Escape/Cancel by itself; an Avalonia IsCancel button only gets clicked, so it closes the window here)
                cancel.Click += (_, _) => w.Close(false);
                w.Opened += (_, _) => { tb.Focus(); tb.SelectAll(); };   // (WPF: Loaded)
                // (WPF: w.ShowDialog(), which blocks; a nested dispatcher frame, as CustomMessageBox.ShowDialogBlocking)
                var frame = new DispatcherFrame();
                w.Closed += (_, _) => frame.Continue = false;
                if (owner != null) _ = w.ShowDialog(owner); else w.Show();
                Dispatcher.UIThread.PushFrame(frame);
            };
            for (int i_ = 0; i_ <= 7; i_++)
            {
                var i = i_;
                // room map
                for (int j_ = 0; j_ <= 7; j_++)
                {
                    var j = j_;
                    var BUFFER = 2.0;  // I often accidentally click room when trying to target doors with mouse, make canvas smaller and draw outside it, so clicks on very edge not seen
                    var c = new Canvas { Width = (double)(13 * 3) - 2.0 * BUFFER, Height = (double)(9 * 3) - 2.0 * BUFFER, Background = Brushes.Black, IsHitTestVisible = true };
                    var (ROOM_X, ROOM_Y) = ((double)(i * 51) + BUFFER, (double)(j * 39) + BUFFER);
                    canvasAdd(roomCanvas, c, ROOM_X, ROOM_Y);
                    roomCanvases[i, j] = c;
                    roomIsCircled[i, j] = false;
                    void redraw()
                    {
                        if (!skipRedrawInsideCurrentImport)
                        {
                            c.Children.Clear();
                            roomCirclesCanvas.Children.Remove(roomCircles[i, j]);
                            var image = roomStates[i, j].CurrentDisplayEx(usedTransports);
                            image.IsHitTestVisible = false;
                            canvasAdd(c, image, -BUFFER, -BUFFER);

                            var rowsWithPlaceholders = new HashSet<int> { 0, 1, 4, 5, 6, 7 };
                            if (TrackerModelOptions.DisplayIconsInDungeonMap.Value && roomStates[i, j].IsEmpty && rowsWithPlaceholders.Contains(j))
                            {
                                var src = placeholderIconsByRow[j].Source;   // (WPF: .Source.Clone(); an Avalonia bitmap is immutable, so it is shared)
                                var ph = new Image();
                                ph.Source = src;
                                ph.Opacity = TrackerModelOptions.DungeonMapLocationHintOpacity;
                                ph.Stretch = Stretch.Uniform;
                                ph.Width = (double)(13 * 3);
                                ph.Height = (double)(9 * 3);
                                RenderOptions.SetBitmapInterpolationMode(ph, BitmapInterpolationMode.None);
                                canvasAdd(c, ph, -BUFFER, -BUFFER);
                            }

                            // a marked room next to an OFF tile gets a red 'no bomb' edge on each side that touches one (an unmarked room gets none)
                            if (TrackerModelOptions.ShowOffTileEdges.Value && !roomStates[i, j].IsEmpty)
                            {
                                bool isOffAt(int x, int y) => x >= 0 && x <= 7 && y >= 0 && y <= 7 && roomStates[x, y].RoomType.IsOffMap();
                                var (x0, x1, y0, y1) = (-BUFFER + 1.5, -BUFFER + (double)(13 * 3) - 1.5, -BUFFER + 1.5, -BUFFER + (double)(9 * 3) - 1.5);   // (inside the room picture's edge)
                                void edge(double ax, double ay, double bx, double by) =>
                                    canvasAdd(c, new Line { StartPoint = new Point(ax, ay), EndPoint = new Point(bx, by), Stroke = Brushes.Red, StrokeThickness = 3.0, IsHitTestVisible = false }, 0.0, 0.0);
                                if (isOffAt(i, j - 1)) edge(x0, y0, x1, y0);
                                if (isOffAt(i, j + 1)) edge(x0, y1, x1, y1);
                                if (isOffAt(i - 1, j)) edge(x0, y0, x0, y1);
                                if (isOffAt(i + 1, j)) edge(x1, y0, x1, y1);
                            }

                            if (roomIsCircled[i, j])
                            {
                                var ellipse = new Ellipse { Width = (double)(13 * 3 + 12), Height = (double)(9 * 3 + 12), Stroke = Brushes.Yellow, StrokeThickness = 3.0, IsHitTestVisible = false };
                                //ellipse.StrokeDashArray <- new DoubleCollection( seq[0.;2.5;6.;5.;6.;5.;6.;5.;6.;5.] )
                                ellipse.StrokeDashArray = new AvaloniaList<double>(new[] { 0.0, 12.5, 8.0, 15.0, 8.0, 15.0, });
                                roomCircles[i, j] = ellipse;
                                canvasAdd(roomCirclesCanvas, ellipse, ROOM_X - 6.0 - BUFFER, ROOM_Y - 6.0 - BUFFER);
                            }
                        }
                    }
                    //RPT new. Order unsure
                    redraw();
                    roomRedrawFuncs.Add(() => redraw());
                    // what a room turning OFF (or back on) does to its neighbors: runs from every path that changes a room (Changed event and SetNewValue)
                    void offTileNeighborEffects()
                    {
                        // an OFF room gets the red 'no bomb' edge on its marked neighbors, so a hand-placed 'no' door wall between them is redundant: remove it
                        // (not when applying a partner's update: their app already sent the door changes)
                        if (TrackerModelOptions.ShowOffTileEdges.Value && roomStates[i, j].RoomType.IsOffMap() && !TrackerModelOptions.isCurrentlyApplyingRemoteUpdate())
                        {
                            void clearNo(Dungeon.Door d) { if (d.State == Dungeon.DoorState.NO) d.State = Dungeon.DoorState.UNKNOWN; }
                            if (i > 0 && !roomStates[i - 1, j].IsEmpty) clearNo(horizontalDoors[i - 1, j]);
                            if (i < 7 && !roomStates[i + 1, j].IsEmpty) clearNo(horizontalDoors[i, j]);
                            if (j > 0 && !roomStates[i, j - 1].IsEmpty) clearNo(verticalDoors[i, j - 1]);
                            if (j < 7 && !roomStates[i, j + 1].IsEmpty) clearNo(verticalDoors[i, j]);
                        }
                        // the neighbors' red 'no bomb' edges depend on whether this room is OFF
                        foreach (var (nx, ny) in new[] { (i - 1, j), (i + 1, j), (i, j - 1), (i, j + 1) })
                            if (nx >= 0 && nx <= 7 && ny >= 0 && ny <= 7 && nx * 8 + ny < roomRedrawFuncs.Count) roomRedrawFuncs[nx * 8 + ny]();
                    }
                    roomStates[i, j].Changed += _ =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[Changed] triggered for ({i},{j})");
                        redraw();
                        offTileNeighborEffects();
                    };

                    //let usedTransportsRemoveState(roomState:DungeonRoomState) =
                    //    // track transport being changed away from
                    //    let ktn = roomState.RoomType.KnownTransportNumber()
                    //    if ktn.HasValue then
                    //        usedTransports.[ktn.Value] <- usedTransports.[ktn.Value] - 1
                    //        true
                    //    else
                    //        false
                    //let usedTransportsAddState (roomState: DungeonRoomState) =
                    //    let ktn = roomState.RoomType.KnownTransportNumber()
                    //    if ktn.HasValue then
                    //        usedTransports.[ktn.Value] <- usedTransports.[ktn.Value] + 1
                    //        true
                    //    else
                    //        false

                    void SetNewValue(DungeonRoomState newState)
                    {
                        var originalState = roomStates[i, j].Clone();
#if DEBUG
                        System.Diagnostics.Debug.WriteLine($"SetNewValue called for ({i},{j}) - New ID: {newState.DebugId} - Current ID: {roomStates[i, j].DebugId.ToString()}");
#endif


                        if (
                            //originalState.IsComplete <> newState.IsComplete ||
                            //originalState.RoomType <> newState.RoomType ||
                            //originalState.MonsterDetail <> newState.MonsterDetail ||
                            //originalState.FloorDropDetail <> newState.FloorDropDetail ||
                            //originalState.FloorDropAppearsBright <> newState.FloorDropAppearsBright
                            1 != 2 // RPT: this is a hack to avoid the above checks, which are not needed for the current use case
                        )
                        {
                            // (F#: `originalState.RoomType.IsNotMarked`, a function value called later; C# can't make a delegate from an extension method on an enum, so a local function)
                            var originalRoomType = originalState.RoomType;
                            bool originallyWasNotMarked() => originalRoomType.IsNotMarked();

                            // Determine if this room change is legal
                            bool make_isLegal()
                            {
                                var newKtn = newState.RoomType.KnownTransportNumber();
                                var oldKtn = originalState.RoomType.KnownTransportNumber();
                                if (newKtn.HasValue && (!oldKtn.HasValue || newKtn.Value != oldKtn.Value))
                                    return usedTransports[newKtn.Value] < 2;
                                else
                                    return true;
                            }
                            var isLegal = make_isLegal();


                            if (isLegal)
                            {
                                bool make_removedTransport()
                                {
                                    var v = originalState.RoomType.KnownTransportNumber();
                                    if (v.HasValue)
                                    {
                                        usedTransports[v.Value] = usedTransports[v.Value] - 1;
                                        return true;
                                    }
                                    else return false;
                                }
                                var removedTransport = make_removedTransport();

                                if (originalState.RoomType.IsOldMan())
                                    oldManCount = oldManCount - 1;

                                roomStates[i, j].CSharp.CopyFrom(newState.CSharp);
                                // Restore positional metadata: CopyFrom can overwrite Level/X/Y
                                // with 0 when newState comes from AsDungeonRoomState() (DungeonMaps import),
                                // which doesn't set Level/X/Y. Without this, OnRoomChanged fires with
                                // Level=0 and is silently dropped by sendRoomChangeUpdate's level<>0 guard.
                                roomStates[i, j].CSharp.Level = level;  // level is 1-indexed (outer loop var)
                                roomStates[i, j].CSharp.X = i;
                                roomStates[i, j].CSharp.Y = j;
                                if (originalState.RoomType.IsOffMap() != newState.RoomType.IsOffMap() && !skipRedrawInsideCurrentImport) offTileNeighborEffects();
                                //roomStates.[i,j].Changed.Add(fun _ -> redraw())
                                //RPT maybe flip this
                                if (TrackerModelOptions.isCurrentlyApplyingRemoteUpdate())
                                    redraw();


                                if (newState.RoomType.IsOldMan())
                                    oldManCount = oldManCount + 1;
                                updateOldManCountText();

                                bool make_addedTransport()
                                {
                                    var v = newState.RoomType.KnownTransportNumber();
                                    if (v.HasValue)
                                    {
                                        usedTransports[v.Value] = usedTransports[v.Value] + 1;
                                        return true;
                                    }
                                    else return false;
                                }
                                var addedTransport = make_addedTransport();

                                if (removedTransport || addedTransport)
                                    redrawAllRooms();

                                // Determine if this is a second transport being added
                                var ktn = newState.RoomType.KnownTransportNumber();
                                var secondTransport = ktn.HasValue && usedTransports[ktn.Value] == 2;

                                // Door inference: only infer when a previously-unmarked room is now marked.
                                // Track whether any door state actually changed so we can skip the redrawAllDoors() call
                                // when nothing needs updating (the common case for plain room marks).
                                bool make_doorInferenceChanged()
                                {
                                    if (TrackerModelOptions.DoDoorInference.Value && originallyWasNotMarked() && !newState.IsEmpty && !secondTransport && !newState.IsGannonOrZelda)
                                    {
                                        var possibleEntries = new List<Dungeon.Door>();
                                        void maybeAdd(Dungeon.Door door)
                                        {
                                            if (door.State != Dungeon.DoorState.NO)
                                                possibleEntries.Add(door);
                                        }
                                        if (i > 0 && !roomStates[i - 1, j].IsEmpty) maybeAdd(horizontalDoors[i - 1, j]);
                                        if (i < 7 && !roomStates[i + 1, j].IsEmpty) maybeAdd(horizontalDoors[i, j]);
                                        if (j > 0 && !roomStates[i, j - 1].IsEmpty) maybeAdd(verticalDoors[i, j - 1]);
                                        if (j < 7 && !roomStates[i, j + 1].IsEmpty) maybeAdd(verticalDoors[i, j]);
                                        if (possibleEntries.Count == 1)
                                        {
                                            var door = possibleEntries[0];
                                            if (door.State == Dungeon.DoorState.UNKNOWN)
                                            {
                                                door.State = Dungeon.DoorState.YES;
                                                return true;  // a door state changed; redraw needed
                                            }
                                            else return false;
                                        }
                                        else return false;
                                    }
                                    else return false;
                                }
                                var doorInferenceChanged = make_doorInferenceChanged();

                                // the Princess Room has unbombable walls on its left, top and right: mark those three doors as solid walls when it is placed
                                // (the Old Man (hint) and Bomb Upgrade rooms have an unbombable wall on the top side only)
                                bool make_princessWallsChanged()
                                {
                                    var isPrincess = newState.RoomType == RoomType.PrincessRoom && originalState.RoomType != RoomType.PrincessRoom;
                                    var isTopWallRoom = (newState.RoomType == RoomType.OldManHint || newState.RoomType == RoomType.BombUpgrade) && originalState.RoomType != newState.RoomType;
                                    if (isPrincess || isTopWallRoom)
                                    {
                                        var walls = new List<Dungeon.Door>();
                                        if (isPrincess && i > 0) walls.Add(horizontalDoors[i - 1, j]);
                                        if (isPrincess && i < 7) walls.Add(horizontalDoors[i, j]);
                                        if (j > 0) walls.Add(verticalDoors[i, j - 1]);
                                        var changed = false;
                                        foreach (var door in walls)
                                            if (door.State != Dungeon.DoorState.NO)
                                            {
                                                door.State = Dungeon.DoorState.NO;
                                                changed = true;
                                            }
                                        return changed;
                                    }
                                    else return false;
                                }
                                var princessWallsChanged = make_princessWallsChanged();

                                // the other way round: leaving a Princess / Old Man / Bomb Upgrade room takes back the unbombable walls placed when it was marked
                                bool make_wallsReverted()
                                {
                                    bool isTopWall(RoomType rt) => rt == RoomType.OldManHint || rt == RoomType.BombUpgrade;
                                    var wasPrincess = originalState.RoomType == RoomType.PrincessRoom && newState.RoomType != RoomType.PrincessRoom;
                                    var wasTopWallRoom = isTopWall(originalState.RoomType) && !isTopWall(newState.RoomType);
                                    if (wasPrincess || wasTopWallRoom)
                                    {
                                        var walls = new List<Dungeon.Door>();
                                        if (wasPrincess && i > 0) walls.Add(horizontalDoors[i - 1, j]);
                                        if (wasPrincess && i < 7) walls.Add(horizontalDoors[i, j]);
                                        if (j > 0) walls.Add(verticalDoors[i, j - 1]);
                                        var changed = false;
                                        foreach (var door in walls)
                                            if (door.State == Dungeon.DoorState.NO)
                                            {
                                                door.State = Dungeon.DoorState.UNKNOWN;
                                                changed = true;
                                            }
                                        return changed;
                                    }
                                    else return false;
                                }
                                var wallsReverted = make_wallsReverted();

                                // placing a ladder-requiring room (and not having the ladder) puts a ladder in this level's first blocker box
                                if (level >= 1 && level <= 8 && newState.RoomType != originalState.RoomType && !TrackerModel.playerComputedStateSummary.HaveLadder && !(TrackerModelOptions.isCurrentlyApplyingRemoteUpdate()))
                                {
                                    switch (newState.RoomType)
                                    {
                                        case RoomType.Chevy:
                                        case RoomType.TopMoat:
                                        case RoomType.RightMoat:
                                        case RoomType.LavaMoat:
                                        case RoomType.Tee:
                                        case RoomType.CircleMoat:
                                        case RoomType.DoubleMoat:
                                            {
                                                var di = level - 1;
                                                TrackerModel.DungeonBlocker get(int k) => TrackerModel.DungeonBlockersContainer.GetDungeonBlocker(di, k);
                                                var n = TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON;
                                                var hasLadder = Enumerable.Range(0, n).Any(k => get(k).Equals(TrackerModel.DungeonBlocker.LADDER) || get(k).Equals(TrackerModel.DungeonBlocker.MAYBE_LADDER));
                                                if (!hasLadder)
                                                {
                                                    int? make_slot()
                                                    {
                                                        if (n > 2 && get(2).Equals(TrackerModel.DungeonBlocker.NOTHING)) return 2;   // the third blocker box
                                                        else
                                                        {
                                                            foreach (var k in Enumerable.Range(0, n))
                                                                if (get(k).Equals(TrackerModel.DungeonBlocker.NOTHING)) return k;
                                                            return null;
                                                        }
                                                    }
                                                    var slot = make_slot();
                                                    if (slot is int k2) TrackerModel.DungeonBlockersContainer.SetDungeonBlocker(di, k2, TrackerModel.DungeonBlocker.MAYBE_LADDER);
                                                }
                                                break;
                                            }
                                        default: break;
                                    }
                                }

                                //let rendered = roomStates.[i,j].CurrentDisplayEx(usedTransports)
                                //System.Diagnostics.Debug.WriteLine(sprintf "[SetNewValue] Render preview: IsComplete = %b" newState.IsComplete)

                                updateHeaderCanvases();
                                redraw();
                                // Only redraw doors when something actually changed: a transport was added/removed
                                // (already causes redrawAllRooms above) or door inference set a new door state.
                                if (removedTransport || addedTransport || doorInferenceChanged || princessWallsChanged || wallsReverted)
                                    redrawAllDoors();
                                animateDungeonRoomTile(i, j);
                                if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                                    triggerRoomChanged(new RoomChangeInfo
                                    {
                                        Level = level,  // You need to capture this value earlier!
                                        X = i,
                                        Y = j,
                                        NewState = roomStates[i, j].CSharp,
                                    });


                            }
                            else
                                Graphics.ErrorBeepWithReminderLogText("Cannot mark a third copy of transport-stair pair");
                        }
                    }


                    setNewValueFunctions[level - 1][i, j] = SetNewValue;
                    async Task activatePopup(bool positionAtEntranceRoomIcons)
                    {
                        CustomComboBoxes.GlobalFlag.popupIsActive = true;
                        var roomPos = c.TranslatePoint(new Point(), cm.AppMainCanvas).Value;
                        var dashCanvas = new Canvas();
                        canvasAdd(c, dashCanvas, -BUFFER, -BUFFER);
                        var pretty = CustomComboBoxes.GetPrettyDashes("DungeonRoomSelectActivatePopup", Brushes.Lime, 13.0 * 3.0, 9.0 * 3.0, 3.0, 2.0, 1.2);
                        dashCanvas.Children.Add(pretty);
                        var pos = new Point(roomPos.X + 13.0 * 3.0 / 2.0, roomPos.Y + 9.0 * 3.0 / 2.0);
                        DungeonPopups.suppressRoomSyncTemporarily = true;
                        await DungeonPopups.DoDungeonRoomSelectPopup(cm, roomStates[i, j], usedTransports, SetNewValue, positionAtEntranceRoomIcons, new CustomComboBoxes.GridClickDismissalWarpReturn.WarpTo(pos));
                        dashCanvas.Children.Remove(pretty);
                        c.Children.Remove(dashCanvas);
                        redraw();
                        DungeonPopups.suppressRoomSyncTemporarily = false;
                        justUnmarked = null;
                        CustomComboBoxes.GlobalFlag.popupIsActive = false;
                    }
                    void highlightImpl(Canvas canvas, bool[,] contiguous, IBrush brush)
                    {
                        for (int x = 0; x <= 7; x++)
                            for (int y = 0; y <= 7; y++)
                                if (contiguous[x, y])
                                {
                                    var r = new Rectangle { Width = (double)(13 * 3 + 12), Height = (double)(9 * 3 + 12), Fill = brush, Opacity = 0.4, IsHitTestVisible = false };
                                    canvasAdd(canvas, r, (double)(x * 51 - 6), (double)(TH + y * 39 - 6));
                                }
                    }
                    void highlight(bool[,] contiguous, IBrush brush) => highlightImpl(dungeonHighlightCanvas, contiguous, brush);
                    c.MyKeyAdd(ea =>
                    {
                        if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                        {
                            if (!grabHelper.IsGrabMode)
                            {
                                if (HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key, out var target))
                                {
                                    switch (target)
                                    {
                                        case HotKeys.GlobalHotkeyTargets.MoveCursorRight:
                                            ea.Handled = true;
                                            if (i < 7)
                                                Graphics.NavigationallyWarpMouseCursorTo(centerOf((double)i + 1.0, (double)j));
                                            else
                                                Graphics.NavigationallyWarpMouseCursorTo(localDungeonTrackerPanelPosToCursorRightToF());
                                            break;
                                        case HotKeys.GlobalHotkeyTargets.MoveCursorLeft:
                                            ea.Handled = true;
                                            if (i > 0)
                                                Graphics.NavigationallyWarpMouseCursorTo(centerOf((double)i - 1.0, (double)j));
                                            break;
                                        case HotKeys.GlobalHotkeyTargets.MoveCursorUp:
                                            ea.Handled = true;
                                            if (j > 0)
                                                Graphics.NavigationallyWarpMouseCursorTo(centerOf((double)i, (double)j - 1.0));
                                            break;
                                        case HotKeys.GlobalHotkeyTargets.MoveCursorDown:
                                            ea.Handled = true;
                                            if (j < 7)
                                                Graphics.NavigationallyWarpMouseCursorTo(centerOf((double)i, (double)j + 1.0));
                                            break;
                                        default: break;
                                    }
                                }

                                if (!ea.Handled)
                                {
                                    void urb(HotKeys.DungeonRoomHotKey hkOpt)
                                    {
                                        if (justUnmarked is (int x, int y, var phk) && hkOpt is HotKeys.DungeonRoomHotKey hk && x == i && y == j && phk == hk)
                                            unmarkRemarkBehavior(
                                                cm,
                                                (x, y, hk),  // pass the unwrapped Choice value
                                                boxViewsAndActivations,
                                                level - 1,
                                                centerOf((double)i, (double)j)
                                            );
                                        else
                                            justUnmarked = null;
                                    }


                                    HotKeys.DungeonRoomHotKeyProcessor.TryGetValue(ea.Key, out var hk0);

                                    switch (hk0)
                                    {
                                        case HotKeys.DungeonRoomHotKey.OfRoomType(var rtObj):
                                            try
                                            {
                                                var roomType = rtObj;
                                                ea.Handled = true;
                                                // work on a copy: SetNewValue compares against roomStates[i,j] (the OLD state, for the old-man count and transport use) and may refuse the change (a third copy of a transport)
                                                var room = roomStates[i, j].Clone();
                                                if (room.RoomType == roomType)
                                                {
                                                    room.RoomType = RoomType.Unmarked;
                                                    justUnmarked = (i, j, choice1(roomType));
                                                }
                                                else
                                                {
                                                    room.RoomType = roomType;
                                                    if (roomType == RoomType.BombUpgrade || roomType == RoomType.HungryGoriyaMeatBlock) room.IsComplete = false;   // starts un-collected: blue dot on the level number (meat-blocked room: no green check yet)
                                                    urb(choice1(roomType));
                                                }
                                                SetNewValue(room);
                                            }
                                            catch (System.InvalidCastException) { }
                                            break;

                                        case HotKeys.DungeonRoomHotKey.OfMonsterDetail(var mdObj):
                                            try
                                            {
                                                var monsterDetail = mdObj;
                                                ea.Handled = true;
                                                var room = roomStates[i, j];
                                                if (room.MonsterDetail == monsterDetail)
                                                {
                                                    room.MonsterDetail = MonsterDetail.Unmarked;
                                                    justUnmarked = (i, j, choice2(monsterDetail));
                                                }
                                                else
                                                {
                                                    room.MonsterDetail = monsterDetail;
                                                    urb(choice2(monsterDetail));
                                                }
                                                SetNewValue(room);
                                            }
                                            catch (System.InvalidCastException) { }
                                            break;

                                        case HotKeys.DungeonRoomHotKey.OfFloorDropDetail(var fdObj):
                                            try
                                            {
                                                var floorDropDetail = fdObj;
                                                ea.Handled = true;
                                                var room = roomStates[i, j];
                                                if (room.FloorDropDetail == floorDropDetail)
                                                {
                                                    room.FloorDropDetail = FloorDropDetail.Unmarked;
                                                    justUnmarked = (i, j, choice3(floorDropDetail));
                                                }
                                                else
                                                {
                                                    room.FloorDropDetail = floorDropDetail;
                                                    urb(choice3(floorDropDetail));
                                                }
                                                SetNewValue(room);
                                            }
                                            catch (System.InvalidCastException) { }
                                            break;

                                        case HotKeys.DungeonRoomHotKey.OfDoor(var dhkrObj):
                                            if (dhkrObj is DoorHotKeyResponse doorHotKeyResponse)
                                            {
                                                ea.Handled = true;
                                                justUnmarked = (i, j, choice4(doorHotKeyResponse));
                                                switch (doorHotKeyResponse.Direction, doorHotKeyResponse.Action)
                                                {
                                                    case (DoorDirection.West, DoorAction.Increment): if (i > 0) horizontalDoors[i - 1, j].Next(); break;
                                                    case (DoorDirection.West, DoorAction.Decrement): if (i > 0) horizontalDoors[i - 1, j].Prev(); break;
                                                    case (DoorDirection.East, DoorAction.Increment): if (i < 7) horizontalDoors[i, j].Next(); break;
                                                    case (DoorDirection.East, DoorAction.Decrement): if (i < 7) horizontalDoors[i, j].Prev(); break;
                                                    case (DoorDirection.North, DoorAction.Increment): if (j > 0) verticalDoors[i, j - 1].Next(); break;
                                                    case (DoorDirection.North, DoorAction.Decrement): if (j > 0) verticalDoors[i, j - 1].Prev(); break;
                                                    case (DoorDirection.South, DoorAction.Increment): if (j < 7) verticalDoors[i, j].Next(); break;
                                                    case (DoorDirection.South, DoorAction.Decrement): if (j < 7) verticalDoors[i, j].Prev(); break;
                                                    default: break;
                                                }
                                            }
                                            else
                                                justUnmarked = null;
                                            break;

                                        case null: break;
                                    }

                                    if (ea.Handled)
                                    {
                                        isFirstTimeClickingAnyRoom[level - 1].Value = false;
                                        numeral.Opacity = 0.0;
                                    }
                                }
                            }
                        }
                    });
                    var justHighlightedMeatShop = false;
                    c.PointerEntered += (_, _) =>
                    {
                        if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                        {
                            if (grabHelper.IsGrabMode)
                            {
                                if (!grabHelper.HasGrab)
                                {
                                    if (TrackerModelOptions.OldStyleGrab.Value)
                                    {
                                        // old grab: hovering a marked room previews it and every room connected to it
                                        if (!(roomStates[i, j].IsEmpty))
                                        {
                                            dungeonHighlightCanvas.Children.Clear(); // clear old preview
                                            highlight(grabHelper.PreviewGrab(i, j, roomStates), Brushes.Lime);
                                        }
                                    }
                                    else if (!(roomStates[i, j].IsEmpty))
                                    {
                                        dungeonHighlightCanvas.Children.Clear(); // clear old preview
                                        var single = new bool[8, 8];   // just this one room (rooms are picked one at a time)
                                        single[i, j] = true;
                                        highlight(single, Brushes.Orange);
                                    }
                                }
                                else
                                {
                                    dungeonHighlightCanvas.Children.Clear(); // clear old preview
                                    var (ok, warn) = grabHelper.PreviewDrop(i, j);
                                    highlight(ok, Brushes.Lime);
                                    highlight(warn, Brushes.Yellow);
                                }
                            }
                            else
                            {
                                highlightRow(j);
                                highlightColumn(i);
                                roomHighlightOutline.Opacity = Dungeon.highlightOpacity;
                                Canvas.SetLeft(roomHighlightOutline, (double)(i * 51) - 2.0);
                                Canvas.SetTop(roomHighlightOutline, (double)(j * 39) - 2.0);
                                if (roomStates[i, j].RoomType == RoomType.HungryGoriyaMeatBlock)
                                {
                                    AhhGlobalVariables.showShopLocatorInstanceFunc(TrackerModel.MapSquareChoiceDomainHelper.MEAT);
                                    justHighlightedMeatShop = true;
                                }
                            }
                        }
                    };
                    c.PointerExited += (_, _) =>
                    {
                        if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                            if (grabHelper.IsGrabMode)
                                dungeonHighlightCanvas.Children.Clear(); // clear old preview
                        highlightRow(null);
                        highlightColumn(null);
                        roomHighlightOutline.Opacity = 0.0;
                        if (justHighlightedMeatShop)
                        {
                            justHighlightedMeatShop = false;
                            // turn off showShopLocatorInstanceFunc and go back to the highlight for this level
                            AhhGlobalVariables.hideLocator();
                            contentCanvasMouseEnterFunc(level);
                        }
                    };
                    void doMonsterDetailPopup()
                    {
                        async Task work()
                        {
                            var pos = c.TranslatePoint(new Point(-6.0, -6.0), cm.AppMainCanvas).Value;
                            var mdOpt = await DungeonPopups.DoMonsterDetailPopup(cm, pos.X, pos.Y, roomStates[i, j].MonsterDetail);
                            switch (mdOpt)
                            {
                                case (true, var md):
                                    {
                                        var room = roomStates[i, j];
                                        room.MonsterDetail = md;
                                        SetNewValue(room);
                                        break;
                                    }
                                case (false, _): break;
                            }
                            justUnmarked = null;
                            CustomComboBoxes.GlobalFlag.popupIsActive = false;
                        }
                        CustomComboBoxes.StartImmediate(work());
                    }
                    void doFloorDropDetailPopup()
                    {
                        async Task work()
                        {
                            var pos = c.TranslatePoint(new Point(18.0, 6.0), cm.AppMainCanvas).Value;
                            var fdOpt = await DungeonPopups.DoFloorDropDetailPopup(cm, pos.X, pos.Y, roomStates[i, j].FloorDropDetail);
                            switch (fdOpt)
                            {
                                case (true, var fd):
                                    {
                                        var room = roomStates[i, j];
                                        room.FloorDropDetail = fd;
                                        SetNewValue(room);
                                        break;
                                    }
                                case (false, _): break;
                            }
                            justUnmarked = null;
                            CustomComboBoxes.GlobalFlag.popupIsActive = false;
                        }
                        CustomComboBoxes.StartImmediate(work());
                    }
                    c.PointerWheelChanged += (s, x) =>
                    {
                        var notch = Graphics.wheelNotch(s, x);
                        if (notch == 0) return;   // (whole notches only: trackpads send tiny deltas, and sideways scrolling, which WPF's MouseWheel never reports; see PROGRESS.md)
                        if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                        {
                            if (!grabHelper.IsGrabMode)  // cannot scroll rooms in grab mode
                            {
                                CustomComboBoxes.GlobalFlag.popupIsActive = true;
                                var scrollUp = notch > 0;
                                var showMonster = scrollUp != TrackerModelOptions.ReverseScrollWheelDirection.Value;
                                if (showMonster)
                                    doMonsterDetailPopup();
                                else
                                    doFloorDropDetailPopup();
                            }
                        }
                    };
                    // mouse 4: back to a brand new room (type, monster, floor drop, completed mark and circle all cleared)
                    void resetRoom()
                    {
                        var rs = new DungeonRoomState();
                        rs.Level = level;
                        rs.X = i;
                        rs.Y = j;
                        SetNewValue(rs);
                        roomIsCircled[i, j] = false;
                        redraw();
                        redrawAllDoors();
                        showMinimaps();
                        TrackerModel.dungeonRoomModelChanged.SetNow();   // circles reach a co-op partner only through this
                    }
                    void dragBehavior(string whichButtonStr)
                    {
                        if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                        {
                            if (whichButtonStr == "L" && roomStates[i, j].RoomType == RoomType.OffTheMap)
                            {
                                isFirstTimeClickingAnyRoom[level - 1].Value = false;  // originally painting cancels the first time accelerator (for 'play half dungeon, then start maybe-marking' scenario)
                                numeral.Opacity = 0.0;
                                roomStates[i, j].RoomType = RoomType.Unmarked;
                                roomStates[i, j].IsComplete = false;
                                redraw();
                                redrawAllDoors();
                                showMinimaps();
                            }
                            else if (whichButtonStr == "R" && roomStates[i, j].RoomType == RoomType.Unmarked)
                            {
                                isFirstTimeClickingAnyRoom[level - 1].Value = false;  // originally painting cancels the first time accelerator (for 'play half dungeon, then start maybe-marking' scenario)
                                numeral.Opacity = 0.0;
                                roomStates[i, j].RoomType = RoomType.OffTheMap;
                                roomStates[i, j].IsComplete = false;
                                redraw();
                                redrawAllDoors();
                                showMinimaps();
                            }
                            else if (whichButtonStr == "M" && roomStates[i, j].RoomType.IsNotMarked())
                            {
                                isFirstTimeClickingAnyRoom[level - 1].Value = false;  // originally painting cancels the first time accelerator (for 'play half dungeon, then start maybe-marking' scenario)
                                numeral.Opacity = 0.0;
                                roomStates[i, j].RoomType = defaultRoom();
                                roomStates[i, j].IsComplete = true;
                                redraw();
                                redrawAllDoors();
                                showMinimaps();
                            }
                        }
                    }
                    // (WPF: the surface's MouseUp args with ChangedButton; Avalonia: the release's InitialPressMouseButton. Shift is read from the event, not the global keyboard)
                    roomDragDrop.RegisterClickable(c, ea =>
                    {
                        if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                        {
                            async Task work()
                            {
                                if (ea.Handled) return;
                                var shiftIsDown = ea.KeyModifiers.HasFlag(KeyModifiers.Shift);
                                if (ea.InitialPressMouseButton == MouseButton.Left)
                                {
                                    if (grabHelper.IsGrabMode)
                                    {
                                        if (!grabHelper.HasGrab && TrackerModelOptions.OldStyleGrab.Value)
                                        {
                                            // old grab: click a marked room to pick it up along with every connected room; the next click drops them
                                            if (!(roomStates[i, j].IsEmpty))
                                            {
                                                dungeonHighlightCanvas.Children.Clear(); // clear preview
                                                var contiguous = grabHelper.StartGrab(i, j, roomStates, roomIsCircled, horizontalDoors, verticalDoors);
                                                highlightImpl(dungeonSourceHighlightCanvas, contiguous, Brushes.Pink);  // this highlight stays around until completed/aborted
                                                highlight(contiguous, Brushes.Lime);
                                            }
                                        }
                                        else if (!grabHelper.HasGrab)
                                        {
                                            // left click picks (or un-picks) this one room; right click then cuts all the picked rooms out
                                            if (!(roomStates[i, j].IsEmpty))
                                            {
                                                grabHelper.ToggleSelect(i, j);
                                                dungeonSourceHighlightCanvas.Children.Clear();
                                                highlightImpl(dungeonSourceHighlightCanvas, grabHelper.Selected, Brushes.Orange);   // the picked rooms stay orange until cut / aborted
                                            }
                                        }
                                        else
                                        {
                                            //let backupRoomStates = roomStates |> Array2D.map (fun x -> x.Clone())
                                            var backupRoomValues = new (RoomType, MonsterDetail, FloorDropDetail, bool, bool)[8, 8];
                                            for (int x = 0; x < 8; x++)
                                                for (int y = 0; y < 8; y++)
                                                {
                                                    var r = roomStates[x, y];
                                                    backupRoomValues[x, y] = (r.RoomType, r.MonsterDetail, r.FloorDropDetail, r.FloorDropAppearsBright, r.IsComplete);
                                                }

                                            var backupRoomIsCircled = (bool[,])roomIsCircled.Clone();
                                            var backupHorizontalDoors = new Dungeon.DoorState[7, 8];
                                            for (int x = 0; x < 7; x++) for (int y = 0; y < 8; y++) backupHorizontalDoors[x, y] = horizontalDoors[x, y].State;
                                            var backupVerticalDoors = new Dungeon.DoorState[8, 7];
                                            for (int x = 0; x < 8; x++) for (int y = 0; y < 7; y++) backupVerticalDoors[x, y] = verticalDoors[x, y].State;

                                            grabHelper.DoDrop(i, j, roomStates, roomIsCircled, horizontalDoors, verticalDoors);
                                            for (int x = 0; x <= 7; x++)
                                                for (int y = 0; y <= 7; y++)
                                                    if (!roomStates[x, y].IsEmpty)
                                                        triggerRoomChanged(new RoomChangeInfo
                                                        {
                                                            Level = level,
                                                            X = x,
                                                            Y = y,
                                                            NewState = roomStates[x, y].CSharp,
                                                        });


                                            redrawAllRooms();  // make updated changes visual
                                            CustomComboBoxes.GlobalFlag.popupIsActive = true;   // (DoModal requires this to be set by the caller)
                                            string keepOrUndo;
                                            try { keepOrUndo = await PieMenus.ConfirmBoxAsync(cm, "You moved a dungeon segment. Keep this change?", new[] { "Keep changes", "Undo" }); }
                                            finally { CustomComboBoxes.GlobalFlag.popupIsActive = false; }   // (never leave the UI locked, even if the box throws)
                                            grabRedraw();  // DoDrop completes the grab, neeed to update the visual
                                            if (keepOrUndo == null || keepOrUndo == "Undo")
                                            {
                                                // copy back from old state
                                                for (int x = 0; x < 8; x++)
                                                    for (int y = 0; y < 8; y++)
                                                    {
                                                        var (rt, md, fd, bright, complete) = backupRoomValues[x, y];
                                                        var r = roomStates[x, y];
                                                        r.RoomType = rt;
                                                        r.MonsterDetail = md;
                                                        r.FloorDropDetail = fd;
                                                        r.FloorDropAppearsBright = bright;
                                                        r.IsComplete = complete;
                                                    }

                                                for (int x = 0; x < 8; x++) for (int y = 0; y < 8; y++) roomIsCircled[x, y] = backupRoomIsCircled[x, y];
                                                redrawAllRooms();  // make reverted changes visual
                                                for (int x = 0; x < 7; x++) for (int y = 0; y < 8; y++) horizontalDoors[x, y].State = backupHorizontalDoors[x, y];
                                                for (int x = 0; x < 8; x++) for (int y = 0; y < 7; y++) verticalDoors[x, y].State = backupVerticalDoors[x, y];
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (shiftIsDown)
                                        {
                                            CustomComboBoxes.GlobalFlag.popupIsActive = true;
                                            doMonsterDetailPopup();
                                        }
                                        else
                                        {
                                            // plain left click
                                            //let workingCopy = roomStates.[i,j].Clone()

                                            if (!(isFirstTimeClickingAnyRoom[level - 1].Value) && roomStates[i, j].RoomType.IsNotMarked())
                                            {
                                                var room = roomStates[i, j].Clone();   // (change a copy: SetNewValue compares against the OLD state in roomStates and may refuse)
                                                room.RoomType = defaultRoom();
                                                room.IsComplete = true;
                                                SetNewValue(room);
                                                justUnmarked = null;
                                                ea.Handled = true;

                                            }
                                            else
                                            {
                                                if (isFirstTimeClickingAnyRoom[level - 1].Value)
                                                {
                                                    var room = roomStates[i, j].Clone();
#if DEBUG
                                                    Console.Write($"Room X at {room.X} and y at {room.Y}");
#endif
                                                    room.RoomType = RoomType.StartEnterFromS;
                                                    room.IsComplete = true;
                                                    SetNewValue(room);
                                                    isFirstTimeClickingAnyRoom[level - 1].Value = false;
                                                    numeral.Opacity = 0.0;
                                                    justUnmarked = null;
                                                    ea.Handled = true;

                                                }
                                                else
                                                {
                                                    var room = roomStates[i, j].Clone();
                                                    var next = RoomTypeExtensions.NextEntranceRoom(room.RoomType);
                                                    if (room.RoomType.IsEntranceRoom())
                                                    {
                                                        room.RoomType = next.Value;
                                                        SetNewValue(room);
                                                        justUnmarked = null;
                                                        ea.Handled = true;

                                                    }
                                                    else
                                                    {
                                                        if (room.RoomType == RoomType.OffTheMap)
                                                            room.RoomType = RoomType.Unmarked;
                                                        else
                                                            room.IsComplete = !room.IsComplete;

                                                        SetNewValue(room);
                                                        justUnmarked = null;
                                                        ea.Handled = true;
                                                    }
                                                }
                                            }
                                        }

                                        // SetNewValue already calls redraw() internally; no second call needed.
                                        ea.Handled = true;
                                    }
                                }
                                else if (ea.InitialPressMouseButton == MouseButton.Right)
                                {
                                    if (grabHelper.IsGrabMode)
                                    {
                                        // right click in move mode: cut out all the rooms that were picked, so they can be carried to a new spot (left click drops them)
                                        if (!grabHelper.HasGrab && grabHelper.HasSelection && !TrackerModelOptions.OldStyleGrab.Value)
                                        {
                                            dungeonHighlightCanvas.Children.Clear();
                                            grabHelper.StartGrabFromSelection(i, j, roomStates, roomIsCircled, horizontalDoors, verticalDoors);
                                            dungeonSourceHighlightCanvas.Children.Clear();
                                            highlightImpl(dungeonSourceHighlightCanvas, grabHelper.Selected, Brushes.Pink);   // where they came from
                                            var (ok, warn) = grabHelper.PreviewDrop(i, j);
                                            highlight(ok, Brushes.Lime);
                                            highlight(warn, Brushes.Yellow);
                                        }
                                        ea.Handled = true;
                                    }
                                    else
                                    {
                                        if (shiftIsDown)
                                        {
                                            CustomComboBoxes.GlobalFlag.popupIsActive = true;
                                            doFloorDropDetailPopup();
                                        }
                                        else
                                        {
                                            // plain right click
                                            await activatePopup(isFirstTimeClickingAnyRoom[level - 1].Value);
                                            isFirstTimeClickingAnyRoom[level - 1].Value = false;
                                            numeral.Opacity = 0.0;
                                            redraw();
                                        }
                                        ea.Handled = true;
                                    }
                                }
                                else if (ea.InitialPressMouseButton == MouseButton.Middle)
                                {
                                    if (!grabHelper.IsGrabMode)  // cannot middle click rooms in grab mode
                                    {
                                        // middle click toggles floor drops, or if none, toggle circles
                                        if (roomStates[i, j].FloorDropDetail.IsNotMarked())
                                        {
                                            roomIsCircled[i, j] = !roomIsCircled[i, j];
                                            // Circle state isn't in the room object so we need to poke the dungeon map changed time directly
                                            TrackerModel.dungeonRoomModelChanged.SetNow();
                                        }
                                        else
                                            roomStates[i, j].ToggleFloorDropBrightness();
                                        redraw();
                                        ea.Handled = true;
                                    }
                                }
                            }
                            CustomComboBoxes.StartImmediate(work());
                        }
                    }, dragBehavior);
                    // (WPF: c.DragOver with the drag's string data, and AllowDrop)
                    DragDrop.AddDragOverHandler(c, (_, ea) =>
                    {
                        var whichButtonStr = ea.DataTransfer.TryGetText();
                        dragBehavior(whichButtonStr);
                    });
                    DragDrop.SetAllowDrop(c, true);
                    // mouse 4 resets a room, and holding it while sliding over more rooms resets each one (not part of the drag-and-drop above, which only follows left / right / middle)
                    c.AddHandler(InputElement.PointerPressedEvent, new EventHandler<PointerPressedEventArgs>((_, ea) =>
                    {
                        if (ea.GetCurrentPoint(c).Properties.PointerUpdateKind == PointerUpdateKind.XButton1Pressed && !CustomComboBoxes.GlobalFlag.popupIsActive && !grabHelper.IsGrabMode)
                        {
                            ea.Handled = true;
                            ea.Pointer.Capture(null);   // no capture, so PointerEntered keeps firing on the other rooms while the button is held
                            resetRoom();
                        }
                    }), RoutingStrategies.Tunnel);
                    c.PointerEntered += (_, ea) =>
                    {
                        if (ea.GetCurrentPoint(c).Properties.IsXButton1Pressed && !CustomComboBoxes.GlobalFlag.popupIsActive && !grabHelper.IsGrabMode)
                            resetRoom();
                    };
                }
            }
            canvasAdd(dungeonCanvas, outlineDrawingCanvases[level - 1], 0.0, 0.0);
            // animation
            {
                Color c(byte t) => Color.FromArgb(t, 255, 165, 0);
                var scb = new SolidColorBrush(c(0));
                // (WPF: a ColorAnimation c(0) → c(180) over 1 s, AutoReverse, started with scb.BeginAnimation; Avalonia can't animate a brush's colour, so it's
                // tweened by hand on a 16 ms timer (linear, as WPF's default), as Graphics.TileHighlightRectangle does. Restarting begins again from c(0); at the end
                // it holds c(0), as WPF's animation (FillBehavior HoldEnd) does after reversing.)
                var animStopwatch = new System.Diagnostics.Stopwatch();
                var animTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16.0), DispatcherPriority.Render, (_, _) => { });
                animTimer.Tick += (_, _) =>
                {
                    var t = animStopwatch.Elapsed.TotalSeconds;
                    if (t >= 2.0)
                    {
                        scb.Color = c(0);
                        animTimer.Stop();
                    }
                    else
                    {
                        var frac = t < 1.0 ? t : 2.0 - t;
                        scb.Color = c((byte)(180.0 * frac));
                    }
                };
                animTimer.Stop();
                void beginAnimation()
                {
                    scb.Color = c(0);
                    animStopwatch.Restart();
                    animTimer.Start();
                }
                var frontRoomHighlightTile = new Rectangle { Width = (double)(13 * 3) + 6.0, Height = (double)(9 * 3) + 6.0, StrokeThickness = 3.0, Stroke = scb, Opacity = 1.0, IsHitTestVisible = false };
                canvasAdd(dungeonBodyCanvas, frontRoomHighlightTile, 0.0, 0.0);
                void animateRoomTile(int x, int y)
                {
                    if (TrackerModelOptions.AnimateTileChanges.Value)
                    {
                        if (roomStates[x, y].RoomType == RoomType.OffTheMap)
                        {
                            Canvas.SetLeft(frontRoomHighlightTile, (double)(x * 51) - 3.0);
                            Canvas.SetTop(frontRoomHighlightTile, (double)(y * 39) - 3.0);
                            frontRoomHighlightTile.Stroke = scb;
                            backRoomHighlightTile.Stroke = null;
                        }
                        else
                        {
                            Canvas.SetLeft(backRoomHighlightTile, (double)(x * 51) - 3.0);
                            Canvas.SetTop(backRoomHighlightTile, (double)(y * 39) - 3.0);
                            frontRoomHighlightTile.Stroke = null;
                            backRoomHighlightTile.Stroke = scb;
                        }
                        beginAnimation();
                    }
                }
                animateDungeonRoomTile = animateRoomTile;
            }
            // "sunglasses"; make it 6 extra wide&tall (3 off each edge) to cover lobby entrance arrows that draw off the map boundaries
            var darkenRect = new Rectangle { Width = dungeonCanvas.Width + 6.0, Height = dungeonCanvas.Height + 6.0, StrokeThickness = 0.0, Fill = Brushes.Black, IsHitTestVisible = false };
            canvasAdd(dungeonCanvas, darkenRect, -3.0, -3.0);
            void doSunglasses() { if (TrackerModelOptions.GiveDungeonTrackerSunglasses.Value) darkenRect.Opacity = 0.15; else darkenRect.Opacity = 0.0; }
            doSunglasses();
            OptionsMenu.dungeonSunglassesChanged += () => doSunglasses();
            canvasAdd(dungeonBodyCanvas, numeral, 0.0, 0.0);  // so numeral displays atop all else
            // highlights
            DungeonHighlightsUI.makeHighlights(level, dungeonTabs, dungeonBodyHighlightCanvas, rightwardCanvas, roomStates, usedTransports,
                                                currentOutlineDisplayState, horizontalDoors, verticalDoors, blockersHoverEvent);
            // save and load
            exportFunctionsLarge[level - 1] = () =>
            {
                var r = new DungeonSaveAndLoad.DungeonModel();
                r.HorizontalDoors = Enumerable.Range(0, 7).Select(i => Enumerable.Range(0, 8).Select(j => horizontalDoors[i, j].State.AsInt()).ToArray()).ToArray();
                r.VerticalDoors = Enumerable.Range(0, 8).Select(i => Enumerable.Range(0, 7).Select(j => verticalDoors[i, j].State.AsInt()).ToArray()).ToArray();
                r.RoomIsCircled = Enumerable.Range(0, 8).Select(i => Enumerable.Range(0, 8).Select(j => roomIsCircled[i, j]).ToArray()).ToArray();
                r.RoomStates = Enumerable.Range(0, 8).Select(i => Enumerable.Range(0, 8).Select(j => DungeonSaveAndLoad.DungeonRoomStateAsModel(roomStates[i, j])).ToArray()).ToArray();
                r.VanillaMapOverlay = currentOutlineDisplayState[level - 1];
                return r;
            };
            importFunctions[level - 1] = (DungeonSaveAndLoad.DungeonModel dm) =>
            {
                skipRedrawInsideCurrentImport = true;
                for (int i = 0; i <= 7; i++)
                    for (int j = 0; j <= 7; j++)
                    {
                        roomIsCircled[i, j] = dm.RoomIsCircled[j][i];
                        var jsonModel = dm.RoomStates[j][i];
                        if (jsonModel != null)
                        {
                            var rs = jsonModel.AsDungeonRoomState();
                            // Set positional metadata so SetNewValue/CopyFrom doesn't corrupt Level/X/Y
                            rs.Level = level;  // 1-indexed
                            rs.X = i;
                            rs.Y = j;
                            if (rs.RoomType != RoomType.Unmarked)
                            {
                                isFirstTimeClickingAnyRoom[level - 1].Value = false;
                                numeral.Opacity = 0.0;
                            }
                            setNewValueFunctions[level - 1][i, j](rs);
                        }
                    }
                // we set the door after the rooms, because DoDoorInference may have inferred some values during the setNewValueFunctions calls, and want to overwrite
                for (int i = 0; i <= 6; i++)
                    for (int j = 0; j <= 7; j++)
                    {
                        horizontalDoors[i, j].State = DoorStateExtensions.FromInt(dm.HorizontalDoors[j][i]);
                        horizontalDoorsPerLevel[level - 1][i, j].State = horizontalDoors[i, j].State;
                    }
                for (int i = 0; i <= 7; i++)
                    for (int j = 0; j <= 6; j++)
                    {
                        verticalDoors[i, j].State = DoorStateExtensions.FromInt(dm.VerticalDoors[j][i]);
                        verticalDoorsPerLevel[level - 1][i, j].State = verticalDoors[i, j].State;
                    }
                currentOutlineDisplayState[level - 1] = dm.VanillaMapOverlay;
                doVanillaOutlineRedraw(outlineDrawingCanvases[level - 1], currentOutlineDisplayState[level - 1]);
                // just redraw everything once at the end
                skipRedrawInsideCurrentImport = false;
                redrawAllDoors();
                redrawAllRooms();
            };
            masterRedrawAllRooms[level - 1] = () => redrawAllRooms();
            OptionsMenu.dungeonOffTileEdgesChanged += () => redrawAllRooms();
            // right-click the dungeon's number in the tab row to clear this whole dungeon map (after a confirmation)
            // (attached to the header itself: a right click anywhere in the tab's panel must not bring this up)
            // (the number is a read-only TextBox: its own Cut / Copy / Paste context menu must not pop up on that same right click)
            ((Control)levelTab.Header).AddHandler(Control.ContextRequestedEvent, new EventHandler<ContextRequestedEventArgs>((_, ea) => ea.Handled = true), RoutingStrategies.Tunnel);
            ((Control)levelTab.Header).PointerPressed += (_, ea) =>
            {
                if (ea.GetCurrentPoint((Control)levelTab.Header).Properties.PointerUpdateKind != PointerUpdateKind.RightButtonPressed) return;
                ea.Handled = true;
                if (CustomComboBoxes.GlobalFlag.popupIsActive || grabHelper.IsGrabMode) return;
                CustomComboBoxes.GlobalFlag.popupIsActive = true;   // (DoModal requires this to be set by the caller)
                async Task clearIt()
                {
                    string pick;
                    try { pick = await PieMenus.ConfirmBoxAsync(cm, $"Clear the whole map of dungeon {labelChar} (rooms, doors, circles and icons)?", new[] { "Clear", "Cancel" }); }
                    finally { CustomComboBoxes.GlobalFlag.popupIsActive = false; }   // (never leave the UI locked, even if the box throws)
                    if (pick == "Clear")
                    {
                        var blank = new DungeonSaveAndLoad.DungeonModel();
                        // (the import reads every array as [row][column], i.e. [j][i])
                        blank.HorizontalDoors = Enumerable.Range(0, 8).Select(_ => new int[7]).ToArray();
                        blank.VerticalDoors = Enumerable.Range(0, 7).Select(_ => new int[8]).ToArray();
                        blank.RoomIsCircled = Enumerable.Range(0, 8).Select(_ => new bool[8]).ToArray();
                        blank.RoomStates = Enumerable.Range(0, 8).Select(_ => Enumerable.Range(0, 8).Select(_ => DungeonSaveAndLoad.DungeonRoomStateAsModel(new DungeonRoomState())).ToArray()).ToArray();
                        importFunctions[level - 1](blank);
                        isFirstTimeClickingAnyRoom[level - 1].Value = true;   // back to a fresh dungeon: the big numeral and the first-click shortcut return
                        numeral.Opacity = 0.25;
                        TrackerModel.dungeonRoomModelChanged.SetNow();   // circles reach a co-op partner only through this
                    }
                }
                CustomComboBoxes.StartImmediate(clearIt());
            };
            await showProgress($"finish dungeon level {level}");
        }
        // end -- for level in 1 to 9 do
        // Now that all masterRedrawAllRooms are populated, fire a redraw so DisplayIconsInDungeonMap hints appear on startup
        if (TrackerModelOptions.DisplayIconsInDungeonMap.Value)
            for (int i = 0; i <= 8; i++)
                masterRedrawAllRooms[i]();
        {
            // summary tab
            var levelTab = new TabItem { Background = Brushes.Black, Foreground = Brushes.Black };
            dungeonTabs.Items.Add(levelTab);
            var header = new TextBox { Width = 22.0, Background = Brushes.Transparent, Foreground = Brushes.White, Text = "S", IsReadOnly = true, IsHitTestVisible = false,
                                     HorizontalContentAlignment = HorizontalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0), Padding = new Thickness(0.0) };
            var headerGrid = new Grid { Background = Graphics.almostBlack, Width = header.Width };
            headerGrid.Children.Add(header);
            // hovering the "S" tab behaves like hovering the blank space in the upper left of the displayed summary tab
            headerGrid.PointerEntered += (_, _) => contentCanvasMouseEnterFunc(10);
            headerGrid.PointerExited += (_, _) => contentCanvasMouseLeaveFunc(10);
            levelTab.Header = headerGrid;
            var contentCanvasWithBadYOffset = new Canvas { Height = (double)(TH + 3 + 27 * 8 + 12 * 7 + 3), Width = (double)(3 + 39 * 8 + 12 * 7 + 3) + localDungeonTrackerPanelWidth, Background = Brushes.Black };
            levelTab.Content = contentCanvasWithBadYOffset;
            //contentCanvas.MouseEnter.Add(fun _ -> contentCanvasMouseEnterFunc(10))  // just the mini's call Enter
            contentCanvasWithBadYOffset.PointerExited += (_, _) => contentCanvasMouseLeaveFunc(10);
            canvasAdd(contentCanvasWithBadYOffset, dummyCanvas, 0.0, 1.0);
            var monsterPriority = // in order of what goes top of the list to surface, down to bottom
                new[] { MonsterDetail.BlueWizzrobe, MonsterDetail.BlueDarknut, MonsterDetail.RedLynel, MonsterDetail.RedGoriya, MonsterDetail.BlueMoblin,         // principal mobs
                   MonsterDetail.Patra, MonsterDetail.Bow, MonsterDetail.Digdogger, MonsterDetail.Dodongo, MonsterDetail.Gleeok, MonsterDetail.Manhandla,    // blocker bosses
                   MonsterDetail.PolsVoice,                                                                                                                  // not exactly a blocker, but bow helps tons
                   MonsterDetail.Keese, MonsterDetail.Gel, MonsterDetail.Stalfos, MonsterDetail.Zol, MonsterDetail.RedTektite, MonsterDetail.Rope,           // easy notables
                   MonsterDetail.Other, MonsterDetail.RedBubble, MonsterDetail.BlueBubble, MonsterDetail.Aquamentus, MonsterDetail.BlueLanmola,              // other notables, rest of bosses
                   MonsterDetail.Moldorm, MonsterDetail.RupeeBoss, MonsterDetail.Other2, MonsterDetail.Traps,                                                // all the
                   MonsterDetail.Wallmaster, MonsterDetail.Likelike, MonsterDetail.Gibdo, MonsterDetail.Vire, MonsterDetail.Unmarked };                      // rest
            if (monsterPriority.Length != MonsterDetailExtensions.All().Length)
                throw new Exception("design-time bug, not all monsters prioritized");
            Image[] findAnyMarkedMonsters(int dunIdx)
            {
                var rs = masterRoomStates[dunIdx];
                var hasMonster = new System.Collections.Generic.Dictionary<MonsterDetail, bool>();
                foreach (var m in monsterPriority) hasMonster.Add(m, false);
                MonsterDetail? lobbyMonster = null;
                for (int i = 0; i <= 7; i++)
                    for (int j = 0; j <= 7; j++)
                    {
                        var monster = rs[i, j].MonsterDetail;
                        hasMonster[monster] = true;
                        var maybeNext = Z1R_Tracker.Models.Z1R_TrackerInterop.RoomTypeExtensions.NextEntranceRoom(rs[i, j].RoomType);
                        if (maybeNext.HasValue && monster != MonsterDetail.Unmarked)
                            lobbyMonster = monster;
                    }
                var result = new List<Image>();
                if (lobbyMonster is MonsterDetail lm) result.Add(Graphics.BMPtoImage(lm.Bmp()));
                foreach (var m in monsterPriority)
                    if (m != MonsterDetail.Unmarked && hasMonster[m] &&
                                !(lobbyMonster.HasValue && lobbyMonster.Value == m))   // don't duplicate the lobby
                        result.Add(Graphics.BMPtoImage(m.Bmp()));
                return result.ToArray();
            }
            var (w, h) = ((int)contentCanvasWithBadYOffset.Width / 3, (int)contentCanvasWithBadYOffset.Height / 3);
            var monsterSPs = Enumerable.Range(0, 9).Select(_ => new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center,
                                                                                    HorizontalAlignment = HorizontalAlignment.Center, Width = 20.0, MaxHeight = (double)h - 20.0 }).ToArray();
            var tinyMaps = Enumerable.Range(0, 9).Select(_ => new Canvas { Width = (double)(13 * 8), Height = (double)(9 * 8) }).ToArray();
            dungeonTabs.SelectionChanged += (_, ea) =>
            {
                try
                {
                    var x = ea.AddedItems[0];
                    if (object.ReferenceEquals(x, levelTab))
                    {
                        dummyCanvas.Children.Clear();
                        for (int i = 0; i <= 8; i++)
                        {
                            // deparent content canvases
                            contentCanvasesWithBadYOffsets[i].Children.Clear();
                            // make them visually here (but hidden by the dummy's lack of opacity), so that updates (like BOARD<->LEVEL) get visually drawn to be picked up by VisualBrush
                            dummyCanvas.Children.Add(contentCanvases[i]);
                            // redraw the monster tables
                            monsterSPs[i].Children.Clear();
                            foreach (var img in findAnyMarkedMonsters(i))
                            {
                                var c = new Canvas { Width = 16.0, Height = 16.0, ClipToBounds = true };
                                canvasAdd(c, img, -1.0, -1.0);
                                monsterSPs[i].Children.Add(c);
                            }
                            // redraw the tiny map
                            tinyMaps[i].Children.Clear();
                            for (int xx = 0; xx <= 7; xx++)
                                for (int y = 0; y <= 7; y++)
                                {
                                    var room = masterRoomStates[i][xx, y];
                                    Control e =
                                        room.RoomType switch
                                        {
                                            RoomType.OffTheMap => null,
                                            var roomType => Graphics.BMPtoImage(room.IsComplete ? roomType.TinyCompletedBI() : roomType.TinyUncompletedBI()),
                                        };
                                    canvasAdd(tinyMaps[i], e, (double)(13 * xx), (double)(9 * y));
                                }
                        }
                        levelTabSelected.Trigger(10);
                    }
                }
                catch { }
            };
            // grid
            var g = Graphics.makeGrid(3, 3, w, h);
            var summaryMode = new TrackerModel.Eventing<int>(TrackerModelOptions.DungeonSummaryTabMode);   // 0=default, 1=preview, 2=detail
            void makeMidiPreviewBehavior(int i, Control fe)
            {
                // midi at 2/3 size looks fine and covers notes area fine when hovering summary of a dungeon
                // (the preview fills the whole notes area, as large as fits without distorting the dungeon; the layout tells us where that area is)
                var midi = new Rectangle { Width = 2.0 * (double)w, Height = 2.0 * (double)h, Fill = new VisualBrush(contentCanvases[i]) { Stretch = Stretch.Uniform } };
                var overlay = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(5.0), Background = Brushes.Black, IsHitTestVisible = false, Child = midi };
                void placeOverlay()
                {
                    if (rightwardCanvas.Tag is ValueTuple<double, double> area && !(System.Double.IsNaN(rightwardCanvas.Width)))
                    {
                        var (top, height) = area;
                        overlay.Width = rightwardCanvas.Width;
                        overlay.Height = height;
                        midi.Width = Math.Max(10.0, rightwardCanvas.Width - 10.0);
                        midi.Height = Math.Max(10.0, height - 10.0);
                        Canvas.SetLeft(overlay, 0.0);
                        Canvas.SetTop(overlay, top);
                    }
                    else
                    {
                        Canvas.SetLeft(overlay, 0.0);
                        Canvas.SetBottom(overlay, 0.0);
                    }
                }
                fe.PointerEntered += (_, _) =>
                {
                    rightwardCanvas.Children.Clear();
                    placeOverlay();
                    rightwardCanvas.Children.Add(overlay);
                    levelTabSelected.Trigger(i + 1);
                    contentCanvasMouseEnterFunc(10 + i + 1);
                };
                fe.PointerExited += (_, _) =>
                {
                    rightwardCanvas.Children.Clear();
                    if (dungeonTabs.SelectedIndex == 9)  // we may have clicked on D4, and MouseLeave fires after tab is switched
                        levelTabSelected.Trigger(10);
                    contentCanvasMouseLeaveFunc(10 + i + 1);
                };
                fe.PointerPressed += (_, _) =>
                {
                    rightwardCanvas.Children.Clear();
                    Dispatcher.UIThread.Post(() =>
                    {
                        dungeonTabs.SelectedIndex = i;
                        levelTabSelected.Trigger(i + 1);
                    });
                };
            }
            Canvas make(int i)
            {
                var mini = new Canvas { Width = (double)w, Height = (double)h, Background = Brushes.Black };
                var miniOuterDock = new DockPanel { Width = (double)w, Height = (double)h };
                canvasAdd(mini, miniOuterDock, 0.0, 0.0);
                {  // decide if show blank canvas for undiscovered/unmarked dungeon, or what's in the tab
                    var c = new Canvas { Width = (double)w, Height = (double)h, Background = Brushes.Black };
                    var n = makeNumeral(getLabelChar(i + 1));
                    n.FontSize = n.FontSize / 3.0;
                    n.Width = n.Width / 3.0;
                    n.Height = n.Height / 3.0;
                    var numeralTop = (double)h / 2.0 - 60.0;   // the numeral and the note under it are centered in the cell together
                    canvasAdd(c, n, ((double)w - n.Width) / 2.0, numeralTop);
                    canvasAdd(mini, c, 0.0, 0.0);
                    var origNumeralColor = n.Foreground;
                    var notFoundText = new TextBox { Text = "not yet found", Foreground = origNumeralColor, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 12.0, Width = (double)w, TextAlignment = TextAlignment.Center };   // (full cell width with centered text, so it sits under the numeral)
                    var completeText = new TextBox { Text = "complete", Foreground = Brushes.Lime, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), Width = (double)w, TextAlignment = TextAlignment.Center };
                    void placeNote(TextBox tbx)
                    {
                        Graphics.matchWpfTextTop(tbx);   // (like the numeral above it, or the two overlap off Windows)
                        canvasAdd(c, tbx, 0.0, numeralTop + 77.0);
                    }
                    placeNote(notFoundText);
                    placeNote(completeText);
                    var miniContent = new Rectangle { Width = (double)w, Height = (double)h, Fill = new VisualBrush(contentCanvases[i]) { Stretch = Stretch.Fill } };   // (WPF's default Stretch is Fill; Avalonia's is Uniform)
                    canvasAdd(c, miniContent, 0.0, 0.0);
                    void decide()
                    {
                        if (TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Value() || isHoveringFQSQ.Value || summaryMode.Value == 1)
                        {
                            // just show their marks
                            miniContent.Opacity = 1.0;
                            c.Opacity = 1.0;
                            n.Opacity = 0.0;
                            notFoundText.Opacity = 0.0;
                            completeText.Opacity = 0.0;
                        }
                        else if (summaryMode.Value == 2)
                            // show the summary created further below
                            c.Opacity = 0.0;
                        else if (isFirstTimeClickingAnyRoom[i].Value && TrackerModel.mapStateSummary.DungeonLocations[i].Equals(TrackerModel.NOTFOUND))
                        {
                            // nothing marked, not found, show a blank canvas with the numeral
                            miniContent.Opacity = 0.0;
                            c.Opacity = 1.0;
                            n.Opacity = 0.7;
                            n.Foreground = origNumeralColor;
                            notFoundText.Opacity = 0.7;
                            completeText.Opacity = 0.0;
                        }
                        else if (TrackerModel.GetDungeon(i).IsComplete)
                        {
                            // blot out their marks, green numeral
                            miniContent.Opacity = 0.0;
                            c.Opacity = 1.0;
                            n.Opacity = 0.5;
                            n.Foreground = Brushes.Lime;
                            notFoundText.Opacity = 0.0;
                            completeText.Opacity = 0.5;
                        }
                        else
                            // show the summary created further below
                            c.Opacity = 0.0;
                    }
                    decide();
                    onEvent(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Changed, _ => decide());
                    onEvent(isFirstTimeClickingAnyRoom[i].Changed, _ => decide());
                    onEvent(TrackerModel.mapStateSummaryComputedEvent.Publish, _ => decide());
                    onEvent(TrackerModel.GetDungeon(i).IsCompleteWasNoticedChanged, _ => decide());
                    onEvent(isHoveringFQSQ.Changed, _ => decide());
                    onEvent(summaryMode.Changed, _ => { TrackerModelOptions.DungeonSummaryTabMode = summaryMode.Value; TrackerModelOptions.writeSettings(); decide(); });
                }
                Border make_canSeeMapBox()
                {
                    var canSeeMapCanvas = new Canvas { Width = 16.0, Height = 16.0, ClipToBounds = true };
                    var mapImg = Graphics.BMPtoImage(Graphics.zi_map_bmp);
                    var atlasImg = Graphics.BMPtoImage(Graphics.zi_atlas);
                    void update() { if (TrackerModel.playerHasAtlas.Invoke(null)) { atlasImg.Opacity = 1.0; mapImg.Opacity = 0.0; } else { atlasImg.Opacity = 0.0; mapImg.Opacity = 1.0; } }
                    update();
                    onEvent(TrackerModel.playerHasAtlasChanged.Publish, _ => update());
                    canvasAdd(canSeeMapCanvas, mapImg, -1.0, -1.0);
                    canvasAdd(canSeeMapCanvas, atlasImg, -1.0, -1.0);
                    canSeeMapCanvas.Opacity = TrackerModel.GetDungeon(i).PlayerCanSeeMapOfThisDungeon ? 1.0 : 0.0;
                    onEvent(TrackerModel.GetDungeon(i).PlayerCanSeeMapOfThisDungeonChanged, _ => canSeeMapCanvas.Opacity = TrackerModel.GetDungeon(i).PlayerCanSeeMapOfThisDungeon ? 1.0 : 0.0);
                    return new Border { Child = canSeeMapCanvas, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(2.0) };
                }
                var canSeeMapBox = make_canSeeMapBox();
                var mapAndMonstersSp = new StackPanel { Orientation = Orientation.Vertical, Width = 20.0 };
                mapAndMonstersSp.Children.Add(canSeeMapBox);
                var g = new Grid { Width = 20.0, Height = (double)h - 20.0 };   // grid to center the element
                g.Children.Add(monsterSPs[i]);
                mapAndMonstersSp.Children.Add(g);
                DockPanel.SetDock(mapAndMonstersSp, Dock.Right);
                miniOuterDock.Children.Add(mapAndMonstersSp);

                var miniMidDock = new DockPanel();
                miniOuterDock.Children.Add(miniMidDock);
                var sltp = MakeSmallLocalTrackerPanel(i, (i == 8 ? null : mainTrackerGhostbusters[i]));
                sltp.Width = 30.0;
                var scaleTrans = new ScaleTransform(2.0 / 3.0, 2.0 / 3.0);
                // (WPF: sltp.LayoutTransform <- scaleTrans; Avalonia has no LayoutTransform property, so sltp sits in a LayoutTransformControl, which is docked instead)
                var sltpScaled = new LayoutTransformControl { LayoutTransform = scaleTrans, Child = sltp };
                DockPanel.SetDock(sltpScaled, Dock.Right);
                miniMidDock.Children.Add(sltpScaled);

                var miniMainDock = new DockPanel();
                DockPanel.SetDock(tinyMaps[i], Dock.Bottom);
                miniMainDock.Children.Add(tinyMaps[i]);

                var tb = new TextBox { Text = $"{getLabelChar(i + 1)}", Foreground = Brushes.White, Background = Brushes.Black, FontSize = 12.0, Width = 10.0, Height = 14.0, IsHitTestVisible = false,
                                        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0),
                                        TextAlignment = TextAlignment.Center, Margin = new Thickness(2.0, 0.0, 0.0, 0.0) };
                if (i != 8)
                {
                    var blockerSP = new StackPanel { Orientation = Orientation.Horizontal };
                    blockerSP.Children.Add(tb);
                    blockerSP.Children.Add(Views.MakeBlockerView(i, 0));
                    blockerSP.Children.Add(Views.MakeBlockerView(i, 1));
                    blockerSP.Children.Add(Views.MakeBlockerView(i, 2));
                    miniMainDock.Children.Add(blockerSP);
                }

                miniMidDock.Children.Add(miniMainDock);

                makeMidiPreviewBehavior(i, mini);
                return mini;
            }
            Button make_summaryModeButton()
            {
                var tb = new TextBox { Text = "", IsReadOnly = true, IsHitTestVisible = false, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(0.0), Background = Graphics.almostBlack,
                                        FontSize = 12.0, Foreground = Brushes.Orange };
                var button = new Button { Content = tb, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Width = 100.0 };
                void updateModeText()
                {
                    if (summaryMode.Value == 1)
                        tb.Text = "Mode: preview";
                    else if (summaryMode.Value == 2)
                        tb.Text = "Mode: detail";
                    else
                        tb.Text = "Mode: default";
                }
                updateModeText();
                button.Click += (_, _) =>
                {
                    if (summaryMode.Value == 0)
                        summaryMode.Value = 1;
                    else if (summaryMode.Value == 1)
                        summaryMode.Value = 2;
                    else
                        summaryMode.Value = 0;
                    updateModeText();
                };
                return button;
            }
            var summaryModeButton = make_summaryModeButton();
            // the mode button and the three lines of help text, centered in the cell (horizontally and vertically) with even spacing
            var dp = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            summaryModeButton.HorizontalAlignment = HorizontalAlignment.Center;
            summaryModeButton.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);   // (tighter spacing so the whole stack fits in the cell and the mode button isn't pushed up out of view)
            dp.Children.Add(summaryModeButton);
            foreach (var (k, line) in new[] { "Dungeon Summary", "Hover to preview tab", "Click to switch tab" }.Select((s, k) => (k, s)))
                dp.Children.Add(new TextBlock { Text = line, Foreground = Brushes.Orange, FontSize = 12.0, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0.0, 0.0, 0.0, (k == 2 ? 0.0 : 8.0)), IsHitTestVisible = false });
            var tb = new Border { Width = (double)w, Height = (double)h, Background = Brushes.Black, Child = dp };
            makeMidiPreviewBehavior(8, tb);
            Graphics.gridAdd(g, tb, 0, 0);
            Graphics.gridAdd(g, make(0), 1, 0);
            Graphics.gridAdd(g, make(1), 2, 0);
            Graphics.gridAdd(g, make(2), 0, 1);
            Graphics.gridAdd(g, make(3), 1, 1);
            Graphics.gridAdd(g, make(4), 2, 1);
            Graphics.gridAdd(g, make(5), 0, 2);
            Graphics.gridAdd(g, make(6), 1, 2);
            Graphics.gridAdd(g, make(7), 2, 2);
            Canvas nine = null;
            onEvent(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Changed, b =>
            {
                // put dungeon 9 over the intro text at the end of the seed, so you can have a screenshot of everything at once
                if (b)
                {
                    if (nine == null)
                        nine = make(8);
                    g.Children.Remove(tb);
                    g.Children.Remove(nine);
                    Graphics.gridAdd(g, nine, 0, 0);
                }
                else
                {
                    if (nine != null)
                        g.Children.Remove(nine);
                    g.Children.Remove(tb);
                    Graphics.gridAdd(g, tb, 0, 0);
                }
            });
            { // put gridlines to bound each dungeon
                var (ccw, cch) = (contentCanvasWithBadYOffset.Width, (int)contentCanvasWithBadYOffset.Height);
                var gc = new Canvas { Width = (double)ccw, Height = (double)cch };
                gc.Children.Add(g);
                canvasAdd(gc, new Line { StartPoint = new Point(0.0, 0.5 + (double)cch / 3.0), EndPoint = new Point((double)ccw, 0.5 + (double)cch / 3.0), Stroke = Brushes.Gray, StrokeThickness = 1.0 }, 0.0, 0.0);
                canvasAdd(gc, new Line { StartPoint = new Point(0.0, 0.5 + (double)cch * 2.0 / 3.0), EndPoint = new Point((double)ccw, 0.5 + (double)cch * 2.0 / 3.0), Stroke = Brushes.Gray, StrokeThickness = 1.0 }, 0.0, 0.0);
                canvasAdd(gc, new Line { StartPoint = new Point(0.5 + (double)ccw / 3.0, 0.0), EndPoint = new Point(0.5 + (double)ccw / 3.0, (double)cch), Stroke = Brushes.Gray, StrokeThickness = 1.0 }, 0.0, 0.0);
                canvasAdd(gc, new Line { StartPoint = new Point(0.5 + (double)ccw * 2.0 / 3.0, 0.0), EndPoint = new Point(0.5 + (double)ccw * 2.0 / 3.0, (double)cch), Stroke = Brushes.Gray, StrokeThickness = 1.0 }, 0.0, 0.0);
                canvasAdd(contentCanvasWithBadYOffset, gc, 0.0, 0.0);
            }
        }
        // end summary tab
        dungeonTabs.SelectedIndex = 9;
        selectDungeonTabEvent.Fired += i => dungeonTabs.SelectedIndex = i;

        string[] exportDungeonModelsJsonLines() => DungeonSaveAndLoad.SaveAllDungeons(exportFunctionsLarge.Select(f => f()).ToArray());
        async Task importDungeonModels(Func<string, Task> showProgress, DungeonSaveAndLoad.DungeonModel[] dma)
        {
            await showProgress("starting dungeon load");
            for (int i = 0; i <= 8; i++)
            {
                importFunctions[i](dma[i]);
                await showProgress($"finished dungeon {i + 1} of 9");
            }
        }
        OptionsMenu.triggerBOARDInsteadOfLEVELOptionChanged(); // to populate BOARD v LEVEL text for all tabs the first time

        AhhGlobalVariables.resetDungeonsForRouters = () =>
        {
            for (int idx = 0; idx <= 8; idx++)
            {
                var rs = masterRoomStates[idx];
                for (int i = 0; i <= 7; i++)
                    for (int j = 0; j <= 7; j++)
                    {
                        var room = rs[i, j];
                        if (!room.FloorDropAppearsBright)
                            room.ToggleFloorDropBrightness();
                        // don't update room completeness; for one, is wonky about e.g. lobby, and also replaying a map is mainly about marked-floor-drops loot, not clearing every room
                        //room.IsComplete <- false
                    }
                masterRedrawAllRooms[idx]();
            }
        };

        return (dungeonTabsWholeCanvas, new Point(3.0 + 3.5 * 39.0 + 3.0 * 12.0, (double)(2 * TH) + 3.0 + 27.0 * 4.5 + 12.0 * 4.0), // point within dungeonTabsWholeCanvas that is destination of 'tab from overworld' cursor warp
                    grabModeTextBlock, exportDungeonModelsJsonLines, importDungeonModels);
    }

    // (port helper: WPF's ToolTipService.SetShowDuration(x, ms). Avalonia has no show duration (a tooltip stays open while the mouse is over its control),
    // so there's nothing to set; kept as a call so the lines still match WPF, as OptionsMenu.cs does)
    private static void setToolTipShowDuration(Control c, int ms) { }
}

// (F#: DungeonUI's `module FloatHelper`, an extension member on System.Double; C# extension methods have to be in a top-level static class)
public static class FloatHelper
{
    public static bool IsBetween(this double self, double x, double y) =>
        self >= x && self <= y;
}
