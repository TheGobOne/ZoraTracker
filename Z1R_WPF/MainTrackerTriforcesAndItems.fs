module MainTrackerTriforcesAndItems

open System
open System.Windows
open System.Windows.Controls 
open System.Windows.Media

open DungeonUI.AhhGlobalVariables
open HotKeys.MyKey
open OverworldItemGridUI
open CustomComboBoxes.GlobalFlag

let makeGhostBusterImpl(color,isForLinkRouting) =  // for marking off the third box of completed 2-item dungeons in Hidden Dungeon Numbers
    let c = new Canvas(Width=30., Height=30., Opacity=0.0, IsHitTestVisible=false)
    let circle = new Shapes.Ellipse(Width=30., Height=30., StrokeThickness=3., Stroke=color)
    let slash = new Shapes.Line(X1=30.*(1.-0.707), X2=30.*0.707, Y1=30.*0.707, Y2=30.*(1.-0.707), StrokeThickness=3., Stroke=color)
    canvasAdd(c, circle, 0., 0.)
    canvasAdd(c, slash, 0., 0.)
    if isForLinkRouting then
        let txt = new TextBox(FontSize=10., Foreground=color, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, BorderThickness=Thickness(0.), Text="None\nfound")
        Canvas.SetBottom(txt,30.)
        Canvas.SetLeft(txt,0.)
        c.Children.Add(txt) |> ignore
    c
let makeGhostBuster() = makeGhostBusterImpl(Brushes.Gray, false)
let mainTrackerGhostbusters = Array.init 8 (fun _ -> makeGhostBuster())
let updateGhostBusters() =
    if TrackerModel.IsHiddenDungeonNumbers() then
        for i = 0 to 7 do
            let lc = TrackerModel.GetDungeon(i).LabelChar
            let twoItemDungeons = if TrackerModel.IsSecondQuestDungeons then "123567" else "234567"
            if twoItemDungeons.Contains(lc.ToString()) then
                mainTrackerGhostbusters.[i].Opacity <- 1.0
                let boxes = TrackerModel.GetDungeon(i).Boxes
                if boxes.[1].PlayerHas() = TrackerModel.PlayerHas.NO && boxes.[1].CellCurrent() = -1 &&            // middle box empty
                    (boxes.[2].PlayerHas() <> TrackerModel.PlayerHas.NO || boxes.[2].CellCurrent() <> -1) then     // bottom box non-empty
                    // swap the two on behalf of the player
                    let v = boxes.[2].CellCurrent()
                    let ph = boxes.[2].PlayerHas()
                    if not(boxes.[2].AttemptToSet(-1, TrackerModel.PlayerHas.NO)) then
                        failwith "impossible"
                    if not(boxes.[1].AttemptToSet(v,ph)) then
                        failwith "impossible"
            else
                mainTrackerGhostbusters.[i].Opacity <- 0.0

let triforceInnerCanvases = Array.zeroCreate 8
let mainTrackerCanvases : Canvas[,] = Array2D.zeroCreate 9 5
let mainTrackerCanvasShaders : Canvas[,] = Array2D.init 8 5 (fun _i j -> new Canvas(Width=30., Height=30., Background=Brushes.Black, Opacity=(if j=1 then 0.4 else 0.3), IsHitTestVisible=false))
let timelineItems = ResizeArray()
let doUIUpdateEvent = new Event<unit>()

// the 'Item Locations' window: a read-only mirror of the dungeon columns (triforce and item boxes of levels 1-9), and below them the armos / letter / ladder / white sword / magical sword boxes
let mutable itemLocationsWindow : Window = null
let ITEMLOC_MINIMIZED = ShowRunCustom.MINIMIZED_THRESHOLD
let itemLocationsRegex = """^(1|0),(-?\d+),(-?\d+)(?:,(\d+),(\d+))?$"""
// userClick=true: the button was pressed; false: automatic reopen at startup (only if the window was left open last time)
let openItemLocationsWindowImpl(userClick) =
    let m = System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.ItemLocationsWindow_DisplayedLT, itemLocationsRegex)
    let validPos = m.Success && not(float m.Groups.[2].Value < ITEMLOC_MINIMIZED)
    if itemLocationsWindow <> null then
        itemLocationsWindow.Activate() |> ignore
    elif userClick || (validPos && m.Groups.[1].Value = "1") then
        let w = new Window(Title="Item Locations", ResizeMode=ResizeMode.CanResizeWithGrip, SizeToContent=SizeToContent.WidthAndHeight, Background=Brushes.Black, ShowActivated=false)
        let content = new StackPanel(Orientation=Orientation.Vertical, Background=Brushes.Black, Margin=Thickness(6.))
        let box30(view:UIElement) =
            let c = new Canvas(Width=30., Height=30.)
            c.Children.Add(view) |> ignore
            c
        let build() =
            content.Children.Clear()
            let row1 = new StackPanel(Orientation=Orientation.Horizontal)
            for i = 0 to 8 do
                let d = TrackerModel.GetDungeon(i)
                let col = new StackPanel(Orientation=Orientation.Vertical, Margin=Thickness(3.,0.,3.,0.))
                if TrackerModel.IsHiddenDungeonNumbers() then
                    col.Children.Add(Views.MakeColorNumberCanvasForHDN(i)) |> ignore
                col.Children.Add(if i < 8 then Views.MakeTriforceDisplayView(None, i, None) else Views.MakeLevel9View(None)) |> ignore
                for box in d.Boxes do
                    col.Children.Add(box30(fst(Views.MakeBoxItemWithExtraDecorations(None, box, false, None)))) |> ignore
                if i = 1 && not(TrackerModel.IsHiddenDungeonNumbers()) && TrackerModel.level2StairBoxEnabled then
                    col.Children.Add(box30(fst(Views.MakeBoxItemWithExtraDecorations(None, TrackerModel.level2StairBox, false, None)))) |> ignore
                if i = 8 && TrackerModel.level9FloorBoxEnabled then
                    col.Children.Add(box30(fst(Views.MakeBoxItemWithExtraDecorations(None, d.ExtraFloorBox, false, None)))) |> ignore
                row1.Children.Add(col) |> ignore
            content.Children.Add(row1) |> ignore
            let row2 = new StackPanel(Orientation=Orientation.Horizontal, Margin=Thickness(0.,12.,0.,0.))
            // (each box has its icon below it, as on the main tracker)
            let letterBmp = CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.LETTER, None)
            for box, icon in [| TrackerModel.armosBox, Graphics.ow_key_armos_bmp; TrackerModel.letterBox, letterBmp; TrackerModel.ladderBox, Graphics.ladder_bmp
                                TrackerModel.sword2Box, Graphics.ow_sword2_item_bmp; TrackerModel.sword3Box, Graphics.ow_sword3_item_bmp |] do
                let col = new StackPanel(Orientation=Orientation.Vertical, Margin=Thickness(3.,0.,3.,0.))
                col.Children.Add(box30(fst(Views.MakeBoxItemWithExtraDecorations(None, box, false, None)))) |> ignore
                let ic = new Canvas(Width=30., Height=30.)
                canvasAdd(ic, Graphics.BMPtoImage icon, 4., 4.)
                col.Children.Add(ic) |> ignore
                row2.Children.Add(col) |> ignore
            content.Children.Add(row2) |> ignore
        build()
        let vb = new Viewbox(Child=content, Stretch=Stretch.Uniform, StretchDirection=StretchDirection.DownOnly)
        w.Content <- vb
        // remembered size and position from the last session
        let savedW, savedH = (if m.Success && m.Groups.[4].Success && m.Groups.[5].Success && float m.Groups.[4].Value >= 100. && float m.Groups.[5].Value >= 60. then (float m.Groups.[4].Value, float m.Groups.[5].Value) else (0., 0.))
        if savedW > 0. then
            w.SizeToContent <- SizeToContent.Manual
            w.Width <- savedW
            w.Height <- savedH
        if validPos then
            w.WindowStartupLocation <- WindowStartupLocation.Manual
            w.Left <- float m.Groups.[2].Value
            w.Top <- float m.Groups.[3].Value
        let mainClosing = ref false
        let save(isOpen) =
            let bounds = if w.Left < ITEMLOC_MINIMIZED then w.RestoreBounds else Rect(w.Left, w.Top, w.ActualWidth, w.ActualHeight)
            TrackerModelOptions.ItemLocationsWindow_DisplayedLT <- sprintf "%d,%d,%d,%d,%d" (if isOpen then 1 else 0) (int bounds.Left) (int bounds.Top) (int bounds.Width) (int bounds.Height)
            TrackerModelOptions.writeSettings()
        let saveTimer = new System.Windows.Threading.DispatcherTimer(Interval=System.TimeSpan.FromMilliseconds(600.))
        saveTimer.Tick.Add(fun _ -> saveTimer.Stop(); save(true))
        w.LocationChanged.Add(fun _ -> if w.IsLoaded then (saveTimer.Stop(); saveTimer.Start()))
        w.SizeChanged.Add(fun _ -> if w.IsLoaded && w.SizeToContent = SizeToContent.Manual then (saveTimer.Stop(); saveTimer.Start()))
        // opens at the content's natural size (unless a size was remembered), then the content scales as the window is resized
        w.ContentRendered.Add(fun _ ->
            if w.SizeToContent <> SizeToContent.Manual then
                let aw, ah = w.ActualWidth, w.ActualHeight
                w.SizeToContent <- SizeToContent.Manual
                w.Width <- aw
                w.Height <- ah
            vb.StretchDirection <- StretchDirection.Both)   // (also when a remembered size was restored, otherwise it could never scale up)
        // the item-slot options that add boxes can change while the window is open
        TrackerModel.level2StairBoxEnabledChanged.Publish.Add(fun _ -> if itemLocationsWindow <> null then build())
        TrackerModel.level9FloorBoxEnabledChanged.Publish.Add(fun _ -> if itemLocationsWindow <> null then build())
        Application.Current.MainWindow.Closed.Add(fun _ -> mainClosing.Value <- true; try w.Close() with _ -> ())   // (stays 'open' in the settings, so it reappears next time)
        w.Closed.Add(fun _ ->
            itemLocationsWindow <- null
            saveTimer.Stop()
            if not mainClosing.Value then save(false))   // the user closed it themselves: don't reopen it next time
        itemLocationsWindow <- w
        w.Show()
        if userClick then save(true)

