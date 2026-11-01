// Ported from Z1R_WPF/DungeonRoomState.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
// (F#: `module DungeonRoomState` holding `type DungeonRoomState`. As with CustomMessageBox, C# doesn't allow a nested type with the
// same name as its enclosing class (CS0542), so the module becomes a namespace. The module's own values (canvasAdd, cachedTilePairs,
// mkTxt, ...) become static members of the class, ahead of its instance members, so callers write `DungeonRoomState.DungeonRoomState`
// for the type and `DungeonRoomState.DungeonRoomState.mkTxt(...)` for WPF's `DungeonRoomState.mkTxt(...)`.)
// The room model itself is Gob's C# CDungeonRoomState (Z1R_Sync, via Z1R_Avalonia_Sync).
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Z1R_Tracker.Models;
using Z1R_Tracker.Models.Z1R_TrackerInterop;

namespace Z1R_Avalonia.DungeonRoomState;

public class DungeonRoomState
{
    public static void canvasAdd(Canvas c, Control item, double left, double top) => Graphics.canvasAdd(c, item, left, top);

    public static readonly Tuple<System.Drawing.Bitmap, System.Drawing.Bitmap>[] cachedTilePairs =
        RoomTypeGraphics.LoadUpscaledRoomBitmapPairs(RoomTypeGraphics.GetResourceStreamFromWPF("new_icons13x9.png"));

    public static TextBox mkTxt(string txt) =>
        new TextBox
        {
            FontSize = 12.0,
            Foreground = Brushes.Orange,
            Background = Brushes.Black,
            IsReadOnly = true,
            IsHitTestVisible = false,
            Text = txt,
            VerticalAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(0.0)
        };

