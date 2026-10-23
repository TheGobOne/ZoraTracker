module HotKeys

open System.Windows
open Z1R_Tracker.Models.Z1R_TrackerInterop


////////////////////////////////////////////////////////////
// Main Window listens for KeyDown, then sends RoutedEvent to element under the mouse

module MyKey =
    type SingleKeyboardModifier =  // represents the 0 or 1 modifier keys currently help (or 'Unsupported' if 2 or more held at once)
        | NoModifier
        | Shift
        | Ctrl
        // | Windows     // key not supported unless someone asks for it
        | Alt
        | Unsupported
        static member GetCurrent() =
            if Input.Keyboard.Modifiers = Input.ModifierKeys.Shift then
                SingleKeyboardModifier.Shift
            elif Input.Keyboard.Modifiers = Input.ModifierKeys.Control then
                SingleKeyboardModifier.Ctrl
            elif Input.Keyboard.Modifiers = Input.ModifierKeys.Alt then
                SingleKeyboardModifier.Alt
            elif Input.Keyboard.Modifiers = Input.ModifierKeys.None then
                SingleKeyboardModifier.NoModifier
            else
                SingleKeyboardModifier.Unsupported
        static member FromHotKeyTxt(s) =
            if s = "" then
                SingleKeyboardModifier.NoModifier
            elif s = "SHIFT" then
                SingleKeyboardModifier.Shift
            elif s = "CTRL" then
                SingleKeyboardModifier.Ctrl
            elif s = "ALT" then
                SingleKeyboardModifier.Alt
            else failwith "impossible SingleKeyboardModifier.FromHotKeyTxt"
        member this.PrettyString() =
            match this with
            | SingleKeyboardModifier.NoModifier -> ""
            | SingleKeyboardModifier.Shift -> "Shift "
            | SingleKeyboardModifier.Ctrl -> "Ctrl "
            | SingleKeyboardModifier.Alt -> "Alt "
            | SingleKeyboardModifier.Unsupported -> failwith "impossible SingleKeyboardModifier.Pretty"
    
    type MyKeyRoutedEventHandler = delegate of obj * MyKeyRoutedEventArgs -> unit
    and  MyKeyRoutedEventArgs(skm,k) =
        inherit RoutedEventArgs(MyKey.MyKeyEvent)
        member _this.Key : SingleKeyboardModifier*Input.Key = skm,k
    and  MyKey() =
        static let myKeyEvent = EventManager.RegisterRoutedEvent("ZTrackerMyKey", RoutingStrategy.Bubble, typeof<MyKeyRoutedEventHandler>, typeof<MyKey>)
        static member MyKeyEvent = myKeyEvent

    type UIElement with
        member this.MyKeyAdd(f) = this.AddHandler(MyKey.MyKeyEvent, new MyKeyRoutedEventHandler(fun o ea -> f(ea)))

open MyKey

let convertAlpha_NumToKey(ch) =
    if ch >= 'a' && ch <= 'z' then
        enum<Input.Key>(int ch - int 'a' + 44)
    elif ch >= 'A' && ch <= 'Z' then
        enum<Input.Key>(int ch - int 'A' + 44)
    elif ch = '_' then
        Input.Key.OemMinus  // Minus and Underscore on same key on typical keyboard; only handling this as _ is part of \w regex
    elif ch >= '0' && ch <= '9' then
        enum<Input.Key>(int ch - int '0' + 34)
    else
        failwithf "bad input to convertAlpha_NumToKey '%c'" ch

let boundHotKeyUniverse = new System.Collections.Generic.Dictionary<_,_>()
do
    // the overworld circles label ui uses 0-9 and a-z keys, only part of ui not rebindable by hotkeys, so ensure it exists so those keys get processed
    for c in ['0'..'9'] do
        boundHotKeyUniverse.Add((SingleKeyboardModifier.NoModifier, convertAlpha_NumToKey(c)),0)
    for c in ['a'..'z'] do
        boundHotKeyUniverse.Add((SingleKeyboardModifier.NoModifier, convertAlpha_NumToKey(c)),0)
    

let InitializeWindow(w:Window, notesTextBox:System.Windows.Controls.TextBox) =
    w.Focusable <- true
    w.PreviewKeyDown.Add(fun ea ->
        let x = Input.Mouse.DirectlyOver
        if x <> null && not(notesTextBox.IsKeyboardFocused) then
            match SingleKeyboardModifier.GetCurrent() with
            | SingleKeyboardModifier.Unsupported -> ()
            | SingleKeyboardModifier.Alt ->
                // only if the user might want the app to process this key...
                if boundHotKeyUniverse.ContainsKey(SingleKeyboardModifier.Alt,ea.SystemKey) then
                    // ...handle events so they don't bubble out of the app (otherwise e.g. Alt-mostanything will result in system beep)
                    ea.Handled <- true
                    // Alt causes Key to be read from SystemKey
                    let ea = new MyKeyRoutedEventArgs(SingleKeyboardModifier.Alt,ea.SystemKey)
                    x.RaiseEvent(ea)
                // ... but do let un-bound keys exit the app, so if the user has a global windows hotkey alt-x or something, the app won't steal it if it's not in HotKeys.txt
            | skm -> 
                if boundHotKeyUniverse.ContainsKey(skm,ea.Key) then
                    ea.Handled <- true
                    let ea = new MyKeyRoutedEventArgs(skm,ea.Key)
                    x.RaiseEvent(ea)
        )

