module Graphics

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Templates
open Avalonia.Media
open Avalonia.Styling
open Avalonia.Threading

let mutable theWindow : Window = null
// The main window is resizable: its content scale follows the window size. dynamicAppScale is that live scale (0 until the window has computed one).
// Everything that converts between app coordinates and screen/window pixels uses appScale() instead of the fixed 'smaller window' option.
let mutable dynamicAppScale = 0.
let appScale() = 
    if dynamicAppScale > 0. then dynamicAppScale
    elif TrackerModelOptions.SmallerAppWindow.Value then TrackerModelOptions.SmallerAppWindowScaleFactor
    else 1.0

// (Avalonia brushes can't be frozen; kept so the ported code reads the same)
let freeze(b:SolidColorBrush) = b

// (WPF: Win32 SendInput structs. Only used by code the WPF tracker has commented out; the Windows calls now live in Platform.fs.)
type POINT = struct
    val x:int
    val y:int
    new(_x, _y) = {x=_x; y=_y}
end

// Fake mouse input and cursor warping. On Windows these are the same user32 calls as the WPF tracker; macOS/Linux versions are in Platform.fs.
type Win32() =
    //This simulates a left mouse click
    static member LeftMouseClick() = Platform.LeftMouseClick()
    static member RightMouseClick() = Platform.RightMouseClick()
    static member MiddleMouseClick() = Platform.MiddleMouseClick()
    static member ScrollWheelRotateUp() = Platform.ScrollWheelRotateUp()
    static member ScrollWheelRotateDown() = Platform.ScrollWheelRotateDown()
    
    // returns false if this platform can't warp the cursor
    static member SetCursor(x,y) = 
        let transformedPoint = Point(x*appScale(), y*appScale())
        let pos = theWindow.PointToScreen(transformedPoint)
        Platform.WarpCursor(pos.X, pos.Y, theWindow.DesktopScaling)

    static member SetSonar(enable) = Platform.SetMouseSonar(enable)

let volumeChanged = new Event<int>()
let mutable private soundVolume = float TrackerModelOptions.Volume / 300.
let PlaySoundForSpeechRecognizedAndUsedToMark() =
    Platform.PlaySound(Platform.Files.App("confirm_speech.wav"), soundVolume)
let PlaySoundForReminder() =
    Platform.PlaySound(Platform.Files.App("reminder_clink.wav"), soundVolume)
volumeChanged.Publish.Add(fun v ->
    soundVolume <- float v / 300.
    )

let mutable ErrorBeepWithReminderLogText = fun (_txt:string) -> ()

let GetResourceStream(name:string) = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(name)

let unparent(fe:Control) =
    if fe.Parent<> null then
        (fe.Parent :?> Panel).Children.Remove(fe) |> ignore

let canvasAdd(c:Canvas, item:Control, left, top) =
    if item <> null then
        c.Children.Add(item) |> ignore
        Canvas.SetTop(item, top)
        Canvas.SetLeft(item, left)

let gridAdd(g:Grid, x:Control, c, r) =
    g.Children.Add(x) |> ignore
    Grid.SetColumn(x, c)
    Grid.SetRow(x, r)
let makeGrid(nc, nr, cw, rh) =
    let grid = new Grid()
    for i = 0 to nc-1 do
        let w = if cw = -1 then GridLength.Auto else GridLength(float cw)
        grid.ColumnDefinitions.Add(new ColumnDefinition(Width=w))
    for i = 0 to nr-1 do
        let h = if rh = -1 then GridLength.Auto else GridLength(float rh)
        grid.RowDefinitions.Add(new RowDefinition(Height=h))
    grid
let center(e, w, h) =
    let g = makeGrid(1, 1, w, h)
    gridAdd(g, e, 0, 0)
    g
let dock(e:Control, dock) =
    let d = new DockPanel(LastChildFill=false)
    DockPanel.SetDock(e, dock)
    d.Children.Add(e) |> ignore
    d
let makeArrow(targetX, targetY, sourceX, sourceY, brush:IBrush) =
    let tx,ty = targetX, targetY
    let sx,sy = sourceX, sourceY
    // line from source to target
    let line = new Shapes.Line(StartPoint=Point(sx,sy), EndPoint=Point(tx,ty), Stroke=brush, StrokeThickness=3.)
    line.StrokeDashArray <- new Avalonia.Collections.AvaloniaList<float>(seq[5.;4.])
    let sq(x) = x*x
    let pct = 1. - 15./sqrt(sq(tx-sx)+sq(ty-sy))   // arrowhead base ideally 15 pixels down the line
    let pct = max pct 0.93                         // but at most 93% towards the target, for small lines
    let ax,ay = (tx-sx)*pct+sx, (ty-sy)*pct+sy
    // differential between target and arrowhead base
    let dx,dy = tx-ax, ty-ay
    // points orthogonal to the line from the base
    let p1x,p1y = ax+dy/2., ay-dx/2.
    let p2x,p2y = ax-dy/2., ay+dx/2.
    // triangle to make arrowhead
    let triangle = new Shapes.Polygon(Fill=brush)
    triangle.Points <- ResizeArray([Point(tx,ty); Point(p1x,p1y); Point(p2x,p2y)])
    line, triangle
// WPF scales the whole checkbox up and its content back down, so only the box grows. Avalonia has no LayoutTransform property,
// so the rounded checkbox template (below) wraps its box in a LayoutTransformControl named "boxScale" that this sets.
let scaleUpCheckBoxBox(cb:CheckBox, scale) =
    let apply(ns:INameScope) =
        match ns.Find<LayoutTransformControl>("boxScale") with
        | null -> ()
        | ltc -> ltc.LayoutTransform <- new ScaleTransform(scale, scale)
    cb.TemplateApplied.Add(fun ea -> apply ea.NameScope)
    // the WPF transform also scales the gap between the box and the text
    cb.Padding <- Thickness(cb.Padding.Left*scale, cb.Padding.Top, cb.Padding.Right, cb.Padding.Bottom)

let almostBlack = freeze(new SolidColorBrush(Color.FromRgb(30uy, 30uy, 30uy)))
let almostBlackHoverFeedback = freeze(new SolidColorBrush(Color.FromRgb(30uy, 50uy, 50uy)))
let makeButton(text, fontSizeOpt, fgOpt) =
    let tb = new TextBox(Text=text, IsReadOnly=true, IsHitTestVisible=false, TextAlignment=TextAlignment.Center, VerticalContentAlignment=Layout.VerticalAlignment.Center, BorderThickness=Thickness(0.), Background=Brushes.Transparent)   // text vertically centered in every tracker button (transparent: the button's own rounded fill shows through)
    match fontSizeOpt with | None -> () | Some x -> tb.FontSize <- x
    match fgOpt with | None -> () | Some x -> tb.Foreground <- x
    let button = new Button(Content=tb, HorizontalContentAlignment=Layout.HorizontalAlignment.Stretch, VerticalContentAlignment=Layout.VerticalAlignment.Stretch)
    button

// the rounded dark button look used across the app (thin gray border, 4px corners, dark fill, orange border on hover); border/fill come from the Button's own BorderBrush/Background
// (WPF builds these templates from XAML strings with triggers; here they're code templates that react to the same property changes)
let private hoverOrange = freeze(new SolidColorBrush(Color.Parse("#FFA31A")))
let private pressedFill = freeze(new SolidColorBrush(Color.Parse("#241A0A")))
let roundedBorderBrush = freeze(new SolidColorBrush(Color.FromRgb(58uy,58uy,58uy)))
let roundedButtonTemplate =
    FuncControlTemplate<Button>(fun b _scope ->
        // (not named PART_ContentPresenter: the Fluent theme restyles a presenter with that name on hover/press)
        let cp = new Presenters.ContentPresenter()
        let bd = new Border(Name="bd", CornerRadius=CornerRadius(4.), Child=cp)
        let refresh() =
            cp.Content <- b.Content
            cp.ContentTemplate <- b.ContentTemplate
            bd.Background <- (if b.IsPressed then pressedFill :> IBrush else b.Background)
            bd.BorderBrush <- (if b.IsPointerOver then hoverOrange :> IBrush else b.BorderBrush)
            bd.BorderThickness <- b.BorderThickness
            bd.Padding <- b.Padding
            bd.Opacity <- (if b.IsEffectivelyEnabled then 1.0 else 0.45)
            cp.HorizontalAlignment <- b.HorizontalContentAlignment
            cp.VerticalAlignment <- b.VerticalContentAlignment
        refresh()
        b.PropertyChanged.Add(fun ea ->
            let p = ea.Property
            if p = ContentControl.ContentProperty || p = ContentControl.ContentTemplateProperty || p = Button.IsPressedProperty || p = Input.InputElement.IsPointerOverProperty || p = Input.InputElement.IsEffectivelyEnabledProperty
                || p = Primitives.TemplatedControl.BackgroundProperty || p = Primitives.TemplatedControl.BorderBrushProperty || p = Primitives.TemplatedControl.BorderThicknessProperty
                || p = Primitives.TemplatedControl.PaddingProperty || p = ContentControl.HorizontalContentAlignmentProperty || p = ContentControl.VerticalContentAlignmentProperty then
                refresh())
        bd :> Control)
// rounded checkbox: dark rounded box, orange fill with a dark check when checked, orange dash when indeterminate
let private checkMarkGeometry = "M 2,5.5 L 4.6,8.2 L 9,2.5"
let private indeterminateMarkGeometry = "M 2,5.5 L 9,5.5"
let roundedCheckBoxTemplate =
    FuncControlTemplate<CheckBox>(fun cb _scope ->
        let grid = new Grid(Background=Brushes.Transparent)
        grid.ColumnDefinitions.Add(new ColumnDefinition(Width=GridLength.Auto))
        grid.ColumnDefinitions.Add(new ColumnDefinition(Width=GridLength(1.0, GridUnitType.Star)))
        let mark = new Shapes.Path(Stroke=SolidColorBrush(Color.Parse("#0C0C0C")), StrokeThickness=2., IsVisible=false,
                                    HorizontalAlignment=Layout.HorizontalAlignment.Center, VerticalAlignment=Layout.VerticalAlignment.Center)
        let box = new Border(Name="box", Width=14., Height=14., CornerRadius=CornerRadius(3.), BorderThickness=Thickness(1.5), Child=mark)
        let boxScale = new LayoutTransformControl(Name="boxScale", Child=box, VerticalAlignment=Layout.VerticalAlignment.Center)
        _scope.Register("boxScale", boxScale)
        let cp = new Presenters.ContentPresenter(VerticalAlignment=Layout.VerticalAlignment.Center, HorizontalAlignment=Layout.HorizontalAlignment.Left)
        Grid.SetColumn(cp, 1)
        grid.Children.Add(boxScale) |> ignore
        grid.Children.Add(cp) |> ignore
        let grey, dark, orange = SolidColorBrush(Color.Parse("#8A8A8A")), SolidColorBrush(Color.Parse("#171717")), hoverOrange
        let refresh() =
            cp.Content <- cb.Content
            cp.ContentTemplate <- cb.ContentTemplate
            cp.Margin <- cb.Padding
            match cb.IsChecked |> Option.ofNullable with
            | Some true ->
                box.Background <- orange; box.BorderBrush <- orange
                mark.Data <- Geometry.Parse(checkMarkGeometry); mark.IsVisible <- true
            | None ->
                box.Background <- orange; box.BorderBrush <- orange
                mark.Data <- Geometry.Parse(indeterminateMarkGeometry); mark.IsVisible <- true
            | Some false ->
                box.Background <- dark; box.BorderBrush <- grey
                mark.IsVisible <- false
            if cb.IsPointerOver then box.BorderBrush <- orange
            grid.Opacity <- (if cb.IsEffectivelyEnabled then 1.0 else 0.5)
        refresh()
        cb.PropertyChanged.Add(fun ea ->
            let p = ea.Property
            if p = ContentControl.ContentProperty || p = ContentControl.ContentTemplateProperty || p = Primitives.ToggleButton.IsCheckedProperty || p = Input.InputElement.IsPointerOverProperty || p = Input.InputElement.IsEffectivelyEnabledProperty
                || p = Primitives.TemplatedControl.PaddingProperty then
                refresh())
        grid :> Control)
// rounded text box: WPF replaces the template; Avalonia's TextBox template has many required parts, so this keeps the
// theme's template and overrides the colors it reads (dark fill, gray border, orange border on hover and focus, 6px corners)
let styleRoundedTextBox(t:TextBox) =
    let fill, border = SolidColorBrush(Color.Parse("#171717")), SolidColorBrush(Color.Parse("#3A3A3A"))
    for k in ["TextControlBackground"; "TextControlBackgroundPointerOver"; "TextControlBackgroundFocused"] do t.Resources.[k] <- fill
    t.Resources.["TextControlBorderBrush"] <- border
    t.Resources.["TextControlBorderBrushPointerOver"] <- hoverOrange
    t.Resources.["TextControlBorderBrushFocused"] <- hoverOrange
    t.Resources.["TextControlBorderThemeThicknessFocused"] <- Thickness(1.)
    t.BorderThickness <- Thickness(1.)
    t.CornerRadius <- CornerRadius(6.)
    ScrollViewer.SetHorizontalScrollBarVisibility(t, Primitives.ScrollBarVisibility.Hidden)
    ScrollViewer.SetVerticalScrollBarVisibility(t, Primitives.ScrollBarVisibility.Hidden)
