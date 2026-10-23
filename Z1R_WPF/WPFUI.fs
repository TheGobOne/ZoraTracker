module WPFUI

open System
open System.Windows
open System.Windows.Controls 
open System.Windows.Media

open OverworldMapTileCustomization
open DungeonUI.AhhGlobalVariables
open HotKeys.MyKey
open OverworldItemGridUI
open Reminders
open MainTrackerTriforcesAndItems
open CustomComboBoxes.GlobalFlag

module OW_ITEM_GRID_LOCATIONS = OverworldMapTileCustomization.OW_ITEM_GRID_LOCATIONS

// Icons for the 4th Sword and Progressive Sword overworld markers, in the same pixel-art style as the Wood/White/Magical sword cave icons:
// a black glyph on the brown cave floor, with a colored sword underneath (5x9 logical pixels, each drawn 3x3)
let extraSwordIconBmp(item:int) : System.Drawing.Bitmap =
    let brown = System.Drawing.Color.FromArgb(185,122,87)
    let isProg = (item = TrackerModel.ITEMS.PROGRESSIVESWORD)
    let blade = if isProg then System.Drawing.Color.FromArgb(29,148,65) else System.Drawing.Color.FromArgb(128,128,255)
    let hilt = if isProg then System.Drawing.Color.FromArgb(136,0,21) else System.Drawing.Color.White
    let glyph = if isProg then [| "abbba"; "ababa"; "abbba"; "abaaa"; "abaaa" |] else [| "ababa"; "ababa"; "abbba"; "aaaba"; "aaaba" |]   // 'P' or '4'
    let rows = Array.append (Array.append [| "aaaaa" |] glyph) [| "aaaXa"; "HHHXX"; "aaaXa" |]
    let bmp = new System.Drawing.Bitmap(5*3, 9*3)
    for y = 0 to 8 do
        for x = 0 to 4 do
            let c = 
                match rows.[y].[x] with
                | 'b' -> System.Drawing.Color.Black
                | 'X' -> (if isProg || (x + y) % 2 = 0 then blade else System.Drawing.Color.FromArgb(0,0,168))   // the 4th sword: checkered light / dark blue on the guard side, white on the left
                | 'H' -> hilt
                | _ -> brown
            for dx = 0 to 2 do
                for dy = 0 to 2 do
                    bmp.SetPixel(x*3+dx, y*3+dy, c)
    Graphics.restyleSwordCard bmp

// Decorative marker icon for an overworld tile: the 4th/progressive sword get the cave-style sword icons above; every other dungeon item gets its normal
// item icon with a black border around it (one art-pixel = 3px wide), so it stays visible on top of the busy overworld map.
let private decorIconCache = System.Collections.Generic.Dictionary<int, System.Drawing.Bitmap>()
let CLEAR_ICONS = 2000   // pseudo-id of the red-X menu cell that removes every marker icon from the tile
let ANYROAD_MENU = 2001   // pseudo-result: an Any Road cell was right-clicked (offers to place all four vanilla any roads)
let decorDescription(ident:int) = if ident = TrackerModel.DECOR_BOMB then "Bomb" elif ident = TrackerModel.DECOR_BOMB_DROPPER then "Bomb Dropper" else TrackerModel.ITEMS.AsDisplayDescription(ident)
// once the player has the magic key, the key marker icons on the map are hidden; likewise wood arrow markers once silver arrows are collected (they come back if deselected)
let isKeyDecor(ident:int) = (ident = TrackerModel.ITEMS.KEY || ident = TrackerModel.ITEMS.OLDKEY)
let visibleDecorIcons(packed:int) : int list =
    let hideKeys = TrackerModel.playerComputedStateSummary.HaveAnyKey
    let hideWoodArrows = TrackerModel.playerComputedStateSummary.ArrowLevel >= 2   // silver arrows collected: wood arrow markers are no longer useful
    // an icon that has been checked off as collected always stays visible (greyscale), even when keys / wood arrows are otherwise hidden
    TrackerModel.decorEntriesOf(packed) 
    |> List.filter (fun (ident, collected) -> collected || (not (hideKeys && isKeyDecor ident) && not (hideWoodArrows && ident = TrackerModel.ITEMS.WOODARROW)))
    |> List.map fst
// size and position (within the tile) of marker icon k of n; shared by drawing and by middle-click hit-testing
// a dungeon entrance or an any road tile only gets the bomb dropper marker (no other marker icons)
let isDungeonOrAnyRoad(s:int) = (s >= 0 && s <= 8) || (s >= TrackerModel.MapSquareChoiceDomainHelper.WARP_1 && s <= TrackerModel.MapSquareChoiceDomainHelper.WARP_4)
let SHOP_MENU = 2002   // pseudo-id of the 'Shop' cell in the overworld menu (it opens a flyout: potion shop or shop 1-5)
let SHOP_LABEL_BASE = 3000   // pseudo-results 3001-3005: a numbered shop was picked from the flyout
let private decorSlotNoLabel(big:bool, n:int, k:int) : float*float*float =
    if big then
        match n with
        | 1 -> 27., OMTW/2. - 13.5, 3.
        | 2 -> 24., float k * 24., 4.
        | _ -> 16., float k * 16., 8.
    else
        match n with
        | 1 -> 18., OMTW - 20., 0.
        | _ -> 14., OMTW - 15. * float n + 15. * float k, 0.
// a tile with a "SHOP n" text label keeps the top strip of the tile for the label, so its item icons are drawn in the part below it
let decorSlot(hasLabel:bool, big:bool, n:int, k:int) : float*float*float =
    if not hasLabel then decorSlotNoLabel(big, n, k)
    elif big then
        match n with
        | 1 -> 21., OMTW/2. - 10.5, 11.
        | 2 -> 21., 2. + float k * 23., 11.
        | _ -> 15., 1.5 + float k * 15.5, 14.
    else
        let size, x, y = decorSlotNoLabel(big, n, k)
        size, x, y + 10.
// like decorSlot, but 'centered' (a bomb shop tile whose bomb is one of the slots) puts the row of icons in the middle of the tile instead of the top right
let decorSlotC(hasLabel:bool, centered:bool, big:bool, n:int, k:int) : float*float*float =
    let size, x, y = decorSlot(hasLabel, big, n, k)
    if centered && not big then size, x - (OMTW - 15. * float n) / 2., (33. - size) / 2. else size, x, y
// a potion marker is drawn as just the blue potion; any other marker icons on the tile go beside it.
// Returns (icon to draw, index of the marker it belongs to in the original list)
let decorDisplayList(idents:int list) : (int*int) list =
    let indexed = idents |> List.indexed
    match indexed |> List.tryFind (fun (_, id) -> id = TrackerModel.ITEMS.BLUEPOTION) with
    | None -> indexed |> List.map (fun (k, id) -> id, k)
    | Some(_pk, _) when true -> indexed |> List.map (fun (k, id) -> id, k)   // the blue potion marker is an ordinary icon now
    | Some(pk, _) ->
        let others = indexed |> List.filter (fun (k, _) -> k <> pk) |> List.map (fun (k, id) -> id, k)
        [ (TrackerModel.ITEMS.BLUEPOTION, pk) ] @ others   // (just the blue potion: the red one made the icons too small)
let private decorGreyCache = System.Collections.Generic.Dictionary<int, System.Drawing.Bitmap>()
// the bomb icon with a red 'drops' badge in the corner: a screen whose enemies drop bombs when killed
let bombDropperBmp : System.Drawing.Bitmap =
    let b = new System.Drawing.Bitmap(Graphics.bomb_bmp)
    use g = System.Drawing.Graphics.FromImage(b)
    g.SmoothingMode <- System.Drawing.Drawing2D.SmoothingMode.AntiAlias
    let r = System.Drawing.Rectangle(b.Width - 11, b.Height - 11, 10, 10)
    g.FillEllipse(System.Drawing.Brushes.Black, System.Drawing.Rectangle(r.X-1, r.Y-1, r.Width+2, r.Height+2))
    g.FillEllipse(System.Drawing.Brushes.Red, r)
    use pen = new System.Drawing.Pen(System.Drawing.Color.White, 1.6f)
    g.DrawLine(pen, r.X + 5, r.Y + 2, r.X + 5, r.Y + 7)
    g.DrawLine(pen, r.X + 3, r.Y + 5, r.X + 5, r.Y + 8)
    g.DrawLine(pen, r.X + 7, r.Y + 5, r.X + 5, r.Y + 8)
    b
// the bomb dropper icon with a 1px red outline hugging its shape (transparent everywhere else): the badge drawn in a tile's corner
let bombDropperOutlinedBmp : System.Drawing.Bitmap =
    let src = bombDropperBmp
    let o = new System.Drawing.Bitmap(src.Width + 2, src.Height + 2, System.Drawing.Imaging.PixelFormat.Format32bppArgb)
    for x = 0 to src.Width - 1 do
        for y = 0 to src.Height - 1 do
            if src.GetPixel(x, y).A > 60uy then
                for dx = -1 to 1 do
                    for dy = -1 to 1 do
                        o.SetPixel(x + 1 + dx, y + 1 + dy, System.Drawing.Color.Red)
    for x = 0 to src.Width - 1 do
        for y = 0 to src.Height - 1 do
            let c = src.GetPixel(x, y)
            if c.A > 0uy then
                let a = int c.A
                let blend(fg:int, bg:int) : int = (fg * a + bg * (255 - a)) / 255
                o.SetPixel(x + 1, y + 1, System.Drawing.Color.FromArgb(255, blend(int c.R, 255), blend(int c.G, 0), blend(int c.B, 0)))
    o
// Progressive Items: the picture of a family's tier (swords: the P1..P4 sprites; rings / candles / arrows: the base item, then the upgraded one)
let progressiveFamilyTierBmp(family:int, tier:int) : System.Drawing.Bitmap =
    if family = 1 then Graphics.ow_progressive_tier_item_bmps.[max 1 (min 4 tier)]
    else CustomComboBoxes.boxCurrentBMP(TrackerModel.progressiveFamilyIdent(family, tier), None)
let decorSourceBmp(item:int) : System.Drawing.Bitmap =
    if item = TrackerModel.DECOR_BOMB then Graphics.bomb_bmp
    elif item = TrackerModel.DECOR_BOMB_DROPPER then bombDropperBmp
    elif item = TrackerModel.ITEMS.BLUEPOTION then (if TrackerModelOptions.UseOldPotionLetterIcons.Value then Graphics.old_blue_potion_bmp else Graphics.blue_potion_bmp)
    elif item = TrackerModel.ITEMS.REDPOTION then (if TrackerModelOptions.UseOldPotionLetterIcons.Value then Graphics.old_red_potion_bmp else Graphics.red_potion_bmp)
    elif TrackerModel.ProgressiveItems && TrackerModel.progressiveSwordBoxTier(item) >= 1 && TrackerModel.progressiveSwordBoxTier(item) <= 4 then Graphics.ow_progressive_tier_item_bmps.[TrackerModel.progressiveSwordTier()]   // Progressive Items: swords act like the progressive sword (they all show the current tier, P1..P4)
    elif TrackerModel.progressiveFamily item >= 2 then progressiveFamilyTierBmp(TrackerModel.progressiveFamily item, TrackerModel.progressiveFamilyTier(TrackerModel.progressiveFamily item))   // Progressive Items: rings / candles / arrows show the current tier
    elif item = TrackerModel.ITEMS.WOODSWORD then Graphics.swordLevelToOwBmp(1)   // the same sword icon the item progress bar uses
    elif item = TrackerModel.ITEMS.WHITESWORD then Graphics.swordLevelToOwBmp(2)
    elif item = TrackerModel.ITEMS.MAGICALSWORD then Graphics.swordLevelToOwBmp(3)
    else CustomComboBoxes.boxCurrentBMP(item, None)
// the items placed in numbered shop n (taken from the first tile carrying that shop's label), as (icon, collected)
let shopItemBitmapsGlobal(n:int) : (System.Drawing.Bitmap*bool) list =
    // (index 0 is the P shop, which is shop label 5; it holds just one item)
    let label = if n = 0 then 5 else n
    let src = [ for x in 0..15 do
                  for y in 0..7 do
                      if TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[x,y]) = label && not (TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[x,y]).IsEmpty) then yield (x,y) ]
    if src.IsEmpty then
        // no tile of this shop on the map: whatever was entered in the shop window
        let all = [ for (ident, collected) in TrackerModel.decorEntriesOf(TrackerModel.shopStash.[label]) do
                      let bmp = decorSourceBmp ident
                      if bmp <> null then yield (bmp, collected) ]
        if n = 0 then List.truncate 1 all else all
    else
        let sx, sy = src.Head
        let all = [ for (ident, collected) in TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons.[sx,sy]) do
                      let bmp = decorSourceBmp ident
                      if bmp <> null then yield (bmp, collected) ]
        if n = 0 then List.truncate 1 all else all
// Progressive Items: every sword (and the progressive sword) shows the current tier, so its cached picture depends on the tier
let progressiveSwordCacheOffset(item:int) =
    let f = TrackerModel.progressiveFamily item
    if f = 1 then 10000 + 100 * TrackerModel.progressiveSwordTier()
    elif f >= 2 then 20000 + 100 * TrackerModel.progressiveFamilyTier f
    else 0
let decorIconBmp(item:int) : System.Drawing.Bitmap =
    if (item = TrackerModel.ITEMS.PROGRESSIVESWORD || item = TrackerModel.ITEMS.WHITESWORD4) && not TrackerModel.ProgressiveItems then
        extraSwordIconBmp item
    else
        let cacheKey = item + (if TrackerModelOptions.UseOldPotionLetterIcons.Value then 5000 else 0) + progressiveSwordCacheOffset item   // old/new potion & letter icons are cached separately
        let found, cached = decorIconCache.TryGetValue(cacheKey)
        if found then cached
        else
            let src = decorSourceBmp item
            if src = null then null
            else
                let B = 3
                let w, h = src.Width, src.Height
                let dst = new System.Drawing.Bitmap(w + 2*B, h + 2*B)
                // the old potion/letter icons have a transparent background, so every opaque pixel (even a dark one) gets the black outline
                let isOldIcon = TrackerModelOptions.UseOldPotionLetterIcons.Value && (item = 17)
                let isInk(x, y) = 
                    let p = src.GetPixel(x, y)
                    p.A > 0uy && (isOldIcon || (int p.R + int p.G + int p.B) > 40)
                for x = 0 to w-1 do
                    for y = 0 to h-1 do
                        if isInk(x, y) then
                            for dx = 0 to 2*B do
                                for dy = 0 to 2*B do
                                    dst.SetPixel(x+dx, y+dy, System.Drawing.Color.Black)
                for x = 0 to w-1 do
                    for y = 0 to h-1 do
                        let p = src.GetPixel(x, y)
                        if p.A > 0uy then
                            dst.SetPixel(x+B, y+B, p)
                decorIconCache.[cacheKey] <- dst
                dst

let decorGreyBmp(item:int) : System.Drawing.Bitmap =
    let cacheKey = item + (if TrackerModelOptions.UseOldPotionLetterIcons.Value then 5000 else 0) + progressiveSwordCacheOffset item
    let found, cached = decorGreyCache.TryGetValue(cacheKey)
    if found then cached
    else
        let src = decorIconBmp item
        let g = 
            if src = null then null 
            else
                // greyscale that keeps fully transparent pixels transparent (no black box behind the icon)
                Graphics.transformColor(src, fun c -> 
                    if c.A = 0uy then System.Drawing.Color.FromArgb(0, 0, 0, 0)
                    else
                        let v = min 255 ((int c.R + int c.G + int c.B) / 3 + 40)
                        System.Drawing.Color.FromArgb(int c.A, v, v, v))   // collected items are shown in greyscale
        decorGreyCache.[cacheKey] <- g
        g
// a little green checkmark (dark outline so it reads on any icon) drawn over a marker icon that has been checked off as collected
let decorCheckmark(iconSize:float) : FrameworkElement =
    let s = max 8. (iconSize * 0.55)
    let c = new Canvas(Width=s, Height=s, IsHitTestVisible=false)
    let pts() = new PointCollection([ Point(s*0.08, s*0.55); Point(s*0.38, s*0.88); Point(s*0.94, s*0.12) ])
    c.Children.Add(new System.Windows.Shapes.Polyline(Points=pts(), Stroke=Brushes.Black, StrokeThickness=max 3.5 (s*0.42), StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round, StrokeLineJoin=PenLineJoin.Round)) |> ignore
    c.Children.Add(new System.Windows.Shapes.Polyline(Points=pts(), Stroke=Brushes.LimeGreen, StrokeThickness=max 2. (s*0.24), StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round, StrokeLineJoin=PenLineJoin.Round)) |> ignore
    upcast c
let currentMaxHeartsTextBox = new TextBox(Padding=Thickness(0.), VerticalContentAlignment=VerticalAlignment.Center, HorizontalContentAlignment=HorizontalAlignment.Center, FontSize=16., Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, 
                                            BorderThickness=Thickness(0.), Text=sprintf "Max Hearts: %d" TrackerModel.playerComputedStateSummary.PlayerHearts)
// drawn inside a plain Button (not hit-testable; the wrapper border below handles the mouse) so its border has the same look as the Hint Decoder and the other main-screen buttons
let currentMaxHeartsButton = new Button(Content=currentMaxHeartsTextBox, Width=112., Height=28., HorizontalContentAlignment=HorizontalAlignment.Stretch, VerticalContentAlignment=VerticalAlignment.Stretch, IsHitTestVisible=false, Focusable=false)   // 112x28 overall, so it ends right at the top of the map
Graphics.styleRoundedButton(currentMaxHeartsButton)
// heart containers marked 'collected' on the map count toward the player's max hearts; every change to a tile's icons goes through here
let rebuildMapCollected() =
    System.Array.Clear(TrackerModel.mapCollectedCount, 0, TrackerModel.mapCollectedCount.Length)
    // (with Progressive Items, a shop shown on several tiles is still one shop: its swords / rings / candles / arrows count once, however many tiles carry it)
    let shopCounted = new System.Collections.Generic.HashSet<int*int>()   // (shop label, item)
    for i = 0 to 15 do
        for j = 0 to 7 do
            let label = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[i,j])
            for (ident, col) in TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons.[i,j]) do
                if col && ident >= 0 && ident < TrackerModel.mapCollectedCount.Length then
                    let isRepeat = label > 0 && TrackerModel.progressiveFamily ident > 0 && not (shopCounted.Add((label, ident)))
                    if not isRepeat then TrackerModel.mapCollectedCount.[ident] <- TrackerModel.mapCollectedCount.[ident] + 1
let mutable copiedMarkerIdents : int list = []
let mutable copiedMarkerPacked : int = -1   // same, including each icon's collected state   // shift+left-click on a tile that has only item markers copies them; shift+left-click on an unmarked tile pastes them
let mutable decorStateChanged = fun () -> ()   // set once the main UI exists: refreshes the item progress bar etc.
// potion shops: a tile counts as one if it carries the "P SHOP" label (shop label 5) or is marked as an old-style potion shop; the blue potion marker icon is just a marker and does not count
let tileHasPotionShop(i:int, j:int) =
    TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[i,j]) = 5 || TrackerModel.overworldMapMarks.[i,j].Current() = TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP
let canPlacePotionShop(i:int, j:int) =
    if tileHasPotionShop(i,j) then true
    else
        let mutable n = 0
        for ti = 0 to 15 do
            for tj = 0 to 7 do
                if tileHasPotionShop(ti,tj) then n <- n + 1
        n < TrackerModel.mapSquareChoiceDomain.MaxUses(TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP)
// duplicated markers (copy + paste of a tile's marker icons): source tile -> the tiles it was pasted onto. A duplicate leaves out the items already collected on the original;
// when one of those is later un-collected on the original, it shows up on the duplicates.
let dupLinks = new System.Collections.Generic.Dictionary<(int*int), ResizeArray<(int*int)>>()
let mutable copiedMarkerSource = (-1, -1)
let mutable copiedMarkerBomb = false   // the copied tile was a bomb shop (the paste makes a bomb shop too)
let mutable redrawOwTileHook = fun (_i:int, _j:int) -> ()   // set once the map exists: redraws one overworld tile
// toggling a marker icon on a tile; a P shop (shop label 5) holds just one item, so picking a different one replaces it
let decorToggleForTile(before:int, ident:int) : int =
    let cur = TrackerModel.decorIconsOf before
    if TrackerModel.shopLabelOf before = 5 && not cur.IsEmpty && not (cur |> List.contains ident) then
        TrackerModel.decorIconsToggle(TrackerModel.withShopLabel(-1, 5), ident)
    else TrackerModel.decorIconsToggle(before, ident)
let mutable syncingShops = false
let mutable openShopFlyoutDirectly = false   // set by the Overworld_Shop hotkey: the next tile popup goes straight to the Shop flyout (potion shop / shops 1-4)
// the Dead Woods / Lost Hills hint buttons (set when the legend is built): hovering a tile locked by that hint highlights its button
let mutable hintButtonHighlightHook : int -> bool -> unit = fun _ _ -> ()   // zone (1 = Dead Woods, 2 = Lost Hills), on/off
// 'Progressive Items' (ZORA Extras): the next level of each base item; the UI sets the hook (notification + glow) once it exists
let mutable progressiveUpgradeHook : (int*int) list -> System.Drawing.Bitmap -> System.Drawing.Bitmap -> unit = fun _ _ _ -> ()   // upgraded tiles, from icon, to icon
let mutable progressiveSwordCheckHook : unit -> unit = fun () -> ()   // the progressive sword's tier (P1..P4) may have changed
let mutable progressiveWired = false
let progressiveLastTiers : int[] = Array.create 5 1
let progressivePrevTiers : int[] = Array.create 5 1   // per family: the tier before the latest change
let progressiveChangedAt : System.DateTime[] = Array.create 5 System.DateTime.MinValue
// the tier a family item that was just collected is recorded at: the tier it was showing before it was collected. That is the last tier we saw, unless another handler for this very click
// already moved the tier up (then it is the tier from before that move)
let progressiveCollectedTier(family:int) : int =
    let moved = (System.DateTime.Now - progressiveChangedAt.[family]).TotalMilliseconds < 200.
    max 1 (if moved then progressivePrevTiers.[family] else progressiveLastTiers.[family])   // per family (1 swords, 2 rings, 3 candles, 4 arrows): the tier before the change being processed (what a newly collected item is recorded at)
let rec setDecorIcons(i:int, j:int, newPacked0:int) =
    let oldEntriesForDups = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons.[i,j])
    // the tile's "SHOP n" text label is not an item icon: icon changes keep it (only setShopLabel changes it)
    let oldLabel = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[i,j])
    let newPacked = if oldLabel > 0 && TrackerModel.shopLabelOf(newPacked0) = 0 then TrackerModel.withShopLabel(newPacked0, oldLabel) else newPacked0
    // cave/shop items checked off on the map also check their box in the upper item area (and un-check it again if un-collected)
    let boxLinks = 
        [ TrackerModel.ITEMS.WOODARROW,   TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodArrow
          TrackerModel.ITEMS.BLUERING,    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueRing
          TrackerModel.ITEMS.BLUECANDLE,  TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle
          TrackerModel.ITEMS.WOODSWORD,   TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword ]
    let before = boxLinks |> List.map (fun (id,_) -> TrackerModel.mapCollectedCount.[id] > 0)
    let triforceWasCollectedHere = TrackerModel.decorIsCollected(TrackerModel.owSwordIcons.[i,j], TrackerModel.ITEMS.TRIFORCE)
    let heartsBefore = TrackerModel.mapCollectedCount.[TrackerModel.ITEMS.HEARTCONTAINER]
    let itemsBefore = Array.copy TrackerModel.mapCollectedCount
    TrackerModel.owSwordIcons.[i,j] <- newPacked
    rebuildMapCollected()
    if TrackerModel.ProgressiveItems then
        // remember (or forget) the tier each collected sword was collected at, so its icon keeps showing where the upgrade came from
        let nOf (es:(int*bool) list) id = es |> List.filter (fun (x,c) -> x = id && c) |> List.length
        let newEs = TrackerModel.decorEntriesOf(newPacked)
        for id in [ TrackerModel.ITEMS.PROGRESSIVESWORD; TrackerModel.ITEMS.WOODSWORD; TrackerModel.ITEMS.WHITESWORD; TrackerModel.ITEMS.MAGICALSWORD; TrackerModel.ITEMS.WHITESWORD4
                    TrackerModel.ITEMS.BLUERING; TrackerModel.ITEMS.REDRING; TrackerModel.ITEMS.BLUECANDLE; TrackerModel.ITEMS.REDCANDLE; TrackerModel.ITEMS.WOODARROW; TrackerModel.ITEMS.SILVERARROW ] do
            if nOf newEs id > nOf oldEntriesForDups id then TrackerModel.progressiveTileTier.[(i,j,id)] <- progressiveCollectedTier(TrackerModel.progressiveFamily id)
            elif nOf newEs id < nOf oldEntriesForDups id then TrackerModel.progressiveTileTier.Remove((i,j,id)) |> ignore
        progressiveSwordCheckHook()
    List.iter2 (fun (id, (prop:TrackerModel.BoolProperty)) wasHad ->
        let has = TrackerModel.mapCollectedCount.[id] > 0
        if has <> wasHad then prop.Set(has)) boxLinks before
    // a triforce piece icon checked off on a tile marked as a dungeon checks off that dungeon's triforce (and the triforce chart follows), like a triforce piece in its item box
    let triforceIsCollectedHere = TrackerModel.decorIsCollected(newPacked, TrackerModel.ITEMS.TRIFORCE)
    if triforceIsCollectedHere <> triforceWasCollectedHere then
        let mark = TrackerModel.overworldMapMarks.[i,j].Current()
        let d = mark - TrackerModel.MapSquareChoiceDomainHelper.DUNGEON_1
        if d >= 0 && d <= 7 then
            let dungeon = TrackerModel.GetDungeon(d)
            if dungeon.PlayerHasTriforce() <> triforceIsCollectedHere then dungeon.ToggleTriforce()
    // overworld hearts checked off on the map are stamped on the timeline
    let heartsAfter = TrackerModel.mapCollectedCount.[TrackerModel.ITEMS.HEARTCONTAINER]
    if heartsAfter <> heartsBefore then
        for k = 1 to 12 do
            if (heartsBefore >= k) <> (heartsAfter >= k) then
                match TrackerModel.TimelineItemModel.All.TryGetValue(sprintf "OWHeart%d" k) with
                | true, tim -> tim.SetCollectedFromMap(heartsAfter >= k)
                | _ -> ()
    // the other items (boomerang / boots, bow, ladder, ...) checked off on the map are stamped on the timeline too
    for id = 1 to 13 do
        let hadIt, hasIt = itemsBefore.[id] > 0, TrackerModel.mapCollectedCount.[id] > 0
        if hadIt <> hasIt then
            match TrackerModel.TimelineItemModel.All.TryGetValue(fst TrackerModel.ITEMS.itemNamesAndCounts.[id]) with
            | true, tim -> tim.SetCollectedFromMap(hasIt)
            | _ -> ()
    TrackerModel.recomputePlayerStateSummary()
    currentMaxHeartsTextBox.Text <- sprintf "Max Hearts: %d" TrackerModel.playerComputedStateSummary.PlayerHearts
    decorStateChanged()
    // every tile that carries the same "SHOP n" label is the same shop: collecting, un-collecting, adding or removing an item on one does it on all of them
    let shopLabelHere = TrackerModel.shopLabelOf(newPacked)
    if shopLabelHere > 0 && not syncingShops && TrackerModel.decorEntriesOf(newPacked) <> oldEntriesForDups then
        syncingShops <- true
        try
            let newEntries = TrackerModel.decorEntriesOf(newPacked)
            for x = 0 to 15 do
                for y = 0 to 7 do
                    if (x,y) <> (i,j) && TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[x,y]) = shopLabelHere && TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons.[x,y]) <> newEntries then
                        setDecorIcons(x, y, TrackerModel.withShopLabel(TrackerModel.decorEntriesPack newEntries, shopLabelHere))
                        redrawOwTileHook(x, y)
        finally
            syncingShops <- false
    // an item un-collected on the original shows up on the duplicates
    match dupLinks.TryGetValue((i,j)) with
    | true, dups ->
        let newEntries = TrackerModel.decorEntriesOf(newPacked)
        let collectedCount (entries:(int*bool) list) ident = entries |> List.filter (fun (x,c) -> x = ident && c) |> List.length
        for ident in (oldEntriesForDups |> List.map fst |> List.distinct) do
            if collectedCount newEntries ident < collectedCount oldEntriesForDups ident && (newEntries |> List.exists (fun (x,_) -> x = ident)) then
                for (di,dj) in List.ofSeq dups do
                    let de = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons.[di,dj])
                    let have = de |> List.filter (fun (x,_) -> x = ident) |> List.length
                    let want = newEntries |> List.filter (fun (x,_) -> x = ident) |> List.length
                    if have < want && de.Length < TrackerModel.MaxDecorIcons then
                        let label = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[di,dj])
                        let packed = TrackerModel.decorEntriesPack(de @ [(ident,false)])
                        setDecorIcons(di, dj, (if label > 0 then TrackerModel.withShopLabel(packed, label) else packed))
                        redrawOwTileHook(di, dj)
    | _ -> ()
OverworldMapTileCustomization.setDecorIconsHook <- setDecorIcons
// set (1-5) or remove (0) the tile's "SHOP n" text label
let setShopLabel(i:int, j:int, label:int) =
    let old = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[i,j])
    if old > 0 && label <> old && TrackerModel.shopLabelCount(old) = 1 then
        // this was the last tile of that shop: its items stay in the shop window
        TrackerModel.shopStash.[old] <- TrackerModel.decorEntriesPack(TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons.[i,j]))
    TrackerModel.owSwordIcons.[i,j] <- TrackerModel.withShopLabel(TrackerModel.owSwordIcons.[i,j], label)
    if label > 0 && label <> old && TrackerModel.shopLabelCount(label) = 1 && TrackerModel.shopStash.[label] > 0 then
        // the first tile of a shop picks up the items already entered in its shop window
        TrackerModel.owSwordIcons.[i,j] <- TrackerModel.withShopLabel(TrackerModel.shopStash.[label], label)
        TrackerModel.shopStash.[label] <- -1
    decorStateChanged()
// removes every marker icon AND the shop label from a tile
let clearAllTileIcons(i:int, j:int) =
    setShopLabel(i, j, 0)
    setDecorIcons(i, j, -1)
let owRemainingScreensTextBox = new TextBox(Width=116., Height=18., FontSize=12., Margin=Thickness(0.,-5.,0.,0.), Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, TextAlignment=TextAlignment.Center,
                                            BorderThickness=Thickness(0.), Text="OW Spots Left")
// the gettable/total numbers, twice the size of the label under them
let owRemainingNumbersTextBox = new TextBox(Width=116., Height=34., FontSize=24., Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, TextAlignment=TextAlignment.Center,
                                            VerticalContentAlignment=VerticalAlignment.Center, BorderThickness=Thickness(0.), Text=sprintf "0/%d" TrackerModel.mapStateSummary.OwSpotsRemain)
// per-type counts of the remaining OW spots, each with its icon (burn, bomb, push, ladder, raft, whistle) plus 'open' for open caves
let owSpotTypeCountTBs =
    [| for _ in 0..6 -> new TextBlock(FontSize=14., Foreground=Brushes.Orange, VerticalAlignment=VerticalAlignment.Center, Margin=Thickness(3.,0.,0.,0.), Text="0") |]
let owSpotTypesPanel =
    let wp = new WrapPanel(Width=116., Orientation=Orientation.Horizontal)
    let bmps = [| Graphics.red_candle_bmp; Graphics.bomb_bmp; Graphics.power_bracelet_bmp; Graphics.ladder_bmp; Graphics.raft_bmp; Graphics.recorder_bmp; Graphics.openCaveIconBmp |]
    let tips = [| "Burn"; "Bomb"; "Power Bracelet"; "Ladder"; "Raft"; "Whistle"; "Open" |]
    for k in 0..6 do
        let cell = new Grid(Width=(if k=6 then 116. else 58.), Height=18.)
        let sp = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center, ToolTip=tips.[k])
        sp.Background <- Brushes.Transparent   // so only the icon+number (not the whole cell) gets mouse events
        // the left column (burn, push, raft) is pushed against the center line and the right column (bomb, ladder, whistle) starts at it, each a little inward from the edge
        if k < 6 then
            if k % 2 = 0 then
                sp.HorizontalAlignment <- HorizontalAlignment.Right
                sp.Margin <- Thickness(0.,0.,6.,0.)
            else
                sp.HorizontalAlignment <- HorizontalAlignment.Left
                sp.Margin <- Thickness(6.,0.,0.,0.)
        // hovering a type highlights its unmarked spots on the map, like hovering the matching item in the Item Progress bar
        sp.MouseEnter.Add(fun _ ->
            let inst = TrackerModel.owInstance
            match k with
            | 0 -> showLocatorInstanceFunc(inst.Burnable)
            | 1 -> showLocatorInstanceFunc(inst.Bombable)
            | 2 -> showLocatorInstanceFunc(inst.PowerBraceletable)
            | 3 -> showLocatorInstanceFunc(inst.Ladderable)
            | 4 -> showLocatorInstanceFunc(inst.Raftable)
            | 5 -> showLocatorInstanceFunc(inst.Whistleable)
            | _ -> showLocatorInstanceFunc(inst.Nothingable))
        sp.MouseLeave.Add(fun _ -> hideLocator())
        cell.Children.Add(sp) |> ignore
        let img = Graphics.BMPtoImage bmps.[k]
        img.Width <- 16.
        img.Height <- 16.
        img.Stretch <- Stretch.Uniform
        if k < 6 && k % 2 = 0 then
            // left column: the number first, then the icon (next to the center line)
            owSpotTypeCountTBs.[k].Margin <- Thickness(0.,0.,3.,0.)
            sp.Children.Add(owSpotTypeCountTBs.[k]) |> ignore
            sp.Children.Add(img) |> ignore
        else
            sp.Children.Add(img) |> ignore
            sp.Children.Add(owSpotTypeCountTBs.[k]) |> ignore
        if k=6 then
            sp.Children.Add(new TextBlock(FontSize=14., Foreground=Brushes.Orange, VerticalAlignment=VerticalAlignment.Center, Margin=Thickness(3.,0.,0.,0.), Text="open")) |> ignore
        wp.Children.Add(cell) |> ignore
    wp
let owRemainingScreensTextBoxContainerPanelThatSeesMouseEvents = 
    let dp = new StackPanel(Background=Brushes.Black, Orientation=Orientation.Vertical)
    let inner = new StackPanel(Background=Brushes.Black, Orientation=Orientation.Vertical)
    inner.Children.Add(owRemainingNumbersTextBox) |> ignore
    inner.Children.Add(owRemainingScreensTextBox) |> ignore
    inner.Children.Add(owSpotTypesPanel) |> ignore
    dp.Children.Add(inner) |> ignore
    // Race Mode: the OW spot counts are hidden and this text stands in for them
    // (two lines, centered in the block)
    let raceTB = new Border(Width=116., Height=86., Margin=Thickness(0.,20.,0.,0.), IsHitTestVisible=false,
                            Child=new TextBlock(Text="Race\nMode", FontSize=32., LineHeight=34., LineStackingStrategy=LineStackingStrategy.BlockLineHeight, Foreground=Brushes.Orange, TextAlignment=TextAlignment.Center,
                                                HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center, IsHitTestVisible=false))
    dp.Children.Add(raceTB) |> ignore
    let applyRace() =
        let race = TrackerModelOptions.RaceMode.Value
        inner.Visibility <- (if race then Visibility.Collapsed else Visibility.Visible)
        raceTB.Visibility <- (if race then Visibility.Visible else Visibility.Collapsed)
    applyRace()
    OptionsMenu.raceModeChanged.Publish.Add(fun () -> applyRace())
    dp
let owGettableScreensTextBox = new TextBox(Width=80., Height=20., FontSize=14., Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, 
                                            BorderThickness=Thickness(0.), Text="0 gettable")
