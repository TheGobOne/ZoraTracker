// Ported from Z1R_WPF/OverworldItemGridUI.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// The F# `open`s of OverworldMapTileCustomization, DungeonUI.AhhGlobalVariables and CustomComboBoxes.GlobalFlag are `using static`s, so
// `hideLocator()`, `popupIsActive` and `OW_ITEM_GRID_LOCATIONS` read as in WPF. The F# events passed in (resetTimerEvent, doUIUpdateEvent:
// `Event<unit>`) are Graphics.SharedEvents. F# model events (TrackerModel's IEvent<_>s) keep their F# type; `e.Add(f)` becomes `onEvent(e, f)`.
// C# `for` loops share one variable across iterations, so loop indexes that closures capture are copied into a local first (F# gives each
// iteration its own).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using static Z1R_Avalonia.OverworldMapTileCustomization;
using static Z1R_Avalonia.DungeonUI.AhhGlobalVariables;
using static Z1R_Avalonia.CustomComboBoxes.GlobalFlag;

namespace Z1R_Avalonia;

// the top-right portion of the main UI

public static class OverworldItemGridUI
{
    private static void onEvent<T>(IEvent<FSharpHandler<T>, T> e, Action<T> f) => e.AddHandler(new FSharpHandler<T>((_, x) => f(x)));
    // (WPF's ToolTipService.SetShowDuration has no Avalonia equivalent; a no-op, as in OptionsMenu)
    private static void setToolTipShowDuration(Control c, int ms) { }

    public static void canvasAdd(Canvas c, Control item, double left, double top) => Graphics.canvasAdd(c, item, left, top);
    public static void gridAdd(Grid g, Control x, int c, int r) => Graphics.gridAdd(g, x, c, r);
    public static void gridAddTuple(Grid g, Control e, (int, int) xy) { var (x, y) = xy; gridAdd(g, e, x, y); }
    public static Grid makeGrid(int nc, int nr, int cw, int rh) => Graphics.makeGrid(nc, nr, cw, rh);

    public static Rectangle makeHintHighlight(double size) => Views.makeHintHighlight(size);

    public static readonly double OMTW = OverworldRouteDrawing.OMTW;  // overworld map tile width - at normal aspect ratio, is 48 (16*3)

    // (F#: a [<RequireQualifiedAccess>] union; an abstract record with one nested record per case, as HotKeys.DungeonRoomHotKey.
    // `ShowLocatorDescriptor.DungeonNumber(n)` → `new ShowLocatorDescriptor.DungeonNumber(n)`, `ShowLocatorDescriptor.Sword1` → `new ShowLocatorDescriptor.Sword1()`.)
    public abstract record ShowLocatorDescriptor
    {
        private ShowLocatorDescriptor() { }
        public sealed record DungeonNumber(int Value) : ShowLocatorDescriptor;   // 0-7 means dungeon 1-8
        public sealed record DungeonIndex(int Value) : ShowLocatorDescriptor;    // 0-8 means 123456789 or ABCDEFGH9 in top-left-ui presentation order
        public sealed record Sword1() : ShowLocatorDescriptor;
        public sealed record Sword2() : ShowLocatorDescriptor;
        public sealed record Sword3() : ShowLocatorDescriptor;
    }

    // some global variables needed across various UI components
    public const int TCH = 127;  // timeline height   (port: const; see DungeonUI.TH)
    public const int TH = DungeonUI.TH; // text height   (port: const; see DungeonUI.TH)
    public static readonly double THRU_MAIN_MAP_H = (double)(150 + 8 * 11 * 3);
    public static readonly double THRU_MAP_AND_LEGEND_H = THRU_MAIN_MAP_H + (double)(11 * 3);
    public static readonly double THRU_MAIN_MAP_AND_ITEM_PROGRESS_H = THRU_MAP_AND_LEGEND_H + 30.0;
    public static readonly double THRU_BLOCKERS_H = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H + 36.0 * 3.0;
    public static readonly double START_DUNGEON_AND_NOTES_AREA_H = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H;
    public static readonly double THRU_DUNGEON_AND_NOTES_AREA_H = START_DUNGEON_AND_NOTES_AREA_H + (double)(TH + 30 + (3 + 27 * 8 + 12 * 7 + 3) + 3);  // 3 is for a little blank space after this but before timeline
    public static readonly double START_TIMELINE_H = THRU_DUNGEON_AND_NOTES_AREA_H;
    public static readonly double THRU_TIMELINE_H = START_TIMELINE_H + (double)TCH;
    public static readonly double LEFT_OFFSET = 78.0;
    public static readonly double BLOCKERS_AND_NOTES_OFFSET = 408.0 + 42.0;  // dungeon area and side-tracker-panel
    public static readonly double ITEM_PROGRESS_FIRST_ITEM = 93.0;   // the item progress row starts right after the 'Item Progress' button (which is at x=0); the 3-dot box and the view rects follow this value
    // (port: large text placed by pixel, so its glyphs are moved to where WPF draws them; see Graphics.matchWpfTextTop)
    private static TextBox make_hmsTimeTextBox()
    {
        var tb = new TextBox { Width = 148.0, Height = 56.0, Text = "timer", FontSize = 42.0, Background = Brushes.Transparent, Foreground = Brushes.LightGreen, BorderThickness = new Thickness(0.0), IsReadOnly = true, IsHitTestVisible = false };
        Graphics.matchWpfTextTop(tb);
        return tb;
    }
    public static readonly TextBox hmsTimeTextBox = make_hmsTimeTextBox();
    // shown in the timer's spot (same font, scaled down to fit) while the timer is hidden
    public static readonly bool SHOW_NICE_TIMER_BRO = false;   // set to true to bring back the 'Nice timer bro' text in the timer's spot while the timer is hidden
    // (WPF: Visibility=Hidden; Avalonia: Opacity 0, and it is never hit-test visible anyway)
    private static Viewbox make_niceTimerBox()
    {
        var tb = new TextBlock { Text = "Nice timer bro", FontSize = 42.0, FontFamily = hmsTimeTextBox.FontFamily, Foreground = Brushes.LightGreen, VerticalAlignment = VerticalAlignment.Center };
        return new Viewbox { Width = 140.0, Height = 46.0, Stretch = Stretch.Uniform, IsHitTestVisible = false, Child = tb, Opacity = 0.0 };
    }
    public static readonly Viewbox niceTimerBox = make_niceTimerBox();
    private static TextBox make_broadcastTimeTextBox()
    {
        var r = new TextBox { Text = "timer", FontSize = 42.0, Background = Brushes.Black, Foreground = Brushes.LightGreen, BorderThickness = new Thickness(0.0), IsReadOnly = true, IsHitTestVisible = false };
        Graphics.matchWpfTextTop(r);
        void timerOpacity()
        {
            r.Opacity = TrackerModelOptions.HideTimer.Value ? 0.0 : 1.0;
            hmsTimeTextBox.Opacity = TrackerModelOptions.HideTimer.Value ? 0.0 : 1.0;
            niceTimerBox.Opacity = SHOW_NICE_TIMER_BRO && TrackerModelOptions.HideTimer.Value ? 1.0 : 0.0;   // (WPF: Visibility Visible / Hidden)
        }
        OptionsMenu.hideTimerChanged += timerOpacity;
        return r;
    }
    public static readonly TextBox broadcastTimeTextBox = make_broadcastTimeTextBox();

    // some global mutable variables needed across various UI components
    public static bool displayIsCurrentlyMirrored = false;
    public static TextBox notesTextBox = null;
    public static int currentRecorderDestinationIndex = 0;

    public static Action<bool> hideFeatsOfStrength = (bool _b) => { };
    public static Action<bool> hideRaftSpots = (bool _b) => { };

    public static Func<string[]> exportDungeonModelsJsonLines = () => (string[])null;
    public static Action legendStartIconButtonBehavior = () => { };

    // (open DungeonUI.AhhGlobalVariables)
    public static Action<int, int> showLocatorExactLocation = (int _x, int _y) => { };
    public static Action<List<(int, int)>> showLocatorExactLocations = (List<(int, int)> _locs) => { };   // the same magenta crosshairs, on several tiles at once
    public static Action<TrackerModel.HintZone, bool> showLocatorHintedZone = (TrackerModel.HintZone _hz, bool _also) => { };
    public static Action<Func<int, int, bool>> showLocatorInstanceFunc = (Func<int, int, bool> _f) => { };
    public static Action<Func<int, int, bool>> showLocatorInstanceFuncAll = (Func<int, int, bool> _f) => { };   // same, but also highlights tiles that are already marked / completed
    public static Action showHintShopLocator = () => { };
    public static Action<Control, int, double> addHintZoomHook = (Control _fe, int _i, double _x) => { };   // set by WPFUI: the 3x hint zoom shown when hovering a hint box
    public static Action showArrowLocator = () => { };   // highlights every tile you put a wood/silver arrow marker icon on
    public static Action showMeatLocator = () => { };   // highlights every tile you put a meat marker icon on
    public static Action showBombMarkerLocator = () => { };   // highlights every tile you put a bomb marker icon on (adds to whatever the bomb-shop locator already shows)
    public static Action showSwordLocator = () => { };   // highlights every tile you put a sword marker icon on
    public static Action<List<int>> showMarkerItemsLocator = (List<int> _idents) => { };   // additionally highlights every tile showing one of these items (marker icons / cave item boxes)
    public static Action showKeyLocator = () => { };   // highlights every tile you put a key marker icon on
    public static Action showLocatorPotionAndTakeAny = () => { };
    public static Action showLocatorRupees = () => { };
    public static Action showLocatorNoneFound = () => { };
    public static Action<ShowLocatorDescriptor> showLocator = (ShowLocatorDescriptor _sld) => { };

