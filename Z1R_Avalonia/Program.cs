// Ported from Z1R_WPF/Program.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// The F# file's top level (the `Winterop` module, `MyWindowBase`, `MakeTipTextBox`, `ApplyKonamiCodeEasterEgg`, `MyWindow`, `DummyWindow` and the
// `[<EntryPoint>] main`) is the static class `Z1R_Avalonia.Program` (not to be confused with the desktop host's `Z1R_Avalonia_Desktop.Program`, whose
// `Main` calls `Program.main` here with the Avalonia app builder, since the platform backends live in that project). `ZoraTrackerApp` (namespace level,
// after the class) is the Avalonia Application: WPF's `new Application()` + `app.Run(theDummyWindow)`.
//
// Platform-specific pieces go through Platform.cs: the global F10/F5 hotkeys (WPF: user32 RegisterHotKey + a window-procedure hook; here SharpHook on
// every OS, delivered as the same WM_HOTKEY message), the console window (GetConsoleWindow/ShowWindow) and the ZTracker.lnk shortcut (IShellLink), the
// last two the same Win32 calls as WPF on Windows and stubs (logged) on macOS/Linux.
// User files (crash log, recent loads) are Platform.Files.User files. WPF's Width/Height/ActualWidth are the window's outer size; Avalonia's are the client
// size, so the window sizes below subtract WPF's CHROME_WIDTH/CHROME_HEIGHT, and `ActualWidth`/`ActualHeight` add it back (the formulas stay WPF's).
// Static initialization: see `runStaticInitializersInWpfCompileOrder` (and "Startup order" in PROGRESS.md). Mouse events are pointer events; WPF's
// type-keyed styles in `Resources` are `Styles` (they apply to descendants); `Visibility.Collapsed` is IsVisible; `Visibility.Hidden` is Opacity 0.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using Newtonsoft.Json;
using Z1R_Sync;
using static Z1R_Avalonia.CustomComboBoxes.GlobalFlag;
using Shapes = Avalonia.Controls.Shapes;

namespace Z1R_Avalonia;

public static class Program
{
    // (WPF: the user32/kernel32 declarations; the calls now live in Platform.cs. RegisterHotKey/UnregisterHotKey (SharpHook, every OS) take the window
    // (a Control, so the browser build's stand-in window fits) instead of its HWND. The IShellLink COM interface (used for the ZTracker.lnk shortcut) is in Platform.CreateShortcut.)
    public static class Winterop
    {
        public static bool RegisterHotKey(Control w, int id, uint fsModifiers, uint vk) => Platform.RegisterHotKey(w, id, fsModifiers, vk);
        public static bool UnregisterHotKey(Control w, int id) => Platform.UnregisterHotKey(w, id);
        public static readonly int HOTKEY_ID = 9000;