////////////////////////////////////////////////////////////

type UserError(msg) =
    inherit System.Exception(msg)

let AllDoors = 
    [|
        for dd,s in [DoorDirection.West,"West"; DoorDirection.East,"East"; 
                        DoorDirection.North,"North"; DoorDirection.South,"South"] do
            yield sprintf "%sDoorIncrement" s, DoorHotKeyResponse(dd,DoorAction.Increment)
            yield sprintf "%sDoorDecrement" s, DoorHotKeyResponse(dd,DoorAction.Decrement)
    |]
let AllDungeonRoomNames = [|
    for x in RoomTypeExtensions.All() do
        yield "DungeonRoom_" + x.AsHotKeyName()
    for x in MonsterDetailExtensions.All() do
        yield "DungeonRoom_" + x.AsHotKeyName()
    for x in FloorDropDetailExtensions.All() do
        yield "DungeonRoom_" + x.AsHotKeyName()
    for x,_ in AllDoors do
        yield "DungeonRoom_" + x
    |]
// always Bottom/Left/Top/Right order, how PieMenus code works
let TakeAnyNames = [|
    "Contextual_TakeAny_None"
    "Contextual_TakeAny_Potion"
    "Contextual_TakeAny_Candle"
    "Contextual_TakeAny_Heart"
    |]
// the take-any cave's button box (Took Heart / Took Candle / Took Potion / Took Nothing)
let TakeAnyBoxNames = [|
    "Contextual_TakeAnyBox_None"
    "Contextual_TakeAnyBox_Potion"
    "Contextual_TakeAnyBox_Candle"
    "Contextual_TakeAnyBox_Heart"
    |]
// the Shop flyout (opened from the overworld tile menu): potion shop, then shops 1-4
let ShopNames = [|
    "Contextual_Shop_Potion"
    "Contextual_Shop_1"
    "Contextual_Shop_2"
    "Contextual_Shop_3"
    "Contextual_Shop_4"
    |]
let TakeThisNames = [|
    "Contextual_TakeThis_None"
    "Contextual_TakeThis_Candle"
    "Contextual_TakeThis_Sword"
    "Contextual_TakeThis_ShuffledItem"
    "Contextual_TakeThis_SwordIcon"
    "Contextual_TakeThis_Letter"
|]

