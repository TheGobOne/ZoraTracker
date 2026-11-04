// Ported from Z1R_WPF/Dungeon.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
// The room and door model is Gob's C# Z1R_Sync (CDungeonModelStore, ...), via Z1R_Avalonia_Sync.
// Every F# `async { }` here is an `async Task` method, as in CustomComboBoxes.cs; callers `await` them where WPF used `do!`.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Z1R_Tracker.Models;
using Z1R_Tracker.Models.Z1R_TrackerInterop;

namespace Z1R_Avalonia;

public static class Dungeon
{
    // door colors
    public static readonly double highlightOpacity = 1.0;
    public static readonly IBrush highlight = Brushes.Cyan; // new SolidColorBrush(Color.FromRgb(200uy, 200uy, 200uy)) :> Brush
    public static readonly IBrush unknown = Graphics.freeze(new SolidColorBrush(Color.FromRgb(30, 30, 45)));
    public static readonly IBrush no      = Graphics.freeze(new SolidColorBrush(Color.FromRgb(255, 0, 0)));
    public static readonly IBrush yes     = Graphics.freeze(new SolidColorBrush(Color.FromRgb(60,120,60)));
    public static readonly IBrush yellow  = Graphics.freeze(new SolidColorBrush(Color.FromRgb(160, 160, 40)));
    public static readonly IBrush purple  = Graphics.freeze(new SolidColorBrush(Color.FromRgb(140, 30, 140)));

    // (F#: a [<RequireQualifiedAccess>] union with no data, so a C# enum, as HotKeys' SingleKeyboardModifier. Its members are in
    // DoorStateExtensions (bottom of this file): `s.AsInt()` is unchanged, `Dungeon.DoorState.FromInt(x)` → `DoorStateExtensions.FromInt(x)`,
    // `Dungeon.DoorState.FromString(s)` → `DoorStateExtensions.FromString(s)`.)
    public enum DoorState
    {
        UNKNOWN = 0, NO = 1, YES = 2, YELLOW = 3, PURPLE = 4
    }


    public static class DoorInterop
    {
        public static DoorState fromCSharp(Z1R_Tracker.Models.Z1R_TrackerInterop.DoorState cs)
        {
            switch (cs)
            {
                case Z1R_Tracker.Models.Z1R_TrackerInterop.DoorState.Unknown: return DoorState.UNKNOWN;
                case Z1R_Tracker.Models.Z1R_TrackerInterop.DoorState.No:      return DoorState.NO;
                case Z1R_Tracker.Models.Z1R_TrackerInterop.DoorState.Yes:     return DoorState.YES;
                case Z1R_Tracker.Models.Z1R_TrackerInterop.DoorState.Yellow:  return DoorState.YELLOW;
                case Z1R_Tracker.Models.Z1R_TrackerInterop.DoorState.Purple:  return DoorState.PURPLE;
                default: throw new Exception("Unrecognized DoorState from C# interop");
            }
        }

        public static Z1R_Tracker.Models.Z1R_TrackerInterop.DoorState toCSharp(DoorState fs)
        {
            switch (fs)
            {
                case DoorState.UNKNOWN: return Z1R_Tracker.Models.Z1R_TrackerInterop.DoorState.Unknown;
                case DoorState.NO:      return Z1R_Tracker.Models.Z1R_TrackerInterop.DoorState.No;
                case DoorState.YES:     return Z1R_Tracker.Models.Z1R_TrackerInterop.DoorState.Yes;
                case DoorState.YELLOW:  return Z1R_Tracker.Models.Z1R_TrackerInterop.DoorState.Yellow;
                case DoorState.PURPLE:  return Z1R_Tracker.Models.Z1R_TrackerInterop.DoorState.Purple;
                default: throw new Exception("impossible DoorState");
            }
        }

        public static DoorState fromString(string s)
        {
            switch (s.ToUpperInvariant())
            {
                case "UNKNOWN": return DoorState.UNKNOWN;
                case "NO":      return DoorState.NO;
                case "YES":     return DoorState.YES;
                case "YELLOW":  return DoorState.YELLOW;
                case "PURPLE":  return DoorState.PURPLE;
                case var other: throw new Exception($"Unrecognized DoorState string: {other}");
            }
        }
    }

    public class Door
    {
        private DoorState state;
        private readonly Action<DoorState> redraw;
        private readonly int level, x, y;
        private readonly bool isHorizontal;
        public Door(DoorState state, Action<DoorState> redraw, int level, int x, int y, bool isHorizontal)
        {
            this.state = state;
            this.redraw = redraw;
            this.level = level;
            this.x = x;
            this.y = y;
            this.isHorizontal = isHorizontal;
        }

        public DoorState State
        {
            get => state;
            set
            {
                state = value;
                redraw(value);
                // Sync back to C# model
                var csharpState = DoorInterop.toCSharp(value);
                if (isHorizontal)
                    CDungeonModelStore.HorizontalDoors[level - 1][y, x] = csharpState;
                else
                    CDungeonModelStore.VerticalDoors[level - 1][y, x] = csharpState;
            }
        }

        public void Next()
        {
            var newState =
                state switch
                {
                    DoorState.UNKNOWN => DoorState.YES,
                    DoorState.YES => DoorState.NO,
                    DoorState.NO => DoorState.YELLOW,
                    DoorState.YELLOW => DoorState.PURPLE,
                    DoorState.PURPLE => DoorState.UNKNOWN,
                    _ => throw new Exception("impossible DoorState"),
                };
            this.State = newState;
        }

        public void Prev()
        {
            var newState =
                state switch
                {
                    DoorState.UNKNOWN => DoorState.PURPLE,
                    DoorState.PURPLE => DoorState.YELLOW,
                    DoorState.YELLOW => DoorState.NO,
                    DoorState.NO => DoorState.YES,
                    DoorState.YES => DoorState.UNKNOWN,
                    _ => throw new Exception("impossible DoorState"),
                };
            this.State = newState;
        }

        public void Redraw() => redraw(state);

        public bool IsTraversible =>
            state == DoorState.YES || state == DoorState.YELLOW || state == DoorState.PURPLE;
    }

    public static Action<int, int, int> roomChangedCallback = (_, _, _) => { };

    public static void SetRoomChangedCallback(Action<int, int, int> cb)
    {
        roomChangedCallback = cb;
    }

    public static System.Diagnostics.Process signalRHostProcess = null;

    // (WPF: the Windows Job Object declarations (CreateJobObject, AssignProcessToJobObject, SetInformationJobObject), `signalRJobHandle`
    // and `createSignalRJob()` are here. They're Win32-only, so they moved unchanged into Platform.cs, behind
    // Platform.StartChildProcessTiedToApp, which startLocalSignalRHost calls.)