        public static IntPtr GetConsoleWindow() => Platform.GetConsoleWindow();
        public static bool ShowWindow(IntPtr hWnd, int nCmdShow) => Platform.ShowWindow(hWnd, nCmdShow);
        public static readonly int SW_HIDE = 0;
        public static readonly int SW_SHOWNORMAL = 1;
        public static readonly int SW_SHOW = 5;
        public static readonly int SW_MINIMIZE = 6;
    }

#if BROWSER
    public class MyWindowBase : BrowserWindow   // (browser build: pages have no windows; see Browser.cs)
#else
    public class MyWindowBase : Window
#endif
    {
        private Platform.WndProcHook source = null;   // (WPF: the HwndSource the hook is added to)
        private readonly int VK_F10 = 0x79;
#if DEBUG
#else
        private readonly int VK_F5 = 0x74;
        private readonly uint MOD_NONE = 0u;
#endif
        private readonly TrackerModel.LastChangedTime startTime = TrackerModel.theStartTime;
        public MyWindowBase()
        {
            // full window
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(0.1);
            timer.Tick += (_, _) => this.Update(false);
            timer.Start();
        }
        public void SetStartTimeToNow() => startTime.SetNow();
        public TrackerModel.LastChangedTime StartTime => startTime;
        public virtual void Update(bool _f10Press) { }
        // (WPF: OnSourceInitialized, when the HWND exists; Avalonia: when the window opens)
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            source = this.HwndHook;
            Platform.AddWndProcHook(this, source);
            this.RegisterHotKey();
        }
        protected override void OnClosed(EventArgs e)
        {
            if (source != null)
                Platform.RemoveWndProcHook(this, source);
            source = null;
            this.UnregisterHotKey();
            base.OnClosed(e);
        }
        public void RegisterHotKey()
        {
#if DEBUG
            // in debug mode, do not register hotkeys, as I need e.g. F10 to work to use the debugger!
#else
            if (!(Winterop.RegisterHotKey(this, Winterop.HOTKEY_ID, MOD_NONE, (uint)VK_F10)))
            {
                // handle error
            }
            if (!(Winterop.RegisterHotKey(this, Winterop.HOTKEY_ID, MOD_NONE, (uint)VK_F5)))
            {
                // handle error
            }
#endif
        }
        public void UnregisterHotKey()
        {
            Winterop.UnregisterHotKey(this, Winterop.HOTKEY_ID);
        }
        public IntPtr HwndHook(IntPtr _hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            var WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                if (wParam.ToInt32() == Winterop.HOTKEY_ID)
                {
                    //let ctrl_bits = lParam.ToInt32() &&& 0xF  // see WM_HOTKEY docs
                    var key = lParam.ToInt32() >> 16;
                    if (key == VK_F10)
                        //startTime <- DateTime.Now
                        this.Update(true);
                }
            }
            return IntPtr.Zero;
        }
    }

    // (WPF: a read-only wrapped TextBox. Avalonia's TextBox measures wrapped text a little too narrow, so this is a SelectableTextBlock (see PROGRESS.md);
    // its orange-on-black comes from the dark theme's TextBox style in WPF, so it's set here)
    public static SelectableTextBlock MakeTipTextBox(string txt) =>
        new SelectableTextBlock { Text = txt, FontSize = 14.0, TextAlignment = TextAlignment.Left, HorizontalAlignment = HorizontalAlignment.Left, TextWrapping = TextWrapping.Wrap,
                                  Foreground = Brushes.Orange, Background = Brushes.Black };

    // (port helper: WPF's `fe.Resources.[typeof<TextBox>]`, the implicit TextBox style of an element. Avalonia keeps styles in `Styles`, and a style can't be
    // shared between two elements, so this copies it.)
    private static Style copyOfTextBoxStyle(StyledElement fe)
    {
        foreach (var s in fe.Styles)
            if (s is Style st && st.Selector != null && st.Selector.ToString() == "TextBox")   // (the selector of `x => x.OfType<TextBox>()`)
            {
                var copy = new Style(x => x.OfType<TextBox>());
                foreach (var setter in st.Setters)
                    if (setter is Setter ss)
                        copy.Setters.Add(new Setter(ss.Property, ss.Value));
                return copy;
            }
        return null;
    }

    public static void ApplyKonamiCodeEasterEgg(CustomComboBoxes.CanvasManager cm, Control fe)
    {
        var i = 0;
        var code = new[] {
            Key.Up,
            Key.Up,
            Key.Down,
            Key.Down,
            Key.Left,
            Key.Right,
            Key.Left,
            Key.Right,
            Key.B,
            Key.A,
            };
        // Note: they need to click on an element inside the window (such as text of a tip) first to give focus to fe
        // (WPF: PreviewKeyDown, a tunnelling handler)
        fe.AddHandler(InputElement.KeyDownEvent, new EventHandler<KeyEventArgs>((_, ea) =>
        {
            if (i < code.Length && ea.Key == code[i])
            {
                i = i + 1;
//                printfn "%A" ea.Key
                if (i == code.Length)
                {
                    if (!popupIsActive)
                    {
                        popupIsActive = true;
                        var wh = new ManualResetEvent(false);
                        var all = new DockPanel();
                        var tips = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(3.0) };
                        Control header(string txt)
                        {
                            var tb = MakeTipTextBox(txt);
                            // (WPF: the TextBox's own bottom border; a SelectableTextBlock has none, so it sits in a Border)
                            var hb = new Border { Child = tb, BorderThickness = new Thickness(0.0, 0.0, 0.0, 2.0), BorderBrush = Brushes.Orange, HorizontalAlignment = HorizontalAlignment.Left };
                            tb.FontSize = tb.FontSize + 2.0;
                            hb.Margin = new Thickness(0.0, 15.0, 0.0, 10.0);
                            return hb;
                        }
                        var even = false;
                        SelectableTextBlock body(string txt)
                        {
                            var tb = MakeTipTextBox(txt);
                            tb.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
                            if (even)
                                tb.Background = Brushes.Black;
                            else
                                tb.Background = Graphics.almostBlack;
                            even = !even;
                            return tb;
                        }
                        var desc = MakeTipTextBox("ALL OF TIPS");    // grammar mimics 'ALL OF ITEMS' in LoZ startup scroll
                        desc.FontSize = 18.0;
                        desc.Margin = new Thickness(5.0);
                        var b = new Border { BorderBrush = Brushes.Orange, BorderThickness = new Thickness(0.0, 0.0, 0.0, 2.0), Child = desc, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0.0, 0.0, 0.0, 5.0) };
                        all.Children.Add(b);
                        DockPanel.SetDock(b, Dock.Top);
                        var sv = new ScrollViewer { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = tips };
                        all.Children.Add(sv);
                        tips.Children.Add(header("Novice-level tips"));
                        foreach (var t in DungeonData.Factoids.noviceTips)
                            tips.Children.Add(body(t));
                        tips.Children.Add(header("Intermediate-level tips"));
                        foreach (var t in DungeonData.Factoids.intermediateTips)
                            tips.Children.Add(body(t));
                        tips.Children.Add(header("Advanced-level tips"));
                        foreach (var t in DungeonData.Factoids.advancedTips)
                            tips.Children.Add(body(t));
                        tips.Children.Add(header("ZORATracker tips"));
                        foreach (var t in DungeonData.Factoids.zTrackerTips)
                            tips.Children.Add(body(t));
                        var BUFFER = 160.0;
                        var tipsBorder = new Border { Width = cm.AppMainCanvas.Width - BUFFER, Height = cm.AppMainCanvas.Height - BUFFER, Background = Brushes.Black,
                                                      BorderBrush = Brushes.Orange, BorderThickness = new Thickness(3.0), Child = all };
                        var textBoxStyle = copyOfTextBoxStyle(fe);
                        if (textBoxStyle != null)
                            tipsBorder.Styles.Add(textBoxStyle);
                        async Task go()
                        {
                            await CustomComboBoxes.DoModal(cm, wh, BUFFER / 2.0, BUFFER / 2.0, tipsBorder);
                            popupIsActive = false;
                        }
                        CustomComboBoxes.StartImmediate(go());
                    }
                }
            }
        }), RoutingStrategies.Tunnel);
    }





    // (port: WPF's hidden owner window; here an object that does the same job, see DummyWindow below)
    public static DummyWindow theDummyWindow = null;
    public static readonly ManualResetEvent waitForConsole = new ManualResetEvent(true);

    // (port: Console.ReadKey throws when there is no console to read from, e.g. a macOS .app or the WinExe build; WPF's tracker always has one)
    private static void readKey(bool intercept = false)
    {
        try { Console.ReadKey(intercept); }
        catch (InvalidOperationException) { }
    }

    // (port helper: WPF's Microsoft.Win32.OpenFileDialog, on Avalonia's StorageProvider. `Filter` is in WPF's "Name|*.a;*.b|Name|*.c" format;
    // ShowDialog is asynchronous (WPF's blocks), so callers await it.)
    public sealed class OpenFileDialog
    {
        public string InitialDirectory = null;
        public string Filter = "";
        public string FileName = "";
        public async Task<bool?> ShowDialog(Window owner)
        {
            var types = new List<FilePickerFileType>();
            var parts = Filter.Split('|');
            for (int k = 0; k + 1 < parts.Length; k += 2)
            {
                var patterns = parts[k + 1].Split(';');
                if (patterns.Length == 1 && patterns[0] == "*.*")
                    types.Add(new FilePickerFileType(parts[k]) { Patterns = new[] { "*" } });
                else
                    types.Add(new FilePickerFileType(parts[k]) { Patterns = patterns });
            }
            var opts = new FilePickerOpenOptions { AllowMultiple = false, FileTypeFilter = types };
            if (InitialDirectory != null)
                opts.SuggestedStartLocation = await owner.StorageProvider.TryGetFolderFromPathAsync(InitialDirectory);
            IReadOnlyList<IStorageFile> files;
            try { files = await owner.StorageProvider.OpenFilePickerAsync(opts); }
            catch (Exception e) { Console.WriteLine($"the file picker failed: {e.Message}"); return false; }   // (the browser's picker reports Cancel as an error)
            var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
#if BROWSER
            if (path == null && files.Count > 0)
                path = await BrowserStorage.CopyIntoPage(files[0]);   // (a page can't open the user's files by path, so the picked file is copied in)
#endif
            if (path == null)
                return false;
            FileName = path;
            return true;
        }
    }

    // In WPF (an F# exe), each file's top-level values and `do` blocks run the first time a value of that file is used. In C#, a class's static field
    // initializers likewise run when it's first used, but at slightly different moments (and a class whose initializer throws stays broken), so this runs
    // them in WPF's compile order, ending with WPFUI, before anything else needs them (e.g. WPFUI's `_init2` sets OverworldMapTileCustomization.setDecorIconsHook;
    // the Race Mode stamp and the container panel subscribe to OptionsMenu.raceModeChanged; Reminders, CoopSync and MainTrackerTriforcesAndItems subscribe to
    // model events). It must run after the CanvasManager exists: Dungeon's `do` block reads CanvasManager.TheOnlyCanvasManager, and DungeonUI,
    // OverworldItemGridUI, UIComponents, Layout and WPFUI use Dungeon in theirs (in WPF too, so WPF necessarily initializes them after the CanvasManager, which
    // MyWindow creates after reading the settings). SpeechRecognition is left out: its only value is the System.Speech recognizer, which WPF creates only
    // when speech recognition is turned on (creating it can fail on a PC with no recognizer installed).
    private static void runStaticInitializersInWpfCompileOrder()
    {
        var compileOrder = new[] {
            typeof(Gamepad), typeof(Graphics), typeof(CustomMessageBox), typeof(OverworldRouteDrawing), typeof(DungeonRoomState), typeof(HotKeys), typeof(Timeline),
            typeof(CustomComboBoxes), typeof(PieMenus), typeof(ShowRunCustom), typeof(Dungeon), typeof(OptionsMenu), typeof(Views), typeof(DungeonSaveAndLoad),
            typeof(DungeonHighlightsUI), typeof(DungeonPopups), typeof(DungeonUI), /* SpeechRecognition: see above */ typeof(OverworldMapTileCustomization),
            typeof(LinkRouting), typeof(Popouts), typeof(OverworldItemGridUI), typeof(UIComponents), typeof(HUDs), typeof(Broadcast), typeof(UIHelpers),
            typeof(UserCustomLayer), typeof(DrawingLayer), typeof(Layout), typeof(Reminders), typeof(MainTrackerTriforcesAndItems), typeof(Debouncer),
            typeof(SyncOriginTracker), typeof(DungeonCInterop), typeof(CoopSync), typeof(DungeonModelSync), typeof(WPFUI) };
        foreach (var t in compileOrder)
            RuntimeHelpers.RunClassConstructor(t.TypeHandle);
    }

    public class MyWindow : MyWindowBase
    {
        private Action<int> updateTimeline = _ => { };
        private int lastUpdateMinute = 0;
        private int lastUpdateSecond = 0;
        private DateTime lastSnoopPoll = DateTime.MinValue;
        //                             items  ow map  prog  dungeon tabs                                    timeline
        private readonly double HEIGHT_SANS_CHROME = (double)(30*5 + 11*3*9 + 30 + OverworldItemGridUI.TH + 30 + 27*8 + 12*7 + 3 + OverworldItemGridUI.TCH + 6);
        private readonly double WIDTH_SANS_CHROME = (double)(16*16*3);  // ow map width
        private readonly double CHROME_WIDTH = 16.0, CHROME_HEIGHT = 39.0;  // Windows app border   (Avalonia: window sizes are client sizes; see the header comment)
        private readonly double HEIGHT;
        private readonly double WIDTH;
        private bool loggedAnyCrash = false;
        private bool promptedCrashRecovery = false;
        private bool gotThruStartup = false;
        private readonly string crashLogFilename = Platform.Files.User("Z1R_Tracker_crash_log.txt");
        private readonly string dateTimeFormat = "yyyy-MM-dd-HH:mm:ss";
        // (WPF's ActualWidth/ActualHeight: the outer size, frame included. Avalonia's Bounds are the client area.)
        private double ActualWidth => this.Bounds.Width + CHROME_WIDTH;
        private double ActualHeight => this.Bounds.Height + CHROME_HEIGHT;
        public MyWindow()
        {
            HEIGHT = HEIGHT_SANS_CHROME + CHROME_HEIGHT;
            WIDTH = WIDTH_SANS_CHROME + CHROME_WIDTH;
            void showCrashOnConsole()
            {
                waitForConsole.Reset();
                // (F#: `async { ... } |> Async.Start`, on the thread pool)
                Task.Run(() =>
                {
                    try
                    {
                        var handle = Winterop.GetConsoleWindow();
                        Winterop.ShowWindow(handle, Winterop.SW_SHOWNORMAL);
                        while (Console.KeyAvailable)
                            Console.ReadKey(false);   // clear keyboard buffer
                        if (gotThruStartup)
                        {
                            Console.WriteLine("");
                            Console.WriteLine("If ZORATracker crashed while you were playing, you might be able to restore the most recent");
                            Console.WriteLine("auto-save, by restarting ZORATracker right now.");
                        }
                        Console.WriteLine("");
                        Console.WriteLine("Press a key to close this window");
                        Console.ReadKey();
                    }
                    catch (InvalidOperationException) { }   // (port: no console to read from; see readKey)
                    finally
                    {
                        waitForConsole.Set();   // (port: always, so the app can exit even when the console can't be read)
                    }
                });
            }
            void logCrashInfo(string s)
            {
                Console.WriteLine(s);
                try
                {
                    if (!loggedAnyCrash)
                    {
                        loggedAnyCrash = true;
                        File.AppendAllText(crashLogFilename, $"\n\nBEGIN CRASH LOG -- {OverworldData.ProgramNameString} -- {DateTime.Now.ToString(dateTimeFormat)}\n");
                    }
                    File.AppendAllText(crashLogFilename, $"{s}\n");
                }
                catch (Exception) { }
            }
            void finishCrashInfoImpl(string extra)
            {
                var finalText = gotThruStartup ? extra : "(during startup)\n";  // this intentionally interacts with crash recovery dialog
                File.AppendAllText(crashLogFilename, $"END CRASH LOG\n{DateTime.Now.ToString(dateTimeFormat)}\n{finalText}");
            }
            void finishCrashInfo() => finishCrashInfoImpl("");
            void handle(Exception ex)
            {
                switch (ex)
                {
                    case TrackerModelOptions.IntentionalApplicationShutdown ias:
                        logCrashInfo($"{ias.Message}");
                        finishCrashInfo();
                        break;
                    case HotKeys.UserError hue:
                        logCrashInfo("");
                        logCrashInfo("Error parsing HotKeys.txt:");
                        logCrashInfo("");
                        logCrashInfo($"{hue.Message}");
                        logCrashInfo("");
                        logCrashInfo("You should fix this error by editing the text file.");
                        logCrashInfo("Or you can delete it, and an empty hotkeys template file will be created in its place.");
                        logCrashInfo("");
                        finishCrashInfo();
                        var fileToSelect = HotKeys.HotKeyFilename;
                        Platform.RevealInFileManager(fileToSelect);   // (WPF: Explorer.exe /Select, "file")
                        Console.WriteLine("Press a key to close this window");
                        readKey();
                        break;
                    default:
                        logCrashInfo($"{ex.ToString()}");
                        finishCrashInfo();
                        break;
                }
            }
            Graphics.installAppWideStyles();   // rounded dark Button / CheckBox look everywhere unless a panel supplies its own
            // (WPF: Application.Current.DispatcherUnhandledException)
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                if (Debugger.IsAttached)
                    Debugger.Break();
                var ex = e.Exception;
                logCrashInfo("An unhandled exception from UI thread:");
                handle(ex);
                e.Handled = true;
                finishCrashInfo();
                showCrashOnConsole();
                if (theDummyWindow != null)
                    theDummyWindow.Close();
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (Debugger.IsAttached)
                    Debugger.Break();
                switch (e.ExceptionObject)
                {
                    case Exception ex:
                        logCrashInfo("An unhandled exception from background thread:");
                        handle(ex);
                        finishCrashInfo();
                        break;
                    default:
                        logCrashInfo("An unhandled exception from background thread occurred.");
                        finishCrashInfo();
                        break;
                }
                showCrashOnConsole();
            };

            HotKeys.PopulateHotKeyTables();
            var settingsWereSuccessfullyRead = false;
            TrackerModelOptions.readSettings();
            settingsWereSuccessfullyRead = true;
            Z1R_SharedInterop.TrackerOptionsBridge.Initialize(
                FuncConvert.FromFunc(() => TrackerModelOptions.BookForHelpfulHints.Value),
                FuncConvert.FromFunc(() => TrackerModelOptions.DoDoorInference.Value)
            );

            //RPT try to initialize here for SyncManager
            SyncManager.Configure(TrackerModelOptions.CoopSyncOptions.SyncUpdateUrl());
            TrackerModelOptions.CoopSyncOptions.EnableCoopChanged.Publish.AddHandler(new FSharpHandler<bool>((_, isEnabled) =>
            {
                if (isEnabled)
                {
                    Console.WriteLine("[Sync] Co-op enabled at runtime — resetting SyncUrl");
                    SyncManager.Configure(TrackerModelOptions.CoopSyncOptions.SyncUpdateUrl());
                }
                else
                    Console.WriteLine("[Sync] Co-op disabled at runtime — not connecting");

            }));
            // some global variables have already been initialized, and so they read the default value of settings, rather than the settings we just read; this bit patches that up:
            OptionsMenu.InitializeVoice();
            OptionsMenu.triggerHideTimerChanged();
            Graphics.triggerVolumeChanged(TrackerModelOptions.Volume);

#if !BROWSER   // (browser build: no processes, and nothing to make a shortcut to)
            {
                // (WPF: an IShellLink saved as ZTracker.lnk; Windows only, see Platform.CreateShortcut)
                var cwd = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                Platform.CreateShortcut(Path.Combine(cwd, "ZTracker.lnk"), Path.Combine(cwd, Graphics.ExeName), cwd, Path.Combine(cwd, "icons/ztlogo64x64.ico"), "Launch ZORATracker");
            }
#endif

            Graphics.theWindow = this;
            // full window
            this.Title = "ZORATracker";
            this.CanResize = true;   // resizable: the whole UI scales to fit (see the SizeChanged handler below)
            this.MinWidth = Math.Round(WIDTH_SANS_CHROME*0.4 + CHROME_WIDTH) - CHROME_WIDTH;   // (Avalonia: client size)
            this.MinHeight = Math.Round(HEIGHT_SANS_CHROME*0.4 + CHROME_HEIGHT) - CHROME_HEIGHT;
            this.SizeToContent = SizeToContent.Manual;
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            double APP_WIDTH, APP_HEIGHT;
            if (TrackerModelOptions.MainWindowScale > 0.0)   // the user resized the window last time; the size is remembered as the scale
            {
                Graphics.dynamicAppScale = TrackerModelOptions.MainWindowScale;
                (APP_WIDTH, APP_HEIGHT) = (Math.Round(WIDTH_SANS_CHROME*TrackerModelOptions.MainWindowScale + CHROME_WIDTH), Math.Round(HEIGHT_SANS_CHROME*TrackerModelOptions.MainWindowScale + CHROME_HEIGHT));
            }
            else if (TrackerModelOptions.SmallerAppWindow.Value)
                (APP_WIDTH, APP_HEIGHT) = (Math.Round(WIDTH_SANS_CHROME*TrackerModelOptions.SmallerAppWindowScaleFactor + CHROME_WIDTH), Math.Round(HEIGHT_SANS_CHROME*TrackerModelOptions.SmallerAppWindowScaleFactor + CHROME_HEIGHT));
            else
                (APP_WIDTH, APP_HEIGHT) = (WIDTH, HEIGHT);
            //printfn "%f, %f" APP_WIDTH APP_HEIGHT
            var leftTop = TrackerModelOptions.MainWindowLT;
            var matches = System.Text.RegularExpressions.Regex.Match(leftTop, @"^(-?\d+),(-?\d+)$");
            if (matches.Success)
            {
                // (WPF: Left/Top, in WPF units; ShowRunCustom.setLeftTop would use the main window's scaling, which isn't set yet, so this window's own is used)
                var scaling = this.DesktopScaling > 0.0 ? this.DesktopScaling : 1.0;
                this.Position = new PixelPoint((int)Math.Round(double.Parse(matches.Groups[1].Value, CultureInfo.InvariantCulture) * scaling),
                                               (int)Math.Round(double.Parse(matches.Groups[2].Value, CultureInfo.InvariantCulture) * scaling));
            }
            this.PositionChanged += (_, _) =>
            {
                var (left, top) = (ShowRunCustom.leftOf(this), ShowRunCustom.topOf(this));  // cache to local variable; I suspect there is a race where 2 subsequent reads yield different values during app shutdown
                if (left < ShowRunCustom.MINIMIZED_THRESHOLD || top < ShowRunCustom.MINIMIZED_THRESHOLD || this.WindowState == WindowState.Minimized)
                {
                    // when you Minimize the window, -32000,-32000 are reported. Don't save that, as it's hard to recover window later   (macOS/Linux: by WindowState)
                }
                else
                {
                    TrackerModelOptions.MainWindowLT = $"{(int)left},{(int)top}";
                    TrackerModelOptions.writeSettings();
                }
            };
            this.Width = APP_WIDTH - CHROME_WIDTH;   // (Avalonia: client size)
            this.Height = APP_HEIGHT - CHROME_HEIGHT;
            this.FontSize = 18.0;
            this.Background = Brushes.Black;  // on a device with say 125% pixels and UseLayoutRounding=true, there's a white line at the bottom/right edge where RootCanvas doesn't cover app

            Canvas appMainCanvas;
            CustomComboBoxes.CanvasManager cm;
            {  // a scope, so code below is less likely to touch rootCanvas
                var APP_CONTENT_HEIGHT = HEIGHT_SANS_CHROME;
                var rootCanvas = new Canvas { Width = 16.0*Graphics.OMTW, Height = APP_CONTENT_HEIGHT, Background = Brushes.Black };
                rootCanvas.UseLayoutRounding = true;
                appMainCanvas = new Canvas { Width = 16.0*Graphics.OMTW, Height = APP_CONTENT_HEIGHT, Background = Brushes.Black };
                var style = new Style(x => x.OfType<ToolTip>());
                style.Setters.Add(new Setter(TemplatedControl.ForegroundProperty, Brushes.Orange));
                style.Setters.Add(new Setter(TemplatedControl.BackgroundProperty, Graphics.almostBlack));
                style.Setters.Add(new Setter(TemplatedControl.BorderBrushProperty, Brushes.DarkGray));
                rootCanvas.Styles.Add(style);
                Graphics.canvasAdd(rootCanvas, appMainCanvas, 0.0, 0.0);
                cm = new CustomComboBoxes.CanvasManager(rootCanvas, appMainCanvas);
            }
            runStaticInitializersInWpfCompileOrder();   // (port: see the method; the earliest point at which every class can initialize)
            // Stubbed SignalR sync startup

            var lastOverworldSyncSource = "";
            var lastOverworldSyncTime = DateTime.MinValue;

            bool shouldApplyUpdate(string senderId)
            {
                var now = DateTime.UtcNow;
                var elapsed = now - lastOverworldSyncTime;
                if (lastOverworldSyncSource == senderId && elapsed.TotalMilliseconds < 500.0)
                    return false;
                else
                {
                    lastOverworldSyncSource = senderId;
                    lastOverworldSyncTime = now;
                    return true;
                }
            }

            var lastDungeonMapsJson = "";
            var _dungeonMapsDebouncer = new Debouncer(500);

            void handleRemoteTileChange(string tileId, string iconId, string senderId)
            {
                var me = TrackerModelOptions.CoopSyncOptions.MyConsoleId;
                var target = TrackerModelOptions.CoopSyncOptions.TargetConsoleId;
                if (senderId != me && senderId == target)
                {
                    try
                    {
                        //let tileInt = int tileId
                        //let iconInt = int iconId
                        //TileCustomization.assignIcon cm tileInt iconInt
                        TrackerModelOptions.DebugConfig.Log($"[Sync] Received tile change from {senderId}: tileId={tileId}, iconId={iconId}");
                    }
                    catch (Exception ex)
                    {
                        TrackerModelOptions.DebugConfig.Log($"[Sync] Error applying tile change: {ex.Message}");
                    }
                }
                else
                    // Optional debug log for ignored messages
                    TrackerModelOptions.DebugConfig.Log($"[Sync] Ignored tile update from senderId={senderId} (target={target}, me={me})");
            }
            void handleIncomingMessage(string msgType, string payloadJson, string senderId, long timestamp)
            {
                var target = TrackerModelOptions.CoopSyncOptions.TargetConsoleId;
                var me = TrackerModelOptions.CoopSyncOptions.MyConsoleId;
                bool trackerIsInitialized() => FSharpOption<TrackerModel.DungeonTrackerInstance>.get_IsSome(TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption);
                if (!(TrackerModelOptions.CoopSyncOptions.GetEnableCoop()))
                    TrackerModelOptions.DebugConfig.Log($"[Sync] Co-op disabled — ignoring incoming message: {msgType}");
                else
                {
                    if (senderId != me && senderId == target)
                    {
                        if (CoopSync.alreadyReceived(msgType, payloadJson))
                            TrackerModelOptions.DebugConfig.Log($"[Sync] Ignored duplicate message from {senderId}");
                        else
                        {
                            switch (msgType)
                            {
                                case "PlayerProgress":
                                    try
                                    {
                                        TrackerModelOptions.DebugConfig.Log($"[Sync] Raw PlayerProgress payload: {payloadJson}");
                                        var data = JsonConvert.DeserializeObject<SaveAndLoad.PlayerProgressAndTakeAnyHeartsModel>(payloadJson);
                                        Dispatcher.UIThread.Invoke(() =>
                                        {
                                            CoopSync.isApplyingPlayerProgressFromSync = true;
                                            try
                                            {
                                                data.Apply();
                                                TrackerModelOptions.DebugConfig.Log($"[Sync] Applied PlayerProgress update from {senderId}");
                                            }
                                            finally
                                            {
                                                CoopSync.isApplyingPlayerProgressFromSync = false;
                                            }
                                            TrackerModel.TimelineItemModel.TriggerTimelineChanged();
                                        });
                                    }
                                    catch (Exception ex)
                                    {
                                        TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to apply PlayerProgress update: {ex.Message}");
                                    }
                                    break;
                                case "Items":

                                    if (!(CoopSync.shouldApplyUpdate("Items", payloadJson, timestamp)))
                                        TrackerModelOptions.DebugConfig.Log($"[Sync] Skipping duplicate Items update from {senderId}");
                                    else if (!trackerIsInitialized())
                                        TrackerModelOptions.DebugConfig.Log($"[Sync] Skipping Items update from {senderId} — TrackerModel not initialized");
                                    else
                                    {
                                        try
                                        {
                                            var data = JsonConvert.DeserializeObject<SaveAndLoad.Items>(payloadJson);
                                            Dispatcher.UIThread.Invoke(() =>
                                            {
                                                CoopSync.isApplyingItemsFromSync = true;
                                                try
                                                {
                                                    // Top-level flags
                                                    TrackerModel.IsHiddenDungeonNumbers = FuncConvert.FromFunc(() => data.HiddenDungeonNumbers);
                                                    TrackerModel.IsSecondQuestDungeons = data.SecondQuestDungeons;

                                                    // Top-level boxes — only apply if value has actually changed to avoid re-timestamping
                                                    if (data.WhiteSwordBox.CellCurrent != TrackerModel.sword2Box.CellCurrent() || data.WhiteSwordBox.PlayerHas != TrackerModel.sword2Box.PlayerHas().AsInt())
                                                        data.WhiteSwordBox.TryApply(TrackerModel.sword2Box);
                                                    if (data.LadderBox.CellCurrent != TrackerModel.ladderBox.CellCurrent() || data.LadderBox.PlayerHas != TrackerModel.ladderBox.PlayerHas().AsInt())
                                                        data.LadderBox.TryApply(TrackerModel.ladderBox);
                                                    if (data.ArmosBox.CellCurrent != TrackerModel.armosBox.CellCurrent() || data.ArmosBox.PlayerHas != TrackerModel.armosBox.PlayerHas().AsInt())
                                                        data.ArmosBox.TryApply(TrackerModel.armosBox);

                                                    // Dungeons
                                                    for (int i = 0; i < data.Dungeons.Length; i++)
                                                    {
                                                        var dungeon = data.Dungeons[i];
                                                        if (dungeon != null)
                                                        {
                                                            var targetDungeon = TrackerModel.GetDungeon(i);
                                                            var expectedLength = targetDungeon.Boxes.Length;

                                                            dungeon.Triforce = targetDungeon.PlayerHasTriforce();

                                                            if (dungeon.Boxes == null || dungeon.Boxes.Length != expectedLength)
                                                            {
                                                                var safeBoxes = new SaveAndLoad.Box[expectedLength];
                                                                for (int j = 0; j < expectedLength; j++)
                                                                {
                                                                    if (dungeon.Boxes != null && j < dungeon.Boxes.Length && dungeon.Boxes[j] != null)
                                                                        safeBoxes[j] = dungeon.Boxes[j];
                                                                    else
                                                                        safeBoxes[j] = new SaveAndLoad.Box();
                                                                }
                                                                dungeon.Boxes = safeBoxes;
                                                            }

                                                            // Apply all properties EXCEPT Triforce (sync separately)
                                                            targetDungeon.Color = dungeon.Color;
                                                            targetDungeon.LabelChar = dungeon.LabelChar.Length > 0 ? dungeon.LabelChar[0] : '?';
                                                            targetDungeon.PlayerHasMapOfThisDungeon = dungeon.PlayerHasMap;

                                                            for (int j = 0; j < dungeon.Boxes.Length; j++)
                                                            {
                                                                var (srcBox, destBox) = (dungeon.Boxes[j], targetDungeon.Boxes[j]);
                                                                // Only apply if value has actually changed to avoid re-timestamping in the timeline
                                                                if (srcBox != null && (srcBox.CellCurrent != destBox.CellCurrent() || srcBox.PlayerHas != destBox.PlayerHas().AsInt()))
                                                                    srcBox.TryApply(destBox);
                                                            }

                                                            // If any box has an item, mark this dungeon as seen so the summary tab shows content
                                                            if (dungeon.Boxes != null && dungeon.Boxes.Any(b => b != null && b.CellCurrent > -1))
                                                                DungeonUI.isFirstTimeClickingAnyRoom[i].Value = false;
                                                        }
                                                    }

                                                    TrackerModelOptions.DebugConfig.Log($"[Sync] Applied Items update from {senderId} (Triforce excluded)");
                                                }
                                                finally
                                                {
                                                    CoopSync.isApplyingItemsFromSync = false;
                                                }
                                                // Force the timeline to redraw now that items have been applied
                                                TrackerModel.TimelineItemModel.TriggerTimelineChanged();
                                            });

                                        }
                                        catch (Exception ex)
                                        {
                                            TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to apply Items update: {ex.Message}");
                                        }
                                    }
                                    break;
                                case "StartingItems":
                                    try
                                    {
                                        var data = JsonConvert.DeserializeObject<SaveAndLoad.StartingItemsAndExtrasModel>(payloadJson);
                                        Dispatcher.UIThread.Invoke(() =>
                                        {
                                            for (int i = 0; i <= 7; i++)
                                            {
                                                TrackerModel.startingItemsAndExtras.HDNStartingTriforcePieces[i].Set(data.HDNStartingTriforcePieces[i]);
                                                TrackerModel.startingItemsAndExtras.PlayerHasWhiteSword.Set(data.PlayerHasWhiteSword);
                                                TrackerModel.startingItemsAndExtras.PlayerHasMagicalSword.Set(data.PlayerHasMagicalSword);
                                                TrackerModel.startingItemsAndExtras.PlayerHasSilverArrow.Set(data.PlayerHasSilverArrow);
                                                TrackerModel.startingItemsAndExtras.PlayerHasBow.Set(data.PlayerHasBow);
                                                TrackerModel.startingItemsAndExtras.PlayerHasWand.Set(data.PlayerHasWand);
                                                TrackerModel.startingItemsAndExtras.PlayerHasRedCandle.Set(data.PlayerHasRedCandle);
                                                TrackerModel.startingItemsAndExtras.PlayerHasBoomerang.Set(data.PlayerHasBoomerang);
                                                TrackerModel.startingItemsAndExtras.PlayerHasMagicBoomerang.Set(data.PlayerHasMagicBoomerang);
                                                TrackerModel.startingItemsAndExtras.PlayerHasRedRing.Set(data.PlayerHasRedRing);
                                                TrackerModel.startingItemsAndExtras.PlayerHasPowerBracelet.Set(data.PlayerHasPowerBracelet);
                                                TrackerModel.startingItemsAndExtras.PlayerHasLadder.Set(data.PlayerHasLadder);
                                                TrackerModel.startingItemsAndExtras.PlayerHasRaft.Set(data.PlayerHasRaft);
                                                TrackerModel.startingItemsAndExtras.PlayerHasRecorder.Set(data.PlayerHasRecorder);
                                                TrackerModel.startingItemsAndExtras.PlayerHasAnyKey.Set(data.PlayerHasAnyKey);
                                                TrackerModel.startingItemsAndExtras.PlayerHasBook.Set(data.PlayerHasBook);
                                                TrackerModel.startingItemsAndExtras.PlayerHasMeat.Set(data.PlayerHasMeat);
                                                TrackerModel.startingItemsAndExtras.PlayerHasLetter.Set(data.PlayerHasLetter);
                                                TrackerModel.startingItemsAndExtras.MaxHeartsDifferential = data.MaxHeartsDifferential;

                                                TrackerModelOptions.DebugConfig.Log($"[Sync] Applied StartingItems update from {senderId}");
                                            }
                                        });
                                    }
                                    catch (Exception ex)
                                    {
                                        TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to apply StartingItems update: {ex.Message}");
                                    }
                                    break;

                                case "Overworld":
                                    if (TrackerModel.overworldMapMarks == null)
                                        TrackerModelOptions.DebugConfig.Log("[Sync] Skipping Overworld update — TrackerModel not initialized yet.");
                                    else if (!(shouldApplyUpdate(senderId)))
                                        TrackerModelOptions.DebugConfig.Log($"[Sync] Ignored Overworld update from {senderId} (too soon after last update)");
                                    else
                                    {
                                        TrackerModelOptions.DebugConfig.Log($"[Sync] Raw Overworld payload: {payloadJson}");
                                        // Apply the update safely here
                                        try
                                        {
                                            var data = JsonConvert.DeserializeObject<SaveAndLoad.Overworld>(payloadJson);
                                            Dispatcher.UIThread.Invoke(() =>
                                            {
                                                TrackerModel.MirrorOverworld = data.MirrorOverworld;
                                                TrackerModel.startIconX = data.StartIconX;
                                                TrackerModel.startIconY = data.StartIconY;
                                                TrackerModel.customWaypointX = data.CustomWaypointX;
                                                TrackerModel.customWaypointY = data.CustomWaypointY;

                                                if (data.Map != null && data.Map.Length == 384)
                                                {
                                                    for (int j = 0; j <= 7; j++)
                                                        for (int i = 0; i <= 15; i++)
                                                        {
                                                            var idx = (j * 16 + i) * 3;
                                                            var cur = data.Map[idx];
                                                            var ed  = data.Map[idx + 1];
                                                            var circ = data.Map[idx + 2];
                                                            TrackerModel.overworldMapMarks[i, j].Set(cur);
                                                            if (cur != -1)
                                                            {
                                                                var edKey = TrackerModel.MapSquareChoiceDomainHelper.IsItem(cur) ? TrackerModel.MapSquareChoiceDomainHelper.SHOP : cur;
                                                                TrackerModel.setOverworldMapExtraData(i, j, edKey, ed);
                                                            }
                                                            TrackerModel.overworldMapCircles[i, j] = circ;
                                                        }
                                                }
                                                TrackerModelOptions.DebugConfig.Log($"[Sync] Applied Overworld update from {senderId}");
                                            });
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.Write($"[Sync] Failed to apply Overworld update: {ex.Message}");
                                        }
                                    }
                                    break;
                                case "Hints":
                                    try
                                    {
                                        var data = JsonConvert.DeserializeObject<SaveAndLoad.Hints>(payloadJson);
                                        Dispatcher.UIThread.Invoke(() =>
                                        {
                                            for (int i = 0; i <= 10; i++)
                                                TrackerModel.SetLevelHint.Invoke(Tuple.Create(i, TrackerModel.HintZone.FromIndex(data.LocationHints[i])));
                                            TrackerModel.NoFeatOfStrengthHintWasGiven = data.NoFeatOfStrengthHint;
                                            TrackerModel.SailNotHintWasGiven = data.SailNotHint;
                                            TrackerModel.SailToHintWasGiven = data.SailToHint;
                                            TrackerModel.PlayMelodyHintWasGiven = data.PlayMelodyHint;
                                            TrackerModel.StepOverWaterHintWasGiven = data.StepOverWaterHint;
                                            TrackerModel.FireArrowsHintWasGiven = data.FireArrowsHint;

                                            TrackerModelOptions.DebugConfig.Log($"[Sync] Applied Hints update from {senderId}");
                                        });
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"[Sync] Failed to apply Hints update: {ex.Message}");
                                    }
                                    break;
                                case "Blockers":
                                    if (!trackerIsInitialized())
                                        TrackerModelOptions.DebugConfig.Log("[Sync][Blockers] Skipping update — tracker not initialized yet.");
                                    else
                                    {
                                        try
                                        {
                                            var data = JsonConvert.DeserializeObject<SaveAndLoad.Blocker[][]>(payloadJson);
                                            Dispatcher.UIThread.Invoke(() =>
                                            {
                                                TrackerModel.DungeonBlockersContainer.StartIgnoreChangesDuringLoad();
                                                for (int i = 0; i <= data.Length - 1; i++)
                                                    for (int j = 0; j <= data[i].Length - 1; j++)
                                                    {
                                                        var b = data[i][j];
                                                        if (b != null)
                                                        {
                                                            var blockerKind = TrackerModel.DungeonBlocker.FromHotKeyName(b.Kind);
                                                            TrackerModel.DungeonBlockersContainer.SetDungeonBlocker(i, j, blockerKind);
                                                            for (int k = 0; k <= b.AppliesTo.Length - 1; k++)
                                                                TrackerModel.DungeonBlockersContainer.SetDungeonBlockerAppliesTo(i, j, k, b.AppliesTo[k]);
                                                            var actualKind = TrackerModel.DungeonBlockersContainer.GetDungeonBlocker(i, j);
                                                            TrackerModelOptions.DebugConfig.Log($"[Sync][Blockers] Applied blocker [{i},{j}]: {actualKind}");
                                                        }
                                                    }
                                                TrackerModel.DungeonBlockersContainer.FinishIgnoreChangesDuringLoad();
                                                TrackerModelOptions.DebugConfig.Log($"[Sync] Applied Blockers update from {senderId}");
                                            });
                                        }
                                        catch (Exception ex)
                                        {
                                            TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to apply Blockers update: {ex.Message}");
                                        }
                                    }
                                    break;

                                case "DungeonTriforce":
                                    if (!(CoopSync.shouldApplyUpdate("DungeonTriforce", payloadJson, timestamp)))
                                        TrackerModelOptions.DebugConfig.Log("[Sync] Skipped DungeonTriforce update — no change");
                                    else
                                    {
                                        try
                                        {
                                            var data = JsonConvert.DeserializeObject<CoopSync.DungeonsTriforceState>(payloadJson);
                                            Dispatcher.UIThread.Invoke(() =>
                                            {
                                                CoopSync.isApplyingTriforceFromSync = true;
                                                try
                                                {
                                                    for (int i = 0; i <= 8; i++)
                                                    {
                                                        var dungeon = TrackerModel.GetDungeon(i);
                                                        var has = data.Triforces[i];
                                                        if (dungeon.PlayerHasTriforce() != has)
                                                            dungeon.ToggleTriforce();
                                                        if (has)
                                                            DungeonUI.isFirstTimeClickingAnyRoom[i].Value = false;
                                                    }
                                                    TrackerModel.mapLastChangedTime.SetNow();
                                                    TrackerModelOptions.DebugConfig.Log($"[Sync] Applied DungeonTriforce update from {senderId}");
                                                }
                                                finally
                                                {
                                                    CoopSync.isApplyingTriforceFromSync = false;
                                                }
                                                TrackerModel.TimelineItemModel.TriggerTimelineChanged();
                                            });
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"[Sync] Failed to apply DungeonTriforce update: {ex.Message}");
                                        }
                                    }
                                    break;
                                case "DungeonMaps":
                                    if (!(CoopSync.shouldApplyUpdate("DungeonMaps", payloadJson, timestamp)))
                                        TrackerModelOptions.DebugConfig.Log("[Sync] Skipped DungeonMaps update — no change");
                                    else if (!trackerIsInitialized())
                                        TrackerModelOptions.DebugConfig.Log($"[Sync] Skipped DungeonMaps update from {senderId} — TrackerModel not initialized");
                                    else if (payloadJson == lastDungeonMapsJson)
                                        TrackerModelOptions.DebugConfig.Log($"[Sync] Skipped DungeonMaps update from {senderId} — duplicate payload");
                                    else if (senderId == TrackerModelOptions.CoopSyncOptions.MyConsoleId)
                                        TrackerModelOptions.DebugConfig.Log("[Sync] Skipped DungeonMaps update — echo from self");
                                    else
                                    {
                                        lastDungeonMapsJson = payloadJson;
                                        CoopSync.dungeonMapsSyncOrigin.MarkSyncedNow();
                                        // Record that we've "sent" this payload so our own poller won't re-broadcast it
                                        CoopSync.lastSentDungeonMapsPayload = payloadJson;
                                        try
                                        {
                                            var dungeonModels = JsonConvert.DeserializeObject<DungeonSaveAndLoad.DungeonModel[]>(payloadJson);
                                            Dispatcher.UIThread.Invoke(() =>
                                            {
                                                CoopSync.isApplyingRoomFromSync = true;
                                                CoopSync.dungeonMapsSyncOrigin.MarkSyncStart();
                                                try
                                                {
                                                    for (int level = 0; level <= 8; level++)
                                                    {
                                                        var dm = dungeonModels[level];
                                                        // Skip if null or if RoomIsCircled/RoomStates are not fully populated (e.g. sender had uninitialized dungeon)
                                                        var isValid =
                                                            !(object.ReferenceEquals(dm, null)) &&
                                                            !(object.ReferenceEquals(dm.RoomIsCircled, null)) &&
                                                            dm.RoomIsCircled.Length == 8 &&
                                                            !(object.ReferenceEquals(dm.RoomStates, null)) &&
                                                            dm.RoomStates.Length == 8;
                                                        if (isValid)
                                                        {
                                                            try
                                                            {
                                                                DungeonUI.importFunctions[level](dm);
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to apply DungeonMaps for level {level}: {ex.Message}");
                                                            }
                                                        }
                                                    }
                                                    // Update lastSentTime AFTER import so that SetNow() calls during import
                                                    // don't produce a timestamp newer than lastSentTime (which would cause re-broadcast)
                                                    CoopSync.lastSentTime = DateTime.Now;
                                                    CoopSync.dungeonMapsSyncOrigin.MarkSyncedNow();
                                                    var hash = CoopSync.computeHash(payloadJson);
                                                    CoopSync.lastAppliedHashes["DungeonMaps"] = hash;
                                                    CoopSync.lastSentHashes["DungeonMaps"] = hash;
                                                }
                                                finally
                                                {
                                                    CoopSync.dungeonMapsSyncOrigin.MarkSyncEnd();
                                                    CoopSync.isApplyingRoomFromSync = false;
                                                }
                                            });
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"[Sync] Failed to apply DungeonMaps update: {ex.Message}");
                                        }
                                    }
                                    break;
                                case "HiddenDungeonColorLabel":
                                   try
                                   {
                                       if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null)
                                       && trackerIsInitialized())
                                       {
                                           var data = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(payloadJson);
                                           var i = data["Index"].ToObject<int>();
                                           var color = data["Color"].ToObject<int>();
                                           var labelChar = data["LabelChar"].ToObject<string>();
                                           Dispatcher.UIThread.Invoke(() =>
                                           {
                                               var dungeon = TrackerModel.GetDungeon(i);
                                               dungeon.Color = color;
                                               dungeon.LabelChar = !(string.IsNullOrEmpty(labelChar)) ? labelChar[0] : '?';
                                               TrackerModelOptions.DebugConfig.Log($"[Sync] Applied HiddenDungeonColorLabel update to dungeon {i} from {senderId}");
                                           });
                                       }
                                   }
                                   catch (Exception ex)
                                   {
                                       TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to apply HiddenDungeonColorLabel update: {ex.Message}");
                                   }
                                   break;
                                case "DoorChange":
                                    try
                                    {
                                        var data = JsonConvert.DeserializeObject<CoopSync.DoorChangePayload>(payloadJson);
                                        var key = $"DoorChange_{data.Level}_{data.X}_{data.Y}_{(data.IsHorizontal ? "true" : "false")}";
                                        if (trackerIsInitialized() && CoopSync.shouldApplyUpdate("DoorChange", payloadJson, timestamp) && !(SyncManager.ShouldSuppressDoorChange(payloadJson)) && (CoopSync.isDoorChangeFresh(data.Level, data.X, data.Y, data.IsHorizontal, timestamp)) && !(CoopSync.isRecentlySentByUs(key, 5000)))
                                        {
                                            CoopSync.recordDoorTimestamp(data.Level, data.X, data.Y, data.IsHorizontal, timestamp);
                                            Dispatcher.UIThread.Invoke(() =>
                                            {
                                                try
                                                {
                                                    var newState = Dungeon.DoorInterop.fromString(data.NewState.Case);
                                                    var door =
                                                        data.IsHorizontal
                                                            ? DungeonUI.getHorizontalDoor(data.Level, data.X, data.Y)
                                                            : DungeonUI.getVerticalDoor(data.Level, data.X, data.Y);

                                                    if (door.State != newState)
                                                    {
                                                        CoopSync.isApplyingRoomFromSync = true;
                                                        try
                                                        {
                                                            door.State = newState;
                                                        }
                                                        finally
                                                        {
                                                            CoopSync.isApplyingRoomFromSync = false;
                                                        }
                                                        TrackerModelOptions.DebugConfig.Log(
                                                            string.Format("[Sync] Applied DoorChange at L{0} {1} ({2},{3}) -> {4}",
                                                                data.Level,
                                                                (data.IsHorizontal ? "H" : "V"),
                                                                data.X,
                                                                data.Y,
                                                                data.NewState.Case)
                                                        );
                                                    }
                                                }
                                                catch (Exception ex2)
                                                {
                                                    TrackerModelOptions.DebugConfig.Log($"[Sync] DoorChange internal error: {ex2.Message}");
                                                }
                                            });
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to apply DoorChange update: {ex.Message}");
                                    }
                                    break;

                                case "RoomChange":
                                    try
                                    {
                                        TrackerModelOptions.DebugConfig.Log($"[Sync] Raw RoomChange payload: {payloadJson}");
                                        var data = JsonConvert.DeserializeObject<CoopSync.RoomChangePayload>(payloadJson);
                                        var key = $"DungeonRoom_{data.Level}_{data.X}_{data.Y}";
                                        if (CoopSync.shouldApplyUpdate("RoomChange", payloadJson, timestamp) && (CoopSync.isRoomChangeFresh(data.Level, data.X, data.Y, timestamp)) && !(CoopSync.isRecentlySentByUs(key, 5000)))
                                        {
                                            CoopSync.recordRoomTimestamp(data.Level, data.X, data.Y, timestamp);
                                            Dispatcher.UIThread.Invoke(() =>
                                            {
                                                CoopSync.isApplyingRoomFromSync = true;
                                                try
                                                {
                                                    Z1R_Tracker.Models.RoomSyncBridge.ApplyRoomChangeFromSync(
                                                        data.Level,
                                                        data.X,
                                                        data.Y,
                                                        data.IsComplete,
                                                        data.RoomType,
                                                        data.MonsterDetail,
                                                        data.FloorDropDetail,
                                                        data.FloorDropAppearsBright
                                                    );

                                                    switch (DungeonUI.redrawAllRoomsPublic)
                                                    {
                                                        case Action f:
                                                            TrackerModelOptions.DebugConfig.Log("[Sync] Calling redrawAllRoomsPublic after room sync");
                                                            f();
                                                            break;
                                                        case null:
                                                            TrackerModelOptions.DebugConfig.Log("[Sync] redrawAllRoomsPublic was None — cannot redraw");
                                                            break;
                                                    }
                                                }
                                                finally
                                                {
                                                    CoopSync.isApplyingRoomFromSync = false;
                                                }
                                            });
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to apply RoomChange update: {ex.Message}");
                                    }
                                    break;
                                case "OverworldTile":
                                    try
                                    {
                                        var data = JsonConvert.DeserializeObject<CoopSync.OverworldTileUpdate>(payloadJson);
                                        if (CoopSync.shouldApplyUpdate("OverworldTile", payloadJson, timestamp))
                                        {
                                            Dispatcher.UIThread.Invoke(() =>
                                            {
                                                TrackerModel.overworldMapMarks[data.X, data.Y].Set(data.MapTileValue);
                                                if (data.MapTileValue != -1)
                                                {
                                                    // Shop tiles store extra data under SHOP key; use the same key the sender used
                                                    var edKey = TrackerModel.MapSquareChoiceDomainHelper.IsItem(data.MapTileValue) ? TrackerModel.MapSquareChoiceDomainHelper.SHOP : data.MapTileValue;
                                                    TrackerModel.setOverworldMapExtraData(data.X, data.Y, edKey, data.ExtraData);
                                                }
                                                TrackerModel.overworldMapCircles[data.X, data.Y] = data.CircleValue;
                                                TrackerModelOptions.DebugConfig.Log($"[Sync] Applied OverworldTile ({data.X},{data.Y}) update from {senderId}");
                                            });
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to apply OverworldTile update: {ex.Message}");
                                    }
                                    break;

                                default:
                                    TrackerModelOptions.DebugConfig.Log($"[Sync] Unknown messageType: {msgType}");
                                    break;
                            }
                        }
                    }
                    else
                        TrackerModelOptions.DebugConfig.Log($"[Sync] Ignored sync message from senderId={senderId} (target={target}, me={me})");
                }
            }

            //let negotiateUrl = (TrackerModelOptions.CoopSyncOptions.NegotiateUrl())
            // THIS is the correct way to run async without causing the Async<'T> comparison error
            void tryStartSignalRConnection()
            {
                async Task go()
                {
                    try
                    {
                        await SyncManager.StartAsync(TrackerModelOptions.CoopSyncOptions.NegotiateUrl());
                        //CoopSync.subscribeToPlayerProgressChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
                        //CoopSync.subscribeToStartingItemsAndExtrasChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
                        //CoopSync.subscribeToItemsChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
                        // Tile-specific handler
                        var tileHandlerDelegate = new Action<string, string, string>((tileId, iconId, senderId) =>
                            handleRemoteTileChange(tileId, iconId, senderId)
                        );
                        SyncManager.SetTileChangeHandler(tileHandlerDelegate);

                        // General message handler (e.g. PlayerProgress)
                        var messageHandlerDelegate = new Action<string, string, string, long>((messageType, payloadJson, senderId, timestamp) =>
                            handleIncomingMessage(messageType, payloadJson, senderId, timestamp)
                        );
                        SyncManager.SetSyncMessageHandler(messageHandlerDelegate);


                        Console.WriteLine("[Sync] Connected to Azure SignalR via negotiate");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Sync] ERROR: {ex.Message}");
                        switch (ex)
                        {
                            case AggregateException agg:
                                foreach (var inner in agg.InnerExceptions)
                                    Console.WriteLine($"[Sync] Inner exception: {inner.Message}");
                                break;
                            default:
                                break;
                        }
                    }
                }
                CustomComboBoxes.StartImmediate(go());
            }
            if ((TrackerModelOptions.CoopSyncOptions.GetEnableCoop()))
                tryStartSignalRConnection();
            else
                Console.WriteLine("[Sync] Co-op disabled — not starting SignalR connection");
            TrackerModelOptions.CoopSyncOptions.EnableCoopChanged.Publish.AddHandler(new FSharpHandler<bool>((_, isEnabled) =>
            {
                if (isEnabled)
                {
                    Console.WriteLine("[Sync] Co-op enabled at runtime — connecting to Azure SignalR");
                    tryStartSignalRConnection();
                }
                else
                {
                    Console.WriteLine("[Sync] Co-op disabled at runtime — not connecting");
                    // Optional: implement disconnect logic here
                }
            }));


            var (wholeCanvas, hmsTimerCanvas) = (new Canvas(), new Canvas());
            this.Content = wholeCanvas;
            this.Loaded += (_, _) =>
            {
                Graphics.setup(this, wholeCanvas);
                this.Focus();
            };
            var drawingCanvasHolder = new Canvas();  // gets the app RenderTransform (can't RenderTransform the drawingCanvas itself without screwing up Broadcast Window)
            var drawingCanvas = new Canvas { IsHitTestVisible = false };
            drawingCanvasHolder.Children.Add(drawingCanvas);
            wholeCanvas.RenderTransformOrigin = RelativePoint.TopLeft;   // (WPF scales from the top-left corner; Avalonia's default is the centre)
            if (TrackerModelOptions.SmallerAppWindow.Value)
            {
                var trans = new ScaleTransform(TrackerModelOptions.SmallerAppWindowScaleFactor, TrackerModelOptions.SmallerAppWindowScaleFactor);
                wholeCanvas.RenderTransform = trans;
            }
            // Resizable window: scale the whole UI uniformly to fit the window (letterboxed if the shape differs; top-left anchored).
            // The scale is based on the size of the 'world' that the canvas manager currently reports, which changes when the timeline area appears.
            RenderOptions.SetBitmapInterpolationMode(wholeCanvas, Avalonia.Media.Imaging.BitmapInterpolationMode.None);   // keep the pixel-art icons crisp
            var saveScaleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600.0) };
            saveScaleTimer.Tick += (_, _) =>
            {
                saveScaleTimer.Stop();
                if (this.WindowState == WindowState.Normal && Graphics.dynamicAppScale > 0.0)
                {
                    TrackerModelOptions.MainWindowScale = Graphics.dynamicAppScale;
                    TrackerModelOptions.writeSettings();
                }
            };
            var appliedScale = 0.0;   // the scale the RenderTransform actually has (dynamicAppScale may be pre-set from saved settings before any transform exists)
            void applyWindowScale()
            {
                var w = this.ActualWidth - CHROME_WIDTH;
                var h = this.ActualHeight - CHROME_HEIGHT;
                if (w > 50.0 && h > 50.0 && cm.Height > 0.0)
                {
                    var s = Math.Min(w / WIDTH_SANS_CHROME, h / cm.Height);
                    s = Math.Max(0.4, Math.Min(4.0, s));
                    // when the window is exactly the size derived from the remembered scale (e.g. at startup, before the tracker layout exists and while the start screen's
                    // world height may differ), use the remembered scale itself, so the content fills the window
                    var ms = TrackerModelOptions.MainWindowScale;
                    s = (ms > 0.0 && Math.Abs(this.ActualWidth - Math.Round(WIDTH_SANS_CHROME*ms + CHROME_WIDTH)) < 2.0 && Math.Abs(this.ActualHeight - Math.Round(HEIGHT_SANS_CHROME*ms + CHROME_HEIGHT)) < 2.0) ? ms : s;
                    if (Math.Abs(s - appliedScale) > 0.0005)
                    {
                        appliedScale = s;
                        Graphics.dynamicAppScale = s;
                        wholeCanvas.RenderTransform = new ScaleTransform(s, s);
                        saveScaleTimer.Stop();   // remember the new size (as a scale) shortly after the user stops dragging
                        saveScaleTimer.Start();
                    }
                }
            }
            this.SizeChanged += (_, _) => applyWindowScale();
            this.Loaded += (_, _) => applyWindowScale();
            wholeCanvas.Children.Add(cm.RootCanvas);
            wholeCanvas.Children.Add(hmsTimerCanvas);
            wholeCanvas.Children.Add(drawingCanvasHolder);
            cm.AfterCreatePopupCanvas += _ => { drawingCanvas.Opacity = 0.0; hmsTimerCanvas.Opacity = 0.0; };   // the timer (or 'Nice timer bro') must not draw on top of popups
            cm.BeforeDismissPopupCanvas += _ => { if (cm.PopupCanvasStack.Count == 0) { drawingCanvas.Opacity = 1.0; hmsTimerCanvas.Opacity = 1.0; } };
            var mainDock = new DockPanel { Width = appMainCanvas.Width, Height = appMainCanvas.Height };
            ApplyKonamiCodeEasterEgg(cm, mainDock);
            appMainCanvas.Children.Add(mainDock);

            // (WPF: implicit styles added to a ResourceDictionary; Avalonia: styles in the element's Styles, which apply to its descendants)
            void addDarkTheme(StyledElement rd)
            {
                var style = new Style(x => x.OfType<TextBox>());
                style.Setters.Add(new Setter(TemplatedControl.ForegroundProperty, Brushes.Orange));
                style.Setters.Add(new Setter(TemplatedControl.BackgroundProperty, Brushes.Black));
                style.Setters.Add(new Setter(TemplatedControl.BorderBrushProperty, Brushes.Orange));
                rd.Styles.Add(style);
                style = new Style(x => x.OfType<Button>());
                Graphics.addRoundedButtonSetters(style);   // (rounded dark look, like every other button)
                rd.Styles.Add(style);
                style = new Style(x => x.OfType<ToolTip>());
                style.Setters.Add(new Setter(TemplatedControl.ForegroundProperty, Brushes.Orange));
                style.Setters.Add(new Setter(TemplatedControl.BackgroundProperty, Graphics.almostBlack));
                style.Setters.Add(new Setter(TemplatedControl.BorderBrushProperty, Brushes.DarkGray));
                rd.Styles.Add(style);
            }

            var mainStackPanel = new StackPanel { Orientation = Orientation.Vertical };
            var spacing = new Thickness(0.0, 8.0, 0.0, 0.0);
            var smallSpacing = new Thickness(0.0, 3.0, 0.0, 0.0);

            var startButtonHasBeenClicked = false;
            {
                Border menu(ManualResetEvent wh)
                {
                    TextBox mkTxt(string txt) => new TextBox { Text = txt, IsReadOnly = true, Margin = spacing, //TextAlignment=TextAlignment.Center, HorizontalAlignment=HorizontalAlignment.Center,
                                                                BorderThickness = new Thickness(0.0), FontSize = 16.0 };
                    var sp = new StackPanel { Orientation = Orientation.Vertical, Width = appMainCanvas.Width - 60.0, Margin = new Thickness(10.0) };
                    var title = new TextBox { Text = "Window Size", IsReadOnly = true, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                                              BorderThickness = new Thickness(0.0, 0.0, 0.0, 3.0), FontSize = 20.0 };
                    sp.Children.Add(title);
                    sp.Children.Add(mkTxt("ZORATracker can run in a few window sizes: '4/3 size', 'Default', '5/6 size', or '2/3 size'."));
                    var (MODE8, MODE6, MODE5, MODE4) = ("4/3 size (Largest)", "Default", "5/6 size", "2/3 size (Smallest)");
                    var curMode =
                        TrackerModelOptions.SmallerAppWindow.Value
                            ? (TrackerModelOptions.SmallerAppWindowScaleFactor == 2.0/3.0
                                ? MODE4
                                : TrackerModelOptions.SmallerAppWindowScaleFactor == 5.0/6.0
                                    ? MODE5
                                    : TrackerModelOptions.SmallerAppWindowScaleFactor == 4.0/3.0
                                        ? MODE8
                                        : string.Format(CultureInfo.InvariantCulture, "scale factor: {0:F6}", TrackerModelOptions.SmallerAppWindowScaleFactor))
                            : MODE6;
                    sp.Children.Add(mkTxt($"The current ZORATracker window size is '{curMode}'.  You can change the setting here:"));
                    sp.Children.Add(new DockPanel { Height = 20.0 });
                    var rb8 = new RadioButton { Content = new TextBox { Text = MODE8, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 16.0 }, Margin = new Thickness(20.0, 0.0, 0.0, 0.0) };
                    var rb6 = new RadioButton { Content = new TextBox { Text = MODE6, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 16.0 }, Margin = new Thickness(20.0, 0.0, 0.0, 0.0) };
                    var rb5 = new RadioButton { Content = new TextBox { Text = MODE5, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 16.0 }, Margin = new Thickness(20.0, 0.0, 0.0, 0.0) };
                    var rb4 = new RadioButton { Content = new TextBox { Text = MODE4, IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 16.0 }, Margin = new Thickness(20.0, 0.0, 0.0, 0.0) };
                    if (TrackerModelOptions.SmallerAppWindow.Value)
                    {
                        if (curMode == MODE5)
                            rb5.IsChecked = true;
                        else if (curMode == MODE8)
                            rb8.IsChecked = true;
                        else
                            rb4.IsChecked = true;
                    }
                    else
                        rb6.IsChecked = true;
                    var desireSmaller = TrackerModelOptions.SmallerAppWindow.Value;
                    var desireScale = 2.0/3.0;
                    Graphics.onChecked(rb8, () => { desireSmaller = true; desireScale = 4.0/3.0; });
                    Graphics.onChecked(rb6, () => { desireSmaller = false; });
                    Graphics.onChecked(rb5, () => { desireSmaller = true; desireScale = 5.0/6.0; });
                    Graphics.onChecked(rb4, () => { desireSmaller = true; desireScale = 2.0/3.0; });
                    var inner = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center };
                    inner.Children.Add(rb8);
                    inner.Children.Add(rb6);
                    inner.Children.Add(rb5);
                    inner.Children.Add(rb4);
                    sp.Children.Add(inner);

                    title = new TextBox { Text = "Window Shape", IsReadOnly = true, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                                          BorderThickness = new Thickness(0.0, 0.0, 0.0, 3.0), FontSize = 20.0, Margin = spacing };
                    sp.Children.Add(title);
                    sp.Children.Add(mkTxt("ZORATracker can run in two window shapes: 'Tall' (default) and 'Square' (smaller/shorter)."));
                    sp.Children.Add(mkTxt("The 'Square' option auto-swaps between Overworld and Dungeon focus, based on your mouse."));
                    sp.Children.Add(mkTxt("Note: This option only affects the main application; the startup options screen is always 'Tall'."));
                    sp.Children.Add(mkTxt("Note: 'Square' disables some features: overworld magnifier, broadcast window, Draw&UCC buttons."));
                    var desireShort = TrackerModelOptions.ShorterAppWindow.Value;
                    var tallC = new Canvas { Width = 77.0, Height = 99.0, Background = Brushes.Red };
                    var squareC = new Canvas { Width = 77.0, Height = 77.0, Background = Brushes.Red };
                    TextBox tx(string s) => new TextBox { Text = s, IsReadOnly = true, FontSize = 16.0, BorderThickness = new Thickness(0.0), Foreground = Brushes.Black, Background = Brushes.Transparent, FontWeight = FontWeight.Bold,
                                                          Width = 77.0, TextAlignment = TextAlignment.Center, IsHitTestVisible = false };
                    Graphics.canvasAdd(tallC, tx("Tall"), 0.0, 30.0);
                    Graphics.canvasAdd(squareC, tx("Square"), 0.0, 30.0);
                    var tallB = new Border { Child = tallC, BorderThickness = new Thickness(3.0) };
                    var squareB = new Border { Child = squareC, BorderThickness = new Thickness(3.0) };
                    void update()
                    {
                        if (desireShort)
                        {
                            tallB.BorderBrush = Brushes.Black;
                            squareB.BorderBrush = Brushes.Pink;
                        }
                        else
                        {
                            tallB.BorderBrush = Brushes.Pink;
                            squareB.BorderBrush = Brushes.Black;
                        }
                    }
                    update();
                    var rbTall   = new RadioButton { Content = tallB,   VerticalContentAlignment = VerticalAlignment.Center, IsChecked = !desireShort };
                    var rbSquare = new RadioButton { Content = squareB, VerticalContentAlignment = VerticalAlignment.Center, IsChecked = desireShort };
                    inner = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = spacing };
                    Graphics.onChecked(rbTall, () => { desireShort = false; update(); });
                    Graphics.onChecked(rbSquare, () => { desireShort = true; update(); });
                    inner.Children.Add(rbTall);
                    inner.Children.Add(new DockPanel { Width = 30.0 });  // spacer
                    inner.Children.Add(rbSquare);
                    sp.Children.Add(inner);

                    sp.Children.Add(new DockPanel { Background = Brushes.Gray, Margin = new Thickness(20.0, 8.0, 20.0, 0.0), Height = 4.0 });
                    var warn = mkTxt("Changes to these settings will only take effect after restarting the app:");
                    warn.FontSize = 20.0;
                    warn.FontWeight = FontWeight.Bold;
                    sp.Children.Add(warn);
                    var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = spacing };
                    Button makeButton(string head, string text)
                    {
                        var bsp = new StackPanel { Background = Graphics.almostBlack, Orientation = Orientation.Vertical };
                        var tb1 = new TextBox { Text = head, IsReadOnly = true, IsHitTestVisible = false, TextAlignment = TextAlignment.Center, BorderThickness = new Thickness(0.0),
                                                Background = Graphics.almostBlack, FontSize = 24.0, FontWeight = FontWeight.Bold };
                        var tb2 = new TextBox { Text = text, IsReadOnly = true, IsHitTestVisible = false, TextAlignment = TextAlignment.Center, BorderThickness = new Thickness(0.0),
                                                Background = Graphics.almostBlack, FontSize = 16.0 };
                        bsp.Children.Add(tb1);
                        bsp.Children.Add(tb2);
                        var button = new Button { Content = bsp, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, BorderThickness = new Thickness(3.0, 3.0, 3.0, 3.0) };
                        return button;
                    }
                    var sb = makeButton("Save changes", "Save changes and restart ZORATracker");
                    var cb = makeButton("Discard changes", "Don't make any changes\n(exit this popup menu)");
                    cb.Click += (_, _) => wh.Set();
                    sb.Click += (_, _) =>
                    {
                        TrackerModelOptions.SmallerAppWindow.Value = desireSmaller;
                        TrackerModelOptions.SmallerAppWindowScaleFactor = desireScale;
                        TrackerModelOptions.MainWindowScale = 0.0;   // an explicit choice of window size on the start screen replaces the remembered resized size
                        TrackerModelOptions.ShorterAppWindow.Value = desireShort;
                        TrackerModelOptions.writeSettings();
                        Graphics.RestartTheApplication();
                    };
                    buttons.Children.Add(sb);
                    buttons.Children.Add(new DockPanel { Width = 30.0 });  // spacing
                    buttons.Children.Add(cb);
                    sp.Children.Add(buttons);
                    addDarkTheme(sp);
                    return new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(3.0), Background = Brushes.Black, Child = sp };
                }

                var fs = TrackerModelOptions.SmallerAppWindow.Value ? 16.0 : 12.0;
                var topBar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var tb = new TextBox { Text = "You can now resize the window!", Margin = new Thickness(0.0),
                                       IsReadOnly = true, BorderThickness = new Thickness(0.0), FontSize = fs, VerticalAlignment = VerticalAlignment.Center };
                topBar.Children.Add(tb);
                var b = Graphics.makeButton("Click here for options", fs, null);
                b.Click += (_, _) =>
                {
                    if (startButtonHasBeenClicked) { } else
                    if (!popupIsActive)
                    {
                        popupIsActive = true;
                        var wh = new ManualResetEvent(false);
                        CustomComboBoxes.StartImmediate(CustomComboBoxes.DoModal(cm, wh, 20.0, 60.0, menu(wh)));
                        popupIsActive = false;
                    }
                };
                _ = b;   // (the 'Click here for options' button is no longer shown; the window can simply be resized)
                var curFactor = TrackerModelOptions.SmallerAppWindow.Value ? TrackerModelOptions.SmallerAppWindowScaleFactor : 1.0;
                // (WPF: SystemParameters.WorkArea, the primary screen's work area in WPF units)
                var primary = this.Screens?.Primary;   // (port: null in the browser build before the page shows it)
                var workAreaHeight = primary != null ? primary.WorkingArea.Height / (primary.Scaling > 0.0 ? primary.Scaling : 1.0) : double.MaxValue;
                var workingAreaTooSmallForCurrentHeight = workAreaHeight < (1000.0 * curFactor);
                var likelyDoesntFit = workingAreaTooSmallForCurrentHeight;  // 'smaller' now might mean larger    && not(TrackerModelOptions.SmallerAppWindow.Value)
                var barColor =
                    likelyDoesntFit
                        ? Graphics.freeze(new SolidColorBrush(Color.FromRgb(120, 30, 30)))   // reddish
                        : Graphics.freeze(new SolidColorBrush(Color.FromRgb(50, 50, 50)));   // grayish
                var dp = new DockPanel { Height = (curFactor < 1.0 ? 40.0 : 30.0), LastChildFill = true, Background = barColor };
                dp.Children.Add(topBar);
                _ = dp;   // (the "You can now resize the window!" bar is no longer shown)
            }

            var stackPanel = new StackPanel { Orientation = Orientation.Vertical, Width = WIDTH_SANS_CHROME };
            var startupOptionsCanvas = new Canvas();
            startupOptionsCanvas.Children.Add(stackPanel);
            mainStackPanel.Children.Add(startupOptionsCanvas);
            // (the version button now lives at the bottom right of the open space, just above the Settings line: see bottomSP below)

            var startupOptionsTB = new TextBox { Text = "Startup Options", IsReadOnly = true, FontWeight = FontWeight.Bold, FontSize = 16.0, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0), Margin = new Thickness(0.0, 8.0, 0.0, 0.0) };   // centered in the gap between the top bar and the checkbox row
            var hsPanel = new StackPanel { Margin = new Thickness(0.0), Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var hsGrid = Graphics.makeGrid(3, 3, 30, 30);
            hsGrid.Background = Brushes.Black;
            var triforcesNumbered = new List<Image>();
            var triforcesLettered = new List<Image>();
            for (int i = 0; i <= 2; i++)
            {
                var image = Graphics.BMPtoImage(Graphics.emptyUnfoundNumberedTriforce_bmps[i]);
                triforcesNumbered.Add(image);
                Graphics.gridAdd(hsGrid, image, i, 0);
                image = Graphics.BMPtoImage(Graphics.emptyUnfoundLetteredTriforce_bmps[i]);
                triforcesLettered.Add(image);
                Graphics.gridAdd(hsGrid, image, i, 0);
            }
            void turnHideDungeonNumbersOn()
            {
                foreach (var b in triforcesNumbered)
                    b.Opacity = 0.0;
                foreach (var b in triforcesLettered)
                    b.Opacity = 1.0;
            }
            void turnHideDungeonNumbersOff()
            {
                foreach (var b in triforcesNumbered)
                    b.Opacity = 1.0;
                foreach (var b in triforcesLettered)
                    b.Opacity = 0.0;
            }
            turnHideDungeonNumbersOff();
            var row1boxes = Enumerable.Range(0, 3).Select(_ => new TrackerModel.Box(TrackerModel.StairKind.Never, TrackerModel.BoxOwner.None)).ToArray();
            for (int i = 0; i <= 2; i++)
            {
                Graphics.gridAdd(hsGrid, Views.MakeBoxItem(cm, row1boxes[i]).Item1, i, 1);
                Graphics.gridAdd(hsGrid, Views.MakeBoxItem(cm, new TrackerModel.Box(TrackerModel.StairKind.Never, TrackerModel.BoxOwner.None)).Item1, i, 2);
            }
            void turnHeartShuffleOn() { foreach (var b in row1boxes) b.Set(-1, TrackerModel.PlayerHas.NO); }
            void turnHeartShuffleOff() { foreach (var b in row1boxes) b.Set(14, TrackerModel.PlayerHas.NO); }
            turnHeartShuffleOn();
            var cutoffCanvas = new Canvas { Width = 85.0, Height = 85.0, ClipToBounds = true, IsHitTestVisible = false };
            cutoffCanvas.Children.Add(hsGrid);
            var _border = new Border { BorderBrush = Brushes.DarkGray, BorderThickness = new Thickness(8.0, 8.0, 0.0, 0.0), Child = cutoffCanvas };

            var finalChildTipArea = new Canvas();
            var explainerGrid = new Grid { Width = WIDTH*5.0/6.0 };   // (height fits the text)
            var explainerBorder = new Border { Child = explainerGrid, BorderThickness = new Thickness(1.0), BorderBrush = Brushes.Orange, Opacity = 0.0, Background = Brushes.Black, IsHitTestVisible = false };   // (never intercepts the mouse: it overhangs the Settings area below)
            var explainerLine = new Shapes.Line { Stroke = Brushes.Orange, StrokeThickness = 1.0, Opacity = 0.0, IsHitTestVisible = false };
            void hoverExplain(Control fe, string desc)
            {
                if (!startButtonHasBeenClicked)
                {
                    var explanation = MakeTipTextBox(desc);
                    explanation.FontSize = 18.0;
                    explanation.Margin = new Thickness(20.0);
                    explainerGrid.Children.Clear();
                    explainerGrid.Children.Add(explanation);
                    explainerBorder.Opacity = 1.0;
                    if (!(finalChildTipArea.Children.Contains(explainerBorder)))
                    {
                        var (x, y) = (WIDTH/12.0 - CHROME_WIDTH/2.0, 10.0);
                        Graphics.canvasAdd(finalChildTipArea, explainerBorder, x, y);
                        explainerLine.StartPoint = new Point(x, y);
                        finalChildTipArea.Children.Add(explainerLine);
                    }
                    // (TransformToVisual, not TranslatePoint into the element: scaled-up controls like the checkboxes would otherwise throw the line off)
                    // (WPF: TransformToVisual throws when there's no common ancestor, and then falls back; Avalonia returns null)
                    var t = fe.TransformToVisual(finalChildTipArea);
                    Point q;
                    if (t.HasValue) q = new Point(0.0, fe.Bounds.Height / 2.0).Transform(t.Value);
                    else { var p = finalChildTipArea.TranslatePoint(new Point(), fe) ?? new Point(); q = new Point(-p.X, -p.Y); }
                    explainerLine.EndPoint = q;
                    explainerLine.Opacity = 1.0;
                }
            }
            void explainHide()
            {
                explainerBorder.Opacity = 0.0;
                explainerLine.Opacity = 0.0;
            }

            var checkboxSP = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = WIDTH, Margin = new Thickness(0.0, 4.0, 0.0, 0.0) };   // the three startup options sit side by side (same gap under the header as the ZORA Extras section)
            // Heart Shuffle: Off / Intra / Full, radio-style (index = TrackerModel.HeartShuffleMode: 0 Off, 1 Intra, 2 Full)
            CheckBox makeHsCheckBox(string text)
            {
                var c = new CheckBox { Content = new TextBox { Text = text, IsReadOnly = true, BorderThickness = new Thickness(0.0), IsHitTestVisible = false, Margin = new Thickness(2.0) }, Margin = new Thickness(0.0, 0.0, 10.0, 0.0) };
                Graphics.scaleUpCheckBoxBox(c, 1.66);
                return c;
            }
            var hsBoxes = new[] { makeHsCheckBox("Off"), makeHsCheckBox("Intra"), makeHsCheckBox("Full") };
            var hsSetting = false;
            void selectHeartMode(int m)
            {
                hsSetting = true;
                for (int k = 0; k <= 2; k++)
                {
                    var on = (k == m);
                    hsBoxes[k].IsChecked = on;
                }
                hsSetting = false;
                if (m == 0) turnHeartShuffleOff(); else turnHeartShuffleOn();
            }
            int heartModeSelected()
            {
                var k = Array.FindIndex(hsBoxes, c => c.IsChecked == true);
                return k >= 0 ? k : 0;
            }
            for (int k = 0; k <= 2; k++)
            {
                var kk = k;
                Graphics.onChecked(hsBoxes[k], () => { if (!hsSetting) selectHeartMode(kk); });
                Graphics.onUnchecked(hsBoxes[k], () => { if (!hsSetting) selectHeartMode(kk); });   // (clicking the selected one keeps it selected)
            }
            selectHeartMode(2);
            var hsTitle = new TextBox { Text = "Item Shuffle", IsReadOnly = true, BorderThickness = new Thickness(0.0), IsHitTestVisible = false, Margin = new Thickness(2.0), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center };
            var hsDivider = new Border { Height = 1.0, Background = Brushes.DarkGray, Margin = new Thickness(2.0, 2.0, 2.0, 4.0) };
            var hsRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var c in hsBoxes)
                hsRow.Children.Add(c);
            var hsStack = new StackPanel { Orientation = Orientation.Vertical };
            hsStack.Children.Add(hsTitle);
            hsStack.Children.Add(hsDivider);
            hsStack.Children.Add(hsRow);
            var hsArea = Layout.optionBorderStyle(new Border { BorderThickness = new Thickness(1.0), Child = hsStack });
            // the explainer under the checkboxes uses the same bullet text as the tooltips: all three while over the area, just the hovered one while over a checkbox
            var hsTexts = new[] { "• Dungeon Items\n      • Heart on floor of each dungeon", "• Items Anywhere, Hearts within Dungeons\n      • 1 heart per dungeon 1-8, may be in a stair", "• Items + Hearts\n      • Full Shuffle all hearts and items" };
            var hsAllText = "Item Shuffle:\n\n• Off: Dungeon Items\n• Intra: Items Anywhere, Hearts within Dungeons\n• Full: Items + Hearts\n\nHover an option for details.";
            var hsLast = -2;
            void hsUpdate()
            {
                var k = Array.FindIndex(hsBoxes, b => b.IsPointerOver);
                if (k != hsLast)
                {
                    hsLast = k;
                    if (k >= 0) hoverExplain(hsBoxes[k], hsTexts[k]); else hoverExplain(hsArea, hsAllText);   // (the pointer line ends at the hovered checkbox)
                }
            }
            hsArea.PointerEntered += (_, _) => { hsLast = -2; hsUpdate(); };
            hsArea.PointerMoved += (_, _) => hsUpdate();
            hsArea.PointerExited += (_, _) => { hsLast = -2; explainHide(); };
            // Mirrored Overworld checkbox, directly under Full Shuffle (same look); sets the same flag the old mirror icon toggled
            var mocb = new CheckBox { Content = new TextBox { Text = "Mirrored Overworld", IsReadOnly = true, BorderThickness = new Thickness(0.0), IsHitTestVisible = false, Margin = new Thickness(2.0) } };
            Graphics.scaleUpCheckBoxBox(mocb, 1.66);
            mocb.IsChecked = false;
            Graphics.onChecked(mocb, () => TrackerModel.MirrorOverworld = true);
            Graphics.onUnchecked(mocb, () => TrackerModel.MirrorOverworld = false);
            var moArea = Layout.optionBorderStyle(new Border { BorderThickness = new Thickness(1.0), Child = mocb, VerticalAlignment = VerticalAlignment.Top });
            moArea.PointerEntered += (_, _) => hoverExplain(moArea, "Flip the overworld map East<->West.");
            moArea.PointerExited += (_, _) => explainHide();
            hsArea.VerticalAlignment = VerticalAlignment.Top;
            checkboxSP.Children.Add(hsArea);
            // Legacy Mode (under Heart Shuffle): turns off every ZORA Extra and locks them; the wiring is below, once the extras exist
            var lmcb = new CheckBox { Content = new TextBox { Text = "Legacy Mode", IsReadOnly = true, BorderThickness = new Thickness(0.0), IsHitTestVisible = false, Margin = new Thickness(2.0) } };
            Graphics.scaleUpCheckBoxBox(lmcb, 1.66);
            lmcb.IsChecked = false;
            var lmArea = Layout.optionBorderStyle(new Border { BorderThickness = new Thickness(1.0), Child = lmcb, VerticalAlignment = VerticalAlignment.Top });
            lmArea.PointerEntered += (_, _) => hoverExplain(lmArea, "For non-ZORA seeds: turns off and locks the ZORA Extras and limits shops to the original game's items");
            lmArea.PointerExited += (_, _) => explainHide();
            checkboxSP.Children.Add(new DockPanel { Width = 10.0 });
            checkboxSP.Children.Add(lmArea);

            checkboxSP.Children.Add(new DockPanel { Width = 10.0 });

            var hdcb = new CheckBox { Content = new TextBox { Text = "Hide Dungeon Numbers", IsReadOnly = true, BorderThickness = new Thickness(0.0), IsHitTestVisible = false, Margin = new Thickness(2.0) } };
            Graphics.scaleUpCheckBoxBox(hdcb, 1.66);
            hdcb.IsChecked = false;
            Graphics.onChecked(hdcb, () => turnHideDungeonNumbersOn());
            Graphics.onUnchecked(hdcb, () => turnHideDungeonNumbersOff());
            var hdnArea = Layout.optionBorderStyle(new Border { BorderThickness = new Thickness(1.0), Child = hdcb, VerticalAlignment = VerticalAlignment.Top });   // top-aligned so it doesn't stretch to the height of the stacked columns
            hdnArea.PointerEntered += (_, _) => hoverExplain(hdnArea, "This checkbox corresponds to the randomizer setting:\n\nDungeons (tab)\n - Hide Dungeon Numbers (checkbox)");
            hdnArea.PointerExited += (_, _) => explainHide();
            // '4th Sword' option, directly under Hide Dungeon Numbers: checked = the 4th sword is the max sword level and the level 2 staircase box is enabled
            var fscb = new CheckBox { Content = new TextBox { Text = "4th Sword", IsReadOnly = true, BorderThickness = new Thickness(0.0), IsHitTestVisible = false }, Margin = new Thickness(2.0, 0.0, 2.0, 0.0) };
            Graphics.scaleUpCheckBoxBox(fscb, 1.66);
            fscb.IsChecked = false;
            var fsArea = Layout.optionBorderStyle(new Border { BorderThickness = new Thickness(1.0), Child = fscb, VerticalAlignment = VerticalAlignment.Top });
            fsArea.PointerEntered += (_, _) => hoverExplain(fsArea, "Checked: the 4th sword is the highest sword level, and you choose whether its item is in level 2 (the staircase item box is enabled) or in level 9 (a new floor item check is added there).\nUnchecked: the magical sword is the highest sword level, and the staircase box starts greyed out.");
            fsArea.PointerExited += (_, _) => explainHide();
            // next to '4th Sword' sit two radio-style checkboxes to choose where the 4th sword's item lives: the level 2 staircase slot (default), or a new level 9 floor item check
            var fourthSwordLevel = 2;
            CheckBox makeFsLevelCheckBox(string text)
            {
                var c = new CheckBox { Content = new TextBox { Text = text, IsReadOnly = true, BorderThickness = new Thickness(0.0), IsHitTestVisible = false }, Margin = new Thickness(10.0, 2.0, 2.0, 2.0) };
                Graphics.scaleUpCheckBoxBox(c, 1.66);
                return c;
            }
            var fsLevelBoxes = new[] { makeFsLevelCheckBox("Level 2"), makeFsLevelCheckBox("Level 9") };
            var fsLevelSetting = false;
            void setFourthLevel(int lvl)
            {
                fourthSwordLevel = lvl;
                fsLevelSetting = true;
                var is2 = (lvl == 2);
                var is9 = (lvl == 9);
                fsLevelBoxes[0].IsChecked = is2;
                fsLevelBoxes[1].IsChecked = is9;
                fsLevelSetting = false;
            }
            setFourthLevel(2);
            for (int k = 0; k <= 1; k++)
            {
                var lvl = k == 0 ? 2 : 9;
                Graphics.onChecked(fsLevelBoxes[k], () => { if (!fsLevelSetting) setFourthLevel(lvl); });
                Graphics.onUnchecked(fsLevelBoxes[k], () => { if (!fsLevelSetting) setFourthLevel(lvl); });   // (clicking the selected one keeps it selected)
            }
            void refreshFsLevelEnabled() { foreach (var c in fsLevelBoxes) c.IsEnabled = (fscb.IsChecked == true); }
            refreshFsLevelEnabled();
            Graphics.onChecked(fscb, () => { setFourthLevel(2); refreshFsLevelEnabled(); });
            Graphics.onUnchecked(fscb, () => { setFourthLevel(2); refreshFsLevelEnabled(); });
            fsArea.Child = null;   // (detach 4th Sword so it can move into the row)
            {
                // (Level 2 / Level 9 sit on their own row under 4th Sword: they don't fit beside it in a half-width card)
                var sp = new StackPanel { Orientation = Orientation.Vertical };
                sp.Children.Add(fscb);
                var lv = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(34.0, 0.0, 0.0, 0.0) };
                lv.Children.Add(fsLevelBoxes[0]);
                lv.Children.Add(fsLevelBoxes[1]);
                sp.Children.Add(lv);
                fsArea.Child = sp;
            }
            checkboxSP.Children.Add(hdnArea);
            checkboxSP.Children.Add(new DockPanel { Width = 10.0 });
            checkboxSP.Children.Add(moArea);   // Mirrored Overworld takes the third spot
            var mbcb = new CheckBox { Content = new TextBox { Text = "Mark Bomb Droppers", IsReadOnly = true, BorderThickness = new Thickness(0.0), IsHitTestVisible = false, Margin = new Thickness(2.0) } };
            Graphics.scaleUpCheckBoxBox(mbcb, 1.66);
            mbcb.IsChecked = false;
            var mbArea = Layout.optionBorderStyle(new Border { BorderThickness = new Thickness(1.0), Child = mbcb, VerticalAlignment = VerticalAlignment.Top });
            mbArea.PointerEntered += (_, _) => hoverExplain(mbArea, "Middle-click an overworld tile to mark it as a bomb dropper screen (enemies drop bombs).\nThen scroll the mouse wheel to cycle: bomb dropper > blue > yellow > red circle > bomb dropper...\nMiddle-click again to remove it. Shift+middle-click toggles a plain circle.\nHover the bomb shop highlight to see bomb dropper screens outlined in red.");
            mbArea.PointerExited += (_, _) => explainHide();
            checkboxSP.Children.Add(new DockPanel { Width = 10.0 });
            checkboxSP.Children.Add(mbArea);

            // custom level name (replaces 'LEVEL-n' at the top of each dungeon map)
            var clnTextBox = new TextBox { MaxLength = 7, IsEnabled = false, Margin = new Thickness(6.0, 2.0, 2.0, 2.0), VerticalAlignment = VerticalAlignment.Center };
            // (WPF: CharacterCasing = Upper. Avalonia's TextBox has no CharacterCasing, so typed text is upper-cased as it arrives, as DungeonUI does)
            clnTextBox.AddHandler(InputElement.TextInputEvent, new EventHandler<TextInputEventArgs>((_, tea) => { if (tea.Text != null) tea.Text = tea.Text.ToUpper(); }), RoutingStrategies.Tunnel);
            var clncb = new CheckBox { Content = new TextBox { Text = "Custom LEVEL Name", IsReadOnly = true, BorderThickness = new Thickness(0.0), IsHitTestVisible = false, Margin = new Thickness(2.0) } };
            Graphics.scaleUpCheckBoxBox(clncb, 1.66);
            var clnTip = "Only works with ZORA seeds. In a non-ZORA seed, the custom name\nwould replace 'LEVEL-n' and could be confusing, so leave this off.";
            ToolTip.SetTip(clncb, clnTip);
            ToolTip.SetTip(clnTextBox, clnTip);
            clncb.IsChecked = false;
            Graphics.onChecked(clncb, () => { clnTextBox.IsEnabled = true; clnTextBox.Focus(); });
            Graphics.onUnchecked(clncb, () => clnTextBox.IsEnabled = false);
            var clnArea = Layout.optionBorderStyle(new Border { BorderThickness = new Thickness(1.0), Child = clncb });
            clnArea.PointerEntered += (_, _) => hoverExplain(clnArea, "Type up to 7 letters/numbers to use in place of\n'LEVEL-1' at the top of each dungeon map.\n(Fewer than 7 keeps the dash; a 7th character replaces it.)");
            clnArea.PointerExited += (_, _) => explainHide();
            // the name box sits on its own row under the checkbox (in the Startup Options card), lined up with the checkbox's words; greyed out until the box is checked
            clnTextBox.Margin = new Thickness(36.0, 0.0, 0.0, 4.0);
            clnTextBox.Width = double.NaN;
            clnTextBox.Height = 34.0;
            clnTextBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            clnTextBox.VerticalAlignment = VerticalAlignment.Center;
            clnTextBox.HorizontalContentAlignment = HorizontalAlignment.Center;   // text starts from the middle of the box
            clnTextBox.TextAlignment = TextAlignment.Center;   // (Avalonia: the TextBox centres its text by TextAlignment)
            clnTextBox.VerticalContentAlignment = VerticalAlignment.Center;
            clnTextBox.FontSize = 18.0;
            Graphics.styleRoundedTextBox(clnTextBox);
            // (WPF: CharacterCasing.Upper; Avalonia 11 has no such property, so typed or pasted text is upper-cased here)
            clnTextBox.TextChanged += (_, _) => { var t = clnTextBox.Text; if (t != null && t != t.ToUpperInvariant()) { var c = clnTextBox.CaretIndex; clnTextBox.Text = t.ToUpperInvariant(); clnTextBox.CaretIndex = c; } };
            // 'ZORA Extras' section below the main startup options: 4th Sword and the custom level name
            var extrasHeaderText = new TextBox { Text = "ZORA Extras", IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontWeight = FontWeight.Bold, FontSize = 16.0, VerticalAlignment = VerticalAlignment.Center };
            // checkbox beside the header: checks / unchecks every ZORA Extras option below it at once (it shows a partial state when only some are checked)
            var allExtrasCB = new CheckBox { Content = new TextBox { Text = "", IsReadOnly = true, BorderThickness = new Thickness(0.0), IsHitTestVisible = false, Width = 0.0 }, Margin = new Thickness(0.0, 0.0, 8.0, 0.0), VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(allExtrasCB, "Check or uncheck every ZORA Extras option below at once");
            Graphics.scaleUpCheckBoxBox(allExtrasCB, 1.4);
            allExtrasCB.IsChecked = false;
            var extrasHeader = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0.0, 12.0, 0.0, 0.0) };
            // (the master checkbox is no longer shown beside the header)
            extrasHeader.Children.Add(extrasHeaderText);
            var extrasSP = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = WIDTH, Margin = new Thickness(0.0, 4.0, 0.0, 0.0) };
            extrasSP.Children.Add(fsArea);
            // 'Boots Replace Boomerangs' (what it does is still to be worked out; for now it just records the choice)
            var brcb = new CheckBox { Content = new TextBox { Text = "Boots Replace Boomerangs", IsReadOnly = true, BorderThickness = new Thickness(0.0), IsHitTestVisible = false }, Margin = new Thickness(2.0, 0.0, 2.0, 0.0) };
            Graphics.scaleUpCheckBoxBox(brcb, 1.66);
            brcb.IsChecked = false;
            brcb.IsThreeState = true;   // Off, On, or Random (a dash): the flags can leave boots to chance
            var brArea = Layout.optionBorderStyle(new Border { BorderThickness = new Thickness(1.0), Child = brcb, VerticalAlignment = VerticalAlignment.Top });
            brArea.PointerEntered += (_, _) => hoverExplain(brArea, "Boots replace the boomerangs.\n\nClick again for Random (a ?): the flags left it to chance. The tracker keeps the boomerangs until you turn boots on in Overworld + Extras, once you know.");
            brArea.PointerExited += (_, _) => explainHide();
            extrasSP.Children.Add(new DockPanel { Width = 10.0 });
            extrasSP.Children.Add(brArea);
            // 'Extra PB Blocks' / 'Extra Raft Blocks': more overworld tiles that count as blocked by the power bracelet / raft
            CheckBox makeExtraBlocksCheckBox(string label, string explain)
            {
                var cb = new CheckBox { Content = new TextBox { Text = label, IsReadOnly = true, BorderThickness = new Thickness(0.0), IsHitTestVisible = false }, Margin = new Thickness(2.0, 0.0, 2.0, 0.0) };
                Graphics.scaleUpCheckBoxBox(cb, 1.66);
                cb.IsChecked = false;
                var area = Layout.optionBorderStyle(new Border { BorderThickness = new Thickness(1.0), Child = cb, VerticalAlignment = VerticalAlignment.Top });
                area.PointerEntered += (_, _) => hoverExplain(area, explain);
                area.PointerExited += (_, _) => explainHide();
                extrasSP.Children.Add(new DockPanel { Width = 10.0 });
                extrasSP.Children.Add(area);
                return cb;
            }
            // these four can also be Random on the ZORA site (the seed decides): a third click shows a ?, and the tracker then treats the option as On, so every mark is available
            var randomNote = "\n\nClick again for Random (a ?): the flags left it to chance, so the tracker treats it as On.";
            var epbcb = makeExtraBlocksCheckBox("Extra Power Bracelet Blocks", "These overworld tiles are also blocked by the power bracelet:\nA1, A2, A3, A4, B1, B2, B3, B4" + randomNote);
            var erbcb = makeExtraBlocksCheckBox("Extra Raft Blocks", "These overworld tiles are also blocked by the raft:\nD5, E5, A15, A16, B15, B16" + randomNote);
            var dwcb = makeExtraBlocksCheckBox("Randomize Dead Woods", "These overworld tiles are locked by the Dead Woods hint:\nH1, H2, H3\n(hovering one highlights the Dead Woods button)" + randomNote);
            var lhcb = makeExtraBlocksCheckBox("Randomize Lost Hills", "These overworld tiles are locked by the Lost Hills hint:\nA12, A13, A14\n(hovering one highlights the Lost Hills button)" + randomNote);
            foreach (var cb in new[] { epbcb, erbcb, dwcb, lhcb }) cb.IsThreeState = true;
            var pricb = makeExtraBlocksCheckBox("Progressive Items", "Blue candle, blue ring, wood arrows and swords become progressive:\ncollecting the base item upgrades the others of it marked on the map\nblue candle > red candle, blue ring > red ring, wood arrow > silver arrow,\nwood sword > white > magical > 4th sword\n\nRequired by the 4th Sword: it is turned on, and held on, while 4th Sword is checked.");
            // the 'ZORA Extras' master checkbox
            var extrasCheckBoxes = new List<CheckBox> { fscb, clncb, brcb, epbcb, erbcb, dwcb, lhcb, pricb };
            var settingExtras = false;
            bool isOn(CheckBox cb) => cb.IsChecked == true;
            // the 4th Sword needs Progressive Items (the other way round is fine): checking 4th Sword checks it, and it can't be unchecked while 4th Sword is on
            Graphics.onChecked(fscb, () => pricb.IsChecked = true);
            Graphics.onUnchecked(pricb, () => { if (isOn(fscb)) pricb.IsChecked = true; });
            void syncMaster()
            {
                if (!settingExtras)
                {
                    var n = extrasCheckBoxes.Where(isOn).Count();
                    settingExtras = true;
                    allExtrasCB.IsChecked = (n == 0 ? false : n == extrasCheckBoxes.Count ? true : (bool?)null);
                    settingExtras = false;
                }
            }
            foreach (var cb in extrasCheckBoxes)
            {
                Graphics.onChecked(cb, () => syncMaster());
                Graphics.onUnchecked(cb, () => syncMaster());
            }
            allExtrasCB.Click += (_, _) =>
            {
                var want = isOn(allExtrasCB);
                settingExtras = true;
                foreach (var cb in extrasCheckBoxes)
                    cb.IsChecked = want;
                if (want) setFourthLevel(2);   // checking everything puts the 4th Sword in level 2
                settingExtras = false;
                syncMaster();
            };
            void applyLegacyMode()
            {
                var on = isOn(lmcb);
                settingExtras = true;
                foreach (var cb in extrasCheckBoxes)
                {
                    if (on) cb.IsChecked = false;
                    cb.IsEnabled = !on;
                }
                if (on) allExtrasCB.IsChecked = false;
                allExtrasCB.IsEnabled = !on;
                settingExtras = false;
                refreshFsLevelEnabled();
            }
            Graphics.onChecked(lmcb, () => applyLegacyMode());
            Graphics.onUnchecked(lmcb, () => applyLegacyMode());
            // 'Remember startup selection' (Other options): bring back the checkboxes that were checked when the last game was started
            var rememberedCheckBoxes = new List<(string, CheckBox)> {
                ("legacy", lmcb), ("hdn", hdcb), ("mirror", mocb), ("bomb", mbcb), ("4th", fscb), ("cln", clncb), ("boots", brcb), ("epb", epbcb), ("erb", erbcb), ("dw", dwcb), ("lh", lhcb), ("prog", pricb) };
            if (TrackerModelOptions.RememberStartupSelection.Value)
            {
                var saved = new HashSet<string>(TrackerModelOptions.SavedStartupSelection.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                if (saved.Count > 0)
                {
                    foreach (var (key, cb) in rememberedCheckBoxes)
                    {
                        if (cb.IsThreeState && saved.Contains(key + "?")) cb.IsChecked = null;
                        else cb.IsChecked = saved.Contains(key);
                    }
                    selectHeartMode(saved.Contains("full") ? 2 : saved.Contains("hsintra") ? 1 : 0);
                    setFourthLevel(saved.Contains("4th") && saved.Contains("4th9") ? 9 : 2);
                    clnTextBox.Text = TrackerModelOptions.SavedCustomLevelName;
                }
            }
            // two columns: Startup Options (with Load Save State under it) on the left, ZORA Extras on the right; plain checkboxes and text, no boxes around them
            void plain(Border bd)
            {
                bd.Classes.Remove("optionBorder");   // (WPF: bd.Style <- null; see Layout.optionBorderStyle)
                bd.BorderThickness = new Thickness(0.0);
                bd.Background = Brushes.Transparent;   // (still catches the mouse, for the hover explainers)
            }
            plain(clnArea);
            StackPanel plainRows(WrapPanel panel)
            {
                var items = new List<Control>();
                foreach (var c in panel.Children)
                {
                    switch (c)
                    {
                        case DockPanel: break;   // (the little spacers)
                        case Control fe: items.Add(fe); break;
                        default: break;
                    }
                }
                panel.Children.Clear();
                var sp = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0.0, 4.0, 0.0, 0.0) };   // the checkbox block is centered under its header, over the quest button
                foreach (var fe in items)
                {
                    if (fe is Border bd) plain(bd);
                    fe.HorizontalAlignment = HorizontalAlignment.Left;
                    fe.Margin = new Thickness(0.0, 0.0, 0.0, 4.0);
                    sp.Children.Add(fe);
                }
                return sp;
            }
            var leftCol = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0.0) };   // its left edge lines up with the left edge of the '1st Quest Overworld' button
            startupOptionsTB.Margin = new Thickness(0.0);
            startupOptionsTB.HorizontalAlignment = HorizontalAlignment.Center;   // centered over the quest button below it
            leftCol.Children.Add(startupOptionsTB);
            leftCol.Children.Add(new Border { Height = 1.0, Background = Brushes.Gray, Margin = new Thickness(0.0, 2.0, 0.0, 4.0), HorizontalAlignment = HorizontalAlignment.Stretch });   // line under the header
            // every button and text box on the start screen is this tall
            var startBoxHeight = 38.0;
            // Custom LEVEL Name lives here: its checkbox is in the same block as the checkboxes above it (so it lines up with them), a blank row above it; its name box is docked at the bottom of the card (see below), level with Paste Flags
            var startupRows = plainRows(checkboxSP);
            startupRows.Children.Add(new Border { Height = 34.0 });
            clnArea.HorizontalAlignment = HorizontalAlignment.Left;
            clnArea.Margin = new Thickness(0.0, 0.0, 0.0, 4.0);
            startupRows.Children.Add(clnArea);
            clnTextBox.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
            clnTextBox.Height = startBoxHeight;
            clnTextBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            leftCol.Children.Add(startupRows);
            var rightCol = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(3.0, 0.0, 0.0, 0.0) };   // lines up with the left edge of the 'Mixed - 1st Quest Overworld' button (the quest buttons have a 3px margin)
            extrasHeader.Margin = new Thickness(0.0);
            extrasHeader.HorizontalAlignment = HorizontalAlignment.Center;   // centered over the quest button below it
            rightCol.Children.Add(extrasHeader);
            rightCol.Children.Add(new Border { Height = 1.0, Background = Brushes.Gray, Margin = new Thickness(0.0, 2.0, 0.0, 4.0), HorizontalAlignment = HorizontalAlignment.Stretch });   // line under the header
            rightCol.Children.Add(plainRows(extrasSP));
            // the start screen's blocks are bordered cards of one width, a set distance apart
            var startCardsWidth = WIDTH_SANS_CHROME - 32.0;
            var startCardsGap = 80.0;
            Border startCard(Control child) => new Border { Child = child, BorderBrush = Brushes.SaddleBrown, BorderThickness = new Thickness(1.0), CornerRadius = new CornerRadius(6.0), Padding = new Thickness(14.0, 10.0, 14.0, 12.0) };
            var leftCard = startCard(leftCol);
            leftCard.Margin = new Thickness(0.0, 0.0, 11.0, 0.0);
            var rightCard = startCard(rightCol);
            rightCard.Margin = new Thickness(11.0, 0.0, 0.0, 0.0);
            var twoCols = new Grid { Width = startCardsWidth, HorizontalAlignment = HorizontalAlignment.Center };
            twoCols.ColumnDefinitions.Add(new ColumnDefinition(1.0, GridUnitType.Star));
            twoCols.ColumnDefinitions.Add(new ColumnDefinition(1.0, GridUnitType.Star));
            Grid.SetColumn(leftCard, 0);
            Grid.SetColumn(rightCard, 1);
            twoCols.Children.Add(leftCard);
            twoCols.Children.Add(rightCard);
            hsPanel.Children.Add(twoCols);
            // (the little dungeon preview graphic is no longer shown; its hidden state is still driven by the checkboxes)
            stackPanel.Children.Add(hsPanel);

            var topSpacer = new DockPanel { Height = 10.0 };   // height adjusted after first layout, to vertically center the Start buttons in the open space between the divider and the Settings line

            var ctxt = SynchronizationContext.Current;
            async Task doStartup(int n, DungeonSaveAndLoad.AllData loadData)   // (loadData: null for None)
            {
                // remember which startup checkboxes were checked (if the user asked for that)
                if (loadData == null && TrackerModelOptions.RememberStartupSelection.Value)
                {
                    var keys = new List<string>();
                    foreach (var (key, cb) in rememberedCheckBoxes)
                    {
                        if (cb.IsChecked == true) keys.Add(key);
                        else if (cb.IsThreeState && !cb.IsChecked.HasValue) keys.Add(key + "?");   // ("epb?" and so on: Random)
                    }
                    if (keys.Contains("4th") && fourthSwordLevel == 9) keys.Add("4th9");
                    switch (heartModeSelected()) { case 2: keys.Add("full"); break; case 1: keys.Add("hsintra"); break; default: break; }
                    TrackerModelOptions.SavedStartupSelection = string.Join(",", keys);
                    TrackerModelOptions.SavedCustomLevelName = clnTextBox.Text.Trim().ToUpperInvariant();
                    TrackerModelOptions.writeSettings();
                }
                // loadData takes precedence over user selections
                // (a loaded save restores whether it was Legacy Mode; older saves do not say, so they follow the start screen checkbox)
                var legacyMode =
                    (loadData != null && !(loadData.Overworld == null) && loadData.Overworld.LegacyMode >= 0)
                        ? loadData.Overworld.LegacyMode == 1
                        : isOn(lmcb);
                TrackerModel.LegacyMode = legacyMode;
                // (a loaded save restores its Heart Shuffle mode; older saves do not say, so they follow the start screen)
                var heartMode =
                    (loadData != null && !(loadData.Overworld == null) && loadData.Overworld.HeartShuffleMode >= 0)
                        ? loadData.Overworld.HeartShuffleMode
                        : heartModeSelected();
                TrackerModel.HeartShuffleMode = heartMode;
                var heartShuffle = loadData != null || heartMode != 0;
                var kind =
                    loadData != null
                        ? (loadData.Items.HiddenDungeonNumbers
                            ? TrackerModel.DungeonTrackerInstanceKind.HIDE_DUNGEON_NUMBERS
                            : TrackerModel.DungeonTrackerInstanceKind.DEFAULT)
                        : (hdcb.IsChecked == true
                            ? TrackerModel.DungeonTrackerInstanceKind.HIDE_DUNGEON_NUMBERS
                            : TrackerModel.DungeonTrackerInstanceKind.DEFAULT);
                if (loadData != null)
                    TrackerModel.IsSecondQuestDungeons = loadData.Items.SecondQuestDungeons;

                if (loadData == null)   // (a loaded save restores its own 4th-sword/staircase state)
                {
                    var fourth = fscb.IsChecked == true;
                    TrackerModelOptions.FourthSwordEnabled = fourth;
                    var inLevel9 = fourth && fourthSwordLevel == 9;
                    TrackerModel.SetLevel2StairBoxEnabled(fourth && !inLevel9);   // Level 2: the staircase item slot is enabled; Level 9: it is not, and level 9 gets a floor item check instead
                    TrackerModel.SetLevel9FloorBoxEnabled(inLevel9);
                    // with no ZORA Extras selected (any quest), the letter and magical sword item boxes start out holding their (uncollected) items
                    if (extrasCheckBoxes.All(cb => cb.IsChecked == false))   // (a Random dash counts as selected)
                    {
                        TrackerModel.letterBox.Set(TrackerModel.ITEMS.LETTER, TrackerModel.PlayerHas.NO);
                        TrackerModel.sword3Box.Set(TrackerModel.ITEMS.MAGICALSWORD, TrackerModel.PlayerHas.NO);
                    }
                }
                TrackerModel.BootsReplaceBoomerangs = brcb.IsChecked == true;
                TrackerModel.BootsRandom = loadData == null && !brcb.IsChecked.HasValue;   // (a Random dash counts as Off until confirmed in Overworld + Extras)
                OverworldData.IsMirroredFunc = FuncConvert.FromFunc(() => TrackerModel.MirrorOverworld);
                // (Random, the dash, counts as On: the option may be in play, so every mark has to be available)
                bool onOrRandom(CheckBox cb) => cb.IsChecked != false;
                OverworldData.ExtraPBBlocks = onOrRandom(epbcb);
                OverworldData.ExtraRaftBlocks = onOrRandom(erbcb);
                OverworldData.DeadWoodsEnabled = onOrRandom(dwcb);
                OverworldData.LostHillsEnabled = onOrRandom(lhcb);
                bool isRandom(CheckBox cb) => loadData == null && !cb.IsChecked.HasValue;
                OverworldData.ExtraPBRandom = isRandom(epbcb);
                OverworldData.ExtraRaftRandom = isRandom(erbcb);
                OverworldData.DeadWoodsRandom = isRandom(dwcb);
                OverworldData.LostHillsRandom = isRandom(lhcb);
                OverworldData.DeadWoodsHintKnown = false;
                OverworldData.LostHillsHintKnown = false;
                TrackerModel.MarkBombDroppers = mbcb.IsChecked == true;
                TrackerModel.ProgressiveItems = pricb.IsChecked == true;
                // a loaded save brings back its own Boots (Off / On / Random), Progressive Items, extra blocks (with their Random ? states) and hint letters; older saves lack some of them and keep the start screen's choice
                if (loadData != null) SaveAndLoad.ApplyLoadedOptions(loadData.Overworld);
                // with Progressive Items there can be up to 4 progressive swords (P1-P4) and 2 of each base ring / candle / arrow (the second one becomes the upgrade; boomerangs / boots work the same way)
                foreach (var (id, maxUse) in new[] { (TrackerModel.ITEMS.PROGRESSIVESWORD, (TrackerModel.ProgressiveItems ? 4 : 3)), (TrackerModel.ITEMS.BLUERING, (TrackerModel.ProgressiveItems ? 2 : 1)), (TrackerModel.ITEMS.BLUECANDLE, (TrackerModel.ProgressiveItems ? 2 : 1)), (TrackerModel.ITEMS.WOODARROW, (TrackerModel.ProgressiveItems ? 2 : 1)), (TrackerModel.ITEMS.BOOMERANG, (TrackerModel.ProgressiveItems ? 2 : 1)) }) TrackerModel.allItemWithHeartShuffleChoiceDomain.SetMaxUse(id, maxUse);
                TrackerModel.CustomLevelName =
                    clncb.IsChecked == true ? clnTextBox.Text.Trim().ToUpperInvariant() : "";
                SpeechRecognition.SpeechRecognitionInstance speechRecognitionInstance = null;
                if (TrackerModelOptions.ListenForSpeech.Value)
                {
                    Console.WriteLine("Initializing microphone for speech recognition...");
                    try
                    {
                        speechRecognitionInstance = new SpeechRecognition.SpeechRecognitionInstance(kind);
                        SpeechRecognition.speechRecognizer.SetInputToDefaultAudioDevice();
                        SpeechRecognition.speechRecognizer.RecognizeAsyncMultiple();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("An exception setting up speech, speech recognition will be non-functional, but rest of app will work. Exception:");
                        Console.WriteLine($"{ex.ToString()}");
                        Console.WriteLine("");
                        OptionsMenu.microphoneFailedToInitialize = true;
                    }
                }
                else
                {
                    Console.WriteLine("Speech recognition will be disabled");
                    OptionsMenu.microphoneFailedToInitialize = true;
                }

                var loadingText = new StringBuilder("Loading UI...");
                var loadingTB = new TextBox { Text = loadingText.ToString(), IsReadOnly = true, Margin = spacing, Padding = new Thickness(5.0), MaxWidth = WIDTH/2.0 };
                stackPanel.Children.Add(loadingTB);
                var totalsw = Stopwatch.StartNew();
                var sw = Stopwatch.StartNew();
                var printStartupTimeDiagnostics = false;
                void displayStartupTimeDiagnostics(string s) { if (printStartupTimeDiagnostics) Console.WriteLine(s); }  // for debugging startup perf
                async Task showProgress(string label)
                {
                    loadingText.Append('.');
                    loadingTB.Text = loadingText.ToString();
                    await Task.Delay(1); // pump to make 'Loading UI' text update
                    await switchToContext(ctxt);
                    displayStartupTimeDiagnostics($"prev took {sw.ElapsedMilliseconds}ms");
                    displayStartupTimeDiagnostics(label);
                    sw.Restart();
                }
                // move mainDock to topmost while app is built behind it
                mainDock.ZIndex = 9999;
                await showProgress("start");
                if (loadData != null)
                    lastUpdateMinute = (loadData.TimeInSeconds / 60);
                var u = await WPFUI.makeAll(this, cm, drawingCanvas, n, heartShuffle, kind, loadData, showProgress, speechRecognitionInstance);
                updateTimeline = u;
                if (legacyMode)   // Legacy Mode: the letter and magical sword stay in their boxes (they can be collected / uncollected, not removed)
                {
                    if (TrackerModel.letterBox.CellCurrent() == TrackerModel.ITEMS.LETTER) TrackerModel.letterBox.LockedItem = TrackerModel.ITEMS.LETTER;
                    if (TrackerModel.sword3Box.CellCurrent() == TrackerModel.ITEMS.MAGICALSWORD) TrackerModel.sword3Box.LockedItem = TrackerModel.ITEMS.MAGICALSWORD;
                }
                displayStartupTimeDiagnostics($"total startup took {totalsw.ElapsedMilliseconds}ms");
                appMainCanvas.Children.Remove(mainDock);  // remove for good
                HotKeys.InitializeWindow(this, OverworldItemGridUI.notesTextBox);
                WPFUI.resetTimerEvent.Fired += () => { lastUpdateMinute = 0; lastUpdateSecond = 0; updateTimeline(0); this.SetStartTimeToNow(); };
                if (loadData == null)
                    WPFUI.resetTimerEvent.Trigger();  // takes a few seconds to load everything, reset timer at start
                // the timer sits centered in the right column of the control panel, between its TIMER heading and the Pause/Reset button
                var tbox = OverworldItemGridUI.hmsTimeTextBox;
                tbox.Width = Layout.PANEL_RW + 6.0; tbox.Height = 44.0; tbox.FontSize = 32.0; tbox.TextAlignment = TextAlignment.Center; tbox.VerticalContentAlignment = VerticalAlignment.Center; tbox.Padding = new Thickness(0.0);
                Graphics.canvasAdd(hmsTimerCanvas, tbox, Layout.PANEL_X1 - 10.0, 8.0);   // (centered in the open space between the panel divider and the window edge)   // (the text sits low in its box, so the box starts a little above the TIMER heading's bottom edge to look centered)
                Graphics.canvasAdd(hmsTimerCanvas, OverworldItemGridUI.niceTimerBox, Layout.PANEL_X1 - 13.0, 0.0);   // 'Nice timer bro' shows here while the timer is hidden
                OverworldItemGridUI.niceTimerBox.Opacity = (OverworldItemGridUI.SHOW_NICE_TIMER_BRO && TrackerModelOptions.HideTimer.Value ? 1.0 : 0.0);   // (WPF: Visibility Visible / Hidden)
                // Race Mode notice: once the app is up, tell the user what Race Mode does (unless they ticked "Do not show this again")
                if (TrackerModelOptions.RaceMode.Value && !TrackerModelOptions.RaceModeNoticeHidden.Value)
                {
                    void showRaceNotice() => WPFUI.showRaceModeNotice(this);
                    var noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400.0) };   // (one-shot: lets startup finish first)
                    noticeTimer.Tick += (_, _) => { noticeTimer.Stop(); showRaceNotice(); };
                    noticeTimer.Start();
                }
                gotThruStartup = true;
                if (promptedCrashRecovery)
                    finishCrashInfoImpl("prompted for crash recovery, user chose not to, successfully started");
            }

            this.Closed += (_, _) =>  // still does not handle 'rude' shutdown, like if they close the console window
            {
                Popouts.theMainWindowHasClosed = true;
                if (settingsWereSuccessfullyRead)      // don't overwrite an unreadable file, the user may have been intentionally hand-editing it and needs feedback
                    TrackerModelOptions.writeSettings();  // save any settings changes they made before closing the startup window
            };
            // recently loaded save files (most recent first), remembered next to the program so they can be re-opened with one click
            var recentLoadsFile = Platform.Files.User("Z1R_Tracker_recent_loads.txt");
            var maxRecentLoads = 5;
            List<string> readRecentLoads()
            {
                try
                {
                    if (File.Exists(recentLoadsFile))
                        return File.ReadAllLines(recentLoadsFile)
                            .Where(p => p.Trim() != "" && File.Exists(p))
                            .Distinct()
                            .Take(maxRecentLoads)
                            .ToList();
                    else return new List<string>();
                }
                catch (Exception) { return new List<string>(); }
            }
            void recordRecentLoad(string path)
            {
                try
                {
                    var updated = new[] { path }.Concat(readRecentLoads().Where(p => !(string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))).Take(maxRecentLoads).ToList();
                    File.WriteAllLines(recentLoadsFile, updated);
                }
                catch (Exception) { }
            }
            string pendingLoadPath = null;   // set when the user clicks an entry in the recent-saves box (skips the file dialog)
            void startButtonBehavior(int n)
            {
                if (startButtonHasBeenClicked) return;
                startButtonHasBeenClicked = true;
                explainHide();
                turnHeartShuffleOn();  // To draw the display, I have been interacting with the global ChoiceDomain for items.  This switches all the boxes back to empty, 'zeroing out' what we did.
                async Task go()
                {
                    TrackerModelOptions.writeSettings();

                    var thisFeatureIsUsed = false;
                    if (thisFeatureIsUsed)   // this feature is currently unused
                    {
                        Gamepad.ControllerFailureEvent += handle;
                        OptionsMenu.gamepadFailedToInitialize = !(Gamepad.Initialize());
                    }

                    DungeonSaveAndLoad.AllData loadData = null;
                    var abortStartup = false;   // a failed/cancelled load just returns to the start screen (no restart needed)
                    if (n == 999)
                    {
                        string pickedPath;
                        if (pendingLoadPath != null)
                            pickedPath = pendingLoadPath;
                        else
                        {
                            var ofd = new OpenFileDialog();
                            SaveAndLoad.EnsureSavesDirectory();
                            ofd.InitialDirectory = SaveAndLoad.SavesDirectory;
                            ofd.Filter = "All JSON files|*.json|ZTracker saves|zt-save-*.json|All Files|*.*";
                            var r = await ofd.ShowDialog(this);
                            pickedPath = (r.HasValue && r.Value) ? ofd.FileName : null;
                        }
                        if (pickedPath != null)
                        {
                            var pickedFile = pickedPath;
                            string errorMsg = null;   // (C# can't await inside a catch block, so the error box is shown after it)
                            try
                            {
                                var json = File.ReadAllText(pickedFile);
                                var ver = System.Text.Json.JsonSerializer.Deserialize<DungeonSaveAndLoad.JustVersion>(json, new System.Text.Json.JsonSerializerOptions { AllowTrailingCommas = true });
                                var warning = SaveAndLoad.LoadWarning(ver.Version, ver.SaveFormatVersion);
                                if (warning != null && LoadDump.isActive)
                                    LoadDump.SetWarning(warning.Value);   // (--load/--dump: no box, the dump says what it would have shown)
                                else if (warning != null)
                                {
                                    var msg = $"{warning.Value}, but ZORATracker will attempt to load it anyway.";
                                    popupIsActive = true;
                                    var r = await CustomComboBoxes.DoModalMessageBox(cm, System.Drawing.SystemIcons.Error, msg, new[] { "Attempt Load" });
                                    popupIsActive = false;
                                    _ = r;
                                }
                                loadData = DungeonSaveAndLoad.LoadAll(json);
                                recordRecentLoad(pickedFile);
                            }
                            catch (Exception e)
                            {
                                errorMsg = $"Loading the save file\n{pickedFile}\nfailed with error:\n{e.Message}";
                            }
                            if (errorMsg != null && LoadDump.isActive)
                            {
                                LoadDump.Fail(errorMsg);
                                abortStartup = true;
                            }
                            else if (errorMsg != null)
                            {
                                var msg = errorMsg;
                                popupIsActive = true;
                                var r = await CustomComboBoxes.DoModalMessageBox(cm, System.Drawing.SystemIcons.Error, msg, new[] { "Ok" });
                                popupIsActive = false;
                                _ = r;
                                abortStartup = true;
                            }
                        }
                        else
                            abortStartup = true;   // they cancelled the file dialog: just go back to the start screen
                    }
                    if (abortStartup)
                    {
                        // back to the start screen so they can pick again
                        pendingLoadPath = null;
                        startButtonHasBeenClicked = false;
                    }
                    else
                    {
                        // (an extra left on Random, the dash, is decided by the seed: point at the Overworld + Extras button once the overworld is up)
                        var anyRandomExtra = loadData == null && new[] { brcb, epbcb, erbcb, dwcb, lhcb }.Any(cb => !cb.IsChecked.HasValue);
                        await doStartup(n, loadData);
                        if (anyRandomExtra)
                            // the reminder (voice and/or on-screen, as set in Reminders): some extras are Random, so check Overworld + Extras
                            Dispatcher.UIThread.Post(() =>
                                Reminders.SendReminder(TrackerModel.ReminderCategory.RandomExtras, "Check Overworld and Extras: some options are random",
                                                       new[] { Reminders.ReminderTextBox("Check Overworld + Extras") }), DispatcherPriority.ApplicationIdle);
                        if (anyRandomExtra && !TrackerModelOptions.HideRandomExtrasHint && !OptionsMenu.mainTourAfterStart)
                            Dispatcher.UIThread.Post(() =>
                                OptionsMenu.showRandomExtrasPointer(cm, () =>
                                {
                                    TrackerModelOptions.HideRandomExtrasHint = true;
                                    TrackerModelOptions.writeSettings();
                                }), DispatcherPriority.ApplicationIdle);
                        if (OptionsMenu.mainTourAfterStart)
                        {
                            // the start screen tour continues on the main screen, once it has been laid out
                            OptionsMenu.mainTourAfterStart = false;
                            Dispatcher.UIThread.Post(() => OptionsMenu.runGuidedTour(cm, OptionsMenu.mainScreenTourSteps), DispatcherPriority.ApplicationIdle);
                        }
                    }
                }
                CustomComboBoxes.StartImmediate(go());
            }
            if (LoadDump.isActive)   // --load/--dump: load the file as the recent-saves buttons do, once the start screen is up
                Dispatcher.UIThread.Post(() => { pendingLoadPath = LoadDump.loadPath; startButtonBehavior(999); }, DispatcherPriority.Background);
            OptionsMenu.startBlankRunHook = () => startButtonBehavior(0);   // (the start screen tour's Continue starts a 1st Quest Overworld run)
            var quests = new (int, string)[] {
                (0, "1st Quest"),
                (1, "2nd Quest"),
                (2, "Mixed - 1Q"),
                (3, "Mixed - 2Q"),
                (999, "Load Save"),
                };
            void addQuestExplainer(Control startButton)
            {
                startButton.PointerEntered += (_, _) => hoverExplain(startButton, "Choose the start button that matches the randomizer setting:\n\nOverworld Quest (dropdown)");
                startButton.PointerExited += (_, _) => explainHide();
            }
            var startButtons = new Dictionary<int, Button>();
            Button moreOptionsButton = null;
            foreach (var (n, q) in quests)
            {
                var startButton = Graphics.makeButton(q, null, null);
                Layout.styleStartupButton(startButton);
                if (n == 0)
                    startButton.Margin = new Thickness(0.0);
                else if (n == 999)
                    startButton.Margin = smallSpacing;
                else
                    startButton.Margin = spacing;
                if (n == 0)
                {
                    var otherButton = Graphics.makeButton("Other ROM / map...", null, null);
                    Layout.styleStartupButton(otherButton);
                    otherButton.Margin = new Thickness(0.0);
                    otherButton.Click += (_, _) =>
                    {
                        if (popupIsActive) return;
                        popupIsActive = true;
                        var dialog1 = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black };
                        addDarkTheme(dialog1);
                        var tb1 = new TextBox { IsReadOnly = true, BorderThickness = new Thickness(0.0), Foreground = Brushes.Orange };
                        tb1.Text = "ZORATracker was designed for use with fcoughlin's Zelda 1 Randomizer.\n\n" +

                                    "There are other randomizers/ROM-Hacks for Zelda 1 which use other\n" +
                                    "(sometimes randomized) overworld maps.  ZORATracker may not work with\n" +
                                    "these perfectly, but the options here are designed to allow you to use\n" +
                                    "ZORATracker with non-standard overworld maps, for a slightly degraded, but\n" +
                                    "workable tracking experience.\n\n" +

                                    "There are two main options:\n" +
                                    " - use a 'blank' 16x8 grid for overworld map tracking\n" +
                                    " - use a custom .png map file, supplied by your other randomizer/ROM-Hack\n\n" +

                                    "If you do supply a custom map file, you can also choose whether you want the map\n" +
                                    "to be fully revealed/visible at the outset, or whether each of the 16x8 tiles\n" +
                                    "should be individually hidden, until you click each tile to reveal it.\n\n" +

                                    "Choose an option below:";
                        dialog1.Children.Add(tb1);
                        var wh = new ManualResetEvent(false);
                        int? choice = null;
                        foreach (var (txt, nn) in new[] {
                                        ("I just want a blank 16x8 grid", 0),
                                        ("I want to select a map file on disk, and have it fully revealed at the start", 1),
                                        ("I want to select a map file on disk, but have each tile hidden at the start", 2),
                                     })
                        {
                            var button = Graphics.makeButton(txt, null, null);
                            Layout.styleStartupButton(button);
                            button.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
                            dialog1.Children.Add(button);
                            button.Click += (_, _) => { if (choice == null) choice = nn; wh.Set(); };
                        }
                        var cancelButton = Graphics.makeButton("Cancel", null, null);
                        Layout.styleStartupButton(cancelButton);
                        cancelButton.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
                        cancelButton.Click += (_, _) => wh.Set();   // (no choice: back to the start screen)
                        dialog1.Children.Add(cancelButton);
                        async Task go()
                        {
                            var dialogBorder = new Border { Child = dialog1, BorderThickness = new Thickness(3.0), BorderBrush = Brushes.Orange, Background = Brushes.Black, Padding = new Thickness(5.0) };
                            dialogBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                            var areaW = cm.AppMainCanvas.Width > 0.0 ? cm.AppMainCanvas.Width : WIDTH;
                            var areaH = cm.AppMainCanvas.Height > 0.0 ? cm.AppMainCanvas.Height : dialogBorder.DesiredSize.Height + 60.0;
                            // (the real size of the dialog is only known after it is placed on the popup canvas, where it inherits the app's font sizes), so center it, and shrink it
                            // if it is bigger than the window, from its SizeChanged handler using its actual size
                            dialogBorder.RenderTransformOrigin = RelativePoint.TopLeft;
                            dialogBorder.SizeChanged += (_, _) =>
                            {
                                var (dw, dh) = (dialogBorder.Bounds.Width, dialogBorder.Bounds.Height);
                                if (dw > 0.0 && dh > 0.0)
                                {
                                    var sc = Math.Min(1.0, Math.Min(0.95 * areaW / dw, 0.95 * areaH / dh));
                                    dialogBorder.RenderTransform = new ScaleTransform(sc, sc);
                                    Canvas.SetLeft(dialogBorder, Math.Max(0.0, (areaW - dw*sc) / 2.0));
                                    Canvas.SetTop(dialogBorder, Math.Max(0.0, (areaH - dh*sc) / 2.0));
                                }
                            };
                            await CustomComboBoxes.DoModal(cm, wh, Math.Max(0.0, (areaW - dialogBorder.DesiredSize.Width) / 2.0), Math.Max(0.0, (areaH - dialogBorder.DesiredSize.Height) / 2.0), dialogBorder);
                            Graphics.alternativeOverworldMapFilename = "";
                            switch (choice)
                            {
                                case null: break;  // just exit the modal and go back to startup screen if they click outside
                                case int c:
                                    if (c != 0)
                                    {
                                        var ofd = new OpenFileDialog();
                                        ofd.InitialDirectory = Platform.UserDataDir();   // (WPF: the exe folder; the same on Windows)
                                        ofd.Filter = "Overworld map images|*.png";
                                        var r = await ofd.ShowDialog(this);
                                        if (r.HasValue && r.Value)
                                            Graphics.alternativeOverworldMapFilename = ofd.FileName;
                                    }
                                    Graphics.shouldInitiallyHideOverworldMap = (c == 0 || c == 2);
                                    var text = (c == 0 ? "You have chosen a blank map grid.\n\n" : "You have chosen to load a map file.\n\n") +

                                                "Some randomizers have behavior that ZORATracker does not natively support.  For example, " +
                                                "in z1m1 you might be able to purchase a Ladder in an overworld shop.  There is no native " +
                                                "ZORATracker support for marking an overworld tile as a Ladder shop.  But you can add some " +
                                                "abitrary markup to the app in a few ways:\n" +
                                                " - click the 'Draw' button in the bottom left, to place arbitrary icons\n" +
                                                "      (e.g. you might put '$' and Ladder icons on an overworld tile)\n" +
                                                " - shift-left-click an overworld tile, to circle and label it\n" +
                                                "      (e.g. you might mark a tile with a cyan circle and an 'L')\n" +
                                                " - type text into the 'Notes' text box\n" +
                                                "      (e.g. you might type 'Ladder for sale at tile B-4')\n\n" +

                                                "Do whatever works for you.  Good luck!";
                                    var _r = await CustomComboBoxes.DoModalMessageBoxCore(cm, System.Drawing.SystemIcons.Information, text, new[] { "Ok" }, 30.0, 30.0);
                                    startButtonBehavior(4);
                                    break;
                            }
                            popupIsActive = false;
                        }
                        CustomComboBoxes.StartImmediate(go());
                    };
                    moreOptionsButton = otherButton;
                }
                startButtons[n] = startButton;
                startButton.Click += (_, _) => startButtonBehavior(n);
            }

            // layout: [1st Quest | Mixed 1st]  ---  [2nd Quest | Mixed 2nd]  ---  [More options]  ---  [saved state]
            Border divider() => new Border { Height = 1.0, Background = new SolidColorBrush(Color.FromRgb(42, 42, 42)), Width = WIDTH*0.8, Margin = new Thickness(0.0, 7.0, 0.0, 7.0), HorizontalAlignment = HorizontalAlignment.Center };
            Func<Button, Button, Grid> pairRow = (a, b) =>   // (unused in WPF too)
            {
                var g = new Grid { Width = WIDTH*0.9, HorizontalAlignment = HorizontalAlignment.Center };
                g.ColumnDefinitions.Add(new ColumnDefinition(1.0, GridUnitType.Star));
                g.ColumnDefinitions.Add(new ColumnDefinition(1.0, GridUnitType.Star));
                a.Margin = new Thickness(0.0, 0.0, 3.0, 0.0);
                b.Margin = new Thickness(3.0, 0.0, 0.0, 0.0);
                Grid.SetColumn(a, 0);
                Grid.SetColumn(b, 1);
                g.Children.Add(a);
                g.Children.Add(b);
                return g;
            };
            var topDivider = divider();   // (no longer shown; the start spacing code below still measures it)
            // the overworld choices now live in a 'Start Game' menu button at the top of the Startup Options column
            var randomizedQuestButton = Graphics.makeButton("Randomized Quest", null, null);
            Layout.styleStartupButton(randomizedQuestButton);
            randomizedQuestButton.Click += (_, _) => startButtonBehavior(3);   // 'Randomized quest' does exactly what 'Mixed - 2nd Quest Overworld' does; it's just its own entry
            // the six overworld choices sit in one block above the Startup Options and ZORA Extras columns (3 across, 2 rows)
            var popupButtons = new List<Button> { startButtons[0], startButtons[2], randomizedQuestButton, startButtons[1], startButtons[3], moreOptionsButton };
            var owGrid = new UniformGrid { Columns = 3, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0.0, 10.0, 0.0, 6.0) };
            foreach (var b in popupButtons)
            {
                b.Margin = new Thickness(4.0);
                b.Width = double.NaN;
                b.Height = startBoxHeight;
                b.HorizontalAlignment = HorizontalAlignment.Stretch;
                owGrid.Children.Add(b);
            }
            owGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
            // the start screen is a stack of equal-width bordered cards, 40 apart: overworld, the two option cards, recent saves, and a row of four buttons
            {
                var idx = hsPanel.Children.IndexOf(twoCols);
                hsPanel.Children.RemoveAt(idx);
                var colsWrap = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, Width = startCardsWidth };
                var owSP = new StackPanel { Orientation = Orientation.Vertical };
                var owHeader = new TextBox { Text = "Overworld (clicking starts the tracker)", IsReadOnly = true, IsHitTestVisible = false, FontWeight = FontWeight.Bold, FontSize = 16.0, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0), Margin = new Thickness(0.0) };
                owSP.Children.Add(owHeader);
                owSP.Children.Add(new Border { Height = 1.0, Background = Brushes.Gray, Margin = new Thickness(0.0, 2.0, 0.0, 4.0), HorizontalAlignment = HorizontalAlignment.Stretch });   // line under the header
                owSP.Children.Add(owGrid);
                colsWrap.Children.Add(startCard(owSP));
                twoCols.Margin = new Thickness(0.0, startCardsGap, 0.0, 0.0);
                colsWrap.Children.Add(twoCols);
                hsPanel.Children.Insert(idx, colsWrap);
            }
            Action<bool> raiseBottom = _ => { };   // raises the bottom section above the rest while the settings panel is up (set once that section exists)
            // the settings panel is hidden until the Options button is clicked; it then flies up from the bottom edge
            var optionsToggle = Graphics.makeButton("Options ▲", null, null);
            Layout.styleStartupButton(optionsToggle);
            // Load Save, Guided Tour, Options and the version button make one row of four equal buttons
            var tourButton = OptionsMenu.makeGuidedTourButton(cm, false);
            Layout.styleStartupButton(tourButton);
            var vbButton = CustomComboBoxes.makeVersionButtonWithBehavior(cm);
            Layout.styleStartupButton(vbButton);
            if (vbButton.Content is TextBox vbt) vbt.FontSize = 16.0;   // (the version button was made with a 12pt label; match the others)
            // Paste Flags: reads the Z1R and ZORA flag strings from the ZORA site and sets the matching startup options
            var pasteFlagsButton = Graphics.makeButton("Paste Flags", null, null);
            Layout.styleStartupButton(pasteFlagsButton);
            pasteFlagsButton.Click += (_, _) =>
            {
                if (popupIsActive) return;
                popupIsActive = true;
                var dialog = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Black };
                addDarkTheme(dialog);
                TextBox mkInfo(string t) => new TextBox { Text = t, IsReadOnly = true, BorderThickness = new Thickness(0.0), Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap, Width = 600.0, Margin = new Thickness(0.0, 0.0, 0.0, 8.0) };
                TextBox mkInput() => new TextBox { Width = 600.0, FontSize = 16.0, Foreground = Brushes.Orange, Background = Brushes.Black, BorderBrush = Brushes.Orange, CaretBrush = Brushes.Orange, Margin = new Thickness(0.0, 0.0, 0.0, 10.0) };
                var intro = mkInfo("Paste the flag strings from the ZORA site and the tracker ticks the matching startup options when you paste, or when you click Apply. Leave a box empty to leave those settings alone.");
                var (z1rBox, zoraBox) = (mkInput(), mkInput());
                var result = mkInfo("");
                result.IsVisible = false;
                var inputs = new StackPanel { Orientation = Orientation.Vertical };
                inputs.Children.Add(mkInfo("Z1R flags"));
                inputs.Children.Add(z1rBox);
                inputs.Children.Add(mkInfo("ZORA flags (like 18.abc)"));
                inputs.Children.Add(zoraBox);
                dialog.Children.Add(intro);
                dialog.Children.Add(inputs);
                dialog.Children.Add(result);
                var wh = new ManualResetEvent(false);
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                var applyBtn = Graphics.makeButton(" Apply ", null, null);
                var closeBtn = Graphics.makeButton(" Close ", null, null);
                Layout.styleStartupButton(applyBtn);
                Layout.styleStartupButton(closeBtn);
                applyBtn.Margin = new Thickness(6.0);
                closeBtn.Margin = new Thickness(6.0);
                applyBtn.Height = startBoxHeight;
                closeBtn.Height = startBoxHeight;
                buttons.Children.Add(applyBtn);
                buttons.Children.Add(closeBtn);
                dialog.Children.Add(buttons);
                closeBtn.Click += (_, _) => wh.Set();
                // applying sets the checkboxes and lists what was done under the boxes (the dialog stays open, so the second string can still be pasted)
                bool doApply()   // true = no flag decode error
                {
                    var lines = new List<string>();
                    void setChk(CheckBox cb, bool b) => cb.IsChecked = b;
                    void setTri(CheckBox cb, FlagPaste.Tri t, string name)
                    {
                        switch (t)
                        {
                            case FlagPaste.Tri.On: setChk(cb, true); lines.Add("Checked: " + name); break;
                            case FlagPaste.Tri.Off: setChk(cb, false); break;
                            case FlagPaste.Tri.Random: lines.Add(name + " is Random in these flags, so it was left as it was."); break;
                        }
                    }
                    var (z1r, zora) = ((z1rBox.Text ?? "").Trim(), (zoraBox.Text ?? "").Trim());
                    if (z1r != "")
                    {
                        var (p, err) = FlagPaste.decodeZ1R(z1r);
                        if (err != null) lines.Add("Z1R flags: " + err);
                        else
                        {
                            if (p.ItemShuffle.HasValue)
                            {
                                var m = p.ItemShuffle.Value;
                                selectHeartMode(m);
                                lines.Add("Item Shuffle: " + new[] { "Off", "Intra", "Full" }[m]);
                            }
                            setTri(mocb, p.Mirror, "Mirrored Overworld");
                            setTri(hdcb, p.HideNumbers, "Hide Dungeon Numbers");
                            foreach (var b in new[] { startButtons[0], startButtons[1], startButtons[2], startButtons[3] }) b.Effect = null;
                            if (p.Quest.HasValue)
                            {
                                var q = p.Quest.Value;
                                startButtons[q].Effect = new DropShadowDirectionEffect { ShadowDepth = 0.0, BlurRadius = 24.0, Opacity = 1.0, Color = Colors.Orange };
                                lines.Add("Overworld: click " + new[] { "1st Quest", "2nd Quest", "Mixed - 1Q", "Mixed - 2Q" }[q] + " (highlighted) to start");
                            }
                            foreach (var n in p.Notes) lines.Add(n);
                        }
                    }
                    if (zora != "")
                    {
                        var (z, err) = FlagPaste.decodeZora(zora);
                        if (err != null) lines.Add("ZORA flags: " + err);
                        else
                        {
                            var anyOn = z.FourthSword == FlagPaste.Tri.On
                                        || new[] { z.BootsReplaceBoomerangs, z.ExtraRaftBlocks, z.ExtraPowerBlocks, z.RandomizeDeadWoods, z.RandomizeLostHills }.Any(t => t != FlagPaste.Tri.Off)
                                        || z.Progressive;
                            if (anyOn && isOn(lmcb))
                            {
                                setChk(lmcb, false);   // (Legacy Mode locks every ZORA Extra off)
                                lines.Add("Legacy Mode turned off");
                            }
                            switch (z.FourthSword)
                            {
                                case FlagPaste.Tri.On:
                                    setChk(fscb, true);
                                    setFourthLevel(z.FourthInLevel2 ? 2 : 9);
                                    lines.Add($"Checked: 4th Sword (Level {(z.FourthInLevel2 ? 2 : 9)})");
                                    break;
                                case FlagPaste.Tri.Off: setChk(fscb, false); break;
                                case FlagPaste.Tri.Random: lines.Add("4th Sword is Random in these flags, so it was left as it was."); break;
                            }
                            setTri(pricb, (z.Progressive || z.FourthSword == FlagPaste.Tri.On) ? FlagPaste.Tri.On : FlagPaste.Tri.Off, "Progressive Items");   // (the 4th Sword needs Progressive Items)
                            if (z.BootsReplaceBoomerangs == FlagPaste.Tri.Random)
                            {
                                brcb.IsChecked = null;
                                lines.Add("Random (?, counts as Off until you set it in Overworld + Extras): Boots Replace Boomerangs");
                            }
                            else setTri(brcb, z.BootsReplaceBoomerangs, "Boots Replace Boomerangs");
                            // the four block/hint options can be Random: shown as a ?, which the tracker treats as On
                            void setTri3(CheckBox cb, FlagPaste.Tri t, string name)
                            {
                                if (t == FlagPaste.Tri.Random)
                                {
                                    cb.IsChecked = null;
                                    lines.Add("Random (?, counts as On): " + name);
                                }
                                else setTri(cb, t, name);
                            }
                            setTri3(epbcb, z.ExtraPowerBlocks, "Extra Power Bracelet Blocks");
                            setTri3(erbcb, z.ExtraRaftBlocks, "Extra Raft Blocks");
                            setTri3(dwcb, z.RandomizeDeadWoods, "Randomize Dead Woods");
                            setTri3(lhcb, z.RandomizeLostHills, "Randomize Lost Hills");
                        }
                    }
                    result.Text = lines.Count == 0 ? "Nothing to change." : string.Join("\n", lines);
                    result.IsVisible = true;
                    return !lines.Any(l => l.StartsWith("Z1R flags:") || l.StartsWith("ZORA flags:"));
                }
                // (Apply applies and closes, unless a flag string was bad: then the box stays open so the error can be read. Pasting only applies, so the second string can still be pasted)
                applyBtn.Click += (_, _) => { if (doApply()) wh.Set(); };
                // pasting into either box applies straight away
                // (WPF: DataObject.AddPastingHandler, then doApply at Background priority once the text is in; Avalonia: PastingFromClipboard)
                foreach (var box in new[] { z1rBox, zoraBox })
                    box.PastingFromClipboard += (_, _) => Dispatcher.UIThread.Post(() => doApply(), DispatcherPriority.Background);
                async Task show()
                {
                    var dialogBorder = new Border { Child = dialog, BorderThickness = new Thickness(3.0), BorderBrush = Brushes.Orange, Background = Brushes.Black, Padding = new Thickness(8.0) };
                    dialogBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    var areaW = cm.AppMainCanvas.Width > 0.0 ? cm.AppMainCanvas.Width : WIDTH;
                    var areaH = cm.AppMainCanvas.Height > 0.0 ? cm.AppMainCanvas.Height : dialogBorder.DesiredSize.Height + 60.0;
                    // the cursor starts in the top box
                    z1rBox.Loaded += (_, _) => Dispatcher.UIThread.Post(() => z1rBox.Focus(), DispatcherPriority.Background);
                    await CustomComboBoxes.DoModalSticky(cm, wh, Math.Max(0.0, (areaW - dialogBorder.DesiredSize.Width) / 2.0), Math.Max(0.0, (areaH - dialogBorder.DesiredSize.Height) / 3.0), dialogBorder);
                    popupIsActive = false;   // (so the button works again after Cancel or Done)
                }
                CustomComboBoxes.StartImmediate(show());
            };
            // the button sits at the bottom of the ZORA Extras card, one checkbox row of space below the last option (the card grows to fit, and Startup Options matches its height)
            {
                rightCard.Child = null;
                var dp = new DockPanel { LastChildFill = true };
                pasteFlagsButton.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
                pasteFlagsButton.HorizontalAlignment = HorizontalAlignment.Stretch;
                pasteFlagsButton.Height = startBoxHeight;
                DockPanel.SetDock(pasteFlagsButton, Dock.Bottom);
                // (an empty row above the button: the same height as the checkbox rows)
                var gapRow = new Border { Height = 34.0 };
                DockPanel.SetDock(gapRow, Dock.Bottom);
                dp.Children.Add(pasteFlagsButton);
                dp.Children.Add(gapRow);
                dp.Children.Add(rightCol);
                rightCard.Child = dp;
            }
            // the Custom LEVEL Name box sits at the bottom of the Startup Options card, level with Paste Flags and the same size
            {
                leftCard.Child = null;
                var dp = new DockPanel { LastChildFill = true };
                DockPanel.SetDock(clnTextBox, Dock.Bottom);
                dp.Children.Add(clnTextBox);
                dp.Children.Add(leftCol);
                leftCard.Child = dp;
            }
            var toolsButtons = new List<Button> { startButtons[999], tourButton, optionsToggle, vbButton };
            var toolsGrid = new UniformGrid { Columns = 4, Width = startCardsWidth, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0.0, startCardsGap, 0.0, 0.0) };
            foreach (var b in toolsButtons)
            {
                b.Width = double.NaN;
                b.Height = startBoxHeight;
                b.Margin = new Thickness(4.0);
                b.HorizontalAlignment = HorizontalAlignment.Stretch;
                toolsGrid.Children.Add(b);
            }

            // card listing the save files loaded before (newest first), for fast re-selection: three slots across
            Border recentsCard;
            {
                var recents = readRecentLoads().Take(3).ToList();
                var slots = new UniformGrid { Columns = 3, HorizontalAlignment = HorizontalAlignment.Stretch };
                for (int i = 0; i <= 2; i++)
                {
                    if (i < recents.Count)
                    {
                        var p = recents[i];
                        string when_;
                        try { when_ = File.GetLastWriteTime(p).ToString("yyyy-MM-dd HH:mm"); } catch (Exception) { when_ = ""; }
                        var b = Graphics.makeButton(Path.GetFileNameWithoutExtension(p), null, null);
                        Layout.styleStartupButton(b);
                        b.Margin = new Thickness(4.0);
                        b.Width = double.NaN;
                        b.Height = startBoxHeight;
                        b.HorizontalAlignment = HorizontalAlignment.Stretch;
                        if (b.Content is TextBox t) t.FontSize = 14.0;
                        ToolTip.SetTip(b, $"{p}\nlast modified {when_}");
                        b.Click += (_, _) =>
                        {
                            if (!(File.Exists(p)))
                            {
                                b.IsEnabled = false;
                                ToolTip.SetTip(b, "This file no longer exists");
                            }
                            else
                            {
                                pendingLoadPath = p;
                                startButtonBehavior(999);
                            }
                        };
                        slots.Children.Add(b);
                    }
                    else
                        slots.Children.Add(new Border { Margin = new Thickness(4.0), BorderBrush = Brushes.DimGray, BorderThickness = new Thickness(1.0), CornerRadius = new CornerRadius(4.0), Opacity = 0.4 });   // (an empty slot keeps the five equal)
                }
                var sp = new StackPanel { Orientation = Orientation.Vertical };
                sp.Children.Add(new TextBox { Text = "Recently loaded saves:", IsReadOnly = true, IsHitTestVisible = false, FontWeight = FontWeight.Bold, FontSize = 16.0, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0) });
                sp.Children.Add(new Border { Height = 1.0, Background = Brushes.Gray, Margin = new Thickness(0.0, 2.0, 0.0, 4.0), HorizontalAlignment = HorizontalAlignment.Stretch });
                if (recents.Count == 0)
                    sp.Children.Add(new TextBox { Text = "(none yet - files you load will show up here)", IsReadOnly = true, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), Foreground = Brushes.Gray, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 13.0, Margin = new Thickness(0.0, 0.0, 0.0, 4.0) });
                sp.Children.Add(slots);
                var c = startCard(sp);
                c.Width = startCardsWidth;
                c.HorizontalAlignment = HorizontalAlignment.Center;
                c.Margin = new Thickness(0.0, startCardsGap, 0.0, 0.0);
                recentsCard = c;
            }

            stackPanel.Children.Add(finalChildTipArea);  // finalChildTipArea still used for explainer arrows on startup options
            finalChildTipArea.ZIndex = 10;   // (the hover description is drawn over the cards that follow it)
            stackPanel.Children.Add(recentsCard);
            stackPanel.Children.Add(toolsGrid);

            // Tip lives inside bottomSP so it's in proper layout flow — can never overlap the Settings header
            var bottomSP = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Stretch };
            var tipsp = new StackPanel { Orientation = Orientation.Vertical };
            var tipHeaderTB = MakeTipTextBox("Random tip:");
            tipsp.Children.Add(tipHeaderTB);
            var tipTB = MakeTipTextBox(DungeonData.Factoids.allTips[new Random().Next(DungeonData.Factoids.allTips.Length)]);   // correct app behavior
            //let tb = MakeTipTextBox(DungeonData.Factoids.allTips |> Array.sortBy (fun s -> s.Length) |> Seq.last)   // show the longest tip (to test screen layout)
            tipTB.Margin = spacing;
            tipsp.Children.Add(tipTB);
            var _tip = new Border { Child = tipsp, BorderThickness = new Thickness(1.0), Padding = new Thickness(5.0), BorderBrush = Brushes.Orange, Width = WIDTH*2.0/3.0, Margin = new Thickness(0.0, 4.0, 0.0, 4.0), HorizontalAlignment = HorizontalAlignment.Center };
            //bottomSP.Children.Add(tip) |> ignore   // random tip box removed from the startup screen
            bottomSP.Children.Add(new Shapes.Rectangle { HorizontalAlignment = HorizontalAlignment.Stretch, Fill = Brushes.Black, Height = 2.0, Margin = spacing });
            var settingsTB = new TextBox { Text = "Settings (most can be changed later, using 'Options' button above timeline):", HorizontalAlignment = HorizontalAlignment.Center,
                                           Margin = new Thickness(0.0, 0.0, 0.0, 5.0), BorderThickness = new Thickness(0.0) };
            _ = settingsTB;   // (the "Settings (most can be changed later...)" heading is no longer shown)
            var options = OptionsMenu.makeOptionsCanvas(cm, false, true);
            var optionsWithVersion = new Grid();   // the version button sits at the bottom right of the other settings
            optionsWithVersion.Children.Add(options);
            // the settings panel takes no layout room: it sits just under the bottom edge (hidden) and flies up over the startup screen when the Options button is clicked
            // (a close button sits at the top of the panel: the panel covers the row of buttons at the bottom of the start screen when it is up)
            var closeBar = Graphics.makeButton("Options ▼", null, null);
            Layout.styleStartupButton(closeBar);
            closeBar.Width = 240.0;
            closeBar.Height = startBoxHeight;
            closeBar.Margin = new Thickness(0.0, 6.0, 0.0, 6.0);
            closeBar.HorizontalAlignment = HorizontalAlignment.Center;
            var optionsPanelSP = new StackPanel { Orientation = Orientation.Vertical };
            optionsPanelSP.Children.Add(closeBar);
            optionsPanelSP.Children.Add(optionsWithVersion);
            var optionsPanel = new Border { Child = optionsPanelSP, Background = Brushes.Black, BorderBrush = Brushes.SaddleBrown, BorderThickness = new Thickness(2.0), Width = WIDTH_SANS_CHROME };
            var optionsHost = new Canvas { Height = 0.0 };
            Canvas.SetLeft(optionsPanel, 0.0);
            Canvas.SetTop(optionsPanel, 0.0);
            optionsHost.Children.Add(optionsPanel);
            bottomSP.Children.Add(optionsHost);
            mainDock.ClipToBounds = true;
            var optionsShown = false;
            var optionsH = 0.0;
            // (WPF: a 0.25s DoubleAnimation of Canvas.Top with a cubic ease-out; tweened by hand here, as elsewhere in the port, and stopped by setting Top directly)
            DispatcherTimer optionsAnim = null;
            void setOptionsShown(bool show, bool animate)
            {
                if (optionsH == 0.0)
                {
                    optionsPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    optionsH = optionsPanel.DesiredSize.Height;
                }
                optionsShown = show;
                if (optionsToggle.Content is TextBox ot) ot.Text = show ? "Options ▼" : "Options ▲";
                raiseBottom(show);
                var target = show ? -optionsH : 0.0;
                optionsAnim?.Stop();
                if (animate)
                {
                    var from = Canvas.GetTop(optionsPanel);
                    if (double.IsNaN(from)) from = 0.0;
                    var start = DateTime.Now;
                    optionsAnim = new DispatcherTimer(TimeSpan.FromMilliseconds(16.0), DispatcherPriority.Render, (_, _) =>
                    {
                        var f = Math.Min(1.0, (DateTime.Now - start).TotalSeconds / 0.25);
                        var e = 1.0 - Math.Pow(1.0 - f, 3.0);   // (CubicEase, EaseOut)
                        Canvas.SetTop(optionsPanel, from + (target - from) * e);
                        if (f >= 1.0) optionsAnim.Stop();
                    });
                    optionsAnim.Start();
                }
                else
                {
                    Canvas.SetTop(optionsPanel, target);
                    mainDock.UpdateLayout();
                }
            }
            optionsToggle.Click += (_, _) => setOptionsShown(!optionsShown, true);
            closeBar.Click += (_, _) => setOptionsShown(false, true);
            // the block of cards and buttons sits in the middle of the window: the same margin above and below (once, after the first layout)
            var centerBlockOnce = false;
            void centerBlock()
            {
                if (!centerBlockOnce && mainDock.IsLoaded && stackPanel.Bounds.Height > 0.0 && mainDock.Bounds.Height > stackPanel.Bounds.Height)
                {
                    centerBlockOnce = true;
                    Canvas.SetTop(stackPanel, (mainDock.Bounds.Height - stackPanel.Bounds.Height) / 2.0);
                }
            }
            mainDock.LayoutUpdated += (_, _) => centerBlock();
            mainDock.Loaded += (_, _) => Dispatcher.UIThread.Post(centerBlock, DispatcherPriority.ApplicationIdle);
            OptionsMenu.showStartOptionsHook = show => setOptionsShown(show, false);
            mainDock.Children.Add(bottomSP);
            raiseBottom = on => bottomSP.ZIndex = (on ? 100 : 0);   // (so the opened recent-saves list is drawn over the startup options above it, while the hover explainer stays on top the rest of the time)
            DockPanel.SetDock(bottomSP, Dock.Bottom);

            mainDock.Children.Add(mainStackPanel);
            // vertically center the block of Start buttons between the startup-options checkboxes above and the Settings line below
            var centeredOnce = false;   // (everything now sits at the top of the open space; set to false to bring back the vertical centering of the start buttons) center the whole block of Start buttons in the open space (once, after first layout)
            mainDock.LayoutUpdated += (_, _) =>
            {
                if (!centeredOnce && mainDock.IsLoaded && bottomSP.Bounds.Height > 0.0 && finalChildTipArea.IsLoaded && hsPanel.Bounds.Height > 0.0)
                {
                    centeredOnce = true;
                    try
                    {
                        // (WPF's TranslatePoint throws for an element that isn't in the window, as topDivider/topSpacer aren't; Avalonia's returns null, so .Value throws the same way)
                        var hsBottom = topDivider.TranslatePoint(new Point(0.0, topDivider.Bounds.Height + 7.0), mainDock).Value.Y;   // (the quest buttons are centered below the divider)
                        var buttonsTop = topSpacer.TranslatePoint(new Point(0.0, topSpacer.Bounds.Height), mainDock).Value.Y;
                        var buttonsBottom = finalChildTipArea.TranslatePoint(new Point(), mainDock).Value.Y;
                        var settingsTop = bottomSP.TranslatePoint(new Point(), mainDock).Value.Y;
                        var available = settingsTop - hsBottom;
                        var blockHeight = buttonsBottom - buttonsTop;
                        var desiredGap = (available - blockHeight) / 2.0;
                        var currentGap = buttonsTop - hsBottom;
                        if (desiredGap > 0.0)
                            topSpacer.Height = Math.Max(0.0, topSpacer.Bounds.Height + (desiredGap - currentGap));
                    }
                    catch (Exception) { }
                }
            };

            // "dark theme"
            mainDock.Background = Brushes.Black;
            addDarkTheme(mainDock);

            if (File.Exists(crashLogFilename))
            {
                var lines = File.ReadAllLines(crashLogFilename);
                if (lines.Length > 1)
                {
                    if (DateTime.TryParseExact(lines[lines.Length - 1], dateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var crashTime))
                    {
                        if (DateTime.Now - crashTime < TimeSpan.FromMinutes(10.0))
                        {
                            // it crashed in the past 10 minutes
                            if (File.Exists(SaveAndLoad.AutoSaveFilename))
                            {
                                var autoSaveTime = File.GetLastWriteTime(SaveAndLoad.AutoSaveFilename);
                                if (autoSaveTime > (crashTime - TimeSpan.FromMinutes((double)TrackerModelOptions.AutoSaveMinutes + 1.0)))   // (the Auto-save interval option, plus a minute of slack)
                                {
                                    popupIsActive = true;
                                    async Task go()
                                    {
                                        // there was an autosave just before the crash
                                        var r = await CustomComboBoxes.DoModalMessageBox(cm, null,
                                                                                         "It appears that ZORATracker recently crashed.\n"+
                                                                                         "There is an auto-save from shortly before the crash.\n"+
                                                                                         "Would you like to try loading it?", new[] { "Yes, load auto-save", "No" });
                                        popupIsActive = false;
                                        if (r != "No")
                                        {
                                            var json = File.ReadAllText(SaveAndLoad.AutoSaveFilename);
                                            var loadData = DungeonSaveAndLoad.LoadAll(json);
                                            await doStartup(999, loadData);
                                            // successful reload of autosave, call this so next startup won't also trigger recovery dialog
                                            finishCrashInfoImpl("successful reload of autosave");
                                        }
                                        else
                                            promptedCrashRecovery = true;
                                    }
                                    CustomComboBoxes.StartImmediate(go());
                                }
                            }
                        }
                    }
                }
            }
