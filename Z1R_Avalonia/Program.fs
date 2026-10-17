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

let TILE_W = 48.   // 16 source pixels x 3
let TILE_H = 33.   // 11 source pixels x 3

// the same strip the Windows tracker uses: 8 maps side by side, each 256x88 source pixels; 0=1Q 1=2Q 2=mixed 1Q 3=mixed 2Q
let strip = lazy (new Bitmap(System.IO.Path.Combine(System.AppContext.BaseDirectory, "s_map_overworld_vanilla_strip8.png")))

// ---- icons: same pixel art the Windows tracker uses (5x9-pixel icons, shown 3x so 15x27) ----
let stripOw = lazy (new Bitmap(System.IO.Path.Combine(System.AppContext.BaseDirectory, "ow_icons5x9.png")))
let stripItems = lazy (new Bitmap(System.IO.Path.Combine(System.AppContext.BaseDirectory, "icons3x7.png")))
// overworld tile state id -> position in the ow_icons5x9 strip (swords, secrets, door repair, money game, letter, armos, hint shop, take any, potion shop)
let stripIndex = dict [ 13,0; 14,1; 15,2; 24,11; 25,8; 26,6; 27,9; 28,12; 29,10; 30,13; 31,7; 32,3; 33,4; 34,5 ]

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
let strip7 = lazy (new Bitmap(System.IO.Path.Combine(System.AppContext.BaseDirectory, "icons7x7.png")))

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

let itemStrip (index:int) : Control = pixelImage strip7.Value (PixelRect(index * 7, 0, 7, 7)) 21. 21. :> Control

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
        | 15 | 16 -> potionIcon ()
        | 17 -> letterIcon ()
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
    let inner = itemIcon item
    Border(Background=Brushes.Black, Padding=Thickness(3.), Child=inner, IsHitTestVisible=false) :> Control