    public static void startLocalSignalRHost(string port)
    {
        try
        {
            var exePath = Platform.Files.App("Z1R_SignalRHost.exe");
            if (System.IO.File.Exists(exePath))
            {
                // (job creation, the ProcessStartInfo (no shell execute, no window) and adding the process to the job: Platform.StartChildProcessTiedToApp)
                var proc = Platform.StartChildProcessTiedToApp(exePath, port);
                if (proc != null)   // (null: not implemented on this platform yet)
                {
                    signalRHostProcess = proc;
                    Console.WriteLine($"[Sync] Launched SignalR host on port {port}");
                }
            }
            else
                Console.WriteLine($"[Sync] Host EXE not found at {exePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Sync] Launch error: {ex.Message}");
        }
    }

    public static void stopLocalSignalRHost()
    {
        if (signalRHostProcess != null)
        {
            var proc = signalRHostProcess;
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill();
                    proc.WaitForExit(3000);
                }
                proc.Dispose();
                Console.WriteLine("[Sync] Stopped Z1R_SignalRHost.exe");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sync] Error stopping host: {ex.Message}");
            }
            signalRHostProcess = null;
        }
    }

    public class GrabHelper
    {
        private bool isGrabMode = false;
        private int grabMouseX = -1, grabMouseY = -1;
        private readonly bool[,] grabContiguousRooms = new bool[8, 8];
        private DungeonRoomState[,] roomStatesIfGrabWereACut = null;
        private bool[,] roomIsCircledIfGrabWereACut = null;
        private DoorState[,] horizontalDoorsIfGrabWereACut = null;
        private DoorState[,] verticalDoorsIfGrabWereACut = null;
        private readonly bool[,] selectedRooms = new bool[8, 8];   // rooms picked one by one (instead of a contiguous flood) to be cut out and moved

        // (port helper: F#'s Array2D.init)
        private static T[,] init2D<T>(int n1, int n2, Func<int, int, T> f)
        {
            var a = new T[n1, n2];
            for (int x = 0; x < n1; x++)
                for (int y = 0; y < n2; y++)
                    a[x, y] = f(x, y);
            return a;
        }

        public bool[,] PreviewGrab(int mouseX, int mouseY, DungeonRoomState[,] roomStates)
        {
            if (!this.IsGrabMode || this.HasGrab)
                throw new Exception("bad");
            if (RoomTypeExtensions.IsNotMarked(roomStates[mouseX, mouseY].RoomType))
                throw new Exception("bad");
            // compute set of contiguous rooms
            var contiguous = new bool[8, 8];
            var q = new Queue<(int, int)>();
            q.Enqueue((mouseX, mouseY));
            while (q.Count > 0)
            {
                var (roomX, roomY) = q.Dequeue();
                contiguous[roomX, roomY] = true;
                bool unvisited(int x, int y) => x >= 0 && x <= 7 && y >= 0 && y <= 7 && !contiguous[x, y];
                void attempt(int x, int y) { if (unvisited(x, y) && !(roomStates[x, y].IsEmpty)) q.Enqueue((x, y)); }
                attempt(roomX - 1, roomY);
                attempt(roomX + 1, roomY);
                attempt(roomX, roomY - 1);
                attempt(roomX, roomY + 1);
            }
            // return them
            return contiguous;
        }
        public bool[,] Selected => selectedRooms;
        public bool HasSelection => selectedRooms.Cast<bool>().Any(b => b);
        public void ToggleSelect(int x, int y) => selectedRooms[x, y] = !selectedRooms[x, y];
        // cut out exactly the rooms that were picked (mouseX/mouseY = the room the cut was started on, the anchor that the drop position is measured from)
        public bool[,] StartGrabFromSelection(int mouseX, int mouseY, DungeonRoomState[,] roomStates, bool[,] roomIsCircled, Door[,] horizontalDoors, Door[,] verticalDoors)
        {
            var contigous = init2D(8, 8, (x, y) => selectedRooms[x, y]);
            grabMouseX = mouseX;
            grabMouseY = mouseY;
            for (int x = 0; x < 8; x++) for (int y = 0; y < 8; y++) grabContiguousRooms[x, y] = contigous[x, y];
            roomStatesIfGrabWereACut = init2D(8, 8, (x, y) => roomStates[x, y].Clone());
            roomIsCircledIfGrabWereACut = init2D(8, 8, (x, y) => contigous[x, y] ? false : roomIsCircled[x, y]);
            horizontalDoorsIfGrabWereACut = init2D(7, 8, (x, y) => contigous[x, y] || contigous[x + 1, y] ? DoorState.UNKNOWN : horizontalDoors[x, y].State);
            verticalDoorsIfGrabWereACut = init2D(8, 7, (x, y) => contigous[x, y] || contigous[x, y + 1] ? DoorState.UNKNOWN : verticalDoors[x, y].State);
            return contigous;
        }
        public bool[,] StartGrab(int mouseX, int mouseY, DungeonRoomState[,] roomStates, bool[,] roomIsCircled, Door[,] horizontalDoors, Door[,] verticalDoors)
        {
            var contigous = this.PreviewGrab(mouseX, mouseY, roomStates);
            // save them
            grabMouseX = mouseX;
            grabMouseY = mouseY;
            for (int x = 0; x < 8; x++) for (int y = 0; y < 8; y++) grabContiguousRooms[x, y] = contigous[x, y];
            roomStatesIfGrabWereACut = init2D(8, 8, (x, y) =>
            {
                var clone = roomStates[x, y].Clone();
                return clone;
            });

            roomIsCircledIfGrabWereACut = init2D(8, 8, (x, y) => contigous[x, y] ? false : roomIsCircled[x, y]);
            horizontalDoorsIfGrabWereACut = init2D(7, 8, (x, y) => contigous[x, y] || contigous[x + 1, y] ? DoorState.UNKNOWN : horizontalDoors[x, y].State);
            verticalDoorsIfGrabWereACut = init2D(8, 7, (x, y) => contigous[x, y] || contigous[x, y + 1] ? DoorState.UNKNOWN : verticalDoors[x, y].State);
            return contigous;
        }

        public (bool[,], bool[,]) PreviewDrop(int mouseX, int mouseY)
        {
            if (!this.IsGrabMode || !this.HasGrab)
                throw new Exception("bad");
            var (dx, dy) = (mouseX - grabMouseX, mouseY - grabMouseY);
            var contiguousOk = new bool[8, 8];
            var contiguousWarn = new bool[8, 8];
            for (int x = 0; x <= 7; x++)
            {
                for (int y = 0; y <= 7; y++)
                {
                    var (i, j) = (x - dx, y - dy);
                    if (i >= 0 && i <= 7 && j >= 0 && j <= 7)
                    {
                        if (grabContiguousRooms[i, j])
                        {
                            if (!(roomStatesIfGrabWereACut[x, y].IsEmpty))
                                contiguousWarn[x, y] = true;
                            else
                                contiguousOk[x, y] = true;
                        }
                    }
                }
            }
            return (contiguousOk, contiguousWarn);
        }

        public void DoDrop(int mouseX, int mouseY, DungeonRoomState[,] roomStates, bool[,] roomIsCircled, Door[,] horizontalDoors, Door[,] verticalDoors)  // mutates arrays
        {
            if (!this.IsGrabMode || !this.HasGrab)
                throw new Exception("bad");
            var (dx, dy) = (mouseX - grabMouseX, mouseY - grabMouseY);
            var oldRoomStates = init2D(roomStates.GetLength(0), roomStates.GetLength(1), (x, y) =>
            {
                var s = roomStates[x, y];
                var clone = s.Clone();
                return (clone, s);  // return both clone and original reference
            });

            var oldRoomIsCircled = (bool[,])roomIsCircled.Clone();
            var oldHorizontalDoors = init2D(horizontalDoors.GetLength(0), horizontalDoors.GetLength(1), (x, y) => horizontalDoors[x, y].State);
            var oldVerticalDoors = init2D(verticalDoors.GetLength(0), verticalDoors.GetLength(1), (x, y) => verticalDoors[x, y].State);
            for (int x = 0; x < roomStatesIfGrabWereACut.GetLength(0); x++)
                for (int y = 0; y < roomStatesIfGrabWereACut.GetLength(1); y++)
                {
                    var clone = roomStatesIfGrabWereACut[x, y];
                    var target = roomStates[x, y];
                    target.CSharp.CopyFrom(clone.CSharp);
                    target.CSharp.FireChangedManually();
                }


            for (int x = 0; x < roomIsCircledIfGrabWereACut.GetLength(0); x++) for (int y = 0; y < roomIsCircledIfGrabWereACut.GetLength(1); y++) roomIsCircled[x, y] = roomIsCircledIfGrabWereACut[x, y];
            for (int x = 0; x < horizontalDoorsIfGrabWereACut.GetLength(0); x++) for (int y = 0; y < horizontalDoorsIfGrabWereACut.GetLength(1); y++) horizontalDoors[x, y].State = horizontalDoorsIfGrabWereACut[x, y];
            for (int x = 0; x < verticalDoorsIfGrabWereACut.GetLength(0); x++) for (int y = 0; y < verticalDoorsIfGrabWereACut.GetLength(1); y++) verticalDoors[x, y].State = verticalDoorsIfGrabWereACut[x, y];
            for (int i = 0; i <= 7; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    if (grabContiguousRooms[i, j])
                    {
                        var (x, y) = (i + dx, j + dy);
                        if (x >= 0 && x <= 7 && y >= 0 && y <= 7)
                        {
                            var source = oldRoomStates[i, j].Item1;
                            var destination = roomStates[x, y];

                            // Move room data
                            destination.CSharp.CopyFrom(source.CSharp);
                            destination.CSharp.X = x;
                            destination.CSharp.Y = y;
                            destination.CSharp.FireChangedManually();

                            roomIsCircled[x, y] = oldRoomIsCircled[i, j];

                            void do_door(Door[,] target, int x, int y, DoorState[,] source, int i, int j)
                            {
                                if (source[i, j] != DoorState.UNKNOWN)
                                    target[x, y].State = source[i, j];
                            }
                            if (x < 7 && i < 7) do_door(horizontalDoors, x, y, oldHorizontalDoors, i, j);
                            if (x > 0 && i > 0) do_door(horizontalDoors, x - 1, y, oldHorizontalDoors, i - 1, j);
                            if (y < 7 && j < 7) do_door(verticalDoors, x, y, oldVerticalDoors, i, j);
                            if (y > 0 && j > 0) do_door(verticalDoors, x, y - 1, oldVerticalDoors, i, j - 1);
                        }
                    }
                }
            }

            // Second pass: clear source positions that are not also destinations of another grabbed room.
            // Doing this in the same loop as the copy causes rooms to be erroneously cleared when the
            // destination of one grabbed room coincides with the source of another (e.g. moving a group right).
            for (int i = 0; i <= 7; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    if (grabContiguousRooms[i, j])
                    {
                        var (si, sj) = (i - dx, j - dy);
                        var isAlsoDestination = si >= 0 && si <= 7 && sj >= 0 && sj <= 7 && grabContiguousRooms[si, sj];
                        if (!isAlsoDestination)
                            roomStates[i, j].CSharp.Clear();
                    }
                }
            }

            this.Abort();
        }

        public void ToggleGrabMode() => isGrabMode = !isGrabMode;
        public bool IsGrabMode => isGrabMode;
        public bool HasGrab => grabMouseX != -1;
        public void Abort()
        {
            isGrabMode = false;
            grabMouseX = -1;
            grabMouseY = -1;
            for (int x = 0; x <= 7; x++)
            {
                for (int y = 0; y <= 7; y++)
                {
                    grabContiguousRooms[x, y] = false;
                    selectedRooms[x, y] = false;
                }
            }
            roomStatesIfGrabWereACut = null;
            roomIsCircledIfGrabWereACut = null;
        }
        public void Log()
        {
            Console.WriteLine("log");
        }
    }

    ///////////////////////////////////////////

    public static readonly int[] flatColorArray = {
        // dungeon colors
        0x808080,
        0xADADAD,
        0xE0E0E0,

        0x0050F1,
        0x4BA0FF,
        0xB6D8FF,

        0x3B34FF,
        0x8A84FF,
        0xD0CDFF,

        0x8022E8,
        0xD172FF,
        0xEDC6FF,

        0xBB1EA5,
        0xFF6DF7,
        0xFFC4FC,

        0xDB294E,
        0xFF799E,
        0xFFC8D8,

        0xD74000,
        0xFF9047,
        0xFFD2B4,

        0xB15E00,
        0xFFAE0A,
        0xFFDE9C,

        0x737900,
        0xC4CA00,
        0xE7E994,

        0x2D8B00,
        0x7DDC13,
        0xCAF19F,

        0x008F08,
        0x41E157,
        0xB2F3BB,

        0x008460,
        0x21D5B0,
        0xA5EEDF,

        0x006DB5,
        0x25BEFF,
        0xA6E5FF,

        0x000000,   // add black as default
        0x000000,
        0x000000,
        };
    private static int[] make_threeTallColorArray()
    {
        var a = new List<int>();
        var i = 0;
        while (i < flatColorArray.Length)
        {
            a.Add(flatColorArray[i]);
            i = i + 3;
        }
        i = 1;
        while (i < flatColorArray.Length)
        {
            a.Add(flatColorArray[i]);
            i = i + 3;
        }
        i = 2;
        while (i < flatColorArray.Length)
        {
            a.Add(flatColorArray[i]);
            i = i + 3;
        }
        return a.ToArray();
    }
    public static readonly int[] threeTallColorArray = make_threeTallColorArray();

    public static Color makeColor(int rgb) => Graphics.makeColor(rgb);

    public static void canvasAdd(Canvas c, Control item, double left, double top) => Graphics.canvasAdd(c, item, left, top);

    public static readonly double TRIFORCE_SIZE = 80.0;
    public static Canvas DrawTriforceMapCore(bool[] haves, string labels)
    {
        var N = TRIFORCE_SIZE;

        var p1 = new Point(2.0 * N, 0.0);

        var p2 = new Point(N, N);
        var p3 = new Point(2.0 * N, N);
        var p4 = new Point(3.0 * N, N);

        var p5 = new Point(0.0, 2.0 * N);
        var p6 = new Point(N, 2.0 * N);
        var p7 = new Point(2.0 * N, 2.0 * N);
        var p8 = new Point(3.0 * N, 2.0 * N);
        var p9 = new Point(4.0 * N, 2.0 * N);

        Polygon tri(IEnumerable<Point> ps) => new Polygon { Stroke = Brushes.DarkSlateBlue, StrokeThickness = 2.0, Fill = Brushes.Black, Points = new List<Point>(ps) };
        var triforces = new[] {   // first quest mapping
            tri(new[] { p1, p2, p3 }),
            tri(new[] { p1, p3, p4 }),
            tri(new[] { p2, p5, p6 }),
            tri(new[] { p4, p8, p9 }),
            tri(new[] { p2, p3, p7 }),
            tri(new[] { p2, p6, p7 }),
            tri(new[] { p3, p4, p7 }),
            tri(new[] { p4, p7, p8 }),
            };
        var labelLocations = new[] {
            (1.5*N, 0.5*N),
            (2.0*N, 0.5*N),
            (0.5*N, 1.5*N),
            (3.0*N, 1.5*N),
            (1.5*N, 1.0*N),
            (1.0*N, 1.5*N),
            (2.0*N, 1.0*N),
            (2.5*N, 1.5*N),
            };
        var c = new Canvas { Width = 4.0 * N, Height = 2.0 * N, Background = Brushes.Black };
        for (int i = 0; i <= 7; i++)
        {
            canvasAdd(c, triforces[i], 0.0, 0.0);
            var (x, y) = labelLocations[i];
            var tb = new TextBox { Width = N / 2.0, Height = N / 2.0, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = N / 3.0, FontWeight = FontWeight.Bold,
                                        VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center,
                                        Text = labels.ToCharArray()[i].ToString(), Foreground = Brushes.White, Background = Brushes.Transparent };
            canvasAdd(c, tb, x, y);
            if (haves[i])
            {
                triforces[i].Fill = Brushes.Orange;
                tb.Foreground = Brushes.Black;
            }
        }
        c.Margin = new Thickness(N / 8.0);
        return c;
    }
    public static Canvas DrawTriforces1Q(bool[] haves) =>
        DrawTriforceMapCore(haves, "12345678");
    public static Canvas DrawTriforces2Q(bool[] haves)
    {
        var h = new bool[8];
        h[0] = haves[0];
        h[1] = haves[2];
        h[2] = haves[1];
        h[3] = haves[4];
        h[4] = haves[3];
        h[5] = haves[5];
        h[6] = haves[7];
        h[7] = haves[6];
        return DrawTriforceMapCore(h, "13254687");
    }

    public static async Task HiddenDungeonColorChooserPopup(CustomComboBoxes.CanvasManager cm, double tileX, double tileY, double tileW, double tileH, int originalColor, int dungeonIndex, CustomComboBoxes.GridClickDismissalWarpReturn warp)
    {
        var colors = threeTallColorArray;
        var tileCanvas = new Canvas { Width = tileW, Height = tileH };
        var gridElementsSelectablesAndIDs = colors.Select((c, i) => ((Control)new Canvas { Width = 30.0, Height = 30.0, Background = new SolidColorBrush(makeColor(c)) }, true, i)).ToArray();
        var originalStateIndex = Array.IndexOf(colors, originalColor) switch { -1 => colors.Length - 1, var i => i };   // (F#: Array.tryFindIndex)
        tileCanvas.Background = new SolidColorBrush(makeColor(colors[originalStateIndex]));
        var activationDelta = 0;
        var gnc = colors.Length / 3;
        var gnr = 3;
        var (gcw, grh) = (30, 30);
        var (gx, gy) = (-60.0, tileH + 30.0);
        void redrawTile(int i) => tileCanvas.Background = new SolidColorBrush(makeColor(colors[i]));
        // (the popup never returns a result; F# leaves its result type generic, so it's `object` here)
        CustomComboBoxes.PopupClickBehavior<object> onClick(Avalonia.Input.PointerPressedEventArgs _ea, int state)
        {
            TrackerModel.GetDungeon(dungeonIndex).Color = colors[state];
            return new CustomComboBoxes.PopupClickBehavior<object>.DismissPopupWithNoResult();
        }
        var extraDecorations = new List<(Control, double, double)>();
        var brushes = CustomComboBoxes.ModalGridSelectBrushes.Defaults();
        await CustomComboBoxes.DoModalGridSelect<int, object>(cm, tileX, tileY, tileCanvas, gridElementsSelectablesAndIDs, originalStateIndex, activationDelta, (gnc, gnr, gcw, grh),
                                (double)gcw / 2.0, (double)grh / 2.0, gx, gy, redrawTile, onClick, extraDecorations, brushes, warp, null, "HDNColorChooser", null);
    }

    public static Border MakeTriforceDecoderDiagram()
    {
        var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(5.0), Opacity = 0.8 };
        var haves = TrackerModel.GetTriforceHaves();
        TextBox makeTB(string text) => new TextBox { Width = TRIFORCE_SIZE * 3.8, IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 16.0,
                                        VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center,
                                        Text = text, Foreground = Brushes.Orange, Background = Brushes.Black };
        sp.Children.Add(makeTB("Reference diagram - don't click here"));
        sp.Children.Add(makeTB("(Note: in Mixed Quest dungeons,\n7 and 8 may be swapped)"));
        sp.Children.Add(new DockPanel { Height = TRIFORCE_SIZE / 3.0 });
        sp.Children.Add(makeTB("First Quest or Shapes dungeons"));
        sp.Children.Add(DrawTriforces1Q(haves));
        sp.Children.Add(new DockPanel { Height = TRIFORCE_SIZE / 3.0 });
        sp.Children.Add(makeTB("Second Quest dungeons"));
        sp.Children.Add(DrawTriforces2Q(haves));
        var b = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(4.0), Background = Brushes.Black, Child = sp };
        return b;
    }

    // (who: null for None)
    public static void HotKeyAHiddenDungeonLabel(Control fe, TrackerModel.Dungeon dungeon, ManualResetEvent who)
    {
        if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
        {
            fe.MyKeyAdd(ea =>
            {
                if (HotKeys.OverworldHotKeyProcessor.TryGetValue(ea.Key, out var dungIdx) && dungIdx >= 0 && dungIdx <= 7)   // overworld key for e.g. dungeon 4 is key used to hotkey 4
                {
                    var ch = (char)((int)'1' + dungIdx);
                    dungeon.LabelChar = ch;
                    if (who != null)
                        who.Set();
                }
            });
        }
    }

    // (F#: `(TrackerModel.Dungeon * ManualResetEvent) option`; null for None)
    public static (TrackerModel.Dungeon, ManualResetEvent)? HDNCP = null;
    public static async Task HiddenDungeonNumberChooserPopup(CustomComboBoxes.CanvasManager cm, double tileX, double tileY, double tileW, double tileH, char originalLabelChar, int dungeonIndex, CustomComboBoxes.GridClickDismissalWarpReturn warp)
    {
        // TODO can you choose same # twice?
        var tileCanvas = new Canvas { Width = tileW, Height = tileH, Background = Brushes.Black };
        var dp = new DockPanel { Width = tileW, Height = tileH };
        canvasAdd(tileCanvas, dp, 0.0, 0.0);
        TextBox mkTxt(char ch) =>
            new TextBox { Width = 60.0, Height = 60.0, FontSize = 40.0, Foreground = Brushes.White, Background = Brushes.Black, IsHitTestVisible = false,
                        BorderThickness = new Thickness(0.0), Text = ch.ToString(), VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var theTB = mkTxt(originalLabelChar);
        dp.Children.Add(theTB);
        var gridElementsSelectablesAndIDs = "?12345678".Select(ch => ((Control)mkTxt(ch), true, ch)).ToArray();
        var i0 = "?12345678".IndexOf(originalLabelChar);
        var originalStateIndex = i0 == -1 ? 0 : i0;
        var activationDelta = 0;
        var (gnc, gnr) = (3, 3);
        var (gcw, grh) = (60, 60);
        var (gx, gy) = (-73.0, tileH + 30.0);
        void redrawTile(char ch) => theTB.Text = ch.ToString();
        CustomComboBoxes.PopupClickBehavior<object> onClick(Avalonia.Input.PointerPressedEventArgs _ea, char ch)
        {
            TrackerModel.GetDungeon(dungeonIndex).LabelChar = ch;
            return new CustomComboBoxes.PopupClickBehavior<object>.DismissPopupWithNoResult();
        }
        List<(Control, double, double)> make_extraDecorations()
        {
            var warnTB = new TextBox { IsHitTestVisible = false, BorderThickness = new Thickness(0.0), FontSize = 16.0, Margin = new Thickness(5.0),
                                        VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center,
                                        Text = "Don't mark anything other than '?'\nunless you are certain!", Foreground = Brushes.OrangeRed, Background = Brushes.Black };
            var warnBorder = new Border { BorderThickness = new Thickness(4.0), BorderBrush = Brushes.Gray, Background = Brushes.Black, Child = warnTB };
            var b = MakeTriforceDecoderDiagram();
            b.PointerPressed += (_, ea) => ea.Handled = true;  // clicking the reference diagram should not close the dialog, people will do it by accident
            return new List<(Control, double, double)> { ((Control)warnBorder, -123.0, 284.0), ((Control)b, 190.0, -120.0) };
        }
        var extraDecorations = make_extraDecorations();
        var brushes = CustomComboBoxes.ModalGridSelectBrushes.Defaults();
        var wh = new ManualResetEvent(false);
        var dungeon = TrackerModel.GetDungeon(dungeonIndex);
        HDNCP = (dungeon, wh);
        Graphics.WarpMouseCursorTo(new Point(tileX + gx + (double)gcw / 2.0 + 18.0, tileY + gy + (double)grh / 2.0 + 15.0));   // the +(18,15) makes mouse still inside the box but not overlapping the '?', easier to see
        await CustomComboBoxes.DoModalGridSelect<char, object>(cm, tileX, tileY, tileCanvas, gridElementsSelectablesAndIDs, originalStateIndex, activationDelta, (gnc, gnr, gcw, grh),
                        44.0, 34.0, gx, gy, redrawTile, onClick, extraDecorations, brushes, warp, wh, "HDNNumberChooser", null);
        HDNCP = null;
    }
    // (F#: a top-level `do` block, run here in file order; see PLAN.md "Language: C#". Like WPF's, it needs the CanvasManager to exist already.)
    private static bool init1()
    {
        // Add() permanently adds an event listener.  We want to only call Add() once, but only listen while the popup is active
        CustomComboBoxes.CanvasManager.TheOnlyCanvasManager.AfterCreatePopupCanvas += popupCanvas =>
        {
            if (HDNCP.HasValue)
            {
                var (dungeon, wh) = HDNCP.Value;
                HotKeyAHiddenDungeonLabel(popupCanvas, dungeon, wh);
            }
        };
        return true;
    }
    private static readonly bool _init1 = init1();

    public static async Task HiddenDungeonCustomizerPopup(CustomComboBoxes.CanvasManager cm, int dungeonIndex, int curColor, char curLabel, bool accelerateIntoNumberChooser, bool accelerateIntoColorChooser, Point warpReturn)
    {
        var wh = new ManualResetEvent(false);
        // setup main visual tree
        var mainDock = new DockPanel { Background = Brushes.Black };

        var text = $"Dungeon {"ABCDEFGH"[dungeonIndex]}";
        var tb = new TextBox { Foreground = Brushes.Orange, Background = Brushes.Black, FontSize = 24.0, Text = text, IsHitTestVisible = false,
                                HorizontalContentAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0), Margin = new Thickness(0.0, 0.0, 0.0, 20.0) };
        mainDock.Children.Add(tb);
        DockPanel.SetDock(tb, Dock.Top);

// TODO ideally button1 and button2 would respond to simulated mouse clicks and arrow keys
        var innerDock = new DockPanel { LastChildFill = false };
        var button1Content = new Canvas { Width = 60.0, Height = 60.0, Background = new SolidColorBrush(makeColor(curColor)) };
        var button1 = new Button { Content = button1Content, Margin = new Thickness(20.0, 0.0, 20.0, 0.0), BorderThickness = new Thickness(5.0) };
        var button2Content = new TextBox { Width = 60.0, Height = 60.0, IsHitTestVisible = false, FontSize = 40.0, Foreground = Brushes.White, Background = Brushes.Black,
                                            Text = curLabel.ToString(), HorizontalContentAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0) };
        var button2 = new Button { Content = button2Content, Margin = new Thickness(20.0, 0.0, 20.0, 0.0), BorderThickness = new Thickness(5.0), HorizontalAlignment = HorizontalAlignment.Center };
        var b1a = new TextBox { Foreground = Brushes.Orange, Background = Brushes.Black, FontSize = 12.0, Text = "Change", IsHitTestVisible = false,
                                HorizontalContentAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0), HorizontalAlignment = HorizontalAlignment.Center };
        var b1b = new TextBox { Foreground = Brushes.Orange, Background = Brushes.Black, FontSize = 12.0, Text = "Color", IsHitTestVisible = false,
                                HorizontalContentAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0), HorizontalAlignment = HorizontalAlignment.Center };
        var b1sp = new StackPanel { Orientation = Orientation.Vertical };
        b1sp.Children.Add(b1a);
        b1sp.Children.Add(button1);
        b1sp.Children.Add(b1b);
        var b2a = new TextBox { Foreground = Brushes.Orange, Background = Brushes.Black, FontSize = 12.0, Text = "Change", IsHitTestVisible = false,
                                HorizontalContentAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0), HorizontalAlignment = HorizontalAlignment.Center };
        var b2b = new TextBox { Foreground = Brushes.Orange, Background = Brushes.Black, FontSize = 12.0, Text = "Number", IsHitTestVisible = false,
                                HorizontalContentAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0), HorizontalAlignment = HorizontalAlignment.Center };
        var b2sp = new StackPanel { Orientation = Orientation.Vertical };
        b2sp.Children.Add(b2a);
        b2sp.Children.Add(button2);
        b2sp.Children.Add(b2b);

        innerDock.Children.Add(b1sp);
        DockPanel.SetDock(button1, Dock.Left);
        innerDock.Children.Add(b2sp);
        DockPanel.SetDock(button2, Dock.Right);

        mainDock.Children.Add(innerDock);

        var theBorder0 = new Border { BorderBrush = Brushes.Black, BorderThickness = new Thickness(10.0), Child = mainDock };
        var theBorder = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(4.0), Child = theBorder0 };

        var popupIsActive = false;  // second level of popup, need local copy
        // hook up the button actions
        void button1Body()
        {
            if (!popupIsActive)
            {
                popupIsActive = true;
                var pos = button1Content.TranslatePoint(new Point(), cm.AppMainCanvas).Value;
                CustomComboBoxes.StartImmediate(((Func<Task>)(async () =>
                {
                    await HiddenDungeonColorChooserPopup(cm, pos.X, pos.Y, button1Content.Width, button1Content.Height, curColor, dungeonIndex, new CustomComboBoxes.GridClickDismissalWarpReturn.WarpTo(warpReturn));
                    popupIsActive = false;
                    wh.Set();
                }))());
            }
        }
        button1.Click += (_, _) => button1Body();
        void button2Body()
        {
            if (!popupIsActive)
            {
                popupIsActive = true;
                var pos = button2Content.TranslatePoint(new Point(), cm.AppMainCanvas).Value;
                CustomComboBoxes.StartImmediate(((Func<Task>)(async () =>
                {
                    await HiddenDungeonNumberChooserPopup(cm, pos.X, pos.Y, button2Content.Width, button2Content.Height, curLabel, dungeonIndex, new CustomComboBoxes.GridClickDismissalWarpReturn.WarpTo(warpReturn));
                    popupIsActive = false;
                    wh.Set();
                }))());
            }
        }
        button2.Click += (_, _) => button2Body();

        // add main element and extra decorations
        var popupCanvas = new Canvas();
        canvasAdd(popupCanvas, theBorder, 0.0, 0.0);

        var (mainX, mainY) = (24.0, (warpReturn.Y > 400.0 ? 325.0 : 165.0));   // if this originated from the lower half, move the popups down some, so they fit better in the broadcast window

        var dungeonColorCanvas = new Canvas { Width = 30.0, Height = 30.0 };
        canvasAdd(popupCanvas, dungeonColorCanvas, (double)dungeonIndex * 30.0 - mainX, 0.0 - mainY);
        var pretty = CustomComboBoxes.GetPrettyDashes("DungeonColorThingy", Brushes.Lime, 30.0, 30.0, 3.0, 2.0, 1.0);
        dungeonColorCanvas.Children.Add(pretty);

