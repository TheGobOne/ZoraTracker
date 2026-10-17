using System;
using System.Collections.Generic;
using System.Drawing;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Z1R_Tracker.Models.Z1R_TrackerInterop;
using Z1R_SharedInterop;
using System.Diagnostics;
using System.Reflection.Emit;
using Z1R_Sync;


namespace Z1R_Tracker.Models
{


    namespace Z1R_TrackerInterop
    {
        public enum RoomType
        {
            Unmarked,
            NonDescript,
            MaybePushBlock,
            ItemBasement,
            StaircaseToUnknown,
            Transport1,
            Transport2,
            Transport3,
            Transport4,
            Transport5,
            Transport6,
            Transport7,
            Transport8,
            Chevy,
            DoubleMoat,
            TopMoat,
            RightMoat,
            CircleMoat,
            Tee,
            LavaMoat,
            VChute,
            HChute,
            Turnstile,
            OldManHint,
            BombUpgrade,
            LifeOrMoney,
            HungryGoriyaMeatBlock,
            StartEnterFromE,
            StartEnterFromW,
            StartEnterFromN,
            StartEnterFromS,
            OffTheMap,
            Gannon,
            Zelda
        }

        public enum MonsterDetail
        {
            Unmarked,
            Gleeok,
            Bow,
            Digdogger,
            BlueBubble,
            RedBubble,
            Dodongo,
            Patra,
            BlueWizzrobe,
            BlueDarknut,
            Manhandla,
            Vire,
            Zol,
            PolsVoice,
            RedTektite,
            RedGoriya,
            Rope,
            Stalfos,
            Wallmaster,
            Gel,
            Keese,
            Likelike,
            Gibdo,
            RedLynel,
            BlueMoblin,
            Aquamentus,
            BlueLanmola,
            Moldorm,
            RupeeBoss,
            Traps,
            Other,
            Other2
        }

        public enum DoorState
        {
            Unknown = 0,
            No = 1,
            Yes = 2,
            Yellow = 3,
            Purple = 4
        }


        public enum FloorDropDetail
        {
            Unmarked,
            Triforce,
            Heart,
            OtherKeyItem,
            BombPack,
            Key,
            FiveRupee,
            Map,
            Compass
        }


            public enum DoorDirection
            {
                East,
                West,
                North,
                South
            }

            public enum DoorAction
            {
                Increment,
                Decrement
            }

            public class DoorHotKeyResponse
            {
                public DoorDirection Direction { get; set; }
                public DoorAction Action { get; set; }

                public DoorHotKeyResponse(DoorDirection direction, DoorAction action)
                {
                    Direction = direction;
                    Action = action;
                }
            }
        

        public static class RoomTypeExtensions
        {
            public static bool IsNotMarked(this RoomType roomType)
            {
                return roomType == RoomType.Unmarked;
            }
            public static RoomType Parse(string s)
            {
                return (RoomType)Enum.Parse(typeof(RoomType), s);
            }
            public static bool IsGannonOrZelda(this RoomType roomType)
            {
                return roomType == RoomType.Gannon || roomType == RoomType.Zelda;
            }

            public static bool IsOffMap(this RoomType roomType)
            {
                return roomType == RoomType.OffTheMap;
            }
            public static int? KnownTransportNumber(this RoomType roomType)
            {
                switch (roomType)
                {
                    case RoomType.Transport1: return 1;
                    case RoomType.Transport2: return 2;
                    case RoomType.Transport3: return 3;
                    case RoomType.Transport4: return 4;
                    case RoomType.Transport5: return 5;
                    case RoomType.Transport6: return 6;
                    case RoomType.Transport7: return 7;
                    case RoomType.Transport8: return 8;
                    default: return null;
                }
            }
            public static bool IsOldMan(this RoomType roomType)
            {
                return roomType == RoomType.OldManHint ||
                       roomType == RoomType.BombUpgrade ||
                       roomType == RoomType.LifeOrMoney ||
                       roomType == RoomType.HungryGoriyaMeatBlock;
            }
            public static RoomType[] All()
            {
                return Enum.GetValues(typeof(RoomType)).Cast<RoomType>().ToArray();
            }

            public static string AsHotKeyName(this RoomType roomType)
            {
                return $"RoomType_{roomType}";
            }
            public static RoomType FromHotKeyName(string hkn)
            {
                foreach (var rt in All())
                {
                    if (rt.AsHotKeyName() == hkn)
                        return rt;
                }
                return RoomType.Unmarked;
            }
            public static Bitmap UncompletedBI(this RoomType rt)
            {
                return RoomTypeGraphics.GetBmpPair(rt).Item1;
            }

            public static Bitmap CompletedBI(this RoomType rt)
            {

                return RoomTypeGraphics.GetBmpPair(rt).Item2;
            }
            public static Bitmap TinyUncompletedBI(this RoomType rt)
            {
                return RoomTypeGraphics.GetTinyBmpPair(rt).Item1;
            }

