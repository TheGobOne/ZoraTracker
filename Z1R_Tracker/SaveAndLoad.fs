module SaveAndLoad

// all save files (manual, completed and autosave) live in a 'Saves' folder next to the program
let SavesDirectory = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Saves")
let EnsureSavesDirectory() =
    try System.IO.Directory.CreateDirectory(SavesDirectory) |> ignore with _ -> ()
let AutoSaveFilename = System.IO.Path.Combine(SavesDirectory, "zt-save-zz-autosave.json")

[<AllowNullLiteral>]
type Overworld() =
    member val Quest = -1 with get,set
    member val MirrorOverworld = false with get,set
    member val StartIconX = -1 with get,set
    member val StartIconY = -1 with get,set
    member val CustomWaypointX = -1 with get,set
    member val CustomWaypointY = -1 with get,set
    member val Map : int[] = null with get,set
    member val SwordIcons : int[] = null with get,set
    member val BombDroppers : int[] = null with get,set
    member val ShopStash : int[] = null with get,set
    member val CheapKeyShops : int[] = null with get,set
    member val HeartShuffleMode = -1 with get,set   // 0 = Off, 1 = Intra, 2 = Full; -1 = older save: keep what the start screen chose
    member val LegacyMode = -1 with get,set  // 1 = the game was in Legacy Mode, 0 = it was not, -1 = older save: keep what the start screen chose
    member val ExtraBlocks = -1 with get,set   // bit 0 = extra power bracelet blocks, bit 1 = extra raft blocks, bit 2 = Dead Woods hint, bit 3 = Lost Hills hint (-1 = older save: keep what the start screen chose). A Random (?) option is saved here as On (older trackers load it that way), and also in ExtraBlocksRandom
    member val ExtraBlocksRandom = -1 with get,set   // same bits: which of those four were Random (?) rather than plain On (-1 = older save: none were)
    member val BootsReplaceBoomerangs = -1 with get,set   // 0 = Off, 1 = On, 2 = Random (?, counts as Off until confirmed); -1 = older save: keep what the start screen chose
    member val ProgressiveItems = -1 with get,set   // 0 = Off, 1 = On; -1 = older save: keep what the start screen chose
    member val DeadWoodsHint : string = null with get,set   // the letters typed into the Dead Woods / Lost Hills hint buttons (null = older save)
    member val LostHillsHint : string = null with get,set

[<AllowNullLiteral>]
type Box() =
    member val CellCurrent = -1 with get,set
    member val PlayerHas = 0 with get,set
    member this.TryApply(b : TrackerModel.Box) = b.AttemptToSet(this.CellCurrent, TrackerModel.PlayerHas.FromInt this.PlayerHas)

[<AllowNullLiteral>]
type Dungeon() =
    member val Triforce = false with get,set
    member val Color = 0 with get,set
    member val LabelChar = "?" with get,set
    member val PlayerHasMap = false with get,set
    member val Boxes : Box[] = null with get,set
    member val ExtraBox : Box = null with get,set
    member this.TryApply(d : TrackerModel.Dungeon) =
      if isNull this.Boxes || this.Boxes.Length <> d.Boxes.Length then false   // (a save with a different number of boxes: nothing is applied, rather than throwing mid-load)
      else
        if this.Triforce <> d.PlayerHasTriforce() then
            d.ToggleTriforce()
        d.Color <- this.Color
        d.LabelChar <- (if System.String.IsNullOrEmpty(this.LabelChar) then '?' else this.LabelChar.[0])
        d.PlayerHasMapOfThisDungeon <- this.PlayerHasMap
        let extraOk = if this.ExtraBox <> null then this.ExtraBox.TryApply(d.ExtraFloorBox) else true
        let boxesOk = (this.Boxes, d.Boxes) ||> Array.map2 (fun bs bd -> bs.TryApply(bd)) |> Array.fold (fun a b -> a && b) true
        boxesOk && extraOk

[<AllowNullLiteral>]
type Items() =
    member val HiddenDungeonNumbers = false with get,set
    member val SecondQuestDungeons = false with get,set
    member val WhiteSwordBox : Box = null with get,set
    member val LadderBox : Box = null with get,set
    member val ArmosBox : Box = null with get,set
    member val MagicalSwordBox : Box = null with get,set   // (older saves do not have these: null)
    member val LetterBox : Box = null with get,set
    member val WoodSwordBox : Box = null with get,set
    member val CustomLevelName : string = null with get,set   // (older saves do not have this)
    member val ShuffledWoodSwordTiles : string = null with get,set   // "i,j;i,j" overworld tiles whose wood sword cave holds a shuffled item
    member val HintShopColors : string = null with get,set   // "i,j,color;..." background colors chosen for hint shop tiles (older saves do not have this)
    member val ProgressiveTileTiers : string = null with get,set   // "i,j,item,tier;..." the progressive tier each collected overworld item icon was collected at (older saves do not have this)
    member val ProgressiveBoxTiers : string = null with get,set   // "boxname,tier;..." the same for item boxes; box names come from namedBoxes (older saves do not have this)
    member val DupLinks : string = null with get,set   // "si,sj,di,dj;..." overworld tiles whose items were duplicated onto other tiles (older saves do not have this)
    member val ShopPrices : string = null with get,set   // "i,j,price;..." rupee prices noted for shop tiles (older saves do not have this)
    member val TakeAnyTaken : string = null with get,set   // "i,j,slot,candle01,kind;..." take any caves and what was taken there (older saves do not have this)
    member val TakeAnySlotGaveCandle : string = null with get,set   // "0100": per main screen take any heart slot, 1 when cycling it to the candle gave the blue candle (older saves do not have this: all 0)
    member val Level9TriforceCount = 8 with get,set   // triforces the level 9 entry rule needs (older saves: 8)
    member val Level9TriggerSwordTier = 1 with get,set   // progressive sword tier shown by a sword entry item (older saves: 1)
    member val Level9TriggerBox : Box = null with get,set   // the item that opens level 9 instead of triforces (older saves: null, none)
    member val Level2StairBoxEnabled = false with get,set
    member val Level2StairBox : Box = null with get,set
    member val Level9FloorBoxEnabled = false with get,set
    member val Dungeons : Dungeon[] = null with get,set

