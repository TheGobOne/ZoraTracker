// Ported from Z1R_WPF/ShowRunCustom.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;

namespace Z1R_Avalonia;

public static class ShowRunCustom
{
    // When Windows minimizes, or is closing a window, it moves it to -32000,-32000 and WPF reports those values as Left and Top.
    // However, on a high-DPI device, that number gets scaled, e.g. at 1.75, you see -18285 get reported.
    // Since I am using the value as a way to detect 'junk' coordinates, there's am issue deciding which coordinates are junk versus
    // large and negative real.
    // In practice, it seems today devices are unlikely to have a DPI scale of more than 2.0, which means -16000 would be a useful cutoff.
    // So if a Left/Top coordinate is less than this value, assume junk:
    public static readonly double MINIMIZED_THRESHOLD = -15999.0;

    // (port helpers: WPF's Window.Left/Top are in device-independent units; Avalonia's Window.Position is in screen pixels. These convert,
    // so the numbers saved in ShowRunCustom.txt and the settings mean the same as WPF's. Before a window is shown, the main window's scaling is used.
    // Popouts.cs uses them too.)
#if BROWSER
    public static Window mainWindow() => Graphics.theWindow;   // (browser build: the main window is the stand-in shown in the page)
#else
    public static Window mainWindow() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d ? d.MainWindow : null;
#endif
    private static double scalingOf(Window w)
    {
        var s = w.IsVisible ? w.DesktopScaling : (mainWindow()?.DesktopScaling ?? 1.0);
        return s > 0.0 ? s : 1.0;
    }
    public static double leftOf(Window w) => w.Position.X / scalingOf(w);
    public static double topOf(Window w) => w.Position.Y / scalingOf(w);
    public static void setLeftTop(Window w, double left, double top)
    {
        var s = scalingOf(w);
        w.Position = new PixelPoint((int)Math.Round(left * s), (int)Math.Round(top * s));
    }
    // (WPF's Window.Owner + Show(): Avalonia passes the owner to Show)
    public static void showOwned(Window w)
    {
        var owner = mainWindow();
        if (owner != null && owner != w) w.Show(owner); else w.Show();
    }

