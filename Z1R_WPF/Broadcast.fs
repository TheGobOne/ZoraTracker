module Broadcast

open OverworldItemGridUI

open OverworldMapTileCustomization

open System.Windows
open System.Windows.Controls 
open System.Windows.Media

let allWithinOneScreen(w:Window) =
    let resHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height  // e.g. 1440 pixels
    let actualHeight = SystemParameters.PrimaryScreenHeight  // e.g. 960 units
    let scale = float resHeight / actualHeight  // e.g.   1.5 

    //printfn "Broadcast LTWHS: %f %f %f %f %f" w.Left w.Top w.Width w.Height scale
    // for whatever reason, this FUDGE is needed to be accurate
    let W_FUDGE = 8
    let H_FUDGE = 10
    let rect = new System.Drawing.Rectangle(int(w.Left * scale), int(w.Top * scale), int(w.Width * scale) - W_FUDGE, int(w.Height * scale) - H_FUDGE)

    let screens = System.Windows.Forms.Screen.AllScreens
    let mutable ok = false
    for s in screens do
        //printfn "Screen LTWH: %d %d %d %d" s.WorkingArea.Left s.WorkingArea.Top s.WorkingArea.Width s.WorkingArea.Height
        if s.WorkingArea.Contains(rect) then
            //printfn "ok on that screen"
            ok <- true
        else
            //printfn "not ok on that screen"
            ()
    ok

let makeViewRectImpl(upperLeft:Point, lowerRight:Point, c) =
    let vb = new VisualBrush(c)
    vb.ViewboxUnits <- BrushMappingMode.Absolute
    vb.Viewbox <- Rect(upperLeft, lowerRight)
    vb.Stretch <- Stretch.None
    let bwRect = new Shapes.Rectangle(Width=vb.Viewbox.Width, Height=vb.Viewbox.Height)
    bwRect.Fill <- vb
    bwRect

