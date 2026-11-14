// Ported from Z1R_WPF/DrawingLayer.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// The F# `open CustomComboBoxes` / `open DungeonSaveAndLoad` are `using static`s. WPF's ScaleTransform scales from the top-left corner; Avalonia's
// RenderTransformOrigin defaults to the centre, so the stamps set it to TopLeft.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using static Z1R_Avalonia.CustomComboBoxes;
using static Z1R_Avalonia.DungeonSaveAndLoad;

namespace Z1R_Avalonia;

public static class DrawingLayer
{
    public static class ExtraZTbmps
    {
        public static readonly System.Drawing.Bitmap Vertical = make_Vertical();
        private static System.Drawing.Bitmap make_Vertical()
        {
            var r = new System.Drawing.Bitmap(16, 16);
            for (int py = 0; py <= 15; py++)
                r.SetPixel(8, py, System.Drawing.Color.Red);
            return r;
        }
        public static readonly System.Drawing.Bitmap Horizontal = make_Horizontal();
        private static System.Drawing.Bitmap make_Horizontal()
        {
            var r = new System.Drawing.Bitmap(16, 16);
            for (int px = 0; px <= 15; px++)
                r.SetPixel(px, 8, System.Drawing.Color.Red);
            return r;
        }
        public static void top(System.Drawing.Bitmap r)
        {
            for (int px = 0; px <= 15; px++)
                r.SetPixel(px, 0, System.Drawing.Color.Red);
        }
        public static void bottom(System.Drawing.Bitmap r)
        {
            for (int px = 0; px <= 15; px++)
                r.SetPixel(px, 15, System.Drawing.Color.Red);
        }
        public static void left(System.Drawing.Bitmap r)
        {
            for (int py = 0; py <= 15; py++)
                r.SetPixel(0, py, System.Drawing.Color.Red);
        }
        public static void right(System.Drawing.Bitmap r)
        {
            for (int py = 0; py <= 15; py++)
                r.SetPixel(15, py, System.Drawing.Color.Red);
        }
        public static readonly System.Drawing.Bitmap UL = make_UL();
        private static System.Drawing.Bitmap make_UL()
        {
            var r = new System.Drawing.Bitmap(16, 16);
            top(r);
            left(r);
            return r;
        }
        public static readonly System.Drawing.Bitmap UR = make_UR();
        private static System.Drawing.Bitmap make_UR()
        {
            var r = new System.Drawing.Bitmap(16, 16);
            top(r);
            right(r);
            return r;
        }
        public static readonly System.Drawing.Bitmap LL = make_LL();
        private static System.Drawing.Bitmap make_LL()
        {
            var r = new System.Drawing.Bitmap(16, 16);
            bottom(r);
            left(r);
            return r;
        }
        public static readonly System.Drawing.Bitmap LR = make_LR();
        private static System.Drawing.Bitmap make_LR()
        {
            var r = new System.Drawing.Bitmap(16, 16);
            bottom(r);
            right(r);
            return r;
        }
        public static readonly System.Drawing.Bitmap OnlyLeft = make_OnlyLeft();
        private static System.Drawing.Bitmap make_OnlyLeft()
        {
            var r = new System.Drawing.Bitmap(16, 16);
            top(r);
            bottom(r);
            right(r);
            return r;
        }
        public static readonly System.Drawing.Bitmap OnlyUp = make_OnlyUp();
        private static System.Drawing.Bitmap make_OnlyUp()
        {
            var r = new System.Drawing.Bitmap(16, 16);
            left(r);
            bottom(r);
            right(r);
            return r;
        }
        public static readonly System.Drawing.Bitmap OnlyRight = make_OnlyRight();
        private static System.Drawing.Bitmap make_OnlyRight()
        {
            var r = new System.Drawing.Bitmap(16, 16);
            left(r);
            bottom(r);
            top(r);
            return r;
        }
        public static readonly System.Drawing.Bitmap OnlyDown = make_OnlyDown();
        private static System.Drawing.Bitmap make_OnlyDown()
        {
            var r = new System.Drawing.Bitmap(16, 16);
            left(r);
            right(r);
            top(r);
            return r;
        }
        public static readonly (string, System.Drawing.Bitmap)[] All =
        {
            ("Vertical", Vertical),
            ("Horizontal", Horizontal),
            ("UL", UL),
            ("UR", UR),
            ("LL", LL),
            ("LR", LR),
            ("OnlyLeft", OnlyLeft),
            ("OnlyUp", OnlyUp),
            ("OnlyRight", OnlyRight),
            ("OnlyDown", OnlyDown),
        };
    }

