// Ported from Z1R_WPF/DungeonPopups.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
// Every F# `async { }` here is an `async Task` method, as in CustomComboBoxes.cs; callers `await` them where WPF used `let!`/`do!`.
// The detail popups return DoModalGridSelect's `(hasResult, result)` for WPF's `'T option`.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Z1R_Tracker.Models.Z1R_TrackerInterop;

namespace Z1R_Avalonia;

public static class DungeonPopups
{
    public static void canvasAdd(Canvas c, Control item, double left, double top) => Graphics.canvasAdd(c, item, left, top);

    public static readonly double sunglassesOpacity = 0.85;

    public static double THE_DIFF = 0.0;   // a kludge, until/unless I come up with a better way to thread the layout thru global popup logic

    public static bool suppressRoomSyncTemporarily = false;

    // (F#: `let dungeonRoomExplainer, setOpacity = ...`; as Graphics.cs does for tuple lets, one private helper field holds the pair)
    private static (Control, Action<double>) make_dungeonRoomExplainer()
    {
        TextBox mkTxt(string txt) => new TextBox { Text = txt, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false,
                                        BorderThickness = new Thickness(0.0), FontSize = 16.0, VerticalAlignment = VerticalAlignment.Center };
        Border border(Control fe) => new Border { Child = fe, BorderThickness = new Thickness(1.0), BorderBrush = Brushes.Gray };
        Control marginAndSunglasses(Control c) { c.Margin = new Thickness(6.0, 0.0, 0.0, 0.0); c.Opacity = sunglassesOpacity; return c; }
        TextBox left(TextBox tb)
        {
            tb.HorizontalContentAlignment = HorizontalAlignment.Left;
            tb.HorizontalAlignment = HorizontalAlignment.Left;
            tb.Margin = new Thickness(3.0, 0.0, 0.0, 0.0);
            return tb;
        }
        TextBox right(TextBox tb)
        {
            tb.HorizontalContentAlignment = HorizontalAlignment.Right;
            tb.HorizontalAlignment = HorizontalAlignment.Right;
            tb.Margin = new Thickness(0.0, 0.0, 3.0, 0.0);
            return tb;
        }
        var bg = Graphics.iconExtras_bmp.GetPixel(12, 12);
        var ellipsis = Graphics.transformColor(Graphics.iconExtras_bmp, c => c.ToArgb() == bg.ToArgb() ? System.Drawing.Color.Black : c);

        var dp = new DockPanel();
        var header = mkTxt("UI reminder: When mousing a room on the dungeon map...");
        header.HorizontalContentAlignment = HorizontalAlignment.Center;
        DockPanel.SetDock(header, Dock.Top);
        dp.Children.Add(header);
        var g = CustomComboBoxes.makeGrid(3, 4, 250, 33);
        g.ColumnDefinitions[0].Width = new GridLength(218.0);
        g.ColumnDefinitions[1].Width = new GridLength(378.0);
        g.ColumnDefinitions[2].Width = new GridLength(172.0);

        TextBox tb;
        StackPanel sp;
        DungeonRoomState drs;

        tb = right(mkTxt("Left-click"));
        CustomComboBoxes.gridAdd(g, border(tb), 0, 0);
        tb = left(mkTxt("Toggle room brightness (or rotate lobby arrow)"));
        CustomComboBoxes.gridAdd(g, border(tb), 1, 0);
        sp = new StackPanel { Orientation = Orientation.Horizontal };
        drs = new DungeonRoomState();
        drs.RoomType = RoomType.MaybePushBlock;
        drs.IsComplete = true;
        sp.Children.Add(marginAndSunglasses(drs.CurrentDisplay()));
        drs = new DungeonRoomState();
        drs.RoomType = RoomType.MaybePushBlock;
        drs.IsComplete = false;
        sp.Children.Add(marginAndSunglasses(drs.CurrentDisplay()));
        CustomComboBoxes.gridAdd(g, border(sp), 2, 0);

        tb = right(mkTxt("Right-click"));
        CustomComboBoxes.gridAdd(g, border(tb), 0, 1);
        tb = left(mkTxt("Invoke the 'Select a Room Type' popup menu"));
        CustomComboBoxes.gridAdd(g, border(tb), 1, 1);
        sp = new StackPanel { Orientation = Orientation.Horizontal };
        drs = new DungeonRoomState();
        drs.RoomType = RoomType.MaybePushBlock;
        drs.IsComplete = false;
        sp.Children.Add(marginAndSunglasses(drs.CurrentDisplay()));
        drs = new DungeonRoomState();
        drs.RoomType = RoomType.Chevy;
        drs.IsComplete = false;
        sp.Children.Add(marginAndSunglasses(drs.CurrentDisplay()));
        drs = new DungeonRoomState();
        drs.RoomType = RoomType.Transport1;
        drs.IsComplete = false;
        sp.Children.Add(marginAndSunglasses(drs.CurrentDisplay()));
        sp.Children.Add(Graphics.BMPtoImage(ellipsis));
        CustomComboBoxes.gridAdd(g, border(sp), 2, 1);

        tb = right(mkTxt("Scroll-up/shift-left-click"));
        CustomComboBoxes.gridAdd(g, border(tb), 0, 2);
        tb = left(mkTxt("Invoke the 'Select a Monster Detail' popup menu"));
        CustomComboBoxes.gridAdd(g, border(tb), 1, 2);
        sp = new StackPanel { Orientation = Orientation.Horizontal };
        drs = new DungeonRoomState();
        drs.RoomType = RoomType.MaybePushBlock;
        drs.IsComplete = false;
        sp.Children.Add(marginAndSunglasses(drs.CurrentDisplay()));
        drs = new DungeonRoomState();
        drs.RoomType = RoomType.MaybePushBlock;
        drs.IsComplete = false;
        drs.MonsterDetail = MonsterDetail.Gleeok;
        sp.Children.Add(marginAndSunglasses(drs.CurrentDisplay()));
        drs = new DungeonRoomState();
        drs.RoomType = RoomType.MaybePushBlock;
        drs.IsComplete = false;
        drs.MonsterDetail = MonsterDetail.Digdogger;
        sp.Children.Add(marginAndSunglasses(drs.CurrentDisplay()));
        sp.Children.Add(Graphics.BMPtoImage(ellipsis));
        CustomComboBoxes.gridAdd(g, border(sp), 2, 2);

        tb = right(mkTxt("Scroll-down/shift-right-click"));
        CustomComboBoxes.gridAdd(g, border(tb), 0, 3);
        tb = left(mkTxt("Invoke the 'Select a Floor Drop Detail' popup menu"));
        CustomComboBoxes.gridAdd(g, border(tb), 1, 3);
        sp = new StackPanel { Orientation = Orientation.Horizontal };
        drs = new DungeonRoomState();
        drs.RoomType = RoomType.MaybePushBlock;
        drs.IsComplete = false;
        sp.Children.Add(marginAndSunglasses(drs.CurrentDisplay()));
        drs = new DungeonRoomState();
        drs.RoomType = RoomType.MaybePushBlock;
        drs.IsComplete = false;
        drs.FloorDropDetail = FloorDropDetail.Triforce;
        sp.Children.Add(marginAndSunglasses(drs.CurrentDisplay()));
        drs = new DungeonRoomState();
        drs.RoomType = RoomType.MaybePushBlock;
        drs.IsComplete = false;
        drs.FloorDropDetail = FloorDropDetail.BombPack;
        sp.Children.Add(marginAndSunglasses(drs.CurrentDisplay()));
        sp.Children.Add(Graphics.BMPtoImage(ellipsis));
        CustomComboBoxes.gridAdd(g, border(sp), 2, 3);

        dp.Children.Add(g);
        return ((Control)new Border { Child = dp, BorderThickness = new Thickness(0.0, 3.0, 0.0, 3.0), BorderBrush = Brushes.DimGray, Background = Brushes.Black },
            op => dp.Opacity = op);
    }
    private static readonly (Control, Action<double>) _dungeonRoomExplainer = make_dungeonRoomExplainer();
    public static readonly Control dungeonRoomExplainer = _dungeonRoomExplainer.Item1;
    public static readonly Action<double> setOpacity = _dungeonRoomExplainer.Item2;