let makeMainWindow() =
    let w = Window(Title="ZORA Tracker - cross-platform preview", Width=16.*TILE_W+40., Height=8.*TILE_H+170., Background=Brushes.Black)
    let canvas = Canvas(Width=16.*TILE_W, Height=8.*TILE_H, Background=Brushes.Black)
    let status = TextBlock(Foreground=Brushes.Orange, FontSize=16., Margin=Thickness(0., 6., 0., 2.))
    let hover = TextBlock(Foreground=Brushes.White, FontSize=14., Margin=Thickness(0., 2., 0., 2.), Text="Hover a tile.  Left-click: next mark.  Right-click: pick a mark from the menu.")
    let currentN = ref 0
    let menuTile = ref (0, 0)
    let popup = Popup(PlacementTarget=canvas, Placement=PlacementMode.Pointer, IsLightDismissEnabled=true)

    // redraws map art, darkened always-empty tiles, and the current marks from the REAL tracker model
    let draw () =
        canvas.Children.Clear()
        let ow = TrackerModel.owInstance
        let img = Image(Width=16.*TILE_W, Height=8.*TILE_H, Stretch=Stretch.Fill,
                        Source=new CroppedBitmap(strip.Value, PixelRect(256*currentN.Value, 0, 256, 88)))
        RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None)
        canvas.Children.Add(img)
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
                            let icon = makeIcon st
                            icon.IsHitTestVisible <- false
                            Canvas.SetLeft(icon, left + (TILE_W - 15.) / 2.)
                            Canvas.SetTop(icon, top + (TILE_H - 27.) / 2.)
                            canvas.Children.Add(icon)
                    // marker icons go on top of the tile's own mark and its darkening, so they stay bright on finished (Don't Care) tiles
                    let decorList = TrackerModel.decorIconsOf(TrackerModel.owSwordIcons.[x, y])
                    let n = decorList.Length
                    if n > 0 then
                        marked <- marked + 1
                    decorList |> List.iteri (fun k ident ->
                        // 1 icon: 27px centered; 2-3 icons: smaller and side by side so they all fit on the tile
                        let size, dx, dy =
                            match n with
                            | 1 -> 27., (TILE_W - 27.) / 2., 3.
                            | 2 -> 24., float k * 24., 4.
                            | _ -> 16., float k * 16., 8.
                        let d = Viewbox(Width=size, Height=size, Child=decorOnMap ident, IsHitTestVisible=false)
                        Canvas.SetLeft(d, left + dx)
                        Canvas.SetTop(d, top + dy)
                        canvas.Children.Add(d))
        status.Text <- sprintf "%d marked tiles" marked

    let tileAt (p:Point) =
        let x = int (p.X / TILE_W)
        let y = int (p.Y / TILE_H)
        if p.X >= 0. && p.Y >= 0. && x <= 15 && y <= 7 then Some(x, y) else None

    canvas.PointerPressed.Add(fun e ->
        let pt = e.GetCurrentPoint(canvas)
        match tileAt pt.Position with
        | Some(x, y) when not (TrackerModel.owInstance.AlwaysEmpty(x, y)) ->
            if pt.Properties.IsLeftButtonPressed then
                TrackerModel.overworldMapMarks.[x, y].Next()
                TrackerModel.recomputeMapStateSummary()
                draw ()
            elif pt.Properties.IsRightButtonPressed then
                menuTile.Value <- (x, y)
                popup.IsOpen <- true
        | _ -> ())

    canvas.PointerMoved.Add(fun e ->
        match tileAt (e.GetPosition(canvas)) with
        | Some(x, y) ->
            let ow = TrackerModel.owInstance
            let notes =
                [ if ow.AlwaysEmpty(x, y) then yield "always empty"
                  if ow.HasArmos(x, y) then yield "armos"
                  if ow.Ladderable(x, y) then yield "needs ladder"
                  if ow.Raftable(x, y) then yield "raftable" ]
            hover.Text <- sprintf "Tile (%d,%d): %s" x y (if notes.IsEmpty then "nothing special" else String.concat ", " notes)
        | None -> ())

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
                    TrackerModel.recomputeMapStateSummary()
                    popup.IsOpen <- false
                    draw ())
                rp.Children.Add(b)
        menuGrid.Children.Add(rp)
    popup.Child <- Border(Child=menuGrid, Background=Brushes.Black, BorderBrush=Brushes.Gray, BorderThickness=Thickness(3.), Padding=Thickness(4.))

    let quests = [ "1st Quest", 0, OverworldData.OWQuest.FIRST
                   "2nd Quest", 1, OverworldData.OWQuest.SECOND
                   "Mixed 1Q", 2, OverworldData.OWQuest.MIXED_FIRST
                   "Mixed 2Q", 3, OverworldData.OWQuest.MIXED_SECOND ]
    let buttons = StackPanel(Orientation=Orientation.Horizontal, Spacing=8.)
    for (name, n, q) in quests do
        let b = Button(Content=name)
        b.Click.Add(fun _ ->
            // same model call the Windows tracker uses: keeps compatible marks, clears the rest
            TrackerModel.reinitializeOverworld(OverworldData.OverworldInstance(q))
            currentN.Value <- n
            draw ())
        buttons.Children.Add(b)

    // Save / Load (preview format: quest number, then the 128 tile states; saved next to the program)
    let saveFile = System.IO.Path.Combine(System.AppContext.BaseDirectory, "zt-preview-save.txt")
    let saveButton = Button(Content="Save")
    saveButton.Click.Add(fun _ ->
        let states = [ for y in 0..7 do for x in 0..15 do yield string (TrackerModel.overworldMapMarks.[x, y].Current()) ]
        let icons = [ for y in 0..7 do for x in 0..15 do yield string TrackerModel.owSwordIcons.[x, y] ]
        System.IO.File.WriteAllText(saveFile, string currentN.Value + "\n" + String.concat "," states + "\n" + String.concat "," icons)
        status.Text <- "Saved to " + saveFile)
    let loadButton = Button(Content="Load")
    loadButton.Click.Add(fun _ ->
        if System.IO.File.Exists(saveFile) then
            let lines = System.IO.File.ReadAllLines(saveFile)
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
            TrackerModel.recomputeMapStateSummary()
            draw ()
            status.Text <- "Loaded " + saveFile
        else
            status.Text <- "No save file yet")
    buttons.Children.Add(saveButton)
    buttons.Children.Add(loadButton)

    let root = StackPanel(Margin=Thickness(20., 10., 20., 10.))
    root.Children.Add(buttons)
    root.Children.Add(status)
    root.Children.Add(hover)
    root.Children.Add(canvas)
    root.Children.Add(popup)
    w.Content <- root
    // one-time model setup, exactly like the Windows tracker (1st Quest overworld, normal dungeon numbers)
    TrackerModel.initializeAll(OverworldData.OverworldInstance(OverworldData.OWQuest.FIRST), TrackerModel.DungeonTrackerInstanceKind.DEFAULT)
    draw ()
    w

type App() =
    inherit Application()
    override this.Initialize() =
        this.Styles.Add(FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark
    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop -> desktop.MainWindow <- makeMainWindow()
        | _ -> ()
        base.OnFrameworkInitializationCompleted()

[<EntryPoint>]
let main argv =
    AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(argv)
