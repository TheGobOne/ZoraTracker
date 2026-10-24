module TrackerModel

// model to track the state of the tracker independent of any UI/graphics layer

[<RequireQualifiedAccess>]
type ReminderCategory =
    | DungeonFeedback
    | SwordHearts
    | CoastItem
    | RecorderPBSpotsAndBoomstickBook  // legacy, kept for save-compat; logic now uses individual below
    | HaveKeyLadder                    // legacy, kept for save-compat; logic now uses individual below
    | Blockers
    | DoorRepair
    | OverworldOverwrites
    | Asterisk
    | RecorderSpots
    | PowerBraceletSpots
    | BoomstickBook
    | HaveMagicKey
    | HaveLadder
    member this.DisplayName =
        match this with
        | DungeonFeedback -> "Dungeon Feedback"
        | SwordHearts -> "Sword Hearts"
        | CoastItem -> "Coast Item"
        | RecorderPBSpotsAndBoomstickBook -> "Recorder/PB/Boomstick"
        | HaveKeyLadder -> "Have Magic Key/Ladder"
        | Blockers -> "Blockers"
        | DoorRepair -> "Door Repair Count"
        | OverworldOverwrites -> "Overworld Overwrites"
        | Asterisk -> "Error beeps"
        | RecorderSpots -> "Recorder Spots"
        | PowerBraceletSpots -> "Power Bracelet Spots"
        | BoomstickBook -> "Boomstick Book"
        | HaveMagicKey -> "Have Magic Key"
        | HaveLadder -> "Have Ladder"

///////////////////////////////////////////////////////////////////////////

let mutable IsSecondQuestDungeons = false
let mutable ProgressiveItems = false   // ZORA Extras: collecting a base item (blue candle / blue ring / wood arrow / sword) upgrades the rest of that item marked on the map to the next level
let mutable MarkBombDroppers = false   // Startup Options checkbox: middle-clicking an overworld tile marks it as a bomb dropper screen
let mutable BootsReplaceBoomerangs = false   // ZORA Extras startup option (behavior still to be decided)
let mutable CustomLevelName = ""   // up to 7 chars (uppercase); if non-empty, replaces 'LEVEL-n' in each dungeon map header
let mutable MirrorOverworld = false

let GetOldManCount(i) =
    if IsSecondQuestDungeons then
        DungeonData.oldManCounts2Q.[i]
    else
        DungeonData.oldManCounts1Q.[i]

///////////////////////////////////////////////////////////////////////////

// abstraction for a set of scrollable choices
[<AllowNullLiteral>]
type ChoiceDomain(name:string,maxUsesArray:int[]) =
    // index keys are integers used as identifiers, values are max number of times it can appear among a set of cells
    // examples: ladder has max use of 1, heart container has max use of 9 in heartShuffle mode, bomb shop has max use of 999 (infinity)
    let name = name
    let maxUsesArray = Array.copy maxUsesArray
    let uses = Array.zeroCreate maxUsesArray.Length
    let ev = new Event<_>()
    [<CLIEvent>]
    member _this.Changed = ev.Publish
    member _this.Name = name  // just useful for debugging etc
    member _this.MaxKey = uses.Length-1
    member _this.SetMaxUse(key:int, n:int) = maxUsesArray.[key] <- n   // lets the owner enable/disable a key at runtime (e.g. user-supplied custom icons)
    member this.Renotify(key) = if key <> -1 then ev.Trigger(this,key)   // re-announce a key after the owning Cell has settled on its new state
    member this.RemoveUse(key) =
        if key = -1 then  // since Next/Prev FreeKey can return -1 to mean the implicit empty slot, seems ok to RemoveUse() it, it implicitly has infinity current uses
            ev.Trigger(this,key)
        else
            if uses.[key] > 0 then
                uses.[key] <- uses.[key] - 1
                ev.Trigger(this,key)
            else
                failwith "choice domain underflow"
    member this.AddUse(key) =
        if key = -1 then  // since Next/Prev FreeKey can return -1 to mean the implicit empty slot, seems ok to AddUse() it, it implicitly has infinity max uses
            ev.Trigger(this,key)
        else
            if uses.[key] >= maxUsesArray.[key] then
                failwith "choice domain overflow"
            else
                uses.[key] <- uses.[key] + 1
                ev.Trigger(this,key)
    member this.NextFreeKeyWithAllowance(key, allowance) =
        if key = uses.Length-1 then
            -1
        elif (allowance = key+1) || (uses.[key+1] < maxUsesArray.[key+1]) then
            key+1
        else
            this.NextFreeKeyWithAllowance(key+1, allowance)
    member this.NextFreeKey(key) = this.NextFreeKeyWithAllowance(key, -999)  
    member this.PrevFreeKeyWithAllowance(key, allowance) =
        if key = -1 then
            this.PrevFreeKeyWithAllowance(uses.Length, allowance)
        elif key = 0 then
            -1
        elif (allowance = key-1) || (uses.[key-1] < maxUsesArray.[key-1]) then
            key-1
        else
            this.PrevFreeKeyWithAllowance(key-1, allowance)
    member this.PrevFreeKey(key) = this.PrevFreeKeyWithAllowance(key, -999)  
    member _this.NumUses(key) = uses.[key]
    member _this.MaxUses(key) = maxUsesArray.[key]
    member _this.CanAddUse(key) = (key = -1) || (uses.[key] < maxUsesArray.[key])
        
type Cell(cd: ChoiceDomain) =
    let mutable state = -1 // -1 means empty, 0-N are item identifiers
    let changedEvent = new Event<unit>()

    [<CLIEvent>]
    member _.Changed = changedEvent.Publish

    member _this.Current() = state

    member this.Next() =
        if state <> -1 then
            cd.RemoveUse(state)
        let newState = cd.NextFreeKey(state)
        if newState <> state then
            state <- newState
            if state <> -1 then cd.AddUse(state)
            changedEvent.Trigger()

    member this.Prev() =
        if state <> -1 then
            cd.RemoveUse(state)
        let newState = cd.PrevFreeKey(state)
        if newState <> state then
            state <- newState
            if state <> -1 then cd.AddUse(state)
            changedEvent.Trigger()

    member this.Set(newState: int) =
        if newState < -1 || newState > cd.MaxKey then
            failwith "Cell.Set out of range"
        if state <> newState then
            let oldState = state
            cd.AddUse(newState)
            cd.RemoveUse(state)
            state <- newState
            // the use-count events above fire before this cell holds its new state; announce both keys again now that it does,
            // so listeners that look at the map (e.g. a dungeon's found/located display) see the cleared or newly marked tile
            cd.Renotify(oldState)
            cd.Renotify(newState)
            changedEvent.Trigger()

    member this.AttemptToSet(newState: int) =
        try
            this.Set(newState)
            true
        with _ -> 
            false


//////////////////////////////////////////////////////////////////////////////////////////

type IEventingReader<'T> =
    abstract Value : 'T with get
    abstract Changed : IEvent<unit>