#if BROWSER
            // (port: closing the page mid-run leaves no crash log, so the web version offers the autosave from a visit in the last day)
            if (!popupIsActive && BrowserStorage.WrittenAt(SaveAndLoad.AutoSaveFilename) is DateTime autoSavedAt && DateTime.UtcNow - autoSavedAt < TimeSpan.FromDays(1.0))
            {
                popupIsActive = true;
                async Task resume()
                {
                    var ago = DateTime.UtcNow - autoSavedAt;
                    var agoText = ago.TotalMinutes < 2 ? "a minute ago" : ago.TotalMinutes < 90 ? $"{(int)ago.TotalMinutes} minutes ago" : $"{(int)ago.TotalHours} hours ago";
                    var r = await CustomComboBoxes.DoModalMessageBox(cm, null,
                                                                     $"There is an auto-save from your last visit ({agoText}).\n"+
                                                                     "Would you like to load it?", new[] { "Yes, load auto-save", "No" });
                    popupIsActive = false;
                    if (r == "Yes, load auto-save")
                    {
                        var json = File.ReadAllText(SaveAndLoad.AutoSaveFilename);
                        await doStartup(999, DungeonSaveAndLoad.LoadAll(json));
                    }
                }
                CustomComboBoxes.StartImmediate(resume());
            }