[<RequireQualifiedAccess>]
type GlobalHotkeyTargets =
    | ToggleMagicalSword
    | ToggleWoodSword
    | ToggleBoomBook
    | ToggleBlueCandle
    | ToggleWoodArrow
    | ToggleBlueRing
    | ToggleBombs
    | ToggleGannon
    | ToggleZelda
    | DungeonTab1
    | DungeonTab2
    | DungeonTab3
    | DungeonTab4
    | DungeonTab5
    | DungeonTab6
    | DungeonTab7
    | DungeonTab8
    | DungeonTab9
    | DungeonTabS
    | MoveCursorRight
    | MoveCursorLeft
    | MoveCursorUp
    | MoveCursorDown
    | ToggleCursorOverworldOrDungeon
    | LeftClick
    | MiddleClick
    | RightClick
    | ScrollUp
    | ScrollDown
    member this.AsHotKeyName() =
        match this with
        | GlobalHotkeyTargets.ToggleMagicalSword -> "ToggleMagicalSword"
        | GlobalHotkeyTargets.ToggleWoodSword    -> "ToggleWoodSword"
        | GlobalHotkeyTargets.ToggleBoomBook     -> "ToggleBoomBook"
        | GlobalHotkeyTargets.ToggleBlueCandle   -> "ToggleBlueCandle"
        | GlobalHotkeyTargets.ToggleWoodArrow    -> "ToggleWoodArrow"
        | GlobalHotkeyTargets.ToggleBlueRing     -> "ToggleBlueRing"
        | GlobalHotkeyTargets.ToggleBombs        -> "ToggleBombs"
        | GlobalHotkeyTargets.ToggleGannon       -> "ToggleGannon"
        | GlobalHotkeyTargets.ToggleZelda        -> "ToggleZelda"
        | GlobalHotkeyTargets.DungeonTab1        -> "DungeonTab1"
        | GlobalHotkeyTargets.DungeonTab2        -> "DungeonTab2"
        | GlobalHotkeyTargets.DungeonTab3        -> "DungeonTab3"
        | GlobalHotkeyTargets.DungeonTab4        -> "DungeonTab4"
        | GlobalHotkeyTargets.DungeonTab5        -> "DungeonTab5"
        | GlobalHotkeyTargets.DungeonTab6        -> "DungeonTab6"
        | GlobalHotkeyTargets.DungeonTab7        -> "DungeonTab7"
        | GlobalHotkeyTargets.DungeonTab8        -> "DungeonTab8"
        | GlobalHotkeyTargets.DungeonTab9        -> "DungeonTab9"
        | GlobalHotkeyTargets.DungeonTabS        -> "DungeonTabS"
        | GlobalHotkeyTargets.MoveCursorLeft     -> "MoveCursorLeft"
        | GlobalHotkeyTargets.MoveCursorRight    -> "MoveCursorRight"
        | GlobalHotkeyTargets.MoveCursorUp       -> "MoveCursorUp"
        | GlobalHotkeyTargets.MoveCursorDown     -> "MoveCursorDown"
        | GlobalHotkeyTargets.ToggleCursorOverworldOrDungeon -> "ToggleCursorOverworldOrDungeon"
        | GlobalHotkeyTargets.LeftClick          -> "LeftClick"
        | GlobalHotkeyTargets.MiddleClick        -> "MiddleClick"
        | GlobalHotkeyTargets.RightClick         -> "RightClick"
        | GlobalHotkeyTargets.ScrollUp           -> "ScrollUp"
        | GlobalHotkeyTargets.ScrollDown         -> "ScrollDown"
    member this.AsHotKeyDisplay() : System.Windows.FrameworkElement =
        let mkTxt(s) : System.Windows.FrameworkElement = 
            upcast new System.Windows.Controls.TextBox(Background=System.Windows.Media.Brushes.Black, Foreground=System.Windows.Media.Brushes.White, 
                                                    Text=s, IsReadOnly=true, IsHitTestVisible=false, HorizontalContentAlignment=HorizontalAlignment.Center, 
                                                    HorizontalAlignment=HorizontalAlignment.Center, BorderThickness=Thickness(0.), Padding=Thickness(0.))
        let tab(level) : System.Windows.FrameworkElement =
            let labelChar = if level=9 then '9' elif level=10 then 'S' elif TrackerModel.IsHiddenDungeonNumbers() then (char(int 'A' - 1 + level)) else (char(int '0' + level))
            mkTxt(sprintf "Tab%c" labelChar)
        match this with
        | GlobalHotkeyTargets.ToggleMagicalSword -> upcast (Graphics.ow_sword3_item_bmp |> Graphics.BMPtoImage)
        | GlobalHotkeyTargets.ToggleWoodSword    -> upcast (Graphics.ow_sword1_item_bmp |> Graphics.BMPtoImage)
        | GlobalHotkeyTargets.ToggleBoomBook     -> upcast (Graphics.boom_book_bmp |> Graphics.BMPtoImage)
        | GlobalHotkeyTargets.ToggleBlueCandle   -> upcast (Graphics.blue_candle_bmp |> Graphics.BMPtoImage)
        | GlobalHotkeyTargets.ToggleWoodArrow    -> upcast (Graphics.wood_arrow_bmp |> Graphics.BMPtoImage)
        | GlobalHotkeyTargets.ToggleBlueRing     -> upcast (Graphics.blue_ring_bmp |> Graphics.BMPtoImage)
        | GlobalHotkeyTargets.ToggleBombs        -> upcast (Graphics.bomb_bmp |> Graphics.BMPtoImage)
        | GlobalHotkeyTargets.ToggleGannon       -> upcast (Graphics.ganon_bmp |> Graphics.BMPtoImage)
        | GlobalHotkeyTargets.ToggleZelda        -> upcast (Graphics.zelda_bmp |> Graphics.BMPtoImage)
        | GlobalHotkeyTargets.DungeonTab1        -> tab(1)
        | GlobalHotkeyTargets.DungeonTab2        -> tab(2)
        | GlobalHotkeyTargets.DungeonTab3        -> tab(3)
        | GlobalHotkeyTargets.DungeonTab4        -> tab(4)
        | GlobalHotkeyTargets.DungeonTab5        -> tab(5)
        | GlobalHotkeyTargets.DungeonTab6        -> tab(6)
        | GlobalHotkeyTargets.DungeonTab7        -> tab(7)
        | GlobalHotkeyTargets.DungeonTab8        -> tab(8)
        | GlobalHotkeyTargets.DungeonTab9        -> tab(9)
        | GlobalHotkeyTargets.DungeonTabS        -> tab(10)
        | GlobalHotkeyTargets.MoveCursorLeft     -> mkTxt("\u2190")
        | GlobalHotkeyTargets.MoveCursorRight    -> mkTxt("\u2192")
        | GlobalHotkeyTargets.MoveCursorUp       -> mkTxt("\u2191")
        | GlobalHotkeyTargets.MoveCursorDown     -> mkTxt("\u2193")
        | GlobalHotkeyTargets.ToggleCursorOverworldOrDungeon -> mkTxt("O/D")
        | GlobalHotkeyTargets.LeftClick          -> mkTxt("LMB")
        | GlobalHotkeyTargets.MiddleClick        -> mkTxt("MMB")
        | GlobalHotkeyTargets.RightClick         -> mkTxt("RMB")
        | GlobalHotkeyTargets.ScrollUp           -> mkTxt("SWU")
        | GlobalHotkeyTargets.ScrollDown         -> mkTxt("SWD")
    static member All = [|
        GlobalHotkeyTargets.ToggleMagicalSword
        GlobalHotkeyTargets.ToggleWoodSword   
        GlobalHotkeyTargets.ToggleBoomBook    
        GlobalHotkeyTargets.ToggleBlueCandle  
        GlobalHotkeyTargets.ToggleWoodArrow   
        GlobalHotkeyTargets.ToggleBlueRing    
        GlobalHotkeyTargets.ToggleBombs       
        GlobalHotkeyTargets.ToggleGannon      
        GlobalHotkeyTargets.ToggleZelda       
        GlobalHotkeyTargets.DungeonTab1       
        GlobalHotkeyTargets.DungeonTab2       
        GlobalHotkeyTargets.DungeonTab3       
        GlobalHotkeyTargets.DungeonTab4       
        GlobalHotkeyTargets.DungeonTab5       
        GlobalHotkeyTargets.DungeonTab6       
        GlobalHotkeyTargets.DungeonTab7       
        GlobalHotkeyTargets.DungeonTab8       
        GlobalHotkeyTargets.DungeonTab9       
        GlobalHotkeyTargets.DungeonTabS       
        GlobalHotkeyTargets.MoveCursorLeft
        GlobalHotkeyTargets.MoveCursorRight
        GlobalHotkeyTargets.MoveCursorUp
        GlobalHotkeyTargets.MoveCursorDown
        GlobalHotkeyTargets.ToggleCursorOverworldOrDungeon
        GlobalHotkeyTargets.LeftClick
        GlobalHotkeyTargets.MiddleClick
        GlobalHotkeyTargets.RightClick
        GlobalHotkeyTargets.ScrollUp
        GlobalHotkeyTargets.ScrollDown
        |]

