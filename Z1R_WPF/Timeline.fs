module Timeline

open System.Windows.Media
open System.Windows.Controls
open System.Windows

let mutable isCurrentlyLoadingASave = false

let canvasAdd = Graphics.canvasAdd

type TimelineID =
    // "items"
    | WoodSword
    | WhiteSword             // the starting item (which is always a sword)   (Note that e.g. LevelBox(3,1) might me WHITESWORD which might be a BU)
    | MagicalSword           // the shopping area box (which might be a BU)
    | StartingMagicalSword   // the starting item (which is always a sword)
    | Boomerang
    | MagicBoomerang
    | WoodArrow
    | SilverArrow
    | BlueCandle
    | BlueRing
    | RedCandle
    | RedRing
    | BoomstickBook
    | Book
    | Gannon
    | Zelda
    | Bow
    | Wand
    | PowerBracelet
    | Raft
    | Recorder
    | AnyKey
    | Ladder
    // box locations
    | LadderBox
    | ArmosBox
    | MagicalSwordCaveBox
    | LetterCaveBox
    | WhiteSwordBox
    | Level1or4Box3
    | LevelBox of int * int
    // triforces
    | Triforce1
    | Triforce2
    | Triforce3
    | Triforce4
    | Triforce5
    | Triforce6
    | Triforce7
    | Triforce8
    // take any
    | TakeAnyHeart1
    | TakeAnyHeart2
    | TakeAnyHeart3
    | TakeAnyHeart4
    // checked off on the overworld map
    | Sword4
    | OWHeart of int
    | CustomIcon of int   // a custom icon (1-16) checked off on the overworld map
    // custom
    | UserCustom of string
    static member Triforce(n) =
        match n with
        | 1 -> Triforce1
        | 2 -> Triforce2
        | 3 -> Triforce3
        | 4 -> Triforce4
        | 5 -> Triforce5
        | 6 -> Triforce6
        | 7 -> Triforce7
        | 8 -> Triforce8
        | _ -> failwith "bad Triforce(n)"
    member this.Identifier =
        match this with
        | WoodSword -> "WoodSword"
        | WhiteSword -> "WhiteSword"
        | MagicalSword -> "MagicalSword"
        | StartingMagicalSword -> "StartingMagicalSword"
        | Boomerang -> "Boomerang"
        | MagicBoomerang -> "MagicBoomerang"
        | WoodArrow -> "WoodArrow"
        | SilverArrow -> "SilverArrow"
        | BlueCandle -> "BlueCandle"
        | BlueRing -> "BlueRing"
        | RedCandle -> "RedCandle"
        | RedRing -> "RedRing"
        | BoomstickBook -> "BoomstickBook"
        | Book -> "Book"
        | Gannon -> "Gannon"
        | Zelda -> "Zelda"
        | Bow -> "Bow"
        | Wand -> "Wand"
        | PowerBracelet -> "PowerBracelet"
        | Raft -> "Raft"
        | Recorder -> "Recorder"
        | AnyKey -> "AnyKey"
        | Ladder -> "Ladder"
        | LadderBox -> "LadderBox"
        | ArmosBox -> "ArmosBox"
        | MagicalSwordCaveBox -> "MagicalSwordCaveBox"
        | LetterCaveBox -> "LetterCaveBox"
        | WhiteSwordBox -> "WhiteSwordBox"
        | Level1or4Box3 -> "Level1or4Box3"
        | LevelBox(i,j) -> sprintf "Level%dBox%d" i j
        | Triforce1 -> "Triforce1"
        | Triforce2 -> "Triforce2"
        | Triforce3 -> "Triforce3"
        | Triforce4 -> "Triforce4"
        | Triforce5 -> "Triforce5"
        | Triforce6 -> "Triforce6"
        | Triforce7 -> "Triforce7"
        | Triforce8 -> "Triforce8"
        | TakeAnyHeart1 -> "TakeAnyHeart1"
        | TakeAnyHeart2 -> "TakeAnyHeart2"
        | TakeAnyHeart3 -> "TakeAnyHeart3"
        | TakeAnyHeart4 -> "TakeAnyHeart4"
        | Sword4 -> "Sword4"
        | OWHeart n -> sprintf "OWHeart%d" n
        | CustomIcon n -> sprintf "CustomIcon%d" n
        | UserCustom s -> "UserCustom_" + s

