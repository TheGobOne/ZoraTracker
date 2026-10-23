module Popouts

open System.Windows
open System.Windows.Media
open System.Windows.Controls

let MINIMIZED_THRESHOLD = ShowRunCustom.MINIMIZED_THRESHOLD

let mutable theMainWindow : Window = null
let mutable theMainWindowHasClosed = false
let mutable ctxt : System.Threading.SynchronizationContext = null
let Initialize(mainWindow,ctx) =
    theMainWindow <- mainWindow
    ctxt <- ctx
let refocusMainWindow() =   // keep hotkeys working
    async {
        do! Async.Sleep(500)  // give new window time to pop up
        do! Async.SwitchToContext(ctxt)
        theMainWindow.Focus() |> ignore
    } |> Async.StartImmediate

////////////////////////////////////////////////////////

let showHotKeys(isRightClick) =
    let none,p = OverworldMapTileCustomization.MakeMappedHotKeysDisplay()
    let w = new Window(Title="ZORATracker HotKeys", Owner=Application.Current.MainWindow)
    w.Content <- p
    let save() = 
        TrackerModelOptions.HotKeyWindowLTWH <- sprintf "%d,%d,%d,%d" (int w.Left) (int w.Top) (int w.Width) (int w.Height)
        TrackerModelOptions.writeSettings()
    let leftTopWidthHeight = TrackerModelOptions.HotKeyWindowLTWH
    let matches = System.Text.RegularExpressions.Regex.Match(leftTopWidthHeight, """^(-?\d+),(-?\d+),(\d+),(\d+)$""")
    if not none && not isRightClick && matches.Success && not(float matches.Groups.[1].Value < MINIMIZED_THRESHOLD) then
        w.Left <- float matches.Groups.[1].Value
        w.Top <- float matches.Groups.[2].Value
        w.Width <- float matches.Groups.[3].Value
        w.Height <- float matches.Groups.[4].Value
    else
        p.Measure(Size(1280., 720.))
        w.Width <- p.DesiredSize.Width + 16.
        w.Height <- p.DesiredSize.Height + 40.
    w.SizeChanged.Add(fun _ -> save(); refocusMainWindow())
    w.LocationChanged.Add(fun _ -> save(); refocusMainWindow())
    w.Show()

////////////////////////////////////////////////

let makeFauxItemsAndHeartsHUD() =
    let r = new System.Drawing.Bitmap(98,61+18)
    let best = Graphics.allItemsHUDBestBMP
    let worst = Graphics.allItemsHUDWorstBMP
    let BLACK = System.Drawing.Color.Black
    // draw full HUD
    for x = 0 to best.Width-1 do
        for y = 0 to best.Height-1 do
            r.SetPixel(x,y,best.GetPixel(x,y))
    // erase or change based on inventory
    let maybeErase(x1,x2,y1,y2,b) =
        if not(b) then
            for x = x1 to x2 do
                for y = y1 to y2 do
                    r.SetPixel(x,y,BLACK)
    let copyWorse(x1,x2,y1,y2,b) =
        if b then
            for x = x1 to x2 do
                for y = y1 to y2 do
                    r.SetPixel(x,y,worst.GetPixel(x,y))
    // top row
    maybeErase(6,19,0,15,TrackerModel.playerComputedStateSummary.HaveRaft)
    maybeErase(29,36,0,15,TrackerModel.PlayerHasTheBook())
    maybeErase(42,48,3,11,TrackerModel.playerComputedStateSummary.RingLevel>0)
    copyWorse(42,48,3,11,TrackerModel.playerComputedStateSummary.RingLevel=1)
    maybeErase(53,68,0,15,TrackerModel.playerComputedStateSummary.HaveLadder)
    maybeErase(73,81,0,15,TrackerModel.playerComputedStateSummary.HaveAnyKey)
    maybeErase(85,92,0,15,TrackerModel.playerComputedStateSummary.HavePowerBracelet)
    // middle row
    maybeErase(10,14,28,35,TrackerModel.playerComputedStateSummary.BoomerangLevel>0)
    copyWorse(10,14,28,35,TrackerModel.playerComputedStateSummary.BoomerangLevel=1)
    maybeErase(33,40,24,39,TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBombs.Value())
    maybeErase(55,59,24,39,TrackerModel.playerComputedStateSummary.ArrowLevel>0)
    copyWorse(55,59,24,39,TrackerModel.playerComputedStateSummary.ArrowLevel=1)
    maybeErase(61,69,24,39,TrackerModel.playerComputedStateSummary.HaveBow)
    maybeErase(81,88,24,39,TrackerModel.playerComputedStateSummary.CandleLevel>0)
    copyWorse(81,88,24,39,TrackerModel.playerComputedStateSummary.CandleLevel=1)
    // bottom row
    maybeErase(12,14,40,55,TrackerModel.playerComputedStateSummary.HaveRecorder)
    // meat: only shown once collected (starting-items checkbox or a meat box marked obtained); otherwise empty
    let haveMeat = 
        TrackerModel.startingItemsAndExtras.PlayerHasMeat.Value() ||
        (TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes() 
         |> Seq.exists (fun b -> b.CellCurrent() = TrackerModel.ITEMS.MEAT && b.PlayerHas() = TrackerModel.PlayerHas.YES))
    maybeErase(33,40,40,55,haveMeat)
    copyWorse(57,64,40,55,true)  // worse shows potion letter, maybeErased below
    maybeErase(57,64,40,55,TrackerModel.havePotionLetter.Value)
    maybeErase(83,86,40,55,TrackerModel.playerComputedStateSummary.HaveWand)
    // add hearts display
    if TrackerModel.playerComputedStateSummary.PlayerHearts > 8 then
        // top row
        for i = 0 to TrackerModel.playerComputedStateSummary.PlayerHearts - 9 do
            for dx = 0 to 7 do
                for dy = 0 to 7 do
                    r.SetPixel(12+i*8+dx, 63+dy, Graphics.empty_small_heart_bmp.GetPixel(dx,dy))
    // bottom row, 3 red
    for i = 0 to (min TrackerModel.playerComputedStateSummary.PlayerHearts 3)-1 do
        for dx = 0 to 7 do
            for dy = 0 to 7 do
                r.SetPixel(12+i*8+dx, 71+dy, Graphics.filled_small_heart_bmp.GetPixel(dx,dy))
    // bottom row rest
    for i = 3 to (min TrackerModel.playerComputedStateSummary.PlayerHearts 8) - 1 do
        for dx = 0 to 7 do
            for dy = 0 to 7 do
                r.SetPixel(12+i*8+dx, 71+dy, Graphics.empty_small_heart_bmp.GetPixel(dx,dy))
    // maybe change green to white/red based on ring? given randomize tunic colors, seems frivolous, so naah
    r