let pad(s:string,n) = if s.Length < n then s + String.replicate (n - s.Length) " " else s
// Note to self; NumPad . and + are called Decimal and Add, not OemPeriod or OemPlus.  NumPad 'enter' is not supported, sadly.
// the dungeon-item marker icons that can be placed on an overworld tile (same set, same order as the overworld tile menu); hovering a tile and pressing a bound key toggles that icon on the tile
let OverworldMarkerItems : int[] = 
    [| TrackerModel.ITEMS.LADDER; TrackerModel.ITEMS.POWERBRACELET; TrackerModel.ITEMS.RECORDER; TrackerModel.ITEMS.RAFT; TrackerModel.ITEMS.BOW
       TrackerModel.ITEMS.BLUERING; TrackerModel.ITEMS.REDRING; TrackerModel.ITEMS.WOODARROW; TrackerModel.ITEMS.SILVERARROW; TrackerModel.ITEMS.WAND
       TrackerModel.ITEMS.KEY; TrackerModel.ITEMS.BLUECANDLE; TrackerModel.ITEMS.REDCANDLE; TrackerModel.ITEMS.BOOMERANG; TrackerModel.ITEMS.MAGICBOOMERANG
       TrackerModel.ITEMS.BOOKSHIELD; TrackerModel.ITEMS.SHIELD; TrackerModel.ITEMS.MEAT; TrackerModel.DECOR_BOMB; TrackerModel.ITEMS.OLDKEY
       TrackerModel.ITEMS.BLUEPOTION; TrackerModel.ITEMS.LETTER; TrackerModel.ITEMS.HEARTCONTAINER; TrackerModel.ITEMS.BOMBUPGRADE |]
