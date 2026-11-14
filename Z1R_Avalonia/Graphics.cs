// Ported from Z1R_WPF/Graphics.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
// (System.Drawing.* stays fully qualified, as in the F#: a `using System.Drawing;` would make `Graphics` ambiguous.)
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Z1R_Avalonia;

public static class Graphics
{
    public static Window theWindow = null;
    // The main window is resizable: its content scale follows the window size. dynamicAppScale is that live scale (0 until the window has computed one).
    // Everything that converts between app coordinates and screen/window pixels uses appScale() instead of the fixed 'smaller window' option.
    public static double dynamicAppScale = 0.0;
    public static double appScale()
    {
        if (dynamicAppScale > 0.0) return dynamicAppScale;
        else if (TrackerModelOptions.SmallerAppWindow.Value) return TrackerModelOptions.SmallerAppWindowScaleFactor;
        else return 1.0;
    }

    // (Avalonia brushes can't be frozen; kept so the ported code reads the same)
    public static SolidColorBrush freeze(SolidColorBrush b) => b;

    // (WPF: Win32 SendInput structs. Only used by code the WPF tracker has commented out; the Windows calls now live in Platform.cs.)
    public struct POINT
    {
        public readonly int x;
        public readonly int y;
        public POINT(int _x, int _y) { x = _x; y = _y; }
    }

    // Fake mouse input and cursor warping. On Windows these are the same user32 calls as the WPF tracker; macOS/Linux versions are in Platform.cs.
    public class Win32
    {
        //This simulates a left mouse click
        public static void LeftMouseClick() => Platform.LeftMouseClick();
        public static void RightMouseClick() => Platform.RightMouseClick();
        public static void MiddleMouseClick() => Platform.MiddleMouseClick();
        public static void ScrollWheelRotateUp() => Platform.ScrollWheelRotateUp();
        public static void ScrollWheelRotateDown() => Platform.ScrollWheelRotateDown();

        // returns false if this platform can't warp the cursor
        public static bool SetCursor(double x, double y)
        {
            if (SelfTest.isSelfTest || LoadDump.isActive) return false;   // (a test run must not move the user's real mouse cursor)
            var transformedPoint = new Point(x * appScale(), y * appScale());
            var pos = theWindow.PointToScreen(transformedPoint);
            return Platform.WarpCursor(pos.X, pos.Y, theWindow.DesktopScaling);
        }

        public static int SetSonar(bool enable) => Platform.SetMouseSonar(enable);
    }

    // (F#: `let volumeChanged = new Event<int>()`, which other files Trigger. A C# event can only be raised by its own class, so other
    // files call triggerVolumeChanged(v) for WPF's `volumeChanged.Trigger(v)`; subscribe with `volumeChanged += v => ...`.)
    public static event Action<int> volumeChanged;
    public static void triggerVolumeChanged(int v) => volumeChanged?.Invoke(v);
    private static double soundVolume = (double)TrackerModelOptions.Volume / 300.0;
    public static void PlaySoundForSpeechRecognizedAndUsedToMark()
    {
        Platform.PlaySound(Platform.Files.App("confirm_speech.wav"), soundVolume);
    }
    public static void PlaySoundForReminder()
    {
        Platform.PlaySound(Platform.Files.App("reminder_clink.wav"), soundVolume);
    }
    private static readonly bool _init1 = init1();
    private static bool init1()
    {
        volumeChanged += v =>
        {
            soundVolume = (double)v / 300.0;
        };
        return true;
    }

    public static Action<string> ErrorBeepWithReminderLogText = (string _txt) => { };

    // (WPF: this assembly. The art lives in Z1R_Avalonia_WPFResources, an assembly named Z1R_WPF with the same resource names.)
    public static System.IO.Stream GetResourceStream(string name) => Z1R_WPF_Resources.Marker.Assembly.GetManifestResourceStream(name);

    public static void unparent(Control fe)
    {
        if (fe.Parent != null)
            ((Panel)fe.Parent).Children.Remove(fe);
    }

    public static void canvasAdd(Canvas c, Control item, double left, double top)
    {
        if (item != null)
        {
            c.Children.Add(item);
            Canvas.SetTop(item, top);
            Canvas.SetLeft(item, left);
        }
    }

    public static void gridAdd(Grid g, Control x, int c, int r)
    {
        g.Children.Add(x);
        Grid.SetColumn(x, c);
        Grid.SetRow(x, r);
    }
    public static Grid makeGrid(int nc, int nr, int cw, int rh)
    {
        var grid = new Grid();
        for (int i = 0; i < nc; i++)
        {
            var w = cw == -1 ? GridLength.Auto : new GridLength((double)cw);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
        }
        for (int i = 0; i < nr; i++)
        {
            var h = rh == -1 ? GridLength.Auto : new GridLength((double)rh);
            grid.RowDefinitions.Add(new RowDefinition { Height = h });
        }
        return grid;
    }
    public static Grid center(Control e, int w, int h)
    {
        var g = makeGrid(1, 1, w, h);
        gridAdd(g, e, 0, 0);
        return g;
    }
    public static DockPanel dock(Control e, Dock dock)
    {
        var d = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(e, dock);
        d.Children.Add(e);
        return d;
    }
    public static (Line, Polygon) makeArrow(double targetX, double targetY, double sourceX, double sourceY, IBrush brush)
    {
        var (tx, ty) = (targetX, targetY);
        var (sx, sy) = (sourceX, sourceY);
        // line from source to target
        var line = new Line { StartPoint = new Point(sx, sy), EndPoint = new Point(tx, ty), Stroke = brush, StrokeThickness = 3.0 };
        line.StrokeDashArray = new AvaloniaList<double>(new[] { 5.0, 4.0 });
        double sq(double x) => x * x;
        var pct = 1.0 - 15.0 / Math.Sqrt(sq(tx - sx) + sq(ty - sy));   // arrowhead base ideally 15 pixels down the line
        pct = Math.Max(pct, 0.93);                                    // but at most 93% towards the target, for small lines
        var (ax, ay) = ((tx - sx) * pct + sx, (ty - sy) * pct + sy);
        // differential between target and arrowhead base
        var (dx, dy) = (tx - ax, ty - ay);
        // points orthogonal to the line from the base
        var (p1x, p1y) = (ax + dy / 2.0, ay - dx / 2.0);
        var (p2x, p2y) = (ax - dy / 2.0, ay + dx / 2.0);
        // triangle to make arrowhead
        var triangle = new Polygon { Fill = brush };
        triangle.Points = new List<Point> { new Point(tx, ty), new Point(p1x, p1y), new Point(p2x, p2y) };
        return (line, triangle);
    }
    // WPF scales the whole checkbox up and its content back down, so only the box grows. Avalonia has no LayoutTransform property,
    // so the rounded checkbox template (below) wraps its box in a LayoutTransformControl named "boxScale" that this sets.
    public static void scaleUpCheckBoxBox(CheckBox cb, double scale)
    {
        void apply(INameScope ns)
        {
            var ltc = ns.Find<LayoutTransformControl>("boxScale");
            if (ltc != null)
                ltc.LayoutTransform = new ScaleTransform(scale, scale);
        }
        cb.TemplateApplied += (_, ea) => apply(ea.NameScope);
        // the WPF transform also scales the gap between the box and the text (WPF reads the style's 4px padding here; Avalonia's default is 0 until the style is applied, which left the label hugging the box)
        cb.Padding = new Thickness((cb.Padding.Left > 0.0 ? cb.Padding.Left : 4.0) * scale, cb.Padding.Top, cb.Padding.Right, cb.Padding.Bottom);
        // (and the row: WPF's scaled-up checkbox rows come out 2px taller than the box-only scaling here; found by tests/side-by-side.ps1)
        cb.Margin = new Thickness(cb.Margin.Left, cb.Margin.Top + 1.0, cb.Margin.Right, cb.Margin.Bottom + 1.0);
    }

    // WPF's CheckBox `Checked` / `Unchecked` events (raised when IsChecked becomes true / false, also when set from code). Avalonia 11 still has
    // them but marks them obsolete in favour of IsCheckedChanged, so ported files use these instead: WPF `cb.Checked.Add(fun _ -> ...)` →
    // `Graphics.onChecked(cb, () => ...)`.
    // WPF's MouseWheel fires once per wheel notch. Avalonia reports trackpads as a stream of tiny deltas (including while two fingers rest for a
    // right-click), so a handler that acts on every event would open popups by accident. This adds the deltas up per control and returns +1 (up) or
    // -1 (down) once they reach one notch (Delta 1.0), else 0. Sideways scrolling (Delta.Y = 0) is always 0. The sum resets after a 300ms pause.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, double[]> wheelSums = new();
    public static int wheelNotch(object sender, PointerWheelEventArgs ea)
    {
        var s = wheelSums.GetValue(sender, _ => new double[2]);   // [sum, timestamp of the last event]
        if (ea.Timestamp - s[1] > 300) s[0] = 0.0;
        s[1] = ea.Timestamp;
        if (ea.Delta.Y == 0.0) return 0;
        if (Math.Sign(s[0]) != Math.Sign(ea.Delta.Y)) s[0] = 0.0;   // changed direction: start over
        s[0] += ea.Delta.Y;
        if (Math.Abs(s[0]) < 1.0) return 0;
        var r = Math.Sign(s[0]);
        s[0] -= r;
        return r;
    }
    // WPF lays text out with Segoe UI's line metrics (ascent 2210/2048 em). Other platforms fall back to fonts with a shorter ascent (macOS Helvetica:
    // 0.77 em), which puts the glyphs higher in their box: about 60px higher for a 200pt numeral. This pads the top by the difference, so the glyphs
    // land where WPF draws them, and keeps it right when FontSize changes. With Segoe UI (Windows) the padding is 0.
    public static void matchWpfTextTop(TemplatedControl c)
    {
        void update()
        {
            var m = new Typeface(c.FontFamily, c.FontStyle, c.FontWeight).GlyphTypeface.Metrics;
            var ascent = -(double)m.Ascent / m.DesignEmHeight;   // (Avalonia's Ascent is negative)
            var p = c.Padding;
            c.Padding = new Thickness(p.Left, Math.Max(0.0, (2210.0 / 2048.0 - ascent) * c.FontSize), p.Right, p.Bottom);
        }
        update();
        c.PropertyChanged += (_, e) => { if (e.Property == TemplatedControl.FontSizeProperty || e.Property == TemplatedControl.FontFamilyProperty) update(); };
    }
    public static void onChecked(ToggleButton tb, Action f) => tb.IsCheckedChanged += (_, _) => { if (tb.IsChecked == true) f(); };
    public static void onUnchecked(ToggleButton tb, Action f) => tb.IsCheckedChanged += (_, _) => { if (tb.IsChecked == false) f(); };

