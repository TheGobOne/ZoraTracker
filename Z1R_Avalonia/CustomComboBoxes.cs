// Ported from Z1R_WPF/CustomComboBoxes.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// F# async → C# Task, and how the threading is kept the same:
// - Every `async { }` (DoModalCore, DoModal*, DoModalMessageBox*, DoModalGridSelect, DisplayItemComboBox) is an `async Task`/`Task<T>`
//   method. Like an F# async started with Async.StartImmediate, a C# async method runs synchronously on the calling (UI) thread until its
//   first incomplete await, so a popup is fully built and shown before the call returns. Callers `await` them where WPF used `let!`/`do!`.
// - `Async.AwaitWaitHandle(wh)` followed by `Async.SwitchToContext(ctxt)` (DoModalCore) becomes `await AwaitWaitHandle(wh)`: the wait runs on
//   the thread pool (ThreadPool.RegisterWaitForSingleObject, which is also what FSharp.Core uses) and completes a TaskCompletionSource; the
//   await captured the UI thread's SynchronizationContext, so the rest of the method (the `finally` that removes the popup layer, and
//   everything after `do! DoModal(...)` in the callers) runs back on the UI thread, posted, exactly as after SwitchToContext.
// - `Async.SwitchToThreadPool()` ... `Async.SwitchToContext ctx` (the update download) becomes `await Task.Run(...)`.
// - `someAsync |> Async.StartImmediate` (fire and forget) becomes `StartImmediate(someTask)`, an `async void` wrapper: the task starts
//   immediately on the current thread, and an exception escaping it is rethrown on the UI thread, as F#'s StartImmediate does.
// - The ManualResetEvents callers pass to dismiss a popup early are unchanged (System.Threading.ManualResetEvent).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Z1R_Avalonia;

public static class CustomComboBoxes
{
    // (port helper, see the top of the file: WPF's `x |> Async.StartImmediate`. Starts the task now, on this thread; an exception that
    // escapes it is rethrown on the UI thread's SynchronizationContext, like an exception escaping an F# async started with StartImmediate.)
    public static async void StartImmediate(Task t) => await t;
    // (port helper: WPF's Async.AwaitWaitHandle. The wait happens on the thread pool; the caller's await resumes on its own context.)
    private static Task AwaitWaitHandle(WaitHandle wh)
    {
#if BROWSER
        // (browser build: a page has one thread, so nothing can wait on the thread pool; check the handle every few ms instead, without blocking)
        return pollWaitHandle(wh);
#else
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reg = ThreadPool.RegisterWaitForSingleObject(wh, (_, _) => tcs.TrySetResult(true), null, Timeout.Infinite, true);
        return tcs.Task.ContinueWith(_ => { reg.Unregister(null); }, TaskScheduler.Default);
#endif
    }

#if BROWSER
    private static async Task pollWaitHandle(WaitHandle wh)
    {
        while (!wh.WaitOne(0))
            await Task.Delay(15);
    }
#endif

    public static void canvasAdd(Canvas c, Control item, double left, double top) => Graphics.canvasAdd(c, item, left, top);
    public static void gridAdd(Grid g, Control x, int c, int r) => Graphics.gridAdd(g, x, c, r);
    public static Grid makeGrid(int nc, int nr, int cw, int rh) => Graphics.makeGrid(nc, nr, cw, rh);