// 'BookOrShield' is split: the first item slot is now 'Book' (it still displays as the Magical Shield in a boomstick seed); the separate 'Shield' item keeps its own name.
// (the old 'Item_BookOrShield' name is still accepted in an existing HotKeys.txt)
// the Item_X hotkeys also work when hovering an overworld tile (toggling that marker icon); the bomb marker is not an item, so it alone gets its own 'OverworldMarker_Bomb' entry
let CLEAR_TILE_HOTKEY = 2000   // pseudo-ident (in the overworld marker hotkey table): hovering a tile and pressing this key clears it completely (marker icons, caves, everything), like the red X in the tile menu
let OverworldMarkerOwnHotKeys : int[] = [| TrackerModel.DECOR_BOMB; CLEAR_TILE_HOTKEY |]
let ItemHotKeyName(i:int) = if i = TrackerModel.ITEMS.BOOKSHIELD then "Book" else TrackerModel.ITEMS.AsHotKeyName(i)
let OverworldMarkerHotKeyName(ident:int) = if ident = CLEAR_TILE_HOTKEY then "Overworld_ClearTile" else "OverworldMarker_" + (if ident = TrackerModel.DECOR_BOMB then "Bomb" else ItemHotKeyName(ident))
let MakeDefaultHotKeyFile(filename:string, isSample) =
    let lines = ResizeArray()
    if isSample then 
        lines.Add("# DON'T EDIT THIS FILE.  This is just a blank sample template, to show what HotKeys are available.")
        lines.Add("# To make changes to your own HotKeys, you should edit HotKeys.txt.")
        lines.Add("")
    (sprintf """# %s HotKeys

# General form is 'SelectorName = key'
#  - key can be 0-9 or a-z, or alternately of the form \nnn where nnn is the numeric key code from
#        https://docs.microsoft.com/en-us/dotnet/api/system.windows.input.key#fields
#    (so for example \75 should be used as the key name for NumPad1)
#  - key can optionally be preceded by "SHIFT " or "CTRL " or "ALT "
#    (so for example "SHIFT 4" means holding shift while pressing the '4' key)
#  - SelectorName can be any of the list below
# You can leave the key blank to not bind a hotkey to that selector

# Blank lines, or lines that start with '#', are comments and are ignored by the parser
# All other lines must have the 'General form' syntax, though whitespace is optional and ignored

    """ OverworldData.ProgramNameString).Split('\n') |> Array.iter (fun line -> lines.Add(line.Replace("\r","")))
    // items
    lines.Add("# ITEMS - these hotkey bindings take effect when mouse-hovering an item box (in the dungeons or the overworld item area),")
    lines.Add("# and also when mouse-hovering an overworld map tile, where they toggle that item's marker icon on the tile (up to 3 per tile)")
    lines.Add("# Note that Book is the first item slot (it shows as the Magical Shield in a boomstick seed); Shield is a separate item. (The old name Item_BookOrShield still works as Item_Book.)")
    let n = [0..TrackerModel.ITEMS.NumBuiltIn-1] |> Seq.map (fun i -> (ItemHotKeyName i).Length + 5) |> Seq.max
    for i in [0..TrackerModel.ITEMS.NumBuiltIn-1] |> List.sortBy (fun i -> (ItemHotKeyName i).ToLowerInvariant()) do
        lines.Add(pad("Item_" + ItemHotKeyName i,n) + " = ")
    lines.Add(pad("Item_Nothing",n) + " = ")
    lines.Add("")
    lines.Add("# OVERWORLD - these hotkey bindings take effect when mouse-hovering an overworld map tile")
    lines.Add("# Note that Level1-Level8 refer to dungeons A-H if using the 'Hide Dungeon Numbers' flag setting")
    let n = [0..TrackerModel.dummyOverworldTiles.Length-1] |> Seq.map (fun i -> TrackerModel.MapSquareChoiceDomainHelper.AsHotKeyName(i).Length + 10) |> Seq.max
    for i = 0 to TrackerModel.dummyOverworldTiles.Length-1 do
        lines.Add(pad("Overworld_" + TrackerModel.MapSquareChoiceDomainHelper.AsHotKeyName(i),n) + " = ")
    lines.Add(pad("Overworld_Nothing",n) + " = ")
    lines.Add("")
    lines.Add("# OVERWORLD MARKER ICONS - the Item_... hotkeys above also toggle marker icons while hovering an overworld tile;")
    lines.Add("# the bomb marker is not an item, so it has its own entry here")
    let n = (OverworldMarkerOwnHotKeys |> Seq.map (fun i -> (OverworldMarkerHotKeyName i).Length) |> Seq.max) + 5
    for ident in OverworldMarkerOwnHotKeys |> Array.sortBy (fun i -> (OverworldMarkerHotKeyName i).ToLowerInvariant()) do
        lines.Add(pad(OverworldMarkerHotKeyName ident,n) + " = ")
    lines.Add("")
    lines.Add("# BLOCKERS - these hotkey bindings take effect when mouse-hovering a blocker box")
    let n = TrackerModel.DungeonBlocker.All |> Seq.map (fun b -> b.AsHotKeyName().Length) |> Seq.max
    for b in TrackerModel.DungeonBlocker.All do
        let s = b.AsHotKeyName()
        lines.Add(pad(s,(if s.Contains("Maybe") || s.Contains("Nothing") then n else n-6)) + " = ")
    lines.Add("")
    lines.Add("# DUNGEON ROOMS - these hotkey bindings take effect when mouse-hovering a room in a dungeon")
    for x in AllDungeonRoomNames do
        if x.Contains("MonsterDetail_Bow") then
            lines.Add("# Note that MonsterDetail_Bow actually means MonsterDetail_Gohma, but the old Bow name is still used instead, for HotKeys.txt backward compatibility")
        let n = if x.Contains("RoomType") then 42 elif x.Contains("MonsterDetail") then 38 elif x.Contains("FloorDropDetail") then 40 else 30
        lines.Add(pad(x,n) + " = ")
    lines.Add("")
    lines.Add("# HINT ZONES - these hotkey bindings take effect when mouse-hovering a hint zone box (above triforces in non-hidden-dungeon-numbers)")
    let n = TrackerModel.HintZone.All |> Seq.map (fun hz -> hz.AsHotKeyName().Length) |> Seq.max
    for hz in TrackerModel.HintZone.All do
        lines.Add(pad(hz.AsHotKeyName(),n) + " = ")
    lines.Add("")
    lines.Add("# CONTEXTUAL CHOICES - these hotkey bindings only take effect when the corresponding menus are on-screen")
    let n = [ yield! TakeAnyNames; yield! TakeAnyBoxNames; yield! ShopNames; yield! TakeThisNames ] |> Seq.map (fun x -> x.Length) |> Seq.max
    for x in TakeAnyNames do
        lines.Add(pad(x,n) + " = ")
    for x in TakeAnyBoxNames do
        lines.Add(pad(x,n) + " = ")
    for x in ShopNames do
        lines.Add(pad(x,n) + " = ")
    for x in TakeThisNames do
        lines.Add(pad(x,n) + " = ")
    lines.Add("")
    lines.Add("# GLOBAL - these hotkey bindings take effect anywhere, and cannot conflict with any other non-contextuals")
    for x in GlobalHotkeyTargets.All do
        let s = x.AsHotKeyName()
        let n = if s.Contains("Toggle") then 25 elif s.Contains("Move") || s.Contains("Click") || s.Contains("Scroll") then 22 else 0
        lines.Add(pad("Global_" + s,n) + " = ")
    lines.Add("")
    System.IO.File.WriteAllLines(filename, lines)

