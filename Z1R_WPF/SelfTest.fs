module SelfTest

// Background self-test: `Z1R_WPF.exe --selftest --quest=N` (N = 0 1st, 1 2nd, 2 Mixed 1Q, 3 Mixed 2Q).
// It runs on the tracker's own UI thread: it clicks controls by calling their click code and raises mouse events on whatever is under a grid of
// points, so it never touches the real mouse or keyboard. Run it from a COPY of the release folder (it changes settings), see selftest.ps1.
// Writes selftest_report.txt next to the exe, then exits. A crash shows up as a new Z1R_Tracker_crash_log.txt entry.

open System
open System.IO
open System.Reflection
open System.Windows
open System.Windows.Controls
open System.Windows.Controls.Primitives
open System.Windows.Input
open System.Windows.Media
open Z1R_Sync
open Z1R_Tracker.Models.Z1R_TrackerInterop

let private args = Environment.GetCommandLineArgs()
let isSelfTest = args |> Array.contains "--selftest"
let private questArg =
    args |> Array.tryPick (fun a -> if a.StartsWith("--quest=") then Some(int (a.Substring(8))) else None) |> Option.defaultValue 2
let private reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest_report.txt")
let private failures = ref 0
let private popupsSeen = ref 0   // popups the test opened and closed (a test that never opens one is not testing much)
let private clicks = ref 0
let private hitsByType = System.Collections.Generic.Dictionary<string,int>()
let private popupsByType = System.Collections.Generic.Dictionary<string,int>()
let private log(s:string) = try File.AppendAllText(reportPath, s + "\n") with _ -> ()
let private fail(s:string) =
    failures.Value <- failures.Value + 1
    log("FAIL: " + s)

let rec private descendants (d:DependencyObject) : seq<DependencyObject> =
    seq { for i in 0 .. VisualTreeHelper.GetChildrenCount(d) - 1 do
            let c = VisualTreeHelper.GetChild(d, i)
            yield c
            yield! descendants c }

let private label (e:DependencyObject) =
    match e with
    | :? ContentControl as cc ->
        (match cc.Content with
         | :? string as s -> s.Trim()
         | :? TextBox as t -> t.Text.Trim()
         | :? TextBlock as t -> t.Text.Trim()
         | _ -> "")
    | _ -> ""

let private onClickMethod = typeof<ButtonBase>.GetMethod("OnClick", BindingFlags.NonPublic ||| BindingFlags.Instance)
let private click(b:ButtonBase) =
    clicks.Value <- clicks.Value + 1
    onClickMethod.Invoke(b, [||]) |> ignore

// runs an action; an exception it throws is a finding, not the end of the test
let private guard (what:string) (f:unit -> unit) =
    try f()
    with e ->
        let e = (match e with :? TargetInvocationException as t when not (isNull t.InnerException) -> t.InnerException | _ -> e)
        let lines = e.ToString().Split('\n') |> Array.truncate 4 |> String.concat " | "
        fail(sprintf "%s: %s" what lines)

let private raiseMouse (target:UIElement) (ev:RoutedEvent) (button:MouseButton) =
    let a = MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button)
    a.RoutedEvent <- ev
    a.Source <- target
    target.RaiseEvent(a)
let private raiseWheel (target:UIElement) (delta:int) =
    let a = MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta)
    a.RoutedEvent <- UIElement.MouseWheelEvent
    a.Source <- target
    target.RaiseEvent(a)

let private popupCount() = try CustomComboBoxes.CanvasManager.TheOnlyCanvasManager.PopupCanvasStack.Count with _ -> 0
let private popupRoot() : DependencyObject option =
    try Some(CustomComboBoxes.CanvasManager.TheOnlyCanvasManager.PopupCanvasStack.Peek() :> DependencyObject) with _ -> None

// buttons the test must not press (they open file dialogs, other programs, web pages, or windows that need a person)
let private avoid = [ "save"; "load"; "hotkey"; "popout"; "site"; "discord"; "exit"; "restart"; "update"; "quit"; "download"; "import"; "export"; "guided tour"; "v2."; "-b1"; "log" ]
let private isAvoided (text:string) = let t = text.ToLowerInvariant() in avoid |> List.exists (fun a -> t.Contains(a))
let private questButtons = [ "1st Quest"; "2nd Quest"; "Mixed - 1Q"; "Mixed - 2Q"; "Randomized Quest"; "Other..." ]

