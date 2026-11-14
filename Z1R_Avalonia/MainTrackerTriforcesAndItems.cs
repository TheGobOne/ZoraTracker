// Ported from Z1R_WPF/MainTrackerTriforcesAndItems.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// `open DungeonUI.AhhGlobalVariables` / `open OverworldItemGridUI` / `open CustomComboBoxes.GlobalFlag` are `using static`s (this file defines no canvasAdd/OMTW
// of its own, so nothing is ambiguous; hideLocator comes from AhhGlobalVariables, as in F#). `open HotKeys.MyKey` is the MyKeyAdd extension. WPF's
// UIElement/FrameworkElement are Control; mouse events are pointer events (see PROGRESS.md); `Visibility.Hidden` is Opacity 0 + not hit-testable.
// F# model events keep their F# type and are subscribed with `onEvent` (an FSharpHandler). The popout windows keep Left/Top in WPF units
// (ShowRunCustom.leftOf/topOf/setLeftTop) and save the client size, as Popouts.cs.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using static Z1R_Avalonia.DungeonUI.AhhGlobalVariables;
using static Z1R_Avalonia.OverworldItemGridUI;
using static Z1R_Avalonia.CustomComboBoxes.GlobalFlag;

namespace Z1R_Avalonia;

public static class MainTrackerTriforcesAndItems
{
    private static void onEvent<T>(IEvent<FSharpHandler<T>, T> e, Action<T> f) => e.AddHandler(new FSharpHandler<T>((_, x) => f(x)));
    private static double parseFloat(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    public static Canvas makeGhostBusterImpl(IBrush color, bool isForLinkRouting)  // for marking off the third box of completed 2-item dungeons in Hidden Dungeon Numbers
    {
        var c = new Canvas { Width = 30.0, Height = 30.0, Opacity = 0.0, IsHitTestVisible = false };
        var circle = new Ellipse { Width = 30.0, Height = 30.0, StrokeThickness = 3.0, Stroke = color };
        var slash = new Line { StartPoint = new Point(30.0 * (1.0 - 0.707), 30.0 * 0.707), EndPoint = new Point(30.0 * 0.707, 30.0 * (1.0 - 0.707)), StrokeThickness = 3.0, Stroke = color };
        canvasAdd(c, circle, 0.0, 0.0);
        canvasAdd(c, slash, 0.0, 0.0);
        if (isForLinkRouting)
        {
            var txt = new TextBox { FontSize = 10.0, Foreground = color, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), Text = "None\nfound" };
            Canvas.SetBottom(txt, 30.0);
            Canvas.SetLeft(txt, 0.0);
            c.Children.Add(txt);
        }
        return c;
    }
    public static Canvas makeGhostBuster() => makeGhostBusterImpl(Brushes.Gray, false);
    public static readonly Canvas[] mainTrackerGhostbusters = Enumerable.Range(0, 8).Select(_ => makeGhostBuster()).ToArray();
    public static void updateGhostBusters()
    {
        if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
        {
            for (int i = 0; i <= 7; i++)
            {
                var lc = TrackerModel.GetDungeon(i).LabelChar;
                var twoItemDungeons = TrackerModel.IsSecondQuestDungeons ? "123567" : "234567";
                if (twoItemDungeons.Contains(lc.ToString()))
                {
                    mainTrackerGhostbusters[i].Opacity = 1.0;
                    var boxes = TrackerModel.GetDungeon(i).Boxes;
                    if (boxes[1].PlayerHas().Equals(TrackerModel.PlayerHas.NO) && boxes[1].CellCurrent() == -1 &&            // middle box empty
                        (!boxes[2].PlayerHas().Equals(TrackerModel.PlayerHas.NO) || boxes[2].CellCurrent() != -1))      // bottom box non-empty
                    {
                        // swap the two on behalf of the player
                        var v = boxes[2].CellCurrent();
                        var ph = boxes[2].PlayerHas();
                        if (!(boxes[2].AttemptToSet(-1, TrackerModel.PlayerHas.NO)))
                            throw new Exception("impossible");
                        if (!(boxes[1].AttemptToSet(v, ph)))
                            throw new Exception("impossible");
                    }
                }
                else
                    mainTrackerGhostbusters[i].Opacity = 0.0;
            }
        }
    }

