// Cross-platform stand-ins for System.Drawing.Common types, on SkiaSharp. Only the members the trackers use exist.
// Pixels are non-premultiplied 32-bit ARGB, like GDI+'s Format32bppArgb: a new bitmap is all 0x00000000 (transparent black),
// and SetPixel/GetPixel round-trip exact colors, including alpha. System.Drawing.Color/Point/Rectangle/Size are the real
// (cross-platform) types from System.Drawing.Primitives.
using System;
using System.IO;
using SkiaSharp;

namespace System.Drawing
{
    public abstract class Image : IDisposable, ICloneable
    {
        public abstract int Width { get; }
        public abstract int Height { get; }
        public abstract object Clone();
        public virtual void Dispose() { }
        public static Image FromFile(string filename) => new Bitmap(filename);
    }

    public sealed class Bitmap : Image
    {
        readonly int width, height;
        readonly uint[] pixels;   // ARGB, row-major

        Bitmap(int w, int h, uint[] px) { width = w; height = h; pixels = px; }
        public Bitmap(int width, int height) : this(width, height, new uint[width * height]) { }
        public Bitmap(int width, int height, Imaging.PixelFormat format) : this(width, height) { }
        public Bitmap(Stream stream) : this(Decode(stream)) { }
        public Bitmap(string filename) : this(DecodeFile(filename)) { }
        public Bitmap(Image original) : this(((Bitmap)original).width, ((Bitmap)original).height, (uint[])((Bitmap)original).pixels.Clone()) { }
        /// <summary>a resized copy; GDI+'s default for this is bilinear filtering</summary>
        public Bitmap(Image original, Size newSize) : this(Resize((Bitmap)original, newSize.Width, newSize.Height)) { }
        public Bitmap(Image original, int width, int height) : this(original, new Size(width, height)) { }
        Bitmap(Bitmap b) : this(b.width, b.height, b.pixels) { }

        public override int Width => width;
        public override int Height => height;
        public Size Size => new Size(width, height);
        public Imaging.PixelFormat PixelFormat => Imaging.PixelFormat.Format32bppArgb;
        /// <summary>(not in System.Drawing) the pixels, ARGB, row-major; used to hand bitmaps to Avalonia</summary>
        public uint[] Pixels => pixels;

