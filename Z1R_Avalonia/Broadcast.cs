// Ported from Z1R_WPF/Broadcast.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// The F# `open OverworldItemGridUI` is a `using static`, so `canvasAdd` and the layout constants read as in WPF; `open OverworldMapTileCustomization`
// is a `using` alias for OW_ITEM_GRID_LOCATIONS (as in UIComponents.cs). Window positions are kept in WPF units through ShowRunCustom's
// leftOf/topOf/setLeftTop, and sizes are Avalonia's client size, as in Popouts.cs. WPF's VisualBrush Viewbox (absolute units) is an absolute SourceRect.
using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using static Z1R_Avalonia.OverworldItemGridUI;
using OW_ITEM_GRID_LOCATIONS = Z1R_Avalonia.OverworldMapTileCustomization.OW_ITEM_GRID_LOCATIONS;

namespace Z1R_Avalonia;

public static class Broadcast
{
    // (port helpers: WPF's Window.Width/Height are the outer size; Avalonia's FrameSize (known once shown), else the client size)
    private static double outerWidth(Window w) => w.FrameSize?.Width ?? w.ClientSize.Width;
    private static double outerHeight(Window w) => w.FrameSize?.Height ?? w.ClientSize.Height;

    public static bool allWithinOneScreen(Window w)
    {
        // (WPF: System.Windows.Forms.Screen's pixel height over SystemParameters' DIP height, i.e. the display scaling, then Left/Top/Width/Height times that.
        // Avalonia's Position is already in pixels, and DesktopScaling is this window's display scaling.)
        var scale = w.DesktopScaling;  // e.g.   1.5

        //printfn "Broadcast LTWHS: %f %f %f %f %f" w.Left w.Top w.Width w.Height scale
        // for whatever reason, this FUDGE is needed to be accurate
        var W_FUDGE = 8;
        var H_FUDGE = 10;
        var rect = new PixelRect(w.Position.X, w.Position.Y, (int)(outerWidth(w) * scale) - W_FUDGE, (int)(outerHeight(w) * scale) - H_FUDGE);

        var screens = w.Screens.All;
        var ok = false;
        foreach (var s in screens)
        {
            //printfn "Screen LTWH: %d %d %d %d" s.WorkingArea.Left s.WorkingArea.Top s.WorkingArea.Width s.WorkingArea.Height
            if (s.WorkingArea.Contains(rect))
            {
                //printfn "ok on that screen"
                ok = true;
            }
            else
            {
                //printfn "not ok on that screen"
            }
        }
        return ok;
    }

    public static Rectangle makeViewRectImpl(Point upperLeft, Point lowerRight, Visual c)
    {
        var viewbox = new Rect(upperLeft, lowerRight);
        var vb = new VisualBrush(c);
        vb.SourceRect = new RelativeRect(viewbox, RelativeUnit.Absolute);   // (WPF: ViewboxUnits=Absolute, Viewbox=Rect(upperLeft, lowerRight))
        vb.Stretch = Stretch.None;
        var bwRect = new Rectangle { Width = viewbox.Width, Height = viewbox.Height };
        bwRect.Fill = vb;
        return bwRect;
    }

