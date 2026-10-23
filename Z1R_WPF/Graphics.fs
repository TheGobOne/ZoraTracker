module Graphics

open System
open System.Windows
open System.Windows.Controls 
open System.Windows.Media

let mutable theWindow : System.Windows.Window = null
// The main window is resizable: its content scale follows the window size. dynamicAppScale is that live scale (0 until the window has computed one).
// Everything that converts between app coordinates and screen/window pixels uses appScale() instead of the fixed 'smaller window' option.
let mutable dynamicAppScale = 0.
let appScale() = 
    if dynamicAppScale > 0. then dynamicAppScale
    elif TrackerModelOptions.SmallerAppWindow.Value then TrackerModelOptions.SmallerAppWindowScaleFactor
    else 1.0

let freeze(b:SolidColorBrush) = (if b.CanFreeze then b.Freeze()); b

type POINT = struct
    val x:int
    val y:int
    new(_x, _y) = {x=_x; y=_y}
end

// send input stuff
[<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)>]
type MOUSEINPUT = struct
    val dx: int32
    val dy:int32
    val mouseData:uint32
    val dwFlags: uint32
    val time: uint32
    val dwExtraInfo: UIntPtr
    new(_dx, _dy, _mouseData, _dwFlags, _time, _dwExtraInfo) = {dx=_dx; dy=_dy; mouseData=_mouseData; dwFlags=_dwFlags; time=_time; dwExtraInfo=_dwExtraInfo}
end

[<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)>]
type KEYBDINPUT = struct
    val wVk: uint16
    val wScan: uint16
    val dwFlags: uint32
    val time: uint32
    val dwExtraInfo: UIntPtr
    new(_wVk, _wScan, _dwFlags, _time, _dwExtraInfo) = {wVk =_wVk; wScan = _wScan; dwFlags = _dwFlags; time = _time; dwExtraInfo = _dwExtraInfo}
end

[<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)>]
type HARDWAREINPUT = struct
    val uMsg: uint32
    val wParamL: uint16
    val wParamH: uint16
    new(_uMsg, _wParamL, _wParamH) = {uMsg = _uMsg; wParamL = _wParamL; wParamH = _wParamH}
end

#nowarn "9"
[<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)>]
type InputUnion = struct
    [<System.Runtime.InteropServices.FieldOffset(0)>]
    val mutable mi : MOUSEINPUT
    [<System.Runtime.InteropServices.FieldOffset(0)>]
    val mutable ki : KEYBDINPUT
    [<System.Runtime.InteropServices.FieldOffset(0)>]
    val mutable hi : HARDWAREINPUT 
end

[<System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)>]
type LPINPUT  = struct
    val mutable ``type``: int // 1 is keyboard
    val mutable u: InputUnion
end

type Win32() =
    [<System.Runtime.InteropServices.DllImport("User32.dll")>]
    static extern bool SetCursorPos(int X, int Y)

    //[<System.Runtime.InteropServices.DllImport("User32.dll")>]
    //static extern bool GetCursorPos(POINT *p)

    [<System.Runtime.InteropServices.DllImport("User32.dll")>]
    static extern void mouse_event(uint32 dwFlags, int dx, int dy, uint32 cButtons, int dwExtraInfo)
    
    static let MOUSEEVENTF_LEFTDOWN     = 0x02u
    static let MOUSEEVENTF_LEFTUP       = 0x04u
    static let MOUSEEVENTF_RIGHTDOWN    = 0x08u
    static let MOUSEEVENTF_RIGHTUP      = 0x10u
    static let MOUSEEVENTF_MIDDLEDOWN   = 0x20u
    static let MOUSEEVENTF_MIDDLEUP     = 0x40u
    static let MOUSEEVENTF_WHEEL        = 0x800u
    static let WHEEL_DELTA = 120
    // ||| these in if want absolute coords to send event, otherwise is relative
    //static let MOUSEEVENTF_ABSOLUTE     = 0x8000
    //static let MOUSEEVENTF_MOVE         = 0x0001

    // mouse sonar
    static let SPI_SETMOUSESONAR = 0x101Du
    static let SPIF_UPDATEINIFILE = 0x01u
    static let SPIF_SENDCHANGE = 0x02u
    [<System.Runtime.InteropServices.DllImport("user32.dll")>]
    static extern int SystemParametersInfo(uint32 uAction, uint32 uParam, bool lpvParam, uint32 fuWinIni)
    
    [<System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)>]
    static extern uint32 SendInput(int numberOfInputs, LPINPUT[] inputs, int sizeOfInputStructure)

    /////////////////////////////////////////////////////////////////////////////

    //This simulates a left mouse click
    static member LeftMouseClick() =
        //let mutable p = POINT(0,0)
        //GetCursorPos(&&p) |> ignore
        //System.Threading.ThreadPool.QueueUserWorkItem(System.Threading.WaitCallback(fun _ -> 
            //mouse_event(MOUSEEVENTF_LEFTDOWN, p.x, p.y, 0, 0)
            //mouse_event(MOUSEEVENTF_LEFTUP, p.x, p.y, 0, 0)
            //mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0u, 0)
            //System.Threading.Thread.Sleep(50)
            //mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0u, 0)
            //)) |> ignore
        // this seems to work fine, above code would perhaps be more real-ish timing-based
        mouse_event(MOUSEEVENTF_LEFTDOWN ||| MOUSEEVENTF_LEFTUP, 0, 0, 0u, 0)
    static member RightMouseClick() =
        mouse_event(MOUSEEVENTF_RIGHTDOWN ||| MOUSEEVENTF_RIGHTUP, 0, 0, 0u, 0)
    static member MiddleMouseClick() =
        mouse_event(MOUSEEVENTF_MIDDLEDOWN ||| MOUSEEVENTF_MIDDLEUP, 0, 0, 0u, 0)
    static member ScrollWheelRotateUp() =
        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, uint32 WHEEL_DELTA, 0)
    static member ScrollWheelRotateDown() =
        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, uint32 (-WHEEL_DELTA), 0)
    
    static member SetCursor(x,y) = 
        let transformedPoint = Point(x*appScale(), y*appScale())
        let pos = theWindow.PointToScreen(transformedPoint)
        SetCursorPos(int pos.X, int pos.Y) |> ignore

    static member SetSonar(enable) =
        SystemParametersInfo(SPI_SETMOUSESONAR, 0u, enable, SPIF_UPDATEINIFILE ||| SPIF_SENDCHANGE)

    static member DoSendInput(numberOfInputs, inputs, sizeOfInputStructure) =
        SendInput(numberOfInputs, inputs, sizeOfInputStructure)

