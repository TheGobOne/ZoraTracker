module OverworldMapTileCustomization

open System.Windows.Controls
open System.Windows
open System.Windows.Media
open Z1R_Tracker.Models.Z1R_TrackerInterop

let OMTW = Graphics.OMTW
let canvasAdd = Graphics.canvasAdd

open DungeonUI.AhhGlobalVariables
open CustomComboBoxes.GlobalFlag

///////////////////////////////////////////////

// there are a few bits of code that need to 'point' at the owItemGrid in the top right of the main tracker
// factor out the locations here

module OW_ITEM_GRID_LOCATIONS =
    let OFFSET = 280.  // the x coordinate of grid; the y is always 30. (just below the numbered triforces in HDN)
    // there is a 6x4 grid of icons, each 30x30, here are their 0-based grid coords
    // the armos / letter / ladder / white sword / magical sword item boxes run across the top row, with their icons directly underneath
    let ARMOS_ICON = 0,1
    let ARMOS_ITEM_BOX = 0,0
    let LETTER_ICON = 1,1
    let LETTER_ITEM_BOX = 1,0
    let LADDER_ICON = 2,1
    let LADDER_ITEM_BOX = 2,0
    let WHITE_SWORD_ICON = 3,1
    let WHITE_SWORD_ITEM_BOX = 3,0
    let MAGS_BOX = 4,1   // magical sword icon (icon only), right of the white sword icon
    let MAGS_ITEM_BOX = 4,0   // its item box, above it
    let WOOD_SWORD_BOX = 3,0
    let BOOMSTICK_BOX = 5,0
    let BOMB_BOX = 0,2   // bomb and money icons, left of the Ganon box
    let RUPEE_ICON = 1,2

    let BLUE_CANDLE_BOX = 2,1
    let WOOD_ARROW_BOX = 3,1
    let BLUE_RING_BOX = 4,0

    let GANON_BOX = 2,2
    let ZELDA_BOX = 3,2

    let HEARTS = 0,3  // and 1,2 and 2,3 and 3,3; nothing at 4,3

    let Locate(gridX, gridY) = (OFFSET + 30.*float gridX, 30. + 30.*float gridY)

///////////////////////////////////////////////

type MapStateProxy(state) =
    static member NumStates = Graphics.theInteriorBmpTable.Length
    member this.State = state
    member this.IsX = state=TrackerModel.MapSquareChoiceDomainHelper.DARK_X
    member this.IsDungeon = state >= 0 && state < 9
    member this.IsWarp = state >= 9 && state < 13
    member this.IsThreeItemShop = TrackerModel.MapSquareChoiceDomainHelper.IsItem(state)
    member this.IsInteresting = not(state = -1 || this.IsX)
    member this.DefaultInteriorBmp() =  // used by grid popup, reminders, and Link destination icons; unchanging so e.g. dungeons in popup are always yellow
        if state = -1 then
            null
        elif this.IsDungeon then
            if TrackerModel.IsHiddenDungeonNumbers() then Graphics.theInteriorBmpTable.[state].[2] else Graphics.theInteriorBmpTable.[state].[0]
        else
            Graphics.theInteriorBmpTable.[state].[0]

let computeExtraDecorationArrow(topX, topY, pos:Point) =
    // in appMainCanvas coordinates:
    // bottom middle of the box, as an arrow target
    let tx,ty = topX+15., topY+30.
    // top middle of the box we are drawing on the map, as an arrow source
    let sx,sy = pos.X+15., pos.Y
    let line,triangle = Graphics.makeArrow(tx, ty, sx, sy, Brushes.Yellow)
    // rectangle for remote box highlight
    let rect = new Shapes.Rectangle(Width=30., Height=30., Stroke=Brushes.Yellow, StrokeThickness=3.)
    let extraDecorations : (FrameworkElement*float*float)[] = [|
        upcast line, -pos.X-3., -pos.Y-3.
        upcast triangle, -pos.X-3., -pos.Y-3.
        upcast rect, topX-pos.X-3., topY-pos.Y-3.
        |]
    extraDecorations

let (sword3x, sword3y) = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.MAGS_ITEM_BOX)
let (letterX, letterY) = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.LETTER_ITEM_BOX)
let (armosX, armosY), (sword2x,sword2y) = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.ARMOS_ITEM_BOX), OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.WHITE_SWORD_ITEM_BOX)

let DoRemoteItemComboBox(cm:CustomComboBoxes.CanvasManager, activationDelta, trackerModelBoxToUpdate:TrackerModel.Box,
                            topX,topY,pos:Point) = async {  // topX,topY,pos are relative to appMainCanvas; top is for tracker box, pos is for mouse-local box
    let extraDecorations = computeExtraDecorationArrow(topX, topY, pos)
    let! r = CustomComboBoxes.DisplayItemComboBox(cm, pos.X, pos.Y, trackerModelBoxToUpdate.CellCurrent(), activationDelta, trackerModelBoxToUpdate.PlayerHas(), extraDecorations, CustomComboBoxes.WarpToCenter)
    match r with
    | Some(newBoxCellValue, newPlayerHas) ->
        trackerModelBoxToUpdate.Set(newBoxCellValue, newPlayerHas)
        TrackerModel.forceUpdate()
    | None -> ()
    }

// like DoRemoteItemComboBox, but there is no matching box on the overworld item grid, so no arrow/highlight
let DoLocalItemComboBox(cm:CustomComboBoxes.CanvasManager, activationDelta, trackerModelBoxToUpdate:TrackerModel.Box, pos:Point) = async {
    let extraDecorations : (FrameworkElement*float*float)[] = [||]
    CustomComboBoxes.itemMenuDomain <- trackerModelBoxToUpdate.Domain
    let! r = CustomComboBoxes.DisplayItemComboBox(cm, pos.X, pos.Y, trackerModelBoxToUpdate.CellCurrent(), activationDelta, trackerModelBoxToUpdate.PlayerHas(), extraDecorations, CustomComboBoxes.WarpToCenter)
    CustomComboBoxes.itemMenuDomain <- TrackerModel.allItemWithHeartShuffleChoiceDomain
    match r with
    | Some(newBoxCellValue, newPlayerHas) ->
        trackerModelBoxToUpdate.Set(newBoxCellValue, newPlayerHas)
        TrackerModel.forceUpdate()
    | None -> ()
    }

let mutable acceleratorCancelled = false   // set by an accelerator whose popup was dismissed without choosing: the caller then puts the tile back the way it was
let CANDLE_TAKEN = 1000   // extra-data value of a wood sword cave tile where the candle was taken
let mutable setDecorIconsHook : int*int*int -> unit = fun _ -> ()   // set by WPFUI (setDecorIcons lives later in compile order)
// clearing a wood sword cave tile takes back whatever was taken there: the sword / candle (one level), or the shuffled item
let clearWoodSwordCaveItems(i, j) =
    let sw1 = TrackerModel.MapSquareChoiceDomainHelper.SWORD1
    let ex = TrackerModel.getOverworldMapExtraData(i,j,sw1)
    let pp = TrackerModel.playerProgressAndTakeAnyHearts
    let si = TrackerModel.startingItemsAndExtras
    TrackerModel.recomputePlayerStateSummary()
    if ex = sw1 then
        let n = max 0 (TrackerModel.playerComputedStateSummary.SwordLevel - 1)
        pp.PlayerHasWoodSword.Set(n>=1)
        si.PlayerHasWhiteSword.Set(n>=2)
        si.PlayerHasMagicalSword.Set(n>=3)
    elif ex = CANDLE_TAKEN then
        let n = max 0 (TrackerModel.playerComputedStateSummary.CandleLevel - 1)
        pp.PlayerHasBlueCandle.Set(n>=1)
        si.PlayerHasRedCandle.Set(n>=2)
    if TrackerModel.shuffledWoodSwordTiles.Contains((i,j)) then
        TrackerModel.sword1Box.Set(-1, TrackerModel.PlayerHas.SKIPPED)
        TrackerModel.shuffledWoodSwordTiles.Remove((i,j)) |> ignore
    TrackerModel.setOverworldMapExtraData(i,j,sw1,0)
    TrackerModel.recomputePlayerStateSummary()   // the item that was found here no longer counts
    TrackerModel.forceUpdate()
// a take-any cave tile remembers what was taken there (which take-any heart slot got marked, and whether that was the blue candle), so clearing the tile takes it back (this session only)
let takeAnyTaken = new System.Collections.Generic.Dictionary<int*int, int*bool*int>()   // (slot, candle was newly taken, kind: 1 heart / 2 candle / 3 potion)
let clearTakeAnyItems(i, j) =
    match takeAnyTaken.TryGetValue((i,j)) with
    | true, (slot, candle, _kind) ->
        let pp = TrackerModel.playerProgressAndTakeAnyHearts
        pp.SetTakeAnyHeart(slot, 0)
        if candle then pp.PlayerHasBlueCandle.Set(false)
        takeAnyTaken.Remove((i,j)) |> ignore
        TrackerModel.recomputePlayerStateSummary()
        TrackerModel.forceUpdate()
    | _ -> ()
let SHUFFLED_ITEM = 2000   // extra-data value of a white / magical sword cave tile that holds a shuffled item
// true if some tile on the map already holds the shuffled item for this kind of cave (collected or not), so 'Shuffled Item' is crossed out for any other tile of that kind
let shuffledItemAlreadyPlaced(state:int) =
    seq { for x in 0..15 do for y in 0..7 do yield (x,y) }
    |> Seq.exists (fun (x,y) -> TrackerModel.overworldMapMarks.[x,y].Current() = state && TrackerModel.getOverworldMapExtraData(x,y,state) = SHUFFLED_ITEM)
// the white / magical sword caves get the same treatment as the wood sword cave (without the candle): took the sword, shuffled item, or just a sword icon
let clearOtherSwordCaveItems(i, j, state) =
    let ex = TrackerModel.getOverworldMapExtraData(i,j,state)
    let si = TrackerModel.startingItemsAndExtras
    if ex = state then
        if state = TrackerModel.MapSquareChoiceDomainHelper.SWORD2 then si.PlayerHasWhiteSword.Set(false) else si.PlayerHasMagicalSword.Set(false)
    let box = if state = TrackerModel.MapSquareChoiceDomainHelper.SWORD2 then TrackerModel.sword2Box else TrackerModel.sword3Box
    if ex = SHUFFLED_ITEM || (ex = state && not(shuffledItemAlreadyPlaced state)) then box.Set(-1, TrackerModel.PlayerHas.SKIPPED)   // the item found here (or the sword taken here) is taken back
    TrackerModel.setOverworldMapExtraData(i,j,state,0)
    TrackerModel.recomputePlayerStateSummary()
    TrackerModel.forceUpdate()
// the letter cave gets the same treatment: took the letter, or a shuffled item
let clearLetterCaveItems(i, j) =
    let ex = TrackerModel.getOverworldMapExtraData(i,j,TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER)
    if ex = SHUFFLED_ITEM || (ex = TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER && not(shuffledItemAlreadyPlaced TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER)) then TrackerModel.letterBox.Set(-1, TrackerModel.PlayerHas.SKIPPED)   // the item found here (or the letter taken here) is taken back
    TrackerModel.setOverworldMapExtraData(i,j,TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER,0)
    TrackerModel.recomputePlayerStateSummary()
    TrackerModel.forceUpdate()
let clearSwordCaveItemsForState(i, j, state) =
    if state = TrackerModel.MapSquareChoiceDomainHelper.SWORD1 then clearWoodSwordCaveItems(i, j)
    elif state = TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER then clearLetterCaveItems(i, j)
    elif state = TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY then clearTakeAnyItems(i, j)
    elif state = TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || state = TrackerModel.MapSquareChoiceDomainHelper.SWORD3 then clearOtherSwordCaveItems(i, j, state)
let isSwordCaveState(state) = state = TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY || state = TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER || state = TrackerModel.MapSquareChoiceDomainHelper.SWORD1 || state = TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || state = TrackerModel.MapSquareChoiceDomainHelper.SWORD3
let overworldAcceleratorTable = new System.Collections.Generic.Dictionary<_,_>()
// put back what was taken at a take any cave (after its chooser was opened again and dismissed without choosing)
let restoreTakeAny(i:int, j:int, slot:int, candle:bool, kind:int, extra:int) =
    let pp = TrackerModel.playerProgressAndTakeAnyHearts
    pp.SetTakeAnyHeart(slot, (if kind = 1 then 1 else 2))
    if candle then pp.PlayerHasBlueCandle.Set(true)
    takeAnyTaken.[(i,j)] <- (slot, candle, kind)
    TrackerModel.setOverworldMapExtraData(i, j, TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY, extra)
    TrackerModel.recomputePlayerStateSummary()
    TrackerModel.forceUpdate()
