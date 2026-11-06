// Ported from Z1R_WPF/OverworldMapTileCustomization.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// The module's `overworldAcceleratorTable.Add(...)` lines are top-level `do`s, so they are `_initN` fields at the same positions.
// Every F# `async { }` is an `async Task`; F# options are nullable / `(hasResult, result)` as in CustomComboBoxes.
// Model collections keyed by F# tuples (`TrackerModel.hintShopColors`, `shuffledWoodSwordTiles`, `progressiveTileTier`) keep their
// `System.Tuple` keys; this file's own dictionaries use value tuples.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Z1R_Tracker.Models.Z1R_TrackerInterop;

namespace Z1R_Avalonia;

public static class OverworldMapTileCustomization
{
    public static readonly double OMTW = Graphics.OMTW;
    public static void canvasAdd(Canvas c, Control item, double left, double top) => Graphics.canvasAdd(c, item, left, top);

    ///////////////////////////////////////////////

    // there are a few bits of code that need to 'point' at the owItemGrid in the top right of the main tracker
    // factor out the locations here

    public static class OW_ITEM_GRID_LOCATIONS
    {
        public static readonly double OFFSET = 280.0;  // the x coordinate of grid; the y is always 30. (just below the numbered triforces in HDN)
        // there is a 6x4 grid of icons, each 30x30, here are their 0-based grid coords
        // the armos / letter / ladder / white sword / magical sword item boxes run across the top row, with their icons directly underneath
        public static readonly (int, int) ARMOS_ICON = (0, 1);
        public static readonly (int, int) ARMOS_ITEM_BOX = (0, 0);
        public static readonly (int, int) LETTER_ICON = (1, 1);
        public static readonly (int, int) LETTER_ITEM_BOX = (1, 0);
        public static readonly (int, int) LADDER_ICON = (2, 1);
        public static readonly (int, int) LADDER_ITEM_BOX = (2, 0);
        public static readonly (int, int) WHITE_SWORD_ICON = (3, 1);
        public static readonly (int, int) WHITE_SWORD_ITEM_BOX = (3, 0);
        public static readonly (int, int) MAGS_BOX = (4, 1);   // magical sword icon (icon only), right of the white sword icon
        public static readonly (int, int) MAGS_ITEM_BOX = (4, 0);   // its item box, above it
        public static readonly (int, int) WOOD_SWORD_BOX = (3, 0);
        public static readonly (int, int) BOOMSTICK_BOX = (5, 0);
        public static readonly (int, int) BOMB_BOX = (0, 2);   // bomb and money icons, left of the Ganon box
        public static readonly (int, int) RUPEE_ICON = (1, 2);

        public static readonly (int, int) BLUE_CANDLE_BOX = (2, 1);
        public static readonly (int, int) WOOD_ARROW_BOX = (3, 1);
        public static readonly (int, int) BLUE_RING_BOX = (4, 0);

        public static readonly (int, int) GANON_BOX = (2, 2);
        public static readonly (int, int) ZELDA_BOX = (3, 2);

        public static readonly (int, int) HEARTS = (0, 3);  // and 1,2 and 2,3 and 3,3; nothing at 4,3

        public static (double, double) Locate((int gridX, int gridY) g) => (OFFSET + 30.0 * (double)g.gridX, 30.0 + 30.0 * (double)g.gridY);
    }

    ///////////////////////////////////////////////

    public class MapStateProxy
    {
        private readonly int state;
        public MapStateProxy(int state) { this.state = state; }
        public static int NumStates => Graphics.theInteriorBmpTable.Length;
        public int State => state;
        public bool IsX => state == TrackerModel.MapSquareChoiceDomainHelper.DARK_X;
        public bool IsDungeon => state >= 0 && state < 9;
        public bool IsWarp => state >= 9 && state < 13;
        public bool IsThreeItemShop => TrackerModel.MapSquareChoiceDomainHelper.IsItem(state);
        public bool IsInteresting => !(state == -1 || this.IsX);
        public System.Drawing.Bitmap DefaultInteriorBmp()  // used by grid popup, reminders, and Link destination icons; unchanging so e.g. dungeons in popup are always yellow
        {
            if (state == -1)
                return null;
            else if (this.IsDungeon)
                return TrackerModel.IsHiddenDungeonNumbers.Invoke(null) ? Graphics.theInteriorBmpTable[state][2] : Graphics.theInteriorBmpTable[state][0];
            else
                return Graphics.theInteriorBmpTable[state][0];
        }
    }

    public static (Control, double, double)[] computeExtraDecorationArrow(double topX, double topY, Point pos)
    {
        // in appMainCanvas coordinates:
        // bottom middle of the box, as an arrow target
        var (tx, ty) = (topX + 15.0, topY + 30.0);
        // top middle of the box we are drawing on the map, as an arrow source
        var (sx, sy) = (pos.X + 15.0, pos.Y);
        var (line, triangle) = Graphics.makeArrow(tx, ty, sx, sy, Brushes.Yellow);
        // rectangle for remote box highlight
        var rect = new Rectangle { Width = 30.0, Height = 30.0, Stroke = Brushes.Yellow, StrokeThickness = 3.0 };
        var extraDecorations = new (Control, double, double)[] {
            (line, -pos.X - 3.0, -pos.Y - 3.0),
            (triangle, -pos.X - 3.0, -pos.Y - 3.0),
            (rect, topX - pos.X - 3.0, topY - pos.Y - 3.0),
            };
        return extraDecorations;
    }

