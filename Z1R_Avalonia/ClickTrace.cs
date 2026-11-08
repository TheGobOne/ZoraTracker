// A trace of what the user does, written to Z1R_Tracker_trace_log.txt (next to the crash log) from the start of every run, so a report like
// "the screen went gray after these clicks" comes with the clicks, key presses, popup open/close steps and exceptions that led to it.
// The file starts fresh on each launch and stops growing at about 3 MB. Nothing is sent anywhere. Port only (WPF is frozen to bug fixes).
using System;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Z1R_Avalonia;

public static class ClickTrace
{
    private const long MaxBytes = 3L * 1024L * 1024L;
    private static readonly object gate = new object();
    private static string path;
    private static long written;
    private static bool disabled;
    private static bool inputHooked;
    private static readonly DateTime started = DateTime.Now;

    private static void ensureOpen()
    {
        if (path != null || disabled) return;
        try
        {
            path = Platform.Files.User("Z1R_Tracker_trace_log.txt");
            var header = $"TRACE -- started {started:yyyy-MM-dd HH:mm:ss} -- v{CustomComboBoxes.CurrentReleaseLabel} -- {Environment.OSVersion}\n";
            System.IO.File.WriteAllText(path, header);
            written = header.Length;
        }
        catch { disabled = true; }
    }

    public static void Log(string msg)
    {
        lock (gate)
        {
            ensureOpen();
            if (disabled || path == null) return;
            if (written > MaxBytes)
            {
                if (written != long.MaxValue) { try { System.IO.File.AppendAllText(path, "(trace stopped: size limit)\n"); } catch { } written = long.MaxValue; }
                return;
            }
            try
            {
                var line = $"{(DateTime.Now - started).TotalSeconds,9:F3}s  {msg}\n";
                System.IO.File.AppendAllText(path, line);
                written += line.Length;
            }
            catch { disabled = true; }
        }
    }

    // the few methods above the call that matter: who asked (e.g. "PieMenus.showShopMenu < OverworldMapTileCustomization.onClick")
    public static string Caller(int skip = 2, int count = 3)
    {
        try
        {
            var frames = new StackTrace(skip, false).GetFrames() ?? new StackFrame[0];
            var names = frames
                .Select(f => f.GetMethod())
                .Where(m => m != null && m.DeclaringType != null && !m.DeclaringType.FullName.StartsWith("System.") && !m.DeclaringType.FullName.StartsWith("Avalonia.")
                            && m.DeclaringType != typeof(ClickTrace))
                .Select(m => (m.DeclaringType.DeclaringType?.Name is string o ? o + "." : "") + m.DeclaringType.Name + "." + m.Name)
                .Take(count);
            return string.Join(" < ", names);
        }
        catch { return "?"; }
    }

    // what is under the pointer: the clicked control and a few of its parents
    private static string Describe(object source)
    {
        try
        {
            var parts = new System.Collections.Generic.List<string>();
            var v = source as Avalonia.Visual;
            for (int i = 0; v != null && i < 4; i++, v = v.GetVisualParent())
            {
                var n = (v as Control)?.Name;
                parts.Add(string.IsNullOrEmpty(n) ? v.GetType().Name : $"{v.GetType().Name}#{n}");
            }
            return string.Join(" < ", parts);
        }
        catch { return "?"; }
    }

    private static string Box(Visual v)
    {
        var c = v as Control;
        var b = c?.Bounds ?? default;
        return $"{v.GetType().Name} {b.Width:F0}x{b.Height:F0} at ({b.X:F0},{b.Y:F0}) hit={(c?.IsHitTestVisible)} visible={(c?.IsVisible)} z={v.ZIndex} opacity={v.Opacity:F2}";
    }

