module OptionsMenu

open System.Windows.Controls
open System.Windows.Media
open System.Windows

open CustomComboBoxes.GlobalFlag

let voice = new System.Speech.Synthesis.SpeechSynthesizer()
let defaultVoice = try voice.Voice.Name with _ -> ""
let InitializeVoice() =
    try 
        voice.Volume <- TrackerModelOptions.Volume
        voice.SelectVoice(TrackerModelOptions.PreferredVoice)
    with _ -> ()

let mutable microphoneFailedToInitialize = false
let mutable gamepadFailedToInitialize = false

let mutable userWantsMute = false
let mutable raceModeMuted = false




let broadcastWindowOptionChanged = new Event<unit>()
let mouseMagnifierWindowOptionChanged = new Event<unit>()
let BOARDInsteadOfLEVELOptionChanged = new Event<unit>()
let displayIconsInDungeonMapOptionChanged = new Event<unit>()
let showDungeonHintIconsOptionChanged = new Event<unit>()
let secondQuestDungeonsOptionChanged = new Event<unit>()
let showBasementInfoOptionChanged = new Event<unit>()
let bookForHelpfulHintsOptionChanged = new Event<unit>()
let requestRedrawOverworldEvent = new Event<unit>()
let hideTimerChanged = new Event<unit>()
let dungeonSunglassesChanged = new Event<unit>()
let raceModeChanged = new Event<unit>()

let isValidUrl (url: string) =
    System.Uri.TryCreate(url, System.UriKind.Absolute) |> fst


let link(cb:CheckBox, b:TrackerModelOptions.Bool, needFU, otherEffect) =
    let effect() = 
        if needFU then 
            TrackerModel.forceUpdate()
        otherEffect()
    cb.IsChecked <- System.Nullable.op_Implicit b.Value
    cb.Checked.Add(fun _ -> b.Value <- true; effect())
    cb.Unchecked.Add(fun _ -> b.Value <- false; effect())

let data1o(isStandardHyrule) = 
    [|
    // These features are disabled by the app when not(isStandardHyrule)
    if isStandardHyrule then yield "Draw Routes", "Constantly display routing lines when mousing over overworld tiles", TrackerModelOptions.Overworld.DrawRoutes, true, (fun()->()), None
    if isStandardHyrule then yield "Show Screen Scrolls", "Routing lines assume the player can screen scroll\nScreen scrolls appear as curved lines", TrackerModelOptions.Overworld.RoutesCanScreenScroll, true, (fun()->()), Some(Thickness(20.,0.,0.,0.))
    if isStandardHyrule then yield "Highlight Nearby", "Highlight nearest unmarked gettable overworld tiles when mousing", TrackerModelOptions.Overworld.HighlightNearby, false, (fun()->()), None
    if isStandardHyrule then yield "Hover On Zones/Coords", "Hovering the Zones or Coords checkbox shows that overlay while the mouse is over it", TrackerModelOptions.Overworld.HoverZonesCoords, false, (fun()->()), None
    if isStandardHyrule then yield "Show Magnifier", "Display magnified view of overworld tiles when mousing", TrackerModelOptions.Overworld.ShowMagnifier, false, (fun()->()), None
    yield "Use Old Popups", "Check this to show the old picture popups (instead of the button boxes)\nwhen marking the wood sword cave and the take-any caves", TrackerModelOptions.Overworld.UseOldPopups, false, (fun()->()), None
    // Mirror overworld is not useful when not(isStandardHyrule), but if the user has it checked, we want them to be able to uncheck it
// now a clickable feature of top tracker area
//    yield "Mirror overworld", "Flip the overworld map East<->West", TrackerModelOptions.Overworld.MirrorOverworld, true, (fun()->()), None
    |]

let data1d = [|
    "Dungeon Map Hint", "Check this box to add a transparent icon to the dungeon map rooms in order to better guess where that room is", TrackerModelOptions.DisplayIconsInDungeonMap, false, displayIconsInDungeonMapOptionChanged.Trigger
// now a clickable feature of top tracker area
//    "Second quest dungeons", "Check this if dungeon 4, rather than dungeon 1, has 3 items (no effect when Hidden Dungeon Numbers)", TrackerModelOptions.IsSecondQuestDungeons, false, secondQuestDungeonsOptionChanged.Trigger
    "Show Dungeon Hint Icons", "Uncheck to hide the dim question-mark icons above the dungeons and other items\n(where you click to pick a location hint)", TrackerModelOptions.ShowDungeonHintIcons, false, showDungeonHintIconsOptionChanged.Trigger
    "BOARD Instead Of LEVEL", "Check this to label the dungeon tabs and headers BOARD-1 ... BOARD-9\ninstead of LEVEL-1 ... LEVEL-9", TrackerModelOptions.BOARDInsteadOfLEVEL, false, BOARDInsteadOfLEVELOptionChanged.Trigger
    "Show Basement Info", "Check this if empty dungeon item boxes should suggest whether they are found as\nbasement items rather than floor drops (no effect when Hidden Dungeon Numbers)", TrackerModelOptions.ShowBasementInfo, false, showBasementInfoOptionChanged.Trigger
    "Do Door Inference", "Check this to mark a green door when you mark a new room, if the point of entry can be inferred", TrackerModelOptions.DoDoorInference, false, fun()->()
    "Book For Helpful Hints", "Check this if both 'Book To Understand Old Men' flag is on, and\n'Helpful' hints are available. The tracker will let you left-click\nOld Man Hint rooms to toggle whether you have read them yet.", TrackerModelOptions.BookForHelpfulHints, false, bookForHelpfulHintsOptionChanged.Trigger
    "Alphabetize Hint Zone List", "When checked, the hint zone dropdown (where you mark which zone a dungeon/sword is in)\nshows zones in alphabetical order instead of geographic order.", TrackerModelOptions.AlphabetizeHintZones, false, (fun()->())
    "Left-Drag Auto-Inverts", "Painting maps: When checked, If your first drag is with left-click,\nand you've not yet inverted OffTheMap with Unmarked, then\nauto-invert when left-click-dragging, to immediately start painting a map.", TrackerModelOptions.LeftClickDragAutoInverts, false, (fun()->())
    "Default To NonDescript", "Room default: When checked, clicking an Unmarked room will mark it as\nNonDescript (empty box) rather than MaybePushBlock (box with two dots)", TrackerModelOptions.DefaultRoomPreferNonDescriptToMaybePushBlock, false, (fun()->())
    "Dungeon 'Sunglasses'", "The dungeon tracker has high contrast (bright colors against black);\nTurn this on to darken the colors somewhat to reduce bright contrast", TrackerModelOptions.GiveDungeonTrackerSunglasses, false, dungeonSunglassesChanged.Trigger
    "Reverse Scroll Wheel", "When unchecked: scroll up = monster popup, scroll down = floor-drop/item popup.\nWhen checked: scroll up = floor-drop/item popup, scroll down = monster popup.\n(Dungeon rooms)", TrackerModelOptions.ReverseScrollWheelDirection, false, (fun()->())
    "Hide Lock And Hints", "Check this to hide the tip box and the lock bar on the dungeon room type,\nmonster and floor drop menus", TrackerModelOptions.HidePopupHintAndLock, false, (fun()->())
    |]

let data2 = [|
    TrackerModel.ReminderCategory.DungeonFeedback.DisplayName, "Note when dungeons are located/completed, triforces obtained, and go-time",
        TrackerModelOptions.VoiceReminders.DungeonFeedback, TrackerModelOptions.VisualReminders.DungeonFeedback
    TrackerModel.ReminderCategory.SwordHearts.DisplayName, "Remind to consider white/magical sword when you get 4-6 or 10-14 hearts",
        TrackerModelOptions.VoiceReminders.SwordHearts,     TrackerModelOptions.VisualReminders.SwordHearts
    TrackerModel.ReminderCategory.CoastItem.DisplayName, "Reminder to fetch to coast item when you have the ladder",
        TrackerModelOptions.VoiceReminders.CoastItem,       TrackerModelOptions.VisualReminders.CoastItem
    TrackerModel.ReminderCategory.RecorderSpots.DisplayName, "Periodic reminder of how many recorder spots remain (fires at reminder interval)",
        TrackerModelOptions.VoiceReminders.RecorderSpots,   TrackerModelOptions.VisualReminders.RecorderSpots
    TrackerModel.ReminderCategory.PowerBraceletSpots.DisplayName, "Periodic reminder of how many power-bracelet spots remain (fires at reminder interval)",
        TrackerModelOptions.VoiceReminders.PowerBraceletSpots, TrackerModelOptions.VisualReminders.PowerBraceletSpots
    TrackerModel.ReminderCategory.BoomstickBook.DisplayName, "Periodic reminder when the boomstick book is available in a found shop (fires at reminder interval)",
        TrackerModelOptions.VoiceReminders.BoomstickBook,   TrackerModelOptions.VisualReminders.BoomstickBook
    TrackerModel.ReminderCategory.HaveMagicKey.DisplayName, "One-time reminder when entering a dungeon that you have the magic key",
        TrackerModelOptions.VoiceReminders.HaveMagicKey,    TrackerModelOptions.VisualReminders.HaveMagicKey
    TrackerModel.ReminderCategory.HaveLadder.DisplayName, "One-time reminder when entering a dungeon that you have the ladder",
        TrackerModelOptions.VoiceReminders.HaveLadder,      TrackerModelOptions.VisualReminders.HaveLadder
    TrackerModel.ReminderCategory.Blockers.DisplayName, "Reminder when you may have become unblocked on a previously-aborted dungeon",
        TrackerModelOptions.VoiceReminders.Blockers,        TrackerModelOptions.VisualReminders.Blockers
    TrackerModel.ReminderCategory.DoorRepair.DisplayName, "Each time you uncover a door repair charge, remind the count of how many you have found",
        TrackerModelOptions.VoiceReminders.DoorRepair,        TrackerModelOptions.VisualReminders.DoorRepair
    TrackerModel.ReminderCategory.OverworldOverwrites.DisplayName, "Each time you make a destructive change to an overworld mark, remind the change, in case it was accidental",
        TrackerModelOptions.VoiceReminders.OverworldOverwrites, TrackerModelOptions.VisualReminders.OverworldOverwrites
    |]

