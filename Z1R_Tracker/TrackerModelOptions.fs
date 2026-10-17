module TrackerModelOptions

open System.Text.Json
open System.Text.Json.Serialization

type IntentionalApplicationShutdown(msg) =
    inherit System.Exception(msg)

type Bool(init) =
    let mutable v = init
    member this.Value with get() = v and set(x) = v <- x
module Overworld =
    let mutable DrawRoutes = Bool(true)
    let mutable RoutesCanScreenScroll = Bool(false)
    let mutable HighlightNearby = Bool(true)
    let mutable ShowMagnifier = Bool(true)
    let mutable ShopsFirst = Bool(false)
    let mutable Zones = Bool(false)
    let mutable Coords = Bool(false)
    let mutable Gettables = Bool(true)
    let mutable OpenCaves = Bool(false)
module VoiceReminders =
    let mutable DungeonFeedback = Bool(true)
    let mutable SwordHearts = Bool(true)
    let mutable CoastItem = Bool(true)
    let mutable RecorderPBSpotsAndBoomstickBook = Bool(false)  // legacy
    let mutable HaveKeyLadder = Bool(true)                     // legacy
    let mutable Blockers = Bool(true)
    let mutable DoorRepair = Bool(true)
    let mutable OverworldOverwrites = Bool(true)
    let mutable RecorderSpots = Bool(false)
    let mutable PowerBraceletSpots = Bool(false)
    let mutable BoomstickBook = Bool(false)
    let mutable HaveMagicKey = Bool(true)
    let mutable HaveLadder = Bool(true)
module VisualReminders =
    let mutable DungeonFeedback = Bool(true)
    let mutable SwordHearts = Bool(true)
    let mutable CoastItem = Bool(true)
    let mutable RecorderPBSpotsAndBoomstickBook = Bool(false)  // legacy
    let mutable HaveKeyLadder = Bool(true)                     // legacy
    let mutable Blockers = Bool(true)
    let mutable DoorRepair = Bool(true)
    let mutable OverworldOverwrites = Bool(true)
    let mutable RecorderSpots = Bool(true)
    let mutable PowerBraceletSpots = Bool(true)
    let mutable BoomstickBook = Bool(true)
    let mutable HaveMagicKey = Bool(true)
    let mutable HaveLadder = Bool(true)
module OverworldTilesToHide =
    let mutable Sword3 = Bool(false)
    let mutable Sword2 = Bool(false)
    let mutable Sword1 = Bool(false)
    let mutable LargeSecret = Bool(false)
    let mutable MediumSecret = Bool(false)
    let mutable SmallSecret = Bool(false)
    let mutable DoorRepair = Bool(false)
    let mutable MoneyMakingGame = Bool(false)
    let mutable TheLetter = Bool(false)
    let mutable Armos = Bool(false)
    let mutable HintShop = Bool(false)
    let mutable TakeAny = Bool(false)
    let mutable Shop = Bool(false)
    let mutable AlwaysHideMeatShops = Bool(false)
//RPT added this to the CoopSyncOptions module to allow for the ability to set the console ID for the local tracker and the target tracker.
module CoopSyncOptions = 
    let mutable MyConsoleId: string = "unspecified"
    let mutable TargetConsoleId : string = "unspecified"
    //let mutable EnableCoop: bool = false
    let mutable FunctionAppBase: string = "unspecified"
    let mutable baseNegotiateUrl: string = "/api/negotiate"
    let mutable baseSyncUpdateUrl: string = "/api/SyncUpdate"

    let NegotiateUrl () = FunctionAppBase + baseNegotiateUrl
    let SyncUpdateUrl () = FunctionAppBase + baseSyncUpdateUrl

    //Event to notify when EnableCoop changes
    let EnableCoopChanged = new Event<bool>()

    //private backing field
    let mutable private enableCoop = false

    let GetEnableCoop () = enableCoop
    //let SetEnableCoop value =
    //    if enableCoop <> value then
    //        enableCoop <- value
    //        EnableCoopChanged.Trigger(value)
    let SetEnableCoop(v: bool) =
        enableCoop <- v
        EnableCoopChanged.Trigger(v)

    let mutable IsBulkDoorInit = false

//RPT added this to support debugging for coop purposes
module DebugConfig =
    let mutable DebugMode: bool = false

    let Log message = 
        if DebugMode then
            printfn "%s" message

let mutable currentSyncSource : string option = None