//    let guideline = new Shapes.Line(X1=float dungeonIndex*30. - mainX + 15., Y1=36. - mainY, X2=0., Y2=0., Stroke=Brushes.Gray, StrokeThickness=3.)
//    canvasAdd(popupCanvas, guideline, 0., 0.)

        if (accelerateIntoNumberChooser)
            button2.Loaded += (_, _) => button2Body();
        else if (accelerateIntoColorChooser)
            button2.Loaded += (_, _) => button1Body();
        else
            button1.Loaded += (_, _) => Graphics.WarpMouseCursorTo(button1.TranslatePoint(new Point(button1Content.Width / 2.0, button1Content.Height / 2.0), cm.AppMainCanvas).Value);
        await CustomComboBoxes.DoModal(cm, wh, mainX, mainY, popupCanvas);
        dungeonColorCanvas.Children.Remove(pretty);
    }

    // Zelda-like minimap

    public static class ZeldaFont
    {
        public static readonly string[] A = @"
..XXX...
.XX.XX..
XX...XX.
XX...XX.
XXXXXXX.
XX...XX.
XX...XX.
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] B = @"
XXXXXX..
XX...XX.
XX...XX.
XXXXXX..
XX...XX.
XX...XX.
XXXXXX..
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] C = @"
..XXXX..
.XX..XX.
XX......
XX......
XX......
.XX..XX.
..XXXX..
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] D = @"
XXXXX...
XX..XX..
XX...XX.
XX...XX.
XX...XX.
XX..XX..
XXXXX...
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] E = @"
XXXXXXX.
XX......
XX......
XXXXXX..
XX......
XX......
XXXXXXX.
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] F = @"
XXXXXXX.
XX......
XX......
XXXXXX..
XX......
XX......
XX......
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] G = @"
..XXXXX.
.XX.....
XX......
XX..XXX.
XX...XX.
.XX..XX.
..XXXXX.
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] H = @"
XX...XX.
XX...XX.
XX...XX.
XXXXXXX.
XX...XX.
XX...XX.
XX...XX.
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[][] lettersAthruH = { A, B, C, D, E, F, G, H };
        public static readonly string[] L = @"
