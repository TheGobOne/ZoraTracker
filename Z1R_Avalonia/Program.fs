module Z1R_Avalonia.Program

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Controls.Primitives
open Avalonia.Controls.Shapes
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Media.Imaging
open Avalonia.Themes.Fluent

// Preview: the real overworld map art with the REAL tracker logic (Z1R_Tracker_Core) on top.
// Always-empty tiles are darkened; hover a tile to see what the logic knows about it.

// images are bundled as Avalonia resources (works on desktop AND in the browser, where there is no real file system)
let loadBmp (name:string) : Bitmap =
    use s = Avalonia.Platform.AssetLoader.Open(System.Uri("avares://Z1R_Avalonia/Assets/" + name))
    new Bitmap(s)

let TILE_W = 64.   // 16 source pixels x 4 (same size as the Windows tracker)
let TILE_H = 44.   // 11 source pixels x 4

// the same strip the Windows tracker uses: 8 maps side by side, each 256x88 source pixels; 0=1Q 1=2Q 2=mixed 1Q 3=mixed 2Q
let strip = lazy (loadBmp "s_map_overworld_vanilla_strip8.png")

// ---- icons: same pixel art the Windows tracker uses (5x9-pixel icons, shown 3x so 15x27) ----
let stripOw = lazy (loadBmp "ow_icons5x9.png")
let stripItems = lazy (loadBmp "icons3x7.png")
// overworld tile state id -> position in the ow_icons5x9 strip (swords, secrets, door repair, money game, letter, armos, hint shop, take any, potion shop)
let stripIndex = dict [ 13,0; 14,1; 15,2; 24,11; 25,8; 26,6; 27,9; 28,12; 29,10; 30,13; 31,7; 32,3; 33,4; 34,5 ]

// copy of a sprite sheet where pure-black pixels become transparent (so marker icons sit directly on the map art with no black box)
let blackToTransparent (src:Bitmap) : Bitmap =
    let wb = new WriteableBitmap(src.PixelSize, src.Dpi, Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Unpremul)
    use fb = wb.Lock()
    src.CopyPixels(fb, Avalonia.Platform.AlphaFormat.Unpremul)
    let n = fb.RowBytes * fb.Size.Height
    let buf : byte[] = Array.zeroCreate n
    System.Runtime.InteropServices.Marshal.Copy(fb.Address, buf, 0, n)
    for i in 0 .. 4 .. n - 4 do
        if buf.[i] = 0uy && buf.[i + 1] = 0uy && buf.[i + 2] = 0uy then buf.[i + 3] <- 0uy
    System.Runtime.InteropServices.Marshal.Copy(buf, 0, fb.Address, n)
    wb :> Bitmap

let pixelImage (bmp:Bitmap) (rect:PixelRect) (w:float) (h:float) : Image =
    let img = Image(Width=w, Height=h, Stretch=Stretch.Fill, Source=new CroppedBitmap(bmp, rect))
    RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None)
    img

let coloredBox (bg:IBrush) (child:Control) : Control =
    Border(Width=15., Height=27., Background=bg, Child=child) :> Control

let digitText (text:string) =
    TextBlock(Text=text, Foreground=Brushes.Black, FontWeight=FontWeight.Bold, FontSize=15.,
              HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center) :> Control

// small pixel-art shapes (unit coordinates, 3x3 screen pixels each) for the two personal-preference markers
let patternBox (bg:IBrush) (units:(int*int) list) : Control =
    let c = Canvas(Width=15., Height=27., Background=bg)
    for (ux, uy) in units do
        let r = Rectangle(Width=3., Height=3., Fill=Brushes.Black)
        Canvas.SetLeft(r, float (3 * ux))
        Canvas.SetTop(r, float (3 * uy))
        c.Children.Add(r)
    c :> Control

let makeIcon (state:int) : Control =
    if state <= 8 then coloredBox Brushes.Yellow (digitText (string (state + 1)))                    // dungeons 1-9
    elif state <= 12 then coloredBox Brushes.Orchid (digitText (string (state - 8)))                 // any roads 1-4
    elif stripIndex.ContainsKey state then pixelImage stripOw.Value (PixelRect(stripIndex.[state] * 5, 0, 5, 9)) 15. 27. :> Control
    elif state >= 16 && state <= 23 then                                                              // item shops: 3x7 item icon on orange
        let inner = pixelImage stripItems.Value (PixelRect((state - 16) * 3, 0, 3, 7)) 9. 21.
        let c = Canvas(Width=15., Height=27., Background=SolidColorBrush(Color.FromRgb(0xEFuy, 0x83uy, 0uy)))
        Canvas.SetLeft(inner, 3.)
        Canvas.SetTop(inner, 3.)
        c.Children.Add(inner)
        c :> Control
    elif state = 35 then coloredBox Brushes.Black (TextBlock() :> Control)                           // Don't Care
    elif state = 36 then patternBox Brushes.Magenta [ (1,2);(3,2);(2,3);(0,4);(1,4);(2,4);(3,4);(4,4);(2,5);(1,6);(3,6) ]
    elif state = 37 then patternBox Brushes.Cyan [ (2,2);(1,3);(2,3);(3,3);(0,4);(1,4);(2,4);(3,4);(4,4);(1,5);(2,5);(3,5);(2,6) ]
    else coloredBox Brushes.Gray (digitText "?")

// ---- dungeon-item and extra-sword icons (the decorative markers from the Windows right-click menu) ----
let strip7 = lazy (loadBmp "icons7x7.png")

// pixel-art sprite drawn from text rows: one character per art pixel, '.' (or any unlisted character) is transparent
let spriteCanvas (rows:string[]) (palette:(char*Color) list) (unit:float) : Control =
    let h = rows.Length
    let w = rows.[0].Length
    let c = Canvas(Width=float w * unit, Height=float h * unit)
    for y = 0 to h - 1 do
        for x = 0 to w - 1 do
            match List.tryFind (fun (ch, _) -> ch = rows.[y].[x]) palette with
            | Some(_, col) ->
                let r = Rectangle(Width=unit, Height=unit, Fill=SolidColorBrush(col))
                Canvas.SetLeft(r, float x * unit)
                Canvas.SetTop(r, float y * unit)
                c.Children.Add(r)
            | None -> ()
    c :> Control

let rgb (r:int) (g:int) (b:int) = Color.FromRgb(byte r, byte g, byte b)

// 4th sword and progressive sword: brown tile, digit/letter on top, small sword below (same art as the Windows tracker), 15x27
let extraSwordIcon (item:int) : Control =
    let isProg = (item = TrackerModel.ITEMS.PROGRESSIVESWORD)
    let glyph = if isProg then [| "abbba"; "ababa"; "abbba"; "abaaa"; "abaaa" |] else [| "ababa"; "ababa"; "abbba"; "aaaba"; "aaaba" |]   // 'P' or '4'
    let rows = Array.append (Array.append [| "aaaaa" |] glyph) [| "aaaXa"; "HHHXX"; "aaaXa" |]
    let blade = if isProg then rgb 29 148 65 else rgb 255 165 0
    let hilt = if isProg then rgb 136 0 21 else Colors.White
    spriteCanvas rows [ 'a', rgb 185 122 87; 'b', Colors.Black; 'X', blade; 'H', hilt ] 3.

let strip7T = lazy (blackToTransparent strip7.Value)
let stripOwT = lazy (blackToTransparent stripOw.Value)
let transparentMode = ref false
let owBmp () : Bitmap = if transparentMode.Value then stripOwT.Value else stripOw.Value
let itemStrip (index:int) : Control = pixelImage (if transparentMode.Value then strip7T.Value else strip7.Value) (PixelRect(index * 7, 0, 7, 7)) 21. 21. :> Control

let magicKeyIcon () = spriteCanvas [| "..ooo.."; ".oo.orr"; ".ooorr."; "...or.."; "...or.."; "...oo.."; "...or.." |] [ 'o', rgb 252 152 56; 'r', rgb 216 40 0 ] 3.
let letterIcon () =
    spriteCanvas [| ".f.f.f."; ".fjfjw."; ".ffjww."; ".fjwww."; ".ffwww."; ".fwww.."; ".ffjfw." |] [ 'f', rgb 92 148 252; 'j', rgb 0 0 168; 'w', rgb 252 252 252 ] 3.
let potionIcon () =   // blue bottle on the left, red on the right (like the Windows tracker's split potion icon)
    let potionRows = [| "..aaa.."; "..cLc.."; "..c.c.."; ".c...c."; ".cLLLc."; ".cLhLc."; "..ccc.." |] |> Array.map (fun r -> r.PadRight(7, '.'))
    let blue = [ 'a', rgb 155 142 255; 'c', Colors.White; 'L', rgb 40 0 165; 'h', rgb 155 142 255 ]
    let red = [ 'a', rgb 231 156 33; 'c', Colors.White; 'L', rgb 181 49 33; 'h', rgb 231 156 33 ]
    let g = Panel(Width=21., Height=21.)
    let half (pal:(char*Color) list) (x0:float) (x1:float) =
        let sp = spriteCanvas potionRows pal 3.
        sp.Clip <- RectangleGeometry(Rect(x0, 0., x1 - x0, 21.))
        g.Children.Add(sp)
    half blue 0. 11.
    half red 11. 21.
    g :> Control

// ITEMS id -> icon. Swords are the 15x27 cave-style icons; everything else is 21x21.
let itemIcon (ident:int) : Control =
    let I = TrackerModel.ITEMS.BOOKSHIELD
    if ident = TrackerModel.ITEMS.WHITESWORD4 || ident = TrackerModel.ITEMS.PROGRESSIVESWORD then extraSwordIcon ident
    elif ident = I then itemStrip (if TrackerModel.IsCurrentlyBook() then 8 else 13)
    else
        match ident with
        | 1 -> itemStrip 0       // boomerang
        | 2 -> itemStrip 1       // bow
        | 3 -> itemStrip 16      // power bracelet
        | 4 -> itemStrip 4       // ladder
        | 5 -> itemStrip 2       // magic boomerang
        | 6 -> magicKeyIcon ()   // key
        | 7 -> itemStrip 3       // raft
        | 8 -> itemStrip 5       // recorder
        | 9 -> itemStrip 7       // red candle
        | 10 -> itemStrip 12     // red ring
        | 11 -> itemStrip 10     // silver arrow
        | 12 -> itemStrip 6      // wand
        | 14 -> itemStrip 15     // heart container
        | 15 | 16 ->   // the tracker's original potion-shop artwork, with a small blue (15) or red (16) corner mark so the two potions can be told apart
            let g = Panel(Width=15., Height=27.)
            g.Children.Add(pixelImage (owBmp ()) (PixelRect(5 * 5, 0, 5, 9)) 15. 27.)
            g.Children.Add(Border(Width=6., Height=6., Background=(if ident = 15 then Brushes.RoyalBlue else Brushes.Red), HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Bottom))
            g :> Control
        | 17 -> pixelImage (owBmp ()) (PixelRect(13 * 5, 0, 5, 9)) 15. 27. :> Control    // the tracker's original (blue) letter artwork
        | 29 -> spriteCanvas [| "...a..."; "..aaa.."; ".aaaaa."; "aaaaaaa" |] [ 'a', rgb 252 188 60 ] 3.   // triforce piece
        | 13 -> pixelImage (owBmp ()) (PixelRect(1 * 5, 0, 5, 9)) 15. 27. :> Control   // white sword
        | 19 -> pixelImage (owBmp ()) (PixelRect(2 * 5, 0, 5, 9)) 15. 27. :> Control   // wood sword
        | 20 -> pixelImage (owBmp ()) (PixelRect(0 * 5, 0, 5, 9)) 15. 27. :> Control   // magical sword
        | 22 -> itemStrip 22     // blue ring
        | 23 -> itemStrip 11     // wood arrow
        | 24 -> itemStrip 21     // blue candle
        | 25 -> itemStrip 13     // magical shield
        | 26 -> itemStrip 27     // meat
        | 27 -> itemStrip 9      // old key
        | 28 -> itemStrip 31     // bomb upgrade
        | 100 -> itemStrip 25    // generic bomb
        | _ -> coloredBox Brushes.Gray (digitText "?")

// the marker icon as drawn ON the map: the item picture on a black plate, so it stays visible over the busy map art
let decorOnMap (item:int) : Control =
    transparentMode.Value <- true
    let inner = try itemIcon item finally transparentMode.Value <- false
    inner.IsHitTestVisible <- false   // drawn straight on the map art, no plate behind it (like the Windows tracker)
    inner

// ---- items + triforce panel: toggles the same model state as the Windows tracker's starting-items / triforce boxes ----
let startingFlags () : TrackerModel.BoolProperty list =
    let S = TrackerModel.startingItemsAndExtras
    [ S.PlayerHasBook; S.PlayerHasBoomerang; S.PlayerHasMagicBoomerang; S.PlayerHasBow; S.PlayerHasSilverArrow; S.PlayerHasRedCandle; S.PlayerHasRedRing
      S.PlayerHasWand; S.PlayerHasRecorder; S.PlayerHasRaft; S.PlayerHasLadder; S.PlayerHasPowerBracelet; S.PlayerHasAnyKey; S.PlayerHasMeat ]

let makeItemsPanel (onChange: unit -> unit) : Control =
    let S = TrackerModel.startingItemsAndExtras
    let I = TrackerModel.ITEMS.BOOKSHIELD
    let P = TrackerModel.playerProgressAndTakeAnyHearts
    // laid out like the dungeon item menu order, 6 per row (the blue ring / wood arrow / blue candle caves live here too)
    let all : (int * TrackerModel.BoolProperty) list =
        [ 4, S.PlayerHasLadder; 3, S.PlayerHasPowerBracelet; 8, S.PlayerHasRecorder; 7, S.PlayerHasRaft; 2, S.PlayerHasBow; 22, P.PlayerHasBlueRing
          10, S.PlayerHasRedRing; 6, S.PlayerHasAnyKey; 23, P.PlayerHasWoodArrow; 11, S.PlayerHasSilverArrow; 12, S.PlayerHasWand; 24, P.PlayerHasBlueCandle
          9, S.PlayerHasRedCandle; 1, S.PlayerHasBoomerang; 5, S.PlayerHasMagicBoomerang; I, S.PlayerHasBook; 26, S.PlayerHasMeat ]
    let rows = all |> List.chunkBySize 6
    let panel = StackPanel(Spacing=2.)
    let dim (c:Control) (on:bool) = c.Opacity <- (if on then 1.0 else 0.3)
    let refreshers = ResizeArray<unit -> unit>()
    for r in rows do
        let row = StackPanel(Orientation=Orientation.Horizontal)
        for (id, flag) in r do
            let icon = itemIcon id
            let b = Border(Width=33., Height=33., Background=Brushes.Black, BorderBrush=Brushes.Red, BorderThickness=Thickness(3.), Margin=Thickness(1.), Child=icon, Cursor=Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand))
            icon.HorizontalAlignment <- HorizontalAlignment.Center
            icon.VerticalAlignment <- VerticalAlignment.Center
            ToolTip.SetTip(b, TrackerModel.ITEMS.AsDisplayDescription(id))
            let refresh () = dim icon (flag.Value()); b.BorderBrush <- (if flag.Value() then Brushes.LimeGreen else Brushes.Red)
            refresh ()
            refreshers.Add(refresh)
            b.PointerPressed.Add(fun _ -> flag.Toggle(); refresh (); onChange ())
            row.Children.Add(b)
        panel.Children.Add(row)
    panel :> Control

// item order used when cycling a box: the same order as the Windows tracker's item menu (swords first, then the grid rows); empty (-1) comes after the last item
let boxItemOrder = [| 19; 13; 20; 21; 18;  4; 3; 8; 7; 2;  22; 10; 6; 23; 11;  12; 24; 9; 1; 5;  0; 25; 26; 27; 28;  15; 17; 14; 29 |]
let cycleBox (b:TrackerModel.Box) (backwards:bool) =
    let n = boxItemOrder.Length
    // positions: -1 = empty, 0..n-1 = boxItemOrder; step until a legal one (items with limited uses may be taken elsewhere)
    let posOf c = match Array.tryFindIndex (fun x -> x = c) boxItemOrder with Some i -> i | None -> -1
    let mutable pos = posOf (b.CellCurrent())
    let mutable fin = false
    let mutable tries = 0
    while not fin && tries <= n + 1 do
        tries <- tries + 1
        pos <- (if backwards then (if pos = -1 then n - 1 else pos - 1) else (if pos = n - 1 then -1 else pos + 1))
        let cell = if pos = -1 then -1 else boxItemOrder.[pos]
        if b.AttemptToSet(cell, b.PlayerHas()) then fin <- true

// ---- dungeon panel: 9 dungeons x item boxes.  Left-click a box: cycle NO(red) -> HAVE(green) -> SKIPPED(white).  Right-click: next item (shift+right-click: previous). ----
// the item grid shown when you click a dungeon item box (Windows layout: swords, then 5-wide rows; the last cell clears the box)
let itemMenuIdents = [| 19; 13; 20; 21; 18;  4; 3; 8; 7; 2;  22; 10; 6; 23; 11;  12; 24; 9; 1; 5;  0; 25; 26; 27; 28;  15; 17; 14; 29; -1 |]
let makeItemMenu (pick: int -> unit) : Control =
    let grid = StackPanel(Spacing=3.)
    for r in 0 .. itemMenuIdents.Length / 5 - 1 do
        let row = StackPanel(Orientation=Orientation.Horizontal, Spacing=3.)
        for c in 0 .. 4 do
            let id = itemMenuIdents.[r * 5 + c]
            let content : Control = if id = -1 then (TextBlock(Text="none", Foreground=Brushes.Gray, FontSize=11., VerticalAlignment=VerticalAlignment.Center, HorizontalAlignment=HorizontalAlignment.Center) :> Control) else itemIcon id
            let cell = Border(Width=36., Height=36., Background=Brushes.Black, BorderBrush=Brushes.DimGray, BorderThickness=Thickness(1.), Child=content, Cursor=Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand))
            if id <> -1 then ToolTip.SetTip(cell, TrackerModel.ITEMS.AsDisplayDescription(id))
            cell.PointerPressed.Add(fun _ -> pick id)
            cell.PointerEntered.Add(fun _ -> cell.BorderBrush <- Brushes.Yellow; cell.BorderThickness <- Thickness(2.))
            cell.PointerExited.Add(fun _ -> cell.BorderBrush <- Brushes.DimGray; cell.BorderThickness <- Thickness(1.))
            row.Children.Add(cell)
        grid.Children.Add(row)
    Border(Background=SolidColorBrush(Color.FromRgb(20uy, 20uy, 20uy)), BorderBrush=Brushes.Orange, BorderThickness=Thickness(1.), Padding=Thickness(6.), Child=grid) :> Control