let roundedButtonFill = freeze(new SolidColorBrush(Color.FromRgb(23uy,23uy,23uy)))
// setters that give a Button the rounded look; also used inside the dialog-specific button styles that replace the app-wide default
let addRoundedButtonSetters(style:Style) =
    style.Setters.Add(new Setter(Primitives.TemplatedControl.TemplateProperty, roundedButtonTemplate))
    style.Setters.Add(new Setter(Primitives.TemplatedControl.BorderBrushProperty, roundedBorderBrush))
    style.Setters.Add(new Setter(Primitives.TemplatedControl.BackgroundProperty, roundedButtonFill))
    style.Setters.Add(new Setter(Primitives.TemplatedControl.ForegroundProperty, Brushes.Orange))
    style.Setters.Add(new Setter(Primitives.TemplatedControl.PaddingProperty, Thickness(0.)))
    style.Setters.Add(new Setter(Control.FocusAdornerProperty, null))
    style.Setters.Add(new Setter(Visual.RenderTransformProperty, null))   // the Fluent theme shrinks pressed buttons slightly; WPF doesn't
// app-wide defaults: every Button and CheckBox gets the rounded look unless a window / panel provides its own style
// (Avalonia: plus "WPF metrics" for the theme's controls, whose default sizes are much bigger than WPF's; see PROGRESS.md)
let installAppWideStyles() =
    let app = Avalonia.Application.Current
    if not(isNull app) then
        let bs = new Style(fun x -> x.OfType<Button>())
        addRoundedButtonSetters(bs)
        app.Styles.Add(bs)
        let cs = new Style(fun x -> x.OfType<CheckBox>())
        cs.Setters.Add(new Setter(Primitives.TemplatedControl.TemplateProperty, roundedCheckBoxTemplate))
        cs.Setters.Add(new Setter(Primitives.TemplatedControl.PaddingProperty, Thickness(4.,0.,0.,0.)))
        cs.Setters.Add(new Setter(Primitives.TemplatedControl.ForegroundProperty, Brushes.Orange))
        cs.Setters.Add(new Setter(Control.FocusAdornerProperty, null))
        cs.Setters.Add(new Setter(Layout.Layoutable.MinHeightProperty, 0.))
        cs.Setters.Add(new Setter(Layout.Layoutable.MinWidthProperty, 0.))
        app.Styles.Add(cs)
        // (the Fluent theme recolors a checkbox's text when it's checked, hovered or pressed; WPF's template doesn't)
        for pseudo in [":checked"; ":unchecked"; ":indeterminate"; ":pointerover"; ":pressed"] do
            let csp = new Style(fun x -> x.OfType<CheckBox>().Class(pseudo))
            csp.Setters.Add(new Setter(Primitives.TemplatedControl.ForegroundProperty, Brushes.Orange))
            app.Styles.Add(csp)
        // WPF metrics: WPF text boxes have no minimum size and only a hairline of padding
        let ts = new Style(fun x -> x.OfType<TextBox>())
        ts.Setters.Add(new Setter(Layout.Layoutable.MinHeightProperty, 0.))
        ts.Setters.Add(new Setter(Layout.Layoutable.MinWidthProperty, 0.))
        ts.Setters.Add(new Setter(Primitives.TemplatedControl.PaddingProperty, Thickness(2.,0.,2.,0.)))
        app.Styles.Add(ts)
        // WPF metrics: WPF labels have 5px padding
        let ls = new Style(fun x -> x.OfType<Label>())
        ls.Setters.Add(new Setter(Primitives.TemplatedControl.PaddingProperty, Thickness(5.)))
        app.Styles.Add(ls)
let styleRoundedButton(b:Button) =
    b.Template <- roundedButtonTemplate
    b.BorderBrush <- roundedBorderBrush
    b.Background <- freeze(new SolidColorBrush(Color.FromRgb(23uy,23uy,23uy)))
    match b.Content with
    | :? TextBox as t -> t.Background <- Brushes.Transparent
    | :? Canvas as c -> c.Background <- Brushes.Transparent
    | _ -> ()

let makeColor(rgb) =
    let r = (rgb &&& 0xFF0000) / 0x10000
    let g = (rgb &&& 0x00FF00) / 0x100
    let b = (rgb &&& 0x0000FF) / 0x1
    Color.FromRgb(byte r, byte g, byte b)

let isBlackGoodContrast(rgb) =
    // https://gamedev.stackexchange.com/questions/38536/given-a-rgb-color-x-how-to-find-the-most-contrasting-color-y
    let r = (rgb &&& 0xFF0000) / 0x10000
    let g = (rgb &&& 0x00FF00) / 0x100
    let b = (rgb &&& 0x0000FF) / 0x1
    let r = float r / 255.
    let g = float g / 255.
    let b = float b / 255.
    let l = 0.2126 * r*r + 0.7152 * g*g + 0.0722 * b*b
    let use_black = l > 0.5*0.5
    use_black  // else use white

let transformColor(bmp:Z1RDrawing.Bitmap, f) =
    let r = new Z1RDrawing.Bitmap(bmp.Width,bmp.Height)
    for px = 0 to bmp.Width-1 do
        for py = 0 to bmp.Height-1 do
            let c = bmp.GetPixel(px,py)
            r.SetPixel(px, py, f c)
    r

let greyscale(bmp:Z1RDrawing.Bitmap) =
    transformColor(bmp, fun c -> 
        if c.ToArgb() = System.Drawing.Color.Transparent.ToArgb() then
            c
        else
            let avg = (int c.R + int c.G + int c.B) / 5  // not just average, but overall darker
            let avg = if avg = 0 then 0 else avg + 60    // never too dark
            System.Drawing.Color.FromArgb(avg, avg, avg)
        )
let desaturateColor(c:System.Drawing.Color, pct) =
    let f = pct   // 0.60 // desaturate by 60%
    let L = 0.3*float c.R + 0.6*float c.G + 0.1*float c.B
    let newR = float c.R + f * (L - float c.R)
    let newG = float c.G + f * (L - float c.G)
    let newB = float c.B + f * (L - float c.B)
    System.Drawing.Color.FromArgb(int newR, int newG, int newB)
let desaturate(bmp:Z1RDrawing.Bitmap, pct) = transformColor(bmp, (fun c -> desaturateColor(c,pct)))
let darkenImpl pct (bmp:Z1RDrawing.Bitmap) =
    let r = new Z1RDrawing.Bitmap(bmp.Width,bmp.Height)
    for px = 0 to bmp.Width-1 do
        for py = 0 to bmp.Height-1 do
            let c = bmp.GetPixel(px,py)
            let c = System.Drawing.Color.FromArgb(int(float c.R * pct), int(float c.G * pct), int(float c.B * pct))
            r.SetPixel(px, py, c)
    r
let darken(bmp:Z1RDrawing.Bitmap) =
    darkenImpl 0.5 bmp
let mediaColor(c:System.Drawing.Color) =
    Media.Color.FromArgb(c.A, c.R, c.G, c.B)

// (WPF: the display's DpiScale, needed to undo WPF's dpi scaling of bitmaps. Avalonia shows bitmaps at 96 dpi, 1 pixel per unit, so it's informational only.)
let mutable dpi = None : float option
let BItoImage(bi:Imaging.Bitmap) =
    new Image(Source=bi)
// a bitmap Avalonia can draw (WPF: a frozen BitmapImage)
let BMPToBI(b:Z1RDrawing.Bitmap) : Imaging.Bitmap =
    let wb = new Imaging.WriteableBitmap(PixelSize(b.Width, b.Height), Vector(96., 96.), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Unpremul)
    use fb = wb.Lock()
    let row = Array.zeroCreate<int> b.Width
    let px = b.Pixels
    for y = 0 to b.Height-1 do
        for x = 0 to b.Width-1 do
            row.[x] <- int px.[y*b.Width + x]   // ARGB as a little-endian int is BGRA in memory
        System.Runtime.InteropServices.Marshal.Copy(row, 0, fb.Address + nativeint (y*fb.RowBytes), b.Width)
    wb :> Imaging.Bitmap
let BMPtoImage(bmp:Z1RDrawing.Bitmap) =
    let i = new Image()
    i.Source <- BMPToBI(bmp)
    i.Height <- float bmp.Height 
    i.Width <- float bmp.Width 
    i

let OMTW = 48.  // overworld map tile width - at normal aspect ratio, is 48 (16*3)
let green = freeze(new SolidColorBrush(mediaColor(desaturateColor(System.Drawing.Color.Lime, 0.50))))
let cyan = freeze(new SolidColorBrush(mediaColor(desaturateColor(System.Drawing.Color.FromArgb(0xFF, 0, 0xFF, 0xFF), 0.30))))
let yellow = freeze(new SolidColorBrush(mediaColor(desaturateColor(System.Drawing.Color.Yellow, 0.50))))
let red = freeze(new SolidColorBrush(mediaColor(desaturateColor(System.Drawing.Color.Red, 0.50))))
let palegreen = freeze(new SolidColorBrush(mediaColor(desaturateColor(System.Drawing.Color.Lime, 0.65))))
let paleyellow = freeze(new SolidColorBrush(mediaColor(desaturateColor(System.Drawing.Color.Yellow, 0.65))))
let palered = freeze(new SolidColorBrush(mediaColor(desaturateColor(System.Drawing.Color.Red, 0.65))))
type TileHighlightRectangle() as this =
    let s = new Shapes.Rectangle(Width=OMTW,Height=11.*3.,Stroke=Brushes.Lime,StrokeThickness=3.,Opacity=0.0,IsHitTestVisible=false)
    // WPF runs a 0.5s DoubleAnimation (stroke 12 -> 4) and ColorAnimation (cyan -> color); Avalonia can't animate a
    // shape's stroke brush color with its animation system, so this tweens both by hand on a timer
    let mutable anim : DispatcherTimer = null
    let stopAnim() =
        if anim <> null then
            anim.Stop()
            anim <- null
    let startAnim(toColor:Color) =
        stopAnim()
        let brush = new SolidColorBrush(Colors.Cyan)
        s.Stroke <- brush
        s.StrokeThickness <- 12.0
        let start = DateTime.Now
        let lerp (a:byte) (b:byte) t = byte(float a + (float b - float a) * t)
        let timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16.), DispatcherPriority.Render, fun _ _ -> ())
        timer.Tick.Add(fun _ ->
            let t = min 1.0 ((DateTime.Now - start).TotalSeconds / 0.5)
            s.StrokeThickness <- 12.0 + (4.0 - 12.0) * t
            brush.Color <- Color.FromRgb(lerp Colors.Cyan.R toColor.R t, lerp Colors.Cyan.G toColor.G t, lerp Colors.Cyan.B toColor.B t)
            if t >= 1.0 then stopAnim()
            )
        anim <- timer
        timer.Start()
    let Draw(isPale) =
        stopAnim()
        s.Opacity <- 1.0
        s.StrokeThickness <- if isPale then 2.0 else 4.0
    member _this.MakeRed() = s.Stroke <- red; Draw(false)
    member _this.MakeYellow() = s.Stroke <- yellow; Draw(false)
    member _this.MakeBoldGreen() = s.Stroke <- green; Draw(false); s.StrokeThickness <- 6.0
    member _this.MakeCyan() = s.Stroke <- cyan; Draw(false)
    member _this.MakeGreen() = s.Stroke <- green; Draw(false)
    member _this.MakePaleRed() = s.Stroke <- palered; Draw(true)
    member _this.MakePaleYellow() = s.Stroke <- paleyellow; Draw(true)
    member _this.MakePaleGreen() = s.Stroke <- palegreen; Draw(true)
    member _this.MakeMagenta() = stopAnim(); s.Stroke <- Brushes.Magenta; s.Opacity <- 0.85; s.StrokeThickness <- 5.
    member _this.Hide() = stopAnim(); s.Opacity <- 0.0
    member _this.MakeGreenWithBriefAnimation() = 
        if TrackerModelOptions.AnimateShopHighlights.Value then
            Draw(false)
            startAnim(Colors.Lime)
        else
            this.MakeGreen()
    member _this.MakeYellowWithBriefAnimation() = 
        if TrackerModelOptions.AnimateShopHighlights.Value then
            Draw(false)
            startAnim(Colors.Yellow)
        else
            this.MakeYellow()
    member _this.MakeRedWithBriefAnimation() = 
        if TrackerModelOptions.AnimateShopHighlights.Value then
            Draw(false)
            startAnim(Colors.Red)
        else
            this.MakeRed()
    member _this.Shape = s

