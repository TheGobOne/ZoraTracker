module Layout

open System
open System.Windows
open System.Windows.Controls 
open System.Windows.Media
open OverworldItemGridUI            // has canvasAdd, etc
open OverworldMapTileCustomization  // has a lot of constants

let RIGHT_COL = 410.
let WEBCAM_LINE = OMTW*16.-200.  // height of upper area is 150, so 200 wide is 4x3 box in upper right; timer and other controls here could be obscured
let kittyWidth = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H - THRU_MAIN_MAP_H

// a small underlined 'Discord' text that opens the given url in the default browser when clicked
let DiscordLinkWidth = 70.
let RacingLinkWidth = 112.
let MakeLinkText(text:string, url:string, width:float) : UIElement =
    let tb = new TextBlock(Text=text, FontSize=10., Foreground=Brushes.Orange, TextDecorations=TextDecorations.Underline, TextAlignment=TextAlignment.Center,
                            Width=width, Background=Brushes.Transparent, Cursor=System.Windows.Input.Cursors.Hand, ToolTip=url)
    tb.MouseEnter.Add(fun _ -> tb.Foreground <- Brushes.Yellow)
    tb.MouseLeave.Add(fun _ -> tb.Foreground <- Brushes.Orange)
    tb.MouseLeftButtonUp.Add(fun _ ->
        try
            let psi = new System.Diagnostics.ProcessStartInfo(url)
            psi.UseShellExecute <- true
            System.Diagnostics.Process.Start(psi) |> ignore
        with _ -> ())
    upcast tb
let MakeDiscordLink(url:string) : UIElement = MakeLinkText("ZORA Discord", url, DiscordLinkWidth)

// keep the 'Item Progress' label horizontally centered between the version button (left) and the first progress icon (right); needs real sizes, so done on SizeChanged
let centerItemProgressLabel(ip:UIElement, vb:UIElement) =
    match ip, vb with
    | (:? FrameworkElement as ipf), (:? FrameworkElement as vbf) ->
        let recenter() =
            if ipf.ActualWidth > 0. && vbf.ActualWidth > 0. then
                Canvas.SetLeft(ipf, (vbf.ActualWidth + ITEM_PROGRESS_FIRST_ITEM - ipf.ActualWidth) / 2.)
        ipf.SizeChanged.Add(fun _ -> recenter())
        vbf.SizeChanged.Add(fun _ -> recenter())
    | _ -> ()

// center an element horizontally on x=cx (needs its real width, so done on SizeChanged); null-safe
let centerHorizontallyAt(e:UIElement, cx:float) =
    match e with
    | :? FrameworkElement as f ->
        let recenter() = if f.ActualWidth > 0. then Canvas.SetLeft(f, cx - f.ActualWidth / 2.)
        f.SizeChanged.Add(fun _ -> recenter())
    | _ -> ()
// center the Hotkeys button horizontally under the Hint Decoder button (needs both real widths, so done on SizeChanged)
let mutable hintDecoderElementForCentering : UIElement = null
let mutable hotkeysElementForCentering : UIElement = null
let mutable hotkeysCenteringHooked = false
let tryCenterHotkeysUnderHintDecoder(hintLeft:float) =
    if not hotkeysCenteringHooked && hintDecoderElementForCentering <> null && hotkeysElementForCentering <> null then
        match hintDecoderElementForCentering, hotkeysElementForCentering with
        | (:? FrameworkElement as h), (:? FrameworkElement as t) ->
            hotkeysCenteringHooked <- true
            let recenter() = if h.ActualWidth > 0. && t.ActualWidth > 0. then Canvas.SetLeft(t, hintLeft + (h.ActualWidth - t.ActualWidth) / 2.)
            h.SizeChanged.Add(fun _ -> recenter())
            t.SizeChanged.Add(fun _ -> recenter())
        | _ -> ()
// the four small icons at the top left (? / eyeball / save / mirror) are all made the same size as the mirror icon, in a 2x2 block;
// the zones/coords checkboxes below are pushed down if the block needs more room
let mutable iconQuestion : UIElement = null
let mutable iconEye : UIElement = null
let mutable iconSave : UIElement = null
let mutable iconOptions : UIElement = null   // the Options / Draw / log buttons, stacked just above the Item Locations button
let mutable iconDraw : UIElement = null
let mutable iconLog : UIElement = null
let mutable iconItemLocations : UIElement = null   // the Item Locations button, the top button of the Save / Hint Decoder / Hotkeys column
let mutable iconMirror : UIElement = null
let mutable zonesCheckBoxEl : UIElement = null
let mutable coordsCheckBoxEl : UIElement = null
let mutable zonesCoordsNatural : (float*float*float*float) option = None   // unscaled sizes of the zones / coords checkboxes
let mutable topLeftIconsHooked = false
let SMALL_BUTTON_W = 84.   // width of the Save / Hint Decoder / Hotkeys column (it has to fit between the OW stats and the Pause/Reset timer column)
let mutable rightButtonForSizing : UIElement = null
let PANEL_X0 = WEBCAM_LINE-44.   // (3px left of the old column so the panel isn't against the window edge)
// // left column of the control panel (TRACK / TOOLS)
let PANEL_LW = 114.
let PANEL_X1 = PANEL_X0 + PANEL_LW + 13.   // right column (TIMER / FILE)
let PANEL_RW = PANEL_LW
// the panel buttons' look (from the approved mockup): dark fill, thin gray border, 4px rounded corners, orange border on hover
let panelButtonTemplate =
    let xaml = "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='bd' Background='#171717' BorderBrush='#3A3A3A' BorderThickness='1' CornerRadius='4' SnapsToDevicePixels='True'><ContentPresenter HorizontalAlignment='Stretch' VerticalAlignment='Stretch'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='#FFA31A'/><Setter TargetName='bd' Property='Background' Value='#241A0A'/></Trigger></ControlTemplate.Triggers></ControlTemplate>"
    System.Windows.Markup.XamlReader.Parse(xaml) :?> ControlTemplate
// startup screen: the same button look as the control panel (rounded 8px), and option boxes (checkbox areas) with a matching rounded border + orange hover
let startupButtonTemplate =
    let xaml = "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='bd' Background='#171717' BorderBrush='#3A3A3A' BorderThickness='1' CornerRadius='8' SnapsToDevicePixels='True'><ContentPresenter HorizontalAlignment='Stretch' VerticalAlignment='Stretch'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='#FFA31A'/><Setter TargetName='bd' Property='Background' Value='#241A0A'/></Trigger></ControlTemplate.Triggers></ControlTemplate>"
    System.Windows.Markup.XamlReader.Parse(xaml) :?> ControlTemplate
let styleStartupButton(e:UIElement) =
    match e with
    | :? Button as b ->
        b.Template <- startupButtonTemplate
        (match b.Content with
         | :? TextBox as t -> t.Background <- Brushes.Transparent
         | _ -> ())
    | _ -> ()
let optionBorderStyle =
    let xaml = "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Border'><Setter Property='BorderBrush' Value='#3A3A3A'/><Setter Property='Background' Value='#171717'/><Setter Property='CornerRadius' Value='8'/><Style.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='BorderBrush' Value='#FFA31A'/><Setter Property='Background' Value='#241A0A'/></Trigger></Style.Triggers></Style>"
    System.Windows.Markup.XamlReader.Parse(xaml) :?> Style
let mutable iconSpot : UIElement = null
let mutable iconShowRun : UIElement = null
let mutable iconFQSQ : UIElement = null
let panelHeading(text:string) =
    new TextBlock(Text=text, FontSize=8., FontWeight=FontWeights.SemiBold, Foreground=new SolidColorBrush(Color.FromRgb(143uy,143uy,143uy)), TextAlignment=TextAlignment.Center, IsHitTestVisible=false)
let panelHeadings = [ panelHeading("TRACK"), PANEL_X0, 0., PANEL_LW; panelHeading("TOOLS"), PANEL_X0, 32., PANEL_LW; panelHeading("TIMER"), PANEL_X1, 0., PANEL_RW; panelHeading("OVERWORLD OPTIONS"), PANEL_X0, 118., PANEL_LW; panelHeading("FILE"), PANEL_X1, 86., PANEL_RW ]
let panelDivider = new System.Windows.Shapes.Rectangle(Width=1., Height=148., Fill=new SolidColorBrush(Color.FromRgb(38uy,38uy,38uy)), IsHitTestVisible=false)
let addPanelDecor(add:UIElement -> unit) =
    for tb, _, _, _ in panelHeadings do add(tb)
    add(panelDivider)
