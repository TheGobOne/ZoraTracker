using System;
using System.Threading;
using Avalonia;
using Avalonia.Headless;

namespace Z1R_Avalonia_Desktop
{
    public static class Program
    {
        // Starts the ported tracker (Z1R_Avalonia.Program.main, the port of Z1R_WPF/Program.fs).
        // --sound-test: plays the tracker's sounds once each, then quits (to check sound on a new OS or machine)
        [STAThread]
        public static int Main(string[] args)
        {
            if (Array.IndexOf(args, "--sound-test") >= 0)
            {
                Console.WriteLine("confirm_speech.wav (speech command recognised)");
                Z1R_Avalonia.Platform.PlaySound(Z1R_Avalonia.Platform.Files.App("confirm_speech.wav"), 0.5);
                Thread.Sleep(1500);
                Console.WriteLine("reminder_clink.wav (a reminder appears)");
                Z1R_Avalonia.Platform.PlaySound(Z1R_Avalonia.Platform.Files.App("reminder_clink.wav"), 0.5);
                Thread.Sleep(1500);
                Console.WriteLine("system beep (error beep)");
                Z1R_Avalonia.Platform.SystemBeep();
                Thread.Sleep(1500);
                return 0;
            }
            // --dump (see docs/load-dump.md) runs on Avalonia's headless platform: no window server, so a tool that starts it many times takes no focus
            // (both flags, as LoadDump.isActive needs: --dump= alone would run headless with nothing to load or to exit it, and a crash dialog would never close)
            var headless = Array.Exists(args, x => x.StartsWith("--dump=")) && Array.Exists(args, x => x.StartsWith("--load="));
            return Z1R_Avalonia.Program.main(args, () => {
                var builder = AppBuilder.Configure<Z1R_Avalonia.ZoraTrackerApp>();
                return headless ? builder.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true }) : builder.UsePlatformDetect();
            });
        }
    }
}
