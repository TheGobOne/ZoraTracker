// Ported from Z1R_WPF/Layout.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// The F# `open OverworldItemGridUI` is a `using static`, so `canvasAdd`, `OMTW` and the layout constants read as in WPF; `open OverworldMapTileCustomization`
// is a `using` alias for OW_ITEM_GRID_LOCATIONS (as in UIComponents.cs). WPF's UIElement/FrameworkElement are Avalonia's Control (so the F#
// `:? FrameworkElement as f` matches are null checks), ActualWidth/ActualHeight are Bounds.Width/Height, and `Visibility.Hidden` is Opacity 0 +
// IsHitTestVisible false. WPF's ScaleTransform scales from the top-left corner; Avalonia's RenderTransformOrigin defaults to the centre, so
// every scaled element sets it to TopLeft.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using static Z1R_Avalonia.OverworldItemGridUI;            // has canvasAdd, etc
using OW_ITEM_GRID_LOCATIONS = Z1R_Avalonia.OverworldMapTileCustomization.OW_ITEM_GRID_LOCATIONS;   // (F#: `open OverworldMapTileCustomization  // has a lot of constants`; only this is used from it)

namespace Z1R_Avalonia;

public static class Layout
{
    public static readonly double RIGHT_COL = 410.0;
    public static readonly double WEBCAM_LINE = OMTW * 16.0 - 200.0;  // height of upper area is 150, so 200 wide is 4x3 box in upper right; timer and other controls here could be obscured
    public static readonly double kittyWidth = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H - THRU_MAIN_MAP_H;

