module CustomComboBoxes

open System.Windows.Controls
open System.Windows.Media
open System.Windows

let canvasAdd = Graphics.canvasAdd
let gridAdd = Graphics.gridAdd
let makeGrid = Graphics.makeGrid

open HotKeys.MyKey

module GlobalFlag = 
    let mutable popupIsActive = false
open GlobalFlag

////////////////////////////////////////////////////////////
// ItemComboBox

let MouseButtonEventArgsToPlayerHas(ea:Input.MouseButtonEventArgs) =
    if ea.ChangedButton = Input.MouseButton.Left then TrackerModel.PlayerHas.YES
    elif ea.ChangedButton = Input.MouseButton.Right then TrackerModel.PlayerHas.NO
    else TrackerModel.PlayerHas.SKIPPED
let no  = new SolidColorBrush(Color.FromRgb(0xA8uy,0x00uy,0x00uy))
let noPctBrushes = Array.init 101 (fun i -> new SolidColorBrush(Color.FromRgb(0xFFuy - byte(100-i),byte(i/2),byte(i*2/3))))
let noPct(f) = noPctBrushes.[int(f*100.)]
let noNEPctBrushes = Array.init 101 (fun i -> new SolidColorBrush(Color.FromRgb(0xFFuy,byte(i/2),byte(i*2/3))))
let noAndNotEmpty(f) = noNEPctBrushes.[int(f*100.)]
let yes = new SolidColorBrush(Color.FromRgb(0x32uy,0xA8uy,0x32uy))
let skipped = Graphics.skipped
let skippedAndEmpty = Brushes.White
let boxCurrentBMP(boxCellCurrent, timelineOrigin) =
    match boxCellCurrent with
    | -1 -> null
    |  0 -> (if TrackerModel.IsCurrentlyBook() then Graphics.book_bmp else Graphics.magic_shield_bmp)
    |  1 -> Graphics.boomerangOrBootsBmp()
    |  2 -> Graphics.bow_bmp
    |  3 -> Graphics.power_bracelet_bmp
    |  4 -> Graphics.ladder_bmp
    |  5 -> Graphics.magicBoomerangOrMagicalBootsBmp()
    |  6 -> Graphics.magic_key_bmp
    |  7 -> Graphics.raft_bmp
    |  8 -> Graphics.recorder_bmp
    |  9 -> Graphics.red_candle_bmp
    | 10 -> Graphics.red_ring_bmp
    | 11 -> Graphics.silver_arrow_bmp
    | 12 -> Graphics.wand_bmp
    | 13 -> (if TrackerModel.IsWSMSReplacedByBU() then Graphics.ws_ms_bomb_upgrade_bmp else Graphics.ow_sword2_item_bmp)
    | 15 -> Graphics.blue_potion_bmp
    | 16 -> Graphics.red_potion_bmp
    | 17 -> Graphics.old_letter_bmp
    | 18 -> Graphics.ow_progressive_tier_item_bmps.[0]   // (the plain P sword; once placed, the box / tile shows the tier sprite)
    | 19 -> Graphics.ow_sword1_item_bmp
    | 20 -> Graphics.ow_sword3_item_bmp
    | 21 -> Graphics.ow_sword4_item_bmp
    | 22 -> Graphics.blue_ring_bmp
    | 23 -> Graphics.wood_arrow_bmp
    | 24 -> Graphics.blue_candle_bmp
    | 25 -> Graphics.magic_shield_bmp
    | 26 -> Graphics.bait_bmp
    | 27 -> Graphics.key_bmp
    | 28 -> Graphics.ws_ms_bomb_upgrade_bmp
    | 29 -> Graphics.triforce_item_bmp
    | n when n >= 30 && n < 46 -> Graphics.customItemBmps.[n-30]
    |  _ -> 
        match timelineOrigin with 
        | Some(tid) -> 
            let level = 
                match tid with
                | Timeline.TimelineID.LevelBox(i,_) -> Some(i)
                | Timeline.TimelineID.Level1or4Box3 -> if TrackerModel.IsSecondQuestDungeons then Some(4) else Some(1)
                | _ -> None
            match level with
            | Some(i) ->
                if TrackerModel.IsHiddenDungeonNumbers() then
                    if i=9 then
                        Graphics.heartFromNumberedDungeon_bmps.[i-1]
                    elif TrackerModel.GetDungeon(i-1).LabelChar <> '?' then
                        Graphics.heartFromNumberedDungeon_bmps.[int(TrackerModel.GetDungeon(i-1).LabelChar) - int('1')]
                    else
                        Graphics.heartFromLetteredDungeon_bmps.[i-1]
                else
                    Graphics.heartFromNumberedDungeon_bmps.[i-1]
            | None -> 
                match tid with
                | Timeline.TimelineID.LadderBox -> Graphics.heartFromCoast_bmp
                | Timeline.TimelineID.WhiteSwordBox -> Graphics.heartFromWhiteSwordCave_bmp
                | Timeline.TimelineID.ArmosBox -> Graphics.heartFromArmos_bmp
                | _ -> Graphics.owHeartTallFull_bmp 
        | None -> Graphics.heart_container_bmp

///////////////////////////////////////////////////////////////////