    public static readonly Canvas[] triforceInnerCanvases = new Canvas[8];
    public static readonly Canvas[,] mainTrackerCanvases = new Canvas[9, 5];
    public static readonly Canvas[,] mainTrackerCanvasShaders = make_mainTrackerCanvasShaders();
    private static Canvas[,] make_mainTrackerCanvasShaders()
    {
        var r = new Canvas[8, 5];
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 5; j++)
                r[i, j] = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black, Opacity = (j == 1 ? 0.4 : 0.3), IsHitTestVisible = false };
        return r;
    }
    public static readonly List<TimelineItem> timelineItems = new List<TimelineItem>();
    public static readonly Graphics.SharedEvent doUIUpdateEvent = new Graphics.SharedEvent();

    // the 'Item Locations' window: a read-only mirror of the dungeon columns (triforce and item boxes of levels 1-9), and below them the armos / letter / ladder / white sword / magical sword boxes
    public static Window itemLocationsWindow = null;
    public static readonly double ITEMLOC_MINIMIZED = ShowRunCustom.MINIMIZED_THRESHOLD;
    public static readonly string itemLocationsRegex = @"^(1|0),(-?\d+),(-?\d+)(?:,(\d+),(\d+)(?:,(0|1))?)?$";
    public static bool itemLocationsHorizontal = false;   // the window's layout: false = each dungeon is a column (Vertical), true = each dungeon is a row (Horizontal); remembered with the window's position
    // (port: WPF's `w.Left < ITEMLOC_MINIMIZED` is how Windows reports a minimized window; other systems leave it in place, so the window state is checked too, as Popouts.cs.
    // Avalonia has no RestoreBounds: the last bounds seen while the window was in its normal state stand in for it.)
    private sealed class RestoreBoundsTracker
    {
        private readonly Window w;
        private Rect restore = new Rect();
        public RestoreBoundsTracker(Window w) { this.w = w; }
        public bool MinimizedOrClosing() => ShowRunCustom.leftOf(w) < ITEMLOC_MINIMIZED || w.WindowState == WindowState.Minimized;
        public void Note()
        {
            if (w.WindowState == WindowState.Normal && !(ShowRunCustom.leftOf(w) < ITEMLOC_MINIMIZED))
                restore = new Rect(ShowRunCustom.leftOf(w), ShowRunCustom.topOf(w), w.ClientSize.Width, w.ClientSize.Height);
        }
        // (WPF: `if w.Left < ITEMLOC_MINIMIZED then w.RestoreBounds else Rect(w.Left, w.Top, w.ActualWidth, w.ActualHeight)`)
        public Rect Bounds()
        {
            Note();
            return MinimizedOrClosing() ? restore : new Rect(ShowRunCustom.leftOf(w), ShowRunCustom.topOf(w), w.ClientSize.Width, w.ClientSize.Height);
        }
    }
    // userClick=true: the button was pressed; false: automatic reopen at startup (only if the window was left open last time)
    public static void openItemLocationsWindowImpl(bool userClick)
    {
        var m = System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.ItemLocationsWindow_DisplayedLT, itemLocationsRegex);
        var validPos = m.Success && !(parseFloat(m.Groups[2].Value) < ITEMLOC_MINIMIZED);
        if (m.Success && m.Groups[6].Success) itemLocationsHorizontal = (m.Groups[6].Value == "1");
        if (itemLocationsWindow != null)
            itemLocationsWindow.Activate();
        else if (userClick || (validPos && m.Groups[1].Value == "1"))
        {
            // (WPF: ResizeMode=CanResizeWithGrip; Avalonia windows resize from their edges and corners)
            var w = new Window { Title = "Item Locations", CanResize = true, SizeToContent = SizeToContent.WidthAndHeight, Background = Brushes.Black, ShowActivated = false };
            var content = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black, Margin = new Thickness(6.0) };
            Canvas box30(Control view)
            {
                var c = new Canvas { Width = 30.0, Height = 30.0 };
                c.Children.Add(view);
                return c;
            }
            var layoutCB = new CheckBox { Margin = new Thickness(0.0, 6.0, 2.0, 0.0), HorizontalAlignment = HorizontalAlignment.Right, Cursor = new Cursor(StandardCursorType.Hand) };
            var layoutLabel = new TextBlock { Foreground = Brushes.Orange, FontSize = 12.0, Margin = new Thickness(2.0, 0.0, 0.0, 0.0) };
            layoutCB.Content = layoutLabel;
            void buildHorizontal()
            {
                // each dungeon is a row: its triforce, then its item boxes to the right (the armos / letter / ladder / sword boxes become a column on the far right, each icon beside its box)
                var left = new StackPanel { Orientation = Orientation.Vertical };
                for (int i = 0; i <= 8; i++)
                {
                    var d = TrackerModel.GetDungeon(i);
                    var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0.0, 3.0, 0.0, 3.0) };
                    if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
                        row.Children.Add(Views.MakeColorNumberCanvasForHDN(i));
                    var tf = (i < 8 ? Views.MakeTriforceDisplayView(null, i, null) : Views.MakeLevel9View(null));
                    tf.VerticalAlignment = VerticalAlignment.Center;
                    row.Children.Add(tf);
                    // (every dungeon's boxes sit left-justified in the same fixed-width area, so the first boxes line up in a clean column whether a dungeon has 2 or 3)
                    var boxesSP = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                    foreach (var box in d.Boxes)
                        boxesSP.Children.Add(box30(Views.MakeBoxItemWithExtraDecorations(null, box, false, null).Item1));
                    if (i == 1 && !(TrackerModel.IsHiddenDungeonNumbers.Invoke(null)) && TrackerModel.level2StairBoxEnabled)
                        boxesSP.Children.Add(box30(Views.MakeBoxItemWithExtraDecorations(null, TrackerModel.level2StairBox, false, null).Item1));
                    if (i == 8 && TrackerModel.level9FloorBoxEnabled)
                        boxesSP.Children.Add(box30(Views.MakeBoxItemWithExtraDecorations(null, d.ExtraFloorBox, false, null).Item1));
                    row.Children.Add(new Border { Width = 98.0, Child = boxesSP });
                    left.Children.Add(row);
                }
                var right = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(18.0, 0.0, 0.0, 0.0), VerticalAlignment = VerticalAlignment.Center };   // (centered against the dungeon rows)
                var letterBmp = CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.LETTER, null);
                foreach (var (box, icon) in new (TrackerModel.Box, System.Drawing.Bitmap)[] { (TrackerModel.armosBox, Graphics.ow_key_armos_bmp), (TrackerModel.letterBox, letterBmp), (TrackerModel.ladderBox, Graphics.ladder_bmp),
                                                                                               (TrackerModel.sword2Box, Graphics.ow_sword2_item_bmp), (TrackerModel.sword3Box, Graphics.ow_sword3_item_bmp) })
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0.0, 3.0, 0.0, 3.0) };
                    row.Children.Add(box30(Views.MakeBoxItemWithExtraDecorations(null, box, false, null).Item1));
                    var ic = new Canvas { Width = 30.0, Height = 30.0 };
                    canvasAdd(ic, Graphics.BMPtoImage(icon), 4.0, 4.0);
                    row.Children.Add(ic);
                    right.Children.Add(row);
                }
                layoutCB.Margin = new Thickness(0.0, 16.0, 0.0, 0.0);
                layoutCB.HorizontalAlignment = HorizontalAlignment.Center;
                right.Children.Add(layoutCB);   // (the layout switch is centered under the column of boxes)
                var both = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                both.Children.Add(left);
                both.Children.Add(right);
                content.Children.Add(both);
            }
            void buildVertical()
            {
                var row1 = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                for (int i = 0; i <= 8; i++)
                {
                    var d = TrackerModel.GetDungeon(i);
                    var col = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(3.0, 0.0, 3.0, 0.0) };
                    if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
                        col.Children.Add(Views.MakeColorNumberCanvasForHDN(i));
                    col.Children.Add(i < 8 ? Views.MakeTriforceDisplayView(null, i, null) : Views.MakeLevel9View(null));
                    foreach (var box in d.Boxes)
                        col.Children.Add(box30(Views.MakeBoxItemWithExtraDecorations(null, box, false, null).Item1));
                    if (i == 1 && !(TrackerModel.IsHiddenDungeonNumbers.Invoke(null)) && TrackerModel.level2StairBoxEnabled)
                        col.Children.Add(box30(Views.MakeBoxItemWithExtraDecorations(null, TrackerModel.level2StairBox, false, null).Item1));
                    if (i == 8 && TrackerModel.level9FloorBoxEnabled)
                        col.Children.Add(box30(Views.MakeBoxItemWithExtraDecorations(null, d.ExtraFloorBox, false, null).Item1));
                    row1.Children.Add(col);
                }
                content.Children.Add(row1);
                var row2 = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0.0, 12.0, 0.0, 0.0) };
                // (each box has its icon below it, as on the main tracker)
                var letterBmp = CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.LETTER, null);
                foreach (var (box, icon) in new (TrackerModel.Box, System.Drawing.Bitmap)[] { (TrackerModel.armosBox, Graphics.ow_key_armos_bmp), (TrackerModel.letterBox, letterBmp), (TrackerModel.ladderBox, Graphics.ladder_bmp),
                                                                                               (TrackerModel.sword2Box, Graphics.ow_sword2_item_bmp), (TrackerModel.sword3Box, Graphics.ow_sword3_item_bmp) })
                {
                    var col = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(3.0, 0.0, 3.0, 0.0) };
                    col.Children.Add(box30(Views.MakeBoxItemWithExtraDecorations(null, box, false, null).Item1));
                    var ic = new Canvas { Width = 30.0, Height = 30.0 };
                    canvasAdd(ic, Graphics.BMPtoImage(icon), 4.0, 4.0);
                    col.Children.Add(ic);
                    row2.Children.Add(col);
                }
                content.Children.Add(row2);
                // (the layout switch is centered under the boxes)
                layoutCB.Margin = new Thickness(0.0, 10.0, 0.0, 2.0);
                layoutCB.HorizontalAlignment = HorizontalAlignment.Center;
                content.Children.Add(layoutCB);
            }
            void build()
            {
                // (the switch is re-parented by each layout)
                if (layoutCB.Parent is Panel pn) pn.Children.Remove(layoutCB);
                layoutCB.VerticalAlignment = VerticalAlignment.Top;
                content.Children.Clear();
                // its label names the layout the window is in now, and clicking it switches to the other one
                layoutLabel.Text = (itemLocationsHorizontal ? "Horizontal" : "Vertical");
                layoutCB.IsChecked = itemLocationsHorizontal;
                if (itemLocationsHorizontal) buildHorizontal(); else buildVertical();
            }
            build();
            var vb = new Viewbox { Child = content, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
            w.Content = vb;
            // remembered size and position from the last session
            var (savedW, savedH) = (m.Success && m.Groups[4].Success && m.Groups[5].Success && parseFloat(m.Groups[4].Value) >= 100.0 && parseFloat(m.Groups[5].Value) >= 60.0) ? (parseFloat(m.Groups[4].Value), parseFloat(m.Groups[5].Value)) : (0.0, 0.0);
            if (savedW > 0.0)
            {
                w.SizeToContent = SizeToContent.Manual;
                w.Width = savedW;
                w.Height = savedH;
            }
            if (validPos)
            {
                w.WindowStartupLocation = WindowStartupLocation.Manual;
                ShowRunCustom.setLeftTop(w, parseFloat(m.Groups[2].Value), parseFloat(m.Groups[3].Value));
            }
            var mainClosing = false;
            var rb = new RestoreBoundsTracker(w);
            void save(bool isOpen)
            {
                var bounds = rb.Bounds();
                TrackerModelOptions.ItemLocationsWindow_DisplayedLT = $"{(isOpen ? 1 : 0)},{(int)bounds.Left},{(int)bounds.Top},{(int)bounds.Width},{(int)bounds.Height},{(itemLocationsHorizontal ? 1 : 0)}";
                TrackerModelOptions.writeSettings();
            }
            layoutCB.Click += (_, _) =>
            {
                itemLocationsHorizontal = !itemLocationsHorizontal;
                build();
                // the window re-fits its content for the new layout
                w.SizeToContent = SizeToContent.WidthAndHeight;
                vb.StretchDirection = StretchDirection.DownOnly;
                w.UpdateLayout();
                // (port: WPF reads the window's ActualWidth/Height after UpdateLayout; Avalonia applies a SizeToContent resize asynchronously, so the content's natural size is measured instead)
                vb.Measure(Size.Infinity);
                var (aw, ah) = (vb.DesiredSize.Width, vb.DesiredSize.Height);
                w.SizeToContent = SizeToContent.Manual;
                w.Width = aw;
                w.Height = ah;
                vb.StretchDirection = StretchDirection.Both;
                save(true);
            };
            var saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600.0) };
            saveTimer.Tick += (_, _) => { saveTimer.Stop(); save(true); };
            w.PositionChanged += (_, _) => { rb.Note(); if (w.IsLoaded) { saveTimer.Stop(); saveTimer.Start(); } };   // (WPF: LocationChanged)
            w.SizeChanged += (_, _) => { rb.Note(); if (w.IsLoaded && w.SizeToContent == SizeToContent.Manual) { saveTimer.Stop(); saveTimer.Start(); } };
            // opens at the content's natural size (unless a size was remembered), then the content scales as the window is resized
            w.Opened += (_, _) =>   // (WPF: ContentRendered)
            {
                if (w.SizeToContent != SizeToContent.Manual)
                {
                    var (aw, ah) = (w.ClientSize.Width, w.ClientSize.Height);
                    w.SizeToContent = SizeToContent.Manual;
                    w.Width = aw;
                    w.Height = ah;
                }
                vb.StretchDirection = StretchDirection.Both;   // (also when a remembered size was restored, otherwise it could never scale up)
            };
            // the item-slot options that add boxes can change while the window is open
            // (these handlers are removed when this window closes: each reopening used to add another set that stayed for the session)
            var stairHandler = new FSharpHandler<Microsoft.FSharp.Core.Unit>((_, _) => { if (itemLocationsWindow == w) build(); });
            var floorHandler = new FSharpHandler<Microsoft.FSharp.Core.Unit>((_, _) => { if (itemLocationsWindow == w) build(); });
            TrackerModel.level2StairBoxEnabledChanged.Publish.AddHandler(stairHandler);
            TrackerModel.level9FloorBoxEnabledChanged.Publish.AddHandler(floorHandler);
            var mainWin = ShowRunCustom.mainWindow();
            EventHandler onMainClosed = (_, _) => { mainClosing = true; try { w.Close(); } catch { } };   // (stays 'open' in the settings, so it reappears next time)
            mainWin.Closed += onMainClosed;
            w.Closed += (_, _) =>
            {
                TrackerModel.level2StairBoxEnabledChanged.Publish.RemoveHandler(stairHandler);
                TrackerModel.level9FloorBoxEnabledChanged.Publish.RemoveHandler(floorHandler);
                mainWin.Closed -= onMainClosed;
                itemLocationsWindow = null;
                saveTimer.Stop();
                if (!mainClosing) save(false);   // the user closed it themselves: don't reopen it next time
            };
            itemLocationsWindow = w;
            w.Show();
            if (userClick) save(true);
        }
    }

    public static void openItemLocationsWindow() => openItemLocationsWindowImpl(true);
    // called at startup (after the tracker is set up): reopens the window if it was left open when the program last closed
    public static void reopenItemLocationsWindowIfWasOpen() => openItemLocationsWindowImpl(false);

    // a popout window that mirrors the Item Progress bar (a live picture of the bar; set 'itemProgressSource' to the bar's canvas)
    public static Control itemProgressSource = null;
    public static Window itemProgressWindow = null;
    public static void openItemProgressWindowImpl(bool userClick)
    {
        var m = System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.ItemProgressWindow_DisplayedLT, itemLocationsRegex);
        var validPos = m.Success && !(parseFloat(m.Groups[2].Value) < ITEMLOC_MINIMIZED);
        if (itemProgressSource == null) { }
        else if (itemProgressWindow != null)
            itemProgressWindow.Activate();
        else if (userClick || (validPos && m.Groups[1].Value == "1"))
        {
            var (itemsW, itemsH) = (15.0 * 30.0, 30.0);
            // (a picture of the whole bar, shifted left so only the item slots show; the plain whole-visual brush is the reliable way to mirror a live element)
            // a brush maps the visual's *content bounds* onto the picture, so give the bar an invisible full-size child: then the bounds are always exactly the whole bar (1:1), however many items are showing
            if (itemProgressSource is Canvas c && !Equals(c.Tag, "mirrorPad"))
            {
                c.Tag = "mirrorPad";
                c.Children.Add(new Rectangle { Width = c.Width, Height = c.Height, Fill = Brushes.Transparent, IsHitTestVisible = false });
            }
            var full = new Rectangle { Width = itemProgressSource.Width, Height = itemsH, IsHitTestVisible = false, Fill = new VisualBrush { Visual = itemProgressSource, Stretch = Stretch.Fill } };
            var rect = new Canvas { Width = itemsW, Height = itemsH, ClipToBounds = true, Background = Brushes.Black, IsHitTestVisible = false };
            Canvas.SetLeft(full, -OverworldItemGridUI.ITEM_PROGRESS_FIRST_ITEM + 6.0);   // (+6 so the first icon isn't clipped at the left edge)
            rect.Children.Add(full);
            var vb = new Viewbox { Child = rect, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
            var w = new Window { Title = "Item Progress", CanResize = true, SizeToContent = SizeToContent.WidthAndHeight, Background = Brushes.Black, ShowActivated = false, Content = vb };
            var (savedW, savedH) = (m.Success && m.Groups[4].Success && m.Groups[5].Success && parseFloat(m.Groups[4].Value) >= 100.0 && parseFloat(m.Groups[5].Value) >= 30.0) ? (parseFloat(m.Groups[4].Value), parseFloat(m.Groups[5].Value)) : (0.0, 0.0);
            if (savedW > 0.0)
            {
                w.SizeToContent = SizeToContent.Manual;
                w.Width = savedW;
                w.Height = savedH;
            }
            if (validPos)
            {
                w.WindowStartupLocation = WindowStartupLocation.Manual;
                ShowRunCustom.setLeftTop(w, parseFloat(m.Groups[2].Value), parseFloat(m.Groups[3].Value));
            }
            var mainClosing = false;
            var rb = new RestoreBoundsTracker(w);
            void save(bool isOpen)
            {
                var bounds = rb.Bounds();
                TrackerModelOptions.ItemProgressWindow_DisplayedLT = $"{(isOpen ? 1 : 0)},{(int)bounds.Left},{(int)bounds.Top},{(int)bounds.Width},{(int)bounds.Height}";
                TrackerModelOptions.writeSettings();
            }
            var saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600.0) };
            saveTimer.Tick += (_, _) => { saveTimer.Stop(); save(true); };
            w.PositionChanged += (_, _) => { rb.Note(); if (w.IsLoaded) { saveTimer.Stop(); saveTimer.Start(); } };   // (WPF: LocationChanged)
            w.SizeChanged += (_, _) => { rb.Note(); if (w.IsLoaded && w.SizeToContent == SizeToContent.Manual) { saveTimer.Stop(); saveTimer.Start(); } };
            w.Opened += (_, _) =>   // (WPF: ContentRendered)
            {
                if (w.SizeToContent != SizeToContent.Manual)
                {
                    var (aw, ah) = (w.ClientSize.Width, w.ClientSize.Height);
                    w.SizeToContent = SizeToContent.Manual;
                    w.Width = aw;
                    w.Height = ah;
                }
                vb.StretchDirection = StretchDirection.Both;
            };
            var mainWin = ShowRunCustom.mainWindow();
            EventHandler onMainClosed = (_, _) => { mainClosing = true; try { w.Close(); } catch { } };   // (stays 'open' in the settings, so it reappears next time)
            mainWin.Closed += onMainClosed;
            w.Closed += (_, _) =>
            {
                mainWin.Closed -= onMainClosed;   // (removed with this window: each reopening used to add another one)
                itemProgressWindow = null;
                saveTimer.Stop();
                if (!mainClosing) save(false);   // the user closed it themselves: don't reopen it next time
            };
            itemProgressWindow = w;
            w.Show();
            if (userClick) save(true);
        }
    }
    public static void openItemProgressWindow() => openItemProgressWindowImpl(true);
    public static void reopenItemProgressWindowIfWasOpen() => openItemProgressWindowImpl(false);

    // (F#: returns `boxItemImpl, extrasImage, updateNumberedTriforceDisplayImpl, updateNumberedTriforceDisplayIfItExists`)
    public static (Func<TimelineID, TrackerModel.Box, bool, Canvas>, Image, Action<Canvas, int>, Action) setup(CustomComboBoxes.CanvasManager cm, OverworldData.OverworldInstance owInstance,
                                                                                                             Layout.IApplicationLayoutBase layout, TrackerModel.DungeonTrackerInstanceKind kind)
    {
        var mainTrackerGrid = makeGrid(9, 5, 30, 30);
        var mainTrackerCanvas = new Canvas();
        mainTrackerCanvas.Children.Add(mainTrackerGrid);
        layout.AddMainTracker(mainTrackerCanvas);

        // items (we draw these before drawing triforces, as triforce display can draw slightly atop the item boxes, when there's a triforce-specific-blocker drawn)
        var boxActivators = new Dictionary<TrackerModel.Box, Action>(ReferenceEqualityComparer.Instance);
        Canvas boxItemImpl(TimelineID tid, TrackerModel.Box box, bool requiresForceUpdate)
        {
            var (c, activate) = Views.MakeBoxItem(cm, box);
            boxActivators[box] = () => activate(0, new CustomComboBoxes.GridClickDismissalWarpReturn.WarpToCenter());
            onEvent(box.Changed, _ => { if (requiresForceUpdate) TrackerModel.forceUpdate(); });
            // an item box (coast, dungeon, etc.) holding one of these items and marked as obtained also checks off that item's box in the upper overworld item grid (and un-checks it again if removed)
            var gridLinks = new (int, TrackerModel.BoolProperty)[]
            {
                (TrackerModel.ITEMS.WOODARROW,    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodArrow),
                (TrackerModel.ITEMS.BLUERING,     TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueRing),
                (TrackerModel.ITEMS.BLUECANDLE,   TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle),
                (TrackerModel.ITEMS.WOODSWORD,    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword),
                (TrackerModel.ITEMS.MAGICALSWORD, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword),
            };
            int linkedItemId()
            {
                if (box.PlayerHas().Equals(TrackerModel.PlayerHas.YES))
                {
                    foreach (var (id, _) in gridLinks)
                        if (id == box.CellCurrent())
                            return id;
                    return -1;
                }
                else return -1;
            }
            var wasLinkedItemId = linkedItemId();
            var weCheckedIt = false;   // true only if this box itself checked the grid item (it was not already collected, e.g. by hand), so that is the only case we un-check it, and the timeline never gets a second stamp
            onEvent(box.Changed, _ =>
            {
                var now = linkedItemId();
                var was = wasLinkedItemId;
                if (now != was)
                {
                    wasLinkedItemId = now;
                    if (was >= 0 && weCheckedIt && TrackerModel.mapCollectedCount[was] == 0)
                    {
                        foreach (var (id, prop) in gridLinks)
                            if (id == was) prop.Set(false);
                    }
                    if (was >= 0) weCheckedIt = false;
                    // one timeline entry per item: if the item was already collected on its own, this box adds none; if this box collected it, the grid item adds none
                    string linkedTimelineName(int id)
                    {
                        if (id == TrackerModel.ITEMS.WOODARROW) return "WoodArrow"; else if (id == TrackerModel.ITEMS.BLUERING) return "BlueRing"; else if (id == TrackerModel.ITEMS.BLUECANDLE) return "BlueCandle";
                        else if (id == TrackerModel.ITEMS.WOODSWORD) return "WoodSword"; else return "MagicalSword";
                    }
                    TrackerModel.timelineSuppressedBoxes.Remove(box);
                    if (now >= 0)
                    {
                        foreach (var (id, prop) in gridLinks)
                        {
                            if (id == now)
                            {
                                if (!(prop.Value()))
                                {
                                    prop.Set(true);
                                    weCheckedIt = true;
                                    TrackerModel.TimelineItemModel.UnstampByIdentifier(linkedTimelineName(id));   // (this box's own entry stays)
                                }
                                else if (!TrackerModel.ProgressiveItems)   // (Progressive Items: a second copy is an upgrade, so this box keeps its own entry)
                                {
                                    TrackerModel.timelineSuppressedBoxes.Add(box);
                                    TrackerModel.TimelineItemModel.UnstampByIdentifier(tid.Identifier);   // (the item's own entry stays)
                                }
                            }
                        }
                    }
                    TrackerModel.recomputePlayerStateSummary();
                }
            });
            c.PointerEntered += (_, _) =>   // (WPF: MouseEnter)
            {
                switch (box.CellCurrent())
                {
                    case 3: showLocatorInstanceFunc(TrackerModel.owInstance.PowerBraceletable); break;
                    case 4: showLocatorInstanceFunc(TrackerModel.owInstance.Ladderable); break;
                    case 7: showLocatorInstanceFunc(TrackerModel.owInstance.Raftable); break;
                    case 8: showLocatorInstanceFunc(TrackerModel.owInstance.Whistleable); break;
                    case 9: showLocatorInstanceFunc(TrackerModel.owInstance.Burnable); break;
                    default: break;
                }
            };
            c.PointerExited += (_, _) => hideLocator();   // (WPF: MouseLeave)
            // putting a triforce piece in a dungeon's item box also checks off that dungeon's triforce, same as clicking its numbered triforce above
            // (the 1-or-4 third box is re-evaluated on every change, since toggling the dungeon quest moves it between dungeon 1 and dungeon 4)
            Func<int> dungeonIndexFnOpt;   // (F#: an option; null for None)
            if (tid.Tag == TimelineID.Tags.LevelBox && tid.Item1 <= 8) { var level = tid.Item1; dungeonIndexFnOpt = () => level - 1; }
            else if (tid.Tag == TimelineID.Tags.Level1or4Box3) dungeonIndexFnOpt = () => TrackerModel.IsSecondQuestDungeons ? 3 : 0;
            else dungeonIndexFnOpt = null;
            if (dungeonIndexFnOpt is Func<int> dungeonIndexFn)
            {
                // a triforce piece that is marked as obtained (green) checks the dungeon's triforce; changing/removing that item, or marking it not-obtained, unchecks it again
                bool isObtainedTriforcePiece() => box.CellCurrent() == TrackerModel.ITEMS.TRIFORCE && box.PlayerHas().Equals(TrackerModel.PlayerHas.YES);
                var wasObtainedTriforcePiece = isObtainedTriforcePiece();   // ref cell, since closures can't capture mutable locals
                var dungeonIndexWhenMarked = dungeonIndexFn();              // which dungeon we checked off, so un-marking unchecks that same one
                onEvent(box.Changed, _ =>
                {
                    var now = isObtainedTriforcePiece();
                    if (now && !wasObtainedTriforcePiece)
                    {
                        dungeonIndexWhenMarked = dungeonIndexFn();
                        var dungeon = TrackerModel.GetDungeon(dungeonIndexWhenMarked);
                        if (!(dungeon.PlayerHasTriforce()))
                            dungeon.ToggleTriforce();
                    }
                    else if (wasObtainedTriforcePiece && !now)
                    {
                        var dungeon = TrackerModel.GetDungeon(dungeonIndexWhenMarked);
                        if (dungeon.PlayerHasTriforce())
                            dungeon.ToggleTriforce();
                    }
                    wasObtainedTriforcePiece = now;
                });
            }
            timelineItems.Add(new TimelineItem(tid, () =>
            {
                // Progressive Items: a collected upgrade shows the tier it was collected at (e.g. the red ring)
                var f = TrackerModel.progressiveFamily(box.CellCurrent());
                int v = 0;
                if (f > 0 && box.PlayerHas().Equals(TrackerModel.PlayerHas.YES) && TrackerModel.progressiveBoxTier.TryGetValue(box, out v))
                    return (f == 1 ? Graphics.ow_progressive_tier_item_bmps[Math.Min(4, Math.Max(1, v))] : CustomComboBoxes.boxCurrentBMP(TrackerModel.progressiveFamilyIdent(f, Math.Min(2, Math.Max(1, v))), null));
                else
                    return CustomComboBoxes.boxCurrentBMP(box.CellCurrent(), tid);
            }));
            return c;
        }
        if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
        {
            for (int i = 0; i <= 8; i++)
            {
                for (int j = 0; j <= 2; j++)
                {
                    var c = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black };
                    gridAdd(mainTrackerGrid, c, i, j + 2);
                    if (j != 2 || i != 8)   // dungeon 9 does not have 3 items
                        canvasAdd(c, boxItemImpl(TimelineID.LevelBox(i + 1, j + 1), TrackerModel.GetDungeon(i).Boxes[j], false), 0.0, 0.0);
                    mainTrackerCanvases[i, j + 2] = c;
                    if (j == 2 && i != 8)
                        canvasAdd(c, mainTrackerGhostbusters[i], 0.0, 0.0);
                }
            }
        }
        else
        {
            for (int i = 0; i <= 8; i++)
            {
                for (int j = 0; j <= 2; j++)
                {
                    var c = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black };
                    gridAdd(mainTrackerGrid, c, i, j + 2);
                    if (j == 0 || j == 1 || i == 7)
                        canvasAdd(c, boxItemImpl(TimelineID.LevelBox(i + 1, j + 1), TrackerModel.GetDungeon(i).Boxes[j], false), 0.0, 0.0);
                    mainTrackerCanvases[i, j + 2] = c;
                }
            }
        }
        // optional staircase item slot under level 2: starts as a gray box (like the dungeon-quest toggle under level 4); click to turn it into a staircase item slot,
        // middle-click the slot to hide it again (which also clears whatever item was in it)
        if (!(TrackerModel.IsHiddenDungeonNumbers.Invoke(null)))
        {
            var l2c = mainTrackerCanvases[1, 4];
            var placeholder = new Canvas { Width = 30.0, Height = 30.0, Background = Graphics.freeze(new SolidColorBrush(Color.FromRgb(55, 55, 85))) };
            ToolTip.SetTip(placeholder, "Optional extra staircase item slot for level 2 (click to add it; middle-click it afterwards to remove it)");
            Views.appMainCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(placeholder);
            var slot = boxItemImpl(TimelineID.LevelBox(2, 3), TrackerModel.level2StairBox, false);
            ToolTip.SetTip(slot, "Optional extra staircase item slot (middle-click to remove it and clear its item)");
            void refreshL2Slot()
            {
                l2c.Children.Remove(placeholder);
                l2c.Children.Remove(slot);
                if (TrackerModel.level9FloorBoxEnabled || (!TrackerModel.level2StairBoxEnabled && !TrackerModelOptions.FourthSwordEnabled)) { }   // no 4th sword option: no extra slot. The 4th sword is in level 9: no level 2 staircase slot at all (not even the greyed-out placeholder)
                else if (TrackerModel.level2StairBoxEnabled) l2c.Children.Insert(0, slot); else l2c.Children.Insert(0, placeholder);
            }
            placeholder.PointerPressed += (_, ea) =>   // (WPF: MouseDown, ChangedButton = Left)
            {
                if (ea.GetCurrentPoint(placeholder).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
                {
                    ea.Handled = true;
                    TrackerModel.SetLevel2StairBoxEnabled(true);
                }
            };
            // tunneling handler on the container so it wins over the box's own middle-click behavior (which sets 'skipped')
            l2c.AddHandler(InputElement.PointerPressedEvent, new EventHandler<PointerPressedEventArgs>((_, ea) =>   // (WPF: PreviewMouseDown)
            {
                if (TrackerModel.level2StairBoxEnabled && ea.GetCurrentPoint(l2c).Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonPressed && !popupIsActive)
                {
                    ea.Handled = true;
                    TrackerModel.level2StairBox.Set(-1, TrackerModel.PlayerHas.NO);
                    TrackerModel.SetLevel2StairBoxEnabled(false);
                    TrackerModel.forceUpdate();
                }
            }), RoutingStrategies.Tunnel);
            onEvent(TrackerModel.level2StairBoxEnabledChanged.Publish, _ => refreshL2Slot());
            onEvent(TrackerModel.level9FloorBoxEnabledChanged.Publish, _ => refreshL2Slot());
            refreshL2Slot();
        }
        var extrasImage = Graphics.BMPtoImage(Graphics.iconExtras_bmp);
        ToolTip.SetTip(extrasImage, "Starting items and extra drops");
        ToolTip.SetPlacement(extrasImage, PlacementMode.Top);
        gridAdd(mainTrackerGrid, extrasImage, 8, 4);
        // hovering the button turns the blue part of the icon gold
        var extrasNormalSource = extrasImage.Source;
        IImage make_extrasGoldSource()
        {
            var gold = Graphics.transformColor(Graphics.iconExtras_bmp, c =>
            {
                if (c.A > 0 && c.B > c.R)
                {
                    var v = (int)c.B;
                    return System.Drawing.Color.FromArgb((int)c.A, v, (int)((double)v * 0.84), 0);
                }
                else return c;
            });
            return (Graphics.BMPtoImage(gold)).Source;
        }
        var extrasGoldSource = make_extrasGoldSource();
        extrasImage.PointerEntered += (_, _) => extrasImage.Source = extrasGoldSource;
        extrasImage.PointerExited += (_, _) => extrasImage.Source = extrasNormalSource;
        var IDEAL = new Point(Views.IDEAL_BOX_MOUSE_X, Views.IDEAL_BOX_MOUSE_Y);
        extrasImage.MyKeyAdd(ea =>
        {
            if (HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key, out var target))
            {
                switch (target)
                {
                    case HotKeys.GlobalHotkeyTargets.MoveCursorLeft:
                        ea.Handled = true;
                        Graphics.NavigationallyWarpMouseCursorTo(mainTrackerCanvases[7, 4].TranslatePoint(IDEAL, cm.AppMainCanvas).Value);
                        break;
                    case HotKeys.GlobalHotkeyTargets.MoveCursorUp:
                        ea.Handled = true;
                        Graphics.NavigationallyWarpMouseCursorTo(mainTrackerCanvases[8, 3].TranslatePoint(IDEAL, cm.AppMainCanvas).Value);
                        break;
                    default: break;
                }
            }
        });
        // (no hover box on the extras icon: the turquoise highlight was removed here)
        var finalCanvasOf1Or4 =
            TrackerModel.IsHiddenDungeonNumbers.Invoke(null)
                ? null
                : boxItemImpl(TimelineID.Level1or4Box3, TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.FinalBoxOf1Or4, false);
        Canvas make_toggleSecondQuestDungeonCanvas()
        {
            if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
                return null;
            var c = new Canvas { Width = 30.0, Height = 30.0, Background = Graphics.freeze(new SolidColorBrush(Color.FromRgb(55, 55, 85))) };
            var rect = new Rectangle { Width = 30.0, Height = 30.0, Stroke = Brushes.White, StrokeThickness = 3.0, Opacity = 0.0 };
            c.Children.Add(rect);
            var pf = new PathFigure { StartPoint = new Point(0.0, 20.0), IsClosed = false,
                                      Segments = new PathSegments { new BezierSegment { Point1 = new Point(0.0, 10.0), Point2 = new Point(60.0, 10.0), Point3 = new Point(60.0, 20.0), IsStroked = true } } };
            var curve = new Avalonia.Controls.Shapes.Path { Stroke = Brushes.White, StrokeThickness = 3.0, IsHitTestVisible = false, Data = new PathGeometry { Figures = new PathFigures { pf } }, Opacity = 0.0 };
            var tb1 = new TextBox { IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 9.0, Margin = new Thickness(0.0),
                                    VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center,
                                    Text = "click to toggle", Foreground = Brushes.White, Background = Brushes.Black, Opacity = 0.0 };
            var tb2 = new TextBox { IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 9.0, Margin = new Thickness(0.0),
                                    VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center,
                                    Text = "dungeon quest", Foreground = Brushes.White, Background = Brushes.Black, Opacity = 0.0 };
            var explainer = new TextBox { IsHitTestVisible = false, BorderThickness = new Thickness(3.0), BorderBrush = Brushes.Gray, FontSize = 14.0, Margin = new Thickness(0.0),
                                          Text = "Usually dungeon 1 has three items and dungeon 4 has two.\n" +
                                          "However if you are playing with the 'second quest dungeons' flag\n" +
                                          "then dungeon 4 gets a third item and dungeon 1 will only have 2.\n" +
                                          "This toggle is used to decide which dungeon (1 or 4) gets the third box.",
                                          Padding = new Thickness(3.0), Foreground = Brushes.Orange, Background = Brushes.Black };
            canvasAdd(mainTrackerCanvas, curve, 30.0, 118.0);
            canvasAdd(mainTrackerCanvas, tb1, 30.0, 115.0);
            canvasAdd(mainTrackerCanvas, tb2, 28.0, 137.0);
            c.PointerEntered += (_, _) =>
            {
                rect.Opacity = 1.0;
                curve.Opacity = 1.0;
                tb1.Opacity = 1.0;
                tb2.Opacity = 1.0;
                layout.AddTopLayerHover(explainer, 140.0, 54.0);
            };
            c.PointerExited += (_, _) =>
            {
                rect.Opacity = 0.0;
                curve.Opacity = 0.0;
                tb1.Opacity = 0.0;
                tb2.Opacity = 0.0;
                layout.ClearTopLayerHovers();
            };
            c.PointerPressed += (_, _) =>   // (WPF: MouseDown, any button)
            {
                TrackerModel.IsSecondQuestDungeons = !TrackerModel.IsSecondQuestDungeons;
                rect.Opacity = 0.0;
                curve.Opacity = 0.0;
                tb1.Opacity = 0.0;
                tb2.Opacity = 0.0;
                layout.ClearTopLayerHovers();
                OptionsMenu.triggerSecondQuestDungeonsOptionChanged();
            };
            return c;
        }
        var toggleSecondQuestDungeonCanvas = make_toggleSecondQuestDungeonCanvas();
        // hovering a dungeon's hint box (the two-letter 'zora hint' above its triforce) shows the hint 3x larger in a popup below it
        void addHintZoomOnHover(Control fe, int i)
        {
            fe.PointerEntered += (_, _) =>
            {
                var hz = TrackerModel.GetLevelHint.Invoke(i);
                if (!hz.Equals(TrackerModel.HintZone.UNKNOWN) && !popupIsActive)
                {
                    var inner = OverworldItemGridUI.HintZoneDisplayTextBox(hz.AsDisplayTwoChars());
                    var vb = new Viewbox { Width = 72.0, Height = 72.0, Stretch = Stretch.Uniform, Child = inner };
                    var b = new Border { BorderBrush = OverworldItemGridUI.hintyBrush, BorderThickness = new Thickness(3.0), Background = Brushes.Black, Child = vb, IsHitTestVisible = false };
                    layout.ClearTopLayerHovers();
                    layout.AddTopLayerHover(b, Math.Max(0.0, (double)(30 * i) - 24.0), 34.0);
                }
            };
            fe.PointerExited += (_, _) => layout.ClearTopLayerHovers();
            fe.PointerPressed += (_, _) => layout.ClearTopLayerHovers();
        }
        // numbered triforce display - the extra row of triforce in IsHiddenDungeonNumbers
        void updateNumberedTriforceDisplayImpl(Canvas c, int i)
        {
            var (hasTriforce, index) = TrackerModel.doesPlayerHaveTriforceAndWhichDungeonIndexIsIt(i);
            var found = index == -1 ? false : TrackerModel.GetDungeon(index).HasBeenLocated();
            var hasHint = !(found) && !TrackerModel.GetLevelHint.Invoke(i).Equals(TrackerModel.HintZone.UNKNOWN);
            c.Children.Clear();
            if (hasHint)
                c.Children.Add(makeHintHighlight(30.0));
            if (!hasTriforce)
            {
                if (!found)
                    c.Children.Add(Graphics.BMPtoImage(Graphics.emptyUnfoundNumberedTriforce_bmps[i]));
                else
                    c.Children.Add(Graphics.BMPtoImage(Graphics.emptyFoundNumberedTriforce_bmps[i]));
            }
            else
            {
                if (!found)
                    c.Children.Add(Graphics.BMPtoImage(Graphics.fullNumberedUnfoundTriforce_bmps[i]));
                else
                    c.Children.Add(Graphics.BMPtoImage(Graphics.fullNumberedFoundTriforce_bmps[i]));
            }
        }
        Action make_updateNumberedTriforceDisplayIfItExists()
        {
#pragma warning disable CS0162   // (unreachable code, as in WPF)
            if (false && TrackerModel.IsHiddenDungeonNumbers.Invoke(null))   // (the extra row of numbered triforces above the armos/letter/etc. item boxes is no longer shown with Hide Dungeon Numbers)
            {
                var numberedTriforceCanvases = Enumerable.Range(0, 8).Select(_ => new Canvas { Width = 30.0, Height = 30.0 }).ToArray();
                for (int i = 0; i <= 7; i++)
                {
                    var c = numberedTriforceCanvases[i];
                    layout.AddNumberedTriforceCanvas(c, i);
                    var ii = i;
                    c.PointerEntered += (_, _) => showLocator(new ShowLocatorDescriptor.DungeonNumber(ii));
                    c.PointerExited += (_, _) => hideLocator();
                }
                void update()
                {
                    for (int i = 0; i <= 7; i++)
                        updateNumberedTriforceDisplayImpl(numberedTriforceCanvases[i], i);
                }
                return update;
            }
            else
                return () => { };
#pragma warning restore CS0162
        }
        var updateNumberedTriforceDisplayIfItExists = make_updateNumberedTriforceDisplayIfItExists();
        updateNumberedTriforceDisplayIfItExists();
        // triforce
        for (int i0 = 0; i0 <= 7; i0++)
        {
            var i = i0;   // (each closure keeps its own i, as in F#)
            if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
            {
                // triforce dungeon color
                var colorCanvas = new Canvas { Width = 28.0, Height = 28.0, Background = Brushes.Black };
                //mainTrackerCanvases.[i,0] <- colorCanvas
                var colorButton = new Button { Width = 30.0, Height = 30.0, BorderThickness = new Thickness(1.0), Margin = new Thickness(0.0), Padding = new Thickness(0.0), BorderBrush = Brushes.DimGray, Content = colorCanvas };
                colorButton.Click += (_, _) =>
                {
                    if (!popupIsActive && TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
                    {
                        popupIsActive = true;
                        var pos = colorButton.TranslatePoint(new Point(15.0, 15.0), cm.AppMainCanvas).Value;
                        async Task run()
                        {
                            await Dungeon.HiddenDungeonCustomizerPopup(cm, i, TrackerModel.GetDungeon(i).Color, TrackerModel.GetDungeon(i).LabelChar, false, false, pos);
                            popupIsActive = false;
                        }
                        CustomComboBoxes.StartImmediate(run());
                    }
                };
                gridAdd(mainTrackerGrid, colorButton, i, 0);
                var dungeon = TrackerModel.GetDungeon(i);
                Dungeon.HotKeyAHiddenDungeonLabel(colorCanvas, dungeon, null);
                onEvent(dungeon.HiddenDungeonColorOrLabelChanged, t =>
                {
                    var (color, labelChar) = t;
                    colorCanvas.Background = Graphics.freeze(new SolidColorBrush(Graphics.makeColor(color)));
                    colorCanvas.Children.Clear();
                    var color2 = Graphics.isBlackGoodContrast(color) ? System.Drawing.Color.Black : System.Drawing.Color.White;
                    if (TrackerModel.GetDungeon(i).LabelChar != '?')  // ? and 7 look alike, and also it is easier to parse 'blank' as unknown/unset dungeon number
                        colorCanvas.Children.Add(Graphics.BMPtoImage(Graphics.alphaNumOnTransparentBmp(labelChar, color2, 28, 28, 3, 2)));
                });
                colorButton.PointerEntered += (_, _) => showLocator(new ShowLocatorDescriptor.DungeonIndex(i));
                colorButton.PointerExited += (_, _) => hideLocator();
            }
            else
            {
                var hintCanvas = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Transparent };  // Background to accept mouse input
                OverworldItemGridUI.RedrawHintCanvas(hintCanvas, TrackerModel.GetLevelHint.Invoke(i));
                onEvent(TrackerModel.LevelHintChanged.Invoke(i), hz => OverworldItemGridUI.RedrawHintCanvas(hintCanvas, hz));
                OverworldItemGridUI.ApplyFastHintSelectorBehavior(cm, ((double)(30 * i), 0.0), hintCanvas, i, true, new ShowLocatorDescriptor.DungeonIndex(i), false);
                addHintZoomOnHover(hintCanvas, i);
                mainTrackerCanvases[i, 0] = hintCanvas;
                Views.appMainCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(hintCanvas);
                gridAdd(mainTrackerGrid, hintCanvas, i, 0);
            }
            // triforce itself and label
            {
                var c = new Canvas { Width = 30.0, Height = 30.0 };
                mainTrackerCanvases[i, 1] = c;
                var innerc = Views.MakeTriforceDisplayView(cm, i, owInstance);
                triforceInnerCanvases[i] = innerc;
                Canvas.SetLeft(innerc, -1.0);   // nudged 1px left
                c.Children.Add(innerc);
                c.PointerEntered += (_, _) => showLocator(new ShowLocatorDescriptor.DungeonIndex(i));
                c.PointerExited += (_, _) => hideLocator();
                gridAdd(mainTrackerGrid, c, i, 1);
                timelineItems.Add(new TimelineItem(TimelineID.Triforce(i + 1), () =>
                {
                    switch (kind.Tag)
                    {
                        case TrackerModel.DungeonTrackerInstanceKind.Tags.DEFAULT: return Graphics.fullNumberedFoundTriforce_bmps[i];
                        case TrackerModel.DungeonTrackerInstanceKind.Tags.HIDE_DUNGEON_NUMBERS:
                            if (TrackerModel.GetDungeon(i).LabelChar != '?')
                            {
                                var num = (int)(TrackerModel.GetDungeon(i).LabelChar) - (int)'1';
                                return Graphics.fullNumberedFoundTriforce_bmps[num];
                            }
                            else
                                return Graphics.fullLetteredFoundTriforce_bmps[i];
                        default: throw new InvalidOperationException("unknown DungeonTrackerInstanceKind");
                    }
                }));
            }
        }
        var level9NumeralCanvas = Views.MakeLevel9View(owInstance);
        gridAdd(mainTrackerGrid, level9NumeralCanvas, 8, 1);
        mainTrackerCanvases[8, 1] = level9NumeralCanvas;
        level9NumeralCanvas.PointerEntered += (_, _) => showLocator(new ShowLocatorDescriptor.DungeonIndex(8));
        level9NumeralCanvas.PointerExited += (_, _) => hideLocator();
        // dungeon 9 doesn't need a color, we display a 'found summary' here instead (except we don't any more)
        var level9ColorCanvas = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black };
        if (!(TrackerModel.IsHiddenDungeonNumbers.Invoke(null)))
        {
            OverworldItemGridUI.RedrawHintCanvas(level9ColorCanvas, TrackerModel.GetLevelHint.Invoke(8));
            onEvent(TrackerModel.LevelHintChanged.Invoke(8), hz => OverworldItemGridUI.RedrawHintCanvas(level9ColorCanvas, hz));
            OverworldItemGridUI.ApplyFastHintSelectorBehavior(cm, ((double)(30 * 8), 0.0), level9ColorCanvas, 8, true, new ShowLocatorDescriptor.DungeonIndex(8), false);
            addHintZoomOnHover(level9ColorCanvas, 8);
            Views.appMainCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(level9ColorCanvas);
        }
        gridAdd(mainTrackerGrid, level9ColorCanvas, 8, 0);
        mainTrackerCanvases[8, 0] = level9ColorCanvas;
        // optional level 9 floor item check (the '4th Sword' start option can place the 4th sword there instead of the level 2 staircase): it takes the bottom cell under level 9
        {
            Canvas l9slot = null;
            Canvas l9host = null;   // its own cell host, placed straight into the grid (row 4 of the level 9 column) on top of everything else there
            void refreshL9Slot()
            {
                var enabled = TrackerModel.level9FloorBoxEnabled;
                if (enabled)
                {
                    if (l9slot == null)
                    {
                        l9slot = boxItemImpl(TimelineID.LevelBox(9, 3), TrackerModel.GetDungeon(8).ExtraFloorBox, false);
                        l9host = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black };
                        l9host.Children.Add(l9slot);
                        l9host.ZIndex = 50;
                    }
                    if (!(mainTrackerGrid.Children.Contains(l9host)))
                        gridAdd(mainTrackerGrid, l9host, 8, 4);
                }
                else if (l9host != null)
                    mainTrackerGrid.Children.Remove(l9host);
            }
            onEvent(TrackerModel.level9FloorBoxEnabledChanged.Publish, _ => refreshL9Slot());
            refreshL9Slot();
        }
        // Level 9 entry rule, in the empty cells of the bottom row (under levels 5-7): "9 ? = [triforce] x8".
        // The default is all 8 triforces; right-click the triforce to pick a different item that opens level 9. Once that item is collected, the go-time reminders play.
        if (kind.Equals(TrackerModel.DungeonTrackerInstanceKind.DEFAULT))
        {
            var entry = new Canvas { Width = 90.0, Height = 30.0, Background = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            // the 9: just the 9 in its yellow box
            var nine = Graphics.BMPtoImage(Graphics.theInteriorBmpTable[8][0]);
            nine.Stretch = Stretch.Uniform;
            nine.Width = 10.0;
            nine.Height = 18.0;
            RenderOptions.SetBitmapInterpolationMode(nine, BitmapInterpolationMode.None);
            var nineHost = new Grid { Width = 10.0, Height = 18.0 };
            nineHost.Children.Add(nine);
            canvasAdd(entry, nineHost, 3.0, 6.0);
            TextBlock mkText(string s) => new TextBlock { Text = s, Foreground = Brushes.Orange, FontSize = 18.0, IsHitTestVisible = false };
            var eqText = mkText("=");
            canvasAdd(entry, eqText, 17.0, 3.0);
            // the entry requirement: the 8-triforce default, or the item the user chose
            var trigArea = new Canvas { Width = 44.0, Height = 30.0, Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand) };
            ToolTip.SetTip(trigArea, "What it takes to enter level 9 (default: all 8 triforces; scroll to change the number).\nRight-click to choose a different item that opens level 9;\nthe go-time reminders play once it is collected.");
            canvasAdd(entry, trigArea, 32.0, 0.0);
            void redrawEntry()
            {
                trigArea.Children.Clear();
                var item = TrackerModel.level9CustomTriggerItem();
                // center the whole "9 = ..." group in the open space (it is narrower once an item replaces the triforce and "xN")
                var contentW = 50.0;
                if (item == -1)
                {
                    var img = Graphics.BMPtoImage(Graphics.triforce_item_bmp);
                    img.Width = 21.0;
                    img.Height = 21.0;
                    canvasAdd(trigArea, img, 0.0, 4.0);
                    var x8 = new TextBlock { Text = $"x{TrackerModel.level9TriforceCount}", Foreground = Brushes.Orange, FontSize = 14.0, IsHitTestVisible = false };
                    canvasAdd(trigArea, x8, 22.0, 7.0);
                    x8.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    contentW = 32.0 + 22.0 + x8.DesiredSize.Width - 3.0;   // the real width of the "xN" text
                }
                else
                {
                    // (the progressive sword shows the tier it had when picked, not the live tier, so upgrades never change this box)
                    var bmp = item == TrackerModel.ITEMS.PROGRESSIVESWORD ? Graphics.ow_progressive_tier_item_bmps[Math.Max(1, Math.Min(4, TrackerModel.level9TriggerSwordTier))] : CustomComboBoxes.boxCurrentBMP(item, null);
                    var img = Graphics.BMPtoImage(bmp);
                    img.Width = 21.0;
                    img.Height = 21.0;
                    canvasAdd(trigArea, img, 0.0, 4.0);
                }
                var shift = (90.0 - contentW) / 2.0 - 3.0;
                Canvas.SetLeft(nineHost, 3.0 + shift);
                Canvas.SetLeft(eqText, 17.0 + shift);
                Canvas.SetLeft(trigArea, 32.0 + shift);
            }
            redrawEntry();
            Views.redrawBoxes.Add(redrawEntry);
            onEvent(TrackerModel.level9TriggerBox.Changed, _ => redrawEntry());
            // scrolling the triforce (default rule) changes how many triforces are needed
            trigArea.PointerWheelChanged += (s, ea) =>   // (WPF: MouseWheel)
            {
                ea.Handled = true;
                var notch = Graphics.wheelNotch(s, ea);
                if (notch == 0) return;
                if (!popupIsActive && TrackerModel.level9CustomTriggerItem() == -1)
                {
                    var n = TrackerModel.level9TriforceCount + (notch > 0 ? 1 : -1);
                    TrackerModel.level9TriforceCount = Math.Max(0, Math.Min(8, n));   // (0 = level 9 needs no triforces)
                    redrawEntry();
                    TrackerModel.forceUpdate();
                }
            };
            trigArea.PointerPressed += (_, ea) =>   // (WPF: MouseRightButtonDown)
            {
                if (ea.GetCurrentPoint(trigArea).Properties.PointerUpdateKind != PointerUpdateKind.RightButtonPressed) return;
                ea.Handled = true;
                if (!popupIsActive)
                {
                    var pos = trigArea.TranslatePoint(new Point(0.0, 0.0), cm.AppMainCanvas).Value;
                    popupIsActive = true;
                    async Task run()
                    {
                        CustomComboBoxes.allItemsSelectable = true;   // this box is only a note: any item can be chosen
                        try
                        {
                            await OverworldMapTileCustomization.DoLocalItemComboBox(cm, 0, TrackerModel.level9TriggerBox, pos);
                        }
                        finally
                        {
                            CustomComboBoxes.allItemsSelectable = false;
                        }
                        if (TrackerModel.level9TriggerBox.CellCurrent() == TrackerModel.ITEMS.PROGRESSIVESWORD) TrackerModel.level9TriggerSwordTier = Math.Max(1, TrackerModel.progressiveSwordTier());
                        // this spot is only a note of what it takes to enter level 9: it is never itself "collected" (so it counts for nothing)
                        if (!TrackerModel.level9TriggerBox.PlayerHas().Equals(TrackerModel.PlayerHas.SKIPPED))
                        {
                            TrackerModel.level9TriggerBox.SetPlayerHas(TrackerModel.PlayerHas.SKIPPED);
                            TrackerModel.recomputePlayerStateSummary();
                            TrackerModel.forceUpdate();
                        }
                        popupIsActive = false;
                        redrawEntry();
                    }
                    CustomComboBoxes.StartImmediate(run());
                }
            };
            gridAdd(mainTrackerGrid, entry, 4, 4);
            Grid.SetColumnSpan(entry, 3);
        }
        // 'Item Locations' button: the top button of the Save / Hint Decoder / Hotkeys column (the layout sizes and places it)
        {
            // (made with Graphics.makeButton, so it has the same look and border as the other tracker buttons; use that helper for new buttons)
            var btn = Graphics.makeButton("Popout Windows", 12.0, Brushes.Orange);
            if (btn.Content is TextBox t) t.Background = Brushes.Black;   // same black as the Save / Hint Decoder / Hotkeys buttons
            btn.Focusable = false;
            ToolTip.SetTip(btn, "Choose which popout windows to show (they are remembered for next time)");
            btn.Click += (_, _) =>
            {
                if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                {
                    CustomComboBoxes.GlobalFlag.popupIsActive = true;
                    var pos = btn.TranslatePoint(new Point(0.0, btn.Bounds.Height + 4.0), cm.AppMainCanvas).Value;
                    async Task run()
                    {
                        var wh = new ManualResetEvent(false);
                        var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(6.0) };
                        sp.Children.Add(new TextBox { Text = "Popout Windows", IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 16.0, FontWeight = FontWeight.Bold,
                                                      Foreground = Brushes.Orange, Background = Brushes.Black, Margin = new Thickness(4.0, 2.0, 4.0, 6.0) });
                        void entry(string text, Func<bool> isOpen, Action openIt, Action closeIt)
                        {
                            var cb = PieMenus.makeOrangeCheckBox(text, isOpen());
                            cb.Click += (_, _) =>
                            {
                                if (cb.IsChecked == true) openIt(); else closeIt();
                                cb.IsChecked = isOpen();
                            };
                            sp.Children.Add(cb);
                        }
                        entry("Current Equipment", () => Popouts.isPopoutOpen(Popouts.PopoutKind.InventoryAndHeartsPopout), () => Popouts.openPopout(Popouts.PopoutKind.InventoryAndHeartsPopout), () => Popouts.closePopout(Popouts.PopoutKind.InventoryAndHeartsPopout));
                        entry("Item Locations", () => itemLocationsWindow != null, () => openItemLocationsWindow(), () => { if (itemLocationsWindow != null) itemLocationsWindow.Close(); });
                        entry("Item Progress", () => itemProgressWindow != null, () => openItemProgressWindow(), () => { if (itemProgressWindow != null) itemProgressWindow.Close(); });
                        entry("Spot Summary", () => Popouts.isPopoutOpen(Popouts.PopoutKind.SpotSummaryPopout), () => Popouts.openPopout(Popouts.PopoutKind.SpotSummaryPopout), () => Popouts.closePopout(Popouts.PopoutKind.SpotSummaryPopout));
                        entry("Uncollected Items", () => Popouts.isPopoutOpen(Popouts.PopoutKind.RemainingItemsPoput), () => Popouts.openPopout(Popouts.PopoutKind.RemainingItemsPoput), () => Popouts.closePopout(Popouts.PopoutKind.RemainingItemsPoput));
                        sp.Children.Add(new DockPanel { Background = Brushes.Gray, Height = 1.0, Margin = new Thickness(6.0, 6.0, 6.0, 4.0) });
                        var showCustomCb = PieMenus.makeOrangeCheckBox("Show Custom", ShowRunCustom.areShowWindowsOpen());
                        showCustomCb.Click += (_, _) =>
                        {
                            if (showCustomCb.IsChecked == true) ShowRunCustom.DoShowRunCustom(Popouts.refocusMainWindow); else ShowRunCustom.closeShowWindows();
                            showCustomCb.IsChecked = ShowRunCustom.areShowWindowsOpen();
                            TrackerModelOptions.ShowCustomOnStartup = (showCustomCb.IsChecked == true);   // remembered for next time
                            TrackerModelOptions.writeSettings();
                        };
                        sp.Children.Add(showCustomCb);
                        var b = new Border { Child = sp, Background = Brushes.Black, BorderThickness = new Thickness(3.0), BorderBrush = Brushes.Gray };
                        await CustomComboBoxes.DoModal(cm, wh, pos.X, pos.Y, b);
                        CustomComboBoxes.GlobalFlag.popupIsActive = false;
                    }
                    CustomComboBoxes.StartImmediate(run());
                }
            };
            layout.AddItemLocationsButton(btn);
        }