let mutable topLeftPlace : unit -> unit = fun () -> ()
let hookTopLeftOnSizeChange(e:UIElement) =
    match e with
    | :? FrameworkElement as f -> f.SizeChanged.Add(fun _ -> topLeftPlace())
    | _ -> ()
let tryLayoutTopLeftIcons() =
    if not topLeftIconsHooked && iconQuestion <> null && iconEye <> null && iconSave <> null && iconMirror <> null then
        match iconMirror with
        | :? FrameworkElement as m ->
            topLeftIconsHooked <- true
            let place() =
                if m.ActualWidth > 0. && m.ActualHeight > 0. then
                    let w, h = m.ActualWidth, m.ActualHeight
                    for e in [iconQuestion; iconEye] do
                        match e with
                        | :? FrameworkElement as f -> 
                            f.Width <- w
                            f.Height <- h
                        | _ -> ()
                    // everything above the Hint Decoder button is centered horizontally on that button
                    let hintLeft = WEBCAM_LINE-41.
                    let hintCx = hintLeft + SMALL_BUTTON_W / 2.
                    let blockLeft = hintCx - (2.*w + 2.) / 2.
                    Canvas.SetLeft(iconQuestion, blockLeft)
                    Canvas.SetTop(iconQuestion, 0.)
                    Canvas.SetLeft(iconEye, blockLeft + w + 2.)
                    Canvas.SetTop(iconEye, 0.)
                    // the ? / eyeball / mirror icons are hidden (still laid out, so the measurements above keep working); only the Save button is shown
                    for e in [iconQuestion; iconEye; iconMirror] do e.Visibility <- Visibility.Hidden
                    Canvas.SetLeft(iconMirror, blockLeft + w + 2.)
                    Canvas.SetTop(iconMirror, h + 2.)
                    // zones and coords sit at the very top of the screen; Save goes right above the Hint Decoder button, and Hotkeys/Save/Hint Decoder are all the same size
                    match zonesCheckBoxEl, coordsCheckBoxEl with
                    | (:? FrameworkElement as z), (:? FrameworkElement as cc) when z.ActualHeight > 0. && cc.ActualHeight > 0. ->
                        // zones and coords sit side by side in the open strip above the item grid (centered over the armos / letter / ladder item boxes, the first three grid columns) and scaled down to fit
                        let areaX, areaW, areaY, areaH = OW_ITEM_GRID_LOCATIONS.OFFSET + 2., 86., 3., 24.
                        if not zonesCoordsNatural.IsSome && not(z.LayoutTransform :? ScaleTransform) then
                            zonesCoordsNatural <- Some(z.ActualWidth, z.ActualHeight, cc.ActualWidth, cc.ActualHeight)
                        match zonesCoordsNatural with
                        | Some(zw, zh, cw, ch) ->
                            let gap = 4.
                            let s = min 1. (min ((areaW - gap) / (zw + cw)) (areaH / (max zh ch)))
                            let want = new ScaleTransform(s, s)
                            let cur = (match z.LayoutTransform with :? ScaleTransform as t -> t.ScaleX | _ -> 1.)
                            if abs (cur - s) > 0.001 then
                                z.LayoutTransform <- want
                                cc.LayoutTransform <- new ScaleTransform(s, s)
                            let totalW = (zw + cw) * s + gap
                            let left = areaX + (areaW - totalW) / 2.
                            Canvas.SetLeft(z, left)
                            Canvas.SetTop(z, areaY + (areaH - zh * s) / 2.)
                            Canvas.SetLeft(cc, left + zw * s + gap)
                            Canvas.SetTop(cc, areaY + (areaH - ch * s) / 2.)
                            Canvas.SetZIndex(z, 100)
                            Canvas.SetZIndex(cc, 100)
                        | None -> ()
                    | _ -> ()
                    // two-column control panel: left TRACK / TOOLS, right TIMER (+ Pause/Reset level with Spot Summary) / FILE; every button the same size
                    let items : (UIElement * float * float) list =
                        [ iconItemLocations, PANEL_X0, 10.   // (the 'Popout Windows' button; Spot Summary and Show Custom now live in its menu)
                          hintDecoderElementForCentering, PANEL_X0, 42.
                          hotkeysElementForCentering, PANEL_X0, 60.
                          iconLog, PANEL_X0, 78.
                          iconDraw, PANEL_X0, 96.
                          rightButtonForSizing, PANEL_X1, 60.
                          iconFQSQ, PANEL_X0, 128.
                          iconOptions, PANEL_X1, 96.
                          iconSave, PANEL_X1, 114. ]
                    for e in [iconSpot; iconShowRun] do (if e <> null then e.Visibility <- Visibility.Collapsed)
                    for e, x, y in items do
                        match e with
                        | :? FrameworkElement as f ->
                            (match f with
                             | :? Button as b ->
                                (match b.Content with
                                 | :? TextBox as t ->
                                    t.VerticalContentAlignment <- VerticalAlignment.Center; t.VerticalAlignment <- VerticalAlignment.Stretch; t.HorizontalAlignment <- HorizontalAlignment.Stretch
                                    t.FontSize <- 11.; t.Padding <- Thickness(0.); t.RenderTransform <- null; t.Background <- Brushes.Transparent
                                 | _ -> ())
                                b.Template <- panelButtonTemplate
                             | _ -> ())
                            f.SnapsToDevicePixels <- true
                            f.UseLayoutRounding <- true
                            if f.Width <> PANEL_LW then f.Width <- PANEL_LW
                            if f.Height <> 16. then f.Height <- 16.
                            Canvas.SetLeft(f, x)
                            Canvas.SetTop(f, y)
                        | _ -> ()
                    for tb, x, y, w in panelHeadings do
                        Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y); tb.Width <- w
                    Canvas.SetLeft(panelDivider, PANEL_X0 + PANEL_LW + 6.); Canvas.SetTop(panelDivider, 0.)
            m.SizeChanged.Add(fun _ -> place())
            topLeftPlace <- place
            place()
        | _ -> ()
// center an element vertically on y=cy (needs its real height, so done on SizeChanged); null-safe
let centerVerticallyAt(e:UIElement, cy:float) =
    match e with
    | :? FrameworkElement as f ->
        let recenter() = if f.ActualHeight > 0. then Canvas.SetTop(f, cy - f.ActualHeight / 2.)
        f.SizeChanged.Add(fun _ -> recenter())
    | _ -> ()
// the item progress icons are drawn 3px into their 30px-high canvas, so the middle of the progress row is 15px below the canvas top
let ITEM_PROGRESS_ROW_CENTER_Y = THRU_MAP_AND_LEGEND_H + 3. + 15.
// the 'Item Progress' button is centered vertically on the sword icon (the first icon in the row, drawn 3px into the canvas)
let SWORD_ICON_CENTER_Y = THRU_MAP_AND_LEGEND_H + 3. + 3. + float(Graphics.swordLevelToBmp(0).Height) / 2.
// x center of the span between the inner left edge of the blue ring's red box and the inner right edge of the money hover icon's box (grid columns 4..7)
let OW_STATS_CENTER_X = OW_ITEM_GRID_LOCATIONS.OFFSET + 180.
// the 'OW spots left / gettable / cave highlight' block sits in the open space just right of the item grid (top, above the Zones/Coords checkboxes)
// the 'OW spots left' text (and its per-type breakdown) sits a little further right, clear of the fifth item box / hint icon above the grid's top row
let OW_SPOTS_LEFT_CENTER_X = OW_ITEM_GRID_LOCATIONS.OFFSET + 197.
let OW_STATS_BLOCK_CENTER_X = OW_ITEM_GRID_LOCATIONS.OFFSET + 294.

