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
    /// <summary>Closing the page's only "window" ends the app (the desktop app quits); in a page there's nothing to quit, so for the main window it's
    /// a no-op. Other windows raise Closed once, as a real window does, so the cleanup their owners hang on it (such as the broadcast window's) runs.</summary>
    public void Close()
    {
        if (this is Program.MyWindow || closed) return;
        closed = true;
        OnClosed(EventArgs.Empty);
    }
    public void Close(object dialogResult) => Close();
    private bool closed;
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
    private const string declinedPrefix = "zt-declined:";   // (the version of a file the player said No to, see Decline)
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
        string[] keys;
        try { keys = BrowserJs.StorageKeys(keyPrefix); }   // (site data can be blocked, so localStorage reads can throw)
        catch (Exception e) { Console.WriteLine($"reading browser storage failed: {e.Message}"); return; }
        foreach (var key in keys)
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
            if (BrowserJs.StoragePaused()) return;   // (a newer tab of the tracker is open: its files win, see main.js)
            var seen = new HashSet<string>();
            foreach (var path in keptFiles())
            {
                var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
                seen.Add(rel);
                var st = stamp(path);
                if (saved.TryGetValue(rel, out var old) && old == st) continue;
                if (didNotFit.TryGetValue(rel, out var failed) && failed == st) continue;   // (tried already and didn't fit: wait until it changes)
                var text = File.ReadAllText(path);
                // (when full, the oldest other save makes room: saves other than the autosave were also downloaded)
                if (store(rel, st, text) || (evictOldestSave(rel) && store(rel, st, text)))
                {
                    saved[rel] = st;
                    writtenAt[rel] = st.Item2;
                    didNotFit.Remove(rel);
                }
                else
                {
                    didNotFit[rel] = st;
                    if (!reportedFull)
                    {
                        reportedFull = true;
                        Console.WriteLine($"the browser's storage is full: {rel} and later changes may not be kept");
                        tellPlayerStorageIsFull = true;
                    }
                }
            }
            foreach (var rel in saved.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                BrowserJs.StorageRemove(keyPrefix + rel);
                BrowserJs.StorageRemove(timePrefix + rel);
                BrowserJs.StorageRemove(declinedPrefix + rel);
                saved.Remove(rel);
                writtenAt.Remove(rel);
                evicted.Remove(rel);
            }
            foreach (var rel in didNotFit.Keys.Where(k => !seen.Contains(k)).ToList()) didNotFit.Remove(rel);
            if (tellPlayerStorageIsFull) showStorageFull();
        }
        catch (Exception e) { Console.WriteLine($"saving to browser storage failed: {e.Message}"); }
    }
    // files that didn't fit, with the size and time that failed
    private static readonly Dictionary<string, (long, DateTime)> didNotFit = new();
    // saves taken out of localStorage to make room (still in the page's file system, and downloaded when they were written)
    private static readonly HashSet<string> evicted = new();
    private static bool tellPlayerStorageIsFull = false;

    // stores one file and its time; false if it didn't fit, in which case neither is changed (so the content and time can't disagree)
    private static bool store(string rel, (long, DateTime) st, string text)
    {
        var oldTime = BrowserJs.StorageGet(timePrefix + rel);
        if (!BrowserJs.StorageSet(timePrefix + rel, st.Item2.Ticks.ToString())) return false;
        if (BrowserJs.StorageSet(keyPrefix + rel, text))
        {
            evicted.Remove(rel);
            return true;
        }
        if (oldTime == "") BrowserJs.StorageRemove(timePrefix + rel);
        else BrowserJs.StorageSet(timePrefix + rel, oldTime);   // (the same size as the new one, so it fits)
        return false;
    }
    // takes the oldest kept save other than the autosave (and other than rel) out of localStorage; false if there is none
    private static bool evictOldestSave(string rel)
    {
        var savesRel = Path.GetRelativePath(root, SaveAndLoad.SavesDirectory).Replace('\\', '/') + "/";
        var autoRel = Path.GetRelativePath(root, SaveAndLoad.AutoSaveFilename).Replace('\\', '/');
        var oldest = saved.Keys.Where(k => k.StartsWith(savesRel) && k != autoRel && k != rel && !evicted.Contains(k) && writtenAt.ContainsKey(k))
                               .OrderBy(k => writtenAt[k]).FirstOrDefault();
        if (oldest == null) return false;
        BrowserJs.StorageRemove(keyPrefix + oldest);
        BrowserJs.StorageRemove(timePrefix + oldest);
        BrowserJs.StorageRemove(declinedPrefix + oldest);
        evicted.Add(oldest);   // (its entry in saved stays, so it isn't stored again unless it changes)
        Console.WriteLine($"the browser's storage is full: removed the save {oldest} from it to make room for {rel}");
        return true;
    }
    // tells the player once, in the tracker, that the browser's storage is full (when no other popup is up)
    private static void showStorageFull()
    {
        var cm = CustomComboBoxes.CanvasManager.TheOnlyCanvasManager;
        if (cm == null || CustomComboBoxes.GlobalFlag.popupIsActive) return;   // (asked again at the next flush)
        tellPlayerStorageIsFull = false;
        CustomComboBoxes.GlobalFlag.popupIsActive = true;
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await CustomComboBoxes.DoModalMessageBox(cm, System.Drawing.SystemIcons.Warning,
                    "This browser's storage for the tracker is full, so some changes may not be kept after this page closes.\n" +
                    "Saves other than the auto-save are also downloaded; keep those files, and open them with Load Save to get them back.", new[] { "Ok" });
            }
            catch (Exception e) { Console.WriteLine($"showing the storage-full message failed: {e.Message}"); }
            finally { CustomComboBoxes.GlobalFlag.popupIsActive = false; }
        });
    }

    /// <summary>When a kept file was last written (UTC), counting earlier visits; null if it isn't kept.</summary>
    public static DateTime? WrittenAt(string path) =>
        writtenAt.TryGetValue(Path.GetRelativePath(root, path).Replace('\\', '/'), out var t) ? t : null;

    /// <summary>Remembers that the player said No to this version of a kept file (the autosave offer), so it isn't offered again until it is rewritten.</summary>
    public static void Decline(string path)
    {
        try
        {
            if (WrittenAt(path) is DateTime t) BrowserJs.StorageSet(declinedPrefix + Path.GetRelativePath(root, path).Replace('\\', '/'), t.Ticks.ToString());
        }
        catch (Exception e) { Console.WriteLine($"remembering the No failed: {e.Message}"); }
    }
    /// <summary>True when the player already said No to this version of the file.</summary>
    public static bool Declined(string path)
    {
        try
        {
            return WrittenAt(path) is DateTime t && BrowserJs.StorageGet(declinedPrefix + Path.GetRelativePath(root, path).Replace('\\', '/')) == t.Ticks.ToString();
        }
        catch (Exception) { return false; }
    }

    /// <summary>Copies a file the user picked (Load, or a custom map image) into the page's file system and returns its path there. Picked saves go to
    /// the Saves folder, so they're kept and listed with the recent loads like on the desktop.</summary>
    public static async System.Threading.Tasks.Task<string> CopyIntoPage(IStorageFile file)
    {
        var dir = file.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? SaveAndLoad.SavesDirectory : Path.Combine(root, "Uploads");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, Path.GetFileName(file.Name));
        // (a picked file named like one already here, such as the autosave or a save from this browser, gets a free name instead of replacing it)
        for (int n = 2; File.Exists(path); n++)
            path = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(file.Name)} ({n}){Path.GetExtension(file.Name)}");
        await using (var src = await file.OpenReadAsync())
        await using (var dst = File.Create(path))
            await src.CopyToAsync(dst);
        return path;
    }
}