[<AllowNullLiteral>]
type Blocker() =
    member val Kind = "" with get,set
    member val AppliesTo : bool[] = null with get,set  // map, compass, tri, box1, box2, box3

[<AllowNullLiteral>]
type Hints() =
    member val LocationHints : int[] = null with get,set
    member val NoFeatOfStrengthHint = false with get,set
    member val SailNotHint = false with get,set
    member val SailToHint = false with get,set
    member val PlayMelodyHint = false with get,set
    member val StepOverWaterHint = false with get,set
    member val FireArrowsHint = false with get,set

[<AllowNullLiteral>]
type PlayerProgressAndTakeAnyHeartsModel() =
    member val TakeAnyHearts : int[] = null with get,set
    member val PlayerHasBoomBook = false with get,set
    member val PlayerHasWoodSword = false with get,set
    member val PlayerHasWoodArrow = false with get,set
    member val PlayerHasBlueRing = false with get,set
    member val PlayerHasBlueCandle = false with get,set
    member val PlayerHasMagicalSword = false with get,set
    member val PlayerHasDefeatedGanon = false with get,set
    member val PlayerHasRescuedZelda = false with get,set
    member val PlayerHasBombs = false with get,set
    static member Create() =
        let r = new PlayerProgressAndTakeAnyHeartsModel()
        r.TakeAnyHearts <- Array.init 4 (fun i -> TrackerModel.playerProgressAndTakeAnyHearts.GetTakeAnyHeart(i))
        r.PlayerHasBoomBook <- TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBoomBook.Value()
        r.PlayerHasWoodSword <- TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword.Value()
        r.PlayerHasWoodArrow <- TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodArrow.Value()
        r.PlayerHasBlueRing <- TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueRing.Value()
        r.PlayerHasBlueCandle <- TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle.Value()
        r.PlayerHasMagicalSword <- TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Value()
        r.PlayerHasDefeatedGanon <- TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasDefeatedGanon.Value()
        r.PlayerHasRescuedZelda <- TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Value()
        r.PlayerHasBombs <- TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBombs.Value()
        r
    member this.Apply() =
        for i = 0 to 3 do
            TrackerModel.playerProgressAndTakeAnyHearts.SetTakeAnyHeart(i, this.TakeAnyHearts.[i])
        TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBoomBook.Set(this.PlayerHasBoomBook)
        TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword.Set(this.PlayerHasWoodSword)
        TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodArrow.Set(this.PlayerHasWoodArrow)
        TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueRing.Set(this.PlayerHasBlueRing)
        TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle.Set(this.PlayerHasBlueCandle)
        TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Set(this.PlayerHasMagicalSword)
        TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasDefeatedGanon.Set(this.PlayerHasDefeatedGanon)
        TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Set(this.PlayerHasRescuedZelda)
        TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBombs.Set(this.PlayerHasBombs)

