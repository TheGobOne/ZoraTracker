// Ported from Z1R_WPF/PieMenus.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// Every F# `async { }` here is an `async Task`/`Task<T>` method, as in CustomComboBoxes.cs; callers `await` them where WPF used `let!`/`do!`.
// The pie menus' panels and behaviors are built when the method is called (WPF: when the `Async<_>` value is created, i.e. also at the call,
// since every caller starts it right away).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Z1R_Avalonia;

public static class PieMenus
{
    public static void canvasAdd(Canvas c, Control item, double left, double top) => Graphics.canvasAdd(c, item, left, top);
    public static readonly double OMTW = Graphics.OMTW;

    public static List<Dock> pieDisabledDocks = new List<Dock>();   // set by the caller just before opening a pie menu: panels docked here are disabled (they can't be clicked or chosen by hotkey)
    // (F#: displaysDocksBehaviors is `((Canvas * (Brush -> unit)) * Dock * (unit -> unit))[]`)
    public static async Task FourWayPieMenu(CustomComboBoxes.CanvasManager cm, double h, ((Canvas, Action<IBrush>), Dock, Action)[] displaysDocksBehaviors, HotKeys.HotKeyProcessor<int> hkp)
    {
        var disabledDocks = pieDisabledDocks;
        pieDisabledDocks = new List<Dock>();
        var wh = new ManualResetEvent(false);
        var c = new Canvas { IsHitTestVisible = true };
        var MARGIN = 10.0;
        var w = 16.0 * OMTW - 2.0 * MARGIN;
        var someDrawnPixelToSeeMouseMoves = new Canvas { Width = w, Height = h, Background = Brushes.Black, Opacity = 0.01 };
        c.Children.Add(someDrawnPixelToSeeMouseMoves);
        var sbcs = new List<Action<IBrush>>();
        (Action<IBrush>, Action)? leftPanel = null, topPanel = null, rightPanel = null, bottomPanel = null;
        var selfCleanupFuncs = new List<Action>();
        foreach (var ((p, setBorderColor), dock, behavior) in displaysDocksBehaviors)
        {
            switch (dock)
            {
                case Dock.Left:   if (leftPanel != null)   throw new Exception("multiple lefts");   else leftPanel   = (setBorderColor, behavior); break;
                case Dock.Right:  if (rightPanel != null)  throw new Exception("multiple rights");  else rightPanel  = (setBorderColor, behavior); break;
                case Dock.Top:    if (topPanel != null)    throw new Exception("multiple tops");    else topPanel    = (setBorderColor, behavior); break;
                case Dock.Bottom: if (bottomPanel != null) throw new Exception("multiple bottoms"); else bottomPanel = (setBorderColor, behavior); break;
                default: throw new Exception("bad Dock value");
            }
            var dp = new DockPanel { Width = 16.0 * OMTW - 2.0 * MARGIN, Height = h, LastChildFill = false };
            DockPanel.SetDock(p, dock);
            dp.Children.Add(p);
            selfCleanupFuncs.Add(() => dp.Children.Clear());  // deparent the panels so we can reuse them
            canvasAdd(c, dp, MARGIN, MARGIN);
            sbcs.Add(setBorderColor);
        }
        void onCloseOrDismiss()
        {
            foreach (var f in sbcs) f(Brushes.Gray);
            foreach (var f in selfCleanupFuncs) f();
        }
        var targetBrush = Brushes.Gray;
        var innerH = h - 2.0 * displaysDocksBehaviors[0].Item1.Item1.Height;
        var g = new Grid { Width = w, Height = h };
        var circle = new Ellipse { Width = innerH / 1.5, Height = innerH / 1.5, Stroke = targetBrush, StrokeThickness = 3.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        g.Children.Add(circle);
        var outerCircle = new Ellipse { Width = innerH * 1.5, Height = innerH * 1.5, Stroke = targetBrush, StrokeThickness = 3.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        outerCircle.StrokeDashArray = new AvaloniaList<double>(new[] { 1.0, 2.0 });
        g.Children.Add(outerCircle);
        var innerR = circle.Width / 2.0;
        var outerR = outerCircle.Width / 2.0;
        var root2 = 1.414;
        var delta = outerR / root2 - innerR / root2;
        var w2 = 2.0 * outerR / root2;   // (F# shadows w)
        var lineCanvas = new Canvas { Width = w2, Height = w2 };   // inscribed in the outer circle
        var slash1 = new Line { StartPoint = new Point(0.0, w2), EndPoint = new Point(delta, w2 - delta), Stroke = targetBrush, StrokeThickness = 3.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        lineCanvas.Children.Add(slash1);
        var slash2 = new Line { StartPoint = new Point(w2 - delta, delta), EndPoint = new Point(w2, 0.0), Stroke = targetBrush, StrokeThickness = 3.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        lineCanvas.Children.Add(slash2);
        var backslash1 = new Line { StartPoint = new Point(0.0, 0.0), EndPoint = new Point(delta, delta), Stroke = targetBrush, StrokeThickness = 3.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        lineCanvas.Children.Add(backslash1);
        var backslash2 = new Line { StartPoint = new Point(w2 - delta, w2 - delta), EndPoint = new Point(w2, w2), Stroke = targetBrush, StrokeThickness = 3.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        lineCanvas.Children.Add(backslash2);
        g.Children.Add(lineCanvas);
        canvasAdd(c, g, MARGIN, MARGIN);
        var center = new Point(8.0 * OMTW, h * 0.5 + MARGIN);
        var currentSelection = -1;
        bool isDisabledSelection() => disabledDocks.Exists(d => (int)d == currentSelection);
        c.PointerMoved += (_, ea) =>
        {
            var pos = ea.GetPosition(c);
            Vector vector = pos - center;
            var distance = vector.Length;
            foreach (var f in sbcs) f(Brushes.Gray);
            currentSelection = -1;
            if (distance > innerR)
            {
                if (vector.X > 0.0 && vector.X > Math.Abs(vector.Y))
                {
                    if (rightPanel is (var f, _))
                    {
                        f(Brushes.Yellow);
                        currentSelection = (int)Dock.Right;
                    }
                    else currentSelection = -1;
                }
                else if (vector.X < 0.0 && Math.Abs(vector.X) > Math.Abs(vector.Y))
                {
                    if (leftPanel is (var f, _))
                    {
                        f(Brushes.Yellow);
                        currentSelection = (int)Dock.Left;
                    }
                    else currentSelection = -1;
                }
                else if (vector.Y < 0.0 && Math.Abs(vector.Y) > Math.Abs(vector.X))
                {
                    if (topPanel is (var f, _))
                    {
                        f(Brushes.Yellow);
                        currentSelection = (int)Dock.Top;
                    }
                    else currentSelection = -1;
                }
                else if (vector.Y > 0.0 && vector.Y > Math.Abs(vector.X))
                {
                    if (bottomPanel is (var f, _))
                    {
                        f(Brushes.Yellow);
                        currentSelection = (int)Dock.Bottom;
                    }
                    else currentSelection = -1;
                }
                else
                    currentSelection = -1;
            }
            else
                currentSelection = -1;
        };
        void doBehavior()
        {
            if (currentSelection == (int)Dock.Left)
            {
                if (leftPanel is (_, var b)) b(); else throw new Exception("impossible");
            }
            else if (currentSelection == (int)Dock.Top)
            {
                if (topPanel is (_, var b)) b(); else throw new Exception("impossible");
            }
            else if (currentSelection == (int)Dock.Right)
            {
                if (rightPanel is (_, var b)) b(); else throw new Exception("impossible");
            }
            else if (currentSelection == (int)Dock.Bottom)
            {
                if (bottomPanel is (_, var b)) b(); else throw new Exception("impossible");
            }
            else // cancel
            { }
        }
        c.MyKeyAdd(ea =>
        {
            ea.Handled = true;
            if (hkp.TryGetValue(ea.Key, out var which))
            {
                if (which == 0) currentSelection = (int)Dock.Bottom;
                else if (which == 1) currentSelection = (int)Dock.Left;
                else if (which == 2) currentSelection = (int)Dock.Top;
                else if (which == 3) currentSelection = (int)Dock.Right;
                else throw new Exception("unexpected hkp which");
                if (!isDisabledSelection())
                {
                    doBehavior();
                    wh.Set();
                }
            }
        });
        void click(PointerEventArgs ea)
        {
            ea.Handled = true;
            doBehavior();
        }
        c.PointerPressed += (_, ea) =>
        {
            ea.Handled = true;
            if (!isDisabledSelection())   // clicking a disabled (crossed-out) panel does nothing
            {
                click(ea);
                wh.Set();
            }
        };
/*  This was intended to let e.g. a single mousedown-mousemove-mouseup drag-right from TakeAny to select heart, but it it sometimes fires spuriously; commenting out requires two separate clicks (mousedowns)
        var isFirstTimeMouseUp = true;
        c.PointerReleased += (_, ea) =>
        {
            if (isFirstTimeMouseUp && currentSelection == -1)
                isFirstTimeMouseUp = false;
            else
            {
                ea.Handled = true;
                click(ea);
                wh.Set();
            }
        };
*/
        Graphics.WarpMouseCursorTo(center);
        //await Task.Delay(10);  // ensure the cursor is warped by yielding briefly       // no longer necessary now that Warp...() does a synchronous pump
        var tb = new TextBox { Text = "Indicate which option you chose", Foreground = Brushes.Orange, Background = Brushes.Black, FontSize = 16.0, IsHitTestVisible = false,
                                BorderThickness = new Thickness(1.0), TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        canvasAdd(c, tb, 270.0, -15.0);
        await CustomComboBoxes.DoModal(cm, wh, 0.0, 16.0, c);
        onCloseOrDismiss();
    }

    public static readonly double takeAnyW = 330.0;
    public static readonly double takeAnyH = 220.0;
    public static readonly double BT = 5.0;
    public static readonly double rightMarginSize = 40.0;

    public static Canvas makeSkippedHeart()
    {
        var c = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black };
        canvasAdd(c, Graphics.BMPtoImage(Graphics.owHeartEmpty_bmp), 0.0, 0.0);
        Graphics.placeSkippedItemXDecoration(c);
        return c;
    }
    public static Canvas makeItemBox(System.Drawing.Bitmap itemBMP, IBrush yesno)
    {
        var c = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black };
        c.Children.Add(new Rectangle { Width = 30.0, Height = 30.0, Stroke = yesno, StrokeThickness = 3.0 });
        canvasAdd(c, Graphics.BMPtoImage(itemBMP), 4.0, 4.0);
        return c;
    }
    public static void makeXtoY(Control x, Control y, double rightMargin, StackPanel group)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0.0, 0.0, rightMargin, 0.0) };
        group.Children.Add(row);
        row.Children.Add(x);
        row.Children.Add(Graphics.BMPtoImage(Graphics.iconRightArrow_bmp));
        row.Children.Add(y);
    }

    public static readonly int TAKE_ANY = TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY;
    public static readonly int SWORD1 = TrackerModel.MapSquareChoiceDomainHelper.SWORD1;

    // TAKE ANY ONE YOU WANT

    // (keyOpt: the pretty hotkey text, or null for None)
    public static void addHotKey(Canvas c, string keyOpt)
    {
        if (keyOpt is string pretty)
        {
            var tb = new TextBox { Text = pretty, Foreground = Brushes.Orange, Background = Brushes.Black, FontSize = 16.0, IsHitTestVisible = false,
                                    BorderThickness = new Thickness(1.0), TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            canvasAdd(c, tb, 125.0, 175.0);
        }
    }

    public static (Canvas, Action<IBrush>) takeAnyCandlePanel(string keyOpt)
    {
        var c1 = makeItemBox(Graphics.blue_candle_bmp, CustomComboBoxes.no);
        var c2 = makeItemBox(Graphics.blue_candle_bmp, CustomComboBoxes.yes);
        var col = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black };
        var group = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        makeXtoY(Graphics.BMPtoImage(Graphics.owHeartEmpty_bmp), makeSkippedHeart(), rightMarginSize, group);
        makeXtoY(Graphics.BMPtoImage(Graphics.theInteriorBmpTable[TAKE_ANY][0]), Graphics.BMPtoImage(Graphics.theInteriorBmpTable[TAKE_ANY][1]), rightMarginSize, group);
        makeXtoY(c1, c2, 0.0, group);
        col.Children.Add(group);
        col.Children.Add(Graphics.BMPtoImage(Graphics.takeAnyCandleBMP));
        var b = new Border { Child = col, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(BT), Width = takeAnyW + 2.0 * BT, Height = takeAnyH + 2.0 * BT + 30.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var c = new Canvas { Width = b.Width, Height = b.Height };
        c.Children.Add(b);
        addHotKey(c, keyOpt);
        return (c, x => b.BorderBrush = x);
    }

    public static (Canvas, Action<IBrush>) takeAnyPotionPanel(string keyOpt)
    {
        var col = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black };
        var group = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        makeXtoY(Graphics.BMPtoImage(Graphics.owHeartEmpty_bmp), makeSkippedHeart(), rightMarginSize, group);
        makeXtoY(Graphics.BMPtoImage(Graphics.theInteriorBmpTable[TAKE_ANY][0]), Graphics.BMPtoImage(Graphics.theInteriorBmpTable[TAKE_ANY][1]), 0.0, group);
        col.Children.Add(group);
        col.Children.Add(Graphics.BMPtoImage(Graphics.takeAnyPotionBMP));
        var b = new Border { Child = col, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(BT), Width = takeAnyW + 2.0 * BT, Height = takeAnyH + 2.0 * BT + 30.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var c = new Canvas { Width = b.Width, Height = b.Height };
        c.Children.Add(b);
        addHotKey(c, keyOpt);
        return (c, x => b.BorderBrush = x);
    }

    public static (Canvas, Action<IBrush>) takeAnyHeartPanel(string keyOpt)
    {
        var col = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black };
        var group = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        makeXtoY(Graphics.BMPtoImage(Graphics.owHeartEmpty_bmp), Graphics.BMPtoImage(Graphics.owHeartFull_bmp), rightMarginSize, group);
        makeXtoY(Graphics.BMPtoImage(Graphics.theInteriorBmpTable[TAKE_ANY][0]), Graphics.BMPtoImage(Graphics.theInteriorBmpTable[TAKE_ANY][1]), 0.0, group);
        col.Children.Add(group);
        col.Children.Add(Graphics.BMPtoImage(Graphics.takeAnyHeartBMP));
        var b = new Border { Child = col, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(BT), Width = takeAnyW + 6.0, Height = takeAnyH + 2.0 * BT + 30.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var c = new Canvas { Width = b.Width, Height = b.Height };
        c.Children.Add(b);
        addHotKey(c, keyOpt);
        return (c, x => b.BorderBrush = x);
    }

    public static (Canvas, Action<IBrush>) takeAnyLeavePanel(string keyOpt)
    {
        var col = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black };
        var group = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        makeXtoY(Graphics.BMPtoImage(Graphics.theInteriorBmpTable[TAKE_ANY][0]), Graphics.BMPtoImage(Graphics.theInteriorBmpTable[TAKE_ANY][0]), 0.0, group);
        col.Children.Add(Graphics.BMPtoImage(Graphics.takeAnyLeaveBMP));
        col.Children.Add(group);
        var b = new Border { Child = col, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(BT), Width = takeAnyW + 6.0, Height = takeAnyH + 2.0 * BT + 30.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var c = new Canvas { Width = b.Width, Height = b.Height };
        c.Children.Add(b);
        addHotKey(c, keyOpt);
        return (c, x => b.BorderBrush = x);
    }

    public static int lastTakeAnyKind = 0;   // what the last take-any popup chose: 0 = nothing, 1 = heart, 2 = candle, 3 = potion
    public static bool lastTakeAnyDismissed = false;   // the last take-any button box was dismissed without choosing anything (so the caller can leave the tile as it was)
    public static async Task<bool> TakeAnyPieMenuAsync(CustomComboBoxes.CanvasManager cm, double h)
    {
        int whichHeart()
        {
            if (TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyHeart(0) == 0) return 0;
            else if (TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyHeart(1) == 0) return 1;
            else if (TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyHeart(2) == 0) return 2;
            else if (TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyHeart(3) == 0) return 3;
            else
            {
                Graphics.ErrorBeepWithReminderLogText("All four take-any hearts have already been marked, so no marking will be made");
                return -1;
            }
        }
        var r = false;
        void candleBehavior()
        {
            var which = whichHeart();
            if (which != -1)
                TrackerModel.playerProgressAndTakeAnyHearts.SetTakeAnyHeart(which, 2);
            TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle.Set(true);
            lastTakeAnyKind = 2;
            r = true;
        }
        void potionBehavior()
        {
            var which = whichHeart();
            if (which != -1)
                TrackerModel.playerProgressAndTakeAnyHearts.SetTakeAnyHeart(which, 2);
            lastTakeAnyKind = 3;
            r = true;
        }
        void heartBehavior()
        {
            var which = whichHeart();
            if (which != -1)
                TrackerModel.playerProgressAndTakeAnyHearts.SetTakeAnyHeart(which, 1);
            lastTakeAnyKind = 1;
            r = true;
        }
        var hkp = HotKeys.TakeAnyHotKeyProcessor;
        var bordersDocksBehaviors = new ((Canvas, Action<IBrush>), Dock, Action)[] {
            (takeAnyCandlePanel(hkp.AsPrettyHotKeyOpt(2)), Dock.Top,    candleBehavior),
            (takeAnyPotionPanel(hkp.AsPrettyHotKeyOpt(1)), Dock.Left,   potionBehavior),
            (takeAnyHeartPanel( hkp.AsPrettyHotKeyOpt(3)), Dock.Right,  heartBehavior),
            (takeAnyLeavePanel( hkp.AsPrettyHotKeyOpt(0)), Dock.Bottom, () => { }),
            };
        await FourWayPieMenu(cm, h, bordersDocksBehaviors, hkp);
        return r;
    }

    // IT'S DANGEROUS TO GO ALONE - TAKE THIS

    public static (Canvas, Action<IBrush>) takeThisCandlePanel(string keyOpt)
    {
        var c1 = makeItemBox(Graphics.blue_candle_bmp, CustomComboBoxes.no);
        var c2 = makeItemBox(Graphics.blue_candle_bmp, CustomComboBoxes.yes);
        var col = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black };
        var group = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        makeXtoY(c1, c2, rightMarginSize, group);
        makeXtoY(Graphics.BMPtoImage(Graphics.theInteriorBmpTable[SWORD1][0]), Graphics.BMPtoImage(Graphics.theInteriorBmpTable[SWORD1][1]), 0.0, group);
        col.Children.Add(group);
        col.Children.Add(Graphics.BMPtoImage(Graphics.takeThisCandleBMP));
        var b = new Border { Child = col, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(BT), Width = takeAnyW + 6.0, Height = takeAnyH + 2.0 * BT + 30.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var c = new Canvas { Width = b.Width, Height = b.Height };
        c.Children.Add(b);
        addHotKey(c, keyOpt);
        return (c, x => b.BorderBrush = x);
    }

    public static (Canvas, Action<IBrush>) takeThisWoodSwordPanel(string keyOpt)
    {
        var c1 = makeItemBox(Graphics.ow_sword1_item_bmp, CustomComboBoxes.no);
        var c2 = makeItemBox(Graphics.ow_sword1_item_bmp, CustomComboBoxes.yes);
        var col = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black };
        var group = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        makeXtoY(c1, c2, rightMarginSize, group);
        makeXtoY(Graphics.BMPtoImage(Graphics.theInteriorBmpTable[SWORD1][0]), Graphics.BMPtoImage(Graphics.theInteriorBmpTable[SWORD1][1]), 0.0, group);
        col.Children.Add(group);
        col.Children.Add(Graphics.BMPtoImage(Graphics.takeThisWoodSwordBMP));
        var b = new Border { Child = col, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(BT), Width = takeAnyW + 6.0, Height = takeAnyH + 2.0 * BT + 30.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var c = new Canvas { Width = b.Width, Height = b.Height };
        c.Children.Add(b);
        addHotKey(c, keyOpt);
        return (c, x => b.BorderBrush = x);
    }

    public static (Canvas, Action<IBrush>) takeThisLeavePanel(string keyOpt)
    {
        var col = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black };
        var group = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        makeXtoY(Graphics.BMPtoImage(Graphics.theInteriorBmpTable[SWORD1][0]), Graphics.BMPtoImage(Graphics.theInteriorBmpTable[SWORD1][0]), 0.0, group);
        col.Children.Add(Graphics.BMPtoImage(Graphics.takeThisLeaveBMP));
        col.Children.Add(group);
        var b = new Border { Child = col, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(BT), Width = takeAnyW + 6.0, Height = takeAnyH + 2.0 * BT + 30.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var c = new Canvas { Width = b.Width, Height = b.Height };
        c.Children.Add(b);
        addHotKey(c, keyOpt);
        return (c, x => b.BorderBrush = x);
    }

    public static async Task<bool> TakeThisPieMenuAsync(CustomComboBoxes.CanvasManager cm, double h)
    {
        var r = false;
        void candleBehavior()
        {
            TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle.Set(true);
            r = true;
        }
        void swordBehavior()
        {
            TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword.Set(true);
            r = true;
        }
        var hkp = HotKeys.TakeThisHotKeyProcessor;
        var bordersDocksBehaviors = new ((Canvas, Action<IBrush>), Dock, Action)[] {
            (takeThisWoodSwordPanel(hkp.AsPrettyHotKeyOpt(2)), Dock.Top,    swordBehavior),
            (takeThisCandlePanel(hkp.AsPrettyHotKeyOpt(1)),    Dock.Left,   candleBehavior),
            (takeThisLeavePanel(hkp.AsPrettyHotKeyOpt(0)),     Dock.Bottom, () => { }),
            };
        await FourWayPieMenu(cm, h, bordersDocksBehaviors, hkp);
        return r;
    }

    // the wood sword cave's pie menu: the usual three hotkey-able choices, plus a 4th (right) for the less common 'Shuffled Item' / 'Sword Icon' choices.
    // returns 0 = leave/cancel, 1 = took sword, 2 = took candle, 3 = other (the caller asks which)
    public static (Canvas, Action<IBrush>) takeThisOtherPanel()
    {
        var col = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black, VerticalAlignment = VerticalAlignment.Center };
        col.Children.Add(new TextBlock { Text = "Shuffled item\nor Sword in Shop...", Foreground = Brushes.Orange, FontSize = 20.0, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
        var b = new Border { Child = col, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(BT), Width = takeAnyW + 6.0, Height = takeAnyH + 2.0 * BT + 30.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var c = new Canvas { Width = b.Width, Height = b.Height };
        c.Children.Add(b);
        return (c, x => b.BorderBrush = x);
    }

    public static Action cheapKeysChangedHook = () => { };   // set by the main UI: redraws the overworld tiles when a shop's 'cheap keys' box changes
    public static Action<int, int> removeShopItemAtHook = (_, _) => { };   // set by the main UI: remove the item at that position from shop n
    public static Func<int, List<int>> shopItemIdentsHook = _ => new List<int>();   // set by the main UI: the item idents behind the icons of numbered shop n (index 0 = the P shop), in order
    public static Func<int, int> shopKeyIndexHook = _ => -1;   // set by the main UI: index of the key in the items shown for numbered shop n, or -1 if that shop has no key
    public static List<string> legacyDisabledChoices = new List<string>();   // set with crossedOutChoices: the crossed-out choices that are off because of Legacy Mode (they get a 'Disabled In Legacy Mode' tooltip; the box clears it)
    public static List<string> crossedOutChoices = new List<string>();   // set by the caller just before opening a cave choice box: these choices are shown crossed out and can't be picked (the box clears it)
    // a giant red X over a panel, to show it is crossed out (disabled)
    public static void addGiantRedX(Canvas c)
    {
        var m = 14.0;
        foreach (var (x1, y1, x2, y2) in new[] { (m, m, c.Width - m, c.Height - m), (c.Width - m, m, m, c.Height - m) })
            // (WPF sets StrokeStartLineCap and StrokeEndLineCap; Avalonia has one StrokeLineCap for both ends)
            c.Children.Add(new Line { StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2), Stroke = Brushes.Red, StrokeThickness = 14.0, StrokeLineCap = PenLineCap.Round, Opacity = 0.85, IsHitTestVisible = false });
    }
    public static async Task<int> TakeThisChoicePieMenuAsync(CustomComboBoxes.CanvasManager cm, double h)
    {
        var r = 0;
        var hkp = HotKeys.TakeThisHotKeyProcessor;
        var candleCrossed = crossedOutChoices.Contains("Took Candle");   // (left in place: the caller clears it, and the 'Shuffled Item' choice box reads it)
        (Canvas, Action<IBrush>) make_candlePanel()
        {
            var (c, setBorder) = takeThisCandlePanel(hkp.AsPrettyHotKeyOpt(1));
            if (candleCrossed)
            {
                addGiantRedX(c);
                pieDisabledDocks = new List<Dock> { Dock.Left };
                setBorder(Brushes.Red);
                return (c, _ => setBorder(Brushes.Red));   // always a red border (never the yellow hover highlight)
            }
            else
                return (c, setBorder);
        }
        var candlePanel = make_candlePanel();
        var bordersDocksBehaviors = new ((Canvas, Action<IBrush>), Dock, Action)[] {
            (takeThisWoodSwordPanel(hkp.AsPrettyHotKeyOpt(2)), Dock.Top,    () => r = 1),
            (candlePanel,                                      Dock.Left,   () => r = 2),
            (takeThisOtherPanel(),                             Dock.Right,  () => r = 3),
            (takeThisLeavePanel(hkp.AsPrettyHotKeyOpt(0)),     Dock.Bottom, () => { }),
            };
        await FourWayPieMenu(cm, h, bordersDocksBehaviors, hkp);
        return r;
    }

    // (port helper: WPF strikes through the read-only TextBox label of a crossed-out button with `TextDecorations`. Avalonia's TextBox has no
    // TextDecorations, so a crossed-out button gets a TextBlock with the same text and look instead; 2px side padding like the app-wide TextBox style.)
    private static TextBlock strikethroughCopy(TextBox tb) =>
        new TextBlock { Text = tb.Text, TextAlignment = tb.TextAlignment, VerticalAlignment = VerticalAlignment.Center, FontSize = tb.FontSize, Foreground = tb.Foreground,
                        Background = tb.Background, Padding = new Thickness(2.0, 0.0, 2.0, 0.0), TextDecorations = TextDecorations.Strikethrough, IsHitTestVisible = false };

    // the wood sword cave's chooser: one box of buttons that also answers the Contextual_TakeThis_* hotkeys (sword / candle / none).
    // returns "Took Sword", "Took Candle", "Shuffled Item", "Sword Icon", "Leave" (the 'none' hotkey), or null if dismissed (clicked outside)
    public static async Task<string> TakeThisButtonBoxAsync(CustomComboBoxes.CanvasManager cm)
    {
        var crossed = crossedOutChoices;
        crossedOutChoices = new List<string>();
        var legacyOff = legacyDisabledChoices;
        legacyDisabledChoices = new List<string>();
        var hkp = HotKeys.TakeThisHotKeyProcessor;
        var wh = new ManualResetEvent(false);
        string result = null;
        var col = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black, Margin = new Thickness(16.0) };
        void addButton(string text, string keyOpt, string value)
        {
            var label = keyOpt is string k ? $"{text}   [{k}]" : text;
            var b = new Button { Height = 34.0, MinWidth = 280.0, Margin = new Thickness(0.0, 4.0, 0.0, 4.0), Focusable = false, Background = Graphics.almostBlack, BorderBrush = Brushes.Orange };
            b.Content = new TextBox { Text = label, IsReadOnly = true, IsHitTestVisible = false, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(0.0), FontSize = 18.0, Foreground = Brushes.Orange, Background = Graphics.almostBlack };
            b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            b.VerticalContentAlignment = VerticalAlignment.Stretch;
            if (crossed.Contains(value))
            {
                b.Content = strikethroughCopy((TextBox)b.Content);
                b.Opacity = 0.5;
                if (legacyOff.Contains(value)) ToolTip.SetTip(b, "Disabled In Legacy Mode");
            }
            else
                b.Click += (_, _) => { result = value; wh.Set(); };
            col.Children.Add(b);
        }
        addButton("Took Sword", hkp.AsPrettyHotKeyOpt(2), "Took Sword");
        addButton("Took Candle", hkp.AsPrettyHotKeyOpt(1), "Took Candle");
        addButton("Shuffled Item", hkp.AsPrettyHotKeyOpt(3), "Shuffled Item");
        addButton("Sword in Shop", hkp.AsPrettyHotKeyOpt(4), "Sword Icon");
        var bord = new Border { Child = col, Background = Brushes.Black, BorderThickness = new Thickness(5.0), BorderBrush = Brushes.Gray };
        bord.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var c = new Canvas { Width = bord.DesiredSize.Width, Height = bord.DesiredSize.Height };
        c.Children.Add(bord);
        // hotkeys: 'sword' / 'candle' pick those, 'none' leaves the cave marked as not taken
        c.MyKeyAdd(ea =>
        {
            ea.Handled = true;
            if (hkp.TryGetValue(ea.Key, out var which))
            {
                if (which == 2) result = "Took Sword";
                else if (which == 1) result = "Took Candle";
                else if (which == 0) result = "Leave";
                else if (which == 3) result = "Shuffled Item";
                else if (which == 4) result = "Sword Icon";
                if (result != null && crossed.Contains(result)) result = null;
                if (result != null) wh.Set();
            }
        });
        var (areaW, areaH) = (cm.AppMainCanvas.Width, cm.AppMainCanvas.Height);
        var cx = areaW > 0.0 ? Math.Max(0.0, (areaW - c.Width) / 2.0) : 150.0;
        var cy = areaH > 0.0 ? Math.Max(0.0, (areaH - c.Height) / 2.0) : 200.0;
        // measured before it was in the window, so re-center it once it has its real size
        bord.SizeChanged += (_, _) =>
        {
            c.Width = bord.Bounds.Width;
            c.Height = bord.Bounds.Height;
            if (areaW > 0.0) Canvas.SetLeft(c, Math.Max(0.0, (areaW - bord.Bounds.Width) / 2.0));
            if (areaH > 0.0) Canvas.SetTop(c, Math.Max(0.0, (areaH - bord.Bounds.Height) / 2.0));
        };
        await CustomComboBoxes.DoModal(cm, wh, cx, cy, c);
        return result;
    }

    // a small box with a grid of icon buttons (the overworld menu's marker icons); returns the id of the one clicked, or -1 if dismissed
    public static Func<int, string> itemPickerDisabledTip = _ => null;   // a non-null result greys that item out with this tooltip (set by the caller before opening the picker)
    public static async Task<int> ItemPickerAsync(CustomComboBoxes.CanvasManager cm, IEnumerable<(int, Control)> items, int cols)
    {
        var wh = new ManualResetEvent(false);
        var result = -1;
        var grid = new UniformGrid { Columns = cols, Background = Brushes.Black, Margin = new Thickness(4.0) };
        foreach (var (ident, content) in items)
        {
            var b = new Button { MinWidth = 36.0, MinHeight = 40.0, Margin = new Thickness(2.0), Padding = new Thickness(4.0), Focusable = false, Background = Graphics.almostBlack, BorderBrush = Brushes.Orange, Content = content };
            var disabledTip = itemPickerDisabledTip(ident);
            if (disabledTip == null)
                b.Click += (_, _) => { result = ident; wh.Set(); };
            else
            {
                b.Opacity = 0.4;
                ToolTip.SetTip(b, disabledTip);
                ToolTip.SetShowOnDisabled(b, true);
                b.IsEnabled = false;
            }
            grid.Children.Add(b);
        }
        var bord = new Border { Child = grid, Background = Brushes.Black, BorderThickness = new Thickness(5.0), BorderBrush = Brushes.Gray };
        bord.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var c = new Canvas { Width = bord.DesiredSize.Width, Height = bord.DesiredSize.Height };
        c.Children.Add(bord);
        var (areaW, areaH) = (cm.AppMainCanvas.Width, cm.AppMainCanvas.Height);
        var cx = areaW > 0.0 ? Math.Max(0.0, (areaW - c.Width) / 2.0) : 150.0;
        var cy = areaH > 0.0 ? Math.Max(0.0, (areaH - c.Height) / 2.0) : 200.0;
        await CustomComboBoxes.DoModal(cm, wh, cx, cy, c);
        return result;
    }

    // the Shop flyout (opened from the 'Shop' icon in the overworld menu): the P shop (potions) or one of the 4 numbered shops (each button also shows the items placed in that shop).
    // counts.[n] = how many tiles carry that shop (index 0 = the P shop); current = the shop number already on this tile (0 = none).
    // returns -1 if dismissed, 0 = P shop, 1-4 = that numbered shop
    // right clicking a shop button asks pickItem() for a marker icon (-1 if none) and puts it in that shop
    // a plain orange-on-black checkbox (no button frame)
    // (WPF parses a XAML ControlTemplate with an IsChecked trigger; here it's a code template that shows the tick when IsChecked changes, as in Graphics.roundedCheckBoxTemplate)
    private static readonly FuncControlTemplate<CheckBox> orangeCheckBoxTemplate =
        new FuncControlTemplate<CheckBox>((cb, _scope) =>
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Transparent };
            var tick = new Path { Name = "tick", Data = Geometry.Parse("M 2,7 L 6,11 L 12,2"), Stroke = Brushes.Orange, StrokeThickness = 2.5, IsVisible = false };
            var bd = new Border { Width = 16.0, Height = 16.0, BorderBrush = Brushes.Orange, BorderThickness = new Thickness(2.0), Background = Brushes.Black, VerticalAlignment = VerticalAlignment.Center, Child = tick };
            var cp = new ContentPresenter { Margin = new Thickness(8.0, 0.0, 0.0, 0.0), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(bd);
            sp.Children.Add(cp);
            void refresh()
            {
                cp.Content = cb.Content;
                cp.ContentTemplate = cb.ContentTemplate;
                tick.IsVisible = cb.IsChecked == true;
            }
            refresh();
            cb.PropertyChanged += (_, ea) =>
            {
                var p = ea.Property;
                if (p == ContentControl.ContentProperty || p == ContentControl.ContentTemplateProperty || p == ToggleButton.IsCheckedProperty)
                    refresh();
            };
            return sp;
        });
    private static readonly Cursor handCursor = new Cursor(StandardCursorType.Hand);
    public static CheckBox makeOrangeCheckBox(string text, bool isOn)
    {
        var cb = new CheckBox { Content = text, Foreground = Brushes.Orange, FontSize = 14.0, Margin = new Thickness(10.0, 3.0, 10.0, 3.0), Focusable = false, Cursor = handCursor };
        cb.Template = orangeCheckBoxTemplate;
        cb.IsChecked = isOn;
        return cb;
    }
    // Legacy Mode: the numbered shops can only hold these items (wood arrow, blue candle, blue ring, regular key, bombs, meat, book, shield)
    public static bool legacyShopAllows(int ident) =>
        ident == TrackerModel.ITEMS.WOODARROW || ident == TrackerModel.ITEMS.BLUECANDLE || ident == TrackerModel.ITEMS.OLDKEY || ident == TrackerModel.ITEMS.BLUERING || ident == TrackerModel.DECOR_BOMB
        || ident == TrackerModel.ITEMS.MEAT || ident == TrackerModel.ITEMS.BOOKSHIELD || ident == TrackerModel.ITEMS.SHIELD;
    // (F#: shopItems is `(Bitmap*bool) list[]`, addItem is `int*int -> int[] * (Bitmap*bool) list[]`, pickItem is `unit -> Async<int>`)
    public static async Task<int> ShopFlyoutAsync(CustomComboBoxes.CanvasManager cm, int[] counts, int _current, List<(System.Drawing.Bitmap, bool)>[] shopItems, bool potionOk,
                                                  Func<int, int, (int[], List<(System.Drawing.Bitmap, bool)>[])> addItem, Func<Task<int>> pickItem)
    {
        var wh = new ManualResetEvent(false);
        var result = -1;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Black, Margin = new Thickness(2.0) };
        var hkp = HotKeys.ShopHotKeyProcessor;
        // (hotkey indexes: 0 = P shop, 1-4 = that numbered shop)
        var itemPanels = new List<(int, StackPanel)>();   // (shop index, the row showing what is in that shop), refreshed when an item is added
        var countBlocks = new TextBlock[5];
        var cheapBoxes = new CheckBox[5];   // the 'cheap keys' checkboxes of shops 1-4 (shown once a key is in that shop)
        var latestItems = (List<(System.Drawing.Bitmap, bool)>[])shopItems.Clone();
        void fillItems(int n, StackPanel panel, List<(System.Drawing.Bitmap, bool)> items)
        {
            panel.Children.Clear();
            void addImgB(System.Drawing.Bitmap bmp, bool collected, bool boxed)
            {
                var img = Graphics.BMPtoImage(bmp);
                img.Stretch = Stretch.Uniform;
                img.Width = 21.0;
                img.Height = 21.0;
                img.Margin = new Thickness(1.0);
                if (collected) img.Opacity = 0.4;
                if (boxed) panel.Children.Add(new Border { BorderBrush = Brushes.Yellow, BorderThickness = new Thickness(2.0), Child = img });   // the cheaper key shop's key
                else panel.Children.Add(img);
            }
            void addImg(System.Drawing.Bitmap bmp, bool collected) => addImgB(bmp, collected, false);
            if (n == 0 && TrackerModel.LegacyMode)
            {
                // Legacy Mode: the P shop is just the two potions, with no item between them
                addImg(Graphics.blue_potion_bmp, false);
                addImg(Graphics.red_potion_bmp, false);
            }
            else if (n == 0)
            {
                // the P shop always shows the blue potion, then its one item (a ? until something is placed), then the red potion
                addImg(Graphics.blue_potion_bmp, false);
                if (items.Count > 0)
                {
                    var (bmp, collected) = items[0];
                    addImg(bmp, collected);
                }
                else
                    panel.Children.Add(new TextBlock { Text = "?", Foreground = Brushes.White, FontSize = 16.0, FontWeight = FontWeight.Bold, Width = 23.0, Height = 23.0, TextAlignment = TextAlignment.Center });
                addImg(Graphics.red_potion_bmp, false);
            }
            else
            {
                var keyIdx = (n >= 1 && n <= 4 && TrackerModel.cheapKeyShops[n]) ? shopKeyIndexHook(n) : -1;
                for (var i = 0; i < items.Count; i++)
                {
                    var (bmp, collected) = items[i];
                    addImgB(bmp, collected, (i == keyIdx));
                }
            }
        }
        // shows the 'cheap keys' checkbox of each numbered shop that holds a key (and drops the setting from one that no longer does), and redraws the key's box
        void refreshCheapBoxes()
        {
            for (var n = 1; n <= 4; n++)
            {
                var cb = cheapBoxes[n];
                if (cb != null)
                {
                    var hasKey = shopKeyIndexHook(n) >= 0;
                    if (!hasKey)
                    {
                        TrackerModel.cheapKeyShops[n] = false;
                        cb.IsChecked = false;
                    }
                    cb.IsVisible = hasKey;   // (collapsed: no key, no gap, so the icons center between the title and the bottom)
                    foreach (var (m, panel) in itemPanels)
                        if (m == n) fillItems(m, panel, latestItems[m]);
                }
            }
        }
        void applyItem(int shop, int ident, bool removing = false)
        {
            // the item goes into that shop right away and the menu stays open (clicking a shop or its hotkey is what closes it)
            // (removing an item already in the shop is always allowed, even one Legacy Mode would not let you add)
            if (!removing && TrackerModel.LegacyMode && shop != 0 && !legacyShopAllows(ident))
                Graphics.ErrorBeepWithReminderLogText("Legacy Mode: a shop can only hold a wood arrow, blue candle, blue ring, regular key, bombs, meat, book or shield");
            else if (!(TrackerModel.LegacyMode && shop == 0))   // (Legacy Mode: the P shop holds only its two potions, no item)
            {
                var (newCounts, newItems) = addItem(shop, ident);
                for (var n = 0; n <= 4; n++) latestItems[n] = newItems[n];
                foreach (var (n, panel) in itemPanels) fillItems(n, panel, newItems[n]);
                for (var n = 0; n <= 4; n++) countBlocks[n].Text = $"x{newCounts[n]}";
                refreshCheapBoxes();
            }
        }
        var hoverButtons = new List<(Button, int)>();   // (button, shop index) of the enabled ones, to find which shop an item hotkey is meant for
        Button addButton(Control content, int value, bool enabled)
        {
            // the content sits centered in the top part and the hotkey label is pinned to the bottom, so it lines up across all the buttons
            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.VerticalAlignment = VerticalAlignment.Stretch;
            Grid.SetRow(content, 0);
            g.Children.Add(content);
            if (hkp.AsPrettyHotKeyOpt(value) is string k)
            {
                var tb = new TextBlock { Text = $"[{k}]", Foreground = Brushes.Gray, FontSize = 10.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom };
                Grid.SetRow(tb, 1);
                g.Children.Add(tb);
            }
            var b = new Button { MinHeight = 56.0, MinWidth = 32.0, Margin = new Thickness(2.0), Padding = new Thickness(4.0), Focusable = false, Background = Graphics.almostBlack, BorderBrush = Brushes.Orange };
            b.Content = g;
            b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            b.VerticalContentAlignment = VerticalAlignment.Stretch;
            if (enabled)
            {
                // ('Swap left/right click': left click adds items to the shop and right click places it)
                void place() { result = value; wh.Set(); }
                void addItems()
                {
                    CustomComboBoxes.StartImmediate(((Func<Task>)(async () =>
                    {
                        var ident = await pickItem();
                        if (ident >= 0) applyItem(value, ident);
                    }))());
                }
                bool overCheap() => cheapBoxes.Any(c => c != null && c.IsPointerOver);   // (clicks on a 'cheap keys' box are not clicks on the shop)
                b.Click += (_, _) => { if (!overCheap()) { if (TrackerModelOptions.SwapMouseButtons) addItems(); else place(); } };
                // right click: pick a marker icon from the overworld menu's icons to add to this shop
                // (WPF: PreviewMouseRightButtonUp → a tunnelling PointerReleased for the right button)
                b.AddHandler(InputElement.PointerReleasedEvent, new EventHandler<PointerReleasedEventArgs>((_, ea) =>
                {
                    if (ea.InitialPressMouseButton != MouseButton.Right) return;
                    ea.Handled = true;
                    if (!overCheap()) { if (TrackerModelOptions.SwapMouseButtons) place(); else addItems(); }
                }), RoutingStrategies.Tunnel);
            }
            else b.Opacity = 0.4;
            if (enabled) hoverButtons.Add((b, value));
            row.Children.Add(b);
            return b;
        }
        Button firstButton = null;
        for (var n0 = 0; n0 <= 4; n0++)
        {
            var n = n0;   // (each iteration's handlers keep their own n, as in F#)
            var sp = new Grid { HorizontalAlignment = HorizontalAlignment.Center, MinHeight = 108.0 };   // (the same shop window height as before 'cheap keys' was added)
            sp.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sp.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
            var top = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };   // (title + 'cheap keys' on top; the items are centered in the space under them)
            sp.Children.Add(top);
            // the shop's title: its number, with how many tiles carry it right beside it
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            titleRow.Children.Add(new TextBlock { Text = (n == 0 ? "P" : n.ToString()), Foreground = Brushes.Orange, FontSize = 24.0, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center });
            var cb = new TextBlock { Text = $"x{counts[n]}", Foreground = Brushes.Orange, FontSize = 10.0, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(6.0, 0.0, 0.0, 6.0) };
            countBlocks[n] = cb;
            titleRow.Children.Add(cb);
            top.Children.Add(titleRow);
            if (n >= 1)
            {
                // 'cheap keys': appears under the title once a key is in this shop; checked = that key gets a yellow box (it is the cheaper of the key shops)
                var cheapCb = makeOrangeCheckBox("cheap keys", TrackerModel.cheapKeyShops[n]);
                cheapCb.FontSize = 10.0;
                cheapCb.Margin = new Thickness(0.0, 0.0, 0.0, 2.0);
                cheapCb.HorizontalAlignment = HorizontalAlignment.Center;
                cheapCb.IsVisible = shopKeyIndexHook(n) >= 0;
                cheapCb.Click += (_, _) =>
                {
                    TrackerModel.cheapKeyShops[n] = (cheapCb.IsChecked == true);
                    foreach (var (m, panel) in itemPanels)
                        if (m == n) fillItems(m, panel, latestItems[m]);
                    cheapKeysChangedHook();
                };
                cheapBoxes[n] = cheapCb;
                top.Children.Add(cheapCb);
            }
            // the items placed in this shop (collected ones are dimmed), a row under the count
            var items = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0.0, 2.0, 0.0, 2.0), MinHeight = 27.0, MinWidth = (n == 0 && TrackerModel.LegacyMode ? 0.0 : 72.0) };
            fillItems(n, items, shopItems[n]);
            // middle click an item icon to remove that item from the shop (by position: heart containers repeat, so toggling would add another)
            // (the P shop's panel is blue potion, its item, red potion: the item is the middle icon)
            items.AddHandler(InputElement.PointerPressedEvent, new EventHandler<PointerPressedEventArgs>((_, ea) =>
            {
                if (ea.GetCurrentPoint(items).Properties.PointerUpdateKind != PointerUpdateKind.MiddleButtonPressed) return;
                ea.Handled = true;
                var p = ea.GetPosition(items);
                var childIdx = items.Children.ToList().FindIndex(ch => ch.Bounds.Contains(p));
                var itemIdx = n == 0 ? childIdx - 1 : childIdx;
                var idents = shopItemIdentsHook(n);
                if (childIdx >= 0 && itemIdx >= 0 && itemIdx < idents.Count && !(TrackerModel.LegacyMode && n == 0))
                {
                    removeShopItemAtHook(n, itemIdx);
                    for (var m = 0; m <= 4; m++) latestItems[m] = OverworldMapTileCustomization.shopItemBitmapsHook(m);
                    foreach (var (m, panel) in itemPanels) fillItems(m, panel, latestItems[m]);
                    refreshCheapBoxes();
                }
            }), RoutingStrategies.Tunnel);
            itemPanels.Add((n, items));
            Grid.SetRow(items, 1);
            sp.Children.Add(items);
            var btn = addButton(sp, n, (n != 0 || potionOk));   // the P shop is greyed out once all the potion shops are placed
            if (n == 0) firstButton = btn;
        }
        // two option toggles above the shops
        CheckBox makeToggle(string text, TrackerModelOptions.Bool opt)
        {
            var t = makeOrangeCheckBox(text, opt.Value);
            t.FontSize = 11.0;   // (smaller, so three fit)
            t.Margin = new Thickness(5.0, 3.0, 5.0, 3.0);
            t.Click += (_, _) =>
            {
                opt.Value = (t.IsChecked == true);
                TrackerModelOptions.writeSettings();
            };
            return t;
        }
        var header = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0.0, 2.0, 0.0, 0.0) };
        header.Children.Add(makeToggle("Jump mouse to shop", TrackerModelOptions.JumpMouseToShop));
        header.Children.Add(makeToggle("Hover shop tooltip", TrackerModelOptions.HoverShopTooltip));
        header.Children.Add(makeToggle("Shops in menu", TrackerModelOptions.ShopsInMenu));   // (applies the next time the overworld menu opens)
        CheckBox make_swapToggle()
        {
            var t = makeOrangeCheckBox("Swap L/R click: left adds items, right places", TrackerModelOptions.SwapMouseButtons);
            t.FontSize = 11.0;
            t.Margin = new Thickness(5.0, 3.0, 5.0, 3.0);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.Click += (_, _) =>
            {
                TrackerModelOptions.SwapMouseButtons = (t.IsChecked == true);
                TrackerModelOptions.writeSettings();
            };
            return t;
        }
        var swapToggle = make_swapToggle();
        swapToggle.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        row.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var tip = new TextBlock { Text = "Right click to enter shops then click (or hotkey) to place on the map. You can also place the shop on the map first and enter items after.",
                                  TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Orange, FontSize = 12.0, TextAlignment = TextAlignment.Center, Width = Math.Max(200.0, row.DesiredSize.Width - 16.0), Margin = new Thickness(6.0, 6.0, 6.0, 2.0) };
        tip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        header.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var headerH = tip.DesiredSize.Height + tip.Margin.Top + tip.Margin.Bottom + header.DesiredSize.Height + swapToggle.DesiredSize.Height + swapToggle.Margin.Top + swapToggle.Margin.Bottom + 2.0;
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(tip);
        stack.Children.Add(swapToggle);
        stack.Children.Add(header);
        stack.Children.Add(row);
        var bord = new Border { Child = stack, Background = Brushes.Black, BorderThickness = new Thickness(5.0), BorderBrush = Brushes.Gray };
        bord.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var c = new Canvas { Width = bord.DesiredSize.Width, Height = bord.DesiredSize.Height };
        c.Children.Add(bord);
        // an item hotkey goes into the shop under the mouse (no shop hovered: the item is returned as 1000 + item id, a plain item marker on the tile)
        void itemPick(int ident)
        {
            var hovered = hoverButtons.FindIndex(t => t.Item1.IsPointerOver);
            if (hovered >= 0)
            {
                var (_, shop) = hoverButtons[hovered];
                applyItem(shop, ident);
            }
            else
            {
                result = 1000 + ident;
                wh.Set();
            }
        }
        c.MyKeyAdd(ea =>
        {
            ea.Handled = true;
            if (hkp.TryGetValue(ea.Key, out var which))
            {
                if (which != 0 || potionOk)
                {
                    result = which;
                    wh.Set();
                }
                else { }
            }
            else
            {
                // the overworld item hotkeys (candle, arrow, bomb, ...) also work here: the item goes in the shop under the mouse
                if (HotKeys.OverworldHotKeyProcessor.TryGetValue(ea.Key, out var st) && ((TrackerModel.MapSquareChoiceDomainHelper.IsItem(st) && st != TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP)
                        || st == TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER || st == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || st == TrackerModel.MapSquareChoiceDomainHelper.SWORD3))
                {
                    int ident;
                    if (st == TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER) ident = TrackerModel.ITEMS.LETTER;
                    else if (st == TrackerModel.MapSquareChoiceDomainHelper.SWORD2) ident = TrackerModel.ITEMS.WHITESWORD;
                    else if (st == TrackerModel.MapSquareChoiceDomainHelper.SWORD3) ident = TrackerModel.ITEMS.MAGICALSWORD;
                    else if (st == TrackerModel.MapSquareChoiceDomainHelper.ARROW) ident = TrackerModel.ITEMS.WOODARROW;
                    else if (st == TrackerModel.MapSquareChoiceDomainHelper.BOMB) ident = TrackerModel.DECOR_BOMB;
                    else if (st == TrackerModel.MapSquareChoiceDomainHelper.BOOK) ident = TrackerModel.ITEMS.BOOKSHIELD;
                    else if (st == TrackerModel.MapSquareChoiceDomainHelper.BLUE_CANDLE) ident = TrackerModel.ITEMS.BLUECANDLE;
                    else if (st == TrackerModel.MapSquareChoiceDomainHelper.BLUE_RING) ident = TrackerModel.ITEMS.BLUERING;
                    else if (st == TrackerModel.MapSquareChoiceDomainHelper.MEAT) ident = TrackerModel.ITEMS.MEAT;
                    else if (st == TrackerModel.MapSquareChoiceDomainHelper.KEY) ident = TrackerModel.ITEMS.OLDKEY;
                    else ident = TrackerModel.ITEMS.SHIELD;
                    itemPick(ident);
                }
                else
                {
                    // the OverworldMarker_* and Item_* hotkeys work here too
                    int? markerIdent;
                    if (HotKeys.OverworldMarkerHotKeyProcessor.TryGetValue(ea.Key, out var mIdent))
                        markerIdent = mIdent != HotKeys.CLEAR_TILE_HOTKEY ? mIdent : null;
                    else if (HotKeys.ItemHotKeyProcessor.TryGetValue(ea.Key, out var item) && HotKeys.OverworldMarkerItems.Contains(item))
                        markerIdent = item;
                    else
                        markerIdent = null;
                    if (markerIdent is int ident)
                        itemPick(ident);
                }
            }
        });
        var (areaW, areaH) = (cm.AppMainCanvas.Width, cm.AppMainCanvas.Height);
        var cx = areaW > 0.0 ? Math.Max(0.0, (areaW - c.Width) / 2.0) : 150.0;
        var cy = areaH > 0.0 ? Math.Max(0.0, (areaH - c.Height) / 2.0) : 200.0;
        // the cursor jumps onto the first shop button (the border is 5 wide, the row has a 2 margin, and the button a 2 margin)
        firstButton.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        if (TrackerModelOptions.JumpMouseToShop.Value)
            Graphics.WarpMouseCursorTo(new Point(cx + 5.0 + 2.0 + 2.0 + firstButton.DesiredSize.Width / 2.0, cy + 5.0 + headerH + 2.0 + firstButton.DesiredSize.Height / 2.0));
        // measured before it was in the window (the buttons come out smaller then), so once it has its real size: size the click area to match what is drawn
        // (otherwise a click on the right or lower part counts as outside the window and closes it) and re-center it
        var resizedOnce = false;
        bord.SizeChanged += (_, _) =>
        {
            c.Width = bord.Bounds.Width;
            c.Height = bord.Bounds.Height;
            var left = areaW > 0.0 ? Math.Max(0.0, (areaW - bord.Bounds.Width) / 2.0) : cx;
            var top = areaH > 0.0 ? Math.Max(0.0, (areaH - bord.Bounds.Height) / 2.0) : cy;
            Canvas.SetLeft(c, left);
            Canvas.SetTop(c, top);
            if (!resizedOnce)
            {
                resizedOnce = true;
                if (TrackerModelOptions.JumpMouseToShop.Value)   // (the cursor jump above aimed at the first, smaller layout)
                    Graphics.WarpMouseCursorTo(new Point(left + 5.0 + 2.0 + 2.0 + firstButton.Bounds.Width / 2.0, top + 5.0 + headerH + 2.0 + firstButton.Bounds.Height / 2.0));
            }
        };
        await CustomComboBoxes.DoModal(cm, wh, cx, cy, c);
        return result;
    }

    // the take-any cave's chooser, in the same style as the wood sword cave's: one box of buttons that also answers the Contextual_TakeAnyBox_* hotkeys.
    // returns true if the cave should be marked as done (took something; false for 'Took Nothing' or if dismissed)
    public static async Task<bool> TakeAnyButtonBoxAsync(CustomComboBoxes.CanvasManager cm)
    {
        var hkp = HotKeys.TakeAnyBoxHotKeyProcessor;
        var pp = TrackerModel.playerProgressAndTakeAnyHearts;
        var wh = new ManualResetEvent(false);
        string result = null;
        var col = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black, Margin = new Thickness(16.0) };
        // (hotkey indexes: 0 = nothing, 1 = potion, 2 = candle, 3 = heart)
        void addButton(string text, int hotkeyIndex)
        {
            var label = hkp.AsPrettyHotKeyOpt(hotkeyIndex) is string k ? $"{text}   [{k}]" : text;
            var b = new Button { Height = 34.0, MinWidth = 280.0, Margin = new Thickness(0.0, 4.0, 0.0, 4.0), Focusable = false, Background = Graphics.almostBlack, BorderBrush = Brushes.Orange };
            b.Content = new TextBox { Text = label, IsReadOnly = true, IsHitTestVisible = false, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(0.0), FontSize = 18.0, Foreground = Brushes.Orange, Background = Graphics.almostBlack };
            b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            b.VerticalContentAlignment = VerticalAlignment.Stretch;
            b.Click += (_, _) => { result = text; wh.Set(); };
            col.Children.Add(b);
        }
        addButton("Took Heart", 3);
        addButton("Took Candle", 2);
        addButton("Took Potion", 1);
        addButton("Took Nothing", 0);
        var bord = new Border { Child = col, Background = Brushes.Black, BorderThickness = new Thickness(5.0), BorderBrush = Brushes.Gray };
        bord.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var c = new Canvas { Width = bord.DesiredSize.Width, Height = bord.DesiredSize.Height };
        c.Children.Add(bord);
        c.MyKeyAdd(ea =>
        {
            ea.Handled = true;
            if (hkp.TryGetValue(ea.Key, out var which))
            {
                if (which == 3) result = "Took Heart";
                else if (which == 2) result = "Took Candle";
                else if (which == 1) result = "Took Potion";
                else if (which == 0) result = "Took Nothing";
                if (result != null) wh.Set();
            }
        });
        var (areaW, areaH) = (cm.AppMainCanvas.Width, cm.AppMainCanvas.Height);
        var cx = areaW > 0.0 ? Math.Max(0.0, (areaW - c.Width) / 2.0) : 150.0;
        var cy = areaH > 0.0 ? Math.Max(0.0, (areaH - c.Height) / 2.0) : 200.0;
        // measured before it was in the window, so re-center it once it has its real size
        bord.SizeChanged += (_, _) =>
        {
            c.Width = bord.Bounds.Width;
            c.Height = bord.Bounds.Height;
            if (areaW > 0.0) Canvas.SetLeft(c, Math.Max(0.0, (areaW - bord.Bounds.Width) / 2.0));
            if (areaH > 0.0) Canvas.SetTop(c, Math.Max(0.0, (areaH - bord.Bounds.Height) / 2.0));
        };
        await CustomComboBoxes.DoModal(cm, wh, cx, cy, c);
        var choice = result;
        lastTakeAnyDismissed = choice == null;
        if (choice == null || choice == "Took Nothing")
            return false;
        else
        {
            // same behavior as the old take-any picture menu: the next empty take-any heart slot is marked (an empty heart for the candle / potion, a full one for the heart)
            int which;
            if (pp.GetTakeAnyHeart(0) == 0) which = 0;
            else if (pp.GetTakeAnyHeart(1) == 0) which = 1;
            else if (pp.GetTakeAnyHeart(2) == 0) which = 2;
            else if (pp.GetTakeAnyHeart(3) == 0) which = 3;
            else
            {
                Graphics.ErrorBeepWithReminderLogText("All four take-any hearts have already been marked, so no marking will be made");
                which = -1;
            }
            if (which != -1)
                pp.SetTakeAnyHeart(which, (choice == "Took Heart" ? 1 : 2));
            if (choice == "Took Candle")
                pp.PlayerHasBlueCandle.Set(true);
            lastTakeAnyKind = (choice == "Took Heart" ? 1 : choice == "Took Candle" ? 2 : 3);
            return true;
        }
    }

    // a question with a row of buttons, in the same in-app style as the take-any cave's box (no OS dialog); returns the clicked button's text, or null if dismissed (clicked outside)
    public static async Task<string> ConfirmBoxAsync(CustomComboBoxes.CanvasManager cm, string question, IEnumerable<string> buttonTexts)
    {
        var wh = new ManualResetEvent(false);
        string result = null;
        var col = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black, Margin = new Thickness(16.0) };
        col.Children.Add(new TextBlock { Text = question, Foreground = Brushes.White, FontSize = 16.0, TextWrapping = TextWrapping.Wrap, MaxWidth = 360.0, Margin = new Thickness(0.0, 0.0, 0.0, 12.0) });
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var text in buttonTexts)
        {
            var b = new Button { Height = 34.0, MinWidth = 120.0, Margin = new Thickness(6.0, 0.0, 6.0, 0.0), Focusable = false, Background = Graphics.almostBlack, BorderBrush = Brushes.Orange };
            b.Content = new TextBox { Text = text, IsReadOnly = true, IsHitTestVisible = false, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(0.0), FontSize = 18.0, Foreground = Brushes.Orange, Background = Graphics.almostBlack };
            b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            b.VerticalContentAlignment = VerticalAlignment.Stretch;
            var t = text;
            b.Click += (_, _) => { result = t; wh.Set(); };
            row.Children.Add(b);
        }
        col.Children.Add(row);
        var bord = new Border { Child = col, Background = Brushes.Black, BorderThickness = new Thickness(5.0), BorderBrush = Brushes.Gray };
        bord.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var c = new Canvas { Width = bord.DesiredSize.Width, Height = bord.DesiredSize.Height };
        c.Children.Add(bord);
        c.MyKeyAdd(ea => ea.Handled = true);
        var (areaW, areaH) = (cm.AppMainCanvas.Width, cm.AppMainCanvas.Height);
        var cx = areaW > 0.0 ? Math.Max(0.0, (areaW - c.Width) / 2.0) : 150.0;
        var cy = areaH > 0.0 ? Math.Max(0.0, (areaH - c.Height) / 2.0) : 200.0;
        // measured before it was in the window, so re-center it once it has its real size
        bord.SizeChanged += (_, _) =>
        {
            c.Width = bord.Bounds.Width;
            c.Height = bord.Bounds.Height;
            if (areaW > 0.0) Canvas.SetLeft(c, Math.Max(0.0, (areaW - bord.Bounds.Width) / 2.0));
            if (areaH > 0.0) Canvas.SetTop(c, Math.Max(0.0, (areaH - bord.Bounds.Height) / 2.0));
        };
        await CustomComboBoxes.DoModal(cm, wh, cx, cy, c);
        return result;
    }

    // the other sword caves and the letter cave use the same style of choice box as the wood sword cave (without the hotkeys); returns the chosen label, or null if dismissed
    public static async Task<string> CaveChoiceBoxAsync(CustomComboBoxes.CanvasManager cm, IEnumerable<string> labels)
    {
        var crossed = crossedOutChoices;
        crossedOutChoices = new List<string>();
        var legacyOff = legacyDisabledChoices;
        legacyDisabledChoices = new List<string>();
        var wh = new ManualResetEvent(false);
        string result = null;
        var col = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black, Margin = new Thickness(16.0) };
        var hkp = HotKeys.TakeThisHotKeyProcessor;
        // the same Contextual_TakeThis_* hotkeys as the wood sword cave: 2=Took Sword, 3=Shuffled Item, 4=Sword Icon, 5=Took Letter
        int indexOf(string label) => label switch { "Took Sword" => 2, "Shuffled Item" => 3, "Sword Icon" => 4, "Took Letter" => 5, _ => -1 };
        foreach (var label in labels)
        {
            var shown = label == "Sword Icon" ? "Sword in Shop" : label;
            var displayLabel = hkp.AsPrettyHotKeyOpt(indexOf(label)) is string k ? $"{shown}   [{k}]" : shown;
            var b = new Button { Height = 34.0, MinWidth = 280.0, Margin = new Thickness(0.0, 4.0, 0.0, 4.0), Focusable = false, Background = Graphics.almostBlack, BorderBrush = Brushes.Orange };
            b.Content = new TextBox { Text = displayLabel, IsReadOnly = true, IsHitTestVisible = false, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(0.0), FontSize = 18.0, Foreground = Brushes.Orange, Background = Graphics.almostBlack };
            b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            b.VerticalContentAlignment = VerticalAlignment.Stretch;
            if (crossed.Contains(label))
            {
                b.Content = strikethroughCopy((TextBox)b.Content);
                b.Opacity = 0.5;
                if (label == "Took Sword" && TrackerModel.ProgressiveItems) ToolTip.SetTip(b, "Disabled Due to Progressive Items");
                if (legacyOff.Contains(label)) ToolTip.SetTip(b, "Disabled In Legacy Mode");
            }
            else
                b.Click += (_, _) => { result = label; wh.Set(); };
            col.Children.Add(b);
        }
        var bord = new Border { Child = col, Background = Brushes.Black, BorderThickness = new Thickness(5.0), BorderBrush = Brushes.Gray };
        bord.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var c = new Canvas { Width = bord.DesiredSize.Width, Height = bord.DesiredSize.Height };
        c.Children.Add(bord);
        c.MyKeyAdd(ea =>
        {
            ea.Handled = true;
            if (hkp.TryGetValue(ea.Key, out var which))
            {
                var l = labels.FirstOrDefault(l => indexOf(l) == which);
                if (l != null && !crossed.Contains(l)) { result = l; wh.Set(); }
            }
        });
        var (areaW, areaH) = (cm.AppMainCanvas.Width, cm.AppMainCanvas.Height);
        var cx = areaW > 0.0 ? Math.Max(0.0, (areaW - c.Width) / 2.0) : 150.0;
        var cy = areaH > 0.0 ? Math.Max(0.0, (areaH - c.Height) / 2.0) : 200.0;
        // measured before it was in the window, so re-center it once it has its real size
        bord.SizeChanged += (_, _) =>
        {
            c.Width = bord.Bounds.Width;
            c.Height = bord.Bounds.Height;
            if (areaW > 0.0) Canvas.SetLeft(c, Math.Max(0.0, (areaW - bord.Bounds.Width) / 2.0));
            if (areaH > 0.0) Canvas.SetTop(c, Math.Max(0.0, (areaH - bord.Bounds.Height) / 2.0));
        };
        await CustomComboBoxes.DoModal(cm, wh, cx, cy, c);
        return result;
    }

    // a confirmation box in the same style as the wood sword cave menu: the message, then one wide button per choice; returns the chosen label, or null if dismissed
    public static async Task<string> MessageChoiceBoxAsync(CustomComboBoxes.CanvasManager cm, string message, IEnumerable<string> labels)
    {
        var wh = new ManualResetEvent(false);
        string result = null;
        var col = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black, Margin = new Thickness(string.IsNullOrEmpty(message) ? 8.0 : 16.0) };   // (an empty message gives just the buttons in a tight box)
        if (!string.IsNullOrEmpty(message))
            col.Children.Add(new TextBlock { Text = message, Foreground = Brushes.Orange, FontSize = 20.0, TextWrapping = TextWrapping.Wrap, MaxWidth = 480.0, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0.0, 0.0, 0.0, 8.0) });
        foreach (var label in labels)
        {
            var b = new Button { Height = 34.0, MinWidth = 280.0, Margin = new Thickness(0.0, 4.0, 0.0, 4.0), Focusable = false, Background = Graphics.almostBlack, BorderBrush = Brushes.Orange };
            b.Content = new TextBox { Text = label, IsReadOnly = true, IsHitTestVisible = false, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(0.0), FontSize = 18.0, Foreground = Brushes.Orange, Background = Graphics.almostBlack };
            b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            b.VerticalContentAlignment = VerticalAlignment.Stretch;
            b.Click += (_, _) => { result = label; wh.Set(); };
            col.Children.Add(b);
        }
        var bord = new Border { Child = col, Background = Brushes.Black, BorderThickness = new Thickness(5.0), BorderBrush = Brushes.Gray };
        bord.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var c = new Canvas { Width = bord.DesiredSize.Width, Height = bord.DesiredSize.Height };
        c.Children.Add(bord);
        var (areaW, areaH) = (cm.AppMainCanvas.Width, cm.AppMainCanvas.Height);
        var cx = areaW > 0.0 ? Math.Max(0.0, (areaW - c.Width) / 2.0) : 150.0;
        var cy = areaH > 0.0 ? Math.Max(0.0, (areaH - c.Height) / 2.0) : 200.0;
        // measured before it was in the window, so re-center it once it has its real size
        bord.SizeChanged += (_, _) =>
        {
            c.Width = bord.Bounds.Width;
            c.Height = bord.Bounds.Height;
            if (areaW > 0.0) Canvas.SetLeft(c, Math.Max(0.0, (areaW - bord.Bounds.Width) / 2.0));
            if (areaH > 0.0) Canvas.SetTop(c, Math.Max(0.0, (areaH - bord.Bounds.Height) / 2.0));
        };
        await CustomComboBoxes.DoModal(cm, wh, cx, cy, c);
        return result;
    }

    // a small centered box with a title and a row of icon/text buttons; returns the key of the one clicked (null if dismissed)
    public static async Task<string> IconChoiceBoxAsync(CustomComboBoxes.CanvasManager cm, string title, IEnumerable<(string, Control)> items)
    {
        var wh = new ManualResetEvent(false);
        string result = null;
        var col = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black, Margin = new Thickness(0.0) };
        if (title != "") col.Children.Add(new TextBlock { Text = title, Foreground = Brushes.Orange, FontSize = 20.0, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0.0, 8.0, 0.0, 0.0) });
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var (key, content) in items)
        {
            var b = new Button { MinWidth = 80.0, MinHeight = 60.0, Margin = new Thickness(6.0), Padding = new Thickness(8.0), Focusable = false, Background = Graphics.almostBlack, BorderBrush = Brushes.Orange, Content = content };
            b.Click += (_, _) => { result = key; wh.Set(); };
            row.Children.Add(b);
        }
        col.Children.Add(row);
        var bord = new Border { Child = col, Background = Brushes.Black, BorderThickness = new Thickness(5.0), BorderBrush = Brushes.Gray };
        bord.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var c = new Canvas { Width = bord.DesiredSize.Width, Height = bord.DesiredSize.Height };
        c.Children.Add(bord);
        var (areaW, areaH) = (cm.AppMainCanvas.Width, cm.AppMainCanvas.Height);
        var cx = areaW > 0.0 ? Math.Max(0.0, (areaW - c.Width) / 2.0) : 150.0;
        var cy = areaH > 0.0 ? Math.Max(0.0, (areaH - c.Height) / 2.0) : 200.0;
        // measured before it was in the window, so re-center it once it has its real size
        bord.SizeChanged += (_, _) =>
        {
            c.Width = bord.Bounds.Width;
            c.Height = bord.Bounds.Height;
            if (areaW > 0.0) Canvas.SetLeft(c, Math.Max(0.0, (areaW - bord.Bounds.Width) / 2.0));
            if (areaH > 0.0) Canvas.SetTop(c, Math.Max(0.0, (areaH - bord.Bounds.Height) / 2.0));
        };
        await CustomComboBoxes.DoModal(cm, wh, cx, cy, c);
        return result;
    }
}
