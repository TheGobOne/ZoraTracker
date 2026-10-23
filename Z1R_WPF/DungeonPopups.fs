module DungeonPopups

open System
open System.Windows
open System.Windows.Controls 
open System.Windows.Media
open Z1R_Tracker.Models.Z1R_TrackerInterop


open CustomComboBoxes
open DungeonRoomState
let canvasAdd = Graphics.canvasAdd

let sunglassesOpacity = 0.85

let mutable THE_DIFF = 0.   // a kludge, until/unless I come up with a better way to thread the layout thru global popup logic

let mutable suppressRoomSyncTemporarily = false

let dungeonRoomExplainer, setOpacity =
    let mkTxt(txt) = new TextBox(Text=txt, Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, 
                                    BorderThickness=Thickness(0.), FontSize=16., VerticalAlignment=VerticalAlignment.Center)
    let border(fe) = new Border(Child=fe, BorderThickness=Thickness(1.), BorderBrush=Brushes.Gray)
    let marginAndSunglasses(c:FrameworkElement) = c.Margin <- Thickness(6.,0.,0.,0.); c.Opacity <- sunglassesOpacity; c
    let left(tb:TextBox) = 
        tb.HorizontalContentAlignment <- HorizontalAlignment.Left
        tb.HorizontalAlignment <- HorizontalAlignment.Left
        tb.Margin <- Thickness(3.,0.,0.,0.)
        tb
    let right(tb:TextBox) = 
        tb.HorizontalContentAlignment <- HorizontalAlignment.Right
        tb.HorizontalAlignment <- HorizontalAlignment.Right
        tb.Margin <- Thickness(0.,0.,3.,0.)
        tb
    let bg = Graphics.iconExtras_bmp.GetPixel(12,12)
    let ellipsis = Graphics.transformColor(Graphics.iconExtras_bmp, fun c -> if c.ToArgb()=bg.ToArgb() then System.Drawing.Color.Black else c)

    let dp = new DockPanel()
    let header = mkTxt("UI reminder: When mousing a room on the dungeon map...")
    header.HorizontalContentAlignment <- HorizontalAlignment.Center
    DockPanel.SetDock(header, Dock.Top)
    dp.Children.Add(header) |> ignore
    let g = makeGrid(3,4,250,33)
    g.ColumnDefinitions.[0].Width <- GridLength(218.)
    g.ColumnDefinitions.[1].Width <- GridLength(378.)
    g.ColumnDefinitions.[2].Width <- GridLength(172.)

    let tb = right <| mkTxt("Left-click")
    gridAdd(g, border(tb), 0, 0)
    let tb = left <| mkTxt("Toggle room brightness (or rotate lobby arrow)")
    gridAdd(g, border(tb), 1, 0)
    let sp = new StackPanel(Orientation=Orientation.Horizontal)
    let drs = DungeonRoomState()
    drs.RoomType <- RoomType.MaybePushBlock
    drs.IsComplete <- true
    sp.Children.Add(drs.CurrentDisplay()|>marginAndSunglasses) |> ignore
    let drs = DungeonRoomState()
    drs.RoomType <- RoomType.MaybePushBlock
    drs.IsComplete <- false
    sp.Children.Add(drs.CurrentDisplay()|>marginAndSunglasses) |> ignore
    gridAdd(g, border(sp), 2, 0)

    let tb = right <| mkTxt("Right-click")
    gridAdd(g, border(tb), 0, 1)
    let tb = left <| mkTxt("Invoke the 'Select a Room Type' popup menu")
    gridAdd(g, border(tb), 1, 1)
    let sp = new StackPanel(Orientation=Orientation.Horizontal)
    let drs = DungeonRoomState()
    drs.RoomType <- RoomType.MaybePushBlock
    drs.IsComplete <- false
    sp.Children.Add(drs.CurrentDisplay()|>marginAndSunglasses) |> ignore
    let drs = DungeonRoomState()
    drs.RoomType <- RoomType.Chevy
    drs.IsComplete <- false
    sp.Children.Add(drs.CurrentDisplay()|>marginAndSunglasses) |> ignore
    let drs = DungeonRoomState()
    drs.RoomType <- RoomType.Transport1
    drs.IsComplete <- false
    sp.Children.Add(drs.CurrentDisplay()|>marginAndSunglasses) |> ignore
    sp.Children.Add(ellipsis |> Graphics.BMPtoImage) |> ignore
    gridAdd(g, border(sp), 2, 1)

    let tb = right <| mkTxt("Scroll-up/shift-left-click")
    gridAdd(g, border(tb), 0, 2)
    let tb = left <| mkTxt("Invoke the 'Select a Monster Detail' popup menu")
    gridAdd(g, border(tb), 1, 2)
    let sp = new StackPanel(Orientation=Orientation.Horizontal)
    let drs = DungeonRoomState()
    drs.RoomType <- RoomType.MaybePushBlock
    drs.IsComplete <- false
    sp.Children.Add(drs.CurrentDisplay()|>marginAndSunglasses) |> ignore
    let drs = DungeonRoomState()
    drs.RoomType <- RoomType.MaybePushBlock
    drs.IsComplete <- false
    drs.MonsterDetail <- MonsterDetail.Gleeok
    sp.Children.Add(drs.CurrentDisplay()|>marginAndSunglasses) |> ignore
    let drs = DungeonRoomState()
    drs.RoomType <- RoomType.MaybePushBlock
    drs.IsComplete <- false
    drs.MonsterDetail <- MonsterDetail.Digdogger
    sp.Children.Add(drs.CurrentDisplay()|>marginAndSunglasses) |> ignore
    sp.Children.Add(ellipsis |> Graphics.BMPtoImage) |> ignore
    gridAdd(g, border(sp), 2, 2)

    let tb = right <| mkTxt("Scroll-down/shift-right-click")
    gridAdd(g, border(tb), 0, 3)
    let tb = left <| mkTxt("Invoke the 'Select a Floor Drop Detail' popup menu")
    gridAdd(g, border(tb), 1, 3)
    let sp = new StackPanel(Orientation=Orientation.Horizontal)
    let drs = DungeonRoomState()
    drs.RoomType <- RoomType.MaybePushBlock
    drs.IsComplete <- false
    sp.Children.Add(drs.CurrentDisplay()|>marginAndSunglasses) |> ignore
    let drs = DungeonRoomState()
    drs.RoomType <- RoomType.MaybePushBlock
    drs.IsComplete <- false
    drs.FloorDropDetail <- FloorDropDetail.Triforce
    sp.Children.Add(drs.CurrentDisplay()|>marginAndSunglasses) |> ignore
    let drs = DungeonRoomState()
    drs.RoomType <- RoomType.MaybePushBlock
    drs.IsComplete <- false
    drs.FloorDropDetail <- FloorDropDetail.BombPack
    sp.Children.Add(drs.CurrentDisplay()|>marginAndSunglasses) |> ignore
    sp.Children.Add(ellipsis |> Graphics.BMPtoImage) |> ignore
    gridAdd(g, border(sp), 2, 3)

    dp.Children.Add(g) |> ignore
    (new Border(Child=dp, BorderThickness=Thickness(0.,3.,0.,3.), BorderBrush=Brushes.DimGray, Background=Brushes.Black) :> FrameworkElement),
        fun op -> dp.Opacity <- op

