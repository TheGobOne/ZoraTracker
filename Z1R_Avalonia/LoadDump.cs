// `ZoraTracker.CrossPlatform --load=<save file> --dump=<out file>`: loads a save file through the tracker's normal load path (the same code the Load Save button
// and the recent-saves buttons use), writes what the tracker then holds to <out file> as JSON, and exits. The desktop host runs it on Avalonia's headless platform (no window server: nothing is shown and nothing takes the focus).
// For tools that write tracker saves: load one, then compare what came back with what was meant. Run it from a COPY of the release folder (it may change settings).
// The out file is a JSON object:
//   "warning":      the text of the version warning the load would have shown ("Attempt Load" box), or null
//   "error":        the text of the load error, if the file failed to load (then there is no "save"), or null
//   "setProblems":  true if the load could not set an item box (an id the tracker refuses is left empty, no message otherwise)
//   "save":         the tracker's own save of its state after the load (the same format as a manual save, built in memory: no file is written to Saves)
// Compare "save" with the file that was loaded: anything the load turned into "unmarked", left empty or dropped shows as a difference.
using System;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Z1R_Avalonia;

public static class LoadDump
{
    private static readonly string[] args = Environment.GetCommandLineArgs();
    private static string argValue(string name) => args.Where(a => a.StartsWith(name + "=")).Select(a => a.Substring(name.Length + 1)).FirstOrDefault();
    public static readonly string loadPath = argValue("--load");
    private static readonly string dumpPath = argValue("--dump");
    public static readonly bool isActive = loadPath != null && dumpPath != null;
    public static bool setProblems = false;
    private static string warning = null;

    public static void SetWarning(string w) { warning = w; }

    private static string quote(string s) => s == null ? "null" : System.Text.Json.JsonSerializer.Serialize(s);

    private static bool written = false;

    private static void write(string error, string[] saveLines)
    {
        if (written) return;
        written = true;
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append($"\"warning\": {quote(warning)},\n");
        sb.Append($"\"error\": {quote(error)},\n");
        sb.Append($"\"setProblems\": {(setProblems ? "true" : "false")},\n");
        // (the tracker's save text allows trailing commas, which it reads back; parse it and write it again so the dump is plain JSON for any reader)
        string plainSave = "null";
        if (saveLines != null)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(string.Join("\n", saveLines), new System.Text.Json.JsonDocumentOptions { AllowTrailingCommas = true });
            using var ms = new MemoryStream();
            using (var w = new System.Text.Json.Utf8JsonWriter(ms)) doc.RootElement.WriteTo(w);   // (written again without the commas)
            plainSave = Encoding.UTF8.GetString(ms.ToArray());
        }
        sb.Append("\"save\": " + plainSave + "\n");
        sb.Append("}\n");
        File.WriteAllText(dumpPath, sb.ToString());
        Dispatcher.UIThread.Post(() => (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(0));
    }

    // the load finished: write the tracker's save of what it now holds
    public static void Finish(string[] saveLines) => write(null, saveLines);
    // the load failed
    public static void Fail(string error) => write(error, null);

    // keeps any window off the screen and out of the taskbar (as the selftest does; on the headless platform there is no screen anyway)
    public static void arm()
    {
        Control.LoadedEvent.AddClassHandler<Window>((w, _) =>
        {
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Position = new PixelPoint(-4000, 0);
            w.ShowInTaskbar = false;
        });
        // a load that fails after the file was read (a wrong-sized map, say) is a result too: say so in the dump instead of hanging
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            Fail(e.Exception.ToString());
        };
    }
}
