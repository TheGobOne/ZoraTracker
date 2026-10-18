open System
open System.Windows
open System.Windows.Controls 
open System.Windows.Media
open Z1R_Sync
open SaveAndLoad
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open DungeonUI


open CustomComboBoxes.GlobalFlag

open System.Runtime.InteropServices 
module Winterop = 
    [<DllImport("User32.dll")>]
    extern bool RegisterHotKey(IntPtr hWnd,int id,uint32 fsModifiers,uint32 vk)
    [<DllImport("User32.dll")>]
    extern bool UnregisterHotKey(IntPtr hWnd,int id)
    let HOTKEY_ID = 9000

    [<DllImport("kernel32.dll")>]
    extern IntPtr GetConsoleWindow()
    [<DllImport("user32.dll")>]
    extern bool ShowWindow(IntPtr hWnd, int nCmdShow)
    let SW_HIDE = 0
    let SW_SHOWNORMAL = 1
    let SW_SHOW = 5
    let SW_MINIMIZE = 6

    (*
    [<ComImport>]
    [<Guid("00021401-0000-0000-C000-000000000046")>]
    internal type ShellLink

    let shellLink = 
        let ty = System.Type.GetTypeFromCLSID (System.Guid "00021401-0000-0000-C000-000000000046")
        Activator.CreateInstance ty
    *)

    [<ComImport>]
    [<InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
    [<Guid("000214F9-0000-0000-C000-000000000046")>]
    type IShellLink =
        abstract member GetPath : [<Out; MarshalAs(UnmanagedType.LPWStr)>] pszFile:System.Text.StringBuilder * cchMaxPath:int * [<Out>]pfd:IntPtr * fFlags:int -> unit
        abstract member GetIDList : [<Out>]ppidl:IntPtr -> unit
        abstract member SetIDList : pidl:IntPtr -> unit
        abstract member GetDescription : [<Out; MarshalAs(UnmanagedType.LPWStr)>] pszName:System.Text.StringBuilder * cchMaxName:int -> unit
        abstract member SetDescription : [<MarshalAs(UnmanagedType.LPWStr)>] pszName:string -> unit
        abstract member GetWorkingDirectory : [<Out; MarshalAs(UnmanagedType.LPWStr)>] pszDir:System.Text.StringBuilder * cchMaxPath:int -> unit
        abstract member SetWorkingDirectory : [<MarshalAs(UnmanagedType.LPWStr)>] pszDir:string -> unit
        abstract member GetArguments : [<Out; MarshalAs(UnmanagedType.LPWStr)>] pszArgs:System.Text.StringBuilder * cchMaxPath:int -> unit
        abstract member SetArguments : [<MarshalAs(UnmanagedType.LPWStr)>] pszArgs:string -> unit
        abstract member GetHotkey : [<Out>] pwHotkey:int16 -> unit
        abstract member SetHotkey : wHotkey:int16 -> unit 
        abstract member GetShowCmd : [<Out>] piShowCmd:int -> unit
        abstract member SetShowCmd : iShowCmd:int -> unit
        abstract member GetIconLocation : [<Out; MarshalAs(UnmanagedType.LPWStr)>] pszIconPath:System.Text.StringBuilder * cchIconPath:int * [<Out>]piIcon:int -> unit
        abstract member SetIconLocation : [<MarshalAs(UnmanagedType.LPWStr)>] pszIconPath:string * iIcon:int -> unit
        abstract member SetRelativePath : [<MarshalAs(UnmanagedType.LPWStr)>] pszPathRel:string * dwReserved:int -> unit
        abstract member Resolve : hwnd:IntPtr * fFlags:int -> unit
        abstract member SetPath : [<MarshalAs(UnmanagedType.LPWStr)>] pszFile:string -> unit

type MyWindowBase() as this = 
    inherit Window()
    let mutable source = null
    let VK_F10 = 0x79
#if DEBUG
#else
    let VK_F5 = 0x74
    let MOD_NONE = 0u
#endif
    let startTime = TrackerModel.theStartTime
    do
        // full window
        let timer = new System.Windows.Threading.DispatcherTimer()
        timer.Interval <- TimeSpan.FromSeconds(0.1)
        timer.Tick.Add(fun _ -> this.Update(false))
        timer.Start()
    member this.SetStartTimeToNow() = startTime.SetNow()
    member this.StartTime = startTime
    abstract member Update : bool -> unit
    default this.Update(_f10Press) = ()
    override this.OnSourceInitialized(e) =
        base.OnSourceInitialized(e)
        let helper = new System.Windows.Interop.WindowInteropHelper(this)
        source <- System.Windows.Interop.HwndSource.FromHwnd(helper.Handle)
        source.AddHook(System.Windows.Interop.HwndSourceHook(fun a b c d e -> this.HwndHook(a,b,c,d,&e)))
        this.RegisterHotKey()
    override this.OnClosed(e) =
        if source <> null then
            source.RemoveHook(System.Windows.Interop.HwndSourceHook(fun a b c d e -> this.HwndHook(a,b,c,d,&e)))
        source <- null
        this.UnregisterHotKey()
        base.OnClosed(e)
    member this.RegisterHotKey() =
#if DEBUG
        // in debug mode, do not register hotkeys, as I need e.g. F10 to work to use the debugger!
        ()
#else
        let helper = new System.Windows.Interop.WindowInteropHelper(this);
        if(not(Winterop.RegisterHotKey(helper.Handle, Winterop.HOTKEY_ID, MOD_NONE, uint32 VK_F10))) then
            // handle error
            ()
        if(not(Winterop.RegisterHotKey(helper.Handle, Winterop.HOTKEY_ID, MOD_NONE, uint32 VK_F5))) then
            // handle error
            ()
#endif
    member this.UnregisterHotKey() =
        let helper = new System.Windows.Interop.WindowInteropHelper(this)
        Winterop.UnregisterHotKey(helper.Handle, Winterop.HOTKEY_ID) |> ignore
    member this.HwndHook(_hwnd:IntPtr, msg:int, wParam:IntPtr, lParam:IntPtr, handled:byref<bool>) : IntPtr =
        let WM_HOTKEY = 0x0312
        if msg = WM_HOTKEY then
            if wParam.ToInt32() = Winterop.HOTKEY_ID then
                //let ctrl_bits = lParam.ToInt32() &&& 0xF  // see WM_HOTKEY docs
                let key = lParam.ToInt32() >>> 16
                if key = VK_F10 then
                    //startTime <- DateTime.Now
                    this.Update(true)
        IntPtr.Zero

let MakeTipTextBox(txt) =
    new TextBox(Text=txt, FontSize=14., IsReadOnly=true, TextAlignment=TextAlignment.Left, HorizontalAlignment=HorizontalAlignment.Left, TextWrapping=TextWrapping.Wrap, BorderThickness=Thickness(0.))

let ApplyKonamiCodeEasterEgg(cm:CustomComboBoxes.CanvasManager, fe:FrameworkElement) =
    let mutable i = 0
    let code = [|
        Input.Key.Up
        Input.Key.Up
        Input.Key.Down
        Input.Key.Down
        Input.Key.Left
        Input.Key.Right
        Input.Key.Left
        Input.Key.Right
        Input.Key.B
        Input.Key.A
        |]
    // Note: they need to click on an element inside the window (such as text of a tip) first to give focus to fe
    fe.PreviewKeyDown.Add(fun ea ->
        if i < code.Length && ea.Key = code.[i] then
            i <- i + 1
//            printfn "%A" ea.Key
            if i = code.Length then
                if not popupIsActive then
                    popupIsActive <- true
                    let wh = new System.Threading.ManualResetEvent(false)
                    let all = new DockPanel()
                    let tips = new StackPanel(Orientation=Orientation.Vertical, Margin=Thickness(3.))
                    let header(txt) =
                        let tb = MakeTipTextBox(txt)
                        tb.BorderThickness <- Thickness(0.,0.,0.,2.)
                        tb.BorderBrush <- Brushes.Orange
                        tb.FontSize <- tb.FontSize + 2.
                        tb.Margin <- Thickness(0., 15., 0., 10.)
                        tb
                    let mutable even = false
                    let body(txt) =
                        let tb = MakeTipTextBox(txt)
                        tb.Margin <- Thickness(0., 8., 0., 0.)
                        if even then
                            tb.Background <- Brushes.Black
                        else
                            tb.Background <- Graphics.almostBlack
                        even <- not even
                        tb
                    let desc = MakeTipTextBox("ALL OF TIPS")    // grammar mimics 'ALL OF ITEMS' in LoZ startup scroll
                    desc.FontSize <- 18.
                    desc.Margin <- Thickness(5.)
                    let b = new Border(BorderBrush=Brushes.Orange, BorderThickness=Thickness(0.,0.,0.,2.), Child=desc, HorizontalAlignment=HorizontalAlignment.Center, Margin=Thickness(0.,0.,0.,5.))
                    all.Children.Add(b) |> ignore
                    DockPanel.SetDock(b, Dock.Top)
                    let sv = new ScrollViewer(HorizontalAlignment=HorizontalAlignment.Left, VerticalAlignment=VerticalAlignment.Top, Content=tips)
                    all.Children.Add(sv) |> ignore
                    tips.Children.Add(header("Novice-level tips")) |> ignore
                    for t in DungeonData.Factoids.noviceTips do
                        tips.Children.Add(body(t)) |> ignore
                    tips.Children.Add(header("Intermediate-level tips")) |> ignore
                    for t in DungeonData.Factoids.intermediateTips do
                        tips.Children.Add(body(t)) |> ignore
                    tips.Children.Add(header("Advanced-level tips")) |> ignore
                    for t in DungeonData.Factoids.advancedTips do
                        tips.Children.Add(body(t)) |> ignore
                    tips.Children.Add(header("Z-Tracker tips")) |> ignore
                    for t in DungeonData.Factoids.zTrackerTips do
                        tips.Children.Add(body(t)) |> ignore
                    let BUFFER = 160.
                    let tipsBorder = new Border(Width=cm.AppMainCanvas.Width-BUFFER, Height=cm.AppMainCanvas.Height-BUFFER, Background=Brushes.Black, 
                                                BorderBrush=Brushes.Orange, BorderThickness=Thickness(3.), Child=all)
                    tipsBorder.Resources.Add(typeof<TextBox>, fe.Resources.[typeof<TextBox>])
                    async { 
                        do! CustomComboBoxes.DoModal(cm, wh, BUFFER/2., BUFFER/2., tipsBorder)
                        popupIsActive <- false
                    } |> Async.StartImmediate
        )





let mutable theDummyWindow : Window = null
let waitForConsole = new System.Threading.ManualResetEvent(true)

type MyWindow() as this = 
    inherit MyWindowBase()
    let mutable updateTimeline = fun _ -> ()
    let mutable lastUpdateMinute = 0
    let mutable lastUpdateSecond = 0
    //                             items  ow map  prog  dungeon tabs                                    timeline   
    let HEIGHT_SANS_CHROME = float(30*5 + 11*3*9 + 30 + OverworldItemGridUI.TH + 30 + 27*8 + 12*7 + 3 + OverworldItemGridUI.TCH + 6)
    let WIDTH_SANS_CHROME = float(16*16*3)  // ow map width
    let CHROME_WIDTH, CHROME_HEIGHT = 16., 39.  // Windows app border
    let HEIGHT = HEIGHT_SANS_CHROME + CHROME_HEIGHT
    let WIDTH = WIDTH_SANS_CHROME + CHROME_WIDTH
    let mutable loggedAnyCrash = false
    let mutable promptedCrashRecovery = false
    let mutable gotThruStartup = false
    let crashLogFilename = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Z1R_Tracker_crash_log.txt")
    let dateTimeFormat = "yyyy-MM-dd-HH:mm:ss"
    do
        let showCrashOnConsole() =
            waitForConsole.Reset() |> ignore
            async {
                let handle = Winterop.GetConsoleWindow()
                Winterop.ShowWindow(handle, Winterop.SW_SHOWNORMAL) |> ignore
                while Console.KeyAvailable do
                    Console.ReadKey(false) |> ignore   // clear keyboard buffer
                if gotThruStartup then
                    printfn ""
                    printfn "If Z-Tracker crashed while you were playing, you might be able to restore the most recent"
                    printfn "auto-save, by restarting Z-Tracker right now."
                printfn ""
                printfn "Press a key to close this window"
                System.Console.ReadKey() |> ignore
                waitForConsole.Set() |> ignore
            } |> Async.Start
        let logCrashInfo(s:string) =
            printfn "%s" s
            try
                if not loggedAnyCrash then
                    loggedAnyCrash <- true
                    System.IO.File.AppendAllText(crashLogFilename, sprintf "\n\nBEGIN CRASH LOG -- %s -- %s\n" OverworldData.ProgramNameString (DateTime.Now.ToString(dateTimeFormat)))
                System.IO.File.AppendAllText(crashLogFilename, sprintf "%s\n" s)
            with _ -> ()
        let finishCrashInfoImpl(extra) =
            let finalText = if gotThruStartup then extra else "(during startup)\n"  // this intentionally interacts with crash recovery dialog
            System.IO.File.AppendAllText(crashLogFilename, sprintf "END CRASH LOG\n%s\n%s" (DateTime.Now.ToString(dateTimeFormat)) finalText)
        let finishCrashInfo() = finishCrashInfoImpl("")
        let handle(ex:System.Exception) =
            match ex with
            | :? TrackerModelOptions.IntentionalApplicationShutdown as ias ->
                logCrashInfo <| sprintf "%s" ias.Message
                finishCrashInfo()
            | :? HotKeys.UserError as hue ->
                logCrashInfo ""
                logCrashInfo "Error parsing HotKeys.txt:"
                logCrashInfo ""
                logCrashInfo <| sprintf "%s" hue.Message
                logCrashInfo ""
                logCrashInfo "You should fix this error by editing the text file."
                logCrashInfo "Or you can delete it, and an empty hotkeys template file will be created in its place."
                logCrashInfo ""
                finishCrashInfo()
                let fileToSelect = HotKeys.HotKeyFilename
                let args = sprintf "/Select, \"%s\"" fileToSelect
                let psi = new System.Diagnostics.ProcessStartInfo("Explorer.exe", args)
                System.Diagnostics.Process.Start(psi) |> ignore
                printfn "Press a key to close this window"
                System.Console.ReadKey() |> ignore
            | _ ->
                logCrashInfo <| sprintf "%s" (ex.ToString())
                finishCrashInfo()
        System.Windows.Application.Current.DispatcherUnhandledException.Add(fun e -> 
            if System.Diagnostics.Debugger.IsAttached then
                System.Diagnostics.Debugger.Break()
            let ex = e.Exception
            logCrashInfo <| sprintf "An unhandled exception from UI thread:"
            handle(ex)
            e.Handled <- true
            finishCrashInfo()
            showCrashOnConsole()
            if theDummyWindow <> null then
                theDummyWindow.Close()
            )
        System.AppDomain.CurrentDomain.UnhandledException.Add(fun e -> 
            if System.Diagnostics.Debugger.IsAttached then
                System.Diagnostics.Debugger.Break()
            match e.ExceptionObject with
            | :? System.Exception as ex ->
                logCrashInfo <| sprintf "An unhandled exception from background thread:"
                handle(ex)
                finishCrashInfo()
            | _ ->
                logCrashInfo <| sprintf "An unhandled exception from background thread occurred."
                finishCrashInfo()
            showCrashOnConsole()
            )

        HotKeys.PopulateHotKeyTables()
        let mutable settingsWereSuccessfullyRead = false
        TrackerModelOptions.readSettings()
        settingsWereSuccessfullyRead <- true
        Z1R_SharedInterop.TrackerOptionsBridge.Initialize(
            (fun () -> TrackerModelOptions.BookForHelpfulHints.Value),
            (fun () -> TrackerModelOptions.DoDoorInference.Value)
        ) 

        //RPT try to initialize here for SyncManager
        SyncManager.Configure(TrackerModelOptions.CoopSyncOptions.SyncUpdateUrl())
        TrackerModelOptions.CoopSyncOptions.EnableCoopChanged.Publish.Add(fun isEnabled ->
            if isEnabled then
                printfn "[Sync] Co-op enabled at runtime — resetting SyncUrl"
                SyncManager.Configure(TrackerModelOptions.CoopSyncOptions.SyncUpdateUrl())
            else
                printfn "[Sync] Co-op disabled at runtime — not connecting"
               
        )
        // some global variables have already been initialized, and so they read the default value of settings, rather than the settings we just read; this bit patches that up:
        OptionsMenu.InitializeVoice()
        OptionsMenu.hideTimerChanged.Trigger()
        Graphics.volumeChanged.Trigger(TrackerModelOptions.Volume)

        do
            let shellLink = 
                let ty = System.Type.GetTypeFromCLSID (System.Guid "00021401-0000-0000-C000-000000000046")
                Activator.CreateInstance ty
            let isl = shellLink :?> Winterop.IShellLink
            let cwd = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName)
            isl.SetDescription("Launch Z-Tracker")
            isl.SetPath(System.IO.Path.Combine(cwd, Graphics.ExeName))
            isl.SetWorkingDirectory(cwd)
            isl.SetIconLocation(System.IO.Path.Combine(cwd, "icons/ztlogo64x64.ico"), 0)
            let ipf = isl :?> System.Runtime.InteropServices.ComTypes.IPersistFile
            ipf.Save(System.IO.Path.Combine(cwd, "ZTracker.lnk"), false)

        Graphics.theWindow <- this
        // full window
        this.Title <- "Z-Tracker for Zelda 1 Randomizer"
        this.ResizeMode <- ResizeMode.CanMinimize
        this.SizeToContent <- SizeToContent.Manual
        this.WindowStartupLocation <- WindowStartupLocation.Manual
        let APP_WIDTH, APP_HEIGHT = 
            if TrackerModelOptions.SmallerAppWindow.Value then 
                round(WIDTH_SANS_CHROME*TrackerModelOptions.SmallerAppWindowScaleFactor + CHROME_WIDTH), round(HEIGHT_SANS_CHROME*TrackerModelOptions.SmallerAppWindowScaleFactor + CHROME_HEIGHT)
            else 
                WIDTH, HEIGHT
        //printfn "%f, %f" APP_WIDTH APP_HEIGHT
        let leftTop = TrackerModelOptions.MainWindowLT
        let matches = System.Text.RegularExpressions.Regex.Match(leftTop, """^(-?\d+),(-?\d+)$""")
        if matches.Success then
            this.Left <- float matches.Groups.[1].Value
            this.Top <- float matches.Groups.[2].Value
        this.LocationChanged.Add(fun _ ->
            let left, top = this.Left, this.Top  // cache to local variable; I suspect there is a race where 2 subsequent reads yield different values during app shutdown
            if left < ShowRunCustom.MINIMIZED_THRESHOLD || top < ShowRunCustom.MINIMIZED_THRESHOLD then
                ()  // when you Minimize the window, -32000,-32000 are reported. Don't save that, as it's hard to recover window later
            else
                TrackerModelOptions.MainWindowLT <- sprintf "%d,%d" (int left) (int top)
                TrackerModelOptions.writeSettings()
            )
        this.Width <- APP_WIDTH
        this.Height <- APP_HEIGHT
        this.FontSize <- 18.
        this.Background <- Brushes.Black  // on a device with say 125% pixels and UseLayoutRounding=true, there's a white line at the bottom/right edge where RootCanvas doesn't cover app 

        let appMainCanvas, cm =  // a scope, so code below is less likely to touch rootCanvas
            let APP_CONTENT_HEIGHT = HEIGHT_SANS_CHROME
            let rootCanvas =    new Canvas(Width=16.*Graphics.OMTW, Height=APP_CONTENT_HEIGHT, Background=Brushes.Black)
            rootCanvas.UseLayoutRounding <- true
            let appMainCanvas = new Canvas(Width=16.*Graphics.OMTW, Height=APP_CONTENT_HEIGHT, Background=Brushes.Black)
            let style = new Style(typeof<ToolTip>)
            style.Setters.Add(new Setter(ToolTip.ForegroundProperty, Brushes.Orange))
            style.Setters.Add(new Setter(ToolTip.BackgroundProperty, Graphics.almostBlack))
            style.Setters.Add(new Setter(ToolTip.BorderBrushProperty, Brushes.DarkGray))
            rootCanvas.Resources.Add(typeof<ToolTip>, style)
            Graphics.canvasAdd(rootCanvas, appMainCanvas, 0., 0.)
            let cm = new CustomComboBoxes.CanvasManager(rootCanvas, appMainCanvas)
            appMainCanvas, cm
        // Stubbed SignalR sync startup

        let mutable lastOverworldSyncSource = ""
        let mutable lastOverworldSyncTime = System.DateTime.MinValue

        let shouldApplyUpdate (senderId: string) =
            let now = System.DateTime.UtcNow
            let elapsed = now - lastOverworldSyncTime
            if lastOverworldSyncSource = senderId && elapsed.TotalMilliseconds < 500.0 then
                false
            else
                lastOverworldSyncSource <- senderId
                lastOverworldSyncTime <- now
                true
        
        let mutable lastDungeonMapsJson = ""
        let _dungeonMapsDebouncer = Debouncer.Debouncer(500)

        let handleRemoteTileChange (tileId: string, iconId: string, senderId: string) =
            let me = TrackerModelOptions.CoopSyncOptions.MyConsoleId
            let target = TrackerModelOptions.CoopSyncOptions.TargetConsoleId
            if senderId <> me && senderId = target then
                try
                    //let tileInt = int tileId
                    //let iconInt = int iconId
                    //TileCustomization.assignIcon cm tileInt iconInt
                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Received tile change from %s: tileId=%s, iconId=%s" senderId tileId iconId)
                 with ex ->
                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Error applying tile change: %s" ex.Message)
                else
                    // Optional debug log for ignored messages
                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Ignored tile update from senderId=%s (target=%s, me=%s)" senderId target me)
        let handleIncomingMessage (msgType: string) (payloadJson: string) (senderId: string) (timestamp: int64) =
            let target = TrackerModelOptions.CoopSyncOptions.TargetConsoleId
            let me = TrackerModelOptions.CoopSyncOptions.MyConsoleId
            if not (TrackerModelOptions.CoopSyncOptions.GetEnableCoop()) then
                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Co-op disabled — ignoring incoming message: %s" msgType)
            else
                if senderId <> me && senderId = target then
                    if CoopSync.alreadyReceived msgType payloadJson then
                        TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Ignored duplicate message from %s" senderId)
                    else
                        match msgType with
                        | "PlayerProgress" ->
                            try
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Raw PlayerProgress payload: %s" payloadJson)
                                let data = JsonConvert.DeserializeObject<PlayerProgressAndTakeAnyHeartsModel>(payloadJson)
                                Application.Current.Dispatcher.Invoke(fun () ->
                                    CoopSync.isApplyingPlayerProgressFromSync <- true
                                    try
                                        data.Apply()
                                        TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Applied PlayerProgress update from %s" senderId)
                                    finally
                                        CoopSync.isApplyingPlayerProgressFromSync <- false
                                    TrackerModel.TimelineItemModel.TriggerTimelineChanged()
                                )
                            with ex ->
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to apply PlayerProgress update: %s" ex.Message)
                        | "Items" ->
                        
                            if not (CoopSync.shouldApplyUpdate "Items" payloadJson timestamp) then
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Skipping duplicate Items update from %s" senderId)
                            elif TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption.IsNone then
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Skipping Items update from %s — TrackerModel not initialized" senderId)
                            else
                                try
                                    let data = JsonConvert.DeserializeObject<SaveAndLoad.Items>(payloadJson)
                                    Application.Current.Dispatcher.Invoke(fun () ->
                                        CoopSync.isApplyingItemsFromSync <- true
                                        try
                                            // Top-level flags
                                            TrackerModel.IsHiddenDungeonNumbers <- fun () -> data.HiddenDungeonNumbers
                                            TrackerModel.IsSecondQuestDungeons <- data.SecondQuestDungeons

                                            // Top-level boxes — only apply if value has actually changed to avoid re-timestamping
                                            if data.WhiteSwordBox.CellCurrent <> TrackerModel.sword2Box.CellCurrent() || data.WhiteSwordBox.PlayerHas <> TrackerModel.sword2Box.PlayerHas().AsInt() then
                                                data.WhiteSwordBox.TryApply(TrackerModel.sword2Box) |> ignore
                                            if data.LadderBox.CellCurrent <> TrackerModel.ladderBox.CellCurrent() || data.LadderBox.PlayerHas <> TrackerModel.ladderBox.PlayerHas().AsInt() then
                                                data.LadderBox.TryApply(TrackerModel.ladderBox) |> ignore
                                            if data.ArmosBox.CellCurrent <> TrackerModel.armosBox.CellCurrent() || data.ArmosBox.PlayerHas <> TrackerModel.armosBox.PlayerHas().AsInt() then
                                                data.ArmosBox.TryApply(TrackerModel.armosBox) |> ignore

                                            // Dungeons
                                            data.Dungeons
                                            |> Array.iteri (fun i dungeon ->
                                                if dungeon <> null then
                                                    let targetDungeon = TrackerModel.GetDungeon(i)
                                                    let expectedLength = targetDungeon.Boxes.Length

                                                    dungeon.Triforce <- targetDungeon.PlayerHasTriforce()

                                                    if dungeon.Boxes = null || dungeon.Boxes.Length <> expectedLength then
                                                        let safeBoxes =
                                                            Array.init expectedLength (fun j ->
                                                                if dungeon.Boxes <> null && j < dungeon.Boxes.Length && dungeon.Boxes.[j] <> null then
                                                                    dungeon.Boxes.[j]
                                                                else
                                                                    new SaveAndLoad.Box()
                                                            )
                                                        dungeon.Boxes <- safeBoxes

                                                    // Apply all properties EXCEPT Triforce (sync separately)
                                                    targetDungeon.Color <- dungeon.Color
                                                    targetDungeon.LabelChar <- if dungeon.LabelChar.Length > 0 then dungeon.LabelChar.[0] else '?'
                                                    targetDungeon.PlayerHasMapOfThisDungeon <- dungeon.PlayerHasMap

                                                    (dungeon.Boxes, targetDungeon.Boxes)
                                                    ||> Array.iter2 (fun srcBox destBox ->
                                                        // Only apply if value has actually changed to avoid re-timestamping in the timeline
                                                        if srcBox <> null && (srcBox.CellCurrent <> destBox.CellCurrent() || srcBox.PlayerHas <> destBox.PlayerHas().AsInt()) then
                                                            srcBox.TryApply(destBox) |> ignore
                                                    )

                                                    // If any box has an item, mark this dungeon as seen so the summary tab shows content
                                                    if dungeon.Boxes <> null && dungeon.Boxes |> Array.exists (fun b -> b <> null && b.CellCurrent > -1) then
                                                        DungeonUI.isFirstTimeClickingAnyRoom.[i].Value <- false
                                            )

                                            TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Applied Items update from %s (Triforce excluded)" senderId)
                                        finally
                                            CoopSync.isApplyingItemsFromSync <- false
                                        // Force the timeline to redraw now that items have been applied
                                        TrackerModel.TimelineItemModel.TriggerTimelineChanged()
                                    )

                                with ex ->
                                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to apply Items update: %s" ex.Message)
                        | "StartingItems" ->
                            try
                                let data = JsonConvert.DeserializeObject<SaveAndLoad.StartingItemsAndExtrasModel>(payloadJson)
                                Application.Current.Dispatcher.Invoke(fun () ->
                                    for i = 0 to 7 do
                                        TrackerModel.startingItemsAndExtras.HDNStartingTriforcePieces.[i].Set(data.HDNStartingTriforcePieces.[i])
                                        TrackerModel.startingItemsAndExtras.PlayerHasWhiteSword.Set(data.PlayerHasWhiteSword)
                                        TrackerModel.startingItemsAndExtras.PlayerHasMagicalSword.Set(data.PlayerHasMagicalSword)
                                        TrackerModel.startingItemsAndExtras.PlayerHasSilverArrow.Set(data.PlayerHasSilverArrow)
                                        TrackerModel.startingItemsAndExtras.PlayerHasBow.Set(data.PlayerHasBow)
                                        TrackerModel.startingItemsAndExtras.PlayerHasWand.Set(data.PlayerHasWand)
                                        TrackerModel.startingItemsAndExtras.PlayerHasRedCandle.Set(data.PlayerHasRedCandle)
                                        TrackerModel.startingItemsAndExtras.PlayerHasBoomerang.Set(data.PlayerHasBoomerang)
                                        TrackerModel.startingItemsAndExtras.PlayerHasMagicBoomerang.Set(data.PlayerHasMagicBoomerang)
                                        TrackerModel.startingItemsAndExtras.PlayerHasRedRing.Set(data.PlayerHasRedRing)
                                        TrackerModel.startingItemsAndExtras.PlayerHasPowerBracelet.Set(data.PlayerHasPowerBracelet)
                                        TrackerModel.startingItemsAndExtras.PlayerHasLadder.Set(data.PlayerHasLadder)
                                        TrackerModel.startingItemsAndExtras.PlayerHasRaft.Set(data.PlayerHasRaft)
                                        TrackerModel.startingItemsAndExtras.PlayerHasRecorder.Set(data.PlayerHasRecorder)
                                        TrackerModel.startingItemsAndExtras.PlayerHasAnyKey.Set(data.PlayerHasAnyKey)
                                        TrackerModel.startingItemsAndExtras.PlayerHasBook.Set(data.PlayerHasBook)
                                        TrackerModel.startingItemsAndExtras.PlayerHasMeat.Set(data.PlayerHasMeat)
                                        TrackerModel.startingItemsAndExtras.MaxHeartsDifferential <- data.MaxHeartsDifferential

                                        TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Applied StartingItems update from %s" senderId)
                                )
                            with ex ->
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to apply StartingItems update: %s" ex.Message)

                        | "Overworld" ->
                            if isNull TrackerModel.overworldMapMarks then
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Skipping Overworld update — TrackerModel not initialized yet.")
                            elif not (shouldApplyUpdate senderId) then
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Ignored Overworld update from %s (too soon after last update)" senderId)
                            else
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Raw Overworld payload: %s" payloadJson)
                                // Apply the update safely here
                                try
                                    let data = JsonConvert.DeserializeObject<SaveAndLoad.Overworld>(payloadJson)
                                    Application.Current.Dispatcher.Invoke(fun () ->
                                        TrackerModel.MirrorOverworld <- data.MirrorOverworld
                                        TrackerModel.startIconX <- data.StartIconX
                                        TrackerModel.startIconY <- data.StartIconY
                                        TrackerModel.customWaypointX <- data.CustomWaypointX
                                        TrackerModel.customWaypointY <- data.CustomWaypointY

                                        if data.Map <> null && data.Map.Length = 384 then
                                            for j = 0 to 7 do
                                                for i = 0 to 15 do
                                                    let idx = (j * 16 + i) * 3
                                                    let cur = data.Map.[idx]
                                                    let ed  = data.Map.[idx + 1]
                                                    let circ = data.Map.[idx + 2]
                                                    TrackerModel.overworldMapMarks.[i,j].Set(cur)
                                                    if cur <> -1 then
                                                        let edKey = if TrackerModel.MapSquareChoiceDomainHelper.IsItem(cur) then TrackerModel.MapSquareChoiceDomainHelper.SHOP else cur
                                                        TrackerModel.setOverworldMapExtraData(i, j, edKey, ed)
                                                    TrackerModel.overworldMapCircles.[i,j] <- circ
                                        TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Applied Overworld update from %s" senderId)
                                    )
                                with ex ->
                                    printf "[Sync] Failed to apply Overworld update: %s" ex.Message
                        | "Hints" ->
                            try
                                let data = JsonConvert.DeserializeObject<SaveAndLoad.Hints>(payloadJson)
                                Application.Current.Dispatcher.Invoke(fun () ->
                                    for i = 0 to 10 do
                                        TrackerModel.SetLevelHint(i, TrackerModel.HintZone.FromIndex(data.LocationHints.[i]))
                                    TrackerModel.NoFeatOfStrengthHintWasGiven <- data.NoFeatOfStrengthHint
                                    TrackerModel.SailNotHintWasGiven <- data.SailNotHint
                                    TrackerModel.SailToHintWasGiven <- data.SailToHint
                                    TrackerModel.PlayMelodyHintWasGiven <- data.PlayMelodyHint
                                    TrackerModel.StepOverWaterHintWasGiven <- data.StepOverWaterHint
                                    TrackerModel.FireArrowsHintWasGiven <- data.FireArrowsHint

                                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Applied Hints update from %s" senderId)
                                )
                            with ex ->
                                printfn "[Sync] Failed to apply Hints update: %s" ex.Message
                        | "Blockers" ->
                            if TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption.IsNone then
                                TrackerModelOptions.DebugConfig.Log("[Sync][Blockers] Skipping update — tracker not initialized yet.")
                            else
                                try
                                    let data = JsonConvert.DeserializeObject<SaveAndLoad.Blocker[][]>(payloadJson)
                                    Application.Current.Dispatcher.Invoke(fun () ->
                                        TrackerModel.DungeonBlockersContainer.StartIgnoreChangesDuringLoad()
                                        for i = 0 to data.Length - 1 do
                                            for j = 0 to data.[i].Length - 1 do
                                                let b = data.[i].[j]
                                                if b <> null then
                                                    let blockerKind = TrackerModel.DungeonBlocker.FromHotKeyName(b.Kind)
                                                    TrackerModel.DungeonBlockersContainer.SetDungeonBlocker(i, j, blockerKind)
                                                    for k = 0 to b.AppliesTo.Length - 1 do
                                                        TrackerModel.DungeonBlockersContainer.SetDungeonBlockerAppliesTo(i, j, k, b.AppliesTo.[k])
                                                    let actualKind = TrackerModel.DungeonBlockersContainer.GetDungeonBlocker(i, j)
                                                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync][Blockers] Applied blocker [%d,%d]: %A" i j actualKind)
                                        TrackerModel.DungeonBlockersContainer.FinishIgnoreChangesDuringLoad()
                                        TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Applied Blockers update from %s" senderId)
                                    )
                                with ex ->
                                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to apply Blockers update: %s" ex.Message)

                        | "DungeonTriforce" ->
                            if not (CoopSync.shouldApplyUpdate "DungeonTriforce" payloadJson timestamp) then
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Skipped DungeonTriforce update — no change")
                            else
                                try
                                    let data = JsonConvert.DeserializeObject<CoopSync.DungeonsTriforceState>(payloadJson)
                                    Application.Current.Dispatcher.Invoke(fun () ->
                                        CoopSync.isApplyingTriforceFromSync <- true
                                        try
                                            for i = 0 to 8 do
                                                let dungeon = TrackerModel.GetDungeon(i)
                                                let has = data.Triforces.[i]
                                                if dungeon.PlayerHasTriforce() <> has then
                                                    dungeon.ToggleTriforce()
                                                if has then
                                                    DungeonUI.isFirstTimeClickingAnyRoom.[i].Value <- false
                                            TrackerModel.mapLastChangedTime.SetNow()
                                            TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Applied DungeonTriforce update from %s" senderId)
                                        finally
                                            CoopSync.isApplyingTriforceFromSync <- false
                                        TrackerModel.TimelineItemModel.TriggerTimelineChanged()
                                    )
                                with ex ->
                                    printfn "[Sync] Failed to apply DungeonTriforce update: %s" ex.Message
                        | "DungeonMaps" ->
                            if not (CoopSync.shouldApplyUpdate "DungeonMaps" payloadJson timestamp) then
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Skipped DungeonMaps update — no change")
                            elif TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption.IsNone then
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Skipped DungeonMaps update from %s — TrackerModel not initialized" senderId)
                            elif payloadJson = lastDungeonMapsJson then
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Skipped DungeonMaps update from %s — duplicate payload" senderId)
                            elif senderId = TrackerModelOptions.CoopSyncOptions.MyConsoleId then
                                TrackerModelOptions.DebugConfig.Log("[Sync] Skipped DungeonMaps update — echo from self")
                            else
                                lastDungeonMapsJson <- payloadJson
                                CoopSync.dungeonMapsSyncOrigin.MarkSyncedNow()
                                // Record that we've "sent" this payload so our own poller won't re-broadcast it
                                CoopSync.lastSentDungeonMapsPayload <- payloadJson
                                try
                                    let dungeonModels = JsonConvert.DeserializeObject<DungeonSaveAndLoad.DungeonModel[]>(payloadJson)
                                    Application.Current.Dispatcher.Invoke(fun () ->
                                        CoopSync.isApplyingRoomFromSync <- true
                                        CoopSync.dungeonMapsSyncOrigin.MarkSyncStart()
                                        try
                                            for level = 0 to 8 do
                                                let dm = dungeonModels.[level]
                                                // Skip if null or if RoomIsCircled/RoomStates are not fully populated (e.g. sender had uninitialized dungeon)
                                                let isValid =
                                                    not (System.Object.ReferenceEquals(dm, null)) &&
                                                    not (System.Object.ReferenceEquals(dm.RoomIsCircled, null)) &&
                                                    dm.RoomIsCircled.Length = 8 &&
                                                    not (System.Object.ReferenceEquals(dm.RoomStates, null)) &&
                                                    dm.RoomStates.Length = 8
                                                if isValid then
                                                    try
                                                        DungeonUI.importFunctions.[level](dm)
                                                    with ex ->
                                                        TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to apply DungeonMaps for level %d: %s" level ex.Message)
                                            // Update lastSentTime AFTER import so that SetNow() calls during import
                                            // don't produce a timestamp newer than lastSentTime (which would cause re-broadcast)
                                            CoopSync.lastSentTime <- DateTime.Now
                                            CoopSync.dungeonMapsSyncOrigin.MarkSyncedNow()
                                            let hash = CoopSync.computeHash payloadJson
                                            CoopSync.lastAppliedHashes.["DungeonMaps"] <- hash
                                            CoopSync.lastSentHashes.["DungeonMaps"] <- hash
                                        finally
                                            CoopSync.dungeonMapsSyncOrigin.MarkSyncEnd()
                                            CoopSync.isApplyingRoomFromSync <- false
                                    )
                                with ex ->
                                    printfn "[Sync] Failed to apply DungeonMaps update: %s" ex.Message
                        | "HiddenDungeonColorLabel" ->
                           try
                               if TrackerModel.IsHiddenDungeonNumbers()
                               && TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption.IsSome then
                                   let data = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(payloadJson)
                                   let i = data.["Index"].ToObject<int>()
                                   let color = data.["Color"].ToObject<int>()
                                   let labelChar = data.["LabelChar"].ToObject<string>()
                                   Application.Current.Dispatcher.Invoke(fun () ->
                                       let dungeon = TrackerModel.GetDungeon(i)
                                       dungeon.Color <- color
                                       dungeon.LabelChar <- if not (String.IsNullOrEmpty(labelChar)) then labelChar.[0] else '?'
                                       TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Applied HiddenDungeonColorLabel update to dungeon %d from %s" i senderId)
                                   )
                           with ex ->
                               TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to apply HiddenDungeonColorLabel update: %s" ex.Message)
                        | "DoorChange" ->
                            try
                                let data = JsonConvert.DeserializeObject<CoopSync.DoorChangePayload>(payloadJson)
                                let key = sprintf "DoorChange_%d_%d_%d_%b" data.Level data.X data.Y data.IsHorizontal
                                if TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption.IsSome && CoopSync.shouldApplyUpdate "DoorChange" payloadJson timestamp && not (SyncManager.ShouldSuppressDoorChange payloadJson) && (CoopSync.isDoorChangeFresh data.Level data.X data.Y data.IsHorizontal timestamp) && not(CoopSync.isRecentlySentByUs key 5000)then
                                    CoopSync.recordDoorTimestamp data.Level data.X data.Y data.IsHorizontal timestamp
                                    Application.Current.Dispatcher.Invoke(fun () ->
                                        try
                                            let newState = Dungeon.DoorInterop.fromString data.NewState.Case
                                            let door =
                                                if data.IsHorizontal then
                                                    DungeonUI.getHorizontalDoor data.Level data.X data.Y
                                                else
                                                    DungeonUI.getVerticalDoor data.Level data.X data.Y

                                            if door.State <> newState then
                                                CoopSync.isApplyingRoomFromSync <- true
                                                try
                                                    door.State <- newState
                                                finally
                                                    CoopSync.isApplyingRoomFromSync <- false
                                                TrackerModelOptions.DebugConfig.Log(
                                                    sprintf "[Sync] Applied DoorChange at L%d %s (%d,%d) -> %s"
                                                        data.Level
                                                        (if data.IsHorizontal then "H" else "V")
                                                        data.X
                                                        data.Y
                                                        data.NewState.Case
                                                )
                                        with ex2 ->
                                            TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] DoorChange internal error: %s" ex2.Message)
                                    )
                            with ex ->
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to apply DoorChange update: %s" ex.Message)

                        | "RoomChange" ->
                            try
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Raw RoomChange payload: %s" payloadJson)
                                let data = JsonConvert.DeserializeObject<CoopSync.RoomChangePayload>(payloadJson)
                                let key = sprintf "DungeonRoom_%d_%d_%d" data.Level data.X data.Y
                                if CoopSync.shouldApplyUpdate "RoomChange" payloadJson timestamp && (CoopSync.isRoomChangeFresh data.Level data.X data.Y timestamp) && not (CoopSync.isRecentlySentByUs key 5000) then
                                    CoopSync.recordRoomTimestamp data.Level data.X data.Y timestamp
                                    Application.Current.Dispatcher.Invoke(fun () ->
                                        CoopSync.isApplyingRoomFromSync <- true
                                        try
                                            Z1R_Tracker.Models.RoomSyncBridge.ApplyRoomChangeFromSync(
                                                data.Level,
                                                data.X,
                                                data.Y,
                                                data.IsComplete,
                                                data.RoomType,
                                                data.MonsterDetail,
                                                data.FloorDropDetail,
                                                data.FloorDropAppearsBright
                                            )

                                            match DungeonUI.redrawAllRoomsPublic with
                                            | Some f ->
                                                TrackerModelOptions.DebugConfig.Log("[Sync] Calling redrawAllRoomsPublic after room sync")
                                                f()
                                            | None ->
                                                TrackerModelOptions.DebugConfig.Log("[Sync] redrawAllRoomsPublic was None — cannot redraw")
                                        finally
                                            CoopSync.isApplyingRoomFromSync <- false
                                    )
                            with ex ->
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to apply RoomChange update: %s" ex.Message)
                        | "OverworldTile" ->
                            try
                                let data = JsonConvert.DeserializeObject<CoopSync.OverworldTileUpdate>(payloadJson)
                                if CoopSync.shouldApplyUpdate "OverworldTile" payloadJson timestamp then
                                    Application.Current.Dispatcher.Invoke(fun () ->
                                        TrackerModel.overworldMapMarks.[data.X, data.Y].Set(data.MapTileValue)
                                        if data.MapTileValue <> -1 then
                                            // Shop tiles store extra data under SHOP key; use the same key the sender used
                                            let edKey = if TrackerModel.MapSquareChoiceDomainHelper.IsItem(data.MapTileValue) then TrackerModel.MapSquareChoiceDomainHelper.SHOP else data.MapTileValue
                                            TrackerModel.setOverworldMapExtraData(data.X, data.Y, edKey, data.ExtraData)
                                        TrackerModel.overworldMapCircles.[data.X, data.Y] <- data.CircleValue
                                        TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Applied OverworldTile (%d,%d) update from %s" data.X data.Y senderId)
                                    )
                            with ex ->
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to apply OverworldTile update: %s" ex.Message)

                        | _ ->
                            TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Unknown messageType: %s" msgType)
                else
                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Ignored sync message from senderId=%s (target=%s, me=%s)" senderId target me)

        //let negotiateUrl = (TrackerModelOptions.CoopSyncOptions.NegotiateUrl())
        // THIS is the correct way to run async without causing the Async<'T> comparison error
        let tryStartSignalRConnection() =
            Async.StartImmediate(async {
                try
                    do! SyncManager.StartAsync(TrackerModelOptions.CoopSyncOptions.NegotiateUrl()) |> Async.AwaitTask
                    //CoopSync.subscribeToPlayerProgressChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
                    //CoopSync.subscribeToStartingItemsAndExtrasChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
                    //CoopSync.subscribeToItemsChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId)
                    // Tile-specific handler
                    let tileHandlerDelegate = System.Action<string, string, string>(fun tileId iconId senderId ->
                        handleRemoteTileChange(tileId, iconId, senderId)
                    )
                    SyncManager.SetTileChangeHandler(tileHandlerDelegate)

                    // General message handler (e.g. PlayerProgress)
                    let messageHandlerDelegate = System.Action<string, string, string, int64>(fun messageType payloadJson senderId timestamp ->
                        handleIncomingMessage messageType payloadJson senderId timestamp
                    )
                    SyncManager.SetSyncMessageHandler(messageHandlerDelegate)


                    printfn "[Sync] Connected to Azure SignalR via negotiate"
                with ex ->
                    printfn "[Sync] ERROR: %s" ex.Message
                    match ex with
                    | :? System.AggregateException as agg ->
                        for inner in agg.InnerExceptions do
                        printfn "[Sync] Inner exception: %s" inner.Message
                    | _ -> ()
            })
        if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop()) then
            tryStartSignalRConnection()
        else
            printfn "[Sync] Co-op disabled — not starting SignalR connection"
        TrackerModelOptions.CoopSyncOptions.EnableCoopChanged.Publish.Add(fun isEnabled ->
            if isEnabled then
                printfn "[Sync] Co-op enabled at runtime — connecting to Azure SignalR"
                tryStartSignalRConnection()
            else
                printfn "[Sync] Co-op disabled at runtime — not connecting"
                // Optional: implement disconnect logic here
        )


        let wholeCanvas, hmsTimerCanvas = new Canvas(), new Canvas()
        this.Content <- wholeCanvas
        this.Loaded.Add(fun _ -> 
            Graphics.setup(this, wholeCanvas)
            this.Focus() |> ignore)
        let drawingCanvasHolder = new Canvas()  // gets the app RenderTransform (can't RenderTransform the drawingCanvas itself without screwing up Broadcast Window)
        let drawingCanvas = new Canvas(IsHitTestVisible=false)
        drawingCanvasHolder.Children.Add(drawingCanvas) |> ignore
        if TrackerModelOptions.SmallerAppWindow.Value then 
            let trans = new ScaleTransform(TrackerModelOptions.SmallerAppWindowScaleFactor, TrackerModelOptions.SmallerAppWindowScaleFactor)
            if trans.CanFreeze then
                trans.Freeze()
            wholeCanvas.RenderTransform <- trans
        wholeCanvas.Children.Add(cm.RootCanvas) |> ignore
        wholeCanvas.Children.Add(hmsTimerCanvas) |> ignore
        wholeCanvas.Children.Add(drawingCanvasHolder) |> ignore
        cm.AfterCreatePopupCanvas.Add(fun _ -> drawingCanvas.Opacity <- 0.)
        cm.BeforeDismissPopupCanvas.Add(fun _ -> if cm.PopupCanvasStack.Count=0 then drawingCanvas.Opacity <- 1.)
        let mainDock = new DockPanel(Width=appMainCanvas.Width, Height=appMainCanvas.Height)
        ApplyKonamiCodeEasterEgg(cm, mainDock)
        appMainCanvas.Children.Add(mainDock) |> ignore

        let addDarkTheme(rd:ResourceDictionary) = 
            let style = new Style(typeof<TextBox>)
            style.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.Orange))
            style.Setters.Add(new Setter(TextBox.BackgroundProperty, Brushes.Black))
            style.Setters.Add(new Setter(TextBox.BorderBrushProperty, Brushes.Orange))
            rd.Add(typeof<TextBox>, style)
            let style = new Style(typeof<Button>)
            style.Setters.Add(new Setter(Button.BorderBrushProperty, Brushes.Orange))
            style.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.DarkGray))
            rd.Add(typeof<Button>, style)
            let style = new Style(typeof<ToolTip>)
            style.Setters.Add(new Setter(ToolTip.ForegroundProperty, Brushes.Orange))
            style.Setters.Add(new Setter(ToolTip.BackgroundProperty, Graphics.almostBlack))
            style.Setters.Add(new Setter(ToolTip.BorderBrushProperty, Brushes.DarkGray))
            rd.Add(typeof<ToolTip>, style)

        let mainStackPanel = new StackPanel(Orientation=Orientation.Vertical)
        let spacing = Thickness(0., 8., 0., 0.)
        let smallSpacing = Thickness(0., 3., 0., 0.)

        let mutable startButtonHasBeenClicked = false
        do        
            let menu(wh:Threading.ManualResetEvent) = 
                let mkTxt(txt) = new TextBox(Text=txt,IsReadOnly=true, Margin=spacing, //TextAlignment=TextAlignment.Center, HorizontalAlignment=HorizontalAlignment.Center, 
                                                BorderThickness=Thickness(0.), FontSize=16.)
                let sp = new StackPanel(Orientation=Orientation.Vertical, Width=appMainCanvas.Width-60., Margin=Thickness(10.))
                let title = new TextBox(Text="Window Size",IsReadOnly=true, TextAlignment=TextAlignment.Center, HorizontalAlignment=HorizontalAlignment.Center, 
                                            BorderThickness=Thickness(0.,0.,0.,3.), FontSize=20.)
                sp.Children.Add(title) |> ignore
                sp.Children.Add(mkTxt("Z-Tracker can run in a few window sizes: '4/3 size', 'Default', '5/6 size', or '2/3 size'.")) |> ignore
                let MODE8,MODE6,MODE5,MODE4 = "4/3 size (Largest)", "Default", "5/6 size", "2/3 size (Smallest)"
                let curMode = 
                    if TrackerModelOptions.SmallerAppWindow.Value then 
                        if TrackerModelOptions.SmallerAppWindowScaleFactor = 2.0/3.0 then 
                            MODE4
                        elif TrackerModelOptions.SmallerAppWindowScaleFactor = 5.0/6.0 then 
                            MODE5
                        elif TrackerModelOptions.SmallerAppWindowScaleFactor = 4.0/3.0 then 
                            MODE8
                        else
                            sprintf "scale factor: %f" TrackerModelOptions.SmallerAppWindowScaleFactor
                    else 
                        MODE6
                sp.Children.Add(mkTxt(sprintf "The current Z-Tracker window size is '%s'.  You can change the setting here:" curMode)) |> ignore
                sp.Children.Add(new DockPanel(Height=20.)) |> ignore
                let rb8 = new RadioButton(Content=new TextBox(Text=MODE8,IsReadOnly=true,IsHitTestVisible=false,BorderThickness=Thickness(0.),FontSize=16.),Margin=Thickness(20.,0.,0.,0.))
                let rb6 = new RadioButton(Content=new TextBox(Text=MODE6,IsReadOnly=true,IsHitTestVisible=false,BorderThickness=Thickness(0.),FontSize=16.),Margin=Thickness(20.,0.,0.,0.))
                let rb5 = new RadioButton(Content=new TextBox(Text=MODE5,IsReadOnly=true,IsHitTestVisible=false,BorderThickness=Thickness(0.),FontSize=16.),Margin=Thickness(20.,0.,0.,0.))
                let rb4 = new RadioButton(Content=new TextBox(Text=MODE4,IsReadOnly=true,IsHitTestVisible=false,BorderThickness=Thickness(0.),FontSize=16.),Margin=Thickness(20.,0.,0.,0.))
                if TrackerModelOptions.SmallerAppWindow.Value then
                    if curMode=MODE5 then
                        rb5.IsChecked <- System.Nullable.op_Implicit true
                    elif curMode=MODE8 then
                        rb8.IsChecked <- System.Nullable.op_Implicit true
                    else
                        rb4.IsChecked <- System.Nullable.op_Implicit true
                else
                    rb6.IsChecked <- System.Nullable.op_Implicit true
                let mutable desireSmaller = TrackerModelOptions.SmallerAppWindow.Value
                let mutable desireScale = 2.0/3.0
                rb8.Checked.Add(fun _ -> desireSmaller <- true; desireScale <- 4.0/3.0)
                rb6.Checked.Add(fun _ -> desireSmaller <- false)
                rb5.Checked.Add(fun _ -> desireSmaller <- true; desireScale <- 5.0/6.0)
                rb4.Checked.Add(fun _ -> desireSmaller <- true; desireScale <- 2.0/3.0)
                let inner = new StackPanel(Orientation=Orientation.Vertical, HorizontalAlignment=HorizontalAlignment.Center)
                inner.Children.Add(rb8) |> ignore
                inner.Children.Add(rb6) |> ignore
                inner.Children.Add(rb5) |> ignore
                inner.Children.Add(rb4) |> ignore
                sp.Children.Add(inner) |> ignore
                
                let title = new TextBox(Text="Window Shape",IsReadOnly=true, TextAlignment=TextAlignment.Center, HorizontalAlignment=HorizontalAlignment.Center, 
                                            BorderThickness=Thickness(0.,0.,0.,3.), FontSize=20., Margin=spacing)
                sp.Children.Add(title) |> ignore
                sp.Children.Add(mkTxt("Z-Tracker can run in two window shapes: 'Tall' (default) and 'Square' (smaller/shorter).")) |> ignore
                sp.Children.Add(mkTxt("The 'Square' option auto-swaps between Overworld and Dungeon focus, based on your mouse.")) |> ignore
                sp.Children.Add(mkTxt("Note: This option only affects the main application; the startup options screen is always 'Tall'.")) |> ignore
                sp.Children.Add(mkTxt("Note: 'Square' disables some features: overworld magnifier, broadcast window, Draw&UCC buttons.")) |> ignore
                let mutable desireShort = TrackerModelOptions.ShorterAppWindow.Value
                let tallC = new Canvas(Width=77., Height=99., Background=Brushes.Red)
                let squareC = new Canvas(Width=77., Height=77., Background=Brushes.Red)
                let tx(s) = new TextBox(Text=s,IsReadOnly=true,FontSize=16.,BorderThickness=Thickness(0.),Foreground=Brushes.Black,Background=Brushes.Transparent,FontWeight=FontWeights.Bold,
                                        Width=77., TextAlignment=TextAlignment.Center, IsHitTestVisible=false)
                Graphics.canvasAdd(tallC, tx("Tall"), 0., 30.)
                Graphics.canvasAdd(squareC, tx("Square"), 0., 30.)
                let tallB = new Border(Child=tallC, BorderThickness=Thickness(3.))
                let squareB = new Border(Child=squareC, BorderThickness=Thickness(3.))
                let update() =
                    if desireShort then
                        tallB.BorderBrush <- Brushes.Black
                        squareB.BorderBrush <- Brushes.Pink
                    else
                        tallB.BorderBrush <- Brushes.Pink
                        squareB.BorderBrush <- Brushes.Black
                update()
                let rbTall   = new RadioButton(Content=tallB,   VerticalContentAlignment=VerticalAlignment.Center, IsChecked=System.Nullable.op_Implicit (not desireShort))
                let rbSquare = new RadioButton(Content=squareB, VerticalContentAlignment=VerticalAlignment.Center, IsChecked=System.Nullable.op_Implicit desireShort)
                let inner = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center, Margin=spacing)
                rbTall.Checked.Add(fun _ -> desireShort <- false; update())
                rbSquare.Checked.Add(fun _ -> desireShort <- true; update())
                inner.Children.Add(rbTall) |> ignore
                inner.Children.Add(new DockPanel(Width=30.)) |> ignore  // spacer
                inner.Children.Add(rbSquare) |> ignore
                sp.Children.Add(inner) |> ignore
                
                sp.Children.Add(new DockPanel(Background=Brushes.Gray, Margin=Thickness(20., 8., 20., 0.), Height=4.)) |> ignore
                let warn = mkTxt("Changes to these settings will only take effect after restarting the app:")
                warn.FontSize <- 20.
                warn.FontWeight <- FontWeights.Bold
                sp.Children.Add(warn) |> ignore
                let buttons = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, Margin=spacing)
                let makeButton(head, text) =
                    let sp = new StackPanel(Background=Graphics.almostBlack, Orientation=Orientation.Vertical)
                    let tb1 = new TextBox(Text=head,IsReadOnly=true,IsHitTestVisible=false,TextAlignment=TextAlignment.Center,BorderThickness=Thickness(0.),
                                            Background=Graphics.almostBlack,FontSize=24.,FontWeight=FontWeights.Bold)
                    let tb2 = new TextBox(Text=text,IsReadOnly=true,IsHitTestVisible=false,TextAlignment=TextAlignment.Center,BorderThickness=Thickness(0.),
                                            Background=Graphics.almostBlack,FontSize=16.)
                    sp.Children.Add(tb1) |> ignore
                    sp.Children.Add(tb2) |> ignore
                    let button = new Button(Content=sp, HorizontalContentAlignment=HorizontalAlignment.Stretch, VerticalContentAlignment=VerticalAlignment.Stretch,BorderThickness=Thickness(3.,3.,3.,3.))
                    button
                let sb = makeButton("Save changes","Save changes and restart Z-Tracker")
                let cb = makeButton("Discard changes","Don't make any changes\n(exit this popup menu)")
                cb.Click.Add(fun _ -> wh.Set() |> ignore)
                sb.Click.Add(fun _ ->
                    TrackerModelOptions.SmallerAppWindow.Value <- desireSmaller
                    TrackerModelOptions.SmallerAppWindowScaleFactor <- desireScale
                    TrackerModelOptions.ShorterAppWindow.Value <- desireShort
                    TrackerModelOptions.writeSettings()
                    Graphics.RestartTheApplication()
                    )
                buttons.Children.Add(sb) |> ignore
                buttons.Children.Add(new DockPanel(Width=30.)) |> ignore  // spacing
                buttons.Children.Add(cb) |> ignore
                sp.Children.Add(buttons) |> ignore
                addDarkTheme(sp.Resources)
                new Border(BorderBrush=Brushes.Gray, BorderThickness=Thickness(3.), Background=Brushes.Black, Child=sp)

            let fs = if TrackerModelOptions.SmallerAppWindow.Value then 16. else 12.
            let topBar = new StackPanel(Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center)
            let tb = new TextBox(Text="Z-Tracker window too large/small?", Margin=Thickness(0.,0.,10.,0.),
                                    IsReadOnly=true, BorderThickness=Thickness(0.), FontSize=fs, VerticalAlignment=VerticalAlignment.Center)
            topBar.Children.Add(tb) |> ignore
            let b = Graphics.makeButton("Click here for options", Some(fs), None)
            b.Click.Add(fun _ -> 
                if startButtonHasBeenClicked then () else
                if not popupIsActive then
                    popupIsActive <- true
                    let wh = new Threading.ManualResetEvent(false)
                    CustomComboBoxes.DoModal(cm, wh, 20., 60., menu(wh)) |> Async.StartImmediate
                    popupIsActive <- false
                )
            topBar.Children.Add(b) |> ignore
            let curFactor = if TrackerModelOptions.SmallerAppWindow.Value then TrackerModelOptions.SmallerAppWindowScaleFactor else 1.0
            let workingAreaTooSmallForCurrentHeight = SystemParameters.WorkArea.Height < (1000.0 * curFactor)
            let likelyDoesntFit = workingAreaTooSmallForCurrentHeight  // 'smaller' now might mean larger    && not(TrackerModelOptions.SmallerAppWindow.Value)
            let barColor = 
                if likelyDoesntFit then 
                    Graphics.freeze(new SolidColorBrush(Color.FromRgb(120uy, 30uy, 30uy)))   // reddish
                else 
                    Graphics.freeze(new SolidColorBrush(Color.FromRgb(50uy, 50uy, 50uy)))    // grayish
            let dp = new DockPanel(Height=(if curFactor<1.0 then 40. else 30.), LastChildFill=true, Background=barColor)
            dp.Children.Add(topBar) |> ignore
            mainStackPanel.Children.Add(dp) |> ignore

        let stackPanel = new StackPanel(Orientation=Orientation.Vertical, Width=WIDTH_SANS_CHROME)
        let startupOptionsCanvas = new Canvas()
        startupOptionsCanvas.Children.Add(stackPanel) |> ignore
        mainStackPanel.Children.Add(startupOptionsCanvas) |> ignore
        do  // version button, drawn atop without affecting layout
            let dp = new DockPanel(Width=WIDTH_SANS_CHROME, Height=60., LastChildFill=false)
            let vb = Graphics.dock(CustomComboBoxes.makeVersionButtonWithBehavior(cm), Dock.Top)
            vb.LayoutTransform <- new ScaleTransform(2., 2.)   // version button is twice as big on the start screen
            DockPanel.SetDock(vb, Dock.Right)
            dp.Children.Add(vb) |> ignore
            startupOptionsCanvas.Children.Add(dp) |> ignore

        let tb = new TextBox(Text="Startup Options:",IsReadOnly=true, TextAlignment=TextAlignment.Center, HorizontalAlignment=HorizontalAlignment.Center, BorderThickness=Thickness(0.), Margin=Thickness(0.,16.,0.,0.))   // centered in the gap between the top bar and the checkbox row
        stackPanel.Children.Add(tb) |> ignore

        let hsPanel = new StackPanel(Margin=spacing, Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Center)
        let hsGrid = Graphics.makeGrid(3, 3, 30, 30)
        hsGrid.Background <- Brushes.Black
        let triforcesNumbered = ResizeArray()
        let triforcesLettered = ResizeArray()
        for i = 0 to 2 do
            let image = Graphics.BMPtoImage Graphics.emptyUnfoundNumberedTriforce_bmps.[i]
            triforcesNumbered.Add(image)
            Graphics.gridAdd(hsGrid, image, i, 0)
            let image = Graphics.BMPtoImage Graphics.emptyUnfoundLetteredTriforce_bmps.[i]
            triforcesLettered.Add(image)
            Graphics.gridAdd(hsGrid, image, i, 0)
        let turnHideDungeonNumbersOn() =
            for b in triforcesNumbered do
                b.Opacity <- 0.
            for b in triforcesLettered do
                b.Opacity <- 1.
        let turnHideDungeonNumbersOff() =
            for b in triforcesNumbered do
                b.Opacity <- 1.
            for b in triforcesLettered do
                b.Opacity <- 0.
        turnHideDungeonNumbersOff()
        let row1boxes = Array.init 3 (fun _ -> new TrackerModel.Box(TrackerModel.StairKind.Never, TrackerModel.BoxOwner.None))
        for i = 0 to 2 do
            Graphics.gridAdd(hsGrid, fst(Views.MakeBoxItem(cm, row1boxes.[i])), i, 1)
            Graphics.gridAdd(hsGrid, fst(Views.MakeBoxItem(cm, new TrackerModel.Box(TrackerModel.StairKind.Never, TrackerModel.BoxOwner.None))), i, 2)
        let turnHeartShuffleOn() = for b in row1boxes do b.Set(-1, TrackerModel.PlayerHas.NO)
        let turnHeartShuffleOff() = for b in row1boxes do b.Set(14, TrackerModel.PlayerHas.NO)
        turnHeartShuffleOn()
        let cutoffCanvas = new Canvas(Width=85., Height=85., ClipToBounds=true, IsHitTestVisible=false)
        cutoffCanvas.Children.Add(hsGrid) |> ignore
        let border = new Border(BorderBrush=Brushes.DarkGray, BorderThickness=Thickness(8.,8.,0.,0.), Child=cutoffCanvas)

        let finalChildTipArea = new Canvas()
        let explainerGrid = new Grid(Width=WIDTH*5./6., Height=160.)
        let explainerBorder = new Border(Child=explainerGrid, BorderThickness=Thickness(1.), BorderBrush=Brushes.Orange, Opacity=0., Background=Brushes.Black)
        let explainerLine = new Shapes.Line(Stroke=Brushes.Orange, StrokeThickness=1., Opacity=0.)
        let hoverExplain(fe:FrameworkElement, desc) =
            if not startButtonHasBeenClicked then
                let explanation = MakeTipTextBox(desc)
                explanation.FontSize <- 18.
                explanation.Margin <- Thickness(20.)
                explainerGrid.Children.Clear()
                explainerGrid.Children.Add(explanation) |> ignore
                explainerBorder.Opacity <- 1.
                if not(finalChildTipArea.Children.Contains(explainerBorder)) then
                    let x,y = WIDTH/12. - CHROME_WIDTH/2., 10.
                    Graphics.canvasAdd(finalChildTipArea, explainerBorder, x, y)
                    explainerLine.X1 <- x
                    explainerLine.Y1 <- y
                    finalChildTipArea.Children.Add(explainerLine) |> ignore
                let p = finalChildTipArea.TranslatePoint(Point(), fe)
                explainerLine.X2 <- - p.X
                explainerLine.Y2 <- - p.Y
                explainerLine.Opacity <- 1.
        let explainHide() =
            explainerBorder.Opacity <- 0.
            explainerLine.Opacity <- 0.

        let checkboxSP = new WrapPanel(Orientation=Orientation.Horizontal, VerticalAlignment=VerticalAlignment.Center, HorizontalAlignment=HorizontalAlignment.Center, MaxWidth=WIDTH, Margin=Thickness(0.,9.,0.,0.))   // the three startup options sit side by side
        let hscb = new CheckBox(Content=new TextBox(Text="Full Shuffle",IsReadOnly=true,BorderThickness=Thickness(0.),IsHitTestVisible=false, Margin=Thickness(2.)))
        Graphics.scaleUpCheckBoxBox(hscb, 1.66)
        hscb.IsChecked <- System.Nullable.op_Implicit true
        hscb.Checked.Add(fun _ -> turnHeartShuffleOn())
        hscb.Unchecked.Add(fun _ -> turnHeartShuffleOff())
        let hsArea = new Border(BorderBrush=Brushes.SaddleBrown, BorderThickness=Thickness(1.), Child=hscb)
        hsArea.MouseEnter.Add(fun _ -> hoverExplain(hsArea,"This checkbox corresponds to the randomizer setting:\n\nDungeon Items (tab)\n - Shuffle Dungeon Hearts (checkbox)"))
        hsArea.MouseLeave.Add(fun _ -> explainHide())
        checkboxSP.Children.Add(hsArea) |> ignore

        checkboxSP.Children.Add(new DockPanel(Width=10.)) |> ignore

        let hdcb = new CheckBox(Content=new TextBox(Text="Hide Dungeon Numbers",IsReadOnly=true,BorderThickness=Thickness(0.),IsHitTestVisible=false), Margin=Thickness(2.))
        Graphics.scaleUpCheckBoxBox(hdcb, 1.66)
        hdcb.IsChecked <- System.Nullable.op_Implicit false
        hdcb.Checked.Add(fun _ -> turnHideDungeonNumbersOn())
        hdcb.Unchecked.Add(fun _ -> turnHideDungeonNumbersOff())
        let hdnArea = new Border(BorderBrush=Brushes.SaddleBrown, BorderThickness=Thickness(1.), Child=hdcb)
        hdnArea.MouseEnter.Add(fun _ -> hoverExplain(hdnArea,"This checkbox corresponds to the randomizer setting:\n\nDungeons (tab)\n - Hide Dungeon Numbers (checkbox)"))
        hdnArea.MouseLeave.Add(fun _ -> explainHide())
        checkboxSP.Children.Add(hdnArea) |> ignore

        // custom level name (replaces 'LEVEL-n' at the top of each dungeon map)
        let clnTextBox = new TextBox(Width=110., MaxLength=6, CharacterCasing=CharacterCasing.Upper, IsEnabled=false, Margin=Thickness(6.,2.,2.,2.), VerticalAlignment=VerticalAlignment.Center)
        let clncb = new CheckBox(Content=new TextBox(Text="Custom LEVEL Name",IsReadOnly=true,BorderThickness=Thickness(0.),IsHitTestVisible=false), Margin=Thickness(2.))
        Graphics.scaleUpCheckBoxBox(clncb, 1.66)
        clncb.IsChecked <- System.Nullable.op_Implicit false
        clncb.Checked.Add(fun _ -> clnTextBox.IsEnabled <- true; clnTextBox.Focus() |> ignore)
        clncb.Unchecked.Add(fun _ -> clnTextBox.IsEnabled <- false)
        let clnArea = new Border(BorderBrush=Brushes.SaddleBrown, BorderThickness=Thickness(1.), Child=clncb)
        clnArea.MouseEnter.Add(fun _ -> hoverExplain(clnArea,"Type up to 6 letters/numbers to use in place of\n'LEVEL-1' at the top of each dungeon map."))
        clnArea.MouseLeave.Add(fun _ -> explainHide())
        checkboxSP.Children.Add(new DockPanel(Width=10.)) |> ignore
        checkboxSP.Children.Add(clnArea) |> ignore
        checkboxSP.Children.Add(clnTextBox) |> ignore

        hsPanel.Children.Add(checkboxSP) |> ignore
        // (the little dungeon preview graphic is no longer shown; its hidden state is still driven by the checkboxes)
        stackPanel.Children.Add(hsPanel) |> ignore

        let topSpacer = new DockPanel(Height=10.)   // height adjusted after first layout, to vertically center the Start buttons in the open space above the Settings line
        stackPanel.Children.Add(topSpacer) |> ignore

        let ctxt = System.Threading.SynchronizationContext.Current
        let doStartup(n, loadData : DungeonSaveAndLoad.AllData option) = async {
            // loadData takes precedence over user selections
            let heartShuffle = loadData.IsSome || (hscb.IsChecked.HasValue && hscb.IsChecked.Value)
            let kind = 
                if loadData.IsSome then
                    if loadData.Value.Items.HiddenDungeonNumbers then 
                        TrackerModel.DungeonTrackerInstanceKind.HIDE_DUNGEON_NUMBERS 
                    else 
                        TrackerModel.DungeonTrackerInstanceKind.DEFAULT
                else
                    if hdcb.IsChecked.HasValue && hdcb.IsChecked.Value then
                        TrackerModel.DungeonTrackerInstanceKind.HIDE_DUNGEON_NUMBERS
                    else
                        TrackerModel.DungeonTrackerInstanceKind.DEFAULT
            if loadData.IsSome then
                TrackerModel.IsSecondQuestDungeons <- loadData.Value.Items.SecondQuestDungeons

            TrackerModel.CustomLevelName <- 
                if clncb.IsChecked.HasValue && clncb.IsChecked.Value then clnTextBox.Text.Trim().ToUpperInvariant() else ""
            let mutable speechRecognitionInstance = null
            if TrackerModelOptions.ListenForSpeech.Value then
                printfn "Initializing microphone for speech recognition..."
                try
                    speechRecognitionInstance <- new SpeechRecognition.SpeechRecognitionInstance(kind)
                    SpeechRecognition.speechRecognizer.SetInputToDefaultAudioDevice()
                    SpeechRecognition.speechRecognizer.RecognizeAsync(System.Speech.Recognition.RecognizeMode.Multiple)
                with ex ->
                    printfn "An exception setting up speech, speech recognition will be non-functional, but rest of app will work. Exception:"
                    printfn "%s" (ex.ToString())
                    printfn ""
                    OptionsMenu.microphoneFailedToInitialize <- true
            else
                printfn "Speech recognition will be disabled"
                OptionsMenu.microphoneFailedToInitialize <- true

            let loadingText = new System.Text.StringBuilder("Loading UI...")
            let tb = new TextBox(Text=loadingText.ToString(), IsReadOnly=true, Margin=spacing, Padding=Thickness(5.), MaxWidth=WIDTH/2.)
            stackPanel.Children.Add(tb) |> ignore
            let totalsw = System.Diagnostics.Stopwatch.StartNew()
            let sw = System.Diagnostics.Stopwatch.StartNew()
            let displayStartupTimeDiagnostics(s) = if false then printfn "%s" s  // for debugging startup perf
            let showProgress(label) = 
                async {
                    loadingText.Append('.') |> ignore
                    tb.Text <- loadingText.ToString()
                    do! Async.Sleep(1) // pump to make 'Loading UI' text update
                    do! Async.SwitchToContext ctxt
                    displayStartupTimeDiagnostics(sprintf "prev took %dms" sw.ElapsedMilliseconds)
                    displayStartupTimeDiagnostics(label)
                    sw.Restart()
                }
            // move mainDock to topmost while app is built behind it
            Canvas.SetZIndex(mainDock, 9999)
            do! showProgress("start")
            match loadData with
            | Some data -> lastUpdateMinute <- (data.TimeInSeconds / 60)
            | _ -> ()
            let! u = WPFUI.makeAll(this, cm, drawingCanvas, n, heartShuffle, kind, loadData, showProgress, speechRecognitionInstance)
            updateTimeline <- u
            displayStartupTimeDiagnostics(sprintf "total startup took %dms" totalsw.ElapsedMilliseconds)
            appMainCanvas.Children.Remove(mainDock)  // remove for good
            HotKeys.InitializeWindow(this, OverworldItemGridUI.notesTextBox)
            WPFUI.resetTimerEvent.Publish.Add(fun _ -> lastUpdateMinute <- 0; lastUpdateSecond <- 0; updateTimeline(0); this.SetStartTimeToNow())
            if loadData.IsNone then
                WPFUI.resetTimerEvent.Trigger()  // takes a few seconds to load everything, reset timer at start
            Graphics.canvasAdd(hmsTimerCanvas, OverworldItemGridUI.hmsTimeTextBox, Layout.RIGHT_COL+190., 0.)
            gotThruStartup <- true
            if promptedCrashRecovery then
                finishCrashInfoImpl("prompted for crash recovery, user chose not to, successfully started")
            }

        this.Closed.Add(fun _ ->  // still does not handle 'rude' shutdown, like if they close the console window
            Popouts.theMainWindowHasClosed <- true
            if settingsWereSuccessfullyRead then      // don't overwrite an unreadable file, the user may have been intentionally hand-editing it and needs feedback
                TrackerModelOptions.writeSettings()  // save any settings changes they made before closing the startup window
            )
        // recently loaded save files (most recent first), remembered next to the program so they can be re-opened with one click
        let recentLoadsFile = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Z1R_Tracker_recent_loads.txt")
        let maxRecentLoads = 5
        let readRecentLoads() : string list =
            try
                if System.IO.File.Exists(recentLoadsFile) then
                    System.IO.File.ReadAllLines(recentLoadsFile)
                    |> Array.filter (fun p -> p.Trim() <> "" && System.IO.File.Exists(p))
                    |> Array.distinct
                    |> Array.truncate maxRecentLoads
                    |> List.ofArray
                else []
            with _ -> []
        let recordRecentLoad(path:string) =
            try
                let updated = path :: (readRecentLoads() |> List.filter (fun p -> not(System.String.Equals(p, path, System.StringComparison.OrdinalIgnoreCase)))) |> List.truncate maxRecentLoads
                System.IO.File.WriteAllLines(recentLoadsFile, updated)
            with _ -> ()
        let pendingLoadPath : string option ref = ref None   // set when the user clicks an entry in the recent-saves box (skips the file dialog)
        let startButtonBehavior(n) = 
            if startButtonHasBeenClicked then () else
            startButtonHasBeenClicked <- true
            explainHide()
            turnHeartShuffleOn()  // To draw the display, I have been interacting with the global ChoiceDomain for items.  This switches all the boxes back to empty, 'zeroing out' what we did.
            async {
                TrackerModelOptions.writeSettings()

                if false then   // this feature is currently unused
                    Gamepad.ControllerFailureEvent.Publish.Add(handle)
                    OptionsMenu.gamepadFailedToInitialize <- not(Gamepad.Initialize())

                let mutable loadData = None
                if n = 999 then
                    let pickedPath =
                        match pendingLoadPath.Value with
                        | Some p -> Some p
                        | None ->
                            let ofd = new Microsoft.Win32.OpenFileDialog()
                            ofd.InitialDirectory <- System.AppDomain.CurrentDomain.BaseDirectory
                            ofd.Filter <- "All JSON files|*.json|ZTracker saves|zt-save-*.json|All Files|*.*"
                            let r = ofd.ShowDialog(this)
                            if r.HasValue && r.Value then Some ofd.FileName else None
                    if pickedPath.IsSome then
                        let pickedFile = pickedPath.Value
                        try
                            let json = System.IO.File.ReadAllText(pickedFile)
                            let ver = System.Text.Json.JsonSerializer.Deserialize<DungeonSaveAndLoad.JustVersion>(json, new System.Text.Json.JsonSerializerOptions(AllowTrailingCommas=true))
                            if ver.Version <> OverworldData.VersionString then
                                let msg = 
                                    let prefix = 
                                        if ver.Version.StartsWith("1.2") || ver.Version.StartsWith("1.3") then
                                            "Loading this file might not work"
                                        else
                                            "Loading this file will probably fail"
                                    sprintf "You are running Z-Tracker version '%s' but the\nsave file was created using version '%s'.\n%s, but Z-Tracker will attempt to load it anyway." 
                                                    OverworldData.VersionString ver.Version prefix
                                popupIsActive <- true
                                let! r = CustomComboBoxes.DoModalMessageBox(cm, System.Drawing.SystemIcons.Error, msg, ["Attempt Load"])
                                popupIsActive <- false
                                ignore r
                            loadData <- Some(DungeonSaveAndLoad.LoadAll(json))
                            recordRecentLoad(pickedFile)
                        with e ->
                            let msg = sprintf "Loading the save file\n%s\nfailed with error:\n%s"  pickedFile e.Message
                            popupIsActive <- true
                            let! r = CustomComboBoxes.DoModalMessageBox(cm, System.Drawing.SystemIcons.Error, msg, ["Restart"])
                            popupIsActive <- false
                            ignore r
                            Graphics.RestartTheApplication()
                    else
                        let msg = sprintf "Failed to load a save file."
                        popupIsActive <- true
                        let! r = CustomComboBoxes.DoModalMessageBox(cm, System.Drawing.SystemIcons.Error, msg, ["Restart"])
                        popupIsActive <- false
                        ignore r
                        Graphics.RestartTheApplication()
                do! doStartup(n,loadData)
            } |> Async.StartImmediate
        let quests = [|
            0, "1st Quest Overworld"
            1, "2nd Quest Overworld"
            2, "Mixed - 1st Quest Overworld"
            3, "Mixed - 2nd Quest Overworld\n(or randomized quest)"
            999, "from a previously saved state"
            |]
        let addQuestExplainer(startButton:FrameworkElement) =
            startButton.MouseEnter.Add(fun _ -> hoverExplain(startButton,"Choose the start button that matches the randomizer setting:\n\nOverworld Quest (dropdown)"))
            startButton.MouseLeave.Add(fun _ -> explainHide())
        for n,q in quests do
            let startButton = Graphics.makeButton(sprintf "Start: %s" q, None, None)
            if n=0 then
                startButton.Margin <-Thickness(0.)
            elif n=999 then
                startButton.Margin <- smallSpacing
            else
                startButton.Margin <- spacing
            if n=0 then
                let dp = new DockPanel(LastChildFill=true, Width=WIDTH/2.)
                let otherButton = Graphics.makeButton(". . .", None, None)
                let tb = new TextBox(Text="See more\noptions",IsReadOnly=true, TextAlignment=TextAlignment.Center, HorizontalAlignment=HorizontalAlignment.Center, BorderThickness=Thickness(2.), Opacity=0.)
                appMainCanvas.Children.Add(tb) |> ignore
                otherButton.MouseEnter.Add(fun _ ->
                    let pos = otherButton.TransformToAncestor(appMainCanvas).Transform(Point(otherButton.ActualWidth+3., 0.))
                    Canvas.SetLeft(tb, pos.X)
                    Canvas.SetTop(tb, pos.Y)
                    tb.Opacity <- 1.0
                    )
                otherButton.MouseLeave.Add(fun _ -> tb.Opacity <- 0.0)
                otherButton.Margin <-Thickness(6.,0.,0.,0.)
                dp.Children.Add(otherButton) |> ignore
                otherButton.Click.Add(fun _ -> 
                    if popupIsActive then () else
                    popupIsActive <- true
                    let dialog1 = new StackPanel(Orientation=Orientation.Vertical, Background=Brushes.Black)
                    addDarkTheme(dialog1.Resources)
                    let tb1 = new TextBox(IsReadOnly=true, BorderThickness=Thickness(0.), Foreground=Brushes.Orange)
                    tb1.Text <- "Z-Tracker was designed for use with fcoughlin's Zelda 1 Randomizer.\n\n" +
                                
                                "There are other randomizers/ROM-Hacks for Zelda 1 which use other\n" + 
                                "(sometimes randomized) overworld maps.  Z-Tracker may not work with\n" +
                                "these perfectly, but the options here are designed to allow you to use\n" +
                                "Z-Tracker with non-standard overworld maps, for a slightly degraded, but\n" +
                                "workable tracking experience.\n\n" + 

                                "There are two main options:\n" +
                                " - use a 'blank' 16x8 grid for overworld map tracking\n" +
                                " - use a custom .png map file, supplied by your other randomizer/ROM-Hack\n\n" +

                                "If you do supply a custom map file, you can also choose whether you want the map\n" +
                                "to be fully revealed/visible at the outset, or whether each of the 16x8 tiles\n" +
                                "should be individually hidden, until you click each tile to reveal it.\n\n" +

                                "Choose an option below:"
                    dialog1.Children.Add(tb1) |> ignore
                    let wh = new System.Threading.ManualResetEvent(false)
                    let mutable choice = None
                    for txt,n in [
                                    "I just want a blank 16x8 grid", 0
                                    "I want to select a map file on disk, and have it fully revealed at the start", 1
                                    "I want to select a map file on disk, but have each tile hidden at the start", 2
                                 ] do
                        let button = Graphics.makeButton(txt, None, None)
                        button.Margin <- Thickness(0., 10., 0., 0.)
                        dialog1.Children.Add(button) |> ignore
                        button.Click.Add(fun _ -> if choice.IsNone then choice <- Some(n); wh.Set() |> ignore)
                    async {
                        do! CustomComboBoxes.DoModal(cm, wh, 30., 30., new Border(Child=dialog1, BorderThickness=Thickness(3.), BorderBrush=Brushes.Orange, Background=Brushes.Black, Padding=Thickness(5.)))
                        Graphics.alternativeOverworldMapFilename <- ""
                        match choice with
                        | None -> ()  // just exit the modal and go back to startup screen if they click outside
                        | Some choice ->
                            if choice<>0 then
                                let ofd = new Microsoft.Win32.OpenFileDialog()
                                ofd.InitialDirectory <- System.AppDomain.CurrentDomain.BaseDirectory
                                ofd.Filter <- "Overworld map images|*.png"
                                let r = ofd.ShowDialog(this)
                                if r.HasValue && r.Value then
                                    Graphics.alternativeOverworldMapFilename <- ofd.FileName
                            Graphics.shouldInitiallyHideOverworldMap <- (choice=0 || choice=2)
                            let text = (if choice=0 then "You have chosen a blank map grid.\n\n" else "You have chosen to load a map file.\n\n") +

                                        "Some randomizers have behavior that Z-Tracker does not natively support.  For example, " +
                                        "in z1m1 you might be able to purchase a Ladder in an overworld shop.  There is no native " +
                                        "Z-Tracker support for marking an overworld tile as a Ladder shop.  But you can add some " +
                                        "abitrary markup to the app in a few ways:\n" +
                                        " - click the 'Draw' button in the bottom left, to place arbitrary icons\n" +
                                        "      (e.g. you might put '$' and Ladder icons on an overworld tile)\n" +
                                        " - shift-left-click an overworld tile, to circle and label it\n" +
                                        "      (e.g. you might mark a tile with a cyan circle and an 'L')\n" +
                                        " - type text into the 'Notes' text box\n"+
                                        "      (e.g. you might type 'Ladder for sale at tile B-4')\n\n" +

                                        "Do whatever works for you.  Good luck!"
                            let! _r = CustomComboBoxes.DoModalMessageBoxCore(cm, System.Drawing.SystemIcons.Information, text, ["Ok"], 30., 30.)
                            startButtonBehavior(4)
                        popupIsActive <- false
                    } |> Async.StartImmediate
                    )
                DockPanel.SetDock(otherButton, Dock.Right)
                dp.Children.Add(startButton) |> ignore
                addQuestExplainer(startButton)
                stackPanel.Children.Add(dp) |> ignore
            else
                startButton.Width <- WIDTH/2.
                stackPanel.Children.Add(startButton) |> ignore
                if n<=3 then addQuestExplainer(startButton)
            startButton.Click.Add(fun _ -> startButtonBehavior(n))

        // box listing the save files loaded before, for fast re-selection
        do
            let recents = readRecentLoads()
            let listSP = new StackPanel(Orientation=Orientation.Vertical)
            let header = new TextBox(Text="Recently loaded saves:", IsReadOnly=true, BorderThickness=Thickness(0.), HorizontalAlignment=HorizontalAlignment.Center, FontSize=14., Margin=Thickness(0.,0.,0.,3.))
            listSP.Children.Add(header) |> ignore
            if recents.IsEmpty then
                listSP.Children.Add(new TextBox(Text="(none yet - files you load will show up here)", IsReadOnly=true, BorderThickness=Thickness(0.), Foreground=Brushes.Gray, HorizontalAlignment=HorizontalAlignment.Center, FontSize=13.)) |> ignore
            for p in recents do
                let when_ = try System.IO.File.GetLastWriteTime(p).ToString("yyyy-MM-dd HH:mm") with _ -> ""
                let b = Graphics.makeButton(System.IO.Path.GetFileNameWithoutExtension(p), None, None)
                b.Margin <- Thickness(0.,1.,0.,1.)
                b.FontSize <- 14.
                b.ToolTip <- sprintf "%s\nlast modified %s" p when_
                b.Click.Add(fun _ ->
                    if not(System.IO.File.Exists(p)) then
                        b.IsEnabled <- false
                        b.ToolTip <- "This file no longer exists"
                    else
                        pendingLoadPath.Value <- Some p
                        startButtonBehavior(999))
                listSP.Children.Add(b) |> ignore
            let scroll = new ScrollViewer(Content=listSP, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, MaxHeight=150.)
            let recentBox = new Border(Child=scroll, BorderBrush=Brushes.SaddleBrown, BorderThickness=Thickness(1.), Padding=Thickness(4.), Width=WIDTH/2., HorizontalAlignment=HorizontalAlignment.Center, Margin=smallSpacing)
            stackPanel.Children.Add(recentBox) |> ignore

        stackPanel.Children.Add(finalChildTipArea) |> ignore  // finalChildTipArea still used for explainer arrows on startup options

        // Tip lives inside bottomSP so it's in proper layout flow — can never overlap the Settings header
        let bottomSP = new StackPanel(Orientation=Orientation.Vertical, HorizontalAlignment=HorizontalAlignment.Center)
        let tipsp = new StackPanel(Orientation=Orientation.Vertical)
        let tb = MakeTipTextBox("Random tip:")
        tipsp.Children.Add(tb) |> ignore
        let tb = MakeTipTextBox(DungeonData.Factoids.allTips.[ System.Random().Next(DungeonData.Factoids.allTips.Length) ])   // correct app behavior
        //let tb = MakeTipTextBox(DungeonData.Factoids.allTips |> Array.sortBy (fun s -> s.Length) |> Seq.last)   // show the longest tip (to test screen layout)
        tb.Margin <- spacing
        tipsp.Children.Add(tb) |> ignore
        let _tip = new Border(Child=tipsp, BorderThickness=Thickness(1.), Padding=Thickness(5.), BorderBrush=Brushes.Orange, Width=WIDTH*2./3., Margin=Thickness(0.,4.,0.,4.), HorizontalAlignment=HorizontalAlignment.Center)
        //bottomSP.Children.Add(tip) |> ignore   // random tip box removed from the startup screen
        bottomSP.Children.Add(new Shapes.Rectangle(HorizontalAlignment=HorizontalAlignment.Stretch, Fill=Brushes.Black, Height=2., Margin=spacing)) |> ignore
        let tb = new TextBox(Text="Settings (most can be changed later, using 'Options...' button above timeline):", HorizontalAlignment=HorizontalAlignment.Center,
                                Margin=Thickness(0.,0.,0.,5.), BorderThickness=Thickness(0.))
        bottomSP.Children.Add(tb) |> ignore
        let options = OptionsMenu.makeOptionsCanvas(cm, false, true)
        bottomSP.Children.Add(options) |> ignore
        mainDock.Children.Add(bottomSP) |> ignore
        DockPanel.SetDock(bottomSP, Dock.Bottom)

        mainDock.Children.Add(mainStackPanel) |> ignore
        // vertically center the block of Start buttons between the startup-options checkboxes above and the Settings line below
        let centeredOnce = ref true   // (no longer vertically centering the Start buttons; they sit right under the options row)
        mainDock.LayoutUpdated.Add(fun _ ->
            if not centeredOnce.Value && mainDock.IsLoaded && bottomSP.ActualHeight > 0. && finalChildTipArea.IsLoaded && hsPanel.ActualHeight > 0. then
                centeredOnce.Value <- true
                try
                    let hsBottom = hsPanel.TranslatePoint(Point(0., hsPanel.ActualHeight), mainDock).Y
                    let buttonsTop = topSpacer.TranslatePoint(Point(0., topSpacer.ActualHeight), mainDock).Y
                    let buttonsBottom = finalChildTipArea.TranslatePoint(Point(), mainDock).Y
                    let settingsTop = bottomSP.TranslatePoint(Point(), mainDock).Y
                    let available = settingsTop - hsBottom
                    let blockHeight = buttonsBottom - buttonsTop
                    let desiredGap = (available - blockHeight) / 2.
                    let currentGap = buttonsTop - hsBottom
                    if desiredGap > currentGap then
                        topSpacer.Height <- topSpacer.ActualHeight + (desiredGap - currentGap)
                with _ -> ()
            )

        // "dark theme"
        mainDock.Background <- Brushes.Black
        addDarkTheme(mainDock.Resources)

        if System.IO.File.Exists(crashLogFilename) then
            let lines = System.IO.File.ReadAllLines(crashLogFilename)
            if lines.Length > 1 then
                match DateTime.TryParseExact(lines.[lines.Length-1], dateTimeFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None) with
                | false, _ -> ()
                | true, crashTime ->
                    if DateTime.Now - crashTime < TimeSpan.FromMinutes(10.) then
                        // it crashed in the past 10 minutes
                        if System.IO.File.Exists(SaveAndLoad.AutoSaveFilename) then
                            let autoSaveTime = System.IO.File.GetLastWriteTime(SaveAndLoad.AutoSaveFilename)
                            if autoSaveTime > (crashTime - TimeSpan.FromMinutes(2.)) then
                                popupIsActive <- true
                                async {
                                // there was an autosave just before the crash
                                let! r = CustomComboBoxes.DoModalMessageBox(cm, System.Drawing.SystemIcons.Error, 
                                                                            "It appears that Z-Tracker recently crashed.\n\n"+
                                                                                "There is a recent auto-save from a\n"+
                                                                                "minute or so before the crash.\n\n"+
                                                                                "Would you like to try loading the auto-save?", ["Yes, load auto-save"; "No"])
                                popupIsActive <- false
                                if r <> "No" then
                                    let json = System.IO.File.ReadAllText(SaveAndLoad.AutoSaveFilename)
                                    let loadData = Some(DungeonSaveAndLoad.LoadAll(json))
                                    do! doStartup(999, loadData)
                                    // successful reload of autosave, call this so next startup won't also trigger recovery dialog
                                    finishCrashInfoImpl("successful reload of autosave")
                                else
                                    promptedCrashRecovery <- true
                                } |> Async.StartImmediate
        
    override this.Update(f10Press) =
        base.Update(f10Press)
        // update time
        let ts = DateTime.Now - this.StartTime.Time
        let curSecond = int ts.TotalSeconds
        if curSecond > lastUpdateSecond then
            lastUpdateSecond <- curSecond
            let h,m,s = ts.Hours, ts.Minutes, ts.Seconds
            let time = sprintf "%2d:%02d:%02d" h m s
            OverworldItemGridUI.hmsTimeTextBox.Text <- time
            OverworldItemGridUI.broadcastTimeTextBox.Text <- time
            let curMinute = int ts.TotalMinutes
            if f10Press || curMinute > lastUpdateMinute then
                lastUpdateMinute <- curMinute
                updateTimeline(curMinute)