let MakeInventoryAndHearts() =
    let bmp = makeFauxItemsAndHeartsHUD()
    let img = Graphics.BMPtoImage bmp
    img.Width <- 3. * img.Width
    img.Height <- 3. * img.Height
    img.Stretch <- Stretch.UniformToFill
    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor)
    let sp = new StackPanel(Orientation=Orientation.Vertical)
    sp.Children.Add(img) |> ignore
    let playerKnowsLocationOfWoodBoomer = TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes() |> Seq.exists (fun b -> b.CellCurrent()=TrackerModel.ITEMS.BOOMERANG)
    let playerKnowsLocationOfWhiteSword = TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes() |> Seq.exists (fun b -> b.CellCurrent()=TrackerModel.ITEMS.WHITESWORD)
    if TrackerModel.playerComputedStateSummary.BoomerangLevel=2 && not(playerKnowsLocationOfWoodBoomer) then
        let tb = new TextBox(FontSize=14., Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, BorderThickness=Thickness(0.), 
                                Text="Note: having magical boomerang may make\n   it hard to notice wooden boomerang pick up")
        sp.Children.Add(tb) |> ignore
    if TrackerModel.playerComputedStateSummary.SwordLevel=3 && not(playerKnowsLocationOfWhiteSword) then
        let tb = new TextBox(FontSize=14., Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, BorderThickness=Thickness(0.), 
                                Text="Note: having magical sword may make it\n   hard to notice if white sword picked up")
        sp.Children.Add(tb) |> ignore
    sp