/// <summary>Text the page's keyboard sends as one insert (phone keyboards, IME, autocomplete). Avalonia.Browser 11.3 takes typed text only from
/// keydown, and its beforeinput handler drops the inserted text, so main.js passes that text here. It goes to the focused TextBox as a normal
/// TextInput event, so the box's own rules (caret, selection, MaxLength) and any TextInput handlers (such as upper-casing) still apply.</summary>
[SupportedOSPlatform("browser")]
public static partial class BrowserTextInput
{
    /// <summary>Types text into the focused, editable TextBox; false if there is none (main.js then leaves the browser's input alone).</summary>
    [JSExport]
    public static bool InsertText(string text)
    {
        try
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime sv || sv.MainView == null) return false;
            var top = TopLevel.GetTopLevel(sv.MainView);
            if (top?.FocusManager?.GetFocusedElement() is not TextBox tb || tb.IsReadOnly || !tb.IsEffectivelyEnabled) return false;
            tb.RaiseEvent(new Avalonia.Input.TextInputEventArgs { RoutedEvent = Avalonia.Input.InputElement.TextInputEvent, Source = tb, Text = text });
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"text input failed: {e.Message}");
            return false;
        }
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
    [JSImport("storagePaused", "main.js")]
    internal static partial bool StoragePaused();
}
#endif