// see also
// https://stackoverflow.com/questions/63184765/wpf-left-click-and-drag
// https://stackoverflow.com/questions/12802122/wpf-handle-drag-and-drop-as-well-as-left-click
type DragDropSurface<'T>(surface:Control, onStartDrag : Input.PointerEventArgs * ('T -> unit) -> unit ) = // surface is the entire element we can drag across that encompasses all the multiple items and sees all mouse interactions in the area
    let mutable isDragging = false
    let mutable startPoint : Point option = None
    let mutable clickFunc = None
    let mutable initiateDragFunc = None
    do
        // (WPF PreviewMouseMove: a tunnelling handler)
        surface.AddHandler(Input.InputElement.PointerMovedEvent, EventHandler<Input.PointerEventArgs>(fun _ ea ->
            let props = ea.GetCurrentPoint(surface).Properties
            if (props.IsLeftButtonPressed ||
                props.IsMiddleButtonPressed ||
                props.IsRightButtonPressed)
                    && not isDragging && startPoint.IsSome then
                let pos = ea.GetPosition(surface)
                // This is the windows setting for click v drag
                //     if Math.Abs(pos.X - startPoint.Value.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(pos.Y - startPoint.Value.Y) > SystemParameters.MinimumVerticalDragDistance then
                // However it defaults to just 4 pixels, and some folks were inadvertantly dragging when they wanted to click.
                // The only place setupClickVersusDrag() is called is for 'painting rooms' in dungeon UI.  Intentional drag-painting
                // requires crossing to a different room (each room is ~39px wide), so 20px gives plenty of margin over hand tremor
                // (~5-8px) while still triggering well before the user reaches the next room.  The old 12px threshold was measured
                // from the click point (not the room edge), so even a small wobble mid-click could silently swallow the click.
                if Math.Abs(pos.X - startPoint.Value.X) > 20.0 || Math.Abs(pos.Y - startPoint.Value.Y) > 20.0 then
                    //printfn "%A  -  %A" startPoint pos
                    isDragging <- true
                    onStartDrag(ea, initiateDragFunc.Value)
                    isDragging <- false
                    startPoint <- None
                    clickFunc <- None
                    initiateDragFunc <- None
            ), Interactivity.RoutingStrategies.Tunnel)
        surface.PointerReleased.Add(fun ea ->
            //printfn "MouseUp"
            if not isDragging then
                if clickFunc.IsSome then
                    clickFunc.Value(ea)
                // else e.g. dragged into and released
            startPoint <- None
            clickFunc <- None
            initiateDragFunc <- None
            )
    member this.RegisterClickable(e:Control, onClick:Input.PointerReleasedEventArgs -> unit, onInitiateDrag) =     // each individual element says how it should respond to a click, or to being an initiator of a drag
        // (WPF PreviewMouseDown: a tunnelling handler)
        e.AddHandler(Input.InputElement.PointerPressedEvent, EventHandler<Input.PointerPressedEventArgs>(fun _ ea -> 
            //printfn "PrevMD %A" (Input.Mouse.GetPosition(surface))
            startPoint <- Some(ea.GetPosition(surface))
            clickFunc <- Some(onClick)
            initiateDragFunc <- Some(onInitiateDrag)
            ), Interactivity.RoutingStrategies.Tunnel)

////////////////////////////////////////////////////

let alphaNumBmp =
    let imageStream = GetResourceStream("alphanumerics3x5.png")
    new Z1RDrawing.Bitmap(imageStream)
let paintAlphanumerics3x5(ch, color, bmp:Z1RDrawing.Bitmap, x, y) =  // x and y are 1-pixel coordinates, even though bmp is blown up 3x
    let index =
        match ch with
        | '1' -> 0
        | '2' -> 1
        | '3' -> 2
        | '4' -> 3
        | '5' -> 4
        | '6' -> 5
        | '7' -> 6
        | '8' -> 7
        | '9' -> 8
        | '?' -> 9
        | 'A' -> 10
        | 'B' -> 11
        | 'C' -> 12
        | 'D' -> 13
        | 'E' -> 14
        | 'F' -> 15
        | 'G' -> 16
        | 'H' -> 17
        | 'S' -> 18 // sword
        | 'L' -> 19 // ladder
        | 'R' -> 20 // robot armos
        | 'T' -> 21 // T for take any
        | _ -> failwith "bad alphanumeric character to paint"
    for i = 0 to 2 do
        for j = 0 to 4 do
            if alphaNumBmp.GetPixel(index*3 + i, j).ToArgb() = System.Drawing.Color.Black.ToArgb() then
                // blow it up 3x
                for dx = 0 to 2 do
                    for dy = 0 to 2 do
                        bmp.SetPixel(3*(x+i)+dx, 3*(y+j)+dy, color)

let alphaNumOnTransparentBmp(ch, color, w:int, h:int, x, y) =
    let r = new Z1RDrawing.Bitmap(w,h)
    for px = 0 to w-1 do
        for py = 0 to h-1 do
            r.SetPixel(px, py, System.Drawing.Color.Transparent)
    paintAlphanumerics3x5(ch, color, r, x, y)
    r

let openCaveIconBmp = 
    let imageStream = GetResourceStream("open_cave20x20.png")
    let bmp = new Z1RDrawing.Bitmap(imageStream)
    bmp

let (filled_small_heart_bmp, empty_small_heart_bmp) =
    let imageStream = GetResourceStream("icons8x8.png")
    let bmp = new Z1RDrawing.Bitmap(imageStream)
    let a = [|  
        for i = 0 to bmp.Width/8 - 1 do
            let r = new Z1RDrawing.Bitmap(8,8)
            for px = 0 to 8-1 do
                for py = 0 to 8-1 do
                    let color = bmp.GetPixel(px + i*8, py)
                    //let color = if color.ToArgb() = System.Drawing.Color.Black.ToArgb() then System.Drawing.Color.Transparent else color
                    r.SetPixel(px, py, color)
            yield r
        |]
    (a.[0], a.[1])

let (boomerang_bmp, bow_bmp, magic_boomerang_bmp, raft_bmp, ladder_bmp, recorder_bmp, wand_bmp, red_candle_bmp, book_bmp, key_bmp, 
        silver_arrow_bmp, wood_arrow_bmp, red_ring_bmp, magic_shield_bmp, boom_book_bmp, 
        heart_container_bmp, power_bracelet_bmp, white_sword_bmp, ow_key_armos_bmp,
        brown_sword_bmp, magical_sword_bmp, blue_candle_bmp, blue_ring_bmp,
        ganon_bmp, zelda_bmp, bomb_bmp, bow_and_arrow_bmp, bait_bmp, question_marks_bmp, rupee_bmp, basement_stair_bmp, ws_ms_bomb_upgrade_bmp) =
    let imageStream = GetResourceStream("icons7x7.png")
    let bmp = new Z1RDrawing.Bitmap(imageStream)
    let a = [|  
        for i = 0 to bmp.Width/7 - 1 do
            let r = new Z1RDrawing.Bitmap(7*3,7*3)
            for px = 0 to 7*3-1 do
                for py = 0 to 7*3-1 do
                    let color = bmp.GetPixel(px/3 + i*7, py/3)
                    let color = if color.ToArgb() = System.Drawing.Color.Black.ToArgb() then System.Drawing.Color.Transparent else color
                    r.SetPixel(px, py, color)
            yield r
    |]
    (a.[0], a.[1], a.[2], a.[3], a.[4], a.[5], a.[6], a.[7], a.[8], a.[9],
        a.[10], a.[11], a.[12], a.[13], a.[14], a.[15], a.[16], a.[17], a.[18], a.[19],
        a.[20], a.[21], a.[22], a.[23], a.[24], a.[25], a.[26], a.[27], a.[28], a.[29], a.[30], a.[31])

let bg16x16 = System.Drawing.Color.FromArgb(45, 50, 00)
let (digdogger_bmp, gleeok_bmp, gohma_bmp, manhandla_bmp, wizzrobe_bmp, patra_bmp, dodongo_bmp, red_bubble_bmp, blue_bubble_bmp, blue_darknut_bmp, other_monster_bmp, old_man_bmp,
        vire_bmp, zol_bmp, pols_voice_bmp, red_tektite, red_goriya, rope, stalfos, wallmaster, gel, keese, likelike, gibdo, red_lynel, blue_moblin, aquamentus, blue_lanmola,
        moldorm, rupee_boss, traps, other_monster2) =
    let imageStream = GetResourceStream("zelda_bosses16x16.png")
    let bmp = new Z1RDrawing.Bitmap(imageStream)
    let a = [|  
        for i = 0 to bmp.Width/16 - 1 do
            let r = new Z1RDrawing.Bitmap(18,18)  // border around it
            for px = 0 to 17 do
                for py = 0 to 17 do
                    if px=0 || px=17 || py=0 || py=17 then
                        r.SetPixel(px, py, bg16x16)
                    else
                        r.SetPixel(px, py, System.Drawing.Color.Black)
            for px = 0 to 15 do
                for py = 0 to 15 do
                    let color = bmp.GetPixel(px + i*16, py)
                    if color.ToArgb() = System.Drawing.Color.Black.ToArgb() then () else r.SetPixel(px+1, py+1, color)
            yield r
    |]
    (a.[0], a.[1], a.[2], a.[3], a.[4], a.[5], a.[6], a.[7], a.[8], a.[9], a.[10], a.[11], a.[12], a.[13], a.[14], a.[15], a.[16], a.[17], a.[18], a.[19], a.[20], 
        a.[21], a.[22], a.[23], a.[24], a.[25], a.[26], a.[27], a.[28], a.[29], a.[30], a.[31])

let (zi_triforce_bmp, zi_heart_bmp, zi_bomb_bmp, zi_key_bmp, zi_fiver_bmp, zi_map_bmp, zi_compass_bmp, zi_other_item_bmp, zi_alt_bomb_bmp, zi_rock, zi_tree, zi_atlas, zi_staircase) =
    let imageStream = GetResourceStream("zelda_items16x16.png")
    let bmp = new Z1RDrawing.Bitmap(imageStream)
    let a = [|  
        for i = 0 to bmp.Width/16 - 1 do
            let r = new Z1RDrawing.Bitmap(18,18)  // border around it
            for px = 0 to 17 do
                for py = 0 to 17 do
                    if px=0 || px=17 || py=0 || py=17 then
                        r.SetPixel(px, py, bg16x16)
                    else
                        r.SetPixel(px, py, System.Drawing.Color.Black)
            for px = 0 to 15 do
                for py = 0 to 15 do
                    let color = bmp.GetPixel(px + i*16, py)
                    if color.ToArgb() = System.Drawing.Color.Black.ToArgb() then () else r.SetPixel(px+1, py+1, color)
            yield r
    |]
    (a.[0], a.[1], a.[2], a.[3], a.[4], a.[5], a.[6], a.[7], a.[8], a.[9], a.[10], a.[11], a.[12])

//let (placeholder_row0_bmp, placeholder_row1_bmp, placeholder_row2_bmp, placeholder_row3_bmp, placeholder_row4_bmp, placeholder_row5_bmp, placeholder_row6_bmp, placeholder_row7_bmp) =
//    let imageStream = GetResourceStream("zelda_items16x16.png")
//    let bmp = new Z1RDrawing.Bitmap(imageStream)
//    let a = [|  
//        for i = 0 to bmp.Width/16 - 1 do
//            let r = new Z1RDrawing.Bitmap(18,18)  // border around it
//            for px = 0 to 17 do
//                for py = 0 to 17 do
//                    if px=0 || px=17 || py=0 || py=17 then
//                        r.SetPixel(px, py, bg16x16)
//                    else
//                        r.SetPixel(px, py, System.Drawing.Color.Black)
//            for px = 0 to 15 do
//                for py = 0 to 15 do
//                    let color = bmp.GetPixel(px + i*16, py)
//                    if color.ToArgb() = System.Drawing.Color.Black.ToArgb() then () else r.SetPixel(px+1, py+1, color)
//            yield r
//    |]
//    (a.[0], a.[1], a.[2], a.[3], a.[4], a.[5], a.[6], a.[7])
let unborder(bmp:Z1RDrawing.Bitmap) =
    let r = new Z1RDrawing.Bitmap(bmp.Width-2, bmp.Height-2)
    for i = 0 to r.Width-1 do
        for j = 0 to r.Height-1 do
            r.SetPixel(i,j,bmp.GetPixel(i+1,j+1))
    r
let zi_staircase_unborderedBI = unborder(zi_staircase) |> BMPToBI