type IApplicationLayoutBase =
    abstract member AddMainTracker : mainTracker:UIElement -> unit
    abstract member AddNumberedTriforceCanvas : triforceCanvas:Canvas * i:int -> unit 
    abstract member AddItemGridStuff : owItemGrid:UIElement * toggleBookShieldCheckBox:UIElement * bookIsAtlasCheckBox:UIElement * highlightOpenCavesCheckBox:UIElement * 
                                            timerResetButton:UIElement * spotSummaryTB:UIElement * mirrorOW:UIElement * hideIconsEyeball:UIElement * moreFQSQoptionsButton:Button * ws_ms_bu_toggleButton:UIElement -> unit
    abstract member AddHideQuestCheckboxes : hideFirstQuestCheckBox:UIElement * hideSecondQuestCheckBox:UIElement -> unit
    abstract member AddLinkRouting : linkIcon:UIElement * currentTargetIcon:UIElement -> unit
    abstract member AddWebcamLine : unit -> unit
    abstract member AddOverworldCanvas : overworldCanvas:UIElement -> unit
    abstract member AddLegend : legendCanvas:UIElement -> unit
    abstract member AddItemProgress : itemProgressCanvas:UIElement * itemProgressTB:UIElement -> unit
    abstract member AddVersionButton : vb:UIElement -> unit
    abstract member AddHintDecoderButton : tb:UIElement -> unit
    abstract member AddOverworldOptionsButton : fqsqButton:UIElement -> unit
    abstract member AddExtraSiteLinks : zoraSiteLink:UIElement * racingSiteLink:UIElement -> unit
    abstract member AddKittyAndLogo : kitty:Image * logoBorder:UIElement * ztlogo:Image * discordLink:UIElement * racingLink:UIElement -> unit
    abstract member AddShowHotKeysButton : showHotKeysButton:UIElement -> unit
    abstract member AddShowRunCustomButton : showRunCustomButton:UIElement -> unit
    abstract member AddSaveButton : saveButton:UIElement -> unit
    abstract member AddItemLocationsButton : itemLocationsButton:UIElement -> unit
    abstract member AddTopLeftButtons : optionsButton:UIElement * drawButton:UIElement * logButton:UIElement -> unit
    abstract member AddQuestReferenceChart : chart:UIElement * x:float * y:float -> unit
    abstract member AddUserCustomContentButton : uccButton:UIElement -> unit
    abstract member AddDungeonTabsOverlay : dungeonTabsOverlay:UIElement -> unit
    abstract member AddDungeonTabs : dungeonTabsWholeCanvas:UIElement -> unit
    abstract member GetDungeonY : unit -> float
    abstract member AddBlockers : blockerGrid:UIElement -> unit
    abstract member AddNotesSeedsFlags : notesTextBox:UIElement * seedAndFlagsDisplayCanvas:UIElement -> unit
    abstract member AddExtraDungeonRightwardStuff : grabModeTextBlock:UIElement * rightwardCanvas:UIElement -> unit
    abstract member AddOWRemainingScreens : owRemainingScreensTextBox:UIElement -> unit
    abstract member AddOWGettableScreens : owGettableScreensCheckBox:UIElement -> unit
    abstract member AddCurrentMaxHearts : currentMaxHeartsTextBox:UIElement -> unit
    abstract member AddShowCoords : showCoordsCB:UIElement * havePotionLetterImage:Image -> unit
    abstract member AddOWZoneOverlay : zone_checkbox:UIElement -> unit
    abstract member AddMouseHoverExplainer : mouseHoverExplainerIcon:UIElement * c:UIElement -> unit
    abstract member AddLinkTarget : currentTargetGhostBuster:UIElement -> unit
    abstract member AddTimeline : t1c:UIElement * t2c:UIElement * t3c:UIElement-> unit
    abstract member GetTimelineBounds : unit -> Rect
    abstract member GetFullAppBounds : unit -> Rect
    abstract member AddReminderDisplayOverlay : reminderDisplayOuterDockPanel:UIElement -> unit
    abstract member AddPostGameDecorationCanvas : postgameDecorationCanvas:UIElement -> unit
    abstract member AddSpotSummary : spotSummaryCanvas:UIElement -> unit
    abstract member AddDiskIcon : diskIcon:UIElement -> unit
    abstract member AllDone : unit -> unit
    abstract member AddTopLayerHover : fe:FrameworkElement * x:float * y:float -> unit
    abstract member ClearTopLayerHovers : unit -> unit
    abstract member FocusOverworld : unit -> unit
    abstract member FocusDungeon : unit -> unit
    abstract member ShowMouseWarp : Point -> unit
    abstract member IsShort : bool

