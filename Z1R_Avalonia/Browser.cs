// Pieces only the browser (WebAssembly) build compiles; it defines BROWSER (see Z1R_Avalonia_Browser). Not part of the WPF mirror.
// The browser build is a limited version of the tracker (PLAN.md, phase 6): a web page has no windows, so the main window's content is shown
// in the page, and what browsers can't do (global hotkeys, cursor warping, fake input, co-op hosting, gamepad, voice commands) is off.
#if BROWSER
// Every `Window` in the tracker's code is the stand-in below in this build: the main window shows in the page, and other windows (pop-outs, HUDs,
// the broadcast window, message boxes) are created but never shown. Code that names Avalonia.Controls.Window in full still gets the real one.
global using Window = Z1R_Avalonia.BrowserWindow;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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

/// <summary>Keeps the tracker's files across page reloads. Files the tracker writes in a page go to an in-memory file system that's gone when the
/// page closes, so the ones the user expects back (settings, the autosave and other saves, HotKeys.txt, notes, recent loads) are copied to the
/// browser's localStorage: restored before the tracker starts (Z1R_Avalonia_Browser's Main), and saved within a second of changing, and as the page
/// is left (main.js calls Flush). Saves other than the autosave are also downloaded (Platform.OfferDownload), since localStorage belongs to one
/// browser and is small (about 5 MB).</summary>
[SupportedOSPlatform("browser")]
public static partial class BrowserStorage
{
    private const string keyPrefix = "zt:";
    private const string timePrefix = "zt-time:";   // (when each file was last written, since restoring a file makes it look new)
    // each kept file's size and time when it was last saved, by path relative to the user folder
    private static readonly Dictionary<string, (long, DateTime)> saved = new();
    // when each kept file was written, from earlier visits too (UTC)
    private static readonly Dictionary<string, DateTime> writtenAt = new();
    private static bool reportedFull = false;

    private static string root => Platform.UserDataDir();
    private static IEnumerable<string> keptFiles()
    {
        // (the crash log grows without limit, the screenshot and tile caches are big, and the blank hotkeys template is rewritten at each start, so they aren't kept)
        var top = Directory.EnumerateFiles(root).Where(f => (f.EndsWith(".json") || f.EndsWith(".txt")) && !f.EndsWith("crash_log.txt") && !f.EndsWith("BlankSampleTemplate.txt"));
        var saves = Directory.Exists(SaveAndLoad.SavesDirectory) ? Directory.EnumerateFiles(SaveAndLoad.SavesDirectory, "*.json") : Enumerable.Empty<string>();
        return top.Concat(saves);
    }
    private static (long, DateTime) stamp(string path) { var fi = new FileInfo(path); return (fi.Length, fi.LastWriteTimeUtc); }

    /// <summary>Puts the kept files back in the page's file system; call it before the tracker reads its settings.</summary>
    public static void Restore()
    {
        foreach (var key in BrowserJs.StorageKeys(keyPrefix))
        {
            var rel = key.Substring(keyPrefix.Length);
            var path = Path.Combine(root, rel);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, BrowserJs.StorageGet(key));
                saved[rel] = stamp(path);
                if (long.TryParse(BrowserJs.StorageGet(timePrefix + rel), out var ticks))
                    writtenAt[rel] = new DateTime(ticks, DateTimeKind.Utc);
            }
            catch (Exception e) { Console.WriteLine($"restoring {rel} failed: {e.Message}"); }
        }
    }

    /// <summary>Starts saving changed files every second.</summary>
    public static void StartSaving()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Flush();
        timer.Start();
    }

    /// <summary>Saves the kept files that changed since the last call, and forgets deleted ones.</summary>
    [JSExport]
    public static void Flush()
    {
        try
        {
            var seen = new HashSet<string>();
            foreach (var path in keptFiles())
            {
                var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
                seen.Add(rel);
                var st = stamp(path);
                if (saved.TryGetValue(rel, out var old) && old == st) continue;
                if (BrowserJs.StorageSet(keyPrefix + rel, File.ReadAllText(path)))
                {
                    saved[rel] = st;
                    writtenAt[rel] = st.Item2;
                    BrowserJs.StorageSet(timePrefix + rel, st.Item2.Ticks.ToString());
                }
                else if (!reportedFull)
                {
                    reportedFull = true;
                    Console.WriteLine($"the browser's storage is full: {rel} and later changes may not be kept");
                }
            }
            foreach (var rel in saved.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                BrowserJs.StorageRemove(keyPrefix + rel);
                BrowserJs.StorageRemove(timePrefix + rel);
                saved.Remove(rel);
                writtenAt.Remove(rel);
            }
        }
        catch (Exception e) { Console.WriteLine($"saving to browser storage failed: {e.Message}"); }
    }

    /// <summary>When a kept file was last written (UTC), counting earlier visits; null if it isn't kept.</summary>
    public static DateTime? WrittenAt(string path) =>
        writtenAt.TryGetValue(Path.GetRelativePath(root, path).Replace('\\', '/'), out var t) ? t : null;

    /// <summary>Copies a file the user picked (Load, or a custom map image) into the page's file system and returns its path there. Picked saves go to
    /// the Saves folder, so they're kept and listed with the recent loads like on the desktop.</summary>
    public static async System.Threading.Tasks.Task<string> CopyIntoPage(IStorageFile file)
    {
        var dir = file.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? SaveAndLoad.SavesDirectory : Path.Combine(root, "Uploads");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, Path.GetFileName(file.Name));
        await using (var src = await file.OpenReadAsync())
        await using (var dst = File.Create(path))
            await src.CopyToAsync(dst);
        return path;
    }
}

/// <summary>JavaScript helpers in wwwroot/main.js (sound, the system beep, opening links in a new tab, downloads, localStorage).</summary>
[SupportedOSPlatform("browser")]
internal static partial class BrowserJs
{
    [JSImport("playSound", "main.js")]
    internal static partial void PlaySound(string url, double volume);
    [JSImport("beep", "main.js")]
    internal static partial void Beep();
    [JSImport("openUrl", "main.js")]
    internal static partial void OpenUrl(string url);
    [JSImport("downloadText", "main.js")]
    internal static partial void DownloadText(string fileName, string text);
    [JSImport("storageKeys", "main.js")]
    internal static partial string[] StorageKeys(string prefix);
    [JSImport("storageGet", "main.js")]
    internal static partial string StorageGet(string key);
    [JSImport("storageSet", "main.js")]
    internal static partial bool StorageSet(string key, string value);
    [JSImport("storageRemove", "main.js")]
    internal static partial void StorageRemove(string key);
}
#endif