let _brightTriforce_bmp, fullOrangeTriforce_bmp, _dullOrangeTriforce_bmp, greyTriforce_bmp, owHeartSkipped_bmp, owHeartEmpty_bmp, owHeartFull_bmp, iconRightArrow_bmp, iconCheckMark_bmp, iconExtras_bmp, iconDisk_bmp, owHeartTallFull_bmp = 
    let imageStream = GetResourceStream("icons10x10.png")
    let bmp = new Z1RDrawing.Bitmap(imageStream)
    let all = [|
        for i = 0 to bmp.Width/10 - 1 do
            let r = new Z1RDrawing.Bitmap(10*3,10*3)
            for px = 0 to 10*3-1 do
                for py = 0 to 10*3-1 do
                    let color = bmp.GetPixel(px/3 + i*10, py/3)
                    let color = if color.ToArgb() = System.Drawing.Color.White.ToArgb() then System.Drawing.Color.Transparent else color
                    r.SetPixel(px, py, color)
            yield r
        |]
    transformColor(all.[1], (fun c -> if c.ToArgb() <> System.Drawing.Color.Transparent.ToArgb() then System.Drawing.Color.LightGray else c)), 
        all.[1],
        transformColor(all.[1], (fun c -> if c.ToArgb() <> System.Drawing.Color.Transparent.ToArgb() then desaturateColor(c, 0.25) else c)), 
        all.[0], all.[2], all.[3], all.[4], all.[5], all.[6], all.[7], all.[8], all.[9]
let UNFOUND_NUMERAL_COLOR = System.Drawing.Color.FromArgb(0x77,0x77,0x99)
let FOUND_NUMERAL_COLOR = System.Drawing.Color.White
let heartFromTakeAny_bmp, heartFromWhiteSwordCave_bmp, heartFromCoast_bmp, heartFromArmos_bmp, heartFromNumberedDungeon_bmps, heartFromLetteredDungeon_bmps =
    let LABEL = System.Drawing.Color.FromArgb(0xFF,0xFF,0xFF)
    let paint(ch) = 
        let bmp = owHeartTallFull_bmp.Clone() :?> Z1RDrawing.Bitmap
        paintAlphanumerics3x5(ch, LABEL, bmp, 4, 2)
        bmp
    let a = [|
        for ch in ["123456789"; "ABCDEFGH9"] do
            yield [|
            for i = 0 to 8 do
                yield paint(ch.Chars(i))
            |] |]
    paint('T'), paint('S'), paint('L'), paint('R'), a.[0], a.[1]
let emptyUnfoundNumberedTriforce_bmps, emptyUnfoundLetteredTriforce_bmps = 
    let a = [|
        for ch in ['1'; 'A'] do
            yield [|
            for i = 0 to 7 do
                let bmp = greyTriforce_bmp.Clone() :?> Z1RDrawing.Bitmap
                paintAlphanumerics3x5(char(int ch + i), UNFOUND_NUMERAL_COLOR, bmp, 4, 4)
                yield bmp
            |] |]
    a.[0], a.[1]
let emptyFoundNumberedTriforce_bmps, emptyFoundLetteredTriforce_bmps = 
    let a = [|
        for ch in ['1'; 'A'] do
            yield [|
            for i = 0 to 7 do
                let bmp = greyTriforce_bmp.Clone() :?> Z1RDrawing.Bitmap
                paintAlphanumerics3x5(char(int ch + i), FOUND_NUMERAL_COLOR, bmp, 4, 4)
                yield bmp
            |] |]
    a.[0], a.[1]
let fullNumberedUnfoundTriforce_bmps, fullNumberedFoundTriforce_bmps, fullLetteredUnfoundTriforce_bmps, fullLetteredFoundTriforce_bmps =
    let a = [|
        for ch in ['1'; 'A'] do
            yield [|
            for i = 0 to 7 do
                let bmp = fullOrangeTriforce_bmp.Clone() :?> Z1RDrawing.Bitmap
                paintAlphanumerics3x5(char(int ch + i), UNFOUND_NUMERAL_COLOR, bmp, 4, 4)
                yield bmp
            |]
            yield [|
            for i = 0 to 7 do
                let bmp = fullOrangeTriforce_bmp.Clone() :?> Z1RDrawing.Bitmap
                paintAlphanumerics3x5(char(int ch + i), FOUND_NUMERAL_COLOR, bmp, 4, 4)
                yield bmp
            |]
        |]
    a.[0], a.[1], a.[2], a.[3]
let unfoundL9_bmp,foundL9_bmp =
    let unfoundTriforceColor = emptyUnfoundNumberedTriforce_bmps.[0].GetPixel(15, 28)
    let u = new Z1RDrawing.Bitmap(10*3,10*3)
    let f = new Z1RDrawing.Bitmap(10*3,10*3)
    // make a rectangle 'shield' to draw the 9 on, so that a hint halo does not wash out the '9'
    for i = 3*3 to 8*3-1 do
        for j = 3*3 to 10*3-1 do
            u.SetPixel(i,j,unfoundTriforceColor)
            f.SetPixel(i,j,unfoundTriforceColor)
    paintAlphanumerics3x5('9', UNFOUND_NUMERAL_COLOR, u, 4, 4)
    paintAlphanumerics3x5('9', FOUND_NUMERAL_COLOR, f, 4, 4)
    u, f

let fairy_bmp =
    let imageStream = GetResourceStream("icons8x16.png")
    let bmp = new Z1RDrawing.Bitmap(imageStream)
    let r = new Z1RDrawing.Bitmap(16,32)
    for x = 0 to 15 do
        for y = 0 to 31 do
            let c = bmp.GetPixel(x/2, y/2)
            if c.ToArgb() <> System.Drawing.Color.Black.ToArgb() then
                r.SetPixel(x, y, c)
    r

let mutable entranceRoomArrowColor = None
let dungeonRoomBmpPairsF() =
    let imageStream = GetResourceStream("new_icons13x9.png")
    let bmp = new Z1RDrawing.Bitmap(imageStream)
    let a = [|  
        for i = 0 to bmp.Width/13 - 1 do
            let cr = new Z1RDrawing.Bitmap(13*3,9*3)
            let ur = new Z1RDrawing.Bitmap(13*3,9*3)
            for px = 0 to 13*3-1 do
                for py = 0 to 9*3-1 do
                    ur.SetPixel(px, py, bmp.GetPixel(px/3 + i*13,   py/3))
                    cr.SetPixel(px, py, bmp.GetPixel(px/3 + i*13, 9+py/3))
            if i=28 then 
                entranceRoomArrowColor <- Some(cr.GetPixel(18, 24))
            yield (BMPToBI ur, BMPToBI cr)
    |]
    a
let dungeonRoomBmpPairs = dungeonRoomBmpPairsF()
let dungeonRoomTinyBmpPairsF() =
    let imageStream = GetResourceStream("new_icons13x9.png")
    let bmp = new Z1RDrawing.Bitmap(imageStream)
    let a = [|  
        for i = 0 to bmp.Width/13 - 1 do
            let ur = new Z1RDrawing.Bitmap(13,9)
            let cr = new Z1RDrawing.Bitmap(13,9)
            for px = 0 to 13-1 do
                for py = 0 to 9-1 do
                    ur.SetPixel(px, py, bmp.GetPixel(px + i*13,   py))
                    cr.SetPixel(px, py, bmp.GetPixel(px + i*13, 9+py))
            yield (BMPToBI ur, BMPToBI cr)
    |]
    a
let dungeonRoomTinyBmpPairs = dungeonRoomTinyBmpPairsF()

(*
let dungeonRoomFloorDrops, dungeonRoomMonsters =
    let imageStream = GetResourceStream("icons3x3.png")
    let bmp = new Z1RDrawing.Bitmap(imageStream)
    let a = [|  
        for i = 0 to bmp.Width/3 - 1 do
            let r = new Z1RDrawing.Bitmap(5*3,5*3)
            let b = new Z1RDrawing.Bitmap(8*3,8*3)
            // black border around them
            for x = 0 to 14 do
                for y = 0 to 14 do
                    r.SetPixel(x,y,System.Drawing.Color.Black)
            for x = 0 to 23 do
                for y = 0 to 23 do
                    b.SetPixel(x,y,System.Drawing.Color.Black)
            // the icon in the middle
            for px = 0 to 3*3-1 do
                for py = 0 to 3*3-1 do
                    r.SetPixel(px+3, py+3, bmp.GetPixel(px/3 + i*3,   py/3))
                    b.SetPixel(2*px+3, 2*py+3, bmp.GetPixel(px/3 + i*3,   py/3))
                    b.SetPixel(2*px+1+3, 2*py+3, bmp.GetPixel(px/3 + i*3,   py/3))
                    b.SetPixel(2*px+3, 2*py+1+3, bmp.GetPixel(px/3 + i*3,   py/3))
                    b.SetPixel(2*px+1+3, 2*py+1+3, bmp.GetPixel(px/3 + i*3,   py/3))
            yield r, b
    |]
    a.[0..7], a.[8..]
*)

let overworldImage =
    let files = [|
//        "s_map_overworld_strip8.png"
        "s_map_overworld_vanilla_strip8.png"
//        "s_map_overworld_zones_strip8.png"
        |]
    let file = files.[(new System.Random()).Next(files.Length)]
//    printfn "selecting overworld file %s" file
    let imageStream = GetResourceStream(file)
    new Z1RDrawing.Bitmap(imageStream)
let zhMapIcons =
    let imageStream = GetResourceStream("s_icon_overworld_strip39.png")
    new Z1RDrawing.Bitmap(imageStream)
let zhDungeonIcons =
    let imageStream = GetResourceStream("s_btn_tr_dungeon_cell_strip3.png")
    new Z1RDrawing.Bitmap(imageStream)
let zhDungeonNums =
    let imageStream = GetResourceStream("s_btn_tr_dungeon_num_strip18.png")
    new Z1RDrawing.Bitmap(imageStream)


// 'Boots Replace Boomerangs' startup option: the boomerang is shown as the boots, and the magical (blue) boomerang as the magical boots
let private bmpFromBase64(b64:string) =
    use ms = new System.IO.MemoryStream(System.Convert.FromBase64String(b64))
    let src = new Z1RDrawing.Bitmap(ms)
    let r = new Z1RDrawing.Bitmap(src)   // detach from the stream
    r.SetResolution(96.0f, 96.0f)
    r
let boots_bmp = bmpFromBase64 "iVBORw0KGgoAAAANSUhEUgAAABUAAAAVCAYAAACpF6WWAAABq0lEQVR4nN2Uz2oUQRCHv+rpyay7MWuIEjwF/LMgGHISTyo5iCdfwIuPIb6PCOLFJ/DiUYUEkSCCB0nWEBNMgu5mZ7q7PAxK3O7dZA65+Dt2V39d1b+qhjOQjC+8fTirqkpw4d8NhSDGzbWMffxmMP9+z+8bgaAx1I4vqCp5kdO53AY9dsIIg4MRejRKpHICNLhAZ/Ec1+9dhTLUAFVoWfprfb5/3EJkOjWCZnnG4e5P1l99iILLKtDOLRrKZlAURIRsJoPj7yVgPGhwU4FJqHeeucVZenfj8rfWv7G70UdMw/L/ZItXNCgitXniFdWE1QmZU0U11H8ONVJ7NcmutFFTJMDAUabG8++lTYFeYXneXLt1MbvQnUk3V0Oo2mGlPLlZrL1c7fzodU03lfCpoCICVeDS0gIrD25g2oXrH1Tu6XLx4tmd9vPx+IlvqjrW7EHJC0t+vqDTbVlXOXqq99V7gEcnQsUIUuSI8bHFClduL0Ge8en1Z7e3uR+dj6BihNGvkp2N7XpMU1UAkgnVsLTWxhER1GSG4eERX959ZXInKmj9TZrsTOYn1m8QJqArILh0CgAAAABJRU5ErkJggg=="
let magical_boots_bmp = bmpFromBase64 "iVBORw0KGgoAAAANSUhEUgAAABUAAAAVCAYAAACpF6WWAAABkElEQVR4nM2UuUpDQRSGv5m5XJcEF0QQFGxMYxHb4AJpfAUVLXwCewt7KwvRIk/gWtvnCYJNELQIgqAIwS2Cy50Zi7lBSCbLFRF/GC53me/8Z+45B/5Cto201p/WWpvP54cAlFJeRuB7+PIGl7egpLsXwKeGsUGYGAFr2xvzQiv3sHUC6V7QFgIJj6+wPg9rC2B+Ap0eh6MNEAJs7NRYCGPn9QwSQbWF2puDgrtqA+kQ+tvzWkOvbmHrFFI9zqGS8PQKa7OwMucCJIYKAaFyqw4Ng85p19XyM+tZ3arL2Mn0f6BSSqGUQtTLo0HeH9VJtVrtQ2vd8n1iqAVyudxUOtV3XS6Xn6vVqrENfZso/SCQgQD293bPi8XiQzabHWwEdg01BvpDKF7A5iFU7k1kjIkKhcJxqVQ6aArugwhcoSsJIjYi46Fy8w53TzJI9UAmk1mMt6x2hEbGASLt5oDge7AIATtnbjZsL5toZrJ5vxc6OgBLOdeavk4SwEcEY8MykJ4DbCo06zv5DhKtCvY39QWjQ6CiSDEc2gAAAABJRU5ErkJggg=="
let boomerangOrBootsBmp() = if TrackerModel.BootsReplaceBoomerangs then boots_bmp else boomerang_bmp
let magicBoomerangOrMagicalBootsBmp() = if TrackerModel.BootsReplaceBoomerangs then magical_boots_bmp else magic_boomerang_bmp
let customItemBmps : Z1RDrawing.Bitmap[] = Array.zeroCreate 16   // the user's ExtraIcons-folder icons, resized to item-icon size (filled in by DrawingLayer.applyCustomMenuIcons)
let allItemBMPs = [| book_bmp; boomerang_bmp; bow_bmp; power_bracelet_bmp; ladder_bmp; magic_boomerang_bmp; key_bmp; raft_bmp; recorder_bmp; red_candle_bmp; red_ring_bmp; silver_arrow_bmp; wand_bmp; white_sword_bmp |]
let allItemBMPsWithHeartShuffle = [| yield! allItemBMPs; for _i = 0 to 8 do yield heart_container_bmp |]

