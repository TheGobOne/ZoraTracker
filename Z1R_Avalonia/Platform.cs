// Everything that differs between Windows, macOS and Linux goes through this class, so the ported WPF files stay
// platform-neutral. Anything here that is still a stub is listed under "Stubs" in docs/avalonia-port/PROGRESS.md
// and has to be replaced before parity.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Z1R_Avalonia;

public static class Platform
{
    public static readonly bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static readonly bool isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    public static readonly bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    // stubs log once per feature, so a missing feature shows up in the console without flooding it
    private static readonly HashSet<string> reportedStubs = new HashSet<string>();
    public static void stub(string feature)
    {
        bool isNew;
        lock (reportedStubs) { isNew = reportedStubs.Add(feature); }
        if (isNew)
            Console.WriteLine($"[platform stub] {feature} is not implemented on this platform yet");
    }

    ////////////////////////////////////////////////////////////
    // files

    /// <summary>Folder the app was installed to: read-only files that ship with the app (sounds, README, ShowRunCustomImages).</summary>
    public static string AppDir() => AppContext.BaseDirectory;

    /// <summary>Folder for files the user or the app writes: settings, HotKeys.txt, crash logs, ShowRunCustom.txt, ExtraIcons, overworld tile cache.
    /// On Windows this is the app folder, exactly like the WPF tracker. On macOS a signed .app can't be written to, so it's
    /// ~/Library/Application Support/ZoraTracker; on Linux it's $XDG_CONFIG_HOME/ZoraTracker (default ~/.config/ZoraTracker).
    /// (The macOS/Linux location is waiting on TheGobOne's sign-off: risk 10 in PLAN.md.)</summary>
    public static string UserDataDir()
    {
        string dir;
        if (isWindows)
            dir = AppContext.BaseDirectory;
        else if (isMacOS)
            dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "ZoraTracker");
        else
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var baseDir = string.IsNullOrEmpty(xdg) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config") : xdg;
            dir = Path.Combine(baseDir, "ZoraTracker");
        }
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static class Files
    {
        /// <summary>A path to a user file (see UserDataDir). Pass path segments separately; never use backslashes.</summary>
        public static string User(params string[] parts) { var all = new string[parts.Length + 1]; all[0] = UserDataDir(); parts.CopyTo(all, 1); return Path.Combine(all); }
        /// <summary>A path to a file that ships with the app (see AppDir).</summary>
        public static string App(params string[] parts) { var all = new string[parts.Length + 1]; all[0] = AppDir(); parts.CopyTo(all, 1); return Path.Combine(all); }
    }

    /// <summary>Open a folder, file or URL with the system's default handler (Explorer / Finder / xdg-open).</summary>
    public static void OpenWithShell(string target)
    {
        try
        {
            if (isWindows)
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            else if (isMacOS)
                Process.Start("open", new[] { target });
            else
                Process.Start("xdg-open", new[] { target });
        }
        catch (Exception e)
        {
            Console.WriteLine($"could not open '{target}': {e.Message}");
        }
    }

    /// <summary>Open the file manager with this file selected (WPF: `Explorer.exe /Select, "file"`). macOS: Finder (`open -R`).
    /// Linux has no standard way to select a file, so its folder is opened.</summary>
    public static void RevealInFileManager(string file)
    {
        try
        {
            if (isWindows)
                Process.Start(new ProcessStartInfo("Explorer.exe", $"/Select, \"{file}\""));
            else if (isMacOS)
                Process.Start("open", new[] { "-R", file });
            else
                Process.Start("xdg-open", new[] { Path.GetDirectoryName(file) });
        }
        catch (Exception e)
        {
            Console.WriteLine($"could not reveal '{file}': {e.Message}");
        }
    }

    /// <summary>Start an executable in its own folder with no console window, as WPF's ShowRunCustom RUN does
    /// (stdout redirected, no shell execute). Exceptions propagate to the caller, as Process.Start's do in WPF.</summary>
    public static void StartProcessHidden(string fileName, string arguments)
    {
        Process.Start(new ProcessStartInfo
        {
            WorkingDirectory = Path.GetDirectoryName(fileName),
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    // Windows Job Object: ties child process lifetime to the parent process.
    // When this process exits for any reason (crash, task kill, window close),
    // the OS automatically kills all processes in the job.
    // (WPF: these declarations, the job handle and createSignalRJob() are in Dungeon.fs, used by startLocalSignalRHost; moved here unchanged.)
    private static class WinJob
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, IntPtr lpName);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetInformationJobObject(IntPtr hJob, int infoClass, IntPtr lpInfo, uint cbInfoLength);
    }

    private static IntPtr childProcessJobHandle = IntPtr.Zero;   // (WPF: Dungeon.signalRJobHandle)

    private static void createChildProcessJob()   // (WPF: Dungeon.createSignalRJob)
    {
        try
        {
            var job = WinJob.CreateJobObject(IntPtr.Zero, IntPtr.Zero);
            if (job != IntPtr.Zero)
            {
                // LimitFlags is always at offset 16 (after two LARGE_INTEGERs) regardless of bitness.
                // The total struct size depends on compiler alignment settings; try all plausible values:
                //   x64 natural alignment: 144  |  x64 pack=4: 136
                //   x86 with 8-byte-aligned ULONGLONG: 112  |  x86 pack=4: 108
                var JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
                var JobObjectExtendedLimitInformation = 9;
                var sizesToTry =
                    IntPtr.Size == 8 ? new[] { 144, 136 }
                    : new[] { 112, 108 };
                var jobSet = false;
                foreach (var structSize in sizesToTry)
                {
                    if (!jobSet)
                    {
                        var ptr = Marshal.AllocHGlobal(structSize);
                        try
                        {
                            for (int i = 0; i <= structSize - 1; i++)
                                Marshal.WriteByte(ptr, i, 0);
                            Marshal.WriteInt32(ptr, 16, JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE);
                            if (WinJob.SetInformationJobObject(job, JobObjectExtendedLimitInformation, ptr, (uint)structSize))
                            {
                                childProcessJobHandle = job;
                                jobSet = true;
                            }
                            else
                            {
                                var err = Marshal.GetLastWin32Error();
                                if (err != 24)  // 24 = ERROR_BAD_LENGTH; only retry for that specific error
                                {
                                    Console.WriteLine($"[Sync] SetInformationJobObject failed (error {err})");
                                    jobSet = true;  // non-recoverable error; stop trying other sizes
                                }
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                }
                if (!jobSet)
                    Console.WriteLine($"[Sync] SetInformationJobObject: no struct size worked ({(IntPtr.Size == 8 ? "x64" : "x86")})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Sync] Could not create job object: {ex.Message}");
        }
    }

    /// <summary>Start an executable with no window (no shell execute), tied to this app's lifetime, as WPF's Dungeon.startLocalSignalRHost
    /// does for Z1R_SignalRHost.exe. On Windows the process goes in a Job Object with KILL_ON_JOB_CLOSE, so the OS kills it whenever the app
    /// exits for any reason. Exceptions from Process.Start propagate to the caller, as in WPF. Returns the process, or null where this isn't
    /// implemented yet (macOS/Linux: stub; co-op is phase 4).</summary>
    public static Process StartChildProcessTiedToApp(string fileName, string arguments)
    {
        if (!isWindows)
        {
            stub("starting a child process tied to the app's lifetime (co-op host; Windows uses a Job Object)");
            return null;
        }
        // Defer job creation to first actual use; calling at module-load time (do) can trigger
        // SetInformationJobObject error 24 on first launch due to static-initializer ordering.
        if (childProcessJobHandle == IntPtr.Zero)
            createChildProcessJob();
        var psi = new ProcessStartInfo();
        psi.FileName = fileName;
        psi.Arguments = arguments;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        var proc = Process.Start(psi);
        // Add to job object so it is killed automatically if ZTracker exits for any reason
        if (childProcessJobHandle != IntPtr.Zero)
            WinJob.AssignProcessToJobObject(childProcessJobHandle, proc.Handle);
        return proc;
    }

    /// <summary>Path of the running executable, for restarting the app.</summary>
    public static string ExecutablePath() => Environment.ProcessPath;

    ////////////////////////////////////////////////////////////
    // global hotkeys, the console window and the app shortcut (WPF: Program.fs's `Winterop` module, user32/kernel32/shell32 calls)

    private static class WinProgram
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }

    // Global hotkeys use SharpHook's global keyboard hook (libuiohook) on every OS, in place of WPF's user32 RegisterHotKey. As with RegisterHotKey,
    // a registered key fires only with exactly the registered modifiers, and is swallowed so no other app sees it (Windows and macOS; X11 can't
    // swallow keys). The key reaches the window the way WPF's does: as a WM_HOTKEY message to the hooks added with AddWndProcHook, on the UI thread.
    // macOS: the hook needs Accessibility permission; macOS asks for it the first time, and without it there are no global hotkeys.
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8;
    private sealed record HotKeyRegistration(Avalonia.Controls.Window Window, int Id, uint Modifiers, uint Vk);
    private static readonly List<HotKeyRegistration> hotKeys = new();
    private static SharpHook.SimpleGlobalHook keyboardHook;   // (SimpleGlobalHook: handlers run on the hook's thread, which is what lets them swallow keys)

    // Windows virtual-key codes for F1-F24 (the only keys the tracker registers) as SharpHook key codes
    private static SharpHook.Data.KeyCode keyCodeOfVk(uint vk) =>
        vk >= 0x70 && vk <= 0x87 ? Enum.Parse<SharpHook.Data.KeyCode>("VcF" + (vk - 0x70 + 1)) : SharpHook.Data.KeyCode.VcUndefined;
    private static uint vkOfKeyCode(SharpHook.Data.KeyCode k)
    {
        for (uint vk = 0x70; vk <= 0x87; vk++)
            if (keyCodeOfVk(vk) == k) return vk;
        return 0;
    }
    private static uint modifiersOf(SharpHook.Data.EventMask m) =>
        (m.HasFlag(SharpHook.Data.EventMask.LeftAlt) || m.HasFlag(SharpHook.Data.EventMask.RightAlt) ? MOD_ALT : 0) |
        (m.HasFlag(SharpHook.Data.EventMask.LeftCtrl) || m.HasFlag(SharpHook.Data.EventMask.RightCtrl) ? MOD_CONTROL : 0) |
        (m.HasFlag(SharpHook.Data.EventMask.LeftShift) || m.HasFlag(SharpHook.Data.EventMask.RightShift) ? MOD_SHIFT : 0) |
        (m.HasFlag(SharpHook.Data.EventMask.LeftMeta) || m.HasFlag(SharpHook.Data.EventMask.RightMeta) ? MOD_WIN : 0);

    private static void onGlobalKeyPressed(object sender, SharpHook.KeyboardHookEventArgs e)
    {
        var vk = vkOfKeyCode(e.Data.KeyCode);
        if (vk == 0) return;
        var mods = modifiersOf(e.RawEvent.Mask);
        List<HotKeyRegistration> hits;
        lock (hotKeys) hits = hotKeys.FindAll(h => h.Vk == vk && h.Modifiers == mods);
        if (hits.Count == 0) return;
        e.SuppressEvent = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            foreach (var h in hits)
            {
                // WM_HOTKEY: wParam is the hotkey id, lParam has the modifiers in its low word and the virtual-key code in its high word
                var lParam = new IntPtr((int)((vk << 16) | mods));
                List<WndProcHook> hooks;
                lock (wndProcHooks) hooks = wndProcHooks.TryGetValue(h.Window, out var l) ? new List<WndProcHook>(l) : new List<WndProcHook>();
                foreach (var hook in hooks)
                {
                    bool handled = false;
                    hook(IntPtr.Zero, WM_HOTKEY, new IntPtr(h.Id), lParam, ref handled);
                    if (handled) break;
                }
            }
        });
    }

    private static void ensureKeyboardHook()
    {
        if (keyboardHook != null) return;
        keyboardHook = new SharpHook.SimpleGlobalHook();
        keyboardHook.KeyPressed += onGlobalKeyPressed;
        keyboardHook.RunAsync(SharpHook.Data.GlobalHookType.Keyboard, useBackgroundThread: true).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                var ex = t.Exception.GetBaseException();
                var why = ex is SharpHook.HookException he && (he.Result == SharpHook.Data.UioHookResult.ErrorAxApiDisabled || he.Result == SharpHook.Data.UioHookResult.ErrorAxApiRevoked)
                    ? "it needs Accessibility permission (System Settings > Privacy & Security > Accessibility), then a restart of the tracker"
                    : ex.Message;
                Console.WriteLine($"global hotkeys are unavailable: {why}");
            }
        });
    }

    /// <summary>A system-wide hotkey for this window (WPF: user32 `RegisterHotKey(hWnd, id, fsModifiers, vk)`); the key arrives as WM_HOTKEY at the
    /// hooks added with AddWndProcHook. Every OS: SharpHook's global keyboard hook (see above). Only F1-F24 are supported (all the tracker uses);
    /// returns false for other keys.</summary>
    public static bool RegisterHotKey(Avalonia.Controls.Window w, int id, uint fsModifiers, uint vk)
    {
        if (keyCodeOfVk(vk) == SharpHook.Data.KeyCode.VcUndefined) return false;
        lock (hotKeys) hotKeys.Add(new HotKeyRegistration(w, id, fsModifiers & (MOD_ALT | MOD_CONTROL | MOD_SHIFT | MOD_WIN), vk));
        ensureKeyboardHook();
        return true;
    }
    public static bool UnregisterHotKey(Avalonia.Controls.Window w, int id)
    {
        lock (hotKeys) return hotKeys.RemoveAll(h => h.Window == w && h.Id == id) > 0;
    }

    /// <summary>A window-procedure hook (WPF: `HwndSource.AddHook`). Return IntPtr.Zero and leave `handled` alone to let the message through.
    /// Only WM_HOTKEY is ever delivered (see RegisterHotKey), on every OS.</summary>
    public delegate IntPtr WndProcHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);
    private static readonly Dictionary<Avalonia.Controls.Window, List<WndProcHook>> wndProcHooks = new();
    public static void AddWndProcHook(Avalonia.Controls.Window w, WndProcHook hook)
    {
        lock (wndProcHooks)
        {
            if (!wndProcHooks.TryGetValue(w, out var l)) wndProcHooks[w] = l = new List<WndProcHook>();
            l.Add(hook);
        }
    }
    public static void RemoveWndProcHook(Avalonia.Controls.Window w, WndProcHook hook)
    {
        lock (wndProcHooks)
        {
            if (wndProcHooks.TryGetValue(w, out var l) && l.Remove(hook) && l.Count == 0)
                wndProcHooks.Remove(w);
        }
    }

    /// <summary>The console window (WPF: kernel32 `GetConsoleWindow()`); IntPtr.Zero when there is none, and always off Windows.</summary>
    public static IntPtr GetConsoleWindow() => isWindows ? WinProgram.GetConsoleWindow() : IntPtr.Zero;
    /// <summary>Shows or hides a window by handle (WPF: user32 `ShowWindow(hWnd, nCmdShow)`, used on the console window). macOS/Linux: a stub
    /// (the console there is the terminal the app was started from, if any, and stays as it is).</summary>
    public static bool ShowWindow(IntPtr hWnd, int nCmdShow)
    {
        if (isWindows) return WinProgram.ShowWindow(hWnd, nCmdShow);
        stub("showing/hiding the console window (macOS/Linux: the terminal, if any, is left alone)");
        return false;
    }

    /// <summary>Writes a shortcut file that launches the app (WPF: an `IShellLink` saved via `IPersistFile` as ZTracker.lnk next to the exe).
    /// Windows: the same shell COM calls as WPF. macOS/Linux: a stub (macOS: the .app in Applications; Linux: a .desktop file).</summary>
    public static void CreateShortcut(string lnkPath, string targetPath, string workingDirectory, string iconPath, string description)
    {
        if (OperatingSystem.IsWindows()) WinShellLink.Create(lnkPath, targetPath, workingDirectory, iconPath, description);
        else stub("app shortcut (WPF: ZTracker.lnk next to the exe; macOS: the .app in Applications, Linux: a .desktop file)");
    }

    [SupportedOSPlatform("windows")]
    private static class WinShellLink
    {
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cchMaxPath, [Out] IntPtr pfd, int fFlags);
            void GetIDList([Out] IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey([Out] short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd([Out] int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cchIconPath, [Out] int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }
        public static void Create(string lnkPath, string targetPath, string workingDirectory, string iconPath, string description)
        {
            var shellLink = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046")));
            var isl = (IShellLink)shellLink;
            isl.SetDescription(description);
            isl.SetPath(targetPath);
            isl.SetWorkingDirectory(workingDirectory);
            isl.SetIconLocation(iconPath, 0);
            var ipf = (System.Runtime.InteropServices.ComTypes.IPersistFile)isl;
            ipf.Save(lnkPath, false);
        }
    }

    ////////////////////////////////////////////////////////////
    // mouse

    // Fake mouse input (hotkeys can act as mouse buttons and the scroll wheel) uses SharpHook's event simulator on every OS.
    // macOS: it needs Accessibility permission; macOS asks for it the first time, and without it nothing happens.
    private static SharpHook.Simulation.EventSimulator eventSimulator;
    private static bool eventSimulatorFailed;
    private static SharpHook.Simulation.EventSimulator simulator()
    {
        if (eventSimulator == null && !eventSimulatorFailed)
        {
            try { eventSimulator = SharpHook.Simulation.EventSimulator.Create("ZoraTracker"); }   // (on Linux this sets up virtual input devices)
            catch (Exception e)
            {
                eventSimulatorFailed = true;
                Console.WriteLine($"fake mouse input is unavailable: {e.Message}");
            }
        }
        return eventSimulator;
    }
    private static void reportSimulation(SharpHook.Data.UioHookResult r)
    {
        if (r == SharpHook.Data.UioHookResult.ErrorAxApiDisabled || r == SharpHook.Data.UioHookResult.ErrorAxApiRevoked)
            stub("fake mouse input without Accessibility permission (System Settings > Privacy & Security > Accessibility)");
        else if (r != SharpHook.Data.UioHookResult.Success)
            stub($"fake mouse input ({r})");
    }

    // Cursor warping stays one OS call each: SharpHook's warp on macOS needs Accessibility permission, which CGWarpMouseCursorPosition doesn't,
    // and SetCursorPos is exactly what WPF calls. Linux uses SharpHook.
    private static class Win
    {
        [DllImport("User32.dll")]
        public static extern bool SetCursorPos(int X, int Y);
    }

    private static class Mac
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct CGPoint { public double X; public double Y; public CGPoint(double x, double y) { X = x; Y = y; } }
        const string CG = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        const string CF = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        [DllImport(CG)] public static extern int CGWarpMouseCursorPosition(CGPoint newCursorPosition);
        [DllImport(CG)] public static extern int CGAssociateMouseAndMouseCursorPosition(int connected);
        [DllImport(CG)] public static extern IntPtr CGEventCreateMouseEvent(IntPtr source, uint mouseType, CGPoint mouseCursorPosition, uint mouseButton);
        [DllImport(CG)] public static extern void CGEventPost(uint tap, IntPtr ev);
        [DllImport(CF)] public static extern void CFRelease(IntPtr cf);
        public const uint kCGHIDEventTap = 0;
        public const uint kCGEventMouseMoved = 5;
        public const uint kCGMouseButtonLeft = 0;
        public static void post(IntPtr ev)
        {
            if (ev != IntPtr.Zero)
            {
                CGEventPost(kCGHIDEventTap, ev);
                CFRelease(ev);
            }
        }
    }

    /// <summary>Moves the mouse cursor. (x, y) are screen coordinates in physical pixels, as returned by Avalonia's PointToScreen;
    /// desktopScaling is the window's DesktopScaling. Returns false where cursor warping isn't supported (no warp happened).</summary>
    public static bool WarpCursor(int x, int y, double desktopScaling)
    {
        if (isWindows)
        {
            Win.SetCursorPos(x, y);
            return true;
        }
        else if (isMacOS)
        {
            // Avalonia reports macOS screen coordinates in pixels; CoreGraphics wants points (top-left origin, like Avalonia)
            var p = new Mac.CGPoint(x / desktopScaling, y / desktopScaling);
            Mac.CGWarpMouseCursorPosition(p);
            // re-associating the mouse cancels the ~0.25s in which macOS ignores mouse movement after a warp
            Mac.CGAssociateMouseAndMouseCursorPosition(1);
            // a warp doesn't produce a mouse-moved event on macOS, but the app waits for one (to update hover state);
            // posting one works only once the user has granted Accessibility permission, and is silently dropped otherwise
            Mac.post(Mac.CGEventCreateMouseEvent(IntPtr.Zero, Mac.kCGEventMouseMoved, p, Mac.kCGMouseButtonLeft));
            return true;
        }
        else
        {
            // X11 coordinates are pixels, as Avalonia's (Wayland doesn't let apps move the cursor; SharpHook then reports an error)
            var sim = simulator();
            if (sim == null) return false;
            var r = sim.SimulateMouseMovement((short)x, (short)y);
            reportSimulation(r);
            return r == SharpHook.Data.UioHookResult.Success;
        }
    }

    // Fake mouse input, sent at the cursor's current position.
    private static void click(SharpHook.Data.MouseButton b)
    {
        var sim = simulator();
        if (sim == null) return;
        var r = sim.SimulateMousePress(b);
        if (r == SharpHook.Data.UioHookResult.Success) r = sim.SimulateMouseRelease(b);
        reportSimulation(r);
    }
    // one notch of the wheel: Windows and Linux count 120 per notch (WPF: WHEEL_DELTA); macOS counts lines
    private static readonly short wheelNotchRotation = isMacOS ? (short)1 : (short)120;
    private static void wheel(short rotation)
    {
        var sim = simulator();
        if (sim == null) return;
        reportSimulation(sim.SimulateMouseWheel(rotation, SharpHook.Data.MouseWheelScrollDirection.Vertical, SharpHook.Data.MouseWheelScrollType.UnitScroll));
    }
    public static void LeftMouseClick() => click(SharpHook.Data.MouseButton.Button1);
    public static void RightMouseClick() => click(SharpHook.Data.MouseButton.Button2);
    public static void MiddleMouseClick() => click(SharpHook.Data.MouseButton.Button3);
    public static void ScrollWheelRotateUp() => wheel(wheelNotchRotation);
    public static void ScrollWheelRotateDown() => wheel((short)-wheelNotchRotation);

    /// <summary>The Windows "show location of pointer when I press Ctrl" setting (mouse sonar). Only referenced from code the WPF
    /// tracker has commented out; risk 8 in PLAN.md.</summary>
    public static int SetMouseSonar(bool enable)
    {
        stub("mouse sonar");
        return 0;
    }

    ////////////////////////////////////////////////////////////
    // sound

    // Sounds play through SDL's audio on every OS (WPF: one MediaPlayer per sound file). As WPF's players, each file plays on its own stream,
    // and playing a file again restarts it from the beginning.
    private sealed unsafe class LoadedSound
    {
        public SDL.SDL_AudioStream* Stream;
        public IntPtr Data;
        public int Length;
    }
    private static readonly Dictionary<string, LoadedSound> loadedSounds = new();
    private static bool sdlAudioFailed;

    private static unsafe LoadedSound loadSound(string fileName)
    {
        if (loadedSounds.TryGetValue(fileName, out var s)) return s;
        if (sdlAudioFailed) return null;
        if (!Sdl.InitSubSystem(SDL.SDL_InitFlags.SDL_INIT_AUDIO))
        {
            sdlAudioFailed = true;
            Console.WriteLine($"sound is unavailable: {SDL.SDL3.SDL_GetError()}");
            return null;
        }
        SDL.SDL_AudioSpec spec;
        byte* buf;
        uint len;
        if (!SDL.SDL3.SDL_LoadWAV(fileName, &spec, &buf, &len))
        {
            Console.WriteLine($"could not load {Path.GetFileName(fileName)}: {SDL.SDL3.SDL_GetError()}");
            loadedSounds[fileName] = null;
            return null;
        }
        var stream = SDL.SDL3.SDL_OpenAudioDeviceStream(SDL.SDL3.SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, &spec, null, IntPtr.Zero);
        if (stream == null)
        {
            Console.WriteLine($"could not open an audio device: {SDL.SDL3.SDL_GetError()}");
            SDL.SDL3.SDL_free(buf);
            loadedSounds[fileName] = null;
            return null;
        }
        SDL.SDL3.SDL_ResumeAudioStreamDevice(stream);
        s = new LoadedSound { Stream = stream, Data = (IntPtr)buf, Length = (int)len };
        loadedSounds[fileName] = s;
        return s;
    }

    /// <summary>Plays a .wav file from the app folder, from the start (stopping that file if it's still playing). volume is 0.0-1.0.</summary>
    public static void PlaySound(string fileName, double volume)
    {
        unsafe
        {
            lock (loadedSounds)
            {
                var s = loadSound(fileName);
                if (s == null) return;
                SDL.SDL3.SDL_ClearAudioStream(s.Stream);
                SDL.SDL3.SDL_SetAudioStreamGain(s.Stream, (float)volume);
                SDL.SDL3.SDL_PutAudioStreamData(s.Stream, s.Data, s.Length);
            }
        }
    }

    private static class WinBeep
    {
        [DllImport("user32.dll")]
        public static extern bool MessageBeep(uint uType);
        public const uint MB_ICONASTERISK = 0x40;
    }
    private static class MacBeep
    {
        [DllImport("/System/Library/Frameworks/AppKit.framework/AppKit")]
        public static extern void NSBeep();
    }
    /// <summary>The system "asterisk" sound (WPF: SystemSounds.Asterisk, which is MessageBeep(MB_ICONASTERISK)). macOS: the system alert sound
    /// (NSBeep). Linux has no standard alert sound, so a short tone plays through SDL.</summary>
    public static void SystemBeep()
    {
        if (isWindows) WinBeep.MessageBeep(WinBeep.MB_ICONASTERISK);
        else if (isMacOS) MacBeep.NSBeep();
        else playBeepTone();
    }
    private static unsafe void playBeepTone()
    {
        lock (loadedSounds)
        {
            const string key = "(beep)";
            if (!loadedSounds.TryGetValue(key, out var s))
            {
                s = null;
                if (!sdlAudioFailed && Sdl.InitSubSystem(SDL.SDL_InitFlags.SDL_INIT_AUDIO))
                {
                    // 120ms of an 880Hz sine with short fades, mono 16-bit at 44.1kHz
                    const int rate = 44100, n = rate * 120 / 1000;
                    var data = (short*)NativeMemory.Alloc((nuint)(n * sizeof(short)));
                    for (int i = 0; i < n; i++)
                    {
                        double fade = Math.Min(1.0, Math.Min(i, n - i) / (rate * 0.01));
                        data[i] = (short)(Math.Sin(2 * Math.PI * 880 * i / rate) * fade * 12000);
                    }
                    var spec = new SDL.SDL_AudioSpec { format = SDL.SDL_AudioFormat.SDL_AUDIO_S16LE, channels = 1, freq = rate };
                    var stream = SDL.SDL3.SDL_OpenAudioDeviceStream(SDL.SDL3.SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, &spec, null, IntPtr.Zero);
                    if (stream != null)
                    {
                        SDL.SDL3.SDL_ResumeAudioStreamDevice(stream);
                        s = new LoadedSound { Stream = stream, Data = (IntPtr)data, Length = n * sizeof(short) };
                    }
                }
                loadedSounds[key] = s;
            }
            if (s == null) return;
            SDL.SDL3.SDL_ClearAudioStream(s.Stream);
            SDL.SDL3.SDL_PutAudioStreamData(s.Stream, s.Data, s.Length);
        }
    }

    /// <summary>SDL (sound, and the gamepad in Gamepad.cs), started one subsystem at a time as it's first needed. SDL never gets a window here,
    /// so it's told to keep reading the gamepad while another app has focus, as WPF's DirectInput did.</summary>
    public static class Sdl
    {
        private static bool hintsSet;
        public static bool InitSubSystem(SDL.SDL_InitFlags flags)
        {
            lock (typeof(Sdl))
            {
                if (!hintsSet)
                {
                    SDL.SDL3.SDL_SetHint(SDL.SDL3.SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1");
                    SDL.SDL3.SDL_SetHint(SDL.SDL3.SDL_HINT_APP_NAME, "Z1R Tracker");
                    hintsSet = true;
                }
                return SDL.SDL3.SDL_InitSubSystem(flags);
            }
        }
    }

    ////////////////////////////////////////////////////////////
    // speech synthesis

    /// <summary>One installed voice, as WPF's System.Speech `InstalledVoice` (`v.VoiceInfo.Name`, `v.Enabled`).</summary>
    public sealed record InstalledVoice(string Name, bool Enabled);

    /// <summary>The text-to-speech voice (WPF: a System.Speech.Synthesis.SpeechSynthesizer), with the members the tracker uses.
    /// Windows: System.Speech (the System.Speech NuGet package), so it behaves as in WPF. macOS/Linux: a stub (risk 6) that keeps
    /// Volume, lists no voices, logs once and prints each spoken text instead of speaking it.</summary>
    public sealed class SpeechSynthesizer
    {
        private readonly object impl;   // a System.Speech.Synthesis.SpeechSynthesizer on Windows, else null
        private int stubVolume = 100;   // (System.Speech's default; only the stub uses this)
        public SpeechSynthesizer()
        {
            if (OperatingSystem.IsWindows())
                impl = WinSpeech.Create();
        }
        /// <summary>0-100 (WPF: SpeechSynthesizer.Volume).</summary>
        public int Volume
        {
            get => OperatingSystem.IsWindows() ? WinSpeech.GetVolume(impl) : stubVolume;
            set { if (OperatingSystem.IsWindows()) WinSpeech.SetVolume(impl, value); else stubVolume = value; }
        }
        /// <summary>The current voice's name (WPF: `voice.Voice.Name`). The stub returns "".</summary>
        public string VoiceName
        {
            get
            {
                if (OperatingSystem.IsWindows()) return WinSpeech.GetVoiceName(impl);
                stub("speech synthesis voices (macOS: AVSpeechSynthesizer, Linux: speech-dispatcher)");
                return "";
            }
        }
        /// <summary>WPF: `voice.GetInstalledVoices()`. The stub returns no voices.</summary>
        public List<InstalledVoice> GetInstalledVoices()
        {
            if (OperatingSystem.IsWindows()) return WinSpeech.GetInstalledVoices(impl);
            stub("speech synthesis voices (macOS: AVSpeechSynthesizer, Linux: speech-dispatcher)");
            return new List<InstalledVoice>();
        }
        /// <summary>WPF: `voice.SelectVoice(name)`. On Windows it throws if there's no such enabled voice, as System.Speech does (callers catch it).
        /// The stub does nothing.</summary>
        public void SelectVoice(string name)
        {
            if (OperatingSystem.IsWindows()) WinSpeech.SelectVoice(impl, name);
            else stub("speech synthesis voices (macOS: AVSpeechSynthesizer, Linux: speech-dispatcher)");
        }
        /// <summary>Speaks the text and returns when it's done (WPF: `voice.Speak(text)`, synchronous). The stub prints the text.</summary>
        public void Speak(string text)
        {
            if (OperatingSystem.IsWindows()) WinSpeech.Speak(impl, text);
            else
            {
                stub("speech synthesis (macOS: AVSpeechSynthesizer, Linux: speech-dispatcher)");
                Console.WriteLine($"[speech] {text}");
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static class WinSpeech
    {
        private static System.Speech.Synthesis.SpeechSynthesizer s(object o) => (System.Speech.Synthesis.SpeechSynthesizer)o;
        public static object Create() => new System.Speech.Synthesis.SpeechSynthesizer();
        public static int GetVolume(object o) => s(o).Volume;
        public static void SetVolume(object o, int v) => s(o).Volume = v;
        public static string GetVoiceName(object o) => s(o).Voice.Name;
        public static List<InstalledVoice> GetInstalledVoices(object o)
        {
            var r = new List<InstalledVoice>();
            foreach (var v in s(o).GetInstalledVoices())
                r.Add(new InstalledVoice(v.VoiceInfo.Name, v.Enabled));
            return r;
        }
        public static void SelectVoice(object o, string name) => s(o).SelectVoice(name);
        public static void Speak(object o, string text) => s(o).Speak(text);
    }

    ////////////////////////////////////////////////////////////
    // speech recognition

    /// <summary>One recognized phrase, as WPF's System.Speech `SpeechRecognizedEventArgs.Result` (`r.Result.Text`, `r.Result.Confidence`).</summary>
    public sealed record RecognizedSpeech(string Text, float Confidence);

    /// <summary>The speech recognizer (WPF: a System.Speech.Recognition.SpeechRecognitionEngine), with the members the tracker uses.
    /// Windows: System.Speech (the System.Speech NuGet package, which includes System.Speech.Recognition), so it behaves as in WPF.
    /// macOS/Linux: a stub (risk 4) that accepts grammars, logs once and never recognizes anything.</summary>
    public sealed class SpeechRecognitionEngine
    {
        private readonly object impl;   // a System.Speech.Recognition.SpeechRecognitionEngine on Windows, else null
        /// <summary>Raised (on a background thread, as System.Speech does) for each recognized phrase (WPF: `SpeechRecognized`).</summary>
        public event Action<RecognizedSpeech> SpeechRecognized;
        public SpeechRecognitionEngine()
        {
            if (OperatingSystem.IsWindows())
            {
                impl = WinSpeechRecognition.Create(r => SpeechRecognized?.Invoke(r));
                Console.WriteLine("speech recognition: System.Speech (Windows)");
            }
            else
                Console.WriteLine("speech recognition: not available on this platform yet (stub; nothing will be recognized)");
        }
        /// <summary>WPF: `LoadGrammar(new Grammar(let gb = new GrammarBuilder(wakePhrase) in gb.Append(new Choices(choices)); gb))`.</summary>
        public void LoadGrammar(string wakePhrase, string[] choices)
        {
            if (OperatingSystem.IsWindows()) WinSpeechRecognition.LoadGrammar(impl, wakePhrase, choices);
            else stub("speech recognition (planned: Vosk with a restricted grammar, or a native helper per OS)");
        }
        /// <summary>WPF: `SetInputToDefaultAudioDevice()`. On Windows it throws if there's no microphone, as System.Speech does.</summary>
        public void SetInputToDefaultAudioDevice()
        {
            if (OperatingSystem.IsWindows()) WinSpeechRecognition.SetInputToDefaultAudioDevice(impl);
            else stub("speech recognition (planned: Vosk with a restricted grammar, or a native helper per OS)");
        }
        /// <summary>WPF: `RecognizeAsync(RecognizeMode.Multiple)`.</summary>
        public void RecognizeAsyncMultiple()
        {
            if (OperatingSystem.IsWindows()) WinSpeechRecognition.RecognizeAsyncMultiple(impl);
            else stub("speech recognition (planned: Vosk with a restricted grammar, or a native helper per OS)");
        }
    }

    [SupportedOSPlatform("windows")]
    private static class WinSpeechRecognition
    {
        private static System.Speech.Recognition.SpeechRecognitionEngine e(object o) => (System.Speech.Recognition.SpeechRecognitionEngine)o;
        public static object Create(Action<RecognizedSpeech> onRecognized)
        {
            var engine = new System.Speech.Recognition.SpeechRecognitionEngine();
            engine.SpeechRecognized += (_, r) => onRecognized(new RecognizedSpeech(r.Result.Text, r.Result.Confidence));
            return engine;
        }
        public static void LoadGrammar(object o, string wakePhrase, string[] choices)
        {
            var gb = new System.Speech.Recognition.GrammarBuilder(wakePhrase);
            gb.Append(new System.Speech.Recognition.Choices(choices));
            e(o).LoadGrammar(new System.Speech.Recognition.Grammar(gb));
        }
        public static void SetInputToDefaultAudioDevice(object o) => e(o).SetInputToDefaultAudioDevice();
        public static void RecognizeAsyncMultiple(object o) => e(o).RecognizeAsync(System.Speech.Recognition.RecognizeMode.Multiple);
    }
}
