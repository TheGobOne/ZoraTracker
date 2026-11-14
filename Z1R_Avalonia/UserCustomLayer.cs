// Ported from Z1R_WPF/UserCustomLayer.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// The ExtraIcons folder is a user folder (Platform.Files.User("ExtraIcons")). invokeExtras (an F# Async<unit>) is a Func<Task>.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.FSharp.Control;
using static Z1R_Avalonia.CustomComboBoxes.GlobalFlag;

namespace Z1R_Avalonia;

public static class UserCustomLayer
{
    private static bool extrasOpening = false;
    // a bitmap from a user file, or null when it is missing or unreadable (read into memory, so the file is not locked)
    private static System.Drawing.Bitmap LoadImageOrNull(string path)
    {
        try
        {
            using (var ms = new System.IO.MemoryStream(System.IO.File.ReadAllBytes(path)))
            using (var loaded = new System.Drawing.Bitmap(ms))
                return new System.Drawing.Bitmap(loaded);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /*
    let rawDataMetroid =
        [|
        //brinstar
        19,113,"E",""
        72,113,"U",""
        57, 41,"E",""
        121,17,"E",""
        194,25,"U",""
        211,25,"U",""
        201,41,"E",""
        201,57,"U",""
        128,73,"M",""
        153,73,"E",""
        146,89,"U",""
        // norfair
        218,81, "U",""
        227,81, "U",""
        209,89, "U",""
        218,89, "U",""
        227,89, "U",""
        218,97, "E",""
        145,113,"U",""
        218,113,"M",""
        137,121,"U",""
        153,121,"U",""
        162,121,"M",""
        121,129,"E",""
        217,137,"E",""
        209,153,"U",""
        225,161,"U",""
        145,169,"E",""
        154,177,"U",""
        163,177,"U",""
        // kraid
        78,137,"M",""
        78,145,"M",""
        78,153,"M",""
        39,169,"U",""
        74,169,"U",""
        74,177,"U",""
        46,193,"M",""
        46,209,"M",""
        82,201,"U",""
        46,217,"U",""
        37,225,"M",""
        46,225,"M",""
        55,225,"U",""
        57,233,"Q",""
        // ridley
        146,193,"U",""
        113,201,"M",""
        138,201,"U",""
        147,201,"M",""
        156,201,"M",""
        165,201,"M",""
        174,201,"M",""
        113,217,"M",""
        190,217,"U",""
        217,217,"U",""
        121,233,"Q",""
        113,241,"M",""
        168,241,"U",""
        // checklist
        265,165," ","Kraid.png"
        301,165," ","Ridley.png"
        265,185," ","MorphBall.png"
        301,185," ","IceBeam.png"
        265,205," ","WaveBeam.png"
        301,205," ","LongBeam.png"
        265,225," ","Bombs.png"
        301,225," ","HighJump.png"
        265,245," ","ScrewAttack.png"
        301,245," ","Varia.png"
        |] |> Array.map (fun (x,y,label,ti) -> (x,y,6,6,label,ti))
    let backgroundImageFile = """z1m1-metroid-ingame-regions-and-checklist.png"""
    */

    public static readonly string extraIconsDirectory = Platform.Files.User("ExtraIcons");   // (WPF: AppDomain.CurrentDomain.BaseDirectory\ExtraIcons)
    public static readonly string checklistFilename = System.IO.Path.Combine(extraIconsDirectory, "UserCustomChecklist.json");

    public static StackPanel thePanel = null;
    public static async Task InitializeUserCustom(CustomComboBoxes.CanvasManager cm, List<TimelineItem> timelineItems, Func<Task> invokeExtras)
    {
        if (SaveAndLoad.theUserCustomChecklist == null)
        {
            var errorText = "";
            if (System.IO.File.Exists(checklistFilename))
            {
                try
                {
                    var json = System.IO.File.ReadAllText(checklistFilename);
                    SaveAndLoad.theUserCustomChecklist = System.Text.Json.JsonSerializer.Deserialize<SaveAndLoad.UserCustomChecklist>(json);
                }
                catch (Exception e)
                {
                    errorText = $"Error loading UserCustomChecklist.json\n\n{e}";
                }
            }
            else
                errorText = "No UserCustomChecklist.json file exists.";
            if (errorText != "")
            {
                if (errorText.Length > 600)
                    errorText = errorText.Substring(0, 600) + "...";
                var r = await CustomComboBoxes.DoModalMessageBox(cm, System.Drawing.SystemIcons.Error, errorText, new[] { "Open ExtraIcons folder", "Ok" });
                if (r != "Ok")
                {
                    var fileToSelect = checklistFilename;
                    Platform.RevealInFileManager(fileToSelect);   // (WPF: Process.Start(ProcessStartInfo("Explorer.exe", "/Select, \"file\"")))
                }
            }
        }
        if (SaveAndLoad.theUserCustomChecklist != null && thePanel == null)
        {
            var backgroundImageFilename = System.IO.Path.Combine(extraIconsDirectory, SaveAndLoad.theUserCustomChecklist.BackgroundImageFilename);
            // (a missing or unreadable image used to throw here and crash the app: the checklist then shows without its background)
            var backgroundImageBmp = LoadImageOrNull(backgroundImageFilename);

            var (W, H) = (cm.AppMainCanvas.Width, OverworldItemGridUI.THRU_BLOCKERS_H);
            var c = new Canvas { Width = W, Height = H, Background = Brushes.Black };
            double scale = 1.0;
            if (backgroundImageBmp != null)
            {
                var hRatio = H / (double)backgroundImageBmp.Height;
                var wRatio = W / (double)backgroundImageBmp.Width;
                scale = Math.Min(hRatio, wRatio);

                var img = Graphics.BMPtoImage(backgroundImageBmp);
                img.Stretch = Stretch.Uniform;
                img.Width = (double)backgroundImageBmp.Width * scale;
                img.Height = (double)backgroundImageBmp.Height * scale;
                Graphics.canvasAdd(c, img, 0.0, 0.0);
            }

            TextBox mkTxt(string text) => new TextBox { Text = text, BorderThickness = new Thickness(0.0), Foreground = Brushes.White, Background = Brushes.Black, IsHitTestVisible = false, IsReadOnly = true };
            var T = 3.0;
            for (int n0 = 0; n0 <= SaveAndLoad.theUserCustomChecklist.Items.Length - 1; n0++)
            {
                var n = n0;
                var ucc = SaveAndLoad.theUserCustomChecklist.Items[n];
                var (x, y, w, h, label, ti) = (ucc.Left, ucc.Top, ucc.Width, ucc.Height, ucc.DisplayLabel, ucc.TimelineIconFilename);
                // timeline
                // (F#: an Event<bool> option; null for None. The FSharpEvent goes to the F# model.)
                FSharpEvent<bool> ev = null;
                if (ti != "")
                {
                    var tid = TimelineID.UserCustom(ti);
                    if (!TrackerModel.TimelineItemModel.All.ContainsKey(tid.Identifier))
                    {
                        var bmpFile = System.IO.Path.Combine(extraIconsDirectory, ti);
                        var orig = LoadImageOrNull(bmpFile);
                        System.Drawing.Bitmap bmp = null;   // (stays null for a missing icon: the item then gets no timeline entry)
                        if (orig == null) { }
                        else if (orig.Width == 16 && orig.Height == 16)
                        {
                            bmp = new System.Drawing.Bitmap(21, 21);
                            for (int i = 0; i <= 20; i++)
                                for (int j = 0; j <= 20; j++)
                                    bmp.SetPixel(i, j, System.Drawing.Color.Transparent);
                            for (int i = 1; i <= 18; i++)
                                for (int j = 1; j <= 18; j++)
                                    bmp.SetPixel(i, j, System.Drawing.Color.Black);
                            for (int i = 2; i <= 17; i++)
                                for (int j = 2; j <= 17; j++)
                                    bmp.SetPixel(i, j, orig.GetPixel(i - 2, j - 2));
                        }
                        else
                            bmp = orig;
                        if (bmp != null)
                        {
                            var ev1 = new FSharpEvent<bool>();
                            var tim = new TrackerModel.TimelineItemModel(TrackerModel.TimelineItemDescription.NewUserCustom(tid.Identifier, ev1));
                            TrackerModel.TimelineItemModel.All.Add(tid.Identifier, tim);
                            var ti2 = new TimelineItem(tid, () => bmp);
                            timelineItems.Add(ti2);
                            ev = ev1;
                        }
                    }
                }
                // canvas interaction
                var (ws, hs) = ((double)w * scale, (double)h * scale);
                var tb = mkTxt(label);
                var b = new Button { BorderThickness = new Thickness(T), Padding = new Thickness(0.0), Content = new Viewbox { Child = tb, Stretch = Stretch.Fill },
                                     Width = 2.0 * T + ws, Height = 2.0 * T + hs };
                Graphics.canvasAdd(c, b, (double)x * scale - T, (double)y * scale - T);
                void redraw()
                {
                    if (SaveAndLoad.theUserCustomChecklist.Items[n].IsChecked)
                    {
                        tb.Foreground = Brushes.Black;
                        tb.Background = Brushes.Lime;
                    }
                    else
                    {
                        tb.Foreground = Brushes.White;
                        tb.Background = Brushes.Black;
                    }
                }
                redraw();
                b.Click += (_, _) =>
                {
                    SaveAndLoad.theUserCustomChecklist.Items[n].IsChecked = !SaveAndLoad.theUserCustomChecklist.Items[n].IsChecked;
                    redraw();
                    if (ev != null)
                        ev.Trigger(SaveAndLoad.theUserCustomChecklist.Items[n].IsChecked);
                };
            }
            var sp = new StackPanel { Orientation = Orientation.Vertical, Width = cm.AppMainCanvas.Width, Background = Brushes.Black };
            sp.Children.Add(c);
            TextBox mkTxt2(string text) => new TextBox { Text = text, BorderThickness = new Thickness(0.0), FontSize = 16.0, Foreground = Brushes.Orange, Background = Brushes.Black, IsHitTestVisible = false, IsReadOnly = true };
            sp.Children.Add(mkTxt2($"User Custom Checklist: \"{SaveAndLoad.theUserCustomChecklist.Name}\""));
            sp.Children.Add(new DockPanel { Height = 10.0 });
            var hp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(hp);
            hp.Children.Add(mkTxt2("Click outside this popup (below here) to exit the checklist, or\nclick the icon to the right to mark extra Zelda items obtained here: "));
            var extrasImage = Graphics.dock(Graphics.BMPtoImage(Graphics.iconExtras_bmp), Dock.Bottom);
            extrasImage.PointerPressed += (_, _) =>   // (WPF: MouseDown)
                CustomComboBoxes.StartImmediate(((Func<Task>)(async () =>
                {
                    // (this runs inside the checklist's own popup, which already holds the popup flag, so the extras window opens directly.
                    // The old check, 'if not already a popup', never passed here and the icon did nothing.)
                    if (extrasOpening) return;   // (a second click while the extras window is opening would open another)
                    extrasOpening = true;
                    try { await invokeExtras(); }
                    finally { extrasOpening = false; }
                }))());
            hp.Children.Add(extrasImage);
            sp.Children.Add(new DockPanel { Height = 3.0, Background = Brushes.Gray });
            thePanel = sp;
        }
    }

    public static async Task InteractWithUserCustom(CustomComboBoxes.CanvasManager cm, List<TimelineItem> timelineItems, Func<Task> invokeExtras)
    {
        await InitializeUserCustom(cm, timelineItems, invokeExtras);
        if (thePanel != null)
        {
            var wh = new ManualResetEvent(false);
            await CustomComboBoxes.DoModal(cm, wh, 0.0, 0.0, thePanel);
        }
    }
}
