// Ported from Z1R_WPF/HUDs.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// The F# `open OverworldItemGridUI` is a `using static`, so `canvasAdd`, `TH` and `displayIsCurrentlyMirrored` read as in WPF. The F# events
// passed in (`Event<VisualBrush>`, `Event<TrackerLocation*int*int>`) are Graphics.SharedEvent<T>s. WPF's `#if NOT_RACE_LEGAL` blocks are kept as
// C# `#if`s (the symbol isn't defined in WPF's build or here, so, as in WPF, the two HUD makers are built but never hooked up, and WPFUI doesn't
// call MakeHUDs). Window positions are kept in WPF units through ShowRunCustom's leftOf/topOf/setLeftTop, as Popouts does.
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using static Z1R_Avalonia.OverworldItemGridUI;

namespace Z1R_Avalonia;

public static class HUDs
{
    // (port helpers: WPF's Window.Width/Height of a window with a frame are its outer size; Avalonia's Width/Height are the client size, and FrameSize
    // (known once the window is shown) the outer one. Frameless windows (WindowStyle.None) are the same either way.)
    private static double outerWidth(Window w) => w.FrameSize?.Width ?? w.ClientSize.Width;
    private static double outerHeight(Window w) => w.FrameSize?.Height ?? w.ClientSize.Height;
    // (WPF's `AllowsTransparency=true` with `WindowStyle.None`)
    private static void makeFramelessTransparent(Window w)
    {
        w.SystemDecorations = SystemDecorations.None;
        w.TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
    }

