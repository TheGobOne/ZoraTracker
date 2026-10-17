module DungeonRoomState

open System.Windows.Controls
open System.Windows.Media
open System
open System.Windows
open Z1R_Tracker.Models
open Z1R_Tracker.Models.Z1R_TrackerInterop

let canvasAdd = Graphics.canvasAdd

let cachedTilePairs =
    RoomTypeGraphics.LoadUpscaledRoomBitmapPairs(RoomTypeGraphics.GetResourceStreamFromWPF("new_icons13x9.png"))

let mkTxt(txt) =
    new TextBox(
        FontSize = 12.,
        Foreground = Brushes.Orange,
        Background = Brushes.Black,
        IsReadOnly = true,
        IsHitTestVisible = false,
        Text = txt,
        VerticalAlignment = VerticalAlignment.Center,
        BorderThickness = Thickness(0.)
    )

let entranceRoomArrowColorBrush =
    let c = Graphics.entranceRoomArrowColor.Value
    Graphics.freeze(new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)))

let scale(bmp, scale) =
    if bmp = null then null
    else
        let icon = Graphics.BMPtoImage(bmp)
        icon.Width <- icon.Width * scale
        icon.Height <- icon.Height * scale
        icon.Stretch <- Stretch.UniformToFill
        RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.NearestNeighbor)
        icon

let mutable isDoingDragPaintOffTheMap = false
let veryDark = Graphics.freeze(new SolidColorBrush(Color.FromArgb(255uy, 60uy, 10uy, 20uy)))
let fakeUsedTransports = Array.init 9 (fun _ -> 2)