(*
note: a possible alternate way to 'sunglasses' would be to capture a snapshot as a bitmap, and then place it, here is some code i found that may be relevant
public void SaveUIElementToImageBMPFile(UIElement VisualElement, string FilePath) {
System.Windows.Media.Imaging.RenderTargetBitmap targetBitmap =
      new System.Windows.Media.Imaging.RenderTargetBitmap((int)VisualElement.RenderSize.Width,
                                                          (int)VisualElement.RenderSize.Height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
VisualElement.Measure(VisualElement.RenderSize);  
VisualElement.Arrange(new Rect(VisualElement.RenderSize)); 
targetBitmap.Render(VisualElement);
System.Windows.Media.Imaging.BmpBitmapEncoder bmpencoder = new System.Windows.Media.Imaging.BmpBitmapEncoder();
bmpencoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(targetBitmap));
using (System.IO.FileStream filestream = new System.IO.FileStream(FilePath, System.IO.FileMode.Create))
{
    bmpencoder.Save(filestream);
    filestream.Close();
}
*)
type CanvasManager(rootCanvas:Canvas, appMainCanvas:Canvas) as this =
    static let mutable theOnlyCanvasManager = None
    do
        if rootCanvas.Width <> appMainCanvas.Width || rootCanvas.Height <> appMainCanvas.Height then
            failwith "rootCanvas and appMainCanvas should be the same size"
        if not(obj.Equals(appMainCanvas.Parent,rootCanvas)) || not(rootCanvas.Children.Count=1) then
            failwith "rootCanvas must have appMainCanvas as its only child"
        if theOnlyCanvasManager.IsSome then
            failwith "created more than one CanvasManager"
        theOnlyCanvasManager <- Some(this)
    let popupCanvasStack = new System.Collections.Generic.Stack<_>()
    let opacityStack = new System.Collections.Generic.Stack<_>()
    let afterCreatePopupCanvas = new Event<_>()
    let beforeDismissPopupCanvas = new Event<_>()
    let width = rootCanvas.Width
    let sunglasses = new Canvas(Width=width, Height=rootCanvas.Height, Background=Brushes.Black, IsHitTestVisible=false)
    // a one-line note in the crash log whenever the popup layers get out of step (so a report of a stuck gray screen says when it began)
    let logPopupNote(msg:string) =
        try System.IO.File.AppendAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Z1R_Tracker_crash_log.txt"), sprintf "\n\nPOPUP NOTE -- %s -- %s\n" (System.DateTime.Now.ToString("yyyy-MM-dd-HH:mm:ss")) msg)
        with _ -> ()
    // removes every popup layer and the dimming layer (only used to recover from a layer that was left behind)
    let clearAllPopups() =
        while popupCanvasStack.Count > 0 do
            let pc = popupCanvasStack.Pop()
            (try beforeDismissPopupCanvas.Trigger(pc) with _ -> ())
            rootCanvas.Children.Remove(pc)
            if opacityStack.Count > 0 then opacityStack.Pop() |> ignore
        opacityStack.Clear()
        if not (isNull sunglasses.Parent) then (sunglasses.Parent :?> Canvas).Children.Remove(sunglasses)
    do
        // a click when no popup is supposed to be open but a popup layer is still there: remove the leftover layer so the tracker is usable again
        rootCanvas.PreviewMouseDown.Add(fun _ ->
            if popupCanvasStack.Count > 0 && not popupIsActive then
                logPopupNote(sprintf "%d leftover popup layer(s) with no popup active; removed on click" popupCanvasStack.Count)
                clearAllPopups())
    member _this.Width = width
    member _this.Height = rootCanvas.Height
    member _this.SetHeight(h) = 
        rootCanvas.Height <- h
        appMainCanvas.Height <- h
    member _this.RootCanvas = rootCanvas           // basically, no one should touch this, except to set mainWindow.Content <- cm.RootCanvas
    member _this.AppMainCanvas = appMainCanvas     // this is where the app should be putting all its content
    // and down here is where/how the popups should be putting their content
    member _this.PopupCanvasStack = popupCanvasStack
    member _this.CreatePopup(blackSunglassesOpacity) = 
        opacityStack.Push(blackSunglassesOpacity)
        sunglasses.Opacity <- opacityStack |> Seq.max
        // remove sunglasses from prior prior layer (if there was one)
        if sunglasses.Parent <> null then
            (sunglasses.Parent :?> Canvas).Children.Remove(sunglasses)
        // put the sunglasses over the prior child (appMainCanvas or previous popupCanvas)
        Canvas.SetZIndex(sunglasses, 100000)   // above everything on the layer, including elements with a raised ZIndex (e.g. the Show/Run Custom button)
        (rootCanvas.Children.[rootCanvas.Children.Count-1] :?> Canvas).Children.Add(sunglasses) |> ignore
        // put a new popup canvas in the root
        let popupCanvas = new Canvas(Width=appMainCanvas.Width, Height=appMainCanvas.Height, Background=Brushes.Transparent, IsHitTestVisible=true, Opacity=1.)
        rootCanvas.Children.Add(popupCanvas) |> ignore
        popupCanvasStack.Push(popupCanvas)
        afterCreatePopupCanvas.Trigger(popupCanvas)
        // return the popup canvas
        popupCanvas
    member _this.DismissPopup() =
        // remove the popup canvas
        let pc = popupCanvasStack.Pop()
        beforeDismissPopupCanvas.Trigger(pc)
        rootCanvas.Children.Remove(pc)
        opacityStack.Pop() |> ignore
        // remove the sunglasses from the prior child
        (rootCanvas.Children.[rootCanvas.Children.Count-1] :?> Canvas).Children.Remove(sunglasses) |> ignore
        // place the sunglasses on the prior prior layer (if there is one)
        if rootCanvas.Children.Count-2 >= 0 then
            sunglasses.Opacity <- opacityStack |> Seq.max
            (rootCanvas.Children.[rootCanvas.Children.Count-2] :?> Canvas).Children.Add(sunglasses) |> ignore
    // removes this particular popup's layer, even if popups were closed out of order (DismissPopup() always removes the top one)
    member _this.DismissPopupCanvas(pc:Canvas) =
        if popupCanvasStack.Count > 0 && obj.ReferenceEquals(popupCanvasStack.Peek(), pc) then
            _this.DismissPopup()
        elif popupCanvasStack.Contains(pc) then
            logPopupNote("a popup was closed out of order; its own layer was removed")
            (try beforeDismissPopupCanvas.Trigger(pc) with _ -> ())
            let cs = popupCanvasStack.ToArray()    // top first
            let os = opacityStack.ToArray()
            popupCanvasStack.Clear()
            opacityStack.Clear()
            for i = cs.Length-1 downto 0 do
                if not (obj.ReferenceEquals(cs.[i], pc)) then
                    popupCanvasStack.Push(cs.[i])
                    opacityStack.Push(os.[i])
            if not (isNull sunglasses.Parent) then (sunglasses.Parent :?> Canvas).Children.Remove(sunglasses)
            rootCanvas.Children.Remove(pc)
            if rootCanvas.Children.Count-2 >= 0 && opacityStack.Count > 0 then
                sunglasses.Opacity <- opacityStack |> Seq.max
                (rootCanvas.Children.[rootCanvas.Children.Count-2] :?> Canvas).Children.Add(sunglasses) |> ignore
        // else: this layer was already removed (e.g. by the leftover-layer cleanup); nothing to do
    // and here is how the broadcast window can listen for popup activity
    member _this.AfterCreatePopupCanvas = afterCreatePopupCanvas.Publish
    member _this.BeforeDismissPopupCanvas = beforeDismissPopupCanvas.Publish
    static member TheOnlyCanvasManager with get() = theOnlyCanvasManager.Value

// Escape closes the topmost modal popup (the same as clicking outside it)
let private modalDismissStack = System.Collections.Generic.Stack<System.Threading.ManualResetEvent>()
let private escapeHandlerRegistered = ref false
let private ensureEscapeHandler() =
    if not escapeHandlerRegistered.Value then
        escapeHandlerRegistered.Value <- true
        System.Windows.EventManager.RegisterClassHandler(typeof<System.Windows.Window>, System.Windows.Input.Keyboard.PreviewKeyDownEvent,
            System.Windows.Input.KeyEventHandler(fun _ ea ->
                if ea.Key = System.Windows.Input.Key.Escape && modalDismissStack.Count > 0 then
                    ea.Handled <- true
                    modalDismissStack.Peek().Set() |> ignore))

// TODO rename DoModals
let DoModalCore(cm:CanvasManager, wh:System.Threading.ManualResetEvent, placeElementOntoCanvas, removeElementFromCanvas, element:FrameworkElement, blackSunglassesOpacity) = async {
    if not popupIsActive then
        failwith "DoModalCore: popupIsActive was not set, which indicates programming error where a re-entrant UI thread may crash the program"
    // rather than use MouseCapture() API, just draw a canvas over entire window which will intercept all mouse gestures
    let c = cm.CreatePopup(blackSunglassesOpacity)
    try
      // place the element
      placeElementOntoCanvas(c, element)
      // catch mouse clicks outside the element
      c.MouseDown.Add(fun ea ->
          let pos = ea.GetPosition(element)
          if (pos.X < 0. || pos.X > element.ActualWidth) || (pos.Y < 0. || pos.Y > element.ActualHeight) then
              if ea.ButtonState = Input.MouseButtonState.Pressed &&
                      (ea.ChangedButton = Input.MouseButton.Left || ea.ChangedButton = Input.MouseButton.Middle || ea.ChangedButton = Input.MouseButton.Right) then
                  ea.Handled <- true
                  wh.Set() |> ignore
          )
      // hotkeys are routed to whatever the mouse is directly over; when the mouse is over the dimmed backdrop instead of the popup (e.g. a centered menu),
      // pass the key on to the popup so its hotkeys (like the wood sword cave's Took Sword / Took Candle) still work
      let forwarding = ref false
      c.MyKeyAdd(fun ea ->
          if not forwarding.Value then
              forwarding.Value <- true
              try
                  let skm, k = ea.Key
                  element.RaiseEvent(new MyKeyRoutedEventArgs(skm, k))
              finally
                  forwarding.Value <- false
              ea.Handled <- true)
      let ctxt = System.Threading.SynchronizationContext.Current
      ensureEscapeHandler()
      modalDismissStack.Push(wh)
      let! _ = Async.AwaitWaitHandle(wh)
      do! Async.SwitchToContext(ctxt)
    finally
      // (always runs, even if something above threw: take this popup's own layer off, so a failed popup can't leave a gray screen behind)
      if modalDismissStack.Contains(wh) then
          let keep = modalDismissStack.ToArray() |> Array.filter (fun w -> not (obj.ReferenceEquals(w, wh)))   // top first
          modalDismissStack.Clear()
          for k = keep.Length-1 downto 0 do modalDismissStack.Push(keep.[k])
      (try removeElementFromCanvas(c, element) with _ -> ())
      cm.DismissPopupCanvas(c)
    }

let DoModalOpa(cm:CanvasManager, wh:System.Threading.ManualResetEvent, x, y, element, opa) =
    DoModalCore(cm, wh, (fun (c,e) -> canvasAdd(c, e, x, y)), (fun (c,e) -> c.Children.Remove(e)), element, opa)

let DoModal(cm, wh, x, y, element) = DoModalOpa(cm, wh, x, y, element, 0.5)

let DoModalDockedOpa(cm:CanvasManager, wh:System.Threading.ManualResetEvent, dock, element, blackSunglassesOpacity) =
    let d = new DockPanel(Width=cm.Width, Height=cm.Height, LastChildFill=false)
    DoModalCore(cm, wh,
                    (fun (c,e) -> 
                        DockPanel.SetDock(e, dock)
                        d.Children.Add(e) |> ignore
                        canvasAdd(c, d, 0., 0.)),
                    (fun (_c,e) -> d.Children.Remove(e)), element, blackSunglassesOpacity)

let DoModalDocked(cm:CanvasManager, wh:System.Threading.ManualResetEvent, dock, element) =
    DoModalDockedOpa(cm, wh, dock, element, 0.5)

/////////////////////////////////////////

let DoModalMessageBoxCore(cm:CanvasManager, icon:System.Drawing.Icon, mainText, buttonTexts:seq<string>, x, y) = async { // returns buttonText if a button was pressed, or null if dismissed
    let grid = new Grid()
    grid.RowDefinitions.Add(new RowDefinition(Height=GridLength(1.0, GridUnitType.Star)))
    grid.RowDefinitions.Add(new RowDefinition(Height=GridLength.Auto))

    let mainDock = new DockPanel()
    let noIcon = isNull icon   // (no icon: the text is shown on its own, and the buttons are centered along the bottom)
    if not noIcon then
        let image = Graphics.BMPtoImage(icon.ToBitmap())
        image.HorizontalAlignment <- HorizontalAlignment.Left
        image.Margin <- Thickness(30.,0.,0.,0.)
        mainDock.Children.Add(image) |> ignore
        DockPanel.SetDock(image, Dock.Left)
    let mainTextBlock = new TextBox(Text=mainText, Background=Brushes.Transparent, BorderThickness=Thickness(0.), IsReadOnly=true, 
                                        TextWrapping=TextWrapping.Wrap, MaxWidth=cm.AppMainCanvas.Width-x-100., Width=System.Double.NaN, 
                                        VerticalAlignment=VerticalAlignment.Center, Margin=Thickness((if noIcon then 40. else 12.),20.,41.,15.))
    mainDock.Children.Add(mainTextBlock) |> ignore
    grid.Children.Add(mainDock) |> ignore
    Grid.SetRow(mainDock, 0)

    let wh = new System.Threading.ManualResetEvent(false)
    let mutable result = null
    let buttonDock : Panel =
        if noIcon then upcast new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(5.,0.,5.,20.))   // same distance from the border as the text has at the top
        else upcast new DockPanel(Margin=Thickness(5.,0.,0.,0.))
    let mutable first = not noIcon
    let lastText = Seq.last buttonTexts
    for bt in (if noIcon then buttonTexts else Seq.rev buttonTexts) do
        let b = new Button(MinWidth=88., MaxWidth=200., Height=26., Margin=Thickness(5.), HorizontalAlignment=HorizontalAlignment.Right, HorizontalContentAlignment=HorizontalAlignment.Stretch, VerticalContentAlignment=VerticalAlignment.Stretch)
        if first || (noIcon && bt = lastText) then
            b.Focus() |> ignore
            first <- false
        DockPanel.SetDock(b, Dock.Right)
        buttonDock.Children.Add(b) |> ignore
        b.Content <- new TextBox(Text=bt, IsReadOnly=true, IsHitTestVisible=false, TextAlignment=TextAlignment.Center, BorderThickness=Thickness(0.), FontSize=16.,
                                    Background=Graphics.almostBlack, Margin=Thickness(0.))
        b.Click.Add(fun _ -> result <- bt; wh.Set() |> ignore)
    grid.Children.Add(buttonDock) |> ignore
    Grid.SetRow(buttonDock, 1)

    let b = new Border(Child=grid, Background=Brushes.Black, BorderThickness=Thickness(5.), BorderBrush=Brushes.Gray, MaxWidth=cm.AppMainCanvas.Width-x-10.)
    let style = new Style(typeof<TextBox>)
    style.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.Orange))
    style.Setters.Add(new Setter(TextBox.BackgroundProperty, Brushes.Black))
    style.Setters.Add(new Setter(TextBox.BorderBrushProperty, Brushes.Orange))
    b.Resources.Add(typeof<TextBox>, style)
    let style = new Style(typeof<Button>)
    Graphics.addRoundedButtonSetters(style)   // (rounded dark look, like every other button)
    b.Resources.Add(typeof<Button>, style)

    // message boxes are centered in the app window
    b.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
    let areaW, areaH = cm.AppMainCanvas.Width, cm.AppMainCanvas.Height
    let cx = if areaW > 0. then max 0. ((areaW - b.DesiredSize.Width) / 2.) else x
    let cy = if areaH > 0. then max 0. ((areaH - b.DesiredSize.Height) / 2.) else y
    // the box was measured before it was in the window (where inherited fonts can make it bigger), so re-center it once it has its real size
    b.SizeChanged.Add(fun _ ->
        if areaW > 0. then Canvas.SetLeft(b, max 0. ((areaW - b.ActualWidth) / 2.))
        if areaH > 0. then Canvas.SetTop(b, max 0. ((areaH - b.ActualHeight) / 2.)))
    do! DoModal(cm, wh, cx, cy, b)
    return result
    }
let DoModalMessageBox(cm:CanvasManager, icon:System.Drawing.Icon, mainText, buttonTexts:seq<string>) = 
    DoModalMessageBoxCore(cm, icon, mainText, buttonTexts, 150., 200.)

/////////////////////////////////////////

// draw a pretty dashed rectangle with thickness ST around outside of (0,0) to (W,H)
let MakePrettyDashesImpl(canvas:Canvas, brush, W, H, ST, dash:float, space:float) =
    let dashLength = dash * ST
    let spaceLength = space * ST
    let computeDashArray(length) =
        let usable = length - dashLength
        let n = System.Math.Round(usable/(dashLength + spaceLength)) |> int
        let usableSpace = usable - float n * dashLength
        let actualSpaceLength = usableSpace / float n
        let a = [| 
            yield dash
            for _i=0 to n-1 do 
                yield actualSpaceLength / ST
                yield dash
        |]
        a
    let topLine = new Shapes.Line(X1 = -ST, Y1 = -ST/2., X2 = W + ST, Y2 = -ST/2., StrokeThickness=ST, Stroke=brush)
    topLine.StrokeDashArray <- new DoubleCollection(computeDashArray (W+2.*ST))
    canvasAdd(canvas, topLine, 0., 0.)
    let bottomLine = new Shapes.Line(X1 = -ST, Y1 = H+ST/2., X2 = W + ST, Y2 = H+ST/2., StrokeThickness=ST, Stroke=brush)
    bottomLine.StrokeDashArray <- new DoubleCollection(computeDashArray (W+2.*ST))
    canvasAdd(canvas, bottomLine, 0., 0.)
    let leftLine = new Shapes.Line(X1 = -ST/2., Y1 = -ST, X2 = -ST/2., Y2 = H+ST, StrokeThickness=ST, Stroke=brush)
    leftLine.StrokeDashArray <- new DoubleCollection(computeDashArray (H+2.*ST))
    canvasAdd(canvas, leftLine, 0., 0.)
    let rightLine = new Shapes.Line(X1 = W+ST/2., Y1 = -ST, X2 = W+ST/2., Y2 = H+ST, StrokeThickness=ST, Stroke=brush)
    rightLine.StrokeDashArray <- new DoubleCollection(computeDashArray (H+2.*ST))
    canvasAdd(canvas, rightLine, 0., 0.)

let prettyDashesCache = new System.Collections.Generic.Dictionary<string,Canvas>()
let GetPrettyDashes(name:string, brush, W, H, ST, dash:float, space:float) =
    match prettyDashesCache.TryGetValue(name) with
    | true, c -> c
    | false, _ ->
        let c = new Canvas()
        MakePrettyDashesImpl(c, brush, W, H, ST, dash, space)
        prettyDashesCache.Add(name, c)
        c