let ParseHotKeyDataFile(filename:string) =
    let lines = System.IO.File.ReadAllLines(filename)
    let commentRegex = new System.Text.RegularExpressions.Regex("^#.*$", System.Text.RegularExpressions.RegexOptions.None)
    let emptyLineRegex = new System.Text.RegularExpressions.Regex("^\s*$", System.Text.RegularExpressions.RegexOptions.None)
    let dataRegex = new System.Text.RegularExpressions.Regex("^\s*(\w+)\s*=\s*(SHIFT|CTRL|ALT|)\s*(\w)?\s*$", System.Text.RegularExpressions.RegexOptions.None)
    let data2Regex = new System.Text.RegularExpressions.Regex("""^\s*(\w+)\s*=\s*(SHIFT|CTRL|ALT|)\s*\\(\d+)\s*$""", System.Text.RegularExpressions.RegexOptions.None)
    let data = ResizeArray()
    let mutable lineNumber = 1
    for line in lines do
        if commentRegex.IsMatch(line) || emptyLineRegex.IsMatch(line) then
            () // skip
        else
            let m = dataRegex.Match(line)
            if not m.Success then
                let m = data2Regex.Match(line)
                if not m.Success then
                    raise <| new UserError(sprintf "Error parsing '%s', line %d:\n    %s\nEach line should look like  Name = KEY  (optionally SHIFT, CTRL or ALT before the key), or start with # for a comment" filename lineNumber (line.Trim()))
                else
                    let name = m.Groups.[1].Value
                    let value = if m.Groups.Count > 2 then Some(SingleKeyboardModifier.FromHotKeyTxt(m.Groups.[2].Value), enum<Input.Key>(int m.Groups.[3].Value)) else None
                    data.Add(name, value, (lineNumber, filename))

            else
                // Groups.[0] is the whole match
                let name = m.Groups.[1].Value
                let value = if m.Groups.Count > 2 && m.Groups.[3].Value.Length > 0 then Some(SingleKeyboardModifier.FromHotKeyTxt(m.Groups.[2].Value), convertAlpha_NumToKey(m.Groups.[3].Value.[0])) else None
                data.Add(name, value, (lineNumber, filename))
        lineNumber <- lineNumber + 1
    data

////////////////////////////////////////////////////////////

let keyUniverse = 
    [| 
        for i = 1 to 172 do // it appears Input.Key does not range outside 1 to 172
            for j in [| SingleKeyboardModifier.NoModifier; SingleKeyboardModifier.Shift; SingleKeyboardModifier.Ctrl; SingleKeyboardModifier.Alt |] do
                yield j,enum<Input.Key>(i) 
    |]   
let PrettyKey(skm:SingleKeyboardModifier,key:Input.Key) =
    let s = key.ToString()
    if System.Text.RegularExpressions.Regex.IsMatch(s, "D\d") then  // the 'name' of 0-9 across top keyboard is D0-D9
        sprintf "%s%c" (skm.PrettyString()) s.[1]
    else
        skm.PrettyString()+s