            public static Bitmap TinyCompletedBI(this RoomType rt)
            {
                return RoomTypeGraphics.GetTinyBmpPair(rt).Item2;
            }
            public static string DisplayDescription(this RoomType roomType)
            {
                switch (roomType)
                {
                    case RoomType.Unmarked: return "(None)";
                    case RoomType.NonDescript: return "Generic Room";
                    case RoomType.MaybePushBlock: return "Push Block?";
                    case RoomType.ItemBasement: return "Item Basement";
                    case RoomType.StaircaseToUnknown: return "Staircase";
                    case RoomType.Transport1:
                    case RoomType.Transport2:
                    case RoomType.Transport3:
                    case RoomType.Transport4:
                    case RoomType.Transport5:
                    case RoomType.Transport6:
                    case RoomType.Transport7:
                    case RoomType.Transport8:
                        return roomType.ToString(); // e.g., "Transport1"
                    case RoomType.Chevy: return "Chevy Room";
                    case RoomType.DoubleMoat: return "Double Moat";
                    case RoomType.TopMoat: return "Top Moat";
                    case RoomType.RightMoat: return "Right Moat";
                    case RoomType.CircleMoat: return "Circle Moat";
                    case RoomType.Tee: return "Tee Room";
                    case RoomType.LavaMoat: return "Lava Moat";
                    case RoomType.VChute: return "Vertical Chute";
                    case RoomType.HChute: return "Horizontal Chute";
                    case RoomType.Turnstile: return "Turnstile";
                    case RoomType.OldManHint: return "Old Man (Hint)";
                    case RoomType.BombUpgrade: return "Bomb Upgrade";
                    case RoomType.LifeOrMoney: return "Life or Money";
                    case RoomType.HungryGoriyaMeatBlock: return "Meat Block";
                    case RoomType.StartEnterFromE: return "Start (E)";
                    case RoomType.StartEnterFromW: return "Start (W)";
                    case RoomType.StartEnterFromN: return "Start (N)";
                    case RoomType.StartEnterFromS: return "Start (S)";
                    case RoomType.OffTheMap: return "Off the Map";
                    case RoomType.Gannon: return "Ganon";
                    case RoomType.Zelda: return "Zelda";
                    default: return roomType.ToString();
                }
            }
            public static RoomType? NextEntranceRoom(this RoomType rt)
            {
                switch (rt)
                {
                    case RoomType.StartEnterFromS: return RoomType.StartEnterFromW;
                    case RoomType.StartEnterFromW: return RoomType.StartEnterFromN;
                    case RoomType.StartEnterFromN: return RoomType.StartEnterFromE;
                    case RoomType.StartEnterFromE: return RoomType.StartEnterFromS;
                    default: return RoomType.Unmarked;
                }
            }
            public static bool IsEntranceRoom(this RoomType rt)
            {
                return rt == RoomType.StartEnterFromE ||
                       rt == RoomType.StartEnterFromW ||
                       rt == RoomType.StartEnterFromN ||
                       rt == RoomType.StartEnterFromS;
            }




        }

        public static class MonsterDetailExtensions
        {
            public static MonsterDetail[] All()
            {
                return Enum.GetValues(typeof(MonsterDetail)).Cast<MonsterDetail>().ToArray();
            }
            public static MonsterDetail Parse(string s)
            {
                return (MonsterDetail)Enum.Parse(typeof(MonsterDetail), s);
            }
            public static MonsterDetail[] DisplayOrder()
            {
                return new[]
                {
        MonsterDetail.Gleeok,
        MonsterDetail.Bow,
        MonsterDetail.Digdogger,
        MonsterDetail.Dodongo,
        MonsterDetail.Patra,
        MonsterDetail.Manhandla,
        MonsterDetail.Aquamentus,
        MonsterDetail.Moldorm,
        MonsterDetail.BlueLanmola,
        MonsterDetail.BlueWizzrobe,
        MonsterDetail.BlueDarknut,
        MonsterDetail.RedLynel,
        MonsterDetail.PolsVoice,
        MonsterDetail.RedGoriya,
        MonsterDetail.Gibdo,
        MonsterDetail.Rope,
        MonsterDetail.Vire,
        MonsterDetail.Keese,
        MonsterDetail.Zol,
        MonsterDetail.Gel,
        MonsterDetail.Stalfos,
        MonsterDetail.Wallmaster,
        MonsterDetail.Likelike,
        MonsterDetail.BlueMoblin,
        MonsterDetail.Other,
        MonsterDetail.Other2,
        MonsterDetail.Traps,
        MonsterDetail.RedTektite,
        MonsterDetail.BlueBubble,
        MonsterDetail.RedBubble,
        MonsterDetail.RupeeBoss,
        MonsterDetail.Unmarked
    };
            }