type ApplicationLayout(cm:CustomComboBoxes.CanvasManager) =
    let appMainCanvas = cm.AppMainCanvas
    let mutable itemProgressLabelForCentering : UIElement = null
    let topLayerHoverCanvas = new Canvas()   // for temporary bits that appear on hover, to ensure they appear on top of everything else in the app, when no other popup active
    let ctxt = System.Threading.SynchronizationContext.Current  // this class is created on the UI thread
    interface IApplicationLayoutBase with
        member this.AddMainTracker(mainTracker) =
            canvasAdd(appMainCanvas, mainTracker, 0., 0.)
        member this.AddNumberedTriforceCanvas(triforceCanvas, i) =
            canvasAdd(appMainCanvas, triforceCanvas, OW_ITEM_GRID_LOCATIONS.OFFSET+30.*float i, 0.)
        member this.AddItemGridStuff(owItemGrid, _toggleBookShieldCheckBox, _bookIsAtlasCheckBox, _highlightOpenCavesCheckBox, timerResetButton, spotSummaryTB, mirrorOW, 
                                        hideIconsEyeball, _moreFQSQoptionsButton, _ws_ms_bu_toggleButton) =
            canvasAdd(appMainCanvas, owItemGrid, OW_ITEM_GRID_LOCATIONS.OFFSET, 30.)
            rightButtonForSizing <- timerResetButton
            hookTopLeftOnSizeChange(timerResetButton)
            canvasAdd(appMainCanvas, timerResetButton, 12.8*OMTW + 10., 60.)
            canvasAdd(appMainCanvas, spotSummaryTB, 12.8*OMTW + 10., 90.)
            iconSpot <- spotSummaryTB
            topLeftPlace()
            canvasAdd(appMainCanvas, hideIconsEyeball, WEBCAM_LINE+4.-10., 2.)
            canvasAdd(appMainCanvas, mirrorOW, WEBCAM_LINE+4.-10., 26.)
            iconEye <- hideIconsEyeball
            iconMirror <- mirrorOW
            tryLayoutTopLeftIcons()
        member this.AddHideQuestCheckboxes(hideFirstQuestCheckBox, hideSecondQuestCheckBox) = 
            canvasAdd(appMainCanvas, hideFirstQuestCheckBox,  WEBCAM_LINE + 5., 110.) 
            canvasAdd(appMainCanvas, hideSecondQuestCheckBox, WEBCAM_LINE + 5., 130.)
        member this.AddLinkRouting(linkIcon, currentTargetIcon) =
            // Link routing icon (and its current-destination icon to its left), scaled down to sit left of the logo, above the triforce chart
            let scale = 0.9   // 50% bigger than the first 0.6
            let size = 30.*scale
            let logoX = 16.*OMTW - kittyWidth - 12. + (kittyWidth - 44.)/2.
            let x = logoX - 18. - 2. - 25. + 20. - 10.   // 25px further left than the original spot, 20px back to the right, then 10px left again
            let y = THRU_MAIN_MAP_H + 3. + 25. - 20.   // ...and 25px lower, then 20px back up
            linkIcon.RenderTransform <- new ScaleTransform(scale, scale)
            currentTargetIcon.RenderTransform <- new ScaleTransform(scale, scale)
            canvasAdd(appMainCanvas, linkIcon, x, y)
            canvasAdd(appMainCanvas, currentTargetIcon, x - size - 2., y)
            Canvas.SetZIndex(linkIcon, 50)           // above the triforce chart (added later) so hovering/clicking Link always works
            Canvas.SetZIndex(currentTargetIcon, 50)
            linkIcon.Visibility <- Visibility.Collapsed   // the Link routing icon is hidden too
            currentTargetIcon.Visibility <- Visibility.Collapsed   // routing still works; just don't show the destination box next to Link
        member this.AddWebcamLine() = () // the webcam area was always too small to be useful
        member this.AddOverworldCanvas(overworldCanvas) =
            canvasAdd(appMainCanvas, overworldCanvas, 0., 150.)
        member this.AddLegend(legendCanvas) = 
            canvasAdd(appMainCanvas, legendCanvas, 0., THRU_MAIN_MAP_H)
        member this.AddItemProgress(itemProgressCanvas, itemProgressTB) = 
            canvasAdd(appMainCanvas, itemProgressCanvas, 0., THRU_MAP_AND_LEGEND_H + 3.)
            canvasAdd(appMainCanvas, itemProgressTB, 3., THRU_MAP_AND_LEGEND_H + 10.)   // all the way left (the version button that used to share this space is gone)
            itemProgressLabelForCentering <- itemProgressTB
            centerVerticallyAt(itemProgressTB, SWORD_ICON_CENTER_Y)
        member this.AddVersionButton(_vb) = ()   // the version button is no longer shown on the main screen
        member this.AddOverworldOptionsButton(fqsqButton) =
            canvasAdd(appMainCanvas, fqsqButton, PANEL_X1, 78.)
            iconFQSQ <- fqsqButton
            topLeftPlace()
        member this.AddHintDecoderButton(tb) =
            canvasAdd(appMainCanvas, tb, WEBCAM_LINE-41., 108.)
            hintDecoderElementForCentering <- tb
            hookTopLeftOnSizeChange(tb)
            tryCenterHotkeysUnderHintDecoder(PANEL_X0)
        member this.AddKittyAndLogo(kitty:Image, _logoBorder, _ztlogo:Image, discordLink:UIElement, _racingLink:UIElement) =
            let logoX = 16.*OMTW - kittyWidth - 22. + (kittyWidth - kitty.Width)/2.   // centered in the space the old picture used, nudged 10px left
            canvasAdd(appMainCanvas, discordLink, logoX - (DiscordLinkWidth - kitty.Width)/2., THRU_MAIN_MAP_H + 3. + kitty.Height + 2.)
            canvasAdd(appMainCanvas, kitty, 0., 0.)   // the ZORA logo sits above the ZORA links (positioned in WPFUI.placeLinkStack)
        member this.AddExtraSiteLinks(zoraSiteLink, _racingSiteLink) =
            canvasAdd(appMainCanvas, zoraSiteLink, 0., 0.)   // (positioned together with the other links in WPFUI.placeLinkStack)
        member this.AddShowHotKeysButton(showHotKeysButton) = 
            canvasAdd(appMainCanvas, showHotKeysButton, WEBCAM_LINE-41., 128.)
            hotkeysElementForCentering <- showHotKeysButton
            tryCenterHotkeysUnderHintDecoder(PANEL_X0)
        member this.AddShowRunCustomButton(showRunCustomButton) = 
            canvasAdd(appMainCanvas, showRunCustomButton, 12.8*OMTW + 10., 120.)   // under Spot Summary
            iconShowRun <- showRunCustomButton
            topLeftPlace()
            Canvas.SetZIndex(showRunCustomButton, 1000)   // always above the map tiles / their icons
        member this.AddTopLeftButtons(optionsButton, drawButton, logButton) =
            for e in [optionsButton; drawButton; logButton] do canvasAdd(appMainCanvas, e, 0., 0.)
            addPanelDecor(fun e -> canvasAdd(appMainCanvas, e, 0., 0.))
            iconOptions <- optionsButton
            iconDraw <- drawButton
            iconLog <- logButton
            topLeftPlace()
        member this.AddQuestReferenceChart(chart, x, y) = canvasAdd(appMainCanvas, chart, x, y)
        member this.AddSaveButton(saveButton) = 
            canvasAdd(appMainCanvas, saveButton, 530., 28.)   // below the ? button
            iconSave <- saveButton
            tryLayoutTopLeftIcons()
        member this.AddItemLocationsButton(itemLocationsButton) =
            canvasAdd(appMainCanvas, itemLocationsButton, WEBCAM_LINE-41., 60.)   // (placed exactly by tryLayoutTopLeftIcons, above the Save button)
            iconItemLocations <- itemLocationsButton
            topLeftPlace()
        member this.AddUserCustomContentButton(uccButton) = 
            canvasAdd(appMainCanvas, uccButton, 16.*OMTW - kittyWidth - 50., THRU_MAIN_MAP_H + 42.)
        member this.AddDungeonTabsOverlay(dungeonTabsOverlay) =  // for magnifier
            canvasAdd(appMainCanvas, dungeonTabsOverlay, 0., START_DUNGEON_AND_NOTES_AREA_H+float(TH) - 2.)   // (2px up)
        member this.AddDungeonTabs(dungeonTabsWholeCanvas) =
            canvasAdd(appMainCanvas, dungeonTabsWholeCanvas, 0., START_DUNGEON_AND_NOTES_AREA_H)
        member this.GetDungeonY() =
            START_DUNGEON_AND_NOTES_AREA_H
        member this.AddBlockers(blockerGrid) =
            canvasAdd(appMainCanvas, blockerGrid, BLOCKERS_AND_NOTES_OFFSET, START_DUNGEON_AND_NOTES_AREA_H) 
        member this.AddNotesSeedsFlags(notesTextBox, seedAndFlagsDisplayCanvas) =
            canvasAdd(appMainCanvas, notesTextBox, BLOCKERS_AND_NOTES_OFFSET, THRU_BLOCKERS_H) 
            canvasAdd(appMainCanvas, seedAndFlagsDisplayCanvas, BLOCKERS_AND_NOTES_OFFSET, THRU_BLOCKERS_H) 
        member this.AddExtraDungeonRightwardStuff(grabModeTextBlock, rightwardCanvas) = 
            canvasAdd(appMainCanvas, grabModeTextBlock, BLOCKERS_AND_NOTES_OFFSET, START_DUNGEON_AND_NOTES_AREA_H) // grab mode instructions
            // the hover preview of a dungeon summary fills the notes area: tell it how far down the notes start (Tag = top offset, height) and how wide the area is
            let rwc = rightwardCanvas :?> Canvas
            rwc.Width <- appMainCanvas.Width - BLOCKERS_AND_NOTES_OFFSET
            rwc.Tag <- box (THRU_BLOCKERS_H - START_DUNGEON_AND_NOTES_AREA_H, rwc.Height - (THRU_BLOCKERS_H - START_DUNGEON_AND_NOTES_AREA_H))
            canvasAdd(appMainCanvas, rightwardCanvas, BLOCKERS_AND_NOTES_OFFSET, START_DUNGEON_AND_NOTES_AREA_H)   // extra place for dungeonTabs to draw atop blockers/notes, e.g. hover minimaps
        member this.AddOWRemainingScreens(owRemainingScreensTextBox) =
            canvasAdd(appMainCanvas, owRemainingScreensTextBox, OW_SPOTS_LEFT_CENTER_X-58., 0.)   // spots-left text with the per-type breakdown under it, all just above the Max Hearts box
            centerHorizontallyAt(owRemainingScreensTextBox, OW_SPOTS_LEFT_CENTER_X)
            Canvas.SetZIndex(owRemainingScreensTextBox, -1)   // draw underneath the item boxes / hint icons so it can never cover them
        member this.AddOWGettableScreens(owGettableScreensCheckBox) =
            canvasAdd(appMainCanvas, owGettableScreensCheckBox, OW_STATS_BLOCK_CENTER_X-50., 22.)
            centerHorizontallyAt(owGettableScreensCheckBox, OW_STATS_BLOCK_CENTER_X)
        member this.AddCurrentMaxHearts(currentMaxHeartsTextBox) = 
            canvasAdd(appMainCanvas, currentMaxHeartsTextBox, OW_STATS_CENTER_X-56., 121.)
            centerHorizontallyAt(currentMaxHeartsTextBox, OW_STATS_CENTER_X)
        member this.AddShowCoords(showCoordsCB, _havePotionLetterImage) = 
            canvasAdd(appMainCanvas, showCoordsCB, WEBCAM_LINE-41., 88.)
            coordsCheckBoxEl <- showCoordsCB
            hookTopLeftOnSizeChange(showCoordsCB)
            tryLayoutTopLeftIcons()
            topLeftPlace()
        member this.AddOWZoneOverlay(zone_checkbox) =
            canvasAdd(appMainCanvas, zone_checkbox, WEBCAM_LINE-41., 64.)
            zonesCheckBoxEl <- zone_checkbox
            hookTopLeftOnSizeChange(zone_checkbox)
            tryLayoutTopLeftIcons()
            topLeftPlace()
        member this.AddMouseHoverExplainer(mouseHoverExplainerIcon, c) =
            canvasAdd(appMainCanvas, mouseHoverExplainerIcon, 530., 0.)
            iconQuestion <- mouseHoverExplainerIcon
            tryLayoutTopLeftIcons()
            canvasAdd(appMainCanvas, c, 0., 0.)
        member this.AddLinkTarget(currentTargetGhostBuster) =
            canvasAdd(appMainCanvas, currentTargetGhostBuster, 16.*OMTW-30., 120.)  // location where Link's currentTarget is
        member this.AddTimeline(t1c, t2c, t3c) =
            let TCX = 22.  // should be 24, but moving two pixels to the left is simplest fix to get timeline items on 3n pixel boundaries so 2/3 size looks good
            canvasAdd(appMainCanvas, t1c, TCX, START_TIMELINE_H)
            canvasAdd(appMainCanvas, t2c, TCX, START_TIMELINE_H)
            canvasAdd(appMainCanvas, t3c, TCX, START_TIMELINE_H)
        member this.GetTimelineBounds() =
            Rect(Point(0.,START_TIMELINE_H), Point(appMainCanvas.Width,THRU_TIMELINE_H))
        member this.GetFullAppBounds() =
            Rect(Point(0.,0.), Point(appMainCanvas.Width,THRU_TIMELINE_H))
        member this.AddReminderDisplayOverlay(reminderDisplayOuterDockPanel) =
            canvasAdd(appMainCanvas, reminderDisplayOuterDockPanel, 0., START_TIMELINE_H)
        member this.AddPostGameDecorationCanvas(postgameDecorationCanvas) =
            canvasAdd(appMainCanvas, postgameDecorationCanvas, 0., START_TIMELINE_H)
        member this.AddSpotSummary(spotSummaryCanvas) =
            canvasAdd(appMainCanvas, spotSummaryCanvas, 50., 3.)
        member this.AddDiskIcon(diskIcon) =
            canvasAdd(appMainCanvas, diskIcon, OMTW*16.-40., START_TIMELINE_H+60.)
        member this.AllDone() =
            canvasAdd(appMainCanvas, topLayerHoverCanvas, 0., 0.)
        member this.AddTopLayerHover(fe, x, y) = canvasAdd(topLayerHoverCanvas, fe, x, y)
        member this.ClearTopLayerHovers() = topLayerHoverCanvas.Children.Clear()
        member this.FocusOverworld() = ()
        member this.FocusDungeon() = ()
        member this.ShowMouseWarp(newPos:Point) =
            let oldPos = Input.Mouse.GetPosition(cm.AppMainCanvas)
            let line = new Shapes.Line(X1=oldPos.X, X2=newPos.X, Y1=oldPos.Y, Y2=newPos.Y, Stroke=Brushes.White, StrokeThickness=2.)
            cm.AppMainCanvas.Children.Add(line) |> ignore
            let x1 = new Animation.DoubleAnimation(oldPos.X, newPos.X, new Duration(TimeSpan.FromMilliseconds(300.)))
            x1.BeginTime <- TimeSpan.FromMilliseconds(150.)
            Animation.Storyboard.SetTarget(x1, line)
            Animation.Storyboard.SetTargetProperty(x1, new PropertyPath(Shapes.Line.X1Property))
            let x2 = new Animation.DoubleAnimation(oldPos.X, newPos.X, new Duration(TimeSpan.FromMilliseconds(300.)))
            Animation.Storyboard.SetTarget(x2, line)
            Animation.Storyboard.SetTargetProperty(x2, new PropertyPath(Shapes.Line.X2Property))
            let y1 = new Animation.DoubleAnimation(oldPos.Y, newPos.Y, new Duration(TimeSpan.FromMilliseconds(300.)))
            y1.BeginTime <- TimeSpan.FromMilliseconds(150.)
            Animation.Storyboard.SetTarget(y1, line)
            Animation.Storyboard.SetTargetProperty(y1, new PropertyPath(Shapes.Line.Y1Property))
            let y2 = new Animation.DoubleAnimation(oldPos.Y, newPos.Y, new Duration(TimeSpan.FromMilliseconds(300.)))
            Animation.Storyboard.SetTarget(y2, line)
            Animation.Storyboard.SetTargetProperty(y2, new PropertyPath(Shapes.Line.Y2Property))
            let sb = new Animation.Storyboard()
            sb.Children.Add(x1); sb.Children.Add(x2); sb.Children.Add(y1); sb.Children.Add(y2)
            sb.Begin()
            async {
                do! Async.Sleep(500)
                do! Async.SwitchToContext(ctxt)
                cm.AppMainCanvas.Children.Remove(line)
            } |> Async.StartImmediate
        member this.IsShort = false