let volumeChanged = new Event<int>()
let soundPlayer = new MediaPlayer()
soundPlayer.Volume <- float TrackerModelOptions.Volume / 300.
soundPlayer.Open(new Uri("confirm_speech.wav", UriKind.Relative))
let PlaySoundForSpeechRecognizedAndUsedToMark() =
    soundPlayer.Position <- TimeSpan(0L)
    soundPlayer.Play()
let soundPlayer2 = new MediaPlayer()
soundPlayer2.Volume <- float TrackerModelOptions.Volume / 300.
soundPlayer2.Open(new Uri("reminder_clink.wav", UriKind.Relative))
let PlaySoundForReminder() =
    soundPlayer2.Position <- TimeSpan(0L)
    soundPlayer2.Play()
volumeChanged.Publish.Add(fun v ->
    soundPlayer.Volume <- float v / 300.
    soundPlayer2.Volume <- float v / 300.
    )

let mutable ErrorBeepWithReminderLogText = fun (_txt:string) -> ()

let GetResourceStream(name) = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(name)

let unparent(fe:FrameworkElement) =
    if fe.Parent<> null then
        (fe.Parent :?> Panel).Children.Remove(fe)

let canvasAdd(c:Canvas, item, left, top) =
    if item <> null then
        c.Children.Add(item) |> ignore
        Canvas.SetTop(item, top)
        Canvas.SetLeft(item, left)

let gridAdd(g:Grid, x, c, r) =
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
let dock(e, dock) =
    let d = new DockPanel(LastChildFill=false)
    DockPanel.SetDock(e, dock)
    d.Children.Add(e) |> ignore
    d
let makeArrow(targetX, targetY, sourceX, sourceY, brush) =
    let tx,ty = targetX, targetY
    let sx,sy = sourceX, sourceY
    // line from source to target
    let line = new Shapes.Line(X1=sx, Y1=sy, X2=tx, Y2=ty, Stroke=brush, StrokeThickness=3.)
    line.StrokeDashArray <- new DoubleCollection(seq[5.;4.])
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
    triangle.Points <- new PointCollection([Point(tx,ty); Point(p1x,p1y); Point(p2x,p2y)])
    line, triangle
let scaleUpCheckBoxBox(cb:CheckBox, scale) =
    cb.LayoutTransform <- new ScaleTransform(scale, scale)
    (cb.Content :?> FrameworkElement).LayoutTransform <- new ScaleTransform(1.0/scale, 1.0/scale)

let almostBlack = freeze(new SolidColorBrush(Color.FromRgb(30uy, 30uy, 30uy)))
let almostBlackHoverFeedback = freeze(new SolidColorBrush(Color.FromRgb(30uy, 50uy, 50uy)))
let makeButton(text, fontSizeOpt, fgOpt) =
    let tb = new TextBox(Text=text, IsReadOnly=true, IsHitTestVisible=false, TextAlignment=TextAlignment.Center, VerticalContentAlignment=VerticalAlignment.Center, BorderThickness=Thickness(0.), Background=Brushes.Transparent)   // text vertically centered in every tracker button (transparent: the button's own rounded fill shows through)
    match fontSizeOpt with | None -> () | Some x -> tb.FontSize <- x
    match fgOpt with | None -> () | Some x -> tb.Foreground <- x
    let button = new Button(Content=tb, HorizontalContentAlignment=HorizontalAlignment.Stretch, VerticalContentAlignment=VerticalAlignment.Stretch)
    button

// the rounded dark button look used across the app (thin gray border, 4px corners, dark fill, orange border on hover); border/fill come from the Button's own BorderBrush/Background
let roundedBorderBrush = freeze(new SolidColorBrush(Color.FromRgb(58uy,58uy,58uy)))
let roundedButtonTemplate =
    let xaml = "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}' CornerRadius='4' SnapsToDevicePixels='True'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='#FFA31A'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='bd' Property='Background' Value='#241A0A'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='bd' Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate>"
    System.Windows.Markup.XamlReader.Parse(xaml) :?> ControlTemplate
// rounded checkbox: dark rounded box, orange fill with a dark check when checked, orange dash when indeterminate
let roundedCheckBoxTemplate =
    let xaml = "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='CheckBox'><Grid Background='Transparent' SnapsToDevicePixels='True'><Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions><Border x:Name='box' Width='14' Height='14' CornerRadius='3' BorderThickness='1.5' BorderBrush='#8A8A8A' Background='#171717' VerticalAlignment='Center'><Path x:Name='mark' Data='M 2,5.5 L 4.6,8.2 L 9,2.5' Stroke='#0C0C0C' StrokeThickness='2' Visibility='Collapsed' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ContentPresenter Grid.Column='1' Margin='{TemplateBinding Padding}' VerticalAlignment='Center' HorizontalAlignment='Left' RecognizesAccessKey='True'/></Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='box' Property='Background' Value='#FFA31A'/><Setter TargetName='box' Property='BorderBrush' Value='#FFA31A'/><Setter TargetName='mark' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsChecked' Value='{x:Null}'><Setter TargetName='box' Property='Background' Value='#FFA31A'/><Setter TargetName='box' Property='BorderBrush' Value='#FFA31A'/><Setter TargetName='mark' Property='Data' Value='M 2,5.5 L 9,5.5'/><Setter TargetName='mark' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='#FFA31A'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate>"
    System.Windows.Markup.XamlReader.Parse(xaml) :?> ControlTemplate
let roundedTextBoxTemplate =
    let xaml = "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TextBox'><Border x:Name='bd' Background='#171717' BorderBrush='#3A3A3A' BorderThickness='1' CornerRadius='6' SnapsToDevicePixels='True'><ScrollViewer x:Name='PART_ContentHost' Focusable='False' HorizontalScrollBarVisibility='Hidden' VerticalScrollBarVisibility='Hidden'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='#FFA31A'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='#FFA31A'/></Trigger></ControlTemplate.Triggers></ControlTemplate>"
    System.Windows.Markup.XamlReader.Parse(xaml) :?> ControlTemplate
let styleRoundedTextBox(t:TextBox) = t.Template <- roundedTextBoxTemplate
let roundedButtonFill = freeze(new SolidColorBrush(Color.FromRgb(23uy,23uy,23uy)))
// setters that give a Button the rounded look; also used inside the dialog-specific button styles that replace the app-wide default
let addRoundedButtonSetters(style:Style) =
    style.Setters.Add(new Setter(Control.TemplateProperty, roundedButtonTemplate))
    style.Setters.Add(new Setter(Control.BorderBrushProperty, roundedBorderBrush))
    style.Setters.Add(new Setter(Control.BackgroundProperty, roundedButtonFill))
    style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Orange))
    style.Setters.Add(new Setter(Control.PaddingProperty, Thickness(0.)))
    style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null))