let private run (win:Window) = async {
    let ctxt = System.Threading.SynchronizationContext.Current
    let pause (ms:int) = async {
        do! Async.Sleep ms
        do! Async.SwitchToContext ctxt }
    let stuck = ref false
    // buttons like the popout ones open separate windows: close every window except the main one (and keep it from sitting on screen meanwhile)
    let initialWindows = Application.Current.Windows |> Seq.cast<Window> |> Seq.toList   // (the ones that exist before the test starts are left alone)
    let closeExtraWindows () =
        for w in Application.Current.Windows |> Seq.cast<Window> |> Seq.toList do
            if w.IsVisible && not (initialWindows |> List.exists (fun i -> obj.ReferenceEquals(i, w))) then
                (try w.Close() with _ -> ())
    // closes every popup that is open; a popup that will not close is a failure (the gray-screen kind), and ends the test
    let settle (what:string) = async {
        if popupCount() > 0 then popupsSeen.Value <- popupsSeen.Value + 1
        let tries = ref 0
        while popupCount() > 0 && tries.Value < 10 do
            if not (CustomComboBoxes.DismissTopModal()) then tries.Value <- 10 else tries.Value <- tries.Value + 1
            do! pause 120
        closeExtraWindows ()
        if popupCount() > 0 || CustomComboBoxes.GlobalFlag.popupIsActive then
            fail(sprintf "a popup was left open after: %s" what)
            stuck.Value <- true }
    let find (text:string) = descendants win |> Seq.tryPick (fun d -> match d with :? ButtonBase as b when label d = text -> Some b | _ -> None)
    let checkboxes (root:DependencyObject) = descendants root |> Seq.choose (fun d -> match d with :? CheckBox as c -> Some c | _ -> None) |> Seq.toList
    let buttons (root:DependencyObject) = descendants root |> Seq.choose (fun d -> match d with :? ButtonBase as b when not (b :? ToggleButton) -> Some b | _ -> None) |> Seq.toList
    let cycle (what:string) (cb:CheckBox) = async {
        for i in 1 .. (if cb.IsThreeState then 4 else 3) do
            if not stuck.Value then
                guard (sprintf "%s checkbox '%s' click %d" what (label cb) i) (fun () -> click cb)
                do! settle (sprintf "clicking checkbox '%s'" (label cb)) }

    log(sprintf "ZORATracker selftest, quest %d, %s" questArg (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")))
    TrackerModelOptions.SuppressAudioForSelfTest <- true   // hundreds of clicks fire real reminders: keep the run quiet

    // 0. flag decoding (no UI)
    guard "flag decode" (fun () ->
        (match FlagPaste.decodeZ1R "GmCbTYod8E8ige6igkDR1ZyxsOpshPXUUTcU5" with
         | Ok p -> if p.Quest <> Some 2 then fail "Z1R flags did not decode to Mixed 1Q"
         | Error m -> fail ("Z1R flags: " + m))
        (match FlagPaste.decodeZora "10.Km67jtU" with
         | Ok z -> if not (z.Progressive && z.FourthSword = FlagPaste.On && z.FourthInLevel2 && z.BootsReplaceBoomerangs = FlagPaste.Random) then fail "ZORA flags decoded wrong"
         | Error m -> fail ("ZORA flags: " + m))
        // a version 18 string (the newest fields sit above the ones the tracker reads, so it must still decode and leave them unread)
        (match FlagPaste.decodeZora "18.1!5I5HHHX0dg" with
         | Ok z -> if not (z.Progressive && z.FourthSword = FlagPaste.On && z.BootsReplaceBoomerangs = FlagPaste.On) then fail "ZORA flags (version 18) decoded wrong"
         | Error m -> fail ("ZORA flags (version 18): " + m)))
    log("flag decode done")

    // 1. start screen: every checkbox through all its states
    for cb in checkboxes win do
        if not stuck.Value && not (isAvoided (label cb)) then do! cycle "start screen" cb
    log(sprintf "start screen checkboxes done (%d failures so far)" failures.Value)
    // start screen buttons that open things (not the ones that start the tracker)
    for b in buttons win do
        let text = label b
        if not stuck.Value && text <> "" && not (isAvoided text) && not (questButtons |> List.contains text) && text <> "Paste Flags" then
            guard (sprintf "start screen button '%s'" text) (fun () -> click b)
            do! pause 200
            do! settle (sprintf "start screen button '%s'" text)
    // the Guided Tour: every start screen stop (Paste Flags included) must find its anchors, or the tour silently leaves it out
    // (the settings panel's stops are only on screen while it is raised, so those are checked after raising it)
    let tourPanelOnly = [ "Overworld settings"; "Dungeon settings"; "Reminders"; "Auto-save"; "Other options"; "Co-Op and crash log" ]
    for title in OptionsMenu.startScreenTourMissingStops(win) do
        if not (List.contains title tourPanelOnly) then fail (sprintf "Guided Tour: the start screen stop '%s' has no anchor on screen" title)
    if not (OptionsMenu.startScreenTourSteps |> List.exists (fun (_, t, _) -> t = "Paste Flags")) then fail "Guided Tour: no Paste Flags stop"
    // Paste Flags: type both strings, Apply, expect it to close and the options to be set
    let pasteButton = find "Paste Flags"
    if pasteButton.IsSome && not stuck.Value then
        guard "Paste Flags click" (fun () -> click pasteButton.Value)
        do! pause 400
        let root = popupRoot()
        if root.IsNone then fail "Paste Flags: the box did not open"
        else
            let boxes = descendants root.Value |> Seq.choose (fun d -> match d with :? TextBox as t when not t.IsReadOnly -> Some t | _ -> None) |> Seq.toList
            let apply = descendants root.Value |> Seq.tryPick (fun d -> match d with :? ButtonBase as b when label d = "Apply" -> Some b | _ -> None)
            if boxes.Length < 2 then fail "Paste Flags: the two text boxes were not found"
            elif apply.IsNone then fail "Paste Flags: no Apply button"
            else
                boxes.[0].Text <- "GmCbTYod8E8ige6igkDR1ZyxsOpshPXUUTcU5"
                boxes.[1].Text <- "10.Km67jtU"
                guard "Paste Flags Apply" (fun () -> click apply.Value)
                do! pause 400
                if popupCount() > 0 then fail "Paste Flags: Apply did not close the box"
                let progressive = checkboxes win |> List.tryFind (fun c -> (label c).StartsWith("Progressive"))
                if progressive.IsNone then fail "Paste Flags: no Progressive Items checkbox"
                elif progressive.Value.IsChecked <> Nullable true then fail "Paste Flags: Progressive Items was not checked"
        do! settle "Paste Flags"
    log(sprintf "start screen done (%d failures so far)" failures.Value)

    // 2. start the tracker
    let questLabel = [| "1st Quest"; "2nd Quest"; "Mixed - 1Q"; "Mixed - 2Q" |].[questArg]
    let questButton = find questLabel
    if questButton.IsNone || stuck.Value then fail (sprintf "no '%s' start button" questLabel)
    else guard (sprintf "start '%s'" questLabel) (fun () -> click questButton.Value)
    let waited = ref 0
    while (find "Overworld + Extras").IsNone && waited.Value < 60 do
        do! pause 500
        waited.Value <- waited.Value + 1
    if (find "Overworld + Extras").IsNone then
        fail "the main screen never appeared"
        stuck.Value <- true
    else
        do! pause 1500
        do! settle "tracker start"
        log(sprintf "tracker started (%d failures so far)" failures.Value)
        // a save remembers the Heart Shuffle mode (Off / Intra / Full), so a reload does not follow the start screen
        guard "heart shuffle save round trip" (fun () ->
            let saved = TrackerModel.HeartShuffleMode
            try
                for m in [0; 1; 2] do
                    TrackerModel.HeartShuffleMode <- m
                    let json = "{ " + String.concat "\n" (SaveAndLoad.SaveOverworld("")) + " }"
                    let d = DungeonSaveAndLoad.LoadAll(json)
                    if d.Overworld.HeartShuffleMode <> m then fail (sprintf "Heart Shuffle mode %d saved and loaded back as %d" m d.Overworld.HeartShuffleMode)
                let old = DungeonSaveAndLoad.LoadAll("{ \"Overworld\": { \"Quest\": 2 } }")
                if old.Overworld.HeartShuffleMode <> -1 then fail "a save without a Heart Shuffle mode should load as -1"
            finally
                TrackerModel.HeartShuffleMode <- saved)
        // a save keeps every ZORA option, not just On / Off: Boots and the four Extra / Randomize options each come back Off / On / ? (Random), as do Progressive Items,
        // the 4th Sword, Mirrored Overworld, the custom LEVEL name, the Dead Woods / Lost Hills hint letters, dungeon 9's items and the notes. Nothing is clicked or written to the Saves folder.
        guard "ZORA options save round trip" (fun () ->
            let (sBoots, sBootsR, sProg, sPB, sRaft, sDW, sLH, sPBR, sRaftR, sDWR, sLHR, sDWH, sLHH, sDWK, sLHK) =
                (TrackerModel.BootsReplaceBoomerangs, TrackerModel.BootsRandom, TrackerModel.ProgressiveItems,
                 OverworldData.ExtraPBBlocks, OverworldData.ExtraRaftBlocks, OverworldData.DeadWoodsEnabled, OverworldData.LostHillsEnabled,
                 OverworldData.ExtraPBRandom, OverworldData.ExtraRaftRandom, OverworldData.DeadWoodsRandom, OverworldData.LostHillsRandom,
                 OverworldData.DeadWoodsHint, OverworldData.LostHillsHint, OverworldData.DeadWoodsHintKnown, OverworldData.LostHillsHintKnown)
            let sName, sMirror, sL2, sL9 = TrackerModel.CustomLevelName, TrackerModel.MirrorOverworld, TrackerModel.level2StairBoxEnabled, TrackerModel.level9FloorBoxEnabled
            let box9 = (TrackerModel.GetDungeon(8)).Boxes.[0]
            let sBox9Cell, sBox9Has = box9.CellCurrent(), box9.PlayerHas()
            let build (notes:string) = String.concat "\n" (SaveAndLoad.BuildSaveLines(notes, 0, [| "}" |], [||], "", false, 0, false, false))
            try
                // states: 0 = Off, 1 = On, 2 = ? (Random, counts as On for the extra blocks and as Off for Boots)
                let setModel (pb, raft, dw, lh, boots, prog) =
                    OverworldData.ExtraPBBlocks <- (pb <> 0);  OverworldData.ExtraPBRandom <- (pb = 2)
                    OverworldData.ExtraRaftBlocks <- (raft <> 0);  OverworldData.ExtraRaftRandom <- (raft = 2)
                    OverworldData.DeadWoodsEnabled <- (dw <> 0);  OverworldData.DeadWoodsRandom <- (dw = 2)
                    OverworldData.LostHillsEnabled <- (lh <> 0);  OverworldData.LostHillsRandom <- (lh = 2)
                    TrackerModel.BootsReplaceBoomerangs <- (boots = 1);  TrackerModel.BootsRandom <- (boots = 2)
                    TrackerModel.ProgressiveItems <- (prog = 1)
                let st e r = if r then 2 elif e then 1 else 0
                let readModel() =
                    (st OverworldData.ExtraPBBlocks OverworldData.ExtraPBRandom, st OverworldData.ExtraRaftBlocks OverworldData.ExtraRaftRandom,
                     st OverworldData.DeadWoodsEnabled OverworldData.DeadWoodsRandom, st OverworldData.LostHillsEnabled OverworldData.LostHillsRandom,
                     (if TrackerModel.BootsRandom then 2 elif TrackerModel.BootsReplaceBoomerangs then 1 else 0), (if TrackerModel.ProgressiveItems then 1 else 0))
                let mutable bad = 0
                for i in 0 .. 80 do
                    let pb, raft, dw, lh, boots, prog = i % 3, (i / 3) % 3, (i / 9) % 3, (i / 27) % 3, (i / 2) % 3, i % 2
                    let dwHint, lhHint = (if i % 2 = 0 then "UDLR" else ""), (if i % 2 = 0 then "" else "LLRR")
                    setModel (pb, raft, dw, lh, boots, prog)
                    OverworldData.DeadWoodsHint <- dwHint
                    OverworldData.LostHillsHint <- lhHint
                    let d = DungeonSaveAndLoad.LoadAll(build "notes")
                    // forget everything (the opposite of what was saved), then load it back the way a real load does
                    setModel ((pb + 1) % 3, (raft + 1) % 3, (dw + 1) % 3, (lh + 1) % 3, (boots + 1) % 3, 1 - prog)
                    OverworldData.DeadWoodsHint <- "ZZ"
                    OverworldData.LostHillsHint <- "ZZ"
                    SaveAndLoad.ApplyLoadedOptions(d.Overworld)
                    let want = (pb, raft, dw, lh, boots, prog)
                    if readModel() <> want && bad < 5 then
                        bad <- bad + 1
                        fail (sprintf "save round trip: (Extra PB, Raft, Dead Woods, Lost Hills, Boots, Progressive) = %A (0 = Off, 1 = On, 2 = ?) came back as %A" want (readModel()))
                    elif OverworldData.DeadWoodsHint <> dwHint || OverworldData.LostHillsHint <> lhHint || OverworldData.DeadWoodsHintKnown <> (dwHint <> "") || OverworldData.LostHillsHintKnown <> (lhHint <> "") then
                        if bad < 5 then
                            bad <- bad + 1
                            fail "save round trip: the Dead Woods / Lost Hills hint letters did not come back"
                // an older save (no Random bits, no boots, no progressive items, no hint letters): those keep whatever is set, and the old On / Off bits still load
                let old = DungeonSaveAndLoad.LoadAll("{ \"Overworld\": { \"Quest\": 2, \"ExtraBlocks\": 5 } }")
                if old.Overworld.ExtraBlocksRandom <> -1 || old.Overworld.BootsReplaceBoomerangs <> -1 || old.Overworld.ProgressiveItems <> -1 || not (isNull old.Overworld.DeadWoodsHint) then
                    fail "an older save should load the new fields as -1 / null"
                setModel (0, 2, 0, 2, 1, 1)
                OverworldData.DeadWoodsHint <- "UD"
                SaveAndLoad.ApplyLoadedOptions(old.Overworld)
                if readModel() <> (1, 0, 1, 0, 1, 1) then fail (sprintf "an older save (ExtraBlocks 5) loaded wrong: %A" (readModel()))
                if OverworldData.DeadWoodsHint <> "UD" then fail "an older save should leave the hint letters alone"
                // Mirrored Overworld, the custom LEVEL name (an empty name must come back empty, not as null), the notes
                TrackerModel.MirrorOverworld <- true
                TrackerModel.CustomLevelName <- "ZORA"
                let notes = "line one\n\"quoted\" and a \\ backslash\ttab\nlast line"
                let d1 = DungeonSaveAndLoad.LoadAll(build notes)
                if not d1.Overworld.MirrorOverworld then fail "save round trip: Mirrored Overworld (on) came back off"
                if d1.Items.CustomLevelName <> "ZORA" then fail (sprintf "save round trip: the custom LEVEL name came back as '%s'" d1.Items.CustomLevelName)
                if d1.Notes <> notes then fail "save round trip: the notes came back different"
                TrackerModel.MirrorOverworld <- false
                TrackerModel.CustomLevelName <- ""
                let d2 = DungeonSaveAndLoad.LoadAll(build "")
                if d2.Overworld.MirrorOverworld then fail "save round trip: Mirrored Overworld (off) came back on"
                if isNull d2.Items.CustomLevelName || d2.Items.CustomLevelName <> "" then fail "save round trip: an empty custom LEVEL name should come back empty, not missing"
                // 4th Sword: its two slots (level 2 staircase / level 9 floor check) say whether it was on, and where
                for (l2, l9) in [ (false, false); (true, false); (false, true) ] do
                    TrackerModel.level2StairBoxEnabled <- l2
                    TrackerModel.level9FloorBoxEnabled <- l9
                    let d3 = DungeonSaveAndLoad.LoadAll(build "")
                    if d3.Items.Level2StairBoxEnabled <> l2 || d3.Items.Level9FloorBoxEnabled <> l9 || SaveAndLoad.FourthSwordWasOn(d3.Items) <> (l2 || l9) then
                        fail (sprintf "save round trip: 4th Sword slots (level 2 %b, level 9 %b) came back wrong" l2 l9)
                // dungeon 9's items: an item and its got-it state are written, and put back by the load
                let wantCell = (if sBox9Cell = TrackerModel.ITEMS.BOW then TrackerModel.ITEMS.RAFT else TrackerModel.ITEMS.BOW)
                if not (box9.AttemptToSet(wantCell, TrackerModel.PlayerHas.YES)) then fail "save round trip: could not put a test item in a dungeon 9 box"
                else
                    let d4 = DungeonSaveAndLoad.LoadAll(build "")
                    if d4.Items.Dungeons.[8].Boxes.[0].CellCurrent <> box9.CellCurrent() || d4.Items.Dungeons.[8].Boxes.[0].PlayerHas <> box9.PlayerHas().AsInt() then
                        fail "save round trip: a dungeon 9 item box was not written to the save"
                    box9.AttemptToSet(TrackerModel.ITEMS.LADDER, TrackerModel.PlayerHas.NO) |> ignore
                    if not (d4.Items.Dungeons.[8].Boxes.[0].TryApply(box9)) || box9.CellCurrent() <> wantCell || box9.PlayerHas() <> TrackerModel.PlayerHas.YES then
                        fail "save round trip: a dungeon 9 item box did not come back"
            finally
                OverworldData.ExtraPBBlocks <- sPB;  OverworldData.ExtraRaftBlocks <- sRaft;  OverworldData.DeadWoodsEnabled <- sDW;  OverworldData.LostHillsEnabled <- sLH
                OverworldData.ExtraPBRandom <- sPBR;  OverworldData.ExtraRaftRandom <- sRaftR;  OverworldData.DeadWoodsRandom <- sDWR;  OverworldData.LostHillsRandom <- sLHR
                OverworldData.DeadWoodsHint <- sDWH;  OverworldData.LostHillsHint <- sLHH;  OverworldData.DeadWoodsHintKnown <- sDWK;  OverworldData.LostHillsHintKnown <- sLHK
                TrackerModel.BootsReplaceBoomerangs <- sBoots;  TrackerModel.BootsRandom <- sBootsR;  TrackerModel.ProgressiveItems <- sProg
                TrackerModel.CustomLevelName <- sName;  TrackerModel.MirrorOverworld <- sMirror
                TrackerModel.level2StairBoxEnabled <- sL2;  TrackerModel.level9FloorBoxEnabled <- sL9
                box9.AttemptToSet(sBox9Cell, sBox9Has) |> ignore)
        log(sprintf "ZORA options save round trip done (%d failures so far)" failures.Value)

    if not stuck.Value then
        // 3a. every ordinary button on the main screen
        for b in buttons win do
            let text = label b
            if not stuck.Value && text <> "" && not (isAvoided text) then
                guard (sprintf "button '%s'" text) (fun () -> click b)
                do! pause 200
                // inside whatever opened: every checkbox through its states, then close it
                let root = popupRoot()
                if popupCount() > 0 && root.IsSome then
                    for cb in checkboxes root.Value do
                        if not stuck.Value && not (isAvoided (label cb)) then do! cycle (sprintf "'%s' popup" text) cb
                do! settle (sprintf "button '%s'" text)
        log(sprintf "main screen buttons done (%d failures so far)" failures.Value)

        // 3b. a click, right click and wheel on whatever is under each point of a grid over the window
        let content = win.Content :?> Visual
        let w, h = int win.ActualWidth, int win.ActualHeight
        let step = 20
        let n = ref 0
        log(sprintf "grid starts: popupIsActive=%b popups=%d window %dx%d" CustomComboBoxes.GlobalFlag.popupIsActive (popupCount()) w h)
        for y in step / 2 .. step .. h - 1 do
            for x in step / 2 .. step .. w - 1 do
                if not stuck.Value then
                    let hit = (try VisualTreeHelper.HitTest(content, Point(float x, float y)) with _ -> null)
                    if not (isNull hit) && not (isNull hit.VisualHit) then
                        let target = (let rec up (d:DependencyObject) = (match d with :? UIElement as u -> u | null -> null | _ -> up (VisualTreeHelper.GetParent d)) in up hit.VisualHit)
                        if not (isNull target) then
                            n.Value <- n.Value + 1
                            let where = sprintf "(%d,%d) %s" x y (target.GetType().Name)
                            let tname = target.GetType().Name
                            hitsByType.[tname] <- (match hitsByType.TryGetValue tname with | true, c -> c + 1 | _ -> 1)
                            let afterwards (what:string) = async {
                                if popupCount() > 0 then
                                    popupsByType.[tname] <- (match popupsByType.TryGetValue tname with | true, c -> c + 1 | _ -> 1)
                                    do! pause 80
                                    do! settle what }
                            // (three left clicks: an unmarked overworld tile first marks X, then DontCare, and only then opens its popup)
                            for k in 1 .. 3 do
                                if not stuck.Value then
                                    guard (sprintf "left click %d %s" k where) (fun () -> raiseMouse target UIElement.MouseDownEvent MouseButton.Left; raiseMouse target UIElement.MouseUpEvent MouseButton.Left)
                                    do! afterwards (sprintf "left click %d %s" k where)
                            guard ("right click " + where) (fun () -> raiseMouse target UIElement.MouseDownEvent MouseButton.Right; raiseMouse target UIElement.MouseUpEvent MouseButton.Right)
                            do! afterwards ("right click " + where)
                            guard ("wheel " + where) (fun () -> raiseWheel target 120; raiseWheel target -120)
                            do! afterwards ("wheel " + where)
            do! pause 1   // (let the window breathe between rows)
        // 3c. the overworld tile canvases themselves (48x33, one per tile): left click each 4 times, right click once
        let tiles = descendants content |> Seq.choose (fun d -> match d with :? Canvas as c when c.Width = 48. && c.Height = 33. && c.IsHitTestVisible -> Some c | _ -> None) |> Seq.toList
        log(sprintf "overworld tile canvases found: %d (popupIsActive=%b)" tiles.Length CustomComboBoxes.GlobalFlag.popupIsActive)
        let tilePopups = ref 0
        for t in tiles do
            if not stuck.Value then
                for k in 1 .. 4 do
                    if not stuck.Value then
                        guard "tile left click" (fun () -> raiseMouse t UIElement.MouseDownEvent MouseButton.Left; raiseMouse t UIElement.MouseUpEvent MouseButton.Left)
                        do! pause 5
                        if popupCount() > 0 then
                            tilePopups.Value <- tilePopups.Value + 1
                            do! pause 80
                            do! settle "tile left click"
                guard "tile right click" (fun () -> raiseMouse t UIElement.MouseDownEvent MouseButton.Right; raiseMouse t UIElement.MouseUpEvent MouseButton.Right)
                do! pause 5
                if popupCount() > 0 then
                    tilePopups.Value <- tilePopups.Value + 1
                    do! pause 80
                    do! settle "tile right click"
        log(sprintf "tile clicks done: %d popups opened" tilePopups.Value)
        log(sprintf "grid done: %d points" n.Value)
        log("grid targets (type: points, popups opened): " + (hitsByType |> Seq.sortByDescending (fun kv -> kv.Value) |> Seq.truncate 12 |> Seq.map (fun kv -> sprintf "%s: %d, %d" kv.Key kv.Value (match popupsByType.TryGetValue kv.Key with | true, c -> c | _ -> 0)) |> String.concat " | "))

        // 3d. Overworld + Extras: what the checkboxes actually do (not just that they can be clicked)
        let anyRoads = [ (TrackerModel.MapSquareChoiceDomainHelper.WARP_1, 10, 7); (TrackerModel.MapSquareChoiceDomainHelper.WARP_2, 14, 1)
                         (TrackerModel.MapSquareChoiceDomainHelper.WARP_3, 4, 2);  (TrackerModel.MapSquareChoiceDomainHelper.WARP_4, 10, 4) ]
        let owExtras = if stuck.Value then None else find "Overworld + Extras"
        if owExtras.IsSome then
            guard "Overworld + Extras open" (fun () -> click owExtras.Value)
            do! pause 400
            match popupRoot() with
            | None -> fail "Overworld + Extras: the window did not open"
            | Some root ->
                let cbNamed (text:string) = checkboxes root |> List.tryFind (fun c -> (label c).StartsWith(text))
                let clickTo (cb:CheckBox) (target:Nullable<bool>) = async {
                    let tries = ref 0
                    while cb.IsChecked <> target && tries.Value < 4 do
                        guard "Overworld + Extras checkbox" (fun () -> click cb)
                        tries.Value <- tries.Value + 1
                        do! pause 50 }
                // mirror: the checkbox flips the map and the model, and back
                let mirrorCb = cbNamed "Mirrored Overworld"
                if mirrorCb.IsNone then fail "Overworld + Extras: no Mirrored Overworld checkbox"
                else
                    let cb = mirrorCb.Value
                    let before = TrackerModel.MirrorOverworld
                    guard "mirror click" (fun () -> click cb)
                    do! pause 300
                    if TrackerModel.MirrorOverworld = before then fail "Mirrored Overworld: clicking the box did not flip the map"
                    guard "mirror click back" (fun () -> click cb)
                    do! pause 300
                    if TrackerModel.MirrorOverworld <> before then fail "Mirrored Overworld: clicking again did not flip it back"
                // Extra PB blocks: Off removes the vanilla any roads, On brings them back, then the box shows ? (Random, counts as On)
                let pbCb = cbNamed "Extra Power Bracelet Blocks"
                if pbCb.IsNone then fail "Overworld + Extras: no Extra Power Bracelet Blocks checkbox"
                else
                    let cb = pbCb.Value
                    do! clickTo cb (Nullable false)
                    if OverworldData.ExtraPBBlocks || OverworldData.ExtraPBRandom then fail "Extra PB Off: the model is not Off"
                    for (state, col, row) in anyRoads do
                        if TrackerModel.overworldMapMarks.[col-1, row].Current() = state then fail (sprintf "Extra PB Off: the any road at %d,%d is still marked" col row)
                    do! clickTo cb (Nullable true)
                    if not OverworldData.ExtraPBBlocks || OverworldData.ExtraPBRandom then fail "Extra PB On: the model is not On"
                    for (state, col, row) in anyRoads do
                        let cur = TrackerModel.overworldMapMarks.[col-1, row].Current()
                        if cur = -1 && not (TrackerModel.owInstance.AlwaysEmpty(col-1, row)) then fail (sprintf "Extra PB On: the any road at %d,%d was not brought back" col row)
                    do! clickTo cb (Nullable())
                    if not OverworldData.ExtraPBBlocks || not OverworldData.ExtraPBRandom then fail "Extra PB ?: Random should be set and count as On"
                    // Random: the maybe-blocked outlines show until the bracelet is owned, then they are gone
                    let without = OverworldData.maybeBlockedTiles(false, false)
                    let withPB = OverworldData.maybeBlockedTiles(true, false)
                    if not (OverworldData.extraPBBlockTiles |> Set.forall without.Contains) then fail "Extra PB ?: the maybe-blocked tiles are missing"
                    if OverworldData.extraPBBlockTiles |> Set.exists withPB.Contains then fail "Extra PB ?: the outlines stay after the bracelet is owned"
                    do! clickTo cb (Nullable false)
                // the other three that can be Random: Random sets the flag and counts as On; Off clears both
                for name in [ "Extra Raft Blocks"; "Randomize Dead Woods"; "Randomize Lost Hills" ] do
                    match cbNamed name with
                    | None -> fail (sprintf "Overworld + Extras: no '%s' checkbox" name)
                    | Some cb ->
                        do! clickTo cb (Nullable())
                        if cb.IsChecked.HasValue then fail (sprintf "%s: could not reach the ? state" name)
                        if (OverworldData.maybeBlockedTiles(false, false)).IsEmpty then fail (sprintf "%s ?: no maybe-blocked tiles" name)
                        do! clickTo cb (Nullable false)
                // Boots: Off, On, or ? (the flags left it to chance: counts as Off, the boomerangs stay, until it is confirmed)
                let bootsCb = cbNamed "Boots Replace Boomerangs"
                if bootsCb.IsNone then fail "Overworld + Extras: no Boots checkbox"
                elif not bootsCb.Value.IsThreeState then fail "Boots Replace Boomerangs should have a ? (Random) state"
                else
                    let cb = bootsCb.Value
                    do! clickTo cb (Nullable true)
                    if not TrackerModel.BootsReplaceBoomerangs || TrackerModel.BootsRandom then fail "Boots On: the model is not On"
                    do! clickTo cb (Nullable())
                    if TrackerModel.BootsReplaceBoomerangs || not TrackerModel.BootsRandom then fail "Boots ?: should be Random and count as Off until confirmed"
                    do! clickTo cb (Nullable false)
                    if TrackerModel.BootsReplaceBoomerangs || TrackerModel.BootsRandom then fail "Boots Off: the model is not Off"
                if (OverworldData.maybeBlockedTiles(false, false)).IsEmpty |> not then fail "everything is Off but tiles are still maybe-blocked"
                // the Close button closes the window
                let closeBtn = descendants root |> Seq.tryPick (fun d -> match d with :? ButtonBase as b when label d = "Close" -> Some b | _ -> None)
                if closeBtn.IsNone then fail "Overworld + Extras: no Close button"
                else
                    guard "Overworld + Extras Close" (fun () -> click closeBtn.Value)
                    do! pause 300
                    if popupCount() > 0 then fail "Overworld + Extras: the Close button did not close the window"
            do! settle "Overworld + Extras checks"
        log(sprintf "overworld + extras checks done (%d failures so far)" failures.Value)

    // dungeon + shop checks: in-process, they call the same SetNewValue / helpers the clicks and hotkeys end up in (nothing is clicked, nothing is heard)
    guard "dungeon and shop checks" (fun () ->
        let rooms = DungeonUI.masterRoomStatesPublic
        if isNull (box rooms) then failwith "the dungeon rooms were not built"
        TrackerModelOptions.ShowOffTileEdges.Value <- true
        let set x y (rt:RoomType) =
            let f = DungeonUI.setNewValueFunctions.[0].[x,y]
            if obj.ReferenceEquals(f, null) then failwithf "no SetNewValue for dungeon 1 room (%d,%d)" x y
            let r = rooms.[0].[x,y].Clone()
            r.RoomType <- rt
            f r
        let typeAt x y = rooms.[0].[x,y].RoomType
        let hDoor x y = DungeonUI.horizontalDoorsPerLevel.[0].[x,y]
        let vDoor x y = DungeonUI.verticalDoorsPerLevel.[0].[x,y]
        let marked = RoomType.StartEnterFromS
        let off, unmarked = RoomType.OffTheMap, RoomType.Unmarked
        // 1. a room set OFF removes a hand-placed 'no' wall between it and a marked neighbour
        set 3 3 marked
        (hDoor 3 3).State <- Dungeon.DoorState.NO
        set 4 3 off
        if (hDoor 3 3).State <> Dungeon.DoorState.UNKNOWN then fail "OFF room: the 'no' wall toward the marked neighbour was not removed"
        // 2. a third copy of a transport is refused and leaves the room untouched; after unmarking one, a third fits
        let t1 = RoomType.Transport1
        set 0 6 t1
        set 2 6 t1
        set 4 6 t1
        if typeAt 4 6 <> unmarked then fail "transport: a third copy was placed"
        set 0 6 unmarked
        set 4 6 t1
        if typeAt 4 6 <> t1 then fail "transport: after unmarking one copy, the third could not be placed"
        // 3. an Old Man (hint) room puts an unbombable wall on its top side, and taking the room away takes the wall back
        set 6 5 RoomType.OldManHint
        if (vDoor 6 4).State <> Dungeon.DoorState.NO then fail "old man room: no top wall was placed"
        set 6 5 unmarked
        if (vDoor 6 4).State <> Dungeon.DoorState.UNKNOWN then fail "old man room: the top wall stayed after the room was removed"
        // 4. shops: a numbered shop label goes away with the tile's icons; only secrets / door repair / money making game replace a shop
        let ms = TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET
        if not (WPFUI.tileTypeReplacesShop ms) then fail "shop: a secret should replace a shop"
        if WPFUI.tileTypeReplacesShop TrackerModel.MapSquareChoiceDomainHelper.THE_LETTER then fail "shop: the letter cave should not replace a shop"
        WPFUI.setShopLabel(2, 2, 1)
        if TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[2,2]) <> 1 then fail "shop: the SHOP 1 label was not set"
        WPFUI.clearAllTileIcons(2, 2)
        if TrackerModel.shopLabelOf(TrackerModel.owSwordIcons.[2,2]) <> 0 then fail "shop: clearing the tile left the shop label"
        // put everything back
        for (x, y) in [ (3,3); (4,3); (0,6); (2,6); (4,6); (6,5) ] do (try set x y unmarked with _ -> ())
        for (x, y) in [ (3,3) ] do (hDoor x y).State <- Dungeon.DoorState.UNKNOWN)
    log(sprintf "dungeon and shop checks done (%d failures so far)" failures.Value)

    closeExtraWindows ()
    log(sprintf "%d button/checkbox clicks, %d popups opened and closed" clicks.Value popupsSeen.Value)
    log(if failures.Value = 0 then "RESULT: PASS" else sprintf "RESULT: FAIL (%d)" failures.Value)
    Application.Current.Shutdown(if failures.Value = 0 then 0 else 1)
    }

let arm() =
    (try File.Delete(reportPath) with _ -> ())
    // any window the test opens (popouts and so on) goes off the screen and out of the taskbar as soon as it loads
    EventManager.RegisterClassHandler(typeof<Window>, FrameworkElement.LoadedEvent, new RoutedEventHandler(fun s _ ->
        match s with
        | :? Window as w -> w.WindowStartupLocation <- WindowStartupLocation.Manual; w.Left <- -4000.; w.ShowInTaskbar <- false
        | _ -> ()))
    let started = ref false
    let t = new System.Windows.Threading.DispatcherTimer(Interval=TimeSpan.FromSeconds(1.))
    t.Tick.Add(fun _ ->
        if not started.Value then
            match Application.Current.Windows |> Seq.cast<Window> |> Seq.tryFind (fun w -> w.Title = "ZORATracker") with
            | Some w when w.IsLoaded && not (isNull w.Content) ->
                started.Value <- true
                t.Stop()
                w.WindowStartupLocation <- WindowStartupLocation.Manual
                w.Left <- -4000.   // off the screen, so it does not get in the way
                w.ShowInTaskbar <- false
                Async.StartWithContinuations(run w, ignore, (fun e -> log("FAIL: the test itself broke: " + e.ToString()); Application.Current.Shutdown(2)), ignore)
            | _ -> ())
    t.Start()
