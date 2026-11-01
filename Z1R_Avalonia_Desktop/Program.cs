using System;
using Avalonia;

namespace Z1R_Avalonia_Desktop
{
    public static class Program
    {
        // Starts the ported tracker (Z1R_Avalonia.Program.main, the port of Z1R_WPF/Program.fs) by default.
        // Temporary, until the ported app is confirmed working at runtime (then both are removed):
        // --dungeon-preview: the dungeon-tracker-only window (phase 1); add --screenshot <file.png> to save a picture and quit
        // --prototype: the old F# prototype UI (Z1R_Avalonia_Prototype)
        [STAThread]
        public static int Main(string[] args)
        {
            if (Array.IndexOf(args, "--dungeon-preview") >= 0)
            {
                int s = Array.IndexOf(args, "--screenshot");
                if (s >= 0 && s + 1 < args.Length) Z1R_Avalonia.DungeonPreviewApp.ScreenshotPath = args[s + 1];
                return AppBuilder.Configure<Z1R_Avalonia.DungeonPreviewApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
            }
            if (Array.IndexOf(args, "--prototype") >= 0)
                return AppBuilder.Configure<Z1R_Avalonia.Prototype.App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
            return Z1R_Avalonia.Program.main(args, () => AppBuilder.Configure<Z1R_Avalonia.ZoraTrackerApp>().UsePlatformDetect());
        }
    }
}