// Extra item sprites (potions, letter, progressive sword). Drawn in code as 7x7 pixel art, blown up 3x like the icons7x7 sheet sprites.
let makeSprite7x7(rows:string[], palette:(char*System.Drawing.Color) list) =
    let r = new Z1RDrawing.Bitmap(7*3,7*3)
    for px = 0 to 7*3-1 do
        for py = 0 to 7*3-1 do
            let ch = rows.[py/3].[px/3]
            let color = match List.tryFind (fun (c,_) -> c = ch) palette with | Some(_,col) -> col | None -> System.Drawing.Color.Transparent
            r.SetPixel(px, py, color)
    r
// triforce piece icon (the item-sheet triforce, centered in a 21x21 cell)
let triforce_item_bmp =
    let r = new Z1RDrawing.Bitmap(21,21)
    for px = 0 to 15 do
        for py = 0 to 15 do
            let c = zi_triforce_bmp.GetPixel(px+1, py+1)
            if c.ToArgb() <> System.Drawing.Color.Black.ToArgb() then r.SetPixel(px+2, py+2, c)
    r
// magical key (the NES magic key sprite: orange ring-head, red shading, short toothed shaft)
let magic_key_bmp =
    makeSprite7x7([| "..ooo.."
                     ".oo.orr"
                     ".ooorr."
                     "...or.."
                     "...or.."
                     "...oo.."
                     "...or.." |], ['o', System.Drawing.Color.FromArgb(252,152,56); 'r', System.Drawing.Color.FromArgb(216,40,0)])
let readCacheFileOrCreateBmp(filename, createF : unit -> Z1RDrawing.Bitmap) =
    // always treat the tiles as 96dpi, whatever the cache file (or this machine's display scaling) says; a tile with another dpi gets drawn smaller than 48x33, leaving dark lines between map tiles
    if System.IO.File.Exists(filename) then
        let bmp = Z1RDrawing.Bitmap.FromFile(filename) :?> Z1RDrawing.Bitmap
        bmp.SetResolution(96.0f, 96.0f)
        bmp
    else
        let bmp = createF()
        bmp.SetResolution(96.0f, 96.0f)
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filename)) |> ignore
        bmp.Save(filename)
        bmp

let mutable alternativeOverworldMapFilename, shouldInitiallyHideOverworldMap = "", false   // startup screen can set these
let blankTileBmp =
    let fullTileBmp = new Z1RDrawing.Bitmap(16*3,11*3)
    let main = System.Drawing.Color.DarkSlateGray
    let alt = System.Drawing.Color.FromArgb(int main.R + 48, int main.G + 48, int main.B + 88)
    for px = 0 to 16*3-1 do
        for py = 0 to 11*3-1 do
            if px >= 15*3 || py >= 10*3 then
                fullTileBmp.SetPixel(px, py, System.Drawing.Color.Black)
            else
                fullTileBmp.SetPixel(px, py, (if (px/3+py/3)%2=0 then main else alt))
    fullTileBmp
let overworldMapBMPs(n) =
    let m = overworldImage
    let tiles = Array2D.zeroCreate 16 8
    if n=4 && not(System.String.IsNullOrEmpty(alternativeOverworldMapFilename)) then
        let image = new Z1RDrawing.Bitmap(alternativeOverworldMapFilename)
        let bitmap = new Z1RDrawing.Bitmap( image, new System.Drawing.Size( 256*3, 88*3 ) )
        for x = 0 to 15 do
            for y = 0 to 7 do
                let tile = new Z1RDrawing.Bitmap(16*3,11*3)
                for px = 0 to 16*3-1 do
                    for py = 0 to 11*3-1 do
                        tile.SetPixel(px, py, bitmap.GetPixel((x*16*3 + px), (y*11*3 + py)))
                tiles.[x,y] <- tile
    else
        for x = 0 to 15 do
            for y = 0 to 7 do
                let tile = 
                    let filename = Platform.Files.User("Overworld", sprintf "quest.%d.ow.%2d.%2d.bmp" n x y)   // (WPF: next to the exe, with a backslash path)
                    readCacheFileOrCreateBmp(filename, fun () ->
                        let tile = new Z1RDrawing.Bitmap(16*3,11*3)
                        for px = 0 to 16*3-1 do
                            for py = 0 to 11*3-1 do
                                tile.SetPixel(px, py, m.GetPixel(256*n + (x*16*3 + px)/3, (y*11*3 + py)/3))
                        tile)
                tiles.[x,y] <- tile
    tiles

let TRANS_BG = System.Drawing.Color.FromArgb(1, System.Drawing.Color.Black)  // transparent background (will be darkened in program layer)
let itemBackgroundColor = System.Drawing.Color.FromArgb(0xEF,0x83,0)
let itemsBMP = 
    let imageStream = GetResourceStream("icons3x7.png")
    new Z1RDrawing.Bitmap(imageStream)

// a full tile with nothing drawn on it (used under marker icons when the tile's own icon is shown as one of those marker slots)
let emptyTileBmp =
    let b = new Z1RDrawing.Bitmap(16*3,11*3)
    for px = 0 to 16*3-1 do
        for py = 0 to 11*3-1 do
            b.SetPixel(px, py, TRANS_BG)
    b
// the bomb (as at the top of the tracker), scaled to fit inside a w x h area and centered in it, on bg
let bombFitBmp(w:int, h:int, bg:System.Drawing.Color) =
    let r = new Z1RDrawing.Bitmap(w, h)
    for x = 0 to w-1 do
        for y = 0 to h-1 do
            r.SetPixel(x, y, bg)
    let isArt(x,y) = bomb_bmp.GetPixel(x,y).A > 128uy
    let pts = [ for x = 0 to bomb_bmp.Width-1 do for y = 0 to bomb_bmp.Height-1 do if isArt(x,y) then yield (x,y) ]
    if not pts.IsEmpty then
        let minX, maxX = (pts |> List.map fst |> List.min), (pts |> List.map fst |> List.max)
        let minY, maxY = (pts |> List.map snd |> List.min), (pts |> List.map snd |> List.max)
        let bw, bh = float (maxX-minX+1), float (maxY-minY+1)
        let scale = min (float w / bw) (float h / bh)
        let dw, dh = max 1 (int (bw*scale + 0.5)), max 1 (int (bh*scale + 0.5))
        let dw, dh = min w dw, min h dh
        let offX, offY = (w-dw)/2, (h-dh)/2
        for x = 0 to dw-1 do
            for y = 0 to dh-1 do
                let sx = minX + min (int bw - 1) (int (float x / scale))
                let sy = minY + min (int bh - 1) (int (float y / scale))
                if isArt(sx,sy) then r.SetPixel(offX + x, offY + y, bomb_bmp.GetPixel(sx,sy))
    r

let genericDungeonInterior_bmp =
    let bmp = new Z1RDrawing.Bitmap(5*3,9*3)
    for px = 0 to 5*3-1 do
        for py = 0 to 9*3-1 do
            bmp.SetPixel(px, py, System.Drawing.Color.Yellow)
    paintAlphanumerics3x5('?', System.Drawing.Color.Black, bmp, 1, 2)
    bmp

let X_OPACITY = 0.55
let overworldCommonestFloorColorBrush = freeze(new SolidColorBrush(Color.FromRgb(204uy,176uy,136uy)))
let overworldCommonestFloorColorDarkBrush = freeze(new SolidColorBrush(Color.FromRgb(byte(float 204 * X_OPACITY),byte(float 176 * X_OPACITY),byte(float 136 * X_OPACITY))))
let unmarkedBmp = new Z1RDrawing.Bitmap(5*3,9*3)
let dontCareBmp = new Z1RDrawing.Bitmap(5*3,9*3)

// each overworld map tile may have multiple icons that can represent it (e.g. dungeon 1 versus dungeon A)
// we store a table, where the array index is the mapSquareChoiceDomain index of the general entry type, and the value there is a list of all possible icons
// The sword cave icons (5x9 art pixels, 3x3 screen pixels each) without the brown cave floor: the number/letter becomes white with a black outline,
// and the rest of the brown is transparent. Art that has no brown (already restyled) is returned unchanged.
let restyleSwordCard(bmp:Z1RDrawing.Bitmap) : Z1RDrawing.Bitmap =
    let isBrown(c:System.Drawing.Color) = c.A = 255uy && c.R = 185uy && c.G = 122uy && c.B = 87uy
    let isBlack(c:System.Drawing.Color) = c.A = 255uy && c.R = 0uy && c.G = 0uy && c.B = 0uy
    let cw, ch = bmp.Width/3, bmp.Height/3
    let cell x y = bmp.GetPixel(x*3, y*3)
    let mutable anyBrown = false
    for x = 0 to cw-1 do
        for y = 0 to ch-1 do
            if isBrown (cell x y) then anyBrown <- true
    if not anyBrown then bmp
    else
        let r = new Z1RDrawing.Bitmap(bmp.Width, bmp.Height)
        for x = 0 to cw-1 do
            for y = 0 to ch-1 do
                let c = cell x y
                let nc =
                    if isBlack c then System.Drawing.Color.White
                    elif isBrown c then
                        let mutable nearGlyph = false
                        for dx = -1 to 1 do
                            for dy = -1 to 1 do
                                let nx, ny = x+dx, y+dy
                                if nx>=0 && nx<cw && ny>=0 && ny<ch && isBlack (cell nx ny) then nearGlyph <- true
                        if nearGlyph then System.Drawing.Color.Black else System.Drawing.Color.Transparent
                    else c
                for dx = 0 to 2 do
                    for dy = 0 to 2 do
                        r.SetPixel(x*3+dx, y*3+dy, nc)
        r
// a darker copy that keeps transparent pixels transparent
let darkenKeepAlpha (pct:float) (bmp:Z1RDrawing.Bitmap) =
    let r = new Z1RDrawing.Bitmap(bmp.Width,bmp.Height)
    for px = 0 to bmp.Width-1 do
        for py = 0 to bmp.Height-1 do
            let c = bmp.GetPixel(px,py)
            r.SetPixel(px, py, System.Drawing.Color.FromArgb(int c.A, int(float c.R * pct), int(float c.G * pct), int(float c.B * pct)))
    r