    // (returns (hasResult, result) for WPF's `MonsterDetail option`)
    public static async Task<(bool hasResult, MonsterDetail result)> DoMonsterDetailPopup(CustomComboBoxes.CanvasManager cm, double boxX, double boxY, MonsterDetail currentMonsterDetail)
    {
        var innerc = new Canvas { Width = 18.0, Height = 18.0, Background = Brushes.Black };  // just has monster icon drawn on it, not the box
        Graphics.SilentlyWarpMouseCursorTo(new Point(boxX + 12.0, boxY + 12.0));
        Canvas redraw(MonsterDetail md)
        {
            innerc.Children.Clear();
            var bmp = md.Bmp();
            if (bmp != null)
            {
                var image = Graphics.BMPtoImage(bmp);
                image.Opacity = sunglassesOpacity;
                canvasAdd(innerc, image, 0.0, 0.0);
            }
            return innerc;
        }
        redraw(currentMonsterDetail);
        var all = MonsterDetailExtensions.DisplayOrder();
        var gridElementsSelectablesAndIDsList = new List<(Control, bool, MonsterDetail)>();
        foreach (var detail in all)
        {


            Control fe =
                detail.IsNotMarked() ? new Canvas()
                : Graphics.BMPtoImage(detail.Bmp());


            gridElementsSelectablesAndIDsList.Add((fe, true, detail));
        }
        var gridElementsSelectablesAndIDs = gridElementsSelectablesAndIDsList.ToArray();

        var originalStateIndex = Array.IndexOf(all, currentMonsterDetail) is var idx && idx >= 0 ? idx : 0;

        CustomComboBoxes.PopupClickBehavior<MonsterDetail> onClick(PointerPressedEventArgs _ea, MonsterDetail ident) =>
            // we're getting a click with mouse event args ea on one of the selectable items in the grid, namely ident. take appropriate action.
            new CustomComboBoxes.PopupClickBehavior<MonsterDetail>.DismissPopupWithResult(ident);
        // the grid comes up where the room type menu does: same bottom edge, centered on it (the room menu is 7x5 cells of 39x27, shown 4/3 larger); this one is 8x4 cells of 18x18, shown 2x
        var ST = CustomComboBoxes.borderThickness;
        var roomG = 4.0 / 3.0;
        var (roomW, roomH) = (7.0 * (39.0 + 2.0 * ST) * roomG, 5.0 * (27.0 + 2.0 * ST) * roomG);
        var roomLeft = 555.0 - 117.0 - (roomW - roomW / roomG);
        var roomBottom = (768.0 + 60.0) - THE_DIFF - 78.0;
        var (gridW, gridH) = ((8.0 * (18.0 + 2.0 * ST) + 2.0 * ST) * 2.0, (4.0 * (18.0 + 2.0 * ST) + 2.0 * ST) * 2.0);
        var gridX = roomLeft + (roomW - gridW) / 2.0 + 20.0 - (boxX + 3.0);   // (nudged 20 to the right)
        var gridY = roomBottom - gridH - (boxY + 3.0);
        void redrawTile(MonsterDetail ident)
        {
            // the user has changed the current selection via mousing or scrolling, redraw the preview tile appropriately to display ident
            var innerc = redraw(ident);
            var s = ident.DisplayDescription();
            s = HotKeys.DungeonRoomHotKeyProcessor.AppendHotKeyToDescription(s, new HotKeys.DungeonRoomHotKey.OfMonsterDetail(ident));
            // the monster's name goes under the right part of the grid (the shift-drag tip box goes under the left part)
            var text = new TextBox { Text = s, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0),
                                        FontSize = 16.0, HorizontalContentAlignment = HorizontalAlignment.Center, Width = gridW - 170.0 - 6.0 };
            var textBorder = new Border { BorderThickness = new Thickness(3.0), Child = text, Background = Brushes.Black, BorderBrush = Brushes.Gray };
            var (rx, ry, rw, rh) = CustomComboBoxes.MovableGridRect;   // (the grid can be moved and resized)
            var hidden = TrackerModelOptions.HidePopupHintAndLock.Value;   // with the tip box and lock bar hidden, the name goes across the whole bottom
            text.Width = Math.Max(80.0, hidden ? rw - 6.0 : rw - 150.0 - 6.0);
            var dp = new DockPanel { LastChildFill = false };
            dp.Children.Add(textBorder);
            innerc.Children.Add(dp);
            Canvas.SetTop(dp, ry + rh + 8.0);
            Canvas.SetLeft(dp, hidden ? rx : rx + 150.0);   // right-aligned under the grid, the tip box takes the left part
        }
        var extraDecorations =
            new List<(Control, double, double)>
            {
                (dungeonRoomExplainer, -3.0 - boxX, 342.0 - boxY - THE_DIFF)
            };
        setOpacity(0.8);