overworldAcceleratorTable.Add(TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY, (fun (cm:CustomComboBoxes.CanvasManager,_c:Canvas,i,j) -> async {
    let pp = TrackerModel.playerProgressAndTakeAnyHearts
    let hadTaken = takeAnyTaken.ContainsKey((i,j))
    let prevSlot, prevCandle, prevKind = (if hadTaken then takeAnyTaken.[(i,j)] else (0, false, 0))
    let prevExtra = TrackerModel.getOverworldMapExtraData(i,j,TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY)
    // a left click on a take any cave that already has something taken does nothing (right click changes or clears it)
    if not(hadTaken && prevKind <> 0) then
        clearTakeAnyItems(i, j)   // choosing again replaces what was taken here before
        PieMenus.lastTakeAnyKind <- 0
        PieMenus.lastTakeAnyDismissed <- false
        let heartsBefore = [| for h in 0..3 -> pp.GetTakeAnyHeart(h) |]
        let candleBefore = pp.PlayerHasBlueCandle.Value()
        let! shouldMarkTakeAnyAsComplete =
            if TrackerModelOptions.Overworld.UseOldPopups.Value then PieMenus.TakeAnyPieMenuAsync(cm, 666.)   // the old picture popup
            else PieMenus.TakeAnyButtonBoxAsync(cm)
        if PieMenus.lastTakeAnyDismissed && hadTaken && prevKind <> 0 && not(TrackerModelOptions.Overworld.UseOldPopups.Value) then
            // the chooser was opened again and dismissed without choosing: what was taken here stays taken
            restoreTakeAny(i, j, prevSlot, prevCandle, prevKind, prevExtra)
        else
            // remember what was taken here (which heart slot changed, and whether the blue candle was newly taken) so clearing the tile can take it back
            for h in 0..3 do
                if pp.GetTakeAnyHeart(h) <> heartsBefore.[h] && not(takeAnyTaken.ContainsKey((i,j))) then
                    takeAnyTaken.[(i,j)] <- (h, (not candleBefore && pp.PlayerHasBlueCandle.Value()), PieMenus.lastTakeAnyKind)
            TrackerModel.setOverworldMapExtraData(i, j, TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY, if shouldMarkTakeAnyAsComplete then TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY else 0)
    }))
overworldAcceleratorTable.Add(TrackerModel.MapSquareChoiceDomainHelper.SWORD1, (fun (cm:CustomComboBoxes.CanvasManager,c:Canvas,i,j) -> async {
    let pp = TrackerModel.playerProgressAndTakeAnyHearts
    let si = TrackerModel.startingItemsAndExtras
    let s = TrackerModel.playerComputedStateSummary
    acceleratorCancelled <- false
    let hadShuffled = TrackerModel.shuffledWoodSwordTiles.Remove((i,j))
    // only one wood sword cave can hold a non-sword item: once another wood sword cave has had the candle taken or holds the shuffled item, this one can only be the sword (or a sword icon / leave)
    let candleTakenElsewhere =
        seq { for x in 0..15 do for y in 0..7 do yield (x,y) }
        |> Seq.exists (fun (x,y) -> (x,y) <> (i,j) && TrackerModel.overworldMapMarks.[x,y].Current() = TrackerModel.MapSquareChoiceDomainHelper.SWORD1 && TrackerModel.getOverworldMapExtraData(x,y,TrackerModel.MapSquareChoiceDomainHelper.SWORD1) = CANDLE_TAKEN)
    if TrackerModel.shuffledWoodSwordTiles.Count > 0 || candleTakenElsewhere then PieMenus.crossedOutChoices <- ["Shuffled Item"; "Took Candle"]
    elif hadShuffled then PieMenus.crossedOutChoices <- ["Shuffled Item"]
    // one box of 4 buttons; it also supports the Contextual_TakeThis_* hotkeys (sword / candle / none)
    let! choice =
        if TrackerModelOptions.Overworld.UseOldPopups.Value then async {
            // the old picture popup: sword / candle / leave, plus a 4th choice for the less common 'Shuffled Item' / 'Sword Icon'
            let! r = PieMenus.TakeThisChoicePieMenuAsync(cm, 666.)
            match r with
            | 1 -> return "Took Sword"
            | 2 -> return "Took Candle"
            | 3 -> return! PieMenus.CaveChoiceBoxAsync(cm, ["Shuffled Item"; "Sword Icon"])
            | _ -> return "Leave"
            }
        else PieMenus.TakeThisButtonBoxAsync(cm)
    PieMenus.crossedOutChoices <- []   // (the old pie menu path doesn't consume it)
    let mutable doneValue = 0   // 0 = not taken yet, SWORD1 = took the sword, CANDLE_TAKEN = took the candle
    match choice with
    | "Took Sword" ->
        let n = min 3 (s.SwordLevel + 1)
        pp.PlayerHasWoodSword.Set(n>=1)
        si.PlayerHasWhiteSword.Set(n>=2)
        si.PlayerHasMagicalSword.Set(n>=3)
        doneValue <- TrackerModel.MapSquareChoiceDomainHelper.SWORD1
    | "Took Candle" ->
        let n = min 2 (s.CandleLevel + 1)
        pp.PlayerHasBlueCandle.Set(n>=1)
        si.PlayerHasRedCandle.Set(n>=2)
        doneValue <- CANDLE_TAKEN
    | "Shuffled Item" ->
        TrackerModel.shuffledWoodSwordTiles.Add((i,j)) |> ignore
        let pos = c.TranslatePoint(Point(OMTW/2.-15.,1.), cm.AppMainCanvas)  // place to draw the local box
        do! DoLocalItemComboBox(cm, 0, TrackerModel.sword1Box, pos)
    | "Sword Icon" ->
        let before = TrackerModel.owSwordIcons.[i,j]
        setDecorIconsHook (i, j, TrackerModel.decorIconsToggle(before, TrackerModel.ITEMS.WOODSWORD))
        if hadShuffled then
            TrackerModel.shuffledWoodSwordTiles.Add((i,j)) |> ignore   // the cave keeps its shuffled item; only the icon is added
        // like any other marker icon, the tile itself becomes an 'unmarked, finished' tile that shows the icon
        elif not(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]).IsEmpty) then
            TrackerModel.overworldMapMarks.[i,j].AttemptToSet(TrackerModel.MapSquareChoiceDomainHelper.DARK_X) |> ignore
    | "Leave" -> ()   // as before: marks the cave as not taken
    | _ -> acceleratorCancelled <- true   // clicked outside the choices: nothing gets placed
    if not acceleratorCancelled then
        TrackerModel.setOverworldMapExtraData(i, j, TrackerModel.MapSquareChoiceDomainHelper.SWORD1, doneValue)
    elif hadShuffled then
        TrackerModel.shuffledWoodSwordTiles.Add((i,j)) |> ignore   // dismissed: the cave still holds its shuffled item
    }))
overworldAcceleratorTable.Add(TrackerModel.MapSquareChoiceDomainHelper.ARMOS, (fun (cm:CustomComboBoxes.CanvasManager,c:Canvas,_i,_j) -> async {
    let pos = c.TranslatePoint(Point(OMTW/2.-15.,1.), cm.AppMainCanvas)  // place to draw the local box
    do! DoRemoteItemComboBox(cm, 0, TrackerModel.armosBox, armosX, armosY, pos)
    }))
let swordCaveAccelerator(state:int, box:TrackerModel.Box, boxX:float, boxY:float, decorIdent:int, takeSword:unit->unit) =
    (fun (cm:CustomComboBoxes.CanvasManager,c:Canvas,i,j) -> async {
        acceleratorCancelled <- false
        let hadShuffled = TrackerModel.getOverworldMapExtraData(i,j,state) = SHUFFLED_ITEM
        if hadShuffled || shuffledItemAlreadyPlaced state then PieMenus.crossedOutChoices <- ["Shuffled Item"]   // this cave, or another one of the same kind, already holds a shuffled item
        let! choice = PieMenus.CaveChoiceBoxAsync(cm, ["Took Sword"; "Shuffled Item"; "Sword Icon"])
        let mutable doneValue = 0   // 0 = nothing yet, state = took the sword, SHUFFLED_ITEM = the cave holds a shuffled item
        match choice with
        | "Took Sword" ->
            takeSword()
            // the sword also fills in this cave's item box (and sword icon) in the overworld item area up top
            // (but never over a shuffled item already found in one of these caves)
            if not(hadShuffled || shuffledItemAlreadyPlaced state) then
                box.Set((if state = TrackerModel.MapSquareChoiceDomainHelper.SWORD2 then TrackerModel.ITEMS.WHITESWORD else TrackerModel.ITEMS.MAGICALSWORD), TrackerModel.PlayerHas.YES)
                TrackerModel.forceUpdate()
            doneValue <- state
        | "Shuffled Item" ->
            doneValue <- SHUFFLED_ITEM
            let pos = c.TranslatePoint(Point(OMTW/2.-15.,1.), cm.AppMainCanvas)  // place to draw the local box
            do! DoRemoteItemComboBox(cm, 0, box, boxX, boxY, pos)
        | "Sword Icon" ->
            let before = TrackerModel.owSwordIcons.[i,j]
            setDecorIconsHook (i, j, TrackerModel.decorIconsToggle(before, decorIdent))
            if hadShuffled then doneValue <- SHUFFLED_ITEM   // the cave keeps its shuffled item; only the icon is added
            elif not(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]).IsEmpty) then
                TrackerModel.overworldMapMarks.[i,j].AttemptToSet(TrackerModel.MapSquareChoiceDomainHelper.DARK_X) |> ignore
        | _ -> acceleratorCancelled <- true
        if not acceleratorCancelled then
            TrackerModel.setOverworldMapExtraData(i, j, state, doneValue)
        })
// the letter cave: took the letter, a shuffled item, or a letter icon (like the sword caves; the item is shown in a local box that points at the letter item box up top)
overworldAcceleratorTable.Add(TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER, (fun (cm:CustomComboBoxes.CanvasManager,c:Canvas,i,j) -> async {
    acceleratorCancelled <- false
    if TrackerModel.getOverworldMapExtraData(i,j,TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER) = SHUFFLED_ITEM || shuffledItemAlreadyPlaced TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER then PieMenus.crossedOutChoices <- ["Shuffled Item"]   // this cave, or another letter cave, already holds a shuffled item
    let! choice = PieMenus.CaveChoiceBoxAsync(cm, ["Took Letter"; "Shuffled Item"])
    let mutable doneValue = 0   // 0 = nothing yet, THE_LETTER = took the letter, SHUFFLED_ITEM = the cave holds a shuffled item
    match choice with
    | "Took Letter" ->
        // fills in the letter item box up top (but never over a shuffled item already found in a letter cave)
        if not(shuffledItemAlreadyPlaced TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER) then
            TrackerModel.letterBox.Set(TrackerModel.ITEMS.LETTER, TrackerModel.PlayerHas.YES)
            TrackerModel.forceUpdate()
        doneValue <- TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER
    | "Shuffled Item" ->
        doneValue <- SHUFFLED_ITEM
        let pos = c.TranslatePoint(Point(OMTW/2.-15.,1.), cm.AppMainCanvas)  // place to draw the local box
        do! DoRemoteItemComboBox(cm, 0, TrackerModel.letterBox, letterX, letterY, pos)
    | _ -> acceleratorCancelled <- true
    if not acceleratorCancelled then
        TrackerModel.setOverworldMapExtraData(i, j, TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER, doneValue)
    }))
overworldAcceleratorTable.Add(TrackerModel.MapSquareChoiceDomainHelper.SWORD2,
    swordCaveAccelerator(TrackerModel.MapSquareChoiceDomainHelper.SWORD2, TrackerModel.sword2Box, sword2x, sword2y, TrackerModel.ITEMS.WHITESWORD, (fun () -> TrackerModel.startingItemsAndExtras.PlayerHasWhiteSword.Set(true))))
overworldAcceleratorTable.Add(TrackerModel.MapSquareChoiceDomainHelper.SWORD3,
    swordCaveAccelerator(TrackerModel.MapSquareChoiceDomainHelper.SWORD3, TrackerModel.sword3Box, sword3x, sword3y, TrackerModel.ITEMS.MAGICALSWORD, (fun () -> TrackerModel.startingItemsAndExtras.PlayerHasMagicalSword.Set(true))))