let owGettableScreensCheckBox = new CheckBox(Content = owGettableScreensTextBox, IsChecked=TrackerModelOptions.Overworld.Gettables.Value)
let mutable highlightOpenCavesCheckBox : CheckBox = null

type RouteDestination = LinkRouting.RouteDestination

let NoCyan(_i,_j) = false
let drawRoutesToImpl(routeDestinationOption, point, i, j, drawRouteMarks, maxBoldGYR, showPale, whatToCyan) =
    if TrackerModelOptions.RaceMode.Value then
        ()
    else
    let showPale = if owGettableScreensCheckBox.IsChecked.HasValue && owGettableScreensCheckBox.IsChecked.Value then true else showPale
    let unmarked = TrackerModel.overworldMapMarks |> Array2D.map (fun cell -> cell.Current() = -1)
    let interestingButInaccesible = ResizeArray()
    let owTargetworthySpots = Array2D.zeroCreate 16 8
    let processHint(hz:TrackerModel.HintZone,couldBeLetterDungeon) =
        for i = 0 to 15 do
            for j = 0 to 7 do
                if OverworldData.owMapZone.[j].[i] = hz.AsDataChar() then
                    let cur = TrackerModel.overworldMapMarks.[i,j].Current()
                    let cbld = (couldBeLetterDungeon && cur>=0 && cur<=7 && TrackerModel.GetDungeon(cur).LabelChar='?')
                    if cur = -1 || cbld then
                        if TrackerModel.mapStateSummary.OwGettableLocations.[i,j] || cbld then
                            owTargetworthySpots.[i,j] <- true
                            unmarked.[i,j] <- true  // for cbld case
                        else
                            interestingButInaccesible.Add(i,j)
    let mutable lightUpDestinations = false
    OverworldRouteDrawing.specificTargetMode <- (match routeDestinationOption with | Some _ -> true | None -> false)
    match routeDestinationOption with
    | Some(RouteDestination.OW_MAP(spots)) ->
        for x,y in spots do
            owTargetworthySpots.[x,y] <- true
        OverworldRouteDrawing.drawPathsImpl(owTargetworthySpots, unmarked, point, i, j, true, false, 0, false, NoCyan)
        lightUpDestinations <- true
    | Some(RouteDestination.HINTZONE(hz,couldBeLetterDungeon)) ->
        processHint(hz,couldBeLetterDungeon)
        OverworldRouteDrawing.drawPathsImpl(owTargetworthySpots, unmarked, point, i, j, true, false, OverworldRouteDrawing.All, false, NoCyan)
    | Some(RouteDestination.UNMARKEDINSTANCEFUNC(f)) ->
        for x = 0 to 15 do
            for y = 0 to 7 do
                if unmarked.[x,y] && f(x,y) then
                    owTargetworthySpots.[x,y] <- true
        OverworldRouteDrawing.drawPathsImpl(owTargetworthySpots, unmarked, point, i, j, true, false, OverworldRouteDrawing.MaxGYR, true, NoCyan)
    | None ->
        OverworldRouteDrawing.drawPathsImpl(TrackerModel.mapStateSummary.OwRouteworthySpots, unmarked, point, i, j, drawRouteMarks, true, maxBoldGYR, showPale, whatToCyan)
    for x,y in interestingButInaccesible do
        OverworldRouteDrawing.routeDrawingLayer.GetHighlightTile(x,y).MakeRed()
    if lightUpDestinations then
        for x = 0 to 15 do
            for y = 0 to 7 do
                if owTargetworthySpots.[x,y] then
                    OverworldRouteDrawing.routeDrawingLayer.GetHighlightTile(x,y).MakeGreen()

let resetTimerEvent = new Event<unit>()
let mutable currentlyMousedOWX, currentlyMousedOWY = -1, -1

