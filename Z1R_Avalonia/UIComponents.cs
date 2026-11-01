// Ported from Z1R_WPF/UIComponents.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// The F# `open`s of OverworldItemGridUI, DungeonUI.AhhGlobalVariables and CustomComboBoxes.GlobalFlag are `using static`s, so `canvasAdd`,
// `OMTW`, `hideLocator()` and `popupIsActive` read as in WPF. The later `open OverworldMapTileCustomization` is a `using` alias for
// OW_ITEM_GRID_LOCATIONS (a `using static` would make its canvasAdd/OMTW clash with OverworldItemGridUI's; they're the same values). The F# events passed
// in (doUIUpdateEvent: `Event<unit>`, levelTabSelected: `Event<int>`, blockersHoverEvent: `Event<bool>`) are Graphics.SharedEvents. F# model
// events (TrackerModel's IEvent<_>s) keep their F# type; `e.Add(f)` becomes `onEvent(e, f)`. Every `async { } |> Async.StartImmediate` is a
// `CustomComboBoxes.StartImmediate(...)` of a local async function. C# `for` loops share one variable across iterations, so loop indexes that
// closures capture are copied into a local first (F# gives each iteration its own).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.FSharp.Control;
using static Z1R_Avalonia.OverworldItemGridUI;
using static Z1R_Avalonia.DungeonUI.AhhGlobalVariables;
using static Z1R_Avalonia.CustomComboBoxes.GlobalFlag;
using OW_ITEM_GRID_LOCATIONS = Z1R_Avalonia.OverworldMapTileCustomization.OW_ITEM_GRID_LOCATIONS;   // (the F# `open OverworldMapTileCustomization` near the end; only this is used from it)

namespace Z1R_Avalonia;

public static class UIComponents
{
    // (port helper: WPF's `modelEvent.Add(f)` on an F# IEvent of the model)
    private static void onEvent<T>(IEvent<FSharpHandler<T>, T> e, Action<T> f) => e.AddHandler(new FSharpHandler<T>((_, x) => f(x)));
    // (WPF's ToolTipService.SetShowDuration has no Avalonia equivalent; a no-op, as in OptionsMenu)
    private static void setToolTipShowDuration(Control c, int ms) { }
    // (port helper: WPF's `new PointCollection(points)`)
    private static Points pts(IEnumerable<(double, double)> xys) => new Points(xys.Select(p => new Point(p.Item1, p.Item2)));