let openItemLocationsWindow() = openItemLocationsWindowImpl(true)
// called at startup (after the tracker is set up): reopens the window if it was left open when the program last closed
let reopenItemLocationsWindowIfWasOpen() = openItemLocationsWindowImpl(false)

// a popout window that mirrors the Item Progress bar (a live picture of the bar; set 'itemProgressSource' to the bar's canvas)
let mutable itemProgressSource : FrameworkElement = null
let mutable itemProgressWindow : Window = null
let openItemProgressWindowImpl(userClick) =
    let m = System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.ItemProgressWindow_DisplayedLT, itemLocationsRegex)
    let validPos = m.Success && not(float m.Groups.[2].Value < ITEMLOC_MINIMIZED)
    if itemProgressSource = null then ()
    elif itemProgressWindow <> null then
        itemProgressWindow.Activate() |> ignore
    elif userClick || (validPos && m.Groups.[1].Value = "1") then
        let itemsW, itemsH = 15.*30., 30.
        // (a picture of the whole bar, shifted left so only the item slots show; the plain whole-visual brush is the reliable way to mirror a live element)
        // a brush maps the visual's *content bounds* onto the picture, so give the bar an invisible full-size child: then the bounds are always exactly the whole bar (1:1), however many items are showing
        (match itemProgressSource with
         | :? Canvas as c when not(c.Tag = box "mirrorPad") ->
            c.Tag <- box "mirrorPad"
            c.Children.Add(new Shapes.Rectangle(Width=c.Width, Height=c.Height, Fill=Brushes.Transparent, IsHitTestVisible=false)) |> ignore
         | _ -> ())
        let full = new Shapes.Rectangle(Width=itemProgressSource.Width, Height=itemsH, IsHitTestVisible=false, Fill=new VisualBrush(itemProgressSource))
        let rect = new Canvas(Width=itemsW, Height=itemsH, ClipToBounds=true, Background=Brushes.Black, IsHitTestVisible=false)
        Canvas.SetLeft(full, -OverworldItemGridUI.ITEM_PROGRESS_FIRST_ITEM + 6.)   // (+6 so the first icon isn't clipped at the left edge)
        rect.Children.Add(full) |> ignore
        let vb = new Viewbox(Child=rect, Stretch=Stretch.Uniform, StretchDirection=StretchDirection.DownOnly)
        let w = new Window(Title="Item Progress", ResizeMode=ResizeMode.CanResizeWithGrip, SizeToContent=SizeToContent.WidthAndHeight, Background=Brushes.Black, ShowActivated=false, Content=vb)
        let savedW, savedH = (if m.Success && m.Groups.[4].Success && m.Groups.[5].Success && float m.Groups.[4].Value >= 100. && float m.Groups.[5].Value >= 30. then (float m.Groups.[4].Value, float m.Groups.[5].Value) else (0., 0.))
        if savedW > 0. then
            w.SizeToContent <- SizeToContent.Manual
            w.Width <- savedW
            w.Height <- savedH
        if validPos then
            w.WindowStartupLocation <- WindowStartupLocation.Manual
            w.Left <- float m.Groups.[2].Value
            w.Top <- float m.Groups.[3].Value
        let mainClosing = ref false
        let save(isOpen) =
            let bounds = if w.Left < ITEMLOC_MINIMIZED then w.RestoreBounds else Rect(w.Left, w.Top, w.ActualWidth, w.ActualHeight)
            TrackerModelOptions.ItemProgressWindow_DisplayedLT <- sprintf "%d,%d,%d,%d,%d" (if isOpen then 1 else 0) (int bounds.Left) (int bounds.Top) (int bounds.Width) (int bounds.Height)
            TrackerModelOptions.writeSettings()
        let saveTimer = new System.Windows.Threading.DispatcherTimer(Interval=System.TimeSpan.FromMilliseconds(600.))
        saveTimer.Tick.Add(fun _ -> saveTimer.Stop(); save(true))
        w.LocationChanged.Add(fun _ -> if w.IsLoaded then (saveTimer.Stop(); saveTimer.Start()))
        w.SizeChanged.Add(fun _ -> if w.IsLoaded && w.SizeToContent = SizeToContent.Manual then (saveTimer.Stop(); saveTimer.Start()))
        w.ContentRendered.Add(fun _ ->
            if w.SizeToContent <> SizeToContent.Manual then
                let aw, ah = w.ActualWidth, w.ActualHeight
                w.SizeToContent <- SizeToContent.Manual
                w.Width <- aw
                w.Height <- ah
            vb.StretchDirection <- StretchDirection.Both)
        Application.Current.MainWindow.Closed.Add(fun _ -> mainClosing.Value <- true; try w.Close() with _ -> ())   // (stays 'open' in the settings, so it reappears next time)
        w.Closed.Add(fun _ ->
            itemProgressWindow <- null
            saveTimer.Stop()
            if not mainClosing.Value then save(false))   // the user closed it themselves: don't reopen it next time
        itemProgressWindow <- w
        w.Show()
        if userClick then save(true)
let openItemProgressWindow() = openItemProgressWindowImpl(true)
let reopenItemProgressWindowIfWasOpen() = openItemProgressWindowImpl(false)