// ---- keyboard hotkeys: same HotKeys.txt format as the Windows tracker (Name = [SHIFT|CTRL|ALT ]key).  Supported here: Overworld_*, Item_*, OverworldMarker_Bomb, Blocker_*, HintZone_* ----
let hotKeyMap = System.Collections.Generic.Dictionary<string, string>()
let hoverHandler : (string -> unit) option ref = ref None       // set while the mouse is over something that accepts hotkeys
let focusHost : (unit -> unit) ref = ref (fun () -> ())
let hotKeyFilePath () = System.IO.Path.Combine(System.AppContext.BaseDirectory, "HotKeys.txt")
let itemHotKeyName (i:int) = if i = TrackerModel.ITEMS.BOOKSHIELD then "Book" else TrackerModel.ITEMS.AsHotKeyName(i)
let hoverHotKeys (c:Control) (handler: string -> unit) =
    c.PointerEntered.Add(fun _ -> hoverHandler.Value <- Some handler; focusHost.Value ())
    c.PointerExited.Add(fun _ -> hoverHandler.Value <- None)
let makeHotKeyTemplate () : string =
    let sb = System.Text.StringBuilder()
    let add (l:string) = sb.AppendLine(l) |> ignore
    add "# ZORA Tracker HotKeys (cross-platform preview).  This is the same file format as the Windows tracker, so you can copy your HotKeys.txt over."
    add "# General form is 'SelectorName = key'.  key can be 0-9 or a-z, optionally preceded by SHIFT, CTRL or ALT (for example 'SHIFT 4')."
    add "# Leave the key blank to not bind a hotkey.  Lines starting with '#' are comments."
    add "# Supported here: Item_*, Overworld_*, OverworldMarker_Bomb, Blocker_*, HintZone_*  (dungeon-room and global hotkeys are not in the preview yet)."
    add ""
    add "# ITEMS - while hovering an item box, sets that item; while hovering a map tile, toggles that item's marker icon"
    for i in 0 .. TrackerModel.ITEMS.itemNamesAndCounts.Length - 1 do add ("Item_" + itemHotKeyName i + " = ")
    add "Item_Nothing = "
    add ""
    add "# OVERWORLD - while hovering a map tile"
    for i in 0 .. TrackerModel.dummyOverworldTiles.Length - 1 do add ("Overworld_" + TrackerModel.MapSquareChoiceDomainHelper.AsHotKeyName(i) + " = ")
    add "Overworld_Nothing = "
    add "OverworldMarker_Bomb = "
    add ""
    add "# BLOCKERS - while hovering a blocker box"
    for b in TrackerModel.DungeonBlocker.All do add (b.AsHotKeyName() + " = ")
    add ""
    add "# HINT ZONES - while hovering a hint decoder zone button"
    for hz in TrackerModel.HintZone.All do add (hz.AsHotKeyName() + " = ")
    sb.ToString()
let loadHotKeys () =
    hotKeyMap.Clear()
    try
        let f = hotKeyFilePath ()
        if not (System.IO.File.Exists f) then System.IO.File.WriteAllText(f, makeHotKeyTemplate ())
        for raw in System.IO.File.ReadAllLines f do
            let line = raw.Trim()
            if line.Length > 0 && not (line.StartsWith "#") && line.Contains "=" then
                let i = line.IndexOf '='
                let name = line.Substring(0, i).Trim()
                let key = System.String.Join(" ", line.Substring(i + 1).Trim().ToUpperInvariant().Split([| ' ' |], System.StringSplitOptions.RemoveEmptyEntries))
                if key <> "" && not (hotKeyMap.ContainsKey key) then hotKeyMap.[key] <- name
    with _ -> ()   // no file system (browser) or unreadable file: just no hotkeys
let comboOf (e:Avalonia.Input.KeyEventArgs) : string option =
    let k = e.Key
    let keyText =
        if k >= Avalonia.Input.Key.A && k <= Avalonia.Input.Key.Z then Some(string (char (int 'A' + int k - int Avalonia.Input.Key.A)))
        elif k >= Avalonia.Input.Key.D0 && k <= Avalonia.Input.Key.D9 then Some(string (int k - int Avalonia.Input.Key.D0))
        else None
    let m = e.KeyModifiers
    let prefix =
        if m = Avalonia.Input.KeyModifiers.None then Some ""
        elif m = Avalonia.Input.KeyModifiers.Shift then Some "SHIFT "
        elif m = Avalonia.Input.KeyModifiers.Control then Some "CTRL "
        elif m = Avalonia.Input.KeyModifiers.Alt then Some "ALT "
        else None
    match keyText, prefix with
    | Some t, Some p -> Some(p + t)
    | _ -> None
let handleKeyDown (e:Avalonia.Input.KeyEventArgs) =
    match e.Source with
    | :? TextBox -> ()
    | _ ->
        match comboOf e, hoverHandler.Value with
        | Some combo, Some h ->
            match hotKeyMap.TryGetValue combo with
            | true, name -> h name; e.Handled <- true
            | _ -> ()
        | _ -> ()

// one item box (dungeon box, or the coast / armos / white-sword boxes): left-click checks/unchecks (Shift: skipped), right-click opens the item grid
let makeBoxCell (itemMenu:Popup) (onChange: unit -> unit) (b:TrackerModel.Box) : Control =
    let holder = Border(Width=33., Height=33., Background=Brushes.Black, BorderThickness=Thickness(3.), Cursor=Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand))
    let refresh () =
        let c = b.CellCurrent()
        holder.Child <- (if c = -1 then null else (let ic = itemIcon c in ic.HorizontalAlignment <- HorizontalAlignment.Center; ic.VerticalAlignment <- VerticalAlignment.Center; ic))
        holder.BorderBrush <-
            (match b.PlayerHas() with
             | TrackerModel.PlayerHas.YES -> Brushes.LimeGreen
             | TrackerModel.PlayerHas.SKIPPED -> Brushes.White
             | _ -> Brushes.Red)
        holder.Opacity <- (if c <> -1 && b.PlayerHas() = TrackerModel.PlayerHas.NO then 0.6 else 1.0)
    refresh ()
    b.Changed.Add(fun _ -> refresh ())
    hoverHotKeys holder (fun name ->
        if name = "Item_Nothing" then (b.AttemptToSet(-1, b.PlayerHas()) |> ignore; onChange ())
        elif name.StartsWith "Item_" then
            match [ 0 .. TrackerModel.ITEMS.itemNamesAndCounts.Length - 1 ] |> List.tryFind (fun i -> "Item_" + itemHotKeyName i = name) with
            | Some ident -> (b.AttemptToSet(ident, b.PlayerHas()) |> ignore; onChange ())
            | None -> ())
    holder.PointerPressed.Add(fun e ->
        let props = e.GetCurrentPoint(holder).Properties
        if props.IsRightButtonPressed then
            itemMenu.Child <- makeItemMenu (fun id -> b.AttemptToSet(id, b.PlayerHas()) |> ignore; itemMenu.IsOpen <- false; onChange ())
            itemMenu.PlacementTarget <- holder
            itemMenu.IsOpen <- true
        else
            let skip = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift)
            b.SetPlayerHas(match b.PlayerHas() with
                           | TrackerModel.PlayerHas.NO -> (if skip then TrackerModel.PlayerHas.SKIPPED else TrackerModel.PlayerHas.YES)
                           | _ -> TrackerModel.PlayerHas.NO)
        onChange ())
    holder :> Control

// the three single item boxes next to the items (coast / armos / white sword cave): same boxes the Windows tracker has
let makeExtrasPanel (onChange: unit -> unit) : Control =
    let itemMenu = Popup(Placement=PlacementMode.Right, IsLightDismissEnabled=true)
    let col = StackPanel(Spacing=3.)
    // top to bottom: white sword cave, coast, armos
    for (b, tip) in [ TrackerModel.sword2Box, "White sword cave item"; TrackerModel.ladderBox, "Coast item"; TrackerModel.armosBox, "Armos item" ] do
        let c = makeBoxCell itemMenu onChange b
        ToolTip.SetTip(c, tip)
        col.Children.Add(c)
    col.Children.Add(itemMenu)
    col :> Control

// the three reminder pictures that sit to the left of those boxes (white sword, ladder from the coast, power bracelet from the armos)
let makeExtrasIcons () : Control =
    let col = StackPanel(Spacing=3.)
    for id in [ 13; 4; 3 ] do
        let ic = itemIcon id
        ic.HorizontalAlignment <- HorizontalAlignment.Center
        ic.VerticalAlignment <- VerticalAlignment.Center
        col.Children.Add(Border(Width=33., Height=33., Child=ic, IsHitTestVisible=false))
    col :> Control

let makeDungeonPanel (onChange: unit -> unit) : Control =
    let itemMenu = Popup(Placement=PlacementMode.Right, IsLightDismissEnabled=true)
    let outer = StackPanel(Orientation=Orientation.Horizontal, Spacing=3.)
    for di = 0 to 8 do
        let d = TrackerModel.GetDungeon(di)
        let col = StackPanel(Spacing=3.)
        let tri = Avalonia.Controls.Shapes.Polygon(Points=Avalonia.Collections.AvaloniaList<Point>([ Point(16., 0.); Point(32., 26.); Point(0., 26.) ]), Fill=Brushes.DimGray, IsHitTestVisible=false)
        let label = TextBlock(Foreground=Brushes.White, FontWeight=FontWeight.Bold, FontSize=13., IsHitTestVisible=false)
        let host = Canvas(Width=33., Height=28.)
        host.Children.Add(tri)
        Canvas.SetLeft(label, 12.)
        Canvas.SetTop(label, 8.)
        host.Children.Add(label)
        let header = Border(Background=Brushes.Transparent, Child=host, Cursor=Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand))
        ToolTip.SetTip(header, "Click: toggle this dungeon's triforce.  Boxes: left-click checks/unchecks (Shift: skipped), right-click picks the item")
        let refreshHeader () =
            label.Text <- (if di = 8 then "9" elif d.LabelChar = '?' then string (di + 1) else string d.LabelChar)
            tri.Fill <- (if di = 8 then Brushes.DimGray elif d.PlayerHasTriforce() then Brushes.Gold else Brushes.DimGray)
            label.Foreground <- (if di < 8 && d.PlayerHasTriforce() then Brushes.Black else Brushes.White)
        refreshHeader ()
        header.PointerPressed.Add(fun _ -> if di < 8 then (d.ToggleTriforce(); refreshHeader (); onChange ()))
        if di < 8 then d.PlayerHasTriforceChanged.Add(fun _ -> refreshHeader ())
        col.Children.Add(header)
        for b in d.Boxes do
            col.Children.Add(makeBoxCell itemMenu onChange b)
        outer.Children.Add(col)
    let wrap = StackPanel()
    wrap.Children.Add(outer)
    wrap.Children.Add(itemMenu)
    wrap :> Control

let progressFlags () : TrackerModel.BoolProperty list =
    let P = TrackerModel.playerProgressAndTakeAnyHearts
    [ P.PlayerHasBoomBook; P.PlayerHasWoodArrow; P.PlayerHasBlueRing; P.PlayerHasBlueCandle ]

let hintZoneNames = [| "?"; "Death Mountain"; "Lake"; "Lost Hills"; "River"; "Graveyard"; "Desert"; "Coast"; "Dead Woods"; "Near Start"; "Forest" |]
let hintBoolGetters : (string * (unit -> bool) * (bool -> unit)) list =
    [ "No feat of strength... (Power Bracelet / pushing graves not required)", (fun () -> TrackerModel.NoFeatOfStrengthHintWasGiven), (fun v -> TrackerModel.NoFeatOfStrengthHintWasGiven <- v)
      "Sail not... (Raft not required)", (fun () -> TrackerModel.SailNotHintWasGiven), (fun v -> TrackerModel.SailNotHintWasGiven <- v)
      "Sail across the water... (Raft required to reach a place)", (fun () -> TrackerModel.SailToHintWasGiven), (fun v -> TrackerModel.SailToHintWasGiven <- v)
      "Play a melody... (Recorder required)", (fun () -> TrackerModel.PlayMelodyHintWasGiven), (fun v -> TrackerModel.PlayMelodyHintWasGiven <- v)
      "Step over the water... (Ladder required)", (fun () -> TrackerModel.StepOverWaterHintWasGiven), (fun v -> TrackerModel.StepOverWaterHintWasGiven <- v)
      "Fire the arrow... (Bow and arrow required for Gohma)", (fun () -> TrackerModel.FireArrowsHintWasGiven), (fun v -> TrackerModel.FireArrowsHintWasGiven <- v) ]

// items / triforce / dungeon boxes, as one text line:  flags|triforces|di.bj.cell.ph;...
let saveExtras () =
    let b2s (b:bool) = if b then "1" else "0"
    let flags = startingFlags () |> List.map (fun f -> b2s (f.Value())) |> String.concat ""
    let tri = [ for i in 0..7 -> b2s (TrackerModel.GetDungeon(i).PlayerHasTriforce()) ] |> String.concat ""
    let boxes = [ for di in 0..8 do
                    let d = TrackerModel.GetDungeon(di)
                    let bs = d.Boxes
                    for bj in 0 .. bs.Length - 1 do
                        yield sprintf "%d.%d.%d.%d" di bj (bs.[bj].CellCurrent()) (bs.[bj].PlayerHas().AsInt()) ] |> String.concat ";"
    let prog = (progressFlags () |> List.map (fun f -> b2s (f.Value())) |> String.concat "")
               + String.concat "" [ for i in 0..3 -> string (TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyHeart(i)) ]
    let hints = String.concat "" [ for i in 0..10 -> string (Array.findIndex (fun x -> x = TrackerModel.GetLevelHint(i)) TrackerModel.HintZone.All |> fun n -> char (int 'a' + n)) ]
                + (hintBoolGetters |> List.map (fun (_, g, _) -> b2s (g ())) |> String.concat "")
    flags + "|" + tri + "|" + boxes + "|" + prog + "|" + hints

let loadExtras (line:string) =
    let parts = line.Split('|')
    if parts.Length >= 3 then
        startingFlags () |> List.iteri (fun i f -> if i < parts.[0].Length && (parts.[0].[i] = '1') <> f.Value() then f.Toggle())
        for i in 0..7 do
            let d = TrackerModel.GetDungeon(i)
            if i < parts.[1].Length && (parts.[1].[i] = '1') <> d.PlayerHasTriforce() then d.ToggleTriforce()
        for e in parts.[2].Split([|';'|], System.StringSplitOptions.RemoveEmptyEntries) do
            let v = e.Split('.') |> Array.map int
            let bs = TrackerModel.GetDungeon(v.[0]).Boxes
            if v.[1] < bs.Length then bs.[v.[1]].Set(v.[2], TrackerModel.PlayerHas.FromInt v.[3])
        if parts.Length >= 4 && parts.[3].Length >= 8 then
            progressFlags () |> List.iteri (fun i f -> if (parts.[3].[i] = '1') <> f.Value() then f.Toggle())
            for i in 0..3 do TrackerModel.playerProgressAndTakeAnyHearts.SetTakeAnyHeart(i, int parts.[3].[4+i] - int '0')
        if parts.Length >= 5 && parts.[4].Length >= 17 then
            for i in 0..10 do TrackerModel.SetLevelHint(i, TrackerModel.HintZone.All.[int parts.[4].[i] - int 'a'])
            hintBoolGetters |> List.iteri (fun i (_, _, set) -> set (parts.[4].[11 + i] = '1'))