    // (F#: `module GlobalFlag`, which callers `open`. In C#: CustomComboBoxes.GlobalFlag.popupIsActive.)
    public static class GlobalFlag
    {
        public static bool popupIsActive = false;
        public static DateTime popupOpenStamp = DateTime.MinValue;   // set when a grid menu starts opening: the stuck-flag self-heal leaves the flag alone for a moment after
        // a popup that failed part-way: write the exception to the crash log (so a report of a dead overworld comes with a stack trace) and unlock the UI
        public static void logPopupFailure(string where, System.Exception e)
        {
            try
            {
                System.IO.File.AppendAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Z1R_Tracker_crash_log.txt"),
                    $"\n\nPOPUP FAILURE -- {System.DateTime.Now:yyyy-MM-dd-HH:mm:ss} -- {where}\n{e}\n");
            }
            catch { }
            popupIsActive = false;
        }
        public static async System.Threading.Tasks.Task Guard(System.Threading.Tasks.Task t, string where)
        {
            try { await t; } catch (System.Exception e) { logPopupFailure(where, e); }
        }
    }

    ////////////////////////////////////////////////////////////
    // ItemComboBox

    // (WPF: takes a MouseButtonEventArgs and reads ChangedButton; in Avalonia the button that went down is the press's PointerUpdateKind)
    public static TrackerModel.PlayerHas MouseButtonEventArgsToPlayerHas(PointerPressedEventArgs ea)
    {
        var kind = ea.GetCurrentPoint(null).Properties.PointerUpdateKind;
        if (kind == PointerUpdateKind.LeftButtonPressed) return TrackerModel.PlayerHas.YES;
        else if (kind == PointerUpdateKind.RightButtonPressed) return TrackerModel.PlayerHas.NO;
        else return TrackerModel.PlayerHas.SKIPPED;
    }
    public static readonly SolidColorBrush no = new SolidColorBrush(Color.FromRgb(0xA8, 0x00, 0x00));
    public static readonly SolidColorBrush[] noPctBrushes = Enumerable.Range(0, 101).Select(i => new SolidColorBrush(Color.FromRgb((byte)(0xFF - (byte)(100 - i)), (byte)(i / 2), (byte)(i * 2 / 3)))).ToArray();
    public static SolidColorBrush noPct(double f) => noPctBrushes[(int)(f * 100.0)];
    public static readonly SolidColorBrush[] noNEPctBrushes = Enumerable.Range(0, 101).Select(i => new SolidColorBrush(Color.FromRgb(0xFF, (byte)(i / 2), (byte)(i * 2 / 3)))).ToArray();
    public static SolidColorBrush noAndNotEmpty(double f) => noNEPctBrushes[(int)(f * 100.0)];
    public static readonly SolidColorBrush yes = new SolidColorBrush(Color.FromRgb(0x32, 0xA8, 0x32));
    public static readonly IImmutableSolidColorBrush skipped = Graphics.skipped;
    public static readonly IImmutableSolidColorBrush skippedAndEmpty = Brushes.White;
    // (timelineOrigin: null for F#'s None)
    public static System.Drawing.Bitmap boxCurrentBMP(int boxCellCurrent, TimelineID timelineOrigin)
    {
        switch (boxCellCurrent)
        {
            case -1: return null;
            case 0: return (TrackerModel.IsCurrentlyBook.Invoke(null) ? Graphics.book_bmp : Graphics.magic_shield_bmp);
            case 1: return Graphics.boomerangOrBootsBmp();
            case 2: return Graphics.bow_bmp;
            case 3: return Graphics.power_bracelet_bmp;
            case 4: return Graphics.ladder_bmp;
            case 5: return Graphics.magicBoomerangOrMagicalBootsBmp();
            case 6: return Graphics.magic_key_bmp;
            case 7: return Graphics.raft_bmp;
            case 8: return Graphics.recorder_bmp;
            case 9: return Graphics.red_candle_bmp;
            case 10: return Graphics.red_ring_bmp;
            case 11: return Graphics.silver_arrow_bmp;
            case 12: return Graphics.wand_bmp;
            case 13: return (TrackerModel.IsWSMSReplacedByBU.Invoke(null) ? Graphics.ws_ms_bomb_upgrade_bmp : Graphics.ow_sword2_item_bmp);
            case 15: return Graphics.blue_potion_bmp;
            case 16: return Graphics.red_potion_bmp;
            case 17: return Graphics.old_letter_bmp;
            case 18: return Graphics.ow_progressive_tier_item_bmps[0];   // (the plain P sword; once placed, the box / tile shows the tier sprite)
            case 19: return Graphics.ow_sword1_item_bmp;
            case 20: return Graphics.ow_sword3_item_bmp;
            case 21: return Graphics.ow_sword4_item_bmp;
            case 22: return Graphics.blue_ring_bmp;
            case 23: return Graphics.wood_arrow_bmp;
            case 24: return Graphics.blue_candle_bmp;
            case 25: return Graphics.magic_shield_bmp;
            case 26: return Graphics.bait_bmp;
            case 27: return Graphics.key_bmp;
            case 28: return Graphics.ws_ms_bomb_upgrade_bmp;
            case 29: return Graphics.triforce_item_bmp;
            case var n when n >= 30 && n < 46: return Graphics.customItemBmps[n - 30];
            default:
                if (timelineOrigin != null)
                {
                    var tid = timelineOrigin;
                    int? level =
                        tid.Tag switch
                        {
                            TimelineID.Tags.LevelBox => tid.Item1,
                            TimelineID.Tags.Level1or4Box3 => (TrackerModel.IsSecondQuestDungeons ? 4 : 1),
                            _ => null,
                        };
                    if (level is int i)
                    {
                        if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
                        {
                            if (i == 9)
                                return Graphics.heartFromNumberedDungeon_bmps[i - 1];
                            else if (TrackerModel.GetDungeon(i - 1).LabelChar != '?')
                                return Graphics.heartFromNumberedDungeon_bmps[(int)TrackerModel.GetDungeon(i - 1).LabelChar - (int)'1'];
                            else
                                return Graphics.heartFromLetteredDungeon_bmps[i - 1];
                        }
                        else
                            return Graphics.heartFromNumberedDungeon_bmps[i - 1];
                    }
                    else
                    {
                        switch (tid.Tag)
                        {
                            case TimelineID.Tags.LadderBox: return Graphics.heartFromCoast_bmp;
                            case TimelineID.Tags.WhiteSwordBox: return Graphics.heartFromWhiteSwordCave_bmp;
                            case TimelineID.Tags.ArmosBox: return Graphics.heartFromArmos_bmp;
                            default: return Graphics.owHeartTallFull_bmp;
                        }
                    }
                }
                else
                    return Graphics.heart_container_bmp;
        }
    }

    ///////////////////////////////////////////////////////////////////

    /*
    note: a possible alternate way to 'sunglasses' would be to capture a snapshot as a bitmap, and then place it, here is some code i found that may be relevant
    public void SaveUIElementToImageBMPFile(UIElement VisualElement, string FilePath) {
    System.Windows.Media.Imaging.RenderTargetBitmap targetBitmap =
          new System.Windows.Media.Imaging.RenderTargetBitmap((int)VisualElement.RenderSize.Width,
                                                              (int)VisualElement.RenderSize.Height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
    VisualElement.Measure(VisualElement.RenderSize);
    VisualElement.Arrange(new Rect(VisualElement.RenderSize));
    targetBitmap.Render(VisualElement);
    System.Windows.Media.Imaging.BmpBitmapEncoder bmpencoder = new System.Windows.Media.Imaging.BmpBitmapEncoder();
    bmpencoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(targetBitmap));
    using (System.IO.FileStream filestream = new System.IO.FileStream(FilePath, System.IO.FileMode.Create))
    {
        bmpencoder.Save(filestream);
        filestream.Close();
    }
    (Avalonia: new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(w, h)), .Render(visual), .Save(stream).)
    */
    public class CanvasManager
    {
        private static CanvasManager theOnlyCanvasManager = null;
        private readonly Canvas rootCanvas;
        private readonly Canvas appMainCanvas;
        private readonly Stack<Canvas> popupCanvasStack;
        private readonly Stack<double> opacityStack;
        private readonly double width;
        private readonly Canvas sunglasses;
        // (F#: Event<Canvas>s only this class triggers, so C# events; WPF's `cm.AfterCreatePopupCanvas.Add(fun pc -> ...)` becomes `cm.AfterCreatePopupCanvas += pc => ...`)
        public event Action<Canvas> AfterCreatePopupCanvas;
        public event Action<Canvas> BeforeDismissPopupCanvas;
        public CanvasManager(Canvas rootCanvas, Canvas appMainCanvas)
        {
            this.rootCanvas = rootCanvas;
            this.appMainCanvas = appMainCanvas;
            if (rootCanvas.Width != appMainCanvas.Width || rootCanvas.Height != appMainCanvas.Height)
                throw new Exception("rootCanvas and appMainCanvas should be the same size");
            if (!(object.Equals(appMainCanvas.Parent, rootCanvas)) || !(rootCanvas.Children.Count == 1))
                throw new Exception("rootCanvas must have appMainCanvas as its only child");
            if (theOnlyCanvasManager != null)
                throw new Exception("created more than one CanvasManager");
            theOnlyCanvasManager = this;
            popupCanvasStack = new Stack<Canvas>();
            opacityStack = new Stack<double>();
            width = rootCanvas.Width;
            sunglasses = new Canvas { Width = width, Height = rootCanvas.Height, Background = Brushes.Black, IsHitTestVisible = false };
            // a click when no popup is supposed to be open but a popup layer is still there: remove the leftover layer so the tracker is usable again
            // (WPF PreviewMouseDown: a tunnelling handler)
            rootCanvas.AddHandler(InputElement.PointerPressedEvent, new EventHandler<PointerPressedEventArgs>((_, _) =>
            {
                var opening = (DateTime.Now - GlobalFlag.popupOpenStamp).TotalMilliseconds < 1500.0;   // a menu is still being set up: not stuck
                var orphanLayers = rootCanvas.Children.Count - 1 != popupCanvasStack.Count;         // a layer in the window that is not on the stack (or the reverse)
                if ((popupCanvasStack.Count > 0 && !GlobalFlag.popupIsActive) || (orphanLayers && !opening))
                {
                    logPopupNote($"{popupCanvasStack.Count} leftover popup layer(s) (window has {rootCanvas.Children.Count - 1}) with no popup active; removed on click");
                    clearAllPopups();
                    GlobalFlag.popupIsActive = false;
                }
                else if (popupCanvasStack.Count == 0 && GlobalFlag.popupIsActive && !opening)
                {
                    // the tracker thinks a popup is open but there is no popup layer: clear the stuck flag so clicks work again
                    logPopupNote("popup flag was stuck on with no popup layer; cleared on click");
                    GlobalFlag.popupIsActive = false;
                }
            }), RoutingStrategies.Tunnel);
        }
        // a one-line note in the crash log whenever the popup layers get out of step (so a report of a stuck gray screen says when it began)
        // (the crash log is a user file: Platform.Files.User, which is the exe folder on Windows, as in WPF)
        private void logPopupNote(string msg)
        {
            try { System.IO.File.AppendAllText(Platform.Files.User("Z1R_Tracker_crash_log.txt"), $"\n\nPOPUP NOTE -- {DateTime.Now.ToString("yyyy-MM-dd-HH:mm:ss")} -- {msg}\n"); }
            catch { }
        }
        // removes every popup layer and the dimming layer (only used to recover from a layer that was left behind)
        private void clearAllPopups()
        {
            while (popupCanvasStack.Count > 0)
            {
                var pc = popupCanvasStack.Pop();
                try { BeforeDismissPopupCanvas?.Invoke(pc); } catch { }
                rootCanvas.Children.Remove(pc);
                if (opacityStack.Count > 0) opacityStack.Pop();
            }
            opacityStack.Clear();
            if (sunglasses.Parent != null) ((Canvas)sunglasses.Parent).Children.Remove(sunglasses);
            // also any layer that is in the window but not on the stack (a popup that failed part-way)
            foreach (var orphan in rootCanvas.Children.Where(ch => !object.ReferenceEquals(ch, appMainCanvas)).ToList())
                rootCanvas.Children.Remove(orphan);
        }
        public double Width => width;
        public double Height => rootCanvas.Height;
        public void SetHeight(double h)
        {
            rootCanvas.Height = h;
            appMainCanvas.Height = h;
        }
        public Canvas RootCanvas => rootCanvas;           // basically, no one should touch this, except to set mainWindow.Content <- cm.RootCanvas
        public Canvas AppMainCanvas => appMainCanvas;     // this is where the app should be putting all its content
        // and down here is where/how the popups should be putting their content
        public Stack<Canvas> PopupCanvasStack => popupCanvasStack;
        public Canvas CreatePopup(double blackSunglassesOpacity)
        {
            opacityStack.Push(blackSunglassesOpacity);
            sunglasses.Opacity = opacityStack.Max();
            // remove sunglasses from prior prior layer (if there was one)
            if (sunglasses.Parent != null)
                ((Canvas)sunglasses.Parent).Children.Remove(sunglasses);
            // put the sunglasses over the prior child (appMainCanvas or previous popupCanvas)
            sunglasses.ZIndex = 100000;   // above everything on the layer, including elements with a raised ZIndex (e.g. the Show/Run Custom button)
            ((Canvas)rootCanvas.Children[rootCanvas.Children.Count - 1]).Children.Add(sunglasses);
            // put a new popup canvas in the root
            var popupCanvas = new Canvas { Width = appMainCanvas.Width, Height = appMainCanvas.Height, Background = Brushes.Transparent, IsHitTestVisible = true, Opacity = 1.0 };
            rootCanvas.Children.Add(popupCanvas);
            popupCanvasStack.Push(popupCanvas);
            try { AfterCreatePopupCanvas?.Invoke(popupCanvas); } catch (Exception e) { logPopupNote("a popup-created listener failed: " + e.Message); }   // (a failing listener must not leave this layer behind)
            // return the popup canvas
            return popupCanvas;
        }
        public void DismissPopup()
        {
            // remove the popup canvas
            var pc = popupCanvasStack.Pop();
            try { BeforeDismissPopupCanvas?.Invoke(pc); } catch (Exception e) { logPopupNote("a popup-dismissed listener failed: " + e.Message); }   // (the layer must come off even if a listener fails)
            rootCanvas.Children.Remove(pc);
            opacityStack.Pop();
            // remove the sunglasses from the prior child
            ((Canvas)rootCanvas.Children[rootCanvas.Children.Count - 1]).Children.Remove(sunglasses);
            // place the sunglasses on the prior prior layer (if there is one)
            if (rootCanvas.Children.Count - 2 >= 0)
            {
                sunglasses.Opacity = opacityStack.Max();
                ((Canvas)rootCanvas.Children[rootCanvas.Children.Count - 2]).Children.Add(sunglasses);
            }
        }
        // removes this particular popup's layer, even if popups were closed out of order (DismissPopup() always removes the top one)
        public void DismissPopupCanvas(Canvas pc)
        {
            if (popupCanvasStack.Count > 0 && object.ReferenceEquals(popupCanvasStack.Peek(), pc))
                this.DismissPopup();
            else if (popupCanvasStack.Contains(pc))
            {
                logPopupNote("a popup was closed out of order; its own layer was removed");
                try { BeforeDismissPopupCanvas?.Invoke(pc); } catch { }
                var cs = popupCanvasStack.ToArray();    // top first
                var os = opacityStack.ToArray();
                popupCanvasStack.Clear();
                opacityStack.Clear();
                for (int i = cs.Length - 1; i >= 0; i--)
                {
                    if (!(object.ReferenceEquals(cs[i], pc)))
                    {
                        popupCanvasStack.Push(cs[i]);
                        opacityStack.Push(os[i]);
                    }
                }
                if (sunglasses.Parent != null) ((Canvas)sunglasses.Parent).Children.Remove(sunglasses);
                rootCanvas.Children.Remove(pc);
                if (rootCanvas.Children.Count - 2 >= 0 && opacityStack.Count > 0)
                {
                    sunglasses.Opacity = opacityStack.Max();
                    ((Canvas)rootCanvas.Children[rootCanvas.Children.Count - 2]).Children.Add(sunglasses);
                }
            }
            // else: this layer was already removed (e.g. by the leftover-layer cleanup); nothing to do
        }
        // and here is how the broadcast window can listen for popup activity
        // (AfterCreatePopupCanvas / BeforeDismissPopupCanvas: the C# events above)
        public static CanvasManager TheOnlyCanvasManager => theOnlyCanvasManager;
    }

    // Escape closes the topmost modal popup (the same as clicking outside it)
    private static readonly Stack<ManualResetEvent> modalDismissStack = new Stack<ManualResetEvent>();
    private static bool escapeHandlerRegistered = false;
    private static void ensureEscapeHandler()
    {
        if (!escapeHandlerRegistered)
        {
            escapeHandlerRegistered = true;
            // (WPF: EventManager.RegisterClassHandler(typeof<Window>, Keyboard.PreviewKeyDownEvent, ...): a tunnelling class handler on every Window)
            InputElement.KeyDownEvent.AddClassHandler<Window>((_, ea) =>
            {
                if (ea.Key == Key.Escape && modalDismissStack.Count > 0)
                {
                    ea.Handled = true;
                    modalDismissStack.Peek().Set();
                }
            }, RoutingStrategies.Tunnel);
        }
    }

    // closes the topmost modal popup the way Escape does (used by the --selftest mode); false when no modal popup is open
    public static bool DismissTopModal()
    {
        if (modalDismissStack.Count > 0)
        {
            modalDismissStack.Peek().Set();
            return true;
        }
        else return false;
    }

    // TODO rename DoModals
    // (placeElementOntoCanvas / removeElementFromCanvas: F# functions of a (Canvas, element) tuple)
    public static async Task DoModalCoreEx(CanvasManager cm, ManualResetEvent wh, Action<Canvas, Control> placeElementOntoCanvas, Action<Canvas, Control> removeElementFromCanvas, Control element, double blackSunglassesOpacity, bool closeOnOutsideClick)
    {
        if (!GlobalFlag.popupIsActive)
            throw new Exception("DoModalCore: popupIsActive was not set, which indicates programming error where a re-entrant UI thread may crash the program");
        // rather than use MouseCapture() API, just draw a canvas over entire window which will intercept all mouse gestures
        var c = cm.CreatePopup(blackSunglassesOpacity);
        try
        {
            // place the element
            placeElementOntoCanvas(c, element);
            // catch mouse clicks outside the element
            c.PointerPressed += (_, ea) =>
            {
                var pos = ea.GetPosition(element);
                if ((pos.X < 0.0 || pos.X > element.Bounds.Width) || (pos.Y < 0.0 || pos.Y > element.Bounds.Height))
                {
                    var kind = ea.GetCurrentPoint(c).Properties.PointerUpdateKind;
                    if (!closeOnOutsideClick)
                        ea.Handled = true;   // (swallow the click: only the popup's own buttons or Escape close it)
                    else if (kind == PointerUpdateKind.LeftButtonPressed || kind == PointerUpdateKind.MiddleButtonPressed || kind == PointerUpdateKind.RightButtonPressed)
                    {
                        ea.Handled = true;
                        wh.Set();
                    }
                }
            };
            // hotkeys are routed to whatever the mouse is directly over; when the mouse is over the dimmed backdrop instead of the popup (e.g. a centered menu),
            // pass the key on to the popup so its hotkeys (like the wood sword cave's Took Sword / Took Candle) still work
            var forwarding = false;
            c.MyKeyAdd(ea =>
            {
                if (!forwarding)
                {
                    forwarding = true;
                    try
                    {
                        var (skm, k) = ea.Key;
                        element.RaiseEvent(new MyKeyRoutedEventArgs(skm, k));
                    }
                    finally
                    {
                        forwarding = false;
                    }
                    ea.Handled = true;
                }
            });
            ensureEscapeHandler();
            modalDismissStack.Push(wh);
            // (WPF: `let! _ = Async.AwaitWaitHandle(wh)` then `do! Async.SwitchToContext(ctxt)`; this await resumes on the UI thread's context)
            await AwaitWaitHandle(wh);
        }
        finally
        {
            // (always runs, even if something above threw: take this popup's own layer off, so a failed popup can't leave a gray screen behind)
            if (modalDismissStack.Contains(wh))
            {
                var keep = modalDismissStack.ToArray().Where(w => !(object.ReferenceEquals(w, wh))).ToArray();   // top first
                modalDismissStack.Clear();
                for (int k = keep.Length - 1; k >= 0; k--) modalDismissStack.Push(keep[k]);
            }
            try { removeElementFromCanvas(c, element); } catch { }
            cm.DismissPopupCanvas(c);
        }
    }

    public static Task DoModalCore(CanvasManager cm, ManualResetEvent wh, Action<Canvas, Control> placeElementOntoCanvas, Action<Canvas, Control> removeElementFromCanvas, Control element, double blackSunglassesOpacity) =>
        DoModalCoreEx(cm, wh, placeElementOntoCanvas, removeElementFromCanvas, element, blackSunglassesOpacity, true);

    // like DoModal, but clicking outside the element does not close it
    public static Task DoModalSticky(CanvasManager cm, ManualResetEvent wh, double x, double y, Control element) =>
        DoModalCoreEx(cm, wh, (c, e) => canvasAdd(c, e, x, y), (c, e) => c.Children.Remove(e), element, 0.5, false);

    public static Task DoModalOpa(CanvasManager cm, ManualResetEvent wh, double x, double y, Control element, double opa) =>
        DoModalCore(cm, wh, (c, e) => canvasAdd(c, e, x, y), (c, e) => c.Children.Remove(e), element, opa);

    public static Task DoModal(CanvasManager cm, ManualResetEvent wh, double x, double y, Control element) => DoModalOpa(cm, wh, x, y, element, 0.5);

    public static Task DoModalDockedOpa(CanvasManager cm, ManualResetEvent wh, Dock dock, Control element, double blackSunglassesOpacity)
    {
        var d = new DockPanel { Width = cm.Width, Height = cm.Height, LastChildFill = false };
        return DoModalCore(cm, wh,
                        (c, e) =>
                        {
                            DockPanel.SetDock(e, dock);
                            d.Children.Add(e);
                            canvasAdd(c, d, 0.0, 0.0);
                        },
                        (_c, e) => d.Children.Remove(e), element, blackSunglassesOpacity);
    }

    public static Task DoModalDocked(CanvasManager cm, ManualResetEvent wh, Dock dock, Control element) =>
        DoModalDockedOpa(cm, wh, dock, element, 0.5);

    /////////////////////////////////////////

    public static async Task<string> DoModalMessageBoxCore(CanvasManager cm, System.Drawing.Icon icon, string mainText, IEnumerable<string> buttonTexts, double x, double y) // returns buttonText if a button was pressed, or null if dismissed
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var mainDock = new DockPanel();
        var noIcon = icon == null;   // (no icon: the text is shown on its own, and the buttons are centered along the bottom)
        if (!noIcon)
        {
            var image = Graphics.BMPtoImage(icon.ToBitmap());
            image.HorizontalAlignment = HorizontalAlignment.Left;
            image.Margin = new Thickness(30.0, 0.0, 0.0, 0.0);
            mainDock.Children.Add(image);
            DockPanel.SetDock(image, Dock.Left);
        }
        // (WPF: a read-only, borderless, transparent, wrapped TextBox, styled orange by the TextBox style below. Avalonia's TextBox mis-measures
        // wrapped text, so this is a SelectableTextBlock (see PROGRESS.md), given the style's orange directly since it isn't a TextBox.)
        var mainTextBlock = new SelectableTextBlock { Text = mainText, Foreground = Brushes.Orange, Background = Brushes.Transparent,
                                            TextWrapping = TextWrapping.Wrap, MaxWidth = cm.AppMainCanvas.Width - x - 100.0, Width = double.NaN,
                                            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness((noIcon ? 40.0 : 12.0), 20.0, 41.0, 15.0) };
        mainDock.Children.Add(mainTextBlock);
        grid.Children.Add(mainDock);
        Grid.SetRow(mainDock, 0);

        var wh = new ManualResetEvent(false);
        string result = null;
        Panel buttonDock =
            noIcon ? new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(5.0, 0.0, 5.0, 20.0) }   // same distance from the border as the text has at the top
            : new DockPanel { Margin = new Thickness(5.0, 0.0, 0.0, 0.0) };
        var first = !noIcon;
        var lastText = buttonTexts.Last();
        foreach (var bt in (noIcon ? buttonTexts : buttonTexts.Reverse()))
        {
            var b = new Button { MinWidth = 88.0, MaxWidth = 200.0, Height = 26.0, Margin = new Thickness(5.0), HorizontalAlignment = HorizontalAlignment.Right, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
            if (first || (noIcon && bt == lastText))
            {
                b.Focus();
                first = false;
            }
            DockPanel.SetDock(b, Dock.Right);
            buttonDock.Children.Add(b);
            b.Content = new TextBox { Text = bt, IsReadOnly = true, IsHitTestVisible = false, TextAlignment = TextAlignment.Center, BorderThickness = new Thickness(0.0), FontSize = 16.0,
                                        Background = Graphics.almostBlack, Margin = new Thickness(0.0) };
            b.Click += (_, _) => { result = bt; wh.Set(); };
        }
        grid.Children.Add(buttonDock);
        Grid.SetRow(buttonDock, 1);

        var bord = new Border { Child = grid, Background = Brushes.Black, BorderThickness = new Thickness(5.0), BorderBrush = Brushes.Gray, MaxWidth = cm.AppMainCanvas.Width - x - 10.0 };
        // (WPF: implicit styles keyed by type in b.Resources; Avalonia: styles in the border's Styles, which apply to its descendants)
        var style = new Style(s => s.OfType<TextBox>());
        style.Setters.Add(new Setter(TemplatedControl.ForegroundProperty, Brushes.Orange));
        style.Setters.Add(new Setter(TemplatedControl.BackgroundProperty, Brushes.Black));
        style.Setters.Add(new Setter(TemplatedControl.BorderBrushProperty, Brushes.Orange));
        bord.Styles.Add(style);
        var style2 = new Style(s => s.OfType<Button>());
        Graphics.addRoundedButtonSetters(style2);   // (rounded dark look, like every other button)
        bord.Styles.Add(style2);

        // message boxes are centered in the app window
        bord.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var (areaW, areaH) = (cm.AppMainCanvas.Width, cm.AppMainCanvas.Height);
        var cx = areaW > 0.0 ? Math.Max(0.0, (areaW - bord.DesiredSize.Width) / 2.0) : x;
        var cy = areaH > 0.0 ? Math.Max(0.0, (areaH - bord.DesiredSize.Height) / 2.0) : y;
        // the box was measured before it was in the window (where inherited fonts can make it bigger), so re-center it once it has its real size
        bord.SizeChanged += (_, _) =>
        {
            if (areaW > 0.0) Canvas.SetLeft(bord, Math.Max(0.0, (areaW - bord.Bounds.Width) / 2.0));
            if (areaH > 0.0) Canvas.SetTop(bord, Math.Max(0.0, (areaH - bord.Bounds.Height) / 2.0));
        };
        await DoModal(cm, wh, cx, cy, bord);
        return result;
    }
    public static Task<string> DoModalMessageBox(CanvasManager cm, System.Drawing.Icon icon, string mainText, IEnumerable<string> buttonTexts) =>
        DoModalMessageBoxCore(cm, icon, mainText, buttonTexts, 150.0, 200.0);

    /////////////////////////////////////////

    // draw a pretty dashed rectangle with thickness ST around outside of (0,0) to (W,H)
    public static void MakePrettyDashesImpl(Canvas canvas, IBrush brush, double W, double H, double ST, double dash, double space)
    {
        var dashLength = dash * ST;
        var spaceLength = space * ST;
        double[] computeDashArray(double length)
        {
            var usable = length - dashLength;
            var n = (int)Math.Round(usable / (dashLength + spaceLength));
            var usableSpace = usable - (double)n * dashLength;
            var actualSpaceLength = usableSpace / (double)n;
            var a = new List<double>();
            a.Add(dash);
            for (int _i = 0; _i <= n - 1; _i++)
            {
                a.Add(actualSpaceLength / ST);
                a.Add(dash);
            }
            return a.ToArray();
        }
        // (WPF Line X1/Y1/X2/Y2 → Avalonia StartPoint/EndPoint)
        var topLine = new Line { StartPoint = new Point(-ST, -ST / 2.0), EndPoint = new Point(W + ST, -ST / 2.0), StrokeThickness = ST, Stroke = brush };
        topLine.StrokeDashArray = new AvaloniaList<double>(computeDashArray(W + 2.0 * ST));
        canvasAdd(canvas, topLine, 0.0, 0.0);
        var bottomLine = new Line { StartPoint = new Point(-ST, H + ST / 2.0), EndPoint = new Point(W + ST, H + ST / 2.0), StrokeThickness = ST, Stroke = brush };
        bottomLine.StrokeDashArray = new AvaloniaList<double>(computeDashArray(W + 2.0 * ST));
        canvasAdd(canvas, bottomLine, 0.0, 0.0);
        var leftLine = new Line { StartPoint = new Point(-ST / 2.0, -ST), EndPoint = new Point(-ST / 2.0, H + ST), StrokeThickness = ST, Stroke = brush };
        leftLine.StrokeDashArray = new AvaloniaList<double>(computeDashArray(H + 2.0 * ST));
        canvasAdd(canvas, leftLine, 0.0, 0.0);
        var rightLine = new Line { StartPoint = new Point(W + ST / 2.0, -ST), EndPoint = new Point(W + ST / 2.0, H + ST), StrokeThickness = ST, Stroke = brush };
        rightLine.StrokeDashArray = new AvaloniaList<double>(computeDashArray(H + 2.0 * ST));
        canvasAdd(canvas, rightLine, 0.0, 0.0);
    }

    public static readonly Dictionary<string, Canvas> prettyDashesCache = new Dictionary<string, Canvas>();
    public static Canvas GetPrettyDashes(string name, IBrush brush, double W, double H, double ST, double dash, double space)
    {
        if (prettyDashesCache.TryGetValue(name, out var c0))
            return c0;
        else
        {
            var c = new Canvas();
            MakePrettyDashesImpl(c, brush, W, H, ST, dash, space);
            prettyDashesCache.Add(name, c);
            return c;
        }
    }

    // TODO: factor out background brush color (assumes black)
    // (WPF: Brush; Dim reads each one's Color, so they're ISolidColorBrush here, which both Avalonia's Brushes.X and SolidColorBrush are)
    public class ModalGridSelectBrushes
    {
        private readonly ISolidColorBrush originalTileHighlightBrush, gridSelectableHighlightBrush, gridNotSelectableHighlightBrush, borderBrush;
        public ModalGridSelectBrushes(ISolidColorBrush originalTileHighlightBrush, ISolidColorBrush gridSelectableHighlightBrush, ISolidColorBrush gridNotSelectableHighlightBrush, ISolidColorBrush borderBrush)
        {
            this.originalTileHighlightBrush = originalTileHighlightBrush;
            this.gridSelectableHighlightBrush = gridSelectableHighlightBrush;
            this.gridNotSelectableHighlightBrush = gridNotSelectableHighlightBrush;
            this.borderBrush = borderBrush;
        }
        public ISolidColorBrush OriginalTileHighlightBrush => originalTileHighlightBrush;
        public ISolidColorBrush GridSelectableHighlightBrush => gridSelectableHighlightBrush;
        public ISolidColorBrush GridNotSelectableHighlightBrush => gridNotSelectableHighlightBrush;
        public ISolidColorBrush BorderBrush => borderBrush;
        public ModalGridSelectBrushes Dim(double opacity)
        {
            SolidColorBrush dim(ISolidColorBrush scb)
            {
                var c = scb.Color;
                var nc = Color.FromArgb((byte)(255.0 * opacity), c.R, c.G, c.B);
                return Graphics.freeze(new SolidColorBrush(nc));
            }
            return new ModalGridSelectBrushes(dim(this.OriginalTileHighlightBrush), dim(this.GridSelectableHighlightBrush), dim(this.GridNotSelectableHighlightBrush), dim(this.BorderBrush));
        }
        public static ModalGridSelectBrushes Defaults() =>
            new ModalGridSelectBrushes(Brushes.Lime, Brushes.Lime, Brushes.Red, Brushes.Gray);
    }

    public static readonly double borderThickness = 3.0;  // TODO should this be a param?

    // (F# unions → small record hierarchies, as HotKeys.DungeonRoomHotKey: `DismissPopupWithResult(r)` becomes
    // `new PopupClickBehavior<T>.DismissPopupWithResult(r)`, and a match arm `| DismissPopupWithResult r ->` becomes `case PopupClickBehavior<T>.DismissPopupWithResult(var r):`)
    public abstract record PopupClickBehavior<T>
    {
        private PopupClickBehavior() { }
        public sealed record DismissPopupWithResult(T Value) : PopupClickBehavior<T>;     // return result
        public sealed record DismissPopupWithNoResult() : PopupClickBehavior<T>;          // tear down popup as though user clicked outside it
        public sealed record StayPoppedUp() : PopupClickBehavior<T>;                      // keep awaiting more clicks
    }
    public abstract record GridClickDismissalWarpReturn
    {
        private GridClickDismissalWarpReturn() { }
        public sealed record NoWarp() : GridClickDismissalWarpReturn;
        public sealed record WarpTo(Point Pos) : GridClickDismissalWarpReturn;
        public sealed record WarpToCenter() : GridClickDismissalWarpReturn;
    }
    /*
    CustomComboBoxes.DoModalGridSelect(cm, tileX, tileY, tileCanvas, gridElementsSelectablesAndIDs, originalStateIndex, activationDelta, (gnc, gnr, gcw, grh),
        gx, gy, redrawTile, onClick, extraDecorations, brushes, gridClickDismissalWarpReturn, who)
    */
    // where the movable dungeon-room popup grids (monster / floor drop detail) currently are, relative to their tile: x, y, pixel width, pixel height;
    // those popups use it to keep their name caption under the grid
    public static (double, double, double, double) MovableGridRect = (0.0, 0.0, 0.0, 0.0);
    // (port helper: WPF's Cursors.X are shared static cursors; Avalonia cursors are objects made on demand (after the app has started), so they're made once here)
    private static readonly Dictionary<StandardCursorType, Cursor> stdCursors = new Dictionary<StandardCursorType, Cursor>();
    private static Cursor stdCursor(StandardCursorType t)
    {
        if (!stdCursors.TryGetValue(t, out var c)) { c = new Cursor(t); stdCursors[t] = c; }
        return c;
    }
    // (returns (hasResult, result): F#'s `'Result option`, as a value tuple because 'Result may be a value type)
    public static async Task<(bool hasResult, TResult result)> DoModalGridSelect<TState, TResult>(
            CanvasManager cm, double tileX, double tileY, Canvas tileCanvas, // tileCanvas - an empty Canvas with just Width and Height set, on which you will redrawTile your preview-tile
                    (Control, bool, TState)[] gridElementsSelectablesAndIDs, // array of display elements, whether they're selectable, and your stateID name/identifier for them
                    int originalStateIndex, // originalStateIndex is array index into the array
                    int activationDelta, // activationDelta is -1/0/1 if we should give initial input of scrollup/none/scrolldown
                    (int, int, int, int) gridShape,   // grid: numCols, numRows, colWidth, rowHeight (heights of elements; this control will add border highlight)
                    double idealX, double idealY,         // in a tile that is gcw by gch size, when the mouse cursor is being warped to its 'center', where should that be? good default: (gcw/2,grh/2)
                    double gx, double gy,   // where to place grid (x,y) relative to (0,0) being the (unbordered) corner of your TileCanvas
                    Action<TState> redrawTile,  // we pass you currentStateID
                    Func<PointerPressedEventArgs, TState, PopupClickBehavior<TResult>> onClick,  // called on tile click or selectable grid click, you choose what to do:   (mousebuttonEA, currentStateID) -> PopupClickBehavior<'Result>
                    IEnumerable<(Control, double, double)> extraDecorations,  // extra things to draw at (x,y)s
                    ModalGridSelectBrushes brushes,
                    GridClickDismissalWarpReturn gridClickDismissalWarpReturn,
                    ManualResetEvent who,        // pass an unset one, if caller wants to be able to early-dismiss the dialog on its own (null: None)
                    string uniqueName,      // for PrettyDashes caching
                    double? opaOpt     // blackSunglassesOpacity
                    )
    {
        var (gnc, gnr, gcw, grh) = gridShape;
        GlobalFlag.popupOpenStamp = DateTime.Now;
        var wh = who ?? new ManualResetEvent(false);
        var hasResult = false;
        TResult result = default(TResult);
        var popupCanvas = new Canvas();  // we will draw outside the canvas
        canvasAdd(popupCanvas, tileCanvas, 0.0, 0.0);
        var ST = borderThickness;
        var pretty = GetPrettyDashes(uniqueName, brushes.OriginalTileHighlightBrush, tileCanvas.Width, tileCanvas.Height, ST, 2.0, 1.2);
        popupCanvas.Children.Add(pretty);
        if (gridElementsSelectablesAndIDs.Length > gnr * gnc)
            throw new Exception("the grid is not big enough to accomodate all the choices");
        // Shift+drag rearranging is supported in the item-box, overworld-tile and dungeon-room (room type / monster / floor drop) menus; the layout is remembered in the settings file
        var isReorderable = (uniqueName == "ItemBox" || uniqueName == "OWGridTile" || uniqueName == "DungeonRoom" || uniqueName == "MonsterDetail" || uniqueName == "FloorDropDetail");
        var total = gnc * gnr;
        string keyOf((Control, bool, TState) it) { var (ic, _, st) = it; return ic == null ? "" : ((object)st).ToString(); }
        (Control, bool, TState)[] defaultItems =
            Enumerable.Range(0, total).Select(n => n < gridElementsSelectablesAndIDs.Length ? gridElementsSelectablesAndIDs[n] : ((Control)null, false, default(TState))).ToArray();
        var originalKey = keyOf(defaultItems[originalStateIndex]);
        (Control, bool, TState)[] items = ((Control, bool, TState)[])defaultItems.Clone();
        if (isReorderable)
        {
            if (TrackerModelOptions.MenuLayouts.TryGetValue(uniqueName, out var saved) && saved != null)
            {
                var byKey = new Dictionary<string, (Control, bool, TState)>();
                foreach (var it in defaultItems)
                {
                    var k = keyOf(it);
                    if (k != "" && !(byKey.ContainsKey(k))) byKey[k] = it;
                }
                var used = new HashSet<string>();
                (Control, bool, TState)[] placed = Enumerable.Repeat(((Control)null, false, default(TState)), total).ToArray();
                for (int pos = 0; pos <= Math.Min(saved.Length, total) - 1; pos++)
                {
                    var k = saved[pos];
                    if (k != "" && byKey.ContainsKey(k) && used.Add(k)) placed[pos] = byKey[k];
                }
                // anything not in the saved layout (e.g. a newly added item) goes into the first free spots, in default order
                var leftovers = new List<(Control, bool, TState)>();
                foreach (var it in defaultItems)
                {
                    var k = keyOf(it);
                    if (k != "" && !(used.Contains(k))) leftovers.Add(it);
                }
                var freeSpots = new List<int>();
                for (int pos = 0; pos <= total - 1; pos++)
                {
                    var (ic, _, _) = placed[pos];
                    if (ic == null) freeSpots.Add(pos);
                }
                if (leftovers.Count <= freeSpots.Count)
                {
                    // several new items (e.g. the shop buttons) stay together in order, starting at the first free row where they all fit side by side; otherwise they take the first free spots
                    var n = leftovers.Count;
                    var freeSet = new HashSet<int>(freeSpots);
                    int? rowStart = null;
                    if (n > 1 && n <= gnc)
                    {
                        for (int r = 0; r <= (total / gnc) - 1; r++)
                        {
                            var st = r * gnc;
                            if (Enumerable.Range(st, n).All(k => freeSet.Contains(k))) { rowStart = st; break; }
                        }
                    }
                    if (rowStart is int st0)
                    {
                        for (int i = 0; i < leftovers.Count; i++) placed[st0 + i] = leftovers[i];
                    }
                    else
                    {
                        for (int i = 0; i < leftovers.Count; i++) placed[freeSpots[i]] = leftovers[i];
                    }
                    Array.Copy(placed, 0, items, 0, total);
                }
            }
        }
        int findOrig()
        {
            for (int i = 0; i < items.Length; i++)
            {
                var (ic, _, _) = items[i];
                if (ic != null && keyOf(items[i]) == originalKey) return i;
            }
            return -1;
        }
        var origIdx0 = findOrig();
        var origIdx = origIdx0 >= 0 ? origIdx0 : originalStateIndex;
        var dragFrom = -1;
        var (COLW, ROWH) = (gcw + 2 * (int)ST, grh + 2 * (int)ST);
        var firstRowExtra = uniqueName == "ItemBox" ? 6 : 0;   // the item menu's top row (the swords) is taller, so the sword icons can be bigger
        var grid = makeGrid(gnc, gnr, COLW, ROWH);
        if (firstRowExtra > 0) grid.RowDefinitions[0].Height = new GridLength((double)(ROWH + firstRowExtra));
        grid.Background = Brushes.Black;
        // the dungeon-room monster and floor-item menus are shown 2x larger (the grid scales from its top-left corner; cell hit-testing and arrow-key navigation follow the transform)
        var gridScale = uniqueName == "MonsterDetail" || uniqueName == "FloorDropDetail" ? 2.0 : uniqueName == "ItemBox" || uniqueName == "OWGridTile" ? 1.25 : uniqueName == "DungeonRoom" ? 4.0 / 3.0 : 1.0;
        // (Avalonia's RenderTransformOrigin defaults to the center; WPF's is the top-left corner)
        var gridBorder = new Border { BorderThickness = new Thickness(ST), BorderBrush = brushes.BorderBrush, Child = grid, RenderTransformOrigin = RelativePoint.TopLeft };
        // the dungeon-room monster and floor drop menus can be moved (drag the little bar above the grid) and resized (drag the corner grip); the placement is remembered
        var tipUnderGrid = (uniqueName == "MonsterDetail" || uniqueName == "FloorDropDetail" || uniqueName == "DungeonRoom");   // (these menus keep their tip box under the grid)
        // the item menus (dungeon / overworld item boxes and the overworld tile menu) can be moved and resized too, in 'Jump to mouse' mode: the menu opens at its saved spot, the mouse jumps into it, and jumps back after a choice (like the monster menu).
        // In 'Anchor to mouse' mode (the original behavior) the menu opens next to the tile you clicked.
        var isJumpCapable = (uniqueName == "ItemBox" || uniqueName == "OWGridTile");
        var jumpKey = uniqueName + "Jump";
        var jumpMode = (isJumpCapable && (TrackerModelOptions.MenuLayouts.TryGetValue(jumpKey, out var jv) && jv != null && jv.Length == 1 && jv[0] == "1"));
        var isMovable = tipUnderGrid || isJumpCapable;
        bool moveActive() => !isJumpCapable || jumpMode;   // whether dragging the menu to a new spot is on (always, except in 'Anchor to mouse' mode)
        var gripOnLeft = true;   // the resize grip is normally at the bottom-left (the right edge stays put while resizing); it moves to the bottom-right when the left side would be off screen
        var gripLeft = gripOnLeft;
        var gripBusy = false;   // true while the grip is being dragged (it stays on its side meanwhile)
        var placeKey = uniqueName + "Placement";
        var isResizable = isMovable;
        double offX = 0.0, offY = 0.0, userScale = 1.0;
        if (isResizable)
        {
            if (TrackerModelOptions.MenuLayouts.TryGetValue(placeKey, out var v) && v != null && v.Length == 3)
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var ok0 = double.TryParse(v[0], System.Globalization.NumberStyles.Float, inv, out var _a);
                var ok1 = double.TryParse(v[1], System.Globalization.NumberStyles.Float, inv, out var _b);
                var ok2 = double.TryParse(v[2], System.Globalization.NumberStyles.Float, inv, out var c);
                if (ok0 && ok1 && ok2)
                    userScale = Math.Max(0.5, Math.Min(4.0, c));
            }
        }
        if (gridScale != 1.0 || userScale != 1.0)
        {
            gridBorder.RenderTransform = new ScaleTransform(gridScale * userScale, gridScale * userScale);
            RenderOptions.SetBitmapInterpolationMode(gridBorder, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
        }
        // keep the (enlarged) overworld-tile menu inside the app window: slide it up if it would run off the bottom, and put the tip box on the left if it would run off the right
        var (gridPixW, gridPixH) = (((double)(gnc * COLW) + 2.0 * ST) * gridScale, ((double)(gnr * ROWH + firstRowExtra) + 2.0 * ST) * gridScale);
        // every popup grid stays inside the app window: slide it up/left if it would run off the bottom/right, and down/right if off the top/left
        var (appW, appH) = (cm.AppMainCanvas.Width, cm.AppMainCanvas.Height);
        if (appW > 0.0 && appH > 0.0)
        {
            double shiftIntoRange(double tilePos, double g, double size, double limit)
            {
                var over = tilePos + g + size - limit;
                var g2 = over > 0.0 ? g - Math.Ceiling(over) : g;
                return tilePos + g2 < 0.0 ? -tilePos : g2;
            }
            (gx, gy) = (shiftIntoRange(tileX, gx, gridPixW, appW), shiftIntoRange(tileY, gy, gridPixH + (uniqueName == "MonsterDetail" || uniqueName == "DungeonRoom" ? 80.0 : 0.0), appH));   // (those two menus have their tip box under the grid: leave room for it)
        }
        canvasAdd(popupCanvas, gridBorder, gx, gy);
        // a moved menu stays at the same spot on screen (its position is saved absolutely, not relative to the tile you clicked)
        void applySavedAbs()
        {
            if (TrackerModelOptions.MenuLayouts.TryGetValue(placeKey + "AbsV2", out var v) && v != null && v.Length == 2)
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var ok0 = double.TryParse(v[0], System.Globalization.NumberStyles.Float, inv, out var a);
                var ok1 = double.TryParse(v[1], System.Globalization.NumberStyles.Float, inv, out var b);
                if (ok0 && ok1)
                {
                    // never let a saved spot put the menu off the window
                    var a2 = appW > 0.0 ? Math.Max(0.0, Math.Min(a, appW - 80.0)) : a;
                    var b2 = appH > 0.0 ? Math.Max(50.0, Math.Min(b, appH - 60.0)) : b;
                    offX = a2 - tileX - gx;
                    offY = b2 - tileY - gy;
                }
            }
        }
        if (isMovable && moveActive()) applySavedAbs();
        var noteTipText = "Tip: hold Shift and\ndrag an icon to move it\nto your preferred spot.\n(Saved for next time.)";
        TextBlock noteTextBlock = null;
        Border noteBox = null;   // (F#: Border option; null for None)
        if (isReorderable)
        {
            var tb = new TextBlock { Text = noteTipText, Foreground = Brushes.Gainsboro, FontSize = 12.0, TextWrapping = TextWrapping.Wrap, Width = 120.0 };
            noteTextBlock = tb;
            var b = new Border { Child = tb, Background = Brushes.Black, BorderBrush = brushes.BorderBrush, BorderThickness = new Thickness(1.0), Padding = new Thickness(6.0), IsHitTestVisible = false };
            var rightX = gx + gridPixW + 6.0;
            var noteX = tileX + rightX + 132.0 > cm.AppMainCanvas.Width ? gx - 132.0 - 6.0 : rightX;
            if (uniqueName == "MonsterDetail" || uniqueName == "DungeonRoom") canvasAdd(popupCanvas, b, gx, gy + gridPixH + 8.0);   // under the left part of the grid (the monster's name is to its right)
            else if (tileX + noteX < 0.0) canvasAdd(popupCanvas, b, gx, gy + gridPixH + 6.0);   // no room on either side: put it under the grid
            else canvasAdd(popupCanvas, b, noteX, gy);
            noteBox = b;
        }
        // the placement starts locked so a stray click near the edge can't move it; click the lock to unlock, then drag / resize, and lock it again
        var lockKey = uniqueName + "Locked";
        var isLocked = !(TrackerModelOptions.MenuLayouts.TryGetValue(lockKey, out var lv) && lv != null && lv.Length == 1 && lv[0] == "0");
        var lockText = new TextBlock { FontSize = 10.0, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
        // the lock bar is a tall vertical strip beside the tip box
        // (WPF: lockText.LayoutTransform <- RotateTransform(-90.); Avalonia has no LayoutTransform property, so the text sits in a LayoutTransformControl)
        var lockTextRotated = new LayoutTransformControl { LayoutTransform = new RotateTransform(-90.0), Child = lockText, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
        // (WPF Cursors.X → Avalonia standard cursors; SizeNESW/SizeNWSE are the bottom-left/bottom-right corner cursors)
        var handCursor = stdCursor(StandardCursorType.Hand);
        var arrowCursor = stdCursor(StandardCursorType.Arrow);
        var sizeAllCursor = stdCursor(StandardCursorType.SizeAll);
        var sizeNESWCursor = stdCursor(StandardCursorType.BottomLeftCorner);
        var sizeNWSECursor = stdCursor(StandardCursorType.BottomRightCorner);
        var lockBtn = new Border { Width = 24.0, Background = Brushes.Black, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1.0), Cursor = handCursor,
                                     Child = lockTextRotated };
        ToolTip.SetTip(lockBtn, "Lock / unlock this menu's position and size");
        // 'Anchor to mouse' (the menu opens next to the tile) / 'Jump to mouse' (the menu opens at its saved spot and the mouse jumps into it, then back to the tile after a choice)
        Border mkModeBtn(string text, string tip)
        {
            var mb = new Border { Background = Brushes.Black, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1.0), Padding = new Thickness(4.0, 2.0, 4.0, 2.0), Cursor = handCursor,
                       Child = new TextBlock { Text = text, FontSize = 11.0, Foreground = Brushes.Gainsboro, IsHitTestVisible = false, TextAlignment = TextAlignment.Center } };
            ToolTip.SetTip(mb, tip);
            return mb;
        }
        var anchorBtn = mkModeBtn("Anchor to mouse", "The menu opens next to the tile you clicked (the original behavior)");
        var jumpBtn = mkModeBtn("Jump to mouse", "The menu opens at its saved spot, the mouse jumps into it, and jumps back to the tile after you choose");
        anchorBtn.Width = 110.0;
        jumpBtn.Width = 110.0;
        void refreshModeLook()
        {
            foreach (var (b, on) in new[] { (anchorBtn, !jumpMode), (jumpBtn, jumpMode) })
            {
                b.BorderBrush = (on ? Brushes.Orange : Brushes.Gray);
                ((TextBlock)b.Child).Foreground = (on ? Brushes.Orange : Brushes.Gainsboro);
            }
        }
        refreshModeLook();
        var hintText = new TextBlock { Foreground = Brushes.Gray, FontSize = 10.0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        var headerGrid = new Grid();
        headerGrid.Children.Add(hintText);
        var hideHintAndLock = isMovable && TrackerModelOptions.HidePopupHintAndLock.Value;
        if (hideHintAndLock)
        {
            if (noteBox != null) popupCanvas.Children.Remove(noteBox);   // (not just hidden: removed, so nothing of it can show)
        }
        var moveHeader = new Border { Height = 14.0, Background = Brushes.Black, BorderBrush = brushes.BorderBrush, BorderThickness = new Thickness(1.0), Child = headerGrid };
        void refreshLockLook()
        {
            if (noteTextBlock != null && !(moveActive())) noteTextBlock.Text = noteTipText;
            if (isLocked)
            {
                lockText.Text = "LOCKED";
                lockText.Foreground = Brushes.Gray;
                if (noteTextBlock != null && isMovable && moveActive()) noteTextBlock.Text = noteTipText + "\n\nClick LOCKED (the bar) to unlock this menu so it can be moved / resized.";
                moveHeader.Cursor = arrowCursor;
                hintText.Text = "";
            }
            else
            {
                lockText.Text = "UNLOCKED";
                lockText.Foreground = Brushes.Orange;
                if (noteTextBlock != null && isMovable && moveActive()) noteTextBlock.Text = noteTipText + "\n\nUNLOCKED: drag the bar to move this menu, drag the corner grip to resize it.";
                moveHeader.Cursor = sizeAllCursor;
                hintText.Text = "drag here to move";
            }
        }
        var sizeGrip = new Border { Width = 32.0, Height = 32.0, Background = Brushes.Black, BorderBrush = brushes.BorderBrush, BorderThickness = new Thickness(1.0), Cursor = (gripOnLeft ? sizeNESWCursor : sizeNWSECursor),
                                      Child = new TextBlock { Text = (gripOnLeft ? "◣" : "◢"), Foreground = Brushes.Gray, FontSize = 20.0, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false } };
        ToolTip.SetTip(sizeGrip, "drag to resize");
        void layoutMovables()
        {
            if (isResizable)
            {
                if (uniqueName == "OWGridTile")
                {
                    // the overworld tile menu opens next to its tile: don't let a (saved) size make it run off the window
                    var (areaW, areaH) = (cm.AppMainCanvas.Width, cm.AppMainCanvas.Height);
                    if (areaW > 0.0 && areaH > 0.0)
                    {
                        var maxS = Math.Max(0.5, Math.Min(areaW / gridPixW, areaH / gridPixH));
                        if (userScale > maxS) userScale = maxS;
                        var s0 = userScale;
                        var (w0, h0) = (gridPixW * s0, gridPixH * s0);
                        var (left0, top0) = (tileX + gx + offX, tileY + gy + offY);
                        var left1 = Math.Max(0.0, Math.Min(left0, areaW - w0));
                        var top1 = Math.Max(0.0, Math.Min(top0, areaH - h0 - 20.0));
                        offX = offX + (left1 - left0);
                        offY = offY + (top1 - top0);
                    }
                }
                var s = userScale;
                gridBorder.RenderTransform = new ScaleTransform(gridScale * s, gridScale * s);
                var (X, Y) = (gx + offX, gy + offY);
                var (W, H) = (gridPixW * s, gridPixH * s);
                Canvas.SetLeft(gridBorder, X);
                Canvas.SetTop(gridBorder, Y);
                moveHeader.Width = W;
                Canvas.SetLeft(moveHeader, X);
                Canvas.SetTop(moveHeader, (uniqueName == "DungeonRoom" ? Y + H + 55.0 : Y - 16.0));   // (the room menu has its caption frame around the grid, so its bar goes under the frame)
                var gripLeftX = X - (uniqueName == "DungeonRoom" ? 42.0 : 34.0);
                if (!gripBusy)
                {
                    gripLeft = (tileX + gripLeftX >= 0.0 || (appW > 0.0 && tileX + X + W + 2.0 + 32.0 > appW));   // (X is relative to the tile, so add the tile's position to compare with the window)   // moves to the right side of the menu if the left side would be off screen
                    sizeGrip.Cursor = (gripLeft ? sizeNESWCursor : sizeNWSECursor);
                    ((TextBlock)sizeGrip.Child).Text = (gripLeft ? "◣" : "◢");
                }
                Canvas.SetLeft(sizeGrip, (gripLeft ? gripLeftX : X + W + 2.0));
                Canvas.SetTop(sizeGrip, Y + H - 30.0);
                if (noteBox != null && !tipUnderGrid)
                {
                    var b = noteBox;
                    b.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    var wasTop = Math.Abs(Canvas.GetTop(b) - gy) < 1.0;
                    if (Canvas.GetLeft(b) > gx)
                    {
                        Canvas.SetLeft(b, X + W + 6.0);   // the tip box was to the right of the grid; keep it there
                        if (wasTop) Canvas.SetTop(b, Y);
                    }
                    else if (Canvas.GetLeft(b) < gx)
                    {
                        Canvas.SetLeft(b, X - b.DesiredSize.Width - 6.0);   // it was to the left of the grid; keep it there
                        if (wasTop) Canvas.SetTop(b, Y);
                    }
                    if (isJumpCapable && !hideHintAndLock)
                    {
                        // the lock bar goes beside the tip box; the two mode toggles sit above the grid's top corners
                        var (tipLeft, tipTop) = (Canvas.GetLeft(b), Canvas.GetTop(b));
                        lockBtn.Height = b.DesiredSize.Height;
                        Canvas.SetLeft(lockBtn, (tipLeft < X - 1.0 ? tipLeft - 30.0 : tipLeft + b.DesiredSize.Width + 6.0));
                        Canvas.SetTop(lockBtn, tipTop);
                        if (uniqueName == "ItemBox")
                        {
                            // the item menu: two small buttons along the bottom edge, under the explainer
                            var decoBottom = extraDecorations.Select(t => { var (d, _, dy) = t; return dy + (double.IsNaN(d.Height) ? 0.0 : d.Height); }).Aggregate(0.0, Math.Max);
                            var btnTop = (Math.Max(decoBottom + offY, Y + H)) + 4.0;
                            var bw = Math.Floor((W - 2.0) / 2.0);
                            foreach (var btn in new[] { anchorBtn, jumpBtn })
                            {
                                btn.Width = bw;
                                btn.Padding = new Thickness(2.0, 1.0, 2.0, 1.0);
                                ((TextBlock)btn.Child).FontSize = 9.0;
                            }
                            Canvas.SetLeft(anchorBtn, X);
                            Canvas.SetTop(anchorBtn, btnTop);
                            Canvas.SetLeft(jumpBtn, X + W - bw);
                            Canvas.SetTop(jumpBtn, btnTop);
                        }
                        else
                        {
                            var btnTop = (moveActive() && !isLocked ? Y - 46.0 : Y - 30.0);   // right above the grid (above the drag bar while that is showing)
                            Canvas.SetLeft(anchorBtn, X);
                            Canvas.SetTop(anchorBtn, btnTop);
                            Canvas.SetLeft(jumpBtn, X + W - jumpBtn.Width);
                            Canvas.SetTop(jumpBtn, btnTop);
                        }
                    }
                }
                else if (noteBox != null)
                {
                    var b = noteBox;
                    var noteTop = (uniqueName == "DungeonRoom" ? Y + H + 75.0 : Y + H + 8.0 + (uniqueName == "FloorDropDetail" ? 36.0 : 0.0));
                    Canvas.SetLeft(b, X);
                    Canvas.SetTop(b, noteTop);
                    // the lock bar: a tall vertical strip just to the right of the tip box, the same height
                    if (!hideHintAndLock)
                    {
                        b.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        lockBtn.Height = b.DesiredSize.Height;
                        Canvas.SetLeft(lockBtn, X + b.DesiredSize.Width + 6.0);
                        Canvas.SetTop(lockBtn, noteTop);   // under the grid (the floor drop's name goes between)
                    }
                }
                if (isJumpCapable)
                {
                    // the explainer under the grid moves with it
                    foreach (var (d, dx, dy) in extraDecorations)
                    {
                        Canvas.SetLeft(d, dx + offX);
                        Canvas.SetTop(d, dy + offY);
                    }
                }
                MovableGridRect = (X, Y, W, H);
            }
        }
        void refreshLock()
        {
            refreshLockLook();
            sizeGrip.IsVisible = !(isMovable && isLocked && moveActive());
            moveHeader.IsVisible = !(isLocked || hideHintAndLock || !(moveActive()));
            lockBtn.IsVisible = !(hideHintAndLock || !(moveActive()));
        }
        if (isResizable)
        {
            moveHeader.ZIndex = 50;
            sizeGrip.ZIndex = 50;
            lockBtn.ZIndex = 50;
            if (isMovable && !hideHintAndLock) popupCanvas.Children.Add(moveHeader);
            if (isMovable && !hideHintAndLock) popupCanvas.Children.Add(lockBtn);
            popupCanvas.Children.Add(sizeGrip);
            refreshLock();
            layoutMovables();
            if (isJumpCapable && !hideHintAndLock)
            {
                anchorBtn.ZIndex = 50;
                jumpBtn.ZIndex = 50;
                popupCanvas.Children.Add(anchorBtn);
                popupCanvas.Children.Add(jumpBtn);
                void setMode(bool jump)
                {
                    jumpMode = jump;
                    TrackerModelOptions.MenuLayouts[jumpKey] = new[] { (jump ? "1" : "0") };
                    if (jump) applySavedAbs(); else { offX = 0.0; offY = 0.0; }
                    TrackerModelOptions.writeSettings();
                    refreshModeLook();
                    refreshLock();
                    layoutMovables();
                }
                // (WPF MouseLeftButtonDown)
                anchorBtn.PointerPressed += (_, ea) => { if (ea.GetCurrentPoint(anchorBtn).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed) { setMode(false); ea.Handled = true; } };
                jumpBtn.PointerPressed += (_, ea) => { if (ea.GetCurrentPoint(jumpBtn).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed) { setMode(true); ea.Handled = true; } };
            }
        }
        var currentState = origIdx;   // the only bit of local mutable state during the modal - it ranges from 0..gridElements.Length-1
        var allDoneSoNoLongerNeedToMouseLeaveSnapback = false;
        var canvasesToClear = new List<Canvas>();
        void selfCleanup()
        {
            foreach (var (d, _x, _y) in extraDecorations)
                popupCanvas.Children.Remove(d);
            popupCanvas.Children.Remove(pretty);
            if (noteBox != null) popupCanvas.Children.Remove(noteBox);
            popupCanvas.Children.Remove(moveHeader);
            popupCanvas.Children.Remove(lockBtn);
            popupCanvas.Children.Remove(sizeGrip);
            popupCanvas.Children.Remove(anchorBtn);
            popupCanvas.Children.Remove(jumpBtn);
            foreach (var c in canvasesToClear)
                c.Children.Clear();
        }
        void dismiss()
        {
            wh.Set();
        }
        bool isSelectable() { var (_, s, _) = items[currentState]; return s; }
        TState stateID() { var (_, _, x) = items[currentState]; return x; }
        var redrawGridFuncs = new List<Action>();
        void changeCurrentState(int newState)
        {
            currentState = newState;
            redrawTile(stateID());
            foreach (var r in redrawGridFuncs) r();
        }
        void next()
        {
            currentState = (currentState + 1) % items.Length;
            while (!(isSelectable()))
                currentState = (currentState + 1) % items.Length;
            changeCurrentState(currentState);
        }
        void prev()
        {
            currentState = (currentState - 1 + items.Length) % items.Length;
            while (!(isSelectable()))
                currentState = (currentState - 1 + items.Length) % items.Length;
            changeCurrentState(currentState);
        }
        void snapBack() { if (!allDoneSoNoLongerNeedToMouseLeaveSnapback) changeCurrentState(origIdx); }     // don't redraw and do other work after the popup is done (MouseLeave can be very error-prone if not careful!)
        // original tile
        redrawTile(stateID());
        if (isResizable)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            void saveMovable()
            {
                TrackerModelOptions.MenuLayouts[placeKey] = new[] { "0", "0", userScale.ToString(inv) };
                if (isMovable && moveActive()) TrackerModelOptions.MenuLayouts[placeKey + "AbsV2"] = new[] { (tileX + gx + offX).ToString(inv), (tileY + gy + offY).ToString(inv) };
                TrackerModelOptions.writeSettings();
            }
            void relayout()
            {
                layoutMovables();
                redrawTile(stateID());
            }
            var dragging = false;
            var last = new Point();
            // (WPF MouseLeftButtonDown / MouseMove / MouseLeftButtonUp, and CaptureMouse()/ReleaseMouseCapture() → the pointer's Capture)
            lockBtn.PointerPressed += (_, ea) =>
            {
                if (ea.GetCurrentPoint(lockBtn).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed) return;
                isLocked = !isLocked;
                TrackerModelOptions.MenuLayouts[lockKey] = new[] { (isLocked ? "1" : "0") };
                TrackerModelOptions.writeSettings();
                refreshLock();
                layoutMovables();
                ea.Handled = true;
            };
            moveHeader.PointerPressed += (_, ea) =>
            {
                if (ea.GetCurrentPoint(moveHeader).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed) return;
                if (!isLocked)
                {
                    dragging = true;
                    last = ea.GetPosition(popupCanvas);
                    ea.Pointer.Capture(moveHeader);
                }
                ea.Handled = true;
            };
            moveHeader.PointerMoved += (_, ea) =>
            {
                if (dragging)
                {
                    var p = ea.GetPosition(popupCanvas);
                    offX = offX + (p.X - last.X);
                    offY = offY + (p.Y - last.Y);
                    last = p;
                    relayout();
                }
            };
            moveHeader.PointerReleased += (_, ea) =>
            {
                if (ea.InitialPressMouseButton != MouseButton.Left) return;
                if (dragging)
                {
                    dragging = false;
                    ea.Pointer.Capture(null);
                    saveMovable();
                }
                ea.Handled = true;
            };
            var resizing = false;
            double startX = 0.0, startScale = 1.0, startOffX = 0.0;
            sizeGrip.PointerPressed += (_, ea) =>
            {
                if (ea.GetCurrentPoint(sizeGrip).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed) return;
                if (!(isMovable && isLocked && moveActive()))
                {
                    resizing = true;
                    gripBusy = true;
                    startX = ea.GetPosition(popupCanvas).X;
                    startScale = userScale;
                    startOffX = offX;
                    ea.Pointer.Capture(sizeGrip);
                }
                ea.Handled = true;
            };
            sizeGrip.PointerMoved += (_, ea) =>
            {
                if (resizing)
                {
                    var dx = ea.GetPosition(popupCanvas).X - startX;
                    var w0 = gridPixW * startScale;
                    var dw = gripLeft ? -dx : dx;   // dragging the left grip further left makes it bigger
                    userScale = Math.Max(0.5, Math.Min(4.0, startScale * (w0 + dw) / w0));
                    if (gripLeft) offX = startOffX - (gridPixW * userScale - w0);   // keep the right edge fixed
                    relayout();
                }
            };
            sizeGrip.PointerReleased += (_, ea) =>
            {
                if (ea.InitialPressMouseButton != MouseButton.Left) return;
                if (resizing)
                {
                    resizing = false;
                    gripBusy = false;
                    ea.Pointer.Capture(null);
                    saveMovable();
                }
                ea.Handled = true;
            };
        }
        var centerOfTile = new Point(tileX + tileCanvas.Width / 2.0, tileY + tileCanvas.Height / 2.0);   // use actual center, not the "ideal" coords, as we want the most wiggle-room, even if mouse obscures item a little
        // you can scroll to activate a box along its edge, such that clicking after the scroll ends up outside the tile; center the mouse when scrolling, as scrolling often jiggles it too
        void recenterMouse() => Graphics.SilentlyWarpMouseCursorTo(centerOfTile);
        // the monster / floor drop menus (opened by scrolling a dungeon room) bring the mouse into the menu, onto its top-left choice
        if (uniqueName == "MonsterDetail" || uniqueName == "FloorDropDetail")
        {
            var (cx, cy) = (0, 0);   // the top-left choice
            var sc = gridScale * userScale;
            Graphics.WarpMouseCursorTo(new Point(tileX + gx + offX + sc * (ST + ((double)cx + 0.5) * (double)COLW), tileY + gy + offY + sc * (ST + ((double)cy + 0.5) * (double)ROWH)));
        }
        // the item menus in 'Jump to mouse' mode bring the mouse into the menu, onto the current choice
        if (isJumpCapable && jumpMode)
        {
            var sc = gridScale * userScale;
            var (cx, cy) = (origIdx % gnc, origIdx / gnc);
            var yOff = cy > 0 ? (double)firstRowExtra : (double)firstRowExtra / 2.0;
            Graphics.WarpMouseCursorTo(new Point(tileX + gx + offX + sc * (ST + ((double)cx + 0.5) * (double)COLW), tileY + gy + offY + sc * (ST + (double)(cy * ROWH) + yOff + (double)ROWH / 2.0)));
        }
        if (activationDelta != 0 && !(isJumpCapable && jumpMode))
            recenterMouse();
        // (WPF MouseWheel: Delta is ±120 per notch, Avalonia's is ±1.0; only the sign is used. WPF's MouseWheel is only the vertical wheel; Avalonia
        // also reports horizontal scrolling (e.g. a sideways trackpad swipe) here with Delta.Y = 0, which WPF never delivers, so that is ignored)
        tileCanvas.PointerWheelChanged += (s, x) => { var notch = Graphics.wheelNotch(s, x); if (notch == 0) return; recenterMouse(); if (notch < 0) next(); else prev(); };   // (whole notches only: trackpads send tiny deltas, and sideways scrolling, which WPF's MouseWheel never reports; see PROGRESS.md)
        tileCanvas.PointerPressed += (_, ea) =>
        {
            ea.Handled = true;
            switch (onClick(ea, stateID()))
            {
                case PopupClickBehavior<TResult>.DismissPopupWithResult(var r): hasResult = true; result = r; dismiss(); break;
                case PopupClickBehavior<TResult>.DismissPopupWithNoResult: dismiss(); break;
                case PopupClickBehavior<TResult>.StayPoppedUp: break;
            }
        };
        tileCanvas.PointerExited += (_, _) => snapBack();
        // (Avalonia's TranslatePoint returns null when the two aren't in one tree; WPF throws, as .Value does)
        Point centerOf(int x, int y) => grid.TranslatePoint(new Point(ST + (double)(x * COLW) + idealX, ST + (double)(y * ROWH) + (y > 0 ? (double)firstRowExtra : 0.0) + idealY), cm.AppMainCanvas).Value;
        tileCanvas.MyKeyAdd(ea =>
        {
            var (x, y) = (currentState % gnc, currentState / gnc);
            if (!HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key, out var target)) return;
            switch (target)
            {
                // 'arrows' warp mouse to current entry, then apply arrow to nav thru grid
                case HotKeys.GlobalHotkeyTargets.MoveCursorRight:
                    ea.Handled = true;
                    Graphics.NavigationallyWarpMouseCursorTo(centerOf(x, y));
                    if (x < gnc - 1)
                        Graphics.NavigationallyWarpMouseCursorTo(centerOf(x + 1, y));
                    break;
                case HotKeys.GlobalHotkeyTargets.MoveCursorLeft:
                    ea.Handled = true;
                    Graphics.NavigationallyWarpMouseCursorTo(centerOf(x, y));
                    if (x > 0)
                        Graphics.NavigationallyWarpMouseCursorTo(centerOf(x - 1, y));
                    break;
                case HotKeys.GlobalHotkeyTargets.MoveCursorUp:
                    ea.Handled = true;
                    Graphics.NavigationallyWarpMouseCursorTo(centerOf(x, y));
                    if (y > 0)
                        Graphics.NavigationallyWarpMouseCursorTo(centerOf(x, y - 1));
                    break;
                case HotKeys.GlobalHotkeyTargets.MoveCursorDown:
                    ea.Handled = true;
                    Graphics.NavigationallyWarpMouseCursorTo(centerOf(x, y));
                    if (y < gnr - 1)
                        Graphics.NavigationallyWarpMouseCursorTo(centerOf(x, y + 1));
                    break;
                // behave like a click on the original tile, e.g. to right click dismiss an accidentally activated overworld tile popup
                case HotKeys.GlobalHotkeyTargets.LeftClick: Graphics.Win32.LeftMouseClick(); break;
                case HotKeys.GlobalHotkeyTargets.MiddleClick: Graphics.Win32.MiddleMouseClick(); break;
                case HotKeys.GlobalHotkeyTargets.RightClick: Graphics.Win32.RightMouseClick(); break;
                case HotKeys.GlobalHotkeyTargets.ScrollUp: Graphics.Win32.ScrollWheelRotateUp(); break;
                case HotKeys.GlobalHotkeyTargets.ScrollDown: Graphics.Win32.ScrollWheelRotateDown(); break;
                default: break;
            }
        });
        Action<MyKeyRoutedEventArgs> handleCursorHotKey(int x, int y) =>
            (MyKeyRoutedEventArgs ea) =>
            {
                if (!HotKeys.GlobalHotKeyProcessor.TryGetValue(ea.Key, out var target)) return;
                switch (target)
                {
                    case HotKeys.GlobalHotkeyTargets.MoveCursorRight:
                        ea.Handled = true;
                        if (x < gnc - 1)
                            Graphics.NavigationallyWarpMouseCursorTo(centerOf(x + 1, y));
                        break;
                    case HotKeys.GlobalHotkeyTargets.MoveCursorLeft:
                        ea.Handled = true;
                        if (x > 0)
                            Graphics.NavigationallyWarpMouseCursorTo(centerOf(x - 1, y));
                        break;
                    case HotKeys.GlobalHotkeyTargets.MoveCursorUp:
                        ea.Handled = true;
                        if (y > 0)
                            Graphics.NavigationallyWarpMouseCursorTo(centerOf(x, y - 1));
                        break;
                    case HotKeys.GlobalHotkeyTargets.MoveCursorDown:
                        ea.Handled = true;
                        if (y < gnr - 1)
                            Graphics.NavigationallyWarpMouseCursorTo(centerOf(x, y + 1));
                        break;
                    case HotKeys.GlobalHotkeyTargets.LeftClick: Graphics.Win32.LeftMouseClick(); break;
                    case HotKeys.GlobalHotkeyTargets.MiddleClick: Graphics.Win32.MiddleMouseClick(); break;
                    case HotKeys.GlobalHotkeyTargets.RightClick: Graphics.Win32.RightMouseClick(); break;
                    default: break;
                }
            };
        // grid of choices (every cell, including holes, is a bordered canvas so that Shift+drag can move items into empty spots too)
        var cellBorders = new Border[total];
        void populate(int n)
        {
            var c = (Canvas)cellBorders[n].Child;
            var (icon, isSel, _) = items[n];
            if (icon != null)
            {
                // the sword icons are drawn big in the (taller) top row; if one is moved to another row it shrinks to fit that row's cell
                if (icon is Image img && (img.Tag is string t && t == "sword"))
                {
                    if (firstRowExtra > 0 && n / gnc == 0)
                    {
                        img.Width = 15.0;
                        img.Height = 27.0;
                        Canvas.SetLeft(img, 3.0);
                        Canvas.SetTop(img, 0.0);
                    }
                    else
                    {
                        img.Width = 12.0;
                        img.Height = 21.0;
                        Canvas.SetLeft(img, 4.0);
                        Canvas.SetTop(img, 0.0);
                    }
                }
                c.Children.Add(icon);
                if (!isSel) // grey out
                    c.Children.Add(new Canvas { Width = c.Width, Height = c.Height, Background = Brushes.Black, Opacity = 0.6, IsHitTestVisible = false });
            }
        }
        // (WPF reads the global keyboard state (Keyboard.IsKeyDown LeftShift/RightShift); Avalonia has none, so this reads the click's own modifiers)
        bool isShiftDown(KeyModifiers modifiers) => modifiers.HasFlag(KeyModifiers.Shift);
        void saveLayout()
        {
            TrackerModelOptions.MenuLayouts[uniqueName] = items.Select(keyOf).ToArray();
            TrackerModelOptions.writeSettings();
        }
        void refreshHighlights() { foreach (var r in redrawGridFuncs) r(); }
        void swapCells(int a, int m)
        {
            var (ca, cm_) = (((Canvas)cellBorders[a].Child), ((Canvas)cellBorders[m].Child));
            ca.Children.Clear();
            cm_.Children.Clear();
            var tmp = items[a];
            items[a] = items[m];
            items[m] = tmp;
            populate(a);
            populate(m);
            // keep tracking where the tile's original state now lives
            var i = findOrig();
            if (i >= 0) origIdx = i;
            saveLayout();
        }
        for (int y0 = 0; y0 <= gnr - 1; y0++)
        {
            for (int x0 = 0; x0 <= gnc - 1; x0++)
            {
                var (x, y) = (x0, y0);   // (fresh copies per cell for the closures below; C# for-loop variables are shared between iterations)
                var n = y * gnc + x;
                var b = new Border { BorderThickness = new Thickness(ST), Background = Brushes.Black };
                gridAdd(grid, b, x, y);
                var c = new Canvas { Background = Brushes.Black, Width = (double)gcw, Height = (double)(grh + (y == 0 ? firstRowExtra : 0)) };  // ensure the canvas has a surface on which to receive mouse clicks
                b.Child = c;
                cellBorders[n] = b;
                canvasesToClear.Add(c);
                populate(n);
                b.PointerEntered += (_, _) =>
                {
                    var (ic, _, _) = items[n];
                    if (ic == null) snapBack(); else changeCurrentState(n);
                };
                void redraw()
                {
                    var (ic, isSel, _) = items[n];
                    b.BorderBrush =
                        ic == null && dragFrom != n ? Brushes.Black
                        : dragFrom == n ? Brushes.Orange
                        : n == currentState ? (isSel ? brushes.GridSelectableHighlightBrush : brushes.GridNotSelectableHighlightBrush)
                        : Brushes.Black;
                }
                redrawGridFuncs.Add(redraw);
                redraw();
                void mouseWarpDismiss()
                {
                    allDoneSoNoLongerNeedToMouseLeaveSnapback = true;
                    switch ((isJumpCapable && jumpMode ? new GridClickDismissalWarpReturn.WarpToCenter() : gridClickDismissalWarpReturn))
                    {
                        case GridClickDismissalWarpReturn.WarpTo(var pos): Graphics.WarpMouseCursorTo(pos); break;
                        case GridClickDismissalWarpReturn.WarpToCenter:
                            {
                                var pos = tileCanvas.TranslatePoint(new Point(tileCanvas.Width / 2.0, tileCanvas.Height / 2.0), cm.AppMainCanvas).Value;
                                Graphics.WarpMouseCursorTo(pos);
                                break;
                            }
                        case GridClickDismissalWarpReturn.NoWarp: break;
                    }
                    dismiss();
                }
                b.PointerPressed += (_, ea) =>
                {
                    ea.Handled = true;
                    // (Avalonia captures the pointer to the pressed element, which would send the release to this cell instead of the cell under the mouse
                    // (breaking Shift+drag) and stop other cells' PointerEntered while the button is held; WPF doesn't capture, so release the capture)
                    ea.Pointer.Capture(null);
                    var (ic, isSel, _) = items[n];
                    if (isReorderable && ea.GetCurrentPoint(b).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed && isShiftDown(ea.KeyModifiers))
                    {
                        // Shift+drag: pick up this item (if there is one); it is dropped on whatever cell the mouse is released over
                        if (ic != null)
                        {
                            dragFrom = n;
                            refreshHighlights();
                        }
                    }
                    else if (ic != null && isSel)
                    {
                        switch (onClick(ea, stateID()))
                        {
                            case PopupClickBehavior<TResult>.DismissPopupWithResult(var r): hasResult = true; result = r; mouseWarpDismiss(); break;
                            case PopupClickBehavior<TResult>.DismissPopupWithNoResult: mouseWarpDismiss(); break;
                            case PopupClickBehavior<TResult>.StayPoppedUp: break;
                        }
                    }
                };
                b.PointerReleased += (_, ea) =>
                {
                    if (dragFrom >= 0 && ea.InitialPressMouseButton == MouseButton.Left)
                    {
                        ea.Handled = true;
                        var a = dragFrom;
                        dragFrom = -1;
                        if (a != n)
                        {
                            swapCells(a, n);
                            var (ic, isSel, _) = items[n];
                            if (ic != null && isSel) changeCurrentState(n);
                        }
                        refreshHighlights();
                    }
                };
                b.MyKeyAdd(handleCursorHotKey(x, y));
            }
        }
        grid.PointerExited += (_, _) =>
        {
            if (dragFrom >= 0)
            {
                dragFrom = -1;
                refreshHighlights();
            }
            snapBack();
        };
        foreach (var (d, x, y) in extraDecorations)
            canvasAdd(popupCanvas, d, x, y);
        if (isJumpCapable && isResizable) layoutMovables();   // (puts the explainer where the moved menu is)
        // initial input
        switch (activationDelta)
        {
            case -1: prev(); break;
            case 0: break;
            case 1: next(); break;
            default: throw new Exception("bad activationDelta");
        }
        // activate the modal
        try
        {
            if (opaOpt is double opa)
                await DoModalOpa(cm, wh, tileX, tileY, popupCanvas, opa);
            else
                await DoModal(cm, wh, tileX, tileY, popupCanvas);
        }
        finally
        {
            // (always: the cached dashes and the caller's elements must be taken out of this popup)
            selfCleanup();
            allDoneSoNoLongerNeedToMouseLeaveSnapback = true;
        }
        return (hasResult, result);
    }

    ////////////////////////////////

    private static Control make_itemBoxMouseButtonExplainerDecoration()
    {
        var d = new DockPanel { Height = 90.0, LastChildFill = true, Background = Brushes.Black };
        var sp = new StackPanel { Orientation = Orientation.Vertical };
        d.Children.Add(sp);
        foreach (var (color, text) in new (IBrush, string)[] { (yes, "Have it"), (skipped, "Don't want it"), (no, "Don't have it") })
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal };
            var c = new Canvas { Width = 30.0, Height = 30.0 };
            var rect = new Rectangle { Width = 30.0, Height = 30.0, Stroke = color, StrokeThickness = 3.0, IsHitTestVisible = false };
            c.Children.Add(rect);
            if (object.Equals(color, skipped))
                Graphics.placeSkippedItemXDecoration(c);
            p.Children.Add(c);
            var tb = new TextBox { FontSize = 12.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false,
                                    Text = text, VerticalAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(0.0) };
            p.Children.Add(tb);
            sp.Children.Add(p);
        }
        var b = new Border { Background = Brushes.Black, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(3.0), Child = d };
        b.PointerPressed += (_, ea) => ea.Handled = true;  // absorb mouse clicks, so that clicking explainer decoration does not dismiss popup due to being outside-the-area click
        Control fe = b;
        return fe;
    }
    public static readonly Control itemBoxMouseButtonExplainerDecoration = make_itemBoxMouseButtonExplainerDecoration();
    public static readonly ModalGridSelectBrushes itemBoxModalGridSelectBrushes = new ModalGridSelectBrushes(Brushes.Yellow, Brushes.Yellow, new SolidColorBrush(Color.FromRgb(140, 10, 0)), Brushes.Gray);

    // the big (15x27) sword icons for the item menu's top row; null for every other item
    public static System.Drawing.Bitmap swordMenuBmpFor(int ident) =>
        ident switch
        {
            19 => (TrackerModel.ProgressiveItems ? Graphics.ow_progressive_tier_menu_bmps[1] : Graphics.ow_sword1_menu_bmp),   // (with Progressive Items, the plain swords show as P1..P4 even though they are disabled)
            13 => (TrackerModel.IsWSMSReplacedByBU.Invoke(null) ? null : TrackerModel.ProgressiveItems ? Graphics.ow_progressive_tier_menu_bmps[2] : Graphics.ow_sword2_menu_bmp),
            20 => (TrackerModel.ProgressiveItems ? Graphics.ow_progressive_tier_menu_bmps[3] : Graphics.ow_sword3_menu_bmp),
            21 => (TrackerModel.ProgressiveItems ? Graphics.ow_progressive_tier_menu_bmps[4] : Graphics.ow_sword4_menu_bmp),
            18 => Graphics.ow_progressive_tier_menu_bmps[0],
            _ => null,
        };
    public static bool allItemsSelectable = false;   // true while the level 9 entry item menu is open: that box is only a note, so every item (even a 'disabled' one) can be picked
    public static TrackerModel.ChoiceDomain itemMenuDomain = TrackerModel.allItemWithHeartShuffleChoiceDomain;   // DoLocalItemComboBox swaps this for boxes that don't use up items
    // (returns DoModalGridSelect's (hasResult, (ident, playerHas)); callerExtraDecorations: (element, x, y)s)
    public static async Task<(bool hasResult, (int, TrackerModel.PlayerHas) result)> DisplayItemComboBox(CanvasManager cm, double boxX, double boxY, int boxCellCurrent, int activationDelta, TrackerModel.PlayerHas originalPlayerHas,
                                                                                                          IEnumerable<(Control, double, double)> callerExtraDecorations, GridClickDismissalWarpReturn gridClickDismissalWarpReturn)
    {
        var innerc = new Canvas { Width = 24.0, Height = 24.0, Background = Brushes.Black };  // just has item drawn on it, not the box
        Canvas redraw(int n0)
        {
            var n = n0 == -2 ? -1 : n0;   // -2 is a blank padding cell in the menu
            innerc.Children.Clear();
            var bmp = boxCurrentBMP(n, null);
            if (bmp != null)
            {
                var image = Graphics.BMPtoImage(bmp);
                canvasAdd(innerc, image, 1.0, 1.0);
            }
            return innerc;
        }
        redraw(boxCellCurrent);
        // the grid is 5 columns wide and 6 rows tall (-1 is the empty cell)
        var gridIdents0 = new[] {
            // row 1: swords
            TrackerModel.ITEMS.WOODSWORD, TrackerModel.ITEMS.WHITESWORD, TrackerModel.ITEMS.MAGICALSWORD, TrackerModel.ITEMS.WHITESWORD4, TrackerModel.ITEMS.PROGRESSIVESWORD,
            // row 2
            TrackerModel.ITEMS.LADDER, TrackerModel.ITEMS.POWERBRACELET, TrackerModel.ITEMS.RECORDER, TrackerModel.ITEMS.RAFT, TrackerModel.ITEMS.BOW,
            // row 3
            TrackerModel.ITEMS.BLUERING, TrackerModel.ITEMS.REDRING, TrackerModel.ITEMS.KEY, TrackerModel.ITEMS.WOODARROW, TrackerModel.ITEMS.SILVERARROW,
            // row 4
            TrackerModel.ITEMS.WAND, TrackerModel.ITEMS.BLUECANDLE, TrackerModel.ITEMS.REDCANDLE, TrackerModel.ITEMS.BOOMERANG, TrackerModel.ITEMS.MAGICBOOMERANG,
            // row 5
            TrackerModel.ITEMS.BOOKSHIELD, TrackerModel.ITEMS.SHIELD, TrackerModel.ITEMS.MEAT, TrackerModel.ITEMS.OLDKEY, TrackerModel.ITEMS.BOMBUPGRADE,
            // row 6
            TrackerModel.ITEMS.BLUEPOTION, TrackerModel.ITEMS.LETTER, TrackerModel.ITEMS.HEARTCONTAINER, TrackerModel.ITEMS.TRIFORCE, -1,
            };
        // the user's custom icons (if that option is on) go in extra rows at the bottom; -2 pads out the last row with blank, unselectable cells
        var emptyCellIdx = gridIdents0.Length - 1;
        int[] make_gridIdents()
        {
            var nc = TrackerModel.ITEMS.customMenuCount;
            // a box that already holds a custom icon the menu no longer offers (option off / folder changed) still shows that icon as its current cell
            var stray = boxCellCurrent >= TrackerModel.ITEMS.CUSTOM_BASE && boxCellCurrent < TrackerModel.ITEMS.CUSTOM_BASE + TrackerModel.ITEMS.CUSTOM_COUNT && boxCellCurrent - TrackerModel.ITEMS.CUSTOM_BASE >= nc ? new[] { boxCellCurrent } : new int[0];
            var offered = Enumerable.Range(0, nc).Select(k => TrackerModel.ITEMS.CUSTOM_BASE + k).Concat(stray).ToArray();
            if (offered.Length == 0) return gridIdents0;
            else
            {
                var padded = ((offered.Length + 4) / 5) * 5;
                return gridIdents0.Concat(Enumerable.Range(0, padded).Select(k => (k < offered.Length ? offered[k] : -2))).ToArray();
            }
        }
        var gridIdents = make_gridIdents();
        var gridRows = gridIdents.Length / 5;
        var gridElementsSelectablesAndIDs = new List<(Control, bool, int)>();
        foreach (var ident in gridIdents)
        {
            Control fe;
            if (ident == -1 || ident == -2) fe = new Canvas();
            else
            {
                var sm = swordMenuBmpFor(ident);
                if (sm != null)
                {
                    var img = Graphics.BMPtoImage(sm);
                    img.Stretch = Stretch.Fill;
                    img.Tag = "sword";
                    RenderOptions.SetBitmapInterpolationMode(img, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
                    fe = img;
                }
                else
                {
                    // Progressive Items: the blue ring / candle and wood arrow cells show the current tier (the red ones are disabled and keep their own picture)
                    var f = TrackerModel.progressiveFamily(ident);
                    var shownIdent = f >= 2 && !(TrackerModel.progressiveDisabledIdent(ident)) ? TrackerModel.progressiveFamilyIdent(f, TrackerModel.progressiveFamilyTier(f)) : ident;
                    fe = Graphics.BMPtoImage(boxCurrentBMP(shownIdent, null));
                }
            }
            // Progressive Items: the plain swords are disabled (the progressive sword does their job)
            var progDisabled = !allItemsSelectable && ident != boxCellCurrent && TrackerModel.progressiveDisabledIdent(ident);
            if (progDisabled)
            {
                ToolTip.SetTip(fe, "Disabled Due to Progressive Items");
                ToolTip.SetShowOnDisabled(fe, true);
            }
            var isSelectable = (allItemsSelectable && ident != -2) || !progDisabled && (ident == -1 || (ident != -2 && ident == boxCellCurrent) || (ident != -2 && (itemMenuDomain.CanAddUse(ident) && !(ident == TrackerModel.ITEMS.WHITESWORD4 && !TrackerModelOptions.FourthSwordEnabled))));   // the 4th sword is disabled unless the 4th Sword start option is on
            gridElementsSelectablesAndIDs.Add((fe, isSelectable, ident));
        }
        var originalStateIndex0 = Array.FindIndex(gridIdents, ident => ident == boxCellCurrent);
        var originalStateIndex = originalStateIndex0 >= 0 ? originalStateIndex0 : emptyCellIdx;
        PopupClickBehavior<(int, TrackerModel.PlayerHas)> onClick(PointerPressedEventArgs ea, int ident)
        {
            // we're getting a click with mouse event args ea on one of the selectable items in the grid, namely ident. take appropriate action.
            var newPH = MouseButtonEventArgsToPlayerHas(ea);
            return new PopupClickBehavior<(int, TrackerModel.PlayerHas)>.DismissPopupWithResult((ident,
                ident == -1
                    ? (originalPlayerHas == TrackerModel.PlayerHas.NO && newPH == TrackerModel.PlayerHas.SKIPPED
                        ? TrackerModel.PlayerHas.SKIPPED   // middle click an empty box toggles it to white
                        : TrackerModel.PlayerHas.NO)
                    : newPH));
        }
        var decorationsShouldGoToTheLeft = boxX > Graphics.OMTW * 8.0;
        // the extra row goes above the original grid, so the original items stay where they always were; if that would run off the top of the window, shift the whole grid down instead
        var extraRowHeight = 60.0;   // two extra rows of 21 (cell) + 2*3 (border) above the original 4-row grid, plus 6 for the taller top (sword) row
        var wantedGridY = -3.0 - extraRowHeight;   // the grid is 2 rows taller than the original 4-row grid, so it starts 2 rows higher
        var gridY = Math.Max(wantedGridY, -(boxY + 3.0));
        var gridX = decorationsShouldGoToTheLeft ? -179.0 : 27.0;   // (the item menu is drawn 1.25x bigger, so the left-hand version starts further left to keep its right edge in place)
        var menuExtraH = 44.0;   // the 1.25x bigger menu is about 44px taller, so the things below it move down
        var customExtraH = (double)(gridRows - 6) * 34.0;   // each custom-icon row makes the menu taller, so the things below it move down
        var (_decoX, _decoY) = decorationsShouldGoToTheLeft ? (-179.0, 108.0 + menuExtraH + customExtraH + (gridY - wantedGridY)) : (27.0, 108.0 + menuExtraH + customExtraH + (gridY - wantedGridY));
        void redrawTile(int ident0)
        {
            // the user has changed the current selection via mousing or scrolling, redraw the preview tile appropriately to display ident
            var ident = ident0 == -2 ? -1 : ident0;   // -2 is a blank padding cell in the menu
            var innerc = redraw(ident);
            var s0 = ident == -1 ? "Unmarked" : TrackerModel.ITEMS.AsDisplayDescription(ident);
            var s = HotKeys.ItemHotKeyProcessor.AppendHotKeyToDescription(s0, ident);
            var text = new TextBox { Text = s, Foreground = Brushes.Orange, Background = Brushes.Black, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0),
                                        FontSize = 16.0, HorizontalContentAlignment = HorizontalAlignment.Center };
            var textBorder = new Border { BorderThickness = new Thickness(3.0), Child = text, Background = Brushes.Black, BorderBrush = Brushes.Gray };
            var dp = new DockPanel { LastChildFill = false };
            dp.Children.Add(textBorder);
            innerc.Children.Add(dp);
            if (decorationsShouldGoToTheLeft)
            {
                DockPanel.SetDock(textBorder, Dock.Right);
                Canvas.SetTop(dp, -3.0);
                Canvas.SetRight(dp, 165.0);
            }
            else
            {
                DockPanel.SetDock(textBorder, Dock.Right);
                Canvas.SetTop(dp, -3.0);
                Canvas.SetLeft(dp, 165.0);
            }
        }
        var extraDecorations = callerExtraDecorations.ToList();   // (the 'Have it / Don't want it / Don't have it' explainer box was removed)
        return await DoModalGridSelect<int, (int, TrackerModel.PlayerHas)>(cm, boxX + 3.0, boxY + 3.0, innerc, gridElementsSelectablesAndIDs.ToArray(), originalStateIndex, activationDelta, (5, gridRows, 21, 21),
                                    17.0, 12.0, gridX, gridY, redrawTile, onClick, extraDecorations, itemBoxModalGridSelectBrushes, gridClickDismissalWarpReturn, null, "ItemBox", null);
    }

    // version buttons that are currently glowing (start screen and main screen each have one); cleared once the user opens the patch notes
    public static readonly List<Action> versionGlowClearers = new List<Action>();
    // ---- update check (GitHub releases) ----
    public static readonly string CurrentReleaseLabel = "2.2.0-beta-19";   // bump for each release; it must match the tag you publish on GitHub (without the leading 'v')
    // (F#: returns (int list, (int * string * int * string)) and the callers compare those with F#'s structural `>`; compareTagKeys below
    // does the same comparison: the number list element by element (a shorter list that is a prefix sorts first), then the tuple field
    // by field, with strings compared ordinally as F# does)
    private static (List<int> nums, (int, string, int, string) preKey) parseTagForCompare(string tag)
    {
        var t = tag.Trim().TrimStart('v', 'V');
        var dash = t.IndexOf('-');
        var (main, pre) = dash == -1 ? (t, "") : (t.Substring(0, dash), t.Substring(dash + 1));
        var nums = main.Split('.').Select(p => int.TryParse(p, out var v) ? v : 0).ToList();
        nums.AddRange(Enumerable.Repeat(0, Math.Max(0, 4 - nums.Count)));
        (int, string, int, string) preKey;
        if (pre == "") preKey = (1, "", 0, "");   // a final release sorts after any beta/rc of the same number
        else
        {
            var letters = new string(pre.TakeWhile(c => !(char.IsDigit(c))).ToArray());
            var digits = pre.Substring(letters.Length);
            // (digits, then any suffix such as '-hotfix1': a suffixed tag sorts after the plain one, so beta13-hotfix1 is newer than beta13)
            var numText = new string(digits.TakeWhile(char.IsDigit).ToArray());
            preKey = (0, letters.ToLowerInvariant(), (int.TryParse(numText, out var v) ? v : 0), digits.Substring(numText.Length).ToLowerInvariant());
        }
        return (nums, preKey);
    }
    private static int compareTagKeys((List<int> nums, (int, string, int, string) preKey) a, (List<int> nums, (int, string, int, string) preKey) b)
    {
        for (int i = 0; i < Math.Min(a.nums.Count, b.nums.Count); i++)
        {
            var c = a.nums[i].CompareTo(b.nums[i]);
            if (c != 0) return c;
        }
        if (a.nums.Count != b.nums.Count) return a.nums.Count.CompareTo(b.nums.Count);
        var c1 = a.preKey.Item1.CompareTo(b.preKey.Item1);
        if (c1 != 0) return c1;
        var c2 = string.CompareOrdinal(a.preKey.Item2, b.preKey.Item2);
        if (c2 != 0) return c2;
        var c3 = a.preKey.Item3.CompareTo(b.preKey.Item3);
        if (c3 != 0) return c3;
        return string.CompareOrdinal(a.preKey.Item4, b.preKey.Item4);
    }
    public static bool isNewerRelease(string candidateTag, string currentLabel) => compareTagKeys(parseTagForCompare(candidateTag), parseTagForCompare(currentLabel)) > 0;
    public static (string, string)? updateAvailable = null;   // (tag, release page url) of a newer release, once one has been found
    public static readonly List<Action<(string, string)>> updateListeners = new List<Action<(string, string)>>();
    // (WPF: System.Net.WebClient, which is obsolete in .NET (SYSLIB0014); HttpClient does the same requests. WPF also turns on TLS 1.2 through
    // ServicePointManager, which is obsolete too and doesn't affect HttpClient: .NET already uses TLS 1.2/1.3.)
    private static System.Net.Http.HttpClient makeUpdateHttpClient()
    {
        var http = new System.Net.Http.HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "ZoraTracker-update-check");
        return http;
    }
    // blocking: call from a background thread. Returns the newest non-draft release (pre-releases included), or None if it could not be determined.
    // (null for None)
    public static (string, string)? fetchLatestRelease()
    {
        try
        {
            using var wc = makeUpdateHttpClient();
            var json = wc.GetStringAsync("https://api.github.com/repos/TheGobOne/ZoraTracker/releases?per_page=10").GetAwaiter().GetResult();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            (string, string)? best = null;
            foreach (var r in doc.RootElement.EnumerateArray())
            {
                var ok = r.TryGetProperty("draft", out var d);
                var isDraft = ok && d.GetBoolean();
                if (!isDraft)
                {
                    var tag = r.GetProperty("tag_name").GetString();
                    var url = r.GetProperty("html_url").GetString();
                    if (best.HasValue && !(isNewerRelease(tag, best.Value.Item1))) { }
                    else best = (tag, url);
                }
            }
            return best;
        }
        catch { return null; }
    }
    // whenDone(latest, newer) runs on the UI thread: latest = newest release found (None if the check failed), newer = Some if it is newer than this build
    public static void checkForUpdatesAsync(Action<(string, string)?, (string, string)?> whenDone)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            var latest = fetchLatestRelease();
            (string, string)? newer = latest.HasValue && isNewerRelease(latest.Value.Item1, CurrentReleaseLabel) ? latest : null;
            Dispatcher.UIThread.Post(() => whenDone(latest, newer));
        });
    }
    private static bool updateCheckStarted = false;
    public static void startupUpdateCheck()
    {
        if (!updateCheckStarted && TrackerModelOptions.CheckForUpdates.Value)
        {
            updateCheckStarted = true;
            checkForUpdatesAsync((_, newer) =>
            {
                if (newer.HasValue)
                {
                    var n = newer.Value;
                    updateAvailable = n;
                    foreach (var l in updateListeners) l(n);
                }
            });
        }
    }

    // ---- in-app update install ----
    // the release's .zip asset url (only ever a download from this project's own GitHub releases), or None
    // (null for None)
    public static string fetchReleaseZipUrl(string tag)
    {
        try
        {
            using var wc = makeUpdateHttpClient();
            var json = wc.GetStringAsync($"https://api.github.com/repos/TheGobOne/ZoraTracker/releases/tags/{Uri.EscapeDataString(tag)}").GetAwaiter().GetResult();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            string found = null;
            foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = a.GetProperty("name").GetString();
                var url = a.GetProperty("browser_download_url").GetString();
                if (found == null && name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                   && name.IndexOf("CrossPlatform", StringComparison.OrdinalIgnoreCase) >= 0   // (the port's zips have CrossPlatform in the name; the WPF build's zip does not)
                   && url != null && url.StartsWith("https://github.com/TheGobOne/ZoraTracker/releases/download/"))
                    found = url;
            }
            return found;
        }
        catch { return null; }
    }
    // downloads the release zip, then hands over to a small PowerShell script that waits for this app to exit, unpacks the zip over the app folder (leaving the user's
    // HotKeys.txt, settings file and custom images alone) and starts the new version. Returns an error message on failure, or None once the script is running (the caller then closes the app).
    // (null for None. Windows only: it replaces the app's files with the release's Windows build, so on other systems it refuses and changes nothing;
    // the version button opens the release page there instead. See PROGRESS.md, Stubs.)
    // the release's .zip is the WPF Windows build; unpacking it over this app's folder swaps in the wrong FSharp.Core and Z1R_Tracker.dll and the app no longer starts.
    // So the in-app install stays off until the releases carry a cross-platform zip; the version button opens the release page instead.
    public static readonly bool inAppInstallIsSafe = false;
    public static string downloadAndStageUpdate(string zipUrl, Action<int> onProgress)
    {
        if (!Platform.isWindows || !inAppInstallIsSafe)
            return "the automatic update install only exists for the Windows (WPF) build";
        try
        {
            var work = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ZoraTrackerUpdate");
            if (System.IO.Directory.Exists(work)) System.IO.Directory.Delete(work, true);
            System.IO.Directory.CreateDirectory(work);
            var zipPath = System.IO.Path.Combine(work, "update.zip");
            using (var wc = makeUpdateHttpClient())
            {
                // (WPF: WebClient.DownloadFileTaskAsync with DownloadProgressChanged reporting ProgressPercentage; this reports the same percentage as the bytes arrive)
                using var resp = wc.GetAsync(new Uri(zipUrl), System.Net.Http.HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
                resp.EnsureSuccessStatusCode();
                var totalBytes = resp.Content.Headers.ContentLength ?? -1L;
                using var src = resp.Content.ReadAsStream();
                using var dst = System.IO.File.Create(zipPath);
                var buffer = new byte[81920];
                long received = 0;
                int lastPct = -1;
                int read;
                while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                {
                    dst.Write(buffer, 0, read);
                    received += read;
                    var pct = totalBytes > 0 ? (int)(received * 100L / totalBytes) : 0;
                    if (pct != lastPct) { lastPct = pct; onProgress(pct); }
                }
            }
            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar);
            // (WPF: "Z1R_WPF.exe"; the port restarts its own executable, as Graphics.RestartTheApplication does)
            var exe = System.IO.Path.Combine(appDir, Graphics.ExeName);
            string q(string p) => "'" + p.Replace("'", "''") + "'";
            var stage = System.IO.Path.Combine(work, "stage");
            var log = System.IO.Path.Combine(work, "update-log.txt");
            var script =
                string.Join("\r\n", new[] {
                    "$ErrorActionPreference = 'Stop'",
                    "try {",
                    $"  Wait-Process -Id {System.Diagnostics.Process.GetCurrentProcess().Id} -ErrorAction SilentlyContinue",
                    "  Start-Sleep -Seconds 1",
                    // the install is a function so it can be run again: robocopy's result used to be ignored, so a locked file (e.g. held by an antivirus scan) left an old .dll beside the new .exe.
                    // Now every .dll / .exe is checked after the copy; if any was not replaced it tries once more, then downloads the zip again and tries a last time, and only then gives up with a message
                    $"$dst = {q(appDir)}",
                    "function Install {",
                    $"  if (Test-Path -LiteralPath {q(stage)}) {{ Remove-Item -LiteralPath {q(stage)} -Recurse -Force }}",
                    $"  Expand-Archive -LiteralPath {q(zipPath)} -DestinationPath {q(stage)} -Force",
                    $"  $src = {q(stage)}",
                    "  $d = @(Get-ChildItem -LiteralPath $src)",
                    "  if ($d.Count -eq 1 -and $d[0].PSIsContainer) { $src = $d[0].FullName }",
                    $"  robocopy $src {q(appDir)} /E /R:30 /W:1 /XF HotKeys.txt Z1R_Tracker_settings.json /XD ShowRunCustomImages | Out-Null",
                    "  $rc = $LASTEXITCODE",
                    "  $bad = @()",
                    "  if ($rc -ge 8) { $bad += ('robocopy exit code ' + $rc) }",
                    "  Get-ChildItem -LiteralPath $src -Recurse -File | Where-Object { $_.Extension -in '.dll','.exe' } | ForEach-Object {",
                    "    $rel = $_.FullName.Substring($src.Length).TrimStart('\\')",
                    "    $t = Join-Path $dst $rel",
                    "    if (-not (Test-Path -LiteralPath $t)) { $bad += $rel }",
                    "    elseif ($_.Length -ne (Get-Item -LiteralPath $t).Length) { $bad += $rel }",
                    "    elseif ($_.Length -lt 20000000 -and (Get-FileHash -LiteralPath $_.FullName).Hash -ne (Get-FileHash -LiteralPath $t).Hash) { $bad += $rel }",
                    "  }",
                    "  return ,$bad",
                    "}",
                    "$bad = Install",
                    "if ($bad.Count -gt 0) { Start-Sleep -Seconds 5; $bad = Install }",
                    "if ($bad.Count -gt 0) {",
                    "  try {",
                    "    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12",
                    $"    Invoke-WebRequest -UseBasicParsing -Uri {q(zipUrl)} -OutFile {q(zipPath)}",
                    "  } catch { }",
                    "  $bad = Install",
                    "}",
                    "if ($bad.Count -gt 0) {",
                    $"  $msg = 'The update could not be fully installed, even after trying again and downloading it again. These files could not be replaced: ' + ($bad -join ', ') + '. Close ZORA Tracker, any other program using its folder, and your antivirus scan, then extract the downloaded zip over this folder by hand: ' + {q(zipPath)}",
                    $"  $msg | Out-File -LiteralPath {q(log)}",
                    "  Add-Type -AssemblyName PresentationFramework",
                    "  [System.Windows.MessageBox]::Show($msg, 'ZORA Tracker update', 'OK', 'Warning') | Out-Null",
                    "} else {",
                    $"  Start-Process -FilePath {q(exe)} -WorkingDirectory {q(appDir)}",
                    "}",
                    $"}} catch {{ $_ | Out-File -LiteralPath {q(log)} }}",
                });
            var scriptPath = System.IO.Path.Combine(work, "update.ps1");
            System.IO.File.WriteAllText(scriptPath, script);
            var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            System.Diagnostics.Process.Start(psi);
            return null;
        }
        catch (Exception e) { return e.Message; }
    }

    public static Button makeVersionButtonWithBehavior(CanvasManager cm)
    {
        // shows the current release (e.g. "v2.2-b3" for 2.2.0-beta3), so it only needs updating via CurrentReleaseLabel
        var shortLabel = CurrentReleaseLabel.Replace(".0-", "-").Replace("-beta", "-b");
        var vb = Graphics.makeButton($"v{shortLabel}", 12.0, Brushes.Orange);
        // a green glow plus tooltip when a newer release is available on GitHub
        void showUpdateGlow((string, string) n)
        {
            var (tag, _url) = n;
            ToolTip.SetTip(vb, $"Update available: {tag} (click for details)");
            vb.Effect = new DropShadowDirectionEffect { ShadowDepth = 0.0, BlurRadius = 16.0, Opacity = 1.0, Color = Colors.Lime };
        }
        updateListeners.Add(showUpdateGlow);
        if (updateAvailable.HasValue)
            showUpdateGlow(updateAvailable.Value);
        startupUpdateCheck();
        vb.Click += (_, _) =>
        {
            if (TrackerModelOptions.LastSeenVersionNotes != CurrentReleaseLabel)
            {
                TrackerModelOptions.LastSeenVersionNotes = CurrentReleaseLabel;
                TrackerModelOptions.writeSettings();
                foreach (var f in versionGlowClearers) f();
                versionGlowClearers.Clear();
            }
            if (!GlobalFlag.popupIsActive)
            {
                GlobalFlag.popupIsActive = true;
                async Task body()
                {
                    var (bodyText, buttons) =
                        updateAvailable.HasValue
                            ? ($"{OverworldData.AboutBody}\n*** A newer version is available: {updateAvailable.Value.Item1} (you have v{CurrentReleaseLabel}) ***\n", new[] { "Install update", "Open folder", "Go to website", "Ok" })
                            : (OverworldData.AboutBody, new[] { "Check for updates", "Open folder", "Go to website", "Ok" });
                    var r = await DoModalMessageBox(cm, null, bodyText, buttons);
                    GlobalFlag.popupIsActive = false;
                    if (r == "Go to website")
                        Platform.OpenWithShell(OverworldData.Website);
                    else if (r == "Open folder")
                    {
                        // open the folder the tracker is running from (where HotKeys.txt, the settings and the crash log live)
                        // (those files are in Platform.UserDataDir(): the exe folder on Windows, as in WPF; the user-data folder on macOS/Linux)
                        try { Platform.OpenWithShell(Platform.UserDataDir()); } catch { }
                    }
                    else if (r == "Install update")
                    {
                        if (updateAvailable.HasValue)
                        {
                            var (tag, url) = updateAvailable.Value;
                            if (!Platform.isWindows || !inAppInstallIsSafe)
                            {
                                // (the in-app install replaces the Windows build's files; on macOS/Linux there's nothing to install yet, so open the release page,
                                // as WPF does when the install fails. Risk 11 in PLAN.md; listed under Stubs in PROGRESS.md.)
                                Platform.stub("in-app update install (macOS/Linux: opens the release page instead)");
                                Platform.OpenWithShell(url != null && url.StartsWith(OverworldData.Website) ? url : OverworldData.Website);
                                return;
                            }
                            GlobalFlag.popupIsActive = true;
                            var ok = await DoModalMessageBoxCore(cm, System.Drawing.SystemIcons.Question,
                                                            $"Download {tag} and install it over this copy?\n\nThis OVERWRITES your current version of ZORA Tracker with the new one.\nIf you want to keep your current version and have a separate install,\ndownload the release from GitHub instead (Go to website).\n\nThe tracker will close and reopen on the new version.\nYour hotkeys, settings and custom images will NOT be touched.\nAny game in progress is discarded (save it first).",
                                                            new[] { "Install and restart", "Cancel" }, 100.0, 300.0);
                            if (ok == "Install and restart")
                            {
                                // a small progress popup while the zip downloads
                                var progressWh = new ManualResetEvent(false);
                                var bar = new ProgressBar { Width = 320.0, Height = 22.0, Minimum = 0.0, Maximum = 100.0, Value = 0.0, Foreground = Brushes.Orange, Background = Brushes.Black, BorderBrush = Brushes.Orange };
                                var label = new TextBlock { Text = "Downloading update...  0%", Foreground = Brushes.Orange, FontSize = 18.0, Margin = new Thickness(0.0, 0.0, 0.0, 8.0) };
                                var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(14.0) };
                                panel.Children.Add(label);
                                panel.Children.Add(bar);
                                var progressBorder = new Border { Child = panel, BorderBrush = Brushes.Orange, BorderThickness = new Thickness(3.0), Background = Brushes.Black };
                                progressBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                                var pcx = Math.Max(0.0, (cm.AppMainCanvas.Width - progressBorder.DesiredSize.Width) / 2.0);
                                var pcy = Math.Max(0.0, (cm.AppMainCanvas.Height - progressBorder.DesiredSize.Height) / 2.0);
                                progressBorder.SizeChanged += (_, _) =>
                                {
                                    Canvas.SetLeft(progressBorder, Math.Max(0.0, (cm.AppMainCanvas.Width - progressBorder.Bounds.Width) / 2.0));
                                    Canvas.SetTop(progressBorder, Math.Max(0.0, (cm.AppMainCanvas.Height - progressBorder.Bounds.Height) / 2.0));
                                };
                                StartImmediate(DoModal(cm, progressWh, pcx, pcy, progressBorder));
                                void onProgress(int pct)
                                {
                                    Dispatcher.UIThread.Post(() =>
                                    {
                                        bar.Value = (double)pct;
                                        label.Text = $"Downloading update...  {pct}%";
                                    });
                                }
                                // (WPF: Async.SwitchToThreadPool() ... Async.SwitchToContext ctx)
                                var result = await Task.Run(() =>
                                {
                                    var zip = fetchReleaseZipUrl(tag);
                                    return zip != null ? downloadAndStageUpdate(zip, onProgress) : "no downloadable zip was found on that release";
                                });
                                progressWh.Set();
                                if (result == null)
                                {
                                    // (the update script waits for this to exit, then copies the files and relaunches)
                                    if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d && d.MainWindow != null)
                                        d.MainWindow.Close();
                                }
                                else
                                {
                                    var err = result;
                                    await DoModalMessageBox(cm, System.Drawing.SystemIcons.Warning, $"The update could not be installed automatically:\n{err}\n\nThe release page will be opened instead.", new[] { "Ok" });
                                    Platform.OpenWithShell(url != null && url.StartsWith(OverworldData.Website) ? url : OverworldData.Website);
                                }
                            }
                            GlobalFlag.popupIsActive = false;
                        }
                    }
                    else if (r == "Check for updates")
                    {
                        checkForUpdatesAsync((latest, newer) =>
                        {
                            string msg; System.Drawing.Icon _icon;
                            if (newer.HasValue)
                            {
                                var (tag, _) = newer.Value;
                                updateAvailable = newer;
                                foreach (var l in updateListeners) l(newer.Value);
                                (msg, _icon) = ($"A newer version is available: {tag}\n(you have v{CurrentReleaseLabel})\nClick the version button again to download it.", System.Drawing.SystemIcons.Information);
                            }
                            else if (latest.HasValue)
                                (msg, _icon) = ($"You are up to date (v{CurrentReleaseLabel}).", System.Drawing.SystemIcons.Information);
                            else
                                (msg, _icon) = ("Could not check for updates right now.\n(Are you offline?)", System.Drawing.SystemIcons.Warning);
                            if (!GlobalFlag.popupIsActive)
                            {
                                GlobalFlag.popupIsActive = true;
                                async Task showIt()
                                {
                                    await DoModalMessageBox(cm, null, msg, new[] { "Ok" });
                                    GlobalFlag.popupIsActive = false;
                                }
                                StartImmediate(showIt());
                            }
                        });
                    }
                }
                StartImmediate(body());
            }
        };
        return vb;
    }
}