let makeSwordLeftSideFullTileBmpN(swordState, interiorIndex) =
    let interiorBmp = Graphics.theInteriorBmpTable.[swordState].[interiorIndex]
    let fullTileBmp = new System.Drawing.Bitmap(16*3,11*3)
    let BG = System.Drawing.Color.FromArgb(int Graphics.TRANS_BG.A, int Graphics.TRANS_BG.R, int Graphics.TRANS_BG.G, int Graphics.TRANS_BG.B)   // the compiler is being stupid, clone this color to workaround
    for px = 0 to 16*3-1 do
        for py = 0 to 11*3-1 do
            if px>=1*3 && px<6*3 && py>=1*3 && py<10*3 then 
                fullTileBmp.SetPixel(px, py, interiorBmp.GetPixel(px-1*3, py-1*3))
            else
                fullTileBmp.SetPixel(px, py, BG)
    fullTileBmp
let makeSwordLeftSideFullTileBmp(swordState) = makeSwordLeftSideFullTileBmpN(swordState, 0)
let sword1LeftSideFullTileBmp = makeSwordLeftSideFullTileBmp(TrackerModel.MapSquareChoiceDomainHelper.SWORD1)
let sword1LeftSideDarkFullTileBmp = makeSwordLeftSideFullTileBmpN(TrackerModel.MapSquareChoiceDomainHelper.SWORD1, 1)   // dark version, sword at the left so an item fits beside it
// the take-any cave tile with its icon at the left (dark), so what was taken fits beside it; falls back to the plain dark tile
let takeAnyLeftSideDarkFullTileBmp =
    try makeSwordLeftSideFullTileBmpN(TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY, 1)
    with _ -> Graphics.theFullTileBmpTable.[TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY].[1]
let sword2LeftSideFullTileBmp = makeSwordLeftSideFullTileBmp(TrackerModel.MapSquareChoiceDomainHelper.SWORD2)
let sword2LeftSideDarkFullTileBmp = makeSwordLeftSideFullTileBmpN(TrackerModel.MapSquareChoiceDomainHelper.SWORD2, 1)
let sword3LeftSideDarkFullTileBmp = makeSwordLeftSideFullTileBmpN(TrackerModel.MapSquareChoiceDomainHelper.SWORD3, 1)
// hint shop tiles can have their purple background changed (scroll the mouse wheel on the tile): 0 = purple, 1 = red, 2 = green, 3 = orange (4 = grey, used only by the Spot Summary until the user picks a color)
let hintShopColors = TrackerModel.hintShopColors   // (lives in the model so it is saved and loaded with everything else)
let mutable lastHintShopColor = 0   // the color the user last chose; new hint shop tiles start with it
let mutable hintShopColorChosen = false   // true once the user has scrolled a hint shop to a color (the Spot Summary shows its hint boxes grey until then)
let hintShopColorOf(i,j) =
    match hintShopColors.TryGetValue((i,j)) with
    | true, k -> k
    | _ ->
        hintShopColors.[(i,j)] <- lastHintShopColor   // (pin it, so later color changes don't recolor tiles already drawn)
        lastHintShopColor
let private recolorPurple(src:System.Drawing.Bitmap, kind:int) : System.Drawing.Bitmap =
    let isPurple(p:System.Drawing.Color) = p.A > 0uy && int p.R > int p.G + 25 && int p.B > int p.G + 25
    let mutable refMax = 1
    for x = 0 to src.Width-1 do
        for y = 0 to src.Height-1 do
            let p = src.GetPixel(x, y)
            if isPurple p then refMax <- max refMax (max (int p.R) (int p.B))
    let tr, tg, tb = (match kind with | 1 -> 220, 30, 30 | 2 -> 30, 170, 50 | 4 -> 150, 150, 150 | _ -> 255, 150, 0)
    let r = new System.Drawing.Bitmap(src.Width, src.Height)
    for x = 0 to src.Width-1 do
        for y = 0 to src.Height-1 do
            let p = src.GetPixel(x, y)
            if isPurple p then
                let f = float (max (int p.R) (int p.B)) / float refMax
                r.SetPixel(x, y, System.Drawing.Color.FromArgb(int p.A, min 255 (int (float tr * f)), min 255 (int (float tg * f)), min 255 (int (float tb * f))))
            else r.SetPixel(x, y, p)
    r
let private hintShopRecolorCache = new System.Collections.Generic.Dictionary<int*bool, System.Drawing.Bitmap>()
let hintShopTileBmp(kind:int, dark:bool) : System.Drawing.Bitmap =
    let src = Graphics.theFullTileBmpTable.[TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP].[(if dark then 1 else 0)]
    if kind = 0 then src else
    match hintShopRecolorCache.TryGetValue((kind, dark)) with
    | true, b -> b
    | _ ->
        let r = recolorPurple(src, kind)
        hintShopRecolorCache.[(kind, dark)] <- r
        r
// the hint shop icon shown in the overworld tile menu, in the color the user last chose
let private hintShopInteriorCache = new System.Collections.Generic.Dictionary<int, System.Drawing.Bitmap>()
let hintShopMenuBmp(kind:int) : System.Drawing.Bitmap =
    let src = TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP |> fun n -> Graphics.theInteriorBmpTable.[n].[0]
    if kind = 0 then src else
    match hintShopInteriorCache.TryGetValue(kind) with
    | true, b -> b
    | _ ->
        let r = recolorPurple(src, kind)
        hintShopInteriorCache.[kind] <- r
        r
// ---- shop icons: tiny pixel-font bitmaps (3x5 letters / digits), used by the overworld menu's 'Shop' cell and the Spot Summary ----
let private shopFont : System.Collections.Generic.IDictionary<char, string[]> =
    dict [ 'S', [| "###"; "#.."; "###"; "..#"; "###" |]
           'H', [| "#.#"; "#.#"; "###"; "#.#"; "#.#" |]
           'O', [| "###"; "#.#"; "#.#"; "#.#"; "###" |]
           'P', [| "###"; "#.#"; "###"; "#.."; "#.." |]
           '1', [| ".#."; "##."; ".#."; ".#."; "###" |]
           '2', [| "###"; "..#"; "###"; "#.."; "###" |]
           '3', [| "###"; "..#"; "###"; "..#"; "###" |]
           '4', [| "#.#"; "#.#"; "###"; "..#"; "..#" |]
           '5', [| "###"; "#.."; "###"; "..#"; "###" |] ]
let private drawShopGlyph(bmp:System.Drawing.Bitmap, ch:char, x0:int, y0:int, scale:int, color:System.Drawing.Color) =
    let rows = shopFont.[ch]
    for r = 0 to 4 do
        for c = 0 to 2 do
            if rows.[r].[c] = '#' then
                for dx = 0 to scale-1 do
                    for dy = 0 to scale-1 do
                        let x, y = x0 + c*scale + dx, y0 + r*scale + dy
                        if x >= 0 && x < bmp.Width && y >= 0 && y < bmp.Height then bmp.SetPixel(x, y, color)
// the 'Shop' cell of the overworld menu: S-H-O-P stepping diagonally down and to the right, centered in the 21x27 cell
let shopMenuBmp : System.Drawing.Bitmap =
    let bmp = new System.Drawing.Bitmap(21, 27)
    using (System.Drawing.Graphics.FromImage(bmp)) (fun g -> g.Clear(System.Drawing.Color.Black))
    let gold = System.Drawing.Color.FromArgb(255, 255, 215, 0)
    "SHOP" |> Seq.iteri (fun k ch -> drawShopGlyph(bmp, ch, 5 + 3*k, 4 + 5*k, 1, gold))
    bmp
// a numbered shop for the Spot Summary: tiny 'SHOP' across the top and the shop's number big underneath
let private shopNumberBmps = new System.Collections.Generic.Dictionary<int, System.Drawing.Bitmap>()
let shopNumberBmp(n:int) : System.Drawing.Bitmap =
    match shopNumberBmps.TryGetValue(n) with
    | true, b -> b
    | _ ->
        let bmp = new System.Drawing.Bitmap(21, 27)
        using (System.Drawing.Graphics.FromImage(bmp)) (fun g -> g.Clear(System.Drawing.Color.Black))
        let gold = System.Drawing.Color.FromArgb(255, 255, 215, 0)
        "SHOP" |> Seq.iteri (fun k ch -> drawShopGlyph(bmp, ch, 3 + 4*k, 1, 1, gold))
        drawShopGlyph(bmp, (string n).[0], 6, 9, 3, System.Drawing.Color.White)
        shopNumberBmps.[n] <- bmp
        bmp
// the armos tile with its icon moved to the left edge (so the item found under the armos fits beside it); the icon is whatever differs from the tile background
let armosLeftSideTileBmp(dark:bool) : System.Drawing.Bitmap =
    let src = Graphics.theFullTileBmpTable.[TrackerModel.MapSquareChoiceDomainHelper.ARMOS].[(if dark then 1 else 0)]
    let bg = src.GetPixel(0, 0)
    let mutable minX, minY, maxX, maxY = src.Width, src.Height, -1, -1
    for x = 0 to src.Width-1 do
        for y = 0 to src.Height-1 do
            if src.GetPixel(x, y).ToArgb() <> bg.ToArgb() then
                minX <- min minX x; maxX <- max maxX x; minY <- min minY y; maxY <- max maxY y
    if maxX < 0 then src else
    let dst = new System.Drawing.Bitmap(src.Width, src.Height)
    for x = 0 to dst.Width-1 do
        for y = 0 to dst.Height-1 do
            dst.SetPixel(x, y, bg)
    let shift = minX - 3   // the icon starts 3px (one art pixel) from the left edge, like the cave tiles
    for x = minX to maxX do
        for y = minY to maxY do
            dst.SetPixel(x - shift, y, src.GetPixel(x, y))
    dst
let armosLeftSideBrightBmp = lazy (armosLeftSideTileBmp false)
let armosLeftSideDarkBmp = lazy (armosLeftSideTileBmp true)
// the marker-icon look used for items placed on a tile by themselves: the icon with a thick black outline (see WPFUI.decorIconBmp)
let withDecorOutline(src:System.Drawing.Bitmap) : System.Drawing.Bitmap =
    let B = 3
    let w, h = src.Width, src.Height
    let dst = new System.Drawing.Bitmap(w + 2*B, h + 2*B)
    let isInk(x, y) = let p = src.GetPixel(x, y) in p.A > 0uy && (int p.R + int p.G + int p.B) > 40
    for x = 0 to w-1 do
        for y = 0 to h-1 do
            if isInk(x, y) then
                for dx = 0 to 2*B do
                    for dy = 0 to 2*B do
                        dst.SetPixel(x+dx, y+dy, System.Drawing.Color.Black)
    for x = 0 to w-1 do
        for y = 0 to h-1 do
            let p = src.GetPixel(x, y)
            if p.A > 0uy then dst.SetPixel(x+B, y+B, p)
    dst
// the letter (not shuffled) on a tile: the same letter sprite as the item menus, outlined like a marker icon, centered on a plain tile (no brown cave background)
let letterTileBmp(dark:bool, leftAligned:bool) : System.Drawing.Bitmap =
    // the item-menu letter sprite; its own near-black background pixels are dropped so only the letter itself gets the outline (no black box)
    let raw = CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.LETTER, None)
    let cleaned = new System.Drawing.Bitmap(raw.Width, raw.Height)
    for x = 0 to raw.Width-1 do
        for y = 0 to raw.Height-1 do
            let p = raw.GetPixel(x, y)
            if p.A > 0uy && (int p.R + int p.G + int p.B) > 40 then cleaned.SetPixel(x, y, p)
    let icon = withDecorOutline cleaned   // black outline hugging the letter
    let BG = System.Drawing.Color.FromArgb(int Graphics.TRANS_BG.A, int Graphics.TRANS_BG.R, int Graphics.TRANS_BG.G, int Graphics.TRANS_BG.B)
    let tile = new System.Drawing.Bitmap(16*3, 11*3)
    for px = 0 to 16*3-1 do
        for py = 0 to 11*3-1 do
            tile.SetPixel(px, py, BG)
    let ox = if leftAligned then 0 else (tile.Width - icon.Width)/2
    let oy = (tile.Height - icon.Height)/2
    for x = 0 to icon.Width-1 do
        for y = 0 to icon.Height-1 do
            let p = icon.GetPixel(x, y)
            let p = if dark then System.Drawing.Color.FromArgb(int p.A, int p.R / 2, int p.G / 2, int p.B / 2) else p   // (darkened by hand so the transparent pixels stay transparent: no black box)
            let tx, ty = x+ox, y+oy
            if p.A > 0uy && tx >= 0 && ty >= 0 && tx < tile.Width && ty < tile.Height then tile.SetPixel(tx, ty, p)
    tile