let makeAll(mainWindow:Window, cm:CustomComboBoxes.CanvasManager, drawingCanvas:Canvas, owMapNum, heartShuffle, kind, loadData:DungeonSaveAndLoad.AllData option, 
                showProgress, speechRecognitionInstance:SpeechRecognition.SpeechRecognitionInstance) = async {
    let ctxt = System.Threading.SynchronizationContext.Current
    Popouts.Initialize(mainWindow,ctxt)
    let refocusMainWindow = Popouts.refocusMainWindow
    match loadData with
    | Some(data) ->
        Graphics.alternativeOverworldMapFilename <- data.AlternativeOverworldMapFilename
        Graphics.shouldInitiallyHideOverworldMap <- data.ShouldInitiallyHideOverworldMap
        // rest of data is loaded at end, but these are needed at start
    | _ -> ()
    // initialize based on startup parameters
    let owMapBMPs, isMixed, owInstance, owMapNum, maxOverworldRemain =
        match owMapNum, loadData with
        | 0, _ -> Graphics.overworldMapBMPs(0), false, new OverworldData.OverworldInstance(OverworldData.OWQuest.FIRST), 0, TrackerModel.MaxRemain1Q
        | 1, _ -> Graphics.overworldMapBMPs(1), false, new OverworldData.OverworldInstance(OverworldData.OWQuest.SECOND), 1, TrackerModel.MaxRemain2Q
        | 2, _ -> Graphics.overworldMapBMPs(2), true,  new OverworldData.OverworldInstance(OverworldData.OWQuest.MIXED_FIRST), 2, TrackerModel.MaxRemainMQ
        | 3, _ -> Graphics.overworldMapBMPs(3), true,  new OverworldData.OverworldInstance(OverworldData.OWQuest.MIXED_SECOND), 3, TrackerModel.MaxRemainMQ
        | 4, _ -> Graphics.overworldMapBMPs(4), false,  new OverworldData.OverworldInstance(OverworldData.OWQuest.BLANK), 4, TrackerModel.MaxRemainUQ
        | 999, Some(data) -> 
            match data.Overworld.Quest with
            | 0 -> Graphics.overworldMapBMPs(0), false, new OverworldData.OverworldInstance(OverworldData.OWQuest.FIRST), 0, TrackerModel.MaxRemain1Q
            | 1 -> Graphics.overworldMapBMPs(1), false, new OverworldData.OverworldInstance(OverworldData.OWQuest.SECOND), 1, TrackerModel.MaxRemain2Q
            | 2 -> Graphics.overworldMapBMPs(2), true,  new OverworldData.OverworldInstance(OverworldData.OWQuest.MIXED_FIRST), 2, TrackerModel.MaxRemainMQ
            | 3 -> Graphics.overworldMapBMPs(3), true,  new OverworldData.OverworldInstance(OverworldData.OWQuest.MIXED_SECOND), 3, TrackerModel.MaxRemainMQ
            | 4 -> Graphics.overworldMapBMPs(4), false,  new OverworldData.OverworldInstance(OverworldData.OWQuest.BLANK), 4, TrackerModel.MaxRemainUQ
            | _ -> failwith "bad load data at root.Overworld.Quest"
        | _ -> failwith "bad/unsupported (owMapNum,loadData)"
    let owMapBIs = owMapBMPs |> Array2D.map Graphics.BMPToBI
    let owMapBIsRef = ref owMapBIs   // swapped when the user switches the overworld template (1st/2nd/mixed) from the 1Q/2Q menu
    do! showProgress("after ow map load")
    if owMapNum < 0 || owMapNum > 4 then
        failwith "bad owMapNum"
    let isStandardHyrule = owMapNum <> 4   // should features assume we know the overworld map as standard/mirrored z1r
    let drawRoutesTo(routeDestinationOption, point, i, j, drawRouteMarks, maxBoldGYR, whatToCyan) =
        if isStandardHyrule then 
            drawRoutesToImpl(routeDestinationOption, point, i, j, drawRouteMarks, maxBoldGYR, (owGettableScreensCheckBox.IsChecked.HasValue && owGettableScreensCheckBox.IsChecked.Value), whatToCyan)
        else ()
    TrackerModel.initializeAll(owInstance, kind)
    Z1R_Tracker.Models.RoomSyncBridge.WireRoomChangeEvents()
    CoopSync.subscribeToRoomChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
    CoopSync.subscribeToPlayerProgressChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
    CoopSync.subscribeToStartingItemsAndExtrasChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
    CoopSync.subscribeToItemsChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
    CoopSync.subscribeToOverworldChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
    CoopSync.subscribeToOverworldTileChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
    //CoopSync.subscribeToDungeonChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
    CoopSync.subscribeToDungeonTriforceChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
    CoopSync.subscribeToBlockersChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
    DrawingLayer.applyCustomMenuIcons()
    OverworldMapTileCustomization.shopItemBitmapsHook <- shopItemBitmapsGlobal
    OverworldMapTileCustomization.decorBitmapHook <- decorSourceBmp
    CoopSync.subscribeToDungeonMapsChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
    CoopSync.subscribeToHiddenDungeonColorLabelChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
    //just to test
    CoopSync.subscribeToDoorChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
    



    if not heartShuffle then
        for i = 0 to 7 do
            TrackerModel.GetDungeon(i).Boxes.[0].Set(TrackerModel.ITEMS.HEARTCONTAINER, TrackerModel.PlayerHas.NO)
    let spokenOWTiles = TrackerModel.overworldTiles(owInstance.Quest.IsFirstQuestOW,TrackerModel.IsHiddenDungeonNumbers()) |> Array.map (fun (_,_,s,_) -> s)

    // make the entire UI
    let whetherToCyanOpenCavesOrArmos() = 
        if highlightOpenCavesCheckBox<>null && highlightOpenCavesCheckBox.IsChecked.HasValue && highlightOpenCavesCheckBox.IsChecked.Value then
            // There are 2 reasons to highlight some open caves.
            // First, you might be looking for wood sword cave to obtain free wood sword (or candle)
            if (TrackerModel.playerComputedStateSummary.SwordLevel > 0 && TrackerModel.playerComputedStateSummary.CandleLevel > 0)
                        || TrackerModel.mapStateSummary.Sword1Location <> TrackerModel.NOTFOUND then
                // (highlighting ALL open caves is no longer meaningful, but...)
                if TrackerModel.armosBox.CellCurrent() = -1 then
                    // Second, if your armos box is still empty, highlight the armos
                    (fun (x,y) -> owInstance.HasArmos(x,y) && not(owInstance.HintLocked(x,y)) && TrackerModel.overworldMapMarks.[x,y].Current() = -1)
                else
                    NoCyan
            else
                (fun (x,y) -> owInstance.Nothingable(x,y) && not(owInstance.HintLocked(x,y)) && TrackerModel.overworldMapMarks.[x,y].Current() = -1)
        else
            NoCyan
    let isSpecificRouteTargetActive,currentRouteTarget,eliminateCurrentRouteTarget,changeCurrentRouteTarget =
        let routeTargetLastClickedTime = new TrackerModel.LastChangedTime(TimeSpan.FromMinutes(10.))
        let mutable routeTarget = None
        let isSpecificRouteTargetActive() = DateTime.Now - routeTargetLastClickedTime.Time < TimeSpan.FromSeconds(10.)
        let currentRouteTarget() =
            if isSpecificRouteTargetActive() then
                routeTarget
            else
                None
        let eliminateCurrentRouteTarget() =
            routeTarget <- None
            routeTargetLastClickedTime.SetAgo(TimeSpan.FromMinutes(10.))
        let changeCurrentRouteTarget(newTarget) =
            routeTargetLastClickedTime.SetNow()
            routeTarget <- Some(newTarget)
        isSpecificRouteTargetActive,currentRouteTarget,eliminateCurrentRouteTarget,changeCurrentRouteTarget
    
    let appMainCanvas = cm.AppMainCanvas
    let layout = 
        if TrackerModelOptions.ShorterAppWindow.Value then
            new Layout.ShorterApplicationLayout(cm) :> Layout.IApplicationLayoutBase
        else
            new Layout.ApplicationLayout(cm) :> Layout.IApplicationLayoutBase
    let boxItemImpl, extrasImage, updateNumberedTriforceDisplayImpl, updateNumberedTriforceDisplayIfItExists = 
        MainTrackerTriforcesAndItems.setup(cm, owInstance, layout, kind)

    let owLocatorTileRectangles = Array2D.zeroCreate 16 8
    let redrawOWCircle = ref(fun (_x,_y) -> ())
    let _hideFirstQuestCheckBox, _hideSecondQuestCheckBox, moreFQSQoptionsButton = MakeFQSQStuff(cm, isMixed, owLocatorTileRectangles, redrawOWCircle)
    // (in mixed quest, the Hide 1st/2nd Quest checkboxes now live in the bottom right of the 1Q/2Q popup menu instead of on the main window)

    let mutable toggleBookShieldCheckBox : CheckBox = null
    let mutable bookIsAtlasCheckBox : CheckBox = null
    let MakeManualSave() = SaveAndLoad.SaveAll(notesTextBox.Text, DungeonUI.theDungeonTabControl.SelectedIndex, exportDungeonModelsJsonLines(), DungeonSaveAndLoad.SaveDrawingLayer(), 
                                    Graphics.alternativeOverworldMapFilename, Graphics.shouldInitiallyHideOverworldMap, currentRecorderDestinationIndex, 
                                    toggleBookShieldCheckBox.IsChecked.Value, bookIsAtlasCheckBox.IsChecked.Value, SaveAndLoad.ManualSave)

    let mirrorOW = new Border(Child=Graphics.BMPtoImage Graphics.mirrorOverworldBMP, BorderBrush=Brushes.Gray, BorderThickness=Thickness(1.))
    mirrorOW.MouseEnter.Add(fun _ -> mirrorOW.BorderBrush <- Brushes.DarkGray)
    mirrorOW.MouseLeave.Add(fun _ -> mirrorOW.BorderBrush <- Brushes.Gray)
    mirrorOW.MouseDown.Add(fun _ ->
        TrackerModel.MirrorOverworld <- not TrackerModel.MirrorOverworld
        doUIUpdateEvent.Trigger()
        )
    mirrorOW.ToolTip <- "Toggle mirrored overworld"
    OverworldItemGridUI.setMirroredOverworld <- (fun v -> TrackerModel.MirrorOverworld <- v; doUIUpdateEvent.Trigger())
    ToolTipService.SetPlacement(mirrorOW, System.Windows.Controls.Primitives.PlacementMode.Top)
    let mutable temporarilyHideAllIconsWhileHoveringEyeball = false
    let hideIconsEyeball = new Border(Child=Graphics.BMPtoImage Graphics.eyeballBMP, BorderBrush=Brushes.Gray, BorderThickness=Thickness(1.))
    hideIconsEyeball.MouseEnter.Add(fun _ -> temporarilyHideAllIconsWhileHoveringEyeball <- true; OptionsMenu.requestRedrawOverworldEvent.Trigger())
    hideIconsEyeball.MouseLeave.Add(fun _ -> temporarilyHideAllIconsWhileHoveringEyeball <- false; OptionsMenu.requestRedrawOverworldEvent.Trigger())
    hideIconsEyeball.ToolTip <- "Hover to hide icons\n(peek at unobscured overworld map)"
    ToolTipService.SetPlacement(hideIconsEyeball, System.Windows.Controls.Primitives.PlacementMode.Bottom)
    // the main-screen boxes made by Graphics.makeButton get the same pure-black face as the legend boxes (1Q/2Q, Start Spot, ...)
    let blackenButton(b:obj) =
        match b with
        | :? Button as bt -> (match bt.Content with | :? TextBox as t -> t.Background <- Brushes.Black | _ -> ())
        | _ -> ()
    // hovering the white / magical sword hint boxes shows the hint 3x larger below them, like the dungeon hints
    OverworldItemGridUI.addHintZoomHook <- (fun (fe:FrameworkElement, idx:int, bx:float) ->
        fe.MouseEnter.Add(fun _ ->
            let hz = TrackerModel.GetLevelHint(idx)
            if hz <> TrackerModel.HintZone.UNKNOWN && not popupIsActive then
                let inner = OverworldItemGridUI.HintZoneDisplayTextBox(hz.AsDisplayTwoChars())
                let vb = new Viewbox(Width=72., Height=72., Stretch=Stretch.Uniform, Child=inner)
                let b = new Border(BorderBrush=OverworldItemGridUI.hintyBrush, BorderThickness=Thickness(3.), Background=Brushes.Black, Child=vb, IsHitTestVisible=false)
                layout.ClearTopLayerHovers()
                layout.AddTopLayerHover(b, max 0. (bx - 24.), 34.))
        fe.MouseLeave.Add(fun _ -> layout.ClearTopLayerHovers())
        fe.MouseDown.Add(fun _ -> layout.ClearTopLayerHovers()))
    let white_sword_canvas, mags_canvas, redrawWhiteSwordCanvas, redrawMagicalSwordCanvas, spotSummaryCanvas, invokeExtras,
        owItemGrid, toggleBookShieldCB, bookIsAtlasCB, highlightOpenCavesCB, timerResetButton, spotSummaryTB, ws_ms_bu_toggleButton = 
            MakeItemGrid(cm, boxItemImpl, timelineItems, owInstance, extrasImage, resetTimerEvent, isStandardHyrule, doUIUpdateEvent, MakeManualSave)
    blackenButton(timerResetButton)
    blackenButton(spotSummaryTB)
    toggleBookShieldCheckBox <- toggleBookShieldCB
    bookIsAtlasCheckBox <- bookIsAtlasCB
    highlightOpenCavesCheckBox <- highlightOpenCavesCB
    if isStandardHyrule then
        layout.AddItemGridStuff(owItemGrid, toggleBookShieldCheckBox, bookIsAtlasCheckBox, highlightOpenCavesCheckBox, timerResetButton, spotSummaryTB, mirrorOW, hideIconsEyeball, moreFQSQoptionsButton, ws_ms_bu_toggleButton)
    else
        layout.AddItemGridStuff(owItemGrid, toggleBookShieldCheckBox, bookIsAtlasCheckBox, highlightOpenCavesCheckBox, timerResetButton, spotSummaryTB, mirrorOW, hideIconsEyeball, null, ws_ms_bu_toggleButton)

    do! showProgress("link")

    // overworld map grouping, as main point of support for mirroring
    let mutable animateOverworldTile = fun _ -> ()
    let animateOverworldTileIfOptionIsChecked(i,j) = animateOverworldTile(i,j)  // the option is checked in the body - all OW tile changes should call this
    let mirrorOverworldFEs = ResizeArray<FrameworkElement>()   // overworldCanvas (on which all map is drawn) is here, as well as individual tiny textual/icon elements that need to be re-flipped
    let overworldCanvas = new Canvas(Width=OMTW*16., Height=11.*3.*8.)
    layout.AddOverworldCanvas(overworldCanvas)
    mirrorOverworldFEs.Add(overworldCanvas)

    let blockerQueries = ResizeArray()
    let stepAnimateLink = 
        if isStandardHyrule then   // Routing only works on standard map
            let stepAnimateLink, linkIcon, currentTargetIcon = 
                LinkRouting.SetupLinkRouting(cm, changeCurrentRouteTarget, eliminateCurrentRouteTarget, isSpecificRouteTargetActive, blockerQueries, updateNumberedTriforceDisplayImpl,
                                               (fun() -> displayIsCurrentlyMirrored), MapStateProxy(14).DefaultInteriorBmp(), owInstance, redrawWhiteSwordCanvas, redrawMagicalSwordCanvas)
            layout.AddLinkRouting(linkIcon, currentTargetIcon)
            stepAnimateLink
        else 
            fun () -> ()

    do! showProgress("overworld start start 1")

    layout.AddWebcamLine()
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // ow map opaque fixed bottom layer
    let X_OPACITY = Graphics.X_OPACITY
    let owOpaqueMapGrid = makeGrid(16, 8, int OMTW, 11*3)
    owOpaqueMapGrid.IsHitTestVisible <- false  // do not let this layer see/absorb mouse interactions
    let owOpaqueImages : Image[,] = Array2D.zeroCreate 16 8
    for i = 0 to 15 do
        for j = 0 to 7 do
            let image = Graphics.BItoImage(owMapBIs.[i,j])
            owOpaqueImages.[i,j] <- image
            let c = new Canvas(Width=OMTW, Height=float(11*3))
            canvasAdd(c, image, 0., 0.)
            gridAdd(owOpaqueMapGrid, c, i, j)
            // shading between map tiles
            let OPA = 0.25
            let bottomShade = new Canvas(Width=OMTW, Height=float(3), Background=Brushes.Black, Opacity=OPA)
            canvasAdd(c, bottomShade, 0., float(10*3))
            let rightShade  = new Canvas(Width=float(3), Height=float(11*3), Background=Brushes.Black, Opacity=OPA)
            canvasAdd(c, rightShade, OMTW-3., 0.)
            // permanent icons (only for tiles that are ALWAYS empty in every quest variant, never switchable)
            if owInstance.AlwaysEmpty(i,j) && OverworldData.owMapSquaresSecondQuestOnly.[j].[i] <> 'X' && OverworldData.owMapSquaresFirstQuestOnly.[j].[i] <> 'X' then
                let icon = Graphics.BMPtoImage(Graphics.theFullTileBmpTable.[TrackerModel.MapSquareChoiceDomainHelper.DARK_X].[0]) // "X"
                icon.Opacity <- X_OPACITY
                canvasAdd(c, icon, 0., 0.)
    canvasAdd(overworldCanvas, owOpaqueMapGrid, 0., 0.)

    // layer to place darkening icons - dynamic icons that are below route-drawing but above the fixed base layer
    let owDarkeningMapGrid = makeGrid(16, 8, int OMTW, 11*3)
    let owDarkeningMapGridCanvases = Array2D.zeroCreate 16 8
    owDarkeningMapGrid.IsHitTestVisible <- false  // do not let this layer see/absorb mouse interactions
    for i = 0 to 15 do
        for j = 0 to 7 do
            let c = new Canvas(Width=OMTW, Height=float(11*3))
            gridAdd(owDarkeningMapGrid, c, i, j)
            owDarkeningMapGridCanvases.[i,j] <- c
    canvasAdd(overworldCanvas, owDarkeningMapGrid, 0., 0.)

    // layer to place 'hiding' icons - dynamic darkening icons that are below route-drawing but above the previous layers
    do // HFQ/HSQ (created for every overworld template, since the template can be switched to mixed from the 1Q/2Q menu)
        let owHidingMapGrid = makeGrid(16, 8, int OMTW, 11*3)
        let owHidingMapGridCanvases = Array2D.zeroCreate 16 8
        owHidingMapGrid.IsHitTestVisible <- false  // do not let this layer see/absorb mouse interactions
        for i = 0 to 15 do
            for j = 0 to 7 do
                let c = new Canvas(Width=OMTW, Height=float(11*3))
                gridAdd(owHidingMapGrid, c, i, j)
                owHidingMapGridCanvases.[i,j] <- c
        canvasAdd(overworldCanvas, owHidingMapGrid, 0., 0.)
        let hide(x,y) =
            let hideColor = Brushes.DarkSlateGray
            let hideOpacity = 0.7
            let mark = new Shapes.Ellipse(Width=OMTW-2., Height=float(11*3)-2., Stroke=hideColor, StrokeThickness = 3., Fill=hideColor, Opacity=hideOpacity)
            canvasAdd(owHidingMapGridCanvases.[x,y], mark, 0., 0.)
        hideSecondQuestFromMixed <- 
            (fun unhide ->  // make mixed appear reduced to 1st quest
                for x = 0 to 15 do
                    for y = 0 to 7 do
                        if OverworldData.owMapSquaresSecondQuestOnly.[y].Chars(x) = 'X' then
                            if unhide then
                                owHidingMapGridCanvases.[x,y].Children.Clear()
                            else
                                hide(x,y)
            )
        hideFirstQuestFromMixed <-
            (fun unhide ->   // make mixed appear reduced to 2nd quest
                for x = 0 to 15 do
                    for y = 0 to 7 do
                        if OverworldData.owMapSquaresFirstQuestOnly.[y].Chars(x) = 'X' then
                            if unhide then
                                owHidingMapGridCanvases.[x,y].Children.Clear()
                            else
                                hide(x,y)
            )
    do  // SailNot/NoFeats
        let owHidingMapGrid = makeGrid(16, 8, int OMTW, 11*3)
        let owHidingMapGridCanvases = Array2D.zeroCreate 16 8
        owHidingMapGrid.IsHitTestVisible <- false  // do not let this layer see/absorb mouse interactions
        for i = 0 to 15 do
            for j = 0 to 7 do
                let c = new Canvas(Width=OMTW, Height=float(11*3))
                gridAdd(owHidingMapGrid, c, i, j)
                owHidingMapGridCanvases.[i,j] <- c
        canvasAdd(overworldCanvas, owHidingMapGrid, 0., 0.)
        let _hide(x,y) =
            let hideColor = Brushes.DarkSlateGray
            let hideOpacity = 0.7
            let mark = new Shapes.Ellipse(Width=OMTW-2., Height=float(11*3)-2., Stroke=hideColor, StrokeThickness = 3., Fill=hideColor, Opacity=hideOpacity)
            canvasAdd(owHidingMapGridCanvases.[x,y], mark, 0., 0.)
        // red dashed borders (instead of dimming) for hinted feat of strength / raft / recorder spots
        let dashGrid = makeGrid(16, 8, int OMTW, 11*3)
        dashGrid.IsHitTestVisible <- false
        let dashCanvases = Array2D.zeroCreate 16 8
        for i = 0 to 15 do
            for j = 0 to 7 do
                let c = new Canvas(Width=OMTW, Height=float(11*3))
                gridAdd(dashGrid, c, i, j)
                dashCanvases.[i,j] <- c
        canvasAdd(overworldCanvas, dashGrid, 0., 0.)
        let setDashed(tag:string, pred:int*int->bool, b:bool) =
            for x = 0 to 15 do
                for y = 0 to 7 do
                    let c : Canvas = dashCanvases.[x,y]
                    let existing = c.Children |> Seq.cast<UIElement> |> Seq.filter (fun e -> (e :?> FrameworkElement).Tag = box tag) |> Seq.toList
                    for e in existing do c.Children.Remove(e)
                    if b && pred(x,y) then
                        // same size and thickness as the (cyan) cave highlight rectangle, so the edges line up with it
                        let r = new Shapes.Rectangle(Width=OMTW, Height=float(11*3), Stroke=Brushes.Red, StrokeThickness=4., StrokeDashArray=new DoubleCollection([2.;1.5]), Tag=box tag, IsHitTestVisible=false)
                        canvasAdd(c, r, 0., 0.)
        hideFeatsOfStrength <- (fun b -> setDashed("feats", (fun (x,y) -> owInstance.PowerBraceletable(x,y) || owInstance.GravePushable(x,y)), b))
        hideRaftSpots <- (fun b -> setDashed("raft", (fun (x,y) -> owInstance.Raftable(x,y)), b))

    // ow route drawing layer
    OverworldRouteDrawing.routeDrawingLayer.AddSelfTo(overworldCanvas)

    // middle click overworld circles
    let makeOwCircle(brush) = new Shapes.Ellipse(Width=float(11*3)-2., Height=float(11*3)-2., Stroke=brush, StrokeThickness=3.0, IsHitTestVisible=false)
    let pinkBrush = Graphics.freeze(new SolidColorBrush(Color.FromRgb(0xFFuy, 0x40uy, 0x99uy)))
    let brightBlueBrush = Graphics.freeze(new SolidColorBrush(Color.FromRgb(0x00uy, 0xD0uy, 0xFFuy)))
    let owCircleColor(data) = if data >= 200 then Brushes.Yellow elif data >= 100 then pinkBrush else brightBlueBrush
    let overworldCirclesCanvas = new Canvas(Width=16.*OMTW, Height=float(8*11*3))  
    overworldCirclesCanvas.IsHitTestVisible <- false
    let owCircleRedraws = Array2D.init 16 8 (fun i j -> 
        let c = new Canvas(Width=OMTW, Height=float(11*3))
        mirrorOverworldFEs.Add(c)
        canvasAdd(overworldCirclesCanvas, c, OMTW*float(i), float(j*11*3))
        let redraw() =
            c.Children.Clear()
            let v = TrackerModel.overworldMapCircles.[i,j]
            let brush = owCircleColor(v)
            let v = v % 100
            if v <> 0 then
                canvasAdd(c, makeOwCircle(brush), 11.5*OMTW/48.-3., 0.)
            if (v >= 48 && v <= 57) || (v >= 65 && v <= 90) then
                let tb = new TextBox(Text=sprintf "%c" (char v), FontSize=12., FontWeight=FontWeights.Bold, Foreground=brush, Background=Brushes.Black, 
                                        IsReadOnly=true, IsHitTestVisible=false, BorderThickness=Thickness(0.))
                c.Children.Add(tb) |> ignore
                Canvas.SetLeft(tb, 0.)
                Canvas.SetBottom(tb, 0.)
        redraw)
    redrawOWCircle := fun (x,y) -> owCircleRedraws.[x,y]()

    do! showProgress("overworld magnifier")
    let onMouseForMagnifier, dungeonTabsOverlay, dungeonTabsOverlayContent = UIComponents.MakeMagnifier(mirrorOverworldFEs, owMapNum, owMapBMPs)
    do! showProgress("overworld start 2")
    // ow map -> dungeon tabs interaction
    let selectDungeonTabEvent = new Event<_>()
    // ow map
    let AsyncBrieflyHighlightAnOverworldLocation(loc) = async {
            showLocatorExactLocation loc
            do! Async.Sleep(3000)
            do! Async.SwitchToContext ctxt
            hideLocator()  // this does mean the dungeon location highlight will disappear if we're moused in a dungeon, and routing marks if on overworld
            // ideally, would have a way to respect the mouse position when done, but the code for mouse is all MouseEnters on overworld/dungeon and no easy way to do...
            // ...or is there?
            let p = Input.Mouse.GetPosition(cm.AppMainCanvas)
            Graphics.SilentlyWarpMouseCursorTo(Point(0.,0.))
            Graphics.SilentlyWarpMouseCursorTo(p)
        } 
    let owMapGrid = makeGrid(16, 8, int OMTW, 11*3)
    let owCanvases = Array2D.zeroCreate 16 8
    let owUpdateFunctions = Array2D.create 16 8 (fun _ _ -> ())
    let drawDarkening(c,x,y) =
        let rect = new System.Windows.Shapes.Rectangle(Width=OMTW, Height=float(11*3), Stroke=System.Windows.Media.Brushes.Black, StrokeThickness = 3.,
                                                        Fill=System.Windows.Media.Brushes.Black, Opacity=X_OPACITY)
        canvasAdd(c, rect, x*OMTW, float(y*11*3))
    let ntf = UIHelpers.NotTooFrequently(System.TimeSpan.FromSeconds(0.25))
    let mutable ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes = fun () -> ()
    overworldCanvas.MouseLeave.Add(fun _ -> 
        if not popupIsActive then
            ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes()
        )
    do! showProgress("overworld before 16x8 loop")
    let centerOf(i,j) = overworldCanvas.TranslatePoint(Point(float(i)*OMTW+OMTW/2., float(j*11*3)+float(11*3)/2.), appMainCanvas)
    let owMapDummyImages = Array2D.init 16 8 (fun i j -> 
        let image = Graphics.BItoImage(owMapBIs.[i,j])
        image.Opacity <- 0.001
        image
        )
    // Ctrl+Z undoes the most recent overworld tile marking (made from the tile popup)
    let lastTileChange : (int*int*int) option ref = ref None
    System.Windows.EventManager.RegisterClassHandler(typeof<System.Windows.Window>, System.Windows.Input.Keyboard.PreviewKeyDownEvent,
        System.Windows.Input.KeyEventHandler(fun _ ea ->
            if ea.Key = System.Windows.Input.Key.Z && System.Windows.Input.Keyboard.Modifiers = System.Windows.Input.ModifierKeys.Control
                    && not popupIsActive && not (ea.OriginalSource :? System.Windows.Controls.TextBox) then
                match lastTileChange.Value with
                | Some(ui,uj,orig) ->
                    lastTileChange.Value <- None
                    ea.Handled <- true
                    if TrackerModel.overworldMapMarks.[ui,uj].AttemptToSet(orig) then
                        OptionsMenu.requestRedrawOverworldEvent.Trigger()
                | None -> ()))
    // when the overworld template is switched (e.g. 1st quest -> mixed) from the 1Q/2Q menu, show that template's map art too
    TrackerModel.overworldQuestChanged.Publish.Add(fun _ ->
        let n = TrackerModel.owInstance.Quest.AsInt()
        if n >= 0 && n <= 3 then
            try
                let newBIs = Graphics.overworldMapBMPs(n) |> Array2D.map Graphics.BMPToBI
                owMapBIsRef.Value <- newBIs
                for i = 0 to 15 do
                    for j = 0 to 7 do
                        owOpaqueImages.[i,j].Source <- newBIs.[i,j]
                        owMapDummyImages.[i,j].Source <- newBIs.[i,j]
            with _ -> ()
        )
    // copied item markers follow the mouse cursor until pasted over / cancelled with a right click
    let copyCursor = new Canvas(IsHitTestVisible=false, Width=70., Height=24.)
    let copyCursorAdded = ref false
    let refreshCopyCursor() =
        if not copyCursorAdded.Value then
            copyCursorAdded.Value <- true
            canvasAdd(appMainCanvas, copyCursor, -1000., -1000.)
            appMainCanvas.PreviewMouseMove.Add(fun ea ->
                if not copiedMarkerIdents.IsEmpty then
                    let p = ea.GetPosition(appMainCanvas)
                    Canvas.SetLeft(copyCursor, p.X + 12.)
                    Canvas.SetTop(copyCursor, p.Y + 12.))
            appMainCanvas.PreviewMouseRightButtonDown.Add(fun ea ->
                if not copiedMarkerIdents.IsEmpty then
                    copiedMarkerIdents <- []
                    copyCursor.Children.Clear()
                    Canvas.SetLeft(copyCursor, -1000.)
                    ea.Handled <- true)   // right click cancels the copy instead of doing its usual thing
        copyCursor.Children.Clear()
        if copiedMarkerIdents.IsEmpty then Canvas.SetLeft(copyCursor, -1000.)   // nothing copied (any more): no cursor icons
        else
            TrackerModel.decorEntriesOf(copiedMarkerPacked) |> List.iteri (fun k (ident, col) ->
                let bmp = decorIconBmp ident   // collected items just get a green checkmark
                if bmp <> null then
                    let img = Graphics.BMPtoImage(bmp)
                    img.Stretch <- Stretch.Uniform
                    img.Width <- 22.
                    img.Height <- 22.
                    img.Opacity <- 0.85
                    canvasAdd(copyCursor, img, float k * 23., 0.)
                    if col && ident <> TrackerModel.ITEMS.OLDKEY then
                        let ck = decorCheckmark(22.)
                        canvasAdd(copyCursor, ck, float k * 23. + (22. - ck.Width)/2., (22. - ck.Height)/2.))
            let mp = Input.Mouse.GetPosition(appMainCanvas)
            Canvas.SetLeft(copyCursor, mp.X + 12.)
            Canvas.SetTop(copyCursor, mp.Y + 12.)
    for i = 0 to 15 do
        for j = 0 to 7 do
            let activateCircleLabelPopup() =
                if not popupIsActive then
                    popupIsActive <- true
                    let tb = new TextBox(Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, FontSize=16., TextAlignment=TextAlignment.Center,
                                            Text="Press a key A-Z or 0-9 to label this circle\nOR\nLeft-click to make an un-labeled circle\nOR\nRight-click to remove this circle")
                    let element = new Canvas(Width=appMainCanvas.Width, Height=appMainCanvas.Height, Background=Brushes.Transparent, IsHitTestVisible=true)
                    canvasAdd(element, tb, 250., 10.)
                    let baseColor = if TrackerModel.overworldMapCircles.[i,j] >= 200 then 200 elif TrackerModel.overworldMapCircles.[i,j] >= 100 then 100 else 0
                    let displayCircle = makeOwCircle(owCircleColor(baseColor))
                    canvasAdd(element, displayCircle, float (if displayIsCurrentlyMirrored then (15-i) else i)*OMTW + 8.5*OMTW/48., 150.+float(j*11*3))
                    let wh = new System.Threading.ManualResetEvent(false)
                    element.MouseDown.Add(fun ea ->
                        if ea.ChangedButton = Input.MouseButton.Left then
                            TrackerModel.overworldMapCircles.[i,j] <- baseColor + 1
                            wh.Set() |> ignore
                        elif ea.ChangedButton = Input.MouseButton.Right then
                            TrackerModel.overworldMapCircles.[i,j] <- baseColor + 0
                            wh.Set() |> ignore
                        )
                    element.MyKeyAdd(fun ea ->
                        let key = snd ea.Key
                        if key >= Input.Key.D0 && key <= Input.Key.D9 then
                            TrackerModel.overworldMapCircles.[i,j] <- baseColor + int key - int Input.Key.D0 + int '0'
                            wh.Set() |> ignore
                        elif key >= Input.Key.A && key <= Input.Key.Z then
                            TrackerModel.overworldMapCircles.[i,j] <- baseColor + int key - int Input.Key.A + int 'A'
                            wh.Set() |> ignore
                        )
                    async {
                        do! CustomComboBoxes.DoModalCore(cm, wh, (fun (c,e) -> canvasAdd(c, e, 0., 0.)), (fun (c,e) -> c.Children.Remove(e)), element, 0.7)
                        owCircleRedraws.[i,j]()
                        popupIsActive <- false
                        } |> Async.StartImmediate
            let c = new Canvas(Width=OMTW, Height=float(11*3))
            gridAdd(owMapGrid, c, i, j)
            // we need a dummy image to make the canvas absorb the mouse interactions, so just re-draw the map at almost 0 opacity
            canvasAdd(c, owMapDummyImages.[i,j], 0., 0.)
            // highlight mouse, do mouse-sensitive stuff
            let rect = new System.Windows.Shapes.Rectangle(Width=OMTW-4., Height=float(11*3)-4., Stroke=System.Windows.Media.Brushes.White)
            c.MouseEnter.Add(fun ea ->  canvasAdd(c, rect, 2., 2.)
                                        // draw routes
                                        let mousePos = ea.GetPosition(c)
                                        let mousePos = if displayIsCurrentlyMirrored then Point(OMTW - mousePos.X, mousePos.Y) else mousePos
                                        // hovering an any road highlights all of them, the same style as hovering a blocking item (like the candle) does; otherwise the usual routes
                                        let isAnyRoad(x:int, y:int) =
                                            let m = TrackerModel.overworldMapMarks.[x,y].Current()
                                            m >= TrackerModel.MapSquareChoiceDomainHelper.WARP_1 && m <= TrackerModel.MapSquareChoiceDomainHelper.WARP_4
                                        let hoveringAnyRoad = isAnyRoad(i,j)
                                        ntf.SendThunk(fun () -> 
                                            if hoveringAnyRoad then showLocatorInstanceFuncAll(isAnyRoad)
                                            else
                                                drawRoutesTo(currentRouteTarget(), mousePos, i, j, TrackerModelOptions.Overworld.DrawRoutes.Value, 
                                                                (if TrackerModelOptions.Overworld.HighlightNearby.Value then OverworldRouteDrawing.MaxGYR else 0),
                                                                whetherToCyanOpenCavesOrArmos())
                                            )
                                        if TrackerModelOptions.Overworld.ShowMagnifier.Value then
                                            onMouseForMagnifier(i,j)   // somewhat expensive, only call if it's getting shown
                                        // track current location for F5 & speech recognition purposes
                                        currentlyMousedOWX <- i
                                        currentlyMousedOWY <- j
                                        )
            c.MouseLeave.Add(fun _ -> c.Children.Remove(rect) |> ignore
                                      (let m = TrackerModel.overworldMapMarks.[i,j].Current()
                                       if m >= TrackerModel.MapSquareChoiceDomainHelper.WARP_1 && m <= TrackerModel.MapSquareChoiceDomainHelper.WARP_4 then hideLocator())
                                      dungeonTabsOverlayContent.Children.Clear()
                                      dungeonTabsOverlay.Opacity <- 0.
                                      )
            c.MyKeyAdd(fun ea ->
                match HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key), displayIsCurrentlyMirrored with
                | Some(HotKeys.GlobalHotkeyTargets.MoveCursorRight),false 
                | Some(HotKeys.GlobalHotkeyTargets.MoveCursorLeft),true -> 
                    ea.Handled <- true
                    if i<15 then
                        Graphics.NavigationallyWarpMouseCursorTo(centerOf(i+1,j))
                | Some(HotKeys.GlobalHotkeyTargets.MoveCursorLeft),false
                | Some(HotKeys.GlobalHotkeyTargets.MoveCursorRight),true ->
                    ea.Handled <- true
                    if i>0 then
                        let n = i-1   // without this I seem to encounter a compiler bug?!?
                        Graphics.NavigationallyWarpMouseCursorTo(centerOf(n,j))
                | Some(HotKeys.GlobalHotkeyTargets.MoveCursorDown),_ -> 
                    ea.Handled <- true
                    if j<7 then
                        Graphics.NavigationallyWarpMouseCursorTo(centerOf(i,j+1))
                | Some(HotKeys.GlobalHotkeyTargets.MoveCursorUp),_ -> 
                    ea.Handled <- true
                    if j>0 then
                        Graphics.NavigationallyWarpMouseCursorTo(centerOf(i,j-1))
                | _ -> ()
            )
            // icon
            // Every tile gets the full interactive handler (right-click menu, circles, hotkeys).
            // isPermEmpty marks tiles that are empty in every quest variant; their 'X' comes from the base layer, and they are never hidden in hide-and-reveal mode.
            let isPermEmpty = owInstance.AlwaysEmpty(i,j) && OverworldData.owMapSquaresSecondQuestOnly.[j].[i] <> 'X' && OverworldData.owMapSquaresFirstQuestOnly.[j].[i] <> 'X'
            // fairy icons and the coast-item box live on always-empty tiles; they must be re-added on every redraw since redrawGridSpot clears the canvas
            let permanentDecorations : (UIElement*float*float) list =
                [
                    if isPermEmpty then
                        if i=9 && j=3 || i=3 && j=4 || (owInstance.Quest=OverworldData.OWQuest.SECOND && i=11 && j=0) then // fairy spots
                            yield (Graphics.BMPtoImage Graphics.fairy_bmp :> UIElement), OMTW/2.-8., 1.
                        if i=15 && j=5 then // coast item ladder spot
                            let extraDecorationsF(boxPos:Point) =
                                let lx,ly = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.LADDER_ITEM_BOX)
                                seq(OverworldMapTileCustomization.computeExtraDecorationArrow(lx, ly, boxPos))
                            let coastBoxOnOwGrid = fst(Views.MakeBoxItemWithExtraDecorations(Some(cm), TrackerModel.ladderBox, false, Some extraDecorationsF))
                            // (not re-flipped: its tile canvas is already flipped back, so flipping the box again made the ladder backwards on a mirrored map)
                            doUIUpdateEvent.Publish.Add(fun _ ->
                                if not(TrackerModel.ladderBox.IsDone()) then
                                    coastBoxOnOwGrid.Opacity <- 1.
                                    coastBoxOnOwGrid.IsHitTestVisible <- true
                                else
                                    coastBoxOnOwGrid.Opacity <- 0.
                                    coastBoxOnOwGrid.IsHitTestVisible <- false
                                )
                            yield (coastBoxOnOwGrid :> UIElement), OMTW-31., 1.
                ]
            for fe,x,y in permanentDecorations do
                canvasAdd(c, fe, x, y)
            let bombSlotActive = ref false   // a bomb shop tile that also has marker icons: the bomb is drawn as one of the icon slots instead of big in the middle
            let redrawGridSpot() =
                // cant remove-by-identity because of non-uniques; remake whole canvas
                owDarkeningMapGridCanvases.[i,j].Children.Clear()
                c.Children.Clear()    // deparents owMapDummyImages.[i,j]
                // we need a dummy image to make the canvas absorb the mouse interactions, so just re-draw the map at almost 0 opacity
                canvasAdd(c, owMapDummyImages.[i,j], 0., 0.)
                for fe,x,y in permanentDecorations do
                    canvasAdd(c, fe, x, y)
                let ms = MapStateProxy(TrackerModel.overworldMapMarks.[i,j].Current())
                let shouldAppearLikeDarkX,iconBMP,extraDecorations = GetIconBMPAndExtraDecorations(cm,ms,i,j)
                let bombAsSlot = 
                    iconBMP <> null && obj.ReferenceEquals(iconBMP, Graphics.theFullTileBmpTable.[TrackerModel.MapSquareChoiceDomainHelper.BOMB].[0]) 
                bombSlotActive.Value <- bombAsSlot
                let iconBMP = if bombAsSlot then Graphics.emptyTileBmp else iconBMP
                // be sure to draw in appropriate layer
                if iconBMP <> null then 
                    if temporarilyHideAllIconsWhileHoveringEyeball then
                        let icon = Graphics.BMPtoImage(Graphics.theFullTileBmpTable.[TrackerModel.MapSquareChoiceDomainHelper.DARK_X].[0])
                        icon.Opacity <- X_OPACITY / 3.
                        canvasAdd(owDarkeningMapGridCanvases.[i,j], icon, 0., 0.)  // the icon 'is' the darkening
                    elif ms.IsX || shouldAppearLikeDarkX then
                        if ms.IsX && TrackerModel.getOverworldMapExtraData(i,j,ms.State)=ms.State then
                            // used when Graphics.CanHideAndReveal()
                            let blankTile = Graphics.BMPtoImage Graphics.blankTileBmp
                            canvasAdd(c, blankTile, 0., 0.)
                        elif not (isPermEmpty && ms.State = TrackerModel.MapSquareChoiceDomainHelper.DARK_X) then   // permanently-empty tiles already show their X in the base layer
                            let icon = Graphics.BMPtoImage(Graphics.theFullTileBmpTable.[TrackerModel.MapSquareChoiceDomainHelper.DARK_X].[0])
                            icon.Opacity <- X_OPACITY
                            canvasAdd(owDarkeningMapGridCanvases.[i,j], icon, 0., 0.)  // the icon 'is' the darkening
                        // Even on a dark-X tile, overlay any stored item icon (right-click feature on AlwaysEmpty tiles)
                        if TrackerModelOptions.AllowItemIconOnNonShopTile.Value then
                            let item2 = TrackerModel.getShopItem2_1based(i,j)
                            if item2 <> 0 then
                                let itemIcon = Graphics.BMPtoImage(Graphics.theFullTileBmpTable.[item2 - 1 + TrackerModel.MapSquareChoiceDomainHelper.ARROW].[0])
                                itemIcon.Opacity <- 1.0
                                canvasAdd(c, itemIcon, 0., 0.)
                    else
                        let icon = Graphics.BMPtoImage iconBMP
                        icon.Opacity <- 1.0
                        drawDarkening(owDarkeningMapGridCanvases.[i,j], 0., 0)     // darken below icon and routing marks
                        canvasAdd(c, icon, 0., 0.)                                 // add icon above routing
                        for fe,x,y in extraDecorations do
                            canvasAdd(c, fe, x, y)
                // marker icon (sword / dungeon item) chosen from the menu; drawn on top of the tile
                let decorList = (if bombAsSlot then [TrackerModel.DECOR_BOMB] else []) @ visibleDecorIcons(TrackerModel.owSwordIcons.[i,j]) |> decorDisplayList |> List.map fst
                let shopLabelNow = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[i,j])
                let hasShopLabel = shopLabelNow > 0
                if shopLabelNow = 5 then
                    // the P shop mirrors the flyout: blue potion, the one item (a ? until something is placed), red potion; only the middle slot can change
                    let big = ms.State = -1 || ms.IsX || bombAsSlot
                    let entries = TrackerModel.decorEntriesOf TrackerModel.owSwordIcons.[i,j]
                    let mid = match decorList with | h :: _ -> Some h | [] -> None
                    let midCollected = match mid with | Some h -> (entries |> List.exists (fun (x,col) -> x = h && col)) | None -> false
                    for k in 0..2 do
                        let size, x, y = decorSlotC(true, bombAsSlot, big, 3, k)
                        if k = 1 && mid.IsNone then
                            let q = new TextBlock(Text="?", Foreground=Brushes.White, FontSize=size, FontWeight=FontWeights.Bold, Width=size, Height=size, TextAlignment=TextAlignment.Center, IsHitTestVisible=false)
                            canvasAdd(c, q, x, y - 3.)
                        else
                            let ident = if k = 0 then TrackerModel.ITEMS.BLUEPOTION elif k = 2 then TrackerModel.ITEMS.REDPOTION else mid.Value
                            let dbmp =
                                if (k = 1 && midCollected) && TrackerModel.progressiveFamily ident > 0 then
                                    // a collected sword keeps the tier it was collected at (so you can see where each upgrade came from)
                                    TrackerModel.progressiveTierOverride <- (match TrackerModel.progressiveTileTier.TryGetValue((i,j,ident)) with | true, v -> v | _ -> max 1 (TrackerModel.progressiveFamilyTier(TrackerModel.progressiveFamily ident) - 1))
                                    let b = (try decorIconBmp ident finally TrackerModel.progressiveTierOverride <- -1)
                                    b
                                else decorIconBmp ident
                            if dbmp <> null then
                                let img = Graphics.BMPtoImage(dbmp)
                                img.Stretch <- Stretch.Uniform
                                img.Width <- size
                                img.Height <- size
                                if k = 1 && midCollected && ident = TrackerModel.ITEMS.OLDKEY then img.Opacity <- 0.35
                                canvasAdd(c, img, x, y)
                                if k = 1 && midCollected && ident <> TrackerModel.ITEMS.OLDKEY then
                                    let ck = decorCheckmark(size)
                                    canvasAdd(c, ck, x + (size - ck.Width)/2., y + (size - ck.Height)/2.)
                elif not decorList.IsEmpty then
                    let n = decorList.Length
                    let big = ms.State = -1 || ms.IsX || bombAsSlot   // a bomb shop's bomb and its other icons are drawn full size, like two icons on an unmarked tile
                    let packedNow = TrackerModel.owSwordIcons.[i,j]
                    let allEntries = TrackerModel.decorEntriesOf packedNow
                    decorList |> List.iteri (fun k ident ->
                        // (an icon can repeat, e.g. several heart containers: each copy has its own collected state)
                        let copyNumber = decorList |> List.take k |> List.filter (fun id -> id = ident) |> List.length
                        let sameIdent = allEntries |> List.filter (fun (x,_) -> x = ident)
                        let collected = copyNumber < sameIdent.Length && snd sameIdent.[copyNumber]
                        let justDim = collected && ident = TrackerModel.ITEMS.OLDKEY   // regular key: just dimmed, collects nothing
                        // a single sword icon on an open tile is drawn at the same size as the sword on its cave tile (15x27), not shrunk into the square marker slot
                        let naturalSword =
                            if n = 1 && big && not bombAsSlot && not hasShopLabel && not TrackerModel.ProgressiveItems then   // (on a "SHOP n" tile the label needs the top of the tile, so the sword is scaled down into the slot below it, like the 4th sword)
                                (if ident = TrackerModel.ITEMS.WOODSWORD then Some Graphics.ow_sword1_menu_bmp
                                 elif ident = TrackerModel.ITEMS.WHITESWORD then Some Graphics.ow_sword2_menu_bmp
                                 elif ident = TrackerModel.ITEMS.MAGICALSWORD then Some Graphics.ow_sword3_menu_bmp
                                 else None)
                            else None
                        let dbmp =   // collected items get a little green checkmark (the regular key just dims)
                            if collected && TrackerModel.progressiveFamily ident > 0 then
                                // a collected sword keeps the tier it was collected at (so you can see where each upgrade came from)
                                TrackerModel.progressiveTierOverride <- (match TrackerModel.progressiveTileTier.TryGetValue((i,j,ident)) with | true, v -> v | _ -> max 1 (TrackerModel.progressiveFamilyTier(TrackerModel.progressiveFamily ident) - 1))
                                let b = (try decorIconBmp ident finally TrackerModel.progressiveTierOverride <- -1)
                                b
                            else decorIconBmp ident
                        if naturalSword.IsSome then
                            let sb = naturalSword.Value
                            let img = Graphics.BMPtoImage(if collected then Graphics.greyscale sb else sb)
                            let sx, sy = (OMTW - float sb.Width) / 2., 3.
                            canvasAdd(c, img, sx, sy)
                            if collected then
                                let ck = decorCheckmark(27.)
                                canvasAdd(c, ck, sx + (float sb.Width - ck.Width)/2., sy + (float sb.Height - ck.Height)/2.)
                        elif dbmp <> null then
                            let img = Graphics.BMPtoImage(dbmp)
                            img.Stretch <- Stretch.Uniform
                            // 1 icon: as before; 2-3 icons: smaller, side by side so they all fit on the tile
                            let size, x, y = decorSlotC(hasShopLabel, bombAsSlot, big, n, k)
                            if justDim then img.Opacity <- 0.35
                            img.Width <- size
                            img.Height <- size
                            canvasAdd(c, img, x, y)
                            if Views.itemIsHintDashed ident then canvasAdd(c, Views.makeHintDashedRect(size, size), x, y)
                            if collected && not justDim then
                                let ck = decorCheckmark(size)
                                canvasAdd(c, ck, x + (size - ck.Width)/2., y + (size - ck.Height)/2.))
                // the "SHOP n" text label across the top of the tile (plain text: it is not an item icon and never uses up one of the item slots)
                if hasShopLabel then
                    let labelTB = new TextBlock(Text=(if shopLabelNow = 5 then "P SHOP" else sprintf "SHOP %d" shopLabelNow), Foreground=Brushes.Gold, FontSize=9., FontWeight=FontWeights.Bold, TextAlignment=TextAlignment.Center,
                                                Width=OMTW, Height=11., Background=Brushes.Black, IsHitTestVisible=false)
                    canvasAdd(c, labelTB, 0., 0.)
                // the bomb dropper badge: its own layer in the top-right corner of the tile (never an item slot); the icon carries its own red outline
                if TrackerModel.owBombDroppers.[i,j] then
                    let badge = Graphics.BMPtoImage(bombDropperOutlinedBmp)
                    badge.Stretch <- Stretch.Uniform
                    badge.Width <- 12.
                    badge.Height <- 12.
                    badge.IsHitTestVisible <- false
                    canvasAdd(c, badge, OMTW - 12., 0.)
            Views.hintDashedItemsChanged.Publish.Add(fun _ -> redrawGridSpot())
            // the letter / white sword / magical sword cave once the user has picked 'Took ...' there
            let tookCaveItem(state) =
                (state = TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER || state = TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || state = TrackerModel.MapSquareChoiceDomainHelper.SWORD3)
                && TrackerModel.getOverworldMapExtraData(i,j,state) = state
            let isLegalHere(state) = if state = TrackerModel.MapSquareChoiceDomainHelper.ARMOS then owInstance.HasArmos(i,j) else true
            let updateGridSpot delta phrase = 
                async {
                    // figure out what new state we just interacted-to
                    if delta = 777 then 
                        let curState = TrackerModel.overworldMapMarks.[i,j].Current()
                        if curState = -1 then
                            // if unmarked, use voice to set new state
                            match speechRecognitionInstance.ConvertSpokenPhraseToMapCell(phrase) with
                            | Some newState -> 
                                if isLegalHere(newState) && TrackerModel.overworldMapMarks.[i,j].AttemptToSet(newState) then
                                    if newState >=0 && newState <=8 then
                                        selectDungeonTabEvent.Trigger(newState)
                                    Graphics.PlaySoundForSpeechRecognizedAndUsedToMark()
                                    let pos = c.TranslatePoint(Point(OMTW/2., 11.*3./2.), appMainCanvas)
                                    match overworldAcceleratorTable.TryGetValue(newState) with
                                    | (true,f) -> 
                                        OverworldMapTileCustomization.acceleratorCancelled <- false
                                        do! f(cm,c,i,j)
                                        if OverworldMapTileCustomization.acceleratorCancelled then TrackerModel.overworldMapMarks.[i,j].Set(curState)
                                        Graphics.WarpMouseCursorTo(pos)
                                    | _ -> ()
                                    animateOverworldTileIfOptionIsChecked(i,j)
                            | None -> ()
                        elif MapStateProxy(curState).IsThreeItemShop && TrackerModel.getShopItem2_1based(i,j)=0 then
                            // if item shop with only one item marked, use voice to set item2
                            match speechRecognitionInstance.ConvertSpokenPhraseToMapCell(phrase) with
                            | Some newState ->
                                if TrackerModel.MapSquareChoiceDomainHelper.IsItem(newState) then
                                    TrackerModel.setShopItems(i,j,TrackerModel.MapSquareChoiceDomainHelper.ToItem(newState),0)
                                    Graphics.PlaySoundForSpeechRecognizedAndUsedToMark()
                                    animateOverworldTileIfOptionIsChecked(i,j)
                            | None -> ()
                        elif MapStateProxy(curState).IsThreeItemShop && TrackerModel.getShopItem3_1based(i,j)=0 then
                            // if item shop with two items marked, use voice to set item3
                            match speechRecognitionInstance.ConvertSpokenPhraseToMapCell(phrase) with
                            | Some newState ->
                                if TrackerModel.MapSquareChoiceDomainHelper.IsItem(newState) then
                                    TrackerModel.setShopItems(i,j,TrackerModel.getShopItem2_1based(i,j),TrackerModel.MapSquareChoiceDomainHelper.ToItem(newState))
                                    Graphics.PlaySoundForSpeechRecognizedAndUsedToMark()
                                    animateOverworldTileIfOptionIsChecked(i,j)
                            | None -> ()
                    elif delta = 0 then 
                        ()
                    else failwith "bad delta"
                    redrawGridSpot()
                    } |> Async.StartImmediate
            owUpdateFunctions.[i,j] <- updateGridSpot 
            redrawOwTileHook <- (fun (x,y) -> owUpdateFunctions.[x,y] 0 null)
            owCanvases.[i,j] <- c
            mirrorOverworldFEs.Add(c)
            mirrorOverworldFEs.Add(owDarkeningMapGridCanvases.[i,j])
            let SetNewValue(currentState, originalState) = async {
                // picking the same sword / letter cave again while it holds a shuffled item re-opens its choice box (so the icon, or 'took it', can still be placed; the shuffled item is crossed out)
                let holdsShuffledItem =
                    currentState = originalState &&
                    ((currentState = TrackerModel.MapSquareChoiceDomainHelper.SWORD1 && TrackerModel.shuffledWoodSwordTiles.Contains((i,j)))
                     || ((currentState = TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER || currentState = TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || currentState = TrackerModel.MapSquareChoiceDomainHelper.SWORD3)
                         && TrackerModel.getOverworldMapExtraData(i,j,currentState) = OverworldMapTileCustomization.SHUFFLED_ITEM))
                if holdsShuffledItem then
                    match overworldAcceleratorTable.TryGetValue(currentState) with
                    | (true,f) ->
                        let pos = c.TranslatePoint(Point(OMTW/2., 11.*3./2.), appMainCanvas)
                        OverworldMapTileCustomization.acceleratorCancelled <- false
                        do! f(cm,c,i,j)
                        Graphics.WarpMouseCursorTo(pos)
                        redrawGridSpot()
                    | _ -> ()
                elif currentState <> originalState then   // only do work if they actually changed anything
                    if not(isLegalHere(currentState)) then
                        Graphics.ErrorBeepWithReminderLogText("Tried to mark Armos on an overworld tile other than the 5 possible Armos screens")
                    elif TrackerModel.overworldMapMarks.[i,j].AttemptToSet(currentState) then
                        lastTileChange.Value <- Some(i,j,originalState)
                        if originalState<>TrackerModel.MapSquareChoiceDomainHelper.DARK_X && originalState <> -1 then  
                            // remind destructive changes
                            RemindOverworldOverwrites(i, j, originalState, currentState, spokenOWTiles, AsyncBrieflyHighlightAnOverworldLocation(i,j))
                        if currentState >=0 && currentState <=8 then
                            selectDungeonTabEvent.Trigger(currentState)
                        match overworldAcceleratorTable.TryGetValue(currentState) with
                        | (true,f) -> 
                            let pos = c.TranslatePoint(Point(OMTW/2., 11.*3./2.), appMainCanvas)
                            OverworldMapTileCustomization.acceleratorCancelled <- false
                            do! f(cm,c,i,j)
                            if OverworldMapTileCustomization.acceleratorCancelled then TrackerModel.overworldMapMarks.[i,j].Set(originalState)   // dismissed without choosing: put the tile back
                            Graphics.WarpMouseCursorTo(pos)
                        | _ -> ()
                        redrawGridSpot()
                        if originalState = -1 && currentState <> -1 then OverworldRouteDrawing.routeDrawingLayer.GetHighlightTile(i,j).Hide()  // dismiss any green/yellow highlight on this tile
                        animateOverworldTileIfOptionIsChecked(i,j)
                    else
                        // e.g. they tried to set Level1 when already found elsewhere
                        Graphics.ErrorBeepWithReminderLogText(sprintf "Tried to mark overworld tile %s as %s but that is already fully marked elsewhere" 
                                                                (sprintf "%c%d" (char (int 'A' + j)) (if TrackerModel.MirrorOverworld then 16-i else (i+1)))
                                                                (TrackerModel.MapSquareChoiceDomainHelper.AsHotKeyName(currentState)))
            }
            let GCOL = 9
            let ST = CustomComboBoxes.borderThickness
            let gridWidth = float(GCOL*(7*3+2*int ST)+int ST) * 1.25   // cells are 21 wide so the item icons can be shown at full size; the menu is drawn 1.25x bigger (see gridScale in DoModalGridSelect)
            let tileImage = Graphics.BItoImage(owMapBIsRef.Value.[i,j])
            let rec activatePopup(activationDelta) =
                // Recompute GROW/GCOUNT live on each popup call so they always match
                // gridElementsSelectablesAndIDs, which is also built live from the current option value.
                // Capturing these at tile-init time causes a length mismatch crash if the option
                // is toggled between init and the next right-click.
                popupIsActive <- true
                let tileCanvas = new Canvas(Width=OMTW, Height=11.*3.)   // has to be fresh object for each popup
                let pos = c.TranslatePoint(Point(), appMainCanvas)
                let gridxPosition = 
                    if pos.X < OMTW*2. then 
                        -ST // left align
                    elif pos.X > OMTW*13. then 
                        OMTW - gridWidth  // right align
                    else
                        let r = int((OMTW - gridWidth)/2.)  // center align
                        let r = float(r - r%3)  // ensure at a multiple-of-3 pixel, to look good at 2/3 size
                        // keep the whole menu inside the window (the centered menu used to run off the left edge at column 3 and off the right edge at column 14)
                        let windowW = if appMainCanvas.ActualWidth > 0. then appMainCanvas.ActualWidth else 16.*OMTW
                        let r = min r (windowW - gridWidth - pos.X)
                        max r (-pos.X)
                let originalState = TrackerModel.overworldMapMarks.[i,j].Current()
                let typicalGESAI(n) : FrameworkElement*_*_ =
                    let isSwordTile = (n = TrackerModel.MapSquareChoiceDomainHelper.SWORD1 || n = TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || n = TrackerModel.MapSquareChoiceDomainHelper.SWORD3)
                    // a sword cave that is already placed elsewhere (e.g. holding a shuffled item) can still be chosen here: it places just that sword's marker icon
                    let isSelectable = ((n = originalState) || isSwordTile || TrackerModel.mapSquareChoiceDomain.CanAddUse(n)) && isLegalHere(n) && (n <> TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP || canPlacePotionShop(i,j))
                    // the sword cave icons are shown in the menu without their brown background, with white numbers
                    let tileBmp = 
                        if isSwordTile then Graphics.swordMenuBmp(MapStateProxy(n).DefaultInteriorBmp()) 
                        elif n = TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP then OverworldMapTileCustomization.hintShopMenuBmp(OverworldMapTileCustomization.lastHintShopColor)   // the hint shop icon shows the color the user last chose
                        else MapStateProxy(n).DefaultInteriorBmp()
                    let img = Graphics.BMPtoImage(tileBmp)   // fresh image each popup; cell is wider than the 15px icon, so center it
                    let cell = new Canvas(Width=7.*3., Height=9.*3.)
                    canvasAdd(cell, img, 3., 0.)
                    (cell :> FrameworkElement), isSelectable, n
                let hole : FrameworkElement*bool*int = null, false, -999   // null asks selector to 'leave a hole' here
                let M = TrackerModel.MapSquareChoiceDomainHelper.DARK_X
                let darkXCell : FrameworkElement*bool*int = (new Canvas(Width=7.*3., Height=9.*3., Background=Graphics.overworldCommonestFloorColorDarkBrush) :> FrameworkElement), true, M
                let unmarkedCell : FrameworkElement*bool*int = (new Canvas(Width=7.*3., Height=9.*3., Background=Graphics.overworldCommonestFloorColorBrush) :> FrameworkElement), true, -1
                // decorative marker icons (ids are offset by 1000 so they cannot be confused with real overworld tile states)
                // a Take Any cave, or a letter / white sword / magical sword cave, only gets the bomb dropper marker (middle click); other icons only go on a tile where the sword / letter icon was picked instead (that tile is no longer a cave)
                // (a letter / white / magical sword cave that already holds a shuffled item can take icons: the choice box just crosses out its 'Shuffled Item' option)
                let holdsShuffled = (originalState = TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER || originalState = TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || originalState = TrackerModel.MapSquareChoiceDomainHelper.SWORD3) && TrackerModel.getOverworldMapExtraData(i,j,originalState) = OverworldMapTileCustomization.SHUFFLED_ITEM
                let markersBlocked = not holdsShuffled && ((originalState = TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY) || originalState = TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER || originalState = TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || originalState = TrackerModel.MapSquareChoiceDomainHelper.SWORD3 || isDungeonOrAnyRoad(originalState))   // (a dungeon entrance or any road tile only gets the bomb dropper marker too)
                let swordIconCell(ident) : FrameworkElement*bool*int =
                  if ident = SHOP_MENU then
                    // the 'Shop' cell: diagonal SHOP lettering; picking it opens the flyout (potion shop / shop 1-5)
                    let img = Graphics.BMPtoImage(OverworldMapTileCustomization.shopMenuBmp)
                    img.Stretch <- Stretch.Uniform
                    img.Width <- 21.
                    img.Height <- 27.
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor)
                    let cell = new Canvas(Width=7.*3., Height=9.*3.)
                    canvasAdd(cell, img, 0., 0.)
                    (cell :> FrameworkElement), (not markersBlocked), SHOP_MENU
                  else
                    // the swords are 15x27 cave-style icons; the dungeon items are shown at full 21x21 size (without the black outline, which is only for the map)
                    let isSword = (ident = TrackerModel.ITEMS.PROGRESSIVESWORD || ident = TrackerModel.ITEMS.WHITESWORD4)
                    let progSword = TrackerModel.ProgressiveItems && TrackerModel.progressiveSwordBoxTier(ident) > 0   // Progressive Items: the sword cells show the current P tier sprite (21x27)
                    let bmp = if progSword then Graphics.ow_progressive_tier_menu_bmps.[(let t = TrackerModel.progressiveSwordBoxTier(ident) in if t >= 1 && t <= 4 then t else TrackerModel.progressiveSwordTier())] elif isSword then Graphics.swordMenuBmp(decorIconBmp ident) elif TrackerModel.progressiveDisabledIdent ident && TrackerModel.progressiveFamily ident >= 2 then CustomComboBoxes.boxCurrentBMP(ident, None) else decorSourceBmp ident   // (a disabled red ring / candle / silver arrow keeps its own picture)
                    let img = Graphics.BMPtoImage(bmp)
                    img.Stretch <- Stretch.Uniform
                    img.Width <- (if progSword then 21. elif isSword then 15. else 21.)
                    img.Height <- (if progSword || isSword then 27. else 21.)
                    let cell = new Canvas(Width=7.*3., Height=9.*3.)
                    canvasAdd(cell, img, (if isSword && not progSword then 3. else 0.), (if isSword then 0. else 3.))
                    let progDisabled = TrackerModel.progressiveDisabledIdent ident
                    if progDisabled then
                        cell.Background <- Brushes.Transparent
                    (cell :> FrameworkElement), (not markersBlocked && not progDisabled && not (ident = TrackerModel.ITEMS.WHITESWORD4 && not TrackerModelOptions.FourthSwordEnabled)), 1000 + ident   // the 4th sword icon is disabled unless the 4th Sword start option is on
                // 9 columns x 6 rows
                let topRows : (FrameworkElement*bool*int)[] = [|
                    // row 1: all 9 dungeons
                    for n = 0 to 8 do
                        yield typicalGESAI(n)
                    // row 2: 4 any roads, then the 5 swords starting with wood
                    for n = 9 to 12 do
                        yield typicalGESAI(n)
                    yield typicalGESAI(TrackerModel.MapSquareChoiceDomainHelper.SWORD1)
                    yield typicalGESAI(TrackerModel.MapSquareChoiceDomainHelper.SWORD2)
                    yield typicalGESAI(TrackerModel.MapSquareChoiceDomainHelper.SWORD3)
                    yield swordIconCell(TrackerModel.ITEMS.WHITESWORD4)
                    yield swordIconCell(TrackerModel.ITEMS.PROGRESSIVESWORD)
                    |]
                let markerIdentList =
                    [ TrackerModel.ITEMS.LADDER; TrackerModel.ITEMS.POWERBRACELET; TrackerModel.ITEMS.RECORDER; TrackerModel.ITEMS.RAFT; TrackerModel.ITEMS.BOW
                      TrackerModel.ITEMS.BLUERING; TrackerModel.ITEMS.REDRING; TrackerModel.ITEMS.WOODARROW; TrackerModel.ITEMS.SILVERARROW; TrackerModel.ITEMS.WAND
                      TrackerModel.ITEMS.KEY; TrackerModel.ITEMS.BLUECANDLE; TrackerModel.ITEMS.REDCANDLE; TrackerModel.ITEMS.BOOMERANG; TrackerModel.ITEMS.MAGICBOOMERANG
                      TrackerModel.ITEMS.BOOKSHIELD; TrackerModel.ITEMS.SHIELD; TrackerModel.ITEMS.MEAT; TrackerModel.DECOR_BOMB; TrackerModel.ITEMS.OLDKEY
                      SHOP_MENU; TrackerModel.ITEMS.LETTER; TrackerModel.ITEMS.HEARTCONTAINER; TrackerModel.ITEMS.BLUEPOTION; TrackerModel.ITEMS.BOMBUPGRADE ]
                // the bottom row of the menu is always: shop, bomb, key, letter, heart container, blue potion, bomb upgrade, clear
                let lastRowIdents = [ SHOP_MENU; TrackerModel.DECOR_BOMB; TrackerModel.ITEMS.OLDKEY; TrackerModel.ITEMS.LETTER; TrackerModel.ITEMS.HEARTCONTAINER; TrackerModel.ITEMS.BLUEPOTION; TrackerModel.ITEMS.BOMBUPGRADE ]
                let bottomRows : (FrameworkElement*bool*int)[] = [|
                    // row 3: unknown secret '?' first, then secrets, door repair, money making game, armos, take any, and the purple '?' hint shop at the end
                    yield typicalGESAI(TrackerModel.MapSquareChoiceDomainHelper.UNKNOWN_SECRET)
                    for n = 25 to 29 do
                        yield typicalGESAI(n)
                    yield typicalGESAI(TrackerModel.MapSquareChoiceDomainHelper.ARMOS)
                    yield typicalGESAI(TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY)
                    yield typicalGESAI(TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP)
                    // rows 4-6: dungeon items (decorative icons, in item-menu order from the ladder to the heart container), then the personal preference markers, Don't Care and Unmarked
                    for ident in markerIdentList do
                        if not (lastRowIdents |> List.contains ident) then
                            yield swordIconCell(ident)
                    // the user's custom icons (if that option is on) go at the bottom
                    for k = 0 to TrackerModel.ITEMS.customMenuCount-1 do
                        yield swordIconCell(TrackerModel.ITEMS.CUSTOM_BASE + k)
                    |]
                // Don't Care and Unmarked always end the grid: holes pad the gap so they are the last two cells of the last row
                let clearIconsCell : FrameworkElement*bool*int =
                    let cell = new Canvas(Width=7.*3., Height=9.*3.)
                    for (x1, y1, x2, y2) in [ (4., 7., 17., 20.); (17., 7., 4., 20.) ] do
                        cell.Children.Add(new System.Windows.Shapes.Line(X1=x1, Y1=y1, X2=x2, Y2=y2, Stroke=Brushes.Red, StrokeThickness=3.5, StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round)) |> ignore
                    (cell :> FrameworkElement), true, CLEAR_ICONS
                let tailCells = [| yield! (lastRowIdents |> List.map swordIconCell); yield clearIconsCell |]   // (no Don't Care cell: the clear option does it better)
                let bodyCells = Array.append topRows bottomRows
                let paddedCount = ((bodyCells.Length + tailCells.Length + GCOL - 1) / GCOL) * GCOL
                let GROW = paddedCount / GCOL
                let GCOUNT = GCOL*GROW
                let gridElementsSelectablesAndIDs = Array.concat [ bodyCells; Array.create (paddedCount - bodyCells.Length - tailCells.Length) hole; tailCells ]
                // the tile's current state might not be offered in this menu (e.g. a potion shop / letter / other state set by a hotkey or an older save);
                // fall back to the 'Unmarked' cell rather than crashing
                let originalStateIndex =
                    match gridElementsSelectablesAndIDs |> Array.tryFindIndex (fun (_,_,s) -> s = originalState) with
                    | Some idx -> idx
                    | None -> gridElementsSelectablesAndIDs |> Array.findIndex (fun (_,_,s) -> s = CLEAR_ICONS)   // (no 'Unmarked' cell: the red X does the same)
                if gridElementsSelectablesAndIDs.Length <> GCOUNT then
                    failwith "bad ow grid tile layout"
                async {
                    let! r = (if openShopFlyoutDirectly then (openShopFlyoutDirectly <- false; async { return Some(SHOP_MENU) }) else CustomComboBoxes.DoModalGridSelect(cm, pos.X, pos.Y, tileCanvas,
                                gridElementsSelectablesAndIDs, originalStateIndex, activationDelta, (GCOL, GROW, 7*3, 9*3), float(7*3)/2., float(9*3)/2., gridxPosition, 11.*3.+ST,
                                (fun (currentState) -> 
                                    tileCanvas.Children.Clear()
                                    canvasAdd(tileCanvas, tileImage, 0., 0.)
                                    let isClearCell = (currentState = CLEAR_ICONS)
                                    let isShopCell = (currentState = SHOP_MENU)
                                    let isSwordIcon = currentState >= 1000 && not isClearCell && not isShopCell
                                    if isSwordIcon then
                                        let pbmp = decorIconBmp (currentState-1000)
                                        if pbmp <> null then
                                            let icon = Graphics.BMPtoImage(pbmp)
                                            icon.Stretch <- Stretch.Uniform
                                            icon.Width <- 27.
                                            icon.Height <- 27.
                                            canvasAdd(tileCanvas, icon, OMTW/2. - 13.5, 3.)
                                    elif isClearCell || isShopCell then ()
                                    else
                                        let _,bmp,_ = GetIconBMPAndExtraDecorations(cm, MapStateProxy(currentState), i, j)
                                        if bmp <> null then
                                            let icon = bmp |> Graphics.BMPtoImage
                                            if MapStateProxy(currentState).IsX then
                                                icon.Opacity <- X_OPACITY
                                            canvasAdd(tileCanvas, icon, 0., 0.)
                                    let s = 
                                        if isSwordIcon then decorDescription(currentState-1000) + "\n(marker icon only)" + (if TrackerModel.progressiveDisabledIdent (currentState-1000) then "\nDisabled Due to Progressive Items" else "")
                                        elif isClearCell then "Clear this tile\n(removes marker icons and sets it back to unmarked)"
                                        elif isShopCell then "Shop...\n(potion shop, or shop 1-4: puts the shop's name at the top of the tile)"
                                        elif currentState = -1 then "Unmarked" 
                                        else let _,_,_,s = TrackerModel.dummyOverworldTiles.[currentState] in s
                                    let s = if isSwordIcon || isClearCell || isShopCell then s else HotKeys.OverworldHotKeyProcessor.AppendHotKeyToDescription(s,currentState)
                                    let s = if currentState >= TrackerModel.MapSquareChoiceDomainHelper.WARP_1 && currentState <= TrackerModel.MapSquareChoiceDomainHelper.WARP_4 then s + " - Right click to place Vanilla" else s
                                    // the description box sits just above the menu and is exactly as wide as it (the menu can be resized, so follow its current size); the text wraps and scales with the width
                                    let (menuX, menuY, menuW, menuH) = CustomComboBoxes.MovableGridRect
                                    let boxW = if menuW > 0. then menuW else gridWidth
                                    let fs = max 9. (min 24. (16. * boxW / gridWidth))
                                    let text = new TextBox(Text=s, Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, BorderThickness=Thickness(0.),
                                                                FontSize=fs, TextWrapping=TextWrapping.Wrap, HorizontalContentAlignment=HorizontalAlignment.Center)
                                    let textBorder = 
                                        if currentState >= TrackerModel.MapSquareChoiceDomainHelper.SHOP && currentState < TrackerModel.MapSquareChoiceDomainHelper.SHOP + TrackerModel.MapSquareChoiceDomainHelper.NUM_ITEMS then
                                            let extra = new TextBox(Text="After marking a shop, left-click\nit again to mark a second item", 
                                                                        Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, BorderThickness=Thickness(0.),
                                                                        FontSize=10., TextWrapping=TextWrapping.Wrap, HorizontalContentAlignment=HorizontalAlignment.Center)
                                            let sp = new StackPanel(Orientation=Orientation.Vertical)
                                            sp.Children.Add(text) |> ignore
                                            sp.Children.Add(new DockPanel(Background=Brushes.Gray, Height=1., Margin=Thickness(12.,0.,12.,0.))) |> ignore
                                            sp.Children.Add(extra) |> ignore
                                            new Border(BorderThickness=Thickness(ST), Child=sp, Background=Brushes.Black, BorderBrush=Brushes.Gray)
                                        else
                                            new Border(BorderThickness=Thickness(ST), Child=text, Background=Brushes.Black, BorderBrush=Brushes.Gray)
                                    textBorder.Width <- boxW
                                    textBorder.Measure(Size(boxW, System.Double.PositiveInfinity))
                                    let boxLeft = if menuW > 0. then menuX else gridxPosition
                                    let boxTop = if menuW > 0. then menuY + menuH + 4. else (11.*3.+ST) - textBorder.DesiredSize.Height - 4.   // under the menu (below the tip box)
                                    Canvas.SetLeft(textBorder, boxLeft)
                                    Canvas.SetTop(textBorder, boxTop)
                                    tileCanvas.Children.Add(textBorder) |> ignore
                                    ),
                                (fun (ea:Input.MouseButtonEventArgs, currentState) ->
                                    if ea.ChangedButton = Input.MouseButton.Right && currentState >= TrackerModel.MapSquareChoiceDomainHelper.WARP_1 && currentState <= TrackerModel.MapSquareChoiceDomainHelper.WARP_4 then
                                        CustomComboBoxes.DismissPopupWithResult(ANYROAD_MENU)
                                    else CustomComboBoxes.DismissPopupWithResult(currentState)),
                                [], CustomComboBoxes.ModalGridSelectBrushes.Defaults(), CustomComboBoxes.WarpToCenter, None, "OWGridTile", None))
                    let reopenMenu = ref false
                    let flyoutAddedItem = ref false   // set when items were added to a shop from inside the flyout (so clicking that shop afterwards keeps its label instead of toggling it off)
                    let shopItemBitmaps(n:int) = shopItemBitmapsGlobal n
                    // placing another 'Shop n' starts it with the same items (and collected state) as an existing 'Shop n' tile, and links the two like a copy / paste would
                    let autoDupShop(n:int) : bool =
                        if TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]).IsEmpty then
                            let src = [ for x in 0..15 do
                                          for y in 0..7 do
                                              if (x,y) <> (i,j) && TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[x,y]) = n && not (TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[x,y]).IsEmpty) then yield (x,y) ]
                            if src.IsEmpty then false
                            else
                                let source = src.Head
                                setDecorIcons(i, j, TrackerModel.owSwordIcons.[fst source, snd source])   // (the packed value carries the shop label too)
                                let ok, l = dupLinks.TryGetValue(source)
                                if ok then
                                    if not (l.Contains((i,j))) then l.Add((i,j))
                                else
                                    let nl = new ResizeArray<(int*int)>()
                                    nl.Add((i,j))
                                    dupLinks.[source] <- nl
                                TrackerModel.overworldMapMarks.[fst source, snd source].Current() = TrackerModel.MapSquareChoiceDomainHelper.BOMB   // true if the source is a bomb shop
                        else false
                    // the 'Shop' cell opens a flyout: the potion shop (the usual blue potion marker) or one of the 4 numbered shops
                    let! r =
                        async {
                            match r with
                            | Some(sel) when sel = SHOP_MENU ->
                                flyoutAddedItem.Value <- false
                                let counts = Array.init 5 (fun n -> TrackerModel.shopLabelCount (if n = 0 then 5 else n))
                                let shopItems = Array.init 5 shopItemBitmaps
                                // an item hotkey over a shop: this tile becomes that shop (if it isn't already) and the item goes in it, while the flyout stays open
                                let addItemToShopImpl(shop:int, ident:int) =
                                    // (the flyout's shop 0 is the P shop, shop label 5)
                                    flyoutAddedItem.Value <- true
                                    let lbl = if shop = 0 then 5 else shop
                                    let curLabel = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[i,j])
                                    let hasIdent (x:int, y:int) = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons.[x,y]) |> List.exists (fun (id,_) -> id = ident)
                                    // the tile the item goes on: this one if it is that shop (or has no shop yet); otherwise another tile of that shop, or if there is none this tile changes into it
                                    let target : int*int =
                                        if curLabel = lbl || curLabel = 0 then (i,j)
                                        else
                                            match [ for x in 0..15 do for y in 0..7 do if TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[x,y]) = lbl then yield (x,y) ] with
                                            | t :: _ -> t
                                            | [] -> (i,j)
                                    let (tx, ty) = target
                                    // an item already in a different shop moves: it is taken out of that shop first (shops with the same label are synced, so one tile does them all)
                                    let removeIdent (x:int, y:int) =
                                        let p0 = TrackerModel.owSwordIcons.[x,y]
                                        let l0 = TrackerModel.shopLabelOf(p0)
                                        let es = TrackerModel.decorEntriesOf(p0)
                                        let k = es |> List.findIndex (fun (id,_) -> id = ident)
                                        let es2 = es |> List.indexed |> List.filter (fun (n,_) -> n <> k) |> List.map snd
                                        let packed = TrackerModel.decorEntriesPack es2
                                        setDecorIcons(x, y, (if l0 > 0 then TrackerModel.withShopLabel(packed, l0) else packed))
                                        redrawOwTileHook(x, y)
                                    for m in 1..5 do
                                        if false && m <> lbl then   // (an item can now be in several shops at once, so adding it to one shop no longer takes it out of another)   // (potions can be sold in several shops, so a blue potion is never moved out of another shop; with Progressive Items neither are swords, rings, candles and arrows)
                                            match [ for x in 0..15 do for y in 0..7 do if TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[x,y]) = m && hasIdent(x,y) then yield (x,y) ] with
                                            | t :: _ -> removeIdent t
                                            | [] -> ()
                                    if TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[tx,ty]) <> lbl then
                                        // a tile becoming this shop: take on what that shop already holds (otherwise this tile's old items would overwrite it on every tile of that shop)
                                        let others = [ for x in 0..15 do for y in 0..7 do if (x,y) <> (tx,ty) && TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[x,y]) = lbl then yield TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons.[x,y]) ]
                                        setShopLabel(tx, ty, lbl)
                                        match others with
                                        | e :: _ -> setDecorIcons(tx, ty, TrackerModel.withShopLabel(TrackerModel.decorEntriesPack e, lbl))
                                        | [] -> ()
                                    let before = TrackerModel.owSwordIcons.[tx,ty]
                                    if lbl = 5 then
                                        // the P shop has just the one slot: the new item replaces what was there (the same item again removes it)
                                        let already = TrackerModel.decorEntriesOf(before) |> List.exists (fun (x,_) -> x = ident)
                                        setDecorIcons(tx, ty, TrackerModel.withShopLabel(TrackerModel.decorEntriesPack(if already then [] else [(ident,false)]), lbl))
                                    else
                                        setDecorIcons(tx, ty, TrackerModel.decorIconsToggle(before, ident))
                                    if TrackerModel.owSwordIcons.[tx,ty] = before then
                                        Graphics.ErrorBeepWithReminderLogText(sprintf "A tile can show at most %d marker icons; ctrl+left click one of its icons to remove it first" TrackerModel.MaxDecorIcons)
                                    if (tx,ty) = (i,j) && not(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[tx,ty]).IsEmpty) && not(owInstance.AlwaysEmpty(tx,ty)) && TrackerModel.overworldMapMarks.[tx,ty].Current() = -1 then
                                        SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.DARK_X, -1) |> Async.StartImmediate
                                    redrawOwTileHook(tx, ty)
                                    redrawGridSpot()
                                    (Array.init 5 (fun n -> TrackerModel.shopLabelCount (if n = 0 then 5 else n))), (Array.init 5 shopItemBitmaps)
                                let addItemToShop(shop:int, ident:int) =
                                    let lbl = if shop = 0 then 5 else shop
                                    let curLabel = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[i,j])
                                    let targetHasTile = [ for x in 0..15 do for y in 0..7 do if TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[x,y]) = lbl then yield 1 ] |> List.isEmpty |> not
                                    if curLabel <> lbl && not targetHasTile then
                                        // that shop is not on the map yet: the items wait in the shop window until a tile becomes that shop
                                        if false then   // (items can be in several shops at once: nothing is taken out of the other shops)
                                            for m in 1..5 do
                                                if m <> lbl then
                                                    match [ for x in 0..15 do for y in 0..7 do if TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[x,y]) = m && (TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[x,y]) |> List.contains ident) then yield (x,y) ] with
                                                    | (x,y) :: _ ->
                                                        let es = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons.[x,y]) |> List.filter (fun (id,_) -> id <> ident)
                                                        setDecorIcons(x, y, TrackerModel.withShopLabel(TrackerModel.decorEntriesPack es, m))
                                                        redrawOwTileHook(x, y)
                                                    | [] ->
                                                        let es = TrackerModel.decorEntriesOf(TrackerModel.shopStash.[m])
                                                        if es |> List.exists (fun (id,_) -> id = ident) then
                                                            TrackerModel.shopStash.[m] <- TrackerModel.decorEntriesPack(es |> List.filter (fun (id,_) -> id <> ident))
                                        let stashBefore = TrackerModel.shopStash.[lbl]
                                        if lbl = 5 then
                                            let already = TrackerModel.decorEntriesOf(stashBefore) |> List.exists (fun (x,_) -> x = ident)
                                            TrackerModel.shopStash.[lbl] <- TrackerModel.decorEntriesPack(if already then [] else [(ident,false)])
                                        else
                                            TrackerModel.shopStash.[lbl] <- TrackerModel.decorIconsToggle(stashBefore, ident)
                                            if TrackerModel.shopStash.[lbl] = stashBefore then
                                                Graphics.ErrorBeepWithReminderLogText(sprintf "A shop can show at most %d items" TrackerModel.MaxDecorIcons)
                                        decorStateChanged()
                                        (Array.init 5 (fun n -> TrackerModel.shopLabelCount (if n = 0 then 5 else n))), (Array.init 5 shopItemBitmaps)
                                    else addItemToShopImpl(shop, ident)
                                // the marker icons offered when right clicking a shop in the flyout (the same icons as the overworld menu, minus the Shop cell)
                                let pickItemForShop() : Async<int> =
                                    let idents = [ TrackerModel.ITEMS.WOODSWORD; TrackerModel.ITEMS.WHITESWORD; TrackerModel.ITEMS.MAGICALSWORD; TrackerModel.ITEMS.WHITESWORD4; TrackerModel.ITEMS.PROGRESSIVESWORD ] @ [ for ident in markerIdentList do if ident <> SHOP_MENU then yield ident ] @ [ for k = 0 to TrackerModel.ITEMS.customMenuCount-1 do yield TrackerModel.ITEMS.CUSTOM_BASE + k ]
                                    let isProgressiveDisabled(ident:int) = TrackerModel.progressiveDisabledIdent ident
                                    let items = [ for ident in idents do
                                                    let (cell, enabled, _) = swordIconCell ident
                                                    if enabled || isProgressiveDisabled ident then yield (ident, cell) ]
                                    PieMenus.itemPickerDisabledTip <- (fun ident -> if isProgressiveDisabled ident then "Disabled Due to Progressive Items" else null)
                                    PieMenus.ItemPickerAsync(cm, items, 9)
                                let! pick = PieMenus.ShopFlyoutAsync(cm, counts, TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[i,j]), shopItems, canPlacePotionShop(i,j), addItemToShop, pickItemForShop)
                                if pick = 0 then return Some(SHOP_LABEL_BASE + 5)   // the P shop is shop label 5
                                elif pick >= 1 && pick <= 4 then return Some(SHOP_LABEL_BASE + pick)
                                elif pick >= 1000 then return Some(pick)   // an item hotkey pressed in the flyout
                                else return None
                            | other -> return other
                        }
                    match r with
                    | Some(currentState) -> 
                        if currentState > SHOP_LABEL_BASE && currentState <= SHOP_LABEL_BASE + 5 then
                            // a numbered shop: its "SHOP n" text goes across the top of the tile (picking the same number again removes it)
                            let n = currentState - SHOP_LABEL_BASE
                            setShopLabel(i, j, (if TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[i,j]) = n && not flyoutAddedItem.Value then 0 else n))
                            let sourceWasBomb = (TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[i,j]) = n && autoDupShop(n))
                            if sourceWasBomb && TrackerModel.overworldMapMarks.[i,j].Current() = -1 && not(owInstance.AlwaysEmpty(i,j)) && TrackerModel.mapSquareChoiceDomain.CanAddUse(TrackerModel.MapSquareChoiceDomainHelper.BOMB) then
                                do! SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.BOMB, -1)   // the shop being duplicated is a bomb shop: this one is too
                            // like a marker icon, an unmarked tile that gets a shop label counts as finished (so the shop's items can be put on it)
                            if TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[i,j]) > 0 && not(owInstance.AlwaysEmpty(i,j)) && TrackerModel.overworldMapMarks.[i,j].Current() = -1 then
                                do! SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.DARK_X, -1)
                            redrawGridSpot()
                        elif currentState = ANYROAD_MENU then
                            let placeText = "Place Vanilla Any Roads"
                            let! choice = PieMenus.MessageChoiceBoxAsync(cm, "", [placeText; "Cancel"])
                            if choice = placeText then
                                // 1 in H10, 2 in B14, 3 in C4, 4 in E10 (row letter, then column number)
                                for (state, rowLetter, col) in [ (TrackerModel.MapSquareChoiceDomainHelper.WARP_1, 'H', 10); (TrackerModel.MapSquareChoiceDomainHelper.WARP_2, 'B', 14)
                                                                 (TrackerModel.MapSquareChoiceDomainHelper.WARP_3, 'C', 4);  (TrackerModel.MapSquareChoiceDomainHelper.WARP_4, 'E', 10) ] do
                                    let ti, tj = col-1, int rowLetter - int 'A'
                                    for x = 0 to 15 do
                                        for y = 0 to 7 do
                                            if TrackerModel.overworldMapMarks.[x,y].Current() = state && (x <> ti || y <> tj) then   // this any road was marked somewhere else: move it
                                                TrackerModel.overworldMapMarks.[x,y].Set(-1)
                                                owUpdateFunctions.[x,y] 0 null
                                    TrackerModel.overworldMapMarks.[ti,tj].Set(state)
                                    owUpdateFunctions.[ti,tj] 0 null
                                TrackerModel.forceUpdate()
                                redrawGridSpot()
                            else
                                reopenMenu.Value <- true   // cancel: back to the overworld menu
                        elif currentState = CLEAR_ICONS then
                            // red X: clears the tile completely - every marker icon is removed and the tile goes back to unmarked
                            clearAllTileIcons(i, j)
                            if OverworldMapTileCustomization.isSwordCaveState(originalState) then
                                OverworldMapTileCustomization.clearSwordCaveItemsForState(i, j, originalState)   // the sword / candle / shuffled item taken there is given back
                                UIComponents.itemProgressChanged()
                            // (a tile that can never hold a mark still gets a cave / dungeon that was placed on it removed, but its plain dark X stays)
                            if originalState <> -1 && (not(owInstance.AlwaysEmpty(i,j)) || originalState <> TrackerModel.MapSquareChoiceDomainHelper.DARK_X) then
                                do! SetNewValue(-1, originalState)
                            redrawGridSpot()
                        elif currentState = 1000 + TrackerModel.ITEMS.LETTER
                                && (originalState = -1 || originalState = TrackerModel.MapSquareChoiceDomainHelper.DARK_X)
                                && not(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]) |> List.contains TrackerModel.ITEMS.LETTER)
                                && not(owInstance.AlwaysEmpty(i,j)) && TrackerModel.mapSquareChoiceDomain.CanAddUse(TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER) then
                            // the letter works like the sword caves: it places the letter cave, and the popup asks Took Letter / Shuffled Item / Letter Icon
                            do! SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER, originalState)
                            redrawGridSpot()
                        elif currentState = 1000 + TrackerModel.DECOR_BOMB
                                && (originalState = -1 || originalState = TrackerModel.MapSquareChoiceDomainHelper.DARK_X)
                                && not(owInstance.AlwaysEmpty(i,j)) && TrackerModel.mapSquareChoiceDomain.CanAddUse(TrackerModel.MapSquareChoiceDomainHelper.BOMB) then
                            // a bomb marker means a bomb shop: place the bomb shop tile
                            do! SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.BOMB, originalState)
                            redrawGridSpot()
                        elif (currentState = TrackerModel.MapSquareChoiceDomainHelper.SWORD1 || currentState = TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || currentState = TrackerModel.MapSquareChoiceDomainHelper.SWORD3)
                                && currentState <> originalState && not(TrackerModel.mapSquareChoiceDomain.CanAddUse(currentState)) then
                            // that sword cave is already placed elsewhere: this tile just gets the sword's marker icon (and counts as finished, like any other marker icon)
                            let ident = (if currentState = TrackerModel.MapSquareChoiceDomainHelper.SWORD1 then TrackerModel.ITEMS.WOODSWORD elif currentState = TrackerModel.MapSquareChoiceDomainHelper.SWORD2 then TrackerModel.ITEMS.WHITESWORD else TrackerModel.ITEMS.MAGICALSWORD)
                            let before = TrackerModel.owSwordIcons.[i,j]
                            setDecorIcons(i, j, TrackerModel.decorIconsToggle(before, ident))
                            if TrackerModel.owSwordIcons.[i,j] = before then
                                Graphics.ErrorBeepWithReminderLogText(sprintf "A tile can show at most %d marker icons; ctrl+left click one of its icons to remove it first" TrackerModel.MaxDecorIcons)
                            if not(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]).IsEmpty) && not(owInstance.AlwaysEmpty(i,j)) && TrackerModel.overworldMapMarks.[i,j].Current() = -1 then
                                do! SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.DARK_X, -1)
                            redrawGridSpot()
                        elif currentState = 1000 + TrackerModel.DECOR_BOMB && originalState = TrackerModel.MapSquareChoiceDomainHelper.BOMB then
                            // the bomb marker on a bomb shop takes the bomb back off: the shop keeps its other icons (as a finished tile), or goes back to unmarked if it has none
                            do! SetNewValue((if TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]).IsEmpty then -1 else TrackerModel.MapSquareChoiceDomainHelper.DARK_X), originalState)
                            redrawGridSpot()
                        elif currentState >= 1000 then
                            // decorative sword item icon: toggles on this tile, tile's real mark is left alone
                            let ident = currentState - 1000
                            let before = TrackerModel.owSwordIcons.[i,j]
                            setDecorIcons(i, j, decorToggleForTile(before, ident))
                            if TrackerModel.owSwordIcons.[i,j] = before then
                                Graphics.ErrorBeepWithReminderLogText(sprintf "A tile can show at most %d marker icons; ctrl+left click one of its icons to remove it first" TrackerModel.MaxDecorIcons)
                            // an unmarked tile that gets a marker icon counts as finished (Don't Care), icons stay visible
                            if not(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]).IsEmpty) && not(owInstance.AlwaysEmpty(i,j)) && TrackerModel.overworldMapMarks.[i,j].Current() = -1 then
                                do! SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.DARK_X, -1)
                            redrawGridSpot()
                        else
                            if currentState = -1 || currentState = TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY then clearAllTileIcons(i, j)   // 'Unmarked' also clears any sword icon; placing a take any cave clears every other item on the tile
                            if OverworldMapTileCustomization.isSwordCaveState(originalState) && currentState <> originalState then
                                OverworldMapTileCustomization.clearSwordCaveItemsForState(i, j, originalState)
                                UIComponents.itemProgressChanged()
                            do! SetNewValue(currentState, originalState)
                            if currentState = -1 then redrawGridSpot()
                    | None -> ()
                    tileCanvas.Children.Clear()   // deparent tileImage
                    popupIsActive <- false
                    if reopenMenu.Value then activatePopup(0)
                    } |> Async.StartImmediate
            // copy / paste item markers (mouse button 4; while markers are carried, a plain left click on an open tile also pastes); false if there was nothing to copy or paste here
            let copyOrPasteMarkers() : bool =
                let curMark = TrackerModel.overworldMapMarks.[i,j].Current()
                let hereIcons = TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j])
                let onlyItems = curMark = -1 || curMark = TrackerModel.MapSquareChoiceDomainHelper.DARK_X || curMark = TrackerModel.MapSquareChoiceDomainHelper.BOMB
                if not hereIcons.IsEmpty && onlyItems then
                    copiedMarkerIdents <- hereIcons
                    // the duplicate keeps everything on the original, including which items are already collected
                    copiedMarkerPacked <- (let p = TrackerModel.owSwordIcons.[i,j]
                                           let kept = TrackerModel.decorEntriesOf p
                                           let packed = TrackerModel.decorEntriesPack kept
                                           let label = TrackerModel.shopLabelOf p
                                           if label > 0 then TrackerModel.withShopLabel(packed, label) else packed)
                    copiedMarkerSource <- (i, j)
                    copiedMarkerBomb <- (curMark = TrackerModel.MapSquareChoiceDomainHelper.BOMB)
                    refreshCopyCursor()
                    animateOverworldTileIfOptionIsChecked(i,j)   // brief flash = copied; left-click (or mouse button 4 on) an open tile to paste
                    true
                elif hereIcons.IsEmpty && curMark = -1 && not copiedMarkerIdents.IsEmpty && not(owInstance.AlwaysEmpty(i,j)) then
                    popupIsActive <- true
                    let linkDuplicate() =
                        if copiedMarkerSource <> (i, j) && copiedMarkerSource <> (-1, -1) then
                            let ok, l = dupLinks.TryGetValue(copiedMarkerSource)
                            if ok then
                                if not (l.Contains((i,j))) then l.Add((i,j))
                            else
                                let nl = new ResizeArray<(int*int)>()
                                nl.Add((i,j))
                                dupLinks.[copiedMarkerSource] <- nl
                    async {
                        setDecorIcons(i, j, copiedMarkerPacked)   // just the duplicated markers: the tile itself is left as it was (a copied bomb shop makes this a bomb shop too)
                        if copiedMarkerBomb && TrackerModel.mapSquareChoiceDomain.CanAddUse(TrackerModel.MapSquareChoiceDomainHelper.BOMB) then
                            do! SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.BOMB, -1)
                        elif not copiedMarkerBomb then
                            do! SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.DARK_X, -1)   // a duplicated shop is placed as complete (dimmed), like any placed shop
                        linkDuplicate()
                        redrawGridSpot()
                        copiedMarkerIdents <- []   // one paste per copy: clear the cursor
                        refreshCopyCursor()
                        popupIsActive <- false
                    } |> Async.StartImmediate
                    true
                else false
            c.MouseDown.Add(fun ea -> 
                if not popupIsActive then
                    if ea.ChangedButton = Input.MouseButton.Left then
                        let canPasteHere = not copiedMarkerIdents.IsEmpty && TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]).IsEmpty && TrackerModel.overworldMapMarks.[i,j].Current() = -1 && not(owInstance.AlwaysEmpty(i,j))
                        if canPasteHere then
                            // markers are being carried on the mouse (copied with mouse button 4): the first left click on any open tile puts them down
                            copyOrPasteMarkers() |> ignore
                        elif Input.Keyboard.IsKeyDown(Input.Key.LeftShift) || Input.Keyboard.IsKeyDown(Input.Key.RightShift) then
                            // shift left click: the circle label popup
                            activateCircleLabelPopup()
                        elif visibleDecorIcons(TrackerModel.owSwordIcons.[i,j]) |> List.exists (fun id -> id <> TrackerModel.DECOR_BOMB_DROPPER) then   // (a bomb dropper marker is not a checkable item: left click on it falls through to the normal tile click)
                            // left click on a tile with marker icons checks off (or un-checks) just the icon nearest the mouse
                            let vis = visibleDecorIcons(TrackerModel.owSwordIcons.[i,j])
                            let px = ea.GetPosition(c).X
                            let ms = MapStateProxy(TrackerModel.overworldMapMarks.[i,j].Current())
                            let big = ms.State = -1 || ms.IsX || bombSlotActive.Value
                            let nearest =
                                let disp = decorDisplayList vis
                                disp |> List.indexed |> List.filter (fun (_,(id,_)) -> id <> TrackerModel.DECOR_BOMB_DROPPER) |> List.minBy (fun (dk,_) ->
                                    let off = if bombSlotActive.Value then 1 else 0
                                    let size,x,_y = decorSlotC(TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[i,j]) > 0, bombSlotActive.Value, big, disp.Length + off, dk + off)
                                    abs (px - (x + size/2.)))
                                |> fun (_,(_,origK)) -> origK, vis.[origK]
                            // (the same item can appear more than once, e.g. several heart containers: act on just the nearest copy)
                            let nearestIdx = fst nearest
                            let nearestIdent = snd nearest
                            let copyNumber = vis |> List.take nearestIdx |> List.filter (fun id -> id = nearestIdent) |> List.length
                            if Input.Keyboard.IsKeyDown(Input.Key.LeftCtrl) || Input.Keyboard.IsKeyDown(Input.Key.RightCtrl) then
                                // ctrl+left click removes just that one icon from the tile
                                setDecorIcons(i, j, TrackerModel.decorRemoveNth(TrackerModel.owSwordIcons.[i,j], nearestIdent, copyNumber))
                            elif nearestIdent = TrackerModel.ITEMS.WHITESWORD || nearestIdent = TrackerModel.ITEMS.MAGICALSWORD then
                                ()   // the white and magical sword icons can't be checked off as collected by clicking (hovering still shows the purple crosshairs)
                            else
                                setDecorIcons(i, j, TrackerModel.decorToggleCollectedNth(TrackerModel.owSwordIcons.[i,j], nearestIdent, copyNumber))
                            redrawGridSpot()
                        else
                            // left click is the 'special interaction'
                            let pos = c.TranslatePoint(Point(), appMainCanvas)
                            let msp = MapStateProxy(TrackerModel.overworldMapMarks.[i,j].Current())
                            if msp.IsX then
                                if TrackerModel.owInstance.AlwaysEmpty(i,j) then
                                    ()  // tile is AlwaysEmpty in current quest; don't open popup (only circles allowed)
                                elif Graphics.shouldInitiallyHideOverworldMap then
                                    let ex = TrackerModel.getOverworldMapExtraData(i,j,msp.State)
                                    popupIsActive <- true
                                    async {
                                        if ex=msp.State then
                                            // hidden -> unmarked           (and recall that unmarked left clicks to DontCare)
                                            TrackerModel.setOverworldMapExtraData(i,j,msp.State,0)
                                            do! SetNewValue(-1,msp.State)
                                        else
                                            // DontCare -> hidden           (thus left click is a 3-cycle hidden, unmarked, DontCare)
                                            TrackerModel.setOverworldMapExtraData(i,j,msp.State,msp.State)
                                        redrawGridSpot()
                                        popupIsActive <- false
                                    } |> Async.StartImmediate
                                else
                                    activatePopup(0)  // thus, if you have unmarked, then left-click left-click pops up, as the first marks X, and the second now pops up
                            elif msp.IsThreeItemShop then
                                activatePopup(0)   // shops now use the marker-icon menu (no old 2nd / 3rd shop item chooser)
                            else
                                popupIsActive <- true
                                async {
                                    let! needRedraw, needHighlightTileHide = DoLeftClick(cm,msp,i,j,pos)
                                    if needRedraw then 
                                        redrawGridSpot()
                                        animateOverworldTileIfOptionIsChecked(i,j)
                                    if needHighlightTileHide then OverworldRouteDrawing.routeDrawingLayer.GetHighlightTile(i,j).Hide()  // dismiss any green/yellow highlight on this tile
                                    popupIsActive <- false
                                } |> Async.StartImmediate
                    elif ea.ChangedButton = Input.MouseButton.Right then
                        if Input.Keyboard.IsKeyDown(Input.Key.LeftShift) || Input.Keyboard.IsKeyDown(Input.Key.RightShift) then
                            // shift right click clears the tile: back to unmarked (which also un-finds a dungeon/cave marked here), no marker icons, no circle
                            // (tiles that can never hold a mark, e.g. E1, still get their marker icons / circle cleared)
                            if not popupIsActive then
                                popupIsActive <- true
                                ea.Handled <- true
                                async {
                                    let cur = TrackerModel.overworldMapMarks.[i,j].Current()
                                    clearAllTileIcons(i, j)   // un-collects any items that were checked off from this tile's icons
                                    // a wood sword cave where the sword / candle / shuffled item was taken: take that item back
                                    if OverworldMapTileCustomization.isSwordCaveState(cur) then
                                        OverworldMapTileCustomization.clearSwordCaveItemsForState(i,j,cur)
                                        UIComponents.itemProgressChanged()
                                    if cur <> -1 && (not(owInstance.AlwaysEmpty(i,j)) || cur <> TrackerModel.MapSquareChoiceDomainHelper.DARK_X) then
                                        do! SetNewValue(-1, cur)
                                    if TrackerModel.overworldMapCircles.[i,j] <> 0 then
                                        TrackerModel.overworldMapCircles.[i,j] <- 0
                                        owCircleRedraws.[i,j]()
                                    TrackerModel.forceUpdate()
                                    redrawGridSpot()
                                    popupIsActive <- false
                                } |> Async.StartImmediate
                        else
                            // right click activates the popup selector on every tile
                            activatePopup(0)
                    elif ea.ChangedButton = Input.MouseButton.XButton1 then
                        copyOrPasteMarkers() |> ignore   // mouse button 4 copies / pastes item markers
                    elif ea.ChangedButton = Input.MouseButton.Middle then
                        if TrackerModel.MarkBombDroppers && Input.Keyboard.Modifiers <> Input.ModifierKeys.Shift then
                            // 'Mark Bomb Droppers' startup option: the first middle click puts the bomb dropper icon on the tile; the scroll wheel then cycles bomb dropper > blue > yellow > red circle and around;
                            // middle clicking again removes whatever was chosen. (shift+middle click is the plain circle toggle)
                            let hasDropper = TrackerModel.owBombDroppers.[i,j]
                            if hasDropper then
                                TrackerModel.owBombDroppers.[i,j] <- false
                            elif TrackerModel.overworldMapCircles.[i,j] <> 0 then
                                TrackerModel.overworldMapCircles.[i,j] <- 0
                                owCircleRedraws.[i,j]()
                            else
                                TrackerModel.owBombDroppers.[i,j] <- true
                            TrackerModel.forceUpdate()
                            redrawGridSpot()
                        else
                            // middle click toggles the circle
                            TrackerModel.toggleOverworldMapCircle(i,j)
                            owCircleRedraws.[i,j]()
                )
            c.MouseWheel.Add(fun x -> 
                if not popupIsActive then 
                    let hasDropper = TrackerModel.owBombDroppers.[i,j]
                    if TrackerModel.overworldMapMarks.[i,j].Current() = TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP && TrackerModel.overworldMapCircles.[i,j] = 0 then
                        // a hint shop: scrolling cycles its background color (purple, red, green, orange)
                        let cur = OverworldMapTileCustomization.hintShopColorOf(i,j)
                        OverworldMapTileCustomization.hintShopColors.[(i,j)] <- (cur + (if x.Delta<0 then 1 else 3)) % 4
                        OverworldMapTileCustomization.hintShopColorChosen <- true
                        OverworldMapTileCustomization.lastHintShopColor <- OverworldMapTileCustomization.hintShopColors.[(i,j)]
                        redrawGridSpot()
                    elif TrackerModel.MarkBombDroppers && hasDropper && TrackerModel.overworldMapCircles.[i,j] = 0 then
                        // bomb dropper > (scroll) blue > yellow > red > bomb dropper ...
                        TrackerModel.owBombDroppers.[i,j] <- false
                        TrackerModel.toggleOverworldMapCircle(i,j)   // starts at the first color
                        if x.Delta >= 0 then TrackerModel.prevOverworldMapCircleColor(i,j)   // scrolling the other way enters the rotation at the last color
                        owCircleRedraws.[i,j]()
                        redrawGridSpot()
                    elif TrackerModel.overworldMapCircles.[i,j] <> 0 then   // if there's a circle, then it takes scrollwheel priority
                        let colorIndex = TrackerModel.overworldMapCircles.[i,j] / 100
                        let isLabeled = (TrackerModel.overworldMapCircles.[i,j] % 100) > 1   // a circle labeled with a letter/digit (hint) just cycles its colors; only unlabeled circles rotate through the bomb dropper icon
                        if TrackerModel.MarkBombDroppers && not isLabeled && ((x.Delta<0 && colorIndex = 2) || (x.Delta>=0 && colorIndex = 0)) then
                            // scrolled past the last (first) color: back around to the bomb dropper icon
                            TrackerModel.overworldMapCircles.[i,j] <- 0
                            owCircleRedraws.[i,j]()
                            TrackerModel.owBombDroppers.[i,j] <- true
                            TrackerModel.forceUpdate()
                            redrawGridSpot()
                        else
                            if x.Delta<0 then
                                TrackerModel.nextOverworldMapCircleColor(i,j)
                            else
                                TrackerModel.prevOverworldMapCircleColor(i,j)
                            owCircleRedraws.[i,j]()
                    else
                        activatePopup(if x.Delta<0 then 1 else -1)
                )
            c.MouseEnter.Add(fun _ -> let z = owInstance.HintZoneOf(i,j) in if z <> 0 then hintButtonHighlightHook z true)
            c.MouseLeave.Add(fun _ -> let z = owInstance.HintZoneOf(i,j) in if z <> 0 then hintButtonHighlightHook z false)
            // 'Hover shop tooltip' option: hovering a shop tile shows what is in that shop (same size as the Dead Woods hint hover box)
            c.MouseEnter.Add(fun _ ->
                let label = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[i,j])
                if TrackerModelOptions.HoverShopTooltip.Value && label > 0 && not popupIsActive then
                    let items = shopItemBitmapsGlobal (if label = 5 then 0 else label)
                    let sp = new StackPanel(Orientation=Orientation.Vertical, Margin=Thickness(4.), VerticalAlignment=VerticalAlignment.Center)
                    let iconSize = (if label = 5 || items.Length <= 3 then 48. else 36.)
                    sp.Children.Add(new TextBlock(Text=(if label = 5 then "P SHOP" else sprintf "SHOP %d" label), FontSize=22., FontWeight=FontWeights.Bold, Foreground=Brushes.Orange, HorizontalAlignment=HorizontalAlignment.Center)) |> ignore
                    let wp = new WrapPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(0.,6.,0.,0.))
                    let addImg(bmp:System.Drawing.Bitmap, collected:bool) =
                        let img = Graphics.BMPtoImage(bmp)
                        img.Stretch <- Stretch.Uniform
                        img.Width <- iconSize
                        img.Height <- iconSize
                        img.Margin <- Thickness(2.)
                        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor)
                        if collected then img.Opacity <- 0.4
                        wp.Children.Add(img) |> ignore
                    if label = 5 then
                        addImg(decorSourceBmp TrackerModel.ITEMS.BLUEPOTION, false)
                        (match items with
                         | h :: _ -> addImg h
                         | [] -> wp.Children.Add(new TextBlock(Text="?", FontSize=40., FontWeight=FontWeights.Bold, Foreground=Brushes.White, Width=iconSize + 4., TextAlignment=TextAlignment.Center)) |> ignore)
                        addImg(decorSourceBmp TrackerModel.ITEMS.REDPOTION, false)
                    else
                        for it in items do addImg it
                    sp.Children.Add(wp) |> ignore
                    let big = new Border(Child=sp, Background=Brushes.Black, BorderBrush=Brushes.DarkGray, BorderThickness=Thickness(3.), Width=200., Height=112., IsHitTestVisible=false)
                    let ctr = centerOf(i,j)
                    let x = max 0. (min (ctr.X - 100.) (cm.AppMainCanvas.Width - 202.))
                    let y = if ctr.Y + 20. + 112. < cm.AppMainCanvas.Height then ctr.Y + 20. else max 0. (ctr.Y - 20. - 112.)
                    layout.ClearTopLayerHovers()
                    layout.AddTopLayerHover(big, x, y))
            c.MouseLeave.Add(fun _ -> if TrackerModelOptions.HoverShopTooltip.Value then layout.ClearTopLayerHovers())
            c.MyKeyAdd(fun ea ->
                if not popupIsActive then
                    match HotKeys.OverworldHotKeyProcessor.TryGetValue(ea.Key) with
                    | Some(hotKeyedState) when (hotKeyedState = TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP || TrackerModel.MapSquareChoiceDomainHelper.IsItem(hotKeyedState)) && (TrackerModel.overworldMapMarks.[i,j].Current() = TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY || tookCaveItem(TrackerModel.overworldMapMarks.[i,j].Current()) || isDungeonOrAnyRoad(TrackerModel.overworldMapMarks.[i,j].Current())) ->
                        ea.Handled <- true
                        Graphics.ErrorBeepWithReminderLogText "This cave can only have the bomb dropper marker"
                    | Some(hotKeyedState) when hotKeyedState = TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP ->
                        // the Overworld_Shop hotkey opens the Shop flyout (potion shop / shops 1-4); its own hotkeys (Contextual_Shop_...) then pick one
                        ea.Handled <- true
                        openShopFlyoutDirectly <- true
                        activatePopup(0)
                    | Some(hotKeyedState) when hotKeyedState = TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP && not(canPlacePotionShop(i,j)) ->
                        ea.Handled <- true
                        Graphics.ErrorBeepWithReminderLogText "All the potion shops are already placed"
                    | Some(hotKeyedState) when hotKeyedState = TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP || TrackerModel.MapSquareChoiceDomainHelper.IsItem(hotKeyedState) ->
                        // the shop hotkeys place the new marker icons, exactly like picking the item from the tile menu (the potion shop is the blue + red potion pair)
                        let shopIdent =
                            if hotKeyedState = TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP then TrackerModel.ITEMS.BLUEPOTION
                            elif hotKeyedState = TrackerModel.MapSquareChoiceDomainHelper.ARROW then TrackerModel.ITEMS.WOODARROW
                            elif hotKeyedState = TrackerModel.MapSquareChoiceDomainHelper.BOMB then TrackerModel.DECOR_BOMB
                            elif hotKeyedState = TrackerModel.MapSquareChoiceDomainHelper.BOOK then TrackerModel.ITEMS.BOOKSHIELD
                            elif hotKeyedState = TrackerModel.MapSquareChoiceDomainHelper.BLUE_CANDLE then TrackerModel.ITEMS.BLUECANDLE
                            elif hotKeyedState = TrackerModel.MapSquareChoiceDomainHelper.BLUE_RING then TrackerModel.ITEMS.BLUERING
                            elif hotKeyedState = TrackerModel.MapSquareChoiceDomainHelper.MEAT then TrackerModel.ITEMS.MEAT
                            elif hotKeyedState = TrackerModel.MapSquareChoiceDomainHelper.KEY then TrackerModel.ITEMS.OLDKEY
                            else TrackerModel.ITEMS.SHIELD
                        popupIsActive <- true
                        ea.Handled <- true
                        async {
                            let origState = TrackerModel.overworldMapMarks.[i,j].Current()
                            if shopIdent = TrackerModel.DECOR_BOMB && TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[i,j]) = 0 && (origState = -1 || origState = TrackerModel.MapSquareChoiceDomainHelper.DARK_X)
                                    && not(owInstance.AlwaysEmpty(i,j)) && TrackerModel.mapSquareChoiceDomain.CanAddUse(TrackerModel.MapSquareChoiceDomainHelper.BOMB) then
                                // like the tile menu: a bomb marker places the bomb shop tile (so it shows the yellow 'found but not collected' bomb)
                                do! SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.BOMB, origState)
                                redrawGridSpot()
                            elif shopIdent = TrackerModel.DECOR_BOMB && origState = TrackerModel.MapSquareChoiceDomainHelper.BOMB then
                                // pressing the bomb hotkey again on a bomb tile takes the bomb back off
                                do! SetNewValue((if TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]).IsEmpty then -1 else TrackerModel.MapSquareChoiceDomainHelper.DARK_X), origState)
                                redrawGridSpot()
                            else
                                let before = TrackerModel.owSwordIcons.[i,j]
                                setDecorIcons(i, j, TrackerModel.decorIconsToggle(before, shopIdent))
                                if TrackerModel.owSwordIcons.[i,j] = before then
                                    Graphics.ErrorBeepWithReminderLogText(sprintf "A tile can show at most %d marker icons; ctrl+left click one of its icons to remove it first" TrackerModel.MaxDecorIcons)
                                if not(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]).IsEmpty) && not(owInstance.AlwaysEmpty(i,j)) && TrackerModel.overworldMapMarks.[i,j].Current() = -1 then
                                    do! SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.DARK_X, -1)
                            popupIsActive <- false
                        } |> Async.StartImmediate
                    | Some(hotKeyedState) -> 
                        popupIsActive <- true
                        ea.Handled <- true
                        let originalState = TrackerModel.overworldMapMarks.[i,j].Current()
                        let state = OverworldMapTileCustomization.DoSpecialHotKeyHandlingForOverworldTiles(i, j, originalState, hotKeyedState)
                        async {
                            do! SetNewValue(state, originalState)
                            popupIsActive <- false
                        } |> Async.StartImmediate
                    | None -> 
                        // hotkey for a dungeon-item marker icon: toggles it on this tile, exactly like picking it from the tile menu
                        // (Item_X hotkeys work here too; the bomb marker has its own OverworldMarker_Bomb entry)
                        let markerIdent =
                            match HotKeys.OverworldMarkerHotKeyProcessor.TryGetValue(ea.Key) with
                            | Some(ident) -> Some(ident)
                            | None ->
                                match HotKeys.ItemHotKeyProcessor.TryGetValue(ea.Key) with
                                | Some(item) when HotKeys.OverworldMarkerItems |> Array.contains item -> Some(item)
                                | _ -> None
                        match markerIdent with
                        | Some(ident) when ident = HotKeys.CLEAR_TILE_HOTKEY ->
                            // 'Overworld_ClearTile': the same as the red X in the tile menu - every marker icon and whatever was placed or taken there is cleared, and the tile is unmarked again
                            popupIsActive <- true
                            ea.Handled <- true
                            async {
                                let originalState = TrackerModel.overworldMapMarks.[i,j].Current()
                                clearAllTileIcons(i, j)
                                if OverworldMapTileCustomization.isSwordCaveState(originalState) then
                                    OverworldMapTileCustomization.clearSwordCaveItemsForState(i, j, originalState)
                                    UIComponents.itemProgressChanged()
                                if originalState <> -1 && (not(owInstance.AlwaysEmpty(i,j)) || originalState <> TrackerModel.MapSquareChoiceDomainHelper.DARK_X) then
                                    do! SetNewValue(-1, originalState)
                                redrawGridSpot()
                                popupIsActive <- false
                            } |> Async.StartImmediate
                        | Some(_) when isDungeonOrAnyRoad(TrackerModel.overworldMapMarks.[i,j].Current()) ->
                            ea.Handled <- true
                            Graphics.ErrorBeepWithReminderLogText "A dungeon entrance or any road can only have the bomb dropper marker"
                        | Some(ident) ->
                            popupIsActive <- true
                            ea.Handled <- true
                            async {
                                let before = TrackerModel.owSwordIcons.[i,j]
                                setDecorIcons(i, j, decorToggleForTile(before, ident))
                                if TrackerModel.owSwordIcons.[i,j] = before then
                                    Graphics.ErrorBeepWithReminderLogText(sprintf "A tile can show at most %d marker icons; ctrl+left click one of its icons to remove it first" TrackerModel.MaxDecorIcons)
                                if not(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]).IsEmpty) && not(owInstance.AlwaysEmpty(i,j)) && TrackerModel.overworldMapMarks.[i,j].Current() = -1 then
                                    do! SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.DARK_X, -1)
                                popupIsActive <- false
                            } |> Async.StartImmediate
                        | None -> ()
                )
            if Graphics.shouldInitiallyHideOverworldMap && not isPermEmpty then
                TrackerModel.overworldMapMarks.[i,j].Set(TrackerModel.MapSquareChoiceDomainHelper.DARK_X)
                TrackerModel.setOverworldMapExtraData(i,j,TrackerModel.MapSquareChoiceDomainHelper.DARK_X,TrackerModel.MapSquareChoiceDomainHelper.DARK_X)
                redrawGridSpot()
            elif TrackerModel.overworldMapMarks.[i,j].Current() = TrackerModel.MapSquareChoiceDomainHelper.DARK_X then
                redrawGridSpot()  // quest-switchable tile that starts as AlwaysEmpty in the current quest; draw its initial dark X
    if speechRecognitionInstance <> null then
        speechRecognitionInstance.AttachSpeechRecognizedToApp(appMainCanvas, (fun recognizedText ->
                                if currentlyMousedOWX >= 0 then // can hear speech before we have moused over any (uninitialized location)
                                    let c = owCanvases.[currentlyMousedOWX,currentlyMousedOWY]
                                    if c <> null && c.IsMouseOver then  // canvas can be null for always-empty grid places
                                        // Note: IsMouseOver appears only be true if we are along the way from a mouse click to the visual root.
                                        // As a result, the update never fires when a popup window is active, because the popup sunglasses would always 
                                        // intercept the click in a different part of the Visual tree.  This is good, because we don't want speech 
                                        // mutating the world while a popup is active (the popup's modality should block speech).
                                        // I guess we could also ask 'cm' if a popup is active.
                                        owUpdateFunctions.[currentlyMousedOWX,currentlyMousedOWY] 777 recognizedText
                            ))
    OptionsMenu.requestRedrawOverworldEvent.Publish.Add(fun _ ->
        for i = 0 to 15 do
            for j = 0 to 7 do
                owUpdateFunctions.[i,j] 0 null  // redraw tile
        )
    canvasAdd(overworldCanvas, owMapGrid, 0., 0.)
    ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes <- (fun () ->
//        printfn "eROWGSAOCCB"
        let showGettables = owGettableScreensCheckBox.IsChecked.HasValue && owGettableScreensCheckBox.IsChecked.Value
        OverworldRouteDrawing.drawPathsImpl(TrackerModel.mapStateSummary.OwRouteworthySpots, 
                    TrackerModel.overworldMapMarks |> Array2D.map (fun cell -> cell.Current() = -1), Point(0.,0.), 0, 0, false, true, 0, showGettables, whetherToCyanOpenCavesOrArmos())
        )
            
    do! showProgress("overworld finish, legend")

    let recorderingCanvas = new Canvas(Width=16.*OMTW, Height=float(8*11*3))  // really the 'extra top layer' canvas for adding final marks to overworld map
    recorderingCanvas.IsHitTestVisible <- false  // do not let this layer see/absorb mouse interactions
    canvasAdd(overworldCanvas, recorderingCanvas, 0., 0.)
    
    canvasAdd(overworldCanvas, overworldCirclesCanvas, 0., 0.)

    // legend
    let recorderDestinationButtonCanvas, anyRoadLegendIcon, dungeonLegendIconArea, updateCurrentRecorderDestinationNumeral, legendCanvas, startIcon, theCustomWaypointIcon = 
            UIComponents.MakeLegend(cm, doUIUpdateEvent)
    layout.AddLegend(legendCanvas)
    // 1Q/2Q button: sits right beside the Start Spot box in the legend row, built exactly like the Start Spot button
    // (a default Button around a 29px-high black canvas) so height and border match; it forwards its click to the real 1Q/2Q button
    if isStandardHyrule && moreFQSQoptionsButton <> null then
        // (the 1Q/2Q button now lives in the control panel at the top, under the OVERWORLD OPTIONS heading; it forwards its click to the real 1Q/2Q button)
        let fqsqPanelButton = Graphics.makeButton("1Q / 2Q", Some(11.), Some(Brushes.Orange))
        fqsqPanelButton.Click.Add(fun _ -> moreFQSQoptionsButton.RaiseEvent(new RoutedEventArgs(Primitives.ButtonBase.ClickEvent)))
        layout.AddOverworldOptionsButton(fqsqPanelButton)
        // ZORA Hints: Lost Woods / Lost Hills direction sequences, in the open legend space right of the 1Q/2Q button
        let makeHintButton(name:string, prefix:string) =
            // built like the 1Q/2Q and Start Spot buttons (plain Button, pure-black borderless TextBox content that is not stretched), so the border looks identical
            let tb = new TextBox(Text=name, FontSize=10., Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, 
                                    BorderThickness=Thickness(0.), Padding=Thickness(2.,0.,2.,0.), TextAlignment=TextAlignment.Center, VerticalContentAlignment=VerticalAlignment.Center)
            let btn = new Button(Content=tb, Margin=Thickness(0.), Width=OMTW*1.45+2., Height=31., HorizontalContentAlignment=HorizontalAlignment.Stretch, VerticalContentAlignment=VerticalAlignment.Stretch)   // (side by side now, as tall as the Start Spot / 1Q-2Q buttons)
            Graphics.styleRoundedButton(btn)   // two of these stacked (32px) fit inside the 33px legend row
            let hint = ref ""
            let defaultBorderBrush = btn.BorderBrush
            let defaultBorderThickness = btn.BorderThickness
            let refreshLabel() =
                tb.Text <- (if hint.Value = "" then name else hint.Value)   // once entered: just the letters, bigger
                tb.FontSize <- (if hint.Value = "" then 10. else 20.)
                tb.FontWeight <- (if hint.Value = "" then FontWeights.Normal else FontWeights.Bold)
                tb.HorizontalAlignment <- HorizontalAlignment.Stretch   // (fills the button, so its black background doesn't show as a box around the text)
                tb.VerticalAlignment <- VerticalAlignment.Stretch
                tb.VerticalContentAlignment <- VerticalAlignment.Center
                tb.Padding <- (if hint.Value = "" then Thickness(2.,0.,2.,0.) else Thickness(0.))
                tb.TextAlignment <- TextAlignment.Center
            btn.Click.Add(fun _ ->
                if not popupIsActive then
                    popupIsActive <- true
                    let pos = btn.TranslatePoint(Point(0., btn.ActualHeight + 4.), cm.AppMainCanvas)
                    async {
                        let wh = new System.Threading.ManualResetEvent(false)
                        let cur = ref hint.Value
                        let sp = new StackPanel(Orientation=Orientation.Vertical, Margin=Thickness(3.))
                        let mkTxt(size:float, txt) = new TextBox(IsHitTestVisible=false, BorderThickness=Thickness(0.), FontSize=size, Margin=Thickness(3.), Text=txt, Foreground=Brushes.Orange, Background=Brushes.Black, TextAlignment=TextAlignment.Center)
                        let hoverToggle = PieMenus.makeOrangeCheckBox("Hover tooltip", TrackerModelOptions.HintHoverTooltip.Value)
                        hoverToggle.HorizontalAlignment <- HorizontalAlignment.Center
                        hoverToggle.Click.Add(fun _ ->
                            TrackerModelOptions.HintHoverTooltip.Value <- (hoverToggle.IsChecked = System.Nullable<bool>(true))
                            TrackerModelOptions.writeSettings())
                        sp.Children.Add(hoverToggle) |> ignore
                        sp.Children.Add(mkTxt(16., name + " hint")) |> ignore
                        sp.Children.Add(mkTxt(12., "Select direction or use arrow keys")) |> ignore
                        let display = mkTxt(20., "")
                        let updateDisplay() = display.Text <- (if cur.Value = "" then "-" else System.String.Join(" ", cur.Value.ToCharArray() |> Array.map string))
                        updateDisplay()
                        sp.Children.Add(display) |> ignore
                        let add(c:string) = 
                            if cur.Value.Length < 4 then
                                cur.Value <- cur.Value + c
                                updateDisplay()
                                if cur.Value.Length >= 4 then wh.Set() |> ignore   // 4 directions entered = done
                        let backspace() =
                            if cur.Value.Length > 0 then
                                cur.Value <- cur.Value.Substring(0, cur.Value.Length-1)
                                updateDisplay()
                        let dirs = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(0.,4.,0.,2.))
                        // Lost Hills hints are up/down/left/right; Dead Woods hints are compass directions
                        let isDeadWoods = (name = "Dead Woods")
                        let kU, kD, kL, kR = (if isDeadWoods then "N","S","W","E" else "U","D","L","R")
                        for c in (if isDeadWoods then ["N"; "E"; "S"; "W"] else ["U"; "D"; "L"; "R"]) do
                            let db = new Button(Content=c, Width=36., Height=32., FontSize=16., Margin=Thickness(4.,0.,4.,0.), Focusable=false)
                            db.Click.Add(fun _ -> add c)
                            dirs.Children.Add(db) |> ignore
                        sp.Children.Add(dirs) |> ignore
                        let buttons = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(0.,4.,0.,2.))
                        let backBtn = new Button(Content=" Back ", Margin=Thickness(4.,0.,4.,0.), Focusable=false)
                        let clearBtn = new Button(Content=" Clear ", Margin=Thickness(4.,0.,4.,0.), Focusable=false)
                        let doneBtn = new Button(Content=" Done ", Margin=Thickness(12.,0.,4.,0.), Focusable=false)
                        backBtn.Click.Add(fun _ -> backspace())
                        clearBtn.Click.Add(fun _ -> cur.Value <- ""; updateDisplay())
                        doneBtn.Click.Add(fun _ -> wh.Set() |> ignore)
                        buttons.Children.Add(backBtn) |> ignore
                        buttons.Children.Add(clearBtn) |> ignore
                        buttons.Children.Add(doneBtn) |> ignore
                        sp.Children.Add(buttons) |> ignore
                        let border = new Border(Child=sp, Background=Brushes.Black, BorderThickness=Thickness(4.), BorderBrush=Brushes.Gray, Focusable=true)
                        border.PreviewKeyDown.Add(fun ea ->
                            if ea.Key = Input.Key.Up then 
                                ea.Handled <- true
                                add kU
                            elif ea.Key = Input.Key.Down then 
                                ea.Handled <- true
                                add kD
                            elif ea.Key = Input.Key.Left then 
                                ea.Handled <- true
                                add kL
                            elif ea.Key = Input.Key.Right then 
                                ea.Handled <- true
                                add kR
                            elif ea.Key = Input.Key.Back then 
                                ea.Handled <- true
                                backspace()
                            elif ea.Key = Input.Key.Enter then 
                                ea.Handled <- true
                                wh.Set() |> ignore)
                        border.Loaded.Add(fun _ -> border.Focus() |> ignore)
                        do! CustomComboBoxes.DoModal(cm, wh, pos.X, pos.Y, border)
                        hint.Value <- cur.Value
                        refreshLabel()
                        if name = "Dead Woods" then OverworldData.DeadWoodsHintKnown <- (hint.Value <> "") else OverworldData.LostHillsHintKnown <- (hint.Value <> "")
                        // once a hint is entered the button gets a green border
                        btn.BorderBrush <- (if hint.Value <> "" then (Brushes.LimeGreen :> Brush) else defaultBorderBrush)
                        btn.BorderThickness <- (if hint.Value <> "" then Thickness(2.) else defaultBorderThickness)
                        TrackerModel.forceUpdate()
                        popupIsActive <- false
                        doUIUpdateEvent.Trigger()
                    } |> Async.StartImmediate)
            // once a hint has been entered, hovering the button shows it in a much larger popup so it is easy to read at a glance
            btn.MouseEnter.Add(fun _ ->
                if hint.Value <> "" && not popupIsActive && TrackerModelOptions.HintHoverTooltip.Value then
                    let sp = new StackPanel(Orientation=Orientation.Vertical, Margin=Thickness(12.,6.,12.,6.))
                    sp.Children.Add(new TextBlock(Text=name, FontSize=27., Foreground=Brushes.Orange, HorizontalAlignment=HorizontalAlignment.Center)) |> ignore
                    sp.Children.Add(new TextBlock(Text=System.String.Join("  ", hint.Value.ToCharArray() |> Array.map string), FontSize=45., FontWeight=FontWeights.Bold, 
                                                    Foreground=Brushes.Orange, HorizontalAlignment=HorizontalAlignment.Center)) |> ignore
                    let big = new Border(Child=sp, Background=Brushes.Black, BorderThickness=Thickness(3.), BorderBrush=Brushes.Gray, IsHitTestVisible=false)
                    let pos = btn.TranslatePoint(Point(0., btn.ActualHeight + 4.), cm.AppMainCanvas)
                    layout.ClearTopLayerHovers()
                    layout.AddTopLayerHover(big, pos.X, pos.Y)
                )
            btn.MouseLeave.Add(fun _ -> layout.ClearTopLayerHovers())
            // right click does the same as left click (opens the hint entry)
            btn.MouseRightButtonUp.Add(fun ea ->
                ea.Handled <- true
                btn.RaiseEvent(new RoutedEventArgs(Primitives.ButtonBase.ClickEvent)))
            btn
        // the two hint buttons are stacked, no heading
        let hintButtons = new StackPanel(Orientation=Orientation.Horizontal)
        let deadWoodsButton = makeHintButton("Dead Woods", "DW")
        hintButtons.Children.Add(deadWoodsButton) |> ignore
        let lostHillsButton = makeHintButton("Lost Hills", "LH")
        let hintBtnNormalBrush = lostHillsButton.BorderBrush
        let hintBtnNormalThickness = lostHillsButton.BorderThickness
        hintButtonHighlightHook <- (fun zone on ->
            // a bright red glow, a thick red border, and a red-filled label, so it is obvious which button the hovered tile is waiting on
            let b = if zone = 1 then deadWoodsButton else lostHillsButton
            let label = b.Content :?> TextBox
            if on then
                b.BorderBrush <- (Brushes.Red :> Brush)
                b.BorderThickness <- Thickness(3.)
                b.Effect <- new System.Windows.Media.Effects.DropShadowEffect(Color=Colors.Red, BlurRadius=18., ShadowDepth=0., Opacity=1.)
                label.Background <- (Brushes.Red :> Brush)
                label.Foreground <- (Brushes.White :> Brush)
                label.FontWeight <- FontWeights.Bold
            else
                let known = (if zone = 1 then OverworldData.DeadWoodsHintKnown else OverworldData.LostHillsHintKnown)
                b.BorderBrush <- (if known then (Brushes.LimeGreen :> Brush) else hintBtnNormalBrush)   // (a button whose hint has been entered keeps its green border)
                b.BorderThickness <- (if known then Thickness(2.) else hintBtnNormalThickness)
                b.Effect <- null
                label.Background <- (Brushes.Black :> Brush)
                label.Foreground <- (Brushes.Orange :> Brush)
                label.FontWeight <- FontWeights.Normal)
        lostHillsButton.Margin <- Thickness(4.,0.,0.,0.)
        hintButtons.Children.Add(lostHillsButton) |> ignore
        canvasAdd(legendCanvas, hintButtons, UIComponents.legendAfterStartX + 28. + 8., 0.)
        let placeHints() =
            Canvas.SetTop(hintButtons, 4.)   // side by side, where the 1Q/2Q button used to be (just right of the Start Spot box); 4px down like the Start Spot box
            Canvas.SetLeft(hintButtons, UIComponents.legendAfterStartX)
        hintButtons.Loaded.Add(fun _ -> placeHints())
        placeHints()
    // 'Starting items and extra drops' icon: moved from the dungeon column grid to just right of the last item (meat) in the item progress row
    let extrasImageOldParent = extrasImage.Parent :?> Panel
    if extrasImageOldParent <> null then
        extrasImageOldParent.Children.Remove(extrasImage) |> ignore
    canvasAdd(legendCanvas, extrasImage, ITEM_PROGRESS_FIRST_ITEM + 14.*30. - 4., 36.)   // nudged 3px left
    extrasImage.Visibility <- Visibility.Collapsed   // the 'Starting items and extra drops' button is gone: starting items are now tracked by clicking the item progress bar
    // the meat icon is the last item in the progress row: its top is 3px into the row's canvas, which is itself 3px below the legend row
    let meatBottomY = THRU_MAP_AND_LEGEND_H + 3. + 3. + float Graphics.bait_bmp.Height   // absolute y of the bottom of the meat icon
    let redrawItemProgressBar, itemProgressCanvas, itemProgressTB = UIComponents.MakeItemProgressBar(owInstance)
    UIComponents.uncollectMapIcons <- (fun idents ->
        for i = 0 to 15 do
            for j = 0 to 7 do
                let packed = TrackerModel.owSwordIcons.[i,j]
                let entries = TrackerModel.decorEntriesOf packed
                if entries |> List.exists (fun (x,c) -> c && List.contains x idents) then
                    setDecorIcons(i, j, TrackerModel.decorEntriesPack (entries |> List.map (fun (x,c) -> (x, c && not(List.contains x idents))))))
    UIComponents.itemProgressChanged <- (fun () ->
        // recompute the player state right away (instead of waiting for the periodic poll) so the icon changes instantly, then do the full refresh
        if not (TrackerModel.recomputeWhatIsNeeded()) then TrackerModel.recomputePlayerStateSummary()
        redrawItemProgressBar()
        doUIUpdateEvent.Trigger())
    let itemProgressHoverTarget =
        let dp = new DockPanel(Background=Brushes.Transparent)  // Background to get mouse events
        dp.Children.Add(itemProgressTB) |> ignore
        dp.MouseEnter.Add(fun _ -> 
            spotSummaryCanvas.Children.Clear()
            itemProgressTB.BorderBrush <- Brushes.DarkTurquoise   // (click it for the remaining-items popout; no hover box)
            )
        dp.MouseLeave.Add(fun _ -> itemProgressTB.BorderBrush <- Graphics.roundedBorderBrush; spotSummaryCanvas.Children.Clear())
        dp.MouseDown.Add(fun ea -> Popouts.makePopout(Popouts.RemainingItemsPoput, not(ea.ChangedButton = Input.MouseButton.Right), ea.ChangedButton = Input.MouseButton.Right, doUIUpdateEvent))
        dp
    layout.AddItemProgress(itemProgressCanvas, itemProgressHoverTarget)

    
    // Version
    let vb = CustomComboBoxes.makeVersionButtonWithBehavior(cm)
    blackenButton(vb)
    // vb.Click.Add(fun _ -> failwith "crash")                     // Uncomment this for crash testing
    layout.AddVersionButton(vb)

    // hint decoder
    if isStandardHyrule then   // Hints only apply to z1r and standard map zones
        let hintDecoderUI = UIComponents.MakeHintDecoderUI(cm)
        blackenButton(hintDecoderUI)
        layout.AddHintDecoderButton(hintDecoderUI)

    // WANT!