let showCoopHostSettingsWindow(cm: CustomComboBoxes.CanvasManager) =
    let wh = new System.Threading.ManualResetEvent(false)
    let window = new Window(Title = "Co-Op Host Settings", Width = 400.0, Height = 300.0)
    let sp = new StackPanel(Orientation = Orientation.Vertical, Margin = Thickness(10.))

    // Apply modal style
    let AddStyle(e:FrameworkElement) =
        let textboxStyle = new Style(typeof<TextBox>)
        textboxStyle.Setters.Add(new Setter(TextBox.BorderThicknessProperty, Thickness(0.)))
        textboxStyle.Setters.Add(new Setter(TextBox.BorderBrushProperty, Brushes.DarkGray))
        textboxStyle.Setters.Add(new Setter(TextBox.FontSizeProperty, 16.))
        textboxStyle.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.Orange))
        textboxStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, Brushes.Black))
        e.Resources.Add(typeof<TextBox>, textboxStyle)

        let checkboxStyle = new Style(typeof<CheckBox>)
        checkboxStyle.Setters.Add(new Setter(CheckBox.HeightProperty, 22.))
        checkboxStyle.Setters.Add(new Setter(Control.TemplateProperty, Graphics.roundedCheckBoxTemplate))
        checkboxStyle.Setters.Add(new Setter(Control.PaddingProperty, Thickness(4.,0.,0.,0.)))
        e.Resources.Add(typeof<CheckBox>, checkboxStyle)

    AddStyle(sp)

    let mutable isSessionEnabled = TrackerModelOptions.CoopHostSession

    let enableCheck = new CheckBox(Content = new TextBox(Text="Enable Hosting", IsReadOnly=true))
    enableCheck.IsChecked <- System.Nullable.op_Implicit(isSessionEnabled)
    enableCheck.Checked.Add(fun _ -> isSessionEnabled <- true)
    enableCheck.Unchecked.Add(fun _ -> isSessionEnabled <- false)
    sp.Children.Add(enableCheck) |> ignore

    let portLabel = new TextBox(Text = "Port Number:", IsReadOnly = true)
    sp.Children.Add(portLabel) |> ignore

    let portBox = new TextBox(Text = TrackerModelOptions.PortNumber)
    portBox.HorizontalAlignment <- HorizontalAlignment.Stretch
    portBox.TextAlignment <- TextAlignment.Left

    let portBoxBorder = new Border(
        Child = portBox,
        BorderBrush = Brushes.Orange,
        BorderThickness = Thickness(2.),
        Margin = Thickness(0., 2., 0., 6.)
    )
    sp.Children.Add(portBoxBorder) |> ignore

    let portValidationMessage = new TextBlock(
        Text = "Port must be a number between 1024 and 65535",
        Foreground = Brushes.Red,
        Visibility = Visibility.Collapsed,
        Margin = Thickness(0., 0., 0., 6.)
    )
    sp.Children.Add(portValidationMessage) |> ignore

    let launchButton = Graphics.makeButton("Launch", None, None)
    launchButton.HorizontalAlignment <- HorizontalAlignment.Left

    launchButton.Click.Add(fun _ ->
        let port = portBox.Text.Trim()
        TrackerModelOptions.PortNumber <- port
        if not isSessionEnabled then
            isSessionEnabled <- true
            enableCheck.IsChecked <- System.Nullable.op_Implicit true
        TrackerModelOptions.CoopHostSession <- true
        TrackerModelOptions.CoopSyncOptions.SetEnableCoop(true)
        Dungeon.startLocalSignalRHost port
        wh.Set() |> ignore
        window.Close()
    )

    let stopButton = Graphics.makeButton("Stop", None, None)
    stopButton.HorizontalAlignment <- HorizontalAlignment.Left

    stopButton.Click.Add(fun _ ->
        Dungeon.stopLocalSignalRHost()
        if isSessionEnabled then
            isSessionEnabled <- false
            enableCheck.IsChecked <- System.Nullable.op_Implicit false
        TrackerModelOptions.CoopHostSession <- false
        wh.Set() |> ignore
        window.Close()
    )

    sp.Children.Add(launchButton) |> ignore
    sp.Children.Add(stopButton) |> ignore

    let stylePanel = new Border(
        Child = sp,
        BorderBrush = Brushes.Gray,
        BorderThickness = Thickness(3.),
        Background = Brushes.Black,
        Width = 400.,
        HorizontalAlignment = HorizontalAlignment.Center
    )

    let validatePort () =
        let text = portBox.Text.Trim()
        let mutable valid = false
        match System.Int32.TryParse(text) with
        | (true, port) when port >= 1024 && port <= 65535 -> valid <- true
        | _ -> ()
        portBoxBorder.BorderBrush <- if valid then Brushes.Orange else Brushes.Red
        portValidationMessage.Visibility <- if valid then Visibility.Collapsed else Visibility.Visible
        enableCheck.IsEnabled <- valid
        launchButton.IsEnabled <- valid && (System.Nullable.op_Explicit(enableCheck.IsChecked) = true)

    portBox.TextChanged.Add(fun _ -> validatePort())
    enableCheck.Checked.Add(fun _ -> validatePort())
    enableCheck.Unchecked.Add(fun _ -> validatePort())

    validatePort()

    async {
        CustomComboBoxes.GlobalFlag.popupIsActive <- true
        do! CustomComboBoxes.DoModalDocked(cm, wh, Dock.Bottom, stylePanel)
        CustomComboBoxes.GlobalFlag.popupIsActive <- false
    } |> Async.StartImmediate





// ---------------- Guided Tour ----------------
// A step-by-step tour: the screen is dimmed except one section, which is outlined and described. Sections are found by the text of the labels in them
// (so no screen has to hand its controls over): each step is (label texts, title, description); the highlight covers all of the labels it finds.
let private tourTextOf(d:DependencyObject) : string =
    match d with
    | :? TextBox as t -> t.Text
    | :? TextBlock as t -> t.Text
    | _ -> null
let private tourFind(root:DependencyObject, pattern:string) : FrameworkElement option =
    let pat = pattern.Trim()
    let exact : FrameworkElement option ref = ref None
    let prefix : FrameworkElement option ref = ref None
    let rec walk (d:DependencyObject) =
        if exact.Value.IsNone then
            let t = tourTextOf d
            if not (isNull t) then
                let fe = d :?> FrameworkElement
                if fe.IsVisible && fe.ActualWidth > 0. then
                    let tt = t.Trim()
                    if tt = pat then exact.Value <- Some fe
                    elif prefix.Value.IsNone && tt.StartsWith(pat) then prefix.Value <- Some fe
            for k in 0 .. VisualTreeHelper.GetChildrenCount(d) - 1 do
                walk (VisualTreeHelper.GetChild(d, k))
    walk root
    match exact.Value with
    | Some _ -> exact.Value
    | None -> prefix.Value
// a label inside a button or checkbox: use the whole control (so the checkbox square is inside the highlight)
let private tourWholeControl(fe:FrameworkElement) : FrameworkElement =
    let rec up (d:DependencyObject) (n:int) : FrameworkElement =
        if isNull d || n > 5 then fe
        else
            match d with
            | :? Primitives.ButtonBase as b -> (b :> FrameworkElement)
            | _ -> up (VisualTreeHelper.GetParent d) (n + 1)
    up (VisualTreeHelper.GetParent fe) 0