// app-wide defaults: every Button and CheckBox gets the rounded look unless a window / panel provides its own style
let installAppWideStyles() =
    let app = System.Windows.Application.Current
    if not(isNull app) then
        let bs = new Style(typeof<Button>)
        addRoundedButtonSetters(bs)
        app.Resources.[typeof<Button>] <- bs
        let cs = new Style(typeof<CheckBox>)
        cs.Setters.Add(new Setter(Control.TemplateProperty, roundedCheckBoxTemplate))
        cs.Setters.Add(new Setter(Control.PaddingProperty, Thickness(4.,0.,0.,0.)))
        cs.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Orange))
        cs.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null))
        app.Resources.[typeof<CheckBox>] <- cs
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

let transformColor(bmp:System.Drawing.Bitmap, f) =
    let r = new System.Drawing.Bitmap(bmp.Width,bmp.Height)
    for px = 0 to bmp.Width-1 do
        for py = 0 to bmp.Height-1 do
            let c = bmp.GetPixel(px,py)
            r.SetPixel(px, py, f c)
    r

let greyscale(bmp:System.Drawing.Bitmap) =
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
let desaturate(bmp:System.Drawing.Bitmap, pct) = transformColor(bmp, (fun c -> desaturateColor(c,pct)))
let darkenImpl pct (bmp:System.Drawing.Bitmap) =
    let r = new System.Drawing.Bitmap(bmp.Width,bmp.Height)
    for px = 0 to bmp.Width-1 do
        for py = 0 to bmp.Height-1 do
            let c = bmp.GetPixel(px,py)
            let c = System.Drawing.Color.FromArgb(int(float c.R * pct), int(float c.G * pct), int(float c.B * pct))
            r.SetPixel(px, py, c)
    r
let darken(bmp:System.Drawing.Bitmap) =
    darkenImpl 0.5 bmp
let mediaColor(c:System.Drawing.Color) =
    Media.Color.FromArgb(c.A, c.R, c.G, c.B)

(*
let ConvertPixelFormat(pixelFormat:System.Drawing.Imaging.PixelFormat) =
    if (pixelFormat = System.Drawing.Imaging.PixelFormat.Format24bppRgb)       then System.Windows.Media.PixelFormats.Bgr24

    elif (pixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppArgb)      then System.Windows.Media.PixelFormats.Bgr32
    elif (pixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppPArgb)     then System.Windows.Media.PixelFormats.Pbgra32
    elif (pixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppRgb)       then System.Windows.Media.PixelFormats.Bgr32

    elif (pixelFormat = System.Drawing.Imaging.PixelFormat.Format16bppGrayScale)   then System.Windows.Media.PixelFormats.Gray16
    elif (pixelFormat = System.Drawing.Imaging.PixelFormat.Format16bppRgb555)    then System.Windows.Media.PixelFormats.Bgr555
    elif (pixelFormat = System.Drawing.Imaging.PixelFormat.Format16bppRgb565)    then System.Windows.Media.PixelFormats.Bgr565

    elif (pixelFormat = System.Drawing.Imaging.PixelFormat.Indexed)              then System.Windows.Media.PixelFormats.Bgr101010
    elif (pixelFormat = System.Drawing.Imaging.PixelFormat.Format1bppIndexed)    then System.Windows.Media.PixelFormats.Indexed1
    elif (pixelFormat = System.Drawing.Imaging.PixelFormat.Format4bppIndexed)    then System.Windows.Media.PixelFormats.Indexed4
    elif (pixelFormat = System.Drawing.Imaging.PixelFormat.Format8bppIndexed)    then System.Windows.Media.PixelFormats.Indexed8

    elif (pixelFormat = System.Drawing.Imaging.PixelFormat.Format48bppRgb)       then System.Windows.Media.PixelFormats.Rgb48

    elif (pixelFormat = System.Drawing.Imaging.PixelFormat.Format64bppArgb)      then System.Windows.Media.PixelFormats.Prgba64

    elif (pixelFormat = System.Drawing.Imaging.PixelFormat.Undefined)            then System.Windows.Media.PixelFormats.Default
    else
        raise(new NotSupportedException("Convertion not supported with " + pixelFormat.ToString()))
*)
let mutable dpi = None : DpiScale option
(*
let BmpToSource(bmp:System.Drawing.Bitmap) =
    let bitmapData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat)
    let bitmapSource = 
        System.Windows.Media.Imaging.BitmapSource.Create(bitmapData.Width, bitmapData.Height, float bmp.HorizontalResolution, float bmp.VerticalResolution,
            //PixelFormats.Bgra32, 
            ConvertPixelFormat(bmp.PixelFormat),
            null, bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride)
    bmp.UnlockBits(bitmapData)
    bitmapSource
*)
let BItoImage(bi:System.Windows.Media.Imaging.BitmapImage) =
    if dpi.Value.DpiScaleX=1.0 && dpi.Value.DpiScaleY=1.0 then
        new Image(Source=bi)
    else
        //let ppiy = System.Math.Round(dpi.Value.PixelsPerInchY)
        let pf = PixelFormats.Bgra32   // Bitmap uses System.Drawing.Imaging.PixelFormat.Format32bppArgb, and this is the corresponding thingy
        let rawStride = (bi.PixelWidth * pf.BitsPerPixel + 7) / 8
        let rawImage : byte[] = Array.zeroCreate (rawStride * bi.PixelHeight)
        bi.CopyPixels(rawImage, rawStride, 0)                                                            // dpi.ppix, dpi.ppiy
        new Image(Source=System.Windows.Media.Imaging.BitmapSource.Create(bi.PixelWidth, bi.PixelHeight, 96., 96., pf, null, rawImage, rawStride))
let BMPToBI(b:System.Drawing.Bitmap) =
    use ms = new System.IO.MemoryStream()
    b.Save(ms, System.Drawing.Imaging.ImageFormat.Png)
    ms.Position <- 0L
    let bitmapImage = new System.Windows.Media.Imaging.BitmapImage()
    bitmapImage.BeginInit()
    bitmapImage.StreamSource <- ms
    bitmapImage.CacheOption <- System.Windows.Media.Imaging.BitmapCacheOption.OnLoad
    bitmapImage.EndInit()
    bitmapImage.Freeze()
    bitmapImage
let BMPtoImage(bmp:System.Drawing.Bitmap) =
    let ms = new System.IO.MemoryStream()
    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png)  // must be png (not bmp) to save transparency info
    ms.Seek(0L, System.IO.SeekOrigin.Begin) |> ignore
    let bmimage = new System.Windows.Media.Imaging.BitmapImage()
    bmimage.BeginInit()
    
    // this is slower in practice
    //bmimage.CacheOption <- System.Windows.Media.Imaging.BitmapCacheOption.OnLoad    // can 'use' ms with this
    
    // this is faster in practice
    bmimage.CreateOptions <- System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation
    bmimage.CacheOption <- System.Windows.Media.Imaging.BitmapCacheOption.OnDemand    // must 'let' ms with this
    
    bmimage.StreamSource <- ms
    bmimage.EndInit()
    let i = new Image()
    i.Source <- bmimage