type Eventing<'T when 'T:equality>(orig:'T) =
    let mutable state = orig
    let e = new Event<unit>()
    member this.Value with get() = state and set(x) = let orig=state in state <- x; if orig<>state then e.Trigger()
    member this.Changed = e.Publish
    interface IEventingReader<'T> with
        member this.Value with get() = this.Value
        member this.Changed = this.Changed
type EventingBool = Eventing<bool>
type EventingInt = Eventing<int>
type SyntheticEventingBool(recompute, changesToWatch:seq<IEvent<unit>>) =
    let b = new EventingBool(recompute())
    do
        for e in changesToWatch do
            e.Add(fun _ -> b.Value <- recompute())
    member this.Value = b.Value
    member this.Changed = b.Changed
    interface IEventingReader<bool> with
        member this.Value with get() = this.Value
        member this.Changed = this.Changed
let FALSE = new EventingBool(false)

let IsCurrentlyBook, ToggleIsCurrentlyBook, IsCurrentlyBookChanged = 
    let mutable isCurrentlyBook = true   // false if this is a boomstick seed
    let isCurrentlyBookChangeEvent = new Event<_>()
    let IsCurrentlyBook() = isCurrentlyBook
    let ToggleIsCurrentlyBook() = isCurrentlyBook <- not isCurrentlyBook; isCurrentlyBookChangeEvent.Trigger(isCurrentlyBook)
    let IsCurrentlyBookChanged = isCurrentlyBookChangeEvent.Publish
    IsCurrentlyBook, ToggleIsCurrentlyBook, IsCurrentlyBookChanged
let IsBookAnAtlas, ToggleBookIsAtlas, IsBookAnAtlasChanged = 
    let mutable isBookAnAtlas = false
    let isBookAnAtlasChangedEvent = new Event<_>()
    let IsBookAnAtlas() = isBookAnAtlas
    let ToggleBookIsAtlas() = isBookAnAtlas <- not isBookAnAtlas; isBookAnAtlasChangedEvent.Trigger(isBookAnAtlas)
    let IsBookAnAtlasChanged = isBookAnAtlasChangedEvent.Publish
    IsBookAnAtlas, ToggleBookIsAtlas, IsBookAnAtlasChanged
let IsWSMSReplacedByBU, ToggleWSMSReplacedByBU, IsWSMSReplacedByBUChanged = 
    let mutable isWSMSReplacedByBU = false
    let isWSMSReplacedByBUChangedEvent = new Event<_>()
    let IsWSMSReplacedByBU() = isWSMSReplacedByBU
    let ToggleWSMSReplacedByBU() = isWSMSReplacedByBU <- not isWSMSReplacedByBU; isWSMSReplacedByBUChangedEvent.Trigger(isWSMSReplacedByBU)
    let IsWSMSReplacedByBUChanged = isWSMSReplacedByBUChangedEvent.Publish
    IsWSMSReplacedByBU, ToggleWSMSReplacedByBU, IsWSMSReplacedByBUChanged
let playerComputedStateSummaryForPlayerHasBookChanged = new Event<unit>()

module ITEMS =
    let itemNamesAndCounts = [|
        "BookOrShield"      , 1
        "Boomerang"         , 1
        "Bow"               , 1
        "PowerBracelet"     , 1
        "Ladder"            , 1
        "MagicBoomerang"    , 1
        "AnyKey"            , 1
        "Raft"              , 1
        "Recorder"          , 1
        "RedCandle"         , 1
        "RedRing"           , 1
        "SilverArrow"       , 1
        "Wand"              , 1
        "WhiteSword"        , 1
        "HeartContainer"    , 9
        // extra marker items (added after the originals so existing saves stay valid); counts are how many can be marked at once
        "BluePotion"        , 1
        "RedPotion"         , 1
        "Letter"            , 1
        "ProgressiveSword"  , 3
        "WoodSword" , 1
        "MagicalSword" , 1
        "WhiteSword4" , 1
        "BlueRing" , 1
        "WoodArrow" , 1
        "BlueCandle" , 1
        "Shield" , 1
        "Meat" , 1
        "OldKey" , 1
        "BombUpgrade" , 1
        "TriforcePiece" , 9   // a triforce piece in a dungeon item box also marks that dungeon's triforce as collected
        // 16 slots for the user's ExtraIcons-folder icons (ids 30-45); the domain only allows them once the "add custom icons to menus" option is on
        "CustomIcon1" , 27
        "CustomIcon2" , 27
        "CustomIcon3" , 27
        "CustomIcon4" , 27
        "CustomIcon5" , 27
        "CustomIcon6" , 27
        "CustomIcon7" , 27
        "CustomIcon8" , 27
        "CustomIcon9" , 27
        "CustomIcon10" , 27
        "CustomIcon11" , 27
        "CustomIcon12" , 27
        "CustomIcon13" , 27
        "CustomIcon14" , 27
        "CustomIcon15" , 27
        "CustomIcon16" , 27
        |]
    let NumBuiltIn = 30
    let CUSTOM_BASE = 30
    let CUSTOM_COUNT = 16
    let customNames : string[] = Array.init CUSTOM_COUNT (fun i -> sprintf "Custom icon %d" (i+1))
    let mutable customMenuCount = 0   // how many custom icons the menus currently offer (0 when the option is off)
    let BOOKSHIELD = 0
    let BOOMERANG = 1
    let BOW = 2
    let POWERBRACELET = 3
    let LADDER = 4
    let MAGICBOOMERANG = 5
    let KEY = 6
    let RAFT = 7
    let RECORDER = 8
    let REDCANDLE = 9
    let REDRING = 10
    let SILVERARROW = 11
    let WAND = 12
    let WHITESWORD = 13
    let HEARTCONTAINER = 14
    let BLUEPOTION = 15
    let REDPOTION = 16
    let LETTER = 17
    let PROGRESSIVESWORD = 18   // wood sword with a green up-arrow: a progressive sword upgrade
    let WOODSWORD = 19
    let MAGICALSWORD = 20
    let WHITESWORD4 = 21
    let BLUERING = 22
    let WOODARROW = 23
    let BLUECANDLE = 24
    let SHIELD = 25
    let MEAT = 26
    let OLDKEY = 27
    let BOMBUPGRADE = 28
    let TRIFORCE = 29
    let PriorityOrderForRemainingDisplay = [| BOW; SILVERARROW; LADDER; RECORDER; POWERBRACELET; RAFT; WAND; REDRING; KEY; WHITESWORD; BOOKSHIELD; REDCANDLE; MAGICBOOMERANG; BOOMERANG |]
    let AsPronounceString(n) =
        match n with
        | 0 -> if IsCurrentlyBook() then "book" else "shield"
        | 1 -> "boomerang"
        | 2 -> "beau"
        | 3 -> "power bracelet"
        | 4 -> "ladder"
        | 5 -> "magic boomerang"
        | 6 -> "magic key"
        | 7 -> "raft"
        | 8 -> "recorder"
        | 9 -> "red candle"
        | 10 -> "red ring"
        | 11 -> "silver arrow"
        | 12 -> "wand"
        | 13 -> "white sword"
        | 14 -> "heart container"
        | 15 -> "potion"
        | 16 -> "red potion"
        | 17 -> "letter"
        | 18 -> "progressive sword"
        | 19 -> "wood sword"
        | 20 -> "magical sword"
        | 21 -> "fourth sword"
        | 22 -> "blue ring"
        | 23 -> "wooden arrow"
        | 24 -> "blue candle"
        | 25 -> "magical shield"
        | 26 -> "meat"
        | 27 -> "key"
        | 28 -> "bomb upgrade"
        | 29 -> "triforce"
        | n when n >= CUSTOM_BASE && n < CUSTOM_BASE + CUSTOM_COUNT -> customNames.[n - CUSTOM_BASE]
        | _ -> failwith "bad ITEMS id"
    let AsHotKeyName(n) =
        if n>=0 && n<itemNamesAndCounts.Length then
            fst(itemNamesAndCounts.[n])
        else
            failwith "bad ITEMS id"
    let AsDisplayDescription(n) =
        match n with
        | 0 -> if IsCurrentlyBook() then "Book" else "Magical Shield"
        | 1 -> "Wooden Boomerang"
        | 2 -> "Bow"
        | 3 -> "Power Bracelet"
        | 4 -> "Ladder"
        | 5 -> "Magic Boomerang"
        | 6 -> "Magic Key"
        | 7 -> "Raft"
        | 8 -> "Recorder"
        | 9 -> "Red Candle"
        | 10 -> "Red Ring"
        | 11 -> "Silver Arrow"
        | 12 -> "Wand"
        | 13 -> "White Sword"
        | 14 -> "Heart Container"
        | 15 -> "Potion"
        | 16 -> "Red Potion"
        | 17 -> "Letter"
        | 18 -> "Progressive Sword"
        | 19 -> "Wood Sword"
        | 20 -> "Magical Sword"
        | 21 -> "4th Sword"
        | 22 -> "Blue Ring"
        | 23 -> "Wooden Arrow"
        | 24 -> "Blue Candle"
        | 25 -> "Magical Shield"
        | 26 -> "Meat"
        | 27 -> "Key"
        | 28 -> "Bomb Upgrade"
        | 29 -> "Triforce Piece"
        | n when n >= CUSTOM_BASE && n < CUSTOM_BASE + CUSTOM_COUNT -> customNames.[n - CUSTOM_BASE]
        | _ -> failwith "bad ITEMS id"
let allItemWithHeartShuffleChoiceDomain = ChoiceDomain("allItemsWithHeartShuffle", ITEMS.itemNamesAndCounts |> Array.map snd)

//////////////////////////////////////////////////////////////////////////////////////////

let spokenDungeon(level, isHDN) = if level<>9 && isHDN then sprintf "Dungeon %c" (char (int 'A' - 1 + level)) else sprintf "Dungeon %d" level
let overworldTiles(isFirstQuestOverworld, isHDN) = [|
    // hotkey name       maxuses                                              spoken name,            popup display text
    "Level1"           , 1                                                  , spokenDungeon(1,isHDN), "Dungeon"
    "Level2"           , 1                                                  , spokenDungeon(2,isHDN), "Dungeon"
    "Level3"           , 1                                                  , spokenDungeon(3,isHDN), "Dungeon"
    "Level4"           , 1                                                  , spokenDungeon(4,isHDN), "Dungeon"
    "Level5"           , 1                                                  , spokenDungeon(5,isHDN), "Dungeon"
    "Level6"           , 1                                                  , spokenDungeon(6,isHDN), "Dungeon"
    "Level7"           , 1                                                  , spokenDungeon(7,isHDN), "Dungeon"
    "Level8"           , 1                                                  , spokenDungeon(8,isHDN), "Dungeon"
    "Level9"           , 1                                                  , spokenDungeon(9,isHDN), "Final Dungeon"
    "AnyRoad1"         , 1                                                  , "Any Road 1",           "Any Road 1/4"
    "AnyRoad2"         , 1                                                  , "Any Road 2",           "Any Road 2/4"
    "AnyRoad3"         , 1                                                  , "Any Road 3",           "Any Road 3/4"
    "AnyRoad4"         , 1                                                  , "Any Road 4",           "Any Road 4/4"
    "Sword3"           , 2                                                  , "Magical Sword Cave",   "Magical Sword Cave\n(10-14 hearts to lift)"
    "Sword2"           , 2                                                  , "White Sword Cave",     "'White Sword' Cave\n(4-6 hearts to lift)\nNote: might have\nany item, not just\nwhite sword"
    "Sword1"           , 2                                                  , "Wood Sword Cave",      "Wood Sword Cave\n(can always lift)"
    // 1Q has 12 shops, distributed 4,4,3,1                                
    // 2Q has 15 shops, distributed 6,4,4,1     (4 kinds of shops)         
    "ArrowShop"        , 999                                                , "Wood Arrow shop",      "Shop with\nWood Arrows\n(60-100 rupees)"
    "BombShop"         , 999                                                , "Bomb shop",            "Shop with\n4 Bomb Pack\n(1-40 rupees)"
    "BookShop"         , 999                                                , "Book shop",            "Shop with\n(Boomstick) Book\n(180-220 rupees)"
    "CandleShop"       , 999                                                , "Candle shop",          "Shop with\nBlue Candle\n(40-80 rupees)"
    "BlueRingShop"     , 999                                                , "Blue ring shop",       "Shop with\nBlue Ring\n(230-255 rupees)"
    "MeatShop"         , 999                                                , "Meat shop",            "Shop with\nMeat\n(40-120 rupees)"
    "KeyShop"          , 999                                                , "Key shop",             "Shop with\nKey\n(60-120 rupees)"
    "ShieldShop"       , 999                                                , "Shield shop",          "Shop with\nMagical Shield\n(70-180 rupees)"
    "UnknownSecret"    , 999                                                , "Unknown Secret",       "Unknown Secret"
    "LargeSecret"      , (if isFirstQuestOverworld then 3 else 1)           , "Large Secret",         "Large Secret\n(50-150 rupees)"
    "MediumSecret"     , (if isFirstQuestOverworld then 7 else 7)           , "Medium Secret",        "Medium Secret\n(25-40 rupees)"
    "SmallSecret"      , (if isFirstQuestOverworld then 4 else 6)           , "Small Secret",         "Small Secret\n(1-20 rupees)"
    "DoorRepairCharge" , (if isFirstQuestOverworld then 9 else 10)          , "Door Repair",          "Door Repair Charge\n(15-25 rupees)"
    "MoneyMakingGame"  , (if isFirstQuestOverworld then 5 else 6)           , "Money Making Game",    "Money Making Game\n(gambling)"
    "Letter"           , 1                                                  , "The Letter",           "The Letter\n(for buying potions)"
    "Armos"            , 1                                                  , "Armos",                "Armos Item"
    // white/magical sword cave hint may also be marked as                 
    // a free hint 'shop', so 4 rather than 2                              
    "HintShop"         , 99                                                 , "Hint Shop",            "Hints Shop\nScroll icon on the map\nfor different colors"
    "TakeAny"          , 4                                                  , "Take Any",             "Take Any One\nYou Want"
    "Shop"             , (if isFirstQuestOverworld then 7 else 9)           , "Potion Shop",          "Potion Shop\n(20-60, 48-88 rupees)"
    "DarkX"            , 999                                                , "Don't Care",           "Don't Care"
    "PersonalPref1"    , 999                                                , "Personal Pref 1",      "Personal Preference 1\n(custom marker)"
    "PersonalPref2"    , 999                                                , "Personal Pref 2",      "Personal Preference 2\n(custom marker)"
    |]
// 1Q has 73 total spots, 2Q has 80, Mixed has 93
let MaxRemain1Q = 73
let MaxRemain2Q = 80
let MaxRemainMQ = 93
let MaxRemainUQ = 128
let dummyOverworldTiles = overworldTiles(true,false)  // some bits need to read the hotkey names or array length, before the 1Q/2Q choice has been made by the user, this gives them that info

let mutable mapSquareChoiceDomain = null : ChoiceDomain
let mapSquareChangedStable = new Event<ChoiceDomain*int>()   // forwards Changed from whichever ChoiceDomain is current (reinitializeOverworld replaces the domain)
// Note: if you make changes to above/below, also check: recomputeMapStateSummary(), Graphics.theInteriorBmpTable, SpeechRecognition, OverworldMapTileCustomization, ui's isLegalHere()
type MapSquareChoiceDomainHelper = 
    static member DUNGEON_1 = 0
    static member DUNGEON_2 = 1
    static member DUNGEON_3 = 2
    static member DUNGEON_4 = 3
    static member DUNGEON_5 = 4
    static member DUNGEON_6 = 5
    static member DUNGEON_7 = 6
    static member DUNGEON_8 = 7
    static member DUNGEON_9 = 8
    static member WARP_1 = 9
    static member WARP_2 = 10
    static member WARP_3 = 11
    static member WARP_4 = 12
    static member SWORD3 = 13
    static member SWORD2 = 14
    static member SWORD1 = 15
    // item shop stuff
    static member ARROW = 16
    static member BOMB = 17
    static member BOOK = 18
    static member BLUE_CANDLE = 19
    static member BLUE_RING = 20
    static member MEAT = 21
    static member KEY = 22
    static member SHIELD = 23
    static member SHOP = MapSquareChoiceDomainHelper.ARROW  // key into extra-data store for all shops
    static member NUM_ITEMS = 8 // 8 possible types of items can be tracked, listed above
    static member IsItem(state) = state >= 16 && state <= 23
    static member ToItem(state) = if MapSquareChoiceDomainHelper.IsItem(state) then state-15 else 0   // format used by TrackerModel.overworldMapExtraData
    // other stuff
    static member UNKNOWN_SECRET = 24
    static member LARGE_SECRET = 25
    static member MEDIUM_SECRET = 26
    static member SMALL_SECRET = 27
    static member DOOR_REPAIR_CHARGE = 28
    static member MONEY_MAKING_GAME = 29
    static member THE_LETTER = 30
    static member ARMOS = 31
    static member HINT_SHOP = 32
    static member TAKE_ANY = 33
    static member POTION_SHOP = 34
    static member DARK_X = 35
    static member PERSONAL_PREF_1 = 36
    static member PERSONAL_PREF_2 = 37
    static member AsHotKeyName(n) =
        if n>=0 && n<dummyOverworldTiles.Length then
            let r,_,_,_ = dummyOverworldTiles.[n] in r
        else
            failwith "bad overworld tile id"
    static member AsTrackerModelOptionsOverworldTilesToHide(n) =
        if n = MapSquareChoiceDomainHelper.SWORD3 then
            TrackerModelOptions.OverworldTilesToHide.Sword3
        elif n = MapSquareChoiceDomainHelper.SWORD2 then
            TrackerModelOptions.OverworldTilesToHide.Sword2
        elif n = MapSquareChoiceDomainHelper.SWORD1 then
            TrackerModelOptions.OverworldTilesToHide.Sword1
        elif n = MapSquareChoiceDomainHelper.LARGE_SECRET then
            TrackerModelOptions.OverworldTilesToHide.LargeSecret
        elif n = MapSquareChoiceDomainHelper.MEDIUM_SECRET then
            TrackerModelOptions.OverworldTilesToHide.MediumSecret
        elif n = MapSquareChoiceDomainHelper.SMALL_SECRET then
            TrackerModelOptions.OverworldTilesToHide.SmallSecret
        elif n = MapSquareChoiceDomainHelper.DOOR_REPAIR_CHARGE then
            TrackerModelOptions.OverworldTilesToHide.DoorRepair
        elif n = MapSquareChoiceDomainHelper.MONEY_MAKING_GAME then
            TrackerModelOptions.OverworldTilesToHide.MoneyMakingGame
        elif n = MapSquareChoiceDomainHelper.THE_LETTER then
            TrackerModelOptions.OverworldTilesToHide.TheLetter
        elif n = MapSquareChoiceDomainHelper.ARMOS then
            TrackerModelOptions.OverworldTilesToHide.Armos
        elif n = MapSquareChoiceDomainHelper.HINT_SHOP then
            TrackerModelOptions.OverworldTilesToHide.HintShop
        elif n = MapSquareChoiceDomainHelper.TAKE_ANY then
            TrackerModelOptions.OverworldTilesToHide.TakeAny
        elif n = MapSquareChoiceDomainHelper.SHOP then
            TrackerModelOptions.OverworldTilesToHide.Shop
        else 
            failwith "bad AsTrackerModelOptionsOverworldTilesToHide"


//////////////////////////////////////////////////////////////////////////////////////////

// not threadsafe
type LastChangedTime(intervalHowFarInThePast) as this =
    static let mutable pause = None
    static let allInstances = ResizeArray()
    let mutable stamp = System.DateTime.Now - intervalHowFarInThePast
    let mutable isFuture = None  // if Some(interval), interval is how far in past upon waking
    do
        allInstances.Add(this)
    static member private CurrentPause = pause
    static member IsPaused = pause.IsSome
    static member PauseAll() = 
        match pause with
        | Some _ -> ()
        | None -> pause <- Some(System.DateTime.Now)
    static member ResumeAll() =
        match pause with
        | None -> ()
        | Some whenPaused ->
            let now = System.DateTime.Now
            let interval = now - whenPaused
            for lct in allInstances do
                lct.AddTime(interval, now)
            pause <- None
    new() = LastChangedTime(System.TimeSpan.Zero)
    member this.SetNow() = 
        stamp <- System.DateTime.Now
        match LastChangedTime.CurrentPause with
        | None -> ()
        | Some _ -> isFuture <- Some(System.TimeSpan.Zero)
    member this.SetAgo(interval) = 
        stamp <- System.DateTime.Now - interval
        match LastChangedTime.CurrentPause with
        | None -> ()
        | Some _ -> isFuture <- Some(interval)
    member private this.AddTime(interval, now) =
        match isFuture with
        | None -> stamp <- stamp + interval
        | Some(howFarToGoBack) ->
            stamp <- now - howFarToGoBack
            isFuture <- None
    member this.Time = 
        match LastChangedTime.CurrentPause with
        | None -> stamp
        | Some whenPaused -> 
            match isFuture with
            | Some(interval) -> System.DateTime.Now - interval
            | None -> stamp + (System.DateTime.Now - whenPaused)
    member this.SetNowIfLocal(isRemote: bool) =
        if not isRemote then this.SetNow()


let theStartTime = new LastChangedTime()
let dungeonRoomModelChanged = new LastChangedTime()

//////////////////////////////////////////////////////////////////////////////////////////

(*
Two main kinds of UI changes can occur:
 - user can scroll/change a Cell or click a Box, which requires immediate visual feedback
 - user can change model state requiring model to be recomputed, where feedback need not be immediate
     - indeed, 'buffering' these changes can be good, as scrolling an item often begets another scroll (need to scroll through many to find item user wants)

Thus, UI-driven live changes to UI do not need change events - if UI scrolls, UI is responsible to redraw the scrolled Cell right now.

And model changes need to just record some sub-portion of the model which has become stale, and then have some policy by which after some time, the entire model
gets recomputed and a fresh model is published via change events to the UI for a display update.

In practice, chose to divide state into a few main groups, which keep of when they last changed, and then have simple dependencies to recompute stale data and bring entire model back up to date.

Model is not entirely threadsafe; will do all these computations on the UI thread.
*)


//////////////////////////////////////////////////////////////////////////////////////////
// Player Progress

type BoolProperty(initState,changedFunc) =
    let mutable state = initState
    let changed = new Event<_>()
    member _this.Set(b) =
        if state <> b then
            state <- b
            changedFunc()
            changed.Trigger(state)
    member _this.Toggle() =
        state <- not state
        changedFunc()
        changed.Trigger(state)
    member _this.Value() = state
    member _this.Changed = changed.Publish
let playerProgressLastChangedTime = new LastChangedTime()
type PlayerProgressAndTakeAnyHearts() =
    // describes the state directly accessible in the upper right portion of the UI
    let takeAnyHearts = [| 0; 0; 0; 0 |]   // 0 = untaken (open heart on UI), 1 = taken heart (red heart on UI), 2 = taken potion/candle (X out empty heart on UI)
    let playerHasBoomBook      = BoolProperty(false,fun()->playerProgressLastChangedTime.SetNow())
    let playerHasWoodSword     = BoolProperty(false,fun()->playerProgressLastChangedTime.SetNow())
    let playerHasWoodArrow     = BoolProperty(false,fun()->playerProgressLastChangedTime.SetNow())
    let playerHasBlueRing      = BoolProperty(false,fun()->playerProgressLastChangedTime.SetNow())
    let playerHasBlueCandle    = BoolProperty(false,fun()->playerProgressLastChangedTime.SetNow())
    let playerHasMagicalSword  = BoolProperty(false,fun()->playerProgressLastChangedTime.SetNow())
    let playerHasDefeatedGanon = BoolProperty(false,fun()->playerProgressLastChangedTime.SetNow())
    let playerHasRescuedZelda  = BoolProperty(false,fun()->playerProgressLastChangedTime.SetNow())
    let playerHasBombs         = BoolProperty(false,fun()->playerProgressLastChangedTime.SetNow())
    let takeAnyKinds = [| 0; 0; 0; 0 |]   // what was taken in the slot: 0 = unknown / nothing, 1 = heart, 2 = blue candle, 3 = potion (this session only)
    let takeAnyHeartChanged = new Event<_>()
    member _this.GetTakeAnyHeart(i) = takeAnyHearts.[i]
    member _this.GetTakeAnyKind(i) = takeAnyKinds.[i]
    member _this.SetTakeAnyKind(i,k) =
        takeAnyKinds.[i] <- k
        takeAnyHearts.[i] <- (if k = 0 then 0 elif k = 1 then 1 else 2)
        playerProgressLastChangedTime.SetNow()
        takeAnyHeartChanged.Trigger(i)
    member _this.SetTakeAnyHeart(i,v) =
        if v = 0 then takeAnyKinds.[i] <- 0
        elif v = 1 then takeAnyKinds.[i] <- 1
        elif takeAnyKinds.[i] < 2 then takeAnyKinds.[i] <- 0
        if takeAnyHearts.[i] <> v then
            takeAnyHearts.[i] <- v
            playerProgressLastChangedTime.SetNow()
            takeAnyHeartChanged.Trigger(i)
    member _this.TakeAnyHeartChanged = takeAnyHeartChanged.Publish
    member _this.PlayerHasBoomBook      = playerHasBoomBook
    member _this.PlayerHasWoodSword     = playerHasWoodSword     
    member _this.PlayerHasWoodArrow     = playerHasWoodArrow
    member _this.PlayerHasBlueRing      = playerHasBlueRing      
    member _this.PlayerHasBlueCandle    = playerHasBlueCandle    
    member _this.PlayerHasMagicalSword  = playerHasMagicalSword  
    member _this.PlayerHasDefeatedGanon = playerHasDefeatedGanon 
    member _this.PlayerHasRescuedZelda  = playerHasRescuedZelda  
    member _this.PlayerHasBombs         = playerHasBombs
    member this.ResetAll() =
        for i = 0 to 3 do
            this.SetTakeAnyHeart(i,0)
        this.PlayerHasBoomBook.Set(false)
        this.PlayerHasWoodSword.Set(false)
        this.PlayerHasWoodArrow.Set(false)
        this.PlayerHasBlueRing.Set(false)
        this.PlayerHasBlueCandle.Set(false)
        this.PlayerHasMagicalSword.Set(false)
        this.PlayerHasDefeatedGanon.Set(false)
        this.PlayerHasRescuedZelda.Set(false)
        this.PlayerHasBombs.Set(false)
let playerProgressAndTakeAnyHearts = PlayerProgressAndTakeAnyHearts()

let extrasLastChangedTime = new LastChangedTime()
type StartingItemsAndExtras() =
    let triforces = Array.init 8 (fun _ -> BoolProperty(false,fun()->extrasLastChangedTime.SetNow()))
    let whiteSword = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let magicalSword = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let sword4 = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let silverArrow = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let bow = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let wand = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let redCandle = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let boomerang = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let magicBoomerang = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let redRing = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let powerBracelet = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let ladder = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let raft = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let recorder = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let anyKey = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let book = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let meat = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let letter = BoolProperty(false,fun()->extrasLastChangedTime.SetNow())
    let mutable maxHeartsDiff = 0
    member _this.HDNStartingTriforcePieces = triforces
    member _this.PlayerHasWhiteSword = whiteSword         // Note that unlike the item box / cave versions of these items, the starting item
    member _this.PlayerHasMagicalSword = magicalSword     // versions of these mean the player actually has a sword, even if WSMS->BU flag is on
    member _this.PlayerHasSword4 = sword4
    member _this.PlayerHasSilverArrow = silverArrow
    member _this.PlayerHasBow = bow
    member _this.PlayerHasWand = wand
    member _this.PlayerHasRedCandle = redCandle
    member _this.PlayerHasBoomerang = boomerang
    member _this.PlayerHasMagicBoomerang = magicBoomerang
    member _this.PlayerHasRedRing = redRing
    member _this.PlayerHasPowerBracelet = powerBracelet
    member _this.PlayerHasLadder = ladder
    member _this.PlayerHasRaft = raft
    member _this.PlayerHasRecorder = recorder
    member _this.PlayerHasAnyKey = anyKey
    member _this.PlayerHasBook = book
    member _this.PlayerHasMeat = meat
    member _this.PlayerHasLetter = letter
    member _this.MaxHeartsDifferential with get() = maxHeartsDiff and set(x) = maxHeartsDiff <- x
let startingItemsAndExtras = StartingItemsAndExtras()

let mutable playerHasAtlas = fun() -> false

let mutable IsHiddenDungeonNumbers = fun () -> false
let mutable GetDungeonLabelChar = fun (_idx) -> '?'

//////////////////////////////////////////////////////////////////////////////////////////
// Dungeons and Boxes

let dungeonsAndBoxesLastChangedTime = new LastChangedTime()
[<RequireQualifiedAccess>]
type PlayerHas = 
    | YES | NO | SKIPPED
    member this.AsInt() = match this with | PlayerHas.NO -> 0 | PlayerHas.YES -> 1 | PlayerHas.SKIPPED -> 2
    static member FromInt(x) = if x=0 then PlayerHas.NO elif x=1 then PlayerHas.YES elif x=2 then PlayerHas.SKIPPED else failwith "bad PlayerHas value"

[<RequireQualifiedAccess>]
type StairKind = // does this box represent a dungeon basement item? we only both with this for display purposes in non-hidden-dungeon-numbers
    | Never
    | Always
    | LikeL2  // 2nd box of 2 is basement in 2nd quest
    | LikeL3  // 2nd box of 3 is a basement in 1st quest
[<RequireQualifiedAccess>]
type BoxOwner =   // for deciding what Blocker-projections to display in it
    | DungeonIndexAndNth of int*int // { 0-8 for 1-9 or A-H,9 ; 0-2 for first-third box }
    | Dungeon1or4                   // the third box of 1 or 4 depending upon SecondQuestDungeons value
    | None                          // an item box that does not below to a dungeon, e.g. CoastItem
// the level 9 entry box is only a note, so it gets its own domain (it must not use up items that the real boxes need)
let level9TriggerDomain = ChoiceDomain("level9Trigger", ITEMS.itemNamesAndCounts |> Array.map (fun _ -> 1000))
type Box(stair:StairKind, owner, domain:ChoiceDomain) =
    // this contains both a Cell (player-knowing-location-contents), and a bool (whether the players _has_ the thing there)
    let cell = new Cell(domain)
    let mutable playerHas = PlayerHas.NO
    let changed = new Event<_>()
    member _this.Changed = changed.Publish
    member _this.PlayerHas() = playerHas           // note that the state (cell = -1, playerHas = SKIPPED) is used to mean 'white-highlighted box' in the UI
    member this.IsEmptyRedBox = this.PlayerHas() = PlayerHas.NO && this.CellCurrent() = -1
    member _this.Stair = stair
    member _this.CurrentlyHasBasementStair =
        if IsHiddenDungeonNumbers() then
            match owner with
            | BoxOwner.DungeonIndexAndNth(i,n) ->
                if i=8 then true else   // dungeon 9 has all basements
                let lc = GetDungeonLabelChar(i)
                if IsSecondQuestDungeons then
                    match lc with
                    |'1'|'3' -> false
                    |'2'|'5'|'6'|'7' -> n=1
                    |'4'|'8' -> n=1 || n=2
                    | _ -> false
                else
                    match lc with
                    |'1' -> n=2
                    |'2' -> false
                    |'3'|'4'|'5'|'6'|'7' -> n=1
                    |'8' -> n=1 || n=2
                    | _ -> false
            | _ -> false
        else
            match stair with
            | StairKind.LikeL2 -> false   // level 2's second item is a floor item (not a basement) in both quests
            | StairKind.LikeL3 -> not(IsSecondQuestDungeons)
            | StairKind.Always -> true
            | StairKind.Never -> false
    member _this.Owner = owner
    new(stair, owner) = Box(stair, owner, allItemWithHeartShuffleChoiceDomain)
    member _this.Domain = domain
    member _this.CellNextFreeKey() = domain.NextFreeKey(cell.Current())
    member _this.CellPrevFreeKey() = domain.PrevFreeKey(cell.Current())
    member _this.CellPrev() = 
        cell.Prev()
        dungeonsAndBoxesLastChangedTime.SetNow()
        changed.Trigger()
    member _this.CellNext() = 
        cell.Next()
        dungeonsAndBoxesLastChangedTime.SetNow()
        changed.Trigger()
    member _this.CellCurrent() = cell.Current()
    member _this.Set(v,ph) = 
        cell.Set(v)
        playerHas <- ph
        dungeonsAndBoxesLastChangedTime.SetNow()
        changed.Trigger()
    member _this.AttemptToSet(v,ph) = 
        if cell.AttemptToSet(v) then
            playerHas <- ph
            dungeonsAndBoxesLastChangedTime.SetNow()
            changed.Trigger()
            true
        else
            false
    member _this.SetPlayerHas(v) = 
        playerHas <- v
        dungeonsAndBoxesLastChangedTime.SetNow()
        changed.Trigger()
    member _this.IsDone() = cell.Current() <> -1 && playerHas <> PlayerHas.NO   // the player knows the item here and has gotten or intentionally skipped it

let ladderBox = (let b = Box(StairKind.Never, BoxOwner.None) in b.SetPlayerHas(PlayerHas.SKIPPED); b)
let armosBox  = (let b = Box(StairKind.Never, BoxOwner.None) in b.SetPlayerHas(PlayerHas.SKIPPED); b)
let sword2Box = (let b = Box(StairKind.Never, BoxOwner.None) in b.SetPlayerHas(PlayerHas.SKIPPED); b)
let sword3Box = (let b = Box(StairKind.Never, BoxOwner.None) in b.SetPlayerHas(PlayerHas.SKIPPED); b)   // item found in the magical sword cave (this session only, not saved)
let letterBox = (let b = Box(StairKind.Never, BoxOwner.None) in b.SetPlayerHas(PlayerHas.SKIPPED); b)   // item found in the letter cave (this session only, not saved)
let progressiveBoxGlow = new Event<Box>()   // 'Progressive Items': an item box whose item was just upgraded (its views glow and grow for a moment)
let sword1Box = (let b = Box(StairKind.Never, BoxOwner.None) in b.SetPlayerHas(PlayerHas.SKIPPED); b)   // item found in a 'shuffled' wood sword cave (this session only, not saved)
let hintShopColors = new System.Collections.Generic.Dictionary<int*int, int>()   // overworld tile -> the background color chosen for its hint shop (0 purple, 1 red, 2 green, 3 orange); saved with the tracker
let shuffledWoodSwordTiles = new System.Collections.Generic.HashSet<int*int>()   // overworld tiles whose wood sword cave holds a shuffled item (this session only)
// the optional extra 'level 2 staircase' item box (disabled/greyed out until enabled)
let level9TriggerBox = (let b = Box(StairKind.Never, BoxOwner.None, level9TriggerDomain) in b.SetPlayerHas(PlayerHas.SKIPPED); b)   // the level 9 entry rule: empty means the default (all 8 triforces); otherwise the item that opens level 9 (this session only)
let mutable level9TriggerSwordTier = 1   // when the entry item is the progressive sword: the tier it showed when picked (fixed; the picture does not follow later upgrades)
let mutable level9TriforceCount = 8   // how many triforces the default level 9 entry rule needs (scroll the number on the main tracker to change it)
let level2StairBox = Box(StairKind.Always, BoxOwner.None)   // shows the basement staircase icon like the other basement item slots
let mutable level2StairBoxEnabled = false
let level2StairBoxEnabledChanged = new Event<unit>()
let SetLevel2StairBoxEnabled(enabled:bool) =
    if level2StairBoxEnabled <> enabled then
        level2StairBoxEnabled <- enabled
        level2StairBoxEnabledChanged.Trigger()
// the other place the '4th Sword' start option can put the extra item: a floor item check in level 9 (it uses level 9's ExtraFloorBox)
let mutable level9FloorBoxEnabled = false
let level9FloorBoxEnabledChanged = new Event<unit>()
let SetLevel9FloorBoxEnabled(enabled:bool) =
    if level9FloorBoxEnabled <> enabled then
        level9FloorBoxEnabled <- enabled
        level9FloorBoxEnabledChanged.Trigger()

[<RequireQualifiedAccess>]
type DungeonTrackerInstanceKind =
    | HIDE_DUNGEON_NUMBERS
    | DEFAULT

type DungeonTrackerInstance(kind) =
    //RPT adjusted this below
    static let mutable theInstance = None : DungeonTrackerInstance option


    let finalBoxOf1Or4 = new Box(StairKind.Always, BoxOwner.Dungeon1or4)  // only relevant in DEFAULT
    let makeDungeons() = 
        match kind with
        | DungeonTrackerInstanceKind.HIDE_DUNGEON_NUMBERS -> [| 
            for i = 0 to 7 do 
                yield new Dungeon(i,3) 
            yield new Dungeon(8,2)
            |]
        | DungeonTrackerInstanceKind.DEFAULT -> [|
            new Dungeon(0, 2)
            new Dungeon(1, 2)
            new Dungeon(2, 2)
            new Dungeon(3, 2)
            new Dungeon(4, 2)
            new Dungeon(5, 2)
            new Dungeon(6, 2)
            new Dungeon(7, 3)
            new Dungeon(8, 2)
            |]
    let mutable dungeons = null
    let getDungeons() =
        if dungeons = null then
            dungeons <- makeDungeons()
        dungeons
    let mutable allBoxes : Box[] = null
    let all() =
        if allBoxes = null then
            allBoxes <- 
                [|
                for d in getDungeons() do
                    yield! d.Boxes
                    yield d.ExtraFloorBox
                if kind = DungeonTrackerInstanceKind.DEFAULT then yield finalBoxOf1Or4   // (always include the shared level 1/4 basement box, even if the quest type changed after this list was first built)
                yield level2StairBox   // the optional extra level 2 staircase slot (its item counts for Item Progress and logic like any other box)
                yield ladderBox
                yield armosBox
                yield sword2Box
                yield sword3Box
                yield letterBox
                yield sword1Box
                |] |> Array.distinct
        allBoxes
    let allBoxProgress = 
        let mutable pct = 0.
        let mutable anyChanged = None
        let any() =
            if anyChanged.IsNone then
                anyChanged <- Some(
                    new SyntheticEventingBool((fun()-> 
                        pct <- float(all() |> Seq.sumBy (fun b -> if not(b.IsEmptyRedBox) then 1 else 0)) / 20. // 23 items exist; be max red when few left
                        pct <- min pct 1.0    // don't say e.g. 23./20., and also hidden dungeon numbers has extra boxes
                        false), all() |> Seq.map (fun b -> b.Changed)))
            anyChanged.Value
        { new IEventingReader<float> with
            member this.Value = pct
            member this.Changed = any().Changed
        }

    
    static member TheDungeonTrackerInstanceOption
        with get() = theInstance

    member _this.Kind = kind
    member _this.Dungeons(i) = getDungeons().[i]
    member _this.FinalBoxOf1Or4 =
        match kind with
        | DungeonTrackerInstanceKind.HIDE_DUNGEON_NUMBERS -> failwith "FinalBoxOf1Or4 does not exist in HIDE_DUNGEON_NUMBERS"
        | DungeonTrackerInstanceKind.DEFAULT -> finalBoxOf1Or4
    member _this.AllBoxes() = all()
    member this.AllBoxProgress = allBoxProgress
    //RPT updating
    static member TheDungeonTrackerInstance 
        with get() = 
            match theInstance with 
            | Some i -> i 
            | None -> 
                TrackerModelOptions.DebugConfig.Log("[ERROR] Accessed TheDungeonTrackerInstance before initialization")
                failwith "uninitialized TheDungeonTrackerInstance" 
        and set(x:DungeonTrackerInstance) = theInstance <- Some(x)

and Dungeon(id,numBoxes) =
    let mutable playerHasTriforce = false                     // just ignore this for dungeon 9 (id=8)
    let mutable playerHasMap = false                          // map for this dungeon (book atlas does not affect this)
    let boxes = Array.init numBoxes (fun j -> 
        if DungeonTrackerInstance.TheDungeonTrackerInstance.Kind = DungeonTrackerInstanceKind.HIDE_DUNGEON_NUMBERS then
            new Box(StairKind.Never, BoxOwner.DungeonIndexAndNth(id,j))
        else
            if id=8 || (j=1 && not(id=0 || id=1 || id=2)) || j=2 then
                new Box(StairKind.Always, BoxOwner.DungeonIndexAndNth(id,j))
            elif j=1 && id=1 then
                new Box(StairKind.LikeL2, BoxOwner.DungeonIndexAndNth(id,j))
            elif j=1 && id=2 then
                new Box(StairKind.LikeL3, BoxOwner.DungeonIndexAndNth(id,j))
            else
                new Box(StairKind.Never, BoxOwner.DungeonIndexAndNth(id,j))
        )
    let extraFloorBox = new Box(StairKind.Never, BoxOwner.None)   // the floor item that replaces the triforce header when a Triforce Piece is found in an item slot
    let mutable color = 0                // 0xRRGGBB format   // just ignore this for dungeon 9 (id=8)
    let mutable labelChar = '?'          // ?12345678         // just ignore this for dungeon 9 (id=8)
    let hiddenDungeonColorLabelChangeEvent = new Event<_>()
    let playerHasTriforceChangeEvent = new Event<_>()
    let hasBeenLocatedChangeEvent = new Event<_>()
    let playerHasMapOfThisDungeonChangeEvent = new Event<_>()
    let playerCanSeeMapOfThisDungeonChangeEvent = new Event<_>()
    let completedWasNoticedChanged = new Event<_>()
    let mutable priorComplete = false
    let mutable currentlyReentrant = false
    do
        mapSquareChangedStable.Publish.Add(fun (_,key) -> if key=id then hasBeenLocatedChangeEvent.Trigger())
        // atlas watching
        IsCurrentlyBookChanged.Add(fun _ -> playerCanSeeMapOfThisDungeonChangeEvent.Trigger())
        IsBookAnAtlasChanged.Add(fun _ -> playerCanSeeMapOfThisDungeonChangeEvent.Trigger())
        playerProgressAndTakeAnyHearts.PlayerHasBoomBook.Changed.Add(fun _ -> playerCanSeeMapOfThisDungeonChangeEvent.Trigger())
        playerComputedStateSummaryForPlayerHasBookChanged.Publish.Add(fun _ -> playerCanSeeMapOfThisDungeonChangeEvent.Trigger())
    member _this.HasBeenLocated() = mapSquareChoiceDomain.NumUses(id) = 1   // WARNING: this being true does NOT mean TrackerModel.mapStateSummary.DungeonLocations.[i] has a legal (non-NOTFOUND) value
    member _this.HasBeenLocatedChanged = hasBeenLocatedChangeEvent.Publish
    member this.PlayerHasTriforce() = playerHasTriforce || (this.Boxes |> Array.exists (fun b -> b.CellCurrent() = ITEMS.TRIFORCE && b.PlayerHas() = PlayerHas.YES)) || (id=1 && level2StairBoxEnabled && level2StairBox.CellCurrent() = ITEMS.TRIFORCE && level2StairBox.PlayerHas() = PlayerHas.YES)   // (a triforce piece marked as obtained in an item slot counts too)
    member _this.ToggleTriforce() = playerHasTriforce <- not playerHasTriforce; playerHasTriforceChangeEvent.Trigger(playerHasTriforce); dungeonsAndBoxesLastChangedTime.SetNow()
    member _this.PlayerHasTriforceChanged = playerHasTriforceChangeEvent.Publish
    member _this.PlayerHasMapOfThisDungeon with get() = playerHasMap and set(x) = playerHasMap <- x; playerHasMapOfThisDungeonChangeEvent.Trigger(); playerCanSeeMapOfThisDungeonChangeEvent.Trigger()
    member _this.PlayerHasMapOfThisDungeonChanged = playerHasMapOfThisDungeonChangeEvent.Publish
    member _this.PlayerCanSeeMapOfThisDungeon = playerHasMap || playerHasAtlas()
    member _this.PlayerCanSeeMapOfThisDungeonChanged = playerCanSeeMapOfThisDungeonChangeEvent.Publish
    member _this.Boxes = 
        match DungeonTrackerInstance.TheDungeonTrackerInstance.Kind with
        | DungeonTrackerInstanceKind.HIDE_DUNGEON_NUMBERS -> boxes
        | DungeonTrackerInstanceKind.DEFAULT ->
            if id=0 && not(IsSecondQuestDungeons) || id=3 && IsSecondQuestDungeons then
                [| yield! boxes; yield DungeonTrackerInstance.TheDungeonTrackerInstance.FinalBoxOf1Or4 |]
            else
                boxes
    member _this.ExtraFloorBox = extraFloorBox
    member this.TriforceIsInItemSlot = this.Boxes |> Array.exists (fun b -> b.CellCurrent() = ITEMS.TRIFORCE) || (id=1 && level2StairBoxEnabled && level2StairBox.CellCurrent() = ITEMS.TRIFORCE)
    member this.IsComplete = 
        if currentlyReentrant then
            priorComplete
        else
            currentlyReentrant <- true
            let r = 
                match DungeonTrackerInstance.TheDungeonTrackerInstance.Kind with
                | DungeonTrackerInstanceKind.HIDE_DUNGEON_NUMBERS ->
                    if this.PlayerHasTriforce() then
                        let mutable numBoxesDone = 0
                        for b in boxes do
                            if b.IsDone() then
                                numBoxesDone <- numBoxesDone + 1
                        let twoBoxers = if IsSecondQuestDungeons then "123567" else "234567"
                        numBoxesDone = 3 || (numBoxesDone = 2 && (twoBoxers |> Seq.contains this.LabelChar))
                    else
                        false
                | DungeonTrackerInstanceKind.DEFAULT ->
                    this.PlayerHasTriforce() && this.Boxes |> Array.forall (fun b -> b.IsDone()) && (not(this.TriforceIsInItemSlot) || extraFloorBox.CellCurrent() = -1 || extraFloorBox.IsDone())   // (the floor item that replaces the triforce only has to be collected if one was recorded)
            let needToNotify = priorComplete <> r
            priorComplete <- r
            if needToNotify then
                completedWasNoticedChanged.Trigger()   // can cause a recursive call
            currentlyReentrant <- false
            r
    member this.IsCompleteWasNoticedChanged = completedWasNoticedChanged.Publish  // not watching underlying, but main app polls IsCompleted every second, so next poll will notice change and report
    // for Hidden Dungeon Numbers
    member _this.Color with get() = color and set(x) = color <- x; hiddenDungeonColorLabelChangeEvent.Trigger(color,labelChar)
    member _this.LabelChar with get() = labelChar and set(x) = labelChar <- x; hiddenDungeonColorLabelChangeEvent.Trigger(color,labelChar); dungeonsAndBoxesLastChangedTime.SetNow()
    member _this.HiddenDungeonColorOrLabelChanged = hiddenDungeonColorLabelChangeEvent.Publish

let GetDungeon(i) = DungeonTrackerInstance.TheDungeonTrackerInstance.Dungeons(i)
do IsHiddenDungeonNumbers <- fun() -> DungeonTrackerInstance.TheDungeonTrackerInstance.Kind = DungeonTrackerInstanceKind.HIDE_DUNGEON_NUMBERS
do GetDungeonLabelChar <- fun i -> GetDungeon(i).LabelChar



let GetTriforceHaves() =
    if IsHiddenDungeonNumbers() then
        let haves = Array.zeroCreate 8
        for i = 0 to 7 do
            let d = GetDungeon(i)
            if d.PlayerHasTriforce() then
                if d.LabelChar >= '1' && d.LabelChar <= '8' then
                    let n = int d.LabelChar - int '1'
                    haves.[n] <- true
            if startingItemsAndExtras.HDNStartingTriforcePieces.[i].Value() then
                haves.[i] <- true
        haves
    else
        [| for i = 0 to 7 do yield GetDungeon(i).PlayerHasTriforce() |]

//////////////////////////////////////////////////////////////////////////////////////////
// Player computed state summary

type PlayerComputedStateSummary(haveRecorder,haveLadder,haveAnyKey,haveCoastItem,haveWhiteSwordItem,havePowerBracelet,haveRaft,playerHearts,
                                swordLevel,candleLevel,ringLevel,haveBow,arrowLevel,haveWand,haveBook,boomerangLevel) = 
    // computed from Boxes and other bits
    member _this.HaveRecorder = haveRecorder
    member _this.HaveLadder = haveLadder
    member _this.HaveAnyKey = haveAnyKey
    member _this.HaveCoastItem = haveCoastItem
    member _this.HaveWhiteSwordItem = haveWhiteSwordItem
    member _this.HavePowerBracelet = havePowerBracelet
    member _this.HaveRaft = haveRaft
    member _this.PlayerHearts = playerHearts
    member _this.SwordLevel = swordLevel
    member _this.CandleLevel = candleLevel
    member _this.RingLevel = ringLevel
    member _this.HaveBow = haveBow
    member _this.ArrowLevel = arrowLevel
    member _this.HaveWand = haveWand
    member _this.HaveBookOrShield = haveBook
    member _this.BoomerangLevel = boomerangLevel

let mutable playerComputedStateSummary = PlayerComputedStateSummary(false,false,false,false,false,false,false,3,0,0,0,false,0,false,false,0)
let PlayerHasTheBook() = if IsCurrentlyBook() then playerComputedStateSummary.HaveBookOrShield else playerProgressAndTakeAnyHearts.PlayerHasBoomBook.Value()
let playerComputedStateSummaryLastComputedTime = new LastChangedTime()
let playerSwordLevel, playerCandleLevel, playerRingLevel, playerArrowLevel = new EventingInt(0), new EventingInt(0), new EventingInt(0), new EventingInt(0)
let playerHasSword4 = new EventingInt(0)   // 1 if a 4th sword has been collected (an item box, or its marker icon checked off on the map); puts the purple X on the 3rd (magical) sword box
// how many marker icons of each kind are checked off as 'collected' on the overworld map (index = item id); these count as owning the item
let mapCollectedCount : int[] = Array.zeroCreate 128
// Progressive Items: every progressive sword (the 'P' sword) that has been collected, on the map or in an item box, bumps the tier shown by the ones still to be collected (P1 wood, P2 white, P3 magical, P4 4th sword); 0 when the option is off
let progressiveTierChanged = new Event<unit>()
// with Progressive Items on, a sword placed in a dungeon item box acts like its progressive sword: wood = P1, white = P2, magical = P3, 4th = P4
let progressiveSwordBoxTier(ident:int) : int =
    if not ProgressiveItems then 0
    elif ident = ITEMS.WOODSWORD then 1
    elif ident = ITEMS.WHITESWORD then 2
    elif ident = ITEMS.MAGICALSWORD then 3
    elif ident = ITEMS.WHITESWORD4 then 4
    elif ident = ITEMS.PROGRESSIVESWORD then 5   // (the progressive sword itself: shows the current tier)
    else 0
// which tier each collected sword was collected at (session only), so the collected icon keeps showing where the upgrade came from
let progressiveTileTier = new System.Collections.Generic.Dictionary<int*int*int, int>()   // (tile x, tile y, item) -> tier
let progressiveBoxTier = new System.Collections.Generic.Dictionary<Box, int>(HashIdentity.Reference)
let mutable progressiveTierOverride = -1   // while set, drawing code asks for this tier instead of the current one
let progressiveSwordTier() : int =
    if not ProgressiveItems then 0
    elif progressiveTierOverride >= 1 then progressiveTierOverride
    else
        let inBoxes = try DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes() |> Array.filter (fun b -> progressiveSwordBoxTier(b.CellCurrent()) > 0 && b.PlayerHas() = PlayerHas.YES) |> Array.length with _ -> 0
        let onMap = [ ITEMS.PROGRESSIVESWORD; ITEMS.WOODSWORD; ITEMS.WHITESWORD; ITEMS.MAGICALSWORD; ITEMS.WHITESWORD4 ] |> List.sumBy (fun id -> mapCollectedCount.[id])   // (swords on the map, in shops too, act like progressive swords)
        // the wood sword cave's 'Took Sword' (and starting swords) also count: the sword level the player has, when higher than what the icons and boxes account for
        let level = if startingItemsAndExtras.PlayerHasMagicalSword.Value() then 3 elif startingItemsAndExtras.PlayerHasWhiteSword.Value() then 2 elif playerProgressAndTakeAnyHearts.PlayerHasWoodSword.Value() then 1 else 0
        min 4 (1 + max (onMap + inBoxes) level)
// Progressive Items: families of items that upgrade each other. 1 = swords, 2 = rings, 3 = candles, 4 = arrows (0 = not part of one, or the option is off)
let progressiveFamily(ident:int) : int =
    if not ProgressiveItems then 0
    elif progressiveSwordBoxTier(ident) > 0 then 1
    elif ident = ITEMS.BLUERING || ident = ITEMS.REDRING then 2
    elif ident = ITEMS.BLUECANDLE || ident = ITEMS.REDCANDLE then 3
    elif ident = ITEMS.WOODARROW || ident = ITEMS.SILVERARROW then 4
    elif ident = ITEMS.BOOMERANG || ident = ITEMS.MAGICBOOMERANG then 5   // (boomerangs, or the boots / magical boots that replace them)
    else 0
// the item (base = tier 1, upgraded = tier 2) that stands for a family tier; swords use their own sprites instead
let progressiveFamilyIdent(family:int, tier:int) : int =
    match family with
    | 2 -> (if tier >= 2 then ITEMS.REDRING else ITEMS.BLUERING)
    | 3 -> (if tier >= 2 then ITEMS.REDCANDLE else ITEMS.BLUECANDLE)
    | 4 -> (if tier >= 2 then ITEMS.SILVERARROW else ITEMS.WOODARROW)
    | 5 -> (if tier >= 2 then ITEMS.MAGICBOOMERANG else ITEMS.BOOMERANG)
    | _ -> ITEMS.PROGRESSIVESWORD
// the current tier of a family: 1 + how many of its items have been collected (on the map or in item boxes), 2 at most for rings / candles / arrows
let progressiveFamilyTier(family:int) : int =
    if not ProgressiveItems || family <= 0 then 0
    elif family = 1 then progressiveSwordTier()
    elif progressiveTierOverride >= 1 then min 2 progressiveTierOverride
    else
        let ids, propLevel =
            match family with
            | 2 -> [ITEMS.BLUERING; ITEMS.REDRING], (if startingItemsAndExtras.PlayerHasRedRing.Value() then 2 elif playerProgressAndTakeAnyHearts.PlayerHasBlueRing.Value() then 1 else 0)
            | 3 -> [ITEMS.BLUECANDLE; ITEMS.REDCANDLE], (if startingItemsAndExtras.PlayerHasRedCandle.Value() then 2 elif playerProgressAndTakeAnyHearts.PlayerHasBlueCandle.Value() then 1 else 0)
            | 5 -> [ITEMS.BOOMERANG; ITEMS.MAGICBOOMERANG], (if startingItemsAndExtras.PlayerHasMagicBoomerang.Value() then 2 elif startingItemsAndExtras.PlayerHasBoomerang.Value() then 1 else 0)
            | _ -> [ITEMS.WOODARROW; ITEMS.SILVERARROW], (if startingItemsAndExtras.PlayerHasSilverArrow.Value() then 2 elif playerProgressAndTakeAnyHearts.PlayerHasWoodArrow.Value() then 1 else 0)
        let n = (ids |> List.sumBy (fun id -> mapCollectedCount.[id])) + (try DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes() |> Array.filter (fun b -> List.contains (b.CellCurrent()) ids && b.PlayerHas() = PlayerHas.YES) |> Array.length with _ -> 0)
        min 2 (1 + max n propLevel)
// items that the option disables in the pickers: the plain swords (use the progressive sword) and the second tier of rings, candles and arrows
let progressiveDisabledIdent(ident:int) : bool =
    ProgressiveItems && ((progressiveSwordBoxTier(ident) >= 1 && progressiveSwordBoxTier(ident) <= 4) || ident = ITEMS.REDRING || ident = ITEMS.REDCANDLE || ident = ITEMS.SILVERARROW || ident = ITEMS.MAGICBOOMERANG)
let recomputePlayerStateSummary() =
    let mutable haveRecorder,haveLadder,haveAnyKey,haveCoastItem,haveWhiteSwordItem,havePowerBracelet,haveRaft,playerHearts = false,false,false,false,false,false,false,3
    let mutable swordLevel,candleLevel,ringLevel,haveBow,arrowLevel,haveWand,haveBookOrShield,boomerangLevel = 0,0,0,false,0,false,false,0
    let mutable haveSword4 = false
    let mutable progressiveSwords = 0   // each progressive sword marked as obtained bumps the sword level by 1
    for b in DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes() do
        if b.PlayerHas() = PlayerHas.YES then
            if b.CellCurrent() = ITEMS.RECORDER then
                haveRecorder <- true
            elif b.CellCurrent() = ITEMS.LADDER then
                haveLadder <- true
            elif b.CellCurrent() = ITEMS.KEY then
                haveAnyKey <- true
            elif b.CellCurrent() = ITEMS.POWERBRACELET then
                havePowerBracelet <- true
            elif b.CellCurrent() = ITEMS.RAFT then
                haveRaft <- true
            elif b.CellCurrent() = ITEMS.REDCANDLE then
                candleLevel <- 2
            elif b.CellCurrent() = ITEMS.HEARTCONTAINER then
                playerHearts <- playerHearts + 1
            elif b.CellCurrent() = ITEMS.BOOKSHIELD then
                haveBookOrShield <- true
            elif b.CellCurrent() = ITEMS.BOOMERANG then
                boomerangLevel <- max boomerangLevel 1
            elif b.CellCurrent() = ITEMS.BOW then
                haveBow <- true
            elif b.CellCurrent() = ITEMS.MAGICBOOMERANG then
                boomerangLevel <- 2
            elif b.CellCurrent() = ITEMS.REDRING then
                ringLevel <- 2
            elif b.CellCurrent() = ITEMS.SILVERARROW then
                arrowLevel <- 2
            elif b.CellCurrent() = ITEMS.WAND then
                haveWand <- true
            elif ProgressiveItems && progressiveSwordBoxTier(b.CellCurrent()) >= 1 && progressiveSwordBoxTier(b.CellCurrent()) <= 4 then
                progressiveSwords <- progressiveSwords + 1   // Progressive Items: every sword counts as one more progressive sword
            elif b.CellCurrent() = ITEMS.WHITESWORD then
                if not(IsWSMSReplacedByBU()) then
                    swordLevel <- max swordLevel 2
            elif b.CellCurrent() = ITEMS.BLUECANDLE then
                candleLevel <- max candleLevel 1
            elif b.CellCurrent() = ITEMS.BLUERING then
                ringLevel <- max ringLevel 1
            elif b.CellCurrent() = ITEMS.WOODARROW then
                arrowLevel <- max arrowLevel 1
            elif b.CellCurrent() = ITEMS.WOODSWORD then
                swordLevel <- max swordLevel 1
            elif b.CellCurrent() = ITEMS.PROGRESSIVESWORD then
                progressiveSwords <- progressiveSwords + 1
            elif b.CellCurrent() = ITEMS.WHITESWORD4 then
                swordLevel <- max swordLevel 2
                haveSword4 <- true
            elif b.CellCurrent() = ITEMS.MAGICALSWORD then
                if not(IsWSMSReplacedByBU()) then
                    swordLevel <- 3
    if playerProgressAndTakeAnyHearts.PlayerHasBlueCandle.Value() then
        candleLevel <- max candleLevel 1
    if playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Value() && not(IsWSMSReplacedByBU()) then
        swordLevel <- 3
    if playerProgressAndTakeAnyHearts.PlayerHasWoodSword.Value() then
        swordLevel <- max swordLevel 1
    if playerProgressAndTakeAnyHearts.PlayerHasBlueRing.Value() then
        ringLevel <- max ringLevel 1
    if playerProgressAndTakeAnyHearts.PlayerHasWoodArrow.Value() then
        arrowLevel <- max arrowLevel 1
    if startingItemsAndExtras.PlayerHasWhiteSword.Value() then
        swordLevel <- max swordLevel 2
    if startingItemsAndExtras.PlayerHasMagicalSword.Value() then
        swordLevel <- 3
    if startingItemsAndExtras.PlayerHasSword4.Value() then
        swordLevel <- max swordLevel 2
        haveSword4 <- true
    if startingItemsAndExtras.PlayerHasSilverArrow.Value() then
        arrowLevel <- 2
    if startingItemsAndExtras.PlayerHasBoomerang.Value() then
        boomerangLevel <- max boomerangLevel 1
    if startingItemsAndExtras.PlayerHasMagicBoomerang.Value() then
        boomerangLevel <- 2
    if startingItemsAndExtras.PlayerHasBow.Value() then
        haveBow <- true
    if startingItemsAndExtras.PlayerHasRedCandle.Value() then
        candleLevel <- 2
    if startingItemsAndExtras.PlayerHasWand.Value() then
        haveWand <- true
    if startingItemsAndExtras.PlayerHasRedRing.Value() then
        ringLevel <- 2
    if startingItemsAndExtras.PlayerHasPowerBracelet.Value() then
        havePowerBracelet <- true
    if startingItemsAndExtras.PlayerHasLadder.Value() then
        haveLadder <- true
    if startingItemsAndExtras.PlayerHasRaft.Value() then
        haveRaft <- true
    if startingItemsAndExtras.PlayerHasRecorder.Value() then
        haveRecorder <- true
    if startingItemsAndExtras.PlayerHasAnyKey.Value() then
        haveAnyKey <- true
    if startingItemsAndExtras.PlayerHasBook.Value() then
        haveBookOrShield <- true
    // items checked off as collected on the overworld map
    let mapHas(id) = mapCollectedCount.[id] > 0
    if ProgressiveItems then
        // swords (of any kind) collected on the map each count as one more progressive sword
        progressiveSwords <- progressiveSwords + mapCollectedCount.[ITEMS.WOODSWORD] + mapCollectedCount.[ITEMS.WHITESWORD] + mapCollectedCount.[ITEMS.MAGICALSWORD] + mapCollectedCount.[ITEMS.WHITESWORD4]
    else
        if mapHas ITEMS.WOODSWORD then swordLevel <- max swordLevel 1
        if mapHas ITEMS.WHITESWORD || mapHas ITEMS.WHITESWORD4 then swordLevel <- max swordLevel 2
        if mapHas ITEMS.WHITESWORD4 then haveSword4 <- true
        if mapHas ITEMS.MAGICALSWORD then swordLevel <- 3
    progressiveSwords <- progressiveSwords + mapCollectedCount.[ITEMS.PROGRESSIVESWORD]
    for _ in 1 .. progressiveSwords do   // each one is the next sword up: wood > white > magical > 4th sword (the max)
        if not haveSword4 then
            if swordLevel >= 3 then (if TrackerModelOptions.FourthSwordEnabled then haveSword4 <- true)   // the 4th sword is only reachable if that option is on
            else swordLevel <- swordLevel + 1
    if mapHas ITEMS.BLUECANDLE then candleLevel <- max candleLevel 1
    if mapHas ITEMS.REDCANDLE then candleLevel <- 2
    if mapHas ITEMS.BLUERING then ringLevel <- max ringLevel 1
    if mapHas ITEMS.REDRING then ringLevel <- 2
    if mapHas ITEMS.WOODARROW then arrowLevel <- max arrowLevel 1
    if mapHas ITEMS.SILVERARROW then arrowLevel <- 2
    if ProgressiveItems then
        // rings, candles and arrows: the second one collected of a kind (blue or red) is the upgrade
        let famCount (ids:int list) =
            (ids |> List.sumBy (fun id -> mapCollectedCount.[id]))
            + (DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes() |> Array.filter (fun b -> List.contains (b.CellCurrent()) ids && b.PlayerHas() = PlayerHas.YES) |> Array.length)
        let lvl n = if n >= 2 then 2 elif n >= 1 then 1 else 0
        candleLevel <- max candleLevel (lvl (famCount [ITEMS.BLUECANDLE; ITEMS.REDCANDLE]))
        ringLevel <- max ringLevel (lvl (famCount [ITEMS.BLUERING; ITEMS.REDRING]))
        arrowLevel <- max arrowLevel (lvl (famCount [ITEMS.WOODARROW; ITEMS.SILVERARROW]))
        boomerangLevel <- max boomerangLevel (lvl (famCount [ITEMS.BOOMERANG; ITEMS.MAGICBOOMERANG]))
    if mapHas ITEMS.BOOMERANG then boomerangLevel <- max boomerangLevel 1
    if mapHas ITEMS.MAGICBOOMERANG then boomerangLevel <- 2
    if mapHas ITEMS.BOW then haveBow <- true
    if mapHas ITEMS.WAND then haveWand <- true
    if mapHas ITEMS.POWERBRACELET then havePowerBracelet <- true
    if mapHas ITEMS.LADDER then haveLadder <- true
    if mapHas ITEMS.RAFT then haveRaft <- true
    if mapHas ITEMS.RECORDER then haveRecorder <- true
    if mapHas ITEMS.KEY then haveAnyKey <- true   // the regular (old) key is only dimmed on the map; it does not count as the any key
    if mapHas ITEMS.BOOKSHIELD then haveBookOrShield <- true   // a collected (plain) shield does not count as the book
    if ladderBox.IsDone() then
        haveCoastItem <- true
    if sword2Box.IsDone() then
        haveWhiteSwordItem <- true
    for h = 0 to 3 do
        if playerProgressAndTakeAnyHearts.GetTakeAnyHeart(h) = 1 then
            playerHearts <- playerHearts + 1
    playerHearts <- playerHearts + startingItemsAndExtras.MaxHeartsDifferential + mapCollectedCount.[ITEMS.HEARTCONTAINER]
    let bookChanged = haveBookOrShield <> playerComputedStateSummary.HaveBookOrShield
    playerComputedStateSummary <- PlayerComputedStateSummary(haveRecorder,haveLadder,haveAnyKey,haveCoastItem,haveWhiteSwordItem,havePowerBracelet,haveRaft,playerHearts,
                                                                swordLevel,candleLevel,ringLevel,haveBow,arrowLevel,haveWand,haveBookOrShield,boomerangLevel)
    playerSwordLevel.Value <- swordLevel
    playerCandleLevel.Value <- candleLevel
    playerRingLevel.Value <- ringLevel
    playerArrowLevel.Value <- arrowLevel
    playerHasSword4.Value <- (if haveSword4 then 1 else 0)
    if bookChanged then
        playerComputedStateSummaryForPlayerHasBookChanged.Trigger()
    playerComputedStateSummaryLastComputedTime.SetNow()


let playerHasAtlasChanged = new Event<unit>()
do 
    playerHasAtlas <- (fun() ->
        if IsCurrentlyBook() then
            IsBookAnAtlas() && playerComputedStateSummary.HaveBookOrShield
        else  // boomstick seed
            IsBookAnAtlas() && playerProgressAndTakeAnyHearts.PlayerHasBoomBook.Value()
        )
    IsCurrentlyBookChanged.Add(fun _ -> playerHasAtlasChanged.Trigger())
    IsBookAnAtlasChanged.Add(fun _ -> playerHasAtlasChanged.Trigger())
    playerProgressAndTakeAnyHearts.PlayerHasBoomBook.Changed.Add(fun _ -> playerHasAtlasChanged.Trigger())
    playerComputedStateSummaryForPlayerHasBookChanged.Publish.Add(fun _ -> playerHasAtlasChanged.Trigger())

//////////////////////////////////////////////////////////////////////////////////////////
// Map

let mutable owInstance = new OverworldData.OverworldInstance(OverworldData.OWQuest.FIRST)
let overworldQuestChanged = new Event<unit>()   // fired when reinitializeOverworld switches to a new quest

let mapLastChangedTime = new LastChangedTime()
let shopStash : int[] = Array.create 6 -1   // items entered in a shop window (index = shop label 1-5) while no tile of that shop is on the map: packed like a tile's icons, picked up by the first tile that becomes that shop
let owBombDroppers = Array2D.create 16 8 false   // bomb dropper screens: their own layer (a small badge in the tile's corner), independent of the marker icons / shops
let owSwordIcons = Array2D.create 16 8 -1   // decorative marker icons per overworld tile: up to 3 packed into one int (see decorIconsOf/decorIconsPack below), -1 means none
let DECOR_BOMB_DROPPER = 101   // marker-icon id for a screen whose enemies drop bombs
let DECOR_BOMB = 100   // marker-icon id for the generic bomb (it is not a tracked ITEMS entry, so it lives outside that id range)
let MaxDecorIcons = 3
// each icon takes 7 bits holding (id+1); 0 means an empty slot. Ids are ITEMS ids (0-29) or DECOR_BOMB.
// bits 21-23 hold a 'collected' flag for each of the three slots
// bombs and potions on the overworld are just markers: they never need to be (and cannot be) checked off as collected
let decorNeedsCollecting(ident:int) = not (ident = DECOR_BOMB || ident = DECOR_BOMB_DROPPER || ident = ITEMS.OLDKEY || ident = ITEMS.BLUEPOTION || ident = ITEMS.REDPOTION)
let decorEntriesOf(packed:int) : (int*bool) list =
    if packed <= 0 then [] else [ for k in 0..MaxDecorIcons-1 do
                                    let v = (packed >>> (7*k)) &&& 127
                                    if v > 0 then yield (v-1, ((packed >>> (21+k)) &&& 1) = 1 && decorNeedsCollecting (v-1)) ]
let decorEntriesPack(entries:(int*bool) list) : int =
    if entries.IsEmpty then -1 else entries |> List.truncate MaxDecorIcons |> List.mapi (fun k (ident,col) -> ((ident+1) <<< (7*k)) ||| (if col then 1 <<< (21+k) else 0)) |> List.sum
let decorIconsOf(packed:int) : int list = decorEntriesOf packed |> List.map fst
let decorIconsPack(idents:int list) : int = idents |> List.map (fun i -> (i,false)) |> decorEntriesPack
// remove the icon if the tile already has it; otherwise add it (uncollected) if there is room (unchanged when the tile is full)
let decorIconsToggle(packed:int, ident:int) : int =
    let cur = decorEntriesOf packed
    // a potion marker draws as two icons (blue and red), so a potion tile holds just one more marker icon, which goes in the middle between them
    let limit = MaxDecorIcons   // (the blue potion marker is just a vanity icon like any other: no special limit)
    if ident = ITEMS.HEARTCONTAINER then   // heart containers can repeat on a tile (e.g. a cave with several), each copy checked off separately
        if cur.Length < limit then decorEntriesPack (cur @ [(ident,false)]) else packed
    elif cur |> List.exists (fun (x,_) -> x = ident) then cur |> List.filter (fun (x,_) -> x <> ident) |> decorEntriesPack
    elif cur.Length < limit then decorEntriesPack (cur @ [(ident,false)])
    else packed
// Shop labels: a tile can carry a "SHOP n" text label (n = 1-5), stored in bits 24-26 of the same packed int. The label is just text, not an icon,
// so it never takes one of the MaxDecorIcons item slots (the shop's items are the normal marker icons on the tile). 0 = no label.
let MaxShopLabelTiles = 3   // up to 3 tiles per shop number
let shopLabelOf(packed:int) : int = if packed <= 0 then 0 else (packed >>> 24) &&& 7
let withShopLabel(packed:int, label:int) : int =
    let basePacked = if packed <= 0 then 0 else packed &&& 0xFFFFFF
    let p = basePacked ||| ((label &&& 7) <<< 24)
    if p = 0 then -1 else p
let shopLabelCount(label:int) : int =   // how many tiles currently carry this shop label
    let mutable n = 0
    for i = 0 to 15 do
        for j = 0 to 7 do
            if shopLabelOf owSwordIcons.[i,j] = label then n <- n + 1
    n
// collected (checked-off) state of the marker icons on a tile
let decorIsCollected(packed:int, ident:int) = decorEntriesOf packed |> List.exists (fun (x,c) -> x = ident && c)
// Some ident: flip that one icon; None: if every icon is collected un-collect them all, otherwise collect them all
let decorToggleCollected(packed:int, identOpt:int option) : int =
    let cur = decorEntriesOf packed
    match identOpt with
    | Some ident -> cur |> List.map (fun (x,c) -> if x = ident && decorNeedsCollecting x then (x, not c) else (x,c)) |> decorEntriesPack
    | None ->
        let allDone = cur |> List.forall (fun (x,c) -> c || not (decorNeedsCollecting x))
        cur |> List.map (fun (x,_) -> (x, decorNeedsCollecting x && not allDone)) |> decorEntriesPack
// the n-th (0-based) copy of this icon on the tile: flip just its collected state / remove just that copy
let decorToggleCollectedNth(packed:int, ident:int, n:int) : int =
    if not (decorNeedsCollecting ident) then packed else
    let mutable seen = -1
    decorEntriesOf packed |> List.map (fun (x,c) -> if x = ident then (seen <- seen + 1; if seen = n then (x, not c) else (x,c)) else (x,c)) |> decorEntriesPack
let decorRemoveNth(packed:int, ident:int, n:int) : int =
    let mutable seen = -1
    decorEntriesOf packed |> List.filter (fun (x,_) -> if x = ident then (seen <- seen + 1; seen <> n) else true) |> decorEntriesPack
let overworldMapCircles = Array2D.create 16 8 0   // 0 means none, 1 means just circle, 48-57 means circle with 0-9 label, 65-90 means circle with A-Z label; +100 of those or +200 of those changes color
let toggleOverworldMapCircle(i,j) =
    overworldMapCircles.[i,j] <- 
        if overworldMapCircles.[i,j]%100=0 then 
            overworldMapCircles.[i,j]+1 
        else 
            overworldMapCircles.[i,j] - overworldMapCircles.[i,j]%100
let nextOverworldMapCircleColor(i,j) =
    overworldMapCircles.[i,j] <- overworldMapCircles.[i,j] + 100
    if overworldMapCircles.[i,j] >= 300 then
        overworldMapCircles.[i,j] <- overworldMapCircles.[i,j] - 300
let prevOverworldMapCircleColor(i,j) =
    overworldMapCircles.[i,j] <- overworldMapCircles.[i,j] + 200
    if overworldMapCircles.[i,j] >= 300 then
        overworldMapCircles.[i,j] <- overworldMapCircles.[i,j] - 300
let mutable overworldMapMarks : Cell[,] = null
// a wood sword cave tile that is changed to something else (cleared, re-marked...) gives back the 'shuffled' item that was found there
let pruneShuffledWoodSwordTiles() =
    for (i,j) in Seq.toArray shuffledWoodSwordTiles do
        if overworldMapMarks <> null && overworldMapMarks.[i,j].Current() <> MapSquareChoiceDomainHelper.SWORD1 then
            shuffledWoodSwordTiles.Remove((i,j)) |> ignore
            sword1Box.Set(-1, PlayerHas.SKIPPED)
            recomputePlayerStateSummary()
mapSquareChangedStable.Publish.Add(fun (_,key) -> if key = MapSquareChoiceDomainHelper.SWORD1 then pruneShuffledWoodSwordTiles())
let private overworldMapExtraData = Array2D.init 16 8 (fun _ _ -> Array.zeroCreate (MapSquareChoiceDomainHelper.DARK_X+1))
// Per-shop price notes: 0 = not set, positive value = rupee price the user recorded for that shop
let overworldShopPrices = Array2D.create 16 8 0
let getShopPrice(i,j) = overworldShopPrices.[i,j]
let setShopPrice(i,j,v:int) =
    overworldShopPrices.[i,j] <- v
    mapLastChangedTime.SetNow()
// extra data key-value store, used by
//  - 3-item shops to store the second and third items; key for all shops is SHOP
//    encoding: ed = item2_1based + 9 * item3_1based  (0=none, 1-8=item)
//    backward compat: old saves with ed ∈ {0..8} decode as item2=ed, item3=0
//  - various others store a brightness toggle, key is <mapstate>, value is 0 or <mapstate>
let getOverworldMapExtraData(i,j,k) =
#if DEBUG
    let cur = overworldMapMarks.[i,j].Current()
    if cur=k || (MapSquareChoiceDomainHelper.IsItem(cur) && k=MapSquareChoiceDomainHelper.SHOP) then
        () // ok
    else
        printfn "dodgy, but there are legal times to be here, e.g. popup redrawing-on-hover when changing from non-shop to shop"  // put a breakpoint here for debugging
#endif
    if k < 0 || k > MapSquareChoiceDomainHelper.DARK_X then
        0  // or some other sensible default fallback
    else
        overworldMapExtraData.[i,j].[k]
let setOverworldMapExtraData(i,j,k,v) =
    if k >= 0 && k <= MapSquareChoiceDomainHelper.DARK_X then
        overworldMapExtraData.[i,j].[k] <- v
        mapLastChangedTime.SetNow()
// Helpers for 3-item shop encoding: ed = item2_1based + 9 * item3_1based
let getShopItem2_1based(i,j) = getOverworldMapExtraData(i,j,MapSquareChoiceDomainHelper.SHOP) % 9
let getShopItem3_1based(i,j) = getOverworldMapExtraData(i,j,MapSquareChoiceDomainHelper.SHOP) / 9
let setShopItems(i,j,item2_1based,item3_1based) = setOverworldMapExtraData(i,j,MapSquareChoiceDomainHelper.SHOP, item2_1based + 9*item3_1based)
let NOTFOUND = (-1,-1)
let magsCaveFound, woodSwordCaveFound, foundBlueRingShop, foundBookShop, foundCandleShop, foundArrowShop, foundBombShop, havePotionLetter = 
    new EventingBool(false), new EventingBool(false), new EventingBool(false), new EventingBool(false), new EventingBool(false), new EventingBool(false), new EventingBool(false), new EventingBool(false)
type MapStateSummary(dungeonLocations,anyRoadLocations,armosLocation,sword3Location,sword2Location,sword1Location,owSpotsRemain,owGettableLocations,
                        owWhistleSpotsRemain,owPowerBraceletSpotsRemain,owRouteworthySpots,firstQuestOnlyInterestingMarks,secondQuestOnlyInterestingMarks) =
    member _this.DungeonLocations = dungeonLocations
    member _this.AnyRoadLocations = anyRoadLocations
    member _this.ArmosLocation = armosLocation
    member _this.Sword3Location = sword3Location
    member _this.Sword2Location = sword2Location
    member _this.Sword1Location = sword1Location
    member _this.OwSpotsRemain = owSpotsRemain
    member _this.OwGettableLocations = owGettableLocations
    member _this.OwWhistleSpotsRemain = owWhistleSpotsRemain
    member _this.OwPowerBraceletSpotsRemain = owPowerBraceletSpotsRemain
    member _this.OwRouteworthySpots = owRouteworthySpots
    member _this.FirstQuestOnlyInterestingMarks = firstQuestOnlyInterestingMarks
    member _this.SecondQuestOnlyInterestingMarks = secondQuestOnlyInterestingMarks
let mutable mapStateSummary = MapStateSummary(null,null,NOTFOUND,NOTFOUND,NOTFOUND,NOTFOUND,0,Array2D.zeroCreate 16 8,null,0,null,null,null)
let owSpotTypeCounts : int[] = Array.zeroCreate 7   // remaining unmarked OW spots by type: burn, bomb, push, ladder, raft, whistle, open
let mapStateSummaryComputedEvent = new Event<_>()
let mapStateSummaryLastComputedTime = new LastChangedTime()
let recomputeMapStateSummary() =
    let dungeonLocations = Array.create 9 NOTFOUND
    let anyRoadLocations = Array.create 4 NOTFOUND
    let mutable armosLocation = NOTFOUND
    let mutable sword3Location = NOTFOUND
    let mutable sword2Location = NOTFOUND
    let mutable sword1Location = NOTFOUND
    let mutable owSpotsRemain = 0
    let owGettableLocations = Array2D.zeroCreate 16 8
    let owWhistleSpotsRemain = ResizeArray()
    let mutable owPowerBraceletSpotsRemain = 0
    let mutable owRouteworthySpots = Array2D.create 16 8 false
    let firstQuestOnlyInterestingMarks = Array2D.zeroCreate 16 8
    let secondQuestOnlyInterestingMarks = Array2D.zeroCreate 16 8
    let mutable magsCaveFound_, woodSwordCaveFound_, foundBlueRingShop_, foundBookShop_, foundCandleShop_, foundArrowShop_, foundBombShop_, havePotionLetter_ = 
                            false, false, false, false, false, false, false, false
    for i = 0 to 15 do
        for j = 0 to 7 do
            if not(owInstance.AlwaysEmpty(i,j)) then
                match overworldMapMarks.[i,j].Current() with
                | x when x>=0 && x<9 -> 
                    dungeonLocations.[x] <- i,j
                    if x < 8 && not(GetDungeon(x).IsComplete) then
                        owRouteworthySpots.[i,j] <- true
                    let mutable playerHasAllTriforce = true
                    for i = 0 to 7 do
                        if not(GetDungeon(i).PlayerHasTriforce()) then
                            playerHasAllTriforce <- false
                    if x = 8 && playerHasAllTriforce then
                        owRouteworthySpots.[i,j] <- true
                | x when x>=9 && x<13 -> 
                    anyRoadLocations.[x-9] <- i,j
                | x when x=MapSquareChoiceDomainHelper.SWORD3 -> 
                    magsCaveFound_ <- true
                    sword3Location <- i,j
                    if not(playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Value()) && playerComputedStateSummary.PlayerHearts >= 10 then
                        owRouteworthySpots.[i,j] <- true
                | x when x=MapSquareChoiceDomainHelper.SWORD2 -> 
                    sword2Location <- i,j
                    if not playerComputedStateSummary.HaveWhiteSwordItem && playerComputedStateSummary.PlayerHearts >= 4 then
                        owRouteworthySpots.[i,j] <- true
                | x when x=MapSquareChoiceDomainHelper.SWORD1 -> 
                    // TODO if not took item, is routeworthy
                    woodSwordCaveFound_ <- true
                    sword1Location <- i,j
                | n when n=MapSquareChoiceDomainHelper.ARMOS -> 
                    armosLocation <- i,j
                    if not(armosBox.IsDone()) then
                        owRouteworthySpots.[i,j] <- true
                | -1 ->
                    owSpotsRemain <- owSpotsRemain + 1
                    if owInstance.Whistleable(i,j) then
                        owWhistleSpotsRemain.Add(i,j)
                    if owInstance.PowerBraceletable(i,j) then
                        owPowerBraceletSpotsRemain <- owPowerBraceletSpotsRemain + 1
                    if (owInstance.Whistleable(i,j) && not playerComputedStateSummary.HaveRecorder) ||
                        (owInstance.PowerBraceletable(i,j) && not playerComputedStateSummary.HavePowerBracelet) ||
                        (owInstance.Ladderable(i,j) && not playerComputedStateSummary.HaveLadder) ||
                        (owInstance.Raftable(i,j) && not playerComputedStateSummary.HaveRaft) ||
                        owInstance.HintLocked(i,j) ||
                        (owInstance.Bombable(i,j) && not(playerProgressAndTakeAnyHearts.PlayerHasBombs.Value())) ||
                        (owInstance.Burnable(i,j) && playerComputedStateSummary.CandleLevel=0) then
                        ()  // not routeworthy, as the player can't uncover the spot... except...
                        if i=15 && j=2 && TrackerModelOptions.Overworld.DrawRoutes.Value && (not TrackerModelOptions.RaceMode.Value) && TrackerModelOptions.Overworld.RoutesCanScreenScroll.Value && MirrorOverworld then
                            owRouteworthySpots.[i,j] <- true  // can screen scroll to coast island; even though is out-of-logic, is good to teach that is possible
                    else
                        owRouteworthySpots.[i,j] <- true
                        owGettableLocations.[i,j] <- true
                | n when n=MapSquareChoiceDomainHelper.DARK_X ->
                    if getOverworldMapExtraData(i, j, n)=n then
                        owSpotsRemain <- owSpotsRemain + 1         // un-revealed spots count as remaining
                | n -> 
                    if MapSquareChoiceDomainHelper.IsItem(n) then // shop
                        let item2 = getShopItem2_1based(i,j)
                        let item3 = getShopItem3_1based(i,j)
                        let hasItem(item) = n = item || item2 = MapSquareChoiceDomainHelper.ToItem(item) || item3 = MapSquareChoiceDomainHelper.ToItem(item)
                        if hasItem(MapSquareChoiceDomainHelper.BLUE_RING)   then foundBlueRingShop_ <- true
                        if hasItem(MapSquareChoiceDomainHelper.BOOK)        then foundBookShop_ <- true
                        if hasItem(MapSquareChoiceDomainHelper.BLUE_CANDLE) then foundCandleShop_ <- true
                        if hasItem(MapSquareChoiceDomainHelper.ARROW)       then foundArrowShop_ <- true
                        if hasItem(MapSquareChoiceDomainHelper.BOMB)        then foundBombShop_ <- true
                    else
                        if n=MapSquareChoiceDomainHelper.THE_LETTER && getOverworldMapExtraData(i,j,MapSquareChoiceDomainHelper.THE_LETTER)=MapSquareChoiceDomainHelper.THE_LETTER then
                            havePotionLetter_ <- true
                let isInteresting = overworldMapMarks.[i,j].Current() <> -1 && overworldMapMarks.[i,j].Current() <> mapSquareChoiceDomain.MaxKey
                if OverworldData.owMapSquaresSecondQuestOnly.[j].Chars(i) = 'X' then 
                    secondQuestOnlyInterestingMarks.[i,j] <- isInteresting 
                if OverworldData.owMapSquaresFirstQuestOnly.[j].Chars(i) = 'X' then 
                    firstQuestOnlyInterestingMarks.[i,j] <- isInteresting 
    owRouteworthySpots.[15,5] <- playerComputedStateSummary.HaveLadder && not playerComputedStateSummary.HaveCoastItem // gettable coast item is routeworthy
    magsCaveFound.Value <- magsCaveFound_
    woodSwordCaveFound.Value <- woodSwordCaveFound_
    foundBlueRingShop.Value <- foundBlueRingShop_
    foundBookShop.Value <- foundBookShop_
    foundCandleShop.Value <- foundCandleShop_
    foundArrowShop.Value <- foundArrowShop_
    foundBombShop.Value <- foundBombShop_
    havePotionLetter.Value <- havePotionLetter_
    mapStateSummary <- MapStateSummary(dungeonLocations,anyRoadLocations,armosLocation,sword3Location,sword2Location,sword1Location,owSpotsRemain,owGettableLocations,
                                        owWhistleSpotsRemain,owPowerBraceletSpotsRemain,owRouteworthySpots,firstQuestOnlyInterestingMarks,secondQuestOnlyInterestingMarks)
    mapStateSummaryLastComputedTime.SetNow()
    mapStateSummaryComputedEvent.Trigger()

//////////////////////////////////////////////////////////////////////////////////////////
// Dungeon blockers

// only some of these can participate in semantic reminders, obviously...
[<RequireQualifiedAccess>]
type DungeonBlocker =
    | BOW_AND_ARROW
    | RECORDER
    | LADDER  
    | KEY
    | BAIT    
    | MONEY     // money or life room? bomb upgrade?
    | BOMB
    | COMBAT    // need better weapon/armor
    | MAYBE_BOW_AND_ARROW
    | MAYBE_RECORDER
    | MAYBE_LADDER  
    | MAYBE_KEY
    | MAYBE_BAIT    
    | MAYBE_MONEY
    | MAYBE_BOMB
    | NOTHING
    static member All = [| 
        DungeonBlocker.BOW_AND_ARROW
        DungeonBlocker.RECORDER
        DungeonBlocker.LADDER  
        DungeonBlocker.KEY
        DungeonBlocker.BAIT    
        DungeonBlocker.MONEY
        DungeonBlocker.BOMB
        DungeonBlocker.COMBAT
        DungeonBlocker.MAYBE_BOW_AND_ARROW
        DungeonBlocker.MAYBE_RECORDER
        DungeonBlocker.MAYBE_LADDER  
        DungeonBlocker.MAYBE_KEY
        DungeonBlocker.MAYBE_BAIT    
        DungeonBlocker.MAYBE_MONEY
        DungeonBlocker.MAYBE_BOMB
        DungeonBlocker.NOTHING
        |]
    static member FromHotKeyName(hkn) =
        let mutable r = DungeonBlocker.NOTHING
        for db in DungeonBlocker.All do
            if db.AsHotKeyName()=hkn then
                r <- db
        r
    member this.PlayerCouldBeBlockedByThis() =
        match this.HardCanonical() with
        | DungeonBlocker.LADDER -> not playerComputedStateSummary.HaveLadder
        | DungeonBlocker.RECORDER -> not playerComputedStateSummary.HaveRecorder
        | DungeonBlocker.BOW_AND_ARROW -> not (playerComputedStateSummary.HaveBow && playerComputedStateSummary.ArrowLevel > 0)
        | DungeonBlocker.KEY -> not playerComputedStateSummary.HaveAnyKey
        | _ -> true
    member this.AsHotKeyName() =
        match this with
        | DungeonBlocker.COMBAT -> "Blocker_Combat"
        | DungeonBlocker.BOW_AND_ARROW -> "Blocker_Bow_And_Arrow"
        | DungeonBlocker.RECORDER -> "Blocker_Recorder"
        | DungeonBlocker.LADDER -> "Blocker_Ladder"
        | DungeonBlocker.BAIT -> "Blocker_Bait"
        | DungeonBlocker.KEY -> "Blocker_Key"
        | DungeonBlocker.BOMB -> "Blocker_Bomb"
        | DungeonBlocker.MONEY -> "Blocker_Money"
        | DungeonBlocker.MAYBE_BOW_AND_ARROW -> "Blocker_Maybe_Bow_And_Arrow"
        | DungeonBlocker.MAYBE_RECORDER -> "Blocker_Maybe_Recorder"
        | DungeonBlocker.MAYBE_LADDER -> "Blocker_Maybe_Ladder"
        | DungeonBlocker.MAYBE_BAIT -> "Blocker_Maybe_Bait"
        | DungeonBlocker.MAYBE_KEY -> "Blocker_Maybe_Key"
        | DungeonBlocker.MAYBE_BOMB -> "Blocker_Maybe_Bomb"
        | DungeonBlocker.MAYBE_MONEY  -> "Blocker_Maybe_Money"
        | DungeonBlocker.NOTHING -> "Blocker_Nothing"
    member this.HardCanonical() =
        match this with
        | DungeonBlocker.MAYBE_BOW_AND_ARROW -> DungeonBlocker.BOW_AND_ARROW
        | DungeonBlocker.MAYBE_RECORDER -> DungeonBlocker.RECORDER
        | DungeonBlocker.MAYBE_LADDER -> DungeonBlocker.LADDER
        | DungeonBlocker.MAYBE_BAIT -> DungeonBlocker.BAIT
        | DungeonBlocker.MAYBE_KEY -> DungeonBlocker.KEY
        | DungeonBlocker.MAYBE_BOMB -> DungeonBlocker.BOMB
        | DungeonBlocker.MAYBE_MONEY -> DungeonBlocker.MONEY 
        | x -> x
    member this.DisplayDescription() =
        match this with
        | DungeonBlocker.COMBAT -> "Need better\nweapon/armor"
        | DungeonBlocker.BOW_AND_ARROW -> "Need bow&arrow"
        | DungeonBlocker.MAYBE_BOW_AND_ARROW -> "Might need bow&arrow"
        | DungeonBlocker.RECORDER -> "Need recorder"
        | DungeonBlocker.MAYBE_RECORDER -> "Might need recorder"
        | DungeonBlocker.LADDER -> "Need ladder"
        | DungeonBlocker.MAYBE_LADDER -> "Might need ladder"
        | DungeonBlocker.BAIT -> "Need meat"
        | DungeonBlocker.MAYBE_BAIT -> "Might need meat"
        | DungeonBlocker.KEY -> "Need keys"
        | DungeonBlocker.MAYBE_KEY -> "Might need keys"
        | DungeonBlocker.BOMB -> "Need bombs"
        | DungeonBlocker.MAYBE_BOMB -> "Might need bombs"
        | DungeonBlocker.MONEY -> "Need money\n(e.g. mugger)"
        | DungeonBlocker.MAYBE_MONEY -> "Might need money\n(e.g. mugger)"
        | DungeonBlocker.NOTHING -> "Unmarked"
    member this.Next() =
        let i = DungeonBlocker.All |> Array.findIndex (fun x -> x = this)
        let j = (i + 1) % DungeonBlocker.All.Length
        DungeonBlocker.All.[j]
    member this.Prev() =
        let i = DungeonBlocker.All |> Array.findIndex (fun x -> x = this)
        let j = (DungeonBlocker.All.Length + i - 1) % DungeonBlocker.All.Length
        DungeonBlocker.All.[j]
[<RequireQualifiedAccess>]
type CombatUnblockerDetail =
    | BETTER_SWORD
    | BETTER_ARMOR
    | WAND
type DungeonBlockerAppliesTo() =
    static member MAX = 6
    member val Data = Array.create DungeonBlockerAppliesTo.MAX false // map, compass, tri, box1, box2, box3
type DungeonBlockersContainer() =
    static let dungeonBlockers = Array2D.create 8 DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON DungeonBlocker.NOTHING  // Note: we don't need to LastComputedTime-invalidate anything when the blocker set changes
    static let appliesTo = Array2D.init 8 DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON (fun _ _ -> new DungeonBlockerAppliesTo())
    static let mutable currentlyIgnoringChangesDuringLoad = false
    static let changed = new Event<unit>()
    static member StartIgnoreChangesDuringLoad() = currentlyIgnoringChangesDuringLoad <- true
    static member FinishIgnoreChangesDuringLoad() = currentlyIgnoringChangesDuringLoad <- false; changed.Trigger()
    static member AnyBlockerChanged = changed.Publish
    static member GetDungeonBlocker(i,j) = dungeonBlockers.[i,j]
    static member SetDungeonBlocker(i,j,db) = dungeonBlockers.[i,j] <- db; if not currentlyIgnoringChangesDuringLoad then changed.Trigger()
    // i = dungeon index, j = which blocker instance (0-2), k = which item (m/c/t/1/2/3)
    static member GetDungeonBlockerAppliesTo(i,j,k) = appliesTo.[i,j].Data.[k]
    static member SetDungeonBlockerAppliesTo(i,j,k,b) = appliesTo.[i,j].Data.[k] <- b; if not currentlyIgnoringChangesDuringLoad then changed.Trigger()
    static member AsJsonString(i,j) = 
        let body = appliesTo.[i,j].Data |> Array.map (fun b -> b.ToString().ToLowerInvariant()) |> Array.fold (fun s x -> s+x+", ") ""
        sprintf """{ "Kind": "%s", "AppliesTo": [ %s ] }""" (dungeonBlockers.[i,j].AsHotKeyName()) (body.Substring(0, body.Length-2))
    static member MAX_BLOCKERS_PER_DUNGEON = 3

//////////////////////////////////////////////////////////////////////////////////////////

let recomputeWhatIsNeeded() =
    let mutable changed = false
    if playerProgressLastChangedTime.Time > playerComputedStateSummaryLastComputedTime.Time ||
        extrasLastChangedTime.Time > playerComputedStateSummaryLastComputedTime.Time ||
        dungeonsAndBoxesLastChangedTime.Time > playerComputedStateSummaryLastComputedTime.Time then
        recomputePlayerStateSummary()
        changed <- true
    if playerComputedStateSummaryLastComputedTime.Time > mapStateSummaryLastComputedTime.Time ||
        mapLastChangedTime.Time > mapStateSummaryLastComputedTime.Time then
        recomputeMapStateSummary()
        changed <- true
    changed

//////////////////////////////////////////////////////////////////////////////////////////
// Other minor bits
    
let mutable recorderToNewDungeons = true
let mutable recorderToUnbeatenDungeons = false
let mutable startIconX,startIconY = NOTFOUND  // UI can poke and display these
let mutable customWaypointX,customWaypointY = NOTFOUND  // UI can poke and display these

[<RequireQualifiedAccess>]
type HintZone =
    | UNKNOWN
    | DEATH_MOUNTAIN
    | LAKE
    | LOST_HILLS
    | RIVER
    | GRAVE
    | DESERT
    | COAST
    | DEAD_WOODS
    | NEAR_START
    | FOREST
    static member All = [|
        HintZone.UNKNOWN
        HintZone.DEATH_MOUNTAIN
        HintZone.LAKE
        HintZone.LOST_HILLS
        HintZone.RIVER
        HintZone.GRAVE
        HintZone.DESERT
        HintZone.COAST
        HintZone.DEAD_WOODS
        HintZone.NEAR_START
        HintZone.FOREST
        |]
    member this.AsHotKeyName() =
        match this with
        | UNKNOWN -> "HintZone_Unknown"
        | DEATH_MOUNTAIN -> "HintZone_DeathMountain"
        | LAKE -> "HintZone_Lake"
        | LOST_HILLS -> "HintZone_LostHills"
        | RIVER -> "HintZone_River"
        | GRAVE -> "HintZone_Grave"
        | DESERT -> "HintZone_Desert"
        | COAST -> "HintZone_Coast"
        | DEAD_WOODS -> "HintZone_DeadWoods"
        | NEAR_START -> "HintZone_CloseToStart"
        | FOREST -> "HintZone_Forest"
    member this.AsDataChar() =  // as per OverworldData.owMapZone
        match this with
        | UNKNOWN -> '_'
        | DEATH_MOUNTAIN -> 'M'
        | LAKE -> 'L'
        | LOST_HILLS -> 'H'
        | RIVER -> 'R'
        | GRAVE -> 'G'
        | DESERT -> 'D'
        | COAST -> 'C'
        | DEAD_WOODS -> 'W'
        | NEAR_START -> 'S'
        | FOREST -> 'F'
    member this.AsDisplayTwoChars() =
        match this with
        | UNKNOWN -> "UN"
        | DEATH_MOUNTAIN -> "DM"
        | LAKE -> "LK"
        | LOST_HILLS -> "LH"
        | RIVER -> "RI"
        | GRAVE -> "GR"
        | DESERT -> "DE"
        | COAST -> "CO"
        | DEAD_WOODS -> "DW"
        | NEAR_START -> "ST"
        | FOREST -> "FO"
    override this.ToString() =
        match this with
        | UNKNOWN -> "(Unknown)"
        | DEATH_MOUNTAIN -> "Death Mountain"
        | LAKE -> "Lake"
        | LOST_HILLS -> "Lost Hills"
        | RIVER -> "River"
        | GRAVE -> "Grave"
        | DESERT -> "Desert"
        | COAST -> "Coast"
        | DEAD_WOODS -> "Dead Woods"
        | NEAR_START -> "Close to Start"
        | FOREST -> "Forest"
    static member FromIndex(i) =
        match i with
        | 0 -> UNKNOWN
        | 1 -> DEATH_MOUNTAIN
        | 2 -> LAKE
        | 3 -> LOST_HILLS
        | 4 -> RIVER
        | 5 -> GRAVE
        | 6 -> DESERT
        | 7 -> COAST
        | 8 -> DEAD_WOODS
        | 9 -> NEAR_START
        | 10 -> FOREST
        | _ -> failwith "bad HintZone index"
    member this.ToIndex() =
        match this with
        | UNKNOWN -> 0
        | DEATH_MOUNTAIN -> 1
        | LAKE -> 2
        | LOST_HILLS -> 3
        | RIVER -> 4
        | GRAVE -> 5
        | DESERT -> 6
        | COAST -> 7
        | DEAD_WOODS -> 8
        | NEAR_START -> 9
        | FOREST -> 10
let GetLevelHint, SetLevelHint, LevelHintChanged = 
    let levelHints = Array.create 13 HintZone.UNKNOWN   // 0-8 is L1-9, 9 is WS, 10 is MS, 11 is ladder item, 12 is armos item (the last two are not saved)
    let levelHintChangeEvents = Array.init 13 (fun _ -> new Event<_>())
    let GetLevelHint(i) = levelHints.[i]
    let SetLevelHint(i,v) = levelHints.[i] <- v; levelHintChangeEvents.[i].Trigger(v)
    let LevelHintChanged(i) = levelHintChangeEvents.[i].Publish
    GetLevelHint, SetLevelHint, LevelHintChanged
let mutable NoFeatOfStrengthHintWasGiven = false
let mutable SailNotHintWasGiven = false
let mutable SailToHintWasGiven = false
let mutable PlayMelodyHintWasGiven = false
let mutable StepOverWaterHintWasGiven = false
let mutable FireArrowsHintWasGiven = false

let mutable currentlyIgnoringForceUpdatesDuringALoad = false
let forceUpdate() = 
    if not currentlyIgnoringForceUpdatesDuringALoad then
        // UI can force an update for a few bits that we don't model well yet
        // TODO ideally dont want this, feels like kludge?
        mapLastChangedTime.SetNow()
                
//////////////////////////////////////////////////////////////////////////////////////////

let unreachablePossibleDungeonSpotCount() =
    let mutable count = 0
    for x = 0 to 15 do
        for y = 0 to 7 do
            let cur = overworldMapMarks.[x,y].Current()
            if cur < 9 then  // it's marked as a dungeon, or it's unmarked so it might be an unfound dungeon
                if owInstance.Bombable(x,y) && not(playerProgressAndTakeAnyHearts.PlayerHasBombs.Value()) then
                    count <- count + 1
                if owInstance.Burnable(x,y) && not(playerComputedStateSummary.CandleLevel>0) then
                    count <- count + 1
                if owInstance.Raftable(x,y) && not(playerComputedStateSummary.HaveRaft) then
                    count <- count + 1
                if owInstance.Ladderable(x,y) && not(playerComputedStateSummary.HaveLadder) then
                    count <- count + 1
                if owInstance.Whistleable(x,y) && not(playerComputedStateSummary.HaveRecorder) then
                    count <- count + 1
    count
// the level 9 entry item the user picked instead of the 8 triforces (-1 = the default rule), and whether it has been collected (any item box holding it, marked as obtained)
let level9CustomTriggerItem() = (let c = level9TriggerBox.CellCurrent() in if c = ITEMS.TRIFORCE then -1 else c)   // picking the triforce item means the default rule
let level9TriggerCollected() =
    let it = level9CustomTriggerItem()
    it <> -1 &&
        (DungeonTrackerInstance.TheDungeonTrackerInstance.AllBoxes() |> Array.exists (fun b -> b.CellCurrent() = it && b.PlayerHas() = PlayerHas.YES)
         || (let sm = playerComputedStateSummary
             (it = ITEMS.REDRING && sm.RingLevel >= 2) || (it = ITEMS.BLUERING && sm.RingLevel >= 1)   // owned per the item progress (however it was marked)
             || (it = ITEMS.REDCANDLE && sm.CandleLevel >= 2) || (it = ITEMS.BLUECANDLE && sm.CandleLevel >= 1)
             || (it = ITEMS.BOW && sm.HaveBow) || (it = ITEMS.SILVERARROW && sm.ArrowLevel >= 2) || (it = ITEMS.WOODARROW && sm.ArrowLevel >= 1)
             || (it = ITEMS.LADDER && sm.HaveLadder) || (it = ITEMS.RECORDER && sm.HaveRecorder) || (it = ITEMS.RAFT && sm.HaveRaft)
             || (it = ITEMS.POWERBRACELET && sm.HavePowerBracelet) || (it = ITEMS.KEY && sm.HaveAnyKey) || (it = ITEMS.WAND && sm.HaveWand)
             || (it = ITEMS.BOOKSHIELD && sm.HaveBookOrShield) || (it = ITEMS.BOOMERANG && sm.BoomerangLevel >= 1) || (it = ITEMS.MAGICBOOMERANG && sm.BoomerangLevel >= 2)
             || (it = ITEMS.WHITESWORD && sm.SwordLevel >= 2) || (it = ITEMS.MAGICALSWORD && sm.SwordLevel >= 3)
             || (it = ITEMS.WOODSWORD && sm.SwordLevel >= 1) || (it = ITEMS.PROGRESSIVESWORD && progressiveSwordTier() > level9TriggerSwordTier) || (it = ITEMS.WHITESWORD4 && (sm.SwordLevel >= 4 || playerHasSword4.Value = 1)))
         || (it >= 0 && it < mapCollectedCount.Length && mapCollectedCount.[it] > 0)   // checked off on the overworld map
         || [ladderBox; sword2Box; sword3Box; letterBox] |> List.exists (fun b -> b.CellCurrent() = it && b.PlayerHas() = PlayerHas.YES))
type TriforceAndGoSummary() =
    let customTrigger = level9CustomTriggerItem() <> -1
    let customTriggerCollected = level9TriggerCollected()
    // Note: we're just going to assume the player has a sword (or wand in swordless), let's not go nuts with advanced flags and/or unlikely no-sword-but-all-items situations
    let haveBow = playerComputedStateSummary.HaveBow
    let haveSilvers = playerComputedStateSummary.ArrowLevel=2
    let silversKnownToBeInLevel9 = GetDungeon(8).Boxes.[0].CellCurrent()=ITEMS.SILVERARROW || GetDungeon(8).Boxes.[1].CellCurrent()=ITEMS.SILVERARROW
    let haveLadder = playerComputedStateSummary.HaveLadder
    let haveRecorder = playerComputedStateSummary.HaveLadder
    let unreachableCount = unreachablePossibleDungeonSpotCount()
    let mutable missingTriforceFromLocatedDungeonCount = 0   // TODO advanced flags: only need N triforces to enter 9
    let mutable missingDungeonCount = 0
    let tagLevel = 
        for i = 0 to 8 do
            if not customTrigger && not(GetDungeon(i).PlayerHasTriforce()) then   // (a custom level 9 entry item replaces the triforce requirement)
                if not(GetDungeon(i).HasBeenLocated()) then
                    missingDungeonCount <- missingDungeonCount + 1
                elif i <> 8 then // L9 has no triforce
                    missingTriforceFromLocatedDungeonCount <- missingTriforceFromLocatedDungeonCount + 1
        // fewer than 8 triforces needed: that many dungeons may be skipped (unlocated ones first)
        let spare = 8 - level9TriforceCount
        let skipDungeons = min spare missingDungeonCount
        missingDungeonCount <- missingDungeonCount - skipDungeons
        missingTriforceFromLocatedDungeonCount <- max 0 (missingTriforceFromLocatedDungeonCount - (spare - skipDungeons))
        let compute() =
            let mutable score = 100
            score <- score - missingDungeonCount*20                              // big penalty for unlocated dungeon
            score <- score - missingTriforceFromLocatedDungeonCount*8            // smallish penalty for missing triforce in a dungeon you already located
            if not haveBow then score <- score - 35                              // huge penalty for missing bow
            if not haveSilvers then score <- score - 30                          // huge penalty for missing silvers
            if not haveLadder then score <- score - 15                           // medium penalty for missing ladder
            if not haveRecorder then score <- score - 5                          // small penalty for missing recorder
            //printfn "score: %d" score
            if score < 0 then 0 else score
        // you might need e.g. power bracelet or raft to find missing dungeon, so never TAG without being able to locate them all  
        let knowSilvers = haveSilvers || silversKnownToBeInLevel9
        if customTrigger && not customTriggerCollected then
            0   // the item that opens level 9 has not been collected yet: nothing to report
        elif missingDungeonCount=0 || unreachableCount=0 then 
            if haveBow && knowSilvers && haveLadder && haveRecorder then
                103
            elif haveBow && knowSilvers && haveLadder then
                102
            elif haveBow && knowSilvers then
                101
            else
                compute()
        else
            compute()
    member _this.CustomTrigger = customTrigger
    member _this.Level = tagLevel  // 103 TAG, 102 probably-TAG, 101 might-be-TAG, 1-100 see features below, 0 not worth reporting
    member _this.HaveBow = haveBow
    member _this.HaveSilvers = haveSilvers
    member _this.SilversKnownToBeInLevel9 = silversKnownToBeInLevel9
    member _this.HaveLadder = haveLadder
    member _this.HaveRecorder = haveRecorder
    member _this.MissingDungeonCount = missingDungeonCount
type ITrackerEvents =
    // hearts
    abstract CurrentHearts : int -> unit
    abstract AnnounceConsiderSword2 : unit -> unit
    abstract AnnounceConsiderSword3 : unit -> unit
    // map
    abstract OverworldSpotsRemaining : int * int -> unit  // total remaining, currently gettable
    abstract DungeonLocation : int*int*int*bool*bool -> unit   // number 0-7, x, y, hasTri, isCompleted --- to update triforce color (found), completed shading, recorderable/completed map icon
    abstract AnyRoadLocation : int*int*int -> unit // number 0-3, x, y
    abstract Armos : int*int -> unit // x,y
    abstract Sword3 : int*int -> unit // x,y
    abstract Sword2 : int*int -> unit // x,y
    abstract RoutingInfo : bool*bool*seq<(int*int)*int>*seq<int*int>*bool[,] -> unit // haveLadder haveRaft currentRecorderWarpDestinations currentAnyRoadDestinations owRouteworthySpots
    // dungeons
    abstract AnnounceCompletedDungeon : int -> unit
    abstract CompletedDungeons : bool[] -> unit     // for current shading
    abstract AnnounceFoundDungeonCount : int -> unit
    abstract AnnounceTriforceCount : int -> unit
    abstract AnnounceTriforceAndGo : int * TriforceAndGoSummary -> unit
    // blockers
    abstract RemindUnblock : DungeonBlocker * seq<int> * seq<CombatUnblockerDetail> -> unit
    // items
    abstract RemindShortly : int -> unit

// state-transition announcements
let haveAnnouncedHearts = Array.zeroCreate 17
let haveAnnouncedCompletedDungeons = Array.zeroCreate 8
let mutable previouslyAnnouncedFoundDungeonCount = 0
let mutable previouslyAnnouncedTriforceCount = 0
let mutable previouslyLocatedDungeonCount = 0
let mutable remindedLadder, remindedAnyKey = false, false
let mutable priorSwordLevel = 0
let mutable priorSwordWandLevel = 0
let mutable priorRingLevel = 0
let mutable priorBombs = false
let mutable priorBowArrow = false
let mutable priorRecorder = false
let mutable priorLadder = false
let mutable priorAnyKey = false
// timeline data
let timelineDataOverworldSpotsRemain = ResizeArray<int*int>()  // (seconds, numSpotsRemain)
let mutable priorOWSpotsRemain = 0
// triforce-and-go levels
let mutable level9EntryAnnouncement = false   // true only while the "level 9 entry rule just became satisfied" announcement is being sent
let mutable previousLevel9EntryRuleMet = false   // the level 9 entry rule (N triforces, or the item the user picked) was already satisfied at the last check
let mutable previouslyAnnouncedTriforceAndGo = 0  // 0 = no, 1 = might be, 2 = probably, 3 = certainly triforce-and-go
let mutable previousCompletedDungeonCount = 0
let ResetForGroundhogOrRoutersOrFourPlusFourEtc() =
    for i = 0 to haveAnnouncedHearts.Length-1 do
        haveAnnouncedHearts.[i] <- false
    for i = 0 to haveAnnouncedCompletedDungeons.Length-1 do
        haveAnnouncedCompletedDungeons.[i] <- false
    // previouslyAnnouncedFoundDungeonCount // still found
    previouslyAnnouncedTriforceCount <- 0
    // previouslyLocatedDungeonCount // still found
    remindedLadder <- false
    remindedAnyKey <- false
    priorSwordLevel <- 0
    priorSwordWandLevel <- 0
    priorRingLevel <- 0
    // priorBombs // meh
    priorBowArrow <- false
    priorRecorder <- false
    priorLadder <- false
    priorAnyKey <- false
    // priorOWSpotsRemain // still have
    previouslyAnnouncedTriforceAndGo <- 0
    previousLevel9EntryRuleMet <- false
    previousCompletedDungeonCount <- 0
    recomputePlayerStateSummary()
let doesPlayerHaveTriforceAndWhichDungeonIndexIsIt(i) =  // i=0-7, refers to triforce piece 1-8
    if IsHiddenDungeonNumbers() then
        let level = i+1
        let levelLabel = char(int '0' + level)
        let mutable index = -1
        for j = 0 to 7 do
            if GetDungeon(j).LabelChar = levelLabel then
                index <- j
        let mutable hasTriforce = false
        if index <> -1 then
            hasTriforce <- GetDungeon(index).PlayerHasTriforce() 
        hasTriforce <- hasTriforce || startingItemsAndExtras.HDNStartingTriforcePieces.[i].Value()
        hasTriforce, index  // index can be -1 if have tri (e.g. start item) but not located/labeled dungeon
    else
        GetDungeon(i).PlayerHasTriforce(), i
let allUIEventingLogic(ite : ITrackerEvents) =
    // hearts
    let playerHearts = playerComputedStateSummary.PlayerHearts
    ite.CurrentHearts playerHearts
    if playerHearts >=4 && playerHearts <= 6 && not haveAnnouncedHearts.[playerHearts] then
        haveAnnouncedHearts.[playerHearts] <- true
        if not playerComputedStateSummary.HaveWhiteSwordItem && mapStateSummary.Sword2Location<>NOTFOUND then
            ite.AnnounceConsiderSword2()
    if playerHearts >=10 && playerHearts <= 14 && not haveAnnouncedHearts.[playerHearts] then
        haveAnnouncedHearts.[playerHearts] <- true
        if not(playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Value()) && mapStateSummary.Sword3Location<>NOTFOUND then
            ite.AnnounceConsiderSword3()
    // map
    let mutable remain = 0
    for i = 0 to 15 do
        for j = 0 to 7 do
            if mapStateSummary.OwGettableLocations.[i,j] then
                remain <- remain + 1
    // per-type breakdown of the remaining (unmarked) spots: burn, bomb, push, ladder, raft, whistle, open
    for k in 0..6 do owSpotTypeCounts.[k] <- 0
    for i = 0 to 15 do
        for j = 0 to 7 do
            if not(owInstance.AlwaysEmpty(i,j)) && overworldMapMarks.[i,j].Current() = -1 then
                if owInstance.Burnable(i,j) then owSpotTypeCounts.[0] <- owSpotTypeCounts.[0] + 1
                if owInstance.Bombable(i,j) then owSpotTypeCounts.[1] <- owSpotTypeCounts.[1] + 1
                if owInstance.PowerBraceletable(i,j) then owSpotTypeCounts.[2] <- owSpotTypeCounts.[2] + 1
                if owInstance.Ladderable(i,j) then owSpotTypeCounts.[3] <- owSpotTypeCounts.[3] + 1
                if owInstance.Raftable(i,j) then owSpotTypeCounts.[4] <- owSpotTypeCounts.[4] + 1
                if owInstance.Whistleable(i,j) then owSpotTypeCounts.[5] <- owSpotTypeCounts.[5] + 1
                if owInstance.Nothingable(i,j) then owSpotTypeCounts.[6] <- owSpotTypeCounts.[6] + 1
    ite.OverworldSpotsRemaining(mapStateSummary.OwSpotsRemain, remain)
    if mapStateSummary.OwSpotsRemain <> priorOWSpotsRemain then
        priorOWSpotsRemain <- mapStateSummary.OwSpotsRemain
        let span = System.DateTime.Now - theStartTime.Time
        let s = int span.TotalSeconds
        timelineDataOverworldSpotsRemain.Add(s, priorOWSpotsRemain) 
    for d = 0 to 8 do
        if mapStateSummary.DungeonLocations.[d] <> NOTFOUND then
            let x,y = mapStateSummary.DungeonLocations.[d]
            ite.DungeonLocation(d, x, y, GetDungeon(d).PlayerHasTriforce(), GetDungeon(d).IsComplete)
    for a = 0 to 3 do
        if mapStateSummary.AnyRoadLocations.[a] <> NOTFOUND then
            let x,y = mapStateSummary.AnyRoadLocations.[a]
            ite.AnyRoadLocation(a, x, y)
    if mapStateSummary.ArmosLocation <> NOTFOUND then
        ite.Armos(mapStateSummary.ArmosLocation)
    if mapStateSummary.Sword3Location <> NOTFOUND then
        ite.Sword3(mapStateSummary.Sword3Location)
    if mapStateSummary.Sword2Location <> NOTFOUND then
        ite.Sword2(mapStateSummary.Sword2Location)
    let vanillaRecorderLocations = OverworldData.vanilla1QDungeonLocations.[0..7]
    let recorderDests = [|
        if playerComputedStateSummary.HaveRecorder then
            for tri = 0 to 7 do
                let hasTri,idx = doesPlayerHaveTriforceAndWhichDungeonIndexIsIt(tri)
                if hasTri <> recorderToUnbeatenDungeons then
                    if recorderToNewDungeons then
                        if idx <> -1 then
                            let pos = mapStateSummary.DungeonLocations.[idx]
                            if pos <> NOTFOUND then
                                yield pos,idx           // Note: in HDN, you might have found dungeon G, but if you have starting triforce 4, and dunno if 4=G, we don't know if can recorder there
                    else
                        yield vanillaRecorderLocations.[tri], tri
        |]
    let anyRoadDests = [|
        for d in mapStateSummary.AnyRoadLocations do
            if d <> NOTFOUND then
                yield d
        |]
    ite.RoutingInfo(playerComputedStateSummary.HaveLadder, playerComputedStateSummary.HaveRaft, recorderDests, anyRoadDests, mapStateSummary.OwRouteworthySpots)
    // dungeons
    for d = 0 to 7 do
        if not haveAnnouncedCompletedDungeons.[d] && GetDungeon(d).IsComplete then
            ite.AnnounceCompletedDungeon(d)
            haveAnnouncedCompletedDungeons.[d] <- true
    ite.CompletedDungeons [| for d = 0 to 7 do yield GetDungeon(d).IsComplete |]
    let mutable numFound = 0
    for d = 0 to 8 do
        if mapStateSummary.DungeonLocations.[d]<>NOTFOUND then
            numFound <- numFound + 1
    if numFound > previouslyAnnouncedFoundDungeonCount then
        ite.AnnounceFoundDungeonCount(numFound)
        previouslyAnnouncedFoundDungeonCount <- numFound
    let triforces = GetTriforceHaves() |> Array.filter id |> Array.length
    let mutable locatedDungeons = 0
    let mutable completedDungeons = 0
    for d = 0 to 8 do
        if GetDungeon(d).IsComplete then
            completedDungeons <- completedDungeons + 1
        if GetDungeon(d).HasBeenLocated() then
            locatedDungeons <- locatedDungeons + 1
    let tagSummary = new TriforceAndGoSummary()
    let mutable justAnnouncedTAG = false
    if triforces > previouslyAnnouncedTriforceCount then
        ite.AnnounceTriforceCount(triforces)
        previouslyAnnouncedTriforceCount <- triforces
        if completedDungeons <= previousCompletedDungeonCount && tagSummary.Level > 101 then
            // just got a new triforce, it did not complete a dungeon, but the player is probably triforce and go, so remind them, so they might abandon rest of dungeon
            ite.AnnounceTriforceAndGo(triforces, tagSummary)
            justAnnouncedTAG <- true
    if locatedDungeons > previouslyLocatedDungeonCount && tagSummary.Level > 101 && not justAnnouncedTAG then
        // just located a new dungeon, the player is probably triforce and go, so remind them
        ite.AnnounceTriforceAndGo(triforces, tagSummary)
        justAnnouncedTAG <- true
    previousCompletedDungeonCount <- completedDungeons
    previouslyLocatedDungeonCount <- locatedDungeons
    if tagSummary.Level > previouslyAnnouncedTriforceAndGo then
        previouslyAnnouncedTriforceAndGo <- tagSummary.Level
        if not justAnnouncedTAG then
            ite.AnnounceTriforceAndGo(triforces, tagSummary)
            justAnnouncedTAG <- true
    // the level 9 entry rule has just been satisfied (enough triforces, or the item the user picked was collected): always play the go-time reminder
    let entryRuleMet = if level9CustomTriggerItem() <> -1 then level9TriggerCollected() else triforces >= level9TriforceCount
    if entryRuleMet && not previousLevel9EntryRuleMet then
        level9EntryAnnouncement <- true
        (try
            if not justAnnouncedTAG || tagSummary.CustomTrigger then   // (for a custom item, the item + check popup always plays)
                ite.AnnounceTriforceAndGo(triforces, tagSummary)
         finally
            level9EntryAnnouncement <- false)
    previousLevel9EntryRuleMet <- entryRuleMet
    // blockers - COMBAT
    let calcFromDungeon(item) =
        let mutable fromDungeon = -1
        for i = 0 to 7 do
            if GetDungeon(i).Boxes |> Array.exists (fun b -> b.CellCurrent() = item) then
                fromDungeon <- i
        fromDungeon
    let combatUnblockers = ResizeArray()
    let combatUnblockerOrigins = ResizeArray()
    if playerComputedStateSummary.SwordLevel > priorSwordWandLevel || 
                (playerComputedStateSummary.SwordLevel>=2 && priorSwordLevel<2) then  // even with prior wand, upgrade to white sword is worthy of reminder, thanks to wizzrobes
        combatUnblockers.Add(CombatUnblockerDetail.BETTER_SWORD)
        if playerComputedStateSummary.SwordLevel = 2 then
            combatUnblockerOrigins.Add(calcFromDungeon(ITEMS.WHITESWORD))
    if playerComputedStateSummary.HaveWand && (priorSwordWandLevel < 2) then
        combatUnblockers.Add(CombatUnblockerDetail.WAND)
        combatUnblockerOrigins.Add(calcFromDungeon(ITEMS.WAND))
    if playerComputedStateSummary.RingLevel > priorRingLevel 
                && (playerComputedStateSummary.SwordLevel>0 || playerComputedStateSummary.HaveWand) then  // armor won't help you win combat if you have 0 weapons
        combatUnblockers.Add(CombatUnblockerDetail.BETTER_ARMOR)
        combatUnblockerOrigins.Add(calcFromDungeon(ITEMS.REDRING))
    if combatUnblockers.Count > 0 then
        let dungeonIdxs = ResizeArray()
        for i = 0 to 7 do
            if combatUnblockerOrigins.Count = 1 && combatUnblockerOrigins.[0] = i then
                () // do nothing, they're already in the dungeon we'd be reminding them to go to
            else
                let mutable anyCombatBlocker = false
                for j = 0 to DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON-1 do
                    if DungeonBlockersContainer.GetDungeonBlocker(i,j) = DungeonBlocker.COMBAT then
                        anyCombatBlocker <- true
                if anyCombatBlocker then
                    if not(GetDungeon(i).IsComplete) then
                        dungeonIdxs.Add(i)
        if dungeonIdxs.Count > 0 then
            if tagSummary.Level < 103 then // no need for blocker-reminder if fully-go-time
                ite.RemindUnblock(DungeonBlocker.COMBAT, dungeonIdxs, combatUnblockers)
    priorSwordLevel <- playerComputedStateSummary.SwordLevel
    priorSwordWandLevel <- max playerComputedStateSummary.SwordLevel (if playerComputedStateSummary.HaveWand then 2 else 0)
    priorRingLevel <- playerComputedStateSummary.RingLevel
    // blockers - generic
    let blockerLogic(db:DungeonBlocker, fromDungeon) =
        let dungeonIdxs = ResizeArray()
        for i = 0 to 7 do
            if i <> fromDungeon then
                let mutable anyMatchingBlocker = false
                for j = 0 to DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON-1 do
                    if DungeonBlockersContainer.GetDungeonBlocker(i,j).HardCanonical() = db.HardCanonical() then
                        anyMatchingBlocker <- true
                if anyMatchingBlocker then
                    if not(GetDungeon(i).IsComplete) then
                        dungeonIdxs.Add(i)
        if dungeonIdxs.Count > 0 then
            if tagSummary.Level < 103 then // no need for blocker-reminder if fully-go-time
                ite.RemindUnblock(db, dungeonIdxs, [])
    // blockers - others
    if not priorBombs && playerProgressAndTakeAnyHearts.PlayerHasBombs.Value() then
        blockerLogic(DungeonBlocker.BOMB, -1)
    priorBombs <- playerProgressAndTakeAnyHearts.PlayerHasBombs.Value()

    if not priorBowArrow && playerComputedStateSummary.HaveBow && playerComputedStateSummary.ArrowLevel>=1 then
        blockerLogic(DungeonBlocker.BOW_AND_ARROW, calcFromDungeon(ITEMS.BOW))   // may still spuriously fire if had bow, got silvers in 6 and 6 was bow blocked
    priorBowArrow <- playerComputedStateSummary.HaveBow && playerComputedStateSummary.ArrowLevel>=1

    if not priorRecorder && playerComputedStateSummary.HaveRecorder then
        blockerLogic(DungeonBlocker.RECORDER, calcFromDungeon(ITEMS.RECORDER))
    priorRecorder <- playerComputedStateSummary.HaveRecorder

    if not priorLadder && playerComputedStateSummary.HaveLadder then
        blockerLogic(DungeonBlocker.LADDER, calcFromDungeon(ITEMS.LADDER))
    priorLadder <- playerComputedStateSummary.HaveLadder

    if not priorAnyKey && playerComputedStateSummary.HaveAnyKey then
        blockerLogic(DungeonBlocker.KEY, calcFromDungeon(ITEMS.KEY))
    priorAnyKey <- playerComputedStateSummary.HaveAnyKey

    // Note: no logic for BAIT or loose KEYs or MONEY, as the tracker has no reliable knowledge of this aspect of player's inventory

    // items
    if not remindedLadder && playerComputedStateSummary.HaveLadder then
        ite.RemindShortly(ITEMS.LADDER)
        remindedLadder <- true
    if not remindedAnyKey && playerComputedStateSummary.HaveAnyKey then
        ite.RemindShortly(ITEMS.KEY)
        remindedAnyKey <- true

///////////////////////////////////////////////////////

[<RequireQualifiedAccess>]
type TimelineItemDescription =   // a way to identify which unique timeline item we are referring to, without any associated 'state' (timestamps, gotten-ness, ...)
    | ExtrasOrShopping of string * BoolProperty
    | TakeAnyHeart of int
    | Triforce of int
    | ItemBox of string * Box
    | UserCustom of string * Event<bool>
    | MapCollected of string   // stamped by the UI when an item is checked off on the overworld map (4th sword, overworld hearts)
    member this.Identifier =
        match this with
        | TimelineItemDescription.ExtrasOrShopping(i,_) -> i
        | TimelineItemDescription.TakeAnyHeart n -> sprintf "TakeAnyHeart%d" (n+1)
        | TimelineItemDescription.Triforce n -> sprintf "Triforce%d" (n+1)
        | TimelineItemDescription.ItemBox(i,_) -> i
        | TimelineItemDescription.UserCustom(s,_) -> s
        | TimelineItemDescription.MapCollected s -> s
// item boxes whose timeline entry is not stamped, because the item they hold was already collected on its own (the overworld item grid box has the timeline entry); see boxItemImpl
let timelineSuppressedBoxes = System.Collections.Generic.HashSet<Box>(HashIdentity.Reference)
type TimelineItemModel(desc: TimelineItemDescription) =
    // TODO keep history of all changes maybe
    static let all = new System.Collections.Generic.Dictionary<_,_>()
    let mutable finishedTotalSeconds = -1
    let mutable itemPlayerHas = PlayerHas.YES             // for non-item-box things, is always YES.  for item boxes, reflects the state of the box, suggests UI display.
    static let timelineChanged = new Event<_>()
    let stamp(b,ph) = 
        let span = System.DateTime.Now - theStartTime.Time
        let s = int span.TotalSeconds
        if b then
            finishedTotalSeconds <- s
        else
            finishedTotalSeconds <- -1
        itemPlayerHas <- ph
        timelineChanged.Trigger(int span.TotalMinutes)

    do
        // listen for changes
        match desc with
        | TimelineItemDescription.ExtrasOrShopping(_,bp) -> bp.Changed.Add(fun _ -> stamp(bp.Value(), PlayerHas.YES))
        | TimelineItemDescription.TakeAnyHeart(i) -> playerProgressAndTakeAnyHearts.TakeAnyHeartChanged.Add(fun x -> if x=i then stamp(playerProgressAndTakeAnyHearts.GetTakeAnyHeart(i)=1, PlayerHas.YES))
        | TimelineItemDescription.Triforce(i) -> GetDungeon(i).PlayerHasTriforceChanged.Add(fun _ -> stamp(GetDungeon(i).PlayerHasTriforce(), PlayerHas.YES))
        | TimelineItemDescription.ItemBox(_,b) -> b.Changed.Add(fun _ -> stamp(b.CellCurrent() <> -1 && b.PlayerHas()<>PlayerHas.NO && not(timelineSuppressedBoxes.Contains(b)), b.PlayerHas()))
        | TimelineItemDescription.UserCustom(_,e) -> e.Publish.Add(fun b -> stamp(b,PlayerHas.YES))
        | TimelineItemDescription.MapCollected _ -> ()
    static member TriggerTimelineChanged() =  // used when Loading a Save file
        let span = System.DateTime.Now - theStartTime.Time
        timelineChanged.Trigger(int span.TotalMinutes)
    member this.SetCollectedFromMap(b:bool) = stamp(b, PlayerHas.YES)   // used for items checked off on the overworld map
    member this.Unstamp() = if finishedTotalSeconds <> -1 then stamp(false, itemPlayerHas)   // take this entry off the timeline (used to avoid two entries for one item)
    member this.ResetTotalSeconds() = if finishedTotalSeconds <> -1 then finishedTotalSeconds <- 0    // used when user presses Reset Timer
    member this.StampTotalSeconds(s,ph) =  // used when Loading a Save file
        match desc with
        | TimelineItemDescription.ItemBox(_,_) -> itemPlayerHas <- ph
        | _ -> ()
        finishedTotalSeconds <- s
    member this.Identifier = desc.Identifier
    member this.FinishedTotalSeconds = finishedTotalSeconds
    member this.Has = itemPlayerHas
    member this.MarkDone(?remoteTimestamp: int, ?remoteHas: PlayerHas) =
        let timestamp =
            defaultArg remoteTimestamp (int ((System.DateTime.Now - theStartTime.Time).TotalSeconds))

        if finishedTotalSeconds = -1 then
            finishedTotalSeconds <- timestamp
            itemPlayerHas <- defaultArg remoteHas PlayerHas.YES
            timelineChanged.Trigger(timestamp / 60)



    static member TimelineChanged = timelineChanged.Publish
    static member All = all
    static member UnstampByIdentifier(identifier:string) =
        match all.TryGetValue(identifier) with
        | true, (m:TimelineItemModel) -> m.Unstamp()
        | _ -> ()
    static member MakeAll() =
        // descriptions
        let all = ResizeArray()
        // shopping
        all.Add(TimelineItemDescription.ExtrasOrShopping("WoodSword", playerProgressAndTakeAnyHearts.PlayerHasWoodSword))
        all.Add(TimelineItemDescription.ExtrasOrShopping("WoodArrow", playerProgressAndTakeAnyHearts.PlayerHasWoodArrow))
        all.Add(TimelineItemDescription.ExtrasOrShopping("BlueCandle", playerProgressAndTakeAnyHearts.PlayerHasBlueCandle))
        all.Add(TimelineItemDescription.ExtrasOrShopping("BlueRing", playerProgressAndTakeAnyHearts.PlayerHasBlueRing))
        all.Add(TimelineItemDescription.ExtrasOrShopping("MagicalSword", playerProgressAndTakeAnyHearts.PlayerHasMagicalSword))   // might be a BU
        all.Add(TimelineItemDescription.ExtrasOrShopping("BoomstickBook", playerProgressAndTakeAnyHearts.PlayerHasBoomBook))
        all.Add(TimelineItemDescription.ExtrasOrShopping("Gannon", playerProgressAndTakeAnyHearts.PlayerHasDefeatedGanon))
        all.Add(TimelineItemDescription.ExtrasOrShopping("Zelda", playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda))
        // extras
        all.Add(TimelineItemDescription.ExtrasOrShopping("WhiteSword", startingItemsAndExtras.PlayerHasWhiteSword))
        all.Add(TimelineItemDescription.ExtrasOrShopping("StartingMagicalSword", startingItemsAndExtras.PlayerHasMagicalSword))   // always mags
        all.Add(TimelineItemDescription.ExtrasOrShopping("SilverArrow", startingItemsAndExtras.PlayerHasSilverArrow))
        all.Add(TimelineItemDescription.ExtrasOrShopping("Bow", startingItemsAndExtras.PlayerHasBow))
        all.Add(TimelineItemDescription.ExtrasOrShopping("Wand", startingItemsAndExtras.PlayerHasWand))
        all.Add(TimelineItemDescription.ExtrasOrShopping("RedCandle", startingItemsAndExtras.PlayerHasRedCandle))
        all.Add(TimelineItemDescription.ExtrasOrShopping("Boomerang", startingItemsAndExtras.PlayerHasBoomerang))
        all.Add(TimelineItemDescription.ExtrasOrShopping("MagicBoomerang", startingItemsAndExtras.PlayerHasMagicBoomerang))
        all.Add(TimelineItemDescription.ExtrasOrShopping("RedRing", startingItemsAndExtras.PlayerHasRedRing))
        all.Add(TimelineItemDescription.ExtrasOrShopping("PowerBracelet", startingItemsAndExtras.PlayerHasPowerBracelet))
        all.Add(TimelineItemDescription.ExtrasOrShopping("Ladder", startingItemsAndExtras.PlayerHasLadder))
        all.Add(TimelineItemDescription.ExtrasOrShopping("Raft", startingItemsAndExtras.PlayerHasRaft))
        all.Add(TimelineItemDescription.ExtrasOrShopping("Recorder", startingItemsAndExtras.PlayerHasRecorder))
        all.Add(TimelineItemDescription.ExtrasOrShopping("AnyKey", startingItemsAndExtras.PlayerHasAnyKey))
        all.Add(TimelineItemDescription.ExtrasOrShopping("Book", startingItemsAndExtras.PlayerHasBook))
        // items checked off on the overworld map
        all.Add(TimelineItemDescription.MapCollected "Sword4")
        for i = 1 to 12 do
            all.Add(TimelineItemDescription.MapCollected(sprintf "OWHeart%d" i))
        for i = 1 to 16 do
            all.Add(TimelineItemDescription.MapCollected(sprintf "CustomIcon%d" i))
        // take any hearts
        for i = 0 to 3 do
            all.Add(TimelineItemDescription.TakeAnyHeart i)
        // triforce
        for i = 0 to 7 do
            all.Add(TimelineItemDescription.Triforce i)
        // items
        all.Add(TimelineItemDescription.ItemBox("LadderBox", ladderBox))
        all.Add(TimelineItemDescription.ItemBox("ArmosBox", armosBox))
        all.Add(TimelineItemDescription.ItemBox("MagicalSwordCaveBox", sword3Box))
        all.Add(TimelineItemDescription.ItemBox("LetterCaveBox", letterBox))
        all.Add(TimelineItemDescription.ItemBox("WhiteSwordBox", sword2Box))
        all.Add(TimelineItemDescription.ItemBox("Level9Box3", GetDungeon(8).ExtraFloorBox))   // the optional level 9 floor item check (4th Sword start option)
        if IsHiddenDungeonNumbers() then
            for i = 0 to 8 do
                for j = 0 to 2 do
                    if i<>8 || j<>2 then
                        all.Add(TimelineItemDescription.ItemBox(sprintf "Level%dBox%d" (i+1) (j+1), GetDungeon(i).Boxes.[j]))
        else
            all.Add(TimelineItemDescription.ItemBox("Level1or4Box3", DungeonTrackerInstance.TheDungeonTrackerInstance.FinalBoxOf1Or4))
            all.Add(TimelineItemDescription.ItemBox("Level2Box3", level2StairBox))   // the optional extra level 2 staircase slot
            for i = 0 to 8 do
                for j = 0 to 2 do
                    if j=0 || j=1 || i=7 then
                        all.Add(TimelineItemDescription.ItemBox(sprintf "Level%dBox%d" (i+1) (j+1), GetDungeon(i).Boxes.[j]))
        // models
        for tid in all do
            TimelineItemModel.All.Add(tid.Identifier, new TimelineItemModel(tid))
        
///////////////////////////////////////////////////////

let initializeAll(instance:OverworldData.OverworldInstance, kind) =
    if mapSquareChoiceDomain = null then
        mapSquareChoiceDomain <- ChoiceDomain("mapSquare", overworldTiles(instance.Quest.IsFirstQuestOW,false) |> Array.map (fun (_,x,_,_) -> x))
        mapSquareChoiceDomain.Changed.Add(fun _ -> mapLastChangedTime.SetNow())
        mapSquareChoiceDomain.Changed.Add(fun x -> mapSquareChangedStable.Trigger(x))
        overworldMapMarks <- Array2D.init 16 8 (fun _ _ -> new Cell(mapSquareChoiceDomain))  
    else
        failwith "cannot initialize mapSquareChoiceDomain twice"

    let dungeonInstance = new DungeonTrackerInstance(kind)

    DungeonTrackerInstance.TheDungeonTrackerInstance <- dungeonInstance
    owInstance <- instance
    for i = 0 to 15 do
        for j = 0 to 7 do
            if owInstance.AlwaysEmpty(i,j) then
                overworldMapMarks.[i,j].Set(MapSquareChoiceDomainHelper.DARK_X)   // set to 'X'
    // Extra Power Bracelet Blocks: the four any roads start out in their vanilla places (1 in H10, 2 in B14, 3 in C4, 4 in E10), same as 'Place Vanilla Any Roads' in the overworld menu
    if OverworldData.ExtraPBBlocks then
        for (state, col, row) in [ (MapSquareChoiceDomainHelper.WARP_1, 10, 7); (MapSquareChoiceDomainHelper.WARP_2, 14, 1); (MapSquareChoiceDomainHelper.WARP_3, 4, 2); (MapSquareChoiceDomainHelper.WARP_4, 10, 4) ] do
            if not (owInstance.AlwaysEmpty(col-1, row)) then overworldMapMarks.[col-1, row].Set(state)
    recomputeMapStateSummary()
    TimelineItemModel.MakeAll()

let reinitializeOverworld(newInstance: OverworldData.OverworldInstance) =
    // Save current state before replacing data structures
    let savedMarks = Array2D.init 16 8 (fun i j -> overworldMapMarks.[i,j].Current())
    let oldInstance = owInstance

    // Update owInstance
    owInstance <- newInstance

    // Replace ChoiceDomain (new quest may have different maxUses for a few tile types)
    mapSquareChoiceDomain <- ChoiceDomain("mapSquare", overworldTiles(newInstance.Quest.IsFirstQuestOW, false) |> Array.map (fun (_,x,_,_) -> x))
    mapSquareChoiceDomain.Changed.Add(fun _ -> mapLastChangedTime.SetNow())
    mapSquareChoiceDomain.Changed.Add(fun x -> mapSquareChangedStable.Trigger(x))

    // Replace Cell array (all reads of overworldMapMarks.[i,j] will now use new cells)
    overworldMapMarks <- Array2D.init 16 8 (fun _ _ -> new Cell(mapSquareChoiceDomain))

    // Restore marks, preserving as much data as possible
    for i = 0 to 15 do
        for j = 0 to 7 do
            if newInstance.AlwaysEmpty(i,j) then
                overworldMapMarks.[i,j].Set(MapSquareChoiceDomainHelper.DARK_X)  // set to DARK_X
            else
                let v = savedMarks.[i,j]
                let wasAlwaysEmpty = oldInstance.AlwaysEmpty(i,j)
                if not wasAlwaysEmpty && v >= 0 && mapSquareChoiceDomain.CanAddUse(v) then
                    // Tile was interactive in old quest with a mark — restore if compatible
                    overworldMapMarks.[i,j].Set(v)
                // else: was AlwaysEmpty (DARK_X auto-set), was empty (-1), or mark already used elsewhere → leave at -1

    recomputeMapStateSummary()
    for d in 0..8 do mapSquareChangedStable.Trigger((mapSquareChoiceDomain,d))   // refresh each dungeon's found/located display against the new domain
    overworldQuestChanged.Trigger()   // notify listeners (e.g. Reminders) to reset cached spot counts
    forceUpdate()   // refresh 'OW Spots Left' counts and per-type breakdown immediately for the new quest

