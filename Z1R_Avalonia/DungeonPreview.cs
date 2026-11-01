// TEMPORARY (phase 1): a window with only the dungeon tracker, so it can be used and tested before WPFUI/Program are ported.
// Start the desktop app with --dungeon-preview. Not part of the WPF mirror; delete once the ported Program.cs runs the app.
// It sets things up the way Program.fs / WPFUI.makeAll do before they call DungeonUI.makeDungeonTabs.
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;

namespace Z1R_Avalonia;

public class DungeonPreviewApp : Application
{
    /// <summary>if set, save a screenshot of the window to this path shortly after it opens, then quit</summary>
    public static string ScreenshotPath;

    public override void Initialize()
    {
        Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;   // WPF's unstyled defaults are light (see PROGRESS.md)
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var window = CreateWindow();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
            d.MainWindow = window;
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>set when makeDungeonTabs has finished (or failed)</summary>
    public static TaskCompletionSource<bool> Ready = new TaskCompletionSource<bool>();

    /// <summary>the window made by CreateWindow (headless tests show it themselves)</summary>
    public static new Window Current;

    /// <summary>the preview window</summary>
    public static Window CreateWindow()
    {
        Graphics.installAppWideStyles();
        const double W = 16 * 48;   // 16 * Graphics.OMTW, as in Program.fs
        const double H = 720;
        var window = new Window { Title = "ZORA Tracker - dungeon preview (temporary)", Width = W, Height = H, FontSize = 18, Background = Brushes.Black, CanResize = false };
        // as in Program.fs
        var rootCanvas = new Canvas { Width = W, Height = H, Background = Brushes.Black, UseLayoutRounding = true };
        var appMainCanvas = new Canvas { Width = W, Height = H, Background = Brushes.Black };
        Graphics.canvasAdd(rootCanvas, appMainCanvas, 0, 0);
        var cm = new CustomComboBoxes.CanvasManager(rootCanvas, appMainCanvas);
        window.Content = rootCanvas;
        Graphics.theWindow = window;
        // as in WPFUI.makeAll (first-quest overworld, default dungeon kind)
        TrackerModel.initializeAll(new OverworldData.OverworldInstance(OverworldData.OWQuest.FIRST), TrackerModel.DungeonTrackerInstanceKind.DEFAULT);

        window.Opened += async (_, _) =>
        {
            Graphics.setup(window, rootCanvas);
            try
            {
                var ghostbusters = new Canvas[8];
                for (int i = 0; i < 8; i++) ghostbusters[i] = new Canvas();
                var rightwardCanvas = new Canvas();
                var result = await DungeonUI.makeDungeonTabs(cm, c => Graphics.canvasAdd(appMainCanvas, c, 0, 0), () => 0.0,
                    new Graphics.SharedEvent<int>(), 24, rightwardCanvas, new Graphics.SharedEvent<int>(), new Graphics.SharedEvent<bool>(),
                    ghostbusters, s => { Console.WriteLine($"[progress] {s}"); return Task.CompletedTask; }, _ => { }, _ => { });
                var grabModeTextBlock = result.Item3;
                grabModeTextBlock.Opacity = 0;
                Graphics.canvasAdd(appMainCanvas, rightwardCanvas, result.Item1.Width + 4, 0);
                Console.WriteLine($"dungeon tabs ready: {result.Item1.Width}x{result.Item1.Height}");
                Ready.TrySetResult(true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"dungeon preview failed: {e}");
                Ready.TrySetResult(false);
            }
            if (ScreenshotPath != null)
            {
                await Task.Delay(1500);
                var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)W, (int)H));
                rtb.Render(rootCanvas);
                rtb.Save(ScreenshotPath);
                Console.WriteLine($"screenshot saved to {ScreenshotPath}");
                window.Close();
            }
        };
        Current = window;
        return window;
    }
}