    public static readonly SolidColorBrush hintBG = Graphics.freeze(new SolidColorBrush(Color.FromArgb(255, 0, 0, 120)));
    public static Control HintZoneDisplayTextBox(string s)
    {
        if (s == "")
            return new Canvas();   // don't display anything, we pass "" for HintZone.Unknown on the MainTracker, and also want to avoid hintBG from the textbox
        else
        {
            var tb = new TextBox { Text = s, Foreground = Brushes.Orange, Background = hintBG, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0),
                                   FontSize = 12.0, FontWeight = FontWeight.Bold, HorizontalContentAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center,
                                   HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            return Graphics.center(tb, 24, 24);
        }
    }
    // redraws a 30x30 location-hint canvas: the hinted zone's two letters, or (when there is no hint yet) a barely-visible white question mark to show that it can be clicked to pick one
    public static readonly Dictionary<Canvas, TrackerModel.HintZone> hintCanvasLastZone = new Dictionary<Canvas, TrackerModel.HintZone>();
    public static void RedrawHintCanvas(Canvas c, TrackerModel.HintZone hz)
    {
        hintCanvasLastZone[c] = hz;
        c.Children.Clear();
        if (hz.Equals(TrackerModel.HintZone.UNKNOWN))
        {
            if (TrackerModelOptions.ShowDungeonHintIcons.Value)
            {
                // a barely-visible white question mark icon (no box), to show the spot can be clicked to pick a hint
                var qm = new Canvas { Width = 14.0, Height = 22.0, IsHitTestVisible = false, Opacity = 0.35 };   // (WPF: 0.12; brighter here, as the port's question mark was hard to see)
                qm.Children.Add(new Rectangle { Width = 14.0, Height = 22.0, Fill = Brushes.White });
                var t = new TextBlock { Text = "?", FontSize = 18.0, FontWeight = FontWeight.Bold, Foreground = Brushes.Black, Width = 14.0, TextAlignment = TextAlignment.Center };
                canvasAdd(qm, t, 0.0, -1.0);
                canvasAdd(c, qm, 8.0, 4.0);
            }
        }
        else
            canvasAdd(c, HintZoneDisplayTextBox(hz.AsDisplayTwoChars()), 3.0, 3.0);
    }
    private static bool init1()
    {
        OptionsMenu.showDungeonHintIconsOptionChanged += () =>
        {
            foreach (var kv in hintCanvasLastZone.ToList()) RedrawHintCanvas(kv.Key, kv.Value);
        };
        return true;
    }
    private static readonly bool _init1 = init1();
    private static SolidColorBrush make_hintyBrush()
    {
        var (HHS, HHE) = (Views.HHS, Views.HHE);
        var c = Color.FromRgb((byte)(HHS.R / 2 + HHE.R / 2), (byte)(HHS.G / 2 + HHE.G / 2), (byte)(HHS.B / 2 + HHE.B / 2));
        return Graphics.freeze(new SolidColorBrush(c));
    }
    public static readonly SolidColorBrush hintyBrush = make_hintyBrush();
    private static (Border, double, double) make_learnMoreHintDecoration()
    {
        var img = Graphics.BMPtoImage(Graphics.z1rSampleHintBMP);
        var showHotKeysWidthToRightEdge = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H - THRU_MAIN_MAP_H + 115.0;
        var w = showHotKeysWidthToRightEdge - 24.0;
        img.Height = img.Height / img.Width * w;
        img.Width = w;
        var sp = new StackPanel { Orientation = Orientation.Vertical };
        var tb = new TextBox { Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 12.0, Margin = new Thickness(3.0, 0.0, 0.0, 0.0) };
        tb.Text = "Learn more about z1r hints via\nthe Hint Decoder button\n<--\n\nSample Hint:";
        sp.Children.Add(new DockPanel { Height = 6.0 });
        sp.Children.Add(tb);
        sp.Children.Add(new Border { BorderBrush = Brushes.DarkSlateBlue, BorderThickness = new Thickness(3.0), Child = img, Width = showHotKeysWidthToRightEdge - 18.0, Margin = new Thickness(6.0) });
        var b = new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(3.0), Background = Brushes.Black, Child = sp, Width = showHotKeysWidthToRightEdge, IsHitTestVisible = false };
        return (b, 16.0 * OMTW - showHotKeysWidthToRightEdge, THRU_MAIN_MAP_H);
    }
    public static readonly (Border, double, double) learnMoreHintDecoration = make_learnMoreHintDecoration();
    // (F#: Async<HintZone option>; returns (hasResult, result), DoModalGridSelect's tuple passed through)
    public static async Task<(bool hasResult, TrackerModel.HintZone result)> FastHintSelector(CustomComboBoxes.CanvasManager cm, int levelHintIndex, double px, double py, int activationDelta, bool shouldDescriptionGoOnLeft)
    {
        Func<string, Control> tb = HintZoneDisplayTextBox;
        (Control, bool, TrackerModel.HintZone) gesai(TrackerModel.HintZone hz) => (tb(hz.AsDisplayTwoChars()), true, hz);
        (Control, bool, TrackerModel.HintZone)[] make_gridElementsSelectablesAndIDs()
        {
            var zones = new[] { TrackerModel.HintZone.DEATH_MOUNTAIN, TrackerModel.HintZone.RIVER, TrackerModel.HintZone.LOST_HILLS, TrackerModel.HintZone.COAST,
                                TrackerModel.HintZone.GRAVE, TrackerModel.HintZone.LAKE, TrackerModel.HintZone.DESERT,
                                TrackerModel.HintZone.DEAD_WOODS, TrackerModel.HintZone.NEAR_START, TrackerModel.HintZone.FOREST };
            var blank = ((Control)new Canvas(), true, TrackerModel.HintZone.UNKNOWN);   // the blank cell: select it to clear the hint
            if (TrackerModelOptions.AlphabetizeHintZones.Value)
                // zones alphabetized by their two-letter codes, then UN, then the blank cell last
                // (F#'s string comparison is ordinal)
                return zones.OrderBy(z => z.AsDisplayTwoChars(), StringComparer.Ordinal).Select(gesai).Concat(new[] { gesai(TrackerModel.HintZone.UNKNOWN) }).Concat(new[] { blank }).ToArray();
            else
                return zones[0..7].Select(gesai).Concat(new[] { blank }).Concat(zones[7..].Select(gesai)).Concat(new[] { gesai(TrackerModel.HintZone.UNKNOWN) }).ToArray();
        }
        var gridElementsSelectablesAndIDs = make_gridElementsSelectablesAndIDs();
        int make_originalStateIndex()
        {
            var h = TrackerModel.GetLevelHint.Invoke(levelHintIndex);
            var idx = Array.FindLastIndex(gridElementsSelectablesAndIDs, t => t.Item3.Equals(h));
            if (idx < 0) throw new KeyNotFoundException();   // (F#'s Array.findIndexBack throws when nothing matches)
            return idx;
        }
        var originalStateIndex = make_originalStateIndex();
        CustomComboBoxes.PopupClickBehavior<TrackerModel.HintZone> onClick(PointerPressedEventArgs _ea, TrackerModel.HintZone ident) => new CustomComboBoxes.PopupClickBehavior<TrackerModel.HintZone>.DismissPopupWithResult(ident);
        var tile = new Canvas { Width = 24.0, Height = 24.0, Background = Brushes.Black };
        var levelHintDescription = new TextBox { Text = "", Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 20.0 };
        var levelHintLocation    = new TextBox { Text = "", Foreground = Brushes.Orange, Background = hintBG, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 20.0 };
        Control boxDecoration = new Border { BorderBrush = hintyBrush, BorderThickness = new Thickness(3.0), Width = 30.0, Height = 30.0 };
        Control make_descDecoration()
        {
            var b = new Border { Background = Brushes.Black, BorderBrush = Brushes.DarkGray, BorderThickness = new Thickness(3.0), Width = 200.0, Height = 112.0 };
            var sp = new StackPanel { Orientation = Orientation.Vertical };
            sp.Children.Add(levelHintDescription);
            sp.Children.Add(levelHintLocation);
            b.Child = sp;
            return b;
        }
        var descDecoration = make_descDecoration();
        void redrawTile(TrackerModel.HintZone hz)
        {
            tile.Children.Clear();
            tile.Children.Add(HintZoneDisplayTextBox(hz.AsDisplayTwoChars()));
            levelHintDescription.Text = levelHintIndex < OverworldData.hintMeaningsDecriptionTextForUI.Length ? OverworldData.hintMeaningsDecriptionTextForUI[levelHintIndex] : levelHintIndex == 11 ? "Ladder item location hint" : "Armos item location hint";
            levelHintLocation.Text = HotKeys.HintZoneHotKeyProcessor.AppendHotKeyToDescription(hz.ToString(), hz);
            hideLocator();
            showLocatorHintedZone(hz, false);
        }
        var brushes = new CustomComboBoxes.ModalGridSelectBrushes(hintyBrush, Brushes.Lime, Brushes.Red, Brushes.DarkGray);
        var extraDecorations = new List<(Control, double, double)> {
            (shouldDescriptionGoOnLeft ? (descDecoration, -203.0, 27.0) : (descDecoration, 153.0, 27.0)),
            (boxDecoration, -3.0, 27.0),
        };
        var r = await CustomComboBoxes.DoModalGridSelect<TrackerModel.HintZone, TrackerModel.HintZone>(cm, px, py, tile, gridElementsSelectablesAndIDs, originalStateIndex, activationDelta, (4, 3, 24, 24),
                                    17.0, 12.0, 27.0, 27.0, redrawTile, onClick, extraDecorations, brushes, new CustomComboBoxes.GridClickDismissalWarpReturn.WarpToCenter(), null, "FastHintSelector", 0.2);
        hideLocator();
        return r;
    }
    // (WPF: fe.MouseWheel / fe.MouseDown; whole wheel notches only, see Graphics.wheelNotch. showLocatorDescriptorOpt is null for None.)
    public static void ApplyFastHintSelectorBehavior(CustomComboBoxes.CanvasManager cm, (double, double) pxy, Control fe, int i, bool activateOnClickAndAcceptHotKey, ShowLocatorDescriptor showLocatorDescriptorOpt, bool shouldDescriptionGoOnLeft)
    {
        var (px, py) = pxy;
        fe.PointerWheelChanged += (sender, x) =>
        {
            var notch = Graphics.wheelNotch(sender, x);
            if (notch == 0) return;
            if (!popupIsActive)
            {
                popupIsActive = true;
                Graphics.SilentlyWarpMouseCursorTo(new Point(px + 15.0, py + 15.0));   // white/magical sword can activate scroll from icons below; just always center on box when activated
                async Task go()
                {
                    var r = await FastHintSelector(cm, i, px + 3.0, py + 3.0, (notch < 0 ? 1 : -1), shouldDescriptionGoOnLeft);
                    if (r.hasResult) TrackerModel.SetLevelHint.Invoke(Tuple.Create(i, r.result));
                    popupIsActive = false;
                }
                CustomComboBoxes.StartImmediate(go());
            }
        };
        if (activateOnClickAndAcceptHotKey)
        {
            fe.PointerPressed += (_, ea) =>
            {
                if (!popupIsActive)
                {
                    popupIsActive = true;
                    ea.Handled = true;
                    async Task go()
                    {
                        var r = await FastHintSelector(cm, i, px + 3.0, py + 3.0, 0, shouldDescriptionGoOnLeft);
                        if (r.hasResult) TrackerModel.SetLevelHint.Invoke(Tuple.Create(i, r.result));
                        popupIsActive = false;
                    }
                    CustomComboBoxes.StartImmediate(go());
                }
            };
            fe.MyKeyAdd(ea =>
            {
                if (HotKeys.HintZoneHotKeyProcessor.TryGetValue(ea.Key, out var hz))
                {
                    ea.Handled = true;
                    TrackerModel.SetLevelHint.Invoke(Tuple.Create(i, hz));
                }
            });
        }
        if (showLocatorDescriptorOpt is ShowLocatorDescriptor sld)
        {
            fe.PointerEntered += (_, _) => showLocator(sld);
            fe.PointerExited += (_, _) => { if (!popupIsActive) hideLocator(); };  // don't hide when popup active, else that hides right as they activate the popup and first display does not show
        }
    }

    // (F#: a 13-tuple; a C# value tuple with the same elements in the same order. invokeExtras (an Async<unit>) is a Func<Task>; highlightOpenCavesCB is null when not standard Hyrule.)
    // boxItemImpl is WPFUI's `(TimelineID, Box, bool) -> FrameworkElement`; resetTimerEvent/doUIUpdateEvent are Graphics.SharedEvents; makeManualSave returns the save's file name.
    public static (Canvas, Canvas, Action<Canvas>, Action<Canvas>, Canvas, Func<Task>, Grid, CheckBox, CheckBox, CheckBox, Button, Button, Border) MakeItemGrid(
                    CustomComboBoxes.CanvasManager cm, Func<TimelineID, TrackerModel.Box, bool, Control> boxItemImpl, List<TimelineItem> timelineItems, OverworldData.OverworldInstance owInstance,
                    Image extrasImage, Graphics.SharedEvent resetTimerEvent, bool isStandardHyrule, Graphics.SharedEvent doUIUpdateEvent, Func<string> makeManualSave)
    {
        var owItemGrid = makeGrid(8, 4, 30, 30);
        // ow 'take any' hearts
        for (var i_ = 0; i_ <= 3; i_++)
        {
            var i = i_;   // (each closure gets its own i, as in F#)
            var c = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black };
            void redraw()
            {
                c.Children.Clear();
                var curState = TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyHeart(i);
                if (curState == 0) canvasAdd(c, Graphics.BMPtoImage(Graphics.owHeartEmpty_bmp), 0.0, 0.0);
                else if (curState == 1) canvasAdd(c, Graphics.BMPtoImage(Graphics.owHeartFull_bmp), 0.0, 0.0);
                else if (TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyKind(i) == 2) canvasAdd(c, Graphics.BMPtoImage(Graphics.blue_candle_bmp), 4.0, 4.0);
                else if (TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyKind(i) == 3) canvasAdd(c, Graphics.BMPtoImage(Graphics.red_potion_bmp), 4.0, 4.0);
                else { canvasAdd(c, Graphics.BMPtoImage(Graphics.owHeartEmpty_bmp), 0.0, 0.0); Graphics.placeSkippedItemXDecoration(c); }
            }
            redraw();
            onEvent(TrackerModel.playerProgressAndTakeAnyHearts.TakeAnyHeartChanged, n => { if (n == i) redraw(); });
            // left click cycles empty -> heart -> blue candle -> potion (right click goes back); the take any cave tile follows
            void f(bool b)
            {
                var d = b ? 1 : -1;
                var next = (TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyKind(i) + d + 4) % 4;
                // while a take any cave is holding this slot, 'empty' is skipped (clear the cave to give it back)
                var hasCave = OverworldMapTileCustomization.takeAnyTaken.Values.Any(v => v.Item1 == i);
                next = hasCave && next == 0 ? (next + d + 4) % 4 : next;
                OverworldMapTileCustomization.setTakeAnySlotKind(i, next);
            }
            // (WPF: MouseLeftButtonDown / MouseRightButtonDown / MouseWheel)
            c.PointerPressed += (_, ea) =>
            {
                var kind = ea.GetCurrentPoint(c).Properties.PointerUpdateKind;
                if (kind == PointerUpdateKind.LeftButtonPressed) f(true);
                else if (kind == PointerUpdateKind.RightButtonPressed) f(false);
            };
            c.PointerWheelChanged += (sender, x) => { var notch = Graphics.wheelNotch(sender, x); if (notch == 0) return; f(notch < 0); };
            c.PointerEntered += (_, _) => showLocatorPotionAndTakeAny();
            c.PointerExited += (_, _) => hideLocator();
            Views.appMainCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(c);
            var (HEARTX, HEARTY) = OW_ITEM_GRID_LOCATIONS.HEARTS;
            gridAdd(owItemGrid, c, HEARTX + i, HEARTY);
            timelineItems.Add(new TimelineItem((i + 1) switch
                                                {
                                                    1 => TimelineID.TakeAnyHeart1,
                                                    2 => TimelineID.TakeAnyHeart2,
                                                    3 => TimelineID.TakeAnyHeart3,
                                                    4 => TimelineID.TakeAnyHeart4,
                                                    _ => throw new Exception("bad take any #")
                                                }, () => Graphics.heartFromTakeAny_bmp));
        }
        // ladder, armos, white sword items
        foreach (var b in new[] { TrackerModel.ladderBox, TrackerModel.armosBox, TrackerModel.sword2Box, TrackerModel.sword3Box, TrackerModel.letterBox }) Views.redBorderBoxes.Add(b);   // these four boxes always have a red outline when empty
        var ladderBoxImpl = boxItemImpl(TimelineID.LadderBox, TrackerModel.ladderBox, true);
        var armosBoxImpl  = boxItemImpl(TimelineID.ArmosBox, TrackerModel.armosBox, false);
        var sword2BoxImpl = boxItemImpl(TimelineID.WhiteSwordBox, TrackerModel.sword2Box, true);
        gridAddTuple(owItemGrid, ladderBoxImpl, OW_ITEM_GRID_LOCATIONS.LADDER_ITEM_BOX);
        gridAddTuple(owItemGrid, armosBoxImpl,  OW_ITEM_GRID_LOCATIONS.ARMOS_ITEM_BOX);
        gridAddTuple(owItemGrid, sword2BoxImpl, OW_ITEM_GRID_LOCATIONS.WHITE_SWORD_ITEM_BOX);
        // letter cave: item box with the letter icon below it
        var letterBoxImpl = boxItemImpl(TimelineID.LetterCaveBox, TrackerModel.letterBox, true);
        gridAddTuple(owItemGrid, letterBoxImpl, OW_ITEM_GRID_LOCATIONS.LETTER_ITEM_BOX);
        // (WPF: re-raises the MouseDown on newDest; Avalonia re-raises the PointerPressed args)
        Control rerouteClick(Control fe, Control newDest) { fe.PointerPressed += (_, ea) => newDest.RaiseEvent(ea); return fe; }
        var ladderIcon = Graphics.BMPtoImage(Graphics.ladder_bmp);
        gridAddTuple(owItemGrid, rerouteClick(ladderIcon, ladderBoxImpl), OW_ITEM_GRID_LOCATIONS.LADDER_ICON);
        ladderIcon.PointerEntered += (_, _) => showLocatorExactLocation(15, 5);   // F16, the coast item
        ladderIcon.PointerExited += (_, _) => hideLocator();
        var armos = Graphics.BMPtoImage(Graphics.ow_key_armos_bmp);
        armos.PointerEntered += (_, _) =>
        {
            var loc = TrackerModel.mapStateSummary.ArmosLocation;
            if (!loc.Equals(TrackerModel.NOTFOUND)) showLocatorExactLocation(loc.Item1, loc.Item2); else showLocatorInstanceFunc(owInstance.HasArmos);
        };
        armos.PointerExited += (_, _) => hideLocator();
        gridAddTuple(owItemGrid, rerouteClick(armos, armosBoxImpl), OW_ITEM_GRID_LOCATIONS.ARMOS_ICON);
        var letterIcon = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Transparent };
        canvasAdd(letterIcon, Graphics.BMPtoImage(CustomComboBoxes.boxCurrentBMP(TrackerModel.ITEMS.LETTER, null)), 4.0, 4.0);
        letterIcon.PointerEntered += (_, _) =>
        {
            // the pink crosshairs on the tile marked as the letter cave
            var found = new List<(int, int)>();
            for (var x = 0; x <= 15; x++) for (var y = 0; y <= 7; y++) if (TrackerModel.overworldMapMarks[x, y].Current() == TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER) found.Add((x, y));
            if (found.Count > 0) showLocatorExactLocation(found[0].Item1, found[0].Item2);
        };
        letterIcon.PointerExited += (_, _) => hideLocator();
        gridAddTuple(owItemGrid, rerouteClick(letterIcon, letterBoxImpl), OW_ITEM_GRID_LOCATIONS.LETTER_ICON);
        // magical sword: the item box (holds the item found in the magical sword cave) with a plain, non-interactive magical sword icon below it
        var sword3BoxImpl = boxItemImpl(TimelineID.MagicalSwordCaveBox, TrackerModel.sword3Box, true);
        gridAddTuple(owItemGrid, sword3BoxImpl, OW_ITEM_GRID_LOCATIONS.MAGS_ITEM_BOX);
        var magsIconOnly = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Transparent };
        magsIconOnly.PointerEntered += (_, _) => showLocator(new ShowLocatorDescriptor.Sword3());
        magsIconOnly.PointerExited += (_, _) => hideLocator();
        canvasAdd(magsIconOnly, Graphics.BMPtoImage(Graphics.ow_sword3_item_bmp), 4.0, 4.0);
        gridAddTuple(owItemGrid, magsIconOnly, OW_ITEM_GRID_LOCATIONS.MAGS_BOX);
        var white_sword_canvas = new Canvas { Width = 30.0, Height = 30.0 };
        var wsHintCanvas = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Transparent };  // Background to accept mouse input
        void redrawWhiteSwordCanvas(Canvas c)
        {
            c.Children.Clear();
            if (!(TrackerModel.playerComputedStateSummary.HaveWhiteSwordItem) &&           // don't have it yet
                    TrackerModel.mapStateSummary.Sword2Location.Equals(TrackerModel.NOTFOUND) &&    // have not found cave
                    !TrackerModel.GetLevelHint.Invoke(9).Equals(TrackerModel.HintZone.UNKNOWN))        // have a hint
                canvasAdd(c, makeHintHighlight(21.0), 4.0, 4.0);
            canvasAdd(c, Graphics.BMPtoImage(Graphics.ow_sword2_item_bmp), 4.0, 4.0);
            Views.drawTinyIconIfLocationIsOverworldBlock(c, owInstance, TrackerModel.mapStateSummary.Sword2Location);
        }
        redrawWhiteSwordCanvas(white_sword_canvas);
        {   // (also with Hide Dungeon Numbers: the numbered triforce row that used to occupy this space is gone)
            // location hint boxes in the row above the item boxes: white sword (9) and magical sword (10) only (the ladder and armos boxes need no hint)
            // (this is kinda gross, poking these onto appMainCanvas, but it doesn't fit into normal layout rules easily)
            var hintSpecs = new List<((int, int), int, ShowLocatorDescriptor)> { (OW_ITEM_GRID_LOCATIONS.WHITE_SWORD_ITEM_BOX, 9, new ShowLocatorDescriptor.Sword2()),
                                                                                 (OW_ITEM_GRID_LOCATIONS.MAGS_ITEM_BOX, 10, new ShowLocatorDescriptor.Sword3()) };
            foreach (var (loc, idx, desc) in hintSpecs)
            {
                var (bx, _) = OW_ITEM_GRID_LOCATIONS.Locate(loc);
                var hc = idx == 9 ? wsHintCanvas : new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Transparent };
                canvasAdd(cm.AppMainCanvas, hc, bx, 0.0);
                RedrawHintCanvas(hc, TrackerModel.GetLevelHint.Invoke(idx));
                onEvent(TrackerModel.LevelHintChanged.Invoke(idx), hz =>
                {
                    RedrawHintCanvas(hc, hz);
                    if (idx == 9) redrawWhiteSwordCanvas(white_sword_canvas);
                });
                ApplyFastHintSelectorBehavior(cm, (bx, 0.0), hc, idx, true, desc, true);
                Views.appMainCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(hc);
                addHintZoomHook(hc, idx, bx);
            }
            var (wsx, _) = OW_ITEM_GRID_LOCATIONS.Locate(OW_ITEM_GRID_LOCATIONS.WHITE_SWORD_ITEM_BOX);
            ApplyFastHintSelectorBehavior(cm, (wsx, 0.0), white_sword_canvas, 9, false, null, true);
        }
        /*  don't need to do this, as redrawWhiteSwordCanvas() is currently called every doUIUpdate, heh
        // redraw after we can look up its new location coordinates
        let newLocation = Views.SynthesizeANewLocationKnownEvent(TrackerModel.mapSquareChoiceDomain.Changed |> Event.filter (fun (_,key) -> key=TrackerModel.MapSquareChoiceDomainHelper.SWORD2))
        newLocation.Add(fun _ -> redrawWhiteSwordCanvas())
        */
        gridAddTuple(owItemGrid, rerouteClick(white_sword_canvas, sword2BoxImpl), OW_ITEM_GRID_LOCATIONS.WHITE_SWORD_ICON);
        white_sword_canvas.PointerEntered += (_, _) => showLocator(new ShowLocatorDescriptor.Sword2());
        white_sword_canvas.PointerExited += (_, _) => hideLocator();

        var extrasCanvasGlobalBoxMouseOverHighlight = new Views.GlobalBoxMouseOverHighlight(Brushes.Gold);   // gold (not the usual turquoise) hover box inside the Starting Items / extras popup
        // brown sword, blue candle, blue ring, magical sword
        // the bomb / Ganon / Zelda boxes (and the money icon) show a blue, rather than red, border while unmarked
        SolidColorBrush unmarkedBoxBrush = CustomComboBoxes.no;   // (F#: a ref cell)
        var blueUnmarkedBrush = Graphics.freeze(new SolidColorBrush(Color.FromRgb(30, 144, 255)));
        T withBlueUnmarked<T>(Func<T> f)
        {
            unmarkedBoxBrush = blueUnmarkedBrush;
            var r = f();
            unmarkedBoxBrush = CustomComboBoxes.no;
            return r;
        }
        // (timelineID is null for None)
        Canvas veryBasicBoxImpl(System.Drawing.Bitmap bmp, TimelineID timelineID, TrackerModel.BoolProperty prop, TrackerModel.IEventingReader<bool> located, TrackerModel.IEventingReader<bool> superseded)
        {
            var c = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black };
            IBrush no = unmarkedBoxBrush;
            IBrush yes = CustomComboBoxes.yes;
            IBrush loc = Brushes.Yellow;
            IBrush sup = CustomComboBoxes.skipped;
            var rect = new Rectangle { Width = 30.0, Height = 30.0, StrokeThickness = 3.0 };
            var innerc = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Transparent };  // just has item drawn on it, not the box
            System.Drawing.Bitmap bmpF()
            {
                if (timelineID is TimelineID tid)
                {
                    if (tid == TimelineID.MagicalSword)
                        // for the overworld item grid shopping area
                        return TrackerModel.IsWSMSReplacedByBU.Invoke(null) ? Graphics.ws_ms_bomb_upgrade_bmp : Graphics.ow_sword3_item_bmp;
                    else
                        return bmp;
                }
                else return bmp;
            }
            void redraw()
            {
                innerc.Children.Clear();
                canvasAdd(innerc, Graphics.BMPtoImage(bmpF()), 4.0, 4.0);
                c.Children.Clear();
                c.Children.Add(rect);
                c.Children.Add(innerc);
                // the magical sword box gets its purple X as soon as a 4th sword has been collected, even if the magical sword itself was also collected
                var supersededWins = timelineID is TimelineID tid ? tid == TimelineID.MagicalSword && superseded.Value : false;
                if (prop.Value() && !supersededWins)
                {
                    rect.Stroke = yes;
                    if (object.Equals(TrackerModel.foundBombShop, located) && located.Value)  // special case handling for bombs
                    {
                        canvasAdd(c, new Line { StartPoint = new Point(0.0, 1.5), EndPoint = new Point(30.0, 1.5), StrokeThickness = 3.0, Stroke = loc }, 0.0, 0.0);
                        canvasAdd(c, new Line { StartPoint = new Point(28.5, 0.0), EndPoint = new Point(28.5, 30.0), StrokeThickness = 3.0, Stroke = loc }, 0.0, 0.0);
                    }
                }
                else if (superseded.Value)
                {
                    rect.Stroke = sup;
                    Graphics.placeSkippedItemXDecoration(c);
                }
                else if (located.Value)
                    rect.Stroke = loc;
                else
                    rect.Stroke = no;
            }
            redraw();
            onEvent(prop.Changed, _ => redraw());
            onEvent(located.Changed, _ => redraw());
            onEvent(superseded.Changed, _ => redraw());
            onEvent(TrackerModel.IsWSMSReplacedByBUChanged, _ => redraw());
            c.PointerPressed += (_, _) => prop.Toggle();
            Views.appMainCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(c);
            extrasCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(c);
            if (timelineID is TimelineID tid2)
            {
                // Progressive Items: the upgraded sword entries show the progressive sword at that tier
                var progTier = tid2 == TimelineID.WhiteSword ? 2 : (tid2 == TimelineID.MagicalSword || tid2 == TimelineID.StartingMagicalSword) ? 3 : 0;
                timelineItems.Add(new TimelineItem(tid2, () => progTier > 0 && TrackerModel.ProgressiveItems ? Graphics.ow_progressive_tier_item_bmps[progTier] : bmpF()));
            }
            return c;
        }
        Canvas basicBoxImpl(string _tts, TimelineID tid, System.Drawing.Bitmap img, TrackerModel.BoolProperty prop, TrackerModel.IEventingReader<bool> located, TrackerModel.IEventingReader<bool> superseded)
        {
            var c = veryBasicBoxImpl(img, tid, prop, located, superseded);
            return c;
        }
        Canvas basicBoxImplNoTimeline(string _tts, System.Drawing.Bitmap img, TrackerModel.BoolProperty prop, TrackerModel.IEventingReader<bool> located, TrackerModel.IEventingReader<bool> superseded)
        {
            var c = veryBasicBoxImpl(img, null, prop, located, superseded);
            return c;
        }
        // (port helper: TrackerModel.SyntheticEventingBool takes an F# `unit -> bool` and a seq of F# `IEvent<unit>`s)
        TrackerModel.SyntheticEventingBool synthetic(Func<bool> recompute, params IEvent<FSharpHandler<Unit>, Unit>[] changesToWatch) =>
            new TrackerModel.SyntheticEventingBool(FuncConvert.FromFunc(recompute), changesToWatch);
        var FALSE = TrackerModel.FALSE;   // always false for e.g. boxes that never light up Yellow to say their shop is found
        var yellowWoodSwordLogic = synthetic((() => TrackerModel.playerSwordLevel.Value == 0 && TrackerModel.woodSwordCaveFound.Value), TrackerModel.woodSwordCaveFound.Changed, TrackerModel.playerSwordLevel.Changed);
        var woodSwordSuperseded = synthetic((() => TrackerModel.playerSwordLevel.Value >= 1), TrackerModel.playerSwordLevel.Changed);
        var wood_sword_box = basicBoxImpl("Acquired wood sword (mark timeline)", TimelineID.WoodSword, Graphics.ow_sword1_item_bmp, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword, yellowWoodSwordLogic, woodSwordSuperseded);
        wood_sword_box.PointerEntered += (_, _) => showLocator(new ShowLocatorDescriptor.Sword1());
        wood_sword_box.PointerExited += (_, _) => hideLocator();
        // (removed from the grid: wood_sword_box)
        var yellowArrowLogic = synthetic((() => TrackerModel.playerArrowLevel.Value == 0 && TrackerModel.foundArrowShop.Value), TrackerModel.foundArrowShop.Changed, TrackerModel.playerArrowLevel.Changed);
        var woodArrowSuperseded = synthetic((() => TrackerModel.playerArrowLevel.Value >= 2), TrackerModel.playerArrowLevel.Changed);
        var wood_arrow_box = basicBoxImpl("Acquired wood arrow (mark timeline)", TimelineID.WoodArrow, Graphics.wood_arrow_bmp, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodArrow, yellowArrowLogic, woodArrowSuperseded);
        wood_arrow_box.PointerEntered += (_, _) => showShopLocatorInstanceFunc(TrackerModel.MapSquareChoiceDomainHelper.ARROW);
        wood_arrow_box.PointerExited += (_, _) => hideLocator();
        // (wood arrow box removed from the grid)
        var yellowCandleLogic = synthetic((() => TrackerModel.playerCandleLevel.Value == 0 && TrackerModel.foundCandleShop.Value), TrackerModel.foundCandleShop.Changed, TrackerModel.playerCandleLevel.Changed);
        var blueCandleSuperseded = synthetic((() => TrackerModel.playerCandleLevel.Value >= 2), TrackerModel.playerCandleLevel.Changed);
        var blue_candle_box = basicBoxImpl("Acquired blue candle (mark timeline, affects gettables and routing)", TimelineID.BlueCandle, Graphics.blue_candle_bmp, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle, yellowCandleLogic, blueCandleSuperseded);
        blue_candle_box.PointerEntered += (_, _) => { if (TrackerModel.playerComputedStateSummary.CandleLevel == 0) showShopLocatorInstanceFunc(TrackerModel.MapSquareChoiceDomainHelper.BLUE_CANDLE); else showLocatorInstanceFunc(owInstance.Burnable); };
        blue_candle_box.PointerExited += (_, _) => hideLocator();
        // (removed from the grid: blue_candle_box)
        var yellowRingLogic = synthetic((() => TrackerModel.playerRingLevel.Value == 0 && TrackerModel.foundBlueRingShop.Value), TrackerModel.foundBlueRingShop.Changed, TrackerModel.playerRingLevel.Changed);
        var blueRingSuperseded = synthetic((() => TrackerModel.playerRingLevel.Value >= 2), TrackerModel.playerRingLevel.Changed);
        var blue_ring_box = basicBoxImpl("Acquired blue ring (mark timeline)", TimelineID.BlueRing, Graphics.blue_ring_bmp, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueRing, yellowRingLogic, blueRingSuperseded);
        blue_ring_box.PointerEntered += (_, _) => showShopLocatorInstanceFunc(TrackerModel.MapSquareChoiceDomainHelper.BLUE_RING);
        blue_ring_box.PointerExited += (_, _) => hideLocator();
        // (removed from the grid: blue_ring_box)
        var mags_box = basicBoxImpl("Acquired magical sword (mark timeline)\n(10-14 hearts to lift)", TimelineID.MagicalSword, Graphics.ow_sword3_item_bmp, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword, TrackerModel.magsCaveFound,
                                        synthetic((() => TrackerModel.playerHasSword4.Value == 1), TrackerModel.playerHasSword4.Changed));
        var mags_canvas = (Canvas)mags_box.Children[1]; // a tiny bit fragile
        var _magsHintCanvas = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Transparent };  // Background to accept mouse input
        void redrawMagicalSwordCanvas(Canvas c)
        {
            c.Children.Clear();
            if (!(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Value()) &&   // dont have sword
                    TrackerModel.mapStateSummary.Sword3Location.Equals(TrackerModel.NOTFOUND) &&           // not yet located cave
                    !TrackerModel.GetLevelHint.Invoke(10).Equals(TrackerModel.HintZone.UNKNOWN))              // have a hint
                canvasAdd(c, makeHintHighlight(21.0), 4.0, 4.0);
            var bmp = TrackerModel.IsWSMSReplacedByBU.Invoke(null) ? Graphics.ws_ms_bomb_upgrade_bmp : Graphics.ow_sword3_item_bmp;
            canvasAdd(c, Graphics.BMPtoImage(bmp), 4.0, 4.0);
        }
        redrawMagicalSwordCanvas(mags_canvas);
        // (magical sword box removed from the grid)
        onEvent(TrackerModel.IsWSMSReplacedByBUChanged, _ => redrawMagicalSwordCanvas(mags_canvas));
        // (the magical sword box and its hint selector are no longer on the grid)
        mags_box.PointerEntered += (_, _) => showLocator(new ShowLocatorDescriptor.Sword3());
        mags_box.PointerExited += (_, _) => hideLocator();
        // boomstick book, to mark when purchase in boomstick seed (normal book will become shield found in dungeon)
        var boom_book_box = basicBoxImpl("Purchased boomstick book (mark timeline)", TimelineID.BoomstickBook, Graphics.boom_book_bmp, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBoomBook, TrackerModel.foundBookShop, FALSE);
        boom_book_box.PointerEntered += (_, _) => showShopLocatorInstanceFunc(TrackerModel.MapSquareChoiceDomainHelper.BOOK);
        boom_book_box.PointerExited += (_, _) => hideLocator();
        // (removed from the grid: boom_book_box)
        // mark the dungeon wins on timeline via ganon/zelda boxes
        gridAddTuple(owItemGrid, withBlueUnmarked(() => basicBoxImpl("Killed Gannon (mark timeline)", TimelineID.Gannon, Graphics.ganon_bmp, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasDefeatedGanon, FALSE, FALSE)), OW_ITEM_GRID_LOCATIONS.GANON_BOX);
        var zelda_box = withBlueUnmarked(() => basicBoxImpl("Rescued Zelda (mark timeline)", TimelineID.Zelda,  Graphics.zelda_bmp, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda, FALSE, FALSE));
        gridAddTuple(owItemGrid, zelda_box,  OW_ITEM_GRID_LOCATIONS.ZELDA_BOX);
        // hover zelda to display hidden overworld icons (note that Armos/Sword2/Sword3 will not be darkened)
        zelda_box.PointerEntered += (_, _) =>
        {
            for (var i = 0; i <= 15; i++) for (var j = 0; j <= 7; j++) OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks[i, j] = true;
            OptionsMenu.triggerRequestRedrawOverworldEvent();
        };
        zelda_box.PointerExited += (_, _) =>
        {
            for (var i = 0; i <= 15; i++) for (var j = 0; j <= 7; j++) OverworldMapTileCustomization.temporarilyDisplayHiddenOverworldTileMarks[i, j] = false;
            OptionsMenu.triggerRequestRedrawOverworldEvent();
        };
        // shield versus book icon (for boomstick flags/seeds)
        CheckBox make_toggleBookShieldCheckBox()
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var shieldIcon = Graphics.BMPtoImage(Graphics.magic_shield_bmp);
            shieldIcon.Width = 14.0;
            shieldIcon.Height = 14.0;
            sp.Children.Add(shieldIcon);
            var boomBookIcon = Graphics.BMPtoImage(Graphics.boom_book_bmp);
            boomBookIcon.Width = 14.0;
            boomBookIcon.Height = 14.0;
            sp.Children.Add(boomBookIcon);
            return new CheckBox { Content = sp };
        }
        var toggleBookShieldCheckBox = make_toggleBookShieldCheckBox();
        ToolTip.SetTip(toggleBookShieldCheckBox, "Shield instead of book (in item pool, for boomstick seeds)");
        toggleBookShieldCheckBox.IsChecked = false;
        Graphics.onChecked(toggleBookShieldCheckBox, () => TrackerModel.ToggleIsCurrentlyBook.Invoke(null));
        Graphics.onUnchecked(toggleBookShieldCheckBox, () => TrackerModel.ToggleIsCurrentlyBook.Invoke(null));
        // book is atlas
        CheckBox make_bookIsAtlasCheckBox()
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var bookIcon = Graphics.BMPtoImage(Graphics.book_bmp);
            bookIcon.Width = 14.0;
            bookIcon.Height = 14.0;
            sp.Children.Add(bookIcon);
            var mapIcon = Graphics.BMPtoImage(Graphics.zi_map_bmp);
            mapIcon.Width = 18.0;
            mapIcon.Height = 18.0;
            var c = new Canvas { Width = 16.0, Height = 16.0, ClipToBounds = true };  // icon has border, canvas will clip it
            canvasAdd(c, mapIcon, -1.0, -1.0);
            sp.Children.Add(c);
            return new CheckBox { Content = sp };
        }
        var bookIsAtlasCheckBox = make_bookIsAtlasCheckBox();
        ToolTip.SetTip(bookIsAtlasCheckBox, "Book is an Atlas (of all dungeon maps)");
        bookIsAtlasCheckBox.IsChecked = false;
        Graphics.onChecked(bookIsAtlasCheckBox, () => TrackerModel.ToggleBookIsAtlas.Invoke(null));
        Graphics.onUnchecked(bookIsAtlasCheckBox, () => TrackerModel.ToggleBookIsAtlas.Invoke(null));
        onEvent(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Changed, b =>
        {
            if (b)
            {
                notesTextBox.Text = notesTextBox.Text + "\n" + hmsTimeTextBox.Text;
                TrackerModel.LastChangedTime.PauseAll();
                if (TrackerModelOptions.SaveOnCompletion.Value && !(Timeline.isCurrentlyLoadingASave))
                {
                    try
                    {
                        var filename = SaveAndLoad.SaveAll(notesTextBox.Text, DungeonUI.theDungeonTabControl.SelectedIndex, exportDungeonModelsJsonLines(), DungeonSaveAndLoad.SaveDrawingLayer(),
                                            Graphics.alternativeOverworldMapFilename, Graphics.shouldInitiallyHideOverworldMap, currentRecorderDestinationIndex,
                                            toggleBookShieldCheckBox.IsChecked.Value, bookIsAtlasCheckBox.IsChecked.Value, SaveAndLoad.SaveType.FinishedSave);
                        Platform.OfferDownload(filename);   // (port: the browser build also downloads it)
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            else
                TrackerModel.LastChangedTime.ResumeAll();
        });
        // mark whether player currently has bombs, for overworld routing
        var bombIcon = withBlueUnmarked(() => veryBasicBoxImpl(Graphics.bomb_bmp, null, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBombs, TrackerModel.foundBombShop, TrackerModel.FALSE));
        bombIcon.PointerEntered += (_, _) => { showShopLocatorInstanceFunc(TrackerModel.MapSquareChoiceDomainHelper.BOMB); showBombMarkerLocator(); };
        bombIcon.PointerExited += (_, _) => hideLocator();
        ToolTip.SetTip(bombIcon, "Player currently has bombs (affects gettables and routing)");
        gridAddTuple(owItemGrid, bombIcon, OW_ITEM_GRID_LOCATIONS.BOMB_BOX);

        // hover target to look for money - highlight gambling money making game casinos, as well as unknown secrets and un-taken secret spots
        var rupeeIcon = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black };   // red box around the icon, same look as the item boxes
        canvasAdd(rupeeIcon, new Rectangle { Width = 30.0, Height = 30.0, StrokeThickness = 3.0, Stroke = blueUnmarkedBrush }, 0.0, 0.0);
        canvasAdd(rupeeIcon, Graphics.BMPtoImage(Graphics.rupee_bmp), 4.0, 4.0);
        Views.appMainCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(rupeeIcon);   // same turquoise hover box as the bomb and the other boxes
        rupeeIcon.PointerEntered += (_, _) => showLocatorRupees();
        rupeeIcon.PointerExited += (_, _) => hideLocator();
        ToolTip.SetTip(rupeeIcon, "When hovered, highlights locations of Money Making Game,\nUnknown Secret, and not-yet-taken-Secret overworld spots");
        gridAddTuple(owItemGrid, rupeeIcon, OW_ITEM_GRID_LOCATIONS.RUPEE_ICON);

        CheckBox make_highlightOpenCavesCB()
        {
            if (isStandardHyrule)
            {
                var highlightOpenCaves = new TextBox { Text = "Cave highlight", FontSize = 14.0, Background = Brushes.Black, Foreground = Brushes.Orange, BorderThickness = new Thickness(0.0), IsReadOnly = true, IsHitTestVisible = false };
                TrackerModelOptions.Overworld.OpenCaves.Value = true;   // always start with cave highlight turned on
                var cb = new CheckBox { Content = highlightOpenCaves, IsChecked = TrackerModelOptions.Overworld.OpenCaves.Value };
                ToolTip.SetTip(cb, "Highlight unmarked open caves (when hovered)\n" +
                    "When checked, highlights all unmarked open caves until you\neither mark wood sword cave or obtain both a sword and a candle;\n" +
                    "after that, highlights just the armos locations until you mark armos item.");
                ToolTip.SetPlacement(cb, PlacementMode.Top);
                setToolTipShowDuration(cb, 15000);
                cb.PointerEntered += (_, _) => showLocatorInstanceFunc(owInstance.Nothingable);
                cb.PointerExited += (_, _) => hideLocator();
                Graphics.onChecked(cb, () => { TrackerModelOptions.Overworld.OpenCaves.Value = true; TrackerModelOptions.writeSettings(); });
                Graphics.onUnchecked(cb, () => { TrackerModelOptions.Overworld.OpenCaves.Value = false; TrackerModelOptions.writeSettings(); });
                return cb;
            }
            else
                return null;
        }
        var highlightOpenCavesCB = make_highlightOpenCavesCB();

        // these panels need to be created once, at startup time, as they have side effects that populate the timelineItems set
        // 4th sword and overworld hearts checked off on the map show on the timeline
        timelineItems.Add(new TimelineItem(TimelineID.Sword4, () => (TrackerModel.ProgressiveItems ? Graphics.ow_progressive_tier_item_bmps[4] : Graphics.ow_sword4_item_bmp)));
        for (var k = 1; k <= 12; k++)
            timelineItems.Add(new TimelineItem(TimelineID.OWHeart(k), () => Graphics.heart_container_bmp));
        for (var k_ = 1; k_ <= 16; k_++)
        {
            var k = k_;   // (each closure gets its own k, as in F#)
            timelineItems.Add(new TimelineItem(TimelineID.CustomIcon(k), () => (k - 1 < Graphics.customItemBmps.Length ? Graphics.customItemBmps[k - 1] : null)));   // custom icons checked off on the map
        }
        onEvent(TrackerModel.playerHasSword4.Changed, _ =>
        {
            if (TrackerModel.TimelineItemModel.All.TryGetValue("Sword4", out var tim))
                tim.SetCollectedFromMap(TrackerModel.playerHasSword4.Value == 1);
        });
        var weaponsRowPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        weaponsRowPanel.Children.Add(basicBoxImplNoTimeline("Wood sword", Graphics.ow_sword1_item_bmp, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword, FALSE, FALSE));
        weaponsRowPanel.Children.Add(basicBoxImpl("White sword", TimelineID.WhiteSword, Graphics.ow_sword2_item_bmp, TrackerModel.startingItemsAndExtras.PlayerHasWhiteSword, FALSE, FALSE));
        weaponsRowPanel.Children.Add(basicBoxImpl("Magical sword", TimelineID.StartingMagicalSword, Graphics.ow_sword3_item_bmp, TrackerModel.startingItemsAndExtras.PlayerHasMagicalSword, FALSE, FALSE));
        weaponsRowPanel.Children.Add(basicBoxImplNoTimeline("4th sword", Graphics.ow_sword4_item_bmp, TrackerModel.startingItemsAndExtras.PlayerHasSword4, FALSE, FALSE));
        weaponsRowPanel.Children.Add(basicBoxImplNoTimeline("Wood arrow", Graphics.wood_arrow_bmp, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodArrow, FALSE, FALSE));
        weaponsRowPanel.Children.Add(basicBoxImpl("Silver arrow", TimelineID.SilverArrow, Graphics.silver_arrow_bmp, TrackerModel.startingItemsAndExtras.PlayerHasSilverArrow, FALSE, FALSE));
        weaponsRowPanel.Children.Add(basicBoxImpl("Wand", TimelineID.Wand, Graphics.wand_bmp, TrackerModel.startingItemsAndExtras.PlayerHasWand, FALSE, FALSE));
        weaponsRowPanel.Children.Add(basicBoxImplNoTimeline("Blue candle", Graphics.blue_candle_bmp, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle, FALSE, FALSE));
        weaponsRowPanel.Children.Add(basicBoxImpl("Red candle", TimelineID.RedCandle, Graphics.red_candle_bmp, TrackerModel.startingItemsAndExtras.PlayerHasRedCandle, FALSE, FALSE));
        weaponsRowPanel.Children.Add(basicBoxImpl("Boomerang", TimelineID.Boomerang, Graphics.boomerangOrBootsBmp(), TrackerModel.startingItemsAndExtras.PlayerHasBoomerang, FALSE, FALSE));
        weaponsRowPanel.Children.Add(basicBoxImpl("Magic boomerang", TimelineID.MagicBoomerang, Graphics.magicBoomerangOrMagicalBootsBmp(), TrackerModel.startingItemsAndExtras.PlayerHasMagicBoomerang, FALSE, FALSE));
        var importantRowPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        importantRowPanel.Children.Add(basicBoxImpl("Bow", TimelineID.Bow, Graphics.bow_bmp, TrackerModel.startingItemsAndExtras.PlayerHasBow, FALSE, FALSE));
        importantRowPanel.Children.Add(basicBoxImpl("Ladder", TimelineID.Ladder, Graphics.ladder_bmp, TrackerModel.startingItemsAndExtras.PlayerHasLadder, FALSE, FALSE));
        importantRowPanel.Children.Add(basicBoxImpl("Power bracelet", TimelineID.PowerBracelet, Graphics.power_bracelet_bmp, TrackerModel.startingItemsAndExtras.PlayerHasPowerBracelet, FALSE, FALSE));
        importantRowPanel.Children.Add(basicBoxImpl("Raft", TimelineID.Raft, Graphics.raft_bmp, TrackerModel.startingItemsAndExtras.PlayerHasRaft, FALSE, FALSE));
        importantRowPanel.Children.Add(basicBoxImpl("Recorder", TimelineID.Recorder, Graphics.recorder_bmp, TrackerModel.startingItemsAndExtras.PlayerHasRecorder, FALSE, FALSE));
        var utilityRowPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        utilityRowPanel.Children.Add(basicBoxImplNoTimeline("Blue ring", Graphics.blue_ring_bmp, TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueRing, FALSE, FALSE));
        utilityRowPanel.Children.Add(basicBoxImpl("Red ring", TimelineID.RedRing, Graphics.red_ring_bmp, TrackerModel.startingItemsAndExtras.PlayerHasRedRing, FALSE, FALSE));
        utilityRowPanel.Children.Add(basicBoxImpl("Magic key", TimelineID.AnyKey, Graphics.magic_key_bmp, TrackerModel.startingItemsAndExtras.PlayerHasAnyKey, FALSE, FALSE));
        utilityRowPanel.Children.Add(basicBoxImpl("Book", TimelineID.Book, Graphics.book_bmp, TrackerModel.startingItemsAndExtras.PlayerHasBook, FALSE, FALSE));
        utilityRowPanel.Children.Add(basicBoxImplNoTimeline("Meat", Graphics.bait_bmp, TrackerModel.startingItemsAndExtras.PlayerHasMeat, FALSE, FALSE));
        (StackPanel, Action)? extrasPanelAndepRefresh = null;
        (StackPanel, Action) makeExtrasPanelAndepRefresh()
        {
            TextBox mkTxt(double size, string txt) =>
                new TextBox { IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = size, Margin = new Thickness(5.0),
                              VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center,
                              Text = txt, Foreground = Brushes.Orange, Background = Brushes.Black };
            var leftPanel = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black };
            var headerDescription1 = mkTxt(20.0, "Starting Items and Extra Drops");
            var iconedHeader = new StackPanel { Orientation = Orientation.Horizontal };
            iconedHeader.Children.Add(Graphics.BMPtoImage(Graphics.iconExtras_bmp));
            iconedHeader.Children.Add(headerDescription1);
            leftPanel.Children.Add(iconedHeader);
            leftPanel.Children.Add(new DockPanel { Height = 10.0 });
            var triforcePanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            for (var i_ = 0; i_ <= 7; i_++)
            {
                var i = i_;   // (each closure gets its own i, as in F#)
                var innerc = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black };
                void redraw()
                {
                    innerc.Children.Clear();
                    if (TrackerModel.GetTriforceHaves()[i])
                        innerc.Children.Add(Graphics.BMPtoImage(Graphics.fullNumberedFoundTriforce_bmps[i]));
                    else
                        innerc.Children.Add(Graphics.BMPtoImage(Graphics.emptyFoundNumberedTriforce_bmps[i]));
                }
                redraw();
                if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
                {
                    for (var j = 0; j <= 7; j++)
                    {
                        onEvent(TrackerModel.GetDungeon(j).PlayerHasTriforceChanged, _ => redraw());
                        onEvent(TrackerModel.GetDungeon(j).HiddenDungeonColorOrLabelChanged, _ => redraw());
                    }
                }
                else
                    onEvent(TrackerModel.GetDungeon(i).PlayerHasTriforceChanged, _ => redraw());
                innerc.PointerPressed += (_, _) =>
                {
                    if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
                        TrackerModel.startingItemsAndExtras.HDNStartingTriforcePieces[i].Toggle();
                    else
                        TrackerModel.GetDungeon(i).ToggleTriforce();
                    redraw();
                };
                extrasCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(innerc);
                triforcePanel.Children.Add(innerc);
            }
            leftPanel.Children.Add(triforcePanel);
            leftPanel.Children.Add(new DockPanel { Height = 10.0 });
            leftPanel.Children.Add(weaponsRowPanel);
            leftPanel.Children.Add(new DockPanel { Height = 10.0 });
            leftPanel.Children.Add(utilityRowPanel);
            leftPanel.Children.Add(new DockPanel { Height = 10.0 });
            leftPanel.Children.Add(mkTxt(16.0, "Important items"));
            leftPanel.Children.Add(importantRowPanel);
            var b = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(4.0), Background = Brushes.Black, Child = leftPanel };
            b.PointerPressed += (_, ea) => ea.Handled = true;
            var bp = new StackPanel { Orientation = Orientation.Vertical };
            bp.Children.Add(b);


            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(bp);
            void refresh() { }   // max hearts adjustment now lives on right-click of the Max Hearts box on the main screen
            return (panel, refresh);
        }
        var watchForPopups = false;   // (F#: a ref cell)
        cm.AfterCreatePopupCanvas += pc => { if (watchForPopups) extrasCanvasGlobalBoxMouseOverHighlight.AttachToGlobalCanvas(pc); };
        cm.BeforeDismissPopupCanvas += pc => { if (watchForPopups) extrasCanvasGlobalBoxMouseOverHighlight.DetachFromGlobalCanvas(pc); };
        async Task invokeExtras()
        {
            var wh = new ManualResetEvent(false);
            var whole = new Canvas { Width = cm.Width, Height = cm.Height };
            var mouseClickInterceptor = new Canvas { Width = cm.Width, Height = cm.Height, Background = Brushes.Black, Opacity = 0.01 };
            whole.Children.Add(mouseClickInterceptor);
            if (extrasPanelAndepRefresh == null)
                extrasPanelAndepRefresh = makeExtrasPanelAndepRefresh();  // created on-demand, to improve app startup time
            var (extrasPanel, epRefresh) = extrasPanelAndepRefresh.Value;
            epRefresh();
            whole.Children.Add(extrasPanel);
            mouseClickInterceptor.PointerPressed += (_, _) => wh.Set();  // if they click outside the two interior panels that swallow clicks, dismiss it
            watchForPopups = true;
            try
            {
                await CustomComboBoxes.DoModal(cm, wh, 20.0, 155.0, whole);
            }
            finally
            {
                watchForPopups = false;
                whole.Children.Clear(); // to reparent extrasPanel again next popup
            }
        }
        extrasImage.PointerPressed += (_, _) =>
        {
            async Task go()
            {
                if (!popupIsActive)
                {
                    popupIsActive = true;
                    await invokeExtras();
                    popupIsActive = false;
                }
            }
            CustomComboBoxes.StartImmediate(go());
        };

        // timer reset
        var timerResetButton = Graphics.makeButton("Pause/Reset timer", 16.0, Brushes.Orange);
        timerResetButton.Click += (_, _) =>
        {
            if (!popupIsActive)
            {
                popupIsActive = true;
                SaveAndLoad.MaybePollSeedAndFlags();
                var resumeButton = Graphics.makeButton("Timer has been Paused.\nClick here to Resume.\n(Look below for Reset info.)", 16.0, Brushes.Orange);
                var restartTimerButton = Graphics.makeButton("Timer has been Paused.\nClick here to confirm you want to\nReset the TIMER to 0:00:00.", 16.0, Brushes.Orange);
                var resetTrackerButton = Graphics.makeButton("Click here to Reset the TRACKER,\n(remove inventory but preserve maps)\nfor groundhog/routers/4+4 purposes.", 16.0, Brushes.Orange);
                var shutdownAndRestartButton = Graphics.makeButton("Click here to close ZORA Tracker,\nand restart the app. All your\ncurrent work will be discarded!", 16.0, Brushes.Orange);
                var userPressedReset = false;
                var sp = new StackPanel { Orientation = Orientation.Vertical };
                var hsp1 = new StackPanel { Orientation = Orientation.Horizontal };
                hsp1.Children.Add(resumeButton);
                hsp1.Children.Add(new DockPanel { Width = 50.0 });
                hsp1.Children.Add(shutdownAndRestartButton);
                sp.Children.Add(hsp1);
                sp.Children.Add(new DockPanel { Height = 300.0 });
                var hsp2 = new StackPanel { Orientation = Orientation.Horizontal };
                hsp2.Children.Add(restartTimerButton);
                resumeButton.HorizontalAlignment = HorizontalAlignment.Left;
                resumeButton.Width = 370.0;
                restartTimerButton.Width = 370.0;
                hsp2.Children.Add(new DockPanel { Width = 50.0 });
                hsp2.Children.Add(resetTrackerButton);
                sp.Children.Add(hsp2);
                var wh = new ManualResetEvent(false);
                resumeButton.Click += (_, _) =>
                {
                    wh.Set();
                };
                restartTimerButton.Click += (_, _) =>
                {
                    resetTimerEvent.Trigger();
                    userPressedReset = true;
                    // In addition to just resetting the timer, the user clicking 'Reset' should zero out OverworldSpotsRemainingOverTime and Timeline data, for e.g. scenario
                    // where you repeatedly play flags where you start with map knowledge and/or items, so that the graph and timeline display all this at time 0.
                    // Note: the resetTimerEvent is only about the 0:00:00 timer, for example, after loading data off disk, it fires that event, since loading takes several seconds,
                    // so only this user-activated section of code (and not the event) should reset OverworldSpotsRemainingOverTime/Timeline data.
                    TrackerModel.timelineDataOverworldSpotsRemain.Clear();
                    TrackerModel.timelineDataOverworldSpotsRemain.Add(Tuple.Create(0, TrackerModel.mapStateSummary.OwSpotsRemain));
                    foreach (var kv in TrackerModel.TimelineItemModel.All)
                        kv.Value.ResetTotalSeconds();
                    TrackerModel.TimelineItemModel.TriggerTimelineChanged();  // redraw
                    wh.Set();
                };
                restartTimerButton.MyKeyAdd(ea =>
                {
                    if (HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key, out var hotKeyedState))
                    {
                        ea.Handled = true;
                        switch (hotKeyedState)
                        {
                            case HotKeys.GlobalHotkeyTargets.LeftClick: Graphics.Win32.LeftMouseClick(); break;
                            default: break;
                        }
                    }
                });
                resetTrackerButton.Click += (_, _) =>
                {
                    async Task go()
                    {
                        try
                        {
                            // make hard save
                            var filename = makeManualSave();
                            filename = System.IO.Path.GetFileName(filename);  // remove directory info (could have username in path, don't display PII on-screen)
                            var r = await CustomComboBoxes.DoModalMessageBox(cm, null, $"ZORA Tracker data saved to file\n{filename}\nThe tracker will now be reset.", new[] { "Ok" });
                            _ = r;
                            // remove triforces
                            for (var i = 0; i <= 7; i++)
                            {
                                var dung = TrackerModel.GetDungeon(i);
                                if (dung.PlayerHasTriforce())
                                    dung.ToggleTriforce();
                            }
                            // remove (red-ify) all items (keep skipped as marked)
                            foreach (var b in TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes())
                            {
                                if (!b.PlayerHas().Equals(TrackerModel.PlayerHas.SKIPPED))
                                    b.SetPlayerHas(TrackerModel.PlayerHas.NO);
                            }
                            // clear mags/shop items and the take-any heart boxes
                            TrackerModel.playerProgressAndTakeAnyHearts.ResetAll();
                            // secrets reset to bright green, take-anys to bright red heart, letter bright, wood sword bright
                            var toBrighten = new[] {
                                TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY,
                                TrackerModel.MapSquareChoiceDomainHelper.SWORD1,
                                TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET,
                                TrackerModel.MapSquareChoiceDomainHelper.MEDIUM_SECRET,
                                TrackerModel.MapSquareChoiceDomainHelper.SMALL_SECRET,
                                TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER,
                                };
                            for (var i = 0; i <= 15; i++)
                            {
                                for (var j = 0; j <= 7; j++)
                                {
                                    var cur = TrackerModel.overworldMapMarks[i, j].Current();
                                    if (toBrighten.Any(x => x == cur))
                                    {
                                        var bright = cur == TrackerModel.MapSquareChoiceDomainHelper.TAKE_ANY || cur == TrackerModel.MapSquareChoiceDomainHelper.SWORD1 ? 0 : cur;
                                        TrackerModel.setOverworldMapExtraData(i, j, cur, bright);
                                    }
                                }
                            }
                            // not clear blockers (maybe 4+4 and were keyblocked still)
                            // dungeon maps (make darkened floor drops become bright again)
                            DungeonUI.AhhGlobalVariables.resetDungeonsForRouters();
                            // make reminders play again
                            TrackerModel.ResetForGroundhogOrRoutersOrFourPlusFourEtc();
                            // redraw UI
                            TrackerModel.forceUpdate();
                            doUIUpdateEvent.Trigger();
                            wh.Set();
                        }
                        catch (Exception e)
                        {
                            var r = await CustomComboBoxes.DoModalMessageBox(cm, System.Drawing.SystemIcons.Error, $"ZORA Tracker was unable to save the\ntracker state to a file\nError:\n{e.Message}", new[] { "Ok" });
                            _ = r;
                        }
                    }
                    CustomComboBoxes.StartImmediate(go());
                };
                shutdownAndRestartButton.Click += (_, _) =>
                {
                    async Task go()
                    {
                        var restartText = "Discard all my work";
                        var r = await PieMenus.MessageChoiceBoxAsync(cm, "You are about to shutdown and restart the application.", new[] { restartText, "Wait! Take me back!" });
                        if (r == restartText)
                            Graphics.RestartTheApplication();
                    }
                    CustomComboBoxes.StartImmediate(go());
                };
                async Task modal()
                {
                    TrackerModel.LastChangedTime.PauseAll();
                    await CustomComboBoxes.DoModal(cm, wh, 50.0, 200.0, sp);
                    TrackerModel.LastChangedTime.ResumeAll();
                    popupIsActive = false;
                    if (userPressedReset)
                    {
                        if (TrackerModel.startIconX == TrackerModel.NOTFOUND.Item1 && TrackerModel.startIconY == TrackerModel.NOTFOUND.Item2)  // don't re-ask if already placed, e.g. known start, 4+4, groundhog, etc
                            legendStartIconButtonBehavior();  // jump into the 'place the start spot' popup
                    }
                }
                CustomComboBoxes.StartImmediate(modal());
            }
        };
        // spot summary
        var spotSummaryTB = Graphics.makeButton("Spot Summary", 16.0, Brushes.Orange);   // same button style as Hint Decoder
        var spotSummaryCanvas = new Canvas();
        if (isStandardHyrule)   // Spot Summary only makes sense in standard map
        {
            spotSummaryTB.PointerEntered += (_, _) =>
            {
                spotSummaryCanvas.Children.Clear();
                spotSummaryCanvas.Children.Add(OverworldMapTileCustomization.MakeRemainderSummaryDisplay());
            };
            spotSummaryTB.PointerExited += (_, _) =>
            {
                spotSummaryCanvas.Children.Clear();
            };
            // a Button marks left-clicks as handled, so listen with handledEventsToo to keep left and right click behavior
            spotSummaryTB.AddHandler(InputElement.PointerPressedEvent, new EventHandler<PointerPressedEventArgs>((_, ea) =>
            {
                var isRight = ea.GetCurrentPoint(spotSummaryTB).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed;
                Popouts.makePopout(Popouts.PopoutKind.SpotSummaryPopout, !(isRight), isRight, doUIUpdateEvent);
            }), RoutingStrategies.Bubble, true);
        }

        // ws ms bu toggle button
        var midGreen = Graphics.freeze(new SolidColorBrush(Color.FromRgb(0x00, 0xC0, 0x50)));
        Border make_ws_ms_bu_toggleButton()
        {
            var icon = Graphics.BMPtoImage(Graphics.wsmsbuBMP);
            var b = new Border { BorderThickness = new Thickness(1.0), BorderBrush = Brushes.Gray, Child = icon };
            void redraw() => b.BorderBrush = TrackerModel.IsWSMSReplacedByBU.Invoke(null) ? Brushes.Lime : Brushes.Gray;
            b.PointerPressed += (_, _) => { TrackerModel.ToggleWSMSReplacedByBU.Invoke(null); TrackerModel.recomputePlayerStateSummary(); };
            b.PointerEntered += (_, _) => b.BorderBrush = TrackerModel.IsWSMSReplacedByBU.Invoke(null) ? midGreen : Brushes.LightGray;
            b.PointerExited += (_, _) => b.BorderBrush = TrackerModel.IsWSMSReplacedByBU.Invoke(null) ? Brushes.Lime : Brushes.Gray;
            onEvent(TrackerModel.IsWSMSReplacedByBUChanged, _ => redraw());
            ToolTip.SetTip(b, "Replace White Sword and Magical Sword with\nBomb Upgrades (changes their icons)");
            return b;
        }
        var ws_ms_bu_toggleButton = make_ws_ms_bu_toggleButton();

        return (white_sword_canvas, mags_canvas, redrawWhiteSwordCanvas, redrawMagicalSwordCanvas, spotSummaryCanvas, invokeExtras,
                owItemGrid, toggleBookShieldCheckBox, bookIsAtlasCheckBox, highlightOpenCavesCB, timerResetButton, spotSummaryTB, ws_ms_bu_toggleButton);
    }

    public static Action<bool> hideFirstQuestFromMixed = _b => { };
    public static Action<bool> hideSecondQuestFromMixed = _b => { };

    // set by WPFUI: turns the mirrored overworld on/off and refreshes the UI (what the old mirror icon did)
    public static Action<bool> setMirroredOverworld = _ => { };
    public static Action refreshUIAfterOverworldTemplateChange = () => { };   // set by WPFUI to trigger a full UI update
    // (redrawOWCircle is WPFUI's F# ref cell of a function, read when used: a StrongBox, so WPFUI can set its Value later)
    public static (CheckBox, CheckBox, Button) MakeFQSQStuff(CustomComboBoxes.CanvasManager cm, bool _isMixed, Graphics.TileHighlightRectangle[,] owLocatorTilesZone, StrongBox<Action<int, int>> redrawOWCircle)
    {
        bool thereAreMarks(string[] questOnly)
        {
            var r = false;
            for (var x = 0; x <= 15; x++)
            {
                for (var y = 0; y <= 7; y++)
                {
                    if (questOnly[y][x] == 'X' && new MapStateProxy(TrackerModel.overworldMapMarks[x, y].Current()).IsInteresting)
                        r = true;
                }
            }
            return r;
        }
        void highlight(string[] questOnly, bool warn)
        {
            for (var x = 0; x <= 15; x++)
            {
                for (var y = 0; y <= 7; y++)
                {
                    if (questOnly[y][x] == 'X')
                    {
                        if (warn && new MapStateProxy(TrackerModel.overworldMapMarks[x, y].Current()).IsInteresting)
                            owLocatorTilesZone[x, y].MakeRedWithBriefAnimation();
                        else
                            owLocatorTilesZone[x, y].MakeYellowWithBriefAnimation();
                    }
                }
            }
        }
        void showVanilla(bool first)
        {
            for (var x = 0; x <= 15; x++)
            {
                for (var y = 0; y <= 7; y++)
                {
                    var locs = first ? OverworldData.vanilla1QDungeonLocations : OverworldData.vanilla2QDungeonLocations;
                    if (locs.Contains(Tuple.Create(x, y)))
                        owLocatorTilesZone[x, y].MakeGreenWithBriefAnimation();
                }
            }
        }
        void clearOW() { DungeonUI.AhhGlobalVariables.hideLocator(); OverworldRouteDrawing.routeDrawingLayer.Clear(); }

        // in mixed quest, buttons to hide first/second quest
        var hideFirstQuestCheckBox  = new CheckBox { Content = new TextBox { Text = "HFQ", FontSize = 12.0, Background = Brushes.Black, Foreground = Brushes.Orange, BorderThickness = new Thickness(0.0), IsReadOnly = true, IsHitTestVisible = false } };
        ToolTip.SetTip(hideFirstQuestCheckBox, "Hide First Quest\nIn a mixed quest overworld tracker, shade out the first-quest-only spots.\nUseful if you're unsure if randomizer flags are mixed quest or second quest.\nCan't be used if you've marked a first-quest-only spot as having something.");
        setToolTipShowDuration(hideFirstQuestCheckBox, 12000);
        var hideSecondQuestCheckBox = new CheckBox { Content = new TextBox { Text = "HSQ", FontSize = 12.0, Background = Brushes.Black, Foreground = Brushes.Orange, BorderThickness = new Thickness(0.0), IsReadOnly = true, IsHitTestVisible = false } };
        ToolTip.SetTip(hideSecondQuestCheckBox, "Hide Second Quest\nIn a mixed quest overworld tracker, shade out the second-quest-only spots.\nUseful if you're unsure if randomizer flags are mixed quest or first quest.\nCan't be used if you've marked a second-quest-only spot as having something.");
        setToolTipShowDuration(hideSecondQuestCheckBox, 12000);

        hideFirstQuestCheckBox.IsChecked = false;
        Graphics.onChecked(hideFirstQuestCheckBox, () =>
        {
            if (thereAreMarks(OverworldData.owMapSquaresFirstQuestOnly))
                // warn, but let them
                Graphics.ErrorBeepWithReminderLogText("There are first-quest locations already marked, but you chose to hide them anyway");
            hideFirstQuestFromMixed(false);
            hideSecondQuestCheckBox.IsChecked = false;
        });
        Graphics.onUnchecked(hideFirstQuestCheckBox, () => hideFirstQuestFromMixed(true));

        hideSecondQuestCheckBox.IsChecked = false;
        Graphics.onChecked(hideSecondQuestCheckBox, () =>
        {
            if (thereAreMarks(OverworldData.owMapSquaresSecondQuestOnly))
                // warn, but let them
                Graphics.ErrorBeepWithReminderLogText("There are second-quest locations already marked, but you chose to hide them anyway");
            hideSecondQuestFromMixed(false);
            hideFirstQuestCheckBox.IsChecked = false;
        });
        Graphics.onUnchecked(hideSecondQuestCheckBox, () => hideSecondQuestFromMixed(true));

        var M = new Thickness(2.0);
        var moreFQSQoptionsButton = Graphics.makeButton("1Q\n2Q", 10.0, Brushes.Orange);   // two rows, stacked vertically
        moreFQSQoptionsButton.Width = 26.0;
        moreFQSQoptionsButton.Height = 26.0;
        moreFQSQoptionsButton.Padding = new Thickness(0.0);
        TextBox mkTxt(string text) => new TextBox { Text = text, FontSize = 16.0, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), Foreground = Brushes.Orange, Background = Brushes.Black };
        moreFQSQoptionsButton.Click += (_, _) =>
        {
            if (!popupIsActive)
            {
                popupIsActive = true;
                async Task go()
                {
                    var wh = new ManualResetEvent(false);
                    var sp = new StackPanel { Orientation = Orientation.Vertical };
                    sp.Children.Add(mkTxt("Warning! These actions cannot be undone! Don't click unless you are sure!"));
                    var hsp = new StackPanel { Orientation = Orientation.Horizontal };
                    sp.Children.Add(hsp);
                    var left = new StackPanel { Orientation = Orientation.Vertical };
                    var right = new StackPanel { Orientation = Orientation.Vertical };
                    hsp.Children.Add(left);
                    hsp.Children.Add(right);
                    var nowMixed = TrackerModel.owInstance.Quest.AsInt() is var q && (q == 2 || q == 3);   // follows the current template, which can be switched from this menu
                    if (nowMixed)
                    {
                        var erase1ok = !(thereAreMarks(OverworldData.owMapSquaresFirstQuestOnly));
                        var erase2ok = !(thereAreMarks(OverworldData.owMapSquaresSecondQuestOnly));
                        IBrush color1 = !(erase1ok) ? Brushes.Red : Brushes.Yellow;
                        var clearFQbutton = Graphics.makeButton("Mark all First Quest Only spots as Don't Care.\nI am certain this is Second Quest.", 16.0, color1);
                        clearFQbutton.Margin = M;
                        clearFQbutton.PointerEntered += (_, _) => highlight(OverworldData.owMapSquaresFirstQuestOnly, true);
                        clearFQbutton.PointerExited += (_, _) => clearOW();
                        clearFQbutton.Click += (_, _) =>
                        {
                            for (var x = 0; x <= 15; x++)
                            {
                                for (var y = 0; y <= 7; y++)
                                {
                                    if (OverworldData.owMapSquaresFirstQuestOnly[y][x] == 'X')
                                    {
                                        var cell = TrackerModel.overworldMapMarks[x, y];
                                        cell.AttemptToSet(TrackerModel.MapSquareChoiceDomainHelper.DARK_X);
                                    }
                                }
                            }
                            OptionsMenu.triggerRequestRedrawOverworldEvent();
                            wh.Set();
                        };
                        right.Children.Add(clearFQbutton);
                        IBrush color2 = !(erase2ok) ? Brushes.Red : Brushes.Yellow;
                        var clearSQbutton = Graphics.makeButton("Mark all Second Quest Only spots as Don't Care.\nI am certain this is First Quest.", 16.0, color2);
                        clearSQbutton.Margin = M;
                        clearSQbutton.PointerEntered += (_, _) => highlight(OverworldData.owMapSquaresSecondQuestOnly, true);
                        clearSQbutton.PointerExited += (_, _) => clearOW();
                        clearSQbutton.Click += (_, _) =>
                        {
                            for (var x = 0; x <= 15; x++)
                            {
                                for (var y = 0; y <= 7; y++)
                                {
                                    if (OverworldData.owMapSquaresSecondQuestOnly[y][x] == 'X')
                                    {
                                        var cell = TrackerModel.overworldMapMarks[x, y];
                                        cell.AttemptToSet(TrackerModel.MapSquareChoiceDomainHelper.DARK_X);
                                    }
                                }
                            }
                            OptionsMenu.triggerRequestRedrawOverworldEvent();
                            wh.Set();
                        };
                        right.Children.Add(clearSQbutton);
                    }
                    else
                    {
                        var txt = mkTxt("(The buttons that would\notherwise appear in this space\nonly apply to Mixed Quest\noverworlds.)");
                        txt.Foreground = Brushes.DarkGray;
                        txt.Margin = new Thickness(50.0, 10.0, 0.0, 0.0);
                        right.Children.Add(txt);
                    }
                    var mark1QdungeonLocationsButton = Graphics.makeButton("Mark vanilla First Quest\ndungeon locations.", 16.0, Brushes.Orange);
                    mark1QdungeonLocationsButton.Margin = M;
                    mark1QdungeonLocationsButton.PointerEntered += (_, _) => showVanilla(true);
                    mark1QdungeonLocationsButton.PointerExited += (_, _) => clearOW();
                    mark1QdungeonLocationsButton.Click += (_, _) =>
                    {
                        for (var i = 0; i <= 8; i++)
                        {
                            var (x, y) = OverworldData.vanilla1QDungeonLocations[i];
                            TrackerModel.overworldMapCircles[x, y] = 149 + i;
                            redrawOWCircle.Value(x, y);
                        }
                        wh.Set();
                    };
                    left.Children.Add(mark1QdungeonLocationsButton);
                    var mark2QdungeonLocationsButton = Graphics.makeButton("Mark vanilla Second Quest\ndungeon locations.", 16.0, Brushes.Orange);
                    mark2QdungeonLocationsButton.Margin = M;
                    mark2QdungeonLocationsButton.PointerEntered += (_, _) => showVanilla(false);
                    mark2QdungeonLocationsButton.PointerExited += (_, _) => clearOW();
                    mark2QdungeonLocationsButton.Click += (_, _) =>
                    {
                        for (var i = 0; i <= 8; i++)
                        {
                            var (x, y) = OverworldData.vanilla2QDungeonLocations[i];
                            TrackerModel.overworldMapCircles[x, y] = 249 + i;
                            redrawOWCircle.Value(x, y);
                        }
                        wh.Set();
                    };
                    left.Children.Add(mark2QdungeonLocationsButton);
                    // Change OW Template section
                    var changeOWTxt = mkTxt("Change Overworld Template (preserves dungeon data and circles):");
                    changeOWTxt.Margin = new Thickness(0.0, 10.0, 0.0, 2.0);
                    sp.Children.Add(changeOWTxt);
                    var changeOWSP = new StackPanel { Orientation = Orientation.Horizontal };
                    var quests = new[] {
                        (0, "1st Quest OW",    new OverworldData.OverworldInstance(OverworldData.OWQuest.FIRST)),
                        (1, "2nd Quest OW",    new OverworldData.OverworldInstance(OverworldData.OWQuest.SECOND)),
                        (2, "Mixed 1Q OW",     new OverworldData.OverworldInstance(OverworldData.OWQuest.MIXED_FIRST)),
                        (3, "Mixed 2Q OW",     new OverworldData.OverworldInstance(OverworldData.OWQuest.MIXED_SECOND)),
                        };
                    foreach (var (n, label, newInstance) in quests)
                    {
                        var isCurrent = TrackerModel.owInstance.Quest.AsInt() == n;
                        IBrush btnColor = isCurrent ? Brushes.Gray : Brushes.Yellow;
                        var btn = Graphics.makeButton(label, 14.0, btnColor);
                        btn.Margin = new Thickness(2.0);
                        btn.IsEnabled = !isCurrent;
                        ToolTip.SetTip(btn, "Switch to this overworld template.\nOW marks on compatible tiles are preserved; AlwaysEmpty tile marks are cleared.\nNote: tile interactivity and background image reflect startup choice.");
                        btn.Click += (_, _) =>
                        {
                            TrackerModel.reinitializeOverworld(newInstance);
                            OptionsMenu.triggerRequestRedrawOverworldEvent();
                            refreshUIAfterOverworldTemplateChange();   // refresh OW Spots Left / per-type counts for the new quest now
                            wh.Set();
                        };
                        changeOWSP.Children.Add(btn);
                    }
                    sp.Children.Add(changeOWSP);
                    // extra raft / power bracelet blocks: can be switched on and off at any time (saved with the save file)
                    var blocksTxt = mkTxt("Extra blocks and hints (change any time, saved with your save file):");
                    blocksTxt.Margin = new Thickness(0.0, 10.0, 0.0, 2.0);
                    sp.Children.Add(blocksTxt);
                    var blocksRow = new StackPanel { Orientation = Orientation.Horizontal };
                    // rnd: for options that can be Random on the start screen, the Random flag; the box then cycles off / on / Random (a dash, counts as On), so a wrong click can be undone
                    CheckBox mkBlocksCB(string label, string tip, Func<bool> getV, Action<bool> setV, (Func<bool>, Action<bool>)? rnd)
                    {
                        var t = mkTxt(label);
                        t.FontSize = 14.0;
                        var isRnd = rnd.HasValue && rnd.Value.Item1();
                        var cb = new CheckBox { Content = t, IsChecked = (isRnd ? null : getV()), Margin = new Thickness(6.0, 2.0, 14.0, 2.0), IsThreeState = rnd.HasValue };
                        ToolTip.SetTip(cb, label.StartsWith("Boots") ? tip + "\n\nClicks cycle Off, On, Random (?). Random counts as Off (boomerangs) until you confirm it." : rnd.HasValue ? tip + "\n\nClicks cycle Off, On, Random (?). Random counts as On, with yellow outlines on the tiles that might be blocked." : tip);
                        cb.Click += (_, _) =>
                        {
                            if (rnd.HasValue && !cb.IsChecked.HasValue) { setV(true); rnd.Value.Item2(true); }
                            else if (rnd.HasValue) { setV(cb.IsChecked.Value); rnd.Value.Item2(false); }
                            else setV(cb.IsChecked.HasValue && cb.IsChecked.Value);
                            TrackerModel.recomputeMapStateSummary();
                            TrackerModel.forceUpdate();
                            OptionsMenu.triggerRequestRedrawOverworldEvent();
                            refreshUIAfterOverworldTemplateChange();
                        };
                        return cb;
                    }
                    blocksRow.Children.Add(mkBlocksCB("Extra Power Bracelet Blocks", "These overworld tiles are also blocked by the power bracelet:\nA1, A2, A3, A4, B1, B2, B3, B4", (() => OverworldData.ExtraPBBlocks), (v =>
                    {
                        OverworldData.ExtraPBBlocks = v;
                        OverworldData.ExtraPBRandom = false;
                        // on or Random puts the vanilla any roads back (1 in H10, 2 in B14, 3 in C4, 4 in E10; only on empty tiles); off takes away the ones still marked in their vanilla spots
                        foreach (var (state, col, row) in new[] { (TrackerModel.MapSquareChoiceDomainHelper.WARP_1, 10, 7), (TrackerModel.MapSquareChoiceDomainHelper.WARP_2, 14, 1),
                                                                  (TrackerModel.MapSquareChoiceDomainHelper.WARP_3, 4, 2),  (TrackerModel.MapSquareChoiceDomainHelper.WARP_4, 10, 4) })
                        {
                            var cell = TrackerModel.overworldMapMarks[col - 1, row];
                            if (!v)
                            {
                                if (cell.Current() == state) cell.Set(-1);
                            }
                            else if (cell.Current() == -1 && !(TrackerModel.owInstance.AlwaysEmpty(col - 1, row))) cell.Set(state);
                        }
                    }), ((() => OverworldData.ExtraPBRandom), (r => OverworldData.ExtraPBRandom = r))));
                    blocksRow.Children.Add(mkBlocksCB("Extra Raft Blocks", "These overworld tiles are also blocked by the raft:\nD5, E5, A15, A16, B15, B16", (() => OverworldData.ExtraRaftBlocks), (v => { OverworldData.ExtraRaftBlocks = v; OverworldData.ExtraRaftRandom = false; }), ((() => OverworldData.ExtraRaftRandom), (r => OverworldData.ExtraRaftRandom = r))));
                    var hintsRow = new StackPanel { Orientation = Orientation.Horizontal };
                    hintsRow.Children.Add(mkBlocksCB("Randomize Dead Woods", "These overworld tiles are locked by the Dead Woods hint:\nH1, H2, H3\n(hovering one highlights the Dead Woods button)", (() => OverworldData.DeadWoodsEnabled), (v => { OverworldData.DeadWoodsEnabled = v; OverworldData.DeadWoodsRandom = false; }), ((() => OverworldData.DeadWoodsRandom), (r => OverworldData.DeadWoodsRandom = r))));
                    hintsRow.Children.Add(mkBlocksCB("Randomize Lost Hills", "These overworld tiles are locked by the Lost Hills hint:\nA12, A13, A14\n(hovering one highlights the Lost Hills button)", (() => OverworldData.LostHillsEnabled), (v => { OverworldData.LostHillsEnabled = v; OverworldData.LostHillsRandom = false; }), ((() => OverworldData.LostHillsRandom), (r => OverworldData.LostHillsRandom = r))));
                    var hideQuestPanel = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(10.0, 0.0, 10.0, 0.0) };
                    DockPanel.SetDock(hideQuestPanel, Dock.Right);
                    if (nowMixed)
                    {
                        ((TextBox)hideFirstQuestCheckBox.Content).Text = "Hide 1st Quest";
                        ((TextBox)hideSecondQuestCheckBox.Content).Text = "Hide 2nd Quest";
                        ((TextBox)hideFirstQuestCheckBox.Content).FontSize = 14.0;
                        ((TextBox)hideSecondQuestCheckBox.Content).FontSize = 14.0;
                        hideFirstQuestCheckBox.Margin = new Thickness(0.0, 2.0, 0.0, 2.0);
                        hideSecondQuestCheckBox.Margin = new Thickness(0.0, 2.0, 0.0, 2.0);
                        hideQuestPanel.Children.Add(hideFirstQuestCheckBox);
                        hideQuestPanel.Children.Add(hideSecondQuestCheckBox);
                    }
                    var blocksCol = new StackPanel { Orientation = Orientation.Vertical };
                    blocksCol.Children.Add(blocksRow);
                    blocksCol.Children.Add(hintsRow);
                    // boots: on or off any time too (for when the flags left Boots on Random). The boomerang pickers use it at once; the boomerang icons already drawn in the item boxes update the next time the tracker starts
                    var bootsRow = new StackPanel { Orientation = Orientation.Horizontal };
                    bootsRow.Children.Add(mkBlocksCB("Boots Replace Boomerangs", "The wooden and magical boomerangs are boots.\nThe boomerang picks and letters use this at once; the icons already drawn in the item boxes update the next time the tracker starts.", (() => TrackerModel.BootsReplaceBoomerangs), (v => { TrackerModel.BootsReplaceBoomerangs = v; TrackerModel.BootsRandom = false; }), ((() => TrackerModel.BootsRandom), (r => { TrackerModel.BootsRandom = r; if (r) TrackerModel.BootsReplaceBoomerangs = false; }))));   // (Random, the ?: the boomerangs stay until boots are confirmed)
                    bootsRow.Children.Add(mkBlocksCB("Mirrored Overworld", "Flip the overworld map East<->West (same as the mirror icon at the top left).", (() => TrackerModel.MirrorOverworld), (v => setMirroredOverworld(v)), null));
                    blocksCol.Children.Add(bootsRow);
                    // the Hide 1st / 2nd Quest checkboxes (mixed overworlds only) sit in a third column, level with the four checkboxes
                    var blocksArea = new DockPanel { LastChildFill = true };
                    blocksArea.Children.Add(hideQuestPanel);
                    blocksArea.Children.Add(blocksCol);
                    sp.Children.Add(blocksArea);
                    // Shop prices popout
                    var shopPricesTxt = mkTxt("Shop price notes:");
                    shopPricesTxt.Margin = new Thickness(0.0, 10.0, 0.0, 2.0);
                    var shopRow = new DockPanel { LastChildFill = true };
                    var shopLeft = new StackPanel { Orientation = Orientation.Vertical };
                    shopRow.Children.Add(shopLeft);
                    sp.Children.Add(shopRow);
                    var closeBtn = Graphics.makeButton("Close", 16.0, Brushes.Orange);
                    closeBtn.Margin = new Thickness(0.0, 10.0, 8.0, 8.0);
                    closeBtn.HorizontalAlignment = HorizontalAlignment.Right;
                    closeBtn.Click += (_, _) => CustomComboBoxes.DismissTopModal();
                    sp.Children.Add(closeBtn);
                    var b = new Border { Child = sp, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(5.0), Background = Brushes.Black };
                    clearOW();
                    await CustomComboBoxes.DoModalCore(cm, wh, ((c, e) => canvasAdd(c, e, 10.0, START_DUNGEON_AND_NOTES_AREA_H)), ((c, e) => c.Children.Remove(e)), b, 0.25);
                    hideQuestPanel.Children.Clear();   // release the checkboxes so the next popup can reuse them
                    popupIsActive = false;
                }
                CustomComboBoxes.StartImmediate(go());
            }
        };

        return (hideFirstQuestCheckBox, hideSecondQuestCheckBox, moreFQSQoptionsButton);
    }
}