    public static void MakeHUDs(CustomComboBoxes.CanvasManager cm, Graphics.SharedEvent<VisualBrush> trackerDungeonMoused, Graphics.SharedEvent<(DungeonUI.TrackerLocation, int, int)> trackerLocationMoused)
    {
        var appMainCanvas = cm.AppMainCanvas;
        // near-mouse HUD
        Window nearMouseHUDChromeWindow = null;
        void makeOverlayWindow(bool _isRightClick) // todo: save location/size/position/stayfade, use right click to reset defaults
        {
            if (nearMouseHUDChromeWindow != null)
                nearMouseHUDChromeWindow.Close(); // only one at a time
            // (WPF: ResizeMode=CanMinimize → CanResize=false; Owner → passed to Show, below)
            nearMouseHUDChromeWindow = new Window { Title = "ZORATracker near-mouse HUD controls", CanResize = false, SizeToContent = SizeToContent.WidthAndHeight,
                                                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                                                    Background = Brushes.Black };
            var chromeWindow = nearMouseHUDChromeWindow;   // (the closures below use this window, as WPF's do through the mutable)
            var lastMouse = DateTime.Now;
            var maxOpacity = 1.0;
            var (oW, oH, oStay, oFade) = (new StrongBox<double>(250.0), new StrongBox<double>(250.0), new StrongBox<double>(1000.0), new StrongBox<double>(1300.0));   // (F#: `ref` cells)

            // controls layout
            TextBox mkTxt(string txt) => new TextBox { FontSize = 16.0, Foreground = Brushes.Lime, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), Text = txt };
            Action doUpdate = () => { };
            void edit(TextBox tb, TextBox label, double least, StrongBox<double> update)
            {
                tb.BorderThickness = new Thickness(1.0);
                tb.IsReadOnly = false;
                tb.IsHitTestVisible = true;
                tb.Width = 80.0;
                tb.TextChanged += (_, _) =>
                {
                    try
                    {
                        var r = double.Parse(tb.Text, CultureInfo.InvariantCulture);   // (F#'s `float s` parses with the invariant culture)
                        if (r >= least)
                        {
                            label.Foreground = Brushes.Lime;
                            update.Value = r;
                            doUpdate();
                        }
                        else
                            label.Foreground = Brushes.Red;
                    }
                    catch
                    {
                        label.Foreground = Brushes.Red;
                    }
                };
            }
            var sp = new StackPanel { Orientation = Orientation.Vertical };
            var topButtons = new DockPanel { LastChildFill = false };
            var topLeftButton = new Button { Content = mkTxt("Put HUD above") };
            topButtons.Children.Add(topLeftButton);
            DockPanel.SetDock(topLeftButton, Dock.Left);
            var topRightButton = new Button { Content = mkTxt("Put HUD above") };
            topButtons.Children.Add(topRightButton);
            DockPanel.SetDock(topRightButton, Dock.Right);
            sp.Children.Add(topButtons);
            void spacer() => sp.Children.Add(new DockPanel { Height = 10.0 });
            spacer();
            sp.Children.Add(mkTxt("Move this control window to position the HUD.\n(Click corner buttons if needed.)\nYou can minimize this control window once\nthe HUD is positioned where you want it."));
            spacer();
            var spWH = new StackPanel { Orientation = Orientation.Horizontal };
            var size = mkTxt("HUD Size -- ");
            var widthLabel = mkTxt("Width:");
            var widthInput = mkTxt((oW.Value).ToString());
            var heightLabel = mkTxt("   Height:");
            var heightInput = mkTxt((oH.Value).ToString());
            edit(widthInput, widthLabel, 20.0, oW);
            edit(heightInput, heightLabel, 20.0, oH);
            spWH.Children.Add(size);
            spWH.Children.Add(widthLabel);
            spWH.Children.Add(widthInput);
            spWH.Children.Add(heightLabel);
            spWH.Children.Add(heightInput);
            sp.Children.Add(spWH);
            spacer();
            var opacityLabel = mkTxt("Max Opacity:");
            sp.Children.Add(opacityLabel);
            // (WPF TickPlacement.Both → Avalonia TickPlacement.Outside, ticks on both sides)
            var slider = new Slider { Orientation = Orientation.Horizontal, Maximum = 100.0, TickFrequency = 10.0, TickPlacement = TickPlacement.Outside, IsSnapToTickEnabled = false, Width = 400.0 };
            slider.Value = maxOpacity * slider.Maximum;
            slider.ValueChanged += (_, _) => { lastMouse = DateTime.Now; maxOpacity = slider.Value / 100.0; };
            sp.Children.Add(slider);
            spacer();
            sp.Children.Add(mkTxt("After each mouse move in the tracker,\nHUD will stay at Max Opacity for 'Stay'ms, then\nfade out completely after 'Fade'ms.\nFor 'always on' mode, set 'Fade' to 0."));
            spacer();
            var spSF = new StackPanel { Orientation = Orientation.Horizontal };
            var stayLabel = mkTxt("Stay(ms):");
            var stayInput = mkTxt((oStay.Value).ToString());
            var fadeLabel = mkTxt("   Fade(ms):");
            var fadeInput = mkTxt((oFade.Value).ToString());
            edit(stayInput, stayLabel, 0.0, oStay);
            edit(fadeInput, fadeLabel, 0.0, oFade);
            spSF.Children.Add(stayLabel);
            spSF.Children.Add(stayInput);
            spSF.Children.Add(fadeLabel);
            spSF.Children.Add(fadeInput);
            sp.Children.Add(spSF);
            spacer();
            var bottomButtons = new DockPanel { LastChildFill = false };
            var bottomLeftButton = new Button { Content = mkTxt("Put HUD below") };
            ((TextBox)bottomLeftButton.Content).Background = Brushes.Green;
            bottomButtons.Children.Add(bottomLeftButton);
            DockPanel.SetDock(bottomLeftButton, Dock.Left);
            var bottomRightButton = new Button { Content = mkTxt("Put HUD below") };
            bottomButtons.Children.Add(bottomRightButton);
            DockPanel.SetDock(bottomRightButton, Dock.Right);
            sp.Children.Add(bottomButtons);

            chromeWindow.Content = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(2.0), Child = sp };
            ShowRunCustom.showOwned(chromeWindow);

            var W = appMainCanvas.Width;
            // (WPF: ResizeMode=NoResize, WindowStyle=None, AllowsTransparency=true, Owner → passed to Show, below)
            var nearMouseHUDWindow = new Window { Title = "ZORATracker near-mouse HUD", CanResize = false, SizeToContent = SizeToContent.Manual,
                                            Background = Brushes.Black, Opacity = maxOpacity, Topmost = true };
            makeFramelessTransparent(nearMouseHUDWindow);
            nearMouseHUDWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            ShowRunCustom.setLeftTop(nearMouseHUDWindow, 0.0, 0.0);