        public Color GetPixel(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) throw new ArgumentOutOfRangeException(nameof(x));
            return Color.FromArgb((int)pixels[y * width + x]);
        }
        public void SetPixel(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) throw new ArgumentOutOfRangeException(nameof(x));
            pixels[y * width + x] = (uint)c.ToArgb();
        }
        public override object Clone() => new Bitmap(width, height, (uint[])pixels.Clone());
        /// <summary>GDI+ keeps a dpi per bitmap; bitmaps here are always shown at 96 dpi, so this does nothing</summary>
        public void SetResolution(float xDpi, float yDpi) { }
        public void MakeTransparent(Color transparentColor)
        {
            uint key = (uint)transparentColor.ToArgb();
            for (int i = 0; i < pixels.Length; i++) if (pixels[i] == key) pixels[i] = 0;
        }

        /// <summary>always writes PNG, whatever the extension. (System.Drawing does the same for bitmaps created in memory.)</summary>
        public void Save(string filename) { using (var s = File.Create(filename)) Save(s); }
        public void Save(string filename, Imaging.ImageFormat format) { using (var s = File.Create(filename)) Save(s, format); }
        public void Save(Stream stream) => Save(stream, Imaging.ImageFormat.Png);
        public void Save(Stream stream, Imaging.ImageFormat format)
        {
            // (only PNG is written; SkiaSharp can't encode BMP. Everything that reads these files decodes PNG fine.)
            using (var skb = ToSKBitmap())
            using (var data = skb.Encode(SKEncodedImageFormat.Png, 100))
                data.SaveTo(stream);
        }

        // ---- SkiaSharp interop (not in System.Drawing)
        static SKImageInfo UnpremulInfo(int w, int h) => new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        static uint[] PixelsOf(SKBitmap skb) { var cs = skb.Pixels; var px = new uint[cs.Length]; for (int i = 0; i < cs.Length; i++) px[i] = (uint)cs[i]; return px; }
        static Bitmap Decode(Stream s)
        {
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                ms.Position = 0;
                using (var codec = SKCodec.Create(ms))
                {
                    if (codec == null) throw new ArgumentException("could not decode image");
                    // decode straight into unpremultiplied BGRA so each pixel reads back exactly as stored in the file
                    using (var skb = SKBitmap.Decode(codec, UnpremulInfo(codec.Info.Width, codec.Info.Height)))
                    {
                        if (skb == null) throw new ArgumentException("could not decode image");
                        return new Bitmap(skb.Width, skb.Height, PixelsOf(skb));
                    }
                }
            }
        }
        static Bitmap DecodeFile(string filename) { using (var s = File.OpenRead(filename)) return Decode(s); }
        static Bitmap Resize(Bitmap src, int w, int h)
        {
            using (var skb = src.ToSKBitmap())
            using (var dst = skb.Resize(UnpremulInfo(w, h), SKFilterQuality.Low))
                return new Bitmap(w, h, PixelsOf(dst));
        }
        public SKBitmap ToSKBitmap()
        {
            var skb = new SKBitmap(UnpremulInfo(width, height));
            var cs = new SKColor[pixels.Length];
            for (int i = 0; i < cs.Length; i++) cs[i] = new SKColor(pixels[i]);
            skb.Pixels = cs;
            return skb;
        }
        internal void CopyFrom(SKBitmap skb)
        {
            var cs = skb.Pixels;   // (unpremultiplied colors, whatever the bitmap's alpha type)
            for (int i = 0; i < cs.Length; i++) pixels[i] = (uint)cs[i];
        }
    }

    public abstract class Brush : IDisposable { public abstract Color Color { get; } public void Dispose() { } }
    public sealed class SolidBrush : Brush { readonly Color c; public SolidBrush(Color color) { c = color; } public override Color Color => c; }
    public static class Brushes
    {
        public static Brush Black => new SolidBrush(Color.Black);
        public static Brush White => new SolidBrush(Color.White);
        public static Brush Red => new SolidBrush(Color.Red);
        public static Brush Transparent => new SolidBrush(Color.Transparent);
    }
    public sealed class Pen : IDisposable
    {
        public Pen(Color color, float width) { Color = color; Width = width; }
        public Pen(Color color) : this(color, 1f) { }
        public Color Color { get; set; }
        public float Width { get; set; }
        public void Dispose() { }
    }

    /// <summary>Drawing on a Bitmap. Each call draws through SkiaSharp and writes the result straight back into the bitmap.</summary>
    public sealed class Graphics : IDisposable
    {
        readonly Bitmap target;
        Graphics(Bitmap b) { target = b; }
        public static Graphics FromImage(Image image) => new Graphics((Bitmap)image);
        public Drawing2D.InterpolationMode InterpolationMode { get; set; } = Drawing2D.InterpolationMode.Bilinear;
        public Drawing2D.SmoothingMode SmoothingMode { get; set; } = Drawing2D.SmoothingMode.None;
        bool AntiAlias => SmoothingMode == Drawing2D.SmoothingMode.AntiAlias || SmoothingMode == Drawing2D.SmoothingMode.HighQuality;
        static SKColor Sk(Color c) => new SKColor((uint)c.ToArgb());

        void Draw(Action<SKCanvas> f)
        {
            // SkiaSharp canvases need premultiplied pixels: draw on a premultiplied copy, then copy back
            using (var src = target.ToSKBitmap())
            using (var work = new SKBitmap(new SKImageInfo(target.Width, target.Height, SKColorType.Bgra8888, SKAlphaType.Premul)))
            {
                src.CopyTo(work, SKColorType.Bgra8888);
                using (var canvas = new SKCanvas(work)) { f(canvas); canvas.Flush(); }
                target.CopyFrom(work);
            }
        }
        public void Clear(Color color) => Draw(c => c.Clear(Sk(color)));
        public void DrawImage(Image image, int x, int y, int width, int height)
        {
            var quality = InterpolationMode == Drawing2D.InterpolationMode.NearestNeighbor ? SKFilterQuality.None
                        : InterpolationMode == Drawing2D.InterpolationMode.HighQualityBicubic || InterpolationMode == Drawing2D.InterpolationMode.HighQualityBilinear || InterpolationMode == Drawing2D.InterpolationMode.High ? SKFilterQuality.High
                        : SKFilterQuality.Low;
            using (var img = ((Bitmap)image).ToSKBitmap())
                Draw(c => { using (var p = new SKPaint { FilterQuality = quality, IsAntialias = true }) c.DrawBitmap(img, new SKRect(x, y, x + width, y + height), p); });
        }
        public void DrawImage(Image image, int x, int y) => DrawImage(image, x, y, image.Width, image.Height);
        public void FillRectangle(Brush brush, Rectangle r) => Draw(c => { using (var p = new SKPaint { Color = Sk(brush.Color), Style = SKPaintStyle.Fill }) c.DrawRect(r.X, r.Y, r.Width, r.Height, p); });
        public void FillRectangle(Brush brush, int x, int y, int w, int h) => FillRectangle(brush, new Rectangle(x, y, w, h));
        public void DrawRectangle(Pen pen, Rectangle r) => Draw(c => { using (var p = new SKPaint { Color = Sk(pen.Color), Style = SKPaintStyle.Stroke, StrokeWidth = pen.Width, IsAntialias = AntiAlias }) c.DrawRect(r.X, r.Y, r.Width, r.Height, p); });
        public void DrawRectangle(Pen pen, int x, int y, int w, int h) => DrawRectangle(pen, new Rectangle(x, y, w, h));
        public void FillEllipse(Brush brush, Rectangle r) => Draw(c => { using (var p = new SKPaint { Color = Sk(brush.Color), Style = SKPaintStyle.Fill, IsAntialias = AntiAlias }) c.DrawOval(new SKRect(r.X, r.Y, r.Right, r.Bottom), p); });
        public void DrawLine(Pen pen, int x1, int y1, int x2, int y2) => Draw(c => { using (var p = new SKPaint { Color = Sk(pen.Color), Style = SKPaintStyle.Stroke, StrokeWidth = pen.Width, IsAntialias = AntiAlias }) c.DrawLine(x1, y1, x2, y2, p); });
        public void DrawLine(Pen pen, float x1, float y1, float x2, float y2) => Draw(c => { using (var p = new SKPaint { Color = Sk(pen.Color), Style = SKPaintStyle.Stroke, StrokeWidth = pen.Width, IsAntialias = AntiAlias }) c.DrawLine(x1, y1, x2, y2, p); });
        public void Dispose() { }
    }

    /// <summary>Only used for the message box icons.</summary>
    public sealed class Icon : IDisposable
    {
        readonly Bitmap bmp;
        public Icon(Bitmap b) { bmp = b; }
        public Bitmap ToBitmap() => new Bitmap(bmp);
        public int Width => bmp.Width;
        public int Height => bmp.Height;
        public void Dispose() { }
    }

    /// <summary>Windows' own icons can't ship on other systems, so these are drawn to look like them:
    /// 32x32, a blue circle with '?' or 'i', a yellow triangle with '!', a red circle with an 'X'.</summary>
    public static class SystemIcons
    {
        static Icon Make(SKColor shape, string glyph, SKColor glyphColor, bool triangle)
        {
            var b = new Bitmap(32, 32);
            using (var surface = SKSurface.Create(new SKImageInfo(32, 32, SKColorType.Bgra8888, SKAlphaType.Premul)))
            {
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);
                using (var fill = new SKPaint { Color = shape, IsAntialias = true, Style = SKPaintStyle.Fill })
                {
                    if (triangle)
                    {
                        using (var path = new SKPath()) { path.MoveTo(16, 2); path.LineTo(30, 29); path.LineTo(2, 29); path.Close(); canvas.DrawPath(path, fill); }
                    }
                    else canvas.DrawCircle(16, 16, 14, fill);
                }
                using (var text = new SKPaint { Color = glyphColor, IsAntialias = true, TextSize = 20, TextAlign = SKTextAlign.Center, Typeface = SKTypeface.FromFamilyName(null, SKFontStyle.Bold) })
                    canvas.DrawText(glyph, 16, triangle ? 26 : 23, text);
                using (var img = surface.Snapshot())
                using (var skb = SKBitmap.FromImage(img))
                    b.CopyFrom(skb);
            }
            return new Icon(b);
        }
        public static readonly Icon Question = Make(new SKColor(0x1F, 0x5F, 0xBF), "?", SKColors.White, false);
        public static readonly Icon Information = Make(new SKColor(0x1F, 0x5F, 0xBF), "i", SKColors.White, false);
        public static readonly Icon Asterisk = Information;
        public static readonly Icon Warning = Make(new SKColor(0xFF, 0xC8, 0x1E), "!", SKColors.Black, true);
        public static readonly Icon Exclamation = Warning;
        public static readonly Icon Error = Make(new SKColor(0xD8, 0x2A, 0x2A), "X", SKColors.White, false);
        public static readonly Icon Hand = Error;
    }
}

namespace System.Drawing.Imaging
{
    public enum PixelFormat { Format32bppArgb = 2498570, Format32bppPArgb = 925707, Format24bppRgb = 137224 }
    public sealed class ImageFormat
    {
        readonly string name;
        ImageFormat(string n) { name = n; }
        public static ImageFormat Png { get; } = new ImageFormat("Png");
        public static ImageFormat Bmp { get; } = new ImageFormat("Bmp");
        public static ImageFormat Jpeg { get; } = new ImageFormat("Jpeg");
        public override string ToString() => name;
    }
}

namespace System.Drawing.Drawing2D
{
    public enum InterpolationMode { Invalid = -1, Default = 0, Low = 1, High = 2, Bilinear = 3, Bicubic = 4, NearestNeighbor = 5, HighQualityBilinear = 6, HighQualityBicubic = 7 }
    public enum SmoothingMode { Invalid = -1, Default = 0, HighSpeed = 1, HighQuality = 2, None = 3, AntiAlias = 4 }
}