// Ctrl+mouse wheel over the Uncollected Items window grows / shrinks its icons
let mutable remainingItemsIconScale = 1.0
let MakeRemainingItemsDisplay() =
    let scaledImg(bmp:System.Drawing.Bitmap) =
        let r = Graphics.BMPtoImage(bmp)
        r.Stretch <- Stretch.Fill
        r.Width <- float bmp.Width * remainingItemsIconScale
        r.Height <- float bmp.Height * remainingItemsIconScale
        RenderOptions.SetBitmapScalingMode(r, BitmapScalingMode.NearestNeighbor)
        r.Margin <- Thickness(0.,0.,3.,3.)
        r
    let img(i) =
        let im = scaledImg(CustomComboBoxes.boxCurrentBMP(i, None))
        if Views.itemIsHintDashed i then
            let g = new Grid()
            g.Children.Add(im) |> ignore
            g.Children.Add(new Shapes.Rectangle(Stroke=Brushes.Red, StrokeThickness=2., StrokeDashArray=new DoubleCollection([3.;2.]), IsHitTestVisible=false)) |> ignore
            g :> UIElement
        else im :> UIElement
    let swordImg(bmp:System.Drawing.Bitmap) = scaledImg(bmp)
    // wraps onto more rows as the window is made narrower
    let hsp = new WrapPanel(Orientation=Orientation.Horizontal, Margin=Thickness(3.))
    // An item is "remaining" if no dungeon box has it marked as obtained (IsDone = CellCurrent set AND PlayerHas is YES or SKIPPED).
    // This correctly handles presets where all items are pre-placed but not yet obtained (PlayerHas=NO).
    let allBoxes = TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes()
    // (an item whose marker icon has been checked off as collected on the overworld map also counts as obtained)
    let isObtained(i) = (allBoxes |> Array.exists (fun box -> box.CellCurrent() = i && box.IsDone())) || TrackerModel.mapCollectedCount.[i] > 0
    // same order as the Item Progress bar: swords first (every sword level not yet reached)
    let swordLevel = TrackerModel.playerComputedStateSummary.SwordLevel
    if swordLevel < 1 then hsp.Children.Add(swordImg(Graphics.swordLevelToOwBmp(1))) |> ignore
    if swordLevel < 2 && not(isObtained TrackerModel.ITEMS.WHITESWORD) then hsp.Children.Add(swordImg(Graphics.swordLevelToOwBmp(2))) |> ignore
    if swordLevel < 3 && not(isObtained TrackerModel.ITEMS.MAGICALSWORD) then hsp.Children.Add(swordImg(Graphics.swordLevelToOwBmp(3))) |> ignore
    if TrackerModelOptions.FourthSwordEnabled && TrackerModel.playerHasSword4.Value <> 1 then hsp.Children.Add(swordImg(Graphics.ow_sword4_item_bmp)) |> ignore
    let candleLevel = TrackerModel.playerComputedStateSummary.CandleLevel
    let ringLevel = TrackerModel.playerComputedStateSummary.RingLevel
    for i in [| TrackerModel.ITEMS.SILVERARROW; TrackerModel.ITEMS.BOW; TrackerModel.ITEMS.LADDER; TrackerModel.ITEMS.POWERBRACELET; TrackerModel.ITEMS.RECORDER; TrackerModel.ITEMS.RAFT
                TrackerModel.ITEMS.BLUECANDLE; TrackerModel.ITEMS.REDCANDLE; TrackerModel.ITEMS.BLUERING; TrackerModel.ITEMS.REDRING; TrackerModel.ITEMS.WAND; TrackerModel.ITEMS.KEY
                TrackerModel.ITEMS.BOOMERANG; TrackerModel.ITEMS.MAGICBOOMERANG; TrackerModel.ITEMS.BOOKSHIELD |] do
        let levelReached =
            if i = TrackerModel.ITEMS.BLUECANDLE then candleLevel >= 1
            elif i = TrackerModel.ITEMS.REDCANDLE then candleLevel >= 2
            elif i = TrackerModel.ITEMS.BLUERING then ringLevel >= 1
            elif i = TrackerModel.ITEMS.REDRING then ringLevel >= 2
            else false
        if not (isObtained i) && not levelReached then
            hsp.Children.Add(img(i)) |> ignore
    let h = TrackerModel.ITEMS.HEARTCONTAINER
    let obtainedHC = (allBoxes |> Array.sumBy (fun box -> if box.CellCurrent() = h && box.IsDone() then 1 else 0)) + TrackerModel.mapCollectedCount.[h]
    // 13 hearts in all: the 9 in the item pool plus the 4 take-any hearts; every take-any slot that has been used (a heart, or a candle / potion taken instead) counts as collected
    let tookAnyCount = [0..3] |> List.filter (fun k -> TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyHeart(k) <> 0) |> List.length
    let c = TrackerModel.allItemWithHeartShuffleChoiceDomain.MaxUses(h) + 4 - obtainedHC - tookAnyCount
    if c >= 1 then
        // just one heart, followed by how many remain
        // the heart and its count are one unit, so they never get separated onto different rows when the window is resized
        let heartAndCount = new StackPanel(Orientation=Orientation.Horizontal)
        heartAndCount.Children.Add(img(h)) |> ignore
        heartAndCount.Children.Add(new TextBox(Text=sprintf "x%d" c,FontSize=20.*remainingItemsIconScale,FontWeight=FontWeights.Bold,IsReadOnly=true,IsHitTestVisible=false,BorderThickness=Thickness(0.),Foreground=Brushes.Orange,Background=Brushes.Black,VerticalAlignment=VerticalAlignment.Center)) |> ignore
        hsp.Children.Add(heartAndCount) |> ignore
    if hsp.Children.Count=0 then
        hsp.Children.Add(new TextBox(Text="None",FontSize=12.,IsReadOnly=true,IsHitTestVisible=false,BorderThickness=Thickness(0.),Foreground=Brushes.Orange,Background=Brushes.Black)) |> ignore
    let vsp = new StackPanel(Orientation=Orientation.Vertical)
    vsp.Children.Add(hsp) |> ignore
    vsp