            public static string AsHotKeyName(this MonsterDetail detail)
            {
                return $"MonsterDetail_{detail}";
            }
            public static MonsterDetail FromHotKeyName(string hkn)
            {
                foreach (var md in All())
                {
                    if (md.AsHotKeyName() == hkn)
                        return md;
                }
                return MonsterDetail.Unmarked;
            }
            public static bool IsNotMarked(this MonsterDetail detail)
            {
                return detail == MonsterDetail.Unmarked;
            }
            public static string DisplayDescription(this MonsterDetail detail)
            {
                switch (detail)
                {
                    case MonsterDetail.Unmarked: return "(None)";
                    case MonsterDetail.Gleeok: return "Gleeok";
                    case MonsterDetail.Bow: return "Gohma";
                    case MonsterDetail.Digdogger: return "Digdogger";
                    case MonsterDetail.BlueBubble: return "Blue Bubble";
                    case MonsterDetail.RedBubble: return "Red Bubble";
                    case MonsterDetail.Dodongo: return "Dodongo";
                    case MonsterDetail.Patra: return "Patra";
                    case MonsterDetail.BlueWizzrobe: return "Wizzrobe";
                    case MonsterDetail.BlueDarknut: return "Darknut";
                    case MonsterDetail.Manhandla: return "Manhandla";
                    case MonsterDetail.Vire: return "Vire";
                    case MonsterDetail.Zol: return "Zol";
                    case MonsterDetail.PolsVoice: return "Pols Voice";
                    case MonsterDetail.RedTektite: return "Tektite";
                    case MonsterDetail.RedGoriya: return "Goriya";
                    case MonsterDetail.Rope: return "Rope";
                    case MonsterDetail.Stalfos: return "Stalfos";
                    case MonsterDetail.Wallmaster: return "Wallmaster";
                    case MonsterDetail.Gel: return "Gel";
                    case MonsterDetail.Keese: return "Keese";
                    case MonsterDetail.Likelike: return "Likelike";
                    case MonsterDetail.Gibdo: return "Gibdo";
                    case MonsterDetail.RedLynel: return "Lynel";
                    case MonsterDetail.BlueMoblin: return "Moblin";
                    case MonsterDetail.Aquamentus: return "Aquamentus";
                    case MonsterDetail.BlueLanmola: return "Lanmola";
                    case MonsterDetail.Moldorm: return "Moldorm";
                    case MonsterDetail.RupeeBoss: return "Rupee Boss";
                    case MonsterDetail.Traps: return "Traps";
                    case MonsterDetail.Other: return "Other";
                    case MonsterDetail.Other2: return "Other2";
                    default: return "(Unknown)";
                }
            }
            public static Bitmap Bmp(this MonsterDetail detail)
            {
                return MonsterGraphics.GetBitmap(detail);
            }




        }

        public static class FloorDropDetailExtensions
        {
            public static FloorDropDetail[] All()
            {
                return Enum.GetValues(typeof(FloorDropDetail)).Cast<FloorDropDetail>().ToArray();
            }
            public static FloorDropDetail Parse(string s)
            {
                return (FloorDropDetail)Enum.Parse(typeof(FloorDropDetail), s);
            }
            public static FloorDropDetail[] DisplayOrder()
            {
                return new[]
                {
            FloorDropDetail.Triforce,
            FloorDropDetail.Heart,
            FloorDropDetail.OtherKeyItem,
            FloorDropDetail.BombPack,
            FloorDropDetail.Key,
            FloorDropDetail.FiveRupee,
            FloorDropDetail.Map,
            FloorDropDetail.Compass,
            FloorDropDetail.Unmarked
        };
            }
            public static string AsHotKeyName(this FloorDropDetail drop)
            {
                return $"FloorDropDetail_{drop}";
            }
            public static FloorDropDetail FromHotKeyName(string hkn)
            {
                foreach (var fd in All())
                {
                    if (fd.AsHotKeyName() == hkn)
                        return fd;
                }
                return FloorDropDetail.Unmarked;
            }
            public static string DisplayDescription(this FloorDropDetail detail)
            {
                switch (detail)
                {
                    case FloorDropDetail.Unmarked: return "(None)";
                    case FloorDropDetail.Triforce: return "Triforce";
                    case FloorDropDetail.Heart: return "Heart Container";
                    case FloorDropDetail.OtherKeyItem: return "Other Key Item";
                    case FloorDropDetail.BombPack: return "Bomb Pack";
                    case FloorDropDetail.Key: return "Key (single use)";
                    case FloorDropDetail.FiveRupee: return "Five-Rupee";
                    case FloorDropDetail.Map: return "Map";
                    case FloorDropDetail.Compass: return "Compass";
                    default: return "(Unknown)";
                }
            }
            public static bool IsNotMarked(this FloorDropDetail detail)
            {
                return detail == FloorDropDetail.Unmarked;
            }
            public static Bitmap Bmp(this FloorDropDetail detail)
            {
                return FloorDropGraphics.GetBitmap(detail);
            }


        }
    }

    public class CDungeonRoomState
    {
        public event EventHandler _changed;
        public event EventHandler Changed
        {
            add
            {
                _changed += value;
            }
            remove
            {
                _changed -= value;
            }
        }

        private bool _isComplete;
        private RoomType _roomType;
        private MonsterDetail _monsterDetail;
        private FloorDropDetail _floorDropDetail;
        private bool _floorDropAppearsBright;

        public CDungeonRoomState()
        {
            _isComplete = true;
            _roomType = RoomType.Unmarked;
            _monsterDetail = MonsterDetail.Unmarked;
            _floorDropDetail = FloorDropDetail.Unmarked;
            _floorDropAppearsBright = true;
        }

        public void FireChangedManually()
        {
            _changed?.Invoke(this, EventArgs.Empty);
        }
        public void Clear()
        {
            this.RoomType = RoomType.Unmarked;
            this.MonsterDetail = MonsterDetail.Unmarked;
            this.FloorDropDetail = FloorDropDetail.Unmarked;
            this.FloorDropAppearsBright = false;
            this.IsComplete = false;

            FireChangedManually(); // notify UI
        }



        public bool IsEmpty => RoomType.IsNotMarked() || RoomType.IsOffMap();

        public bool IsGannonOrZelda => RoomType.IsGannonOrZelda();