    public static readonly string UNICODE_UP = "\U0001F845";
    public static readonly string UNICODE_LEFT = "\U0001F844";
    public static readonly string UNICODE_DOWN = "\U0001F847";
    public static readonly string UNICODE_RIGHT = "\U0001F846";
    public static readonly SolidColorBrush arrowColor = Graphics.freeze(new SolidColorBrush(Color.FromArgb(255, 0, 180, 250)));
    public static readonly SolidColorBrush bgColor = Graphics.freeze(new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)));

    // (returns onMouseForMagnifier (F#: a function of an (i, j) tuple), dungeonTabsOverlay, dungeonTabsOverlayContent)
    public static (Action<int, int>, Border, Canvas) MakeMagnifier(List<Control> mirrorOverworldFEs, int owMapNum, System.Drawing.Bitmap[,] owMapBMPs)
    {
        // nearby ow tiles magnified overlay
        var ENLARGE = 8.0;
        var POP = 1;  // width of entrance border
        var BT = 2.0;  // border thickness of the interior 3x3 grid of tiles
        var dungeonTabsOverlay = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(5.0), Background = Brushes.Black, Opacity = 0.0, IsHitTestVisible = false,
                                        Width = 3.0 + 39.0 * 8.0 + 12.0 * 7.0 + 3.0 + 42.0 + 6.0, Height = (double)(2 * DungeonUI.TH + 3 + 27 * 8 + 12 * 7 + 3 + 6 - DungeonUI.TH + 2) };  // exactly covers the dungeon tab area beneath it
        var (DTOCW, DTOCH) = (3.0 * 16.0 * ENLARGE + 4.0 * BT, 3.0 * 11.0 * ENLARGE + 4.0 * BT);
        var dungeonTabsOverlayContent = new Canvas { Width = DTOCW, Height = DTOCH };
        if (owMapNum == 4)  // disable magnifier on blank/custom map
        {
            void onMouseForMagnifierNoop(int _i, int _j) { }
            return (onMouseForMagnifierNoop, dungeonTabsOverlay, dungeonTabsOverlayContent);
        }
        mirrorOverworldFEs.Add(dungeonTabsOverlayContent);
        var dtocPlusLegend = new StackPanel { Orientation = Orientation.Vertical };
        dtocPlusLegend.Children.Add(dungeonTabsOverlayContent);
        var dtocLegend = new StackPanel { Orientation = Orientation.Horizontal, Background = Graphics.almostBlack };
        foreach (var (outer_, inner_, desc) in new (IBrush, IBrush, string)[] {
                                 (Brushes.Cyan, Brushes.Black, "open cave"),
                                 (Brushes.Black, Brushes.Cyan, "bomb spot"),
                                 (Brushes.Black, Brushes.Red, "burn spot"),
                                 (Brushes.Black, Brushes.Yellow, "recorder spot"),
                                 (Brushes.Black, Brushes.Magenta, "pushable spot") })
        {
            var black = new Canvas { Width = ENLARGE + 2.0 * ((double)POP + 1.0), Height = ENLARGE + 2.0 * ((double)POP + 1.0), Background = Brushes.Black };
            var outer = new Canvas { Width = ENLARGE + 2.0 * ((double)POP), Height = ENLARGE + 2.0 * ((double)POP), Background = outer_ };
            var inner = new Canvas { Width = ENLARGE, Height = ENLARGE, Background = inner_ };
            canvasAdd(black, outer, 1.0, 1.0);
            canvasAdd(black, inner, 1.0 + (double)POP, 1.0 + (double)POP);
            dtocLegend.Children.Add(black);
            var text = new TextBox { Text = desc, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0),
                                        FontSize = 12.0, HorizontalContentAlignment = HorizontalAlignment.Center };
            dtocLegend.Children.Add(text);
        }
        dtocPlusLegend.Children.Add(dtocLegend);
        // (WPF: dtocPlusLegend.LayoutTransform; Avalonia has no LayoutTransform property, so it sits in a LayoutTransformControl, which is the border's child)
        var dtocPlusLegendScaled = new LayoutTransformControl { LayoutTransform = new ScaleTransform(1.02, 1.02), Child = dtocPlusLegend };   // grow the magnified map slightly, to halve the empty black margin around it
        RenderOptions.SetBitmapInterpolationMode(dtocPlusLegendScaled, BitmapInterpolationMode.None);
        dtocPlusLegendScaled.HorizontalAlignment = HorizontalAlignment.Center;
        dtocPlusLegendScaled.VerticalAlignment = VerticalAlignment.Center;
        dungeonTabsOverlay.Child = dtocPlusLegendScaled;
        var overlayTiles = new Image[16, 8];
        for (int i = 0; i <= 15; i++)
        {
            for (int j = 0; j <= 7; j++)
            {
                System.Drawing.Bitmap make_bmp()
                {
                    // (WPF: <exe folder>\Magnifier\...; moved to Platform.Files.User like the Overworld tile cache, see Graphics.overworldMapBMPs. sprintf "%2d" pads with a space.)
                    var magnifierFilename = Platform.Files.User("Magnifier", $"quest.{owMapNum}.ow.{i,2}.{j,2}.bmp");
                    return Graphics.readCacheFileOrCreateBmp(magnifierFilename, () =>
                    {
                        var bmp = new System.Drawing.Bitmap(16 * (int)ENLARGE, 11 * (int)ENLARGE);
                        for (int x = 0; x <= 15; x++)
                        {
                            for (int y = 0; y <= 10; y++)
                            {
                                var c0 = owMapBMPs[i, j].GetPixel(x * 3, y * 3);
                                for (int px = 0; px <= (int)ENLARGE - 1; px++)
                                {
                                    for (int py = 0; py <= (int)ENLARGE - 1; py++)
                                    {
                                        // diagonal rocks
                                        System.Drawing.Color make_c1()
                                        {
                                            // The diagonal rock data is based on the first quest map. A few screens are different in 2nd/mixed quest.
                                            // So we apply a kludge to load the correct diagonal data.
                                            var (i2, j2) =
                                                (owMapNum == 1 && i == 4 && j == 7) ? // second quest has a cave like 14,5 here
                                                    (14, 5)
                                                : (owMapNum == 1 && i == 11 && j == 0) ? // second quest has fairy here, borrow 2,4
                                                    (2, 4)
                                                : (owMapNum != 0 && i == 12 && j == 3) ? // non-first quest has a whistle lake here, borrow 2,4
                                                    (2, 4)
                                                :
                                                    (i, j);
                                            if (OverworldData.owNEupperRock[i2, j2][x, y])
                                            {
                                                if (px + py > (int)ENLARGE - 1)
                                                    return owMapBMPs[i2, j2].GetPixel(x * 3, (y + 1) * 3);
                                                else
                                                    return c0;
                                            }
                                            else if (OverworldData.owSEupperRock[i2, j2][x, y])
                                            {
                                                if (px < py)
                                                    return owMapBMPs[i2, j2].GetPixel(x * 3, (y + 1) * 3);
                                                else
                                                    return c0;
                                            }
                                            else if (OverworldData.owNElowerRock[i2, j2][x, y])
                                            {
                                                if (px + py < (int)ENLARGE - 1)
                                                    return owMapBMPs[i2, j2].GetPixel(x * 3, (y - 1) * 3);
                                                else
                                                    return c0;
                                            }
                                            else if (OverworldData.owSElowerRock[i2, j2][x, y])
                                            {
                                                if (px > py)
                                                    return owMapBMPs[i2, j2].GetPixel(x * 3, (y - 1) * 3);
                                                else
                                                    return c0;
                                            }
                                            else
                                                return c0;
                                        }
                                        var c1 = make_c1();
                                        // edges of squares
                                        var c2 =
                                            ((px + 1) % (int)ENLARGE == 0 || (py + 1) % (int)ENLARGE == 0) ?
                                                System.Drawing.Color.FromArgb((int)c1.R / 2, (int)c1.G / 2, (int)c1.B / 2)
                                            :
                                                c1;
                                        bmp.SetPixel(x * (int)ENLARGE + px, y * (int)ENLARGE + py, c2);
                                    }
                                }
                            }
                        }
                        // make the entrances 'pop'
                        // No 'entrance pixels' are on the edge of a tile, and we would be drawing outside bitmap array bounds if they were, so only iterate over interior pixels:
                        for (int x = 1; x <= 14; x++)
                        {
                            for (int y = 1; y <= 9; y++)
                            {
                                var c = owMapBMPs[i, j].GetPixel(x * 3, y * 3);
                                System.Drawing.Color? border;
                                if (c.ToArgb() == System.Drawing.Color.Black.ToArgb())    // black open cave
                                {
                                    var c2 = owMapBMPs[i, j].GetPixel((x - 1) * 3, y * 3);
                                    if (c2.ToArgb() == System.Drawing.Color.Black.ToArgb())    // also black to the left, this is vanilla 6 two-wide entrance, only show one
                                        border = null;
                                    else
                                        border = System.Drawing.Color.FromArgb(0xFF, 0x00, 0xCC, 0xCC);
                                }
                                else if (c.ToArgb() == System.Drawing.Color.FromArgb(0xFF, 0x00, 0xFF, 0xFF).ToArgb())  // cyan bomb spot
                                    border = System.Drawing.Color.FromArgb(0xFF, 0x00, 0x00, 0x00);
                                else if (c.ToArgb() == System.Drawing.Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00).ToArgb())  // yellow recorder spot
                                    border = System.Drawing.Color.FromArgb(0xFF, 0x00, 0x00, 0x00);
                                else if (c.ToArgb() == System.Drawing.Color.FromArgb(0xFF, 0xFF, 0x00, 0x00).ToArgb())  // red burn spot
                                    border = System.Drawing.Color.FromArgb(0xFF, 0x00, 0x00, 0x00);
                                else if (c.ToArgb() == System.Drawing.Color.FromArgb(0xFF, 0xFF, 0x00, 0xFF).ToArgb())  // magenta pushblock spot
                                    border = System.Drawing.Color.FromArgb(0xFF, 0x00, 0x00, 0x00);
                                else
                                    border = null;
                                if (border is System.Drawing.Color bc)
                                {
                                    // thin black outline
                                    for (int px = x * (int)ENLARGE - POP - 1; px <= (x + 1) * (int)ENLARGE - 1 + POP + 1; px++)
                                        for (int py = y * (int)ENLARGE - POP - 1; py <= (y + 1) * (int)ENLARGE - 1 + POP + 1; py++)
                                            bmp.SetPixel(px, py, System.Drawing.Color.Black);
                                    // border color
                                    for (int px = x * (int)ENLARGE - POP; px <= (x + 1) * (int)ENLARGE - 1 + POP; px++)
                                        for (int py = y * (int)ENLARGE - POP; py <= (y + 1) * (int)ENLARGE - 1 + POP; py++)
                                            bmp.SetPixel(px, py, bc);
                                    // inner actual pixel
                                    for (int px = x * (int)ENLARGE; px <= (x + 1) * (int)ENLARGE - 1; px++)
                                        for (int py = y * (int)ENLARGE; py <= (y + 1) * (int)ENLARGE - 1; py++)
                                            bmp.SetPixel(px, py, c);
                                }
                            }
                        }
                        return bmp;
                    });
                }
                var bmp_ = make_bmp();
                overlayTiles[i, j] = Graphics.BMPtoImage(bmp_);
            }
        }
        TextBox makeArrow(string text)
        {
            var tb = new TextBox { Text = text, FontSize = 20.0, Foreground = arrowColor, Background = bgColor, IsReadOnly = true, BorderThickness = new Thickness(0.0) };
            tb.Clip = new RectangleGeometry(new Rect(0.0, 6.0, 30.0, 18.0));
            return tb;
        }
        void onMouseForMagnifier(int i, int j)
        {
            // show enlarged version of current & nearby rooms
            dungeonTabsOverlayContent.Children.Clear();
            // fill whole canvas black, so elements behind don't show through
            canvasAdd(dungeonTabsOverlayContent, new Rectangle { Width = dungeonTabsOverlayContent.Width, Height = dungeonTabsOverlayContent.Height, Fill = Brushes.Black }, 0.0, 0.0);
            var xmin = Math.Min(Math.Max(i - 1, 0), 13);
            var ymin = Math.Min(Math.Max(j - 1, 0), 5);
            // draw a white highlight rectangle behind the tile where mouse is
            var rect = new Rectangle { Width = 16.0 * ENLARGE + 2.0 * BT, Height = 11.0 * ENLARGE + 2.0 * BT, Fill = Brushes.White };
            canvasAdd(dungeonTabsOverlayContent, rect, (double)(i - xmin) * (16.0 * ENLARGE + BT), (double)(j - ymin) * (11.0 * ENLARGE + BT));
            // draw the 3x3 tiles
            for (int x = 0; x <= 2; x++)
            {
                for (int y = 0; y <= 2; y++)
                {
                    var dx = BT + (double)x * (16.0 * ENLARGE + BT);
                    var dy = BT + (double)y * (11.0 * ENLARGE + BT);
                    canvasAdd(dungeonTabsOverlayContent, overlayTiles[xmin + x, ymin + y], dx, dy);
                    if (xmin + x == 1 && ymin + y == 6) // Lost Woods
                    {
                        canvasAdd(dungeonTabsOverlayContent, makeArrow(UNICODE_UP),   dx + 3.0, dy - 2.0);
                        canvasAdd(dungeonTabsOverlayContent, makeArrow(UNICODE_LEFT), dx + 3.0, dy + 18.0);
                        canvasAdd(dungeonTabsOverlayContent, makeArrow(UNICODE_DOWN), dx + 3.0, dy + 38.0);
                        canvasAdd(dungeonTabsOverlayContent, makeArrow(UNICODE_LEFT), dx + 3.0, dy + 58.0);
                        canvasAdd(dungeonTabsOverlayContent, makeArrow(UNICODE_RIGHT), dx + 101.0, dy + 28.0);
                    }
                    if (xmin + x == 11 && ymin + y == 1) // Lost Hills
                    {
                        canvasAdd(dungeonTabsOverlayContent, makeArrow(UNICODE_UP),   dx + 20.0, dy - 2.0);
                        canvasAdd(dungeonTabsOverlayContent, makeArrow(UNICODE_UP),   dx + 40.0, dy - 2.0);
                        canvasAdd(dungeonTabsOverlayContent, makeArrow(UNICODE_UP),   dx + 60.0, dy - 2.0);
                        canvasAdd(dungeonTabsOverlayContent, makeArrow(UNICODE_UP),   dx + 80.0, dy - 2.0);
                        canvasAdd(dungeonTabsOverlayContent, makeArrow(UNICODE_LEFT), dx + 3.0,  dy + 28.0);
                    }
                }
            }
            if (TrackerModelOptions.Overworld.ShowMagnifier.Value)
                dungeonTabsOverlay.Opacity = 1.0;
        }

        return (onMouseForMagnifier, dungeonTabsOverlay, dungeonTabsOverlayContent);
    }

    public static readonly SolidColorBrush recorderEllipseNewDungeonsColor = Graphics.freeze(new SolidColorBrush(Color.FromRgb(220, 220, 220)));
    public static readonly IBrush recorderEllipseVanillaColor = Brushes.White;
    public static IBrush RecorderEllipseColor()
    {
        if (TrackerModel.recorderToNewDungeons)
            return recorderEllipseNewDungeonsColor;
        else
            return recorderEllipseVanillaColor;
    }
    public static double legendStartX = 0.0;   // x of the Start Spot box
    public static Button legendStartButton = null;   // the Start Spot box (so neighbors can copy its gap to the 1Q/2Q button)
    public static double legendAfterStartX = 0.0;   // x just right of the Start Spot box (plus the legend gap); the 1Q/2Q button is placed here
    // (returns recorderDestinationButtonCanvas, anyRoadLegendIcon, dungeonLegendIconArea, updateCurrentRecorderDestinationNumeral, legendCanvas, startIcon, theCustomWaypointIcon)
    public static (Canvas, Image, Canvas, Action, Canvas, Canvas, Canvas) MakeLegend(CustomComboBoxes.CanvasManager cm, Graphics.SharedEvent doUIUpdateEvent)
    {
        Canvas makeStartIcon()
        {
            var back = new Ellipse { Width = (double)(11 * 3) - 2.0, Height = (double)(11 * 3) - 2.0, Stroke = Brushes.DarkViolet, StrokeThickness = 3.0, IsHitTestVisible = false };
            //back.StrokeDashArray <- new DoubleCollection([5.; 4.8; 9.7; 4.8; 5.])
            //let back = new Shapes.Rectangle(Width=23., Height=23., Stroke=Brushes.DarkViolet, StrokeThickness=3.0, IsHitTestVisible=false, RenderTransform=DungeonUI.fortyFiveDegrees)
            back.Effect = new BlurEffect { Radius = 7.0 };   // (WPF: KernelType=Gaussian; Avalonia's blur is always Gaussian)
            var front = new Ellipse { Width = (double)(11 * 3) - 2.0, Height = (double)(11 * 3) - 2.0, Stroke = Brushes.Lime, StrokeThickness = 3.0, IsHitTestVisible = false };
            //front.StrokeDashArray <- new DoubleCollection([5.; 4.8; 9.7; 4.8; 5.])
            //let front = new Shapes.Rectangle(Width=23., Height=23., Stroke=Brushes.Lime, StrokeThickness=3.0, IsHitTestVisible=false, RenderTransform=DungeonUI.fortyFiveDegrees)
            var c = new Canvas { Width = front.Width, Height = front.Height };
            if (Graphics.canUseEffectsWithoutDestroyingPerformance)
            {
                canvasAdd(c, back, 0.0, 0.0);
                //canvasAdd(c, back, 15., 0.)
            }
            canvasAdd(c, front, 0.0, 0.0);
            //canvasAdd(c, front, 15., 0.)
            return c;
        }
        var startIcon = makeStartIcon();
        (Func<Canvas>, Canvas) make_customWaypoint()
        {
            var L = 6.0;
            Canvas makeIcon()
            {
                var bg = new Ellipse { Width = (double)(11 * 3) - 2.0 + 3.0 * L, Height = (double)(11 * 3) - 2.0 + 2.0 * L, Stroke = Brushes.Black, StrokeThickness = 3.0, IsHitTestVisible = false };
                bg.Effect = new BlurEffect { Radius = 5.0 };
                var fg = new Ellipse { Width = (double)(11 * 3) - 2.0 + 3.0 * L, Height = (double)(11 * 3) - 2.0 + 2.0 * L, Stroke = Brushes.Orange, StrokeThickness = 3.0, IsHitTestVisible = false };
                var c = new Canvas();
                if (Graphics.canUseEffectsWithoutDestroyingPerformance)
                    canvasAdd(c, bg, 1.0, 0.0);
                canvasAdd(c, fg, 1.0, 0.0);
                return c;
            }
            var theCustomWaypointIcon_ = makeIcon();
            var animBrush = new LinearGradientBrush
            {
                GradientStops = new GradientStops { new GradientStop(Colors.Orange, 0.0), new GradientStop(Colors.Orange, 0.6), new GradientStop(Colors.White, 1.0) },
                StartPoint = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1.0, 0.0, RelativeUnit.Relative)
            };
            ((Ellipse)theCustomWaypointIcon_.Children[theCustomWaypointIcon_.Children.Count - 1]).Stroke = animBrush;
            // (WPF: a PointAnimationUsingKeyFrames on EndPoint, linear key frames at every 1/8 of 4 s, repeating forever, not auto-reversed.
            // Tweened by hand on a 16 ms timer, as Graphics.TileHighlightRectangle and Views' glow do.)
            var keyFrames = new (double x, double y)[] { (1.0, 0.5), (0.8, 0.8), (0.5, 1.0), (0.2, 0.8), (0.0, 0.5), (0.2, 0.2), (0.5, 0.0), (0.8, 0.2), (1.0, 0.5) };   // at 0, 12.5%, 25%, ... 100%
            var start = DateTime.Now;
            var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16.0), DispatcherPriority.Render, (_, _) => { });
            timer.Tick += (_, _) =>
            {
                var f = ((DateTime.Now - start).TotalSeconds % 4.0) / 4.0 * 8.0;   // 0..8, one unit per key frame
                var k = Math.Min((int)Math.Floor(f), 7);
                var t = f - (double)k;
                var (x0, y0) = keyFrames[k];
                var (x1, y1) = keyFrames[k + 1];
                animBrush.EndPoint = new RelativePoint(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, RelativeUnit.Relative);
            };
            timer.Start();
            return (makeIcon, theCustomWaypointIcon_);
        }
        var (makeCustomWaypointIcon, theCustomWaypointIcon) = make_customWaypoint();

        // map legend
        var BG = Graphics.freeze(new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)));
        var legendCanvas = new Canvas { Width = 586.0, Height = (double)(11 * 3), Background = BG };
        // everything after the "LEGEND - " label starts one space-width to the right of the dash (the same gap as between the D and the dash)
        double visibleLeft(System.Drawing.Bitmap bmp)
        {
            var minX = bmp.Width;
            for (int x = 0; x <= bmp.Width - 1; x++)
                for (int y = 0; y <= bmp.Height - 1; y++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (p.A > 0 && (p.R > 12 || p.G > 12 || p.B > 12) && x < minX) minX = x;
                }
            return (double)(minX == bmp.Width ? 0 : minX);
        }
        // the 'LEGEND -' label is gone: the Any Road icon's visible left edge sits at x=0, the left edge of the row
        var legendShift = -(visibleLeft(Graphics.theFullTileBmpTable[9][0])) - 58.0;
        // Spacing of everything after the Any Road section: the same visible gap as between the dash in "LEGEND -" and the Any Road icon.
        (double, double, double, double, double, double) make_legendXs()
        {
            // (WPF: SystemFonts.MessageFontFamily, the font WPF's TextBoxes use; here Avalonia's default font, which its TextBoxes use)
            var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);
            double measure(string t) => (new FormattedText(t, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, 12.0, Brushes.Black)).WidthIncludingTrailingWhitespace;
            double measureLines(string t) => t.Split('\n').Select(measure).Max();
            var anyRoadX = 58.0 + legendShift;
            var g = measure(" ") + 14.0;   // a bit more room between Any Road, Dungeon (Completed) and Recorder Destination
            var tileLeft = visibleLeft(Graphics.theFullTileBmpTable[0][0]);
            var anyRoadTextRight = anyRoadX + 33.0 + 2.0 + measureLines("Any Road\n(Warp)");
            var dungeonX = anyRoadTextRight + g - tileLeft;
            var dungeonTextRight = dungeonX + 35.0 + 2.0 + measureLines("Dungeon\n(Completed)");
            var recorderX = dungeonTextRight + g - tileLeft;
            var dotsX = recorderX + 100.0;   // original spot: right next to the word 'Recorder' (371 vs the recorder icon at 271)
            var _dotsRight = dotsX + measure("...") + 14.0;
            var customWaypointX = ITEM_PROGRESS_FIRST_ITEM + 7.0 * 30.0 + visibleLeft(Graphics.red_candle_bmp);   // aligned with the left of the candle icon (item slot 7) in the item row below
            var boxGap = 7.0;   // the circle, Start Spot, 1Q/2Q and hint boxes sit close together
            var startX = customWaypointX;   // (the Custom Waypoint button is gone: everything to its right starts where the button was)
            return (dungeonX, recorderX, dotsX, customWaypointX, startX, startX + OMTW * 1.45 + boxGap);
        }
        var legendXs = make_legendXs();
        var (dungeonLegendX, recorderLegendX, dotsLegendX, customWaypointLegendX, startLegendX, afterStartLegendX) = legendXs;
        legendAfterStartX = afterStartLegendX;
        legendStartX = startLegendX;
        var dungeonLegendIconCanvas = new Canvas { Width = (double)(16 * 3), Height = (double)(11 * 3) };
        var dungeonLegendIconArea = new Canvas { Width = 15.0, Height = (double)(11 * 3), Background = Brushes.White, Opacity = 0.0001 };   // to respond to mouse hover
        canvasAdd(legendCanvas, dungeonLegendIconCanvas, dungeonLegendX, 3.0);
        var recorderDestinationButtonCanvas = new Canvas { Width = OMTW, Height = (double)(11 * 3), Background = Brushes.Transparent, ClipToBounds = true };   // transparent (still clickable) so it can't cover the end of the 'Dungeon (Completed)' text
        var recorderDestinationMouseHoverHighlight = new Rectangle { Width = OMTW, Height = (double)(11 * 3), Stroke = Brushes.DarkCyan, StrokeThickness = 1.0, Opacity = 0.0 };
        //let recorderEllipse = new Shapes.Ellipse(Width=float(11*3)-2.+12.0, Height=float(11*3)-2.+6., Stroke=RecorderEllipseColor(), StrokeThickness=3.0, IsHitTestVisible=false)
        // (Avalonia rotates around the centre by default; WPF around the top-left corner, see DungeonUI.fortyFiveDegrees)
        var recorderEllipse = new Rectangle { Width = 35.0, Height = 35.0, Stroke = RecorderEllipseColor(), StrokeThickness = 3.0, IsHitTestVisible = false, RenderTransform = DungeonUI.fortyFiveDegrees, RenderTransformOrigin = RelativePoint.TopLeft };

        var _legendTB = new TextBox { FontSize = 12.0, Foreground = Brushes.Orange, Background = BG, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), Text = "LEGEND - ", Height = (double)(11 * 3), VerticalContentAlignment = VerticalAlignment.Center };
        var tb = new TextBox { FontSize = 12.0, Foreground = Brushes.Orange, Background = BG, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), Text = "Dungeon\n(Completed)" };
        canvasAdd(legendCanvas, tb, dungeonLegendX + 35.0, 3.0);
        canvasAdd(legendCanvas, recorderDestinationButtonCanvas, recorderLegendX, 3.0);
        tb = new TextBox { FontSize = 12.0, Foreground = Brushes.Orange, Background = BG, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), Text = "Recorder\nDestination" };
        canvasAdd(legendCanvas, tb, recorderLegendX + 48.0, 3.0);
        tb = new TextBox { FontSize = 12.0, Foreground = Brushes.Orange, Background = BG, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), Text = "..." };
        tb.FontSize = 12.0;
        tb.FontWeight = FontWeight.Bold;
        tb.TextAlignment = TextAlignment.Center;
        tb.Padding = new Thickness(0.0, 0.0, 0.0, 2.0);
        var recorderDestinationSettingsButton = new Button { Content = tb, Width = 20.0, Height = 18.0 };
        Graphics.styleRoundedButton(recorderDestinationSettingsButton);
        canvasAdd(legendCanvas, recorderDestinationSettingsButton, dotsLegendX, 3.0);
        void updateCurrentRecorderDestinationNumeral()
        {
            dungeonLegendIconCanvas.Children.Clear();
            recorderDestinationButtonCanvas.Children.Clear();
            canvasAdd(recorderDestinationButtonCanvas, recorderEllipse, 23.0, -8.0);
            var yellowDungeonBMP = Graphics.theFullTileBmpTable[currentRecorderDestinationIndex][0];
            canvasAdd(dungeonLegendIconCanvas, Graphics.BMPtoImage(yellowDungeonBMP), 0.0, 0.0);
            recorderEllipse.Stroke = RecorderEllipseColor();
            if (TrackerModel.recorderToNewDungeons)
            {
                var recorderDestinationLegendIcon = Graphics.BMPtoImage(yellowDungeonBMP);
                canvasAdd(recorderDestinationButtonCanvas, recorderDestinationLegendIcon, 0.0, 0.0);
            }
            else
            {
                var tb2 = new TextBox { Text = $"{(char)(currentRecorderDestinationIndex + (int)'1')}", FontSize = 12.0, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Background = Brushes.Black,
                                        IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0) };
                recorderDestinationButtonCanvas.Children.Add(tb2);
                Canvas.SetLeft(tb2, 0.0);
                Canvas.SetBottom(tb2, 0.0);
            }
            recorderDestinationButtonCanvas.Children.Add(recorderDestinationMouseHoverHighlight);
            var halfShade = new Rectangle { Width = 15.0, Height = 14.0, StrokeThickness = 0.0, Fill = Brushes.Black, Opacity = 0.4, IsHitTestVisible = false };
            canvasAdd(dungeonLegendIconCanvas, halfShade, 15.0, 16.0);
            canvasAdd(dungeonLegendIconCanvas, dungeonLegendIconArea, 15.0, 0.0);
        }
        updateCurrentRecorderDestinationNumeral();
        recorderDestinationSettingsButton.Click += (_, _) =>
        {
            if (!popupIsActive)
            {
                popupIsActive = true;
                async Task go()
                {
                    var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(3.0) };
                    var tb3 = new TextBox { Text = "Recorder settings", IsReadOnly = true, IsHitTestVisible = false, FontSize = 16.0, BorderThickness = new Thickness(0.0), Foreground = Brushes.Orange, Background = Brushes.Black };
                    sp.Children.Add(tb3);
                    sp.Children.Add(new DockPanel { Background = Brushes.Orange, Margin = new Thickness(0.0, 5.0, 0.0, 5.0), Height = 3.0 });
                    var cb = new CheckBox { Content = new TextBox { Text = "Recorder to new dungeons", IsReadOnly = true, IsHitTestVisible = false, FontSize = 16.0, BorderThickness = new Thickness(0.0),
                                                                Foreground = Brushes.Orange, Background = Brushes.Black }, Height = 22.0 };
                    ToolTip.SetTip(cb, "If unchecked, the recorder goes to vanilla first quest dungeon locations");
                    cb.IsChecked = TrackerModel.recorderToNewDungeons;
                    Graphics.onChecked(cb, () => { TrackerModel.recorderToNewDungeons = true; updateCurrentRecorderDestinationNumeral(); doUIUpdateEvent.Trigger(); });
                    Graphics.onUnchecked(cb, () => { TrackerModel.recorderToNewDungeons = false; updateCurrentRecorderDestinationNumeral(); doUIUpdateEvent.Trigger(); });
                    sp.Children.Add(cb);
                    var cb2 = new CheckBox { Content = new TextBox { Text = "Recorder to unbeaten dungeons", IsReadOnly = true, IsHitTestVisible = false, FontSize = 16.0, BorderThickness = new Thickness(0.0),
                                                                Foreground = Brushes.Orange, Background = Brushes.Black }, Height = 22.0 };
                    ToolTip.SetTip(cb2, "If checked, the recorder goes to dungeons where you do NOT yet have the triforce");
                    cb2.IsChecked = TrackerModel.recorderToUnbeatenDungeons;
                    Graphics.onChecked(cb2, () => { TrackerModel.recorderToUnbeatenDungeons = true; doUIUpdateEvent.Trigger(); });
                    Graphics.onUnchecked(cb2, () => { TrackerModel.recorderToUnbeatenDungeons = false; doUIUpdateEvent.Trigger(); });
                    sp.Children.Add(cb2);
                    var wh = new ManualResetEvent(false);
                    await CustomComboBoxes.DoModal(cm, wh, 200.0, 200.0, new Border { Child = sp, Background = Brushes.Black, BorderThickness = new Thickness(6.0), BorderBrush = Brushes.Gray });
                    popupIsActive = false;
                }
                CustomComboBoxes.StartImmediate(go());
            }
        };
        // hovering shows the whistle (recorder) pattern for this overworld: 1,2,3,...,8 in a first quest overworld, 1,3,2,5,4,6,8,7 in a second quest one
        // (WPF: a ToolTip whose content is rebuilt in its Opened event. Avalonia uses a ToolTip set as the tip as it is, and raises ToolTipOpening on the
        // control just before showing it, so the content is rebuilt there. Placement and the show delay are attached properties of the control.)
        var recorderPatternTip = new ToolTip { Background = Brushes.Black, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(2.0), Padding = new Thickness(4.0, 1.0, 4.0, 2.0) };
        ToolTip.SetPlacement(recorderDestinationButtonCanvas, PlacementMode.Top);
        ToolTip.SetTip(recorderDestinationButtonCanvas, recorderPatternTip);
        ToolTip.SetShowDelay(recorderDestinationButtonCanvas, 100);
        setToolTipShowDuration(recorderDestinationButtonCanvas, 60000);
        ToolTip.AddToolTipOpeningHandler(recorderDestinationButtonCanvas, (_, _) =>
        {
            var isFirst = TrackerModel.owInstance.Quest.IsFirstQuestOW;
            var order = isFirst ? new[] { 1, 2, 3, 4, 5, 6, 7, 8 } : new[] { 1, 3, 2, 5, 4, 6, 8, 7 };
            var col = new StackPanel { Orientation = Orientation.Vertical };
            TextBlock mk(string t, double size) => new TextBlock { Text = t, Foreground = Brushes.Orange, FontSize = size, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            col.Children.Add(mk((isFirst ? "1Q whistle order" : "2Q whistle order"), 12.0));
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var n in order)
            {
                var t = mk(n.ToString(), 16.0);
                t.Margin = new Thickness(3.0, 0.0, 3.0, 0.0);
                row.Children.Add(t);
            }
            col.Children.Add(row);
            recorderPatternTip.Content = col;
        });
        recorderDestinationButtonCanvas.PointerEntered += (_, _) => recorderDestinationMouseHoverHighlight.Opacity = 1.0;
        recorderDestinationButtonCanvas.PointerExited += (_, _) => recorderDestinationMouseHoverHighlight.Opacity = 0.0;
        recorderDestinationButtonCanvas.PointerPressed += (_, ea) =>
        {
            var kind = ea.GetCurrentPoint(recorderDestinationButtonCanvas).Properties.PointerUpdateKind;
            var delta =
                kind == PointerUpdateKind.LeftButtonPressed ?
                    1
                : kind == PointerUpdateKind.RightButtonPressed ?
                    -1
                :
                    0;
            currentRecorderDestinationIndex = (currentRecorderDestinationIndex + 8 + delta) % 8;
            updateCurrentRecorderDestinationNumeral();
            ea.Handled = true;
        };
        /*   // This screwed me up multiple times, and the fact that it's not user-rebindable is bad.
        recorderDestinationButtonCanvas.MyKeyAdd(fun ea ->
            let _skm,k = ea.Key
            if k >= Input.Key.D1 && k <= Input.Key.D8 then
                currentRecorderDestinationIndex <- (int k) - (int Input.Key.D1)
                updateCurrentRecorderDestinationNumeral()
                ea.Handled <- true
            if k >= Input.Key.NumPad1 && k <= Input.Key.NumPad8 then
                currentRecorderDestinationIndex <- (int k) - (int Input.Key.NumPad1)
                updateCurrentRecorderDestinationNumeral()
                ea.Handled <- true
            )
        */

        var anyRoadLegendIcon = Graphics.BMPtoImage(Graphics.theFullTileBmpTable[9][0]);
        canvasAdd(legendCanvas, anyRoadLegendIcon, 58.0 + legendShift + 3.0, 3.0);   // legend entries 3px down: centered vertically between the map and the item progress icons
        tb = new TextBox { FontSize = 12.0, Foreground = Brushes.Orange, Background = BG, IsReadOnly = true, BorderThickness = new Thickness(0.0), Text = "Any Road\n(Warp)" };
        canvasAdd(legendCanvas, tb, 91.0 + legendShift + 3.0, 3.0);

        var legendStartIconButtonCanvas = new Canvas { Background = BG, Width = OMTW * 1.45, Height = 29.0 };   // same height as the Custom Waypoint button
        var legendStartIcon = makeStartIcon();
        legendStartIcon.RenderTransform = new ScaleTransform(0.85, 0.85);   // 31px ring scaled down to fit the shorter button
        legendStartIcon.RenderTransformOrigin = RelativePoint.TopLeft;   // (WPF scales from the top-left corner; Avalonia's default is the centre)
        canvasAdd(legendStartIconButtonCanvas, legendStartIcon, 0.0 + 4.0 * OMTW / 48.0, 1.0);
        tb = new TextBox { FontSize = 10.0, Foreground = Brushes.Orange, Background = Brushes.Transparent, IsReadOnly = true, BorderThickness = new Thickness(0.0), Text = "Start\nSpot", IsHitTestVisible = false };
        canvasAdd(legendStartIconButtonCanvas, tb, 0.8 * OMTW, 0.0);
        var legendStartIconButton = new Button { Content = legendStartIconButtonCanvas };
        Graphics.styleRoundedButton(legendStartIconButton);
        canvasAdd(legendCanvas, legendStartIconButton, startLegendX, 4.0);   // 4px down: centered vertically between the map and the item progress icons
        legendStartButton = legendStartIconButton;
        Action makeIconButtonBehavior(string prefix, string name, Func<Canvas> makeIcon, double iconOffsetX, double iconOffsetY, Action<int, int> setIconXY) => () =>
        {
            if (!popupIsActive)
            {
                popupIsActive = true;
                var tb4 = new TextBox { Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, FontSize = 16.0, Padding = new Thickness(3.0),
                                        Text = $"{prefix}Left-Click an overworld map tile to move the {name} icon there, or\nRight-click to remove it from the map, or\nClick anywhere outside the map to cancel and make no changes" };
                var element = new Canvas { Width = OMTW * 16.0, Height = (double)(8 * 11 * 3), Background = Brushes.Transparent, IsHitTestVisible = true };
                element.Children.Add(tb4);
                Canvas.SetBottom(tb4, element.Height);
                var hoverIcon = makeIcon();
                // (WPF reads Input.Mouse.GetPosition(element) in the key handler; Avalonia has no global mouse position, so the last position seen over the element is kept, null when the mouse is elsewhere)
                Point? lastMousePos = null;
                element.PointerExited += (_, _) => { element.Children.Remove(hoverIcon); lastMousePos = null; };
                element.PointerMoved += (_, ea) =>
                {
                    var mousePos = ea.GetPosition(element);
                    lastMousePos = mousePos;
                    var i = (int)(mousePos.X / OMTW);
                    var j = (int)(mousePos.Y / (11.0 * 3.0));
                    element.Children.Remove(hoverIcon);
                    canvasAdd(element, hoverIcon, (double)i * OMTW + iconOffsetX, (double)(j * 11 * 3) + iconOffsetY);
                };
                var wh = new ManualResetEvent(false);
                element.PointerPressed += (_, ea) =>
                {
                    if (ea.GetCurrentPoint(element).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
                    {
                        setIconXY(-1, -1);
                        doUIUpdateEvent.Trigger();
                        wh.Set();
                    }
                    else
                    {
                        var mousePos = ea.GetPosition(element);
                        var i = (int)(mousePos.X / OMTW);
                        var j = (int)(mousePos.Y / (11.0 * 3.0));
                        if (i >= 0 && i <= 15 && j >= 0 && j <= 7)
                        {
                            setIconXY((displayIsCurrentlyMirrored ? (15 - i) : i), j);
                            doUIUpdateEvent.Trigger();
                            wh.Set();
                        }
                    }
                };
                element.MyKeyAdd(ea =>
                {
                    if (lastMousePos is not Point mousePos) return;
                    var i = (int)(mousePos.X / OMTW);
                    var j = (int)(mousePos.Y / (11.0 * 3.0));
                    if (i >= 0 && i <= 15 && j >= 0 && j <= 7)
                    {
                        if (HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key, out var target))
                        {
                            switch (target)
                            {
                                case HotKeys.GlobalHotkeyTargets.MoveCursorRight:
                                    ea.Handled = true;
                                    if (i < 15) Graphics.NavigationallyWarpMouseCursorTo(element.TranslatePoint(new Point((double)(i + 1) * OMTW + OMTW / 2.0, (double)(j * 11 * 3 + 11 * 3 / 2)), cm.AppMainCanvas).Value);
                                    break;
                                case HotKeys.GlobalHotkeyTargets.MoveCursorLeft:
                                    ea.Handled = true;
                                    if (i > 0) Graphics.NavigationallyWarpMouseCursorTo(element.TranslatePoint(new Point((double)(i - 1) * OMTW + OMTW / 2.0, (double)(j * 11 * 3 + 11 * 3 / 2)), cm.AppMainCanvas).Value);
                                    break;
                                case HotKeys.GlobalHotkeyTargets.MoveCursorUp:
                                    ea.Handled = true;
                                    if (j > 0) Graphics.NavigationallyWarpMouseCursorTo(element.TranslatePoint(new Point((double)(i) * OMTW + OMTW / 2.0, (double)((j - 1) * 11 * 3 + 11 * 3 / 2)), cm.AppMainCanvas).Value);
                                    break;
                                case HotKeys.GlobalHotkeyTargets.MoveCursorDown:
                                    ea.Handled = true;
                                    if (j < 7) Graphics.NavigationallyWarpMouseCursorTo(element.TranslatePoint(new Point((double)(i) * OMTW + OMTW / 2.0, (double)((j + 1) * 11 * 3 + 11 * 3 / 2)), cm.AppMainCanvas).Value);
                                    break;
                                case HotKeys.GlobalHotkeyTargets.LeftClick: Graphics.Win32.LeftMouseClick(); break;
                                case HotKeys.GlobalHotkeyTargets.MiddleClick: Graphics.Win32.MiddleMouseClick(); break;
                                case HotKeys.GlobalHotkeyTargets.RightClick: Graphics.Win32.RightMouseClick(); break;
                                default: break;
                            }
                        }
                    }
                });
                async Task go()
                {
                    Graphics.WarpMouseCursorTo(new Point(OMTW * 8.5, 150.0 + (double)(11 * 3 * 4 + 11 * 3 / 2)));  // warp mouse to center of map
                    await CustomComboBoxes.DoModal(cm, wh, 0.0, 150.0, element);
                    popupIsActive = false;
                }
                CustomComboBoxes.StartImmediate(go());
            }
        };
        void setStartIconXY(int i, int j)
        {
            TrackerModel.startIconX = i;
            TrackerModel.startIconY = j;
        }
        legendStartIconButtonBehavior = makeIconButtonBehavior("Mark where you first spawned at the start of the game\n", "Start Spot", makeStartIcon, 8.5 * OMTW / 48.0, 0.0, setStartIconXY);
        legendStartIconButton.Click += (_, _) => legendStartIconButtonBehavior();

        void setCustomWaypointXY(int i, int j)
        {
            TrackerModel.customWaypointX = i;
            TrackerModel.customWaypointY = j;
        }
        var customWaypointButtonBehavior = makeIconButtonBehavior("(You can use the Custom Waypoint marker for whatever reason you like, to mark a single overworld tile)\n",
                                                                    "Custom Waypoint", makeCustomWaypointIcon, -2.0, -5.0, setCustomWaypointXY);
        var customWaypointButtonCanvas = new Canvas { Background = BG, Width = 29.0, Height = 29.0 };
        var customWaypointIcon = makeCustomWaypointIcon();
        var scaleTrans = new ScaleTransform(0.5378, 0.6128);   // x scaled less than y so the 49x43 ellipse becomes a true circle, 26.35px = same diameter as the scaled Start Spot ring
        // (WPF freezes it if it can; Avalonia has no Freeze)
        customWaypointIcon.RenderTransform = scaleTrans;
        customWaypointIcon.RenderTransformOrigin = RelativePoint.TopLeft;   // (WPF scales from the top-left corner; Avalonia's default is the centre)
        canvasAdd(customWaypointButtonCanvas, customWaypointIcon, 1.3, 1.0);
        var customWaypointButton = new Button { Content = customWaypointButtonCanvas };
        _ = customWaypointLegendX;   // (the Custom Waypoint button is no longer shown in the legend)
        _ = customWaypointButton;
        _ = customWaypointButtonBehavior;

        return (recorderDestinationButtonCanvas, anyRoadLegendIcon, dungeonLegendIconArea, updateCurrentRecorderDestinationNumeral, legendCanvas, startIcon, theCustomWaypointIcon);
    }

    // set by WPFUI: refreshes the whole UI after the player manually changes an item in the item progress bar
    public static Action itemProgressChanged = () => { };
    // set by WPFUI: un-collects every marker icon with one of these item ids on the overworld map
    public static Action<List<int>> uncollectMapIcons = _ => { };
    // (returns redrawItemProgressBar, itemProgressCanvas, the 'Item Progress' button)
    public static (Action, Canvas, Button) MakeItemProgressBar(OverworldData.OverworldInstance _owInstance)
    {
        // item progress
        var itemProgressCanvas = new Canvas { Width = 16.0 * OMTW, Height = 30.0 };
        var tb = Graphics.makeButton("Item Progress", 12.0, Brushes.Orange);   // same look as the version button next to it
        tb.IsHitTestVisible = false;
        Graphics.styleRoundedButton(tb);
        itemProgressCanvas.PointerMoved += (_, ea) =>
        {
            var pos = ea.GetPosition(itemProgressCanvas);
            var slot = (int)(Math.Floor((pos.X - ITEM_PROGRESS_FIRST_ITEM) / 30.0));
            // slots are 30px wide, in this order: sword, arrow, bow, ladder, power bracelet, recorder, raft, candle, ring, wand, key, boomerang, meat, book, letter
            switch (slot)
            {
                case 0: showSwordLocator(); break;
                case 1: showShopLocatorInstanceFunc(TrackerModel.MapSquareChoiceDomainHelper.ARROW); break;   // arrow shops (the marker icons are added below)
                case 3: showLocatorInstanceFunc(TrackerModel.owInstance.Ladderable); break;
                case 4: showLocatorInstanceFunc(TrackerModel.owInstance.PowerBraceletable); break;
                case 5: showLocatorInstanceFunc(TrackerModel.owInstance.Whistleable); break;
                case 6: showLocatorInstanceFunc(TrackerModel.owInstance.Raftable); break;
                case 7: showLocatorInstanceFunc(TrackerModel.owInstance.Burnable); break;
                case 10: showShopLocatorInstanceFunc(TrackerModel.MapSquareChoiceDomainHelper.KEY); break;
                case 12: showShopLocatorInstanceFunc(TrackerModel.MapSquareChoiceDomainHelper.MEAT); break;
                default: break;
            }
            // also highlight any tile where you've placed this item on the map (marker icons, cave item boxes)
            switch (slot)
            {
                case 0: showMarkerItemsLocator(new List<int> { TrackerModel.ITEMS.WOODSWORD, TrackerModel.ITEMS.WHITESWORD, TrackerModel.ITEMS.MAGICALSWORD, TrackerModel.ITEMS.PROGRESSIVESWORD }); break;
                case 1: showMarkerItemsLocator(new List<int> { TrackerModel.ITEMS.WOODARROW, TrackerModel.ITEMS.SILVERARROW }); break;
                case 2: showMarkerItemsLocator(new List<int> { TrackerModel.ITEMS.BOW }); break;
                case 10: showMarkerItemsLocator(new List<int> { TrackerModel.ITEMS.KEY, TrackerModel.ITEMS.OLDKEY }); break;
                case 12: showMarkerItemsLocator(new List<int> { TrackerModel.ITEMS.MEAT }); break;
                case 3: showMarkerItemsLocator(new List<int> { TrackerModel.ITEMS.LADDER }); break;
                case 4: showMarkerItemsLocator(new List<int> { TrackerModel.ITEMS.POWERBRACELET }); break;
                case 5: showMarkerItemsLocator(new List<int> { TrackerModel.ITEMS.RECORDER }); break;
                case 6: showMarkerItemsLocator(new List<int> { TrackerModel.ITEMS.RAFT }); break;
                case 7: showMarkerItemsLocator(new List<int> { TrackerModel.ITEMS.BLUECANDLE, TrackerModel.ITEMS.REDCANDLE }); break;
                case 8: showMarkerItemsLocator(new List<int> { TrackerModel.ITEMS.BLUERING, TrackerModel.ITEMS.REDRING }); break;
                case 9: showMarkerItemsLocator(new List<int> { TrackerModel.ITEMS.WAND }); break;
                case 11: showMarkerItemsLocator(new List<int> { TrackerModel.ITEMS.BOOMERANG, TrackerModel.ITEMS.MAGICBOOMERANG }); break;
                case 13: showMarkerItemsLocator(new List<int> { TrackerModel.ITEMS.BOOKSHIELD }); break;
                case 14: showMarkerItemsLocator(new List<int> { TrackerModel.ITEMS.LETTER }); break;
                default: break;
            }
        };
        itemProgressCanvas.PointerExited += (_, _) => hideLocator();

        // make item progress bar images only once, then swap e.g. grey/blue/red candle on redraw; re-creating the images each time is expensive as measure in profiling
        double x = ITEM_PROGRESS_FIRST_ITEM, y = 3.0;
        var DX = 30.0;
        var swordsByLevel = Enumerable.Range(0, 4).Select(level =>
        {
            var sword = Graphics.BMPtoImage(Graphics.swordLevelToOwBmp(level));
            sword.Opacity = 0.0;
            canvasAdd(itemProgressCanvas, sword, x, y);
            return sword;
        }).ToArray();
        var sword4 = Graphics.BMPtoImage(Graphics.ow_sword4_item_bmp);   // once a 4th sword has been collected, the 4th sword icon (blue handle) shows
        sword4.Opacity = 0.0;
        canvasAdd(itemProgressCanvas, sword4, x, y);
        x = x + DX;
        var grey_arrow = Graphics.BMPtoImage(Graphics.greyscale(Graphics.silver_arrow_bmp));
        var wood_arrow = Graphics.BMPtoImage(Graphics.wood_arrow_bmp);
        var silver_arrow = Graphics.BMPtoImage(Graphics.silver_arrow_bmp);
        foreach (var arrow in new[] { grey_arrow, wood_arrow, silver_arrow })
        {
            arrow.Opacity = 0.0;
            canvasAdd(itemProgressCanvas, arrow, x, y);
        }
        x = x + DX;
        var have_bow = Graphics.BMPtoImage(Graphics.bow_bmp);
        var grey_bow = Graphics.BMPtoImage(Graphics.greyscale(Graphics.bow_bmp));
        have_bow.Opacity = 0.0;
        grey_bow.Opacity = 0.0;
        canvasAdd(itemProgressCanvas, have_bow, x, y);
        canvasAdd(itemProgressCanvas, grey_bow, x, y);
        x = x + DX;
        var grey_ladder = Graphics.BMPtoImage(Graphics.greyscale(Graphics.ladder_bmp));
        var have_ladder = Graphics.BMPtoImage(Graphics.ladder_bmp);
        grey_ladder.Opacity = 0.0;
        have_ladder.Opacity = 0.0;
        canvasAdd(itemProgressCanvas, grey_ladder, x, y);
        canvasAdd(itemProgressCanvas, have_ladder, x, y);
        x = x + DX;
        var grey_power_bracelet = Graphics.BMPtoImage(Graphics.greyscale(Graphics.power_bracelet_bmp));
        var have_power_bracelet = Graphics.BMPtoImage(Graphics.power_bracelet_bmp);
        var dash_bracelet = new Rectangle { Width = (double)Graphics.power_bracelet_bmp.Width + 2.0, Height = (double)Graphics.power_bracelet_bmp.Height + 2.0, Stroke = Brushes.Red, StrokeThickness = 2.0, StrokeDashArray = new AvaloniaList<double>(new[] { 3.0, 2.0 }), IsHitTestVisible = false, IsVisible = false };
        grey_power_bracelet.Opacity = 0.0;
        have_power_bracelet.Opacity = 0.0;
        canvasAdd(itemProgressCanvas, grey_power_bracelet, x, y);
        canvasAdd(itemProgressCanvas, have_power_bracelet, x, y);
        canvasAdd(itemProgressCanvas, dash_bracelet, x - 1.0, y - 1.0);
        x = x + DX;
        var grey_recorder = Graphics.BMPtoImage(Graphics.greyscale(Graphics.recorder_bmp));
        var have_recorder = Graphics.BMPtoImage(Graphics.recorder_bmp);
        grey_recorder.Opacity = 0.0;
        have_recorder.Opacity = 0.0;
        canvasAdd(itemProgressCanvas, grey_recorder, x, y);
        canvasAdd(itemProgressCanvas, have_recorder, x, y);
        x = x + DX;
        var grey_raft = Graphics.BMPtoImage(Graphics.greyscale(Graphics.raft_bmp));
        var have_raft = Graphics.BMPtoImage(Graphics.raft_bmp);
        grey_raft.Opacity = 0.0;
        have_raft.Opacity = 0.0;
        canvasAdd(itemProgressCanvas, grey_raft, x, y);
        canvasAdd(itemProgressCanvas, have_raft, x, y);
        var dash_raft = new Rectangle { Width = (double)Graphics.raft_bmp.Width + 2.0, Height = (double)Graphics.raft_bmp.Height + 2.0, Stroke = Brushes.Red, StrokeThickness = 2.0, StrokeDashArray = new AvaloniaList<double>(new[] { 3.0, 2.0 }), IsHitTestVisible = false, IsVisible = false };
        canvasAdd(itemProgressCanvas, dash_raft, x - 1.0, y - 1.0);
        x = x + DX;
        var grey_candle = Graphics.BMPtoImage(Graphics.greyscale(Graphics.red_candle_bmp));
        var blue_candle = Graphics.BMPtoImage(Graphics.blue_candle_bmp);
        var red_candle = Graphics.BMPtoImage(Graphics.red_candle_bmp);
        foreach (var candle in new[] { grey_candle, blue_candle, red_candle })
        {
            candle.Opacity = 0.0;
            canvasAdd(itemProgressCanvas, candle, x, y);
        }
        x = x + DX;
        var ringsByLevel = Enumerable.Range(0, 3).Select(level =>
        {
            var ring = Graphics.BMPtoImage(Graphics.ringLevelToBmp(level));
            ring.Opacity = 0.0;
            canvasAdd(itemProgressCanvas, ring, x, y);
            return ring;
        }).ToArray();
        x = x + DX;
        var have_wand = Graphics.BMPtoImage(Graphics.wand_bmp);
        var grey_wand = Graphics.BMPtoImage(Graphics.greyscale(Graphics.wand_bmp));
        have_wand.Opacity = 0.0;
        grey_wand.Opacity = 0.0;
        canvasAdd(itemProgressCanvas, have_wand, x, y);
        canvasAdd(itemProgressCanvas, grey_wand, x, y);
        x = x + DX;
        var grey_key = Graphics.BMPtoImage(Graphics.greyscale(Graphics.magic_key_bmp));
        var have_key = Graphics.BMPtoImage(Graphics.magic_key_bmp);
        grey_key.Opacity = 0.0;
        have_key.Opacity = 0.0;
        canvasAdd(itemProgressCanvas, grey_key, x, y);
        canvasAdd(itemProgressCanvas, have_key, x, y);
        x = x + DX;
        var grey_boomer = Graphics.BMPtoImage(Graphics.greyscale(Graphics.magicBoomerangOrMagicalBootsBmp()));
        var wood_boomer = Graphics.BMPtoImage(Graphics.boomerangOrBootsBmp());
        var magi_boomer = Graphics.BMPtoImage(Graphics.magicBoomerangOrMagicalBootsBmp());
        foreach (var boo in new[] { grey_boomer, wood_boomer, magi_boomer })
        {
            boo.Opacity = 0.0;
            canvasAdd(itemProgressCanvas, boo, x, y);
        }
        x = x + DX;
        var grey_meat = Graphics.BMPtoImage(Graphics.greyscale(Graphics.bait_bmp));
        var have_meat = Graphics.BMPtoImage(Graphics.bait_bmp);
        grey_meat.Opacity = 0.0;
        have_meat.Opacity = 0.0;
        canvasAdd(itemProgressCanvas, grey_meat, x, y);
        canvasAdd(itemProgressCanvas, have_meat, x, y);
        x = x + DX;
        var have_book = Graphics.BMPtoImage(Graphics.book_bmp);
        var grey_book = Graphics.BMPtoImage(Graphics.greyscale(Graphics.book_bmp));
        var have_boom_book = Graphics.BMPtoImage(Graphics.boom_book_bmp);
        var grey_boom_book = Graphics.BMPtoImage(Graphics.greyscale(Graphics.boom_book_bmp));
        foreach (var book in new[] { have_book, grey_book, have_boom_book, grey_boom_book })
        {
            book.Opacity = 0.0;
            canvasAdd(itemProgressCanvas, book, x, y);
        }
        x = x + DX;
        var have_letter = Graphics.BMPtoImage(Graphics.old_letter_bmp);
        var grey_letter = Graphics.BMPtoImage(Graphics.greyscale(Graphics.old_letter_bmp));
        have_letter.Opacity = 0.0;
        grey_letter.Opacity = 0.0;
        canvasAdd(itemProgressCanvas, have_letter, x, y);
        canvasAdd(itemProgressCanvas, grey_letter, x, y);

        void redrawItemProgressBar()
        {
            var hasSword4 = (TrackerModel.playerHasSword4.Value == 1);
            for (int i = 0; i < swordsByLevel.Length; i++) { var sword = swordsByLevel[i]; if (!hasSword4 && i == TrackerModel.playerComputedStateSummary.SwordLevel) sword.Opacity = 1.0; else sword.Opacity = 0.0; }
            sword4.Opacity = (hasSword4 ? 1.0 : 0.0);
            switch (TrackerModel.playerComputedStateSummary.CandleLevel)
            {
                case 0: grey_candle.Opacity = 1.0; blue_candle.Opacity = 0.0; red_candle.Opacity = 0.0; break;
                case 1: grey_candle.Opacity = 0.0; blue_candle.Opacity = 1.0; red_candle.Opacity = 0.0; break;
                case 2: grey_candle.Opacity = 0.0; blue_candle.Opacity = 0.0; red_candle.Opacity = 1.0; break;
                default: throw new Exception("bad CandleLevel");
            }
            for (int i = 0; i < ringsByLevel.Length; i++) { var ring = ringsByLevel[i]; if (i == TrackerModel.playerComputedStateSummary.RingLevel) ring.Opacity = 1.0; else ring.Opacity = 0.0; }
            if (TrackerModel.playerComputedStateSummary.HaveBow)
            { have_bow.Opacity = 1.0; grey_bow.Opacity = 0.0; }
            else
            { have_bow.Opacity = 0.0; grey_bow.Opacity = 1.0; }
            switch (TrackerModel.playerComputedStateSummary.ArrowLevel)
            {
                case 0: grey_arrow.Opacity = 1.0; wood_arrow.Opacity = 0.0; silver_arrow.Opacity = 0.0; break;
                case 1: grey_arrow.Opacity = 0.0; wood_arrow.Opacity = 1.0; silver_arrow.Opacity = 0.0; break;
                case 2: grey_arrow.Opacity = 0.0; wood_arrow.Opacity = 0.0; silver_arrow.Opacity = 1.0; break;
                default: throw new Exception("bad ArrowLevel");
            }
            if (TrackerModel.playerComputedStateSummary.HaveWand)
            { have_wand.Opacity = 1.0; grey_wand.Opacity = 0.0; }
            else
            { have_wand.Opacity = 0.0; grey_wand.Opacity = 1.0; }
            if (TrackerModel.IsCurrentlyBook.Invoke(null))
            {
                // book seed
                if (TrackerModel.playerComputedStateSummary.HaveBookOrShield)
                { have_book.Opacity = 1.0; grey_book.Opacity = 0.0; have_boom_book.Opacity = 0.0; grey_boom_book.Opacity = 0.0; }
                else
                { have_book.Opacity = 0.0; grey_book.Opacity = 1.0; have_boom_book.Opacity = 0.0; grey_boom_book.Opacity = 0.0; }
            }
            else
            {
                // boomstick seed
                if (TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBoomBook.Value())
                { have_book.Opacity = 0.0; grey_book.Opacity = 0.0; have_boom_book.Opacity = 1.0; grey_boom_book.Opacity = 0.0; }
                else
                { have_book.Opacity = 0.0; grey_book.Opacity = 0.0; have_boom_book.Opacity = 0.0; grey_boom_book.Opacity = 1.0; }
            }
            switch (TrackerModel.playerComputedStateSummary.BoomerangLevel)
            {
                case 0: grey_boomer.Opacity = 1.0; wood_boomer.Opacity = 0.0; magi_boomer.Opacity = 0.0; break;
                case 1: grey_boomer.Opacity = 0.0; wood_boomer.Opacity = 1.0; magi_boomer.Opacity = 0.0; break;
                case 2: grey_boomer.Opacity = 0.0; wood_boomer.Opacity = 0.0; magi_boomer.Opacity = 1.0; break;
                default: throw new Exception("bad BoomerangLevel");
            }
            if (TrackerModel.playerComputedStateSummary.HaveLadder)
            { have_ladder.Opacity = 1.0; grey_ladder.Opacity = 0.0; }
            else
            { have_ladder.Opacity = 0.0; grey_ladder.Opacity = 1.0; }
            if (TrackerModel.playerComputedStateSummary.HaveRecorder)
            { have_recorder.Opacity = 1.0; grey_recorder.Opacity = 0.0; }
            else
            { have_recorder.Opacity = 0.0; grey_recorder.Opacity = 1.0; }
            if (TrackerModel.playerComputedStateSummary.HavePowerBracelet)
            { have_power_bracelet.Opacity = 1.0; grey_power_bracelet.Opacity = 0.0; }
            else
            { have_power_bracelet.Opacity = 0.0; grey_power_bracelet.Opacity = 1.0; }
            dash_bracelet.IsVisible = TrackerModel.NoFeatOfStrengthHintWasGiven;
            dash_raft.IsVisible = TrackerModel.SailNotHintWasGiven;
            if (TrackerModel.playerComputedStateSummary.HaveRaft)
            { have_raft.Opacity = 1.0; grey_raft.Opacity = 0.0; }
            else
            { have_raft.Opacity = 0.0; grey_raft.Opacity = 1.0; }
            if (TrackerModel.playerComputedStateSummary.HaveAnyKey)
            { have_key.Opacity = 1.0; grey_key.Opacity = 0.0; }
            else
            { have_key.Opacity = 0.0; grey_key.Opacity = 1.0; }
            // letter: lit when marked here, when the letter is marked on the overworld map, or when a Letter item has been obtained in a dungeon box / checked off on the map
            var haveLetter =
                TrackerModel.startingItemsAndExtras.PlayerHasLetter.Value() || TrackerModel.havePotionLetter.Value ||
                TrackerModel.mapCollectedCount[TrackerModel.ITEMS.LETTER] > 0 ||
                (TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes()
                 .Any(b => b.CellCurrent() == TrackerModel.ITEMS.LETTER && b.PlayerHas().Equals(TrackerModel.PlayerHas.YES)));
            if (haveLetter)
            { have_letter.Opacity = 1.0; grey_letter.Opacity = 0.0; }
            else
            { have_letter.Opacity = 0.0; grey_letter.Opacity = 1.0; }
            // meat: lights up once a Meat item has been marked as obtained in any dungeon item box
            var haveMeat =
                TrackerModel.startingItemsAndExtras.PlayerHasMeat.Value() ||
                TrackerModel.mapCollectedCount[TrackerModel.ITEMS.MEAT] > 0 ||   // meat icon checked off on the overworld map
                (TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes()
                 .Any(b => b.CellCurrent() == TrackerModel.ITEMS.MEAT && b.PlayerHas().Equals(TrackerModel.PlayerHas.YES)));
            if (haveMeat)
            { have_meat.Opacity = 1.0; grey_meat.Opacity = 0.0; }
            else
            { have_meat.Opacity = 0.0; grey_meat.Opacity = 1.0; }
        }
        // manual tracking: left-click an item to cycle it forward (e.g. sword: none > wood > white > magical > 4th sword > none), right-click to cycle backward.
        // These set the same 'starting items' flags the old Starting Items popup did, so the rest of the tracker (routing, timeline, saves) just works.
        void stepItem(int slot, int dir)
        {
            var s = TrackerModel.playerComputedStateSummary;
            var pp = TrackerModel.playerProgressAndTakeAnyHearts;
            var si = TrackerModel.startingItemsAndExtras;
            void cycleTo(int cur, int count, Action<int> apply) => apply(((cur + dir) % count + count) % count);
            void flip(bool have, TrackerModel.BoolProperty p) => p.Set(!have);
            switch (slot)
            {
                case 0: cycleTo((TrackerModel.playerHasSword4.Value == 1 ? 4 : s.SwordLevel), (TrackerModelOptions.FourthSwordEnabled ? 5 : 4), n =>
                        {
                            pp.PlayerHasWoodSword.Set(n >= 1);
                            si.PlayerHasWhiteSword.Set(n >= 2);
                            si.PlayerHasMagicalSword.Set(n >= 3);
                            si.PlayerHasSword4.Set(n >= 4);
                        }); break;
                case 1: cycleTo(s.ArrowLevel, 3, n => { pp.PlayerHasWoodArrow.Set(n >= 1); si.PlayerHasSilverArrow.Set(n >= 2); }); break;
                case 2: flip(s.HaveBow, si.PlayerHasBow); break;
                case 3: flip(s.HaveLadder, si.PlayerHasLadder); break;
                case 4: flip(s.HavePowerBracelet, si.PlayerHasPowerBracelet); break;
                case 5: flip(s.HaveRecorder, si.PlayerHasRecorder); break;
                case 6: flip(s.HaveRaft, si.PlayerHasRaft); break;
                case 7: cycleTo(s.CandleLevel, 3, n => { pp.PlayerHasBlueCandle.Set(n >= 1); si.PlayerHasRedCandle.Set(n >= 2); }); break;
                case 8: cycleTo(s.RingLevel, 3, n => { pp.PlayerHasBlueRing.Set(n >= 1); si.PlayerHasRedRing.Set(n >= 2); }); break;
                case 9: flip(s.HaveWand, si.PlayerHasWand); break;
                case 10: flip(s.HaveAnyKey, si.PlayerHasAnyKey); break;
                case 11: cycleTo(s.BoomerangLevel, 3, n => { si.PlayerHasBoomerang.Set(n >= 1); si.PlayerHasMagicBoomerang.Set(n >= 2); }); break;
                case 12:
                    {
                        var haveMeat =
                            si.PlayerHasMeat.Value() || TrackerModel.mapCollectedCount[TrackerModel.ITEMS.MEAT] > 0 ||
                            (TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes()
                             .Any(b => b.CellCurrent() == TrackerModel.ITEMS.MEAT && b.PlayerHas().Equals(TrackerModel.PlayerHas.YES)));
                        flip(haveMeat, si.PlayerHasMeat);
                        break;
                    }
                case 13:
                    if (TrackerModel.IsCurrentlyBook.Invoke(null)) flip(s.HaveBookOrShield, si.PlayerHasBook);
                    else flip(pp.PlayerHasBoomBook.Value(), pp.PlayerHasBoomBook);
                    break;
                case 14:
                    {
                        var haveLetter =
                            si.PlayerHasLetter.Value() || TrackerModel.havePotionLetter.Value ||
                            TrackerModel.mapCollectedCount[TrackerModel.ITEMS.LETTER] > 0 ||
                            (TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes()
                             .Any(b => b.CellCurrent() == TrackerModel.ITEMS.LETTER && b.PlayerHas().Equals(TrackerModel.PlayerHas.YES)));
                        flip(haveLetter, si.PlayerHasLetter);
                        break;
                    }
                default: break;
            }
        }
        // (WPF: MouseLeftButtonDown / MouseRightButtonDown handlers; here one PointerPressed handler calls onItemClick(1) or onItemClick(-1))
        Action<PointerPressedEventArgs> onItemClick(int dir) => ea =>
        {
            if (!popupIsActive)
            {
                var slot = (int)(Math.Floor((ea.GetPosition(itemProgressCanvas).X - ITEM_PROGRESS_FIRST_ITEM) / 30.0));
                if (slot >= 0 && slot <= 14)
                {
                    // leveled items: the item can also have been collected from a dungeon box or a map icon, which would keep the level from ever dropping back down
                    // (e.g. arrow stuck cycling wood <-> silver), so those sources are un-collected when the click is meant to go to a lower level
                    (Func<int>, (int, int)[])? levelsOf =
                        slot switch
                        {
                            1 => ((() => TrackerModel.playerComputedStateSummary.ArrowLevel), new[] { (TrackerModel.ITEMS.WOODARROW, 1), (TrackerModel.ITEMS.SILVERARROW, 2) }),
                            7 => ((() => TrackerModel.playerComputedStateSummary.CandleLevel), new[] { (TrackerModel.ITEMS.BLUECANDLE, 1), (TrackerModel.ITEMS.REDCANDLE, 2) }),
                            8 => ((() => TrackerModel.playerComputedStateSummary.RingLevel), new[] { (TrackerModel.ITEMS.BLUERING, 1), (TrackerModel.ITEMS.REDRING, 2) }),
                            11 => ((() => TrackerModel.playerComputedStateSummary.BoomerangLevel), new[] { (TrackerModel.ITEMS.BOOMERANG, 1), (TrackerModel.ITEMS.MAGICBOOMERANG, 2) }),
                            _ => null,
                        };
                    // on/off items: turning one off must also un-collect it from dungeon boxes and map icons, or it stays lit in the bar
                    (Func<bool>, int)? flipSources =
                        slot switch
                        {
                            2 => ((() => TrackerModel.playerComputedStateSummary.HaveBow), TrackerModel.ITEMS.BOW),
                            3 => ((() => TrackerModel.playerComputedStateSummary.HaveLadder), TrackerModel.ITEMS.LADDER),
                            4 => ((() => TrackerModel.playerComputedStateSummary.HavePowerBracelet), TrackerModel.ITEMS.POWERBRACELET),
                            5 => ((() => TrackerModel.playerComputedStateSummary.HaveRecorder), TrackerModel.ITEMS.RECORDER),
                            6 => ((() => TrackerModel.playerComputedStateSummary.HaveRaft), TrackerModel.ITEMS.RAFT),
                            9 => ((() => TrackerModel.playerComputedStateSummary.HaveWand), TrackerModel.ITEMS.WAND),
                            10 => ((() => TrackerModel.playerComputedStateSummary.HaveAnyKey), TrackerModel.ITEMS.KEY),
                            _ => null,
                        };
                    var hadBefore = flipSources is (Func<bool> f0, int _) ? f0() : false;
                    var levelBefore = levelsOf is (Func<int> f1, _) ? f1() : 0;
                    stepItem(slot, dir);
                    if (levelsOf is (Func<int> getLevel, (int, int)[] idsAndLevels))
                    {
                        TrackerModel.recomputePlayerStateSummary();
                        var expected = ((levelBefore + dir) % 3 + 3) % 3;
                        if (getLevel() > expected)
                        {
                            var toClear = idsAndLevels.Where(t => t.Item2 > expected).Select(t => t.Item1).ToList();
                            foreach (var b in TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes())
                                if (b.PlayerHas().Equals(TrackerModel.PlayerHas.YES) && toClear.Contains(b.CellCurrent()))
                                    b.SetPlayerHas(TrackerModel.PlayerHas.NO);
                            uncollectMapIcons(toClear);
                            TrackerModel.recomputePlayerStateSummary();
                        }
                    }
                    if (flipSources is (Func<bool> have, int ident) && hadBefore)
                    {
                        TrackerModel.recomputePlayerStateSummary();
                        if (have())
                        {
                            foreach (var b in TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes())
                                if (b.PlayerHas().Equals(TrackerModel.PlayerHas.YES) && b.CellCurrent() == ident)
                                    b.SetPlayerHas(TrackerModel.PlayerHas.NO);
                            uncollectMapIcons(new List<int> { ident });
                            TrackerModel.recomputePlayerStateSummary();
                        }
                    }
                    itemProgressChanged();
                    ea.Handled = true;
                }
            }
        };
        var onLeftClick = onItemClick(1);
        var onRightClick = onItemClick(-1);
        itemProgressCanvas.PointerPressed += (_, ea) =>
        {
            var kind = ea.GetCurrentPoint(itemProgressCanvas).Properties.PointerUpdateKind;
            if (kind == PointerUpdateKind.LeftButtonPressed) onLeftClick(ea);
            else if (kind == PointerUpdateKind.RightButtonPressed) onRightClick(ea);
        };
        return (redrawItemProgressBar, itemProgressCanvas, tb);
    }

    private static Border make_hintExplainer()
    {
        var img = Graphics.BMPtoImage(Graphics.z1rSampleHintBMP);
        var showHotKeysWidthToRightEdge = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H - THRU_MAIN_MAP_H + 115.0 + 20.0;  // +20 is a kludge to make this wider
        var w = showHotKeysWidthToRightEdge - 24.0;
        img.Height = img.Height / img.Width * w;
        img.Width = w;
        var sp = new StackPanel { Orientation = Orientation.Vertical };
        var tb = new TextBox { Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 20.0, Margin = new Thickness(3.0, 0.0, 0.0, 0.0) };
        tb.Text = "Z1R Hints Explained!";
        sp.Children.Add(tb);
        sp.Children.Add(new DockPanel { Height = 3.0, Background = Brushes.Orange, Margin = new Thickness(6.0) });
        tb = new TextBox { Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 12.0, Margin = new Thickness(3.0, 0.0, 0.0, 0.0) };
        tb.Text = "Each z1r hint takes the form\n\n(encoded dungeon/cave name)\n(map region)\n\nor\n\n" +
                    "(encoded dungeon/cave name)\nwith (some item)\n\nUse the table at the left\nto decode the locations and\nto record any map regions\n\nSample Hint:";
        sp.Children.Add(new DockPanel { Height = 6.0 });
        sp.Children.Add(tb);
        sp.Children.Add(new Border { BorderBrush = Brushes.DarkSlateBlue, BorderThickness = new Thickness(3.0), Child = img, Width = showHotKeysWidthToRightEdge - 18.0, Margin = new Thickness(6.0) });
        var b = new Border { BorderBrush = Brushes.White, BorderThickness = new Thickness(3.0), Background = Brushes.Black, Child = sp, Width = showHotKeysWidthToRightEdge };
        b.PointerPressed += (_, ea) => ea.Handled = true;
        return b;
    }
    public static readonly Border hintExplainer = make_hintExplainer();
    public static Button MakeHintDecoderUI(CustomComboBoxes.CanvasManager cm)
    {
        var (HINTGRID_W, HINTGRID_H) = (180.0, 36.0);
        var hintGrid = makeGrid(3, OverworldData.hintMeanings.Length, (int)HINTGRID_W, (int)HINTGRID_H);
        var row = 0;
        var updateViewFunctions = Enumerable.Repeat<Action>(() => { }, 11).ToArray();
        TextBox mkTxt(string text) =>
            new TextBox { FontSize = 16.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false,
                        Width = HINTGRID_W - 6.0 - 4.0, Height = HINTGRID_H - 6.0 - 4.0, BorderThickness = new Thickness(0.0), VerticalAlignment = VerticalAlignment.Center, Text = text };
        foreach (var hm in OverworldData.hintMeanings)
        {
            var (a, b_) = (hm.Item1, hm.Item2);
            var thisRow = row;
            gridAdd(hintGrid, new TextBox { FontSize = 16.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(1.0), Text = a }, 0, row);
            var tb = new TextBox { FontSize = 16.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(1.0), Text = b_ };
            var dp = new DockPanel { LastChildFill = true };
            var bmp =
                row < 8 ?
                    Graphics.emptyUnfoundNumberedTriforce_bmps[row]
                : row == 8 ?
                    Graphics.unfoundL9_bmp
                : row == 9 ?
                    Graphics.ow_sword2_item_bmp
                :
                    Graphics.ow_sword3_item_bmp;
            var image = Graphics.BMPtoImage(bmp);
            image.Width = 32.0;
            image.Stretch = Stretch.None;
            var b = new Border { Child = image, BorderThickness = new Thickness(1.0), BorderBrush = Brushes.LightGray, Background = Brushes.Black };
            DockPanel.SetDock(b, Dock.Left);
            dp.Children.Add(b);
            dp.Children.Add(tb);
            gridAdd(hintGrid, dp, 1, row);
            var button = new Button();
            var border = new Border { Child = button, Padding = new Thickness(3.0), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1.0) };
            gridAdd(hintGrid, border, 2, row);
            void updateView()
            {
                var hintZone = TrackerModel.GetLevelHint.Invoke(thisRow);
                if (hintZone.ToIndex() == 0)
                    b.Background = Brushes.Black;
                else
                    b.Background = Views.hintHighlightBrush;
                TextBox make_tb()
                {
                    if (hintZone.Equals(TrackerModel.HintZone.UNKNOWN))
                    {
                        var tb5 = mkTxt("Click to select");
                        tb5.Foreground = Brushes.DeepSkyBlue;
                        return tb5;
                    }
                    else
                        return mkTxt(hintZone.ToString());
                }
                var tb2 = make_tb();
                tb2.Background = Brushes.Transparent;
                var dp2 = new DockPanel { LastChildFill = true, Background = Graphics.almostBlack };
                dp2.PointerEntered += (_, _) => dp2.Background = Graphics.almostBlackHoverFeedback;
                dp2.PointerExited += (_, _) => dp2.Background = Graphics.almostBlack;
                var chevron = new TextBox { FontSize = 16.0, Foreground = Brushes.Gray, Background = Brushes.Transparent, IsReadOnly = true, IsHitTestVisible = false,
                                            BorderThickness = new Thickness(0.0), VerticalAlignment = VerticalAlignment.Center, Text = "\U000025BC" };
                dp2.Children.Add(chevron);
                DockPanel.SetDock(chevron, Dock.Right);
                dp2.Children.Add(tb2);
                button.Content = dp2;
            }
            updateViewFunctions[thisRow] = updateView;
            var popupIsActive = false;  // second level of popup, need local copy
            void activatePopup(int activationDelta)
            {
                popupIsActive = true;
                var (tileX, tileY) = ((Func<(double, double)>)(() => { var p = border.TranslatePoint(new Point(), cm.AppMainCanvas).Value; return (p.X + 3.0, p.Y + 3.0); }))();
                var tileCanvas = new Canvas { Width = HINTGRID_W - 6.0, Height = HINTGRID_H - 6.0, Background = Brushes.Black };
                void redrawTile(int i)
                {
                    tileCanvas.Children.Clear();
                    canvasAdd(tileCanvas, mkTxt(TrackerModel.HintZone.FromIndex(i).ToString()), 3.0, 3.0);
                }
                (Control, bool, int)[] make_gridElementsSelectablesAndIDs()
                {
                    var raw = Enumerable.Range(0, 11).Select(i => ((Control)mkTxt(TrackerModel.HintZone.FromIndex(i).ToString()), true, i)).ToArray();
                    if (TrackerModelOptions.AlphabetizeHintZones.Value)
                    {
                        // keep UNKNOWN (index 0) first, alphabetize the rest
                        // (F#'s string comparison is ordinal)
                        var rest = raw[1..].OrderBy(t => TrackerModel.HintZone.FromIndex(t.Item3).ToString(), StringComparer.Ordinal).ToArray();
                        return new[] { raw[0] }.Concat(rest).ToArray();
                    }
                    else
                        return raw;
                }
                var gridElementsSelectablesAndIDs = make_gridElementsSelectablesAndIDs();
                var currentHintIndex = TrackerModel.GetLevelHint.Invoke(thisRow).ToIndex();
                var originalStateIndex = Array.FindIndex(gridElementsSelectablesAndIDs, t => t.Item3 == currentHintIndex);
                if (originalStateIndex < 0) throw new KeyNotFoundException();   // (F#'s Array.findIndex throws when nothing matches)
                var (gnc, gnr, gcw, grh) = (1, 11, (int)HINTGRID_W - 6, (int)HINTGRID_H - 6);
                var (gx, gy) = (HINTGRID_W - 3.0, -HINTGRID_H * (double)(thisRow) - 9.0);
                CustomComboBoxes.PopupClickBehavior<int> onClick(PointerPressedEventArgs _ea, int i) => new CustomComboBoxes.PopupClickBehavior<int>.DismissPopupWithResult(i);
                var extraDecorations = new List<(Control, double, double)>();
                var brushes = CustomComboBoxes.ModalGridSelectBrushes.Defaults();
                async Task go()
                {
                    var r = await CustomComboBoxes.DoModalGridSelect<int, int>(cm, tileX, tileY, tileCanvas, gridElementsSelectablesAndIDs, originalStateIndex, activationDelta, (gnc, gnr, gcw, grh),
                                                    (double)gcw / 2.0, (double)grh / 2.0, gx, gy, redrawTile, onClick, extraDecorations, brushes, new CustomComboBoxes.GridClickDismissalWarpReturn.NoWarp(), null, "HintDecoder", null);
                    if (r.hasResult)
                    {
                        var i = r.result;
                        TrackerModel.SetLevelHint.Invoke(Tuple.Create(thisRow, TrackerModel.HintZone.FromIndex(i)));
                        TrackerModel.forceUpdate();
                        updateView();
                    }
                    popupIsActive = false;
                }
                CustomComboBoxes.StartImmediate(go());
            }
            button.Click += (_, _) => { if (!popupIsActive) activatePopup(0); };
            button.PointerWheelChanged += (sender, x) =>
            {
                var notch = Graphics.wheelNotch(sender, x);
                if (notch == 0) return;
                if (!popupIsActive) activatePopup(notch > 0 ? -1 : 1);
            };
            row = row + 1;
        }
        var hintDescriptionTextBox =
            new TextBox { FontSize = 16.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0, 0.0, 0.0, 4.0),
                        Text = "Each hinted-but-not-yet-found location will cause a 'halo' to appear on\n" +
                             "the triforce/sword icon in the upper portion of the tracker, and hovering the\n" +
                             "halo will show the possible locations for that dungeon or sword cave." };
        var hintSP = new StackPanel { Orientation = Orientation.Vertical };
        hintSP.Children.Add(hintDescriptionTextBox);
        hintSP.Children.Add(hintGrid);
        TextBox makeHintText(string txt) => new TextBox { FontSize = 16.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, Text = txt };
        // 'Other hints' section, shown underneath the hint decoder grid (rebuilt on each open so the checkboxes show current state)
        Control makeOtherHintsSection()
        {
            var otherSP = new StackPanel { Orientation = Orientation.Vertical };
            var otherTopTB = makeHintText("Here are the meanings of some hints, which you need to track on your own:");
            otherTopTB.BorderThickness = new Thickness(0.0, 0.0, 0.0, 4.0);
            otherSP.Children.Add(otherTopTB);
            foreach (var (desc, mean) in
                new[] {
                ("A feat of strength will lead to...", "Either push a gravestone, or push\nan overworld rock requiring Power Bracelet"),
                ("Sail across the water...", "Raft required to reach a place"),
                ("Play a melody...", "Either an overworld recorder spot, or a\nDigdogger in a dungeon logically blocks..."),
                ("Fire the arrow...", "In a dungeon, Gohma logically blocks..."),
                ("Step over the water...", "Ladder required to obtain... (coast item,\noverworld river, or dungeon moat)"),
                })
            {
                var dp = new DockPanel { LastChildFill = true };
                var d = makeHintText(desc);
                d.Width = 240.0;
                dp.Children.Add(d);
                var m = makeHintText(mean);
                DockPanel.SetDock(m, Dock.Right);
                dp.Children.Add(m);
                otherSP.Children.Add(dp);
            }
            var otherBottomTB = makeHintText("\nHere are the meanings of a couple final hints, which the tracker can help with\nby darkening the overworld spots you can logically ignore\n(click the checkbox to darken corresponding spots on the overworld)");
            otherBottomTB.BorderThickness = new Thickness(0.0, 4.0, 0.0, 4.0);
            otherSP.Children.Add(otherBottomTB);
            var featsCheckBox = new CheckBox { Content = makeHintText("No feat of strength... (Power Bracelet / pushing graves not required)") };
            featsCheckBox.IsChecked = TrackerModel.NoFeatOfStrengthHintWasGiven;
            Graphics.onChecked(featsCheckBox, () => { TrackerModel.NoFeatOfStrengthHintWasGiven = true; hideFeatsOfStrength(true); itemProgressChanged(); Views.triggerHintDashedItemsChanged(); });
            Graphics.onUnchecked(featsCheckBox, () => { TrackerModel.NoFeatOfStrengthHintWasGiven = false; hideFeatsOfStrength(false); itemProgressChanged(); Views.triggerHintDashedItemsChanged(); });
            otherSP.Children.Add(featsCheckBox);
            var raftsCheckBox = new CheckBox { Content = makeHintText("Sail not... (Raft not required)") };
            raftsCheckBox.IsChecked = TrackerModel.SailNotHintWasGiven;
            Graphics.onChecked(raftsCheckBox, () => { TrackerModel.SailNotHintWasGiven = true; hideRaftSpots(true); itemProgressChanged(); Views.triggerHintDashedItemsChanged(); });
            Graphics.onUnchecked(raftsCheckBox, () => { TrackerModel.SailNotHintWasGiven = false; hideRaftSpots(false); itemProgressChanged(); Views.triggerHintDashedItemsChanged(); });
            otherSP.Children.Add(raftsCheckBox);
            var sailToCheckBox = new CheckBox { Content = makeHintText("Sail across the water... (Raft required to reach a place)") };
            sailToCheckBox.IsChecked = TrackerModel.SailToHintWasGiven;
            Graphics.onChecked(sailToCheckBox, () => TrackerModel.SailToHintWasGiven = true);
            Graphics.onUnchecked(sailToCheckBox, () => TrackerModel.SailToHintWasGiven = false);
            otherSP.Children.Add(sailToCheckBox);
            var melodyCheckBox = new CheckBox { Content = makeHintText("Play a melody... (Recorder required)") };
            melodyCheckBox.IsChecked = TrackerModel.PlayMelodyHintWasGiven;
            Graphics.onChecked(melodyCheckBox, () => TrackerModel.PlayMelodyHintWasGiven = true);
            Graphics.onUnchecked(melodyCheckBox, () => TrackerModel.PlayMelodyHintWasGiven = false);
            otherSP.Children.Add(melodyCheckBox);
            var stepOverCheckBox = new CheckBox { Content = makeHintText("Step over the water... (Ladder required)") };
            stepOverCheckBox.IsChecked = TrackerModel.StepOverWaterHintWasGiven;
            Graphics.onChecked(stepOverCheckBox, () => TrackerModel.StepOverWaterHintWasGiven = true);
            Graphics.onUnchecked(stepOverCheckBox, () => TrackerModel.StepOverWaterHintWasGiven = false);
            otherSP.Children.Add(stepOverCheckBox);
            var fireArrowsCheckBox = new CheckBox { Content = makeHintText("Fire the arrow... (Bow and arrow required for Gohma)") };
            fireArrowsCheckBox.IsChecked = TrackerModel.FireArrowsHintWasGiven;
            Graphics.onChecked(fireArrowsCheckBox, () => TrackerModel.FireArrowsHintWasGiven = true);
            Graphics.onUnchecked(fireArrowsCheckBox, () => TrackerModel.FireArrowsHintWasGiven = false);
            otherSP.Children.Add(fireArrowsCheckBox);
            // (WPF: an implicit CheckBox style in otherSP.Resources; Avalonia: a style in otherSP.Styles, which applies to its descendants)
            var hintCbStyle = new Style(x => x.OfType<CheckBox>());
            hintCbStyle.Setters.Add(new Setter(TemplatedControl.TemplateProperty, Graphics.roundedCheckBoxTemplate));
            hintCbStyle.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(4.0, 0.0, 0.0, 0.0)));
            otherSP.Styles.Add(hintCbStyle);
            return otherSP;
        }
        Control otherSection = null;   // (F#: a `UIElement ref`)
        var hintBorder = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(8.0), Background = Brushes.Black, Child = hintSP };
        hintBorder.PointerPressed += (_, ea) => ea.Handled = true;
        var tb6 = Graphics.makeButton("Hint Decoder", 12.0, Brushes.Orange);
        tb6.PointerEntered += (_, _) => showHintShopLocator();
        tb6.PointerExited += (_, _) => hideLocator();
        tb6.Click += (_, _) =>
        {
            if (!popupIsActive)
            {
                popupIsActive = true;
                for (int i = 0; i <= 10; i++)
                    updateViewFunctions[i]();
                if (otherSection != null) hintSP.Children.Remove(otherSection);
                otherSection = makeOtherHintsSection();
                hintSP.Children.Add(otherSection);
                var wh = new ManualResetEvent(false);
                async Task go()
                {
                    var all = new Canvas();
                    canvasAdd(all, hintBorder, 0.0, 0.0);
                    canvasAdd(all, hintExplainer, 560.0, 0.0);
                    await CustomComboBoxes.DoModal(cm, wh, 0.0, 5.0, all);
                    all.Children.Clear();
                    popupIsActive = false;
                }
                CustomComboBoxes.StartImmediate(go());
            }
        };
        return tb6;
    }

    // (WPF: `open HotKeys.MyKey` again here; nothing to translate)

    // highlights every overworld tile a recorder / ladder / bomb blocker blocks (even ones already marked); false if this blocker isn't one of those
    public static bool showBlockedTilesLocator(TrackerModel.DungeonBlocker db)
    {
        var inst = TrackerModel.owInstance;
        switch (db.HardCanonical().Tag)
        {
            case TrackerModel.DungeonBlocker.Tags.RECORDER: showLocatorInstanceFuncAll((x, y) => inst.Whistleable(x, y) && !(inst.Ladderable(x, y))); return true;
            case TrackerModel.DungeonBlocker.Tags.LADDER: showLocatorInstanceFuncAll(inst.Ladderable); return true;
            case TrackerModel.DungeonBlocker.Tags.BOMB: showLocatorInstanceFuncAll((x, y) => inst.Bombable(x, y) && !(inst.Ladderable(x, y))); return true;
            default: return false;
        }
    }
    // (blockerQueries: LinkRouting's list, each query returning (shop item, (x, y)) or null for None. levelTabSelected/blockersHoverEvent are
    // Graphics.SharedEvents; blockerDungeonSunglasses is filled in here; contentCanvasMouseEnterFunc/LeaveFunc take a level number.)
    public static Grid MakeBlockers(CustomComboBoxes.CanvasManager cm, List<Func<(int, (double, double))?>> blockerQueries, Graphics.SharedEvent<int> levelTabSelected, Graphics.SharedEvent<bool> blockersHoverEvent, Control[] blockerDungeonSunglasses,
                            Action<int> contentCanvasMouseEnterFunc, Action<int> contentCanvasMouseLeaveFunc)
    {
        // blockers
        var blockerBoxes = new Canvas[8, TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON];
        Canvas makeBlockerBox(int dungeonIndex, int blockerIndex)
        {
            var c = Views.MakeBlockerView(dungeonIndex, blockerIndex);
            var current = TrackerModel.DungeonBlockersContainer.GetDungeonBlocker(dungeonIndex, blockerIndex);
            onEvent(TrackerModel.DungeonBlockersContainer.AnyBlockerChanged, _ =>
            {
                current = TrackerModel.DungeonBlockersContainer.GetDungeonBlocker(dungeonIndex, blockerIndex);
            });
            // hovering a blocker box with a sellable item will highlight the corresponding shops (currently only way to mouse-hover to see key/meat shops)
            c.PointerEntered += (_, _) =>
            {
                switch (current.HardCanonical().Tag)
                {
                    case TrackerModel.DungeonBlocker.Tags.BAIT:           showShopLocatorInstanceFunc(TrackerModel.MapSquareChoiceDomainHelper.MEAT); break;
                    case TrackerModel.DungeonBlocker.Tags.KEY:            showShopLocatorInstanceFunc(TrackerModel.MapSquareChoiceDomainHelper.KEY); break;
                    case TrackerModel.DungeonBlocker.Tags.BOMB:
                    case TrackerModel.DungeonBlocker.Tags.RECORDER:
                    case TrackerModel.DungeonBlocker.Tags.LADDER: showBlockedTilesLocator(current); break;   // the overworld tiles this blocks
                    case TrackerModel.DungeonBlocker.Tags.BOW_AND_ARROW:  showShopLocatorInstanceFunc(TrackerModel.MapSquareChoiceDomainHelper.ARROW); break;
                    case TrackerModel.DungeonBlocker.Tags.MONEY:          showLocatorRupees(); break;
                    default: contentCanvasMouseEnterFunc(dungeonIndex + 1); break;   // if nothing specific for this box to show, default to parent's hover behavior
                }
            };
            c.PointerExited += (_, _) => hideLocator();
            Views.appMainCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(c);
            blockerQueries.Add(() =>
            {
                var pos = c.TranslatePoint(new Point(), cm.AppMainCanvas).Value;
                switch (current.HardCanonical().Tag)
                {
                    case TrackerModel.DungeonBlocker.Tags.BAIT:           return (TrackerModel.MapSquareChoiceDomainHelper.MEAT, (pos.X, pos.Y));
                    case TrackerModel.DungeonBlocker.Tags.KEY:            return (TrackerModel.MapSquareChoiceDomainHelper.KEY, (pos.X, pos.Y));
                    case TrackerModel.DungeonBlocker.Tags.BOMB:           return (TrackerModel.MapSquareChoiceDomainHelper.BOMB, (pos.X, pos.Y));
                    case TrackerModel.DungeonBlocker.Tags.BOW_AND_ARROW:  return (TrackerModel.MapSquareChoiceDomainHelper.ARROW, (pos.X, pos.Y));
                    default: return null;
                }
            });
            void SetNewValue(TrackerModel.DungeonBlocker db) =>
                TrackerModel.DungeonBlockersContainer.SetDungeonBlocker(dungeonIndex, blockerIndex, db);   // no collection logic: any blocker may be selected
            void activate(int activationDelta)
            {
                popupIsActive = true;
                var (pc, predraw) = Views.MakeBlockerCore();
                void popupRedraw(TrackerModel.DungeonBlocker n)
                {
                    var innerc = predraw(n);
                    // hovering a recorder / ladder / bomb blocker highlights every overworld tile it blocks (including ones already marked)
                    if (!(showBlockedTilesLocator(n))) hideLocator();
                    var s = HotKeys.BlockerHotKeyProcessor.AppendHotKeyToDescription(n.DisplayDescription(), n);
                    var text = new TextBox { Text = s, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0),
                                                FontSize = 16.0, HorizontalContentAlignment = HorizontalAlignment.Center };
                    var textBorder = new Border { BorderThickness = new Thickness(3.0), Child = text, Background = Brushes.Black, BorderBrush = Brushes.Gray };
                    var dp = new DockPanel { LastChildFill = false };
                    DockPanel.SetDock(textBorder, Dock.Right);
                    dp.Children.Add(textBorder);
                    Canvas.SetTop(dp, 30.0);
                    Canvas.SetRight(dp, 120.0);
                    innerc.Children.Add(dp);
                }
                var pos = c.TranslatePoint(new Point(), cm.AppMainCanvas).Value;
                async Task go()
                {
                    var r = await CustomComboBoxes.DoModalGridSelect<TrackerModel.DungeonBlocker, TrackerModel.DungeonBlocker>(cm, pos.X, pos.Y, pc, TrackerModel.DungeonBlocker.All.Select(db =>
                                    ((db.Equals(TrackerModel.DungeonBlocker.NOTHING) ? (Control)new Canvas() : (Control)Graphics.blockerCurrentDisplay(db)), true, db)).ToArray(),
                                    Array.IndexOf(TrackerModel.DungeonBlocker.All, current), activationDelta, (4, 4, 24, 24), 12.0, 12.0, -90.0, 30.0, popupRedraw,
                                    (_ea, db) => new CustomComboBoxes.PopupClickBehavior<TrackerModel.DungeonBlocker>.DismissPopupWithResult(db), new List<(Control, double, double)>(), CustomComboBoxes.ModalGridSelectBrushes.Defaults(), new CustomComboBoxes.GridClickDismissalWarpReturn.WarpToCenter(), null, "Blocker", null);
                    if (r.hasResult) SetNewValue(r.result);
                    hideLocator();
                    popupIsActive = false;
                }
                CustomComboBoxes.StartImmediate(go());
            }
            void doPanel(Point pos)
            {
                popupIsActive = true;
                var border = new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(3.0), Background = Brushes.Black, Width = 110.0 };
                // (WPF: implicit styles keyed by type in border.Resources; Avalonia: styles in the border's Styles, which apply to its descendants)
                var style = new Style(x => x.OfType<TextBox>());
                style.Setters.Add(new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0.0)));
                style.Setters.Add(new Setter(TemplatedControl.FontSizeProperty, 16.0));
                style.Setters.Add(new Setter(TemplatedControl.ForegroundProperty, Brushes.Orange));
                style.Setters.Add(new Setter(TemplatedControl.BackgroundProperty, Brushes.Black));
                style.Setters.Add(new Setter(InputElement.IsHitTestVisibleProperty, false));
                style.Setters.Add(new Setter(TextBox.IsReadOnlyProperty, true));
                border.Styles.Add(style);
                var style2 = new Style(x => x.OfType<CheckBox>());
                style2.Setters.Add(new Setter(Layoutable.HeightProperty, 22.0));
                style2.Setters.Add(new Setter(TemplatedControl.TemplateProperty, Graphics.roundedCheckBoxTemplate));
                style2.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(4.0, 0.0, 0.0, 0.0)));
                border.Styles.Add(style2);
                var panel = new StackPanel { Orientation = Orientation.Vertical };
                var hPanel = new UniformGrid { Rows = 1, Columns = 2 };
                var allButton = Graphics.makeButton("All", 16.0, Brushes.Orange);
                var noneButton = Graphics.makeButton("None", 16.0, Brushes.Orange);
                hPanel.Children.Add(allButton);
                hPanel.Children.Add(noneButton);
                var decorationCanvas = new Canvas();
                canvasAdd(decorationCanvas, new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(3.0), Width = 30.0, Height = 30.0 }, 77.0, -33.0);
                panel.Children.Add(decorationCanvas);
                panel.Children.Add(new TextBox { Text = "Applies to..." });
                panel.Children.Add(hPanel);
                var cbs = new List<CheckBox>();
                foreach (var (name, i) in new[] { ("Map", 0), ("Compass", 1), ("Triforce", 2), ("Item Box 1", 3), ("Item Box 2", 4), ("Item Box 3", 5) })
                {
                    if (i < 5 || TrackerModel.GetDungeon(dungeonIndex).Boxes.Length > 2)
                    {
                        var cb = new CheckBox { Content = new TextBox { Text = name } };
                        cb.IsChecked = TrackerModel.DungeonBlockersContainer.GetDungeonBlockerAppliesTo(dungeonIndex, blockerIndex, i);
                        Graphics.onChecked(cb, () => TrackerModel.DungeonBlockersContainer.SetDungeonBlockerAppliesTo(dungeonIndex, blockerIndex, i, true));
                        Graphics.onUnchecked(cb, () => TrackerModel.DungeonBlockersContainer.SetDungeonBlockerAppliesTo(dungeonIndex, blockerIndex, i, false));
                        panel.Children.Add(cb);
                        cbs.Add(cb);
                    }
                }
                allButton.Click += (_, _) => { foreach (var cb in cbs) cb.IsChecked = true; };
                noneButton.Click += (_, _) => { foreach (var cb in cbs) cb.IsChecked = false; };
                border.Child = panel;
                var wh = new ManualResetEvent(false);
                async Task go()
                {
                    await CustomComboBoxes.DoModal(cm, wh, pos.X - 80.0, pos.Y + 30.0, border);
                    popupIsActive = false;
                }
                CustomComboBoxes.StartImmediate(go());
            }
            c.PointerWheelChanged += (sender, x) =>
            {
                var notch = Graphics.wheelNotch(sender, x);
                if (notch == 0) return;
                if (!popupIsActive) activate(notch < 0 ? 1 : -1);
            };
            c.PointerPressed += (_, ea) =>
            {
                if (!popupIsActive)
                {
                    var kind = ea.GetCurrentPoint(c).Properties.PointerUpdateKind;
                    if (kind == PointerUpdateKind.MiddleButtonPressed ||
                            (kind == PointerUpdateKind.LeftButtonPressed && ea.KeyModifiers.HasFlag(KeyModifiers.Shift)))   // (WPF: Keyboard.IsKeyDown(LeftShift/RightShift))
                    {
                        // middle-click or shift-left-click activates the checkbox panel
                        var pos = c.TranslatePoint(new Point(), cm.AppMainCanvas).Value;
                        doPanel(pos);
                    }
                    else
                        activate(0);
                }
            };
            c.MyKeyAdd(ea =>
            {
                if (!popupIsActive)
                {
                    if (HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key, out var target))
                    {
                        switch (target)
                        {
                            case HotKeys.GlobalHotkeyTargets.MoveCursorRight:
                                ea.Handled = true;
                                if (blockerIndex < TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON - 1)
                                    Graphics.NavigationallyWarpMouseCursorTo(blockerBoxes[dungeonIndex, blockerIndex + 1].TranslatePoint(new Point(15.0, 15.0), cm.AppMainCanvas).Value);
                                else if (dungeonIndex != 1 && dungeonIndex != 4 && dungeonIndex != 7)
                                    Graphics.NavigationallyWarpMouseCursorTo(blockerBoxes[dungeonIndex + 1, 0].TranslatePoint(new Point(15.0, 15.0), cm.AppMainCanvas).Value);
                                break;
                            case HotKeys.GlobalHotkeyTargets.MoveCursorLeft:
                                if (blockerIndex > 0)
                                {
                                    ea.Handled = true;
                                    Graphics.NavigationallyWarpMouseCursorTo(blockerBoxes[dungeonIndex, blockerIndex - 1].TranslatePoint(new Point(15.0, 15.0), cm.AppMainCanvas).Value);
                                }
                                else if (dungeonIndex != 0 && dungeonIndex != 2 && dungeonIndex != 5)
                                {
                                    ea.Handled = true;
                                    Graphics.NavigationallyWarpMouseCursorTo(blockerBoxes[dungeonIndex - 1, TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON - 1].TranslatePoint(new Point(15.0, 15.0), cm.AppMainCanvas).Value);
                                }
                                // else unhandled, so app global handler will nudge mouse left
                                break;
                            case HotKeys.GlobalHotkeyTargets.MoveCursorDown:
                                ea.Handled = true;
                                if (dungeonIndex < 5)
                                    Graphics.NavigationallyWarpMouseCursorTo(blockerBoxes[dungeonIndex + 3, blockerIndex].TranslatePoint(new Point(15.0, 15.0), cm.AppMainCanvas).Value);
                                break;
                            case HotKeys.GlobalHotkeyTargets.MoveCursorUp:
                                if (dungeonIndex > 2)
                                {
                                    ea.Handled = true;
                                    Graphics.NavigationallyWarpMouseCursorTo(blockerBoxes[dungeonIndex - 3, blockerIndex].TranslatePoint(new Point(15.0, 15.0), cm.AppMainCanvas).Value);
                                }
                                // else unhandled, so app global handler will nudge mouse up
                                break;
                            default: break;
                        }
                    }
                    if (HotKeys.BlockerHotKeyProcessor.TryGetValue(ea.Key, out var db))
                    {
                        ea.Handled = true;
                        if (current.Equals(db))
                            SetNewValue(TrackerModel.DungeonBlocker.NOTHING);    // idempotent hotkeys behave as a toggle
                        else
                            SetNewValue(db);
                    }
                }
            });
            blockerBoxes[dungeonIndex, blockerIndex] = c;
            return c;
        }

        //let blockerColumnWidth = int((cm.AppMainCanvas.Width-BLOCKERS_AND_NOTES_OFFSET)/3.)   // would be 106
        var blockerColumnWidth = 108; // multiple of 9, to make 2/3 size avoid looking blurry
        var blockerGrid = makeGrid(3, 3, blockerColumnWidth, 36);
        var blockerHighlightBrush = Graphics.freeze(new SolidColorBrush(Color.FromRgb(50, 70, 50)));
        blockerGrid.Height = (double)(36 * 3);
        for (int i = 0; i <= 2; i++)
        {
            for (int j = 0; j <= 2; j++)
            {
                if (i == 0 && j == 0)
                {
                    var d = new DockPanel { LastChildFill = false, Background = Brushes.Black };
                    var tb = new TextBox { Foreground = Brushes.Orange, Background = Brushes.Black, FontSize = 12.0, Text = "BLOCKERS", Width = (double)blockerColumnWidth, IsHitTestVisible = false,
                                            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0), TextAlignment = TextAlignment.Center };
                    ToolTip.SetTip(d, "The icons you set in this area can remind you of what blocked you in a dungeon.\nFor example, a ladder represents being ladder blocked, or a sword means you need better weapons.\nSome reminders will trigger when you get the item that may unblock you.");
                    ToolTip.SetPlacement(d, PlacementMode.Top);
                    d.Children.Add(tb);
                    var cooldownTimer = new DispatcherTimer(DispatcherPriority.Normal);
                    cooldownTimer.Interval = TimeSpan.FromMilliseconds(1000.0);
                    cooldownTimer.Tick += (_, _) =>
                    {
                        blockersHoverEvent.Trigger(true);
                    };
                    d.PointerEntered += (_, _) =>
                    {
                        cooldownTimer.Start();
                    };
                    d.PointerExited += (_, _) =>
                    {
                        cooldownTimer.Stop();
                        blockersHoverEvent.Trigger(false);
                    };
                    gridAdd(blockerGrid, d, i, j);
                }
                else
                {
                    var dungeonIndex = (3 * j + i) - 1;
                    var labelChar = TrackerModel.IsHiddenDungeonNumbers.Invoke(null) ? "ABCDEFGH"[dungeonIndex] : "12345678"[dungeonIndex];
                    var sp = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Black };
                    levelTabSelected.Fired += level => { if (level == dungeonIndex + 1) sp.Background = blockerHighlightBrush; else sp.Background = Brushes.Black; };
                    var tb = new TextBox { Foreground = Brushes.Orange, Background = Brushes.Black, FontSize = 12.0, Text = labelChar.ToString(), Width = 10.0, Height = 14.0, IsHitTestVisible = false,
                                            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0),
                                            TextAlignment = TextAlignment.Center, Margin = new Thickness(2.0, 0.0, 0.0, 0.0) };
                    void updateLabelColor() => tb.Foreground = TrackerModel.GetDungeon(dungeonIndex).HasBeenLocated() ? Brushes.White : Brushes.Orange;
                    onEvent(TrackerModel.GetDungeon(dungeonIndex).HasBeenLocatedChanged, _ => updateLabelColor());
                    sp.Children.Add(tb);
                    for (int k = 0; k <= TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON - 1; k++)   // (F#: `for i`, shadowing the outer i)
                        sp.Children.Add(makeBlockerBox(dungeonIndex, k));
                    gridAdd(blockerGrid, sp, i, j);
                    sp.PointerEntered += (_, _) => contentCanvasMouseEnterFunc(dungeonIndex + 1);
                    sp.PointerExited += (_, _) => contentCanvasMouseLeaveFunc(dungeonIndex + 1);
                    blockerDungeonSunglasses[dungeonIndex] = sp; // just reduce its opacity
                }
            }
        }
        return blockerGrid;
    }

    // (returns zone_checkbox, addZoneName (F#: a function of an (hz, name, x, y) tuple), changeZoneOpacity (F#: of a (hintZone, show) tuple), allOwMapZoneBlackCanvases)
    public static (CheckBox, Action<TrackerModel.HintZone, string, double, double>, Action<TrackerModel.HintZone, bool>, Canvas[,]) MakeZoneOverlay(Canvas overworldCanvas, Action ensurePlaceholderFinished, List<Control> mirrorOverworldFEs)
    {
        // zone overlay
        (Canvas[,], Canvas[,]) make_owMapZoneCanvases()
        {
            System.Drawing.Color avg(System.Drawing.Color c1, System.Drawing.Color c2) => System.Drawing.Color.FromArgb(((int)c1.R + (int)c2.R) / 2, ((int)c1.G + (int)c2.G) / 2, ((int)c1.B + (int)c2.B) / 2);
            SolidColorBrush toBrush(System.Drawing.Color c) => Graphics.freeze(new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)));
            var colors =
                new Dictionary<char, SolidColorBrush> {
                    { 'M', toBrush(avg(System.Drawing.Color.Pink, System.Drawing.Color.Crimson)) },
                    { 'L', toBrush(System.Drawing.Color.BlueViolet) },
                    { 'R', toBrush(System.Drawing.Color.LightSeaGreen) },
                    { 'H', toBrush(System.Drawing.Color.Gray) },
                    { 'C', toBrush(System.Drawing.Color.LightBlue) },
                    { 'G', toBrush(avg(System.Drawing.Color.LightSteelBlue, System.Drawing.Color.SteelBlue)) },
                    { 'D', toBrush(System.Drawing.Color.Orange) },
                    { 'F', toBrush(System.Drawing.Color.LightGreen) },
                    { 'S', toBrush(System.Drawing.Color.DarkGray) },
                    { 'W', toBrush(System.Drawing.Color.Brown) },
                };
            var (imgs, darks) = (new Canvas[16, 8], new Canvas[16, 8]);
            for (int x = 0; x <= 15; x++)
            {
                for (int y = 0; y <= 7; y++)
                {
                    imgs[x, y] = new Canvas { Width = OMTW, Height = (double)(11 * 3), Background = colors[OverworldData.owMapZone[y][x]], IsHitTestVisible = false };
                    darks[x, y] = new Canvas { Width = OMTW, Height = (double)(11 * 3), Background = Brushes.Black, IsHitTestVisible = false };
                }
            }
            return (imgs, darks);
        }
        var (owMapZoneColorCanvases, owMapZoneBlackCanvases) = make_owMapZoneCanvases();
        var owMapZoneGrid = makeGrid(16, 8, (int)OMTW, 11 * 3);
        var (allOwMapZoneColorCanvases, allOwMapZoneBlackCanvases) = (new Canvas[16, 8], new Canvas[16, 8]);
        for (int i = 0; i <= 15; i++)
        {
            for (int j = 0; j <= 7; j++)
            {
                var (zcc, zbc) = (owMapZoneColorCanvases[i, j], owMapZoneBlackCanvases[i, j]);
                zcc.Opacity = 0.0;
                zbc.Opacity = 0.0;
                allOwMapZoneColorCanvases[i, j] = zcc;
                allOwMapZoneBlackCanvases[i, j] = zbc;
                gridAdd(owMapZoneGrid, zcc, i, j);
                gridAdd(owMapZoneGrid, zbc, i, j);
            }
        }
        canvasAdd(overworldCanvas, owMapZoneGrid, 0.0, 0.0);

        var owMapZoneBoundaries = new List<Line>();
        Line makeLine(int x1, int x2, int y1, int y2)
        {
            var line = new Line { StartPoint = new Point(OMTW * (double)x1, (double)(y1 * 11 * 3)), EndPoint = new Point(OMTW * (double)x2, (double)(y2 * 11 * 3)), Stroke = Brushes.White, StrokeThickness = 3.0 };
            line.IsHitTestVisible = false; // transparent to mouse
            return line;
        }
        void addLine(int x1, int x2, int y1, int y2)
        {
            var line = makeLine(x1, x2, y1, y2);
            line.Opacity = 0.0;
            owMapZoneBoundaries.Add(line);
            canvasAdd(overworldCanvas, line, 0.0, 0.0);
        }
        addLine(0, 7, 2, 2);
        addLine(7, 11, 1, 1);
        addLine(7, 7, 1, 2);
        addLine(10, 10, 0, 1);
        addLine(11, 11, 0, 2);
        addLine(8, 14, 2, 2);
        addLine(14, 14, 0, 2);
        addLine(6, 6, 2, 3);
        addLine(4, 4, 3, 4);
        addLine(2, 2, 4, 5);
        addLine(1, 1, 5, 7);
        addLine(0, 1, 7, 7);
        addLine(1, 4, 5, 5);
        addLine(2, 4, 4, 4);
        addLine(4, 6, 3, 3);
        addLine(4, 7, 6, 6);
        addLine(7, 12, 5, 5);
        addLine(9, 10, 4, 4);
        addLine(7, 10, 3, 3);
        addLine(7, 7, 2, 3);
        addLine(10, 10, 3, 4);
        addLine(9, 9, 4, 7);
        addLine(7, 7, 5, 6);
        addLine(4, 4, 5, 6);
        addLine(5, 5, 6, 8);
        addLine(6, 6, 6, 8);
        addLine(11, 11, 5, 8);
        addLine(9, 15, 7, 7);
        addLine(12, 12, 3, 5);
        addLine(13, 13, 2, 3);
        addLine(8, 8, 2, 3);
        addLine(12, 14, 3, 3);
        addLine(14, 15, 4, 4);
        addLine(15, 15, 4, 7);
        addLine(14, 14, 3, 4);

        var zoneNames = new List<(TrackerModel.HintZone, TextBox)>();  // added later, to be top of z-order
        void addZoneName(TrackerModel.HintZone hz, string name, double x, double y)
        {
            var tb = new TextBox { Text = name, FontSize = 16.0, Background = Brushes.Black, Foreground = Brushes.Orange, BorderThickness = new Thickness(2.0), IsReadOnly = true };
            mirrorOverworldFEs.Add(tb);
            canvasAdd(overworldCanvas, tb, x * OMTW, y * 11.0 * 3.0);
            tb.Opacity = 0.0;
            tb.TextAlignment = TextAlignment.Center;
            tb.FontWeight = FontWeight.Bold;
            tb.IsHitTestVisible = false;
            zoneNames.Add((hz, tb));
        }

        var isCurrentlyShown = false;
        void changeZoneOpacity(TrackerModel.HintZone hintZone, bool show)
        {
            ensurePlaceholderFinished();
            var noZone = hintZone.Equals(TrackerModel.HintZone.UNKNOWN);
            if (show)
            {
                isCurrentlyShown = true;
                if (noZone)
                    foreach (var zcc in allOwMapZoneColorCanvases) zcc.Opacity = 0.3;
                foreach (var x in owMapZoneBoundaries) x.Opacity = 0.9;
                foreach (var (hz, textbox) in zoneNames) if (noZone || hz.Equals(hintZone)) textbox.Opacity = 0.6;
            }
            else
            {
                if (isCurrentlyShown)   // performance - don't constantly re-do all this idempotent work
                {
                    foreach (var zcc in allOwMapZoneColorCanvases) zcc.Opacity = 0.0;
                    foreach (var x in owMapZoneBoundaries) x.Opacity = 0.0;
                    foreach (var (_hz, textbox) in zoneNames) textbox.Opacity = 0.0;
                    isCurrentlyShown = false;
                }
            }
        }
        var zone_checkbox = new CheckBox { Content = new TextBox { Text = "Zones", FontSize = 14.0, Background = Brushes.Black, Foreground = Brushes.Orange, BorderThickness = new Thickness(0.0), IsReadOnly = true, IsHitTestVisible = false } };
        zone_checkbox.IsChecked = TrackerModelOptions.Overworld.Zones.Value;
        Graphics.onChecked(zone_checkbox, () => { changeZoneOpacity(TrackerModel.HintZone.UNKNOWN, true); TrackerModelOptions.Overworld.Zones.Value = true; TrackerModelOptions.writeSettings(); });
        Graphics.onUnchecked(zone_checkbox, () => { changeZoneOpacity(TrackerModel.HintZone.UNKNOWN, false); TrackerModelOptions.Overworld.Zones.Value = false; TrackerModelOptions.writeSettings(); });

        // 'Hover on zones/coords': the overlay shows while the mouse is over the (unchecked) checkbox
        zone_checkbox.PointerEntered += (_, _) => { if (TrackerModelOptions.Overworld.HoverZonesCoords.Value && !(zone_checkbox.IsChecked == true)) changeZoneOpacity(TrackerModel.HintZone.UNKNOWN, true); };
        zone_checkbox.PointerExited += (_, _) => { if (TrackerModelOptions.Overworld.HoverZonesCoords.Value && !(zone_checkbox.IsChecked == true)) changeZoneOpacity(TrackerModel.HintZone.UNKNOWN, false); };
        return (zone_checkbox, addZoneName, changeZoneOpacity, allOwMapZoneBlackCanvases);
    }

    // (WPF: `open OverworldMapTileCustomization` here; see the `using` alias at the top of the file)

    public static (Button, Canvas) MakeMouseHoverExplainer(Canvas appMainCanvas)
    {
        var mouseHoverExplainerIcon = new Button { Content = Graphics.BMPtoImage(Graphics.greyscale(Graphics.question_marks_bmp)) };
        var c = new Canvas { Width = appMainCanvas.Width, Height = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H, Opacity = 0.0, IsHitTestVisible = false };
        var darkenTop = new Canvas { Width = OMTW * 16.0, Height = 150.0, Background = Brushes.Black, Opacity = 0.40 };
        canvasAdd(c, darkenTop, 0.0, 0.0);
        var darkenOW = new Canvas { Width = OMTW * 16.0, Height = 11.0 * 3.0 * 8.0, Background = Brushes.Black, Opacity = 0.85 };
        canvasAdd(c, darkenOW, 0.0, 150.0);
        var darkenBottom = new Canvas { Width = OMTW * 16.0, Height = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H - THRU_MAIN_MAP_H, Background = Brushes.Black, Opacity = 0.40 };
        canvasAdd(c, darkenBottom, 0.0, 150.0 + 11.0 * 3.0 * 8.0);

        // (large text placed by pixel, so its glyphs are moved to where WPF draws them; see Graphics.matchWpfTextTop)
        var descMHE = new TextBox { Text = "Mouse Hover Explainer", FontSize = 30.0, Background = Brushes.Transparent, Foreground = Brushes.Orange, BorderThickness = new Thickness(0.0), IsReadOnly = true };
        Graphics.matchWpfTextTop(descMHE);

        var delayedDescriptions = new List<(Canvas, TextBox, double, double)>();
        TextBox mkTxt(string text) => new TextBox { Text = text, FontSize = 14.0, Background = Brushes.Black, Foreground = Brushes.Orange, BorderThickness = new Thickness(1.0), IsReadOnly = true };
        void addLabel(Polyline poly, string text, double x, double y)
        {
            poly.Points.Add(new Point(x, y));
            canvasAdd(c, poly, 0.0, 0.0);
            delayedDescriptions.Add((c, mkTxt(text), x, y));
        }
        IEnumerable<(double, double)> offset(IEnumerable<(double, double)> xys, double dx, double dy) => xys.Select(p => (dx + p.Item1, dy + p.Item2));

        var ST = 2.0;
        IBrush COL = Brushes.Green;
        var triforces =
            TrackerModel.IsHiddenDungeonNumbers.Invoke(null) ?
                new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(new[] { (2.0, 58.0), (268.0, 58.0), (268.0, 32.0), (528.0, 32.0), (528.0, 2.0), (2.0, 2.0), (2.0, 58.0) }) }
            :
                new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(new[] { (2.0, 58.0), (268.0, 58.0), (268.0, 32.0), (2.0, 32.0), (2.0, 58.0) }) };
        addLabel(triforces, "Show location of dungeon, if known or hinted", 10.0, 300.0);

        COL = Brushes.MediumVioletRed;
        var (dx, dy) = (573.0, 0.0);
        var eyeball = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (2.0, 28.0), (28.0, 28.0), (28.0, 2.0), (2.0, 2.0), (2.0, 28.0) }, dx, dy)) };
        addLabel(eyeball, "Hide all overworld icons", 300.0, 4.0);

        (dx, dy) = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.WHITE_SWORD_ICON);
        var whiteSword = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (2.0, 28.0), (28.0, 28.0), (28.0, 2.0), (2.0, 2.0), (2.0, 28.0) }, dx, dy)) };
        addLabel(whiteSword, "Show location of white sword cave, if known or hinted", 30.0, 270.0);

        (dx, dy) = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.ZELDA_BOX);
        var shopping = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (28.0, 28.0), (28.0, 2.0), (2.0, 2.0), (2.0, 28.0), (28.0, 28.0) }, dx, dy)) };
        addLabel(shopping, "Show icons you opted to hide", 410.0, 150.0);

        COL = Brushes.CornflowerBlue;
        (dx, dy) = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.ARMOS_ICON);
        var armos = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (2.0, 28.0), (28.0, 28.0), (28.0, 2.0), (2.0, 2.0), (2.0, 28.0) }, dx, dy)) };
        addLabel(armos, "Show locations of any unmarked armos", 120.0, 240.0);

        (dx, dy) = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.WOOD_ARROW_BOX);
        shopping = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (2.0, 28.0), (88.0, 28.0), (88.0, -28.0), (32.0, -28.0), (32.0, 2.0), (2.0, 2.0), (2.0, 28.0) }, dx, dy)) };
        addLabel(shopping, "Show locations of shops containing each item (or blocker", 400.0, 240.0);
        (dx, dy) = (BLOCKERS_AND_NOTES_OFFSET + 72.0, START_DUNGEON_AND_NOTES_AREA_H);
        var blockers = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (10.0, 0.0), (-70.0, 0.0), (-70.0, 36.0), (36.0, 36.0), (36.0, 0.0), (10.0, 0.0) }, 210.0 + dx, dy)) };
        addLabel(blockers, ")", 757.0, 240.0);

        COL = Brushes.MediumVioletRed;
        (dx, dy) = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.BLUE_CANDLE_BOX);
        shopping = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (2.0, 28.0), (28.0, 28.0), (28.0, 2.0), (2.0, 2.0), (2.0, 28.0) }, dx, dy)) };
        addLabel(shopping, "If have no candle, show locations of shops with blue candle\nElse show unmarked burnable bush locations", 380.0, 270.0);

        COL = Brushes.Green;
        (dx, dy) = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.MAGS_BOX);
        var magsAndWoodSword = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (2.0, 28.0), (58.0, 28.0), (58.0, 2.0), (2.0, 2.0), (2.0, 28.0) }, dx, dy)) };
        addLabel(magsAndWoodSword, "Show locations of magical/wood sword caves, if known or hinted", 300.0, 210.0);

        (dx, dy) = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.HEARTS);
        var hearts = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (2.0, 28.0), (118.0, 28.0), (118.0, 2.0), (2.0, 2.0), (2.0, 28.0) }, dx, dy)) };
        hearts.Points.Add(new Point(270.0, 170.0));
        canvasAdd(c, hearts, 0.0, 0.0);
        var desc = mkTxt("Show locations of potion shops\nand un-taken Take Anys");
        desc.TextAlignment = TextAlignment.Right;
        Canvas.SetRight(desc, c.Width - 270.0);
        Canvas.SetTop(desc, 170.0);
        c.Children.Add(desc);

        COL = Brushes.CornflowerBlue;
        (dx, dy) = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.HEARTS);
        var maxhearts = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (124.0, 28.0), (124.0, 10.0), (227.0, 10.0), (227.0, 28.0), (124.0, 28.0) }, dx, dy)) };
        maxhearts.Points.Add(new Point(390.0, 160.0));
        canvasAdd(c, maxhearts, 0.0, 0.0);
        desc = mkTxt("Show inventory\n& max hearts");
        desc.TextAlignment = TextAlignment.Right;
        Canvas.SetRight(desc, c.Width - 390.0);
        Canvas.SetTop(desc, 160.0);
        c.Children.Add(desc);

        COL = Brushes.Green;
        var openCaves = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(new[] { (410.0, 115.0), (430.0, 115.0), (430.0, 96.0), (410.0, 96.0), (410.0, 115.0) }) };
        addLabel(openCaves, "Show locations of unmarked open caves", 430.0, 180.0);

        COL = Brushes.MediumVioletRed;
        var zonesEtAl = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(new[] { (550.0, 50.0), (475.0, 50.0), (475.0, 92.0), (406.0, 92.0), (406.0, 130.0), (505.0, 130.0), (505.0, 110.0), (550.0, 110.0), (550.0, 50.0) }) };
        addLabel(zonesEtAl, "As described", 630.0, 150.0);

        var spotSummary = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(new[] { (614.0, 115.0), (725.0, 115.0), (725.0, 90.0), (614.0, 90.0), (614.0, 115.0), (630.0, 150.0) }) };
        canvasAdd(c, spotSummary, 0.0, 0.0);

        COL = Brushes.Green;
        (dx, dy) = (60.0, THRU_MAIN_MAP_H + 3.0);
        var anyRoad = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (-2.0, 2.0), (-2.0, 25.0), (16.0, 25.0), (16.0, 2.0), (-2.0, 2.0) }, dx, dy)) };
        addLabel(anyRoad, "Show Any Roads", 40.0, 340.0);
        COL = Brushes.MediumVioletRed;
        (dx, dy) = (ITEM_PROGRESS_FIRST_ITEM + 25.0, THRU_MAP_AND_LEGEND_H + 3.0);
        var candle = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (2.0, 2.0), (2.0, 28.0), (28.0, 28.0), (28.0, 2.0), (2.0, 2.0) }, dx, dy)) };
        candle.Points.Add(new Point(120.0, 410.0));
        canvasAdd(c, candle, 0.0, 0.0);
        desc = mkTxt("Show Burnables");
        Canvas.SetRight(desc, c.Width - 120.0);
        Canvas.SetTop(desc, 390.0);
        c.Children.Add(desc);
        COL = Brushes.CornflowerBlue;
        (dx, dy) = (ITEM_PROGRESS_FIRST_ITEM + 25.0 + 7.0 * 30.0, THRU_MAP_AND_LEGEND_H + 1.0);
        var others = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (2.0, 2.0), (2.0, 28.0), (118.0, 28.0), (118.0, 2.0), (2.0, 2.0) }, dx, dy)) };
        others.Points.Add(new Point(330.0, 405.0));
        canvasAdd(c, others, 0.0, 0.0);
        desc = mkTxt("Show Ladderable/Recorderable/\nPowerBraceletable/Raftable");
        desc.TextAlignment = TextAlignment.Right;
        Canvas.SetRight(desc, c.Width - 330.0);
        Canvas.SetTop(desc, 370.0);
        c.Children.Add(desc);
        COL = Brushes.MediumVioletRed;
        (dx, dy) = (LEFT_OFFSET + 4.8 * OMTW + 15.0, THRU_MAIN_MAP_H + 3.0);
        var recorderDest = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (10.0, -3.0), (-12.0, -3.0), (-12.0, 17.0), (10.0, 17.0), (10.0, -3.0) }, dx, dy)) };
        recorderDest.Points.Add(new Point(330.0, 340.0));
        canvasAdd(c, recorderDest, 0.0, 0.0);
        desc = mkTxt("Show recorder destinations");
        Canvas.SetRight(desc, c.Width - 330.0);
        Canvas.SetTop(desc, 340.0);
        c.Children.Add(desc);

        COL = Brushes.CornflowerBlue;
        (dx, dy) = (LEFT_OFFSET + 7.8 * OMTW + 56.0, THRU_MAIN_MAP_H + 36.0);
        var hintDecoderButton = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (39.0, 5.0), (39.0, 29.0), (71.0, 29.0), (71.0, 5.0), (39.0, 5.0) }, dx, dy)) };
        addLabel(hintDecoderButton, "Show hint shops", 442.0, 348.0);

        COL = Brushes.MediumVioletRed;
        (dx, dy) = (BLOCKERS_AND_NOTES_OFFSET + 70.0, START_DUNGEON_AND_NOTES_AREA_H);
        blockers = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (0.0, 0.0), (-70.0, 0.0), (-70.0, 36.0), (38.0, 36.0), (38.0, 0.0), (0.0, 0.0) }, dx, dy)) };
        addLabel(blockers, "Highlight potential\ndungeon continuations", 570.0, 320.0);

        COL = Brushes.Green;
        (dx, dy) = (BLOCKERS_AND_NOTES_OFFSET - 82.0, START_DUNGEON_AND_NOTES_AREA_H + 2.0);
        blockers = new Polyline { Stroke = COL, StrokeThickness = ST, Points = pts(offset(new[] { (-4.0, 0.0), (-4.0, 20.0), (25.0, 20.0), (25.0, 0.0), (-4.0, 0.0), (-217.0, -34.0), (-235.0, -34.0), (-235.0, -64.0), (-217.0, -64.0), (-217.0, -34.0), (-4.0, 0.0) }, dx, dy)) };
        addLabel(blockers, "Show\ndungeon\nlocations", 339.0, 320.0);

        foreach (var (ddc, ddtb, ddx, ddy) in delayedDescriptions)   // ensure these draw atop all the PolyLines
            canvasAdd(ddc, ddtb, ddx, ddy);
        // put MHE text atop all
        canvasAdd(c, descMHE, 450.0, 370.0);

        mouseHoverExplainerIcon.PointerEntered += (_, _) =>
        {
            c.Opacity = 1.0;
        };
        mouseHoverExplainerIcon.PointerExited += (_, _) =>
        {
            c.Opacity = 0.0;
        };

        return (mouseHoverExplainerIcon, c);
    }
}