// ---- progress panel: the 4 take-any-heart caves (the cave items now live in the item grid) ----
let makeProgressPanel (onChange: unit -> unit) : Control =
    let P = TrackerModel.playerProgressAndTakeAnyHearts
    let row = StackPanel(Orientation=Orientation.Horizontal, Spacing=3.)
    let hand = Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
    for i = 0 to 3 do
        let t = TextBlock(FontSize=40., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
        let b = Border(Width=46., Height=46., Background=Brushes.Black, Child=t, Cursor=hand)
        let refresh () =
            match P.GetTakeAnyHeart(i) with
            | 1 -> t.Text <- "\u2665"; t.Foreground <- Brushes.Red
            | 2 -> t.Text <- "\u2715"; t.Foreground <- Brushes.Gray
            | _ -> t.Text <- "\u2665"; t.Foreground <- Brushes.DimGray
        refresh ()
        ToolTip.SetTip(b, "Take-any cave: click to cycle (open / took heart / took potion or candle)")
        b.PointerPressed.Add(fun _ -> P.SetTakeAnyHeart(i, (P.GetTakeAnyHeart(i) + 1) % 3); refresh (); onChange ())
        row.Children.Add(b)
    row :> Control

// the whole items area like the Windows tracker: [reminder pictures | cave boxes | item grid] over the four take-any hearts, with the stats column on the right
let makeItemArea (onChange: unit -> unit) (stats: Control) : Control =
    let topRow = StackPanel(Orientation=Orientation.Horizontal, Spacing=6.)
    topRow.Children.Add(makeExtrasIcons ())
    topRow.Children.Add(makeExtrasPanel onChange)
    topRow.Children.Add(makeItemsPanel onChange)
    let left = StackPanel(Spacing=6.)
    left.Children.Add(topRow)
    left.Children.Add(makeProgressPanel onChange)
    let outer = StackPanel(Orientation=Orientation.Horizontal, Spacing=12.)
    outer.Children.Add(left)
    outer.Children.Add(stats)
    outer :> Control

// ---- hints panel: where each dungeon / sword cave was hinted to be, plus the "was given" hint checkboxes ----
let makeHintPanel (onChange: unit -> unit) : Control =
    let hand = Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
    let txt (t:string) (w:float) = TextBlock(Text=t, Foreground=Brushes.Orange, FontSize=15., Width=w, VerticalAlignment=VerticalAlignment.Center, TextWrapping=TextWrapping.Wrap)
    let zonePopup = Popup(Placement=PlacementMode.Right, IsLightDismissEnabled=true)
    let sp = StackPanel(Spacing=0.)
    sp.Children.Add(TextBlock(Text="Each hinted-but-not-yet-found location is noted here.  Click a zone to choose where the hint says it is.", Foreground=Brushes.Orange, FontSize=14., TextWrapping=TextWrapping.Wrap, Width=640., Margin=Thickness(0., 0., 0., 6.)))
    let order = (let zs = [| for i in 0..10 -> i |] in if TrackerModelOptions.AlphabetizeHintZones.Value then Array.append [| 0 |] (zs.[1..] |> Array.sortBy (fun i -> TrackerModel.HintZone.FromIndex(i).ToString())) else zs)
    for i in 0..10 do
        let (phrase, meaning) = OverworldData.hintMeanings.[i]
        let row = StackPanel(Orientation=Orientation.Horizontal, Spacing=6., Margin=Thickness(0., 1., 0., 1.))
        row.Children.Add(txt phrase 200.)
        row.Children.Add(Border(Width=34., Height=30., Background=Brushes.Black, BorderBrush=Brushes.LightGray, BorderThickness=Thickness(1.),
                                Child=TextBlock(Text=(if i < 8 then string (i + 1) elif i = 8 then "9" else (if i = 9 then "WS" else "MS")), Foreground=Brushes.Gray, FontWeight=FontWeight.Bold, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)))
        row.Children.Add(txt meaning 170.)
        let zoneText = TextBlock(Foreground=Brushes.Orange, FontSize=15., VerticalAlignment=VerticalAlignment.Center)
        let zoneBtn = Border(Width=190., Height=30., Background=SolidColorBrush(Color.FromRgb(30uy, 30uy, 30uy)), BorderBrush=Brushes.LightGray, BorderThickness=Thickness(1.), Padding=Thickness(6., 0.), Child=zoneText, Cursor=hand)
        let refresh () =
            let z = TrackerModel.GetLevelHint(i)
            if z = TrackerModel.HintZone.UNKNOWN then
                zoneText.Text <- "Click to select"
                zoneText.Foreground <- Brushes.DeepSkyBlue
            else
                zoneText.Text <- z.ToString()
                zoneText.Foreground <- Brushes.Orange
        refresh ()
        let setZone (zi:int) =
            TrackerModel.SetLevelHint(i, TrackerModel.HintZone.FromIndex(zi))
            TrackerModel.forceUpdate ()
            refresh ()
            onChange ()
        zoneBtn.PointerPressed.Add(fun _ ->
            let list = StackPanel(Spacing=1.)
            for zi in order do
                let item = Border(Width=190., Height=28., Background=Brushes.Black, Padding=Thickness(6., 0.), Cursor=hand,
                                  Child=TextBlock(Text=TrackerModel.HintZone.FromIndex(zi).ToString(), Foreground=Brushes.Orange, FontSize=15., VerticalAlignment=VerticalAlignment.Center))
                item.PointerPressed.Add(fun _ -> setZone zi; zonePopup.IsOpen <- false)
                list.Children.Add(item)
            zonePopup.Child <- Border(BorderBrush=Brushes.Gray, BorderThickness=Thickness(2.), Background=Brushes.Black, Child=list)
            zonePopup.PlacementTarget <- zoneBtn
            zonePopup.IsOpen <- true)
        zoneBtn.PointerWheelChanged.Add(fun e ->
            let all = TrackerModel.HintZone.All
            let idx = Array.findIndex (fun x -> x = TrackerModel.GetLevelHint(i)) all
            setZone ((idx + (if e.Delta.Y > 0. then all.Length - 1 else 1)) % all.Length)
            e.Handled <- true)
        hoverHotKeys zoneBtn (fun name ->
            match TrackerModel.HintZone.All |> Array.tryFind (fun hz -> hz.AsHotKeyName() = name) with
            | Some hz -> setZone (hz.ToIndex())
            | None -> ())
        row.Children.Add(zoneBtn)
        sp.Children.Add(row)
    sp.Children.Add(zonePopup)
    // the other hints: you track these yourself
    sp.Children.Add(TextBlock(Text="Here are the meanings of some hints, which you need to track on your own:", Foreground=Brushes.Orange, FontSize=14., Margin=Thickness(0., 10., 0., 4.)))
    for (d, m) in [ "A feat of strength will lead to...", "Either push a gravestone, or push an overworld rock requiring Power Bracelet"
                    "Sail across the water...", "Raft required to reach a place"
                    "Play a melody...", "Either an overworld recorder spot, or a Digdogger in a dungeon logically blocks..."
                    "Fire the arrow...", "In a dungeon, Gohma logically blocks..."
                    "Step over the water...", "Ladder required to obtain... (coast item, overworld river, or dungeon moat)" ] do
        let r = StackPanel(Orientation=Orientation.Horizontal, Spacing=6.)
        r.Children.Add(txt d 240.)
        r.Children.Add(txt m 400.)
        sp.Children.Add(r)
    sp.Children.Add(TextBlock(Text="Check the hints you were given:", Foreground=Brushes.Orange, FontSize=14., Margin=Thickness(0., 10., 0., 4.)))
    for (name, get, set) in hintBoolGetters do
        let cb = CheckBox(Content=name, IsChecked=System.Nullable<bool>(get ()), Foreground=Brushes.Orange, FontSize=14.)
        cb.IsCheckedChanged.Add(fun _ -> set (cb.IsChecked.GetValueOrDefault(false)); onChange ())
        sp.Children.Add(cb)
    sp :> Control

// ---- dungeon tabs: 1-8, 9 and Summary (the room-by-room dungeon map editor is not ported yet) ----
let makeDungeonTabs () : Control * (unit -> unit) =
    let hand = Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
    let content = Border(Height=300., Background=Brushes.Black, BorderBrush=Brushes.Gray, BorderThickness=Thickness(1.))
    let selected = ref 9   // 0-7 = dungeons 1-8, 8 = dungeon 9, 9 = Summary
    let tabRow = StackPanel(Orientation=Orientation.Horizontal, Spacing=2.)
    let tabButtons = [| for i in 0..9 -> Border(Width=50., Height=26., BorderBrush=Brushes.Gray, BorderThickness=Thickness(1.), Cursor=hand) |]
    let tabLabels = [| for i in 0..9 -> TextBlock(Text=(if i = 9 then "S" elif i = 8 then "9" else string (i + 1)), Foreground=Brushes.Orange, FontSize=14., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center) |]
    let locText (di:int) =
        if TrackerModel.GetDungeon(di).HasBeenLocated() then
            let loc = TrackerModel.mapStateSummary.DungeonLocations.[di]
            if loc <> TrackerModel.NOTFOUND then
                let (x, y) = loc
                sprintf "found: column %d, row %d" (x + 1) (y + 1)
            else "found"
        else "not yet found"
    let rec render () =
        for i in 0..9 do
            tabButtons.[i].Background <- (if i = selected.Value then Brushes.White else Brushes.Black)
            tabLabels.[i].Foreground <- (if i = selected.Value then Brushes.Black else Brushes.Orange)
        if selected.Value = 9 then
            let grid = Grid()
            for _ in 0..2 do grid.ColumnDefinitions.Add(ColumnDefinition())
            for _ in 0..2 do grid.RowDefinitions.Add(RowDefinition())
            for di in 0..7 do
                let d = TrackerModel.GetDungeon(di)
                let num = TextBlock(Text=string (if d.LabelChar = '?' then char (int '1' + di) else d.LabelChar), FontSize=56., FontWeight=FontWeight.Bold, Foreground=(if d.PlayerHasTriforce() then Brushes.Gold else Brushes.MediumPurple), HorizontalAlignment=HorizontalAlignment.Center)
                let sub = TextBlock(Text=locText di, Foreground=Brushes.Orange, FontSize=13., HorizontalAlignment=HorizontalAlignment.Center)
                let cell = StackPanel(VerticalAlignment=VerticalAlignment.Center)
                cell.Children.Add(num)
                cell.Children.Add(sub)
                let b = Border(BorderBrush=Brushes.DimGray, BorderThickness=Thickness(1.), Child=cell, Cursor=hand)
                b.PointerPressed.Add(fun _ -> selected.Value <- di; (render ()))
                Grid.SetColumn(b, di % 3)
                Grid.SetRow(b, di / 3)
                grid.Children.Add(b)
            let info = StackPanel(VerticalAlignment=VerticalAlignment.Center, Margin=Thickness(8.))
            info.Children.Add(TextBlock(Text="Dungeon Summary", Foreground=Brushes.Orange, FontSize=14., HorizontalAlignment=HorizontalAlignment.Center))
            info.Children.Add(TextBlock(Text="Click a dungeon to open its tab", Foreground=Brushes.Orange, FontSize=12., HorizontalAlignment=HorizontalAlignment.Center))
            Grid.SetColumn(info, 2)
            Grid.SetRow(info, 2)
            grid.Children.Add(info)
            content.Child <- grid
        else
            let di = selected.Value
            let d = TrackerModel.GetDungeon(di)
            let p = StackPanel(Margin=Thickness(12.), Spacing=6.)
            p.Children.Add(TextBlock(Text=sprintf "Dungeon %d" (di + 1), Foreground=Brushes.MediumPurple, FontSize=28., FontWeight=FontWeight.Bold))
            p.Children.Add(TextBlock(Text=locText di, Foreground=Brushes.Orange, FontSize=14.))
            if di < 8 then p.Children.Add(TextBlock(Text=(if d.PlayerHasTriforce() then "Triforce: collected" else "Triforce: not yet collected"), Foreground=Brushes.Orange, FontSize=14.))
            let doneCount = d.Boxes |> Array.filter (fun b -> b.IsDone()) |> Array.length
            p.Children.Add(TextBlock(Text=sprintf "Item boxes done: %d of %d" doneCount d.Boxes.Length, Foreground=Brushes.Orange, FontSize=14.))
            p.Children.Add(TextBlock(Text="(room-by-room dungeon map: coming in a later step)", Foreground=Brushes.Gray, FontSize=12.))
            content.Child <- p
    for i in 0..9 do
        tabButtons.[i].Child <- tabLabels.[i]
        tabButtons.[i].PointerPressed.Add(fun _ -> selected.Value <- i; render ())
        tabRow.Children.Add(tabButtons.[i])
    render ()
    let root = StackPanel(Margin=Thickness(0., 6., 0., 0.))
    root.Children.Add(tabRow)
    root.Children.Add(content)
    root :> Control, render

// ---- blockers: up to 3 per dungeon (left-click: next, right-click: previous) ----
let blockerLabel (bk:TrackerModel.DungeonBlocker) : string * IBrush =
    let n = bk.AsHotKeyName()
    let maybe = n.Contains("Maybe")
    let txt =
        if n.Contains("Bow") then "BOW" elif n.Contains("Recorder") then "REC" elif n.Contains("Ladder") then "LAD" elif n.Contains("Key") then "KEY"
        elif n.Contains("Bait") then "BAIT" elif n.Contains("Money") then "$" elif n.Contains("Bomb") then "BOMB" elif n.Contains("Combat") then "FIGHT" else ""
    (if maybe && txt <> "" then "?" + txt else txt), (if maybe then Brushes.Gray else Brushes.Orange)

let makeBlockersPanel (onChange: unit -> unit) : Control =
    let all = TrackerModel.DungeonBlocker.All
    let grid = WrapPanel(Orientation=Orientation.Horizontal, Width=420.)
    for di in 0..7 do
        let group = StackPanel(Orientation=Orientation.Horizontal, Margin=Thickness(2., 2., 12., 4.), Spacing=1.)
        group.Children.Add(TextBlock(Text=string (di + 1), Foreground=Brushes.Orange, FontSize=14., VerticalAlignment=VerticalAlignment.Center, Margin=Thickness(0., 0., 4., 0.)))
        for j in 0 .. TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON - 1 do
            let t = TextBlock(FontSize=10., FontWeight=FontWeight.Bold, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
            let cellB = Border(Width=30., Height=30., Background=Brushes.Black, BorderBrush=Brushes.Gray, BorderThickness=Thickness(2.), Child=t, Cursor=Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand))
            let refresh () =
                let (txt, col) = blockerLabel (TrackerModel.DungeonBlockersContainer.GetDungeonBlocker(di, j))
                t.Text <- txt
                t.Foreground <- col
            refresh ()
            hoverHotKeys cellB (fun name ->
                if name.StartsWith "Blocker_" then
                    TrackerModel.DungeonBlockersContainer.SetDungeonBlocker(di, j, TrackerModel.DungeonBlocker.FromHotKeyName name)
                    refresh ()
                    onChange ())
            cellB.PointerPressed.Add(fun e ->
                let cur = TrackerModel.DungeonBlockersContainer.GetDungeonBlocker(di, j)
                let idx = Array.findIndex (fun x -> x = cur) all
                let step = if e.GetCurrentPoint(cellB).Properties.IsRightButtonPressed then all.Length - 1 else 1
                TrackerModel.DungeonBlockersContainer.SetDungeonBlocker(di, j, all.[(idx + step) % all.Length])
                refresh ()
                onChange ())
            group.Children.Add(cellB)
        grid.Children.Add(group)
    let wrap = StackPanel()
    wrap.Children.Add(TextBlock(Text="BLOCKERS", Foreground=Brushes.Orange, FontSize=14.))
    wrap.Children.Add(grid)
    wrap :> Control

// ---- item progress row (under the legend, like Windows): the best version of each key item you hold, dimmed while you don't have it yet ----
let makeItemProgress () : Control * (unit -> unit) =
    let pcs () = TrackerModel.playerComputedStateSummary
    let haveMeat () =
        TrackerModel.startingItemsAndExtras.PlayerHasMeat.Value()
        || TrackerModel.mapCollectedCount.[TrackerModel.ITEMS.MEAT] > 0
        || (TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes() |> Seq.exists (fun b -> b.CellCurrent() = TrackerModel.ITEMS.MEAT && b.PlayerHas() = TrackerModel.PlayerHas.YES))
    // each slot says which item picture to show and whether it is held
    let slots : (string * (unit -> int * bool)) list =
        [ "Sword", (fun () -> let l = pcs().SwordLevel in [| 19; 19; 13; 20 |].[min l 3], l > 0)
          "Candle", (fun () -> match pcs().CandleLevel with | 0 -> 9, false | 1 -> 24, true | _ -> 9, true)
          "Ring", (fun () -> match pcs().RingLevel with | 0 -> 22, false | 1 -> 22, true | _ -> 10, true)
          "Bow", (fun () -> 2, pcs().HaveBow)
          "Arrow", (fun () -> match pcs().ArrowLevel with | 0 -> 11, false | 1 -> 23, true | _ -> 11, true)
          "Wand", (fun () -> 12, pcs().HaveWand)
          "Book", (fun () -> TrackerModel.ITEMS.BOOKSHIELD, (if TrackerModel.IsCurrentlyBook() then pcs().HaveBookOrShield else TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBoomBook.Value()))
          "Boomerang", (fun () -> match pcs().BoomerangLevel with | 0 -> 5, false | 1 -> 1, true | _ -> 5, true)
          "Ladder", (fun () -> 4, pcs().HaveLadder)
          "Recorder", (fun () -> 8, pcs().HaveRecorder)
          "Power Bracelet", (fun () -> 3, pcs().HavePowerBracelet)
          "Raft", (fun () -> 7, pcs().HaveRaft)
          "Key", (fun () -> 6, pcs().HaveAnyKey)
          "Meat", (fun () -> 26, haveMeat ()) ]
    let row = StackPanel(Orientation=Orientation.Horizontal, Spacing=6., Margin=Thickness(0., 4., 0., 0.))
    let verPopup = Popup(Placement=PlacementMode.Top, IsLightDismissEnabled=true)
    let verButton = Button(Content="v2.2-b3")
    verPopup.PlacementTarget <- verButton
    verPopup.Child <- Border(Background=Brushes.Black, BorderBrush=Brushes.Orange, BorderThickness=Thickness(2.), Padding=Thickness(10.), Child=TextBlock(Text=OverworldData.AboutBody, Foreground=Brushes.Orange, FontSize=14.))
    verButton.Click.Add(fun _ -> verPopup.IsOpen <- not verPopup.IsOpen)
    row.Children.Add(verButton)
    row.Children.Add(verPopup)
    row.Children.Add(Border(BorderBrush=Brushes.Orange, Background=SolidColorBrush(Color.FromRgb(30uy, 30uy, 30uy)), BorderThickness=Thickness(1.), Padding=Thickness(8., 3.), Child=TextBlock(Text="Item Progress", Foreground=Brushes.Orange, FontSize=13.), VerticalAlignment=VerticalAlignment.Center))
    let holders =
        [ for (name, f) in slots do
            let h = Border(Width=30., Height=30., Background=Brushes.Black)
            ToolTip.SetTip(h, name)
            row.Children.Add(h)
            yield h, f, System.Collections.Generic.Dictionary<int, Control>() ]
    let refresh () =
        for (h, f, cache) in holders do
            let (id, lit) = f ()
            let ic =
                match cache.TryGetValue id with
                | true, c -> c
                | _ ->
                    let c = Viewbox(Width=24., Height=24., Child=itemIcon id) :> Control
                    cache.[id] <- c
                    c
            ic.Opacity <- (if lit then 1.0 else 0.25)
            if not (obj.ReferenceEquals(h.Child, ic)) then h.Child <- ic
    (row :> Control), refresh

// ---- legend (under the map, like the Windows tracker) ----
let makeLegend () : Control =
    let row = StackPanel(Orientation=Orientation.Horizontal, Spacing=8., Margin=Thickness(0., 6., 0., 0.))
    let txt (t:string) = TextBlock(Text=t, Foreground=Brushes.Orange, FontSize=13., VerticalAlignment=VerticalAlignment.Center)
    let box (bg:IBrush) (t:string) = Border(Width=22., Height=28., Background=bg, Child=TextBlock(Text=t, Foreground=Brushes.Black, FontWeight=FontWeight.Bold, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center))
    row.Children.Add(txt "LEGEND -")
    row.Children.Add(box Brushes.Orchid "1")
    row.Children.Add(txt "Any Road (Warp)")
    row.Children.Add(box Brushes.Yellow "1")
    row.Children.Add(txt "Dungeon (Complete)")
    row.Children.Add(Avalonia.Controls.Shapes.Polygon(Points=Avalonia.Collections.AvaloniaList<Point>([ Point(14., 0.); Point(28., 14.); Point(14., 28.); Point(0., 14.) ]), Fill=Brushes.Gold))
    row.Children.Add(txt "Recorder Destination")
    row :> Control

// ---- the settings grid shared by the startup window and the timeline's Options... popup ----
let makeSettingsGrid () : Control =
    let orange = Brushes.Orange
    let opt (label:string) (b:TrackerModelOptions.Bool) =
        let cb = CheckBox(Content=label, IsChecked=System.Nullable<bool>(b.Value), Foreground=orange, FontSize=15.)
        cb.IsCheckedChanged.Add(fun _ -> b.Value <- cb.IsChecked.GetValueOrDefault(false); (try TrackerModelOptions.writeSettings() with _ -> ()))
        cb
    let column (title:string) (items:Control list) =
        let sp = StackPanel(Spacing=4., Margin=Thickness(8.))
        sp.Children.Add(TextBlock(Text=title, Foreground=orange, FontWeight=FontWeight.Bold, FontSize=15.))
        for i in items do sp.Children.Add(i)
        sp
    let cols = Grid()
    for _ in 0..2 do cols.ColumnDefinitions.Add(ColumnDefinition())
    let c1 = column "Overworld Settings" [ opt "Draw routes" TrackerModelOptions.Overworld.DrawRoutes; opt "Show screen scrolls" TrackerModelOptions.Overworld.RoutesCanScreenScroll; opt "Highlight nearby" TrackerModelOptions.Overworld.HighlightNearby; opt "Show magnifier" TrackerModelOptions.Overworld.ShowMagnifier
                                           TextBlock(Text="Dungeon Settings", Foreground=orange, FontWeight=FontWeight.Bold, FontSize=15., Margin=Thickness(0., 8., 0., 0.))
                                           opt "Dungeon Map Hint" TrackerModelOptions.DisplayIconsInDungeonMap; opt "BOARD instead of LEVEL" TrackerModelOptions.BOARDInsteadOfLEVEL; opt "Show basement info" TrackerModelOptions.ShowBasementInfo
                                           opt "Do door inference" TrackerModelOptions.DoDoorInference; opt "Book for Helpful Hints" TrackerModelOptions.BookForHelpfulHints; opt "Left-drag auto-inverts" TrackerModelOptions.LeftClickDragAutoInverts
                                           opt "Default to NonDescript" TrackerModelOptions.DefaultRoomPreferNonDescriptToMaybePushBlock; opt "Dungeon 'sunglasses'" TrackerModelOptions.GiveDungeonTrackerSunglasses; opt "Reverse scroll wheel" TrackerModelOptions.ReverseScrollWheelDirection ] 
    let c2 = column "Reminders (visual)" [ opt "Dungeon feedback" TrackerModelOptions.VisualReminders.DungeonFeedback; opt "Sword hearts" TrackerModelOptions.VisualReminders.SwordHearts; opt "Coast Item" TrackerModelOptions.VisualReminders.CoastItem; opt "Recorder spots" TrackerModelOptions.VisualReminders.RecorderSpots; opt "Power Bracelet spots" TrackerModelOptions.VisualReminders.PowerBraceletSpots
                                           opt "Boomstick book" TrackerModelOptions.VisualReminders.BoomstickBook; opt "Have magic key" TrackerModelOptions.VisualReminders.HaveMagicKey; opt "Have ladder" TrackerModelOptions.VisualReminders.HaveLadder; opt "Blockers" TrackerModelOptions.VisualReminders.Blockers; opt "Door Repair Count" TrackerModelOptions.VisualReminders.DoorRepair; opt "Overworld overwrites" TrackerModelOptions.VisualReminders.OverworldOverwrites ]
    let c3 = column "Other" [ opt "Animate tile changes" TrackerModelOptions.AnimateTileChanges; opt "Animate shop highlights" TrackerModelOptions.AnimateShopHighlights; opt "Save on completion" TrackerModelOptions.SaveOnCompletion
                              opt "Check for updates on startup" TrackerModelOptions.CheckForUpdates; opt "Hide timer" TrackerModelOptions.HideTimer; opt "Race Mode" TrackerModelOptions.RaceMode ]
    Grid.SetColumn(c2, 1)
    Grid.SetColumn(c3, 2)
    cols.Children.Add(c1)
    cols.Children.Add(c2)
    cols.Children.Add(c3)
    cols :> Control

// ---- timer + timeline (the Windows tracker's bottom strip): driven by the real model's start time, OW-spots-remaining graph and item/triforce/heart timestamps ----
let timelineBox (id:string) : TrackerModel.Box option =
    match id with
    | "LadderBox" -> Some TrackerModel.ladderBox
    | "ArmosBox" -> Some TrackerModel.armosBox
    | "WhiteSwordBox" -> Some TrackerModel.sword2Box
    | "Level1or4Box3" -> Some TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.FinalBoxOf1Or4
    | s when s.StartsWith "Level" && s.Length >= 10 && System.Char.IsDigit s.[5] && System.Char.IsDigit s.[9] ->
        Some(TrackerModel.GetDungeon(int s.[5] - int '1').Boxes.[int s.[9] - int '1'])
    | _ -> None
let timelineIcon (id:string) : Control option =
    let I = TrackerModel.ITEMS.BOOKSHIELD
    match id with
    | "WoodSword" -> Some(itemIcon 19)
    | "WhiteSword" -> Some(itemIcon 13)
    | "MagicalSword" | "StartingMagicalSword" -> Some(itemIcon 20)
    | "Boomerang" -> Some(itemIcon 1)
    | "MagicBoomerang" -> Some(itemIcon 5)
    | "WoodArrow" -> Some(itemIcon 23)
    | "SilverArrow" -> Some(itemIcon 11)
    | "BlueCandle" -> Some(itemIcon 24)
    | "BlueRing" -> Some(itemIcon 22)
    | "RedCandle" -> Some(itemIcon 9)
    | "RedRing" -> Some(itemIcon 10)
    | "BoomstickBook" | "Book" -> Some(itemIcon I)
    | "Bow" -> Some(itemIcon 2)
    | "Wand" -> Some(itemIcon 12)
    | "PowerBracelet" -> Some(itemIcon 3)
    | "Raft" -> Some(itemIcon 7)
    | "Recorder" -> Some(itemIcon 8)
    | "AnyKey" -> Some(itemIcon 6)
    | "Ladder" -> Some(itemIcon 4)
    | "Gannon" -> Some(TextBlock(Text="G", Foreground=Brushes.Red, FontWeight=FontWeight.Bold, FontSize=18.) :> Control)
    | "Zelda" -> Some(TextBlock(Text="Z", Foreground=Brushes.Gold, FontWeight=FontWeight.Bold, FontSize=18.) :> Control)
    | s when s.StartsWith "TakeAnyHeart" -> Some(itemIcon 14)
    | s when s.StartsWith "Triforce" -> Some(itemIcon 29)
    | s ->
        match timelineBox s with
        | Some b -> Some(itemIcon (if b.CellCurrent() = -1 then 14 else b.CellCurrent()))
        | None -> None

let makeTimeline () : Control * Control =
    TrackerModel.theStartTime.SetNow()
    let numTicks, ticksPerHash = 60, 5
    let iconSize, numRows, SP, BIG = 21., 4, 6., 15.
    let lineWidth, ox = 930., 35.
    let iconAreaHeight = float numRows * (iconSize + SP)
    let TLC = SolidColorBrush(Color.FromRgb(244uy, 164uy, 96uy)) :> IBrush
    let paused = ref false
    let mode = ref 0
    let logScale = ref false
    let timeText = TextBlock(Foreground=Brushes.Orange, FontSize=18., FontWeight=FontWeight.Bold, VerticalAlignment=VerticalAlignment.Center)
    let axis = Canvas(Width=1000., Height=iconAreaHeight + BIG + 8., Background=Brushes.Black)
    let graphLayer = Canvas()
    let staticLayer = Canvas()
    let itemLayer = Canvas()
    let curLine = Line(Stroke=Brushes.White, StrokeThickness=3., IsHitTestVisible=false)
    axis.Children.Add(graphLayer)
    axis.Children.Add(staticLayer)
    axis.Children.Add(itemLayer)
    axis.Children.Add(curLine)
    let xf (i:float) = ox + i * (lineWidth / float numTicks)
    let elapsedSec () = max 0 (int (System.DateTime.Now - TrackerModel.theStartTime.Time).TotalSeconds)
    let buildStatic (mpt:int) =
        staticLayer.Children.Clear()
        staticLayer.Children.Add(Line(StartPoint=Point(xf 0., iconAreaHeight + BIG / 2.), EndPoint=Point(xf (float numTicks), iconAreaHeight + BIG / 2.), Stroke=TLC, StrokeThickness=3.))
        for i in 0 .. numTicks do
            if i % (2 * ticksPerHash) = 0 then
                staticLayer.Children.Add(Line(StartPoint=Point(xf (float i), iconAreaHeight), EndPoint=Point(xf (float i), iconAreaHeight + BIG), Stroke=TLC, StrokeThickness=3.))
                let m = i * mpt
                let t = TextBlock(Text=sprintf "%d:%02d" (m / 60) (m % 60), Foreground=TLC, FontSize=12.)
                Canvas.SetLeft(t, xf (float i) - 12.)
                Canvas.SetTop(t, iconAreaHeight + 6.)
                staticLayer.Children.Add(t)
            elif i % ticksPerHash = 0 then
                staticLayer.Children.Add(Line(StartPoint=Point(xf (float i), iconAreaHeight + BIG / 4.), EndPoint=Point(xf (float i), iconAreaHeight + BIG * 3. / 4.), Stroke=TLC, StrokeThickness=3.))
        let lbl = TextBlock(Text="OW Completion", Foreground=Brushes.DarkCyan, FontSize=11., FontWeight=FontWeight.Bold, IsHitTestVisible=false)
        lbl.RenderTransformOrigin <- RelativePoint.TopLeft
        lbl.RenderTransform <- RotateTransform(-90.)
        Canvas.SetLeft(lbl, 4.)
        Canvas.SetTop(lbl, iconAreaHeight - 4.)
        staticLayer.Children.Add(lbl)
    let draw () =
        let sec = elapsedSec ()
        let minute = sec / 60
        timeText.Text <- sprintf "%d:%02d:%02d" (sec / 3600) ((sec / 60) % 60) (sec % 60)
        let mpt = if minute <= 60 then 1 elif minute <= 120 then 2 else 3
        if mode.Value <> mpt then
            mode.Value <- mpt
            buildStatic mpt
        let curTick = min numTicks (minute / mpt)
        curLine.StartPoint <- Point(xf (float curTick) + 0.5, iconAreaHeight)
        curLine.EndPoint <- Point(xf (float curTick) + 0.5, iconAreaHeight + BIG)
        // overworld-spots-remaining graph
        graphLayer.Children.Clear()
        if TrackerModel.timelineDataOverworldSpotsRemain.Count > 0 then
            let sorted = TrackerModel.timelineDataOverworldSpotsRemain.ToArray() |> Array.sortBy fst
            let maxRemain = max 1 (sorted |> Array.maxBy snd |> snd)
            let remainPerTick = Array.create (numTicks + 1) -1
            remainPerTick.[0] <- maxRemain
            let mutable highest = -1
            for (s, r) in sorted do
                let bucket = 1 + (s / 60) / mpt
                if bucket >= 0 && bucket <= numTicks then
                    if bucket > highest then
                        for i = highest + 1 to bucket - 1 do
                            remainPerTick.[i] <- (if i = 0 then maxRemain else remainPerTick.[i - 1])
                        remainPerTick.[bucket] <- r
                        highest <- bucket
                    else
                        remainPerTick.[bucket] <- r
            if curTick > highest then
                let r = snd sorted.[sorted.Length - 1]
                for i = highest + 1 to curTick do
                    remainPerTick.[i] <- r
                highest <- curTick
            let y (r:int) = if logScale.Value then iconAreaHeight * (log (1. + float r) / log (1. + float maxRemain)) else (iconAreaHeight / float maxRemain) * float r
            for i = 1 to curTick do
                if remainPerTick.[i - 1] <> -1 && remainPerTick.[i] <> -1 then
                    graphLayer.Children.Add(Line(StartPoint=Point(xf (float (i - 1)), y remainPerTick.[i - 1]), EndPoint=Point(xf (float i), y remainPerTick.[i]), Stroke=Brushes.DarkCyan, StrokeThickness=2.))
        // items, triforces, hearts: bucketed by tick, placed in the least-crowded row
        itemLayer.Children.Clear()
        let filled = Array2D.zeroCreate<int> (int lineWidth + 2) numRows
        let buckets = System.Collections.Generic.Dictionary<int, ResizeArray<string * int * TrackerModel.PlayerHas>>()
        for KeyValue(id, ti) in TrackerModel.TimelineItemModel.All do
            if ti.FinishedTotalSeconds <> -1 then
                let tick = int (float ti.FinishedTotalSeconds / (60. * float mpt))
                if tick >= 0 && tick <= numTicks then
                    if not (buckets.ContainsKey tick) then buckets.[tick] <- ResizeArray()
                    buckets.[tick].Add((id, ti.FinishedTotalSeconds, ti.Has))
        for tick in 0 .. numTicks do
            if buckets.ContainsKey tick then
                let xc = float tick * (lineWidth / float numTicks)
                let xminOrig = xc - iconSize / 2.
                let xmin, xmax = max 0 (int xminOrig), min (int (xc + iconSize / 2.)) (int lineWidth)
                let placed = ResizeArray<int * Control * int * string * TrackerModel.PlayerHas>()
                for (id, secs, has) in buckets.[tick] do
                    match timelineIcon id with
                    | Some ic ->
                        let mutable bestRow, bestPenalty = 0, 99999
                        for row = numRows - 1 downto 0 do
                            let mutable penalty = 0
                            for xx = xmin to xmax do penalty <- penalty + filled.[xx, row]
                            if penalty < bestPenalty then
                                bestRow <- row
                                bestPenalty <- penalty
                        for xx = xmin to xmax do filled.[xx, bestRow] <- filled.[xx, bestRow] + 1
                        placed.Add((bestRow, ic, secs, id, has))
                    | None -> ()
                if placed.Count > 0 then
                    let bottomRow = placed |> Seq.map (fun (r, _, _, _, _) -> r) |> Seq.max
                    itemLayer.Children.Add(Line(StartPoint=Point(ox + xc + 0.5, float (bottomRow + 1) * (iconSize + SP) - SP), EndPoint=Point(ox + xc + 0.5, iconAreaHeight + BIG / 2.), Stroke=Brushes.Gray, StrokeThickness=3., IsHitTestVisible=false))
                    for (row, ic, secs, id, has) in placed do
                        let cell = Panel(Width=iconSize, Height=iconSize, Background=Brushes.Transparent)
                        let vb = Viewbox(Width=iconSize, Height=iconSize, Child=ic)
                        if has = TrackerModel.PlayerHas.NO then vb.Opacity <- 0.35
                        cell.Children.Add(vb)
                        if has = TrackerModel.PlayerHas.SKIPPED then
                            cell.Children.Add(TextBlock(Text="X", Foreground=Brushes.Red, FontWeight=FontWeight.Bold, FontSize=iconSize, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center, IsHitTestVisible=false))
                        ToolTip.SetTip(cell, System.TimeSpan.FromSeconds(float secs).ToString("""hh\:mm\:ss""") + "\n" + id)
                        Canvas.SetLeft(cell, ox + System.Math.Ceiling xminOrig)
                        Canvas.SetTop(cell, float row * (iconSize + SP))
                        itemLayer.Children.Add(cell)
    draw ()
    TrackerModel.TimelineItemModel.TimelineChanged.Add(fun _ -> draw ())
    let timer = Avalonia.Threading.DispatcherTimer(Interval=System.TimeSpan.FromSeconds(1.))
    timer.Tick.Add(fun _ -> if not paused.Value then draw ())
    timer.Start()
    let btn = Button(Content="Pause")
    ToolTip.SetTip(btn, "Pause / resume the timer")
    btn.Click.Add(fun _ ->
        paused.Value <- not paused.Value
        (if paused.Value then TrackerModel.LastChangedTime.PauseAll() else TrackerModel.LastChangedTime.ResumeAll())
        btn.Content <- (if paused.Value then "Resume" else "Pause")
        draw ())
    let reset = Button(Content="Reset")
    ToolTip.SetTip(reset, "Restart the timer at 0:00 and clear the timeline and the OW graph")
    reset.Click.Add(fun _ ->
        if paused.Value then
            paused.Value <- false
            TrackerModel.LastChangedTime.ResumeAll()
            btn.Content <- "Pause"
        TrackerModel.theStartTime.SetNow()
        TrackerModel.timelineDataOverworldSpotsRemain.Clear()
        TrackerModel.timelineDataOverworldSpotsRemain.Add(0, TrackerModel.mapStateSummary.OwSpotsRemain)
        for KeyValue(_, v) in TrackerModel.TimelineItemModel.All do v.ResetTotalSeconds()
        TrackerModel.TimelineItemModel.TriggerTimelineChanged())
    let bar = StackPanel(Orientation=Orientation.Horizontal, Spacing=8.)
    bar.Children.Add(timeText)
    bar.Children.Add(btn)
    bar.Children.Add(reset)
    (if TrackerModelOptions.HideTimer.Value then timeText.IsVisible <- false)
    let col = StackPanel(Margin=Thickness(0., 8., 0., 0.))
    // Options... (left) and log (right) above the timeline, like the Windows tracker
    let optsPopup = Popup(Placement=PlacementMode.Top, IsLightDismissEnabled=true)
    let optsButton = Button(Content="Options...", FontSize=12.)
    optsPopup.PlacementTarget <- optsButton
    optsPopup.Child <- Border(Background=SolidColorBrush(Color.FromRgb(20uy, 20uy, 20uy)), BorderBrush=Brushes.Orange, BorderThickness=Thickness(1.), Padding=Thickness(6.), Width=900., Child=makeSettingsGrid ())
    optsButton.Click.Add(fun _ -> optsPopup.IsOpen <- not optsPopup.IsOpen)
    let logButton = Button(Content="log", FontSize=12., HorizontalAlignment=HorizontalAlignment.Right)
    ToolTip.SetTip(logButton, "Draw the OW completion graph on a log scale")
    logButton.Click.Add(fun _ ->
        logScale.Value <- not logScale.Value
        logButton.BorderBrush <- (if logScale.Value then Brushes.Lime else Brushes.Orange)
        logButton.BorderThickness <- Thickness(if logScale.Value then 2. else 1.)
        draw ())
    let top = DockPanel(Width=1000.)
    DockPanel.SetDock(logButton, Dock.Right)
    top.Children.Add(logButton)
    top.Children.Add(optsButton)
    top.Children.Add(optsPopup)
    col.Children.Add(top)
    col.Children.Add(axis)
    (bar :> Control), (col :> Control)

// ---- Spot Summary: what is left to find on the overworld, dimming what has been marked/collected (same layout as the Windows popout) ----
let makeSpotSummary () : Control =
    let OPA = 0.4
    let sp = StackPanel(Spacing=4.)
    let header (t:string) = sp.Children.Add(TextBlock(Text=t, Foreground=Brushes.Orange, FontSize=16., HorizontalAlignment=HorizontalAlignment.Center))
    let rule () = sp.Children.Add(Border(Height=3., Background=Brushes.Gray, Margin=Thickness(3.)))
    let icon (c:Control) (dim:bool) =
        let v = Viewbox(Width=20., Height=36., Child=c)
        let b = Border(Child=v, Margin=Thickness(2.))
        if dim then b.Opacity <- OPA
        b :> Control
    let rowOf (items:Control list) =
        let r = StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center)
        for i in items do r.Children.Add(i)
        sp.Children.Add(r)
    let D = TrackerModel.mapSquareChoiceDomain
    let allBoxes = TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes()
    let itemCollected (id:int) = (allBoxes |> Array.exists (fun b -> b.CellCurrent() = id && b.IsDone())) || TrackerModel.mapCollectedCount.[id] > 0
    let swordCollected (tile:int) =
        match tile with
        | 15 -> itemCollected TrackerModel.ITEMS.WOODSWORD || itemCollected TrackerModel.ITEMS.PROGRESSIVESWORD || TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword.Value()
        | 14 -> itemCollected TrackerModel.ITEMS.WHITESWORD || TrackerModel.startingItemsAndExtras.PlayerHasWhiteSword.Value()
        | 13 -> itemCollected TrackerModel.ITEMS.MAGICALSWORD || TrackerModel.startingItemsAndExtras.PlayerHasMagicalSword.Value() || TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Value()
        | _ -> false
    header "Unique Locations"
    rowOf [ for i in 0..8 -> icon (makeIcon i) (not (D.CanAddUse(i))) ]
    rowOf [ for i in [ 9; 10; 11; 12; 13; 14; 15; 30; 31 ] ->
                (if i = 30 then icon (itemIcon TrackerModel.ITEMS.LETTER) (not (D.CanAddUse(i)) || itemCollected TrackerModel.ITEMS.LETTER)
                 else icon (makeIcon i) (not (D.CanAddUse(i)) || swordCollected i)) ]
    rule ()
    header "Secrets"
    let large, medium, small = if TrackerModel.owInstance.Quest.IsFirstQuestOW then 3, 7, 4 else 1, 7, 6
    let secretRow (kind:int) (count:int) =
        [ for i in 0 .. count - 1 -> icon (makeIcon kind) (i < D.NumUses(kind)) ]
    rowOf (List.concat [ secretRow TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET large; secretRow TrackerModel.MapSquareChoiceDomainHelper.MEDIUM_SECRET medium; secretRow TrackerModel.MapSquareChoiceDomainHelper.SMALL_SECRET small ])
    let unknownUsed = D.NumUses(TrackerModel.MapSquareChoiceDomainHelper.UNKNOWN_SECRET)
    if unknownUsed > 0 then header (sprintf "(+%d unknown-size secrets marked)" unknownUsed)
    rule ()
    header "Non-Unique Locations"
    for multi in [ 28; 29; 32; 33; 34 ] do
        let isPotion = (multi = 34)
        let potionsCollected =
            if isPotion then
                (allBoxes |> Array.sumBy (fun b -> if (b.CellCurrent() = TrackerModel.ITEMS.BLUEPOTION || b.CellCurrent() = TrackerModel.ITEMS.REDPOTION) && b.IsDone() then 1 else 0))
                + TrackerModel.mapCollectedCount.[TrackerModel.ITEMS.BLUEPOTION] + TrackerModel.mapCollectedCount.[TrackerModel.ITEMS.REDPOTION]
            else 0
        let used = if isPotion then max (D.NumUses(multi)) potionsCollected else D.NumUses(multi)
        rowOf [ for i in 0 .. D.MaxUses(multi) - 1 -> icon (if isPotion then itemIcon TrackerModel.ITEMS.BLUEPOTION else makeIcon multi) (i < used) ]
    Border(Background=Brushes.Black, BorderBrush=Brushes.Gray, BorderThickness=Thickness(3.), Padding=Thickness(6.), Child=sp) :> Control