let isCurrentlyApplyingRemoteUpdate () =
    currentSyncSource.IsSome

//RPT added this to check if the CoopSyncOptions module is enabled.
let isCoopEnabled () = CoopSyncOptions.GetEnableCoop()
let mutable UseBlurEffects = Bool(true)
let mutable AnimateTileChanges = Bool(true)
let mutable AnimateShopHighlights = Bool(true)
let mutable SaveOnCompletion = Bool(false)
let mutable SnoopSeedAndFlags = Bool(false)
let mutable DisplaySeedAndFlags = Bool(true)
let mutable ListenForSpeech = Bool(false)
let mutable RequirePTTForSpeech = Bool(false)
let mutable PlaySoundWhenUseSpeech = Bool(true)
let mutable BOARDInsteadOfLEVEL = Bool(false)
let mutable DisplayIconsInDungeonMap = Bool(false)
let mutable DungeonMapLocationHintOpacity = 0.3
let mutable ReverseScrollWheelDirection = Bool(false)
let mutable ShowBasementInfo = Bool(true)
let mutable DoDoorInference = Bool(false)
let mutable DefaultRoomPreferNonDescriptToMaybePushBlock = Bool(false)
let mutable GiveDungeonTrackerSunglasses = Bool(true)
let mutable LeftClickDragAutoInverts = Bool(false)
let mutable BookForHelpfulHints = Bool(false)
let mutable ShowMouseMagnifierWindow = Bool(false)
let mutable HideTimer = Bool(false)
let mutable ShowBroadcastWindow = Bool(false)
let mutable BroadcastWindowSize = 3
let mutable BroadcastWindowIncludesOverworldMagnifier = Bool(false)
let mutable SmallerAppWindow = Bool(false)
let mutable SmallerAppWindowScaleFactor = 2.0/3.0
let mutable ShorterAppWindow = Bool(false)
let mutable IsMuted = false
let mutable Volume = 30
//How often should reminders fire?  In milliseconds.
let mutable ReminderFrequency = 1000
// Base interval (in minutes) for periodic reminders. Actual intervals scale proportionally.
let mutable ReminderIntervalMinutes = 5
let mutable PreferredVoice = ""
let mutable MainWindowLT = ""
let mutable BroadcastWindowLT = ""
let mutable HotKeyWindowLTWH = ""
let mutable LastSeenVersionNotes = ""   // version whose About/patch notes the user has opened (version button glows until then)
let mutable OverlayLocatorWindowLTWH = ""
let mutable MouseMagnifierWindowLTWH = ""
let mutable SpotSummaryPopout_DisplayedLT = ""
let mutable InventoryAndHeartsPopout_DisplayedLT = ""
let mutable RemainingItemsPoput_DisplayedLT = ""
let mutable DungeonSummaryTabMode = 0
let mutable PortNumber = "5000"
let mutable CoopHostSession = false
let mutable RaceMode = Bool(false)
let mutable AllowItemIconOnNonShopTile = Bool(false)
let mutable PersonalPrefMarkersEnabled = Bool(false)
let mutable AlphabetizeHintZones = Bool(false)
module PerItemReminders =
    let mutable Recorder = Bool(true)
    let mutable PowerBracelet = Bool(true)
    let mutable Boomstick = Bool(true)
    let mutable LadderInDungeon = Bool(true)
    let mutable KeyInDungeon = Bool(true)