///////////////////////////////////////////////////////////////////////////////////////

type ShorterApplicationLayout(cm:CustomComboBoxes.CanvasManager) =
    inherit ApplicationLayout(cm) 
    let appMainCanvas = cm.AppMainCanvas
    let mutable itemProgressLabelForCentering : UIElement = null
    let topLayerHoverCanvas = new Canvas()   // for temporary bits that appear on hover, to ensure they appear on top of everything else in the app, when no other popup active
    let blockerGridHeight = float(36*3) // from blocker code
    let RED_LINE = 3.
    let DUNGEON_H = START_TIMELINE_H - THRU_MAIN_MAP_AND_ITEM_PROGRESS_H
    let TIMELINE_H = THRU_TIMELINE_H - START_TIMELINE_H
    let H = START_DUNGEON_AND_NOTES_AREA_H + RED_LINE + blockerGridHeight + TIMELINE_H  // the top one is the larger of the two, so always have window that size
    let afterSoldItemBoxesX = OW_ITEM_GRID_LOCATIONS.OFFSET + 120.
    let W = appMainCanvas.Width
    let theCanvas = new Canvas(Width=W, Height=H)
    // we always draw both the upper and lower, so that e.g. when mouse in upper, a timelime update (the timeline canonically lives in the lower) 
    // still draws immediately and we see the projection immediately.  do this by putting a black canvas in between, and swapping order drawn atop each other
    let black = new Canvas(Width=W, Height=H, Background=Brushes.Black)
    let upper = new Canvas(Width=W, Height=H)
    let lower = new Canvas(Width=W, Height=H)
    let mutable currentlyUpper = true
    // constants for upper half
    let upperTimelineStart = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H + RED_LINE + blockerGridHeight
    // constants for lower half
    let dungeonStart = H - TIMELINE_H - DUNGEON_H
    let notesStart = H - TIMELINE_H - DUNGEON_H + blockerGridHeight
    let timelineStart = H - TIMELINE_H
    let switchToDungeon() =
        currentlyUpper <- false
        Canvas.SetZIndex(upper, 0)
        Canvas.SetZIndex(lower, 2)
    let switchToOverworld() =
        currentlyUpper <- true
        Canvas.SetZIndex(upper, 2)
        Canvas.SetZIndex(lower, 0)
    do
        canvasAdd(appMainCanvas, theCanvas, 0., 0.)  
        canvasAdd(theCanvas, upper, 0., 0.)  
        canvasAdd(theCanvas, black, 0., 0.)  
        canvasAdd(theCanvas, lower, 0., 0.)  
        Canvas.SetZIndex(upper, 2)
        Canvas.SetZIndex(black, 1)
        Canvas.SetZIndex(lower, 0)
        let upperThreshold = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H + RED_LINE
        let lowerThreshold = H - TIMELINE_H - DUNGEON_H - RED_LINE
        let diff = upperThreshold - lowerThreshold
        DungeonPopups.THE_DIFF <- diff
        appMainCanvas.MouseMove.Add(fun e ->
            if cm.PopupCanvasStack.Count = 0 then
                let pos = e.GetPosition(appMainCanvas)
                if currentlyUpper && pos.Y > upperThreshold then
                    switchToDungeon()
                    Graphics.SilentlyWarpMouseCursorTo(Point(pos.X, pos.Y-diff))
                if not(currentlyUpper) && pos.Y < lowerThreshold then
                    switchToOverworld()
                    Graphics.SilentlyWarpMouseCursorTo(Point(pos.X, pos.Y+diff))
        )
    interface IApplicationLayoutBase with
        member this.AddMainTracker(mainTracker) =
            canvasAdd(upper, mainTracker, 0., 0.)
            let mainView = Broadcast.makeViewRectImpl(Point(0.,0.), Point(OW_ITEM_GRID_LOCATIONS.OFFSET,float(30*5)), upper)
            canvasAdd(lower, mainView, 0., 0.)
        member this.AddNumberedTriforceCanvas(triforceCanvas, i) =
            canvasAdd(upper, triforceCanvas, OW_ITEM_GRID_LOCATIONS.OFFSET+30.*float i, 0.)
            if i=1 then // only once
                let ntView = Broadcast.makeViewRectImpl(Point(OW_ITEM_GRID_LOCATIONS.OFFSET,0.), Point(OW_ITEM_GRID_LOCATIONS.OFFSET+float(30*8), float(30)), upper)
                canvasAdd(lower, ntView, OW_ITEM_GRID_LOCATIONS.OFFSET, 0.)
        member this.AddItemGridStuff(owItemGrid, _toggleBookShieldCheckBox, _bookIsAtlasCheckBox, _highlightOpenCavesCheckBox, timerResetButton, spotSummaryTB, mirrorOW, 
                                        hideIconsEyeball, _moreFQSQoptionsButton, _ws_ms_bu_toggleButton) =
            canvasAdd(upper, owItemGrid, OW_ITEM_GRID_LOCATIONS.OFFSET, 30.)
            rightButtonForSizing <- timerResetButton
            hookTopLeftOnSizeChange(timerResetButton)
            canvasAdd(upper, timerResetButton, 12.8*OMTW + 10., 60.)
            canvasAdd(upper, spotSummaryTB, 12.8*OMTW + 10., 90.)
            iconSpot <- spotSummaryTB
            topLeftPlace()
            canvasAdd(upper, hideIconsEyeball, WEBCAM_LINE+4.-10., 2.)
            canvasAdd(upper, mirrorOW, WEBCAM_LINE+4.-10., 26.)
            iconEye <- hideIconsEyeball
            iconMirror <- mirrorOW
            tryLayoutTopLeftIcons()
            // just capture a swath of stuff
            let swathView = Broadcast.makeViewRectImpl(Point(OW_ITEM_GRID_LOCATIONS.OFFSET,30.), Point(WEBCAM_LINE, float(30*5)), upper)
            canvasAdd(lower, swathView, OW_ITEM_GRID_LOCATIONS.OFFSET, 30.)
        member this.AddHideQuestCheckboxes(hideFirstQuestCheckBox, hideSecondQuestCheckBox) = 
            canvasAdd(upper, hideFirstQuestCheckBox,  WEBCAM_LINE + 5., 110.) 
            canvasAdd(upper, hideSecondQuestCheckBox, WEBCAM_LINE + 5., 130.)
        member this.AddLinkRouting(_linkIcon, _currentTargetIcon) =
            ()   // the Link routing icon is intentionally not shown
        member this.AddWebcamLine() = () // the webcam area was always too small to be useful
        member this.AddOverworldCanvas(overworldCanvas) =
            canvasAdd(upper, overworldCanvas, 0., 150.)
            let scaleW = (W - afterSoldItemBoxesX) / W
            let scaleH = (180.-45.)/(THRU_MAIN_MAP_H-150.)
            let owm = Broadcast.makeViewRectImpl(Point(0.,150.), Point(W,THRU_MAIN_MAP_H), upper)
            let scaleTrans = new ScaleTransform(scaleW,scaleH)
            if scaleTrans.CanFreeze then
                scaleTrans.Freeze()
            owm.RenderTransform <- scaleTrans
            canvasAdd(lower, owm, afterSoldItemBoxesX, dungeonStart-(180.-45.)-RED_LINE)
        member this.AddLegend(legendCanvas) = 
            canvasAdd(upper, legendCanvas, 0., THRU_MAIN_MAP_H)
        member this.AddItemProgress(itemProgressCanvas, itemProgressTB) = 
            canvasAdd(upper, itemProgressCanvas, 0., THRU_MAP_AND_LEGEND_H + 3.)
            canvasAdd(upper, itemProgressTB, 3., THRU_MAP_AND_LEGEND_H + 10.)   // all the way left (the version button that used to share this space is gone)
            itemProgressLabelForCentering <- itemProgressTB
            centerVerticallyAt(itemProgressTB, SWORD_ICON_CENTER_Y)
            let pro = Broadcast.makeViewRectImpl(Point(ITEM_PROGRESS_FIRST_ITEM,THRU_MAP_AND_LEGEND_H), 
                                Point(ITEM_PROGRESS_FIRST_ITEM + 15.*30.-11.,THRU_MAIN_MAP_AND_ITEM_PROGRESS_H), upper)  // -11. because 'Hint decoder' button infringes into empty space by Magic Key 
            canvasAdd(lower, pro, 20., 150.+20.)
            let tb = new TextBox(Text="Move mouse above red line for overworld view", Foreground=Brushes.Orange, Background=Brushes.Black, 
                                            IsReadOnly=true, IsHitTestVisible=false, BorderThickness=Thickness(0.), FontSize=16., VerticalAlignment=VerticalAlignment.Center)
            canvasAdd(lower, tb, 50., 180.+20.)
            let lineCenter = dungeonStart - RED_LINE/2.
            let line = new Shapes.Line(X1=0., X2=W, Y1=lineCenter, Y2=lineCenter, Stroke=Brushes.Red, StrokeThickness=3.)
            canvasAdd(lower, line, 0., 0.)
        member this.AddVersionButton(_vb) =   // the version button is no longer shown on the main screen
            let v = Broadcast.makeViewRectImpl(Point(0.,THRU_MAP_AND_LEGEND_H), Point(50.,THRU_MAIN_MAP_AND_ITEM_PROGRESS_H), upper)
            canvasAdd(lower, v, 0., 180.+20.)
        member this.AddOverworldOptionsButton(fqsqButton) =
            canvasAdd(upper, fqsqButton, PANEL_X1, 78.)
            iconFQSQ <- fqsqButton
            topLeftPlace()
        member this.AddHintDecoderButton(tb) =
            canvasAdd(upper, tb, WEBCAM_LINE-41., 108.)
            hintDecoderElementForCentering <- tb
            hookTopLeftOnSizeChange(tb)
            tryCenterHotkeysUnderHintDecoder(PANEL_X0)
        member this.AddKittyAndLogo(kitty:Image, _logoBorder, _ztlogo:Image, discordLink:UIElement, _racingLink:UIElement) =
            let logoX = 16.*OMTW - kittyWidth - 22. + (kittyWidth - kitty.Width)/2.   // centered in the space the old picture used, nudged 10px left
            canvasAdd(upper, discordLink, logoX - (DiscordLinkWidth - kitty.Width)/2., THRU_MAIN_MAP_H + 3. + kitty.Height + 2.)
            // WANT!
            let kitty = new Image()
            let imageStream = Graphics.GetResourceStream("CustomLogo.png")
            kitty.Source <- System.Windows.Media.Imaging.BitmapFrame.Create(imageStream)
            kitty.Width <- 45.
            kitty.Height <- 45.
            let lowerDiscordLink = MakeDiscordLink("https://discord.gg/zescSjDtme")
            canvasAdd(lower, lowerDiscordLink, afterSoldItemBoxesX+150. + 5. - (DiscordLinkWidth - kitty.Width)/2., 48. + kitty.Height + 1.)
        member this.AddExtraSiteLinks(zoraSiteLink, _racingSiteLink) =
            canvasAdd(upper, zoraSiteLink, 0., 0.)
        member this.AddShowHotKeysButton(showHotKeysButton) = 
            canvasAdd(upper, showHotKeysButton, WEBCAM_LINE-41., 128.)
            hotkeysElementForCentering <- showHotKeysButton
            tryCenterHotkeysUnderHintDecoder(PANEL_X0)
        member this.AddShowRunCustomButton(showRunCustomButton) = 
            canvasAdd(upper, showRunCustomButton, 12.8*OMTW + 10., 120.)   // under Spot Summary
            iconShowRun <- showRunCustomButton
            topLeftPlace()
            Canvas.SetZIndex(showRunCustomButton, 1000)   // always above the map tiles / their icons
        member this.AddTopLeftButtons(optionsButton, drawButton, logButton) =
            for e in [optionsButton; drawButton; logButton] do canvasAdd(upper, e, 0., 0.)
            addPanelDecor(fun e -> canvasAdd(upper, e, 0., 0.))
            iconOptions <- optionsButton
            iconDraw <- drawButton
            iconLog <- logButton
            topLeftPlace()
        member this.AddQuestReferenceChart(chart, x, y) = canvasAdd(upper, chart, x, y)
        member this.AddSaveButton(saveButton) = 
            canvasAdd(upper, saveButton, 530., 28.)   // below the ? button
            iconSave <- saveButton
            tryLayoutTopLeftIcons()
        member this.AddItemLocationsButton(itemLocationsButton) =
            canvasAdd(upper, itemLocationsButton, WEBCAM_LINE-41., 60.)   // (placed exactly by tryLayoutTopLeftIcons, above the Save button)
            iconItemLocations <- itemLocationsButton
            topLeftPlace()
        member this.AddUserCustomContentButton(uccButton) = 
            // canvasAdd(upper, uccButton, 16.*OMTW - kittyWidth - 50., THRU_MAIN_MAP_H + 42.)
            ignore uccButton // UCC is disabled in ShorterApplicationLayout
        member this.AddDungeonTabsOverlay(dungeonTabsOverlay) =  // for magnifier
            // canvasAdd(upper, dungeonTabsOverlay, 0., THRU_MAP_AND_LEGEND_H)
            ignore dungeonTabsOverlay // magnifier is disabled in ShorterApplicationLayout
        member this.AddDungeonTabs(dungeonTabsWholeCanvas) =
            canvasAdd(lower, dungeonTabsWholeCanvas, 0., dungeonStart)
        member this.GetDungeonY() =
            dungeonStart
        member this.AddBlockers(blockerGrid) =
            canvasAdd(lower, blockerGrid, BLOCKERS_AND_NOTES_OFFSET, dungeonStart) 
            let blockersView = Broadcast.makeViewRectImpl(Point(BLOCKERS_AND_NOTES_OFFSET,dungeonStart), Point(W,dungeonStart + blockerGridHeight), lower)
            canvasAdd(upper, blockersView, BLOCKERS_AND_NOTES_OFFSET, THRU_MAIN_MAP_AND_ITEM_PROGRESS_H)
        member this.AddNotesSeedsFlags(notesTextBox, seedAndFlagsDisplayCanvas) =
            canvasAdd(lower, notesTextBox, BLOCKERS_AND_NOTES_OFFSET, notesStart) 
            canvasAdd(lower, seedAndFlagsDisplayCanvas, BLOCKERS_AND_NOTES_OFFSET, notesStart) 
            let notesView = Broadcast.makeViewRectImpl(Point(BLOCKERS_AND_NOTES_OFFSET,notesStart), Point(W,notesStart + blockerGridHeight), lower)
            canvasAdd(upper, notesView, 0., THRU_MAIN_MAP_AND_ITEM_PROGRESS_H + RED_LINE)
            let tb = new TextBox(Text="Move mouse\nbelow red line\nto switch to\ndungeon view", Foreground=Brushes.Orange, Background=Brushes.Black, 
                                            IsReadOnly=true, IsHitTestVisible=false, BorderThickness=Thickness(0.), FontSize=16., VerticalAlignment=VerticalAlignment.Center)
            let b = new Border(Child=tb, BorderThickness=Thickness(0., 3., 0., 0.))
            canvasAdd(upper, b, W - BLOCKERS_AND_NOTES_OFFSET + 5., THRU_MAIN_MAP_AND_ITEM_PROGRESS_H + RED_LINE)
            let lineCenter = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H + RED_LINE/2.
            let line = new Shapes.Line(X1=0., X2=W, Y1=lineCenter, Y2=lineCenter, Stroke=Brushes.Red, StrokeThickness=3.)
            canvasAdd(upper, line, 0., 0.)
        member this.AddExtraDungeonRightwardStuff(grabModeTextBlock, rightwardCanvas) = 
            canvasAdd(lower, grabModeTextBlock, BLOCKERS_AND_NOTES_OFFSET, dungeonStart) // grab mode instructions
            let rwc = rightwardCanvas :?> Canvas
            rwc.Width <- W - BLOCKERS_AND_NOTES_OFFSET
            rwc.Tag <- box (notesStart - dungeonStart, rwc.Height - (notesStart - dungeonStart))
            canvasAdd(lower, rightwardCanvas, BLOCKERS_AND_NOTES_OFFSET, dungeonStart)   // extra place for dungeonTabs to draw atop blockers/notes, e.g. hover minimaps
        member this.AddOWRemainingScreens(owRemainingScreensTextBox) =
            canvasAdd(upper, owRemainingScreensTextBox, OW_SPOTS_LEFT_CENTER_X-58., 0.)   // spots-left text with the per-type breakdown under it, all just above the Max Hearts box
            centerHorizontallyAt(owRemainingScreensTextBox, OW_SPOTS_LEFT_CENTER_X)
            Canvas.SetZIndex(owRemainingScreensTextBox, -1)   // draw underneath the item boxes / hint icons so it can never cover them
        member this.AddOWGettableScreens(owGettableScreensCheckBox) =
            canvasAdd(upper, owGettableScreensCheckBox, OW_STATS_BLOCK_CENTER_X-50., 22.)
            centerHorizontallyAt(owGettableScreensCheckBox, OW_STATS_BLOCK_CENTER_X)
        member this.AddCurrentMaxHearts(currentMaxHeartsTextBox) = 
            canvasAdd(upper, currentMaxHeartsTextBox, OW_STATS_CENTER_X-56., 121.)
            centerHorizontallyAt(currentMaxHeartsTextBox, OW_STATS_CENTER_X)
        member this.AddShowCoords(showCoordsCB, _havePotionLetterImage) = 
            canvasAdd(upper, showCoordsCB, WEBCAM_LINE-41., 88.)
            coordsCheckBoxEl <- showCoordsCB
            hookTopLeftOnSizeChange(showCoordsCB)
            tryLayoutTopLeftIcons()
            topLeftPlace()
        member this.AddOWZoneOverlay(zone_checkbox) =
            canvasAdd(upper, zone_checkbox, WEBCAM_LINE-41., 64.)
            zonesCheckBoxEl <- zone_checkbox
            hookTopLeftOnSizeChange(zone_checkbox)
            tryLayoutTopLeftIcons()
            topLeftPlace()
        member this.AddMouseHoverExplainer(mouseHoverExplainerIcon, c) =
            canvasAdd(upper, mouseHoverExplainerIcon, 530., 0.)
            iconQuestion <- mouseHoverExplainerIcon
            tryLayoutTopLeftIcons()
            canvasAdd(upper, c, 0., 0.)
        member this.AddLinkTarget(currentTargetGhostBuster) =
            canvasAdd(upper, currentTargetGhostBuster, 16.*OMTW-30., 120.)  // location where Link's currentTarget is
        member this.AddTimeline(t1c, t2c, t3c) =
            let TCX = 22.  // should be 24, but moving two pixels to the left is simplest fix to get timeline items on 3n pixel boundaries so 2/3 size looks good
            canvasAdd(lower, t1c, TCX, timelineStart)
            canvasAdd(lower, t2c, TCX, timelineStart)
            canvasAdd(lower, t3c, TCX, timelineStart)
            let timelineView = Broadcast.makeViewRectImpl(Point(0.,timelineStart), Point(W,timelineStart + TIMELINE_H), lower)
            canvasAdd(upper, timelineView, 0., upperTimelineStart)
        member this.GetTimelineBounds() =
            Rect(Point(0.,timelineStart), Point(W,H))
        member this.GetFullAppBounds() =
            Rect(Point(0.,0.), Point(W,H))
        member this.AddReminderDisplayOverlay(reminderDisplayOuterDockPanel) =
            canvasAdd(lower, reminderDisplayOuterDockPanel, 0., timelineStart)
        member this.AddPostGameDecorationCanvas(postgameDecorationCanvas) =
            canvasAdd(lower, postgameDecorationCanvas, 0., timelineStart)
        member this.AddSpotSummary(spotSummaryCanvas) =
            canvasAdd(upper, spotSummaryCanvas, 50., 3.)
        member this.AddDiskIcon(diskIcon) =
            canvasAdd(lower, diskIcon, OMTW*16.-40., timelineStart+60.)
            cm.SetHeight(H)  // canvas manager always knows the world as though SmallerAppWindowScaleFactor = 1.0
            // change the main app window height
            let H = H * Graphics.appScale()   // (the live scale of the resizable main window)
            let CHROME_HEIGHT = 39.  // Windows app border
            ((cm.RootCanvas.Parent :?> Canvas).Parent :?> Window).Height <- H + CHROME_HEIGHT // this is fragile, but don't know a better way right now
        member this.AllDone() =
            canvasAdd(appMainCanvas, topLayerHoverCanvas, 0., 0.)
        member this.AddTopLayerHover(fe, x, y) = canvasAdd(topLayerHoverCanvas, fe, x, y)
        member this.ClearTopLayerHovers() = topLayerHoverCanvas.Children.Clear()
        member this.FocusOverworld() = 
            let ctxt = System.Threading.SynchronizationContext.Current
            async {
                do! Async.Sleep(10)   // the mouse was just warped before this call, pump events to process the MouseMove
                do! Async.SwitchToContext(ctxt)
                switchToOverworld()
            } |> Async.StartImmediate
        member this.FocusDungeon() = 
            let ctxt = System.Threading.SynchronizationContext.Current
            async {
                do! Async.Sleep(10)   // the mouse was just warped before this call, pump events to process the MouseMove
                do! Async.SwitchToContext(ctxt)
                switchToDungeon()
            } |> Async.StartImmediate
        member this.ShowMouseWarp(_newPos:Point) = ()
        member this.IsShort = true