[<AllowNullLiteral>]
type StartingItemsAndExtrasModel() =
    member val HDNStartingTriforcePieces : bool[] = null with get,set
    member val PlayerHasWhiteSword = false with get,set
    member val PlayerHasMagicalSword = false with get,set
    member val PlayerHasSilverArrow = false with get,set
    member val PlayerHasBow = false with get,set
    member val PlayerHasWand = false with get,set
    member val PlayerHasRedCandle = false with get,set
    member val PlayerHasBoomerang = false with get,set
    member val PlayerHasMagicBoomerang = false with get,set
    member val PlayerHasRedRing = false with get,set
    member val PlayerHasPowerBracelet = false with get,set
    member val PlayerHasLadder = false with get,set
    member val PlayerHasRaft = false with get,set
    member val PlayerHasRecorder = false with get,set
    member val PlayerHasAnyKey = false with get,set
    member val PlayerHasBook = false with get,set
    member val PlayerHasMeat = false with get,set
    member val PlayerHasLetter = false with get,set
    member val PlayerHasSword4 = false with get,set   // (older saves do not have this: false)
    member val MaxHeartsDifferential = 0 with get,set
    static member Create() =
        let r = new StartingItemsAndExtrasModel()
        r.HDNStartingTriforcePieces <- Array.init 8 (fun i -> TrackerModel.startingItemsAndExtras.HDNStartingTriforcePieces.[i].Value())
        r.PlayerHasWhiteSword <- TrackerModel.startingItemsAndExtras.PlayerHasWhiteSword.Value()
        r.PlayerHasMagicalSword <- TrackerModel.startingItemsAndExtras.PlayerHasMagicalSword.Value()
        r.PlayerHasSilverArrow <- TrackerModel.startingItemsAndExtras.PlayerHasSilverArrow.Value()
        r.PlayerHasBow <- TrackerModel.startingItemsAndExtras.PlayerHasBow.Value()
        r.PlayerHasWand <- TrackerModel.startingItemsAndExtras.PlayerHasWand.Value()
        r.PlayerHasRedCandle <- TrackerModel.startingItemsAndExtras.PlayerHasRedCandle.Value()
        r.PlayerHasBoomerang <- TrackerModel.startingItemsAndExtras.PlayerHasBoomerang.Value()
        r.PlayerHasMagicBoomerang <- TrackerModel.startingItemsAndExtras.PlayerHasMagicBoomerang.Value()
        r.PlayerHasRedRing <- TrackerModel.startingItemsAndExtras.PlayerHasRedRing.Value()
        r.PlayerHasPowerBracelet <- TrackerModel.startingItemsAndExtras.PlayerHasPowerBracelet.Value()
        r.PlayerHasLadder <- TrackerModel.startingItemsAndExtras.PlayerHasLadder.Value()
        r.PlayerHasRaft <- TrackerModel.startingItemsAndExtras.PlayerHasRaft.Value()
        r.PlayerHasRecorder <- TrackerModel.startingItemsAndExtras.PlayerHasRecorder.Value()
        r.PlayerHasAnyKey <- TrackerModel.startingItemsAndExtras.PlayerHasAnyKey.Value()
        r.PlayerHasBook <- TrackerModel.startingItemsAndExtras.PlayerHasBook.Value()
        r.PlayerHasMeat <- TrackerModel.startingItemsAndExtras.PlayerHasMeat.Value()
        r.PlayerHasLetter <- TrackerModel.startingItemsAndExtras.PlayerHasLetter.Value()
        r.PlayerHasSword4 <- TrackerModel.startingItemsAndExtras.PlayerHasSword4.Value()
        r.MaxHeartsDifferential <- TrackerModel.startingItemsAndExtras.MaxHeartsDifferential
        r
    member this.Apply() =
        for i = 0 to 7 do
            TrackerModel.startingItemsAndExtras.HDNStartingTriforcePieces.[i].Set(this.HDNStartingTriforcePieces.[i])
        TrackerModel.startingItemsAndExtras.PlayerHasWhiteSword.Set(this.PlayerHasWhiteSword)
        TrackerModel.startingItemsAndExtras.PlayerHasMagicalSword.Set(this.PlayerHasMagicalSword)
        TrackerModel.startingItemsAndExtras.PlayerHasSilverArrow.Set(this.PlayerHasSilverArrow)
        TrackerModel.startingItemsAndExtras.PlayerHasBow.Set(this.PlayerHasBow)
        TrackerModel.startingItemsAndExtras.PlayerHasWand.Set(this.PlayerHasWand)
        TrackerModel.startingItemsAndExtras.PlayerHasRedCandle.Set(this.PlayerHasRedCandle)
        TrackerModel.startingItemsAndExtras.PlayerHasBoomerang.Set(this.PlayerHasBoomerang)
        TrackerModel.startingItemsAndExtras.PlayerHasMagicBoomerang.Set(this.PlayerHasMagicBoomerang)
        TrackerModel.startingItemsAndExtras.PlayerHasRedRing.Set(this.PlayerHasRedRing)
        TrackerModel.startingItemsAndExtras.PlayerHasPowerBracelet.Set(this.PlayerHasPowerBracelet)
        TrackerModel.startingItemsAndExtras.PlayerHasLadder.Set(this.PlayerHasLadder)
        TrackerModel.startingItemsAndExtras.PlayerHasRaft.Set(this.PlayerHasRaft)
        TrackerModel.startingItemsAndExtras.PlayerHasRecorder.Set(this.PlayerHasRecorder)
        TrackerModel.startingItemsAndExtras.PlayerHasAnyKey.Set(this.PlayerHasAnyKey)
        TrackerModel.startingItemsAndExtras.PlayerHasBook.Set(this.PlayerHasBook)
        TrackerModel.startingItemsAndExtras.PlayerHasMeat.Set(this.PlayerHasMeat)
        TrackerModel.startingItemsAndExtras.PlayerHasLetter.Set(this.PlayerHasLetter)
        TrackerModel.startingItemsAndExtras.PlayerHasSword4.Set(this.PlayerHasSword4)
        TrackerModel.startingItemsAndExtras.MaxHeartsDifferential <- this.MaxHeartsDifferential

[<AllowNullLiteral>]
type TimelineDatum() =
    member val Ident = "" with get,set
    member val Seconds = -1 with get,set
    member val Has = 1 with get,set

[<AllowNullLiteral>]
type UserCustomCheckbox() =
    member val Left = 0 with get,set
    member val Top = 0 with get,set
    member val Width = 0 with get,set
    member val Height = 0 with get,set
    member val DisplayLabel = "" with get,set
    member val IsChecked = false with get,set
    member val TimelineIconFilename = "" with get,set       // empty string means not for timeline, else pulls icon from ExtraIcons

[<AllowNullLiteral>]
type UserCustomChecklist() =
    member val Name = "" with get,set
    member val BackgroundImageFilename = "" with get,set    // pulls image from ExtraIcons
    member val Items : UserCustomCheckbox[] = null with get,set

let mutable theUserCustomChecklist = null : UserCustomChecklist

//////////////////////////////////////////////////////////////////////////////