    // a small underlined 'Discord' text that opens the given url in the default browser when clicked
    public static readonly double DiscordLinkWidth = 70.0;
    public static readonly double RacingLinkWidth = 112.0;
    public static Control MakeLinkText(string text, string url, double width)
    {
        var tb = new TextBlock { Text = text, FontSize = 10.0, Foreground = Brushes.Orange, TextDecorations = TextDecorations.Underline, TextAlignment = TextAlignment.Center,
                                 Width = width, Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand) };
        ToolTip.SetTip(tb, url);
        tb.PointerEntered += (_, _) => tb.Foreground = Brushes.Yellow;   // (WPF: MouseEnter)
        tb.PointerExited += (_, _) => tb.Foreground = Brushes.Orange;    // (WPF: MouseLeave)
        tb.PointerReleased += (_, ea) =>   // (WPF: MouseLeftButtonUp)
        {
            if (ea.InitialPressMouseButton == MouseButton.Left)
            {
                // (WPF: Process.Start(ProcessStartInfo(url, UseShellExecute=true)), errors ignored; Platform.OpenWithShell also catches them)
                Platform.OpenWithShell(url);
            }
        };
        return tb;
    }
    public static Control MakeDiscordLink(string url) => MakeLinkText("ZORA Discord", url, DiscordLinkWidth);

    // keep the 'Item Progress' label horizontally centered between the version button (left) and the first progress icon (right); needs real sizes, so done on SizeChanged
    public static void centerItemProgressLabel(Control ip, Control vb)
    {
        if (ip is Control ipf && vb is Control vbf)
        {
            void recenter()
            {
                if (ipf.Bounds.Width > 0.0 && vbf.Bounds.Width > 0.0)
                    Canvas.SetLeft(ipf, (vbf.Bounds.Width + ITEM_PROGRESS_FIRST_ITEM - ipf.Bounds.Width) / 2.0);
            }
            ipf.SizeChanged += (_, _) => recenter();
            vbf.SizeChanged += (_, _) => recenter();
        }
    }

    // center an element horizontally on x=cx (needs its real width, so done on SizeChanged); null-safe
    public static void centerHorizontallyAt(Control e, double cx)
    {
        if (e is Control f)
        {
            void recenter() { if (f.Bounds.Width > 0.0) Canvas.SetLeft(f, cx - f.Bounds.Width / 2.0); }
            f.SizeChanged += (_, _) => recenter();
        }
    }
    // center the Hotkeys button horizontally under the Hint Decoder button (needs both real widths, so done on SizeChanged)
    public static Control hintDecoderElementForCentering = null;
    public static Control hotkeysElementForCentering = null;
    public static bool hotkeysCenteringHooked = false;
    public static void tryCenterHotkeysUnderHintDecoder(double hintLeft)
    {
        if (!hotkeysCenteringHooked && hintDecoderElementForCentering != null && hotkeysElementForCentering != null)
        {
            if (hintDecoderElementForCentering is Control h && hotkeysElementForCentering is Control t)
            {
                hotkeysCenteringHooked = true;
                void recenter() { if (h.Bounds.Width > 0.0 && t.Bounds.Width > 0.0) Canvas.SetLeft(t, hintLeft + (h.Bounds.Width - t.Bounds.Width) / 2.0); }
                h.SizeChanged += (_, _) => recenter();
                t.SizeChanged += (_, _) => recenter();
            }
        }
    }
    // the four small icons at the top left (? / eyeball / save / mirror) are all made the same size as the mirror icon, in a 2x2 block;
    // the zones/coords checkboxes below are pushed down if the block needs more room
    public static Control iconQuestion = null;
    public static Control iconEye = null;
    public static Control iconSave = null;
    public static Control iconOptions = null;   // the Options / Draw / log buttons, stacked just above the Item Locations button
    public static Control iconDraw = null;
    public static Control iconLog = null;
    public static Control iconItemLocations = null;   // the Item Locations button, the top button of the Save / Hint Decoder / Hotkeys column
    public static Control iconMirror = null;
    public static Control zonesCheckBoxEl = null;
    public static Control coordsCheckBoxEl = null;
    public static (double, double, double, double)? zonesCoordsNatural = null;   // unscaled sizes of the zones / coords checkboxes
    public static bool topLeftIconsHooked = false;
    public static readonly double SMALL_BUTTON_W = 84.0;   // width of the Save / Hint Decoder / Hotkeys column (it has to fit between the OW stats and the Pause/Reset timer column)
    public static Control rightButtonForSizing = null;
    public static readonly double PANEL_X0 = WEBCAM_LINE - 44.0;   // (3px left of the old column so the panel isn't against the window edge)
    // // left column of the control panel (TRACK / TOOLS)
    public static readonly double PANEL_LW = 114.0;
    public static readonly double PANEL_X1 = PANEL_X0 + PANEL_LW + 13.0;   // right column (TIMER / FILE)
    public static readonly double PANEL_RW = PANEL_LW;
    // (port helper: WPF parses these two templates from XAML strings: a Border 'bd' (fill #171717, border #3A3A3A, 1px, rounded corners, SnapsToDevicePixels)
    // around a stretched ContentPresenter, with an IsMouseOver trigger setting the border to #FFA31A and the fill to #241A0A. Here a code template that reacts
    // to IsPointerOver, as Graphics.roundedButtonTemplate.)
    private static FuncControlTemplate<Button> make_buttonTemplate(double cornerRadius) =>
        new FuncControlTemplate<Button>((b, _scope) =>
        {
            // (not named PART_ContentPresenter: the Fluent theme restyles a presenter with that name on hover/press)
            var cp = new ContentPresenter { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            Graphics.adoptContent(b, cp);   // (the panel buttons get this template after they have been shown; see Graphics.adoptContent)
            var bd = new Border { Name = "bd", BorderThickness = new Thickness(1.0), CornerRadius = new CornerRadius(cornerRadius), UseLayoutRounding = true, Child = cp };
            void refresh()
            {
                if (!Graphics.isCurrentPresenter(b, cp)) return;   // (this template has been replaced)
                cp.Content = b.Content;
                cp.ContentTemplate = b.ContentTemplate;
                bd.Background = b.IsPointerOver ? templateHoverFill : templateFill;
                bd.BorderBrush = b.IsPointerOver ? templateHoverBorder : templateBorder;
            }
            refresh();
            b.PropertyChanged += (_, ea) =>
            {
                var p = ea.Property;
                if (p == ContentControl.ContentProperty || p == ContentControl.ContentTemplateProperty || p == InputElement.IsPointerOverProperty)
                    refresh();
            };
            return bd;
        });
    private static readonly SolidColorBrush templateFill = new SolidColorBrush(Color.Parse("#171717"));
    private static readonly SolidColorBrush templateBorder = new SolidColorBrush(Color.Parse("#3A3A3A"));
    private static readonly SolidColorBrush templateHoverBorder = new SolidColorBrush(Color.Parse("#FFA31A"));
    private static readonly SolidColorBrush templateHoverFill = new SolidColorBrush(Color.Parse("#241A0A"));
    // the panel buttons' look (from the approved mockup): dark fill, thin gray border, 4px rounded corners, orange border on hover
    public static readonly FuncControlTemplate<Button> panelButtonTemplate = make_buttonTemplate(4.0);
    // startup screen: the same button look as the control panel (rounded 8px), and option boxes (checkbox areas) with a matching rounded border + orange hover
    public static readonly FuncControlTemplate<Button> startupButtonTemplate = make_buttonTemplate(8.0);
    public static void styleStartupButton(Control e)
    {
        switch (e)
        {
            case Button b:
                b.Template = startupButtonTemplate;
                switch (b.Content)
                {
                    case TextBox t: t.Background = Brushes.Transparent; break;
                    default: break;
                }
                break;
            default:
                break;
        }
    }
    // (WPF: a XAML Style for Border (border #3A3A3A, fill #171717, corner radius 8; on IsMouseOver border #FFA31A, fill #241A0A), used as
    // `new Border(Style=Layout.optionBorderStyle, ...)`. Avalonia has no Style property, and a style can't be shared between controls, so this
    // gives the border the class "optionBorder" and its own copy of the styles, and returns it: `Layout.optionBorderStyle(new Border { ... })`.
    // The selector matches the class, so Borders inside the area (e.g. a checkbox's box) aren't restyled.)
    public static Border optionBorderStyle(Border b)
    {
        var normal = new Style(x => x.OfType<Border>().Class("optionBorder"));
        normal.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.Parse("#3A3A3A"))));
        normal.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.Parse("#171717"))));
        normal.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(8.0)));
        var hover = new Style(x => x.OfType<Border>().Class("optionBorder").Class(":pointerover"));
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.Parse("#FFA31A"))));
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.Parse("#241A0A"))));
        b.Classes.Add("optionBorder");
        b.Styles.Add(normal);
        b.Styles.Add(hover);
        return b;
    }
    public static Control iconSpot = null;
    public static Control iconShowRun = null;
    public static Control iconFQSQ = null;
    public static TextBlock panelHeading(string text) =>
        new TextBlock { Text = text, FontSize = 8.0, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(143, 143, 143)), TextAlignment = TextAlignment.Center, IsHitTestVisible = false };
    public static readonly List<(TextBlock, double, double, double)> panelHeadings = new List<(TextBlock, double, double, double)> {
        (panelHeading("TRACK"), PANEL_X0, 0.0, PANEL_LW), (panelHeading("TOOLS"), PANEL_X0, 32.0, PANEL_LW), (panelHeading("TIMER"), PANEL_X1, 0.0, PANEL_RW), (panelHeading("OVERWORLD OPTIONS"), PANEL_X0, 118.0, PANEL_LW), (panelHeading("FILE"), PANEL_X1, 86.0, PANEL_RW) };
    public static readonly Rectangle panelDivider = new Rectangle { Width = 1.0, Height = 148.0, Fill = new SolidColorBrush(Color.FromRgb(38, 38, 38)), IsHitTestVisible = false };
    public static void addPanelDecor(Action<Control> add)
    {
        foreach (var (tb, _, _, _) in panelHeadings) add(tb);
        add(panelDivider);
    }
    public static Action topLeftPlace = () => { };
    public static void hookTopLeftOnSizeChange(Control e)
    {
        if (e is Control f)
            f.SizeChanged += (_, _) => topLeftPlace();
    }
    // (port helper: WPF's Visibility.Hidden: invisible but still laid out)
    private static void hide(Control e)
    {
        e.Opacity = 0.0;
        e.IsHitTestVisible = false;
    }
    public static void tryLayoutTopLeftIcons()
    {
        if (!topLeftIconsHooked && iconQuestion != null && iconEye != null && iconSave != null && iconMirror != null)
        {
            if (iconMirror is Control m)
            {
                topLeftIconsHooked = true;
                void place()
                {
                    if (m.Bounds.Width > 0.0 && m.Bounds.Height > 0.0)
                    {
                        var (w, h) = (m.Bounds.Width, m.Bounds.Height);
                        foreach (var e in new[] { iconQuestion, iconEye })
                        {
                            if (e is Control f)
                            {
                                f.Width = w;
                                f.Height = h;
                            }
                        }
                        // everything above the Hint Decoder button is centered horizontally on that button
                        var hintLeft = WEBCAM_LINE - 41.0;
                        var hintCx = hintLeft + SMALL_BUTTON_W / 2.0;
                        var blockLeft = hintCx - (2.0 * w + 2.0) / 2.0;
                        Canvas.SetLeft(iconQuestion, blockLeft);
                        Canvas.SetTop(iconQuestion, 0.0);
                        Canvas.SetLeft(iconEye, blockLeft + w + 2.0);
                        Canvas.SetTop(iconEye, 0.0);
                        // the ? / eyeball / mirror icons are hidden (still laid out, so the measurements above keep working); only the Save button is shown
                        foreach (var e in new[] { iconQuestion, iconEye, iconMirror }) hide(e);   // (WPF: e.Visibility <- Visibility.Hidden)
                        Canvas.SetLeft(iconMirror, blockLeft + w + 2.0);
                        Canvas.SetTop(iconMirror, h + 2.0);
                        // zones and coords sit at the very top of the screen; Save goes right above the Hint Decoder button, and Hotkeys/Save/Hint Decoder are all the same size
                        if (zonesCheckBoxEl is Control z && coordsCheckBoxEl is Control cc && z.Bounds.Height > 0.0 && cc.Bounds.Height > 0.0)
                        {
                            // zones and coords sit side by side in the open strip above the item grid (centered over the armos / letter / ladder item boxes, the first three grid columns) and scaled down to fit
                            var (areaX, areaW, areaY, areaH) = (OW_ITEM_GRID_LOCATIONS.OFFSET + 2.0, 86.0, 3.0, 24.0);
                            // (WPF scales these with a LayoutTransform. Avalonia has no LayoutTransform property; inside a Canvas a RenderTransform from the
                            // top-left corner looks the same, and, like WPF's ActualWidth under a LayoutTransform, Bounds stays the unscaled size.)
                            if (!zonesCoordsNatural.HasValue && !(z.RenderTransform is ScaleTransform))
                                zonesCoordsNatural = (z.Bounds.Width, z.Bounds.Height, cc.Bounds.Width, cc.Bounds.Height);
                            if (zonesCoordsNatural is { } zn)
                            {
                                var (zw, zh, cw, ch) = zn;
                                var gap = 4.0;
                                var s = Math.Min(1.0, Math.Min((areaW - gap) / (zw + cw), areaH / Math.Max(zh, ch)));
                                var want = new ScaleTransform(s, s);
                                var cur = (z.RenderTransform is ScaleTransform t ? t.ScaleX : 1.0);
                                if (Math.Abs(cur - s) > 0.001)
                                {
                                    z.RenderTransformOrigin = RelativePoint.TopLeft;
                                    cc.RenderTransformOrigin = RelativePoint.TopLeft;
                                    z.RenderTransform = want;
                                    cc.RenderTransform = new ScaleTransform(s, s);
                                }
                                var totalW = (zw + cw) * s + gap;
                                var left = areaX + (areaW - totalW) / 2.0;
                                Canvas.SetLeft(z, left);
                                Canvas.SetTop(z, areaY + (areaH - zh * s) / 2.0);
                                Canvas.SetLeft(cc, left + zw * s + gap);
                                Canvas.SetTop(cc, areaY + (areaH - ch * s) / 2.0);
                                z.ZIndex = 100;
                                cc.ZIndex = 100;
                            }
                        }
                        // two-column control panel: left TRACK / TOOLS, right TIMER (+ Pause/Reset level with Spot Summary) / FILE; every button the same size
                        var items = new List<(Control, double, double)> {
                            (iconItemLocations, PANEL_X0, 10.0),   // (the 'Popout Windows' button; Spot Summary and Show Custom now live in its menu)
                            (hintDecoderElementForCentering, PANEL_X0, 42.0),
                            (hotkeysElementForCentering, PANEL_X0, 60.0),
                            (iconLog, PANEL_X0, 78.0),
                            (iconDraw, PANEL_X0, 96.0),
                            (rightButtonForSizing, PANEL_X1, 60.0),
                            (iconFQSQ, PANEL_X0, 128.0),
                            (iconOptions, PANEL_X1, 96.0),
                            (iconSave, PANEL_X1, 114.0) };
                        foreach (var e in new[] { iconSpot, iconShowRun }) { if (e != null) e.IsVisible = false; }   // (WPF: Visibility.Collapsed)
                        foreach (var (e, x, y) in items)
                        {
                            if (e is Control f)
                            {
                                switch (f)
                                {
                                    case Button b:
                                        switch (b.Content)
                                        {
                                            case TextBox t:
                                                t.VerticalContentAlignment = VerticalAlignment.Center; t.VerticalAlignment = VerticalAlignment.Stretch; t.HorizontalAlignment = HorizontalAlignment.Stretch;
                                                t.FontSize = 11.0; t.Padding = new Thickness(0.0); t.RenderTransform = null; t.Background = Brushes.Transparent;
                                                break;
                                            default: break;
                                        }
                                        b.Template = panelButtonTemplate;
                                        break;
                                    default: break;
                                }
                                // (WPF: f.SnapsToDevicePixels <- true; Avalonia has no SnapsToDevicePixels, layout rounding does the same job)
                                f.UseLayoutRounding = true;
                                if (f.Width != PANEL_LW) f.Width = PANEL_LW;
                                if (f.Height != 16.0) f.Height = 16.0;
                                Canvas.SetLeft(f, x);
                                Canvas.SetTop(f, y);
                            }
                        }
                        foreach (var (tb, x, y, w2) in panelHeadings)
                        {
                            Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y); tb.Width = w2;
                        }
                        Canvas.SetLeft(panelDivider, PANEL_X0 + PANEL_LW + 6.0); Canvas.SetTop(panelDivider, 0.0);
                    }
                }
                m.SizeChanged += (_, _) => place();
                topLeftPlace = place;
                place();
            }
        }
    }
    // center an element vertically on y=cy (needs its real height, so done on SizeChanged); null-safe
    public static void centerVerticallyAt(Control e, double cy)
    {
        if (e is Control f)
        {
            void recenter() { if (f.Bounds.Height > 0.0) Canvas.SetTop(f, cy - f.Bounds.Height / 2.0); }
            f.SizeChanged += (_, _) => recenter();
        }
    }
    // the item progress icons are drawn 3px into their 30px-high canvas, so the middle of the progress row is 15px below the canvas top
    public static readonly double ITEM_PROGRESS_ROW_CENTER_Y = THRU_MAP_AND_LEGEND_H + 3.0 + 15.0;
    // the 'Item Progress' button is centered vertically on the sword icon (the first icon in the row, drawn 3px into the canvas)
    public static readonly double SWORD_ICON_CENTER_Y = THRU_MAP_AND_LEGEND_H + 3.0 + 3.0 + (double)(Graphics.swordLevelToBmp(0).Height) / 2.0;
    // x center of the span between the inner left edge of the blue ring's red box and the inner right edge of the money hover icon's box (grid columns 4..7)
    public static readonly double OW_STATS_CENTER_X = OW_ITEM_GRID_LOCATIONS.OFFSET + 180.0;
    // the 'OW spots left / gettable / cave highlight' block sits in the open space just right of the item grid (top, above the Zones/Coords checkboxes)
    // the 'OW spots left' text (and its per-type breakdown) sits a little further right, clear of the fifth item box / hint icon above the grid's top row
    public static readonly double OW_SPOTS_LEFT_CENTER_X = OW_ITEM_GRID_LOCATIONS.OFFSET + 197.0;
    public static readonly double OW_STATS_BLOCK_CENTER_X = OW_ITEM_GRID_LOCATIONS.OFFSET + 294.0;

    // (WPF: UIElement / FrameworkElement parameters are Controls; Image, Canvas, Button, Point and Rect are Avalonia's)
    public interface IApplicationLayoutBase
    {
        void AddMainTracker(Control mainTracker);
        void AddNumberedTriforceCanvas(Canvas triforceCanvas, int i);
        void AddItemGridStuff(Control owItemGrid, Control toggleBookShieldCheckBox, Control bookIsAtlasCheckBox, Control highlightOpenCavesCheckBox,
                              Control timerResetButton, Control spotSummaryTB, Control mirrorOW, Control hideIconsEyeball, Button moreFQSQoptionsButton, Control ws_ms_bu_toggleButton);
        void AddHideQuestCheckboxes(Control hideFirstQuestCheckBox, Control hideSecondQuestCheckBox);
        void AddLinkRouting(Control linkIcon, Control currentTargetIcon);
        void AddWebcamLine();
        void AddOverworldCanvas(Control overworldCanvas);
        void AddLegend(Control legendCanvas);
        void AddItemProgress(Control itemProgressCanvas, Control itemProgressTB);
        void AddVersionButton(Control vb);
        void AddHintDecoderButton(Control tb);
        void AddOverworldOptionsButton(Control fqsqButton);
        void AddExtraSiteLinks(Control zoraSiteLink, Control racingSiteLink);
        void AddKittyAndLogo(Image kitty, Control logoBorder, Image ztlogo, Control discordLink, Control racingLink);
        void AddShowHotKeysButton(Control showHotKeysButton);
        void AddShowRunCustomButton(Control showRunCustomButton);
        void AddSaveButton(Control saveButton);
        void AddItemLocationsButton(Control itemLocationsButton);
        void AddTopLeftButtons(Control optionsButton, Control drawButton, Control logButton);
        void AddQuestReferenceChart(Control chart, double x, double y);
        void AddUserCustomContentButton(Control uccButton);
        void AddDungeonTabsOverlay(Control dungeonTabsOverlay);
        void AddDungeonTabs(Control dungeonTabsWholeCanvas);
        double GetDungeonY();
        void AddBlockers(Control blockerGrid);
        void AddNotesSeedsFlags(Control notesTextBox, Control seedAndFlagsDisplayCanvas);
        void AddExtraDungeonRightwardStuff(Control grabModeTextBlock, Control rightwardCanvas);
        void AddOWRemainingScreens(Control owRemainingScreensTextBox);
        void AddOWGettableScreens(Control owGettableScreensCheckBox);
        void AddCurrentMaxHearts(Control currentMaxHeartsTextBox);
        void AddShowCoords(Control showCoordsCB, Image havePotionLetterImage);
        void AddOWZoneOverlay(Control zone_checkbox);
        void AddMouseHoverExplainer(Control mouseHoverExplainerIcon, Control c);
        void AddLinkTarget(Control currentTargetGhostBuster);
        void AddTimeline(Control t1c, Control t2c, Control t3c);
        Rect GetTimelineBounds();
        Rect GetFullAppBounds();
        void AddReminderDisplayOverlay(Control reminderDisplayOuterDockPanel);
        void AddPostGameDecorationCanvas(Control postgameDecorationCanvas);
        void AddSpotSummary(Control spotSummaryCanvas);
        void AddDiskIcon(Control diskIcon);
        void AllDone();
        void AddTopLayerHover(Control fe, double x, double y);
        void ClearTopLayerHovers();
        void FocusOverworld();
        void FocusDungeon();
        void ShowMouseWarp(Point newPos);
        bool IsShort { get; }
    }

    public class ApplicationLayout : IApplicationLayoutBase
    {
        private readonly CustomComboBoxes.CanvasManager cm;
        private readonly Canvas appMainCanvas;
        private Control itemProgressLabelForCentering = null;
        private readonly Canvas topLayerHoverCanvas = new Canvas();   // for temporary bits that appear on hover, to ensure they appear on top of everything else in the app, when no other popup active
        private readonly SynchronizationContext ctxt;  // this class is created on the UI thread
        // (port: WPF's ShowMouseWarp reads Input.Mouse.GetPosition(cm.AppMainCanvas). Avalonia has no global mouse position, so the last pointer position
        // seen over the root canvas (which sees moves in popups too) is kept, in AppMainCanvas coordinates.)
        private Point lastMousePos = new Point();
        public ApplicationLayout(CustomComboBoxes.CanvasManager cm)
        {
            this.cm = cm;
            this.appMainCanvas = cm.AppMainCanvas;
            this.ctxt = SynchronizationContext.Current;
            cm.RootCanvas.PointerMoved += (_, ea) => lastMousePos = ea.GetPosition(cm.AppMainCanvas);
        }
        void IApplicationLayoutBase.AddMainTracker(Control mainTracker)
        {
            canvasAdd(appMainCanvas, mainTracker, 0.0, 0.0);
        }
        void IApplicationLayoutBase.AddNumberedTriforceCanvas(Canvas triforceCanvas, int i)
        {
            canvasAdd(appMainCanvas, triforceCanvas, OW_ITEM_GRID_LOCATIONS.OFFSET + 30.0 * (double)i, 0.0);
        }
        void IApplicationLayoutBase.AddItemGridStuff(Control owItemGrid, Control _toggleBookShieldCheckBox, Control _bookIsAtlasCheckBox, Control _highlightOpenCavesCheckBox, Control timerResetButton, Control spotSummaryTB, Control mirrorOW,
                                                     Control hideIconsEyeball, Button _moreFQSQoptionsButton, Control _ws_ms_bu_toggleButton)
        {
            canvasAdd(appMainCanvas, owItemGrid, OW_ITEM_GRID_LOCATIONS.OFFSET, 30.0);
            rightButtonForSizing = timerResetButton;
            hookTopLeftOnSizeChange(timerResetButton);
            canvasAdd(appMainCanvas, timerResetButton, 12.8 * OMTW + 10.0, 60.0);
            canvasAdd(appMainCanvas, spotSummaryTB, 12.8 * OMTW + 10.0, 90.0);
            iconSpot = spotSummaryTB;
            topLeftPlace();
            canvasAdd(appMainCanvas, hideIconsEyeball, WEBCAM_LINE + 4.0 - 10.0, 2.0);
            canvasAdd(appMainCanvas, mirrorOW, WEBCAM_LINE + 4.0 - 10.0, 26.0);
            iconEye = hideIconsEyeball;
            iconMirror = mirrorOW;
            tryLayoutTopLeftIcons();
        }
        void IApplicationLayoutBase.AddHideQuestCheckboxes(Control hideFirstQuestCheckBox, Control hideSecondQuestCheckBox)
        {
            canvasAdd(appMainCanvas, hideFirstQuestCheckBox, WEBCAM_LINE + 5.0, 110.0);
            canvasAdd(appMainCanvas, hideSecondQuestCheckBox, WEBCAM_LINE + 5.0, 130.0);
        }
        void IApplicationLayoutBase.AddLinkRouting(Control linkIcon, Control currentTargetIcon)
        {
            // Link routing icon (and its current-destination icon to its left), scaled down to sit left of the logo, above the triforce chart
            var scale = 0.9;   // 50% bigger than the first 0.6
            var size = 30.0 * scale;
            var logoX = 16.0 * OMTW - kittyWidth - 12.0 + (kittyWidth - 44.0) / 2.0;
            var x = logoX - 18.0 - 2.0 - 25.0 + 20.0 - 10.0;   // 25px further left than the original spot, 20px back to the right, then 10px left again
            var y = THRU_MAIN_MAP_H + 3.0 + 25.0 - 20.0;   // ...and 25px lower, then 20px back up
            linkIcon.RenderTransformOrigin = RelativePoint.TopLeft;   // (WPF scales from the top-left corner)
            currentTargetIcon.RenderTransformOrigin = RelativePoint.TopLeft;
            linkIcon.RenderTransform = new ScaleTransform(scale, scale);
            currentTargetIcon.RenderTransform = new ScaleTransform(scale, scale);
            canvasAdd(appMainCanvas, linkIcon, x, y);
            canvasAdd(appMainCanvas, currentTargetIcon, x - size - 2.0, y);
            linkIcon.ZIndex = 50;           // above the triforce chart (added later) so hovering/clicking Link always works
            currentTargetIcon.ZIndex = 50;
            linkIcon.IsVisible = false;   // the Link routing icon is hidden too   // (WPF: Visibility.Collapsed)
            currentTargetIcon.IsVisible = false;   // routing still works; just don't show the destination box next to Link
        }
        void IApplicationLayoutBase.AddWebcamLine() { } // the webcam area was always too small to be useful
        void IApplicationLayoutBase.AddOverworldCanvas(Control overworldCanvas)
        {
            canvasAdd(appMainCanvas, overworldCanvas, 0.0, 150.0);
        }
        void IApplicationLayoutBase.AddLegend(Control legendCanvas)
        {
            canvasAdd(appMainCanvas, legendCanvas, 0.0, THRU_MAIN_MAP_H);
        }
        void IApplicationLayoutBase.AddItemProgress(Control itemProgressCanvas, Control itemProgressTB)
        {
            canvasAdd(appMainCanvas, itemProgressCanvas, 0.0, THRU_MAP_AND_LEGEND_H + 3.0);
            canvasAdd(appMainCanvas, itemProgressTB, 3.0, THRU_MAP_AND_LEGEND_H + 10.0);   // all the way left (the version button that used to share this space is gone)
            itemProgressLabelForCentering = itemProgressTB;
            centerVerticallyAt(itemProgressTB, SWORD_ICON_CENTER_Y);
        }
        void IApplicationLayoutBase.AddVersionButton(Control _vb) { }   // the version button is no longer shown on the main screen
        void IApplicationLayoutBase.AddOverworldOptionsButton(Control fqsqButton)
        {
            canvasAdd(appMainCanvas, fqsqButton, PANEL_X1, 78.0);
            iconFQSQ = fqsqButton;
            topLeftPlace();
        }
        void IApplicationLayoutBase.AddHintDecoderButton(Control tb)
        {
            canvasAdd(appMainCanvas, tb, WEBCAM_LINE - 41.0, 108.0);
            hintDecoderElementForCentering = tb;
            hookTopLeftOnSizeChange(tb);
            tryCenterHotkeysUnderHintDecoder(PANEL_X0);
        }
        void IApplicationLayoutBase.AddKittyAndLogo(Image kitty, Control _logoBorder, Image _ztlogo, Control discordLink, Control _racingLink)
        {
            var logoX = 16.0 * OMTW - kittyWidth - 22.0 + (kittyWidth - kitty.Width) / 2.0;   // centered in the space the old picture used, nudged 10px left
            canvasAdd(appMainCanvas, discordLink, logoX - (DiscordLinkWidth - kitty.Width) / 2.0, THRU_MAIN_MAP_H + 3.0 + kitty.Height + 2.0);
            canvasAdd(appMainCanvas, kitty, 0.0, 0.0);   // the ZORA logo sits above the ZORA links (positioned in WPFUI.placeLinkStack)
        }
        void IApplicationLayoutBase.AddExtraSiteLinks(Control zoraSiteLink, Control _racingSiteLink)
        {
            canvasAdd(appMainCanvas, zoraSiteLink, 0.0, 0.0);   // (positioned together with the other links in WPFUI.placeLinkStack)
        }
        void IApplicationLayoutBase.AddShowHotKeysButton(Control showHotKeysButton)
        {
            canvasAdd(appMainCanvas, showHotKeysButton, WEBCAM_LINE - 41.0, 128.0);
            hotkeysElementForCentering = showHotKeysButton;
            tryCenterHotkeysUnderHintDecoder(PANEL_X0);
        }
        void IApplicationLayoutBase.AddShowRunCustomButton(Control showRunCustomButton)
        {
            canvasAdd(appMainCanvas, showRunCustomButton, 12.8 * OMTW + 10.0, 120.0);   // under Spot Summary
            iconShowRun = showRunCustomButton;
            topLeftPlace();
            showRunCustomButton.ZIndex = 1000;   // always above the map tiles / their icons
        }
        void IApplicationLayoutBase.AddTopLeftButtons(Control optionsButton, Control drawButton, Control logButton)
        {
            foreach (var e in new[] { optionsButton, drawButton, logButton }) canvasAdd(appMainCanvas, e, 0.0, 0.0);
            addPanelDecor(e => canvasAdd(appMainCanvas, e, 0.0, 0.0));
            iconOptions = optionsButton;
            iconDraw = drawButton;
            iconLog = logButton;
            topLeftPlace();
        }
        void IApplicationLayoutBase.AddQuestReferenceChart(Control chart, double x, double y) => canvasAdd(appMainCanvas, chart, x, y);
        void IApplicationLayoutBase.AddSaveButton(Control saveButton)
        {
            canvasAdd(appMainCanvas, saveButton, 530.0, 28.0);   // below the ? button
            iconSave = saveButton;
            tryLayoutTopLeftIcons();
        }
        void IApplicationLayoutBase.AddItemLocationsButton(Control itemLocationsButton)
        {
            canvasAdd(appMainCanvas, itemLocationsButton, WEBCAM_LINE - 41.0, 60.0);   // (placed exactly by tryLayoutTopLeftIcons, above the Save button)
            iconItemLocations = itemLocationsButton;
            topLeftPlace();
        }
        void IApplicationLayoutBase.AddUserCustomContentButton(Control uccButton)
        {
            canvasAdd(appMainCanvas, uccButton, 16.0 * OMTW - kittyWidth - 50.0, THRU_MAIN_MAP_H + 42.0);
        }
        void IApplicationLayoutBase.AddDungeonTabsOverlay(Control dungeonTabsOverlay)  // for magnifier
        {
            canvasAdd(appMainCanvas, dungeonTabsOverlay, 0.0, START_DUNGEON_AND_NOTES_AREA_H + (double)TH - 2.0);   // (2px up)
        }
        void IApplicationLayoutBase.AddDungeonTabs(Control dungeonTabsWholeCanvas)
        {
            canvasAdd(appMainCanvas, dungeonTabsWholeCanvas, 0.0, START_DUNGEON_AND_NOTES_AREA_H);
        }
        double IApplicationLayoutBase.GetDungeonY() =>
            START_DUNGEON_AND_NOTES_AREA_H;
        void IApplicationLayoutBase.AddBlockers(Control blockerGrid)
        {
            canvasAdd(appMainCanvas, blockerGrid, BLOCKERS_AND_NOTES_OFFSET, START_DUNGEON_AND_NOTES_AREA_H);
        }
        void IApplicationLayoutBase.AddNotesSeedsFlags(Control notesTextBox, Control seedAndFlagsDisplayCanvas)
        {
            canvasAdd(appMainCanvas, notesTextBox, BLOCKERS_AND_NOTES_OFFSET, THRU_BLOCKERS_H);
            canvasAdd(appMainCanvas, seedAndFlagsDisplayCanvas, BLOCKERS_AND_NOTES_OFFSET, THRU_BLOCKERS_H);
        }
        void IApplicationLayoutBase.AddExtraDungeonRightwardStuff(Control grabModeTextBlock, Control rightwardCanvas)
        {
            canvasAdd(appMainCanvas, grabModeTextBlock, BLOCKERS_AND_NOTES_OFFSET, START_DUNGEON_AND_NOTES_AREA_H); // grab mode instructions
            // the hover preview of a dungeon summary fills the notes area: tell it how far down the notes start (Tag = top offset, height) and how wide the area is
            var rwc = (Canvas)rightwardCanvas;
            rwc.Width = appMainCanvas.Width - BLOCKERS_AND_NOTES_OFFSET;
            rwc.Tag = (THRU_BLOCKERS_H - START_DUNGEON_AND_NOTES_AREA_H, rwc.Height - (THRU_BLOCKERS_H - START_DUNGEON_AND_NOTES_AREA_H));   // (a boxed (double, double), as DungeonUI reads it)
            canvasAdd(appMainCanvas, rightwardCanvas, BLOCKERS_AND_NOTES_OFFSET, START_DUNGEON_AND_NOTES_AREA_H);   // extra place for dungeonTabs to draw atop blockers/notes, e.g. hover minimaps
        }
        void IApplicationLayoutBase.AddOWRemainingScreens(Control owRemainingScreensTextBox)
        {
            canvasAdd(appMainCanvas, owRemainingScreensTextBox, OW_SPOTS_LEFT_CENTER_X - 58.0, 0.0);   // spots-left text with the per-type breakdown under it, all just above the Max Hearts box
            centerHorizontallyAt(owRemainingScreensTextBox, OW_SPOTS_LEFT_CENTER_X);
            owRemainingScreensTextBox.ZIndex = -1;   // draw underneath the item boxes / hint icons so it can never cover them
        }
        void IApplicationLayoutBase.AddOWGettableScreens(Control owGettableScreensCheckBox)
        {
            canvasAdd(appMainCanvas, owGettableScreensCheckBox, OW_STATS_BLOCK_CENTER_X - 50.0, 22.0);
            centerHorizontallyAt(owGettableScreensCheckBox, OW_STATS_BLOCK_CENTER_X);
        }
        void IApplicationLayoutBase.AddCurrentMaxHearts(Control currentMaxHeartsTextBox)
        {
            canvasAdd(appMainCanvas, currentMaxHeartsTextBox, OW_STATS_CENTER_X - 56.0, 121.0);
            centerHorizontallyAt(currentMaxHeartsTextBox, OW_STATS_CENTER_X);
        }
        void IApplicationLayoutBase.AddShowCoords(Control showCoordsCB, Image _havePotionLetterImage)
        {
            canvasAdd(appMainCanvas, showCoordsCB, WEBCAM_LINE - 41.0, 88.0);
            coordsCheckBoxEl = showCoordsCB;
            hookTopLeftOnSizeChange(showCoordsCB);
            tryLayoutTopLeftIcons();
            topLeftPlace();
        }
        void IApplicationLayoutBase.AddOWZoneOverlay(Control zone_checkbox)
        {
            canvasAdd(appMainCanvas, zone_checkbox, WEBCAM_LINE - 41.0, 64.0);
            zonesCheckBoxEl = zone_checkbox;
            hookTopLeftOnSizeChange(zone_checkbox);
            tryLayoutTopLeftIcons();
            topLeftPlace();
        }
        void IApplicationLayoutBase.AddMouseHoverExplainer(Control mouseHoverExplainerIcon, Control c)
        {
            canvasAdd(appMainCanvas, mouseHoverExplainerIcon, 530.0, 0.0);
            iconQuestion = mouseHoverExplainerIcon;
            tryLayoutTopLeftIcons();
            canvasAdd(appMainCanvas, c, 0.0, 0.0);
        }
        void IApplicationLayoutBase.AddLinkTarget(Control currentTargetGhostBuster)
        {
            canvasAdd(appMainCanvas, currentTargetGhostBuster, 16.0 * OMTW - 30.0, 120.0);  // location where Link's currentTarget is
        }
        void IApplicationLayoutBase.AddTimeline(Control t1c, Control t2c, Control t3c)
        {
            var TCX = 22.0;  // should be 24, but moving two pixels to the left is simplest fix to get timeline items on 3n pixel boundaries so 2/3 size looks good
            canvasAdd(appMainCanvas, t1c, TCX, START_TIMELINE_H);
            canvasAdd(appMainCanvas, t2c, TCX, START_TIMELINE_H);
            canvasAdd(appMainCanvas, t3c, TCX, START_TIMELINE_H);
        }
        Rect IApplicationLayoutBase.GetTimelineBounds() =>
            new Rect(new Point(0.0, START_TIMELINE_H), new Point(appMainCanvas.Width, THRU_TIMELINE_H));
        Rect IApplicationLayoutBase.GetFullAppBounds() =>
            new Rect(new Point(0.0, 0.0), new Point(appMainCanvas.Width, THRU_TIMELINE_H));
        void IApplicationLayoutBase.AddReminderDisplayOverlay(Control reminderDisplayOuterDockPanel)
        {
            canvasAdd(appMainCanvas, reminderDisplayOuterDockPanel, 0.0, START_TIMELINE_H);
        }
        void IApplicationLayoutBase.AddPostGameDecorationCanvas(Control postgameDecorationCanvas)
        {
            canvasAdd(appMainCanvas, postgameDecorationCanvas, 0.0, START_TIMELINE_H);
        }
        void IApplicationLayoutBase.AddSpotSummary(Control spotSummaryCanvas)
        {
            canvasAdd(appMainCanvas, spotSummaryCanvas, 50.0, 3.0);
        }
        void IApplicationLayoutBase.AddDiskIcon(Control diskIcon)
        {
            canvasAdd(appMainCanvas, diskIcon, OMTW * 16.0 - 40.0, START_TIMELINE_H + 60.0);
        }
        void IApplicationLayoutBase.AllDone()
        {
            canvasAdd(appMainCanvas, topLayerHoverCanvas, 0.0, 0.0);
        }
        void IApplicationLayoutBase.AddTopLayerHover(Control fe, double x, double y) => canvasAdd(topLayerHoverCanvas, fe, x, y);
        void IApplicationLayoutBase.ClearTopLayerHovers() => topLayerHoverCanvas.Children.Clear();
        void IApplicationLayoutBase.FocusOverworld() { }
        void IApplicationLayoutBase.FocusDungeon() { }
        void IApplicationLayoutBase.ShowMouseWarp(Point newPos)
        {
            var oldPos = lastMousePos;   // (WPF: Input.Mouse.GetPosition(cm.AppMainCanvas))
            var line = new Line { StartPoint = new Point(oldPos.X, oldPos.Y), EndPoint = new Point(newPos.X, newPos.Y), Stroke = Brushes.White, StrokeThickness = 2.0 };
            cm.AppMainCanvas.Children.Add(line);
            // (WPF: a Storyboard of four linear 300ms DoubleAnimations: X2/Y2 (the end) from oldPos to newPos at once, X1/Y1 (the start) the same after 150ms
            // (BeginTime), so the line shoots from the old position to the new one. Avalonia's Line has StartPoint/EndPoint, so this tweens them on a timer.)
            double lerp(double a, double b, double t) => a + (b - a) * Math.Clamp(t, 0.0, 1.0);
            var sw = Stopwatch.StartNew();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15.0) };
            void tick()
            {
                var ms = sw.Elapsed.TotalMilliseconds;
                line.EndPoint = new Point(lerp(oldPos.X, newPos.X, ms / 300.0), lerp(oldPos.Y, newPos.Y, ms / 300.0));
                line.StartPoint = new Point(lerp(oldPos.X, newPos.X, (ms - 150.0) / 300.0), lerp(oldPos.Y, newPos.Y, (ms - 150.0) / 300.0));
                if (ms >= 450.0) timer.Stop();
            }
            timer.Tick += (_, _) => tick();
            tick();
            timer.Start();
            async Task go()
            {
                await Task.Delay(500);
                // (WPF: Async.SwitchToContext(ctxt); the await already resumes on the UI thread's context, which is ctxt)
                timer.Stop();
                cm.AppMainCanvas.Children.Remove(line);
            }
            CustomComboBoxes.StartImmediate(go());
        }
        bool IApplicationLayoutBase.IsShort => false;
    }

    ///////////////////////////////////////////////////////////////////////////////////////

    // (F#: inherits ApplicationLayout and re-implements IApplicationLayoutBase; in C# it re-lists the interface, so its own implementations are used)
    public class ShorterApplicationLayout : ApplicationLayout, IApplicationLayoutBase
    {
        private readonly CustomComboBoxes.CanvasManager cm;
        private readonly Canvas appMainCanvas;
        private Control itemProgressLabelForCentering = null;
        private readonly Canvas topLayerHoverCanvas = new Canvas();   // for temporary bits that appear on hover, to ensure they appear on top of everything else in the app, when no other popup active
        private readonly double blockerGridHeight = (double)(36 * 3); // from blocker code
        private readonly double RED_LINE = 3.0;
        private readonly double DUNGEON_H = START_TIMELINE_H - THRU_MAIN_MAP_AND_ITEM_PROGRESS_H;
        private readonly double TIMELINE_H = THRU_TIMELINE_H - START_TIMELINE_H;
        private readonly double H;  // the top one is the larger of the two, so always have window that size
        private readonly double afterSoldItemBoxesX = OW_ITEM_GRID_LOCATIONS.OFFSET + 120.0;
        private readonly double W;
        private readonly Canvas theCanvas;
        // we always draw both the upper and lower, so that e.g. when mouse in upper, a timelime update (the timeline canonically lives in the lower)
        // still draws immediately and we see the projection immediately.  do this by putting a black canvas in between, and swapping order drawn atop each other
        private readonly Canvas black;
        private readonly Canvas upper;
        private readonly Canvas lower;
        private bool currentlyUpper = true;
        // constants for upper half
        private readonly double upperTimelineStart;
        // constants for lower half
        private readonly double dungeonStart;
        private readonly double notesStart;
        private readonly double timelineStart;
        private void switchToDungeon()
        {
            currentlyUpper = false;
            upper.ZIndex = 0;
            lower.ZIndex = 2;
        }
        private void switchToOverworld()
        {
            currentlyUpper = true;
            upper.ZIndex = 2;
            lower.ZIndex = 0;
        }
        public ShorterApplicationLayout(CustomComboBoxes.CanvasManager cm) : base(cm)
        {
            this.cm = cm;
            appMainCanvas = cm.AppMainCanvas;
            H = START_DUNGEON_AND_NOTES_AREA_H + RED_LINE + blockerGridHeight + TIMELINE_H;
            W = appMainCanvas.Width;
            theCanvas = new Canvas { Width = W, Height = H };
            black = new Canvas { Width = W, Height = H, Background = Brushes.Black };
            upper = new Canvas { Width = W, Height = H };
            lower = new Canvas { Width = W, Height = H };
            upperTimelineStart = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H + RED_LINE + blockerGridHeight;
            dungeonStart = H - TIMELINE_H - DUNGEON_H;
            notesStart = H - TIMELINE_H - DUNGEON_H + blockerGridHeight;
            timelineStart = H - TIMELINE_H;
            // (F#: the `do` block)
            canvasAdd(appMainCanvas, theCanvas, 0.0, 0.0);
            canvasAdd(theCanvas, upper, 0.0, 0.0);
            canvasAdd(theCanvas, black, 0.0, 0.0);
            canvasAdd(theCanvas, lower, 0.0, 0.0);
            upper.ZIndex = 2;
            black.ZIndex = 1;
            lower.ZIndex = 0;
            var upperThreshold = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H + RED_LINE;
            var lowerThreshold = H - TIMELINE_H - DUNGEON_H - RED_LINE;
            var diff = upperThreshold - lowerThreshold;
            DungeonPopups.THE_DIFF = diff;
            appMainCanvas.PointerMoved += (_, e) =>   // (WPF: MouseMove)
            {
                if (cm.PopupCanvasStack.Count == 0)
                {
                    var pos = e.GetPosition(appMainCanvas);
                    if (currentlyUpper && pos.Y > upperThreshold)
                    {
                        switchToDungeon();
                        Graphics.SilentlyWarpMouseCursorTo(new Point(pos.X, pos.Y - diff));
                    }
                    if (!(currentlyUpper) && pos.Y < lowerThreshold)
                    {
                        switchToOverworld();
                        Graphics.SilentlyWarpMouseCursorTo(new Point(pos.X, pos.Y + diff));
                    }
                }
            };
        }
        void IApplicationLayoutBase.AddMainTracker(Control mainTracker)
        {
            canvasAdd(upper, mainTracker, 0.0, 0.0);
            var mainView = Broadcast.makeViewRectImpl(new Point(0.0, 0.0), new Point(OW_ITEM_GRID_LOCATIONS.OFFSET, (double)(30 * 5)), upper);
            canvasAdd(lower, mainView, 0.0, 0.0);
        }
        void IApplicationLayoutBase.AddNumberedTriforceCanvas(Canvas triforceCanvas, int i)
        {
            canvasAdd(upper, triforceCanvas, OW_ITEM_GRID_LOCATIONS.OFFSET + 30.0 * (double)i, 0.0);
            if (i == 1) // only once
            {
                var ntView = Broadcast.makeViewRectImpl(new Point(OW_ITEM_GRID_LOCATIONS.OFFSET, 0.0), new Point(OW_ITEM_GRID_LOCATIONS.OFFSET + (double)(30 * 8), (double)(30)), upper);
                canvasAdd(lower, ntView, OW_ITEM_GRID_LOCATIONS.OFFSET, 0.0);
            }
        }
        void IApplicationLayoutBase.AddItemGridStuff(Control owItemGrid, Control _toggleBookShieldCheckBox, Control _bookIsAtlasCheckBox, Control _highlightOpenCavesCheckBox, Control timerResetButton, Control spotSummaryTB, Control mirrorOW,
                                                     Control hideIconsEyeball, Button _moreFQSQoptionsButton, Control _ws_ms_bu_toggleButton)
        {
            canvasAdd(upper, owItemGrid, OW_ITEM_GRID_LOCATIONS.OFFSET, 30.0);
            rightButtonForSizing = timerResetButton;
            hookTopLeftOnSizeChange(timerResetButton);
            canvasAdd(upper, timerResetButton, 12.8 * OMTW + 10.0, 60.0);
            canvasAdd(upper, spotSummaryTB, 12.8 * OMTW + 10.0, 90.0);
            iconSpot = spotSummaryTB;
            topLeftPlace();
            canvasAdd(upper, hideIconsEyeball, WEBCAM_LINE + 4.0 - 10.0, 2.0);
            canvasAdd(upper, mirrorOW, WEBCAM_LINE + 4.0 - 10.0, 26.0);
            iconEye = hideIconsEyeball;
            iconMirror = mirrorOW;
            tryLayoutTopLeftIcons();
            // just capture a swath of stuff
            var swathView = Broadcast.makeViewRectImpl(new Point(OW_ITEM_GRID_LOCATIONS.OFFSET, 30.0), new Point(WEBCAM_LINE, (double)(30 * 5)), upper);
            canvasAdd(lower, swathView, OW_ITEM_GRID_LOCATIONS.OFFSET, 30.0);
        }
        void IApplicationLayoutBase.AddHideQuestCheckboxes(Control hideFirstQuestCheckBox, Control hideSecondQuestCheckBox)
        {
            canvasAdd(upper, hideFirstQuestCheckBox, WEBCAM_LINE + 5.0, 110.0);
            canvasAdd(upper, hideSecondQuestCheckBox, WEBCAM_LINE + 5.0, 130.0);
        }
        void IApplicationLayoutBase.AddLinkRouting(Control _linkIcon, Control _currentTargetIcon)
        {
            // the Link routing icon is intentionally not shown
        }
        void IApplicationLayoutBase.AddWebcamLine() { } // the webcam area was always too small to be useful
        void IApplicationLayoutBase.AddOverworldCanvas(Control overworldCanvas)
        {
            canvasAdd(upper, overworldCanvas, 0.0, 150.0);
            var scaleW = (W - afterSoldItemBoxesX) / W;
            var scaleH = (180.0 - 45.0) / (THRU_MAIN_MAP_H - 150.0);
            var owm = Broadcast.makeViewRectImpl(new Point(0.0, 150.0), new Point(W, THRU_MAIN_MAP_H), upper);
            var scaleTrans = new ScaleTransform(scaleW, scaleH);
            // (WPF: if scaleTrans.CanFreeze then scaleTrans.Freeze(); Avalonia transforms can't be frozen)
            owm.RenderTransformOrigin = RelativePoint.TopLeft;   // (WPF scales from the top-left corner)
            owm.RenderTransform = scaleTrans;
            canvasAdd(lower, owm, afterSoldItemBoxesX, dungeonStart - (180.0 - 45.0) - RED_LINE);
        }
        void IApplicationLayoutBase.AddLegend(Control legendCanvas)
        {
            canvasAdd(upper, legendCanvas, 0.0, THRU_MAIN_MAP_H);
        }
        void IApplicationLayoutBase.AddItemProgress(Control itemProgressCanvas, Control itemProgressTB)
        {
            canvasAdd(upper, itemProgressCanvas, 0.0, THRU_MAP_AND_LEGEND_H + 3.0);
            canvasAdd(upper, itemProgressTB, 3.0, THRU_MAP_AND_LEGEND_H + 10.0);   // all the way left (the version button that used to share this space is gone)
            itemProgressLabelForCentering = itemProgressTB;
            centerVerticallyAt(itemProgressTB, SWORD_ICON_CENTER_Y);
            var pro = Broadcast.makeViewRectImpl(new Point(ITEM_PROGRESS_FIRST_ITEM, THRU_MAP_AND_LEGEND_H),
                                new Point(ITEM_PROGRESS_FIRST_ITEM + 15.0 * 30.0 - 11.0, THRU_MAIN_MAP_AND_ITEM_PROGRESS_H), upper);  // -11. because 'Hint decoder' button infringes into empty space by Magic Key
            var proScale = new ScaleTransform(0.88, 0.88);   // shrunk so the whole strip ends before the map
            // (WPF: if proScale.CanFreeze then proScale.Freeze())
            pro.RenderTransformOrigin = RelativePoint.TopLeft;   // (WPF scales from the top-left corner)
            pro.RenderTransform = proScale;
            canvasAdd(lower, pro, 10.0, dungeonStart - RED_LINE - 30.0 * 0.88 - 4.0);   // just above the dungeon panels
            var tb = new TextBox { Text = "Move mouse above red line for overworld view", Foreground = Brushes.Orange, Background = Brushes.Black,
                                   IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 14.0, VerticalAlignment = VerticalAlignment.Center };
            canvasAdd(lower, tb, 50.0, 152.0);
            var lineCenter = dungeonStart - RED_LINE / 2.0;
            var line = new Line { StartPoint = new Point(0.0, lineCenter), EndPoint = new Point(W, lineCenter), Stroke = Brushes.Red, StrokeThickness = 3.0 };
            canvasAdd(lower, line, 0.0, 0.0);
        }
        void IApplicationLayoutBase.AddVersionButton(Control _vb)   // the version button is no longer shown on the main screen
        {
            var v = Broadcast.makeViewRectImpl(new Point(0.0, THRU_MAP_AND_LEGEND_H), new Point(50.0, THRU_MAIN_MAP_AND_ITEM_PROGRESS_H), upper);
            _ = v;   // (the version button is gone; its old slot is where the item progress strip used to sit)
        }
        void IApplicationLayoutBase.AddOverworldOptionsButton(Control fqsqButton)
        {
            canvasAdd(upper, fqsqButton, PANEL_X1, 78.0);
            iconFQSQ = fqsqButton;
            topLeftPlace();
        }
        void IApplicationLayoutBase.AddHintDecoderButton(Control tb)
        {
            canvasAdd(upper, tb, WEBCAM_LINE - 41.0, 108.0);
            hintDecoderElementForCentering = tb;
            hookTopLeftOnSizeChange(tb);
            tryCenterHotkeysUnderHintDecoder(PANEL_X0);
        }
        void IApplicationLayoutBase.AddKittyAndLogo(Image kitty0, Control _logoBorder, Image _ztlogo, Control discordLink, Control _racingLink)
        {
            var logoX = 16.0 * OMTW - kittyWidth - 22.0 + (kittyWidth - kitty0.Width) / 2.0;   // centered in the space the old picture used, nudged 10px left
            canvasAdd(upper, discordLink, logoX - (DiscordLinkWidth - kitty0.Width) / 2.0, THRU_MAIN_MAP_H + 3.0 + kitty0.Height + 2.0);
            // WANT!
            // (F# shadows the `kitty` parameter here; the parameter is `kitty0` in C#)
            var kitty = new Image();
            var imageStream = Graphics.GetResourceStream("CustomLogo.png");
            kitty.Source = new Bitmap(imageStream);   // (WPF: BitmapFrame.Create(imageStream))
            kitty.Width = 45.0;
            kitty.Height = 45.0;
            var lowerDiscordLink = MakeDiscordLink("https://discord.gg/zescSjDtme");
            canvasAdd(lower, lowerDiscordLink, afterSoldItemBoxesX + 150.0 + 5.0 - (DiscordLinkWidth - kitty.Width) / 2.0, 48.0 + kitty.Height + 1.0);
        }
        void IApplicationLayoutBase.AddExtraSiteLinks(Control zoraSiteLink, Control _racingSiteLink)
        {
            canvasAdd(upper, zoraSiteLink, 0.0, 0.0);
        }
        void IApplicationLayoutBase.AddShowHotKeysButton(Control showHotKeysButton)
        {
            canvasAdd(upper, showHotKeysButton, WEBCAM_LINE - 41.0, 128.0);
            hotkeysElementForCentering = showHotKeysButton;
            tryCenterHotkeysUnderHintDecoder(PANEL_X0);
        }
        void IApplicationLayoutBase.AddShowRunCustomButton(Control showRunCustomButton)
        {
            canvasAdd(upper, showRunCustomButton, 12.8 * OMTW + 10.0, 120.0);   // under Spot Summary
            iconShowRun = showRunCustomButton;
            topLeftPlace();
            showRunCustomButton.ZIndex = 1000;   // always above the map tiles / their icons
        }
        void IApplicationLayoutBase.AddTopLeftButtons(Control optionsButton, Control drawButton, Control logButton)
        {
            foreach (var e in new[] { optionsButton, drawButton, logButton }) canvasAdd(upper, e, 0.0, 0.0);
            addPanelDecor(e => canvasAdd(upper, e, 0.0, 0.0));
            iconOptions = optionsButton;
            iconDraw = drawButton;
            iconLog = logButton;
            topLeftPlace();
        }
        void IApplicationLayoutBase.AddQuestReferenceChart(Control chart, double x, double y) => canvasAdd(upper, chart, x, y);
        void IApplicationLayoutBase.AddSaveButton(Control saveButton)
        {
            canvasAdd(upper, saveButton, 530.0, 28.0);   // below the ? button
            iconSave = saveButton;
            tryLayoutTopLeftIcons();
        }
        void IApplicationLayoutBase.AddItemLocationsButton(Control itemLocationsButton)
        {
            canvasAdd(upper, itemLocationsButton, WEBCAM_LINE - 41.0, 60.0);   // (placed exactly by tryLayoutTopLeftIcons, above the Save button)
            iconItemLocations = itemLocationsButton;
            topLeftPlace();
        }
        void IApplicationLayoutBase.AddUserCustomContentButton(Control uccButton)
        {
            // canvasAdd(upper, uccButton, 16.*OMTW - kittyWidth - 50., THRU_MAIN_MAP_H + 42.)
            _ = uccButton; // UCC is disabled in ShorterApplicationLayout
        }
        void IApplicationLayoutBase.AddDungeonTabsOverlay(Control dungeonTabsOverlay)  // for magnifier
        {
            // canvasAdd(upper, dungeonTabsOverlay, 0., THRU_MAP_AND_LEGEND_H)
            _ = dungeonTabsOverlay; // magnifier is disabled in ShorterApplicationLayout
        }
        void IApplicationLayoutBase.AddDungeonTabs(Control dungeonTabsWholeCanvas)
        {
            canvasAdd(lower, dungeonTabsWholeCanvas, 0.0, dungeonStart);
        }
        double IApplicationLayoutBase.GetDungeonY() =>
            dungeonStart;
        void IApplicationLayoutBase.AddBlockers(Control blockerGrid)
        {
            canvasAdd(lower, blockerGrid, BLOCKERS_AND_NOTES_OFFSET, dungeonStart);
            var blockersView = Broadcast.makeViewRectImpl(new Point(BLOCKERS_AND_NOTES_OFFSET, dungeonStart), new Point(W, dungeonStart + blockerGridHeight), lower);
            canvasAdd(upper, blockersView, BLOCKERS_AND_NOTES_OFFSET, THRU_MAIN_MAP_AND_ITEM_PROGRESS_H);
        }
        void IApplicationLayoutBase.AddNotesSeedsFlags(Control notesTextBox, Control seedAndFlagsDisplayCanvas)
        {
            canvasAdd(lower, notesTextBox, BLOCKERS_AND_NOTES_OFFSET, notesStart);
            canvasAdd(lower, seedAndFlagsDisplayCanvas, BLOCKERS_AND_NOTES_OFFSET, notesStart);
            var notesView = Broadcast.makeViewRectImpl(new Point(BLOCKERS_AND_NOTES_OFFSET, notesStart), new Point(W, notesStart + blockerGridHeight), lower);
            canvasAdd(upper, notesView, 0.0, THRU_MAIN_MAP_AND_ITEM_PROGRESS_H + RED_LINE);
            var tb = new TextBox { Text = "Move mouse\nbelow red line\nto switch to\ndungeon view", Foreground = Brushes.Orange, Background = Brushes.Black,
                                   IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 16.0, VerticalAlignment = VerticalAlignment.Center };
            var b = new Border { Child = tb, BorderThickness = new Thickness(0.0, 3.0, 0.0, 0.0) };
            canvasAdd(upper, b, W - BLOCKERS_AND_NOTES_OFFSET + 5.0, THRU_MAIN_MAP_AND_ITEM_PROGRESS_H + RED_LINE);
            var lineCenter = THRU_MAIN_MAP_AND_ITEM_PROGRESS_H + RED_LINE / 2.0;
            var line = new Line { StartPoint = new Point(0.0, lineCenter), EndPoint = new Point(W, lineCenter), Stroke = Brushes.Red, StrokeThickness = 3.0 };
            canvasAdd(upper, line, 0.0, 0.0);
        }
        void IApplicationLayoutBase.AddExtraDungeonRightwardStuff(Control grabModeTextBlock, Control rightwardCanvas)
        {
            canvasAdd(lower, grabModeTextBlock, BLOCKERS_AND_NOTES_OFFSET, dungeonStart); // grab mode instructions
            var rwc = (Canvas)rightwardCanvas;
            rwc.Width = W - BLOCKERS_AND_NOTES_OFFSET;
            rwc.Tag = (notesStart - dungeonStart, rwc.Height - (notesStart - dungeonStart));   // (a boxed (double, double), as DungeonUI reads it)
            canvasAdd(lower, rightwardCanvas, BLOCKERS_AND_NOTES_OFFSET, dungeonStart);   // extra place for dungeonTabs to draw atop blockers/notes, e.g. hover minimaps
        }
        void IApplicationLayoutBase.AddOWRemainingScreens(Control owRemainingScreensTextBox)
        {
            canvasAdd(upper, owRemainingScreensTextBox, OW_SPOTS_LEFT_CENTER_X - 58.0, 0.0);   // spots-left text with the per-type breakdown under it, all just above the Max Hearts box
            centerHorizontallyAt(owRemainingScreensTextBox, OW_SPOTS_LEFT_CENTER_X);
            owRemainingScreensTextBox.ZIndex = -1;   // draw underneath the item boxes / hint icons so it can never cover them
        }
        void IApplicationLayoutBase.AddOWGettableScreens(Control owGettableScreensCheckBox)
        {
            canvasAdd(upper, owGettableScreensCheckBox, OW_STATS_BLOCK_CENTER_X - 50.0, 22.0);
            centerHorizontallyAt(owGettableScreensCheckBox, OW_STATS_BLOCK_CENTER_X);
        }
        void IApplicationLayoutBase.AddCurrentMaxHearts(Control currentMaxHeartsTextBox)
        {
            canvasAdd(upper, currentMaxHeartsTextBox, OW_STATS_CENTER_X - 56.0, 121.0);
            centerHorizontallyAt(currentMaxHeartsTextBox, OW_STATS_CENTER_X);
        }
        void IApplicationLayoutBase.AddShowCoords(Control showCoordsCB, Image _havePotionLetterImage)
        {
            canvasAdd(upper, showCoordsCB, WEBCAM_LINE - 41.0, 88.0);
            coordsCheckBoxEl = showCoordsCB;
            hookTopLeftOnSizeChange(showCoordsCB);
            tryLayoutTopLeftIcons();
            topLeftPlace();
        }
        void IApplicationLayoutBase.AddOWZoneOverlay(Control zone_checkbox)
        {
            canvasAdd(upper, zone_checkbox, WEBCAM_LINE - 41.0, 64.0);
            zonesCheckBoxEl = zone_checkbox;
            hookTopLeftOnSizeChange(zone_checkbox);
            tryLayoutTopLeftIcons();
            topLeftPlace();
        }
        void IApplicationLayoutBase.AddMouseHoverExplainer(Control mouseHoverExplainerIcon, Control c)
        {
            canvasAdd(upper, mouseHoverExplainerIcon, 530.0, 0.0);
            iconQuestion = mouseHoverExplainerIcon;
            tryLayoutTopLeftIcons();
            canvasAdd(upper, c, 0.0, 0.0);
        }
        void IApplicationLayoutBase.AddLinkTarget(Control currentTargetGhostBuster)
        {
            canvasAdd(upper, currentTargetGhostBuster, 16.0 * OMTW - 30.0, 120.0);  // location where Link's currentTarget is
        }
        void IApplicationLayoutBase.AddTimeline(Control t1c, Control t2c, Control t3c)
        {
            var TCX = 22.0;  // should be 24, but moving two pixels to the left is simplest fix to get timeline items on 3n pixel boundaries so 2/3 size looks good
            canvasAdd(lower, t1c, TCX, timelineStart);
            canvasAdd(lower, t2c, TCX, timelineStart);
            canvasAdd(lower, t3c, TCX, timelineStart);
            var timelineView = Broadcast.makeViewRectImpl(new Point(0.0, timelineStart), new Point(W, timelineStart + TIMELINE_H), lower);
            canvasAdd(upper, timelineView, 0.0, upperTimelineStart);
        }
        Rect IApplicationLayoutBase.GetTimelineBounds() =>
            new Rect(new Point(0.0, timelineStart), new Point(W, H));
        Rect IApplicationLayoutBase.GetFullAppBounds() =>
            new Rect(new Point(0.0, 0.0), new Point(W, H));
        void IApplicationLayoutBase.AddReminderDisplayOverlay(Control reminderDisplayOuterDockPanel)
        {
            canvasAdd(lower, reminderDisplayOuterDockPanel, 0.0, timelineStart);
        }
        void IApplicationLayoutBase.AddPostGameDecorationCanvas(Control postgameDecorationCanvas)
        {
            canvasAdd(lower, postgameDecorationCanvas, 0.0, timelineStart);
        }
        void IApplicationLayoutBase.AddSpotSummary(Control spotSummaryCanvas)
        {
            canvasAdd(upper, spotSummaryCanvas, 50.0, 3.0);
        }
        void IApplicationLayoutBase.AddDiskIcon(Control diskIcon)
        {
            canvasAdd(lower, diskIcon, OMTW * 16.0 - 40.0, timelineStart + 60.0);
            cm.SetHeight(H);  // canvas manager always knows the world as though SmallerAppWindowScaleFactor = 1.0
            // change the main app window height
            var H2 = H * Graphics.appScale();   // (the live scale of the resizable main window)   // (F# shadows H)
            // (WPF: `let CHROME_HEIGHT = 39.  // Windows app border`, then `((cm.RootCanvas.Parent :?> Canvas).Parent :?> Window).Height <- H + CHROME_HEIGHT // this is fragile, but
            // don't know a better way right now`. WPF's Window.Height is the outer height, so it adds the Windows frame; Avalonia's is the client height, so no
            // CHROME_HEIGHT. The window is found with TopLevel.GetTopLevel, as WPF's Window.GetWindow, rather than by the parent chain.)
#if BROWSER
            if (BrowserWindow.Of(cm.RootCanvas) is Window win)
#else
            if (TopLevel.GetTopLevel(cm.RootCanvas) is Window win)
#endif
                win.Height = H2;
        }
        void IApplicationLayoutBase.AllDone()
        {
            canvasAdd(appMainCanvas, topLayerHoverCanvas, 0.0, 0.0);
        }
        void IApplicationLayoutBase.AddTopLayerHover(Control fe, double x, double y) => canvasAdd(topLayerHoverCanvas, fe, x, y);
        void IApplicationLayoutBase.ClearTopLayerHovers() => topLayerHoverCanvas.Children.Clear();
        void IApplicationLayoutBase.FocusOverworld()
        {
            async Task go()
            {
                await Task.Delay(10);   // the mouse was just warped before this call, pump events to process the MouseMove
                // (WPF: Async.SwitchToContext(ctxt); the await resumes on the UI thread's context)
                switchToOverworld();
            }
            CustomComboBoxes.StartImmediate(go());
        }
        void IApplicationLayoutBase.FocusDungeon()
        {
            async Task go()
            {
                await Task.Delay(10);   // the mouse was just warped before this call, pump events to process the MouseMove
                // (WPF: Async.SwitchToContext(ctxt); the await resumes on the UI thread's context)
                switchToDungeon();
            }
            CustomComboBoxes.StartImmediate(go());
        }
        void IApplicationLayoutBase.ShowMouseWarp(Point _newPos) { }
        bool IApplicationLayoutBase.IsShort => true;
    }

    ////////////////////////////////////////////////////////////////////////

    public static Window makeMouseMagnifierWindow(CustomComboBoxes.CanvasManager cm)
    {
        var mmWindow = new Window();
        mmWindow.Title = "ZORATracker mouse magnifier";
        mmWindow.CanResize = true;   // (WPF: ResizeMode.CanResizeWithGrip; Avalonia windows resize from their edges and corners)
        mmWindow.SizeToContent = SizeToContent.Manual;
        mmWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        // Do not set Owner — owned windows go behind non-owned popout windows and cannot be brought back.
        // Instead, close manually when main window closes (same pattern as popout windows).
        var mainWindow = ShowRunCustom.mainWindow();   // (WPF: Application.Current.MainWindow)
        if (mainWindow != null)
            mainWindow.Closed += (_, _) => { try { mmWindow.Close(); } catch { } };

        var MSF = // main scale factor
            TrackerModelOptions.SmallerAppWindow.Value ? TrackerModelOptions.SmallerAppWindowScaleFactor : 1.0;
        var defaultStartSizeFactor = 0.66666666666666;
        mmWindow.Width = cm.AppMainCanvas.Width * MSF * defaultStartSizeFactor;
        mmWindow.Height = cm.AppMainCanvas.Width * MSF * defaultStartSizeFactor;  // square start

        var c = new Canvas { Background = Brushes.DarkSlateBlue };
        c.UseLayoutRounding = true;
        mmWindow.Content = c;
        // use BitmapCacheBrush rather than VisualBrush because we want to NearestNeightbor all the pixels
        // (Avalonia has no BitmapCacheBrush; a VisualBrush of the root canvas, with nearest-neighbour bitmap scaling on the rectangle)
        var wholeView = new Rectangle { Width = cm.RootCanvas.Width, Height = cm.RootCanvas.Height, Fill = new VisualBrush(cm.RootCanvas) { Stretch = Stretch.Fill } };
        RenderOptions.SetBitmapInterpolationMode(wholeView, BitmapInterpolationMode.None);

        var SCALE = 3.0 * MSF;
        var st = new ScaleTransform(SCALE, SCALE);
        wholeView.RenderTransform = st;
        canvasAdd(c, wholeView, 0.0, 0.0);

        Polygon addFakeMouse(Canvas c)
        {
            var fakeMouse = new Polygon { Fill = Brushes.White };
            fakeMouse.Points = new List<Point> { new Point(0.0, 0.0), new Point(12.0, 6.0), new Point(6.0, 12.0) };
            c.Children.Add(fakeMouse);
            return fakeMouse;
        }
        var fakeMouse = addFakeMouse(c);
        double round(double x) => Math.Round(x);
        void update()
        {
            Canvas.SetLeft(fakeMouse, round(c.Bounds.Width / 2.0));
            Canvas.SetTop(fakeMouse, round(c.Bounds.Height / 2.0));
            // (WPF: st.CenterX/CenterY; Avalonia's ScaleTransform has no centre, so the same point is the RenderTransformOrigin, in absolute units)
            wholeView.RenderTransformOrigin = new RelativePoint(round(c.Bounds.Width / 2.0), round(c.Bounds.Height / 2.0), RelativeUnit.Absolute);
        }
        var desc = "You can resize this window by grabbing\nthe bottom right of it, or move this window\nby grabbing the title bar with the mouse.";
        var text = new TextBox { Text = desc, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0),
                                 FontSize = 16.0, Margin = new Thickness(6.0) };
        var b = new Border { Child = text, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(3.0), Background = Brushes.Black };
        var loaded = false;
        var w = mmWindow;
        void save()
        {
            // (Left/Top in WPF units, through ShowRunCustom's helpers; the size is the client size, as Popouts' HotKeys window)
            TrackerModelOptions.MouseMagnifierWindowLTWH = $"{(int)ShowRunCustom.leftOf(w)},{(int)ShowRunCustom.topOf(w)},{(int)w.ClientSize.Width},{(int)w.ClientSize.Height}";
            TrackerModelOptions.writeSettings();
        }
        var leftTopWidthHeight = TrackerModelOptions.MouseMagnifierWindowLTWH;
        var matches = System.Text.RegularExpressions.Regex.Match(leftTopWidthHeight, @"^(-?\d+),(-?\d+),(\d+),(\d+)$");
        if (matches.Success)
        {
            ShowRunCustom.setLeftTop(w, double.Parse(matches.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), double.Parse(matches.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
            w.Width = double.Parse(matches.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
            w.Height = double.Parse(matches.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
        mmWindow.PositionChanged += (_, _) =>   // (WPF: LocationChanged)
        {
            if (loaded) b.Opacity = 0.0;  // dismiss instructions
            save();
        };
        mmWindow.SizeChanged += (_, _) =>
        {
            update();
            if (loaded) b.Opacity = 0.0;  // dismiss instructions
            save();
        };
        mmWindow.Loaded += (_, _) =>
        {
            update();
            canvasAdd(c, b, 20.0, 20.0);
            async Task go()
            {
                // (WPF: Async.SwitchToThreadPool, Sleep 8000, SwitchToContext(ctxt); Task.Delay waits without blocking and resumes on the UI thread)
                await Task.Delay(8000);
                b.Opacity = 0.0;  // dismiss instructions after a short time
            }
            CustomComboBoxes.StartImmediate(go());
            loaded = true;
        };
        cm.RootCanvas.PointerMoved += (_, ea) =>   // we need RootCanvas to see mouse moving in popups   // (WPF: MouseMove)
        {
            var mousePos = ea.GetPosition(cm.AppMainCanvas);
            Canvas.SetLeft(wholeView, round(SCALE * ((c.Bounds.Width / 2.0) - mousePos.X)));
            Canvas.SetTop(wholeView, round(SCALE * ((c.Bounds.Height / 2.0) - mousePos.Y)));
        };
        return mmWindow;
    }

    public static void setupMouseMagnifier(CustomComboBoxes.CanvasManager cm, Action refocusMainWindow)
    {
        Window mouseMagnifierWindow = null;
        if (TrackerModelOptions.ShowMouseMagnifierWindow.Value)
        {
            mouseMagnifierWindow = makeMouseMagnifierWindow(cm);
            mouseMagnifierWindow.Show();
            refocusMainWindow();
        }
        OptionsMenu.mouseMagnifierWindowOptionChanged += () =>
        {
            // close existing
            if (mouseMagnifierWindow != null)
            {
                mouseMagnifierWindow.Close();
                mouseMagnifierWindow = null;
            }
            // maybe restart
            if (TrackerModelOptions.ShowMouseMagnifierWindow.Value)
            {
                mouseMagnifierWindow = makeMouseMagnifierWindow(cm);
                mouseMagnifierWindow.Show();
                refocusMainWindow();
            }
        };
    }
}