type HotKeyProcessor<'v when 'v : equality>(contextName) =
    let table = new System.Collections.Generic.Dictionary<SingleKeyboardModifier*Input.Key,'v>()
    let stateToKeys = new System.Collections.Generic.Dictionary<_,_>()  // caching
    let cachedDescriptions = new System.Collections.Generic.Dictionary<_,_>()  // hotkeys never change during runtime, so first lookup is always the permanent description
    member this.ContextName = contextName
    member this.TryGetValue(skm,k) = 
        match table.TryGetValue((skm,k)) with
        | true, v -> Some(v)
        | _ -> None
    member this.ContainsKey(skm,k) = table.ContainsKey(skm,k)
    member this.Keys() = table.Keys |> Seq.toArray
    member this.TryAdd((skm,k),v) =
        if table.ContainsKey(skm,k) then
            false
        else
            table.Add((skm,k),v)
            true
    member this.StateToKeys(state) =
        if not(stateToKeys.ContainsKey(state)) then
            let r = ResizeArray()
            for k in keyUniverse do
                match this.TryGetValue(k) with
                | Some x -> if x = state then r.Add(k)
                | None -> ()
            stateToKeys.Add(state, r)
            r
        else
            stateToKeys.[state]
    member this.AsPrettyHotKeyOpt(state) =
        let keys = this.StateToKeys(state)
        if keys.Count > 0 then
            Some(sprintf "HotKey = %s" (PrettyKey(keys.[0])))
        else
            None
    member this.AppendHotKeyToDescription(desc, state) =
        match cachedDescriptions.TryGetValue(state) with
        | true, r -> r
        | false, _ ->
            let r = 
                let keys = this.StateToKeys(state)
                if keys.Count > 0 then
                    sprintf "%s\nHotKey = %s" desc (PrettyKey(keys.[0]))
                else
                    desc
            cachedDescriptions.Add(state, r)
            r

// area specific
let ItemHotKeyProcessor = new HotKeyProcessor<int>("Item")
let OverworldHotKeyProcessor = new HotKeyProcessor<int>("Overworld")
let OverworldMarkerHotKeyProcessor = new HotKeyProcessor<int>("OverworldMarker")
let BlockerHotKeyProcessor = new HotKeyProcessor<TrackerModel.DungeonBlocker>("Blocker")
let DungeonRoomHotKeyProcessor = new HotKeyProcessor<Choice<RoomType,MonsterDetail,FloorDropDetail,DoorHotKeyResponse> >("DungeonRoom")
let HintZoneHotKeyProcessor = new HotKeyProcessor<TrackerModel.HintZone>("HintZone")
// contextual
let TakeAnyHotKeyProcessor = new HotKeyProcessor<int>("TakeAny")
let TakeThisHotKeyProcessor = new HotKeyProcessor<int>("TakeThis")
let TakeAnyBoxHotKeyProcessor = new HotKeyProcessor<int>("TakeAnyBox")
let ShopHotKeyProcessor = new HotKeyProcessor<int>("Shop")
// global
let GlobalHotKeyProcessor = new HotKeyProcessor<GlobalHotkeyTargets>("Global")

let HotKeyFilename = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "HotKeys.txt")
let HotKeyBlankSampleFilename = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "HotKeys_BlankSampleTemplate.txt")