    private static readonly (double, double) _sword3 = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.MAGS_ITEM_BOX);
    public static readonly double sword3x = _sword3.Item1, sword3y = _sword3.Item2;
    private static readonly (double, double) _letter = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.LETTER_ITEM_BOX);
    public static readonly double letterX = _letter.Item1, letterY = _letter.Item2;
    private static readonly ((double, double), (double, double)) _armosSword2 = (OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.ARMOS_ITEM_BOX), OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.WHITE_SWORD_ITEM_BOX));
    public static readonly double armosX = _armosSword2.Item1.Item1, armosY = _armosSword2.Item1.Item2;
    public static readonly double sword2x = _armosSword2.Item2.Item1, sword2y = _armosSword2.Item2.Item2;

    public static async Task DoRemoteItemComboBox(CustomComboBoxes.CanvasManager cm, int activationDelta, TrackerModel.Box trackerModelBoxToUpdate,
                                double topX, double topY, Point pos)  // topX,topY,pos are relative to appMainCanvas; top is for tracker box, pos is for mouse-local box
    {
        var extraDecorations = computeExtraDecorationArrow(topX, topY, pos);
        var r = await CustomComboBoxes.DisplayItemComboBox(cm, pos.X, pos.Y, trackerModelBoxToUpdate.CellCurrent(), activationDelta, trackerModelBoxToUpdate.PlayerHas(), extraDecorations, new CustomComboBoxes.GridClickDismissalWarpReturn.WarpToCenter());
        if (r.hasResult)
        {
            var (newBoxCellValue, newPlayerHas) = r.result;
            trackerModelBoxToUpdate.Set(newBoxCellValue, newPlayerHas);
            TrackerModel.forceUpdate();
        }
    }

    // like DoRemoteItemComboBox, but there is no matching box on the overworld item grid, so no arrow/highlight
    public static async Task DoLocalItemComboBox(CustomComboBoxes.CanvasManager cm, int activationDelta, TrackerModel.Box trackerModelBoxToUpdate, Point pos)
    {
        var extraDecorations = new (Control, double, double)[0];
        CustomComboBoxes.itemMenuDomain = trackerModelBoxToUpdate.Domain;
        var r = await CustomComboBoxes.DisplayItemComboBox(cm, pos.X, pos.Y, trackerModelBoxToUpdate.CellCurrent(), activationDelta, trackerModelBoxToUpdate.PlayerHas(), extraDecorations, new CustomComboBoxes.GridClickDismissalWarpReturn.WarpToCenter());
        CustomComboBoxes.itemMenuDomain = TrackerModel.allItemWithHeartShuffleChoiceDomain;
        if (r.hasResult)
        {
            var (newBoxCellValue, newPlayerHas) = r.result;
            trackerModelBoxToUpdate.Set(newBoxCellValue, newPlayerHas);
            TrackerModel.forceUpdate();
        }
    }

    public static bool acceleratorCancelled = false;   // set by an accelerator whose popup was dismissed without choosing: the caller then puts the tile back the way it was
    public static readonly int CANDLE_TAKEN = 1000;   // extra-data value of a wood sword cave tile where the candle was taken
    public static Action<int, int, int> setDecorIconsHook = (_, _, _) => { };   // set by WPFUI (setDecorIcons lives later in compile order)
    // clearing a wood sword cave tile takes back whatever was taken there: the sword / candle (one level), or the shuffled item
    public static void clearWoodSwordCaveItems(int i, int j)
    {
        var sw1 = TrackerModel.MapSquareChoiceDomainHelper.SWORD1;
        var ex = TrackerModel.getOverworldMapExtraData(i, j, sw1);
        var pp = TrackerModel.playerProgressAndTakeAnyHearts;
        var si = TrackerModel.startingItemsAndExtras;
        TrackerModel.recomputePlayerStateSummary();
        if (ex == sw1 && TrackerModel.ProgressiveItems)
            pp.PlayerHasWoodSword.Set(false);   // (Progressive Items: the cave only ever gave the first sword; any other swords are separate)
        else if (ex == sw1)
        {
            var n = Math.Max(0, TrackerModel.playerComputedStateSummary.SwordLevel - 1);
            pp.PlayerHasWoodSword.Set(n >= 1);
            si.PlayerHasWhiteSword.Set(n >= 2);
            si.PlayerHasMagicalSword.Set(n >= 3);
        }
        else if (ex == CANDLE_TAKEN)
        {
            var n = Math.Max(0, TrackerModel.playerComputedStateSummary.CandleLevel - 1);
            pp.PlayerHasBlueCandle.Set(n >= 1);
            si.PlayerHasRedCandle.Set(n >= 2);
        }
        if (TrackerModel.shuffledWoodSwordTiles.Contains(Tuple.Create(i, j)))
        {
            TrackerModel.sword1Box.Set(-1, TrackerModel.PlayerHas.SKIPPED);
            TrackerModel.shuffledWoodSwordTiles.Remove(Tuple.Create(i, j));
        }
        TrackerModel.progressiveTileTier.Remove(Tuple.Create(i, j, sw1));
        TrackerModel.setOverworldMapExtraData(i, j, sw1, 0);
        TrackerModel.recomputePlayerStateSummary();   // the item that was found here no longer counts
        TrackerModel.forceUpdate();
    }
    // a take-any cave tile remembers what was taken there (which take-any heart slot got marked, and whether that was the blue candle), so clearing the tile takes it back (this session only)
    public static readonly Dictionary<(int, int), (int, bool, int)> takeAnyTaken = new Dictionary<(int, int), (int, bool, int)>();   // (slot, candle was newly taken, kind: 1 heart / 2 candle / 3 potion)
    public static void clearTakeAnyItems(int i, int j)
    {
        if (takeAnyTaken.TryGetValue((i, j), out var v))
        {
            var (slot, candle, _kind) = v;
            var pp = TrackerModel.playerProgressAndTakeAnyHearts;
            pp.SetTakeAnyHeart(slot, 0);
            if (candle) pp.PlayerHasBlueCandle.Set(false);
            takeAnyTaken.Remove((i, j));
            TrackerModel.recomputePlayerStateSummary();
            TrackerModel.forceUpdate();
        }
    }
    // the take any hearts on the main screen were clicked: kind 0 = empty, 1 = heart, 2 = blue candle, 3 = potion. Keeps the take any cave tile(s) tied to that slot in sync
    public static void setTakeAnySlotKind(int slot, int kind)
    {
        var pp = TrackerModel.playerProgressAndTakeAnyHearts;
        var prevKind = pp.GetTakeAnyKind(slot);
        var caves = takeAnyTaken.Where(kv => kv.Value.Item1 == slot).Select(kv => kv.Key).ToList();
        if (kind == 0 && caves.Count != 0)
        {
            // emptied: the cave is no longer 'taken' (this also gives back the candle if the cave had given it)
            foreach (var (i, j) in caves)
            {
                clearTakeAnyItems(i, j);
                TrackerModel.setOverworldMapExtraData(i, j, TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY, 0);
            }
        }
        else
        {
            pp.SetTakeAnyKind(slot, kind);
            if (caves.Count == 0)
            {
                if (kind == 2 && prevKind != 2) pp.PlayerHasBlueCandle.Set(true);
                else if (kind != 2 && prevKind == 2) pp.PlayerHasBlueCandle.Set(false);
            }
            else
            {
                foreach (var (i, j) in caves)
                {
                    var (s, candle, _) = takeAnyTaken[(i, j)];
                    var candle2 = kind == 2 ? true : prevKind == 2 ? false : candle;
                    if (kind == 2 && prevKind != 2) pp.PlayerHasBlueCandle.Set(true);
                    else if (kind != 2 && prevKind == 2) pp.PlayerHasBlueCandle.Set(false);
                    takeAnyTaken[(i, j)] = (s, candle2, kind);
                }
            }
        }
        if (kind == 0) pp.SetTakeAnyKind(slot, 0);
        TrackerModel.recomputePlayerStateSummary();
        TrackerModel.forceUpdate();
    }
    public static readonly int SHUFFLED_ITEM = 2000;   // extra-data value of a white / magical sword cave tile that holds a shuffled item
    // (port helper: F#'s `seq { for x in 0..15 do for y in 0..7 do yield (x,y) }`)
    private static IEnumerable<(int, int)> allTiles()
    {
        for (int x = 0; x <= 15; x++)
            for (int y = 0; y <= 7; y++)
                yield return (x, y);
    }
    // true if some tile on the map already holds the shuffled item for this kind of cave (collected or not), so 'Shuffled Item' is crossed out for any other tile of that kind
    public static bool shuffledItemAlreadyPlaced(int state) =>
        allTiles().Any(t => TrackerModel.overworldMapMarks[t.Item1, t.Item2].Current() == state && TrackerModel.getOverworldMapExtraData(t.Item1, t.Item2, state) == SHUFFLED_ITEM);
    // the white / magical sword caves get the same treatment as the wood sword cave (without the candle): took the sword, shuffled item, or just a sword icon
    public static void clearOtherSwordCaveItems(int i, int j, int state)
    {
        var ex = TrackerModel.getOverworldMapExtraData(i, j, state);
        var si = TrackerModel.startingItemsAndExtras;
        if (ex == state)
        {
            if (state == TrackerModel.MapSquareChoiceDomainHelper.SWORD2) si.PlayerHasWhiteSword.Set(false); else si.PlayerHasMagicalSword.Set(false);
        }
        var box = state == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 ? TrackerModel.sword2Box : TrackerModel.sword3Box;
        if (ex == SHUFFLED_ITEM || (ex == state && !(shuffledItemAlreadyPlaced(state)))) box.Set(-1, TrackerModel.PlayerHas.SKIPPED);   // the item found here (or the sword taken here) is taken back
        TrackerModel.setOverworldMapExtraData(i, j, state, 0);
        TrackerModel.recomputePlayerStateSummary();
        TrackerModel.forceUpdate();
    }
    // the letter cave gets the same treatment: took the letter, or a shuffled item
    public static void clearLetterCaveItems(int i, int j)
    {
        var ex = TrackerModel.getOverworldMapExtraData(i, j, TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER);
        if (ex == SHUFFLED_ITEM || (ex == TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER && !(shuffledItemAlreadyPlaced(TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER)))) TrackerModel.letterBox.Set(-1, TrackerModel.PlayerHas.SKIPPED);   // the item found here (or the letter taken here) is taken back
        TrackerModel.setOverworldMapExtraData(i, j, TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER, 0);
        TrackerModel.recomputePlayerStateSummary();
        TrackerModel.forceUpdate();
    }
    public static void clearSwordCaveItemsForState(int i, int j, int state)
    {
        if (state == TrackerModel.MapSquareChoiceDomainHelper.SWORD1) clearWoodSwordCaveItems(i, j);
        else if (state == TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER) clearLetterCaveItems(i, j);
        else if (state == TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY) clearTakeAnyItems(i, j);
        else if (state == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || state == TrackerModel.MapSquareChoiceDomainHelper.SWORD3) clearOtherSwordCaveItems(i, j, state);
    }
    public static bool isSwordCaveState(int state) => state == TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY || state == TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER || state == TrackerModel.MapSquareChoiceDomainHelper.SWORD1 || state == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || state == TrackerModel.MapSquareChoiceDomainHelper.SWORD3;
    // (state -> accelerator(cm, tileCanvas, i, j))
    public static readonly Dictionary<int, Func<CustomComboBoxes.CanvasManager, Canvas, int, int, Task>> overworldAcceleratorTable = new Dictionary<int, Func<CustomComboBoxes.CanvasManager, Canvas, int, int, Task>>();
    // put back what was taken at a take any cave (after its chooser was opened again and dismissed without choosing)
    public static void restoreTakeAny(int i, int j, int slot, bool candle, int kind, int extra)
    {
        var pp = TrackerModel.playerProgressAndTakeAnyHearts;
        pp.SetTakeAnyKind(slot, kind);
        if (candle) pp.PlayerHasBlueCandle.Set(true);
        takeAnyTaken[(i, j)] = (slot, candle, kind);
        TrackerModel.setOverworldMapExtraData(i, j, TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY, extra);
        TrackerModel.recomputePlayerStateSummary();
        TrackerModel.forceUpdate();
    }
    private static readonly bool _init1 = init1();
    private static bool init1()
    {
        overworldAcceleratorTable.Add(TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY, async (CustomComboBoxes.CanvasManager cm, Canvas _c, int i, int j) =>
        {
            var pp = TrackerModel.playerProgressAndTakeAnyHearts;
            var hadTaken = takeAnyTaken.ContainsKey((i, j));
            var (prevSlot, prevCandle, prevKind) = hadTaken ? takeAnyTaken[(i, j)] : (0, false, 0);
            var prevExtra = TrackerModel.getOverworldMapExtraData(i, j, TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY);
            // a left click on a take any cave that already has something taken does nothing (right click changes or clears it)
            if (!(hadTaken && prevKind != 0))
            {
                clearTakeAnyItems(i, j);   // choosing again replaces what was taken here before
                PieMenus.lastTakeAnyKind = 0;
                PieMenus.lastTakeAnyDismissed = false;
                var heartsBefore = Enumerable.Range(0, 4).Select(h => pp.GetTakeAnyHeart(h)).ToArray();
                var candleBefore = pp.PlayerHasBlueCandle.Value();
                var shouldMarkTakeAnyAsComplete =
                    TrackerModelOptions.Overworld.UseOldPopups.Value ? await PieMenus.TakeAnyPieMenuAsync(cm, 666.0)   // the old picture popup
                    : await PieMenus.TakeAnyButtonBoxAsync(cm);
                if (PieMenus.lastTakeAnyDismissed && hadTaken && prevKind != 0 && !(TrackerModelOptions.Overworld.UseOldPopups.Value))
                {
                    // the chooser was opened again and dismissed without choosing: what was taken here stays taken
                    restoreTakeAny(i, j, prevSlot, prevCandle, prevKind, prevExtra);
                }
                else
                {
                    // remember what was taken here (which heart slot changed, and whether the blue candle was newly taken) so clearing the tile can take it back
                    for (int h = 0; h <= 3; h++)
                    {
                        if (pp.GetTakeAnyHeart(h) != heartsBefore[h] && !(takeAnyTaken.ContainsKey((i, j))))
                        {
                            takeAnyTaken[(i, j)] = (h, (!candleBefore && pp.PlayerHasBlueCandle.Value()), PieMenus.lastTakeAnyKind);
                            pp.SetTakeAnyKind(h, PieMenus.lastTakeAnyKind);
                        }
                    }
                    TrackerModel.setOverworldMapExtraData(i, j, TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY, shouldMarkTakeAnyAsComplete ? TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY : 0);
                }
            }
        });
        return true;
    }
    private static readonly bool _init2 = init2();
    private static bool init2()
    {
        overworldAcceleratorTable.Add(TrackerModel.MapSquareChoiceDomainHelper.SWORD1, async (CustomComboBoxes.CanvasManager cm, Canvas c, int i, int j) =>
        {
            var pp = TrackerModel.playerProgressAndTakeAnyHearts;
            var si = TrackerModel.startingItemsAndExtras;
            var s = TrackerModel.playerComputedStateSummary;
            acceleratorCancelled = false;
            var hadShuffled = TrackerModel.shuffledWoodSwordTiles.Remove(Tuple.Create(i, j));
            // only one wood sword cave can hold a non-sword item: once another wood sword cave has had the candle taken or holds the shuffled item, this one can only be the sword (or a sword icon / leave)
            var candleTakenElsewhere =
                allTiles().Any(t => t != (i, j) && TrackerModel.overworldMapMarks[t.Item1, t.Item2].Current() == TrackerModel.MapSquareChoiceDomainHelper.SWORD1 && TrackerModel.getOverworldMapExtraData(t.Item1, t.Item2, TrackerModel.MapSquareChoiceDomainHelper.SWORD1) == CANDLE_TAKEN);
            // and the wood sword can only be taken once
            var swordTakenElsewhere =
                allTiles().Any(t => t != (i, j) && TrackerModel.overworldMapMarks[t.Item1, t.Item2].Current() == TrackerModel.MapSquareChoiceDomainHelper.SWORD1 && TrackerModel.getOverworldMapExtraData(t.Item1, t.Item2, TrackerModel.MapSquareChoiceDomainHelper.SWORD1) == TrackerModel.MapSquareChoiceDomainHelper.SWORD1);
            PieMenus.legacyDisabledChoices = TrackerModel.LegacyMode ? new List<string> { "Shuffled Item", "Sword Icon" } : new List<string>();
            PieMenus.crossedOutChoices = new List<string>();   // (start clean: a sword cave that skipped its popup used to leave 'Took Sword' struck out here)
            var crossedBase = (swordTakenElsewhere ? new List<string> { "Took Sword" } : new List<string>()).Concat(TrackerModel.LegacyMode ? new List<string> { "Shuffled Item", "Sword Icon" } : new List<string>()).ToList();   // (Legacy Mode: no shuffled item in the wood sword cave)
            if (TrackerModel.shuffledWoodSwordTiles.Count > 0 || candleTakenElsewhere) PieMenus.crossedOutChoices = crossedBase.Concat(new[] { "Shuffled Item", "Took Candle" }).ToList();
            else if (hadShuffled) PieMenus.crossedOutChoices = crossedBase.Concat(new[] { "Shuffled Item" }).ToList();
            else if (swordTakenElsewhere || TrackerModel.LegacyMode) PieMenus.crossedOutChoices = crossedBase;
            // one box of 4 buttons; it also supports the Contextual_TakeThis_* hotkeys (sword / candle / none)
            string choice;
            if (TrackerModelOptions.Overworld.UseOldPopups.Value)
            {
                // the old picture popup: sword / candle / leave, plus a 4th choice for the less common 'Shuffled Item' / 'Sword Icon'
                var r = await PieMenus.TakeThisChoicePieMenuAsync(cm, 666.0);
                switch (r)
                {
                    case 1: choice = "Took Sword"; break;
                    case 2: choice = "Took Candle"; break;
                    case 3: choice = await PieMenus.CaveChoiceBoxAsync(cm, new[] { "Shuffled Item", "Sword Icon" }); break;
                    default: choice = "Leave"; break;
                }
            }
            else choice = await PieMenus.TakeThisButtonBoxAsync(cm);
            PieMenus.crossedOutChoices = new List<string>();   // (the old pie menu path doesn't consume it)
            PieMenus.legacyDisabledChoices = new List<string>();
            var doneValue = 0;   // 0 = not taken yet, SWORD1 = took the sword, CANDLE_TAKEN = took the candle
            switch (choice)
            {
                case "Took Sword" when !swordTakenElsewhere:
                {
                    if (TrackerModel.ProgressiveItems) TrackerModel.progressiveTileTier[Tuple.Create(i, j, TrackerModel.MapSquareChoiceDomainHelper.SWORD1)] = TrackerModel.progressiveSwordTier();   // (the tile keeps the tier this sword was collected at: P2 when another sword came first)
                    var n = TrackerModel.ProgressiveItems ? 1 : Math.Min(3, s.SwordLevel + 1);   // (Progressive Items: the tier logic does the upgrading, so this just records the sword)
                    pp.PlayerHasWoodSword.Set(n >= 1);
                    si.PlayerHasWhiteSword.Set(n >= 2);
                    si.PlayerHasMagicalSword.Set(n >= 3);
                    doneValue = TrackerModel.MapSquareChoiceDomainHelper.SWORD1;
                    break;
                }
                case "Took Candle":
                {
                    var n = Math.Min(2, s.CandleLevel + 1);
                    pp.PlayerHasBlueCandle.Set(n >= 1);
                    si.PlayerHasRedCandle.Set(n >= 2);
                    doneValue = CANDLE_TAKEN;
                    break;
                }
                case "Shuffled Item":
                {
                    TrackerModel.shuffledWoodSwordTiles.Add(Tuple.Create(i, j));
                    var pos = c.TranslatePoint(new Point(OMTW / 2.0 - 15.0, 1.0), cm.AppMainCanvas).Value;  // place to draw the local box
                    await DoLocalItemComboBox(cm, 0, TrackerModel.sword1Box, pos);
                    break;
                }
                case "Sword Icon":
                {
                    var before = TrackerModel.owSwordIcons[i, j];
                    setDecorIconsHook(i, j, TrackerModel.decorIconsToggle(before, TrackerModel.ITEMS.WOODSWORD));
                    if (hadShuffled)
                        TrackerModel.shuffledWoodSwordTiles.Add(Tuple.Create(i, j));   // the cave keeps its shuffled item; only the icon is added
                    // like any other marker icon, the tile itself becomes an 'unmarked, finished' tile that shows the icon
                    else if (!(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).IsEmpty))
                        TrackerModel.overworldMapMarks[i, j].AttemptToSet(TrackerModel.MapSquareChoiceDomainHelper.DARK_X);
                    break;
                }
                case "Leave": break;   // as before: marks the cave as not taken
                default: acceleratorCancelled = true; break;   // clicked outside the choices: nothing gets placed
            }
            if (!acceleratorCancelled)
                TrackerModel.setOverworldMapExtraData(i, j, TrackerModel.MapSquareChoiceDomainHelper.SWORD1, doneValue);
            else if (hadShuffled)
                TrackerModel.shuffledWoodSwordTiles.Add(Tuple.Create(i, j));   // dismissed: the cave still holds its shuffled item
        });
        return true;
    }
    private static readonly bool _init3 = init3();
    private static bool init3()
    {
        overworldAcceleratorTable.Add(TrackerModel.MapSquareChoiceDomainHelper.ARMOS, async (CustomComboBoxes.CanvasManager cm, Canvas c, int _i, int _j) =>
        {
            var pos = c.TranslatePoint(new Point(OMTW / 2.0 - 15.0, 1.0), cm.AppMainCanvas).Value;  // place to draw the local box
            await DoRemoteItemComboBox(cm, 0, TrackerModel.armosBox, armosX, armosY, pos);
        });
        return true;
    }
    public static Func<CustomComboBoxes.CanvasManager, Canvas, int, int, Task> swordCaveAccelerator(int state, TrackerModel.Box box, double boxX, double boxY, int decorIdent, Action takeSword) =>
        async (CustomComboBoxes.CanvasManager cm, Canvas c, int i, int j) =>
        {
            acceleratorCancelled = false;
            var hadShuffled = TrackerModel.getOverworldMapExtraData(i, j, state) == SHUFFLED_ITEM;
            PieMenus.legacyDisabledChoices = TrackerModel.LegacyMode ? (state == TrackerModel.MapSquareChoiceDomainHelper.SWORD3 ? new List<string> { "Shuffled Item", "Sword Icon" } : new List<string> { "Sword Icon" }) : new List<string>();   // (Legacy Mode: no sword in a shop)
            var crossed = new List<string>();
            if (hadShuffled || shuffledItemAlreadyPlaced(state) || (TrackerModel.LegacyMode && state == TrackerModel.MapSquareChoiceDomainHelper.SWORD3)) crossed.Add("Shuffled Item");   // this cave, or another one of the same kind, already holds a shuffled item (Legacy Mode: the magical sword cave never does)
            if (TrackerModel.LegacyMode) crossed.Add("Sword Icon");
            if (TrackerModel.ProgressiveItems) crossed.Add("Took Sword");   // Progressive Items: the white and magical swords are not separate items
            PieMenus.crossedOutChoices = crossed;
            // an item already put in this cave's item box (that is not just the cave's own sword) is its shuffled item: no menu, the cave starts out holding it (collected or not as the box says)
            var preplaced = box.CellCurrent() != -1 && box.CellCurrent() != decorIdent && !hadShuffled && !(shuffledItemAlreadyPlaced(state));
            var autoTake = TrackerModel.LegacyMode && state == TrackerModel.MapSquareChoiceDomainHelper.SWORD3 && !TrackerModel.ProgressiveItems;   // (Legacy Mode: the magical sword cave only ever holds the sword, so no menu)
            var choice = autoTake ? "Placed" : preplaced ? "Preplaced" : await PieMenus.CaveChoiceBoxAsync(cm, new[] { "Took Sword", "Shuffled Item", "Sword Icon" });
            PieMenus.crossedOutChoices = new List<string>();   // (when the popup was skipped nothing consumed it)
            PieMenus.legacyDisabledChoices = new List<string>();
            var doneValue = 0;   // 0 = nothing yet, state = took the sword, SHUFFLED_ITEM = the cave holds a shuffled item
            switch (choice)
            {
                case "Took Sword":
                    takeSword();
                    // the sword also fills in this cave's item box (and sword icon) in the overworld item area up top
                    // (but never over a shuffled item already found in one of these caves)
                    if (!(hadShuffled || shuffledItemAlreadyPlaced(state)))
                    {
                        // (AttemptToSet: that sword may already be in another item box, and the item pool would overflow)
                        box.AttemptToSet((state == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 ? TrackerModel.ITEMS.WHITESWORD : TrackerModel.ITEMS.MAGICALSWORD), TrackerModel.PlayerHas.YES);
                        TrackerModel.forceUpdate();
                    }
                    doneValue = state;
                    break;
                case "Placed":   // Legacy Mode magical sword cave: placed like a cave holding a shuffled item - the cave with its item box showing the sword, not collected yet (a click collects it, matching the item box)
                    if (box.CellCurrent() != decorIdent) box.AttemptToSet(decorIdent, TrackerModel.PlayerHas.NO);
                    doneValue = SHUFFLED_ITEM;
                    break;
                case "Preplaced": doneValue = SHUFFLED_ITEM; break;
                case "Shuffled Item":
                {
                    doneValue = SHUFFLED_ITEM;
                    var pos = c.TranslatePoint(new Point(OMTW / 2.0 - 15.0, 1.0), cm.AppMainCanvas).Value;  // place to draw the local box
                    await DoRemoteItemComboBox(cm, 0, box, boxX, boxY, pos);
                    break;
                }
                case "Sword Icon":
                {
                    var before = TrackerModel.owSwordIcons[i, j];
                    setDecorIconsHook(i, j, TrackerModel.decorIconsToggle(before, decorIdent));
                    if (hadShuffled) doneValue = SHUFFLED_ITEM;   // the cave keeps its shuffled item; only the icon is added
                    else if (!(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).IsEmpty))
                        TrackerModel.overworldMapMarks[i, j].AttemptToSet(TrackerModel.MapSquareChoiceDomainHelper.DARK_X);
                    break;
                }
                default: acceleratorCancelled = true; break;
            }
            if (!acceleratorCancelled)
                TrackerModel.setOverworldMapExtraData(i, j, state, doneValue);
        };
    // the letter cave: took the letter, a shuffled item, or a letter icon (like the sword caves; the item is shown in a local box that points at the letter item box up top)
    private static readonly bool _init4 = init4();
    private static bool init4()
    {
        overworldAcceleratorTable.Add(TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER, async (CustomComboBoxes.CanvasManager cm, Canvas c, int i, int j) =>
        {
            acceleratorCancelled = false;
            if (TrackerModel.getOverworldMapExtraData(i, j, TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER) == SHUFFLED_ITEM || shuffledItemAlreadyPlaced(TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER)) PieMenus.crossedOutChoices = new List<string> { "Shuffled Item" };   // this cave, or another letter cave, already holds a shuffled item
            // an item already put in the letter item box (other than the letter itself) is this cave's shuffled item: no menu, the cave starts out holding it
            var preplaced = TrackerModel.letterBox.CellCurrent() != -1 && TrackerModel.letterBox.CellCurrent() != TrackerModel.ITEMS.LETTER
                            && TrackerModel.getOverworldMapExtraData(i, j, TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER) != SHUFFLED_ITEM && !(shuffledItemAlreadyPlaced(TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER));
            var choice = preplaced ? "Preplaced" : await PieMenus.CaveChoiceBoxAsync(cm, new[] { "Took Letter", "Shuffled Item" });
            var doneValue = 0;   // 0 = nothing yet, THE_LETTER = took the letter, SHUFFLED_ITEM = the cave holds a shuffled item
            switch (choice)
            {
                case "Took Letter":
                    // fills in the letter item box up top (but never over a shuffled item already found in a letter cave)
                    if (!(shuffledItemAlreadyPlaced(TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER)))
                    {
                        TrackerModel.letterBox.AttemptToSet(TrackerModel.ITEMS.LETTER, TrackerModel.PlayerHas.YES);
                        TrackerModel.forceUpdate();
                    }
                    doneValue = TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER;
                    break;
                case "Preplaced": doneValue = SHUFFLED_ITEM; break;
                case "Shuffled Item":
                {
                    doneValue = SHUFFLED_ITEM;
                    var pos = c.TranslatePoint(new Point(OMTW / 2.0 - 15.0, 1.0), cm.AppMainCanvas).Value;  // place to draw the local box
                    await DoRemoteItemComboBox(cm, 0, TrackerModel.letterBox, letterX, letterY, pos);
                    break;
                }
                default: acceleratorCancelled = true; break;
            }
            if (!acceleratorCancelled)
                TrackerModel.setOverworldMapExtraData(i, j, TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER, doneValue);
        });
        return true;
    }
    private static readonly bool _init5 = init5();
    private static bool init5()
    {
        overworldAcceleratorTable.Add(TrackerModel.MapSquareChoiceDomainHelper.SWORD2,
            swordCaveAccelerator(TrackerModel.MapSquareChoiceDomainHelper.SWORD2, TrackerModel.sword2Box, sword2x, sword2y, TrackerModel.ITEMS.WHITESWORD, () => TrackerModel.startingItemsAndExtras.PlayerHasWhiteSword.Set(true)));
        overworldAcceleratorTable.Add(TrackerModel.MapSquareChoiceDomainHelper.SWORD3,
            swordCaveAccelerator(TrackerModel.MapSquareChoiceDomainHelper.SWORD3, TrackerModel.sword3Box, sword3x, sword3y, TrackerModel.ITEMS.MAGICALSWORD, () => TrackerModel.startingItemsAndExtras.PlayerHasMagicalSword.Set(true)));
        return true;
    }

    public static System.Drawing.Bitmap makeSwordLeftSideFullTileBmpN(int swordState, int interiorIndex)
    {
        var interiorBmp = Graphics.theInteriorBmpTable[swordState][interiorIndex];
        var fullTileBmp = new System.Drawing.Bitmap(16 * 3, 11 * 3);
        var BG = System.Drawing.Color.FromArgb((int)Graphics.TRANS_BG.A, (int)Graphics.TRANS_BG.R, (int)Graphics.TRANS_BG.G, (int)Graphics.TRANS_BG.B);   // the compiler is being stupid, clone this color to workaround
        for (int px = 0; px <= 16 * 3 - 1; px++)
            for (int py = 0; py <= 11 * 3 - 1; py++)
            {
                if (px >= 1 * 3 && px < 6 * 3 && py >= 1 * 3 && py < 10 * 3)
                    fullTileBmp.SetPixel(px, py, interiorBmp.GetPixel(px - 1 * 3, py - 1 * 3));
                else
                    fullTileBmp.SetPixel(px, py, BG);
            }
        return fullTileBmp;
    }
    public static System.Drawing.Bitmap makeSwordLeftSideFullTileBmp(int swordState) => makeSwordLeftSideFullTileBmpN(swordState, 0);
    public static readonly System.Drawing.Bitmap sword1LeftSideFullTileBmp = makeSwordLeftSideFullTileBmp(TrackerModel.MapSquareChoiceDomainHelper.SWORD1);
    public static readonly System.Drawing.Bitmap sword1LeftSideDarkFullTileBmp = makeSwordLeftSideFullTileBmpN(TrackerModel.MapSquareChoiceDomainHelper.SWORD1, 1);   // dark version, sword at the left so an item fits beside it
    // the take-any cave tile with its icon at the left (dark), so what was taken fits beside it; falls back to the plain dark tile
    private static System.Drawing.Bitmap make_takeAnyLeftSideDarkFullTileBmp()
    {
        try { return makeSwordLeftSideFullTileBmpN(TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY, 1); }
        catch { return Graphics.theFullTileBmpTable[TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY][1]; }
    }
    public static readonly System.Drawing.Bitmap takeAnyLeftSideDarkFullTileBmp = make_takeAnyLeftSideDarkFullTileBmp();
    public static readonly System.Drawing.Bitmap sword2LeftSideFullTileBmp = makeSwordLeftSideFullTileBmp(TrackerModel.MapSquareChoiceDomainHelper.SWORD2);
    public static readonly System.Drawing.Bitmap sword2LeftSideDarkFullTileBmp = makeSwordLeftSideFullTileBmpN(TrackerModel.MapSquareChoiceDomainHelper.SWORD2, 1);
    public static readonly System.Drawing.Bitmap sword3LeftSideDarkFullTileBmp = makeSwordLeftSideFullTileBmpN(TrackerModel.MapSquareChoiceDomainHelper.SWORD3, 1);
    // hint shop tiles can have their purple background changed (scroll the mouse wheel on the tile): 0 = purple, 1 = red, 2 = green, 3 = orange (4 = grey, used only by the Spot Summary until the user picks a color)
    public static readonly Dictionary<Tuple<int, int>, int> hintShopColors = TrackerModel.hintShopColors;   // (lives in the model so it is saved and loaded with everything else)
    public static int lastHintShopColor = 0;   // the color the user last chose; new hint shop tiles start with it
    public static bool hintShopColorChosen = false;   // true once the user has scrolled a hint shop to a color (the Spot Summary shows its hint boxes grey until then)
    public static int hintShopColorOf(int i, int j)
    {
        if (hintShopColors.TryGetValue(Tuple.Create(i, j), out var k))
            return k;
        else
        {
            hintShopColors[Tuple.Create(i, j)] = lastHintShopColor;   // (pin it, so later color changes don't recolor tiles already drawn)
            return lastHintShopColor;
        }
    }
    private static System.Drawing.Bitmap recolorPurple(System.Drawing.Bitmap src, int kind)
    {
        bool isPurple(System.Drawing.Color p) => p.A > 0 && (int)p.R > (int)p.G + 25 && (int)p.B > (int)p.G + 25;
        var refMax = 1;
        for (int x = 0; x <= src.Width - 1; x++)
            for (int y = 0; y <= src.Height - 1; y++)
            {
                var p = src.GetPixel(x, y);
                if (isPurple(p)) refMax = Math.Max(refMax, Math.Max((int)p.R, (int)p.B));
            }
        var (tr, tg, tb) = kind switch { 1 => (220, 30, 30), 2 => (30, 170, 50), 4 => (150, 150, 150), _ => (255, 150, 0) };
        var r = new System.Drawing.Bitmap(src.Width, src.Height);
        for (int x = 0; x <= src.Width - 1; x++)
            for (int y = 0; y <= src.Height - 1; y++)
            {
                var p = src.GetPixel(x, y);
                if (isPurple(p))
                {
                    var f = (double)Math.Max((int)p.R, (int)p.B) / (double)refMax;
                    r.SetPixel(x, y, System.Drawing.Color.FromArgb((int)p.A, Math.Min(255, (int)((double)tr * f)), Math.Min(255, (int)((double)tg * f)), Math.Min(255, (int)((double)tb * f))));
                }
                else r.SetPixel(x, y, p);
            }
        return r;
    }
    private static readonly Dictionary<(int, bool), System.Drawing.Bitmap> hintShopRecolorCache = new Dictionary<(int, bool), System.Drawing.Bitmap>();
    public static System.Drawing.Bitmap hintShopTileBmp(int kind, bool dark)
    {
        var src = Graphics.theFullTileBmpTable[TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP][(dark ? 1 : 0)];
        if (kind == 0) return src;
        if (hintShopRecolorCache.TryGetValue((kind, dark), out var b))
            return b;
        var r = recolorPurple(src, kind);
        hintShopRecolorCache[(kind, dark)] = r;
        return r;
    }
    // the hint shop icon shown in the overworld tile menu, in the color the user last chose
    private static readonly Dictionary<int, System.Drawing.Bitmap> hintShopInteriorCache = new Dictionary<int, System.Drawing.Bitmap>();
    public static System.Drawing.Bitmap hintShopMenuBmp(int kind)
    {
        var src = Graphics.theInteriorBmpTable[TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP][0];
        if (kind == 0) return src;
        if (hintShopInteriorCache.TryGetValue(kind, out var b))
            return b;
        var r = recolorPurple(src, kind);
        hintShopInteriorCache[kind] = r;
        return r;
    }
    // ---- shop icons: tiny pixel-font bitmaps (3x5 letters / digits), used by the overworld menu's 'Shop' cell and the Spot Summary ----
    private static readonly IDictionary<char, string[]> shopFont = new Dictionary<char, string[]> {
        ['S'] = new[] { "###", "#..", "###", "..#", "###" },
        ['H'] = new[] { "#.#", "#.#", "###", "#.#", "#.#" },
        ['O'] = new[] { "###", "#.#", "#.#", "#.#", "###" },
        ['P'] = new[] { "###", "#.#", "###", "#..", "#.." },
        ['1'] = new[] { ".#.", "##.", ".#.", ".#.", "###" },
        ['2'] = new[] { "###", "..#", "###", "#..", "###" },
        ['3'] = new[] { "###", "..#", "###", "..#", "###" },
        ['4'] = new[] { "#.#", "#.#", "###", "..#", "..#" },
        ['5'] = new[] { "###", "#..", "###", "..#", "###" } };
    private static void drawShopGlyph(System.Drawing.Bitmap bmp, char ch, int x0, int y0, int scale, System.Drawing.Color color)
    {
        var rows = shopFont[ch];
        for (int r = 0; r <= 4; r++)
            for (int c = 0; c <= 2; c++)
                if (rows[r][c] == '#')
                    for (int dx = 0; dx <= scale - 1; dx++)
                        for (int dy = 0; dy <= scale - 1; dy++)
                        {
                            var (x, y) = (x0 + c * scale + dx, y0 + r * scale + dy);
                            if (x >= 0 && x < bmp.Width && y >= 0 && y < bmp.Height) bmp.SetPixel(x, y, color);
                        }
    }
    // the 'Shop' cell of the overworld menu: S-H-O-P stepping diagonally down and to the right, centered in the 21x27 cell
    private static System.Drawing.Bitmap make_shopMenuBmp()
    {
        var bmp = new System.Drawing.Bitmap(21, 27);
        using (var g = System.Drawing.Graphics.FromImage(bmp)) { g.Clear(System.Drawing.Color.Black); }
        var gold = System.Drawing.Color.FromArgb(255, 255, 215, 0);
        var k = 0;
        foreach (var ch in "SHOP") { drawShopGlyph(bmp, ch, 5 + 3 * k, 4 + 5 * k, 1, gold); k++; }
        return bmp;
    }
    public static readonly System.Drawing.Bitmap shopMenuBmp = make_shopMenuBmp();
    // a numbered shop for the Spot Summary: tiny 'SHOP' across the top and the shop's number big underneath
    private static readonly Dictionary<int, System.Drawing.Bitmap> shopNumberBmps = new Dictionary<int, System.Drawing.Bitmap>();
    public static System.Drawing.Bitmap shopNumberBmp(int n)
    {
        if (shopNumberBmps.TryGetValue(n, out var b))
            return b;
        var bmp = new System.Drawing.Bitmap(21, 27);
        using (var g = System.Drawing.Graphics.FromImage(bmp)) { g.Clear(System.Drawing.Color.Black); }
        var gold = System.Drawing.Color.FromArgb(255, 255, 215, 0);
        var k = 0;
        foreach (var ch in "SHOP") { drawShopGlyph(bmp, ch, 3 + 4 * k, 1, 1, gold); k++; }
        drawShopGlyph(bmp, n.ToString()[0], 6, 9, 3, System.Drawing.Color.White);
        shopNumberBmps[n] = bmp;
        return bmp;
    }
    // the armos tile with its icon moved to the left edge (so the item found under the armos fits beside it); the icon is whatever differs from the tile background
    public static System.Drawing.Bitmap armosLeftSideTileBmp(bool dark)
    {
        var src = Graphics.theFullTileBmpTable[TrackerModel.MapSquareChoiceDomainHelper.ARMOS][(dark ? 1 : 0)];
        var bg = src.GetPixel(0, 0);
        int minX = src.Width, minY = src.Height, maxX = -1, maxY = -1;
        for (int x = 0; x <= src.Width - 1; x++)
            for (int y = 0; y <= src.Height - 1; y++)
                if (src.GetPixel(x, y).ToArgb() != bg.ToArgb())
                {
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
        if (maxX < 0) return src;
        var dst = new System.Drawing.Bitmap(src.Width, src.Height);
        for (int x = 0; x <= dst.Width - 1; x++)
            for (int y = 0; y <= dst.Height - 1; y++)
                dst.SetPixel(x, y, bg);
        var shift = minX - 3;   // the icon starts 3px (one art pixel) from the left edge, like the cave tiles
        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                dst.SetPixel(x - shift, y, src.GetPixel(x, y));
        return dst;
    }
    public static readonly Lazy<System.Drawing.Bitmap> armosLeftSideBrightBmp = new Lazy<System.Drawing.Bitmap>(() => armosLeftSideTileBmp(false));
    public static readonly Lazy<System.Drawing.Bitmap> armosLeftSideDarkBmp = new Lazy<System.Drawing.Bitmap>(() => armosLeftSideTileBmp(true));
    // the marker-icon look used for items placed on a tile by themselves: the icon with a thick black outline (see WPFUI.decorIconBmp)
    public static System.Drawing.Bitmap withDecorOutline(System.Drawing.Bitmap src)
    {
        var B = 3;
        var (w, h) = (src.Width, src.Height);
        var dst = new System.Drawing.Bitmap(w + 2 * B, h + 2 * B);
        bool isInk(int x, int y) { var p = src.GetPixel(x, y); return p.A > 0 && ((int)p.R + (int)p.G + (int)p.B) > 40; }
        for (int x = 0; x <= w - 1; x++)
            for (int y = 0; y <= h - 1; y++)
                if (isInk(x, y))
                    for (int dx = 0; dx <= 2 * B; dx++)
                        for (int dy = 0; dy <= 2 * B; dy++)
                            dst.SetPixel(x + dx, y + dy, System.Drawing.Color.Black);
        for (int x = 0; x <= w - 1; x++)
            for (int y = 0; y <= h - 1; y++)
            {
                var p = src.GetPixel(x, y);
                if (p.A > 0) dst.SetPixel(x + B, y + B, p);
            }
        return dst;
    }
    // the letter (not shuffled) on a tile: the same letter sprite as the item menus, outlined like a marker icon, centered on a plain tile (no brown cave background)
    public static System.Drawing.Bitmap letterTileBmp(bool dark, bool leftAligned)
    {
        // the item-menu letter sprite; its own near-black background pixels are dropped so only the letter itself gets the outline (no black box)
        var raw = CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.LETTER, null);
        var cleaned = new System.Drawing.Bitmap(raw.Width, raw.Height);
        for (int x = 0; x <= raw.Width - 1; x++)
            for (int y = 0; y <= raw.Height - 1; y++)
            {
                var p = raw.GetPixel(x, y);
                if (p.A > 0 && ((int)p.R + (int)p.G + (int)p.B) > 40) cleaned.SetPixel(x, y, p);
            }
        var icon = withDecorOutline(cleaned);   // black outline hugging the letter
        var BG = System.Drawing.Color.FromArgb((int)Graphics.TRANS_BG.A, (int)Graphics.TRANS_BG.R, (int)Graphics.TRANS_BG.G, (int)Graphics.TRANS_BG.B);
        var tile = new System.Drawing.Bitmap(16 * 3, 11 * 3);
        for (int px = 0; px <= 16 * 3 - 1; px++)
            for (int py = 0; py <= 11 * 3 - 1; py++)
                tile.SetPixel(px, py, BG);
        var ox = leftAligned ? 0 : (tile.Width - icon.Width) / 2;
        var oy = (tile.Height - icon.Height) / 2;
        for (int x = 0; x <= icon.Width - 1; x++)
            for (int y = 0; y <= icon.Height - 1; y++)
            {
                var p0 = icon.GetPixel(x, y);
                var p = dark ? System.Drawing.Color.FromArgb((int)p0.A, (int)p0.R / 2, (int)p0.G / 2, (int)p0.B / 2) : p0;   // (darkened by hand so the transparent pixels stay transparent: no black box)
                var (tx, ty) = (x + ox, y + oy);
                if (p.A > 0 && tx >= 0 && ty >= 0 && tx < tile.Width && ty < tile.Height) tile.SetPixel(tx, ty, p);
            }
        return tile;
    }
    public static System.Drawing.Bitmap letterPlainTileBmp(bool dark) => letterTileBmp(dark, false);
    public static System.Drawing.Bitmap letterLeftSideFullTileBmp() => letterTileBmp(false, true);
    public static System.Drawing.Bitmap letterLeftSideDarkFullTileBmp() => letterTileBmp(true, true);
    // the green check mark drawn on a collected marker icon (same as WPFUI.decorCheckmark)
    public static Control caveCheckmark(double iconSize)
    {
        var s = Math.Max(8.0, iconSize * 0.55);
        var c = new Canvas { Width = s, Height = s, IsHitTestVisible = false };
        List<Point> pts() => new List<Point> { new Point(s * 0.08, s * 0.55), new Point(s * 0.38, s * 0.88), new Point(s * 0.94, s * 0.12) };
        // (WPF sets StrokeStartLineCap and StrokeEndLineCap; Avalonia has one StrokeLineCap, Round for both as in WPF)
        c.Children.Add(new Polyline { Points = pts(), Stroke = Brushes.Black, StrokeThickness = Math.Max(3.5, s * 0.42), StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round });
        c.Children.Add(new Polyline { Points = pts(), Stroke = Brushes.LimeGreen, StrokeThickness = Math.Max(2.0, s * 0.24), StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round });
        return c;
    }
    // a cave's shuffled item, drawn to the right of the (left-aligned) sword icon at the sword icon's height, in the same style as an item placed on a tile by itself
    public static Canvas makeBmpBesideSword(System.Drawing.Bitmap bmp)
    {
        var swordIconHeight = 27.0;
        var ob = bmp != null ? withDecorOutline(bmp) : null;
        var w = ob != null ? Math.Min(27.0, swordIconHeight * (double)ob.Width / (double)ob.Height) : 0.0;
        var iconCanvas = new Canvas { Width = w + 2.0, Height = swordIconHeight + 2.0 };
        if (ob != null)
        {
            var icon = Graphics.BMPtoImage(ob);
            icon.Stretch = Stretch.Uniform;
            icon.Height = swordIconHeight;
            icon.Width = w;
            canvasAdd(iconCanvas, icon, 1.0, 1.0);
        }
        return iconCanvas;
    }
    public static Canvas makeCaveItemBesideSword(TrackerModel.Box box)
    {
        var c = makeBmpBesideSword(CustomComboBoxes.boxCurrentBMP(box.CellCurrent(), null));
        if (box.PlayerHas().Equals(TrackerModel.PlayerHas.YES) && box.CellCurrent() != -1)
        {
            var ck = caveCheckmark(27.0);
            canvasAdd(c, ck, 1.0 + (c.Width - 2.0 - ck.Width) / 2.0, 1.0 + (27.0 - ck.Height) / 2.0);
        }
        c.Background = Brushes.Transparent;   // so the whole icon area gets mouse events
        ToolTip.SetTip(c, "Left click: item collected / right click: not collected\n(click the same one again to un-check)");
        c.PointerPressed += (_, ea) =>
        {
            // (WPF: `ButtonState = Pressed && (ChangedButton = Left || ChangedButton = Right)`; on a press, PointerUpdateKind names the button that went down)
            var kind = ea.GetCurrentPoint(c).Properties.PointerUpdateKind;
            if (kind == PointerUpdateKind.LeftButtonPressed || kind == PointerUpdateKind.RightButtonPressed)
            {
                ea.Handled = true;
                var desire = CustomComboBoxes.MouseButtonEventArgsToPlayerHas(ea);
                box.SetPlayerHas(desire.Equals(box.PlayerHas()) ? TrackerModel.PlayerHas.NO : desire);
                TrackerModel.forceUpdate();
            }
        };
        return c;
    }
    // the green check drawn over the sword icon of a sword cave tile where the sword was taken (centered on the tile)
    public static (Canvas, double, double) takenSwordCheck()
    {
        var ck = caveCheckmark(27.0);
        var host = new Canvas { Width = ck.Width, Height = ck.Height, IsHitTestVisible = false };   // (a Canvas, like the other tile decorations)
        host.Children.Add(ck);
        return (host, OMTW / 2.0 - ck.Width / 2.0, 33.0 / 2.0 - ck.Height / 2.0);
    }
    // Progressive Items: the sword taken at a wood sword cave is drawn as its tier sprite (P1, or P2 when another sword was collected first) with the check
    public static (Canvas, double, double) takenProgressiveSwordCheck(int i, int j)
    {
        var tier = TrackerModel.progressiveTileTier.TryGetValue(Tuple.Create(i, j, TrackerModel.MapSquareChoiceDomainHelper.SWORD1), out var v) ? Math.Max(1, Math.Min(4, v)) : 1;
        var host = new Canvas { Width = 21.0, Height = 27.0, Background = Brushes.Black, IsHitTestVisible = false };   // (black, to cover the plain sword on the cave tile)
        var img = Graphics.BMPtoImage(Graphics.ow_progressive_tier_menu_bmps[tier]);
        img.Stretch = Stretch.Uniform;
        img.Width = 21.0;
        img.Height = 27.0;
        host.Children.Add(img);
        var ck = caveCheckmark(27.0);
        canvasAdd(host, ck, (21.0 - ck.Width) / 2.0, (27.0 - ck.Height) / 2.0);
        return (host, (OMTW - 21.0) / 2.0, 3.0);
    }
    public static readonly System.Drawing.Bitmap sword3LeftSideFullTileBmp = makeSwordLeftSideFullTileBmp(TrackerModel.MapSquareChoiceDomainHelper.SWORD3);

    // in a shop tile, the bomb (item 1) is drawn as the real bomb icon fitted to its slot rather than from the thin 3x7 item strip
    public static void paintBombInSlot(System.Drawing.Bitmap tile, int x0, int y0)
    {
        var b = Graphics.bombFitBmp(9, 21, Graphics.itemBackgroundColor);
        for (int x = 0; x <= 8; x++)
            for (int y = 0; y <= 20; y++)
                tile.SetPixel(x0 + x, y0 + y, b.GetPixel(x, y));
    }
    public static readonly Dictionary<(int, int), System.Drawing.Bitmap> twoItemShopBmpCache = new Dictionary<(int, int), System.Drawing.Bitmap>();
    public static readonly Dictionary<(int, int, int), System.Drawing.Bitmap> threeItemShopBmpCache = new Dictionary<(int, int, int), System.Drawing.Bitmap>();
    public static System.Drawing.Bitmap makeTwoItemShopBmp(int item1, int item2)  // 0-based, -1 for blank
    {
        if (item1 == item2)
        {
            var state = item1 + TrackerModel.MapSquareChoiceDomainHelper.ARROW;
            return Graphics.theFullTileBmpTable[state][0];
        }
        else
        {
            if (twoItemShopBmpCache.TryGetValue((item1, item2), out var r))
                return r;
            // cons up a two-item shop image
            var tile = new System.Drawing.Bitmap(16 * 3, 11 * 3);
            for (int px = 0; px <= 16 * 3 - 1; px++)
                for (int py = 0; py <= 11 * 3 - 1; py++)
                {
                    // two-icon area
                    if (px / 3 >= 3 && px / 3 <= 11 && py / 3 >= 1 && py / 3 <= 9)
                        tile.SetPixel(px, py, Graphics.itemBackgroundColor);
                    else
                        tile.SetPixel(px, py, Graphics.TRANS_BG);
                    if (item1 >= 0)
                    {
                        // icon 1
                        if (px / 3 >= 4 && px / 3 <= 6 && py / 3 >= 2 && py / 3 <= 8)
                        {
                            var c = Graphics.itemsBMP.GetPixel(item1 * 3 + px / 3 - 4, py / 3 - 2);
                            if (c.ToArgb() != System.Drawing.Color.Black.ToArgb())
                                tile.SetPixel(px, py, c);
                        }
                    }
                    if (item2 >= 0)
                    {
                        // icon 2
                        if (px / 3 >= 8 && px / 3 <= 10 && py / 3 >= 2 && py / 3 <= 8)
                        {
                            var c = Graphics.itemsBMP.GetPixel(item2 * 3 + px / 3 - 8, py / 3 - 2);
                            if (c.ToArgb() != System.Drawing.Color.Black.ToArgb())
                                tile.SetPixel(px, py, c);
                        }
                    }
                }
            if (item1 == 1) paintBombInSlot(tile, 12, 6);
            if (item2 == 1) paintBombInSlot(tile, 24, 6);
            twoItemShopBmpCache.Add((item1, item2), tile);
            return tile;
        }
    }

    public static System.Drawing.Bitmap makeThreeItemShopBmp(int item1, int item2, int item3)  // 0-based, -1 for blank
    {
        if (threeItemShopBmpCache.TryGetValue((item1, item2, item3), out var r))
            return r;
        var tile = new System.Drawing.Bitmap(16 * 3, 11 * 3);
        for (int px = 0; px <= 16 * 3 - 1; px++)
            for (int py = 0; py <= 11 * 3 - 1; py++)
            {
                if (px / 3 >= 3 && px / 3 <= 11 && py / 3 >= 1 && py / 3 <= 9)
                    tile.SetPixel(px, py, Graphics.itemBackgroundColor);
                else
                    tile.SetPixel(px, py, Graphics.TRANS_BG);
                if (item1 >= 0)
                {
                    if (px / 3 >= 3 && px / 3 <= 5 && py / 3 >= 2 && py / 3 <= 8)
                    {
                        var c = Graphics.itemsBMP.GetPixel(item1 * 3 + px / 3 - 3, py / 3 - 2);
                        if (c.ToArgb() != System.Drawing.Color.Black.ToArgb())
                            tile.SetPixel(px, py, c);
                    }
                }
                if (item2 >= 0)
                {
                    if (px / 3 >= 6 && px / 3 <= 8 && py / 3 >= 2 && py / 3 <= 8)
                    {
                        var c = Graphics.itemsBMP.GetPixel(item2 * 3 + px / 3 - 6, py / 3 - 2);
                        if (c.ToArgb() != System.Drawing.Color.Black.ToArgb())
                            tile.SetPixel(px, py, c);
                    }
                }
                if (item3 >= 0)
                {
                    if (px / 3 >= 9 && px / 3 <= 11 && py / 3 >= 2 && py / 3 <= 8)
                    {
                        var c = Graphics.itemsBMP.GetPixel(item3 * 3 + px / 3 - 9, py / 3 - 2);
                        if (c.ToArgb() != System.Drawing.Color.Black.ToArgb())
                            tile.SetPixel(px, py, c);
                    }
                }
            }
        if (item1 == 1) paintBombInSlot(tile, 9, 6);
        if (item2 == 1) paintBombInSlot(tile, 18, 6);
        if (item3 == 1) paintBombInSlot(tile, 27, 6);
        threeItemShopBmpCache.Add((item1, item2, item3), tile);
        return tile;
    }

    public static readonly bool[,] temporarilyDisplayHiddenOverworldTileMarks = new bool[16, 8];
    public static bool ShouldHide(int state, int i, int j)
    {
        var r =
            (temporarilyDisplayHiddenOverworldTileMarks[i, j] || TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Value())
                ? false
                : TrackerModel.MapSquareChoiceDomainHelper.AsTrackerModelOptionsOverworldTilesToHide(state).Value;
        //printfn "SH(%d)=%A" state r
        return r;
    }

    public static bool IsHideableShopItem(int i) => // 0-based
        (i == 0 && TrackerModel.playerComputedStateSummary.ArrowLevel > 0)
        || (i == 2 && TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBoomBook.Value())
        || (i == 3 && TrackerModel.playerComputedStateSummary.CandleLevel > 0)
        || (i == 4 && TrackerModel.playerComputedStateSummary.RingLevel > 0)
        || (i == 6 && TrackerModel.playerComputedStateSummary.HaveAnyKey)
        || (i == 5 && TrackerModelOptions.OverworldTilesToHide.AlwaysHideMeatShops.Value);

    // (port helper: an F# list literal of (element, x, y) decorations)
    private static List<(Control, double, double)> decos(params (Control, double, double)[] xs) => new List<(Control, double, double)>(xs);
    public static (bool, System.Drawing.Bitmap, List<(Control, double, double)>) GetIconBMPAndExtraDecorations(CustomComboBoxes.CanvasManager cm, MapStateProxy ms, int i, int j)   // returns: (shouldAppearLikeDarkX,iconBmp,[decos])
    {
        if (ms.State == -1)
        {
            // Unmarked tile; still check for a stored item icon (right-click icon feature on AlwaysEmpty tiles)
            if (TrackerModelOptions.AllowItemIconOnNonShopTile.Value)
            {
                var item2 = TrackerModel.getShopItem2_1based(i, j);
                if (item2 != 0)
                    return (false, Graphics.theFullTileBmpTable[item2 - 1 + TrackerModel.MapSquareChoiceDomainHelper.ARROW][0], decos());
                else
                    return (false, null, decos());
            }
            else
                return (false, null, decos());
        }
        else if (ms.IsThreeItemShop)
        {
            var items = new List<int>();
            items.Add(ms.State - TrackerModel.MapSquareChoiceDomainHelper.ARROW);  // 0-based
            var item2 = TrackerModel.getShopItem2_1based(i, j);
            var item3 = TrackerModel.getShopItem3_1based(i, j);
            if (item2 != 0) items.Add(item2 - 1);   // 0-based
            if (item3 != 0) items.Add(item3 - 1);   // 0-based
            List<int> itemsToShow;
            if (ShouldHide(TrackerModel.MapSquareChoiceDomainHelper.SHOP, i, j))
            {
                itemsToShow = new List<int>();
                foreach (var x in items)
                    if (!(IsHideableShopItem(x)))
                        itemsToShow.Add(x);
            }
            else
                itemsToShow = items;
            if (itemsToShow.Count == 3)
            {
                var tile = makeThreeItemShopBmp(itemsToShow[0], itemsToShow[1], itemsToShow[2]);
                return (false, tile, decos());
            }
            else if (itemsToShow.Count == 2)
            {
                var tile = makeTwoItemShopBmp(itemsToShow[0], itemsToShow[1]);
                return (false, tile, decos());
            }
            else if (itemsToShow.Count == 1)
                return (false, Graphics.theFullTileBmpTable[itemsToShow[0] + TrackerModel.MapSquareChoiceDomainHelper.ARROW][0], decos());
            else
                return (true, Graphics.theFullTileBmpTable[ms.State][0], decos());
        }
        // secrets and potion letter default to being dark
        else if (ms.State == TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET)
        {
            if (TrackerModel.getOverworldMapExtraData(i, j, ms.State) == TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET)
                return (false, Graphics.theFullTileBmpTable[ms.State][0], decos());
            else
                return (ShouldHide(ms.State, i, j), Graphics.theFullTileBmpTable[ms.State][1], decos());
        }
        else if (ms.State == TrackerModel.MapSquareChoiceDomainHelper.MEDIUM_SECRET)
        {
            if (TrackerModel.getOverworldMapExtraData(i, j, ms.State) == TrackerModel.MapSquareChoiceDomainHelper.MEDIUM_SECRET)
                return (false, Graphics.theFullTileBmpTable[ms.State][0], decos());
            else
                return (ShouldHide(ms.State, i, j), Graphics.theFullTileBmpTable[ms.State][1], decos());
        }
        else if (ms.State == TrackerModel.MapSquareChoiceDomainHelper.SMALL_SECRET)
        {
            if (TrackerModel.getOverworldMapExtraData(i, j, ms.State) == TrackerModel.MapSquareChoiceDomainHelper.SMALL_SECRET)
                return (false, Graphics.theFullTileBmpTable[ms.State][0], decos());
            else
                return (ShouldHide(ms.State, i, j), Graphics.theFullTileBmpTable[ms.State][1], decos());
        }
        else if (ms.State == TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER)
        {
            var ex = TrackerModel.getOverworldMapExtraData(i, j, ms.State);
            if (ex == SHUFFLED_ITEM && !(TrackerModel.letterBox.IsDone()))
            {
                IEnumerable<(Control, double, double)> extraDecorationsF(Point boxPos)
                {
                    var extraDecorations = computeExtraDecorationArrow(letterX, letterY, boxPos);
                    return extraDecorations;
                }
                return (false, letterLeftSideFullTileBmp(), decos((Views.MakeBoxItemWithExtraDecorations(cm, TrackerModel.letterBox, false, extraDecorationsF).Item1, OMTW - 30.0, 1.0)));
            }
            else if (ex == SHUFFLED_ITEM)
                return (ShouldHide(ms.State, i, j), letterLeftSideDarkFullTileBmp(), decos((makeCaveItemBesideSword(TrackerModel.letterBox), 20.0, 2.0)));
            else if (ex == ms.State || TrackerModel.letterBox.IsDone())   // took the letter: dark, like the white sword cave once taken
            {
                // with other marker icons on the tile, the letter moves to the left like the other cave tiles do, leaving room for them on the right
                var hasOtherIcons = !(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).IsEmpty);
                return (ShouldHide(ms.State, i, j), (hasOtherIcons ? letterLeftSideDarkFullTileBmp() : letterPlainTileBmp(true)), decos());
            }
            else
            {
                var hasOtherIcons = !(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).IsEmpty);
                return (false, (hasOtherIcons ? letterLeftSideFullTileBmp() : letterPlainTileBmp(false)), decos());   // not yet taken: bright
            }
        }
        // door repair always dark (but light in the grid selector)
        else if (ms.State == TrackerModel.MapSquareChoiceDomainHelper.DOOR_REPAIR_CHARGE)
            return (ShouldHide(ms.State, i, j), Graphics.theFullTileBmpTable[ms.State][1], decos());
        // armos dark if player already got
        else if (ms.State == TrackerModel.MapSquareChoiceDomainHelper.ARMOS)
        {
            if (TrackerModel.armosBox.CellCurrent() != -1)
            {
                // the item found under the armos is drawn beside the armos icon (click it to mark it collected)
                if (TrackerModel.armosBox.IsDone())
                    return (ShouldHide(ms.State, i, j), armosLeftSideDarkBmp.Value, decos((makeCaveItemBesideSword(TrackerModel.armosBox), 20.0, 2.0)));
                else
                    return (false, armosLeftSideBrightBmp.Value, decos((makeCaveItemBesideSword(TrackerModel.armosBox), 20.0, 2.0)));
            }
            else if (TrackerModel.armosBox.IsDone())
                return (ShouldHide(ms.State, i, j), Graphics.theFullTileBmpTable[ms.State][1], decos());
            else
                return (false, Graphics.theFullTileBmpTable[ms.State][0], decos());
        }
        // MMG always bright
        else if (ms.State == TrackerModel.MapSquareChoiceDomainHelper.MONEY_MAKING_GAME)
            return (ShouldHide(ms.State, i, j), Graphics.theFullTileBmpTable[ms.State][0], decos());
        // take any and sword1 default to being light (accelerators often darken them)
        else if (ms.State == TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY)
        {
            if (TrackerModel.getOverworldMapExtraData(i, j, ms.State) == TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY)
            {
                if (takeAnyTaken.TryGetValue((i, j), out var taken) && taken.Item3 != 0)
                {
                    var kind = taken.Item3;
                    // what was taken (heart / candle / potion) beside the icon, with a green check
                    var bmp = kind == 1 ? Graphics.owHeartFull_bmp : kind == 2 ? Graphics.blue_candle_bmp : Graphics.red_potion_bmp;
                    var itemCanvas = makeBmpBesideSword(bmp);
                    var ck = caveCheckmark(27.0);
                    canvasAdd(itemCanvas, ck, 1.0 + (itemCanvas.Width - 2.0 - ck.Width) / 2.0, 1.0 + (27.0 - ck.Height) / 2.0);
                    itemCanvas.Background = Brushes.Transparent;
                    itemCanvas.PointerPressed += (_, ea) => { if (ea.GetCurrentPoint(itemCanvas).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed) ea.Handled = true; };   // left clicking the item taken from a take any cave does nothing (right click still works)
                    return (ShouldHide(ms.State, i, j), takeAnyLeftSideDarkFullTileBmp, decos((itemCanvas, 20.0, 2.0)));
                }
                else
                    return (ShouldHide(ms.State, i, j), Graphics.theFullTileBmpTable[ms.State][1], decos());
            }
            else
                return (false, Graphics.theFullTileBmpTable[ms.State][0], decos());
        }
        else if (ms.State == TrackerModel.MapSquareChoiceDomainHelper.SWORD1 && TrackerModel.shuffledWoodSwordTiles.Contains(Tuple.Create(i, j)) && TrackerModel.sword1Box.IsDone())
        {
            // the shuffled item was found: the dark sword cave tile with that item to the right of the sword, the same height as the sword icon
            var iconCanvas = makeCaveItemBesideSword(TrackerModel.sword1Box);
            return (ShouldHide(ms.State, i, j), sword1LeftSideFullTileBmp, decos((iconCanvas, 20.0, 2.0)));   // (the sword icon stays bright)
        }
        else if (ms.State == TrackerModel.MapSquareChoiceDomainHelper.SWORD1 && TrackerModel.shuffledWoodSwordTiles.Contains(Tuple.Create(i, j)) && !(TrackerModel.sword1Box.IsDone()))
            return (false, sword1LeftSideFullTileBmp, decos((Views.MakeBoxItemWithExtraDecorations(cm, TrackerModel.sword1Box, false, null).Item1, OMTW - 30.0, 1.0)));
        else if (ms.State == TrackerModel.MapSquareChoiceDomainHelper.SWORD1)
        {
            var ex = TrackerModel.getOverworldMapExtraData(i, j, ms.State);
            if (ex == CANDLE_TAKEN)
            {
                // took the candle: styled like a shuffled item - the dark sword cave tile with the candle beside the sword, at the sword's height
                var candleCanvas = makeBmpBesideSword(Graphics.blue_candle_bmp);
                var ck = caveCheckmark(27.0);
                canvasAdd(candleCanvas, ck, 1.0 + (candleCanvas.Width - 2.0 - ck.Width) / 2.0, 1.0 + (27.0 - ck.Height) / 2.0);
                candleCanvas.Background = Brushes.Transparent;
                candleCanvas.PointerPressed += (_, ea) => ea.Handled = true;   // clicking the candle does nothing
                return (ShouldHide(ms.State, i, j), sword1LeftSideDarkFullTileBmp, decos((candleCanvas, 20.0, 2.0)));
            }
            else if (ex == TrackerModel.MapSquareChoiceDomainHelper.SWORD1)
            {
                var (host, x, y) = TrackerModel.ProgressiveItems ? takenProgressiveSwordCheck(i, j) : takenSwordCheck();
                return (ShouldHide(ms.State, i, j), Graphics.theFullTileBmpTable[ms.State][0], decos((host, x, y)));   // (the sword stays bright, with a check; Progressive Items: as its P tier sprite)
            }
            else
                return (false, Graphics.theFullTileBmpTable[ms.State][0], decos());
        }
        // hint shop default to being light, user can darken if bought all, or if was white/magic sword hint they already saw
        // actually, helpful hints are so rarely played, make default dark
        else if (ms.State == TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP)
        {
            if (TrackerModel.getOverworldMapExtraData(i, j, ms.State) == TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP)
            {
                var (host, x, y) = takenSwordCheck();
                return (ShouldHide(ms.State, i, j), hintShopTileBmp(hintShopColorOf(i, j), false), decos((host, x, y)));   // (stays bright, with a green check)
            }
            else
                return (false, hintShopTileBmp(hintShopColorOf(i, j), false), decos());
        }
        else if (ms.State == TrackerModel.MapSquareChoiceDomainHelper.SWORD2)
        {
            var ex = TrackerModel.getOverworldMapExtraData(i, j, ms.State);
            if (ex == SHUFFLED_ITEM && !(TrackerModel.sword2Box.IsDone()))
            {
                IEnumerable<(Control, double, double)> extraDecorationsF(Point boxPos)
                {
                    var extraDecorations = computeExtraDecorationArrow(sword2x, sword2y, boxPos);
                    return extraDecorations;
                }
                return (false, sword2LeftSideFullTileBmp, decos((Views.MakeBoxItemWithExtraDecorations(cm, TrackerModel.sword2Box, false, extraDecorationsF).Item1, OMTW - 30.0, 1.0)));
            }
            else if (ex == SHUFFLED_ITEM)
                return (ShouldHide(ms.State, i, j), sword2LeftSideFullTileBmp, decos((makeCaveItemBesideSword(TrackerModel.sword2Box), 20.0, 2.0)));
            else if (ex == ms.State || TrackerModel.sword2Box.IsDone())
                return (ShouldHide(ms.State, i, j), Graphics.theFullTileBmpTable[ms.State][(ex == ms.State ? 0 : 1)], (ex == ms.State ? decos(takenSwordCheck()) : decos()));
            else
                return (false, Graphics.theFullTileBmpTable[ms.State][0], decos());
        }
        else if (ms.State == TrackerModel.MapSquareChoiceDomainHelper.SWORD3)
        {
            var ex = TrackerModel.getOverworldMapExtraData(i, j, ms.State);
            if (ex == SHUFFLED_ITEM && !(TrackerModel.sword3Box.IsDone()))
            {
                IEnumerable<(Control, double, double)> extraDecorationsF(Point boxPos)
                {
                    var extraDecorations = computeExtraDecorationArrow(sword3x, sword3y, boxPos);
                    return extraDecorations;
                }
                return (false, sword3LeftSideFullTileBmp, decos((Views.MakeBoxItemWithExtraDecorations(cm, TrackerModel.sword3Box, false, extraDecorationsF).Item1, OMTW - 30.0, 1.0)));
            }
            else if (ex == SHUFFLED_ITEM)
                return (ShouldHide(ms.State, i, j), sword3LeftSideFullTileBmp, decos((makeCaveItemBesideSword(TrackerModel.sword3Box), 20.0, 2.0)));
            else if (TrackerModel.LegacyMode)
            {
                // Legacy Mode: the check mirrors the item box (collected = checked), so the two always match
                var taken = TrackerModel.sword3Box.IsDone();
                return ((taken ? ShouldHide(ms.State, i, j) : false), Graphics.theFullTileBmpTable[ms.State][0], (taken ? decos(takenSwordCheck()) : decos()));
            }
            else if (ex == ms.State || TrackerModel.sword3Box.IsDone() || TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Value())
                return (ShouldHide(ms.State, i, j), Graphics.theFullTileBmpTable[ms.State][(ex == ms.State ? 0 : 1)], (ex == ms.State ? decos(takenSwordCheck()) : decos()));
            else
                return (false, Graphics.theFullTileBmpTable[ms.State][0], decos());
        }
        else if (ms.IsDungeon)
        {
            System.Drawing.Bitmap combine(System.Drawing.Bitmap number, System.Drawing.Bitmap letter)
            {
                var fullTileBmp = new System.Drawing.Bitmap(16 * 3, 11 * 3);
                var TRANS_BG = Graphics.TRANS_BG;
                for (int px = 0; px <= 16 * 3 - 1; px++)
                    for (int py = 0; py <= 11 * 3 - 1; py++)
                    {
                        if (px >= 3 * 3 && px < 8 * 3 && py >= 1 * 3 && py < 10 * 3)
                            fullTileBmp.SetPixel(px, py, number.GetPixel(px - 3 * 3, py - 1 * 3));
                        else if (px >= 7 * 3 && px < 12 * 3 && py >= 1 * 3 && py < 10 * 3) // sharing one 'pixel'
                            fullTileBmp.SetPixel(px, py, letter.GetPixel(px - 7 * 3, py - 1 * 3));
                        else
                            fullTileBmp.SetPixel(px, py, TRANS_BG);
                    }
                return fullTileBmp;
            }
            var isDarker = TrackerModel.GetDungeon(ms.State).IsComplete;
            if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
            {
                if (TrackerModel.GetDungeon(ms.State).LabelChar != '?')
                {
                    if (isDarker)
                    {
                        var letter = Graphics.theInteriorBmpTable[ms.State][3];
                        var number = Graphics.theInteriorBmpTable[(int)(TrackerModel.GetDungeon(ms.State).LabelChar) - (int)('1')][1];
                        return (false, combine(number, letter), decos());
                    }
                    else
                    {
                        var letter = Graphics.theInteriorBmpTable[ms.State][2];
                        var number = Graphics.theInteriorBmpTable[(int)(TrackerModel.GetDungeon(ms.State).LabelChar) - (int)('1')][0];
                        return (false, combine(number, letter), decos());
                    }
                }
                else
                {
                    if (isDarker)
                        return (false, Graphics.theFullTileBmpTable[ms.State][3], decos());
                    else
                        return (false, Graphics.theFullTileBmpTable[ms.State][2], decos());
                }
            }
            else
            {
                if (isDarker)
                    return (false, Graphics.theFullTileBmpTable[ms.State][1], decos());
                else
                    return (false, Graphics.theFullTileBmpTable[ms.State][0], decos());
            }
        }
        else
        {
            // Return just the tile bitmap; item icon overlays for marked tiles (state >= 0) are intentionally
            // NOT shown here — they only appear on unmarked (state=-1) tiles or dark-X tiles, which are
            // handled by the state=-1 branch above and the redrawGridSpot dark-X path respectively.
            // Composing here caused the popup hover callback to show a spurious "letter icon" in the tile
            // preview whenever a tile with leftover shop data was hovered over PP/WARP tiles in the selector.
            return (false, Graphics.theFullTileBmpTable[ms.State][0], decos());
        }
    }

    public static readonly int[] toggleables = {
        TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY,
        TrackerModel.MapSquareChoiceDomainHelper.SWORD1,
        TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP,
        TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET,
        TrackerModel.MapSquareChoiceDomainHelper.MEDIUM_SECRET,
        TrackerModel.MapSquareChoiceDomainHelper.SMALL_SECRET,
        };   // (the letter cave is no longer a simple toggle: see its accelerator)
    public static void ToggleOverworldTileIfItIsToggleable(int i, int j, int state)
    {
        if (toggleables.Contains(state))
        {
            // left click to toggle it 'used'
            var ex = TrackerModel.getOverworldMapExtraData(i, j, state);
            if (state == TrackerModel.MapSquareChoiceDomainHelper.SWORD1 && ex == CANDLE_TAKEN)
            {
                // the candle was taken here: left click does nothing (clear the tile to take it back)
            }
            else if (ex != 0 && ex == state)
            {
                if (state == TrackerModel.MapSquareChoiceDomainHelper.SWORD1) clearWoodSwordCaveItems(i, j);   // un-collecting the cave's sword takes it back (item progress goes with it)
                else TrackerModel.setOverworldMapExtraData(i, j, state, 0);
            }
            else
            {
                if (state == TrackerModel.MapSquareChoiceDomainHelper.SWORD1 && !(TrackerModel.shuffledWoodSwordTiles.Contains(Tuple.Create(i, j))))
                {
                    // collecting it again gives the sword again (only one wood sword can be taken)
                    var swordTakenElsewhere =
                        allTiles().Any(t => t != (i, j) && TrackerModel.overworldMapMarks[t.Item1, t.Item2].Current() == state && TrackerModel.getOverworldMapExtraData(t.Item1, t.Item2, state) == state);
                    if (!swordTakenElsewhere)
                    {
                        var pp = TrackerModel.playerProgressAndTakeAnyHearts;
                        var si = TrackerModel.startingItemsAndExtras;
                        if (TrackerModel.ProgressiveItems) TrackerModel.progressiveTileTier[Tuple.Create(i, j, state)] = TrackerModel.progressiveSwordTier();
                        var n = TrackerModel.ProgressiveItems ? 1 : Math.Min(3, TrackerModel.playerComputedStateSummary.SwordLevel + 1);
                        pp.PlayerHasWoodSword.Set(n >= 1);
                        si.PlayerHasWhiteSword.Set(n >= 2);
                        si.PlayerHasMagicalSword.Set(n >= 3);
                        TrackerModel.setOverworldMapExtraData(i, j, state, state);
                        TrackerModel.recomputePlayerStateSummary();
                        TrackerModel.forceUpdate();
                    }
                }
                else
                    TrackerModel.setOverworldMapExtraData(i, j, state, state);
            }
        }
    }

    public static Action<int> goToDungeon = (int _idx) => { };

    public static async Task<(bool, bool)> DoLeftClick(CustomComboBoxes.CanvasManager cm, MapStateProxy msp, int i, int j, Point pos)  // returns tuple of two booleans (needRedrawGridSpot, needHighlightTileHide)
    {
        if (msp.State == -1)
        {
            // left click empty tile changes to 'X'
            TrackerModel.overworldMapMarks[i, j].Set(TrackerModel.MapSquareChoiceDomainHelper.DARK_X);
            return (true, true);
        }
        else if (msp.IsDungeon)
        {
            goToDungeon(msp.State);
            return (false, false);
        }
        else if (msp.IsThreeItemShop)
        {
            CustomComboBoxes.GlobalFlag.popupIsActive = true;
            var item1 = msp.State - TrackerModel.MapSquareChoiceDomainHelper.ARROW;  // 0-based
            var item2_1b = TrackerModel.getShopItem2_1based(i, j);
            var item3_1b = TrackerModel.getShopItem3_1based(i, j);
            var item2 = item2_1b - 1;  // 0-based, -1=none
            var item3 = item3_1b - 1;  // 0-based, -1=none
            var ST = CustomComboBoxes.borderThickness;
            // If item2 not set, pick item2; else pick item3
            var choosingItem3 = item2 >= 0;
            var currentChoice = choosingItem3 ? item3 : item2;
            var promptText = choosingItem3 ? "Choose 3rd shop item" : "Choose 2nd shop item";
            System.Drawing.Bitmap tileImage()
            {
                if (choosingItem3)
                    return makeTwoItemShopBmp(item1, item2);
                else
                    return Graphics.theFullTileBmpTable[msp.State][0];
            }
            var tileCanvas = new Canvas { Width = OMTW, Height = 11.0 * 3.0 };
            canvasAdd(tileCanvas, Graphics.BMPtoImage(tileImage()), 0.0, 0.0);
            var originalStateIndex = Math.Max(0, currentChoice);
            double gridxPosition;
            if (pos.X < OMTW * 2.0)
                gridxPosition = -ST;
            else if (pos.X > OMTW * 13.0)
                gridxPosition = OMTW - (double)(8 * (5 * 3 + 2 * (int)ST) + (int)ST);
            else
                gridxPosition = (OMTW - (double)(8 * (5 * 3 + 2 * (int)ST) + (int)ST)) / 2.0;
            var gridElementsSelectablesAndIDs = Enumerable.Range(0, 8).Select(n =>
            {
                var st = n + TrackerModel.MapSquareChoiceDomainHelper.ARROW;   // (F#: `i`, shadowing the tile's i)
                return ((Control)Graphics.BMPtoImage(new MapStateProxy(st).DefaultInteriorBmp()), true, n);
            }).ToArray();
            var edTB1 = new TextBox { IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 16.0, Margin = new Thickness(2.0),
                                        VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center,
                                        Text = promptText,
                                        Foreground = Brushes.Orange, Background = Brushes.Black };
            var edBorder = new Border { BorderThickness = new Thickness(3.0), BorderBrush = Brushes.Gray, Background = Brushes.Black, Child = edTB1 };
            var extraDecorations = new List<(Control, double, double)> { (edBorder, gridxPosition, -33.0) };
            var g = await CustomComboBoxes.DoModalGridSelect<int, int>(cm, pos.X, pos.Y, tileCanvas,
                            gridElementsSelectablesAndIDs, originalStateIndex, 0, (8, 1, 5 * 3, 9 * 3), (double)(5 * 3) / 2.0, (double)(9 * 3) / 2.0, gridxPosition, 11.0 * 3.0 + ST,
                            (currentState) =>
                            {
                                tileCanvas.Children.Clear();
                                var preview =
                                    choosingItem3 ? makeThreeItemShopBmp(item1, item2, currentState)
                                    : makeTwoItemShopBmp(item1, currentState);
                                canvasAdd(tileCanvas, Graphics.BMPtoImage(preview), 0.0, 0.0);
                            },
                            (_ea, currentState) => new CustomComboBoxes.PopupClickBehavior<int>.DismissPopupWithResult(currentState),
                            extraDecorations, CustomComboBoxes.ModalGridSelectBrushes.Defaults(), new CustomComboBoxes.GridClickDismissalWarpReturn.WarpToCenter(), null, "SecondShopItem", null);
            (bool, bool) r;
            if (g.hasResult)
            {
                var currentState = g.result;
                if (choosingItem3)
                    TrackerModel.setShopItems(i, j, item2_1b, currentState + 1);
                else
                    TrackerModel.setShopItems(i, j, currentState + 1, 0);
                TrackerModel.mapLastChangedTime.SetNow();
                r = (true, (currentChoice == -1 && currentState >= 0));
            }
            else r = (false, false);
            CustomComboBoxes.GlobalFlag.popupIsActive = false;
            return r;
        }
        else if (TrackerModel.LegacyMode && msp.State == TrackerModel.MapSquareChoiceDomainHelper.SWORD3 && !TrackerModel.ProgressiveItems)
        {
            // Legacy Mode: left click on the magical sword cave toggles its sword collected, in step with the item box
            var box = TrackerModel.sword3Box;
            if (box.IsDone())
            {
                TrackerModel.startingItemsAndExtras.PlayerHasMagicalSword.Set(false);
                box.AttemptToSet(TrackerModel.ITEMS.MAGICALSWORD, TrackerModel.PlayerHas.NO);
            }
            else
            {
                TrackerModel.startingItemsAndExtras.PlayerHasMagicalSword.Set(true);
                box.AttemptToSet(TrackerModel.ITEMS.MAGICALSWORD, TrackerModel.PlayerHas.YES);
            }
            TrackerModel.setOverworldMapExtraData(i, j, msp.State, SHUFFLED_ITEM);
            TrackerModel.forceUpdate();
            return (true, false);
        }
        else
        {
            if (toggleables.Contains(msp.State))
            {
                // left click to toggle it 'used'
                ToggleOverworldTileIfItIsToggleable(i, j, msp.State);
                return (true, false);
            }
            else
                return (false, false);
        }
    }

    /// Shows the shop item icon picker when right-clicking an overworld tile that has no normal
    /// right-click context menu (i.e. AlwaysEmpty tiles). Returns true if a redraw is needed.
    public static async Task<bool> DoNonShopItemIconPopup(CustomComboBoxes.CanvasManager cm, MapStateProxy msp, int i, int j, Point pos)
    {
        var item2_1b = TrackerModel.getShopItem2_1based(i, j);
        var item2 = item2_1b - 1;  // 0-based, -1=none
        var ST = CustomComboBoxes.borderThickness;
        var tileCanvas = new Canvas { Width = OMTW, Height = 11.0 * 3.0 };
        if (msp.State >= 0)
            canvasAdd(tileCanvas, Graphics.BMPtoImage(Graphics.theFullTileBmpTable[msp.State][0]), 0.0, 0.0);
        var originalStateIndex = Math.Max(0, item2);
        double gridxPosition;
        if (pos.X < OMTW * 2.0)
            gridxPosition = -ST;
        else if (pos.X > OMTW * 13.0)
            gridxPosition = OMTW - (double)(8 * (5 * 3 + 2 * (int)ST) + (int)ST);
        else
            gridxPosition = (OMTW - (double)(8 * (5 * 3 + 2 * (int)ST) + (int)ST)) / 2.0;
        var gridElementsSelectablesAndIDs = Enumerable.Range(0, 8).Select(n =>
        {
            var idx = n + TrackerModel.MapSquareChoiceDomainHelper.ARROW;
            return ((Control)Graphics.BMPtoImage(new MapStateProxy(idx).DefaultInteriorBmp()), true, n);
        }).ToArray();
        var edTB1 = new TextBox { IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 16.0, Margin = new Thickness(2.0),
                                    VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center,
                                    Text = "Mark item icon",
                                    Foreground = Brushes.Orange, Background = Brushes.Black };
        var edBorder = new Border { BorderThickness = new Thickness(3.0), BorderBrush = Brushes.Gray, Background = Brushes.Black, Child = edTB1 };
        var extraDecorations = new List<(Control, double, double)> { (edBorder, gridxPosition, -33.0) };
        var g = await CustomComboBoxes.DoModalGridSelect<int, int>(cm, pos.X, pos.Y, tileCanvas,
                        gridElementsSelectablesAndIDs, originalStateIndex, 0, (8, 1, 5 * 3, 9 * 3), (double)(5 * 3) / 2.0, (double)(9 * 3) / 2.0, gridxPosition, 11.0 * 3.0 + ST,
                        (_currentState) => { },
                        (PointerPressedEventArgs ea, int currentState) =>
                        {
                            // (WPF: `ea.ChangedButton = MouseButton.Right`; on a press, PointerUpdateKind names the button that went down)
                            if (ea.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
                                return new CustomComboBoxes.PopupClickBehavior<int>.DismissPopupWithResult(-1);  // right-click = clear icon
                            else
                                return new CustomComboBoxes.PopupClickBehavior<int>.DismissPopupWithResult(currentState);
                        },
                        extraDecorations, CustomComboBoxes.ModalGridSelectBrushes.Defaults(), new CustomComboBoxes.GridClickDismissalWarpReturn.WarpToCenter(), null, "NonShopItemIcon", null);
        if (g.hasResult && g.result == -1)  // right-click = clear stored item icon
        {
            TrackerModel.setShopItems(i, j, 0, 0);
            TrackerModel.mapLastChangedTime.SetNow();
            return true;
        }
        else if (g.hasResult)
        {
            var currentState = g.result;
            TrackerModel.setShopItems(i, j, currentState + 1, 0);
            TrackerModel.mapLastChangedTime.SetNow();
            return true;
        }
        else return false;
    }

    ///////////////////////////////////////////////////

    public static int DoSpecialHotKeyHandlingForOverworldTiles(int i, int j, int originalState, int hotKeyedState)
    {
        // rather than have many idempotent keys, turn some of them into useful actions
        var orig = new MapStateProxy(originalState);
        if (orig.IsThreeItemShop)
        {
            var item2_1b = TrackerModel.getShopItem2_1based(i, j);
            var item3_1b = TrackerModel.getShopItem3_1based(i, j);
            var item2 = item2_1b - 1;  // 0-based, -1=none
            var item3 = item3_1b - 1;  // 0-based, -1=none
            var item2AsState = item2 + TrackerModel.MapSquareChoiceDomainHelper.ARROW;
            var item3AsState = item3 + TrackerModel.MapSquareChoiceDomainHelper.ARROW;
            if (hotKeyedState == originalState)           // pressed same key as left item
            {
                if (item2 == -1)
                    return -1;   // no second item, so turn back to empty tile
                else
                    return item2AsState;   // change first item to second item
            }
            else if (new MapStateProxy(hotKeyedState).IsThreeItemShop)
            {
                if (item2AsState == hotKeyedState)        // pressed same key as item2 → remove item2 and item3
                {
                    TrackerModel.setShopItems(i, j, 0, 0);
                    return originalState;
                }
                else if (item3 >= 0 && item3AsState == hotKeyedState)  // pressed same key as item3 → remove item3
                {
                    TrackerModel.setShopItems(i, j, item2_1b, 0);
                    return originalState;
                }
                else if (item2 == -1)                        // no item2 yet → set as item2
                {
                    var v = hotKeyedState - TrackerModel.MapSquareChoiceDomainHelper.ARROW + 1;
                    TrackerModel.setShopItems(i, j, v, 0);
                    return originalState;
                }
                else if (item3 == -1)                        // item2 set, no item3 → set as item3
                {
                    var v = hotKeyedState - TrackerModel.MapSquareChoiceDomainHelper.ARROW + 1;
                    TrackerModel.setShopItems(i, j, item2_1b, v);
                    return originalState;
                }
                else
                    return hotKeyedState;
            }
            else
                return hotKeyedState;
        }
        else if (hotKeyedState == originalState && toggleables.Contains(originalState))
        {
            ToggleOverworldTileIfItIsToggleable(i, j, originalState);
            return hotKeyedState;
        }
        else
            return hotKeyedState;
    }

    ///////////////////////////////////////////////////

    // the items placed in numbered shop n as (icon, collected) pairs; set by WPFUI (which knows how to draw the marker icons)
    public static Func<int, System.Drawing.Bitmap> decorBitmapHook = _ => null;   // marker icon id -> its picture (set from WPFUI)
    public static Func<int, List<(System.Drawing.Bitmap, bool)>> shopItemBitmapsHook = _ => new List<(System.Drawing.Bitmap, bool)>();
    public static Border MakeRemainderSummaryDisplayEx(bool _freeWidth)   // Spot Summary (freeWidth: the count cells may spread over as many columns as the width allows; otherwise 4 per row)
    {
        var sp = new StackPanel { Orientation = Orientation.Vertical };
        Border b(Control x) => new Border { Child = x, BorderThickness = new Thickness(3.0), BorderBrush = Brushes.Black };
#pragma warning disable CS0219, CS8321   // (_HEIGHT and _horizontalRule are unused, as in WPF)
        var _HEIGHT = 3.0 + 27.0 + 3.0;

        var OPA = 0.4;
        var OPA2 = 0.75;

        void header(string _txt) { }   // the Spot Summary has no text headings (just the icons)
        void _horizontalRule() =>
            sp.Children.Add(new Canvas { Height = 3.0, Background = Brushes.Gray, Margin = new Thickness(3.0) });
#pragma warning restore CS0219, CS8321

        // header("Remaining Locations Summary")
        // horizontalRule()

        // the unique-location icons are spread out by half of the room that would reach the width of the count cells below (4 columns of 76)
        // the unique-location icons are spread out by half of the room that would reach the width of the count cells below
        var uniqueW = 0.0;
        void halveGap(UniformGrid row)
        {
            row.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var natural = row.DesiredSize.Width;
            var before = (natural + 9.0 * 3.0) + (Math.Max(0.0, 4.0 * 68.0 - (natural + 9.0 * 3.0))) / 2.0;   // the width before the borders were thinned
            row.MinWidth = Math.Max(natural, before * 0.9);   // another 10% narrower than that
            uniqueW = row.MinWidth;
        }
        // uniques
        header("Unique Locations");

        {
            var row = new UniformGrid { Columns = 9, HorizontalAlignment = HorizontalAlignment.Stretch };   // the nine icons are spread out a little (see halveGap)
            for (int i = 0; i <= 8; i++)
            {
                var icon = Graphics.BMPtoImage(new MapStateProxy(i).DefaultInteriorBmp());
                if (!(TrackerModel.mapSquareChoiceDomain.CanAddUse(i)))
                    icon.Opacity = OPA;
                var bi = new Border { Child = icon, BorderThickness = new Thickness(1.5), BorderBrush = Brushes.Black };   // (thinner black border than the count cells, to keep the row narrower)
                bi.HorizontalAlignment = HorizontalAlignment.Center;
                row.Children.Add(bi);
            }
            halveGap(row);
            sp.Children.Add(row);
        }

        {
            var row = new UniformGrid { Columns = 9, HorizontalAlignment = HorizontalAlignment.Stretch };   // the nine icons are spread out a little (see halveGap)
            var allBoxes = TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes();
            bool itemCollected(int id) => (allBoxes.Any(box => box.CellCurrent() == id && box.IsDone())) || TrackerModel.mapCollectedCount[id] > 0;
            var letterCollected = itemCollected(TrackerModel.ITEMS.LETTER);
            // sword cave tiles (15 = 1st sword, 14 = 2nd, 13 = 3rd) dim once that sword has been collected
            bool swordCollected(int tile) => tile switch
            {
                15 => itemCollected(TrackerModel.ITEMS.WOODSWORD) || itemCollected(TrackerModel.ITEMS.PROGRESSIVESWORD) || TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword.Value(),
                14 => itemCollected(TrackerModel.ITEMS.WHITESWORD) || TrackerModel.startingItemsAndExtras.PlayerHasWhiteSword.Value(),
                13 => itemCollected(TrackerModel.ITEMS.MAGICALSWORD) || TrackerModel.startingItemsAndExtras.PlayerHasMagicalSword.Value() || TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Value(),
                _ => false,
            };
            foreach (var i in new[] { 9, 10, 11, 12, 13, 14, 15, 30, 31 })
            {
                // the letter uses the same icon as the item menus, and dims once the letter has been collected
                var icon = i == 30 ? Graphics.BMPtoImage(CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.LETTER, null)) : Graphics.BMPtoImage(new MapStateProxy(i).DefaultInteriorBmp());
                if (!(TrackerModel.mapSquareChoiceDomain.CanAddUse(i)) || (i == 30 && letterCollected) || swordCollected(i))
                    icon.Opacity = OPA;
                var bi = new Border { Child = icon, BorderThickness = new Thickness(1.5), BorderBrush = Brushes.Black };   // (thinner black border than the count cells, to keep the row narrower)
                bi.HorizontalAlignment = HorizontalAlignment.Center;
                row.Children.Add(bi);
            }
            halveGap(row);
            sp.Children.Add(row);
        }

        // secrets (no dividers: just an icon with how many are collected out of the max, three to a row)
        var (LARGE, MEDIUM, SMALL, UNKNOWN) = (TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET, TrackerModel.MapSquareChoiceDomainHelper.MEDIUM_SECRET,
                                                TrackerModel.MapSquareChoiceDomainHelper.SMALL_SECRET, TrackerModel.MapSquareChoiceDomainHelper.UNKNOWN_SECRET);
        var (LARGE_BMP, MEDIUM_BMP, SMALL_BMP) =
            (Graphics.theInteriorBmpTable[LARGE][0], Graphics.theInteriorBmpTable[MEDIUM][0], Graphics.theInteriorBmpTable[SMALL][0]);
        // grab at most 14 that the user marked
        int userLarge = 0, userMedium = 0, userSmall = 0, userUnknown = 0, userTotal = 0;
        for (int i = 0; i <= TrackerModel.mapSquareChoiceDomain.NumUses(LARGE) - 1; i++)
            if (userTotal < 14)
            {
                userLarge = userLarge + 1;
                userTotal = userTotal + 1;
            }
        for (int i = 0; i <= TrackerModel.mapSquareChoiceDomain.NumUses(MEDIUM) - 1; i++)
            if (userTotal < 14)
            {
                userMedium = userMedium + 1;
                userTotal = userTotal + 1;
            }
        for (int i = 0; i <= TrackerModel.mapSquareChoiceDomain.NumUses(SMALL) - 1; i++)
            if (userTotal < 14)
            {
                userSmall = userSmall + 1;
                userTotal = userTotal + 1;
            }
        for (int i = 0; i <= TrackerModel.mapSquareChoiceDomain.NumUses(UNKNOWN) - 1; i++)
            if (userTotal < 14)
            {
                userUnknown = userUnknown + 1;
                userTotal = userTotal + 1;
            }
        // place as many as possible into the 'right' bins
        Image dark(System.Drawing.Bitmap bmp)
        {
            var i = Graphics.BMPtoImage(bmp);
            i.Opacity = OPA;
            return i;
        }
        var (large, medium, small) = TrackerModel.owInstance.Quest.IsFirstQuestOW ? (3, 7, 4) : (1, 7, 6);
        var (userLargeIcons, userMediumIcons, userSmallIcons) = (new List<Image>(), new List<Image>(), new List<Image>());
        while (userLarge > 0 && userLargeIcons.Count < large)
        {
            userLargeIcons.Add(dark(LARGE_BMP));
            userLarge = userLarge - 1;
        }
        while (userMedium > 0 && userMediumIcons.Count < medium)
        {
            userMediumIcons.Add(dark(MEDIUM_BMP));
            userMedium = userMedium - 1;
        }
        while (userSmall > 0 && userSmallIcons.Count < small)
        {
            userSmallIcons.Add(dark(SMALL_BMP));
            userSmall = userSmall - 1;
        }
        // allocate the remainders into other bins
        while (userLarge > 0)
        {
            if (medium - userMediumIcons.Count > small - userSmallIcons.Count)
                userMediumIcons.Insert(0, dark(LARGE_BMP));
            else
                userSmallIcons.Insert(0, dark(LARGE_BMP));
            userLarge = userLarge - 1;
        }
        while (userMedium > 0)
        {
            if (large - userLargeIcons.Count > small - userSmallIcons.Count)
                userLargeIcons.Add(dark(MEDIUM_BMP));
            else
                userSmallIcons.Insert(0, dark(MEDIUM_BMP));
            userMedium = userMedium - 1;
        }
        while (userSmall > 0)
        {
            if (large - userLargeIcons.Count > medium - userMediumIcons.Count)
                userLargeIcons.Add(dark(SMALL_BMP));
            else
                userMediumIcons.Add(dark(SMALL_BMP));
            userSmall = userSmall - 1;
        }
        int largeUnknown = 0, mediumUnknown = 0, smallUnknown = 0;
        while (userUnknown > 0)
        {
            var largeHoles = large - userLargeIcons.Count;
            var mediumHoles = medium - userMediumIcons.Count;
            var smallHoles = small - userSmallIcons.Count;
            if (largeHoles >= mediumHoles && largeHoles >= smallHoles)
            {
                userLargeIcons.Add(Graphics.BMPtoImage(new MapStateProxy(UNKNOWN).DefaultInteriorBmp()));
                largeUnknown = largeUnknown + 1;
            }
            else if (mediumHoles >= largeHoles && mediumHoles >= smallHoles)
            {
                userMediumIcons.Add(Graphics.BMPtoImage(new MapStateProxy(UNKNOWN).DefaultInteriorBmp()));
                mediumUnknown = mediumUnknown + 1;
            }
            else
            {
                userSmallIcons.Add(Graphics.BMPtoImage(new MapStateProxy(UNKNOWN).DefaultInteriorBmp()));
                smallUnknown = smallUnknown + 1;
            }
            userUnknown = userUnknown - 1;
        }

        // one cell: the icon, then 'xN/M' = N collected out of the max M
        Control countCell(Control icon, int collected, int mx)
        {
            var cell = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0.0, 2.0, 0.0, 2.0) };   // centered in its column
            cell.Children.Add(b(icon));
            var txt = new TextBlock { Text = (mx < 0 ? $"x{collected}" : $"x{collected}/{mx}"), Foreground = Brushes.Orange, FontSize = 12.0, FontWeight = FontWeight.Bold, MinWidth = 30.0, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2.0, 0.0, 0.0, 0.0) };   // (MinWidth: "x0/9" and "x0/10" take the same room, so 2nd quest's 10 door repairs don't widen the window)
            if (mx >= 0 && collected >= mx) txt.Opacity = OPA2;   // all collected: dim it
            cell.Children.Add(txt);
            return cell;
        }
        // all the count cells are collected here and then laid out four to a row (fixed columns, together about as wide as the 9 unique-location icons above)
        var allCells = new List<Control>();
        (Grid, Border, TextBlock)? potionContentsParts = null;   // the potion shop contents cell (if any), so flushRows can line it up
        void addRow(IEnumerable<Control> cells) { foreach (var c in cells) allCells.Add(c); }
        void flushRows()
        {
            // exactly three columns (a Grid, not a wrapping panel), together as wide as the unique-location rows above
            var g = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            for (int _k = 0; _k <= 2; _k++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            var nRows = (allCells.Count + 2) / 3;
            for (int _r = 1; _r <= nRows; _r++) g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int k = 0; k < allCells.Count; k++)
            {
                var c = allCells[k];
                Grid.SetColumn(c, k % 3);
                Grid.SetRow(c, k / 3);
                g.Children.Add(c);
            }
            // line the potion shop's icon up with the icons in the row above, and put the = halfway between the potion shop count and that icon
            if (potionContentsParts is (Grid holder, Border iconBox, TextBlock eq))
            {
                var k = allCells.IndexOf(holder);
                if (k >= 3)
                {
                    var above = allCells[k - 3];
                    var potionCell = allCells[k - 1];
                    // (WPF ActualWidth → Bounds.Width; TranslatePoint returns null when the two aren't in one visual tree, which the try/with catches as WPF's exception)
                    g.LayoutUpdated += (_, _) =>
                    {
                        try
                        {
                            if (above.Bounds.Width > 0.0 && potionCell.Bounds.Width > 0.0 && holder.Bounds.Width > 0.0)
                            {
                                var aboveIconLeft = (above is Panel pn && pn.Children.Count > 0)
                                                    ? pn.Children[0].TranslatePoint(new Point(0.0, 0.0), g).Value.X
                                                    : above.TranslatePoint(new Point(0.0, 0.0), g).Value.X;
                                var cellLeft = holder.TranslatePoint(new Point(0.0, 0.0), g).Value.X - holder.Margin.Left;
                                var potionRight = potionCell.TranslatePoint(new Point(potionCell.Bounds.Width, 0.0), g).Value.X;
                                var iconLeftWanted = aboveIconLeft - cellLeft;
                                eq.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                                var eqLeft = ((potionRight + aboveIconLeft) / 2.0 - eq.DesiredSize.Width / 2.0) - cellLeft;
                                if (Math.Abs(iconBox.Margin.Left - iconLeftWanted) > 0.5) iconBox.Margin = new Thickness(iconLeftWanted, 0.0, 0.0, 0.0);
                                if (Math.Abs(eq.Margin.Left - eqLeft) > 0.5) eq.Margin = new Thickness(eqLeft, 0.0, 0.0, 0.0);
                            }
                        }
                        catch { }
                    };
                }
            }
            sp.Children.Add(g);
        }
        // row: the three kinds of secrets (the marks you placed count as collected; '?' marks are placed in the emptiest kind)
        addRow(new[] { countCell(Graphics.BMPtoImage(LARGE_BMP), Math.Min(large, userLargeIcons.Count), large),
                       countCell(Graphics.BMPtoImage(MEDIUM_BMP), Math.Min(medium, userMediumIcons.Count), medium),
                       countCell(Graphics.BMPtoImage(SMALL_BMP), Math.Min(small, userSmallIcons.Count), small) });
        // the non-unique locations
        Control multiCell(int multi)
        {
            var isPotion = (multi == 34);
            // (potions placed in item boxes do not count towards the potion spots shown here)
            int usedCount;
            if (isPotion)
            {
                // a potion spot is used by a tile marked as a potion shop, or by a tile labeled "P SHOP"
                var n = 0;
                for (int ti = 0; ti <= 15; ti++)
                    for (int tj = 0; tj <= 7; tj++)
                        if (TrackerModel.overworldMapMarks[ti, tj].Current() == 34 || TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[ti, tj]) == 5)
                            n = n + 1;
                usedCount = n;
            }
            else if (multi == 33)
            {
                // uncollected hearts: starts at 13; every heart container found and every take-any heart slot used up (a heart, or a candle / potion taken instead) takes one off
                var tookAny = Enumerable.Range(0, 4).Where(h => TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyHeart(h) != 0).Count();
                usedCount = Math.Max(0, 13 - TrackerModel.mapCollectedCount[TrackerModel.ITEMS.HEARTCONTAINER] - tookAny);
            }
            else usedCount = TrackerModel.mapSquareChoiceDomain.NumUses(multi);
            var mx = multi == 33 ? 13 : TrackerModel.mapSquareChoiceDomain.MaxUses(multi);
            // (potion shops are shown as just the blue potion; hint shops show the color the user last chose, grey until then)
            Image icon;
            if (isPotion)
            {
                var im = Graphics.BMPtoImage(Graphics.blue_potion_bmp);
                im.Width = 21.0;
                im.Height = 27.0;
                im.Stretch = Stretch.Uniform;
                icon = im;
            }
            else if (multi == TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP) icon = Graphics.BMPtoImage(hintShopMenuBmp(hintShopColorChosen || (hintShopColors.Values.Any(v => v != 0)) ? lastHintShopColor : 4));
            else icon = Graphics.BMPtoImage(new MapStateProxy(multi).DefaultInteriorBmp());
            if (multi == 33 || multi == TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP) return countCell(icon, usedCount, -1);   // the hearts show just the uncollected count, no "/13"; hint shops are unlimited, so just how many are placed
            else return countCell(icon, Math.Min(usedCount, mx), mx);
        }
        // row: doors, money making game, and the hint shops right beside it
        addRow(new[] { multiCell(28), multiCell(29), multiCell(32) });
        // row: take any hearts and the potions next to them
        // (the open space beside them shows what is in the potion shops: just the middle item, the potion; dimmed once it has been collected from every placed potion shop)
        List<Control> potionShopContents()
        {
            // nothing shows until a potion shop has its 3rd icon (the one in the middle, between the blue and red potions); that icon is shown here
            var entries = new List<(int, bool)>();
            for (int ti = 0; ti <= 15; ti++)
                for (int tj = 0; tj <= 7; tj++)
                    if (TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[ti, tj]) == 5)
                        foreach (var e in TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[ti, tj]))
                            entries.Add((e.Item1, e.Item2));
            if (entries.Count == 0)
                return new List<Control>();
            else
            {
                var (ident, collected) = entries[0];
                var bmp = decorBitmapHook(ident);
                if (bmp == null) return new List<Control>();
                else
                {
                    var img = Graphics.BMPtoImage(bmp);
                    img.Stretch = Stretch.Uniform;
                    img.Width = 24.0;
                    img.Height = 24.0;
                    if (collected) img.Opacity = 0.4;
                    // "= icon": an equals sign between the potion shop count and the icon that is in the shop
                    // the icon lines up with the icons in the rows above, and the = sits exactly halfway between the potion shop count and the icon (both placed after layout, see flushRows)
                    var holder = new Grid { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                    var iconBox = b(img);
                    iconBox.HorizontalAlignment = HorizontalAlignment.Left;
                    var eq = new TextBlock { Text = "=", Foreground = Brushes.Orange, FontSize = 20.0, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                    holder.Children.Add(iconBox);
                    holder.Children.Add(eq);
                    potionContentsParts = (holder, iconBox, eq);
                    return new List<Control> { holder };
                }
            }
        }
        addRow(new[] { multiCell(33), multiCell(34) }.Concat(potionShopContents()));
        // the five numbered shops (each can appear up to 3 times); a tile gets its number from the 'Shop' flyout in the overworld menu
        Control shopCell(int n)
        {
            var im = Graphics.BMPtoImage(shopNumberBmp(n));
            var top = countCell(im, TrackerModel.shopLabelCount(n), -1);   // shops have no fixed max: just how many tiles are placed
            var items = shopItemBitmapsHook(n);
            if (items.Count == 0)
            {
                // no items yet: the space under the shop is still kept, so the window doesn't grow when the first item is placed
                var col = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
                col.Children.Add(top);
                col.Children.Add(new DockPanel { Height = 17.0 });
                return col;
            }
            else
            {
                // the items placed in this shop, in a small row under its count (collected ones dimmed)
                var col = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
                col.Children.Add(top);
                var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                var keyIdx = n >= 1 && n <= 4 && TrackerModel.cheapKeyShops[n] ? PieMenus.shopKeyIndexHook(n) : -1;
                for (int idx = 0; idx < items.Count; idx++)
                {
                    var (bmp, collected) = items[idx];
                    var img = Graphics.BMPtoImage(bmp);
                    img.Stretch = Stretch.Uniform;
                    img.Width = 15.0;
                    img.Height = 15.0;
                    img.Margin = new Thickness(1.0);
                    if (collected) img.Opacity = 0.4;
                    if (idx == keyIdx) row.Children.Add(new Border { BorderBrush = Brushes.Yellow, BorderThickness = new Thickness(1.0), Child = img });   // the cheaper key shop's key
                    else row.Children.Add(img);
                }
                col.Children.Add(row);
                return col;
            }
        }
        flushRows();
        // the four shops in one row, as wide as the unique-location rows above
        var sg = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        for (int _k = 0; _k <= 3; _k++) sg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
        {
            var k = 0;
            foreach (var n in new[] { 1, 2, 3, 4 })
            {
                var c = shopCell(n);
                Grid.SetColumn(c, k);
                sg.Children.Add(c);
                k++;
            }
        }
        sp.Children.Add(sg);

        sp.Width = uniqueW;   // every row (the unique locations and everything under them) is exactly the same width, so the window is a clean rectangle
        sp.Margin = new Thickness(3.0);
        return new Border { Child = sp, BorderThickness = new Thickness(3.0), BorderBrush = Brushes.Gray, Background = Brushes.Black };
    }

    public static Border MakeRemainderSummaryDisplay() => MakeRemainderSummaryDisplayEx(false);

    //////////////////////////////////////////////////

    public static (bool, Border) MakeMappedHotKeysDisplay()
    {
        var total = 0;
        Panel makePanel<T>(IEnumerable<T> states, HotKeys.HotKeyProcessor<T> hkp, Func<T, Control> mkIcon, int iconW, string header)
        {
            var panel = new WrapPanel { MaxHeight = 800.0, Orientation = Orientation.Vertical };
            var bucket = new List<(string, StackPanel)>();
            foreach (var state in states)
            {
                if (hkp.StateToKeys(state).Count > 0)
                {
                    var keys = hkp.StateToKeys(state).Aggregate("", (s, c) => s + HotKeys.PrettyKey(c) + ",");
                    keys = keys.Substring(0, keys.Length - 1);
                    var icon = mkIcon(state);
                    icon.Width = (double)iconW;
                    var border = new Border { BorderBrush = Brushes.DimGray, BorderThickness = new Thickness(1.0), Child = icon };
                    border.Margin = new Thickness(3.0);
                    var txt = DungeonRoomState.mkTxt(keys);
                    txt.TextAlignment = TextAlignment.Left;
                    txt.Margin = new Thickness(0.0, 0.0, 9.0, 0.0);  // right margin, so that in two columns, text clearly is attached to the left icon, not the right
                    var sp = new StackPanel { Orientation = Orientation.Horizontal };
                    sp.Children.Add(border);
                    sp.Children.Add(txt);
                    bucket.Add((keys, sp));
                    total = total + 1;
                }
            }
            //for _,sp in bucket |> Seq.sortBy fst do   // sort by alphabetical of key
            foreach (var (_, sp) in bucket)            // "logical" order
                panel.Children.Add(sp);
            if (panel.Children.Count > 0)
            {
                var dp = new DockPanel();
                var h = DungeonRoomState.mkTxt(header);
                DockPanel.SetDock(h, Dock.Top);
                dp.Children.Add(h);
                dp.Children.Add(panel);
                dp.Margin = new Thickness(3.0, 3.0, 13.0, 3.0);
                return dp;
            }
            else
                return panel;
        }
        Control bmpElseSize((int w, int h) size, System.Drawing.Bitmap bmp)
        {
            Control icon = bmp == null ? new DockPanel { Width = (double)size.w, Height = (double)size.h } : Graphics.BMPtoImage(bmp);
            return icon;
        }
        var itemPanel = makePanel(Enumerable.Range(-1, 16), HotKeys.ItemHotKeyProcessor, (item => bmpElseSize((21, 21), CustomComboBoxes.boxCurrentBMP(item, null))), 21, "ITEMS");
        var overworldPanel = makePanel(Enumerable.Range(-1, TrackerModel.dummyOverworldTiles.Length + 1), HotKeys.OverworldHotKeyProcessor, (state =>
        {
            // the shop hotkeys place the new marker icons (the same ones the item menus use), so that is what is shown here too
            System.Drawing.Bitmap shopBmp =
                state == TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP ? Graphics.blue_potion_bmp
                : state == TrackerModel.MapSquareChoiceDomainHelper.ARROW ? CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.WOODARROW, null)
                : state == TrackerModel.MapSquareChoiceDomainHelper.BOMB ? Graphics.bomb_bmp
                : state == TrackerModel.MapSquareChoiceDomainHelper.BOOK ? CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.BOOKSHIELD, null)
                : state == TrackerModel.MapSquareChoiceDomainHelper.BLUE_CANDLE ? CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.BLUECANDLE, null)
                : state == TrackerModel.MapSquareChoiceDomainHelper.BLUE_RING ? CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.BLUERING, null)
                : state == TrackerModel.MapSquareChoiceDomainHelper.MEAT ? CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.MEAT, null)
                : state == TrackerModel.MapSquareChoiceDomainHelper.KEY ? CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.OLDKEY, null)
                : state == TrackerModel.MapSquareChoiceDomainHelper.SHIELD ? CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.SHIELD, null)
                : null;
            if (shopBmp != null)
            {
                var img = Graphics.BMPtoImage(shopBmp);
                img.Width = 15.0;
                img.Height = 15.0;
                img.Stretch = Stretch.Uniform;
                var g = new Grid { Height = 27.0 };
                img.VerticalAlignment = VerticalAlignment.Center;
                g.Children.Add(img);
                return (Control)g;
            }
            else
                return bmpElseSize((15, 27), new MapStateProxy(state).DefaultInteriorBmp());
        }), 15, "OVERWORLD");
        var blockerPanel = makePanel(TrackerModel.DungeonBlocker.All, HotKeys.BlockerHotKeyProcessor, (state =>
            (Control)Graphics.blockerCurrentDisplay(state)), 24, "BLOCKERS");
        var thingies = new List<HotKeys.DungeonRoomHotKey>();
        thingies.AddRange(RoomTypeExtensions.All().Select(x => (HotKeys.DungeonRoomHotKey)new HotKeys.DungeonRoomHotKey.OfRoomType(x)));
        thingies.AddRange(MonsterDetailExtensions.All().Select(x => (HotKeys.DungeonRoomHotKey)new HotKeys.DungeonRoomHotKey.OfMonsterDetail(x)));
        thingies.AddRange(FloorDropDetailExtensions.All().Select(x => (HotKeys.DungeonRoomHotKey)new HotKeys.DungeonRoomHotKey.OfFloorDropDetail(x)));
        thingies.AddRange(HotKeys.AllDoors.Select(x => x.Item2).Select(x => (HotKeys.DungeonRoomHotKey)new HotKeys.DungeonRoomHotKey.OfDoor(x)));
        var dungeonRoomPanel = makePanel(thingies, HotKeys.DungeonRoomHotKeyProcessor, (c =>
        {
            switch (c)
            {
                case HotKeys.DungeonRoomHotKey.OfRoomType(var rt):
                {
                    var bmp = RoomTypeExtensions.UncompletedBI(rt);
                    return (Control)Graphics.BMPtoImage(bmp);
                }
                case HotKeys.DungeonRoomHotKey.OfMonsterDetail(var md):
                {
                    var i = bmpElseSize((18, 18), md.Bmp());
                    i.HorizontalAlignment = HorizontalAlignment.Left;
                    return i;
                }
                case HotKeys.DungeonRoomHotKey.OfFloorDropDetail(var fd):
                {
                    var i = bmpElseSize((18, 18), fd.Bmp());
                    i.HorizontalAlignment = HorizontalAlignment.Right;
                    return i;
                }
                case HotKeys.DungeonRoomHotKey.OfDoor(var dr):
                {
                    IBrush c2 =
                        dr.Action switch
                        {
                            DoorAction.Increment => Brushes.Green,
                            _ => Brushes.Purple,
                        };
                    var panel =
                        dr.Direction switch
                        {
                            DoorDirection.East =>
                                new DockPanel { Background = c2, Width = 9.0, Height = 9.0, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center },
                            DoorDirection.West =>
                                new DockPanel { Background = c2, Width = 9.0, Height = 9.0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center },
                            DoorDirection.North =>
                                new DockPanel { Background = c2, Width = 14.0, Height = 9.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top },
                            DoorDirection.South =>
                                new DockPanel { Background = c2, Width = 14.0, Height = 9.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom },
                            _ => // Handle unknown values
                                new DockPanel { Background = Brushes.Red, Width = 14.0, Height = 9.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                        };
                    var r = new DockPanel { Width = 39.0, Height = 27.0 };
                    r.Children.Add(panel);
                    return r;
                }
                default: throw new InvalidOperationException();
            }
        }), 39, "DUNGEON");

        var globalPanel = makePanel(GlobalHotkeyTargetsExtensions.All, HotKeys.GlobalHotKeyProcessor, (state => state.AsHotKeyDisplay()), 30, "GLOBALS");
        var all = new StackPanel { Orientation = Orientation.Horizontal };
        all.Children.Add(itemPanel);
        all.Children.Add(overworldPanel);
        all.Children.Add(blockerPanel);
        all.Children.Add(dungeonRoomPanel);
        // Note: contextual keys not displayed here, only on their corresponding menus
        all.Children.Add(globalPanel);
        if (total == 0)
        {
            var tb = DungeonRoomState.mkTxt("You have no HotKeys mapped.\nYou can edit HotKeys.txt to add\nsome, to use the next time you\nrestart the app.");
            tb.FontSize = 16.0;
            all.Children.Add(tb);
            // (a button, rather than opening the folder on its own)
            var openBtn = Graphics.makeButton("Open HotKeys.txt folder", 16.0, Brushes.Orange);
            openBtn.VerticalAlignment = VerticalAlignment.Top;
            openBtn.Margin = new Thickness(8.0);
            openBtn.Click += (_, _) => Platform.RevealInFileManager(HotKeys.HotKeyFilename);
            all.Children.Add(openBtn);
        }
        return (total == 0, new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.0), Background = Brushes.Black, Child = all });   // (only shown in the hotkeys popout: no grey border there)
    }
}