// TODO: factor out background brush color (assumes black)
type ModalGridSelectBrushes(originalTileHighlightBrush, gridSelectableHighlightBrush, gridNotSelectableHighlightBrush, borderBrush) =
    member _this.OriginalTileHighlightBrush = originalTileHighlightBrush
    member _this.GridSelectableHighlightBrush = gridSelectableHighlightBrush
    member _this.GridNotSelectableHighlightBrush = gridNotSelectableHighlightBrush
    member _this.BorderBrush = borderBrush
    member this.Dim(opacity) =
        let dim(scb:SolidColorBrush) =
            let c = scb.Color
            let nc = Color.FromArgb(byte(255.*opacity), c.R, c.G, c.B)
            Graphics.freeze(new SolidColorBrush(nc))
        new ModalGridSelectBrushes(dim(this.OriginalTileHighlightBrush), dim(this.GridSelectableHighlightBrush), dim(this.GridNotSelectableHighlightBrush), dim(this.BorderBrush))
    static member Defaults() =
        new ModalGridSelectBrushes(Brushes.Lime, Brushes.Lime, Brushes.Red, Brushes.Gray)

let borderThickness = 3.  // TODO should this be a param?

type PopupClickBehavior<'a> =
    | DismissPopupWithResult of 'a     // return result
    | DismissPopupWithNoResult         // tear down popup as though user clicked outside it
    | StayPoppedUp                     // keep awaiting more clicks
type GridClickDismissalWarpReturn =
    | NoWarp
    | WarpTo of Point
    | WarpToCenter