let PopulateHotKeyTables() =
    let filename = HotKeyBlankSampleFilename
    // the blank sample template holds no user data, so regenerate it every launch (keeps it current with new hotkeys)
    (try MakeDefaultHotKeyFile(filename,true) with _ -> ())
    let filename = HotKeyFilename
    if not(System.IO.File.Exists(filename)) then
        MakeDefaultHotKeyFile(filename,false)
    let data = ParseHotKeyDataFile(filename)
    for name, chOpt, (lineNumber, filename) in data do
        let Add(hkp:HotKeyProcessor<_>, keyOpt, x) =
            match keyOpt with
            | None -> ()  // Foo=    means Foo is not bound to a hotkey on this line
            | Some key ->
                if not(hkp.TryAdd(key,x)) then
                    raise <| new UserError(sprintf "Keyboard key '%s' given multiple meanings for '%s' context; second occurrence at line %d of '%s'" (PrettyKey key) hkp.ContextName lineNumber filename)
                else
                    if not(boundHotKeyUniverse.ContainsKey(key)) then
                        boundHotKeyUniverse.Add(key,0) 
        match name with
        | "Item_Nothing"          -> Add(ItemHotKeyProcessor, chOpt, -1)
        | "Overworld_Nothing"     -> Add(OverworldHotKeyProcessor, chOpt, -1)
        | "Overworld_PotionShop"  -> Add(OverworldHotKeyProcessor, chOpt, TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP)   // old name of Overworld_Shop
        | _ -> 
            let mutable found = false
            for b in TrackerModel.DungeonBlocker.All do
                if name = b.AsHotKeyName() then
                    Add(BlockerHotKeyProcessor, chOpt, b)
                    found <- true
            if not found then
                for hz in TrackerModel.HintZone.All do
                    if name = hz.AsHotKeyName() then
                        Add(HintZoneHotKeyProcessor, chOpt, hz)
                        found <- true
            if not found then
                for i = 0 to TrackerModel.ITEMS.NumBuiltIn-1 do
                    if name = "Item_" + ItemHotKeyName(i) || (i = TrackerModel.ITEMS.BOOKSHIELD && name = "Item_BookOrShield") then
                        Add(ItemHotKeyProcessor, chOpt, i)
                        found <- true
            if not found then
                for i = 0 to TrackerModel.dummyOverworldTiles.Length-1 do
                    if name = "Overworld_" + TrackerModel.MapSquareChoiceDomainHelper.AsHotKeyName(i) then
                        Add(OverworldHotKeyProcessor, chOpt, i)
                        found <- true
            if not found then
                for ident in Array.append OverworldMarkerItems [| CLEAR_TILE_HOTKEY |] do   // (all names still accepted, for files written by pre-release test builds; they work as extra marker bindings)
                    if name = OverworldMarkerHotKeyName ident then
                        Add(OverworldMarkerHotKeyProcessor, chOpt, ident)
                        found <- true
            if not found then
                for x in RoomTypeExtensions.All() do
                    if name = "DungeonRoom_" + x.AsHotKeyName() then
                        Add(DungeonRoomHotKeyProcessor, chOpt, Choice1Of4 x)
                        found <- true
                for x in MonsterDetailExtensions.All() do
                    if name = "DungeonRoom_" + x.AsHotKeyName() then
                        Add(DungeonRoomHotKeyProcessor, chOpt, Choice2Of4 x)
                        found <- true
                for x in FloorDropDetailExtensions.All() do
                    if name = "DungeonRoom_" + x.AsHotKeyName() then
                        Add(DungeonRoomHotKeyProcessor, chOpt, Choice3Of4 x)
                        found <- true
                for x,dhkr in AllDoors do
                    if name = "DungeonRoom_" + x then
                        Add(DungeonRoomHotKeyProcessor, chOpt, Choice4Of4 dhkr)
                        found <- true
            if not found then
                for i = 0 to 3 do do
                    if name = TakeAnyNames.[i] then
                        Add(TakeAnyHotKeyProcessor, chOpt, i)
                        found <- true
            if not found then
                for i = 0 to 3 do do
                    if name = TakeAnyBoxNames.[i] then
                        Add(TakeAnyBoxHotKeyProcessor, chOpt, i)
                        found <- true
            if not found then
                for i = 0 to ShopNames.Length-1 do do
                    if name = ShopNames.[i] then
                        Add(ShopHotKeyProcessor, chOpt, i)
                        found <- true
            if not found then
                for i = 0 to TakeThisNames.Length-1 do do
                    if name = TakeThisNames.[i] then
                        Add(TakeThisHotKeyProcessor, chOpt, i)
                        found <- true
            if not found then
                for x in GlobalHotkeyTargets.All do
                    if name = "Global_"+ x.AsHotKeyName() then
                        Add(GlobalHotKeyProcessor, chOpt, x)
                        found <- true
            if not found then
                raise <| new UserError(sprintf "Bad name '%s' specified in '%s', line %d\nCheck the spelling against the names in HotKeys_BlankSampleTemplate.txt" name filename lineNumber)
    // the overworld tile and overworld marker bindings apply in the same place (hovering a tile), so they can't share a key
    for k in OverworldMarkerHotKeyProcessor.Keys() do
        if OverworldHotKeyProcessor.ContainsKey(k) then
            raise <| new UserError(sprintf "Keyboard key '%s' is bound both to an 'Overworld_...' entry and to an 'OverworldMarker_...' entry" (PrettyKey k))
    // global conflict check
    for k in GlobalHotKeyProcessor.Keys() do
        let error(kind) =
            let msg = sprintf "Global hotkey '%s' was bound as '%s', but that key was also bound to %s entry" (PrettyKey k) ("Global_"+GlobalHotKeyProcessor.TryGetValue(k).Value.AsHotKeyName()) kind
            raise <| new UserError(msg)
        if ItemHotKeyProcessor.ContainsKey(k) then
            error "an 'Item_...'"
        if OverworldHotKeyProcessor.ContainsKey(k) then
            error "an 'Overworld_...'"
        if OverworldMarkerHotKeyProcessor.ContainsKey(k) then
            error "an 'OverworldMarker_...'"
        if BlockerHotKeyProcessor.ContainsKey(k) then
            error "a 'Blocker_...'"
        // don't bother with contextual hotkey conflicts, they're ok
        if DungeonRoomHotKeyProcessor.ContainsKey(k) then
            error "a 'DungeonRoom_...'"