let SaveOverworld(prefix) =
    let lines = ResizeArray()
    lines.Add(sprintf """"Overworld": {""")
    lines.Add(sprintf """    "Quest": %d,""" (TrackerModel.owInstance.Quest.AsInt()))
    lines.Add(sprintf """    "MirrorOverworld": %b,""" (TrackerModel.MirrorOverworld))
    lines.Add(sprintf """    "StartIconX": %d,""" TrackerModel.startIconX)
    lines.Add(sprintf """    "StartIconY": %d,""" TrackerModel.startIconY)
    lines.Add(sprintf """    "CustomWaypointX": %d,""" TrackerModel.customWaypointX)
    lines.Add(sprintf """    "CustomWaypointY": %d,""" TrackerModel.customWaypointY)
    lines.Add(sprintf """    "SwordIcons": [%s],""" (System.String.Join(",", [| for j = 0 to 7 do for i = 0 to 15 do yield string TrackerModel.owSwordIcons.[i,j] |])))
    lines.Add(sprintf """    "BombDroppers": [%s],""" (System.String.Join(",", [| for j = 0 to 7 do for i = 0 to 15 do yield (if TrackerModel.owBombDroppers.[i,j] then "1" else "0") |])))
    lines.Add(sprintf """    "ShopStash": [%s],""" (System.String.Join(",", [| for k = 0 to 5 do yield string TrackerModel.shopStash.[k] |])))
    lines.Add(sprintf """    "CheapKeyShops": [%s],""" (System.String.Join(",", [| for k = 0 to 5 do yield (if TrackerModel.cheapKeyShops.[k] then "1" else "0") |])))
    lines.Add(sprintf """    "LegacyMode": %d,""" (if TrackerModel.LegacyMode then 1 else 0))
    lines.Add(sprintf """    "HeartShuffleMode": %d,""" TrackerModel.HeartShuffleMode)
    lines.Add(sprintf """    "ExtraBlocks": %d,""" ((if OverworldData.ExtraPBBlocks then 1 else 0) + (if OverworldData.ExtraRaftBlocks then 2 else 0) + (if OverworldData.DeadWoodsEnabled then 4 else 0) + (if OverworldData.LostHillsEnabled then 8 else 0)))
    lines.Add(sprintf """    "ExtraBlocksRandom": %d,""" ((if OverworldData.ExtraPBRandom then 1 else 0) + (if OverworldData.ExtraRaftRandom then 2 else 0) + (if OverworldData.DeadWoodsRandom then 4 else 0) + (if OverworldData.LostHillsRandom then 8 else 0)))
    lines.Add(sprintf """    "BootsReplaceBoomerangs": %d,""" (if TrackerModel.BootsReplaceBoomerangs then 1 elif TrackerModel.BootsRandom then 2 else 0))
    lines.Add(sprintf """    "ProgressiveItems": %d,""" (if TrackerModel.ProgressiveItems then 1 else 0))
    let cleanHint (s:string) = (if isNull s then "" else s).Replace("\\", "").Replace("\"", "")
    lines.Add(sprintf """    "DeadWoodsHint": "%s",""" (cleanHint OverworldData.DeadWoodsHint))
    lines.Add(sprintf """    "LostHillsHint": "%s",""" (cleanHint OverworldData.LostHillsHint))
    lines.Add(sprintf """    "Map": [""")
    for j = 0 to 7 do
        let sb = new System.Text.StringBuilder("        ")
        for i = 0 to 15 do
            let cur = TrackerModel.overworldMapMarks.[i,j].Current()
            let k = if TrackerModel.MapSquareChoiceDomainHelper.IsItem(cur) then TrackerModel.MapSquareChoiceDomainHelper.SHOP else cur
            let ed = if cur = -1 then -1 else TrackerModel.getOverworldMapExtraData(i,j,k)   // TODO why -1 case, not exist?
            let circle = TrackerModel.overworldMapCircles.[i,j]
            let comma = if j=7 && i=15 then "" else ","
            sb.Append(sprintf "%2d,%2d,%3d%s  " cur ed circle comma) |> ignore
        lines.Add(sb.ToString())
    lines.Add(sprintf """    ]""")
    lines.Add(sprintf """},""")
    lines |> Seq.map (fun s -> prefix+s) |> Seq.toArray

// every item box with a name that stays the same between saving and loading (progressiveBoxTier is keyed by the box object itself)
let namedBoxes() : (string * TrackerModel.Box)[] =
    let inst = TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstance
    let finalBox = if inst.Kind = TrackerModel.DungeonTrackerInstanceKind.DEFAULT then Some(inst.FinalBoxOf1Or4) else None
    let isFinal (b:TrackerModel.Box) = match finalBox with Some f -> obj.ReferenceEquals(b, f) | None -> false
    [|
        for i = 0 to 8 do
            let d = TrackerModel.GetDungeon(i)
            let own = d.Boxes |> Array.filter (fun b -> not(isFinal b))   // (the shared level 1/4 box moves between dungeons 1 and 4: it gets its own name)
            for k = 0 to own.Length-1 do
                yield (sprintf "d%d.%d" i k, own.[k])
            yield (sprintf "x%d" i, d.ExtraFloorBox)
        match finalBox with
        | Some f -> yield ("F", f)
        | None -> ()
        yield ("L2", TrackerModel.level2StairBox)
        yield ("Ladder", TrackerModel.ladderBox)
        yield ("Armos", TrackerModel.armosBox)
        yield ("Sword2", TrackerModel.sword2Box)
        yield ("Sword3", TrackerModel.sword3Box)
        yield ("Letter", TrackerModel.letterBox)
        yield ("Sword1", TrackerModel.sword1Box)
    |]
// the UI keeps the duplicate links; it sets this so a save includes them (null: none)
let mutable DupLinksText : System.Func<string> = null
// the UI keeps which take any caves were taken; it sets this so a save includes them (null: none)
let mutable TakeAnyTakenText : System.Func<string> = null
// and which main screen take any heart slots gave the blue candle ("0100"; null: none)
let mutable TakeAnySlotGaveCandleText : System.Func<string> = null

/// Puts the saved shop prices and level 9 entry rule back into the model (older saves keep the startup values).
let ApplyLoadedItemExtras(items:Items) =
    if not (isNull items) then
        if not (isNull items.ShopPrices) then
            for i = 0 to 15 do
                for j = 0 to 7 do
                    TrackerModel.overworldShopPrices.[i,j] <- 0
            for t in items.ShopPrices.Split([|';'|], System.StringSplitOptions.RemoveEmptyEntries) do
                let p = t.Split(',')
                if p.Length = 3 then
                    match System.Int32.TryParse(p.[0]), System.Int32.TryParse(p.[1]), System.Int32.TryParse(p.[2]) with
                    | (true, i), (true, j), (true, v) when i >= 0 && i <= 15 && j >= 0 && j <= 7 && v > 0 -> TrackerModel.setShopPrice(i, j, v)
                    | _ -> ()
        TrackerModel.level9TriforceCount <- max 0 (min 8 items.Level9TriforceCount)
        TrackerModel.level9TriggerSwordTier <- max 1 (min 4 items.Level9TriggerSwordTier)
        if not (isNull items.Level9TriggerBox) then
            items.Level9TriggerBox.TryApply(TrackerModel.level9TriggerBox) |> ignore   // (its Changed event redraws the level 9 entry picture and count)