    public static readonly SolidColorBrush almostBlack = freeze(new SolidColorBrush(Color.FromRgb(30, 30, 30)));
    public static readonly SolidColorBrush almostBlackHoverFeedback = freeze(new SolidColorBrush(Color.FromRgb(30, 50, 50)));
    public static Button makeButton(string text, double? fontSizeOpt, IBrush fgOpt)
    {
        var tb = new TextBox { Text = text, IsReadOnly = true, IsHitTestVisible = false, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(0.0), Background = Brushes.Transparent };   // text vertically centered in every tracker button (transparent: the button's own rounded fill shows through)
        if (fontSizeOpt is double x) tb.FontSize = x;
        if (fgOpt != null) tb.Foreground = fgOpt;
        var button = new Button { Content = tb, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        return button;
    }

    // the rounded dark button look used across the app (thin gray border, 4px corners, dark fill, orange border on hover); border/fill come from the Button's own BorderBrush/Background
    // (WPF builds these templates from XAML strings with triggers; here they're code templates that react to the same property changes)
    private static readonly SolidColorBrush hoverOrange = freeze(new SolidColorBrush(Color.Parse("#FFA31A")));
    private static readonly SolidColorBrush pressedFill = freeze(new SolidColorBrush(Color.Parse("#241A0A")));
    public static readonly SolidColorBrush roundedBorderBrush = freeze(new SolidColorBrush(Color.FromRgb(58, 58, 58)));
    // (port helper, not in Graphics.fs: the code button templates set their presenter's Content themselves. A button can be given another template after it
    // has been shown (Layout re-templates the control-panel buttons); the old template's presenter then still holds the content as its visual child, and
    // Avalonia refuses to give the content a second visual parent. A template calls adoptContent when it's built, which releases the content from the
    // button's previous presenter, and its refresh does nothing once isCurrentPresenter is false.)
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ContentControl, ContentPresenter> currentPresenters = new();
    public static void adoptContent(ContentControl owner, ContentPresenter cp)
    {
        if (currentPresenters.TryGetValue(owner, out var old) && old != cp)
        {
            old.Content = null;
            old.UpdateChild();
        }
        currentPresenters.AddOrUpdate(owner, cp);
    }
    public static bool isCurrentPresenter(ContentControl owner, ContentPresenter cp) => currentPresenters.TryGetValue(owner, out var c) && c == cp;
    // (WPF: an IsKeyboardFocused trigger turns the border white. Avalonia has no such property, so the focus is tracked here: only focus that arrived by Tab or the arrow keys counts, as in WPF)
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> keyboardFocused = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();
    public static bool hasKeyboardFocus(Control c) => keyboardFocused.TryGetValue(c, out _);
    public static readonly SolidColorBrush keyboardFocusBorder = new SolidColorBrush(Colors.White);
    // A code template listens to its control's property changes (and focus) to redraw itself. It used to subscribe each time it was built and
    // never unsubscribe, so a re-templated control kept every old template alive and redrawing. This keeps the handlers until the control gets
    // a different template. They stay on while the control is out of the window: a control changed while hidden (for example a Hint Decoder
    // button whose Content is replaced while its popup is closed) must still move the new content into its presenter, or the presenter keeps the
    // old content, which has lost its logical parent, and Avalonia throws when the control is shown again.
    public static void listenWhileShown(TemplatedControl owner, Control root, Action refresh, Func<AvaloniaProperty, bool> watches, bool trackFocus)
    {
        EventHandler<AvaloniaPropertyChangedEventArgs> onProp = (_, ea) => { if (watches(ea.Property)) refresh(); };
        EventHandler<GotFocusEventArgs> onGot = (_, e) =>
        {
            if (e.NavigationMethod == NavigationMethod.Tab || e.NavigationMethod == NavigationMethod.Directional) keyboardFocused.AddOrUpdate(owner, new object());
            refresh();
        };
        EventHandler<RoutedEventArgs> onLost = (_, _) => { keyboardFocused.Remove(owner); refresh(); };
        EventHandler<TemplateAppliedEventArgs> onTemplateApplied = null;
        onTemplateApplied = (_, _) =>
        {
            // a new template took over when this root is no longer the control's visual child: stop listening for good
            if (ReferenceEquals(Avalonia.VisualTree.VisualExtensions.GetVisualParent(root), owner)) return;
            owner.PropertyChanged -= onProp;
            if (trackFocus) { owner.GotFocus -= onGot; owner.LostFocus -= onLost; }
            owner.TemplateApplied -= onTemplateApplied;
        };
        owner.PropertyChanged += onProp;
        if (trackFocus) { owner.GotFocus += onGot; owner.LostFocus += onLost; }
        owner.TemplateApplied += onTemplateApplied;
        // (focus can be cleared while the control is out of the window; drop a stale keyboard-focus mark when it is shown again)
        root.AttachedToVisualTree += (_, _) => { if (!owner.IsFocused && keyboardFocused.TryGetValue(owner, out _)) { keyboardFocused.Remove(owner); refresh(); } };
        refresh();
    }
    public static readonly FuncControlTemplate<Button> roundedButtonTemplate =
        new FuncControlTemplate<Button>((b, _scope) =>
        {
            // (not named PART_ContentPresenter: the Fluent theme restyles a presenter with that name on hover/press)
            var cp = new ContentPresenter();
            adoptContent(b, cp);
            var bd = new Border { Name = "bd", CornerRadius = new CornerRadius(4.0), Child = cp };
            void refresh()
            {
                if (!isCurrentPresenter(b, cp)) return;   // (this template has been replaced; see adoptContent)
                cp.Content = b.Content;
                cp.ContentTemplate = b.ContentTemplate;
                bd.Background = b.IsPressed ? pressedFill : b.Background;
                bd.BorderBrush = hasKeyboardFocus(b) ? keyboardFocusBorder : b.IsPointerOver ? hoverOrange : b.BorderBrush;
                bd.BorderThickness = b.BorderThickness;
                bd.Padding = b.Padding;
                bd.Opacity = b.IsEffectivelyEnabled ? 1.0 : 0.75;
                cp.HorizontalAlignment = b.HorizontalContentAlignment;
                cp.VerticalAlignment = b.VerticalContentAlignment;
            }
            listenWhileShown(b, bd, refresh, p =>
                p == ContentControl.ContentProperty || p == ContentControl.ContentTemplateProperty || p == Button.IsPressedProperty || p == InputElement.IsPointerOverProperty || p == InputElement.IsEffectivelyEnabledProperty
                    || p == TemplatedControl.BackgroundProperty || p == TemplatedControl.BorderBrushProperty || p == TemplatedControl.BorderThicknessProperty
                    || p == TemplatedControl.PaddingProperty || p == ContentControl.HorizontalContentAlignmentProperty || p == ContentControl.VerticalContentAlignmentProperty,
                true);
            return bd;
        });
    // WPF-like slider: a thin grey track line, a small grey thumb, tick marks above and/or below (the Fluent slider is much taller, and its track is nearly invisible on black)
    public static readonly FuncControlTemplate<Slider> wpfSliderTemplate =
        new FuncControlTemplate<Slider>((s, scope) =>
        {
            var gray = new SolidColorBrush(Color.Parse("#8A8A8A"));
            // (as tall as WPF's: the thumb alone, plus 6px for each side that has tick marks)
            var sides = (s.TickPlacement == TickPlacement.Outside ? 2 : (s.TickPlacement == TickPlacement.None ? 0 : 1));
            var rootH = 18.0 + 6.0 * sides;
            var root = new Grid { Background = Brushes.Transparent, Height = rootH };
            root.Children.Add(new Border { Height = 2.0, Background = new SolidColorBrush(Color.Parse("#606060")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5.0, 0.0) });
            if (s.TickPlacement != TickPlacement.None && s.TickFrequency > 0.0 && !double.IsNaN(s.Width) && s.Maximum > s.Minimum)
            {
                var ticks = new Canvas { IsHitTestVisible = false };
                bool top = s.TickPlacement == TickPlacement.TopLeft || s.TickPlacement == TickPlacement.Outside;
                bool bottom = s.TickPlacement == TickPlacement.BottomRight || s.TickPlacement == TickPlacement.Outside;
                int n = (int)Math.Round((s.Maximum - s.Minimum) / s.TickFrequency);
                for (int i = 0; i <= n; i++)
                {
                    double x = 5.0 + (double)i / n * (s.Width - 10.0);
                    if (top) { var t = new Rectangle { Width = 1.0, Height = 4.0, Fill = gray }; Canvas.SetLeft(t, x - 0.5); Canvas.SetTop(t, 2.0); ticks.Children.Add(t); }
                    if (bottom) { var t = new Rectangle { Width = 1.0, Height = 4.0, Fill = gray }; Canvas.SetLeft(t, x - 0.5); Canvas.SetTop(t, rootH - 6.0); ticks.Children.Add(t); }
                }
                root.Children.Add(ticks);
            }
            var emptyTemplate = new FuncControlTemplate<RepeatButton>((_, _) => new Border { Background = Brushes.Transparent });
            var dec = new RepeatButton { Name = "PART_DecreaseButton", Template = emptyTemplate, Focusable = false };
            var inc = new RepeatButton { Name = "PART_IncreaseButton", Template = emptyTemplate, Focusable = false };
            var thumb = new Thumb
            {
                Width = 10.0, Height = 18.0,
                Template = new FuncControlTemplate<Thumb>((_, _) => new Border { Background = new SolidColorBrush(Color.Parse("#B8B8B8")), BorderBrush = new SolidColorBrush(Color.Parse("#E0E0E0")), BorderThickness = new Thickness(1.0), CornerRadius = new CornerRadius(2.0) })
            };
            var track = new Track { Name = "PART_Track", Orientation = Orientation.Horizontal, DecreaseButton = dec, IncreaseButton = inc, Thumb = thumb };
            track.Bind(RangeBase.MinimumProperty, new Avalonia.Data.Binding("Minimum") { Source = s });
            track.Bind(RangeBase.MaximumProperty, new Avalonia.Data.Binding("Maximum") { Source = s });
            track.Bind(RangeBase.ValueProperty, new Avalonia.Data.Binding("Value") { Source = s, Mode = Avalonia.Data.BindingMode.TwoWay });
            scope.Register("PART_DecreaseButton", dec);
            scope.Register("PART_IncreaseButton", inc);
            scope.Register("PART_Track", track);
            root.Children.Add(track);
            return root;
        });
    // rounded checkbox: dark rounded box, orange fill with a dark check when checked, light blue fill with a dark '?' when indeterminate (Random)
    private static readonly string checkMarkGeometry = "M 2,5.5 L 4.6,8.2 L 9,2.5";
    public static readonly FuncControlTemplate<CheckBox> roundedCheckBoxTemplate =
        new FuncControlTemplate<CheckBox>((cb, _scope) =>
        {
            var grid = new Grid { Background = Brushes.Transparent };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            var mark = new Path { Stroke = new SolidColorBrush(Color.Parse("#0C0C0C")), StrokeThickness = 2.0, IsVisible = false,
                                  HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var q = new TextBlock { Text = "?", FontSize = 13.0, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#0C0C0C")), IsVisible = false,
                                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0.0, -2.0, 0.0, 0.0) };
            var markGrid = new Grid();
            markGrid.Children.Add(mark);
            markGrid.Children.Add(q);
            var box = new Border { Name = "box", Width = 14.0, Height = 14.0, CornerRadius = new CornerRadius(3.0), BorderThickness = new Thickness(1.5), Child = markGrid };
            var boxScale = new LayoutTransformControl { Name = "boxScale", Child = box, VerticalAlignment = VerticalAlignment.Center };
            _scope.Register("boxScale", boxScale);
            var cp = new ContentPresenter { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
            Grid.SetColumn(cp, 1);
            grid.Children.Add(boxScale);
            grid.Children.Add(cp);
            var (grey, dark, orange, lightBlue) = (new SolidColorBrush(Color.Parse("#8A8A8A")), new SolidColorBrush(Color.Parse("#171717")), hoverOrange, new SolidColorBrush(Color.Parse("#4FC3F7")));
            void refresh()
            {
                cp.Content = cb.Content;
                cp.ContentTemplate = cb.ContentTemplate;
                cp.Margin = cb.Padding;
                switch (cb.IsChecked)
                {
                    case true:
                        box.Background = orange; box.BorderBrush = orange;
                        mark.Data = Geometry.Parse(checkMarkGeometry); mark.IsVisible = true; q.IsVisible = false;
                        break;
                    case null:
                        box.Background = lightBlue; box.BorderBrush = lightBlue;
                        mark.IsVisible = false; q.IsVisible = true;
                        break;
                    case false:
                        box.Background = dark; box.BorderBrush = grey;
                        mark.IsVisible = false; q.IsVisible = false;
                        break;
                }
                if (cb.IsPointerOver) box.BorderBrush = orange;
                if (hasKeyboardFocus(cb)) box.BorderBrush = keyboardFocusBorder;
                grid.Opacity = cb.IsEffectivelyEnabled ? 1.0 : 0.5;
            }
            listenWhileShown(cb, grid, refresh, p =>
                p == ContentControl.ContentProperty || p == ContentControl.ContentTemplateProperty || p == ToggleButton.IsCheckedProperty || p == InputElement.IsPointerOverProperty || p == InputElement.IsEffectivelyEnabledProperty
                    || p == TemplatedControl.PaddingProperty,
                true);
            return grid;
        });
    // rounded text box: WPF replaces the template; Avalonia's TextBox template has many required parts, so this keeps the
    // theme's template and overrides the colors it reads (dark fill, gray border, orange border on hover and focus, 6px corners)
    public static void styleRoundedTextBox(TextBox t)
    {
        var (fill, border) = (new SolidColorBrush(Color.Parse("#171717")), new SolidColorBrush(Color.Parse("#3A3A3A")));
        foreach (var k in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused" }) t.Resources[k] = fill;
        t.Resources["TextControlBackgroundDisabled"] = fill;   // (WPF's template has no disabled look: a disabled box keeps the dark fill and gray border, e.g. Custom LEVEL Name before its checkbox is ticked)
        t.Resources["TextControlBorderBrushDisabled"] = border;
        t.Resources["TextControlBorderBrush"] = border;
        t.Resources["TextControlBorderBrushPointerOver"] = hoverOrange;
        t.Resources["TextControlBorderBrushFocused"] = hoverOrange;
        t.Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(1.0);
        t.BorderThickness = new Thickness(1.0);
        t.CornerRadius = new CornerRadius(6.0);
        ScrollViewer.SetHorizontalScrollBarVisibility(t, ScrollBarVisibility.Hidden);
        ScrollViewer.SetVerticalScrollBarVisibility(t, ScrollBarVisibility.Hidden);
    }
    public static readonly SolidColorBrush roundedButtonFill = freeze(new SolidColorBrush(Color.FromRgb(23, 23, 23)));
    // setters that give a Button the rounded look; also used inside the dialog-specific button styles that replace the app-wide default
    public static void addRoundedButtonSetters(Style style)
    {
        style.Setters.Add(new Setter(TemplatedControl.TemplateProperty, roundedButtonTemplate));
        style.Setters.Add(new Setter(TemplatedControl.BorderBrushProperty, roundedBorderBrush));
        style.Setters.Add(new Setter(TemplatedControl.BackgroundProperty, roundedButtonFill));
        style.Setters.Add(new Setter(TemplatedControl.ForegroundProperty, Brushes.Orange));
        style.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(0.0)));
        style.Setters.Add(new Setter(Control.FocusAdornerProperty, null));
        style.Setters.Add(new Setter(Visual.RenderTransformProperty, null));   // the Fluent theme shrinks pressed buttons slightly; WPF doesn't
    }
    // a ready-made Button style with the rounded look, for a dialog's own Styles (dialogs whose buttons would otherwise get no fill and no border)
    public static Style roundedButtonStyle()
    {
        var st = new Style(x => x.OfType<Button>());
        addRoundedButtonSetters(st);
        return st;
    }
    // app-wide defaults: every Button and CheckBox gets the rounded look unless a window / panel provides its own style
    // (Avalonia: plus "WPF metrics" for the theme's controls, whose default sizes are much bigger than WPF's; see PROGRESS.md)
    public static void installAppWideStyles()
    {
        var app = Application.Current;
        if (app != null)
        {
            var bs = new Style(x => x.OfType<Button>());
            addRoundedButtonSetters(bs);
            app.Styles.Add(bs);
            var cs = new Style(x => x.OfType<CheckBox>());
            cs.Setters.Add(new Setter(TemplatedControl.TemplateProperty, roundedCheckBoxTemplate));
            cs.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(6.0, 0.0, 0.0, 0.0)));
            cs.Setters.Add(new Setter(TemplatedControl.ForegroundProperty, Brushes.Orange));
            cs.Setters.Add(new Setter(Control.FocusAdornerProperty, null));
            cs.Setters.Add(new Setter(Layoutable.MinHeightProperty, 0.0));
            cs.Setters.Add(new Setter(Layoutable.MinWidthProperty, 0.0));
            app.Styles.Add(cs);
            // button and checkbox labels are read-only, non-clickable TextBoxes: they must not be extra Tab stops (focus landed on them with nothing visible)
            InputElement.GotFocusEvent.AddClassHandler<TextBox>((tb, e) =>
            {
                if (!tb.IsReadOnly || tb.IsHitTestVisible) return;
                // skip past it in the direction the user was tabbing
                var dir = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? NavigationDirection.Previous : NavigationDirection.Next;
                Avalonia.Threading.Dispatcher.UIThread.Post(() => KeyboardNavigationHandler.GetNext(tb, dir)?.Focus());
            });
            // (the Fluent theme recolors a checkbox's text when it's checked, hovered or pressed; WPF's template doesn't)
            foreach (var pseudo in new[] { ":checked", ":unchecked", ":indeterminate", ":pointerover", ":pressed" })
            {
                var csp = new Style(x => x.OfType<CheckBox>().Class(pseudo));
                csp.Setters.Add(new Setter(TemplatedControl.ForegroundProperty, Brushes.Orange));
                app.Styles.Add(csp);
            }
            // WPF metrics: WPF text boxes have no minimum size and only a hairline of padding
            var ts = new Style(x => x.OfType<TextBox>());
            ts.Setters.Add(new Setter(Layoutable.MinHeightProperty, 0.0));
            ts.Setters.Add(new Setter(Layoutable.MinWidthProperty, 0.0));
            ts.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(2.0, 0.0, 2.0, 0.0)));
            // WPF's selection colors, not the system accent (selected text came out black on red); the theme reads this resource for it, so set that too
            // WPF's default TextBox border is a light gray (#ABADB3) that goes light blue on hover and focus; the Fluent Light theme's is nearly invisible, which dropped the white cell lines
            // around every label in the Hint Decoder. Same hover and focus colors as WPF's default template.
            app.Resources["TextControlBorderBrush"] = new SolidColorBrush(Color.Parse("#ABADB3"));
            app.Resources["TextControlBorderBrushPointerOver"] = new SolidColorBrush(Color.Parse("#7EB4EA"));
            app.Resources["TextControlBorderBrushFocused"] = new SolidColorBrush(Color.Parse("#569DE5"));
            app.Resources["TextControlSelectionHighlightColor"] = new SolidColorBrush(Color.Parse("#3399FF"));
            ts.Setters.Add(new Setter(TextBox.SelectionBrushProperty, new SolidColorBrush(Color.Parse("#3399FF"))));
            ts.Setters.Add(new Setter(TextBox.SelectionForegroundBrushProperty, Brushes.White));
            app.Styles.Add(ts);
            // The Fluent TextBox template swaps in its own fill (white in the Light theme) and a system-accent border when the box is hovered or focused,
            // so a box with a dark Background (WPF: Background=Black is all it takes) turned white with orange text the moment it was clicked. Any TextBox whose
            // effective Background is dark keeps that fill in every state and gets WPF's orange hover/focus border. (Boxes that set these resources themselves, e.g. styleRoundedTextBox, are left alone.)
            TemplatedControl.TemplateAppliedEvent.AddClassHandler<TextBox>((tb, _) =>
            {
                // (read-only boxes are the labels; a Transparent or dark one must not turn light gray when the mouse passes over it, e.g. "Auto-save Interval" in Options)
                if (tb.Background is not ISolidColorBrush bg) return;
                var c = bg.Color;
                if ((c.A != 0 && (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) > 80.0) || tb.Resources.ContainsKey("TextControlBackgroundFocused")) return;
                foreach (var k in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "TextControlBackgroundDisabled" }) tb.Resources[k] = bg;
                if (tb.IsReadOnly)
                {
                    // a label keeps its own text color and border too (Fluent's hover state made the text dark and added a border)
                    var fg = tb.Foreground;
                    foreach (var k in new[] { "TextControlForeground", "TextControlForegroundPointerOver", "TextControlForegroundFocused", "TextControlForegroundDisabled" }) tb.Resources[k] = fg;
                    // (border: the same brush in every state, i.e. the theme's idle one, so a label that has a border keeps it and one that has none gains none)
                    // (a Transparent label, e.g. "Auto-save Interval" in Options, shows no border in WPF)
                    IBrush idleBorder = c.A == 0 ? Brushes.Transparent : tb.TryFindResource("TextControlBorderBrush", tb.ActualThemeVariant, out var nb) && nb is IBrush b0 ? b0 : null;
                    if (idleBorder != null)
                        foreach (var k in new[] { "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused", "TextControlBorderBrushDisabled" }) tb.Resources[k] = idleBorder;
                    return;
                }
                tb.Resources["TextControlBorderBrushPointerOver"] = hoverOrange;
                tb.Resources["TextControlBorderBrushFocused"] = hoverOrange;
                tb.Resources["TextControlBorderThemeThicknessFocused"] = tb.BorderThickness;
            });
            // WPF look for sliders (see wpfSliderTemplate)
            var sls = new Style(x => x.OfType<Slider>());
            sls.Setters.Add(new Setter(TemplatedControl.TemplateProperty, wpfSliderTemplate));
            sls.Setters.Add(new Setter(Layoutable.MinHeightProperty, 0.0));
            sls.Setters.Add(new Setter(Layoutable.MinWidthProperty, 0.0));
            app.Styles.Add(sls);
            // (the Fluent theme's own horizontal/vertical slider templates win over the style above, so also set it on each slider as it appears)
            Control.LoadedEvent.AddClassHandler<Slider>((sl, _) =>
            {
                if (sl.Orientation == Orientation.Horizontal && !ReferenceEquals(sl.Template, wpfSliderTemplate)) sl.Template = wpfSliderTemplate;
            });
            // WPF metrics: WPF labels have 5px padding
            var ls = new Style(x => x.OfType<Label>());
            ls.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(5.0)));
            app.Styles.Add(ls);
            // WPF metrics: WPF tab headers are small (12px text, 6,2 padding, no minimum height; Fluent's are 48px tall with 24px text), and a WPF TabControl
            // has a 1px grey border and 2px padding around the selected tab's content (the dungeon tracker's layout depends on both)
            var tis = new Style(x => x.OfType<TabItem>());
            tis.Setters.Add(new Setter(Layoutable.MinHeightProperty, 0.0));
            tis.Setters.Add(new Setter(Layoutable.MinWidthProperty, 0.0));
            tis.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(6.0, 2.0, 6.0, 2.0)));
            tis.Setters.Add(new Setter(TemplatedControl.FontSizeProperty, 12.0));
            // WPF's (Aero2) tab look, which the tracker relies on: boxed tabs with a 1px #ACACAC border and the TabItem's own background;
            // the selected tab is white and raised by 2px on each side; hovering an unselected tab turns it light blue
            tis.Setters.Add(new Setter(TemplatedControl.TemplateProperty, wpfTabItemTemplate));
            app.Styles.Add(tis);
            var tcs = new Style(x => x.OfType<TabControl>());
            tcs.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(2.0)));
            tcs.Setters.Add(new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1.0)));
            tcs.Setters.Add(new Setter(TemplatedControl.BorderBrushProperty, freeze(new SolidColorBrush(Color.FromRgb(0xAC, 0xAC, 0xAC)))));
            tcs.Setters.Add(new Setter(TemplatedControl.TemplateProperty, wpfTabControlTemplate));
            app.Styles.Add(tcs);

        }
    }
    private static readonly IBrush wpfTabBorder = freeze(new SolidColorBrush(Color.FromRgb(0xAC, 0xAC, 0xAC)));
    private static readonly IBrush wpfTabHoverFill = freeze(new SolidColorBrush(Color.FromRgb(0xE5, 0xF1, 0xFB)));
    private static readonly IBrush wpfTabHoverBorder = freeze(new SolidColorBrush(Color.FromRgb(0x7E, 0xB4, 0xEA)));
    // (Avalonia: not in WPF's code; it's WPF's default TabItem look, rebuilt so the dungeon tabs look the same. See installAppWideStyles.)
    public static readonly FuncControlTemplate<TabItem> wpfTabItemTemplate = new FuncControlTemplate<TabItem>((ti, _scope) =>
    {
        var cp = new ContentPresenter { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var bd = new Border { Child = cp, BorderThickness = new Thickness(1, 1, 1, 0) };
        void refresh()
        {
            cp.Content = ti.Header;
            cp.ContentTemplate = ti.HeaderTemplate;
            bd.Padding = ti.Padding;
            if (ti.IsSelected)
            {
                bd.Background = Brushes.White; bd.BorderBrush = wpfTabBorder;
                bd.Margin = new Thickness(-2, -2, -2, -1);   // (and over the content border's top line)
                ti.ZIndex = 1;
            }
            else
            {
                bool hover = ti.IsPointerOver;
                bd.Background = hover ? wpfTabHoverFill : ti.Background;
                bd.BorderBrush = hover ? wpfTabHoverBorder : wpfTabBorder;
                bd.Margin = new Thickness(0, 0, -2, 0);   // (WPF's tabs sit 2px closer than border + padding add up to: a 28px dungeon header makes a 40px tab; found by tests/side-by-side.ps1)
                ti.ZIndex = 0;
            }
            bd.Opacity = ti.IsEffectivelyEnabled ? 1.0 : 0.56;
        }
        listenWhileShown(ti, bd, refresh, p =>
            p == HeaderedContentControl.HeaderProperty || p == HeaderedContentControl.HeaderTemplateProperty || p == TabItem.IsSelectedProperty
                || p == InputElement.IsPointerOverProperty || p == InputElement.IsEffectivelyEnabledProperty || p == TemplatedControl.BackgroundProperty
                || p == TemplatedControl.PaddingProperty,
            false);
        return bd;
    });
    // (Avalonia: WPF's default TabControl shape. The tab strip sits above the content with a 2,2,2,0 margin, and only the content has the
    // border and padding; Avalonia's theme draws the border around the tabs too.)
    public static readonly FuncControlTemplate<TabControl> wpfTabControlTemplate = new FuncControlTemplate<TabControl>((tc, scope) =>
    {
        var items = new ItemsPresenter { Name = "PART_ItemsPresenter", Margin = new Thickness(2, 2, 2, 0), ZIndex = 1 };
        items.ItemsPanel = tc.ItemsPanel;
        scope.Register("PART_ItemsPresenter", items);
        DockPanel.SetDock(items, Dock.Top);
        var host = new ContentPresenter { Name = "PART_SelectedContentHost" };
        scope.Register("PART_SelectedContentHost", host);
        var contentBorder = new Border { Child = host };
        void refresh()
        {
            host.Content = tc.SelectedContent;
            host.ContentTemplate = tc.SelectedContentTemplate;
            contentBorder.Background = tc.Background;
            contentBorder.BorderBrush = tc.BorderBrush;
            contentBorder.BorderThickness = tc.BorderThickness;
            contentBorder.Padding = tc.Padding;
        }
        var dp = new DockPanel();
        dp.Children.Add(items);
        dp.Children.Add(contentBorder);
        listenWhileShown(tc, dp, refresh, p =>
            p == TabControl.SelectedContentProperty || p == TabControl.SelectedContentTemplateProperty || p == TemplatedControl.BackgroundProperty
                || p == TemplatedControl.BorderBrushProperty || p == TemplatedControl.BorderThicknessProperty || p == TemplatedControl.PaddingProperty,
            false);
        return dp;
    });
    public static void styleRoundedButton(Button b)
    {
        b.Template = roundedButtonTemplate;
        b.BorderBrush = roundedBorderBrush;
        b.Background = freeze(new SolidColorBrush(Color.FromRgb(23, 23, 23)));
        switch (b.Content)
        {
            case TextBox t: t.Background = Brushes.Transparent; break;
            case Canvas c: c.Background = Brushes.Transparent; break;
            default: break;
        }
    }

    public static Color makeColor(int rgb)
    {
        var r = (rgb & 0xFF0000) / 0x10000;
        var g = (rgb & 0x00FF00) / 0x100;
        var b = (rgb & 0x0000FF) / 0x1;
        return Color.FromRgb((byte)r, (byte)g, (byte)b);
    }

    public static bool isBlackGoodContrast(int rgb)
    {
        // https://gamedev.stackexchange.com/questions/38536/given-a-rgb-color-x-how-to-find-the-most-contrasting-color-y
        var ri = (rgb & 0xFF0000) / 0x10000;
        var gi = (rgb & 0x00FF00) / 0x100;
        var bi = (rgb & 0x0000FF) / 0x1;
        var r = (double)ri / 255.0;
        var g = (double)gi / 255.0;
        var b = (double)bi / 255.0;
        var l = 0.2126 * r * r + 0.7152 * g * g + 0.0722 * b * b;
        var use_black = l > 0.5 * 0.5;
        return use_black;  // else use white
    }

    public static System.Drawing.Bitmap transformColor(System.Drawing.Bitmap bmp, Func<System.Drawing.Color, System.Drawing.Color> f)
    {
        var r = new System.Drawing.Bitmap(bmp.Width, bmp.Height);
        for (int px = 0; px < bmp.Width; px++)
            for (int py = 0; py < bmp.Height; py++)
            {
                var c = bmp.GetPixel(px, py);
                r.SetPixel(px, py, f(c));
            }
        return r;
    }

    public static System.Drawing.Bitmap greyscale(System.Drawing.Bitmap bmp) =>
        transformColor(bmp, c =>
        {
            if (c.A == 0)   // (any fully transparent pixel, not just exactly Color.Transparent: PNG-decoded art such as the boots has 0x00000000 pixels, which became opaque black)
                return c;
            else
            {
                var avg = ((int)c.R + (int)c.G + (int)c.B) / 5;  // not just average, but overall darker
                avg = avg == 0 ? 0 : avg + 60;                   // never too dark
                return System.Drawing.Color.FromArgb(c.A, avg, avg, avg);
            }
        });
    public static System.Drawing.Color desaturateColor(System.Drawing.Color c, double pct)
    {
        var f = pct;   // 0.60 // desaturate by 60%
        var L = 0.3 * c.R + 0.6 * c.G + 0.1 * c.B;
        var newR = c.R + f * (L - c.R);
        var newG = c.G + f * (L - c.G);
        var newB = c.B + f * (L - c.B);
        return System.Drawing.Color.FromArgb((int)newR, (int)newG, (int)newB);
    }
    public static System.Drawing.Bitmap desaturate(System.Drawing.Bitmap bmp, double pct) => transformColor(bmp, c => desaturateColor(c, pct));
    public static System.Drawing.Bitmap darkenImpl(double pct, System.Drawing.Bitmap bmp)
    {
        var r = new System.Drawing.Bitmap(bmp.Width, bmp.Height);
        for (int px = 0; px < bmp.Width; px++)
            for (int py = 0; py < bmp.Height; py++)
            {
                var c = bmp.GetPixel(px, py);
                c = System.Drawing.Color.FromArgb((int)(c.R * pct), (int)(c.G * pct), (int)(c.B * pct));
                r.SetPixel(px, py, c);
            }
        return r;
    }
    public static System.Drawing.Bitmap darken(System.Drawing.Bitmap bmp) =>
        darkenImpl(0.5, bmp);
    public static Color mediaColor(System.Drawing.Color c) =>
        Color.FromArgb(c.A, c.R, c.G, c.B);

    // (WPF: the display's DpiScale, needed to undo WPF's dpi scaling of bitmaps. Avalonia shows bitmaps at 96 dpi, 1 pixel per unit, so it's informational only.)
    public static double? dpi = null;
    public static Image BItoImage(Avalonia.Media.Imaging.Bitmap bi) =>
        new Image { Source = bi };
    // a bitmap Avalonia can draw (WPF: a frozen BitmapImage)
    public static Avalonia.Media.Imaging.Bitmap BMPToBI(System.Drawing.Bitmap b)
    {
        var wb = new Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(b.Width, b.Height), new Vector(96.0, 96.0), Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Unpremul);
        using (var fb = wb.Lock())
        {
            var row = new int[b.Width];
            var px = b.Pixels;
            for (int y = 0; y < b.Height; y++)
            {
                for (int x = 0; x < b.Width; x++)
                    row[x] = (int)px[y * b.Width + x];   // ARGB as a little-endian int is BGRA in memory
                System.Runtime.InteropServices.Marshal.Copy(row, 0, fb.Address + y * fb.RowBytes, b.Width);
            }
        }
        return wb;
    }
    public static Image BMPtoImage(System.Drawing.Bitmap bmp)
    {
        var i = new Image();
        i.Source = BMPToBI(bmp);
        i.Height = (double)bmp.Height;
        i.Width = (double)bmp.Width;
        return i;
    }

    public static readonly double OMTW = 48.0;  // overworld map tile width - at normal aspect ratio, is 48 (16*3)
    public static readonly SolidColorBrush green = freeze(new SolidColorBrush(mediaColor(desaturateColor(System.Drawing.Color.Lime, 0.50))));
    public static readonly SolidColorBrush cyan = freeze(new SolidColorBrush(mediaColor(desaturateColor(System.Drawing.Color.FromArgb(0xFF, 0, 0xFF, 0xFF), 0.30))));
    public static readonly SolidColorBrush yellow = freeze(new SolidColorBrush(mediaColor(desaturateColor(System.Drawing.Color.Yellow, 0.50))));
    public static readonly SolidColorBrush red = freeze(new SolidColorBrush(mediaColor(desaturateColor(System.Drawing.Color.Red, 0.50))));
    public static readonly SolidColorBrush palegreen = freeze(new SolidColorBrush(mediaColor(desaturateColor(System.Drawing.Color.Lime, 0.65))));
    public static readonly SolidColorBrush paleyellow = freeze(new SolidColorBrush(mediaColor(desaturateColor(System.Drawing.Color.Yellow, 0.65))));
    public static readonly SolidColorBrush palered = freeze(new SolidColorBrush(mediaColor(desaturateColor(System.Drawing.Color.Red, 0.65))));
    public class TileHighlightRectangle
    {
        private readonly Rectangle s = new Rectangle { Width = OMTW, Height = 11.0 * 3.0, Stroke = Brushes.Lime, StrokeThickness = 3.0, Opacity = 0.0, IsHitTestVisible = false };
        // WPF runs a 0.5s DoubleAnimation (stroke 12 -> 4) and ColorAnimation (cyan -> color); Avalonia can't animate a
        // shape's stroke brush color with its animation system, so this tweens both by hand on a timer
        private DispatcherTimer anim = null;
        private void stopAnim()
        {
            if (anim != null)
            {
                anim.Stop();
                anim = null;
            }
        }
        private void startAnim(Color toColor)
        {
            stopAnim();
            var brush = new SolidColorBrush(Colors.Cyan);
            s.Stroke = brush;
            s.StrokeThickness = 12.0;
            var start = DateTime.Now;
            byte lerp(byte a, byte b, double t) => (byte)((double)a + ((double)b - (double)a) * t);
            var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16.0), DispatcherPriority.Render, (_, _) => { });
            timer.Tick += (_, _) =>
            {
                var t = Math.Min(1.0, (DateTime.Now - start).TotalSeconds / 0.5);
                s.StrokeThickness = 12.0 + (4.0 - 12.0) * t;
                brush.Color = Color.FromRgb(lerp(Colors.Cyan.R, toColor.R, t), lerp(Colors.Cyan.G, toColor.G, t), lerp(Colors.Cyan.B, toColor.B, t));
                if (t >= 1.0) stopAnim();
            };
            anim = timer;
            timer.Start();
        }
        private void Draw(bool isPale)
        {
            stopAnim();
            s.Opacity = 1.0;
            s.StrokeThickness = isPale ? 2.0 : 4.0;
        }
        public void MakeRed() { s.Stroke = red; Draw(false); }
        public void MakeYellow() { s.Stroke = yellow; Draw(false); }
        public void MakeBoldGreen() { s.Stroke = green; Draw(false); s.StrokeThickness = 6.0; }
        public void MakeCyan() { s.Stroke = cyan; Draw(false); }
        public void MakeGreen() { s.Stroke = green; Draw(false); }
        public void MakePaleRed() { s.Stroke = palered; Draw(true); }
        public void MakePaleYellow() { s.Stroke = paleyellow; Draw(true); }
        public void MakePaleGreen() { s.Stroke = palegreen; Draw(true); }
        public void MakeMagenta() { stopAnim(); s.Stroke = Brushes.Magenta; s.Opacity = 0.85; s.StrokeThickness = 5.0; }
        public void Hide() { stopAnim(); s.Opacity = 0.0; }
        public void MakeGreenWithBriefAnimation()
        {
            if (TrackerModelOptions.AnimateShopHighlights.Value)
            {
                Draw(false);
                startAnim(Colors.Lime);
            }
            else
                this.MakeGreen();
        }
        public void MakeYellowWithBriefAnimation()
        {
            if (TrackerModelOptions.AnimateShopHighlights.Value)
            {
                Draw(false);
                startAnim(Colors.Yellow);
            }
            else
                this.MakeYellow();
        }
        public void MakeRedWithBriefAnimation()
        {
            if (TrackerModelOptions.AnimateShopHighlights.Value)
            {
                Draw(false);
                startAnim(Colors.Red);
            }
            else
                this.MakeRed();
        }
        public Rectangle Shape => s;
    }

    // see also
    // https://stackoverflow.com/questions/63184765/wpf-left-click-and-drag
    // https://stackoverflow.com/questions/12802122/wpf-handle-drag-and-drop-as-well-as-left-click
    public class DragDropSurface<T> // surface is the entire element we can drag across that encompasses all the multiple items and sees all mouse interactions in the area
    {
        private readonly Control surface;
        private bool isDragging = false;
        private Point? startPoint = null;
        private Action<PointerReleasedEventArgs> clickFunc = null;
        private Action<T> initiateDragFunc = null;
        public DragDropSurface(Control surface, Action<PointerEventArgs, Action<T>> onStartDrag)
        {
            this.surface = surface;
            // (WPF PreviewMouseMove: a tunnelling handler)
            surface.AddHandler(InputElement.PointerMovedEvent, new EventHandler<PointerEventArgs>((_, ea) =>
            {
                var props = ea.GetCurrentPoint(surface).Properties;
                if ((props.IsLeftButtonPressed ||
                     props.IsMiddleButtonPressed ||
                     props.IsRightButtonPressed)
                        && !isDragging && startPoint.HasValue)
                {
                    var pos = ea.GetPosition(surface);
                    // This is the windows setting for click v drag
                    //     if Math.Abs(pos.X - startPoint.Value.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(pos.Y - startPoint.Value.Y) > SystemParameters.MinimumVerticalDragDistance then
                    // However it defaults to just 4 pixels, and some folks were inadvertantly dragging when they wanted to click.
                    // The only place setupClickVersusDrag() is called is for 'painting rooms' in dungeon UI.  Intentional drag-painting
                    // requires crossing to a different room (each room is ~39px wide), so 20px gives plenty of margin over hand tremor
                    // (~5-8px) while still triggering well before the user reaches the next room.  The old 12px threshold was measured
                    // from the click point (not the room edge), so even a small wobble mid-click could silently swallow the click.
                    if (Math.Abs(pos.X - startPoint.Value.X) > 20.0 || Math.Abs(pos.Y - startPoint.Value.Y) > 20.0)
                    {
                        //printfn "%A  -  %A" startPoint pos
                        isDragging = true;
                        try { onStartDrag(ea, initiateDragFunc); }
                        finally
                        {
                            // (always reset: a failed drag must not leave every later click ignored)
                            isDragging = false;
                            startPoint = null;
                            clickFunc = null;
                            initiateDragFunc = null;
                        }
                    }
                }
            }), RoutingStrategies.Tunnel);
            surface.PointerReleased += (_, ea) =>
            {
                //printfn "MouseUp"
                if (!isDragging)
                {
                    if (clickFunc != null)
                        clickFunc(ea);
                    // else e.g. dragged into and released
                }
                startPoint = null;
                clickFunc = null;
                initiateDragFunc = null;
            };
        }
        public void RegisterClickable(Control e, Action<PointerReleasedEventArgs> onClick, Action<T> onInitiateDrag)     // each individual element says how it should respond to a click, or to being an initiator of a drag
        {
            // (WPF PreviewMouseDown: a tunnelling handler)
            e.AddHandler(InputElement.PointerPressedEvent, new EventHandler<PointerPressedEventArgs>((_, ea) =>
            {
                //printfn "PrevMD %A" (Input.Mouse.GetPosition(surface))
                startPoint = ea.GetPosition(surface);
                clickFunc = onClick;
                initiateDragFunc = onInitiateDrag;
            }), RoutingStrategies.Tunnel);
        }
    }

    ////////////////////////////////////////////////////

    private static System.Drawing.Bitmap make_alphaNumBmp()
    {
        var imageStream = GetResourceStream("alphanumerics3x5.png");
        return new System.Drawing.Bitmap(imageStream);
    }
    public static readonly System.Drawing.Bitmap alphaNumBmp = make_alphaNumBmp();
    public static void paintAlphanumerics3x5(char ch, System.Drawing.Color color, System.Drawing.Bitmap bmp, int x, int y)  // x and y are 1-pixel coordinates, even though bmp is blown up 3x
    {
        var index =
            ch switch
            {
                '1' => 0,
                '2' => 1,
                '3' => 2,
                '4' => 3,
                '5' => 4,
                '6' => 5,
                '7' => 6,
                '8' => 7,
                '9' => 8,
                '?' => 9,
                'A' => 10,
                'B' => 11,
                'C' => 12,
                'D' => 13,
                'E' => 14,
                'F' => 15,
                'G' => 16,
                'H' => 17,
                'S' => 18, // sword
                'L' => 19, // ladder
                'R' => 20, // robot armos
                'T' => 21, // T for take any
                _ => throw new Exception("bad alphanumeric character to paint"),
            };
        for (int i = 0; i <= 2; i++)
            for (int j = 0; j <= 4; j++)
                if (alphaNumBmp.GetPixel(index * 3 + i, j).ToArgb() == System.Drawing.Color.Black.ToArgb())
                    // blow it up 3x
                    for (int dx = 0; dx <= 2; dx++)
                        for (int dy = 0; dy <= 2; dy++)
                            bmp.SetPixel(3 * (x + i) + dx, 3 * (y + j) + dy, color);
    }

    public static System.Drawing.Bitmap alphaNumOnTransparentBmp(char ch, System.Drawing.Color color, int w, int h, int x, int y)
    {
        var r = new System.Drawing.Bitmap(w, h);
        for (int px = 0; px < w; px++)
            for (int py = 0; py < h; py++)
                r.SetPixel(px, py, System.Drawing.Color.Transparent);
        paintAlphanumerics3x5(ch, color, r, x, y);
        return r;
    }

    private static System.Drawing.Bitmap make_openCaveIconBmp()
    {
        var imageStream = GetResourceStream("open_cave20x20.png");
        var bmp = new System.Drawing.Bitmap(imageStream);
        return bmp;
    }
    public static readonly System.Drawing.Bitmap openCaveIconBmp = make_openCaveIconBmp();

    private static System.Drawing.Bitmap[] make_icons8x8()
    {
        var imageStream = GetResourceStream("icons8x8.png");
        var bmp = new System.Drawing.Bitmap(imageStream);
        var a = new List<System.Drawing.Bitmap>();
        for (int i = 0; i < bmp.Width / 8; i++)
        {
            var r = new System.Drawing.Bitmap(8, 8);
            for (int px = 0; px < 8; px++)
                for (int py = 0; py < 8; py++)
                {
                    var color = bmp.GetPixel(px + i * 8, py);
                    //let color = if color.ToArgb() = System.Drawing.Color.Black.ToArgb() then System.Drawing.Color.Transparent else color
                    r.SetPixel(px, py, color);
                }
            a.Add(r);
        }
        return a.ToArray();
    }
    private static readonly System.Drawing.Bitmap[] _icons8x8 = make_icons8x8();
    public static readonly System.Drawing.Bitmap filled_small_heart_bmp = _icons8x8[0];
    public static readonly System.Drawing.Bitmap empty_small_heart_bmp = _icons8x8[1];

    private static System.Drawing.Bitmap[] make_icons7x7()
    {
        var imageStream = GetResourceStream("icons7x7.png");
        var bmp = new System.Drawing.Bitmap(imageStream);
        var a = new List<System.Drawing.Bitmap>();
        for (int i = 0; i < bmp.Width / 7; i++)
        {
            var r = new System.Drawing.Bitmap(7 * 3, 7 * 3);
            for (int px = 0; px < 7 * 3; px++)
                for (int py = 0; py < 7 * 3; py++)
                {
                    var color = bmp.GetPixel(px / 3 + i * 7, py / 3);
                    color = color.ToArgb() == System.Drawing.Color.Black.ToArgb() ? System.Drawing.Color.Transparent : color;
                    r.SetPixel(px, py, color);
                }
            a.Add(r);
        }
        return a.ToArray();
    }
    private static readonly System.Drawing.Bitmap[] _icons7x7 = make_icons7x7();
    public static readonly System.Drawing.Bitmap boomerang_bmp = _icons7x7[0];
    public static readonly System.Drawing.Bitmap bow_bmp = _icons7x7[1];
    public static readonly System.Drawing.Bitmap magic_boomerang_bmp = _icons7x7[2];
    public static readonly System.Drawing.Bitmap raft_bmp = _icons7x7[3];
    public static readonly System.Drawing.Bitmap ladder_bmp = _icons7x7[4];
    public static readonly System.Drawing.Bitmap recorder_bmp = _icons7x7[5];
    public static readonly System.Drawing.Bitmap wand_bmp = _icons7x7[6];
    public static readonly System.Drawing.Bitmap red_candle_bmp = _icons7x7[7];
    public static readonly System.Drawing.Bitmap book_bmp = _icons7x7[8];
    public static readonly System.Drawing.Bitmap key_bmp = _icons7x7[9];
    public static readonly System.Drawing.Bitmap silver_arrow_bmp = _icons7x7[10];
    public static readonly System.Drawing.Bitmap wood_arrow_bmp = _icons7x7[11];
    public static readonly System.Drawing.Bitmap red_ring_bmp = _icons7x7[12];
    public static readonly System.Drawing.Bitmap magic_shield_bmp = _icons7x7[13];
    public static readonly System.Drawing.Bitmap boom_book_bmp = _icons7x7[14];
    public static readonly System.Drawing.Bitmap heart_container_bmp = _icons7x7[15];
    public static readonly System.Drawing.Bitmap power_bracelet_bmp = _icons7x7[16];
    public static readonly System.Drawing.Bitmap white_sword_bmp = _icons7x7[17];
    public static readonly System.Drawing.Bitmap ow_key_armos_bmp = _icons7x7[18];
    public static readonly System.Drawing.Bitmap brown_sword_bmp = _icons7x7[19];
    public static readonly System.Drawing.Bitmap magical_sword_bmp = _icons7x7[20];
    public static readonly System.Drawing.Bitmap blue_candle_bmp = _icons7x7[21];
    public static readonly System.Drawing.Bitmap blue_ring_bmp = _icons7x7[22];
    public static readonly System.Drawing.Bitmap ganon_bmp = _icons7x7[23];
    public static readonly System.Drawing.Bitmap zelda_bmp = _icons7x7[24];
    public static readonly System.Drawing.Bitmap bomb_bmp = _icons7x7[25];
    public static readonly System.Drawing.Bitmap bow_and_arrow_bmp = _icons7x7[26];
    public static readonly System.Drawing.Bitmap bait_bmp = _icons7x7[27];
    public static readonly System.Drawing.Bitmap question_marks_bmp = _icons7x7[28];
    public static readonly System.Drawing.Bitmap rupee_bmp = _icons7x7[29];
    public static readonly System.Drawing.Bitmap basement_stair_bmp = _icons7x7[30];
    public static readonly System.Drawing.Bitmap ws_ms_bomb_upgrade_bmp = _icons7x7[31];

    public static readonly System.Drawing.Color bg16x16 = System.Drawing.Color.FromArgb(45, 50, 00);
    private static System.Drawing.Bitmap[] make_bosses16x16()
    {
        var imageStream = GetResourceStream("zelda_bosses16x16.png");
        var bmp = new System.Drawing.Bitmap(imageStream);
        var a = new List<System.Drawing.Bitmap>();
        for (int i = 0; i < bmp.Width / 16; i++)
        {
            var r = new System.Drawing.Bitmap(18, 18);  // border around it
            for (int px = 0; px <= 17; px++)
                for (int py = 0; py <= 17; py++)
                {
                    if (px == 0 || px == 17 || py == 0 || py == 17)
                        r.SetPixel(px, py, bg16x16);
                    else
                        r.SetPixel(px, py, System.Drawing.Color.Black);
                }
            for (int px = 0; px <= 15; px++)
                for (int py = 0; py <= 15; py++)
                {
                    var color = bmp.GetPixel(px + i * 16, py);
                    if (color.ToArgb() == System.Drawing.Color.Black.ToArgb()) { } else r.SetPixel(px + 1, py + 1, color);
                }
            a.Add(r);
        }
        return a.ToArray();
    }
    private static readonly System.Drawing.Bitmap[] _bosses16x16 = make_bosses16x16();
    public static readonly System.Drawing.Bitmap digdogger_bmp = _bosses16x16[0];
    public static readonly System.Drawing.Bitmap gleeok_bmp = _bosses16x16[1];
    public static readonly System.Drawing.Bitmap gohma_bmp = _bosses16x16[2];
    public static readonly System.Drawing.Bitmap manhandla_bmp = _bosses16x16[3];
    public static readonly System.Drawing.Bitmap wizzrobe_bmp = _bosses16x16[4];
    public static readonly System.Drawing.Bitmap patra_bmp = _bosses16x16[5];
    public static readonly System.Drawing.Bitmap dodongo_bmp = _bosses16x16[6];
    public static readonly System.Drawing.Bitmap red_bubble_bmp = _bosses16x16[7];
    public static readonly System.Drawing.Bitmap blue_bubble_bmp = _bosses16x16[8];
    public static readonly System.Drawing.Bitmap blue_darknut_bmp = _bosses16x16[9];
    public static readonly System.Drawing.Bitmap other_monster_bmp = _bosses16x16[10];
    public static readonly System.Drawing.Bitmap old_man_bmp = _bosses16x16[11];
    public static readonly System.Drawing.Bitmap vire_bmp = _bosses16x16[12];
    public static readonly System.Drawing.Bitmap zol_bmp = _bosses16x16[13];
    public static readonly System.Drawing.Bitmap pols_voice_bmp = _bosses16x16[14];
    public static readonly System.Drawing.Bitmap red_tektite = _bosses16x16[15];
    public static readonly System.Drawing.Bitmap red_goriya = _bosses16x16[16];
    public static readonly System.Drawing.Bitmap rope = _bosses16x16[17];
    public static readonly System.Drawing.Bitmap stalfos = _bosses16x16[18];
    public static readonly System.Drawing.Bitmap wallmaster = _bosses16x16[19];
    public static readonly System.Drawing.Bitmap gel = _bosses16x16[20];
    public static readonly System.Drawing.Bitmap keese = _bosses16x16[21];
    public static readonly System.Drawing.Bitmap likelike = _bosses16x16[22];
    public static readonly System.Drawing.Bitmap gibdo = _bosses16x16[23];
    public static readonly System.Drawing.Bitmap red_lynel = _bosses16x16[24];
    public static readonly System.Drawing.Bitmap blue_moblin = _bosses16x16[25];
    public static readonly System.Drawing.Bitmap aquamentus = _bosses16x16[26];
    public static readonly System.Drawing.Bitmap blue_lanmola = _bosses16x16[27];
    public static readonly System.Drawing.Bitmap moldorm = _bosses16x16[28];
    public static readonly System.Drawing.Bitmap rupee_boss = _bosses16x16[29];
    public static readonly System.Drawing.Bitmap traps = _bosses16x16[30];
    public static readonly System.Drawing.Bitmap other_monster2 = _bosses16x16[31];

    private static System.Drawing.Bitmap[] make_items16x16()
    {
        var imageStream = GetResourceStream("zelda_items16x16.png");
        var bmp = new System.Drawing.Bitmap(imageStream);
        var a = new List<System.Drawing.Bitmap>();
        for (int i = 0; i < bmp.Width / 16; i++)
        {
            var r = new System.Drawing.Bitmap(18, 18);  // border around it
            for (int px = 0; px <= 17; px++)
                for (int py = 0; py <= 17; py++)
                {
                    if (px == 0 || px == 17 || py == 0 || py == 17)
                        r.SetPixel(px, py, bg16x16);
                    else
                        r.SetPixel(px, py, System.Drawing.Color.Black);
                }
            for (int px = 0; px <= 15; px++)
                for (int py = 0; py <= 15; py++)
                {
                    var color = bmp.GetPixel(px + i * 16, py);
                    if (color.ToArgb() == System.Drawing.Color.Black.ToArgb()) { } else r.SetPixel(px + 1, py + 1, color);
                }
            a.Add(r);
        }
        return a.ToArray();
    }
    private static readonly System.Drawing.Bitmap[] _items16x16 = make_items16x16();
    public static readonly System.Drawing.Bitmap zi_triforce_bmp = _items16x16[0];
    public static readonly System.Drawing.Bitmap zi_heart_bmp = _items16x16[1];
    public static readonly System.Drawing.Bitmap zi_bomb_bmp = _items16x16[2];
    public static readonly System.Drawing.Bitmap zi_key_bmp = _items16x16[3];
    public static readonly System.Drawing.Bitmap zi_fiver_bmp = _items16x16[4];
    public static readonly System.Drawing.Bitmap zi_map_bmp = _items16x16[5];
    public static readonly System.Drawing.Bitmap zi_compass_bmp = _items16x16[6];
    public static readonly System.Drawing.Bitmap zi_other_item_bmp = _items16x16[7];
    public static readonly System.Drawing.Bitmap zi_alt_bomb_bmp = _items16x16[8];
    public static readonly System.Drawing.Bitmap zi_rock = _items16x16[9];
    public static readonly System.Drawing.Bitmap zi_tree = _items16x16[10];
    public static readonly System.Drawing.Bitmap zi_atlas = _items16x16[11];
    public static readonly System.Drawing.Bitmap zi_staircase = _items16x16[12];

    //let (placeholder_row0_bmp, placeholder_row1_bmp, placeholder_row2_bmp, placeholder_row3_bmp, placeholder_row4_bmp, placeholder_row5_bmp, placeholder_row6_bmp, placeholder_row7_bmp) =
    //    let imageStream = GetResourceStream("zelda_items16x16.png")
    //    let bmp = new System.Drawing.Bitmap(imageStream)
    //    let a = [|
    //        for i = 0 to bmp.Width/16 - 1 do
    //            let r = new System.Drawing.Bitmap(18,18)  // border around it
    //            for px = 0 to 17 do
    //                for py = 0 to 17 do
    //                    if px=0 || px=17 || py=0 || py=17 then
    //                        r.SetPixel(px, py, bg16x16)
    //                    else
    //                        r.SetPixel(px, py, System.Drawing.Color.Black)
    //            for px = 0 to 15 do
    //                for py = 0 to 15 do
    //                    let color = bmp.GetPixel(px + i*16, py)
    //                    if color.ToArgb() = System.Drawing.Color.Black.ToArgb() then () else r.SetPixel(px+1, py+1, color)
    //            yield r
    //    |]
    //    (a.[0], a.[1], a.[2], a.[3], a.[4], a.[5], a.[6], a.[7])
    public static System.Drawing.Bitmap unborder(System.Drawing.Bitmap bmp)
    {
        var r = new System.Drawing.Bitmap(bmp.Width - 2, bmp.Height - 2);
        for (int i = 0; i < r.Width; i++)
            for (int j = 0; j < r.Height; j++)
                r.SetPixel(i, j, bmp.GetPixel(i + 1, j + 1));
        return r;
    }
    public static readonly Avalonia.Media.Imaging.Bitmap zi_staircase_unborderedBI = BMPToBI(unborder(zi_staircase));

    private static System.Drawing.Bitmap[] make_icons10x10()
    {
        var imageStream = GetResourceStream("icons10x10.png");
        var bmp = new System.Drawing.Bitmap(imageStream);
        var allList = new List<System.Drawing.Bitmap>();
        for (int i = 0; i < bmp.Width / 10; i++)
        {
            var r = new System.Drawing.Bitmap(10 * 3, 10 * 3);
            for (int px = 0; px < 10 * 3; px++)
                for (int py = 0; py < 10 * 3; py++)
                {
                    var color = bmp.GetPixel(px / 3 + i * 10, py / 3);
                    color = color.ToArgb() == System.Drawing.Color.White.ToArgb() ? System.Drawing.Color.Transparent : color;
                    r.SetPixel(px, py, color);
                }
            allList.Add(r);
        }
        var all = allList.ToArray();
        return new[] {
            transformColor(all[1], c => c.ToArgb() != System.Drawing.Color.Transparent.ToArgb() ? System.Drawing.Color.LightGray : c),
            all[1],
            transformColor(all[1], c => c.ToArgb() != System.Drawing.Color.Transparent.ToArgb() ? desaturateColor(c, 0.25) : c),
            all[0], all[2], all[3], all[4], all[5], all[6], all[7], all[8], all[9] };
    }
    private static readonly System.Drawing.Bitmap[] _icons10x10 = make_icons10x10();
    public static readonly System.Drawing.Bitmap _brightTriforce_bmp = _icons10x10[0];
    public static readonly System.Drawing.Bitmap fullOrangeTriforce_bmp = _icons10x10[1];
    public static readonly System.Drawing.Bitmap _dullOrangeTriforce_bmp = _icons10x10[2];
    public static readonly System.Drawing.Bitmap greyTriforce_bmp = _icons10x10[3];
    public static readonly System.Drawing.Bitmap owHeartSkipped_bmp = _icons10x10[4];
    public static readonly System.Drawing.Bitmap owHeartEmpty_bmp = _icons10x10[5];
    public static readonly System.Drawing.Bitmap owHeartFull_bmp = _icons10x10[6];
    public static readonly System.Drawing.Bitmap iconRightArrow_bmp = _icons10x10[7];
    public static readonly System.Drawing.Bitmap iconCheckMark_bmp = _icons10x10[8];
    public static readonly System.Drawing.Bitmap iconExtras_bmp = _icons10x10[9];
    public static readonly System.Drawing.Bitmap iconDisk_bmp = _icons10x10[10];
    public static readonly System.Drawing.Bitmap owHeartTallFull_bmp = _icons10x10[11];
    public static readonly System.Drawing.Color UNFOUND_NUMERAL_COLOR = System.Drawing.Color.FromArgb(0x77, 0x77, 0x99);
    public static readonly System.Drawing.Color FOUND_NUMERAL_COLOR = System.Drawing.Color.White;
    private static (System.Drawing.Bitmap, System.Drawing.Bitmap, System.Drawing.Bitmap, System.Drawing.Bitmap, System.Drawing.Bitmap[], System.Drawing.Bitmap[]) make_heartFroms()
    {
        var LABEL = System.Drawing.Color.FromArgb(0xFF, 0xFF, 0xFF);
        System.Drawing.Bitmap paint(char ch)
        {
            var bmp = (System.Drawing.Bitmap)owHeartTallFull_bmp.Clone();
            paintAlphanumerics3x5(ch, LABEL, bmp, 4, 2);
            return bmp;
        }
        var a = new List<System.Drawing.Bitmap[]>();
        foreach (var ch in new[] { "123456789", "ABCDEFGH9" })
        {
            var inner = new List<System.Drawing.Bitmap>();
            for (int i = 0; i <= 8; i++)
                inner.Add(paint(ch[i]));
            a.Add(inner.ToArray());
        }
        return (paint('T'), paint('S'), paint('L'), paint('R'), a[0], a[1]);
    }
    private static readonly (System.Drawing.Bitmap, System.Drawing.Bitmap, System.Drawing.Bitmap, System.Drawing.Bitmap, System.Drawing.Bitmap[], System.Drawing.Bitmap[]) _heartFroms = make_heartFroms();
    public static readonly System.Drawing.Bitmap heartFromTakeAny_bmp = _heartFroms.Item1;
    public static readonly System.Drawing.Bitmap heartFromWhiteSwordCave_bmp = _heartFroms.Item2;
    public static readonly System.Drawing.Bitmap heartFromCoast_bmp = _heartFroms.Item3;
    public static readonly System.Drawing.Bitmap heartFromArmos_bmp = _heartFroms.Item4;
    public static readonly System.Drawing.Bitmap[] heartFromNumberedDungeon_bmps = _heartFroms.Item5;
    public static readonly System.Drawing.Bitmap[] heartFromLetteredDungeon_bmps = _heartFroms.Item6;
    private static System.Drawing.Bitmap[][] make_emptyUnfoundTriforces()
    {
        var a = new List<System.Drawing.Bitmap[]>();
        foreach (var ch in new[] { '1', 'A' })
        {
            var inner = new List<System.Drawing.Bitmap>();
            for (int i = 0; i <= 7; i++)
            {
                var bmp = (System.Drawing.Bitmap)greyTriforce_bmp.Clone();
                paintAlphanumerics3x5((char)(ch + i), UNFOUND_NUMERAL_COLOR, bmp, 4, 4);
                inner.Add(bmp);
            }
            a.Add(inner.ToArray());
        }
        return a.ToArray();
    }
    private static readonly System.Drawing.Bitmap[][] _emptyUnfoundTriforces = make_emptyUnfoundTriforces();
    public static readonly System.Drawing.Bitmap[] emptyUnfoundNumberedTriforce_bmps = _emptyUnfoundTriforces[0];
    public static readonly System.Drawing.Bitmap[] emptyUnfoundLetteredTriforce_bmps = _emptyUnfoundTriforces[1];
    private static System.Drawing.Bitmap[][] make_emptyFoundTriforces()
    {
        var a = new List<System.Drawing.Bitmap[]>();
        foreach (var ch in new[] { '1', 'A' })
        {
            var inner = new List<System.Drawing.Bitmap>();
            for (int i = 0; i <= 7; i++)
            {
                var bmp = (System.Drawing.Bitmap)greyTriforce_bmp.Clone();
                paintAlphanumerics3x5((char)(ch + i), FOUND_NUMERAL_COLOR, bmp, 4, 4);
                inner.Add(bmp);
            }
            a.Add(inner.ToArray());
        }
        return a.ToArray();
    }
    private static readonly System.Drawing.Bitmap[][] _emptyFoundTriforces = make_emptyFoundTriforces();
    public static readonly System.Drawing.Bitmap[] emptyFoundNumberedTriforce_bmps = _emptyFoundTriforces[0];
    public static readonly System.Drawing.Bitmap[] emptyFoundLetteredTriforce_bmps = _emptyFoundTriforces[1];
    private static System.Drawing.Bitmap[][] make_fullTriforces()
    {
        var a = new List<System.Drawing.Bitmap[]>();
        foreach (var ch in new[] { '1', 'A' })
        {
            var unfound = new List<System.Drawing.Bitmap>();
            for (int i = 0; i <= 7; i++)
            {
                var bmp = (System.Drawing.Bitmap)fullOrangeTriforce_bmp.Clone();
                paintAlphanumerics3x5((char)(ch + i), UNFOUND_NUMERAL_COLOR, bmp, 4, 4);
                unfound.Add(bmp);
            }
            a.Add(unfound.ToArray());
            var found = new List<System.Drawing.Bitmap>();
            for (int i = 0; i <= 7; i++)
            {
                var bmp = (System.Drawing.Bitmap)fullOrangeTriforce_bmp.Clone();
                paintAlphanumerics3x5((char)(ch + i), FOUND_NUMERAL_COLOR, bmp, 4, 4);
                found.Add(bmp);
            }
            a.Add(found.ToArray());
        }
        return a.ToArray();
    }
    private static readonly System.Drawing.Bitmap[][] _fullTriforces = make_fullTriforces();
    public static readonly System.Drawing.Bitmap[] fullNumberedUnfoundTriforce_bmps = _fullTriforces[0];
    public static readonly System.Drawing.Bitmap[] fullNumberedFoundTriforce_bmps = _fullTriforces[1];
    public static readonly System.Drawing.Bitmap[] fullLetteredUnfoundTriforce_bmps = _fullTriforces[2];
    public static readonly System.Drawing.Bitmap[] fullLetteredFoundTriforce_bmps = _fullTriforces[3];
    private static (System.Drawing.Bitmap, System.Drawing.Bitmap) make_L9s()
    {
        var unfoundTriforceColor = emptyUnfoundNumberedTriforce_bmps[0].GetPixel(15, 28);
        var u = new System.Drawing.Bitmap(10 * 3, 10 * 3);
        var f = new System.Drawing.Bitmap(10 * 3, 10 * 3);
        // make a rectangle 'shield' to draw the 9 on, so that a hint halo does not wash out the '9'
        for (int i = 3 * 3; i < 8 * 3; i++)
            for (int j = 3 * 3; j < 10 * 3; j++)
            {
                u.SetPixel(i, j, unfoundTriforceColor);
                f.SetPixel(i, j, unfoundTriforceColor);
            }
        paintAlphanumerics3x5('9', UNFOUND_NUMERAL_COLOR, u, 4, 4);
        paintAlphanumerics3x5('9', FOUND_NUMERAL_COLOR, f, 4, 4);
        return (u, f);
    }
    private static readonly (System.Drawing.Bitmap, System.Drawing.Bitmap) _L9s = make_L9s();
    public static readonly System.Drawing.Bitmap unfoundL9_bmp = _L9s.Item1;
    public static readonly System.Drawing.Bitmap foundL9_bmp = _L9s.Item2;

    private static System.Drawing.Bitmap make_fairy_bmp()
    {
        var imageStream = GetResourceStream("icons8x16.png");
        var bmp = new System.Drawing.Bitmap(imageStream);
        var r = new System.Drawing.Bitmap(16, 32);
        for (int x = 0; x <= 15; x++)
            for (int y = 0; y <= 31; y++)
            {
                var c = bmp.GetPixel(x / 2, y / 2);
                if (c.ToArgb() != System.Drawing.Color.Black.ToArgb())
                    r.SetPixel(x, y, c);
            }
        return r;
    }
    public static readonly System.Drawing.Bitmap fairy_bmp = make_fairy_bmp();

    public static System.Drawing.Color? entranceRoomArrowColor = null;
    public static (Avalonia.Media.Imaging.Bitmap, Avalonia.Media.Imaging.Bitmap)[] dungeonRoomBmpPairsF()
    {
        var imageStream = GetResourceStream("new_icons13x9.png");
        var bmp = new System.Drawing.Bitmap(imageStream);
        var a = new List<(Avalonia.Media.Imaging.Bitmap, Avalonia.Media.Imaging.Bitmap)>();
        for (int i = 0; i < bmp.Width / 13; i++)
        {
            var cr = new System.Drawing.Bitmap(13 * 3, 9 * 3);
            var ur = new System.Drawing.Bitmap(13 * 3, 9 * 3);
            for (int px = 0; px < 13 * 3; px++)
                for (int py = 0; py < 9 * 3; py++)
                {
                    ur.SetPixel(px, py, bmp.GetPixel(px / 3 + i * 13, py / 3));
                    cr.SetPixel(px, py, bmp.GetPixel(px / 3 + i * 13, 9 + py / 3));
                }
            if (i == 28)
                entranceRoomArrowColor = cr.GetPixel(18, 24);
            a.Add((BMPToBI(ur), BMPToBI(cr)));
        }
        return a.ToArray();
    }
    public static readonly (Avalonia.Media.Imaging.Bitmap, Avalonia.Media.Imaging.Bitmap)[] dungeonRoomBmpPairs = dungeonRoomBmpPairsF();
    public static (Avalonia.Media.Imaging.Bitmap, Avalonia.Media.Imaging.Bitmap)[] dungeonRoomTinyBmpPairsF()
    {
        var imageStream = GetResourceStream("new_icons13x9.png");
        var bmp = new System.Drawing.Bitmap(imageStream);
        var a = new List<(Avalonia.Media.Imaging.Bitmap, Avalonia.Media.Imaging.Bitmap)>();
        for (int i = 0; i < bmp.Width / 13; i++)
        {
            var ur = new System.Drawing.Bitmap(13, 9);
            var cr = new System.Drawing.Bitmap(13, 9);
            for (int px = 0; px < 13; px++)
                for (int py = 0; py < 9; py++)
                {
                    ur.SetPixel(px, py, bmp.GetPixel(px + i * 13, py));
                    cr.SetPixel(px, py, bmp.GetPixel(px + i * 13, 9 + py));
                }
            a.Add((BMPToBI(ur), BMPToBI(cr)));
        }
        return a.ToArray();
    }
    public static readonly (Avalonia.Media.Imaging.Bitmap, Avalonia.Media.Imaging.Bitmap)[] dungeonRoomTinyBmpPairs = dungeonRoomTinyBmpPairsF();

    /*
    let dungeonRoomFloorDrops, dungeonRoomMonsters =
        let imageStream = GetResourceStream("icons3x3.png")
        let bmp = new System.Drawing.Bitmap(imageStream)
        let a = [|
            for i = 0 to bmp.Width/3 - 1 do
                let r = new System.Drawing.Bitmap(5*3,5*3)
                let b = new System.Drawing.Bitmap(8*3,8*3)
                // black border around them
                for x = 0 to 14 do
                    for y = 0 to 14 do
                        r.SetPixel(x,y,System.Drawing.Color.Black)
                for x = 0 to 23 do
                    for y = 0 to 23 do
                        b.SetPixel(x,y,System.Drawing.Color.Black)
                // the icon in the middle
                for px = 0 to 3*3-1 do
                    for py = 0 to 3*3-1 do
                        r.SetPixel(px+3, py+3, bmp.GetPixel(px/3 + i*3,   py/3))
                        b.SetPixel(2*px+3, 2*py+3, bmp.GetPixel(px/3 + i*3,   py/3))
                        b.SetPixel(2*px+1+3, 2*py+3, bmp.GetPixel(px/3 + i*3,   py/3))
                        b.SetPixel(2*px+3, 2*py+1+3, bmp.GetPixel(px/3 + i*3,   py/3))
                        b.SetPixel(2*px+1+3, 2*py+1+3, bmp.GetPixel(px/3 + i*3,   py/3))
                yield r, b
        |]
        a.[0..7], a.[8..]
    */

    private static System.Drawing.Bitmap make_overworldImage()
    {
        var files = new[] {
//            "s_map_overworld_strip8.png"
            "s_map_overworld_vanilla_strip8.png"
//            "s_map_overworld_zones_strip8.png"
            };
        var file = files[(new System.Random()).Next(files.Length)];
//        printfn "selecting overworld file %s" file
        var imageStream = GetResourceStream(file);
        return new System.Drawing.Bitmap(imageStream);
    }
    public static readonly System.Drawing.Bitmap overworldImage = make_overworldImage();
    private static System.Drawing.Bitmap make_zhMapIcons()
    {
        var imageStream = GetResourceStream("s_icon_overworld_strip39.png");
        return new System.Drawing.Bitmap(imageStream);
    }
    public static readonly System.Drawing.Bitmap zhMapIcons = make_zhMapIcons();
    private static System.Drawing.Bitmap make_zhDungeonIcons()
    {
        var imageStream = GetResourceStream("s_btn_tr_dungeon_cell_strip3.png");
        return new System.Drawing.Bitmap(imageStream);
    }
    public static readonly System.Drawing.Bitmap zhDungeonIcons = make_zhDungeonIcons();
    private static System.Drawing.Bitmap make_zhDungeonNums()
    {
        var imageStream = GetResourceStream("s_btn_tr_dungeon_num_strip18.png");
        return new System.Drawing.Bitmap(imageStream);
    }
    public static readonly System.Drawing.Bitmap zhDungeonNums = make_zhDungeonNums();


    // 'Boots Replace Boomerangs' startup option: the boomerang is shown as the boots, and the magical (blue) boomerang as the magical boots
    private static System.Drawing.Bitmap bmpFromBase64(string b64)
    {
        using var ms = new System.IO.MemoryStream(System.Convert.FromBase64String(b64));
        var src = new System.Drawing.Bitmap(ms);
        var r = new System.Drawing.Bitmap(src);   // detach from the stream
        r.SetResolution(96.0f, 96.0f);
        return r;
    }
    public static readonly System.Drawing.Bitmap boots_bmp = bmpFromBase64("iVBORw0KGgoAAAANSUhEUgAAABUAAAAVCAYAAACpF6WWAAABq0lEQVR4nN2Uz2oUQRCHv+rpyay7MWuIEjwF/LMgGHISTyo5iCdfwIuPIb6PCOLFJ/DiUYUEkSCCB0nWEBNMgu5mZ7q7PAxK3O7dZA65+Dt2V39d1b+qhjOQjC+8fTirqkpw4d8NhSDGzbWMffxmMP9+z+8bgaAx1I4vqCp5kdO53AY9dsIIg4MRejRKpHICNLhAZ/Ec1+9dhTLUAFVoWfprfb5/3EJkOjWCZnnG4e5P1l99iILLKtDOLRrKZlAURIRsJoPj7yVgPGhwU4FJqHeeucVZenfj8rfWv7G70UdMw/L/ZItXNCgitXniFdWE1QmZU0U11H8ONVJ7NcmutFFTJMDAUabG8++lTYFeYXneXLt1MbvQnUk3V0Oo2mGlPLlZrL1c7fzodU03lfCpoCICVeDS0gIrD25g2oXrH1Tu6XLx4tmd9vPx+IlvqjrW7EHJC0t+vqDTbVlXOXqq99V7gEcnQsUIUuSI8bHFClduL0Ge8en1Z7e3uR+dj6BihNGvkp2N7XpMU1UAkgnVsLTWxhER1GSG4eERX959ZXInKmj9TZrsTOYn1m8QJqArILh0CgAAAABJRU5ErkJggg==");
    public static readonly System.Drawing.Bitmap magical_boots_bmp = bmpFromBase64("iVBORw0KGgoAAAANSUhEUgAAABUAAAAVCAYAAACpF6WWAAABkElEQVR4nM2UuUpDQRSGv5m5XJcEF0QQFGxMYxHb4AJpfAUVLXwCewt7KwvRIk/gWtvnCYJNELQIgqAIwS2Cy50Zi7lBSCbLFRF/GC53me/8Z+45B/5Cto201p/WWpvP54cAlFJeRuB7+PIGl7egpLsXwKeGsUGYGAFr2xvzQiv3sHUC6V7QFgIJj6+wPg9rC2B+Ap0eh6MNEAJs7NRYCGPn9QwSQbWF2puDgrtqA+kQ+tvzWkOvbmHrFFI9zqGS8PQKa7OwMucCJIYKAaFyqw4Ng85p19XyM+tZ3arL2Mn0f6BSSqGUQtTLo0HeH9VJtVrtQ2vd8n1iqAVyudxUOtV3XS6Xn6vVqrENfZso/SCQgQD293bPi8XiQzabHWwEdg01BvpDKF7A5iFU7k1kjIkKhcJxqVQ6aArugwhcoSsJIjYi46Fy8w53TzJI9UAmk1mMt6x2hEbGASLt5oDge7AIATtnbjZsL5toZrJ5vxc6OgBLOdeavk4SwEcEY8MykJ4DbCo06zv5DhKtCvY39QWjQ6CiSDEc2gAAAABJRU5ErkJggg==");
    public static System.Drawing.Bitmap boomerangOrBootsBmp() => TrackerModel.BootsReplaceBoomerangs ? boots_bmp : boomerang_bmp;
    public static System.Drawing.Bitmap magicBoomerangOrMagicalBootsBmp() => TrackerModel.BootsReplaceBoomerangs ? magical_boots_bmp : magic_boomerang_bmp;
    // the AP Item (an Archipelago multiworld item that belongs to another world): the Archipelago logo mark, 21x21 like the other item icons
    public static readonly System.Drawing.Bitmap ap_item_bmp = bmpFromBase64("iVBORw0KGgoAAAANSUhEUgAAABUAAAAVCAYAAACpF6WWAAABcklEQVR42s1VoXICMRCNQCBPIipOnKioSGcqkBGICkQFn4ADgUCcOMFMO4NAVFQiEBWIfgKin4DgAyoQCAQCgUBcd5mXmUwm2TAoxJtcdt8+NrvZoJ4HH0pAg1AR/gg11gr2aJxKiC4h5mN5q+gL4RwRZfvjNaIFYUJYEN6x14SdJ7iDvQBvgbjCF+0RToFsehA4OzYNu3+KE+wX0Twg6BI5g2/s59jHynIktBS6WQuYIAP+fsORJf5YoSYSif0G3+YaPouWhAPWJ5SD1zFhj0wzCGbIdO/xNewHm+kDYHD/frF2uD4Icm9JDvsr4cfhG+i0LHEUOUoVuYuxPoxs97XQzRp+V1AL3MuVY9JUIM1QxxzHy7GfCTFTqfslarfy7CvYS6n7w4BjTWgSNpHADfzrgG+ocBx/vvmH+on72A8kxDqZLX6bsHWcBiMpic6dobBvbdt/pZoYQ1vLr4ToJ3g8JF3EJ9/TbkK0czcv/83/Uf9WKOHiQPnmlQAAAABJRU5ErkJggg==");
    public static readonly System.Drawing.Bitmap[] customItemBmps = new System.Drawing.Bitmap[16];   // the user's ExtraIcons-folder icons, resized to item-icon size (filled in by DrawingLayer.applyCustomMenuIcons)
    public static readonly System.Drawing.Bitmap[] allItemBMPs = new[] { book_bmp, boomerang_bmp, bow_bmp, power_bracelet_bmp, ladder_bmp, magic_boomerang_bmp, key_bmp, raft_bmp, recorder_bmp, red_candle_bmp, red_ring_bmp, silver_arrow_bmp, wand_bmp, white_sword_bmp };
    private static System.Drawing.Bitmap[] make_allItemBMPsWithHeartShuffle()
    {
        var a = new List<System.Drawing.Bitmap>(allItemBMPs);
        for (int _i = 0; _i <= 8; _i++)
            a.Add(heart_container_bmp);
        return a.ToArray();
    }
    public static readonly System.Drawing.Bitmap[] allItemBMPsWithHeartShuffle = make_allItemBMPsWithHeartShuffle();

    // Extra item sprites (potions, letter, progressive sword). Drawn in code as 7x7 pixel art, blown up 3x like the icons7x7 sheet sprites.
    public static System.Drawing.Bitmap makeSprite7x7(string[] rows, (char, System.Drawing.Color)[] palette)
    {
        var r = new System.Drawing.Bitmap(7 * 3, 7 * 3);
        for (int px = 0; px < 7 * 3; px++)
            for (int py = 0; py < 7 * 3; py++)
            {
                var ch = rows[py / 3][px / 3];
                var color = System.Drawing.Color.Transparent;
                foreach (var (c, col) in palette)
                    if (c == ch) { color = col; break; }
                r.SetPixel(px, py, color);
            }
        return r;
    }
    // triforce piece icon (the item-sheet triforce, centered in a 21x21 cell)
    private static System.Drawing.Bitmap make_triforce_item_bmp()
    {
        var r = new System.Drawing.Bitmap(21, 21);
        for (int px = 0; px <= 15; px++)
            for (int py = 0; py <= 15; py++)
            {
                var c = zi_triforce_bmp.GetPixel(px + 1, py + 1);
                if (c.ToArgb() != System.Drawing.Color.Black.ToArgb()) r.SetPixel(px + 2, py + 2, c);
            }
        return r;
    }
    public static readonly System.Drawing.Bitmap triforce_item_bmp = make_triforce_item_bmp();
    // magical key (the NES magic key sprite: orange ring-head, red shading, short toothed shaft)
    public static readonly System.Drawing.Bitmap magic_key_bmp =
        makeSprite7x7(new[] { "..ooo..",
                              ".oo.orr",
                              ".ooorr.",
                              "...or..",
                              "...or..",
                              "...oo..",
                              "...or.." }, new[] { ('o', System.Drawing.Color.FromArgb(252, 152, 56)), ('r', System.Drawing.Color.FromArgb(216, 40, 0)) });
    public static System.Drawing.Bitmap readCacheFileOrCreateBmp(string filename, Func<System.Drawing.Bitmap> createF)
    {
        // always treat the tiles as 96dpi, whatever the cache file (or this machine's display scaling) says; a tile with another dpi gets drawn smaller than 48x33, leaving dark lines between map tiles
        // (a cache file cut short by a crash used to throw here on every launch: a file that will not load is regenerated;
        // the bytes are read into memory so the file is not locked while the app runs)
        if (System.IO.File.Exists(filename))
        {
            try
            {
                using (var ms = new System.IO.MemoryStream(System.IO.File.ReadAllBytes(filename)))
                using (var loaded = new System.Drawing.Bitmap(ms))
                {
                    var bmp = new System.Drawing.Bitmap(loaded);
                    bmp.SetResolution(96.0f, 96.0f);
                    return bmp;
                }
            }
            catch (Exception e)
            {
                // (only a file that cannot be decoded is deleted: a sharing violation, say from a virus scanner, leaves a good file alone)
                if (!(e is System.IO.IOException))
                    try { System.IO.File.Delete(filename); } catch (Exception) { }
            }
        }
        {
            var bmp = createF();
            bmp.SetResolution(96.0f, 96.0f);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filename));
            bmp.Save(filename);
            return bmp;
        }
    }

    public static string alternativeOverworldMapFilename = ""; public static bool shouldInitiallyHideOverworldMap = false;   // startup screen can set these
    private static System.Drawing.Bitmap make_blankTileBmp()
    {
        var fullTileBmp = new System.Drawing.Bitmap(16 * 3, 11 * 3);
        var main = System.Drawing.Color.DarkSlateGray;
        var alt = System.Drawing.Color.FromArgb((int)main.R + 48, (int)main.G + 48, (int)main.B + 88);
        for (int px = 0; px < 16 * 3; px++)
            for (int py = 0; py < 11 * 3; py++)
            {
                if (px >= 15 * 3 || py >= 10 * 3)
                    fullTileBmp.SetPixel(px, py, System.Drawing.Color.Black);
                else
                    fullTileBmp.SetPixel(px, py, (px / 3 + py / 3) % 2 == 0 ? main : alt);
            }
        return fullTileBmp;
    }
    public static readonly System.Drawing.Bitmap blankTileBmp = make_blankTileBmp();
    public static System.Drawing.Bitmap[,] overworldMapBMPs(int n)
    {
        var m = overworldImage;
        var tiles = new System.Drawing.Bitmap[16, 8];
        if (n == 4 && !System.String.IsNullOrEmpty(alternativeOverworldMapFilename))
        {
            var image = new System.Drawing.Bitmap(alternativeOverworldMapFilename);
            var bitmap = new System.Drawing.Bitmap(image, new System.Drawing.Size(256 * 3, 88 * 3));
            for (int x = 0; x <= 15; x++)
                for (int y = 0; y <= 7; y++)
                {
                    var tile = new System.Drawing.Bitmap(16 * 3, 11 * 3);
                    for (int px = 0; px < 16 * 3; px++)
                        for (int py = 0; py < 11 * 3; py++)
                            tile.SetPixel(px, py, bitmap.GetPixel((x * 16 * 3 + px), (y * 11 * 3 + py)));
                    tiles[x, y] = tile;
                }
        }
        else
        {
            for (int x = 0; x <= 15; x++)
                for (int y = 0; y <= 7; y++)
                {
                    var filename = Platform.Files.User("Overworld", $"quest.{n}.ow.{x,2}.{y,2}.bmp");   // (WPF: next to the exe, with a backslash path; sprintf "%2d" pads with a space)
                    var tile = readCacheFileOrCreateBmp(filename, () =>
                    {
                        var tile = new System.Drawing.Bitmap(16 * 3, 11 * 3);
                        for (int px = 0; px < 16 * 3; px++)
                            for (int py = 0; py < 11 * 3; py++)
                                tile.SetPixel(px, py, m.GetPixel(256 * n + (x * 16 * 3 + px) / 3, (y * 11 * 3 + py) / 3));
                        return tile;
                    });
                    tiles[x, y] = tile;
                }
        }
        return tiles;
    }

    public static readonly System.Drawing.Color TRANS_BG = System.Drawing.Color.FromArgb(1, System.Drawing.Color.Black);  // transparent background (will be darkened in program layer)
    public static readonly System.Drawing.Color itemBackgroundColor = System.Drawing.Color.FromArgb(0xEF, 0x83, 0);
    private static System.Drawing.Bitmap make_itemsBMP()
    {
        var imageStream = GetResourceStream("icons3x7.png");
        return new System.Drawing.Bitmap(imageStream);
    }
    public static readonly System.Drawing.Bitmap itemsBMP = make_itemsBMP();

    // a full tile with nothing drawn on it (used under marker icons when the tile's own icon is shown as one of those marker slots)
    private static System.Drawing.Bitmap make_emptyTileBmp()
    {
        var b = new System.Drawing.Bitmap(16 * 3, 11 * 3);
        for (int px = 0; px < 16 * 3; px++)
            for (int py = 0; py < 11 * 3; py++)
                b.SetPixel(px, py, TRANS_BG);
        return b;
    }
    public static readonly System.Drawing.Bitmap emptyTileBmp = make_emptyTileBmp();
    // the bomb (as at the top of the tracker), scaled to fit inside a w x h area and centered in it, on bg
    public static System.Drawing.Bitmap bombFitBmp(int w, int h, System.Drawing.Color bg)
    {
        var r = new System.Drawing.Bitmap(w, h);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                r.SetPixel(x, y, bg);
        bool isArt(int x, int y) => bomb_bmp.GetPixel(x, y).A > 128;
        var pts = new List<(int, int)>();
        for (int x = 0; x < bomb_bmp.Width; x++) for (int y = 0; y < bomb_bmp.Height; y++) if (isArt(x, y)) pts.Add((x, y));
        if (pts.Count != 0)
        {
            var (minX, maxX) = (pts.Min(p => p.Item1), pts.Max(p => p.Item1));
            var (minY, maxY) = (pts.Min(p => p.Item2), pts.Max(p => p.Item2));
            var (bw, bh) = ((double)(maxX - minX + 1), (double)(maxY - minY + 1));
            var scale = Math.Min((double)w / bw, (double)h / bh);
            var (dw, dh) = (Math.Max(1, (int)(bw * scale + 0.5)), Math.Max(1, (int)(bh * scale + 0.5)));
            (dw, dh) = (Math.Min(w, dw), Math.Min(h, dh));
            var (offX, offY) = ((w - dw) / 2, (h - dh) / 2);
            for (int x = 0; x < dw; x++)
                for (int y = 0; y < dh; y++)
                {
                    var sx = minX + Math.Min((int)bw - 1, (int)((double)x / scale));
                    var sy = minY + Math.Min((int)bh - 1, (int)((double)y / scale));
                    if (isArt(sx, sy)) r.SetPixel(offX + x, offY + y, bomb_bmp.GetPixel(sx, sy));
                }
        }
        return r;
    }

    private static System.Drawing.Bitmap make_genericDungeonInterior_bmp()
    {
        var bmp = new System.Drawing.Bitmap(5 * 3, 9 * 3);
        for (int px = 0; px < 5 * 3; px++)
            for (int py = 0; py < 9 * 3; py++)
                bmp.SetPixel(px, py, System.Drawing.Color.Yellow);
        paintAlphanumerics3x5('?', System.Drawing.Color.Black, bmp, 1, 2);
        return bmp;
    }
    public static readonly System.Drawing.Bitmap genericDungeonInterior_bmp = make_genericDungeonInterior_bmp();

    public static readonly double X_OPACITY = 0.55;
    public static readonly SolidColorBrush overworldCommonestFloorColorBrush = freeze(new SolidColorBrush(Color.FromRgb(204, 176, 136)));
    public static readonly SolidColorBrush overworldCommonestFloorColorDarkBrush = freeze(new SolidColorBrush(Color.FromRgb((byte)(204.0 * X_OPACITY), (byte)(176.0 * X_OPACITY), (byte)(136.0 * X_OPACITY))));
    public static readonly System.Drawing.Bitmap unmarkedBmp = new System.Drawing.Bitmap(5 * 3, 9 * 3);
    public static readonly System.Drawing.Bitmap dontCareBmp = new System.Drawing.Bitmap(5 * 3, 9 * 3);

    // each overworld map tile may have multiple icons that can represent it (e.g. dungeon 1 versus dungeon A)
    // we store a table, where the array index is the mapSquareChoiceDomain index of the general entry type, and the value there is a list of all possible icons
    // The sword cave icons (5x9 art pixels, 3x3 screen pixels each) without the brown cave floor: the number/letter becomes white with a black outline,
    // and the rest of the brown is transparent. Art that has no brown (already restyled) is returned unchanged.
    public static System.Drawing.Bitmap restyleSwordCard(System.Drawing.Bitmap bmp)
    {
        bool isBrown(System.Drawing.Color c) => c.A == 255 && c.R == 185 && c.G == 122 && c.B == 87;
        bool isBlack(System.Drawing.Color c) => c.A == 255 && c.R == 0 && c.G == 0 && c.B == 0;
        var (cw, ch) = (bmp.Width / 3, bmp.Height / 3);
        System.Drawing.Color cell(int x, int y) => bmp.GetPixel(x * 3, y * 3);
        var anyBrown = false;
        for (int x = 0; x < cw; x++)
            for (int y = 0; y < ch; y++)
                if (isBrown(cell(x, y))) anyBrown = true;
        if (!anyBrown) return bmp;
        else
        {
            var r = new System.Drawing.Bitmap(bmp.Width, bmp.Height);
            for (int x = 0; x < cw; x++)
                for (int y = 0; y < ch; y++)
                {
                    var c = cell(x, y);
                    System.Drawing.Color nc;
                    if (isBlack(c)) nc = System.Drawing.Color.White;
                    else if (isBrown(c))
                    {
                        var nearGlyph = false;
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                var (nx, ny) = (x + dx, y + dy);
                                if (nx >= 0 && nx < cw && ny >= 0 && ny < ch && isBlack(cell(nx, ny))) nearGlyph = true;
                            }
                        nc = nearGlyph ? System.Drawing.Color.Black : System.Drawing.Color.Transparent;
                    }
                    else nc = c;
                    for (int dx = 0; dx <= 2; dx++)
                        for (int dy = 0; dy <= 2; dy++)
                            r.SetPixel(x * 3 + dx, y * 3 + dy, nc);
                }
            return r;
        }
    }
    // a darker copy that keeps transparent pixels transparent
    public static System.Drawing.Bitmap darkenKeepAlpha(double pct, System.Drawing.Bitmap bmp)
    {
        var r = new System.Drawing.Bitmap(bmp.Width, bmp.Height);
        for (int px = 0; px < bmp.Width; px++)
            for (int py = 0; py < bmp.Height; py++)
            {
                var c = bmp.GetPixel(px, py);
                r.SetPixel(px, py, System.Drawing.Color.FromArgb((int)c.A, (int)(c.R * pct), (int)(c.G * pct), (int)(c.B * pct)));
            }
        return r;
    }
    // MapStateProxy will eventually be responsible for 'decoding' the current tracker state into the appropriate icon
    public static readonly List<System.Drawing.Bitmap>[] theInteriorBmpTable = Enumerable.Range(0, TrackerModel.dummyOverworldTiles.Length).Select(_ => new List<System.Drawing.Bitmap>()).ToArray();
    private static readonly bool _init2 = init2();
    private static bool init2()
    {
        var imageStream = GetResourceStream("ow_icons5x9.png");
        var interiorIconStrip = new System.Drawing.Bitmap(imageStream);
        var ubc = overworldCommonestFloorColorBrush;
        for (int px = 0; px < 5 * 3; px++)
            for (int py = 0; py < 9 * 3; py++)
                unmarkedBmp.SetPixel(px, py, System.Drawing.Color.FromArgb((int)ubc.Color.R, (int)ubc.Color.G, (int)ubc.Color.B));
        var dcbc = overworldCommonestFloorColorDarkBrush;
        for (int px = 0; px < 5 * 3; px++)
            for (int py = 0; py < 9 * 3; py++)
                dontCareBmp.SetPixel(px, py, System.Drawing.Color.FromArgb((int)dcbc.Color.R, (int)dcbc.Color.G, (int)dcbc.Color.B));
        var darkxbmp = new System.Drawing.Bitmap(5 * 3, 9 * 3);
        for (int px = 0; px < 5 * 3; px++)
            for (int py = 0; py < 9 * 3; py++)
                darkxbmp.SetPixel(px, py, System.Drawing.Color.Black);
        System.Drawing.Bitmap getInteriorIconFromStrip(int i)
        {
            var bmp = new System.Drawing.Bitmap(5 * 3, 9 * 3);
            for (int px = 0; px < 5 * 3; px++)
                for (int py = 0; py < 9 * 3; py++)
                    bmp.SetPixel(px, py, interiorIconStrip.GetPixel(i * 5 + px / 3, py / 3));
            return bmp;
        }
        // 0-8  dungeons: 4 varieties (numbered yellow, numbered dark-yellow, lettered yellow, lettered dark-yellow)
        foreach (var labels in new[] { "123456789", "ABCDEFGH9" })
            foreach (var color in new[] { System.Drawing.Color.Yellow, System.Drawing.Color.FromArgb(153, 153, 0) })
                for (int i = 0; i < labels.Length; i++)
                {
                    var ch = labels[i];
                    var bmp = new System.Drawing.Bitmap(5 * 3, 9 * 3);
                    for (int px = 0; px < 5 * 3; px++)
                        for (int py = 0; py < 9 * 3; py++)
                            bmp.SetPixel(px, py, color);
                    paintAlphanumerics3x5(ch, System.Drawing.Color.Black, bmp, 1, 2);
                    theInteriorBmpTable[i].Add(bmp);
                }
        // 9-12  any roads
        {
            var labels = "1234";
            for (int i = 0; i < labels.Length; i++)
            {
                var ch = labels[i];
                var bmp = new System.Drawing.Bitmap(5 * 3, 9 * 3);
                for (int px = 0; px < 5 * 3; px++)
                    for (int py = 0; py < 9 * 3; py++)
                        bmp.SetPixel(px, py, System.Drawing.Color.Orchid);
                paintAlphanumerics3x5(ch, System.Drawing.Color.Black, bmp, 1, 2);
                theInteriorBmpTable[i + 9].Add(bmp);
            }
        }
        // 13  sword3
        theInteriorBmpTable[13].Add(restyleSwordCard(getInteriorIconFromStrip(0)));
        theInteriorBmpTable[13].Add(darkenKeepAlpha(0.6, restyleSwordCard(getInteriorIconFromStrip(0))));
        // 14  sword2
        theInteriorBmpTable[14].Add(restyleSwordCard(getInteriorIconFromStrip(1)));
        theInteriorBmpTable[14].Add(darkenKeepAlpha(0.6, restyleSwordCard(getInteriorIconFromStrip(1))));
        // 15  sword1
        theInteriorBmpTable[15].Add(restyleSwordCard(getInteriorIconFromStrip(2)));
        theInteriorBmpTable[15].Add(darkenKeepAlpha(0.6, restyleSwordCard(getInteriorIconFromStrip(2))));
        // 16-23  item shops (as single-item icons)
        for (int i = 0; i < TrackerModel.MapSquareChoiceDomainHelper.NUM_ITEMS; i++)
        {
            var bmp = new System.Drawing.Bitmap(5 * 3, 9 * 3);
            for (int px = 0; px < 5 * 3; px++)
                for (int py = 0; py < 9 * 3; py++)
                {
                    bmp.SetPixel(px, py, itemBackgroundColor);
                    if (px / 3 >= 1 && px / 3 <= 3 && py / 3 >= 1 && py / 3 <= 7)
                    {
                        var c = itemsBMP.GetPixel(i * 3 + px / 3 - 1, py / 3 - 1);
                        if (c.ToArgb() != System.Drawing.Color.Black.ToArgb())
                            bmp.SetPixel(px, py, c);
                    }
                }
            if (i == TrackerModel.MapSquareChoiceDomainHelper.BOMB - 16)
            {
                // the bomb shop is just the bomb icon (no orange background), like the one at the top of the tracker
                var b = bombFitBmp(13, 13, System.Drawing.Color.Transparent);
                for (int px = 0; px <= 14; px++)
                    for (int py = 0; py <= 26; py++)
                        bmp.SetPixel(px, py, TRANS_BG);
                bool artAt(int x, int y) => x >= 1 && x <= 13 && y >= 7 && y <= 19 && b.GetPixel(x - 1, y - 7).A > 128;
                for (int px = 0; px <= 14; px++)
                    for (int py = 0; py <= 26; py++)
                    {
                        if (artAt(px, py)) bmp.SetPixel(px, py, b.GetPixel(px - 1, py - 7));
                        else
                        {
                            var anyNeighborArt = false;
                            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if (artAt(px + dx, py + dy)) anyNeighborArt = true;
                            if (anyNeighborArt) bmp.SetPixel(px, py, System.Drawing.Color.Black);   // black outline, like the Bomb marker icon
                        }
                    }
            }
            theInteriorBmpTable[i + 16].Add(bmp);
        }
        // 24  unknown money secret
        theInteriorBmpTable[24].Add(getInteriorIconFromStrip(11));
        // 25  large secret
        theInteriorBmpTable[25].Add(getInteriorIconFromStrip(8));
        theInteriorBmpTable[25].Add(darken(getInteriorIconFromStrip(8)));
        // 26  medium secret
        theInteriorBmpTable[26].Add(getInteriorIconFromStrip(6));
        theInteriorBmpTable[26].Add(darken(getInteriorIconFromStrip(6)));
        // 27  small secret
        theInteriorBmpTable[27].Add(getInteriorIconFromStrip(9));
        theInteriorBmpTable[27].Add(darken(getInteriorIconFromStrip(9)));
        // 28  door repair charge
        theInteriorBmpTable[28].Add(getInteriorIconFromStrip(12));
        theInteriorBmpTable[28].Add(darkenImpl(0.7, getInteriorIconFromStrip(12)));
        // 29  money making game
        // (the brown top and bottom edge of the card is filled in with the card's own light gray)
        {
            var b = getInteriorIconFromStrip(10);
            for (int px = 0; px < b.Width; px++)
                for (int py = 0; py < b.Height; py++)
                {
                    var c = b.GetPixel(px, py);
                    if (c.R == 185 && c.G == 122 && c.B == 87) b.SetPixel(px, py, System.Drawing.Color.FromArgb(255, 228, 228, 228));
                }
            theInteriorBmpTable[29].Add(b);
        }
        // 30  letter
        theInteriorBmpTable[30].Add(getInteriorIconFromStrip(13));
        theInteriorBmpTable[30].Add(darkenImpl(0.7, getInteriorIconFromStrip(13)));
        // 31  armos
        theInteriorBmpTable[31].Add(getInteriorIconFromStrip(7));
        theInteriorBmpTable[31].Add(darken(getInteriorIconFromStrip(7)));
        // 32  hint shop
        theInteriorBmpTable[32].Add(getInteriorIconFromStrip(3));
        theInteriorBmpTable[32].Add(darkenImpl(0.7, getInteriorIconFromStrip(3)));
        // 33  take any
        theInteriorBmpTable[33].Add(getInteriorIconFromStrip(4));
        theInteriorBmpTable[33].Add(darken(getInteriorIconFromStrip(4)));
        // 34  potion shop
        theInteriorBmpTable[34].Add(getInteriorIconFromStrip(5));
        // 35  'X'
        theInteriorBmpTable[35].Add(darkxbmp);
        // Helper: paint a 3x3 block at unit coordinates (ux, uy) in black
        void p3(System.Drawing.Bitmap bmp, int ux, int uy)
        {
            for (int dx = 0; dx <= 2; dx++)
                for (int dy = 0; dy <= 2; dy++)
                    bmp.SetPixel(3 * ux + dx, 3 * uy + dy, System.Drawing.Color.Black);
        }
        // 36  personal preference 1 (magenta background, ✳ asterisk shape — not a dungeon letter)
        //  Pattern (unit coords, 5 wide x 9 tall):
        //    . X . X .   row 2
        //    . . X . .   row 3
        //    X X X X X   row 4  (horizontal bar)
        //    . . X . .   row 5
        //    . X . X .   row 6
        var pp1bmp = new System.Drawing.Bitmap(5 * 3, 9 * 3);
        for (int px = 0; px < 5 * 3; px++)
            for (int py = 0; py < 9 * 3; py++)
                pp1bmp.SetPixel(px, py, System.Drawing.Color.Magenta);
        foreach (var (ux, uy) in new[] { (1, 2), (3, 2), (2, 3), (0, 4), (1, 4), (2, 4), (3, 4), (4, 4), (2, 5), (1, 6), (3, 6) })
            p3(pp1bmp, ux, uy);
        theInteriorBmpTable[36].Add(pp1bmp);
        // 37  personal preference 2 (cyan background, ◆ diamond shape — not a dungeon letter)
        //  Pattern (unit coords):
        //    . . X . .   row 2  (top point)
        //    . X X X .   row 3
        //    X X X X X   row 4  (widest)
        //    . X X X .   row 5
        //    . . X . .   row 6  (bottom point)
        var pp2bmp = new System.Drawing.Bitmap(5 * 3, 9 * 3);
        for (int px = 0; px < 5 * 3; px++)
            for (int py = 0; py < 9 * 3; py++)
                pp2bmp.SetPixel(px, py, System.Drawing.Color.Cyan);
        foreach (var (ux, uy) in new[] { (2, 2), (1, 3), (2, 3), (3, 3), (0, 4), (1, 4), (2, 4), (3, 4), (4, 4), (1, 5), (2, 5), (3, 5), (2, 6) })
            p3(pp2bmp, ux, uy);
        theInteriorBmpTable[37].Add(pp2bmp);
        return true;
    }
    // full tiles just have interior bmp in the center and transparent pixels all around (except for the final 'X' one)
    // 'Use old potion/letter icons': the tracker's original overworld-tile artwork for the potion shop and the (blue) letter, shrunk to fit an item cell.
    // The 5x9 interior icon is redrawn at 2x (10x18) and centered in a 21x21 cell.
    public static System.Drawing.Bitmap oldTileIconAsItem(System.Drawing.Bitmap interior)
    {
        // drop the brown (corner-colored) background, crop to the artwork, and scale it up (nearest-neighbor) to fill as much of the 21x21 item cell as it can
        var r = new System.Drawing.Bitmap(21, 21);
        for (int px = 0; px <= 20; px++)
            for (int py = 0; py <= 20; py++)
                r.SetPixel(px, py, System.Drawing.Color.Transparent);
        System.Drawing.Color art(int ux, int uy) => interior.GetPixel(ux * 3 + 1, uy * 3 + 1);
        var bg = art(0, 0);
        bool isArt(int ux, int uy) { var c = art(ux, uy); return !(c.R == bg.R && c.G == bg.G && c.B == bg.B); }
        var pts = new List<(int, int)>();
        for (int ux = 0; ux <= 4; ux++) for (int uy = 0; uy <= 8; uy++) if (isArt(ux, uy)) pts.Add((ux, uy));
        if (pts.Count != 0)
        {
            var minX = pts.Min(p => p.Item1);
            var maxX = pts.Max(p => p.Item1);
            var minY = pts.Min(p => p.Item2);
            var maxY = pts.Max(p => p.Item2);
            var (bw, bh) = ((double)(maxX - minX + 1), (double)(maxY - minY + 1));
            var scale = Math.Min(21.0 / bw, 21.0 / bh);
            var (dw, dh) = ((int)(bw * scale), (int)(bh * scale));
            var (offX, offY) = ((21 - dw) / 2, (21 - dh) / 2);
            for (int x = 0; x < dw; x++)
                for (int y = 0; y < dh; y++)
                {
                    var ux = minX + Math.Min((int)bw - 1, (int)((double)x / scale));
                    var uy = minY + Math.Min((int)bh - 1, (int)((double)y / scale));
                    if (isArt(ux, uy))
                        r.SetPixel(offX + x, offY + y, art(ux, uy));
                }
        }
        return r;
    }
    // the old potion-shop icon exactly as it looked on the overworld tile (tall, with its brown background), scaled to the 21-pixel item height
    public static System.Drawing.Bitmap oldTileIconWithBackgroundAsItem(System.Drawing.Bitmap interior)
    {
        var r = new System.Drawing.Bitmap(21, 21);
        for (int px = 0; px <= 20; px++)
            for (int py = 0; py <= 20; py++)
                r.SetPixel(px, py, System.Drawing.Color.Transparent);
        var scale = 21.0 / (double)interior.Height;
        var (dw, dh) = (Math.Max(1, (int)((double)interior.Width * scale)), 21);
        var offX = (21 - dw) / 2;
        for (int x = 0; x < dw; x++)
            for (int y = 0; y < dh; y++)
            {
                var sx = Math.Min(interior.Width - 1, (int)((double)x / scale));
                var sy = Math.Min(interior.Height - 1, (int)((double)y / scale));
                r.SetPixel(offX + x, y, interior.GetPixel(sx, sy));
            }
        return r;
    }
    // NES-style bottle: white outline, cork on top, liquid in the lower half (liquid/cork colors taken from the NES sprites)
    public static readonly string[] potionRows = new[] { "..aaa..",
                                                         "..cLc..",
                                                         "..c.c..",
                                                         ".c...c.",
                                                         ".cLLLc.",
                                                         ".cLhLc.",
                                                         "..ccc.." };
    public static readonly System.Drawing.Bitmap blue_potion_bmp =
        makeSprite7x7(potionRows, new[] { ('a', System.Drawing.Color.FromArgb(155, 142, 255)), ('c', System.Drawing.Color.White), ('L', System.Drawing.Color.FromArgb(40, 0, 165)), ('h', System.Drawing.Color.FromArgb(155, 142, 255)) });
    public static readonly System.Drawing.Bitmap red_potion_bmp =
        makeSprite7x7(potionRows, new[] { ('a', System.Drawing.Color.FromArgb(231, 156, 33)), ('c', System.Drawing.Color.White), ('L', System.Drawing.Color.FromArgb(181, 49, 33)), ('h', System.Drawing.Color.FromArgb(231, 156, 33)) });
    // one icon for both potions: left half blue potion, right half red potion (split vertically)
    private static System.Drawing.Bitmap make_potion_split_bmp()
    {
        var r = new System.Drawing.Bitmap(21, 21);
        for (int x = 0; x <= 20; x++)
            for (int y = 0; y <= 20; y++)
                r.SetPixel(x, y, (x <= 10 ? blue_potion_bmp : red_potion_bmp).GetPixel(x, y));
        return r;
    }
    public static readonly System.Drawing.Bitmap potion_split_bmp = make_potion_split_bmp();
    // (the old brown-background potion sprite is gone for good: the "old" potion icons are the same potion sprites as the dungeon item menu)
    public static readonly System.Drawing.Bitmap old_potion_bmp = potion_split_bmp;
    public static readonly System.Drawing.Bitmap old_blue_potion_bmp = blue_potion_bmp;
    public static readonly System.Drawing.Bitmap old_red_potion_bmp = red_potion_bmp;
    public static readonly System.Drawing.Bitmap old_potion_split_bmp = potion_split_bmp;
    public static readonly System.Drawing.Bitmap old_letter_bmp = oldTileIconAsItem(theInteriorBmpTable[30][0]);   // the old (blue) letter icon
    // the potion shop's overworld icon (menu cell and placed tile): the split potion sprite (blue half on the left, red half on the right) on a plain background, no brown cave color
    private static readonly bool _init3 = init3();
    private static bool init3()
    {
        var BG = System.Drawing.Color.FromArgb((int)TRANS_BG.A, (int)TRANS_BG.R, (int)TRANS_BG.G, (int)TRANS_BG.B);
        var r = new System.Drawing.Bitmap(5 * 3, 9 * 3);
        for (int x = 0; x < r.Width; x++)
            for (int y = 0; y < r.Height; y++)
                r.SetPixel(x, y, BG);
        var sz = r.Width;   // 15: the 21x21 split sprite scaled down (nearest neighbor), centered vertically
        for (int x = 0; x < sz; x++)
            for (int y = 0; y < sz; y++)
            {
                var p = potion_split_bmp.GetPixel(x * 21 / sz, y * 21 / sz);
                if (p.A > 0 && ((int)p.R + (int)p.G + (int)p.B) > 40) r.SetPixel(x, y + (r.Height - sz) / 2, p);
            }
        theInteriorBmpTable[34][0] = r;
        return true;
    }
    // the overworld sword tile icons (1/2/3), used for the swords in the dungeon item menus
    // the thin, tall overworld cave icon (15x27) scaled to fit the 21-tall item cell: a 12x21 icon centered in the cell, no brown fill around it
    private static System.Drawing.Color swordMenuPixel(System.Drawing.Color c) => c;
    public static System.Drawing.Bitmap swordIconAsItem(System.Drawing.Bitmap interior0)
    {
        var interior = restyleSwordCard(interior0);
        var r = new System.Drawing.Bitmap(21, 21);
        for (int px = 0; px <= 20; px++)
            for (int py = 0; py <= 20; py++)
                r.SetPixel(px, py, System.Drawing.Color.Transparent);
        for (int x = 0; x <= 11; x++)
            for (int y = 0; y <= 20; y++)
            {
                var sx = Math.Min(14, (int)(((double)x + 0.5) * 15.0 / 12.0));
                var sy = Math.Min(26, (int)(((double)y + 0.5) * 27.0 / 21.0));
                r.SetPixel(4 + x, y, swordMenuPixel(interior.GetPixel(sx, sy)));
            }
        return r;
    }
    // the same icon at its natural 15x27 size (3x3 screen pixels per art pixel), used in the (taller) top row of the dungeon item menu
    public static System.Drawing.Bitmap swordMenuBmp(System.Drawing.Bitmap interior0)
    {
        var interior = restyleSwordCard(interior0);
        var r = new System.Drawing.Bitmap(15, 27);
        for (int x = 0; x <= 14; x++)
            for (int y = 0; y <= 26; y++)
                r.SetPixel(x, y, swordMenuPixel(interior.GetPixel(x, y)));
        return r;
    }
    public static readonly System.Drawing.Bitmap ow_sword1_menu_bmp_new = swordMenuBmp(theInteriorBmpTable[15][0]);
    public static readonly System.Drawing.Bitmap ow_sword2_menu_bmp_new = swordMenuBmp(theInteriorBmpTable[14][0]);
    public static readonly System.Drawing.Bitmap ow_sword3_menu_bmp_new = swordMenuBmp(theInteriorBmpTable[13][0]);
    public static readonly System.Drawing.Bitmap ow_sword1_item_bmp_new = swordIconAsItem(theInteriorBmpTable[15][0]);
    public static readonly System.Drawing.Bitmap ow_sword2_item_bmp_new = swordIconAsItem(theInteriorBmpTable[14][0]);
    public static readonly System.Drawing.Bitmap ow_sword3_item_bmp_new = swordIconAsItem(theInteriorBmpTable[13][0]);
    // the overworld 4th-sword ('4') and progressive-sword ('P') icons: same pixel art as WPFUI.extraSwordIconBmp
    private static System.Drawing.Bitmap extraSwordInterior(bool isProg)
    {
        var brown = System.Drawing.Color.FromArgb(185, 122, 87);
        var blade = isProg ? System.Drawing.Color.FromArgb(255, 242, 0) : System.Drawing.Color.FromArgb(128, 128, 255);
        var hilt = isProg ? System.Drawing.Color.White : System.Drawing.Color.White;
        var glyph = isProg ? new[] { "abbba", "ababa", "abbba", "abaaa", "abaaa" } : new[] { "ababa", "ababa", "abbba", "aaaba", "aaaba" };
        var rows = new[] { "aaaaa" }.Concat(glyph).Concat(new[] { "aaaXa", "HHHXX", "aaaXa" }).ToArray();
        var bmp = new System.Drawing.Bitmap(5 * 3, 9 * 3);
        for (int y = 0; y <= 8; y++)
            for (int x = 0; x <= 4; x++)
            {
                var c =
                    rows[y][x] switch
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
        return bmp;
    }
    public static readonly System.Drawing.Bitmap ow_sword4_item_bmp_new = swordIconAsItem(extraSwordInterior(false));
    public static readonly System.Drawing.Bitmap ow_progressive_item_bmp = swordIconAsItem(extraSwordInterior(true));
    public static readonly System.Drawing.Bitmap ow_sword4_menu_bmp_new = swordMenuBmp(extraSwordInterior(false));
    public static readonly System.Drawing.Bitmap ow_progressive_menu_bmp = swordMenuBmp(extraSwordInterior(true));
    // the full 15x27 sword icons with a grey (instead of black) outline around the number/letter: shown in the sword item boxes
    public static System.Drawing.Bitmap swordBoxBmp(System.Drawing.Bitmap menuBmp)
    {
        var r = new System.Drawing.Bitmap(menuBmp.Width, menuBmp.Height);
        for (int x = 0; x < r.Width; x++)
            for (int y = 0; y < r.Height; y++)
            {
                var c = menuBmp.GetPixel(x, y);
                r.SetPixel(x, y, (c.A == 255 && c.R < 30 && c.G < 30 && c.B < 30 ? System.Drawing.Color.FromArgb(150, 150, 150) : c));
            }
        return r;
    }
    public static readonly System.Drawing.Bitmap ow_sword1_box_bmp_new = swordBoxBmp(ow_sword1_menu_bmp_new);
    public static readonly System.Drawing.Bitmap ow_sword2_box_bmp_new = swordBoxBmp(ow_sword2_menu_bmp_new);
    public static readonly System.Drawing.Bitmap ow_sword3_box_bmp_new = swordBoxBmp(ow_sword3_menu_bmp_new);
    public static readonly System.Drawing.Bitmap ow_sword4_box_bmp_new = swordBoxBmp(ow_sword4_menu_bmp_new);
    public static readonly System.Drawing.Bitmap ow_progressive_box_bmp = swordBoxBmp(ow_progressive_menu_bmp);
    // "Use old sword icons" (Options > Other): the plain sword pictures from icons7x7.png instead of the numbered overworld sword icons. Read each time an icon is asked for, but the
    // pictures already on screen keep what they were built with, so the option takes effect when the tracker starts. The progressive sword icons are not part of this.
    // The 4th sword is the white sword with its lavender part (guard and handle) checkered with dark blue.
    private static System.Drawing.Bitmap oldFourthSwordSprite()
    {
        var r = new System.Drawing.Bitmap(21, 21);
        var navy = System.Drawing.Color.FromArgb(255, 0, 0, 170);
        for (int px = 0; px < 21; px++)
            for (int py = 0; py < 21; py++)
            {
                var c = white_sword_bmp.GetPixel(px, py);
                if (c.A == 255 && c.R == 128 && c.G == 128 && c.B == 255 && ((px / 3 + py / 3) % 2 == 0)) c = navy;
                r.SetPixel(px, py, c);
            }
        return r;
    }
    // the 21x21 sprite in the 15x27 menu cell: the sword's art is 5 pixels wide (columns 1-5), so cropping 3 pixels off each side loses nothing; centered vertically
    private static System.Drawing.Bitmap oldSwordAsMenu(System.Drawing.Bitmap sprite21)
    {
        var r = new System.Drawing.Bitmap(15, 27);
        for (int x = 0; x < 15; x++)
            for (int y = 0; y < 27; y++)
            {
                var sy = y - 3;
                r.SetPixel(x, y, sy >= 0 && sy < 21 ? sprite21.GetPixel(x + 3, sy) : System.Drawing.Color.Transparent);
            }
        return r;
    }
    private static readonly System.Drawing.Bitmap[] oldSwordItemBmps = { brown_sword_bmp, white_sword_bmp, magical_sword_bmp, oldFourthSwordSprite() };
    private static readonly System.Drawing.Bitmap[] oldSwordMenuBmps = oldSwordItemBmps.Select(oldSwordAsMenu).ToArray();
    private static bool useOldSwords => TrackerModelOptions.UseOldSwordIcons.Value;
    public static System.Drawing.Bitmap ow_sword1_item_bmp => useOldSwords ? oldSwordItemBmps[0] : ow_sword1_item_bmp_new;
    public static System.Drawing.Bitmap ow_sword2_item_bmp => useOldSwords ? oldSwordItemBmps[1] : ow_sword2_item_bmp_new;
    public static System.Drawing.Bitmap ow_sword3_item_bmp => useOldSwords ? oldSwordItemBmps[2] : ow_sword3_item_bmp_new;
    public static System.Drawing.Bitmap ow_sword4_item_bmp => useOldSwords ? oldSwordItemBmps[3] : ow_sword4_item_bmp_new;
    public static System.Drawing.Bitmap ow_sword1_menu_bmp => useOldSwords ? oldSwordMenuBmps[0] : ow_sword1_menu_bmp_new;
    public static System.Drawing.Bitmap ow_sword2_menu_bmp => useOldSwords ? oldSwordMenuBmps[1] : ow_sword2_menu_bmp_new;
    public static System.Drawing.Bitmap ow_sword3_menu_bmp => useOldSwords ? oldSwordMenuBmps[2] : ow_sword3_menu_bmp_new;
    public static System.Drawing.Bitmap ow_sword4_menu_bmp => useOldSwords ? oldSwordMenuBmps[3] : ow_sword4_menu_bmp_new;
    public static System.Drawing.Bitmap ow_sword1_box_bmp => useOldSwords ? oldSwordMenuBmps[0] : ow_sword1_box_bmp_new;   // (no black outline to turn grey on the old pictures)
    public static System.Drawing.Bitmap ow_sword2_box_bmp => useOldSwords ? oldSwordMenuBmps[1] : ow_sword2_box_bmp_new;
    public static System.Drawing.Bitmap ow_sword3_box_bmp => useOldSwords ? oldSwordMenuBmps[2] : ow_sword3_box_bmp_new;
    public static System.Drawing.Bitmap ow_sword4_box_bmp => useOldSwords ? oldSwordMenuBmps[3] : ow_sword4_box_bmp_new;
    // Progressive Items: the placed swords show their tier (P1..P4). With "Use old sword icons" the tiers 1-4 are the wood, white, magical and 4th sword pictures instead; index 0 (the plain P sword) is unchanged
    private static System.Drawing.Bitmap[] oldTierItemBmpsCache;
    public static System.Drawing.Bitmap[] ow_progressive_tier_item_bmps =>
        !useOldSwords ? ow_progressive_tier_item_bmps_new
        : (oldTierItemBmpsCache ??= new[] { ow_progressive_tier_item_bmps_new[0], oldSwordItemBmps[0], oldSwordItemBmps[1], oldSwordItemBmps[2], oldSwordItemBmps[3] });
    // Progressive Items: the progressive sword with its tier next to the P (P1 / P2 / P3 / P4), over a sword drawn like the item progress bar's swords (hilt, tall guard, blade).
    // 21x27: the P and number glyph on top (outlined like the plain P sword), the sword along the bottom. Index 0 of the arrays below is the plain P sword.
    private static System.Drawing.Bitmap progressiveTierSwordBmp(int tier)
    {
        var pGlyph = new[] { "bbb", "bab", "bbb", "baa", "baa" };
        var digit =
            tier switch
            {
                1 => new[] { "aba", "bba", "aba", "aba", "bbb" },
                2 => new[] { "bbb", "aab", "bbb", "baa", "bbb" },
                3 => new[] { "bbb", "aab", "bbb", "aab", "bbb" },
                _ => new[] { "bab", "bab", "bbb", "aab", "aab" },
            };
        var rows = new[] { "aaaaaaa" }.Concat(Enumerable.Range(0, 5).Select(r => pGlyph[r] + "a" + digit[r])).Concat(new[] { "aaaaaaa" }).ToArray();
        var brown = System.Drawing.Color.FromArgb(185, 122, 87);
        var art = new System.Drawing.Bitmap(7 * 3, 7 * 3);
        for (int y = 0; y <= 6; y++)
            for (int x = 0; x <= 6; x++)
            {
                var c = rows[y][x] == 'b' ? System.Drawing.Color.Black : brown;
                for (int dx = 0; dx <= 2; dx++)
                    for (int dy = 0; dy <= 2; dy++)
                        art.SetPixel(x * 3 + dx, y * 3 + dy, c);
            }
        var glyphPart = restyleSwordCard(art);
        System.Drawing.Color rgb(int r, int g, int b) => System.Drawing.Color.FromArgb(r, g, b);
        // hilt, hilt end, blade, guard, bottom edge, tip colors (the same palette as the item progress bar swords)
        var (hilt, hiltEnd, blade, guard, edge, tip) =
            tier switch
            {
                1 => (rgb(136, 0, 21), rgb(76, 0, 12), rgb(29, 148, 65), rgb(29, 148, 65), rgb(15, 74, 33), rgb(22, 111, 49)),
                2 => (rgb(255, 255, 255), rgb(80, 80, 80), rgb(63, 72, 204), rgb(63, 72, 204), rgb(32, 36, 102), rgb(63, 72, 204)),
                3 => (rgb(255, 255, 255), rgb(32, 32, 32), rgb(237, 0, 36), rgb(237, 0, 36), rgb(119, 0, 18), rgb(59, 0, 9)),
                _ => (rgb(255, 255, 255), rgb(223, 223, 223), rgb(128, 128, 255), rgb(0, 0, 168), rgb(0, 0, 84), rgb(0, 0, 84)),
            };
        var r = new System.Drawing.Bitmap(21, 27);
        for (int x = 0; x <= 20; x++)
            for (int y = 0; y <= 17; y++)
                r.SetPixel(x, y, glyphPart.GetPixel(x, y));
        var y0 = 18;
        for (int ry = 0; ry <= 8; ry++)
            for (int x = 13; x <= 16; x++)
                r.SetPixel(x, y0 + ry, (ry < 8 ? guard : edge));
        for (int ry = 3; ry <= 5; ry++)
        {
            for (int x = 0; x <= 12; x++)
                r.SetPixel(x, y0 + ry, (x == 0 ? hiltEnd : hilt));
            for (int x = 13; x <= 20; x++)
                r.SetPixel(x, y0 + ry, (x == 20 ? tip : tier == 4 && x > 17 ? rgb(0, 0, 168) : blade));
        }
        return r;
    }
    private static System.Drawing.Bitmap progressiveTierItemBmp(System.Drawing.Bitmap interior)
    {
        var r = new System.Drawing.Bitmap(21, 21);
        for (int x = 0; x <= 14; x++)
            for (int y = 0; y <= 20; y++)
            {
                var sx = Math.Min(20, (int)(((double)x + 0.5) * 21.0 / 15.0));
                var sy = Math.Min(26, (int)(((double)y + 0.5) * 27.0 / 21.0));
                r.SetPixel(3 + x, y, interior.GetPixel(sx, sy));
            }
        return r;
    }
    // index 0 is the plain 'P' sword (shown in the menus); 1..4 are the tier sprites shown once an item is placed
    private static System.Drawing.Bitmap[] make_ow_progressive_tier_item_bmps()
    {
        var a = new List<System.Drawing.Bitmap> { ow_progressive_item_bmp };
        for (int t = 1; t <= 4; t++) a.Add(progressiveTierItemBmp(progressiveTierSwordBmp(t)));
        return a.ToArray();
    }
    public static readonly System.Drawing.Bitmap[] ow_progressive_tier_item_bmps_new = make_ow_progressive_tier_item_bmps();
    private static System.Drawing.Bitmap[] make_ow_progressive_tier_menu_bmps()
    {
        var a = new List<System.Drawing.Bitmap> { ow_progressive_menu_bmp };
        for (int t = 1; t <= 4; t++) a.Add(progressiveTierSwordBmp(t));
        return a.ToArray();
    }
    public static readonly System.Drawing.Bitmap[] ow_progressive_tier_menu_bmps = make_ow_progressive_tier_menu_bmps();
    public static readonly List<System.Drawing.Bitmap>[] theFullTileBmpTable = Enumerable.Range(0, theInteriorBmpTable.Length).Select(_ => new List<System.Drawing.Bitmap>()).ToArray();
    public static void initFull()
    {
        var len = theInteriorBmpTable.Length;
        var BG = System.Drawing.Color.FromArgb((int)TRANS_BG.A, (int)TRANS_BG.R, (int)TRANS_BG.G, (int)TRANS_BG.B);   // the compiler is being stupid, clone this color to workaround
        for (int i = 0; i < len; i++)
            foreach (var interiorBmp in theInteriorBmpTable[i])
            {
                var fullTileBmp = new System.Drawing.Bitmap(16 * 3, 11 * 3);
                for (int px = 0; px < 16 * 3; px++)
                    for (int py = 0; py < 11 * 3; py++)
                    {
                        if (px >= 5 * 3 && px < 10 * 3 && py >= 1 * 3 && py < 10 * 3)
                            fullTileBmp.SetPixel(px, py, interiorBmp.GetPixel(px - 5 * 3, py - 1 * 3));
                        else
                            fullTileBmp.SetPixel(px, py, i == 35 ? System.Drawing.Color.Black : BG);  // 35=DARK_X gets black border; all others (including PersonalPref) get transparent BG
                    }
                theFullTileBmpTable[i].Add(fullTileBmp);
            }
    }
    private static readonly bool _init4 = init4();
    private static bool init4()
    {
        initFull();
        return true;
    }

    private static System.Drawing.Bitmap[] make_linkIcons()
    {
        var imageStream = GetResourceStream("link_icons.png");
        var bmp = new System.Drawing.Bitmap(imageStream);
        var a = new List<System.Drawing.Bitmap>();
        for (int i = 0; i <= 3; i++)
        {
            var r = new System.Drawing.Bitmap(16, 16);
            for (int x = 0; x <= 15; x++)
                for (int y = 0; y <= 15; y++)
                {
                    var color = bmp.GetPixel(16 * i + x, y);
                    color = color.ToArgb() == System.Drawing.Color.Black.ToArgb() ? System.Drawing.Color.Transparent : color;
                    r.SetPixel(x, y, color);
                }
            a.Add(r);
        }
        return a.ToArray();
    }
    private static readonly System.Drawing.Bitmap[] _linkIcons = make_linkIcons();
    public static readonly System.Drawing.Bitmap linkFaceForward_bmp = _linkIcons[0];
    public static readonly System.Drawing.Bitmap linkRunRight_bmp = _linkIcons[1];
    public static readonly System.Drawing.Bitmap linkFaceRight_bmp = _linkIcons[2];
    public static readonly System.Drawing.Bitmap linkGotTheThing_bmp = _linkIcons[3];

    public static System.Drawing.Bitmap loadBMP(string filename)
    {
        var imageStream = GetResourceStream(filename);
        var bmp = new System.Drawing.Bitmap(imageStream);
        return bmp;
    }
    public static readonly System.Drawing.Bitmap mouseIconButtonColorsBMP = loadBMP("mouse-icon-button-colors.png");
    public static readonly System.Drawing.Bitmap mouseIconButtonColors2BMP = loadBMP("mouse-icon-button-colors-2.png");


    public static readonly System.Drawing.Bitmap takeAnyPotionBMP = loadBMP("take-any-potion.png");
    public static readonly System.Drawing.Bitmap takeAnyCandleBMP = loadBMP("take-any-candle.png");
    public static readonly System.Drawing.Bitmap takeAnyHeartBMP = loadBMP("take-any-heart.png");
    public static readonly System.Drawing.Bitmap takeAnyLeaveBMP = loadBMP("take-any-leave.png");
    public static readonly System.Drawing.Bitmap takeThisWoodSwordBMP = loadBMP("take-this-wood-sword.png");
    public static readonly System.Drawing.Bitmap takeThisCandleBMP = loadBMP("take-this-candle.png");
    public static readonly System.Drawing.Bitmap takeThisLeaveBMP = loadBMP("take-this-leave.png");

    public static readonly System.Drawing.Bitmap z1rSampleHintBMP = loadBMP("z1r-sample-hint.png");
    public static readonly System.Drawing.Bitmap allItemsHUDBestBMP = loadBMP("all-items-hud-pixels1.png");
    public static readonly System.Drawing.Bitmap allItemsHUDWorstBMP = loadBMP("all-items-hud-pixels1-worse.png");

    public static readonly System.Drawing.Bitmap firstQuestItemReferenceBMP = loadBMP("first-quest-item-reference.png");
    public static readonly System.Drawing.Bitmap secondQuestItemReferenceBMP = loadBMP("second-quest-item-reference.png");
    public static readonly System.Drawing.Bitmap mirrorOverworldBMP = loadBMP("mirror-overworld.png");
    public static readonly System.Drawing.Bitmap eyeballBMP = loadBMP("eyeball.png");
    public static readonly System.Drawing.Bitmap wsmsbuBMP = loadBMP("ws-ms-bu.png");

    /*
    code for clipping screenshots to same size

    let clipTakeAny(bmp:System.Drawing.Bitmap) =
        let r = new System.Drawing.Bitmap(672, 448)
        for x = 0 to 671 do
            for y = 0 to 447 do
                r.SetPixel(x,y,bmp.GetPixel(x+48, y+45))
        r
    let takeAnyPotionBMP = loadBMP("take-any-potion.png") |> clipTakeAny
    let takeAnyCandleBMP = loadBMP("take-any-candle.png") |> clipTakeAny
    let takeAnyHeartBMP = loadBMP("take-any-heart.png") |> clipTakeAny
    let takeAnyLeaveBMP = loadBMP("take-any-leave.png") |> clipTakeAny
    let takeThisWoodSwordBMP = loadBMP("take-this-wood-sword.png") |> clipTakeAny
    let takeThisCandleBMP = loadBMP("take-this-candle.png") |> clipTakeAny
    let takeThisLeaveBMP = loadBMP("take-this-leave.png") |> clipTakeAny

    do
        takeAnyPotionBMP.Save("takeAnyPotionBMP.png")
        takeAnyCandleBMP.Save("takeAnyCandleBMP.png")
        takeAnyHeartBMP.Save("takeAnyHeartBMP.png")
        takeAnyLeaveBMP.Save("takeAnyLeaveBMP.png")
        takeThisWoodSwordBMP.Save("takeThisWoodSwordBMP.png")
        takeThisCandleBMP.Save("takeThisCandleBMP.png")
        takeThisLeaveBMP.Save("takeThisLeaveBMP.png")
    */

    /*
    do
        let imageStream = GetResourceStream("icons3x7.png")
        let bmp37 = new System.Drawing.Bitmap(imageStream)
        let imageStream = GetResourceStream("icons7x7.png")
        let bmp = new System.Drawing.Bitmap(imageStream)
        let newBmp = new System.Drawing.Bitmap(bmp.Width+7, bmp.Height)
        for i = 0 to bmp.Width-1 do
            for j = 0 to bmp.Height-1 do
                newBmp.SetPixel(i,j,bmp.GetPixel(i,j))
        for i = 0 to 6 do
            for j = 0 to 6 do
                newBmp.SetPixel(bmp.Width+i, j, Drawing.Color.Transparent)
        for i = 0 to 2 do
            for j = 0 to 6 do
                newBmp.SetPixel(bmp.Width+i+2, j, bmp37.GetPixel(3*5+i,j))
        newBmp.Save("""C:\Users\Admin1\Source\Repos\Zelda1RandoTools\Z1R_Tracker\Z1R_WPF\tmp.png""")

    do
        let imageStream = GetResourceStream("icons7x7.png")
        let bmp = new System.Drawing.Bitmap(imageStream)
        let newBmp = new System.Drawing.Bitmap(bmp.Width, bmp.Height)
        for i = 0 to bmp.Width-1 do
            for j = 0 to bmp.Height-1 do
                let c = bmp.GetPixel(i,j)
                if c.A <> 0xFFuy then
                    newBmp.SetPixel(i,j,System.Drawing.Color.Black)
                else
                    newBmp.SetPixel(i,j,c)
        newBmp.Save("""C:\Users\Admin1\Source\Repos\Zelda1RandoTools\Z1R_Tracker\Z1R_WPF\tmp.png""")
    */

    public static System.Drawing.Bitmap swordLevelToBmp(int swordLevel) =>
        swordLevel switch
        {
            0 => greyscale(ow_sword1_item_bmp),
            1 => ow_sword1_item_bmp,
            2 => ow_sword2_item_bmp,
            3 => ow_sword3_item_bmp,
            _ => throw new Exception("bad SwordLevel"),
        };
    // the white sword with a yellow handle (instead of its usual blue one): shown in the item progress bar once a 4th sword has been collected
    public static System.Drawing.Bitmap white_sword4_bmp => ow_sword4_item_bmp;   // (the overworld sword icons are used everywhere; there are no plain sword pictures)
    // the overworld cave-style sword icons (the 4th sword has a blue handle), used instead of the plain sword pictures in the item progress bar and popouts
    public static System.Drawing.Bitmap swordLevelToOwBmp(int swordLevel) =>
        swordLevel switch
        {
            0 => greyscale(ow_sword1_item_bmp),   // no sword yet: the sword 1 icon, greyed out
            1 => ow_sword1_item_bmp,
            2 => ow_sword2_item_bmp,
            3 => ow_sword3_item_bmp,
            _ => throw new Exception("bad SwordLevel"),
        };
    public static System.Drawing.Bitmap ringLevelToBmp(int ringLevel) =>
        ringLevel switch
        {
            0 => greyscale(red_ring_bmp),
            1 => blue_ring_bmp,
            2 => red_ring_bmp,
            _ => throw new Exception("bad RingLevel"),
        };


    public static readonly GradientStops blockers_gsc = new GradientStops();
    private static readonly bool _init5 = init5();
    private static bool init5()
    {
        blockers_gsc.Add(new GradientStop(Color.FromArgb(255, 0, 180, 0), 0.0));
        blockers_gsc.Add(new GradientStop(Color.FromArgb(255, 40, 40, 40), 0.3));
        blockers_gsc.Add(new GradientStop(Color.FromArgb(255, 40, 40, 40), 0.7));
        blockers_gsc.Add(new GradientStop(Color.FromArgb(255, 180, 0, 0), 1.0));
        return true;
    }
    public static readonly LinearGradientBrush blockers_maybeBG = new LinearGradientBrush { GradientStops = blockers_gsc, StartPoint = new RelativePoint(0.0, 0.0, RelativeUnit.Relative), EndPoint = new RelativePoint(1.0, 1.0, RelativeUnit.Relative) };
    public static System.Drawing.Bitmap blockerHardCanonicalBMP(TrackerModel.DungeonBlocker current) =>
        current.Tag switch
        {
            TrackerModel.DungeonBlocker.Tags.COMBAT => ow_sword2_item_bmp,
            TrackerModel.DungeonBlocker.Tags.BOW_AND_ARROW or TrackerModel.DungeonBlocker.Tags.MAYBE_BOW_AND_ARROW => bow_and_arrow_bmp,
            TrackerModel.DungeonBlocker.Tags.RECORDER or TrackerModel.DungeonBlocker.Tags.MAYBE_RECORDER => recorder_bmp,
            TrackerModel.DungeonBlocker.Tags.LADDER or TrackerModel.DungeonBlocker.Tags.MAYBE_LADDER => ladder_bmp,
            TrackerModel.DungeonBlocker.Tags.BAIT or TrackerModel.DungeonBlocker.Tags.MAYBE_BAIT => bait_bmp,
            TrackerModel.DungeonBlocker.Tags.KEY or TrackerModel.DungeonBlocker.Tags.MAYBE_KEY => magic_key_bmp,
            TrackerModel.DungeonBlocker.Tags.BOMB or TrackerModel.DungeonBlocker.Tags.MAYBE_BOMB => bomb_bmp,
            TrackerModel.DungeonBlocker.Tags.MONEY or TrackerModel.DungeonBlocker.Tags.MAYBE_MONEY => rupee_bmp,
            TrackerModel.DungeonBlocker.Tags.NOTHING => null,
            _ => throw new ArgumentOutOfRangeException(nameof(current)),   // (F#: the match is complete)
        };
    public static Canvas blockerCurrentDisplay(TrackerModel.DungeonBlocker current)
    {
        var innerc = new Canvas { Width = 24.0, Height = 24.0, Background = Brushes.Transparent, IsHitTestVisible = false };  // just has item drawn on it, not the box
        innerc.Children.Clear();
        innerc.Background = current.Tag switch
                            {
                                TrackerModel.DungeonBlocker.Tags.MAYBE_LADDER
                                or TrackerModel.DungeonBlocker.Tags.MAYBE_BAIT
                                or TrackerModel.DungeonBlocker.Tags.MAYBE_BOMB
                                or TrackerModel.DungeonBlocker.Tags.MAYBE_BOW_AND_ARROW
                                or TrackerModel.DungeonBlocker.Tags.MAYBE_KEY
                                or TrackerModel.DungeonBlocker.Tags.MAYBE_MONEY
                                or TrackerModel.DungeonBlocker.Tags.MAYBE_RECORDER
                                    => (IBrush)blockers_maybeBG,
                                _ => (IBrush)Brushes.Black,
                            };
        var bmp = blockerHardCanonicalBMP(current);
        if (bmp != null)
        {
            var image = BMPtoImage(bmp);
            image.IsHitTestVisible = false;
            canvasAdd(innerc, image, 1.0, 1.0);
        }
        return innerc;
    }

    public static readonly IImmutableSolidColorBrush skipped = Brushes.MediumPurple;
    public static void placeSkippedItemXDecorationImpl(Canvas innerc, double size)
    {
        innerc.Children.Add(new Line { Stroke = skipped, StrokeThickness = 3.0, StartPoint = new Point(0.0, 0.0), EndPoint = new Point(size, size), IsHitTestVisible = false });
        innerc.Children.Add(new Line { Stroke = skipped, StrokeThickness = 3.0, StartPoint = new Point(size, 0.0), EndPoint = new Point(0.0, size), IsHitTestVisible = false });
    }
    public static void placeSkippedItemXDecoration(Canvas innerc) => placeSkippedItemXDecorationImpl(innerc, 30.0);


    // (WPF: "Z1R_WPF.exe". Used for restarting, the single-instance mutex name and the Windows desktop shortcut.)
    public static readonly string ExeName = System.IO.Path.GetFileName(Platform.ExecutablePath());
    public static void RestartTheApplication()
    {
        var cwd = System.IO.Path.GetDirectoryName(Platform.ExecutablePath());
        var thisExe = System.IO.Path.Combine(cwd, ExeName);
        System.Diagnostics.Process.Start(thisExe);
        switch (Application.Current.ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime d when d.MainWindow != null: d.MainWindow.Close(); break;
            default: break;
        }
    }

    // ideas from
    // https://kent-boogaart.com/blog/dispatcher-frames
    // from https://stackoverflow.com/questions/4502037/where-is-the-application-doevents-in-wpf
    // see also
    // https://stackoverflow.com/questions/21248643/how-can-i-convert-win32-mouse-messages-to-wpf-mouse-events
    public static bool sawMouseEvent = false;
    public static DispatcherTimer theTimer = null;
    public static DispatcherFrame theFrame = null;
    public static int theCounter = 0;
    public static void setup(Window w, Visual _v)
    {
        if (theTimer == null)
        {
            dpi = w.RenderScaling;
            theTimer = new DispatcherTimer(DispatcherPriority.Render); // this only runs for a very short time when warping mouse; use high priority to avoid noticeable latency
            theTimer.Stop();
            theTimer.Interval = TimeSpan.FromSeconds(0.03);
            theTimer.Tick += (_, _) =>
            {
                if (theFrame != null)   // avoid a race
                {
                    if (!sawMouseEvent && theCounter > 4)
                        //printfn "uh oh too slow"     // or, the mouse didn't move at all
                        sawMouseEvent = true;
                    if (sawMouseEvent)
                    {
                        theFrame.Continue = false;
                        theTimer.Stop();
                    }
                    else
                        theCounter = theCounter + 1;
                }
            };
            w.PointerMoved += (_, _) => sawMouseEvent = true;
        }
    }
    // true while a warp's message pump runs (a popup about to open warps the cursor first; the stuck-popup self-heal must leave it alone)
    public static bool IsWarpingCursor => theFrame != null;
    public static void SilentlyWarpMouseCursorTo(Point pos)
    {
        if (theFrame == null)  // Note: cannot process a second of these calls while one is active; I think that's fine
        {
            theFrame = new DispatcherFrame();
            try
            {
                sawMouseEvent = false;
                theCounter = 0;
                theTimer.Start();
                if (Win32.SetCursor(pos.X, pos.Y))    // this queues a mouse move input, and we want to block until that event is processed, to avoid spurious MouseLeave()s and other reasons
                    Dispatcher.UIThread.PushFrame(theFrame);   // creates a blocking message pump that only stops when Continue set to false
                else
                    theTimer.Stop();   // (this platform can't warp the cursor, so there's no mouse move to wait for)
            }
            finally
            {
                // (a failure here used to leave theFrame set, so every later warp did nothing for the rest of the session)
                theTimer.Stop();
                theFrame = null;
            }
        }
    }
    public static void WarpMouseCursorTo(Point pos)
    {
        SilentlyWarpMouseCursorTo(pos);
        PlaySoundForSpeechRecognizedAndUsedToMark();
    }
    public static void NavigationallyWarpMouseCursorTo(Point pos)   // can abstract over whether keyboard 'arrow' keys play the sound or not
    {
        SilentlyWarpMouseCursorTo(pos);
        // (WPF has commented-out code here that turned on mouse sonar and pressed Ctrl to show where the cursor went)
    }

    // https://learn.microsoft.com/en-us/archive/blogs/greg_schechter/more-details-on-how-effects-work
    // https://learn.microsoft.com/en-us/archive/blogs/jgoldb/software-rendering-usage-in-wpf
    public static readonly bool canUseEffectsWithoutDestroyingPerformance =
        // (System.Windows.Media.RenderCapability.Tier >>> 16) >= 2
        // System.Windows.Media.RenderCapability.IsPixelShaderVersionSupported(2s,0s)
        // none of the above worked in practice, so there is a flag in the settings file instead
        TrackerModelOptions.UseBlurEffects.Value;

    // (port helper, not in Graphics.fs: an F# `Event<T>` that one file creates and passes to others, which subscribe or `Trigger` it
    // (e.g. WPFUI's `blockersHoverEvent`, passed to UIComponents and DungeonUI/DungeonHighlightsUI). A C# event can't be passed around
    // or raised from outside its class, so such a value is a SharedEvent<T>: WPF's `new Event<bool>()` → `new Graphics.SharedEvent<bool>()`,
    // `ev.Trigger(x)` → `ev.Trigger(x)`, `ev.Publish.Add(f)` → `ev.Fired += f`.)
    public sealed class SharedEvent<T>
    {
        public event Action<T> Fired;
        public void Trigger(T x) => Fired?.Invoke(x);
    }
    // (the same for an F# `Event<unit>`: WPF's `new Event<unit>()` → `new Graphics.SharedEvent()`, `ev.Trigger()`, `ev.Publish.Add(fun _ -> f())` → `ev.Fired += f`)
    public sealed class SharedEvent
    {
        public event Action Fired;
        public void Trigger() => Fired?.Invoke();
    }
}