// in order for multiple app windows to not have a forced Z-Order from Owner-Child relationship, need a hidden dummy window to own all the visible windows
type DummyWindow() as this =
    inherit Window()
    do
        this.ShowInTaskbar <- false
        this.Title <- "Z-Tracker start-up..."
        this.Width <- 300.
        this.Height <- 100.
        this.WindowState <- WindowState.Minimized
        this.Loaded.Add(fun _ ->
            this.Visibility <- Visibility.Hidden
            
(*
            // method 1
            let resHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height  // e.g. 1440
            let actualHeight = SystemParameters.PrimaryScreenHeight  // e.g. 960
            let scale = float resHeight / actualHeight  // e.g.   1.5 
            let waHeight = SystemParameters.WorkArea.Height
            printfn "method1: scale = %f    actualHeight=%f    waHeight=%f" scale actualHeight waHeight
            // TODO if waHeight < 1000.0 then suggest Smaller
//caffy
//method1: scale = 1.250000    actualHeight=864.000000    waHeight=824.000000
//method2: scale = 1.250000
            // method 2
            let dpi = VisualTreeHelper.GetDpi(this)
            printfn "method2: scale = %f, ppi = %f" dpi.DpiScaleY dpi.PixelsPerInchY
*)

            let mainW = new MyWindow()
            mainW.Owner <- this
            mainW.Show()
            let handle = Winterop.GetConsoleWindow()
            Winterop.ShowWindow(handle, Winterop.SW_MINIMIZE) |> ignore
            mainW.Closed.Add(fun _ -> try this.Close() with e -> ())  // ignore exceptions on close; restart button tends to crash here
            )