let controlsText =
    "MAP TILES\n" +
    "  Left-click ..... mark the tile done (X); on a marked tile with an icon, check it off / toggle 'used'\n" +
    "  Left-click an X tile ... open the tile menu\n" +
    "  Right-click .... open the tile menu (works on every tile)\n" +
    "  Middle-click ... circle the tile (on a marker icon: check off just that icon)\n" +
    "  Shift+right-click ... change the circle color\n" +
    "  Mouse wheel .... step the tile's mark (or the circle color)\n\n" +
    "ITEM BOXES (dungeons, coast, armos, white sword)\n" +
    "  Left-click ..... have / not have      Shift+left-click ... skipped\n" +
    "  Right-click .... pick the item from the item menu\n\n" +
    "HOTKEYS\n" +
    "  Bind keys in HotKeys.txt (next to the program; same format as the Windows tracker), then hover and press the key.\n" +
    "  Supported: Overworld_*, Item_*, OverworldMarker_Bomb, Blocker_*, HintZone_*\n\n" +
    "OTHER\n" +
    "  Click a dungeon number triangle ... toggle its triforce\n" +
    "  Blockers: left-click next, right-click previous"

let M = TrackerModel.MapSquareChoiceDomainHelper.DARK_X   // the "X" / Don't Care tile state

