namespace Z1RDrawing

// Cross-platform stand-in for System.Drawing.Bitmap, which only works on Windows in .NET.
// The ported files use Z1RDrawing.Bitmap wherever the WPF files use System.Drawing.Bitmap, with the same members
// (constructors, GetPixel/SetPixel, Width/Height, Clone, Save...), so the pixel code ports line for line.
// System.Drawing.Color itself is cross-platform and is used unchanged.
//
// Pixels are stored as non-premultiplied 32-bit ARGB, like GDI+'s Format32bppArgb: a new bitmap is all 0x00000000
// (transparent black) and SetPixel/GetPixel round-trip exact colors, including alpha.

open System
open SkiaSharp

module internal SkiaHelpers =
    let unpremulInfo(w, h) = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Unpremul)
    let pixelsOf(skb:SKBitmap) = skb.Pixels |> Array.map (fun (c:SKColor) -> uint32 c)
    let decode(s:IO.Stream) =
        use ms = new IO.MemoryStream()
        s.CopyTo(ms)
        ms.Position <- 0L
        use codec = SKCodec.Create(ms)
        if isNull codec then failwith "could not decode image"
        // decode straight into unpremultiplied BGRA so each pixel reads back as the exact ARGB stored in the file
        use skb = SKBitmap.Decode(codec, unpremulInfo(codec.Info.Width, codec.Info.Height))
        if isNull skb then failwith "could not decode image"
        skb.Width, skb.Height, pixelsOf skb

[<AllowNullLiteral>]
type Bitmap(width:int, height:int, pixels:uint32[]) =
    static let ofSize(w:int, h:int) = (w, h, Array.zeroCreate<uint32> (w*h))
    static let resized(src:Bitmap, w:int, h:int) =
        use skb : SKBitmap = src.ToSKBitmap()
        use dst = skb.Resize(SkiaHelpers.unpremulInfo(w, h), SKFilterQuality.Low)   // GDI+'s default for a resizing copy is bilinear
        (w, h, SkiaHelpers.pixelsOf dst)
    /// like `new System.Drawing.Bitmap(w, h)`
    new(w:int, h:int) = let (w, h, px) = ofSize(w, h) in Bitmap(w, h, px)
    /// like `new System.Drawing.Bitmap(stream)`
    new(s:IO.Stream) = let (w, h, px) = SkiaHelpers.decode s in Bitmap(w, h, px)
    /// like `new System.Drawing.Bitmap(filename)`
    new(filename:string) = let (w, h, px) = (use s = IO.File.OpenRead(filename) in SkiaHelpers.decode s) in Bitmap(w, h, px)
    /// like `new System.Drawing.Bitmap(src)`
    new(src:Bitmap) = Bitmap(src.Width, src.Height, Array.copy src.Pixels)
    /// like `new System.Drawing.Bitmap(image, size)`: a resized copy
    new(src:Bitmap, size:Drawing.Size) = let (w, h, px) = resized(src, size.Width, size.Height) in Bitmap(w, h, px)
    member _.Width = width
    member _.Height = height
    member _.Pixels = pixels   // ARGB, row-major
    member _.GetPixel(x:int, y:int) =
        if x < 0 || y < 0 || x >= width || y >= height then raise(ArgumentOutOfRangeException("x/y"))
        Drawing.Color.FromArgb(int pixels.[y*width + x])
    member _.SetPixel(x:int, y:int, c:Drawing.Color) =
        if x < 0 || y < 0 || x >= width || y >= height then raise(ArgumentOutOfRangeException("x/y"))
        pixels.[y*width + x] <- uint32 (c.ToArgb())
    /// returns obj, like System.Drawing's Image.Clone(), so the WPF code's `:?> Bitmap` casts port unchanged
    member _.Clone() = box(new Bitmap(width, height, Array.copy pixels))
    /// GDI+ keeps a dpi per bitmap; bitmaps here are always shown at 96 dpi (1 pixel = 1 unit), so this does nothing
    member _.SetResolution(_x:float32, _y:float32) = ()
    member _.ToSKBitmap() : SKBitmap =
        let skb = new SKBitmap(SkiaHelpers.unpremulInfo(width, height))
        skb.Pixels <- pixels |> Array.map (fun p -> SKColor(p))
        skb
    /// always writes PNG. (System.Drawing's Save(filename) also writes PNG for bitmaps created in memory, whatever the file extension.)
    member this.Save(filename:string) =
        use s = IO.File.Create(filename)
        this.Save(s)
    member this.Save(s:IO.Stream) =
        use skb = this.ToSKBitmap()
        use data = skb.Encode(SKEncodedImageFormat.Png, 100)
        data.SaveTo(s)
    /// like `System.Drawing.Bitmap.FromFile(filename)`; returns obj, as System.Drawing's returns Image
    static member FromFile(filename:string) = box(new Bitmap(filename))

/// Stand-in for System.Drawing.Icon (only used for the message box icons)
[<AllowNullLiteral>]
type Icon(bmp:Bitmap) =
    member _.ToBitmap() = new Bitmap(bmp)

/// Stand-in for System.Drawing.SystemIcons. Windows' own icons can't be shipped on other systems, so these are drawn
/// to look like them: 32x32, a blue circle with '?' or 'i', a yellow triangle with '!', a red circle with an 'X'.
module SystemIcons =
    let private draw(shapeColor:SKColor, glyph:string, glyphColor:SKColor, triangle:bool) =
        let info = new SKImageInfo(32, 32, SKColorType.Bgra8888, SKAlphaType.Premul)
        use surface = SKSurface.Create(info)
        let canvas = surface.Canvas
        canvas.Clear(SKColors.Transparent)
        use fill = new SKPaint(Color=shapeColor, IsAntialias=true, Style=SKPaintStyle.Fill)
        if triangle then
            use path = new SKPath()
            path.MoveTo(16.f, 2.f); path.LineTo(30.f, 29.f); path.LineTo(2.f, 29.f); path.Close()
            canvas.DrawPath(path, fill)
        else
            canvas.DrawCircle(16.f, 16.f, 14.f, fill)
        use text = new SKPaint(Color=glyphColor, IsAntialias=true, TextSize=20.f, TextAlign=SKTextAlign.Center,
                               Typeface=SKTypeface.FromFamilyName(null, SKFontStyle.Bold))
        canvas.DrawText(glyph, 16.f, (if triangle then 26.f else 23.f), text)
        use img = surface.Snapshot()
        use skb = SKBitmap.FromImage(img)
        let b = new Bitmap(32, 32)
        for y = 0 to 31 do
            for x = 0 to 31 do
                let c = skb.GetPixel(x, y)   // (GetPixel returns unpremultiplied colors)
                b.SetPixel(x, y, Drawing.Color.FromArgb(int c.Alpha, int c.Red, int c.Green, int c.Blue))
        new Icon(b)
    let Question = draw(SKColor(0x1Fuy, 0x5Fuy, 0xBFuy), "?", SKColors.White, false)
    let Information = draw(SKColor(0x1Fuy, 0x5Fuy, 0xBFuy), "i", SKColors.White, false)
    let Warning = draw(SKColor(0xFFuy, 0xC8uy, 0x1Euy), "!", SKColors.Black, true)
    let Error = draw(SKColor(0xD8uy, 0x2Auy, 0x2Auy), "X", SKColors.White, false)