type TimelineItem(tid : TimelineID, f) =
    let model = TrackerModel.TimelineItemModel.All.[tid.Identifier]
    member this.IsDone = model.FinishedTotalSeconds <> -1
    member this.FinishedTotalSeconds = model.FinishedTotalSeconds
    member this.Bmp = f()
    member this.Has = model.Has
    member this.TID = tid

let TLC = Brushes.SandyBrown   // timeline color
let ICON_SPACING = 6.
let BIG_HASH = 15.
let LINE_THICKNESS = 3.
let numTicks, ticksPerHash = 60, 5
type Timeline(iconSize, numRows, lineWidth, minutesPerTick, sevenTexts:string[], moreOptionsButton:Button, _drawButton:Button) =
    let topRowReserveWidth = moreOptionsButton.DesiredSize.Width-24.
    let iconAreaHeight = float numRows*(iconSize+ICON_SPACING)
    let timelineCanvas = new Canvas(Height=iconAreaHeight+BIG_HASH, Width=lineWidth)
    let graphCanvas = new Canvas(Height=iconAreaHeight+BIG_HASH, Width=lineWidth)
    let owAxisLabel = new TextBox(Text="Overworld and Collection Timeline", Foreground=Brushes.DarkCyan, Background=Brushes.Transparent, TextAlignment=TextAlignment.Center, BorderThickness=Thickness(0.0), FontSize=12.0, FontWeight=FontWeights.Bold, IsHitTestVisible=false, IsReadOnly=true)
    let seedLabel = new TextBox(Foreground=Brushes.Orange, Background=Brushes.Transparent, BorderThickness=Thickness(0.0), FontSize=12.0, FontWeight=FontWeights.Bold, IsHitTestVisible=false, IsReadOnly=true)   // top left of the timeline: seed (all digits)
    let flagsLabel = new TextBox(Foreground=Brushes.Orange, Background=Brushes.Transparent, BorderThickness=Thickness(0.0), FontSize=12.0, FontWeight=FontWeights.Bold, IsHitTestVisible=false, IsReadOnly=true, TextAlignment=TextAlignment.Right)   // top right of the timeline: flags (letters and digits)
    let itemCanvas = new Canvas(Height=iconAreaHeight+BIG_HASH, Width=lineWidth)
    let timeToolTip = new TextBox(Foreground=Brushes.Orange, Background=Brushes.Black, BorderThickness=Thickness(3.0), FontSize=16.0, IsHitTestVisible=false)
    let line1 = new Shapes.Line(X1=0., Y1=iconAreaHeight+BIG_HASH/2., X2=lineWidth, Y2=iconAreaHeight+BIG_HASH/2., Stroke=TLC, StrokeThickness=LINE_THICKNESS)
    let curTime = new Shapes.Line(X1=0., Y1=iconAreaHeight, X2=0., Y2=iconAreaHeight+BIG_HASH, Stroke=Brushes.White, StrokeThickness=LINE_THICKNESS)
    let mutable iconAreaFilled = Array2D.zeroCreate (int lineWidth + 1) numRows
    let xf(i) =      i*(lineWidth/float numTicks)
    let x(i) = float i*(lineWidth/float numTicks)
    do
        let tb = new TextBlock(Text="0h",FontSize=12.,Foreground=TLC,Background=Brushes.Black)
        let mkft(t) = new FormattedText(t, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, 
                                            new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch), 
                                            tb.FontSize, tb.Foreground, new NumberSubstitution(), TextFormattingMode.Display, VisualTreeHelper.GetDpi(timelineCanvas).PixelsPerDip)
        let mutable n = 0
        let placeTextLabel(x) =
            let ft = mkft(sevenTexts.[n])
            if ft.Height > iconSize then
                printfn "bad formatted text"
            let ypos = iconAreaHeight + 6.
            let xpos = x - ft.Width/2.
            canvasAdd(timelineCanvas, new TextBlock(Text=sevenTexts.[n],FontSize=12.,Foreground=TLC,Background=Brushes.Black), xpos, ypos)
            n <- n + 1
        canvasAdd(timelineCanvas, graphCanvas, 0., 0.)
        owAxisLabel.Width <- lineWidth   // horizontal title, centered across the top of the timeline
        Canvas.SetLeft(owAxisLabel, 0.)
        Canvas.SetTop(owAxisLabel, 6.)
        graphCanvas.Children.Add(owAxisLabel) |> ignore
        Canvas.SetLeft(seedLabel, 0.)
        Canvas.SetTop(seedLabel, 6.)
        Canvas.SetRight(flagsLabel, 0.)
        Canvas.SetTop(flagsLabel, 6.)
        graphCanvas.Children.Add(seedLabel) |> ignore
        graphCanvas.Children.Add(flagsLabel) |> ignore
        let refreshSeedAndFlags() =
            let show = TrackerModelOptions.DisplaySeedAndFlags.Value
            seedLabel.Text <- (if show && SaveAndLoad.lastKnownSeed <> "" then "Seed: " + SaveAndLoad.lastKnownSeed else "")
            flagsLabel.Text <- (if show && SaveAndLoad.lastKnownFlags <> "" then "Flags: " + SaveAndLoad.lastKnownFlags else "")
            // the title is centered in the open space between the seed (left) and the flags (right)
            let textWidth(t:string) =
                if t = "" then 0.
                else (new FormattedText(t, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                                        new Typeface(seedLabel.FontFamily, seedLabel.FontStyle, FontWeights.Bold, seedLabel.FontStretch),
                                        12., Brushes.Orange, new NumberSubstitution(), TextFormattingMode.Display, VisualTreeHelper.GetDpi(timelineCanvas).PixelsPerDip)).Width + 10.   // (+10: the text box's own padding)
            let leftEdge = if seedLabel.Text = "" then 0. else textWidth(seedLabel.Text) + 10.
            let rightEdge = if flagsLabel.Text = "" then 0. else textWidth(flagsLabel.Text) + 10.
            Canvas.SetLeft(owAxisLabel, leftEdge)
            owAxisLabel.Width <- max 0. (lineWidth - leftEdge - rightEdge)
        refreshSeedAndFlags()
        SaveAndLoad.seedAndFlagsUpdated.Publish.Add(fun _ -> refreshSeedAndFlags())
        canvasAdd(timelineCanvas, itemCanvas, 0., 0.)
        for i = 0 to numTicks do
            if i%(2*ticksPerHash)=0 then
                let line = new Shapes.Line(X1=x(i)+0.5, Y1=iconAreaHeight, X2=x(i)+0.5, Y2=iconAreaHeight+BIG_HASH, Stroke=TLC, StrokeThickness=LINE_THICKNESS)
                canvasAdd(timelineCanvas, line, 0., 0.)
                placeTextLabel(x(i))
            elif i%(ticksPerHash)=0 then
                let line = new Shapes.Line(X1=x(i)+0.5, Y1=iconAreaHeight+BIG_HASH/4., X2=x(i)+0.5, Y2=iconAreaHeight+BIG_HASH*3./4., Stroke=TLC, StrokeThickness=LINE_THICKNESS)
                canvasAdd(timelineCanvas, line, 0., 0.)
        canvasAdd(timelineCanvas, line1, 0., 0.)
        canvasAdd(timelineCanvas, curTime, 0., 0.)
        // text labels
        for x = 0 to int topRowReserveWidth do
            iconAreaFilled.[x, 0] <- 99
    member this.Canvas = timelineCanvas
    member this.ThisIsNowTheVisibleTimeline() =
        // (the Options and Draw buttons now live in the top-left button stack above Item Locations; see Layout.AddTopLeftButtons)
        ()
    member this.Update(minute, timelineItems:seq<TimelineItem>, maxOverworldRemain) =
        if not isCurrentlyLoadingASave then
            let tick = minute / minutesPerTick
            if tick < 0 || tick > numTicks then
                ()
            else
                this.DrawGraph(tick, maxOverworldRemain)
                this.DrawItemsAndGuidelines(timelineItems)
                curTime.X1 <- xf(float (minute / minutesPerTick)) + 0.5
                curTime.X2 <- xf(float (minute / minutesPerTick)) + 0.5
    member private this.DrawGraph(curTick, maxOverworldRemain) =
        if TrackerModel.timelineDataOverworldSpotsRemain.Count > 0 then
            // populate data to graph
            let sorted = TrackerModel.timelineDataOverworldSpotsRemain.ToArray() |> Array.sortBy fst
            let remainPerTick = Array.create (numTicks+1) -1
            let maxRemain = sorted |> Array.maxBy snd |> snd
            remainPerTick.[0] <- maxRemain   // other buckets are populated with most-recent-value-achieved-in-prior-minute, but for 0th minute, we want max value
            let mutable highestTickPopulated = -1
            for s,r in sorted do
                let tickBucket = 1 + (s/60)/minutesPerTick
                if tickBucket >= 0 && tickBucket <= numTicks then
                    if tickBucket > highestTickPopulated then
                        for i = highestTickPopulated+1 to tickBucket-1 do
                            remainPerTick.[i] <- if i=0 then maxRemain else remainPerTick.[i-1]
                        remainPerTick.[tickBucket] <- r
                        highestTickPopulated <- tickBucket
                    else
                        assert(tickBucket = highestTickPopulated)
                        remainPerTick.[tickBucket] <- r
            if curTick > highestTickPopulated then
                let r = snd sorted.[sorted.Length-1]
                for i = highestTickPopulated+1 to curTick do
                    remainPerTick.[i] <- r
                    highestTickPopulated <- curTick
            // draw it
            graphCanvas.Children.Clear()
            graphCanvas.Children.Add(owAxisLabel) |> ignore
            graphCanvas.Children.Add(seedLabel) |> ignore
            graphCanvas.Children.Add(flagsLabel) |> ignore
            let y(r) = (iconAreaHeight / float maxOverworldRemain) * float r
            for i = 1 to curTick do
                if remainPerTick.[i-1] <> -1 && remainPerTick.[i] <> -1 then
                    let x1,x2 = xf(float(i-1)), xf(float(i))
                    let y1,y2 = y(remainPerTick.[i-1]), y(remainPerTick.[i])
                    let segment = new Shapes.Line(X1=x1, Y1=y1, X2=x2, Y2=y2, Stroke=Brushes.DarkCyan, StrokeThickness=2.)
                    canvasAdd(graphCanvas, segment, 0., 0.)
    member private this.DrawItemsAndGuidelines(timelineItems) =
        // redraw guidelines and items
        itemCanvas.Children.Clear()
        let iconAreaFilled = Array2D.copy iconAreaFilled  // grab fresh copy of fixed timeline labels, to mutate each time we redraw
        let buckets = new System.Collections.Generic.Dictionary<_,_>()
        // only the highest sword collected is shown (wood < white < magical < 4th sword), not every sword along the way
        let swordRank(tid:TimelineID) =
            match tid with
            | WoodSword -> 1
            | WhiteSword -> 2
            | MagicalSword | StartingMagicalSword -> 3
            | Sword4 -> 4
            | _ -> 0
        let topSwordRank = timelineItems |> Seq.filter (fun ti -> ti.IsDone) |> Seq.map (fun ti -> swordRank ti.TID) |> Seq.fold max 0
        // (one icon for the top sword: the starting-item flag and the cave's item box can both be stamped for the same sword, so show just the earliest)
        let topSwordItem = if topSwordRank = 0 then None else timelineItems |> Seq.filter (fun ti -> ti.IsDone && swordRank ti.TID = topSwordRank) |> Seq.sortBy (fun ti -> ti.FinishedTotalSeconds) |> Seq.tryHead
        let swordBoxHoldsSword = TrackerModel.sword3Box.IsDone() && TrackerModel.sword3Box.CellCurrent() = TrackerModel.ITEMS.MAGICALSWORD
        for ti in timelineItems do
            // (the magical sword cave's item box already shows that sword on the timeline: its 'have the sword' flag would draw it a second time)
            let dupOfCaveBox = swordBoxHoldsSword && (match ti.TID with | MagicalSword | StartingMagicalSword -> true | _ -> false)
            if ti.IsDone && not dupOfCaveBox && (swordRank ti.TID = 0 || (match topSwordItem with | Some t -> obj.ReferenceEquals(t, ti) | None -> false)) then
                let tick = float ti.FinishedTotalSeconds/(60.*float minutesPerTick) |> int
                if not(buckets.ContainsKey(tick)) then
                    buckets.Add(tick, ResizeArray())
                buckets.[tick].Add(ti.Bmp, ti.FinishedTotalSeconds, ti.Has, ti.TID)
        for tick = 0 to numTicks do
            if buckets.ContainsKey(tick) then
                let rowBmps = ResizeArray()
                let xminOrig,xmaxOrig = x(tick)-iconSize/2. , x(tick)+iconSize/2.
                let xmin,xmax = max 0 (int xminOrig), min (int xmaxOrig) (int lineWidth)
                for bmp in buckets.[tick] do
                    let mutable bestRow, bestPenalty = 0, 99999
                    for row = numRows-1 downto 0 do
                        let mutable penalty = 0
                        for x=xmin to xmax do
                            penalty <- penalty + iconAreaFilled.[x,row]
                        if penalty < bestPenalty then
                            bestRow <- row
                            bestPenalty <- penalty
                    for x=xmin to xmax do
                        iconAreaFilled.[x,bestRow] <- iconAreaFilled.[x,bestRow] + 1
                    rowBmps.Add(bestRow, bmp)
                if rowBmps.Count > 0 then
                    // guideline
                    let bottomRow = rowBmps |> Seq.maxBy fst |> fst
                    let line = new Shapes.Line(X1=x(tick)+0.5, Y1=float(bottomRow+1)*(iconSize+ICON_SPACING)-ICON_SPACING, X2=x(tick)+0.5, Y2=iconAreaHeight+BIG_HASH/2., Stroke=Brushes.Gray, StrokeThickness=LINE_THICKNESS)
                    canvasAdd(itemCanvas, line, 0., 0.)
                    // items
                    for row,(bmp,totalSeconds,has,tid) in rowBmps do
                        let c = new Canvas(Width=iconSize, Height=iconSize)
                        let img =
                            try Graphics.BMPtoImage ((if has = TrackerModel.PlayerHas.NO then Graphics.greyscale else id) bmp)
                            with e ->
                                printfn "Timeline icon for %s failed: %s" tid.Identifier (e.ToString())
                                Graphics.BMPtoImage ((if has = TrackerModel.PlayerHas.NO then Graphics.greyscale else id) (new System.Drawing.Bitmap(bmp.Width, bmp.Height)))
                        if img.Width = 30. && iconSize = 21. then
                            // a kludge to not make overworld hearts look so awful when scaled
                            img.Width <- 20.
                            img.Height <- 20.
                        else
                            img.Width <- iconSize
                            img.Height <- iconSize
                        c.Children.Add(img) |> ignore
                        if has = TrackerModel.PlayerHas.SKIPPED then Graphics.placeSkippedItemXDecorationImpl(c, iconSize)
                        c.MouseEnter.Add(fun _ ->
                            timelineCanvas.Children.Remove(timeToolTip)
                            timeToolTip.Text <- System.TimeSpan.FromSeconds(float totalSeconds).ToString("""hh\:mm\:ss""") + "\n" + tid.Identifier
                            let x = xminOrig
                            let x = min x (float(16*16*3 - 120))  // don't go off right screen edge
                            canvasAdd(timelineCanvas, timeToolTip, x, float row*(iconSize+ICON_SPACING)-40.)
                            )
                        c.MouseLeave.Add(fun _ ->
                            timelineCanvas.Children.Remove(timeToolTip)
                            )
                        canvasAdd(itemCanvas, c, System.Math.Ceiling(xminOrig), float row*(iconSize+ICON_SPACING))
