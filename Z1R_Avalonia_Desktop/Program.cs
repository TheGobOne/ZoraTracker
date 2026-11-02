using System;
using System.Threading;
using Avalonia;

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
            return Z1R_Avalonia.Program.main(args, () => AppBuilder.Configure<Z1R_Avalonia.ZoraTrackerApp>().UsePlatformDetect());
        }
    }
}