            Rectangle makeViewRect(Point upperLeft, Point lowerRight)
            {
                // (WPF: ViewboxUnits=Absolute, Viewbox=Rect(upperLeft, lowerRight); Avalonia: an absolute SourceRect)
                var viewbox = new Rect(upperLeft, lowerRight);
                var vb = new VisualBrush(cm.RootCanvas);
                vb.SourceRect = new RelativeRect(viewbox, RelativeUnit.Absolute);
                vb.Stretch = Stretch.None;
                var bwRect = new Rectangle { Width = viewbox.Width, Height = viewbox.Height };
                bwRect.Fill = vb;
                return bwRect;
            }

            var c = new Canvas();
            var wholeView = makeViewRect(new Point(0.0, 0.0), new Point(W, appMainCanvas.Height));
            canvasAdd(c, wholeView, 0.0, 0.0);

            Polygon addFakeMouse(Canvas c2)
            {
                var fakeMouse_ = new Polygon { Fill = Brushes.White };
                fakeMouse_.Points = new Points { new Point(0.0, 0.0), new Point(12.0, 6.0), new Point(6.0, 12.0) };
                c2.Children.Add(fakeMouse_);
                return fakeMouse_;
            }
            var fakeMouse = addFakeMouse(c);
            doUpdate = () =>
            {
                nearMouseHUDWindow.Width = oW.Value;
                nearMouseHUDWindow.Height = oH.Value;
                Canvas.SetLeft(fakeMouse, oW.Value / 2.0);
                Canvas.SetTop(fakeMouse, oH.Value / 2.0);
            };
            doUpdate();

            var dp = new DockPanel { Width = W };
            dp.UseLayoutRounding = true;
            dp.Children.Add(c);
            var borderForLocationMoveSetup = new Border { BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(1.0), Child = dp };
            nearMouseHUDWindow.Content = borderForLocationMoveSetup;

            cm.RootCanvas.PointerMoved += (_, ea) =>   // we need RootCanvas to see mouse moving in popups
            {
                var mousePos = ea.GetPosition(appMainCanvas);
                Canvas.SetLeft(wholeView, (oW.Value / 2.0) - mousePos.X);
                Canvas.SetTop(wholeView, (oH.Value / 2.0) - mousePos.Y);
                lastMouse = DateTime.Now;
            };

            var which = 0;  // 0 = bottom left, 1 = bottom right, 2 = top left, 3 = top right
            void moveWindow()
            {
                var (cl, ct) = (ShowRunCustom.leftOf(chromeWindow), ShowRunCustom.topOf(chromeWindow));
                if (which == 0)
                    ShowRunCustom.setLeftTop(nearMouseHUDWindow, cl + 8.0, ct + outerHeight(chromeWindow));
                else if (which == 1)
                    ShowRunCustom.setLeftTop(nearMouseHUDWindow, cl - 8.0 + outerWidth(chromeWindow) - nearMouseHUDWindow.Width, ct + outerHeight(chromeWindow));
                else if (which == 2)
                    ShowRunCustom.setLeftTop(nearMouseHUDWindow, cl + 8.0, ct - 8.0 - nearMouseHUDWindow.Height);
                else
                    ShowRunCustom.setLeftTop(nearMouseHUDWindow, cl - 8.0 + outerWidth(chromeWindow) - nearMouseHUDWindow.Width, ct - 8.0 - nearMouseHUDWindow.Height);
                borderForLocationMoveSetup.BorderBrush = Brushes.Gray;
                lastMouse = DateTime.Now;
            }
            var all = new[] { bottomLeftButton, bottomRightButton, topLeftButton, topRightButton };
            foreach (var (n, b) in new[] { (0, bottomLeftButton), (1, bottomRightButton), (2, topLeftButton), (3, topRightButton) })
            {
                b.Click += (_, _) =>
                {
                    which = n;
                    foreach (var x in all)
                        ((TextBox)x.Content).Background = Brushes.Black;
                    ((TextBox)b.Content).Background = Brushes.Green;
                    moveWindow();
                };
            }
            chromeWindow.PositionChanged += (_, _ea) =>
            {
                //if overlayChromeWindow.WindowState = WindowState.Normal then  // dont update when Minimized
                // (WPF: `Left <> -32000.`, where Windows moves minimized windows; macOS/Linux don't, so WindowState counts too, as in Popouts)
                if (!(ShowRunCustom.leftOf(chromeWindow) < ShowRunCustom.MINIMIZED_THRESHOLD || chromeWindow.WindowState == WindowState.Minimized))  // dont update when Minimized
                    moveWindow();
            };
            chromeWindow.Closed += (_, _) => nearMouseHUDWindow.Close();

            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100.0);
            timer.Tick += (_, _) =>
            {
                if (oFade.Value == 0.0)
                    nearMouseHUDWindow.Opacity = maxOpacity;
                else
                {
                    var diffms = (double)(DateTime.Now - lastMouse).TotalMilliseconds;
                    if (diffms < oStay.Value)
                        nearMouseHUDWindow.Opacity = maxOpacity;
                    else if (diffms < oFade.Value)
                    {
                        var pct = (diffms - oStay.Value) / (oFade.Value - oStay.Value);
                        nearMouseHUDWindow.Opacity = maxOpacity * (1.0 - pct);
                    }
                    else
                        nearMouseHUDWindow.Opacity = 0.0;
                }
                borderForLocationMoveSetup.BorderBrush = Brushes.Transparent;
            };
            timer.Start();

            ShowRunCustom.showOwned(nearMouseHUDWindow);
            moveWindow();
        }