(*
CustomComboBoxes.DoModalGridSelect(cm, tileX, tileY, tileCanvas, gridElementsSelectablesAndIDs, originalStateIndex, activationDelta, (gnc, gnr, gcw, grh),
    gx, gy, redrawTile, onClick, extraDecorations, brushes, gridClickDismissalWarpReturn, who)
*)
// where the movable dungeon-room popup grids (monster / floor drop detail) currently are, relative to their tile: x, y, pixel width, pixel height;
// those popups use it to keep their name caption under the grid
let mutable MovableGridRect = (0., 0., 0., 0.)
let DoModalGridSelect<'State,'Result>
        (cm:CanvasManager, tileX, tileY, tileCanvas:Canvas, // tileCanvas - an empty Canvas with just Width and Height set, on which you will redrawTile your preview-tile
                gridElementsSelectablesAndIDs:(FrameworkElement*bool*'State)[], // array of display elements, whether they're selectable, and your stateID name/identifier for them
                originalStateIndex:int, // originalStateIndex is array index into the array
                activationDelta:int, // activationDelta is -1/0/1 if we should give initial input of scrollup/none/scrolldown
                (gnc, gnr, gcw, grh),   // grid: numCols, numRows, colWidth, rowHeight (heights of elements; this control will add border highlight)
                idealX, idealY,         // in a tile that is gcw by gch size, when the mouse cursor is being warped to its 'center', where should that be? good default: (gcw/2,grh/2)
                gx, gy,   // where to place grid (x,y) relative to (0,0) being the (unbordered) corner of your TileCanvas
                redrawTile,  // we pass you currentStateID
                onClick,  // called on tile click or selectable grid click, you choose what to do:   (mousebuttonEA, currentStateID) -> PopupClickBehavior<'Result>
                extraDecorations:seq<FrameworkElement*float*float>,  // extra things to draw at (x,y)s
                brushes:ModalGridSelectBrushes,
                gridClickDismissalWarpReturn,
                who:System.Threading.ManualResetEvent option,        // pass an unset one, if caller wants to be able to early-dismiss the dialog on its own
                uniqueName:string,      // for PrettyDashes caching
                opaOpt:float option     // blackSunglassesOpacity
                ) = async {
    let wh = match who with Some(x) -> x | _ -> new System.Threading.ManualResetEvent(false)
    let mutable result = None
    let popupCanvas = new Canvas()  // we will draw outside the canvas
    canvasAdd(popupCanvas, tileCanvas, 0., 0.)
    let ST = borderThickness
    let pretty = GetPrettyDashes(uniqueName, brushes.OriginalTileHighlightBrush, tileCanvas.Width, tileCanvas.Height, ST, 2., 1.2)
    popupCanvas.Children.Add(pretty) |> ignore
    if gridElementsSelectablesAndIDs.Length > gnr*gnc then
        failwith "the grid is not big enough to accomodate all the choices"
    // Shift+drag rearranging is supported in the item-box, overworld-tile and dungeon-room (room type / monster / floor drop) menus; the layout is remembered in the settings file
    let isReorderable = (uniqueName = "ItemBox" || uniqueName = "OWGridTile" || uniqueName = "DungeonRoom" || uniqueName = "MonsterDetail" || uniqueName = "FloorDropDetail")
    let total = gnc*gnr
    let keyOf(it:FrameworkElement*bool*'State) = let ic,_,st = it in if isNull (box ic) then "" else (box st).ToString()
    let defaultItems : (FrameworkElement*bool*'State)[] = 
        Array.init total (fun n -> if n < gridElementsSelectablesAndIDs.Length then gridElementsSelectablesAndIDs.[n] else (null, false, Unchecked.defaultof<'State>))
    let originalKey = keyOf defaultItems.[originalStateIndex]
    let items : (FrameworkElement*bool*'State)[] = Array.copy defaultItems
    if isReorderable then
        match TrackerModelOptions.MenuLayouts.TryGetValue(uniqueName) with
        | true, saved when not (isNull saved) ->
            let byKey = new System.Collections.Generic.Dictionary<string, FrameworkElement*bool*'State>()
            for it in defaultItems do
                let k = keyOf it
                if k <> "" && not (byKey.ContainsKey k) then byKey.[k] <- it
            let used = new System.Collections.Generic.HashSet<string>()
            let placed : (FrameworkElement*bool*'State)[] = Array.create total (null, false, Unchecked.defaultof<'State>)
            for pos = 0 to (min saved.Length total) - 1 do
                let k = saved.[pos]
                if k <> "" && byKey.ContainsKey k && used.Add k then placed.[pos] <- byKey.[k]
            // anything not in the saved layout (e.g. a newly added item) goes into the first free spots, in default order
            let leftovers = [ for it in defaultItems do
                                let k = keyOf it
                                if k <> "" && not (used.Contains k) then yield it ]
            let freeSpots = [ for pos = 0 to total-1 do
                                let ic,_,_ = placed.[pos]
                                if isNull (box ic) then yield pos ]
            if leftovers.Length <= freeSpots.Length then
                // several new items (e.g. the shop buttons) stay together in order, starting at the first free row where they all fit side by side; otherwise they take the first free spots
                let n = leftovers.Length
                let freeSet = new System.Collections.Generic.HashSet<int>(freeSpots)
                let rowStart =
                    if n > 1 && n <= gnc then
                        [ for r in 0 .. (total / gnc) - 1 do
                            let st = r * gnc
                            if [ st .. st + n - 1 ] |> List.forall (fun k -> freeSet.Contains k) then yield st ] |> List.tryHead
                    else None
                match rowStart with
                | Some st -> List.iteri (fun i it -> placed.[st + i] <- it) leftovers
                | None -> List.iteri (fun i it -> placed.[freeSpots.[i]] <- it) leftovers
                Array.blit placed 0 items 0 total
        | _ -> ()
    let mutable origIdx = 
        match items |> Array.tryFindIndex (fun it -> let ic,_,_ = it in not (isNull (box ic)) && keyOf it = originalKey) with
        | Some i -> i
        | None -> originalStateIndex
    let mutable dragFrom = -1
    let COLW, ROWH = gcw+2*int ST, grh+2*int ST
    let firstRowExtra = if uniqueName = "ItemBox" then 6 else 0   // the item menu's top row (the swords) is taller, so the sword icons can be bigger
    let grid = makeGrid(gnc, gnr, COLW, ROWH)
    if firstRowExtra > 0 then grid.RowDefinitions.[0].Height <- GridLength(float (ROWH + firstRowExtra))
    grid.Background <- Brushes.Black
    // the dungeon-room monster and floor-item menus are shown 2x larger (the grid scales from its top-left corner; cell hit-testing and arrow-key navigation follow the transform)
    let gridScale = if uniqueName = "MonsterDetail" || uniqueName = "FloorDropDetail" then 2.0 elif uniqueName = "ItemBox" || uniqueName = "OWGridTile" then 1.25 elif uniqueName = "DungeonRoom" then 4./3. else 1.0
    let gridBorder = new Border(BorderThickness=Thickness(ST), BorderBrush=brushes.BorderBrush, Child=grid)
    // the dungeon-room monster and floor drop menus can be moved (drag the little bar above the grid) and resized (drag the corner grip); the placement is remembered
    let tipUnderGrid = (uniqueName = "MonsterDetail" || uniqueName = "FloorDropDetail" || uniqueName = "DungeonRoom")   // (these menus keep their tip box under the grid)
    // the item menus (dungeon / overworld item boxes and the overworld tile menu) can be moved and resized too, in 'Jump to mouse' mode: the menu opens at its saved spot, the mouse jumps into it, and jumps back after a choice (like the monster menu).
    // In 'Anchor to mouse' mode (the original behavior) the menu opens next to the tile you clicked.
    let isJumpCapable = (uniqueName = "ItemBox" || uniqueName = "OWGridTile")
    let jumpKey = uniqueName + "Jump"
    let jumpMode = ref (isJumpCapable && (match TrackerModelOptions.MenuLayouts.TryGetValue(jumpKey) with | true, v when not (isNull v) && v.Length = 1 && v.[0] = "1" -> true | _ -> false))
    let isMovable = tipUnderGrid || isJumpCapable
    let moveActive() = not isJumpCapable || jumpMode.Value   // whether dragging the menu to a new spot is on (always, except in 'Anchor to mouse' mode)
    let gripOnLeft = true   // the resize grip is normally at the bottom-left (the right edge stays put while resizing); it moves to the bottom-right when the left side would be off screen
    let gripLeft = ref gripOnLeft
    let gripBusy = ref false   // true while the grip is being dragged (it stays on its side meanwhile)
    let placeKey = uniqueName + "Placement"
    let isResizable = isMovable
    let offX, offY, userScale = ref 0., ref 0., ref 1.0
    if isResizable then
        match TrackerModelOptions.MenuLayouts.TryGetValue(placeKey) with
        | true, v when not (isNull v) && v.Length = 3 ->
            let inv = System.Globalization.CultureInfo.InvariantCulture
            let ok0, _a = System.Double.TryParse(v.[0], System.Globalization.NumberStyles.Float, inv)
            let ok1, _b = System.Double.TryParse(v.[1], System.Globalization.NumberStyles.Float, inv)
            let ok2, c = System.Double.TryParse(v.[2], System.Globalization.NumberStyles.Float, inv)
            if ok0 && ok1 && ok2 then
                userScale.Value <- max 0.5 (min 4.0 c)
        | _ -> ()
    if gridScale <> 1.0 || userScale.Value <> 1.0 then
        gridBorder.RenderTransform <- new ScaleTransform(gridScale*userScale.Value, gridScale*userScale.Value)
        RenderOptions.SetBitmapScalingMode(gridBorder, BitmapScalingMode.NearestNeighbor)
    // keep the (enlarged) overworld-tile menu inside the app window: slide it up if it would run off the bottom, and put the tip box on the left if it would run off the right
    let gridPixW, gridPixH = (float(gnc*COLW) + 2.*ST)*gridScale, (float(gnr*ROWH + firstRowExtra) + 2.*ST)*gridScale
    // every popup grid stays inside the app window: slide it up/left if it would run off the bottom/right, and down/right if off the top/left
    let appW, appH = cm.AppMainCanvas.Width, cm.AppMainCanvas.Height
    let gx, gy =
        if appW > 0. && appH > 0. then
            let shiftIntoRange(tilePos:float, g:float, size:float, limit:float) =
                let over = tilePos + g + size - limit
                let g2 = if over > 0. then g - System.Math.Ceiling(over) else g
                if tilePos + g2 < 0. then -tilePos else g2
            shiftIntoRange(tileX, gx, gridPixW, appW), shiftIntoRange(tileY, gy, gridPixH, appH)
        else gx, gy
    canvasAdd(popupCanvas, gridBorder, gx, gy)
    // a moved menu stays at the same spot on screen (its position is saved absolutely, not relative to the tile you clicked)
    let applySavedAbs() =
        match TrackerModelOptions.MenuLayouts.TryGetValue(placeKey + "AbsV2") with
        | true, v when not (isNull v) && v.Length = 2 ->
            let inv = System.Globalization.CultureInfo.InvariantCulture
            let ok0, a = System.Double.TryParse(v.[0], System.Globalization.NumberStyles.Float, inv)
            let ok1, b = System.Double.TryParse(v.[1], System.Globalization.NumberStyles.Float, inv)
            if ok0 && ok1 then
                // never let a saved spot put the menu off the window
                let a2 = if appW > 0. then max 0. (min a (appW - 80.)) else a
                let b2 = if appH > 0. then max 50. (min b (appH - 60.)) else b
                offX.Value <- a2 - tileX - gx
                offY.Value <- b2 - tileY - gy
        | _ -> ()
    if isMovable && moveActive() then applySavedAbs()
    let noteTipText = "Tip: hold Shift and\ndrag an icon to move it\nto your preferred spot.\n(Saved for next time.)"
    let noteTextBlock : TextBlock ref = ref null
    let noteBox = 
        if isReorderable then
            let tb = new TextBlock(Text=noteTipText, Foreground=Brushes.Gainsboro, FontSize=12., TextWrapping=TextWrapping.Wrap, Width=120.)
            noteTextBlock.Value <- tb
            let b = new Border(Child=tb, Background=Brushes.Black, BorderBrush=brushes.BorderBrush, BorderThickness=Thickness(1.), Padding=Thickness(6.), IsHitTestVisible=false)
            let rightX = gx + gridPixW + 6.
            let noteX = if tileX + rightX + 132. > cm.AppMainCanvas.Width then gx - 132. - 6. else rightX
            if uniqueName = "MonsterDetail" || uniqueName = "DungeonRoom" then canvasAdd(popupCanvas, b, gx, gy + gridPixH + 8.)   // under the left part of the grid (the monster's name is to its right)
            elif tileX + noteX < 0. then canvasAdd(popupCanvas, b, gx, gy + gridPixH + 6.)   // no room on either side: put it under the grid
            else canvasAdd(popupCanvas, b, noteX, gy)
            Some b
        else None
    // the placement starts locked so a stray click near the edge can't move it; click the lock to unlock, then drag / resize, and lock it again
    let lockKey = uniqueName + "Locked"
    let isLocked = ref (match TrackerModelOptions.MenuLayouts.TryGetValue(lockKey) with | true, v when not (isNull v) && v.Length = 1 && v.[0] = "0" -> false | _ -> true)
    let lockText = new TextBlock(FontSize=10., VerticalAlignment=VerticalAlignment.Center, HorizontalAlignment=HorizontalAlignment.Center, IsHitTestVisible=false)
    lockText.LayoutTransform <- new RotateTransform(-90.)   // the lock bar is a tall vertical strip beside the tip box
    let lockBtn = new Border(Width=24., Background=Brushes.Black, BorderBrush=Brushes.Gray, BorderThickness=Thickness(1.), Cursor=System.Windows.Input.Cursors.Hand,
                                 ToolTip="Lock / unlock this menu's position and size", Child=lockText)
    // 'Anchor to mouse' (the menu opens next to the tile) / 'Jump to mouse' (the menu opens at its saved spot and the mouse jumps into it, then back to the tile after a choice)
    let mkModeBtn(text:string, tip:string) =
        new Border(Background=Brushes.Black, BorderBrush=Brushes.Gray, BorderThickness=Thickness(1.), Padding=Thickness(4.,2.,4.,2.), Cursor=System.Windows.Input.Cursors.Hand, ToolTip=tip,
                   Child=new TextBlock(Text=text, FontSize=11., Foreground=Brushes.Gainsboro, IsHitTestVisible=false, TextAlignment=TextAlignment.Center))
    let anchorBtn = mkModeBtn("Anchor to mouse", "The menu opens next to the tile you clicked (the original behavior)")
    let jumpBtn = mkModeBtn("Jump to mouse", "The menu opens at its saved spot, the mouse jumps into it, and jumps back to the tile after you choose")
    anchorBtn.Width <- 110.
    jumpBtn.Width <- 110.
    let refreshModeLook() =
        for (b:Border, on) in [ anchorBtn, not jumpMode.Value; jumpBtn, jumpMode.Value ] do
            b.BorderBrush <- (if on then Brushes.Orange else Brushes.Gray)
            (b.Child :?> TextBlock).Foreground <- (if on then Brushes.Orange else Brushes.Gainsboro)
    refreshModeLook()
    let hintText = new TextBlock(Foreground=Brushes.Gray, FontSize=10., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center, IsHitTestVisible=false)
    let headerGrid = new Grid()
    headerGrid.Children.Add(hintText) |> ignore
    let hideHintAndLock = isMovable && TrackerModelOptions.HidePopupHintAndLock.Value
    if hideHintAndLock then
        match noteBox with Some b -> popupCanvas.Children.Remove(b) | None -> ()   // (not just hidden: removed, so nothing of it can show)
    let moveHeader = new Border(Height=14., Background=Brushes.Black, BorderBrush=brushes.BorderBrush, BorderThickness=Thickness(1.), Child=headerGrid)
    let refreshLockLook() =
        if not (isNull noteTextBlock.Value) && not (moveActive()) then noteTextBlock.Value.Text <- noteTipText
        if isLocked.Value then
            lockText.Text <- "LOCKED"
            lockText.Foreground <- Brushes.Gray
            if not (isNull noteTextBlock.Value) && isMovable && moveActive() then noteTextBlock.Value.Text <- noteTipText + "\n\nClick LOCKED (the bar) to unlock this menu so it can be moved / resized."
            moveHeader.Cursor <- System.Windows.Input.Cursors.Arrow
            hintText.Text <- ""
        else
            lockText.Text <- "UNLOCKED"
            lockText.Foreground <- Brushes.Orange
            if not (isNull noteTextBlock.Value) && isMovable && moveActive() then noteTextBlock.Value.Text <- noteTipText + "\n\nUNLOCKED: drag the bar to move this menu, drag the corner grip to resize it."
            moveHeader.Cursor <- System.Windows.Input.Cursors.SizeAll
            hintText.Text <- "drag here to move"
    let sizeGrip = new Border(Width=32., Height=32., Background=Brushes.Black, BorderBrush=brushes.BorderBrush, BorderThickness=Thickness(1.), Cursor=(if gripOnLeft then System.Windows.Input.Cursors.SizeNESW else System.Windows.Input.Cursors.SizeNWSE),
                                  ToolTip="drag to resize",
                                  Child=new TextBlock(Text=(if gripOnLeft then "◣" else "◢"), Foreground=Brushes.Gray, FontSize=20., HorizontalAlignment=HorizontalAlignment.Center, IsHitTestVisible=false))
    let layoutMovables() =
        if isResizable then
            if uniqueName = "OWGridTile" then
                // the overworld tile menu opens next to its tile: don't let a (saved) size make it run off the window
                let areaW, areaH = cm.AppMainCanvas.Width, cm.AppMainCanvas.Height
                if areaW > 0. && areaH > 0. then
                    let maxS = max 0.5 (min (areaW / gridPixW) (areaH / gridPixH))
                    if userScale.Value > maxS then userScale.Value <- maxS
                    let s0 = userScale.Value
                    let w0, h0 = gridPixW*s0, gridPixH*s0
                    let left0, top0 = tileX + gx + offX.Value, tileY + gy + offY.Value
                    let left1 = max 0. (min left0 (areaW - w0))
                    let top1 = max 0. (min top0 (areaH - h0 - 20.))
                    offX.Value <- offX.Value + (left1 - left0)
                    offY.Value <- offY.Value + (top1 - top0)
            let s = userScale.Value
            gridBorder.RenderTransform <- new ScaleTransform(gridScale*s, gridScale*s)
            let X, Y = gx + offX.Value, gy + offY.Value
            let W, H = gridPixW*s, gridPixH*s
            Canvas.SetLeft(gridBorder, X)
            Canvas.SetTop(gridBorder, Y)
            moveHeader.Width <- W
            Canvas.SetLeft(moveHeader, X)
            Canvas.SetTop(moveHeader, (if uniqueName = "DungeonRoom" then Y + H + 55. else Y - 16.))   // (the room menu has its caption frame around the grid, so its bar goes under the frame)
            let gripLeftX = X - (if uniqueName = "DungeonRoom" then 42. else 34.)
            if not gripBusy.Value then
                gripLeft.Value <- (tileX + gripLeftX >= 0. || (appW > 0. && tileX + X + W + 2. + 32. > appW))   // (X is relative to the tile, so add the tile's position to compare with the window)   // moves to the right side of the menu if the left side would be off screen
                sizeGrip.Cursor <- (if gripLeft.Value then System.Windows.Input.Cursors.SizeNESW else System.Windows.Input.Cursors.SizeNWSE)
                (sizeGrip.Child :?> TextBlock).Text <- (if gripLeft.Value then "◣" else "◢")
            Canvas.SetLeft(sizeGrip, (if gripLeft.Value then gripLeftX else X + W + 2.))
            Canvas.SetTop(sizeGrip, Y + H - 30.)
            match noteBox with
            | Some b when not tipUnderGrid ->
                b.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
                let wasTop = abs (Canvas.GetTop(b) - gy) < 1.
                if Canvas.GetLeft(b) > gx then
                    Canvas.SetLeft(b, X + W + 6.)   // the tip box was to the right of the grid; keep it there
                    if wasTop then Canvas.SetTop(b, Y)
                elif Canvas.GetLeft(b) < gx then
                    Canvas.SetLeft(b, X - b.DesiredSize.Width - 6.)   // it was to the left of the grid; keep it there
                    if wasTop then Canvas.SetTop(b, Y)
                if isJumpCapable && not hideHintAndLock then
                    // the lock bar goes beside the tip box; the two mode toggles sit above the grid's top corners
                    let tipLeft, tipTop = Canvas.GetLeft(b), Canvas.GetTop(b)
                    lockBtn.Height <- b.DesiredSize.Height
                    Canvas.SetLeft(lockBtn, (if tipLeft < X - 1. then tipLeft - 30. else tipLeft + b.DesiredSize.Width + 6.))
                    Canvas.SetTop(lockBtn, tipTop)
                    if uniqueName = "ItemBox" then
                        // the item menu: two small buttons along the bottom edge, under the explainer
                        let decoBottom = extraDecorations |> Seq.map (fun (d, _, dy) -> dy + (if System.Double.IsNaN d.Height then 0. else d.Height)) |> Seq.fold max 0.
                        let btnTop = (max (decoBottom + offY.Value) (Y + H)) + 4.
                        let bw = floor ((W - 2.) / 2.)
                        for (btn:Border) in [ anchorBtn; jumpBtn ] do
                            btn.Width <- bw
                            btn.Padding <- Thickness(2.,1.,2.,1.)
                            (btn.Child :?> TextBlock).FontSize <- 9.
                        Canvas.SetLeft(anchorBtn, X)
                        Canvas.SetTop(anchorBtn, btnTop)
                        Canvas.SetLeft(jumpBtn, X + W - bw)
                        Canvas.SetTop(jumpBtn, btnTop)
                    else
                        let btnTop = (if moveActive() && not isLocked.Value then Y - 46. else Y - 30.)   // right above the grid (above the drag bar while that is showing)
                        Canvas.SetLeft(anchorBtn, X)
                        Canvas.SetTop(anchorBtn, btnTop)
                        Canvas.SetLeft(jumpBtn, X + W - jumpBtn.Width)
                        Canvas.SetTop(jumpBtn, btnTop)
            | Some b ->
                let noteTop = (if uniqueName = "DungeonRoom" then Y + H + 75. else Y + H + 8. + (if uniqueName = "FloorDropDetail" then 36. else 0.))
                Canvas.SetLeft(b, X)
                Canvas.SetTop(b, noteTop)
                // the lock bar: a tall vertical strip just to the right of the tip box, the same height
                if not hideHintAndLock then
                    b.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
                    lockBtn.Height <- b.DesiredSize.Height
                    Canvas.SetLeft(lockBtn, X + b.DesiredSize.Width + 6.)
                    Canvas.SetTop(lockBtn, noteTop)   // under the grid (the floor drop's name goes between)
            | None -> ()
            if isJumpCapable then
                // the explainer under the grid moves with it
                for (d, dx, dy) in extraDecorations do
                    Canvas.SetLeft(d, dx + offX.Value)
                    Canvas.SetTop(d, dy + offY.Value)
            MovableGridRect <- (X, Y, W, H)
    let refreshLock() =
        refreshLockLook()
        sizeGrip.Visibility <- (if isMovable && isLocked.Value && moveActive() then Visibility.Collapsed else Visibility.Visible)
        moveHeader.Visibility <- (if isLocked.Value || hideHintAndLock || not (moveActive()) then Visibility.Collapsed else Visibility.Visible)
        lockBtn.Visibility <- (if hideHintAndLock || not (moveActive()) then Visibility.Collapsed else Visibility.Visible)
    if isResizable then
        Canvas.SetZIndex(moveHeader, 50)
        Canvas.SetZIndex(sizeGrip, 50)
        Canvas.SetZIndex(lockBtn, 50)
        if isMovable && not hideHintAndLock then popupCanvas.Children.Add(moveHeader) |> ignore
        if isMovable && not hideHintAndLock then popupCanvas.Children.Add(lockBtn) |> ignore
        popupCanvas.Children.Add(sizeGrip) |> ignore
        refreshLock()
        layoutMovables()
        if isJumpCapable && not hideHintAndLock then
            Canvas.SetZIndex(anchorBtn, 50)
            Canvas.SetZIndex(jumpBtn, 50)
            popupCanvas.Children.Add(anchorBtn) |> ignore
            popupCanvas.Children.Add(jumpBtn) |> ignore
            let setMode(jump:bool) =
                jumpMode.Value <- jump
                TrackerModelOptions.MenuLayouts.[jumpKey] <- [| (if jump then "1" else "0") |]
                if jump then applySavedAbs() else (offX.Value <- 0.; offY.Value <- 0.)
                TrackerModelOptions.writeSettings()
                refreshModeLook()
                refreshLock()
                layoutMovables()
            anchorBtn.MouseLeftButtonDown.Add(fun ea -> setMode false; ea.Handled <- true)
            jumpBtn.MouseLeftButtonDown.Add(fun ea -> setMode true; ea.Handled <- true)
    let mutable currentState = origIdx   // the only bit of local mutable state during the modal - it ranges from 0..gridElements.Length-1
    let mutable allDoneSoNoLongerNeedToMouseLeaveSnapback = false
    let canvasesToClear = ResizeArray<Canvas>()
    let selfCleanup() =
        for (d,_x,_y) in extraDecorations do
            popupCanvas.Children.Remove(d)
        popupCanvas.Children.Remove(pretty)
        match noteBox with Some b -> popupCanvas.Children.Remove(b) | None -> ()
        popupCanvas.Children.Remove(moveHeader)
        popupCanvas.Children.Remove(lockBtn)
        popupCanvas.Children.Remove(sizeGrip)
        popupCanvas.Children.Remove(anchorBtn)
        popupCanvas.Children.Remove(jumpBtn)
        for c in canvasesToClear do
            c.Children.Clear()
    let dismiss() =
        wh.Set() |> ignore
    let isSelectable() = let _,s,_ = items.[currentState] in s
    let stateID() = let _,_,x = items.[currentState] in x
    let redrawGridFuncs = ResizeArray()
    let changeCurrentState(newState) =
        currentState <- newState
        redrawTile(stateID())
        for r in redrawGridFuncs do r()
    let next() =
        currentState <- (currentState+1) % items.Length
        while not(isSelectable()) do
            currentState <- (currentState+1) % items.Length
        changeCurrentState(currentState)
    let prev() =
        currentState <- (currentState-1+items.Length) % items.Length
        while not(isSelectable()) do
            currentState <- (currentState-1+items.Length) % items.Length
        changeCurrentState(currentState)
    let snapBack() = if not allDoneSoNoLongerNeedToMouseLeaveSnapback then changeCurrentState(origIdx)     // don't redraw and do other work after the popup is done (MouseLeave can be very error-prone if not careful!)
    // original tile
    redrawTile(stateID())
    if isResizable then
        let inv = System.Globalization.CultureInfo.InvariantCulture
        let saveMovable() =
            TrackerModelOptions.MenuLayouts.[placeKey] <- [| "0"; "0"; userScale.Value.ToString(inv) |]
            if isMovable && moveActive() then TrackerModelOptions.MenuLayouts.[placeKey + "AbsV2"] <- [| (tileX + gx + offX.Value).ToString(inv); (tileY + gy + offY.Value).ToString(inv) |]
            TrackerModelOptions.writeSettings()
        let relayout() =
            layoutMovables()
            redrawTile(stateID())
        let dragging = ref false
        let last = ref (Point())
        lockBtn.MouseLeftButtonDown.Add(fun ea ->
            isLocked.Value <- not isLocked.Value
            TrackerModelOptions.MenuLayouts.[lockKey] <- [| (if isLocked.Value then "1" else "0") |]
            TrackerModelOptions.writeSettings()
            refreshLock()
            layoutMovables()
            ea.Handled <- true)
        moveHeader.MouseLeftButtonDown.Add(fun ea ->
            if not isLocked.Value then
                dragging.Value <- true
                last.Value <- ea.GetPosition(popupCanvas)
                moveHeader.CaptureMouse() |> ignore
            ea.Handled <- true)
        moveHeader.MouseMove.Add(fun ea ->
            if dragging.Value then
                let p = ea.GetPosition(popupCanvas)
                offX.Value <- offX.Value + (p.X - last.Value.X)
                offY.Value <- offY.Value + (p.Y - last.Value.Y)
                last.Value <- p
                relayout())
        moveHeader.MouseLeftButtonUp.Add(fun ea ->
            if dragging.Value then
                dragging.Value <- false
                moveHeader.ReleaseMouseCapture()
                saveMovable()
            ea.Handled <- true)
        let resizing = ref false
        let startX, startScale, startOffX = ref 0., ref 1.0, ref 0.
        sizeGrip.MouseLeftButtonDown.Add(fun ea ->
            if not (isMovable && isLocked.Value && moveActive()) then
                resizing.Value <- true
                gripBusy.Value <- true
                startX.Value <- ea.GetPosition(popupCanvas).X
                startScale.Value <- userScale.Value
                startOffX.Value <- offX.Value
                sizeGrip.CaptureMouse() |> ignore
            ea.Handled <- true)
        sizeGrip.MouseMove.Add(fun ea ->
            if resizing.Value then
                let dx = ea.GetPosition(popupCanvas).X - startX.Value
                let w0 = gridPixW*startScale.Value
                let dw = if gripLeft.Value then -dx else dx   // dragging the left grip further left makes it bigger
                userScale.Value <- max 0.5 (min 4.0 (startScale.Value * (w0 + dw) / w0))
                if gripLeft.Value then offX.Value <- startOffX.Value - (gridPixW*userScale.Value - w0)   // keep the right edge fixed
                relayout())
        sizeGrip.MouseLeftButtonUp.Add(fun ea ->
            if resizing.Value then
                resizing.Value <- false
                gripBusy.Value <- false
                sizeGrip.ReleaseMouseCapture()
                saveMovable()
            ea.Handled <- true)
    let centerOfTile = Point(tileX+tileCanvas.Width/2., tileY+tileCanvas.Height/2.)   // use actual center, not the "ideal" coords, as we want the most wiggle-room, even if mouse obscures item a little
    // you can scroll to activate a box along its edge, such that clicking after the scroll ends up outside the tile; center the mouse when scrolling, as scrolling often jiggles it too
    let recenterMouse() = Graphics.SilentlyWarpMouseCursorTo(centerOfTile)
    // the monster / floor drop menus (opened by scrolling a dungeon room) bring the mouse into the menu, onto its top-left choice
    if uniqueName = "MonsterDetail" || uniqueName = "FloorDropDetail" then
        let cx, cy = 0, 0   // the top-left choice
        let sc = gridScale * userScale.Value
        Graphics.WarpMouseCursorTo(Point(tileX + gx + offX.Value + sc*(ST + (float cx + 0.5)*float COLW), tileY + gy + offY.Value + sc*(ST + (float cy + 0.5)*float ROWH)))
    // the item menus in 'Jump to mouse' mode bring the mouse into the menu, onto the current choice
    if isJumpCapable && jumpMode.Value then
        let sc = gridScale * userScale.Value
        let cx, cy = origIdx % gnc, origIdx / gnc
        let yOff = if cy > 0 then float firstRowExtra else float firstRowExtra / 2.
        Graphics.WarpMouseCursorTo(Point(tileX + gx + offX.Value + sc*(ST + (float cx + 0.5)*float COLW), tileY + gy + offY.Value + sc*(ST + float (cy*ROWH) + yOff + float ROWH / 2.)))
    if activationDelta <> 0 && not (isJumpCapable && jumpMode.Value) then  
        recenterMouse()
    tileCanvas.MouseWheel.Add(fun x -> recenterMouse(); if x.Delta<0 then next() else prev())
    tileCanvas.MouseDown.Add(fun ea -> 
        ea.Handled <- true
        match onClick(ea, stateID()) with
        | DismissPopupWithResult(r:'Result) -> result <- Some(r); dismiss()
        | DismissPopupWithNoResult -> dismiss()
        | StayPoppedUp -> ()
        )
    tileCanvas.MouseLeave.Add(fun _ -> snapBack())
    let centerOf(x,y) = grid.TranslatePoint(Point(ST+float(x*COLW)+idealX, ST+float(y*ROWH)+(if y > 0 then float firstRowExtra else 0.)+idealY), cm.AppMainCanvas)
    tileCanvas.MyKeyAdd(fun ea ->
        let x,y = currentState % gnc, currentState / gnc
        match HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key) with
        // 'arrows' warp mouse to current entry, then apply arrow to nav thru grid
        | Some(HotKeys.GlobalHotkeyTargets.MoveCursorRight) -> 
            ea.Handled <- true
            Graphics.NavigationallyWarpMouseCursorTo(centerOf(x,y))
            if x<gnc-1 then
                Graphics.NavigationallyWarpMouseCursorTo(centerOf(x+1,y))
        | Some(HotKeys.GlobalHotkeyTargets.MoveCursorLeft) -> 
            ea.Handled <- true
            Graphics.NavigationallyWarpMouseCursorTo(centerOf(x,y))
            if x>0 then
                Graphics.NavigationallyWarpMouseCursorTo(centerOf(x-1,y))
        | Some(HotKeys.GlobalHotkeyTargets.MoveCursorUp) -> 
            ea.Handled <- true
            Graphics.NavigationallyWarpMouseCursorTo(centerOf(x,y))
            if y>0 then
                Graphics.NavigationallyWarpMouseCursorTo(centerOf(x,y-1))
        | Some(HotKeys.GlobalHotkeyTargets.MoveCursorDown) -> 
            ea.Handled <- true
            Graphics.NavigationallyWarpMouseCursorTo(centerOf(x,y))
            if y<gnr-1 then
                Graphics.NavigationallyWarpMouseCursorTo(centerOf(x,y+1))
        // behave like a click on the original tile, e.g. to right click dismiss an accidentally activated overworld tile popup
        | Some(HotKeys.GlobalHotkeyTargets.LeftClick) -> Graphics.Win32.LeftMouseClick()
        | Some(HotKeys.GlobalHotkeyTargets.MiddleClick) -> Graphics.Win32.MiddleMouseClick()
        | Some(HotKeys.GlobalHotkeyTargets.RightClick) -> Graphics.Win32.RightMouseClick()
        | Some(HotKeys.GlobalHotkeyTargets.ScrollUp) -> Graphics.Win32.ScrollWheelRotateUp()
        | Some(HotKeys.GlobalHotkeyTargets.ScrollDown) -> Graphics.Win32.ScrollWheelRotateDown()
        | _ -> ()
        )
    let handleCursorHotKey(x,y) =
        fun (ea:MyKeyRoutedEventArgs) ->
        match HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key) with
        | Some(HotKeys.GlobalHotkeyTargets.MoveCursorRight) -> 
            ea.Handled <- true
            if x<gnc-1 then
                Graphics.NavigationallyWarpMouseCursorTo(centerOf(x+1,y))
        | Some(HotKeys.GlobalHotkeyTargets.MoveCursorLeft) -> 
            ea.Handled <- true
            if x>0 then
                Graphics.NavigationallyWarpMouseCursorTo(centerOf(x-1,y))
        | Some(HotKeys.GlobalHotkeyTargets.MoveCursorUp) -> 
            ea.Handled <- true
            if y>0 then
                Graphics.NavigationallyWarpMouseCursorTo(centerOf(x,y-1))
        | Some(HotKeys.GlobalHotkeyTargets.MoveCursorDown) -> 
            ea.Handled <- true
            if y<gnr-1 then
                Graphics.NavigationallyWarpMouseCursorTo(centerOf(x,y+1))
        | Some(HotKeys.GlobalHotkeyTargets.LeftClick) -> Graphics.Win32.LeftMouseClick()
        | Some(HotKeys.GlobalHotkeyTargets.MiddleClick) -> Graphics.Win32.MiddleMouseClick()
        | Some(HotKeys.GlobalHotkeyTargets.RightClick) -> Graphics.Win32.RightMouseClick()
        | _ -> ()                    
    // grid of choices (every cell, including holes, is a bordered canvas so that Shift+drag can move items into empty spots too)
    let cellBorders : Border[] = Array.zeroCreate total
    let populate(n) =
        let c = cellBorders.[n].Child :?> Canvas
        let icon,isSel,_ = items.[n]
        if not (isNull (box icon)) then
            // the sword icons are drawn big in the (taller) top row; if one is moved to another row it shrinks to fit that row's cell
            match icon with
            | :? System.Windows.Controls.Image as img when (match img.Tag with :? string as t -> t = "sword" | _ -> false) ->
                if firstRowExtra > 0 && n / gnc = 0 then
                    img.Width <- 15.
                    img.Height <- 27.
                    Canvas.SetLeft(img, 3.)
                    Canvas.SetTop(img, 0.)
                else
                    img.Width <- 12.
                    img.Height <- 21.
                    Canvas.SetLeft(img, 4.)
                    Canvas.SetTop(img, 0.)
            | _ -> ()
            c.Children.Add(icon) |> ignore
            if not isSel then // grey out
                c.Children.Add(new Canvas(Width=c.Width, Height=c.Height, Background=Brushes.Black, Opacity=0.6, IsHitTestVisible=false)) |> ignore
    let isShiftDown() = Input.Keyboard.IsKeyDown(Input.Key.LeftShift) || Input.Keyboard.IsKeyDown(Input.Key.RightShift)
    let saveLayout() =
        TrackerModelOptions.MenuLayouts.[uniqueName] <- items |> Array.map keyOf
        TrackerModelOptions.writeSettings()
    let refreshHighlights() = for r in redrawGridFuncs do r()
    let swapCells(a, m) =
        let ca, cm_ = (cellBorders.[a].Child :?> Canvas), (cellBorders.[m].Child :?> Canvas)
        ca.Children.Clear()
        cm_.Children.Clear()
        let tmp = items.[a]
        items.[a] <- items.[m]
        items.[m] <- tmp
        populate a
        populate m
        // keep tracking where the tile's original state now lives
        let ok = originalKey
        match items |> Array.tryFindIndex (fun it -> let ic,_,_ = it in not (isNull (box ic)) && keyOf it = ok) with
        | Some i -> origIdx <- i
        | None -> ()
        saveLayout()
    for y = 0 to gnr - 1 do
        for x = 0 to gnc - 1 do
            let n = y * gnc + x
            let b = new Border(BorderThickness=Thickness(ST), Background=Brushes.Black)
            gridAdd(grid, b, x, y)
            let c = new Canvas(Background=Brushes.Black, Width=float gcw, Height=float (grh + (if y = 0 then firstRowExtra else 0)))  // ensure the canvas has a surface on which to receive mouse clicks
            b.Child <- c
            cellBorders.[n] <- b
            canvasesToClear.Add(c)
            populate n
            b.MouseEnter.Add(fun _ -> 
                let ic,_,_ = items.[n]
                if isNull (box ic) then snapBack() else changeCurrentState(n))
            let redraw() = 
                let ic,isSel,_ = items.[n]
                b.BorderBrush <- 
                    if isNull (box ic) && dragFrom <> n then Brushes.Black
                    elif dragFrom = n then Brushes.Orange
                    elif n = currentState then (if isSel then brushes.GridSelectableHighlightBrush else brushes.GridNotSelectableHighlightBrush) 
                    else Brushes.Black
            redrawGridFuncs.Add(redraw)
            redraw()
            let mouseWarpDismiss() =
                allDoneSoNoLongerNeedToMouseLeaveSnapback <- true
                match (if isJumpCapable && jumpMode.Value then WarpToCenter else gridClickDismissalWarpReturn) with
                | WarpTo(pos) -> Graphics.WarpMouseCursorTo(pos)
                | WarpToCenter -> 
                    let pos = tileCanvas.TranslatePoint(Point(tileCanvas.Width/2.,tileCanvas.Height/2.), cm.AppMainCanvas)
                    Graphics.WarpMouseCursorTo(pos)
                | NoWarp -> ()
                dismiss()
            b.MouseDown.Add(fun ea -> 
                ea.Handled <- true
                let ic,isSel,_ = items.[n]
                if isReorderable && ea.ChangedButton = Input.MouseButton.Left && isShiftDown() then
                    // Shift+drag: pick up this item (if there is one); it is dropped on whatever cell the mouse is released over
                    if not (isNull (box ic)) then
                        dragFrom <- n
                        refreshHighlights()
                elif not (isNull (box ic)) && isSel then
                    match onClick(ea, stateID()) with
                    | DismissPopupWithResult(r) -> result <- Some(r); mouseWarpDismiss()
                    | DismissPopupWithNoResult -> mouseWarpDismiss()
                    | StayPoppedUp -> ()
                )
            b.MouseUp.Add(fun ea ->
                if dragFrom >= 0 && ea.ChangedButton = Input.MouseButton.Left then
                    ea.Handled <- true
                    let a = dragFrom
                    dragFrom <- -1
                    if a <> n then
                        swapCells(a, n)
                        let ic,isSel,_ = items.[n]
                        if not (isNull (box ic)) && isSel then changeCurrentState(n)
                    refreshHighlights()
                )
            b.MyKeyAdd(handleCursorHotKey(x,y))
    grid.MouseLeave.Add(fun _ -> 
        if dragFrom >= 0 then
            dragFrom <- -1
            refreshHighlights()
        snapBack())
    for (d,x,y) in extraDecorations do
        canvasAdd(popupCanvas, d, x, y)
    if isJumpCapable && isResizable then layoutMovables()   // (puts the explainer where the moved menu is)
    // initial input
    match activationDelta with
    | -1 -> prev()
    |  0 -> ()
    |  1 -> next()
    | _ -> failwith "bad activationDelta"
    // activate the modal
    match opaOpt with
    | Some opa -> do! DoModalOpa(cm, wh, tileX, tileY, popupCanvas, opa)
    | _ -> do! DoModal(cm, wh, tileX, tileY, popupCanvas)
    selfCleanup()
    allDoneSoNoLongerNeedToMouseLeaveSnapback <- true
    return result
    }

////////////////////////////////

let itemBoxMouseButtonExplainerDecoration =
    let d = new DockPanel(Height=90., LastChildFill=true, Background=Brushes.Black)
    let sp = new StackPanel(Orientation=Orientation.Vertical)
    d.Children.Add(sp) |> ignore
    for color, text in [yes,"Have it"; skipped,"Don't want it"; no,"Don't have it"] do
        let p = new StackPanel(Orientation=Orientation.Horizontal)
        let c = new Canvas(Width=30., Height=30.)
        let rect = new Shapes.Rectangle(Width=30., Height=30., Stroke=color, StrokeThickness=3.0, IsHitTestVisible=false)
        c.Children.Add(rect) |> ignore
        if obj.Equals(color, skipped) then
            Graphics.placeSkippedItemXDecoration(c)
        p.Children.Add(c) |> ignore
        let tb = new TextBox(FontSize=12., Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, 
                                Text=text, VerticalAlignment=VerticalAlignment.Center, BorderThickness=Thickness(0.))
        p.Children.Add(tb) |> ignore
        sp.Children.Add(p) |> ignore
    let b = new Border(Background=Brushes.Black, BorderBrush=Brushes.Gray, BorderThickness=Thickness(3.), Child=d)
    b.MouseDown.Add(fun ea -> ea.Handled <- true)  // absorb mouse clicks, so that clicking explainer decoration does not dismiss popup due to being outside-the-area click
    let fe : FrameworkElement = upcast b
    fe
let itemBoxModalGridSelectBrushes = new ModalGridSelectBrushes(Brushes.Yellow, Brushes.Yellow, new SolidColorBrush(Color.FromRgb(140uy,10uy,0uy)), Brushes.Gray)

// the big (15x27) sword icons for the item menu's top row; null for every other item
let swordMenuBmpFor(ident:int) : System.Drawing.Bitmap =
    match ident with
    | 19 -> (if TrackerModel.ProgressiveItems then Graphics.ow_progressive_tier_menu_bmps.[1] else Graphics.ow_sword1_menu_bmp)   // (with Progressive Items, the plain swords show as P1..P4 even though they are disabled)
    | 13 -> (if TrackerModel.IsWSMSReplacedByBU() then null elif TrackerModel.ProgressiveItems then Graphics.ow_progressive_tier_menu_bmps.[2] else Graphics.ow_sword2_menu_bmp)
    | 20 -> (if TrackerModel.ProgressiveItems then Graphics.ow_progressive_tier_menu_bmps.[3] else Graphics.ow_sword3_menu_bmp)
    | 21 -> (if TrackerModel.ProgressiveItems then Graphics.ow_progressive_tier_menu_bmps.[4] else Graphics.ow_sword4_menu_bmp)
    | 18 -> Graphics.ow_progressive_tier_menu_bmps.[0]
    | _ -> null
let mutable allItemsSelectable = false   // true while the level 9 entry item menu is open: that box is only a note, so every item (even a 'disabled' one) can be picked
let mutable itemMenuDomain = TrackerModel.allItemWithHeartShuffleChoiceDomain   // DoLocalItemComboBox swaps this for boxes that don't use up items
let DisplayItemComboBox(cm:CanvasManager, boxX, boxY, boxCellCurrent, activationDelta, originalPlayerHas, callerExtraDecorations, gridClickDismissalWarpReturn) = async {
    let innerc = new Canvas(Width=24., Height=24., Background=Brushes.Black)  // just has item drawn on it, not the box
    let redraw(n) =
        let n = if n = -2 then -1 else n   // -2 is a blank padding cell in the menu
        innerc.Children.Clear()
        let bmp = boxCurrentBMP(n, None)
        if bmp <> null then
            let image = Graphics.BMPtoImage(bmp)
            canvasAdd(innerc, image, 1., 1.)
        innerc
    redraw(boxCellCurrent) |> ignore
    // the grid is 5 columns wide and 6 rows tall (-1 is the empty cell)
    let gridIdents = [|
        // row 1: swords
        TrackerModel.ITEMS.WOODSWORD; TrackerModel.ITEMS.WHITESWORD; TrackerModel.ITEMS.MAGICALSWORD; TrackerModel.ITEMS.WHITESWORD4; TrackerModel.ITEMS.PROGRESSIVESWORD
        // row 2
        TrackerModel.ITEMS.LADDER; TrackerModel.ITEMS.POWERBRACELET; TrackerModel.ITEMS.RECORDER; TrackerModel.ITEMS.RAFT; TrackerModel.ITEMS.BOW
        // row 3
        TrackerModel.ITEMS.BLUERING; TrackerModel.ITEMS.REDRING; TrackerModel.ITEMS.KEY; TrackerModel.ITEMS.WOODARROW; TrackerModel.ITEMS.SILVERARROW
        // row 4
        TrackerModel.ITEMS.WAND; TrackerModel.ITEMS.BLUECANDLE; TrackerModel.ITEMS.REDCANDLE; TrackerModel.ITEMS.BOOMERANG; TrackerModel.ITEMS.MAGICBOOMERANG
        // row 5
        TrackerModel.ITEMS.BOOKSHIELD; TrackerModel.ITEMS.SHIELD; TrackerModel.ITEMS.MEAT; TrackerModel.ITEMS.OLDKEY; TrackerModel.ITEMS.BOMBUPGRADE
        // row 6
        TrackerModel.ITEMS.BLUEPOTION; TrackerModel.ITEMS.LETTER; TrackerModel.ITEMS.HEARTCONTAINER; TrackerModel.ITEMS.TRIFORCE; -1
        |]
    // the user's custom icons (if that option is on) go in extra rows at the bottom; -2 pads out the last row with blank, unselectable cells
    let emptyCellIdx = gridIdents.Length-1
    let gridIdents =
        let nc = TrackerModel.ITEMS.customMenuCount
        // a box that already holds a custom icon the menu no longer offers (option off / folder changed) still shows that icon as its current cell
        let stray = if boxCellCurrent >= TrackerModel.ITEMS.CUSTOM_BASE && boxCellCurrent < TrackerModel.ITEMS.CUSTOM_BASE + TrackerModel.ITEMS.CUSTOM_COUNT && boxCellCurrent - TrackerModel.ITEMS.CUSTOM_BASE >= nc then [| boxCellCurrent |] else [||]
        let offered = Array.append [| for k in 0..nc-1 -> TrackerModel.ITEMS.CUSTOM_BASE + k |] stray
        if offered.Length = 0 then gridIdents
        else
            let padded = ((offered.Length + 4) / 5) * 5
            Array.append gridIdents [| for k in 0..padded-1 -> (if k < offered.Length then offered.[k] else -2) |]
    let gridRows = gridIdents.Length / 5
    let gridElementsSelectablesAndIDs = [|
        for ident in gridIdents do
            let fe:FrameworkElement = 
                if ident = -1 || ident = -2 then upcast new Canvas() 
                else
                    let sm = swordMenuBmpFor ident
                    if sm <> null then
                        let img = Graphics.BMPtoImage(sm)
                        img.Stretch <- Stretch.Fill
                        img.Tag <- box "sword"
                        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor)
                        upcast img
                    else
                        // Progressive Items: the blue ring / candle and wood arrow cells show the current tier (the red ones are disabled and keep their own picture)
                        let f = TrackerModel.progressiveFamily ident
                        let shownIdent = if f >= 2 && not (TrackerModel.progressiveDisabledIdent ident) then TrackerModel.progressiveFamilyIdent(f, TrackerModel.progressiveFamilyTier f) else ident
                        upcast (boxCurrentBMP(shownIdent, None) |> Graphics.BMPtoImage)
            // Progressive Items: the plain swords are disabled (the progressive sword does their job)
            let progDisabled = not allItemsSelectable && ident <> boxCellCurrent && TrackerModel.progressiveDisabledIdent ident
            if progDisabled then
                fe.ToolTip <- "Disabled Due to Progressive Items"
                System.Windows.Controls.ToolTipService.SetShowOnDisabled(fe, true)
            let isSelectable = (allItemsSelectable && ident <> -2) || not progDisabled && (ident = -1 || (ident <> -2 && ident=boxCellCurrent) || (ident <> -2 &&  (itemMenuDomain.CanAddUse(ident) && not (ident = TrackerModel.ITEMS.WHITESWORD4 && not TrackerModelOptions.FourthSwordEnabled))))   // the 4th sword is disabled unless the 4th Sword start option is on
            yield fe, isSelectable, ident
        |]
    let originalStateIndex = gridIdents |> Array.tryFindIndex (fun ident -> ident = boxCellCurrent) |> Option.defaultValue emptyCellIdx
    let onClick(ea,ident) =
        // we're getting a click with mouse event args ea on one of the selectable items in the grid, namely ident. take appropriate action.
        DismissPopupWithResult(ident, 
            let newPH = MouseButtonEventArgsToPlayerHas ea
            if ident = -1 then 
                if originalPlayerHas=TrackerModel.PlayerHas.NO && newPH = TrackerModel.PlayerHas.SKIPPED then
                    TrackerModel.PlayerHas.SKIPPED   // middle click an empty box toggles it to white
                else
                    TrackerModel.PlayerHas.NO 
            else 
                newPH)
    let decorationsShouldGoToTheLeft = boxX > Graphics.OMTW*8.
    // the extra row goes above the original grid, so the original items stay where they always were; if that would run off the top of the window, shift the whole grid down instead
    let extraRowHeight = 60.   // two extra rows of 21 (cell) + 2*3 (border) above the original 4-row grid, plus 6 for the taller top (sword) row
    let wantedGridY = -3. - extraRowHeight   // the grid is 2 rows taller than the original 4-row grid, so it starts 2 rows higher
    let gridY = max wantedGridY (-(boxY+3.))
    let gridX = if decorationsShouldGoToTheLeft then -179. else 27.   // (the item menu is drawn 1.25x bigger, so the left-hand version starts further left to keep its right edge in place)
    let menuExtraH = 44.   // the 1.25x bigger menu is about 44px taller, so the things below it move down
    let customExtraH = float(gridRows - 6) * 34.   // each custom-icon row makes the menu taller, so the things below it move down
    let _decoX,_decoY = if decorationsShouldGoToTheLeft then -179., 108.+menuExtraH+customExtraH+(gridY-wantedGridY) else 27., 108.+menuExtraH+customExtraH+(gridY-wantedGridY)
    let redrawTile(ident) =
        // the user has changed the current selection via mousing or scrolling, redraw the preview tile appropriately to display ident
        let ident = if ident = -2 then -1 else ident   // -2 is a blank padding cell in the menu
        let innerc = redraw(ident)
        let s = if ident = -1 then "Unmarked" else TrackerModel.ITEMS.AsDisplayDescription(ident)
        let s = HotKeys.ItemHotKeyProcessor.AppendHotKeyToDescription(s, ident)
        let text = new TextBox(Text=s, Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, BorderThickness=Thickness(0.),
                                    FontSize=16., HorizontalContentAlignment=HorizontalAlignment.Center)
        let textBorder = new Border(BorderThickness=Thickness(3.), Child=text, Background=Brushes.Black, BorderBrush=Brushes.Gray)
        let dp = new DockPanel(LastChildFill=false)
        dp.Children.Add(textBorder) |> ignore
        innerc.Children.Add(dp) |> ignore
        if decorationsShouldGoToTheLeft then
            DockPanel.SetDock(textBorder, Dock.Right)
            Canvas.SetTop(dp, -3.)
            Canvas.SetRight(dp, 165.)
        else
            DockPanel.SetDock(textBorder, Dock.Right)
            Canvas.SetTop(dp, -3.)
            Canvas.SetLeft(dp, 165.)
    let extraDecorations = [yield! callerExtraDecorations]   // (the 'Have it / Don't want it / Don't have it' explainer box was removed)
    return! DoModalGridSelect(cm, boxX+3., boxY+3., innerc, gridElementsSelectablesAndIDs, originalStateIndex, activationDelta, (5, gridRows, 21, 21), 
                                17., 12., gridX, gridY, redrawTile, onClick, extraDecorations, itemBoxModalGridSelectBrushes, gridClickDismissalWarpReturn, None, "ItemBox", None)
    }

// version buttons that are currently glowing (start screen and main screen each have one); cleared once the user opens the patch notes
let versionGlowClearers = new System.Collections.Generic.List<unit -> unit>()
// ---- update check (GitHub releases) ----
let CurrentReleaseLabel = "2.2.0-beta17-hotfix1"   // bump for each release; it must match the tag you publish on GitHub (without the leading 'v')
let private parseTagForCompare(tag:string) =
    let t = tag.Trim().TrimStart('v','V')
    let main, pre = (match t.IndexOf('-') with | -1 -> t, "" | i -> t.Substring(0,i), t.Substring(i+1))
    let nums = main.Split('.') |> Array.map (fun p -> match System.Int32.TryParse(p) with | true, v -> v | _ -> 0) |> Array.toList
    let nums = nums @ List.replicate (max 0 (4 - nums.Length)) 0
    let preKey =
        if pre = "" then (1, "", 0, "")   // a final release sorts after any beta/rc of the same number
        else
            let letters = new string(pre |> Seq.takeWhile (fun c -> not (System.Char.IsDigit c)) |> Seq.toArray)
            let digits = pre.Substring(letters.Length)
            // (digits, then any suffix such as '-hotfix1': a suffixed tag sorts after the plain one, so beta13-hotfix1 is newer than beta13)
            let numText = new string(digits |> Seq.takeWhile System.Char.IsDigit |> Seq.toArray)
            (0, letters.ToLowerInvariant(), (match System.Int32.TryParse(numText) with | true, v -> v | _ -> 0), digits.Substring(numText.Length).ToLowerInvariant())
    nums, preKey
let isNewerRelease(candidateTag:string, currentLabel:string) = parseTagForCompare candidateTag > parseTagForCompare currentLabel
let mutable updateAvailable : (string*string) option = None   // (tag, release page url) of a newer release, once one has been found
let updateListeners = new System.Collections.Generic.List<(string*string) -> unit>()
// blocking: call from a background thread. Returns the newest non-draft release (pre-releases included), or None if it could not be determined.
let fetchLatestRelease() : (string*string) option =
    try
        System.Net.ServicePointManager.SecurityProtocol <- System.Net.ServicePointManager.SecurityProtocol ||| System.Net.SecurityProtocolType.Tls12
        use wc = new System.Net.WebClient()
        wc.Headers.Add("User-Agent", "ZoraTracker-update-check")
        let json = wc.DownloadString("https://api.github.com/repos/TheGobOne/ZoraTracker/releases?per_page=10")
        use doc = System.Text.Json.JsonDocument.Parse(json)
        let mutable best : (string*string) option = None
        for r in doc.RootElement.EnumerateArray() do
            let ok, d = r.TryGetProperty("draft")
            let isDraft = ok && d.GetBoolean()
            if not isDraft then
                let tag = r.GetProperty("tag_name").GetString()
                let url = r.GetProperty("html_url").GetString()
                match best with
                | Some(bt,_) when not (isNewerRelease(tag, bt)) -> ()
                | _ -> best <- Some(tag, url)
        best
    with _ -> None
// whenDone(latest, newer) runs on the UI thread: latest = newest release found (None if the check failed), newer = Some if it is newer than this build
let checkForUpdatesAsync(whenDone:(string*string) option -> (string*string) option -> unit) =
    System.Threading.ThreadPool.QueueUserWorkItem(fun _ ->
        let latest = fetchLatestRelease()
        let newer = match latest with | Some(tag,url) when isNewerRelease(tag, CurrentReleaseLabel) -> Some(tag,url) | _ -> None
        Application.Current.Dispatcher.BeginInvoke(new System.Action(fun () -> whenDone latest newer)) |> ignore) |> ignore
let mutable private updateCheckStarted = false
let startupUpdateCheck() =
    if not updateCheckStarted && TrackerModelOptions.CheckForUpdates.Value then
        updateCheckStarted <- true
        checkForUpdatesAsync (fun _ newer ->
            match newer with
            | Some n -> 
                updateAvailable <- Some n
                for l in updateListeners do l n
            | None -> ())

// ---- in-app update install ----
// the release's .zip asset url (only ever a download from this project's own GitHub releases), or None
let fetchReleaseZipUrl(tag:string) : string option =
    try
        System.Net.ServicePointManager.SecurityProtocol <- System.Net.ServicePointManager.SecurityProtocol ||| System.Net.SecurityProtocolType.Tls12
        use wc = new System.Net.WebClient()
        wc.Headers.Add("User-Agent", "ZoraTracker-update-check")
        let json = wc.DownloadString(sprintf "https://api.github.com/repos/TheGobOne/ZoraTracker/releases/tags/%s" (System.Uri.EscapeDataString tag))
        use doc = System.Text.Json.JsonDocument.Parse(json)
        let mutable found : string option = None
        for a in doc.RootElement.GetProperty("assets").EnumerateArray() do
            let name = a.GetProperty("name").GetString()
            let url = a.GetProperty("browser_download_url").GetString()
            if found.IsNone && not (isNull name) && name.EndsWith(".zip", System.StringComparison.OrdinalIgnoreCase)
               && not (isNull url) && url.StartsWith("https://github.com/TheGobOne/ZoraTracker/releases/download/") then
                found <- Some url
        found
    with _ -> None
// downloads the release zip, then hands over to a small PowerShell script that waits for this app to exit, unpacks the zip over the app folder (leaving the user's
// HotKeys.txt, settings file and custom images alone) and starts the new version. Returns an error message on failure, or None once the script is running (the caller then closes the app).
let downloadAndStageUpdate(zipUrl:string, onProgress:int -> unit) : string option =
    try
        let work = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ZoraTrackerUpdate")
        if System.IO.Directory.Exists(work) then System.IO.Directory.Delete(work, true)
        System.IO.Directory.CreateDirectory(work) |> ignore
        let zipPath = System.IO.Path.Combine(work, "update.zip")
        System.Net.ServicePointManager.SecurityProtocol <- System.Net.ServicePointManager.SecurityProtocol ||| System.Net.SecurityProtocolType.Tls12
        use wc = new System.Net.WebClient()
        wc.Headers.Add("User-Agent", "ZoraTracker-update-check")
        wc.DownloadProgressChanged.Add(fun ea -> onProgress ea.ProgressPercentage)
        wc.DownloadFileTaskAsync(new System.Uri(zipUrl), zipPath).Wait()
        let appDir = System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')
        let exe = System.IO.Path.Combine(appDir, "Z1R_WPF.exe")
        let q(p:string) = "'" + p.Replace("'", "''") + "'"
        let stage = System.IO.Path.Combine(work, "stage")
        let log = System.IO.Path.Combine(work, "update-log.txt")
        let script =
            String.concat "\r\n" [
                "$ErrorActionPreference = 'Stop'"
                sprintf "try {"
                sprintf "  Wait-Process -Id %d -ErrorAction SilentlyContinue" (System.Diagnostics.Process.GetCurrentProcess().Id)
                sprintf "  Start-Sleep -Seconds 1"
                // the install is a function so it can be run again: robocopy's result used to be ignored, so a locked file (e.g. held by an antivirus scan) left an old .dll beside the new .exe.
                // Now every .dll / .exe is checked after the copy; if any was not replaced it tries once more, then downloads the zip again and tries a last time, and only then gives up with a message
                sprintf "$dst = %s" (q appDir)
                "function Install {"
                sprintf "  if (Test-Path -LiteralPath %s) { Remove-Item -LiteralPath %s -Recurse -Force }" (q stage) (q stage)
                sprintf "  Expand-Archive -LiteralPath %s -DestinationPath %s -Force" (q zipPath) (q stage)
                sprintf "  $src = %s" (q stage)
                "  $d = @(Get-ChildItem -LiteralPath $src)"
                "  if ($d.Count -eq 1 -and $d[0].PSIsContainer) { $src = $d[0].FullName }"
                sprintf "  robocopy $src %s /E /R:30 /W:1 /XF HotKeys.txt Z1R_Tracker_settings.json /XD ShowRunCustomImages | Out-Null" (q appDir)
                "  $rc = $LASTEXITCODE"
                "  $bad = @()"
                "  if ($rc -ge 8) { $bad += ('robocopy exit code ' + $rc) }"
                "  Get-ChildItem -LiteralPath $src -Recurse -File | Where-Object { $_.Extension -in '.dll','.exe' } | ForEach-Object {"
                "    $rel = $_.FullName.Substring($src.Length).TrimStart('\\')"
                "    $t = Join-Path $dst $rel"
                "    if (-not (Test-Path -LiteralPath $t)) { $bad += $rel }"
                "    elseif ($_.Length -ne (Get-Item -LiteralPath $t).Length) { $bad += $rel }"
                "    elseif ($_.Length -lt 20000000 -and (Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $t).Hash) { $bad += $rel }"
                "  }"
                "  return ,$bad"
                "}"
                "$bad = Install"
                "if ($bad.Count -gt 0) { Start-Sleep -Seconds 5; $bad = Install }"
                "if ($bad.Count -gt 0) {"
                "  try {"
                "    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12"
                sprintf "    Invoke-WebRequest -UseBasicParsing -Uri %s -OutFile %s" (q zipUrl) (q zipPath)
                "  } catch { }"
                "  $bad = Install"
                "}"
                "if ($bad.Count -gt 0) {"
                sprintf "  $msg = 'The update could not be fully installed, even after trying again and downloading it again. These files could not be replaced: ' + ($bad -join ', ') + '. Close ZORA Tracker, any other program using its folder, and your antivirus scan, then extract the downloaded zip over this folder by hand: ' + %s" (q zipPath)
                sprintf "  $msg | Out-File -LiteralPath %s" (q log)
                "  Add-Type -AssemblyName PresentationFramework"
                "  [System.Windows.MessageBox]::Show($msg, 'ZORA Tracker update', 'OK', 'Warning') | Out-Null"
                "} else {"
                sprintf "  Start-Process -FilePath %s -WorkingDirectory %s" (q exe) (q appDir)
                "}"
                sprintf "} catch { $_ | Out-File -LiteralPath %s }" (q log)
            ]
        let scriptPath = System.IO.Path.Combine(work, "update.ps1")
        System.IO.File.WriteAllText(scriptPath, script)
        let psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", sprintf "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"%s\"" scriptPath)
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true
        System.Diagnostics.Process.Start(psi) |> ignore
        None
    with e -> Some e.Message

let makeVersionButtonWithBehavior(cm:CanvasManager) =
    // shows the current release (e.g. "v2.2-b3" for 2.2.0-beta3), so it only needs updating via CurrentReleaseLabel
    let shortLabel = CurrentReleaseLabel.Replace(".0-","-").Replace("-beta","-b")
    let vb = Graphics.makeButton(sprintf "v%s" shortLabel, Some(12.), Some(Brushes.Orange))
    // a green glow plus tooltip when a newer release is available on GitHub
    let showUpdateGlow(tag:string, _url:string) =
        vb.ToolTip <- sprintf "Update available: %s (click for details)" tag
        vb.Effect <- new System.Windows.Media.Effects.DropShadowEffect(ShadowDepth=0., BlurRadius=16., Opacity=1., Color=Colors.Lime)
    updateListeners.Add(showUpdateGlow)
    match updateAvailable with
    | Some n -> showUpdateGlow n
    | None -> ()
    startupUpdateCheck()
    vb.Click.Add(fun _ ->
        if TrackerModelOptions.LastSeenVersionNotes <> CurrentReleaseLabel then
            TrackerModelOptions.LastSeenVersionNotes <- CurrentReleaseLabel
            TrackerModelOptions.writeSettings()
            for f in versionGlowClearers do f()
            versionGlowClearers.Clear()
        if not popupIsActive then
            popupIsActive <- true
            async {
                let body, buttons = 
                    match updateAvailable with
                    | Some(tag,_) -> sprintf "%s\n*** A newer version is available: %s (you have v%s) ***\n" OverworldData.AboutBody tag CurrentReleaseLabel, ["Install update"; "Open folder"; "Go to website"; "Ok"]
                    | None -> OverworldData.AboutBody, ["Check for updates"; "Open folder"; "Go to website"; "Ok"]
                let! r = DoModalMessageBox(cm, null, body, buttons)
                popupIsActive <- false
                if r = "Go to website" then
                    System.Diagnostics.Process.Start(OverworldData.Website) |> ignore
                elif r = "Open folder" then
                    // open the folder the tracker is running from (where HotKeys.txt, the settings and the crash log live)
                    try System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("Explorer.exe", sprintf "\"%s\"" (System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')))) |> ignore with _ -> ()
                elif r = "Install update" then
                    match updateAvailable with
                    | Some(tag,url) -> 
                        popupIsActive <- true
                        let! ok = DoModalMessageBoxCore(cm, System.Drawing.SystemIcons.Question, 
                                                        sprintf "Download %s and install it over this copy?\n\nThis OVERWRITES your current version of ZORA Tracker with the new one.\nIf you want to keep your current version and have a separate install,\ndownload the release from GitHub instead (Go to website).\n\nThe tracker will close and reopen on the new version.\nYour hotkeys, settings and custom images will NOT be touched.\nAny game in progress is discarded (save it first)." tag,
                                                        ["Install and restart"; "Cancel"], 100., 300.)
                        if ok = "Install and restart" then
                            // a small progress popup while the zip downloads
                            let progressWh = new System.Threading.ManualResetEvent(false)
                            let bar = new System.Windows.Controls.ProgressBar(Width=320., Height=22., Minimum=0., Maximum=100., Value=0., Foreground=Brushes.Orange, Background=Brushes.Black, BorderBrush=Brushes.Orange)
                            let label = new TextBlock(Text="Downloading update...  0%", Foreground=Brushes.Orange, FontSize=18., Margin=Thickness(0.,0.,0.,8.))
                            let panel = new StackPanel(Orientation=Orientation.Vertical, Margin=Thickness(14.))
                            panel.Children.Add(label) |> ignore
                            panel.Children.Add(bar) |> ignore
                            let progressBorder = new Border(Child=panel, BorderBrush=Brushes.Orange, BorderThickness=Thickness(3.), Background=Brushes.Black)
                            progressBorder.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
                            let pcx = max 0. ((cm.AppMainCanvas.Width - progressBorder.DesiredSize.Width) / 2.)
                            let pcy = max 0. ((cm.AppMainCanvas.Height - progressBorder.DesiredSize.Height) / 2.)
                            progressBorder.SizeChanged.Add(fun _ ->
                                Canvas.SetLeft(progressBorder, max 0. ((cm.AppMainCanvas.Width - progressBorder.ActualWidth) / 2.))
                                Canvas.SetTop(progressBorder, max 0. ((cm.AppMainCanvas.Height - progressBorder.ActualHeight) / 2.)))
                            DoModal(cm, progressWh, pcx, pcy, progressBorder) |> Async.StartImmediate
                            let onProgress(pct:int) =
                                Application.Current.Dispatcher.BeginInvoke(new System.Action(fun () ->
                                    bar.Value <- float pct
                                    label.Text <- sprintf "Downloading update...  %d%%" pct)) |> ignore
                            let ctx = System.Threading.SynchronizationContext.Current
                            do! Async.SwitchToThreadPool()
                            let zip = fetchReleaseZipUrl tag
                            let result = match zip with | Some z -> downloadAndStageUpdate(z, onProgress) | None -> Some "no downloadable zip was found on that release"
                            do! Async.SwitchToContext ctx
                            progressWh.Set() |> ignore
                            match result with
                            | None -> Application.Current.MainWindow.Close()   // (the update script waits for this to exit, then copies the files and relaunches)
                            | Some err ->
                                let! _ = DoModalMessageBox(cm, System.Drawing.SystemIcons.Warning, sprintf "The update could not be installed automatically:\n%s\n\nThe release page will be opened instead." err, ["Ok"])
                                System.Diagnostics.Process.Start(if not (isNull url) && url.StartsWith(OverworldData.Website) then url else OverworldData.Website) |> ignore
                        popupIsActive <- false
                    | None -> ()
                elif r = "Check for updates" then
                    checkForUpdatesAsync (fun latest newer ->
                        let msg, _icon = 
                            match newer, latest with
                            | Some(tag,_), _ -> 
                                updateAvailable <- newer
                                for l in updateListeners do l newer.Value
                                sprintf "A newer version is available: %s\n(you have v%s)\nClick the version button again to download it." tag CurrentReleaseLabel, System.Drawing.SystemIcons.Information
                            | None, Some(_) -> sprintf "You are up to date (v%s)." CurrentReleaseLabel, System.Drawing.SystemIcons.Information
                            | None, None -> "Could not check for updates right now.\n(Are you offline?)", System.Drawing.SystemIcons.Warning
                        if not popupIsActive then
                            popupIsActive <- true
                            async {
                                let! _ = DoModalMessageBox(cm, null, msg, ["Ok"])
                                popupIsActive <- false
                            } |> Async.StartImmediate)
            } |> Async.StartImmediate
        )
    vb