        return await CustomComboBoxes.DoModalGridSelect<MonsterDetail, MonsterDetail>(cm, boxX + 3.0, boxY + 3.0, innerc, gridElementsSelectablesAndIDs, originalStateIndex, 0, (8, 4, 18, 18), 9.0, 9.0, gridX, gridY,
                                redrawTile, onClick, extraDecorations, CustomComboBoxes.itemBoxModalGridSelectBrushes, new CustomComboBoxes.GridClickDismissalWarpReturn.WarpToCenter(), null, "MonsterDetail", null);
    }

    // (returns (hasResult, result) for WPF's `FloorDropDetail option`)
    public static async Task<(bool hasResult, FloorDropDetail result)> DoFloorDropDetailPopup(CustomComboBoxes.CanvasManager cm, double boxX, double boxY, FloorDropDetail currentFloorDropDetail)
    {
        var innerc = new Canvas { Width = 18.0, Height = 18.0, Background = Brushes.Black };  // just has floordrop icon drawn on it, not the box
        Graphics.SilentlyWarpMouseCursorTo(new Point(boxX + 9.0, boxY + 9.0));
        Canvas redraw(FloorDropDetail fd)
        {
            innerc.Children.Clear();
            var bmp = fd.Bmp();
            if (bmp != null)
            {
                var image = Graphics.BMPtoImage(bmp);
                image.Opacity = sunglassesOpacity;
                canvasAdd(innerc, image, 0.0, 0.0);
            }
            return innerc;
        }
        redraw(currentFloorDropDetail);
        var all = FloorDropDetailExtensions.DisplayOrder();
        (Control, bool, FloorDropDetail)[] make_gridElementsSelectablesAndIDs()
        {
            var temp =
                FloorDropDetailExtensions.DisplayOrder()
                .Select(detail =>
                {
                    Control fe =
                        detail.IsNotMarked() ? new Canvas()
                        : Graphics.BMPtoImage(detail.Bmp());
                    return (fe, true, detail);
                })
                .ToArray();
            // Force alignment to a 3x3 grid if needed
            if (temp.Length < 9)
                return temp.Concat(Enumerable.Range(1, 9 - temp.Length).Select(_ => ((Control)null, false, FloorDropDetail.Unmarked))).ToArray();
            else return temp;
        }
        var gridElementsSelectablesAndIDs = make_gridElementsSelectablesAndIDs();


        var originalStateIndex = Array.IndexOf(all, currentFloorDropDetail);
        CustomComboBoxes.PopupClickBehavior<FloorDropDetail> onClick(PointerPressedEventArgs _ea, FloorDropDetail ident) =>
            // we're getting a click with mouse event args ea on one of the selectable items in the grid, namely ident. take appropriate action.
            new CustomComboBoxes.PopupClickBehavior<FloorDropDetail>.DismissPopupWithResult(ident);
        var (gridX, gridY) = (21.0, -3.0);
        void redrawTile(FloorDropDetail ident)
        {
            // the user has changed the current selection via mousing or scrolling, redraw the preview tile appropriately to display ident
            var innerc = redraw(ident);
            var s = ident.DisplayDescription();
            s = HotKeys.DungeonRoomHotKeyProcessor.AppendHotKeyToDescription(s, new HotKeys.DungeonRoomHotKey.OfFloorDropDetail(ident));
            var (rx, ry, rw, rh) = CustomComboBoxes.MovableGridRect;   // (the grid can be moved and resized)
            // the name goes right under the grid (the shift-drag tip box goes under that)
            var text = new TextBox { Text = s, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0),
                                        FontSize = 16.0, HorizontalContentAlignment = HorizontalAlignment.Center, Width = Math.Max(80.0, rw - 6.0) };
            var textBorder = new Border { BorderThickness = new Thickness(3.0), Child = text, Background = Brushes.Black, BorderBrush = Brushes.Gray };
            var dp = new DockPanel { LastChildFill = false };
            dp.Children.Add(textBorder);
            innerc.Children.Add(dp);
            Canvas.SetTop(dp, ry + rh + 8.0);
            Canvas.SetLeft(dp, rx);
        }
        var extraDecorations =
            new List<(Control, double, double)>
            {
                (dungeonRoomExplainer, -3.0 - boxX, 342.0 - boxY - THE_DIFF)
            };
        setOpacity(0.8);
        return await CustomComboBoxes.DoModalGridSelect<FloorDropDetail, FloorDropDetail>(cm, boxX + 3.0, boxY + 3.0, innerc, gridElementsSelectablesAndIDs, originalStateIndex, 0, (3, 3, 18, 18), 9.0, 9.0, gridX, gridY,
                                redrawTile, onClick, extraDecorations, CustomComboBoxes.itemBoxModalGridSelectBrushes, new CustomComboBoxes.GridClickDismissalWarpReturn.WarpToCenter(), null, "FloorDropDetail", null);
    }

    public static readonly CustomComboBoxes.ModalGridSelectBrushes brushes = (new CustomComboBoxes.ModalGridSelectBrushes(Brushes.Lime, Brushes.Lime, Brushes.Red, Brushes.Gray)).Dim(0.6);
    public static async Task DoDungeonRoomSelectPopup(CustomComboBoxes.CanvasManager cm, DungeonRoomState originalRoomState, int[] usedTransports, Action<DungeonRoomState> setNewValue, bool positionAtEntranceRoomIcons,
                                                        CustomComboBoxes.GridClickDismissalWarpReturn gridClickDismissalWarpReturn)
    {
        Image tweak(Image im) { im.Opacity = 0.65; return im; }

        var popupCanvas = cm.CreatePopup(0.75);
        var workingCopy = originalRoomState.Clone();
        workingCopy.CSharp.IsPreview = true; // for rendering only

        TextBox makeCaption(string txt, bool centered)
        {
            var tb = new TextBox { Text = txt, FontSize = 16.0, Foreground = Brushes.Orange, Background = Brushes.Black, BorderThickness = new Thickness(0.0), IsReadOnly = true, IsHitTestVisible = false,
                                    VerticalAlignment = VerticalAlignment.Center };
            if (centered) tb.TextAlignment = TextAlignment.Center;
            return tb;
        }

        var (tileX, tileY) = (555.0, 768.0 + 60.0);   // (moved down 60 now that the room preview is gone: low enough to clear the UI-reminder overlay at the top, without a big gap above the menu)
        var ST = CustomComboBoxes.borderThickness;
        var tileCanvas = new Canvas { Width = 1.0, Height = 1.0 };   // (no room preview is shown any more; this just hosts the captions)
        var G = 4.0 / 3.0;   // the room type menu is shown 33% larger

        var grid =
            new[] {
            RoomType.DoubleMoat, RoomType.CircleMoat, RoomType.LifeOrMoney, RoomType.BombUpgrade, RoomType.HungryGoriyaMeatBlock, RoomType.StartEnterFromE, RoomType.Gannon,
            RoomType.TopMoat, RoomType.Chevy, RoomType.OldManHint, RoomType.VChute, RoomType.HChute, RoomType.StartEnterFromN, RoomType.Zelda,
            RoomType.RightMoat, RoomType.Unmarked, RoomType.MaybePushBlock, RoomType.NonDescript, RoomType.Tee, RoomType.StartEnterFromS, RoomType.LavaMoat,
            RoomType.ItemBasement, RoomType.StaircaseToUnknown, RoomType.Transport1, RoomType.Transport2, RoomType.Transport3, RoomType.StartEnterFromW, RoomType.OffTheMap,
            RoomType.Transport4, RoomType.Transport5, RoomType.Transport6, RoomType.Transport7, RoomType.Transport8, RoomType.Turnstile, RoomType.PrincessRoom,
            };

        (Control, bool, RoomType)[] gridElementsSelectablesAndIDs =
            grid.Select((rt, i) =>
            {
                var isLegal =
                    (rt == originalRoomState.RoomType) ||
                    (rt.KnownTransportNumber() switch
                     {
                         null => true,
                         int n => usedTransports[n] != 2
                     });

                if (rt == RoomType.OldManHint) { var im = Graphics.BMPtoImage(rt.UncompletedBI()); im.Opacity = 1.0; return ((Control)im, isLegal, rt); }   // this icon is naturally dark, so show it undimmed
                else return ((Control)tweak(Graphics.BMPtoImage(rt.UncompletedBI())), isLegal, rt);
            }).ToArray();

        var originalStateIndex =
            Array.FindIndex(grid, x => x == originalRoomState.RoomType) is var k0 && k0 >= 0 ? k0
            : Array.FindIndex(grid, x => x == RoomType.Unmarked);   // (F#: Array.findIndex, which throws if missing; Unmarked is always in the grid)


        var activationDelta = 0;
        var (gnc, gnr, gcw, grh) = (7, 5, 13 * 3, 9 * 3);
        var totalGridWidth = (double)gnc * ((double)gcw + 2.0 * ST) * G;
        var totalGridHeight = (double)gnr * ((double)grh + 2.0 * ST) * G;
        var (gx, gy) = (-117.0 - (totalGridWidth - totalGridWidth / G), -78.0 - totalGridHeight);

        var fullRoom = originalRoomState.Clone();
        fullRoom.IsComplete = false;

        void redrawTile(RoomType curState)
        {
            tileCanvas.Children.Clear();
            // the caption frame follows the (movable, resizable) grid
            var (mx, my, mw, mh) = CustomComboBoxes.MovableGridRect;
            var haveRect = mw > 0.0;
            var textWidth = haveRect ? mw + 13.0 : totalGridWidth + 13.0;
            var gridH = haveRect ? mh : totalGridHeight;
            var topText = Graphics.center(makeCaption("Select a room type", true), (int)textWidth, 24);
            var s = HotKeys.DungeonRoomHotKeyProcessor.AppendHotKeyToDescription(Z1R_Tracker.Models.Z1R_TrackerInterop.RoomTypeExtensions.DisplayDescription(curState), new HotKeys.DungeonRoomHotKey.OfRoomType(curState));
            var bottomText = Graphics.center(makeCaption(s, true), (int)textWidth, 49);

            var dp = new DockPanel { Width = textWidth, Height = gridH + 75.0, LastChildFill = false };
            DockPanel.SetDock(topText, Dock.Top);
            DockPanel.SetDock(bottomText, Dock.Bottom);
            dp.Children.Add(topText);
            dp.Children.Add(bottomText);
            var frame = new Border { BorderBrush = Brushes.DimGray, BorderThickness = new Thickness(3.0), Background = Brushes.Black, Child = dp };
            var sp = new StackPanel { Orientation = Orientation.Vertical };
            sp.Children.Add(frame);
            if (haveRect) canvasAdd(tileCanvas, sp, mx + mw / 2.0 - textWidth / 2.0, my + gridH + 51.0 - dp.Height);
            else canvasAdd(tileCanvas, sp, gx + totalGridWidth / 2.0 - textWidth / 2.0, -27.0 - dp.Height);
        }

        // (WPF: `(ea.ChangedButton = Left || ea.ChangedButton = Right) && ea.ButtonState = Pressed`; on a press, Avalonia's PointerUpdateKind names the button that went down)
        CustomComboBoxes.PopupClickBehavior<RoomType> onClick(PointerPressedEventArgs ea, RoomType curState)
        {
            var kind = ea.GetCurrentPoint(null).Properties.PointerUpdateKind;
            if (kind == PointerUpdateKind.LeftButtonPressed || kind == PointerUpdateKind.RightButtonPressed)
                return new CustomComboBoxes.PopupClickBehavior<RoomType>.DismissPopupWithResult(curState);
            else
                return new CustomComboBoxes.PopupClickBehavior<RoomType>.StayPoppedUp();
        }

        var extraDecorations = new (Control, double, double)[] { (dungeonRoomExplainer, -3.0 - tileX, 342.0 - tileY) };
        setOpacity(0.5);

        // where the (possibly moved / resized by the user) grid actually is, so the mouse lands on the right cell
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        double? tryNum(string s) => System.Double.TryParse(s, System.Globalization.NumberStyles.Float, inv, out var v) ? v : (double?)null;
        double make_placeScale()
        {
            if (TrackerModelOptions.MenuLayouts.TryGetValue("DungeonRoomPlacement", out var v) && !(v == null) && v.Length == 3)
                return tryNum(v[2]) is double c ? Math.Max(0.5, Math.Min(4.0, c)) : 1.0;
            else return 1.0;
        }
        (double, double)? make_placeAbs()
        {
            if (TrackerModelOptions.MenuLayouts.TryGetValue("DungeonRoomPlacementAbsV2", out var v) && !(v == null) && v.Length == 2)
            {
                if (tryNum(v[0]) is double a && tryNum(v[1]) is double b)
                {
                    var (appW, appH) = (cm.AppMainCanvas.Width, cm.AppMainCanvas.Height);
                    return ((appW > 0.0 ? Math.Max(0.0, Math.Min(a, appW - 80.0)) : a), (appH > 0.0 ? Math.Max(50.0, Math.Min(b, appH - 60.0)) : b));
                }
                else return null;
            }
            else return null;
        }
        var placeScale = make_placeScale();
        var placeAbs = make_placeAbs();
        Point cellPoint(double cx, double cy)
        {
            if (placeAbs is (double ax, double ay))
                return new Point(ax + G * placeScale * (ST + cx * ((double)gcw + ST * 2.0)), ay + G * placeScale * (ST + cy * ((double)grh + ST * 2.0)));
            else
                return new Point(tileX + gx + cx * ((double)gcw + ST * 2.0) * G * placeScale, tileY + gy - THE_DIFF + cy * ((double)grh + ST * 2.0) * G * placeScale);
        }
        if (positionAtEntranceRoomIcons)
        {
            if (placeAbs == null && placeScale == 1.0)
                Graphics.WarpMouseCursorTo(new Point(tileX + gx + 5.5 * ((double)gcw + ST * 2.0) * G, tileY + gy + totalGridHeight / 2.0));
            else
                Graphics.WarpMouseCursorTo(cellPoint(5.5, (double)gnr / 2.0));
        }
        else
        {
            // the grid may have been rearranged by the user (Shift+drag); find where the current room type is displayed
            int shownIndex;
            if (TrackerModelOptions.MenuLayouts.TryGetValue("DungeonRoom", out var saved) && !(saved == null))
            {
                var k = System.Array.IndexOf(saved, (originalRoomState.RoomType).ToString());
                shownIndex = (k >= 0 && k < gnc * gnr) ? k : originalStateIndex;
            }
            else shownIndex = originalStateIndex;
            var x = shownIndex % gnc;
            var y = shownIndex / gnc;
            Graphics.WarpMouseCursorTo(cellPoint((double)x + 0.5, (double)y + 0.5));
        }

        var r = await CustomComboBoxes.DoModalGridSelect<RoomType, RoomType>(cm, tileX, tileY - THE_DIFF, tileCanvas, gridElementsSelectablesAndIDs, originalStateIndex, activationDelta, (gnc, gnr, gcw, grh),
                       (double)gcw / 2.0, (double)grh / 2.0, gx, gy, redrawTile, onClick, extraDecorations, brushes, gridClickDismissalWarpReturn, null, "DungeonRoom", null);

        switch (r)
        {
            case (false, _): break;
            case (true, var curState):
                workingCopy.CSharp.IsPreview = false;
                workingCopy.RoomType = curState;
                workingCopy.IsComplete = (curState != RoomType.BombUpgrade && curState != RoomType.HungryGoriyaMeatBlock);   // a placed bomb upgrade room starts un-collected (blue dot on the level number); left click it to clear the dot. A placed meat-blocked room starts without its green check; left click it to add the check
                suppressRoomSyncTemporarily = false;
                setNewValue(workingCopy);
                CustomComboBoxes.StartImmediate(Task.Delay(250));   // (F#: async { do! Async.Sleep(250) } |> Async.StartImmediate)


                //originalRoomState.RoomType <- curState
                //originalRoomState.IsComplete <- true
                //setNewValue(originalRoomState)
                break;
        }

        popupCanvas.Children.Clear();
        cm.DismissPopupCanvas(popupCanvas);

    }
}
