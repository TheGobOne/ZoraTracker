// Ported from Z1R_WPF/Popouts.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// Window positions: WPF's Left/Top are device-independent units, Avalonia's Position is pixels, so they go through ShowRunCustom's
// leftOf/topOf/setLeftTop (the settings keep WPF's numbers). WPF's Width/Height/ActualWidth/RestoreBounds sizes are the window's outer size;
// Avalonia's are the client size (as ShowRunCustom). Avalonia has no RestoreBounds: the popouts remember their last normal-state bounds themselves.
// The doUIUpdateEvent (an F# `Event<unit>` created by WPFUI) is a Graphics.SharedEvent.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Z1R_Avalonia;

public static class Popouts
{
    public static readonly double MINIMIZED_THRESHOLD = ShowRunCustom.MINIMIZED_THRESHOLD;

    public static Window theMainWindow = null;
    public static bool theMainWindowHasClosed = false;
    public static SynchronizationContext ctxt = null;
    public static void Initialize(Window mainWindow, SynchronizationContext ctx)
    {
        theMainWindow = mainWindow;
        ctxt = ctx;
    }
    public static void refocusMainWindow() => refocusMainWindow(null);
    // stillWanted: checked after the delay (activating the main window would close a flyout still open in a popout)
    public static void refocusMainWindow(Func<bool> stillWanted, Action then = null)   // keep hotkeys working
    {
        async Task go()
        {
            await Task.Delay(500);  // give new window time to pop up
            if (stillWanted != null && !stillWanted()) return;
            // (WPF: Async.SwitchToContext(ctxt); awaiting Task.Delay on the UI thread resumes on the UI thread)
            // (WPF's Window.Focus() gives the window keyboard focus, activating it; Avalonia's Focus() only moves focus inside a window, so Activate() too)
            theMainWindow.Activate();
            theMainWindow.Focus();
            then?.Invoke();
        }
        CustomComboBoxes.StartImmediate(go());
    }

    // (port helpers: F#'s `float s` parses with the invariant culture; WPF's Left/Top and Avalonia's Position, see the top of the file)
    private static double parseFloat(string s) => double.Parse(s, CultureInfo.InvariantCulture);
    private static double leftOf(Window w) => ShowRunCustom.leftOf(w);
    private static double topOf(Window w) => ShowRunCustom.topOf(w);
    private static void setLeftTop(Window w, double left, double top) => ShowRunCustom.setLeftTop(w, left, top);

    ////////////////////////////////////////////////////////

    public static void showHotKeys(bool isRightClick)
    {
        var (none, p) = OverworldMapTileCustomization.MakeMappedHotKeysDisplay();
        var w = new Window { Title = "ZORATracker HotKeys" };   // (WPF: Owner=Application.Current.MainWindow; Avalonia passes the owner to Show, below)
        w.Content = p;
        void save()
        {
            TrackerModelOptions.HotKeyWindowLTWH = $"{(int)leftOf(w)},{(int)topOf(w)},{(int)w.ClientSize.Width},{(int)w.ClientSize.Height}";
            ShowRunCustom.writeSettingsSoon();   // (called on every move event)
        }
        var leftTopWidthHeight = TrackerModelOptions.HotKeyWindowLTWH;
        var matches = System.Text.RegularExpressions.Regex.Match(leftTopWidthHeight, @"^(-?\d+),(-?\d+),(\d+),(\d+)$");
        if (!none && !isRightClick && matches.Success && !(parseFloat(matches.Groups[1].Value) < MINIMIZED_THRESHOLD))
        {
            setLeftTop(w, parseFloat(matches.Groups[1].Value), parseFloat(matches.Groups[2].Value));
            w.Width = parseFloat(matches.Groups[3].Value);
            w.Height = parseFloat(matches.Groups[4].Value);
        }
        else
        {
            p.Measure(new Size(1280.0, 720.0));
            w.Width = p.DesiredSize.Width + 16.0;
            w.Height = p.DesiredSize.Height + 40.0;
        }
        w.SizeChanged += (_, _) => { save(); refocusMainWindow(); };
        w.PositionChanged += (_, _) => { save(); refocusMainWindow(); };
        ShowRunCustom.showOwned(w);
    }

    ////////////////////////////////////////////////