////////////////////////////////////////////////////////

type PopoutKind =
    | SpotSummaryPopout
    | InventoryAndHeartsPopout
    | RemainingItemsPoput

let mutable theSpotSummaryPopoutWindow : Window = null
let mutable theInventoryAndHeartsPopoutWindow : Window = null
let mutable theRemainingItemsPopoutWindow : Window = null

let spotSummaryContent(doUIUpdateEvent:Event<unit>) =
    let dp = new DockPanel(Background=Brushes.Black)
    let redraw() =
        dp.Children.Clear()
        dp.Children.Add(OverworldMapTileCustomization.MakeRemainderSummaryDisplay()) |> ignore
    redraw()
    TrackerModel.mapStateSummaryComputedEvent.Publish.Add(fun _ -> 
//        printfn "redraw spot summary"
        redraw()
        )
    // also redraw as items get collected (the summary dims potions/letter/swords once collected)
    doUIUpdateEvent.Publish.Add(fun _ -> redraw())
    dp  // already has a border
let inventoryAndHeartsContent(doUIUpdateEvent:Event<unit>) =
    let dp = new DockPanel(Background=Brushes.Black)
    let redraw() =
        dp.Children.Clear()
        dp.Children.Add(MakeInventoryAndHearts()) |> ignore
    redraw()
    doUIUpdateEvent.Publish.Add(fun _ -> 
//        printfn "redraw inventory and hearts"
        redraw()
        )
    new Border(Child=dp, BorderBrush=Brushes.Gray, BorderThickness=Thickness(3.))
let remainingItemsContent(doUIUpdateEvent:Event<unit>) =
    remainingItemsIconScale <- TrackerModelOptions.RemainingItemsIconScale   // remembered from last time
    let dp = new DockPanel(Background=Brushes.Black)
    let redraw() =
        dp.Children.Clear()
        dp.Children.Add(MakeRemainingItemsDisplay()) |> ignore
    redraw()
    doUIUpdateEvent.Publish.Add(fun _ -> 
//        printfn "redraw remaining items"
        redraw()
        )
    let border = new Border(Child=dp, BorderBrush=Brushes.Gray, BorderThickness=Thickness(3.), Background=Brushes.Black)
    border.PreviewMouseWheel.Add(fun e ->
        if System.Windows.Input.Keyboard.Modifiers = System.Windows.Input.ModifierKeys.Control then
            e.Handled <- true
            let factor = if e.Delta > 0 then 1.15 else 1. / 1.15
            remainingItemsIconScale <- max 0.5 (min 8.0 (remainingItemsIconScale * factor))
            TrackerModelOptions.RemainingItemsIconScale <- remainingItemsIconScale
            TrackerModelOptions.writeSettings()
            redraw())
    border