    private static SolidColorBrush make_entranceRoomArrowColorBrush()
    {
        var c = Graphics.entranceRoomArrowColor.Value;
        return Graphics.freeze(new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)));
    }
    public static readonly SolidColorBrush entranceRoomArrowColorBrush = make_entranceRoomArrowColorBrush();

    public static Image scale(System.Drawing.Bitmap bmp, double scale)
    {
        if (bmp == null) return null;
        else
        {
            var icon = Graphics.BMPtoImage(bmp);
            icon.Width = icon.Width * scale;
            icon.Height = icon.Height * scale;
            icon.Stretch = Stretch.UniformToFill;
            RenderOptions.SetBitmapInterpolationMode(icon, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
            return icon;
        }
    }

    public static bool isDoingDragPaintOffTheMap = false;
    public static readonly SolidColorBrush veryDark = Graphics.freeze(new SolidColorBrush(Color.FromArgb(255, 60, 10, 20)));
    public static readonly int[] fakeUsedTransports = make_fakeUsedTransports();
    private static int[] make_fakeUsedTransports() { var a = new int[9]; Array.Fill(a, 2); return a; }

    ////////////////////////////////////////////////////////////
    // (F#: type DungeonRoomState(csharpModel: CDungeonRoomState))

    private readonly CDungeonRoomState csharpModel;
    // (F#: `let changedEvent = new Event<DungeonRoomState>()` exposed as `member Changed = changedEvent.Publish`; only this class triggers it,
    // so it's a C# event: callers write `drs.Changed += (_drs) => ...` for `drs.Changed.Add(fun _ -> ...)`)
    public event Action<DungeonRoomState> Changed;

    private bool suppressChange = false;

    public DungeonRoomState(CDungeonRoomState csharpModel)
    {
        this.csharpModel = csharpModel;
        csharpModel.Changed += (_, _) =>
        {
            if (!suppressChange)
            {
                Changed?.Invoke(this);
                TrackerModel.dungeonRoomModelChanged.SetNowIfLocal(TrackerModelOptions.isCurrentlyApplyingRemoteUpdate());
            }
        };
    }

    public DungeonRoomState() : this(new CDungeonRoomState()) { }

    public CDungeonRoomState CSharp => csharpModel;




    public static DungeonRoomState FromCSharp(CDungeonRoomState csharp)
    {
        var drs = new DungeonRoomState();
        drs.CSharp.IsComplete = csharp.IsComplete;
        drs.CSharp.RoomType = csharp.RoomType;
        drs.CSharp.MonsterDetail = csharp.MonsterDetail;
        drs.CSharp.FloorDropDetail = csharp.FloorDropDetail;
        drs.CSharp.FloorDropAppearsBright = csharp.FloorDropAppearsBright;
        drs.CSharp.X = csharp.X;
        drs.CSharp.Y = csharp.Y;
        drs.CSharp.Level = csharp.Level;
        return drs;
    }

    public bool IsComplete
    {
        get => csharpModel.IsComplete;
        set
        {
            if (csharpModel.IsComplete != value)
            {
                suppressChange = true;
                csharpModel.IsComplete = value;
                suppressChange = false;
                Changed?.Invoke(this);
            }
        }
    }

    public RoomType RoomType
    {
        get => csharpModel.RoomType;
        set
        {
            if (csharpModel.RoomType != value)
            {
                suppressChange = true;
                csharpModel.RoomType = value;
                suppressChange = false;
                Changed?.Invoke(this);
            }
        }
    }

    public MonsterDetail MonsterDetail
    {
        get => csharpModel.MonsterDetail;
        set
        {
            if (csharpModel.MonsterDetail != value)
            {
                suppressChange = true;
                csharpModel.MonsterDetail = value;
                suppressChange = false;
                Changed?.Invoke(this);
            }
        }
    }

    public FloorDropDetail FloorDropDetail
    {
        get => csharpModel.FloorDropDetail;
        set
        {
            if (csharpModel.FloorDropDetail != value)
            {
                suppressChange = true;
                csharpModel.FloorDropDetail = value;
                suppressChange = false;
                Changed?.Invoke(this);
            }
        }
    }

    public bool FloorDropAppearsBright
    {
        get => csharpModel.FloorDropAppearsBright;
        set
        {
            if (csharpModel.FloorDropAppearsBright != value)
            {
                suppressChange = true;
                csharpModel.FloorDropAppearsBright = value;
                suppressChange = false;
                Changed?.Invoke(this);
            }
        }
    }

    public Guid DebugId
    {
        get => csharpModel.DebugId;
        set
        {
            if (csharpModel.DebugId != value)
            {
                suppressChange = true;
                csharpModel.DebugId = value;
                suppressChange = false;
            }
        }
    }

    public int X
    {
        get => csharpModel.X;
        set
        {
            if (csharpModel.X != value)
            {
                suppressChange = true;
                csharpModel.X = value;
                suppressChange = false;
                Changed?.Invoke(this);
            }
        }
    }

    public int Y
    {
        get => csharpModel.Y;
        set
        {
            if (csharpModel.Y != value)
            {
                suppressChange = true;
                csharpModel.Y = value;
                suppressChange = false;
                Changed?.Invoke(this);
            }
        }
    }

    public int Level
    {
        get => csharpModel.Level;
        set
        {
            if (csharpModel.Level != value)
            {
                suppressChange = true;
                csharpModel.Level = value;
                suppressChange = false;
                Changed?.Invoke(this);
            }
        }
    }

    public void ToggleFloorDropBrightness()
    {
        this.FloorDropAppearsBright = !this.FloorDropAppearsBright;
    }

    public bool IsEmpty => csharpModel.RoomType.IsNotMarked() || csharpModel.RoomType.IsOffMap();
    public bool IsGannonOrZelda => csharpModel.RoomType.IsGannonOrZelda();

    public DungeonRoomState Clone()
    {
        var result = DungeonRoomState.FromCSharp(this.CSharp.Clone());
        return result;
    }

    public void CopyFrom(DungeonRoomState other)
    {
        this.IsComplete = other.IsComplete;
        this.RoomType = other.RoomType;
        this.MonsterDetail = other.MonsterDetail;
        this.FloorDropDetail = other.FloorDropDetail;
        this.FloorDropAppearsBright = other.FloorDropAppearsBright;
        this.X = other.X;
        this.Y = other.Y;
        this.Level = other.Level;
    }


    public Control CurrentDisplay() => this.CurrentDisplayEx(fakeUsedTransports);

    // (WPF: returns FrameworkElement)
    public Control CurrentDisplayEx(int[] usedTransports)
    {
        var rt = this.RoomType;
        var md = this.MonsterDetail;
        var fd = this.FloorDropDetail;
        var isCompleted = this.IsComplete;
        var appearsBright = this.FloorDropAppearsBright;
        var K = 18.0;

        if (rt == RoomType.Unmarked && md == MonsterDetail.Unmarked && fd == FloorDropDetail.Unmarked)
            return Graphics.BMPtoImage(rt.UncompletedBI());
        else
        {
            var c = new Canvas { Width = 13.0 * 3.0, Height = 9.0 * 3.0 };

            switch (rt)
            {
                case RoomType.OffTheMap:
                    {
                        var black = new Canvas { Width = 13.0 * 3.0 + 12.0, Height = 9.0 * 3.0 + 12.0, Background = Brushes.Black, Opacity = 0.6 };
                        canvasAdd(c, black, -6.0, -6.0);
                        if (isDoingDragPaintOffTheMap)
                            canvasAdd(black, new Avalonia.Controls.Shapes.Rectangle { Width = 13.0 * 3.0, Height = 9.0 * 3.0, Stroke = veryDark, StrokeThickness = 2.0 }, 6.0, 6.0);
                        break;
                    }
                default:
                    {
                        int? spriteOverride = rt switch
                        {
                            RoomType.Transport1 when usedTransports[1] < 2 => 34,
                            RoomType.Transport2 when usedTransports[2] < 2 => 35,
                            RoomType.Transport3 when usedTransports[3] < 2 => 36,
                            RoomType.Transport4 when usedTransports[4] < 2 => 37,
                            RoomType.Transport5 when usedTransports[5] < 2 => 38,
                            RoomType.Transport6 when usedTransports[6] < 2 => 39,
                            RoomType.Transport7 when usedTransports[7] < 2 => 40,
                            RoomType.Transport8 when usedTransports[8] < 2 => 41,
                            _ => null,
                        };

                        System.Drawing.Bitmap bmp;
                        if (spriteOverride is int idx)
                        {
                            var (uncompleted, completed) = cachedTilePairs[idx];
                            bmp = isCompleted ? completed : uncompleted;

                        }
                        else
                            bmp = isCompleted ? rt.CompletedBI() : rt.UncompletedBI();

                        canvasAdd(c, Graphics.BMPtoImage(bmp), 0.0, 0.0);
                        break;
                    }
            }

            switch (rt)
            {
                case RoomType.StartEnterFromE: canvasAdd(c, new Canvas { Background = entranceRoomArrowColorBrush, Width = 3.0, Height = 9.0 }, 13.0 * 3.0, 3.0 * 3.0); break;
                case RoomType.StartEnterFromW: canvasAdd(c, new Canvas { Background = entranceRoomArrowColorBrush, Width = 3.0, Height = 9.0 }, -1.0 * 3.0, 3.0 * 3.0); break;
                case RoomType.StartEnterFromN: canvasAdd(c, new Canvas { Background = entranceRoomArrowColorBrush, Width = 9.0, Height = 3.0 }, 5.0 * 3.0, -1.0 * 3.0); break;
                case RoomType.StartEnterFromS: canvasAdd(c, new Canvas { Background = entranceRoomArrowColorBrush, Width = 9.0, Height = 3.0 }, 5.0 * 3.0, 9.0 * 3.0); break;
                default: break;
            }

            switch (md)
            {
                case MonsterDetail.Unmarked: break;
                default:
                    {
                        var bmp = md.Bmp();
                        if (bmp == null) { }
                        else
                        {
                            var monsterIcon = Graphics.BMPtoImage(bmp);
                            canvasAdd(c, monsterIcon, -5.0, -3.0);
                            if (isCompleted)
                            {
                                var shouldDarken = md switch
                                {
                                    MonsterDetail.BlueBubble or MonsterDetail.RedBubble or MonsterDetail.Other or MonsterDetail.Other2 or MonsterDetail.Traps => false,
                                    _ => true,
                                };
                                if (shouldDarken)
                                {
                                    var dp = new DockPanel { Width = K, Height = K, Background = Brushes.Black, Opacity = 0.5 };
                                    canvasAdd(c, dp, -5.0, -3.0);
                                }
                            }
                        }
                        break;
                    }
            }

            switch (fd)
            {
                case FloorDropDetail.Unmarked: break;
                default:
                    {
                        var bmp = fd.Bmp();
                        if (bmp == null) { }
                        else
                        {
                            var floorDropIcon = Graphics.BMPtoImage(bmp);
                            canvasAdd(c, floorDropIcon, 44.0 - K, 30.0 - K);
                            if (!appearsBright)
                            {
                                var dp = new DockPanel { Width = K, Height = K, Background = Brushes.Black, Opacity = 0.5 };
                                canvasAdd(c, dp, 44.0 - K, 30.0 - K);
                            }
                        }
                        break;
                    }
            }

            return c;
        }
    }
}