let questOf (n:int) =
    match n with
    | 0 -> OverworldData.OWQuest.FIRST
    | 1 -> OverworldData.OWQuest.SECOND
    | 2 -> OverworldData.OWQuest.MIXED_FIRST
    | _ -> OverworldData.OWQuest.MIXED_SECOND

let makeMainContent (startN:int) (heartShuffle:bool) (hideNumbers:bool) (levelName:string) (saved:string option) : Control =
    // one-time model setup, exactly like the Windows tracker (1st Quest overworld, normal dungeon numbers)
    TrackerModel.CustomLevelName <- levelName
    TrackerModel.initializeAll(OverworldData.OverworldInstance(questOf startN), (if hideNumbers then TrackerModel.DungeonTrackerInstanceKind.HIDE_DUNGEON_NUMBERS else TrackerModel.DungeonTrackerInstanceKind.DEFAULT))
    if not heartShuffle then
        for i = 0 to 7 do
            TrackerModel.GetDungeon(i).Boxes.[0].Set(TrackerModel.ITEMS.HEARTCONTAINER, TrackerModel.PlayerHas.NO)
    let panelHost = StackPanel(Orientation=Orientation.Horizontal)
    let itemsColumn = StackPanel(Margin=Thickness(10., 0., 0., 0.))
    let statsHost = ContentControl()
    let drawRef : (unit -> unit) ref = ref (fun () -> ())
    let tabsControl, tabsRefresh = makeDungeonTabs ()
    let notesBox = TextBox(AcceptsReturn=true, TextWrapping=TextWrapping.Wrap, Height=150., Width=420., Foreground=Brushes.LimeGreen, Background=Brushes.Black, BorderBrush=Brushes.Gray, Watermark="Notes")
    let blockersHost = ContentControl()
    let timerBar, timelineAxis = makeTimeline ()
    let spotPopup = Popup(Placement=PlacementMode.Bottom, IsLightDismissEnabled=true)
    let spotButton = Button(Content="Spot Summary")
    spotPopup.PlacementTarget <- spotButton
    spotButton.Click.Add(fun _ ->
        spotPopup.Child <- makeSpotSummary ()
        spotPopup.IsOpen <- not spotPopup.IsOpen)
    let controlsPopup = Popup(Placement=PlacementMode.Bottom, IsLightDismissEnabled=true)
    let controlsButton = Button(Content="Controls")
    controlsPopup.PlacementTarget <- controlsButton
    controlsPopup.Child <- Border(Background=Brushes.Black, BorderBrush=Brushes.Orange, BorderThickness=Thickness(2.), Padding=Thickness(10.), Child=TextBlock(Text=controlsText, Foreground=Brushes.Orange, FontSize=13., FontFamily=FontFamily("Consolas")))
    controlsButton.Click.Add(fun _ -> controlsPopup.IsOpen <- not controlsPopup.IsOpen)
    let overlayChecks = WrapPanel(Orientation=Orientation.Horizontal, ItemSpacing=14., LineSpacing=2.)
    let hintPopup = Popup(Placement=PlacementMode.Bottom, IsLightDismissEnabled=true)
    let rebuildPanels () =
        panelHost.Children.Clear()
        itemsColumn.Children.Clear()
        itemsColumn.Children.Add(makeItemArea (fun () -> drawRef.Value ()) statsHost)
        panelHost.Children.Add(makeDungeonPanel (fun () -> drawRef.Value ()))
        panelHost.Children.Add(itemsColumn)
        blockersHost.Content <- makeBlockersPanel (fun () -> drawRef.Value ())
        hintPopup.Child <- Border(Background=SolidColorBrush(Color.FromRgb(20uy, 20uy, 20uy)), BorderBrush=Brushes.Gray, BorderThickness=Thickness(1.), Padding=Thickness(8.), Width=690., Child=makeHintPanel (fun () -> drawRef.Value ()))
    let canvas = Canvas(Width=16.*TILE_W, Height=8.*TILE_H, Background=Brushes.Black)
    let status = TextBlock(Foreground=Brushes.Orange, FontSize=16., Margin=Thickness(0., 6., 0., 2.))
    let infoBox (t:TextBlock) = Border(BorderBrush=Brushes.Gray, BorderThickness=Thickness(1.), Padding=Thickness(6., 2.), Margin=Thickness(0., 0., 0., 4.), Child=t) :> Control
    let spotsText = TextBlock(Foreground=Brushes.Orange, FontSize=16.)
    let gettableText = TextBlock(Foreground=Brushes.Orange, FontSize=16.)
    let heartsText = TextBlock(Foreground=Brushes.Orange, FontSize=16.)
    let hover = TextBlock(Foreground=Brushes.Orange, FontSize=13., TextWrapping=TextWrapping.Wrap, Width=330., Margin=Thickness(0., 2., 0., 2.), Text="Left-click: mark done / toggle used.  Right-click: menu.  Middle-click: circle.  Wheel: change mark.")
    let currentN = ref startN
    let menuTile = ref (0, 0)
    let popup = Popup(PlacementTarget=canvas, Placement=PlacementMode.Pointer, IsLightDismissEnabled=true)

    // marker icons the way the Windows tracker shows them: big and centered on unmarked / Don't-Care tiles, small in the top-right corner on marked tiles; checked-off ones are dimmed
    let visibleDecor (x:int) (y:int) : (int * bool) list =
        let hideKeys = TrackerModel.playerComputedStateSummary.HaveAnyKey
        let hideWoodArrows = TrackerModel.playerComputedStateSummary.ArrowLevel >= 2
        TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons.[x, y])
        |> List.filter (fun (ident, collected) ->
            collected || (not (hideKeys && (ident = TrackerModel.ITEMS.KEY || ident = TrackerModel.ITEMS.OLDKEY)) && not (hideWoodArrows && ident = TrackerModel.ITEMS.WOODARROW)))
    let decorSlot (big:bool) (n:int) (k:int) : float * float * float =
        if big then
            match n with
            | 1 -> 36., TILE_W / 2. - 18., 4.
            | 2 -> 32., float k * 32., 6.
            | _ -> 21., float k * 21., 11.
        else
            match n with
            | 1 -> 24., TILE_W - 27., 0.
            | _ -> 19., TILE_W - 20. * float n + 20. * float k, 0.
    let drawDecor (x:int) (y:int) (left:float) (top:float) (big:bool) : int =
        let vis = visibleDecor x y
        let n = vis.Length
        vis |> List.iteri (fun k (ident, collected) ->
            let size, dx, dy = decorSlot big n k
            let d = Viewbox(Width=size, Height=size, Child=decorOnMap ident, IsHitTestVisible=false)
            if collected then d.Opacity <- 0.3
            Canvas.SetLeft(d, left + dx)
            Canvas.SetTop(d, top + dy)
            canvas.Children.Add(d))
        (if n > 0 then 1 else 0)
    // a green/yellow/magenta ring (middle-click) with an optional label, like the Windows tracker's circles
    let drawCircle (x:int) (y:int) (left:float) (top:float) =
        let v = TrackerModel.overworldMapCircles.[x, y]
        if v <> 0 then
            let color = match v / 100 with | 0 -> Brushes.Cyan | 1 -> Brushes.Yellow | _ -> Brushes.Magenta
            let e = Ellipse(Width=TILE_W - 8., Height=TILE_H - 6., Stroke=color, StrokeThickness=3., IsHitTestVisible=false)
            Canvas.SetLeft(e, left + 4.)
            Canvas.SetTop(e, top + 3.)
            canvas.Children.Add(e)
            let lab = v % 100
            if lab >= 48 then
                let t = TextBlock(Text=string (char lab), Foreground=color, FontWeight=FontWeight.Bold, FontSize=18., IsHitTestVisible=false)
                Canvas.SetLeft(t, left + TILE_W / 2. - 6.)
                Canvas.SetTop(t, top + 10.)
                canvas.Children.Add(t)

    let toggleables = [| TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY; TrackerModel.MapSquareChoiceDomainHelper.SWORD1; TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP
                         TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET; TrackerModel.MapSquareChoiceDomainHelper.MEDIUM_SECRET; TrackerModel.MapSquareChoiceDomainHelper.SMALL_SECRET; TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER |]
    // wheel-scrolling a tile (like Windows): the tile menu opens, the wheel steps through its marks in menu order with a live preview;
    // left-click on the tile keeps the shown mark, leaving the tile puts the original back
    let scrollState : (int * int * int) option ref = ref None    // (x, y, original state)
    let scrollIcons = ref (-1)          // the tile's marker icons before scrolling began (decor entries in the menu are previewed on top of them)
    let scrollCur = ref (-1)            // menu id currently shown (tile marks are plain ids, marker icons are 1000+item)
    let scrollStepRef : (int -> int -> int -> unit) ref = ref (fun _ _ _ -> ())
    let scrollEndRef : (bool -> unit) ref = ref (fun _ -> ())
    let progressRefresh : (unit -> unit) ref = ref (fun () -> ())
    let placeMode = ref 0         // 1 = placing the Start Spot ring, 2 = placing the Custom Waypoint ring
    let mapHot = ref false        // mouse is over the map
    let caveCbHot = ref false     // mouse is over the Cave highlight checkbox
    // yellow outline on the tile under the mouse
    // yellow outline on the tile under the mouse (re-added on top after every redraw)
    let hoverRect = Rectangle(Width=TILE_W, Height=TILE_H, Stroke=Brushes.Yellow, StrokeThickness=3., IsHitTestVisible=false, IsVisible=false)
    // redraws map art, darkened always-empty tiles, and the current marks from the REAL tracker model
    let draw () =
        canvas.Children.Clear()
        let ow = TrackerModel.owInstance
        let img = Image(Width=16.*TILE_W, Height=8.*TILE_H, Stretch=Stretch.Fill,
                        Source=new CroppedBitmap(strip.Value, PixelRect(256*currentN.Value, 0, 256, 88)))
        RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None)
        canvas.Children.Add(img)
        // black grid so neighbouring tiles are easy to tell apart
        for gx in 0..16 do
            canvas.Children.Add(Line(StartPoint=Point(float gx * TILE_W, 0.), EndPoint=Point(float gx * TILE_W, 8. * TILE_H), Stroke=Brushes.Black, StrokeThickness=2., IsHitTestVisible=false))
        for gy in 0..8 do
            canvas.Children.Add(Line(StartPoint=Point(0., float gy * TILE_H), EndPoint=Point(16. * TILE_W, float gy * TILE_H), Stroke=Brushes.Black, StrokeThickness=2., IsHitTestVisible=false))
        let mutable marked = 0
        for x = 0 to 15 do
            for y = 0 to 7 do
                let left = float x * TILE_W
                let top = float y * TILE_H
                if ow.AlwaysEmpty(x, y) then
                    let shade = Rectangle(Width=TILE_W, Height=TILE_H, Fill=SolidColorBrush(Color.FromArgb(150uy, 0uy, 0uy, 0uy)), IsHitTestVisible=false)
                    Canvas.SetLeft(shade, left)
                    Canvas.SetTop(shade, top)
                    canvas.Children.Add(shade)
                    marked <- marked + drawDecor x y left top true
                    drawCircle x y left top
                else
                    let st = TrackerModel.overworldMapMarks.[x, y].Current()
                    if st >= 0 && st <= TrackerModel.MapSquareChoiceDomainHelper.DARK_X + 2 then
                        marked <- marked + 1
                        if st = TrackerModel.MapSquareChoiceDomainHelper.DARK_X then
                            let shade = Rectangle(Width=TILE_W, Height=TILE_H, Fill=SolidColorBrush(Color.FromArgb(190uy, 0uy, 0uy, 0uy)), IsHitTestVisible=false)
                            Canvas.SetLeft(shade, left)
                            Canvas.SetTop(shade, top)
                            canvas.Children.Add(shade)
                        else
                            let icon = Viewbox(Width=20., Height=36., Child=makeIcon st, IsHitTestVisible=false)
                            if toggleables |> Array.contains st && TrackerModel.getOverworldMapExtraData(x, y, st) = st then icon.Opacity <- 0.35   // 'used' (left-click toggles it)
                            Canvas.SetLeft(icon, left + (TILE_W - 20.) / 2.)
                            Canvas.SetTop(icon, top + (TILE_H - 36.) / 2.)
                            canvas.Children.Add(icon)
                    // marker icons go on top of the tile's own mark and its darkening, so they stay bright on finished (Don't Care) tiles
                    marked <- marked + drawDecor x y left top (st = -1 || st = M)
                    drawCircle x y left top
        // Start Spot (green ring) and Custom Waypoint (orange ring), placed from the legend buttons
        let ring (px:int) (py:int) (c:IBrush) =
            let r = Ellipse(Width=TILE_W - 12., Height=TILE_H - 6., Stroke=c, StrokeThickness=4., IsHitTestVisible=false)
            Canvas.SetLeft(r, float px * TILE_W + 6.)
            Canvas.SetTop(r, float py * TILE_H + 3.)
            canvas.Children.Add(r)
        if TrackerModel.startIconX <> -1 then ring TrackerModel.startIconX TrackerModel.startIconY Brushes.LimeGreen
        if TrackerModel.customWaypointX <> -1 then ring TrackerModel.customWaypointX TrackerModel.customWaypointY Brushes.Orange
        // Gettables: pale outlines on unmarked, interesting spots (green = reachable, yellow = might be empty, red = not reachable yet)
        let outline (x:int) (y:int) (c:Color) (th:float) =
            let r = Rectangle(Width=TILE_W - 4., Height=TILE_H - 4., Stroke=SolidColorBrush(c), StrokeThickness=th, IsHitTestVisible=false)
            Canvas.SetLeft(r, float x * TILE_W + 2.)
            Canvas.SetTop(r, float y * TILE_H + 2.)
            canvas.Children.Add(r)
        if TrackerModelOptions.Overworld.Gettables.Value && not TrackerModelOptions.RaceMode.Value then
            for x in 0..15 do
                for y in 0..7 do
                    if TrackerModel.mapStateSummary.OwRouteworthySpots.[x, y] && TrackerModel.overworldMapMarks.[x, y].Current() = -1 then
                        if not (TrackerModel.mapStateSummary.OwGettableLocations.[x, y]) then outline x y (Color.FromArgb(170uy, 255uy, 80uy, 80uy)) 3.
                        elif ow.SometimesEmpty(x, y) then outline x y (Color.FromArgb(170uy, 255uy, 230uy, 80uy)) 3.
                        else outline x y (Color.FromArgb(170uy, 110uy, 255uy, 110uy)) 3.
        // Cave highlight: until you hold a sword and a candle (or have found the wood sword cave) every unmarked open cave is a candidate; after that, the armos while its item box is empty
        if TrackerModelOptions.Overworld.OpenCaves.Value && not TrackerModelOptions.RaceMode.Value then
            let pcs = TrackerModel.playerComputedStateSummary
            let haveBasics = (pcs.SwordLevel > 0 && pcs.CandleLevel > 0) || TrackerModel.mapStateSummary.Sword1Location <> TrackerModel.NOTFOUND
            for x in 0..15 do
                for y in 0..7 do
                    if TrackerModel.overworldMapMarks.[x, y].Current() = -1 then
                        let hit =
                            if haveBasics then TrackerModel.armosBox.CellCurrent() = -1 && ow.HasArmos(x, y)
                            else ow.Nothingable(x, y)
                        if hit then outline x y Colors.Cyan 4.
        canvas.Children.Add(hoverRect)
        status.Text <- sprintf "%d marked tiles" marked
        let gettable = [ for gx in 0..15 do for gy in 0..7 do if TrackerModel.mapStateSummary.OwGettableLocations.[gx, gy] then yield 1 ] |> List.length
        tabsRefresh ()
        // Coords / Zones overlays (like the Windows tracker's checkboxes)
        if TrackerModelOptions.Overworld.Zones.Value && TrackerModel.owInstance.Quest.IsFirstQuestOW then
            let zoneColor (c:char) =
                match c with
                | 'M' -> Color.FromRgb(150uy, 90uy, 60uy) | 'L' -> Color.FromRgb(40uy, 80uy, 200uy) | 'H' -> Color.FromRgb(120uy, 120uy, 120uy)
                | 'R' -> Color.FromRgb(60uy, 160uy, 200uy) | 'G' -> Color.FromRgb(90uy, 90uy, 140uy) | 'D' -> Color.FromRgb(210uy, 180uy, 90uy)
                | 'W' -> Color.FromRgb(110uy, 70uy, 40uy) | 'S' -> Color.FromRgb(200uy, 60uy, 60uy) | 'C' -> Color.FromRgb(40uy, 160uy, 160uy) | 'F' -> Color.FromRgb(40uy, 140uy, 50uy) | _ -> Color.FromRgb(100uy, 100uy, 100uy)
            for zx in 0..15 do
                for zy in 0..7 do
                    let zc = OverworldData.owMapZone.[zy].[zx]
                    let r = Rectangle(Width=TILE_W, Height=TILE_H, Fill=SolidColorBrush(zoneColor zc, 0.45), IsHitTestVisible=false)
                    Canvas.SetLeft(r, float zx * TILE_W)
                    Canvas.SetTop(r, float zy * TILE_H)
                    canvas.Children.Add(r)
            // one label per zone, on the zone cell closest to the zone's centre
            for hz in TrackerModel.HintZone.All do
                let ch = hz.AsDataChar()
                let cells = [ for zy in 0..7 do for zx in 0..15 do if OverworldData.owMapZone.[zy].[zx] = ch then yield (zx, zy) ]
                if not cells.IsEmpty then
                    let cx = (cells |> List.sumBy (fun (a, _) -> float a)) / float cells.Length
                    let cy = (cells |> List.sumBy (fun (_, b) -> float b)) / float cells.Length
                    let (bx, by) = cells |> List.minBy (fun (a, b) -> (float a - cx) * (float a - cx) + (float b - cy) * (float b - cy))
                    let lbl = TextBlock(Text=hz.AsDisplayTwoChars(), Foreground=Brushes.White, FontSize=22., FontWeight=FontWeight.Bold, IsHitTestVisible=false, Width=TILE_W, Height=TILE_H, TextAlignment=TextAlignment.Center, Padding=Thickness(0., 8., 0., 0.))
                    Canvas.SetLeft(lbl, float bx * TILE_W)
                    Canvas.SetTop(lbl, float by * TILE_H)
                    canvas.Children.Add(lbl)
        if TrackerModelOptions.Overworld.Coords.Value then
            for cx in 0..15 do
                for cy in 0..7 do
                    let t = TextBlock(Text=sprintf "%c  %d" (char (int 'A' + cy)) (cx + 1), Foreground=Brushes.White, FontFamily=FontFamily("Consolas"), FontSize=16., FontWeight=FontWeight.Bold, IsHitTestVisible=false, Opacity=0.85)
                    Canvas.SetLeft(t, float cx * TILE_W + 3.)
                    Canvas.SetTop(t, float cy * TILE_H + 8.)
                    canvas.Children.Add(t)
        spotsText.Text <- sprintf "%d OW spots left" TrackerModel.mapStateSummary.OwSpotsRemain
        gettableText.Text <- sprintf "%d gettable" gettable
        TrackerModel.recomputeWhatIsNeeded() |> ignore
        heartsText.Text <- sprintf "Max Hearts: %d" TrackerModel.playerComputedStateSummary.PlayerHearts
        progressRefresh.Value ()

    let choicePopup = Popup(PlacementTarget=canvas, Placement=PlacementMode.Pointer, IsLightDismissEnabled=true)
    // Windows' take-any / take-this pie menus, as a small button list; each option returns true when the tile should be marked 'used'
    let showChoice (x:int, y:int, st:int) (title:string) (opts:(string * (unit -> bool)) list) =
        let sp = StackPanel(Orientation=Orientation.Vertical, Spacing=4.)
        sp.Children.Add(TextBlock(Text=title, Foreground=Brushes.Orange, FontSize=16., Margin=Thickness(0., 0., 0., 2.)))
        for (label, act) in opts do
            let b = Button(Content=label, HorizontalAlignment=HorizontalAlignment.Stretch, MinWidth=150.)
            b.Click.Add(fun _ ->
                choicePopup.IsOpen <- false
                let mark = act ()
                TrackerModel.setOverworldMapExtraData(x, y, st, (if mark then st else 0))
                TrackerModel.recomputeWhatIsNeeded() |> ignore
                TrackerModel.recomputeMapStateSummary()
                draw ())
            sp.Children.Add(b)
        choicePopup.Child <- Border(Background=SolidColorBrush(Color.FromRgb(20uy, 20uy, 20uy)), BorderBrush=Brushes.Orange, BorderThickness=Thickness(2.), Padding=Thickness(8.), Child=sp)
        choicePopup.IsOpen <- false
        choicePopup.IsOpen <- true
    let takeAnyChoice (x:int, y:int) =
        let P = TrackerModel.playerProgressAndTakeAnyHearts
        let freeSlot () = [ 0..3 ] |> List.tryFind (fun i -> P.GetTakeAnyHeart(i) = 0)
        let take (v:int) (extra:unit -> unit) () =
            match freeSlot () with
            | Some i -> P.SetTakeAnyHeart(i, v); extra (); true
            | None -> status.Text <- "All four take-any slots are already used"; false
        showChoice (x, y, TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY) "Take any one you want"
            [ "Candle", take 2 (fun () -> P.PlayerHasBlueCandle.Set(true))
              "Potion", take 2 ignore
              "Heart", take 1 ignore
              "Leave", (fun () -> false) ]
    let takeThisChoice (x:int, y:int) =
        let P = TrackerModel.playerProgressAndTakeAnyHearts
        showChoice (x, y, TrackerModel.MapSquareChoiceDomainHelper.SWORD1) "Take this"
            [ "Wood sword", (fun () -> P.PlayerHasWoodSword.Set(true); true)
              "Candle", (fun () -> P.PlayerHasBlueCandle.Set(true); true)
              "Leave", (fun () -> false) ]

    let tileAt (p:Point) =
        let x = int (p.X / TILE_W)
        let y = int (p.Y / TILE_H)
        if p.X >= 0. && p.Y >= 0. && x <= 15 && y <= 7 then Some(x, y) else None

    canvas.PointerPressed.Add(fun e ->
        let pt = e.GetCurrentPoint(canvas)
        let shift = e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift)
        let handledByScroll =
            match scrollState.Value, tileAt pt.Position with
            | Some(sx, sy, _), Some(x, y) when sx = x && sy = y && pt.Properties.IsLeftButtonPressed -> scrollEndRef.Value true; true
            | Some _, _ -> scrollEndRef.Value false; false
            | _ -> false
        let handledByPlace =
            if placeMode.Value = 0 then false
            else
                let mode = placeMode.Value
                placeMode.Value <- 0
                status.Text <- ""
                let (nx, ny) = if pt.Properties.IsRightButtonPressed then (-1, -1) else (match tileAt pt.Position with | Some(x, y) -> (x, y) | None -> (-2, -2))
                if nx <> -2 then
                    (if mode = 1 then (TrackerModel.startIconX <- nx; TrackerModel.startIconY <- ny) else (TrackerModel.customWaypointX <- nx; TrackerModel.customWaypointY <- ny))
                    draw ()
                true
        match (if handledByScroll || handledByPlace then None else tileAt pt.Position) with
        | Some(x, y) ->
            let cell = TrackerModel.overworldMapMarks.[x, y]
            let always = TrackerModel.owInstance.AlwaysEmpty(x, y)
            if pt.Properties.IsLeftButtonPressed then
                // exactly the Windows tracker's left-click: check off marker icons, else unmarked -> X (done), X -> menu, 'used' toggle for shops/secrets/letter
                if not (visibleDecor x y).IsEmpty then
                    TrackerModel.owSwordIcons.[x, y] <- TrackerModel.decorToggleCollected(TrackerModel.owSwordIcons.[x, y], None)
                elif not always then
                    let st = cell.Current()
                    if st = -1 then
                        cell.Set(M)
                    elif st = M then
                        menuTile.Value <- (x, y)
                        popup.IsOpen <- true
                    elif st = TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY && TrackerModel.getOverworldMapExtraData(x, y, st) <> st then takeAnyChoice (x, y)
                    elif st = TrackerModel.MapSquareChoiceDomainHelper.SWORD1 && TrackerModel.getOverworldMapExtraData(x, y, st) <> st then takeThisChoice (x, y)
                    elif toggleables |> Array.contains st then
                        let ex = TrackerModel.getOverworldMapExtraData(x, y, st)
                        TrackerModel.setOverworldMapExtraData(x, y, st, (if ex = st then 0 else st))
                (TrackerModel.recomputeWhatIsNeeded() |> ignore; TrackerModel.recomputeMapStateSummary())
                draw ()
            elif pt.Properties.IsRightButtonPressed then
                if shift then
                    TrackerModel.nextOverworldMapCircleColor(x, y)
                    draw ()
                else
                    menuTile.Value <- (x, y)   // right-click opens the selector on every tile
                    popup.IsOpen <- true
            elif pt.Properties.IsMiddleButtonPressed then
                // over one of the tile's marker icons: check off just that icon; elsewhere toggle the circle
                let p = pt.Position
                let big = (let st = cell.Current() in st = -1 || st = M)
                let vis = visibleDecor x y
                let hit =
                    vis |> List.indexed |> List.tryFind (fun (k, _) ->
                        let size, dx, dy = decorSlot big vis.Length k
                        let lx, ly = float x * TILE_W + dx, float y * TILE_H + dy
                        p.X >= lx && p.X <= lx + size && p.Y >= ly && p.Y <= ly + size)
                match hit with
                | Some(_, (ident, _)) -> TrackerModel.owSwordIcons.[x, y] <- TrackerModel.decorToggleCollected(TrackerModel.owSwordIcons.[x, y], Some ident)
                | None -> TrackerModel.toggleOverworldMapCircle(x, y)
                draw ()
        | _ -> ())

    // mouse wheel: recolors a circle if the tile has one, otherwise steps the tile's mark (same as the Windows tracker)
    canvas.PointerWheelChanged.Add(fun e ->
        match tileAt (e.GetPosition(canvas)) with
        | Some(x, y) ->
            if TrackerModel.overworldMapCircles.[x, y] <> 0 then
                (if e.Delta.Y < 0. then TrackerModel.nextOverworldMapCircleColor(x, y) else TrackerModel.prevOverworldMapCircleColor(x, y))
            elif not (TrackerModel.owInstance.AlwaysEmpty(x, y)) then
                scrollStepRef.Value x y (if e.Delta.Y < 0. then 1 else -1)
                (TrackerModel.recomputeWhatIsNeeded() |> ignore; TrackerModel.recomputeMapStateSummary())
            e.Handled <- true
            draw ()
        | _ -> ())

    canvas.PointerMoved.Add(fun e ->
        (match scrollState.Value, tileAt (e.GetPosition(canvas)) with
         | Some(sx, sy, _), Some(x, y) when sx = x && sy = y -> ()
         | Some _, _ -> scrollEndRef.Value false
         | _ -> ())
        match tileAt (e.GetPosition(canvas)) with
        | Some(x, y) ->
            let ow = TrackerModel.owInstance
            let notes =
                [ if ow.AlwaysEmpty(x, y) then yield "always empty"
                  if ow.HasArmos(x, y) then yield "armos"
                  if ow.Ladderable(x, y) then yield "needs ladder"
                  if ow.Raftable(x, y) then yield "raftable" ]
            hover.Text <- sprintf "Tile (%d,%d): %s" x y (if notes.IsEmpty then "nothing special" else String.concat ", " notes)
            hoverRect.IsVisible <- true
            Canvas.SetLeft(hoverRect, float x * TILE_W)
            Canvas.SetTop(hoverRect, float y * TILE_H)
            hoverHandler.Value <- Some(fun name ->
                let cell = TrackerModel.overworldMapMarks.[x, y]
                if name = "OverworldMarker_Bomb" || (name.StartsWith "Item_" && name <> "Item_Nothing") then
                    let ident =
                        if name = "OverworldMarker_Bomb" then Some TrackerModel.DECOR_BOMB
                        else [ 0 .. TrackerModel.ITEMS.itemNamesAndCounts.Length - 1 ] |> List.tryFind (fun i -> "Item_" + itemHotKeyName i = name)
                    match ident with
                    | Some id ->
                        TrackerModel.owSwordIcons.[x, y] <- TrackerModel.decorIconsToggle(TrackerModel.owSwordIcons.[x, y], id)
                        if not (TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[x, y]).IsEmpty) && cell.Current() = -1 && not (TrackerModel.owInstance.AlwaysEmpty(x, y)) then cell.AttemptToSet(M) |> ignore
                    | None -> ()
                elif name.StartsWith "Overworld_" && not (TrackerModel.owInstance.AlwaysEmpty(x, y)) then
                    let state =
                        if name = "Overworld_Nothing" then Some -1
                        else [ 0 .. TrackerModel.dummyOverworldTiles.Length - 1 ] |> List.tryFind (fun i -> "Overworld_" + TrackerModel.MapSquareChoiceDomainHelper.AsHotKeyName(i) = name)
                    match state with
                    | Some st ->
                        if st = cell.Current() && toggleables |> Array.contains st then
                            let ex = TrackerModel.getOverworldMapExtraData(x, y, st)
                            TrackerModel.setOverworldMapExtraData(x, y, st, (if ex = st then 0 else st))
                        else
                            cell.AttemptToSet(st) |> ignore
                            if st = -1 then TrackerModel.owSwordIcons.[x, y] <- -1
                    | None -> ()
                (TrackerModel.recomputeWhatIsNeeded() |> ignore; TrackerModel.recomputeMapStateSummary())
                draw ())
        | None -> ())
    canvas.PointerExited.Add(fun _ -> scrollEndRef.Value false; hoverHandler.Value <- None; hoverRect.IsVisible <- false; (if TrackerModelOptions.Overworld.OpenCaves.Value then (mapHot.Value <- false; draw ())) )
    canvas.PointerEntered.Add(fun _ -> focusHost.Value (); (if TrackerModelOptions.Overworld.OpenCaves.Value then (mapHot.Value <- true; draw ())) )

    // right-click grid menu: same 9 columns x 6 rows and order as the Windows tracker's menu.
    // ids: 0..37 = real tile states (from the model), 1000+item = decorative item marker, -1 = Unmarked, -999 = empty cell
    let M = TrackerModel.MapSquareChoiceDomainHelper.DARK_X
    let menuCells : (int * string)[] =
        [| for n in 0..8 do yield n, ""                                                  // row 1: the 9 dungeons
           for n in 9..12 do yield n, ""                                                 // row 2: any roads, then the 5 swords starting with wood
           yield TrackerModel.MapSquareChoiceDomainHelper.SWORD1, ""
           yield TrackerModel.MapSquareChoiceDomainHelper.SWORD2, ""
           yield TrackerModel.MapSquareChoiceDomainHelper.SWORD3, ""
           yield 1000 + TrackerModel.ITEMS.WHITESWORD4, "4th sword (marker icon only)"
           yield 1000 + TrackerModel.ITEMS.PROGRESSIVESWORD, "Progressive sword (marker icon only)"
           yield TrackerModel.MapSquareChoiceDomainHelper.UNKNOWN_SECRET, ""             // row 3: unknown secret, secrets, door repair, money game, armos, take any, hint shop
           for n in 25..29 do yield n, ""
           yield TrackerModel.MapSquareChoiceDomainHelper.ARMOS, ""
           yield TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY, ""
           yield TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP, ""
           // rows 4-6: dungeon items (marker icons only), then personal markers, Don't Care and Unmarked
           for ident in [ TrackerModel.ITEMS.LADDER; TrackerModel.ITEMS.POWERBRACELET; TrackerModel.ITEMS.RECORDER; TrackerModel.ITEMS.RAFT; TrackerModel.ITEMS.BOW
                          TrackerModel.ITEMS.BLUERING; TrackerModel.ITEMS.REDRING; TrackerModel.ITEMS.WOODARROW; TrackerModel.ITEMS.SILVERARROW; TrackerModel.ITEMS.WAND
                          TrackerModel.ITEMS.KEY; TrackerModel.ITEMS.BLUECANDLE; TrackerModel.ITEMS.REDCANDLE; TrackerModel.ITEMS.BOOMERANG; TrackerModel.ITEMS.MAGICBOOMERANG
                          TrackerModel.ITEMS.BOOKSHIELD; TrackerModel.ITEMS.SHIELD; TrackerModel.ITEMS.MEAT; TrackerModel.DECOR_BOMB; TrackerModel.ITEMS.OLDKEY
                          TrackerModel.ITEMS.BLUEPOTION; TrackerModel.ITEMS.LETTER; TrackerModel.ITEMS.HEARTCONTAINER; TrackerModel.ITEMS.BOMBUPGRADE ] do
               yield 1000 + ident, (if ident = TrackerModel.DECOR_BOMB then "Bomb" else TrackerModel.ITEMS.AsDisplayDescription(ident)) + " (marker icon only)"
           |]
    // Don't Care and Unmarked always end the grid: holes pad the gap so they are the last two cells of the last row
    let menuCells =
        let paddedCount = ((menuCells.Length + 3 + 8) / 9) * 9
        Array.concat [ menuCells; Array.create (paddedCount - menuCells.Length - 3) (-999, ""); [| 2000, "Clear this tile's marker icons (keeps the tile's mark)"; M, ""; -1, "Unmarked" |] ]
    let floorColor = SolidColorBrush(Color.FromRgb(204uy, 176uy, 136uy))
    let floorColorDark = SolidColorBrush(Color.FromRgb(112uy, 97uy, 75uy))
    let cellContent (id:int) : Control =
        if id = 2000 then
            let c = Canvas(Width=15., Height=27.)
            for (x1, y1, x2, y2) in [ (2., 7., 13., 20.); (13., 7., 2., 20.) ] do
                c.Children.Add(Line(StartPoint=Point(x1, y1), EndPoint=Point(x2, y2), Stroke=Brushes.Red, StrokeThickness=3.))
            c :> Control
        elif id = -1 then Border(Width=15., Height=27., Background=floorColor) :> Control
        elif id = M then Border(Width=15., Height=27., Background=floorColorDark) :> Control
        elif id >= 1000 then itemIcon (id - 1000)
        else makeIcon id
    let menuButtons = System.Collections.Generic.Dictionary<int, Button>()
    let menuGrid = StackPanel(Orientation=Orientation.Vertical, Spacing=2.)
    for r = 0 to menuCells.Length / 9 - 1 do
        let rp = StackPanel(Orientation=Orientation.Horizontal, Spacing=2.)
        for c = 0 to 8 do
            let (id, tip) = menuCells.[r * 9 + c]
            if id = -999 then
                rp.Children.Add(Border(Width=30., Height=36.))
            else
                let b = Button(Content=cellContent id, Width=30., Height=36., Padding=Thickness(0.),
                               HorizontalContentAlignment=HorizontalAlignment.Center, VerticalContentAlignment=VerticalAlignment.Center)
                let text =
                    if tip <> "" then tip
                    elif id >= 0 && id < TrackerModel.dummyOverworldTiles.Length then (let (_, _, spoken, _) = TrackerModel.dummyOverworldTiles.[id] in spoken)
                    else ""
                ToolTip.SetTip(b, text)
                (if id = -1 || (id >= 0 && id < 2000) then menuButtons.[id] <- b)
                b.PointerEntered.Add(fun _ -> if scrollState.Value.IsNone then (b.BorderBrush <- Brushes.Yellow; b.BorderThickness <- Thickness(3.)))
                b.PointerExited.Add(fun _ -> if scrollState.Value.IsNone then (b.ClearValue(Button.BorderBrushProperty); b.ClearValue(Button.BorderThicknessProperty)))
                b.Click.Add(fun _ ->
                    let (x, y) = menuTile.Value
                    if id = 2000 then
                        TrackerModel.owSwordIcons.[x, y] <- -1   // red X: remove every marker icon; a tile that was auto-finished goes back to unmarked
                        if TrackerModel.overworldMapMarks.[x, y].Current() = M then TrackerModel.overworldMapMarks.[x, y].AttemptToSet(-1) |> ignore
                    elif id >= 1000 then
                        // decorative marker icon: click toggles it on this tile (the tile's real mark is unchanged)
                        let ident = id - 1000
                        TrackerModel.owSwordIcons.[x, y] <- TrackerModel.decorIconsToggle(TrackerModel.owSwordIcons.[x, y], ident)
                        // an unmarked tile that gets a marker icon counts as finished (Don't Care); the icons stay visible
                        if not (TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[x, y]).IsEmpty) && TrackerModel.overworldMapMarks.[x, y].Current() = -1 then
                            TrackerModel.overworldMapMarks.[x, y].AttemptToSet(M) |> ignore
                    else
                        TrackerModel.overworldMapMarks.[x, y].AttemptToSet(id) |> ignore
                        if id = -1 then TrackerModel.owSwordIcons.[x, y] <- -1   // 'Unmarked' also clears any marker icon
                    (TrackerModel.recomputeWhatIsNeeded() |> ignore; TrackerModel.recomputeMapStateSummary())
                    popup.IsOpen <- false
                    draw ())
                rp.Children.Add(b)
        menuGrid.Children.Add(rp)
    popup.Child <- Border(Child=menuGrid, Background=Brushes.Black, BorderBrush=Brushes.Gray, BorderThickness=Thickness(3.), Padding=Thickness(4.))
    let highlightMenuCell (cur:int) =
        for KeyValue(id, b) in menuButtons do
            if id = cur then
                b.BorderBrush <- Brushes.Yellow
                b.BorderThickness <- Thickness(3.)
            else
                b.ClearValue(Button.BorderBrushProperty)
                b.ClearValue(Button.BorderThicknessProperty)
    scrollEndRef.Value <- (fun keep ->
        match scrollState.Value with
        | Some(x, y, orig) ->
            scrollState.Value <- None
            let cell = TrackerModel.overworldMapMarks.[x, y]
            if keep then
                // keeping a marker-icon entry: the tile counts as finished (Don't Care) like when picking it from the menu
                if scrollCur.Value >= 1000 && not (TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[x, y]).IsEmpty) && cell.Current() = -1 then cell.AttemptToSet(M) |> ignore
            else
                cell.AttemptToSet(orig) |> ignore
                TrackerModel.owSwordIcons.[x, y] <- scrollIcons.Value
            popup.IsOpen <- false
            popup.IsLightDismissEnabled <- true
            popup.HorizontalOffset <- 0.
            popup.VerticalOffset <- 0.
            highlightMenuCell -12345
            (TrackerModel.recomputeWhatIsNeeded() |> ignore; TrackerModel.recomputeMapStateSummary())
            draw ()
        | None -> ())
    scrollStepRef.Value <- (fun x y delta ->
        let cell = TrackerModel.overworldMapMarks.[x, y]
        (match scrollState.Value with
         | Some(sx, sy, _) when sx = x && sy = y -> ()
         | Some _ -> scrollEndRef.Value false
         | None -> ())
        if scrollState.Value.IsNone then
            scrollState.Value <- Some(x, y, cell.Current())
            scrollIcons.Value <- TrackerModel.owSwordIcons.[x, y]
            scrollCur.Value <- cell.Current()
            menuTile.Value <- (x, y)
            popup.IsLightDismissEnabled <- false
            popup.HorizontalOffset <- 28.
            popup.VerticalOffset <- 28.
            popup.IsOpen <- true
        let orig = (match scrollState.Value with Some(_, _, o) -> o | None -> cell.Current())
        // everything the menu offers in its own order: real tile marks (that are still available), then the marker-icon entries
        let order =
            menuCells |> Array.map fst |> Array.filter (fun id ->
                (id = -1 || id = M || (id >= 0 && id < 1000) || (id >= 1000 && id < 2000))
                && (id >= 1000 || id = orig || id = scrollCur.Value || id = -1 || id = M || TrackerModel.mapSquareChoiceDomain.CanAddUse(id)))
        if order.Length > 0 then
            let pos = match Array.tryFindIndex (fun id -> id = scrollCur.Value) order with Some i -> i | None -> (if delta > 0 then -1 else 0)
            let next = order.[((pos + delta) % order.Length + order.Length) % order.Length]
            scrollCur.Value <- next
            if next >= 1000 then
                // marker icon preview: the tile keeps its mark, the icon is shown on top
                cell.AttemptToSet(orig) |> ignore
                TrackerModel.owSwordIcons.[x, y] <- TrackerModel.decorIconsToggle(scrollIcons.Value, next - 1000)
            else
                TrackerModel.owSwordIcons.[x, y] <- scrollIcons.Value
                cell.AttemptToSet(next) |> ignore
            highlightMenuCell next)

    let quests = [ "1st Quest", 0, OverworldData.OWQuest.FIRST
                   "2nd Quest", 1, OverworldData.OWQuest.SECOND
                   "Mixed 1Q", 2, OverworldData.OWQuest.MIXED_FIRST
                   "Mixed 2Q", 3, OverworldData.OWQuest.MIXED_SECOND ]
    let buttons = StackPanel(Orientation=Orientation.Horizontal, Spacing=8.)
    let hintsButton = Button(Content="Hint Decoder")
    hintPopup.PlacementTarget <- hintsButton
    hintsButton.Click.Add(fun _ -> hintPopup.IsOpen <- not hintPopup.IsOpen)
    for (name, n, q) in quests do
        let b = Button(Content=name)
        b.Click.Add(fun _ ->
            // same model call the Windows tracker uses: keeps compatible marks, clears the rest
            TrackerModel.reinitializeOverworld(OverworldData.OverworldInstance(q))
            currentN.Value <- n
            draw ())
        buttons.Children.Add(b)

    drawRef.Value <- draw
    rebuildPanels ()
    // Save / Load (preview format: quest number, 128 tile states, sword icons, then items / triforces / dungeon boxes)
    let saveFile = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "zt-preview-save.txt")
    let makeSaveText () =
        let states = [ for y in 0..7 do for x in 0..15 do yield string (TrackerModel.overworldMapMarks.[x, y].Current()) ]
        let icons = [ for y in 0..7 do for x in 0..15 do yield string TrackerModel.owSwordIcons.[x, y] ]
        string currentN.Value + "\n" + String.concat "," states + "\n" + String.concat "," icons + "\n" + saveExtras ()
        + "\n" + System.Uri.EscapeDataString(notesBox.Text |> fun t -> if isNull t then "" else t)
        + "\n" + String.concat ";" [ for di in 0..7 do for j in 0 .. TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON - 1 -> TrackerModel.DungeonBlockersContainer.GetDungeonBlocker(di, j).AsHotKeyName() ]
    let applySaveText (text:string) =
        let lines = text.Replace("\r", "").Split('\n')
        let n = int lines.[0]
        let (_, _, q) = quests.[n]
        TrackerModel.reinitializeOverworld(OverworldData.OverworldInstance(q))
        currentN.Value <- n
        let vals = lines.[1].Split(',') |> Array.map int
        for y = 0 to 7 do
            for x = 0 to 15 do
                let v = vals.[y * 16 + x]
                if not (TrackerModel.owInstance.AlwaysEmpty(x, y)) && v <> -1 then
                    TrackerModel.overworldMapMarks.[x, y].AttemptToSet(v) |> ignore
        if lines.Length > 2 then
            let icons = lines.[2].Split(',') |> Array.map int
            for y = 0 to 7 do
                for x = 0 to 15 do
                    TrackerModel.owSwordIcons.[x, y] <- icons.[y * 16 + x]
        (TrackerModel.recomputeWhatIsNeeded() |> ignore; TrackerModel.recomputeMapStateSummary())
        if lines.Length > 3 then loadExtras lines.[3]
        if lines.Length > 4 then notesBox.Text <- System.Uri.UnescapeDataString(lines.[4])
        if lines.Length > 5 && lines.[5].Length > 0 then
            lines.[5].Split(';') |> Array.iteri (fun k name ->
                if k < 24 then TrackerModel.DungeonBlockersContainer.SetDungeonBlocker(k / 3, k % 3, TrackerModel.DungeonBlocker.FromHotKeyName name))
        rebuildPanels ()
        draw ()
    saved |> Option.iter (fun t -> try applySaveText t with _ -> ())
    let saveButton = Button(Content="Save")
    saveButton.Click.Add(fun _ ->
        try
            System.IO.File.WriteAllText(saveFile, makeSaveText ())
            status.Text <- "Saved to " + saveFile
        with e -> status.Text <- "File save not available here (use Copy save): " + e.Message)
    let loadButton = Button(Content="Load")
    loadButton.Click.Add(fun _ ->
        try
            if System.IO.File.Exists(saveFile) then
                applySaveText (System.IO.File.ReadAllText(saveFile))
                status.Text <- "Loaded " + saveFile
            else
                status.Text <- "No save file yet"
        with e -> status.Text <- "Load failed: " + e.Message)
    // clipboard save: works everywhere, including the browser (paste it into a text file to keep it)
    let copyButton = Button(Content="Copy save")
    copyButton.Click.Add(fun _ ->
        let top = TopLevel.GetTopLevel(canvas)
        if not (isNull top) && not (isNull top.Clipboard) then
            top.Clipboard.SetTextAsync(makeSaveText ()) |> ignore
            status.Text <- "Save copied to the clipboard"
        else status.Text <- "Clipboard not available")
    let pasteButton = Button(Content="Paste load")
    pasteButton.Click.Add(fun _ ->
        let top = TopLevel.GetTopLevel(canvas)
        if not (isNull top) && not (isNull top.Clipboard) then
            let t = top.Clipboard.GetTextAsync()
            t.ContinueWith(fun (task:System.Threading.Tasks.Task<string>) ->
                Avalonia.Threading.Dispatcher.UIThread.Post(fun () ->
                    try
                        if System.String.IsNullOrWhiteSpace task.Result then status.Text <- "Clipboard has no text"
                        else
                            applySaveText task.Result
                            status.Text <- "Loaded from the clipboard"
                    with e -> status.Text <- "Clipboard text is not a valid save: " + e.Message)) |> ignore
        else status.Text <- "Clipboard not available")

    // Windows-style layout: top strip (dungeons | items + progress | buttons and info), map at 4x, quest buttons underneath
    // one "1Q2Q" button like the Windows tracker; it opens the four quest choices
    let questMenuPanel = StackPanel(Spacing=4.)
    for c in List.ofSeq buttons.Children do
        buttons.Children.Remove(c) |> ignore
        questMenuPanel.Children.Add(c)
    let questButton = Button(Content="1Q2Q")
    let questPopup = Popup(Placement=PlacementMode.Bottom, PlacementTarget=questButton, IsLightDismissEnabled=true,
                           Child=Border(Background=SolidColorBrush(Color.FromRgb(20uy, 20uy, 20uy)), BorderBrush=Brushes.Orange, BorderThickness=Thickness(1.), Padding=Thickness(6.), Child=questMenuPanel))
    for c in questMenuPanel.Children do
        (c :?> Button).Click.Add(fun _ -> questPopup.IsOpen <- false)
    questButton.Click.Add(fun _ -> questPopup.IsOpen <- not questPopup.IsOpen)
    // right-hand column (like the Windows tracker's button stack): Menu / 1Q2Q, timer, Zones + Coords, Hint Decoder; everything else lives in the Menu
    let infoColumn = StackPanel(Spacing=8., Margin=Thickness(14., 0., 0., 0.), Width=236.)
    let menuButton = Button(Content="Menu \u25BE")
    let menuList = StackPanel(Spacing=4.)
    let menuPopup = Popup(Placement=PlacementMode.Bottom, PlacementTarget=menuButton, IsLightDismissEnabled=true,
                          Child=Border(Background=SolidColorBrush(Color.FromRgb(20uy, 20uy, 20uy)), BorderBrush=Brushes.Orange, BorderThickness=Thickness(1.), Padding=Thickness(6.), Child=menuList))
    for b in [ saveButton; loadButton; copyButton; pasteButton; spotButton; controlsButton ] do
        b.HorizontalAlignment <- HorizontalAlignment.Stretch
        b.HorizontalContentAlignment <- HorizontalAlignment.Left
        b.Margin <- Thickness(0.)
        b.Click.Add(fun _ -> menuPopup.IsOpen <- false)
        menuList.Children.Add(b)
    spotPopup.PlacementTarget <- menuButton
    controlsPopup.PlacementTarget <- menuButton
    menuButton.Click.Add(fun _ -> menuPopup.IsOpen <- not menuPopup.IsOpen)
    let rowA = StackPanel(Orientation=Orientation.Horizontal, Spacing=8.)
    rowA.Children.Add(menuButton)
    rowA.Children.Add(questButton)
    infoColumn.Children.Add(rowA)
    infoColumn.Children.Add(timerBar)
    for pp in [ menuPopup; questPopup; hintPopup; spotPopup; controlsPopup; popup; choicePopup ] do infoColumn.Children.Add(pp)
    let zonesCb = CheckBox(Content="Zones", IsChecked=System.Nullable<bool>(TrackerModelOptions.Overworld.Zones.Value), Foreground=Brushes.Orange)
    zonesCb.IsCheckedChanged.Add(fun _ -> TrackerModelOptions.Overworld.Zones.Value <- zonesCb.IsChecked.GetValueOrDefault(false); draw ())
    let coordsCb = CheckBox(Content="Coords", IsChecked=System.Nullable<bool>(TrackerModelOptions.Overworld.Coords.Value), Foreground=Brushes.Orange)
    coordsCb.IsCheckedChanged.Add(fun _ -> TrackerModelOptions.Overworld.Coords.Value <- coordsCb.IsChecked.GetValueOrDefault(false); draw ())
    let gettablesCb = CheckBox(Content=gettableText, IsChecked=System.Nullable<bool>(TrackerModelOptions.Overworld.Gettables.Value), Foreground=Brushes.Orange)
    ToolTip.SetTip(gettablesCb, "Outline unmarked interesting spots: green = reachable now, yellow = reachable but might be empty, red = not reachable yet")
    gettablesCb.IsCheckedChanged.Add(fun _ -> TrackerModelOptions.Overworld.Gettables.Value <- gettablesCb.IsChecked.GetValueOrDefault(false); (try TrackerModelOptions.writeSettings() with _ -> ()); draw ())
    TrackerModelOptions.Overworld.OpenCaves.Value <- true   // like Windows: always start with cave highlight on
    let caveCb = CheckBox(Content="Cave highlight", IsChecked=System.Nullable<bool>(true), Foreground=Brushes.Orange)
    ToolTip.SetTip(caveCb, "Highlight unmarked open caves (all the time while checked)\nuntil you mark the wood sword cave or hold both a sword and a candle;\nafter that, highlights just the armos spots until you mark the armos item.")
    caveCb.IsCheckedChanged.Add(fun _ -> TrackerModelOptions.Overworld.OpenCaves.Value <- caveCb.IsChecked.GetValueOrDefault(false); (try TrackerModelOptions.writeSettings() with _ -> ()); draw ())
    caveCb.PointerEntered.Add(fun _ -> caveCbHot.Value <- true; draw ())
    caveCb.PointerExited.Add(fun _ -> caveCbHot.Value <- false; draw ())
    let statsCol = StackPanel(Spacing=4., VerticalAlignment=VerticalAlignment.Top)
    spotsText.FontSize <- 20.
    spotsText.Margin <- Thickness(4., 0., 0., 4.)
    heartsText.FontSize <- 22.
    statsCol.Children.Add(spotsText)
    statsCol.Children.Add(gettablesCb)
    statsCol.Children.Add(caveCb)
    statsCol.Children.Add(Border(BorderBrush=Brushes.Orange, BorderThickness=Thickness(2.), Padding=Thickness(10., 4.), Margin=Thickness(0., 10., 0., 0.), Child=heartsText))
    statsHost.Content <- statsCol
    overlayChecks.Children.Add(zonesCb)
    overlayChecks.Children.Add(coordsCb)
    infoColumn.Children.Add(overlayChecks)
    hintsButton.HorizontalAlignment <- HorizontalAlignment.Left
    infoColumn.Children.Add(hintsButton)
    let topStrip = StackPanel(Orientation=Orientation.Horizontal, Margin=Thickness(0., 0., 0., 6.))
    topStrip.Children.Add(panelHost)
    topStrip.Children.Add(infoColumn)
    let root = StackPanel(Margin=Thickness(8.))
    root.Children.Add(topStrip)
    canvas.HorizontalAlignment <- HorizontalAlignment.Left
    root.Children.Add(canvas)
    let legendRowControl = makeLegend ()
    root.Children.Add(legendRowControl)
    // legend extras (like Windows): Custom Waypoint / Start Spot placement buttons and the ZORA Hints direction notes
    (match legendRowControl with
     | :? StackPanel as lr ->
        let ringButton (c:IBrush) (label:string) (mode:int) (tip:string) =
            let cv = Canvas(Width=62., Height=26.)
            let el = Ellipse(Width=26., Height=22., Stroke=c, StrokeThickness=3.)
            Canvas.SetLeft(el, 1.)
            Canvas.SetTop(el, 2.)
            cv.Children.Add(el)
            if label <> "" then
                let t = TextBlock(Text=label, Foreground=Brushes.Orange, FontSize=10., IsHitTestVisible=false)
                Canvas.SetLeft(t, 32.)
                Canvas.SetTop(t, 0.)
                cv.Children.Add(t)
            let b = Button(Content=cv, Padding=Thickness(3., 1.))
            ToolTip.SetTip(b, tip + "\nLeft-click a map tile to place it there, right-click to remove it, click elsewhere to cancel.")
            b.Click.Add(fun _ ->
                placeMode.Value <- mode
                status.Text <- tip + ": left-click a map tile to place it, right-click removes it")
            b
        lr.Children.Add(ringButton Brushes.Orange "" 2 "Custom Waypoint")
        lr.Children.Add(ringButton Brushes.LimeGreen "Start\nSpot" 1 "Mark where you first spawned (Start Spot)")
        // ZORA Hints: Dead Woods / Lost Hills direction sequences
        let hintsPanel = StackPanel()
        hintsPanel.Children.Add(TextBlock(Text="ZORA Hints", Foreground=Brushes.Orange, FontSize=10., HorizontalAlignment=HorizontalAlignment.Center))
        let hb = StackPanel(Orientation=Orientation.Horizontal, Spacing=3.)
        let makeHintButton (name:string) (prefix:string) =
            let cur = ref ""
            let label = TextBlock(Text=name, Foreground=Brushes.Orange)   // default size, so these match the 'v2.2' button
            let btn = Button(Content=label, BorderBrush=Brushes.Orange, BorderThickness=Thickness(1.), Background=SolidColorBrush(Color.FromRgb(30uy, 30uy, 30uy)), Padding=Thickness(8., 3.))
            let pop = Popup(Placement=PlacementMode.Bottom, PlacementTarget=btn, IsLightDismissEnabled=true)
            let display = TextBlock(Foreground=Brushes.Orange, FontSize=22., HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(4.))
            let refresh () =
                display.Text <- (if cur.Value = "" then "-" else System.String.Join(" ", cur.Value.ToCharArray() |> Array.map string))
                label.Text <- (if cur.Value = "" then name else prefix + ": " + cur.Value)
            refresh ()
            let sp = StackPanel(Spacing=4., Margin=Thickness(6.))
            sp.Children.Add(TextBlock(Text=name + " hint", Foreground=Brushes.Orange, FontSize=16.))
            sp.Children.Add(display)
            let dirs = StackPanel(Orientation=Orientation.Horizontal, Spacing=6., HorizontalAlignment=HorizontalAlignment.Center)
            for c in [ "U"; "D"; "L"; "R" ] do
                let d = Button(Content=c, Width=36., Height=32., FontSize=16., HorizontalContentAlignment=HorizontalAlignment.Center)
                d.Click.Add(fun _ -> (if cur.Value.Length < 4 then cur.Value <- cur.Value + c); refresh (); (if cur.Value.Length >= 4 then pop.IsOpen <- false))
                dirs.Children.Add(d)
            sp.Children.Add(dirs)
            let acts = StackPanel(Orientation=Orientation.Horizontal, Spacing=6., HorizontalAlignment=HorizontalAlignment.Center)
            let back = Button(Content="Back")
            back.Click.Add(fun _ -> (if cur.Value.Length > 0 then cur.Value <- cur.Value.Substring(0, cur.Value.Length - 1)); refresh ())
            let clear = Button(Content="Clear")
            clear.Click.Add(fun _ -> cur.Value <- ""; refresh ())
            let dn = Button(Content="Done")
            dn.Click.Add(fun _ -> pop.IsOpen <- false)
            for b in [ back; clear; dn ] do acts.Children.Add(b)
            sp.Children.Add(acts)
            pop.Child <- Border(Background=Brushes.Black, BorderBrush=Brushes.Gray, BorderThickness=Thickness(3.), Child=sp)
            btn.Click.Add(fun _ -> pop.IsOpen <- not pop.IsOpen)
            let holder = StackPanel(Orientation=Orientation.Horizontal)
            holder.Children.Add(btn)
            holder.Children.Add(pop)
            holder :> Control
        hb.Children.Add(makeHintButton "Dead Woods" "DW")
        hb.Children.Add(makeHintButton "Lost Hills" "LH")
        hintsPanel.Children.Add(hb)
        lr.Children.Add(hintsPanel)
     | _ -> ())
    let progressRow, progressRefreshFn = makeItemProgress ()
    progressRefresh.Value <- progressRefreshFn
    (match progressRow with
     | :? StackPanel as pr ->
        let chart = Canvas(Width=140., Height=76., Margin=Thickness(60., 0., 0., 0.))
        chart.Children.Add(Avalonia.Controls.Shapes.Polygon(Points=Avalonia.Collections.AvaloniaList<Point>([ Point(70., 2.); Point(138., 74.); Point(2., 74.) ]), Stroke=SolidColorBrush(Color.FromRgb(90uy, 70uy, 160uy)), StrokeThickness=2.))
        for (txt, x, y) in [ "1", 62., 6.; "2", 76., 6.; "5", 56., 28.; "7", 80., 28.; "3", 28., 52.; "6", 52., 52.; "8", 88., 52.; "4", 112., 52. ] do
            let t = TextBlock(Text=txt, Foreground=Brushes.White, FontSize=12., FontWeight=FontWeight.Bold, IsHitTestVisible=false)
            Canvas.SetLeft(t, x)
            Canvas.SetTop(t, y)
            chart.Children.Add(t)
        ToolTip.SetTip(chart, "Triforce reference chart")
        pr.Children.Add(chart)
        // logo and ZORA Discord link (like the Windows tracker's bottom-right corner)
        let discordUrl = "https://discord.com/channels/723242830331510844/1431412013170688152"
        let logoPanel = StackPanel(Margin=Thickness(40., 0., 0., 0.), HorizontalAlignment=HorizontalAlignment.Center)
        (try logoPanel.Children.Add(Image(Source=loadBmp "CustomLogo.png", Width=44., Height=44.)) with _ -> ())
        let link = TextBlock(Text="ZORA Discord", FontSize=10., Foreground=Brushes.Orange, TextDecorations=TextDecorations.Underline, HorizontalAlignment=HorizontalAlignment.Center, Cursor=Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand), Background=Brushes.Transparent)
        ToolTip.SetTip(link, discordUrl)
        link.PointerEntered.Add(fun _ -> link.Foreground <- Brushes.Yellow)
        link.PointerExited.Add(fun _ -> link.Foreground <- Brushes.Orange)
        link.PointerPressed.Add(fun _ ->
            try
                match TopLevel.GetTopLevel(link) with
                | null -> ()
                | tl -> tl.Launcher.LaunchUriAsync(System.Uri(discordUrl)) |> ignore
            with _ -> ())
        logoPanel.Children.Add(link)
        pr.Children.Add(logoPanel)
     | _ -> ())
    root.Children.Add(progressRow)
    // hover help + status live in one line under the legend (instead of crowding the top strip)
    hover.Width <- 800.
    hover.Margin <- Thickness(0.)
    status.Margin <- Thickness(0.)
    status.FontSize <- 13.
    let footer = StackPanel(Orientation=Orientation.Horizontal, Spacing=24., Margin=Thickness(0., 4., 0., 4.), HorizontalAlignment=HorizontalAlignment.Left)
    footer.Children.Add(hover)
    footer.Children.Add(status)
    root.Children.Add(footer)
    let lowerRow = StackPanel(Orientation=Orientation.Horizontal, Spacing=12.)
    lowerRow.Children.Add(Border(Width=580., Child=tabsControl))
    let rightLower = StackPanel(Spacing=6., Margin=Thickness(0., 6., 0., 0.))
    rightLower.Children.Add(blockersHost)
    rightLower.Children.Add(TextBlock(Text="Notes", Foreground=Brushes.LimeGreen, FontSize=18.))
    rightLower.Children.Add(notesBox)
    lowerRow.Children.Add(rightLower)
    root.Children.Add(lowerRow)
    root.Children.Add(timelineAxis)
    draw ()
    ScrollViewer(Content=root, HorizontalScrollBarVisibility=ScrollBarVisibility.Auto, VerticalScrollBarVisibility=ScrollBarVisibility.Auto) :> Control