let DoMonsterDetailPopup(cm:CanvasManager, boxX, boxY, currentMonsterDetail) = async {
    let innerc = new Canvas(Width=18., Height=18., Background=Brushes.Black)  // just has monster icon drawn on it, not the box
    Graphics.SilentlyWarpMouseCursorTo(Point(boxX+12., boxY+12.))
    let redraw(md:MonsterDetail) =
        innerc.Children.Clear()
        let bmp = md.Bmp()
        if bmp <> null then
            let image = Graphics.BMPtoImage(bmp)
            image.Opacity <- sunglassesOpacity
            canvasAdd(innerc, image, 0., 0.)
        innerc
    redraw(currentMonsterDetail) |> ignore
    let all = MonsterDetailExtensions.DisplayOrder()
    let gridElementsSelectablesAndIDs = [|
        for detail in all do
            

            let fe: FrameworkElement =
                if detail.IsNotMarked() then upcast new Canvas()
                else upcast (detail.Bmp() |> Graphics.BMPtoImage)


            yield fe, true, detail
    |]

    let originalStateIndex =
        match all |> Array.tryFindIndex ((=) currentMonsterDetail) with
        | Some i -> i
        | None -> 0

    let onClick(_ea,ident) =
        // we're getting a click with mouse event args ea on one of the selectable items in the grid, namely ident. take appropriate action.
        DismissPopupWithResult(ident)
    // the grid comes up where the room type menu does: same bottom edge, centered on it (the room menu is 7x5 cells of 39x27, shown 4/3 larger); this one is 8x4 cells of 18x18, shown 2x
    let ST = CustomComboBoxes.borderThickness
    let roomG = 4./3.
    let roomW, _roomH = 7.*(39.+2.*ST)*roomG, 5.*(27.+2.*ST)*roomG
    let roomLeft = 555. - 117. - (roomW - roomW/roomG)
    let roomBottom = (768. + 60.) - THE_DIFF - 78.
    let gridW, gridH = (8.*(18.+2.*ST)+2.*ST)*2., (4.*(18.+2.*ST)+2.*ST)*2.
    let gridX = roomLeft + (roomW - gridW)/2. + 20. - (boxX+3.)   // (nudged 20 to the right)
    let gridY = roomBottom - gridH - (boxY+3.)
    let redrawTile(ident) =
        // the user has changed the current selection via mousing or scrolling, redraw the preview tile appropriately to display ident
        let innerc = redraw(ident)
        let s = ident.DisplayDescription()
        let s = HotKeys.DungeonRoomHotKeyProcessor.AppendHotKeyToDescription(s, Choice2Of4 ident)
        // the monster's name goes under the right part of the grid (the shift-drag tip box goes under the left part)
        let text = new TextBox(Text=s, Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, BorderThickness=Thickness(0.),
                                    FontSize=16., HorizontalContentAlignment=HorizontalAlignment.Center, Width=gridW-170.-6.)
        let textBorder = new Border(BorderThickness=Thickness(3.), Child=text, Background=Brushes.Black, BorderBrush=Brushes.Gray)
        let (rx, ry, rw, rh) = CustomComboBoxes.MovableGridRect   // (the grid can be moved and resized)
        let hidden = TrackerModelOptions.HidePopupHintAndLock.Value   // with the tip box and lock bar hidden, the name goes across the whole bottom
        text.Width <- max 80. (if hidden then rw - 6. else rw - 150. - 6.)
        let dp = new DockPanel(LastChildFill=false)
        dp.Children.Add(textBorder) |> ignore
        innerc.Children.Add(dp) |> ignore
        Canvas.SetTop(dp, ry + rh + 8.)
        Canvas.SetLeft(dp, (if hidden then rx else rx + 150.))   // right-aligned under the grid, the tip box takes the left part
    let extraDecorations = 
        [
            dungeonRoomExplainer, -3.-boxX, 342.-boxY-THE_DIFF
        ]
    setOpacity(0.8)


    return! DoModalGridSelect(cm, boxX+3., boxY+3., innerc, gridElementsSelectablesAndIDs, originalStateIndex, 0, (8, 4, 18, 18), 9., 9., gridX, gridY, 
                                redrawTile, onClick, extraDecorations, itemBoxModalGridSelectBrushes, WarpToCenter, None, "MonsterDetail", None)
    }