// the in-app Options popup closes itself (set by the main window) so the main window tour can start
let mutable optionsTourRequested = false
let mutable closeOptionsPopupHook : unit -> unit = fun () -> ()
// the start screen tour ends by starting a run, then the main window tour follows (the hook starts the run; Program.fs sets it)
let mutable startBlankRunHook : unit -> unit = fun () -> ()
let mutable mainTourAfterStart = false
// (continueLabel: when not empty, the last step's button has this label and, once clicked, onContinue runs after the tour closes)
let runGuidedTourCore(cm:CustomComboBoxes.CanvasManager, steps:(string list * string * string) list, continueLabel:string, onContinue:unit -> unit) =
    if not CustomComboBoxes.GlobalFlag.popupIsActive then
        let found =
            steps |> List.choose (fun (pats, title, body) ->
                let rects =
                    pats |> List.choose (fun p ->
                        // "@x0,y0,x1,y1" is a region given as fractions of the whole window (for parts of the screen that have no label text)
                        if p.StartsWith("@") then
                            try
                                let v = p.Substring(1).Split(',') |> Array.map (fun t -> System.Double.Parse(t, System.Globalization.CultureInfo.InvariantCulture))
                                let cw, ch = cm.AppMainCanvas.Width, cm.AppMainCanvas.Height
                                Some (Rect(v.[0] * cw, v.[1] * ch, (v.[2] - v.[0]) * cw, (v.[3] - v.[1]) * ch))
                            with _ -> None
                        else
                            match tourFind(cm.AppMainCanvas, p) with
                            | Some fe ->
                                let w = tourWholeControl fe
                                (try
                                    let t = w.TransformToAncestor(cm.AppMainCanvas)
                                    Some (t.TransformBounds(Rect(0., 0., w.ActualWidth, w.ActualHeight)))
                                 with _ -> None)
                            | None -> None)
                if rects.IsEmpty then None else Some (List.reduce (fun a b -> Rect.Union(a, b)) rects, title, body))
            |> Array.ofList
        if found.Length > 0 then
            CustomComboBoxes.GlobalFlag.popupIsActive <- true
            async {
                let continued = ref false
                try
                    let wh = new System.Threading.ManualResetEvent(false)
                    let W, H = cm.AppMainCanvas.Width, cm.AppMainCanvas.Height
                    let overlay = new Canvas(Width=W, Height=H, Background=Brushes.Transparent)
                    let idx = ref 0
                    let mkButton (text:string) =
                        new Button(Content=text, Margin=Thickness(3.), Padding=Thickness(10.,2.,10.,2.), Focusable=false, FontSize=15., Background=Brushes.Black, Foreground=Brushes.Orange, BorderBrush=Brushes.Orange)
                    let rec show () =
                        overlay.Children.Clear()
                        let (r, title, body) = found.[idx.Value]
                        let pad = 5.
                        let hx, hy = max 0. (r.X - pad), max 0. (r.Y - pad)
                        let hole = Rect(hx, hy, min (W - hx) (r.Width + 2.*pad), min (H - hy) (r.Height + 2.*pad))
                        let geo = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(Rect(0., 0., W, H)), new RectangleGeometry(hole))
                        overlay.Children.Add(new System.Windows.Shapes.Path(Data=geo, Fill=Brushes.Black, Opacity=0.75, IsHitTestVisible=false)) |> ignore
                        let frame = new System.Windows.Shapes.Rectangle(Width=hole.Width, Height=hole.Height, Stroke=Brushes.Orange, StrokeThickness=3., RadiusX=4., RadiusY=4., IsHitTestVisible=false)
                        Canvas.SetLeft(frame, hole.X)
                        Canvas.SetTop(frame, hole.Y)
                        overlay.Children.Add(frame) |> ignore
                        let sp = new StackPanel(Orientation=Orientation.Vertical, Margin=Thickness(10.))
                        sp.Children.Add(new TextBlock(Text=title, Foreground=Brushes.Orange, FontSize=20., FontWeight=FontWeights.Bold, TextWrapping=TextWrapping.Wrap)) |> ignore
                        sp.Children.Add(new TextBlock(Text=body, Foreground=Brushes.White, FontSize=15., TextWrapping=TextWrapping.Wrap, Margin=Thickness(0.,6.,0.,8.))) |> ignore
                        let buttons = new DockPanel(LastChildFill=false)
                        let counter = new TextBlock(Text=sprintf "%d / %d" (idx.Value + 1) found.Length, Foreground=Brushes.Gray, FontSize=14., VerticalAlignment=VerticalAlignment.Center, Margin=Thickness(0.,0.,12.,0.))
                        let skipBtn = mkButton (if idx.Value = found.Length - 1 then " Close " else " Skip tour ")
                        let backBtn = mkButton " Back "
                        let nextBtn = mkButton (if idx.Value = found.Length - 1 then (if continueLabel <> "" then continueLabel else " Done ") else " Next ")
                        backBtn.IsEnabled <- idx.Value > 0
                        skipBtn.Click.Add(fun _ -> wh.Set() |> ignore)
                        backBtn.Click.Add(fun _ -> idx.Value <- idx.Value - 1; show ())
                        nextBtn.Click.Add(fun _ ->
                            if idx.Value >= found.Length - 1 then
                                if continueLabel <> "" then continued.Value <- true
                                wh.Set() |> ignore
                            else
                                idx.Value <- idx.Value + 1
                                show ())
                        DockPanel.SetDock(counter, Dock.Left)
                        DockPanel.SetDock(nextBtn, Dock.Right)
                        DockPanel.SetDock(backBtn, Dock.Right)
                        DockPanel.SetDock(skipBtn, Dock.Right)
                        buttons.Children.Add(counter) |> ignore
                        buttons.Children.Add(nextBtn) |> ignore
                        buttons.Children.Add(backBtn) |> ignore
                        buttons.Children.Add(skipBtn) |> ignore
                        sp.Children.Add(buttons) |> ignore
                        let callout = new Border(Child=sp, Background=Brushes.Black, BorderBrush=Brushes.Orange, BorderThickness=Thickness(3.), Width=430.)
                        callout.Measure(Size(System.Double.PositiveInfinity, System.Double.PositiveInfinity))
                        let cw, ch = callout.DesiredSize.Width, callout.DesiredSize.Height
                        let clampX x = max 0. (min (W - cw) x)
                        let clampY y = max 0. (min (H - ch) y)
                        let gap = 12.
                        // below the highlight if it fits, else above, else beside it, else at the roomier end of the screen
                        let cx, cy =
                            if hole.Bottom + gap + ch <= H then (clampX hole.X, hole.Bottom + gap)
                            elif hole.Top - gap - ch >= 0. then (clampX hole.X, hole.Top - gap - ch)
                            elif hole.Right + gap + cw <= W then (hole.Right + gap, clampY hole.Y)
                            elif hole.Left - gap - cw >= 0. then (hole.Left - gap - cw, clampY hole.Y)
                            else (clampX hole.X, (if hole.Top > H - hole.Bottom then 0. else H - ch))
                        Canvas.SetLeft(callout, cx)
                        Canvas.SetTop(callout, cy)
                        overlay.Children.Add(callout) |> ignore
                    show ()
                    do! CustomComboBoxes.DoModalOpa(cm, wh, 0., 0., overlay, 0.0)
                finally
                    CustomComboBoxes.GlobalFlag.popupIsActive <- false
                if continued.Value then onContinue ()
            } |> Async.StartImmediate
let runGuidedTour(cm:CustomComboBoxes.CanvasManager, steps:(string list * string * string) list) =
    runGuidedTourCore(cm, steps, "", (fun () -> ()))

// the main window tour. ("@x0,y0,x1,y1" regions are fractions of the window, for parts with no label text)
let mainScreenTourSteps : (string list * string * string) list =
    [ ["@0.0,0.0,0.358,0.063"], "Triforce and levels",
        "The dungeon level numbers and triforce pieces along the top. Mark each dungeon as you finish it."
      ["@0.0,0.063,0.358,0.154"], "Dungeon item boxes",
        "One row of boxes for each dungeon, holding what you found there. Click a box to set its item; a green border means collected."
      ["@0.363,0.03,0.572,0.156"], "Items and hearts",
        "The items you have found, your hearts, and Max Hearts. Click to mark what you own."
      ["@0.572,0.004,0.684,0.13"], "Counters",
        "Progress counters: how many overworld spots are left to check, plus counts of items and open spots."
      ["Popout Windows"; "1Q/2Q/Blocks"], "Tools",
        "Popout Windows opens extra windows, Hint Decoder helps with hints, Hotkeys lists the keyboard shortcuts, Log shows the reminders that have played, Draw/Custom Icons lets you draw on the map, and 1Q/2Q/Blocks sets the quest and block options."
      ["TIMER"; "Save"], "Timer and file",
        "Pause/Reset timer controls the run clock. Options opens the settings (and this tour). Save writes a save file you can load later."
      ["@0.0,0.154,1.0,0.43"], "Overworld map",
        "Click a tile to mark what is there: caves, shops, dungeon entrances and more. Right click for more choices, and fill in the details as you explore."
      ["Any Road"; "Lost Hills"], "Marker legend",
        "The special spots you can place on the map: Any Road (warp), completed dungeons, the recorder destination, your Start Spot, Dead Woods and Lost Hills."
      ["@0.0,0.467,0.726,0.495"], "Item Progress",
        "A quick view of the items and upgrades you have collected so far."
      ["@0.0,0.496,0.586,0.517"], "Dungeon tabs",
        "Tabs for dungeons 1 to 9, and S for the summary. Click one to open that dungeon's map. 1Q and 2Q switch between the first and second quest layouts."
      ["@0.0,0.517,0.586,0.867"], "Dungeon boards",
        "A small map of each dungeon. Hover to preview a dungeon's tab, and click to switch to it."
      ["@0.591,0.496,1.0,0.607"], "Blockers",
        "Note what is blocking each dungeon, so you remember what you still need."
      ["Notes"; "Aa"], "Notes",
        "Free text notes for the run. The Aa button changes the text size."
      ["@0.0,0.873,1.0,1.0"], "Timeline",
        "The Overworld and Collection Timeline: when you found each item and how the overworld filled in over the run, with the seed and flags along the top." ]