        public int Level { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public Guid DebugId { get; set; } = Guid.NewGuid();


        public bool IsComplete
        {
            get => _isComplete;
            set
            {
                if (_isComplete == value) return;
                _isComplete = value;
                // Only notify coop sync if not in preview mode
                if (!IsPreview)
                {
                    // Invoke the OnRoomChanged event with this room’s coordinates
                    RoomSyncBridge.OnRoomChanged?.Invoke(Level, X, Y, this);
                }
            }
        }

        public RoomType RoomType
        {
            get => _roomType;
            set
            {
                if (_roomType == value) return;
                _roomType = value;
                // Only notify coop sync if not in preview mode
                if (!IsPreview)
                {
                    // Invoke the OnRoomChanged event with this room’s coordinates
                    RoomSyncBridge.OnRoomChanged?.Invoke(Level, X, Y, this);
                }
            }
        }

        public MonsterDetail MonsterDetail
        {
            get => _monsterDetail;
            set
            {
                if (_monsterDetail == value) return;
                _monsterDetail = value;
                // Only notify coop sync if not in preview mode
                if (!IsPreview)
                {
                    // Invoke the OnRoomChanged event with this room’s coordinates
                    RoomSyncBridge.OnRoomChanged?.Invoke(Level, X, Y, this);
                }
            }
        }

        public FloorDropDetail FloorDropDetail
        {
            get => _floorDropDetail;
            set
            {
                if (_floorDropDetail == value) return;
                _floorDropDetail = value;
                // Only notify coop sync if not in preview mode
                if (!IsPreview)
                {
                    // Invoke the OnRoomChanged event with this room’s coordinates
                    RoomSyncBridge.OnRoomChanged?.Invoke(Level, X, Y, this);
                }
            }
        }

        public bool FloorDropAppearsBright
        {
            get => _floorDropAppearsBright;
            set
            {
                if (_floorDropAppearsBright == value) return;
                _floorDropAppearsBright = value;
                // Only notify coop sync if not in preview mode
                if (!IsPreview)
                {
                    // Invoke the OnRoomChanged event with this room’s coordinates
                    RoomSyncBridge.OnRoomChanged?.Invoke(Level, X, Y, this);
                }
            }
        }

        public bool IsPreview { get; set; } = false;

        public CDungeonRoomState Clone()
        {
            return new CDungeonRoomState
            {
                IsComplete = this.IsComplete,
                RoomType = this.RoomType,
                MonsterDetail = this.MonsterDetail,
                FloorDropDetail = this.FloorDropDetail,
                FloorDropAppearsBright = this.FloorDropAppearsBright,
                X = this.X,
                Y = this.Y,
                Level = this.Level,
                //DebugId = this.DebugId // clone the DebugId for tracking
            };
        }
        public void CopyFrom(CDungeonRoomState other)
        {
            this.IsComplete = other.IsComplete;
            this.RoomType = other.RoomType;
            this.MonsterDetail = other.MonsterDetail;
            this.FloorDropDetail = other.FloorDropDetail;
            this.FloorDropAppearsBright = other.FloorDropAppearsBright;
            this.X = other.X;
            this.Y = other.Y;
            this.Level = other.Level;
            //this.DebugId = other.DebugId; // copy DebugId for tracking
        }


        public override bool Equals(object obj)
        {
            var other = obj as CDungeonRoomState;
            if (other == null) return false;

            return this.IsComplete == other.IsComplete &&
                   this.RoomType == other.RoomType &&
                   this.MonsterDetail == other.MonsterDetail &&
                   this.FloorDropDetail == other.FloorDropDetail &&
                   this.FloorDropAppearsBright == other.FloorDropAppearsBright &&
                   this.X == other.X &&
                   this.Y == other.Y &&
                   this.Level == other.Level;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + IsComplete.GetHashCode();
                hash = hash * 23 + RoomType.GetHashCode();
                hash = hash * 23 + MonsterDetail.GetHashCode();
                hash = hash * 23 + FloorDropDetail.GetHashCode();
                hash = hash * 23 + FloorDropAppearsBright.GetHashCode();
                hash = hash * 23 + X.GetHashCode();
                hash = hash * 23 + Y.GetHashCode();
                hash = hash * 23 + Level.GetHashCode();
                return hash;
            }
        }


        public void ToggleFloorDropBrightness()
        {
            FloorDropAppearsBright = !FloorDropAppearsBright;
        }

        private void OnChanged()
        {
            System.Diagnostics.Debug.WriteLine($"[CDungeonRoomState] OnChanged() firing for ID {this.DebugId}");

            if (Z1R_Tracker.Models.RoomSyncBridge.OnRoomChanged != null)
            {
                if (SyncManager.IsSuppressingRoomChanges())
                {
                    System.Diagnostics.Debug.WriteLine($"[CDungeonRoomState] Suppressing RoomChanged for L{this.Level} ({this.X},{this.Y}) - ID {this.DebugId}");
                    return;
                }
                System.Diagnostics.Debug.WriteLine($"[CDungeonRoomState] Invoking RoomSyncBridge.OnRoomChanged for L{this.Level} ({this.X},{this.Y}) - ID {this.DebugId}");
                Z1R_Tracker.Models.RoomSyncBridge.OnRoomChanged.Invoke(this.Level, this.X, this.Y, this);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[CDungeonRoomState] RoomSyncBridge.OnRoomChanged is null for L{this.Level} ({this.X},{this.Y}) - ID {this.DebugId}");
            }
        }




    }
    public static class CDungeonModelStore
    {
        public static readonly CDungeonRoomState[][,] Dungeons =
            Enumerable.Range(0, 9)
                      .Select(level => CreateEmptyGrid(level))
                      .ToArray();