#if NOT_RACE_LEGAL
    let kittyBoxWidth = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H - THRU_MAIN_MAP_H   // the space (63x63) the old picture occupied; neighboring buttons are positioned relative to it
#endif
    let kitty = new Image()   // the custom logo
    let imageStream = Graphics.GetResourceStream("CustomLogo.png")
    kitty.Source <- System.Windows.Media.Imaging.BitmapFrame.Create(imageStream)
    kitty.Width <- 30.     // leaves room for the two ZORA link lines underneath
    kitty.Height <- 30.
    let ztlogo = new Image()
    let imageStream = Graphics.GetResourceStream("ZTlogo64x64.png")
    ztlogo.Source <- System.Windows.Media.Imaging.BitmapFrame.Create(imageStream)
    ztlogo.Width <- 20.
    ztlogo.Height <- 20.
    let logoBorder = new Border(BorderThickness=Thickness(1.), BorderBrush=Brushes.Gray, Child=ztlogo)
    let discordUrl = "https://discord.gg/zescSjDtme"
    let discordLink = Layout.MakeDiscordLink(discordUrl)
    let racingLink = Layout.MakeLinkText("Z1R Racing Discord", "https://discord.gg/z1rracing", Layout.RacingLinkWidth)
    layout.AddKittyAndLogo(kitty, logoBorder, ztlogo, discordLink, racingLink)
    let zoraSiteLink = Layout.MakeLinkText("ZORA Site", "https://zora.gg/", Layout.DiscordLinkWidth)
    let racingSiteLink = Layout.MakeLinkText("Z1R Racing Site", "https://z1rracing.com/", Layout.RacingLinkWidth)
    layout.AddExtraSiteLinks(zoraSiteLink, racingSiteLink)
    (discordLink :?> FrameworkElement).SizeChanged.Add(fun _ -> 
        let d = discordLink :?> FrameworkElement
        if d.ActualHeight > 0. then 
            Canvas.SetTop(d, meatBottomY - d.ActualHeight)   // bottom-align with the meat icon
            let r = racingLink :?> FrameworkElement
            Canvas.SetTop(r, meatBottomY - d.ActualHeight - (if r.ActualHeight > 0. then r.ActualHeight else 14.))   // the Z1R Racing Discord link sits directly above it
            Canvas.SetLeft(r, Canvas.GetLeft(d) + (Layout.DiscordLinkWidth - Layout.RacingLinkWidth) / 2.))

    // triforce decoder chart, left of the ZORA logo: 1Q by default, 2Q for Second Quest / Mixed Second Quest overworlds
    // (follows the start screen choice and the 1Q/2Q button, since both go through TrackerModel.owInstance)
    let questRefIsSecond() = 
        TrackerModel.owInstance.Quest = OverworldData.OWQuest.SECOND || TrackerModel.owInstance.Quest = OverworldData.OWQuest.MIXED_SECOND
    let makeQuestRefChart() : UIElement =
        let haves = TrackerModel.GetTriforceHaves()
        if questRefIsSecond() then Dungeon.DrawTriforces2Q(haves) :> UIElement else Dungeon.DrawTriforces1Q(haves) :> UIElement
    let _questRefLogoX = 16.*OMTW - Layout.kittyWidth - 12. + (Layout.kittyWidth - kitty.Width)/2.
    let questRefH0 = Layout.kittyWidth - 4.   // the original height
    let questRefH = questRefH0   // the chart itself is its original size; only the hover window is larger
    let questRefViewbox = new Viewbox(Stretch=Stretch.Uniform, Height=questRefH)
    let questRefBorder = new Border(BorderThickness=Thickness(0.), Background=Brushes.Transparent, Child=questRefViewbox)
    questRefBorder.Height <- questRefH
    questRefBorder.Width <- questRefH * 340. / 180.
    let updateQuestRef() =
        let chart = makeQuestRefChart()
        (chart :?> Canvas).Background <- Brushes.Transparent   // transparent outside the triforce shapes
        // clicking a triforce piece in the chart collects it (or un-collects it) like clicking that dungeon's triforce above
        let chartCanvas = chart :?> Canvas
        let labels = if questRefIsSecond() then "13254687" else "12345678"
        for k = 0 to 7 do
            if chartCanvas.Children.Count > 2*k then
                let poly = chartCanvas.Children.[2*k]
                poly.MouseLeftButtonDown.Add(fun ea ->
                    ea.Handled <- true
                    if not popupIsActive then
                        let dungeonIndex =
                            if TrackerModel.IsHiddenDungeonNumbers() then
                                [0..7] |> List.tryFind (fun d -> TrackerModel.GetDungeon(d).LabelChar = labels.[k])
                            else Some(int labels.[k] - int '1')
                        match dungeonIndex with
                        | Some(d) when not Views.triforceBlocked.[d] -> TrackerModel.GetDungeon(d).ToggleTriforce()
                        | _ -> ())
        questRefViewbox.Child <- chart
    updateQuestRef()
    let lastQuestRefKey = ref ""
    // hovering the chart shows it at 2x size (current state), above the chart, kept inside the window
    // (only while the mouse is inside the big outer triangle of the chart: the 340x180 drawing's apex is at the top center and its base line is at y=171)
    let questRefHoverShown = ref false
    let mouseInsideQuestRefTriangle(p:Point) =
        let s = questRefH / 180.
        let x, y = p.X / s, p.Y / s
        y >= 1. && y <= 172. && abs (x - 170.) <= (y - 1.) + 1.
    let showQuestRefHover() =
        let bigH = questRefH * 3.   // hover window: 50% bigger than the original 2x
        let bigViewbox = new Viewbox(Stretch=Stretch.Uniform, Height=bigH, Child=makeQuestRefChart())
        let big = new Border(Child=bigViewbox, Background=Brushes.Black, BorderThickness=Thickness(3.), BorderBrush=Brushes.Gray, IsHitTestVisible=false)
        let w, h = bigH * 340. / 180. + 6., bigH + 6.
        let pos = questRefBorder.TranslatePoint(Point(0., 0.), cm.AppMainCanvas)
        let x = max 0. (min pos.X (cm.AppMainCanvas.Width - w - 2.))
        let y = max 0. (pos.Y - h - 4.)
        layout.ClearTopLayerHovers()
        layout.AddTopLayerHover(big, x, y)
    questRefBorder.MouseMove.Add(fun ea ->
        let inside = mouseInsideQuestRefTriangle(ea.GetPosition(questRefBorder))
        if inside && not questRefHoverShown.Value then
            questRefHoverShown.Value <- true
            showQuestRefHover()
        elif not inside && questRefHoverShown.Value then
            questRefHoverShown.Value <- false
            layout.ClearTopLayerHovers())
    questRefBorder.MouseLeave.Add(fun _ ->
        if questRefHoverShown.Value then
            questRefHoverShown.Value <- false
            layout.ClearTopLayerHovers())
    TrackerModel.overworldQuestChanged.Publish.Add(fun _ -> updateQuestRef())
    for i = 0 to 7 do
        TrackerModel.GetDungeon(i).PlayerHasTriforceChanged.Add(fun _ -> updateQuestRef())
        // a triforce piece marked as obtained in a dungeon item box also lights its piece in the chart
        for b in TrackerModel.GetDungeon(i).Boxes do
            b.Changed.Add(fun _ -> updateQuestRef())
    layout.AddQuestReferenceChart(questRefBorder, BLOCKERS_AND_NOTES_OFFSET + 108. + 12. - 20. - 9. * questRefH0 / 180. - (questRefH - questRefH0) * 340. / 180., meatBottomY - questRefH + 3.)   // the chart's bottom-left corner (drawn 9/180 of its height in from its edge) lines up with the left side of the first blocker box (blocker column is 108 wide, then a 12px dungeon-number label)   // bottom-aligned with the meat icon
    // the blue 3-dot box's bottom edge is lined up with the drawn base line of the triforce chart, measured from the real on-screen positions
    // (the chart is a 340x180 drawing whose base line, 2px stroke included, is at y=171, scaled to fit questRefH)
    let alignExtrasWithChart() =
        try
            if extrasImage.ActualHeight > 0. && questRefBorder.ActualHeight > 0. && questRefBorder.IsLoaded && extrasImage.IsLoaded then
                let chartTopInLegend = questRefBorder.TranslatePoint(Point(0., 0.), legendCanvas).Y
                Canvas.SetTop(extrasImage, chartTopInLegend + questRefH * 171. / 180. - extrasImage.ActualHeight + 3.)   // +3: the icon image has a little transparent margin below the visible blue box
        with _ -> ()
    extrasImage.SizeChanged.Add(fun _ -> alignExtrasWithChart())
    questRefBorder.SizeChanged.Add(fun _ -> alignExtrasWithChart())
    extrasImage.Loaded.Add(fun _ -> alignExtrasWithChart())
    questRefBorder.Loaded.Add(fun _ -> alignExtrasWithChart())
    

    // show hotkeys button
    let showHotKeysButton = Graphics.makeButton("Hotkeys", Some(12.), Some(Brushes.Orange))   // same style as the Save and Hint Decoder buttons
    blackenButton(showHotKeysButton)
    layout.AddShowHotKeysButton(showHotKeysButton)
    showHotKeysButton.Click.Add(fun _ -> Popouts.showHotKeys(false))
    showHotKeysButton.MouseRightButtonDown.Add(fun _ -> Popouts.showHotKeys(true))

    // show/run custom button
    let showRunCustomButton = Graphics.makeButton("Show/Run Custom", Some(16.), Some(Brushes.Orange))   // same style/size as Spot Summary and Pause/Reset timer
    blackenButton(showRunCustomButton)
    layout.AddShowRunCustomButton(showRunCustomButton)
    showRunCustomButton.Click.Add(fun _ -> ShowRunCustom.DoShowRunCustom(refocusMainWindow))

    let saveButton = Graphics.makeButton("Save", Some(12.), Some(Brushes.Orange))
    saveButton.Width <- 84.   // same style as the other main-screen buttons
    blackenButton(saveButton)
    saveButton.ToolTip <- "Save the current tracker state to a file"
    layout.AddSaveButton(saveButton)
    // stack (bottom to top): ZORA Discord link, Z1R Racing Discord link, centered on the discord links (Save now sits above the Hint Decoder button, placed in Layout)
    let placeLinkStack() =
        let d = discordLink :?> FrameworkElement
        let zs = zoraSiteLink :?> FrameworkElement
        if d.ActualHeight > 0. && zs.ActualHeight > 0. && not (System.Double.IsNaN(Canvas.GetLeft(d))) then
            let cx = Canvas.GetLeft(d) + Layout.DiscordLinkWidth / 2.
            // top to bottom: ZORA logo, ZORA Site, ZORA Discord; the stack ends at the bottom of the strip of space (the old logo area) to the right of the map
            let place(e:FrameworkElement, y:float) =
                Canvas.SetTop(e, y)
                Canvas.SetLeft(e, cx - e.ActualWidth / 2.)
            let bottom = THRU_MAIN_MAP_H + Layout.kittyWidth
            let dTop = bottom - d.ActualHeight
            let zTop = dTop - zs.ActualHeight
            place(zs, zTop)
            place(d, dTop)
            Canvas.SetTop(kitty, zTop - kitty.Height - 1.)
            Canvas.SetLeft(kitty, cx - kitty.Width / 2.)
    for e in [discordLink :?> FrameworkElement; zoraSiteLink :?> FrameworkElement] do
        e.SizeChanged.Add(fun _ -> placeLinkStack())
    saveButton.Click.Add(fun _ -> 
        if not popupIsActive then
            popupIsActive <- true
            async {
                try
                    let filename = MakeManualSave()
                    let filename = System.IO.Path.GetFileName(filename)  // remove directory info (could have username in path, don't display PII on-screen)
                    let! r = CustomComboBoxes.DoModalMessageBox(cm, System.Drawing.SystemIcons.Information, sprintf "ZORA Tracker data saved to file\n%s" filename, ["Ok"])
                    ignore r
                    popupIsActive <- false
                with e ->
                    let! r = CustomComboBoxes.DoModalMessageBox(cm, System.Drawing.SystemIcons.Error, sprintf "ZORA Tracker was unable to save the\ntracker state to a file\nError:\n%s" e.Message, ["Ok"])
                    ignore r
                    popupIsActive <- false
            } |> Async.StartImmediate
        )

    let uccTB = new TextBox(FontSize=12., Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, BorderThickness=Thickness(0.), 
                                        Text="UCC", IsHitTestVisible=false, TextAlignment=TextAlignment.Center)
    let uccButton = new Button(Content=uccTB)
    if System.IO.File.Exists(UserCustomLayer.checklistFilename) then
        layout.AddUserCustomContentButton(uccButton)
        uccButton.Click.Add(fun _ -> 
            if not popupIsActive then
                popupIsActive <- true
                async {
                    do! UserCustomLayer.InteractWithUserCustom(cm, timelineItems, invokeExtras)
                    popupIsActive <- false
                    } |> Async.StartImmediate
            )