let letterPlainTileBmp(dark:bool) = letterTileBmp(dark, false)
let letterLeftSideFullTileBmp() = letterTileBmp(false, true)
let letterLeftSideDarkFullTileBmp() = letterTileBmp(true, true)
// the green check mark drawn on a collected marker icon (same as WPFUI.decorCheckmark)
let caveCheckmark(iconSize:float) : FrameworkElement =
    let s = max 8. (iconSize * 0.55)
    let c = new Canvas(Width=s, Height=s, IsHitTestVisible=false)
    let pts() = new PointCollection([ Point(s*0.08, s*0.55); Point(s*0.38, s*0.88); Point(s*0.94, s*0.12) ])
    c.Children.Add(new System.Windows.Shapes.Polyline(Points=pts(), Stroke=Brushes.Black, StrokeThickness=max 3.5 (s*0.42), StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round, StrokeLineJoin=PenLineJoin.Round)) |> ignore
    c.Children.Add(new System.Windows.Shapes.Polyline(Points=pts(), Stroke=Brushes.LimeGreen, StrokeThickness=max 2. (s*0.24), StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round, StrokeLineJoin=PenLineJoin.Round)) |> ignore
    upcast c
// a cave's shuffled item, drawn to the right of the (left-aligned) sword icon at the sword icon's height, in the same style as an item placed on a tile by itself
let makeBmpBesideSword(bmp:System.Drawing.Bitmap) : Canvas =
    let swordIconHeight = 27.
    let ob = if bmp <> null then withDecorOutline bmp else null
    let w = if ob <> null then min 27. (swordIconHeight * float ob.Width / float ob.Height) else 0.
    let iconCanvas = new Canvas(Width=w+2., Height=swordIconHeight+2.)
    if ob <> null then
        let icon = Graphics.BMPtoImage(ob)
        icon.Stretch <- Stretch.Uniform
        icon.Height <- swordIconHeight
        icon.Width <- w
        canvasAdd(iconCanvas, icon, 1., 1.)
    iconCanvas
let makeCaveItemBesideSword(box:TrackerModel.Box) : Canvas =
    let c = makeBmpBesideSword(CustomComboBoxes.boxCurrentBMP(box.CellCurrent(), None))
    if box.PlayerHas() = TrackerModel.PlayerHas.YES && box.CellCurrent() <> -1 then
        let ck = caveCheckmark(27.)
        canvasAdd(c, ck, 1. + (c.Width - 2. - ck.Width)/2., 1. + (27. - ck.Height)/2.)
    c.Background <- Brushes.Transparent   // so the whole icon area gets mouse events
    c.ToolTip <- "Left click: item collected / right click: not collected\n(click the same one again to un-check)"
    c.MouseDown.Add(fun ea ->
        if ea.ButtonState = System.Windows.Input.MouseButtonState.Pressed &&
                (ea.ChangedButton = System.Windows.Input.MouseButton.Left || ea.ChangedButton = System.Windows.Input.MouseButton.Right) then
            ea.Handled <- true
            let desire = CustomComboBoxes.MouseButtonEventArgsToPlayerHas ea
            box.SetPlayerHas(if desire = box.PlayerHas() then TrackerModel.PlayerHas.NO else desire)
            TrackerModel.forceUpdate())
    c
// the green check drawn over the sword icon of a sword cave tile where the sword was taken (centered on the tile)
let takenSwordCheck() : Canvas * float * float =
    let ck = caveCheckmark(27.)
    let host = new Canvas(Width=ck.Width, Height=ck.Height, IsHitTestVisible=false)   // (a Canvas, like the other tile decorations)
    host.Children.Add(ck) |> ignore
    host, OMTW/2. - ck.Width/2., 33./2. - ck.Height/2.
let sword3LeftSideFullTileBmp = makeSwordLeftSideFullTileBmp(TrackerModel.MapSquareChoiceDomainHelper.SWORD3)

// in a shop tile, the bomb (item 1) is drawn as the real bomb icon fitted to its slot rather than from the thin 3x7 item strip
let paintBombInSlot(tile:System.Drawing.Bitmap, x0:int, y0:int) =
    let b = Graphics.bombFitBmp(9, 21, Graphics.itemBackgroundColor)
    for x = 0 to 8 do
        for y = 0 to 20 do
            tile.SetPixel(x0+x, y0+y, b.GetPixel(x,y))
let twoItemShopBmpCache   = new System.Collections.Generic.Dictionary<_,_>()
let threeItemShopBmpCache = new System.Collections.Generic.Dictionary<_,_>()
let makeTwoItemShopBmp(item1, item2) =  // 0-based, -1 for blank
    if item1 = item2 then
        let state = item1 + TrackerModel.MapSquareChoiceDomainHelper.ARROW
        Graphics.theFullTileBmpTable.[state].[0]
    else
        match twoItemShopBmpCache.TryGetValue((item1,item2)) with
        | true, r -> r
        | _ ->
            // cons up a two-item shop image
            let tile = new System.Drawing.Bitmap(16*3,11*3)
            for px = 0 to 16*3-1 do
                for py = 0 to 11*3-1 do
                    // two-icon area
                    if px/3 >= 3 && px/3 <= 11 && py/3 >= 1 && py/3 <= 9 then
                        tile.SetPixel(px, py, Graphics.itemBackgroundColor)
                    else
                        tile.SetPixel(px, py, Graphics.TRANS_BG)
                    if item1 >=0 then
                        // icon 1
                        if px/3 >= 4 && px/3 <= 6 && py/3 >= 2 && py/3 <= 8 then
                            let c = Graphics.itemsBMP.GetPixel(item1*3 + px/3-4, py/3-2)
                            if c.ToArgb() <> System.Drawing.Color.Black.ToArgb() then
                                tile.SetPixel(px, py, c)
                    if item2 >= 0 then
                        // icon 2
                        if px/3 >= 8 && px/3 <= 10 && py/3 >= 2 && py/3 <= 8 then
                            let c = Graphics.itemsBMP.GetPixel(item2*3 + px/3-8, py/3-2)
                            if c.ToArgb() <> System.Drawing.Color.Black.ToArgb() then
                                tile.SetPixel(px, py, c)
            if item1 = 1 then paintBombInSlot(tile, 12, 6)
            if item2 = 1 then paintBombInSlot(tile, 24, 6)
            twoItemShopBmpCache.Add((item1,item2), tile)
            tile

let makeThreeItemShopBmp(item1, item2, item3) =  // 0-based, -1 for blank
    match threeItemShopBmpCache.TryGetValue((item1,item2,item3)) with
    | true, r -> r
    | _ ->
        let tile = new System.Drawing.Bitmap(16*3,11*3)
        for px = 0 to 16*3-1 do
            for py = 0 to 11*3-1 do
                if px/3 >= 3 && px/3 <= 11 && py/3 >= 1 && py/3 <= 9 then
                    tile.SetPixel(px, py, Graphics.itemBackgroundColor)
                else
                    tile.SetPixel(px, py, Graphics.TRANS_BG)
                if item1 >= 0 then
                    if px/3 >= 3 && px/3 <= 5 && py/3 >= 2 && py/3 <= 8 then
                        let c = Graphics.itemsBMP.GetPixel(item1*3 + px/3-3, py/3-2)
                        if c.ToArgb() <> System.Drawing.Color.Black.ToArgb() then
                            tile.SetPixel(px, py, c)
                if item2 >= 0 then
                    if px/3 >= 6 && px/3 <= 8 && py/3 >= 2 && py/3 <= 8 then
                        let c = Graphics.itemsBMP.GetPixel(item2*3 + px/3-6, py/3-2)
                        if c.ToArgb() <> System.Drawing.Color.Black.ToArgb() then
                            tile.SetPixel(px, py, c)
                if item3 >= 0 then
                    if px/3 >= 9 && px/3 <= 11 && py/3 >= 2 && py/3 <= 8 then
                        let c = Graphics.itemsBMP.GetPixel(item3*3 + px/3-9, py/3-2)
                        if c.ToArgb() <> System.Drawing.Color.Black.ToArgb() then
                            tile.SetPixel(px, py, c)
        if item1 = 1 then paintBombInSlot(tile, 9, 6)
        if item2 = 1 then paintBombInSlot(tile, 18, 6)
        if item3 = 1 then paintBombInSlot(tile, 27, 6)
        threeItemShopBmpCache.Add((item1,item2,item3), tile)
        tile

let temporarilyDisplayHiddenOverworldTileMarks = Array2D.create 16 8 false
let ShouldHide(state,i,j) =
    let r = 
        if temporarilyDisplayHiddenOverworldTileMarks.[i,j] || TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Value() then
            false
        else
            TrackerModel.MapSquareChoiceDomainHelper.AsTrackerModelOptionsOverworldTilesToHide(state).Value
    //printfn "SH(%d)=%A" state r
    r

let IsHideableShopItem(i) = // 0-based
    (i=0 && TrackerModel.playerComputedStateSummary.ArrowLevel>0)
    || (i=2 && TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBoomBook.Value())
    || (i=3 && TrackerModel.playerComputedStateSummary.CandleLevel>0)
    || (i=4 && TrackerModel.playerComputedStateSummary.RingLevel>0)
    || (i=6 && TrackerModel.playerComputedStateSummary.HaveAnyKey)
    || (i=5 && TrackerModelOptions.OverworldTilesToHide.AlwaysHideMeatShops.Value)
        