let SaveItems(prefix) =
    let lines = ResizeArray()
    let SaveBox(pre, box:TrackerModel.Box) =
        lines.Add(sprintf """%s%s"CellCurrent": %d, "PlayerHas": %d""" prefix pre (box.CellCurrent()) (box.PlayerHas().AsInt()))
    lines.Add(sprintf """"Items": {""")
    lines.Add(sprintf """    "HiddenDungeonNumbers": %b,""" (TrackerModel.IsHiddenDungeonNumbers()))
    lines.Add(sprintf """    "SecondQuestDungeons": %b,""" TrackerModel.IsSecondQuestDungeons)
    lines.Add(sprintf """    "WhiteSwordBox": {""")
    SaveBox("    ", TrackerModel.sword2Box)
    lines.Add(sprintf """    }, "LadderBox": {""")
    SaveBox("    ", TrackerModel.ladderBox)
    lines.Add(sprintf """    }, "ArmosBox": {""")
    SaveBox("    ", TrackerModel.armosBox)
    lines.Add(sprintf """    }, "MagicalSwordBox": {""")
    SaveBox("    ", TrackerModel.sword3Box)
    lines.Add(sprintf """    }, "LetterBox": {""")
    SaveBox("    ", TrackerModel.letterBox)
    lines.Add(sprintf """    }, "WoodSwordBox": {""")
    SaveBox("    ", TrackerModel.sword1Box)
    let shuffledTiles = TrackerModel.shuffledWoodSwordTiles |> Seq.map (fun (i,j) -> sprintf "%d,%d" i j) |> String.concat ";"
    let customName = TrackerModel.CustomLevelName.Replace("\\", "").Replace("\"", "")
    let hintColors = TrackerModel.hintShopColors |> Seq.filter (fun kv -> kv.Value <> 0) |> Seq.map (fun kv -> sprintf "%d,%d,%d" (fst kv.Key) (snd kv.Key) kv.Value) |> String.concat ";"
    let progressiveTiers = TrackerModel.progressiveTileTier |> Seq.map (fun kv -> let (i,j,item) = kv.Key in sprintf "%d,%d,%d,%d" i j item kv.Value) |> String.concat ";"
    let boxTiers =
        try
            namedBoxes() |> Array.choose (fun (name, b) -> match TrackerModel.progressiveBoxTier.TryGetValue(b) with | true, t -> Some(sprintf "%s,%d" name t) | _ -> None) |> String.concat ";"
        with _ -> ""
    let dupLinks = if isNull DupLinksText then "" else (try DupLinksText.Invoke() with _ -> "")
    let takeAnyTaken = if isNull TakeAnyTakenText then "" else (try TakeAnyTakenText.Invoke() with _ -> "")
    let takeAnyCandle = if isNull TakeAnySlotGaveCandleText then "" else (try TakeAnySlotGaveCandleText.Invoke() with _ -> "")
    let shopPrices = [| for i = 0 to 15 do for j = 0 to 7 do if TrackerModel.overworldShopPrices.[i,j] > 0 then yield sprintf "%d,%d,%d" i j TrackerModel.overworldShopPrices.[i,j] |] |> String.concat ";"
    lines.Add(sprintf """    }, "ShuffledWoodSwordTiles": "%s", "HintShopColors": "%s", "ProgressiveTileTiers": "%s", "ProgressiveBoxTiers": "%s", "DupLinks": "%s", "ShopPrices": "%s", "TakeAnyTaken": "%s", "TakeAnySlotGaveCandle": "%s", "CustomLevelName": "%s",""" shuffledTiles hintColors progressiveTiers boxTiers dupLinks shopPrices takeAnyTaken takeAnyCandle customName)
    lines.Add(sprintf """    "Level9TriforceCount": %d, "Level9TriggerSwordTier": %d, "Level9TriggerBox": {""" TrackerModel.level9TriforceCount TrackerModel.level9TriggerSwordTier)
    SaveBox("    ", TrackerModel.level9TriggerBox)
    lines.Add(sprintf """    }, "Level2StairBoxEnabled": %b, "Level2StairBox": {""" TrackerModel.level2StairBoxEnabled)
    SaveBox("    ", TrackerModel.level2StairBox)
    lines.Add(sprintf """    }, "Level9FloorBoxEnabled": %b, "Dungeons": [""" TrackerModel.level9FloorBoxEnabled)
    for i = 0 to 8 do
        let d = TrackerModel.GetDungeon(i)
        lines.Add(sprintf """            { "Triforce": %b, "Color": %d, "LabelChar": "%s", "PlayerHasMap": %b, "Boxes": [ {""" (d.PlayerHasTriforce()) d.Color (d.LabelChar.ToString()) d.PlayerHasMapOfThisDungeon) |> ignore
        for box in d.Boxes do
            SaveBox("            ", box)
            lines.Add("                }, {")
        lines.RemoveAt(lines.Count-1)
        lines.Add("                } ], \"ExtraBox\": {")
        SaveBox("            ", d.ExtraFloorBox)
        if i<>8 then
            lines.Add("        } },")
        else
            lines.Add("} } ] },")
    lines |> Seq.map (fun s -> prefix+s) |> Seq.toArray

let SavePlayerProgressAndTakeAnyHearts(prefix) =
    let m = PlayerProgressAndTakeAnyHeartsModel.Create()
    let lines = ResizeArray()
    lines.Add(""""PlayerProgressAndTakeAnyHearts": {""")
    lines.Add(sprintf """    "TakeAnyHearts": [ %d, %d, %d, %d ],""" m.TakeAnyHearts.[0] m.TakeAnyHearts.[1] m.TakeAnyHearts.[2] m.TakeAnyHearts.[3])
    lines.Add(sprintf """    "PlayerHasBoomBook": %b,""" m.PlayerHasBoomBook)
    lines.Add(sprintf """    "PlayerHasWoodSword": %b,""" m.PlayerHasWoodSword)
    lines.Add(sprintf """    "PlayerHasWoodArrow": %b,""" m.PlayerHasWoodArrow)
    lines.Add(sprintf """    "PlayerHasBlueRing": %b,""" m.PlayerHasBlueRing)
    lines.Add(sprintf """    "PlayerHasBlueCandle": %b,""" m.PlayerHasBlueCandle)
    lines.Add(sprintf """    "PlayerHasMagicalSword": %b,""" m.PlayerHasMagicalSword)
    lines.Add(sprintf """    "PlayerHasDefeatedGanon": %b,""" m.PlayerHasDefeatedGanon)
    lines.Add(sprintf """    "PlayerHasRescuedZelda": %b,""" m.PlayerHasRescuedZelda)
    lines.Add(sprintf """    "PlayerHasBombs": %b""" m.PlayerHasBombs)
    lines.Add("""},""")
    lines |> Seq.map (fun s -> prefix+s) |> Seq.toArray