/*
        let foundDungeonsTB1 = new TextBox(Text="0/9", FontSize=20., Background=Brushes.Black,Foreground=Brushes.Orange,BorderThickness=Thickness(0.0),IsReadOnly=true,IsHitTestVisible=false)
        let foundDungeonsTB2 = new TextBox(Text="found", FontSize=12., Background=Brushes.Black,Foreground=Brushes.Orange,BorderThickness=Thickness(0.0),IsReadOnly=true,IsHitTestVisible=false)
        canvasAdd(level9ColorCanvas, foundDungeonsTB1, 4., -6.)
        canvasAdd(level9ColorCanvas, foundDungeonsTB2, 4., 16.)
*/
        for (int i0 = 0; i0 <= mainTrackerCanvases.GetLength(0) - 1; i0++)
        {
            for (int j0 = 0; j0 <= mainTrackerCanvases.GetLength(1) - 1; j0++)
            {
                var i = i0; var j = j0;   // (each closure keeps its own i, j, as in F#)
                if (mainTrackerCanvases[i, j] != null)
                {
                    mainTrackerCanvases[i, j].MyKeyAdd(ea =>
                    {
                        if (HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key, out var target))
                        {
                            switch (target)
                            {
                                case HotKeys.GlobalHotkeyTargets.MoveCursorRight:
                                    if (i < mainTrackerCanvases.GetLength(0) - 1)
                                    {
                                        ea.Handled = true;
                                        Graphics.NavigationallyWarpMouseCursorTo(mainTrackerCanvases[i + 1, j].TranslatePoint(IDEAL, cm.AppMainCanvas).Value);
                                    }
                                    // else unhandled, so app global handler will nudge mouse right
                                    break;
                                case HotKeys.GlobalHotkeyTargets.MoveCursorLeft:
                                    ea.Handled = true;
                                    if (i > 0)
                                        Graphics.NavigationallyWarpMouseCursorTo(mainTrackerCanvases[i - 1, j].TranslatePoint(IDEAL, cm.AppMainCanvas).Value);
                                    break;
                                case HotKeys.GlobalHotkeyTargets.MoveCursorUp:
                                    ea.Handled = true;
                                    if (j > 0)
                                        Graphics.NavigationallyWarpMouseCursorTo(mainTrackerCanvases[i, j - 1].TranslatePoint(IDEAL, cm.AppMainCanvas).Value);
                                    break;
                                case HotKeys.GlobalHotkeyTargets.MoveCursorDown:
                                    if (j < 4)
                                    {
                                        ea.Handled = true;
                                        Graphics.NavigationallyWarpMouseCursorTo(mainTrackerCanvases[i, j + 1].TranslatePoint(IDEAL, cm.AppMainCanvas).Value);
                                    }
                                    // else unhandled, so app global handler will nudge mouse down
                                    break;
                                default: break;
                            }
                        }
                    });
                }
            }
        }