let mutable insideMakePopoutCall = false
let makePopout(kind, isLeftClick, isRightClick, doUIUpdateEvent:Event<unit>) =
    let w = new Window(ResizeMode=ResizeMode.CanResizeWithGrip, SizeToContent=SizeToContent.WidthAndHeight, Topmost=false, ShowActivated=false)   // resizable: the content scales to fit the window (drag the edges or the corner grip)
    // the window opens at the content's natural size (DownOnly keeps the Viewbox from blowing the content up to fill the screen-sized space the window offers while sizing),
    // then it's frozen at that size and the content is free to scale both ways as the user drags the window bigger or smaller
    let savedW, savedH = ref 0., ref 0.   // size remembered from the last session (0 = none yet)
    let sz() = sprintf ",%d,%d" (int w.RestoreBounds.Width) (int w.RestoreBounds.Height)
    let scalable(e:UIElement) : UIElement =
        let vb = new System.Windows.Controls.Viewbox(Child=e, Stretch=System.Windows.Media.Stretch.Uniform, StretchDirection=System.Windows.Controls.StretchDirection.DownOnly)
        w.ContentRendered.Add(fun _ ->
            if w.SizeToContent <> SizeToContent.Manual then
                let aw, ah = (if savedW.Value > 0. then savedW.Value else w.ActualWidth), (if savedH.Value > 0. then savedH.Value else w.ActualHeight)
                w.SizeToContent <- SizeToContent.Manual
                w.Width <- aw
                w.Height <- ah
            vb.StretchDirection <- System.Windows.Controls.StretchDirection.Both)
        upcast vb
    // like scalable, but the content is not scaled: it re-flows onto more or fewer rows as the window is resized
    let wrapping(e:UIElement) : UIElement =
        w.ContentRendered.Add(fun _ ->
            if w.SizeToContent <> SizeToContent.Manual then
                let aw, ah = (if savedW.Value > 0. then savedW.Value else w.ActualWidth), (if savedH.Value > 0. then savedH.Value else w.ActualHeight)
                w.SizeToContent <- SizeToContent.Manual
                w.Width <- aw
                w.Height <- ah)
        e
    //w.Owner <- Application.Current.MainWindow  // we don't do this; doing so causes clicking any window to bring all ZT windows to the front, making impossible to layer
                                                 // popout ZT windows with other Windows applications in Z-order of windows desktop
    Application.Current.MainWindow.Closed.Add(fun _ -> theMainWindowHasClosed <- true; try w.Close() with _ -> ())  // do this to get similar close behavior to ownership
    let mutable save = fun() -> ()
    let mutable noDisplay = fun() -> ()
    insideMakePopoutCall <- true
    let matches =    
        match kind with
        | SpotSummaryPopout ->
            if theSpotSummaryPopoutWindow <> null then
                theSpotSummaryPopoutWindow.Close()
            theSpotSummaryPopoutWindow <- w
            w.Title <- "Spot Summary"
            w.Content <- scalable(spotSummaryContent(doUIUpdateEvent))
            save <- (fun() ->
                if w.Left < MINIMIZED_THRESHOLD then // minimized or closing
                    TrackerModelOptions.SpotSummaryPopout_DisplayedLT <- sprintf "0,%d,%d" (int w.RestoreBounds.Left) (int w.RestoreBounds.Top) + sz()
                else
                    TrackerModelOptions.SpotSummaryPopout_DisplayedLT <- sprintf "1,%d,%d" (int w.Left) (int w.Top) + sz()
                TrackerModelOptions.writeSettings()
                )
            noDisplay <- (fun () ->
                if TrackerModelOptions.SpotSummaryPopout_DisplayedLT.Length <> 0 then
                    TrackerModelOptions.SpotSummaryPopout_DisplayedLT <- "0"+TrackerModelOptions.SpotSummaryPopout_DisplayedLT.Substring(1)
                    TrackerModelOptions.writeSettings()
                )
            System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.SpotSummaryPopout_DisplayedLT, """^(1|0),(-?\d+),(-?\d+)(?:,(\d+),(\d+))?$""")
        | InventoryAndHeartsPopout ->
            if theInventoryAndHeartsPopoutWindow <> null then
                theInventoryAndHeartsPopoutWindow.Close()
            theInventoryAndHeartsPopoutWindow <- w
            w.Title <- "Inventory and Max Hearts popout"
            w.Content <- scalable(inventoryAndHeartsContent(doUIUpdateEvent))
            save <- (fun() ->
                if w.Left < MINIMIZED_THRESHOLD then // minimized or closing
                    TrackerModelOptions.InventoryAndHeartsPopout_DisplayedLT <- sprintf "0,%d,%d" (int w.RestoreBounds.Left) (int w.RestoreBounds.Top) + sz()
                else
                    TrackerModelOptions.InventoryAndHeartsPopout_DisplayedLT <- sprintf "1,%d,%d" (int w.Left) (int w.Top) + sz()
                TrackerModelOptions.writeSettings()
                )
            noDisplay <- (fun () ->
                if TrackerModelOptions.InventoryAndHeartsPopout_DisplayedLT.Length <> 0 then
                    TrackerModelOptions.InventoryAndHeartsPopout_DisplayedLT <- "0"+TrackerModelOptions.InventoryAndHeartsPopout_DisplayedLT.Substring(1)
                    TrackerModelOptions.writeSettings()
                )
            System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.InventoryAndHeartsPopout_DisplayedLT, """^(1|0),(-?\d+),(-?\d+)(?:,(\d+),(\d+))?$""")
        | RemainingItemsPoput ->
            if theRemainingItemsPopoutWindow <> null then
                theRemainingItemsPopoutWindow.Close()
            theRemainingItemsPopoutWindow <- w
            w.Title <- "Uncollected Items"
            w.Content <- wrapping(remainingItemsContent(doUIUpdateEvent))
            save <- (fun() ->
                if w.Left < MINIMIZED_THRESHOLD then // minimized or closing
                    TrackerModelOptions.RemainingItemsPoput_DisplayedLT <- sprintf "0,%d,%d" (int w.RestoreBounds.Left) (int w.RestoreBounds.Top) + sz()
                else
                    TrackerModelOptions.RemainingItemsPoput_DisplayedLT <- sprintf "1,%d,%d" (int w.Left) (int w.Top) + sz()
                TrackerModelOptions.writeSettings()
                )
            noDisplay <- (fun () ->
                if TrackerModelOptions.RemainingItemsPoput_DisplayedLT.Length <> 0 then
                    TrackerModelOptions.RemainingItemsPoput_DisplayedLT <- "0"+TrackerModelOptions.RemainingItemsPoput_DisplayedLT.Substring(1)
                    TrackerModelOptions.writeSettings()
                )
            System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.RemainingItemsPoput_DisplayedLT, """^(1|0),(-?\d+),(-?\d+)(?:,(\d+),(\d+))?$""")
    let ok,disp,left,top = 
        if matches.Success && not(float matches.Groups.[2].Value < MINIMIZED_THRESHOLD) then
            true,(int matches.Groups.[1].Value)=1, float matches.Groups.[2].Value, float matches.Groups.[3].Value
        else
            false,false,0.,0.
    if matches.Success && matches.Groups.[4].Success && matches.Groups.[5].Success && float matches.Groups.[4].Value >= 100. && float matches.Groups.[5].Value >= 60. then
        savedW.Value <- float matches.Groups.[4].Value
        savedH.Value <- float matches.Groups.[5].Value
        w.SizeToContent <- SizeToContent.Manual   // apply the remembered size up front (before Show), rather than after first render
        w.Width <- savedW.Value
        w.Height <- savedH.Value
    if (isLeftClick && ok)                                  // the user explicitly is trying to show the window, and we are able to parse valid coords
        || (not isLeftClick && not isRightClick && disp)    // it's startup automatically looking for windows to display, and this one has disp=1 and valid coords
            then
        w.Left <- left
        w.Top <- top
    // otherwise (e.g. right click, bad data) just display a new window at default location selected by Windows
    w.LocationChanged.Add(fun _ -> save(); refocusMainWindow())
    let sizeSaveTimer = new System.Windows.Threading.DispatcherTimer(Interval=System.TimeSpan.FromMilliseconds(600.))
    sizeSaveTimer.Tick.Add(fun _ -> sizeSaveTimer.Stop(); save())
    w.SizeChanged.Add(fun _ -> if w.IsLoaded && w.SizeToContent = SizeToContent.Manual then (sizeSaveTimer.Stop(); sizeSaveTimer.Start()))   // remember the size the user drags it to
    w.Closed.Add(fun _ -> if not(theMainWindowHasClosed) && not(insideMakePopoutCall) then noDisplay())
    w.Closed.Add(fun _ ->   // (so the Popout Windows menu knows it is no longer open)
        match kind with
        | SpotSummaryPopout -> if obj.ReferenceEquals(theSpotSummaryPopoutWindow, w) then theSpotSummaryPopoutWindow <- null
        | InventoryAndHeartsPopout -> if obj.ReferenceEquals(theInventoryAndHeartsPopoutWindow, w) then theInventoryAndHeartsPopoutWindow <- null
        | RemainingItemsPoput -> if obj.ReferenceEquals(theRemainingItemsPopoutWindow, w) then theRemainingItemsPopoutWindow <- null)
    w.Show()
    insideMakePopoutCall <- false