// ---- startup window (like the Windows tracker's): startup options, the four start buttons, and the settings ----
let makeStartupView (onStart: int -> bool -> bool -> string -> string option -> unit) : Control =
    (try TrackerModelOptions.readSettings() with _ -> ())   // restore the settings from last time (not available in a browser)
    let orange = Brushes.Orange
    let hand = Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
    let heading (t:string) (size:float) = TextBlock(Text=t, Foreground=orange, FontSize=size, HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(0., 8., 0., 8.))
    let check (label:string) (initial:bool) (onChange: bool -> unit) =
        let cb = CheckBox(Content=label, IsChecked=System.Nullable<bool>(initial), Foreground=orange, FontSize=15.)
        cb.IsCheckedChanged.Add(fun _ -> onChange (cb.IsChecked.GetValueOrDefault(false)))
        cb
    let opt (label:string) (b:TrackerModelOptions.Bool) = check label b.Value (fun v -> b.Value <- v)
    let root = StackPanel(Width=980., Margin=Thickness(0., 10., 0., 20.), HorizontalAlignment=HorizontalAlignment.Center)
    let narrow = StackPanel(Width=640., HorizontalAlignment=HorizontalAlignment.Center)
    let verPopup = Popup(Placement=PlacementMode.Bottom, IsLightDismissEnabled=true)
    let verButton = Button(Content="v2.2-b3", FontSize=22., HorizontalAlignment=HorizontalAlignment.Right)
    verPopup.PlacementTarget <- verButton
    verPopup.Child <- Border(Background=Brushes.Black, BorderBrush=Brushes.Orange, BorderThickness=Thickness(2.), Padding=Thickness(10.), Child=TextBlock(Text=OverworldData.AboutBody, Foreground=Brushes.Orange, FontSize=14.))
    verButton.Click.Add(fun _ -> verPopup.IsOpen <- not verPopup.IsOpen)
    root.Children.Add(verButton)
    root.Children.Add(verPopup)
    root.Children.Add(heading "Startup Options:" 20.)
    let full = check "Full Shuffle" true ignore
    let hide = check "Hide Dungeon Numbers" false ignore
    let custom = check "Custom LEVEL Name" false ignore
    let nameBox = TextBox(MaxLength=6, Width=130., HorizontalAlignment=HorizontalAlignment.Left, Foreground=orange, Margin=Thickness(28., 2., 0., 8.))
    for c in [ full; hide; custom ] do narrow.Children.Add(c)
    narrow.Children.Add(nameBox)
    // small picture of the dungeon header (three triforce slots over a grid of item boxes), like the Windows startup screen
    let pic = Canvas(Width=130., Height=110.)
    pic.Children.Add(Line(StartPoint=Point(2., 2.), EndPoint=Point(2., 108.), Stroke=Brushes.Gray, StrokeThickness=5.))
    pic.Children.Add(Line(StartPoint=Point(0., 2.), EndPoint=Point(128., 2.), Stroke=Brushes.Gray, StrokeThickness=5.))
    for i in 0..2 do
        let tri = Avalonia.Controls.Shapes.Polygon(Points=Avalonia.Collections.AvaloniaList<Point>([ Point(14., 0.); Point(28., 24.); Point(0., 24.) ]), Fill=Brushes.DimGray)
        Canvas.SetLeft(tri, 12. + float i * 36.)
        Canvas.SetTop(tri, 14.)
        pic.Children.Add(tri)
        for r in 0..1 do
            let bx = Border(Width=30., Height=30., BorderBrush=Brushes.DarkRed, BorderThickness=Thickness(3.))
            Canvas.SetLeft(bx, 10. + float i * 36.)
            Canvas.SetTop(bx, 46. + float r * 32.)
            pic.Children.Add(bx)
    let topRow = StackPanel(Orientation=Orientation.Horizontal, Spacing=40., HorizontalAlignment=HorizontalAlignment.Center)
    topRow.Children.Add(narrow)
    topRow.Children.Add(pic)
    narrow.Width <- 340.
    root.Children.Add(topRow)
    let message = TextBlock(Foreground=Brushes.Gold, FontSize=13., TextWrapping=TextWrapping.Wrap, HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(0., 6., 0., 0.))
    let go (n:int) (saved:string option) =
        let name = if custom.IsChecked.GetValueOrDefault(false) then nameBox.Text.Trim().ToUpperInvariant() else ""
        (try TrackerModelOptions.writeSettings() with _ -> ())
        onStart n (full.IsChecked.GetValueOrDefault(true)) (hide.IsChecked.GetValueOrDefault(false)) name saved
    let startButton (text:string) (n:int) =
        let b = Button(Content=text, Width=640., HorizontalAlignment=HorizontalAlignment.Center, HorizontalContentAlignment=HorizontalAlignment.Center, Margin=Thickness(0., 4., 0., 4.), Padding=Thickness(8., 8.), FontSize=16.)
        b.Click.Add(fun _ -> go n None)
        b
    root.Children.Add(Border(Height=10.))
    root.Children.Add(startButton "Start: 1st Quest Overworld" 0)
    root.Children.Add(startButton "Start: 2nd Quest Overworld" 1)
    root.Children.Add(startButton "Start: Mixed - 1st Quest Overworld" 2)
    root.Children.Add(startButton "Start: Mixed - 2nd Quest Overworld (or randomized quest)" 3)
    root.Children.Add(heading "- OR -" 16.)
    // saved state: the preview's save file on desktop, or the clipboard (Copy save text) - also the way to do it in the browser
    let loadBtn = Button(Content="Start: from a previously saved state", Width=640., HorizontalAlignment=HorizontalAlignment.Center, HorizontalContentAlignment=HorizontalAlignment.Center, Margin=Thickness(0., 4., 0., 4.), Padding=Thickness(8., 8.), FontSize=16.)
    let startFrom (text:string) =
        try
            let n = int (text.Replace("\r", "").Split('\n').[0])
            go n (Some text)
        with _ -> message.Text <- "That doesn't look like a saved state."
    loadBtn.Click.Add(fun _ ->
        let saveFile = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "zt-preview-save.txt")
        let fromFile = try (if System.IO.File.Exists(saveFile) then Some(System.IO.File.ReadAllText(saveFile)) else None) with _ -> None
        match fromFile with
        | Some t -> startFrom t
        | None ->
            let top = TopLevel.GetTopLevel(loadBtn)
            if not (isNull top) && not (isNull top.Clipboard) then
                top.Clipboard.GetTextAsync().ContinueWith(fun (task:System.Threading.Tasks.Task<string>) ->
                    Avalonia.Threading.Dispatcher.UIThread.Post(fun () ->
                        if System.String.IsNullOrWhiteSpace task.Result then message.Text <- "No save file found, and the clipboard has no saved state (use Copy save in the tracker first)."
                        else startFrom task.Result)) |> ignore
            else message.Text <- "No save file found.")
    root.Children.Add(loadBtn)
    root.Children.Add(message)
    // settings (most can be changed later)
    root.Children.Add(heading "Settings (most can be changed later, using the options in the tracker):" 15.)
    let cols = makeSettingsGrid ()
    root.Children.Add(Border(BorderBrush=Brushes.Gray, BorderThickness=Thickness(1.), Child=cols))
    ScrollViewer(Content=root, HorizontalScrollBarVisibility=ScrollBarVisibility.Auto, VerticalScrollBarVisibility=ScrollBarVisibility.Auto) :> Control