.XXX....
.XXX....
.XXX....
.XXX....
.XXX....
.XXX....
.XXXXXX.
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] O = @"
.XXXXX..
XX...XX.
XX...XX.
XX...XX.
XX...XX.
XX...XX.
.XXXXX..
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] R = @"
XXXXXX..
XX...XX.
XX...XX.
XX..XXX.
XXXXX...
XX.XXX..
XX..XXX.
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] V = @"
XX...XX.
XX...XX.
XX...XX.
XXX.XXX.
.XXXXX..
..XXX...
...X....
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] hyphen = @"
........
........
........
.XXXXX..
........
........
........
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] questionMark = @"
..XXX...
.X...X..
.....X..
....X...
...X....
........
...X....
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] digit0 = @"
..XXX...
.X..XX..
XX...XX.
XX...XX.
XX...XX.
.XX..X..
..XXX...
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] digit1 = @"
..XX....
.XXX....
..XX....
..XX....
..XX....
..XX....
XXXXXX..
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] digit2 = @"
.XXXXX..
XX...XX.
....XXX.
..XXXX..
.XXXX...
XXX.....
XXXXXXX.
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] digit3 = @"
.XXXXXX.
....XX..
...XX...
..XXXX..
.....XX.
XX...XX.
.XXXXX..
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] digit4 = @"
...XXX..
..XXXX..
.XX.XX..
XX..XX..
XXXXXXX.
....XX..
....XX..
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] digit5 = @"
XXXXXX..
XX......
XXXXXX..
.....XX.
.....XX.
XX...XX.
.XXXXX..
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] digit6 = @"
..XXXX..
.XX.....
XX......
XXXXXX..
XX...XX.
XX...XX.
.XXXXX..
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] digit7 = @"
XXXXXXX.
XX...XX.
....XX..
...XX...
..XX....
..XX....
..XX....
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] digit8 = @"
.XXXXX..
XX...XX.
XX...XX.
.XXXXX..
XX...XX.
XX...XX.
.XXXXX..
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] digit9 = @"
.XXXXX..
XX...XX.
XX...XX.
.XXXXXX.
.....XX.
....XX..
.XXXX...
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[][] digits = { digit0, digit1, digit2, digit3, digit4, digit5, digit6, digit7, digit8, digit9 };
        public static readonly string[] gI = @"