#if NOT_RACE_LEGAL
    // minimap overlay button
    let minimapOverlayTB = new TextBox(FontSize=12., Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, BorderThickness=Thickness(0.), Text="Minimap overlay", IsHitTestVisible=false)
    let minimapOverlayButton = new Button(Content=minimapOverlayTB)
    canvasAdd(appMainCanvas, minimapOverlayButton, 16.*OMTW - kittyBoxWidth - 115., THRU_MAIN_MAP_H + 22.)
    let mutable minimapOverlay = fun _irc -> ()
    minimapOverlayButton.Click.Add(fun _ -> minimapOverlay(false))
    minimapOverlayButton.MouseRightButtonDown.Add(fun _ -> minimapOverlay(true))

    // near-mouse HUD button
    let nearMouseHUDTB = new TextBox(FontSize=12., Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, BorderThickness=Thickness(0.), Text="Near-mouse HUD", IsHitTestVisible=false)
    let nearMouseHUDButton = new Button(Content=nearMouseHUDTB)
    canvasAdd(appMainCanvas, nearMouseHUDButton, 16.*OMTW - kittyBoxWidth - 115., THRU_MAIN_MAP_H + 44.)
    let mutable nearMouseHUD = fun _irc -> ()
    nearMouseHUDButton.Click.Add(fun _ -> nearMouseHUD(false))
    nearMouseHUDButton.MouseRightButtonDown.Add(fun _ -> nearMouseHUD(true))