let DoFloorDropDetailPopup(cm:CanvasManager, boxX, boxY, currentFloorDropDetail) = async {
    let innerc = new Canvas(Width=18., Height=18., Background=Brushes.Black)  // just has floordrop icon drawn on it, not the box
    Graphics.SilentlyWarpMouseCursorTo(Point(boxX+9., boxY+9.))
    let redraw(fd:FloorDropDetail) =
        innerc.Children.Clear()
        let bmp = fd.Bmp()
        if bmp <> null then
            let image = Graphics.BMPtoImage(bmp)
            image.Opacity <- sunglassesOpacity
            canvasAdd(innerc, image, 0., 0.)
        innerc
    redraw(currentFloorDropDetail) |> ignore
    let all = FloorDropDetailExtensions.DisplayOrder()
    let gridElementsSelectablesAndIDs =
        let temp =
            FloorDropDetailExtensions.DisplayOrder()
            |> Array.map (fun detail ->
                let fe: FrameworkElement =
                    if detail.IsNotMarked() then upcast new Canvas()
                    else upcast (detail.Bmp() |> Graphics.BMPtoImage)
                fe, true, detail
            )
        // Force alignment to a 3x3 grid if needed
        if temp.Length < 9 then
            Array.append temp [| for _ in 1 .. (9 - temp.Length) -> (null, false, FloorDropDetail.Unmarked) |]
        else temp


    let originalStateIndex = Array.IndexOf(all, currentFloorDropDetail)
    let onClick(_ea,ident) =
        // we're getting a click with mouse event args ea on one of the selectable items in the grid, namely ident. take appropriate action.
        DismissPopupWithResult(ident)
    let gridX, gridY = 21., -3.
    let redrawTile(ident) =
        // the user has changed the current selection via mousing or scrolling, redraw the preview tile appropriately to display ident
        let innerc = redraw(ident)
        let s = ident.DisplayDescription()
        let s = HotKeys.DungeonRoomHotKeyProcessor.AppendHotKeyToDescription(s, Choice3Of4 ident)
        let (rx, ry, rw, rh) = CustomComboBoxes.MovableGridRect   // (the grid can be moved and resized)
        // the name goes right under the grid (the shift-drag tip box goes under that)
        let text = new TextBox(Text=s, Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, BorderThickness=Thickness(0.),
                                    FontSize=16., HorizontalContentAlignment=HorizontalAlignment.Center, Width=max 80. (rw-6.))
        let textBorder = new Border(BorderThickness=Thickness(3.), Child=text, Background=Brushes.Black, BorderBrush=Brushes.Gray)
        let dp = new DockPanel(LastChildFill=false)
        dp.Children.Add(textBorder) |> ignore
        innerc.Children.Add(dp) |> ignore
        Canvas.SetTop(dp, ry + rh + 8.)
        Canvas.SetLeft(dp, rx)
    let extraDecorations = 
        [
            dungeonRoomExplainer, -3.-boxX, 342.-boxY-THE_DIFF
        ]
    setOpacity(0.8)
    return! DoModalGridSelect(cm, boxX+3., boxY+3., innerc, gridElementsSelectablesAndIDs, originalStateIndex, 0, (3, 3, 18, 18), 9., 9., gridX, gridY, 
                                redrawTile, onClick, extraDecorations, itemBoxModalGridSelectBrushes, WarpToCenter, None, "FloorDropDetail", None)
    }