// MapStateProxy will eventually be responsible for 'decoding' the current tracker state into the appropriate icon
let theInteriorBmpTable = Array.init (TrackerModel.dummyOverworldTiles.Length) (fun _ -> ResizeArray())
do
    let imageStream = GetResourceStream("ow_icons5x9.png")
    let interiorIconStrip = new Z1RDrawing.Bitmap(imageStream)
    let ubc = overworldCommonestFloorColorBrush
    for px = 0 to 5*3-1 do
        for py = 0 to 9*3-1 do
            unmarkedBmp.SetPixel(px, py, System.Drawing.Color.FromArgb(int ubc.Color.R, int ubc.Color.G, int ubc.Color.B))
    let dcbc = overworldCommonestFloorColorDarkBrush
    for px = 0 to 5*3-1 do
        for py = 0 to 9*3-1 do
            dontCareBmp.SetPixel(px, py, System.Drawing.Color.FromArgb(int dcbc.Color.R, int dcbc.Color.G, int dcbc.Color.B))
    let darkxbmp = new Z1RDrawing.Bitmap(5*3,9*3)
    for px = 0 to 5*3-1 do
        for py = 0 to 9*3-1 do
            darkxbmp.SetPixel(px, py, System.Drawing.Color.Black)
    let getInteriorIconFromStrip(i) = 
        let bmp = new Z1RDrawing.Bitmap(5*3,9*3)
        for px = 0 to 5*3-1 do
            for py = 0 to 9*3-1 do
                bmp.SetPixel(px, py, interiorIconStrip.GetPixel(i*5+px/3, py/3))
        bmp
    // 0-8  dungeons: 4 varieties (numbered yellow, numbered dark-yellow, lettered yellow, lettered dark-yellow)
    for labels in ["123456789";"ABCDEFGH9"] do
        for color in [System.Drawing.Color.Yellow; System.Drawing.Color.FromArgb(153,153,0)] do
            labels |> Seq.iteri (fun i ch ->
                let bmp = new Z1RDrawing.Bitmap(5*3,9*3)
                for px = 0 to 5*3-1 do
                    for py = 0 to 9*3-1 do
                        bmp.SetPixel(px, py, color)
                paintAlphanumerics3x5(ch, System.Drawing.Color.Black, bmp, 1, 2)
                theInteriorBmpTable.[i].Add(bmp)
                )
    // 9-12  any roads
    "1234" |> Seq.iteri (fun i ch ->
        let bmp = new Z1RDrawing.Bitmap(5*3,9*3)
        for px = 0 to 5*3-1 do
            for py = 0 to 9*3-1 do
                bmp.SetPixel(px, py, System.Drawing.Color.Orchid)
        paintAlphanumerics3x5(ch, System.Drawing.Color.Black, bmp, 1, 2)
        theInteriorBmpTable.[i+9].Add(bmp)
        )
    // 13  sword3
    theInteriorBmpTable.[13].Add(restyleSwordCard(getInteriorIconFromStrip(0)))
    theInteriorBmpTable.[13].Add(restyleSwordCard(getInteriorIconFromStrip(0)) |> darkenKeepAlpha 0.6)
    // 14  sword2
    theInteriorBmpTable.[14].Add(restyleSwordCard(getInteriorIconFromStrip(1)))
    theInteriorBmpTable.[14].Add(restyleSwordCard(getInteriorIconFromStrip(1)) |> darkenKeepAlpha 0.6)
    // 15  sword1
    theInteriorBmpTable.[15].Add(restyleSwordCard(getInteriorIconFromStrip(2)))
    theInteriorBmpTable.[15].Add(restyleSwordCard(getInteriorIconFromStrip(2)) |> darkenKeepAlpha 0.6)
    // 16-23  item shops (as single-item icons)
    for i = 0 to TrackerModel.MapSquareChoiceDomainHelper.NUM_ITEMS-1 do
        let bmp = new Z1RDrawing.Bitmap(5*3,9*3)
        for px = 0 to 5*3-1 do
            for py = 0 to 9*3-1 do
                bmp.SetPixel(px, py, itemBackgroundColor)
                if px/3 >= 1 && px/3 <= 3 && py/3 >= 1 && py/3 <= 7 then
                    let c = itemsBMP.GetPixel(i*3 + px/3-1, py/3-1)
                    if c.ToArgb() <> System.Drawing.Color.Black.ToArgb() then
                        bmp.SetPixel(px, py, c)
        if i = TrackerModel.MapSquareChoiceDomainHelper.BOMB - 16 then
            // the bomb shop is just the bomb icon (no orange background), like the one at the top of the tracker
            let b = bombFitBmp(13, 13, System.Drawing.Color.Transparent)
            for px = 0 to 14 do
                for py = 0 to 26 do
                    bmp.SetPixel(px, py, TRANS_BG)
            let artAt(x,y) = x >= 1 && x <= 13 && y >= 7 && y <= 19 && b.GetPixel(x-1, y-7).A > 128uy
            for px = 0 to 14 do
                for py = 0 to 26 do
                    if artAt(px,py) then bmp.SetPixel(px, py, b.GetPixel(px-1, py-7))
                    elif [ for dx in -1..1 do for dy in -1..1 -> artAt(px+dx, py+dy) ] |> List.contains true then bmp.SetPixel(px, py, System.Drawing.Color.Black)   // black outline, like the Bomb marker icon
        theInteriorBmpTable.[i+16].Add(bmp)
    // 24  unknown money secret
    theInteriorBmpTable.[24].Add(getInteriorIconFromStrip(11))
    // 25  large secret
    theInteriorBmpTable.[25].Add(getInteriorIconFromStrip(8))
    theInteriorBmpTable.[25].Add(getInteriorIconFromStrip(8) |> darken)
    // 26  medium secret
    theInteriorBmpTable.[26].Add(getInteriorIconFromStrip(6))
    theInteriorBmpTable.[26].Add(getInteriorIconFromStrip(6) |> darken)
    // 27  small secret
    theInteriorBmpTable.[27].Add(getInteriorIconFromStrip(9))
    theInteriorBmpTable.[27].Add(getInteriorIconFromStrip(9) |> darken)
    // 28  door repair charge
    theInteriorBmpTable.[28].Add(getInteriorIconFromStrip(12))
    theInteriorBmpTable.[28].Add(getInteriorIconFromStrip(12) |> darkenImpl 0.7)
    // 29  money making game
    // (the brown top and bottom edge of the card is filled in with the card's own light gray)
    theInteriorBmpTable.[29].Add(
        let b = getInteriorIconFromStrip(10)
        for px = 0 to b.Width-1 do
            for py = 0 to b.Height-1 do
                let c = b.GetPixel(px,py)
                if c.R = 185uy && c.G = 122uy && c.B = 87uy then b.SetPixel(px, py, System.Drawing.Color.FromArgb(255, 228, 228, 228))
        b)
    // 30  letter
    theInteriorBmpTable.[30].Add(getInteriorIconFromStrip(13))
    theInteriorBmpTable.[30].Add(getInteriorIconFromStrip(13) |> darkenImpl 0.7)
    // 31  armos
    theInteriorBmpTable.[31].Add(getInteriorIconFromStrip(7))
    theInteriorBmpTable.[31].Add(getInteriorIconFromStrip(7) |> darken)
    // 32  hint shop
    theInteriorBmpTable.[32].Add(getInteriorIconFromStrip(3))
    theInteriorBmpTable.[32].Add(getInteriorIconFromStrip(3) |> darkenImpl 0.7)
    // 33  take any
    theInteriorBmpTable.[33].Add(getInteriorIconFromStrip(4))
    theInteriorBmpTable.[33].Add(getInteriorIconFromStrip(4) |> darken)
    // 34  potion shop
    theInteriorBmpTable.[34].Add(getInteriorIconFromStrip(5))
    // 35  'X'
    theInteriorBmpTable.[35].Add(darkxbmp)
    // Helper: paint a 3x3 block at unit coordinates (ux, uy) in black
    let p3 (bmp:Z1RDrawing.Bitmap) (ux:int) (uy:int) =
        for dx = 0 to 2 do
            for dy = 0 to 2 do
                bmp.SetPixel(3*ux+dx, 3*uy+dy, System.Drawing.Color.Black)
    // 36  personal preference 1 (magenta background, ✳ asterisk shape — not a dungeon letter)
    //  Pattern (unit coords, 5 wide x 9 tall):
    //    . X . X .   row 2
    //    . . X . .   row 3
    //    X X X X X   row 4  (horizontal bar)
    //    . . X . .   row 5
    //    . X . X .   row 6
    let pp1bmp = new Z1RDrawing.Bitmap(5*3, 9*3)
    for px = 0 to 5*3-1 do
        for py = 0 to 9*3-1 do
            pp1bmp.SetPixel(px, py, System.Drawing.Color.Magenta)
    for ux,uy in [| (1,2);(3,2); (2,3); (0,4);(1,4);(2,4);(3,4);(4,4); (2,5); (1,6);(3,6) |] do
        p3 pp1bmp ux uy
    theInteriorBmpTable.[36].Add(pp1bmp)
    // 37  personal preference 2 (cyan background, ◆ diamond shape — not a dungeon letter)
    //  Pattern (unit coords):
    //    . . X . .   row 2  (top point)
    //    . X X X .   row 3
    //    X X X X X   row 4  (widest)
    //    . X X X .   row 5
    //    . . X . .   row 6  (bottom point)
    let pp2bmp = new Z1RDrawing.Bitmap(5*3, 9*3)
    for px = 0 to 5*3-1 do
        for py = 0 to 9*3-1 do
            pp2bmp.SetPixel(px, py, System.Drawing.Color.Cyan)
    for ux,uy in [| (2,2); (1,3);(2,3);(3,3); (0,4);(1,4);(2,4);(3,4);(4,4); (1,5);(2,5);(3,5); (2,6) |] do
        p3 pp2bmp ux uy
    theInteriorBmpTable.[37].Add(pp2bmp)
// full tiles just have interior bmp in the center and transparent pixels all around (except for the final 'X' one)
// 'Use old potion/letter icons': the tracker's original overworld-tile artwork for the potion shop and the (blue) letter, shrunk to fit an item cell.
// The 5x9 interior icon is redrawn at 2x (10x18) and centered in a 21x21 cell.
let oldTileIconAsItem(interior:Z1RDrawing.Bitmap) =
    // drop the brown (corner-colored) background, crop to the artwork, and scale it up (nearest-neighbor) to fill as much of the 21x21 item cell as it can
    let r = new Z1RDrawing.Bitmap(21,21)
    for px = 0 to 20 do
        for py = 0 to 20 do
            r.SetPixel(px, py, System.Drawing.Color.Transparent)
    let art(ux,uy) = interior.GetPixel(ux*3+1, uy*3+1)
    let bg = art(0,0)
    let isArt(ux,uy) = let c = art(ux,uy) in not (c.R = bg.R && c.G = bg.G && c.B = bg.B)
    let pts = [ for ux = 0 to 4 do for uy = 0 to 8 do if isArt(ux,uy) then yield (ux,uy) ]
    if not pts.IsEmpty then
        let minX = pts |> List.map fst |> List.min
        let maxX = pts |> List.map fst |> List.max
        let minY = pts |> List.map snd |> List.min
        let maxY = pts |> List.map snd |> List.max
        let bw, bh = float (maxX-minX+1), float (maxY-minY+1)
        let scale = min (21. / bw) (21. / bh)
        let dw, dh = int (bw*scale), int (bh*scale)
        let offX, offY = (21-dw)/2, (21-dh)/2
        for x = 0 to dw-1 do
            for y = 0 to dh-1 do
                let ux = minX + min (int bw - 1) (int (float x / scale))
                let uy = minY + min (int bh - 1) (int (float y / scale))
                if isArt(ux,uy) then
                    r.SetPixel(offX + x, offY + y, art(ux,uy))
    r
// the old potion-shop icon exactly as it looked on the overworld tile (tall, with its brown background), scaled to the 21-pixel item height
let oldTileIconWithBackgroundAsItem(interior:Z1RDrawing.Bitmap) =
    let r = new Z1RDrawing.Bitmap(21,21)
    for px = 0 to 20 do
        for py = 0 to 20 do
            r.SetPixel(px, py, System.Drawing.Color.Transparent)
    let scale = 21. / float interior.Height
    let dw, dh = max 1 (int (float interior.Width * scale)), 21
    let offX = (21-dw)/2
    for x = 0 to dw-1 do
        for y = 0 to dh-1 do
            let sx = min (interior.Width-1) (int (float x / scale))
            let sy = min (interior.Height-1) (int (float y / scale))
            r.SetPixel(offX + x, y, interior.GetPixel(sx, sy))
    r
// NES-style bottle: white outline, cork on top, liquid in the lower half (liquid/cork colors taken from the NES sprites)
let potionRows = [| "..aaa.."
                    "..cLc.."
                    "..c.c.."
                    ".c...c."
                    ".cLLLc."
                    ".cLhLc."
                    "..ccc.." |]
let blue_potion_bmp =
    makeSprite7x7(potionRows, ['a', System.Drawing.Color.FromArgb(155,142,255); 'c', System.Drawing.Color.White; 'L', System.Drawing.Color.FromArgb(40,0,165); 'h', System.Drawing.Color.FromArgb(155,142,255)])
let red_potion_bmp =
    makeSprite7x7(potionRows, ['a', System.Drawing.Color.FromArgb(231,156,33); 'c', System.Drawing.Color.White; 'L', System.Drawing.Color.FromArgb(181,49,33); 'h', System.Drawing.Color.FromArgb(231,156,33)])
// one icon for both potions: left half blue potion, right half red potion (split vertically)
let potion_split_bmp =
    let r = new Z1RDrawing.Bitmap(21,21)
    for x = 0 to 20 do
        for y = 0 to 20 do
            r.SetPixel(x, y, (if x <= 10 then blue_potion_bmp else red_potion_bmp).GetPixel(x, y))
    r