#endif

    do! showProgress("misc")

    let blockerDungeonSunglasses : FrameworkElement[] = Array.zeroCreate 8
    let mutable oneTimeRemindLadder, oneTimeRemindAnyKey = None, None
    OverworldItemGridUI.refreshUIAfterOverworldTemplateChange <- (fun () -> doUIUpdateEvent.Trigger())
    decorStateChanged <- (fun () -> doUIUpdateEvent.Trigger())   // marker-icon check-offs refresh the progress bar etc.
    doUIUpdateEvent.Publish.Add(fun () ->
//        let diagSW = System.Diagnostics.Stopwatch.StartNew()
        if displayIsCurrentlyMirrored <> TrackerModel.MirrorOverworld then
            // model changed, align the view
            displayIsCurrentlyMirrored <- not displayIsCurrentlyMirrored
            if displayIsCurrentlyMirrored then
                for fe in mirrorOverworldFEs do
                    // (flip about the element's own center: elements not yet measured, like the lazily-built coords grid, have ActualWidth 0 here)
                    let scaleTrans = new ScaleTransform(-1., 1.)
                    if scaleTrans.CanFreeze then
                        scaleTrans.Freeze()
                    fe.RenderTransformOrigin <- Point(0.5, 0.5)
                    fe.RenderTransform <- scaleTrans
            else
                for fe in mirrorOverworldFEs do
                    fe.RenderTransform <- null
        // redraw triforce display (some may have located/unlocated/hinted)
        updateNumberedTriforceDisplayIfItExists()
        // redraw white/magical swords (may have located/unlocated/hinted)
        redrawWhiteSwordCanvas(white_sword_canvas)
        redrawMagicalSwordCanvas(mags_canvas)
        // update specific-blockers that may have been (un)blocked
        for f in Views.redrawBoxes do f()
        for f in Views.redrawTriforces do f()
        // the triforce reference chart follows the triforces however they were marked (numbered triforce, chart click, or a triforce piece in an item box)
        let havesKey = System.String.Join(",", TrackerModel.GetTriforceHaves() |> Array.map (fun b -> if b then "1" else "0")) + (if questRefIsSecond() then "2" else "1")
        if havesKey <> lastQuestRefKey.Value then
            lastQuestRefKey.Value <- havesKey
            updateQuestRef()
        // update overworld marks (may be hiding useless shops, and they just got an item rendering a shop useless (e.g. magic key and key shops))
        OptionsMenu.requestRedrawOverworldEvent.Trigger()

        recorderingCanvas.Children.Clear()
        // TODO event for redraw item progress? does any of this event interface make sense? hmmm
        redrawItemProgressBar()

        // Progressive Items: each family (1 swords, 2 rings, 3 candles, 4 arrows) has a tier that follows the number collected; when it goes up, the others glow and the alert plays
        for f in 1..4 do progressiveLastTiers.[f] <- max 1 (TrackerModel.progressiveFamilyTier f)
        let progressiveIdentsOf(f:int) = [ for id in 0..127 do if TrackerModel.progressiveFamily id = f then yield id ]
        let progressiveAutoChecking = ref false
        progressiveSwordCheckHook <- (fun () ->
            for f in 1..4 do
                let t = TrackerModel.progressiveFamilyTier f
                let old = progressiveLastTiers.[f]
                if t <> old then
                    progressivePrevTiers.[f] <- old
                    progressiveChangedAt.[f] <- System.DateTime.Now
                    progressiveLastTiers.[f] <- t
                    TrackerModel.progressiveTierChanged.Trigger()
                    for x = 0 to 15 do
                        for y = 0 to 7 do
                            redrawOwTileHook(x, y)
                    if t > old && old > 0 then
                        let ids = progressiveIdentsOf f
                        let tiles = [ for x in 0..15 do
                                        for y in 0..7 do
                                            if TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons.[x,y]) |> List.exists (fun (id,c) -> List.contains id ids && not c) then yield (x,y) ]
                        let boxes = TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes() |> Array.filter (fun b -> List.contains (b.CellCurrent()) ids && b.PlayerHas() = TrackerModel.PlayerHas.NO)
                        if not tiles.IsEmpty || boxes.Length > 0 then
                            for b in boxes do TrackerModel.progressiveBoxGlow.Trigger(b)
                            progressiveUpgradeHook tiles (progressiveFamilyTierBmp(f, old)) (progressiveFamilyTierBmp(f, t))
                    // once the click that caused this has finished (other handlers may still be updating), draw everything again from the final state
                    async {
                        do! Async.Sleep 150
                        TrackerModel.progressiveTierChanged.Trigger()
                        for x = 0 to 15 do
                            for y = 0 to 7 do
                                redrawOwTileHook(x, y)
                    } |> Async.StartImmediate
            // once the last upgrade of a family has been collected (4 swords, 2 rings / candles / arrows), every other copy of it still to be collected is checked off too
            if not progressiveAutoChecking.Value then
                progressiveAutoChecking.Value <- true
                try
                    for f in 1..4 do
                        let cap = if f = 1 then 4 else 2
                        let ids = progressiveIdentsOf f
                        let boxesAll = TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes()
                        let n = (ids |> List.sumBy (fun id -> TrackerModel.mapCollectedCount.[id])) + (boxesAll |> Array.filter (fun b -> List.contains (b.CellCurrent()) ids && b.PlayerHas() = TrackerModel.PlayerHas.YES) |> Array.length)
                        if n >= cap then
                            for x in 0..15 do
                                for y in 0..7 do
                                    let es = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons.[x,y])
                                    if es |> List.exists (fun (id,c) -> List.contains id ids && not c) then
                                        let label = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[x,y])
                                        let packed = TrackerModel.decorEntriesPack(es |> List.map (fun (id,c) -> if List.contains id ids then (id, true) else (id, c)))
                                        setDecorIcons(x, y, (if label > 0 then TrackerModel.withShopLabel(packed, label) else packed))
                                        redrawOwTileHook(x, y)
                            for b in boxesAll do
                                if List.contains (b.CellCurrent()) ids && b.PlayerHas() = TrackerModel.PlayerHas.NO then b.SetPlayerHas(TrackerModel.PlayerHas.YES)
                finally
                    progressiveAutoChecking.Value <- false)
        if not progressiveWired then
            progressiveWired <- true
            // collecting (or un-collecting) any item of a family in an item box (dungeon, shop/cave box, ...)
            for b in TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes() do
                let prev = ref (b.CellCurrent(), b.PlayerHas())
                b.Changed.Add(fun () ->
                    let (pc, pph) = prev.Value
                    let c, ph = b.CellCurrent(), b.PlayerHas()
                    prev.Value <- (c, ph)
                    if TrackerModel.ProgressiveItems then
                        let nowF = if ph = TrackerModel.PlayerHas.YES then TrackerModel.progressiveFamily c else 0
                        let wasF = if pph = TrackerModel.PlayerHas.YES then TrackerModel.progressiveFamily pc else 0
                        if nowF > 0 && (wasF = 0 || pc <> c) then TrackerModel.progressiveBoxTier.[b] <- progressiveCollectedTier nowF
                        elif wasF > 0 && nowF = 0 then TrackerModel.progressiveBoxTier.Remove(b) |> ignore
                        if TrackerModel.progressiveFamily pc > 0 || TrackerModel.progressiveFamily c > 0 then progressiveSwordCheckHook())
            // items taken from caves / ticked in Item Progress or the starting items also count toward the tiers
            let pp = TrackerModel.playerProgressAndTakeAnyHearts
            let se = TrackerModel.startingItemsAndExtras
            for (prop:TrackerModel.BoolProperty) in [ pp.PlayerHasWoodSword; se.PlayerHasWhiteSword; se.PlayerHasMagicalSword; pp.PlayerHasBlueRing; se.PlayerHasRedRing; pp.PlayerHasBlueCandle; se.PlayerHasRedCandle; pp.PlayerHasWoodArrow; se.PlayerHasSilverArrow ] do
                prop.Changed.Add(fun _ -> if TrackerModel.ProgressiveItems then progressiveSwordCheckHook())
        // Progressive Items: a notification plus a 3 second glow-and-grow of every upgraded item on the map
        progressiveUpgradeHook <- (fun tiles fromBmp toBmp ->
            SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, "Progressive Item Upgraded", [ReminderTextBox("Progressive Item Upgraded"); upcb(fromBmp); upcb(Graphics.iconRightArrow_bmp); upcb(toBmp)])
            for (x, y) in tiles do
                let glow = Views.makeProgressiveGlow(toBmp, 24.)   // the upgraded item in full colour, on a golden halo
                canvasAdd(overworldCanvas, glow, float x * OMTW + OMTW / 2. - 12., float (y * 11 * 3) + 33. / 2. - 12.)
                async {
                    do! Async.Sleep 3000
                    overworldCanvas.Children.Remove(glow)
                } |> Async.StartImmediate)
        TrackerModel.allUIEventingLogic( {new TrackerModel.ITrackerEvents with
            member _this.CurrentHearts(h) = currentMaxHeartsTextBox.Text <- sprintf "Max Hearts: %d" h
            member _this.AnnounceConsiderSword2() = RemindSword2()
            member _this.AnnounceConsiderSword3() = 
                SendReminderImpl(TrackerModel.ReminderCategory.SwordHearts, "Consider the magical sword", [upcb(Graphics.iconRightArrow_bmp); upcb(Graphics.ow_sword3_item_bmp)],
                                    Some(AsyncBrieflyHighlightAnOverworldLocation(TrackerModel.mapStateSummary.Sword3Location)))
            member _this.OverworldSpotsRemaining(remain,gettable) = 
                owRemainingNumbersTextBox.Text <- sprintf "%d/%d" gettable remain   // gettable / total
                owGettableScreensTextBox.Text <- sprintf "%d gettable" gettable
                for k in 0..6 do
                    owSpotTypeCountTBs.[k].Text <- string TrackerModel.owSpotTypeCounts.[k]
            member _this.DungeonLocation(i,x,y,hasTri,isCompleted) =
                owUpdateFunctions.[x,y] 0 null
            member _this.AnyRoadLocation(i,x,y) = ()
            member _this.Armos(x,y)  = owUpdateFunctions.[x,y] 0 null  // redraw the tile, to update bright/dark or remove icon if player hides useless icons
            member _this.Sword3(x,y) = owUpdateFunctions.[x,y] 0 null  // redraw the tile, to update bright/dark or remove icon if player hides useless icons
            member _this.Sword2(x,y) = owUpdateFunctions.[x,y] 0 null  // redraw the tile, to update bright/dark or remove icon if player hides useless icons
            member _this.RoutingInfo(haveLadder,haveRaft,currentRecorderWarpDestinations,currentAnyRoadDestinations,_owRouteworthySpots) = 
                // clear and redraw routing
                OverworldRouting.repopulate(haveLadder,haveRaft,currentRecorderWarpDestinations|>Seq.map fst|>Seq.toArray,currentAnyRoadDestinations|>Seq.toArray,displayIsCurrentlyMirrored)
                // redraw recorder dests
                for (i,j),idx in currentRecorderWarpDestinations do
                    //let L = 6.0
                    //let bg = new Shapes.Ellipse(Width=float(11*3)-2.+2.0*L, Height=float(11*3)-2.+L, Stroke=Brushes.Black, StrokeThickness=3.0, IsHitTestVisible=false)
                    let bg = new Shapes.Rectangle(Width=35., Height=35., Stroke=Brushes.Black, StrokeThickness=3.0, IsHitTestVisible=false, RenderTransform=DungeonUI.fortyFiveDegrees)
                    bg.Effect <- new Effects.BlurEffect(Radius=5.0, KernelType=Effects.KernelType.Gaussian)
                    //let fg = new Shapes.Ellipse(Width=float(11*3)-2.+2.0*L, Height=float(11*3)-2.+L, Stroke=UIComponents.RecorderEllipseColor(), StrokeThickness=3.0, IsHitTestVisible=false)
                    let fg = new Shapes.Rectangle(Width=35., Height=35., Stroke=UIComponents.RecorderEllipseColor(), StrokeThickness=3.0, IsHitTestVisible=false, RenderTransform=DungeonUI.fortyFiveDegrees)
                    if Graphics.canUseEffectsWithoutDestroyingPerformance then
                        //canvasAdd(recorderingCanvas, bg, OMTW*float i+7.-L+1., float(11*3*j)-L/2.+1.)
                        canvasAdd(recorderingCanvas, bg, OMTW*float i+23., float(11*3*j)-9.)
                    //canvasAdd(recorderingCanvas, fg, OMTW*float i+7.-L+1., float(11*3*j)-L/2.+1.)
                    canvasAdd(recorderingCanvas, fg, OMTW*float i+23., float(11*3*j)-9.)
                    if not(TrackerModel.recorderToNewDungeons) then
                        // label the vanilla spots, so player knows the order
                        let tb = new TextBox(Text=sprintf "%c" (char idx + char '1'), FontSize=12., FontWeight=FontWeights.Bold, Foreground=Brushes.White, Background=Brushes.Black, 
                                                IsReadOnly=true, IsHitTestVisible=false, BorderThickness=Thickness(0.))
                        recorderingCanvas.Children.Add(tb) |> ignore
                        Canvas.SetLeft(tb, OMTW*float i+2.)
                        Canvas.SetBottom(tb, float(11*3*(7-j))+2.)
                // redraw routes and highlights
                let pos = System.Windows.Input.Mouse.GetPosition(overworldCanvas)
                let i,j = int(Math.Floor(pos.X / OMTW)), int(Math.Floor(pos.Y / (11.*3.)))
                if i>=0 && i<16 && j>=0 && j<8 then
                    drawRoutesTo(currentRouteTarget(), Point(0.,0.), i, j, TrackerModelOptions.Overworld.DrawRoutes.Value, 
                                    (if TrackerModelOptions.Overworld.HighlightNearby.Value then OverworldRouteDrawing.MaxGYR else 0),
                                    whetherToCyanOpenCavesOrArmos())
                else
                    ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes()
            member _this.AnnounceCompletedDungeon(i) = 
                let icons = [upcb(MapStateProxy(i).DefaultInteriorBmp()); upcb(Graphics.iconCheckMark_bmp)]
                if TrackerModel.IsHiddenDungeonNumbers() then
                    let labelChar = TrackerModel.GetDungeon(i).LabelChar
                    if labelChar <> '?' then
                        SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, sprintf "Dungeon %c is complete" labelChar, icons)
                    else
                        SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, "This dungeon is complete", icons)
                else
                    SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, sprintf "Dungeon %d is complete" (i+1), icons)
            member _this.CompletedDungeons(a) =
                for i = 0 to 7 do
                    // top ui
                    for j = 1 to 4 do
                        mainTrackerCanvases.[i,j].Children.Remove(mainTrackerCanvasShaders.[i,j]) |> ignore
                    if a.[i] then
                        for j = 1 to 4 do  // don't shade the color swatches
                            mainTrackerCanvases.[i,j].Children.Add(mainTrackerCanvasShaders.[i,j]) |> ignore
                    // blockers ui
                    if a.[i] then
                        blockerDungeonSunglasses.[i].Opacity <- 0.3
                    else
                        blockerDungeonSunglasses.[i].Opacity <- 1.
            member _this.AnnounceFoundDungeonCount(n) = RemindFoundDungeonCount(n)
            member _this.AnnounceTriforceCount(n) = RemindTriforceCount(n, AsyncBrieflyHighlightAnOverworldLocation)
            member _this.AnnounceTriforceAndGo(triforceCount, tagSummary) = 
                // even once the way into level 9 is satisfied, the bow and silver arrows are still needed (both can be inside level 9 itself)
                let stillNeed = [
                    if not tagSummary.HaveBow then
                        yield "the bow", upcb(Graphics.greyscale Graphics.bow_bmp)
                    if not tagSummary.HaveSilvers then
                        yield (if tagSummary.SilversKnownToBeInLevel9 then "the silver arrows from level 9" else "the silver arrows"), upcb(Graphics.greyscale Graphics.silver_arrow_bmp)
                    ]
                let stillNeedText = if stillNeed.IsEmpty then "" else ", but you still need " + System.String.Join(" and ", stillNeed |> List.map fst)
                let stillNeedIcons = [ if not stillNeed.IsEmpty then yield upcb(Graphics.iconRightArrow_bmp); for (_,icon) in stillNeed do yield icon ]
                // the user's level 9 entry item was just collected: show the item and a green check (instead of triforces)
                if tagSummary.CustomTrigger && TrackerModel.level9EntryAnnouncement then
                    let it = TrackerModel.level9CustomTriggerItem()
                    SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, sprintf "You have the %s needed to enter level 9%s" (TrackerModel.ITEMS.AsPronounceString(it)) stillNeedText,
                                    [yield upcb(CustomComboBoxes.boxCurrentBMP(it, None)); yield upcb(Graphics.iconCheckMark_bmp); yield! stillNeedIcons])
                let needSomeThingsicons = [
                    for _i = 1 to tagSummary.MissingDungeonCount do
                        yield upcb(Graphics.greyscale Graphics.genericDungeonInterior_bmp)
                    if not tagSummary.HaveBow then
                        yield upcb(Graphics.greyscale Graphics.bow_bmp)
                    if not tagSummary.HaveSilvers && not tagSummary.SilversKnownToBeInLevel9 then
                        yield upcb(Graphics.greyscale Graphics.silver_arrow_bmp)
                    ]
                let triforceAndGoIcons = [
                    if triforceCount < TrackerModel.level9TriforceCount && not tagSummary.CustomTrigger then
                        if needSomeThingsicons.Length<>0 then
                            yield upcb(Graphics.iconRightArrow_bmp)
                        for _i = 1 to (TrackerModel.level9TriforceCount-triforceCount) do
                            yield upcb(Graphics.greyTriforce_bmp)
                    yield upcb(Graphics.iconRightArrow_bmp)
                    if not tagSummary.HaveSilvers && tagSummary.SilversKnownToBeInLevel9 then
                        yield upcb(Graphics.greyscale Graphics.silver_arrow_bmp)
                    yield upcb(Graphics.ganon_bmp)
                    ]
                let icons = [yield! needSomeThingsicons; yield! triforceAndGoIcons]
                let go = if triforceCount >= TrackerModel.level9TriforceCount || tagSummary.CustomTrigger then "go time" else "triforce and go"
                let silverDisclaimer = if not tagSummary.HaveSilvers && tagSummary.SilversKnownToBeInLevel9 then ", but you will need the silver arrows from 9" else ""
                match tagSummary.Level with
                | 101 -> SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, "You might be "+go+silverDisclaimer, icons)
                | 102 -> SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, "You are probably "+go+silverDisclaimer, icons)
                | 103 -> SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, "You are "+go+silverDisclaimer, icons)
                | 0 ->
                    // nothing else worth reporting, but if the level 9 entry rule is satisfied still say so
                    if not tagSummary.CustomTrigger && triforceCount >= TrackerModel.level9TriforceCount then
                        SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, sprintf "You have the %d triforces needed to enter level 9%s" TrackerModel.level9TriforceCount stillNeedText,
                                        [yield upcb(Graphics.fullOrangeTriforce_bmp); yield upcb(Graphics.iconCheckMark_bmp); yield! stillNeedIcons])
                | _ -> SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, (sprintf "You need %s to be " (if needSomeThingsicons.Length>1 then "some things" else "something"))+go, icons)
            member _this.RemindUnblock(blockerType, dungeons, detail) =
                let name(d) =
                    if TrackerModel.IsHiddenDungeonNumbers() then
                        (char(int 'A' + d)).ToString()
                    else
                        (1+d).ToString()
                let icons = ResizeArray()
                let mutable sentence = "Now that you have"
                match blockerType with
                | TrackerModel.DungeonBlocker.COMBAT ->
                    let words = ResizeArray()
                    for d in detail do
                        match d with
                        | TrackerModel.CombatUnblockerDetail.BETTER_SWORD -> words.Add(" a better sword,"); icons.Add(upcb(Graphics.swordLevelToBmp(TrackerModel.playerComputedStateSummary.SwordLevel)))
                        | TrackerModel.CombatUnblockerDetail.BETTER_ARMOR -> words.Add(" better armor,"); icons.Add(upcb(Graphics.ringLevelToBmp(TrackerModel.playerComputedStateSummary.RingLevel)))
                        | TrackerModel.CombatUnblockerDetail.WAND -> words.Add(" the wand,"); icons.Add(upcb(Graphics.wand_bmp))
                    sentence <- sentence + System.String.Concat words
                | TrackerModel.DungeonBlocker.BOW_AND_ARROW -> sentence <- sentence + " a beau and arrow,"; icons.Add(upcb(Graphics.bow_and_arrow_bmp))
                | TrackerModel.DungeonBlocker.RECORDER -> sentence <- sentence + " the recorder,"; icons.Add(upcb(Graphics.recorder_bmp))
                | TrackerModel.DungeonBlocker.LADDER -> sentence <- sentence + " the ladder,"; icons.Add(upcb(Graphics.ladder_bmp))
                | TrackerModel.DungeonBlocker.KEY -> sentence <- sentence + " the magic key,"; icons.Add(upcb(Graphics.magic_key_bmp))
                | TrackerModel.DungeonBlocker.BOMB -> sentence <- sentence + " bombs,"; icons.Add(upcb(Graphics.bomb_bmp))
                | _ -> ()
                sentence <- sentence + " consider dungeon" + (if Seq.length dungeons > 1 then "s " else " ")
                icons.Add(upcb(Graphics.iconRightArrow_bmp))
                let d = Seq.head dungeons
                sentence <- sentence + name(d)
                icons.Add(upcb(MapStateProxy(d).DefaultInteriorBmp()))
                for d in Seq.tail dungeons do
                    sentence <- sentence + " and " + name(d)
                    icons.Add(upcb(MapStateProxy(d).DefaultInteriorBmp()))
                SendReminder(TrackerModel.ReminderCategory.Blockers, sentence, icons)
            member _this.RemindShortly(itemId) = 
                if itemId = TrackerModel.ITEMS.KEY then
                    oneTimeRemindAnyKey <- Some(new TrackerModel.LastChangedTime(), (fun() ->
                        oneTimeRemindAnyKey <- None
                        if TrackerModel.playerComputedStateSummary.HaveAnyKey then
                            SendReminder(TrackerModel.ReminderCategory.HaveMagicKey, "Don't forget that you have the magic key", [upcb(Graphics.magic_key_bmp)])
                        else
                            TrackerModel.remindedAnyKey <- false))
                elif itemId = TrackerModel.ITEMS.LADDER then
                    oneTimeRemindLadder <- Some(new TrackerModel.LastChangedTime(), (fun() ->
                        oneTimeRemindLadder <- None
                        if TrackerModel.playerComputedStateSummary.HaveLadder then
                            SendReminder(TrackerModel.ReminderCategory.HaveLadder, "Don't forget that you have the ladder", [upcb(Graphics.ladder_bmp)])
                        else
                            TrackerModel.remindedLadder <- false))
                else
                    failwith "bad reminder"
            })
        // place start icon in top layer at very top (above e.g. completed dungeon highlight)
        if TrackerModel.startIconX <> -1 then
            canvasAdd(recorderingCanvas, startIcon, 11.5*OMTW/48.-3.+OMTW*float(TrackerModel.startIconX), float(TrackerModel.startIconY*11*3))
        if TrackerModel.customWaypointX <> -1 then
            canvasAdd(recorderingCanvas, theCustomWaypointIcon, -2.+OMTW*float(TrackerModel.customWaypointX), float(TrackerModel.customWaypointY*11*3)-5.)