// the Max Hearts button (set by WPFUI); the dungeon-hover view of the broadcast window shows a live copy of it under the take-any hearts, since the original is hidden behind the map there
let mutable maxHeartsForBroadcast : FrameworkElement = null
let MakeBroadcastWindow(cm:CustomComboBoxes.CanvasManager, drawingCanvas:Canvas, blockerGrid:Grid, dungeonTabsOverlayContent:Canvas, refocusMainWindow) =
    let appMainCanvas = cm.AppMainCanvas
    let makeBroadcastWindow(size, showOverworldMagnifier) =
        let W = 768.
        let scaleSize = if size=1 then 256. elif size=2 then 512. else 768.
        let broadcastWindow = new Window()
        broadcastWindow.Title <- "Z-Tracker broadcast"
        broadcastWindow.ResizeMode <- ResizeMode.CanResize   // resizable: the content scales (via a Viewbox) to fit the window
        broadcastWindow.SizeToContent <- SizeToContent.Manual
        broadcastWindow.WindowStartupLocation <- WindowStartupLocation.Manual

        let topBar = new StackPanel(Orientation=Orientation.Vertical, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center, 
                                        Background=Graphics.freeze(new SolidColorBrush(Color.FromRgb(120uy, 30uy, 30uy))), Width=scaleSize, Opacity=0.0)
        let tb = new TextBox(Text="The broadcast window is at least partially off-screen.  Streaming software like OBS cannot capture display updates to off-screen windows.  " +
                                    "Re-position this window so that it is entirely within the bounds of a screen.  It is ok for this window to be BEHIND another window, " +
                                    "such as behind the main Z-Tracker application window.  See the Z-Tracker documentation for details.",
                                    Foreground=Brushes.White, Background=Brushes.Transparent, 
                                    IsReadOnly=true, BorderThickness=Thickness(0.), FontSize=14., VerticalAlignment=VerticalAlignment.Center, TextWrapping=TextWrapping.Wrap)
        topBar.Children.Add(tb) |> ignore
        let updateTopBar() =
            if allWithinOneScreen(broadcastWindow) then
                topBar.Opacity <- 0.0
            else
                topBar.Opacity <- 1.0

        let leftTop = TrackerModelOptions.BroadcastWindowLT
        let matches = System.Text.RegularExpressions.Regex.Match(leftTop, """^(-?\d+),(-?\d+)$""")
        if matches.Success then
            broadcastWindow.Left <- float matches.Groups.[1].Value
            broadcastWindow.Top <- float matches.Groups.[2].Value

        broadcastWindow.Loaded.Add(fun _ -> updateTopBar())
        broadcastWindow.LocationChanged.Add(fun _ ->
            updateTopBar()
            TrackerModelOptions.BroadcastWindowLT <- sprintf "%d,%d" (int broadcastWindow.Left) (int broadcastWindow.Top)
            TrackerModelOptions.writeSettings()
            )
        broadcastWindow.Width <- scaleSize + 16.
        broadcastWindow.Owner <- Application.Current.MainWindow
        broadcastWindow.Background <- Brushes.Black

        let makeViewRect(upperLeft:Point, lowerRight:Point) = makeViewRectImpl(upperLeft, lowerRight, appMainCanvas)
        let dealWithPopups(topViewboxRelativeToApp, topViewboxRelativeToThisBroadcast, c) =
            let popups = new System.Collections.Generic.Dictionary<Canvas, Shapes.Rectangle>(HashIdentity.Reference)   // (keyed by popup layer, so closing popups out of order removes the right copy)
            let popupCanvasArea = new Canvas()
            canvasAdd(c, popupCanvasArea, 0., 0.)
            cm.AfterCreatePopupCanvas.Add(fun pc ->
                let vb = new VisualBrush(pc)
                vb.ViewboxUnits <- BrushMappingMode.Absolute
                vb.Viewbox <- Rect(Point(0.,0.), Point(appMainCanvas.Width,appMainCanvas.Height))
                vb.Stretch <- Stretch.None
                let bwRect = new Shapes.Rectangle(Width=vb.Viewbox.Width, Height=vb.Viewbox.Height)
                bwRect.Fill <- vb
                popups.[pc] <- bwRect
                canvasAdd(popupCanvasArea, bwRect, 0., topViewboxRelativeToThisBroadcast - topViewboxRelativeToApp)
                )
            cm.BeforeDismissPopupCanvas.Add(fun pc ->
                match popups.TryGetValue(pc) with   // (no entry if the user turned on the broadcast window mid-game, as the options window was a popup when the broadcast began)
                | true, bwRect ->
                    popups.Remove(pc) |> ignore
                    popupCanvasArea.Children.Remove(bwRect)
                | _ -> ()
                )

        let timeline = makeViewRect(Point(0.,START_TIMELINE_H), Point(W,THRU_TIMELINE_H))
    
        // construct the top broadcast canvas (topc)
        let notesY = START_DUNGEON_AND_NOTES_AREA_H + blockerGrid.Height
        let top = makeViewRect(Point(0.,0.), Point(W,notesY))
        let drawingOverTop = makeViewRectImpl(Point(0.,0.), Point(W,notesY), drawingCanvas)
        let H = top.Height + (THRU_TIMELINE_H - START_TIMELINE_H)  // the top one is the larger of the two, so always have window that size
        let topc = new Canvas(Width=W, Height=H)
        canvasAdd(topc, top, 0., 0.)
        let notes = makeViewRect(Point(BLOCKERS_AND_NOTES_OFFSET,notesY), Point(W,notesY+blockerGrid.Height))
        canvasAdd(topc, notes, 0., START_DUNGEON_AND_NOTES_AREA_H)
        let blackArea = new Canvas(Width=BLOCKERS_AND_NOTES_OFFSET*2.-W, Height=blockerGrid.Height, Background=Brushes.Black)
        canvasAdd(topc, blackArea, W-BLOCKERS_AND_NOTES_OFFSET, START_DUNGEON_AND_NOTES_AREA_H)
        if showOverworldMagnifier then
            let magnifierView = new Canvas(Width=dungeonTabsOverlayContent.Width, Height=dungeonTabsOverlayContent.Height, Background=new VisualBrush(dungeonTabsOverlayContent))
            Canvas.SetLeft(magnifierView, 0.)
            Canvas.SetBottom(magnifierView, 0.)
            topc.Children.Add(magnifierView) |> ignore
        canvasAdd(topc, drawingOverTop, 0., 0.)
        dealWithPopups(0., 0., topc)

        // construct the bottom broadcast canvas (bottomc)
        let afterSoldItemBoxesX = OW_ITEM_GRID_LOCATIONS.OFFSET + 120.
        let drawingOverTop = makeViewRectImpl(Point(0.,0.), Point(afterSoldItemBoxesX,150.), drawingCanvas)
        let dun = makeViewRect(Point(0.,THRU_MAIN_MAP_AND_ITEM_PROGRESS_H), Point(W,START_TIMELINE_H))
        let tri = makeViewRect(Point(0.,0.), Point(W,150.))
        let pro = makeViewRect(Point(ITEM_PROGRESS_FIRST_ITEM,THRU_MAP_AND_LEGEND_H+4.), 
                                Point(ITEM_PROGRESS_FIRST_ITEM + 15.*30.-11.,THRU_MAIN_MAP_AND_ITEM_PROGRESS_H))  // -11. because 'Hint decoder' button infringes into empty space by Magic Key 
        pro.HorizontalAlignment <- HorizontalAlignment.Left
        pro.Margin <- Thickness(10.,4.,0.,0.)   // (the view starts 4px below the legend row so the bottom edge of the Dead Woods / Lost Hills buttons does not show; the margin keeps the layout height)
        let proScale = new ScaleTransform(0.88, 0.88)   // shrunk so the whole strip ends before the map
        if proScale.CanFreeze then proScale.Freeze()
        pro.RenderTransform <- proScale
        let owm = makeViewRect(Point(0.,150.), Point(W,THRU_MAIN_MAP_H))
        let sp = new StackPanel(Orientation=Orientation.Vertical)
        sp.Children.Add(tri) |> ignore
        sp.Children.Add(new DockPanel(Height=50.)) |> ignore
        sp.Children.Add(pro) |> ignore   // the item progress strip sits right above the dungeon panels
        sp.Children.Add(dun) |> ignore
        let bottomc = new Canvas(Width=W, Height=H)
        canvasAdd(bottomc, sp, 0., 0.)
        let scaleW = (W - afterSoldItemBoxesX) / W
        let scaleH = (180.-45.)/(THRU_MAIN_MAP_H-150.)
        let scaleTrans = new ScaleTransform(scaleW,scaleH)
        if scaleTrans.CanFreeze then
            scaleTrans.Freeze()
        owm.RenderTransform <- scaleTrans
        canvasAdd(bottomc, owm, afterSoldItemBoxesX, 95.)
        if not (isNull maxHeartsForBroadcast) then
            let mh = new Shapes.Rectangle(Width=maxHeartsForBroadcast.Width, Height=maxHeartsForBroadcast.Height, Fill=new VisualBrush(maxHeartsForBroadcast))
            let heartsCenterX = OW_ITEM_GRID_LOCATIONS.OFFSET + 60.   // the four take-any hearts span 4 cells of 30px from the grid's left edge
            canvasAdd(bottomc, mh, heartsCenterX - maxHeartsForBroadcast.Width/2., 152.)   // centered just under the hearts, left of the map
        // WANT!
        canvasAdd(bottomc, drawingOverTop, 0., 0.)
        dealWithPopups(THRU_MAIN_MAP_AND_ITEM_PROGRESS_H, 230., bottomc)

        // draw fake mice on top level 
        let addFakeMouse(c:Canvas) =
            let fakeMouse = new Shapes.Polygon(Fill=Brushes.White)
            fakeMouse.Points <- new PointCollection([Point(0.,0.); Point(12.,6.); Point(6.,12.)])
            c.Children.Add(fakeMouse) |> ignore
            fakeMouse
        let fakeTopMouse = addFakeMouse(topc)
        let fakeBottomMouse = addFakeMouse(bottomc)

        // set up the main broadcast window
        broadcastWindow.Height <- H + 40.
        let dp = new DockPanel(Width=W, Height=H)
        DockPanel.SetDock(timeline, Dock.Bottom)
        dp.Children.Add(timeline) |> ignore
        dp.Children.Add(topc) |> ignore
        
        let factor = if size=1 then 0.333333 elif size=2 then 0.666666 else 1.0   // only the initial window size; the content is always laid out at full size and scaled by the Viewbox below
        if size=1 || size=2 then
            broadcastWindow.Height <- H*factor + 40.
        OverworldItemGridUI.broadcastTimeTextBox.LayoutTransform <- null
        let c = new Canvas(Width=W, Height=H)
        c.UseLayoutRounding <- true
        c.Children.Add(dp) |> ignore
        OverworldItemGridUI.broadcastTimeTextBox.Parent :?> Canvas |> (fun c -> if c <> null then c.Children.Remove(OverworldItemGridUI.broadcastTimeTextBox))  // deparent from prior window
        // the timer sits centered in the right-hand column of the control panel (the same spot as in the main window), clear of the Popout Windows button
        let bt = OverworldItemGridUI.broadcastTimeTextBox
        bt.Width <- 120.; bt.Height <- 44.; bt.FontSize <- 26.; bt.TextAlignment <- TextAlignment.Center; bt.VerticalContentAlignment <- VerticalAlignment.Center; bt.Padding <- Thickness(0.)
        canvasAdd(c, bt, OverworldItemGridUI.OMTW*16. - 200. - 44. + 114. + 13. - 10., 8.)
        topBar.Width <- W
        c.Children.Add(topBar) |> ignore
        broadcastWindow.Content <- new Viewbox(Stretch=Stretch.Uniform, StretchDirection=StretchDirection.Both, Child=c)
        broadcastWindow.MinWidth <- 160.
        broadcastWindow.MinHeight <- 100.
        // remember the size the user gave this window
        let wh = System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.BroadcastWindowWH, """^(\d+),(\d+)$""")
        if wh.Success then
            broadcastWindow.Width <- float wh.Groups.[1].Value
            broadcastWindow.Height <- float wh.Groups.[2].Value
        let saveSizeTimer = new System.Windows.Threading.DispatcherTimer(Interval=System.TimeSpan.FromMilliseconds(600.))
        saveSizeTimer.Tick.Add(fun _ ->
            saveSizeTimer.Stop()
            if broadcastWindow.WindowState = WindowState.Normal then
                TrackerModelOptions.BroadcastWindowWH <- sprintf "%d,%d" (int broadcastWindow.Width) (int broadcastWindow.Height)
                TrackerModelOptions.writeSettings())
        broadcastWindow.SizeChanged.Add(fun _ -> 
            updateTopBar()
            saveSizeTimer.Stop()
            saveSizeTimer.Start())
        
        let mutable isUpper = true
        cm.RootCanvas.MouseMove.Add(fun ea ->   // we need RootCanvas to see mouse moving in popups
            let mousePos = ea.GetPosition(appMainCanvas)
            if mousePos.Y < THRU_MAIN_MAP_AND_ITEM_PROGRESS_H then
                if not isUpper then
                    if cm.PopupCanvasStack.Count=0 then  // don't switch panes if a popup is active
                        isUpper <- true
                        dp.Children.RemoveAt(1)
                        dp.Children.Add(topc) |> ignore
                Canvas.SetLeft(fakeTopMouse, mousePos.X)
                Canvas.SetTop(fakeTopMouse, mousePos.Y)
            else
                if isUpper then
                    if cm.PopupCanvasStack.Count=0 then  // don't switch panes if a popup is active
                        isUpper <- false
                        dp.Children.RemoveAt(1)
                        dp.Children.Add(bottomc) |> ignore
                Canvas.SetLeft(fakeBottomMouse, mousePos.X)
                Canvas.SetTop(fakeBottomMouse, mousePos.Y - THRU_MAIN_MAP_AND_ITEM_PROGRESS_H + 230.)
            )
        broadcastWindow
    let mutable broadcastWindow = null
    if TrackerModelOptions.ShowBroadcastWindow.Value then
        broadcastWindow <- makeBroadcastWindow(3, TrackerModelOptions.BroadcastWindowIncludesOverworldMagnifier.Value)
        broadcastWindow.Show()
        refocusMainWindow()
    OptionsMenu.broadcastWindowOptionChanged.Publish.Add(fun () ->
        // close existing
        if broadcastWindow<>null then
            broadcastWindow.Close()
            broadcastWindow <- null
        // maybe restart
        if TrackerModelOptions.ShowBroadcastWindow.Value then
            broadcastWindow <- makeBroadcastWindow(3, TrackerModelOptions.BroadcastWindowIncludesOverworldMagnifier.Value)
            broadcastWindow.Show()
            refocusMainWindow()
        )