#endif
        }

        // (F#: Async.SwitchToContext; a no-op when already there)
        private static Task switchToContext(SynchronizationContext c)
        {
            if (c == null || SynchronizationContext.Current == c)
                return Task.CompletedTask;
            var tcs = new TaskCompletionSource();
            c.Post(_ => tcs.SetResult(), null);
            return tcs.Task;
        }

        public override void Update(bool f10Press)
        {
            base.Update(f10Press);
            // once Zelda is rescued (the princess icon or its global hotkey), the timer stops; un-marking her resumes it
            if (TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Value()) TrackerModel.LastChangedTime.PauseAll();
            // Snoop For Seed&flags: look in the other windows' titles every 10 seconds (not only when saving or pausing the timer)
            if (TrackerModelOptions.SnoopSeedAndFlags.Value && (DateTime.Now - lastSnoopPoll).TotalSeconds >= 10.0)
            {
                lastSnoopPoll = DateTime.Now;
                SaveAndLoad.MaybePollSeedAndFlags();
            }
            // update time
            var ts = DateTime.Now - this.StartTime.Time;
            var curSecond = (int)ts.TotalSeconds;
            if (curSecond > lastUpdateSecond)
            {
                lastUpdateSecond = curSecond;
                var (h, m, s) = (ts.Hours, ts.Minutes, ts.Seconds);
                var time = $"{h,2}:{m:00}:{s:00}";
                OverworldItemGridUI.hmsTimeTextBox.Text = time;
                OverworldItemGridUI.broadcastTimeTextBox.Text = time;
                var curMinute = (int)ts.TotalMinutes;
                if (f10Press || curMinute > lastUpdateMinute)
                {
                    lastUpdateMinute = curMinute;
                    updateTimeline(curMinute);
                }
            }
        }
    }

    // in order for multiple app windows to not have a forced Z-Order from Owner-Child relationship, need a hidden dummy window to own all the visible windows
    // (Avalonia: not a window. An invisible owner would do more harm than good there (owned windows hide and minimize with their owner, and on macOS an owned
    // window is attached to its owner), and the main window has to be the app lifetime's MainWindow (ShowRunCustom.mainWindow(), Graphics.RestartTheApplication).
    // So this keeps the dummy window's jobs: start the main window, hide the console, and end the app when the main window closes (Close()).)
    public sealed class DummyWindow
    {
        private readonly IClassicDesktopStyleApplicationLifetime lifetime;
        public DummyWindow(IClassicDesktopStyleApplicationLifetime lifetime)
        {
            this.lifetime = lifetime;
            // (WPF: ShowInTaskbar false, Title "ZORATracker start-up...", 300x100, Minimized, hidden once loaded)
        }
        // (WPF: the dummy window's Loaded handler)
        public void Loaded()
        {
/*
            // method 1
            let resHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height  // e.g. 1440
            let actualHeight = SystemParameters.PrimaryScreenHeight  // e.g. 960
            let scale = float resHeight / actualHeight  // e.g.   1.5
            let waHeight = SystemParameters.WorkArea.Height
            printfn "method1: scale = %f    actualHeight=%f    waHeight=%f" scale actualHeight waHeight
            // TODO if waHeight < 1000.0 then suggest Smaller
//caffy
//method1: scale = 1.250000    actualHeight=864.000000    waHeight=824.000000
//method2: scale = 1.250000
            // method 2
            let dpi = VisualTreeHelper.GetDpi(this)
            printfn "method2: scale = %f, ppi = %f" dpi.DpiScaleY dpi.PixelsPerInchY
*/

            var mainW = new MyWindow();
#if !BROWSER
            lifetime.MainWindow = mainW;   // (WPF: mainW.Owner <- this)
#endif
            mainW.Show();
            var handle = Winterop.GetConsoleWindow();
            Winterop.ShowWindow(handle, Winterop.SW_HIDE);   // hidden rather than minimized (no stray bar on screen); a crash still brings it back with SW_SHOWNORMAL
            mainW.Closed += (_, _) => { try { this.Close(); } catch (Exception) { } };  // ignore exceptions on close; restart button tends to crash here
        }
        public void Close() => lifetime.Shutdown();
    }

    // (port: Dungeon.stopLocalSignalRHost, unless Dungeon was never set up. Touching Dungeon runs its static initializer, which needs the CanvasManager (see
    // runStaticInitializersInWpfCompileOrder), so when the app ends before there is one (e.g. "Another instance ... is already running") it would throw.
    // Without a CanvasManager no host can have been started.)
    internal static void stopLocalSignalRHost()
    {
        if (CustomComboBoxes.CanvasManager.TheOnlyCanvasManager != null)
            Dungeon.stopLocalSignalRHost();
    }

    // (WPF: `[<STAThread>] [<EntryPoint>] let main _argv`. The desktop host's Main calls this with its AppBuilder (Avalonia's platform backends live in
    // that project); `configure` is WPF's `new Application()`.)
    public static int main(string[] _argv, Func<AppBuilder> configure)
    {
        Console.WriteLine("Starting ZORATracker...");
        try { Winterop.ShowWindow(Winterop.GetConsoleWindow(), Winterop.SW_HIDE); } catch (Exception) { }   // hide the console right away (a crash still re-shows it)
        var cwd = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        var thisExe = Path.Combine(cwd, Path.GetFileName(Platform.ExecutablePath()));   // (port: Graphics.ExeName, without touching Graphics: its statics make Avalonia bitmaps, and Avalonia isn't running yet)
        var thisExeName = thisExe.Replace('\\', '_').Replace('/', '_');   // backslash is not allowed character in mutex name   (port: nor is '/', on macOS/Linux)
        using var mutex = new Mutex(false, thisExeName);
        void killAnyZombieHosts()
        {
            var procs = Process.GetProcessesByName("Z1R_SignalRHost");
            foreach (var p in procs)
            {
                try { p.Kill(); } catch (Exception) { }
            }
        }

        void runTheApp()
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                stopLocalSignalRHost();
            };
            killAnyZombieHosts();
            var app = configure();