//        if diagSW.ElapsedMilliseconds > 100L then
//            printfn "doUIUpdate %dms" diagSW.ElapsedMilliseconds
        )
    let threshold = TimeSpan.FromMilliseconds(500.0)
    let periodicReminders = new PeriodicReminders()
    let timer = new System.Windows.Threading.DispatcherTimer()
    timer.Interval <- TimeSpan.FromSeconds(1.0)
    timer.Tick.Add(fun _ -> 
        if not(TrackerModel.LastChangedTime.IsPaused) then
            let hasUISettledDown = 
                DateTime.Now - TrackerModel.playerProgressLastChangedTime.Time > threshold &&
                DateTime.Now - TrackerModel.dungeonsAndBoxesLastChangedTime.Time > threshold &&
                DateTime.Now - TrackerModel.mapLastChangedTime.Time > threshold
            if hasUISettledDown then
                let hasTheModelChanged = TrackerModel.recomputeWhatIsNeeded()  
                if hasTheModelChanged then
                    doUIUpdateEvent.Trigger()
            stepAnimateLink()
            //This is where the periodic reminders are called. Not all of them are here since there are one timers after
            periodicReminders.Check()
            // one-time reminders
            match oneTimeRemindAnyKey with
            | None -> ()
            | Some(lct, thunk) ->
                //Frequency of the reminder
                if (DateTime.Now - lct.Time).Minutes > 1 then  // 2 min
                    thunk()
            match oneTimeRemindLadder with
            | None -> ()
            | Some(lct, thunk) ->
                if (DateTime.Now - lct.Time).Minutes > 1 then  // 2 min
                    thunk()
        )

    // create overworld locator stuff (added to correct layer of visual tree later in the code)
    let owLocatorGrid = makeGrid(16, 8, int OMTW, 11*3)
    let owLocatorCanvas = new Canvas()
    for i = 0 to 15 do
        for j = 0 to 7 do
            let z = new Graphics.TileHighlightRectangle()
            z.Hide()
            owLocatorTileRectangles.[i,j] <- z
            gridAdd(owLocatorGrid, z.Shape, i, j)

    // Dungeon level trackers
    let rightwardCanvas = new Canvas()
    let levelTabSelected = new Event<_>()  // blockers listens, to subtly highlight a dungeon
    let blockersHoverEvent = new Event<bool>()
    let mutable currentCCLevel = -1
    let contentCanvasMouseEnterFunc(level) =
        if level <> currentCCLevel then
            currentCCLevel <- level
            if level>=10 then // 10+ = summary tab, show all dungeon locations; 11 means moused over 1, 12 means 2, ...
                for i = 0 to 15 do
                    for j = 0 to 7 do
                        let cur = TrackerModel.overworldMapMarks.[i,j].Current()
                        if cur >= TrackerModel.MapSquareChoiceDomainHelper.DUNGEON_1 && cur <= TrackerModel.MapSquareChoiceDomainHelper.DUNGEON_9 then
                            let curLevel = cur-TrackerModel.MapSquareChoiceDomainHelper.DUNGEON_1 // 0-8
                            if (curLevel = level-11) || (level=10) then  // if hovering this particular dungeon within summary tab, or if hovering 'S' header
                                owLocatorTileRectangles.[i,j].MakeGreenWithBriefAnimation()
                            elif not(TrackerModel.GetDungeon(curLevel).IsComplete) then
                                owLocatorTileRectangles.[i,j].MakeBoldGreen()
                            else
                                () // do nothing - don't highlight completed dungeons
                drawRoutesTo(None, Point(), 0, 0, false, 0, whetherToCyanOpenCavesOrArmos())
            else
                let i,j = TrackerModel.mapStateSummary.DungeonLocations.[level-1]
                if (i,j) <> TrackerModel.NOTFOUND then
                    // when mouse in a dungeon map, show its location...
                    showLocatorExactLocation(TrackerModel.mapStateSummary.DungeonLocations.[level-1])
                    // ...and behave like we are moused there
                    drawRoutesTo(None, Point(), i, j, TrackerModelOptions.Overworld.DrawRoutes.Value, 
                                        (if TrackerModelOptions.Overworld.HighlightNearby.Value then OverworldRouteDrawing.MaxGYR else 0),
                                        whetherToCyanOpenCavesOrArmos())
    let! dungeonTabs,posToWarpToWhenTabbingFromOverworld,grabModeTextBlock,exportDungeonModelsJsonLinesF,importDungeonModels = 
        DungeonUI.makeDungeonTabs(cm, (fun x -> layout.AddDungeonTabs(x)), (fun () -> layout.GetDungeonY()), selectDungeonTabEvent, TH, rightwardCanvas,
                                    levelTabSelected, blockersHoverEvent, mainTrackerGhostbusters, showProgress, contentCanvasMouseEnterFunc, (fun _level -> hideLocator()))
    exportDungeonModelsJsonLines <- exportDungeonModelsJsonLinesF
    OverworldMapTileCustomization.goToDungeon <- (fun index ->
        selectDungeonTabEvent.Trigger(index)
        let newPos = dungeonTabs.TranslatePoint(posToWarpToWhenTabbingFromOverworld, appMainCanvas)
        layout.ShowMouseWarp(newPos)
        Graphics.NavigationallyWarpMouseCursorTo(newPos)
        layout.FocusDungeon())
    layout.AddDungeonTabsOverlay(dungeonTabsOverlay)

    do! showProgress("blockers")
    
    // blockers
    let blockerGrid = UIComponents.MakeBlockers(cm, blockerQueries, levelTabSelected, blockersHoverEvent, blockerDungeonSunglasses, contentCanvasMouseEnterFunc, (fun _level -> hideLocator()))
    layout.AddBlockers(blockerGrid)

    do! showProgress("notes, gettables")
    // notes    
    notesTextBox <- new TextBox(Width=appMainCanvas.Width-BLOCKERS_AND_NOTES_OFFSET, Height=dungeonTabs.Height - blockerGrid.Height,
                            FontSize=TrackerModelOptions.NotesFontSize, Foreground=Brushes.LimeGreen , Background=Brushes.Black, AcceptsReturn=true, TextWrapping=TextWrapping.Wrap, VerticalScrollBarVisibility=ScrollBarVisibility.Auto)   // long lines wrap instead of running off the box
    let notesFilename = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Notes.txt")
    if not(System.IO.File.Exists(notesFilename)) then
        notesTextBox.Text <- "Notes\n"
    else
        notesTextBox.Text <- System.IO.File.ReadAllText(notesFilename)
    let seedAndFlagsDisplayCanvas = new Canvas(Width=notesTextBox.Width, Height=notesTextBox.Height)
    layout.AddNotesSeedsFlags(notesTextBox, seedAndFlagsDisplayCanvas)
    let seedAndFlagsTB = new TextBox(FontSize=16., Background=Brushes.Transparent, Foreground=Brushes.Orange, 
                                        IsHitTestVisible=false, IsReadOnly=true, Focusable=false, Text="\n", BorderThickness=Thickness(0.))
    Canvas.SetRight(seedAndFlagsTB, 44.)   // leaves room for the font size button in the corner
    Canvas.SetBottom(seedAndFlagsTB, 3.)
    seedAndFlagsDisplayCanvas.Children.Add(seedAndFlagsTB) |> ignore
    // font size button at the bottom right of the notes box: opens a small slider to pick the size of the notes text
    let notesFontButton = Graphics.makeButton("Aa", Some(14.), Some(Brushes.Orange))
    notesFontButton.Width <- 36.
    notesFontButton.Height <- 24.
    Graphics.styleRoundedButton(notesFontButton)
    notesFontButton.ToolTip <- "Notes font size"
    Canvas.SetRight(notesFontButton, 4.)
    Canvas.SetBottom(notesFontButton, 4.)
    seedAndFlagsDisplayCanvas.Children.Add(notesFontButton) |> ignore
    notesFontButton.Click.Add(fun _ ->
        if not popupIsActive then
            popupIsActive <- true
            let pos = notesFontButton.TranslatePoint(Point(-250., -100.), cm.AppMainCanvas)
            async {
                let wh = new System.Threading.ManualResetEvent(false)
                let sp = new StackPanel(Orientation=Orientation.Vertical, Margin=Thickness(6.))
                let title = new TextBox(IsHitTestVisible=false, BorderThickness=Thickness(0.), FontSize=16., Margin=Thickness(3.), Foreground=Brushes.Orange, Background=Brushes.Black)
                let update(v:float) =
                    notesTextBox.FontSize <- v
                    title.Text <- sprintf "Notes font size: %d" (int v)
                update(notesTextBox.FontSize)
                let slider = new Slider(Minimum=8., Maximum=72., Value=notesTextBox.FontSize, TickFrequency=1., IsSnapToTickEnabled=true, Width=220., Margin=Thickness(3.,6.,3.,6.))
                slider.ValueChanged.Add(fun e -> update(e.NewValue))
                let doneBtn = new Button(Content=" Done ", Margin=Thickness(3.), HorizontalAlignment=HorizontalAlignment.Right, Focusable=false)
                doneBtn.Click.Add(fun _ -> wh.Set() |> ignore)
                sp.Children.Add(title) |> ignore
                sp.Children.Add(slider) |> ignore
                sp.Children.Add(doneBtn) |> ignore
                let border = new Border(Child=sp, Background=Brushes.Black, BorderThickness=Thickness(4.), BorderBrush=Brushes.Gray)
                do! CustomComboBoxes.DoModal(cm, wh, max 0. pos.X, max 0. pos.Y, border)
                TrackerModelOptions.NotesFontSize <- notesTextBox.FontSize
                TrackerModelOptions.writeSettings()
                popupIsActive <- false
            } |> Async.StartImmediate)
    SaveAndLoad.seedAndFlagsUpdated.Publish.Add(fun _ ->
        if TrackerModelOptions.DisplaySeedAndFlags.Value then
            seedAndFlagsTB.Text <- sprintf "Seed & Flags: %s\n%s" SaveAndLoad.lastKnownSeed SaveAndLoad.lastKnownFlags
        else
            seedAndFlagsTB.Text <- "\n"
        )

    grabModeTextBlock.Opacity <- 0.
    grabModeTextBlock.Width <- notesTextBox.Width
    layout.AddExtraDungeonRightwardStuff(grabModeTextBlock, rightwardCanvas)

    // remaining OW spots
    layout.AddOWRemainingScreens(owRemainingScreensTextBoxContainerPanelThatSeesMouseEvents)
    owRemainingScreensTextBoxContainerPanelThatSeesMouseEvents.MouseEnter.Add(fun _ ->
      if not TrackerModelOptions.RaceMode.Value then
        let unmarked = TrackerModel.overworldMapMarks |> Array2D.map (fun cell -> cell.Current() = -1)
        OverworldRouteDrawing.drawPathsImpl(unmarked, unmarked, Point(0.,0.), 0, 0, false, true, OverworldRouteDrawing.All, false, NoCyan)
        if not(TrackerModel.playerComputedStateSummary.HaveRaft) then
            // drawPathsImpl cannot reach the raft locations and won't color them, so just ad-hoc those two spots
            for i,j in [5,4 ; 15,2] do
                let cur = TrackerModel.overworldMapMarks.[i,j].Current()
                if cur = -1 then  // they may have marked the raft spots (e.g. Any Road), so only if unmarked...
                    OverworldRouteDrawing.routeDrawingLayer.GetHighlightTile(i,j).MakePaleRed()
        )
    owRemainingScreensTextBoxContainerPanelThatSeesMouseEvents.MouseLeave.Add(fun _ -> ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes())
    // gettables are always shown on the standard map (the checkbox is no longer displayed; its text is folded into the "x/x OW spots left" line)
    owGettableScreensCheckBox.IsChecked <- isStandardHyrule
    owGettableScreensCheckBox.Checked.Add(fun _ -> TrackerModel.forceUpdate(); TrackerModelOptions.Overworld.Gettables.Value <- true; TrackerModelOptions.writeSettings()) 
    owGettableScreensCheckBox.Unchecked.Add(fun _ -> TrackerModel.forceUpdate(); TrackerModelOptions.Overworld.Gettables.Value <- false; TrackerModelOptions.writeSettings())
    owGettableScreensCheckBox.MouseEnter.Add(fun _ -> 
        OverworldRouteDrawing.drawPathsImpl(TrackerModel.mapStateSummary.OwRouteworthySpots, 
                                            TrackerModel.overworldMapMarks |> Array2D.map (fun cell -> cell.Current() = -1), Point(0.,0.), 0, 0, false, true, 0, true, NoCyan)
        )
    owGettableScreensCheckBox.MouseLeave.Add(fun _ -> ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes())

    do! showProgress("coords/zone overlays")

    // current max hearts
    let currentMaxHeartsTextBoxClickable = new Border(Background=Brushes.Black, Child=currentMaxHeartsButton, BorderThickness=Thickness(0.))
    layout.AddCurrentMaxHearts(currentMaxHeartsTextBoxClickable)
    currentMaxHeartsTextBoxClickable.MouseEnter.Add(fun _ -> currentMaxHeartsButton.BorderBrush <- Brushes.DarkTurquoise)
    currentMaxHeartsTextBoxClickable.MouseLeave.Add(fun _ -> currentMaxHeartsButton.BorderBrush <- Graphics.roundedBorderBrush)
    currentMaxHeartsTextBoxClickable.ToolTip <- "Left-click: add a heart\nRight-click: remove a heart"
    currentMaxHeartsTextBoxClickable.PreviewMouseDown.Add(fun ea -> 
        let delta = if ea.ChangedButton = Input.MouseButton.Left then 1 elif ea.ChangedButton = Input.MouseButton.Right then -1 else 0
        if delta <> 0 then
            ea.Handled <- true
            TrackerModel.startingItemsAndExtras.MaxHeartsDifferential <- TrackerModel.startingItemsAndExtras.MaxHeartsDifferential + delta
            TrackerModel.recomputePlayerStateSummary()
            currentMaxHeartsTextBox.Text <- sprintf "Max Hearts: %d" TrackerModel.playerComputedStateSummary.PlayerHearts
            doUIUpdateEvent.Trigger())
    // coordinate grid
    let placeholderCanvas = new Canvas()  // for startup perf, only add in coords & zone overlays on demand
    let zoneCanvas = new Canvas()
    let owCoordsGrid = makeGrid(16, 8, int OMTW, 11*3)
    owCoordsGrid.Opacity <- 0.
    let mutable placeholderFinished = false
    let ensurePlaceholderFinished() =
        if not placeholderFinished then
            placeholderFinished <- true
            canvasAdd(placeholderCanvas, owCoordsGrid, 0., 0.)
            canvasAdd(placeholderCanvas, zoneCanvas, 0., 0.)
    for i = 0 to 15 do
        for j = 0 to 7 do
            let c = new Canvas(Width=OMTW, Height=float(11*3))
            gridAdd(owCoordsGrid, c, i, j) 
            // https://stackoverflow.com/questions/4114590/how-to-make-wpf-text-on-aero-glass-background-readable
            let coordStr = sprintf "%c  %d" (char (int 'A' + j)) (i+1)
            let tb = new TextBox(Text=coordStr,
                                    Foreground=Brushes.White, Background=Brushes.Transparent, BorderThickness=Thickness(0.0), 
                                    FontFamily=FontFamily("Consolas"), FontSize=16.0, FontWeight=FontWeights.Bold, IsHitTestVisible=false)
            if Graphics.canUseEffectsWithoutDestroyingPerformance then
                let tbb = new TextBox(Text=coordStr,
                                        Foreground=Brushes.Black, Background=Brushes.Transparent, BorderThickness=Thickness(0.0), 
                                        FontFamily=FontFamily("Consolas"), FontSize=16.0, FontWeight=FontWeights.Bold, IsHitTestVisible=false)
                tbb.Effect <- new Effects.BlurEffect(Radius=5.0, KernelType=Effects.KernelType.Gaussian)
                canvasAdd(c, tbb, 2., 6.)
            canvasAdd(c, tb, 2., 6.)
    mirrorOverworldFEs.Add(owCoordsGrid)
    canvasAdd(overworldCanvas, placeholderCanvas, 0., 0.)
    let showCoords = new TextBox(Text="Coords",FontSize=14.0,Background=Brushes.Black,Foreground=Brushes.Orange,BorderThickness=Thickness(0.0),IsReadOnly=true,IsHitTestVisible=false)
    let cb = new CheckBox(Content=showCoords)
    cb.IsChecked <- System.Nullable.op_Implicit TrackerModelOptions.Overworld.Coords.Value
    if TrackerModelOptions.Overworld.Coords.Value then
        ensurePlaceholderFinished(); owCoordsGrid.Opacity <- 0.85
    cb.Checked.Add(fun _ -> ensurePlaceholderFinished(); owCoordsGrid.Opacity <- 0.85; TrackerModelOptions.Overworld.Coords.Value <- true; TrackerModelOptions.writeSettings())
    cb.Unchecked.Add(fun _ -> owCoordsGrid.Opacity <- 0.0; TrackerModelOptions.Overworld.Coords.Value <- false; TrackerModelOptions.writeSettings())
    cb.MouseEnter.Add(fun _ -> if TrackerModelOptions.Overworld.HoverZonesCoords.Value && not(cb.IsChecked.HasValue && cb.IsChecked.Value) then (ensurePlaceholderFinished(); owCoordsGrid.Opacity <- 0.85))
    cb.MouseLeave.Add(fun _ -> if TrackerModelOptions.Overworld.HoverZonesCoords.Value && not(cb.IsChecked.HasValue && cb.IsChecked.Value) then owCoordsGrid.Opacity <- 0.0)
    layout.AddShowCoords(cb, null)   // (the old 'have potion letter' indicator icon is no longer shown)

    // zone overlay
    let zone_checkbox, addZoneName, changeZoneOpacity, allOwMapZoneBlackCanvases = 
        if isStandardHyrule then   // Zones only makes sense in standard map
            UIComponents.MakeZoneOverlay(zoneCanvas, ensurePlaceholderFinished, mirrorOverworldFEs)
        else
            new CheckBox(IsChecked=false), (fun _ -> ()), (fun _ -> ()), Array2D.init 16 8 (fun _ _ -> new Canvas())
    if isStandardHyrule then
        layout.AddOWZoneOverlay(zone_checkbox)

    // mouse hover explainer
    layout.AddMouseHoverExplainer(UIComponents.MakeMouseHoverExplainer(appMainCanvas))

    do! showProgress("locators/timeline/reminders")

    canvasAdd(overworldCanvas, owLocatorGrid, 0., 0.)
    canvasAdd(overworldCanvas, owLocatorCanvas, 0., 0.)

    showLocatorExactLocation <- (fun (x,y) ->
        if (x,y) <> TrackerModel.NOTFOUND then
            hideLocator()  // clear prior locators, there are various scenarios where prior might not be cleared
            owLocatorTileRectangles.[x,y].MakeMagenta()
            let OPA = 0.85
            let COL = Brushes.Magenta
            let THI = 2.
            let leftLine = new Shapes.Line(X1=OMTW*float x, Y1=0., X2=OMTW*float x, Y2=float(8*11*3), Stroke=COL, StrokeThickness=THI, IsHitTestVisible=false, Opacity=OPA)
            canvasAdd(owLocatorCanvas, leftLine, 0., 0.)
            let rightLine = new Shapes.Line(X1=OMTW*float (x+1)-1., Y1=0., X2=OMTW*float (x+1)-1., Y2=float(8*11*3), Stroke=COL, StrokeThickness=THI, IsHitTestVisible=false, Opacity=OPA)
            canvasAdd(owLocatorCanvas, rightLine, 0., 0.)
            let topLine = new Shapes.Line(X1=0., Y1=float(y*11*3), X2=OMTW*float(16*3), Y2=float(y*11*3), Stroke=COL, StrokeThickness=THI, IsHitTestVisible=false, Opacity=OPA)
            canvasAdd(owLocatorCanvas, topLine, 0., 0.)
            let bottomLine = new Shapes.Line(X1=0., Y1=float((y+1)*11*3)-1., X2=OMTW*float(16*3), Y2=float((y+1)*11*3)-1., Stroke=COL, StrokeThickness=THI, IsHitTestVisible=false, Opacity=OPA)
            canvasAdd(owLocatorCanvas, bottomLine, 0., 0.)
        )
    showLocatorExactLocations <- (fun locs ->
        hideLocator()
        for (x,y) in locs do
            if (x,y) <> TrackerModel.NOTFOUND then
                owLocatorTileRectangles.[x,y].MakeMagenta()
                let OPA = 0.85
                let COL = Brushes.Magenta
                let THI = 2.
                let mk(x1,y1,x2,y2) = canvasAdd(owLocatorCanvas, new Shapes.Line(X1=x1, Y1=y1, X2=x2, Y2=y2, Stroke=COL, StrokeThickness=THI, IsHitTestVisible=false, Opacity=OPA), 0., 0.)
                mk(OMTW*float x, 0., OMTW*float x, float(8*11*3))
                mk(OMTW*float (x+1)-1., 0., OMTW*float (x+1)-1., float(8*11*3))
                mk(0., float(y*11*3), OMTW*float(16*3), float(y*11*3))
                mk(0., float((y+1)*11*3)-1., OMTW*float(16*3), float((y+1)*11*3)-1.)
        )
    showLocatorHintedZone <- (fun (hinted_zone, alsoHighlightABCDEFGH) ->
        OverworldRouteDrawing.routeDrawingLayer.Clear()
        if hinted_zone <> TrackerModel.HintZone.UNKNOWN then
            // have hint, so draw that zone...
            let inZone = Array2D.init 16 8 (fun x y -> OverworldData.owMapZone.[y].[x] = hinted_zone.AsDataChar())
            for x = 0 to 15 do
                for y = 0 to 7 do
                    if not(inZone.[x,y]) then
                        allOwMapZoneBlackCanvases.[x,y].Opacity <- 0.7
            let isZone(x,y) = inZone.[max 0 (min 15 x), max 0 (min 7 y)]  // if x or y out of bounds, grab nearby in-bounds value
            let ST, OFF, COLOR = 3., 3./2., Brushes.LightGray
            for x = 0 to 15 do
                for y = 0 to 7 do
                    let mkLine(x1,y1,x2,y2) = 
                        let line = new Shapes.Line(X1=x1, Y1=y1, X2=x2, Y2=y2, Stroke=COLOR, StrokeThickness=ST, IsHitTestVisible=false)
                        line.StrokeDashArray <- new DoubleCollection( seq[1.; 1.5] )
                        line
                    if isZone(x,y) && not(isZone(x-1,y)) then  // left
                        let line = mkLine(OMTW*float(x)-OFF, float(y*11*3), OMTW*float(x)-OFF, float((y+1)*11*3))
                        canvasAdd(owLocatorCanvas, line, 0., 0.)
                    if isZone(x,y) && not(isZone(x+1,y)) then  // right
                        let line = mkLine(OMTW*float(x+1)+OFF, float(y*11*3), OMTW*float(x+1)+OFF, float((y+1)*11*3))
                        canvasAdd(owLocatorCanvas, line, 0., 0.)
                    if isZone(x,y) && not(isZone(x,y-1)) then  // top
                        let line = mkLine(OMTW*float(x), float(y*11*3)-OFF, OMTW*float(x+1), float(y*11*3)-OFF)
                        canvasAdd(owLocatorCanvas, line, 0., 0.)
                    if isZone(x,y) && not(isZone(x,y+1)) then  // bottom
                        let line = mkLine(OMTW*float(x), float((y+1)*11*3)+OFF, OMTW*float(x+1), float((y+1)*11*3)+OFF)
                        canvasAdd(owLocatorCanvas, line, 0., 0.)
            for i = 0 to 15 do
                for j = 0 to 7 do
                    // ... and highlight all undiscovered tiles
                    if OverworldData.owMapZone.[j].[i] = hinted_zone.AsDataChar() then
                        let cur = TrackerModel.overworldMapMarks.[i,j].Current()
                        let isLetteredNumberlessDungeon = (alsoHighlightABCDEFGH && cur>=0 && cur<=7 && TrackerModel.GetDungeon(cur).LabelChar='?')
                        if cur = -1 || isLetteredNumberlessDungeon then
                            if TrackerModel.mapStateSummary.OwGettableLocations.[i,j] then
                                if owInstance.SometimesEmpty(i,j) then
                                    owLocatorTileRectangles.[i,j].MakeYellow()
                                else
                                    owLocatorTileRectangles.[i,j].MakeGreen()
                            else
                                if isLetteredNumberlessDungeon then  // OwGettableLocations does not contain already-marked spots
                                    owLocatorTileRectangles.[i,j].MakeGreen()
                                else
                                    owLocatorTileRectangles.[i,j].MakeRed()
        )
    showLocatorInstanceFunc <- (fun f ->
        OverworldRouteDrawing.routeDrawingLayer.Clear()
        for i = 0 to 15 do
            for j = 0 to 7 do
                if f(i,j) && (TrackerModel.overworldMapMarks.[i,j].Current() = -1 || (i,j) = (15,5)) then   // (F16, the coast ladder spot, is always shown even though that tile is already marked)
                    if owInstance.SometimesEmpty(i,j) then
                        owLocatorTileRectangles.[i,j].MakeYellowWithBriefAnimation()
                    else
                        owLocatorTileRectangles.[i,j].MakeGreenWithBriefAnimation()
        )
    showLocatorInstanceFuncAll <- (fun f ->
        OverworldRouteDrawing.routeDrawingLayer.Clear()
        for i = 0 to 15 do
            for j = 0 to 7 do
                if f(i,j) then
                    if owInstance.SometimesEmpty(i,j) then
                        owLocatorTileRectangles.[i,j].MakeYellowWithBriefAnimation()
                    else
                        owLocatorTileRectangles.[i,j].MakeGreenWithBriefAnimation()
        )
    showHintShopLocator <- (fun () ->
        OverworldRouteDrawing.routeDrawingLayer.Clear()
        let mutable anyFound = false
        for i = 0 to 15 do
            for j = 0 to 7 do
                let cur = TrackerModel.overworldMapMarks.[i,j].Current()
                if cur = TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP then
                    owLocatorTileRectangles.[i,j].MakeGreenWithBriefAnimation()
                    OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks.[i,j] <- true
                    owUpdateFunctions.[i,j] 0 null  // redraw tile, with icon shown
                    anyFound <- true
        // (no 'none found' icon here: hovering the Hint Decoder button with no hint shops marked just shows nothing)
        ignore anyFound
        )
    // hovering the magic key in the item progress bar: highlight every tile that has a key marker icon (magic key or old key) on it
    showKeyLocator <- (fun () ->
        OverworldRouteDrawing.routeDrawingLayer.Clear()
        let mutable anyFound = false
        for i = 0 to 15 do
            for j = 0 to 7 do
                let hasKey = visibleDecorIcons(TrackerModel.owSwordIcons.[i,j]) |> List.exists isKeyDecor
                if hasKey then
                    owLocatorTileRectangles.[i,j].MakeGreenWithBriefAnimation()
                    anyFound <- true
        if not(anyFound) then
            showLocatorNoneFound()
        )
    showSwordLocator <- (fun () ->
        OverworldRouteDrawing.routeDrawingLayer.Clear()
        let mutable anyFound = false
        for i = 0 to 15 do
            for j = 0 to 7 do
                if TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]) |> List.exists (fun id -> id = TrackerModel.ITEMS.WOODSWORD || id = TrackerModel.ITEMS.PROGRESSIVESWORD || id = TrackerModel.ITEMS.MAGICALSWORD || id = TrackerModel.ITEMS.WHITESWORD4) then
                    owLocatorTileRectangles.[i,j].MakeGreenWithBriefAnimation()
                    anyFound <- true
        if not(anyFound) then
            showLocatorNoneFound()
        )
    showMarkerItemsLocator <- (fun idents ->
        for i = 0 to 15 do
            for j = 0 to 7 do
                let hasDecor = visibleDecorIcons(TrackerModel.owSwordIcons.[i,j]) |> List.exists (fun id -> List.contains id idents)
                let cur = TrackerModel.overworldMapMarks.[i,j].Current()
                let inBox(b:TrackerModel.Box) = List.contains (b.CellCurrent()) idents
                let hasBox = (cur = TrackerModel.MapSquareChoiceDomainHelper.ARMOS && inBox TrackerModel.armosBox) ||
                             (cur = TrackerModel.MapSquareChoiceDomainHelper.SWORD2 && inBox TrackerModel.sword2Box) ||
                             (cur = TrackerModel.MapSquareChoiceDomainHelper.SWORD3 && inBox TrackerModel.sword3Box) ||
                             (cur = TrackerModel.MapSquareChoiceDomainHelper.SWORD1 && TrackerModel.shuffledWoodSwordTiles.Contains((i,j)) && inBox TrackerModel.sword1Box)   // shuffled wood sword cave item
                if hasDecor || hasBox then
                    owLocatorTileRectangles.[i,j].MakeGreenWithBriefAnimation()
        )
    showArrowLocator <- (fun () ->
        OverworldRouteDrawing.routeDrawingLayer.Clear()
        let mutable anyFound = false
        for i = 0 to 15 do
            for j = 0 to 7 do
                if visibleDecorIcons(TrackerModel.owSwordIcons.[i,j]) |> List.exists (fun id -> id = TrackerModel.ITEMS.WOODARROW || id = TrackerModel.ITEMS.SILVERARROW) then
                    owLocatorTileRectangles.[i,j].MakeGreenWithBriefAnimation()
                    anyFound <- true
        if not(anyFound) then
            showLocatorNoneFound()
        )
    showMeatLocator <- (fun () ->
        OverworldRouteDrawing.routeDrawingLayer.Clear()
        let mutable anyFound = false
        for i = 0 to 15 do
            for j = 0 to 7 do
                if TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]) |> List.contains TrackerModel.ITEMS.MEAT then
                    owLocatorTileRectangles.[i,j].MakeGreenWithBriefAnimation()
                    anyFound <- true
        if not(anyFound) then
            showLocatorNoneFound()
        )
    showBombMarkerLocator <- (fun () ->
        // no Clear() here: this runs right after the bomb-shop locator, and adds the tiles where you placed a bomb marker icon
        for i = 0 to 15 do
            for j = 0 to 7 do
                if TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[i,j]) |> List.contains TrackerModel.DECOR_BOMB then
                    owLocatorTileRectangles.[i,j].MakeGreenWithBriefAnimation()
                if TrackerModel.owBombDroppers.[i,j] then
                    owLocatorTileRectangles.[i,j].MakeRedWithBriefAnimation()   // bomb dropper screens get a red border
        )
    showShopLocatorInstanceFunc <- (fun item ->
        // (the existing overworld highlighting (open cave boxes, routes) stays while hovering arrow / key / meat)
        let mutable anyFound = false
        for i = 0 to 15 do
            for j = 0 to 7 do
                let cur = TrackerModel.overworldMapMarks.[i,j].Current()
                if MapStateProxy(cur).IsThreeItemShop &&
                        (cur = item || TrackerModel.getShopItem2_1based(i,j) = TrackerModel.MapSquareChoiceDomainHelper.ToItem(item) || TrackerModel.getShopItem3_1based(i,j) = TrackerModel.MapSquareChoiceDomainHelper.ToItem(item)) then
                    owLocatorTileRectangles.[i,j].MakeGreenWithBriefAnimation()
                    OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks.[i,j] <- true
                    owUpdateFunctions.[i,j] 0 null  // redraw tile, with icon shown
                    anyFound <- true
        if not(anyFound) then
            showLocatorNoneFound()
        )
    showLocatorPotionAndTakeAny <- (fun () ->
        OverworldRouteDrawing.routeDrawingLayer.Clear()
        let mutable anyFound = false
        for i = 0 to 15 do
            for j = 0 to 7 do
                let cur = TrackerModel.overworldMapMarks.[i,j].Current()
                if cur = TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP || 
                    (cur = TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY && TrackerModel.getOverworldMapExtraData(i,j,cur)<>cur) then
                    owLocatorTileRectangles.[i,j].MakeGreenWithBriefAnimation()
                    OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks.[i,j] <- true  // probably unnecessary, as these can't be hidden
                    owUpdateFunctions.[i,j] 0 null  // redraw tile, with icon shown
                    anyFound <- true
        if not(anyFound) then
            showLocatorNoneFound()
        )
    showLocatorRupees <- (fun () ->
        OverworldRouteDrawing.routeDrawingLayer.Clear()
        let mutable anyFound = false
        for i = 0 to 15 do
            for j = 0 to 7 do
                let cur = TrackerModel.overworldMapMarks.[i,j].Current()
                if cur = TrackerModel.MapSquareChoiceDomainHelper.MONEY_MAKING_GAME || cur = TrackerModel.MapSquareChoiceDomainHelper.UNKNOWN_SECRET ||
                            (cur = TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET && TrackerModel.getOverworldMapExtraData(i,j,cur)<>0) ||
                            (cur = TrackerModel.MapSquareChoiceDomainHelper.MEDIUM_SECRET && TrackerModel.getOverworldMapExtraData(i,j,cur)<>0) ||
                            (cur = TrackerModel.MapSquareChoiceDomainHelper.SMALL_SECRET && TrackerModel.getOverworldMapExtraData(i,j,cur)<>0) then
                    owLocatorTileRectangles.[i,j].MakeGreenWithBriefAnimation()
                    OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks.[i,j] <- true
                    owUpdateFunctions.[i,j] 0 null  // redraw tile, with icon shown
                    anyFound <- true
        if not(anyFound) then
            showLocatorNoneFound()
        )
    recorderDestinationButtonCanvas.MouseEnter.Add(fun _ ->
        OverworldRouteDrawing.routeDrawingLayer.Clear()
        for i = 0 to 15 do
            for j = 0 to 7 do
                // Note: in HDN, you might have found dungeon G, but if you have starting triforce 4, and dunno if 4=G, we don't know if can recorder there
                if TrackerModel.playerComputedStateSummary.HaveRecorder && OverworldRouting.recorderDests |> Seq.contains (i,j) then
                    owLocatorTileRectangles.[i,j].MakeGreenWithBriefAnimation()
        )
    recorderDestinationButtonCanvas.MouseLeave.Add(fun _ -> hideLocator())
    anyRoadLegendIcon.MouseEnter.Add(fun _ ->
        OverworldRouteDrawing.routeDrawingLayer.Clear()
        for i = 0 to 15 do
            for j = 0 to 7 do
                let cur = TrackerModel.overworldMapMarks.[i,j].Current()
                if cur >= TrackerModel.MapSquareChoiceDomainHelper.WARP_1 && cur <= TrackerModel.MapSquareChoiceDomainHelper.WARP_4 then
                    owLocatorTileRectangles.[i,j].MakeGreenWithBriefAnimation()
        )
    anyRoadLegendIcon.MouseLeave.Add(fun _ -> hideLocator())
    dungeonLegendIconArea.MouseEnter.Add(fun _ -> contentCanvasMouseEnterFunc(10))   // the 'show all dungeons' functionality
    dungeonLegendIconArea.MouseLeave.Add(fun _ -> hideLocator())
    showLocator <- (fun sld ->
        match sld with
        | ShowLocatorDescriptor.DungeonNumber(n) ->
            let mutable index = -1
            for i = 0 to 7 do
                if TrackerModel.GetDungeon(i).LabelChar = char(int '1' + n) then
                    index <- i
            let showHint() =
                let hinted_zone = TrackerModel.GetLevelHint(n)
                if hinted_zone <> TrackerModel.HintZone.UNKNOWN then
                    showLocatorHintedZone(hinted_zone,true)
            if index <> -1 then
                let loc = TrackerModel.mapStateSummary.DungeonLocations.[index]
                if loc <> TrackerModel.NOTFOUND then
                    showLocatorExactLocation(loc)
                else
                    showHint()
            else
                showHint()
        | ShowLocatorDescriptor.DungeonIndex(i) ->
            let loc = TrackerModel.mapStateSummary.DungeonLocations.[i]
            if loc <> TrackerModel.NOTFOUND then
                showLocatorExactLocation(loc)
            else
                if TrackerModel.IsHiddenDungeonNumbers() && i<>8 then   // Level 9 works like non-HDN
                    let label = TrackerModel.GetDungeon(i).LabelChar
                    if label >= '1' && label <= '8' then
                        let index = int label - int '1'
                        let hinted_zone = TrackerModel.GetLevelHint(index)
                        if hinted_zone <> TrackerModel.HintZone.UNKNOWN then
                            showLocatorHintedZone(hinted_zone,true)
                else
                    let hinted_zone = TrackerModel.GetLevelHint(i)
                    if hinted_zone <> TrackerModel.HintZone.UNKNOWN then
                        showLocatorHintedZone(hinted_zone,false)
        | ShowLocatorDescriptor.Sword1 ->
            let loc = TrackerModel.mapStateSummary.Sword1Location
            if loc <> TrackerModel.NOTFOUND then
                showLocatorExactLocation(loc)
        | ShowLocatorDescriptor.Sword2 ->
            let loc = TrackerModel.mapStateSummary.Sword2Location
            if loc <> TrackerModel.NOTFOUND then
                showLocatorExactLocation(loc)
            else
                let hinted_zone = TrackerModel.GetLevelHint(9)
                if hinted_zone <> TrackerModel.HintZone.UNKNOWN then
                    showLocatorHintedZone(hinted_zone,false)
        | ShowLocatorDescriptor.Sword3 ->
            let loc = TrackerModel.mapStateSummary.Sword3Location
            if loc <> TrackerModel.NOTFOUND then
                showLocatorExactLocation(loc)
            else
                let hinted_zone = TrackerModel.GetLevelHint(10)
                if hinted_zone <> TrackerModel.HintZone.UNKNOWN then
                    showLocatorHintedZone(hinted_zone,false)
        )
    let currentTargetGhostBuster = makeGhostBusterImpl(Brushes.Red, true)
    currentTargetGhostBuster.Opacity <- 0.
    layout.AddLinkTarget(currentTargetGhostBuster)
    showLocatorNoneFound <- (fun () ->
        ()   // the 'none found' ghostbuster icon is intentionally no longer shown when a hover locator finds nothing
        // ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes()  // do not do this, we want the map to be blank, to make the ghostbuster 'pop' more and the non-found-ness to be more apparent
        )
    hideLocator <- (fun () ->
//        printfn "some other HL: %s" ((new System.Diagnostics.StackTrace()).ToString())        // this is super useful for debugging
        currentCCLevel <- -1
        if not zone_checkbox.IsChecked.HasValue || not zone_checkbox.IsChecked.Value then changeZoneOpacity(TrackerModel.HintZone.UNKNOWN,false)
        allOwMapZoneBlackCanvases |> Array2D.iteri (fun _x _y zbc -> zbc.Opacity <- 0.0)
        for i = 0 to 15 do
            for j = 0 to 7 do
                owLocatorTileRectangles.[i,j].Hide()
                if OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks.[i,j] then
                    OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks.[i,j] <- false
                    owUpdateFunctions.[i,j] 0 null  // redraw tile, with icon possibly hidden
        owLocatorCanvas.Children.Clear()
        currentTargetGhostBuster.Opacity <- 0.
        ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes()
        )

    // hovering the candle in the item progress bar circles every candle marked on the map: blue circles for blue candles, red circles for red candles
    let mutable candleCirclesShown = false
    let showCandleCircles() =
        let tileH = 11.*3.
        let circle(i:int, j:int, brush:Brush) =
            let d = 26.
            let e = new System.Windows.Shapes.Ellipse(Width=d, Height=d, Stroke=brush, StrokeThickness=3., IsHitTestVisible=false)
            canvasAdd(owLocatorCanvas, e, float i*OMTW + (OMTW-d)/2., float j*tileH + (tileH-d)/2.)
        for i = 0 to 15 do
            for j = 0 to 7 do
                let isBlue, isRed =
                    let ids = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons.[i,j]) |> List.map fst
                    (List.contains TrackerModel.ITEMS.BLUECANDLE ids || TrackerModel.overworldMapMarks.[i,j].Current() = TrackerModel.MapSquareChoiceDomainHelper.BLUE_CANDLE),
                    List.contains TrackerModel.ITEMS.REDCANDLE ids
                if isBlue then circle(i, j, Brushes.DodgerBlue)
                if isRed then circle(i, j, Brushes.Red)
    let updateCandleHover(overCandle:bool) =
        if overCandle && not candleCirclesShown then
            candleCirclesShown <- true
            owLocatorCanvas.Children.Clear()
            showCandleCircles()
        elif not overCandle && candleCirclesShown then
            candleCirclesShown <- false
            hideLocator()
    itemProgressCanvas.MouseMove.Add(fun ea ->
        let slot = int (floor ((ea.GetPosition(itemProgressCanvas).X - OverworldItemGridUI.ITEM_PROGRESS_FIRST_ITEM) / 30.))
        updateCandleHover(slot = 7))
    itemProgressCanvas.MouseLeave.Add(fun _ -> updateCandleHover(false))
    addZoneName(TrackerModel.HintZone.DEATH_MOUNTAIN, "DEATH\nMOUNTAIN", 2.5, 0.3)
    addZoneName(TrackerModel.HintZone.GRAVE,          "GRAVE", 1.5, 2.8)
    addZoneName(TrackerModel.HintZone.DEAD_WOODS,     "DEAD\nWOODS", 1.4, 5.3)
    addZoneName(TrackerModel.HintZone.LAKE,           "LAKE 1", 10.2, 0.1)
    addZoneName(TrackerModel.HintZone.LAKE,           "LAKE 2", 5.5, 3.5)
    addZoneName(TrackerModel.HintZone.LAKE,           "LAKE 3", 9.4, 5.5)
    addZoneName(TrackerModel.HintZone.RIVER,          "RIVER 1", 7.3, 1.1)
    addZoneName(TrackerModel.HintZone.RIVER,          "RIV\nER2", 5.1, 6.2)
    addZoneName(TrackerModel.HintZone.NEAR_START,     "START", 7.3, 6.2)
    addZoneName(TrackerModel.HintZone.DESERT,         "DESERT", 10.3, 3.1)
    addZoneName(TrackerModel.HintZone.FOREST,         "FOREST", 12.3, 5.1)
    addZoneName(TrackerModel.HintZone.LOST_HILLS,     "LOST\nHILLS", 12.4, 0.3)
    addZoneName(TrackerModel.HintZone.COAST,          "COAST", 14.3, 2.7)
    if TrackerModelOptions.Overworld.Zones.Value then
        changeZoneOpacity(TrackerModel.HintZone.UNKNOWN,true)

    // timeline, options menu, reminders
    let moreOptionsButton = Graphics.makeButton("Options", Some(12.), Some(Brushes.Orange))
    moreOptionsButton.MaxHeight <- 25.
    moreOptionsButton.Measure(new Size(System.Double.PositiveInfinity, 25.))

    let optionsCanvas = OptionsMenu.makeOptionsCanvas(cm, true, isStandardHyrule)
    optionsCanvas.Opacity <- 1.
    optionsCanvas.IsHitTestVisible <- true

    let drawButton = Graphics.makeButton("Draw + Custom Icons", Some(12.), Some(Brushes.Orange))
    drawButton.MaxHeight <- 22.
    if layout.IsShort then 
        drawButton.Opacity <- 0. // draw button disabled in short layout
        drawButton.IsEnabled <- false  // make not clickable (even if invisible)

    let theTimeline1 = new Timeline.Timeline(21., 4, appMainCanvas.Width-48., 1, [|"0:00";"0:10";"0:20";"0:30";"0:40";"0:50";"1:00"|], moreOptionsButton, drawButton)
    let theTimeline2 = new Timeline.Timeline(21., 4, appMainCanvas.Width-48., 2, [|"0:00";"0:20";"0:40";"1:00";"1:20";"1:40";"2:00"|], moreOptionsButton, drawButton)
    let theTimeline3 = new Timeline.Timeline(21., 4, appMainCanvas.Width-48., 3, [|"0:00";"0:30";"1:00";"1:30";"2:00";"2:30";"3:00"|], moreOptionsButton, drawButton)
    theTimeline1.ThisIsNowTheVisibleTimeline()
    theTimeline1.Canvas.Opacity <- 1.
    theTimeline2.Canvas.Opacity <- 0.
    theTimeline3.Canvas.Opacity <- 0.
    let drawTimeline(minute) =
        if minute <= 60 then
            theTimeline1.ThisIsNowTheVisibleTimeline()
            theTimeline1.Canvas.Opacity <- 1.
            theTimeline2.Canvas.Opacity <- 0.
            theTimeline3.Canvas.Opacity <- 0.
            theTimeline1.Update(minute, timelineItems, maxOverworldRemain)
        elif minute <= 120 then
            theTimeline1.Canvas.Opacity <- 0.
            theTimeline2.ThisIsNowTheVisibleTimeline()
            theTimeline2.Canvas.Opacity <- 1.
            theTimeline3.Canvas.Opacity <- 0.
            theTimeline2.Update(minute, timelineItems, maxOverworldRemain)
        else
            theTimeline1.Canvas.Opacity <- 0.
            theTimeline2.Canvas.Opacity <- 0.
            theTimeline3.ThisIsNowTheVisibleTimeline()
            theTimeline3.Canvas.Opacity <- 1.
            theTimeline3.Update(minute, timelineItems, maxOverworldRemain)
    TrackerModel.TimelineItemModel.TimelineChanged.Add(drawTimeline)
    for j = 0 to 7 do
        TrackerModel.GetDungeon(j).HiddenDungeonColorOrLabelChanged.Add(fun _ -> TrackerModel.TimelineItemModel.TriggerTimelineChanged()) // e.g. to change heart label from E -> 3
    OptionsMenu.secondQuestDungeonsOptionChanged.Publish.Add(fun _ -> TrackerModel.TimelineItemModel.TriggerTimelineChanged())  // e.g. to change heart label from 1 -> 4
    TrackerModel.IsWSMSReplacedByBUChanged.Add(fun _ -> TrackerModel.TimelineItemModel.TriggerTimelineChanged())  // e.g. change mags icon to BU

    moreOptionsButton.Click.Add(fun _ -> 
        if not popupIsActive then
            popupIsActive <- true
            let wh = new System.Threading.ManualResetEvent(false)
            async {
                // the options popup opens right below the overworld tiles (moved up only as far as needed to still fit in the window)
                optionsCanvas.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
                let owBottom = (try overworldCanvas.TranslatePoint(Point(0., overworldCanvas.ActualHeight), cm.AppMainCanvas).Y with _ -> 0.)
                let popupY = max 0. (min owBottom (cm.Height - optionsCanvas.DesiredSize.Height))
                do! CustomComboBoxes.DoModal(cm, wh, 0., popupY, optionsCanvas)
                TrackerModelOptions.writeSettings()
                popupIsActive <- false
                } |> Async.StartImmediate
        )
    layout.AddTimeline(theTimeline1.Canvas, theTimeline2.Canvas, theTimeline3.Canvas)
    drawButton.Click.Add(fun _ -> 
        if not popupIsActive then
            popupIsActive <- true
            async {
                do! DrawingLayer.InteractWithDrawingLayer(cm, drawingCanvas)
                popupIsActive <- false
                } |> Async.StartImmediate
        )

    let reminderDisplayOuterDockPanel = SetupReminderDisplayAndProcessing(cm)
    let drawButtonText = drawButton.Content :?> TextBox
    drawButtonText.Text <- "Draw/Custom Icons"   // shortened to fit the narrow button stack
    layout.AddTopLeftButtons(moreOptionsButton, drawButton, Reminders.reminderLogButton)
    layout.AddReminderDisplayOverlay(reminderDisplayOuterDockPanel)

    // postgameDecorationCanvas atop timeline & reminders
    do
        let postgameDecorationCanvas = new Canvas(Width=appMainCanvas.Width, Opacity=0., IsHitTestVisible=false)   // no hit test so it doesn't absorb clicks to reminder log
        layout.AddPostGameDecorationCanvas(postgameDecorationCanvas)
        let sp = new StackPanel(Orientation=Orientation.Horizontal, Background=Brushes.Black, Height=24.)  // height to cover the "log" button
        Canvas.SetRight(sp, 0.)
        Canvas.SetTop(sp, 0.)
        postgameDecorationCanvas.Children.Add(sp) |> ignore
        let makeViewRectImpl(c:FrameworkElement) = new Shapes.Rectangle(Width=c.Width, Height=c.Height, Fill=new VisualBrush(c))
        let timerClone = makeViewRectImpl(hmsTimeTextBox)
        let scaleTrans = new ScaleTransform(20./timerClone.Height, 20./timerClone.Height)
        if scaleTrans.CanFreeze then
            scaleTrans.Freeze()
        timerClone.LayoutTransform <- scaleTrans
        sp.Children.Add(timerClone) |> ignore
        sp.Children.Add(makeViewRectImpl(owRemainingScreensTextBox)) |> ignore
        let screenshotTimelineFilename = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "most-recent-completion-timeline.png")
        let screenshotFullFilename = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "most-recent-completion-full-screenshot.png")
        TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Changed.Add(fun b -> 
            if b then
                postgameDecorationCanvas.Opacity <- 1.
                if not(Timeline.isCurrentlyLoadingASave) then
                    async {
                        // select the dungeon summary tab, so everything on-screen at once
                        selectDungeonTabEvent.Trigger(9)  
                        if TrackerModelOptions.ShorterAppWindow.Value then
                            Graphics.NavigationallyWarpMouseCursorTo(dungeonTabs.TranslatePoint(posToWarpToWhenTabbingFromOverworld, appMainCanvas))
                            layout.FocusDungeon()
                        // wait for finish drawing (e.g. zelda is not yet drawn on the timeline)
                        do! Async.Sleep(10)  
                        do! Async.SwitchToContext(ctxt)
                        try
                            for filename, viewboxF in [screenshotFullFilename, (fun () -> layout.GetFullAppBounds())
                                                       screenshotTimelineFilename, (fun () -> layout.GetTimelineBounds())
                                                       ] do
                                // screenshot timeline region
                                let vb = new VisualBrush(cm.RootCanvas.Parent :?> Canvas)    // whole canvas (parent of root), so that we can capture the timer display in the full screenshot
                                vb.ViewboxUnits <- BrushMappingMode.Absolute
                                let orig = viewboxF()
                                let mutable rect = viewboxF()
                                vb.Stretch <- Stretch.Uniform
                                let factor = 
                                    if Graphics.appScale() <> 1.0 then 
                                        let r = Graphics.appScale() 
                                        rect.Location <- Point(rect.Left * r, rect.Top * r)
                                        rect.Size <- Size(rect.Width * r, rect.Height * r)
                                        r
                                    else 1.0
                                vb.Viewbox <- rect
                                let visual = new DrawingVisual(Transform=new ScaleTransform(1. / factor, 1. / factor))
                                do 
                                    use dc = visual.RenderOpen()
                                    dc.DrawRectangle(vb, null, Rect(Size(vb.Viewbox.Width, vb.Viewbox.Height)))
                                let bitmap = new Imaging.RenderTargetBitmap(int(orig.Width), int(orig.Height), 96., 96., PixelFormats.Default)
                                bitmap.Render(visual)
                                let encoder = new Imaging.PngBitmapEncoder()
                                encoder.Frames.Add(Imaging.BitmapFrame.Create(bitmap))
                                do
                                    use stream = System.IO.File.Create(filename)
                                    encoder.Save(stream)
                                System.IO.File.SetCreationTime(filename, System.DateTime.Now)
                                System.IO.File.SetLastWriteTime(filename, System.DateTime.Now)
                        with e ->
                            printfn "%s" (e.ToString())
                    } |> Async.StartImmediate
            else
                postgameDecorationCanvas.Opacity <- 0.
            )

    let refocusKeyboard() = 
        let w = Window.GetWindow(appMainCanvas)
        Input.Keyboard.ClearFocus()                // ensure that clicks outside the Notes area de-focus it, by clearing keyboard focus...
        Input.FocusManager.SetFocusedElement(w, null)  // ... and by removing its logical mouse focus
        Input.Keyboard.Focus(w) |> ignore  // refocus keyboard to main window so MyKey still works
    appMainCanvas.MouseDown.Add(fun _ -> refocusKeyboard())

    do! showProgress("broadcast/load/animation")

    // broadcast window
    Broadcast.MakeBroadcastWindow(cm, drawingCanvas, blockerGrid, dungeonTabsOverlayContent, refocusMainWindow)