let SaveStartingItemsAndExtras(prefix) =
    let m = StartingItemsAndExtrasModel.Create()
    let lines = ResizeArray()
    lines.Add(""""StartingItemsAndExtras": {""")
    lines.Add(sprintf """    "HDNStartingTriforcePieces": [ %b, %b, %b, %b, %b, %b, %b, %b ],""" m.HDNStartingTriforcePieces.[0] m.HDNStartingTriforcePieces.[1] m.HDNStartingTriforcePieces.[2]
                        m.HDNStartingTriforcePieces.[3] m.HDNStartingTriforcePieces.[4] m.HDNStartingTriforcePieces.[5] m.HDNStartingTriforcePieces.[6] m.HDNStartingTriforcePieces.[7])
    lines.Add(sprintf """    "PlayerHasWhiteSword": %b,""" m.PlayerHasWhiteSword)
    lines.Add(sprintf """    "PlayerHasMagicalSword": %b,""" m.PlayerHasMagicalSword)
    lines.Add(sprintf """    "PlayerHasSword4": %b,""" m.PlayerHasSword4)
    lines.Add(sprintf """    "PlayerHasSilverArrow": %b,""" m.PlayerHasSilverArrow)
    lines.Add(sprintf """    "PlayerHasBow": %b,""" m.PlayerHasBow)
    lines.Add(sprintf """    "PlayerHasWand": %b,""" m.PlayerHasWand)
    lines.Add(sprintf """    "PlayerHasRedCandle": %b,""" m.PlayerHasRedCandle)
    lines.Add(sprintf """    "PlayerHasBoomerang": %b,""" m.PlayerHasBoomerang)
    lines.Add(sprintf """    "PlayerHasMagicBoomerang": %b,""" m.PlayerHasMagicBoomerang)
    lines.Add(sprintf """    "PlayerHasRedRing": %b,""" m.PlayerHasRedRing)
    lines.Add(sprintf """    "PlayerHasPowerBracelet": %b,""" m.PlayerHasPowerBracelet)
    lines.Add(sprintf """    "PlayerHasLadder": %b,""" m.PlayerHasLadder)
    lines.Add(sprintf """    "PlayerHasRaft": %b,""" m.PlayerHasRaft)
    lines.Add(sprintf """    "PlayerHasRecorder": %b,""" m.PlayerHasRecorder)
    lines.Add(sprintf """    "PlayerHasAnyKey": %b,""" m.PlayerHasAnyKey)
    lines.Add(sprintf """    "PlayerHasBook": %b,""" m.PlayerHasBook)
    lines.Add(sprintf """    "PlayerHasMeat": %b,""" m.PlayerHasMeat)
    lines.Add(sprintf """    "PlayerHasLetter": %b,""" m.PlayerHasLetter)
    lines.Add(sprintf """    "MaxHeartsDifferential": %d,""" m.MaxHeartsDifferential)
    lines.Add("""},""")
    lines |> Seq.map (fun s -> prefix+s) |> Seq.toArray

let SaveBlockers(prefix) =
    let lines = ResizeArray()
    lines.Add(""""Blockers": [ [""")
    for i = 0 to 7 do
        for j = 0 to TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON-1 do
            let s = new System.Text.StringBuilder("    ")
            s.Append(TrackerModel.DungeonBlockersContainer.AsJsonString(i,j)) |> ignore
            if j < TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON-1 then
                s.Append(", ") |> ignore
            lines.Add(s.ToString())
        if i <> 7 then
            lines.Add("], [")
        else
            lines.Add("] ],")
    lines |> Seq.map (fun s -> prefix+s) |> Seq.toArray

let SaveHints(prefix) =
    let lines = ResizeArray()
    lines.Add(""""Hints": {""")
    let sb = new System.Text.StringBuilder("""    "LocationHints": [ """)
    for i = 0 to 10 do
        sb.Append(sprintf "%d%s " (TrackerModel.GetLevelHint(i).ToIndex()) (if i=10 then "" else ",")) |> ignore
    sb.Append("],") |> ignore
    lines.Add(sb.ToString())
    lines.Add(sprintf """    "NoFeatOfStrengthHint": %b,""" TrackerModel.NoFeatOfStrengthHintWasGiven)
    lines.Add(sprintf """    "SailNotHint": %b,""" TrackerModel.SailNotHintWasGiven)
    lines.Add(sprintf """    "SailToHint": %b,""" TrackerModel.SailToHintWasGiven)
    lines.Add(sprintf """    "PlayMelodyHint": %b,""" TrackerModel.PlayMelodyHintWasGiven)
    lines.Add(sprintf """    "StepOverWaterHint": %b,""" TrackerModel.StepOverWaterHintWasGiven)
    lines.Add(sprintf """    "FireArrowsHint": %b""" TrackerModel.FireArrowsHintWasGiven)
    lines.Add("""},""")
    lines |> Seq.map (fun s -> prefix+s) |> Seq.toArray

type SaveType =
    | ManualSave     // user clicked 'Save'
    | FinishedSave   // user clicked Zelda and SaveOnCompletion option is on
    | AutoSave       // each time a minute has passed

let mutable lastKnownSeed, lastKnownFlags = "", ""
/// The ZORA flag string pasted on the startup screen or read from a loaded save (empty = none)
let mutable lastKnownZoraFlags = ""
/// The ZORA release (such as "2.0.0") a loaded save was made for; only a save file carries it, so a paste leaves it empty
let mutable lastKnownZoraVersion = ""
let seedAndFlagsUpdated = new Event<_>()
let seedAndFlagsRegex = new System.Text.RegularExpressions.Regex("_(\d+)_([a-zA-Z0-9!]+)", System.Text.RegularExpressions.RegexOptions.None)
let MaybePollSeedAndFlags() =
    if TrackerModelOptions.SnoopSeedAndFlags.Value then
        let procs = (try System.Diagnostics.Process.GetProcesses() with _ -> [||])
        for p in procs do
            // (reading a window title can throw for system / exited processes: that must not stop the scan of the others)
            let title = (try p.MainWindowTitle with _ -> null)
            if not(System.String.IsNullOrEmpty(title)) then
                let m = seedAndFlagsRegex.Match(title)
                if m.Success then
                    let seed = m.Groups.[1].Value
                    let flags = m.Groups.[2].Value
                    if seed.Length > 6 && flags.Length > 6 then   // just a guess-filter
                        if seed <> lastKnownSeed || flags <> lastKnownFlags then
                            lastKnownSeed <- seed
                            lastKnownFlags <- flags
                            seedAndFlagsUpdated.Trigger()