        public static CDungeonRoomState[,] CreateEmptyGrid(int level)
        {
            var grid = new CDungeonRoomState[8, 8];
            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    var room = new CDungeonRoomState();
                    room.Level = level;
                    room.X = x;
                    room.Y = y;
                    grid[x, y] = room;
                }
            }
            return grid;
        }

        public static DoorState GetDoorState(int level, int x, int y, DoorDirection dir)
        {
            if (level < 0 || level >= 9)
                throw new ArgumentOutOfRangeException(nameof(level));

            switch (dir)
            {
                case DoorDirection.North:
                    if (y <= 0) return DoorState.No;
                    return HorizontalDoors[level][x, y - 1];
                case DoorDirection.South:
                    if (y >= 7) return DoorState.No;
                    return HorizontalDoors[level][x, y];
                case DoorDirection.West:
                    if (x <= 0) return DoorState.No;
                    return VerticalDoors[level][x - 1, y];
                case DoorDirection.East:
                    if (x >= 7) return DoorState.No;
                    return VerticalDoors[level][x, y];
                default:
                    throw new ArgumentException("Invalid direction");
            }
        }

        public static void SetDoorState(int level, int x, int y, DoorDirection dir, DoorState state)
        {
            if (level < 0 || level >= 9)
                throw new ArgumentOutOfRangeException(nameof(level));

            switch (dir)
            {
                case DoorDirection.North:
                    if (y <= 0) return;
                    HorizontalDoors[level][x, y - 1] = state;
                    break;
                case DoorDirection.South:
                    if (y >= 7) return;
                    HorizontalDoors[level][x, y] = state;
                    break;
                case DoorDirection.West:
                    if (x <= 0) return;
                    VerticalDoors[level][x - 1, y] = state;
                    break;
                case DoorDirection.East:
                    if (x >= 7) return;
                    VerticalDoors[level][x, y] = state;
                    break;
                default:
                    throw new ArgumentException("Invalid direction");
            }

            // OPTIONAL: you could trigger something here like UI or sync hooks
        }

        public static readonly DoorState[][,] HorizontalDoors =
            Enumerable.Range(0, 9)
              .Select(_ => CreateHorizontalDoorGrid())
              .ToArray();

        public static readonly DoorState[][,] VerticalDoors =
            Enumerable.Range(0, 9)
                      .Select(_ => CreateVerticalDoorGrid())
                      .ToArray();

        private static DoorState[,] CreateHorizontalDoorGrid()
        {
            var grid = new DoorState[8, 7];  // 8 rows x 7 horizontal edges
            for (int x = 0; x < 8; x++)
                for (int y = 0; y < 7; y++)
                    grid[x, y] = DoorState.Unknown;
            return grid;
        }

        private static DoorState[,] CreateVerticalDoorGrid()
        {
            var grid = new DoorState[7, 8];  // 7 vertical edges x 8 columns
            for (int x = 0; x < 7; x++)
                for (int y = 0; y < 8; y++)
                    grid[x, y] = DoorState.Unknown;
            return grid;
        }

    }
    public static class MonsterGraphics
    {
        private static readonly Dictionary<MonsterDetail, Bitmap> _bitmapMap;

        static MonsterGraphics()
        {
            _bitmapMap = LoadBitmaps();
        }

        private static Dictionary<MonsterDetail, Bitmap> LoadBitmaps()
        {
            var result = new Dictionary<MonsterDetail, Bitmap>();

            var resourceName = "zelda_bosses16x16.png";
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Z1R_WPF");

            if (assembly == null)
                throw new InvalidOperationException("Z1R_WPF assembly not found.");

            using (Stream imageStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (imageStream == null)
                    throw new InvalidOperationException($"Embedded resource '{resourceName}' not found. Ensure it's marked as Embedded Resource in Z1R_WPF.");

                var source = new Bitmap(imageStream);
                int spriteWidth = 16;
                int spriteHeight = 16;

                var spriteOrder = new Dictionary<MonsterDetail, int>
        {
            { MonsterDetail.Digdogger,     0 },
            { MonsterDetail.Gleeok,        1 },
            { MonsterDetail.Bow,         2 },
            { MonsterDetail.Manhandla,     3 },
            { MonsterDetail.BlueWizzrobe,  4 },
            { MonsterDetail.Patra,         5 },
            { MonsterDetail.Dodongo,       6 },
            { MonsterDetail.RedBubble,     7 },
            { MonsterDetail.BlueBubble,    8 },
            { MonsterDetail.BlueDarknut,   9 },
            { MonsterDetail.Other,        10 },
            // skip Old Man at index 11
            { MonsterDetail.Vire,         12 },
            { MonsterDetail.Zol,          13 },
            { MonsterDetail.PolsVoice,    14 },
            { MonsterDetail.RedTektite,   15 },
            { MonsterDetail.RedGoriya,    16 },
            { MonsterDetail.Rope,         17 },
            { MonsterDetail.Stalfos,      18 },
            { MonsterDetail.Wallmaster,   19 },
            { MonsterDetail.Gel,          20 },
            { MonsterDetail.Keese,        21 },
            { MonsterDetail.Likelike,     22 },
            { MonsterDetail.Gibdo,        23 },
            { MonsterDetail.RedLynel,     24 },
            { MonsterDetail.BlueMoblin,   25 },
            { MonsterDetail.Aquamentus,   26 },
            { MonsterDetail.BlueLanmola,  27 },
            { MonsterDetail.Moldorm,      28 },
            { MonsterDetail.RupeeBoss,    29 },
            { MonsterDetail.Traps,        30 },
            { MonsterDetail.Other2,       31 }
        };

                foreach (var kvp in spriteOrder)
                {
                    var detail = kvp.Key;
                    int index = kvp.Value;

                    var tile = new Bitmap(18, 18); // 1px border
                    using (Graphics g = Graphics.FromImage(tile))
                    {
                        g.Clear(Color.Black);

                        for (int px = 0; px < spriteWidth; px++)
                        {
                            for (int py = 0; py < spriteHeight; py++)
                            {
                                var color = source.GetPixel(px + index * spriteWidth, py);
                                if (color.ToArgb() != Color.Black.ToArgb())
                                {
                                    tile.SetPixel(px + 1, py + 1, color); // offset by 1px for border
                                }
                            }
                        }
                    }

                    result[detail] = tile;
                }
            }

            return result;
        }



        public static void DumpLoadedSpritesToDisk(string folder)
        {
            Directory.CreateDirectory(folder);
            foreach (var kvp in _bitmapMap)
            {
                var detail = kvp.Key;
                var bmp = kvp.Value;
                string filename = Path.Combine(folder, $"{(int)detail:00}_{detail}.png");
                bmp.Save(filename, System.Drawing.Imaging.ImageFormat.Png);
            }
        }






        public static Bitmap GetBitmap(MonsterDetail detail)
        {
            if (_bitmapMap.TryGetValue(detail, out var bmp))
                return bmp;
            return null;
        }
    }





    public static class FloorDropGraphics
    {
        private static readonly Dictionary<FloorDropDetail, Bitmap> _bitmapMap;

        static FloorDropGraphics()
        {
            _bitmapMap = LoadBitmaps();
        }

        private static Dictionary<FloorDropDetail, Bitmap> LoadBitmaps()
        {
            var result = new Dictionary<FloorDropDetail, Bitmap>();

            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Z1R_WPF");

            if (assembly == null)
                throw new InvalidOperationException("Z1R_WPF assembly not found.");

            using (Stream imageStream = assembly.GetManifestResourceStream("zelda_items16x16.png"))
            {
                if (imageStream == null)
                    throw new InvalidOperationException("Embedded resource 'zelda_items16x16.png' not found. Ensure it is marked as Embedded Resource under Z1R_WPF.Resources.icons.");

                var source = new Bitmap(imageStream);
                int spriteWidth = 16;
                int spriteHeight = 16;

                // This order is based on your actual sprite sheet layout (visual left-to-right order)
                var spriteOrder = new Dictionary<FloorDropDetail, int>
        {
            { FloorDropDetail.Triforce,     0 },
            { FloorDropDetail.Heart,        1 },
            { FloorDropDetail.BombPack,     2 },
            { FloorDropDetail.Key,          3 },
            { FloorDropDetail.FiveRupee,    4 },
            { FloorDropDetail.Map,          5 },
            { FloorDropDetail.Compass,      6 },
            { FloorDropDetail.OtherKeyItem, 7 }
        };

                foreach (var kvp in spriteOrder)
                {
                    var detail = kvp.Key;
                    int index = kvp.Value;

                    var tile = new Bitmap(18, 18); // with 1px border
                    using (Graphics g = Graphics.FromImage(tile))
                    {
                        g.Clear(Color.Black);

                        for (int px = 0; px < spriteWidth; px++)
                        {
                            for (int py = 0; py < spriteHeight; py++)
                            {
                                var color = source.GetPixel(px + index * spriteWidth, py);
                                if (color.ToArgb() != Color.Black.ToArgb())
                                {
                                    tile.SetPixel(px + 1, py + 1, color); // 1px padding
                                }
                            }
                        }
                    }

                    result[detail] = tile;
                }
            }

            return result;
        }





        public static Bitmap GetBitmap(FloorDropDetail detail)
        {
            if (_bitmapMap.TryGetValue(detail, out var bmp))
                return bmp;
            return null;
        }
    }


    public static class RoomTypeGraphics
    {
        private static readonly Tuple<Bitmap, Bitmap>[] dungeonRoomBmpPairs;
        private static readonly Tuple<Bitmap, Bitmap>[] dungeonRoomTinyBmpPairs;

        static RoomTypeGraphics()
        {
            dungeonRoomBmpPairs = LoadUpscaledRoomBitmapPairs(GetResourceStreamFromWPF("new_icons13x9.png"));
            dungeonRoomTinyBmpPairs = LoadTinyRoomBitmapPairs(GetResourceStreamFromWPF("new_icons13x9.png"));
        }

        public static Stream GetResourceStreamFromWPF(string resourceFileName)
        {
            var wpfAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Z1R_WPF");

            if (wpfAssembly == null)
                throw new InvalidOperationException("Z1R_WPF assembly not found in current AppDomain.");

            var resourceName = wpfAssembly.GetManifestResourceNames()
                .FirstOrDefault(r => r.EndsWith(resourceFileName, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
                throw new InvalidOperationException($"Resource not found: {resourceFileName}");

            return wpfAssembly.GetManifestResourceStream(resourceName);
        }
        public static Tuple<Bitmap, Bitmap>[] LoadUpscaledRoomBitmapPairs(Stream stream)
        {
            Bitmap src = new Bitmap(stream);
            int count = src.Width / 13;
            var pairs = new Tuple<Bitmap, Bitmap>[count];

            for (int i = 0; i < count; i++)
            {
                Bitmap uncompleted = new Bitmap(13 * 3, 9 * 3);
                Bitmap completed = new Bitmap(13 * 3, 9 * 3);

                for (int px = 0; px < 13 * 3; px++)
                {
                    for (int py = 0; py < 9 * 3; py++)
                    {
                        Color uncolor = src.GetPixel(px / 3 + i * 13, py / 3);
                        Color comcolor = src.GetPixel(px / 3 + i * 13, py / 3 + 9);

                        uncompleted.SetPixel(px, py, uncolor);
                        completed.SetPixel(px, py, comcolor);
                    }
                }

                pairs[i] = Tuple.Create(uncompleted, completed);
            }

            return pairs;
        }

        private static Tuple<Bitmap, Bitmap>[] LoadRoomBitmapPairs(Stream stream)
        {
            Bitmap fullImage = new Bitmap(stream);
            int tileWidth = 13;
            int tileHeight = 9;
            int numTiles = fullImage.Width / tileWidth;

            var result = new Tuple<Bitmap, Bitmap>[numTiles];

            for (int i = 0; i < numTiles; i++)
            {
                Bitmap uncompleted = new Bitmap(tileWidth, tileHeight);
                Bitmap completed = new Bitmap(tileWidth, tileHeight);

                for (int x = 0; x < tileWidth; x++)
                {
                    for (int y = 0; y < tileHeight; y++)
                    {
                        uncompleted.SetPixel(x, y, fullImage.GetPixel(i * tileWidth + x, y));
                        completed.SetPixel(x, y, fullImage.GetPixel(i * tileWidth + x, tileHeight + y));
                    }
                }

                result[i] = Tuple.Create(uncompleted, completed);
            }

            return result;
        }


        private static Tuple<Bitmap, Bitmap>[] LoadTinyRoomBitmapPairs(Stream stream)
        {
            Bitmap fullImage = new Bitmap(stream);
            int iconWidth = 13;
            int iconHeight = 9;
            int pairCount = fullImage.Width / iconWidth;

            var result = new Tuple<Bitmap, Bitmap>[pairCount];

            for (int i = 0; i < pairCount; i++)
            {
                Bitmap uncompleted = new Bitmap(iconWidth, iconHeight);
                Bitmap completed = new Bitmap(iconWidth, iconHeight);

                for (int x = 0; x < iconWidth; x++)
                {
                    for (int y = 0; y < iconHeight; y++)
                    {
                        uncompleted.SetPixel(x, y, fullImage.GetPixel(x + i * iconWidth, y));
                        completed.SetPixel(x, y, fullImage.GetPixel(x + i * iconWidth, y + iconHeight));
                    }
                }

                result[i] = Tuple.Create(uncompleted, completed);
            }

            return result;
        }


        public static Tuple<Bitmap, Bitmap> GetBmpPair(RoomType rt)
        {
            switch (rt)
            {
                case RoomType.Unmarked: return Tuple.Create(dungeonRoomBmpPairs[0].Item1, dungeonRoomBmpPairs[0].Item1);
                case RoomType.NonDescript: return dungeonRoomBmpPairs[1];
                case RoomType.MaybePushBlock: return dungeonRoomBmpPairs[10];
                case RoomType.ItemBasement: return dungeonRoomBmpPairs[11];
                case RoomType.StaircaseToUnknown: return dungeonRoomBmpPairs[25];
                case RoomType.Transport1: return dungeonRoomBmpPairs[17];
                case RoomType.Transport2: return dungeonRoomBmpPairs[18];
                case RoomType.Transport3: return dungeonRoomBmpPairs[19];
                case RoomType.Transport4: return dungeonRoomBmpPairs[20];
                case RoomType.Transport5: return dungeonRoomBmpPairs[21];
                case RoomType.Transport6: return dungeonRoomBmpPairs[22];
                case RoomType.Transport7: return dungeonRoomBmpPairs[23];
                case RoomType.Transport8: return dungeonRoomBmpPairs[24];
                case RoomType.Chevy: return dungeonRoomBmpPairs[3];
                case RoomType.DoubleMoat: return dungeonRoomBmpPairs[2];
                case RoomType.TopMoat: return dungeonRoomBmpPairs[5];
                case RoomType.RightMoat: return dungeonRoomBmpPairs[4];
                case RoomType.CircleMoat: return dungeonRoomBmpPairs[6];
                case RoomType.Tee: return dungeonRoomBmpPairs[9];
                case RoomType.LavaMoat: return dungeonRoomBmpPairs[33];
                case RoomType.VChute: return dungeonRoomBmpPairs[7];
                case RoomType.HChute: return dungeonRoomBmpPairs[8];
                case RoomType.Turnstile: return dungeonRoomBmpPairs[16];
                case RoomType.BombUpgrade: return dungeonRoomBmpPairs[15];
                case RoomType.LifeOrMoney: return dungeonRoomBmpPairs[14];
                case RoomType.HungryGoriyaMeatBlock: return dungeonRoomBmpPairs[13];
                case RoomType.StartEnterFromE: return dungeonRoomBmpPairs[29];
                case RoomType.StartEnterFromW: return dungeonRoomBmpPairs[26];
                case RoomType.StartEnterFromN: return dungeonRoomBmpPairs[27];
                case RoomType.StartEnterFromS: return dungeonRoomBmpPairs[28];
                case RoomType.OffTheMap: return dungeonRoomBmpPairs[30];
                case RoomType.Gannon: return Tuple.Create(dungeonRoomBmpPairs[31].Item2, dungeonRoomBmpPairs[31].Item2);
                case RoomType.Zelda: return Tuple.Create(dungeonRoomBmpPairs[32].Item2, dungeonRoomBmpPairs[32].Item2);
                case RoomType.OldManHint:
                    return TrackerOptionsBridge.BookForHelpfulHints()
                        ? dungeonRoomBmpPairs[12]
                        : Tuple.Create(dungeonRoomBmpPairs[12].Item2, dungeonRoomBmpPairs[12].Item2);
                default: return dungeonRoomBmpPairs[0];
            }
        }

        public static Tuple<Bitmap, Bitmap> GetTinyBmpPair(RoomType rt)
        {
            switch (rt)
            {
                case RoomType.Unmarked: return Tuple.Create(dungeonRoomTinyBmpPairs[0].Item1, dungeonRoomTinyBmpPairs[0].Item1);
                case RoomType.NonDescript: return dungeonRoomTinyBmpPairs[1];
                case RoomType.MaybePushBlock: return dungeonRoomTinyBmpPairs[10];
                case RoomType.ItemBasement: return dungeonRoomTinyBmpPairs[11];
                case RoomType.StaircaseToUnknown: return dungeonRoomTinyBmpPairs[25];
                case RoomType.Transport1: return dungeonRoomTinyBmpPairs[17];
                case RoomType.Transport2: return dungeonRoomTinyBmpPairs[18];
                case RoomType.Transport3: return dungeonRoomTinyBmpPairs[19];
                case RoomType.Transport4: return dungeonRoomTinyBmpPairs[20];
                case RoomType.Transport5: return dungeonRoomTinyBmpPairs[21];
                case RoomType.Transport6: return dungeonRoomTinyBmpPairs[22];
                case RoomType.Transport7: return dungeonRoomTinyBmpPairs[23];
                case RoomType.Transport8: return dungeonRoomTinyBmpPairs[24];
                case RoomType.Chevy: return dungeonRoomTinyBmpPairs[3];
                case RoomType.DoubleMoat: return dungeonRoomTinyBmpPairs[2];
                case RoomType.TopMoat: return dungeonRoomTinyBmpPairs[5];
                case RoomType.RightMoat: return dungeonRoomTinyBmpPairs[4];
                case RoomType.CircleMoat: return dungeonRoomTinyBmpPairs[6];
                case RoomType.Tee: return dungeonRoomTinyBmpPairs[9];
                case RoomType.LavaMoat: return dungeonRoomTinyBmpPairs[33];
                case RoomType.VChute: return dungeonRoomTinyBmpPairs[7];
                case RoomType.HChute: return dungeonRoomTinyBmpPairs[8];
                case RoomType.Turnstile: return dungeonRoomTinyBmpPairs[16];
                case RoomType.BombUpgrade: return dungeonRoomTinyBmpPairs[15];
                case RoomType.LifeOrMoney: return dungeonRoomTinyBmpPairs[14];
                case RoomType.HungryGoriyaMeatBlock: return dungeonRoomTinyBmpPairs[13];
                case RoomType.StartEnterFromE: return dungeonRoomTinyBmpPairs[29];
                case RoomType.StartEnterFromW: return dungeonRoomTinyBmpPairs[26];
                case RoomType.StartEnterFromN: return dungeonRoomTinyBmpPairs[27];
                case RoomType.StartEnterFromS: return dungeonRoomTinyBmpPairs[28];
                case RoomType.OffTheMap: return dungeonRoomTinyBmpPairs[30];
                case RoomType.Gannon: return Tuple.Create(dungeonRoomTinyBmpPairs[31].Item2, dungeonRoomTinyBmpPairs[31].Item2);
                case RoomType.Zelda: return Tuple.Create(dungeonRoomTinyBmpPairs[32].Item2, dungeonRoomTinyBmpPairs[32].Item2);
                case RoomType.OldManHint:
                    return TrackerOptionsBridge.BookForHelpfulHints()
                        ? dungeonRoomTinyBmpPairs[12]
                        : Tuple.Create(dungeonRoomTinyBmpPairs[12].Item2, dungeonRoomTinyBmpPairs[12].Item2);
                default: return dungeonRoomTinyBmpPairs[0];
            }
        }
    }

    public static class RoomSyncBridge
    {
        public static Action<int, int, int, CDungeonRoomState> OnRoomChanged;

        public static void WireRoomChangeEvents()
        {
            for (int level = 0; level < 9; level++)
            {
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        var room = CDungeonModelStore.Dungeons[level][x, y];

                        // ⚠️ Capture loop variables properly
                        int capturedLevel = level;
                        int capturedX = x;
                        int capturedY = y;

                        room.Changed += (s, e) =>
                        {
                            Debug.WriteLine($"[DEBUG] Event fired for room ({capturedLevel}, {capturedX}, {capturedY}) - ID: {room.DebugId}");
                            if (RoomSyncBridge.OnRoomChanged != null)
                            {
                                RoomSyncBridge.OnRoomChanged.Invoke(capturedLevel, capturedX, capturedY, room);
                            }
                        };
                    }
                }
            }
        }

        public static void ApplyRoomChangeFromSync(
            int level, int x, int y,
            bool isComplete,
            string roomTypeStr,
            string monsterDetailStr,
            string floorDropDetailStr,
            bool floorDropAppearsBright)
                {
                    if (level < 1 || level > 9 || x < 0 || x > 7 || y < 0 || y > 7)
                        return;

                    // Use the F# interop callback instead of direct mutation
                    if (RoomInteropBridge.applyRoomStateFromSync != null)
                    {
                        RoomInteropBridge.applyRoomStateFromSync(
                            level, x, y,
                            isComplete,
                            roomTypeStr,
                            monsterDetailStr,
                            floorDropDetailStr,
                            floorDropAppearsBright
                        );
                    }
                }





    }

}