    // when a click lands while a popup layer is open but NOT on that popup layer (a gray screen that ignores clicks): write what is stacked in the window
    private static int bypassDumps;
    private static void DumpIfBypassed(Control root, PointerPressedEventArgs e)
    {
        try
        {
            var cm = CustomComboBoxes.CanvasManager.TheOnlyCanvasManager;
            if (cm == null || cm.PopupCanvasStack.Count == 0) return;
            var top = cm.PopupCanvasStack.Peek();
            var inside = false;
            for (var v = e.Source as Avalonia.Visual; v != null; v = v.GetVisualParent())
                if (ReferenceEquals(v, top)) { inside = true; break; }
            if (inside || bypassDumps >= 3) return;
            bypassDumps++;
            Log($"CLICK BYPASSED THE POPUP LAYER (dump {bypassDumps} of 3). Window layers, bottom to top:");
            var i = 0;
            foreach (var ch in cm.RootCanvas.Children)
            {
                Log($"   layer {i++}: {Box(ch)}, children={(ch as Panel)?.Children.Count}{(ReferenceEquals(ch, top) ? "   <- top popup layer" : "")}");
            }
            Log($"   top popup layer content: " + string.Join(" | ", top.Children.Take(6).Select(Box)));
            Log($"   top popup layer: Background={(top.Background == null ? "null" : top.Background.ToString())} effectivelyVisible={top.IsEffectivelyVisible} enabled={top.IsEnabled} parentIsRoot={ReferenceEquals(top.GetVisualParent(), cm.RootCanvas)}");
            var localPt = e.GetPosition(top);
            var topHit = top.InputHitTest(localPt);
            Log($"   hit test on the popup layer itself at ({localPt.X:F0},{localPt.Y:F0}): {(topHit == null ? "nothing" : Describe(topHit))}");
            var rootHit = cm.RootCanvas.InputHitTest(e.GetPosition(cm.RootCanvas));
            Log($"   hit test on the window root: {(rootHit == null ? "nothing" : Describe(rootHit))}");
            var layer1Kids = top.Children.OfType<Control>().Select(ch => (ch.GetType().Name, ch.Bounds, ch.IsHitTestVisible)).ToList();
            Log($"   popup layer children: " + string.Join(" | ", layer1Kids.Select(k => $"{k.Item1} {k.Bounds.Width:F0}x{k.Bounds.Height:F0} hit={k.IsHitTestVisible}")));
            var hit = root.InputHitTest(e.GetPosition(root));
            Log($"   what is under the pointer: {(hit == null ? "nothing" : Describe(hit))}");
            // every ancestor of the window's root canvas, with how many siblings it has (another full-window layer would show here)
            for (var v = (Avalonia.Visual)cm.RootCanvas; v != null && v.GetVisualParent() != null; v = v.GetVisualParent())
            {
                var p = v.GetVisualParent();
                Log($"   {v.GetType().Name} is inside {p.GetType().Name} ({p.GetVisualChildren().Count()} child(ren))");
            }
        }
        catch (Exception ex) { Log("(dump failed: " + ex.Message + ")"); }
    }

    // hooks the main canvas for clicks and every window for key presses; call once, when the popup layers are set up
    public static void HookInput(Control root)
    {
        ensureOpen();
        root.AddHandler(InputElement.PointerPressedEvent, new EventHandler<PointerPressedEventArgs>((_, e) =>
        {
            try
            {
                var pt = e.GetCurrentPoint(root);
                Log($"CLICK {pt.Properties.PointerUpdateKind} at ({pt.Position.X:F0},{pt.Position.Y:F0}) on {Describe(e.Source)}   [popupActive={CustomComboBoxes.GlobalFlag.popupIsActive} layers={CustomComboBoxes.CanvasManager.TheOnlyCanvasManager?.PopupCanvasStack.Count}]");
                DumpIfBypassed(root, e);
            }
            catch { }
        }), RoutingStrategies.Tunnel, handledEventsToo: true);
        if (!inputHooked)
        {
            inputHooked = true;
            InputElement.KeyDownEvent.AddClassHandler<Window>((w, e) =>
            {
                try { Log($"KEY {e.KeyModifiers}+{e.Key} in {w.GetType().Name}   [popupActive={CustomComboBoxes.GlobalFlag.popupIsActive} layers={CustomComboBoxes.CanvasManager.TheOnlyCanvasManager?.PopupCanvasStack.Count}]"); } catch { }
            }, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }
}