    public static System.Drawing.Bitmap makeFauxItemsAndHeartsHUD()
    {
        var r = new System.Drawing.Bitmap(98, 61 + 18);
        var best = Graphics.allItemsHUDBestBMP;
        var worst = Graphics.allItemsHUDWorstBMP;
        var BLACK = System.Drawing.Color.Black;
        // draw full HUD
        for (var x = 0; x <= best.Width - 1; x++)
            for (var y = 0; y <= best.Height - 1; y++)
                r.SetPixel(x, y, best.GetPixel(x, y));
        // erase or change based on inventory
        void maybeErase(int x1, int x2, int y1, int y2, bool b)
        {
            if (!(b))
            {
                for (var x = x1; x <= x2; x++)
                    for (var y = y1; y <= y2; y++)
                        r.SetPixel(x, y, BLACK);
            }
        }
        void copyWorse(int x1, int x2, int y1, int y2, bool b)
        {
            if (b)
            {
                for (var x = x1; x <= x2; x++)
                    for (var y = y1; y <= y2; y++)
                        r.SetPixel(x, y, worst.GetPixel(x, y));
            }
        }
        // top row
        maybeErase(6, 19, 0, 15, TrackerModel.playerComputedStateSummary.HaveRaft);
        maybeErase(29, 36, 0, 15, TrackerModel.PlayerHasTheBook());
        maybeErase(42, 48, 3, 11, TrackerModel.playerComputedStateSummary.RingLevel > 0);
        copyWorse(42, 48, 3, 11, TrackerModel.playerComputedStateSummary.RingLevel == 1);
        maybeErase(53, 68, 0, 15, TrackerModel.playerComputedStateSummary.HaveLadder);
        maybeErase(73, 81, 0, 15, TrackerModel.playerComputedStateSummary.HaveAnyKey);
        maybeErase(85, 92, 0, 15, TrackerModel.playerComputedStateSummary.HavePowerBracelet);
        // middle row
        maybeErase(10, 14, 28, 35, TrackerModel.playerComputedStateSummary.BoomerangLevel > 0);
        copyWorse(10, 14, 28, 35, TrackerModel.playerComputedStateSummary.BoomerangLevel == 1);
        maybeErase(33, 40, 24, 39, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBombs.Value());
        maybeErase(55, 59, 24, 39, TrackerModel.playerComputedStateSummary.ArrowLevel > 0);
        copyWorse(55, 59, 24, 39, TrackerModel.playerComputedStateSummary.ArrowLevel == 1);
        maybeErase(61, 69, 24, 39, TrackerModel.playerComputedStateSummary.HaveBow);
        maybeErase(81, 88, 24, 39, TrackerModel.playerComputedStateSummary.CandleLevel > 0);
        copyWorse(81, 88, 24, 39, TrackerModel.playerComputedStateSummary.CandleLevel == 1);
        // bottom row
        maybeErase(12, 14, 40, 55, TrackerModel.playerComputedStateSummary.HaveRecorder);
        // meat: only shown once collected (starting-items checkbox or a meat box marked obtained); otherwise empty
        var haveMeat =
            TrackerModel.startingItemsAndExtras.PlayerHasMeat.Value() ||
            (TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes()
             .Any(b => b.CellCurrent() == TrackerModel.ITEMS.MEAT && b.PlayerHas() == TrackerModel.PlayerHas.YES));
        maybeErase(33, 40, 40, 55, haveMeat);
        copyWorse(57, 64, 40, 55, true);  // worse shows potion letter, maybeErased below
        maybeErase(57, 64, 40, 55, TrackerModel.havePotionLetter.Value);
        maybeErase(83, 86, 40, 55, TrackerModel.playerComputedStateSummary.HaveWand);
        // add hearts display (at the left edge, with the current sword to their right)
        // (the hearts and the sword are centered together: 8 hearts of width 8, a gap of 8, and the 10-wide sword)
        var swordLvl = Math.Min(3, TrackerModel.playerComputedStateSummary.SwordLevel);
        var groupW = 8 * 8 + (swordLvl >= 1 ? 8 + 10 : 0);
        var heartsX = (98 - groupW) / 2;
        if (TrackerModel.playerComputedStateSummary.PlayerHearts > 8)
        {
            // top row
            for (var i = 0; i <= TrackerModel.playerComputedStateSummary.PlayerHearts - 9; i++)
                for (var dx = 0; dx <= 7; dx++)
                    for (var dy = 0; dy <= 7; dy++)
                        r.SetPixel(heartsX + i * 8 + dx, 63 + dy, Graphics.empty_small_heart_bmp.GetPixel(dx, dy));
        }
        // bottom row, 3 red
        for (var i = 0; i <= (Math.Min(TrackerModel.playerComputedStateSummary.PlayerHearts, 3)) - 1; i++)
            for (var dx = 0; dx <= 7; dx++)
                for (var dy = 0; dy <= 7; dy++)
                    r.SetPixel(heartsX + i * 8 + dx, 71 + dy, Graphics.filled_small_heart_bmp.GetPixel(dx, dy));
        // bottom row rest
        for (var i = 3; i <= (Math.Min(TrackerModel.playerComputedStateSummary.PlayerHearts, 8)) - 1; i++)
            for (var dx = 0; dx <= 7; dx++)
                for (var dy = 0; dy <= 7; dy++)
                    r.SetPixel(heartsX + i * 8 + dx, 71 + dy, Graphics.empty_small_heart_bmp.GetPixel(dx, dy));
        // the current sword (wood / white / magical), centered in the room to the right of the hearts: the 15x27 menu icon sampled down to its 5x9 art pixels
        if (swordLvl >= 1)
        {
            var sb = swordLvl switch { 1 => Graphics.ow_sword1_menu_bmp, 2 => Graphics.ow_sword2_menu_bmp, _ => Graphics.ow_sword3_menu_bmp };
            // (drawn at twice the heart pixel size: 10x18, filling the 18 rows under the items)
            var swordX = heartsX + 8 * 8 + 8;
            var swordY = 61;
            for (var ax = 0; ax <= 4; ax++)
            {
                for (var ay = 0; ay <= 8; ay++)
                {
                    var c = sb.GetPixel(ax * 3 + 1, ay * 3 + 1);
                    if ((int)c.A > 0 && ((int)c.R + (int)c.G + (int)c.B) > 40)
                    {
                        for (var dx = 0; dx <= 1; dx++)
                            for (var dy = 0; dy <= 1; dy++)
                                r.SetPixel(swordX + ax * 2 + dx, swordY + ay * 2 + dy, c);
                    }
                }
            }
        }
        // maybe change green to white/red based on ring? given randomize tunic colors, seems frivolous, so naah
        return r;
    }
    public static StackPanel MakeInventoryAndHearts()
    {
        var bmp = makeFauxItemsAndHeartsHUD();
        var img = Graphics.BMPtoImage(bmp);
        img.Width = 3.0 * img.Width;
        img.Height = 3.0 * img.Height;
        img.Stretch = Stretch.UniformToFill;
        RenderOptions.SetBitmapInterpolationMode(img, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
        var sp = new StackPanel { Orientation = Orientation.Vertical };
        sp.Children.Add(img);
        var playerKnowsLocationOfWoodBoomer = TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes().Any(b => b.CellCurrent() == TrackerModel.ITEMS.BOOMERANG);
        var playerKnowsLocationOfWhiteSword = TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes().Any(b => b.CellCurrent() == TrackerModel.ITEMS.WHITESWORD);
        if (TrackerModel.playerComputedStateSummary.BoomerangLevel == 2 && !(playerKnowsLocationOfWoodBoomer))
        {
            var tb = new TextBox { FontSize = 14.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0),
                                    Text = "Note: having magical boomerang may make\n   it hard to notice wooden boomerang pick up" };
            sp.Children.Add(tb);
        }
        return sp;
    }
    // Ctrl+mouse wheel over the Uncollected Items window grows / shrinks its icons
    public static double remainingItemsIconScale = 1.0;
    public static StackPanel MakeRemainingItemsDisplay()
    {
        Image scaledImg(System.Drawing.Bitmap bmp)
        {
            var r = Graphics.BMPtoImage(bmp);
            r.Stretch = Stretch.Fill;
            r.Width = (double)bmp.Width * remainingItemsIconScale;
            r.Height = (double)bmp.Height * remainingItemsIconScale;
            RenderOptions.SetBitmapInterpolationMode(r, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
            r.Margin = new Thickness(0.0, 0.0, 3.0, 3.0);
            return r;
        }
        Control img(int i)
        {
            var im = scaledImg(CustomComboBoxes.boxCurrentBMP(i, null));
            if (Views.itemIsHintDashed(i))
            {
                var g = new Grid();
                g.Children.Add(im);
                g.Children.Add(new Avalonia.Controls.Shapes.Rectangle { Stroke = Brushes.Red, StrokeThickness = 2.0, StrokeDashArray = new AvaloniaList<double> { 3.0, 2.0 }, IsHitTestVisible = false });
                return g;
            }
            else return im;
        }
        Image swordImg(System.Drawing.Bitmap bmp) => scaledImg(bmp);
        // wraps onto more rows as the window is made narrower
        var hsp = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(3.0) };
        // An item is "remaining" if no dungeon box has it marked as obtained (IsDone = CellCurrent set AND PlayerHas is YES or SKIPPED).
        // This correctly handles presets where all items are pre-placed but not yet obtained (PlayerHas=NO).
        var allBoxes = TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes();
        // (an item whose marker icon has been checked off as collected on the overworld map also counts as obtained)
        // (an item the Item Progress bar says the player owns also counts, however it was marked: clicked in the progress bar, a starting item, a hotkey)
        var sm = TrackerModel.playerComputedStateSummary;
        bool ownedPerProgress(int i) =>
            (i == TrackerModel.ITEMS.BOW && sm.HaveBow) || (i == TrackerModel.ITEMS.SILVERARROW && sm.ArrowLevel >= 2) || (i == TrackerModel.ITEMS.LADDER && sm.HaveLadder)
            || (i == TrackerModel.ITEMS.RECORDER && sm.HaveRecorder) || (i == TrackerModel.ITEMS.RAFT && sm.HaveRaft) || (i == TrackerModel.ITEMS.POWERBRACELET && sm.HavePowerBracelet)
            || (i == TrackerModel.ITEMS.KEY && sm.HaveAnyKey) || (i == TrackerModel.ITEMS.WAND && sm.HaveWand) || (i == TrackerModel.ITEMS.BOOKSHIELD && sm.HaveBookOrShield)
            || (i == TrackerModel.ITEMS.MAGICBOOMERANG && sm.BoomerangLevel >= 2);   // (the wood boomerang is left alone: owning the magical one does not mean the wood one was found)
        bool isObtained(int i) => (allBoxes.Any(box => box.CellCurrent() == i && box.IsDone())) || TrackerModel.mapCollectedCount[i] > 0 || ownedPerProgress(i);
        // same order as the Item Progress bar: swords first (every sword level not yet reached)
        var swordLevel = TrackerModel.playerComputedStateSummary.SwordLevel;
        if (swordLevel < 1) hsp.Children.Add(swordImg(Graphics.swordLevelToOwBmp(1)));
        if (swordLevel < 2 && !(isObtained(TrackerModel.ITEMS.WHITESWORD))) hsp.Children.Add(swordImg(Graphics.swordLevelToOwBmp(2)));
        if (swordLevel < 3 && !(isObtained(TrackerModel.ITEMS.MAGICALSWORD))) hsp.Children.Add(swordImg(Graphics.swordLevelToOwBmp(3)));
        if (TrackerModelOptions.FourthSwordEnabled && TrackerModel.playerHasSword4.Value != 1) hsp.Children.Add(swordImg(Graphics.ow_sword4_item_bmp));
        var candleLevel = TrackerModel.playerComputedStateSummary.CandleLevel;
        var ringLevel = TrackerModel.playerComputedStateSummary.RingLevel;
        foreach (var i in new[] { TrackerModel.ITEMS.SILVERARROW, TrackerModel.ITEMS.BOW, TrackerModel.ITEMS.LADDER, TrackerModel.ITEMS.POWERBRACELET, TrackerModel.ITEMS.RECORDER, TrackerModel.ITEMS.RAFT,
                    TrackerModel.ITEMS.BLUECANDLE, TrackerModel.ITEMS.REDCANDLE, TrackerModel.ITEMS.BLUERING, TrackerModel.ITEMS.REDRING, TrackerModel.ITEMS.WAND, TrackerModel.ITEMS.KEY,
                    TrackerModel.ITEMS.BOOMERANG, TrackerModel.ITEMS.MAGICBOOMERANG, TrackerModel.ITEMS.BOOKSHIELD })
        {
            var levelReached =
                i == TrackerModel.ITEMS.BLUECANDLE ? candleLevel >= 1
                : i == TrackerModel.ITEMS.REDCANDLE ? candleLevel >= 2
                : i == TrackerModel.ITEMS.BLUERING ? ringLevel >= 1
                : i == TrackerModel.ITEMS.REDRING ? ringLevel >= 2
                : false;
            if (!(isObtained(i)) && !levelReached)
                hsp.Children.Add(img(i));
        }
        var h = TrackerModel.ITEMS.HEARTCONTAINER;
        var obtainedHC = (allBoxes.Sum(box => box.CellCurrent() == h && box.IsDone() ? 1 : 0)) + TrackerModel.mapCollectedCount[h];
        // 13 hearts in all: the 9 in the item pool plus the 4 take-any hearts; every take-any slot that has been used (a heart, or a candle / potion taken instead) counts as collected
        var tookAnyCount = Enumerable.Range(0, 4).Count(k => TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyHeart(k) != 0);
        var c = TrackerModel.allItemWithHeartShuffleChoiceDomain.MaxUses(h) + 4 - obtainedHC - tookAnyCount;
        if (c >= 1)
        {
            // just one heart, followed by how many remain
            // the heart and its count are one unit, so they never get separated onto different rows when the window is resized
            var heartAndCount = new StackPanel { Orientation = Orientation.Horizontal };
            heartAndCount.Children.Add(img(h));
            heartAndCount.Children.Add(new TextBox { Text = $"x{c}", FontSize = 20.0 * remainingItemsIconScale, FontWeight = FontWeight.Bold, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), Foreground = Brushes.Orange, Background = Brushes.Black, VerticalAlignment = VerticalAlignment.Center });
            hsp.Children.Add(heartAndCount);
        }
        if (hsp.Children.Count == 0)
            hsp.Children.Add(new TextBox { Text = "None", FontSize = 12.0, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), Foreground = Brushes.Orange, Background = Brushes.Black });
        var vsp = new StackPanel { Orientation = Orientation.Vertical };
        vsp.Children.Add(hsp);
        return vsp;
    }

    ////////////////////////////////////////////////////////

    // (F#: a union with no data, so a C# enum)
    public enum PopoutKind
    {
        SpotSummaryPopout,
        InventoryAndHeartsPopout,
        RemainingItemsPoput,
        ShopsPopout,
        ShuffleItemsPopout
    }

    public static Window theSpotSummaryPopoutWindow = null;
    public static Window theInventoryAndHeartsPopoutWindow = null;
    public static Window theRemainingItemsPopoutWindow = null;
    public static Window theShopsPopoutWindow = null;
    public static Window theShuffleItemsPopoutWindow = null;

    public static DockPanel spotSummaryContent(Graphics.SharedEvent doUIUpdateEvent)
    {
        var dp = new DockPanel { Background = Brushes.Black };
        void redraw()
        {
            dp.Children.Clear();
            var summary = OverworldMapTileCustomization.MakeRemainderSummaryDisplay();
            summary.BorderThickness = new Thickness(0.0);   // no grey border in the popout window
            dp.Children.Add(summary);
        }
        redraw();
        // (subscribed only while the popout is on screen: these are shared events that outlive the window, so a handler left behind would keep redrawing a closed window and pin it in memory)
        var summaryHandler = new Microsoft.FSharp.Control.FSharpHandler<Microsoft.FSharp.Core.Unit>((_, _) => redraw());
        System.Action itemsHandler = () => redraw();   // also redraw as items get collected (the summary dims potions/letter/swords once collected)
        dp.AttachedToVisualTree += (_, _) =>
        {
            TrackerModel.mapStateSummaryComputedEvent.Publish.AddHandler(summaryHandler);
            doUIUpdateEvent.Fired += itemsHandler;
            redraw();
        };
        dp.DetachedFromVisualTree += (_, _) =>
        {
            TrackerModel.mapStateSummaryComputedEvent.Publish.RemoveHandler(summaryHandler);
            doUIUpdateEvent.Fired -= itemsHandler;
        };
        return dp;
    }
    public static Border inventoryAndHeartsContent(Graphics.SharedEvent doUIUpdateEvent)
    {
        var dp = new DockPanel { Background = Brushes.Black };
        void redraw()
        {
            dp.Children.Clear();
            dp.Children.Add(MakeInventoryAndHearts());
        }
        redraw();
        System.Action updateHandler = () => redraw();   // (subscribed only while on screen; see spotSummaryContent)
        dp.AttachedToVisualTree += (_, _) => { doUIUpdateEvent.Fired += updateHandler; redraw(); };
        dp.DetachedFromVisualTree += (_, _) => doUIUpdateEvent.Fired -= updateHandler;
        return new Border { Child = dp, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.0) };   // no grey border in the popout window
    }
    public static Border remainingItemsContent(Graphics.SharedEvent doUIUpdateEvent)
    {
        remainingItemsIconScale = TrackerModelOptions.RemainingItemsIconScale;   // remembered from last time
        var dp = new DockPanel { Background = Brushes.Black };
        void redraw()
        {
            dp.Children.Clear();
            dp.Children.Add(MakeRemainingItemsDisplay());
        }
        redraw();
        System.Action updateHandler = () => redraw();   // (subscribed only while on screen; see spotSummaryContent)
        dp.AttachedToVisualTree += (_, _) => { doUIUpdateEvent.Fired += updateHandler; redraw(); };
        dp.DetachedFromVisualTree += (_, _) => doUIUpdateEvent.Fired -= updateHandler;
        var border = new Border { Child = dp, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.0), Background = Brushes.Black };
        // (WPF: PreviewMouseWheel, so a tunnelling handler; Keyboard.Modifiers → the event's KeyModifiers; whole notches only, see Graphics.wheelNotch)
        border.AddHandler(InputElement.PointerWheelChangedEvent, new EventHandler<PointerWheelEventArgs>((sender, e) =>
        {
            if (Platform.CmdAsCtrl(e.KeyModifiers) == KeyModifiers.Control)   // (port: on macOS Cmd counts as Ctrl)
            {
                e.Handled = true;
                var notch = Graphics.wheelNotch(sender, e);
                if (notch == 0) return;
                var factor = notch > 0 ? 1.15 : 1.0 / 1.15;
                remainingItemsIconScale = Math.Max(0.5, Math.Min(8.0, remainingItemsIconScale * factor));
                TrackerModelOptions.RemainingItemsIconScale = remainingItemsIconScale;
                TrackerModelOptions.writeSettings();
                redraw();
            }
        }), RoutingStrategies.Tunnel);
        return border;
    }

    // the Shop window of the overworld menu, in its own window. It has no tile, so nothing here places a shop on the map: it lists the shops and edits what is in them
    // (Avalonia raises a window with SWP_NOACTIVATE on Windows, so the main window keeps the keyboard)
    private static void raiseWithoutFocus(Window w) { w.Topmost = true; w.Topmost = false; }
    public static Control shopsContent(Window w)
    {
        Border panel = null;
        var pickerOpen = false;
        // the main window gets the keyboard back after a click here, so the map's item hotkeys keep working (not while the picker is open)
        void refocus() => refocusMainWindow(() => !pickerOpen, () => raiseWithoutFocus(w));
        // the item picker, at the pointer (the main window's popup layers can't be used from another window)
        Task<int> showPicker(List<(int, Control)> items)
        {
            var picked = new TaskCompletionSource<int>();
            var fly = new Flyout();
            fly.Content = PieMenus.makeItemPickerBox(items, 9, id => { picked.TrySetResult(id); fly.Hide(); });
            fly.Closed += (_, _) => { pickerOpen = false; picked.TrySetResult(-1); refocus(); };
            pickerOpen = true;
            fly.ShowAt(panel, true);
            return picked.Task;
        }
        var (counts, shopItems) = WPFUI.shopSnapshot();
        var (bord, onKey, _, _) = PieMenus.makeShopPanel(counts, shopItems, true, WPFUI.addItemToShopGlobal, () => WPFUI.pickShopItem(showPicker), null, null,
            "Right click to enter shops. Place a shop on the map from the overworld menu and it picks up these items.");
        panel = bord;
        panel.BorderThickness = new Thickness(0.0);   // no grey border in the popout window
        panel.MyKeyAdd(onKey);
        panel.AddHandler(InputElement.PointerReleasedEvent, new EventHandler<PointerReleasedEventArgs>((_, _) => refocus()), RoutingStrategies.Bubble, true);
        // item hotkeys while this window has the keyboard (with the main window focused, the tracker's own hotkey routing reaches the panel)
        w.KeyDown += (_, ea) =>
        {
            var skm = SingleKeyboardModifierExtensions.GetCurrent(ea.KeyModifiers);
            if (skm != SingleKeyboardModifier.Unsupported && panel.IsPointerOver && HotKeys.boundHotKeyUniverse.ContainsKey((skm, ea.Key)))
            {
                ea.Handled = true;
                panel.RaiseEvent(new MyKeyRoutedEventArgs(skm, ea.Key));
            }
        };
        return panel;
    }

    // the M/C/Tri Shuffle window: one row per level with the triforce, map and compass found there (each marked with the
    // level it is for), then that level's item boxes (the same boxes as the main tracker's)
    public static string shuffleLevelName(int level) => level < 0 ? "?" : level < 8 && TrackerModel.IsHiddenDungeonNumbers.Invoke(null) ? ((char)('A' + level)).ToString() : (level + 1).ToString();
    // the small tag naming the level a spot's triforce / map / compass is for
    public static Control shuffleLevelTag(int level) =>
        new Border { Background = Brushes.Black, Child = new TextBlock { Text = shuffleLevelName(level), FontSize = 11.0, FontWeight = FontWeight.Bold, Foreground = Brushes.White } };

    public static Control shuffleItemsContent(Window w)
    {
        var rows = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Black, Margin = new Thickness(6.0) };   // holds the columns of levels
        var pickerOpen = false;
        void refocus() => refocusMainWindow(() => !pickerOpen, () => raiseWithoutFocus(w));
        // menus open as flyouts (the main window's popup layers can't be used from another window)
        // done(after): closes the menu, then runs after once it has gone (a change redraws the rows, and the menu must not lose its box while closing)
        void showMenu(Control at, Func<Action<Action>, Control> makeContent)
        {
            var fly = new Flyout();
            fly.Content = makeContent(after => { fly.Hide(); Dispatcher.UIThread.Post(after, DispatcherPriority.Background); });
            fly.Closed += (_, _) => { pickerOpen = false; refocus(); };
            pickerOpen = true;
            fly.ShowAt(at, true);
        }
        TextBox text(string s, double size) => new TextBox { Text = s, FontSize = size, FontWeight = FontWeight.Bold, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0),
                                                           Foreground = Brushes.Orange, Background = Brushes.Black, MinWidth = 0.0, MinHeight = 0.0, Padding = new Thickness(0.0), VerticalAlignment = VerticalAlignment.Center };
        string levelName(int level) => level < 8 && TrackerModel.IsHiddenDungeonNumbers.Invoke(null) ? ((char)('A' + level)).ToString() : (level + 1).ToString();
        // the levels to choose from (no "?"), as the slot menus show them
        Control levelStrip(int count, Action<int> picked)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Black };
            for (var n = 0; n < count; n++)
            {
                var pick = n;
                var label = new TextBlock { Text = levelName(pick), FontSize = 16.0, FontWeight = FontWeight.Bold, Foreground = Brushes.Orange, HorizontalAlignment = HorizontalAlignment.Center };
                var b = new Button { Template = Graphics.roundedButtonTemplate, Content = label, MinWidth = 32.0, Margin = new Thickness(2.0), Padding = new Thickness(6.0, 4.0),
                                     Focusable = false, Background = Graphics.almostBlack, BorderBrush = Graphics.roundedBorderBrush };
                b.Click += (_, _) => picked(pick);
                sp.Children.Add(b);
            }
            return sp;
        }
        // a triforce / map / compass placed in a box: asks which level it is for (triforce 1-8, map and compass 1-9), and
        // marks it collected (these are marked as they are picked up)
        void pickLevel(Control at, int id, TrackerModel.Box box) =>
            showMenu(at, done => levelStrip(id == TrackerModel.ITEMS.TRIFORCE ? 8 : 9, lvl => done(() => box.AttemptToSetForLevel(id, TrackerModel.PlayerHas.YES, lvl))));
        Control slotBox(int row, int slot)
        {
            var c = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black };
            var got = TrackerModel.shuffleSlotGot[row * 3 + slot];
            var level = TrackerModel.ShuffleSlotLevel(row, slot);
            var bmp = slot == 0 ? CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.TRIFORCE, null) : slot == 1 ? Graphics.zi_map_bmp : Graphics.zi_compass_bmp;
            var img = Graphics.BMPtoImage(got ? bmp : Graphics.greyscale(bmp));
            img.Width = 21.0; img.Height = 21.0; img.Stretch = Stretch.Uniform;
            RenderOptions.SetBitmapInterpolationMode(img, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
            Graphics.canvasAdd(c, img, 4.0, 4.0);
            c.Children.Add(new Avalonia.Controls.Shapes.Rectangle { Width = 30.0, Height = 30.0, Stroke = got ? CustomComboBoxes.yes : CustomComboBoxes.no, StrokeThickness = 3.0 });
            var tag = new Border { Background = Brushes.Black, Child = new TextBlock { Text = level < 0 ? "?" : levelName(level), FontSize = 11.0, FontWeight = FontWeight.Bold, Foreground = Brushes.White } };
            Graphics.canvasAdd(c, tag, 19.0, 15.0);
            var kind = slot == 0 ? "Triforce" : slot == 1 ? "Map" : "Compass";
            ToolTip.SetTip(c, $"{kind} found in level {levelName(row)}\nLeft click: collected. Right click: which level it is for.");
            c.PointerPressed += (_, ea) =>
            {
                var button = ea.GetCurrentPoint(c).Properties.PointerUpdateKind;
                var chosen = TrackerModel.shuffleSlotChosen[row * 3 + slot];
                if (button == PointerUpdateKind.LeftButtonPressed)
                {
                    TrackerModel.SetShuffleSlot(row, slot, !got, chosen);
                    refocus();   // (that redrew the window, so the release below never comes from this box)
                }
                else if (button == PointerUpdateKind.RightButtonPressed)
                    showMenu(c, done =>
                    {
                        var sp = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Black };
                        var count = slot == 0 ? 8 : 9;
                        for (var n = -1; n < count; n++)
                        {
                            var pick = n;
                            var label = new TextBlock { Text = pick < 0 ? "?" : levelName(pick), FontSize = 16.0, FontWeight = FontWeight.Bold, Foreground = Brushes.Orange, HorizontalAlignment = HorizontalAlignment.Center };
                            var b = new Button { Template = Graphics.roundedButtonTemplate, Content = label, MinWidth = 32.0, Margin = new Thickness(2.0), Padding = new Thickness(6.0, 4.0),
                                                 Focusable = false, Background = Graphics.almostBlack, BorderBrush = Graphics.roundedBorderBrush };
                            b.Click += (_, _) => done(() => TrackerModel.SetShuffleSlot(row, slot, TrackerModel.shuffleSlotGot[row * 3 + slot], pick));
                            sp.Children.Add(b);
                        }
                        return sp;
                    });
                ea.Handled = true;
            };
            return c;
        }
        Control itemBox(TrackerModel.Box box)
        {
            var (view, _) = Views.MakeBoxItemWithExtraDecorations(null, box, false, null);   // (draws and redraws; the clicks are below)
            // Shuffle Type Anywhere: every box here takes any item, a triforce / map / compass included (otherwise none takes those three)
            bool allowed(int id) => TrackerModel.ShuffleAnywhere || !TrackerModel.ITEMS.isShuffleItem(id);
            void pickItem() => showMenu(view, done =>
            {
                var extra = (TrackerModelOptions.ArchipelagoSupport.Value ? new[] { TrackerModel.ITEMS.APITEM, TrackerModel.ITEMS.APITEM_USEFUL, TrackerModel.ITEMS.APITEM_FILLER } : new int[0])
                                .Concat(Enumerable.Range(0, TrackerModel.ITEMS.customMenuCount).Select(k => TrackerModel.ITEMS.CUSTOM_BASE + k))
                                .Concat(new[] { TrackerModel.ITEMS.MAP, TrackerModel.ITEMS.COMPASS });   // (only this window offers them, Shuffle Type Anywhere)
                var items = CustomComboBoxes.itemMenuIdents.Concat(extra)
                    .Where(id => id != -1 && allowed(id) && !TrackerModel.progressiveDisabledIdent(id) && (id == box.CellCurrent() || box.Domain.CanAddUse(id) && !CustomComboBoxes.ownedUniqueItem(id) && !(id == TrackerModel.ITEMS.WHITESWORD4 && !TrackerModelOptions.FourthSwordEnabled)))
                    .Concat(new[] { -1 })   // (the clear button comes last)
                    .Select(id =>
                    {
                        Control icon = id == -1 ? new TextBlock { Text = "X", FontSize = 24.0, FontWeight = FontWeight.Bold, Foreground = Brushes.Red, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                                                : Graphics.BMPtoImage(CustomComboBoxes.boxCurrentBMP(id, null));
                        return (id, icon);
                    }).ToList();
                var picker = PieMenus.makeItemPickerBox(items, 5, id =>
                {
                    done(() =>
                    {
                        if (TrackerModel.ShuffleAnywhere && TrackerModel.ITEMS.isShuffleItem(id)) pickLevel(view, id, box);
                        else if (id != box.CellCurrent()) box.AttemptToSet(id, TrackerModel.PlayerHas.NO);
                    });
                });
                var grid = (Avalonia.Controls.Primitives.UniformGrid)picker.Child;
                while (grid.Children.Count % 5 != 0) grid.Children.Insert(grid.Children.Count - 1, new Control());   // (blanks, so the X ends the bottom row)
                return picker;
            });
            view.PointerPressed += (_, ea) =>
            {
                var button = ea.GetCurrentPoint(view).Properties.PointerUpdateKind;
                if (button != PointerUpdateKind.LeftButtonPressed && button != PointerUpdateKind.MiddleButtonPressed && button != PointerUpdateKind.RightButtonPressed) return;
                ea.Handled = true;
                var desire = CustomComboBoxes.MouseButtonEventArgsToPlayerHas(ea);
                if (box.CellCurrent() == -1 || desire.Equals(box.PlayerHas())) pickItem();   // (as the main tracker's boxes)
                else
                {
                    box.SetPlayerHas(desire);
                    refocus();   // (this can redraw the window, so the release never comes from this box)
                }
            };
            return view;
        }
        void watch<T>(Control owner, Microsoft.FSharp.Control.IEvent<Microsoft.FSharp.Control.FSharpHandler<T>, T> e, Action changed)
        {
            var h = new Microsoft.FSharp.Control.FSharpHandler<T>((_, _) => changed());
            Views.WhileInTree(owner, () => e.AddHandler(h), () => e.RemoveHandler(h), changed);
        }
        // under a level's name: that level's own triforce, map and compass, dim until collected (wherever they turned up)
        Control levelLamps(int row)
        {
            var strip = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 3.0 };
            Image lamp(System.Drawing.Bitmap bmp)
            {
                var img = Graphics.BMPtoImage(bmp);
                img.Width = 12.0; img.Height = 12.0; img.Stretch = Stretch.Uniform;
                RenderOptions.SetBitmapInterpolationMode(img, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
                strip.Children.Add(img);
                return img;
            }
            var tri = lamp(CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.TRIFORCE, null));
            var map = lamp(Graphics.zi_map_bmp);
            var compass = lamp(Graphics.zi_compass_bmp);
            var d = TrackerModel.GetDungeon(row);
            void light()
            {
                tri.Opacity = row == 8 ? 0.0 : d.PlayerHasTriforce() ? 1.0 : 0.25;   // (level 9 has no triforce; the space stays so the icons line up)
                map.Opacity = d.PlayerHasMapOfThisDungeon ? 1.0 : 0.25;
                compass.Opacity = TrackerModel.ShuffleCredits(2, row) ? 1.0 : 0.25;
            }
            light();
            // (compass changes redraw the whole window; these are the ones that do not: the main tracker's marks, and a triforce got in an item box)
            watch(strip, d.PlayerHasTriforceChanged, light);
            watch(strip, d.PlayerHasMapOfThisDungeonChanged, light);
            foreach (var box in d.Boxes) watch(strip, box.Changed, light);
            if (row == 1) watch(strip, TrackerModel.level2StairBox.Changed, light);
            return strip;
        }
        // the levels flow into the number of columns that shows them largest at the window's shape (the window opens as one column)
        var shownColumns = 1;
        var lineWidth = 0.0;
        int bestColumns()
        {
            if (w.SizeToContent != SizeToContent.Manual || lineWidth <= 0.0) return 1;
            double scaleAt(int n) => Math.Min(w.ClientSize.Width / (n * lineWidth + (n - 1) * 16.0 + 12.0), w.ClientSize.Height / (((TrackerModel.ShuffleAnywhere ? 9 : 8) / n + 1) * 34.0 + 12.0));
            var best = 1;
            for (var n = 2; n <= 9; n++) if (scaleAt(n) > scaleAt(best)) best = n;
            return best;
        }
        void redraw()
        {
            rows.Children.Clear();
            var lines = new List<StackPanel>();
            lineWidth = 0.0;
            for (var row = 0; row <= 8; row++)
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0.0, 0.0, 0.0, 4.0) };
                var label = text($"LEVEL-{levelName(row)}", 14.0);
                label.Width = 80.0;
                label.TextAlignment = TextAlignment.Center;
                var name = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
                name.Children.Add(label);
                name.Children.Add(levelLamps(row));
                line.Children.Add(name);
                var boxes = new List<Control>();
                if (TrackerModel.ShuffleAnywhere) for (var slot = row < 8 ? 0 : 1; slot <= 2; slot++) boxes.Add(itemBox(TrackerModel.shuffleSlotBoxes[row * 3 + slot]));   // (level 9 has two floor item slots)
                else for (var slot = row < 8 ? 0 : 1; slot <= 2; slot++) boxes.Add(slotBox(row, slot));
                var d = TrackerModel.GetDungeon(row);
                foreach (var box in d.Boxes) boxes.Add(itemBox(box));
                if (row == 1 && TrackerModel.level2StairBoxEnabled) boxes.Add(itemBox(TrackerModel.level2StairBox));
                if (row == 8 && TrackerModel.level9FloorBoxEnabled) boxes.Add(itemBox(d.ExtraFloorBox));
                foreach (var box in boxes) { box.Margin = new Thickness(0.0, 0.0, 4.0, 0.0); line.Children.Add(box); }
                lines.Add(line);
                lineWidth = Math.Max(lineWidth, 80.0 + boxes.Count * 34.0);
            }
            if (TrackerModel.ShuffleAnywhere)
            {
                // the coast, armos and white sword spots can hold a triforce / map / compass too
                var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0.0, 0.0, 0.0, 4.0) };
                var label = text("OVERWORLD", 14.0);
                label.Width = 114.0;   // (wider than a level's name, which would cut "OVERWORLD" off; one box column more)
                label.TextAlignment = TextAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                line.Children.Add(label);
                foreach (var (box, bmp, spot) in new[] { (TrackerModel.ladderBox, Graphics.ladder_bmp, "Coast item"), (TrackerModel.armosBox, Graphics.ow_key_armos_bmp, "Armos item"),
                                                         (TrackerModel.sword2Box, Graphics.ow_sword2_item_bmp, "White sword item") })
                {
                    var icon = Graphics.BMPtoImage(bmp);
                    icon.Width = 21.0; icon.Height = 21.0; icon.Stretch = Stretch.Uniform;
                    RenderOptions.SetBitmapInterpolationMode(icon, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
                    ToolTip.SetTip(icon, spot);
                    line.Children.Add(icon);
                    var view = itemBox(box);
                    view.Margin = new Thickness(4.0, 0.0, 8.0, 0.0);
                    line.Children.Add(view);
                }
                lines.Add(line);
                lineWidth = Math.Max(lineWidth, 114.0 + 3 * (21.0 + 42.0));
            }
            shownColumns = bestColumns();
            var perColumn = (lines.Count + shownColumns - 1) / shownColumns;   // (rounded up; read down each column, then the next)
            for (var i = 0; i < lines.Count; i += perColumn)
            {
                var column = new StackPanel { Orientation = Orientation.Vertical, Width = lineWidth, Margin = new Thickness(i == 0 ? 0.0 : 16.0, 0.0, 0.0, 0.0) };
                column.Children.AddRange(lines.Skip(i).Take(perColumn));
                rows.Children.Add(column);
            }
        }
        w.SizeChanged += (_, _) => { if (bestColumns() != shownColumns) redraw(); };
        redraw();
        // (subscribed only while on screen; see spotSummaryContent)
        var slotsHandler = new Microsoft.FSharp.Control.FSharpHandler<Microsoft.FSharp.Core.Unit>((_, _) => redraw());
        System.Action questHandler = () => redraw();
        var wasDetached = false;   // redraw only on coming back: box views thrown away before they are ever on screen would stay subscribed
        rows.AttachedToVisualTree += (_, _) =>
        {
            TrackerModel.shuffleSlotsChanged.Publish.AddHandler(slotsHandler);
            TrackerModel.level2StairBoxEnabledChanged.Publish.AddHandler(slotsHandler);
            TrackerModel.level9FloorBoxEnabledChanged.Publish.AddHandler(slotsHandler);
            OptionsMenu.secondQuestDungeonsOptionChanged += questHandler;
            if (wasDetached) redraw();
        };
        rows.DetachedFromVisualTree += (_, _) =>
        {
            wasDetached = true;
            TrackerModel.shuffleSlotsChanged.Publish.RemoveHandler(slotsHandler);
            TrackerModel.level2StairBoxEnabledChanged.Publish.RemoveHandler(slotsHandler);
            TrackerModel.level9FloorBoxEnabledChanged.Publish.RemoveHandler(slotsHandler);
            OptionsMenu.secondQuestDungeonsOptionChanged -= questHandler;
        };
        rows.AddHandler(InputElement.PointerReleasedEvent, new EventHandler<PointerReleasedEventArgs>((_, _) => refocus()), RoutingStrategies.Bubble, true);
        return rows;
    }

    public static bool insideMakePopoutCall = false;
    public static void makePopout(PopoutKind kind, bool isLeftClick, bool isRightClick, Graphics.SharedEvent doUIUpdateEvent)
    {
        // (WPF: ResizeMode=CanResizeWithGrip; Avalonia windows resize from their edges and corners, there is no separate grip)
        var w = new Window { CanResize = true, SizeToContent = SizeToContent.WidthAndHeight, Topmost = false, ShowActivated = false };   // resizable: the content scales to fit the window (drag the edges or the corner grip)
        // the window opens at the content's natural size (DownOnly keeps the Viewbox from blowing the content up to fill the screen-sized space the window offers while sizing),
        // then it's frozen at that size and the content is free to scale both ways as the user drags the window bigger or smaller
        var savedW = 0.0; var savedH = 0.0;   // size remembered from the last session (0 = none yet)
        // (port: Avalonia has no RestoreBounds, so the last bounds seen while the window was in its normal state stand in for it)
        double restoreLeft = 0.0, restoreTop = 0.0, restoreWidth = 0.0, restoreHeight = 0.0;
        void noteRestoreBounds()
        {
            if (w.WindowState == WindowState.Normal && !(leftOf(w) < MINIMIZED_THRESHOLD))
            {
                restoreLeft = leftOf(w); restoreTop = topOf(w);
                restoreWidth = w.ClientSize.Width; restoreHeight = w.ClientSize.Height;
            }
        }
        string sz() => $",{(int)restoreWidth},{(int)restoreHeight}";
        // (WPF: ContentRendered; Avalonia: Opened, which comes after the first layout, when the window has sized itself to its content)
        Control scalable(Control e)
        {
            var vb = new Viewbox { Child = e, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
            w.Opened += (_, _) =>
            {
                if (w.SizeToContent != SizeToContent.Manual)
                {
                    var (aw, ah) = ((savedW > 0.0 ? savedW : w.ClientSize.Width), (savedH > 0.0 ? savedH : w.ClientSize.Height));
                    w.SizeToContent = SizeToContent.Manual;
                    w.Width = aw;
                    w.Height = ah;
                }
                vb.StretchDirection = StretchDirection.Both;
            };
            return vb;
        }
        // like scalable, but the content is not scaled: it re-flows onto more or fewer rows as the window is resized
        Control wrapping(Control e)
        {
            w.Opened += (_, _) =>
            {
                if (w.SizeToContent != SizeToContent.Manual)
                {
                    var (aw, ah) = ((savedW > 0.0 ? savedW : w.ClientSize.Width), (savedH > 0.0 ? savedH : w.ClientSize.Height));
                    w.SizeToContent = SizeToContent.Manual;
                    w.Width = aw;
                    w.Height = ah;
                }
            };
            return e;
        }
        //w.Owner <- Application.Current.MainWindow  // we don't do this; doing so causes clicking any window to bring all ZT windows to the front, making impossible to layer
                                                     // popout ZT windows with other Windows applications in Z-order of windows desktop
        EventHandler onMainClosed = (_, _) => { theMainWindowHasClosed = true; try { w.Close(); } catch { } };  // do this to get similar close behavior to ownership
        var mainW = ShowRunCustom.mainWindow();
        mainW.Closed += onMainClosed;
        w.Closed += (_, _) => mainW.Closed -= onMainClosed;   // (each popout used to leave this handler, and its closed window, on the main window for the session)
        Action save = () => { };
        Action noDisplay = () => { };
        insideMakePopoutCall = true;
        // (port: WPF checks only `w.Left < MINIMIZED_THRESHOLD`, because Windows moves a minimized window to -32000; other systems leave it in place, so the window state is checked too)
        bool minimizedOrClosing() => leftOf(w) < MINIMIZED_THRESHOLD || w.WindowState == WindowState.Minimized;
        System.Text.RegularExpressions.Match matches;
        switch (kind)
        {
            case PopoutKind.SpotSummaryPopout:
                if (theSpotSummaryPopoutWindow != null)
                    theSpotSummaryPopoutWindow.Close();
                theSpotSummaryPopoutWindow = w;
                w.Title = "Spot Summary";
                w.Content = scalable(spotSummaryContent(doUIUpdateEvent));
                save = () =>
                {
                    noteRestoreBounds();
                    if (minimizedOrClosing()) // minimized or closing
                        TrackerModelOptions.SpotSummaryPopout_DisplayedLT = $"0,{(int)restoreLeft},{(int)restoreTop}" + sz();
                    else
                        TrackerModelOptions.SpotSummaryPopout_DisplayedLT = $"1,{(int)leftOf(w)},{(int)topOf(w)}" + sz();
                    ShowRunCustom.writeSettingsSoon();   // (called on every move event)
                };
                noDisplay = () =>
                {
                    if (TrackerModelOptions.SpotSummaryPopout_DisplayedLT.Length != 0)
                    {
                        TrackerModelOptions.SpotSummaryPopout_DisplayedLT = "0" + TrackerModelOptions.SpotSummaryPopout_DisplayedLT.Substring(1);
                        TrackerModelOptions.writeSettings();
                    }
                };
                matches = System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.SpotSummaryPopout_DisplayedLT, @"^(1|0),(-?\d+),(-?\d+)(?:,(\d+),(\d+))?$");
                break;
            case PopoutKind.InventoryAndHeartsPopout:
                if (theInventoryAndHeartsPopoutWindow != null)
                    theInventoryAndHeartsPopoutWindow.Close();
                theInventoryAndHeartsPopoutWindow = w;
                w.Title = "Current Equipment";
                w.Content = scalable(inventoryAndHeartsContent(doUIUpdateEvent));
                save = () =>
                {
                    noteRestoreBounds();
                    if (minimizedOrClosing()) // minimized or closing
                        TrackerModelOptions.InventoryAndHeartsPopout_DisplayedLT = $"0,{(int)restoreLeft},{(int)restoreTop}" + sz();
                    else
                        TrackerModelOptions.InventoryAndHeartsPopout_DisplayedLT = $"1,{(int)leftOf(w)},{(int)topOf(w)}" + sz();
                    ShowRunCustom.writeSettingsSoon();   // (called on every move event)
                };
                noDisplay = () =>
                {
                    if (TrackerModelOptions.InventoryAndHeartsPopout_DisplayedLT.Length != 0)
                    {
                        TrackerModelOptions.InventoryAndHeartsPopout_DisplayedLT = "0" + TrackerModelOptions.InventoryAndHeartsPopout_DisplayedLT.Substring(1);
                        TrackerModelOptions.writeSettings();
                    }
                };
                matches = System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.InventoryAndHeartsPopout_DisplayedLT, @"^(1|0),(-?\d+),(-?\d+)(?:,(\d+),(\d+))?$");
                break;
            case PopoutKind.RemainingItemsPoput:
                if (theRemainingItemsPopoutWindow != null)
                    theRemainingItemsPopoutWindow.Close();
                theRemainingItemsPopoutWindow = w;
                w.Title = "Uncollected Items";
                w.Content = wrapping(remainingItemsContent(doUIUpdateEvent));
                save = () =>
                {
                    noteRestoreBounds();
                    if (minimizedOrClosing()) // minimized or closing
                        TrackerModelOptions.RemainingItemsPoput_DisplayedLT = $"0,{(int)restoreLeft},{(int)restoreTop}" + sz();
                    else
                        TrackerModelOptions.RemainingItemsPoput_DisplayedLT = $"1,{(int)leftOf(w)},{(int)topOf(w)}" + sz();
                    ShowRunCustom.writeSettingsSoon();   // (called on every move event)
                };
                noDisplay = () =>
                {
                    if (TrackerModelOptions.RemainingItemsPoput_DisplayedLT.Length != 0)
                    {
                        TrackerModelOptions.RemainingItemsPoput_DisplayedLT = "0" + TrackerModelOptions.RemainingItemsPoput_DisplayedLT.Substring(1);
                        TrackerModelOptions.writeSettings();
                    }
                };
                matches = System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.RemainingItemsPoput_DisplayedLT, @"^(1|0),(-?\d+),(-?\d+)(?:,(\d+),(\d+))?$");
                break;
            case PopoutKind.ShopsPopout:
                if (theShopsPopoutWindow != null)
                    theShopsPopoutWindow.Close();
                theShopsPopoutWindow = w;
                w.Title = "Shops";
                w.Content = scalable(shopsContent(w));
                save = () =>
                {
                    noteRestoreBounds();
                    if (minimizedOrClosing()) // minimized or closing
                        TrackerModelOptions.ShopsPopout_DisplayedLT = $"0,{(int)restoreLeft},{(int)restoreTop}" + sz();
                    else
                        TrackerModelOptions.ShopsPopout_DisplayedLT = $"1,{(int)leftOf(w)},{(int)topOf(w)}" + sz();
                    ShowRunCustom.writeSettingsSoon();   // (called on every move event)
                };
                noDisplay = () =>
                {
                    if (TrackerModelOptions.ShopsPopout_DisplayedLT.Length != 0)
                    {
                        TrackerModelOptions.ShopsPopout_DisplayedLT = "0" + TrackerModelOptions.ShopsPopout_DisplayedLT.Substring(1);
                        TrackerModelOptions.writeSettings();
                    }
                };
                matches = System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.ShopsPopout_DisplayedLT, @"^(1|0),(-?\d+),(-?\d+)(?:,(\d+),(\d+))?$");
                break;
            case PopoutKind.ShuffleItemsPopout:
                if (theShuffleItemsPopoutWindow != null)
                    theShuffleItemsPopoutWindow.Close();
                theShuffleItemsPopoutWindow = w;
                w.Title = "M/C/Tri Shuffle";
                w.Content = scalable(shuffleItemsContent(w));
                save = () =>
                {
                    noteRestoreBounds();
                    if (minimizedOrClosing()) // minimized or closing
                        TrackerModelOptions.ShuffleItemsPopout_DisplayedLT = $"0,{(int)restoreLeft},{(int)restoreTop}" + sz();
                    else
                        TrackerModelOptions.ShuffleItemsPopout_DisplayedLT = $"1,{(int)leftOf(w)},{(int)topOf(w)}" + sz();
                    ShowRunCustom.writeSettingsSoon();   // (called on every move event)
                };
                noDisplay = () =>
                {
                    if (TrackerModelOptions.ShuffleItemsPopout_DisplayedLT.Length != 0)
                    {
                        TrackerModelOptions.ShuffleItemsPopout_DisplayedLT = "0" + TrackerModelOptions.ShuffleItemsPopout_DisplayedLT.Substring(1);
                        TrackerModelOptions.writeSettings();
                    }
                };
                matches = System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.ShuffleItemsPopout_DisplayedLT, @"^(1|0),(-?\d+),(-?\d+)(?:,(\d+),(\d+))?$");
                break;
            default: throw new InvalidOperationException();
        }
        var (ok, disp, left, top) =
            (matches.Success && !(parseFloat(matches.Groups[2].Value) < MINIMIZED_THRESHOLD))
                ? (true, (int.Parse(matches.Groups[1].Value)) == 1, parseFloat(matches.Groups[2].Value), parseFloat(matches.Groups[3].Value))
                : (false, false, 0.0, 0.0);
        if (matches.Success && matches.Groups[4].Success && matches.Groups[5].Success && parseFloat(matches.Groups[4].Value) >= 100.0 && parseFloat(matches.Groups[5].Value) >= 60.0)
        {
            savedW = parseFloat(matches.Groups[4].Value);
            savedH = parseFloat(matches.Groups[5].Value);
            w.SizeToContent = SizeToContent.Manual;   // apply the remembered size up front (before Show), rather than after first render
            w.Width = savedW;
            w.Height = savedH;
        }
        if ((isLeftClick && ok)                                  // the user explicitly is trying to show the window, and we are able to parse valid coords
            || (!isLeftClick && !isRightClick && disp))          // it's startup automatically looking for windows to display, and this one has disp=1 and valid coords
        {
            setLeftTop(w, left, top);
        }
        // otherwise (e.g. right click, bad data) just display a new window at default location selected by Windows
        w.PositionChanged += (_, _) =>
        {
            save();
            if (kind == PopoutKind.ShopsPopout || kind == PopoutKind.ShuffleItemsPopout) refocusMainWindow(null, () => raiseWithoutFocus(w));   // (dragged over the main window, it stays in front)
            else refocusMainWindow();
        };
        var sizeSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600.0) };
        sizeSaveTimer.Tick += (_, _) => { sizeSaveTimer.Stop(); save(); };
        w.SizeChanged += (_, _) =>   // remember the size the user drags it to
        {
            if (w.IsLoaded && w.SizeToContent == SizeToContent.Manual)
            {
                noteRestoreBounds();   // (read while the window is open: its position can't be read once it has closed)
                sizeSaveTimer.Stop();
                sizeSaveTimer.Start();
            }
        };
        void setDisplayedLT(string s)
        {
            switch (kind)
            {
                case PopoutKind.SpotSummaryPopout: TrackerModelOptions.SpotSummaryPopout_DisplayedLT = s; break;
                case PopoutKind.InventoryAndHeartsPopout: TrackerModelOptions.InventoryAndHeartsPopout_DisplayedLT = s; break;
                case PopoutKind.RemainingItemsPoput: TrackerModelOptions.RemainingItemsPoput_DisplayedLT = s; break;
                case PopoutKind.ShopsPopout: TrackerModelOptions.ShopsPopout_DisplayedLT = s; break;
                case PopoutKind.ShuffleItemsPopout: TrackerModelOptions.ShuffleItemsPopout_DisplayedLT = s; break;
            }
        }
        w.Closed += (_, _) =>
        {
            // (a resize just before closing is saved now, not dropped with the timer. It uses the bounds remembered while the window was open,
            // since a closed window's position reads as 0,0. noDisplay below then marks it closed if the user closed it.)
            if (sizeSaveTimer.IsEnabled)
            {
                sizeSaveTimer.Stop();
                if (restoreWidth > 0.0 && restoreHeight > 0.0)
                {
                    setDisplayedLT($"1,{(int)restoreLeft},{(int)restoreTop}" + sz());
                    ShowRunCustom.writeSettingsSoon();
                }
            }
            if (!(theMainWindowHasClosed) && !(insideMakePopoutCall)) noDisplay();
        };
        w.Closed += (_, _) =>   // (so the Popout Windows menu knows it is no longer open)
        {
            switch (kind)
            {
                case PopoutKind.SpotSummaryPopout: if (ReferenceEquals(theSpotSummaryPopoutWindow, w)) theSpotSummaryPopoutWindow = null; break;
                case PopoutKind.InventoryAndHeartsPopout: if (ReferenceEquals(theInventoryAndHeartsPopoutWindow, w)) theInventoryAndHeartsPopoutWindow = null; break;
                case PopoutKind.RemainingItemsPoput: if (ReferenceEquals(theRemainingItemsPopoutWindow, w)) theRemainingItemsPopoutWindow = null; break;
                case PopoutKind.ShopsPopout: if (ReferenceEquals(theShopsPopoutWindow, w)) theShopsPopoutWindow = null; break;
                case PopoutKind.ShuffleItemsPopout: if (ReferenceEquals(theShuffleItemsPopoutWindow, w)) theShuffleItemsPopoutWindow = null; break;
            }
        };
        w.Show();
        insideMakePopoutCall = false;
    }

    // used by the Popout Windows menu: is a popout open, open it (at its remembered spot), close it (which is remembered too)
    public static Graphics.SharedEvent theUIUpdateEvent = null;   // (F#: an option; null for None)
    private static Window popoutWindowOf(PopoutKind kind) =>
        kind switch
        {
            PopoutKind.SpotSummaryPopout => theSpotSummaryPopoutWindow,
            PopoutKind.InventoryAndHeartsPopout => theInventoryAndHeartsPopoutWindow,
            PopoutKind.RemainingItemsPoput => theRemainingItemsPopoutWindow,
            PopoutKind.ShopsPopout => theShopsPopoutWindow,
            PopoutKind.ShuffleItemsPopout => theShuffleItemsPopoutWindow,
            _ => throw new InvalidOperationException()
        };
    public static bool isPopoutOpen(PopoutKind kind) => popoutWindowOf(kind) != null;
    public static void openPopout(PopoutKind kind)
    {
        var ev = theUIUpdateEvent;
        if (ev != null) { if (popoutWindowOf(kind) == null) makePopout(kind, true, false, ev); }
    }
    public static void closePopout(PopoutKind kind)
    {
        var w = popoutWindowOf(kind);
        if (w != null) { try { w.Close(); } catch { } }
    }

    // shop price info: tile-state → (display name, price range text, min, max, step)
    private static readonly Dictionary<int, (string, string, int, int, int)> shopPriceInfo = new Dictionary<int, (string, string, int, int, int)>
    {
        [TrackerModel.MapSquareChoiceDomainHelper.ARROW] =        ("Arrow Shop",    "60-100r",  60, 100, 10),
        [TrackerModel.MapSquareChoiceDomainHelper.BOMB] =         ("Bomb Shop",     "1-40r",     1,  40,  5),
        [TrackerModel.MapSquareChoiceDomainHelper.BOOK] =         ("Book Shop",    "180-220r", 180, 220, 10),
        [TrackerModel.MapSquareChoiceDomainHelper.BLUE_CANDLE] =  ("Candle Shop",  "40-80r",   40,  80, 10),
        [TrackerModel.MapSquareChoiceDomainHelper.BLUE_RING] =    ("Blue Ring",   "230-255r",  230, 255,  5),
        [TrackerModel.MapSquareChoiceDomainHelper.MEAT] =         ("Meat Shop",   "40-120r",   40, 120, 10),
        [TrackerModel.MapSquareChoiceDomainHelper.KEY] =          ("Key Shop",    "60-120r",   60, 120, 10),
        [TrackerModel.MapSquareChoiceDomainHelper.SHIELD] =       ("Shield Shop", "70-180r",   70, 180, 10),
        [TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP] =    ("Hint Shop",   "10-60r",    10,  60, 10),
        [TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP] =  ("Potion Shop", "20-88r",    20,  88,  8),
    };

    private static Window theShopPricesWindow = null;

    public static void showShopPricesWindow()
    {
        if (theShopPricesWindow != null)
        {
            theShopPricesWindow.Activate();
            return;
        }
        var w = new Window { Title = "Shop Prices", SizeToContent = SizeToContent.WidthAndHeight,
                             Background = Brushes.Black, Foreground = Brushes.Orange, Topmost = false };
        EventHandler onMainClosed = (_, _) => { try { w.Close(); } catch { } };
        var mainW = ShowRunCustom.mainWindow();
        mainW.Closed += onMainClosed;
        w.Closed += (_, _) => { theShopPricesWindow = null; mainW.Closed -= onMainClosed; };
        theShopPricesWindow = w;
        var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(6.0) };
        var header = new TextBox { Text = "Shops found on overworld map — enter the price you saw:", IsReadOnly = true,
                                   Background = Brushes.Black, Foreground = Brushes.Orange, BorderThickness = new Thickness(0.0), Margin = new Thickness(0.0, 0.0, 0.0, 4.0) };
        sp.Children.Add(header);
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // 0: coord
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // 1: icon
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120.0) });  // 2: name
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70.0) });   // 3: range
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160.0) });  // 4: price dropdown
        void makeCell(string text, int col, int row)
        {
            var tb = new TextBox { Text = text, IsReadOnly = true, Background = Brushes.Black, Foreground = Brushes.Orange, BorderThickness = new Thickness(0.0), Margin = new Thickness(2.0), VerticalContentAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tb, col);
            Grid.SetRow(tb, row);
            grid.Children.Add(tb);
        }
        var rowIdx = 0;
        // collect all marked shop tiles
        var shops = new List<(int, int, int)>();
        for (var j = 0; j <= 7; j++)
        {
            for (var i = 0; i <= 15; i++)
            {
                var state = TrackerModel.overworldMapMarks[i, j].Current();
                if (shopPriceInfo.ContainsKey(state))
                    shops.Add((i, j, state));
            }
        }
        if (shops.Count == 0)
        {
            sp.Children.Add(new TextBox { Text = "No shops found on the overworld map yet.", IsReadOnly = true,
                                          Background = Brushes.Black, Foreground = Brushes.DarkGray, BorderThickness = new Thickness(0.0) });
        }
        else
        {
            foreach (var (i, j, state) in shops)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var colLabel = $"{(char)((int)'A' + j)}{i + 1}";
                makeCell(colLabel, 0, rowIdx);
                // icon
                var bmp = new OverworldMapTileCustomization.MapStateProxy(state).DefaultInteriorBmp();
                var img = Graphics.BMPtoImage(bmp);
                img.Width = 20.0; img.Height = 20.0;
                img.Margin = new Thickness(2.0);
                Grid.SetColumn(img, 1);
                Grid.SetRow(img, rowIdx);
                grid.Children.Add(img);
                var (name, range, pMin, pMax, pStep) = shopPriceInfo[state];
                makeCell(name, 2, rowIdx);
                makeCell(range, 3, rowIdx);
                // price dropdown
                var cb = new ComboBox { Margin = new Thickness(2.0), Background = Brushes.Black, Foreground = Brushes.Orange, Width = 150.0 };
                cb.Items.Add("(unknown)");
                var selectedIdx = 0;
                var valIdx = 1;
                var currentPrice = TrackerModel.getShopPrice(i, j);
                var v = pMin;
                while (v <= pMax)
                {
                    cb.Items.Add($"{v}r");
                    if (v == currentPrice) selectedIdx = valIdx;
                    valIdx = valIdx + 1;
                    v = v + pStep;
                }
                cb.SelectedIndex = selectedIdx;
                var (ii, jj) = (i, j);  // capture loop vars
                cb.SelectionChanged += (_, _) =>
                {
                    if (cb.SelectedIndex == 0)
                        TrackerModel.setShopPrice(ii, jj, 0);
                    else
                    {
                        var text = cb.SelectedItem.ToString();
                        if (int.TryParse(text.TrimEnd('r'), out var price))
                            TrackerModel.setShopPrice(ii, jj, price);
                    }
                };
                Grid.SetColumn(cb, 4);
                Grid.SetRow(cb, rowIdx);
                grid.Children.Add(cb);
                rowIdx = rowIdx + 1;
            }
            sp.Children.Add(grid);
        }
        var sv = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 600.0 };
        var border = new Border { Child = sv, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.0), Background = Brushes.Black };   // no grey border in the popout window
        ShowRunCustom.makeScalable(w, border);
        // remember where the window was left (and how big)
        var m = System.Text.RegularExpressions.Regex.Match(TrackerModelOptions.ShopPricesWindowLTWH, @"^(-?\d+),(-?\d+),(\d+),(\d+)$");
        if (m.Success && !(parseFloat(m.Groups[1].Value) < ShowRunCustom.MINIMIZED_THRESHOLD) && parseFloat(m.Groups[3].Value) >= 100.0 && parseFloat(m.Groups[4].Value) >= 60.0)
        {
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            setLeftTop(w, parseFloat(m.Groups[1].Value), parseFloat(m.Groups[2].Value));
            w.SizeToContent = SizeToContent.Manual;
            w.Width = parseFloat(m.Groups[3].Value);
            w.Height = parseFloat(m.Groups[4].Value);
        }
        void saveBounds()
        {
            if (w.IsLoaded && !(leftOf(w) < ShowRunCustom.MINIMIZED_THRESHOLD) && w.WindowState == WindowState.Normal)
            {
                TrackerModelOptions.ShopPricesWindowLTWH = $"{(int)leftOf(w)},{(int)topOf(w)},{(int)w.Bounds.Width},{(int)w.Bounds.Height}";
                ShowRunCustom.writeSettingsSoon();   // (called on every move and resize event)
            }
        }
        w.PositionChanged += (_, _) => saveBounds();
        w.SizeChanged += (_, _) => { if (w.SizeToContent == SizeToContent.Manual) saveBounds(); };
        w.Show();
    }

    public static bool shuffleIsOn => TrackerModel.ShuffleMaps || TrackerModel.ShuffleCompasses || TrackerModel.ShuffleTriforces;
    public static void Startup(Graphics.SharedEvent doUIUpdateEvent)
    {
        theUIUpdateEvent = doUIUpdateEvent;
        void f(PopoutKind kind, string setting)
        {
            var matches = System.Text.RegularExpressions.Regex.Match(setting, @"^(1|0),(-?\d+),(-?\d+)(?:,(\d+),(\d+))?$");   // (optionally followed by the remembered size)
            var (disp, _left, _top) =
                (matches.Success && !(parseFloat(matches.Groups[2].Value) < MINIMIZED_THRESHOLD))
                    ? ((int.Parse(matches.Groups[1].Value)) == 1, parseFloat(matches.Groups[2].Value), parseFloat(matches.Groups[3].Value))
                    : (false, 0.0, 0.0);
            if (disp && (kind != PopoutKind.ShuffleItemsPopout || shuffleIsOn))
                makePopout(kind, false, false, doUIUpdateEvent);
        }
        foreach (var (k, s) in new[] { (PopoutKind.SpotSummaryPopout, TrackerModelOptions.SpotSummaryPopout_DisplayedLT),
                                       (PopoutKind.InventoryAndHeartsPopout, TrackerModelOptions.InventoryAndHeartsPopout_DisplayedLT),
                                       (PopoutKind.RemainingItemsPoput, TrackerModelOptions.RemainingItemsPoput_DisplayedLT),
                                       (PopoutKind.ShopsPopout, TrackerModelOptions.ShopsPopout_DisplayedLT),
                                       (PopoutKind.ShuffleItemsPopout, TrackerModelOptions.ShuffleItemsPopout_DisplayedLT) })
        {
            f(k, s);
        }
    }
}
