using System;
using Avalonia;

namespace Z1R_Avalonia_Desktop
{
    public static class Program
    {
        // (still starts the prototype UI until the ported Program/WPFUI can run the app)
        // --dungeon-preview: the temporary dungeon-tracker-only window (phase 1); add --screenshot <file.png> to save a picture and quit
        [STAThread]
        public static int Main(string[] args)
        {
            if (Array.IndexOf(args, "--dungeon-preview") >= 0)
            {
                int s = Array.IndexOf(args, "--screenshot");
                if (s >= 0 && s + 1 < args.Length) Z1R_Avalonia.DungeonPreviewApp.ScreenshotPath = args[s + 1];
                return AppBuilder.Configure<Z1R_Avalonia.DungeonPreviewApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
            }
            return AppBuilder.Configure<Z1R_Avalonia.Prototype.App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
        }
    }
}