// the start screen tour: (labels that mark the section, title, description)
let startScreenTourSteps : (string list * string * string) list =
    [ ["Pick Overworld (Start Game)"], "Start a new run",
        "Click here and pick which overworld the seed uses to begin a new run. The checkboxes below it are set before you start."
      ["Heart Shuffle"; "Full"], "Heart Shuffle",
        "Set this to match how the seed shuffled the heart containers: Off, Intra or Full."
      ["Legacy Mode"; "Mark Bomb Droppers"], "Startup options",
        "Check the ones your seed uses. Legacy Mode limits shops to the original game's items, Hide Dungeon Numbers hides the level numbers, Mirrored Overworld flips the map, and Mark Bomb Droppers lets you mark bomb-dropper spots."
      ["ZORA Extras"; "Progressive Items"], "ZORA Extras",
        "Extra randomizer options that only some seeds use, such as 4th Sword, Boots Replace Boomerangs and Progressive Items. Check the ones your seed uses."
      ["Load Save"; "Recently loaded saves:"], "Load a save",
        "Resume an earlier run from a save file, or pick one from the recently loaded list."
      ["Overworld Settings"; "Use Old Popups"], "Overworld settings",
        "How the map behaves: route drawing, highlighting nearby tiles, the magnifier and popups. Most of these can be changed later from the Options button."
      ["Dungeon Settings"; "Hide Lock And Hints"], "Dungeon settings",
        "How the dungeon screens behave and look: hint icons, BOARD instead of LEVEL, basement info, door inference and more."
      ["Reminders"; "Change Voice"], "Reminders",
        "Volume, which voice and visual reminders you get (dungeon feedback, sword hearts, coast item and more), how often they repeat, and which voice speaks them."
      ["Auto-save"; "Auto-save Interval"], "Auto-save",
        "How often the tracker saves your run by itself, in minutes."
      ["Other"; "Race Mode"], "Other options",
        "General settings: animations, remembering your startup choices, update checks, seed and flags, listening for speech, the broadcast window, the timer and Race Mode."
      ["Co-Op Client Settings"; "Guided Tour"], "Co-Op, crash log and this tour",
        "Co-Op Client and Host settings are for tracking together with a partner. Show Crash Log opens the log file to send along when reporting a problem. Guided Tour starts this walkthrough again. Click Continue to start a new run (1st Quest Overworld) and tour the main screen." ]