#if DEBUG
#else
            try
            {
#endif
                //startLocalSignalRHost()
                // (WPF: theDummyWindow <- DummyWindow(); app.Exit.Add(...); app.Run(theDummyWindow). Here ZoraTrackerApp creates theDummyWindow once Avalonia is
                // running and adds the Exit handler; the app runs until theDummyWindow.Close() (explicit shutdown, as WPF's app ends when its last window closes))
                app.StartWithClassicDesktopLifetime(_argv, ShutdownMode.OnExplicitShutdown);

#if DEBUG
#else
            }
            catch (Exception e)
            {
                var crashLog = Platform.Files.User("Z1R_Tracker_crash_log.txt");
                try
                {
                    File.AppendAllText(crashLog, $"\n\nBEGIN CRASH LOG (startup) -- {DateTime.Now.ToString("yyyy-MM-dd-HH:mm:ss")}\n{e.ToString()}\nEND CRASH LOG\n");
                }
                catch (Exception) { }
                throw;
            }
#endif
        }

        try
        {
            if (mutex.WaitOne(5000, false))   // single instance, to prevent competing write of auto-save or options in this directory
                runTheApp();
            else
            {
                Console.WriteLine("Another instance of ZORATracker is already running in this directory.");
                Console.WriteLine("Close it before running this.");
                Console.WriteLine("(Press a key to dismiss this console window)");
                Thread.Sleep(1000);
                readKey();
            }
        }
        catch (AbandonedMutexException)  // this happens if e.g. someone shuts down the other instance by clicking 'X' on its window corner while we are waiting
        {
            runTheApp();  // fine to start us now
        }
        catch (Exception e)
        {
            Console.WriteLine($"ERROR: {e.ToString()}");
            Console.WriteLine("(Press a key to dismiss this console window)");
            readKey();
        }

        stopLocalSignalRHost();
        waitForConsole.WaitOne();
        return 0;
    }
}

