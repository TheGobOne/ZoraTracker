// Ported from Z1R_WPF/LinkRouting.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// The F# `open OverworldMapTileCustomization.OW_ITEM_GRID_LOCATIONS` is a `using static`, so `Locate(BOMB_BOX)` reads as in WPF.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using static Z1R_Avalonia.OverworldMapTileCustomization.OW_ITEM_GRID_LOCATIONS;

namespace Z1R_Avalonia;

public static class LinkRouting
{
    public static void canvasAdd(Canvas c, Control item, double left, double top) => Graphics.canvasAdd(c, item, left, top);
    public static readonly double OMTW = OverworldRouteDrawing.OMTW;


    // (F#: a [<RequireQualifiedAccess>] union; an abstract record with one nested record per case, as HotKeys.DungeonRoomHotKey)
    public abstract record RouteDestination
    {
        private RouteDestination() { }
        public sealed record OW_MAP(List<(int, int)> Spots) : RouteDestination;
        public sealed record HINTZONE(TrackerModel.HintZone Zone, bool CouldBeLetterDungeon) : RouteDestination; // bool is couldBeLetterDungeon
        public sealed record UNMARKEDINSTANCEFUNC(Func<int, int, bool> F) : RouteDestination;
    }

    private static Image[] make_linkImages()
    {
        var r = new Image[4];
        for (int n = 0; n < 4; n++)
        {
            var bmp =
                n == 0 ? Graphics.linkFaceForward_bmp
                : n == 1 ? Graphics.linkFaceRight_bmp
                : n == 2 ? Graphics.linkRunRight_bmp
                : Graphics.linkGotTheThing_bmp;
            var linkImage = Graphics.BMPtoImage(bmp);
            linkImage.Width = 30.0;
            linkImage.Height = 30.0;
            linkImage.Stretch = Stretch.UniformToFill;
            r[n] = linkImage;
        }
        return r;
    }
    public static readonly Image[] linkImages = make_linkImages();
    public static Image duplicateLinkImage()
    {
        var linkImage = Graphics.BMPtoImage(Graphics.linkGotTheThing_bmp);
        linkImage.Width = 30.0;
        linkImage.Height = 30.0;
        linkImage.Stretch = Stretch.UniformToFill;
        return linkImage;
    }
    // (blockerQueries: each returns (shopId, (x, y)) or null for None. Returns (stepAnimateLink, linkIcon, currentTargetIcon).)
    public static (Action, Canvas, Canvas) SetupLinkRouting(CustomComboBoxes.CanvasManager cm, Action<RouteDestination> changeCurrentRouteTarget, Action eliminateCurrentRouteTarget, Func<bool> isSpecificRouteTargetActive,
                            List<Func<(int, (double, double))?>> blockerQueries,
                            Action<Canvas, int> updateNumberedTriforceDisplayImpl, Func<bool> isMirrored, System.Drawing.Bitmap sword2bmp, OverworldData.OverworldInstance owInstance, Action<Canvas> redrawWhiteSwordCanvas, Action<Canvas> redrawMagicalSwordCanvas)
    {
        // help the player route to locations
        var linkIcon = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black };
        var linkIconN = 0;
        void setLinkIconImpl(int n, Canvas linkIcon)
        {
            linkIconN = n;
            linkIcon.Children.Clear();
            canvasAdd(linkIcon, linkImages[n], 0.0, 0.0);
        }
        void setLinkIcon(int n) => setLinkIconImpl(n, linkIcon);
        setLinkIcon(0);
        ToolTip.SetTip(linkIcon, "Click me and I'll help route you to a destination!");
        var currentTargetIcon = new Canvas { Width = 30.0, Height = 30.0 };
        void stepAnimateLink()
        {
            // link animation
            if (!(isSpecificRouteTargetActive()))
            {
                currentTargetIcon.Children.Clear();
                if (linkIcon.IsPointerOver)
                    setLinkIcon(3);
                else
                    setLinkIcon(0);
            }
            else
            {
                if (linkIconN == 1)
                    setLinkIcon(2);
                else if (linkIconN == 2)
                    setLinkIcon(1);
            }
        }
        {   // scope for local variable names to not leak out
            async Task activatePopup()
            {
                CustomComboBoxes.GlobalFlag.popupIsActive = true;
                setLinkIcon(3);
                var wholeAppCanvas = new Canvas { Width = 16.0 * OMTW, Height = 1999.0, Background = Brushes.Transparent, IsHitTestVisible = true };  // TODO right height? I guess too big is ok
                var wh = new ManualResetEvent(false);
                void dismissHavingChosenATarget() { setLinkIcon(1); wh.Set(); }

                var fakeSunglassesOverTopThird = new Canvas { Width = 16.0 * OMTW, Height = 150.0, Background = Brushes.Black, Opacity = 0.50 };
                canvasAdd(wholeAppCanvas, fakeSunglassesOverTopThird, 0.0, 0.0);
                var fakeSunglassesOverBottomThird = new Canvas { Width = 16.0 * OMTW, Height = 1999.0, Background = Brushes.Black, Opacity = 0.50 };
                canvasAdd(wholeAppCanvas, fakeSunglassesOverBottomThird, 0.0, 150.0 + 8.0 * 11.0 * 3.0);
                var explanation =
                    new TextBox { Background = Brushes.Black, Foreground = Brushes.Orange, FontSize = 16.0,
                                  Text = "--Temporarily show routing only to a specific destination--\n" +
                                          "Choose a route destination:\n" +
                                          " - click an overworld map tile to route to that tile\n" +
                                          " - click a highlighted shop icon or blocker to route to any\n     shops you've marked with that item\n" +
                                          " - click a highlighted triforce to route to that dungeon if\n     location known or hinted\n" +
                                          " - click highlighted white/magical sword to route to that\n      cave, if location known or hinted\n" +
                                          " - click highlighted open-cave icon to route to all\n     unmarked open caves\n" +
                                          " - click anywhere else to cancel temporary routing\n" +
                                          "Link (next to the Zora logo) will 'chase' the target icon while this is active" };
                                          // TODO item progress, route to all burnables/powerbraceletables/etc?
                var b = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(3.0), Child = explanation, Width = 444.0, Height = 290.0, IsHitTestVisible = false };
                canvasAdd(wholeAppCanvas, b, 0.0, 150.0 + 8.0 * 11.0 * 3.0);

                // bright, clickable targets
                // (no duplicate Link up in the old upper-right spot anymore; the real Link icon next to the logo animates while routing is active)
                void makeIconTargetImpl(double w, double h, Action<Canvas> draw, Action<Canvas> drawLinkTarget, (double x, double y) xy, RouteDestination routeDest)
                {
                    var (x, y) = xy;
                    var c = new Canvas { Width = w, Height = h, Background = Brushes.Black };
                    draw(c);
                    canvasAdd(wholeAppCanvas, c, x, y);
                    var borderRect = new Rectangle { Width = w, Height = h, Stroke = Brushes.White, StrokeThickness = 1.0 };
                    canvasAdd(c, borderRect, 0.0, 0.0);
                    c.PointerPressed += (_, ea) =>
                    {
                        ea.Handled = true;  // so it doesn't bubble up to wholeAppCanvas, which would treat it as an outside-region click and eliminate-target-and-dismiss
                        changeCurrentRouteTarget(routeDest);
                        currentTargetIcon.Children.Clear();
                        drawLinkTarget(currentTargetIcon);
                        dismissHavingChosenATarget();
                    };
                }
                void makeIconTarget(Action<Canvas> draw, double x, double y, RouteDestination routeDest) => makeIconTargetImpl(30.0, 30.0, draw, draw, (x, y), routeDest);
                { // makePotionIconTarget
                    var spots = new List<(int, int)>();
                    for (int i = 0; i <= 15; i++)
                        for (int j = 0; j <= 7; j++)
                        {
                            var cur = TrackerModel.overworldMapMarks[i, j].Current();
                            if (cur == TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP ||
                                TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]) == 5 ||   // (a tile labeled "P SHOP" in the Shop flyout is a potion shop too)
                                (cur == TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY && TrackerModel.getOverworldMapExtraData(i, j, cur) != cur))
                                spots.Add((i, j));
                        }
                    if (spots.Count > 0)
                        makeIconTarget((c => canvasAdd(c, Graphics.BMPtoImage(Graphics.theInteriorBmpTable[TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP][0]), 4.0, 4.0)),
                                                                330.0, 120.0, new RouteDestination.OW_MAP(spots));
                }
                // the marker icon that the shop hotkeys and the tile menu place for a shop item (shops are now marked with these icons, often on a DARK_X tile)
                int shopMarkerIdent(int shopDest) =>
                    shopDest == TrackerModel.MapSquareChoiceDomainHelper.ARROW ? TrackerModel.ITEMS.WOODARROW
                    : shopDest == TrackerModel.MapSquareChoiceDomainHelper.BOMB ? TrackerModel.DECOR_BOMB
                    : shopDest == TrackerModel.MapSquareChoiceDomainHelper.BOOK ? TrackerModel.ITEMS.BOOKSHIELD
                    : shopDest == TrackerModel.MapSquareChoiceDomainHelper.BLUE_CANDLE ? TrackerModel.ITEMS.BLUECANDLE
                    : shopDest == TrackerModel.MapSquareChoiceDomainHelper.BLUE_RING ? TrackerModel.ITEMS.BLUERING
                    : shopDest == TrackerModel.MapSquareChoiceDomainHelper.MEAT ? TrackerModel.ITEMS.MEAT
                    : shopDest == TrackerModel.MapSquareChoiceDomainHelper.KEY ? TrackerModel.ITEMS.OLDKEY
                    : -1;
                bool tileHasMarker(int i, int j, int ident) =>
                    ident != -1 && TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[i, j]).Any(e => e.Item1 == ident);
                void makeShopIconTarget(Action<Canvas> draw, (double x, double y) xy, int shopDest)
                {
                    var (x, y) = xy;
                    var spots = new List<(int, int)>();
                    var ident = shopMarkerIdent(shopDest);
                    // numbered shops: every tile with the same shop number is the same shop, so if one of them holds the item, they all do
                    var labelsWithItem = new HashSet<int>();
                    for (int i = 0; i <= 15; i++)
                        for (int j = 0; j <= 7; j++)
                        {
                            var label = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]);
                            if (label > 0 && tileHasMarker(i, j, ident)) labelsWithItem.Add(label);
                        }
                    for (int i = 0; i <= 15; i++)
                        for (int j = 0; j <= 7; j++)
                        {
                            var cur = TrackerModel.overworldMapMarks[i, j].Current();
                            var oldStyleShop = new OverworldMapTileCustomization.MapStateProxy(cur).IsThreeItemShop &&
                                    (cur == shopDest || TrackerModel.getShopItem2_1based(i, j) == TrackerModel.MapSquareChoiceDomainHelper.ToItem(shopDest) || TrackerModel.getShopItem3_1based(i, j) == TrackerModel.MapSquareChoiceDomainHelper.ToItem(shopDest));
                            var label = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]);
                            if (oldStyleShop || tileHasMarker(i, j, ident) || (label > 0 && labelsWithItem.Contains(label)))
                                spots.Add((i, j));
                        }
                    if (spots.Count > 0)
                        makeIconTarget(draw, x, y, new RouteDestination.OW_MAP(spots));
                }
                // shops
                makeShopIconTarget((c => canvasAdd(c, Graphics.BMPtoImage(Graphics.bomb_bmp), 4.0, 4.0)), Locate(BOMB_BOX), TrackerModel.MapSquareChoiceDomainHelper.BOMB);
                makeShopIconTarget((c => canvasAdd(c, Graphics.BMPtoImage(Graphics.boom_book_bmp), 4.0, 4.0)), Locate(BOOMSTICK_BOX), TrackerModel.MapSquareChoiceDomainHelper.BOOK);
                makeShopIconTarget((c => canvasAdd(c, Graphics.BMPtoImage(Graphics.blue_ring_bmp), 4.0, 4.0)), Locate(BLUE_RING_BOX), TrackerModel.MapSquareChoiceDomainHelper.BLUE_RING);
                makeShopIconTarget((c => canvasAdd(c, Graphics.BMPtoImage(Graphics.wood_arrow_bmp), 4.0, 4.0)), Locate(WOOD_ARROW_BOX), TrackerModel.MapSquareChoiceDomainHelper.ARROW);
                makeShopIconTarget((c => canvasAdd(c, Graphics.BMPtoImage(Graphics.blue_candle_bmp), 4.0, 4.0)), Locate(BLUE_CANDLE_BOX), TrackerModel.MapSquareChoiceDomainHelper.BLUE_CANDLE);
                foreach (var bq in blockerQueries)
                {
                    var q = bq();
                    if (q is (int shopId, (double x, double y)))
                    {
                        System.Drawing.Bitmap bmp;
                        if (shopId == TrackerModel.MapSquareChoiceDomainHelper.BOMB) bmp = Graphics.bomb_bmp;
                        else if (shopId == TrackerModel.MapSquareChoiceDomainHelper.ARROW) bmp = Graphics.wood_arrow_bmp;
                        else if (shopId == TrackerModel.MapSquareChoiceDomainHelper.KEY) bmp = Graphics.magic_key_bmp;
                        else if (shopId == TrackerModel.MapSquareChoiceDomainHelper.MEAT) bmp = Graphics.bait_bmp;
                        else throw new Exception("unexpected blockerQueries result");
                        makeShopIconTarget((c => canvasAdd(c, Graphics.BMPtoImage(bmp), 4.0, 4.0)), (x, y), shopId);
                    }
                }
                // open caves
                void openCave(Canvas c) => canvasAdd(c, Graphics.BMPtoImage(Graphics.openCaveIconBmp), 0.0, 0.0);
                makeIconTargetImpl(20.0, 20.0, openCave, openCave, (529.0, 120.0), new RouteDestination.UNMARKEDINSTANCEFUNC(TrackerModel.owInstance.Nothingable));   // (the current overworld template, read when the popup opens)
                // triforces
                if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
                {
                    // letters
                    for (int i = 0; i <= 7; i++)
                    {
                        var i_ = i;   // (each closure keeps its own i, as F#'s loop variable)
                        // located, letters
                        var (x, y) = TrackerModel.mapStateSummary.DungeonLocations[i];
                        if (!Tuple.Create(x, y).Equals(TrackerModel.NOTFOUND))
                            makeIconTarget((c => canvasAdd(c, Views.MakeTriforceDisplayView(null, i_, owInstance), 0.0, 0.0)), 0.0 + (double)i * 30.0, 30.0, new RouteDestination.OW_MAP(new List<(int, int)> { (x, y) }));
                        else
                        {
                            // hint letter due to numbered hint
                            var label = TrackerModel.GetDungeon(i).LabelChar;
                            if (label >= '1' && label <= '8')
                            {
                                var index = (int)label - (int)'1';
                                if (!TrackerModel.GetLevelHint.Invoke(index).Equals(TrackerModel.HintZone.UNKNOWN))
                                    makeIconTarget((c => canvasAdd(c, Views.MakeTriforceDisplayView(null, i_, owInstance), 0.0, 0.0)), 0.0 + (double)i * 30.0, 30.0, new RouteDestination.HINTZONE(TrackerModel.GetLevelHint.Invoke(index), true));
                            }
                        }
                    }
                    // numbers
                    for (int n = 0; n <= 7; n++)
                    {
                        var n_ = n;   // (each closure keeps its own n, as F#'s loop variable)
                        var index = -1;
                        for (int i = 0; i <= 7; i++)
                            if (TrackerModel.GetDungeon(i).LabelChar == (char)((int)'1' + n))
                                index = i;
                        // located or hinted numbers
                        void hint()
                        {
                            if (!TrackerModel.GetLevelHint.Invoke(n_).Equals(TrackerModel.HintZone.UNKNOWN))
                                makeIconTarget((c => updateNumberedTriforceDisplayImpl(c, n_)), OFFSET + (double)n_ * 30.0, 0.0, new RouteDestination.HINTZONE(TrackerModel.GetLevelHint.Invoke(n_), true));
                        }
                        if (index != -1)
                        {
                            var loc = TrackerModel.mapStateSummary.DungeonLocations[index];
                            if (!loc.Equals(TrackerModel.NOTFOUND))
                                makeIconTarget((c => updateNumberedTriforceDisplayImpl(c, n_)), OFFSET + (double)n * 30.0, 0.0, new RouteDestination.OW_MAP(new List<(int, int)> { (loc.Item1, loc.Item2) }));
                            else
                                hint();
                        }
                        else
                            hint();
                    }
                }
                else
                {
                    for (int i = 0; i <= 7; i++)
                    {
                        var i_ = i;   // (each closure keeps its own i, as F#'s loop variable)
                        var (x, y) = TrackerModel.mapStateSummary.DungeonLocations[i];
                        if (!Tuple.Create(x, y).Equals(TrackerModel.NOTFOUND))
                            makeIconTarget((c => canvasAdd(c, Views.MakeTriforceDisplayView(null, i_, owInstance), 0.0, 0.0)), 0.0 + (double)i * 30.0, 30.0, new RouteDestination.OW_MAP(new List<(int, int)> { (x, y) }));
                        else if (!TrackerModel.GetLevelHint.Invoke(i).Equals(TrackerModel.HintZone.UNKNOWN))
                            makeIconTarget((c => canvasAdd(c, Views.MakeTriforceDisplayView(null, i_, owInstance), 0.0, 0.0)), 0.0 + (double)i * 30.0, 30.0, new RouteDestination.HINTZONE(TrackerModel.GetLevelHint.Invoke(i), false));
                    }
                }
                {
                    var (x, y) = TrackerModel.mapStateSummary.DungeonLocations[8];
                    if (!Tuple.Create(x, y).Equals(TrackerModel.NOTFOUND))
                        makeIconTarget((c => canvasAdd(c, Views.MakeLevel9View(owInstance), 0.0, 0.0)), 0.0 + 8.0 * 30.0, 30.0, new RouteDestination.OW_MAP(new List<(int, int)> { (x, y) }));
                    else if (!TrackerModel.GetLevelHint.Invoke(8).Equals(TrackerModel.HintZone.UNKNOWN))
                        makeIconTarget((c => canvasAdd(c, Views.MakeLevel9View(owInstance), 0.0, 0.0)), 0.0 + 8.0 * 30.0, 30.0, new RouteDestination.HINTZONE(TrackerModel.GetLevelHint.Invoke(8), false));
                }
                // swords
                if (!TrackerModel.mapStateSummary.Sword2Location.Equals(TrackerModel.NOTFOUND) || !TrackerModel.GetLevelHint.Invoke(9).Equals(TrackerModel.HintZone.UNKNOWN))
                {
                    var loc = TrackerModel.mapStateSummary.Sword2Location;
                    var (x, y) = loc;
                    RouteDestination dest = !loc.Equals(TrackerModel.NOTFOUND) ? new RouteDestination.OW_MAP(new List<(int, int)> { (x, y) }) : new RouteDestination.HINTZONE(TrackerModel.GetLevelHint.Invoke(9), false);
                    makeIconTargetImpl(30.0, 30.0, redrawWhiteSwordCanvas,
                        (c =>
                        {
                            // white sword seems dodgy for link to chase, since it's actually the cave which likely has something else, so draw the map marker instead
                            var image = Graphics.BMPtoImage(sword2bmp);
                            canvasAdd(c, image, 7.0, 1.0);
                        }), Locate(WHITE_SWORD_ICON), dest);
                }
                if (!TrackerModel.mapStateSummary.Sword3Location.Equals(TrackerModel.NOTFOUND) || !TrackerModel.GetLevelHint.Invoke(10).Equals(TrackerModel.HintZone.UNKNOWN))
                {
                    var loc = TrackerModel.mapStateSummary.Sword3Location;
                    var (x, y) = loc;
                    RouteDestination dest = !loc.Equals(TrackerModel.NOTFOUND) ? new RouteDestination.OW_MAP(new List<(int, int)> { (x, y) }) : new RouteDestination.HINTZONE(TrackerModel.GetLevelHint.Invoke(10), false);
                    makeIconTarget(redrawMagicalSwordCanvas, Locate(MAGS_BOX).Item1, Locate(MAGS_BOX).Item2, dest);
                }
                wholeAppCanvas.PointerPressed += (_, ea) =>
                {
                    var pos = ea.GetPosition(wholeAppCanvas);
                    if (pos.Y > 150.0 && pos.Y < 150.0 + 8.0 * 11.0 * 3.0)
                    {
                        // overworld map tile
                        var i0 = (int)(pos.X / OMTW);
                        var j = (int)((pos.Y - 150.0) / (11.0 * 3.0));
                        var i = isMirrored() ? 15 - i0 : i0;
                        changeCurrentRouteTarget(new RouteDestination.OW_MAP(new List<(int, int)> { (i, j) }));
                        // draw crosshairs icon
                        currentTargetIcon.Children.Clear();
                        canvasAdd(currentTargetIcon, new Canvas { Width = 30.0, Height = 30.0, Background = Graphics.overworldCommonestFloorColorBrush }, 0.0, 0.0);
                        var tb = new TextBox { FontSize = 18.0, Foreground = Brushes.Black, Background = Brushes.Transparent, Text = "+", IsHitTestVisible = false, BorderThickness = new Thickness(0.0) };
                        canvasAdd(currentTargetIcon, tb, 7.0, 1.0);
                        var borderRect = new Rectangle { Width = 30.0, Height = 30.0, Stroke = Brushes.White, StrokeThickness = 1.0 };
                        canvasAdd(currentTargetIcon, borderRect, 0.0, 0.0);
                        dismissHavingChosenATarget();
                    }
                    else
                    {
                        // they clicked elsewhere
                        eliminateCurrentRouteTarget();
                        setLinkIcon(0);
                        wh.Set();
                    }
                };
                await CustomComboBoxes.DoModalCore(cm, wh,
                                                    ((c, e) => canvasAdd(c, e, 0.0, 0.0)),
                                                    ((c, e) => c.Children.Remove(e)),
                                                    wholeAppCanvas, 0.01);
                CustomComboBoxes.GlobalFlag.popupIsActive = false;
            }
            linkIcon.PointerPressed += (_, _) =>
            {
                if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                    CustomComboBoxes.StartImmediate(activatePopup());
            };
            linkIcon.PointerEntered += (_, _) => { if (!(isSpecificRouteTargetActive())) setLinkIcon(3); };
            linkIcon.PointerExited += (_, _) => { if (!(isSpecificRouteTargetActive())) setLinkIcon(0); };
        }
        return (stepAnimateLink, linkIcon, currentTargetIcon);
    }
}