    // the Max Hearts button (set by WPFUI); the dungeon-hover view of the broadcast window shows a live copy of it under the take-any hearts, since the original is hidden behind the map there
    public static Control maxHeartsForBroadcast = null;
    public static void MakeBroadcastWindow(CustomComboBoxes.CanvasManager cm, Canvas drawingCanvas, Grid blockerGrid, Canvas dungeonTabsOverlayContent, Action refocusMainWindow)
    {
        var appMainCanvas = cm.AppMainCanvas;
        Window makeBroadcastWindow(int size, bool showOverworldMagnifier)
        {
            var W = 768.0;
            var scaleSize = size == 1 ? 256.0 : size == 2 ? 512.0 : 768.0;
            var broadcastWindow = new Window();
            broadcastWindow.Title = "Z-Tracker broadcast";
            broadcastWindow.CanResize = true;   // resizable: the content scales (via a Viewbox) to fit the window
            broadcastWindow.SizeToContent = SizeToContent.Manual;
            broadcastWindow.WindowStartupLocation = WindowStartupLocation.Manual;

            var topBar = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                                            Background = Graphics.freeze(new SolidColorBrush(Color.FromRgb(120, 30, 30))), Width = scaleSize, Opacity = 0.0 };
            // (a read-only wrapped TextBox used as a label → SelectableTextBlock, as Avalonia's TextBox mis-measures wrapped text)
            var tb = new SelectableTextBlock { Text = "The broadcast window is at least partially off-screen.  Streaming software like OBS cannot capture display updates to off-screen windows.  " +
                                        "Re-position this window so that it is entirely within the bounds of a screen.  It is ok for this window to be BEHIND another window, " +
                                        "such as behind the main Z-Tracker application window.  See the Z-Tracker documentation for details.",
                                        Foreground = Brushes.White, Background = Brushes.Transparent,
                                        FontSize = 14.0, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            topBar.Children.Add(tb);
            void updateTopBar()
            {
                if (allWithinOneScreen(broadcastWindow))
                    topBar.Opacity = 0.0;
                else
                    topBar.Opacity = 1.0;
            }

            var leftTop = TrackerModelOptions.BroadcastWindowLT;
            var matches = System.Text.RegularExpressions.Regex.Match(leftTop, @"^(-?\d+),(-?\d+)$");
            if (matches.Success)
                ShowRunCustom.setLeftTop(broadcastWindow, double.Parse(matches.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(matches.Groups[2].Value, CultureInfo.InvariantCulture));

            broadcastWindow.Loaded += (_, _) => updateTopBar();
            broadcastWindow.PositionChanged += (_, _) =>
            {
                updateTopBar();
                TrackerModelOptions.BroadcastWindowLT = $"{(int)ShowRunCustom.leftOf(broadcastWindow)},{(int)ShowRunCustom.topOf(broadcastWindow)}";
                TrackerModelOptions.writeSettings();
            };
            broadcastWindow.Width = scaleSize + 16.0;
            // (WPF: broadcastWindow.Owner <- Application.Current.MainWindow; Avalonia passes the owner to Show, below)
            broadcastWindow.Background = Brushes.Black;

            Rectangle makeViewRect(Point upperLeft, Point lowerRight) => makeViewRectImpl(upperLeft, lowerRight, appMainCanvas);
            void dealWithPopups(double topViewboxRelativeToApp, double topViewboxRelativeToThisBroadcast, Canvas c)
            {
                var popups = new Dictionary<Canvas, Rectangle>(ReferenceEqualityComparer.Instance);   // (keyed by popup layer, so closing popups out of order removes the right copy)
                var popupCanvasArea = new Canvas();
                canvasAdd(c, popupCanvasArea, 0.0, 0.0);
                cm.AfterCreatePopupCanvas += pc =>
                {
                    var viewbox = new Rect(new Point(0.0, 0.0), new Point(appMainCanvas.Width, appMainCanvas.Height));
                    var vb = new VisualBrush(pc);
                    vb.SourceRect = new RelativeRect(viewbox, RelativeUnit.Absolute);
                    vb.Stretch = Stretch.None;
                    var bwRect = new Rectangle { Width = viewbox.Width, Height = viewbox.Height };
                    bwRect.Fill = vb;
                    popups[pc] = bwRect;
                    canvasAdd(popupCanvasArea, bwRect, 0.0, topViewboxRelativeToThisBroadcast - topViewboxRelativeToApp);
                };
                cm.BeforeDismissPopupCanvas += pc =>
                {
                    if (popups.TryGetValue(pc, out var bwRect))   // (no entry if the user turned on the broadcast window mid-game, as the options window was a popup when the broadcast began)
                    {
                        popups.Remove(pc);
                        popupCanvasArea.Children.Remove(bwRect);
                    }
                };
            }

            var timeline = makeViewRect(new Point(0.0, START_TIMELINE_H), new Point(W, THRU_TIMELINE_H));

            // construct the top broadcast canvas (topc)
            var notesY = START_DUNGEON_AND_NOTES_AREA_H + blockerGrid.Height;
            var top = makeViewRect(new Point(0.0, 0.0), new Point(W, notesY));
            var drawingOverTop = makeViewRectImpl(new Point(0.0, 0.0), new Point(W, notesY), drawingCanvas);
            var H = top.Height + (THRU_TIMELINE_H - START_TIMELINE_H);  // the top one is the larger of the two, so always have window that size
            var topc = new Canvas { Width = W, Height = H };
            canvasAdd(topc, top, 0.0, 0.0);
            var notes = makeViewRect(new Point(BLOCKERS_AND_NOTES_OFFSET, notesY), new Point(W, notesY + blockerGrid.Height));
            canvasAdd(topc, notes, 0.0, START_DUNGEON_AND_NOTES_AREA_H);
            var blackArea = new Canvas { Width = BLOCKERS_AND_NOTES_OFFSET * 2.0 - W, Height = blockerGrid.Height, Background = Brushes.Black };
            canvasAdd(topc, blackArea, W - BLOCKERS_AND_NOTES_OFFSET, START_DUNGEON_AND_NOTES_AREA_H);
            if (showOverworldMagnifier)
            {
                // (WPF's default VisualBrush Stretch is Fill; Avalonia's is Uniform)
                var magnifierView = new Canvas { Width = dungeonTabsOverlayContent.Width, Height = dungeonTabsOverlayContent.Height, Background = new VisualBrush(dungeonTabsOverlayContent) { Stretch = Stretch.Fill } };
                Canvas.SetLeft(magnifierView, 0.0);
                Canvas.SetBottom(magnifierView, 0.0);
                topc.Children.Add(magnifierView);
            }
            canvasAdd(topc, drawingOverTop, 0.0, 0.0);
            dealWithPopups(0.0, 0.0, topc);

            // construct the bottom broadcast canvas (bottomc)
            var afterSoldItemBoxesX = OW_ITEM_GRID_LOCATIONS.OFFSET + 120.0;
            drawingOverTop = makeViewRectImpl(new Point(0.0, 0.0), new Point(afterSoldItemBoxesX, 150.0), drawingCanvas);
            var dun = makeViewRect(new Point(0.0, THRU_MAIN_MAP_AND_ITEM_PROGRESS_H), new Point(W, START_TIMELINE_H));
            var tri = makeViewRect(new Point(0.0, 0.0), new Point(W, 150.0));
            var pro = makeViewRect(new Point(ITEM_PROGRESS_FIRST_ITEM, THRU_MAP_AND_LEGEND_H + 4.0),
                                    new Point(ITEM_PROGRESS_FIRST_ITEM + 15.0 * 30.0 - 11.0, THRU_MAIN_MAP_AND_ITEM_PROGRESS_H));  // -11. because 'Hint decoder' button infringes into empty space by Magic Key
            pro.HorizontalAlignment = HorizontalAlignment.Left;
            pro.Margin = new Thickness(10.0, 4.0, 0.0, 0.0);   // (the view starts 4px below the legend row so the bottom edge of the Dead Woods / Lost Hills buttons does not show; the margin keeps the layout height)
            var proScale = new ScaleTransform(0.88, 0.88);   // shrunk so the whole strip ends before the map
            // (WPF freezes it if it can; Avalonia has no Freeze)
            pro.RenderTransform = proScale;
            pro.RenderTransformOrigin = RelativePoint.TopLeft;   // (WPF scales from the top-left corner; Avalonia's default is the centre)
            var owm = makeViewRect(new Point(0.0, 150.0), new Point(W, THRU_MAIN_MAP_H));
            var sp = new StackPanel { Orientation = Orientation.Vertical };
            sp.Children.Add(tri);
            sp.Children.Add(new DockPanel { Height = 50.0 });
            sp.Children.Add(pro);   // the item progress strip sits right above the dungeon panels
            sp.Children.Add(dun);
            var bottomc = new Canvas { Width = W, Height = H };
            canvasAdd(bottomc, sp, 0.0, 0.0);
            var scaleW = (W - afterSoldItemBoxesX) / W;
            var scaleH = (180.0 - 45.0) / (THRU_MAIN_MAP_H - 150.0);
            var scaleTrans = new ScaleTransform(scaleW, scaleH);
            // (WPF freezes it if it can; Avalonia has no Freeze)
            owm.RenderTransform = scaleTrans;
            owm.RenderTransformOrigin = RelativePoint.TopLeft;   // (as above)
            canvasAdd(bottomc, owm, afterSoldItemBoxesX, 95.0);
            if (!(maxHeartsForBroadcast is null))
            {
                var mh = new Rectangle { Width = maxHeartsForBroadcast.Width, Height = maxHeartsForBroadcast.Height, Fill = new VisualBrush(maxHeartsForBroadcast) { Stretch = Stretch.Fill } };   // (WPF's default Stretch is Fill)
                var heartsCenterX = OW_ITEM_GRID_LOCATIONS.OFFSET + 60.0;   // the four take-any hearts span 4 cells of 30px from the grid's left edge
                canvasAdd(bottomc, mh, heartsCenterX - maxHeartsForBroadcast.Width / 2.0, 152.0);   // centered just under the hearts, left of the map
            }
            // WANT!
            canvasAdd(bottomc, drawingOverTop, 0.0, 0.0);
            dealWithPopups(THRU_MAIN_MAP_AND_ITEM_PROGRESS_H, 230.0, bottomc);

            // draw fake mice on top level
            Polygon addFakeMouse(Canvas c)
            {
                var fakeMouse = new Polygon { Fill = Brushes.White };
                fakeMouse.Points = new Points { new Point(0.0, 0.0), new Point(12.0, 6.0), new Point(6.0, 12.0) };
                c.Children.Add(fakeMouse);
                return fakeMouse;
            }
            var fakeTopMouse = addFakeMouse(topc);
            var fakeBottomMouse = addFakeMouse(bottomc);

            // set up the main broadcast window
            broadcastWindow.Height = H + 40.0;
            var dp = new DockPanel { Width = W, Height = H };
            DockPanel.SetDock(timeline, Dock.Bottom);
            dp.Children.Add(timeline);
            dp.Children.Add(topc);

            var factor = size == 1 ? 0.333333 : size == 2 ? 0.666666 : 1.0;   // only the initial window size; the content is always laid out at full size and scaled by the Viewbox below
            if (size == 1 || size == 2)
                broadcastWindow.Height = H * factor + 40.0;
            // (WPF: `broadcastTimeTextBox.LayoutTransform <- null`; Avalonia controls have no LayoutTransform, and nothing gives this one a transform, so there's nothing to clear)
            var c0 = new Canvas { Width = W, Height = H };
            c0.UseLayoutRounding = true;
            c0.Children.Add(dp);
            { var pc = (Canvas)OverworldItemGridUI.broadcastTimeTextBox.Parent; if (pc != null) pc.Children.Remove(OverworldItemGridUI.broadcastTimeTextBox); }  // deparent from prior window
            // the timer sits centered in the right-hand column of the control panel (the same spot as in the main window), clear of the Popout Windows button
            var bt = OverworldItemGridUI.broadcastTimeTextBox;
            bt.Width = 120.0; bt.Height = 44.0; bt.FontSize = 26.0; bt.TextAlignment = TextAlignment.Center; bt.VerticalContentAlignment = VerticalAlignment.Center; bt.Padding = new Thickness(0.0);
            canvasAdd(c0, bt, OverworldItemGridUI.OMTW * 16.0 - 200.0 - 44.0 + 114.0 + 13.0 - 10.0, 8.0);
            topBar.Width = W;
            c0.Children.Add(topBar);
            broadcastWindow.Content = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, Child = c0 };
            broadcastWindow.MinWidth = 160.0;
            broadcastWindow.MinHeight = 100.0;
            // remember the size the user gave this window
            var wh = System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.BroadcastWindowWH, @"^(\d+),(\d+)$");
            if (wh.Success)
            {
                broadcastWindow.Width = double.Parse(wh.Groups[1].Value, CultureInfo.InvariantCulture);
                broadcastWindow.Height = double.Parse(wh.Groups[2].Value, CultureInfo.InvariantCulture);
            }
            var saveSizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600.0) };
            saveSizeTimer.Tick += (_, _) =>
            {
                saveSizeTimer.Stop();
                if (broadcastWindow.WindowState == WindowState.Normal)
                {
                    // (the client size, as Popouts saves; WPF's Width/Height are the outer size)
                    TrackerModelOptions.BroadcastWindowWH = $"{(int)broadcastWindow.ClientSize.Width},{(int)broadcastWindow.ClientSize.Height}";
                    TrackerModelOptions.writeSettings();
                }
            };
            broadcastWindow.SizeChanged += (_, _) =>
            {
                updateTopBar();
                saveSizeTimer.Stop();
                saveSizeTimer.Start();
            };

            var isUpper = true;
            cm.RootCanvas.PointerMoved += (_, ea) =>   // we need RootCanvas to see mouse moving in popups
            {
                var mousePos = ea.GetPosition(appMainCanvas);
                if (mousePos.Y < THRU_MAIN_MAP_AND_ITEM_PROGRESS_H)
                {
                    if (!isUpper)
                    {
                        if (cm.PopupCanvasStack.Count == 0)  // don't switch panes if a popup is active
                        {
                            isUpper = true;
                            dp.Children.RemoveAt(1);
                            dp.Children.Add(topc);
                        }
                    }
                    Canvas.SetLeft(fakeTopMouse, mousePos.X);
                    Canvas.SetTop(fakeTopMouse, mousePos.Y);
                }
                else
                {
                    if (isUpper)
                    {
                        if (cm.PopupCanvasStack.Count == 0)  // don't switch panes if a popup is active
                        {
                            isUpper = false;
                            dp.Children.RemoveAt(1);
                            dp.Children.Add(bottomc);
                        }
                    }
                    Canvas.SetLeft(fakeBottomMouse, mousePos.X);
                    Canvas.SetTop(fakeBottomMouse, mousePos.Y - THRU_MAIN_MAP_AND_ITEM_PROGRESS_H + 230.0);
                }
            };
            return broadcastWindow;
        }
        Window broadcastWindow_ = null;
        if (TrackerModelOptions.ShowBroadcastWindow.Value)
        {
            broadcastWindow_ = makeBroadcastWindow(3, TrackerModelOptions.BroadcastWindowIncludesOverworldMagnifier.Value);
            ShowRunCustom.showOwned(broadcastWindow_);
            refocusMainWindow();
        }
        OptionsMenu.broadcastWindowOptionChanged += () =>
        {
            // close existing
            if (broadcastWindow_ != null)
            {
                broadcastWindow_.Close();
                broadcastWindow_ = null;
            }
            // maybe restart
            if (TrackerModelOptions.ShowBroadcastWindow.Value)
            {
                broadcastWindow_ = makeBroadcastWindow(3, TrackerModelOptions.BroadcastWindowIncludesOverworldMagnifier.Value);
                ShowRunCustom.showOwned(broadcastWindow_);
                refocusMainWindow();
            }
        };
    }
}
