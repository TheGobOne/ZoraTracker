// Ported from Z1R_WPF/DungeonSaveAndLoad.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// Save files must stay interchangeable with the WPF tracker: the JSON text written here is built exactly as WPF builds it
// (same property names, order, spacing and line breaks; numbers in InvariantCulture as F#'s sprintf "%d" is culture-independent;
// booleans as lowercase "true"/"false" as F#'s "%b"), and LoadAll reads with System.Text.Json and the same options.
// The model classes keep WPF's property names and declaration order, since System.Text.Json (LoadAll) and Newtonsoft (co-op)
// map JSON by property name and serialize in declaration order.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Controls;
using Z1R_Tracker.Models.Z1R_TrackerInterop;

namespace Z1R_Avalonia;

public static class DungeonSaveAndLoad
{
    // (F#'s sprintf "%d"/"%b": culture-independent)
    private static string d(int x) => x.ToString(CultureInfo.InvariantCulture);
    private static string b(bool x) => x ? "true" : "false";

    public class DungeonRoomModel
    {
        public bool IsCompleted { get; set; } = false;
        public string RoomType { get; set; } = "";
        public string MonsterDetail { get; set; } = "";
        public string FloorDropDetail { get; set; } = "";
        public bool FloorDropShouldAppearBright { get; set; } = false;
        public bool IsDefault =>
            !(this.IsCompleted) &&
                this.RoomType == Z1R_Tracker.Models.Z1R_TrackerInterop.RoomType.Unmarked.AsHotKeyName() &&
                this.MonsterDetail == Z1R_Tracker.Models.Z1R_TrackerInterop.MonsterDetail.Unmarked.AsHotKeyName() &&
                this.FloorDropDetail == Z1R_Tracker.Models.Z1R_TrackerInterop.FloorDropDetail.Unmarked.AsHotKeyName() &&
                this.FloorDropShouldAppearBright;
        public DungeonRoomState AsDungeonRoomState()
        {
            var r = new DungeonRoomState();
            r.IsComplete = this.IsCompleted;
            r.RoomType = RoomTypeExtensions.FromHotKeyName(this.RoomType);
            r.MonsterDetail = MonsterDetailExtensions.FromHotKeyName(this.MonsterDetail);
            r.FloorDropDetail = FloorDropDetailExtensions.FromHotKeyName(this.FloorDropDetail);
            if (this.FloorDropShouldAppearBright != r.FloorDropAppearsBright)
                r.FloorDropAppearsBright = !r.FloorDropAppearsBright;
            return r;
        }
    }

    public static DungeonRoomModel DungeonRoomStateAsModel(DungeonRoomState state)
    {
        var r = new DungeonRoomModel();
        r.IsCompleted = state.IsComplete;
        r.RoomType = state.RoomType.AsHotKeyName();
        r.MonsterDetail = state.MonsterDetail.AsHotKeyName();
        r.FloorDropDetail = state.FloorDropDetail.AsHotKeyName();
        r.FloorDropShouldAppearBright = state.FloorDropAppearsBright;
        return r;
    }

    public class DungeonModel  // these are serialized in j,i order, to be more human-readable
    {
        public int[][] HorizontalDoors { get; set; } = null;
        public int[][] VerticalDoors { get; set; } = null;
        public bool[][] RoomIsCircled { get; set; } = null;
        public DungeonRoomModel[][] RoomStates { get; set; } = null;
        public int VanillaMapOverlay { get; set; } = 0;
    }

    public static string[] SaveDungeonModel(string prefix, DungeonModel model)
    {
        var lines = new List<string>();
        lines.Add("\"HorizontalDoors\": [");
        if (model.HorizontalDoors == null) throw new Exception("HorizontalDoors is null");
        for (int j = 0; j <= 7; j++)
        {
            var sb = new System.Text.StringBuilder("    [ ");
            for (int i = 0; i <= 6; i++)
                sb.Append(d(model.HorizontalDoors[i][j]) + (i == 6 ? "" : ","));
            sb.Append(" ]" + (j == 7 ? "" : ","));
            lines.Add(sb.ToString());
        }
        lines.Add("],");
        lines.Add("\"VerticalDoors\": [");
        for (int j = 0; j <= 6; j++)
        {
            var sb = new System.Text.StringBuilder("    [ ");
            for (int i = 0; i <= 7; i++)
                sb.Append(d(model.VerticalDoors[i][j]) + (i == 7 ? "" : ","));
            sb.Append(" ]" + (j == 6 ? "" : ","));
            lines.Add(sb.ToString());
        }
        lines.Add("],");
        lines.Add("\"RoomIsCircled\": [");
        for (int j = 0; j <= 7; j++)
        {
            var sb = new System.Text.StringBuilder("    [ ");
            for (int i = 0; i <= 7; i++)
                sb.Append(b(model.RoomIsCircled[i][j]) + (i == 7 ? "" : ","));
            sb.Append(" ]" + (j == 7 ? "" : ","));
            lines.Add(sb.ToString());
        }
        lines.Add("],");
        lines.Add("\"RoomStates\": [");
        for (int j = 0; j <= 7; j++)
        {
            lines.Add("    [");
            for (int i = 0; i <= 7; i++)
            {
                var drm = model.RoomStates[i][j];
                if (drm.IsDefault)
                    lines.Add("    null" + (i == 7 ? "" : ","));
                else
                    lines.Add("    { \"IsCompleted\": " + b(drm.IsCompleted) + ", \"RoomType\": \"" + drm.RoomType + "\", \"MonsterDetail\": \"" + drm.MonsterDetail
                              + "\", \"FloorDropDetail\": \"" + drm.FloorDropDetail + "\", \"FloorDropShouldAppearBright\": " + b(drm.FloorDropShouldAppearBright)
                              + " }" + (i == 7 ? "" : ","));
            }
            lines.Add("    ]" + (j == 7 ? "" : ","));
        }
        lines.Add("],");
        lines.Add("\"VanillaMapOverlay\": " + d(model.VanillaMapOverlay));
        return lines.Select(s => prefix + s).ToArray();
    }