//    i.Source <- BmpToSource(bmp)
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
    let Draw(isPale) =
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
    member _this.MakeMagenta() = s.Stroke <- Brushes.Magenta; s.Opacity <- 0.85; s.StrokeThickness <- 5.
    member _this.Hide() = s.Opacity <- 0.0
    member _this.MakeGreenWithBriefAnimation() = 
        if TrackerModelOptions.AnimateShopHighlights.Value then
            Draw(false)
            let da = new System.Windows.Media.Animation.DoubleAnimation(12.0, 4.0, new Duration(System.TimeSpan.FromSeconds(0.5)))
            s.BeginAnimation(Shapes.Rectangle.StrokeThicknessProperty, da)
            let ca = new System.Windows.Media.Animation.ColorAnimation(From=Colors.Cyan, To=Colors.Lime, Duration=new Duration(System.TimeSpan.FromSeconds(0.5)))
            let brush = new SolidColorBrush()
            s.Stroke <- brush
            brush.BeginAnimation(SolidColorBrush.ColorProperty, ca)
        else
            this.MakeGreen()
    member _this.MakeYellowWithBriefAnimation() = 
        if TrackerModelOptions.AnimateShopHighlights.Value then
            Draw(false)
            let da = new System.Windows.Media.Animation.DoubleAnimation(12.0, 4.0, new Duration(System.TimeSpan.FromSeconds(0.5)))
            s.BeginAnimation(Shapes.Rectangle.StrokeThicknessProperty, da)
            let ca = new System.Windows.Media.Animation.ColorAnimation(From=Colors.Cyan, To=Colors.Yellow, Duration=new Duration(System.TimeSpan.FromSeconds(0.5)))
            let brush = new SolidColorBrush()
            s.Stroke <- brush
            brush.BeginAnimation(SolidColorBrush.ColorProperty, ca)
        else
            this.MakeYellow()
    member _this.MakeRedWithBriefAnimation() = 
        if TrackerModelOptions.AnimateShopHighlights.Value then
            Draw(false)
            let da = new System.Windows.Media.Animation.DoubleAnimation(12.0, 4.0, new Duration(System.TimeSpan.FromSeconds(0.5)))
            s.BeginAnimation(Shapes.Rectangle.StrokeThicknessProperty, da)
            let ca = new System.Windows.Media.Animation.ColorAnimation(From=Colors.Cyan, To=Colors.Red, Duration=new Duration(System.TimeSpan.FromSeconds(0.5)))
            let brush = new SolidColorBrush()
            s.Stroke <- brush
            brush.BeginAnimation(SolidColorBrush.ColorProperty, ca)
        else
            this.MakeRed()
    member _this.Shape = s

// see also
// https://stackoverflow.com/questions/63184765/wpf-left-click-and-drag
// https://stackoverflow.com/questions/12802122/wpf-handle-drag-and-drop-as-well-as-left-click
type DragDropSurface<'T>(surface:FrameworkElement, onStartDrag : _ * ('T -> unit) -> unit ) = // surface is the entire element we can drag across that encompasses all the multiple items and sees all mouse interactions in the area
    let mutable isDragging = false
    let mutable startPoint : Point option = None
    let mutable clickFunc = None
    let mutable initiateDragFunc = None
    do
        surface.PreviewMouseMove.Add(fun ea ->
            if (ea.LeftButton = System.Windows.Input.MouseButtonState.Pressed ||
                ea.MiddleButton = System.Windows.Input.MouseButtonState.Pressed ||
                ea.RightButton = System.Windows.Input.MouseButtonState.Pressed)
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
            )
        surface.MouseUp.Add(fun ea ->
            //printfn "MouseUp"
            if not isDragging then
                if clickFunc.IsSome then
                    clickFunc.Value(ea)
                // else e.g. dragged into and released
            startPoint <- None
            clickFunc <- None
            initiateDragFunc <- None
            )
    member this.RegisterClickable(e:FrameworkElement, onClick, onInitiateDrag) =     // each individual element says how it should respond to a click, or to being an initiator of a drag
        e.PreviewMouseDown.Add(fun ea -> 
            //printfn "PrevMD %A" (Input.Mouse.GetPosition(surface))
            startPoint <- Some(ea.GetPosition(surface))
            clickFunc <- Some(onClick)
            initiateDragFunc <- Some(onInitiateDrag)
            )

////////////////////////////////////////////////////

let alphaNumBmp =
    let imageStream = GetResourceStream("alphanumerics3x5.png")
    new System.Drawing.Bitmap(imageStream)
let paintAlphanumerics3x5(ch, color, bmp:System.Drawing.Bitmap, x, y) =  // x and y are 1-pixel coordinates, even though bmp is blown up 3x
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
    let r = new System.Drawing.Bitmap(w,h)
    for px = 0 to w-1 do
        for py = 0 to h-1 do
            r.SetPixel(px, py, System.Drawing.Color.Transparent)
    paintAlphanumerics3x5(ch, color, r, x, y)
    r

let openCaveIconBmp = 
    let imageStream = GetResourceStream("open_cave20x20.png")
    let bmp = new System.Drawing.Bitmap(imageStream)
    bmp