.XXXXX..
..XXX...
..XXX...
..XXX...
..XXX...
..XXX...
.XXXXX..
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] gJ = @"
...XXXX.
....XX..
....XX..
....XX..
XX..XX..
XX..XX..
.XXXX...
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] gK = @"
XX...XX.
XX..XX..
XX.XX...
XXXX....
XX.XX...
XX..XX..
XX...XX.
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] gM = @"
XX...XX.
XXX.XXX.
XXXXXXX.
XXXXXXX.
XX.X.XX.
XX...XX.
XX...XX.
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] gN = @"
XX...XX.
XXX..XX.
XXXX.XX.
XXXXXXX.
XX.XXXX.
XX..XXX.
XX...XX.
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] gP = @"
XXXXXX..
XX...XX.
XX...XX.
XXXXXX..
XX......
XX......
XX......
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] gQ = @"
.XXXXX..
XX...XX.
XX...XX.
XX...XX.
XX.X.XX.
XX..XX..
.XXXX.X.
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] gS = @"
.XXXXX..
XX...XX.
XX......
.XXXXX..
.....XX.
XX...XX.
.XXXXX..
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] gT = @"
XXXXXXX.
..XXX...
..XXX...
..XXX...
..XXX...
..XXX...
..XXX...
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] gU = @"
XX...XX.
XX...XX.
XX...XX.
XX...XX.
XX...XX.
XX...XX.
.XXXXX..
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] gW = @"
XX...XX.
XX...XX.
XX...XX.
XX.X.XX.
XXXXXXX.
XXX.XXX.
XX...XX.
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] gX = @"
XX...XX.
XX...XX.
.XX.XX..
..XXX...
.XX.XX..
XX...XX.
XX...XX.
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] gY = @"
XX...XX.
XX...XX.
.XX.XX..
..XXX...
..XXX...
..XXX...
..XXX...
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] gZ = @"
XXXXXXX.
.....XX.
....XX..
...XX...
..XX....
.XX.....
XXXXXXX.
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly string[] gBlank = @"
........
........
........
........
........
........
........
........".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        public static readonly Dictionary<char, string[]> extraGlyphs = new Dictionary<char, string[]> { { 'I', gI }, { 'J', gJ }, { 'K', gK }, { 'M', gM }, { 'N', gN }, { 'P', gP }, { 'Q', gQ }, { 'S', gS }, { 'T', gT }, { 'U', gU }, { 'W', gW }, { 'X', gX }, { 'Y', gY }, { 'Z', gZ } };
        public static string[] fromLetter(char letter)
        {
            if (letter >= 'A' && letter <= 'H')
                return lettersAthruH[(int)letter - (int)'A'];
            else if (letter == 'L')
                return L;
            else if (letter == 'O')
                return O;
            else if (letter == 'R')
                return R;
            else if (letter == 'V')
                return V;
            else if (letter == '-')
                return hyphen;
            else if (letter == ' ')
                return gBlank;
            else if (extraGlyphs.ContainsKey(letter))
                return extraGlyphs[letter];
            else if (letter >= '0' && letter <= '9')
                return digits[(int)letter - (int)'0'];
            else //elif letter = '?' then
                return questionMark;
        }
    }

    public static readonly System.Drawing.Color BLUE = System.Drawing.Color.FromArgb(71, 47, 228);
    public static readonly Dictionary<(char, bool), Avalonia.Media.Imaging.Bitmap> zeldaLetterCache = new Dictionary<(char, bool), Avalonia.Media.Imaging.Bitmap>();
    public static Avalonia.Media.Imaging.Bitmap MakeLetterBIInZeldaFont(char letter, bool preferBlack)
    {
        if (zeldaLetterCache.TryGetValue((letter, preferBlack), out var v))
            return v;
        else
        {
            var bmp = new System.Drawing.Bitmap(8, 8);
            for (int x = 0; x <= 7; x++)
                for (int y = 0; y <= 7; y++)
                    bmp.SetPixel(x, y, System.Drawing.Color.Transparent);
            var a = ZeldaFont.fromLetter(letter);
            for (int x = 0; x <= 7; x++)
                for (int y = 0; y <= 7; y++)
                    if (a[y][x] == 'X')
                        bmp.SetPixel(x, y, preferBlack ? System.Drawing.Color.Black : System.Drawing.Color.White);
            var bi = Graphics.BMPToBI(bmp);
            zeldaLetterCache.Add((letter, preferBlack), bi);
            return bi;
        }
    }
    public static System.Drawing.Bitmap MakeMiniMiniMapBmp()
    {
        var bmp = new System.Drawing.Bitmap(8, 8);
        for (int x = 0; x <= 7; x++)
            for (int y = 0; y <= 7; y++)
                bmp.SetPixel(x, y, System.Drawing.Color.Black);
        for (int x = 0; x <= 6; x++)
        {
            for (int y = 0; y <= 2; y++)
                bmp.SetPixel(x, y, BLUE);
            for (int y = 4; y <= 6; y++)
                bmp.SetPixel(x, y, BLUE);
        }
        return bmp;
    }
    public static System.Drawing.Bitmap MakeLoZMinimapDisplayBmp(bool[,] rooms, char letter)
    {
        var OFFWHITE = System.Drawing.Color.FromArgb(210, 210, 210);
        var YELLOW = System.Drawing.Color.FromArgb(215, 152, 42);
        var bmp = new System.Drawing.Bitmap(80, 40);
        for (int x = 0; x <= 79; x++)
            for (int y = 0; y <= 39; y++)
                bmp.SetPixel(x, y, System.Drawing.Color.Black);
        var xoff = 0;
        // (F# shadows `w`, `b` and `pattern` further down; here they're reassigned at those points instead)
        Action<int, int> w = (x, y) => bmp.SetPixel(xoff + x, y, OFFWHITE);
        var LEVEL = new[] { ZeldaFont.L, ZeldaFont.E, ZeldaFont.V, ZeldaFont.E, ZeldaFont.L };
        var BOARD = new[] { ZeldaFont.B, ZeldaFont.O, ZeldaFont.A, ZeldaFont.R, ZeldaFont.D };
        var which = TrackerModelOptions.BOARDInsteadOfLEVEL.Value ? BOARD : LEVEL;
        // L B
        for (int x = 0; x <= 7; x++)
            for (int y = 0; y <= 7; y++)
                if (which[0][y][x] == 'X') w(x, y);
        xoff = 8;
        // E O
        for (int x = 0; x <= 7; x++)
            for (int y = 0; y <= 7; y++)
                if (which[1][y][x] == 'X') w(x, y);
        xoff = 16;
        // V A
        for (int x = 0; x <= 7; x++)
            for (int y = 0; y <= 7; y++)
                if (which[2][y][x] == 'X') w(x, y);
        xoff = 24;
        // E R
        for (int x = 0; x <= 7; x++)
            for (int y = 0; y <= 7; y++)
                if (which[3][y][x] == 'X') w(x, y);
        xoff = 32;
        // L D
        for (int x = 0; x <= 7; x++)
            for (int y = 0; y <= 7; y++)
                if (which[4][y][x] == 'X') w(x, y);
        xoff = 40;
        // -
        for (int x = 0; x <= 7; x++)
            for (int y = 0; y <= 7; y++)
                if (ZeldaFont.hyphen[y][x] == 'X') w(x, y);
        xoff = 48;
        if (letter >= 'A' && letter <= 'H')
        {
            var a = ZeldaFont.lettersAthruH[(int)letter - (int)'A'];
            for (int x = 0; x <= 7; x++)
                for (int y = 0; y <= 7; y++)
                    if (a[y][x] == 'X') w(x, y);
        }
        else if (letter >= '0' && letter <= '9')
        {
            var a = ZeldaFont.digits[(int)letter - (int)'0'];
            for (int x = 0; x <= 7; x++)
                for (int y = 0; y <= 7; y++)
                    if (a[y][x] == 'X') w(x, y);
        }
        else
        {
            // ?
            for (int x = 0; x <= 7; x++)
                for (int y = 0; y <= 7; y++)
                    if (ZeldaFont.questionMark[y][x] == 'X') w(x, y);
        }
        for (int i = 0; i <= 7; i++)
        {
            for (int j = 0; j <= 7; j++)
            {
                if (rooms[i, j])
                {
                    var x = i * 8;
                    var y = j * 4 + 8;
                    for (int dx = 0; dx <= 6; dx++)
                        for (int dy = 0; dy <= 2; dy++)
                            bmp.SetPixel(x + dx, y + dy, BLUE);
                }
            }
        }
        // rupee
        xoff = 72;
        var yoff = 8;
        var pattern = @"
...XXXXX
..XXXX.X
.XXXXX.X
XXXXXX.X
XXXXX...
XXXX.XX.
X...XX..
XXX.X...".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Action<int, int> b = (x, y) => bmp.SetPixel(xoff + x, yoff + y, YELLOW);
        for (int x = 0; x <= 7; x++)
            for (int y = 0; y <= 7; y++)
                if (pattern[y][x] == 'X') b(x, y);
        // key
        yoff = 24;
        pattern = @"
....XXX.
...X..XX
...X...X
....X..X
...X.XX.
..X.....
.X.X....
X.X.....".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int x = 0; x <= 7; x++)
            for (int y = 0; y <= 7; y++)
                if (pattern[y][x] == 'X') b(x, y);
        // bomb
        yoff = 32;
        pattern = @"
.......o
..XXXX.o
.XoXXXX.
XoXXXXXX
XoXXXXXX
XXXXXXXX
.XXXXXX.
..XXXX..".Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        w = (x, y) => bmp.SetPixel(xoff + x, yoff + y, OFFWHITE);
        b = (x, y) => bmp.SetPixel(xoff + x, yoff + y, BLUE);
        for (int x = 0; x <= 7; x++)
        {
            for (int y = 0; y <= 7; y++)
            {
                if (pattern[y][x] == 'X') b(x, y);
                else if (pattern[y][x] == 'o') w(x, y);
            }
        }
        return bmp;
    }
}