    public static string[] SaveAllDungeons(DungeonModel[] models)
    {
        var r = new List<string>();
        for (int i = 0; i <= 8; i++)
        {
            r.AddRange(SaveDungeonModel("    ", models[i]));
            r.Add("    }" + (i == 8 ? "" : ", {"));
        }
        return r.ToArray();
    }

    /////////////////////////////////////////////////////////////////////////////
    // DrawingLayer model

    // (F#: a [<RequireQualifiedAccess>] union. As ShowRunCustom.Line: an abstract record with one nested record per case.
    // WPF's `DrawingLayerIcon.ZTracker k` → `new DrawingLayerIcon.ZTracker(k)`; a match reads `.Value`.)
    public abstract record DrawingLayerIcon
    {
        private DrawingLayerIcon() { }
        public sealed record ZTracker(string Value) : DrawingLayerIcon;   // resource key
        public sealed record ExtraIcon(string Value) : DrawingLayerIcon;  // s, refers to file (appdir)\ExtraIcons\s.png
    }

    public static readonly List<(DrawingLayerIcon, int, int, Image)> AllDrawingLayerStamps = new List<(DrawingLayerIcon, int, int, Image)>();  // also contains Image, but only Icon/X/Y needed for save&load

    public class DrawingLayerIconModel
    {
        public bool Extra { get; set; } = false;
        public string Name { get; set; } = "";
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
        public bool HalfSize { get; set; } = false;
    }

    public static string[] SaveDrawingLayer()
    {
        var r = new List<string>();
        for (int i = 0; i <= AllDrawingLayerStamps.Count - 1; i++)
        {
            var (icon, x, y, img) = AllDrawingLayerStamps[i];
            var (extra, name) = icon switch
            {
                DrawingLayerIcon.ZTracker z => (false, z.Value),
                DrawingLayerIcon.ExtraIcon e => (true, e.Value),
                _ => throw new InvalidOperationException()
            };
            // (WPF also treats a RenderTransform that is the shared Transform.Identity as "no transform"; Avalonia has no such shared
            // instance, and a control's RenderTransform is null until set, so null is the only "not half size" value.)
            var halfSize = !(img.RenderTransform == null);
            r.Add("        { \"Extra\": " + extra.ToString().ToLowerInvariant() + ", \"Name\": \"" + name + "\", \"X\": " + d(x) + ", \"Y\": " + d(y)
                  + ", \"HalfSize\": " + halfSize.ToString().ToLowerInvariant() + " }" + (i == AllDrawingLayerStamps.Count - 1 ? "" : ","));
        }
        return r.ToArray();
    }

    /////////////////////////////////////////////////////////////////////////////

    public class JustVersion
    {
        public string Version { get; set; } = "";
        public int SaveFormatVersion { get; set; } = 0;   // (absent in older saves: 0)
    }

    public class AllData
    {
        public string Version { get; set; } = "";
        public int TimeInSeconds { get; set; } = 0;
        public SaveAndLoad.Overworld Overworld { get; set; } = null;
        public SaveAndLoad.Items Items { get; set; } = null;
        public SaveAndLoad.PlayerProgressAndTakeAnyHeartsModel PlayerProgressAndTakeAnyHearts { get; set; } = null;
        public SaveAndLoad.StartingItemsAndExtrasModel StartingItemsAndExtras { get; set; } = null;
        public SaveAndLoad.Blocker[][] Blockers { get; set; } = null;
        public SaveAndLoad.Hints Hints { get; set; } = null;
        public string Notes { get; set; } = "";
        public int CurrentRecorderDestinationIndex { get; set; } = 0;
        public bool RecorderToNewDungeons { get; set; } = true;
        public bool RecorderToUnbeatenDungeons { get; set; } = false;
        public bool IsBoomstickSeed { get; set; } = false;
        public bool IsAtlasSeed { get; set; } = false;
        public bool IsWSMSReplacedByBUSeed { get; set; } = false;
        public int DungeonTabSelected { get; set; } = 9;
        public DungeonModel[] DungeonMaps { get; set; } = null;
        public SaveAndLoad.UserCustomChecklist UserCustomChecklist { get; set; } = null;
        public DrawingLayerIconModel[] DrawingLayerIcons { get; set; } = null;
        public string AlternativeOverworldMapFilename { get; set; } = "";
        public bool ShouldInitiallyHideOverworldMap { get; set; } = false;
        public string Seed { get; set; } = "";
        public string Flags { get; set; } = "";
        public int[] OverworldSpotsRemainingOverTime { get; set; } = null;
        public SaveAndLoad.TimelineDatum[] Timeline { get; set; } = null;
    }

    public static AllData LoadAll(string json)  // can throw
    {
        var data = System.Text.Json.JsonSerializer.Deserialize<AllData>(json, new System.Text.Json.JsonSerializerOptions { AllowTrailingCommas = true });
        return data;
    }
}