/// Puts a loaded save's ZORA option choices back into the model. Older saves lack some of these (-1 / null): those keep whatever the start screen chose.
/// (Shared by both the WPF and Avalonia trackers, and by the selftest's save / load round trip.)
let ApplyLoadedOptions(ow:Overworld) =
    if not (isNull ow) then
        if ow.BootsReplaceBoomerangs >= 0 then
            TrackerModel.BootsReplaceBoomerangs <- (ow.BootsReplaceBoomerangs = 1)
            TrackerModel.BootsRandom <- (ow.BootsReplaceBoomerangs = 2)   // (Random, ?, counts as Off until confirmed)
        if ow.ProgressiveItems >= 0 then
            TrackerModel.ProgressiveItems <- (ow.ProgressiveItems = 1)
        if ow.ExtraBlocks >= 0 then
            OverworldData.ExtraPBBlocks <- (ow.ExtraBlocks &&& 1) <> 0
            OverworldData.ExtraRaftBlocks <- (ow.ExtraBlocks &&& 2) <> 0
            OverworldData.DeadWoodsEnabled <- (ow.ExtraBlocks &&& 4) <> 0
            OverworldData.LostHillsEnabled <- (ow.ExtraBlocks &&& 8) <> 0
            // which of them were Random (?) rather than plain On (older saves: none)
            let rnd = max 0 ow.ExtraBlocksRandom
            OverworldData.ExtraPBRandom <- (rnd &&& 1) <> 0
            OverworldData.ExtraRaftRandom <- (rnd &&& 2) <> 0
            OverworldData.DeadWoodsRandom <- (rnd &&& 4) <> 0
            OverworldData.LostHillsRandom <- (rnd &&& 8) <> 0
        if not (isNull ow.DeadWoodsHint) then
            OverworldData.DeadWoodsHint <- ow.DeadWoodsHint
            OverworldData.DeadWoodsHintKnown <- (ow.DeadWoodsHint <> "")
        if not (isNull ow.LostHillsHint) then
            OverworldData.LostHillsHint <- ow.LostHillsHint
            OverworldData.LostHillsHintKnown <- (ow.LostHillsHint <> "")

/// Whether the 4th Sword start option was on in a loaded save: the level 2 staircase slot or the level 9 floor check is only enabled by it (older saves predate the option: nothing is restricted)
let FourthSwordWasOn(items:Items) =
    if isNull items.Level2StairBox then true else (items.Level2StairBoxEnabled || items.Level9FloorBoxEnabled)

/// The save file's own format number, separate from the app version: bump it only when the meaning of saved ids or fields changes (new fields alone do not need a bump).
/// 0 = absent: ZTracker 1.x and ZORATracker saves written before this field existed; they use the same ids.
let SaveFormatVersion = 1
/// What to warn about before loading a save (None = load without asking): compares the save's format number, not the app version, so an ordinary upgrade does not warn
let LoadWarning(savedAppVersion:string, savedFormatVersion:int) : string option =
    let v = if isNull savedAppVersion then "" else savedAppVersion
    if savedFormatVersion > SaveFormatVersion then
        Some (sprintf "This save was made by a newer ZORATracker (version '%s').\nSome of it may not load" v)
    elif savedFormatVersion >= 1 then None
    elif v.StartsWith("2.") then None   // an older ZORATracker save: same ids, the newer fields just keep the start screen's choice
    elif v.StartsWith("1.2") || v.StartsWith("1.3") then Some (sprintf "This save was made by ZTracker version '%s'.\nLoading it might not work" v)
    else Some (sprintf "This save was made by version '%s'.\nLoading it will probably fail" v)