// (WPF: the `new Application()` that main runs. Avalonia needs an Application class: the Fluent theme with the Light variant (WPF's unstyled defaults are
// light; see PROGRESS.md). Once Avalonia is running, it does WPF's `theDummyWindow <- DummyWindow()`, `app.Exit.Add(...)`, and the
// dummy window's Loaded (as a dispatcher callback, so exceptions there reach the crash handlers like WPF's Loaded handler's do).)
public class ZoraTrackerApp : Application
{
    public override void Initialize()
    {
        Name = "ZORA Tracker";   // (macOS shows it as the app menu's title; Avalonia's default is "Avalonia Application")
        Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        RequestedThemeVariant = ThemeVariant.Light;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
        {
            if (LoadDump.isActive) LoadDump.arm();
            if (SelfTest.isSelfTest) SelfTest.arm();   // (WPF arms it before app.Run; here Avalonia's dispatcher only exists once the app is running)
            Program.theDummyWindow = new Program.DummyWindow(d);
            d.Exit += (_, _) => Program.stopLocalSignalRHost();
            Dispatcher.UIThread.Post(() => Program.theDummyWindow.Loaded());
        }
#if BROWSER
        // (browser build: the page shows the main window's content, scaled to fit, in place of the dummy window that opens it)
        else if (ApplicationLifetime is ISingleViewApplicationLifetime sv)
        {
            Dispatcher.UIThread.UnhandledException += (_, e) => Console.WriteLine($"UNHANDLED (UI thread): {e.Exception}");   // (the page's console, in place of a crash log)
            BrowserStorage.StartSaving();   // (keeps settings and saves in the browser's storage)
            // (black behind it, so the space around the tracker matches the page instead of the Light theme's white)
            sv.MainView = new Border { Background = Brushes.Black, Child = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both, Child = new Program.MyWindow() } };
        }
#endif
        base.OnFrameworkInitializationCompleted();
    }
}
