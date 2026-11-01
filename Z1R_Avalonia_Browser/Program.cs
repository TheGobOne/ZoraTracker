using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;

[assembly: SupportedOSPlatform("browser")]

namespace Z1R_Avalonia_Browser;

public static class Program
{
    // Starts the tracker inside the <div id="out"> in wwwroot/index.html (ZoraTrackerApp shows the main window's content there).
    public static async Task Main(string[] args)
    {
        // (the JavaScript helpers Z1R_Avalonia.BrowserJs calls are registered by main.js as the module "main.js")
        // (the desktop app writes a crash log; in a page, errors go to the browser's console, with their stack traces. Don't touch
        // Dispatcher.UIThread here: before Avalonia starts, that makes a default dispatcher, and the page then never draws)
        System.AppDomain.CurrentDomain.UnhandledException += (_, e) => System.Console.WriteLine($"UNHANDLED: {e.ExceptionObject}");
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) => System.Console.WriteLine($"UNOBSERVED: {e.Exception}");
        try
        {
            await AppBuilder.Configure<Z1R_Avalonia.ZoraTrackerApp>().WithInterFont().LogToTextWriter(System.Console.Out, Avalonia.Logging.LogEventLevel.Warning).StartBrowserAppAsync("out");
        }
        catch (System.Exception e)
        {
            System.Console.WriteLine($"STARTUP FAILED: {e}");
            throw;
        }
    }
}