let mutable pendingManualSaveName = ""   // set by the Save button's name popup just before a manual save; used once, then cleared (blank = date-time name)
/// The lines of a save file for the current state (SaveAll writes them to a file; the selftest reads them back without touching the Saves folder)
let BuildSaveLines(notesText:string, selectedDungeonTab:int, dungeonModelsJsonLines:string[], drawingLayerJsonLines:string[],
                    alternativeOverworldMapFilename, shouldInitiallyHideOverworldMap:bool, currentRecorderDestinationIndex, isBoomstickSeed, isAtlasSeed) : string[] =
    let totalSeconds = int (System.DateTime.Now - TrackerModel.theStartTime.Time).TotalSeconds
    let lines = [|
        yield sprintf """{"""
        yield sprintf """    "Version": "%s",""" OverworldData.VersionString
        yield sprintf """    "SaveFormatVersion": %d,""" SaveFormatVersion
        yield sprintf """    "TimeInSeconds": %d,""" totalSeconds
        yield! SaveOverworld("    ")
        yield! SaveItems("    ")
        yield! SavePlayerProgressAndTakeAnyHearts("    ")
        yield! SaveStartingItemsAndExtras("    ")
        yield! SaveBlockers("    ")
        yield! SaveHints("    ")
        yield sprintf """    "Notes": %s,""" (System.Text.Json.JsonSerializer.Serialize notesText)
        yield sprintf """    "CurrentRecorderDestinationIndex": %d,""" currentRecorderDestinationIndex
        yield sprintf """    "RecorderToNewDungeons": %b,""" TrackerModel.recorderToNewDungeons
        yield sprintf """    "RecorderToUnbeatenDungeons": %b,""" TrackerModel.recorderToUnbeatenDungeons
        yield sprintf """    "IsBoomstickSeed": %b,""" isBoomstickSeed
        yield sprintf """    "IsAtlasSeed": %b,""" isAtlasSeed
        yield sprintf """    "IsWSMSReplacedByBUSeed": %b,""" (TrackerModel.IsWSMSReplacedByBU())
        yield sprintf """    "DungeonTabSelected": %d,""" selectedDungeonTab
        yield sprintf """    "DungeonMaps": [ {"""
        yield! dungeonModelsJsonLines |> Array.map (fun s -> "    "+s)
        yield sprintf """    ],"""
        if theUserCustomChecklist <> null then
            yield sprintf """    "UserCustomChecklist": {"""
            yield sprintf """        "Name": %s,""" (System.Text.Json.JsonSerializer.Serialize<string>(if isNull theUserCustomChecklist.Name then "" else theUserCustomChecklist.Name))
            yield sprintf """        "BackgroundImageFilename": %s,""" (System.Text.Json.JsonSerializer.Serialize<string>(if isNull theUserCustomChecklist.BackgroundImageFilename then "" else theUserCustomChecklist.BackgroundImageFilename))
            yield sprintf """        "Items": ["""
            for i=0 to theUserCustomChecklist.Items.Length-1 do
                yield sprintf """            %s%s""" (System.Text.Json.JsonSerializer.Serialize<UserCustomCheckbox>(theUserCustomChecklist.Items.[i])) 
                                                        (if i=theUserCustomChecklist.Items.Length-1 then "" else ",")
            yield sprintf """    ] },"""
        yield sprintf """    "DrawingLayerIcons": ["""
        yield! drawingLayerJsonLines
        yield sprintf """    ],"""
        yield sprintf """    "AlternativeOverworldMapFilename": %s,""" (System.Text.Json.JsonSerializer.Serialize<string>(alternativeOverworldMapFilename))
        yield sprintf """    "ShouldInitiallyHideOverworldMap": %s,""" (shouldInitiallyHideOverworldMap.ToString().ToLowerInvariant())
        if lastKnownSeed <> "" then
            yield sprintf """    "Seed": "%s",""" lastKnownSeed
        if lastKnownFlags <> "" then
            yield sprintf """    "Flags": "%s",""" lastKnownFlags
        if lastKnownZoraVersion <> "" then
            yield sprintf """    "ZoraVersion": "%s",""" lastKnownZoraVersion
        if lastKnownZoraFlags <> "" then
            yield sprintf """    "ZoraFlags": "%s",""" lastKnownZoraFlags
        // write the timeline 'pretty' at the bottom of the file, for people who want to easily see/parse splits
        let flat = [| for x,y in TrackerModel.timelineDataOverworldSpotsRemain do 
                        yield x
                        yield y |]
        yield sprintf """    "OverworldSpotsRemainingOverTime": %s,""" (System.Text.Json.JsonSerializer.Serialize<int[]>(flat))
        yield sprintf """    "Timeline": ["""
        let tis = [|for KeyValue(_,ti) in TrackerModel.TimelineItemModel.All do yield ti |] |> Array.sortBy (fun ti -> ti.FinishedTotalSeconds)
        for i=0 to tis.Length-1 do
            let ti = tis.[i]
            yield sprintf """        { "Ident": "%-20s, "Seconds": %6d, "Has": %d }%s""" (ti.Identifier+"\"") (ti.FinishedTotalSeconds) (ti.Has.AsInt()) (if i=tis.Length-1 then "" else ",")
        yield sprintf """    ]"""
        yield sprintf """}"""
        |]
    lines

let SaveAll(notesText:string, selectedDungeonTab:int, dungeonModelsJsonLines:string[], drawingLayerJsonLines:string[],
                alternativeOverworldMapFilename, shouldInitiallyHideOverworldMap:bool, currentRecorderDestinationIndex, isBoomstickSeed, isAtlasSeed, saveType) =  // can throw
    MaybePollSeedAndFlags()
    let lines = BuildSaveLines(notesText, selectedDungeonTab, dungeonModelsJsonLines, drawingLayerJsonLines, alternativeOverworldMapFilename, shouldInitiallyHideOverworldMap, currentRecorderDestinationIndex, isBoomstickSeed, isAtlasSeed)
    let filename =
        match saveType with
        | ManualSave ->
            let invalid = System.IO.Path.GetInvalidFileNameChars()
            let typed = (System.String(pendingManualSaveName |> Seq.filter (fun c -> not (Array.contains c invalid)) |> Seq.toArray)).Trim()
            pendingManualSaveName <- ""
            let typed = if typed.ToLowerInvariant().EndsWith(".json") then typed.Substring(0, typed.Length-5) else typed
            let typed = typed.Trim().TrimEnd('.')
            if typed = "" then
                System.IO.Path.Combine(SavesDirectory, "zt-save-manual-" + System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".json")
            else
                // the file is just <name>.json; if that name is already taken, add -2, -3, ... rather than overwrite
                let mutable p = System.IO.Path.Combine(SavesDirectory, typed + ".json")
                let mutable n = 2
                while System.IO.File.Exists(p) do
                    p <- System.IO.Path.Combine(SavesDirectory, sprintf "%s-%d.json" typed n)
                    n <- n + 1
                p
        | FinishedSave -> System.IO.Path.Combine(SavesDirectory, "zt-save-completed-" + System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".json")
        | AutoSave -> AutoSaveFilename
    //let filename = "J:\\-impossiblesdkgfjhsdg;kdahfskjgfdhsgfh;lahjds;ljfdhs;ljfhldashfldashlfadshgflhjdgflajdgfjkl"  // test errors
    EnsureSavesDirectory()
    // (write a temp file and swap it in, so a crash mid-write cannot cut the only autosave short)
    let tmp = filename + ".tmp"
    System.IO.File.WriteAllLines(tmp, lines)
    if System.IO.File.Exists(filename) then System.IO.File.Replace(tmp, filename, null) else System.IO.File.Move(tmp, filename)
    match saveType with
    | AutoSave -> System.IO.File.SetCreationTime(filename, System.DateTime.Now)
    | _ -> ()
    filename
    