let GetIconBMPAndExtraDecorations(cm, ms:MapStateProxy,i,j) =   // returns: (shouldAppearLikeDarkX,iconBmp,[decos])
    if ms.State = -1 then
        // Unmarked tile; still check for a stored item icon (right-click icon feature on AlwaysEmpty tiles)
        if TrackerModelOptions.AllowItemIconOnNonShopTile.Value then
            let item2 = TrackerModel.getShopItem2_1based(i,j)
            if item2 <> 0 then
                false, Graphics.theFullTileBmpTable.[item2 - 1 + TrackerModel.MapSquareChoiceDomainHelper.ARROW].[0], []
            else
                false, null, []
        else
            false, null, []
    elif ms.IsThreeItemShop then
        let items = ResizeArray()
        items.Add(ms.State - TrackerModel.MapSquareChoiceDomainHelper.ARROW)  // 0-based
        let item2 = TrackerModel.getShopItem2_1based(i,j)
        let item3 = TrackerModel.getShopItem3_1based(i,j)
        if item2 <> 0 then items.Add(item2 - 1)   // 0-based
        if item3 <> 0 then items.Add(item3 - 1)   // 0-based
        let itemsToShow =
            if ShouldHide(TrackerModel.MapSquareChoiceDomainHelper.SHOP, i, j) then
                let itemsToShow = ResizeArray()
                for x in items do
                    if not(IsHideableShopItem(x)) then
                        itemsToShow.Add(x)
                itemsToShow
            else
                items
        if itemsToShow.Count = 3 then
            let tile = makeThreeItemShopBmp(itemsToShow.[0], itemsToShow.[1], itemsToShow.[2])
            false, tile, []
        elif itemsToShow.Count = 2 then
            let tile = makeTwoItemShopBmp(itemsToShow.[0], itemsToShow.[1])
            false, tile, []
        elif itemsToShow.Count = 1 then
            false, Graphics.theFullTileBmpTable.[itemsToShow.[0] + TrackerModel.MapSquareChoiceDomainHelper.ARROW].[0], []
        else
            true, Graphics.theFullTileBmpTable.[ms.State].[0], []
    // secrets and potion letter default to being dark
    elif ms.State = TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET then
        if TrackerModel.getOverworldMapExtraData(i,j,ms.State)=TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET then
            false, Graphics.theFullTileBmpTable.[ms.State].[0], []
        else
            ShouldHide(ms.State,i,j), Graphics.theFullTileBmpTable.[ms.State].[1], []
    elif ms.State = TrackerModel.MapSquareChoiceDomainHelper.MEDIUM_SECRET then
        if TrackerModel.getOverworldMapExtraData(i,j,ms.State)=TrackerModel.MapSquareChoiceDomainHelper.MEDIUM_SECRET then
            false, Graphics.theFullTileBmpTable.[ms.State].[0], []
        else
            ShouldHide(ms.State,i,j), Graphics.theFullTileBmpTable.[ms.State].[1], []
    elif ms.State = TrackerModel.MapSquareChoiceDomainHelper.SMALL_SECRET then
        if TrackerModel.getOverworldMapExtraData(i,j,ms.State)=TrackerModel.MapSquareChoiceDomainHelper.SMALL_SECRET then
            false, Graphics.theFullTileBmpTable.[ms.State].[0], []
        else
            ShouldHide(ms.State,i,j), Graphics.theFullTileBmpTable.[ms.State].[1], []
    elif ms.State = TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER then
        let ex = TrackerModel.getOverworldMapExtraData(i,j,ms.State)
        if ex = SHUFFLED_ITEM && not(TrackerModel.letterBox.IsDone()) then
            let extraDecorationsF(boxPos:Point) =
                let extraDecorations = computeExtraDecorationArrow(letterX, letterY, boxPos)
                seq extraDecorations
            false, letterLeftSideFullTileBmp(), [fst(Views.MakeBoxItemWithExtraDecorations(Some(cm), TrackerModel.letterBox, false, Some extraDecorationsF)), OMTW-30., 1.]
        elif ex = SHUFFLED_ITEM then
            ShouldHide(ms.State,i,j), letterLeftSideDarkFullTileBmp(), [makeCaveItemBesideSword(TrackerModel.letterBox), 20., 2.]
        elif ex = ms.State || TrackerModel.letterBox.IsDone() then   // took the letter: dark, like the white sword cave once taken
            // with other marker icons on the tile, the letter moves to the left like the other cave tiles do, leaving room for them on the right
            let hasOtherIcons = not(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]).IsEmpty)
            ShouldHide(ms.State,i,j), (if hasOtherIcons then letterLeftSideDarkFullTileBmp() else letterPlainTileBmp(true)), []
        else
            let hasOtherIcons = not(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]).IsEmpty)
            false, (if hasOtherIcons then letterLeftSideFullTileBmp() else letterPlainTileBmp(false)), []   // not yet taken: bright
    // door repair always dark (but light in the grid selector)
    elif ms.State = TrackerModel.MapSquareChoiceDomainHelper.DOOR_REPAIR_CHARGE then
        ShouldHide(ms.State,i,j), Graphics.theFullTileBmpTable.[ms.State].[1], []
    // armos dark if player already got
    elif ms.State = TrackerModel.MapSquareChoiceDomainHelper.ARMOS then
        if TrackerModel.armosBox.CellCurrent() <> -1 then
            // the item found under the armos is drawn beside the armos icon (click it to mark it collected)
            if TrackerModel.armosBox.IsDone() then
                ShouldHide(ms.State,i,j), armosLeftSideDarkBmp.Value, [makeCaveItemBesideSword(TrackerModel.armosBox), 20., 2.]
            else
                false, armosLeftSideBrightBmp.Value, [makeCaveItemBesideSword(TrackerModel.armosBox), 20., 2.]
        elif TrackerModel.armosBox.IsDone() then
            ShouldHide(ms.State,i,j), Graphics.theFullTileBmpTable.[ms.State].[1], []
        else
            false, Graphics.theFullTileBmpTable.[ms.State].[0], []
    // MMG always bright
    elif ms.State = TrackerModel.MapSquareChoiceDomainHelper.MONEY_MAKING_GAME then
        ShouldHide(ms.State,i,j), Graphics.theFullTileBmpTable.[ms.State].[0], []
    // take any and sword1 default to being light (accelerators often darken them)
    elif ms.State = TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY then
        if TrackerModel.getOverworldMapExtraData(i,j,ms.State)=TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY then
            match takeAnyTaken.TryGetValue((i,j)) with
            | true, (_, _, kind) when kind <> 0 ->
                // what was taken (heart / candle / potion) beside the icon, with a green check
                let bmp = if kind = 1 then Graphics.owHeartFull_bmp elif kind = 2 then Graphics.blue_candle_bmp else Graphics.red_potion_bmp
                let itemCanvas = makeBmpBesideSword(bmp)
                let ck = caveCheckmark(27.)
                canvasAdd(itemCanvas, ck, 1. + (itemCanvas.Width - 2. - ck.Width)/2., 1. + (27. - ck.Height)/2.)
                itemCanvas.Background <- Brushes.Transparent
                itemCanvas.MouseDown.Add(fun ea -> if ea.ChangedButton = System.Windows.Input.MouseButton.Left then ea.Handled <- true)   // left clicking the item taken from a take any cave does nothing (right click still works)
                ShouldHide(ms.State,i,j), takeAnyLeftSideDarkFullTileBmp, [itemCanvas, 20., 2.]
            | _ ->
                ShouldHide(ms.State,i,j), Graphics.theFullTileBmpTable.[ms.State].[1], []
        else
            false, Graphics.theFullTileBmpTable.[ms.State].[0], []
    elif ms.State = TrackerModel.MapSquareChoiceDomainHelper.SWORD1 && TrackerModel.shuffledWoodSwordTiles.Contains((i,j)) && TrackerModel.sword1Box.IsDone() then
        // the shuffled item was found: the dark sword cave tile with that item to the right of the sword, the same height as the sword icon
        let iconCanvas = makeCaveItemBesideSword(TrackerModel.sword1Box)
        ShouldHide(ms.State,i,j), sword1LeftSideFullTileBmp, [iconCanvas, 20., 2.]   // (the sword icon stays bright)
    elif ms.State = TrackerModel.MapSquareChoiceDomainHelper.SWORD1 && TrackerModel.shuffledWoodSwordTiles.Contains((i,j)) && not(TrackerModel.sword1Box.IsDone()) then
        false, sword1LeftSideFullTileBmp, [fst(Views.MakeBoxItemWithExtraDecorations(Some(cm), TrackerModel.sword1Box, false, None)), OMTW-30., 1.]
    elif ms.State = TrackerModel.MapSquareChoiceDomainHelper.SWORD1 then
        let ex = TrackerModel.getOverworldMapExtraData(i,j,ms.State)
        if ex = CANDLE_TAKEN then
            // took the candle: styled like a shuffled item - the dark sword cave tile with the candle beside the sword, at the sword's height
            let candleCanvas = makeBmpBesideSword(Graphics.blue_candle_bmp)
            let ck = caveCheckmark(27.)
            canvasAdd(candleCanvas, ck, 1. + (candleCanvas.Width - 2. - ck.Width)/2., 1. + (27. - ck.Height)/2.)
            candleCanvas.Background <- Brushes.Transparent
            candleCanvas.MouseDown.Add(fun ea -> ea.Handled <- true)   // clicking the candle does nothing
            ShouldHide(ms.State,i,j), sword1LeftSideDarkFullTileBmp, [candleCanvas, 20., 2.]
        elif ex = TrackerModel.MapSquareChoiceDomainHelper.SWORD1 then
            ShouldHide(ms.State,i,j), Graphics.theFullTileBmpTable.[ms.State].[0], [takenSwordCheck()]   // (the sword stays bright, with a check)
        else
            false, Graphics.theFullTileBmpTable.[ms.State].[0], []
    // hint shop default to being light, user can darken if bought all, or if was white/magic sword hint they already saw
    // actually, helpful hints are so rarely played, make default dark
    elif ms.State = TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP then
        if TrackerModel.getOverworldMapExtraData(i,j,ms.State)=TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP then
            ShouldHide(ms.State,i,j), hintShopTileBmp(hintShopColorOf(i,j), false), [takenSwordCheck()]   // (stays bright, with a green check)
        else
            false, hintShopTileBmp(hintShopColorOf(i,j), false), []
    elif ms.State = TrackerModel.MapSquareChoiceDomainHelper.SWORD2 then
        let ex = TrackerModel.getOverworldMapExtraData(i,j,ms.State)
        if ex = SHUFFLED_ITEM && not(TrackerModel.sword2Box.IsDone()) then
            let extraDecorationsF(boxPos:Point) =
                let extraDecorations = computeExtraDecorationArrow(sword2x, sword2y, boxPos)
                seq extraDecorations
            false, sword2LeftSideFullTileBmp, [fst(Views.MakeBoxItemWithExtraDecorations(Some(cm), TrackerModel.sword2Box, false, Some extraDecorationsF)), OMTW-30., 1.]
        elif ex = SHUFFLED_ITEM then
            ShouldHide(ms.State,i,j), sword2LeftSideFullTileBmp, [makeCaveItemBesideSword(TrackerModel.sword2Box), 20., 2.]
        elif ex = ms.State || TrackerModel.sword2Box.IsDone() then
            ShouldHide(ms.State,i,j), Graphics.theFullTileBmpTable.[ms.State].[(if ex = ms.State then 0 else 1)], (if ex = ms.State then [takenSwordCheck()] else [])
        else
            false, Graphics.theFullTileBmpTable.[ms.State].[0], []
    elif ms.State = TrackerModel.MapSquareChoiceDomainHelper.SWORD3 then
        let ex = TrackerModel.getOverworldMapExtraData(i,j,ms.State)
        if ex = SHUFFLED_ITEM && not(TrackerModel.sword3Box.IsDone()) then
            let extraDecorationsF(boxPos:Point) =
                let extraDecorations = computeExtraDecorationArrow(sword3x, sword3y, boxPos)
                seq extraDecorations
            false, sword3LeftSideFullTileBmp, [fst(Views.MakeBoxItemWithExtraDecorations(Some(cm), TrackerModel.sword3Box, false, Some extraDecorationsF)), OMTW-30., 1.]
        elif ex = SHUFFLED_ITEM then
            ShouldHide(ms.State,i,j), sword3LeftSideFullTileBmp, [makeCaveItemBesideSword(TrackerModel.sword3Box), 20., 2.]
        elif ex = ms.State || TrackerModel.sword3Box.IsDone() || TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Value() then
            ShouldHide(ms.State,i,j), Graphics.theFullTileBmpTable.[ms.State].[(if ex = ms.State then 0 else 1)], (if ex = ms.State then [takenSwordCheck()] else [])
        else
            false, Graphics.theFullTileBmpTable.[ms.State].[0], []
    elif ms.IsDungeon then
        let combine(number:System.Drawing.Bitmap, letter:System.Drawing.Bitmap) =
            let fullTileBmp = new System.Drawing.Bitmap(16*3,11*3)
            let TRANS_BG = Graphics.TRANS_BG
            for px = 0 to 16*3-1 do
                for py = 0 to 11*3-1 do
                    if px>=3*3 && px<8*3 && py>=1*3 && py<10*3 then 
                        fullTileBmp.SetPixel(px, py, number.GetPixel(px-3*3, py-1*3))
                    elif px>=7*3 && px<12*3 && py>=1*3 && py<10*3 then // sharing one 'pixel'
                        fullTileBmp.SetPixel(px, py, letter.GetPixel(px-7*3, py-1*3))  
                    else
                        fullTileBmp.SetPixel(px, py, TRANS_BG)
            fullTileBmp
        let isDarker = TrackerModel.GetDungeon(ms.State).IsComplete
        if TrackerModel.IsHiddenDungeonNumbers() then 
            if TrackerModel.GetDungeon(ms.State).LabelChar <> '?' then
                if isDarker then
                    let letter = Graphics.theInteriorBmpTable.[ms.State].[3]
                    let number = Graphics.theInteriorBmpTable.[int(TrackerModel.GetDungeon(ms.State).LabelChar) - int('1')].[1]
                    false, combine(number,letter), []
                else
                    let letter = Graphics.theInteriorBmpTable.[ms.State].[2]
                    let number = Graphics.theInteriorBmpTable.[int(TrackerModel.GetDungeon(ms.State).LabelChar) - int('1')].[0]
                    false, combine(number,letter), []
            else
                if isDarker then
                    false, Graphics.theFullTileBmpTable.[ms.State].[3], []
                else
                    false, Graphics.theFullTileBmpTable.[ms.State].[2], []
        else 
            if isDarker then
                false, Graphics.theFullTileBmpTable.[ms.State].[1], []
            else
                false, Graphics.theFullTileBmpTable.[ms.State].[0], []
    else
        // Return just the tile bitmap; item icon overlays for marked tiles (state >= 0) are intentionally
        // NOT shown here — they only appear on unmarked (state=-1) tiles or dark-X tiles, which are
        // handled by the state=-1 branch above and the redrawGridSpot dark-X path respectively.
        // Composing here caused the popup hover callback to show a spurious "letter icon" in the tile
        // preview whenever a tile with leftover shop data was hovered over PP/WARP tiles in the selector.
        false, Graphics.theFullTileBmpTable.[ms.State].[0], []

let toggleables = [| 
    TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY
    TrackerModel.MapSquareChoiceDomainHelper.SWORD1
    TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP
    TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET
    TrackerModel.MapSquareChoiceDomainHelper.MEDIUM_SECRET
    TrackerModel.MapSquareChoiceDomainHelper.SMALL_SECRET
    |]   // (the letter cave is no longer a simple toggle: see its accelerator)
let ToggleOverworldTileIfItIsToggleable(i, j, state) =
    if toggleables |> Array.contains state then
        // left click to toggle it 'used'
        let ex = TrackerModel.getOverworldMapExtraData(i,j,state)
        if state=TrackerModel.MapSquareChoiceDomainHelper.SWORD1 && ex=CANDLE_TAKEN then
            ()   // the candle was taken here: left click does nothing (clear the tile to take it back)
        elif ex<>0 && ex=state then
            TrackerModel.setOverworldMapExtraData(i,j,state,0)
        else
            TrackerModel.setOverworldMapExtraData(i,j,state,state)