// used by the Popout Windows menu: is a popout open, open it (at its remembered spot), close it (which is remembered too)
let mutable theUIUpdateEvent : Event<unit> option = None
let private popoutWindowOf(kind) =
    match kind with
    | SpotSummaryPopout -> theSpotSummaryPopoutWindow
    | InventoryAndHeartsPopout -> theInventoryAndHeartsPopoutWindow
    | RemainingItemsPoput -> theRemainingItemsPopoutWindow
let isPopoutOpen(kind) = popoutWindowOf(kind) <> null
let openPopout(kind) =
    match theUIUpdateEvent with
    | Some ev -> if popoutWindowOf(kind) = null then makePopout(kind, true, false, ev)
    | None -> ()
let closePopout(kind) =
    let w = popoutWindowOf(kind)
    if w <> null then (try w.Close() with _ -> ())

// shop price info: tile-state → (display name, price range text, min, max, step)
let private shopPriceInfo = dict [
    TrackerModel.MapSquareChoiceDomainHelper.ARROW,        ("Arrow Shop",    "60-100r",  60, 100, 10)
    TrackerModel.MapSquareChoiceDomainHelper.BOMB,         ("Bomb Shop",     "1-40r",     1,  40,  5)
    TrackerModel.MapSquareChoiceDomainHelper.BOOK,         ("Book Shop",    "180-220r", 180, 220, 10)
    TrackerModel.MapSquareChoiceDomainHelper.BLUE_CANDLE,  ("Candle Shop",  "40-80r",   40,  80, 10)
    TrackerModel.MapSquareChoiceDomainHelper.BLUE_RING,    ("Blue Ring",   "230-255r",  230, 255,  5)
    TrackerModel.MapSquareChoiceDomainHelper.MEAT,         ("Meat Shop",   "40-120r",   40, 120, 10)
    TrackerModel.MapSquareChoiceDomainHelper.KEY,          ("Key Shop",    "60-120r",   60, 120, 10)
    TrackerModel.MapSquareChoiceDomainHelper.SHIELD,       ("Shield Shop", "70-180r",   70, 180, 10)
    TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP,    ("Hint Shop",   "10-60r",    10,  60, 10)
    TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP,  ("Potion Shop", "20-88r",    20,  88,  8)
    ]