// (Dungeon.DoorState's members. F# gives the union instance/static members; a C# enum can't have any, so they live here, as
// SingleKeyboardModifierExtensions does for HotKeys: `s.AsInt()`, `DoorStateExtensions.FromInt(x)`, `DoorStateExtensions.FromString(str)`.)
public static class DoorStateExtensions
{
    public static int AsInt(this Dungeon.DoorState s)
    {
        switch (s)
        {
            case Dungeon.DoorState.UNKNOWN: return 0;
            case Dungeon.DoorState.NO: return 1;
            case Dungeon.DoorState.YES: return 2;
            case Dungeon.DoorState.YELLOW: return 3;
            case Dungeon.DoorState.PURPLE: return 4;
            default: throw new Exception("impossible DoorState");
        }
    }
    public static Dungeon.DoorState FromInt(int x)
    {
        if (x == 0) return Dungeon.DoorState.UNKNOWN;
        else if (x == 1) return Dungeon.DoorState.NO;
        else if (x == 2) return Dungeon.DoorState.YES;
        else if (x == 3) return Dungeon.DoorState.YELLOW;
        else if (x == 4) return Dungeon.DoorState.PURPLE;
        else throw new Exception("bad DoorState.FromInt() value");
    }
    public static Dungeon.DoorState FromString(string str)
    {
        switch (str)
        {
            case "UNKNOWN": return Dungeon.DoorState.UNKNOWN;
            case "NO":      return Dungeon.DoorState.NO;
            case "YES":     return Dungeon.DoorState.YES;
            case "YELLOW":  return Dungeon.DoorState.YELLOW;
            case "PURPLE":  return Dungeon.DoorState.PURPLE;
            default:        throw new Exception($"Unknown DoorState string: {str}");
        }
    }
}
