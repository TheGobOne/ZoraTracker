// Everything that differs between Windows, macOS and Linux goes through this class, so the ported WPF files stay
// platform-neutral. Anything here that is still a stub is listed under "Stubs" in docs/avalonia-port/PROGRESS.md
// and has to be replaced before parity.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

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

    /// <summary>Path of the running executable, for restarting the app.</summary>
    public static string ExecutablePath() => Environment.ProcessPath;

    ////////////////////////////////////////////////////////////
    // mouse

    private static class Win
    {
        [DllImport("User32.dll")]
        public static extern bool SetCursorPos(int X, int Y);
        [DllImport("User32.dll")]
        public static extern void mouse_event(uint dwFlags, int dx, int dy, uint cButtons, int dwExtraInfo);
        public const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        public const uint MOUSEEVENTF_LEFTUP = 0x04;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
        public const uint MOUSEEVENTF_RIGHTUP = 0x10;
        public const uint MOUSEEVENTF_MIDDLEDOWN = 0x20;
        public const uint MOUSEEVENTF_MIDDLEUP = 0x40;
        public const uint MOUSEEVENTF_WHEEL = 0x800;
        public const int WHEEL_DELTA = 120;
    }

    private static class Mac
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct CGPoint { public double X; public double Y; public CGPoint(double x, double y) { X = x; Y = y; } }
        const string CG = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        const string CF = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        [DllImport(CG)] public static extern int CGWarpMouseCursorPosition(CGPoint newCursorPosition);
        [DllImport(CG)] public static extern int CGAssociateMouseAndMouseCursorPosition(int connected);
        [DllImport(CG)] public static extern IntPtr CGEventCreate(IntPtr source);
        [DllImport(CG)] public static extern CGPoint CGEventGetLocation(IntPtr ev);
        [DllImport(CG)] public static extern IntPtr CGEventCreateMouseEvent(IntPtr source, uint mouseType, CGPoint mouseCursorPosition, uint mouseButton);
        // the non-variadic version: variadic P/Invoke isn't safe on Apple Silicon
        [DllImport(CG)] public static extern IntPtr CGEventCreateScrollWheelEvent2(IntPtr source, uint units, uint wheelCount, int wheel1, int wheel2, int wheel3);
        [DllImport(CG)] public static extern void CGEventPost(uint tap, IntPtr ev);
        [DllImport(CF)] public static extern void CFRelease(IntPtr cf);
        public const uint kCGHIDEventTap = 0;
        public const uint kCGEventLeftMouseDown = 1, kCGEventLeftMouseUp = 2;
        public const uint kCGEventRightMouseDown = 3, kCGEventRightMouseUp = 4;
        public const uint kCGEventMouseMoved = 5;
        public const uint kCGEventOtherMouseDown = 25, kCGEventOtherMouseUp = 26;
        public const uint kCGMouseButtonLeft = 0, kCGMouseButtonRight = 1, kCGMouseButtonCenter = 2;
        public const uint kCGScrollEventUnitLine = 1;
        public static CGPoint cursorLocation()
        {
            var ev = CGEventCreate(IntPtr.Zero);
            var p = CGEventGetLocation(ev);
            CFRelease(ev);
            return p;
        }
        public static void post(IntPtr ev)
        {
            if (ev != IntPtr.Zero)
            {
                CGEventPost(kCGHIDEventTap, ev);
                CFRelease(ev);
            }
        }
        public static void click(uint down, uint up, uint button)
        {
            var p = cursorLocation();
            post(CGEventCreateMouseEvent(IntPtr.Zero, down, p, button));
            post(CGEventCreateMouseEvent(IntPtr.Zero, up, p, button));
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
            stub("cursor warping (Linux: XWarpPointer)");
            return false;
        }
    }

    // Fake mouse input, sent at the cursor's current position (hotkeys can act as mouse buttons and the scroll wheel).
    // On macOS these need Accessibility permission, without which they're silently dropped.
    public static void LeftMouseClick()
    {
        if (isWindows) Win.mouse_event(Win.MOUSEEVENTF_LEFTDOWN | Win.MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        else if (isMacOS) Mac.click(Mac.kCGEventLeftMouseDown, Mac.kCGEventLeftMouseUp, Mac.kCGMouseButtonLeft);
        else stub("fake mouse clicks (Linux: XTest)");
    }
    public static void RightMouseClick()
    {
        if (isWindows) Win.mouse_event(Win.MOUSEEVENTF_RIGHTDOWN | Win.MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
        else if (isMacOS) Mac.click(Mac.kCGEventRightMouseDown, Mac.kCGEventRightMouseUp, Mac.kCGMouseButtonRight);
        else stub("fake mouse clicks (Linux: XTest)");
    }
    public static void MiddleMouseClick()
    {
        if (isWindows) Win.mouse_event(Win.MOUSEEVENTF_MIDDLEDOWN | Win.MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
        else if (isMacOS) Mac.click(Mac.kCGEventOtherMouseDown, Mac.kCGEventOtherMouseUp, Mac.kCGMouseButtonCenter);
        else stub("fake mouse clicks (Linux: XTest)");
    }
    public static void ScrollWheelRotateUp()
    {
        if (isWindows) Win.mouse_event(Win.MOUSEEVENTF_WHEEL, 0, 0, (uint)Win.WHEEL_DELTA, 0);
        else if (isMacOS) Mac.post(Mac.CGEventCreateScrollWheelEvent2(IntPtr.Zero, Mac.kCGScrollEventUnitLine, 1, 1, 0, 0));
        else stub("fake scroll wheel (Linux: XTest)");
    }
    public static void ScrollWheelRotateDown()
    {
        if (isWindows) Win.mouse_event(Win.MOUSEEVENTF_WHEEL, 0, 0, unchecked((uint)(-Win.WHEEL_DELTA)), 0);
        else if (isMacOS) Mac.post(Mac.CGEventCreateScrollWheelEvent2(IntPtr.Zero, Mac.kCGScrollEventUnitLine, 1, -1, 0, 0));
        else stub("fake scroll wheel (Linux: XTest)");
    }

    /// <summary>The Windows "show location of pointer when I press Ctrl" setting (mouse sonar). Only referenced from code the WPF
    /// tracker has commented out; risk 8 in PLAN.md.</summary>
    public static int SetMouseSonar(bool enable)
    {
        stub("mouse sonar");
        return 0;
    }

    ////////////////////////////////////////////////////////////
    // sound

    /// <summary>Plays a .wav file from the app folder. volume is 0.0-1.0.</summary>
    public static void PlaySound(string fileName, double volume)
    {
        stub($"sound playback ({Path.GetFileName(fileName)})");
    }

    /// <summary>The system "asterisk" sound (WPF: SystemSounds.Asterisk).</summary>
    public static void SystemBeep()
    {
        stub("system beep");
    }
}