let brushes = (new CustomComboBoxes.ModalGridSelectBrushes(Brushes.Lime, Brushes.Lime, Brushes.Red, Brushes.Gray)).Dim(0.6)
let DoDungeonRoomSelectPopup(cm:CustomComboBoxes.CanvasManager, originalRoomState:DungeonRoomState, usedTransports:_[], setNewValue, positionAtEntranceRoomIcons, gridClickDismissalWarpReturn) = async {
    let tweak(im:Image) = im.Opacity <- 0.65; im

    let popupCanvas = cm.CreatePopup(0.75)
    let workingCopy = originalRoomState.Clone()
    workingCopy.CSharp.IsPreview <- true // for rendering only

    let makeCaption(txt, centered) = 
        let tb = new TextBox(Text=txt, FontSize=16., Foreground=Brushes.Orange, Background=Brushes.Black, BorderThickness=Thickness(0.), IsReadOnly=true, IsHitTestVisible=false,
                                VerticalAlignment=VerticalAlignment.Center)
        if centered then tb.TextAlignment <- TextAlignment.Center
        tb

    let tileX, tileY = 555., 768. + 60.   // (moved down 60 now that the room preview is gone: low enough to clear the UI-reminder overlay at the top, without a big gap above the menu)
    let ST = CustomComboBoxes.borderThickness
    let tileCanvas = new Canvas(Width=1., Height=1.)   // (no room preview is shown any more; this just hosts the captions)
    let G = 4./3.   // the room type menu is shown 33% larger

    let grid = 
        [|
        RoomType.DoubleMoat; RoomType.CircleMoat; RoomType.LifeOrMoney; RoomType.BombUpgrade; RoomType.HungryGoriyaMeatBlock; RoomType.StartEnterFromE; RoomType.Gannon;
        RoomType.TopMoat; RoomType.Chevy; RoomType.OldManHint; RoomType.VChute; RoomType.HChute; RoomType.StartEnterFromN; RoomType.Zelda;
        RoomType.RightMoat; RoomType.Unmarked; RoomType.MaybePushBlock; RoomType.NonDescript; RoomType.Tee; RoomType.StartEnterFromS; RoomType.LavaMoat;
        RoomType.ItemBasement; RoomType.StaircaseToUnknown; RoomType.Transport1; RoomType.Transport2; RoomType.Transport3; RoomType.StartEnterFromW; RoomType.OffTheMap;
        RoomType.Transport4; RoomType.Transport5; RoomType.Transport6; RoomType.Transport7; RoomType.Transport8; RoomType.Turnstile; RoomType.PrincessRoom;
        |]

    let gridElementsSelectablesAndIDs : (FrameworkElement * bool * RoomType)[] =
        grid |> Array.mapi (fun _i rt ->
            let isLegal =
                (rt = originalRoomState.RoomType) ||
                (match Option.ofNullable (rt.KnownTransportNumber()) with
                 | None -> true
                 | Some n -> usedTransports.[n] <> 2)

            if rt = RoomType.OldManHint then upcast (let im = Graphics.BMPtoImage(rt.UncompletedBI()) in im.Opacity <- 1.0; im), isLegal, rt   // this icon is naturally dark, so show it undimmed
            else upcast tweak(Graphics.BMPtoImage(rt.UncompletedBI())), isLegal, rt
        )

    let originalStateIndex =
        match grid |> Array.tryFindIndex (fun x -> x = originalRoomState.RoomType) with
        | Some i -> i
        | None -> grid |> Array.findIndex (fun x -> x = RoomType.Unmarked)


    let activationDelta = 0
    let (gnc, gnr, gcw, grh) = 7, 5, 13*3, 9*3
    let totalGridWidth = float gnc * (float gcw + 2. * ST) * G
    let totalGridHeight = float gnr * (float grh + 2. * ST) * G
    let gx, gy = -117. - (totalGridWidth - totalGridWidth/G), -78. - totalGridHeight

    let fullRoom = originalRoomState.Clone()
    fullRoom.IsComplete <- false

    let redrawTile(curState:RoomType) =
        tileCanvas.Children.Clear()
        // the caption frame follows the (movable, resizable) grid
        let (mx, my, mw, mh) = CustomComboBoxes.MovableGridRect
        let haveRect = mw > 0.
        let textWidth = if haveRect then mw + 13. else totalGridWidth + 13.
        let gridH = if haveRect then mh else totalGridHeight
        let topText = Graphics.center(makeCaption("Select a room type", true), int textWidth, 24)
        let s = HotKeys.DungeonRoomHotKeyProcessor.AppendHotKeyToDescription(Z1R_Tracker.Models.Z1R_TrackerInterop.RoomTypeExtensions.DisplayDescription(curState), Choice1Of4 curState)
        let bottomText = Graphics.center(makeCaption(s, true), int textWidth, 49)

        let dp = new DockPanel(Width=textWidth, Height=gridH+75., LastChildFill=false)
        DockPanel.SetDock(topText, Dock.Top)
        DockPanel.SetDock(bottomText, Dock.Bottom)
        dp.Children.Add(topText) |> ignore
        dp.Children.Add(bottomText) |> ignore
        let frame = new Border(BorderBrush=Brushes.DimGray, BorderThickness=Thickness(3.), Background=Brushes.Black, Child=dp)
        let sp = new StackPanel(Orientation=Orientation.Vertical)
        sp.Children.Add(frame) |> ignore
        if haveRect then canvasAdd(tileCanvas, sp, mx+mw/2.-textWidth/2., my+gridH+51.-dp.Height)
        else canvasAdd(tileCanvas, sp, gx+totalGridWidth/2.-textWidth/2., -27.-dp.Height)

    let onClick(ea:Input.MouseButtonEventArgs, curState) = 
        if (ea.ChangedButton = Input.MouseButton.Left || ea.ChangedButton = Input.MouseButton.Right) && ea.ButtonState = Input.MouseButtonState.Pressed then
            CustomComboBoxes.DismissPopupWithResult(curState)
        else
            CustomComboBoxes.StayPoppedUp

    let extraDecorations = [| dungeonRoomExplainer, -3.-tileX, 342.-tileY |]
    setOpacity(0.5)

    // where the (possibly moved / resized by the user) grid actually is, so the mouse lands on the right cell
    let placeScale, placeAbs =
        let inv = System.Globalization.CultureInfo.InvariantCulture
        let tryNum(s:string) = match System.Double.TryParse(s, System.Globalization.NumberStyles.Float, inv) with | true, v -> Some v | _ -> None
        let s =
            match TrackerModelOptions.MenuLayouts.TryGetValue("DungeonRoomPlacement") with
            | true, v when not (isNull v) && v.Length = 3 -> (match tryNum v.[2] with | Some c -> max 0.5 (min 4.0 c) | None -> 1.0)
            | _ -> 1.0
        let abs =
            match TrackerModelOptions.MenuLayouts.TryGetValue("DungeonRoomPlacementAbsV2") with
            | true, v when not (isNull v) && v.Length = 2 ->
                match tryNum v.[0], tryNum v.[1] with
                | Some a, Some b ->
                    let appW, appH = cm.AppMainCanvas.Width, cm.AppMainCanvas.Height
                    Some((if appW > 0. then max 0. (min a (appW - 80.)) else a), (if appH > 0. then max 50. (min b (appH - 60.)) else b))
                | _ -> None
            | _ -> None
        s, abs
    let cellPoint(cx:float, cy:float) =
        match placeAbs with
        | Some(ax, ay) -> Point(ax + G*placeScale*(ST + cx*(float gcw + ST*2.)), ay + G*placeScale*(ST + cy*(float grh + ST*2.)))
        | None -> Point(tileX+gx+cx*(float gcw + ST*2.)*G*placeScale, tileY+gy-THE_DIFF+cy*(float grh + ST*2.)*G*placeScale)
    if positionAtEntranceRoomIcons then
        if placeAbs.IsNone && placeScale = 1.0 then
            Graphics.WarpMouseCursorTo(Point(tileX+gx+5.5*(float gcw + ST*2.)*G, tileY+gy+totalGridHeight/2.))
        else
            Graphics.WarpMouseCursorTo(cellPoint(5.5, float gnr/2.))
    else
        // the grid may have been rearranged by the user (Shift+drag); find where the current room type is displayed
        let shownIndex =
            match TrackerModelOptions.MenuLayouts.TryGetValue("DungeonRoom") with
            | true, saved when not (isNull saved) ->
                let k = System.Array.IndexOf(saved, (originalRoomState.RoomType).ToString())
                if k >= 0 && k < gnc*gnr then k else originalStateIndex
            | _ -> originalStateIndex
        let x = shownIndex % gnc
        let y = shownIndex / gnc
        Graphics.WarpMouseCursorTo(cellPoint(float x + 0.5, float y + 0.5))

    let! r = CustomComboBoxes.DoModalGridSelect(cm, tileX, tileY-THE_DIFF, tileCanvas, gridElementsSelectablesAndIDs, originalStateIndex, activationDelta, (gnc, gnr, gcw, grh),
                   float gcw/2., float grh/2., gx, gy, redrawTile, onClick, extraDecorations, brushes, gridClickDismissalWarpReturn, None, "DungeonRoom", None)

    match r with
    | None -> ()
    | Some(curState) -> 
        workingCopy.CSharp.IsPreview <- false
        workingCopy.RoomType <- curState
        workingCopy.IsComplete <- (curState <> RoomType.BombUpgrade)   // a placed bomb upgrade room starts un-collected (blue dot on the level number); left click it to clear the dot
        suppressRoomSyncTemporarily <- false
        setNewValue(workingCopy)
        async {
            do! Async.Sleep(250)
        } |> Async.StartImmediate


        //originalRoomState.RoomType <- curState
        //originalRoomState.IsComplete <- true
        //setNewValue(originalRoomState)

    popupCanvas.Children.Clear()
    cm.DismissPopup()

}