[<STAThread>]
[<EntryPoint>]
let main _argv = 
    printfn "Starting Z-Tracker..."
    let cwd = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName)
    let thisExe = System.IO.Path.Combine(cwd, Graphics.ExeName)
    let thisExeName = thisExe.Replace('\\', '_')   // backslash is not allowed character in mutex name
    use mutex = new System.Threading.Mutex(false, thisExeName)
    let killAnyZombieHosts () =
        let procs = System.Diagnostics.Process.GetProcessesByName("Z1R_SignalRHost")
        for p in procs do
            try p.Kill() with _ -> ()

    let runTheApp() =
        System.AppDomain.CurrentDomain.ProcessExit.Add(fun _ ->
            Dungeon.stopLocalSignalRHost()
        )
        killAnyZombieHosts()
        let app = new Application()
#if DEBUG
        do
#else
        try
#endif
            //startLocalSignalRHost()
            theDummyWindow <- DummyWindow()
            app.Exit.Add(fun _ -> Dungeon.stopLocalSignalRHost())
            app.Run(theDummyWindow) |> ignore

#if DEBUG
#else
        with e ->
            let crashLog = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Z1R_Tracker_crash_log.txt")
            try
                System.IO.File.AppendAllText(crashLog, sprintf "\n\nBEGIN CRASH LOG (startup) -- %s\n%s\nEND CRASH LOG\n" (System.DateTime.Now.ToString("yyyy-MM-dd-HH:mm:ss")) (e.ToString()))
            with _ -> ()
            reraise()
#endif
   
    try
        if mutex.WaitOne(5000, false) then   // single instance, to prevent competing write of auto-save or options in this directory
            runTheApp()
        else
            printfn "Another instance of Z-Tracker is already running in this directory."
            printfn "Close it before running this."
            printfn "(Press a key to dismiss this console window)"
            System.Threading.Thread.Sleep(1000)
            System.Console.ReadKey() |> ignore
    with 
    | :? System.Threading.AbandonedMutexException ->  // this happens if e.g. someone shuts down the other instance by clicking 'X' on its window corner while we are waiting
        runTheApp()  // fine to start us now
    | e ->
        printfn "ERROR: %s" (e.ToString())
        printfn "(Press a key to dismiss this console window)"
        System.Console.ReadKey() |> ignore

    Dungeon.stopLocalSignalRHost()
    Async.RunSynchronously(Async.AwaitWaitHandle(waitForConsole)) |> ignore
    0
