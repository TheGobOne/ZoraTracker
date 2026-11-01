// Pieces only the browser (WebAssembly) build compiles; it defines BROWSER (see Z1R_Avalonia_Browser). Not part of the WPF mirror.
// The browser build is a limited version of the tracker (PLAN.md, phase 6): a web page has no windows, so the main window's content is shown
// in the page, and what browsers can't do (global hotkeys, cursor warping, fake input, co-op hosting, gamepad, voice commands) is off.
#if BROWSER
// Every `Window` in the tracker's code is the stand-in below in this build: the main window shows in the page, and other windows (pop-outs, HUDs,
// the broadcast window, message boxes) are created but never shown. Code that names Avalonia.Controls.Window in full still gets the real one.
global using Window = Z1R_Avalonia.BrowserWindow;
using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace Z1R_Avalonia;

/// <summary>Stands in for Avalonia's Window in the browser build, where windows can't be created: Program.MyWindowBase derives from this instead,
/// so MyWindow's code compiles and runs unchanged, and its content is shown in the page (ZoraTrackerApp). Window members the tracker uses are kept as
/// plain values, or mapped to the page (DesktopScaling, Screens); the window-only ones (position, state, resizing) do nothing.</summary>
public class BrowserWindow : UserControl
{
    public string Title { get; set; }
    public WindowState WindowState { get; set; }
    public WindowStartupLocation WindowStartupLocation { get; set; }
    public SizeToContent SizeToContent { get; set; }
    public bool CanResize { get; set; }
    public bool Topmost { get; set; }
    public PixelPoint Position { get; set; }
#pragma warning disable CS0067   // (a page never moves)
    public event EventHandler<PixelPointEventArgs> PositionChanged;
#pragma warning restore CS0067
    public event EventHandler Closed;
    public event EventHandler Opened;
    public double DesktopScaling => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
    public Screens Screens => TopLevel.GetTopLevel(this)?.Screens;

    private bool opened;
    public BrowserWindow()
    {
        // a window "opens" when it's first shown; here, when the page shows it
        AttachedToVisualTree += (_, _) =>
        {
            if (!opened)
            {
                opened = true;
                OnOpened(EventArgs.Empty);
            }
        };
    }
    protected virtual void OnOpened(EventArgs e) => Opened?.Invoke(this, e);
    protected virtual void OnClosed(EventArgs e) => Closed?.Invoke(this, e);
    public bool ShowInTaskbar { get; set; }
    public bool ShowActivated { get; set; }
    public SystemDecorations SystemDecorations { get; set; }
    public System.Collections.Generic.IReadOnlyList<WindowTransparencyLevel> TransparencyLevelHint { get; set; }
    public Size ClientSize
    {
        get => new Size(double.IsNaN(Width) ? Bounds.Width : Width, double.IsNaN(Height) ? Bounds.Height : Height);
        set { Width = value.Width; Height = value.Height; }
    }
    public Size? FrameSize => ClientSize;
    public double RenderScaling => DesktopScaling;
    public Avalonia.Platform.Storage.IStorageProvider StorageProvider => TopLevel.GetTopLevel(this)?.StorageProvider;
    public Avalonia.Input.IFocusManager FocusManager => TopLevel.GetTopLevel(this)?.FocusManager;

    /// <summary>The stand-in window a control is in (WPF: Window.GetWindow; desktop: TopLevel.GetTopLevel(c) as Window).</summary>
    public static BrowserWindow Of(Visual v)
    {
        for (var x = v; x != null; x = x.GetVisualParent())
            if (x is BrowserWindow w) return w;
        return null;
    }

    // Only the main window is in the page. Other windows (pop-outs, HUDs, the broadcast window, dialogs) are never shown: Show does nothing,
    // and ShowDialog returns at once with no result, which the tracker treats as a dismissed dialog.
    public void Show() => reportNotShown();
    public void Show(BrowserWindow owner) => reportNotShown();
    public System.Threading.Tasks.Task ShowDialog(BrowserWindow owner) { reportNotShown(); return System.Threading.Tasks.Task.CompletedTask; }
    public System.Threading.Tasks.Task<T> ShowDialog<T>(BrowserWindow owner) { reportNotShown(); return System.Threading.Tasks.Task.FromResult(default(T)); }
    private void reportNotShown()
    {
        if (this is not Program.MyWindow)
            Platform.stub($"separate windows in the browser (\"{Title}\" isn't shown)");
    }
    public void Activate() { }
    /// <summary>Closing the page's only "window" ends the app (the desktop app quits); in a page there's nothing to quit, so it's a no-op.</summary>
    public void Close() { }
    public void Close(object dialogResult) { }
}

/// <summary>JavaScript helpers in wwwroot/main.js (sound, the system beep, opening links in a new tab).</summary>
[SupportedOSPlatform("browser")]
internal static partial class BrowserJs
{
    [JSImport("playSound", "main.js")]
    internal static partial void PlaySound(string url, double volume);
    [JSImport("beep", "main.js")]
    internal static partial void Beep();
    [JSImport("openUrl", "main.js")]
    internal static partial void OpenUrl(string url);
}
#endif