let setup(cm:CustomComboBoxes.CanvasManager, owInstance:OverworldData.OverworldInstance, layout:Layout.IApplicationLayoutBase, kind:TrackerModel.DungeonTrackerInstanceKind) =
    let mainTrackerGrid = makeGrid(9, 5, 30, 30)
    let mainTrackerCanvas = new Canvas()
    mainTrackerCanvas.Children.Add(mainTrackerGrid) |> ignore
    layout.AddMainTracker(mainTrackerCanvas)

    // items (we draw these before drawing triforces, as triforce display can draw slightly atop the item boxes, when there's a triforce-specific-blocker drawn)
    let boxActivators = new System.Collections.Generic.Dictionary<TrackerModel.Box, (unit -> unit)>(HashIdentity.Reference)
    let boxItemImpl(tid:Timeline.TimelineID, box:TrackerModel.Box, requiresForceUpdate) = 
        let c,activate = Views.MakeBoxItem(cm, box)
        boxActivators.[box] <- (fun () -> activate(0, CustomComboBoxes.WarpToCenter))
        box.Changed.Add(fun _ -> if requiresForceUpdate then TrackerModel.forceUpdate())
        // an item box (coast, dungeon, etc.) holding one of these items and marked as obtained also checks off that item's box in the upper overworld item grid (and un-checks it again if removed)
        let gridLinks = 
            [ TrackerModel.ITEMS.WOODARROW,    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodArrow
              TrackerModel.ITEMS.BLUERING,     TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueRing
              TrackerModel.ITEMS.BLUECANDLE,   TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle
              TrackerModel.ITEMS.WOODSWORD,    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword
              TrackerModel.ITEMS.MAGICALSWORD, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword ]
        let linkedItemId() = 
            if box.PlayerHas() = TrackerModel.PlayerHas.YES then
                match gridLinks |> List.tryFind (fun (id,_) -> id = box.CellCurrent()) with
                | Some(id,_) -> id
                | None -> -1
            else -1
        let wasLinkedItemId = ref (linkedItemId())
        let weCheckedIt = ref false   // true only if this box itself checked the grid item (it was not already collected, e.g. by hand), so that is the only case we un-check it, and the timeline never gets a second stamp
        box.Changed.Add(fun _ ->
            let now = linkedItemId()
            let was = wasLinkedItemId.Value
            if now <> was then
                wasLinkedItemId.Value <- now
                if was >= 0 && weCheckedIt.Value && TrackerModel.mapCollectedCount.[was] = 0 then
                    for (id,prop) in gridLinks do
                        if id = was then prop.Set(false)
                if was >= 0 then weCheckedIt.Value <- false
                // one timeline entry per item: if the item was already collected on its own, this box adds none; if this box collected it, the grid item adds none
                let linkedTimelineName(id) = 
                    if id = TrackerModel.ITEMS.WOODARROW then "WoodArrow" elif id = TrackerModel.ITEMS.BLUERING then "BlueRing" elif id = TrackerModel.ITEMS.BLUECANDLE then "BlueCandle"
                    elif id = TrackerModel.ITEMS.WOODSWORD then "WoodSword" else "MagicalSword"
                TrackerModel.timelineSuppressedBoxes.Remove(box) |> ignore
                if now >= 0 then
                    for (id,prop) in gridLinks do
                        if id = now then
                            if not(prop.Value()) then
                                prop.Set(true)
                                weCheckedIt.Value <- true
                                TrackerModel.TimelineItemModel.UnstampByIdentifier(linkedTimelineName(id))   // (this box's own entry stays)
                            elif not TrackerModel.ProgressiveItems then   // (Progressive Items: a second copy is an upgrade, so this box keeps its own entry)
                                TrackerModel.timelineSuppressedBoxes.Add(box) |> ignore
                                TrackerModel.TimelineItemModel.UnstampByIdentifier(tid.Identifier)   // (the item's own entry stays)
                TrackerModel.recomputePlayerStateSummary())
        c.MouseEnter.Add(fun _ -> 
            match box.CellCurrent() with
            | 3 -> showLocatorInstanceFunc(TrackerModel.owInstance.PowerBraceletable)
            | 4 -> showLocatorInstanceFunc(TrackerModel.owInstance.Ladderable)
            | 7 -> showLocatorInstanceFunc(TrackerModel.owInstance.Raftable)
            | 8 -> showLocatorInstanceFunc(TrackerModel.owInstance.Whistleable)
            | 9 -> showLocatorInstanceFunc(TrackerModel.owInstance.Burnable)
            | _ -> ()
            )
        c.MouseLeave.Add(fun _ -> hideLocator())
        // putting a triforce piece in a dungeon's item box also checks off that dungeon's triforce, same as clicking its numbered triforce above
        // (the 1-or-4 third box is re-evaluated on every change, since toggling the dungeon quest moves it between dungeon 1 and dungeon 4)
        let dungeonIndexFnOpt : (unit -> int) option =
            match tid with
            | Timeline.TimelineID.LevelBox(level,_) when level <= 8 -> Some(fun () -> level-1)
            | Timeline.TimelineID.Level1or4Box3 -> Some(fun () -> if TrackerModel.IsSecondQuestDungeons then 3 else 0)
            | _ -> None
        match dungeonIndexFnOpt with
        | Some(dungeonIndexFn) ->
            // a triforce piece that is marked as obtained (green) checks the dungeon's triforce; changing/removing that item, or marking it not-obtained, unchecks it again
            let isObtainedTriforcePiece() = box.CellCurrent() = TrackerModel.ITEMS.TRIFORCE && box.PlayerHas() = TrackerModel.PlayerHas.YES
            let wasObtainedTriforcePiece = ref (isObtainedTriforcePiece())   // ref cell, since closures can't capture mutable locals
            let dungeonIndexWhenMarked = ref (dungeonIndexFn())                // which dungeon we checked off, so un-marking unchecks that same one
            box.Changed.Add(fun _ -> 
                let now = isObtainedTriforcePiece()
                if now && not wasObtainedTriforcePiece.Value then
                    dungeonIndexWhenMarked.Value <- dungeonIndexFn()
                    let dungeon = TrackerModel.GetDungeon(dungeonIndexWhenMarked.Value)
                    if not(dungeon.PlayerHasTriforce()) then
                        dungeon.ToggleTriforce()
                elif wasObtainedTriforcePiece.Value && not now then
                    let dungeon = TrackerModel.GetDungeon(dungeonIndexWhenMarked.Value)
                    if dungeon.PlayerHasTriforce() then
                        dungeon.ToggleTriforce()
                wasObtainedTriforcePiece.Value <- now)
        | None -> ()
        timelineItems.Add(new Timeline.TimelineItem(tid, fun()->
            // Progressive Items: a collected upgrade shows the tier it was collected at (e.g. the red ring)
            let f = TrackerModel.progressiveFamily(box.CellCurrent())
            match (if f > 0 && box.PlayerHas() = TrackerModel.PlayerHas.YES then TrackerModel.progressiveBoxTier.TryGetValue(box) else (false, 0)) with
            | true, v -> (if f = 1 then Graphics.ow_progressive_tier_item_bmps.[min 4 (max 1 v)] else CustomComboBoxes.boxCurrentBMP(TrackerModel.progressiveFamilyIdent(f, min 2 (max 1 v)), None))
            | _ -> CustomComboBoxes.boxCurrentBMP(box.CellCurrent(), Some(tid))))
        c
    if TrackerModel.IsHiddenDungeonNumbers() then
        for i = 0 to 8 do
            for j = 0 to 2 do
                let c = new Canvas(Width=30., Height=30., Background=Brushes.Black)
                gridAdd(mainTrackerGrid, c, i, j+2)
                if j<>2 || i <> 8 then   // dungeon 9 does not have 3 items
                    canvasAdd(c, boxItemImpl(Timeline.TimelineID.LevelBox(i+1, j+1), TrackerModel.GetDungeon(i).Boxes.[j], false), 0., 0.)
                mainTrackerCanvases.[i,j+2] <- c
                if j=2 && i<> 8 then
                    canvasAdd(c, mainTrackerGhostbusters.[i], 0., 0.)
    else
        for i = 0 to 8 do
            for j = 0 to 2 do
                let c = new Canvas(Width=30., Height=30., Background=System.Windows.Media.Brushes.Black)
                gridAdd(mainTrackerGrid, c, i, j+2)
                if j=0 || j=1 || i=7 then
                    canvasAdd(c, boxItemImpl(Timeline.TimelineID.LevelBox(i+1, j+1), TrackerModel.GetDungeon(i).Boxes.[j], false), 0., 0.)
                mainTrackerCanvases.[i,j+2] <- c
    // optional staircase item slot under level 2: starts as a gray box (like the dungeon-quest toggle under level 4); click to turn it into a staircase item slot,
    // middle-click the slot to hide it again (which also clears whatever item was in it)
    if not(TrackerModel.IsHiddenDungeonNumbers()) then
        let l2c = mainTrackerCanvases.[1,4]
        let placeholder = new Canvas(Width=30., Height=30., Background=Graphics.freeze(new SolidColorBrush(Color.FromRgb(55uy,55uy,85uy))))
        placeholder.ToolTip <- "Optional extra staircase item slot for level 2 (click to add it; middle-click it afterwards to remove it)"
        Views.appMainCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(placeholder)
        let slot = boxItemImpl(Timeline.TimelineID.LevelBox(2,3), TrackerModel.level2StairBox, false)
        slot.ToolTip <- "Optional extra staircase item slot (middle-click to remove it and clear its item)"
        let refreshL2Slot() =
            l2c.Children.Remove(placeholder)
            l2c.Children.Remove(slot)
            if TrackerModel.level9FloorBoxEnabled || (not TrackerModel.level2StairBoxEnabled && not TrackerModelOptions.FourthSwordEnabled) then ()   // no 4th sword option: no extra slot. The 4th sword is in level 9: no level 2 staircase slot at all (not even the greyed-out placeholder)
            elif TrackerModel.level2StairBoxEnabled then l2c.Children.Insert(0, slot) else l2c.Children.Insert(0, placeholder)
        placeholder.MouseDown.Add(fun ea ->
            if ea.ChangedButton = Input.MouseButton.Left then
                ea.Handled <- true
                TrackerModel.SetLevel2StairBoxEnabled(true))
        // tunneling handler on the container so it wins over the box's own middle-click behavior (which sets 'skipped')
        l2c.PreviewMouseDown.Add(fun ea ->
            if TrackerModel.level2StairBoxEnabled && ea.ChangedButton = Input.MouseButton.Middle && ea.ButtonState = Input.MouseButtonState.Pressed && not popupIsActive then
                ea.Handled <- true
                TrackerModel.level2StairBox.Set(-1, TrackerModel.PlayerHas.NO)
                TrackerModel.SetLevel2StairBoxEnabled(false)
                TrackerModel.forceUpdate())
        TrackerModel.level2StairBoxEnabledChanged.Publish.Add(fun _ -> refreshL2Slot())
        TrackerModel.level9FloorBoxEnabledChanged.Publish.Add(fun _ -> refreshL2Slot())
        refreshL2Slot()
    let extrasImage = Graphics.BMPtoImage Graphics.iconExtras_bmp
    extrasImage.ToolTip <- "Starting items and extra drops"
    ToolTipService.SetPlacement(extrasImage, System.Windows.Controls.Primitives.PlacementMode.Top)
    gridAdd(mainTrackerGrid, extrasImage, 8, 4)
    // hovering the button turns the blue part of the icon gold
    let extrasNormalSource = extrasImage.Source
    let extrasGoldSource = 
        let gold = Graphics.transformColor(Graphics.iconExtras_bmp, (fun c ->
            if c.A > 0uy && c.B > c.R then
                let v = int c.B
                System.Drawing.Color.FromArgb(int c.A, v, int (float v * 0.84), 0)
            else c))
        (Graphics.BMPtoImage gold).Source
    extrasImage.MouseEnter.Add(fun _ -> extrasImage.Source <- extrasGoldSource)
    extrasImage.MouseLeave.Add(fun _ -> extrasImage.Source <- extrasNormalSource)
    let IDEAL = Point(Views.IDEAL_BOX_MOUSE_X, Views.IDEAL_BOX_MOUSE_Y)
    extrasImage.MyKeyAdd(fun ea ->
        match HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key) with
        | Some(HotKeys.GlobalHotkeyTargets.MoveCursorLeft) -> 
            ea.Handled <- true
            Graphics.NavigationallyWarpMouseCursorTo(mainTrackerCanvases.[7,4].TranslatePoint(IDEAL,cm.AppMainCanvas))
        | Some(HotKeys.GlobalHotkeyTargets.MoveCursorUp) -> 
            ea.Handled <- true
            Graphics.NavigationallyWarpMouseCursorTo(mainTrackerCanvases.[8,3].TranslatePoint(IDEAL,cm.AppMainCanvas))
        | _ -> ())
    // (no hover box on the extras icon: the turquoise highlight was removed here)
    let finalCanvasOf1Or4 = 
        if TrackerModel.IsHiddenDungeonNumbers() then
            null
        else        
            boxItemImpl(Timeline.TimelineID.Level1or4Box3, TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.FinalBoxOf1Or4, false)
    let toggleSecondQuestDungeonCanvas =
        if TrackerModel.IsHiddenDungeonNumbers() then
            null
        else        
            let c = new Canvas(Width=30., Height=30., Background=Graphics.freeze(new SolidColorBrush(Color.FromRgb(55uy,55uy,85uy))))
            let rect = new System.Windows.Shapes.Rectangle(Width=30., Height=30., Stroke=Brushes.White, StrokeThickness=3.0, Opacity=0.0)
            c.Children.Add(rect) |> ignore
            let pf = new PathFigure(Point(0.,20.), [new BezierSegment(Point(0.,10.), Point(60.,10.), Point(60.,20.), true)], false)
            let curve = new Shapes.Path(Stroke=Brushes.White, StrokeThickness=3., IsHitTestVisible=false, Data=new PathGeometry([pf]), Opacity=0.0)
            let tb1 = new TextBox(IsHitTestVisible=false, BorderThickness=Thickness(0.), FontSize=9., Margin=Thickness(0.),
                                    VerticalContentAlignment=VerticalAlignment.Center, HorizontalContentAlignment=HorizontalAlignment.Center, 
                                    Text="click to toggle", Foreground=Brushes.White, Background=Brushes.Black, Opacity=0.0)
            let tb2 = new TextBox(IsHitTestVisible=false, BorderThickness=Thickness(0.), FontSize=9., Margin=Thickness(0.),
                                    VerticalContentAlignment=VerticalAlignment.Center, HorizontalContentAlignment=HorizontalAlignment.Center, 
                                    Text="dungeon quest", Foreground=Brushes.White, Background=Brushes.Black, Opacity=0.0)
            let explainer = new TextBox(IsHitTestVisible=false, BorderThickness=Thickness(3.), BorderBrush=Brushes.Gray, FontSize=14., Margin=Thickness(0.),
                                        Text="Usually dungeon 1 has three items and dungeon 4 has two.\n"+
                                        "However if you are playing with the 'second quest dungeons' flag\n"+
                                        "then dungeon 4 gets a third item and dungeon 1 will only have 2.\n"+
                                        "This toggle is used to decide which dungeon (1 or 4) gets the third box.", 
                                        Padding=Thickness(3.), Foreground=Brushes.Orange, Background=Brushes.Black)
            canvasAdd(mainTrackerCanvas, curve, 30., 118.)
            canvasAdd(mainTrackerCanvas, tb1, 30., 115.)
            canvasAdd(mainTrackerCanvas, tb2, 28., 137.)
            c.MouseEnter.Add(fun _ ->
                rect.Opacity <- 1.0
                curve.Opacity <- 1.0
                tb1.Opacity <- 1.0
                tb2.Opacity <- 1.0
                layout.AddTopLayerHover(explainer, 140., 54.)
                )
            c.MouseLeave.Add(fun _ ->
                rect.Opacity <- 0.0
                curve.Opacity <- 0.0
                tb1.Opacity <- 0.0
                tb2.Opacity <- 0.0
                layout.ClearTopLayerHovers()
                )
            c.MouseDown.Add(fun _ ->
                TrackerModel.IsSecondQuestDungeons <- not TrackerModel.IsSecondQuestDungeons
                rect.Opacity <- 0.0
                curve.Opacity <- 0.0
                tb1.Opacity <- 0.0
                tb2.Opacity <- 0.0
                layout.ClearTopLayerHovers()
                OptionsMenu.secondQuestDungeonsOptionChanged.Trigger()
                )
            c
    // hovering a dungeon's hint box (the two-letter 'zora hint' above its triforce) shows the hint 3x larger in a popup below it
    let addHintZoomOnHover(fe:FrameworkElement, i:int) =
        fe.MouseEnter.Add(fun _ ->
            let hz = TrackerModel.GetLevelHint(i)
            if hz <> TrackerModel.HintZone.UNKNOWN && not popupIsActive then
                let inner = OverworldItemGridUI.HintZoneDisplayTextBox(hz.AsDisplayTwoChars())
                let vb = new Viewbox(Width=72., Height=72., Stretch=Stretch.Uniform, Child=inner)
                let b = new Border(BorderBrush=OverworldItemGridUI.hintyBrush, BorderThickness=Thickness(3.), Background=Brushes.Black, Child=vb, IsHitTestVisible=false)
                layout.ClearTopLayerHovers()
                layout.AddTopLayerHover(b, max 0. (float(30*i) - 24.), 34.)
            )
        fe.MouseLeave.Add(fun _ -> layout.ClearTopLayerHovers())
        fe.MouseDown.Add(fun _ -> layout.ClearTopLayerHovers())
    // numbered triforce display - the extra row of triforce in IsHiddenDungeonNumbers
    let updateNumberedTriforceDisplayImpl(c:Canvas,i) =
        let hasTriforce, index = TrackerModel.doesPlayerHaveTriforceAndWhichDungeonIndexIsIt(i)
        let found = if index = -1 then false else TrackerModel.GetDungeon(index).HasBeenLocated()
        let hasHint = not(found) && TrackerModel.GetLevelHint(i)<>TrackerModel.HintZone.UNKNOWN
        c.Children.Clear()
        if hasHint then
            c.Children.Add(makeHintHighlight(30.)) |> ignore
        if not hasTriforce then
            if not found then
                c.Children.Add(Graphics.BMPtoImage Graphics.emptyUnfoundNumberedTriforce_bmps.[i]) |> ignore
            else
                c.Children.Add(Graphics.BMPtoImage Graphics.emptyFoundNumberedTriforce_bmps.[i]) |> ignore
        else
            if not found then
                c.Children.Add(Graphics.BMPtoImage Graphics.fullNumberedUnfoundTriforce_bmps.[i]) |> ignore
            else
                c.Children.Add(Graphics.BMPtoImage Graphics.fullNumberedFoundTriforce_bmps.[i]) |> ignore
    let updateNumberedTriforceDisplayIfItExists =
        if false && TrackerModel.IsHiddenDungeonNumbers() then   // (the extra row of numbered triforces above the armos/letter/etc. item boxes is no longer shown with Hide Dungeon Numbers)
            let numberedTriforceCanvases = Array.init 8 (fun _ -> new Canvas(Width=30., Height=30.))
            for i = 0 to 7 do
                let c = numberedTriforceCanvases.[i]
                layout.AddNumberedTriforceCanvas(c, i)
                c.MouseEnter.Add(fun _ -> showLocator(ShowLocatorDescriptor.DungeonNumber i))
                c.MouseLeave.Add(fun _ -> hideLocator())
            let update() =
                for i = 0 to 7 do
                    updateNumberedTriforceDisplayImpl(numberedTriforceCanvases.[i], i)
            update
        else
            fun () -> ()
    updateNumberedTriforceDisplayIfItExists()
    // triforce
    for i = 0 to 7 do
        if TrackerModel.IsHiddenDungeonNumbers() then
            // triforce dungeon color
            let colorCanvas = new Canvas(Width=28., Height=28., Background=Brushes.Black)
            //mainTrackerCanvases.[i,0] <- colorCanvas
            let colorButton = new Button(Width=30., Height=30., BorderThickness=Thickness(1.), Margin=Thickness(0.), Padding=Thickness(0.), BorderBrush=Brushes.DimGray, Content=colorCanvas)
            colorButton.Click.Add(fun _ -> 
                if not popupIsActive && TrackerModel.IsHiddenDungeonNumbers() then
                    popupIsActive <- true
                    let pos = colorButton.TranslatePoint(Point(15., 15.), cm.AppMainCanvas)
                    async {
                        do! Dungeon.HiddenDungeonCustomizerPopup(cm, i, TrackerModel.GetDungeon(i).Color, TrackerModel.GetDungeon(i).LabelChar, false, false, pos)
                        popupIsActive <- false
                        } |> Async.StartImmediate
                )
            gridAdd(mainTrackerGrid, colorButton, i, 0)
            let dungeon = TrackerModel.GetDungeon(i)
            Dungeon.HotKeyAHiddenDungeonLabel(colorCanvas, dungeon, None)
            dungeon.HiddenDungeonColorOrLabelChanged.Add(fun (color,labelChar) -> 
                colorCanvas.Background <- Graphics.freeze(new SolidColorBrush(Graphics.makeColor(color)))
                colorCanvas.Children.Clear()
                let color = if Graphics.isBlackGoodContrast(color) then System.Drawing.Color.Black else System.Drawing.Color.White
                if TrackerModel.GetDungeon(i).LabelChar <> '?' then  // ? and 7 look alike, and also it is easier to parse 'blank' as unknown/unset dungeon number
                    colorCanvas.Children.Add(Graphics.BMPtoImage(Graphics.alphaNumOnTransparentBmp(labelChar, color, 28, 28, 3, 2))) |> ignore
                )
            colorButton.MouseEnter.Add(fun _ -> showLocator(ShowLocatorDescriptor.DungeonIndex i))
            colorButton.MouseLeave.Add(fun _ -> hideLocator())
        else
            let hintCanvas = new Canvas(Width=30., Height=30., Background=Brushes.Transparent)  // Background to accept mouse input
            OverworldItemGridUI.RedrawHintCanvas(hintCanvas, TrackerModel.GetLevelHint(i))
            TrackerModel.LevelHintChanged(i).Add(fun hz -> OverworldItemGridUI.RedrawHintCanvas(hintCanvas, hz))
            OverworldItemGridUI.ApplyFastHintSelectorBehavior(cm, (float(30*i),0.), hintCanvas, i, true, Some(ShowLocatorDescriptor.DungeonIndex i), false)
            addHintZoomOnHover(hintCanvas, i)
            mainTrackerCanvases.[i,0] <- hintCanvas
            Views.appMainCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(hintCanvas)
            gridAdd(mainTrackerGrid, hintCanvas, i, 0)
        // triforce itself and label
        let c = new Canvas(Width=30., Height=30.)
        mainTrackerCanvases.[i,1] <- c
        let innerc = Views.MakeTriforceDisplayView(Some(cm),i,Some(owInstance))
        triforceInnerCanvases.[i] <- innerc
        Canvas.SetLeft(innerc, -1.)   // nudged 1px left
        c.Children.Add(innerc) |> ignore
        c.MouseEnter.Add(fun _ -> showLocator(ShowLocatorDescriptor.DungeonIndex i))
        c.MouseLeave.Add(fun _ -> hideLocator())
        gridAdd(mainTrackerGrid, c, i, 1)
        timelineItems.Add(new Timeline.TimelineItem(Timeline.TimelineID.Triforce(i+1), fun()->
            match kind with
            | TrackerModel.DungeonTrackerInstanceKind.DEFAULT -> Graphics.fullNumberedFoundTriforce_bmps.[i]
            | TrackerModel.DungeonTrackerInstanceKind.HIDE_DUNGEON_NUMBERS -> 
                if TrackerModel.GetDungeon(i).LabelChar <> '?' then
                    let num = int(TrackerModel.GetDungeon(i).LabelChar) - int '1'
                    Graphics.fullNumberedFoundTriforce_bmps.[num]
                else
                    Graphics.fullLetteredFoundTriforce_bmps.[i]
            ))
    let level9NumeralCanvas = Views.MakeLevel9View(Some(owInstance))
    gridAdd(mainTrackerGrid, level9NumeralCanvas, 8, 1) 
    mainTrackerCanvases.[8,1] <- level9NumeralCanvas
    level9NumeralCanvas.MouseEnter.Add(fun _ -> showLocator(ShowLocatorDescriptor.DungeonIndex 8))
    level9NumeralCanvas.MouseLeave.Add(fun _ -> hideLocator())
    // dungeon 9 doesn't need a color, we display a 'found summary' here instead (except we don't any more)
    let level9ColorCanvas = new Canvas(Width=30., Height=30., Background=Brushes.Black)  
    if not(TrackerModel.IsHiddenDungeonNumbers()) then
        OverworldItemGridUI.RedrawHintCanvas(level9ColorCanvas, TrackerModel.GetLevelHint(8))
        TrackerModel.LevelHintChanged(8).Add(fun hz -> OverworldItemGridUI.RedrawHintCanvas(level9ColorCanvas, hz))
        OverworldItemGridUI.ApplyFastHintSelectorBehavior(cm, (float(30*8), 0.), level9ColorCanvas, 8, true, Some(ShowLocatorDescriptor.DungeonIndex 8), false)
        addHintZoomOnHover(level9ColorCanvas, 8)
        Views.appMainCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(level9ColorCanvas)
    gridAdd(mainTrackerGrid, level9ColorCanvas, 8, 0) 
    mainTrackerCanvases.[8,0] <- level9ColorCanvas
    // optional level 9 floor item check (the '4th Sword' start option can place the 4th sword there instead of the level 2 staircase): it takes the bottom cell under level 9,
    // and the Extras button moves up into the (then hidden) level 9 hint cell while it is shown
    do
        let l9slot = ref (null : Canvas)
        let l9host = ref (null : Canvas)   // its own cell host, placed straight into the grid (row 4 of the level 9 column) on top of everything else there
        let refreshL9Slot() =
            let enabled = TrackerModel.level9FloorBoxEnabled
            if enabled then
                if isNull l9slot.Value then
                    l9slot.Value <- boxItemImpl(Timeline.TimelineID.LevelBox(9,3), TrackerModel.GetDungeon(8).ExtraFloorBox, false)
                    l9host.Value <- new Canvas(Width=30., Height=30., Background=Brushes.Black)
                    l9host.Value.Children.Add(l9slot.Value) |> ignore
                    Panel.SetZIndex(l9host.Value, 50)
                if not(mainTrackerGrid.Children.Contains(l9host.Value)) then
                    gridAdd(mainTrackerGrid, l9host.Value, 8, 4)
            elif not(isNull l9host.Value) then
                mainTrackerGrid.Children.Remove(l9host.Value)
            // (the Extras image is later moved out of this grid into the legend: leave it there, since adding it here again would throw 'already the logical child of another element')
            if isNull extrasImage.Parent || obj.ReferenceEquals(extrasImage.Parent, mainTrackerGrid) then
                mainTrackerGrid.Children.Remove(extrasImage)
                gridAdd(mainTrackerGrid, extrasImage, 8, (if enabled then 0 else 4))
            level9ColorCanvas.Visibility <- (if enabled then Visibility.Hidden else Visibility.Visible)
        TrackerModel.level9FloorBoxEnabledChanged.Publish.Add(fun _ -> refreshL9Slot())
        refreshL9Slot()
    // Level 9 entry rule, in the empty cells of the bottom row (under levels 5-7): "9 ? = [triforce] x8".
    // The default is all 8 triforces; right-click the triforce to pick a different item that opens level 9. Once that item is collected, the go-time reminders play.
    if kind = TrackerModel.DungeonTrackerInstanceKind.DEFAULT then
        let entry = new Canvas(Width=90., Height=30., Background=Brushes.Black, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
        // the 9: just the 9 in its yellow box
        let nine = Graphics.BMPtoImage(Graphics.theInteriorBmpTable.[8].[0])
        nine.Stretch <- Stretch.Uniform
        nine.Width <- 10.
        nine.Height <- 18.
        RenderOptions.SetBitmapScalingMode(nine, BitmapScalingMode.NearestNeighbor)
        let nineHost = new Grid(Width=10., Height=18.)
        nineHost.Children.Add(nine) |> ignore
        canvasAdd(entry, nineHost, 3., 6.)
        let mkText(s:string) = new TextBlock(Text=s, Foreground=Brushes.Orange, FontSize=18., IsHitTestVisible=false)
        let eqText = mkText("=")
        canvasAdd(entry, eqText, 17., 3.)
        // the entry requirement: the 8-triforce default, or the item the user chose
        let trigArea = new Canvas(Width=44., Height=30., Background=Brushes.Transparent, Cursor=Input.Cursors.Hand)
        trigArea.ToolTip <- "What it takes to enter level 9 (default: all 8 triforces; scroll to change the number).\nRight-click to choose a different item that opens level 9;\nthe go-time reminders play once it is collected."
        canvasAdd(entry, trigArea, 32., 0.)
        let redrawEntry() =
            trigArea.Children.Clear()
            let item = TrackerModel.level9CustomTriggerItem()
            // center the whole "9 = ..." group in the open space (it is narrower once an item replaces the triforce and "xN")
            let mutable contentW = 50.
            if item = -1 then
                let img = Graphics.BMPtoImage(Graphics.triforce_item_bmp)
                img.Width <- 21.
                img.Height <- 21.
                canvasAdd(trigArea, img, 0., 4.)
                let x8 = new TextBlock(Text=sprintf "x%d" TrackerModel.level9TriforceCount, Foreground=Brushes.Orange, FontSize=14., IsHitTestVisible=false)
                canvasAdd(trigArea, x8, 22., 7.)
                x8.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
                contentW <- 32. + 22. + x8.DesiredSize.Width - 3.   // the real width of the "xN" text
            else
                // (the progressive sword shows the tier it had when picked, not the live tier, so upgrades never change this box)
                let bmp = if item = TrackerModel.ITEMS.PROGRESSIVESWORD then Graphics.ow_progressive_tier_item_bmps.[max 1 (min 4 TrackerModel.level9TriggerSwordTier)] else CustomComboBoxes.boxCurrentBMP(item, None)
                let img = Graphics.BMPtoImage(bmp)
                img.Width <- 21.
                img.Height <- 21.
                canvasAdd(trigArea, img, 0., 4.)
            let shift = (90. - contentW) / 2. - 3.
            Canvas.SetLeft(nineHost, 3. + shift)
            Canvas.SetLeft(eqText, 17. + shift)
            Canvas.SetLeft(trigArea, 32. + shift)
        redrawEntry()
        Views.redrawBoxes.Add(redrawEntry)
        TrackerModel.level9TriggerBox.Changed.Add(fun _ -> redrawEntry())
        // scrolling the triforce (default rule) changes how many triforces are needed
        trigArea.MouseWheel.Add(fun ea ->
            ea.Handled <- true
            if not popupIsActive && TrackerModel.level9CustomTriggerItem() = -1 then
                let n = TrackerModel.level9TriforceCount + (if ea.Delta > 0 then 1 else -1)
                TrackerModel.level9TriforceCount <- max 1 (min 8 n)
                redrawEntry()
                TrackerModel.forceUpdate())
        trigArea.MouseRightButtonDown.Add(fun ea ->
            ea.Handled <- true
            if not popupIsActive then
                let pos = trigArea.TranslatePoint(Point(0.,0.), cm.AppMainCanvas)
                popupIsActive <- true
                async {
                    CustomComboBoxes.allItemsSelectable <- true   // this box is only a note: any item can be chosen
                    try
                        do! OverworldMapTileCustomization.DoLocalItemComboBox(cm, 0, TrackerModel.level9TriggerBox, pos)
                    finally
                        CustomComboBoxes.allItemsSelectable <- false
                    if TrackerModel.level9TriggerBox.CellCurrent() = TrackerModel.ITEMS.PROGRESSIVESWORD then TrackerModel.level9TriggerSwordTier <- max 1 (TrackerModel.progressiveSwordTier())
                    // this spot is only a note of what it takes to enter level 9: it is never itself "collected" (so it counts for nothing)
                    if TrackerModel.level9TriggerBox.PlayerHas() <> TrackerModel.PlayerHas.SKIPPED then
                        TrackerModel.level9TriggerBox.SetPlayerHas(TrackerModel.PlayerHas.SKIPPED)
                        TrackerModel.recomputePlayerStateSummary()
                        TrackerModel.forceUpdate()
                    popupIsActive <- false
                    redrawEntry()
                    } |> Async.StartImmediate)
        gridAdd(mainTrackerGrid, entry, 4, 4)
        Grid.SetColumnSpan(entry, 3)
        // Race Mode: the level 9 entry rule is hidden and this text stands in for it
        let raceText = new Canvas(Width=90., Height=30., Background=Brushes.Black, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
        let raceTB = new TextBlock(Text="Race Mode", FontSize=16., Foreground=Brushes.Orange, IsHitTestVisible=false)
        raceTB.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
        canvasAdd(raceText, raceTB, (90. - raceTB.DesiredSize.Width) / 2., (30. - raceTB.DesiredSize.Height) / 2.)   // centered in the space
        gridAdd(mainTrackerGrid, raceText, 4, 4)
        Grid.SetColumnSpan(raceText, 3)
        let applyRace() =
            let race = TrackerModelOptions.RaceMode.Value
            entry.Visibility <- (if race then Visibility.Collapsed else Visibility.Visible)
            raceText.Visibility <- (if race then Visibility.Visible else Visibility.Collapsed)
        applyRace()
        OptionsMenu.raceModeChanged.Publish.Add(fun () -> applyRace())
    // 'Item Locations' button: the top button of the Save / Hint Decoder / Hotkeys column (the layout sizes and places it)
    do
        // (made with Graphics.makeButton, so it has the same look and border as the other tracker buttons; use that helper for new buttons)
        let btn = Graphics.makeButton("Popout Windows", Some(12.), Some(Brushes.Orange))
        (match btn.Content with | :? TextBox as t -> t.Background <- Brushes.Black | _ -> ())   // same black as the Save / Hint Decoder / Hotkeys buttons
        btn.Focusable <- false
        btn.ToolTip <- "Choose which popout windows to show (they are remembered for next time)"
        btn.Click.Add(fun _ ->
            if not CustomComboBoxes.GlobalFlag.popupIsActive then
                CustomComboBoxes.GlobalFlag.popupIsActive <- true
                let pos = btn.TranslatePoint(Point(0., btn.ActualHeight + 4.), cm.AppMainCanvas)
                async {
                    let wh = new System.Threading.ManualResetEvent(false)
                    let sp = new StackPanel(Orientation=Orientation.Vertical, Margin=Thickness(6.))
                    sp.Children.Add(new TextBox(Text="Popout Windows", IsReadOnly=true, IsHitTestVisible=false, BorderThickness=Thickness(0.), FontSize=16., FontWeight=FontWeights.Bold,
                                                Foreground=Brushes.Orange, Background=Brushes.Black, Margin=Thickness(4.,2.,4.,6.))) |> ignore
                    let entry(text:string, isOpen:unit->bool, openIt:unit->unit, closeIt:unit->unit) =
                        let cb = PieMenus.makeOrangeCheckBox(text, isOpen())
                        cb.Click.Add(fun _ ->
                            if cb.IsChecked = System.Nullable<bool>(true) then openIt() else closeIt()
                            cb.IsChecked <- System.Nullable<bool>(isOpen()))
                        sp.Children.Add(cb) |> ignore
                    entry("Inventory and Max Hearts", (fun () -> Popouts.isPopoutOpen(Popouts.InventoryAndHeartsPopout)), (fun () -> Popouts.openPopout(Popouts.InventoryAndHeartsPopout)), (fun () -> Popouts.closePopout(Popouts.InventoryAndHeartsPopout)))
                    entry("Item Locations", (fun () -> itemLocationsWindow <> null), (fun () -> openItemLocationsWindow()), (fun () -> if itemLocationsWindow <> null then itemLocationsWindow.Close()))
                    entry("Item Progress", (fun () -> itemProgressWindow <> null), (fun () -> openItemProgressWindow()), (fun () -> if itemProgressWindow <> null then itemProgressWindow.Close()))
                    entry("Spot Summary", (fun () -> Popouts.isPopoutOpen(Popouts.SpotSummaryPopout)), (fun () -> Popouts.openPopout(Popouts.SpotSummaryPopout)), (fun () -> Popouts.closePopout(Popouts.SpotSummaryPopout)))
                    entry("Uncollected Items", (fun () -> Popouts.isPopoutOpen(Popouts.RemainingItemsPoput)), (fun () -> Popouts.openPopout(Popouts.RemainingItemsPoput)), (fun () -> Popouts.closePopout(Popouts.RemainingItemsPoput)))
                    sp.Children.Add(new DockPanel(Background=Brushes.Gray, Height=1., Margin=Thickness(6.,6.,6.,4.))) |> ignore
                    let showCustomCb = PieMenus.makeOrangeCheckBox("Show Custom", ShowRunCustom.areShowWindowsOpen())
                    showCustomCb.Click.Add(fun _ ->
                        if showCustomCb.IsChecked = System.Nullable<bool>(true) then ShowRunCustom.DoShowRunCustom(Popouts.refocusMainWindow) else ShowRunCustom.closeShowWindows()
                        showCustomCb.IsChecked <- System.Nullable<bool>(ShowRunCustom.areShowWindowsOpen())
                        TrackerModelOptions.ShowCustomOnStartup <- (showCustomCb.IsChecked = System.Nullable<bool>(true))   // remembered for next time
                        TrackerModelOptions.writeSettings())
                    sp.Children.Add(showCustomCb) |> ignore
                    let b = new Border(Child=sp, Background=Brushes.Black, BorderThickness=Thickness(3.), BorderBrush=Brushes.Gray)
                    do! CustomComboBoxes.DoModal(cm, wh, pos.X, pos.Y, b)
                    CustomComboBoxes.GlobalFlag.popupIsActive <- false
                } |> Async.StartImmediate)
        layout.AddItemLocationsButton(btn)
(*
    let foundDungeonsTB1 = new TextBox(Text="0/9", FontSize=20., Background=Brushes.Black,Foreground=Brushes.Orange,BorderThickness=Thickness(0.0),IsReadOnly=true,IsHitTestVisible=false)
    let foundDungeonsTB2 = new TextBox(Text="found", FontSize=12., Background=Brushes.Black,Foreground=Brushes.Orange,BorderThickness=Thickness(0.0),IsReadOnly=true,IsHitTestVisible=false)
    canvasAdd(level9ColorCanvas, foundDungeonsTB1, 4., -6.)
    canvasAdd(level9ColorCanvas, foundDungeonsTB2, 4., 16.)
*)
    for i = 0 to mainTrackerCanvases.GetLength(0)-1 do
        for j = 0 to mainTrackerCanvases.GetLength(1)-1 do
            if mainTrackerCanvases.[i,j] <> null then
                mainTrackerCanvases.[i,j].MyKeyAdd(fun ea ->
                    match HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key) with
                    | Some(HotKeys.GlobalHotkeyTargets.MoveCursorRight) -> 
                        if i<mainTrackerCanvases.GetLength(0)-1 then
                            ea.Handled <- true
                            Graphics.NavigationallyWarpMouseCursorTo(mainTrackerCanvases.[i+1,j].TranslatePoint(IDEAL,cm.AppMainCanvas))
                        // else unhandled, so app global handler will nudge mouse right
                    | Some(HotKeys.GlobalHotkeyTargets.MoveCursorLeft) -> 
                        ea.Handled <- true
                        if i>0 then
                            Graphics.NavigationallyWarpMouseCursorTo(mainTrackerCanvases.[i-1,j].TranslatePoint(IDEAL,cm.AppMainCanvas))
                    | Some(HotKeys.GlobalHotkeyTargets.MoveCursorUp) -> 
                        ea.Handled <- true
                        if j>0 then
                            Graphics.NavigationallyWarpMouseCursorTo(mainTrackerCanvases.[i,j-1].TranslatePoint(IDEAL,cm.AppMainCanvas))
                    | Some(HotKeys.GlobalHotkeyTargets.MoveCursorDown) -> 
                        if j<4 then
                            ea.Handled <- true
                            Graphics.NavigationallyWarpMouseCursorTo(mainTrackerCanvases.[i,j+1].TranslatePoint(IDEAL,cm.AppMainCanvas))
                        // else unhandled, so app global handler will nudge mouse down
                    | _ -> ()
                )
(*
    let updateFoundDungeonsCount() =
        let mutable r = 0
        for trackerIndex = 0 to 8 do    
            let d = TrackerModel.GetDungeon(trackerIndex)
            if d.HasBeenLocated() then
                r <- r + 1
        foundDungeonsTB1.Text <- sprintf "%d/9" r
    for trackerIndex = 0 to 8 do    
        let d = TrackerModel.GetDungeon(trackerIndex)
        d.HasBeenLocatedChanged.Add(fun _ -> updateFoundDungeonsCount())
*)
    do 
        let RedrawForSecondQuestDungeonToggle() =
            if not(TrackerModel.IsHiddenDungeonNumbers()) then
                mainTrackerCanvases.[0,4].Children.Remove(finalCanvasOf1Or4) |> ignore
                mainTrackerCanvases.[3,4].Children.Remove(finalCanvasOf1Or4) |> ignore
                mainTrackerCanvases.[0,4].Children.Remove(toggleSecondQuestDungeonCanvas) |> ignore
                mainTrackerCanvases.[3,4].Children.Remove(toggleSecondQuestDungeonCanvas) |> ignore
                if TrackerModel.IsSecondQuestDungeons then
                    canvasAdd(mainTrackerCanvases.[3,4], finalCanvasOf1Or4, 0., 0.)
                    canvasAdd(mainTrackerCanvases.[0,4], toggleSecondQuestDungeonCanvas, 0., 0.)
                else
                    canvasAdd(mainTrackerCanvases.[0,4], finalCanvasOf1Or4, 0., 0.)
                    canvasAdd(mainTrackerCanvases.[3,4], toggleSecondQuestDungeonCanvas, 0., 0.)
        RedrawForSecondQuestDungeonToggle()
        // when a Triforce Piece is placed in a dungeon item slot, the numbered triforce (and its click behavior) moves from the dungeon column header into that slot,
        // and the header gets an empty floor item box instead. Right/middle click on the triforce in the slot opens the item menu (so it can be removed).
        let extraFloorCanvases : Canvas[] = Array.zeroCreate 8
        let RefreshTriforceHeaderOrSlot() =
            for i = 0 to 7 do
                let d = TrackerModel.GetDungeon(i)
                let header = mainTrackerCanvases.[i,1]
                let innerc : Canvas = triforceInnerCanvases.[i]
                let boxes = d.Boxes
                let slotIndex =
                    match boxes |> Array.tryFindIndex (fun b -> b.CellCurrent() = TrackerModel.ITEMS.TRIFORCE) with
                    | Some k -> Some k
                    | None -> if i = 1 && TrackerModel.level2StairBoxEnabled && TrackerModel.level2StairBox.CellCurrent() = TrackerModel.ITEMS.TRIFORCE then Some 2 else None   // the extra level 2 staircase slot shares level 2's bottom cell
                // detach from wherever it currently is
                header.Children.Remove(innerc)
                for j = 2 to 4 do
                    mainTrackerCanvases.[i,j].Children.Remove(innerc)
                if extraFloorCanvases.[i] <> null then
                    header.Children.Remove(extraFloorCanvases.[i])
                match slotIndex with
                | Some(k) when k < 3 ->
                    if extraFloorCanvases.[i] = null then
                        let ec,_ = Views.MakeBoxItem(cm, d.ExtraFloorBox)
                        extraFloorCanvases.[i] <- ec
                    canvasAdd(header, extraFloorCanvases.[i], 0., 0.)
                    innerc.Clip <- null   // triforce fully covers the item box, so no colored box outline shows around it
                    canvasAdd(mainTrackerCanvases.[i,k+2], innerc, 0., 0.)
                | _ ->
                    innerc.Clip <- null
                    canvasAdd(header, innerc, 0., 0.)
        for i = 0 to 7 do
            let innerc : Canvas = triforceInnerCanvases.[i]
            innerc.PreviewMouseDown.Add(fun ea ->
                let d = TrackerModel.GetDungeon(i)
                let isInSlot = innerc.Parent <> null && not(obj.ReferenceEquals(innerc.Parent, mainTrackerCanvases.[i,1]))
                if isInSlot && ea.ChangedButton <> System.Windows.Input.MouseButton.Left then
                    ea.Handled <- true
                    if not popupIsActive then
                        match (match d.Boxes |> Array.tryFind (fun b -> b.CellCurrent() = TrackerModel.ITEMS.TRIFORCE) with
                               | Some b -> Some b
                               | None -> if i = 1 && TrackerModel.level2StairBox.CellCurrent() = TrackerModel.ITEMS.TRIFORCE then Some TrackerModel.level2StairBox else None) with
                        | Some(b) -> 
                            match boxActivators.TryGetValue(b) with
                            | true, f -> f()
                            | _ -> ()
                        | None -> ()
                )
            for b in TrackerModel.GetDungeon(i).Boxes do
                b.Changed.Add(fun _ -> RefreshTriforceHeaderOrSlot())
        if not (TrackerModel.IsHiddenDungeonNumbers()) then
            TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.FinalBoxOf1Or4.Changed.Add(fun _ -> RefreshTriforceHeaderOrSlot())
            TrackerModel.level2StairBox.Changed.Add(fun _ -> RefreshTriforceHeaderOrSlot())   // a triforce piece in the extra level 2 staircase slot moves the numbered triforce there too
            TrackerModel.level2StairBoxEnabledChanged.Publish.Add(fun _ -> RefreshTriforceHeaderOrSlot())
        RefreshTriforceHeaderOrSlot()
        OptionsMenu.secondQuestDungeonsOptionChanged.Publish.Add(fun _ -> 
            RedrawForSecondQuestDungeonToggle()
            RefreshTriforceHeaderOrSlot()
            doUIUpdateEvent.Trigger()  // CompletedDungeons may change
            updateGhostBusters()
            )
        if TrackerModel.IsHiddenDungeonNumbers() then
            for i = 0 to 7 do
                TrackerModel.GetDungeon(i).HiddenDungeonColorOrLabelChanged.Add(fun _ -> updateGhostBusters())
    
    boxItemImpl, extrasImage, updateNumberedTriforceDisplayImpl, updateNumberedTriforceDisplayIfItExists