let makeAppRoot () : Control =
    let host = ContentControl(Focusable=true)
    loadHotKeys ()
    host.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, System.EventHandler<Avalonia.Input.KeyEventArgs>(fun _ e -> handleKeyDown e), Avalonia.Interactivity.RoutingStrategies.Tunnel)
    focusHost.Value <- (fun () ->
        match TopLevel.GetTopLevel(host) with
        | null -> ()
        | tl ->
            match tl.FocusManager.GetFocusedElement() with
            | :? TextBox -> ()
            | _ -> host.Focus() |> ignore)
    host.Content <- makeStartupView (fun n heart hide name saved -> host.Content <- makeMainContent n heart hide name saved)
    host :> Control

let makeMainWindow() =
    let w = Window(Title="ZORA Tracker - cross-platform preview", Width=1240., Height=1160., WindowStartupLocation=WindowStartupLocation.CenterScreen, Background=Brushes.Black, Content=makeAppRoot ())
    w.Opened.Add(fun _ ->
        try
            match w.Screens.ScreenFromWindow(w) with
            | null -> ()
            | scr ->
                let wa = scr.WorkingArea
                let sc = scr.Scaling
                let maxH = float wa.Height / sc - 40.
                let maxW = float wa.Width / sc - 20.
                if w.Height > maxH then w.Height <- maxH
                if w.Width > maxW then w.Width <- maxW
                w.Position <- Avalonia.PixelPoint(wa.X + int ((float wa.Width - w.Width * sc) / 2.), wa.Y + int ((float wa.Height - w.Height * sc) / 2.) )
        with _ -> ())
    w

type App() =
    inherit Application()
    override this.Initialize() =
        this.Styles.Add(FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark
        let orange = SolidColorBrush(Color.FromRgb(255uy, 165uy, 0uy))
        let btn = Styling.Style(fun x -> Styling.Selectors.OfType<Button>(x))
        btn.Setters.Add(Styling.Setter(Button.BackgroundProperty, SolidColorBrush(Color.FromRgb(30uy, 30uy, 30uy))))
        btn.Setters.Add(Styling.Setter(Button.ForegroundProperty, orange))
        btn.Setters.Add(Styling.Setter(Button.BorderBrushProperty, orange))
        btn.Setters.Add(Styling.Setter(Button.BorderThicknessProperty, Thickness(1.)))
        btn.Setters.Add(Styling.Setter(Button.PaddingProperty, Thickness(8., 3.)))
        this.Styles.Add(btn)
    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop -> desktop.MainWindow <- makeMainWindow()
        | :? ISingleViewApplicationLifetime as single -> single.MainView <- makeAppRoot ()   // browser / mobile
        | _ -> ()
        base.OnFrameworkInitializationCompleted()
