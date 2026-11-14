// Ported from Z1R_WPF/Views.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// Every F# `async { } |> Async.StartImmediate` is a `CustomComboBoxes.StartImmediate(...)` of an async lambda, as in Dungeon.cs.
// F# model events (TrackerModel's IEvent<_>s) keep their F# type; `e.Add(f)` becomes `onEvent(e, f)` (an FSharpHandler, as Timeline.cs does).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.FSharp.Control;

namespace Z1R_Avalonia;

public static class Views
{
    public static void canvasAdd(Canvas c, Control item, double left, double top) => Graphics.canvasAdd(c, item, left, top);
    // (port helper: WPF's `modelEvent.Add(f)` on an F# IEvent of the model)
    private static void onEvent<T>(IEvent<FSharpHandler<T>, T> e, Action<T> f) => e.AddHandler(new FSharpHandler<T>((_, x) => f(x)));
    // Keeps a subscription only while the owner is in the visual tree. A view that is thrown away (a redrawn overworld tile, a closed or rebuilt window)
    // stops listening when it leaves the tree. If it comes back, it subscribes again and refreshes, since it missed the changes made while it was out.
    public static void WhileInTree(Control owner, Action subscribe, Action unsubscribe, Action refresh)
    {
        var on = true;
        subscribe();
        owner.AttachedToVisualTree += (_, _) => { if (!on) { on = true; subscribe(); refresh?.Invoke(); } };
        owner.DetachedFromVisualTree += (_, _) => { if (on) { on = false; unsubscribe(); } };
    }
    private static void onEventInTree<T>(Control owner, IEvent<FSharpHandler<T>, T> e, Action<T> f, Action refresh)
    {
        var h = new FSharpHandler<T>((_, x) => f(x));
        WhileInTree(owner, () => e.AddHandler(h), () => e.RemoveHandler(h), refresh);
    }
    // the 'new location known' logic of SynthesizeANewLocationKnownEvent, for a view: subscribed only while the view is in the tree
    private static void onNewLocationKnownInTree<T>(Control owner, IEvent<FSharpHandler<T>, T> mapChoiceDomainChangePublished, Action f, Action refresh)
    {
        var needDetailAboutNewLocation = false;
        onEventInTree(owner, mapChoiceDomainChangePublished, _ => needDetailAboutNewLocation = true, refresh);
        onEventInTree(owner, TrackerModel.mapStateSummaryComputedEvent.Publish, _ =>
        {
            if (needDetailAboutNewLocation)
            {
                needDetailAboutNewLocation = false;
                f();
            }
        }, null);
    }

    public class GlobalBoxMouseOverHighlight
    {
        private readonly Rectangle globalBoxMouseOverHighlight;
        private Action<bool, Control> setGlobalBoxMouseOverHighlight = (_b, _e) => { };
        public GlobalBoxMouseOverHighlight(IBrush stroke)
        {
            globalBoxMouseOverHighlight = new Rectangle { Width = 34.0, Height = 34.0, Stroke = stroke, StrokeThickness = 2.0, Opacity = 0.0, IsHitTestVisible = false };
        }
        public GlobalBoxMouseOverHighlight() : this(Brushes.DarkTurquoise) { }   // the usual turquoise box
        public void DetachFromGlobalCanvas(Canvas c) =>
            c.Children.Remove(globalBoxMouseOverHighlight);
        public void AttachToGlobalCanvas(Canvas c)
        {
            canvasAdd(c, globalBoxMouseOverHighlight, 0.0, 0.0);
            globalBoxMouseOverHighlight.ZIndex = 99999;   // (WPF: Canvas.SetZIndex)
            setGlobalBoxMouseOverHighlight = (b, e) =>
            {
                if (b)
                {
                    var maybePos = e.TranslatePoint(new Point(-2.0, -2.0), c);   // (null when e was removed from the tree, e.g. its cell was rebuilt under the pointer)
                    if (maybePos == null) { globalBoxMouseOverHighlight.Opacity = 0.0; return; }
                    var pos = maybePos.Value;
                    Canvas.SetLeft(globalBoxMouseOverHighlight, pos.X);
                    Canvas.SetTop(globalBoxMouseOverHighlight, pos.Y);
                    globalBoxMouseOverHighlight.Opacity = 1.0;
                }
                else
                    globalBoxMouseOverHighlight.Opacity = 0.0;
            };
        }
        public void ApplyBehavior(Control e)
        {
            e.PointerEntered += (_, _) => setGlobalBoxMouseOverHighlight(true, e);
            e.PointerExited += (_, _) => setGlobalBoxMouseOverHighlight(false, e);
        }
    }
    public static readonly GlobalBoxMouseOverHighlight appMainCanvasGlobalBoxMouseOverHighlight = new GlobalBoxMouseOverHighlight();