type ReadWrite() =
    member val DrawRoutes = true with get,set
    member val RoutesCanScreenScroll = false with get,set
    member val HighlightNearby = true with get,set
    member val ShowMagnifier = true with get,set
    member val MirrorOverworld = false with get,set
    member val ShopsFirst = false with get,set
    member val Zones = false with get,set
    member val Coords = false with get,set
    member val Gettables = false with get,set
    member val OpenCaves = false with get,set

    member val Voice_DungeonFeedback = true with get,set
    member val Voice_SwordHearts = true with get,set
    member val Voice_CoastItem = true with get,set
    member val Voice_RecorderPBSpotsAndBoomstickBook = true with get,set
    member val Voice_HaveKeyLadder = true with get,set
    member val Voice_Blockers = true with get,set
    member val Voice_DoorRepair = true with get,set
    member val Voice_OverworldOverwrites = true with get,set

    member val Visual_DungeonFeedback = true with get,set
    member val Visual_SwordHearts = true with get,set
    member val Visual_CoastItem = true with get,set
    member val Visual_RecorderPBSpotsAndBoomstickBook = true with get,set
    member val Visual_HaveKeyLadder = true with get,set
    member val Visual_Blockers = true with get,set
    member val Visual_DoorRepair = true with get,set
    member val Visual_OverworldOverwrites = true with get,set
    member val Voice_RecorderSpots = false with get,set
    member val Voice_PowerBraceletSpots = false with get,set
    member val Voice_BoomstickBook = false with get,set
    member val Voice_HaveMagicKey = true with get,set
    member val Voice_HaveLadder = true with get,set
    member val Visual_RecorderSpots = true with get,set
    member val Visual_PowerBraceletSpots = true with get,set
    member val Visual_BoomstickBook = true with get,set
    member val Visual_HaveMagicKey = true with get,set
    member val Visual_HaveLadder = true with get,set

    member val HideOverworldTile_Sword3 = false with get,set
    member val HideOverworldTile_Sword2 = false with get,set
    member val HideOverworldTile_Sword1 = false with get,set
    member val HideOverworldTile_LargeSecret = false with get,set
    member val HideOverworldTile_MediumSecret = false with get,set
    member val HideOverworldTile_SmallSecret = false with get,set
    member val HideOverworldTile_DoorRepair = false with get,set
    member val HideOverworldTile_MoneyMakingGame = false with get,set
    member val HideOverworldTile_TheLetter = false with get,set
    member val HideOverworldTile_Armos = false with get,set
    member val HideOverworldTile_HintShop = false with get,set
    member val HideOverworldTile_TakeAny = false with get,set
    member val HideOverworldTile_Shop = false with get,set
    member val HideOverworldTile_AlwaysHideMeatShops = false with get,set

    member val UseBlurEffects = true with get,set
    member val AnimateTileChanges = true with get,set
    member val AnimateShopHighlights = true with get,set
    member val SaveOnCompletion = false with get,set
    member val SnoopSeedAndFlags = false with get,set
    member val DisplaySeedAndFlags = true with get,set
    member val ListenForSpeech = false with get,set
    member val RequirePTTForSpeech = false with get,set
    member val PlaySoundWhenUseSpeech = true with get,set
    member val BOARDInsteadOfLEVEL = false with get,set
    member val DisplayIconsInDungeonMap = false with get,set
    member val DungeonMapLocationHintOpacity = 0.3 with get,set
    member val ReverseScrollWheelDirection = false with get,set
    member val IsSecondQuestDungeons = false with get,set
    member val ShowBasementInfo = true with get,set
    member val DoDoorInference = false with get,set
    member val DefaultRoomPreferNonDescriptToMaybePushBlock = false with get,set
    member val GiveDungeonTrackerSunglasses = true with get,set
    member val LeftClickDragAutoInverts = false with get,set
    member val BookForHelpfulHints = false with get,set
    member val ShowMouseMagnifierWindow = false with get,set
    member val HideTimer = false with get,set
    member val ShowBroadcastWindow = false with get,set
    member val BroadcastWindowSize = 3 with get,set
    member val BroadcastWindowIncludesOverworldMagnifier = false with get,set
    member val SmallerAppWindow = false with get,set
    member val SmallerAppWindowScaleFactor = 2.0/3.0 with get,set
    member val ShorterAppWindow = false with get,set

    member val IsMuted = false with get, set
    member val Volume = 30 with get, set
    member val PreferredVoice = "" with get,set
    member val ReminderIntervalMinutes = 5 with get,set
    member val MainWindowLT = "" with get,set
    member val BroadcastWindowLT = "" with get,set
    member val HotKeyWindowLTWH = "" with get, set
    member val LastSeenVersionNotes = "" with get, set
    member val OverlayLocatorWindowLTWH = "" with get, set
    member val MouseMagnifierWindowLTWH = "" with get, set
    member val SpotSummaryPopout_DisplayedLT = "" with get,set
    member val InventoryAndHeartsPopout_DisplayedLT = "" with get,set
    member val RemainingItemsPoput_DisplayedLT = "" with get,set
    member val DungeonSummaryTabMode = 0 with get,set

    member val MyConsoleId = CoopSyncOptions.MyConsoleId with get,set
    member val TargetConsoleId = CoopSyncOptions.TargetConsoleId with get,set
    member val EnableCoop = CoopSyncOptions.GetEnableCoop() with get,set
    member val FunctionAppBase = CoopSyncOptions.FunctionAppBase with get,set
    member val baseNegotiateUrl = CoopSyncOptions.baseNegotiateUrl with get,set
    member val baseSyncUpdateUrl = CoopSyncOptions.baseSyncUpdateUrl with get,set
    member val NegotiateUrl = (CoopSyncOptions.FunctionAppBase + CoopSyncOptions.baseNegotiateUrl) with get
    member val SyncUpdateUrl = (CoopSyncOptions.FunctionAppBase + CoopSyncOptions.baseSyncUpdateUrl) with get

    member val PortNumber = "5000" with get,set
    member val RaceMode = false with get,set
    member val AllowItemIconOnNonShopTile = false with get,set
    member val PersonalPrefMarkersEnabled = false with get,set
    member val AlphabetizeHintZones = false with get,set
    member val PerItemRemind_Recorder = true with get,set
    member val PerItemRemind_PowerBracelet = true with get,set
    member val PerItemRemind_Boomstick = true with get,set
    member val PerItemRemind_LadderInDungeon = true with get,set
    member val PerItemRemind_KeyInDungeon = true with get,set