// (the old brown-background potion sprite is gone for good: the "old" potion icons are the same potion sprites as the dungeon item menu)
let old_potion_bmp = potion_split_bmp
let old_blue_potion_bmp = blue_potion_bmp
let old_red_potion_bmp = red_potion_bmp
let old_potion_split_bmp = potion_split_bmp
let old_letter_bmp = oldTileIconAsItem(theInteriorBmpTable.[30].[0])   // the old (blue) letter icon
// the potion shop's overworld icon (menu cell and placed tile): the split potion sprite (blue half on the left, red half on the right) on a plain background, no brown cave color
do
    let BG = System.Drawing.Color.FromArgb(int TRANS_BG.A, int TRANS_BG.R, int TRANS_BG.G, int TRANS_BG.B)
    let r = new Z1RDrawing.Bitmap(5*3, 9*3)
    for x = 0 to r.Width-1 do
        for y = 0 to r.Height-1 do
            r.SetPixel(x, y, BG)
    let sz = r.Width   // 15: the 21x21 split sprite scaled down (nearest neighbor), centered vertically
    for x = 0 to sz-1 do
        for y = 0 to sz-1 do
            let p = potion_split_bmp.GetPixel(x*21/sz, y*21/sz)
            if p.A > 0uy && (int p.R + int p.G + int p.B) > 40 then r.SetPixel(x, y + (r.Height - sz)/2, p)
    theInteriorBmpTable.[34].[0] <- r
// the overworld sword tile icons (1/2/3), used for the swords in the dungeon item menus
// the thin, tall overworld cave icon (15x27) scaled to fit the 21-tall item cell: a 12x21 icon centered in the cell, no brown fill around it
let private swordMenuPixel(c:System.Drawing.Color) = c
let swordIconAsItem(interior0:Z1RDrawing.Bitmap) =
    let interior = restyleSwordCard interior0
    let r = new Z1RDrawing.Bitmap(21,21)
    for px = 0 to 20 do
        for py = 0 to 20 do
            r.SetPixel(px, py, System.Drawing.Color.Transparent)
    for x = 0 to 11 do
        for y = 0 to 20 do
            let sx = min 14 (int ((float x + 0.5) * 15. / 12.))
            let sy = min 26 (int ((float y + 0.5) * 27. / 21.))
            r.SetPixel(4 + x, y, swordMenuPixel(interior.GetPixel(sx, sy)))
    r
// the same icon at its natural 15x27 size (3x3 screen pixels per art pixel), used in the (taller) top row of the dungeon item menu
let swordMenuBmp(interior0:Z1RDrawing.Bitmap) =
    let interior = restyleSwordCard interior0
    let r = new Z1RDrawing.Bitmap(15,27)
    for x = 0 to 14 do
        for y = 0 to 26 do
            r.SetPixel(x, y, swordMenuPixel(interior.GetPixel(x, y)))
    r
let ow_sword1_menu_bmp = swordMenuBmp(theInteriorBmpTable.[15].[0])
let ow_sword2_menu_bmp = swordMenuBmp(theInteriorBmpTable.[14].[0])
let ow_sword3_menu_bmp = swordMenuBmp(theInteriorBmpTable.[13].[0])
let ow_sword1_item_bmp = swordIconAsItem(theInteriorBmpTable.[15].[0])
let ow_sword2_item_bmp = swordIconAsItem(theInteriorBmpTable.[14].[0])
let ow_sword3_item_bmp = swordIconAsItem(theInteriorBmpTable.[13].[0])
// the overworld 4th-sword ('4') and progressive-sword ('P') icons: same pixel art as WPFUI.extraSwordIconBmp
let private extraSwordInterior(isProg:bool) : Z1RDrawing.Bitmap =
    let brown = System.Drawing.Color.FromArgb(185,122,87)
    let blade = if isProg then System.Drawing.Color.FromArgb(255,242,0) else System.Drawing.Color.FromArgb(128,128,255)
    let hilt = if isProg then System.Drawing.Color.White else System.Drawing.Color.White
    let glyph = if isProg then [| "abbba"; "ababa"; "abbba"; "abaaa"; "abaaa" |] else [| "ababa"; "ababa"; "abbba"; "aaaba"; "aaaba" |]
    let rows = Array.append (Array.append [| "aaaaa" |] glyph) [| "aaaXa"; "HHHXX"; "aaaXa" |]
    let bmp = new Z1RDrawing.Bitmap(5*3, 9*3)
    for y = 0 to 8 do
        for x = 0 to 4 do
            let c = 
                match rows.[y].[x] with
                | 'b' -> System.Drawing.Color.Black
                | 'X' -> (if isProg || (x + y) % 2 = 0 then blade else System.Drawing.Color.FromArgb(0,0,168))   // the 4th sword: checkered light / dark blue on the guard side, white on the left
                | 'H' -> hilt
                | _ -> brown
            for dx = 0 to 2 do
                for dy = 0 to 2 do
                    bmp.SetPixel(x*3+dx, y*3+dy, c)
    bmp
let ow_sword4_item_bmp = swordIconAsItem(extraSwordInterior false)
let ow_progressive_item_bmp = swordIconAsItem(extraSwordInterior true)
let ow_sword4_menu_bmp = swordMenuBmp(extraSwordInterior false)
let ow_progressive_menu_bmp = swordMenuBmp(extraSwordInterior true)
// the full 15x27 sword icons with a grey (instead of black) outline around the number/letter: shown in the sword item boxes
let swordBoxBmp(menuBmp:Z1RDrawing.Bitmap) =
    let r = new Z1RDrawing.Bitmap(menuBmp.Width, menuBmp.Height)
    for x = 0 to r.Width-1 do
        for y = 0 to r.Height-1 do
            let c = menuBmp.GetPixel(x, y)
            r.SetPixel(x, y, (if c.A = 255uy && c.R < 30uy && c.G < 30uy && c.B < 30uy then System.Drawing.Color.FromArgb(150,150,150) else c))
    r
let ow_sword1_box_bmp = swordBoxBmp ow_sword1_menu_bmp
let ow_sword2_box_bmp = swordBoxBmp ow_sword2_menu_bmp
let ow_sword3_box_bmp = swordBoxBmp ow_sword3_menu_bmp
let ow_sword4_box_bmp = swordBoxBmp ow_sword4_menu_bmp
let ow_progressive_box_bmp = swordBoxBmp ow_progressive_menu_bmp
// Progressive Items: the progressive sword with its tier next to the P (P1 / P2 / P3 / P4), over a sword drawn like the item progress bar's swords (hilt, tall guard, blade).
// 21x27: the P and number glyph on top (outlined like the plain P sword), the sword along the bottom. Index 0 of the arrays below is the plain P sword.
let private progressiveTierSwordBmp(tier:int) : Z1RDrawing.Bitmap =
    let pGlyph = [| "bbb"; "bab"; "bbb"; "baa"; "baa" |]
    let digit =
        match tier with
        | 1 -> [| "aba"; "bba"; "aba"; "aba"; "bbb" |]
        | 2 -> [| "bbb"; "aab"; "bbb"; "baa"; "bbb" |]
        | 3 -> [| "bbb"; "aab"; "bbb"; "aab"; "bbb" |]
        | _ -> [| "bab"; "bab"; "bbb"; "aab"; "aab" |]
    let rows = Array.concat [ [| "aaaaaaa" |]; Array.init 5 (fun r -> pGlyph.[r] + "a" + digit.[r]); [| "aaaaaaa" |] ]
    let brown = System.Drawing.Color.FromArgb(185,122,87)
    let art = new Z1RDrawing.Bitmap(7*3, 7*3)
    for y = 0 to 6 do
        for x = 0 to 6 do
            let c = if rows.[y].[x] = 'b' then System.Drawing.Color.Black else brown
            for dx = 0 to 2 do
                for dy = 0 to 2 do
                    art.SetPixel(x*3+dx, y*3+dy, c)
    let glyphPart = restyleSwordCard art
    let rgb (r:int) (g:int) (b:int) = System.Drawing.Color.FromArgb(r, g, b)
    // hilt, hilt end, blade, guard, bottom edge, tip colors (the same palette as the item progress bar swords)
    let hilt, hiltEnd, blade, guard, edge, tip =
        match tier with
        | 1 -> rgb 136 0 21, rgb 76 0 12, rgb 29 148 65, rgb 29 148 65, rgb 15 74 33, rgb 22 111 49
        | 2 -> rgb 255 255 255, rgb 80 80 80, rgb 63 72 204, rgb 63 72 204, rgb 32 36 102, rgb 63 72 204
        | 3 -> rgb 255 255 255, rgb 32 32 32, rgb 237 0 36, rgb 237 0 36, rgb 119 0 18, rgb 59 0 9
        | _ -> rgb 255 255 255, rgb 223 223 223, rgb 128 128 255, rgb 0 0 168, rgb 0 0 84, rgb 0 0 84
    let r = new Z1RDrawing.Bitmap(21, 27)
    for x = 0 to 20 do
        for y = 0 to 17 do
            r.SetPixel(x, y, glyphPart.GetPixel(x, y))
    let y0 = 18
    for ry = 0 to 8 do
        for x = 13 to 16 do
            r.SetPixel(x, y0 + ry, (if ry < 8 then guard else edge))
    for ry = 3 to 5 do
        for x = 0 to 12 do
            r.SetPixel(x, y0 + ry, (if x = 0 then hiltEnd else hilt))
        for x = 13 to 20 do
            r.SetPixel(x, y0 + ry, (if x = 20 then tip elif tier = 4 && x > 17 then rgb 0 0 168 else blade))
    r
let private progressiveTierItemBmp(interior:Z1RDrawing.Bitmap) =
    let r = new Z1RDrawing.Bitmap(21,21)
    for x = 0 to 14 do
        for y = 0 to 20 do
            let sx = min 20 (int ((float x + 0.5) * 21. / 15.))
            let sy = min 26 (int ((float y + 0.5) * 27. / 21.))
            r.SetPixel(3 + x, y, interior.GetPixel(sx, sy))
    r
// index 0 is the plain 'P' sword (shown in the menus); 1..4 are the tier sprites shown once an item is placed
let ow_progressive_tier_item_bmps = [| yield ow_progressive_item_bmp; for t in 1..4 do yield progressiveTierItemBmp(progressiveTierSwordBmp t) |]
let ow_progressive_tier_menu_bmps = [| yield ow_progressive_menu_bmp; for t in 1..4 do yield progressiveTierSwordBmp t |]
let theFullTileBmpTable = Array.init theInteriorBmpTable.Length (fun _ -> ResizeArray())
let initFull() =
    let len = theInteriorBmpTable.Length
    let BG = System.Drawing.Color.FromArgb(int TRANS_BG.A, int TRANS_BG.R, int TRANS_BG.G, int TRANS_BG.B)   // the compiler is being stupid, clone this color to workaround
    for i = 0 to len-1 do
        for interiorBmp in theInteriorBmpTable.[i] do
            let fullTileBmp = new Z1RDrawing.Bitmap(16*3,11*3)
            for px = 0 to 16*3-1 do
                for py = 0 to 11*3-1 do
                    if px>=5*3 && px<10*3 && py>=1*3 && py<10*3 then 
                        fullTileBmp.SetPixel(px, py, interiorBmp.GetPixel(px-5*3, py-1*3))
                    else
                        fullTileBmp.SetPixel(px, py, if i=35 then System.Drawing.Color.Black else BG)  // 35=DARK_X gets black border; all others (including PersonalPref) get transparent BG
            theFullTileBmpTable.[i].Add(fullTileBmp)
do  
    initFull()

let linkFaceForward_bmp,linkRunRight_bmp,linkFaceRight_bmp,linkGotTheThing_bmp =
    let imageStream = GetResourceStream("link_icons.png")
    let bmp = new Z1RDrawing.Bitmap(imageStream)
    let a = [|
        for i = 0 to 3 do
            let r = new Z1RDrawing.Bitmap(16, 16)
            for x = 0 to 15 do
                for y = 0 to 15 do
                    let color = bmp.GetPixel(16*i+x, y)
                    let color = if color.ToArgb() = System.Drawing.Color.Black.ToArgb() then System.Drawing.Color.Transparent else color
                    r.SetPixel(x, y, color)
            yield r
        |]
    a.[0], a.[1], a.[2], a.[3]
    
let loadBMP(filename) = 
    let imageStream = GetResourceStream(filename)
    let bmp = new Z1RDrawing.Bitmap(imageStream)
    bmp
let mouseIconButtonColorsBMP = loadBMP("mouse-icon-button-colors.png")
let mouseIconButtonColors2BMP = loadBMP("mouse-icon-button-colors-2.png")


let takeAnyPotionBMP = loadBMP("take-any-potion.png")
let takeAnyCandleBMP = loadBMP("take-any-candle.png")
let takeAnyHeartBMP = loadBMP("take-any-heart.png")
let takeAnyLeaveBMP = loadBMP("take-any-leave.png")
let takeThisWoodSwordBMP = loadBMP("take-this-wood-sword.png")
let takeThisCandleBMP = loadBMP("take-this-candle.png")
let takeThisLeaveBMP = loadBMP("take-this-leave.png")

