using System;
using Avalonia;

namespace Z1R_Avalonia_Desktop
{
    public static class Program
    {
        // Starts the ported tracker (Z1R_Avalonia.Program.main, the port of Z1R_WPF/Program.fs).
        [STAThread]
        public static int Main(string[] args)
        {
            return Z1R_Avalonia.Program.main(args, () => AppBuilder.Configure<Z1R_Avalonia.ZoraTrackerApp>().UsePlatformDetect());
        }
    }
}