    public static System.Drawing.Bitmap unborder(System.Drawing.Bitmap bmp)  // turn 18x18 into 16x16
    {
        var r = new System.Drawing.Bitmap(16, 16);
        for (int i = 0; i <= 15; i++)
            for (int j = 0; j <= 15; j++)
                r.SetPixel(i, j, bmp.GetPixel(i + 1, j + 1));
        return r;
    }

    public static string anyErrorMessageRelatedToExtraIcons = "";
    public static readonly string extraIconsDirectory = UserCustomLayer.extraIconsDirectory;
    // (F#: `dict arr`, a read-only IDictionary; a Dictionary keeps the same insertion order when enumerated)
    public static readonly Dictionary<string, System.Drawing.Bitmap> ExtraIconsResourceTable = make_ExtraIconsResourceTable();
    private static Dictionary<string, System.Drawing.Bitmap> make_ExtraIconsResourceTable()
    {
        (string, System.Drawing.Bitmap)[] arr;
        var currentFilename = "";
        try
        {
            System.IO.Directory.CreateDirectory(extraIconsDirectory);
            var results = new List<(string, System.Drawing.Bitmap)>();
            var badFiles = new List<string>();
            foreach (var filename in System.IO.Directory.EnumerateFiles(extraIconsDirectory, "*.png"))
            {
                currentFilename = System.IO.Path.GetFileName(filename);
                // (one unreadable file is skipped on its own. It used to empty the whole table, and every ExtraIcon stamp then vanished from the next save.)
                try
                {
                    var bmp = (System.Drawing.Bitmap)System.Drawing.Bitmap.FromFile(filename);
                    var shortFilename = System.IO.Path.GetFileNameWithoutExtension(filename);
                    results.Add((shortFilename, bmp));
                }
                catch (Exception e)
                {
                    badFiles.Add($"{currentFilename}: {e.Message}");
                }
            }
            currentFilename = "";
            if (badFiles.Count > 0)
                anyErrorMessageRelatedToExtraIcons = "These files could not be read and were skipped:\n" + string.Join("\n", badFiles);
            if (results.Count == 0 && badFiles.Count == 0)
            {
                anyErrorMessageRelatedToExtraIcons = "No .png files found in ExtraIcons directory";
                arr = new (string, System.Drawing.Bitmap)[0];
            }
            else
                arr = results.ToArray();
        }
        catch (Exception e)
        {
            anyErrorMessageRelatedToExtraIcons = currentFilename != "" ? $"While loading\n{currentFilename}\nfrom ExtraIcons folder:\n{e}" : e.ToString();
            arr = new (string, System.Drawing.Bitmap)[0];
        }
        var d = new Dictionary<string, System.Drawing.Bitmap>();
        foreach (var (k, v) in arr)
            d.Add(k, v);
        return d;
    }
    // the ExtraIcons-folder icons that can also be offered in the item-box and overworld tile menus (first 16 by name), resized to item-icon size
    public static readonly string[] customMenuIconNames = ExtraIconsResourceTable.Keys.OrderBy(k => k.ToLowerInvariant(), StringComparer.Ordinal).Take(16).ToArray();
    // the AP Item can be placed (any number of times) only while the Archipelago Support option is on
    public static event Action ArchipelagoSupportChanged;   // (the startup screen and Options menu checkboxes keep each other in step)
    public static void applyArchipelagoSupport()
    {
        TrackerModel.allItemWithHeartShuffleChoiceDomain.SetMaxUse(TrackerModel.ITEMS.APITEM, TrackerModelOptions.ArchipelagoSupport.Value ? 1000 : 0);
        ArchipelagoSupportChanged?.Invoke();
    }
    public static void applyCustomMenuIcons()
    {
        applyArchipelagoSupport();
        var n = customMenuIconNames.Length;
        for (int k = 0; k <= n - 1; k++)
        {
            if (Graphics.customItemBmps[k] == null)
            {
                var src = ExtraIconsResourceTable[customMenuIconNames[k]];
                var dst = new System.Drawing.Bitmap(21, 21, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = System.Drawing.Graphics.FromImage(dst))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(src, 0, 0, 21, 21);
                }
                Graphics.customItemBmps[k] = dst;
            }
            TrackerModel.ITEMS.customNames[k] = customMenuIconNames[k];
        }
        var on = TrackerModelOptions.Overworld.AddCustomIconsToMenus.Value;
        for (int k = 0; k <= TrackerModel.ITEMS.CUSTOM_COUNT - 1; k++)
            TrackerModel.allItemWithHeartShuffleChoiceDomain.SetMaxUse(TrackerModel.ITEMS.CUSTOM_BASE + k, (on && k < n ? 27 : 0));
        TrackerModel.ITEMS.customMenuCount = (on ? n : 0);
    }
    public static readonly Dictionary<string, System.Drawing.Bitmap> ZTrackerResourceTable = make_ZTrackerResourceTable();
    private static Dictionary<string, System.Drawing.Bitmap> make_ZTrackerResourceTable()
    {
        var arr = new List<(string, System.Drawing.Bitmap)>();
        arr.AddRange(new (string, System.Drawing.Bitmap)[] {
            // 7x7s
            ("Ladder", Graphics.ladder_bmp),
            ("Heart", Graphics.heart_container_bmp),
            ("Boomerang", Graphics.boomerang_bmp),
            ("Bow", Graphics.bow_bmp),
            ("MagicBoomerang", Graphics.magic_boomerang_bmp),
            ("Raft", Graphics.raft_bmp),
            ("Recorder", Graphics.recorder_bmp),
            ("Wand", Graphics.wand_bmp),
            ("RedCandle", Graphics.red_candle_bmp),
            ("Book", Graphics.book_bmp),
            ("Key", Graphics.magic_key_bmp),
            ("SilverArrow", Graphics.silver_arrow_bmp),
            ("WoodArrow", Graphics.wood_arrow_bmp),
            ("RedRing", Graphics.red_ring_bmp),
            ("MagicShield", Graphics.magic_shield_bmp),
            ("BoomBook", Graphics.boom_book_bmp),
            ("PowerBracelet", Graphics.power_bracelet_bmp),
            ("WhiteSword", Graphics.ow_sword2_item_bmp),
            ("Armos", Graphics.ow_key_armos_bmp),
            ("WoodSword", Graphics.ow_sword1_item_bmp),
            ("MagicalSword", Graphics.ow_sword3_item_bmp),
            ("BlueCandle", Graphics.blue_candle_bmp),
            ("BlueRing", Graphics.blue_ring_bmp),
            ("Gannon", Graphics.ganon_bmp),
            ("Zelda", Graphics.zelda_bmp),
            ("Bomb", Graphics.bomb_bmp),
            ("BowAndArrow", Graphics.bow_and_arrow_bmp),
            ("Bait", Graphics.bait_bmp),
            ("QuestionMarks", Graphics.question_marks_bmp),
            ("Rupee", Graphics.rupee_bmp),
            ("BasementStair", Graphics.basement_stair_bmp),
            // 16x16 monsters
            ("Digdogger", unborder(Graphics.digdogger_bmp)),
            ("Gleeok", unborder(Graphics.gleeok_bmp)),
            ("Gohma", unborder(Graphics.gohma_bmp)),
            ("Manhandla", unborder(Graphics.manhandla_bmp)),
            ("Wizzrobe", unborder(Graphics.wizzrobe_bmp)),
            ("Patra", unborder(Graphics.patra_bmp)),
            ("Dodongo", unborder(Graphics.dodongo_bmp)),
            ("RedBubble", unborder(Graphics.red_bubble_bmp)),
            ("BlueBubble", unborder(Graphics.blue_bubble_bmp)),
            ("Darknut", unborder(Graphics.blue_darknut_bmp)),
            ("Yellow", unborder(Graphics.other_monster_bmp)),
            // some others
            ("OldMan", unborder(Graphics.old_man_bmp)),
            ("Fairy", Graphics.fairy_bmp),
            ("CheckMark", Graphics.iconCheckMark_bmp),
            ("Rock", unborder(Graphics.zi_rock)),
            ("Tree", unborder(Graphics.zi_tree)),
            // 16x16 items
            ("Triforce", unborder(Graphics.zi_triforce_bmp)),
            ("FiveRupee", unborder(Graphics.zi_fiver_bmp)),
            ("Map", unborder(Graphics.zi_map_bmp)),
            ("Compass", unborder(Graphics.zi_compass_bmp)),
        });
        for (int i = 0; i <= Graphics.theInteriorBmpTable.Length - 2; i++)  // -2 rather than -1 because DarkX is not useful as an icon
            arr.Add(($"Overworld{i,2}", Graphics.theInteriorBmpTable[i][0]));   // (F#: sprintf "Overworld%2d")
        arr.AddRange(ExtraZTbmps.All);
        var d = new Dictionary<string, System.Drawing.Bitmap>();
        foreach (var (k, v) in arr)
            d.Add(k, v);
        return d;
    }

    // (F#: `open DungeonSaveAndLoad  // has Model types for DrawingLayer`, a `using static` above)

    // (F#: a (DrawingLayerIcon * Image) option; null for None)
    public static (DrawingLayerIcon, Image)? mouse = null;
    public static readonly Border imgHighlightBorder = new Border { BorderThickness = new Thickness(2.0), BorderBrush = Brushes.Pink, Width = 36.0, Height = 36.0, Opacity = 0.0 };

    public static Image makeImage(System.Drawing.Bitmap bmp)
    {
        var img = Graphics.BMPtoImage(bmp);
        img.RenderTransformOrigin = RelativePoint.TopLeft;   // (WPF scales from the top-left corner; Avalonia's default is the centre)
        if (bmp.Width == 16 || bmp.Height == 16)
        {
            img.Width = 32.0;
            img.Height = 32.0;
            RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None);
            img.Stretch = Stretch.Uniform;
        }
        else
        {
            img.Width = 32.0;
            img.Height = 32.0;
            img.Stretch = Stretch.Uniform;
            img.StretchDirection = StretchDirection.DownOnly;
        }
        img.IsHitTestVisible = true;
        img.PointerEntered += (_, _ea) =>   // (WPF: MouseEnter)
        {
            if (mouse == null)  // don't interact with existing images while mouse is carrying a new one
            {
                imgHighlightBorder.Opacity = 1.0;
                var pos = img.TranslatePoint(new Point(), CanvasManager.TheOnlyCanvasManager.AppMainCanvas).Value;
                Canvas.SetLeft(imgHighlightBorder, pos.X - 2.0);
                Canvas.SetTop(imgHighlightBorder, pos.Y - 2.0);
            }
        };
        img.PointerExited += (_, _ea) =>   // (WPF: MouseLeave)
        {
            imgHighlightBorder.Opacity = 0.0;
        };
        img.PointerPressed += (_, ea) =>  // only lives in a IsHitTestVisible canvas during interaction mode, so can always have this code   // (WPF: MouseDown)
        {
            if (mouse == null)  // don't interact with existing images while mouse is carrying a new one
            {
                var p = img.Parent;
                switch (p)
                {
                    case Canvas c:
                        DrawingLayerIcon remove()
                        {
                            c.Children.Remove(img);
                            var toRemove = -1;
                            for (int i = 0; i <= AllDrawingLayerStamps.Count - 1; i++)
                            {
                                var (_, _, _, x) = AllDrawingLayerStamps[i];
                                if (object.ReferenceEquals(x, img))
                                    toRemove = i;
                            }
                            if (toRemove != -1)
                            {
                                var (icon, _, _, _) = AllDrawingLayerStamps[toRemove];
                                AllDrawingLayerStamps.RemoveAt(toRemove);
                                ea.Handled = true;
                                return icon;
                            }
                            else
                                throw new Exception("stamp not found");
                        }
                        var kind = ea.GetCurrentPoint(img).Properties.PointerUpdateKind;   // (WPF: ea.ChangedButton)
                        // left-click pick up to move
                        if (kind == PointerUpdateKind.LeftButtonPressed)
                        {
                            var icon = remove();
                            mouse = (icon, img);
                        }
                        // right-click = remove self
                        else if (kind == PointerUpdateKind.RightButtonPressed)
                            remove();
                        // middle click toggles half-size
                        else if (kind == PointerUpdateKind.MiddleButtonPressed)
                        {
                            // (WPF also treats the shared Transform.Identity as "no transform"; Avalonia has none, see DungeonSaveAndLoad.SaveDrawingLayer)
                            if (img.RenderTransform == null)
                                img.RenderTransform = new ScaleTransform(0.5, 0.5);
                            else
                                img.RenderTransform = null;
                        }
                        break;
                    default:
                        break;
                }
            }
        };
        return img;
    }

    public static void LoadDrawingLayer(DrawingLayerIconModel[] model, Canvas drawingCanvas)
    {
        if (model != null)
        {
            foreach (var icon in model)
            {
                if (icon.Extra)
                {
                    if (icon.Name != null && ExtraIconsResourceTable.ContainsKey(icon.Name))
                    {
                        var img = makeImage(ExtraIconsResourceTable[icon.Name]);
                        if (icon.HalfSize) img.RenderTransform = new ScaleTransform(0.5, 0.5);
                        AllDrawingLayerStamps.Add((new DrawingLayerIcon.ExtraIcon(icon.Name), icon.X, icon.Y, img));
                    }
                    else if (icon.Name != null)
                    {
                        // (the icon file is missing or could not be read: keep the stamp as an empty image, so the next save still has it)
                        var img = new Image { Width = 32.0, Height = 32.0, IsHitTestVisible = false, RenderTransformOrigin = RelativePoint.TopLeft };
                        if (icon.HalfSize) img.RenderTransform = new ScaleTransform(0.5, 0.5);
                        AllDrawingLayerStamps.Add((new DrawingLayerIcon.ExtraIcon(icon.Name), icon.X, icon.Y, img));
                    }
                }
                else
                {
                    if (icon.Name != null && ZTrackerResourceTable.ContainsKey(icon.Name))
                    {
                        var img = makeImage(ZTrackerResourceTable[icon.Name]);
                        if (icon.HalfSize) img.RenderTransform = new ScaleTransform(0.5, 0.5);
                        AllDrawingLayerStamps.Add((new DrawingLayerIcon.ZTracker(icon.Name), icon.X, icon.Y, img));
                    }
                }
            }
        }
        foreach (var (_, x, y, img) in AllDrawingLayerStamps)
            canvasAdd(drawingCanvas, img, (double)x, (double)y);
    }

    public static async Task InteractWithDrawingLayer(CanvasManager cm, Canvas drawingCanvas)
    {
        var wh = new ManualResetEvent(false);
        var thisSessionHasEnded = false;  // (the RootCanvas handlers below are removed when the session ends; this also stops them if one fires late)

        var interactionCanvas = new Canvas { Width = cm.AppMainCanvas.Width, Height = cm.AppMainCanvas.Height };
        interactionCanvas.Children.Add(imgHighlightBorder);
        var topCanvasToHoldIcons = new Canvas { Width = cm.AppMainCanvas.Width, Height = OverworldItemGridUI.THRU_MAIN_MAP_AND_ITEM_PROGRESS_H, IsHitTestVisible = true };
        interactionCanvas.Children.Add(topCanvasToHoldIcons);
        var mouseCarry = new Border { BorderBrush = Brushes.White, BorderThickness = new Thickness(1.0), Opacity = 0.0, IsHitTestVisible = false };
        var DIFF = 31.0;
        EventHandler<PointerEventArgs> onRootMoved = (_, ea) =>   // (WPF: MouseMove)
        {
            if (!thisSessionHasEnded)
            {
                if (mouse is { } m)   // (F#: | Some(_icon, img) ->)
                {
                    var (_icon, img) = m;
                    var pos = ea.GetPosition(cm.AppMainCanvas);
                    if (pos.Y < OverworldItemGridUI.THRU_MAIN_MAP_AND_ITEM_PROGRESS_H)
                    {
                        interactionCanvas.Children.Remove(mouseCarry);
                        interactionCanvas.Children.Add(mouseCarry);
                        mouseCarry.Child = img;
                        mouseCarry.Opacity = 0.6;
                        Canvas.SetLeft(mouseCarry, pos.X - DIFF);
                        Canvas.SetTop(mouseCarry, pos.Y - DIFF);
                    }
                }
            }
        };
        EventHandler<PointerPressedEventArgs> onRootPressed = (_, ea) =>   // (WPF: MouseDown)
        {
            if (!thisSessionHasEnded)
            {
                if (mouse is { } m)   // (F#: | Some(icon, img) ->)
                {
                    var (icon, img) = m;
                    var pos = ea.GetPosition(cm.AppMainCanvas);
                    if (pos.Y < OverworldItemGridUI.THRU_MAIN_MAP_AND_ITEM_PROGRESS_H)
                    {
                        var kind = ea.GetCurrentPoint(cm.RootCanvas).Properties.PointerUpdateKind;   // (WPF: ea.ChangedButton)
                        // left-click = place a stamp
                        if (kind == PointerUpdateKind.LeftButtonPressed)
                        {
                            AllDrawingLayerStamps.Add((icon, (int)(pos.X - DIFF), (int)(pos.Y - DIFF), img));
                            mouse = null;
                            mouseCarry.Child = null;
                            mouseCarry.Opacity = 0.0;
                            canvasAdd(topCanvasToHoldIcons, img, pos.X - DIFF, pos.Y - DIFF);
                            ea.Handled = true;
                        }
                        // right-click = quit carrying stamp
                        else if (kind == PointerUpdateKind.RightButtonPressed)
                        {
                            mouse = null;
                            mouseCarry.Child = null;
                            mouseCarry.Opacity = 0.0;
                            ea.Handled = true;
                        }
                    }
                }
            }
        };
        cm.RootCanvas.PointerMoved += onRootMoved;
        cm.RootCanvas.PointerPressed += onRootPressed;

        var bottomCanvas = new Canvas { Width = interactionCanvas.Width, Height = interactionCanvas.Height - OverworldItemGridUI.THRU_MAIN_MAP_AND_ITEM_PROGRESS_H, Background = Brushes.Black };
        canvasAdd(interactionCanvas, bottomCanvas, 0.0, OverworldItemGridUI.THRU_MAIN_MAP_AND_ITEM_PROGRESS_H);
        bottomCanvas.PointerMoved += (_, _ea) =>   // (WPF: MouseMove)
        {
            interactionCanvas.Children.Remove(mouseCarry); // if the mouse move into the bottom zone, remove the trailing icon
        };

        var alt = Graphics.freeze(new SolidColorBrush(Color.FromRgb(0x5F, 0x2F, 0x5F)));
        TextBox mkTxtImpl(string text, double bt, IBrush bg) => new TextBox { Text = text, BorderBrush = bg, BorderThickness = new Thickness(bt), FontSize = 12.0, Foreground = Brushes.Orange, Background = Brushes.Black,
                                                                              IsHitTestVisible = false, IsReadOnly = true };
        TextBox mkTxtBT(string text, double bt) => mkTxtImpl(text, bt, Brushes.DarkSlateGray);
        TextBox mkTxt(string text) => mkTxtImpl(text, 1.0, Brushes.DarkSlateGray);
        TextBox mkTxtAlt(string text) => mkTxtImpl(text, 1.0, alt);

        var hsp = new StackPanel { Orientation = Orientation.Horizontal };
        hsp.Children.Add(mkTxt("Left click a button below\nto 'pick up' an icon"));
        hsp.Children.Add(new DockPanel { Width = 8.0 });
        hsp.Children.Add(mkTxt("Then left click above\nto place it anywhere"));
        hsp.Children.Add(new DockPanel { Width = 8.0 });
        hsp.Children.Add(mkTxtAlt("Left click a placed icon\nto pick it up again"));
        hsp.Children.Add(new DockPanel { Width = 8.0 });
        hsp.Children.Add(mkTxtAlt("Right click a placed\nicon to remove it"));
        hsp.Children.Add(new DockPanel { Width = 8.0 });
        hsp.Children.Add(mkTxt("When finished, click\nbutton to the right"));
        hsp.Children.Add(new DockPanel { Width = 8.0 });
        var doneEditingButton = Graphics.makeButton("Done editing", 20.0, Brushes.Orange);
        hsp.Children.Add(doneEditingButton);
        canvasAdd(bottomCanvas, hsp, 10.0, 10.0);
        doneEditingButton.Click += (_, _) => wh.Set();

        canvasAdd(bottomCanvas, mkTxtAlt("Middle click a placed icon to toggle it half-size"), 275.0, 45.0);

        // Native icons
        var ztIconGrid = new UniformGrid { Rows = 8, Columns = 12 };
        foreach (var kv in ZTrackerResourceTable)
        {
            var (k, v) = (kv.Key, kv.Value);
            var img = makeImage(v);
            img.IsHitTestVisible = false; // the button label should not respond to clicks
            var c = new Canvas { Width = 32.0, Height = 32.0 };
            var dark = new Canvas { Width = 32.0, Height = 32.0, Background = Brushes.Black, Opacity = 0.65 };
            c.Children.Add(dark);
            c.Children.Add(img);
            var button = new Button { Content = c, Background = Graphics.almostBlack };
            button.Click += (_, _) =>
            {
                mouse = (new DrawingLayerIcon.ZTracker(k), makeImage(v));
            };
            ztIconGrid.Children.Add(button);
        }
        canvasAdd(bottomCanvas, mkTxtBT("Here are some default icons that come with ZORATracker:", 0.0), 10.0, 65.0);
        canvasAdd(bottomCanvas, ztIconGrid, 10.0, 85.0);
        // ExtraIcons
        var extraIconGrid = new UniformGrid { Rows = 7, Columns = 8 };
        foreach (var kv in ExtraIconsResourceTable)
        {
            var (k, v) = (kv.Key, kv.Value);
            var img = makeImage(v);
            img.IsHitTestVisible = false; // the button label should not respond to clicks
            var c = new Canvas { Width = 32.0, Height = 32.0 };
            var dark = new Canvas { Width = 32.0, Height = 32.0, Background = Brushes.Black, Opacity = 0.65 };
            c.Children.Add(dark);
            c.Children.Add(img);
            var button = new Button { Content = c, Background = Graphics.almostBlack };
            button.Click += (_, _) =>
            {
                mouse = (new DrawingLayerIcon.ExtraIcon(k), makeImage(v));
            };
            extraIconGrid.Children.Add(button);
        }
        var X = 460.0;
        var customCB = new CheckBox { Content = mkTxtBT("Add custom icons to menus", 0.0), IsChecked = TrackerModelOptions.Overworld.AddCustomIconsToMenus.Value };
        ((Control)customCB.Content).IsHitTestVisible = false;   // clicks on the label go to the checkbox
        ToolTip.SetTip(customCB, "Adds (up to 16 of) your ExtraIcons icons to the bottom of the item-box menu and the overworld tile marker-icon menu");
        void onCB()
        {
            TrackerModelOptions.Overworld.AddCustomIconsToMenus.Value = customCB.IsChecked.Value;
            applyCustomMenuIcons();
            TrackerModelOptions.writeSettings();
        }
        Graphics.onChecked(customCB, () => onCB());
        Graphics.onUnchecked(customCB, () => onCB());
        // (WPF: customCB.LayoutTransform <- ScaleTransform(1.5, 1.5); Avalonia has no LayoutTransform property, so the checkbox sits in a LayoutTransformControl)
        var customCBScaled = new LayoutTransformControl { LayoutTransform = new ScaleTransform(1.5, 1.5), Child = customCB };
        canvasAdd(bottomCanvas, customCBScaled, X, 72.0);
        var openFolderButton = Graphics.makeButton("Open ExtraIcons Folder", 12.0, Brushes.Orange);
        canvasAdd(bottomCanvas, openFolderButton, X, 110.0);
        canvasAdd(bottomCanvas, mkTxtBT("<- add .png files here", 0.0), X + 150.0, 110.0);
        canvasAdd(bottomCanvas, mkTxtBT("(must re-start ZORATracker to reload these icons)", 0.0), X, 130.0);
        canvasAdd(bottomCanvas, mkTxtBT("Here are icons found in your ExtraIcons folder:", 0.0), X, 150.0);
        canvasAdd(bottomCanvas, extraIconGrid, X, 170.0);
        openFolderButton.Click += (_, _) =>
        {
            Platform.OpenWithShell(extraIconsDirectory);   // (WPF: Process.Start(ProcessStartInfo("Explorer.exe", extraIconsDirectory)))
        };
        if (anyErrorMessageRelatedToExtraIcons != "")
        {
            var errorTB = mkTxt("Error while loading ExtraIcons:\n" + anyErrorMessageRelatedToExtraIcons);
            bottomCanvas.Children.Add(errorTB);
            Canvas.SetLeft(errorTB, X);
            extraIconGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetTop(errorTB, 170.0 + extraIconGrid.DesiredSize.Height + 10.0);
        }

        // when invoked...

        // move all images from top layer to here (note: they retain attached properties for Canvas.Left/Top)
        var all = new List<Control>();
        for (int i = 0; i <= drawingCanvas.Children.Count - 1; i++)
            all.Add(drawingCanvas.Children[i]);
        drawingCanvas.Children.Clear();
        foreach (var x in all)
            topCanvasToHoldIcons.Children.Add(x);
        // interact
        await DoModalDockedOpa(cm, wh, Dock.Bottom, interactionCanvas, 0.35);
        // turn off the heavy parts of the mouse listener, which will continue to fire
        mouse = null;
        thisSessionHasEnded = true;
        cm.RootCanvas.PointerMoved -= onRootMoved;
        cm.RootCanvas.PointerPressed -= onRootPressed;
        // cleanup
        interactionCanvas.Children.Remove(imgHighlightBorder);
        // move images back to top layer
        var all2 = new List<Control>();
        for (int i = 0; i <= topCanvasToHoldIcons.Children.Count - 1; i++)
            all2.Add(topCanvasToHoldIcons.Children[i]);
        topCanvasToHoldIcons.Children.Clear();
        foreach (var x in all2)
            drawingCanvas.Children.Add(x);
    }
}