let mutable goToDungeon = fun (_idx:int) -> ()

let DoLeftClick(cm:CustomComboBoxes.CanvasManager,msp:MapStateProxy,i,j,pos:Point) = async { // returns tuple of two booleans (needRedrawGridSpot, needHighlightTileHide)
    if msp.State = -1 then
        // left click empty tile changes to 'X'
        TrackerModel.overworldMapMarks.[i,j].Set(TrackerModel.MapSquareChoiceDomainHelper.DARK_X)
        return true, true
    elif msp.IsDungeon then
        goToDungeon(msp.State)
        return false, false
    elif msp.IsThreeItemShop then
        popupIsActive <- true
        let item1 = msp.State - TrackerModel.MapSquareChoiceDomainHelper.ARROW  // 0-based
        let item2_1b = TrackerModel.getShopItem2_1based(i,j)
        let item3_1b = TrackerModel.getShopItem3_1based(i,j)
        let item2 = item2_1b - 1  // 0-based, -1=none
        let item3 = item3_1b - 1  // 0-based, -1=none
        let ST = CustomComboBoxes.borderThickness
        // If item2 not set, pick item2; else pick item3
        let choosingItem3 = item2 >= 0
        let currentChoice = if choosingItem3 then item3 else item2
        let promptText = if choosingItem3 then "Choose 3rd shop item" else "Choose 2nd shop item"
        let tileImage() =
            if choosingItem3 then
                makeTwoItemShopBmp(item1, item2)
            else
                Graphics.theFullTileBmpTable.[msp.State].[0]
        let tileCanvas = new Canvas(Width=OMTW, Height=11.*3.)
        canvasAdd(tileCanvas, Graphics.BMPtoImage(tileImage()), 0., 0.)
        let originalStateIndex = max 0 currentChoice
        let gridxPosition =
            if pos.X < OMTW*2. then
                -ST
            elif pos.X > OMTW*13. then
                OMTW - float(8*(5*3+2*int ST)+int ST)
            else
                (OMTW - float(8*(5*3+2*int ST)+int ST))/2.
        let gridElementsSelectablesAndIDs : (FrameworkElement*bool*int)[] = Array.init 8 (fun n ->
            let i = n + TrackerModel.MapSquareChoiceDomainHelper.ARROW
            upcast Graphics.BMPtoImage(MapStateProxy(i).DefaultInteriorBmp()), true, n
            )
        let edTB1 = new TextBox(IsHitTestVisible=false, BorderThickness=Thickness(0.), FontSize=16., Margin=Thickness(2.),
                                    VerticalContentAlignment=VerticalAlignment.Center, HorizontalContentAlignment=HorizontalAlignment.Center,
                                    Text=promptText,
                                    Foreground=Brushes.Orange, Background=Brushes.Black)
        let edBorder = new Border(BorderThickness=Thickness(3.), BorderBrush=Brushes.Gray, Background=Brushes.Black, Child=edTB1)
        let extraDecorations = [(upcast edBorder : FrameworkElement), gridxPosition, -33.]
        let! g = CustomComboBoxes.DoModalGridSelect(cm, pos.X, pos.Y, tileCanvas,
                        gridElementsSelectablesAndIDs, originalStateIndex, 0, (8, 1, 5*3, 9*3), float(5*3)/2., float(9*3)/2., gridxPosition, 11.*3.+ST,
                        (fun (currentState) ->
                            tileCanvas.Children.Clear()
                            let preview =
                                if choosingItem3 then makeThreeItemShopBmp(item1, item2, currentState)
                                else makeTwoItemShopBmp(item1, currentState)
                            canvasAdd(tileCanvas, Graphics.BMPtoImage(preview), 0., 0.)
                            ),
                        (fun (_ea, currentState) -> CustomComboBoxes.DismissPopupWithResult(currentState)),
                        extraDecorations, CustomComboBoxes.ModalGridSelectBrushes.Defaults(), CustomComboBoxes.WarpToCenter, None, "SecondShopItem", None)
        let r =
            match g with
            | Some(currentState) ->
                if choosingItem3 then
                    TrackerModel.setShopItems(i, j, item2_1b, currentState+1)
                else
                    TrackerModel.setShopItems(i, j, currentState+1, 0)
                TrackerModel.mapLastChangedTime.SetNow()
                true, (currentChoice = -1 && currentState >= 0)
            | None -> false, false
        popupIsActive <- false
        return r
    else
        if toggleables |> Array.contains msp.State then
            // left click to toggle it 'used'
            ToggleOverworldTileIfItIsToggleable(i, j, msp.State)
            return true, false
        else
            return false, false
    }

/// Shows the shop item icon picker when right-clicking an overworld tile that has no normal
/// right-click context menu (i.e. AlwaysEmpty tiles). Returns true if a redraw is needed.
let DoNonShopItemIconPopup(cm:CustomComboBoxes.CanvasManager, msp:MapStateProxy, i, j, pos:Point) = async {
    let item2_1b = TrackerModel.getShopItem2_1based(i,j)
    let item2 = item2_1b - 1  // 0-based, -1=none
    let ST = CustomComboBoxes.borderThickness
    let tileCanvas = new Canvas(Width=OMTW, Height=11.*3.)
    if msp.State >= 0 then
        canvasAdd(tileCanvas, Graphics.BMPtoImage(Graphics.theFullTileBmpTable.[msp.State].[0]), 0., 0.)
    let originalStateIndex = max 0 item2
    let gridxPosition =
        if pos.X < OMTW*2. then
            -ST
        elif pos.X > OMTW*13. then
            OMTW - float(8*(5*3+2*int ST)+int ST)
        else
            (OMTW - float(8*(5*3+2*int ST)+int ST))/2.
    let gridElementsSelectablesAndIDs : (FrameworkElement*bool*int)[] = Array.init 8 (fun n ->
        let idx = n + TrackerModel.MapSquareChoiceDomainHelper.ARROW
        upcast Graphics.BMPtoImage(MapStateProxy(idx).DefaultInteriorBmp()), true, n
        )
    let edTB1 = new TextBox(IsHitTestVisible=false, BorderThickness=Thickness(0.), FontSize=16., Margin=Thickness(2.),
                                VerticalContentAlignment=VerticalAlignment.Center, HorizontalContentAlignment=HorizontalAlignment.Center,
                                Text="Mark item icon",
                                Foreground=Brushes.Orange, Background=Brushes.Black)
    let edBorder = new Border(BorderThickness=Thickness(3.), BorderBrush=Brushes.Gray, Background=Brushes.Black, Child=edTB1)
    let extraDecorations = [(upcast edBorder : FrameworkElement), gridxPosition, -33.]
    let! g = CustomComboBoxes.DoModalGridSelect(cm, pos.X, pos.Y, tileCanvas,
                    gridElementsSelectablesAndIDs, originalStateIndex, 0, (8, 1, 5*3, 9*3), float(5*3)/2., float(9*3)/2., gridxPosition, 11.*3.+ST,
                    (fun (_currentState) -> ()),
                    (fun (ea:System.Windows.Input.MouseButtonEventArgs, currentState) ->
                        if ea.ChangedButton = System.Windows.Input.MouseButton.Right then
                            CustomComboBoxes.DismissPopupWithResult(-1)  // right-click = clear icon
                        else
                            CustomComboBoxes.DismissPopupWithResult(currentState)),
                    extraDecorations, CustomComboBoxes.ModalGridSelectBrushes.Defaults(), CustomComboBoxes.WarpToCenter, None, "NonShopItemIcon", None)
    return
        match g with
        | Some(-1) ->  // right-click = clear stored item icon
            TrackerModel.setShopItems(i, j, 0, 0)
            TrackerModel.mapLastChangedTime.SetNow()
            true
        | Some(currentState) ->
            TrackerModel.setShopItems(i, j, currentState+1, 0)
            TrackerModel.mapLastChangedTime.SetNow()
            true
        | None -> false
    }

///////////////////////////////////////////////////

let DoSpecialHotKeyHandlingForOverworldTiles(i, j, originalState, hotKeyedState) =
    // rather than have many idempotent keys, turn some of them into useful actions
    let orig = MapStateProxy(originalState)
    if orig.IsThreeItemShop then
        let item2_1b = TrackerModel.getShopItem2_1based(i,j)
        let item3_1b = TrackerModel.getShopItem3_1based(i,j)
        let item2 = item2_1b - 1  // 0-based, -1=none
        let item3 = item3_1b - 1  // 0-based, -1=none
        let item2AsState = item2 + TrackerModel.MapSquareChoiceDomainHelper.ARROW
        let item3AsState = item3 + TrackerModel.MapSquareChoiceDomainHelper.ARROW
        if hotKeyedState = originalState then           // pressed same key as left item
            if item2 = -1 then
                -1   // no second item, so turn back to empty tile
            else
                item2AsState   // change first item to second item
        elif MapStateProxy(hotKeyedState).IsThreeItemShop then
            if item2AsState = hotKeyedState then        // pressed same key as item2 → remove item2 and item3
                TrackerModel.setShopItems(i, j, 0, 0)
                originalState
            elif item3 >= 0 && item3AsState = hotKeyedState then  // pressed same key as item3 → remove item3
                TrackerModel.setShopItems(i, j, item2_1b, 0)
                originalState
            elif item2 = -1 then                        // no item2 yet → set as item2
                let v = hotKeyedState - TrackerModel.MapSquareChoiceDomainHelper.ARROW + 1
                TrackerModel.setShopItems(i, j, v, 0)
                originalState
            elif item3 = -1 then                        // item2 set, no item3 → set as item3
                let v = hotKeyedState - TrackerModel.MapSquareChoiceDomainHelper.ARROW + 1
                TrackerModel.setShopItems(i, j, item2_1b, v)
                originalState
            else
                hotKeyedState
        else
            hotKeyedState
    elif hotKeyedState=originalState && toggleables |> Array.contains originalState then
        ToggleOverworldTileIfItIsToggleable(i, j, originalState)
        hotKeyedState
    else
        hotKeyedState

///////////////////////////////////////////////////