type DungeonRoomState(csharpModel: CDungeonRoomState) as this =
    let changedEvent = new Event<DungeonRoomState>()
    
    let mutable suppressChange = false

    do
        csharpModel.Changed.Add(fun _ ->
            if not suppressChange then
                changedEvent.Trigger(this)
                TrackerModel.dungeonRoomModelChanged.SetNowIfLocal(TrackerModelOptions.isCurrentlyApplyingRemoteUpdate())
        )

    new() = DungeonRoomState(new CDungeonRoomState())

    member _.CSharp = csharpModel
    member _.Changed = changedEvent.Publish




    static member FromCSharp(csharp: CDungeonRoomState) =
        let drs = new DungeonRoomState()
        drs.CSharp.IsComplete <- csharp.IsComplete
        drs.CSharp.RoomType <- csharp.RoomType
        drs.CSharp.MonsterDetail <- csharp.MonsterDetail
        drs.CSharp.FloorDropDetail <- csharp.FloorDropDetail
        drs.CSharp.FloorDropAppearsBright <- csharp.FloorDropAppearsBright
        drs.CSharp.X <- csharp.X
        drs.CSharp.Y <- csharp.Y
        drs.CSharp.Level <- csharp.Level
        drs

    member this.IsComplete
        with get() = csharpModel.IsComplete
        and set(v) =
            if csharpModel.IsComplete <> v then
                suppressChange <- true
                csharpModel.IsComplete <- v
                suppressChange <- false
                changedEvent.Trigger(this)

    member this.RoomType
        with get() = csharpModel.RoomType
        and set(v) =
            if csharpModel.RoomType <> v then
                suppressChange <- true
                csharpModel.RoomType <- v
                suppressChange <- false
                changedEvent.Trigger(this)

    member this.MonsterDetail
        with get() = csharpModel.MonsterDetail
        and set(v) =
            if csharpModel.MonsterDetail <> v then
                suppressChange <- true
                csharpModel.MonsterDetail <- v
                suppressChange <- false
                changedEvent.Trigger(this)

    member this.FloorDropDetail
        with get() = csharpModel.FloorDropDetail
        and set(v) =
            if csharpModel.FloorDropDetail <> v then
                suppressChange <- true
                csharpModel.FloorDropDetail <- v
                suppressChange <- false
                changedEvent.Trigger(this)

    member this.FloorDropAppearsBright
        with get() = csharpModel.FloorDropAppearsBright
        and set(v) =
            if csharpModel.FloorDropAppearsBright <> v then
                suppressChange <- true
                csharpModel.FloorDropAppearsBright <- v
                suppressChange <- false
                changedEvent.Trigger(this)

    member this.DebugId
        with get() = csharpModel.DebugId
        and set(v) =
            if csharpModel.DebugId <> v then
                suppressChange <- true
                csharpModel.DebugId <- v
                suppressChange <- false

    member this.X
        with get() = csharpModel.X
        and set(v) =
            if csharpModel.X <> v then
                suppressChange <- true
                csharpModel.X <- v
                suppressChange <- false
                changedEvent.Trigger(this)

    member this.Y
        with get() = csharpModel.Y
        and set(v) =
            if csharpModel.Y <> v then
                suppressChange <- true
                csharpModel.Y <- v
                suppressChange <- false
                changedEvent.Trigger(this)

    member this.Level
        with get() = csharpModel.Level
        and set(v) =
            if csharpModel.Level <> v then
                suppressChange <- true
                csharpModel.Level <- v
                suppressChange <- false
                changedEvent.Trigger(this)

    member this.ToggleFloorDropBrightness() =
        this.FloorDropAppearsBright <- not this.FloorDropAppearsBright

    member this.IsEmpty = csharpModel.RoomType.IsNotMarked() || csharpModel.RoomType.IsOffMap()
    member this.IsGannonOrZelda = csharpModel.RoomType.IsGannonOrZelda()

    member this.Clone() =
        let result = DungeonRoomState.FromCSharp(this.CSharp.Clone())
        result

    member this.CopyFrom(other: DungeonRoomState) =
        this.IsComplete <- other.IsComplete
        this.RoomType <- other.RoomType
        this.MonsterDetail <- other.MonsterDetail
        this.FloorDropDetail <- other.FloorDropDetail
        this.FloorDropAppearsBright <- other.FloorDropAppearsBright
        this.X <- other.X
        this.Y <- other.Y
        this.Level <- other.Level


    member this.CurrentDisplay() = this.CurrentDisplayEx(fakeUsedTransports)

    member this.CurrentDisplayEx(usedTransports) : FrameworkElement =
        let rt = this.RoomType
        let md = this.MonsterDetail
        let fd = this.FloorDropDetail
        let isCompleted = this.IsComplete
        let appearsBright = this.FloorDropAppearsBright
        let K = 18.

        if rt = RoomType.Unmarked && md = MonsterDetail.Unmarked && fd = FloorDropDetail.Unmarked then
            upcast (Graphics.BMPtoImage (rt.UncompletedBI()))
        else
            let c = new Canvas(Width = 13. * 3., Height = 9. * 3.)

            match rt with
            | RoomType.OffTheMap ->
                let black = new Canvas(Width = 13. * 3. + 12., Height = 9. * 3. + 12., Background = Brushes.Black, Opacity = 0.6)
                canvasAdd(c, black, -6., -6.)
                if isDoingDragPaintOffTheMap then
                    canvasAdd(black, new Shapes.Rectangle(Width = 13. * 3., Height = 9. * 3., Stroke = veryDark, StrokeThickness = 2.), 6., 6.)
            | _ ->
                let spriteOverride =
                    match rt with
                    | RoomType.Transport1 when usedTransports.[1] < 2 -> Some(34)
                    | RoomType.Transport2 when usedTransports.[2] < 2 -> Some(35)
                    | RoomType.Transport3 when usedTransports.[3] < 2 -> Some(36)
                    | RoomType.Transport4 when usedTransports.[4] < 2 -> Some(37)
                    | RoomType.Transport5 when usedTransports.[5] < 2 -> Some(38)
                    | RoomType.Transport6 when usedTransports.[6] < 2 -> Some(39)
                    | RoomType.Transport7 when usedTransports.[7] < 2 -> Some(40)
                    | RoomType.Transport8 when usedTransports.[8] < 2 -> Some(41)
                    | _ -> None

                let bmp =
                    match spriteOverride with
                    | Some idx ->
                        let (uncompleted, completed) = cachedTilePairs.[idx]
                        if isCompleted then completed else uncompleted

                    | None ->
                        if isCompleted then rt.CompletedBI() else rt.UncompletedBI()

                canvasAdd(c, Graphics.BMPtoImage bmp, 0., 0.)

            match rt with
            | RoomType.StartEnterFromE -> canvasAdd(c, new Canvas(Background = entranceRoomArrowColorBrush, Width = 3., Height = 9.), 13. * 3., 3. * 3.)
            | RoomType.StartEnterFromW -> canvasAdd(c, new Canvas(Background = entranceRoomArrowColorBrush, Width = 3., Height = 9.), -1. * 3., 3. * 3.)
            | RoomType.StartEnterFromN -> canvasAdd(c, new Canvas(Background = entranceRoomArrowColorBrush, Width = 9., Height = 3.), 5. * 3., -1. * 3.)
            | RoomType.StartEnterFromS -> canvasAdd(c, new Canvas(Background = entranceRoomArrowColorBrush, Width = 9., Height = 3.), 5. * 3., 9. * 3.)
            | _ -> ()

            match md with
            | MonsterDetail.Unmarked -> ()
            | _ ->
                match md.Bmp() with
                | null -> ()
                | bmp ->
                    let monsterIcon = Graphics.BMPtoImage bmp
                    canvasAdd(c, monsterIcon, -5., -3.)
                    if isCompleted then
                        let shouldDarken =
                            match md with
                            | MonsterDetail.BlueBubble | MonsterDetail.RedBubble | MonsterDetail.Other | MonsterDetail.Other2 | MonsterDetail.Traps -> false
                            | _ -> true
                        if shouldDarken then
                            let dp = new DockPanel(Width = K, Height = K, Background = Brushes.Black, Opacity = 0.5)
                            canvasAdd(c, dp, -5., -3.)

            match fd with
            | FloorDropDetail.Unmarked -> ()
            | _ ->
                match fd.Bmp() with
                | null -> ()
                | bmp ->
                    let floorDropIcon = Graphics.BMPtoImage bmp
                    canvasAdd(c, floorDropIcon, 44. - K, 30. - K)
                    if not appearsBright then
                        let dp = new DockPanel(Width = K, Height = K, Background = Brushes.Black, Opacity = 0.5)
                        canvasAdd(c, dp, 44. - K, 30. - K)

            upcast c