let makeOptionsCanvas(cm:CustomComboBoxes.CanvasManager, includePopupExplainer, isStandardHyrule) = 
    let width = cm.AppMainCanvas.Width
    let all = new Border(BorderThickness=Thickness(2.), BorderBrush=Brushes.DarkGray, Background=Brushes.Black)
    let optionsAllsp = new StackPanel(Orientation=Orientation.Horizontal, Width=width, Background=Brushes.Black)
    let AddStyle(e:FrameworkElement) = 
        let style = new Style(typeof<TextBox>)
        style.Setters.Add(new Setter(TextBox.BorderThicknessProperty, Thickness(0.)))
        style.Setters.Add(new Setter(TextBox.BorderBrushProperty, Brushes.DarkGray))
        style.Setters.Add(new Setter(TextBox.FontSizeProperty, 16.))
        style.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.Orange))
        style.Setters.Add(new Setter(TextBox.BackgroundProperty, Brushes.Black))
        e.Resources.Add(typeof<TextBox>, style)
        let style = new Style(typeof<CheckBox>)
        style.Setters.Add(new Setter(CheckBox.HeightProperty, 22.))
        style.Setters.Add(new Setter(Control.TemplateProperty, Graphics.roundedCheckBoxTemplate))
        style.Setters.Add(new Setter(Control.PaddingProperty, Thickness(4.,0.,0.,0.)))
        e.Resources.Add(typeof<CheckBox>, style)
    AddStyle(all)

    let header(tb:TextBox) = 
        tb.Margin <- Thickness(0., 0., 0., 6.)
        tb.BorderThickness <- Thickness(0., 0., 0., 1.)
        tb
    let options1sp = new StackPanel(Orientation=Orientation.Vertical, Margin=Thickness(10.,0.,10.,0.))
    let tb = new TextBox(Text="Overworld Settings", IsReadOnly=true, FontWeight=FontWeights.Bold) |> header
    options1sp.Children.Add(tb) |> ignore
    // Options marked as "advanced" only appear in the main-app options popup (not the startup screen),
    // to keep the startup screen compact and avoid layout overflow.
    let advancedOverworldOptions : string[] = [||]   // (none right now) options listed here only appear in the main-app options popup, not the startup screen
    let advancedDungeonOptions : string[] = [||]  // (none right now)
    let visibleOverworldOpts =
        data1o(isStandardHyrule)
        |> Array.filter (fun (text,_,_,_,_,_) -> includePopupExplainer || not (Array.contains text advancedOverworldOptions))
    for text,tip,b,needFU,oe,marginOpt in visibleOverworldOpts do
        let cb = new CheckBox(Content=new TextBox(Text=text,IsReadOnly=true))
        if marginOpt.IsSome then
            cb.Margin <- marginOpt.Value
        cb.ToolTip <- tip
        ToolTipService.SetShowDuration(cb, 10000)
        link(cb, b, needFU, oe)
        options1sp.Children.Add(cb) |> ignore
    options1sp.Children.Add(new DockPanel(Height=24.)) |> ignore   // a blank row between the overworld settings and this header
    let tb = new TextBox(Text="Dungeon Settings", IsReadOnly=true, FontWeight=FontWeights.Bold) |> header
    options1sp.Children.Add(tb) |> ignore
    // open the folder containing the crash log (with the log file selected), to make it easy to send to the developer
    let crashLogButton = Graphics.makeButton(" Show Crash Log... ",None,None)
    crashLogButton.HorizontalAlignment <- HorizontalAlignment.Left
    crashLogButton.Margin <- Thickness(0.,10.,0.,0.)
    crashLogButton.ToolTip <- "Open the folder containing Z1R_Tracker_crash_log.txt with the file selected,\nso you can send it along when reporting a problem"
    crashLogButton.Click.Add(fun _ ->
        try
            let dir = System.AppDomain.CurrentDomain.BaseDirectory
            let file = System.IO.Path.Combine(dir, "Z1R_Tracker_crash_log.txt")
            let args = if System.IO.File.Exists(file) then sprintf "/Select, \"%s\"" file else sprintf "\"%s\"" dir
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("Explorer.exe", args)) |> ignore
        with _ -> ()
        )
    let visibleDungeonOpts =
        data1d
        |> Array.filter (fun (text,_,_,_,_) -> includePopupExplainer || not (Array.contains text advancedDungeonOptions))
    for text,tip,b,needFU,oe in visibleDungeonOpts do
        let cb = new CheckBox(Content=new TextBox(Text=text,IsReadOnly=true))
        cb.ToolTip <- tip
        ToolTipService.SetShowDuration(cb, 10000)
        link(cb, b, needFU, oe)
        options1sp.Children.Add(cb) |> ignore
        if text = "Dungeon Map Hint" then
            let sliderSp = new StackPanel(Orientation=Orientation.Horizontal, Margin=Thickness(4.,0.,0.,4.))
            let opacityLabel = new TextBox(Text="Opacity",IsReadOnly=true)
            sliderSp.Children.Add(opacityLabel) |> ignore
            let opacitySlider = new Slider(Orientation=Orientation.Horizontal, Minimum=0.05, Maximum=1.0,
                                           TickFrequency=0.05, TickPlacement=Primitives.TickPlacement.BottomRight,
                                           IsSnapToTickEnabled=true, Width=100., Margin=Thickness(4.,0.,0.,0.))
            opacitySlider.Value <- TrackerModelOptions.DungeonMapLocationHintOpacity
            opacitySlider.ValueChanged.Add(fun _ ->
                TrackerModelOptions.DungeonMapLocationHintOpacity <- opacitySlider.Value
                displayIconsInDungeonMapOptionChanged.Trigger()
            )
            sliderSp.Children.Add(opacitySlider) |> ignore
            sliderSp.IsEnabled <- b.Value
            sliderSp.Opacity <- if b.Value then 1.0 else 0.4
            cb.Checked.Add(fun _ -> sliderSp.IsEnabled <- true; sliderSp.Opacity <- 1.0)
            cb.Unchecked.Add(fun _ -> sliderSp.IsEnabled <- false; sliderSp.Opacity <- 0.4)
            options1sp.Children.Add(sliderSp) |> ignore
    optionsAllsp.Children.Add(options1sp) |> ignore

    //This section starts the reminders panel in the initial startup screen
    //The stack panel here is the middle column of content. Everything needs to be added to options2sp in order to render in this section. it is generally added top to bottom
    let options2sp = new StackPanel(Orientation=Orientation.Vertical, Margin=Thickness(10.,2.,10.,0.))
    let tb = new TextBox(Text="Reminders", IsReadOnly=true, FontWeight=FontWeights.Bold) |> header
    //This is an example of how to add a text box to the middle column of content
    options2sp.Children.Add(tb) |> ignore
    // volume and slider
    // This is a new StackPenel uniquely for the volume slider
    let options2Topsp = new StackPanel(Orientation=Orientation.Horizontal, Margin=Thickness(0.,0.,0.,6.))
    let volumeText = new TextBox(Text="Volume",IsReadOnly=true, Margin=Thickness(0.))
    options2Topsp.Children.Add(volumeText) |> ignore
    let slider = new Slider(Orientation=Orientation.Horizontal, Maximum=100., TickFrequency=10., TickPlacement=Primitives.TickPlacement.Both, IsSnapToTickEnabled=true, Width=200.)
    slider.Value <- float TrackerModelOptions.Volume
    //The below is an important control for the UI. The slider.ValueChanged.Add(fun _ ->  is the event handler for the slider. It is triggered when the slider value changes.
    //The code inside the event handler is executed when the slider value changes.
    //This specifically keys to data that is part of the TrackerModelOptions class. ALSO the voice.Volume <- TrackerModelOptions.Volume is important for the voice to actually change.
    //The Graphics.volumeChanged.Trigger seems redundant but is important for the UI to update correctly.
    slider.ValueChanged.Add(fun _ -> 
        TrackerModelOptions.Volume <- int slider.Value
        Graphics.volumeChanged.Trigger(TrackerModelOptions.Volume)
        if not(TrackerModelOptions.IsMuted) then 
            voice.Volume <- TrackerModelOptions.Volume
        )
    let dp = new DockPanel(VerticalAlignment=VerticalAlignment.Center, Margin=Thickness(0.))
    dp.Children.Add(slider) |> ignore
    options2Topsp.Children.Add(dp) |> ignore
    options2sp.Children.Add(options2Topsp) |> ignore
    // stop
    let muteCB = new CheckBox(Content=new TextBox(Text="Disable All",IsReadOnly=true))
    muteCB.ToolTip <- "Turn off all reminders (but you can still view them by\nclicking the reminder log in the upper-right of the timeline)"
    muteCB.IsChecked <- System.Nullable.op_Implicit TrackerModelOptions.IsMuted
    muteCB.Checked.Add(fun _ -> TrackerModelOptions.IsMuted <- true; voice.Volume <- 0)
    muteCB.Unchecked.Add(fun _ -> TrackerModelOptions.IsMuted <- false; voice.Volume <- TrackerModelOptions.Volume)
    options2sp.Children.Add(muteCB) |> ignore
    // reset every setting to its default: delete the settings file and restart (only offered in the in-app options popup)
    if includePopupExplainer then
        let resetButton = Graphics.makeButton(" Reset All Settings To Defaults... ",None,None)
        resetButton.HorizontalAlignment <- HorizontalAlignment.Left
        resetButton.Margin <- Thickness(0.,10.,0.,0.)
        resetButton.ToolTip <- "Delete your saved settings and restart the app, so that every option goes back to its default"
        resetButton.Click.Add(fun _ ->
            async {
                let wasActive = CustomComboBoxes.GlobalFlag.popupIsActive
                if not wasActive then CustomComboBoxes.GlobalFlag.popupIsActive <- true
                let resetText = "Reset and restart"
                let! r = CustomComboBoxes.DoModalMessageBoxCore(cm, System.Drawing.SystemIcons.Warning,
                                "This will delete your saved settings and restart the application,\nso every option goes back to its default.\nYour current tracker session will not be saved.",
                                [resetText; "Cancel"], 100., 300.)
                if not wasActive then CustomComboBoxes.GlobalFlag.popupIsActive <- false
                if r = resetText then
                    try System.IO.File.Delete(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Z1R_Tracker_settings.json")) with _ -> ()
                    Graphics.RestartTheApplication()
                } |> Async.StartImmediate
            )
        options2sp.Children.Add(resetButton) |> ignore
    // other settings
    let options2Grid = new Grid()
    for i = 1 to 3 do
        options2Grid.ColumnDefinitions.Add(new ColumnDefinition(Width=GridLength.Auto))
    for i = 0 to data2.Length do
        options2Grid.RowDefinitions.Add(new RowDefinition(Height=GridLength.Auto))
    let voiceTB = new TextBox(Text="Voice",IsReadOnly=true)
    voiceTB.ToolTip <- "The reminder will be spoken aloud"
    Graphics.gridAdd(options2Grid, voiceTB, 0, 0)
    let visualTB = new TextBox(Text="Visual",IsReadOnly=true)
    visualTB.ToolTip <- "The reminder will be displayed as icons in the upper right of the Timeline"
    Graphics.gridAdd(options2Grid, visualTB, 1, 0)
    let mutable row = 1
    for text,tip,bVoice,bVisual in data2 do
        if row%2=1 then
            let backgroundColor() = new DockPanel(Background=Graphics.almostBlack)
            Graphics.gridAdd(options2Grid, backgroundColor(), 0, row)
            Graphics.gridAdd(options2Grid, backgroundColor(), 1, row)
            Graphics.gridAdd(options2Grid, backgroundColor(), 2, row)
        let cbVoice = new CheckBox(HorizontalAlignment=HorizontalAlignment.Center)
        link(cbVoice, bVoice, false, fun()->())
        Graphics.gridAdd(options2Grid, cbVoice, 0, row)
        let cbVisual = new CheckBox(HorizontalAlignment=HorizontalAlignment.Center)
        link(cbVisual, bVisual, false, fun()->())
        Graphics.gridAdd(options2Grid, cbVisual, 1, row)
        let tb = new TextBox(Text=text,IsReadOnly=true, Background=Brushes.Transparent)
        tb.ToolTip <- tip
        ToolTipService.SetShowDuration(tb, 10000)
        Graphics.gridAdd(options2Grid, tb, 2, row)
        row <- row + 1

    options2sp.Children.Add(options2Grid) |> ignore
    // Reminder interval slider (label row then slider row for compact layout)
    let reminderIntervalLabel = new TextBox(Text=sprintf "Reminder Interval (Minutes): %d" TrackerModelOptions.ReminderIntervalMinutes, IsReadOnly=true, Margin=Thickness(0.,4.,0.,0.), Background=Brushes.Transparent)
    options2sp.Children.Add(reminderIntervalLabel) |> ignore
    let reminderIntervalSlider = new Slider(Minimum=1., Maximum=60., TickFrequency=1., IsSnapToTickEnabled=true, Value=float TrackerModelOptions.ReminderIntervalMinutes, Width=200., Margin=Thickness(0.,0.,0.,2.))
    reminderIntervalSlider.ToolTip <- "How often periodic reminders fire (recorder/power-bracelet spots, boomstick, white sword)"
    reminderIntervalSlider.ValueChanged.Add(fun _ ->
        let v = int reminderIntervalSlider.Value
        TrackerModelOptions.ReminderIntervalMinutes <- v
        reminderIntervalLabel.Text <- sprintf "Reminder Interval (Minutes): %d" v)
    options2sp.Children.Add(reminderIntervalSlider) |> ignore
    if voice.GetInstalledVoices() |> Seq.filter (fun v -> v.Enabled) |> Seq.length > 1 then
        let changeVoiceButton = Graphics.makeButton("Change Voice",None,None)
        changeVoiceButton.HorizontalAlignment <- HorizontalAlignment.Left
        do  // scope local mutable popupIsActive variable to this one button
            let mutable popupIsActive = false  // second level of popup, need local copy
            let mutable changedGlobal = false
            changeVoiceButton.Click.Add(fun _ ->
                if not popupIsActive then
                    popupIsActive <- true
                    // the options menu is not popped up on the start screen, but is in the main app, so we need to make this consistent for both
                    if not CustomComboBoxes.GlobalFlag.popupIsActive then
                        CustomComboBoxes.GlobalFlag.popupIsActive <- true  // must be start screen
                        changedGlobal <- true
                    let wh = new System.Threading.ManualResetEvent(false)
                    let sp = new StackPanel(Orientation=Orientation.Vertical)
                    AddStyle(sp)
                    sp.Children.Add(new TextBox(Text="Select preferred voice",IsReadOnly=true)) |> ignore
                    for v in voice.GetInstalledVoices() do
                        if v.Enabled then
                            let name = v.VoiceInfo.Name
                            let r = new StackPanel(Orientation=Orientation.Horizontal)
                            r.Children.Add(new TextBox(Text=name,IsReadOnly=true,Width=250.)) |> ignore
                            let testSpeech(f) =   // ensure we test aloud
                                let wasMuted = TrackerModelOptions.IsMuted
                                let wasVolumeOtherwiseZero = not wasMuted && TrackerModelOptions.Volume=0
                                if wasMuted || wasVolumeOtherwiseZero then
                                    voice.Volume <- 30  // default
                                f()
                                if wasVolumeOtherwiseZero || wasMuted then
                                    voice.Volume <- 0
                            let sb = Graphics.makeButton("Test it",None,None)
                            sb.Click.Add(fun _ -> testSpeech(fun() -> voice.SelectVoice(name); voice.Speak("Hello")))
                            r.Children.Add(sb) |> ignore
                            let sb = Graphics.makeButton("Choose this",None,None)
                            sb.Click.Add(fun _ -> testSpeech(fun() -> voice.SelectVoice(name); voice.Speak("Voice chosen"); TrackerModelOptions.PreferredVoice <- name; wh.Set() |> ignore))
                            r.Children.Add(sb) |> ignore
                            sp.Children.Add(r) |> ignore
                    async {
                        do! CustomComboBoxes.DoModalDocked(cm, wh, Dock.Bottom, new Border(Child=sp, BorderBrush=Brushes.Gray, BorderThickness=Thickness(3.), HorizontalAlignment=HorizontalAlignment.Center))
                        try
                            voice.SelectVoice(TrackerModelOptions.PreferredVoice)
                        with _ -> 
                            try
                                voice.SelectVoice(defaultVoice)
                            with _ -> ()
                        popupIsActive <- false
                        if changedGlobal then
                            CustomComboBoxes.GlobalFlag.popupIsActive <- false
                    } |> Async.StartImmediate
                )
        options2sp.Children.Add(changeVoiceButton) |> ignore
    // Auto-save: how often (1-10 minutes) the tracker writes its auto-save file
    let autoSaveHeader = new TextBox(Text="Auto-save", IsReadOnly=true, FontWeight=FontWeights.Bold, Margin=Thickness(0.,12.,0.,0.)) |> header
    options2sp.Children.Add(new DockPanel(Height=6.)) |> ignore   // extra space ABOVE the Auto-save heading (a spacer, so this and everything below it moves down 2px)
    options2sp.Children.Add(autoSaveHeader) |> ignore
    let autoSaveLabel = new TextBox(Text=sprintf "Auto-save Interval (Minutes): %d" TrackerModelOptions.AutoSaveMinutes, IsReadOnly=true, Margin=Thickness(0.,4.,0.,0.), Background=Brushes.Transparent)
    options2sp.Children.Add(autoSaveLabel) |> ignore
    let autoSaveSlider = new Slider(Minimum=1., Maximum=10., TickFrequency=1., IsSnapToTickEnabled=true, Value=float TrackerModelOptions.AutoSaveMinutes, Width=200., Margin=Thickness(0.,0.,0.,2.))
    autoSaveSlider.ToolTip <- "How often the tracker automatically saves your run (used to restore after a crash)"
    autoSaveSlider.ValueChanged.Add(fun _ ->
        let v = int autoSaveSlider.Value
        TrackerModelOptions.AutoSaveMinutes <- v
        autoSaveLabel.Text <- sprintf "Auto-save Interval (Minutes): %d" v)
    options2sp.Children.Add(autoSaveSlider) |> ignore

    let coopSettingsButton = Graphics.makeButton("Co-Op Client Settings", None, None)
    coopSettingsButton.HorizontalAlignment <- HorizontalAlignment.Left
    do
        let mutable popupIsActive = false
        let mutable changedGlobal = false
        coopSettingsButton.Click.Add(fun _ ->
            if not popupIsActive then
                popupIsActive <- true
                if not CustomComboBoxes.GlobalFlag.popupIsActive then
                    CustomComboBoxes.GlobalFlag.popupIsActive <- true
                    changedGlobal <- true

                let wh = new System.Threading.ManualResetEvent(false)
                let sp = new StackPanel(Orientation=Orientation.Vertical, Margin=Thickness(10.))
                AddStyle(sp)

                // NEW: Enable Co-op checkbox
                // NEW: Enable Co-op checkbox with label
                let enableCoopCheckbox = new CheckBox()
                // Initial validation of FunctionAppBase
                let isValidUrl (url: string) =
                    System.Uri.TryCreate(url, System.UriKind.Absolute) |> fst

                let initialUrl = TrackerModelOptions.CoopSyncOptions.FunctionAppBase
                let initialIsValid = isValidUrl initialUrl

                // Only enable checkbox if URL is valid
                enableCoopCheckbox.IsEnabled <- initialIsValid
                enableCoopCheckbox.IsChecked <- System.Nullable.op_Implicit(initialIsValid && TrackerModelOptions.CoopSyncOptions.GetEnableCoop())

                enableCoopCheckbox.HorizontalAlignment <- HorizontalAlignment.Left
                enableCoopCheckbox.IsEnabled <- false
                let enableCoopText = new TextBox(
                    Text = "Enable Co-op Sync", 
                    IsReadOnly = true, 
                    BorderThickness = Thickness(0.),
                    FontSize = 16., 
                    Foreground = Brushes.Orange,
                    Background = Brushes.Black,
                    IsHitTestVisible = false
                    
                )
                

                let coopCheckStack = new StackPanel(Orientation = Orientation.Horizontal)
                coopCheckStack.Children.Add(enableCoopCheckbox) |> ignore
                coopCheckStack.Children.Add(enableCoopText) |> ignore


                coopCheckStack.Margin <- Thickness(0., 0., 0., 10.)
                coopCheckStack.ToolTip <- "Enables co-op syncing between two instances of ZORATracker via Azure Functions and Azure SignalR.\nThis cannot be enabled if the value of Function App Base is not a valid URL"
                ToolTipService.SetShowDuration(coopCheckStack, 10000)
                enableCoopCheckbox.IsChecked <- System.Nullable.op_Implicit (TrackerModelOptions.CoopSyncOptions.GetEnableCoop())


                sp.Children.Add(coopCheckStack) |> ignore
                // Checkbox for Debug Mode
                let debugModeCheckbox = new CheckBox(Content=new TextBlock(Text="Enable Debug Logging", Foreground=Brushes.Orange))
                debugModeCheckbox.IsChecked <- System.Nullable.op_Implicit(TrackerModelOptions.DebugConfig.DebugMode)
                debugModeCheckbox.ToolTip <- "Enables debug logging for co-op sync operations.\nThis is useful for troubleshooting issues with co-op sync."
                ToolTipService.SetShowDuration(debugModeCheckbox, 10000)
                sp.Children.Add(debugModeCheckbox) |> ignore

                let urlLabel txt =
                    new TextBox(
                    Text = txt,
                    IsReadOnly = true,
                    BorderThickness = Thickness(0.),
                    FontSize = 16.,
                    Foreground = Brushes.Orange,
                    Background = Brushes.Black
                )

                sp.Children.Add(urlLabel("Function App Base:")) |> ignore

                let functionAppBaseBox = new TextBox(Text = TrackerModelOptions.CoopSyncOptions.FunctionAppBase)

                let functionAppBaseBorder = new Border(
                    Child = functionAppBaseBox,
                    BorderBrush = Brushes.Orange,
                    BorderThickness = Thickness(2.),
                    Margin = Thickness(0., 2., 0., 6.)
                )

                let functionAppBaseValidationMessage = 
                    new TextBlock(
                        Text = "Function App Base must be a valid URL",
                        Foreground = Brushes.Red,
                        Visibility = Visibility.Collapsed,
                        Margin = Thickness(0., 0., 0., 6.)
                    )

                let validateFunctionAppBaseUrl () =
                    let isValid = System.Uri.IsWellFormedUriString(functionAppBaseBox.Text, System.UriKind.Absolute)

                    // Toggle checkbox state
                    enableCoopCheckbox.IsEnabled <- isValid
                    enableCoopCheckbox.Foreground <- if isValid then Brushes.Orange else Brushes.DarkGray
                    if not isValid then
                        enableCoopCheckbox.IsChecked <- System.Nullable.op_Implicit false

                    // Border visual feedback
                    functionAppBaseBorder.BorderBrush <- if isValid then Brushes.Orange else Brushes.Red

                    // Error message visibility
                    functionAppBaseValidationMessage.Visibility <- if isValid then Visibility.Collapsed else Visibility.Visible

                // Initial validation on open
                validateFunctionAppBaseUrl()

                // Dynamic validation on text change
                functionAppBaseBox.TextChanged.Add(fun _ -> validateFunctionAppBaseUrl())


                functionAppBaseBox.TextChanged.Add(fun _ ->
                    let isValid = isValidUrl functionAppBaseBox.Text
                    if not isValid then
                        enableCoopCheckbox.IsChecked <- System.Nullable.op_Implicit false
                        enableCoopCheckbox.IsEnabled <- false
                        enableCoopCheckbox.Foreground <- Brushes.DarkGray
                    else
                        enableCoopCheckbox.IsEnabled <- true
                        enableCoopCheckbox.Foreground <- Brushes.Orange
                )

                functionAppBaseBox.HorizontalAlignment <- HorizontalAlignment.Stretch
                functionAppBaseBox.TextAlignment <- TextAlignment.Left
                
                functionAppBaseBorder.ToolTip <- "The base URL for the Azure Function App that handles co-op sync operations.\nThis must be a valid URL."
                ToolTipService.SetShowDuration(functionAppBaseBorder, 10000)
                sp.Children.Add(functionAppBaseBorder) |> ignore

                sp.Children.Add(functionAppBaseValidationMessage) |> ignore


                sp.Children.Add(urlLabel("Negotiate Suffix:")) |> ignore
                let negotiateUrlBox = new TextBox(Text = TrackerModelOptions.CoopSyncOptions.baseNegotiateUrl)
                negotiateUrlBox.HorizontalAlignment <- HorizontalAlignment.Stretch
                negotiateUrlBox.TextAlignment <- TextAlignment.Left
                let negotiateUrlBorder = new Border(
                    Child = negotiateUrlBox,
                    BorderBrush = Brushes.Orange,
                    BorderThickness = Thickness(2.),
                    Margin = Thickness(0., 2., 0., 6.)
                )
                negotiateUrlBorder.ToolTip <- "The suffix for the Azure Function App that handles co-op sync operations.\nThis will be appended to the Function App Base."
                ToolTipService.SetShowDuration(negotiateUrlBorder, 10000)
                sp.Children.Add(negotiateUrlBorder) |> ignore

                sp.Children.Add(urlLabel("SyncUpdate Suffix:")) |> ignore
                let syncUpdateUrlBox = new TextBox(Text = TrackerModelOptions.CoopSyncOptions.baseSyncUpdateUrl)
                syncUpdateUrlBox.HorizontalAlignment <- HorizontalAlignment.Stretch
                syncUpdateUrlBox.TextAlignment <- TextAlignment.Left
                let syncUpdateUrlBorder = new Border(
                    Child = syncUpdateUrlBox,
                    BorderBrush = Brushes.Orange,
                    BorderThickness = Thickness(2.),
                    Margin = Thickness(0., 2., 0., 6.)
                )
                syncUpdateUrlBorder.ToolTip <- "The suffix for the Azure Function App that handles outgoing co-op sync operations.\nThis will be appended to the Function App Base."
                ToolTipService.SetShowDuration(syncUpdateUrlBorder, 10000)
                sp.Children.Add(syncUpdateUrlBorder) |> ignore

                sp.Children.Add(new DockPanel(Height=10.)) |> ignore  // spacer before ID fields


                sp.Children.Add(new TextBox(Text="Console ID (This instance):", IsReadOnly=true)) |> ignore
                let myIdBox = new TextBox()
                myIdBox.Text <- TrackerModelOptions.CoopSyncOptions.MyConsoleId
                myIdBox.HorizontalAlignment <- HorizontalAlignment.Stretch
                myIdBox.TextAlignment <- TextAlignment.Center

                let myIdBorder = new Border(
                    Child = myIdBox,
                    BorderBrush = Brushes.Orange,
                    BorderThickness = Thickness(2.),
                    Margin = Thickness(0., 2., 0., 6.)
                )
                myIdBorder.ToolTip <- "The unique ID for this console instance.\nThis is used to identify this console in co-op sync operations."
                ToolTipService.SetShowDuration(myIdBorder, 10000)
                sp.Children.Add(myIdBorder) |> ignore

                let guidButton = Graphics.makeButton("Generate GUID", None, None)
                guidButton.Click.Add(fun _ ->
                    let newGuid = System.Guid.NewGuid().ToString()
                    myIdBox.Text <- newGuid
                )
                guidButton.ToolTip <- "Generate a random ID for this console instance.\nThis will be used as the unique ID for this console in co-op sync operations."
                ToolTipService.SetShowDuration(guidButton, 10000)
                sp.Children.Add(guidButton) |> ignore

                sp.Children.Add(new DockPanel(Height=10.)) |> ignore

                sp.Children.Add(new TextBox(Text="Target Console ID (to sync with):", IsReadOnly=true)) |> ignore
                let targetIdBox = new TextBox()
                targetIdBox.Text <- TrackerModelOptions.CoopSyncOptions.TargetConsoleId
                targetIdBox.HorizontalAlignment <- HorizontalAlignment.Stretch
                targetIdBox.TextAlignment <- TextAlignment.Center

                let targetIdBorder = new Border(
                    Child = targetIdBox,
                    BorderBrush = Brushes.Orange,
                    BorderThickness = Thickness(2.),
                    Margin = Thickness(0., 2., 0., 6.)
                )
                targetIdBorder.ToolTip <- "The unique ID for the target console instance.\nThis is used to identify the target console in co-op sync operations."
                sp.Children.Add(targetIdBorder) |> ignore

                sp.Children.Add(new DockPanel(Height=10.)) |> ignore

                let saveButton = Graphics.makeButton("Save", None, None)
                saveButton.Click.Add(fun _ ->
                    TrackerModelOptions.CoopSyncOptions.MyConsoleId <- myIdBox.Text
                    TrackerModelOptions.CoopSyncOptions.TargetConsoleId <- targetIdBox.Text
                    
                    TrackerModelOptions.CoopSyncOptions.FunctionAppBase <- functionAppBaseBox.Text
                    TrackerModelOptions.CoopSyncOptions.baseNegotiateUrl <- negotiateUrlBox.Text
                    TrackerModelOptions.CoopSyncOptions.baseSyncUpdateUrl <- syncUpdateUrlBox.Text

                    TrackerModelOptions.CoopSyncOptions.SetEnableCoop( 
                        match System.Nullable.op_Explicit(enableCoopCheckbox.IsChecked) with
                        | true -> true
                        | false -> false
                        )
                    TrackerModelOptions.DebugConfig.DebugMode <- 
                        match System.Nullable.op_Explicit(debugModeCheckbox.IsChecked) with
                        | true -> true
                        | false -> false

                    TrackerModelOptions.writeSettings()
                    wh.Set() |> ignore
                )
                saveButton.ToolTip <- "Save the current settings for co-op sync operations.\nThis will update the settings."
                sp.Children.Add(saveButton) |> ignore

                async {
                    do! CustomComboBoxes.DoModalDocked(cm, wh, Dock.Bottom,
                        new Border(
                            Child=sp,
                            BorderBrush=Brushes.Gray,
                            BorderThickness=Thickness(3.),
                            Background = Brushes.Black,
                            Width=400.,
                            HorizontalAlignment=HorizontalAlignment.Center
                        )
                    )
                    popupIsActive <- false
                    if changedGlobal then
                        CustomComboBoxes.GlobalFlag.popupIsActive <- false
                } |> Async.StartImmediate
        )
    // Co-op buttons moved to the "Other" column below




    optionsAllsp.Children.Add(new DockPanel(Width=2.,Background=Brushes.Gray)) |> ignore
    optionsAllsp.Children.Add(options2sp) |> ignore
    optionsAllsp.Children.Add(new DockPanel(Width=2.,Background=Brushes.Gray)) |> ignore

    let options3sp = new StackPanel(Orientation=Orientation.Vertical, Margin=Thickness(10.,2.,0.,0.))
    let tb = new TextBox(Text="Other", IsReadOnly=true, FontWeight=FontWeights.Bold) |> header
    options3sp.Children.Add(tb) |> ignore

    let cb = new CheckBox(Content=new TextBox(Text="Animate Tile Changes",IsReadOnly=true))
    cb.IsChecked <- System.Nullable.op_Implicit TrackerModelOptions.AnimateTileChanges.Value
    cb.Checked.Add(fun _ -> TrackerModelOptions.AnimateTileChanges.Value <- true)
    cb.Unchecked.Add(fun _ -> TrackerModelOptions.AnimateTileChanges.Value <- false)
    cb.ToolTip <- "When you change an overworld map spot or a dungeon room type, briefly animate the rectangle to highlight what changed"
    ToolTipService.SetShowDuration(cb, 10000)
    options3sp.Children.Add(cb) |> ignore

    let cb = new CheckBox(Content=new TextBox(Text="Animate Shop Highlights",IsReadOnly=true))
    cb.IsChecked <- System.Nullable.op_Implicit TrackerModelOptions.AnimateShopHighlights.Value
    cb.Checked.Add(fun _ -> TrackerModelOptions.AnimateShopHighlights.Value <- true)
    cb.Unchecked.Add(fun _ -> TrackerModelOptions.AnimateShopHighlights.Value <- false)
    cb.ToolTip <- "When you mouse hover certain icons to highlight corresponding shops on the map, briefly animate the rectangle"
    ToolTipService.SetShowDuration(cb, 10000)
    options3sp.Children.Add(cb) |> ignore

    let cb = new CheckBox(Content=new TextBox(Text="Remember Startup Selection",IsReadOnly=true))
    cb.IsChecked <- System.Nullable.op_Implicit TrackerModelOptions.RememberStartupSelection.Value
    cb.Checked.Add(fun _ -> TrackerModelOptions.RememberStartupSelection.Value <- true)
    cb.Unchecked.Add(fun _ -> TrackerModelOptions.RememberStartupSelection.Value <- false)
    cb.ToolTip <- "When checked, the boxes you check under Startup Options and ZORA Extras on the start screen are remembered\nand come back checked the next time the tracker starts"
    ToolTipService.SetShowDuration(cb, 10000)
    options3sp.Children.Add(cb) |> ignore

    let cb = new CheckBox(Content=new TextBox(Text="Save On Completion",IsReadOnly=true))
    cb.IsChecked <- System.Nullable.op_Implicit TrackerModelOptions.SaveOnCompletion.Value
    cb.Checked.Add(fun _ -> TrackerModelOptions.SaveOnCompletion.Value <- true)
    cb.Unchecked.Add(fun _ -> TrackerModelOptions.SaveOnCompletion.Value <- false)
    cb.ToolTip <- "When you click Zelda to complete the seed, automatically save the full tracker state to a file"
    ToolTipService.SetShowDuration(cb, 10000)
    options3sp.Children.Add(cb) |> ignore

    let cb = new CheckBox(Content=new TextBox(Text="Check For Updates On Startup",IsReadOnly=true))
    cb.IsChecked <- System.Nullable.op_Implicit TrackerModelOptions.CheckForUpdates.Value
    cb.Checked.Add(fun _ -> TrackerModelOptions.CheckForUpdates.Value <- true)
    cb.Unchecked.Add(fun _ -> TrackerModelOptions.CheckForUpdates.Value <- false)
    cb.ToolTip <- "When the tracker starts, look on GitHub for a newer release.\nIf there is one, the version button glows green; click it for the download link."
    ToolTipService.SetShowDuration(cb, 10000)
    options3sp.Children.Add(cb) |> ignore

    let cb = new CheckBox(Content=new TextBox(Text="Snoop For Seed&flags",IsReadOnly=true))
    cb.IsChecked <- System.Nullable.op_Implicit TrackerModelOptions.SnoopSeedAndFlags.Value
    cb.Checked.Add(fun _ -> TrackerModelOptions.SnoopSeedAndFlags.Value <- true; SaveAndLoad.MaybePollSeedAndFlags())
    cb.Unchecked.Add(fun _ -> TrackerModelOptions.SnoopSeedAndFlags.Value <- false)
    cb.ToolTip <- "Periodically check for other system windows (e.g. fceux)\nthat appear to have a seed and flag in the title, to\ninclude with save data and optionally display"
    ToolTipService.SetShowDuration(cb, 10000)
    options3sp.Children.Add(cb) |> ignore

    let cb = new CheckBox(Content=new TextBox(Text="Display Seed&flags",IsReadOnly=true), Margin=Thickness(20.,0.,0.,0.))
    cb.IsChecked <- System.Nullable.op_Implicit TrackerModelOptions.DisplaySeedAndFlags.Value
    cb.Checked.Add(fun _ -> TrackerModelOptions.DisplaySeedAndFlags.Value <- true; SaveAndLoad.seedAndFlagsUpdated.Trigger())
    cb.Unchecked.Add(fun _ -> TrackerModelOptions.DisplaySeedAndFlags.Value <- false; SaveAndLoad.seedAndFlagsUpdated.Trigger())
    cb.ToolTip <- "Display seed & flags (if known) in the bottom corner of Notes box"
    options3sp.Children.Add(cb) |> ignore

    let cb = new CheckBox(Content=new TextBox(Text="Listen For Speech",IsReadOnly=true))
    if microphoneFailedToInitialize then
        cb.IsEnabled <- false
        cb.IsChecked <- System.Nullable.op_Implicit false
        cb.ToolTip <- "Disabled (microphone was not initialized properly during startup)"
        ToolTipService.SetShowDuration(cb, 10000)
        ToolTipService.SetShowOnDisabled(cb, true)
    else
        cb.ToolTip <- "Use the microphone to listen for spoken map update commands\nExample: say 'tracker set bomb shop' while hovering an unmarked map tile"
        ToolTipService.SetShowDuration(cb, 10000)
        link(cb, TrackerModelOptions.ListenForSpeech, false, fun()->())
    options3sp.Children.Add(cb) |> ignore

    let cb = new CheckBox(Content=new TextBox(Text="Confirmation Sound",IsReadOnly=true), Margin=Thickness(20.,0.,0.,0.))
    if microphoneFailedToInitialize then
        cb.IsEnabled <- false
        cb.IsChecked <- System.Nullable.op_Implicit false
        cb.ToolTip <- "Disabled (microphone was not initialized properly during startup)"
        ToolTipService.SetShowDuration(cb, 10000)
        ToolTipService.SetShowOnDisabled(cb, true)
    else
        cb.ToolTip <- "Play a confirmation sound whenever speech recognition is used to make an update to the tracker"
        ToolTipService.SetShowDuration(cb, 10000)
        link(cb, TrackerModelOptions.PlaySoundWhenUseSpeech, false, fun()->())
    options3sp.Children.Add(cb) |> ignore

(*  // this is not (yet) a fully supported feature, so don't publish it on the options menu
    let cb = new CheckBox(Content=new TextBox(Text="Require PTT",IsReadOnly=true), Margin=Thickness(20.,0.,0.,0.))
    if microphoneFailedToInitialize then
        cb.IsEnabled <- false
        cb.IsChecked <- System.Nullable.op_Implicit false
        cb.ToolTip <- "Disabled (microphone was not initialized properly during startup)"
        ToolTipService.SetShowDuration(cb, 10000)
        ToolTipService.SetShowOnDisabled(cb, true)
    elif gamepadFailedToInitialize then
        cb.IsEnabled <- false
        cb.IsChecked <- System.Nullable.op_Implicit false
        cb.ToolTip <- "Disabled (gamepad was not initialized properly during startup)"
        ToolTipService.SetShowDuration(cb, 10000)
        ToolTipService.SetShowOnDisabled(cb, true)
    else
        link(cb, TrackerModelOptions.RequirePTTForSpeech, false)
        cb.ToolTip <- "Only listen for speech when Push-To-Talk button is held (SNES gamepad left shoulder button)"
        ToolTipService.SetShowDuration(cb, 10000)
    options3sp.Children.Add(cb) |> ignore
*)

    let cb = new CheckBox(Content=new TextBox(Text="Broadcast Window",IsReadOnly=true))
    cb.IsChecked <- System.Nullable.op_Implicit TrackerModelOptions.ShowBroadcastWindow.Value
    cb.Checked.Add(fun _ -> TrackerModelOptions.ShowBroadcastWindow.Value <- true; broadcastWindowOptionChanged.Trigger())
    cb.Unchecked.Add(fun _ -> TrackerModelOptions.ShowBroadcastWindow.Value <- false; broadcastWindowOptionChanged.Trigger())
    cb.ToolTip <- "Open a separate, smaller window, for stream capture.\nYou still interact with the original large window,\nbut the smaller window will focus the view on either the overworld or\nthe dungeon tabs, based on your mouse position."
    ToolTipService.SetShowDuration(cb, 10000)
    options3sp.Children.Add(cb) |> ignore


    let cb = new CheckBox(Content=new TextBox(Text="Include Overworld Magnifier",IsReadOnly=true), Margin=Thickness(20.,0.,0.,0.))
    cb.IsChecked <- System.Nullable.op_Implicit TrackerModelOptions.BroadcastWindowIncludesOverworldMagnifier.Value
    cb.Checked.Add(fun _ -> TrackerModelOptions.BroadcastWindowIncludesOverworldMagnifier.Value <- true; broadcastWindowOptionChanged.Trigger())
    cb.Unchecked.Add(fun _ -> TrackerModelOptions.BroadcastWindowIncludesOverworldMagnifier.Value <- false; broadcastWindowOptionChanged.Trigger())
    cb.ToolTip <- "Whether to include the overworld magnifier when it is on-screen, which will obscure some other elements"
    ToolTipService.SetShowDuration(cb, 10000)
    options3sp.Children.Add(cb) |> ignore

    let cb = new CheckBox(Content=new TextBox(Text="Mouse Magnifier Window",IsReadOnly=true))
    cb.IsChecked <- System.Nullable.op_Implicit TrackerModelOptions.ShowMouseMagnifierWindow.Value
    cb.Checked.Add(fun _ -> TrackerModelOptions.ShowMouseMagnifierWindow.Value <- true; mouseMagnifierWindowOptionChanged.Trigger())
    cb.Unchecked.Add(fun _ -> TrackerModelOptions.ShowMouseMagnifierWindow.Value <- false; mouseMagnifierWindowOptionChanged.Trigger())
    cb.ToolTip <- "Open a separate, resizable window that shows a\nzoomed-in area around the mouse.\nCan be useful when using the app on a small screen\nwhere precise mouse targetting is hard to see."
    ToolTipService.SetShowDuration(cb, 10000)
    options3sp.Children.Add(cb) |> ignore

    let cb = new CheckBox(Content=new TextBox(Text="Hide Timer",IsReadOnly=true))
    cb.IsChecked <- System.Nullable.op_Implicit TrackerModelOptions.HideTimer.Value
    cb.Checked.Add(fun _ -> TrackerModelOptions.HideTimer.Value <- true; hideTimerChanged.Trigger())
    cb.Unchecked.Add(fun _ -> TrackerModelOptions.HideTimer.Value <- false; hideTimerChanged.Trigger())
    cb.ToolTip <- "Don't display the bright green timer in the upper right corner."
    ToolTipService.SetShowDuration(cb, 10000)
    options3sp.Children.Add(cb) |> ignore

    let cb = new CheckBox(Content=new TextBox(Text="Race Mode",IsReadOnly=true))
    cb.IsChecked <- System.Nullable.op_Implicit TrackerModelOptions.RaceMode.Value
    cb.Checked.Add(fun _ -> TrackerModelOptions.RaceMode.Value <- true; raceModeChanged.Trigger())
    cb.Unchecked.Add(fun _ -> TrackerModelOptions.RaceMode.Value <- false; raceModeChanged.Trigger())
    cb.ToolTip <- "Disable many features that could be deemed an unfair advantage during a race."
    ToolTipService.SetShowDuration(cb, 10000)
    options3sp.Children.Add(cb) |> ignore

    // Co-op buttons (moved here from Reminders column to reduce its height)
    options3sp.Children.Add(new DockPanel(Height=6.)) |> ignore  // small spacer
    // the two co-op buttons are the same size (the widest label sets it), with a couple of pixels between them
    coopSettingsButton.Width <- 182.
    coopSettingsButton.Margin <- Thickness(0., 0., 0., 3.)
    options3sp.Children.Add(coopSettingsButton) |> ignore
    let coopHostSettingsButton = Graphics.makeButton("Co-Op Host Settings", None, None)
    coopHostSettingsButton.HorizontalAlignment <- HorizontalAlignment.Left
    coopHostSettingsButton.Width <- 182.
    coopHostSettingsButton.Click.Add(fun _ -> showCoopHostSettingsWindow(cm))
    options3sp.Children.Add(coopHostSettingsButton) |> ignore
    options3sp.Children.Add(crashLogButton) |> ignore
    do
        let tourButton = Graphics.makeButton(" Guided Tour ",None,None)
        tourButton.HorizontalAlignment <- HorizontalAlignment.Left
        tourButton.Margin <- Thickness(0.,6.,0.,0.)
        tourButton.ToolTip <- "Walk through each section of this screen and see what it does"
        if includePopupExplainer then
            tourButton.Click.Add(fun _ ->   // (closes this menu first, then the main window tour starts)
                optionsTourRequested <- true
                closeOptionsPopupHook ())
        else
            tourButton.Click.Add(fun _ ->
                runGuidedTourCore(cm, startScreenTourSteps, " Continue to main screen ", (fun () ->
                    mainTourAfterStart <- true
                    startBlankRunHook ())))
        options3sp.Children.Add(tourButton) |> ignore

    optionsAllsp.Children.Add(options3sp) |> ignore

    let total = new StackPanel(Orientation=Orientation.Vertical)
    if includePopupExplainer then
        let tb1 = new TextBox(Text="Options Menu", IsReadOnly=true, FontWeight=FontWeights.Bold, HorizontalAlignment=HorizontalAlignment.Center)
        total.Children.Add(tb1) |> ignore
        total.Children.Add(new DockPanel(Height=2.,Background=Brushes.Gray)) |> ignore

    // some spare black space at the bottom of every column (so the dividers between the columns run all the way down), for future options
    for col in [options1sp; options2sp; options3sp] do
        col.Children.Add(new DockPanel(Height=8., Background=Brushes.Black)) |> ignore
    total.Children.Add(optionsAllsp) |> ignore

    all.Child <- total
    
    raceModeChanged.Publish.Add(fun () ->
        if TrackerModelOptions.RaceMode.Value then
            // Mute audio, same as checking the box
            TrackerModelOptions.IsMuted <- true
            voice.Volume <- 0
            muteCB.IsChecked <- System.Nullable.op_Implicit true
        else
            // Restore checkbox to what user had previously selected
            TrackerModelOptions.IsMuted <- false
            voice.Volume <- TrackerModelOptions.Volume
            muteCB.IsChecked <- System.Nullable.op_Implicit false
    )






    all