    public static readonly Color HHS = Colors.Yellow;
    public static readonly Color HHE = Colors.DarkGreen;
    // (WPF: `new LinearGradientBrush(HHS, HHE, 45.)`, which runs from (0,0) to (cos 45°, sin 45°) of the bounding box; Graphics.freeze is identity)
    private static IBrush make_hintHighlightBrush()
    {
        var r = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.0, 0.0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(Math.Cos(45.0 * Math.PI / 180.0), Math.Sin(45.0 * Math.PI / 180.0), RelativeUnit.Relative)
        };
        r.GradientStops.Add(new GradientStop(HHS, 0.0));
        r.GradientStops.Add(new GradientStop(HHE, 1.0));
        return r;
    }
    public static readonly IBrush hintHighlightBrush = make_hintHighlightBrush();
    public static Rectangle makeHintHighlight(double size) => new Rectangle { Width = size, Height = size, StrokeThickness = 0.0, Fill = hintHighlightBrush };

    /*
    The rest of this module is for reusable display elements with the following properties:
     - they represent a display of some portion of the TrackerModel
     - they can redraw themselves by listening for changes to the TrackerModel, and never need to otherwise be redrawn, as their state is entirely TrackerModel-evented
     - they might optionally also have interactive/update abilities to change the model (which will, of course, be reflected back in their display view)
    */

    public static System.Drawing.Bitmap emptyUnfoundTriforce_bmp(int i) =>
        TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.Kind.Tag switch
        {
            TrackerModel.DungeonTrackerInstanceKind.Tags.HIDE_DUNGEON_NUMBERS => Graphics.emptyUnfoundLetteredTriforce_bmps[i],
            TrackerModel.DungeonTrackerInstanceKind.Tags.DEFAULT => Graphics.emptyUnfoundNumberedTriforce_bmps[i],
            _ => throw new Exception("unreachable")
        };
    public static System.Drawing.Bitmap emptyFoundTriforce_bmp(int i) =>
        TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.Kind.Tag switch
        {
            TrackerModel.DungeonTrackerInstanceKind.Tags.HIDE_DUNGEON_NUMBERS => Graphics.emptyFoundLetteredTriforce_bmps[i],
            TrackerModel.DungeonTrackerInstanceKind.Tags.DEFAULT => Graphics.emptyFoundNumberedTriforce_bmps[i],
            _ => throw new Exception("unreachable")
        };
    public static System.Drawing.Bitmap fullUnfoundTriforce_bmp(int i) =>
        TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.Kind.Tag switch
        {
            TrackerModel.DungeonTrackerInstanceKind.Tags.HIDE_DUNGEON_NUMBERS => Graphics.fullLetteredUnfoundTriforce_bmps[i],
            TrackerModel.DungeonTrackerInstanceKind.Tags.DEFAULT => Graphics.fullNumberedUnfoundTriforce_bmps[i],
            _ => throw new Exception("unreachable")
        };
    public static System.Drawing.Bitmap fullFoundTriforce_bmp(int i) =>
        TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.Kind.Tag switch
        {
            TrackerModel.DungeonTrackerInstanceKind.Tags.HIDE_DUNGEON_NUMBERS => Graphics.fullLetteredFoundTriforce_bmps[i],
            TrackerModel.DungeonTrackerInstanceKind.Tags.DEFAULT => Graphics.fullNumberedFoundTriforce_bmps[i],
            _ => throw new Exception("unreachable")
        };

    // make a superscript icon to help visualize implications of 'Force OW block' rando flag
    // (owInstanceOpt: null for None)
    public static void drawTinyIconIfLocationIsOverworldBlock(Canvas c, OverworldData.OverworldInstance owInstanceOpt, Tuple<int, int> location)
    {
        if (owInstanceOpt != null)
        {
            // (the instance passed in only says 'draw it'; the current one is read here, so a change of overworld template is followed)
            var owInstance = TrackerModel.owInstance ?? owInstanceOpt;
            if (!location.Equals(TrackerModel.NOTFOUND))
            {
                // mark overworld block in the upper right corner
                var icon =
                    owInstance.Raftable(location.Item1, location.Item2) ?
                        Graphics.BMPtoImage(Graphics.raft_bmp)
                    : owInstance.PowerBraceletable(location.Item1, location.Item2) ?
                        Graphics.BMPtoImage(Graphics.power_bracelet_bmp)
                    : owInstance.Ladderable(location.Item1, location.Item2) ?
                        Graphics.BMPtoImage(Graphics.ladder_bmp)
                    : owInstance.Whistleable(location.Item1, location.Item2) ?
                        Graphics.BMPtoImage(Graphics.recorder_bmp)
                    :
                        null;
                if (icon != null)
                {
                    icon.Width = 7.0;
                    icon.Height = 7.0;
                    canvasAdd(c, icon, 23.0, 5.0);
                }
            }
        }
    }
    // (WPF returns the synthesized event's IEvent, to which callers `.Add` handlers; here it's a small C# event holder: `newLocation.Fired += f`.
    // WPF callers that pass `someEvent |> Event.filter p` pass the event and `p` as filter.)
    public sealed class NewLocationKnownEvent
    {
        public event Action Fired;
        internal void Trigger() => Fired?.Invoke();
    }
    public static NewLocationKnownEvent SynthesizeANewLocationKnownEvent<T>(IEvent<FSharpHandler<T>, T> mapChoiceDomainChangePublished, Func<T, bool> filter = null)
    {
        var resultEvent = new NewLocationKnownEvent();
        // if location changes...
        var needDetailAboutNewLocation = false;
        onEvent(mapChoiceDomainChangePublished, x =>
        {
            if (filter == null || filter(x))
                needDetailAboutNewLocation = true;
        });
        // ... Trigger after we can look up its new location coordinates
        onEvent(TrackerModel.mapStateSummaryComputedEvent.Publish, _ =>
        {
            if (needDetailAboutNewLocation)
            {
                resultEvent.Trigger();
                needDetailAboutNewLocation = false;
            }
        });
        return resultEvent;
    }

    // putting the mouse in the very center of triforce/item boxes (30x30) kind of obscures the numeral or pixel art
    // if we are warping the cursor to one of these, ideally put it here in the box, to look nice
    public static readonly double IDEAL_BOX_MOUSE_X = 22.0;
    public static readonly double IDEAL_BOX_MOUSE_Y = 17.0;

    // a not-yet-collected blue/red ring, blue/red candle, wood/silver arrow gets a small grey letter (B / R / W / S) so it is easy to tell them apart
    // (returns null for None)
    public static string uncollectedTierLetter(int ident)
    {
        if (ident == TrackerModel.ITEMS.BLUERING || ident == TrackerModel.ITEMS.BLUECANDLE) return "B";
        else if (ident == TrackerModel.ITEMS.WOODARROW) return "W";
        else if (ident == TrackerModel.ITEMS.REDRING || ident == TrackerModel.ITEMS.REDCANDLE) return "R";
        else if (ident == TrackerModel.ITEMS.SILVERARROW) return "S";
        else if (ident == TrackerModel.ITEMS.BOOMERANG) return (TrackerModel.BootsReplaceBoomerangs ? "B" : "W");   // boomerang W / boots B
        else if (ident == TrackerModel.ITEMS.MAGICBOOMERANG) return "M";   // magic boomerang / magical boots
        else return null;
    }
    public static Control makeTierLetter(string letter, double size)
    {
        // the colored letter with a 1px white outline (the same letter in white drawn behind it, shifted one pixel in every direction)
        IBrush colorBrush = letter switch
        {
            "B" or "W" => Brushes.DodgerBlue,
            "P1" or "P2" or "P3" or "P4" => new SolidColorBrush(Color.FromRgb(176, 112, 255)),
            _ => Brushes.Red
        };
        var c = new Canvas { Width = 30.0, Height = size * 1.4, IsHitTestVisible = false };
        var fontSize = (letter == "W" ? size * 0.9 : letter.Length > 1 ? size * 0.85 : size);   // the W is a wide letter: 10% smaller
        var vShift = (letter == "W" ? size * 0.05 : 0.0);
        void mk(IBrush brush, double dx, double dy0)
        {
            var dy = dy0 + vShift;
            var tb = new TextBlock { Text = letter, FontSize = fontSize, FontWeight = FontWeight.Bold, Foreground = brush, Width = 30.0, TextAlignment = TextAlignment.Center, IsHitTestVisible = false };
            Canvas.SetLeft(tb, dx);
            Canvas.SetTop(tb, dy);
            c.Children.Add(tb);
        }
        foreach (var (dx, dy) in new[] { (-1.0, 0.0), (1.0, 0.0), (0.0, -1.0), (0.0, 1.0), (-1.0, -1.0), (1.0, -1.0), (-1.0, 1.0), (1.0, 1.0) })
            mk(Brushes.White, dx, dy);
        mk(colorBrush, 0.0, 0.0);
        return c;
    }
    // 'Progressive Items': a bright golden halo behind the full-colour upgraded item that swells and shrinks again; the caller adds it where the item is and removes it after 3 seconds
    // the ZIndex each glow-raised element had before its first glow, and how many glows still hold it up
    private static readonly Dictionary<Control, (int, int)> glowRaised = new Dictionary<Control, (int, int)>();
    public static Control makeProgressiveGlow(System.Drawing.Bitmap bmp, double size)
    {
        var root = new Canvas { Width = size, Height = size, IsHitTestVisible = false };
        var d = size * 2.8;
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 240, 140), 0.0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(220, 255, 205, 0), 0.4));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 170, 0), 1.0));
        var halo = new Ellipse { Width = d, Height = d, Fill = brush, IsHitTestVisible = false };
        Canvas.SetLeft(halo, (size - d) / 2.0);
        Canvas.SetTop(halo, (size - d) / 2.0);
        root.Children.Add(halo);
        // the item keeps its black outline (like the map icons): draw it on a copy that has the outline added around every opaque pixel
        var B = 2;
        var outlined = new System.Drawing.Bitmap(bmp.Width + 2 * B, bmp.Height + 2 * B);
        for (int x = 0; x <= bmp.Width - 1; x++)
            for (int y = 0; y <= bmp.Height - 1; y++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.A > 0 && ((int)p.R + (int)p.G + (int)p.B) > 40)
                    for (int dx = 0; dx <= 2 * B; dx++)
                        for (int dy = 0; dy <= 2 * B; dy++)
                            outlined.SetPixel(x + dx, y + dy, System.Drawing.Color.Black);
            }
        for (int x = 0; x <= bmp.Width - 1; x++)
            for (int y = 0; y <= bmp.Height - 1; y++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.A > 0) outlined.SetPixel(x + B, y + B, p);
            }
        var scale = size / (double)bmp.Width;
        var shown = (double)outlined.Width * scale;
        var img = Graphics.BMPtoImage(outlined);   // (the normal, undimmed colours)
        img.Stretch = Stretch.Uniform;
        img.Width = shown;
        img.Height = (double)outlined.Height * scale;
        Canvas.SetLeft(img, (size - shown) / 2.0);
        Canvas.SetTop(img, (size - (double)outlined.Height * scale) / 2.0);
        img.IsHitTestVisible = false;
        // (WPF's DropShadowEffect has ShadowDepth/Direction; Avalonia's equivalent is DropShadowDirectionEffect)
        img.Effect = new DropShadowDirectionEffect { Color = Colors.Gold, BlurRadius = size * 1.3, ShadowDepth = 0.0, Opacity = 1.0 };
        root.Children.Add(img);
        root.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        var st = new ScaleTransform(1.0, 1.0);
        root.RenderTransform = st;
        // (WPF: a DoubleAnimation 1 -> 1.9 over 1.5 s with AutoReverse, on ScaleX and ScaleY. Tweened by hand on a timer, linear like WPF's default,
        // as Graphics.TileHighlightRectangle does; after 3 s it holds 1.0, where WPF's reversed animation ends.)
        var start = DateTime.Now;
        var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16.0), DispatcherPriority.Render, (_, _) => { });
        timer.Tick += (_, _) =>
        {
            var t = (DateTime.Now - start).TotalSeconds;
            var f = t < 1.5 ? t / 1.5 : Math.Max(0.0, (3.0 - t) / 1.5);
            var s = 1.0 + (1.9 - 1.0) * f;
            st.ScaleX = s;
            st.ScaleY = s;
            if (t >= 3.0) timer.Stop();
        };
        timer.Start();
        return root;
    }
    public static readonly List<Action> redrawTriforces = new List<Action>();
    public static readonly bool[] triforceBlocked = new bool[9];   // middle-clicking a dungeon's triforce above it puts a red X on it, and it can't be collected until the X is removed (this session only)
    public static Canvas MakeTriforceDisplayView(CustomComboBoxes.CanvasManager cmo, int trackerIndex, OverworldData.OverworldInstance owInstanceOpt)    // cmo non-null means "makeInteractive" (WPF: Some(cm)); owInstanceOpt null for None
    {
        var emptyUnfoundTriImage = Graphics.BMPtoImage(emptyUnfoundTriforce_bmp(trackerIndex));
        var emptyFoundTriImage   = Graphics.BMPtoImage(emptyFoundTriforce_bmp(trackerIndex));
        var fullUnfoundTriImage  = Graphics.BMPtoImage(fullUnfoundTriforce_bmp(trackerIndex));
        var fullFoundTriImage    = Graphics.BMPtoImage(fullFoundTriforce_bmp(trackerIndex));
        var innerc = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black };
        var dungeon = TrackerModel.GetDungeon(trackerIndex);
        void redraw()
        {
            innerc.Children.Clear();
            var found = dungeon.HasBeenLocated();
            if (!TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
            {
                if (!found && !TrackerModel.GetLevelHint.Invoke(trackerIndex).Equals(TrackerModel.HintZone.UNKNOWN))
                    innerc.Children.Add(makeHintHighlight(30.0));
            }
            else
            {
                var label = dungeon.LabelChar;
                if (label >= '1' && label <= '8')
                {
                    var hintIndex = (int)label - (int)'1';
                    if (!found && !TrackerModel.GetLevelHint.Invoke(hintIndex).Equals(TrackerModel.HintZone.UNKNOWN))
                        innerc.Children.Add(makeHintHighlight(30.0));
                }
            }
            if (!dungeon.PlayerHasTriforce())
                innerc.Children.Add(!found ? emptyUnfoundTriImage : emptyFoundTriImage);
            else
                innerc.Children.Add(!found ? fullUnfoundTriImage : fullFoundTriImage);
            drawTinyIconIfLocationIsOverworldBlock(innerc, owInstanceOpt, TrackerModel.mapStateSummary.DungeonLocations[trackerIndex]);
            if (TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON != 3)
                throw new Exception("This UI was designed for 3 blockers per dungeon");
            if (!dungeon.PlayerHasTriforce()) // draw specific blockers
            {
                var i = trackerIndex;
                for (int j = 0; j <= 2; j++)
                {
                    if (i != 8 && TrackerModel.DungeonBlockersContainer.GetDungeonBlockerAppliesTo(i, j, 2))
                    {
                        var blocker = TrackerModel.DungeonBlockersContainer.GetDungeonBlocker(i, j);
                        var bmp = Graphics.blockerHardCanonicalBMP(blocker);
                        if (bmp != null)
                        {
                            var img = Graphics.BMPtoImage(bmp);
                            img.Width = 7.0;
                            img.Height = 7.0;
                            if (!blocker.PlayerCouldBeBlockedByThis())
                            {
                                canvasAdd(innerc, new Canvas { Width = 9.0, Height = 9.0, Background = Brushes.Lime },  (double)(1 + j * 10), 7.0 + 16.0);
                                canvasAdd(innerc, new Canvas { Width = 7.0, Height = 7.0, Background = Brushes.Black }, (double)(2 + j * 10), 8.0 + 16.0);
                            }
                            else
                                canvasAdd(innerc, new Canvas { Width = 9.0, Height = 9.0, Background = Brushes.Black }, (double)(1 + j * 10), 7.0 + 16.0);
                            canvasAdd(innerc, img, (double)(2 + j * 10), 8.0 + 16.0);
                        }
                    }
                }
            }
            if (triforceBlocked[trackerIndex])
            {
                // a red X over the whole triforce
                canvasAdd(innerc, new Line { StartPoint = new Point(3.0, 3.0), EndPoint = new Point(27.0, 27.0), Stroke = Brushes.Red, StrokeThickness = 4.0, IsHitTestVisible = false }, 0.0, 0.0);
                canvasAdd(innerc, new Line { StartPoint = new Point(27.0, 3.0), EndPoint = new Point(3.0, 27.0), Stroke = Brushes.Red, StrokeThickness = 4.0, IsHitTestVisible = false }, 0.0, 0.0);
            }
        }
        redraw();
        // interactions
        if (cmo != null)
        {
            var cm = cmo;
            innerc.PointerPressed += (_, ea) =>
            {
                if (!CustomComboBoxes.GlobalFlag.popupIsActive && ea.GetCurrentPoint(innerc).Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonPressed)
                {
                    triforceBlocked[trackerIndex] = !triforceBlocked[trackerIndex];
                    ea.Handled = true;
                    redraw();
                }
                else if (!CustomComboBoxes.GlobalFlag.popupIsActive && !triforceBlocked[trackerIndex])
                {
                    dungeon.ToggleTriforce();
                    if (dungeon.PlayerHasTriforce() && TrackerModel.IsHiddenDungeonNumbers.Invoke(null) && dungeon.LabelChar == '?')
                    {
                        // if it's hidden dungeon numbers, the player just got a triforce, and the player has not yet set the dungeon number, then popup the number chooser
                        CustomComboBoxes.GlobalFlag.popupIsActive = true;
                        var pos = innerc.TranslatePoint(new Point(15.0, 15.0), cm.AppMainCanvas).Value;
                        CustomComboBoxes.StartImmediate(((Func<Task>)(async () =>
                        {
                            await Dungeon.HiddenDungeonCustomizerPopup(cm, trackerIndex, dungeon.Color, dungeon.LabelChar, true, false, pos);
                            CustomComboBoxes.GlobalFlag.popupIsActive = false;
                        }))());
                    }
                }
            };
            Dungeon.HotKeyAHiddenDungeonLabel(innerc, dungeon, null);
            appMainCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(innerc);
        }
        // (every subscription below lasts only while this view is in the visual tree, so thrown-away copies stop listening)
        // redraw if PlayerHas changes
        onEventInTree(innerc, dungeon.PlayerHasTriforceChanged, _ => redraw(), redraw);
        // redraw after we can look up its new location coordinates
        onNewLocationKnownInTree(innerc, dungeon.HasBeenLocatedChanged, redraw, null);
        // redraw if hinting changes
        if (!TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
            onEventInTree(innerc, TrackerModel.LevelHintChanged.Invoke(trackerIndex), _ => redraw(), null);
        else
            for (int i = 0; i <= 7; i++) onEventInTree(innerc, TrackerModel.LevelHintChanged.Invoke(i), _ => redraw(), null);   // just redraw on any hints, rather than try to subscribe/unsubscribe based on LabelChar changes
        // redraw if label changed, as that can (un)link an existing hint; or if blockers changed (may need to rewdraw specific-blocker)
        onEventInTree(innerc, dungeon.HiddenDungeonColorOrLabelChanged, _ => redraw(), null);
        onEventInTree(innerc, TrackerModel.DungeonBlockersContainer.AnyBlockerChanged, _ => redraw(), null);
        Action redrawA = redraw;
        WhileInTree(innerc, () => redrawTriforces.Add(redrawA), () => redrawTriforces.Remove(redrawA), null);
        return innerc;
    }
    public static Canvas MakeLevel9View(OverworldData.OverworldInstance owInstanceOpt)   // (owInstanceOpt: null for None)
    {
        var level9NumeralCanvas = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black };
        var dungeon = TrackerModel.GetDungeon(8);
        void redraw()
        {
            level9NumeralCanvas.Children.Clear();
            var l9found = dungeon.HasBeenLocated();
            var img = Graphics.BMPtoImage(!l9found ? Graphics.unfoundL9_bmp : Graphics.foundL9_bmp);
            if (!l9found && !TrackerModel.GetLevelHint.Invoke(8).Equals(TrackerModel.HintZone.UNKNOWN))
                canvasAdd(level9NumeralCanvas, makeHintHighlight(30.0), 0.0, 0.0);
            canvasAdd(level9NumeralCanvas, img, 0.0, 0.0);
            drawTinyIconIfLocationIsOverworldBlock(level9NumeralCanvas, owInstanceOpt, TrackerModel.mapStateSummary.DungeonLocations[8]);
        }
        redraw();
        // (these subscriptions last only while this view is in the visual tree)
        // redraw after we can look up its new location coordinates
        onNewLocationKnownInTree(level9NumeralCanvas, dungeon.HasBeenLocatedChanged, redraw, redraw);
        // redraw if hinting changes
        onEventInTree(level9NumeralCanvas, TrackerModel.LevelHintChanged.Invoke(8), _ => redraw(), null);
        return level9NumeralCanvas;
    }
    public static Canvas MakeColorNumberCanvasForHDN(int dungeonIndex)
    {
        // color/number canvas
        if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
        {
            var colorCanvas = new Canvas { Width = 28.0, Height = 28.0, Background = Brushes.Black };
            var d = TrackerModel.GetDungeon(dungeonIndex);
            void redraw(int color0, char labelChar)
            {
                colorCanvas.Background = new SolidColorBrush(Graphics.makeColor(color0));
                colorCanvas.Children.Clear();
                var color = Graphics.isBlackGoodContrast(color0) ? System.Drawing.Color.Black : System.Drawing.Color.White;
                if (d.LabelChar != '?')
                    colorCanvas.Children.Add(Graphics.BMPtoImage(Graphics.alphaNumOnTransparentBmp(labelChar, color, 28, 28, 3, 2)));
            }
            redraw(d.Color, d.LabelChar);
            onEventInTree(colorCanvas, d.HiddenDungeonColorOrLabelChanged, t => redraw(t.Item1, t.Item2), () => redraw(d.Color, d.LabelChar));   // (only while in the visual tree)
            return colorCanvas;
        }
        else
            return null;
    }

    public static readonly List<Action> redrawBoxes = new List<Action>();
    private static bool init1()
    {
        onEvent(TrackerModel.IsCurrentlyBookChanged, _ =>
        {
            TrackerModel.forceUpdate();
            foreach (var f in redrawBoxes.ToArray())   // (a copy: boxes leaving the visual tree remove themselves from the list)
                f();
        });
        onEvent(TrackerModel.DungeonBlockersContainer.AnyBlockerChanged, _ =>
        {
            foreach (var f in redrawBoxes.ToArray())
                f();
        });
        onEvent(TrackerModel.IsWSMSReplacedByBUChanged, _ =>
        {
            foreach (var f in redrawBoxes.ToArray())
                f();
        });
        return true;
    }
    private static readonly bool _init1 = init1();
    // boxes whose empty outline is always drawn red (never the white 'skipped and empty' look), e.g. the four item boxes in the top right
    public static readonly HashSet<TrackerModel.Box> redBorderBoxes = new HashSet<TrackerModel.Box>();
    // when a hint says the raft / recorder / power bracelet is (or is not) needed, those items get a red dashed border wherever they appear
    public static event Action hintDashedItemsChanged;
    public static void triggerHintDashedItemsChanged() => hintDashedItemsChanged?.Invoke();
    public static bool itemIsHintDashed(int item) =>
        (item == TrackerModel.ITEMS.RAFT && TrackerModel.SailNotHintWasGiven) ||
        (item == TrackerModel.ITEMS.POWERBRACELET && TrackerModel.NoFeatOfStrengthHintWasGiven);
    public static Control makeHintDashedRect(double w, double h) =>
        new Rectangle { Width = w, Height = h, Stroke = Brushes.Red, StrokeThickness = 2.0, StrokeDashArray = new AvaloniaList<double>(new[] { 3.0, 2.0 }), IsHitTestVisible = false };
    // (cmo: null for None. computeExtraDecorationsWhenPopupActivatedOrMouseOverOpt: null for None. Returns the box and its activateComboBox(activationDelta, gridClickDismissalWarpReturn).)
    public static (Canvas, Action<int, CustomComboBoxes.GridClickDismissalWarpReturn>) MakeBoxItemWithExtraDecorations(CustomComboBoxes.CanvasManager cmo, TrackerModel.Box box, bool accelerateIntoComboBox,
                                                                                                                       Func<Point, IEnumerable<(Control, double, double)>> computeExtraDecorationsWhenPopupActivatedOrMouseOverOpt)
    {
        var c = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black };
        if (!box.Stair.Equals(TrackerModel.StairKind.Never))
        {
            var stairImg = Graphics.BItoImage(Graphics.zi_staircase_unborderedBI);
            switch (box.Stair.Tag)
            {
                case TrackerModel.StairKind.Tags.LikeL2:
                    {
                        Action update = () => stairImg.Opacity = TrackerModel.IsSecondQuestDungeons && TrackerModelOptions.ShowBasementInfo.Value ? 1.0 : 0.0;
                        WhileInTree(c, () => { OptionsMenu.secondQuestDungeonsOptionChanged += update; OptionsMenu.showBasementInfoOptionChanged += update; },
                                       () => { OptionsMenu.secondQuestDungeonsOptionChanged -= update; OptionsMenu.showBasementInfoOptionChanged -= update; }, update);
                        update();
                        break;
                    }
                case TrackerModel.StairKind.Tags.LikeL3:
                    {
                        Action update = () => stairImg.Opacity = !TrackerModel.IsSecondQuestDungeons && TrackerModelOptions.ShowBasementInfo.Value ? 1.0 : 0.0;
                        WhileInTree(c, () => { OptionsMenu.secondQuestDungeonsOptionChanged += update; OptionsMenu.showBasementInfoOptionChanged += update; },
                                       () => { OptionsMenu.secondQuestDungeonsOptionChanged -= update; OptionsMenu.showBasementInfoOptionChanged -= update; }, update);
                        update();
                        break;
                    }
                case TrackerModel.StairKind.Tags.Always:
                    {
                        Action update = () => stairImg.Opacity = TrackerModelOptions.ShowBasementInfo.Value ? 1.0 : 0.0;
                        WhileInTree(c, () => OptionsMenu.showBasementInfoOptionChanged += update, () => OptionsMenu.showBasementInfoOptionChanged -= update, update);
                        update();
                        break;
                    }
                default: break;
            }
            canvasAdd(c, stairImg, 7.0, 7.0);
        }
        var rect = new Rectangle { Width = 30.0, Height = 30.0, Stroke = CustomComboBoxes.no, StrokeThickness = 3.0 };
        c.Children.Add(rect);
        var innerc = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Transparent };  // just has item drawn on it, not the box
        c.Children.Add(innerc);
        // 'Progressive Items': when this box's item is upgraded, a copy of the item glows and grows for 3 seconds. It is a separate overlay inside the box (the box itself is never transformed, so hover boxes and menus keep lining up).
        // Progressive Items: the picture of this box's item (null when it is not part of a family): uncollected ones show the family's current tier, collected ones the tier they were collected at
        System.Drawing.Bitmap progressiveBoxBitmap()
        {
            var f = TrackerModel.progressiveFamily(box.CellCurrent());
            if (f <= 0) return null;
            else
            {
                var t = TrackerModel.progressiveFamilyTier(f);
                var shown =
                    box.PlayerHas().Equals(TrackerModel.PlayerHas.YES) ?
                        (TrackerModel.progressiveBoxTier.TryGetValue(box, out var v) ? v : Math.Max(1, t - 1))
                    : t;
                if (f == 1) return Graphics.ow_progressive_tier_item_bmps[Math.Min(4, Math.Max(1, shown))];
                else return CustomComboBoxes.boxCurrentBMP(TrackerModel.progressiveFamilyIdent(f, Math.Min(2, Math.Max(1, shown))), null);
            }
        }
        onEventInTree(c, TrackerModel.progressiveBoxGlow.Publish, b =>   // (only while this box is in the visual tree)
        {
            // (WPF: c.IsVisible, which is false when an ancestor is hidden too, so Avalonia's IsEffectivelyVisible)
            if (ReferenceEquals(b, box) && c.IsEffectivelyVisible)
            {
                var cur = box.CellCurrent();
                System.Drawing.Bitmap colored;
                {
                    var pb = progressiveBoxBitmap();
                    colored = pb != null ? pb : CustomComboBoxes.boxCurrentBMP(cur, null);
                }
                if (colored != null)
                {
                    var overlay = makeProgressiveGlow(colored, 22.0);
                    overlay.ZIndex = 1000;   // (WPF: Panel.SetZIndex)
                    canvasAdd(c, overlay, 4.0, 4.0);
                    // the box and each container it sits in rise above their neighbours while it glows, so nothing (dungeon tiles included) covers the growing item
                    var raised = new List<(Control, int)>();
                    Visual curV = c;
                    while (curV != null)
                    {
                        // (WPF: FrameworkElement.Parent is the logical parent, as Avalonia's Control.Parent)
                        if (curV is Control fe && (fe.Parent is Panel || ReferenceEquals(fe, c)))
                        {
                            // (a container shared by several glowing boxes is raised once: the original is kept from the first raise, so the last restore puts back the real value)
                            if (glowRaised.TryGetValue(fe, out var entry))
                                glowRaised[fe] = (entry.Item1, entry.Item2 + 1);
                            else
                                glowRaised[fe] = (fe.ZIndex, 1);
                            raised.Add((fe, 0));
                            fe.ZIndex = 5000;
                        }
                        curV = curV.GetVisualParent();
                    }
                    CustomComboBoxes.StartImmediate(((Func<Task>)(async () =>
                    {
                        await Task.Delay(3000);
                        // (back on the UI thread: a ZIndex set from the thread pool was not put back, which left the map over the popups for good)
                        Dispatcher.UIThread.Post(() =>
                        {
                            c.Children.Remove(overlay);
                            foreach (var (fe, _) in raised)
                            {
                                if (!glowRaised.TryGetValue(fe, out var entry)) continue;
                                if (entry.Item2 <= 1)
                                {
                                    glowRaised.Remove(fe);
                                    fe.ZIndex = entry.Item1;
                                }
                                else
                                    glowRaised[fe] = (entry.Item1, entry.Item2 - 1);
                            }
                            ClickTrace.Log($"progressive glow ended: restored ZIndex on {raised.Count} element(s)");
                        });
                    }))());
                }
            }
        }, null);
        var innerCanvasStairwayHider = new Canvas { Background = Brushes.Black, Width = 21.0, Height = 21.0 };
        void redraw()
        {
            // redraw inner canvas
            innerc.Children.Clear();
            System.Drawing.Bitmap bmp;
            {
                var pb = progressiveBoxBitmap();
                bmp = pb != null ? pb : CustomComboBoxes.boxCurrentBMP(box.CellCurrent(), null);
            }
            if (bmp != null)
            {
                canvasAdd(innerc, innerCanvasStairwayHider, 3.0, 3.0);  // a black box covers any stair drawing behind the item, so the item is easy to read
                if (box.PlayerHas().Equals(TrackerModel.PlayerHas.NO))
                {
                    var image = Graphics.BMPtoImage(Graphics.greyscale(bmp));
                    canvasAdd(innerc, image, 4.0, 4.0);
                    var fam = TrackerModel.progressiveFamily(box.CellCurrent());
                    var l = uncollectedTierLetter(fam >= 2 ? TrackerModel.progressiveFamilyIdent(fam, TrackerModel.progressiveFamilyTier(fam)) : box.CellCurrent());   // (Progressive Items: the letter follows the tier shown)
                    if (l != null && !TrackerModel.ProgressiveItems)   // (Progressive Items: no letter; the item itself shows the tier)
                        canvasAdd(innerc, makeTierLetter(l, 11.5), 0.0, 7.0);   // just the letter, centered over the item
                }
                else
                {
                    var image = Graphics.BMPtoImage(bmp);
                    canvasAdd(innerc, image, 4.0, 4.0);
                }
                if (itemIsHintDashed(box.CellCurrent())) canvasAdd(innerc, makeHintDashedRect(26.0, 26.0), 2.0, 2.0);
            }
            // redraw box outline
            switch (box.PlayerHas().Tag)
            {
                case TrackerModel.PlayerHas.Tags.YES:
                    rect.Stroke = CustomComboBoxes.yes;
                    break;
                case TrackerModel.PlayerHas.Tags.NO:
                    if (bmp == null)
                    {
                        try
                        {
                            bool redify;
                            var hdn = TrackerModel.IsHiddenDungeonNumbers.Invoke(null);
                            if (!hdn && box.Owner.IsDungeon1or4)
                                redify = !TrackerModel.GetDungeon(TrackerModel.IsSecondQuestDungeons ? 3 : 0).Boxes[1].IsEmptyRedBox;
                            else if (!hdn && box.Owner is TrackerModel.BoxOwner.DungeonIndexAndNth din && din.Item2 == 2)
                                redify = !TrackerModel.GetDungeon(din.Item1).Boxes[1].IsEmptyRedBox;
                            else
                                redify = false;
                            rect.Stroke = CustomComboBoxes.noPct(redify ? 1.0 : TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxProgress.Value);
                        }
                        catch { rect.Stroke = CustomComboBoxes.no; }  // we create fake Views on the startup menu to preview Heart Shuffle; TheDungeonTrackerInstance does not exist yet and throws
                    }
                    else
                    {
                        try { rect.Stroke = CustomComboBoxes.noAndNotEmpty(TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxProgress.Value); }
                        catch { rect.Stroke = CustomComboBoxes.no; }  // we create fake Views on the startup menu to preview Heart Shuffle; TheDungeonTrackerInstance does not exist yet and throws
                    }
                    break;
                case TrackerModel.PlayerHas.Tags.SKIPPED:
                    if (bmp == null)
                        rect.Stroke = CustomComboBoxes.skippedAndEmpty;
                    else
                    {
                        rect.Stroke = CustomComboBoxes.skipped;
                        Graphics.placeSkippedItemXDecoration(innerc);
                    }
                    break;
            }
            if (redBorderBoxes.Contains(box) && bmp == null && !box.PlayerHas().Equals(TrackerModel.PlayerHas.YES))
                rect.Stroke = Brushes.White;   // coast / armos / letter / white sword / magical sword boxes start white until an item is placed in them
            // redraw specific-blockers
            if (!box.PlayerHas().Equals(TrackerModel.PlayerHas.YES))
            {
                var owner =
                    box.Owner.IsDungeon1or4 ?
                        (TrackerModel.IsSecondQuestDungeons ? TrackerModel.BoxOwner.NewDungeonIndexAndNth(3, 2) : TrackerModel.BoxOwner.NewDungeonIndexAndNth(0, 2))
                    : box.Owner;
                if (owner is TrackerModel.BoxOwner.DungeonIndexAndNth dn)
                {
                    var (i, n) = (dn.Item1, dn.Item2);
                    if (TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON != 3)
                        throw new Exception("This UI was designed for 3 blockers per dungeon");
                    for (int j = 0; j <= 2; j++)
                    {
                        if (i != 8 && TrackerModel.DungeonBlockersContainer.GetDungeonBlockerAppliesTo(i, j, n + 3))
                        {
                            var blocker = TrackerModel.DungeonBlockersContainer.GetDungeonBlocker(i, j);
                            var bbmp = Graphics.blockerHardCanonicalBMP(blocker);
                            if (bbmp != null)
                            {
                                var img = Graphics.BMPtoImage(bbmp);
                                img.Width = 7.0;
                                img.Height = 7.0;
                                if (!blocker.PlayerCouldBeBlockedByThis())
                                {
                                    canvasAdd(innerc, new Canvas { Width = 9.0, Height = 9.0, Background = Brushes.Lime },  (double)(1 + j * 10), 4.0 + 16.0);
                                    canvasAdd(innerc, new Canvas { Width = 7.0, Height = 7.0, Background = Brushes.Black }, (double)(2 + j * 10), 5.0 + 16.0);
                                }
                                else
                                    canvasAdd(innerc, new Canvas { Width = 9.0, Height = 9.0, Background = Brushes.Black }, (double)(1 + j * 10), 4.0 + 16.0);
                                canvasAdd(innerc, img, (double)(2 + j * 10), 5.0 + 16.0);
                            }
                        }
                    }
                }
            }
        }
        redraw();
        // (every subscription below lasts only while this box is in the visual tree, so thrown-away boxes, e.g. on redrawn overworld tiles, stop listening)
        try { onEventInTree(c, TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxProgress.Changed, _ => redraw(), null); }
        catch { } // we create fake Views on the startup menu to preview Heart Shuffle; TheDungeonTrackerInstance does not exist yet and throws
        // redraw on changes
        Action redrawA = redraw;
        WhileInTree(c, () => { redrawBoxes.Add(redrawA); hintDashedItemsChanged += redrawA; }, () => { redrawBoxes.Remove(redrawA); hintDashedItemsChanged -= redrawA; }, redraw);
        onEventInTree(c, box.Changed, _ => redraw(), null);
        onEventInTree(c, TrackerModel.progressiveTierChanged.Publish, _ => redraw(), null);   // Progressive Items: the progressive sword's P1..P4 sprite follows the number collected
        // interactions
        if (cmo != null)
        {
            var cm = cmo;
            // a dungeon has just one triforce piece: asking for a triforce in another slot of that dungeon (its item slots or the extra floor box) moves the existing piece to this slot.
            // clears the old slot and returns Some(was it checked off), or None when there was nothing to move
            // (C#: a bool?, null for None)
            bool? takeTriforceFromOtherSlot(int newValue)
            {
                if (newValue != TrackerModel.ITEMS.TRIFORCE) return null;
                else
                {
                    int? dIdx;
                    if (box.Owner is TrackerModel.BoxOwner.DungeonIndexAndNth dn && dn.Item1 <= 7) dIdx = dn.Item1;
                    else if (box.Owner.IsDungeon1or4) dIdx = (TrackerModel.IsSecondQuestDungeons ? 3 : 0);
                    else if (ReferenceEquals(TrackerModel.level2StairBox, box)) dIdx = 1;   // the optional extra level 2 staircase slot belongs to level 2
                    else
                    {
                        dIdx = null;
                        for (int i = 0; i <= 7; i++)
                            if (ReferenceEquals(TrackerModel.GetDungeon(i).ExtraFloorBox, box)) { dIdx = i; break; }
                    }
                    if (dIdx is int di)
                    {
                        var d = TrackerModel.GetDungeon(di);
                        var candidates = d.Boxes.Concat(new[] { d.ExtraFloorBox }).Concat(di == 1 && TrackerModel.level2StairBoxEnabled ? new[] { TrackerModel.level2StairBox } : new TrackerModel.Box[0]).ToArray();
                        var other = candidates.FirstOrDefault(b => !ReferenceEquals(b, box) && b.CellCurrent() == TrackerModel.ITEMS.TRIFORCE);
                        if (other != null)
                        {
                            var had = other.PlayerHas().Equals(TrackerModel.PlayerHas.YES);
                            other.Set(-1, TrackerModel.PlayerHas.NO);
                            return had;
                        }
                        else return null;
                    }
                    else return null;
                }
            }
            void activateComboBox(int activationDelta, CustomComboBoxes.GridClickDismissalWarpReturn gridClickDismissalWarpReturn)
            {
                var maybePos = c.TranslatePoint(new Point(), cm.AppMainCanvas);
                if (maybePos == null) return;   // (this box left the tree; there is nowhere to open the menu)
                CustomComboBoxes.GlobalFlag.popupIsActive = true;
                var pos = maybePos.Value;
                var extraDecorations = computeExtraDecorationsWhenPopupActivatedOrMouseOverOpt != null ? computeExtraDecorationsWhenPopupActivatedOrMouseOverOpt(pos) : new (Control, double, double)[0];
                CustomComboBoxes.StartImmediate(((Func<Task>)(async () =>
                {
                    var r = await CustomComboBoxes.DisplayItemComboBox(cm, pos.X, pos.Y, box.CellCurrent(), activationDelta, box.PlayerHas(), extraDecorations, gridClickDismissalWarpReturn);
                    if (r.hasResult)
                    {
                        var (newBoxCellValue, newPlayerHas) = r.result;
                        var had = takeTriforceFromOtherSlot(newBoxCellValue);
                        if (had.HasValue)
                            box.Set(newBoxCellValue, (had.Value ? TrackerModel.PlayerHas.YES : newPlayerHas));   // the piece moves here, keeping its checked state
                        else
                            box.Set(newBoxCellValue, newPlayerHas);
                    }
                    CustomComboBoxes.GlobalFlag.popupIsActive = false;
                }))());
            }
            c.PointerPressed += (_, ea) =>
            {
                if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                {
                    var kind = ea.GetCurrentPoint(c).Properties.PointerUpdateKind;
                    if (kind == PointerUpdateKind.LeftButtonPressed || kind == PointerUpdateKind.MiddleButtonPressed || kind == PointerUpdateKind.RightButtonPressed)
                    {
                        ea.Handled = true;
                        if (box.CellCurrent() == -1)
                            activateComboBox(0, new CustomComboBoxes.GridClickDismissalWarpReturn.WarpToCenter());
                        else
                        {
                            var desire = CustomComboBoxes.MouseButtonEventArgsToPlayerHas(ea);
                            if (desire.Equals(box.PlayerHas()))
                                activateComboBox(0, new CustomComboBoxes.GridClickDismissalWarpReturn.WarpToCenter()); // rather than idempotent gesture doing nothing, a second try (e.g. left click and already-have item) reactivates popup (easier to discover than scroll)
                            else
                                box.SetPlayerHas(desire);
                        }
                    }
                }
            };
            c.PointerWheelChanged += (s, ea) =>
            {
                var notch = Graphics.wheelNotch(s, ea);
                if (notch == 0) return;   // (whole notches only: trackpads send tiny deltas, and sideways scrolling, which WPF's MouseWheel never reports; see PROGRESS.md)
                if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                {
                    ea.Handled = true;
                    activateComboBox((notch < 0 ? 1 : -1), new CustomComboBoxes.GridClickDismissalWarpReturn.WarpToCenter());
                }
            };
            c.MyKeyAdd(ea =>
            {
                if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                {
                    if (HotKeys.ItemHotKeyProcessor.TryGetValue(ea.Key, out var i))
                    {
                        ea.Handled = true;
                        if (i != -1 && box.CellCurrent() == i)
                        {
                            // if this box already contains the hotkey'd item, pressing the hotkey cycles the PlayerHas state NO -> YES -> SKIPPED
                            if (box.PlayerHas().Equals(TrackerModel.PlayerHas.NO))
                                box.Set(i, TrackerModel.PlayerHas.YES);
                            else if (box.PlayerHas().Equals(TrackerModel.PlayerHas.YES))
                                box.Set(i, TrackerModel.PlayerHas.SKIPPED);
                            else
                                box.Set(i, TrackerModel.PlayerHas.NO);
                        }
                        else if (i == -1)
                        {
                            if (box.CellCurrent() != -1)
                                box.Set(i, TrackerModel.PlayerHas.NO);      // emptying a full box always goes to NO first
                            else if (box.PlayerHas().Equals(TrackerModel.PlayerHas.NO))
                                box.Set(i, TrackerModel.PlayerHas.SKIPPED); // emptying an empty box toggles the box outline color
                            else
                                box.Set(i, TrackerModel.PlayerHas.NO);      // emptying an empty box toggles the box outline color
                        }
                        else
                        {
                            // changing from empty/other-item box to this item value always NO on first hotkey press, as this is 'model harmless'
                            var movedFrom = takeTriforceFromOtherSlot(i);
                            if (movedFrom.HasValue) box.Set(i, (movedFrom.Value ? TrackerModel.PlayerHas.YES : TrackerModel.PlayerHas.NO));
                            else if (!box.AttemptToSet(i, TrackerModel.PlayerHas.NO))
                                // e.g. they tried to set this box to Bow when another box already has 'Bow'
                                Graphics.ErrorBeepWithReminderLogText($"Tried to mark item box as {TrackerModel.ITEMS.AsDisplayDescription(i)}, but that item is already marked in a different box");
                        }
                    }
                }
            });
            if (accelerateIntoComboBox)
                c.Loaded += (_, _) => activateComboBox(0, new CustomComboBoxes.GridClickDismissalWarpReturn.WarpToCenter());
            // hover behavior
            appMainCanvasGlobalBoxMouseOverHighlight.ApplyBehavior(c);
            if (computeExtraDecorationsWhenPopupActivatedOrMouseOverOpt != null)
            {
                var f = computeExtraDecorationsWhenPopupActivatedOrMouseOverOpt;
                var hoverCanvas = new Canvas();
                c.PointerEntered += (_, _) =>
                {
                    cm.AppMainCanvas.Children.Remove(hoverCanvas);  // safeguard, in case MouseEnter/MouseLeave parity is broken
                    var maybePos = c.TranslatePoint(new Point(), cm.AppMainCanvas);
                    if (maybePos == null) return;   // (null when this box left the tree, e.g. it was rebuilt under the pointer; nothing to decorate)
                    var pos = maybePos.Value;
                    var extraDecorations = f(pos);
                    hoverCanvas.Children.Clear();
                    foreach (var (fe, x, y) in extraDecorations)
                        canvasAdd(hoverCanvas, fe, x + 3.0, y + 3.0);   // +3s because decorations are relative to the combobox popup, which is over the interior icon area, excluding the rectangle border
                    canvasAdd(cm.AppMainCanvas, hoverCanvas, pos.X, pos.Y);
                };
                c.PointerExited += (_, _) => cm.AppMainCanvas.Children.Remove(hoverCanvas);
            }
            return (c, activateComboBox);
        }
        else
            return (c, (_, _) => { });
    }

    public static (Canvas, Action<int, CustomComboBoxes.GridClickDismissalWarpReturn>) MakeBoxItem(CustomComboBoxes.CanvasManager cm, TrackerModel.Box box) =>
        MakeBoxItemWithExtraDecorations(cm, box, false, null);

    // blocker view
    private static GradientStops make_blocker_gsc()
    {
        var g = new GradientStops();
        g.Add(new GradientStop(Color.FromArgb(255, 60, 180, 60), 0.0));
        g.Add(new GradientStop(Color.FromArgb(255, 80, 80, 80), 0.4));
        g.Add(new GradientStop(Color.FromArgb(255, 80, 80, 80), 0.6));
        g.Add(new GradientStop(Color.FromArgb(255, 180, 60, 60), 1.0));
        return g;
    }
    public static readonly GradientStops blocker_gsc = make_blocker_gsc();
    public static readonly LinearGradientBrush blocker_brush = new LinearGradientBrush
    {
        GradientStops = blocker_gsc,
        StartPoint = new RelativePoint(0.0, 0.0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1.0, 1.0, RelativeUnit.Relative)
    };
    public static (Canvas, Func<TrackerModel.DungeonBlocker, Canvas>) MakeBlockerCore()
    {
        var c = new Canvas { Width = 30.0, Height = 30.0, Background = Brushes.Black, IsHitTestVisible = true };
        var rect = new Rectangle { Width = 30.0, Height = 30.0, Stroke = Brushes.Gray, StrokeThickness = 3.0, IsHitTestVisible = false };
        Canvas redraw(TrackerModel.DungeonBlocker n)
        {
            c.Children.Clear();
            switch (n.Tag)
            {
                case TrackerModel.DungeonBlocker.Tags.MAYBE_LADDER:
                case TrackerModel.DungeonBlocker.Tags.MAYBE_RECORDER:
                case TrackerModel.DungeonBlocker.Tags.MAYBE_BAIT:
                case TrackerModel.DungeonBlocker.Tags.MAYBE_BOMB:
                case TrackerModel.DungeonBlocker.Tags.MAYBE_BOW_AND_ARROW:
                case TrackerModel.DungeonBlocker.Tags.MAYBE_KEY:
                case TrackerModel.DungeonBlocker.Tags.MAYBE_MONEY:
                    rect.Stroke = blocker_brush;
                    break;
                case TrackerModel.DungeonBlocker.Tags.NOTHING: rect.Stroke = Brushes.Gray; break;
                default: rect.Stroke = Brushes.LightGray; break;
            }
            c.Children.Add(rect);
            canvasAdd(c, Graphics.blockerCurrentDisplay(n), 3.0, 3.0);
            return c;
        }
        return (c, redraw);
    }
    public static Canvas MakeBlockerView(int dungeonIndex, int blockerIndex)
    {
        var (c, redraw) = MakeBlockerCore();
        var current = TrackerModel.DungeonBlockersContainer.GetDungeonBlocker(dungeonIndex, blockerIndex);
        redraw(current);
        onEvent(TrackerModel.DungeonBlockersContainer.AnyBlockerChanged, _ =>
        {
            current = TrackerModel.DungeonBlockersContainer.GetDungeonBlocker(dungeonIndex, blockerIndex);
            redraw(current);
        });
        return c;
    }
}