// the items placed in numbered shop n as (icon, collected) pairs; set by WPFUI (which knows how to draw the marker icons)
let mutable decorBitmapHook : int -> System.Drawing.Bitmap = fun _ -> null   // marker icon id -> its picture (set from WPFUI)
let mutable shopItemBitmapsHook : int -> (System.Drawing.Bitmap*bool) list = fun _ -> []
let MakeRemainderSummaryDisplayEx(_freeWidth:bool) =   // Spot Summary (freeWidth: the count cells may spread over as many columns as the width allows; otherwise 4 per row)
    let sp = new StackPanel(Orientation=Orientation.Vertical)
    let b x = new Border(Child=x, BorderThickness=Thickness(3.), BorderBrush=Brushes.Black)
    let _HEIGHT = 3. + 27. + 3.

    let OPA = 0.4
    let OPA2 = 0.75

    let header(_txt:string) = ()   // the Spot Summary has no text headings (just the icons)
    let _horizontalRule() =
        sp.Children.Add(new Canvas(Height=3., Background=Brushes.Gray, Margin=Thickness(3.))) |> ignore

    // header("Remaining Locations Summary")
    // horizontalRule()

    // the unique-location icons are spread out by half of the room that would reach the width of the count cells below (4 columns of 76)
    // the unique-location icons are spread out by half of the room that would reach the width of the count cells below
    let uniqueW = ref 0.
    let halveGap(row:System.Windows.Controls.Primitives.UniformGrid) =
        row.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
        let natural = row.DesiredSize.Width
        let before = (natural + 9. * 3.) + (max 0. (4. * 68. - (natural + 9. * 3.))) / 2.   // the width before the borders were thinned
        row.MinWidth <- max natural (before * 0.9)   // another 10% narrower than that
        uniqueW.Value <- row.MinWidth
    // uniques
    header("Unique Locations")

    let row = new System.Windows.Controls.Primitives.UniformGrid(Columns=9, HorizontalAlignment=HorizontalAlignment.Stretch)   // the nine icons are spread out a little (see halveGap)
    for i = 0 to 8 do
        let icon = Graphics.BMPtoImage(MapStateProxy(i).DefaultInteriorBmp())
        if not(TrackerModel.mapSquareChoiceDomain.CanAddUse(i)) then
            icon.Opacity <- OPA
        let bi = new Border(Child=icon, BorderThickness=Thickness(1.5), BorderBrush=Brushes.Black)   // (thinner black border than the count cells, to keep the row narrower)
        bi.HorizontalAlignment <- HorizontalAlignment.Center
        row.Children.Add(bi) |> ignore
    halveGap(row)
    sp.Children.Add(row) |> ignore

    let row = new System.Windows.Controls.Primitives.UniformGrid(Columns=9, HorizontalAlignment=HorizontalAlignment.Stretch)   // the nine icons are spread out a little (see halveGap)
    let allBoxes = TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes()
    let itemCollected(id) = (allBoxes |> Array.exists (fun box -> box.CellCurrent() = id && box.IsDone())) || TrackerModel.mapCollectedCount.[id] > 0
    let letterCollected = itemCollected(TrackerModel.ITEMS.LETTER)
    // sword cave tiles (15 = 1st sword, 14 = 2nd, 13 = 3rd) dim once that sword has been collected
    let swordCollected(tile) =
        match tile with
        | 15 -> itemCollected(TrackerModel.ITEMS.WOODSWORD) || itemCollected(TrackerModel.ITEMS.PROGRESSIVESWORD) || TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword.Value()
        | 14 -> itemCollected(TrackerModel.ITEMS.WHITESWORD) || TrackerModel.startingItemsAndExtras.PlayerHasWhiteSword.Value()
        | 13 -> itemCollected(TrackerModel.ITEMS.MAGICALSWORD) || TrackerModel.startingItemsAndExtras.PlayerHasMagicalSword.Value() || TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Value()
        | _ -> false
    for i in [9;10;11;12;13;14;15;30;31] do
        // the letter uses the same icon as the item menus, and dims once the letter has been collected
        let icon = if i = 30 then Graphics.BMPtoImage(CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.LETTER, None)) else Graphics.BMPtoImage(MapStateProxy(i).DefaultInteriorBmp())
        if not(TrackerModel.mapSquareChoiceDomain.CanAddUse(i)) || (i = 30 && letterCollected) || swordCollected(i) then
            icon.Opacity <- OPA
        let bi = new Border(Child=icon, BorderThickness=Thickness(1.5), BorderBrush=Brushes.Black)   // (thinner black border than the count cells, to keep the row narrower)
        bi.HorizontalAlignment <- HorizontalAlignment.Center
        row.Children.Add(bi) |> ignore
    halveGap(row)
    sp.Children.Add(row) |> ignore

    // secrets (no dividers: just an icon with how many are collected out of the max, three to a row)
    let LARGE, MEDIUM, SMALL, UNKNOWN = TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET, TrackerModel.MapSquareChoiceDomainHelper.MEDIUM_SECRET, 
                                            TrackerModel.MapSquareChoiceDomainHelper.SMALL_SECRET, TrackerModel.MapSquareChoiceDomainHelper.UNKNOWN_SECRET
    let LARGE_BMP, MEDIUM_BMP, SMALL_BMP =
        Graphics.theInteriorBmpTable.[LARGE].[0], Graphics.theInteriorBmpTable.[MEDIUM].[0], Graphics.theInteriorBmpTable.[SMALL].[0]
    // grab at most 14 that the user marked
    let mutable userLarge,userMedium,userSmall,userUnknown,userTotal = 0,0,0,0,0
    for i = 0 to TrackerModel.mapSquareChoiceDomain.NumUses(LARGE)-1 do
        if userTotal<14 then
            userLarge <- userLarge + 1
            userTotal <- userTotal + 1
    for i = 0 to TrackerModel.mapSquareChoiceDomain.NumUses(MEDIUM)-1 do
        if userTotal<14 then
            userMedium <- userMedium + 1
            userTotal <- userTotal + 1
    for i = 0 to TrackerModel.mapSquareChoiceDomain.NumUses(SMALL)-1 do
        if userTotal<14 then
            userSmall <- userSmall + 1
            userTotal <- userTotal + 1
    for i = 0 to TrackerModel.mapSquareChoiceDomain.NumUses(UNKNOWN)-1 do
        if userTotal<14 then
            userUnknown <- userUnknown + 1
            userTotal <- userTotal + 1
    // place as many as possible into the 'right' bins
    let dark(bmp) =
        let i = Graphics.BMPtoImage bmp
        i.Opacity <- OPA
        i
    let large,medium,small = if TrackerModel.owInstance.Quest.IsFirstQuestOW then 3,7,4 else 1,7,6
    let userLargeIcons,userMediumIcons,userSmallIcons = ResizeArray(), ResizeArray(), ResizeArray()
    while userLarge > 0 && userLargeIcons.Count < large do
        userLargeIcons.Add(dark <| LARGE_BMP)
        userLarge <- userLarge - 1
    while userMedium > 0 && userMediumIcons.Count < medium do
        userMediumIcons.Add(dark <| MEDIUM_BMP)
        userMedium <- userMedium - 1
    while userSmall > 0 && userSmallIcons.Count < small do
        userSmallIcons.Add(dark <| SMALL_BMP)
        userSmall <- userSmall - 1
    // allocate the remainders into other bins
    while userLarge > 0 do
        if medium - userMediumIcons.Count > small - userSmallIcons.Count then
            userMediumIcons.Insert(0, dark <| LARGE_BMP)
        else
            userSmallIcons.Insert(0, dark <| LARGE_BMP)
        userLarge <- userLarge - 1
    while userMedium > 0 do
        if large - userLargeIcons.Count > small - userSmallIcons.Count then
            userLargeIcons.Add(dark <| MEDIUM_BMP)
        else
            userSmallIcons.Insert(0, dark <| MEDIUM_BMP)
        userMedium <- userMedium - 1
    while userSmall > 0 do
        if large - userLargeIcons.Count > medium - userMediumIcons.Count then
            userLargeIcons.Add(dark <| SMALL_BMP)
        else
            userMediumIcons.Add(dark <| SMALL_BMP)
        userSmall <- userSmall - 1
    let mutable largeUnknown, mediumUnknown, smallUnknown = 0,0,0
    while userUnknown > 0 do
        let largeHoles = large - userLargeIcons.Count 
        let mediumHoles = medium - userMediumIcons.Count
        let smallHoles = small - userSmallIcons.Count
        if largeHoles >= mediumHoles && largeHoles >= smallHoles then
            userLargeIcons.Add(Graphics.BMPtoImage <| MapStateProxy(UNKNOWN).DefaultInteriorBmp())
            largeUnknown <- largeUnknown + 1
        elif mediumHoles >= largeHoles && mediumHoles >= smallHoles then
            userMediumIcons.Add(Graphics.BMPtoImage <| MapStateProxy(UNKNOWN).DefaultInteriorBmp())
            mediumUnknown <- mediumUnknown + 1
        else
            userSmallIcons.Add(Graphics.BMPtoImage <| MapStateProxy(UNKNOWN).DefaultInteriorBmp())
            smallUnknown <- smallUnknown + 1
        userUnknown <- userUnknown - 1

    // one cell: the icon, then 'xN/M' = N collected out of the max M
    let countCell(icon:FrameworkElement, collected:int, mx:int) : FrameworkElement =
        let cell = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center, Margin=Thickness(0.,2.,0.,2.))   // centered in its column
        cell.Children.Add(b icon) |> ignore
        let txt = new TextBlock(Text=(if mx < 0 then sprintf "x%d" collected else sprintf "x%d/%d" collected mx), Foreground=Brushes.Orange, FontSize=12., FontWeight=FontWeights.Bold, MinWidth=30., VerticalAlignment=VerticalAlignment.Center, Margin=Thickness(2.,0.,0.,0.))   // (MinWidth: "x0/9" and "x0/10" take the same room, so 2nd quest's 10 door repairs don't widen the window)
        if mx >= 0 && collected >= mx then txt.Opacity <- OPA2   // all collected: dim it
        cell.Children.Add(txt) |> ignore
        cell :> FrameworkElement
    // all the count cells are collected here and then laid out four to a row (fixed columns, together about as wide as the 9 unique-location icons above)
    let allCells = ResizeArray<FrameworkElement>()
    let potionContentsParts : (Grid * Border * TextBlock) option ref = ref None   // the potion shop contents cell (if any), so flushRows can line it up
    let addRow(cells:FrameworkElement list) = for c in cells do allCells.Add(c)
    let flushRows() =
        // exactly three columns (a Grid, not a wrapping panel), together as wide as the unique-location rows above
        let g = new Grid(HorizontalAlignment=HorizontalAlignment.Stretch)
        for _k = 0 to 2 do g.ColumnDefinitions.Add(new ColumnDefinition(Width=GridLength(1., GridUnitType.Star)))
        let nRows = (allCells.Count + 2) / 3
        for _r = 1 to nRows do g.RowDefinitions.Add(new RowDefinition(Height=GridLength.Auto))
        allCells |> Seq.iteri (fun k c ->
            Grid.SetColumn(c, k % 3)
            Grid.SetRow(c, k / 3)
            g.Children.Add(c) |> ignore)
        // line the potion shop's icon up with the icons in the row above, and put the = halfway between the potion shop count and that icon
        (match potionContentsParts.Value with
         | Some(holder, iconBox, eq) ->
            let k = allCells.IndexOf(holder :> FrameworkElement)
            if k >= 3 then
                let above = allCells.[k-3]
                let potionCell = allCells.[k-1]
                g.LayoutUpdated.Add(fun _ ->
                    try
                        if above.ActualWidth > 0. && potionCell.ActualWidth > 0. && holder.ActualWidth > 0. then
                            let aboveIconLeft = (match above with
                                                 | :? Panel as pn when pn.Children.Count > 0 -> (pn.Children.[0] :?> FrameworkElement).TranslatePoint(Point(0.,0.), g).X
                                                 | _ -> above.TranslatePoint(Point(0.,0.), g).X)
                            let cellLeft = holder.TranslatePoint(Point(0.,0.), g).X - holder.Margin.Left
                            let potionRight = potionCell.TranslatePoint(Point(potionCell.ActualWidth, 0.), g).X
                            let iconLeftWanted = aboveIconLeft - cellLeft
                            eq.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
                            let eqLeft = ((potionRight + aboveIconLeft) / 2. - eq.DesiredSize.Width / 2.) - cellLeft
                            if abs(iconBox.Margin.Left - iconLeftWanted) > 0.5 then iconBox.Margin <- Thickness(iconLeftWanted, 0., 0., 0.)
                            if abs(eq.Margin.Left - eqLeft) > 0.5 then eq.Margin <- Thickness(eqLeft, 0., 0., 0.)
                    with _ -> ())
         | None -> ())
        sp.Children.Add(g) |> ignore
    // row: the three kinds of secrets (the marks you placed count as collected; '?' marks are placed in the emptiest kind)
    addRow [ countCell(Graphics.BMPtoImage(LARGE_BMP), min large userLargeIcons.Count, large)
             countCell(Graphics.BMPtoImage(MEDIUM_BMP), min medium userMediumIcons.Count, medium)
             countCell(Graphics.BMPtoImage(SMALL_BMP), min small userSmallIcons.Count, small) ]
    // the non-unique locations
    let multiCell(multi:int) : FrameworkElement =
        let isPotion = (multi = 34)
        // (potions placed in item boxes do not count towards the potion spots shown here)
        let usedCount =
            if isPotion then
                // a potion spot is used by a tile marked as a potion shop, or by a tile with the potion marker icons on it
                let mutable n = 0
                for ti = 0 to 15 do
                    for tj = 0 to 7 do
                        if TrackerModel.overworldMapMarks.[ti,tj].Current() = 34 || (TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[ti,tj]) |> List.contains TrackerModel.ITEMS.BLUEPOTION) then
                            n <- n + 1
                n
            elif multi = 33 then
                // uncollected hearts: starts at 13; every heart container found and every take-any heart slot used up (a heart, or a candle / potion taken instead) takes one off
                let tookAny = [0..3] |> List.filter (fun h -> TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyHeart(h) <> 0) |> List.length
                max 0 (13 - TrackerModel.mapCollectedCount.[TrackerModel.ITEMS.HEARTCONTAINER] - tookAny)
            else TrackerModel.mapSquareChoiceDomain.NumUses(multi)
        let mx = if multi = 33 then 13 else TrackerModel.mapSquareChoiceDomain.MaxUses(multi)
        // (potion shops are shown as just the blue potion; hint shops show the color the user last chose, grey until then)
        let icon =
            if isPotion then
                let im = Graphics.BMPtoImage(Graphics.blue_potion_bmp)
                im.Width <- 21.
                im.Height <- 27.
                im.Stretch <- Stretch.Uniform
                im
            elif multi = TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP then Graphics.BMPtoImage(hintShopMenuBmp(if hintShopColorChosen || (hintShopColors.Values |> Seq.exists (fun v -> v <> 0)) then lastHintShopColor else 4))
            else Graphics.BMPtoImage(MapStateProxy(multi).DefaultInteriorBmp())
        if multi = 33 then countCell(icon, usedCount, -1)   // the hearts show just the uncollected count, no "/13"
        else countCell(icon, min usedCount mx, mx)
    // row: doors, money making game, and the hint shops right beside it
    addRow [ multiCell 28; multiCell 29; multiCell 32 ]
    // row: take any hearts and the potions next to them
    // (the open space beside them shows what is in the potion shops: just the middle item, the potion; dimmed once it has been collected from every placed potion shop)
    let potionShopContents() : FrameworkElement list =
        // nothing shows until a potion shop has its 3rd icon (the one in the middle, between the blue and red potions); that icon is shown here
        let entries = [ for ti = 0 to 15 do
                          for tj = 0 to 7 do
                              let es = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons.[ti,tj])
                              if es |> List.exists (fun (x,_) -> x = TrackerModel.ITEMS.BLUEPOTION) then
                                  for (ident, collected) in es do
                                      if ident <> TrackerModel.ITEMS.BLUEPOTION then yield (ident, collected) ]
        match entries with
        | [] -> []
        | (ident, collected) :: _ ->
            let bmp = decorBitmapHook ident
            if bmp = null then []
            else
                let img = Graphics.BMPtoImage(bmp)
                img.Stretch <- Stretch.Uniform
                img.Width <- 24.
                img.Height <- 24.
                if collected then img.Opacity <- 0.4
                // "= icon": an equals sign between the potion shop count and the icon that is in the shop
                // the icon lines up with the icons in the rows above, and the = sits exactly halfway between the potion shop count and the icon (both placed after layout, see flushRows)
                let holder = new Grid(HorizontalAlignment=HorizontalAlignment.Left, VerticalAlignment=VerticalAlignment.Center)
                let iconBox = b img
                iconBox.HorizontalAlignment <- HorizontalAlignment.Left
                let eq = new TextBlock(Text="=", Foreground=Brushes.Orange, FontSize=20., FontWeight=FontWeights.Bold, HorizontalAlignment=HorizontalAlignment.Left, VerticalAlignment=VerticalAlignment.Center)
                holder.Children.Add(iconBox) |> ignore
                holder.Children.Add(eq) |> ignore
                potionContentsParts.Value <- Some(holder, iconBox, eq)
                [ holder :> FrameworkElement ]
    addRow ([ multiCell 33; multiCell 34 ] @ potionShopContents())
    // the five numbered shops (each can appear up to 3 times); a tile gets its number from the 'Shop' flyout in the overworld menu
    let shopCell(n:int) : FrameworkElement =
        let im = Graphics.BMPtoImage(shopNumberBmp n)
        let top = countCell(im, TrackerModel.shopLabelCount n, -1)   // shops have no fixed max: just how many tiles are placed
        let items = shopItemBitmapsHook n
        if items.IsEmpty then
            // no items yet: the space under the shop is still kept, so the window doesn't grow when the first item is placed
            let col = new StackPanel(Orientation=Orientation.Vertical, HorizontalAlignment=HorizontalAlignment.Center)
            col.Children.Add(top) |> ignore
            col.Children.Add(new DockPanel(Height=17.)) |> ignore
            col :> FrameworkElement
        else
            // the items placed in this shop, in a small row under its count (collected ones dimmed)
            let col = new StackPanel(Orientation=Orientation.Vertical, HorizontalAlignment=HorizontalAlignment.Center)
            col.Children.Add(top) |> ignore
            let row = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center)
            for (bmp, collected) in items do
                let img = Graphics.BMPtoImage(bmp)
                img.Stretch <- Stretch.Uniform
                img.Width <- 15.
                img.Height <- 15.
                img.Margin <- Thickness(1.)
                if collected then img.Opacity <- 0.4
                row.Children.Add(img) |> ignore
            col.Children.Add(row) |> ignore
            col :> FrameworkElement
    flushRows()
    // the four shops in one row, as wide as the unique-location rows above
    let sg = new Grid(HorizontalAlignment=HorizontalAlignment.Stretch)
    for _k = 0 to 3 do sg.ColumnDefinitions.Add(new ColumnDefinition(Width=GridLength(1., GridUnitType.Star)))
    [1;2;3;4] |> List.iteri (fun k n ->
        let c = shopCell n
        Grid.SetColumn(c, k)
        sg.Children.Add(c) |> ignore)
    sp.Children.Add(sg) |> ignore

    sp.Width <- uniqueW.Value   // every row (the unique locations and everything under them) is exactly the same width, so the window is a clean rectangle
    sp.Margin <- Thickness(3.)
    new Border(Child=sp, BorderThickness=Thickness(3.), BorderBrush=Brushes.Gray, Background=Brushes.Black)

