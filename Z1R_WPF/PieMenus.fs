module PieMenus

open System.Windows.Controls
open System.Windows.Media
open System.Windows

open HotKeys.MyKey

let canvasAdd = Graphics.canvasAdd
let OMTW = Graphics.OMTW

let mutable pieDisabledDocks : Dock list = []   // set by the caller just before opening a pie menu: panels docked here are disabled (they can't be clicked or chosen by hotkey)
let FourWayPieMenu(cm,h,displaysDocksBehaviors:((Canvas*_)*_*_)[],hkp:HotKeys.HotKeyProcessor<int>) = async {
    let disabledDocks = pieDisabledDocks
    pieDisabledDocks <- []
    let wh = new System.Threading.ManualResetEvent(false)
    let c = new Canvas(IsHitTestVisible=true)
    let MARGIN = 10.
    let w = 16.*OMTW-2.*MARGIN
    let someDrawnPixelToSeeMouseMoves = new Canvas(Width=w, Height=h, Background=Brushes.Black, Opacity=0.01)
    c.Children.Add(someDrawnPixelToSeeMouseMoves) |> ignore
    let sbcs = ResizeArray()
    let mutable leftPanel,topPanel,rightPanel,bottomPanel = None,None,None,None
    let selfCleanupFuncs = ResizeArray()    
    for (p, setBorderColor), dock, behavior in displaysDocksBehaviors do
        match dock with
        | Dock.Left   -> match leftPanel   with | Some _ -> failwith "multiple lefts"   | None -> leftPanel   <- Some(setBorderColor, behavior)
        | Dock.Right  -> match rightPanel  with | Some _ -> failwith "multiple rights"  | None -> rightPanel  <- Some(setBorderColor, behavior)
        | Dock.Top    -> match topPanel    with | Some _ -> failwith "multiple tops"    | None -> topPanel    <- Some(setBorderColor, behavior)
        | Dock.Bottom -> match bottomPanel with | Some _ -> failwith "multiple bottoms" | None -> bottomPanel <- Some(setBorderColor, behavior)
        | _ -> failwith "bad Dock value"
        let dp = new DockPanel(Width=16.*OMTW-2.*MARGIN, Height=h, LastChildFill=false)
        DockPanel.SetDock(p, dock)
        dp.Children.Add(p) |> ignore
        selfCleanupFuncs.Add(fun () -> dp.Children.Clear())  // deparent the panels so we can reuse them
        canvasAdd(c, dp, MARGIN, MARGIN)
        sbcs.Add(setBorderColor)
    let onCloseOrDismiss() =
        sbcs |> Seq.iter (fun f -> f Brushes.Gray)
        for f in selfCleanupFuncs do f()
    let targetBrush = Brushes.Gray
    let innerH = h - 2.*(let (fe,_),_,_ = displaysDocksBehaviors.[0] in fe.Height)
    let g = new Grid(Width=w, Height=h)
    let circle = new Shapes.Ellipse(Width=innerH/1.5, Height=innerH/1.5, Stroke=targetBrush, StrokeThickness=3., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
    g.Children.Add(circle) |> ignore
    let outerCircle = new Shapes.Ellipse(Width=innerH*1.5, Height=innerH*1.5, Stroke=targetBrush, StrokeThickness=3., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
    outerCircle.StrokeDashArray <- new DoubleCollection([|1.;2.|])
    g.Children.Add(outerCircle) |> ignore
    let innerR = circle.Width/2.
    let outerR = outerCircle.Width/2.
    let root2 = 1.414
    let delta = outerR/root2 - innerR/root2
    let w = 2.*outerR/root2
    let lineCanvas = new Canvas(Width=w, Height=w)   // inscribed in the outer circle
    let slash1 = new Shapes.Line(X1=0., Y1=w, X2=delta, Y2=w-delta, Stroke=targetBrush, StrokeThickness=3., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
    lineCanvas.Children.Add(slash1) |> ignore
    let slash2 = new Shapes.Line(X1=w-delta, Y1=delta, X2=w, Y2=0., Stroke=targetBrush, StrokeThickness=3., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
    lineCanvas.Children.Add(slash2) |> ignore
    let backslash1 = new Shapes.Line(X1=0., Y1=0., X2=delta, Y2=delta, Stroke=targetBrush, StrokeThickness=3., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
    lineCanvas.Children.Add(backslash1) |> ignore
    let backslash2 = new Shapes.Line(X1=w-delta, Y1=w-delta, X2=w, Y2=w, Stroke=targetBrush, StrokeThickness=3., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
    lineCanvas.Children.Add(backslash2) |> ignore
    g.Children.Add(lineCanvas) |> ignore
    canvasAdd(c, g, MARGIN, MARGIN)
    let center = Point(8.*OMTW,h*0.5+MARGIN)
    let mutable currentSelection = -1
    let isDisabledSelection() = disabledDocks |> List.exists (fun d -> int d = currentSelection)
    c.MouseMove.Add(fun ea ->
        let pos = ea.GetPosition(c)
        let vector = Point.Subtract(pos, center)
        let distance = vector.Length
        sbcs |> Seq.iter (fun f -> f Brushes.Gray)
        currentSelection <- -1
        if distance > innerR then
            if vector.X > 0. && vector.X > abs(vector.Y) then
                match rightPanel with
                | Some(f,_) ->
                    f Brushes.Yellow
                    currentSelection <- int Dock.Right
                | None -> currentSelection <- -1
            elif vector.X < 0. && abs(vector.X) > abs(vector.Y) then
                match leftPanel with
                | Some(f,_) ->
                    f Brushes.Yellow
                    currentSelection <- int Dock.Left
                | None -> currentSelection <- -1
            elif vector.Y < 0. && abs(vector.Y) > abs(vector.X) then
                match topPanel with
                | Some(f,_) ->
                    f Brushes.Yellow
                    currentSelection <- int Dock.Top
                | None -> currentSelection <- -1
            elif vector.Y > 0. && vector.Y > abs(vector.X) then
                match bottomPanel with
                | Some(f,_) ->
                    f Brushes.Yellow
                    currentSelection <- int Dock.Bottom
                | None -> currentSelection <- -1
            else
                currentSelection <- -1
        else
            currentSelection <- -1
        )
    let doBehavior() =
        if currentSelection=int Dock.Left then
            match leftPanel with
            | Some(_,b) -> b()
            | None -> failwith "impossible"
        elif currentSelection=int Dock.Top then
            match topPanel with
            | Some(_,b) -> b()
            | None -> failwith "impossible"
        elif currentSelection=int Dock.Right then
            match rightPanel with
            | Some(_,b) -> b()
            | None -> failwith "impossible"
        elif currentSelection=int Dock.Bottom then
            match bottomPanel with
            | Some(_,b) -> b()
            | None -> failwith "impossible"
        else // cancel
            ()
    c.MyKeyAdd(fun ea ->
        ea.Handled <- true
        match hkp.TryGetValue(ea.Key) with
        | Some(which) -> 
            if which=0 then currentSelection <- int Dock.Bottom
            elif which=1 then currentSelection <- int Dock.Left
            elif which=2 then currentSelection <- int Dock.Top
            elif which=3 then currentSelection <- int Dock.Right
            else failwith "unexpected hkp which"
            if not(isDisabledSelection()) then
                doBehavior()
                wh.Set() |> ignore
        | None -> ()
        )
    let click(ea:Input.MouseEventArgs) =
        ea.Handled <- true
        doBehavior()
    c.MouseDown.Add(fun ea ->
        ea.Handled <- true
        if not(isDisabledSelection()) then   // clicking a disabled (crossed-out) panel does nothing
            click(ea)
            wh.Set() |> ignore
        )
(*  This was intended to let e.g. a single mousedown-mousemove-mouseup drag-right from TakeAny to select heart, but it it sometimes fires spuriously; commenting out requires two separate clicks (mousedowns)
    let mutable isFirstTimeMouseUp = true
    c.MouseUp.Add(fun ea ->
        if isFirstTimeMouseUp && currentSelection = -1 then
            isFirstTimeMouseUp <- false
        else
            ea.Handled <- true
            click(ea)
            wh.Set() |> ignore
        )
*)
    Graphics.WarpMouseCursorTo(center)
    //do! Async.Sleep(10)  // ensure the cursor is warped by yielding briefly       // no longer necessary now that Warp...() does a synchronous pump
    let tb = new TextBox(Text="Indicate which option you chose", Foreground=Brushes.Orange, Background=Brushes.Black, FontSize=16., IsHitTestVisible=false,
                            BorderThickness=Thickness(1.), TextAlignment=TextAlignment.Center, HorizontalAlignment=HorizontalAlignment.Center)
    canvasAdd(c, tb, 270., -15.)
    do! CustomComboBoxes.DoModal(cm, wh, 0., 16., c)
    onCloseOrDismiss()
    }

let takeAnyW = 330.
let takeAnyH = 220.
let BT = 5.
let rightMarginSize = 40.

let makeSkippedHeart() =
    let c = new Canvas(Width=30., Height=30., Background=Brushes.Black)
    canvasAdd(c, Graphics.BMPtoImage(Graphics.owHeartEmpty_bmp), 0., 0.)
    Graphics.placeSkippedItemXDecoration(c)
    c
let makeItemBox(itemBMP, yesno) =
    let c = new Canvas(Width=30., Height=30., Background=Brushes.Black)
    c.Children.Add(new Shapes.Rectangle(Width=30., Height=30., Stroke=yesno, StrokeThickness=3.0)) |> ignore
    canvasAdd(c, Graphics.BMPtoImage itemBMP, 4., 4.)
    c
let makeXtoY(x,y,rightMargin,group:StackPanel) =
    let row = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(0.,0.,rightMargin,0.))
    group.Children.Add(row) |> ignore
    row.Children.Add(x) |> ignore
    row.Children.Add(Graphics.BMPtoImage Graphics.iconRightArrow_bmp) |> ignore
    row.Children.Add(y) |> ignore

let TAKE_ANY = TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY
let SWORD1 = TrackerModel.MapSquareChoiceDomainHelper.SWORD1

// TAKE ANY ONE YOU WANT

let addHotKey(c:Canvas,keyOpt) =
    match keyOpt with
    | Some(pretty) ->
        let tb = new TextBox(Text=pretty, Foreground=Brushes.Orange, Background=Brushes.Black, FontSize=16., IsHitTestVisible=false,
                                BorderThickness=Thickness(1.), TextAlignment=TextAlignment.Center, HorizontalAlignment=HorizontalAlignment.Center)
        canvasAdd(c, tb, 125., 175.)
    | None -> ()

let takeAnyCandlePanel(keyOpt) = 
    let c1 = makeItemBox(Graphics.blue_candle_bmp,CustomComboBoxes.no)
    let c2 = makeItemBox(Graphics.blue_candle_bmp,CustomComboBoxes.yes)
    let col = new StackPanel(Orientation=Orientation.Vertical, Background=Brushes.Black)
    let group = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center)
    makeXtoY(Graphics.BMPtoImage Graphics.owHeartEmpty_bmp, makeSkippedHeart(), rightMarginSize, group)
    makeXtoY(Graphics.BMPtoImage Graphics.theInteriorBmpTable.[TAKE_ANY].[0], Graphics.BMPtoImage Graphics.theInteriorBmpTable.[TAKE_ANY].[1], rightMarginSize, group)
    makeXtoY(c1, c2, 0., group)
    col.Children.Add(group) |> ignore
    col.Children.Add(Graphics.BMPtoImage Graphics.takeAnyCandleBMP) |> ignore
    let b = new Border(Child=col, BorderBrush=Brushes.Gray, BorderThickness=Thickness(BT), Width=takeAnyW+2.*BT, Height=takeAnyH+2.*BT+30., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
    let c = new Canvas(Width=b.Width, Height=b.Height)
    c.Children.Add(b) |> ignore
    addHotKey(c,keyOpt)
    c, fun x -> b.BorderBrush <- x

let takeAnyPotionPanel(keyOpt) = 
    let col = new StackPanel(Orientation=Orientation.Vertical, Background=Brushes.Black)
    let group = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center)
    makeXtoY(Graphics.BMPtoImage Graphics.owHeartEmpty_bmp, makeSkippedHeart(), rightMarginSize, group)
    makeXtoY(Graphics.BMPtoImage Graphics.theInteriorBmpTable.[TAKE_ANY].[0], Graphics.BMPtoImage Graphics.theInteriorBmpTable.[TAKE_ANY].[1], 0., group)
    col.Children.Add(group) |> ignore
    col.Children.Add(Graphics.BMPtoImage Graphics.takeAnyPotionBMP) |> ignore
    let b = new Border(Child=col, BorderBrush=Brushes.Gray, BorderThickness=Thickness(BT), Width=takeAnyW+2.*BT, Height=takeAnyH+2.*BT+30., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
    let c = new Canvas(Width=b.Width, Height=b.Height)
    c.Children.Add(b) |> ignore
    addHotKey(c,keyOpt)
    c, fun x -> b.BorderBrush <- x

let takeAnyHeartPanel(keyOpt) = 
    let col = new StackPanel(Orientation=Orientation.Vertical, Background=Brushes.Black)
    let group = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center)
    makeXtoY(Graphics.BMPtoImage Graphics.owHeartEmpty_bmp, Graphics.BMPtoImage Graphics.owHeartFull_bmp, rightMarginSize, group)
    makeXtoY(Graphics.BMPtoImage Graphics.theInteriorBmpTable.[TAKE_ANY].[0], Graphics.BMPtoImage Graphics.theInteriorBmpTable.[TAKE_ANY].[1], 0., group)
    col.Children.Add(group) |> ignore
    col.Children.Add(Graphics.BMPtoImage Graphics.takeAnyHeartBMP) |> ignore
    let b = new Border(Child=col, BorderBrush=Brushes.Gray, BorderThickness=Thickness(BT), Width=takeAnyW+6., Height=takeAnyH+2.*BT+30., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
    let c = new Canvas(Width=b.Width, Height=b.Height)
    c.Children.Add(b) |> ignore
    addHotKey(c,keyOpt)
    c, fun x -> b.BorderBrush <- x

let takeAnyLeavePanel(keyOpt) = 
    let col = new StackPanel(Orientation=Orientation.Vertical, Background=Brushes.Black)
    let group = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center)
    makeXtoY(Graphics.BMPtoImage Graphics.theInteriorBmpTable.[TAKE_ANY].[0], Graphics.BMPtoImage Graphics.theInteriorBmpTable.[TAKE_ANY].[0], 0., group)
    col.Children.Add(Graphics.BMPtoImage Graphics.takeAnyLeaveBMP) |> ignore
    col.Children.Add(group) |> ignore
    let b = new Border(Child=col, BorderBrush=Brushes.Gray, BorderThickness=Thickness(BT), Width=takeAnyW+6., Height=takeAnyH+2.*BT+30., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
    let c = new Canvas(Width=b.Width, Height=b.Height)
    c.Children.Add(b) |> ignore
    addHotKey(c,keyOpt)
    c, fun x -> b.BorderBrush <- x

let mutable lastTakeAnyKind = 0   // what the last take-any popup chose: 0 = nothing, 1 = heart, 2 = candle, 3 = potion
let mutable lastTakeAnyDismissed = false   // the last take-any button box was dismissed without choosing anything (so the caller can leave the tile as it was)
let TakeAnyPieMenuAsync(cm,h) =
    let whichHeart() =
        if TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyHeart(0)=0 then 0
        elif TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyHeart(1)=0 then 1
        elif TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyHeart(2)=0 then 2
        elif TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyHeart(3)=0 then 3
        else 
            Graphics.ErrorBeepWithReminderLogText("All four take-any hearts have already been marked, so no marking will be made")
            -1
    let mutable r = false
    let candleBehavior() =
        let which = whichHeart()
        if which <> -1 then
            TrackerModel.playerProgressAndTakeAnyHearts.SetTakeAnyHeart(which, 2)
        TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle.Set(true)
        lastTakeAnyKind <- 2
        r <- true
    let potionBehavior() =
        let which = whichHeart()
        if which <> -1 then
            TrackerModel.playerProgressAndTakeAnyHearts.SetTakeAnyHeart(which, 2)
        lastTakeAnyKind <- 3
        r <- true
    let heartBehavior() =
        let which = whichHeart()
        if which <> -1 then
            TrackerModel.playerProgressAndTakeAnyHearts.SetTakeAnyHeart(which, 1)
        lastTakeAnyKind <- 1
        r <- true
    let hkp = HotKeys.TakeAnyHotKeyProcessor
    let bordersDocksBehaviors = [|
        takeAnyCandlePanel(hkp.AsPrettyHotKeyOpt(2)), Dock.Top,    candleBehavior
        takeAnyPotionPanel(hkp.AsPrettyHotKeyOpt(1)), Dock.Left,   potionBehavior
        takeAnyHeartPanel( hkp.AsPrettyHotKeyOpt(3)), Dock.Right,  heartBehavior
        takeAnyLeavePanel( hkp.AsPrettyHotKeyOpt(0)), Dock.Bottom, fun()->()
        |]
    async {
        do! FourWayPieMenu(cm, h, bordersDocksBehaviors, hkp)
        return r
    }

// IT'S DANGEROUS TO GO ALONE - TAKE THIS

let takeThisCandlePanel(keyOpt) = 
    let c1 = makeItemBox(Graphics.blue_candle_bmp,CustomComboBoxes.no)
    let c2 = makeItemBox(Graphics.blue_candle_bmp,CustomComboBoxes.yes)
    let col = new StackPanel(Orientation=Orientation.Vertical, Background=Brushes.Black)
    let group = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center)
    makeXtoY(c1, c2, rightMarginSize, group)
    makeXtoY(Graphics.BMPtoImage Graphics.theInteriorBmpTable.[SWORD1].[0], Graphics.BMPtoImage Graphics.theInteriorBmpTable.[SWORD1].[1], 0., group)
    col.Children.Add(group) |> ignore
    col.Children.Add(Graphics.BMPtoImage Graphics.takeThisCandleBMP) |> ignore
    let b = new Border(Child=col, BorderBrush=Brushes.Gray, BorderThickness=Thickness(BT), Width=takeAnyW+6., Height=takeAnyH+2.*BT+30., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
    let c = new Canvas(Width=b.Width, Height=b.Height)
    c.Children.Add(b) |> ignore
    addHotKey(c,keyOpt)
    c, fun x -> b.BorderBrush <- x

let takeThisWoodSwordPanel(keyOpt) = 
    let c1 = makeItemBox(Graphics.ow_sword1_item_bmp,CustomComboBoxes.no)
    let c2 = makeItemBox(Graphics.ow_sword1_item_bmp,CustomComboBoxes.yes)
    let col = new StackPanel(Orientation=Orientation.Vertical, Background=Brushes.Black)
    let group = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center)
    makeXtoY(c1, c2, rightMarginSize, group)
    makeXtoY(Graphics.BMPtoImage Graphics.theInteriorBmpTable.[SWORD1].[0], Graphics.BMPtoImage Graphics.theInteriorBmpTable.[SWORD1].[1], 0., group)
    col.Children.Add(group) |> ignore
    col.Children.Add(Graphics.BMPtoImage Graphics.takeThisWoodSwordBMP) |> ignore
    let b = new Border(Child=col, BorderBrush=Brushes.Gray, BorderThickness=Thickness(BT), Width=takeAnyW+6., Height=takeAnyH+2.*BT+30., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
    let c = new Canvas(Width=b.Width, Height=b.Height)
    c.Children.Add(b) |> ignore
    addHotKey(c,keyOpt)
    c, fun x -> b.BorderBrush <- x

let takeThisLeavePanel(keyOpt) = 
    let col = new StackPanel(Orientation=Orientation.Vertical, Background=Brushes.Black)
    let group = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center)
    makeXtoY(Graphics.BMPtoImage Graphics.theInteriorBmpTable.[SWORD1].[0], Graphics.BMPtoImage Graphics.theInteriorBmpTable.[SWORD1].[0], 0., group)
    col.Children.Add(Graphics.BMPtoImage Graphics.takeThisLeaveBMP) |> ignore
    col.Children.Add(group) |> ignore
    let b = new Border(Child=col, BorderBrush=Brushes.Gray, BorderThickness=Thickness(BT), Width=takeAnyW+6., Height=takeAnyH+2.*BT+30., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
    let c = new Canvas(Width=b.Width, Height=b.Height)
    c.Children.Add(b) |> ignore
    addHotKey(c,keyOpt)
    c, fun x -> b.BorderBrush <- x

let TakeThisPieMenuAsync(cm,h) =
    let mutable r = false
    let candleBehavior() =
        TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle.Set(true)
        r <- true
    let swordBehavior() =
        TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword.Set(true)
        r <- true
    let hkp = HotKeys.TakeThisHotKeyProcessor
    let bordersDocksBehaviors = [|
        takeThisWoodSwordPanel(hkp.AsPrettyHotKeyOpt(2)), Dock.Top,    swordBehavior
        takeThisCandlePanel(hkp.AsPrettyHotKeyOpt(1)),    Dock.Left,   candleBehavior
        takeThisLeavePanel(hkp.AsPrettyHotKeyOpt(0)),     Dock.Bottom, fun()->()
        |]
    async {
        do! FourWayPieMenu(cm, h, bordersDocksBehaviors, hkp)
        return r
    }

// the wood sword cave's pie menu: the usual three hotkey-able choices, plus a 4th (right) for the less common 'Shuffled Item' / 'Sword Icon' choices.
// returns 0 = leave/cancel, 1 = took sword, 2 = took candle, 3 = other (the caller asks which)
let takeThisOtherPanel() =
    let col = new StackPanel(Orientation=Orientation.Vertical, Background=Brushes.Black, VerticalAlignment=VerticalAlignment.Center)
    col.Children.Add(new TextBlock(Text="Shuffled item\nor Sword in Shop...", Foreground=Brushes.Orange, FontSize=20., TextAlignment=TextAlignment.Center, HorizontalAlignment=HorizontalAlignment.Center)) |> ignore
    let b = new Border(Child=col, BorderBrush=Brushes.Gray, BorderThickness=Thickness(BT), Width=takeAnyW+6., Height=takeAnyH+2.*BT+30., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
    let c = new Canvas(Width=b.Width, Height=b.Height)
    c.Children.Add(b) |> ignore
    c, fun x -> b.BorderBrush <- x

let mutable crossedOutChoices : string list = []   // set by the caller just before opening a cave choice box: these choices are shown crossed out and can't be picked (the box clears it)
// a giant red X over a panel, to show it is crossed out (disabled)
let addGiantRedX(c:Canvas) =
    let m = 14.
    for (x1,y1,x2,y2) in [ (m, m, c.Width-m, c.Height-m); (c.Width-m, m, m, c.Height-m) ] do
        c.Children.Add(new Shapes.Line(X1=x1, Y1=y1, X2=x2, Y2=y2, Stroke=Brushes.Red, StrokeThickness=14., StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round, Opacity=0.85, IsHitTestVisible=false)) |> ignore
let TakeThisChoicePieMenuAsync(cm,h) =
    let r = ref 0
    let hkp = HotKeys.TakeThisHotKeyProcessor
    let candleCrossed = List.contains "Took Candle" crossedOutChoices   // (left in place: the caller clears it, and the 'Shuffled Item' choice box reads it)
    let candlePanel = 
        let (c, setBorder) = takeThisCandlePanel(hkp.AsPrettyHotKeyOpt(1))
        if candleCrossed then
            addGiantRedX(c)
            pieDisabledDocks <- [Dock.Left]
            setBorder Brushes.Red
            c, (fun _ -> setBorder Brushes.Red)   // always a red border (never the yellow hover highlight)
        else
            c, setBorder
    let bordersDocksBehaviors = [|
        takeThisWoodSwordPanel(hkp.AsPrettyHotKeyOpt(2)), Dock.Top,    (fun () -> r.Value <- 1)
        candlePanel,                                      Dock.Left,   (fun () -> r.Value <- 2)
        takeThisOtherPanel(),                             Dock.Right,  (fun () -> r.Value <- 3)
        takeThisLeavePanel(hkp.AsPrettyHotKeyOpt(0)),     Dock.Bottom, fun()->()
        |]
    async {
        do! FourWayPieMenu(cm, h, bordersDocksBehaviors, hkp)
        return r.Value
    }

// the wood sword cave's chooser: one box of buttons that also answers the Contextual_TakeThis_* hotkeys (sword / candle / none).
// returns "Took Sword", "Took Candle", "Shuffled Item", "Sword Icon", "Leave" (the 'none' hotkey), or null if dismissed (clicked outside)
let TakeThisButtonBoxAsync(cm:CustomComboBoxes.CanvasManager) = async {
    let crossed = crossedOutChoices
    crossedOutChoices <- []
    let hkp = HotKeys.TakeThisHotKeyProcessor
    let wh = new System.Threading.ManualResetEvent(false)
    let result : string ref = ref null
    let col = new StackPanel(Orientation=Orientation.Vertical, Background=Brushes.Black, Margin=Thickness(16.))
    let addButton(text:string, keyOpt:string option, value:string) =
        let label = match keyOpt with | Some(k) -> sprintf "%s   [%s]" text k | None -> text
        let b = new Button(Height=34., MinWidth=280., Margin=Thickness(0.,4.,0.,4.), Focusable=false, Background=Graphics.almostBlack, BorderBrush=Brushes.Orange)
        b.Content <- new TextBox(Text=label, IsReadOnly=true, IsHitTestVisible=false, TextAlignment=TextAlignment.Center, VerticalContentAlignment=VerticalAlignment.Center, BorderThickness=Thickness(0.), FontSize=18., Foreground=Brushes.Orange, Background=Graphics.almostBlack)
        b.HorizontalContentAlignment <- HorizontalAlignment.Stretch
        b.VerticalContentAlignment <- VerticalAlignment.Stretch
        if List.contains value crossed then
            (b.Content :?> TextBox).TextDecorations <- TextDecorations.Strikethrough
            b.Opacity <- 0.5
        else
            b.Click.Add(fun _ -> result.Value <- value; wh.Set() |> ignore)
        col.Children.Add(b) |> ignore
    addButton("Took Sword", hkp.AsPrettyHotKeyOpt(2), "Took Sword")
    addButton("Took Candle", hkp.AsPrettyHotKeyOpt(1), "Took Candle")
    addButton("Shuffled Item", hkp.AsPrettyHotKeyOpt(3), "Shuffled Item")
    addButton("Sword in Shop", hkp.AsPrettyHotKeyOpt(4), "Sword Icon")
    let b = new Border(Child=col, Background=Brushes.Black, BorderThickness=Thickness(5.), BorderBrush=Brushes.Gray)
    b.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
    let c = new Canvas(Width=b.DesiredSize.Width, Height=b.DesiredSize.Height)
    c.Children.Add(b) |> ignore
    // hotkeys: 'sword' / 'candle' pick those, 'none' leaves the cave marked as not taken
    c.MyKeyAdd(fun ea ->
        ea.Handled <- true
        match hkp.TryGetValue(ea.Key) with
        | Some(which) ->
            if which=2 then result.Value <- "Took Sword"
            elif which=1 then result.Value <- "Took Candle"
            elif which=0 then result.Value <- "Leave"
            elif which=3 then result.Value <- "Shuffled Item"
            elif which=4 then result.Value <- "Sword Icon"
            if not(isNull result.Value) && List.contains result.Value crossed then result.Value <- null
            if not(isNull result.Value) then wh.Set() |> ignore
        | None -> ()
        )
    let areaW, areaH = cm.AppMainCanvas.Width, cm.AppMainCanvas.Height
    let cx = if areaW > 0. then max 0. ((areaW - c.Width) / 2.) else 150.
    let cy = if areaH > 0. then max 0. ((areaH - c.Height) / 2.) else 200.
    // measured before it was in the window, so re-center it once it has its real size
    b.SizeChanged.Add(fun _ ->
        c.Width <- b.ActualWidth
        c.Height <- b.ActualHeight
        if areaW > 0. then Canvas.SetLeft(c, max 0. ((areaW - b.ActualWidth) / 2.))
        if areaH > 0. then Canvas.SetTop(c, max 0. ((areaH - b.ActualHeight) / 2.)))
    do! CustomComboBoxes.DoModal(cm, wh, cx, cy, c)
    return result.Value
    }

// the Shop flyout (opened from the 'Shop' icon in the overworld menu): the blue potion shop, or one of the 4 numbered shops (each button also shows the items placed in that shop).
// counts.[n] = how many tiles already carry "SHOP n" (max 3 each); current = the shop number already on this tile (0 = none).
// returns -1 if dismissed, 0 = potion shop, 1-5 = that numbered shop
let ShopFlyoutAsync(cm:CustomComboBoxes.CanvasManager, counts:int[], _current:int, shopItems:(System.Drawing.Bitmap*bool) list[], potionOk:bool, addItem:(int*int -> int[] * (System.Drawing.Bitmap*bool) list[])) = async {
    let wh = new System.Threading.ManualResetEvent(false)
    let result = ref -1
    let row = new StackPanel(Orientation=Orientation.Horizontal, Background=Brushes.Black, Margin=Thickness(2.))
    let hkp = HotKeys.ShopHotKeyProcessor
    // (hotkey indexes: 0 = potion shop, 1-4 = that numbered shop)
    let itemPanels = new ResizeArray<int*StackPanel>()   // (shop index, the row showing what is in that shop), refreshed when an item hotkey adds something
    let countBlocks : TextBlock[] = Array.zeroCreate 5
    let fillItems(panel:StackPanel, items:(System.Drawing.Bitmap*bool) list) =
        panel.Children.Clear()
        for (bmp, collected) in items do
            let img = Graphics.BMPtoImage(bmp)
            img.Stretch <- Stretch.Uniform
            img.Width <- 21.
            img.Height <- 21.
            img.Margin <- Thickness(1.)
            if collected then img.Opacity <- 0.4
            panel.Children.Add(img) |> ignore
    let hoverButtons = new ResizeArray<Button*int>()   // (button, shop index) of the enabled ones, to find which shop an item hotkey is meant for
    let addButton(content:UIElement, value:int, enabled:bool) =
        // the content sits centered in the top part and the hotkey label is pinned to the bottom, so it lines up across all the buttons
        let g = new Grid()
        g.RowDefinitions.Add(new RowDefinition(Height=GridLength(1., GridUnitType.Star)))
        g.RowDefinitions.Add(new RowDefinition(Height=GridLength.Auto))
        (content :?> FrameworkElement).VerticalAlignment <- VerticalAlignment.Center
        Grid.SetRow(content, 0)
        g.Children.Add(content) |> ignore
        match hkp.AsPrettyHotKeyOpt(value) with
        | Some(k) ->
            let tb = new TextBlock(Text=sprintf "[%s]" k, Foreground=Brushes.Gray, FontSize=10., HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Bottom)
            Grid.SetRow(tb, 1)
            g.Children.Add(tb) |> ignore
        | None -> ()
        let b = new Button(MinHeight=56., MinWidth=32., Margin=Thickness(2.), Padding=Thickness(4.), Focusable=false, Background=Graphics.almostBlack, BorderBrush=Brushes.Orange)
        b.Content <- g
        b.HorizontalContentAlignment <- HorizontalAlignment.Stretch
        b.VerticalContentAlignment <- VerticalAlignment.Stretch
        if enabled then b.Click.Add(fun _ -> result.Value <- value; wh.Set() |> ignore)
        else b.Opacity <- 0.4
        if enabled then hoverButtons.Add((b, value))
        row.Children.Add(b) |> ignore
        b
    let potion = new StackPanel(Orientation=Orientation.Vertical, HorizontalAlignment=HorizontalAlignment.Center)
    let pimg = Graphics.BMPtoImage(Graphics.blue_potion_bmp)
    pimg.Stretch <- Stretch.Uniform
    pimg.Width <- 21.
    pimg.Height <- 27.
    // sized like the numbered shops: the potion on top (where their numbers are), then the row with what is in the shop (the middle item), then the hotkey at the bottom
    pimg.HorizontalAlignment <- HorizontalAlignment.Center
    potion.Children.Add(pimg) |> ignore
    let potionItems = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(0.,4.,0.,0.), MinHeight=21., MinWidth=23.)
    fillItems(potionItems, List.truncate 1 shopItems.[0])   // the potion shop holds just one item
    itemPanels.Add((0, potionItems))
    potion.Children.Add(potionItems) |> ignore
    let potionButton = addButton(potion, 0, potionOk)   // greyed out once all the potion shops are placed
    for n = 1 to 4 do
        let sp = new StackPanel(Orientation=Orientation.Vertical, HorizontalAlignment=HorizontalAlignment.Center)
        sp.Children.Add(new TextBlock(Text=string n, Foreground=Brushes.Orange, FontSize=24., FontWeight=FontWeights.Bold, HorizontalAlignment=HorizontalAlignment.Center)) |> ignore
        let cb = new TextBlock(Text=sprintf "x%d" counts.[n], Foreground=Brushes.Orange, FontSize=10., HorizontalAlignment=HorizontalAlignment.Center)
        countBlocks.[n] <- cb
        sp.Children.Add(cb) |> ignore
        // the items placed in this shop (collected ones are dimmed), a row under the count
        let items = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(0.,4.,0.,0.), MinHeight=21., MinWidth=72.)
        fillItems(items, shopItems.[n])
        itemPanels.Add((n, items))
        sp.Children.Add(items) |> ignore
        addButton(sp, n, true) |> ignore   // shops have no fixed max, so there is no limit; the count is just how many tiles carry it
    let b = new Border(Child=row, Background=Brushes.Black, BorderThickness=Thickness(5.), BorderBrush=Brushes.Gray)
    b.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
    let c = new Canvas(Width=b.DesiredSize.Width, Height=b.DesiredSize.Height)
    c.Children.Add(b) |> ignore
    // an item hotkey goes into the shop under the mouse: 10000 * (shop index + 1) + item id (no shop hovered: 1000 + item id, a plain item marker on the tile)
    let itemPick(ident:int) =
        match hoverButtons |> Seq.tryFind (fun (b, _) -> b.IsMouseOver) with
        | Some((_, shop)) ->
            // the item goes into that shop right away and the menu stays open (clicking a shop or its hotkey is what closes it)
            let (newCounts, newItems) = addItem(shop, ident)
            for (n, panel) in itemPanels do fillItems(panel, (if n = 0 then List.truncate 1 newItems.[n] else newItems.[n]))
            for n = 1 to 4 do countBlocks.[n].Text <- sprintf "x%d" newCounts.[n]
        | None ->
            result.Value <- 1000 + ident
            wh.Set() |> ignore
    c.MyKeyAdd(fun ea ->
        ea.Handled <- true
        match hkp.TryGetValue(ea.Key) with
        | Some(which) when which <> 0 || potionOk ->
            result.Value <- which
            wh.Set() |> ignore
        | Some(_) -> ()
        | None ->
            // the overworld item hotkeys (candle, arrow, bomb, ...) also work here: the item goes in the shop (returned as 1000 + the item id, like picking it from the tile menu)
            match HotKeys.OverworldHotKeyProcessor.TryGetValue(ea.Key) with
            | Some(st) when TrackerModel.MapSquareChoiceDomainHelper.IsItem(st) && st <> TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP ->
                let ident =
                    if st = TrackerModel.MapSquareChoiceDomainHelper.ARROW then TrackerModel.ITEMS.WOODARROW
                    elif st = TrackerModel.MapSquareChoiceDomainHelper.BOMB then TrackerModel.DECOR_BOMB
                    elif st = TrackerModel.MapSquareChoiceDomainHelper.BOOK then TrackerModel.ITEMS.BOOKSHIELD
                    elif st = TrackerModel.MapSquareChoiceDomainHelper.BLUE_CANDLE then TrackerModel.ITEMS.BLUECANDLE
                    elif st = TrackerModel.MapSquareChoiceDomainHelper.BLUE_RING then TrackerModel.ITEMS.BLUERING
                    elif st = TrackerModel.MapSquareChoiceDomainHelper.MEAT then TrackerModel.ITEMS.MEAT
                    elif st = TrackerModel.MapSquareChoiceDomainHelper.KEY then TrackerModel.ITEMS.OLDKEY
                    else TrackerModel.ITEMS.SHIELD
                itemPick ident
            | _ ->
                // the OverworldMarker_* and Item_* hotkeys work here too
                let markerIdent =
                    match HotKeys.OverworldMarkerHotKeyProcessor.TryGetValue(ea.Key) with
                    | Some(ident) when ident <> HotKeys.CLEAR_TILE_HOTKEY -> Some(ident)
                    | Some(_) -> None
                    | None ->
                        match HotKeys.ItemHotKeyProcessor.TryGetValue(ea.Key) with
                        | Some(item) when HotKeys.OverworldMarkerItems |> Array.contains item -> Some(item)
                        | _ -> None
                match markerIdent with
                | Some(ident) ->
                    itemPick ident
                | None -> ()
        )
    let areaW, areaH = cm.AppMainCanvas.Width, cm.AppMainCanvas.Height
    let cx = if areaW > 0. then max 0. ((areaW - c.Width) / 2.) else 150.
    let cy = if areaH > 0. then max 0. ((areaH - c.Height) / 2.) else 200.
    // the cursor jumps onto the potion shop button (the first one: the border is 5 wide, the row has a 2 margin, and the button a 2 margin)
    potionButton.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
    Graphics.WarpMouseCursorTo(Point(cx + 5. + 2. + 2. + potionButton.DesiredSize.Width / 2., cy + c.Height / 2.))
    do! CustomComboBoxes.DoModal(cm, wh, cx, cy, c)
    return result.Value
    }

// the take-any cave's chooser, in the same style as the wood sword cave's: one box of buttons that also answers the Contextual_TakeAnyBox_* hotkeys.
// returns true if the cave should be marked as done (took something; false for 'Took Nothing' or if dismissed)
let TakeAnyButtonBoxAsync(cm:CustomComboBoxes.CanvasManager) = async {
    let hkp = HotKeys.TakeAnyBoxHotKeyProcessor
    let pp = TrackerModel.playerProgressAndTakeAnyHearts
    let wh = new System.Threading.ManualResetEvent(false)
    let result : string ref = ref null
    let col = new StackPanel(Orientation=Orientation.Vertical, Background=Brushes.Black, Margin=Thickness(16.))
    // (hotkey indexes: 0 = nothing, 1 = potion, 2 = candle, 3 = heart)
    let addButton(text:string, hotkeyIndex:int) =
        let label = match hkp.AsPrettyHotKeyOpt(hotkeyIndex) with | Some(k) -> sprintf "%s   [%s]" text k | None -> text
        let b = new Button(Height=34., MinWidth=280., Margin=Thickness(0.,4.,0.,4.), Focusable=false, Background=Graphics.almostBlack, BorderBrush=Brushes.Orange)
        b.Content <- new TextBox(Text=label, IsReadOnly=true, IsHitTestVisible=false, TextAlignment=TextAlignment.Center, VerticalContentAlignment=VerticalAlignment.Center, BorderThickness=Thickness(0.), FontSize=18., Foreground=Brushes.Orange, Background=Graphics.almostBlack)
        b.HorizontalContentAlignment <- HorizontalAlignment.Stretch
        b.VerticalContentAlignment <- VerticalAlignment.Stretch
        b.Click.Add(fun _ -> result.Value <- text; wh.Set() |> ignore)
        col.Children.Add(b) |> ignore
    addButton("Took Heart", 3)
    addButton("Took Candle", 2)
    addButton("Took Potion", 1)
    addButton("Took Nothing", 0)
    let b = new Border(Child=col, Background=Brushes.Black, BorderThickness=Thickness(5.), BorderBrush=Brushes.Gray)
    b.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
    let c = new Canvas(Width=b.DesiredSize.Width, Height=b.DesiredSize.Height)
    c.Children.Add(b) |> ignore
    c.MyKeyAdd(fun ea ->
        ea.Handled <- true
        match hkp.TryGetValue(ea.Key) with
        | Some(which) ->
            if which=3 then result.Value <- "Took Heart"
            elif which=2 then result.Value <- "Took Candle"
            elif which=1 then result.Value <- "Took Potion"
            elif which=0 then result.Value <- "Took Nothing"
            if not(isNull result.Value) then wh.Set() |> ignore
        | None -> ()
        )
    let areaW, areaH = cm.AppMainCanvas.Width, cm.AppMainCanvas.Height
    let cx = if areaW > 0. then max 0. ((areaW - c.Width) / 2.) else 150.
    let cy = if areaH > 0. then max 0. ((areaH - c.Height) / 2.) else 200.
    // measured before it was in the window, so re-center it once it has its real size
    b.SizeChanged.Add(fun _ ->
        c.Width <- b.ActualWidth
        c.Height <- b.ActualHeight
        if areaW > 0. then Canvas.SetLeft(c, max 0. ((areaW - b.ActualWidth) / 2.))
        if areaH > 0. then Canvas.SetTop(c, max 0. ((areaH - b.ActualHeight) / 2.)))
    do! CustomComboBoxes.DoModal(cm, wh, cx, cy, c)
    let choice = result.Value
    lastTakeAnyDismissed <- isNull choice
    if isNull choice || choice = "Took Nothing" then
        return false
    else
        // same behavior as the old take-any picture menu: the next empty take-any heart slot is marked (an empty heart for the candle / potion, a full one for the heart)
        let which =
            if pp.GetTakeAnyHeart(0)=0 then 0
            elif pp.GetTakeAnyHeart(1)=0 then 1
            elif pp.GetTakeAnyHeart(2)=0 then 2
            elif pp.GetTakeAnyHeart(3)=0 then 3
            else 
                Graphics.ErrorBeepWithReminderLogText("All four take-any hearts have already been marked, so no marking will be made")
                -1
        if which <> -1 then
            pp.SetTakeAnyHeart(which, (if choice = "Took Heart" then 1 else 2))
        if choice = "Took Candle" then
            pp.PlayerHasBlueCandle.Set(true)
        lastTakeAnyKind <- (if choice = "Took Heart" then 1 elif choice = "Took Candle" then 2 else 3)
        return true
    }

// the other sword caves and the letter cave use the same style of choice box as the wood sword cave (without the hotkeys); returns the chosen label, or null if dismissed
let CaveChoiceBoxAsync(cm:CustomComboBoxes.CanvasManager, labels:string list) = async {
    let crossed = crossedOutChoices
    crossedOutChoices <- []
    let wh = new System.Threading.ManualResetEvent(false)
    let result : string ref = ref null
    let col = new StackPanel(Orientation=Orientation.Vertical, Background=Brushes.Black, Margin=Thickness(16.))
    let hkp = HotKeys.TakeThisHotKeyProcessor
    // the same Contextual_TakeThis_* hotkeys as the wood sword cave: 2=Took Sword, 3=Shuffled Item, 4=Sword Icon, 5=Took Letter
    let indexOf(label:string) = match label with | "Took Sword" -> 2 | "Shuffled Item" -> 3 | "Sword Icon" -> 4 | "Took Letter" -> 5 | _ -> -1
    for label in labels do
        let shown = if label = "Sword Icon" then "Sword in Shop" else label
        let displayLabel = match hkp.AsPrettyHotKeyOpt(indexOf label) with | Some(k) -> sprintf "%s   [%s]" shown k | None -> shown
        let b = new Button(Height=34., MinWidth=280., Margin=Thickness(0.,4.,0.,4.), Focusable=false, Background=Graphics.almostBlack, BorderBrush=Brushes.Orange)
        b.Content <- new TextBox(Text=displayLabel, IsReadOnly=true, IsHitTestVisible=false, TextAlignment=TextAlignment.Center, VerticalContentAlignment=VerticalAlignment.Center, BorderThickness=Thickness(0.), FontSize=18., Foreground=Brushes.Orange, Background=Graphics.almostBlack)
        b.HorizontalContentAlignment <- HorizontalAlignment.Stretch
        b.VerticalContentAlignment <- VerticalAlignment.Stretch
        if List.contains label crossed then
            (b.Content :?> TextBox).TextDecorations <- TextDecorations.Strikethrough
            b.Opacity <- 0.5
        else
            b.Click.Add(fun _ -> result.Value <- label; wh.Set() |> ignore)
        col.Children.Add(b) |> ignore
    let b = new Border(Child=col, Background=Brushes.Black, BorderThickness=Thickness(5.), BorderBrush=Brushes.Gray)
    b.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
    let c = new Canvas(Width=b.DesiredSize.Width, Height=b.DesiredSize.Height)
    c.Children.Add(b) |> ignore
    c.MyKeyAdd(fun ea ->
        ea.Handled <- true
        match hkp.TryGetValue(ea.Key) with
        | Some(which) ->
            match labels |> List.tryFind (fun l -> indexOf l = which) with
            | Some(l) when not(List.contains l crossed) -> result.Value <- l; wh.Set() |> ignore
            | Some(_) -> ()
            | None -> ()
        | None -> ()
        )
    let areaW, areaH = cm.AppMainCanvas.Width, cm.AppMainCanvas.Height
    let cx = if areaW > 0. then max 0. ((areaW - c.Width) / 2.) else 150.
    let cy = if areaH > 0. then max 0. ((areaH - c.Height) / 2.) else 200.
    // measured before it was in the window, so re-center it once it has its real size
    b.SizeChanged.Add(fun _ ->
        c.Width <- b.ActualWidth
        c.Height <- b.ActualHeight
        if areaW > 0. then Canvas.SetLeft(c, max 0. ((areaW - b.ActualWidth) / 2.))
        if areaH > 0. then Canvas.SetTop(c, max 0. ((areaH - b.ActualHeight) / 2.)))
    do! CustomComboBoxes.DoModal(cm, wh, cx, cy, c)
    return result.Value
    }

// a confirmation box in the same style as the wood sword cave menu: the message, then one wide button per choice; returns the chosen label, or null if dismissed
let MessageChoiceBoxAsync(cm:CustomComboBoxes.CanvasManager, message:string, labels:string list) = async {
    let wh = new System.Threading.ManualResetEvent(false)
    let result : string ref = ref null
    let col = new StackPanel(Orientation=Orientation.Vertical, Background=Brushes.Black, Margin=Thickness(if System.String.IsNullOrEmpty message then 8. else 16.))   // (an empty message gives just the buttons in a tight box)
    if not(System.String.IsNullOrEmpty message) then
        col.Children.Add(new TextBlock(Text=message, Foreground=Brushes.Orange, FontSize=20., TextWrapping=TextWrapping.Wrap, MaxWidth=480., TextAlignment=TextAlignment.Center, HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(0.,0.,0.,8.))) |> ignore
    for label in labels do
        let b = new Button(Height=34., MinWidth=280., Margin=Thickness(0.,4.,0.,4.), Focusable=false, Background=Graphics.almostBlack, BorderBrush=Brushes.Orange)
        b.Content <- new TextBox(Text=label, IsReadOnly=true, IsHitTestVisible=false, TextAlignment=TextAlignment.Center, VerticalContentAlignment=VerticalAlignment.Center, BorderThickness=Thickness(0.), FontSize=18., Foreground=Brushes.Orange, Background=Graphics.almostBlack)
        b.HorizontalContentAlignment <- HorizontalAlignment.Stretch
        b.VerticalContentAlignment <- VerticalAlignment.Stretch
        b.Click.Add(fun _ -> result.Value <- label; wh.Set() |> ignore)
        col.Children.Add(b) |> ignore
    let b = new Border(Child=col, Background=Brushes.Black, BorderThickness=Thickness(5.), BorderBrush=Brushes.Gray)
    b.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
    let c = new Canvas(Width=b.DesiredSize.Width, Height=b.DesiredSize.Height)
    c.Children.Add(b) |> ignore
    let areaW, areaH = cm.AppMainCanvas.Width, cm.AppMainCanvas.Height
    let cx = if areaW > 0. then max 0. ((areaW - c.Width) / 2.) else 150.
    let cy = if areaH > 0. then max 0. ((areaH - c.Height) / 2.) else 200.
    // measured before it was in the window, so re-center it once it has its real size
    b.SizeChanged.Add(fun _ ->
        c.Width <- b.ActualWidth
        c.Height <- b.ActualHeight
        if areaW > 0. then Canvas.SetLeft(c, max 0. ((areaW - b.ActualWidth) / 2.))
        if areaH > 0. then Canvas.SetTop(c, max 0. ((areaH - b.ActualHeight) / 2.)))
    do! CustomComboBoxes.DoModal(cm, wh, cx, cy, c)
    return result.Value
    }

// a small centered box with a title and a row of icon/text buttons; returns the key of the one clicked (null if dismissed)
let IconChoiceBoxAsync(cm:CustomComboBoxes.CanvasManager, title:string, items:(string*FrameworkElement) list) = async {
    let wh = new System.Threading.ManualResetEvent(false)
    let result : string ref = ref null
    let col = new StackPanel(Orientation=Orientation.Vertical, Background=Brushes.Black, Margin=Thickness(0.))
    if title <> "" then col.Children.Add(new TextBlock(Text=title, Foreground=Brushes.Orange, FontSize=20., HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(0.,8.,0.,0.))) |> ignore
    let row = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center)
    for (key, content) in items do
        let b = new Button(MinWidth=80., MinHeight=60., Margin=Thickness(6.), Padding=Thickness(8.), Focusable=false, Background=Graphics.almostBlack, BorderBrush=Brushes.Orange, Content=content)
        b.Click.Add(fun _ -> result.Value <- key; wh.Set() |> ignore)
        row.Children.Add(b) |> ignore
    col.Children.Add(row) |> ignore
    let b = new Border(Child=col, Background=Brushes.Black, BorderThickness=Thickness(5.), BorderBrush=Brushes.Gray)
    b.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
    let c = new Canvas(Width=b.DesiredSize.Width, Height=b.DesiredSize.Height)
    c.Children.Add(b) |> ignore
    let areaW, areaH = cm.AppMainCanvas.Width, cm.AppMainCanvas.Height
    let cx = if areaW > 0. then max 0. ((areaW - c.Width) / 2.) else 150.
    let cy = if areaH > 0. then max 0. ((areaH - c.Height) / 2.) else 200.
    // measured before it was in the window, so re-center it once it has its real size
    b.SizeChanged.Add(fun _ ->
        c.Width <- b.ActualWidth
        c.Height <- b.ActualHeight
        if areaW > 0. then Canvas.SetLeft(c, max 0. ((areaW - b.ActualWidth) / 2.))
        if areaH > 0. then Canvas.SetTop(c, max 0. ((areaH - b.ActualHeight) / 2.)))
    do! CustomComboBoxes.DoModal(cm, wh, cx, cy, c)
    return result.Value
    }