    // puts a window's content in a Viewbox so the content scales as the window is resized (opens at the content's natural size; the window can be resized with its edges or corner grip)
    // (Avalonia windows are resizable from their edges and corners; there is no separate resize grip, so ResizeMode.CanResizeWithGrip → CanResize)
    public static void makeScalable(Window w, Control content)
    {
        var vb = new Viewbox { Child = content, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
        w.Content = vb;
        w.CanResize = true;
        // (WPF: ContentRendered; Avalonia: Opened, which comes after the first layout, when the window has sized itself to its content)
        w.Opened += (_, _) =>
        {
            if (w.SizeToContent != SizeToContent.Manual)
            {
                var (aw, ah) = (w.ClientSize.Width, w.ClientSize.Height);
                w.SizeToContent = SizeToContent.Manual;
                w.Width = aw;
                w.Height = ah;
            }
            vb.StretchDirection = StretchDirection.Both;
        };
    }

    public static void MakeDefaultShowRunCustomFile(string filename)
    {
        // (WPF's sample paths are ".\ShowRunCustomImages\..."; written with '/' here, which works on every system. Relative paths are relative to the app folder, see resolvePath.)
        System.IO.File.WriteAllText(filename, string.Format(@"
# {0} ShowRunCustom file

# Blank lines, or lines that start with '#', are comments and are ignored by the parser

# Other lines must have one of these formats:
#     SHOW ""FullPathToImageFileName""
#     nnn,nnn,nnn,nnn SHOW ""FullPathToImageFileName""
#     RUN ""FullPathToExe"" CommandLineArguments
#     RUN ""FullUrlToAWebPage""
# FullPathToImageFileName should be an image file, such as a .jpg or .png, which will be displayed in a window
# nnn,nnn,nnn,nnn are the Left, Top, Width, Height of that window (to optionally save window size/location)
# FullPathToExe should be an executable file like .exe
# CommandLineArguments are extra text passed on the command line to the Exe
# FullUrlToAWebPage should start with http:// or https:// and will open in your default browser

# All of the SHOW and RUN lines will be activated when you click the 'Show/Run Custom' button in the app.

# Below are some sample commands you might find useful.  You can activate them by removing the comment
# character '#' before RUN or SHOW, saving this file, and then clicking 'Show/Run Custom' in the app.

# Vanilla 1Q and 2Q maps
# RUN ""https://z1r.fandom.com/wiki/Maps""

# The Zelda in-game inventory display completely full of items - useful reference when playing 'sprite shuffle'
# SHOW ""./ShowRunCustomImages/all-items-hud.png""

# A picture showing 'rooms that never drop in 1Q/Shapes dungeons', as well as enemy drop tables
# SHOW ""./ShowRunCustomImages/enemy-drop-table-and-rooms-that-never-drop.png""

# A textual version of the 'rooms that never drop in 1Q/Shapes dungeons'
# SHOW ""./ShowRunCustomImages/rooms-that-never-drop-text.png""
        ", OverworldData.ProgramNameString));
    }

    // (F#: a [<RequireQualifiedAccess>] union. As HotKeys.DungeonRoomHotKey: an abstract record with one nested record per case.
    // WPF's `Line.SHOW(s, Some(l,t,w,h))` → `new Line.SHOW(s, (l, t, w, h))`, `Line.SHOW(s, None)` → `new Line.SHOW(s, null)`, `Line.BLANK` → `new Line.BLANK()`.)
    public abstract record Line
    {
        private Line() { }
        public sealed record BLANK() : Line;
        public sealed record COMMENT(string Text) : Line;
        public sealed record SHOW(string File, (double, double, double, double)? Ltwh) : Line;
        public sealed record RUN(string Command, string Args) : Line;
        public string AsString() =>
            this switch
            {
                BLANK => "",
                COMMENT(var s) => $"#{s}",
                SHOW(var s, null) => $"SHOW \"{s}\"",
                SHOW(var s, var (l, t, w, h)) => $"{(int)l},{(int)t},{(int)w},{(int)h} SHOW \"{s}\"",
                RUN(var c, var a) => $"RUN \"{c}\" {a}",
                _ => throw new InvalidOperationException()
            };
    }

    public static List<Line> ParseShowRunCustomFile(string filename)
    {
        var lines = System.IO.File.ReadAllLines(filename);
        var emptyLineRegex = new Regex(@"^\s*$", RegexOptions.None);
        var commentRegex = new Regex("^#(.*)$", RegexOptions.None);
        var showRegex = new Regex(@"^\s*SHOW\s*""(.*)""\s*$", RegexOptions.None);
        var ltwhShowRegex = new Regex(@"^\s*(-?\d+),(-?\d+),(\d+),(\d+)\s*SHOW\s*""(.*)""\s*$", RegexOptions.None);
        var runRegex = new Regex(@"^\s*RUN\s*""(.*)""\s*(.*)$", RegexOptions.None);
        var data = new List<Line>();
        var lineNumber = 1;
        foreach (var line in lines)
        {
            if (emptyLineRegex.IsMatch(line))
                data.Add(new Line.BLANK());
            else
            {
                var m = commentRegex.Match(line);
                if (m.Success)
                {
                    var comment = m.Groups[1].Value;
                    data.Add(new Line.COMMENT(comment));
                }
                else
                {
                    m = showRegex.Match(line);
                    if (m.Success)
                    {
                        var img = m.Groups[1].Value;
                        data.Add(new Line.SHOW(img, null));
                    }
                    else
                    {
                        m = ltwhShowRegex.Match(line);
                        if (m.Success)
                        {
                            (double, double, double, double)? ltwh = (parseFloat(m.Groups[1].Value), parseFloat(m.Groups[2].Value), parseFloat(m.Groups[3].Value), parseFloat(m.Groups[4].Value));
                            var img = m.Groups[5].Value;
                            data.Add(new Line.SHOW(img, ltwh));
                        }
                        else
                        {
                            m = runRegex.Match(line);
                            if (m.Success)
                            {
                                var comm = m.Groups[1].Value;
                                var args = m.Groups[2].Value;
                                data.Add(new Line.RUN(comm, args));
                            }
                            else
                            {
                                var fullText = $"Error parsing '{filename}', line {lineNumber}\n\n" +
                                                        "You should fix this error by editing the text file.\n" +
                                                        "Or you can delete it, and pressing the button again will create an empty template file in its place.";
                                throw new HotKeys.UserError(fullText);
                            }
                        }
                    }
                }
            }
            lineNumber = lineNumber + 1;
        }
        return data;
    }
    // (F#'s `float s` parses with the invariant culture)
    private static double parseFloat(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    public static void WriteShowRunCustomFile(string filename, Line[] data) => System.IO.File.WriteAllLines(filename, data.Select(x => x.AsString()));

    public static readonly string ShowRunCustomFilename = Platform.Files.User("ShowRunCustom.txt");

    // (port helper: WPF opens a relative SHOW/RUN path relative to the current directory, which is the app folder when the tracker is started
    // normally; the sample lines use ".\ShowRunCustomImages\...", images that ship with the app. A macOS/Linux app's current directory isn't its
    // folder, so a relative path is taken relative to Platform.AppDir(), and backslashes in it become the platform's separator.)
    private static string resolvePath(string p)
    {
        if (string.IsNullOrEmpty(p) || System.IO.Path.IsPathRooted(p)) return p;
        if (!Platform.isWindows) p = p.Replace('\\', System.IO.Path.DirectorySeparatorChar);
        return Platform.Files.App(p);
    }

    // the SHOW windows currently open (so the Popout Windows menu can show/hide them as a group)
    public static readonly List<Window> openShowWindows = new List<Window>();
    public static bool areShowWindowsOpen() => openShowWindows.Count > 0;
    public static void closeShowWindows()
    {
        foreach (var w in openShowWindows.ToArray())
        {
            try { w.Close(); } catch { }
        }
    }

    public static void DoShowRunCustom(Action refocusMainWindow)
    {
        void displayInfoOrError(string titleKind, string message)
        {
            var tb = new TextBox { FontSize = 16.0, Foreground = Brushes.Orange, Background = Brushes.Black,
                                    IsReadOnly = true, BorderThickness = new Thickness(0.0), Text = message, IsHitTestVisible = false };
            var window = new Window { Title = $"ZORATracker {titleKind}", SizeToContent = SizeToContent.WidthAndHeight };
            makeScalable(window, tb);
            // remember where the user last put this window
            var m = Regex.Match(TrackerModelOptions.ShowRunInfoWindowLT, @"^(-?\d+),(-?\d+)$");
            if (m.Success && !(parseFloat(m.Groups[1].Value) < MINIMIZED_THRESHOLD))
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                setLeftTop(window, parseFloat(m.Groups[1].Value), parseFloat(m.Groups[2].Value));
            }
            window.PositionChanged += (_, _) =>
            {
                if (window.IsLoaded && !(leftOf(window) < MINIMIZED_THRESHOLD))
                {
                    TrackerModelOptions.ShowRunInfoWindowLT = $"{(int)leftOf(window)},{(int)topOf(window)}";
                    TrackerModelOptions.writeSettings();
                }
            };
            showOwned(window);
            var fileToSelect = ShowRunCustomFilename;
            Platform.RevealInFileManager(fileToSelect);   // (WPF: Explorer.exe /Select, "file")
        }
        var currentIndexBeingInterpreted = 0;
        Func<string, string> prefixErrorMessageWithContext = msg => msg;
        try
        {
            var didAnything = false;
            if (!System.IO.File.Exists(ShowRunCustomFilename))
                MakeDefaultShowRunCustomFile(ShowRunCustomFilename);
            var data = ParseShowRunCustomFile(ShowRunCustomFilename).ToArray();
            // the parser gives good errors, but if we successfully parse the file, give context if a further error appears
            prefixErrorMessageWithContext = msg =>
            {
                var lineNum = currentIndexBeingInterpreted + 1;
                var lineText = data[currentIndexBeingInterpreted].AsString();
                return $"While doing line {lineNum} of '{ShowRunCustomFilename}':\n\n    {lineText}\n\nan error occurred:\n\n{msg}";
            };
            for (var i = 0; i <= data.Length - 1; i++)
            {
                currentIndexBeingInterpreted = i;
                switch (data[i])
                {
                    case Line.BLANK: break;
                    case Line.COMMENT: break;
                    case Line.SHOW(var imgFile, var ltwho):
                    {
                        var imgPath = resolvePath(imgFile);
                        if (!System.IO.File.Exists(imgPath))
                            throw new HotKeys.UserError($"The file to SHOW does not exist:\n{imgFile}");
                        var bmp = new System.Drawing.Bitmap(imgPath);
                        var img = Graphics.BMPtoImage(bmp);
                        // the image scales (keeping its aspect ratio) as the window is resized; it opens at its natural size
                        var image = new Image { Source = img.Source, Width = (double)bmp.Width, Height = (double)bmp.Height };
                        var b = new Viewbox { Child = image, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.Both };
                        var window = new Window { Title = "ZORATracker SHOW", Content = b, Background = Brushes.Black };
                        if (ltwho is var (l, t, w, h))
                        {
                            setLeftTop(window, l, t);
                            window.Width = w;
                            window.Height = h;
                        }
                        else
                        {
                            // no remembered size yet: open at the image's natural size, but never bigger than 800x600 (it keeps its aspect ratio; resize it and the size is remembered)
                            var scale = Math.Min(1.0, Math.Min(800.0 / (double)bmp.Width, 600.0 / (double)bmp.Height));
                            image.Width = (double)bmp.Width * scale;
                            image.Height = (double)bmp.Height * scale;
                            window.SizeToContent = SizeToContent.WidthAndHeight;
                        }
                        var cur = i;
                        void update()
                        {
                            if (!(leftOf(window) < MINIMIZED_THRESHOLD)) // minimized
                            {
                                data[cur] = new Line.SHOW(imgFile, (leftOf(window), topOf(window), window.Bounds.Width, window.Bounds.Height));
                                WriteShowRunCustomFile(ShowRunCustomFilename, data);
                            }
                        }
                        window.SizeChanged += (_, _) => { update(); refocusMainWindow(); };
                        window.PositionChanged += (_, _) => { update(); refocusMainWindow(); };
                        openShowWindows.Add(window);
                        window.Closed += (_, _) => openShowWindows.Remove(window);
                        showOwned(window);
                        didAnything = true;
                        break;
                    }
                    case Line.RUN(var comm, var args):
                    {
                        var commPath = resolvePath(comm);
                        if (!System.IO.File.Exists(commPath))
                        {
                            if ((comm.ToLowerInvariant().StartsWith("http://") || comm.ToLowerInvariant().StartsWith("https://")) && Uri.IsWellFormedUriString(comm, UriKind.Absolute))
                            {
                                Platform.OpenWithShell(comm);    // open a URL in default browser
                                didAnything = true;
                            }
                            else
                                throw new HotKeys.UserError($"The file to RUN does not exist:\n{comm}");
                        }
                        else
                        {
                            Platform.StartProcessHidden(commPath, args);
                            didAnything = true;
                        }
                        break;
                    }
                }
            }
            if (!didAnything)
                displayInfoOrError("info", $"There were no custom SHOW or RUN commands to process.\nYou can add some by editing this file:\n{ShowRunCustomFilename}");
        }
        catch (HotKeys.UserError ue)
        {
            displayInfoOrError("error", prefixErrorMessageWithContext(ue.Message));
        }
        catch (Exception e)
        {
            displayInfoOrError("error", prefixErrorMessageWithContext(e.ToString()));
        }
    }
}