#if NOT_RACE_LEGAL
        nearMouseHUD = (isRightClick) => makeOverlayWindow(isRightClick);
#endif

        Window minimapOverlayWindow = null;
        void makeMinimapOverlay(bool isRightClick)
        {
            if (minimapOverlayWindow != null)
                minimapOverlayWindow.Close(); // only one at a time
            // (WPF: ResizeMode=NoResize, WindowStyle=None, AllowsTransparency=true, Owner → passed to Show, below)
            minimapOverlayWindow = new Window { Title = "ZORATracker minimap overlay", CanResize = false, SizeToContent = SizeToContent.Manual,
                                                    WindowStartupLocation = WindowStartupLocation.Manual,
                                                    Background = Brushes.Transparent,
                                                    Opacity = 1.0, Topmost = true };
            makeFramelessTransparent(minimapOverlayWindow);
            var overlayWindow = minimapOverlayWindow;   // (the closures below use this window, as WPF's do through the mutable)
            void init()
            {
                var (W, H) = (overlayWindow.Width, overlayWindow.Height);
                var entireCanvas = new Canvas { Width = W, Height = H };
                var minimapCanvas = new Canvas { Width = W, Height = H };
                var GRIDCOLOR = Brushes.Gray;
                var vs = new Line[9];
                for (int i = 0; i < 9; i++)
                {
                    var x = (48.0 + 24.0 * (double)i) * W / 768.0;
                    vs[i] = new Line { Stroke = GRIDCOLOR, StrokeThickness = 1.0, StartPoint = new Point(x, 48.0 * H / 672.0), EndPoint = new Point(x, (48.0 + 12.0 * 8.0) * H / 672.0) };
                }
                var hs = new Line[9];
                for (int j = 0; j < 9; j++)
                {
                    var y = (48.0 + 12.0 * (double)j) * H / 672.0;
                    hs[j] = new Line { Stroke = GRIDCOLOR, StrokeThickness = 1.0, StartPoint = new Point(48.0 * W / 768.0, y), EndPoint = new Point((48.0 + 24.0 * 8.0) * W / 768.0, y) };
                }
                Border make_drect()
                {
                    var c = new Canvas { Width = 24.0 * W / 768.0 - 2.0, Height = 12.0 * H / 672.0 - 2.0 };
                    var b = new Border { BorderBrush = Brushes.White, BorderThickness = new Thickness(3.0), Child = c, Opacity = 0.0 };
                    return b;
                }
                var drect = make_drect();
                Border make_orect()
                {
                    var c = new Canvas { Width = 12.0 * W / 768.0 - 2.0, Height = 12.0 * H / 672.0 - 2.0 };
                    var b = new Border { BorderBrush = Brushes.White, BorderThickness = new Thickness(3.0), Child = c, Opacity = 0.0 };
                    return b;
                }
                var orect = make_orect();
                Border make_oLegendRect()
                {
                    var c = new Canvas { Width = 12.0 * W / 768.0 - 2.0, Height = 12.0 * H / 672.0 - 2.0 };
                    var b = new Border { BorderBrush = Brushes.White, BorderThickness = new Thickness(3.0), Child = c, Opacity = 1.0, Height = 12.0 * H / 672.0 - 2.0 + 6.0 };
                    return b;
                }
                var oLegendRect = make_oLegendRect();
                TextBox mkTxt(string txt) => new TextBox { FontSize = oLegendRect.Height, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), Text = txt };
                var oText1 = mkTxt("ZORATracker mouse ");
                var oText2 = mkTxt(" at H16");
                var oCaption = new StackPanel { Orientation = Orientation.Horizontal, Opacity = 0.0 };
                oCaption.Children.Add(oText1);
                oCaption.Children.Add(oLegendRect);
                oCaption.Children.Add(oText2);
                canvasAdd(minimapCanvas, oCaption, 48.0 * W / 768.0, 144.0 * H / 672.0 + 2.0);
                for (int i = 0; i <= 8; i++)
                {
                    canvasAdd(minimapCanvas, vs[i], 0.0, 0.0);
                    canvasAdd(minimapCanvas, hs[i], 0.0, 0.0);
                }
                canvasAdd(minimapCanvas, drect, 0.0, 0.0);
                canvasAdd(minimapCanvas, orect, 0.0, 0.0);
                trackerLocationMoused.Fired += tlij =>
                {
                    var (tl, i, j) = tlij;
                    if (i == -1)
                    {
                        minimapCanvas.Opacity = 0.0;
                        drect.Opacity = 0.0;
                        orect.Opacity = 0.0;
                        oCaption.Opacity = 0.0;
                    }
                    else
                    {
                        switch (tl)
                        {
                            case DungeonUI.TrackerLocation.DUNGEON:
                                minimapCanvas.Opacity = 1.0;
                                drect.Opacity = 1.0;
                                orect.Opacity = 0.0;
                                oCaption.Opacity = 0.0;
                                Canvas.SetLeft(drect, (48.0 + 24.0 * (double)i) * W / 768.0 - 2.0);
                                Canvas.SetTop(drect, (48.0 + 12.0 * (double)j) * H / 672.0 - 2.0);
                                break;
                            case DungeonUI.TrackerLocation.OVERWORLD:
                                {
                                    minimapCanvas.Opacity = 1.0;
                                    drect.Opacity = 0.0;
                                    orect.Opacity = 1.0;
                                    oCaption.Opacity = 1.0;
                                    var i2 = displayIsCurrentlyMirrored ? 16 - i : i + 1;
                                    oText2.Text = $" at {"ABCDEFGH"[j]}{i2}";
                                    Canvas.SetLeft(orect, (48.0 + 12.0 * (double)(i2 - 1)) * W / 768.0 - 2.0);
                                    Canvas.SetTop(orect, (48.0 + 12.0 * (double)j) * H / 672.0 - 2.0);
                                    break;
                                }
                        }
                    }
                };
                var pauseScreenMapCanvas = new Canvas { Width = W, Height = H, Opacity = 0.0 };
                var left = 384.0 * W / 768.0;
                var top = 285.0 * H / 672.0;
                var width = 8.0 * 24.0 * W / 768.0;
                var height = ((8.0 * 24.0) - 6.0) * H / 672.0;
                var heightWithHeader = height * (double)(TH + 27 * 8 + 12 * 7) / (double)(27 * 8 + 12 * 7);
                var topWithHeader = top - (heightWithHeader - height);
                var rect = new Rectangle { Width = width, Height = heightWithHeader, Stroke = Brushes.Gray, StrokeThickness = 1.0 };
                canvasAdd(pauseScreenMapCanvas, rect, left, topWithHeader);
                trackerDungeonMoused.Fired += (VisualBrush vb) =>
                {
                    if (vb == null)
                        pauseScreenMapCanvas.Opacity = 0.0;
                    else
                    {
                        rect.Fill = vb;
                        pauseScreenMapCanvas.Opacity = 0.6;
                    }
                };
                //c.UseLayoutRounding <- true
                //let outerBorder = new Border(BorderBrush=Brushes.Lime, BorderThickness=Thickness(1.), Child=c, Opacity=1.0)  // for help debugging
                //overlayLocatorWindow.Content <- outerBorder
                canvasAdd(entireCanvas, minimapCanvas, 0.0, 0.0);
                canvasAdd(entireCanvas, pauseScreenMapCanvas, 0.0, 0.0);
                overlayWindow.Content = entireCanvas;
            }
            // 768   48 72 ...
            // 672   48 60 ...

            var sizerWindow = new Window();
            sizerWindow.Title = "ZORATracker sizer";
            sizerWindow.CanResize = true;   // (WPF: ResizeMode.CanResize)
            sizerWindow.SizeToContent = SizeToContent.Manual;
            void save()
            {
                // (the size saved is Avalonia's client size; WPF's Width/Height are the outer size, see Popouts)
                TrackerModelOptions.OverlayLocatorWindowLTWH = $"{(int)ShowRunCustom.leftOf(sizerWindow)},{(int)ShowRunCustom.topOf(sizerWindow)},{(int)sizerWindow.ClientSize.Width},{(int)sizerWindow.ClientSize.Height}";
                TrackerModelOptions.writeSettings();
            }
            var leftTopWidthHeight = TrackerModelOptions.OverlayLocatorWindowLTWH;
            var matches = System.Text.RegularExpressions.Regex.Match(leftTopWidthHeight, @"^(-?\d+),(-?\d+),(\d+),(\d+)$");
            if (!isRightClick && matches.Success)
            {
                ShowRunCustom.setLeftTop(sizerWindow, double.Parse(matches.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(matches.Groups[2].Value, CultureInfo.InvariantCulture));
                sizerWindow.Width = double.Parse(matches.Groups[3].Value, CultureInfo.InvariantCulture);
                sizerWindow.Height = double.Parse(matches.Groups[4].Value, CultureInfo.InvariantCulture);
                sizerWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            }
            else
            {
                sizerWindow.Width = 500.0;
                sizerWindow.Height = 500.0;
                sizerWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            // (WPF: sizerWindow.Owner <- Application.Current.MainWindow; Avalonia passes the owner to Show, below)
            sizerWindow.Background = Brushes.Black;
            // (WPF: WindowStyle.SingleBorderWindow, the default; Avalonia's default decorations)
            var dp = new DockPanel { Opacity = 0.0, LastChildFill = true };
            var sp = new StackPanel { Orientation = Orientation.Vertical };
            sp.Children.Add(new TextBox { Text = "Resize this window so that it exactly covers\nthe NES game screen, then click the button below", TextAlignment = TextAlignment.Center });
            var button = new Button { Content = new TextBox { Text = "Click here after sizing" }, Width = 300.0, HorizontalAlignment = HorizontalAlignment.Center };
            button.Click += (_, _) =>
            {
                // (WPF's +8/-16/-8 strip the invisible resize borders Windows 10 puts around a window's outer size)
                ShowRunCustom.setLeftTop(overlayWindow, ShowRunCustom.leftOf(sizerWindow) + 8.0, ShowRunCustom.topOf(sizerWindow));
                overlayWindow.Width = outerWidth(sizerWindow) - 16.0;
                overlayWindow.Height = outerHeight(sizerWindow) - 8.0;
                init();
                ShowRunCustom.showOwned(overlayWindow);
                dp.Opacity = 1.0;
                save();
                sizerWindow.Close();
            };
            sp.Children.Add(button);
            sp.Children.Add(new TextBox { Text = "You can make gross adjustments to window size\nby grabbing the window corner, like any other window.", FontSize = 12.0, TextAlignment = TextAlignment.Center });
            sp.Children.Add(new TextBox { Text = "To fine-tune the window size, you can use the buttons\nbelow to adjust one pixel at a time.", FontSize = 12.0, TextAlignment = TextAlignment.Center });
            var nudgeCanvas = new Canvas { Width = 260.0, Height = 260.0, HorizontalAlignment = HorizontalAlignment.Center };
            var r = new Rectangle { Width = 150.0, Height = 150.0, Stroke = Brushes.White, StrokeThickness = 3.0 };
            canvasAdd(nudgeCanvas, r, 55.0, 55.0);
            var leftLarger = new Button { Content = new TextBox { Text = "◄" }, Width = 40.0, Height = 40.0 };
            canvasAdd(nudgeCanvas, leftLarger, 10.0, 110.0);
            var leftSmaller = new Button { Content = new TextBox { Text = "►" }, Width = 40.0, Height = 40.0 };
            canvasAdd(nudgeCanvas, leftSmaller, 60.0, 110.0);
            var rightSmaller = new Button { Content = new TextBox { Text = "◄" }, Width = 40.0, Height = 40.0 };
            canvasAdd(nudgeCanvas, rightSmaller, 160.0, 110.0);
            var rightLarger = new Button { Content = new TextBox { Text = "►" }, Width = 40.0, Height = 40.0 };
            canvasAdd(nudgeCanvas, rightLarger, 210.0, 110.0);
            var topLarger = new Button { Content = new TextBox { Text = "▲" }, Width = 40.0, Height = 40.0 };
            canvasAdd(nudgeCanvas, topLarger, 110.0, 10.0);
            var topSmaller = new Button { Content = new TextBox { Text = "▼" }, Width = 40.0, Height = 40.0 };
            canvasAdd(nudgeCanvas, topSmaller, 110.0, 60.0);
            var bottomSmaller = new Button { Content = new TextBox { Text = "▲" }, Width = 40.0, Height = 40.0 };
            canvasAdd(nudgeCanvas, bottomSmaller, 110.0, 160.0);
            var bottomLarger = new Button { Content = new TextBox { Text = "▼" }, Width = 40.0, Height = 40.0 };
            canvasAdd(nudgeCanvas, bottomLarger, 110.0, 210.0);
            void left_(double delta) => ShowRunCustom.setLeftTop(sizerWindow, ShowRunCustom.leftOf(sizerWindow) + delta, ShowRunCustom.topOf(sizerWindow));
            void width_(double delta) => sizerWindow.Width = sizerWindow.ClientSize.Width + delta;
            void top_(double delta) => ShowRunCustom.setLeftTop(sizerWindow, ShowRunCustom.leftOf(sizerWindow), ShowRunCustom.topOf(sizerWindow) + delta);
            void height_(double delta) => sizerWindow.Height = sizerWindow.ClientSize.Height + delta;
            leftLarger.Click += (_, _) => { left_(-1.0); width_(1.0); };
            leftSmaller.Click += (_, _) => { left_(1.0); width_(-1.0); };
            rightSmaller.Click += (_, _) => width_(-1.0);
            rightLarger.Click += (_, _) => width_(1.0);
            topLarger.Click += (_, _) => { top_(-1.0); height_(1.0); };
            topSmaller.Click += (_, _) => { top_(1.0); height_(-1.0); };
            bottomSmaller.Click += (_, _) => height_(-1.0);
            bottomLarger.Click += (_, _) => height_(1.0);
            sp.Children.Add(nudgeCanvas);
            var outerBorder = new Border { BorderBrush = Brushes.White, BorderThickness = new Thickness(2.0, 0.0, 2.0, 2.0), Child = sp, Opacity = 1.0 };
            // (WPF: an implicit TextBox style in outerBorder.Resources; Avalonia: a style in the border's Styles, which applies to its descendants)
            var style = new Style(x => x.OfType<TextBox>());
            style.Setters.Add(new Setter(TemplatedControl.FontSizeProperty, 16.0));
            style.Setters.Add(new Setter(TextBox.IsReadOnlyProperty, true));
            style.Setters.Add(new Setter(Avalonia.Input.InputElement.IsHitTestVisibleProperty, false));
            outerBorder.Styles.Add(style);
            sizerWindow.Content = outerBorder;
            ShowRunCustom.showOwned(sizerWindow);
        }
#if NOT_RACE_LEGAL
        minimapOverlay = (isRightClick) => makeMinimapOverlay(isRightClick);
#endif

        _ = (Action<bool>)makeMinimapOverlay;
        _ = (Action<bool>)makeOverlayWindow;
    }
}
