using Avalonia;

namespace Z1R_Avalonia_Desktop
{
    public static class Program
    {
        // (still starts the prototype UI until the ported Program/WPFUI can run the app)
        [System.STAThread]
        public static int Main(string[] args) =>
            AppBuilder.Configure<Z1R_Avalonia.Prototype.App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}