let mutable private cachedSettingJson = null

//I don't want to write the settings file every time a setting changes, so I'll cache the json string and only write it when it changes.
//I believe this is how we will set some settings controls to be "live" and others to be "on demand" (like save on completion).
let private writeImpl(filename) =
    let data = ReadWrite()
    data.DrawRoutes <- Overworld.DrawRoutes.Value
    data.RoutesCanScreenScroll <- Overworld.RoutesCanScreenScroll.Value
    data.HighlightNearby <- Overworld.HighlightNearby.Value
    data.ShowMagnifier <- Overworld.ShowMagnifier.Value
    data.ShopsFirst <- Overworld.ShopsFirst.Value
    data.Zones <- Overworld.Zones.Value
    data.Coords <- Overworld.Coords.Value
    data.Gettables <- Overworld.Gettables.Value
    data.OpenCaves <- Overworld.OpenCaves.Value

    data.Voice_DungeonFeedback <- VoiceReminders.DungeonFeedback.Value
    data.Voice_SwordHearts <-     VoiceReminders.SwordHearts.Value
    data.Voice_CoastItem <-       VoiceReminders.CoastItem.Value
    data.Voice_RecorderPBSpotsAndBoomstickBook <- VoiceReminders.RecorderPBSpotsAndBoomstickBook.Value
    data.Voice_HaveKeyLadder <-   VoiceReminders.HaveKeyLadder.Value
    data.Voice_Blockers <-        VoiceReminders.Blockers.Value
    data.Voice_DoorRepair <-      VoiceReminders.DoorRepair.Value
    data.Voice_OverworldOverwrites <- VoiceReminders.OverworldOverwrites.Value

    data.Visual_DungeonFeedback <- VisualReminders.DungeonFeedback.Value
    data.Visual_SwordHearts <-     VisualReminders.SwordHearts.Value
    data.Visual_CoastItem <-       VisualReminders.CoastItem.Value
    data.Visual_RecorderPBSpotsAndBoomstickBook <- VisualReminders.RecorderPBSpotsAndBoomstickBook.Value
    data.Visual_HaveKeyLadder <-   VisualReminders.HaveKeyLadder.Value
    data.Visual_Blockers <-        VisualReminders.Blockers.Value
    data.Visual_DoorRepair <-      VisualReminders.DoorRepair.Value
    data.Visual_OverworldOverwrites <- VisualReminders.OverworldOverwrites.Value
    data.Voice_RecorderSpots <- VoiceReminders.RecorderSpots.Value
    data.Voice_PowerBraceletSpots <- VoiceReminders.PowerBraceletSpots.Value
    data.Voice_BoomstickBook <- VoiceReminders.BoomstickBook.Value
    data.Voice_HaveMagicKey <- VoiceReminders.HaveMagicKey.Value
    data.Voice_HaveLadder <- VoiceReminders.HaveLadder.Value
    data.Visual_RecorderSpots <- VisualReminders.RecorderSpots.Value
    data.Visual_PowerBraceletSpots <- VisualReminders.PowerBraceletSpots.Value
    data.Visual_BoomstickBook <- VisualReminders.BoomstickBook.Value
    data.Visual_HaveMagicKey <- VisualReminders.HaveMagicKey.Value
    data.Visual_HaveLadder <- VisualReminders.HaveLadder.Value

    data.HideOverworldTile_Sword3 <- OverworldTilesToHide.Sword3.Value
    data.HideOverworldTile_Sword2 <- OverworldTilesToHide.Sword2.Value
    data.HideOverworldTile_Sword1 <- OverworldTilesToHide.Sword1.Value
    data.HideOverworldTile_LargeSecret <- OverworldTilesToHide.LargeSecret.Value
    data.HideOverworldTile_MediumSecret <- OverworldTilesToHide.MediumSecret.Value
    data.HideOverworldTile_SmallSecret <- OverworldTilesToHide.SmallSecret.Value
    data.HideOverworldTile_DoorRepair <- OverworldTilesToHide.DoorRepair.Value
    data.HideOverworldTile_MoneyMakingGame <- OverworldTilesToHide.MoneyMakingGame.Value
    data.HideOverworldTile_TheLetter <- OverworldTilesToHide.TheLetter.Value
    data.HideOverworldTile_Armos <- OverworldTilesToHide.Armos.Value
    data.HideOverworldTile_HintShop <- OverworldTilesToHide.HintShop.Value
    data.HideOverworldTile_TakeAny <- OverworldTilesToHide.TakeAny.Value
    data.HideOverworldTile_Shop <- OverworldTilesToHide.Shop.Value
    data.HideOverworldTile_AlwaysHideMeatShops <- OverworldTilesToHide.AlwaysHideMeatShops.Value

    data.UseBlurEffects <- UseBlurEffects.Value
    data.AnimateTileChanges <- AnimateTileChanges.Value
    data.AnimateShopHighlights <- AnimateShopHighlights.Value
    data.SaveOnCompletion <- SaveOnCompletion.Value
    data.SnoopSeedAndFlags <- SnoopSeedAndFlags.Value
    data.DisplaySeedAndFlags <- DisplaySeedAndFlags.Value
    data.ListenForSpeech <- ListenForSpeech.Value
    data.RequirePTTForSpeech <- RequirePTTForSpeech.Value
    data.PlaySoundWhenUseSpeech <- PlaySoundWhenUseSpeech.Value
    data.BOARDInsteadOfLEVEL <- BOARDInsteadOfLEVEL.Value
    data.DisplayIconsInDungeonMap <- DisplayIconsInDungeonMap.Value
    data.DungeonMapLocationHintOpacity <- DungeonMapLocationHintOpacity
    data.ReverseScrollWheelDirection <- ReverseScrollWheelDirection.Value
    data.ShowBasementInfo <- ShowBasementInfo.Value
    data.DoDoorInference <- DoDoorInference.Value
    data.DefaultRoomPreferNonDescriptToMaybePushBlock <- DefaultRoomPreferNonDescriptToMaybePushBlock.Value
    data.GiveDungeonTrackerSunglasses <- GiveDungeonTrackerSunglasses.Value
    data.LeftClickDragAutoInverts <- LeftClickDragAutoInverts.Value
    data.BookForHelpfulHints <- BookForHelpfulHints.Value
    data.ShowMouseMagnifierWindow <- ShowMouseMagnifierWindow.Value
    data.HideTimer <- HideTimer.Value
    data.ShowBroadcastWindow <- ShowBroadcastWindow.Value
    data.BroadcastWindowSize <- BroadcastWindowSize
    data.BroadcastWindowIncludesOverworldMagnifier <- BroadcastWindowIncludesOverworldMagnifier.Value
    data.SmallerAppWindow <- SmallerAppWindow.Value
    data.SmallerAppWindowScaleFactor <- SmallerAppWindowScaleFactor
    data.ShorterAppWindow <- ShorterAppWindow.Value
    data.IsMuted <- IsMuted
    data.Volume <- Volume
    data.PreferredVoice <- PreferredVoice
    data.ReminderIntervalMinutes <- ReminderIntervalMinutes
    data.MainWindowLT <- MainWindowLT
    data.BroadcastWindowLT <- BroadcastWindowLT
    data.HotKeyWindowLTWH <- HotKeyWindowLTWH
    data.LastSeenVersionNotes <- LastSeenVersionNotes
    data.OverlayLocatorWindowLTWH <- OverlayLocatorWindowLTWH
    data.MouseMagnifierWindowLTWH <- MouseMagnifierWindowLTWH
    data.SpotSummaryPopout_DisplayedLT <- SpotSummaryPopout_DisplayedLT
    data.InventoryAndHeartsPopout_DisplayedLT <- InventoryAndHeartsPopout_DisplayedLT
    data.RemainingItemsPoput_DisplayedLT <- RemainingItemsPoput_DisplayedLT
    data.DungeonSummaryTabMode <- DungeonSummaryTabMode
    data.MyConsoleId <- CoopSyncOptions.MyConsoleId
    data.TargetConsoleId <- CoopSyncOptions.TargetConsoleId
    data.EnableCoop <- CoopSyncOptions.GetEnableCoop()
    data.FunctionAppBase <- CoopSyncOptions.FunctionAppBase
    data.baseNegotiateUrl <- CoopSyncOptions.baseNegotiateUrl
    data.baseSyncUpdateUrl <- CoopSyncOptions.baseSyncUpdateUrl
    data.PortNumber <- PortNumber
    data.RaceMode <- RaceMode.Value
    data.AllowItemIconOnNonShopTile <- AllowItemIconOnNonShopTile.Value
    data.PersonalPrefMarkersEnabled <- PersonalPrefMarkersEnabled.Value
    data.AlphabetizeHintZones <- AlphabetizeHintZones.Value
    data.PerItemRemind_Recorder <- PerItemReminders.Recorder.Value
    data.PerItemRemind_PowerBracelet <- PerItemReminders.PowerBracelet.Value
    data.PerItemRemind_Boomstick <- PerItemReminders.Boomstick.Value
    data.PerItemRemind_LadderInDungeon <- PerItemReminders.LadderInDungeon.Value
    data.PerItemRemind_KeyInDungeon <- PerItemReminders.KeyInDungeon.Value


    let json = JsonSerializer.Serialize<ReadWrite>(data, new JsonSerializerOptions(WriteIndented=true))
    if json <> cachedSettingJson then
        cachedSettingJson <- json
        System.IO.File.WriteAllText(filename, cachedSettingJson)