let MakeRemainderSummaryDisplay() = MakeRemainderSummaryDisplayEx(false)

//////////////////////////////////////////////////

let MakeMappedHotKeysDisplay() =
    let mutable total = 0
    let makePanel(states, hkp:HotKeys.HotKeyProcessor<_>, mkIcon:_->FrameworkElement, iconW, header) : Panel =
        let panel = new WrapPanel(MaxHeight=800., Orientation=Orientation.Vertical)
        let bucket = ResizeArray()
        for state in states do
            if hkp.StateToKeys(state).Count > 0 then
                let keys = hkp.StateToKeys(state) |> Seq.fold (fun s c -> s + HotKeys.PrettyKey(c) + ",") "" |> (fun s -> s.Substring(0, s.Length-1))
                let icon = mkIcon(state)
                icon.Width <- float iconW
                let border = new Border(BorderBrush=Brushes.DimGray, BorderThickness=Thickness(1.), Child=icon)
                border.Margin <- Thickness(3.)
                let txt = DungeonRoomState.mkTxt(keys)
                txt.TextAlignment <- TextAlignment.Left
                txt.Margin <- Thickness(0., 0., 9., 0.)  // right margin, so that in two columns, text clearly is attached to the left icon, not the right
                let sp = new StackPanel(Orientation=Orientation.Horizontal)
                sp.Children.Add(border) |> ignore
                sp.Children.Add(txt) |> ignore
                bucket.Add(keys, sp)
                total <- total + 1
        //for _,sp in bucket |> Seq.sortBy fst do   // sort by alphabetical of key
        for _,sp in bucket do            // "logical" order
            panel.Children.Add(sp) |> ignore
        if panel.Children.Count > 0 then
            let dp = new DockPanel()
            let h = DungeonRoomState.mkTxt(header)
            DockPanel.SetDock(h, Dock.Top)
            dp.Children.Add(h) |> ignore
            dp.Children.Add(panel) |> ignore
            dp.Margin <- Thickness(3., 3., 13., 3.)
            upcast dp
        else
            upcast panel
    let bmpElseSize (w, h) bmp =
        let icon : FrameworkElement = if bmp = null then upcast new DockPanel(Width=float w, Height=float h) else upcast Graphics.BMPtoImage bmp
        icon
    let itemPanel = makePanel([-1..14], HotKeys.ItemHotKeyProcessor, (fun item -> CustomComboBoxes.boxCurrentBMP(item, None) |> bmpElseSize(21,21)), 21, "ITEMS")
    let overworldPanel = makePanel([-1..TrackerModel.dummyOverworldTiles.Length-1], HotKeys.OverworldHotKeyProcessor, (fun state ->
        // the shop hotkeys place the new marker icons (the same ones the item menus use), so that is what is shown here too
        let shopBmp : System.Drawing.Bitmap =
            if state = TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP then Graphics.blue_potion_bmp
            elif state = TrackerModel.MapSquareChoiceDomainHelper.ARROW then CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.WOODARROW, None)
            elif state = TrackerModel.MapSquareChoiceDomainHelper.BOMB then Graphics.bomb_bmp
            elif state = TrackerModel.MapSquareChoiceDomainHelper.BOOK then CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.BOOKSHIELD, None)
            elif state = TrackerModel.MapSquareChoiceDomainHelper.BLUE_CANDLE then CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.BLUECANDLE, None)
            elif state = TrackerModel.MapSquareChoiceDomainHelper.BLUE_RING then CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.BLUERING, None)
            elif state = TrackerModel.MapSquareChoiceDomainHelper.MEAT then CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.MEAT, None)
            elif state = TrackerModel.MapSquareChoiceDomainHelper.KEY then CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.OLDKEY, None)
            elif state = TrackerModel.MapSquareChoiceDomainHelper.SHIELD then CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.SHIELD, None)
            else null
        if shopBmp <> null then
            let img = Graphics.BMPtoImage shopBmp
            img.Width <- 15.
            img.Height <- 15.
            img.Stretch <- Stretch.Uniform
            let g = new Grid(Height=27.)
            img.VerticalAlignment <- VerticalAlignment.Center
            g.Children.Add(img) |> ignore
            upcast g
        else
            MapStateProxy(state).DefaultInteriorBmp() |> bmpElseSize(15,27)), 15, "OVERWORLD")
    let blockerPanel = makePanel(TrackerModel.DungeonBlocker.All, HotKeys.BlockerHotKeyProcessor, (fun state -> 
        upcast Graphics.blockerCurrentDisplay(state)), 24, "BLOCKERS")
    let thingies = [| 
        yield! Z1R_Tracker.Models.Z1R_TrackerInterop.RoomTypeExtensions.All() |> Seq.map Choice1Of4
        yield! Z1R_Tracker.Models.Z1R_TrackerInterop.MonsterDetailExtensions.All() |> Seq.map Choice2Of4
        yield! Z1R_Tracker.Models.Z1R_TrackerInterop.FloorDropDetailExtensions.All() |> Seq.map Choice3Of4
        yield! HotKeys.AllDoors |> Seq.map snd |> Seq.map Choice4Of4
    |]
    let dungeonRoomPanel = makePanel(thingies, HotKeys.DungeonRoomHotKeyProcessor, (fun c ->
        match c with 
        | Choice1Of4 rt -> 
            let bmp = Z1R_Tracker.Models.Z1R_TrackerInterop.RoomTypeExtensions.UncompletedBI(rt)
            upcast Graphics.BMPtoImage(bmp)
        | Choice2Of4 md -> 
            let i = md.Bmp() |> bmpElseSize(18,18) 
            i.HorizontalAlignment <- HorizontalAlignment.Left
            i
        | Choice3Of4 fd -> 
            let i = fd.Bmp() |> bmpElseSize(18,18) 
            i.HorizontalAlignment <- HorizontalAlignment.Right
            i
        | Choice4Of4 dr -> 
            let c = 
                match dr.Action with 
                | Z1R_Tracker.Models.Z1R_TrackerInterop.DoorAction.Increment -> Brushes.Green 
                | _ -> Brushes.Purple
            let panel = 
                match dr.Direction with
                | Z1R_Tracker.Models.Z1R_TrackerInterop.DoorDirection.East -> 
                    new DockPanel(Background=c,Width=9.,Height=9.,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center)
                | Z1R_Tracker.Models.Z1R_TrackerInterop.DoorDirection.West -> 
                    new DockPanel(Background=c,Width=9.,Height=9.,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center)
                | Z1R_Tracker.Models.Z1R_TrackerInterop.DoorDirection.North -> 
                    new DockPanel(Background=c,Width=14.,Height=9.,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top)
                | Z1R_Tracker.Models.Z1R_TrackerInterop.DoorDirection.South -> 
                    new DockPanel(Background=c,Width=14.,Height=9.,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom)
                | _ -> // Handle unknown values
                    new DockPanel(Background=Brushes.Red,Width=14.,Height=9.,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center)
            let r = new DockPanel(Width=39., Height=27.)
            r.Children.Add(panel) |> ignore
            upcast r
    ), 39, "DUNGEON")

    let globalPanel = makePanel(HotKeys.GlobalHotkeyTargets.All, HotKeys.GlobalHotKeyProcessor, (fun state -> state.AsHotKeyDisplay()), 30, "GLOBALS")
    let all = new StackPanel(Orientation=Orientation.Horizontal)
    all.Children.Add(itemPanel) |> ignore
    all.Children.Add(overworldPanel) |> ignore
    all.Children.Add(blockerPanel) |> ignore
    all.Children.Add(dungeonRoomPanel) |> ignore
    // Note: contextual keys not displayed here, only on their corresponding menus
    all.Children.Add(globalPanel) |> ignore
    if total = 0 then
        let tb = DungeonRoomState.mkTxt("You have no HotKeys mapped.\nYou can edit HotKeys.txt to add\nsome, to use the next time you\nrestart the app.")
        tb.FontSize <- 16.
        all.Children.Add(tb) |> ignore
        let fileToSelect = HotKeys.HotKeyFilename
        let args = sprintf "/Select, \"%s\"" fileToSelect
        let psi = new System.Diagnostics.ProcessStartInfo("Explorer.exe", args)
        System.Diagnostics.Process.Start(psi) |> ignore
    total=0, new Border(BorderBrush=Brushes.Gray, BorderThickness=Thickness(3.), Background=Brushes.Black, Child=all)

