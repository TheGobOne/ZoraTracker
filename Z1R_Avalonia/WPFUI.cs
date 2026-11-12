// Ported from Z1R_WPF/WPFUI.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// The F# `open`s of OverworldMapTileCustomization, DungeonUI.AhhGlobalVariables, OverworldItemGridUI, Reminders, MainTrackerTriforcesAndItems and
// CustomComboBoxes.GlobalFlag are `using static`s. OverworldMapTileCustomization and OverworldItemGridUI both define `canvasAdd`/`OMTW` (F#: the later
// `open` wins), so this class has private `canvasAdd`/`gridAdd`/`makeGrid`/`OMTW` forwarding to OverworldItemGridUI's, which C# prefers over the
// `using static`s. `module OW_ITEM_GRID_LOCATIONS = ...` and `type RouteDestination = ...` are `using` aliases. WPF's UIElement/FrameworkElement are
// Control; mouse events are pointer events, wheel handlers go through Graphics.wheelNotch (see PROGRESS.md); `Visibility.Hidden` is Opacity 0 + not
// hit-testable. F# model events keep their F# type and are subscribed with `onEvent` (an FSharpHandler). C# `for` loops share one variable across
// iterations, so loop indexes that closures capture are copied into a local first (F# gives each iteration its own). `makeAll` (F#: one `async`
// function holding the whole main window) is one `async Task<Action<int>>` method; its nested `let`s are locals and local functions in the same order.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using static Z1R_Avalonia.OverworldMapTileCustomization;
using static Z1R_Avalonia.DungeonUI.AhhGlobalVariables;
using static Z1R_Avalonia.OverworldItemGridUI;
using static Z1R_Avalonia.Reminders;
using static Z1R_Avalonia.MainTrackerTriforcesAndItems;
using static Z1R_Avalonia.CustomComboBoxes.GlobalFlag;
using OW_ITEM_GRID_LOCATIONS = Z1R_Avalonia.OverworldMapTileCustomization.OW_ITEM_GRID_LOCATIONS;
using RouteDestination = Z1R_Avalonia.LinkRouting.RouteDestination;

namespace Z1R_Avalonia;

public static class WPFUI
{
    private static void onEvent<T>(IEvent<FSharpHandler<T>, T> e, Action<T> f) => e.AddHandler(new FSharpHandler<T>((_, x) => f(x)));
    // (port: F#'s later `open OverworldItemGridUI` wins over `open OverworldMapTileCustomization` for these names)
    private static void canvasAdd(Canvas c, Control item, double left, double top) => OverworldItemGridUI.canvasAdd(c, item, left, top);
    private static void gridAdd(Grid g, Control x, int c, int r) => OverworldItemGridUI.gridAdd(g, x, c, r);
    private static Grid makeGrid(int nc, int nr, int cw, int rh) => OverworldItemGridUI.makeGrid(nc, nr, cw, rh);
    private static readonly double OMTW = OverworldItemGridUI.OMTW;

    // Icons for the 4th Sword and Progressive Sword overworld markers, in the same pixel-art style as the Wood/White/Magical sword cave icons:
    // a black glyph on the brown cave floor, with a colored sword underneath (5x9 logical pixels, each drawn 3x3)
    public static System.Drawing.Bitmap extraSwordIconBmp(int item)
    {
        var brown = System.Drawing.Color.FromArgb(185, 122, 87);
        var isProg = (item == TrackerModel.ITEMS.PROGRESSIVESWORD);
        var blade = isProg ? System.Drawing.Color.FromArgb(29, 148, 65) : System.Drawing.Color.FromArgb(128, 128, 255);
        var hilt = isProg ? System.Drawing.Color.FromArgb(136, 0, 21) : System.Drawing.Color.White;
        var glyph = isProg ? new[] { "abbba", "ababa", "abbba", "abaaa", "abaaa" } : new[] { "ababa", "ababa", "abbba", "aaaba", "aaaba" };   // 'P' or '4'
        var rows = new[] { "aaaaa" }.Concat(glyph).Concat(new[] { "aaaXa", "HHHXX", "aaaXa" }).ToArray();
        var bmp = new System.Drawing.Bitmap(5 * 3, 9 * 3);
        for (int y = 0; y <= 8; y++)
        {
            for (int x = 0; x <= 4; x++)
            {
                var c = rows[y][x] switch
                {
                    'b' => System.Drawing.Color.Black,
                    'X' => (isProg || (x + y) % 2 == 0 ? blade : System.Drawing.Color.FromArgb(0, 0, 168)),   // the 4th sword: checkered light / dark blue on the guard side, white on the left
                    'H' => hilt,
                    _ => brown,
                };
                for (int dx = 0; dx <= 2; dx++)
                    for (int dy = 0; dy <= 2; dy++)
                        bmp.SetPixel(x * 3 + dx, y * 3 + dy, c);
            }
        }
        return Graphics.restyleSwordCard(bmp);
    }

    // Decorative marker icon for an overworld tile: the 4th/progressive sword get the cave-style sword icons above; every other dungeon item gets its normal
    // item icon with a black border around it (one art-pixel = 3px wide), so it stays visible on top of the busy overworld map.
    private static readonly Dictionary<int, System.Drawing.Bitmap> decorIconCache = new Dictionary<int, System.Drawing.Bitmap>();
    public static readonly int CLEAR_ICONS = 2000;   // pseudo-id of the red-X menu cell that removes every marker icon from the tile
    public static readonly int ANYROAD_MENU = 2001;   // pseudo-result: an Any Road cell was right-clicked (offers to place all four vanilla any roads)
    public static string decorDescription(int ident) => ident == TrackerModel.DECOR_BOMB ? "Bomb" : ident == TrackerModel.DECOR_BOMB_DROPPER ? "Bomb Dropper" : TrackerModel.ITEMS.AsDisplayDescription(ident);
    // once the player has the magic key, the key marker icons on the map are hidden; likewise wood arrow markers once silver arrows are collected (they come back if deselected)
    public static bool isKeyDecor(int ident) => (ident == TrackerModel.ITEMS.KEY || ident == TrackerModel.ITEMS.OLDKEY);
    public static List<int> visibleDecorIcons(int packed)
    {
        var hideKeys = TrackerModel.playerComputedStateSummary.HaveAnyKey;
        var hideWoodArrows = TrackerModel.playerComputedStateSummary.ArrowLevel >= 2;   // silver arrows collected: wood arrow markers are no longer useful
        // an icon that has been checked off as collected always stays visible (greyscale), even when keys / wood arrows are otherwise hidden
        return TrackerModel.decorEntriesOf(packed)
            .Where(e => e.Item2 || (!(hideKeys && isKeyDecor(e.Item1)) && !(hideWoodArrows && e.Item1 == TrackerModel.ITEMS.WOODARROW)))
            .Select(e => e.Item1).ToList();
    }
    // size and position (within the tile) of marker icon k of n; shared by drawing and by middle-click hit-testing
    // a dungeon entrance or an any road tile only gets the bomb dropper marker (no other marker icons)
    public static bool isDungeonOrAnyRoad(int s) => (s >= 0 && s <= 8) || (s >= TrackerModel.MapSquareChoiceDomainHelper.WARP_1 && s <= TrackerModel.MapSquareChoiceDomainHelper.WARP_4);
    public static readonly int SHOP_MENU = 2002;   // pseudo-id of the 'Shop' cell in the overworld menu (it opens a flyout: potion shop or shop 1-5)
    public static readonly int SHOP_LABEL_BASE = 3000;   // pseudo-results 3001-3005: a numbered shop was picked from the flyout
    private static (double, double, double) decorSlotNoLabel(bool big, int n, int k)
    {
        if (big)
        {
            switch (n)
            {
                case 1: return (27.0, OMTW / 2.0 - 13.5, 3.0);
                case 2: return (24.0, (double)k * 24.0, 4.0);
                default: return (16.0, (double)k * 16.0, 8.0);
            }
        }
        else
        {
            switch (n)
            {
                case 1: return (18.0, OMTW - 20.0, 0.0);
                default: return (14.0, OMTW - 15.0 * (double)n + 15.0 * (double)k, 0.0);
            }
        }
    }
    // a tile with a "SHOP n" text label keeps the top strip of the tile for the label, so its item icons are drawn in the part below it
    public static (double, double, double) decorSlot(bool hasLabel, bool big, int n, int k)
    {
        if (!hasLabel) return decorSlotNoLabel(big, n, k);
        else if (big)
        {
            switch (n)
            {
                case 1: return (21.0, OMTW / 2.0 - 10.5, 11.0);
                case 2: return (21.0, 2.0 + (double)k * 23.0, 11.0);
                default: return (15.0, 1.5 + (double)k * 15.5, 14.0);
            }
        }
        else
        {
            var (size, x, y) = decorSlotNoLabel(big, n, k);
            return (size, x, y + 10.0);
        }
    }
    // like decorSlot, but 'centered' (a bomb shop tile whose bomb is one of the slots) puts the row of icons in the middle of the tile instead of the top right
    public static (double, double, double) decorSlotC(bool hasLabel, bool centered, bool big, int n, int k)
    {
        var (size, x, y) = decorSlot(hasLabel, big, n, k);
        if (centered && !big) return (size, x - (OMTW - 15.0 * (double)n) / 2.0, (33.0 - size) / 2.0); else return (size, x, y);
    }
    // a potion marker is drawn as just the blue potion; any other marker icons on the tile go beside it.
    // Returns (icon to draw, index of the marker it belongs to in the original list)
    public static List<(int, int)> decorDisplayList(List<int> idents)
    {
        var indexed = idents.Select((id, k) => (k, id)).ToList();
        var found = indexed.Where(p => p.id == TrackerModel.ITEMS.BLUEPOTION).Select(p => ((int, int)?)p).FirstOrDefault();
        if (found == null) return indexed.Select(p => (p.id, p.k)).ToList();
        else if (true) return indexed.Select(p => (p.id, p.k)).ToList();   // the blue potion marker is an ordinary icon now
#pragma warning disable CS0162 // (unreachable, as in WPF's `when true`)
        else
        {
            var pk = found.Value.Item1;
            var others = indexed.Where(p => p.k != pk).Select(p => (p.id, p.k));
            return new List<(int, int)> { (TrackerModel.ITEMS.BLUEPOTION, pk) }.Concat(others).ToList();   // (just the blue potion: the red one made the icons too small)
        }
#pragma warning restore CS0162
    }
    private static readonly Dictionary<int, System.Drawing.Bitmap> decorGreyCache = new Dictionary<int, System.Drawing.Bitmap>();
    // the bomb icon with a red 'drops' badge in the corner: a screen whose enemies drop bombs when killed
    private static System.Drawing.Bitmap make_bombDropperBmp()
    {
        var b = new System.Drawing.Bitmap(Graphics.bomb_bmp);
        using var g = System.Drawing.Graphics.FromImage(b);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var r = new System.Drawing.Rectangle(b.Width - 11, b.Height - 11, 10, 10);
        g.FillEllipse(System.Drawing.Brushes.Black, new System.Drawing.Rectangle(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2));
        g.FillEllipse(System.Drawing.Brushes.Red, r);
        using var pen = new System.Drawing.Pen(System.Drawing.Color.White, 1.6f);
        g.DrawLine(pen, r.X + 5, r.Y + 2, r.X + 5, r.Y + 7);
        g.DrawLine(pen, r.X + 3, r.Y + 5, r.X + 5, r.Y + 8);
        g.DrawLine(pen, r.X + 7, r.Y + 5, r.X + 5, r.Y + 8);
        return b;
    }
    public static readonly System.Drawing.Bitmap bombDropperBmp = make_bombDropperBmp();
    // the bomb dropper icon with a 1px red outline hugging its shape (transparent everywhere else): the badge drawn in a tile's corner
    private static System.Drawing.Bitmap make_bombDropperOutlinedBmp()
    {
        var src = bombDropperBmp;
        var o = new System.Drawing.Bitmap(src.Width + 2, src.Height + 2, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for (int x = 0; x <= src.Width - 1; x++)
            for (int y = 0; y <= src.Height - 1; y++)
                if (src.GetPixel(x, y).A > 60)
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            o.SetPixel(x + 1 + dx, y + 1 + dy, System.Drawing.Color.Red);
        for (int x = 0; x <= src.Width - 1; x++)
        {
            for (int y = 0; y <= src.Height - 1; y++)
            {
                var c = src.GetPixel(x, y);
                if (c.A > 0)
                {
                    var a = (int)c.A;
                    int blend(int fg, int bg) => (fg * a + bg * (255 - a)) / 255;
                    o.SetPixel(x + 1, y + 1, System.Drawing.Color.FromArgb(255, blend((int)c.R, 255), blend((int)c.G, 0), blend((int)c.B, 0)));
                }
            }
        }
        return o;
    }
    public static readonly System.Drawing.Bitmap bombDropperOutlinedBmp = make_bombDropperOutlinedBmp();
    // Progressive Items: the picture of a family's tier (swords: the P1..P4 sprites; rings / candles / arrows: the base item, then the upgraded one)
    public static System.Drawing.Bitmap progressiveFamilyTierBmp(int family, int tier)
    {
        if (family == 1) return Graphics.ow_progressive_tier_item_bmps[Math.Max(1, Math.Min(4, tier))];
        else return CustomComboBoxes.boxCurrentBMP(TrackerModel.progressiveFamilyIdent(family, tier), null);
    }
    public static System.Drawing.Bitmap decorSourceBmp(int item)
    {
        if (item == TrackerModel.DECOR_BOMB) return Graphics.bomb_bmp;
        else if (item == TrackerModel.DECOR_BOMB_DROPPER) return bombDropperBmp;
        else if (item == TrackerModel.ITEMS.BLUEPOTION) return (TrackerModelOptions.UseOldPotionLetterIcons.Value ? Graphics.old_blue_potion_bmp : Graphics.blue_potion_bmp);
        else if (item == TrackerModel.ITEMS.REDPOTION) return (TrackerModelOptions.UseOldPotionLetterIcons.Value ? Graphics.old_red_potion_bmp : Graphics.red_potion_bmp);
        else if (TrackerModel.ProgressiveItems && TrackerModel.progressiveSwordBoxTier(item) >= 1 && TrackerModel.progressiveSwordBoxTier(item) <= 5) return Graphics.ow_progressive_tier_item_bmps[TrackerModel.progressiveSwordTier()];   // Progressive Items: swords act like the progressive sword (they all show the current tier, P1..P4)
        else if (TrackerModel.progressiveFamily(item) >= 2) return progressiveFamilyTierBmp(TrackerModel.progressiveFamily(item), TrackerModel.progressiveFamilyTier(TrackerModel.progressiveFamily(item)));   // Progressive Items: rings / candles / arrows show the current tier
        else if (item == TrackerModel.ITEMS.WOODSWORD) return Graphics.swordLevelToOwBmp(1);   // the same sword icon the item progress bar uses
        else if (item == TrackerModel.ITEMS.WHITESWORD) return Graphics.swordLevelToOwBmp(2);
        else if (item == TrackerModel.ITEMS.MAGICALSWORD) return Graphics.swordLevelToOwBmp(3);
        else return CustomComboBoxes.boxCurrentBMP(item, null);
    }
    // (port helper: the tiles carrying numbered shop `label` that hold item icons, in F#'s x-then-y order)
    private static List<(int, int)> shopTilesWithItems(int label)
    {
        var src = new List<(int, int)>();
        for (int x = 0; x <= 15; x++)
            for (int y = 0; y <= 7; y++)
                if (TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[x, y]) == label && !(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[x, y]).IsEmpty))
                    src.Add((x, y));
        return src;
    }
    // the items placed in numbered shop n (taken from the first tile carrying that shop's label), as (icon, collected)
    public static List<(System.Drawing.Bitmap, bool)> shopItemBitmapsGlobal(int n)
    {
        // (index 0 is the P shop, which is shop label 5; it holds just one item)
        var label = n == 0 ? 5 : n;
        var src = shopTilesWithItems(label);
        if (src.Count == 0)
        {
            // no tile of this shop on the map: whatever was entered in the shop window
            var all = new List<(System.Drawing.Bitmap, bool)>();
            foreach (var e in TrackerModel.decorEntriesOf(TrackerModel.shopStash[label]))
            {
                var bmp = decorSourceBmp(e.Item1);
                if (bmp != null) all.Add((bmp, e.Item2));
            }
            return n == 0 ? all.Take(1).ToList() : all;
        }
        else
        {
            var (sx, sy) = src[0];
            var all = new List<(System.Drawing.Bitmap, bool)>();
            foreach (var e in TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[sx, sy]))
            {
                var bmp = decorSourceBmp(e.Item1);
                if (bmp != null) all.Add((bmp, e.Item2));
            }
            return n == 0 ? all.Take(1).ToList() : all;
        }
    }
    // the item idents behind the icons shopItemBitmapsGlobal(n) gives, in the same order (port only: middle clicking an icon in the shop window removes that item)
    public static List<int> shopItemIdentsGlobal(int n)
    {
        var label = n == 0 ? 5 : n;
        var src = shopTilesWithItems(label);
        var packed = src.Count > 0 ? TrackerModel.owSwordIcons[src[0].Item1, src[0].Item2] : TrackerModel.shopStash[label];
        var idents = TrackerModel.decorEntriesOf(packed).Where(e => decorSourceBmp(e.Item1) != null).Select(e => e.Item1).ToList();
        return n == 0 ? idents.Take(1).ToList() : idents;
    }
    // removes the idx-th item (as shopItemBitmapsGlobal(n) lists them) from shop n (port only: middle click in the shop window)
    // (by position, not by toggling the item: heart containers repeat, so adding one again adds another instead of removing it)
    public static void removeShopItemAtGlobal(int n, int idx)
    {
        var label = n == 0 ? 5 : n;
        var src = shopTilesWithItems(label);
        var packed = src.Count > 0 ? TrackerModel.owSwordIcons[src[0].Item1, src[0].Item2] : TrackerModel.shopStash[label];
        var es = TrackerModel.decorEntriesOf(packed).ToList();
        var shown = Enumerable.Range(0, es.Count).Where(k => decorSourceBmp(es[k].Item1) != null).ToList();
        if (idx < 0 || idx >= shown.Count) return;
        var es2 = ListModule.OfSeq(es.Where((_, k) => k != shown[idx]));
        if (src.Count > 0)
        {
            var (x, y) = src[0];   // (tiles of the same shop are synced, so one tile does them all)
            setDecorIcons(x, y, TrackerModel.withShopLabel(TrackerModel.decorEntriesPack(es2), label));
            redrawOwTileHook(x, y);
        }
        else
            TrackerModel.shopStash[label] = TrackerModel.decorEntriesPack(es2);
        decorStateChanged();
    }
    // index (within the item list shopItemBitmapsGlobal gives) of the key in numbered shop n, or -1 if that shop holds no key
    public static int shopKeyIndexGlobal(int n)
    {
        var label = n == 0 ? 5 : n;
        var src = shopTilesWithItems(label);
        var packed = src.Count > 0 ? TrackerModel.owSwordIcons[src[0].Item1, src[0].Item2] : TrackerModel.shopStash[label];
        var idents = TrackerModel.decorEntriesOf(packed).Where(e => decorSourceBmp(e.Item1) != null).Select(e => e.Item1).ToList();
        return idents.FindIndex(isKeyDecor);   // (-1 when none)
    }
    // Progressive Items: every sword (and the progressive sword) shows the current tier, so its cached picture depends on the tier
    public static int progressiveSwordCacheOffset(int item)
    {
        var f = TrackerModel.progressiveFamily(item);
        if (f == 1) return 10000 + 100 * TrackerModel.progressiveSwordTier();
        else if (f >= 2) return 20000 + 100 * TrackerModel.progressiveFamilyTier(f);
        else return 0;
    }
    public static System.Drawing.Bitmap decorIconBmp(int item)
    {
        if (item == TrackerModel.ITEMS.WHITESWORD4 && !TrackerModel.ProgressiveItems && TrackerModelOptions.UseOldSwordIcons.Value)
            return Graphics.ow_sword4_item_bmp;   // (Use old sword icons)
        else if ((item == TrackerModel.ITEMS.PROGRESSIVESWORD || item == TrackerModel.ITEMS.WHITESWORD4) && !TrackerModel.ProgressiveItems)
            return extraSwordIconBmp(item);
        else
        {
            var cacheKey = item + (TrackerModelOptions.UseOldPotionLetterIcons.Value ? 5000 : 0) + progressiveSwordCacheOffset(item);   // old/new potion & letter icons are cached separately
            if (decorIconCache.TryGetValue(cacheKey, out var cached)) return cached;
            else
            {
                var src = decorSourceBmp(item);
                if (src == null) return null;
                else
                {
                    const int B = 3;
                    var w = src.Width;
                    var h = src.Height;
                    var dst = new System.Drawing.Bitmap(w + 2 * B, h + 2 * B);
                    // the old potion/letter icons have a transparent background, so every opaque pixel (even a dark one) gets the black outline
                    var isOldIcon = TrackerModelOptions.UseOldPotionLetterIcons.Value && (item == 17);
                    bool isInk(int x, int y)
                    {
                        var p = src.GetPixel(x, y);
                        return p.A > 0 && (isOldIcon || ((int)p.R + (int)p.G + (int)p.B) > 40);
                    }
                    for (int x = 0; x <= w - 1; x++)
                        for (int y = 0; y <= h - 1; y++)
                            if (isInk(x, y))
                                for (int dx = 0; dx <= 2 * B; dx++)
                                    for (int dy = 0; dy <= 2 * B; dy++)
                                        dst.SetPixel(x + dx, y + dy, System.Drawing.Color.Black);
                    for (int x = 0; x <= w - 1; x++)
                    {
                        for (int y = 0; y <= h - 1; y++)
                        {
                            var p = src.GetPixel(x, y);
                            if (p.A > 0)
                                dst.SetPixel(x + B, y + B, p);
                        }
                    }
                    decorIconCache[cacheKey] = dst;
                    return dst;
                }
            }
        }
    }

    public static System.Drawing.Bitmap decorGreyBmp(int item)
    {
        var cacheKey = item + (TrackerModelOptions.UseOldPotionLetterIcons.Value ? 5000 : 0) + progressiveSwordCacheOffset(item);
        if (decorGreyCache.TryGetValue(cacheKey, out var cached)) return cached;
        else
        {
            var src = decorIconBmp(item);
            var g =
                src == null ? null
                :
                    // greyscale that keeps fully transparent pixels transparent (no black box behind the icon)
                    Graphics.transformColor(src, c =>
                    {
                        if (c.A == 0) return System.Drawing.Color.FromArgb(0, 0, 0, 0);
                        else
                        {
                            var v = Math.Min(255, ((int)c.R + (int)c.G + (int)c.B) / 3 + 40);
                            return System.Drawing.Color.FromArgb((int)c.A, v, v, v);   // collected items are shown in greyscale
                        }
                    });
            decorGreyCache[cacheKey] = g;
            return g;
        }
    }
    // a little green checkmark (dark outline so it reads on any icon) drawn over a marker icon that has been checked off as collected
    public static Control decorCheckmark(double iconSize)
    {
        var s = Math.Max(8.0, iconSize * 0.55);
        var c = new Canvas { Width = s, Height = s, IsHitTestVisible = false };
        IList<Point> pts() => new List<Point> { new Point(s * 0.08, s * 0.55), new Point(s * 0.38, s * 0.88), new Point(s * 0.94, s * 0.12) };
        // (WPF sets StrokeStartLineCap/StrokeEndLineCap; Avalonia has one StrokeLineCap)
        c.Children.Add(new Polyline { Points = pts(), Stroke = Brushes.Black, StrokeThickness = Math.Max(3.5, s * 0.42), StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round });
        c.Children.Add(new Polyline { Points = pts(), Stroke = Brushes.LimeGreen, StrokeThickness = Math.Max(2.0, s * 0.24), StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round });
        return c;
    }
    public static readonly TextBox currentMaxHeartsTextBox = new TextBox { Padding = new Thickness(0.0), VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center, FontSize = 16.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false,
                                            BorderThickness = new Thickness(0.0), Text = $"Max Hearts: {TrackerModel.playerComputedStateSummary.PlayerHearts}" };
    // drawn inside a plain Button (not hit-testable; the wrapper border below handles the mouse) so its border has the same look as the Hint Decoder and the other main-screen buttons
    public static readonly Button currentMaxHeartsButton = new Button { Content = currentMaxHeartsTextBox, Width = 112.0, Height = 28.0, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, IsHitTestVisible = false, Focusable = false };   // 112x28 overall, so it ends right at the top of the map
    private static bool init1() { Graphics.styleRoundedButton(currentMaxHeartsButton); return true; }
    private static readonly bool _init1 = init1();
    // duplicated markers (copy + paste of a tile's marker icons): source tile -> the tiles it was pasted onto
    public static readonly Dictionary<(int, int), List<(int, int)>> dupLinks = new Dictionary<(int, int), List<(int, int)>>();
    // heart containers marked 'collected' on the map count toward the player's max hearts; every change to a tile's icons goes through here
    public static void rebuildMapCollected()
    {
        Array.Clear(TrackerModel.mapCollectedCount, 0, TrackerModel.mapCollectedCount.Length);
        // (with Progressive Items, a shop shown on several tiles is still one shop: its swords / rings / candles / arrows count once, however many tiles carry it)
        var shopCounted = new HashSet<(int, int)>();   // (shop label, item)
        // (every tile with the same shop label is the same shop: a heart container collected there is one heart however many tiles show it, so a shop counts the most hearts any one of its tiles has collected)
        var shopHearts = new Dictionary<int, int>();
        for (int i = 0; i <= 15; i++)
        {
            for (int j = 0; j <= 7; j++)
            {
                var label = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]);
                foreach (var e in TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[i, j]))
                {
                    var (ident, col) = (e.Item1, e.Item2);
                    if (col && ident >= 0 && ident < TrackerModel.mapCollectedCount.Length)
                    {
                        var isRepeat = label > 0 && TrackerModel.progressiveFamily(ident) > 0 && !(shopCounted.Add((label, ident)));
                        if (ident == TrackerModel.ITEMS.HEARTCONTAINER && label > 0)
                        { } // (counted per shop below)
                        else if (!isRepeat) TrackerModel.mapCollectedCount[ident] = TrackerModel.mapCollectedCount[ident] + 1;
                    }
                }
                if (label > 0)
                {
                    var n = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[i, j]).Count(e => e.Item2 && e.Item1 == TrackerModel.ITEMS.HEARTCONTAINER);
                    var cur = shopHearts.TryGetValue(label, out var v) ? v : 0;
                    if (n > cur) shopHearts[label] = n;
                }
            }
        }
        foreach (var kv in shopHearts)
            TrackerModel.mapCollectedCount[TrackerModel.ITEMS.HEARTCONTAINER] = TrackerModel.mapCollectedCount[TrackerModel.ITEMS.HEARTCONTAINER] + kv.Value;
    }
    public static List<int> copiedMarkerIdents = new List<int>();
    public static int copiedMarkerPacked = -1;   // same, including each icon's collected state   // shift+left-click on a tile that has only item markers copies them; shift+left-click on an unmarked tile pastes them
    public static Action decorStateChanged = () => { };   // set once the main UI exists: refreshes the item progress bar etc.
    // potion shops: a tile counts as one if it carries the "P SHOP" label (shop label 5) or is marked as an old-style potion shop; the blue potion marker icon is just a marker and does not count
    public static bool tileHasPotionShop(int i, int j) =>
        TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]) == 5 || TrackerModel.overworldMapMarks[i, j].Current() == TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP;
    // a tile that is already a shop: it has a shop label (1-4, P) or is marked as an old-style item shop / potion shop
    public static bool isShopTile(int i, int j) =>
        TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]) != 0 || TrackerModel.MapSquareChoiceDomainHelper.IsItem(TrackerModel.overworldMapMarks[i, j].Current()) || tileHasPotionShop(i, j);
    public static bool canPlacePotionShop(int i, int j)
    {
        if (tileHasPotionShop(i, j)) return true;
        else
        {
            var n = 0;
            for (int ti = 0; ti <= 15; ti++)
                for (int tj = 0; tj <= 7; tj++)
                    if (tileHasPotionShop(ti, tj)) n = n + 1;
            return n < TrackerModel.mapSquareChoiceDomain.MaxUses(TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP);
        }
    }
    // duplicated markers (copy + paste of a tile's marker icons): source tile -> the tiles it was pasted onto. A duplicate leaves out the items already collected on the original;
    // when one of those is later un-collected on the original, it shows up on the duplicates.
    public static (int, int) copiedMarkerSource = (-1, -1);
    public static bool copiedMarkerBomb = false;   // the copied tile was a bomb shop (the paste makes a bomb shop too)
    public static Action<int, int> redrawOwTileHook = (_i, _j) => { };   // set once the map exists: redraws one overworld tile
    // toggling a marker icon on a tile; a P shop (shop label 5) holds just one item, so picking a different one replaces it
    // Legacy Mode: a shop holds only what the shop window allows (the P shop none at all); adding anything else to a shop tile, by hotkey or from the tile menu, is refused
    // (taking an item out is always allowed). decorToggleRefusedByLegacy says why the tile came back unchanged, for the beep.
    public static bool decorToggleRefusedByLegacy = false;
    public const string legacyShopRefusal = "Legacy Mode: a shop can only hold a wood arrow, blue candle, blue ring, regular key, bombs, meat, book or shield";
    public static int decorToggleForTile(int before, int ident)
    {
        var cur = TrackerModel.decorIconsOf(before);
        decorToggleRefusedByLegacy = false;
        var shopLbl = TrackerModel.shopLabelOf(before);
        if (TrackerModel.LegacyMode && shopLbl > 0 && !(cur.Contains(ident)) && (shopLbl == 5 || !(PieMenus.legacyShopAllows(ident))))
        {
            decorToggleRefusedByLegacy = true;
            return before;
        }
        if (TrackerModel.shopLabelOf(before) == 5 && !cur.IsEmpty && !(cur.Contains(ident)))
            return TrackerModel.decorIconsToggle(TrackerModel.withShopLabel(-1, 5), ident);
        else return TrackerModel.decorIconsToggle(before, ident);
    }
    public static bool syncingShops = false;
    public static bool openShopFlyoutDirectly = false;   // set by the Overworld_Shop hotkey: the next tile popup goes straight to the Shop flyout (potion shop / shops 1-4)
    // the Dead Woods / Lost Hills hint buttons (set when the legend is built): hovering a tile locked by that hint highlights its button
    public static Action<int, bool> hintButtonHighlightHook = (_, _) => { };   // zone (1 = Dead Woods, 2 = Lost Hills), on/off
    public static readonly System.Collections.Generic.Dictionary<string, Action<string>> hintLoadHooks = new();   // "Dead Woods" / "Lost Hills" -> puts a loaded hint's letters into that hint button
    // 'Progressive Items' (ZORA Extras): the next level of each base item; the UI sets the hook (notification + glow) once it exists
    public static Action<List<(int, int)>, System.Drawing.Bitmap, System.Drawing.Bitmap> progressiveUpgradeHook = (_, _, _) => { };   // upgraded tiles, from icon, to icon
    public static Action progressiveSwordCheckHook = () => { };   // the progressive sword's tier (P1..P4) may have changed
    public static bool progressiveWired = false;
    public static readonly int[] progressiveLastTiers = Enumerable.Repeat(1, 6).ToArray();
    public static readonly int[] progressivePrevTiers = Enumerable.Repeat(1, 6).ToArray();   // per family: the tier before the latest change
    public static readonly DateTime[] progressiveChangedAt = Enumerable.Repeat(DateTime.MinValue, 6).ToArray();
    // the tier a family item that was just collected is recorded at: the tier it was showing before it was collected. That is the last tier we saw, unless another handler for this very click
    // already moved the tier up (then it is the tier from before that move)
    public static int progressiveCollectedTier(int family)
    {
        var moved = (DateTime.Now - progressiveChangedAt[family]).TotalMilliseconds < 200.0;
        return Math.Max(1, moved ? progressivePrevTiers[family] : progressiveLastTiers[family]);   // per family (1 swords, 2 rings, 3 candles, 4 arrows): the tier before the change being processed (what a newly collected item is recorded at)
    }
    // (port helper: F#'s `TrackerModel.TimelineItemModel.All.TryGetValue(name)` match)
    private static bool tryTimelineItem(string name, out TrackerModel.TimelineItemModel tim) => TrackerModel.TimelineItemModel.All.TryGetValue(name, out tim);
    public static void setDecorIcons(int i, int j, int newPacked0)
    {
        var oldEntriesForDups = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[i, j]);
        // the tile's "SHOP n" text label is not an item icon: icon changes keep it (only setShopLabel changes it)
        var oldLabel = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]);
        var newPacked = oldLabel > 0 && TrackerModel.shopLabelOf(newPacked0) == 0 ? TrackerModel.withShopLabel(newPacked0, oldLabel) : newPacked0;
        // cave/shop items checked off on the map also check their box in the upper item area (and un-check it again if un-collected)
        var boxLinks = new List<(int, TrackerModel.BoolProperty)> {
            (TrackerModel.ITEMS.WOODARROW,   TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodArrow),
            (TrackerModel.ITEMS.BLUERING,    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueRing),
            (TrackerModel.ITEMS.BLUECANDLE,  TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle),
            (TrackerModel.ITEMS.WOODSWORD,   TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword) };
        var before = boxLinks.Select(p => TrackerModel.mapCollectedCount[p.Item1] > 0).ToList();
        var triforceWasCollectedHere = TrackerModel.decorIsCollected(TrackerModel.owSwordIcons[i, j], TrackerModel.ITEMS.TRIFORCE);
        var heartsBefore = TrackerModel.mapCollectedCount[TrackerModel.ITEMS.HEARTCONTAINER];
        var itemsBefore = (int[])TrackerModel.mapCollectedCount.Clone();
        TrackerModel.owSwordIcons[i, j] = newPacked;
        rebuildMapCollected();
        var swordIds = new List<int> { TrackerModel.ITEMS.PROGRESSIVESWORD, TrackerModel.ITEMS.WOODSWORD, TrackerModel.ITEMS.WHITESWORD, TrackerModel.ITEMS.MAGICALSWORD, TrackerModel.ITEMS.WHITESWORD4 };
        var woodWasStamped = (tryTimelineItem("WoodSword", out var wtim) ? wtim.FinishedTotalSeconds != -1 : false);
        int swordCount(FSharpList<Tuple<int, bool>> es) => es.Count(e => e.Item2 && swordIds.Contains(e.Item1));
        // Progressive Items: whichever sword icon is collected, the Nth sword collected is the Nth entry (wood, white, magical, 4th) on the timeline
        var swordCollectTier = TrackerModel.ProgressiveItems && swordCount(TrackerModel.decorEntriesOf(newPacked)) > swordCount(oldEntriesForDups) ? progressiveCollectedTier(1) : 0;
        if (TrackerModel.ProgressiveItems)
        {
            // remember (or forget) the tier each collected sword was collected at, so its icon keeps showing where the upgrade came from
            int nOf(FSharpList<Tuple<int, bool>> es, int id) => es.Count(e => e.Item1 == id && e.Item2);
            var newEs = TrackerModel.decorEntriesOf(newPacked);
            foreach (var id in new[] { TrackerModel.ITEMS.PROGRESSIVESWORD, TrackerModel.ITEMS.WOODSWORD, TrackerModel.ITEMS.WHITESWORD, TrackerModel.ITEMS.MAGICALSWORD, TrackerModel.ITEMS.WHITESWORD4,
                                       TrackerModel.ITEMS.BLUERING, TrackerModel.ITEMS.REDRING, TrackerModel.ITEMS.BLUECANDLE, TrackerModel.ITEMS.REDCANDLE, TrackerModel.ITEMS.WOODARROW, TrackerModel.ITEMS.SILVERARROW, TrackerModel.ITEMS.BOOMERANG, TrackerModel.ITEMS.MAGICBOOMERANG })
            {
                // an upgrade collected on the map also goes on the timeline (the first tier already has its own entry); item boxes have their own entry that shows the tier
                string tierEntry(int f, int tier) =>
                    (f, tier) switch
                    {
                        (1, 2) => "WhiteSword", (1, 3) => "MagicalSword", (1, 4) => "Sword4",
                        (2, 2) => "RedRing", (3, 2) => "RedCandle", (4, 2) => "SilverArrow",
                        (5, 1) => "Boomerang", (5, 2) => "MagicBoomerang",
                        _ => "",
                    };
                if (nOf(newEs, id) > nOf(oldEntriesForDups, id))
                {
                    var tier = progressiveCollectedTier(TrackerModel.progressiveFamily(id));
                    TrackerModel.progressiveTileTier[Tuple.Create(i, j, id)] = tier;
                    if (TrackerModel.progressiveFamily(id) != 1)   // (swords are stamped below, by how many swords are collected rather than by which icon)
                        if (tryTimelineItem(tierEntry(TrackerModel.progressiveFamily(id), tier), out var tim) && tim.FinishedTotalSeconds == -1) tim.SetCollectedFromMap(true);
                }
                else if (nOf(newEs, id) < nOf(oldEntriesForDups, id))
                {
                    if (TrackerModel.progressiveTileTier.TryGetValue(Tuple.Create(i, j, id), out var tier) && TrackerModel.progressiveFamily(id) != 1)
                        if (tryTimelineItem(tierEntry(TrackerModel.progressiveFamily(id), tier), out var tim))
                            tim.Unstamp();
                    TrackerModel.progressiveTileTier.Remove(Tuple.Create(i, j, id));
                }
            }
            progressiveSwordCheckHook();
        }
        for (int k = 0; k < boxLinks.Count; k++)
        {
            var (id, prop) = boxLinks[k];
            var wasHad = before[k];
            var has = TrackerModel.mapCollectedCount[id] > 0;
            if (has != wasHad) prop.Set(has);
        }
        // a triforce piece icon checked off on a tile marked as a dungeon checks off that dungeon's triforce (and the triforce chart follows), like a triforce piece in its item box
        var triforceIsCollectedHere = TrackerModel.decorIsCollected(newPacked, TrackerModel.ITEMS.TRIFORCE);
        if (triforceIsCollectedHere != triforceWasCollectedHere)
        {
            var mark = TrackerModel.overworldMapMarks[i, j].Current();
            var d = mark - TrackerModel.MapSquareChoiceDomainHelper.DUNGEON_1;
            if (d >= 0 && d <= 7)
            {
                var dungeon = TrackerModel.GetDungeon(d);
                if (dungeon.PlayerHasTriforce() != triforceIsCollectedHere) dungeon.ToggleTriforce();
            }
        }
        // overworld hearts checked off on the map are stamped on the timeline
        var heartsAfter = TrackerModel.mapCollectedCount[TrackerModel.ITEMS.HEARTCONTAINER];
        if (heartsAfter != heartsBefore)
            for (int k = 1; k <= 12; k++)
                if ((heartsBefore >= k) != (heartsAfter >= k))
                    if (tryTimelineItem($"OWHeart{k}", out var tim))
                        tim.SetCollectedFromMap(heartsAfter >= k);
        // the other items (boomerang / boots, bow, ladder, ...) checked off on the map are stamped on the timeline too
        if (swordCollectTier > 0)
        {
            var wanted = swordCollectTier switch { 1 => "WoodSword", 2 => "WhiteSword", 3 => "MagicalSword", _ => "Sword4" };
            // (a wood sword icon checks off the wood sword box, which stamps the wood sword entry whatever its tier: undo that when it is not the first sword)
            if (wanted != "WoodSword" && !woodWasStamped)
                if (tryTimelineItem("WoodSword", out var tim1) && tim1.FinishedTotalSeconds != -1) tim1.Unstamp();
            if (tryTimelineItem(wanted, out var tim2) && tim2.FinishedTotalSeconds == -1) tim2.SetCollectedFromMap(true);
        }
        foreach (var id in Enumerable.Range(1, 13).Concat(Enumerable.Range(TrackerModel.ITEMS.CUSTOM_BASE, 16)))   // (the custom icons too)
        {
            var (hadIt, hasIt) = (itemsBefore[id] > 0, TrackerModel.mapCollectedCount[id] > 0);
            if (hadIt != hasIt && !(TrackerModel.ProgressiveItems && (id == TrackerModel.ITEMS.WHITESWORD || id == TrackerModel.ITEMS.BOOMERANG || id == TrackerModel.ITEMS.MAGICBOOMERANG)))
                if (tryTimelineItem(TrackerModel.ITEMS.itemNamesAndCounts[id].Item1, out var tim))
                    tim.SetCollectedFromMap(hasIt);
        }
        TrackerModel.recomputePlayerStateSummary();
        currentMaxHeartsTextBox.Text = $"Max Hearts: {TrackerModel.playerComputedStateSummary.PlayerHearts}";
        decorStateChanged();
        // every tile that carries the same "SHOP n" label is the same shop: collecting, un-collecting, adding or removing an item on one does it on all of them
        var shopLabelHere = TrackerModel.shopLabelOf(newPacked);
        if (shopLabelHere > 0 && !syncingShops && !TrackerModel.decorEntriesOf(newPacked).Equals(oldEntriesForDups))
        {
            syncingShops = true;
            try
            {
                var newEntries = TrackerModel.decorEntriesOf(newPacked);
                for (int x = 0; x <= 15; x++)
                {
                    for (int y = 0; y <= 7; y++)
                    {
                        if ((x, y) != (i, j) && TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[x, y]) == shopLabelHere && !TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[x, y]).Equals(newEntries))
                        {
                            setDecorIcons(x, y, TrackerModel.withShopLabel(TrackerModel.decorEntriesPack(newEntries), shopLabelHere));
                            redrawOwTileHook(x, y);
                        }
                    }
                }
            }
            finally
            {
                syncingShops = false;
            }
        }
        // an item un-collected on the original shows up on the duplicates
        if (dupLinks.TryGetValue((i, j), out var dups))
        {
            var newEntries = TrackerModel.decorEntriesOf(newPacked);
            int collectedCount(FSharpList<Tuple<int, bool>> entries, int ident) => entries.Count(e => e.Item1 == ident && e.Item2);
            foreach (var ident in oldEntriesForDups.Select(e => e.Item1).Distinct().ToList())
            {
                if (collectedCount(newEntries, ident) < collectedCount(oldEntriesForDups, ident) && (newEntries.Any(e => e.Item1 == ident)))
                {
                    foreach (var (di, dj) in dups.ToList())
                    {
                        var de = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[di, dj]);
                        var have = de.Count(e => e.Item1 == ident);
                        var want = newEntries.Count(e => e.Item1 == ident);
                        if (have < want && de.Length < TrackerModel.MaxDecorIcons)
                        {
                            var label = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[di, dj]);
                            var packed = TrackerModel.decorEntriesPack(ListModule.OfSeq(de.Append(Tuple.Create(ident, false))));
                            setDecorIcons(di, dj, (label > 0 ? TrackerModel.withShopLabel(packed, label) : packed));
                            redrawOwTileHook(di, dj);
                        }
                    }
                }
            }
        }
    }
    private static bool init2() { OverworldMapTileCustomization.setDecorIconsHook = setDecorIcons; return true; }
    private static readonly bool _init2 = init2();
    // set (1-5) or remove (0) the tile's "SHOP n" text label
    // a duplicated item keeps the tier the original was collected at (otherwise its collected icon would show the current, higher tier)
    public static void copyTileTiers((int, int) src, (int, int) dst)
    {
        foreach (var kv in TrackerModel.progressiveTileTier.ToList())
        {
            var (sx, sy, id) = (kv.Key.Item1, kv.Key.Item2, kv.Key.Item3);
            if ((sx, sy) == src) TrackerModel.progressiveTileTier[Tuple.Create(dst.Item1, dst.Item2, id)] = kv.Value;
        }
    }
    public static void linkDuplicate((int, int) src, (int, int) dst)
    {
        if (dupLinks.TryGetValue(src, out var l))
        {
            if (!(l.Contains(dst))) l.Add(dst);
        }
        else
        {
            var nl = new List<(int, int)>();
            nl.Add(dst);
            dupLinks[src] = nl;
        }
    }
    public static void setShopLabel(int i, int j, int label)
    {
        var old = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]);
        if (old > 0 && label != old && TrackerModel.shopLabelCount(old) == 1)
            // this was the last tile of that shop: its items stay in the shop window
            TrackerModel.shopStash[old] = TrackerModel.decorEntriesPack(TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[i, j]));
        TrackerModel.owSwordIcons[i, j] = TrackerModel.withShopLabel(TrackerModel.owSwordIcons[i, j], label);
        if (old > 0 && label > 0 && label != old)
        {
            // a shop tile changed into another shop (menu or hotkey): it holds that shop's items now, not the old shop's
            var others = new List<(int, int)>();
            for (int x = 0; x <= 15; x++)
                for (int y = 0; y <= 7; y++)
                    if ((x, y) != (i, j) && TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[x, y]) == label) others.Add((x, y));
            foreach (var kv in TrackerModel.progressiveTileTier.ToList())
            {
                var (tx, ty) = (kv.Key.Item1, kv.Key.Item2);
                if ((tx, ty) == (i, j)) TrackerModel.progressiveTileTier.Remove(kv.Key);
            }
            if (others.Count > 0)
            {
                var src = others[0];
                TrackerModel.owSwordIcons[i, j] = TrackerModel.withShopLabel(TrackerModel.owSwordIcons[src.Item1, src.Item2], label);
                copyTileTiers(src, (i, j));
                linkDuplicate(src, (i, j));
            }
            else
            {
                if (TrackerModel.shopStash[label] > 0)
                {
                    TrackerModel.owSwordIcons[i, j] = TrackerModel.withShopLabel(TrackerModel.shopStash[label], label);
                    TrackerModel.shopStash[label] = -1;
                }
                else TrackerModel.owSwordIcons[i, j] = TrackerModel.withShopLabel(-1, label);
            }
            rebuildMapCollected();
        }
        else if (label > 0 && label != old && TrackerModel.shopLabelCount(label) == 1 && TrackerModel.shopStash[label] > 0)
        {
            // the first tile of a shop picks up the items already entered in its shop window
            TrackerModel.owSwordIcons[i, j] = TrackerModel.withShopLabel(TrackerModel.shopStash[label], label);
            TrackerModel.shopStash[label] = -1;
        }
        decorStateChanged();
    }
    // removes every marker icon AND the shop label from a tile
    // placing a secret / door repair / money making game on a tile replaces whatever shop (label + item icons) was there
    public static bool tileTypeReplacesShop(int state) => state >= TrackerModel.MapSquareChoiceDomainHelper.UNKNOWN_SECRET && state <= TrackerModel.MapSquareChoiceDomainHelper.MONEY_MAKING_GAME;
    public static void clearAllTileIcons(int i, int j)
    {
        setShopLabel(i, j, 0);
        setDecorIcons(i, j, -1);
    }
    public static readonly TextBox owRemainingScreensTextBox = new TextBox { Width = 116.0, Height = 18.0, FontSize = 12.0, Margin = new Thickness(0.0, -5.0, 0.0, 0.0), Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, TextAlignment = TextAlignment.Center,
                                            BorderThickness = new Thickness(0.0), Text = "OW Spots Left" };
    // the gettable/total numbers, twice the size of the label under them
    public static readonly TextBox owRemainingNumbersTextBox = new TextBox { Width = 116.0, Height = 34.0, FontSize = 24.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, TextAlignment = TextAlignment.Center,
                                            VerticalContentAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(0.0), Text = $"0/{TrackerModel.mapStateSummary.OwSpotsRemain}" };
    // per-type counts of the remaining OW spots, each with its icon (burn, bomb, push, ladder, raft, whistle) plus 'open' for open caves
    public static readonly TextBlock[] owSpotTypeCountTBs =
        Enumerable.Range(0, 7).Select(_ => new TextBlock { FontSize = 14.0, Foreground = Brushes.Orange, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3.0, 0.0, 0.0, 0.0), Text = "0" }).ToArray();
    private static WrapPanel make_owSpotTypesPanel()
    {
        var wp = new WrapPanel { Width = 116.0, Orientation = Orientation.Horizontal };
        var bmps = new[] { Graphics.red_candle_bmp, Graphics.bomb_bmp, Graphics.power_bracelet_bmp, Graphics.ladder_bmp, Graphics.raft_bmp, Graphics.recorder_bmp, Graphics.openCaveIconBmp };
        var tips = new[] { "Burn", "Bomb", "Power Bracelet", "Ladder", "Raft", "Whistle", "Open" };
        for (int k0 = 0; k0 <= 6; k0++)
        {
            var k = k0;
            var cell = new Grid { Width = (k == 6 ? 116.0 : 58.0), Height = 18.0 };
            var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(sp, tips[k]);
            sp.Background = Brushes.Transparent;   // so only the icon+number (not the whole cell) gets mouse events
            // the left column (burn, push, raft) is pushed against the center line and the right column (bomb, ladder, whistle) starts at it, each a little inward from the edge
            if (k < 6)
            {
                if (k % 2 == 0)
                {
                    sp.HorizontalAlignment = HorizontalAlignment.Right;
                    sp.Margin = new Thickness(0.0, 0.0, 6.0, 0.0);
                }
                else
                {
                    sp.HorizontalAlignment = HorizontalAlignment.Left;
                    sp.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
                }
            }
            // hovering a type highlights its unmarked spots on the map, like hovering the matching item in the Item Progress bar
            sp.PointerEntered += (_, _) =>
            {
                if (TrackerModelOptions.RaceMode.Value) return;   // Race Mode: no per-type spot highlighting
                var inst = TrackerModel.owInstance;
                switch (k)
                {
                    case 0: showLocatorInstanceFunc(inst.Burnable); break;
                    case 1: showLocatorInstanceFunc(inst.Bombable); break;
                    case 2: showLocatorInstanceFunc(inst.PowerBraceletable); break;
                    case 3: showLocatorInstanceFunc(inst.Ladderable); break;
                    case 4: showLocatorInstanceFunc(inst.Raftable); break;
                    case 5: showLocatorInstanceFunc(inst.Whistleable); break;
                    default: showLocatorInstanceFunc(inst.Nothingable); break;
                }
            };
            sp.PointerExited += (_, _) => hideLocator();
            cell.Children.Add(sp);
            var img = Graphics.BMPtoImage(bmps[k]);
            img.Width = 16.0;
            img.Height = 16.0;
            img.Stretch = Stretch.Uniform;
            if (k < 6 && k % 2 == 0)
            {
                // left column: the number first, then the icon (next to the center line)
                owSpotTypeCountTBs[k].Margin = new Thickness(0.0, 0.0, 3.0, 0.0);
                sp.Children.Add(owSpotTypeCountTBs[k]);
                sp.Children.Add(img);
            }
            else
            {
                sp.Children.Add(img);
                sp.Children.Add(owSpotTypeCountTBs[k]);
            }
            if (k == 6)
                sp.Children.Add(new TextBlock { FontSize = 14.0, Foreground = Brushes.Orange, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3.0, 0.0, 0.0, 0.0), Text = "open" });
            wp.Children.Add(cell);
        }
        return wp;
    }
    public static readonly WrapPanel owSpotTypesPanel = make_owSpotTypesPanel();
    // the Race Mode summary box (shown at startup while Race Mode is on, and whenever the RACE MODE stamp is clicked); its checkbox is the saved "do not show at startup" setting
    // (port: WPF's blocking ShowDialog() → a nested dispatcher frame, as CustomMessageBox.ShowDialogBlocking)
    public static void showRaceModeNotice(Window owner)
    {
        var w = new Window { Title = "Race Mode", SizeToContent = SizeToContent.WidthAndHeight, CanResize = false, ShowInTaskbar = false, Background = Brushes.Black };
        if (ReferenceEquals(owner, null))
            w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        else
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var sp = new StackPanel { Margin = new Thickness(24.0) };
        void tb(string text, double size, IBrush brush, double bottom) =>
            sp.Children.Add(new TextBlock { Text = text, FontSize = size, Foreground = brush, TextWrapping = TextWrapping.Wrap, MaxWidth = 420.0, Margin = new Thickness(0.0, 0.0, 0.0, bottom) });
        tb((TrackerModelOptions.RaceMode.Value ? "Race Mode is ON" : "Race Mode"), 22.0, Brushes.Orange, 12.0);
        tb("While Race Mode is on:", 14.0, Brushes.White, 6.0);
        tb("•  The overworld spot counts by item (and open) read n/a", 14.0, Brushes.White, 3.0);
        tb("•  No routes are drawn on the overworld", 14.0, Brushes.White, 3.0);
        tb("•  Audio is muted", 14.0, Brushes.White, 3.0);
        tb("•  The 'go time' / 'triforce and go' reminders are off", 14.0, Brushes.White, 12.0);
        tb("Turn it off any time in the Options menu.", 12.0, Brushes.Gray, 14.0);
        var cb = new CheckBox { Content = new TextBlock { Text = "Do not show this at startup", Foreground = Brushes.White, FontSize = 13.0 }, Margin = new Thickness(0.0, 0.0, 0.0, 14.0), HorizontalAlignment = HorizontalAlignment.Left };
        cb.IsChecked = TrackerModelOptions.RaceModeNoticeHidden.Value;
        sp.Children.Add(cb);
        var ok = new Button { Content = "OK", Width = 90.0, Height = 28.0, HorizontalAlignment = HorizontalAlignment.Center, IsDefault = true };
        ok.Click += (_, _) => w.Close();
        sp.Children.Add(ok);
        w.Content = sp;
        var frame = new DispatcherFrame();
        w.Closed += (_, _) =>
        {
            TrackerModelOptions.RaceModeNoticeHidden.Value = (cb.IsChecked.HasValue && cb.IsChecked.Value);
            frame.Continue = false;
        };
        if (!ReferenceEquals(owner, null))
            _ = w.ShowDialog(owner);
        else
            w.Show();
        Dispatcher.UIThread.PushFrame(frame);
    }
    private static StackPanel make_owRemainingScreensTextBoxContainerPanelThatSeesMouseEvents()
    {
        var dp = new StackPanel { Background = Brushes.Black, Orientation = Orientation.Vertical };
        var inner = new StackPanel { Background = Brushes.Black, Orientation = Orientation.Vertical };
        inner.Children.Add(owRemainingNumbersTextBox);
        inner.Children.Add(owRemainingScreensTextBox);
        inner.Children.Add(owSpotTypesPanel);
        // Race Mode: the totals stay, the per-item counts read n/a (dimmed), and a "RACE MODE" stamp is laid across the block
        var stamp = new Border
        {
            Width = 78.0, Height = 21.0, Margin = new Thickness(0.0, 73.5, 0.0, 0.0), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
            BorderBrush = new SolidColorBrush(Color.FromRgb(217, 130, 0)), BorderThickness = new Thickness(2.0), CornerRadius = new CornerRadius(3.0),
            Background = new SolidColorBrush(Color.FromArgb(217, 0, 0, 0)), IsHitTestVisible = true, Cursor = new Cursor(StandardCursorType.Hand),
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative), RenderTransform = new RotateTransform(-35.0),
            Child = new TextBlock { Text = "RACE MODE", FontSize = 11.0, FontWeight = FontWeight.ExtraBold, Foreground = new SolidColorBrush(Color.FromRgb(217, 130, 0)),
                                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false }
        };
        ToolTip.SetTip(stamp, "Click to see what Race Mode does");
        stamp.PointerPressed += (_, ea) =>
        {
            if (ea.GetCurrentPoint(stamp).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)   // (WPF: MouseLeftButtonDown)
            {
                ea.Handled = true;
                showRaceModeNotice(ShowRunCustom.mainWindow());
            }
        };
        var overlay = new Grid { Background = Brushes.Black };
        overlay.Children.Add(inner);
        overlay.Children.Add(stamp);
        dp.Children.Add(overlay);
        void applyRace()
        {
            var race = TrackerModelOptions.RaceMode.Value;
            stamp.IsVisible = race;
            owSpotTypesPanel.Opacity = (race ? 0.4 : 1.0);
            for (int k = 0; k <= 6; k++)
            {
                if (race) owSpotTypeCountTBs[k].Text = "n/a";
                else if (k < TrackerModel.owSpotTypeCounts.Length) owSpotTypeCountTBs[k].Text = TrackerModel.owSpotTypeCounts[k].ToString();
            }
        }
        applyRace();
        OptionsMenu.raceModeChanged += () => applyRace();
        return dp;
    }
    public static readonly StackPanel owRemainingScreensTextBoxContainerPanelThatSeesMouseEvents = make_owRemainingScreensTextBoxContainerPanelThatSeesMouseEvents();
    public static readonly TextBox owGettableScreensTextBox = new TextBox { Width = 80.0, Height = 20.0, FontSize = 14.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false,
                                            BorderThickness = new Thickness(0.0), Text = "0 gettable" };
    public static readonly CheckBox owGettableScreensCheckBox = new CheckBox { Content = owGettableScreensTextBox, IsChecked = TrackerModelOptions.Overworld.Gettables.Value };
    public static CheckBox highlightOpenCavesCheckBox = null;

    // (`type RouteDestination = LinkRouting.RouteDestination` is a `using` alias at the top of the file)

    public static bool NoCyan(int _i, int _j) => false;
    public static void drawRoutesToImpl(RouteDestination routeDestinationOption, Point point, int i, int j, bool drawRouteMarks, int maxBoldGYR, bool showPale, Func<int, int, bool> whatToCyan)
    {
        if (TrackerModelOptions.RaceMode.Value)
            return;
        showPale = owGettableScreensCheckBox.IsChecked.HasValue && owGettableScreensCheckBox.IsChecked.Value ? true : showPale;
        var unmarked = new bool[16, 8];
        for (int x = 0; x <= 15; x++)
            for (int y = 0; y <= 7; y++)
                unmarked[x, y] = TrackerModel.overworldMapMarks[x, y].Current() == -1;
        var interestingButInaccesible = new List<(int, int)>();
        var owTargetworthySpots = new bool[16, 8];
        void processHint(TrackerModel.HintZone hz, bool couldBeLetterDungeon)
        {
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    if (OverworldData.owMapZone[j][i] == hz.AsDataChar())
                    {
                        var cur = TrackerModel.overworldMapMarks[i, j].Current();
                        var cbld = (couldBeLetterDungeon && cur >= 0 && cur <= 7 && TrackerModel.GetDungeon(cur).LabelChar == '?');
                        if (cur == -1 || cbld)
                        {
                            if (TrackerModel.mapStateSummary.OwGettableLocations[i, j] || cbld)
                            {
                                owTargetworthySpots[i, j] = true;
                                unmarked[i, j] = true;  // for cbld case
                            }
                            else
                                interestingButInaccesible.Add((i, j));
                        }
                    }
                }
            }
        }
        var lightUpDestinations = false;
        OverworldRouteDrawing.specificTargetMode = routeDestinationOption != null;
        switch (routeDestinationOption)
        {
            case RouteDestination.OW_MAP(var spots):
                foreach (var (x, y) in spots)
                    owTargetworthySpots[x, y] = true;
                OverworldRouteDrawing.drawPathsImpl(owTargetworthySpots, unmarked, point, i, j, true, false, 0, false, NoCyan);
                lightUpDestinations = true;
                break;
            case RouteDestination.HINTZONE(var hz, var couldBeLetterDungeon):
                processHint(hz, couldBeLetterDungeon);
                OverworldRouteDrawing.drawPathsImpl(owTargetworthySpots, unmarked, point, i, j, true, false, OverworldRouteDrawing.All, false, NoCyan);
                break;
            case RouteDestination.UNMARKEDINSTANCEFUNC(var f):
                for (int x = 0; x <= 15; x++)
                    for (int y = 0; y <= 7; y++)
                        if (unmarked[x, y] && f(x, y))
                            owTargetworthySpots[x, y] = true;
                OverworldRouteDrawing.drawPathsImpl(owTargetworthySpots, unmarked, point, i, j, true, false, OverworldRouteDrawing.MaxGYR, true, NoCyan);
                break;
            case null:
                OverworldRouteDrawing.drawPathsImpl(TrackerModel.mapStateSummary.OwRouteworthySpots, unmarked, point, i, j, drawRouteMarks, true, maxBoldGYR, showPale, whatToCyan);
                break;
        }
        foreach (var (x, y) in interestingButInaccesible)
            OverworldRouteDrawing.routeDrawingLayer.GetHighlightTile(x, y).MakeRed();
        if (lightUpDestinations)
            for (int x = 0; x <= 15; x++)
                for (int y = 0; y <= 7; y++)
                    if (owTargetworthySpots[x, y])
                        OverworldRouteDrawing.routeDrawingLayer.GetHighlightTile(x, y).MakeGreen();
    }

    public static readonly Graphics.SharedEvent resetTimerEvent = new Graphics.SharedEvent();
    public static int currentlyMousedOWX = -1, currentlyMousedOWY = -1;

    // (port helper: F#'s `int "12"` parse)
    private static int parseInt(string s) => int.Parse(s, CultureInfo.InvariantCulture);
    // (port helper: makeAll passes TrackerModel.allUIEventingLogic an F# object expression implementing ITrackerEvents; C# has no object expressions,
    // so this class forwards each member to a delegate that makeAll fills in with the same code)
    private sealed class TrackerEvents : TrackerModel.ITrackerEvents
    {
        public Action<int> currentHearts;
        public Action announceConsiderSword2;
        public Action announceConsiderSword3;
        public Action<int, int> overworldSpotsRemaining;
        public Action<int, int, int, bool, bool> dungeonLocation;
        public Action<int, int, int> anyRoadLocation;
        public Action<int, int> armos;
        public Action<int, int> sword3;
        public Action<int, int> sword2;
        public Action<bool, bool, IEnumerable<Tuple<Tuple<int, int>, int>>, IEnumerable<Tuple<int, int>>, bool[,]> routingInfo;
        public Action<int> announceCompletedDungeon;
        public Action<bool[]> completedDungeons;
        public Action<int> announceFoundDungeonCount;
        public Action<int> announceTriforceCount;
        public Action<int, TrackerModel.TriforceAndGoSummary> announceTriforceAndGo;
        public Action<TrackerModel.DungeonBlocker, IEnumerable<int>, IEnumerable<TrackerModel.CombatUnblockerDetail>> remindUnblock;
        public Action<int> remindShortly;
        void TrackerModel.ITrackerEvents.CurrentHearts(int h) => currentHearts(h);
        void TrackerModel.ITrackerEvents.AnnounceConsiderSword2() => announceConsiderSword2();
        void TrackerModel.ITrackerEvents.AnnounceConsiderSword3() => announceConsiderSword3();
        void TrackerModel.ITrackerEvents.OverworldSpotsRemaining(int remain, int gettable) => overworldSpotsRemaining(remain, gettable);
        void TrackerModel.ITrackerEvents.DungeonLocation(int i, int x, int y, bool hasTri, bool isCompleted) => dungeonLocation(i, x, y, hasTri, isCompleted);
        void TrackerModel.ITrackerEvents.AnyRoadLocation(int i, int x, int y) => anyRoadLocation(i, x, y);
        void TrackerModel.ITrackerEvents.Armos(int x, int y) => armos(x, y);
        void TrackerModel.ITrackerEvents.Sword3(int x, int y) => sword3(x, y);
        void TrackerModel.ITrackerEvents.Sword2(int x, int y) => sword2(x, y);
        void TrackerModel.ITrackerEvents.RoutingInfo(bool haveLadder, bool haveRaft, IEnumerable<Tuple<Tuple<int, int>, int>> currentRecorderWarpDestinations, IEnumerable<Tuple<int, int>> currentAnyRoadDestinations, bool[,] owRouteworthySpots) =>
            routingInfo(haveLadder, haveRaft, currentRecorderWarpDestinations, currentAnyRoadDestinations, owRouteworthySpots);
        void TrackerModel.ITrackerEvents.AnnounceCompletedDungeon(int i) => announceCompletedDungeon(i);
        void TrackerModel.ITrackerEvents.CompletedDungeons(bool[] a) => completedDungeons(a);
        void TrackerModel.ITrackerEvents.AnnounceFoundDungeonCount(int n) => announceFoundDungeonCount(n);
        void TrackerModel.ITrackerEvents.AnnounceTriforceCount(int n) => announceTriforceCount(n);
        void TrackerModel.ITrackerEvents.AnnounceTriforceAndGo(int triforceCount, TrackerModel.TriforceAndGoSummary tagSummary) => announceTriforceAndGo(triforceCount, tagSummary);
        void TrackerModel.ITrackerEvents.RemindUnblock(TrackerModel.DungeonBlocker blockerType, IEnumerable<int> dungeons, IEnumerable<TrackerModel.CombatUnblockerDetail> detail) => remindUnblock(blockerType, dungeons, detail);
        void TrackerModel.ITrackerEvents.RemindShortly(int itemId) => remindShortly(itemId);
    }

    // (F#: `makeAll(...) = async { ... }`, returning the timeline-drawing function. `loadData` is null for None; `showProgress` is an `Async<unit>` maker, here a
    // `Func<string, Task>`. F# `ref` cells local to makeAll are plain locals (C# closures capture variables, not values), and `Async.StartImmediate` of a
    // local `async { }` is `CustomComboBoxes.StartImmediate(go())` with a local `async Task go()`.)
    public static async Task<Action<int>> makeAll(Window mainWindow, CustomComboBoxes.CanvasManager cm, Canvas drawingCanvas, int owMapNum, bool heartShuffle, TrackerModel.DungeonTrackerInstanceKind kind, DungeonSaveAndLoad.AllData loadData,
                    Func<string, Task> showProgress, SpeechRecognition.SpeechRecognitionInstance speechRecognitionInstance)
    {
        var ctxt = SynchronizationContext.Current;
        Popouts.Initialize(mainWindow, ctxt);
        Action refocusMainWindow = Popouts.refocusMainWindow;
        if (loadData != null)
        {
            var data = loadData;
            Graphics.alternativeOverworldMapFilename = data.AlternativeOverworldMapFilename;
            Graphics.shouldInitiallyHideOverworldMap = data.ShouldInitiallyHideOverworldMap;
            // rest of data is loaded at end, but these are needed at start
        }
        // initialize based on startup parameters
        System.Drawing.Bitmap[,] owMapBMPs;
        bool isMixed;
        OverworldData.OverworldInstance owInstance;
        int maxOverworldRemain;
        void pick(int n)
        {
            switch (n)
            {
                case 0: (owMapBMPs, isMixed, owInstance, owMapNum, maxOverworldRemain) = (Graphics.overworldMapBMPs(0), false, new OverworldData.OverworldInstance(OverworldData.OWQuest.FIRST), 0, TrackerModel.MaxRemain1Q); break;
                case 1: (owMapBMPs, isMixed, owInstance, owMapNum, maxOverworldRemain) = (Graphics.overworldMapBMPs(1), false, new OverworldData.OverworldInstance(OverworldData.OWQuest.SECOND), 1, TrackerModel.MaxRemain2Q); break;
                case 2: (owMapBMPs, isMixed, owInstance, owMapNum, maxOverworldRemain) = (Graphics.overworldMapBMPs(2), true, new OverworldData.OverworldInstance(OverworldData.OWQuest.MIXED_FIRST), 2, TrackerModel.MaxRemainMQ); break;
                case 3: (owMapBMPs, isMixed, owInstance, owMapNum, maxOverworldRemain) = (Graphics.overworldMapBMPs(3), true, new OverworldData.OverworldInstance(OverworldData.OWQuest.MIXED_SECOND), 3, TrackerModel.MaxRemainMQ); break;
                case 4: (owMapBMPs, isMixed, owInstance, owMapNum, maxOverworldRemain) = (Graphics.overworldMapBMPs(4), false, new OverworldData.OverworldInstance(OverworldData.OWQuest.BLANK), 4, TrackerModel.MaxRemainUQ); break;
                default: throw new Exception("impossible");
            }
        }
        if (owMapNum >= 0 && owMapNum <= 4)
            pick(owMapNum);
        else if (owMapNum == 999 && loadData != null)
        {
            var q = loadData.Overworld.Quest;
            if (q >= 0 && q <= 4) pick(q);
            else throw new Exception("bad load data at root.Overworld.Quest");
        }
        else throw new Exception("bad/unsupported (owMapNum,loadData)");
        Avalonia.Media.Imaging.Bitmap[,] mapToBIs(System.Drawing.Bitmap[,] bmps)
        {
            var r = new Avalonia.Media.Imaging.Bitmap[bmps.GetLength(0), bmps.GetLength(1)];
            for (int x = 0; x < bmps.GetLength(0); x++)
                for (int y = 0; y < bmps.GetLength(1); y++)
                    r[x, y] = Graphics.BMPToBI(bmps[x, y]);
            return r;
        }
        var owMapBIs = mapToBIs(owMapBMPs);
        var owMapBIsRef = owMapBIs;   // swapped when the user switches the overworld template (1st/2nd/mixed) from the 1Q/2Q menu
        await showProgress("after ow map load");
        if (owMapNum < 0 || owMapNum > 4)
            throw new Exception("bad owMapNum");
        var isStandardHyrule = owMapNum != 4;   // should features assume we know the overworld map as standard/mirrored z1r
        void drawRoutesTo(RouteDestination routeDestinationOption, Point point, int i, int j, bool drawRouteMarks, int maxBoldGYR, Func<int, int, bool> whatToCyan)
        {
            if (isStandardHyrule)
                drawRoutesToImpl(routeDestinationOption, point, i, j, drawRouteMarks, maxBoldGYR, (owGettableScreensCheckBox.IsChecked.HasValue && owGettableScreensCheckBox.IsChecked.Value), whatToCyan);
        }
        TrackerModel.initializeAll(owInstance, kind);
        Z1R_Tracker.Models.RoomSyncBridge.WireRoomChangeEvents();
        CoopSync.subscribeToRoomChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId);
        CoopSync.subscribeToPlayerProgressChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId);
        CoopSync.subscribeToStartingItemsAndExtrasChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId);
        CoopSync.subscribeToItemsChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId);
        CoopSync.subscribeToOverworldChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId);
        CoopSync.subscribeToOverworldTileChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId);
        //CoopSync.subscribeToDungeonChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
        CoopSync.subscribeToDungeonTriforceChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId);
        CoopSync.subscribeToBlockersChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId);
        DrawingLayer.applyCustomMenuIcons();
        OverworldMapTileCustomization.shopItemBitmapsHook = shopItemBitmapsGlobal;
        PieMenus.shopKeyIndexHook = shopKeyIndexGlobal;
        PieMenus.shopItemIdentsHook = shopItemIdentsGlobal;
        PieMenus.removeShopItemAtHook = removeShopItemAtGlobal;
        PieMenus.cheapKeysChangedHook = () =>
        {
            for (int x = 0; x <= 15; x++)
            {
                for (int y = 0; y <= 7; y++)
                {
                    var l = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[x, y]);
                    if (l >= 1 && l <= 4) redrawOwTileHook(x, y);
                }
            }
        };
        OverworldMapTileCustomization.decorBitmapHook = decorSourceBmp;
        CoopSync.subscribeToDungeonMapsChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId);
        CoopSync.subscribeToHiddenDungeonColorLabelChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId);
        //just to test
        CoopSync.subscribeToDoorChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId);




        if (!heartShuffle)
            for (int i = 0; i <= 7; i++)
                TrackerModel.GetDungeon(i).Boxes[0].Set(TrackerModel.ITEMS.HEARTCONTAINER, TrackerModel.PlayerHas.NO);
        // Heart Shuffle 'Intra': each of dungeons 1-8 holds exactly one heart, in one of its item slots (floor or basement). Once every other slot of a
        // dungeon holds a non-heart item, the last open slot is filled with an uncollected heart. A dungeon with 3 slots (1 or 4, 8, or 2 with the 4th
        // sword) only counts the other items once they are collected, so a placed but uncollected item does not give the heart away.
        if (TrackerModel.HeartShuffleMode == 1 && !(TrackerModel.IsHiddenDungeonNumbers.Invoke(null)))
        {
            var autoHearts = new HashSet<TrackerModel.Box>(ReferenceEqualityComparer.Instance);
            var autoExtraHearts = new HashSet<int>();   // dungeons whose extra floor box holds a heart this code put there
            var busy = false;
            bool isHeart(TrackerModel.Box b) => b.CellCurrent() == TrackerModel.ITEMS.HEARTCONTAINER;
            TrackerModel.Box[] slotsOf(int i)
            {
                var l = new List<TrackerModel.Box>(TrackerModel.GetDungeon(i).Boxes);
                if (i == 1 && TrackerModel.level2StairBoxEnabled) l.Add(TrackerModel.level2StairBox);
                return l.ToArray();
            }
            void reconcile(int i)
            {
                if (!busy)
                {
                    busy = true;
                    try
                    {
                        var slots = slotsOf(i);
                        var needCollected = slots.Length >= 3;
                        bool filled(TrackerModel.Box b) => b.CellCurrent() != -1 && !(isHeart(b)) && (!needCollected || b.PlayerHas().Equals(TrackerModel.PlayerHas.YES) || b.CellCurrent() == TrackerModel.ITEMS.TRIFORCE);   // (a triforce placed in a slot counts even before it is collected)
                        bool othersFilled(TrackerModel.Box b) => slots.All(o => ReferenceEquals(o, b) || filled(o));
                        // the extra floor box (shown while the triforce sits in an item slot) may hold a heart this code moved there: take it back if that no longer holds
                        var extra = TrackerModel.GetDungeon(i).ExtraFloorBox;
                        if (autoExtraHearts.Contains(i))
                        {
                            if (!(isHeart(extra) && extra.PlayerHas().Equals(TrackerModel.PlayerHas.NO)))
                                autoExtraHearts.Remove(i);   // (the player changed it)
                            else if (!TrackerModel.GetDungeon(i).TriforceIsInItemSlot)
                            {
                                autoExtraHearts.Remove(i);
                                extra.Set(-1, TrackerModel.PlayerHas.NO);   // (the triforce left the item slot)
                            }
                        }
                        foreach (var b in slots)
                        {
                            if (autoHearts.Contains(b))
                            {
                                if (!(isHeart(b) && b.PlayerHas().Equals(TrackerModel.PlayerHas.NO)))
                                {
                                    autoHearts.Remove(b);   // (the player changed it)
                                    // the guessed heart sat where a triforce piece has now been placed, so the dungeon's heart is on the floor: the extra floor box
                                    if (b.CellCurrent() == TrackerModel.ITEMS.TRIFORCE && i < 8 && extra.CellCurrent() == -1)
                                        if (extra.AttemptToSet(TrackerModel.ITEMS.HEARTCONTAINER, TrackerModel.PlayerHas.NO)) autoExtraHearts.Add(i);
                                }
                                else if (!(othersFilled(b)))
                                {
                                    autoHearts.Remove(b);
                                    b.Set(-1, TrackerModel.PlayerHas.NO);   // (the item that implied this heart was removed)
                                }
                            }
                        }
                        if (!(slots.Any(isHeart)))
                        {
                            var empties = slots.Where(b => b.IsEmptyRedBox).ToArray();
                            if (empties.Length == 1 && othersFilled(empties[0]))
                            {
                                var b = empties[0];
                                if (b.AttemptToSet(TrackerModel.ITEMS.HEARTCONTAINER, TrackerModel.PlayerHas.NO)) autoHearts.Add(b);
                            }
                        }
                    }
                    finally
                    {
                        busy = false;
                    }
                }
            }
            for (int i0 = 0; i0 <= 7; i0++)
            {
                var i = i0;
                var boxes0 = new List<TrackerModel.Box>(TrackerModel.GetDungeon(i).Boxes);
                if (i == 0 || i == 3) boxes0.Add(TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.FinalBoxOf1Or4);
                if (i == 1) boxes0.Add(TrackerModel.level2StairBox);
                var boxes = boxes0.Distinct().ToArray();   // (F#'s Array.distinct: Box has reference equality)
                foreach (var b in boxes)
                    onEvent(b.Changed, _ => reconcile(i));
                if (i == 1) onEvent(TrackerModel.level2StairBoxEnabledChanged.Publish, _ => reconcile(1));
            }
        }
        var spokenOWTiles = TrackerModel.overworldTiles(owInstance.Quest.IsFirstQuestOW, TrackerModel.IsHiddenDungeonNumbers.Invoke(null)).Select(t => t.Item3).ToArray();

        // make the entire UI
        Func<int, int, bool> whetherToCyanOpenCavesOrArmos()
        {
            if (highlightOpenCavesCheckBox != null && highlightOpenCavesCheckBox.IsChecked.HasValue && highlightOpenCavesCheckBox.IsChecked.Value)
            {
                // There are 2 reasons to highlight some open caves.
                // First, you might be looking for wood sword cave to obtain free wood sword (or candle)
                // (the white sword or the letter already taken means the free item came from elsewhere, so there is nothing left to hunt the wood sword cave for)
                var tookWhiteSwordOrLetter = TrackerModel.playerComputedStateSummary.SwordLevel >= 2 || TrackerModel.letterBox.PlayerHas().Equals(TrackerModel.PlayerHas.YES) || TrackerModel.mapCollectedCount[TrackerModel.ITEMS.LETTER] > 0;
                if ((TrackerModel.playerComputedStateSummary.SwordLevel > 0 && (TrackerModel.playerComputedStateSummary.CandleLevel > 0 || tookWhiteSwordOrLetter))
                            || !TrackerModel.mapStateSummary.Sword1Location.Equals(TrackerModel.NOTFOUND))
                {
                    // (highlighting ALL open caves is no longer meaningful, but...)
                    if (TrackerModel.armosBox.CellCurrent() == -1)
                        // Second, if your armos box is still empty, highlight the armos
                        return (x, y) => owInstance.HasArmos(x, y) && !(owInstance.HintLocked(x, y)) && TrackerModel.overworldMapMarks[x, y].Current() == -1
                                      // (not an armos screen the player can't get to yet: raft / ladder / power bracelet / recorder blocked without that item)
                                      && !(owInstance.Raftable(x, y) && !TrackerModel.playerComputedStateSummary.HaveRaft)
                                      && !(owInstance.Ladderable(x, y) && !TrackerModel.playerComputedStateSummary.HaveLadder)
                                      && !(owInstance.PowerBraceletable(x, y) && !TrackerModel.playerComputedStateSummary.HavePowerBracelet)
                                      && !(owInstance.Whistleable(x, y) && !TrackerModel.playerComputedStateSummary.HaveRecorder);
                    else
                        return NoCyan;
                }
                else
                    return (x, y) => owInstance.Nothingable(x, y) && !(owInstance.HintLocked(x, y)) && TrackerModel.overworldMapMarks[x, y].Current() == -1;
            }
            else
                return NoCyan;
        }
        // (F#: a tuple of four closures over a private `routeTarget`)
        var routeTargetLastClickedTime = new TrackerModel.LastChangedTime(TimeSpan.FromMinutes(10.0));
        RouteDestination routeTarget = null;
        bool isSpecificRouteTargetActive() => DateTime.Now - routeTargetLastClickedTime.Time < TimeSpan.FromSeconds(10.0);
        RouteDestination currentRouteTarget()
        {
            if (isSpecificRouteTargetActive())
                return routeTarget;
            else
                return null;
        }
        void eliminateCurrentRouteTarget()
        {
            routeTarget = null;
            routeTargetLastClickedTime.SetAgo(TimeSpan.FromMinutes(10.0));
        }
        void changeCurrentRouteTarget(RouteDestination newTarget)
        {
            routeTargetLastClickedTime.SetNow();
            routeTarget = newTarget;
        }

        var appMainCanvas = cm.AppMainCanvas;
        // (port: WPF's `Input.Mouse.GetPosition(x)` has no Avalonia equivalent; the last pointer position seen over the root canvas (which sees moves in
        // popups too) is kept, in appMainCanvas coordinates, as Layout does)
        var lastMousePos = new Point();
        cm.RootCanvas.AddHandler(InputElement.PointerMovedEvent, new EventHandler<PointerEventArgs>((_, ea) => lastMousePos = ea.GetPosition(appMainCanvas)), RoutingStrategies.Tunnel, true);
        Point mouseGetPosition(Visual v) => ReferenceEquals(v, appMainCanvas) ? lastMousePos : (appMainCanvas.TranslatePoint(lastMousePos, v) ?? lastMousePos);
        Layout.IApplicationLayoutBase layout =
            TrackerModelOptions.ShorterAppWindow.Value
                ? new Layout.ShorterApplicationLayout(cm)
                : new Layout.ApplicationLayout(cm);
        var (boxItemImpl, extrasImage, updateNumberedTriforceDisplayImpl, updateNumberedTriforceDisplayIfItExists) =
            MainTrackerTriforcesAndItems.setup(cm, owInstance, layout, kind);

        var owLocatorTileRectangles = new Graphics.TileHighlightRectangle[16, 8];
        var redrawOWCircle = new StrongBox<Action<int, int>>((_x, _y) => { });
        var (_hideFirstQuestCheckBox, _hideSecondQuestCheckBox, moreFQSQoptionsButton) = MakeFQSQStuff(cm, isMixed, owLocatorTileRectangles, redrawOWCircle);
        // (in mixed quest, the Hide 1st/2nd Quest checkboxes now live in the bottom right of the 1Q/2Q popup menu instead of on the main window)

        CheckBox toggleBookShieldCheckBox = null;
        CheckBox bookIsAtlasCheckBox = null;
        string MakeManualSave()
        {
            var filename = SaveAndLoad.SaveAll(notesTextBox.Text, DungeonUI.theDungeonTabControl.SelectedIndex, exportDungeonModelsJsonLines(), DungeonSaveAndLoad.SaveDrawingLayer(),
                                        Graphics.alternativeOverworldMapFilename, Graphics.shouldInitiallyHideOverworldMap, currentRecorderDestinationIndex,
                                        toggleBookShieldCheckBox.IsChecked.Value, bookIsAtlasCheckBox.IsChecked.Value, SaveAndLoad.SaveType.ManualSave);
            Platform.OfferDownload(filename);   // (port: the browser build also downloads it)
            return filename;
        }

        var mirrorOW = new Border { Child = Graphics.BMPtoImage(Graphics.mirrorOverworldBMP), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1.0) };
        mirrorOW.PointerEntered += (_, _) => mirrorOW.BorderBrush = Brushes.DarkGray;
        mirrorOW.PointerExited += (_, _) => mirrorOW.BorderBrush = Brushes.Gray;
        mirrorOW.PointerPressed += (_, _) =>
        {
            TrackerModel.MirrorOverworld = !TrackerModel.MirrorOverworld;
            doUIUpdateEvent.Trigger();
        };
        ToolTip.SetTip(mirrorOW, "Toggle mirrored overworld");
        OverworldItemGridUI.setMirroredOverworld = v => { TrackerModel.MirrorOverworld = v; doUIUpdateEvent.Trigger(); };
        ToolTip.SetPlacement(mirrorOW, PlacementMode.Top);
        var temporarilyHideAllIconsWhileHoveringEyeball = false;
        var hideIconsEyeball = new Border { Child = Graphics.BMPtoImage(Graphics.eyeballBMP), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1.0) };
        hideIconsEyeball.PointerEntered += (_, _) => { temporarilyHideAllIconsWhileHoveringEyeball = true; OptionsMenu.triggerRequestRedrawOverworldEvent(); };
        hideIconsEyeball.PointerExited += (_, _) => { temporarilyHideAllIconsWhileHoveringEyeball = false; OptionsMenu.triggerRequestRedrawOverworldEvent(); };
        ToolTip.SetTip(hideIconsEyeball, "Hover to hide icons\n(peek at unobscured overworld map)");
        ToolTip.SetPlacement(hideIconsEyeball, PlacementMode.Bottom);
        // the main-screen boxes made by Graphics.makeButton get the same pure-black face as the legend boxes (1Q/2Q, Start Spot, ...)
        void blackenButton(object b)
        {
            if (b is Button bt)
                if (bt.Content is TextBox t) t.Background = Brushes.Black;
        }
        // hovering the white / magical sword hint boxes shows the hint 3x larger below them, like the dungeon hints
        OverworldItemGridUI.addHintZoomHook = (Control fe, int idx, double bx) =>
        {
            fe.PointerEntered += (_, _) =>
            {
                var hz = TrackerModel.GetLevelHint.Invoke(idx);
                if (!hz.Equals(TrackerModel.HintZone.UNKNOWN) && !popupIsActive)
                {
                    var inner = OverworldItemGridUI.HintZoneDisplayTextBox(hz.AsDisplayTwoChars());
                    var vb = new Viewbox { Width = 72.0, Height = 72.0, Stretch = Stretch.Uniform, Child = inner };
                    var b = new Border { BorderBrush = OverworldItemGridUI.hintyBrush, BorderThickness = new Thickness(3.0), Background = Brushes.Black, Child = vb, IsHitTestVisible = false };
                    layout.ClearTopLayerHovers();
                    layout.AddTopLayerHover(b, Math.Max(0.0, (bx - 24.0)), 34.0);
                }
            };
            fe.PointerExited += (_, _) => layout.ClearTopLayerHovers();
            fe.PointerPressed += (_, _) => layout.ClearTopLayerHovers();
        };
        var (white_sword_canvas, mags_canvas, redrawWhiteSwordCanvas, redrawMagicalSwordCanvas, spotSummaryCanvas, invokeExtras,
             owItemGrid, toggleBookShieldCB, bookIsAtlasCB, highlightOpenCavesCB, timerResetButton, spotSummaryTB, ws_ms_bu_toggleButton) =
                MakeItemGrid(cm, boxItemImpl, timelineItems, owInstance, extrasImage, resetTimerEvent, isStandardHyrule, doUIUpdateEvent, MakeManualSave);
        blackenButton(timerResetButton);
        blackenButton(spotSummaryTB);
        toggleBookShieldCheckBox = toggleBookShieldCB;
        bookIsAtlasCheckBox = bookIsAtlasCB;
        highlightOpenCavesCheckBox = highlightOpenCavesCB;
        if (isStandardHyrule)
            layout.AddItemGridStuff(owItemGrid, toggleBookShieldCheckBox, bookIsAtlasCheckBox, highlightOpenCavesCheckBox, timerResetButton, spotSummaryTB, mirrorOW, hideIconsEyeball, moreFQSQoptionsButton, ws_ms_bu_toggleButton);
        else
            layout.AddItemGridStuff(owItemGrid, toggleBookShieldCheckBox, bookIsAtlasCheckBox, highlightOpenCavesCheckBox, timerResetButton, spotSummaryTB, mirrorOW, hideIconsEyeball, null, ws_ms_bu_toggleButton);

        await showProgress("link");

        // overworld map grouping, as main point of support for mirroring
        Action<int, int> animateOverworldTile = (_, _) => { };
        void animateOverworldTileIfOptionIsChecked(int i, int j) => animateOverworldTile(i, j);  // the option is checked in the body - all OW tile changes should call this
        var mirrorOverworldFEs = new List<Control>();   // overworldCanvas (on which all map is drawn) is here, as well as individual tiny textual/icon elements that need to be re-flipped
        var overworldCanvas = new Canvas { Width = OMTW * 16.0, Height = 11.0 * 3.0 * 8.0 };
        layout.AddOverworldCanvas(overworldCanvas);
        mirrorOverworldFEs.Add(overworldCanvas);

        var blockerQueries = new List<Func<(int, (double, double))?>>();
        Action stepAnimateLink;
        if (isStandardHyrule)   // Routing only works on standard map
        {
            var (stepAnimateLink0, linkIcon, currentTargetIcon) =
                LinkRouting.SetupLinkRouting(cm, changeCurrentRouteTarget, eliminateCurrentRouteTarget, isSpecificRouteTargetActive, blockerQueries, updateNumberedTriforceDisplayImpl,
                                               () => displayIsCurrentlyMirrored, new MapStateProxy(14).DefaultInteriorBmp(), owInstance, redrawWhiteSwordCanvas, redrawMagicalSwordCanvas);
            layout.AddLinkRouting(linkIcon, currentTargetIcon);
            stepAnimateLink = stepAnimateLink0;
        }
        else
            stepAnimateLink = () => { };

        await showProgress("overworld start start 1");

        layout.AddWebcamLine();
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        // ow map opaque fixed bottom layer
        var X_OPACITY = Graphics.X_OPACITY;
        var owOpaqueMapGrid = makeGrid(16, 8, (int)OMTW, 11 * 3);
        owOpaqueMapGrid.IsHitTestVisible = false;  // do not let this layer see/absorb mouse interactions
        var owOpaqueImages = new Image[16, 8];
        for (int i = 0; i <= 15; i++)
        {
            for (int j = 0; j <= 7; j++)
            {
                var image = Graphics.BItoImage(owMapBIs[i, j]);
                owOpaqueImages[i, j] = image;
                var c = new Canvas { Width = OMTW, Height = (double)(11 * 3) };
                canvasAdd(c, image, 0.0, 0.0);
                gridAdd(owOpaqueMapGrid, c, i, j);
                // shading between map tiles
                var OPA = 0.25;
                var bottomShade = new Canvas { Width = OMTW, Height = (double)(3), Background = Brushes.Black, Opacity = OPA };
                canvasAdd(c, bottomShade, 0.0, (double)(10 * 3));
                var rightShade = new Canvas { Width = (double)(3), Height = (double)(11 * 3), Background = Brushes.Black, Opacity = OPA };
                canvasAdd(c, rightShade, OMTW - 3.0, 0.0);
                // permanent icons (only for tiles that are ALWAYS empty in every quest variant, never switchable)
                if (owInstance.AlwaysEmpty(i, j) && OverworldData.owMapSquaresSecondQuestOnly[j][i] != 'X' && OverworldData.owMapSquaresFirstQuestOnly[j][i] != 'X')
                {
                    var icon = Graphics.BMPtoImage(Graphics.theFullTileBmpTable[TrackerModel.MapSquareChoiceDomainHelper.DARK_X][0]); // "X"
                    icon.Opacity = X_OPACITY;
                    canvasAdd(c, icon, 0.0, 0.0);
                }
            }
        }
        canvasAdd(overworldCanvas, owOpaqueMapGrid, 0.0, 0.0);

        // layer to place darkening icons - dynamic icons that are below route-drawing but above the fixed base layer
        var owDarkeningMapGrid = makeGrid(16, 8, (int)OMTW, 11 * 3);
        var owDarkeningMapGridCanvases = new Canvas[16, 8];
        owDarkeningMapGrid.IsHitTestVisible = false;  // do not let this layer see/absorb mouse interactions
        for (int i = 0; i <= 15; i++)
        {
            for (int j = 0; j <= 7; j++)
            {
                var c = new Canvas { Width = OMTW, Height = (double)(11 * 3) };
                gridAdd(owDarkeningMapGrid, c, i, j);
                owDarkeningMapGridCanvases[i, j] = c;
            }
        }
        canvasAdd(overworldCanvas, owDarkeningMapGrid, 0.0, 0.0);

        // layer to place 'hiding' icons - dynamic darkening icons that are below route-drawing but above the previous layers
        { // HFQ/HSQ (created for every overworld template, since the template can be switched to mixed from the 1Q/2Q menu)
            var owHidingMapGrid = makeGrid(16, 8, (int)OMTW, 11 * 3);
            var owHidingMapGridCanvases = new Canvas[16, 8];
            owHidingMapGrid.IsHitTestVisible = false;  // do not let this layer see/absorb mouse interactions
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    var c = new Canvas { Width = OMTW, Height = (double)(11 * 3) };
                    gridAdd(owHidingMapGrid, c, i, j);
                    owHidingMapGridCanvases[i, j] = c;
                }
            }
            canvasAdd(overworldCanvas, owHidingMapGrid, 0.0, 0.0);
            void hide(int x, int y)
            {
                var hideColor = Brushes.DarkSlateGray;
                var hideOpacity = 0.7;
                var mark = new Ellipse { Width = OMTW - 2.0, Height = (double)(11 * 3) - 2.0, Stroke = hideColor, StrokeThickness = 3.0, Fill = hideColor, Opacity = hideOpacity };
                canvasAdd(owHidingMapGridCanvases[x, y], mark, 0.0, 0.0);
            }
            hideSecondQuestFromMixed =
                (unhide =>  // make mixed appear reduced to 1st quest
                {
                    for (int x = 0; x <= 15; x++)
                    {
                        for (int y = 0; y <= 7; y++)
                        {
                            if (OverworldData.owMapSquaresSecondQuestOnly[y][x] == 'X')
                            {
                                if (unhide)
                                    owHidingMapGridCanvases[x, y].Children.Clear();
                                else
                                    hide(x, y);
                            }
                        }
                    }
                });
            hideFirstQuestFromMixed =
                (unhide =>   // make mixed appear reduced to 2nd quest
                {
                    for (int x = 0; x <= 15; x++)
                    {
                        for (int y = 0; y <= 7; y++)
                        {
                            if (OverworldData.owMapSquaresFirstQuestOnly[y][x] == 'X')
                            {
                                if (unhide)
                                    owHidingMapGridCanvases[x, y].Children.Clear();
                                else
                                    hide(x, y);
                            }
                        }
                    }
                });
        }
        {  // SailNot/NoFeats
            var owHidingMapGrid = makeGrid(16, 8, (int)OMTW, 11 * 3);
            var owHidingMapGridCanvases = new Canvas[16, 8];
            owHidingMapGrid.IsHitTestVisible = false;  // do not let this layer see/absorb mouse interactions
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    var c = new Canvas { Width = OMTW, Height = (double)(11 * 3) };
                    gridAdd(owHidingMapGrid, c, i, j);
                    owHidingMapGridCanvases[i, j] = c;
                }
            }
            canvasAdd(overworldCanvas, owHidingMapGrid, 0.0, 0.0);
#pragma warning disable CS8321 // (unused, as in WPF)
            void _hide(int x, int y)
            {
                var hideColor = Brushes.DarkSlateGray;
                var hideOpacity = 0.7;
                var mark = new Ellipse { Width = OMTW - 2.0, Height = (double)(11 * 3) - 2.0, Stroke = hideColor, StrokeThickness = 3.0, Fill = hideColor, Opacity = hideOpacity };
                canvasAdd(owHidingMapGridCanvases[x, y], mark, 0.0, 0.0);
            }
#pragma warning restore CS8321
            // red dashed borders (instead of dimming) for hinted feat of strength / raft / recorder spots
            var dashGrid = makeGrid(16, 8, (int)OMTW, 11 * 3);
            dashGrid.IsHitTestVisible = false;
            var dashCanvases = new Canvas[16, 8];
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    var c = new Canvas { Width = OMTW, Height = (double)(11 * 3) };
                    gridAdd(dashGrid, c, i, j);
                    dashCanvases[i, j] = c;
                }
            }
            canvasAdd(overworldCanvas, dashGrid, 0.0, 0.0);
            void setDashed(string tag, Func<int, int, bool> pred, bool b)
            {
                for (int x = 0; x <= 15; x++)
                {
                    for (int y = 0; y <= 7; y++)
                    {
                        Canvas c = dashCanvases[x, y];
                        var existing = c.Children.Where(e => Equals(e.Tag, tag)).ToList();
                        foreach (var e in existing) c.Children.Remove(e);
                        if (b && pred(x, y))
                        {
                            // same size and thickness as the (cyan) cave highlight rectangle, so the edges line up with it
                            var r = new Rectangle { Width = OMTW, Height = (double)(11 * 3), Stroke = Brushes.Red, StrokeThickness = 4.0, StrokeDashArray = new AvaloniaList<double>(new[] { 2.0, 1.5 }), Tag = tag, IsHitTestVisible = false };
                            canvasAdd(c, r, 0.0, 0.0);
                        }
                    }
                }
            }
            hideFeatsOfStrength = (b => setDashed("feats", (x, y) => owInstance.PowerBraceletable(x, y) || owInstance.GravePushable(x, y), b));
            hideRaftSpots = (b => setDashed("raft", (x, y) => owInstance.Raftable(x, y), b));
        }

        // ow route drawing layer
        OverworldRouteDrawing.routeDrawingLayer.AddSelfTo(overworldCanvas);

        // middle click overworld circles
        Ellipse makeOwCircle(IBrush brush) => new Ellipse { Width = (double)(11 * 3) - 2.0, Height = (double)(11 * 3) - 2.0, Stroke = brush, StrokeThickness = 3.0, IsHitTestVisible = false };
        var pinkBrush = Graphics.freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x40, 0x99)));
        var brightBlueBrush = Graphics.freeze(new SolidColorBrush(Color.FromRgb(0x00, 0xD0, 0xFF)));
        IBrush owCircleColor(int data) => data >= 200 ? Brushes.Yellow : data >= 100 ? pinkBrush : brightBlueBrush;
        var overworldCirclesCanvas = new Canvas { Width = 16.0 * OMTW, Height = (double)(8 * 11 * 3) };
        overworldCirclesCanvas.IsHitTestVisible = false;
        var owCircleRedraws = new Action[16, 8];
        for (int i0 = 0; i0 <= 15; i0++)
        {
            for (int j0 = 0; j0 <= 7; j0++)
            {
                var i = i0;
                var j = j0;
                var c = new Canvas { Width = OMTW, Height = (double)(11 * 3) };
                mirrorOverworldFEs.Add(c);
                canvasAdd(overworldCirclesCanvas, c, OMTW * (double)(i), (double)(j * 11 * 3));
                void redraw()
                {
                    c.Children.Clear();
                    var v = TrackerModel.overworldMapCircles[i, j];
                    var brush = owCircleColor(v);
                    v = v % 100;
                    if (v != 0)
                        canvasAdd(c, makeOwCircle(brush), 11.5 * OMTW / 48.0 - 3.0, 0.0);
                    if ((v >= 48 && v <= 57) || (v >= 65 && v <= 90))
                    {
                        var tb = new TextBox { Text = ((char)v).ToString(), FontSize = 12.0, FontWeight = FontWeight.Bold, Foreground = brush, Background = Brushes.Black,
                                                IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0) };
                        c.Children.Add(tb);
                        Canvas.SetLeft(tb, 0.0);
                        Canvas.SetBottom(tb, 0.0);
                    }
                }
                owCircleRedraws[i, j] = redraw;
            }
        }
        redrawOWCircle.Value = (x, y) => owCircleRedraws[x, y]();

        await showProgress("overworld magnifier");
        var (onMouseForMagnifier, dungeonTabsOverlay, dungeonTabsOverlayContent) = UIComponents.MakeMagnifier(mirrorOverworldFEs, owMapNum, owMapBMPs);
        // Marking a spot opens a popup that takes the pointer off the tile (exit) and gives it back when it closes (enter). Hiding and rebuilding the magnifier
        // across that made it flash, so: stay shown while a popup is open, and don't rebuild for the tile it already shows.
        var magnifierShownFor = (-1, -1);
        var pointerOverOwTile = false;
        DispatcherTimer magnifierHideTimer = null;
        void hideMagnifier()
        {
            dungeonTabsOverlayContent.Children.Clear();
            dungeonTabsOverlay.Opacity = 0.0;
            magnifierShownFor = (-1, -1);
        }
        await showProgress("overworld start 2");
        // ow map -> dungeon tabs interaction
        var selectDungeonTabEvent = new Graphics.SharedEvent<int>();
        // ow map
        Func<Task> AsyncBrieflyHighlightAnOverworldLocation(int lx, int ly) => async () =>
        {
            showLocatorExactLocation(lx, ly);
            await Task.Delay(3000);   // (resumes on the UI thread: WPF's SwitchToContext ctxt)
            hideLocator();  // this does mean the dungeon location highlight will disappear if we're moused in a dungeon, and routing marks if on overworld
            // ideally, would have a way to respect the mouse position when done, but the code for mouse is all MouseEnters on overworld/dungeon and no easy way to do...
            // ...or is there?
            var p = mouseGetPosition(cm.AppMainCanvas);
            Graphics.SilentlyWarpMouseCursorTo(new Point(0.0, 0.0));
            Graphics.SilentlyWarpMouseCursorTo(p);
        };
        var owMapGrid = makeGrid(16, 8, (int)OMTW, 11 * 3);
        var owCanvases = new Canvas[16, 8];
        var owUpdateFunctions = new Action<int, string>[16, 8];
        for (int x = 0; x <= 15; x++) for (int y = 0; y <= 7; y++) owUpdateFunctions[x, y] = (_, _) => { };
        void drawDarkening(Canvas c, double x, int y)
        {
            var rect = new Rectangle { Width = OMTW, Height = (double)(11 * 3), Stroke = Brushes.Black, StrokeThickness = 3.0,
                                       Fill = Brushes.Black, Opacity = X_OPACITY };
            canvasAdd(c, rect, x * OMTW, (double)(y * 11 * 3));
        }
        var ntf = new UIHelpers.NotTooFrequently(TimeSpan.FromSeconds(0.25));
        Action ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes = () => { };
        overworldCanvas.PointerExited += (_, _) =>
        {
            if (!popupIsActive)
                ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes();
        };
        await showProgress("overworld before 16x8 loop");
        Point centerOf(int i, int j) => overworldCanvas.TranslatePoint(new Point((double)(i) * OMTW + OMTW / 2.0, (double)(j * 11 * 3) + (double)(11 * 3) / 2.0), appMainCanvas).Value;
        var owMapDummyImages = new Image[16, 8];
        for (int i = 0; i <= 15; i++)
        {
            for (int j = 0; j <= 7; j++)
            {
                var image = Graphics.BItoImage(owMapBIs[i, j]);
                image.Opacity = 0.001;
                owMapDummyImages[i, j] = image;
            }
        }
        // Ctrl+Z undoes the most recent overworld tile marking (made from the tile popup)
        (int, int, int)? lastTileChange = null;
        // (WPF: EventManager.RegisterClassHandler(typeof<Window>, Keyboard.PreviewKeyDownEvent, ...))
        InputElement.KeyDownEvent.AddClassHandler<Window>((_, ea) =>
        {
            if (ea.Key == Key.Z && Platform.CmdAsCtrl(ea.KeyModifiers) == KeyModifiers.Control   // (port: on macOS Cmd counts as Ctrl)
                    && !popupIsActive && !(ea.Source is TextBox))
            {
                if (lastTileChange is (int, int, int) ltc)
                {
                    var (ui, uj, orig) = ltc;
                    lastTileChange = null;
                    ea.Handled = true;
                    if (TrackerModel.overworldMapMarks[ui, uj].AttemptToSet(orig))
                        OptionsMenu.triggerRequestRedrawOverworldEvent();
                }
            }
        }, RoutingStrategies.Tunnel);
        // when the overworld template is switched (e.g. 1st quest -> mixed) from the 1Q/2Q menu, show that template's map art too
        onEvent(TrackerModel.overworldQuestChanged.Publish, _ =>
        {
            var n = TrackerModel.owInstance.Quest.AsInt();
            if (n >= 0 && n <= 3)
            {
                try
                {
                    var newBIs = mapToBIs(Graphics.overworldMapBMPs(n));
                    owMapBIsRef = newBIs;
                    for (int i = 0; i <= 15; i++)
                    {
                        for (int j = 0; j <= 7; j++)
                        {
                            owOpaqueImages[i, j].Source = newBIs[i, j];
                            owMapDummyImages[i, j].Source = newBIs[i, j];
                        }
                    }
                }
                catch (Exception) { }
            }
        });
        // copied item markers follow the mouse cursor until pasted over / cancelled with a right click
        var copyCursor = new Canvas { IsHitTestVisible = false, Width = 70.0, Height = 24.0 };
        var copyCursorAdded = false;
        void refreshCopyCursor()
        {
            if (!copyCursorAdded)
            {
                copyCursorAdded = true;
                canvasAdd(appMainCanvas, copyCursor, -1000.0, -1000.0);
                appMainCanvas.AddHandler(InputElement.PointerMovedEvent, new EventHandler<PointerEventArgs>((_, ea) =>
                {
                    if (copiedMarkerIdents.Count != 0)
                    {
                        var p = ea.GetPosition(appMainCanvas);
                        Canvas.SetLeft(copyCursor, p.X + 12.0);
                        Canvas.SetTop(copyCursor, p.Y + 12.0);
                    }
                }), RoutingStrategies.Tunnel);
                appMainCanvas.AddHandler(InputElement.PointerPressedEvent, new EventHandler<PointerPressedEventArgs>((_, ea) =>
                {
                    if (ea.GetCurrentPoint(appMainCanvas).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)   // (WPF: PreviewMouseRightButtonDown)
                    {
                        if (copiedMarkerIdents.Count != 0)
                        {
                            copiedMarkerIdents = new List<int>();
                            copyCursor.Children.Clear();
                            Canvas.SetLeft(copyCursor, -1000.0);
                            ea.Handled = true;   // right click cancels the copy instead of doing its usual thing
                        }
                    }
                }), RoutingStrategies.Tunnel);
            }
            copyCursor.Children.Clear();
            if (copiedMarkerIdents.Count == 0) Canvas.SetLeft(copyCursor, -1000.0);   // nothing copied (any more): no cursor icons
            else
            {
                var k = 0;
                foreach (var e in TrackerModel.decorEntriesOf(copiedMarkerPacked))
                {
                    var (ident, col) = (e.Item1, e.Item2);
                    var bmp = decorIconBmp(ident);   // collected items just get a green checkmark
                    if (bmp != null)
                    {
                        var img = Graphics.BMPtoImage(bmp);
                        img.Stretch = Stretch.Uniform;
                        img.Width = 22.0;
                        img.Height = 22.0;
                        img.Opacity = 0.85;
                        canvasAdd(copyCursor, img, (double)k * 23.0, 0.0);
                        if (col && ident != TrackerModel.ITEMS.OLDKEY)
                        {
                            var ck = decorCheckmark(22.0);
                            canvasAdd(copyCursor, ck, (double)k * 23.0 + (22.0 - ck.Width) / 2.0, (22.0 - ck.Height) / 2.0);
                        }
                    }
                    k++;
                }
                var mp = mouseGetPosition(appMainCanvas);
                Canvas.SetLeft(copyCursor, mp.X + 12.0);
                Canvas.SetTop(copyCursor, mp.Y + 12.0);
            }
        }
        for (int i0 = 0; i0 <= 15; i0++)
        {
            for (int j0 = 0; j0 <= 7; j0++)
            {
                var i = i0;
                var j = j0;
                void activateCircleLabelPopup()
                {
                    if (!popupIsActive)
                    {
                        popupIsActive = true;
                        var tb = new TextBox { Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, FontSize = 16.0, TextAlignment = TextAlignment.Center,
                                                Text = "Press a key A-Z or 0-9 to label this circle\nOR\nLeft-click to make an un-labeled circle\nOR\nRight-click to remove this circle" };
                        var element = new Canvas { Width = appMainCanvas.Width, Height = appMainCanvas.Height, Background = Brushes.Transparent, IsHitTestVisible = true };
                        canvasAdd(element, tb, 250.0, 10.0);
                        var baseColor = TrackerModel.overworldMapCircles[i, j] >= 200 ? 200 : TrackerModel.overworldMapCircles[i, j] >= 100 ? 100 : 0;
                        var displayCircle = makeOwCircle(owCircleColor(baseColor));
                        canvasAdd(element, displayCircle, (double)(displayIsCurrentlyMirrored ? (15 - i) : i) * OMTW + 8.5 * OMTW / 48.0, 150.0 + (double)(j * 11 * 3));
                        var wh = new ManualResetEvent(false);
                        element.PointerPressed += (_, ea) =>
                        {
                            var pk = ea.GetCurrentPoint(element).Properties.PointerUpdateKind;
                            if (pk == PointerUpdateKind.LeftButtonPressed)
                            {
                                TrackerModel.overworldMapCircles[i, j] = baseColor + 1;
                                wh.Set();
                            }
                            else if (pk == PointerUpdateKind.RightButtonPressed)
                            {
                                TrackerModel.overworldMapCircles[i, j] = baseColor + 0;
                                wh.Set();
                            }
                        };
                        element.MyKeyAdd(ea =>
                        {
                            var key = ea.Key.Item2;
                            if (key >= Key.D0 && key <= Key.D9)
                            {
                                TrackerModel.overworldMapCircles[i, j] = baseColor + (int)key - (int)Key.D0 + (int)'0';
                                wh.Set();
                            }
                            else if (key >= Key.A && key <= Key.Z)
                            {
                                TrackerModel.overworldMapCircles[i, j] = baseColor + (int)key - (int)Key.A + (int)'A';
                                wh.Set();
                            }
                        });
                        async Task go()
                        {
                            await CustomComboBoxes.DoModalCore(cm, wh, (c, e) => canvasAdd(c, e, 0.0, 0.0), (c, e) => c.Children.Remove(e), element, 0.7);
                            owCircleRedraws[i, j]();
                            popupIsActive = false;
                        }
                        CustomComboBoxes.StartImmediate(go());
                    }
                }
                var c = new Canvas { Width = OMTW, Height = (double)(11 * 3) };
                gridAdd(owMapGrid, c, i, j);
                // we need a dummy image to make the canvas absorb the mouse interactions, so just re-draw the map at almost 0 opacity
                canvasAdd(c, owMapDummyImages[i, j], 0.0, 0.0);
                // highlight mouse, do mouse-sensitive stuff
                // (StrokeThickness 1 spelled out: WPF's default is 1, Avalonia's is 0, which made this hover outline invisible in the port)
                var rect = new Rectangle { Width = OMTW - 4.0, Height = (double)(11 * 3) - 4.0, Stroke = Brushes.White, StrokeThickness = 1.0 };
                c.PointerEntered += (_, ea) =>
                {
                    canvasAdd(c, rect, 2.0, 2.0);
                    // draw routes
                    var mousePos = ea.GetPosition(c);
                    mousePos = displayIsCurrentlyMirrored ? new Point(OMTW - mousePos.X, mousePos.Y) : mousePos;
                    // hovering an any road highlights all of them, the same style as hovering a blocking item (like the candle) does; otherwise the usual routes
                    bool isAnyRoad(int x, int y)
                    {
                        var m = TrackerModel.overworldMapMarks[x, y].Current();
                        return m >= TrackerModel.MapSquareChoiceDomainHelper.WARP_1 && m <= TrackerModel.MapSquareChoiceDomainHelper.WARP_4;
                    }
                    var hoveringAnyRoad = isAnyRoad(i, j);
                    ntf.SendThunk(() =>
                    {
                        if (hoveringAnyRoad) showLocatorInstanceFuncAll(isAnyRoad);
                        else
                            drawRoutesTo(currentRouteTarget(), mousePos, i, j, TrackerModelOptions.Overworld.DrawRoutes.Value,
                                            (TrackerModelOptions.Overworld.HighlightNearby.Value ? OverworldRouteDrawing.MaxGYR : 0),
                                            whetherToCyanOpenCavesOrArmos());
                    });
                    if (TrackerModelOptions.Overworld.ShowMagnifier.Value)
                    {
                        pointerOverOwTile = true;
                        if (magnifierShownFor != (i, j) || dungeonTabsOverlay.Opacity < 1.0)
                        {
                            onMouseForMagnifier(i, j);   // somewhat expensive, only call if it's getting shown
                            magnifierShownFor = (i, j);
                        }
                    }
                    // track current location for F5 & speech recognition purposes
                    currentlyMousedOWX = i;
                    currentlyMousedOWY = j;
                };
                c.PointerExited += (_, _) =>
                {
                    c.Children.Remove(rect);
                    {
                        var m = TrackerModel.overworldMapMarks[i, j].Current();
                        if (m >= TrackerModel.MapSquareChoiceDomainHelper.WARP_1 && m <= TrackerModel.MapSquareChoiceDomainHelper.WARP_4) hideLocator();
                    }
                    pointerOverOwTile = false;
                    if (magnifierHideTimer == null)
                    {
                        // don't hide at once: redrawing a marked tile, or a popup opening and closing, makes the pointer leave and re-enter within a few
                        // milliseconds, and hiding then rebuilding is the flicker. Hide only if no tile has the pointer once things settle (and no popup is open).
                        magnifierHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100.0) };
                        magnifierHideTimer.Tick += (_, _) =>
                        {
                            if (popupIsActive) return;
                            magnifierHideTimer.Stop();
                            magnifierHideTimer = null;
                            if (!pointerOverOwTile) hideMagnifier();
                        };
                        magnifierHideTimer.Start();
                    }
                };
                c.MyKeyAdd(ea =>
                {
                    var found = HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key, out var target);
                    var mirrored = displayIsCurrentlyMirrored;
                    if (found && ((target == HotKeys.GlobalHotkeyTargets.MoveCursorRight && !mirrored) || (target == HotKeys.GlobalHotkeyTargets.MoveCursorLeft && mirrored)))
                    {
                        ea.Handled = true;
                        if (i < 15)
                            Graphics.NavigationallyWarpMouseCursorTo(centerOf(i + 1, j));
                    }
                    else if (found && ((target == HotKeys.GlobalHotkeyTargets.MoveCursorLeft && !mirrored) || (target == HotKeys.GlobalHotkeyTargets.MoveCursorRight && mirrored)))
                    {
                        ea.Handled = true;
                        if (i > 0)
                        {
                            var n = i - 1;   // without this I seem to encounter a compiler bug?!?
                            Graphics.NavigationallyWarpMouseCursorTo(centerOf(n, j));
                        }
                    }
                    else if (found && target == HotKeys.GlobalHotkeyTargets.MoveCursorDown)
                    {
                        ea.Handled = true;
                        if (j < 7)
                            Graphics.NavigationallyWarpMouseCursorTo(centerOf(i, j + 1));
                    }
                    else if (found && target == HotKeys.GlobalHotkeyTargets.MoveCursorUp)
                    {
                        ea.Handled = true;
                        if (j > 0)
                            Graphics.NavigationallyWarpMouseCursorTo(centerOf(i, j - 1));
                    }
                });
                // icon
                // Every tile gets the full interactive handler (right-click menu, circles, hotkeys).
                // isPermEmpty marks tiles that are empty in every quest variant; their 'X' comes from the base layer, and they are never hidden in hide-and-reveal mode.
                var isPermEmpty = owInstance.AlwaysEmpty(i, j) && OverworldData.owMapSquaresSecondQuestOnly[j][i] != 'X' && OverworldData.owMapSquaresFirstQuestOnly[j][i] != 'X';
                // fairy icons and the coast-item box live on always-empty tiles; they must be re-added on every redraw since redrawGridSpot clears the canvas
                var permanentDecorations = new List<(Control, double, double)>();
                if (isPermEmpty)
                {
                    if (i == 9 && j == 3 || i == 3 && j == 4 || (owInstance.Quest.Equals(OverworldData.OWQuest.SECOND) && i == 11 && j == 0)) // fairy spots
                        permanentDecorations.Add((Graphics.BMPtoImage(Graphics.fairy_bmp), OMTW / 2.0 - 8.0, 1.0));
                    if (i == 15 && j == 5) // coast item ladder spot
                    {
                        IEnumerable<(Control, double, double)> extraDecorationsF(Point boxPos)
                        {
                            var (lx, ly) = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.LADDER_ITEM_BOX);
                            return OverworldMapTileCustomization.computeExtraDecorationArrow(lx, ly, boxPos);
                        }
                        var coastBoxOnOwGrid = Views.MakeBoxItemWithExtraDecorations(cm, TrackerModel.ladderBox, false, extraDecorationsF).Item1;
                        // (not re-flipped: its tile canvas is already flipped back, so flipping the box again made the ladder backwards on a mirrored map)
                        doUIUpdateEvent.Fired += () =>
                        {
                            if (!(TrackerModel.ladderBox.IsDone()))
                            {
                                coastBoxOnOwGrid.Opacity = 1.0;
                                coastBoxOnOwGrid.IsHitTestVisible = true;
                            }
                            else
                            {
                                coastBoxOnOwGrid.Opacity = 0.0;
                                coastBoxOnOwGrid.IsHitTestVisible = false;
                            }
                        };
                        permanentDecorations.Add((coastBoxOnOwGrid, OMTW - 31.0, 1.0));
                    }
                }
                foreach (var (fe, x, y) in permanentDecorations)
                    canvasAdd(c, fe, x, y);
                var bombSlotActive = false;   // a bomb shop tile that also has marker icons: the bomb is drawn as one of the icon slots instead of big in the middle
                void redrawGridSpot()
                {
                    // cant remove-by-identity because of non-uniques; remake whole canvas
                    owDarkeningMapGridCanvases[i, j].Children.Clear();
                    c.Children.Clear();    // deparents owMapDummyImages.[i,j]
                    // we need a dummy image to make the canvas absorb the mouse interactions, so just re-draw the map at almost 0 opacity
                    canvasAdd(c, owMapDummyImages[i, j], 0.0, 0.0);
                    foreach (var (fe, x, y) in permanentDecorations)
                        canvasAdd(c, fe, x, y);
                    var ms = new MapStateProxy(TrackerModel.overworldMapMarks[i, j].Current());
                    var (shouldAppearLikeDarkX, iconBMP0, extraDecorations) = GetIconBMPAndExtraDecorations(cm, ms, i, j);
                    var bombAsSlot =
                        iconBMP0 != null && ReferenceEquals(iconBMP0, Graphics.theFullTileBmpTable[TrackerModel.MapSquareChoiceDomainHelper.BOMB][0]);
                    bombSlotActive = bombAsSlot;
                    var iconBMP = bombAsSlot ? Graphics.emptyTileBmp : iconBMP0;
                    // be sure to draw in appropriate layer
                    if (iconBMP != null)
                    {
                        if (temporarilyHideAllIconsWhileHoveringEyeball)
                        {
                            var icon = Graphics.BMPtoImage(Graphics.theFullTileBmpTable[TrackerModel.MapSquareChoiceDomainHelper.DARK_X][0]);
                            icon.Opacity = X_OPACITY / 3.0;
                            canvasAdd(owDarkeningMapGridCanvases[i, j], icon, 0.0, 0.0);  // the icon 'is' the darkening
                        }
                        else if (ms.IsX || shouldAppearLikeDarkX)
                        {
                            if (ms.IsX && TrackerModel.getOverworldMapExtraData(i, j, ms.State) == ms.State)
                            {
                                // used when Graphics.CanHideAndReveal()
                                var blankTile = Graphics.BMPtoImage(Graphics.blankTileBmp);
                                canvasAdd(c, blankTile, 0.0, 0.0);
                            }
                            else if (!(isPermEmpty && ms.State == TrackerModel.MapSquareChoiceDomainHelper.DARK_X))   // permanently-empty tiles already show their X in the base layer
                            {
                                var icon = Graphics.BMPtoImage(Graphics.theFullTileBmpTable[TrackerModel.MapSquareChoiceDomainHelper.DARK_X][0]);
                                icon.Opacity = X_OPACITY;
                                canvasAdd(owDarkeningMapGridCanvases[i, j], icon, 0.0, 0.0);  // the icon 'is' the darkening
                            }
                            // Even on a dark-X tile, overlay any stored item icon (right-click feature on AlwaysEmpty tiles)
                            if (TrackerModelOptions.AllowItemIconOnNonShopTile.Value)
                            {
                                var item2 = TrackerModel.getShopItem2_1based(i, j);
                                if (item2 != 0)
                                {
                                    var itemIcon = Graphics.BMPtoImage(Graphics.theFullTileBmpTable[item2 - 1 + TrackerModel.MapSquareChoiceDomainHelper.ARROW][0]);
                                    itemIcon.Opacity = 1.0;
                                    canvasAdd(c, itemIcon, 0.0, 0.0);
                                }
                            }
                        }
                        else
                        {
                            var icon = Graphics.BMPtoImage(iconBMP);
                            icon.Opacity = 1.0;
                            drawDarkening(owDarkeningMapGridCanvases[i, j], 0.0, 0);     // darken below icon and routing marks
                            canvasAdd(c, icon, 0.0, 0.0);                                 // add icon above routing
                            foreach (var (fe, x, y) in extraDecorations)
                                canvasAdd(c, fe, x, y);
                        }
                    }
                    // marker icon (sword / dungeon item) chosen from the menu; drawn on top of the tile
                    var decorList = decorDisplayList((bombAsSlot ? new List<int> { TrackerModel.DECOR_BOMB } : new List<int>()).Concat(visibleDecorIcons(TrackerModel.owSwordIcons[i, j])).ToList()).Select(p => p.Item1).ToList();
                    var shopLabelNow = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]);
                    var hasShopLabel = shopLabelNow > 0;
                    // (a collected family item keeps the tier it was collected at (so you can see where each upgrade came from))
                    System.Drawing.Bitmap tierBmp(int ident)
                    {
                        TrackerModel.progressiveTierOverride = TrackerModel.progressiveTileTier.TryGetValue(Tuple.Create(i, j, ident), out var v) ? v : Math.Max(1, TrackerModel.progressiveFamilyTier(TrackerModel.progressiveFamily(ident)) - 1);
                        try { return decorIconBmp(ident); } finally { TrackerModel.progressiveTierOverride = -1; }
                    }
                    if (shopLabelNow == 5)
                    {
                        // the P shop mirrors the flyout: blue potion, the one item (a ? until something is placed), red potion; only the middle slot can change
                        var big = ms.State == -1 || ms.IsX || bombAsSlot;
                        var entries = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[i, j]);
                        int? mid = decorList.Count > 0 ? decorList[0] : (int?)null;
                        var midCollected = mid is int h ? (entries.Any(e => e.Item1 == h && e.Item2)) : false;
                        for (int k = 0; k <= 2; k++)
                        {
                            var (size, x, y) = decorSlotC(true, bombAsSlot, big, 3, k);
                            if (k == 1 && TrackerModel.LegacyMode) { }   // (Legacy Mode: the potion shop only holds the two potions, so no '?' middle slot)
                            else if (k == 1 && !mid.HasValue)
                            {
                                var q = new TextBlock { Text = "?", Foreground = Brushes.White, FontSize = size, FontWeight = FontWeight.Bold, Width = size, Height = size, TextAlignment = TextAlignment.Center, IsHitTestVisible = false };
                                canvasAdd(c, q, x, y - 3.0);
                            }
                            else
                            {
                                var ident = k == 0 ? TrackerModel.ITEMS.BLUEPOTION : k == 2 ? TrackerModel.ITEMS.REDPOTION : mid.Value;
                                var dbmp =
                                    (k == 1 && midCollected) && TrackerModel.progressiveFamily(ident) > 0
                                        // a collected sword keeps the tier it was collected at (so you can see where each upgrade came from)
                                        ? tierBmp(ident)
                                        : decorIconBmp(ident);
                                if (dbmp != null)
                                {
                                    var img = Graphics.BMPtoImage(dbmp);
                                    img.Stretch = Stretch.Uniform;
                                    img.Width = size;
                                    img.Height = size;
                                    if (k == 1 && midCollected && ident == TrackerModel.ITEMS.OLDKEY) img.Opacity = 0.35;
                                    canvasAdd(c, img, x, y);
                                    if (k == 1 && midCollected && ident != TrackerModel.ITEMS.OLDKEY)
                                    {
                                        var ck = decorCheckmark(size);
                                        canvasAdd(c, ck, x + (size - ck.Width) / 2.0, y + (size - ck.Height) / 2.0);
                                    }
                                }
                            }
                        }
                    }
                    else if (decorList.Count != 0)
                    {
                        var n = decorList.Count;
                        var big = ms.State == -1 || ms.IsX || bombAsSlot;   // a bomb shop's bomb and its other icons are drawn full size, like two icons on an unmarked tile
                        var packedNow = TrackerModel.owSwordIcons[i, j];
                        var allEntries = TrackerModel.decorEntriesOf(packedNow);
                        for (int k = 0; k < decorList.Count; k++)
                        {
                            var ident = decorList[k];
                            // (an icon can repeat, e.g. several heart containers: each copy has its own collected state)
                            var copyNumber = decorList.Take(k).Count(id => id == ident);
                            var sameIdent = allEntries.Where(e => e.Item1 == ident).ToList();
                            var collected = copyNumber < sameIdent.Count && sameIdent[copyNumber].Item2;
                            var justDim = collected && ident == TrackerModel.ITEMS.OLDKEY;   // regular key: just dimmed, collects nothing
                            // a single sword icon on an open tile is drawn at the same size as the sword on its cave tile (15x27), not shrunk into the square marker slot
                            System.Drawing.Bitmap naturalSword =
                                n == 1 && big && !bombAsSlot && !hasShopLabel && !TrackerModel.ProgressiveItems   // (on a "SHOP n" tile the label needs the top of the tile, so the sword is scaled down into the slot below it, like the 4th sword)
                                    ? (ident == TrackerModel.ITEMS.WOODSWORD ? Graphics.ow_sword1_menu_bmp
                                       : ident == TrackerModel.ITEMS.WHITESWORD ? Graphics.ow_sword2_menu_bmp
                                       : ident == TrackerModel.ITEMS.MAGICALSWORD ? Graphics.ow_sword3_menu_bmp
                                       : null)
                                    : null;
                            var dbmp =   // collected items get a little green checkmark (the regular key just dims)
                                collected && TrackerModel.progressiveFamily(ident) > 0
                                    // a collected sword keeps the tier it was collected at (so you can see where each upgrade came from)
                                    ? tierBmp(ident)
                                    : decorIconBmp(ident);
                            if (naturalSword != null)
                            {
                                var sb = naturalSword;
                                var img = Graphics.BMPtoImage(collected ? Graphics.greyscale(sb) : sb);
                                var (sx, sy) = ((OMTW - (double)sb.Width) / 2.0, 3.0);
                                canvasAdd(c, img, sx, sy);
                                if (collected)
                                {
                                    var ck = decorCheckmark(27.0);
                                    canvasAdd(c, ck, sx + ((double)sb.Width - ck.Width) / 2.0, sy + ((double)sb.Height - ck.Height) / 2.0);
                                }
                            }
                            else if (dbmp != null)
                            {
                                var img = Graphics.BMPtoImage(dbmp);
                                img.Stretch = Stretch.Uniform;
                                // 1 icon: as before; 2-3 icons: smaller, side by side so they all fit on the tile
                                var (size, x, y) = decorSlotC(hasShopLabel, bombAsSlot, big, n, k);
                                if (justDim) img.Opacity = 0.35;
                                img.Width = size;
                                img.Height = size;
                                canvasAdd(c, img, x, y);
                                if (shopLabelNow >= 1 && shopLabelNow <= 4 && TrackerModel.cheapKeyShops[shopLabelNow] && isKeyDecor(ident))   // the cheaper key shop's key
                                    canvasAdd(c, new Rectangle { Width = size, Height = size, Stroke = Brushes.Yellow, StrokeThickness = 1.5, IsHitTestVisible = false }, x, y);
                                if (Views.itemIsHintDashed(ident)) canvasAdd(c, Views.makeHintDashedRect(size, size), x, y);
                                if (collected && !justDim)
                                {
                                    var ck = decorCheckmark(size);
                                    canvasAdd(c, ck, x + (size - ck.Width) / 2.0, y + (size - ck.Height) / 2.0);
                                }
                            }
                        }
                    }
                    // the "SHOP n" text label across the top of the tile (plain text: it is not an item icon and never uses up one of the item slots)
                    if (hasShopLabel)
                    {
                        var labelTB = new TextBlock { Text = (shopLabelNow == 5 ? "P SHOP" : $"SHOP {shopLabelNow}"), Foreground = Brushes.Gold, FontSize = 9.0, FontWeight = FontWeight.Bold, TextAlignment = TextAlignment.Center,
                                                      Width = OMTW, Height = 11.0, Background = Brushes.Black, IsHitTestVisible = false };
                        canvasAdd(c, labelTB, 0.0, 0.0);
                    }
                    // the bomb dropper badge: its own layer in the top-left corner of the tile (never an item slot); the icon carries its own red outline
                    if (TrackerModel.owBombDroppers[i, j])
                    {
                        var badge = Graphics.BMPtoImage(bombDropperOutlinedBmp);
                        badge.Stretch = Stretch.Uniform;
                        badge.Width = 12.0;
                        badge.Height = 12.0;
                        badge.IsHitTestVisible = false;
                        canvasAdd(c, badge, 0.0, 0.0);
                    }
                }
                Views.hintDashedItemsChanged += () => redrawGridSpot();
                // the letter / white sword / magical sword cave once the user has picked 'Took ...' there
                bool tookCaveItem(int state) =>
                    (state == TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER || state == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || state == TrackerModel.MapSquareChoiceDomainHelper.SWORD3)
                    && TrackerModel.getOverworldMapExtraData(i, j, state) == state;
                bool isLegalHere(int state) => state == TrackerModel.MapSquareChoiceDomainHelper.ARMOS ? owInstance.HasArmos(i, j) : true;
                void updateGridSpot(int delta, string phrase)
                {
                    async Task go()
                    {
                        // figure out what new state we just interacted-to
                        if (delta == 777)
                        {
                            var curState = TrackerModel.overworldMapMarks[i, j].Current();
                            if (curState == -1)
                            {
                                // if unmarked, use voice to set new state
                                if (speechRecognitionInstance.ConvertSpokenPhraseToMapCell(phrase) is int newState)
                                {
                                    if (isLegalHere(newState) && TrackerModel.overworldMapMarks[i, j].AttemptToSet(newState))
                                    {
                                        if (newState >= 0 && newState <= 8)
                                            selectDungeonTabEvent.Trigger(newState);
                                        Graphics.PlaySoundForSpeechRecognizedAndUsedToMark();
                                        var pos = c.TranslatePoint(new Point(OMTW / 2.0, 11.0 * 3.0 / 2.0), appMainCanvas).Value;
                                        if (overworldAcceleratorTable.TryGetValue(newState, out var f))
                                        {
                                            OverworldMapTileCustomization.acceleratorCancelled = false;
                                            await f(cm, c, i, j);
                                            if (OverworldMapTileCustomization.acceleratorCancelled) TrackerModel.overworldMapMarks[i, j].Set(curState);
                                            Graphics.WarpMouseCursorTo(pos);
                                        }
                                        animateOverworldTileIfOptionIsChecked(i, j);
                                    }
                                }
                            }
                            else if (new MapStateProxy(curState).IsThreeItemShop && TrackerModel.getShopItem2_1based(i, j) == 0)
                            {
                                // if item shop with only one item marked, use voice to set item2
                                if (speechRecognitionInstance.ConvertSpokenPhraseToMapCell(phrase) is int newState)
                                {
                                    if (TrackerModel.MapSquareChoiceDomainHelper.IsItem(newState))
                                    {
                                        TrackerModel.setShopItems(i, j, TrackerModel.MapSquareChoiceDomainHelper.ToItem(newState), 0);
                                        Graphics.PlaySoundForSpeechRecognizedAndUsedToMark();
                                        animateOverworldTileIfOptionIsChecked(i, j);
                                    }
                                }
                            }
                            else if (new MapStateProxy(curState).IsThreeItemShop && TrackerModel.getShopItem3_1based(i, j) == 0)
                            {
                                // if item shop with two items marked, use voice to set item3
                                if (speechRecognitionInstance.ConvertSpokenPhraseToMapCell(phrase) is int newState)
                                {
                                    if (TrackerModel.MapSquareChoiceDomainHelper.IsItem(newState))
                                    {
                                        TrackerModel.setShopItems(i, j, TrackerModel.getShopItem2_1based(i, j), TrackerModel.MapSquareChoiceDomainHelper.ToItem(newState));
                                        Graphics.PlaySoundForSpeechRecognizedAndUsedToMark();
                                        animateOverworldTileIfOptionIsChecked(i, j);
                                    }
                                }
                            }
                        }
                        else if (delta == 0)
                        { }
                        else throw new Exception("bad delta");
                        redrawGridSpot();
                    }
                    CustomComboBoxes.StartImmediate(go());
                }
                owUpdateFunctions[i, j] = updateGridSpot;
                redrawOwTileHook = (x, y) => owUpdateFunctions[x, y](0, null);
                owCanvases[i, j] = c;
                mirrorOverworldFEs.Add(c);
                mirrorOverworldFEs.Add(owDarkeningMapGridCanvases[i, j]);
                async Task SetNewValue(int currentState, int originalState)
                {
                    // picking the same sword / letter cave again while it holds a shuffled item re-opens its choice box (so the icon, or 'took it', can still be placed; the shuffled item is crossed out)
                    var holdsShuffledItem =
                        currentState == originalState &&
                        ((TrackerModel.LegacyMode && currentState == TrackerModel.MapSquareChoiceDomainHelper.SWORD3)   // (Legacy Mode: clicking the magical sword cave again toggles its sword collected)
                         || (currentState == TrackerModel.MapSquareChoiceDomainHelper.SWORD1 && TrackerModel.shuffledWoodSwordTiles.Contains(Tuple.Create(i, j)))
                         || ((currentState == TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER || currentState == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || currentState == TrackerModel.MapSquareChoiceDomainHelper.SWORD3)
                             && TrackerModel.getOverworldMapExtraData(i, j, currentState) == OverworldMapTileCustomization.SHUFFLED_ITEM));
                    if (holdsShuffledItem)
                    {
                        if (overworldAcceleratorTable.TryGetValue(currentState, out var f))
                        {
                            var pos = c.TranslatePoint(new Point(OMTW / 2.0, 11.0 * 3.0 / 2.0), appMainCanvas).Value;
                            OverworldMapTileCustomization.acceleratorCancelled = false;
                            await f(cm, c, i, j);
                            Graphics.WarpMouseCursorTo(pos);
                            redrawGridSpot();
                        }
                    }
                    else if (currentState != originalState)   // only do work if they actually changed anything
                    {
                        if (!(isLegalHere(currentState)))
                            Graphics.ErrorBeepWithReminderLogText("Tried to mark Armos on an overworld tile other than the 5 possible Armos screens");
                        else if (TrackerModel.overworldMapMarks[i, j].AttemptToSet(currentState))
                        {
                            lastTileChange = (i, j, originalState);
                            if (originalState != TrackerModel.MapSquareChoiceDomainHelper.DARK_X && originalState != -1)
                                // remind destructive changes
                                RemindOverworldOverwrites(i, j, originalState, currentState, spokenOWTiles, AsyncBrieflyHighlightAnOverworldLocation(i, j));
                            if (currentState >= 0 && currentState <= 8)
                                selectDungeonTabEvent.Trigger(currentState);
                            if (overworldAcceleratorTable.TryGetValue(currentState, out var f))
                            {
                                var pos = c.TranslatePoint(new Point(OMTW / 2.0, 11.0 * 3.0 / 2.0), appMainCanvas).Value;
                                OverworldMapTileCustomization.acceleratorCancelled = false;
                                await f(cm, c, i, j);
                                if (OverworldMapTileCustomization.acceleratorCancelled) TrackerModel.overworldMapMarks[i, j].Set(originalState);   // dismissed without choosing: put the tile back
                                Graphics.WarpMouseCursorTo(pos);
                            }
                            if (TrackerModel.ProgressiveItems) progressiveSwordCheckHook();   // (a sword taken at the wood sword cave counts once its tile is recorded)
                            redrawGridSpot();
                            if (originalState == -1 && currentState != -1) OverworldRouteDrawing.routeDrawingLayer.GetHighlightTile(i, j).Hide();  // dismiss any green/yellow highlight on this tile
                            animateOverworldTileIfOptionIsChecked(i, j);
                        }
                        else
                            // e.g. they tried to set Level1 when already found elsewhere
                            Graphics.ErrorBeepWithReminderLogText(string.Format("Tried to mark overworld tile {0} as {1} but that is already fully marked elsewhere",
                                                                    $"{(char)((int)'A' + j)}{(TrackerModel.MirrorOverworld ? 16 - i : (i + 1))}",
                                                                    TrackerModel.MapSquareChoiceDomainHelper.AsHotKeyName(currentState)));
                    }
                }
                var GCOL = 9;
                var ST = CustomComboBoxes.borderThickness;
                var gridWidth = (double)(GCOL * (7 * 3 + 2 * (int)ST) + (int)ST) * 1.25;   // cells are 21 wide so the item icons can be shown at full size; the menu is drawn 1.25x bigger (see gridScale in DoModalGridSelect)
                var tileImage = Graphics.BItoImage(owMapBIsRef[i, j]);
                // (a failure while building or running the tile menu must never leave the overworld locked)
                void activatePopup(int activationDelta)
                {
                    try { activatePopupCore(activationDelta); }
                    catch (Exception e) { ClickTrace.Log($"overworld tile menu setup failed, {(char)('A' + j)}{i + 1}"); CustomComboBoxes.GlobalFlag.logPopupFailure($"overworld tile menu setup, {(char)('A' + j)}{i + 1}", e); }
                }
                void activatePopupCore(int activationDelta)
                {
                    // Recompute GROW/GCOUNT live on each popup call so they always match
                    // gridElementsSelectablesAndIDs, which is also built live from the current option value.
                    // Capturing these at tile-init time causes a length mismatch crash if the option
                    // is toggled between init and the next right-click.
                    popupIsActive = true;
                    var tileCanvas = new Canvas { Width = OMTW, Height = 11.0 * 3.0 };   // has to be fresh object for each popup
                    var pos = c.TranslatePoint(new Point(), appMainCanvas).Value;
                    double gridxPosition;
                    if (pos.X < OMTW * 2.0)
                        gridxPosition = -ST; // left align
                    else if (pos.X > OMTW * 13.0)
                        gridxPosition = OMTW - gridWidth;  // right align
                    else
                    {
                        var r0 = (int)((OMTW - gridWidth) / 2.0);  // center align
                        var r = (double)(r0 - r0 % 3);  // ensure at a multiple-of-3 pixel, to look good at 2/3 size
                        // keep the whole menu inside the window (the centered menu used to run off the left edge at column 3 and off the right edge at column 14)
                        var windowW = appMainCanvas.Bounds.Width > 0.0 ? appMainCanvas.Bounds.Width : 16.0 * OMTW;   // (WPF: ActualWidth)
                        r = Math.Min(r, (windowW - gridWidth - pos.X));
                        gridxPosition = Math.Max(r, (-pos.X));
                    }
                    var originalState = TrackerModel.overworldMapMarks[i, j].Current();
                    (Control, bool, int) typicalGESAI(int n)
                    {
                        var isSwordTile = (n == TrackerModel.MapSquareChoiceDomainHelper.SWORD1 || n == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || n == TrackerModel.MapSquareChoiceDomainHelper.SWORD3);
                        // a sword cave that is already placed elsewhere (e.g. holding a shuffled item) can still be chosen here: it places just that sword's marker icon
                        var isSelectable = ((n == originalState) || isSwordTile || TrackerModel.mapSquareChoiceDomain.CanAddUse(n)) && isLegalHere(n) && (n != TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP || canPlacePotionShop(i, j));
                        // the sword cave icons are shown in the menu without their brown background, with white numbers
                        var tileBmp =
                            isSwordTile && TrackerModelOptions.UseOldSwordIcons.Value ? (n == TrackerModel.MapSquareChoiceDomainHelper.SWORD1 ? Graphics.ow_sword1_menu_bmp : n == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 ? Graphics.ow_sword2_menu_bmp : Graphics.ow_sword3_menu_bmp)   // (Use old sword icons: the plain sword pictures)
                            : isSwordTile ? Graphics.swordMenuBmp(new MapStateProxy(n).DefaultInteriorBmp())
                            : n == TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP ? OverworldMapTileCustomization.hintShopMenuBmp(OverworldMapTileCustomization.lastHintShopColor)   // the hint shop icon shows the color the user last chose
                            : new MapStateProxy(n).DefaultInteriorBmp();
                        var img = Graphics.BMPtoImage(tileBmp);   // fresh image each popup; cell is wider than the 15px icon, so center it
                        var cell = new Canvas { Width = 7.0 * 3.0, Height = 9.0 * 3.0 };
                        canvasAdd(cell, img, 3.0, 0.0);
                        return (cell, isSelectable, n);
                    }
                    (Control, bool, int) hole = (null, false, -999);   // null asks selector to 'leave a hole' here
                    var M = TrackerModel.MapSquareChoiceDomainHelper.DARK_X;
#pragma warning disable CS0219 // (unused, as in WPF)
                    (Control, bool, int) _darkXCell = (new Canvas { Width = 7.0 * 3.0, Height = 9.0 * 3.0, Background = Graphics.overworldCommonestFloorColorDarkBrush }, true, M);
                    (Control, bool, int) _unmarkedCell = (new Canvas { Width = 7.0 * 3.0, Height = 9.0 * 3.0, Background = Graphics.overworldCommonestFloorColorBrush }, true, -1);
#pragma warning restore CS0219
                    // decorative marker icons (ids are offset by 1000 so they cannot be confused with real overworld tile states)
                    // a Take Any cave, or a letter / white sword / magical sword cave, only gets the bomb dropper marker (middle click); other icons only go on a tile where the sword / letter icon was picked instead (that tile is no longer a cave)
                    // (a letter / white / magical sword cave that already holds a shuffled item can take icons: the choice box just crosses out its 'Shuffled Item' option)
                    var holdsShuffled = (originalState == TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER || originalState == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || originalState == TrackerModel.MapSquareChoiceDomainHelper.SWORD3) && TrackerModel.getOverworldMapExtraData(i, j, originalState) == OverworldMapTileCustomization.SHUFFLED_ITEM;
                    var markersBlocked = !holdsShuffled && ((originalState == TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY) || originalState == TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER || originalState == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || originalState == TrackerModel.MapSquareChoiceDomainHelper.SWORD3 || isDungeonOrAnyRoad(originalState));   // (a dungeon entrance or any road tile only gets the bomb dropper marker too)
                    (Control, bool, int) swordIconCell(int ident)
                    {
                        if (ident == SHOP_MENU)
                        {
                            // the 'Shop' cell: diagonal SHOP lettering; picking it opens the flyout (potion shop / shop 1-5)
                            var img = Graphics.BMPtoImage(OverworldMapTileCustomization.shopMenuBmp);
                            img.Stretch = Stretch.Uniform;
                            img.Width = 21.0;
                            img.Height = 27.0;
                            RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None);
                            var cell = new Canvas { Width = 7.0 * 3.0, Height = 9.0 * 3.0 };
                            canvasAdd(cell, img, 0.0, 0.0);
                            return (cell, (!markersBlocked), SHOP_MENU);
                        }
                        else
                        {
                            // the swords are 15x27 cave-style icons; the dungeon items are shown at full 21x21 size (without the black outline, which is only for the map)
                            var isSword = (ident == TrackerModel.ITEMS.PROGRESSIVESWORD || ident == TrackerModel.ITEMS.WHITESWORD4);
                            var progSword = TrackerModel.ProgressiveItems && TrackerModel.progressiveSwordBoxTier(ident) > 0 && !(ident == TrackerModel.ITEMS.WHITESWORD4 && TrackerModelOptions.UseOldSwordIcons.Value);   // Progressive Items: the sword cells show the current P tier sprite (21x27) (Use old sword icons: the 4th sword cell keeps its checkered sword, greyed out when disabled)
                            int tierIndex() { var t = TrackerModel.progressiveSwordBoxTier(ident); return t >= 1 && t <= 4 ? t : 0; }
                            var bmp = progSword ? Graphics.ow_progressive_tier_menu_bmps[tierIndex()] : isSword && ident == TrackerModel.ITEMS.WHITESWORD4 && TrackerModelOptions.UseOldSwordIcons.Value ? Graphics.ow_sword4_menu_bmp : isSword ? Graphics.swordMenuBmp(decorIconBmp(ident)) : TrackerModel.progressiveDisabledIdent(ident) && TrackerModel.progressiveFamily(ident) >= 2 ? CustomComboBoxes.boxCurrentBMP(ident, null) : decorSourceBmp(ident);   // (a disabled red ring / candle / silver arrow keeps its own picture)
                            var img = Graphics.BMPtoImage(bmp);
                            img.Stretch = Stretch.Uniform;
                            img.Width = (progSword ? 21.0 : isSword ? 15.0 : 21.0);
                            img.Height = (progSword || isSword ? 27.0 : 21.0);
                            var cell = new Canvas { Width = 7.0 * 3.0, Height = 9.0 * 3.0 };
                            canvasAdd(cell, img, (isSword && !progSword ? 3.0 : 0.0), (isSword ? 0.0 : 3.0));
                            var progDisabled = TrackerModel.progressiveDisabledIdent(ident);
                            if (progDisabled)
                                cell.Background = Brushes.Transparent;
                            return (cell, (!markersBlocked && !progDisabled && !(ident == TrackerModel.ITEMS.WHITESWORD4 && !TrackerModelOptions.FourthSwordEnabled)), 1000 + ident);   // the 4th sword icon is disabled unless the 4th Sword start option is on
                        }
                    }
                    // 9 columns x 6 rows
                    var topRows0 = new List<(Control, bool, int)>();
                    // row 1: all 9 dungeons
                    for (int n = 0; n <= 8; n++)
                        topRows0.Add(typicalGESAI(n));
                    // row 2: 4 any roads, then the 5 swords starting with wood
                    for (int n = 9; n <= 12; n++)
                        topRows0.Add(typicalGESAI(n));
                    topRows0.Add(typicalGESAI(TrackerModel.MapSquareChoiceDomainHelper.SWORD1));
                    topRows0.Add(typicalGESAI(TrackerModel.MapSquareChoiceDomainHelper.SWORD2));
                    topRows0.Add(typicalGESAI(TrackerModel.MapSquareChoiceDomainHelper.SWORD3));
                    topRows0.Add(swordIconCell(TrackerModel.ITEMS.WHITESWORD4));
                    topRows0.Add(swordIconCell(TrackerModel.ITEMS.PROGRESSIVESWORD));
                    var topRows = topRows0.ToArray();
                    var markerIdentList = new List<int> {
                        TrackerModel.ITEMS.LADDER, TrackerModel.ITEMS.POWERBRACELET, TrackerModel.ITEMS.RECORDER, TrackerModel.ITEMS.RAFT, TrackerModel.ITEMS.BOW,
                        TrackerModel.ITEMS.BLUERING, TrackerModel.ITEMS.REDRING, TrackerModel.ITEMS.WOODARROW, TrackerModel.ITEMS.SILVERARROW, TrackerModel.ITEMS.WAND,
                        TrackerModel.ITEMS.KEY, TrackerModel.ITEMS.BLUECANDLE, TrackerModel.ITEMS.REDCANDLE, TrackerModel.ITEMS.BOOMERANG, TrackerModel.ITEMS.MAGICBOOMERANG,
                        TrackerModel.ITEMS.BOOKSHIELD, TrackerModel.ITEMS.SHIELD, TrackerModel.ITEMS.MEAT, TrackerModel.DECOR_BOMB, TrackerModel.ITEMS.OLDKEY,
                        SHOP_MENU, TrackerModel.ITEMS.LETTER, TrackerModel.ITEMS.HEARTCONTAINER, TrackerModel.ITEMS.BLUEPOTION, TrackerModel.ITEMS.BOMBUPGRADE };
                    // the bottom row of the menu is always: shop, bomb, key, letter, heart container, blue potion, bomb upgrade, clear
                    var lastRowIdents = new List<int> { SHOP_MENU, TrackerModel.DECOR_BOMB, TrackerModel.ITEMS.OLDKEY, TrackerModel.ITEMS.LETTER, TrackerModel.ITEMS.HEARTCONTAINER, TrackerModel.ITEMS.BLUEPOTION, TrackerModel.ITEMS.BOMBUPGRADE };
                    var bottomRows0 = new List<(Control, bool, int)>();
                    // row 3: unknown secret '?' first, then secrets, door repair, money making game, armos, take any, and the purple '?' hint shop at the end
                    bottomRows0.Add(typicalGESAI(TrackerModel.MapSquareChoiceDomainHelper.UNKNOWN_SECRET));
                    for (int n = 25; n <= 29; n++)
                        bottomRows0.Add(typicalGESAI(n));
                    bottomRows0.Add(typicalGESAI(TrackerModel.MapSquareChoiceDomainHelper.ARMOS));
                    bottomRows0.Add(typicalGESAI(TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY));
                    bottomRows0.Add(typicalGESAI(TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP));
                    // rows 4-6: dungeon items (decorative icons, in item-menu order from the ladder to the heart container), then the personal preference markers, Don't Care and Unmarked
                    foreach (var ident in markerIdentList)
                        if (!(lastRowIdents.Contains(ident)))
                            bottomRows0.Add(swordIconCell(ident));
                    // the user's custom icons (if that option is on) go at the bottom
                    for (int k = 0; k <= TrackerModel.ITEMS.customMenuCount - 1; k++)
                        bottomRows0.Add(swordIconCell(TrackerModel.ITEMS.CUSTOM_BASE + k));
                    var bottomRows = bottomRows0.ToArray();
                    // Don't Care and Unmarked always end the grid: holes pad the gap so they are the last two cells of the last row
                    (Control, bool, int) make_clearIconsCell()
                    {
                        var cell = new Canvas { Width = 7.0 * 3.0, Height = 9.0 * 3.0 };
                        foreach (var (x1, y1, x2, y2) in new[] { (4.0, 7.0, 17.0, 20.0), (17.0, 7.0, 4.0, 20.0) })
                            cell.Children.Add(new Line { StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2), Stroke = Brushes.Red, StrokeThickness = 3.5, StrokeLineCap = PenLineCap.Round });   // (WPF: StrokeStartLineCap/StrokeEndLineCap)
                        return (cell, true, CLEAR_ICONS);
                    }
                    var clearIconsCell = make_clearIconsCell();
                    var tailCells = lastRowIdents.Select(swordIconCell).Append(clearIconsCell).Append(_darkXCell).ToArray();   // (Don't Care is the last cell, at the far right of the bottom row)
                    // 'Shops in menu' (a checkbox in the shop window): a row of buttons for the 5 shops (P, 1-4), starting on a new row; they work like picking that shop in the flyout
                    (Control, bool, int)[] shopRowCells;
                    if (!TrackerModelOptions.ShopsInMenu.Value) shopRowCells = new (Control, bool, int)[0];
                    else
                    {
                        var l = new List<(Control, bool, int)>();
                        for (int n = 0; n <= 4; n++)
                        {
                            var lbl = n == 0 ? 5 : n;
                            var cell = new Canvas { Width = 7.0 * 3.0, Height = 9.0 * 3.0, Background = Brushes.Transparent };
                            var tb = new TextBlock { Text = (n == 0 ? "P" : n.ToString()), Foreground = Brushes.Orange, FontSize = 22.0, FontWeight = FontWeight.Bold, Width = 7.0 * 3.0, TextAlignment = TextAlignment.Center, IsHitTestVisible = false };
                            canvasAdd(cell, tb, 0.0, 0.0);
                            l.Add((cell, (!markersBlocked && (n != 0 || canPlacePotionShop(i, j))), SHOP_LABEL_BASE + lbl));
                        }
                        shopRowCells = l.ToArray();
                    }
                    (Control, bool, int)[] make_bodyCells()
                    {
                        var b0 = topRows.Concat(bottomRows).ToArray();
                        if (shopRowCells.Length == 0) return b0;
                        else return b0.Concat(Enumerable.Repeat(hole, (GCOL - b0.Length % GCOL) % GCOL)).Concat(shopRowCells).ToArray();
                    }
                    (Control, bool, int)[] make_bodyCellsWithApRow()
                    {
                        // the AP Item (an Archipelago multiworld item that belongs to another world) starts a row of its own after the other icons; a tile holds up to 3 icons
                        var b1 = make_bodyCells();
                        if (!TrackerModelOptions.ArchipelagoSupport.Value) return b1;   // (the option is off: no AP Item row)
                        var ap = swordIconCell(TrackerModel.ITEMS.APITEM);
                        return b1.Concat(Enumerable.Repeat(hole, (GCOL - b1.Length % GCOL) % GCOL)).Append(ap).ToArray();
                    }
                    var bodyCells = make_bodyCellsWithApRow();
                    var paddedCount = ((bodyCells.Length + tailCells.Length + GCOL - 1) / GCOL) * GCOL;
                    var GROW = paddedCount / GCOL;
                    var GCOUNT = GCOL * GROW;
                    var gridElementsSelectablesAndIDs = bodyCells.Concat(Enumerable.Repeat(hole, paddedCount - bodyCells.Length - tailCells.Length)).Concat(tailCells).ToArray();
                    // the tile's current state might not be offered in this menu (e.g. a potion shop / letter / other state set by a hotkey or an older save);
                    // fall back to the 'Unmarked' cell rather than crashing
                    var originalStateIndex0 = Array.FindIndex(gridElementsSelectablesAndIDs, t => t.Item3 == originalState);
                    var originalStateIndex =
                        originalStateIndex0 >= 0 ? originalStateIndex0
                        : Array.FindIndex(gridElementsSelectablesAndIDs, t => t.Item3 == CLEAR_ICONS);   // (no 'Unmarked' cell: the red X does the same)
                    if (gridElementsSelectablesAndIDs.Length != GCOUNT)
                        throw new Exception("bad ow grid tile layout");
                    async Task go()
                    {
                        int? r0;
                        if (openShopFlyoutDirectly)
                        {
                            openShopFlyoutDirectly = false;
                            r0 = SHOP_MENU;
                        }
                        else
                        {
                            var res = await CustomComboBoxes.DoModalGridSelect<int, int>(cm, pos.X, pos.Y, tileCanvas,
                                gridElementsSelectablesAndIDs, originalStateIndex, activationDelta, (GCOL, GROW, 7 * 3, 9 * 3), (double)(7 * 3) / 2.0, (double)(9 * 3) / 2.0, gridxPosition, 11.0 * 3.0 + ST,
                                (currentState) =>
                                {
                                    tileCanvas.Children.Clear();
                                    canvasAdd(tileCanvas, tileImage, 0.0, 0.0);
                                    var isClearCell = (currentState == CLEAR_ICONS);
                                    var isShopLabelCell = (currentState > SHOP_LABEL_BASE && currentState <= SHOP_LABEL_BASE + 5);
                                    var isShopCell = (currentState == SHOP_MENU) || isShopLabelCell;
                                    var isSwordIcon = currentState >= 1000 && !isClearCell && !isShopCell;
                                    if (isSwordIcon)
                                    {
                                        var pbmp = decorIconBmp(currentState - 1000);
                                        if (pbmp != null)
                                        {
                                            var icon = Graphics.BMPtoImage(pbmp);
                                            icon.Stretch = Stretch.Uniform;
                                            icon.Width = 27.0;
                                            icon.Height = 27.0;
                                            canvasAdd(tileCanvas, icon, OMTW / 2.0 - 13.5, 3.0);
                                        }
                                    }
                                    else if (isClearCell || isShopCell) { }
                                    else
                                    {
                                        var (_, bmp, _) = GetIconBMPAndExtraDecorations(cm, new MapStateProxy(currentState), i, j);
                                        if (bmp != null)
                                        {
                                            var icon = Graphics.BMPtoImage(bmp);
                                            if (new MapStateProxy(currentState).IsX)
                                                icon.Opacity = X_OPACITY;
                                            canvasAdd(tileCanvas, icon, 0.0, 0.0);
                                        }
                                    }
                                    var s =
                                        isSwordIcon ? decorDescription(currentState - 1000) + (currentState - 1000 == TrackerModel.ITEMS.WHITESWORD4 && !TrackerModelOptions.FourthSwordEnabled ? "\n4th Sword Not Enabled" : "") + (TrackerModel.progressiveDisabledIdent(currentState - 1000) ? "\nDisabled Due to Progressive Items" : "")
                                        : isClearCell ? "Clear this tile\n(removes marker icons and sets it back to unmarked)"
                                        : isShopLabelCell ? (currentState == SHOP_LABEL_BASE + 5 ? "P Shop\n(potion shop: puts its name at the top of the tile and copies any other P shop)" : string.Format("Shop {0}\n(puts the shop's name at the top of the tile and copies its items from any other Shop {1})", (currentState - SHOP_LABEL_BASE), (currentState - SHOP_LABEL_BASE)))
                                        : isShopCell ? "Shop...\n(potion shop, or shop 1-4: puts the shop's name at the top of the tile)"
                                        : currentState == -1 ? "Unmarked"
                                        : TrackerModel.dummyOverworldTiles[currentState].Item4;
                                    s = isSwordIcon || isClearCell || isShopCell ? s : HotKeys.OverworldHotKeyProcessor.AppendHotKeyToDescription(s, currentState);
                                    s = currentState >= TrackerModel.MapSquareChoiceDomainHelper.WARP_1 && currentState <= TrackerModel.MapSquareChoiceDomainHelper.WARP_4 ? s + " - Right click to place Vanilla" : s;
                                    // the description box sits just above the menu and is exactly as wide as it (the menu can be resized, so follow its current size); the text wraps and scales with the width
                                    var (menuX, menuY, menuW, menuH) = CustomComboBoxes.MovableGridRect;
                                    var boxW = menuW > 0.0 ? menuW : gridWidth;
                                    var fs = Math.Max(9.0, Math.Min(24.0, (16.0 * boxW / gridWidth)));
                                    // (wrapped read-only text: a SelectableTextBlock, as Avalonia's TextBox mis-measures wrapped text)
                                    var text = new SelectableTextBlock { Text = s, Foreground = Brushes.Orange, Background = Brushes.Black, IsHitTestVisible = false,
                                                                FontSize = fs, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
                                    Border textBorder;
                                    if (currentState >= TrackerModel.MapSquareChoiceDomainHelper.SHOP && currentState < TrackerModel.MapSquareChoiceDomainHelper.SHOP + TrackerModel.MapSquareChoiceDomainHelper.NUM_ITEMS)
                                    {
                                        var extra = new SelectableTextBlock { Text = "After marking a shop, left-click\nit again to mark a second item",
                                                                    Foreground = Brushes.Orange, Background = Brushes.Black, IsHitTestVisible = false,
                                                                    FontSize = 10.0, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
                                        var sp = new StackPanel { Orientation = Orientation.Vertical };
                                        sp.Children.Add(text);
                                        sp.Children.Add(new DockPanel { Background = Brushes.Gray, Height = 1.0, Margin = new Thickness(12.0, 0.0, 12.0, 0.0) });
                                        sp.Children.Add(extra);
                                        textBorder = new Border { BorderThickness = new Thickness(ST), Child = sp, Background = Brushes.Black, BorderBrush = Brushes.Gray };
                                    }
                                    else
                                        textBorder = new Border { BorderThickness = new Thickness(ST), Child = text, Background = Brushes.Black, BorderBrush = Brushes.Gray };
                                    textBorder.Width = boxW;
                                    textBorder.Measure(new Size(boxW, double.PositiveInfinity));
                                    var boxLeft = menuW > 0.0 ? menuX : gridxPosition;
                                    var boxTop = menuW > 0.0 ? menuY + menuH + 4.0 : (11.0 * 3.0 + ST) - textBorder.DesiredSize.Height - 4.0;   // under the menu (below the tip box)
                                    Canvas.SetLeft(textBorder, boxLeft);
                                    Canvas.SetTop(textBorder, boxTop);
                                    tileCanvas.Children.Add(textBorder);
                                },
                                (ea, currentState) =>
                                {
                                    if (ea.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed && currentState >= TrackerModel.MapSquareChoiceDomainHelper.WARP_1 && currentState <= TrackerModel.MapSquareChoiceDomainHelper.WARP_4)
                                        return new CustomComboBoxes.PopupClickBehavior<int>.DismissPopupWithResult(ANYROAD_MENU);
                                    else return new CustomComboBoxes.PopupClickBehavior<int>.DismissPopupWithResult(currentState);
                                },
                                new List<(Control, double, double)>(), CustomComboBoxes.ModalGridSelectBrushes.Defaults(), new CustomComboBoxes.GridClickDismissalWarpReturn.WarpToCenter(), null, "OWGridTile", null);
                            r0 = res.hasResult ? res.result : (int?)null;
                        }
                        var reopenMenu = false;
                        var flyoutAddedItem = false;   // set when items were added to a shop from inside the flyout (so clicking that shop afterwards keeps its label instead of toggling it off)
                        List<(System.Drawing.Bitmap, bool)> shopItemBitmaps(int n) => shopItemBitmapsGlobal(n);
                        // placing another 'Shop n' starts it with the same items (and collected state) as an existing 'Shop n' tile, and links the two like a copy / paste would
                        bool autoDupShop(int n)
                        {
                            if (TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).IsEmpty)
                            {
                                var src = new List<(int, int)>();
                                for (int x = 0; x <= 15; x++)
                                    for (int y = 0; y <= 7; y++)
                                        if ((x, y) != (i, j) && TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[x, y]) == n && !(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[x, y]).IsEmpty)) src.Add((x, y));
                                if (src.Count == 0) return false;
                                else
                                {
                                    var source = src[0];
                                    setDecorIcons(i, j, TrackerModel.owSwordIcons[source.Item1, source.Item2]);   // (the packed value carries the shop label too)
                                    copyTileTiers(source, (i, j));
                                    if (dupLinks.TryGetValue(source, out var l))
                                    {
                                        if (!(l.Contains((i, j)))) l.Add((i, j));
                                    }
                                    else
                                    {
                                        var nl = new List<(int, int)>();
                                        nl.Add((i, j));
                                        dupLinks[source] = nl;
                                    }
                                    return TrackerModel.overworldMapMarks[source.Item1, source.Item2].Current() == TrackerModel.MapSquareChoiceDomainHelper.BOMB;   // true if the source is a bomb shop
                                }
                            }
                            else return false;
                        }
                        // the 'Shop' cell opens a flyout: the potion shop (the usual blue potion marker) or one of the 4 numbered shops
                        async Task<int?> flyout()
                        {
                            if (r0 is int sel && sel == SHOP_MENU)
                            {
                                flyoutAddedItem = false;
                                var counts = Enumerable.Range(0, 5).Select(n => TrackerModel.shopLabelCount(n == 0 ? 5 : n)).ToArray();
                                var shopItems = Enumerable.Range(0, 5).Select(shopItemBitmaps).ToArray();
                                // an item hotkey over a shop: this tile becomes that shop (if it isn't already) and the item goes in it, while the flyout stays open
                                (int[], List<(System.Drawing.Bitmap, bool)>[]) addItemToShopImpl(int shop, int ident)
                                {
                                    // (the flyout's shop 0 is the P shop, shop label 5)
                                    flyoutAddedItem = true;
                                    var lbl = shop == 0 ? 5 : shop;
                                    var curLabel = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]);
                                    bool hasIdent(int x, int y) => TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[x, y]).Any(e => e.Item1 == ident);
                                    List<(int, int)> tilesWhere(Func<int, int, bool> p)
                                    {
                                        var l = new List<(int, int)>();
                                        for (int x = 0; x <= 15; x++) for (int y = 0; y <= 7; y++) if (p(x, y)) l.Add((x, y));
                                        return l;
                                    }
                                    // the tile the item goes on: this one if it is that shop (or has no shop yet); otherwise another tile of that shop, or if there is none this tile changes into it
                                    (int, int) target;
                                    if (curLabel == lbl || curLabel == 0) target = (i, j);
                                    else
                                    {
                                        var ts = tilesWhere((x, y) => TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[x, y]) == lbl);
                                        target = ts.Count > 0 ? ts[0] : (i, j);
                                    }
                                    var (tx, ty) = target;
                                    // an item already in a different shop moves: it is taken out of that shop first (shops with the same label are synced, so one tile does them all)
                                    void removeIdent((int, int) t)
                                    {
                                        var (x, y) = t;
                                        var p0 = TrackerModel.owSwordIcons[x, y];
                                        var l0 = TrackerModel.shopLabelOf(p0);
                                        var es = TrackerModel.decorEntriesOf(p0).ToList();
                                        var k = es.FindIndex(e => e.Item1 == ident);
                                        if (k < 0) throw new KeyNotFoundException();   // (F#'s List.findIndex)
                                        var es2 = es.Where((_, n) => n != k);
                                        var packed = TrackerModel.decorEntriesPack(ListModule.OfSeq(es2));
                                        setDecorIcons(x, y, (l0 > 0 ? TrackerModel.withShopLabel(packed, l0) : packed));
                                        redrawOwTileHook(x, y);
                                    }
                                    for (int m = 1; m <= 5; m++)
                                    {
#pragma warning disable CS0162 // (unreachable, as in WPF's `if false`)
                                        if (false && m != lbl)   // (an item can now be in several shops at once, so adding it to one shop no longer takes it out of another)   // (potions can be sold in several shops, so a blue potion is never moved out of another shop; with Progressive Items neither are swords, rings, candles and arrows)
                                        {
                                            var ts = tilesWhere((x, y) => TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[x, y]) == m && hasIdent(x, y));
                                            if (ts.Count > 0) removeIdent(ts[0]);
                                        }
#pragma warning restore CS0162
                                    }
                                    if (TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[tx, ty]) != lbl)
                                    {
                                        // a tile becoming this shop: take on what that shop already holds (otherwise this tile's old items would overwrite it on every tile of that shop)
                                        var others = tilesWhere((x, y) => (x, y) != (tx, ty) && TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[x, y]) == lbl).Select(t => TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[t.Item1, t.Item2])).ToList();
                                        setShopLabel(tx, ty, lbl);
                                        if (others.Count > 0) setDecorIcons(tx, ty, TrackerModel.withShopLabel(TrackerModel.decorEntriesPack(others[0]), lbl));
                                    }
                                    var before = TrackerModel.owSwordIcons[tx, ty];
                                    if (lbl == 5)
                                    {
                                        // the P shop has just the one slot: the new item replaces what was there (the same item again removes it)
                                        var already = TrackerModel.decorEntriesOf(before).Any(e => e.Item1 == ident);
                                        setDecorIcons(tx, ty, TrackerModel.withShopLabel(TrackerModel.decorEntriesPack(already ? FSharpList<Tuple<int, bool>>.Empty : ListModule.OfSeq(new[] { Tuple.Create(ident, false) })), lbl));
                                    }
                                    else
                                        setDecorIcons(tx, ty, TrackerModel.decorIconsToggle(before, ident));
                                    if (TrackerModel.owSwordIcons[tx, ty] == before)
                                        Graphics.ErrorBeepWithReminderLogText($"A tile can show at most {TrackerModel.MaxDecorIcons} marker icons; ctrl+left click one of its icons to remove it first");
                                    if ((tx, ty) == (i, j) && !(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[tx, ty]).IsEmpty) && !(owInstance.AlwaysEmpty(tx, ty)) && TrackerModel.overworldMapMarks[tx, ty].Current() == -1)
                                        CustomComboBoxes.StartImmediate(SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.DARK_X, -1));
                                    redrawOwTileHook(tx, ty);
                                    redrawGridSpot();
                                    return (Enumerable.Range(0, 5).Select(n => TrackerModel.shopLabelCount(n == 0 ? 5 : n)).ToArray(), Enumerable.Range(0, 5).Select(shopItemBitmaps).ToArray());
                                }
                                (int[], List<(System.Drawing.Bitmap, bool)>[]) addItemToShop(int shop, int ident0)
                                {
                                    // Progressive Items: any sword put in a shop is just the progressive sword
                                    // (except a sword already in the shop: then this is a remove (the add toggles it off), and it must stay that sword)
                                    var alreadyInShop = shopItemIdentsGlobal(shop).Contains(ident0);
                                    var ident = !alreadyInShop && TrackerModel.ProgressiveItems && (ident0 == TrackerModel.ITEMS.WOODSWORD || ident0 == TrackerModel.ITEMS.WHITESWORD || ident0 == TrackerModel.ITEMS.MAGICALSWORD || ident0 == TrackerModel.ITEMS.WHITESWORD4) ? TrackerModel.ITEMS.PROGRESSIVESWORD : ident0;
                                    var lbl = shop == 0 ? 5 : shop;
                                    var curLabel = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]);
                                    var targetHasTile = false;
                                    for (int x = 0; x <= 15; x++) for (int y = 0; y <= 7; y++) if (TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[x, y]) == lbl) targetHasTile = true;
                                    if (curLabel != lbl && !targetHasTile)
                                    {
                                        // that shop is not on the map yet: the items wait in the shop window until a tile becomes that shop
#pragma warning disable CS0162 // (unreachable, as in WPF's `if false`)
                                        if (false)   // (items can be in several shops at once: nothing is taken out of the other shops)
                                        {
                                            for (int m = 1; m <= 5; m++)
                                            {
                                                if (m != lbl)
                                                {
                                                    var found = new List<(int, int)>();
                                                    for (int x = 0; x <= 15; x++) for (int y = 0; y <= 7; y++) if (TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[x, y]) == m && (TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[x, y]).Contains(ident))) found.Add((x, y));
                                                    if (found.Count > 0)
                                                    {
                                                        var (x, y) = found[0];
                                                        var es = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[x, y]).Where(e => e.Item1 != ident);
                                                        setDecorIcons(x, y, TrackerModel.withShopLabel(TrackerModel.decorEntriesPack(ListModule.OfSeq(es)), m));
                                                        redrawOwTileHook(x, y);
                                                    }
                                                    else
                                                    {
                                                        var es = TrackerModel.decorEntriesOf(TrackerModel.shopStash[m]);
                                                        if (es.Any(e => e.Item1 == ident))
                                                            TrackerModel.shopStash[m] = TrackerModel.decorEntriesPack(ListModule.OfSeq(es.Where(e => e.Item1 != ident)));
                                                    }
                                                }
                                            }
                                        }
#pragma warning restore CS0162
                                        var stashBefore = TrackerModel.shopStash[lbl];
                                        if (lbl == 5)
                                        {
                                            var already = TrackerModel.decorEntriesOf(stashBefore).Any(e => e.Item1 == ident);
                                            TrackerModel.shopStash[lbl] = TrackerModel.decorEntriesPack(already ? FSharpList<Tuple<int, bool>>.Empty : ListModule.OfSeq(new[] { Tuple.Create(ident, false) }));
                                        }
                                        else
                                        {
                                            TrackerModel.shopStash[lbl] = TrackerModel.decorIconsToggle(stashBefore, ident);
                                            if (TrackerModel.shopStash[lbl] == stashBefore)
                                                Graphics.ErrorBeepWithReminderLogText($"A shop can show at most {TrackerModel.MaxDecorIcons} items");
                                        }
                                        decorStateChanged();
                                        return (Enumerable.Range(0, 5).Select(n => TrackerModel.shopLabelCount(n == 0 ? 5 : n)).ToArray(), Enumerable.Range(0, 5).Select(shopItemBitmaps).ToArray());
                                    }
                                    else return addItemToShopImpl(shop, ident);
                                }
                                // the marker icons offered when right clicking a shop in the flyout (the same icons as the overworld menu, minus the Shop cell)
                                Task<int> pickItemForShop()
                                {
                                    var idents = new List<int> { TrackerModel.ITEMS.WOODSWORD, TrackerModel.ITEMS.WHITESWORD, TrackerModel.ITEMS.MAGICALSWORD, TrackerModel.ITEMS.WHITESWORD4, TrackerModel.ITEMS.PROGRESSIVESWORD }
                                                    .Concat(markerIdentList.Where(ident => ident != SHOP_MENU))
                                                    .Concat(Enumerable.Range(0, TrackerModel.ITEMS.customMenuCount).Select(k => TrackerModel.ITEMS.CUSTOM_BASE + k))
                                                    .Concat(TrackerModelOptions.ArchipelagoSupport.Value ? new[] { TrackerModel.ITEMS.APITEM } : new int[0]).ToList();
                                    bool isProgressiveDisabled(int ident) => TrackerModel.progressiveDisabledIdent(ident);
                                    // Legacy Mode: a shop only holds a wood arrow, blue candle, regular key, bombs, meat, book or shield (so no swords either)
                                    bool isLegacyDisabled(int ident) => TrackerModel.LegacyMode && !(PieMenus.legacyShopAllows(ident));
                                    var items = new List<(int, Control)>();
                                    foreach (var ident in idents)
                                    {
                                        var (cell, enabled, _) = swordIconCell(ident);
                                        if (enabled || isProgressiveDisabled(ident) || isLegacyDisabled(ident)) items.Add((ident, cell));
                                    }
                                    PieMenus.itemPickerDisabledTip = ident => isLegacyDisabled(ident) ? "Disabled In Legacy Mode" : isProgressiveDisabled(ident) ? "Disabled Due to Progressive Items" : null;
                                    return PieMenus.ItemPickerAsync(cm, items, 9);
                                }
                                var pick = await PieMenus.ShopFlyoutAsync(cm, counts, TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]), shopItems, canPlacePotionShop(i, j), addItemToShop, pickItemForShop);
                                if (pick == 0) return SHOP_LABEL_BASE + 5;   // the P shop is shop label 5
                                else if (pick >= 1 && pick <= 4) return SHOP_LABEL_BASE + pick;
                                else if (pick >= 1000) return pick;   // an item hotkey pressed in the flyout
                                else return null;
                            }
                            else return r0;
                        }
                        var r = await flyout();
                        if (r is int currentState)
                        {
                            if (currentState > SHOP_LABEL_BASE && currentState <= SHOP_LABEL_BASE + 5)
                            {
                                // a numbered shop: its "SHOP n" text goes across the top of the tile (picking the same number again removes it)
                                var n = currentState - SHOP_LABEL_BASE;
                                setShopLabel(i, j, (TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]) == n && !flyoutAddedItem ? 0 : n));
                                var sourceWasBomb = (TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]) == n && autoDupShop(n));
                                if (sourceWasBomb && TrackerModel.overworldMapMarks[i, j].Current() == -1 && !(owInstance.AlwaysEmpty(i, j)) && TrackerModel.mapSquareChoiceDomain.CanAddUse(TrackerModel.MapSquareChoiceDomainHelper.BOMB))
                                    await SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.BOMB, -1);   // the shop being duplicated is a bomb shop: this one is too
                                // like a marker icon, an unmarked tile that gets a shop label counts as finished (so the shop's items can be put on it)
                                if (TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]) > 0 && !(owInstance.AlwaysEmpty(i, j)) && TrackerModel.overworldMapMarks[i, j].Current() == -1)
                                    await SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.DARK_X, -1);
                                redrawGridSpot();
                            }
                            else if (currentState == ANYROAD_MENU)
                            {
                                var placeText = "Place Vanilla Any Roads";
                                var choice = await PieMenus.MessageChoiceBoxAsync(cm, "", new[] { placeText, "Cancel" });
                                if (choice == placeText)
                                {
                                    // 1 in H10, 2 in B14, 3 in C4, 4 in E10 (row letter, then column number)
                                    foreach (var (state, rowLetter, col) in new[] { (TrackerModel.MapSquareChoiceDomainHelper.WARP_1, 'H', 10), (TrackerModel.MapSquareChoiceDomainHelper.WARP_2, 'B', 14),
                                                                                     (TrackerModel.MapSquareChoiceDomainHelper.WARP_3, 'C', 4), (TrackerModel.MapSquareChoiceDomainHelper.WARP_4, 'E', 10) })
                                    {
                                        var (ti, tj) = (col - 1, (int)rowLetter - (int)'A');
                                        for (int x = 0; x <= 15; x++)
                                        {
                                            for (int y = 0; y <= 7; y++)
                                            {
                                                if (TrackerModel.overworldMapMarks[x, y].Current() == state && (x != ti || y != tj))   // this any road was marked somewhere else: move it
                                                {
                                                    TrackerModel.overworldMapMarks[x, y].Set(-1);
                                                    owUpdateFunctions[x, y](0, null);
                                                }
                                            }
                                        }
                                        TrackerModel.overworldMapMarks[ti, tj].Set(state);
                                        owUpdateFunctions[ti, tj](0, null);
                                    }
                                    TrackerModel.forceUpdate();
                                    redrawGridSpot();
                                }
                                else
                                    reopenMenu = true;   // cancel: back to the overworld menu
                            }
                            else if (currentState == CLEAR_ICONS)
                            {
                                // red X: clears the tile completely - every marker icon is removed and the tile goes back to unmarked
                                clearAllTileIcons(i, j);
                                if (OverworldMapTileCustomization.isSwordCaveState(originalState))
                                {
                                    OverworldMapTileCustomization.clearSwordCaveItemsForState(i, j, originalState);   // the sword / candle / shuffled item taken there is given back
                                    UIComponents.itemProgressChanged();
                                }
                                // (a tile that can never hold a mark still gets a cave / dungeon that was placed on it removed, but its plain dark X stays)
                                if (originalState != -1 && (!(owInstance.AlwaysEmpty(i, j)) || originalState != TrackerModel.MapSquareChoiceDomainHelper.DARK_X))
                                    await SetNewValue(-1, originalState);
                                redrawGridSpot();
                            }
                            else if (currentState == 1000 + TrackerModel.ITEMS.LETTER
                                    && (originalState == -1 || originalState == TrackerModel.MapSquareChoiceDomainHelper.DARK_X)
                                    && !(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).Contains(TrackerModel.ITEMS.LETTER))
                                    && !(owInstance.AlwaysEmpty(i, j)) && TrackerModel.mapSquareChoiceDomain.CanAddUse(TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER))
                            {
                                // the letter works like the sword caves: it places the letter cave, and the popup asks Took Letter / Shuffled Item / Letter Icon
                                await SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER, originalState);
                                redrawGridSpot();
                            }
                            else if (currentState == 1000 + TrackerModel.DECOR_BOMB
                                    && (originalState == -1 || originalState == TrackerModel.MapSquareChoiceDomainHelper.DARK_X)
                                    && TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]) == 0   // (on a SHOP n tile the bomb is just a marker icon in the shop, like the hotkey does)
                                    && !(owInstance.AlwaysEmpty(i, j)) && TrackerModel.mapSquareChoiceDomain.CanAddUse(TrackerModel.MapSquareChoiceDomainHelper.BOMB))
                            {
                                // a bomb marker means a bomb shop: place the bomb shop tile
                                await SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.BOMB, originalState);
                                redrawGridSpot();
                            }
                            else if ((currentState == TrackerModel.MapSquareChoiceDomainHelper.SWORD1 || currentState == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || currentState == TrackerModel.MapSquareChoiceDomainHelper.SWORD3)
                                    && currentState != originalState && !(TrackerModel.mapSquareChoiceDomain.CanAddUse(currentState)))
                            {
                                // that sword cave is already placed elsewhere: this tile just gets the sword's marker icon (and counts as finished, like any other marker icon)
                                var ident = (currentState == TrackerModel.MapSquareChoiceDomainHelper.SWORD1 ? TrackerModel.ITEMS.WOODSWORD : currentState == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 ? TrackerModel.ITEMS.WHITESWORD : TrackerModel.ITEMS.MAGICALSWORD);
                                var before = TrackerModel.owSwordIcons[i, j];
                                setDecorIcons(i, j, TrackerModel.decorIconsToggle(before, ident));
                                if (TrackerModel.owSwordIcons[i, j] == before)
                                    Graphics.ErrorBeepWithReminderLogText($"A tile can show at most {TrackerModel.MaxDecorIcons} marker icons; ctrl+left click one of its icons to remove it first");
                                if (!(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).IsEmpty) && !(owInstance.AlwaysEmpty(i, j)) && TrackerModel.overworldMapMarks[i, j].Current() == -1)
                                    await SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.DARK_X, -1);
                                redrawGridSpot();
                            }
                            else if (currentState == 1000 + TrackerModel.DECOR_BOMB && originalState == TrackerModel.MapSquareChoiceDomainHelper.BOMB)
                            {
                                // the bomb marker on a bomb shop takes the bomb back off: the shop keeps its other icons (as a finished tile), or goes back to unmarked if it has none
                                await SetNewValue((TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).IsEmpty ? -1 : TrackerModel.MapSquareChoiceDomainHelper.DARK_X), originalState);
                                redrawGridSpot();
                            }
                            else if (currentState >= 1000)
                            {
                                // decorative sword item icon: toggles on this tile, tile's real mark is left alone
                                var ident = currentState - 1000;
                                var before = TrackerModel.owSwordIcons[i, j];
                                setDecorIcons(i, j, decorToggleForTile(before, ident));
                                if (TrackerModel.owSwordIcons[i, j] == before)
                                    Graphics.ErrorBeepWithReminderLogText(decorToggleRefusedByLegacy ? legacyShopRefusal : $"A tile can show at most {TrackerModel.MaxDecorIcons} marker icons; ctrl+left click one of its icons to remove it first");
                                // an unmarked tile that gets a marker icon counts as finished (Don't Care), icons stay visible
                                if (!(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).IsEmpty) && !(owInstance.AlwaysEmpty(i, j)) && TrackerModel.overworldMapMarks[i, j].Current() == -1)
                                    await SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.DARK_X, -1);
                                redrawGridSpot();
                            }
                            else
                            {
                                if (currentState == -1 || currentState == TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY || tileTypeReplacesShop(currentState)) clearAllTileIcons(i, j);   // 'Unmarked' also clears any sword icon; placing a take any cave clears every other item on the tile
                                if (OverworldMapTileCustomization.isSwordCaveState(originalState) && currentState != originalState)
                                {
                                    OverworldMapTileCustomization.clearSwordCaveItemsForState(i, j, originalState);
                                    UIComponents.itemProgressChanged();
                                }
                                await SetNewValue(currentState, originalState);
                                if (currentState == -1) redrawGridSpot();
                            }
                        }
                        tileCanvas.Children.Clear();   // deparent tileImage
                        popupIsActive = false;
                        if (reopenMenu) activatePopup(0);
                    }
                    async Task goCleaningUp()
                    {
                        try { await go(); }
                        catch { tileCanvas.Children.Clear(); throw; }   // (the shared tileImage must be free to show again; Guard logs the failure)
                    }
                    CustomComboBoxes.StartImmediate(CustomComboBoxes.GlobalFlag.Guard(goCleaningUp(), $"overworld tile menu, {(char)('A' + j)}{i + 1}"));
                }
                // copy / paste item markers (mouse button 4; while markers are carried, a plain left click on an open tile also pastes); false if there was nothing to copy or paste here
                bool copyOrPasteMarkers()
                {
                    var curMark = TrackerModel.overworldMapMarks[i, j].Current();
                    var hereIcons = TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]);
                    var onlyItems = curMark == -1 || curMark == TrackerModel.MapSquareChoiceDomainHelper.DARK_X || curMark == TrackerModel.MapSquareChoiceDomainHelper.BOMB;
                    if (!hereIcons.IsEmpty && onlyItems)
                    {
                        copiedMarkerIdents = hereIcons.ToList();
                        // the duplicate keeps everything on the original, including which items are already collected
                        {
                            var p = TrackerModel.owSwordIcons[i, j];
                            var kept = TrackerModel.decorEntriesOf(p);
                            var packed = TrackerModel.decorEntriesPack(kept);
                            var label = TrackerModel.shopLabelOf(p);
                            copiedMarkerPacked = label > 0 ? TrackerModel.withShopLabel(packed, label) : packed;
                        }
                        copiedMarkerSource = (i, j);
                        copiedMarkerBomb = (curMark == TrackerModel.MapSquareChoiceDomainHelper.BOMB);
                        refreshCopyCursor();
                        animateOverworldTileIfOptionIsChecked(i, j);   // brief flash = copied; left-click (or mouse button 4 on) an open tile to paste
                        return true;
                    }
                    else if (hereIcons.IsEmpty && curMark == -1 && copiedMarkerIdents.Count != 0 && !(owInstance.AlwaysEmpty(i, j)))
                    {
                        popupIsActive = true;
                        void linkDuplicate()
                        {
                            if (copiedMarkerSource != (i, j) && copiedMarkerSource != (-1, -1))
                            {
                                if (dupLinks.TryGetValue(copiedMarkerSource, out var l))
                                {
                                    if (!(l.Contains((i, j)))) l.Add((i, j));
                                }
                                else
                                {
                                    var nl = new List<(int, int)>();
                                    nl.Add((i, j));
                                    dupLinks[copiedMarkerSource] = nl;
                                }
                            }
                        }
                        async Task go()
                        {
                            setDecorIcons(i, j, copiedMarkerPacked);
                            if (copiedMarkerSource != (-1, -1)) copyTileTiers(copiedMarkerSource, (i, j));   // just the duplicated markers: the tile itself is left as it was (a copied bomb shop makes this a bomb shop too)
                            if (copiedMarkerBomb && TrackerModel.mapSquareChoiceDomain.CanAddUse(TrackerModel.MapSquareChoiceDomainHelper.BOMB))
                                await SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.BOMB, -1);
                            else if (!copiedMarkerBomb)
                                await SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.DARK_X, -1);   // a duplicated shop is placed as complete (dimmed), like any placed shop
                            linkDuplicate();
                            redrawGridSpot();
                            copiedMarkerIdents = new List<int>();   // one paste per copy: clear the cursor
                            refreshCopyCursor();
                            popupIsActive = false;
                        }
                        CustomComboBoxes.StartImmediate(go());
                        return true;
                    }
                    else return false;
                }
                c.PointerPressed += (_, ea) =>
                {
                    if (!popupIsActive)
                    {
                        var button = ea.GetCurrentPoint(c).Properties.PointerUpdateKind;   // (WPF: ea.ChangedButton)
                        var shiftDown = (ea.KeyModifiers & KeyModifiers.Shift) != 0;      // (WPF: Keyboard.IsKeyDown(LeftShift/RightShift))
                        var ctrlDown = (ea.KeyModifiers & KeyModifiers.Control) != 0;     // (WPF: Keyboard.IsKeyDown(LeftCtrl/RightCtrl))
                        if (button == PointerUpdateKind.LeftButtonPressed)
                        {
                            var canPasteHere = copiedMarkerIdents.Count != 0 && TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).IsEmpty && TrackerModel.overworldMapMarks[i, j].Current() == -1 && !(owInstance.AlwaysEmpty(i, j));
                            if (canPasteHere)
                                // markers are being carried on the mouse (copied with mouse button 4): the first left click on any open tile puts them down
                                copyOrPasteMarkers();
                            else if (shiftDown)
                                // shift left click: the circle label popup
                                activateCircleLabelPopup();
                            else if (visibleDecorIcons(TrackerModel.owSwordIcons[i, j]).Any(id => id != TrackerModel.DECOR_BOMB_DROPPER))   // (a bomb dropper marker is not a checkable item: left click on it falls through to the normal tile click)
                            {
                                // left click on a tile with marker icons checks off (or un-checks) just the icon nearest the mouse
                                var vis = visibleDecorIcons(TrackerModel.owSwordIcons[i, j]);
                                var px = ea.GetPosition(c).X;
                                var ms = new MapStateProxy(TrackerModel.overworldMapMarks[i, j].Current());
                                var big = ms.State == -1 || ms.IsX || bombSlotActive;
                                (int, int) nearest;
                                {
                                    var disp = decorDisplayList(vis);
                                    var best = disp.Select((p, dk) => (dk, p)).Where(t => t.p.Item1 != TrackerModel.DECOR_BOMB_DROPPER).MinBy(t =>
                                    {
                                        var off = bombSlotActive ? 1 : 0;
                                        var (size, x, _y) = decorSlotC(TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]) > 0, bombSlotActive, big, disp.Count + off, t.dk + off);
                                        return Math.Abs(px - (x + size / 2.0));
                                    });
                                    var origK = best.p.Item2;
                                    nearest = (origK, vis[origK]);
                                }
                                // (the same item can appear more than once, e.g. several heart containers: act on just the nearest copy)
                                var nearestIdx = nearest.Item1;
                                var nearestIdent = nearest.Item2;
                                var copyNumber = vis.Take(nearestIdx).Count(id => id == nearestIdent);
                                if (ctrlDown)
                                    // ctrl+left click removes just that one icon from the tile
                                    setDecorIcons(i, j, TrackerModel.decorRemoveNth(TrackerModel.owSwordIcons[i, j], nearestIdent, copyNumber));
                                else
                                    setDecorIcons(i, j, TrackerModel.decorToggleCollectedNth(TrackerModel.owSwordIcons[i, j], nearestIdent, copyNumber));
                                redrawGridSpot();
                            }
                            else
                            {
                                // left click is the 'special interaction'
                                var pos = c.TranslatePoint(new Point(), appMainCanvas).Value;
                                var msp = new MapStateProxy(TrackerModel.overworldMapMarks[i, j].Current());
                                if (msp.IsX)
                                {
                                    if (TrackerModel.owInstance.AlwaysEmpty(i, j))
                                    { }  // tile is AlwaysEmpty in current quest; don't open popup (only circles allowed)
                                    else if (Graphics.shouldInitiallyHideOverworldMap)
                                    {
                                        var ex = TrackerModel.getOverworldMapExtraData(i, j, msp.State);
                                        popupIsActive = true;
                                        async Task go()
                                        {
                                            if (ex == msp.State)
                                            {
                                                // hidden -> unmarked           (and recall that unmarked left clicks to DontCare)
                                                TrackerModel.setOverworldMapExtraData(i, j, msp.State, 0);
                                                await SetNewValue(-1, msp.State);
                                            }
                                            else
                                                // DontCare -> hidden           (thus left click is a 3-cycle hidden, unmarked, DontCare)
                                                TrackerModel.setOverworldMapExtraData(i, j, msp.State, msp.State);
                                            redrawGridSpot();
                                            popupIsActive = false;
                                        }
                                        CustomComboBoxes.StartImmediate(go());
                                    }
                                    else
                                        activatePopup(0);  // thus, if you have unmarked, then left-click left-click pops up, as the first marks X, and the second now pops up
                                }
                                else if (msp.IsThreeItemShop)
                                    activatePopup(0);   // shops now use the marker-icon menu (no old 2nd / 3rd shop item chooser)
                                else
                                {
                                    popupIsActive = true;
                                    async Task go()
                                    {
                                        var (needRedraw, needHighlightTileHide) = await DoLeftClick(cm, msp, i, j, pos);
                                        if (needRedraw)
                                        {
                                            redrawGridSpot();
                                            animateOverworldTileIfOptionIsChecked(i, j);
                                        }
                                        if (needHighlightTileHide) OverworldRouteDrawing.routeDrawingLayer.GetHighlightTile(i, j).Hide();  // dismiss any green/yellow highlight on this tile
                                        popupIsActive = false;
                                    }
                                    CustomComboBoxes.StartImmediate(go());
                                }
                            }
                        }
                        else if (button == PointerUpdateKind.RightButtonPressed)
                        {
                            if (shiftDown)
                            {
                                // shift right click clears the tile: back to unmarked (which also un-finds a dungeon/cave marked here), no marker icons, no circle
                                // (tiles that can never hold a mark, e.g. E1, still get their marker icons / circle cleared)
                                if (!popupIsActive)
                                {
                                    popupIsActive = true;
                                    ea.Handled = true;
                                    async Task go()
                                    {
                                        var cur = TrackerModel.overworldMapMarks[i, j].Current();
                                        clearAllTileIcons(i, j);   // un-collects any items that were checked off from this tile's icons
                                        // a wood sword cave where the sword / candle / shuffled item was taken: take that item back
                                        if (OverworldMapTileCustomization.isSwordCaveState(cur))
                                        {
                                            OverworldMapTileCustomization.clearSwordCaveItemsForState(i, j, cur);
                                            UIComponents.itemProgressChanged();
                                        }
                                        if (cur != -1 && (!(owInstance.AlwaysEmpty(i, j)) || cur != TrackerModel.MapSquareChoiceDomainHelper.DARK_X))
                                            await SetNewValue(-1, cur);
                                        if (TrackerModel.overworldMapCircles[i, j] != 0)
                                        {
                                            TrackerModel.overworldMapCircles[i, j] = 0;
                                            owCircleRedraws[i, j]();
                                        }
                                        TrackerModel.forceUpdate();
                                        redrawGridSpot();
                                        popupIsActive = false;
                                    }
                                    CustomComboBoxes.StartImmediate(go());
                                }
                            }
                            else
                                // right click activates the popup selector on every tile
                                activatePopup(0);
                        }
                        else if (button == PointerUpdateKind.XButton1Pressed)
                            copyOrPasteMarkers();   // mouse button 4 copies / pastes item markers
                        else if (button == PointerUpdateKind.MiddleButtonPressed)
                        {
                            if (TrackerModel.MarkBombDroppers && ea.KeyModifiers != KeyModifiers.Shift)
                            {
                                // 'Mark Bomb Droppers' startup option: the first middle click puts the bomb dropper icon on the tile; the scroll wheel then cycles bomb dropper > blue > yellow > red circle and around;
                                // middle clicking again removes whatever was chosen. (shift+middle click is the plain circle toggle)
                                var hasDropper = TrackerModel.owBombDroppers[i, j];
                                if (hasDropper)
                                    TrackerModel.owBombDroppers[i, j] = false;
                                else if (TrackerModel.overworldMapCircles[i, j] != 0)
                                {
                                    TrackerModel.overworldMapCircles[i, j] = 0;
                                    owCircleRedraws[i, j]();
                                }
                                else
                                    TrackerModel.owBombDroppers[i, j] = true;
                                TrackerModel.forceUpdate();
                                redrawGridSpot();
                            }
                            else
                            {
                                // middle click toggles the circle
                                TrackerModel.toggleOverworldMapCircle(i, j);
                                owCircleRedraws[i, j]();
                            }
                        }
                    }
                };
                c.PointerWheelChanged += (sender, x) =>
                {
                    var notch = Graphics.wheelNotch(sender, x);
                    if (notch == 0) return;
                    if (!popupIsActive)
                    {
                        var hasDropper = TrackerModel.owBombDroppers[i, j];
                        if (TrackerModel.overworldMapMarks[i, j].Current() == TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP && TrackerModel.overworldMapCircles[i, j] == 0)
                        {
                            // a hint shop: scrolling cycles its background color (purple, red, green, orange)
                            var cur = OverworldMapTileCustomization.hintShopColorOf(i, j);
                            OverworldMapTileCustomization.hintShopColors[Tuple.Create(i, j)] = (cur + (notch < 0 ? 1 : 3)) % 4;
                            OverworldMapTileCustomization.hintShopColorChosen = true;
                            OverworldMapTileCustomization.lastHintShopColor = OverworldMapTileCustomization.hintShopColors[Tuple.Create(i, j)];
                            redrawGridSpot();
                        }
                        else if (TrackerModel.MarkBombDroppers && hasDropper && TrackerModel.overworldMapCircles[i, j] == 0)
                        {
                            // bomb dropper > (scroll) blue > yellow > red > bomb dropper ...
                            TrackerModel.owBombDroppers[i, j] = false;
                            TrackerModel.toggleOverworldMapCircle(i, j);   // starts at the first color
                            if (notch >= 0) TrackerModel.prevOverworldMapCircleColor(i, j);   // scrolling the other way enters the rotation at the last color
                            owCircleRedraws[i, j]();
                            redrawGridSpot();
                        }
                        else if (TrackerModel.overworldMapCircles[i, j] != 0)   // if there's a circle, then it takes scrollwheel priority
                        {
                            var colorIndex = TrackerModel.overworldMapCircles[i, j] / 100;
                            var isLabeled = (TrackerModel.overworldMapCircles[i, j] % 100) > 1;   // a circle labeled with a letter/digit (hint) just cycles its colors; only unlabeled circles rotate through the bomb dropper icon
                            if (TrackerModel.MarkBombDroppers && !isLabeled && ((notch < 0 && colorIndex == 2) || (notch >= 0 && colorIndex == 0)))
                            {
                                // scrolled past the last (first) color: back around to the bomb dropper icon
                                TrackerModel.overworldMapCircles[i, j] = 0;
                                owCircleRedraws[i, j]();
                                TrackerModel.owBombDroppers[i, j] = true;
                                TrackerModel.forceUpdate();
                                redrawGridSpot();
                            }
                            else
                            {
                                if (notch < 0)
                                    TrackerModel.nextOverworldMapCircleColor(i, j);
                                else
                                    TrackerModel.prevOverworldMapCircleColor(i, j);
                                owCircleRedraws[i, j]();
                            }
                        }
                        else
                            activatePopup(notch < 0 ? 1 : -1);
                    }
                };
                c.PointerEntered += (_, _) => { var z = owInstance.HintZoneOf(i, j); if (z != 0) hintButtonHighlightHook(z, true); };
                c.PointerExited += (_, _) => { var z = owInstance.HintZoneOf(i, j); if (z != 0) hintButtonHighlightHook(z, false); };
                // 'Hover shop tooltip' option: hovering a shop tile shows what is in that shop (same size as the Dead Woods hint hover box)
                c.PointerEntered += (_, _) =>
                {
                    var label = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]);
                    if (TrackerModelOptions.HoverShopTooltip.Value && label > 0 && !popupIsActive)
                    {
                        var items = shopItemBitmapsGlobal(label == 5 ? 0 : label);
                        var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4.0), VerticalAlignment = VerticalAlignment.Center };
                        var iconSize = (label == 5 || items.Count <= 3 ? 48.0 : 36.0);
                        sp.Children.Add(new TextBlock { Text = (label == 5 ? "P SHOP" : $"SHOP {label}"), FontSize = 22.0, FontWeight = FontWeight.Bold, Foreground = Brushes.Orange, HorizontalAlignment = HorizontalAlignment.Center });
                        var wp = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0.0, 6.0, 0.0, 0.0) };
                        void addImgB(System.Drawing.Bitmap bmp, bool collected, bool boxed)
                        {
                            var img = Graphics.BMPtoImage(bmp);
                            img.Stretch = Stretch.Uniform;
                            img.Width = iconSize;
                            img.Height = iconSize;
                            img.Margin = new Thickness(2.0);
                            RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None);
                            if (collected) img.Opacity = 0.4;
                            if (boxed) wp.Children.Add(new Border { BorderBrush = Brushes.Yellow, BorderThickness = new Thickness(2.0), Child = img });   // the cheaper key shop's key
                            else wp.Children.Add(img);
                        }
                        void addImg(System.Drawing.Bitmap bmp, bool collected) => addImgB(bmp, collected, false);
                        var keyIdx = label >= 1 && label <= 4 && TrackerModel.cheapKeyShops[label] ? shopKeyIndexGlobal(label) : -1;
                        if (label == 5)
                        {
                            addImg(decorSourceBmp(TrackerModel.ITEMS.BLUEPOTION), false);
                            if (items.Count > 0) addImg(items[0].Item1, items[0].Item2);
                            else wp.Children.Add(new TextBlock { Text = "?", FontSize = 40.0, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Width = iconSize + 4.0, TextAlignment = TextAlignment.Center });
                            addImg(decorSourceBmp(TrackerModel.ITEMS.REDPOTION), false);
                        }
                        else
                        {
                            for (int idx = 0; idx < items.Count; idx++)
                                addImgB(items[idx].Item1, items[idx].Item2, (idx == keyIdx));
                        }
                        sp.Children.Add(wp);
                        var big = new Border { Child = sp, Background = Brushes.Black, BorderBrush = Brushes.DarkGray, BorderThickness = new Thickness(3.0), Width = 200.0, Height = 112.0, IsHitTestVisible = false };
                        var ctr = centerOf(i, j);
                        var x = Math.Max(0.0, Math.Min((ctr.X - 100.0), (cm.AppMainCanvas.Width - 202.0)));
                        var y = ctr.Y + 20.0 + 112.0 < cm.AppMainCanvas.Height ? ctr.Y + 20.0 : Math.Max(0.0, (ctr.Y - 20.0 - 112.0));
                        layout.ClearTopLayerHovers();
                        layout.AddTopLayerHover(big, x, y);
                    }
                };
                c.PointerExited += (_, _) => { if (TrackerModelOptions.HoverShopTooltip.Value) layout.ClearTopLayerHovers(); };
                c.MyKeyAdd(ea =>
                {
                    if (!popupIsActive)
                    {
                        var hasHotKeyedState = HotKeys.OverworldHotKeyProcessor.TryGetValue(ea.Key, out var hotKeyedState);
                        if (hasHotKeyedState && (hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP || TrackerModel.MapSquareChoiceDomainHelper.IsItem(hotKeyedState)) && (TrackerModel.overworldMapMarks[i, j].Current() == TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY || tookCaveItem(TrackerModel.overworldMapMarks[i, j].Current()) || isDungeonOrAnyRoad(TrackerModel.overworldMapMarks[i, j].Current())))
                        {
                            ea.Handled = true;
                            Graphics.ErrorBeepWithReminderLogText("This cave can only have the bomb dropper marker");
                        }
                        else if (hasHotKeyedState && hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP)
                        {
                            // the Overworld_Shop hotkey opens the Shop flyout (potion shop / shops 1-4); its own hotkeys (Contextual_Shop_...) then pick one
                            ea.Handled = true;
                            openShopFlyoutDirectly = true;
                            activatePopup(0);
                        }
                        // (the arm above already catches every POTION_SHOP hotkey, so it is not repeated below)
                        else if (hasHotKeyedState && (TrackerModel.MapSquareChoiceDomainHelper.IsItem(hotKeyedState)
                                 || (isShopTile(i, j) && (hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER || hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 || hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.SWORD3))))
                        {
                            // the shop hotkeys place the new marker icons, exactly like picking the item from the tile menu (the potion shop is the blue + red potion pair)
                            // (on a tile that is already a shop, the Letter / White Sword / Magical Sword hotkeys put that item in the shop instead of turning the tile into a cave)
                            var shopIdent =
                                hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER ? TrackerModel.ITEMS.LETTER
                                : hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 ? TrackerModel.ITEMS.WHITESWORD
                                : hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.SWORD3 ? TrackerModel.ITEMS.MAGICALSWORD
                                : hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP ? TrackerModel.ITEMS.BLUEPOTION
                                : hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.ARROW ? TrackerModel.ITEMS.WOODARROW
                                : hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.BOMB ? TrackerModel.DECOR_BOMB
                                : hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.BOOK ? TrackerModel.ITEMS.BOOKSHIELD
                                : hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.BLUE_CANDLE ? TrackerModel.ITEMS.BLUECANDLE
                                : hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.BLUE_RING ? TrackerModel.ITEMS.BLUERING
                                : hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.MEAT ? TrackerModel.ITEMS.MEAT
                                : hotKeyedState == TrackerModel.MapSquareChoiceDomainHelper.KEY ? TrackerModel.ITEMS.OLDKEY
                                : TrackerModel.ITEMS.SHIELD;
                            popupIsActive = true;
                            ea.Handled = true;
                            async Task go()
                            {
                                var origState = TrackerModel.overworldMapMarks[i, j].Current();
                                if (shopIdent == TrackerModel.DECOR_BOMB && TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]) == 0 && (origState == -1 || origState == TrackerModel.MapSquareChoiceDomainHelper.DARK_X)
                                        && !(owInstance.AlwaysEmpty(i, j)) && TrackerModel.mapSquareChoiceDomain.CanAddUse(TrackerModel.MapSquareChoiceDomainHelper.BOMB))
                                {
                                    // like the tile menu: a bomb marker places the bomb shop tile (so it shows the yellow 'found but not collected' bomb)
                                    await SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.BOMB, origState);
                                    redrawGridSpot();
                                }
                                else if (shopIdent == TrackerModel.DECOR_BOMB && origState == TrackerModel.MapSquareChoiceDomainHelper.BOMB)
                                {
                                    // pressing the bomb hotkey again on a bomb tile takes the bomb back off
                                    await SetNewValue((TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).IsEmpty ? -1 : TrackerModel.MapSquareChoiceDomainHelper.DARK_X), origState);
                                    redrawGridSpot();
                                }
                                else
                                {
                                    var before = TrackerModel.owSwordIcons[i, j];
                                    setDecorIcons(i, j, TrackerModel.decorIconsToggle(before, shopIdent));
                                    if (TrackerModel.owSwordIcons[i, j] == before)
                                        Graphics.ErrorBeepWithReminderLogText($"A tile can show at most {TrackerModel.MaxDecorIcons} marker icons; ctrl+left click one of its icons to remove it first");
                                    if (!(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).IsEmpty) && !(owInstance.AlwaysEmpty(i, j)) && TrackerModel.overworldMapMarks[i, j].Current() == -1)
                                        await SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.DARK_X, -1);
                                }
                                popupIsActive = false;
                            }
                            CustomComboBoxes.StartImmediate(go());
                        }
                        else if (hasHotKeyedState)
                        {
                            popupIsActive = true;
                            ea.Handled = true;
                            var originalState = TrackerModel.overworldMapMarks[i, j].Current();
                            var state = OverworldMapTileCustomization.DoSpecialHotKeyHandlingForOverworldTiles(i, j, originalState, hotKeyedState);
                            async Task go()
                            {
                                await SetNewValue(state, originalState);
                                // a secret / door repair / money making game replaces the shop, but only when the tile really took it (the change can be refused)
                                // (and, like the tile menu: a take any cave clears every other item on the tile, and changing away from a sword / letter / take any cave gives back what was taken there)
                                if (state != originalState && TrackerModel.overworldMapMarks[i, j].Current() == state)
                                {
                                    if (tileTypeReplacesShop(state) || state == TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY) clearAllTileIcons(i, j);
                                    if (OverworldMapTileCustomization.isSwordCaveState(originalState))
                                    {
                                        OverworldMapTileCustomization.clearSwordCaveItemsForState(i, j, originalState);
                                        UIComponents.itemProgressChanged();
                                    }
                                }
                                popupIsActive = false;
                            }
                            CustomComboBoxes.StartImmediate(go());
                        }
                        else
                        {
                            // hotkey for a dungeon-item marker icon: toggles it on this tile, exactly like picking it from the tile menu
                            // (Item_X hotkeys work here too; the bomb marker has its own OverworldMarker_Bomb entry)
                            int? markerIdent;
                            if (HotKeys.OverworldMarkerHotKeyProcessor.TryGetValue(ea.Key, out var ident0)) markerIdent = ident0;
                            else if (HotKeys.ItemHotKeyProcessor.TryGetValue(ea.Key, out var item) && HotKeys.OverworldMarkerItems.Contains(item)) markerIdent = item;
                            else markerIdent = null;
                            if (markerIdent is int ident && ident == HotKeys.CLEAR_TILE_HOTKEY)
                            {
                                // 'Overworld_ClearTile': the same as the red X in the tile menu - every marker icon and whatever was placed or taken there is cleared, and the tile is unmarked again
                                popupIsActive = true;
                                ea.Handled = true;
                                async Task go()
                                {
                                    var originalState = TrackerModel.overworldMapMarks[i, j].Current();
                                    clearAllTileIcons(i, j);
                                    if (OverworldMapTileCustomization.isSwordCaveState(originalState))
                                    {
                                        OverworldMapTileCustomization.clearSwordCaveItemsForState(i, j, originalState);
                                        UIComponents.itemProgressChanged();
                                    }
                                    if (originalState != -1 && (!(owInstance.AlwaysEmpty(i, j)) || originalState != TrackerModel.MapSquareChoiceDomainHelper.DARK_X))
                                        await SetNewValue(-1, originalState);
                                    redrawGridSpot();
                                    popupIsActive = false;
                                }
                                CustomComboBoxes.StartImmediate(go());
                            }
                            else if (markerIdent.HasValue && isDungeonOrAnyRoad(TrackerModel.overworldMapMarks[i, j].Current()))
                            {
                                ea.Handled = true;
                                Graphics.ErrorBeepWithReminderLogText("A dungeon entrance or any road can only have the bomb dropper marker");
                            }
                            else if (markerIdent is int ident2)
                            {
                                popupIsActive = true;
                                ea.Handled = true;
                                async Task go()
                                {
                                    var before = TrackerModel.owSwordIcons[i, j];
                                    setDecorIcons(i, j, decorToggleForTile(before, ident2));
                                    if (TrackerModel.owSwordIcons[i, j] == before)
                                        Graphics.ErrorBeepWithReminderLogText(decorToggleRefusedByLegacy ? legacyShopRefusal : $"A tile can show at most {TrackerModel.MaxDecorIcons} marker icons; ctrl+left click one of its icons to remove it first");
                                    if (!(TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).IsEmpty) && !(owInstance.AlwaysEmpty(i, j)) && TrackerModel.overworldMapMarks[i, j].Current() == -1)
                                        await SetNewValue(TrackerModel.MapSquareChoiceDomainHelper.DARK_X, -1);
                                    popupIsActive = false;
                                }
                                CustomComboBoxes.StartImmediate(go());
                            }
                        }
                    }
                });
                if (Graphics.shouldInitiallyHideOverworldMap && !isPermEmpty)
                {
                    TrackerModel.overworldMapMarks[i, j].Set(TrackerModel.MapSquareChoiceDomainHelper.DARK_X);
                    TrackerModel.setOverworldMapExtraData(i, j, TrackerModel.MapSquareChoiceDomainHelper.DARK_X, TrackerModel.MapSquareChoiceDomainHelper.DARK_X);
                    redrawGridSpot();
                }
                else if (TrackerModel.overworldMapMarks[i, j].Current() == TrackerModel.MapSquareChoiceDomainHelper.DARK_X)
                    redrawGridSpot();  // quest-switchable tile that starts as AlwaysEmpty in the current quest; draw its initial dark X
            }
        }
        if (speechRecognitionInstance != null)
        {
            speechRecognitionInstance.AttachSpeechRecognizedToApp(appMainCanvas, recognizedText =>
            {
                if (currentlyMousedOWX >= 0) // can hear speech before we have moused over any (uninitialized location)
                {
                    var c = owCanvases[currentlyMousedOWX, currentlyMousedOWY];
                    if (c != null && c.IsPointerOver)  // canvas can be null for always-empty grid places
                        // Note: IsMouseOver appears only be true if we are along the way from a mouse click to the visual root.
                        // As a result, the update never fires when a popup window is active, because the popup sunglasses would always
                        // intercept the click in a different part of the Visual tree.  This is good, because we don't want speech
                        // mutating the world while a popup is active (the popup's modality should block speech).
                        // I guess we could also ask 'cm' if a popup is active.
                        owUpdateFunctions[currentlyMousedOWX, currentlyMousedOWY](777, recognizedText);
                }
            });
        }
        OptionsMenu.requestRedrawOverworldEvent += () =>
        {
            for (int i = 0; i <= 15; i++)
                for (int j = 0; j <= 7; j++)
                    owUpdateFunctions[i, j](0, null);  // redraw tile
        };
        canvasAdd(overworldCanvas, owMapGrid, 0.0, 0.0);
        ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes = () =>
        {
//          printfn "eROWGSAOCCB"
            var showGettables = owGettableScreensCheckBox.IsChecked.HasValue && owGettableScreensCheckBox.IsChecked.Value;
            var unmarked = new bool[16, 8];
            for (int x = 0; x <= 15; x++) for (int y = 0; y <= 7; y++) unmarked[x, y] = TrackerModel.overworldMapMarks[x, y].Current() == -1;
            OverworldRouteDrawing.drawPathsImpl(TrackerModel.mapStateSummary.OwRouteworthySpots,
                        unmarked, new Point(0.0, 0.0), 0, 0, false, true, 0, showGettables, whetherToCyanOpenCavesOrArmos());
        };

        await showProgress("overworld finish, legend");

        var recorderingCanvas = new Canvas { Width = 16.0 * OMTW, Height = (double)(8 * 11 * 3) };  // really the 'extra top layer' canvas for adding final marks to overworld map
        recorderingCanvas.IsHitTestVisible = false;  // do not let this layer see/absorb mouse interactions
        canvasAdd(overworldCanvas, recorderingCanvas, 0.0, 0.0);

        canvasAdd(overworldCanvas, overworldCirclesCanvas, 0.0, 0.0);

        // legend
        var (recorderDestinationButtonCanvas, anyRoadLegendIcon, dungeonLegendIconArea, updateCurrentRecorderDestinationNumeral, legendCanvas, startIcon, theCustomWaypointIcon) =
                UIComponents.MakeLegend(cm, doUIUpdateEvent);
        layout.AddLegend(legendCanvas);
        // 1Q/2Q button: sits right beside the Start Spot box in the legend row, built exactly like the Start Spot button
        // (a default Button around a 29px-high black canvas) so height and border match; it forwards its click to the real 1Q/2Q button
        if (isStandardHyrule && moreFQSQoptionsButton != null)
        {
            // (the 1Q/2Q button now lives in the control panel at the top, under the OVERWORLD OPTIONS heading; it forwards its click to the real 1Q/2Q button)
            var fqsqPanelButton = Graphics.makeButton("Overworld + Extras", 11.0, Brushes.Orange);
            fqsqPanelButton.Click += (_, _) => moreFQSQoptionsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            layout.AddOverworldOptionsButton(fqsqPanelButton);
            // ZORA Hints: Lost Woods / Lost Hills direction sequences, in the open legend space right of the 1Q/2Q button
            Button makeHintButton(string name, string _prefix)
            {
                // built like the 1Q/2Q and Start Spot buttons (plain Button, pure-black borderless TextBox content that is not stretched), so the border looks identical
                var tb = new TextBox { Text = name, FontSize = 10.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false,
                                        BorderThickness = new Thickness(0.0), Padding = new Thickness(2.0, 0.0, 2.0, 0.0), TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
                var btn = new Button { Content = tb, Margin = new Thickness(0.0), Width = OMTW * 1.45 + 2.0, Height = 31.0, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };   // (side by side now, as tall as the Start Spot / 1Q-2Q buttons)
                Graphics.styleRoundedButton(btn);   // two of these stacked (32px) fit inside the 33px legend row
                var hint = "";
                var defaultBorderBrush = btn.BorderBrush;
                var defaultBorderThickness = btn.BorderThickness;
                void refreshLabel()
                {
                    tb.Text = (hint == "" ? name : hint);   // once entered: just the letters, bigger
                    tb.FontSize = (hint == "" ? 10.0 : 20.0);
                    tb.FontWeight = (hint == "" ? FontWeight.Normal : FontWeight.Bold);
                    tb.HorizontalAlignment = HorizontalAlignment.Stretch;   // (fills the button, so its black background doesn't show as a box around the text)
                    tb.VerticalAlignment = VerticalAlignment.Stretch;
                    tb.VerticalContentAlignment = VerticalAlignment.Center;
                    tb.Padding = (hint == "" ? new Thickness(2.0, 0.0, 2.0, 0.0) : new Thickness(0.0));
                    tb.TextAlignment = TextAlignment.Center;
                }
                // a loaded save puts its hint letters back (the border goes green, like after typing them in)
                hintLoadHooks[name] = s =>
                {
                    hint = s;
                    refreshLabel();
                    btn.BorderBrush = (s != "" ? Brushes.LimeGreen : defaultBorderBrush);
                    btn.BorderThickness = (s != "" ? new Thickness(2.0) : defaultBorderThickness);
                };
                // when the matching 'Randomize ...' option is not enabled, hovering shows an orange-outlined note saying so (and the hint menu does not open)
                bool hintOptionEnabled() => (name == "Dead Woods" ? OverworldData.DeadWoodsEnabled : OverworldData.LostHillsEnabled);
                var noteTB = new TextBox { Text = $"Randomize {name} Not Enabled", IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 14.0, Foreground = Brushes.Orange, Background = Brushes.Black };
                var noteTip = new ToolTip { Content = noteTB, Background = Brushes.Black, BorderBrush = Brushes.Orange, BorderThickness = new Thickness(2.0), Padding = new Thickness(4.0) };
                ToolTip.SetTip(btn, noteTip);
                ToolTip.SetShowDelay(btn, 0);
                ToolTip.SetPlacement(btn, PlacementMode.Bottom);
                // (WPF: the tip closes itself in its Opened event, and ToolTipOpening is marked handled, when the option is enabled. Avalonia raises
                // ToolTipOpening on the control just before showing the tip; cancelling it keeps the tip closed.)
                ToolTip.AddToolTipOpeningHandler(btn, (_, ea) =>
                {
                    noteTB.Text = (TrackerModel.LegacyMode ? "Disabled In Legacy Mode" : $"Randomize {name} Not Enabled");
                    if (hintOptionEnabled()) ea.Cancel = true;
                });
                var hoverBorder = new SolidColorBrush(Color.FromRgb(0xFF, 0xA3, 0x1A));
                btn.PointerEntered += (_, _) => { if (!(hintOptionEnabled())) btn.BorderBrush = hoverBorder; };
                btn.PointerExited += (_, _) => { if (!(hintOptionEnabled())) btn.BorderBrush = defaultBorderBrush; };
                // hovering the button shows the tiles that hint is blocking
                var hintZoneNumber = (name == "Dead Woods" ? 1 : 2);
                btn.PointerEntered += (_, _) => { if (hintOptionEnabled()) showLocatorInstanceFuncAll((x, y) => owInstance.HintZoneOf(x, y) == hintZoneNumber); };
                btn.PointerExited += (_, _) => { if (hintOptionEnabled()) hideLocator(); };
                btn.Click += (_, _) =>
                {
                    if (!popupIsActive && hintOptionEnabled())
                    {
                        popupIsActive = true;
                        var pos = btn.TranslatePoint(new Point(0.0, btn.Bounds.Height + 4.0), cm.AppMainCanvas).Value;   // (WPF: ActualHeight)
                        async Task go()
                        {
                            var wh = new ManualResetEvent(false);
                            var cur = hint;
                            var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(3.0) };
                            TextBox mkTxt(double size, string txt) => new TextBox { IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = size, Margin = new Thickness(3.0), Text = txt, Foreground = Brushes.Orange, Background = Brushes.Black, TextAlignment = TextAlignment.Center };
                            var hoverToggle = PieMenus.makeOrangeCheckBox("Hover tooltip", TrackerModelOptions.HintHoverTooltip.Value);
                            hoverToggle.HorizontalAlignment = HorizontalAlignment.Center;
                            hoverToggle.Click += (_, _) =>
                            {
                                TrackerModelOptions.HintHoverTooltip.Value = (hoverToggle.IsChecked == true);
                                TrackerModelOptions.writeSettings();
                            };
                            sp.Children.Add(hoverToggle);
                            sp.Children.Add(mkTxt(16.0, name + " hint"));
                            sp.Children.Add(mkTxt(12.0, "Select direction or use arrow keys"));
                            var display = mkTxt(20.0, "");
                            void updateDisplay() => display.Text = (cur == "" ? "-" : string.Join(" ", cur.ToCharArray().Select(ch => ch.ToString())));
                            updateDisplay();
                            sp.Children.Add(display);
                            void add(string c)
                            {
                                if (cur.Length < 4)
                                {
                                    cur = cur + c;
                                    updateDisplay();
                                    if (cur.Length >= 4) wh.Set();   // 4 directions entered = done
                                }
                            }
                            void backspace()
                            {
                                if (cur.Length > 0)
                                {
                                    cur = cur.Substring(0, cur.Length - 1);
                                    updateDisplay();
                                }
                            }
                            var dirs = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0.0, 4.0, 0.0, 2.0) };
                            // Lost Hills hints are up/down/left/right; Dead Woods hints are compass directions
                            var isDeadWoods = (name == "Dead Woods");
                            var (kU, kD, kL, kR) = (isDeadWoods ? ("N", "S", "W", "E") : ("U", "D", "L", "R"));
                            foreach (var c in (isDeadWoods ? new[] { "N", "E", "S", "W" } : new[] { "U", "D", "L", "R" }))
                            {
                                var db = new Button { Content = c, Width = 36.0, Height = 32.0, FontSize = 16.0, Margin = new Thickness(4.0, 0.0, 4.0, 0.0), Focusable = false };
                                db.Click += (_, _) => add(c);
                                dirs.Children.Add(db);
                            }
                            sp.Children.Add(dirs);
                            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0.0, 4.0, 0.0, 2.0) };
                            var backBtn = new Button { Content = " Back ", Margin = new Thickness(4.0, 0.0, 4.0, 0.0), Focusable = false };
                            var clearBtn = new Button { Content = " Clear ", Margin = new Thickness(4.0, 0.0, 4.0, 0.0), Focusable = false };
                            var doneBtn = new Button { Content = " Done ", Margin = new Thickness(12.0, 0.0, 4.0, 0.0), Focusable = false };
                            backBtn.Click += (_, _) => backspace();
                            clearBtn.Click += (_, _) => { cur = ""; updateDisplay(); };
                            doneBtn.Click += (_, _) => wh.Set();
                            buttons.Children.Add(backBtn);
                            buttons.Children.Add(clearBtn);
                            buttons.Children.Add(doneBtn);
                            sp.Children.Add(buttons);
                            var border = new Border { Child = sp, Background = Brushes.Black, BorderThickness = new Thickness(4.0), BorderBrush = Brushes.Gray, Focusable = true };
                            border.AddHandler(InputElement.KeyDownEvent, new EventHandler<KeyEventArgs>((_, ea) =>   // (WPF: PreviewKeyDown)
                            {
                                if (ea.Key == Key.Up)
                                {
                                    ea.Handled = true;
                                    add(kU);
                                }
                                else if (ea.Key == Key.Down)
                                {
                                    ea.Handled = true;
                                    add(kD);
                                }
                                else if (ea.Key == Key.Left)
                                {
                                    ea.Handled = true;
                                    add(kL);
                                }
                                else if (ea.Key == Key.Right)
                                {
                                    ea.Handled = true;
                                    add(kR);
                                }
                                else if (ea.Key == Key.Back)
                                {
                                    ea.Handled = true;
                                    backspace();
                                }
                                else if (ea.Key == Key.Enter)
                                {
                                    ea.Handled = true;
                                    wh.Set();
                                }
                            }), RoutingStrategies.Tunnel);
                            border.Loaded += (_, _) => border.Focus();
                            HotKeys.suspendHotKeys = true;   // the hint entry uses the arrow keys itself, so the global hotkeys (cursor movement etc.) must not take them
                            try
                            {
                                await CustomComboBoxes.DoModal(cm, wh, pos.X, pos.Y, border);
                            }
                            finally
                            {
                                HotKeys.suspendHotKeys = false;
                            }
                            hint = cur;
                            refreshLabel();
                            if (name == "Dead Woods") { OverworldData.DeadWoodsHintKnown = (hint != ""); OverworldData.DeadWoodsHint = hint; } else { OverworldData.LostHillsHintKnown = (hint != ""); OverworldData.LostHillsHint = hint; }
                            // once a hint is entered the button gets a green border
                            btn.BorderBrush = (hint != "" ? Brushes.LimeGreen : defaultBorderBrush);
                            btn.BorderThickness = (hint != "" ? new Thickness(2.0) : defaultBorderThickness);
                            TrackerModel.forceUpdate();
                            popupIsActive = false;
                            doUIUpdateEvent.Trigger();
                        }
                        CustomComboBoxes.StartImmediate(go());
                    }
                };
                // once a hint has been entered, hovering the button shows it in a much larger popup so it is easy to read at a glance
                btn.PointerEntered += (_, _) =>
                {
                    if (hint != "" && !popupIsActive && TrackerModelOptions.HintHoverTooltip.Value)
                    {
                        var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(12.0, 6.0, 12.0, 6.0) };
                        sp.Children.Add(new TextBlock { Text = name, FontSize = 27.0, Foreground = Brushes.Orange, HorizontalAlignment = HorizontalAlignment.Center });
                        sp.Children.Add(new TextBlock { Text = string.Join("  ", hint.ToCharArray().Select(ch => ch.ToString())), FontSize = 45.0, FontWeight = FontWeight.Bold,
                                                        Foreground = Brushes.Orange, HorizontalAlignment = HorizontalAlignment.Center });
                        var big = new Border { Child = sp, Background = Brushes.Black, BorderThickness = new Thickness(3.0), BorderBrush = Brushes.Gray, IsHitTestVisible = false };
                        var pos = btn.TranslatePoint(new Point(0.0, btn.Bounds.Height + 4.0), cm.AppMainCanvas).Value;
                        layout.ClearTopLayerHovers();
                        layout.AddTopLayerHover(big, pos.X, pos.Y);
                    }
                };
                btn.PointerExited += (_, _) => layout.ClearTopLayerHovers();
                // right click does the same as left click (opens the hint entry)
                btn.PointerReleased += (_, ea) =>   // (WPF: MouseRightButtonUp)
                {
                    if (ea.InitialPressMouseButton == MouseButton.Right)
                    {
                        ea.Handled = true;
                        btn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                };
                return btn;
            }
            // the two hint buttons are stacked, no heading
            var hintButtons = new StackPanel { Orientation = Orientation.Horizontal };
            var deadWoodsButton = makeHintButton("Dead Woods", "DW");
            hintButtons.Children.Add(deadWoodsButton);
            var lostHillsButton = makeHintButton("Lost Hills", "LH");
            var hintBtnNormalBrush = lostHillsButton.BorderBrush;
            var hintBtnNormalThickness = lostHillsButton.BorderThickness;
            hintButtonHighlightHook = (zone, on) =>
            {
                // a bright red glow, a thick red border, and a red-filled label, so it is obvious which button the hovered tile is waiting on
                var b = zone == 1 ? deadWoodsButton : lostHillsButton;
                var label = (TextBox)b.Content;
                var known0 = (zone == 1 ? OverworldData.DeadWoodsHintKnown : OverworldData.LostHillsHintKnown);
                if (on && !known0)   // (once the hint has been entered the tiles are no longer waiting on it: no red highlight)
                {
                    b.BorderBrush = Brushes.Red;
                    b.BorderThickness = new Thickness(3.0);
                    b.Effect = new DropShadowEffect { Color = Colors.Red, BlurRadius = 18.0, OffsetX = 0.0, OffsetY = 0.0, Opacity = 1.0 };   // (WPF: ShadowDepth=0)
                    label.Background = Brushes.Red;
                    label.Foreground = Brushes.White;
                    label.FontWeight = FontWeight.Bold;
                }
                else
                {
                    var known = (zone == 1 ? OverworldData.DeadWoodsHintKnown : OverworldData.LostHillsHintKnown);
                    b.BorderBrush = (known ? Brushes.LimeGreen : hintBtnNormalBrush);   // (a button whose hint has been entered keeps its green border)
                    b.BorderThickness = (known ? new Thickness(2.0) : hintBtnNormalThickness);
                    b.Effect = null;
                    label.Background = Brushes.Transparent;   // (transparent, so the button's own dark-gray fill shows, like the other buttons)
                    label.Foreground = Brushes.Orange;
                    label.FontWeight = FontWeight.Normal;
                }
            };
            lostHillsButton.Margin = new Thickness(4.0, 0.0, 0.0, 0.0);
            hintButtons.Children.Add(lostHillsButton);
            canvasAdd(legendCanvas, hintButtons, UIComponents.legendAfterStartX + 28.0 + 8.0, 0.0);
            void placeHints()
            {
                Canvas.SetTop(hintButtons, 4.0);   // side by side, where the 1Q/2Q button used to be (just right of the Start Spot box); 4px down like the Start Spot box
                Canvas.SetLeft(hintButtons, UIComponents.legendAfterStartX);
            }
            hintButtons.Loaded += (_, _) => placeHints();
            placeHints();
        }
        // 'Starting items and extra drops' icon: moved from the dungeon column grid to just right of the last item (meat) in the item progress row
        var extrasImageOldParent = extrasImage.Parent as Panel;
        if (extrasImageOldParent != null)
            extrasImageOldParent.Children.Remove(extrasImage);
        canvasAdd(legendCanvas, extrasImage, ITEM_PROGRESS_FIRST_ITEM + 14.0 * 30.0 - 4.0, 36.0);   // nudged 3px left
        extrasImage.IsVisible = false;   // the 'Starting items and extra drops' button is gone: starting items are now tracked by clicking the item progress bar
        // the meat icon is the last item in the progress row: its top is 3px into the row's canvas, which is itself 3px below the legend row
        var meatBottomY = THRU_MAP_AND_LEGEND_H + 3.0 + 3.0 + (double)Graphics.bait_bmp.Height;   // absolute y of the bottom of the meat icon
        var (redrawItemProgressBar, itemProgressCanvas, itemProgressTB) = UIComponents.MakeItemProgressBar(owInstance);
        UIComponents.uncollectMapIcons = idents =>
        {
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    var packed = TrackerModel.owSwordIcons[i, j];
                    var entries = TrackerModel.decorEntriesOf(packed);
                    if (entries.Any(e => e.Item2 && idents.Contains(e.Item1)))
                        setDecorIcons(i, j, TrackerModel.decorEntriesPack(ListModule.OfSeq(entries.Select(e => Tuple.Create(e.Item1, e.Item2 && !(idents.Contains(e.Item1)))))));
                }
            }
        };
        UIComponents.itemProgressChanged = () =>
        {
            // recompute the player state right away (instead of waiting for the periodic poll) so the icon changes instantly, then do the full refresh
            if (!(TrackerModel.recomputeWhatIsNeeded())) TrackerModel.recomputePlayerStateSummary();
            redrawItemProgressBar();
            doUIUpdateEvent.Trigger();
        };
        DockPanel make_itemProgressHoverTarget()
        {
            var dp = new DockPanel { Background = Brushes.Transparent };  // Background to get mouse events
            dp.Children.Add(itemProgressTB);
            dp.PointerEntered += (_, _) =>
            {
                spotSummaryCanvas.Children.Clear();
                itemProgressTB.BorderBrush = Brushes.DarkTurquoise;   // (click it for the remaining-items popout; no hover box)
            };
            dp.PointerExited += (_, _) => { itemProgressTB.BorderBrush = Graphics.roundedBorderBrush; spotSummaryCanvas.Children.Clear(); };
            dp.PointerPressed += (_, ea) =>
            {
                var isRight = ea.GetCurrentPoint(dp).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed;   // (WPF: ea.ChangedButton = Right)
                Popouts.makePopout(Popouts.PopoutKind.RemainingItemsPoput, !isRight, isRight, doUIUpdateEvent);
            };
            return dp;
        }
        var itemProgressHoverTarget = make_itemProgressHoverTarget();
        layout.AddItemProgress(itemProgressCanvas, itemProgressHoverTarget);
        MainTrackerTriforcesAndItems.itemProgressSource = itemProgressCanvas;


        // Version
        var vb = CustomComboBoxes.makeVersionButtonWithBehavior(cm);
        blackenButton(vb);
        // vb.Click.Add(fun _ -> failwith "crash")                     // Uncomment this for crash testing
        layout.AddVersionButton(vb);

        // hint decoder
        if (isStandardHyrule)   // Hints only apply to z1r and standard map zones
        {
            var hintDecoderUI = UIComponents.MakeHintDecoderUI(cm);
            blackenButton(hintDecoderUI);
            layout.AddHintDecoderButton(hintDecoderUI);
        }

        // WANT!
#if NOT_RACE_LEGAL
        var kittyBoxWidth = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H - THRU_MAIN_MAP_H;   // the space (63x63) the old picture occupied; neighboring buttons are positioned relative to it
#endif
        var kitty = new Image();   // the custom logo
        var imageStream = Graphics.GetResourceStream("CustomLogo.png");
        kitty.Source = new Avalonia.Media.Imaging.Bitmap(imageStream);   // (WPF: BitmapFrame.Create)
        kitty.Width = 30.0;     // leaves room for the two ZORA link lines underneath
        kitty.Height = 30.0;
        var ztlogo = new Image();
        imageStream = Graphics.GetResourceStream("ZTlogo64x64.png");
        ztlogo.Source = new Avalonia.Media.Imaging.Bitmap(imageStream);
        ztlogo.Width = 20.0;
        ztlogo.Height = 20.0;
        var logoBorder = new Border { BorderThickness = new Thickness(1.0), BorderBrush = Brushes.Gray, Child = ztlogo };
        var discordUrl = "https://discord.gg/zescSjDtme";
        var discordLink = Layout.MakeDiscordLink(discordUrl);
        var racingLink = Layout.MakeLinkText("Z1R Racing Discord", "https://discord.gg/z1rracing", Layout.RacingLinkWidth);
        layout.AddKittyAndLogo(kitty, logoBorder, ztlogo, discordLink, racingLink);
        var zoraSiteLink = Layout.MakeLinkText("ZORA Site", "https://zora.gg/", Layout.DiscordLinkWidth);
        var racingSiteLink = Layout.MakeLinkText("Z1R Racing Site", "https://z1rracing.com/", Layout.RacingLinkWidth);
        layout.AddExtraSiteLinks(zoraSiteLink, racingSiteLink);
        discordLink.SizeChanged += (_, _) =>
        {
            var d = discordLink;
            if (d.Bounds.Height > 0.0)   // (WPF: ActualHeight)
            {
                Canvas.SetTop(d, meatBottomY - d.Bounds.Height);   // bottom-align with the meat icon
                var r = racingLink;
                Canvas.SetTop(r, meatBottomY - d.Bounds.Height - (r.Bounds.Height > 0.0 ? r.Bounds.Height : 14.0));   // the Z1R Racing Discord link sits directly above it
                Canvas.SetLeft(r, Canvas.GetLeft(d) + (Layout.DiscordLinkWidth - Layout.RacingLinkWidth) / 2.0);
            }
        };

        // triforce decoder chart, left of the ZORA logo: 1Q by default, 2Q for Second Quest / Mixed Second Quest overworlds
        // (follows the start screen choice and the 1Q/2Q button, since both go through TrackerModel.owInstance)
        bool questRefIsSecond() =>
            TrackerModel.owInstance.Quest.Equals(OverworldData.OWQuest.SECOND) || TrackerModel.owInstance.Quest.Equals(OverworldData.OWQuest.MIXED_SECOND);
        Control makeQuestRefChart()
        {
            var haves = TrackerModel.GetTriforceHaves();
            if (questRefIsSecond()) return Dungeon.DrawTriforces2Q(haves); else return Dungeon.DrawTriforces1Q(haves);
        }
        var _questRefLogoX = 16.0 * OMTW - Layout.kittyWidth - 12.0 + (Layout.kittyWidth - kitty.Width) / 2.0;
        var questRefH0 = Layout.kittyWidth - 4.0;   // the original height
        var questRefH = questRefH0;   // the chart itself is its original size; only the hover window is larger
        var questRefViewbox = new Viewbox { Stretch = Stretch.Uniform, Height = questRefH };
        var questRefBorder = new Border { BorderThickness = new Thickness(0.0), Background = Brushes.Transparent, Child = questRefViewbox };
        questRefBorder.Height = questRefH;
        questRefBorder.Width = questRefH * 340.0 / 180.0;
        void updateQuestRef()
        {
            var chart = makeQuestRefChart();
            ((Canvas)chart).Background = Brushes.Transparent;   // transparent outside the triforce shapes
            // clicking a triforce piece in the chart collects it (or un-collects it) like clicking that dungeon's triforce above
            var chartCanvas = (Canvas)chart;
            var labels = questRefIsSecond() ? "13254687" : "12345678";
            for (int k0 = 0; k0 <= 7; k0++)
            {
                var k = k0;
                if (chartCanvas.Children.Count > 2 * k)
                {
                    var poly = chartCanvas.Children[2 * k];
                    poly.PointerPressed += (_, ea) =>   // (WPF: MouseLeftButtonDown)
                    {
                        if (ea.GetCurrentPoint(poly).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed) return;
                        ea.Handled = true;
                        if (!popupIsActive)
                        {
                            int? dungeonIndex;
                            if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
                            {
                                dungeonIndex = null;
                                for (int d = 0; d <= 7; d++)
                                    if (TrackerModel.GetDungeon(d).LabelChar == labels[k]) { dungeonIndex = d; break; }
                            }
                            else dungeonIndex = (int)labels[k] - (int)'1';
                            if (dungeonIndex is int d2 && !Views.triforceBlocked[d2]) TrackerModel.GetDungeon(d2).ToggleTriforce();
                        }
                    };
                }
            }
            questRefViewbox.Child = chart;
        }
        updateQuestRef();
        var lastQuestRefKey = "";
        // hovering the chart shows it at 2x size (current state), above the chart, kept inside the window
        // (only while the mouse is inside the big outer triangle of the chart: the 340x180 drawing's apex is at the top center and its base line is at y=171)
        var questRefHoverShown = false;
        bool mouseInsideQuestRefTriangle(Point p)
        {
            var s = questRefH / 180.0;
            var (x, y) = (p.X / s, p.Y / s);
            return y >= 1.0 && y <= 172.0 && Math.Abs(x - 170.0) <= (y - 1.0) + 1.0;
        }
        void showQuestRefHover()
        {
            var bigH = questRefH * 3.0;   // hover window: 50% bigger than the original 2x
            var bigViewbox = new Viewbox { Stretch = Stretch.Uniform, Height = bigH, Child = makeQuestRefChart() };
            var big = new Border { Child = bigViewbox, Background = Brushes.Black, BorderThickness = new Thickness(3.0), BorderBrush = Brushes.Gray, IsHitTestVisible = false };
            var (w, h) = (bigH * 340.0 / 180.0 + 6.0, bigH + 6.0);
            var pos = questRefBorder.TranslatePoint(new Point(0.0, 0.0), cm.AppMainCanvas).Value;
            var x = Math.Max(0.0, Math.Min(pos.X, (cm.AppMainCanvas.Width - w - 2.0)));
            var y = Math.Max(0.0, (pos.Y - h - 4.0));
            layout.ClearTopLayerHovers();
            layout.AddTopLayerHover(big, x, y);
        }
        questRefBorder.PointerMoved += (_, ea) =>
        {
            var inside = mouseInsideQuestRefTriangle(ea.GetPosition(questRefBorder));
            if (inside && !questRefHoverShown)
            {
                questRefHoverShown = true;
                showQuestRefHover();
            }
            else if (!inside && questRefHoverShown)
            {
                questRefHoverShown = false;
                layout.ClearTopLayerHovers();
            }
        };
        questRefBorder.PointerExited += (_, _) =>
        {
            if (questRefHoverShown)
            {
                questRefHoverShown = false;
                layout.ClearTopLayerHovers();
            }
        };
        onEvent(TrackerModel.overworldQuestChanged.Publish, _ => updateQuestRef());
        for (int i = 0; i <= 7; i++)
        {
            onEvent(TrackerModel.GetDungeon(i).PlayerHasTriforceChanged, _ => updateQuestRef());
            // a triforce piece marked as obtained in a dungeon item box also lights its piece in the chart
            foreach (var b in TrackerModel.GetDungeon(i).Boxes)
                onEvent(b.Changed, _ => updateQuestRef());
        }
        layout.AddQuestReferenceChart(questRefBorder, BLOCKERS_AND_NOTES_OFFSET + 108.0 + 12.0 - 20.0 + 12.0 - 9.0 * questRefH0 / 180.0 - (questRefH - questRefH0) * 340.0 / 180.0, meatBottomY - questRefH + 3.0);   // the chart's bottom-left corner (drawn 9/180 of its height in from its edge) lines up with the left side of the first blocker box (blocker column is 108 wide, then a 12px dungeon-number label)   // bottom-aligned with the meat icon
        // Legacy Mode: LEGACY up the left slanted side of the chart and MODE down the right slanted side (just outside the triangle; not clickable)
        var legacyOverlay = new Canvas { Width = questRefH * 340.0 / 180.0, Height = questRefH, IsHitTestVisible = false };
        void legacySide(string txt, double cx, double cy, double angle)
        {
            var tb = new TextBlock { Text = txt, Foreground = Brushes.Orange, FontSize = 10.0, FontWeight = FontWeight.Bold, Width = 60.0, Height = 14.0, TextAlignment = TextAlignment.Center, IsHitTestVisible = false };
            tb.RenderTransformOrigin = RelativePoint.Center;
            tb.RenderTransform = new RotateTransform(angle);
            Canvas.SetLeft(tb, cx - 30.0);
            Canvas.SetTop(tb, cy - 7.0);
            legacyOverlay.Children.Add(tb);
        }
        var legacyScale = questRefH / 180.0;
        legacySide("LEGACY", 85.0 * legacyScale - 7.0, 86.0 * legacyScale - 7.0, -45.0);
        legacySide("MODE", 255.0 * legacyScale + 7.0, 86.0 * legacyScale - 7.0, 45.0);
        void updateLegacyOverlay() => legacyOverlay.IsVisible = TrackerModel.LegacyMode;
        updateLegacyOverlay();
        onEvent(TrackerModel.overworldQuestChanged.Publish, _ => updateLegacyOverlay());
        layout.AddQuestReferenceChart(legacyOverlay, BLOCKERS_AND_NOTES_OFFSET + 108.0 + 12.0 - 20.0 + 12.0 - 9.0 * questRefH0 / 180.0 - (questRefH - questRefH0) * 340.0 / 180.0, meatBottomY - questRefH + 3.0);
        // the blue 3-dot box's bottom edge is lined up with the drawn base line of the triforce chart, measured from the real on-screen positions
        // (the chart is a 340x180 drawing whose base line, 2px stroke included, is at y=171, scaled to fit questRefH)
        void alignExtrasWithChart()
        {
            try
            {
                if (extrasImage.Bounds.Height > 0.0 && questRefBorder.Bounds.Height > 0.0 && questRefBorder.IsLoaded && extrasImage.IsLoaded)
                {
                    var chartTopInLegend = questRefBorder.TranslatePoint(new Point(0.0, 0.0), legendCanvas).Value.Y;
                    Canvas.SetTop(extrasImage, chartTopInLegend + questRefH * 171.0 / 180.0 - extrasImage.Bounds.Height + 3.0);   // +3: the icon image has a little transparent margin below the visible blue box
                }
            }
            catch (Exception) { }
        }
        extrasImage.SizeChanged += (_, _) => alignExtrasWithChart();
        questRefBorder.SizeChanged += (_, _) => alignExtrasWithChart();
        extrasImage.Loaded += (_, _) => alignExtrasWithChart();
        questRefBorder.Loaded += (_, _) => alignExtrasWithChart();


        // show hotkeys button
        var showHotKeysButton = Graphics.makeButton("Hotkeys", 12.0, Brushes.Orange);   // same style as the Save and Hint Decoder buttons
        blackenButton(showHotKeysButton);
        layout.AddShowHotKeysButton(showHotKeysButton);
        showHotKeysButton.Click += (_, _) => Popouts.showHotKeys(false);
        showHotKeysButton.PointerPressed += (_, ea) =>   // (WPF: MouseRightButtonDown)
        {
            if (ea.GetCurrentPoint(showHotKeysButton).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed) Popouts.showHotKeys(true);
        };

        // show/run custom button
        var showRunCustomButton = Graphics.makeButton("Show/Run Custom", 16.0, Brushes.Orange);   // same style/size as Spot Summary and Pause/Reset timer
        blackenButton(showRunCustomButton);
        layout.AddShowRunCustomButton(showRunCustomButton);
        showRunCustomButton.Click += (_, _) => ShowRunCustom.DoShowRunCustom(refocusMainWindow);

        var saveButton = Graphics.makeButton("Save", 12.0, Brushes.Orange);
        saveButton.Width = 84.0;   // same style as the other main-screen buttons
        blackenButton(saveButton);
        ToolTip.SetTip(saveButton, "Save the current tracker state to a file");
        layout.AddSaveButton(saveButton);
        // stack (bottom to top): ZORA Discord link, Z1R Racing Discord link, centered on the discord links (Save now sits above the Hint Decoder button, placed in Layout)
        void placeLinkStack()
        {
            var d = discordLink;
            var zs = zoraSiteLink;
            if (d.Bounds.Height > 0.0 && zs.Bounds.Height > 0.0 && !(double.IsNaN(Canvas.GetLeft(d))))
            {
                var cx = Canvas.GetLeft(d) + Layout.DiscordLinkWidth / 2.0 + 4.0;   // +4: centered over the three boxes below (measured from a screenshot; tweak if still off)
                // top to bottom: ZORA logo, ZORA Site, ZORA Discord; the stack ends at the bottom of the strip of space (the old logo area) to the right of the map
                void place(Control e, double y)
                {
                    Canvas.SetTop(e, y);
                    Canvas.SetLeft(e, cx - e.Bounds.Width / 2.0);
                }
                var bottom = THRU_MAIN_MAP_H + Layout.kittyWidth;
                var dTop = bottom - d.Bounds.Height;
                var zTop = dTop - zs.Bounds.Height;
                place(zs, zTop);
                place(d, dTop);
                Canvas.SetTop(kitty, zTop - kitty.Height - 1.0);
                Canvas.SetLeft(kitty, cx - kitty.Width / 2.0);
            }
        }
        foreach (var e in new[] { discordLink, zoraSiteLink })
            e.SizeChanged += (_, _) => placeLinkStack();
        saveButton.Click += (_, _) =>
        {
            if (!popupIsActive)
            {
                popupIsActive = true;
                async Task go()
                {
                    // (F#: `try ... with e -> ...` around code that awaits; C# can't await in a catch block, so the error is kept and handled after it)
                    Exception error = null;
                    try
                    {
                        // ask for an optional name for the save file (blank = the usual date-time name; clicking outside the box or Cancel saves nothing)
                        var nameWh = new ManualResetEvent(false);
                        string chosen = null;
                        var nameTitle = new TextBox { IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 16.0, Margin = new Thickness(3.0), Foreground = Brushes.Orange, Background = Brushes.Black, Text = "Name this save (optional)\nBlank uses the date and time" };
                        var nameTB = new TextBox { FontSize = 16.0, Foreground = Brushes.Orange, Background = Brushes.Black, CaretBrush = Brushes.Orange, MaxLength = 60, Width = 360.0, Margin = new Thickness(3.0, 6.0, 3.0, 6.0) };
                        var nameSaveBtn = new Button { Content = " Save ", Margin = new Thickness(3.0), Focusable = false };
                        var nameCancelBtn = new Button { Content = " Cancel ", Margin = new Thickness(3.0), Focusable = false };
                        void finishName()
                        {
                            chosen = nameTB.Text ?? "";
                            nameWh.Set();
                        }
                        nameSaveBtn.Click += (_, _) => finishName();
                        nameCancelBtn.Click += (_, _) => nameWh.Set();
                        nameTB.KeyDown += (_, ea) =>
                        {
                            if (ea.Key == Key.Enter)
                            {
                                ea.Handled = true;
                                finishName();
                            }
                            else if (ea.Key == Key.Escape)
                            {
                                ea.Handled = true;
                                nameWh.Set();
                            }
                        };
                        nameTB.Loaded += (_, _) => nameTB.Focus();
                        var nameButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                        nameButtons.Children.Add(nameSaveBtn);
                        nameButtons.Children.Add(nameCancelBtn);
                        var nameSp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(6.0) };
                        nameSp.Children.Add(nameTitle);
                        nameSp.Children.Add(nameTB);
                        nameSp.Children.Add(nameButtons);
                        var nameBorder = new Border { Child = nameSp, Background = Brushes.Black, BorderThickness = new Thickness(4.0), BorderBrush = Brushes.Gray };
                        // (the Save / Cancel buttons were invisible (no fill, no border, black text) with only the app-wide style; give the dialog the rounded dark button look the message boxes use)
                        nameBorder.Styles.Add(Graphics.roundedButtonStyle());
                        nameBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        var npx = Math.Max(0.0, ((cm.AppMainCanvas.Width - nameBorder.DesiredSize.Width) / 2.0));
                        var npy = Math.Max(0.0, ((cm.AppMainCanvas.Height - nameBorder.DesiredSize.Height) / 2.0));
                        HotKeys.suspendHotKeys = true;   // typing a name: the global hotkeys (letters, numbers) must not take the keys
                        await CustomComboBoxes.DoModal(cm, nameWh, npx, npy, nameBorder);
                        HotKeys.suspendHotKeys = false;
                        if (chosen == null) popupIsActive = false;
                        else
                        {
                            var typedName = chosen;
                            SaveAndLoad.pendingManualSaveName = typedName;
                            var filename = MakeManualSave();
                            filename = System.IO.Path.GetFileName(filename);  // remove directory info (could have username in path, don't display PII on-screen)
                            var r = await CustomComboBoxes.DoModalMessageBox(cm, null, $"ZORA Tracker data saved to file\n{filename}", new[] { "Ok" });
                            _ = r;
                            popupIsActive = false;
                        }
                    }
                    catch (Exception e)
                    {
                        error = e;
                    }
                    if (error != null)
                    {
                        HotKeys.suspendHotKeys = false;
                        SaveAndLoad.pendingManualSaveName = "";
                        var r = await CustomComboBoxes.DoModalMessageBox(cm, System.Drawing.SystemIcons.Error, $"ZORA Tracker was unable to save the\ntracker state to a file\nError:\n{error.Message}", new[] { "Ok" });
                        _ = r;
                        popupIsActive = false;
                    }
                }
                CustomComboBoxes.StartImmediate(go());
            }
        };

        var uccTB = new TextBox { FontSize = 12.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, BorderThickness = new Thickness(0.0),
                                            Text = "UCC", IsHitTestVisible = false, TextAlignment = TextAlignment.Center };
        var uccButton = new Button { Content = uccTB };
        if (System.IO.File.Exists(UserCustomLayer.checklistFilename))
        {
            layout.AddUserCustomContentButton(uccButton);
            uccButton.Click += (_, _) =>
            {
                if (!popupIsActive)
                {
                    popupIsActive = true;
                    async Task go()
                    {
                        await UserCustomLayer.InteractWithUserCustom(cm, timelineItems, invokeExtras);
                        popupIsActive = false;
                    }
                    CustomComboBoxes.StartImmediate(go());
                }
            };
        }

#if NOT_RACE_LEGAL
        // minimap overlay button
        var minimapOverlayTB = new TextBox { FontSize = 12.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, BorderThickness = new Thickness(0.0), Text = "Minimap overlay", IsHitTestVisible = false };
        var minimapOverlayButton = new Button { Content = minimapOverlayTB };
        canvasAdd(appMainCanvas, minimapOverlayButton, 16.0 * OMTW - kittyBoxWidth - 115.0, THRU_MAIN_MAP_H + 22.0);
        Action<bool> minimapOverlay = _irc => { };
        minimapOverlayButton.Click += (_, _) => minimapOverlay(false);
        minimapOverlayButton.PointerPressed += (_, ea) => { if (ea.GetCurrentPoint(minimapOverlayButton).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed) minimapOverlay(true); };

        // near-mouse HUD button
        var nearMouseHUDTB = new TextBox { FontSize = 12.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, BorderThickness = new Thickness(0.0), Text = "Near-mouse HUD", IsHitTestVisible = false };
        var nearMouseHUDButton = new Button { Content = nearMouseHUDTB };
        canvasAdd(appMainCanvas, nearMouseHUDButton, 16.0 * OMTW - kittyBoxWidth - 115.0, THRU_MAIN_MAP_H + 44.0);
        Action<bool> nearMouseHUD = _irc => { };
        nearMouseHUDButton.Click += (_, _) => nearMouseHUD(false);
        nearMouseHUDButton.PointerPressed += (_, ea) => { if (ea.GetCurrentPoint(nearMouseHUDButton).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed) nearMouseHUD(true); };
#endif

        await showProgress("misc");

        var blockerDungeonSunglasses = new Control[8];
        (TrackerModel.LastChangedTime, Action)? oneTimeRemindLadder = null, oneTimeRemindAnyKey = null;
        OverworldItemGridUI.refreshUIAfterOverworldTemplateChange = () => doUIUpdateEvent.Trigger();
        decorStateChanged = () => doUIUpdateEvent.Trigger();   // marker-icon check-offs refresh the progress bar etc.
        doUIUpdateEvent.Fired += () =>
        {
//          let diagSW = System.Diagnostics.Stopwatch.StartNew()
            if (displayIsCurrentlyMirrored != TrackerModel.MirrorOverworld)
            {
                // model changed, align the view
                displayIsCurrentlyMirrored = !displayIsCurrentlyMirrored;
                if (displayIsCurrentlyMirrored)
                {
                    foreach (var fe in mirrorOverworldFEs)
                    {
                        // (flip about the element's own center: elements not yet measured, like the lazily-built coords grid, have ActualWidth 0 here)
                        var scaleTrans = new ScaleTransform(-1.0, 1.0);   // (WPF freezes it; Avalonia has no Freeze)
                        fe.RenderTransformOrigin = RelativePoint.Center;
                        fe.RenderTransform = scaleTrans;
                    }
                }
                else
                {
                    foreach (var fe in mirrorOverworldFEs)
                        fe.RenderTransform = null;
                }
            }
            // redraw triforce display (some may have located/unlocated/hinted)
            updateNumberedTriforceDisplayIfItExists();
            // redraw white/magical swords (may have located/unlocated/hinted)
            redrawWhiteSwordCanvas(white_sword_canvas);
            redrawMagicalSwordCanvas(mags_canvas);
            // update specific-blockers that may have been (un)blocked
            foreach (var f in Views.redrawBoxes) f();
            foreach (var f in Views.redrawTriforces) f();
            // the triforce reference chart follows the triforces however they were marked (numbered triforce, chart click, or a triforce piece in an item box)
            var havesKey = string.Join(",", TrackerModel.GetTriforceHaves().Select(b => b ? "1" : "0")) + (questRefIsSecond() ? "2" : "1");
            if (havesKey != lastQuestRefKey)
            {
                lastQuestRefKey = havesKey;
                updateQuestRef();
            }
            // update overworld marks (may be hiding useless shops, and they just got an item rendering a shop useless (e.g. magic key and key shops))
            OptionsMenu.triggerRequestRedrawOverworldEvent();

            recorderingCanvas.Children.Clear();
            // TODO event for redraw item progress? does any of this event interface make sense? hmmm
            redrawItemProgressBar();

            // Progressive Items: each family (1 swords, 2 rings, 3 candles, 4 arrows) has a tier that follows the number collected; when it goes up, the others glow and the alert plays
            for (int f = 1; f <= 5; f++) progressiveLastTiers[f] = Math.Max(1, TrackerModel.progressiveFamilyTier(f));
            List<int> progressiveIdentsOf(int f) => Enumerable.Range(0, 128).Where(id => TrackerModel.progressiveFamily(id) == f).ToList();
            var progressiveAutoChecking = false;
            progressiveSwordCheckHook = () =>
            {
                for (int f0 = 1; f0 <= 5; f0++)
                {
                    var f = f0;
                    var t = TrackerModel.progressiveFamilyTier(f);
                    var old = progressiveLastTiers[f];
                    if (t != old)
                    {
                        ClickTrace.Log($"progressive family {f}: tier {old} -> {t}");
                        progressivePrevTiers[f] = old;
                        progressiveChangedAt[f] = DateTime.Now;
                        progressiveLastTiers[f] = t;
                        if (t < old)
                        {
                            // an item was un-collected: the ones still collected move down a tier (e.g. the white sword shows as the wood sword again), in the order they were collected
                            var ids = progressiveIdentsOf(f);
                            var recs = new List<(int, Action<int>)>();
                            foreach (var kv in TrackerModel.progressiveTileTier.ToList())
                            {
                                var id = kv.Key.Item3;
                                if (ids.Contains(id)) recs.Add((kv.Value, v => TrackerModel.progressiveTileTier[kv.Key] = v));
                            }
                            foreach (var kv in TrackerModel.progressiveBoxTier.ToList())
                                if (TrackerModel.progressiveFamily(kv.Key.CellCurrent()) == f && kv.Key.PlayerHas().Equals(TrackerModel.PlayerHas.YES)) recs.Add((kv.Value, v => TrackerModel.progressiveBoxTier[kv.Key] = v));
                            var unrecorded = Math.Max(0, ((t - 1) - recs.Count));   // (collected some other way, e.g. the sword taken at the wood sword cave: those take the lowest tiers)
                            var cap = f == 1 ? 4 : 2;
                            var sorted = recs.OrderBy(rc => rc.Item1).ToList();   // (F#'s Seq.sortBy is stable, as is OrderBy)
                            for (int k = 0; k < sorted.Count; k++) sorted[k].Item2(Math.Min(cap, (unrecorded + k + 1)));
                            // the upgrade entries above the new tier are no longer collected: take them off the timeline (a removed item otherwise leaves its old entry behind)
                            var staleNames = f switch
                            {
                                1 => new[] { (1, "WoodSword"), (2, "WhiteSword"), (3, "MagicalSword"), (4, "Sword4") },
                                2 => new[] { (2, "RedRing") },
                                3 => new[] { (2, "RedCandle") },
                                5 => new[] { (1, "Boomerang"), (2, "MagicBoomerang") },
                                _ => new[] { (2, "SilverArrow") },
                            };
                            foreach (var (k, name) in staleNames)
                                if (k >= t)
                                    if (tryTimelineItem(name, out var tim) && tim.FinishedTotalSeconds != -1) tim.Unstamp();
                        }
                        TrackerModel.progressiveTierChanged.Trigger(null);
                        for (int x = 0; x <= 15; x++)
                            for (int y = 0; y <= 7; y++)
                                redrawOwTileHook(x, y);
                        if (t > old && old > 0)
                        {
                            var ids = progressiveIdentsOf(f);
                            var tiles = new List<(int, int)>();
                            for (int x = 0; x <= 15; x++)
                                for (int y = 0; y <= 7; y++)
                                    if (TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[x, y]).Any(e => ids.Contains(e.Item1) && !e.Item2)) tiles.Add((x, y));
                            var boxes = TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes().Where(b => ids.Contains(b.CellCurrent()) && b.PlayerHas().Equals(TrackerModel.PlayerHas.NO)).ToArray();
                            ClickTrace.Log($"progressive family {f} went up: {tiles.Count} marked map tile(s) and {boxes.Length} box(es) still to collect; the 'Progressive Item Upgraded' notification is {(tiles.Count != 0 || boxes.Length > 0 ? "sent" : "NOT sent (nothing else of this family is marked)")}");
                            if (tiles.Count != 0 || boxes.Length > 0)
                            {
                                foreach (var b in boxes) TrackerModel.progressiveBoxGlow.Trigger(b);
                                progressiveUpgradeHook(tiles, progressiveFamilyTierBmp(f, old), progressiveFamilyTierBmp(f, t));
                            }
                        }
                        // once the click that caused this has finished (other handlers may still be updating), draw everything again from the final state
                        async Task go()
                        {
                            await Task.Delay(150);
                            TrackerModel.progressiveTierChanged.Trigger(null);
                            for (int x = 0; x <= 15; x++)
                                for (int y = 0; y <= 7; y++)
                                    redrawOwTileHook(x, y);
                        }
                        CustomComboBoxes.StartImmediate(go());
                    }
                }
                // once the last upgrade of a family has been collected (4 swords, 2 rings / candles / arrows), every other copy of it still to be collected is checked off too
                if (!progressiveAutoChecking)
                {
                    progressiveAutoChecking = true;
                    try
                    {
                        for (int f = 1; f <= 5; f++)
                        {
                            var cap = f == 1 ? 4 : 2;
                            var ids = progressiveIdentsOf(f);
                            var boxesAll = TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes();
                            var n = (ids.Sum(id => TrackerModel.mapCollectedCount[id])) + (boxesAll.Count(b => ids.Contains(b.CellCurrent()) && b.PlayerHas().Equals(TrackerModel.PlayerHas.YES)));
                            if (n >= cap)
                            {
                                for (int x = 0; x <= 15; x++)
                                {
                                    for (int y = 0; y <= 7; y++)
                                    {
                                        var es = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[x, y]);
                                        if (es.Any(e => ids.Contains(e.Item1) && !e.Item2))
                                        {
                                            var label = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[x, y]);
                                            var packed = TrackerModel.decorEntriesPack(ListModule.OfSeq(es.Select(e => ids.Contains(e.Item1) ? Tuple.Create(e.Item1, true) : Tuple.Create(e.Item1, e.Item2))));
                                            setDecorIcons(x, y, (label > 0 ? TrackerModel.withShopLabel(packed, label) : packed));
                                            redrawOwTileHook(x, y);
                                        }
                                    }
                                }
                                foreach (var b in boxesAll)
                                    if (ids.Contains(b.CellCurrent()) && b.PlayerHas().Equals(TrackerModel.PlayerHas.NO)) b.SetPlayerHas(TrackerModel.PlayerHas.YES);
                            }
                        }
                    }
                    finally
                    {
                        progressiveAutoChecking = false;
                    }
                }
            };
            if (!progressiveWired)
            {
                progressiveWired = true;
                // collecting (or un-collecting) any item of a family in an item box (dungeon, shop/cave box, ...)
                foreach (var b in TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes())
                {
                    var prev = (b.CellCurrent(), b.PlayerHas());
                    onEvent(b.Changed, _ =>
                    {
                        var (pc, pph) = prev;
                        var (c, ph) = (b.CellCurrent(), b.PlayerHas());
                        prev = (c, ph);
                        if (TrackerModel.progressiveFamily(pc) > 0 || TrackerModel.progressiveFamily(c) > 0)
                            ClickTrace.Log($"item box changed: item {pc}->{c}, has {pph}->{ph} (ProgressiveItems={TrackerModel.ProgressiveItems})");
                        if (TrackerModel.ProgressiveItems)
                        {
                            var nowF = ph.Equals(TrackerModel.PlayerHas.YES) ? TrackerModel.progressiveFamily(c) : 0;
                            var wasF = pph.Equals(TrackerModel.PlayerHas.YES) ? TrackerModel.progressiveFamily(pc) : 0;
                            if (nowF > 0 && (wasF == 0 || pc != c))
                            {
                                TrackerModel.progressiveBoxTier[b] = progressiveCollectedTier(nowF);
                                TrackerModel.progressiveTierChanged.Trigger(null);   // draw the box again now that its tier is known (no flash of the wrong tier)
                                TrackerModel.TimelineItemModel.TriggerTimelineChanged();   // and the timeline entry, which was drawn before the tier was known
                            }
                            else if (wasF > 0 && nowF == 0) TrackerModel.progressiveBoxTier.Remove(b);
                            if (TrackerModel.progressiveFamily(pc) > 0 || TrackerModel.progressiveFamily(c) > 0) progressiveSwordCheckHook();
                        }
                    });
                }
                // items taken from caves / ticked in Item Progress or the starting items also count toward the tiers
                var pp = TrackerModel.playerProgressAndTakeAnyHearts;
                var se = TrackerModel.startingItemsAndExtras;
                foreach (TrackerModel.BoolProperty prop in new[] { pp.PlayerHasWoodSword, se.PlayerHasWhiteSword, se.PlayerHasMagicalSword, pp.PlayerHasBlueRing, se.PlayerHasRedRing, pp.PlayerHasBlueCandle, se.PlayerHasRedCandle, pp.PlayerHasWoodArrow, se.PlayerHasSilverArrow, se.PlayerHasBoomerang, se.PlayerHasMagicBoomerang })
                    onEvent(prop.Changed, _ => { ClickTrace.Log($"item flag changed (ProgressiveItems={TrackerModel.ProgressiveItems}, sword tier now {TrackerModel.progressiveFamilyTier(1)})"); if (TrackerModel.ProgressiveItems) progressiveSwordCheckHook(); });
            }
            // Progressive Items: a notification plus a 3 second glow-and-grow of every upgraded item on the map
            progressiveUpgradeHook = (tiles, fromBmp, toBmp) =>
            {
                SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, "Progressive Item Upgraded", new[] { ReminderTextBox("Progressive Item Upgraded"), upcb(fromBmp), upcb(Graphics.iconRightArrow_bmp), upcb(toBmp) });
                foreach (var (x, y) in tiles)
                {
                    var glow = Views.makeProgressiveGlow(toBmp, 24.0);   // the upgraded item in full colour, on a golden halo
                    canvasAdd(overworldCanvas, glow, (double)x * OMTW + OMTW / 2.0 - 12.0, (double)(y * 11 * 3) + 33.0 / 2.0 - 12.0);
                    async Task go()
                    {
                        await Task.Delay(3000);
                        overworldCanvas.Children.Remove(glow);
                    }
                    CustomComboBoxes.StartImmediate(go());
                }
            };
            TrackerModel.allUIEventingLogic(new TrackerEvents
            {
                currentHearts = h => currentMaxHeartsTextBox.Text = $"Max Hearts: {h}",
                announceConsiderSword2 = () => RemindSword2(),
                announceConsiderSword3 = () =>
                    SendReminderImpl(TrackerModel.ReminderCategory.SwordHearts, "Consider the magical sword", new[] { upcb(Graphics.iconRightArrow_bmp), upcb(Graphics.ow_sword3_item_bmp) },
                                        AsyncBrieflyHighlightAnOverworldLocation(TrackerModel.mapStateSummary.Sword3Location.Item1, TrackerModel.mapStateSummary.Sword3Location.Item2)),
                overworldSpotsRemaining = (remain, gettable) =>
                {
                    owRemainingNumbersTextBox.Text = $"{gettable}/{remain}";   // gettable / total
                    owGettableScreensTextBox.Text = $"{gettable} gettable";
                    for (int k = 0; k <= 6; k++)
                        owSpotTypeCountTBs[k].Text = (TrackerModelOptions.RaceMode.Value ? "n/a" : TrackerModel.owSpotTypeCounts[k].ToString());   // (Race Mode: n/a)
                },
                dungeonLocation = (i, x, y, hasTri, isCompleted) =>
                    owUpdateFunctions[x, y](0, null),
                anyRoadLocation = (i, x, y) => { },
                armos = (x, y) => owUpdateFunctions[x, y](0, null),  // redraw the tile, to update bright/dark or remove icon if player hides useless icons
                sword3 = (x, y) => owUpdateFunctions[x, y](0, null),  // redraw the tile, to update bright/dark or remove icon if player hides useless icons
                sword2 = (x, y) => owUpdateFunctions[x, y](0, null),  // redraw the tile, to update bright/dark or remove icon if player hides useless icons
                routingInfo = (haveLadder, haveRaft, currentRecorderWarpDestinations, currentAnyRoadDestinations, _owRouteworthySpots) =>
                {
                    // clear and redraw routing
                    OverworldRouting.repopulate(haveLadder, haveRaft, currentRecorderWarpDestinations.Select(t => t.Item1).ToArray(), currentAnyRoadDestinations.ToArray(), displayIsCurrentlyMirrored);
                    // redraw recorder dests
                    foreach (var t in currentRecorderWarpDestinations)
                    {
                        var (i, j) = (t.Item1.Item1, t.Item1.Item2);
                        var idx = t.Item2;
                        //let L = 6.0
                        //let bg = new Shapes.Ellipse(Width=float(11*3)-2.+2.0*L, Height=float(11*3)-2.+L, Stroke=Brushes.Black, StrokeThickness=3.0, IsHitTestVisible=false)
                        var bg = new Rectangle { Width = 35.0, Height = 35.0, Stroke = Brushes.Black, StrokeThickness = 3.0, IsHitTestVisible = false, RenderTransform = DungeonUI.fortyFiveDegrees, RenderTransformOrigin = RelativePoint.TopLeft };
                        bg.Effect = new BlurEffect { Radius = 5.0 };   // (WPF: KernelType=Gaussian; Avalonia's blur is always Gaussian)
                        //let fg = new Shapes.Ellipse(Width=float(11*3)-2.+2.0*L, Height=float(11*3)-2.+L, Stroke=UIComponents.RecorderEllipseColor(), StrokeThickness=3.0, IsHitTestVisible=false)
                        var fg = new Rectangle { Width = 35.0, Height = 35.0, Stroke = UIComponents.RecorderEllipseColor(), StrokeThickness = 3.0, IsHitTestVisible = false, RenderTransform = DungeonUI.fortyFiveDegrees, RenderTransformOrigin = RelativePoint.TopLeft };
                        if (Graphics.canUseEffectsWithoutDestroyingPerformance)
                            //canvasAdd(recorderingCanvas, bg, OMTW*float i+7.-L+1., float(11*3*j)-L/2.+1.)
                            canvasAdd(recorderingCanvas, bg, OMTW * (double)i + 23.0, (double)(11 * 3 * j) - 9.0);
                        //canvasAdd(recorderingCanvas, fg, OMTW*float i+7.-L+1., float(11*3*j)-L/2.+1.)
                        canvasAdd(recorderingCanvas, fg, OMTW * (double)i + 23.0, (double)(11 * 3 * j) - 9.0);
                        if (!(TrackerModel.recorderToNewDungeons))
                        {
                            // label the vanilla spots, so player knows the order
                            var tb = new TextBox { Text = ((char)(idx + (int)'1')).ToString(), FontSize = 12.0, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Background = Brushes.Black,
                                                    IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0) };
                            recorderingCanvas.Children.Add(tb);
                            Canvas.SetLeft(tb, OMTW * (double)i + 2.0);
                            Canvas.SetBottom(tb, (double)(11 * 3 * (7 - j)) + 2.0);
                        }
                    }
                    // redraw routes and highlights
                    var pos = mouseGetPosition(overworldCanvas);
                    var (mi, mj) = ((int)(Math.Floor(pos.X / OMTW)), (int)(Math.Floor(pos.Y / (11.0 * 3.0))));
                    if (mi >= 0 && mi < 16 && mj >= 0 && mj < 8)
                        drawRoutesTo(currentRouteTarget(), new Point(0.0, 0.0), mi, mj, TrackerModelOptions.Overworld.DrawRoutes.Value,
                                        (TrackerModelOptions.Overworld.HighlightNearby.Value ? OverworldRouteDrawing.MaxGYR : 0),
                                        whetherToCyanOpenCavesOrArmos());
                    else
                        ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes();
                },
                announceCompletedDungeon = i =>
                {
                    var icons = new[] { upcb(new MapStateProxy(i).DefaultInteriorBmp()), upcb(Graphics.iconCheckMark_bmp) };
                    if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
                    {
                        var labelChar = TrackerModel.GetDungeon(i).LabelChar;
                        if (labelChar != '?')
                            SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, $"Dungeon {labelChar} is complete", icons);
                        else
                            SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, "This dungeon is complete", icons);
                    }
                    else
                        SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, $"Dungeon {i + 1} is complete", icons);
                },
                completedDungeons = a =>
                {
                    for (int i = 0; i <= 7; i++)
                    {
                        // top ui
                        for (int j = 1; j <= 4; j++)
                            mainTrackerCanvases[i, j].Children.Remove(mainTrackerCanvasShaders[i, j]);
                        if (a[i])
                            for (int j = 1; j <= 4; j++)  // don't shade the color swatches
                                mainTrackerCanvases[i, j].Children.Add(mainTrackerCanvasShaders[i, j]);
                        // blockers ui
                        if (a[i])
                            blockerDungeonSunglasses[i].Opacity = 0.3;
                        else
                            blockerDungeonSunglasses[i].Opacity = 1.0;
                    }
                },
                announceFoundDungeonCount = n => RemindFoundDungeonCount(n),
                announceTriforceCount = n => RemindTriforceCount(n, loc => AsyncBrieflyHighlightAnOverworldLocation(loc.Item1, loc.Item2)),
                announceTriforceAndGo = (triforceCount, tagSummary) =>
                {
                    if (TrackerModelOptions.RaceMode.Value) return;   // Race Mode: no go-time alerts
                    // even once the way into level 9 is satisfied, the bow and silver arrows are still needed (both can be inside level 9 itself)
                    var stillNeed = new List<(string, Control)>();
                    if (!tagSummary.HaveBow)
                        stillNeed.Add(("the bow", upcb(Graphics.greyscale(Graphics.bow_bmp))));
                    if (!tagSummary.HaveSilvers)
                        stillNeed.Add(((tagSummary.SilversKnownToBeInLevel9 ? "the silver arrows from level 9" : "the silver arrows"), upcb(Graphics.greyscale(Graphics.silver_arrow_bmp))));
                    var stillNeedText = stillNeed.Count == 0 ? "" : ", but you still need " + string.Join(" and ", stillNeed.Select(p => p.Item1));
                    var stillNeedIcons = new List<Control>();
                    if (stillNeed.Count != 0) stillNeedIcons.Add(upcb(Graphics.iconRightArrow_bmp));
                    foreach (var (_, icon) in stillNeed) stillNeedIcons.Add(icon);
                    // the user's level 9 entry item was just collected: show the item and a green check (instead of triforces)
                    if (tagSummary.CustomTrigger && TrackerModel.level9EntryAnnouncement)
                    {
                        var it = TrackerModel.level9CustomTriggerItem();
                        SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, $"You have the {TrackerModel.ITEMS.AsPronounceString(it)} needed to enter level 9{stillNeedText}",
                                        new[] { upcb(CustomComboBoxes.boxCurrentBMP(it, null)), upcb(Graphics.iconCheckMark_bmp) }.Concat(stillNeedIcons).ToList());
                    }
                    var needSomeThingsicons = new List<Control>();
                    for (int _i = 1; _i <= tagSummary.MissingDungeonCount; _i++)
                        needSomeThingsicons.Add(upcb(Graphics.greyscale(Graphics.genericDungeonInterior_bmp)));
                    if (!tagSummary.HaveBow)
                        needSomeThingsicons.Add(upcb(Graphics.greyscale(Graphics.bow_bmp)));
                    if (!tagSummary.HaveSilvers && !tagSummary.SilversKnownToBeInLevel9)
                        needSomeThingsicons.Add(upcb(Graphics.greyscale(Graphics.silver_arrow_bmp)));
                    var triforceAndGoIcons = new List<Control>();
                    if (triforceCount < TrackerModel.level9TriforceCount && !tagSummary.CustomTrigger)
                    {
                        if (needSomeThingsicons.Count != 0)
                            triforceAndGoIcons.Add(upcb(Graphics.iconRightArrow_bmp));
                        for (int _i = 1; _i <= (TrackerModel.level9TriforceCount - triforceCount); _i++)
                            triforceAndGoIcons.Add(upcb(Graphics.greyTriforce_bmp));
                    }
                    triforceAndGoIcons.Add(upcb(Graphics.iconRightArrow_bmp));
                    if (!tagSummary.HaveSilvers && tagSummary.SilversKnownToBeInLevel9)
                        triforceAndGoIcons.Add(upcb(Graphics.greyscale(Graphics.silver_arrow_bmp)));
                    triforceAndGoIcons.Add(upcb(Graphics.ganon_bmp));
                    var icons = needSomeThingsicons.Concat(triforceAndGoIcons).ToList();
                    var go = triforceCount >= TrackerModel.level9TriforceCount || tagSummary.CustomTrigger ? "go time" : "triforce and go";
                    var silverDisclaimer = !tagSummary.HaveSilvers && tagSummary.SilversKnownToBeInLevel9 ? ", but you will need the silver arrows from 9" : "";
                    switch (tagSummary.Level)
                    {
                        case 101: SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, "You might be " + go + silverDisclaimer, icons); break;
                        case 102: SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, "You are probably " + go + silverDisclaimer, icons); break;
                        case 103: SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, "You are " + go + silverDisclaimer, icons); break;
                        case 0:
                            // nothing else worth reporting, but if the level 9 entry rule is satisfied still say so
                            if (!tagSummary.CustomTrigger && triforceCount >= TrackerModel.level9TriforceCount)
                                SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, (TrackerModel.level9TriforceCount == 0 ? $"Level 9 needs no triforces{stillNeedText}" : $"You have the {TrackerModel.level9TriforceCount} triforces needed to enter level 9{stillNeedText}"),
                                                new[] { upcb(Graphics.fullOrangeTriforce_bmp), upcb(Graphics.iconCheckMark_bmp) }.Concat(stillNeedIcons).ToList());
                            break;
                        default: SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, ($"You need {(needSomeThingsicons.Count > 1 ? "some things" : "something")} to be ") + go, icons); break;
                    }
                },
                remindUnblock = (blockerType, dungeons, detail) =>
                {
                    string name(int d)
                    {
                        if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
                            return ((char)((int)'A' + d)).ToString();
                        else
                            return (1 + d).ToString();
                    }
                    var icons = new List<Control>();
                    var sentence = "Now that you have";
                    if (blockerType.Equals(TrackerModel.DungeonBlocker.COMBAT))
                    {
                        var words = new List<string>();
                        foreach (var d in detail)
                        {
                            if (d.Equals(TrackerModel.CombatUnblockerDetail.BETTER_SWORD)) { words.Add(" a better sword,"); icons.Add(upcb(Graphics.swordLevelToBmp(TrackerModel.playerComputedStateSummary.SwordLevel))); }
                            else if (d.Equals(TrackerModel.CombatUnblockerDetail.BETTER_ARMOR)) { words.Add(" better armor,"); icons.Add(upcb(Graphics.ringLevelToBmp(TrackerModel.playerComputedStateSummary.RingLevel))); }
                            else if (d.Equals(TrackerModel.CombatUnblockerDetail.WAND)) { words.Add(" the wand,"); icons.Add(upcb(Graphics.wand_bmp)); }
                        }
                        sentence = sentence + string.Concat(words);
                    }
                    else if (blockerType.Equals(TrackerModel.DungeonBlocker.BOW_AND_ARROW)) { sentence = sentence + " a beau and arrow,"; icons.Add(upcb(Graphics.bow_and_arrow_bmp)); }
                    else if (blockerType.Equals(TrackerModel.DungeonBlocker.RECORDER)) { sentence = sentence + " the recorder,"; icons.Add(upcb(Graphics.recorder_bmp)); }
                    else if (blockerType.Equals(TrackerModel.DungeonBlocker.LADDER)) { sentence = sentence + " the ladder,"; icons.Add(upcb(Graphics.ladder_bmp)); }
                    else if (blockerType.Equals(TrackerModel.DungeonBlocker.KEY)) { sentence = sentence + " the magic key,"; icons.Add(upcb(Graphics.magic_key_bmp)); }
                    else if (blockerType.Equals(TrackerModel.DungeonBlocker.BOMB)) { sentence = sentence + " bombs,"; icons.Add(upcb(Graphics.bomb_bmp)); }
                    sentence = sentence + " consider dungeon" + (dungeons.Count() > 1 ? "s " : " ");
                    icons.Add(upcb(Graphics.iconRightArrow_bmp));
                    var d0 = dungeons.First();
                    sentence = sentence + name(d0);
                    icons.Add(upcb(new MapStateProxy(d0).DefaultInteriorBmp()));
                    foreach (var d in dungeons.Skip(1))
                    {
                        sentence = sentence + " and " + name(d);
                        icons.Add(upcb(new MapStateProxy(d).DefaultInteriorBmp()));
                    }
                    SendReminder(TrackerModel.ReminderCategory.Blockers, sentence, icons);
                },
                remindShortly = itemId =>
                {
                    if (itemId == TrackerModel.ITEMS.KEY)
                        oneTimeRemindAnyKey = (new TrackerModel.LastChangedTime(), () =>
                        {
                            oneTimeRemindAnyKey = null;
                            if (TrackerModel.playerComputedStateSummary.HaveAnyKey)
                                SendReminder(TrackerModel.ReminderCategory.HaveMagicKey, "Don't forget that you have the magic key", new[] { upcb(Graphics.magic_key_bmp) });
                            else
                                TrackerModel.remindedAnyKey = false;
                        });
                    else if (itemId == TrackerModel.ITEMS.LADDER)
                        oneTimeRemindLadder = (new TrackerModel.LastChangedTime(), () =>
                        {
                            oneTimeRemindLadder = null;
                            if (TrackerModel.playerComputedStateSummary.HaveLadder)
                                SendReminder(TrackerModel.ReminderCategory.HaveLadder, "Don't forget that you have the ladder", new[] { upcb(Graphics.ladder_bmp) });
                            else
                                TrackerModel.remindedLadder = false;
                        });
                    else
                        throw new Exception("bad reminder");
                },
            });
            // place start icon in top layer at very top (above e.g. completed dungeon highlight)
            if (TrackerModel.startIconX != -1)
                canvasAdd(recorderingCanvas, startIcon, 11.5 * OMTW / 48.0 - 3.0 + OMTW * (double)(TrackerModel.startIconX), (double)(TrackerModel.startIconY * 11 * 3));
            if (TrackerModel.customWaypointX != -1)
                canvasAdd(recorderingCanvas, theCustomWaypointIcon, -2.0 + OMTW * (double)(TrackerModel.customWaypointX), (double)(TrackerModel.customWaypointY * 11 * 3) - 5.0);
//          if diagSW.ElapsedMilliseconds > 100L then
//              printfn "doUIUpdate %dms" diagSW.ElapsedMilliseconds
        };
        var threshold = TimeSpan.FromMilliseconds(500.0);
        var periodicReminders = new PeriodicReminders();
        var timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromSeconds(1.0);
        timer.Tick += (_, _) =>
        {
            if (!(TrackerModel.LastChangedTime.IsPaused))
            {
                var hasUISettledDown =
                    DateTime.Now - TrackerModel.playerProgressLastChangedTime.Time > threshold &&
                    DateTime.Now - TrackerModel.dungeonsAndBoxesLastChangedTime.Time > threshold &&
                    DateTime.Now - TrackerModel.mapLastChangedTime.Time > threshold;
                if (hasUISettledDown)
                {
                    var hasTheModelChanged = TrackerModel.recomputeWhatIsNeeded();
                    if (hasTheModelChanged)
                        doUIUpdateEvent.Trigger();
                }
                stepAnimateLink();
                //This is where the periodic reminders are called. Not all of them are here since there are one timers after
                periodicReminders.Check();
                // one-time reminders
                if (oneTimeRemindAnyKey is (TrackerModel.LastChangedTime, Action) rak)
                {
                    var (lct, thunk) = rak;
                    //Frequency of the reminder
                    if ((DateTime.Now - lct.Time).Minutes > 1)  // 2 min
                        thunk();
                }
                if (oneTimeRemindLadder is (TrackerModel.LastChangedTime, Action) rl)
                {
                    var (lct, thunk) = rl;
                    if ((DateTime.Now - lct.Time).Minutes > 1)  // 2 min
                        thunk();
                }
            }
        };

        // create overworld locator stuff (added to correct layer of visual tree later in the code)
        var owLocatorGrid = makeGrid(16, 8, (int)OMTW, 11 * 3);
        var owLocatorCanvas = new Canvas();
        for (int i = 0; i <= 15; i++)
        {
            for (int j = 0; j <= 7; j++)
            {
                var z = new Graphics.TileHighlightRectangle();
                z.Hide();
                owLocatorTileRectangles[i, j] = z;
                gridAdd(owLocatorGrid, z.Shape, i, j);
            }
        }

        // Dungeon level trackers
        var rightwardCanvas = new Canvas();
        var levelTabSelected = new Graphics.SharedEvent<int>();  // blockers listens, to subtly highlight a dungeon
        var blockersHoverEvent = new Graphics.SharedEvent<bool>();
        var currentCCLevel = -1;
        void contentCanvasMouseEnterFunc(int level)
        {
            if (level != currentCCLevel)
            {
                currentCCLevel = level;
                if (level >= 10) // 10+ = summary tab, show all dungeon locations; 11 means moused over 1, 12 means 2, ...
                {
                    for (int i = 0; i <= 15; i++)
                    {
                        for (int j = 0; j <= 7; j++)
                        {
                            var cur = TrackerModel.overworldMapMarks[i, j].Current();
                            if (cur >= TrackerModel.MapSquareChoiceDomainHelper.DUNGEON_1 && cur <= TrackerModel.MapSquareChoiceDomainHelper.DUNGEON_9)
                            {
                                var curLevel = cur - TrackerModel.MapSquareChoiceDomainHelper.DUNGEON_1; // 0-8
                                if ((curLevel == level - 11) || (level == 10))  // if hovering this particular dungeon within summary tab, or if hovering 'S' header
                                    owLocatorTileRectangles[i, j].MakeGreenWithBriefAnimation();
                                else if (!(TrackerModel.GetDungeon(curLevel).IsComplete))
                                    owLocatorTileRectangles[i, j].MakeBoldGreen();
                                else
                                { } // do nothing - don't highlight completed dungeons
                            }
                        }
                    }
                    drawRoutesTo(null, new Point(), 0, 0, false, 0, whetherToCyanOpenCavesOrArmos());
                }
                else
                {
                    var loc = TrackerModel.mapStateSummary.DungeonLocations[level - 1];
                    var (i, j) = (loc.Item1, loc.Item2);
                    if (!loc.Equals(TrackerModel.NOTFOUND))
                    {
                        // when mouse in a dungeon map, show its location...
                        showLocatorExactLocation(i, j);
                        // ...and behave like we are moused there
                        drawRoutesTo(null, new Point(), i, j, TrackerModelOptions.Overworld.DrawRoutes.Value,
                                            (TrackerModelOptions.Overworld.HighlightNearby.Value ? OverworldRouteDrawing.MaxGYR : 0),
                                            whetherToCyanOpenCavesOrArmos());
                    }
                }
            }
        }
        var (dungeonTabs, posToWarpToWhenTabbingFromOverworld, grabModeTextBlock, exportDungeonModelsJsonLinesF, importDungeonModels) =
            await DungeonUI.makeDungeonTabs(cm, x => layout.AddDungeonTabs(x), () => layout.GetDungeonY(), selectDungeonTabEvent, TH, rightwardCanvas,
                                        levelTabSelected, blockersHoverEvent, mainTrackerGhostbusters, showProgress, contentCanvasMouseEnterFunc, _level => hideLocator());
        exportDungeonModelsJsonLines = exportDungeonModelsJsonLinesF;
        OverworldMapTileCustomization.goToDungeon = index =>
        {
            selectDungeonTabEvent.Trigger(index);
            var newPos = dungeonTabs.TranslatePoint(posToWarpToWhenTabbingFromOverworld, appMainCanvas).Value;
            layout.ShowMouseWarp(newPos);
            Graphics.NavigationallyWarpMouseCursorTo(newPos);
            layout.FocusDungeon();
        };
        layout.AddDungeonTabsOverlay(dungeonTabsOverlay);

        await showProgress("blockers");

        // blockers
        var blockerGrid = UIComponents.MakeBlockers(cm, blockerQueries, levelTabSelected, blockersHoverEvent, blockerDungeonSunglasses, contentCanvasMouseEnterFunc, _level => hideLocator());
        layout.AddBlockers(blockerGrid);

        await showProgress("notes, gettables");
        // notes
        notesTextBox = new TextBox { Width = appMainCanvas.Width - BLOCKERS_AND_NOTES_OFFSET, Height = dungeonTabs.Height - blockerGrid.Height,
                                FontSize = TrackerModelOptions.NotesFontSize, Foreground = Brushes.LimeGreen, Background = Brushes.Black, CaretBrush = Brushes.LimeGreen, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };   // long lines wrap instead of running off the box (the caret defaults to the theme's dark text color: invisible on black)
        ScrollViewer.SetVerticalScrollBarVisibility(notesTextBox, ScrollBarVisibility.Auto);
        // (the Fluent theme turns a focused or hovered TextBox light gray; WPF's stays black)
        foreach (var k in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused" }) notesTextBox.Resources[k] = Brushes.Black;
        // WPF's notes box has a plain gray 1px border that doesn't change; the Fluent theme has none until focused, then draws its (here red) accent border
        var notesBorderGray = new SolidColorBrush(Color.Parse("#ABADB3"));
        notesTextBox.BorderThickness = new Thickness(1.0);
        notesTextBox.BorderBrush = notesBorderGray;
        foreach (var k in new[] { "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" }) notesTextBox.Resources[k] = notesBorderGray;
        notesTextBox.Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(1.0);
        notesTextBox.SelectionBrush = new SolidColorBrush(Color.Parse("#3399FF"));   // (WPF's selection blue, not the system accent)
        var notesFilename = Platform.Files.User("Notes.txt");   // (WPF: the exe folder; the same folder on Windows)
        if (!(System.IO.File.Exists(notesFilename)))
            notesTextBox.Text = "Notes\n";
        else
            notesTextBox.Text = System.IO.File.ReadAllText(notesFilename);
        var seedAndFlagsDisplayCanvas = new Canvas { Width = notesTextBox.Width, Height = notesTextBox.Height };
        layout.AddNotesSeedsFlags(notesTextBox, seedAndFlagsDisplayCanvas);
        // font size button at the bottom right of the notes box: opens a small slider to pick the size of the notes text
        var notesFontButton = Graphics.makeButton("Aa", 14.0, Brushes.Orange);
        notesFontButton.Width = 36.0;
        notesFontButton.Height = 24.0;
        Graphics.styleRoundedButton(notesFontButton);
        ToolTip.SetTip(notesFontButton, "Notes font size");
        Canvas.SetRight(notesFontButton, 4.0);
        Canvas.SetBottom(notesFontButton, 4.0);
        seedAndFlagsDisplayCanvas.Children.Add(notesFontButton);
        notesFontButton.Click += (_, _) =>
        {
            if (!popupIsActive)
            {
                popupIsActive = true;
                var pos = notesFontButton.TranslatePoint(new Point(-250.0, -100.0), cm.AppMainCanvas).Value;
                async Task go()
                {
                    var wh = new ManualResetEvent(false);
                    var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(6.0) };
                    var title = new TextBox { IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 16.0, Margin = new Thickness(3.0), Foreground = Brushes.Orange, Background = Brushes.Black };
                    void update(double v)
                    {
                        notesTextBox.FontSize = v;
                        title.Text = $"Notes font size: {(int)v}";
                    }
                    update(notesTextBox.FontSize);
                    var slider = new Slider { Minimum = 8.0, Maximum = 72.0, Value = notesTextBox.FontSize, TickFrequency = 1.0, IsSnapToTickEnabled = true, Width = 220.0, Margin = new Thickness(3.0, 6.0, 3.0, 6.0) };
                    slider.ValueChanged += (_, e) => update(e.NewValue);
                    var doneBtn = new Button { Content = " Done ", Margin = new Thickness(3.0), HorizontalAlignment = HorizontalAlignment.Right, Focusable = false };
                    doneBtn.Click += (_, _) => wh.Set();
                    sp.Children.Add(title);
                    sp.Children.Add(slider);
                    sp.Children.Add(doneBtn);
                    var border = new Border { Child = sp, Background = Brushes.Black, BorderThickness = new Thickness(4.0), BorderBrush = Brushes.Gray };
                    await CustomComboBoxes.DoModal(cm, wh, Math.Max(0.0, pos.X), Math.Max(0.0, pos.Y), border);
                    TrackerModelOptions.NotesFontSize = notesTextBox.FontSize;
                    TrackerModelOptions.writeSettings();
                    popupIsActive = false;
                }
                CustomComboBoxes.StartImmediate(go());
            }
        };

        grabModeTextBlock.Opacity = 0.0;
        grabModeTextBlock.Width = notesTextBox.Width;
        layout.AddExtraDungeonRightwardStuff(grabModeTextBlock, rightwardCanvas);

        // remaining OW spots
        layout.AddOWRemainingScreens(owRemainingScreensTextBoxContainerPanelThatSeesMouseEvents);
        owRemainingScreensTextBoxContainerPanelThatSeesMouseEvents.PointerEntered += (_, _) =>
        {
            if (!TrackerModelOptions.RaceMode.Value)
            {
                var unmarked = new bool[16, 8];
                for (int x = 0; x <= 15; x++) for (int y = 0; y <= 7; y++) unmarked[x, y] = TrackerModel.overworldMapMarks[x, y].Current() == -1;
                OverworldRouteDrawing.drawPathsImpl(unmarked, unmarked, new Point(0.0, 0.0), 0, 0, false, true, OverworldRouteDrawing.All, false, NoCyan);
                if (!(TrackerModel.playerComputedStateSummary.HaveRaft))
                {
                    // drawPathsImpl cannot reach the raft locations and won't color them, so just ad-hoc those two spots
                    foreach (var (i, j) in new[] { (5, 4), (15, 2) })
                    {
                        var cur = TrackerModel.overworldMapMarks[i, j].Current();
                        if (cur == -1)  // they may have marked the raft spots (e.g. Any Road), so only if unmarked...
                            OverworldRouteDrawing.routeDrawingLayer.GetHighlightTile(i, j).MakePaleRed();
                    }
                }
            }
        };
        owRemainingScreensTextBoxContainerPanelThatSeesMouseEvents.PointerExited += (_, _) => ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes();
        // gettables are always shown on the standard map (the checkbox is no longer displayed; its text is folded into the "x/x OW spots left" line)
        owGettableScreensCheckBox.IsChecked = isStandardHyrule;
        Graphics.onChecked(owGettableScreensCheckBox, () => { TrackerModel.forceUpdate(); TrackerModelOptions.Overworld.Gettables.Value = true; TrackerModelOptions.writeSettings(); });
        Graphics.onUnchecked(owGettableScreensCheckBox, () => { TrackerModel.forceUpdate(); TrackerModelOptions.Overworld.Gettables.Value = false; TrackerModelOptions.writeSettings(); });
        owGettableScreensCheckBox.PointerEntered += (_, _) =>
        {
            var unmarked = new bool[16, 8];
            for (int x = 0; x <= 15; x++) for (int y = 0; y <= 7; y++) unmarked[x, y] = TrackerModel.overworldMapMarks[x, y].Current() == -1;
            OverworldRouteDrawing.drawPathsImpl(TrackerModel.mapStateSummary.OwRouteworthySpots,
                                                unmarked, new Point(0.0, 0.0), 0, 0, false, true, 0, true, NoCyan);
        };
        owGettableScreensCheckBox.PointerExited += (_, _) => ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes();

        await showProgress("coords/zone overlays");

        // current max hearts
        var currentMaxHeartsTextBoxClickable = new Border { Background = Brushes.Black, Child = currentMaxHeartsButton, BorderThickness = new Thickness(0.0) };
        layout.AddCurrentMaxHearts(currentMaxHeartsTextBoxClickable);
        Broadcast.maxHeartsForBroadcast = currentMaxHeartsButton;
        currentMaxHeartsTextBoxClickable.PointerEntered += (_, _) => currentMaxHeartsButton.BorderBrush = Brushes.DarkTurquoise;
        currentMaxHeartsTextBoxClickable.PointerExited += (_, _) => currentMaxHeartsButton.BorderBrush = Graphics.roundedBorderBrush;
        ToolTip.SetTip(currentMaxHeartsTextBoxClickable, "Left-click: add a heart\nRight-click: remove a heart");
        currentMaxHeartsTextBoxClickable.AddHandler(InputElement.PointerPressedEvent, new EventHandler<PointerPressedEventArgs>((_, ea) =>   // (WPF: PreviewMouseDown)
        {
            var kindPressed = ea.GetCurrentPoint(currentMaxHeartsTextBoxClickable).Properties.PointerUpdateKind;
            var delta = kindPressed == PointerUpdateKind.LeftButtonPressed ? 1 : kindPressed == PointerUpdateKind.RightButtonPressed ? -1 : 0;
            if (delta != 0)
            {
                ea.Handled = true;
                TrackerModel.startingItemsAndExtras.MaxHeartsDifferential = TrackerModel.startingItemsAndExtras.MaxHeartsDifferential + delta;
                TrackerModel.recomputePlayerStateSummary();
                currentMaxHeartsTextBox.Text = $"Max Hearts: {TrackerModel.playerComputedStateSummary.PlayerHearts}";
                doUIUpdateEvent.Trigger();
            }
        }), RoutingStrategies.Tunnel);
        // coordinate grid
        var placeholderCanvas = new Canvas();  // for startup perf, only add in coords & zone overlays on demand
        var zoneCanvas = new Canvas();
        var owCoordsGrid = makeGrid(16, 8, (int)OMTW, 11 * 3);
        owCoordsGrid.Opacity = 0.0;
        var placeholderFinished = false;
        void ensurePlaceholderFinished()
        {
            if (!placeholderFinished)
            {
                placeholderFinished = true;
                canvasAdd(placeholderCanvas, owCoordsGrid, 0.0, 0.0);
                canvasAdd(placeholderCanvas, zoneCanvas, 0.0, 0.0);
            }
        }
        // (WPF uses Consolas, a Windows font; the port lists monospace fallbacks for macOS/Linux)
        var coordsFont = new FontFamily("Consolas, Menlo, DejaVu Sans Mono, monospace");
        for (int i = 0; i <= 15; i++)
        {
            for (int j = 0; j <= 7; j++)
            {
                var c = new Canvas { Width = OMTW, Height = (double)(11 * 3) };
                gridAdd(owCoordsGrid, c, i, j);
                // https://stackoverflow.com/questions/4114590/how-to-make-wpf-text-on-aero-glass-background-readable
                var coordStr = $"{(char)((int)'A' + j)}  {i + 1}";
                var tb = new TextBox { Text = coordStr,
                                        Foreground = Brushes.White, Background = Brushes.Transparent, BorderThickness = new Thickness(0.0),
                                        FontFamily = coordsFont, FontSize = 16.0, FontWeight = FontWeight.Bold, IsHitTestVisible = false };
                if (Graphics.canUseEffectsWithoutDestroyingPerformance)
                {
                    var tbb = new TextBox { Text = coordStr,
                                            Foreground = Brushes.Black, Background = Brushes.Transparent, BorderThickness = new Thickness(0.0),
                                            FontFamily = coordsFont, FontSize = 16.0, FontWeight = FontWeight.Bold, IsHitTestVisible = false };
                    tbb.Effect = new BlurEffect { Radius = 5.0 };   // (WPF: KernelType=Gaussian)
                    canvasAdd(c, tbb, 2.0, 6.0);
                }
                canvasAdd(c, tb, 2.0, 6.0);
            }
        }
        mirrorOverworldFEs.Add(owCoordsGrid);
        canvasAdd(overworldCanvas, placeholderCanvas, 0.0, 0.0);
        var showCoords = new TextBox { Text = "Coords", FontSize = 14.0, Background = Brushes.Black, Foreground = Brushes.Orange, BorderThickness = new Thickness(0.0), IsReadOnly = true, IsHitTestVisible = false };
        var cb = new CheckBox { Content = showCoords };
        cb.IsChecked = TrackerModelOptions.Overworld.Coords.Value;
        if (TrackerModelOptions.Overworld.Coords.Value)
        {
            ensurePlaceholderFinished(); owCoordsGrid.Opacity = 0.85;
        }
        Graphics.onChecked(cb, () => { ensurePlaceholderFinished(); owCoordsGrid.Opacity = 0.85; TrackerModelOptions.Overworld.Coords.Value = true; TrackerModelOptions.writeSettings(); });
        Graphics.onUnchecked(cb, () => { owCoordsGrid.Opacity = 0.0; TrackerModelOptions.Overworld.Coords.Value = false; TrackerModelOptions.writeSettings(); });
        cb.PointerEntered += (_, _) => { if (TrackerModelOptions.Overworld.HoverZonesCoords.Value && !(cb.IsChecked.HasValue && cb.IsChecked.Value)) { ensurePlaceholderFinished(); owCoordsGrid.Opacity = 0.85; } };
        cb.PointerExited += (_, _) => { if (TrackerModelOptions.Overworld.HoverZonesCoords.Value && !(cb.IsChecked.HasValue && cb.IsChecked.Value)) owCoordsGrid.Opacity = 0.0; };
        layout.AddShowCoords(cb, null);   // (the old 'have potion letter' indicator icon is no longer shown)

        // zone overlay
        CheckBox zone_checkbox;
        Action<TrackerModel.HintZone, string, double, double> addZoneName;
        Action<TrackerModel.HintZone, bool> changeZoneOpacity;
        Canvas[,] allOwMapZoneBlackCanvases;
        if (isStandardHyrule)   // Zones only makes sense in standard map
            (zone_checkbox, addZoneName, changeZoneOpacity, allOwMapZoneBlackCanvases) = UIComponents.MakeZoneOverlay(zoneCanvas, ensurePlaceholderFinished, mirrorOverworldFEs);
        else
        {
            zone_checkbox = new CheckBox { IsChecked = false };
            addZoneName = (_, _, _, _) => { };
            changeZoneOpacity = (_, _) => { };
            allOwMapZoneBlackCanvases = new Canvas[16, 8];
            for (int x = 0; x <= 15; x++) for (int y = 0; y <= 7; y++) allOwMapZoneBlackCanvases[x, y] = new Canvas();
        }
        if (isStandardHyrule)
            layout.AddOWZoneOverlay(zone_checkbox);

        // mouse hover explainer
        {
            var (mouseHoverExplainerIcon, mouseHoverExplainerCanvas) = UIComponents.MakeMouseHoverExplainer(appMainCanvas);
            layout.AddMouseHoverExplainer(mouseHoverExplainerIcon, mouseHoverExplainerCanvas);
        }

        await showProgress("locators/timeline/reminders");

        canvasAdd(overworldCanvas, owLocatorGrid, 0.0, 0.0);
        canvasAdd(overworldCanvas, owLocatorCanvas, 0.0, 0.0);

        // tiles that a Random (dash) ZORA Extra might block: a dashed yellow outline (like the sometimes-empty spots) until the tile is marked or the option is confirmed in Overworld + Extras
        var owMaybeCanvas = new Canvas { IsHitTestVisible = false };
        var owMaybeRects = new Rectangle[16, 8];
        for (int i = 0; i < 16; i++)
            for (int j = 0; j < 8; j++)
            {
                var r = new Rectangle { Width = OMTW - 4.0, Height = (double)(11 * 3) - 4.0, Stroke = Brushes.Yellow, StrokeThickness = 2.0, IsVisible = false, IsHitTestVisible = false };
                r.StrokeDashArray = new AvaloniaList<double>(new[] { 3.0, 2.0 });
                canvasAdd(owMaybeCanvas, r, OMTW * (double)i + 2.0, (double)(j * 11 * 3) + 2.0);
                owMaybeRects[i, j] = r;
            }
        canvasAdd(overworldCanvas, owMaybeCanvas, 0.0, 0.0);
        var owMaybeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500.0) };
        owMaybeTimer.Tick += (_, _) =>
        {
            var maybe = OverworldData.maybeBlockedTiles(TrackerModel.playerComputedStateSummary.HavePowerBracelet, TrackerModel.playerComputedStateSummary.HaveRaft);
            for (int i = 0; i <= 15; i++)
                for (int j = 0; j <= 7; j++)
                {
                    var show = maybe.Contains(Tuple.Create(i, j)) && TrackerModel.overworldMapMarks[i, j].Current() == -1;
                    owMaybeRects[i, j].IsVisible = show;
                }
        };
        owMaybeTimer.Start();

        showLocatorExactLocation = (x, y) =>
        {
            if (!Tuple.Create(x, y).Equals(TrackerModel.NOTFOUND))
            {
                hideLocator();  // clear prior locators, there are various scenarios where prior might not be cleared
                owLocatorTileRectangles[x, y].MakeMagenta();
                var OPA = 0.85;
                var COL = Brushes.Magenta;
                var THI = 2.0;
                var leftLine = new Line { StartPoint = new Point(OMTW * (double)x, 0.0), EndPoint = new Point(OMTW * (double)x, (double)(8 * 11 * 3)), Stroke = COL, StrokeThickness = THI, IsHitTestVisible = false, Opacity = OPA };
                canvasAdd(owLocatorCanvas, leftLine, 0.0, 0.0);
                var rightLine = new Line { StartPoint = new Point(OMTW * (double)(x + 1) - 1.0, 0.0), EndPoint = new Point(OMTW * (double)(x + 1) - 1.0, (double)(8 * 11 * 3)), Stroke = COL, StrokeThickness = THI, IsHitTestVisible = false, Opacity = OPA };
                canvasAdd(owLocatorCanvas, rightLine, 0.0, 0.0);
                var topLine = new Line { StartPoint = new Point(0.0, (double)(y * 11 * 3)), EndPoint = new Point(OMTW * (double)(16 * 3), (double)(y * 11 * 3)), Stroke = COL, StrokeThickness = THI, IsHitTestVisible = false, Opacity = OPA };
                canvasAdd(owLocatorCanvas, topLine, 0.0, 0.0);
                var bottomLine = new Line { StartPoint = new Point(0.0, (double)((y + 1) * 11 * 3) - 1.0), EndPoint = new Point(OMTW * (double)(16 * 3), (double)((y + 1) * 11 * 3) - 1.0), Stroke = COL, StrokeThickness = THI, IsHitTestVisible = false, Opacity = OPA };
                canvasAdd(owLocatorCanvas, bottomLine, 0.0, 0.0);
            }
        };
        showLocatorExactLocations = locs =>
        {
            hideLocator();
            foreach (var (x, y) in locs)
            {
                if (!Tuple.Create(x, y).Equals(TrackerModel.NOTFOUND))
                {
                    owLocatorTileRectangles[x, y].MakeMagenta();
                    var OPA = 0.85;
                    var COL = Brushes.Magenta;
                    var THI = 2.0;
                    void mk(double x1, double y1, double x2, double y2) => canvasAdd(owLocatorCanvas, new Line { StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2), Stroke = COL, StrokeThickness = THI, IsHitTestVisible = false, Opacity = OPA }, 0.0, 0.0);
                    mk(OMTW * (double)x, 0.0, OMTW * (double)x, (double)(8 * 11 * 3));
                    mk(OMTW * (double)(x + 1) - 1.0, 0.0, OMTW * (double)(x + 1) - 1.0, (double)(8 * 11 * 3));
                    mk(0.0, (double)(y * 11 * 3), OMTW * (double)(16 * 3), (double)(y * 11 * 3));
                    mk(0.0, (double)((y + 1) * 11 * 3) - 1.0, OMTW * (double)(16 * 3), (double)((y + 1) * 11 * 3) - 1.0);
                }
            }
        };
        showLocatorHintedZone = (hinted_zone, alsoHighlightABCDEFGH) =>
        {
            OverworldRouteDrawing.routeDrawingLayer.Clear();
            if (!hinted_zone.Equals(TrackerModel.HintZone.UNKNOWN))
            {
                // have hint, so draw that zone...
                var inZone = new bool[16, 8];
                for (int x = 0; x <= 15; x++) for (int y = 0; y <= 7; y++) inZone[x, y] = OverworldData.owMapZone[y][x] == hinted_zone.AsDataChar();
                for (int x = 0; x <= 15; x++)
                    for (int y = 0; y <= 7; y++)
                        if (!(inZone[x, y]))
                            allOwMapZoneBlackCanvases[x, y].Opacity = 0.7;
                bool isZone(int x, int y) => inZone[Math.Max(0, Math.Min(15, x)), Math.Max(0, Math.Min(7, y))];  // if x or y out of bounds, grab nearby in-bounds value
                var (ST, OFF, COLOR) = (3.0, 3.0 / 2.0, Brushes.LightGray);
                for (int x = 0; x <= 15; x++)
                {
                    for (int y = 0; y <= 7; y++)
                    {
                        Line mkLine(double x1, double y1, double x2, double y2)
                        {
                            var line = new Line { StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2), Stroke = COLOR, StrokeThickness = ST, IsHitTestVisible = false };
                            line.StrokeDashArray = new AvaloniaList<double>(new[] { 1.0, 1.5 });
                            return line;
                        }
                        if (isZone(x, y) && !(isZone(x - 1, y)))  // left
                        {
                            var line = mkLine(OMTW * (double)(x) - OFF, (double)(y * 11 * 3), OMTW * (double)(x) - OFF, (double)((y + 1) * 11 * 3));
                            canvasAdd(owLocatorCanvas, line, 0.0, 0.0);
                        }
                        if (isZone(x, y) && !(isZone(x + 1, y)))  // right
                        {
                            var line = mkLine(OMTW * (double)(x + 1) + OFF, (double)(y * 11 * 3), OMTW * (double)(x + 1) + OFF, (double)((y + 1) * 11 * 3));
                            canvasAdd(owLocatorCanvas, line, 0.0, 0.0);
                        }
                        if (isZone(x, y) && !(isZone(x, y - 1)))  // top
                        {
                            var line = mkLine(OMTW * (double)(x), (double)(y * 11 * 3) - OFF, OMTW * (double)(x + 1), (double)(y * 11 * 3) - OFF);
                            canvasAdd(owLocatorCanvas, line, 0.0, 0.0);
                        }
                        if (isZone(x, y) && !(isZone(x, y + 1)))  // bottom
                        {
                            var line = mkLine(OMTW * (double)(x), (double)((y + 1) * 11 * 3) + OFF, OMTW * (double)(x + 1), (double)((y + 1) * 11 * 3) + OFF);
                            canvasAdd(owLocatorCanvas, line, 0.0, 0.0);
                        }
                    }
                }
                for (int i = 0; i <= 15; i++)
                {
                    for (int j = 0; j <= 7; j++)
                    {
                        // ... and highlight all undiscovered tiles
                        if (OverworldData.owMapZone[j][i] == hinted_zone.AsDataChar())
                        {
                            var cur = TrackerModel.overworldMapMarks[i, j].Current();
                            var isLetteredNumberlessDungeon = (alsoHighlightABCDEFGH && cur >= 0 && cur <= 7 && TrackerModel.GetDungeon(cur).LabelChar == '?');
                            if (cur == -1 || isLetteredNumberlessDungeon)
                            {
                                if (TrackerModel.mapStateSummary.OwGettableLocations[i, j])
                                {
                                    if (owInstance.SometimesEmpty(i, j))
                                        owLocatorTileRectangles[i, j].MakeYellow();
                                    else
                                        owLocatorTileRectangles[i, j].MakeGreen();
                                }
                                else
                                {
                                    if (isLetteredNumberlessDungeon)  // OwGettableLocations does not contain already-marked spots
                                        owLocatorTileRectangles[i, j].MakeGreen();
                                    else
                                        owLocatorTileRectangles[i, j].MakeRed();
                                }
                            }
                        }
                    }
                }
            }
        };
        showLocatorInstanceFunc = f =>
        {
            OverworldRouteDrawing.routeDrawingLayer.Clear();
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    if (f(i, j) && (TrackerModel.overworldMapMarks[i, j].Current() == -1 || (i, j) == (15, 5)))   // (F16, the coast ladder spot, is always shown even though that tile is already marked)
                    {
                        if (owInstance.SometimesEmpty(i, j))
                            owLocatorTileRectangles[i, j].MakeYellowWithBriefAnimation();
                        else
                            owLocatorTileRectangles[i, j].MakeGreenWithBriefAnimation();
                    }
                }
            }
        };
        showLocatorInstanceFuncAll = f =>
        {
            OverworldRouteDrawing.routeDrawingLayer.Clear();
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    if (f(i, j))
                    {
                        if (owInstance.SometimesEmpty(i, j))
                            owLocatorTileRectangles[i, j].MakeYellowWithBriefAnimation();
                        else
                            owLocatorTileRectangles[i, j].MakeGreenWithBriefAnimation();
                    }
                }
            }
        };
        showHintShopLocator = () =>
        {
            OverworldRouteDrawing.routeDrawingLayer.Clear();
            var anyFound = false;
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    var cur = TrackerModel.overworldMapMarks[i, j].Current();
                    if (cur == TrackerModel.MapSquareChoiceDomainHelper.HINT_SHOP)
                    {
                        owLocatorTileRectangles[i, j].MakeGreenWithBriefAnimation();
                        OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks[i, j] = true;
                        owUpdateFunctions[i, j](0, null);  // redraw tile, with icon shown
                        anyFound = true;
                    }
                }
            }
            // (no 'none found' icon here: hovering the Hint Decoder button with no hint shops marked just shows nothing)
            _ = anyFound;
        };
        // hovering the magic key in the item progress bar: highlight every tile that has a key marker icon (magic key or old key) on it
        showKeyLocator = () =>
        {
            OverworldRouteDrawing.routeDrawingLayer.Clear();
            var anyFound = false;
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    var hasKey = visibleDecorIcons(TrackerModel.owSwordIcons[i, j]).Any(isKeyDecor);
                    if (hasKey)
                    {
                        owLocatorTileRectangles[i, j].MakeGreenWithBriefAnimation();
                        anyFound = true;
                    }
                }
            }
            if (!(anyFound))
                showLocatorNoneFound();
        };
        showSwordLocator = () =>
        {
            OverworldRouteDrawing.routeDrawingLayer.Clear();
            var anyFound = false;
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    if (TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).Any(id => id == TrackerModel.ITEMS.WOODSWORD || id == TrackerModel.ITEMS.PROGRESSIVESWORD || id == TrackerModel.ITEMS.MAGICALSWORD || id == TrackerModel.ITEMS.WHITESWORD4))
                    {
                        owLocatorTileRectangles[i, j].MakeGreenWithBriefAnimation();
                        anyFound = true;
                    }
                }
            }
            if (!(anyFound))
                showLocatorNoneFound();
        };
        showMarkerItemsLocator = idents =>
        {
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    var hasDecor = visibleDecorIcons(TrackerModel.owSwordIcons[i, j]).Any(id => idents.Contains(id));
                    var cur = TrackerModel.overworldMapMarks[i, j].Current();
                    bool inBox(TrackerModel.Box b) => idents.Contains(b.CellCurrent());
                    var hasBox = (cur == TrackerModel.MapSquareChoiceDomainHelper.ARMOS && inBox(TrackerModel.armosBox)) ||
                                 (cur == TrackerModel.MapSquareChoiceDomainHelper.SWORD2 && inBox(TrackerModel.sword2Box)) ||
                                 (cur == TrackerModel.MapSquareChoiceDomainHelper.SWORD3 && inBox(TrackerModel.sword3Box)) ||
                                 (cur == TrackerModel.MapSquareChoiceDomainHelper.SWORD1 && TrackerModel.shuffledWoodSwordTiles.Contains(Tuple.Create(i, j)) && inBox(TrackerModel.sword1Box));   // shuffled wood sword cave item
                    if (hasDecor || hasBox)
                        owLocatorTileRectangles[i, j].MakeGreenWithBriefAnimation();
                }
            }
        };
        showArrowLocator = () =>
        {
            OverworldRouteDrawing.routeDrawingLayer.Clear();
            var anyFound = false;
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    if (visibleDecorIcons(TrackerModel.owSwordIcons[i, j]).Any(id => id == TrackerModel.ITEMS.WOODARROW || id == TrackerModel.ITEMS.SILVERARROW))
                    {
                        owLocatorTileRectangles[i, j].MakeGreenWithBriefAnimation();
                        anyFound = true;
                    }
                }
            }
            if (!(anyFound))
                showLocatorNoneFound();
        };
        showMeatLocator = () =>
        {
            OverworldRouteDrawing.routeDrawingLayer.Clear();
            var anyFound = false;
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    if (TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).Contains(TrackerModel.ITEMS.MEAT))
                    {
                        owLocatorTileRectangles[i, j].MakeGreenWithBriefAnimation();
                        anyFound = true;
                    }
                }
            }
            if (!(anyFound))
                showLocatorNoneFound();
        };
        showBombMarkerLocator = () =>
        {
            // no Clear() here: this runs right after the bomb-shop locator, and adds the tiles where you placed a bomb marker icon
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    if (TrackerModel.decorIconsOf(TrackerModel.owSwordIcons[i, j]).Contains(TrackerModel.DECOR_BOMB))
                        owLocatorTileRectangles[i, j].MakeGreenWithBriefAnimation();
                    if (TrackerModel.owBombDroppers[i, j])
                        owLocatorTileRectangles[i, j].MakeRedWithBriefAnimation();   // bomb dropper screens get a red border
                }
            }
        };
        showShopLocatorInstanceFunc = item =>
        {
            // (the existing overworld highlighting (open cave boxes, routes) stays while hovering arrow / key / meat)
            var anyFound = false;
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    var cur = TrackerModel.overworldMapMarks[i, j].Current();
                    if (new MapStateProxy(cur).IsThreeItemShop &&
                            (cur == item || TrackerModel.getShopItem2_1based(i, j) == TrackerModel.MapSquareChoiceDomainHelper.ToItem(item) || TrackerModel.getShopItem3_1based(i, j) == TrackerModel.MapSquareChoiceDomainHelper.ToItem(item)))
                    {
                        owLocatorTileRectangles[i, j].MakeGreenWithBriefAnimation();
                        OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks[i, j] = true;
                        owUpdateFunctions[i, j](0, null);  // redraw tile, with icon shown
                        anyFound = true;
                    }
                }
            }
            if (!(anyFound))
                showLocatorNoneFound();
        };
        showLocatorPotionAndTakeAny = () =>
        {
            OverworldRouteDrawing.routeDrawingLayer.Clear();
            var anyFound = false;
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    var cur = TrackerModel.overworldMapMarks[i, j].Current();
                    if (cur == TrackerModel.MapSquareChoiceDomainHelper.POTION_SHOP ||
                        (cur == TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY && TrackerModel.getOverworldMapExtraData(i, j, cur) != cur))
                    {
                        owLocatorTileRectangles[i, j].MakeGreenWithBriefAnimation();
                        OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks[i, j] = true;  // probably unnecessary, as these can't be hidden
                        owUpdateFunctions[i, j](0, null);  // redraw tile, with icon shown
                        anyFound = true;
                    }
                }
            }
            if (!(anyFound))
                showLocatorNoneFound();
        };
        showLocatorRupees = () =>
        {
            OverworldRouteDrawing.routeDrawingLayer.Clear();
            var anyFound = false;
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    var cur = TrackerModel.overworldMapMarks[i, j].Current();
                    if (cur == TrackerModel.MapSquareChoiceDomainHelper.MONEY_MAKING_GAME || cur == TrackerModel.MapSquareChoiceDomainHelper.UNKNOWN_SECRET ||
                                (cur == TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET && TrackerModel.getOverworldMapExtraData(i, j, cur) != 0) ||
                                (cur == TrackerModel.MapSquareChoiceDomainHelper.MEDIUM_SECRET && TrackerModel.getOverworldMapExtraData(i, j, cur) != 0) ||
                                (cur == TrackerModel.MapSquareChoiceDomainHelper.SMALL_SECRET && TrackerModel.getOverworldMapExtraData(i, j, cur) != 0))
                    {
                        owLocatorTileRectangles[i, j].MakeGreenWithBriefAnimation();
                        OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks[i, j] = true;
                        owUpdateFunctions[i, j](0, null);  // redraw tile, with icon shown
                        anyFound = true;
                    }
                }
            }
            if (!(anyFound))
                showLocatorNoneFound();
        };
        recorderDestinationButtonCanvas.PointerEntered += (_, _) =>
        {
            OverworldRouteDrawing.routeDrawingLayer.Clear();
            for (int i = 0; i <= 15; i++)
                for (int j = 0; j <= 7; j++)
                    // Note: in HDN, you might have found dungeon G, but if you have starting triforce 4, and dunno if 4=G, we don't know if can recorder there
                    if (TrackerModel.playerComputedStateSummary.HaveRecorder && OverworldRouting.recorderDests.Contains(Tuple.Create(i, j)))
                        owLocatorTileRectangles[i, j].MakeGreenWithBriefAnimation();
        };
        recorderDestinationButtonCanvas.PointerExited += (_, _) => hideLocator();
        anyRoadLegendIcon.PointerEntered += (_, _) =>
        {
            OverworldRouteDrawing.routeDrawingLayer.Clear();
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    var cur = TrackerModel.overworldMapMarks[i, j].Current();
                    if (cur >= TrackerModel.MapSquareChoiceDomainHelper.WARP_1 && cur <= TrackerModel.MapSquareChoiceDomainHelper.WARP_4)
                        owLocatorTileRectangles[i, j].MakeGreenWithBriefAnimation();
                }
            }
        };
        anyRoadLegendIcon.PointerExited += (_, _) => hideLocator();
        dungeonLegendIconArea.PointerEntered += (_, _) => contentCanvasMouseEnterFunc(10);   // the 'show all dungeons' functionality
        dungeonLegendIconArea.PointerExited += (_, _) => hideLocator();
        showLocator = sld =>
        {
            switch (sld)
            {
                case ShowLocatorDescriptor.DungeonNumber(var n):
                {
                    var index = -1;
                    for (int i = 0; i <= 7; i++)
                        if (TrackerModel.GetDungeon(i).LabelChar == (char)((int)'1' + n))
                            index = i;
                    void showHint()
                    {
                        var hinted_zone = TrackerModel.GetLevelHint.Invoke(n);
                        if (!hinted_zone.Equals(TrackerModel.HintZone.UNKNOWN))
                            showLocatorHintedZone(hinted_zone, true);
                    }
                    if (index != -1)
                    {
                        var loc = TrackerModel.mapStateSummary.DungeonLocations[index];
                        if (!loc.Equals(TrackerModel.NOTFOUND))
                            showLocatorExactLocation(loc.Item1, loc.Item2);
                        else
                            showHint();
                    }
                    else
                        showHint();
                    break;
                }
                case ShowLocatorDescriptor.DungeonIndex(var i):
                {
                    var loc = TrackerModel.mapStateSummary.DungeonLocations[i];
                    if (!loc.Equals(TrackerModel.NOTFOUND))
                        showLocatorExactLocation(loc.Item1, loc.Item2);
                    else
                    {
                        if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null) && i != 8)   // Level 9 works like non-HDN
                        {
                            var label = TrackerModel.GetDungeon(i).LabelChar;
                            if (label >= '1' && label <= '8')
                            {
                                var index = (int)label - (int)'1';
                                var hinted_zone = TrackerModel.GetLevelHint.Invoke(index);
                                if (!hinted_zone.Equals(TrackerModel.HintZone.UNKNOWN))
                                    showLocatorHintedZone(hinted_zone, true);
                            }
                        }
                        else
                        {
                            var hinted_zone = TrackerModel.GetLevelHint.Invoke(i);
                            if (!hinted_zone.Equals(TrackerModel.HintZone.UNKNOWN))
                                showLocatorHintedZone(hinted_zone, false);
                        }
                    }
                    break;
                }
                case ShowLocatorDescriptor.Sword1:
                {
                    var loc = TrackerModel.mapStateSummary.Sword1Location;
                    if (!loc.Equals(TrackerModel.NOTFOUND))
                        showLocatorExactLocation(loc.Item1, loc.Item2);
                    break;
                }
                case ShowLocatorDescriptor.Sword2:
                {
                    var loc = TrackerModel.mapStateSummary.Sword2Location;
                    if (!loc.Equals(TrackerModel.NOTFOUND))
                        showLocatorExactLocation(loc.Item1, loc.Item2);
                    else
                    {
                        var hinted_zone = TrackerModel.GetLevelHint.Invoke(9);
                        if (!hinted_zone.Equals(TrackerModel.HintZone.UNKNOWN))
                            showLocatorHintedZone(hinted_zone, false);
                    }
                    break;
                }
                case ShowLocatorDescriptor.Sword3:
                {
                    var loc = TrackerModel.mapStateSummary.Sword3Location;
                    if (!loc.Equals(TrackerModel.NOTFOUND))
                        showLocatorExactLocation(loc.Item1, loc.Item2);
                    else
                    {
                        var hinted_zone = TrackerModel.GetLevelHint.Invoke(10);
                        if (!hinted_zone.Equals(TrackerModel.HintZone.UNKNOWN))
                            showLocatorHintedZone(hinted_zone, false);
                    }
                    break;
                }
            }
        };
        var currentTargetGhostBuster = makeGhostBusterImpl(Brushes.Red, true);
        currentTargetGhostBuster.Opacity = 0.0;
        layout.AddLinkTarget(currentTargetGhostBuster);
        showLocatorNoneFound = () =>
        {
            // the 'none found' ghostbuster icon is intentionally no longer shown when a hover locator finds nothing
            // ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes()  // do not do this, we want the map to be blank, to make the ghostbuster 'pop' more and the non-found-ness to be more apparent
        };
        hideLocator = () =>
        {
//          printfn "some other HL: %s" ((new System.Diagnostics.StackTrace()).ToString())        // this is super useful for debugging
            currentCCLevel = -1;
            if (!zone_checkbox.IsChecked.HasValue || !zone_checkbox.IsChecked.Value) changeZoneOpacity(TrackerModel.HintZone.UNKNOWN, false);
            foreach (var zbc in allOwMapZoneBlackCanvases) zbc.Opacity = 0.0;
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    owLocatorTileRectangles[i, j].Hide();
                    if (OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks[i, j])
                    {
                        OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks[i, j] = false;
                        owUpdateFunctions[i, j](0, null);  // redraw tile, with icon possibly hidden
                    }
                }
            }
            owLocatorCanvas.Children.Clear();
            currentTargetGhostBuster.Opacity = 0.0;
            ensureRespectingOwGettableScreensAndOpenCavesCheckBoxes();
        };

        // hovering the candle in the item progress bar circles every candle marked on the map: blue circles for blue candles, red circles for red candles
        var candleCirclesShown = false;
        void showCandleCircles()
        {
            var tileH = 11.0 * 3.0;
            void circle(int i, int j, IBrush brush)
            {
                var d = 26.0;
                var e = new Ellipse { Width = d, Height = d, Stroke = brush, StrokeThickness = 3.0, IsHitTestVisible = false };
                canvasAdd(owLocatorCanvas, e, (double)i * OMTW + (OMTW - d) / 2.0, (double)j * tileH + (tileH - d) / 2.0);
            }
            for (int i = 0; i <= 15; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    var ids = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[i, j]).Select(e => e.Item1).ToList();
                    var (isBlue, isRed) =
                        ((ids.Contains(TrackerModel.ITEMS.BLUECANDLE) || TrackerModel.overworldMapMarks[i, j].Current() == TrackerModel.MapSquareChoiceDomainHelper.BLUE_CANDLE),
                         ids.Contains(TrackerModel.ITEMS.REDCANDLE));
                    if (isBlue) circle(i, j, Brushes.DodgerBlue);
                    if (isRed) circle(i, j, Brushes.Red);
                }
            }
        }
        void updateCandleHover(bool overCandle)
        {
            if (overCandle && !candleCirclesShown)
            {
                candleCirclesShown = true;
                owLocatorCanvas.Children.Clear();
                showCandleCircles();
            }
            else if (!overCandle && candleCirclesShown)
            {
                candleCirclesShown = false;
                hideLocator();
            }
        }
        itemProgressCanvas.PointerMoved += (_, ea) =>
        {
            var slot = (int)(Math.Floor((ea.GetPosition(itemProgressCanvas).X - OverworldItemGridUI.ITEM_PROGRESS_FIRST_ITEM) / 30.0));
            updateCandleHover(slot == 7);
        };
        itemProgressCanvas.PointerExited += (_, _) => updateCandleHover(false);
        addZoneName(TrackerModel.HintZone.DEATH_MOUNTAIN, "DEATH\nMOUNTAIN", 2.5, 0.3);
        addZoneName(TrackerModel.HintZone.GRAVE,          "GRAVE", 1.5, 2.8);
        addZoneName(TrackerModel.HintZone.DEAD_WOODS,     "DEAD\nWOODS", 1.4, 5.3);
        addZoneName(TrackerModel.HintZone.LAKE,           "LAKE 1", 10.2, 0.1);
        addZoneName(TrackerModel.HintZone.LAKE,           "LAKE 2", 5.5, 3.5);
        addZoneName(TrackerModel.HintZone.LAKE,           "LAKE 3", 9.4, 5.5);
        addZoneName(TrackerModel.HintZone.RIVER,          "RIVER 1", 7.3, 1.1);
        addZoneName(TrackerModel.HintZone.RIVER,          "RIV\nER2", 5.1, 6.2);
        addZoneName(TrackerModel.HintZone.NEAR_START,     "START", 7.3, 6.2);
        addZoneName(TrackerModel.HintZone.DESERT,         "DESERT", 10.3, 3.1);
        addZoneName(TrackerModel.HintZone.FOREST,         "FOREST", 12.3, 5.1);
        addZoneName(TrackerModel.HintZone.LOST_HILLS,     "LOST\nHILLS", 12.4, 0.3);
        addZoneName(TrackerModel.HintZone.COAST,          "COAST", 14.3, 2.7);
        if (TrackerModelOptions.Overworld.Zones.Value)
            changeZoneOpacity(TrackerModel.HintZone.UNKNOWN, true);

        // timeline, options menu, reminders
        var moreOptionsButton = Graphics.makeButton("Options", 12.0, Brushes.Orange);
        moreOptionsButton.MaxHeight = 25.0;
        moreOptionsButton.Measure(new Size(double.PositiveInfinity, 25.0));

        var optionsCanvas = OptionsMenu.makeOptionsCanvas(cm, true, isStandardHyrule);
        optionsCanvas.Opacity = 1.0;
        optionsCanvas.IsHitTestVisible = true;

        var drawButton = Graphics.makeButton("Draw + Custom Icons", 12.0, Brushes.Orange);
        drawButton.MaxHeight = 22.0;
        if (layout.IsShort)
        {
            drawButton.Opacity = 0.0; // draw button disabled in short layout
            drawButton.IsEnabled = false;  // make not clickable (even if invisible)
        }

        var theTimeline1 = new Timeline(21.0, 4, appMainCanvas.Width - 48.0, 1, new[] { "0:00", "0:10", "0:20", "0:30", "0:40", "0:50", "1:00" }, moreOptionsButton, drawButton);
        var theTimeline2 = new Timeline(21.0, 4, appMainCanvas.Width - 48.0, 2, new[] { "0:00", "0:20", "0:40", "1:00", "1:20", "1:40", "2:00" }, moreOptionsButton, drawButton);
        var theTimeline3 = new Timeline(21.0, 4, appMainCanvas.Width - 48.0, 3, new[] { "0:00", "0:30", "1:00", "1:30", "2:00", "2:30", "3:00" }, moreOptionsButton, drawButton);
        theTimeline1.ThisIsNowTheVisibleTimeline();
        theTimeline1.Canvas.Opacity = 1.0;
        theTimeline2.Canvas.Opacity = 0.0;
        theTimeline3.Canvas.Opacity = 0.0;
        void drawTimeline(int minute)
        {
            if (minute <= 60)
            {
                theTimeline1.ThisIsNowTheVisibleTimeline();
                theTimeline1.Canvas.Opacity = 1.0;
                theTimeline2.Canvas.Opacity = 0.0;
                theTimeline3.Canvas.Opacity = 0.0;
                theTimeline1.Update(minute, timelineItems, maxOverworldRemain);
            }
            else if (minute <= 120)
            {
                theTimeline1.Canvas.Opacity = 0.0;
                theTimeline2.ThisIsNowTheVisibleTimeline();
                theTimeline2.Canvas.Opacity = 1.0;
                theTimeline3.Canvas.Opacity = 0.0;
                theTimeline2.Update(minute, timelineItems, maxOverworldRemain);
            }
            else
            {
                theTimeline1.Canvas.Opacity = 0.0;
                theTimeline2.Canvas.Opacity = 0.0;
                theTimeline3.ThisIsNowTheVisibleTimeline();
                theTimeline3.Canvas.Opacity = 1.0;
                theTimeline3.Update(minute, timelineItems, maxOverworldRemain);
            }
        }
        onEvent(TrackerModel.TimelineItemModel.TimelineChanged, drawTimeline);
        for (int j = 0; j <= 7; j++)
            onEvent(TrackerModel.GetDungeon(j).HiddenDungeonColorOrLabelChanged, _ => TrackerModel.TimelineItemModel.TriggerTimelineChanged()); // e.g. to change heart label from E -> 3
        OptionsMenu.secondQuestDungeonsOptionChanged += () => TrackerModel.TimelineItemModel.TriggerTimelineChanged();  // e.g. to change heart label from 1 -> 4
        onEvent(TrackerModel.IsWSMSReplacedByBUChanged, _ => TrackerModel.TimelineItemModel.TriggerTimelineChanged());  // e.g. change mags icon to BU

        moreOptionsButton.Click += (_, _) =>
        {
            if (!popupIsActive)
            {
                popupIsActive = true;
                var wh = new ManualResetEvent(false);
                async Task go()
                {
                    // the options popup opens right below the overworld tiles (moved up only as far as needed to still fit in the window)
                    optionsCanvas.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double owBottom;
                    try { owBottom = overworldCanvas.TranslatePoint(new Point(0.0, overworldCanvas.Bounds.Height), cm.AppMainCanvas).Value.Y; } catch (Exception) { owBottom = 0.0; }   // (WPF: ActualHeight)
                    var popupY = Math.Max(0.0, Math.Min(owBottom, (cm.Height - optionsCanvas.DesiredSize.Height)));
                    OptionsMenu.closeOptionsPopupHook = () => wh.Set();
                    await CustomComboBoxes.DoModal(cm, wh, 0.0, popupY, optionsCanvas);
                    OptionsMenu.closeOptionsPopupHook = () => { };
                    TrackerModelOptions.writeSettings();
                    popupIsActive = false;
                    if (OptionsMenu.optionsTourRequested)
                    {
                        OptionsMenu.optionsTourRequested = false;
                        OptionsMenu.runGuidedTour(cm, OptionsMenu.mainScreenTourSteps);
                    }
                }
                CustomComboBoxes.StartImmediate(go());
            }
        };
        layout.AddTimeline(theTimeline1.Canvas, theTimeline2.Canvas, theTimeline3.Canvas);
        drawButton.Click += (_, _) =>
        {
            if (!popupIsActive)
            {
                popupIsActive = true;
                async Task go()
                {
                    await DrawingLayer.InteractWithDrawingLayer(cm, drawingCanvas);
                    popupIsActive = false;
                }
                CustomComboBoxes.StartImmediate(go());
            }
        };

        var reminderDisplayOuterDockPanel = SetupReminderDisplayAndProcessing(cm);
        var drawButtonText = (TextBox)drawButton.Content;
        drawButtonText.Text = "Draw/Custom Icons";   // shortened to fit the narrow button stack
        layout.AddTopLeftButtons(moreOptionsButton, drawButton, Reminders.reminderLogButton);
        layout.AddReminderDisplayOverlay(reminderDisplayOuterDockPanel);

        // postgameDecorationCanvas atop timeline & reminders
        {
            var postgameDecorationCanvas = new Canvas { Width = appMainCanvas.Width, Opacity = 0.0, IsHitTestVisible = false };   // no hit test so it doesn't absorb clicks to reminder log
            layout.AddPostGameDecorationCanvas(postgameDecorationCanvas);
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Black, Height = 24.0 };  // height to cover the "log" button
            Canvas.SetRight(sp, 0.0);
            Canvas.SetTop(sp, 0.0);
            postgameDecorationCanvas.Children.Add(sp);
            Rectangle makeViewRectImpl(Control c) => new Rectangle { Width = c.Width, Height = c.Height, Fill = new VisualBrush(c) { Stretch = Stretch.Fill } };   // (WPF's default Stretch is Fill)
            var timerClone = makeViewRectImpl(hmsTimeTextBox);
            var scaleTrans = new ScaleTransform(20.0 / timerClone.Height, 20.0 / timerClone.Height);
            // (WPF: LayoutTransform; Avalonia has none, so the clone sits in a LayoutTransformControl)
            sp.Children.Add(new LayoutTransformControl { LayoutTransform = scaleTrans, Child = timerClone });
            sp.Children.Add(makeViewRectImpl(owRemainingScreensTextBox));
            var screenshotTimelineFilename = Platform.Files.User("most-recent-completion-timeline.png");   // (WPF: the exe folder; the same folder on Windows)
            var screenshotFullFilename = Platform.Files.User("most-recent-completion-full-screenshot.png");
            onEvent(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Changed, b =>
            {
                if (b) TrackerModel.LastChangedTime.PauseAll(); else TrackerModel.LastChangedTime.ResumeAll();   // (rescuing Zelda, by the icon or the global hotkey, stops the timer)
                if (b)
                {
                    postgameDecorationCanvas.Opacity = 1.0;
                    if (!(Timeline.isCurrentlyLoadingASave))
                    {
                        async Task go()
                        {
                            // select the dungeon summary tab, so everything on-screen at once
                            selectDungeonTabEvent.Trigger(9);
                            if (TrackerModelOptions.ShorterAppWindow.Value)
                            {
                                Graphics.NavigationallyWarpMouseCursorTo(dungeonTabs.TranslatePoint(posToWarpToWhenTabbingFromOverworld, appMainCanvas).Value);
                                layout.FocusDungeon();
                            }
                            // wait for finish drawing (e.g. zelda is not yet drawn on the timeline)
                            await Task.Delay(10);   // (resumes on the UI thread: WPF's SwitchToContext ctxt)
                            try
                            {
                                foreach (var (filename, viewboxF) in new (string, Func<Rect>)[] { (screenshotFullFilename, () => layout.GetFullAppBounds()),
                                                                                                   (screenshotTimelineFilename, () => layout.GetTimelineBounds()) })
                                {
                                    // screenshot timeline region
                                    // (WPF: a VisualBrush with an absolute Viewbox drawn into a DrawingVisual, rendered by a RenderTargetBitmap and saved with a PngBitmapEncoder.
                                    // Avalonia: a rectangle filled with a VisualBrush whose SourceRect is that region, rendered by a RenderTargetBitmap and saved as PNG.)
                                    var orig = viewboxF();
                                    var rect = viewboxF();
                                    double factor;
                                    if (Graphics.appScale() != 1.0)
                                    {
                                        var r = Graphics.appScale();
                                        rect = new Rect(new Point(rect.Left * r, rect.Top * r), new Size(rect.Width * r, rect.Height * r));
                                        factor = r;
                                    }
                                    else factor = 1.0;
                                    var vb = new VisualBrush((Visual)cm.RootCanvas.Parent) { Stretch = Stretch.Uniform, SourceRect = new RelativeRect(rect, RelativeUnit.Absolute) };    // whole canvas (parent of root), so that we can capture the timer display in the full screenshot
                                    var shot = new Rectangle { Width = orig.Width, Height = orig.Height, Fill = vb };
                                    _ = factor;   // (WPF scales the drawing by 1/factor; the rectangle here is already orig's size)
                                    shot.Measure(new Size(orig.Width, orig.Height));
                                    shot.Arrange(new Rect(0.0, 0.0, orig.Width, orig.Height));
                                    using (var bitmap = new RenderTargetBitmap(new PixelSize((int)(orig.Width), (int)(orig.Height)), new Vector(96.0, 96.0)))
                                    {
                                        bitmap.Render(shot);
                                        using (var stream = System.IO.File.Create(filename))
                                            bitmap.Save(stream);
                                    }
                                    System.IO.File.SetCreationTime(filename, DateTime.Now);
                                    System.IO.File.SetLastWriteTime(filename, DateTime.Now);
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.ToString());
                            }
                        }
                        CustomComboBoxes.StartImmediate(go());
                    }
                }
                else
                    postgameDecorationCanvas.Opacity = 0.0;
            });
        }

        void refocusKeyboard()
        {
#if BROWSER
            var w = BrowserWindow.Of(appMainCanvas);
#else
            var w = TopLevel.GetTopLevel(appMainCanvas) as Window;   // (WPF: Window.GetWindow)
#endif
            w?.FocusManager?.ClearFocus();   // ensure that clicks outside the Notes area de-focus it, by clearing keyboard focus... (WPF: Keyboard.ClearFocus + FocusManager.SetFocusedElement(w, null))
            w?.Focus();  // refocus keyboard to main window so MyKey still works
        }
        appMainCanvas.PointerPressed += (_, _) => refocusKeyboard();

        await showProgress("broadcast/load/animation");

        // broadcast window
        Broadcast.MakeBroadcastWindow(cm, drawingCanvas, blockerGrid, dungeonTabsOverlayContent, refocusMainWindow);

#if NOT_RACE_LEGAL
        HUDs.MakeHUDs(cm, trackerDungeonMoused, trackerLocationMoused);
#endif

        layout.AddSpotSummary(spotSummaryCanvas);

        // poke loaded data values
        if (loadData != null)
        {
            var data = loadData;
            // Overworld
            TrackerModel.MirrorOverworld = data.Overworld.MirrorOverworld;
            SaveAndLoad.ApplyLoadedOptions(data.Overworld);   // (the extra blocks and their Random ? states, boots, progressive items, hint letters; also applied at startup, before the UI was built)
            // the letters typed into the Dead Woods / Lost Hills hint buttons go back into the buttons (older saves do not have them)
            if (data.Overworld.DeadWoodsHint != null && hintLoadHooks.TryGetValue("Dead Woods", out var dwHook)) dwHook(data.Overworld.DeadWoodsHint);
            if (data.Overworld.LostHillsHint != null && hintLoadHooks.TryGetValue("Lost Hills", out var lhHook)) lhHook(data.Overworld.LostHillsHint);
            TrackerModel.startIconX = data.Overworld.StartIconX;
            TrackerModel.startIconY = data.Overworld.StartIconY;
            TrackerModel.customWaypointX = data.Overworld.CustomWaypointX;
            TrackerModel.customWaypointY = data.Overworld.CustomWaypointY;
            var a = data.Overworld.Map;
            if (data.Overworld.SwordIcons != null && data.Overworld.SwordIcons.Length == 16 * 8)
            {
                for (int j = 0; j <= 7; j++)
                {
                    for (int i = 0; i <= 15; i++)
                    {
                        TrackerModel.owSwordIcons[i, j] = data.Overworld.SwordIcons[j * 16 + i];
                        TrackerModel.owBombDroppers[i, j] = false;
                    }
                }
                // (older saves kept bomb droppers as a marker icon: they move to their own layer)
                for (int j = 0; j <= 7; j++)
                {
                    for (int i = 0; i <= 15; i++)
                    {
                        var es = TrackerModel.decorEntriesOf(TrackerModel.owSwordIcons[i, j]);
                        if (es.Any(e => e.Item1 == TrackerModel.DECOR_BOMB_DROPPER))
                        {
                            TrackerModel.owBombDroppers[i, j] = true;
                            var label = TrackerModel.shopLabelOf(TrackerModel.owSwordIcons[i, j]);
                            var packed = TrackerModel.decorEntriesPack(ListModule.OfSeq(es.Where(e => e.Item1 != TrackerModel.DECOR_BOMB_DROPPER)));
                            TrackerModel.owSwordIcons[i, j] = (label > 0 ? TrackerModel.withShopLabel(packed, label) : packed);
                        }
                    }
                }
                for (int k = 0; k <= 5; k++) TrackerModel.cheapKeyShops[k] = false;
                if (data.Overworld.CheapKeyShops != null && data.Overworld.CheapKeyShops.Length == 6)
                    for (int k = 0; k <= 5; k++) TrackerModel.cheapKeyShops[k] = (data.Overworld.CheapKeyShops[k] == 1);
                for (int k = 0; k <= 5; k++) TrackerModel.shopStash[k] = -1;
                if (data.Overworld.ShopStash != null && data.Overworld.ShopStash.Length == 6)
                    for (int k = 0; k <= 5; k++) TrackerModel.shopStash[k] = data.Overworld.ShopStash[k];
                if (data.Overworld.BombDroppers != null && data.Overworld.BombDroppers.Length == 16 * 8)
                    for (int j = 0; j <= 7; j++)
                        for (int i = 0; i <= 15; i++)
                            if (data.Overworld.BombDroppers[j * 16 + i] != 0) TrackerModel.owBombDroppers[i, j] = true;
                rebuildMapCollected();
            }
            if (a.Length != 16 * 8 * 3)
                throw new Exception("bad load data at data.Overworld.Map");
            Timeline.isCurrentlyLoadingASave = true;
            TrackerModel.currentlyIgnoringForceUpdatesDuringALoad = true;
            try
            {
            var anySetProblems = false;
            for (int j = 0; j <= 7; j++)
            {
                for (int i = 0; i <= 15; i++)
                {
                    var cur = a[j * 16 * 3 + i * 3];
                    var ed = a[j * 16 * 3 + i * 3 + 1];
                    var circle = a[j * 16 * 3 + i * 3 + 2];
                    if (!(TrackerModel.overworldMapMarks[i, j].AttemptToSet(cur)))
                        anySetProblems = true;
                    var k = TrackerModel.MapSquareChoiceDomainHelper.IsItem(cur) ? TrackerModel.MapSquareChoiceDomainHelper.SHOP : cur;
                    if (k != -1)
                        TrackerModel.setOverworldMapExtraData(i, j, k, ed);
                    TrackerModel.overworldMapCircles[i, j] = circle;
                    owUpdateFunctions[i, j](0, null);  // redraw the tile
                    owCircleRedraws[i, j]();
                }
            }
            await showProgress("finished loading overworld");
            // Items
            if (!(data.Items.WhiteSwordBox.TryApply(TrackerModel.sword2Box))) anySetProblems = true;
            if (!(data.Items.LadderBox.TryApply(TrackerModel.ladderBox))) anySetProblems = true;
            if (!(data.Items.ArmosBox.TryApply(TrackerModel.armosBox))) anySetProblems = true;
            // the items found in the magical sword / letter / wood sword caves (older saves do not have these)
            if (data.Items.MagicalSwordBox != null)
                if (!(data.Items.MagicalSwordBox.TryApply(TrackerModel.sword3Box))) anySetProblems = true;
            if (data.Items.LetterBox != null)
                if (!(data.Items.LetterBox.TryApply(TrackerModel.letterBox))) anySetProblems = true;
            if (data.Items.WoodSwordBox != null)
                if (!(data.Items.WoodSwordBox.TryApply(TrackerModel.sword1Box))) anySetProblems = true;
            TrackerModel.hintShopColors.Clear();
            if (!(string.IsNullOrEmpty(data.Items.HintShopColors)))
            {
                foreach (var t in data.Items.HintShopColors.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = t.Split(',');
                    if (p.Length == 3)
                        TrackerModel.hintShopColors[Tuple.Create(parseInt(p[0]), parseInt(p[1]))] = parseInt(p[2]);
                }
            }
            TrackerModel.shuffledWoodSwordTiles.Clear();
            if (!(string.IsNullOrEmpty(data.Items.ShuffledWoodSwordTiles)))
            {
                foreach (var t in data.Items.ShuffledWoodSwordTiles.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var p = t.Split(',');
                    if (p.Length == 2)
                        TrackerModel.shuffledWoodSwordTiles.Add(Tuple.Create(parseInt(p[0]), parseInt(p[1])));
                }
            }
            OptionsMenu.triggerRequestRedrawOverworldEvent();   // redraw the tiles now that the cave items are back
            if (data.Items.CustomLevelName != null)   // (an empty name in the save means none was in use: it must not be replaced by the start screen's remembered name; older saves have no name at all: null)
            {
                TrackerModel.CustomLevelName = data.Items.CustomLevelName;
                DungeonUI.triggerCustomLevelNameChanged();   // the custom LEVEL name that was in use when this was saved
            }
            if (data.Items.Level2StairBox != null)   // (older saves do not have the optional level 2 staircase slot)
            {
                if (!(data.Items.Level2StairBox.TryApply(TrackerModel.level2StairBox))) anySetProblems = true;
                TrackerModel.SetLevel2StairBoxEnabled(data.Items.Level2StairBoxEnabled);
                TrackerModel.SetLevel9FloorBoxEnabled(data.Items.Level9FloorBoxEnabled);   // (older saves: false)
            }
            TrackerModelOptions.FourthSwordEnabled = SaveAndLoad.FourthSwordWasOn(data.Items);   // (older saves predate the 4th Sword option: nothing is restricted)
            for (int i = 0; i <= 8; i++)
            {
                var ds = data.Items.Dungeons[i];
                var dd = TrackerModel.GetDungeon(i);
                if (!(ds.TryApply(dd))) anySetProblems = true;
            }
            await showProgress("finished loading items");
            // PlayerProgressAndTakeAnyHearts
            data.PlayerProgressAndTakeAnyHearts.Apply();
            // StartingItemsAndExtras
            data.StartingItemsAndExtras.Apply();
            // Blockers
            TrackerModel.DungeonBlockersContainer.StartIgnoreChangesDuringLoad();
            for (int i = 0; i <= 7; i++)
            {
                for (int j = 0; j <= TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON - 1; j++)
                {
                    TrackerModel.DungeonBlockersContainer.SetDungeonBlocker(i, j, TrackerModel.DungeonBlocker.FromHotKeyName(data.Blockers[i][j].Kind));
                    for (int k = 0; k <= TrackerModel.DungeonBlockerAppliesTo.MAX - 1; k++)
                        TrackerModel.DungeonBlockersContainer.SetDungeonBlockerAppliesTo(i, j, k, data.Blockers[i][j].AppliesTo[k]);
                }
                await showProgress($"finished loading blockers {i + 1} of 8");
            }
            TrackerModel.DungeonBlockersContainer.FinishIgnoreChangesDuringLoad();
            if (anySetProblems)
            { } // TODO
            // Hints
            for (int i = 0; i <= 10; i++)
                TrackerModel.SetLevelHint.Invoke(Tuple.Create(i, TrackerModel.HintZone.FromIndex(data.Hints.LocationHints[i])));
            TrackerModel.NoFeatOfStrengthHintWasGiven = data.Hints.NoFeatOfStrengthHint;
            TrackerModel.SailNotHintWasGiven = data.Hints.SailNotHint;
            TrackerModel.SailToHintWasGiven = data.Hints.SailToHint;
            TrackerModel.PlayMelodyHintWasGiven = data.Hints.PlayMelodyHint;
            TrackerModel.StepOverWaterHintWasGiven = data.Hints.StepOverWaterHint;
            TrackerModel.FireArrowsHintWasGiven = data.Hints.FireArrowsHint;
            hideFeatsOfStrength(TrackerModel.NoFeatOfStrengthHintWasGiven);
            hideRaftSpots(TrackerModel.SailNotHintWasGiven);
            Views.triggerHintDashedItemsChanged();
            // Notes
            notesTextBox.Text = data.Notes;
            // CRDI
            currentRecorderDestinationIndex = data.CurrentRecorderDestinationIndex;
            TrackerModel.recorderToNewDungeons = data.RecorderToNewDungeons;
            TrackerModel.recorderToUnbeatenDungeons = data.RecorderToUnbeatenDungeons;
            if (data.IsBoomstickSeed)
                toggleBookShieldCheckBox.IsChecked = true;
            if (data.IsAtlasSeed)
                bookIsAtlasCheckBox.IsChecked = true;
            if (data.IsWSMSReplacedByBUSeed)
                TrackerModel.ToggleWSMSReplacedByBU.Invoke(null);
            updateCurrentRecorderDestinationNumeral();
            // Dungeon Maps
            await importDungeonModels(showProgress, data.DungeonMaps);
            DungeonUI.theDungeonTabControl.SelectedIndex = 0;   // if they selected the summary tab, it only updates after an actual change, so poke tab 0 first, so the change occurs
            DungeonUI.theDungeonTabControl.SelectedIndex = data.DungeonTabSelected;
            // UserCustom
            if (data.UserCustomChecklist != null && data.UserCustomChecklist.Items != null)
            {
                SaveAndLoad.theUserCustomChecklist = data.UserCustomChecklist;
                // note that line below must happen before we populate Timeline further below, as it updates TrackerModel.TimelineItemModel.All to include UserCustom stuff
                await UserCustomLayer.InitializeUserCustom(cm, timelineItems, invokeExtras);
            }
            // Drawing Layer
            DrawingLayer.LoadDrawingLayer(data.DrawingLayerIcons, drawingCanvas);
            // Graphics.alternativeOverworldMapFilename & Graphics.shouldInitiallyHideOverworldMap were loaded much earlier
            // Seed & Flags
            if (data.Seed != null && data.Seed != "")
            {
                SaveAndLoad.lastKnownSeed = data.Seed;
                SaveAndLoad.seedAndFlagsUpdated.Trigger(null);
            }
            if (data.Flags != null && data.Flags != "")
            {
                SaveAndLoad.lastKnownFlags = data.Flags;
                SaveAndLoad.seedAndFlagsUpdated.Trigger(null);
            }
            // Timeline
            if (data.OverworldSpotsRemainingOverTime != null)
                for (int i = 0; i <= (data.OverworldSpotsRemainingOverTime.Length / 2) - 1; i++)
                    TrackerModel.timelineDataOverworldSpotsRemain.Add(Tuple.Create(data.OverworldSpotsRemainingOverTime[i * 2], data.OverworldSpotsRemainingOverTime[i * 2 + 1]));
            if (data.Timeline != null)
            {
                foreach (var td in data.Timeline)
                {
                    if (td.Seconds <= data.TimeInSeconds)
                        if (TrackerModel.TimelineItemModel.All.TryGetValue(td.Ident, out var v))
                            v.StampTotalSeconds(td.Seconds, TrackerModel.PlayerHas.FromInt(td.Has));
                }
            }
            drawTimeline(data.TimeInSeconds / 60);
            // Timer
            TrackerModel.theStartTime.SetAgo(TimeSpan.FromSeconds((double)data.TimeInSeconds));
            // recompute everything, update UI
            TrackerModel.recomputePlayerStateSummary();
            TrackerModel.recomputeMapStateSummary();
            TrackerModel.recomputeWhatIsNeeded();
            LoadDump.setProblems = anySetProblems;
            }
            catch
            {
                // (a load that fails part-way must not leave the model ignoring updates and the timeline frozen for the rest of the session)
                TrackerModel.currentlyIgnoringForceUpdatesDuringALoad = false;
                Timeline.isCurrentlyLoadingASave = false;
                // (whatever part of the save was applied stays applied, so bring the summaries and the display up to date with it)
                try
                {
                    TrackerModel.recomputePlayerStateSummary();
                    TrackerModel.recomputeMapStateSummary();
                    TrackerModel.recomputeWhatIsNeeded();
                    TrackerModel.forceUpdate();
                }
                catch { }
                throw;
            }
            // done
            TrackerModel.currentlyIgnoringForceUpdatesDuringALoad = false;
            TrackerModel.forceUpdate();
            doUIUpdateEvent.Trigger();
            Timeline.isCurrentlyLoadingASave = false;
            TrackerModel.TimelineItemModel.TriggerTimelineChanged();  // redraw timeline once (redraws are ignored while loading)
            if (LoadDump.isActive)   // --load/--dump: write what the tracker now holds, and exit
                LoadDump.Finish(SaveAndLoad.BuildSaveLines(notesTextBox.Text, DungeonUI.theDungeonTabControl.SelectedIndex, exportDungeonModelsJsonLines(), DungeonSaveAndLoad.SaveDrawingLayer(),
                                        Graphics.alternativeOverworldMapFilename, Graphics.shouldInitiallyHideOverworldMap, currentRecorderDestinationIndex,
                                        toggleBookShieldCheckBox.IsChecked.Value, bookIsAtlasCheckBox.IsChecked.Value));
        }

        // animation
        {
            Color c(byte t) => Color.FromArgb(t, 0, 255, 255);  // Color.FromArgb(t,255uy,165uy,0uy)
            var rgb = new RadialGradientBrush();   // (WPF sets GradientOrigin/Center (0.5,0.5) and RadiusX/RadiusY 0.5, which are Avalonia's defaults)
            rgb.GradientStops.Add(new GradientStop(c(0), 0.0));
            rgb.GradientStops.Add(new GradientStop(c(0), 0.4));
            rgb.GradientStops.Add(new GradientStop(c(0), 1.0));

            var msDuration = 1500;
            // (WPF: a ColorAnimation From c(0) To c(140) over msDuration, AutoReverse, on the third gradient stop's Color. Tweened by hand on a 16 ms timer,
            // as Graphics.TileHighlightRectangle and Views' glow do; starting it again restarts it from c(0), as BeginAnimation does.)
            DispatcherTimer caTimer = null;
            void beginColorAnimation()
            {
                caTimer?.Stop();
                var start = DateTime.Now;
                var stop = rgb.GradientStops[2];
                caTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16.0), DispatcherPriority.Render, (_, _) => { });
                var thisTimer = caTimer;
                thisTimer.Tick += (_, _) =>
                {
                    var t = (DateTime.Now - start).TotalMilliseconds / (double)msDuration;   // 0..2 (up, then back down)
                    if (t >= 2.0)
                    {
                        stop.Color = c(0);
                        thisTimer.Stop();
                        return;
                    }
                    var f = t <= 1.0 ? t : 2.0 - t;
                    stop.Color = c((byte)Math.Round(140.0 * f));
                };
                thisTimer.Start();
            }
            var owHighlightTile = new Rectangle { Width = OMTW, Height = 11.0 * 3.0, StrokeThickness = 0.0, Fill = rgb, Opacity = 1.0, IsHitTestVisible = false };
            canvasAdd(overworldCanvas, owHighlightTile, OMTW * (double)(6), (double)(11 * 3 * 6));

            void animateOWTile(int x, int y)
            {
                if (!Tuple.Create(x, y).Equals(TrackerModel.NOTFOUND))
                {
                    OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks[x, y] = true;
                    owUpdateFunctions[x, y](0, null);  // redraw tile, with icon shown
                    if (TrackerModelOptions.AnimateTileChanges.Value)
                    {
                        Canvas.SetLeft(owHighlightTile, OMTW * (double)(x));
                        Canvas.SetTop(owHighlightTile, (double)(11 * 3 * y));
                        beginColorAnimation();
                    }
                    async Task go()
                    {
                        await Task.Delay(msDuration);   // (resumes on the UI thread: WPF's SwitchToContext ctxt)
                        OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks[x, y] = false;
                        owUpdateFunctions[x, y](0, null);  // redraw tile, with icon possibly hidden
                    }
                    CustomComboBoxes.StartImmediate(go());
                }
            }
            animateOverworldTile = animateOWTile;
        }

        TrackerModel.forceUpdate();
        timer.Start();  // don't start the tick timer updating, until the entire app is loaded
        {  // auto-save every 1 min
            var diskIcon = new Border { BorderThickness = new Thickness(3.0), BorderBrush = Brushes.Gray, Background = Brushes.Gray, Child = Graphics.BMPtoImage(Graphics.iconDisk_bmp), Opacity = 0.0, IsHitTestVisible = false };
            layout.AddDiskIcon(diskIcon);
            var timer2 = new DispatcherTimer();   // (F#: a second `timer` shadowing the first)
            timer2.Interval = TimeSpan.FromSeconds(10.0);   // (checks every 10 seconds; it saves once the Auto-save interval option has passed)
            var lastAutoSave = DateTime.Now;
            var autoSaveFailureShown = false;
            timer2.Tick += (_, _) =>
            {
                if ((DateTime.Now - lastAutoSave).TotalMinutes >= (double)TrackerModelOptions.AutoSaveMinutes - 0.01)
                {
                    lastAutoSave = DateTime.Now;
                    try
                    {
                        var saveType = SaveAndLoad.SaveType.AutoSave;
#if BROWSER
                        // (port: an AutoSave also sets the file's creation time, which stops the WebAssembly runtime, so the page writes it as a save
                        // named like the autosave, in place of the old one)
                        SaveAndLoad.EnsureSavesDirectory();
                        System.IO.File.Delete(SaveAndLoad.AutoSaveFilename);
                        SaveAndLoad.pendingManualSaveName = System.IO.Path.GetFileNameWithoutExtension(SaveAndLoad.AutoSaveFilename);
                        saveType = SaveAndLoad.SaveType.ManualSave;
#endif
                        SaveAndLoad.SaveAll(notesTextBox.Text, DungeonUI.theDungeonTabControl.SelectedIndex, exportDungeonModelsJsonLines(), DungeonSaveAndLoad.SaveDrawingLayer(),
                                                Graphics.alternativeOverworldMapFilename, Graphics.shouldInitiallyHideOverworldMap, currentRecorderDestinationIndex,
                                                toggleBookShieldCheckBox.IsChecked.Value, bookIsAtlasCheckBox.IsChecked.Value, saveType);
                        async Task go()
                        {
                            diskIcon.Opacity = 0.7;
                            await Task.Delay(300);
                            diskIcon.Opacity = 0.4;
                            await Task.Delay(300);
                            diskIcon.Opacity = 0.7;
                            await Task.Delay(300);
                            diskIcon.Opacity = 0.0;
                        }
                        CustomComboBoxes.StartImmediate(go());
                        autoSaveFailureShown = false;   // (it worked: a later failure is worth saying again)
                    }
                    catch (Exception e)
                    {
                        if (!autoSaveFailureShown)   // (say so once: otherwise a Saves folder that cannot be written fails silently for the whole game)
                        {
                            autoSaveFailureShown = true;
                            try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Z1R_Tracker_crash_log.txt"), $"\n\nAUTOSAVE NOTE -- {DateTime.Now:yyyy-MM-dd-HH:mm:ss} -- auto-save failed: {e}\n"); } catch { }   // (also kept in the crash log, so a bug report shows it)
                            Graphics.ErrorBeepWithReminderLogText($"Auto-save failed ({e.Message}). Use the Save button, and check the Saves folder is writable.");
                        }
                    }
                }
            };
            timer2.Start();
        }

        var ARROW_DELTA = 20.0;
        appMainCanvas.MyKeyAdd(ea =>
        {
            if (HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key, out var hotKeyedState))
            {
                ea.Handled = true;
                switch (hotKeyedState)
                {
                    case HotKeys.GlobalHotkeyTargets.ToggleMagicalSword: TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Toggle(); break;
                    case HotKeys.GlobalHotkeyTargets.ToggleWoodSword:    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword.Toggle(); break;
                    case HotKeys.GlobalHotkeyTargets.ToggleBoomBook:     TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBoomBook.Toggle(); break;
                    case HotKeys.GlobalHotkeyTargets.ToggleBlueCandle:   TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle.Toggle(); break;
                    case HotKeys.GlobalHotkeyTargets.ToggleWoodArrow:    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodArrow.Toggle(); break;
                    case HotKeys.GlobalHotkeyTargets.ToggleBlueRing:     TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueRing.Toggle(); break;
                    case HotKeys.GlobalHotkeyTargets.ToggleBombs:        TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBombs.Toggle(); break;
                    case HotKeys.GlobalHotkeyTargets.ToggleGannon:       TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasDefeatedGanon.Toggle(); break;
                    case HotKeys.GlobalHotkeyTargets.ToggleZelda:        TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Toggle(); break;
                    case HotKeys.GlobalHotkeyTargets.DungeonTab1:        selectDungeonTabEvent.Trigger(0); break;
                    case HotKeys.GlobalHotkeyTargets.DungeonTab2:        selectDungeonTabEvent.Trigger(1); break;
                    case HotKeys.GlobalHotkeyTargets.DungeonTab3:        selectDungeonTabEvent.Trigger(2); break;
                    case HotKeys.GlobalHotkeyTargets.DungeonTab4:        selectDungeonTabEvent.Trigger(3); break;
                    case HotKeys.GlobalHotkeyTargets.DungeonTab5:        selectDungeonTabEvent.Trigger(4); break;
                    case HotKeys.GlobalHotkeyTargets.DungeonTab6:        selectDungeonTabEvent.Trigger(5); break;
                    case HotKeys.GlobalHotkeyTargets.DungeonTab7:        selectDungeonTabEvent.Trigger(6); break;
                    case HotKeys.GlobalHotkeyTargets.DungeonTab8:        selectDungeonTabEvent.Trigger(7); break;
                    case HotKeys.GlobalHotkeyTargets.DungeonTab9:        selectDungeonTabEvent.Trigger(8); break;
                    case HotKeys.GlobalHotkeyTargets.DungeonTabS:        selectDungeonTabEvent.Trigger(9); break;
                    // some buttons (e.g. Recorder Destination counter) are Focusable, and clicking them captures future keyboard input
                    case HotKeys.GlobalHotkeyTargets.LeftClick:          Graphics.Win32.LeftMouseClick(); refocusKeyboard(); break;
                    case HotKeys.GlobalHotkeyTargets.MiddleClick:        Graphics.Win32.MiddleMouseClick(); refocusKeyboard(); break;
                    case HotKeys.GlobalHotkeyTargets.RightClick:         Graphics.Win32.RightMouseClick(); refocusKeyboard(); break;
                    case HotKeys.GlobalHotkeyTargets.ScrollUp:           Graphics.Win32.ScrollWheelRotateUp(); refocusKeyboard(); break;
                    case HotKeys.GlobalHotkeyTargets.ScrollDown:         Graphics.Win32.ScrollWheelRotateDown(); refocusKeyboard(); break;
                    case HotKeys.GlobalHotkeyTargets.ToggleCursorOverworldOrDungeon:
                        if (overworldCanvas.IsPointerOver)
                        {
                            var newPos = dungeonTabs.TranslatePoint(posToWarpToWhenTabbingFromOverworld, appMainCanvas).Value;
                            layout.ShowMouseWarp(newPos);
                            Graphics.NavigationallyWarpMouseCursorTo(newPos);
                            layout.FocusDungeon();
                        }
                        else
                        {
                            var newPos = overworldCanvas.TranslatePoint(new Point(OMTW * 8.5, 11.0 * 3.0 * 4.5), appMainCanvas).Value;
                            layout.ShowMouseWarp(newPos);
                            Graphics.NavigationallyWarpMouseCursorTo(newPos);
                            layout.FocusOverworld();
                        }
                        break;
                    // If no element reacted to the arrow key hotkeys, nudge the mouse a small bit.
                    // Note that MainTracker and Blockers specifically leave some arrow-hotkeys unhandled so that this activates.
                    case HotKeys.GlobalHotkeyTargets.MoveCursorLeft:
                    {
                        var pos = mouseGetPosition(appMainCanvas);
                        if (pos.X > ARROW_DELTA)
                            Graphics.SilentlyWarpMouseCursorTo(new Point(pos.X - ARROW_DELTA, pos.Y));
                        break;
                    }
                    case HotKeys.GlobalHotkeyTargets.MoveCursorRight:
                    {
                        var pos = mouseGetPosition(appMainCanvas);
                        if (pos.X < appMainCanvas.Width - ARROW_DELTA)
                            Graphics.SilentlyWarpMouseCursorTo(new Point(pos.X + ARROW_DELTA, pos.Y));
                        break;
                    }
                    case HotKeys.GlobalHotkeyTargets.MoveCursorUp:
                    {
                        var pos = mouseGetPosition(appMainCanvas);
                        if (pos.Y > ARROW_DELTA)
                            Graphics.SilentlyWarpMouseCursorTo(new Point(pos.X, pos.Y - ARROW_DELTA));
                        break;
                    }
                    case HotKeys.GlobalHotkeyTargets.MoveCursorDown:
                    {
                        var pos = mouseGetPosition(appMainCanvas);
                        if (pos.Y < appMainCanvas.Height - ARROW_DELTA)
                            Graphics.SilentlyWarpMouseCursorTo(new Point(pos.X, pos.Y + ARROW_DELTA));
                        break;
                    }
                }
            }
        });

        Layout.setupMouseMagnifier(cm, refocusMainWindow);

        Views.appMainCanvasGlobalBoxMouseOverHighlight.AttachToGlobalCanvas(appMainCanvas);

        Graphics.PlaySoundForSpeechRecognizedAndUsedToMark();  // the very first call to this lags the system for some reason, so get it out of the way at startup
        layout.AllDone();
        Popouts.Startup(doUIUpdateEvent);
        TrackerModel.swordCaveTakenFunc = FuncConvert.FromFunc(() =>
            Enumerable.Range(0, 16).SelectMany(x => Enumerable.Range(0, 8).Select(y => (x, y)))
            .Any(p => TrackerModel.overworldMapMarks[p.x, p.y].Current() == TrackerModel.MapSquareChoiceDomainHelper.SWORD1
                                        && TrackerModel.getOverworldMapExtraData(p.x, p.y, TrackerModel.MapSquareChoiceDomainHelper.SWORD1) == TrackerModel.MapSquareChoiceDomainHelper.SWORD1
                                        && !(TrackerModel.shuffledWoodSwordTiles.Contains(Tuple.Create(p.x, p.y)))));
        if (TrackerModelOptions.ShowCustomOnStartup) ShowRunCustom.DoShowRunCustom(refocusMainWindow);   // reopen the custom SHOW windows if they were showing last time
        MainTrackerTriforcesAndItems.reopenItemProgressWindowIfWasOpen();
        MainTrackerTriforcesAndItems.reopenItemLocationsWindowIfWasOpen();   // like the popouts: reopen at its remembered spot/size if it was open when the program last closed
        await showProgress("all done");
        return drawTimeline;
    }
}