let private write(filename) =
    try
        writeImpl(filename)
    with e ->
        printfn "failed to write settings file '%s':" filename
        printfn "%s" (e.ToString())
        printfn ""

let private read(filename) =
    try
        cachedSettingJson <- System.IO.File.ReadAllText(filename)
        let data = JsonSerializer.Deserialize<ReadWrite>(cachedSettingJson, new JsonSerializerOptions(AllowTrailingCommas=true))
        Overworld.DrawRoutes.Value <- data.DrawRoutes
        Overworld.RoutesCanScreenScroll.Value <- data.RoutesCanScreenScroll
        Overworld.HighlightNearby.Value <- data.HighlightNearby
        Overworld.ShowMagnifier.Value <- data.ShowMagnifier
        Overworld.ShopsFirst.Value <- data.ShopsFirst    
        Overworld.Zones.Value <- data.Zones
        Overworld.Coords.Value <- data.Coords
        Overworld.Gettables.Value <- data.Gettables
        Overworld.OpenCaves.Value <- data.OpenCaves

        VoiceReminders.DungeonFeedback.Value <- data.Voice_DungeonFeedback
        VoiceReminders.SwordHearts.Value <-     data.Voice_SwordHearts
        VoiceReminders.CoastItem.Value <-       data.Voice_CoastItem
        VoiceReminders.RecorderPBSpotsAndBoomstickBook.Value <- data.Voice_RecorderPBSpotsAndBoomstickBook
        VoiceReminders.HaveKeyLadder.Value <-   data.Voice_HaveKeyLadder
        VoiceReminders.Blockers.Value <-        data.Voice_Blockers
        VoiceReminders.DoorRepair.Value <-      data.Voice_DoorRepair
        VoiceReminders.OverworldOverwrites.Value <- data.Voice_OverworldOverwrites

        VisualReminders.DungeonFeedback.Value <- data.Visual_DungeonFeedback
        VisualReminders.SwordHearts.Value <-     data.Visual_SwordHearts
        VisualReminders.CoastItem.Value <-       data.Visual_CoastItem
        VisualReminders.RecorderPBSpotsAndBoomstickBook.Value <- data.Visual_RecorderPBSpotsAndBoomstickBook
        VisualReminders.HaveKeyLadder.Value <-   data.Visual_HaveKeyLadder
        VisualReminders.Blockers.Value <-        data.Visual_Blockers
        VisualReminders.DoorRepair.Value <-      data.Visual_DoorRepair
        VisualReminders.OverworldOverwrites.Value <- data.Visual_OverworldOverwrites
        VoiceReminders.RecorderSpots.Value <- data.Voice_RecorderSpots
        VoiceReminders.PowerBraceletSpots.Value <- data.Voice_PowerBraceletSpots
        VoiceReminders.BoomstickBook.Value <- data.Voice_BoomstickBook
        VoiceReminders.HaveMagicKey.Value <- data.Voice_HaveMagicKey
        VoiceReminders.HaveLadder.Value <- data.Voice_HaveLadder
        VisualReminders.RecorderSpots.Value <- data.Visual_RecorderSpots
        VisualReminders.PowerBraceletSpots.Value <- data.Visual_PowerBraceletSpots
        VisualReminders.BoomstickBook.Value <- data.Visual_BoomstickBook
        VisualReminders.HaveMagicKey.Value <- data.Visual_HaveMagicKey
        VisualReminders.HaveLadder.Value <- data.Visual_HaveLadder

        OverworldTilesToHide.Sword3.Value <- data.HideOverworldTile_Sword3
        OverworldTilesToHide.Sword2.Value <- data.HideOverworldTile_Sword2
        OverworldTilesToHide.Sword1.Value <- data.HideOverworldTile_Sword1
        OverworldTilesToHide.LargeSecret.Value <- data.HideOverworldTile_LargeSecret
        OverworldTilesToHide.MediumSecret.Value <- data.HideOverworldTile_MediumSecret
        OverworldTilesToHide.SmallSecret.Value <- data.HideOverworldTile_SmallSecret
        OverworldTilesToHide.DoorRepair.Value <- data.HideOverworldTile_DoorRepair
        OverworldTilesToHide.MoneyMakingGame.Value <- data.HideOverworldTile_MoneyMakingGame
        OverworldTilesToHide.TheLetter.Value <- data.HideOverworldTile_TheLetter
        OverworldTilesToHide.Armos.Value <- data.HideOverworldTile_Armos
        OverworldTilesToHide.HintShop.Value <- data.HideOverworldTile_HintShop
        OverworldTilesToHide.TakeAny.Value <- data.HideOverworldTile_TakeAny
        OverworldTilesToHide.Shop.Value <- data.HideOverworldTile_Shop
        OverworldTilesToHide.AlwaysHideMeatShops.Value <- data.HideOverworldTile_AlwaysHideMeatShops

        UseBlurEffects.Value <- data.UseBlurEffects
        AnimateTileChanges.Value <- data.AnimateTileChanges
        AnimateShopHighlights.Value <- data.AnimateShopHighlights
        SaveOnCompletion.Value <- data.SaveOnCompletion
        SnoopSeedAndFlags.Value <- data.SnoopSeedAndFlags
        DisplaySeedAndFlags.Value <- data.DisplaySeedAndFlags
        ListenForSpeech.Value <- data.ListenForSpeech
        RequirePTTForSpeech.Value <- data.RequirePTTForSpeech
        PlaySoundWhenUseSpeech.Value <- data.PlaySoundWhenUseSpeech
        BOARDInsteadOfLEVEL.Value <- false   // option removed from the UI; always LEVEL
        DisplayIconsInDungeonMap.Value <- data.DisplayIconsInDungeonMap
        DungeonMapLocationHintOpacity <- max 0.05 (min 1.0 data.DungeonMapLocationHintOpacity)
        ReverseScrollWheelDirection.Value <- data.ReverseScrollWheelDirection
        ShowBasementInfo.Value <- data.ShowBasementInfo
        DoDoorInference.Value <- data.DoDoorInference
        DefaultRoomPreferNonDescriptToMaybePushBlock.Value <- data.DefaultRoomPreferNonDescriptToMaybePushBlock
        GiveDungeonTrackerSunglasses.Value <- data.GiveDungeonTrackerSunglasses
        LeftClickDragAutoInverts.Value <- data.LeftClickDragAutoInverts
        BookForHelpfulHints.Value <- data.BookForHelpfulHints
        ShowMouseMagnifierWindow.Value <- data.ShowMouseMagnifierWindow
        HideTimer.Value <- data.HideTimer
        ShowBroadcastWindow.Value <- data.ShowBroadcastWindow
        BroadcastWindowSize <- max 1 (min 3 data.BroadcastWindowSize)
        BroadcastWindowIncludesOverworldMagnifier.Value <- data.BroadcastWindowIncludesOverworldMagnifier
        SmallerAppWindow.Value <- data.SmallerAppWindow
        SmallerAppWindowScaleFactor <- data.SmallerAppWindowScaleFactor
        ShorterAppWindow.Value <- data.ShorterAppWindow
        IsMuted <- data.IsMuted
        Volume <- max 0 (min 100 data.Volume)
        PreferredVoice <- data.PreferredVoice
        ReminderIntervalMinutes <- max 1 (min 60 data.ReminderIntervalMinutes)
        MainWindowLT <- data.MainWindowLT
        BroadcastWindowLT <- data.BroadcastWindowLT
        HotKeyWindowLTWH <- data.HotKeyWindowLTWH
        LastSeenVersionNotes <- (if data.LastSeenVersionNotes = null then "" else data.LastSeenVersionNotes)
        OverlayLocatorWindowLTWH <- data.OverlayLocatorWindowLTWH
        MouseMagnifierWindowLTWH <- data.MouseMagnifierWindowLTWH
        SpotSummaryPopout_DisplayedLT <- data.SpotSummaryPopout_DisplayedLT
        InventoryAndHeartsPopout_DisplayedLT <- data.InventoryAndHeartsPopout_DisplayedLT
        RemainingItemsPoput_DisplayedLT <- data.RemainingItemsPoput_DisplayedLT
        DungeonSummaryTabMode <- data.DungeonSummaryTabMode

        AllowItemIconOnNonShopTile.Value <- data.AllowItemIconOnNonShopTile
        PersonalPrefMarkersEnabled.Value <- data.PersonalPrefMarkersEnabled
        AlphabetizeHintZones.Value <- data.AlphabetizeHintZones
        PerItemReminders.Recorder.Value <- data.PerItemRemind_Recorder
        PerItemReminders.PowerBracelet.Value <- data.PerItemRemind_PowerBracelet
        PerItemReminders.Boomstick.Value <- data.PerItemRemind_Boomstick
        PerItemReminders.LadderInDungeon.Value <- data.PerItemRemind_LadderInDungeon
        PerItemReminders.KeyInDungeon.Value <- data.PerItemRemind_KeyInDungeon

        CoopSyncOptions.MyConsoleId <- data.MyConsoleId
        CoopSyncOptions.TargetConsoleId <- data.TargetConsoleId
        //CoopSyncOptions.SetEnableCoop(data.EnableCoop)
        CoopSyncOptions.FunctionAppBase <- data.FunctionAppBase
        CoopSyncOptions.baseNegotiateUrl <- data.baseNegotiateUrl
        CoopSyncOptions.baseSyncUpdateUrl <- data.baseSyncUpdateUrl

    with e ->
        cachedSettingJson <- null
        printfn "Unable to read settings file '%s':" filename 
        if System.IO.File.Exists(filename) then
            printfn "That file does exist on disk, but could not be read.  Here is the error detail:"
            printfn "%s" (e.ToString())
            printfn ""
            printfn "If you were intentionally hand-editing the .json settings file, you may have made a mistake that must be corrected."
            printfn ""
            printfn "The application will shut down now. If you want to discard the broken settings file and start %s fresh with some default settings, then simply delete the file:" OverworldData.ProgramNameString
            printfn ""
            printfn "%s" filename
            printfn ""
            printfn "from disk before starting the application again."
            printfn ""
            raise <| IntentionalApplicationShutdown "Intentional application shutdown: failed to read settings file data."
        else
            printfn "Perhaps this is your first time using '%s' on this machine?" OverworldData.ProgramNameString
            printfn "A default settings file will be created for you..."
            try
                writeImpl(filename)
                printfn "... The settings file has been successfully created."
            with e ->
                printfn "... Failed to write settings file.  Perhaps there is an issue with the file location?"
                printfn "'Filename'='%s'" filename
                printfn "Here is more information about the problem:"
                printfn "%s" (e.ToString())
                printfn ""

let mutable private settingsFile = null
    
let readSettings() =
    if settingsFile = null then
        settingsFile <- System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Z1R_Tracker_settings.json")
    read(settingsFile)
let writeSettings() =
    if settingsFile = null then
        settingsFile <- System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Z1R_Tracker_settings.json")
    write(settingsFile)