let mutable private theShopPricesWindow : Window = null

let showShopPricesWindow() =
    if theShopPricesWindow <> null then
        theShopPricesWindow.Activate() |> ignore
    else
    let w = new Window(Title="Shop Prices", SizeToContent=SizeToContent.WidthAndHeight,
                        Background=Brushes.Black, Foreground=Brushes.Orange, Topmost=false)
    Application.Current.MainWindow.Closed.Add(fun _ -> try w.Close() with _ -> ())
    w.Closed.Add(fun _ -> theShopPricesWindow <- null)
    theShopPricesWindow <- w
    let sp = new StackPanel(Orientation=Orientation.Vertical, Margin=Thickness(6.))
    let header = new TextBox(Text="Shops found on overworld map — enter the price you saw:", IsReadOnly=true,
                              Background=Brushes.Black, Foreground=Brushes.Orange, BorderThickness=Thickness(0.), Margin=Thickness(0.,0.,0.,4.))
    sp.Children.Add(header) |> ignore
    let grid = new Grid()
    grid.ColumnDefinitions.Add(new ColumnDefinition(Width=GridLength.Auto))  // 0: coord
    grid.ColumnDefinitions.Add(new ColumnDefinition(Width=GridLength.Auto))  // 1: icon
    grid.ColumnDefinitions.Add(new ColumnDefinition(Width=new GridLength(120.)))  // 2: name
    grid.ColumnDefinitions.Add(new ColumnDefinition(Width=new GridLength(70.)))   // 3: range
    grid.ColumnDefinitions.Add(new ColumnDefinition(Width=new GridLength(160.)))  // 4: price dropdown
    let makeCell(text, col, row) =
        let tb = new TextBox(Text=text, IsReadOnly=true, Background=Brushes.Black, Foreground=Brushes.Orange, BorderThickness=Thickness(0.), Margin=Thickness(2.), VerticalContentAlignment=VerticalAlignment.Center)
        Grid.SetColumn(tb, col)
        Grid.SetRow(tb, row)
        grid.Children.Add(tb) |> ignore
    let mutable rowIdx = 0
    // collect all marked shop tiles
    let shops = ResizeArray()
    for j = 0 to 7 do
        for i = 0 to 15 do
            let state = TrackerModel.overworldMapMarks.[i,j].Current()
            if shopPriceInfo.ContainsKey(state) then
                shops.Add((i, j, state))
    if shops.Count = 0 then
        sp.Children.Add(new TextBox(Text="No shops found on the overworld map yet.", IsReadOnly=true,
                                     Background=Brushes.Black, Foreground=Brushes.DarkGray, BorderThickness=Thickness(0.))) |> ignore
    else
        for (i, j, state) in shops do
            grid.RowDefinitions.Add(new RowDefinition(Height=GridLength.Auto))
            let colLabel = sprintf "%c%d" (char (int 'A' + j)) (i + 1)
            makeCell(colLabel, 0, rowIdx)
            // icon
            let bmp = OverworldMapTileCustomization.MapStateProxy(state).DefaultInteriorBmp()
            let img = Graphics.BMPtoImage(bmp)
            img.Width <- 20.; img.Height <- 20.
            img.Margin <- Thickness(2.)
            Grid.SetColumn(img, 1)
            Grid.SetRow(img, rowIdx)
            grid.Children.Add(img) |> ignore
            let name, range, pMin, pMax, pStep = shopPriceInfo.[state]
            makeCell(name, 2, rowIdx)
            makeCell(range, 3, rowIdx)
            // price dropdown
            let cb = new ComboBox(Margin=Thickness(2.), Background=Brushes.Black, Foreground=Brushes.Orange, Width=150.)
            cb.Items.Add("(unknown)") |> ignore
            let mutable selectedIdx = 0
            let mutable valIdx = 1
            let currentPrice = TrackerModel.getShopPrice(i, j)
            let mutable v = pMin
            while v <= pMax do
                cb.Items.Add(sprintf "%dr" v) |> ignore
                if v = currentPrice then selectedIdx <- valIdx
                valIdx <- valIdx + 1
                v <- v + pStep
            cb.SelectedIndex <- selectedIdx
            let ii, jj = i, j  // capture loop vars
            cb.SelectionChanged.Add(fun _ ->
                if cb.SelectedIndex = 0 then
                    TrackerModel.setShopPrice(ii, jj, 0)
                else
                    let text = cb.SelectedItem.ToString()
                    match System.Int32.TryParse(text.TrimEnd('r')) with
                    | true, price -> TrackerModel.setShopPrice(ii, jj, price)
                    | _ -> ()
                )
            Grid.SetColumn(cb, 4)
            Grid.SetRow(cb, rowIdx)
            grid.Children.Add(cb) |> ignore
            rowIdx <- rowIdx + 1
        sp.Children.Add(grid) |> ignore
    let sv = new ScrollViewer(Content=sp, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, MaxHeight=600.)
    let border = new Border(Child=sv, BorderBrush=Brushes.Gray, BorderThickness=Thickness(3.), Background=Brushes.Black)
    ShowRunCustom.makeScalable(w, border)
    // remember where the window was left (and how big)
    let m = System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.ShopPricesWindowLTWH, """^(-?\d+),(-?\d+),(\d+),(\d+)$""")
    if m.Success && not(float m.Groups.[1].Value < ShowRunCustom.MINIMIZED_THRESHOLD) && float m.Groups.[3].Value >= 100. && float m.Groups.[4].Value >= 60. then
        w.WindowStartupLocation <- WindowStartupLocation.Manual
        w.Left <- float m.Groups.[1].Value
        w.Top <- float m.Groups.[2].Value
        w.SizeToContent <- SizeToContent.Manual
        w.Width <- float m.Groups.[3].Value
        w.Height <- float m.Groups.[4].Value
    let saveBounds() =
        if w.IsLoaded && not(w.Left < ShowRunCustom.MINIMIZED_THRESHOLD) && w.WindowState = WindowState.Normal then
            TrackerModelOptions.ShopPricesWindowLTWH <- sprintf "%d,%d,%d,%d" (int w.Left) (int w.Top) (int w.ActualWidth) (int w.ActualHeight)
            TrackerModelOptions.writeSettings()
    w.LocationChanged.Add(fun _ -> saveBounds())
    w.SizeChanged.Add(fun _ -> if w.SizeToContent = SizeToContent.Manual then saveBounds())
    w.Show()

let Startup(doUIUpdateEvent:Event<unit>) =
    theUIUpdateEvent <- Some doUIUpdateEvent
    let f(kind, setting) =
        let matches = System.Text.RegularExpressions.Regex.Match(setting, """^(1|0),(-?\d+),(-?\d+)(?:,(\d+),(\d+))?$""")   // (optionally followed by the remembered size)
        let disp,_left,_top = 
            if matches.Success && not(float matches.Groups.[2].Value < MINIMIZED_THRESHOLD) then
                (int matches.Groups.[1].Value)=1, float matches.Groups.[2].Value, float matches.Groups.[3].Value
            else
                false,0.,0.
        if disp then
            makePopout(kind, false, false, doUIUpdateEvent)
    for k,s in [  SpotSummaryPopout, TrackerModelOptions.SpotSummaryPopout_DisplayedLT
                  InventoryAndHeartsPopout, TrackerModelOptions.InventoryAndHeartsPopout_DisplayedLT
                  RemainingItemsPoput, TrackerModelOptions.RemainingItemsPoput_DisplayedLT
               ] do
        f(k,s)

        