let (filled_small_heart_bmp, empty_small_heart_bmp) =
    let imageStream = GetResourceStream("icons8x8.png")
    let bmp = new System.Drawing.Bitmap(imageStream)
    let a = [|  
        for i = 0 to bmp.Width/8 - 1 do
            let r = new System.Drawing.Bitmap(8,8)
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
    let bmp = new System.Drawing.Bitmap(imageStream)
    let a = [|  
        for i = 0 to bmp.Width/7 - 1 do
            let r = new System.Drawing.Bitmap(7*3,7*3)
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
    let bmp = new System.Drawing.Bitmap(imageStream)
    let a = [|  
        for i = 0 to bmp.Width/16 - 1 do
            let r = new System.Drawing.Bitmap(18,18)  // border around it
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
    let bmp = new System.Drawing.Bitmap(imageStream)
    let a = [|  
        for i = 0 to bmp.Width/16 - 1 do
            let r = new System.Drawing.Bitmap(18,18)  // border around it
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
//    let bmp = new System.Drawing.Bitmap(imageStream)
//    let a = [|  
//        for i = 0 to bmp.Width/16 - 1 do
//            let r = new System.Drawing.Bitmap(18,18)  // border around it
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
let unborder(bmp:System.Drawing.Bitmap) =
    let r = new System.Drawing.Bitmap(bmp.Width-2, bmp.Height-2)
    for i = 0 to r.Width-1 do
        for j = 0 to r.Height-1 do
            r.SetPixel(i,j,bmp.GetPixel(i+1,j+1))
    r
let zi_staircase_unborderedBI = unborder(zi_staircase) |> BMPToBI

let _brightTriforce_bmp, fullOrangeTriforce_bmp, _dullOrangeTriforce_bmp, greyTriforce_bmp, owHeartSkipped_bmp, owHeartEmpty_bmp, owHeartFull_bmp, iconRightArrow_bmp, iconCheckMark_bmp, iconExtras_bmp, iconDisk_bmp, owHeartTallFull_bmp = 
    let imageStream = GetResourceStream("icons10x10.png")
    let bmp = new System.Drawing.Bitmap(imageStream)
    let all = [|
        for i = 0 to bmp.Width/10 - 1 do
            let r = new System.Drawing.Bitmap(10*3,10*3)
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
        let bmp = owHeartTallFull_bmp.Clone() :?> System.Drawing.Bitmap
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
                let bmp = greyTriforce_bmp.Clone() :?> System.Drawing.Bitmap
                paintAlphanumerics3x5(char(int ch + i), UNFOUND_NUMERAL_COLOR, bmp, 4, 4)
                yield bmp
            |] |]
    a.[0], a.[1]
let emptyFoundNumberedTriforce_bmps, emptyFoundLetteredTriforce_bmps = 
    let a = [|
        for ch in ['1'; 'A'] do
            yield [|
            for i = 0 to 7 do
                let bmp = greyTriforce_bmp.Clone() :?> System.Drawing.Bitmap
                paintAlphanumerics3x5(char(int ch + i), FOUND_NUMERAL_COLOR, bmp, 4, 4)
                yield bmp
            |] |]
    a.[0], a.[1]
let fullNumberedUnfoundTriforce_bmps, fullNumberedFoundTriforce_bmps, fullLetteredUnfoundTriforce_bmps, fullLetteredFoundTriforce_bmps =
    let a = [|
        for ch in ['1'; 'A'] do
            yield [|
            for i = 0 to 7 do
                let bmp = fullOrangeTriforce_bmp.Clone() :?> System.Drawing.Bitmap
                paintAlphanumerics3x5(char(int ch + i), UNFOUND_NUMERAL_COLOR, bmp, 4, 4)
                yield bmp
            |]
            yield [|
            for i = 0 to 7 do
                let bmp = fullOrangeTriforce_bmp.Clone() :?> System.Drawing.Bitmap
                paintAlphanumerics3x5(char(int ch + i), FOUND_NUMERAL_COLOR, bmp, 4, 4)
                yield bmp
            |]
        |]
    a.[0], a.[1], a.[2], a.[3]
let unfoundL9_bmp,foundL9_bmp =
    let unfoundTriforceColor = emptyUnfoundNumberedTriforce_bmps.[0].GetPixel(15, 28)
    let u = new System.Drawing.Bitmap(10*3,10*3)
    let f = new System.Drawing.Bitmap(10*3,10*3)
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
    let bmp = new System.Drawing.Bitmap(imageStream)
    let r = new System.Drawing.Bitmap(16,32)
    for x = 0 to 15 do
        for y = 0 to 31 do
            let c = bmp.GetPixel(x/2, y/2)
            if c.ToArgb() <> System.Drawing.Color.Black.ToArgb() then
                r.SetPixel(x, y, c)
    r

let mutable entranceRoomArrowColor = None
let dungeonRoomBmpPairsF() =
    let imageStream = GetResourceStream("new_icons13x9.png")
    let bmp = new System.Drawing.Bitmap(imageStream)
    let a = [|  
        for i = 0 to bmp.Width/13 - 1 do
            let cr = new System.Drawing.Bitmap(13*3,9*3)
            let ur = new System.Drawing.Bitmap(13*3,9*3)
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
    let bmp = new System.Drawing.Bitmap(imageStream)
    let a = [|  
        for i = 0 to bmp.Width/13 - 1 do
            let ur = new System.Drawing.Bitmap(13,9)
            let cr = new System.Drawing.Bitmap(13,9)
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
    let bmp = new System.Drawing.Bitmap(imageStream)
    let a = [|  
        for i = 0 to bmp.Width/3 - 1 do
            let r = new System.Drawing.Bitmap(5*3,5*3)
            let b = new System.Drawing.Bitmap(8*3,8*3)
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
    new System.Drawing.Bitmap(imageStream)
let zhMapIcons =
    let imageStream = GetResourceStream("s_icon_overworld_strip39.png")
    new System.Drawing.Bitmap(imageStream)
let zhDungeonIcons =
    let imageStream = GetResourceStream("s_btn_tr_dungeon_cell_strip3.png")
    new System.Drawing.Bitmap(imageStream)
let zhDungeonNums =
    let imageStream = GetResourceStream("s_btn_tr_dungeon_num_strip18.png")
    new System.Drawing.Bitmap(imageStream)


// 'Boots Replace Boomerangs' startup option: the boomerang is shown as the boots, and the magical (blue) boomerang as the magical boots
let private bmpFromBase64(b64:string) =
    use ms = new System.IO.MemoryStream(System.Convert.FromBase64String(b64))
    let src = new System.Drawing.Bitmap(ms)
    let r = new System.Drawing.Bitmap(src)   // detach from the stream
    r.SetResolution(96.0f, 96.0f)
    r
let boots_bmp = bmpFromBase64 "iVBORw0KGgoAAAANSUhEUgAAABUAAAAVCAYAAACpF6WWAAABq0lEQVR4nN2Uz2oUQRCHv+rpyay7MWuIEjwF/LMgGHISTyo5iCdfwIuPIb6PCOLFJ/DiUYUEkSCCB0nWEBNMgu5mZ7q7PAxK3O7dZA65+Dt2V39d1b+qhjOQjC+8fTirqkpw4d8NhSDGzbWMffxmMP9+z+8bgaAx1I4vqCp5kdO53AY9dsIIg4MRejRKpHICNLhAZ/Ec1+9dhTLUAFVoWfprfb5/3EJkOjWCZnnG4e5P1l99iILLKtDOLRrKZlAURIRsJoPj7yVgPGhwU4FJqHeeucVZenfj8rfWv7G70UdMw/L/ZItXNCgitXniFdWE1QmZU0U11H8ONVJ7NcmutFFTJMDAUabG8++lTYFeYXneXLt1MbvQnUk3V0Oo2mGlPLlZrL1c7fzodU03lfCpoCICVeDS0gIrD25g2oXrH1Tu6XLx4tmd9vPx+IlvqjrW7EHJC0t+vqDTbVlXOXqq99V7gEcnQsUIUuSI8bHFClduL0Ge8en1Z7e3uR+dj6BihNGvkp2N7XpMU1UAkgnVsLTWxhER1GSG4eERX959ZXInKmj9TZrsTOYn1m8QJqArILh0CgAAAABJRU5ErkJggg=="
let magical_boots_bmp = bmpFromBase64 "iVBORw0KGgoAAAANSUhEUgAAABUAAAAVCAYAAACpF6WWAAABkElEQVR4nM2UuUpDQRSGv5m5XJcEF0QQFGxMYxHb4AJpfAUVLXwCewt7KwvRIk/gWtvnCYJNELQIgqAIwS2Cy50Zi7lBSCbLFRF/GC53me/8Z+45B/5Cto201p/WWpvP54cAlFJeRuB7+PIGl7egpLsXwKeGsUGYGAFr2xvzQiv3sHUC6V7QFgIJj6+wPg9rC2B+Ap0eh6MNEAJs7NRYCGPn9QwSQbWF2puDgrtqA+kQ+tvzWkOvbmHrFFI9zqGS8PQKa7OwMucCJIYKAaFyqw4Ng85p19XyM+tZ3arL2Mn0f6BSSqGUQtTLo0HeH9VJtVrtQ2vd8n1iqAVyudxUOtV3XS6Xn6vVqrENfZso/SCQgQD293bPi8XiQzabHWwEdg01BvpDKF7A5iFU7k1kjIkKhcJxqVQ6aArugwhcoSsJIjYi46Fy8w53TzJI9UAmk1mMt6x2hEbGASLt5oDge7AIATtnbjZsL5toZrJ5vxc6OgBLOdeavk4SwEcEY8MykJ4DbCo06zv5DhKtCvY39QWjQ6CiSDEc2gAAAABJRU5ErkJggg=="
let boomerangOrBootsBmp() = if TrackerModel.BootsReplaceBoomerangs then boots_bmp else boomerang_bmp
let magicBoomerangOrMagicalBootsBmp() = if TrackerModel.BootsReplaceBoomerangs then magical_boots_bmp else magic_boomerang_bmp
let customItemBmps : System.Drawing.Bitmap[] = Array.zeroCreate 16   // the user's ExtraIcons-folder icons, resized to item-icon size (filled in by DrawingLayer.applyCustomMenuIcons)
let allItemBMPs = [| book_bmp; boomerang_bmp; bow_bmp; power_bracelet_bmp; ladder_bmp; magic_boomerang_bmp; key_bmp; raft_bmp; recorder_bmp; red_candle_bmp; red_ring_bmp; silver_arrow_bmp; wand_bmp; white_sword_bmp |]
let allItemBMPsWithHeartShuffle = [| yield! allItemBMPs; for _i = 0 to 8 do yield heart_container_bmp |]

// Extra item sprites (potions, letter, progressive sword). Drawn in code as 7x7 pixel art, blown up 3x like the icons7x7 sheet sprites.
let makeSprite7x7(rows:string[], palette:(char*System.Drawing.Color) list) =
    let r = new System.Drawing.Bitmap(7*3,7*3)
    for px = 0 to 7*3-1 do
        for py = 0 to 7*3-1 do
            let ch = rows.[py/3].[px/3]
            let color = match List.tryFind (fun (c,_) -> c = ch) palette with | Some(_,col) -> col | None -> System.Drawing.Color.Transparent
            r.SetPixel(px, py, color)
    r
// triforce piece icon (the item-sheet triforce, centered in a 21x21 cell)
let triforce_item_bmp =
    let r = new System.Drawing.Bitmap(21,21)
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
let readCacheFileOrCreateBmp(filename, createF : unit -> System.Drawing.Bitmap) =
    // always treat the tiles as 96dpi, whatever the cache file (or this machine's display scaling) says; a tile with another dpi gets drawn smaller than 48x33, leaving dark lines between map tiles
    if System.IO.File.Exists(filename) then
        let bmp = System.Drawing.Bitmap.FromFile(filename) :?> System.Drawing.Bitmap
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
    let fullTileBmp = new System.Drawing.Bitmap(16*3,11*3)
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
        let image = new System.Drawing.Bitmap(alternativeOverworldMapFilename) :> System.Drawing.Image
        let bitmap = new System.Drawing.Bitmap( image, new System.Drawing.Size( 256*3, 88*3 ) )
        for x = 0 to 15 do
            for y = 0 to 7 do
                let tile = new System.Drawing.Bitmap(16*3,11*3)
                for px = 0 to 16*3-1 do
                    for py = 0 to 11*3-1 do
                        tile.SetPixel(px, py, bitmap.GetPixel((x*16*3 + px), (y*11*3 + py)))
                tiles.[x,y] <- tile
    else
        for x = 0 to 15 do
            for y = 0 to 7 do
                let tile = 
                    let filename = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, sprintf """Overworld\quest.%d.ow.%2d.%2d.bmp""" n x y)
                    readCacheFileOrCreateBmp(filename, fun () ->
                        let tile = new System.Drawing.Bitmap(16*3,11*3)
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
    new System.Drawing.Bitmap(imageStream)

// a full tile with nothing drawn on it (used under marker icons when the tile's own icon is shown as one of those marker slots)
let emptyTileBmp =
    let b = new System.Drawing.Bitmap(16*3,11*3)
    for px = 0 to 16*3-1 do
        for py = 0 to 11*3-1 do
            b.SetPixel(px, py, TRANS_BG)
    b
// the bomb (as at the top of the tracker), scaled to fit inside a w x h area and centered in it, on bg
let bombFitBmp(w:int, h:int, bg:System.Drawing.Color) =
    let r = new System.Drawing.Bitmap(w, h)
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
    let bmp = new System.Drawing.Bitmap(5*3,9*3)
    for px = 0 to 5*3-1 do
        for py = 0 to 9*3-1 do
            bmp.SetPixel(px, py, System.Drawing.Color.Yellow)
    paintAlphanumerics3x5('?', System.Drawing.Color.Black, bmp, 1, 2)
    bmp

let X_OPACITY = 0.55
let overworldCommonestFloorColorBrush = freeze(new SolidColorBrush(Color.FromRgb(204uy,176uy,136uy)))
let overworldCommonestFloorColorDarkBrush = freeze(new SolidColorBrush(Color.FromRgb(byte(float 204 * X_OPACITY),byte(float 176 * X_OPACITY),byte(float 136 * X_OPACITY))))
let unmarkedBmp = new System.Drawing.Bitmap(5*3,9*3)
let dontCareBmp = new System.Drawing.Bitmap(5*3,9*3)

// each overworld map tile may have multiple icons that can represent it (e.g. dungeon 1 versus dungeon A)
// we store a table, where the array index is the mapSquareChoiceDomain index of the general entry type, and the value there is a list of all possible icons
// The sword cave icons (5x9 art pixels, 3x3 screen pixels each) without the brown cave floor: the number/letter becomes white with a black outline,
// and the rest of the brown is transparent. Art that has no brown (already restyled) is returned unchanged.
let restyleSwordCard(bmp:System.Drawing.Bitmap) : System.Drawing.Bitmap =
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
        let r = new System.Drawing.Bitmap(bmp.Width, bmp.Height)
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
let darkenKeepAlpha (pct:float) (bmp:System.Drawing.Bitmap) =
    let r = new System.Drawing.Bitmap(bmp.Width,bmp.Height)
    for px = 0 to bmp.Width-1 do
        for py = 0 to bmp.Height-1 do
            let c = bmp.GetPixel(px,py)
            r.SetPixel(px, py, System.Drawing.Color.FromArgb(int c.A, int(float c.R * pct), int(float c.G * pct), int(float c.B * pct)))
    r
// MapStateProxy will eventually be responsible for 'decoding' the current tracker state into the appropriate icon
let theInteriorBmpTable = Array.init (TrackerModel.dummyOverworldTiles.Length) (fun _ -> ResizeArray())
do
    let imageStream = GetResourceStream("ow_icons5x9.png")
    let interiorIconStrip = new System.Drawing.Bitmap(imageStream)
    let ubc = overworldCommonestFloorColorBrush
    for px = 0 to 5*3-1 do
        for py = 0 to 9*3-1 do
            unmarkedBmp.SetPixel(px, py, System.Drawing.Color.FromArgb(int ubc.Color.R, int ubc.Color.G, int ubc.Color.B))
    let dcbc = overworldCommonestFloorColorDarkBrush
    for px = 0 to 5*3-1 do
        for py = 0 to 9*3-1 do
            dontCareBmp.SetPixel(px, py, System.Drawing.Color.FromArgb(int dcbc.Color.R, int dcbc.Color.G, int dcbc.Color.B))
    let darkxbmp = new System.Drawing.Bitmap(5*3,9*3)
    for px = 0 to 5*3-1 do
        for py = 0 to 9*3-1 do
            darkxbmp.SetPixel(px, py, System.Drawing.Color.Black)
    let getInteriorIconFromStrip(i) = 
        let bmp = new System.Drawing.Bitmap(5*3,9*3)
        for px = 0 to 5*3-1 do
            for py = 0 to 9*3-1 do
                bmp.SetPixel(px, py, interiorIconStrip.GetPixel(i*5+px/3, py/3))
        bmp
    // 0-8  dungeons: 4 varieties (numbered yellow, numbered dark-yellow, lettered yellow, lettered dark-yellow)
    for labels in ["123456789";"ABCDEFGH9"] do
        for color in [System.Drawing.Color.Yellow; System.Drawing.Color.FromArgb(153,153,0)] do
            labels |> Seq.iteri (fun i ch ->
                let bmp = new System.Drawing.Bitmap(5*3,9*3)
                for px = 0 to 5*3-1 do
                    for py = 0 to 9*3-1 do
                        bmp.SetPixel(px, py, color)
                paintAlphanumerics3x5(ch, System.Drawing.Color.Black, bmp, 1, 2)
                theInteriorBmpTable.[i].Add(bmp)
                )
    // 9-12  any roads
    "1234" |> Seq.iteri (fun i ch ->
        let bmp = new System.Drawing.Bitmap(5*3,9*3)
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
        let bmp = new System.Drawing.Bitmap(5*3,9*3)
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
    let p3 (bmp:System.Drawing.Bitmap) (ux:int) (uy:int) =
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
    let pp1bmp = new System.Drawing.Bitmap(5*3, 9*3)
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
    let pp2bmp = new System.Drawing.Bitmap(5*3, 9*3)
    for px = 0 to 5*3-1 do
        for py = 0 to 9*3-1 do
            pp2bmp.SetPixel(px, py, System.Drawing.Color.Cyan)
    for ux,uy in [| (2,2); (1,3);(2,3);(3,3); (0,4);(1,4);(2,4);(3,4);(4,4); (1,5);(2,5);(3,5); (2,6) |] do
        p3 pp2bmp ux uy
    theInteriorBmpTable.[37].Add(pp2bmp)
// full tiles just have interior bmp in the center and transparent pixels all around (except for the final 'X' one)
// 'Use old potion/letter icons': the tracker's original overworld-tile artwork for the potion shop and the (blue) letter, shrunk to fit an item cell.
// The 5x9 interior icon is redrawn at 2x (10x18) and centered in a 21x21 cell.
let oldTileIconAsItem(interior:System.Drawing.Bitmap) =
    // drop the brown (corner-colored) background, crop to the artwork, and scale it up (nearest-neighbor) to fill as much of the 21x21 item cell as it can
    let r = new System.Drawing.Bitmap(21,21)
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
let oldTileIconWithBackgroundAsItem(interior:System.Drawing.Bitmap) =
    let r = new System.Drawing.Bitmap(21,21)
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
    let r = new System.Drawing.Bitmap(21,21)
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
    let r = new System.Drawing.Bitmap(5*3, 9*3)
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
let swordIconAsItem(interior0:System.Drawing.Bitmap) =
    let interior = restyleSwordCard interior0
    let r = new System.Drawing.Bitmap(21,21)
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
let swordMenuBmp(interior0:System.Drawing.Bitmap) =
    let interior = restyleSwordCard interior0
    let r = new System.Drawing.Bitmap(15,27)
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
let private extraSwordInterior(isProg:bool) : System.Drawing.Bitmap =
    let brown = System.Drawing.Color.FromArgb(185,122,87)
    let blade = if isProg then System.Drawing.Color.FromArgb(29,148,65) else System.Drawing.Color.FromArgb(128,128,255)
    let hilt = if isProg then System.Drawing.Color.FromArgb(136,0,21) else System.Drawing.Color.White
    let glyph = if isProg then [| "abbba"; "ababa"; "abbba"; "abaaa"; "abaaa" |] else [| "ababa"; "ababa"; "abbba"; "aaaba"; "aaaba" |]
    let rows = Array.append (Array.append [| "aaaaa" |] glyph) [| "aaaXa"; "HHHXX"; "aaaXa" |]
    let bmp = new System.Drawing.Bitmap(5*3, 9*3)
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
let swordBoxBmp(menuBmp:System.Drawing.Bitmap) =
    let r = new System.Drawing.Bitmap(menuBmp.Width, menuBmp.Height)
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
let theFullTileBmpTable = Array.init theInteriorBmpTable.Length (fun _ -> ResizeArray())
let initFull() =
    let len = theInteriorBmpTable.Length
    let BG = System.Drawing.Color.FromArgb(int TRANS_BG.A, int TRANS_BG.R, int TRANS_BG.G, int TRANS_BG.B)   // the compiler is being stupid, clone this color to workaround
    for i = 0 to len-1 do
        for interiorBmp in theInteriorBmpTable.[i] do
            let fullTileBmp = new System.Drawing.Bitmap(16*3,11*3)
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
    let bmp = new System.Drawing.Bitmap(imageStream)
    let a = [|
        for i = 0 to 3 do
            let r = new System.Drawing.Bitmap(16, 16)
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
    let bmp = new System.Drawing.Bitmap(imageStream)
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

let clipTakeAny(bmp:System.Drawing.Bitmap) =
    let r = new System.Drawing.Bitmap(672, 448)
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
    let bmp37 = new System.Drawing.Bitmap(imageStream)
    let imageStream = GetResourceStream("icons7x7.png")
    let bmp = new System.Drawing.Bitmap(imageStream)
    let newBmp = new System.Drawing.Bitmap(bmp.Width+7, bmp.Height)
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
    let bmp = new System.Drawing.Bitmap(imageStream)
    let newBmp = new System.Drawing.Bitmap(bmp.Width, bmp.Height)
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

let blockers_gsc = new GradientStopCollection([new GradientStop(Color.FromArgb(255uy, 0uy, 180uy, 0uy), 0.)
                                               new GradientStop(Color.FromArgb(255uy, 40uy, 40uy, 40uy), 0.3)
                                               new GradientStop(Color.FromArgb(255uy, 40uy, 40uy, 40uy), 0.7)
                                               new GradientStop(Color.FromArgb(255uy, 180uy, 0uy, 0uy), 1.0)
                                               ])
let blockers_maybeBG = new LinearGradientBrush(blockers_gsc, Point(0.,0.), Point(1.,1.))
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
                                -> blockers_maybeBG :> Brush
                            | _ -> Brushes.Black :> Brush
    let bmp = blockerHardCanonicalBMP(current)
    if bmp <> null then
        let image = BMPtoImage(bmp)
        image.IsHitTestVisible <- false
        canvasAdd(innerc, image, 1., 1.)
    innerc

let skipped = Brushes.MediumPurple
let placeSkippedItemXDecorationImpl(innerc:Canvas, size) =
    innerc.Children.Add(new Shapes.Line(Stroke=skipped, StrokeThickness=3., X1=0., Y1=0., X2=size, Y2=size, IsHitTestVisible=false)) |> ignore
    innerc.Children.Add(new Shapes.Line(Stroke=skipped, StrokeThickness=3., X1=size, Y1=0., X2=0., Y2=size, IsHitTestVisible=false)) |> ignore
let placeSkippedItemXDecoration(innerc) = placeSkippedItemXDecorationImpl(innerc, 30.)


let ExeName = "Z1R_WPF.exe"
let RestartTheApplication() = 
    let cwd = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName)
    let thisExe = System.IO.Path.Combine(cwd, ExeName)
    System.Diagnostics.Process.Start(thisExe) |> ignore
    System.Windows.Application.Current.MainWindow.Close()

// ideas from
// https://kent-boogaart.com/blog/dispatcher-frames
// from https://stackoverflow.com/questions/4502037/where-is-the-application-doevents-in-wpf
// see also
// https://stackoverflow.com/questions/21248643/how-can-i-convert-win32-mouse-messages-to-wpf-mouse-events 
let mutable sawMouseEvent = false
let mutable theTimer = null
let mutable theFrame = null : System.Windows.Threading.DispatcherFrame
let mutable theCounter = 0
let setup(w:Window, v) =
    if theTimer=null then
        dpi <- Some(VisualTreeHelper.GetDpi(v))
        theTimer <- new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render) // this only runs for a very short time when warping mouse; use high priority to avoid noticeable latency
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
        w.MouseMove.Add(fun _ -> sawMouseEvent <- true)
let SilentlyWarpMouseCursorTo(pos:Point) =
    if theFrame = null then  // Note: cannot process a second of these calls while one is active; I think that's fine
        theFrame <- new System.Windows.Threading.DispatcherFrame()
        sawMouseEvent <- false
        theCounter <- 0
        theTimer.Start()
        Win32.SetCursor(pos.X, pos.Y)    // this queues a mouse move input, and we want to block until that event is processed, to avoid spurious MouseLeave()s and other reasons
        System.Windows.Threading.Dispatcher.PushFrame(theFrame)   // creates a blocking message pump that only stops when Continue set to false
        theFrame <- null
let WarpMouseCursorTo(pos:Point) =
    SilentlyWarpMouseCursorTo(pos)
    PlaySoundForSpeechRecognizedAndUsedToMark()
let NavigationallyWarpMouseCursorTo(pos:Point) =   // can abstract over whether keyboard 'arrow' keys play the sound or not
    SilentlyWarpMouseCursorTo(pos)
    (*
    Win32.SetSonar(true) |> ignore
    do
        // Press and release Ctrl
        let VK_CONTROL = 0x11us
        let KEYEVENTF_KEYDOWN = 0x0u
        let KEYEVENTF_KEYUP = 0x2u
        let ipa = Array.create 1 (LPINPUT())
        ipa.[0].``type`` <- 1 // INPUT_KEYBOARD
        ipa.[0].u.ki <- KEYBDINPUT(VK_CONTROL, 0us, KEYEVENTF_KEYDOWN, 0u, UIntPtr(0u))
        Win32.DoSendInput(1, ipa, sizeof<LPINPUT>) |> ignore
        ipa.[0].u.ki <- KEYBDINPUT(VK_CONTROL, 0us, KEYEVENTF_KEYUP, 0u, UIntPtr(0u))
        Win32.DoSendInput(1, ipa, sizeof<LPINPUT>) |> ignore
    Win32.SetSonar(false) |> ignore
    *)

// https://learn.microsoft.com/en-us/archive/blogs/greg_schechter/more-details-on-how-effects-work
// https://learn.microsoft.com/en-us/archive/blogs/jgoldb/software-rendering-usage-in-wpf
let canUseEffectsWithoutDestroyingPerformance = 
    // (System.Windows.Media.RenderCapability.Tier >>> 16) >= 2 
    // System.Windows.Media.RenderCapability.IsPixelShaderVersionSupported(2s,0s)
    // none of the above worked in practice, so there is a flag in the settings file instead
    TrackerModelOptions.UseBlurEffects.Value