let z1rSampleHintBMP = loadBMP("z1r-sample-hint.png")
let allItemsHUDBestBMP = loadBMP("all-items-hud-pixels1.png")
let allItemsHUDWorstBMP = loadBMP("all-items-hud-pixels1-worse.png")

let firstQuestItemReferenceBMP = loadBMP("first-quest-item-reference.png")
let secondQuestItemReferenceBMP = loadBMP("second-quest-item-reference.png")
let mirrorOverworldBMP = loadBMP("mirror-overworld.png")
let eyeballBMP = loadBMP("eyeball.png")
let wsmsbuBMP = loadBMP("ws-ms-bu.png")

(*
code for clipping screenshots to same size

let clipTakeAny(bmp:Z1RDrawing.Bitmap) =
    let r = new Z1RDrawing.Bitmap(672, 448)
    for x = 0 to 671 do
        for y = 0 to 447 do
            r.SetPixel(x,y,bmp.GetPixel(x+48, y+45))
    r
let takeAnyPotionBMP = loadBMP("take-any-potion.png") |> clipTakeAny
let takeAnyCandleBMP = loadBMP("take-any-candle.png") |> clipTakeAny
let takeAnyHeartBMP = loadBMP("take-any-heart.png") |> clipTakeAny
let takeAnyLeaveBMP = loadBMP("take-any-leave.png") |> clipTakeAny
let takeThisWoodSwordBMP = loadBMP("take-this-wood-sword.png") |> clipTakeAny
let takeThisCandleBMP = loadBMP("take-this-candle.png") |> clipTakeAny
let takeThisLeaveBMP = loadBMP("take-this-leave.png") |> clipTakeAny

do
    takeAnyPotionBMP.Save("takeAnyPotionBMP.png")
    takeAnyCandleBMP.Save("takeAnyCandleBMP.png")
    takeAnyHeartBMP.Save("takeAnyHeartBMP.png")
    takeAnyLeaveBMP.Save("takeAnyLeaveBMP.png")
    takeThisWoodSwordBMP.Save("takeThisWoodSwordBMP.png")
    takeThisCandleBMP.Save("takeThisCandleBMP.png")
    takeThisLeaveBMP.Save("takeThisLeaveBMP.png")
*)

(*
do
    let imageStream = GetResourceStream("icons3x7.png")
    let bmp37 = new Z1RDrawing.Bitmap(imageStream)
    let imageStream = GetResourceStream("icons7x7.png")
    let bmp = new Z1RDrawing.Bitmap(imageStream)
    let newBmp = new Z1RDrawing.Bitmap(bmp.Width+7, bmp.Height)
    for i = 0 to bmp.Width-1 do
        for j = 0 to bmp.Height-1 do
            newBmp.SetPixel(i,j,bmp.GetPixel(i,j))
    for i = 0 to 6 do
        for j = 0 to 6 do
            newBmp.SetPixel(bmp.Width+i, j, Drawing.Color.Transparent)
    for i = 0 to 2 do
        for j = 0 to 6 do
            newBmp.SetPixel(bmp.Width+i+2, j, bmp37.GetPixel(3*5+i,j))
    newBmp.Save("""C:\Users\Admin1\Source\Repos\Zelda1RandoTools\Z1R_Tracker\Z1R_WPF\tmp.png""")

do
    let imageStream = GetResourceStream("icons7x7.png")
    let bmp = new Z1RDrawing.Bitmap(imageStream)
    let newBmp = new Z1RDrawing.Bitmap(bmp.Width, bmp.Height)
    for i = 0 to bmp.Width-1 do
        for j = 0 to bmp.Height-1 do
            let c = bmp.GetPixel(i,j)
            if c.A <> 0xFFuy then
                newBmp.SetPixel(i,j,System.Drawing.Color.Black)
            else
                newBmp.SetPixel(i,j,c)
    newBmp.Save("""C:\Users\Admin1\Source\Repos\Zelda1RandoTools\Z1R_Tracker\Z1R_WPF\tmp.png""")
*)

let swordLevelToBmp(swordLevel) =
    match swordLevel with
    | 0 -> greyscale ow_sword1_item_bmp
    | 1 -> ow_sword1_item_bmp
    | 2 -> ow_sword2_item_bmp
    | 3 -> ow_sword3_item_bmp
    | _ -> failwith "bad SwordLevel"
// the white sword with a yellow handle (instead of its usual blue one): shown in the item progress bar once a 4th sword has been collected
let white_sword4_bmp = ow_sword4_item_bmp   // (the overworld sword icons are used everywhere; there are no plain sword pictures)
// the overworld cave-style sword icons (the 4th sword has a blue handle), used instead of the plain sword pictures in the item progress bar and popouts
let swordLevelToOwBmp(swordLevel) =
    match swordLevel with
    | 0 -> greyscale ow_sword1_item_bmp   // no sword yet: the sword 1 icon, greyed out
    | 1 -> ow_sword1_item_bmp
    | 2 -> ow_sword2_item_bmp
    | 3 -> ow_sword3_item_bmp
    | _ -> failwith "bad SwordLevel"
let ringLevelToBmp(ringLevel) =
    match ringLevel with
    | 0 -> greyscale red_ring_bmp
    | 1 -> blue_ring_bmp
    | 2 -> red_ring_bmp
    | _ -> failwith "bad RingLevel"


let blockers_gsc = new GradientStops()
do
    blockers_gsc.Add(new GradientStop(Color.FromArgb(255uy, 0uy, 180uy, 0uy), 0.))
    blockers_gsc.Add(new GradientStop(Color.FromArgb(255uy, 40uy, 40uy, 40uy), 0.3))
    blockers_gsc.Add(new GradientStop(Color.FromArgb(255uy, 40uy, 40uy, 40uy), 0.7))
    blockers_gsc.Add(new GradientStop(Color.FromArgb(255uy, 180uy, 0uy, 0uy), 1.0))
let blockers_maybeBG = new LinearGradientBrush(GradientStops=blockers_gsc, StartPoint=RelativePoint(0.,0.,RelativeUnit.Relative), EndPoint=RelativePoint(1.,1.,RelativeUnit.Relative))
let blockerHardCanonicalBMP(current) = 
    match current with
    | TrackerModel.DungeonBlocker.COMBAT -> ow_sword2_item_bmp
    | TrackerModel.DungeonBlocker.BOW_AND_ARROW | TrackerModel.DungeonBlocker.MAYBE_BOW_AND_ARROW -> bow_and_arrow_bmp
    | TrackerModel.DungeonBlocker.RECORDER | TrackerModel.DungeonBlocker.MAYBE_RECORDER -> recorder_bmp
    | TrackerModel.DungeonBlocker.LADDER | TrackerModel.DungeonBlocker.MAYBE_LADDER -> ladder_bmp
    | TrackerModel.DungeonBlocker.BAIT | TrackerModel.DungeonBlocker.MAYBE_BAIT -> bait_bmp
    | TrackerModel.DungeonBlocker.KEY | TrackerModel.DungeonBlocker.MAYBE_KEY -> magic_key_bmp
    | TrackerModel.DungeonBlocker.BOMB | TrackerModel.DungeonBlocker.MAYBE_BOMB -> bomb_bmp
    | TrackerModel.DungeonBlocker.MONEY | TrackerModel.DungeonBlocker.MAYBE_MONEY -> rupee_bmp
    | TrackerModel.DungeonBlocker.NOTHING -> null
let blockerCurrentDisplay(current) =
    let innerc = new Canvas(Width=24., Height=24., Background=Brushes.Transparent, IsHitTestVisible=false)  // just has item drawn on it, not the box
    innerc.Children.Clear()
    innerc.Background <- match current with
                            | TrackerModel.DungeonBlocker.MAYBE_LADDER
                            | TrackerModel.DungeonBlocker.MAYBE_BAIT
                            | TrackerModel.DungeonBlocker.MAYBE_BOMB
                            | TrackerModel.DungeonBlocker.MAYBE_BOW_AND_ARROW
                            | TrackerModel.DungeonBlocker.MAYBE_KEY
                            | TrackerModel.DungeonBlocker.MAYBE_MONEY
                            | TrackerModel.DungeonBlocker.MAYBE_RECORDER                            
                                -> blockers_maybeBG :> IBrush
                            | _ -> Brushes.Black :> IBrush
    let bmp = blockerHardCanonicalBMP(current)
    if bmp <> null then
        let image = BMPtoImage(bmp)
        image.IsHitTestVisible <- false
        canvasAdd(innerc, image, 1., 1.)
    innerc

let skipped = Brushes.MediumPurple
let placeSkippedItemXDecorationImpl(innerc:Canvas, size) =
    innerc.Children.Add(new Shapes.Line(Stroke=skipped, StrokeThickness=3., StartPoint=Point(0.,0.), EndPoint=Point(size,size), IsHitTestVisible=false)) |> ignore
    innerc.Children.Add(new Shapes.Line(Stroke=skipped, StrokeThickness=3., StartPoint=Point(size,0.), EndPoint=Point(0.,size), IsHitTestVisible=false)) |> ignore
let placeSkippedItemXDecoration(innerc) = placeSkippedItemXDecorationImpl(innerc, 30.)


// (WPF: "Z1R_WPF.exe". Used for restarting, the single-instance mutex name and the Windows desktop shortcut.)
let ExeName = System.IO.Path.GetFileName(Platform.ExecutablePath())
let RestartTheApplication() = 
    let cwd = System.IO.Path.GetDirectoryName(Platform.ExecutablePath())
    let thisExe = System.IO.Path.Combine(cwd, ExeName)
    System.Diagnostics.Process.Start(thisExe) |> ignore
    match Avalonia.Application.Current.ApplicationLifetime with
    | :? ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime as d when d.MainWindow <> null -> d.MainWindow.Close()
    | _ -> ()

// ideas from
// https://kent-boogaart.com/blog/dispatcher-frames
// from https://stackoverflow.com/questions/4502037/where-is-the-application-doevents-in-wpf
// see also
// https://stackoverflow.com/questions/21248643/how-can-i-convert-win32-mouse-messages-to-wpf-mouse-events 
let mutable sawMouseEvent = false
let mutable theTimer = null : DispatcherTimer
let mutable theFrame = null : DispatcherFrame
let mutable theCounter = 0
let setup(w:Window, _v:Visual) =
    if theTimer=null then
        dpi <- Some(w.RenderScaling)
        theTimer <- new DispatcherTimer(DispatcherPriority.Render) // this only runs for a very short time when warping mouse; use high priority to avoid noticeable latency
        theTimer.Stop()
        theTimer.Interval <- TimeSpan.FromSeconds(0.03)
        theTimer.Tick.Add(fun _ -> 
            if theFrame <> null then   // avoid a race
                if not sawMouseEvent && theCounter>4 then
                    //printfn "uh oh too slow"     // or, the mouse didn't move at all
                    sawMouseEvent <- true
                if sawMouseEvent then
                    theFrame.Continue <- false
                    theTimer.Stop()
                else
                    theCounter <- theCounter + 1
            )
        w.PointerMoved.Add(fun _ -> sawMouseEvent <- true)
let SilentlyWarpMouseCursorTo(pos:Point) =
    if theFrame = null then  // Note: cannot process a second of these calls while one is active; I think that's fine
        theFrame <- new DispatcherFrame()
        sawMouseEvent <- false
        theCounter <- 0
        theTimer.Start()
        if Win32.SetCursor(pos.X, pos.Y) then    // this queues a mouse move input, and we want to block until that event is processed, to avoid spurious MouseLeave()s and other reasons
            Dispatcher.UIThread.PushFrame(theFrame)   // creates a blocking message pump that only stops when Continue set to false
        else
            theTimer.Stop()   // (this platform can't warp the cursor, so there's no mouse move to wait for)
        theFrame <- null
let WarpMouseCursorTo(pos:Point) =
    SilentlyWarpMouseCursorTo(pos)
    PlaySoundForSpeechRecognizedAndUsedToMark()
let NavigationallyWarpMouseCursorTo(pos:Point) =   // can abstract over whether keyboard 'arrow' keys play the sound or not
    SilentlyWarpMouseCursorTo(pos)
    // (WPF has commented-out code here that turned on mouse sonar and pressed Ctrl to show where the cursor went)

// https://learn.microsoft.com/en-us/archive/blogs/greg_schechter/more-details-on-how-effects-work
// https://learn.microsoft.com/en-us/archive/blogs/jgoldb/software-rendering-usage-in-wpf
let canUseEffectsWithoutDestroyingPerformance = 
    // (System.Windows.Media.RenderCapability.Tier >>> 16) >= 2 
    // System.Windows.Media.RenderCapability.IsPixelShaderVersionSupported(2s,0s)
    // none of the above worked in practice, so there is a flag in the settings file instead
    TrackerModelOptions.UseBlurEffects.Value