////////////////////////////////////////////////////////////////////////

let makeMouseMagnifierWindow(cm:CustomComboBoxes.CanvasManager) =
    let mmWindow = new Window()
    mmWindow.Title <- "ZORATracker mouse magnifier"
    mmWindow.ResizeMode <- ResizeMode.CanResizeWithGrip
    mmWindow.SizeToContent <- SizeToContent.Manual
    mmWindow.WindowStartupLocation <- WindowStartupLocation.Manual
    // Do not set Owner — owned windows go behind non-owned popout windows and cannot be brought back.
    // Instead, close manually when main window closes (same pattern as popout windows).
    Application.Current.MainWindow.Closed.Add(fun _ -> try mmWindow.Close() with _ -> ())

    let MSF = // main scale factor
        if TrackerModelOptions.SmallerAppWindow.Value then
            TrackerModelOptions.SmallerAppWindowScaleFactor
        else
            1.0
    let defaultStartSizeFactor = 0.66666666666666
    mmWindow.Width <- cm.AppMainCanvas.Width * MSF * defaultStartSizeFactor
    mmWindow.Height <- cm.AppMainCanvas.Width * MSF * defaultStartSizeFactor  // square start

    let c = new Canvas(Background=Brushes.DarkSlateBlue)
    c.UseLayoutRounding <- true
    mmWindow.Content <- c
    // use BitmapCacheBrush rather than VisualBrush because we want to NearestNeightbor all the pixels
    let wholeView = new Shapes.Rectangle(Width=cm.RootCanvas.Width, Height=cm.RootCanvas.Height, Fill=new BitmapCacheBrush(cm.RootCanvas))
    RenderOptions.SetBitmapScalingMode(wholeView, BitmapScalingMode.NearestNeighbor)

    let SCALE = 3.0 * MSF
    let st = new ScaleTransform(SCALE, SCALE)
    wholeView.RenderTransform <- st
    canvasAdd(c, wholeView, 0., 0.)

    let addFakeMouse(c:Canvas) =
        let fakeMouse = new Shapes.Polygon(Fill=Brushes.White)
        fakeMouse.Points <- new PointCollection([Point(0.,0.); Point(12.,6.); Point(6.,12.)])
        c.Children.Add(fakeMouse) |> ignore
        fakeMouse
    let fakeMouse = addFakeMouse(c)
    let round(x:float) = System.Math.Round(x)
    let update() =        
        Canvas.SetLeft(fakeMouse, round(c.ActualWidth / 2.))
        Canvas.SetTop(fakeMouse, round(c.ActualHeight / 2.))
        st.CenterX <- round(c.ActualWidth / 2.)
        st.CenterY <- round(c.ActualHeight / 2.)
    let desc = "You can resize this window by grabbing\nthe bottom right of it, or move this window\nby grabbing the title bar with the mouse."
    let text = new TextBox(Text=desc, Foreground=Brushes.Orange, Background=Brushes.Black, IsReadOnly=true, IsHitTestVisible=false, BorderThickness=Thickness(0.),
                                FontSize=16., Margin=Thickness(6.0))
    let b = new Border(Child=text, BorderBrush=Brushes.Gray, BorderThickness=Thickness(3.), Background=Brushes.Black)
    let mutable loaded = false
    let w = mmWindow
    let save() = 
        TrackerModelOptions.MouseMagnifierWindowLTWH <- sprintf "%d,%d,%d,%d" (int w.Left) (int w.Top) (int w.Width) (int w.Height)
        TrackerModelOptions.writeSettings()
    let leftTopWidthHeight = TrackerModelOptions.MouseMagnifierWindowLTWH
    let matches = System.Text.RegularExpressions.Regex.Match(leftTopWidthHeight, """^(-?\d+),(-?\d+),(\d+),(\d+)$""")
    if matches.Success then
        w.Left <- float matches.Groups.[1].Value
        w.Top <- float matches.Groups.[2].Value
        w.Width <- float matches.Groups.[3].Value
        w.Height <- float matches.Groups.[4].Value
    mmWindow.LocationChanged.Add(fun _ ->
        if loaded then b.Opacity <- 0.  // dismiss instructions
        save()
        )
    mmWindow.SizeChanged.Add(fun _ ->
        update()
        if loaded then b.Opacity <- 0.  // dismiss instructions
        save()
        )
    mmWindow.Loaded.Add(fun _ -> 
        update()
        canvasAdd(c, b, 20., 20.)
        let ctxt = System.Threading.SynchronizationContext.Current
        async {
            do! Async.SwitchToThreadPool()
            do! Async.Sleep(8000)
            do! Async.SwitchToContext(ctxt)
            b.Opacity <- 0.  // dismiss instructions after a short time
        } |> Async.StartImmediate
        loaded <- true
        )
    cm.RootCanvas.MouseMove.Add(fun ea ->   // we need RootCanvas to see mouse moving in popups
        let mousePos = ea.GetPosition(cm.AppMainCanvas)
        Canvas.SetLeft(wholeView, round(SCALE * ((c.ActualWidth  / 2.) - mousePos.X)))
        Canvas.SetTop( wholeView, round(SCALE * ((c.ActualHeight / 2.) - mousePos.Y)))
        )
    mmWindow

let setupMouseMagnifier(cm, refocusMainWindow) =
    let mutable mouseMagnifierWindow = null
    if TrackerModelOptions.ShowMouseMagnifierWindow.Value then
        mouseMagnifierWindow <- makeMouseMagnifierWindow(cm)
        mouseMagnifierWindow.Show()
        refocusMainWindow()
    OptionsMenu.mouseMagnifierWindowOptionChanged.Publish.Add(fun () ->
        // close existing
        if mouseMagnifierWindow<>null then
            mouseMagnifierWindow.Close()
            mouseMagnifierWindow <- null
        // maybe restart
        if TrackerModelOptions.ShowMouseMagnifierWindow.Value then
            mouseMagnifierWindow <- makeMouseMagnifierWindow(cm)
            mouseMagnifierWindow.Show()
            refocusMainWindow()
        )