/*
        let updateFoundDungeonsCount() =
            let mutable r = 0
            for trackerIndex = 0 to 8 do
                let d = TrackerModel.GetDungeon(trackerIndex)
                if d.HasBeenLocated() then
                    r <- r + 1
            foundDungeonsTB1.Text <- sprintf "%d/9" r
        for trackerIndex = 0 to 8 do
            let d = TrackerModel.GetDungeon(trackerIndex)
            d.HasBeenLocatedChanged.Add(fun _ -> updateFoundDungeonsCount())
*/
        {
            void RedrawForSecondQuestDungeonToggle()
            {
                if (!(TrackerModel.IsHiddenDungeonNumbers.Invoke(null)))
                {
                    mainTrackerCanvases[0, 4].Children.Remove(finalCanvasOf1Or4);
                    mainTrackerCanvases[3, 4].Children.Remove(finalCanvasOf1Or4);
                    mainTrackerCanvases[0, 4].Children.Remove(toggleSecondQuestDungeonCanvas);
                    mainTrackerCanvases[3, 4].Children.Remove(toggleSecondQuestDungeonCanvas);
                    if (TrackerModel.IsSecondQuestDungeons)
                    {
                        canvasAdd(mainTrackerCanvases[3, 4], finalCanvasOf1Or4, 0.0, 0.0);
                        canvasAdd(mainTrackerCanvases[0, 4], toggleSecondQuestDungeonCanvas, 0.0, 0.0);
                    }
                    else
                    {
                        canvasAdd(mainTrackerCanvases[0, 4], finalCanvasOf1Or4, 0.0, 0.0);
                        canvasAdd(mainTrackerCanvases[3, 4], toggleSecondQuestDungeonCanvas, 0.0, 0.0);
                    }
                }
            }
            RedrawForSecondQuestDungeonToggle();
            // when a Triforce Piece is placed in a dungeon item slot, the numbered triforce (and its click behavior) moves from the dungeon column header into that slot,
            // and the header gets an empty floor item box instead. Right/middle click on the triforce in the slot opens the item menu (so it can be removed).
            var extraFloorCanvases = new Canvas[8];
            void RefreshTriforceHeaderOrSlot()
            {
                for (int i = 0; i <= 7; i++)
                {
                    var d = TrackerModel.GetDungeon(i);
                    var header = mainTrackerCanvases[i, 1];
                    Canvas innerc = triforceInnerCanvases[i];
                    var boxes = d.Boxes;
                    int? slotIndex;
                    var k0 = Array.FindIndex(boxes, b => b.CellCurrent() == TrackerModel.ITEMS.TRIFORCE);
                    if (k0 >= 0) slotIndex = k0;
                    else slotIndex = (i == 1 && TrackerModel.level2StairBoxEnabled && TrackerModel.level2StairBox.CellCurrent() == TrackerModel.ITEMS.TRIFORCE) ? 2 : null;   // the extra level 2 staircase slot shares level 2's bottom cell
                    // detach from wherever it currently is
                    header.Children.Remove(innerc);
                    for (int j = 2; j <= 4; j++)
                        mainTrackerCanvases[i, j].Children.Remove(innerc);
                    if (extraFloorCanvases[i] != null)
                        header.Children.Remove(extraFloorCanvases[i]);
                    if (slotIndex is int k && k < 3)
                    {
                        if (extraFloorCanvases[i] == null)
                        {
                            var (ec, _) = Views.MakeBoxItem(cm, d.ExtraFloorBox);
                            extraFloorCanvases[i] = ec;
                        }
                        canvasAdd(header, extraFloorCanvases[i], 0.0, 0.0);
                        innerc.Clip = null;   // triforce fully covers the item box, so no colored box outline shows around it
                        canvasAdd(mainTrackerCanvases[i, k + 2], innerc, 0.0, 0.0);
                    }
                    else
                    {
                        innerc.Clip = null;
                        canvasAdd(header, innerc, 0.0, 0.0);
                    }
                }
            }
            for (int i0 = 0; i0 <= 7; i0++)
            {
                var i = i0;   // (each closure keeps its own i, as in F#)
                Canvas innerc = triforceInnerCanvases[i];
                innerc.AddHandler(InputElement.PointerPressedEvent, new EventHandler<PointerPressedEventArgs>((_, ea) =>   // (WPF: PreviewMouseDown)
                {
                    var d = TrackerModel.GetDungeon(i);
                    var isInSlot = innerc.Parent != null && !(object.ReferenceEquals(innerc.Parent, mainTrackerCanvases[i, 1]));
                    if (isInSlot && ea.GetCurrentPoint(innerc).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
                    {
                        ea.Handled = true;
                        if (!popupIsActive)
                        {
                            var bOpt = d.Boxes.FirstOrDefault(b => b.CellCurrent() == TrackerModel.ITEMS.TRIFORCE);
                            if (bOpt == null && i == 1 && TrackerModel.level2StairBox.CellCurrent() == TrackerModel.ITEMS.TRIFORCE) bOpt = TrackerModel.level2StairBox;
                            if (bOpt is TrackerModel.Box b)
                            {
                                if (boxActivators.TryGetValue(b, out var f)) f();
                            }
                        }
                    }
                }), RoutingStrategies.Tunnel);
                foreach (var b in TrackerModel.GetDungeon(i).Boxes)
                    onEvent(b.Changed, _ => RefreshTriforceHeaderOrSlot());
            }
            if (!(TrackerModel.IsHiddenDungeonNumbers.Invoke(null)))
            {
                onEvent(TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.FinalBoxOf1Or4.Changed, _ => RefreshTriforceHeaderOrSlot());
                onEvent(TrackerModel.level2StairBox.Changed, _ => RefreshTriforceHeaderOrSlot());   // a triforce piece in the extra level 2 staircase slot moves the numbered triforce there too
                onEvent(TrackerModel.level2StairBoxEnabledChanged.Publish, _ => RefreshTriforceHeaderOrSlot());
            }
            RefreshTriforceHeaderOrSlot();
            OptionsMenu.secondQuestDungeonsOptionChanged += () =>
            {
                RedrawForSecondQuestDungeonToggle();
                RefreshTriforceHeaderOrSlot();
                doUIUpdateEvent.Trigger();  // CompletedDungeons may change
                updateGhostBusters();
            };
            if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
            {
                for (int i = 0; i <= 7; i++)
                    onEvent(TrackerModel.GetDungeon(i).HiddenDungeonColorOrLabelChanged, _ => updateGhostBusters());
            }
        }

        return (boxItemImpl, extrasImage, updateNumberedTriforceDisplayImpl, updateNumberedTriforceDisplayIfItExists);
    }
}