#if NOT_RACE_LEGAL
    HUDs.MakeHUDs(cm, trackerDungeonMoused, trackerLocationMoused)
#endif

    layout.AddSpotSummary(spotSummaryCanvas)

    // poke loaded data values
    match loadData with
    | Some(data) ->
        // Overworld
        TrackerModel.MirrorOverworld <- data.Overworld.MirrorOverworld
        TrackerModel.startIconX <- data.Overworld.StartIconX
        TrackerModel.startIconY <- data.Overworld.StartIconY
        TrackerModel.customWaypointX <- data.Overworld.CustomWaypointX
        TrackerModel.customWaypointY <- data.Overworld.CustomWaypointY
        let a = data.Overworld.Map
        if data.Overworld.SwordIcons <> null && data.Overworld.SwordIcons.Length = 16 * 8 then
            for j = 0 to 7 do
                for i = 0 to 15 do
                    TrackerModel.owSwordIcons.[i,j] <- data.Overworld.SwordIcons.[j*16+i]
                    TrackerModel.owBombDroppers.[i,j] <- false
            // (older saves kept bomb droppers as a marker icon: they move to their own layer)
            for j = 0 to 7 do
                for i = 0 to 15 do
                    let es = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons.[i,j])
                    if es |> List.exists (fun (id,_) -> id = TrackerModel.DECOR_BOMB_DROPPER) then
                        TrackerModel.owBombDroppers.[i,j] <- true
                        let label = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[i,j])
                        let packed = TrackerModel.decorEntriesPack(es |> List.filter (fun (id,_) -> id <> TrackerModel.DECOR_BOMB_DROPPER))
                        TrackerModel.owSwordIcons.[i,j] <- (if label > 0 then TrackerModel.withShopLabel(packed, label) else packed)
            for k = 0 to 5 do TrackerModel.shopStash.[k] <- -1
            if data.Overworld.ShopStash <> null && data.Overworld.ShopStash.Length = 6 then
                for k = 0 to 5 do TrackerModel.shopStash.[k] <- data.Overworld.ShopStash.[k]
            if data.Overworld.BombDroppers <> null && data.Overworld.BombDroppers.Length = 16 * 8 then
                for j = 0 to 7 do
                    for i = 0 to 15 do
                        if data.Overworld.BombDroppers.[j*16+i] <> 0 then TrackerModel.owBombDroppers.[i,j] <- true
            rebuildMapCollected()
        if a.Length <> 16 * 8 * 3 then
            failwith "bad load data at data.Overworld.Map"
        Timeline.isCurrentlyLoadingASave <- true
        TrackerModel.currentlyIgnoringForceUpdatesDuringALoad <- true
        let mutable anySetProblems = false
        for j = 0 to 7 do
            for i = 0 to 15 do
                let cur    = a.[j*16*3 + i*3]
                let ed     = a.[j*16*3 + i*3 + 1]
                let circle = a.[j*16*3 + i*3 + 2]
                if not(TrackerModel.overworldMapMarks.[i,j].AttemptToSet(cur)) then
                    anySetProblems <- true
                let k = if TrackerModel.MapSquareChoiceDomainHelper.IsItem(cur) then TrackerModel.MapSquareChoiceDomainHelper.SHOP else cur
                if k <> -1 then
                    TrackerModel.setOverworldMapExtraData(i,j,k,ed)
                TrackerModel.overworldMapCircles.[i,j] <- circle
                owUpdateFunctions.[i,j] 0 null  // redraw the tile
                owCircleRedraws.[i,j]()
        do! showProgress(sprintf "finished loading overworld")
        // Items
        if not(data.Items.WhiteSwordBox.TryApply(TrackerModel.sword2Box)) then anySetProblems <- true
        if not(data.Items.LadderBox.TryApply(TrackerModel.ladderBox)) then anySetProblems <- true
        if not(data.Items.ArmosBox.TryApply(TrackerModel.armosBox)) then anySetProblems <- true
        // the items found in the magical sword / letter / wood sword caves (older saves do not have these)
        if data.Items.MagicalSwordBox <> null then
            if not(data.Items.MagicalSwordBox.TryApply(TrackerModel.sword3Box)) then anySetProblems <- true
        if data.Items.LetterBox <> null then
            if not(data.Items.LetterBox.TryApply(TrackerModel.letterBox)) then anySetProblems <- true
        if data.Items.WoodSwordBox <> null then
            if not(data.Items.WoodSwordBox.TryApply(TrackerModel.sword1Box)) then anySetProblems <- true
        TrackerModel.hintShopColors.Clear()
        if not(System.String.IsNullOrEmpty(data.Items.HintShopColors)) then
            for t in data.Items.HintShopColors.Split([|';'|], System.StringSplitOptions.RemoveEmptyEntries) do
                let p = t.Split(',')
                if p.Length = 3 then
                    TrackerModel.hintShopColors.[(int p.[0], int p.[1])] <- int p.[2]
        TrackerModel.shuffledWoodSwordTiles.Clear()
        if not(System.String.IsNullOrEmpty(data.Items.ShuffledWoodSwordTiles)) then
            for t in data.Items.ShuffledWoodSwordTiles.Split([|';'|], System.StringSplitOptions.RemoveEmptyEntries) do
                let p = t.Split(',')
                if p.Length = 2 then
                    TrackerModel.shuffledWoodSwordTiles.Add((int p.[0], int p.[1])) |> ignore
        OptionsMenu.requestRedrawOverworldEvent.Trigger()   // redraw the tiles now that the cave items are back
        if not(System.String.IsNullOrEmpty(data.Items.CustomLevelName)) then
            TrackerModel.CustomLevelName <- data.Items.CustomLevelName
            DungeonUI.customLevelNameChanged.Trigger()   // the custom LEVEL name that was in use when this was saved
        if data.Items.Level2StairBox <> null then   // (older saves do not have the optional level 2 staircase slot)
            if not(data.Items.Level2StairBox.TryApply(TrackerModel.level2StairBox)) then anySetProblems <- true
            TrackerModel.SetLevel2StairBoxEnabled(data.Items.Level2StairBoxEnabled)
            TrackerModel.SetLevel9FloorBoxEnabled(data.Items.Level9FloorBoxEnabled)   // (older saves: false)
            TrackerModelOptions.FourthSwordEnabled <- data.Items.Level2StairBoxEnabled || data.Items.Level9FloorBoxEnabled
        else
            TrackerModelOptions.FourthSwordEnabled <- true   // older saves predate this option: nothing is restricted
        for i = 0 to 8 do
            let ds = data.Items.Dungeons.[i]
            let dd = TrackerModel.GetDungeon(i)
            if not(ds.TryApply(dd)) then anySetProblems <- true
        do! showProgress(sprintf "finished loading items")
        // PlayerProgressAndTakeAnyHearts
        data.PlayerProgressAndTakeAnyHearts.Apply()
        // StartingItemsAndExtras
        data.StartingItemsAndExtras.Apply()
        // Blockers
        TrackerModel.DungeonBlockersContainer.StartIgnoreChangesDuringLoad()
        for i = 0 to 7 do
            for j = 0 to TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON-1 do
                TrackerModel.DungeonBlockersContainer.SetDungeonBlocker(i,j,TrackerModel.DungeonBlocker.FromHotKeyName(data.Blockers.[i].[j].Kind))
                for k = 0 to TrackerModel.DungeonBlockerAppliesTo.MAX-1 do
                    TrackerModel.DungeonBlockersContainer.SetDungeonBlockerAppliesTo(i,j,k,data.Blockers.[i].[j].AppliesTo.[k])
            do! showProgress(sprintf "finished loading blockers %d of 8" (i+1))
        TrackerModel.DungeonBlockersContainer.FinishIgnoreChangesDuringLoad()
        if anySetProblems then
            () // TODO
        // Hints
        for i = 0 to 10 do
            TrackerModel.SetLevelHint(i, TrackerModel.HintZone.FromIndex(data.Hints.LocationHints.[i]))
        TrackerModel.NoFeatOfStrengthHintWasGiven <- data.Hints.NoFeatOfStrengthHint
        TrackerModel.SailNotHintWasGiven <- data.Hints.SailNotHint
        TrackerModel.SailToHintWasGiven <- data.Hints.SailToHint
        TrackerModel.PlayMelodyHintWasGiven <- data.Hints.PlayMelodyHint
        TrackerModel.StepOverWaterHintWasGiven <- data.Hints.StepOverWaterHint
        TrackerModel.FireArrowsHintWasGiven <- data.Hints.FireArrowsHint
        hideFeatsOfStrength TrackerModel.NoFeatOfStrengthHintWasGiven
        hideRaftSpots TrackerModel.SailNotHintWasGiven
        Views.hintDashedItemsChanged.Trigger()
        // Notes
        notesTextBox.Text <- data.Notes
        // CRDI
        currentRecorderDestinationIndex <- data.CurrentRecorderDestinationIndex
        TrackerModel.recorderToNewDungeons <- data.RecorderToNewDungeons
        TrackerModel.recorderToUnbeatenDungeons <- data.RecorderToUnbeatenDungeons
        if data.IsBoomstickSeed then
            toggleBookShieldCheckBox.IsChecked <- System.Nullable.op_Implicit true
        if data.IsAtlasSeed then
            bookIsAtlasCheckBox.IsChecked <- System.Nullable.op_Implicit true
        if data.IsWSMSReplacedByBUSeed then
            TrackerModel.ToggleWSMSReplacedByBU()
        updateCurrentRecorderDestinationNumeral()
        // Dungeon Maps
        do! importDungeonModels(showProgress, data.DungeonMaps)
        DungeonUI.theDungeonTabControl.SelectedIndex <- 0   // if they selected the summary tab, it only updates after an actual change, so poke tab 0 first, so the change occurs
        DungeonUI.theDungeonTabControl.SelectedIndex <- data.DungeonTabSelected
        // UserCustom
        if data.UserCustomChecklist <> null && data.UserCustomChecklist.Items <> null then
            SaveAndLoad.theUserCustomChecklist <- data.UserCustomChecklist
            // note that line below must happen before we populate Timeline further below, as it updates TrackerModel.TimelineItemModel.All to include UserCustom stuff
            do! UserCustomLayer.InitializeUserCustom(cm, timelineItems, invokeExtras)
        // Drawing Layer
        DrawingLayer.LoadDrawingLayer(data.DrawingLayerIcons, drawingCanvas)
        // Graphics.alternativeOverworldMapFilename & Graphics.shouldInitiallyHideOverworldMap were loaded much earlier
        // Seed & Flags
        if data.Seed <> null && data.Seed <> "" then
            SaveAndLoad.lastKnownSeed <- data.Seed
            SaveAndLoad.seedAndFlagsUpdated.Trigger()
        if data.Flags <> null && data.Flags <> "" then
            SaveAndLoad.lastKnownFlags <- data.Flags
            SaveAndLoad.seedAndFlagsUpdated.Trigger()
        // Timeline
        if data.OverworldSpotsRemainingOverTime <> null then
            for i = 0 to (data.OverworldSpotsRemainingOverTime.Length/2)-1 do
                TrackerModel.timelineDataOverworldSpotsRemain.Add(data.OverworldSpotsRemainingOverTime.[i*2], data.OverworldSpotsRemainingOverTime.[i*2+1])
        if data.Timeline <> null then
            for td in data.Timeline do
                if td.Seconds <= data.TimeInSeconds then
                    match TrackerModel.TimelineItemModel.All.TryGetValue(td.Ident) with
                    | true, v -> v.StampTotalSeconds(td.Seconds, TrackerModel.PlayerHas.FromInt(td.Has))
                    | _ -> ()
        drawTimeline(data.TimeInSeconds / 60)
        // Timer
        TrackerModel.theStartTime.SetAgo(TimeSpan.FromSeconds(float data.TimeInSeconds))
        // recompute everything, update UI
        TrackerModel.recomputePlayerStateSummary()
        TrackerModel.recomputeMapStateSummary()
        TrackerModel.recomputeWhatIsNeeded() |> ignore
        // done
        TrackerModel.currentlyIgnoringForceUpdatesDuringALoad <- false
        TrackerModel.forceUpdate()
        doUIUpdateEvent.Trigger()
        Timeline.isCurrentlyLoadingASave <- false
        TrackerModel.TimelineItemModel.TriggerTimelineChanged()  // redraw timeline once (redraws are ignored while loading)
    | _ ->
        ()

    // animation
    do
        let c(t) = Color.FromArgb(t,0uy,255uy,255uy)  // Color.FromArgb(t,255uy,165uy,0uy)
        let rgb = new RadialGradientBrush(GradientOrigin=Point(0.5,0.5), Center=Point(0.5,0.5), RadiusX=0.5, RadiusY=0.5)
        rgb.GradientStops.Add(new GradientStop(c(0uy), 0.0))
        rgb.GradientStops.Add(new GradientStop(c(0uy), 0.4))
        rgb.GradientStops.Add(new GradientStop(c(0uy), 1.0))

        let msDuration = 1500
        let ca = new Animation.ColorAnimation(From=Nullable<_>(c(0uy)), To=Nullable<_>(c(140uy)), Duration=new Duration(TimeSpan.FromMilliseconds(float msDuration)), AutoReverse=true)
        let owHighlightTile = new Shapes.Rectangle(Width=OMTW, Height=11. * 3., StrokeThickness = 0., Fill=rgb, Opacity=1.0, IsHitTestVisible=false)
        canvasAdd(overworldCanvas, owHighlightTile, OMTW*float(6), float(11*3*6))

        let animateOWTile(x,y) = 
            if (x,y) <> TrackerModel.NOTFOUND then
                OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks.[x,y] <- true
                owUpdateFunctions.[x,y] 0 null  // redraw tile, with icon shown
                if TrackerModelOptions.AnimateTileChanges.Value then
                    Canvas.SetLeft(owHighlightTile, OMTW*float(x))
                    Canvas.SetTop(owHighlightTile, float(11*3*y))
                    rgb.GradientStops.[2].BeginAnimation(GradientStop.ColorProperty, ca)
                async {
                    do! Async.Sleep(msDuration)
                    do! Async.SwitchToContext(ctxt)
                    OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks.[x,y] <- false
                    owUpdateFunctions.[x,y] 0 null  // redraw tile, with icon possibly hidden
                } |> Async.StartImmediate
        animateOverworldTile <- animateOWTile

    TrackerModel.forceUpdate()
    timer.Start()  // don't start the tick timer updating, until the entire app is loaded
    do  // auto-save every 1 min
        let diskIcon = new Border(BorderThickness=Thickness(3.), BorderBrush=Brushes.Gray, Background=Brushes.Gray, Child=Graphics.BMPtoImage(Graphics.iconDisk_bmp), Opacity=0.0, IsHitTestVisible=false)
        layout.AddDiskIcon(diskIcon)
        let timer = new System.Windows.Threading.DispatcherTimer()
        timer.Interval <- TimeSpan.FromSeconds(10.0)   // (checks every 10 seconds; it saves once the Auto-save interval option has passed)
        let lastAutoSave = ref DateTime.Now
        timer.Tick.Add(fun _ -> 
          if (DateTime.Now - lastAutoSave.Value).TotalMinutes >= float TrackerModelOptions.AutoSaveMinutes - 0.01 then
            lastAutoSave.Value <- DateTime.Now
            try
                SaveAndLoad.SaveAll(notesTextBox.Text, DungeonUI.theDungeonTabControl.SelectedIndex, exportDungeonModelsJsonLines(), DungeonSaveAndLoad.SaveDrawingLayer(), 
                                        Graphics.alternativeOverworldMapFilename, Graphics.shouldInitiallyHideOverworldMap, currentRecorderDestinationIndex, 
                                        toggleBookShieldCheckBox.IsChecked.Value, bookIsAtlasCheckBox.IsChecked.Value, SaveAndLoad.AutoSave) |> ignore
                async {
                    diskIcon.Opacity <- 0.7
                    do! Async.Sleep(300)
                    do! Async.SwitchToContext ctxt
                    diskIcon.Opacity <- 0.4
                    do! Async.Sleep(300)
                    do! Async.SwitchToContext ctxt
                    diskIcon.Opacity <- 0.7
                    do! Async.Sleep(300)
                    do! Async.SwitchToContext ctxt
                    diskIcon.Opacity <- 0.0
                } |> Async.StartImmediate
            with e ->
                ()
        )
        timer.Start()

    let ARROW_DELTA = 20.
    appMainCanvas.MyKeyAdd(fun ea ->
        match HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key) with
        | Some(hotKeyedState) -> 
            ea.Handled <- true
            match hotKeyedState with
            | HotKeys.GlobalHotkeyTargets.ToggleMagicalSword -> TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Toggle()
            | HotKeys.GlobalHotkeyTargets.ToggleWoodSword    -> TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword.Toggle()
            | HotKeys.GlobalHotkeyTargets.ToggleBoomBook     -> TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBoomBook.Toggle()
            | HotKeys.GlobalHotkeyTargets.ToggleBlueCandle   -> TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle.Toggle()
            | HotKeys.GlobalHotkeyTargets.ToggleWoodArrow    -> TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodArrow.Toggle()
            | HotKeys.GlobalHotkeyTargets.ToggleBlueRing     -> TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueRing.Toggle()
            | HotKeys.GlobalHotkeyTargets.ToggleBombs        -> TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBombs.Toggle()
            | HotKeys.GlobalHotkeyTargets.ToggleGannon       -> TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasDefeatedGanon.Toggle()
            | HotKeys.GlobalHotkeyTargets.ToggleZelda        -> TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Toggle()
            | HotKeys.GlobalHotkeyTargets.DungeonTab1        -> selectDungeonTabEvent.Trigger(0)
            | HotKeys.GlobalHotkeyTargets.DungeonTab2        -> selectDungeonTabEvent.Trigger(1)
            | HotKeys.GlobalHotkeyTargets.DungeonTab3        -> selectDungeonTabEvent.Trigger(2)
            | HotKeys.GlobalHotkeyTargets.DungeonTab4        -> selectDungeonTabEvent.Trigger(3)
            | HotKeys.GlobalHotkeyTargets.DungeonTab5        -> selectDungeonTabEvent.Trigger(4)
            | HotKeys.GlobalHotkeyTargets.DungeonTab6        -> selectDungeonTabEvent.Trigger(5)
            | HotKeys.GlobalHotkeyTargets.DungeonTab7        -> selectDungeonTabEvent.Trigger(6)
            | HotKeys.GlobalHotkeyTargets.DungeonTab8        -> selectDungeonTabEvent.Trigger(7)
            | HotKeys.GlobalHotkeyTargets.DungeonTab9        -> selectDungeonTabEvent.Trigger(8)
            | HotKeys.GlobalHotkeyTargets.DungeonTabS        -> selectDungeonTabEvent.Trigger(9)
            // some buttons (e.g. Recorder Destination counter) are Focusable, and clicking them captures future keyboard input
            | HotKeys.GlobalHotkeyTargets.LeftClick          -> Graphics.Win32.LeftMouseClick(); refocusKeyboard()
            | HotKeys.GlobalHotkeyTargets.MiddleClick        -> Graphics.Win32.MiddleMouseClick(); refocusKeyboard()       
            | HotKeys.GlobalHotkeyTargets.RightClick         -> Graphics.Win32.RightMouseClick(); refocusKeyboard()       
            | HotKeys.GlobalHotkeyTargets.ScrollUp           -> Graphics.Win32.ScrollWheelRotateUp(); refocusKeyboard()       
            | HotKeys.GlobalHotkeyTargets.ScrollDown         -> Graphics.Win32.ScrollWheelRotateDown(); refocusKeyboard()       
            | HotKeys.GlobalHotkeyTargets.ToggleCursorOverworldOrDungeon ->
                if overworldCanvas.IsMouseOver then
                    let newPos = dungeonTabs.TranslatePoint(posToWarpToWhenTabbingFromOverworld, appMainCanvas)
                    layout.ShowMouseWarp(newPos)
                    Graphics.NavigationallyWarpMouseCursorTo(newPos)
                    layout.FocusDungeon()
                else
                    let newPos = overworldCanvas.TranslatePoint(Point(OMTW*8.5,11.*3.*4.5), appMainCanvas)
                    layout.ShowMouseWarp(newPos)
                    Graphics.NavigationallyWarpMouseCursorTo(newPos)
                    layout.FocusOverworld()
            // If no element reacted to the arrow key hotkeys, nudge the mouse a small bit.
            // Note that MainTracker and Blockers specifically leave some arrow-hotkeys unhandled so that this activates.
            | HotKeys.GlobalHotkeyTargets.MoveCursorLeft -> 
                let pos = Input.Mouse.GetPosition(appMainCanvas)
                if pos.X > ARROW_DELTA then
                    Graphics.SilentlyWarpMouseCursorTo(Point(pos.X - ARROW_DELTA, pos.Y))
            | HotKeys.GlobalHotkeyTargets.MoveCursorRight -> 
                let pos = Input.Mouse.GetPosition(appMainCanvas)
                if pos.X < appMainCanvas.Width - ARROW_DELTA then
                    Graphics.SilentlyWarpMouseCursorTo(Point(pos.X + ARROW_DELTA, pos.Y))
            | HotKeys.GlobalHotkeyTargets.MoveCursorUp -> 
                let pos = Input.Mouse.GetPosition(appMainCanvas)
                if pos.Y > ARROW_DELTA then
                    Graphics.SilentlyWarpMouseCursorTo(Point(pos.X, pos.Y - ARROW_DELTA))
            | HotKeys.GlobalHotkeyTargets.MoveCursorDown -> 
                let pos = Input.Mouse.GetPosition(appMainCanvas)
                if pos.Y < appMainCanvas.Height - ARROW_DELTA then
                    Graphics.SilentlyWarpMouseCursorTo(Point(pos.X, pos.Y + ARROW_DELTA))
        | None -> 
            ()
    )

    Layout.setupMouseMagnifier(cm, refocusMainWindow)

    Views.appMainCanvasGlobalBoxMouseOverHighlight.AttachToGlobalCanvas(appMainCanvas)
    
    Graphics.PlaySoundForSpeechRecognizedAndUsedToMark()  // the very first call to this lags the system for some reason, so get it out of the way at startup
    layout.AllDone()
    Popouts.Startup(doUIUpdateEvent)
    if TrackerModelOptions.ShowCustomOnStartup then ShowRunCustom.DoShowRunCustom(refocusMainWindow)   // reopen the custom SHOW windows if they were showing last time
    MainTrackerTriforcesAndItems.reopenItemLocationsWindowIfWasOpen()   // like the popouts: reopen at its remembered spot/size if it was open when the program last closed
    do! showProgress("all done")
    return drawTimeline
    }

