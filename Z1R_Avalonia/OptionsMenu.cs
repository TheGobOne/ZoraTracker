// Ported from Z1R_WPF/OptionsMenu.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// The voice (WPF: a System.Speech SpeechSynthesizer) is a Platform.SpeechSynthesizer: System.Speech on Windows, a stub elsewhere.
// Every F# `async { } |> Async.StartImmediate` is a `CustomComboBoxes.StartImmediate(...)` of an async lambda, as in Dungeon.cs.
// F# `Event<unit>`s are C# `event Action`s with a public `trigger…()` method (Graphics.volumeChanged pattern), since other files raise them.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Z1R_Avalonia;

public static class OptionsMenu
{
    private static bool raceModeSavedMuted = false;   // the user's own mute choice while Race Mode forces mute on
    private static bool raceModeWasOn = false;
    // the mute choice is also kept in a small file while Race Mode is on, so turning Race Mode off after a restart still puts it back
    private static string raceModeMuteFile() => Platform.Files.User("Z1R_Tracker_race_mode_mute.txt");
    private static void writeRaceModeSavedMuted(bool muted)
    {
        try { System.IO.File.WriteAllText(raceModeMuteFile(), muted ? "1" : "0"); } catch { }
    }
    private static bool? readRaceModeSavedMuted()
    {
        try
        {
            var f = raceModeMuteFile();
            if (!System.IO.File.Exists(f)) return null;
            var s = System.IO.File.ReadAllText(f).Trim();
            return s == "1" ? true : s == "0" ? false : (bool?)null;
        }
        catch { return null; }
    }
    private static void deleteRaceModeSavedMuted()
    {
        try { System.IO.File.Delete(raceModeMuteFile()); } catch { }
    }
    public static readonly Platform.SpeechSynthesizer voice = new Platform.SpeechSynthesizer();
    private static string make_defaultVoice() { try { return voice.VoiceName; } catch { return ""; } }
    public static readonly string defaultVoice = make_defaultVoice();
    public static void InitializeVoice()
    {
        try
        {
            voice.Volume = TrackerModelOptions.Volume;
            voice.SelectVoice(TrackerModelOptions.PreferredVoice);
        }
        catch { }
    }

    public static bool microphoneFailedToInitialize = false;
    public static bool gamepadFailedToInitialize = false;

    public static bool userWantsMute = false;
    public static bool raceModeMuted = false;




    public static event Action broadcastWindowOptionChanged;
    public static void triggerBroadcastWindowOptionChanged() => broadcastWindowOptionChanged?.Invoke();
    public static event Action mouseMagnifierWindowOptionChanged;
    public static void triggerMouseMagnifierWindowOptionChanged() => mouseMagnifierWindowOptionChanged?.Invoke();
    public static event Action BOARDInsteadOfLEVELOptionChanged;
    public static void triggerBOARDInsteadOfLEVELOptionChanged() => BOARDInsteadOfLEVELOptionChanged?.Invoke();
    public static event Action displayIconsInDungeonMapOptionChanged;
    public static void triggerDisplayIconsInDungeonMapOptionChanged() => displayIconsInDungeonMapOptionChanged?.Invoke();
    public static event Action showDungeonHintIconsOptionChanged;
    public static void triggerShowDungeonHintIconsOptionChanged() => showDungeonHintIconsOptionChanged?.Invoke();
    public static event Action secondQuestDungeonsOptionChanged;
    public static void triggerSecondQuestDungeonsOptionChanged() => secondQuestDungeonsOptionChanged?.Invoke();
    public static event Action showBasementInfoOptionChanged;
    public static void triggerShowBasementInfoOptionChanged() => showBasementInfoOptionChanged?.Invoke();
    public static event Action bookForHelpfulHintsOptionChanged;
    public static void triggerBookForHelpfulHintsOptionChanged() => bookForHelpfulHintsOptionChanged?.Invoke();
    public static event Action requestRedrawOverworldEvent;
    public static void triggerRequestRedrawOverworldEvent() => requestRedrawOverworldEvent?.Invoke();
    public static event Action hideTimerChanged;
    public static void triggerHideTimerChanged() => hideTimerChanged?.Invoke();
    public static event Action dungeonSunglassesChanged;
    public static void triggerDungeonSunglassesChanged() => dungeonSunglassesChanged?.Invoke();
    public static event Action dungeonOffTileEdgesChanged;
    public static void triggerDungeonOffTileEdgesChanged() => dungeonOffTileEdgesChanged?.Invoke();
    public static event Action raceModeChanged;
    public static void triggerRaceModeChanged() => raceModeChanged?.Invoke();

    public static bool isValidUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out _);

    // (port helper: WPF's ToolTipService.SetShowDuration(x, ms). Avalonia has no show duration: a tooltip stays open while the mouse is over
    // its control, so there's nothing to set. Kept as a call so the lines still match WPF; see PROGRESS.md.)
    private static void setToolTipShowDuration(Control c, int ms) { }

    public static void link(CheckBox cb, TrackerModelOptions.Bool b, bool needFU, Action otherEffect)
    {
        void effect()
        {
            if (needFU)
                TrackerModel.forceUpdate();
            otherEffect();
        }
        cb.IsChecked = b.Value;
        Graphics.onChecked(cb, () => { b.Value = true; effect(); });
        Graphics.onUnchecked(cb, () => { b.Value = false; effect(); });
    }

    public static (string text, string tip, TrackerModelOptions.Bool b, bool needFU, Action otherEffect, Thickness? marginOpt)[] data1o(bool isStandardHyrule)
    {
        var r = new List<(string, string, TrackerModelOptions.Bool, bool, Action, Thickness?)>();
        // These features are disabled by the app when not(isStandardHyrule)
        if (isStandardHyrule) r.Add(("Draw Routes", "Constantly display routing lines when mousing over overworld tiles", TrackerModelOptions.Overworld.DrawRoutes, true, () => { }, null));
        if (isStandardHyrule) r.Add(("Show Screen Scrolls", "Routing lines assume the player can screen scroll\nScreen scrolls appear as curved lines", TrackerModelOptions.Overworld.RoutesCanScreenScroll, true, () => { }, new Thickness(20.0, 0.0, 0.0, 0.0)));
        if (isStandardHyrule) r.Add(("Highlight Nearby", "Highlight nearest unmarked gettable overworld tiles when mousing", TrackerModelOptions.Overworld.HighlightNearby, false, () => { }, null));
        if (isStandardHyrule) r.Add(("Hover On Zones/Coords", "Hovering the Zones or Coords checkbox shows that overlay while the mouse is over it", TrackerModelOptions.Overworld.HoverZonesCoords, false, () => { }, null));
        if (isStandardHyrule) r.Add(("Show Magnifier", "Display magnified view of overworld tiles when mousing", TrackerModelOptions.Overworld.ShowMagnifier, false, () => { }, null));
        r.Add(("Use Old Popups", "Check this to show the old picture popups (instead of the button boxes)\nwhen marking the wood sword cave and the take-any caves", TrackerModelOptions.Overworld.UseOldPopups, false, () => { }, null));
        // Mirror overworld is not useful when not(isStandardHyrule), but if the user has it checked, we want them to be able to uncheck it
// now a clickable feature of top tracker area
//    yield "Mirror overworld", "Flip the overworld map East<->West", TrackerModelOptions.Overworld.MirrorOverworld, true, (fun()->()), None
        return r.ToArray();
    }

    public static readonly (string text, string tip, TrackerModelOptions.Bool b, bool needFU, Action otherEffect)[] data1d = new (string, string, TrackerModelOptions.Bool, bool, Action)[] {
        ("Dungeon Map Hint", "Check this box to add a transparent icon to the dungeon map rooms in order to better guess where that room is", TrackerModelOptions.DisplayIconsInDungeonMap, false, triggerDisplayIconsInDungeonMapOptionChanged),
// now a clickable feature of top tracker area
//    "Second quest dungeons", "Check this if dungeon 4, rather than dungeon 1, has 3 items (no effect when Hidden Dungeon Numbers)", TrackerModelOptions.IsSecondQuestDungeons, false, secondQuestDungeonsOptionChanged.Trigger
        ("Show Dungeon Hint Icons", "Uncheck to hide the dim question-mark icons above the dungeons and other items\n(where you click to pick a location hint)", TrackerModelOptions.ShowDungeonHintIcons, false, triggerShowDungeonHintIconsOptionChanged),
        ("BOARD Instead Of LEVEL", "Check this to label the dungeon tabs and headers BOARD-1 ... BOARD-9\ninstead of LEVEL-1 ... LEVEL-9", TrackerModelOptions.BOARDInsteadOfLEVEL, false, triggerBOARDInsteadOfLEVELOptionChanged),
        ("Show Basement Info", "Check this if empty dungeon item boxes should suggest whether they are found as\nbasement items rather than floor drops (no effect when Hidden Dungeon Numbers)", TrackerModelOptions.ShowBasementInfo, false, triggerShowBasementInfoOptionChanged),
        ("Do Door Inference", "Check this to mark a green door when you mark a new room, if the point of entry can be inferred", TrackerModelOptions.DoDoorInference, false, () => { }),
        ("Book For Helpful Hints", "Check this if both 'Book To Understand Old Men' flag is on, and\n'Helpful' hints are available. The tracker will let you left-click\nOld Man Hint rooms to toggle whether you have read them yet.", TrackerModelOptions.BookForHelpfulHints, false, triggerBookForHelpfulHintsOptionChanged),
        ("Alphabetize Hint Zone List", "When checked, the hint zone dropdown (where you mark which zone a dungeon/sword is in)\nshows zones in alphabetical order instead of geographic order.", TrackerModelOptions.AlphabetizeHintZones, false, () => { }),
        ("Red Edge By OFF Tiles", "Uncheck to stop drawing a red 'no bomb' edge on the side of a dungeon room\nthat touches an OFF tile", TrackerModelOptions.ShowOffTileEdges, false, triggerDungeonOffTileEdgesChanged),
        ("Left-Drag Auto-Inverts", "Painting maps: When checked, If your first drag is with left-click,\nand you've not yet inverted OffTheMap with Unmarked, then\nauto-invert when left-click-dragging, to immediately start painting a map.", TrackerModelOptions.LeftClickDragAutoInverts, false, () => { }),
        ("Default To NonDescript", "Room default: When checked, clicking an Unmarked room will mark it as\nNonDescript (empty box) rather than MaybePushBlock (box with two dots)", TrackerModelOptions.DefaultRoomPreferNonDescriptToMaybePushBlock, false, () => { }),
        ("Dungeon 'Sunglasses'", "The dungeon tracker has high contrast (bright colors against black);\nTurn this on to darken the colors somewhat to reduce bright contrast", TrackerModelOptions.GiveDungeonTrackerSunglasses, false, triggerDungeonSunglassesChanged),
        ("Reverse Scroll Wheel", "When unchecked: scroll up = monster popup, scroll down = floor-drop/item popup.\nWhen checked: scroll up = floor-drop/item popup, scroll down = monster popup.\n(Dungeon rooms)", TrackerModelOptions.ReverseScrollWheelDirection, false, () => { }),
        ("Hide Lock And Hints", "Check this to hide the tip box and the lock bar on the item, tile, dungeon room type,\nmonster and floor drop menus", TrackerModelOptions.HidePopupHintAndLock, false, () => { }),
        };

    public static readonly (string text, string tip, TrackerModelOptions.Bool bVoice, TrackerModelOptions.Bool bVisual)[] data2 = new (string, string, TrackerModelOptions.Bool, TrackerModelOptions.Bool)[] {
        (TrackerModel.ReminderCategory.DungeonFeedback.DisplayName, "Note when dungeons are located/completed, triforces obtained, and go-time",
            TrackerModelOptions.VoiceReminders.DungeonFeedback, TrackerModelOptions.VisualReminders.DungeonFeedback),
        (TrackerModel.ReminderCategory.SwordHearts.DisplayName, "Remind to consider white/magical sword when you get 4-6 or 10-14 hearts",
            TrackerModelOptions.VoiceReminders.SwordHearts,     TrackerModelOptions.VisualReminders.SwordHearts),
        (TrackerModel.ReminderCategory.CoastItem.DisplayName, "Reminder to fetch to coast item when you have the ladder",
            TrackerModelOptions.VoiceReminders.CoastItem,       TrackerModelOptions.VisualReminders.CoastItem),
        (TrackerModel.ReminderCategory.RecorderSpots.DisplayName, "Periodic reminder of how many recorder spots remain (fires at reminder interval)",
            TrackerModelOptions.VoiceReminders.RecorderSpots,   TrackerModelOptions.VisualReminders.RecorderSpots),
        (TrackerModel.ReminderCategory.PowerBraceletSpots.DisplayName, "Periodic reminder of how many power-bracelet spots remain (fires at reminder interval)",
            TrackerModelOptions.VoiceReminders.PowerBraceletSpots, TrackerModelOptions.VisualReminders.PowerBraceletSpots),
        (TrackerModel.ReminderCategory.BoomstickBook.DisplayName, "Periodic reminder when the boomstick book is available in a found shop (fires at reminder interval)",
            TrackerModelOptions.VoiceReminders.BoomstickBook,   TrackerModelOptions.VisualReminders.BoomstickBook),
        (TrackerModel.ReminderCategory.HaveMagicKey.DisplayName, "One-time reminder when entering a dungeon that you have the magic key",
            TrackerModelOptions.VoiceReminders.HaveMagicKey,    TrackerModelOptions.VisualReminders.HaveMagicKey),
        (TrackerModel.ReminderCategory.HaveLadder.DisplayName, "One-time reminder when entering a dungeon that you have the ladder",
            TrackerModelOptions.VoiceReminders.HaveLadder,      TrackerModelOptions.VisualReminders.HaveLadder),
        (TrackerModel.ReminderCategory.Blockers.DisplayName, "Reminder when you may have become unblocked on a previously-aborted dungeon",
            TrackerModelOptions.VoiceReminders.Blockers,        TrackerModelOptions.VisualReminders.Blockers),
        (TrackerModel.ReminderCategory.DoorRepair.DisplayName, "Each time you uncover a door repair charge, remind the count of how many you have found",
            TrackerModelOptions.VoiceReminders.DoorRepair,        TrackerModelOptions.VisualReminders.DoorRepair),
        (TrackerModel.ReminderCategory.OverworldOverwrites.DisplayName, "Each time you make a destructive change to an overworld mark, remind the change, in case it was accidental",
            TrackerModelOptions.VoiceReminders.OverworldOverwrites, TrackerModelOptions.VisualReminders.OverworldOverwrites),
        (TrackerModel.ReminderCategory.RandomExtras.DisplayName, "When a run starts with any of the ZORA extras (extra blocks, Dead Woods, Lost Hills) set to Random, remind you to check the Overworld + Extras menu and turn them on or off once you know",
            TrackerModelOptions.VoiceReminders.RandomExtras, TrackerModelOptions.VisualReminders.RandomExtras),
        };

    public static void showCoopHostSettingsWindow(CustomComboBoxes.CanvasManager cm)
    {
        var wh = new ManualResetEvent(false);
        // (as in WPF, this Window is never shown; Launch/Stop just Close() it)
        var window = new Window { Title = "Co-Op Host Settings", Width = 400.0, Height = 300.0 };
        var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10.0) };

        // Apply modal style
        // (WPF: implicit styles keyed by type in e.Resources; Avalonia: styles in e.Styles, which apply to its descendants)
        void AddStyle(Control e)
        {
            var textboxStyle = new Style(x => x.OfType<TextBox>());
            textboxStyle.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(0.0)));
            textboxStyle.Setters.Add(new Setter(TextBox.BorderBrushProperty, Brushes.DarkGray));
            textboxStyle.Setters.Add(new Setter(TextBox.FontSizeProperty, 16.0));
            textboxStyle.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.Orange));
            textboxStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, Brushes.Black));
            e.Styles.Add(textboxStyle);

            var checkboxStyle = new Style(x => x.OfType<CheckBox>());
            checkboxStyle.Setters.Add(new Setter(CheckBox.HeightProperty, 22.0));
            checkboxStyle.Setters.Add(new Setter(TemplatedControl.TemplateProperty, Graphics.roundedCheckBoxTemplate));
            checkboxStyle.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(4.0, 0.0, 0.0, 0.0)));
            e.Styles.Add(checkboxStyle);
        }

        AddStyle(sp);

        var isSessionEnabled = TrackerModelOptions.CoopHostSession;

        var enableCheck = new CheckBox { Content = new TextBox { Text = "Enable Hosting", IsReadOnly = true } };
        enableCheck.IsChecked = isSessionEnabled;
        Graphics.onChecked(enableCheck, () => isSessionEnabled = true);
        Graphics.onUnchecked(enableCheck, () => isSessionEnabled = false);
        sp.Children.Add(enableCheck);

        var portLabel = new TextBox { Text = "Port Number:", IsReadOnly = true };
        sp.Children.Add(portLabel);

        var portBox = new TextBox { Text = TrackerModelOptions.PortNumber };
        portBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        portBox.TextAlignment = TextAlignment.Left;

        var portBoxBorder = new Border
        {
            Child = portBox,
            BorderBrush = Brushes.Orange,
            BorderThickness = new Thickness(2.0),
            Margin = new Thickness(0.0, 2.0, 0.0, 6.0)
        };
        sp.Children.Add(portBoxBorder);

        var portValidationMessage = new TextBlock
        {
            Text = "Port must be a number between 1024 and 65535",
            Foreground = Brushes.Red,
            IsVisible = false,
            Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
        };
        sp.Children.Add(portValidationMessage);

        var launchButton = Graphics.makeButton("Launch", null, null);
        launchButton.HorizontalAlignment = HorizontalAlignment.Left;

        launchButton.Click += (_, _) =>
        {
            var port = portBox.Text.Trim();
            TrackerModelOptions.PortNumber = port;
            if (!isSessionEnabled)
            {
                isSessionEnabled = true;
                enableCheck.IsChecked = true;
            }
            TrackerModelOptions.CoopHostSession = true;
            Dungeon.startLocalSignalRHost(port);   // (the host first: turning co-op on connects to it straight away)
            TrackerModelOptions.CoopSyncOptions.SetEnableCoop(true);
            wh.Set();
            window.Close();
        };

        var stopButton = Graphics.makeButton("Stop", null, null);
        stopButton.HorizontalAlignment = HorizontalAlignment.Left;

        stopButton.Click += (_, _) =>
        {
            Dungeon.stopLocalSignalRHost();
            if (isSessionEnabled)
            {
                isSessionEnabled = false;
                enableCheck.IsChecked = false;
            }
            TrackerModelOptions.CoopHostSession = false;
            wh.Set();
            window.Close();
        };

        sp.Children.Add(launchButton);
        sp.Children.Add(stopButton);

        var stylePanel = new Border
        {
            Child = sp,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(3.0),
            Background = Brushes.Black,
            Width = 400.0,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        void validatePort()
        {
            var text = portBox.Text.Trim();
            var valid = false;
            if (int.TryParse(text, out var port) && port >= 1024 && port <= 65535)
                valid = true;
            portBoxBorder.BorderBrush = valid ? Brushes.Orange : Brushes.Red;
            portValidationMessage.IsVisible = !valid;
            enableCheck.IsEnabled = valid;
            launchButton.IsEnabled = valid && (bool)enableCheck.IsChecked;
        }

        portBox.TextChanged += (_, _) => validatePort();
        Graphics.onChecked(enableCheck, () => validatePort());
        Graphics.onUnchecked(enableCheck, () => validatePort());

        validatePort();

        CustomComboBoxes.StartImmediate(((Func<Task>)(async () =>
        {
            // (only clear the flag if this dialog set it: inside the in-app Options popup it is already set, and clearing it let hotkeys act behind Options)
            var changedGlobal = !CustomComboBoxes.GlobalFlag.popupIsActive;
            CustomComboBoxes.GlobalFlag.popupIsActive = true;
            await CustomComboBoxes.DoModalDocked(cm, wh, Dock.Bottom, stylePanel);
            if (changedGlobal)
                CustomComboBoxes.GlobalFlag.popupIsActive = false;
        }))());
    }





    // ---------------- Guided Tour ----------------
    // A step-by-step tour: the screen is dimmed except one section, which is outlined and described. Sections are found by the text of the labels in them
    // (so no screen has to hand its controls over): each step is (label texts, title, description); the highlight covers all of the labels it finds.
    private static string tourTextOf(Visual d)
    {
        switch (d)
        {
            case TextBox t: return t.Text;
            case TextBlock t: return t.Text;
            default: return null;
        }
    }
    private static Control tourFind(Visual root, string pattern)   // (returns null for None)
    {
        var pat = pattern.Trim();
        Control exact = null;
        Control prefix = null;
        void walk(Visual d)
        {
            if (exact == null)
            {
                var t = tourTextOf(d);
                if (t != null)
                {
                    var fe = (Control)d;
                    // (WPF: IsVisible, which is false when an ancestor is hidden too, so Avalonia's IsEffectivelyVisible; ActualWidth → Bounds.Width)
                    if (fe.IsEffectivelyVisible && fe.Bounds.Width > 0.0)
                    {
                        var tt = t.Trim();
                        if (tt == pat) exact = fe;
                        else if (prefix == null && tt.StartsWith(pat)) prefix = fe;
                    }
                }
                foreach (var k in d.GetVisualChildren())
                    walk(k);
            }
        }
        walk(root);
        return exact ?? prefix;
    }
    // a label inside a button or checkbox: use the whole control (so the checkbox square is inside the highlight)
    // (WPF: Primitives.ButtonBase; in Avalonia, CheckBox, ToggleButton, RadioButton and RepeatButton all derive from Button)
    private static Control tourWholeControl(Control fe)
    {
        Control up(Visual d, int n)
        {
            if (d == null || n > 5) return fe;
            else if (d is Button b) return b;
            else return up(d.GetVisualParent(), n + 1);
        }
        return up(fe.GetVisualParent(), 0);
    }
    // the in-app Options popup closes itself (set by the main window) so the main window tour can start
    public static bool optionsTourRequested = false;
    public static Action closeOptionsPopupHook = () => { };
    // the start screen tour ends by starting a run, then the main window tour follows (the hook starts the run; Program.fs sets it)
    public static Action startBlankRunHook = () => { };
    public static bool mainTourAfterStart = false;
    // the start screen keeps its settings in a panel that flies up from the bottom: the tour opens it first (Program.fs sets the hook)
    public static Action<bool> showStartOptionsHook = _ => { };
    public static Action<string> tourStepEnterHook = _ => { };   // called with a step's title as the tour shows it (the start screen uses it to raise or lower the settings panel)
    // a step can demo its point on the live tracker: returns an undo that puts things back when the tour leaves the step (null for None)
    private static Action tourDemo(string title)
    {
        if (title == "Triforce in the basement")
        {
            // use the first dungeon whose item boxes are all empty, so a real run is never disturbed (dungeon 1 first, whose basement slot is easy to see)
            for (int i = 0; i <= 7; i++)
            {
                var d = TrackerModel.GetDungeon(i);
                if (d.Boxes.All(bx => bx.CellCurrent() == -1))
                {
                    // the box that shows the basement staircase (else the last one)
                    var b = d.Boxes.FirstOrDefault(x => x.CurrentlyHasBasementStair) ?? d.Boxes[d.Boxes.Length - 1];
                    var (oldCell, oldHas) = (b.CellCurrent(), b.PlayerHas());
                    b.Set(TrackerModel.ITEMS.TRIFORCE, TrackerModel.PlayerHas.NO);
                    return () => b.Set(oldCell, oldHas);
                }
            }
            return null;
        }
        else return null;
    }
    // (continueLabel: when not empty, the last step's button has this label and, once clicked, onContinue runs after the tour closes)
    public static void runGuidedTourCore(CustomComboBoxes.CanvasManager cm, IEnumerable<(string[] labels, string title, string body)> steps, string continueLabel, Action onContinue)
    {
        if (!CustomComboBoxes.GlobalFlag.popupIsActive)
        {
            var foundList = new List<(Rect, string, string)>();
            foreach (var (pats, title, body) in steps)
            {
                var rects = new List<Rect>();
                foreach (var p in pats)
                {
                    // "@x0,y0,x1,y1" is a region given as fractions of the whole window (for parts of the screen that have no label text)
                    if (p.StartsWith("@"))
                    {
                        try
                        {
                            var v = p.Substring(1).Split(',').Select(t => double.Parse(t, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                            var (cw, ch) = (cm.AppMainCanvas.Width, cm.AppMainCanvas.Height);
                            rects.Add(new Rect(v[0] * cw, v[1] * ch, (v[2] - v[0]) * cw, (v[3] - v[1]) * ch));
                        }
                        catch { }
                    }
                    else
                    {
                        var fe = tourFind(cm.AppMainCanvas, p);
                        if (fe != null)
                        {
                            var w = tourWholeControl(fe);
                            // (WPF: TransformToAncestor(...).TransformBounds, which throws when w isn't under the canvas; Avalonia returns null then)
                            try
                            {
                                var t = w.TransformToVisual(cm.AppMainCanvas);
                                if (t.HasValue)
                                    rects.Add(new Rect(0.0, 0.0, w.Bounds.Width, w.Bounds.Height).TransformToAABB(t.Value));
                            }
                            catch { }
                        }
                    }
                }
                if (rects.Count > 0)
                    foundList.Add((rects.Aggregate((a, b) => a.Union(b)), title, body));
            }
            var found = foundList.ToArray();
            if (found.Length > 0)
            {
                CustomComboBoxes.GlobalFlag.popupIsActive = true;
                CustomComboBoxes.StartImmediate(((Func<Task>)(async () =>
                {
                    var continued = false;
                    Action undoDemo = () => { };
                    (Action next, Action back, Action skip) keyActions = (() => { }, () => { }, () => { });
                    // while the tour is up, the keyboard drives the tour and nothing reaches the controls behind it (Enter used to press whatever button had focus)
                    var keyWindow = TopLevel.GetTopLevel(cm.AppMainCanvas);
                    EventHandler<Avalonia.Input.KeyEventArgs> keyHandler = (_, ea) =>
                    {
                        switch (ea.Key)
                        {
                            case Avalonia.Input.Key.Enter:
                            case Avalonia.Input.Key.Right: keyActions.next(); break;
                            case Avalonia.Input.Key.Left: keyActions.back(); break;
                            case Avalonia.Input.Key.Escape: keyActions.skip(); break;
                        }
                        ea.Handled = true;
                    };
                    keyWindow?.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, keyHandler, Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
                    try
                    {
                        var wh = new ManualResetEvent(false);
                        var (W, H) = (cm.AppMainCanvas.Width, cm.AppMainCanvas.Height);
                        var overlay = new Canvas { Width = W, Height = H, Background = Brushes.Transparent };
                        var idx = 0;
                        Button mkButton(string text) =>
                            new Button { Content = text, Margin = new Thickness(3.0), Padding = new Thickness(10.0, 2.0, 10.0, 2.0), Focusable = false, FontSize = 15.0, Background = Brushes.Black, Foreground = Brushes.Orange, BorderBrush = Brushes.Orange };
                        void show()
                        {
                            overlay.Children.Clear();
                            var (r, title, body) = found[idx];
                            undoDemo();
                            tourStepEnterHook(title);
                            undoDemo = tourDemo(title) ?? (() => { });
                            var pad = 5.0;
                            var (hx, hy) = (Math.Max(0.0, r.X - pad), Math.Max(0.0, r.Y - pad));
                            var hole = new Rect(hx, hy, Math.Min(W - hx, r.Width + 2.0 * pad), Math.Min(H - hy, r.Height + 2.0 * pad));
                            var geo = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0.0, 0.0, W, H)), new RectangleGeometry(hole));
                            overlay.Children.Add(new Path { Data = geo, Fill = Brushes.Black, Opacity = 0.75, IsHitTestVisible = false });
                            void addPicture(string file, double iw, double ih, (double x, double y, double w, double h)[] boxes)
                            {
                                // a real screenshot of a menu, scaled to fit the highlighted area, with the parts the stop talks about outlined
                                var s = Math.Min(r.Width / iw, r.Height / ih);
                                var (ox, oy) = (r.X + (r.Width - iw * s) / 2.0, r.Y + (r.Height - ih * s) / 2.0);
                                var pic = Graphics.BMPtoImage(Graphics.loadBMP(file));
                                pic.Width = iw * s; pic.Height = ih * s; pic.Stretch = Stretch.Fill;
                                RenderOptions.SetBitmapInterpolationMode(pic, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
                                Canvas.SetLeft(pic, ox); Canvas.SetTop(pic, oy);
                                overlay.Children.Add(pic);
                                foreach (var (x, y, w, h) in boxes)
                                {
                                    var box = new Rectangle { Width = w * s, Height = h * s, Stroke = Brushes.Orange, StrokeThickness = 3.0, RadiusX = 3.0, RadiusY = 3.0, IsHitTestVisible = false };
                                    Canvas.SetLeft(box, ox + x * s); Canvas.SetTop(box, oy + y * s);
                                    overlay.Children.Add(box);
                                }
                            }
                            if (title == "Placing shops") addPicture("tile-menu-tour.png", 500.0, 370.0, new[] { (17.0, 100.0, 322.0, 48.0), (49.0, 234.0, 42.0, 46.0) });
                            if (title == "Moving and resizing menus") addPicture("menu-move-tour.png", 580.0, 420.0, new[] { (72.0, 50.0, 308.0, 16.0), (526.0, 64.0, 28.0, 178.0), (36.0, 290.0, 36.0, 38.0), (272.0, 20.0, 112.0, 26.0) });
                            var frame = new Rectangle { Width = hole.Width, Height = hole.Height, Stroke = Brushes.Orange, StrokeThickness = 3.0, RadiusX = 4.0, RadiusY = 4.0, IsHitTestVisible = false };
                            Canvas.SetLeft(frame, hole.X);
                            Canvas.SetTop(frame, hole.Y);
                            overlay.Children.Add(frame);
                            var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10.0) };
                            sp.Children.Add(new TextBlock { Text = title, Foreground = Brushes.Orange, FontSize = 20.0, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
                            sp.Children.Add(new TextBlock { Text = body, Foreground = Brushes.White, FontSize = 15.0, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0.0, 6.0, 0.0, 8.0) });
                            var buttons = new DockPanel { LastChildFill = false };
                            var counter = new TextBlock { Text = $"{idx + 1} / {found.Length}", Foreground = Brushes.Gray, FontSize = 14.0, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0.0, 0.0, 12.0, 0.0) };
                            var skipBtn = mkButton(idx == found.Length - 1 ? " Close " : " Skip tour ");
                            var backBtn = mkButton(" Back ");
                            var nextBtn = mkButton(idx == found.Length - 1 ? (continueLabel != "" ? continueLabel : " Done ") : " Next ");
                            backBtn.IsEnabled = idx > 0;
                            if (idx == found.Length - 1 && continueLabel == "") skipBtn.IsVisible = false;   // (the last stop: Done already closes it, so no second Close)
                            void doSkip() => wh.Set();
                            void doBack() { if (idx > 0) { idx = idx - 1; show(); } }
                            void doNext()
                            {
                                if (idx >= found.Length - 1)
                                {
                                    if (continueLabel != "") continued = true;
                                    wh.Set();
                                }
                                else
                                {
                                    idx = idx + 1;
                                    show();
                                }
                            }
                            keyActions = (doNext, doBack, doSkip);
                            skipBtn.Click += (_, _) => doSkip();
                            backBtn.Click += (_, _) => doBack();
                            nextBtn.Click += (_, _) => doNext();
                            DockPanel.SetDock(counter, Dock.Left);
                            DockPanel.SetDock(nextBtn, Dock.Right);
                            DockPanel.SetDock(backBtn, Dock.Right);
                            DockPanel.SetDock(skipBtn, Dock.Right);
                            buttons.Children.Add(counter);
                            buttons.Children.Add(nextBtn);
                            buttons.Children.Add(backBtn);
                            buttons.Children.Add(skipBtn);
                            sp.Children.Add(buttons);
                            var callout = new Border { Child = sp, Background = Brushes.Black, BorderBrush = Brushes.Orange, BorderThickness = new Thickness(3.0), Width = 430.0 };
                            callout.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                            var (cw, ch) = (callout.DesiredSize.Width, callout.DesiredSize.Height);
                            double clampX(double x) => Math.Max(0.0, Math.Min(W - cw, x));
                            double clampY(double y) => Math.Max(0.0, Math.Min(H - ch, y));
                            var gap = 12.0;
                            // below the highlight if it fits, else above, else beside it, else at the roomier end of the screen
                            var (cx, cy) =
                                hole.Bottom + gap + ch <= H ? (clampX(hole.X), hole.Bottom + gap)
                                : hole.Top - gap - ch >= 0.0 ? (clampX(hole.X), hole.Top - gap - ch)
                                : hole.Right + gap + cw <= W ? (hole.Right + gap, clampY(hole.Y))
                                : hole.Left - gap - cw >= 0.0 ? (hole.Left - gap - cw, clampY(hole.Y))
                                : (clampX(hole.X), (hole.Top > H - hole.Bottom ? 0.0 : H - ch));
                            Canvas.SetLeft(callout, cx);
                            Canvas.SetTop(callout, cy);
                            overlay.Children.Add(callout);
                        }
                        show();
                        await CustomComboBoxes.DoModalOpa(cm, wh, 0.0, 0.0, overlay, 0.0);
                    }
                    finally
                    {
                        keyWindow?.RemoveHandler(Avalonia.Input.InputElement.KeyDownEvent, keyHandler);
                        undoDemo();
                        CustomComboBoxes.GlobalFlag.popupIsActive = false;
                    }
                    if (continued) onContinue();
                }))());
            }
        }
    }
    public static void runGuidedTour(CustomComboBoxes.CanvasManager cm, IEnumerable<(string[] labels, string title, string body)> steps)
    {
        runGuidedTourCore(cm, steps, "", () => { });
    }

    // shown when the overworld comes up with a ZORA extra set to Random: the screen is dimmed except the Overworld + Extras button, with a note beside it.
    // "Don't show again" runs dontShowAgain (which saves that choice).
    public static void showRandomExtrasPointer(CustomComboBoxes.CanvasManager cm, Action dontShowAgain)
    {
        if (!CustomComboBoxes.GlobalFlag.popupIsActive)
        {
            var fe = tourFind(cm.AppMainCanvas, "Overworld + Extras");
            if (fe == null) return;
            var w = tourWholeControl(fe);
            // (WPF: TransformToAncestor(...).TransformBounds, an empty Rect when it throws; Avalonia returns null when w isn't under the canvas)
            var r = new Rect(0.0, 0.0, 0.0, 0.0);
            try
            {
                var t = w.TransformToVisual(cm.AppMainCanvas);
                if (t.HasValue) r = new Rect(0.0, 0.0, w.Bounds.Width, w.Bounds.Height).TransformToAABB(t.Value);
            }
            catch { }
            if (r.Width > 0.0)
            {
                CustomComboBoxes.GlobalFlag.popupIsActive = true;
                CustomComboBoxes.StartImmediate(((Func<Task>)(async () =>
                {
                    var dont = false;
                    try
                    {
                        var wh = new ManualResetEvent(false);
                        var (W, H) = (cm.AppMainCanvas.Width, cm.AppMainCanvas.Height);
                        var overlay = new Canvas { Width = W, Height = H, Background = Brushes.Transparent };
                        var pad = 5.0;
                        var (hx, hy) = (Math.Max(0.0, r.X - pad), Math.Max(0.0, r.Y - pad));
                        var hole = new Rect(hx, hy, Math.Min(W - hx, r.Width + 2.0 * pad), Math.Min(H - hy, r.Height + 2.0 * pad));
                        var geo = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0.0, 0.0, W, H)), new RectangleGeometry(hole));
                        overlay.Children.Add(new Path { Data = geo, Fill = Brushes.Black, Opacity = 0.75, IsHitTestVisible = false });
                        var frame = new Rectangle { Width = hole.Width, Height = hole.Height, Stroke = Brushes.Orange, StrokeThickness = 3.0, RadiusX = 4.0, RadiusY = 4.0, IsHitTestVisible = false };
                        Canvas.SetLeft(frame, hole.X);
                        Canvas.SetTop(frame, hole.Y);
                        overlay.Children.Add(frame);
                        Button mkButton(string text) =>
                            new Button { Content = text, Margin = new Thickness(3.0), Padding = new Thickness(10.0, 2.0, 10.0, 2.0), Focusable = false, FontSize = 15.0, Background = Brushes.Black, Foreground = Brushes.Orange, BorderBrush = Brushes.Orange };
                        var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10.0) };
                        sp.Children.Add(new TextBlock { Text = "Turn Extras on/off once you confirm.", Foreground = Brushes.White, FontSize = 17.0, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0.0, 0.0, 0.0, 8.0) });
                        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                        var okBtn = mkButton(" OK ");
                        var dontBtn = mkButton(" Don't show again ");
                        okBtn.Click += (_, _) => wh.Set();
                        dontBtn.Click += (_, _) => { dont = true; wh.Set(); };
                        buttons.Children.Add(dontBtn);
                        buttons.Children.Add(okBtn);
                        sp.Children.Add(buttons);
                        var callout = new Border { Child = sp, Background = Brushes.Black, BorderBrush = Brushes.Orange, BorderThickness = new Thickness(3.0), Width = 330.0 };
                        callout.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        var (cw, ch) = (callout.DesiredSize.Width, callout.DesiredSize.Height);
                        var gap = 12.0;
                        var cx = Math.Max(0.0, Math.Min(W - cw, hole.Right - cw));   // (lined up under the right end of the button)
                        var cy = hole.Bottom + gap + ch <= H ? hole.Bottom + gap : Math.Max(0.0, hole.Top - gap - ch);
                        Canvas.SetLeft(callout, cx);
                        Canvas.SetTop(callout, cy);
                        overlay.Children.Add(callout);
                        await CustomComboBoxes.DoModalOpa(cm, wh, 0.0, 0.0, overlay, 0.0);
                    }
                    finally
                    {
                        CustomComboBoxes.GlobalFlag.popupIsActive = false;
                    }
                    if (dont) dontShowAgain();
                }))());
            }
        }
    }

    // the main window tour. ("@x0,y0,x1,y1" regions are fractions of the window, for parts with no label text)
    public static readonly List<(string[] labels, string title, string body)> mainScreenTourSteps = new List<(string[], string, string)> {
        (new[] { "@0.0,0.0,0.358,0.063" }, "Triforce and levels",
            "The dungeon level numbers and triforce pieces along the top. Mark each dungeon as you finish it."),
        (new[] { "@0.0,0.063,0.358,0.154" }, "Dungeon item boxes",
            "One row of boxes for each dungeon, holding what you found there. Click a box to set its item; a green border means collected."),
        (new[] { "@0.2,0.2,0.8,0.62" }, "Moving and resizing menus",
            "Menus can be moved and resized. On the item and tile menus, first click Jump to mouse at the top of the menu. Then click the LOCKED strip beside the tip box to unlock it, drag the bar on top to move the menu, and drag the corner grip at the bottom left to resize it. Click UNLOCKED to lock it again. Your layout is remembered. If you don't see the LOCKED strip, uncheck Hide Lock And Hints in Options, Dungeon Settings."),
        (new[] { "@0.0,0.0,0.358,0.154" }, "Triforce in the basement",
            "Some seeds hide a dungeon's Triforce Piece in a basement. Click an item box in a dungeon and choose Triforce Piece to put it there. The numbered triforce moves into that box. This means you'll have an extra floor item to make up for it."),
        (new[] { "@0.363,0.03,0.67,0.175" }, "Items and hearts",
            "The items you have found, your hearts, and Max Hearts. Click to mark what you own."),
        (new[] { "@0.572,0.004,0.684,0.13" }, "Counters",
            "Overworld spots left to check, and how many spots still need each item (hover an icon, such as Burn, to see them outlined on the map), plus the open spots."),
        (new[] { "Popout Windows", "Overworld + Extras" }, "Tools",
            "Popout Windows opens extra windows, Hint Decoder helps with hints, Hotkeys lists the keyboard shortcuts, Log shows the reminders that have played, Draw/Custom Icons lets you draw on the map, and Overworld + Extras sets the quest and block options."),
        (new[] { "TIMER", "Save" }, "Timer and file",
            "Pause/Reset timer controls the run clock. Options opens the settings (and this tour). Save writes a save file you can load later."),
        (new[] { "@0.0,0.154,1.0,0.43" }, "Overworld map",
            "Left click a tile to mark it done (nothing there): OW Spots Left goes down by one. Right click a tile (or left click it again) to open the marker menu: caves, shops, dungeon entrances and more. Fill in the details as you explore."),
        (new[] { "Any Road", "Lost Hills" }, "Marker legend",
            "The special spots you can place on the map: Any Road (warp), completed dungeons, the recorder destination, your Start Spot. Enter your Dead Woods/Lost Hills hints."),
        (new[] { "@0.2,0.2,0.8,0.62" }, "Placing shops",
            "Right click a map tile to open this menu. The SHOP button at the bottom picks a potion shop or shop 1 to 4, and puts its name at the top of the tile. The outlined row holds the other spots to place: secrets, door repair, money making game, armos, take any and the hint shop."),
        (new[] { "@0.0,0.467,0.726,0.495" }, "Item Progress",
            "A quick view of the items and upgrades you have collected so far."),
        (new[] { "@0.0,0.496,0.586,0.517" }, "Dungeon tabs",
            "Tabs for dungeons 1 to 9, and S for the summary. Click one to open that dungeon's map. 1Q and 2Q overlay the vanilla first or second quest outline on the dungeon map (click again to hide it)."),
        (new[] { "@0.0,0.517,0.586,0.867" }, "Dungeon boards",
            "A small map of each dungeon. Hover to preview a dungeon's tab, and click to switch to it."),
        (new[] { "@0.591,0.496,1.0,0.607" }, "Blockers",
            "Note what is blocking each dungeon, so you remember what you still need."),
        (new[] { "Notes", "Aa" }, "Notes",
            "Free text notes for the run. The Aa button changes the text size."),
        (new[] { "@0.0,0.873,1.0,1.0" }, "Timeline",
            "The Overworld and Collection Timeline: when you found each item and how the overworld filled in over the run, with the seed and flags along the top (when Display Seed + Flags is on).") };

    // the start screen tour: (labels that mark the section, title, description)
    public static readonly List<(string[] labels, string title, string body)> startScreenTourSteps = new List<(string[], string, string)> {
        (new[] { "1st Quest", "Other..." }, "Start a new run",
            "Click the overworld your seed uses to begin a new run. The checkboxes below are set before you start."),
        (new[] { "Item Shuffle", "Off", "Full" }, "Item Shuffle",
            "Set this to match the seed's Item Shuffle: Off (dungeon items stay in dungeons), Intra (items anywhere, hearts within dungeons) or Full (items and hearts)."),
        (new[] { "Legacy Mode", "Hide Dungeon Numbers", "Mirrored Overworld", "Mark Bomb Droppers" }, "Startup options",
            "Check the ones your seed uses. Legacy Mode is for non-ZORA seeds: it turns off and locks the ZORA Extras and limits shops to the original game's items, Hide Dungeon Numbers hides the level numbers, Mirrored Overworld flips the map and Mark Bomb Droppers lets you mark bomb-dropper spots."),
        (new[] { "ZORA Extras", "Extra Power Bracelet Blocks", "Progressive Items", "Custom LEVEL Name" }, "ZORA Extras",
            "Extra randomizer options that only some seeds use, such as 4th Sword, Pegasus Boots and Progressive Items. Check the ones your seed uses. Custom LEVEL Name, at the bottom, replaces \"LEVEL\" in the dungeon headers with a name of your choice."),
        (new[] { "Paste Flags" }, "Paste Flags",
            "Copy the Z1R and ZORA flag strings from the ZORA site, paste them in, then click Apply to set the options and close the box (pasting alone sets them too). The matching options are checked for you, and Boots and the four block and hint options show a ? (Random) when the flags left them to chance."),
        (new[] { "@0.021,0.744,0.979,0.844" }, "Recent saves",
            "Your three most recently loaded save files, newest first. Click one to resume that run."),
        (new[] { "Load Save" }, "Load a save",
            "Resume an earlier run from any save file on your computer."),
        (new[] { "Overworld Settings", "Use Old Popups" }, "Overworld settings",
            "How the map behaves: route drawing, highlighting nearby tiles, the magnifier and popups. Most of these can be changed later from the Options button."),
        (new[] { "Dungeon Settings", "Hide Lock And Hints" }, "Dungeon settings",
            "How the dungeon screens behave and look: hint icons, BOARD instead of LEVEL, basement info, door inference and more."),
        (new[] { "Reminders", "Change Voice" }, "Reminders",
            "Volume, which voice and visual reminders you get (dungeon feedback, sword hearts, coast item, a check of Overworld + Extras when an option is Random, and more), how often they repeat, and which voice speaks them."),
        (new[] { "@0.31,0.905,0.67,0.99" }, "Auto-save",
            "How often the tracker saves your run by itself, in minutes."),
        (new[] { "Other", "Race Mode" }, "Other options",
            "General settings: animations, remembering your startup choices, update checks, Display Seed + Flags, listening for speech, the broadcast window, the timer and Race Mode."),
        (new[] { "Co-Op Client Settings", "Show Crash Log..." }, "Co-Op and crash log",
            "Co-Op Client and Host settings are for tracking together with a partner. Show Crash Log opens the log file to send along when reporting a problem."),
        (new[] { "Guided Tour", "v2." }, "Tour, options and version",
            "Guided Tour starts this walkthrough again. Options opens and closes the settings panel. The version button shows the version, with Check for updates, Open folder and Go to website, and glows green when an update is available. Click Continue to start a new run (1st Quest) and tour the main screen.") };

    // the titles of the start screen tour stops that would be left out because none of their labels is on screen (a stop with no anchor is skipped silently); used by the self-test
    public static List<string> startScreenTourMissingStops(Visual root) =>
        startScreenTourSteps.Where(s => !s.labels.Any(p => p.StartsWith("@") || tourFind(root, p) != null)).Select(s => s.title).ToList();

    // the start screen tour raises the settings panel for the steps about it and lowers it for the rest (so it never hides the part being shown)
    // (F#: a module-level `do`, run when the module is first used; here a static constructor, which runs before any of OptionsMenu is used)
    static OptionsMenu()
    {
        var panelSteps = new[] { "Overworld settings", "Dungeon settings", "Reminders", "Auto-save", "Other options", "Co-Op and crash log" };
        tourStepEnterHook = title =>
        {
            if (startScreenTourSteps.Any(st => st.title == title))
                showStartOptionsHook(panelSteps.Contains(title));
        };
    }

    // the Guided Tour button: in the in-app Options popup (includePopupExplainer) it runs the main window tour, on the start screen it runs the start screen tour
    private static readonly System.Collections.Generic.List<Action> allTourButtonStops = new();
    public static Button makeGuidedTourButton(CustomComboBoxes.CanvasManager cm, bool includePopupExplainer)
    {
        var tourButton = Graphics.makeButton(" Guided Tour ", null, null);
        tourButton.HorizontalAlignment = HorizontalAlignment.Left;
        tourButton.Margin = new Thickness(0.0, 6.0, 0.0, 0.0);
        // pulses until the first click so new users notice it
        // (WPF: a DoubleAnimation of the glow's BlurRadius, 6 to 30 over 0.8s, reversing, forever; tweened by hand here, as elsewhere in the port)
        DispatcherTimer pulse = null;
        if (!TrackerModelOptions.GuidedTourSeen)
        {
            var glow = new DropShadowDirectionEffect { ShadowDepth = 0.0, BlurRadius = 30.0, Opacity = 1.0, Color = Colors.Orange };
            var start = DateTime.Now;
            pulse = new DispatcherTimer(TimeSpan.FromMilliseconds(16.0), DispatcherPriority.Render, (_, _) =>
            {
                var p = (DateTime.Now - start).TotalSeconds % 1.6;
                var f = p < 0.8 ? p / 0.8 : (1.6 - p) / 0.8;
                glow.BlurRadius = 6.0 + 24.0 * f;
            });
            pulse.Start();
            tourButton.Effect = glow;
        }
        allTourButtonStops.Add(() => { pulse?.Stop(); tourButton.Effect = null; });   // (the pulse stops on every Guided Tour button, start screen and Options panel, at the first click)
        tourButton.Click += (_, _) =>
        {
            if (!TrackerModelOptions.GuidedTourSeen)
            {
                TrackerModelOptions.GuidedTourSeen = true;
                TrackerModelOptions.writeSettings();
            }
            foreach (var stop in allTourButtonStops) stop();
        };
        if (includePopupExplainer)
            tourButton.Click += (_, _) =>
            {   // (closes this menu first, then the main window tour starts)
                optionsTourRequested = true;
                closeOptionsPopupHook();
            };
        else
            tourButton.Click += (_, _) =>
            {
                showStartOptionsHook(true);
                runGuidedTourCore(cm, startScreenTourSteps, " Continue to main screen ", () =>
                {
                    mainTourAfterStart = true;
                    startBlankRunHook();
                });
            };
        return tourButton;
    }
    public static Border makeOptionsCanvas(CustomComboBoxes.CanvasManager cm, bool includePopupExplainer, bool isStandardHyrule)
    {
        var width = cm.AppMainCanvas.Width;
        var all = new Border { BorderThickness = new Thickness(2.0), BorderBrush = Brushes.DarkGray, Background = Brushes.Black };
        // (no fixed width: the three columns take the width their labels need, and the Viewbox below shrinks them to fit the window if they need more.
        // A fixed width clipped the last column's labels where text is wider, e.g. "Remember Startup Selection" in the browser build.)
        var optionsAllsp = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Black };
        // (WPF: implicit styles keyed by type in e.Resources; Avalonia: styles in e.Styles, which apply to its descendants)
        void AddStyle(Control e)
        {
            var style = new Style(x => x.OfType<TextBox>());
            style.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(0.0)));
            style.Setters.Add(new Setter(TextBox.BorderBrushProperty, Brushes.DarkGray));
            style.Setters.Add(new Setter(TextBox.FontSizeProperty, 16.0));
            style.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.Orange));
            style.Setters.Add(new Setter(TextBox.BackgroundProperty, Brushes.Black));
            style.Setters.Add(new Setter(Layoutable.MinHeightProperty, 0.0));   // (WPF metrics: no minimum size, a hairline of padding)
            style.Setters.Add(new Setter(Layoutable.MinWidthProperty, 0.0));
            style.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(2.0, 0.0, 2.0, 0.0)));
            e.Styles.Add(style);
            var style3 = new Style(x => x.OfType<Button>());
            Graphics.addRoundedButtonSetters(style3);   // (the rounded dark look, like every other button)
            e.Styles.Add(style3);
            var style2 = new Style(x => x.OfType<CheckBox>());
            style2.Setters.Add(new Setter(Layoutable.MinHeightProperty, 0.0));
            style2.Setters.Add(new Setter(CheckBox.HeightProperty, 22.0));
            style2.Setters.Add(new Setter(TemplatedControl.TemplateProperty, Graphics.roundedCheckBoxTemplate));
            style2.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(4.0, 0.0, 0.0, 0.0)));
            e.Styles.Add(style2);
        }
        AddStyle(all);

        TextBox header(TextBox tb)
        {
            tb.Margin = new Thickness(0.0, 0.0, 0.0, 6.0);
            tb.BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0);
            return tb;
        }
        var options1sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10.0, 0.0, 10.0, 0.0) };
        var tb = header(new TextBox { Text = "Overworld Settings", IsReadOnly = true, FontWeight = FontWeight.Bold });
        options1sp.Children.Add(tb);
        // Options marked as "advanced" only appear in the main-app options popup (not the startup screen),
        // to keep the startup screen compact and avoid layout overflow.
        var advancedOverworldOptions = new string[] { };   // (none right now) options listed here only appear in the main-app options popup, not the startup screen
        var advancedDungeonOptions = new string[] { };  // (none right now)
        var visibleOverworldOpts =
            data1o(isStandardHyrule)
            .Where(o => includePopupExplainer || !advancedOverworldOptions.Contains(o.text))
            .ToArray();
        foreach (var (text, tip, b, needFU, oe, marginOpt) in visibleOverworldOpts)
        {
            var cb = new CheckBox { Content = new TextBox { Text = text, IsReadOnly = true } };
            if (marginOpt.HasValue)
                cb.Margin = marginOpt.Value;
            ToolTip.SetTip(cb, tip);
            setToolTipShowDuration(cb, 10000);
            link(cb, b, needFU, oe);
            options1sp.Children.Add(cb);
        }
        options1sp.Children.Add(new DockPanel { Height = 17.0 });   // a blank row between the overworld settings and this header
        tb = header(new TextBox { Text = "Dungeon Settings", IsReadOnly = true, FontWeight = FontWeight.Bold });
        options1sp.Children.Add(tb);
        // open the folder containing the crash log (with the log file selected), to make it easy to send to the developer
        var crashLogButton = Graphics.makeButton(" Show Crash Log... ", null, null);
        crashLogButton.HorizontalAlignment = HorizontalAlignment.Left;
        crashLogButton.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
        ToolTip.SetTip(crashLogButton, "Open the folder containing Z1R_Tracker_crash_log.txt with the file selected,\nso you can send it along when reporting a problem");
        crashLogButton.Click += (_, _) =>
        {
            // (WPF: Explorer.exe on the exe folder; here the user-data folder, the exe folder on Windows, through Platform)
            try
            {
                var dir = Platform.UserDataDir();
                var file = Platform.Files.User("Z1R_Tracker_crash_log.txt");
                if (System.IO.File.Exists(file))
                    Platform.RevealInFileManager(file);
                else
                    Platform.OpenWithShell(dir);
            }
            catch { }
        };
        var visibleDungeonOpts =
            data1d
            .Where(o => includePopupExplainer || !advancedDungeonOptions.Contains(o.text))
            .ToArray();
        foreach (var (text, tip, b, needFU, oe) in visibleDungeonOpts)
        {
            var cb = new CheckBox { Content = new TextBox { Text = text, IsReadOnly = true } };
            ToolTip.SetTip(cb, tip);
            setToolTipShowDuration(cb, 10000);
            link(cb, b, needFU, oe);
            options1sp.Children.Add(cb);
            if (text == "Dungeon Map Hint")
            {
                var sliderSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4.0, 0.0, 0.0, 4.0) };
                var opacityLabel = new TextBox { Text = "Opacity", IsReadOnly = true };
                sliderSp.Children.Add(opacityLabel);
                var opacitySlider = new Slider { Orientation = Orientation.Horizontal, Minimum = 0.05, Maximum = 1.0,
                                                 TickFrequency = 0.05, TickPlacement = TickPlacement.BottomRight,
                                                 IsSnapToTickEnabled = true, Width = 100.0, Margin = new Thickness(4.0, 0.0, 0.0, 0.0) };
                opacitySlider.Value = TrackerModelOptions.DungeonMapLocationHintOpacity;
                opacitySlider.ValueChanged += (_, _) =>
                {
                    TrackerModelOptions.DungeonMapLocationHintOpacity = opacitySlider.Value;
                    triggerDisplayIconsInDungeonMapOptionChanged();
                };
                sliderSp.Children.Add(opacitySlider);
                sliderSp.IsEnabled = b.Value;
                sliderSp.Opacity = b.Value ? 1.0 : 0.4;
                Graphics.onChecked(cb, () => { sliderSp.IsEnabled = true; sliderSp.Opacity = 1.0; });
                Graphics.onUnchecked(cb, () => { sliderSp.IsEnabled = false; sliderSp.Opacity = 0.4; });
                options1sp.Children.Add(sliderSp);
            }
        }
        optionsAllsp.Children.Add(options1sp);

        //This section starts the reminders panel in the initial startup screen
        //The stack panel here is the middle column of content. Everything needs to be added to options2sp in order to render in this section. it is generally added top to bottom
        var options2sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10.0, 2.0, 10.0, 0.0) };
        tb = header(new TextBox { Text = "Reminders", IsReadOnly = true, FontWeight = FontWeight.Bold });
        //This is an example of how to add a text box to the middle column of content
        options2sp.Children.Add(tb);
        // volume and slider
        // This is a new StackPenel uniquely for the volume slider
        var options2Topsp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0.0, 0.0, 0.0, 6.0) };
        var volumeText = new TextBox { Text = "Volume", IsReadOnly = true, Margin = new Thickness(0.0) };
        options2Topsp.Children.Add(volumeText);
        // (WPF TickPlacement.Both → Avalonia TickPlacement.Outside, ticks on both sides)
        var slider = new Slider { Orientation = Orientation.Horizontal, Maximum = 100.0, TickFrequency = 10.0, TickPlacement = TickPlacement.Outside, IsSnapToTickEnabled = true, Width = 200.0 };
        slider.Value = (double)TrackerModelOptions.Volume;
        //The below is an important control for the UI. The slider.ValueChanged.Add(fun _ ->  is the event handler for the slider. It is triggered when the slider value changes.
        //The code inside the event handler is executed when the slider value changes.
        //This specifically keys to data that is part of the TrackerModelOptions class. ALSO the voice.Volume <- TrackerModelOptions.Volume is important for the voice to actually change.
        //The Graphics.volumeChanged.Trigger seems redundant but is important for the UI to update correctly.
        slider.ValueChanged += (_, _) =>
        {
            TrackerModelOptions.Volume = (int)slider.Value;
            Graphics.triggerVolumeChanged(TrackerModelOptions.Volume);
            if (!TrackerModelOptions.IsMuted)
                voice.Volume = TrackerModelOptions.Volume;
        };
        var dp = new DockPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0.0) };
        dp.Children.Add(slider);
        options2Topsp.Children.Add(dp);
        options2sp.Children.Add(options2Topsp);
        // stop
        var muteCB = new CheckBox { Content = new TextBox { Text = "Disable All", IsReadOnly = true } };
        ToolTip.SetTip(muteCB, "Turn off all reminders (but you can still view them by\nclicking the reminder log in the upper-right of the timeline)");
        muteCB.IsChecked = TrackerModelOptions.IsMuted;
        Graphics.onChecked(muteCB, () => { TrackerModelOptions.IsMuted = true; voice.Volume = 0; });
        Graphics.onUnchecked(muteCB, () => { TrackerModelOptions.IsMuted = false; voice.Volume = TrackerModelOptions.Volume; });
        options2sp.Children.Add(muteCB);
        // other settings
        var options2Grid = new Grid();
        for (int i = 1; i <= 3; i++)
            options2Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (int i = 0; i <= data2.Length; i++)
            options2Grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var voiceTB = new TextBox { Text = "Voice", IsReadOnly = true };
        ToolTip.SetTip(voiceTB, "The reminder will be spoken aloud");
        Graphics.gridAdd(options2Grid, voiceTB, 0, 0);
        var visualTB = new TextBox { Text = "Visual", IsReadOnly = true };
        ToolTip.SetTip(visualTB, "The reminder will be displayed as icons in the upper right of the Timeline");
        Graphics.gridAdd(options2Grid, visualTB, 1, 0);
        var row = 1;
        foreach (var (text, tip, bVoice, bVisual) in data2)
        {
            if (row % 2 == 1)
            {
                DockPanel backgroundColor() => new DockPanel { Background = Graphics.almostBlack };
                Graphics.gridAdd(options2Grid, backgroundColor(), 0, row);
                Graphics.gridAdd(options2Grid, backgroundColor(), 1, row);
                Graphics.gridAdd(options2Grid, backgroundColor(), 2, row);
            }
            var cbVoice = new CheckBox { HorizontalAlignment = HorizontalAlignment.Center };
            link(cbVoice, bVoice, false, () => { });
            Graphics.gridAdd(options2Grid, cbVoice, 0, row);
            var cbVisual = new CheckBox { HorizontalAlignment = HorizontalAlignment.Center };
            link(cbVisual, bVisual, false, () => { });
            Graphics.gridAdd(options2Grid, cbVisual, 1, row);
            var rtb = new TextBox { Text = text, IsReadOnly = true, Background = Brushes.Transparent };
            ToolTip.SetTip(rtb, tip);
            setToolTipShowDuration(rtb, 10000);
            Graphics.gridAdd(options2Grid, rtb, 2, row);
            row = row + 1;
        }

        options2sp.Children.Add(options2Grid);
        // Reminder interval slider (label row then slider row for compact layout)
        var reminderIntervalLabel = new TextBox { Text = $"Reminder Interval (Minutes): {TrackerModelOptions.ReminderIntervalMinutes}", IsReadOnly = true, Margin = new Thickness(0.0, 4.0, 0.0, 0.0), Background = Brushes.Transparent };
        options2sp.Children.Add(reminderIntervalLabel);
        var reminderIntervalSlider = new Slider { Minimum = 1.0, Maximum = 60.0, TickFrequency = 1.0, IsSnapToTickEnabled = true, Value = (double)TrackerModelOptions.ReminderIntervalMinutes, Width = 200.0, Margin = new Thickness(0.0, 0.0, 0.0, 2.0) };
        ToolTip.SetTip(reminderIntervalSlider, "How often periodic reminders fire (recorder/power-bracelet spots, boomstick, white sword)");
        reminderIntervalSlider.ValueChanged += (_, _) =>
        {
            var v = (int)reminderIntervalSlider.Value;
            TrackerModelOptions.ReminderIntervalMinutes = v;
            reminderIntervalLabel.Text = $"Reminder Interval (Minutes): {v}";
        };
        options2sp.Children.Add(reminderIntervalSlider);
        // (macOS/Linux: the speech stub lists no voices, so this button doesn't appear there yet; see Platform.SpeechSynthesizer)
        if (voice.GetInstalledVoices().Where(v => v.Enabled).Count() > 1)
        {
            var changeVoiceButton = Graphics.makeButton("Change Voice", null, null);
            changeVoiceButton.HorizontalAlignment = HorizontalAlignment.Left;
            {  // scope local mutable popupIsActive variable to this one button
                var popupIsActive = false;  // second level of popup, need local copy
                var changedGlobal = false;
                changeVoiceButton.Click += (_, _) =>
                {
                    if (!popupIsActive)
                    {
                        popupIsActive = true;
                        // the options menu is not popped up on the start screen, but is in the main app, so we need to make this consistent for both
                        if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                        {
                            CustomComboBoxes.GlobalFlag.popupIsActive = true;  // must be start screen
                            changedGlobal = true;
                        }
                        var wh = new ManualResetEvent(false);
                        var sp = new StackPanel { Orientation = Orientation.Vertical };
                        AddStyle(sp);
                        sp.Children.Add(new TextBox { Text = "Select preferred voice", IsReadOnly = true });
                        foreach (var v in voice.GetInstalledVoices())
                        {
                            if (v.Enabled)
                            {
                                var name = v.Name;
                                var r = new StackPanel { Orientation = Orientation.Horizontal };
                                r.Children.Add(new TextBox { Text = name, IsReadOnly = true, Width = 250.0 });
                                void testSpeech(Action f)   // ensure we test aloud
                                {
                                    var wasMuted = TrackerModelOptions.IsMuted;
                                    var wasVolumeOtherwiseZero = !wasMuted && TrackerModelOptions.Volume == 0;
                                    if (wasMuted || wasVolumeOtherwiseZero)
                                        voice.Volume = 30;  // default
                                    f();
                                    if (wasVolumeOtherwiseZero || wasMuted)
                                        voice.Volume = 0;
                                }
                                var sb = Graphics.makeButton("Test it", null, null);
                                sb.Click += (_, _) => testSpeech(() => { voice.SelectVoice(name); voice.Speak("Hello"); });
                                r.Children.Add(sb);
                                var sb2 = Graphics.makeButton("Choose this", null, null);
                                sb2.Click += (_, _) => testSpeech(() => { voice.SelectVoice(name); voice.Speak("Voice chosen"); TrackerModelOptions.PreferredVoice = name; wh.Set(); });
                                r.Children.Add(sb2);
                                sp.Children.Add(r);
                            }
                        }
                        CustomComboBoxes.StartImmediate(((Func<Task>)(async () =>
                        {
                            await CustomComboBoxes.DoModalDocked(cm, wh, Dock.Bottom, new Border { Child = sp, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(3.0), HorizontalAlignment = HorizontalAlignment.Center });
                            try
                            {
                                voice.SelectVoice(TrackerModelOptions.PreferredVoice);
                            }
                            catch
                            {
                                try
                                {
                                    voice.SelectVoice(defaultVoice);
                                }
                                catch { }
                            }
                            popupIsActive = false;
                            if (changedGlobal)
                                CustomComboBoxes.GlobalFlag.popupIsActive = false;
                        }))());
                    }
                };
            }
            options2sp.Children.Add(changeVoiceButton);
        }
        // Auto-save: how often (1-10 minutes) the tracker writes its auto-save file
        var autoSaveHeader = header(new TextBox { Text = "Auto-save", IsReadOnly = true, FontWeight = FontWeight.Bold, Margin = new Thickness(0.0, 12.0, 0.0, 0.0) });
        options2sp.Children.Add(new DockPanel { Height = 6.0 });   // extra space ABOVE the Auto-save heading (a spacer, so this and everything below it moves down 2px)
        options2sp.Children.Add(autoSaveHeader);
        var autoSaveLabel = new TextBox { Text = $"Auto-save Interval (Minutes): {TrackerModelOptions.AutoSaveMinutes}", IsReadOnly = true, Margin = new Thickness(0.0, 4.0, 0.0, 0.0), Background = Brushes.Transparent };
        options2sp.Children.Add(autoSaveLabel);
        var autoSaveSlider = new Slider { Minimum = 1.0, Maximum = 10.0, TickFrequency = 1.0, IsSnapToTickEnabled = true, Value = (double)TrackerModelOptions.AutoSaveMinutes, Width = 200.0, Margin = new Thickness(0.0, 0.0, 0.0, 2.0) };
        ToolTip.SetTip(autoSaveSlider, "How often the tracker automatically saves your run (used to restore after a crash)");
        autoSaveSlider.ValueChanged += (_, _) =>
        {
            var v = (int)autoSaveSlider.Value;
            TrackerModelOptions.AutoSaveMinutes = v;
            autoSaveLabel.Text = $"Auto-save Interval (Minutes): {v}";
        };
        options2sp.Children.Add(autoSaveSlider);

        var coopSettingsButton = Graphics.makeButton("Co-Op Client Settings", null, null);
        coopSettingsButton.HorizontalAlignment = HorizontalAlignment.Left;
        {
            var popupIsActive = false;
            var changedGlobal = false;
            coopSettingsButton.Click += (_, _) =>
            {
                if (!popupIsActive)
                {
                    popupIsActive = true;
                    if (!CustomComboBoxes.GlobalFlag.popupIsActive)
                    {
                        CustomComboBoxes.GlobalFlag.popupIsActive = true;
                        changedGlobal = true;
                    }

                    var wh = new ManualResetEvent(false);
                    var sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10.0) };
                    AddStyle(sp);

                    // NEW: Enable Co-op checkbox
                    // NEW: Enable Co-op checkbox with label
                    var enableCoopCheckbox = new CheckBox();
                    // Initial validation of FunctionAppBase
                    bool isValidUrl(string url) =>
                        Uri.TryCreate(url, UriKind.Absolute, out _);

                    var initialUrl = TrackerModelOptions.CoopSyncOptions.FunctionAppBase;
                    var initialIsValid = isValidUrl(initialUrl);

                    // Only enable checkbox if URL is valid
                    enableCoopCheckbox.IsEnabled = initialIsValid;
                    enableCoopCheckbox.IsChecked = initialIsValid && TrackerModelOptions.CoopSyncOptions.GetEnableCoop();

                    enableCoopCheckbox.HorizontalAlignment = HorizontalAlignment.Left;
                    enableCoopCheckbox.IsEnabled = false;
                    var enableCoopText = new TextBox
                    {
                        Text = "Enable Co-op Sync",
                        IsReadOnly = true,
                        BorderThickness = new Thickness(0.0),
                        FontSize = 16.0,
                        Foreground = Brushes.Orange,
                        Background = Brushes.Black,
                        IsHitTestVisible = false

                    };


                    var coopCheckStack = new StackPanel { Orientation = Orientation.Horizontal };
                    coopCheckStack.Children.Add(enableCoopCheckbox);
                    coopCheckStack.Children.Add(enableCoopText);


                    coopCheckStack.Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
                    ToolTip.SetTip(coopCheckStack, "Enables co-op syncing between two instances of ZORATracker via Azure Functions and Azure SignalR.\nThis cannot be enabled if the value of Function App Base is not a valid URL");
                    setToolTipShowDuration(coopCheckStack, 10000);
                    enableCoopCheckbox.IsChecked = TrackerModelOptions.CoopSyncOptions.GetEnableCoop();


                    sp.Children.Add(coopCheckStack);
                    // Checkbox for Debug Mode
                    var debugModeCheckbox = new CheckBox { Content = new TextBlock { Text = "Enable Debug Logging", Foreground = Brushes.Orange } };
                    debugModeCheckbox.IsChecked = TrackerModelOptions.DebugConfig.DebugMode;
                    ToolTip.SetTip(debugModeCheckbox, "Enables debug logging for co-op sync operations.\nThis is useful for troubleshooting issues with co-op sync.");
                    setToolTipShowDuration(debugModeCheckbox, 10000);
                    sp.Children.Add(debugModeCheckbox);

                    TextBox urlLabel(string txt) =>
                        new TextBox
                        {
                            Text = txt,
                            IsReadOnly = true,
                            BorderThickness = new Thickness(0.0),
                            FontSize = 16.0,
                            Foreground = Brushes.Orange,
                            Background = Brushes.Black
                        };

                    sp.Children.Add(urlLabel("Function App Base:"));

                    var functionAppBaseBox = new TextBox { Text = TrackerModelOptions.CoopSyncOptions.FunctionAppBase }; functionAppBaseBox.Foreground = Brushes.Orange; functionAppBaseBox.Background = Brushes.Black; functionAppBaseBox.CaretBrush = Brushes.Orange;

                    var functionAppBaseBorder = new Border
                    {
                        Child = functionAppBaseBox,
                        BorderBrush = Brushes.Orange,
                        BorderThickness = new Thickness(2.0),
                        Margin = new Thickness(0.0, 2.0, 0.0, 6.0)
                    };

                    var functionAppBaseValidationMessage =
                        new TextBlock
                        {
                            Text = "Function App Base must be a valid URL",
                            Foreground = Brushes.Red,
                            IsVisible = false,
                            Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
                        };

                    void validateFunctionAppBaseUrl()
                    {
                        var isValid = Uri.IsWellFormedUriString(functionAppBaseBox.Text, UriKind.Absolute);

                        // Toggle checkbox state
                        enableCoopCheckbox.IsEnabled = isValid;
                        enableCoopCheckbox.Foreground = isValid ? Brushes.Orange : Brushes.DarkGray;
                        if (!isValid)
                            enableCoopCheckbox.IsChecked = false;

                        // Border visual feedback
                        functionAppBaseBorder.BorderBrush = isValid ? Brushes.Orange : Brushes.Red;

                        // Error message visibility
                        functionAppBaseValidationMessage.IsVisible = !isValid;
                    }

                    // Initial validation on open
                    validateFunctionAppBaseUrl();

                    // Dynamic validation on text change
                    functionAppBaseBox.TextChanged += (_, _) => validateFunctionAppBaseUrl();


                    functionAppBaseBox.TextChanged += (_, _) =>
                    {
                        var isValid = isValidUrl(functionAppBaseBox.Text);
                        if (!isValid)
                        {
                            enableCoopCheckbox.IsChecked = false;
                            enableCoopCheckbox.IsEnabled = false;
                            enableCoopCheckbox.Foreground = Brushes.DarkGray;
                        }
                        else
                        {
                            enableCoopCheckbox.IsEnabled = true;
                            enableCoopCheckbox.Foreground = Brushes.Orange;
                        }
                    };

                    functionAppBaseBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                    functionAppBaseBox.TextAlignment = TextAlignment.Left;

                    ToolTip.SetTip(functionAppBaseBorder, "The base URL for the Azure Function App that handles co-op sync operations.\nThis must be a valid URL.");
                    setToolTipShowDuration(functionAppBaseBorder, 10000);
                    sp.Children.Add(functionAppBaseBorder);

                    sp.Children.Add(functionAppBaseValidationMessage);


                    sp.Children.Add(urlLabel("Negotiate Suffix:"));
                    var negotiateUrlBox = new TextBox { Text = TrackerModelOptions.CoopSyncOptions.baseNegotiateUrl }; negotiateUrlBox.Foreground = Brushes.Orange; negotiateUrlBox.Background = Brushes.Black; negotiateUrlBox.CaretBrush = Brushes.Orange;
                    negotiateUrlBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                    negotiateUrlBox.TextAlignment = TextAlignment.Left;
                    var negotiateUrlBorder = new Border
                    {
                        Child = negotiateUrlBox,
                        BorderBrush = Brushes.Orange,
                        BorderThickness = new Thickness(2.0),
                        Margin = new Thickness(0.0, 2.0, 0.0, 6.0)
                    };
                    ToolTip.SetTip(negotiateUrlBorder, "The suffix for the Azure Function App that handles co-op sync operations.\nThis will be appended to the Function App Base.");
                    setToolTipShowDuration(negotiateUrlBorder, 10000);
                    sp.Children.Add(negotiateUrlBorder);

                    sp.Children.Add(urlLabel("SyncUpdate Suffix:"));
                    var syncUpdateUrlBox = new TextBox { Text = TrackerModelOptions.CoopSyncOptions.baseSyncUpdateUrl }; syncUpdateUrlBox.Foreground = Brushes.Orange; syncUpdateUrlBox.Background = Brushes.Black; syncUpdateUrlBox.CaretBrush = Brushes.Orange;
                    syncUpdateUrlBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                    syncUpdateUrlBox.TextAlignment = TextAlignment.Left;
                    var syncUpdateUrlBorder = new Border
                    {
                        Child = syncUpdateUrlBox,
                        BorderBrush = Brushes.Orange,
                        BorderThickness = new Thickness(2.0),
                        Margin = new Thickness(0.0, 2.0, 0.0, 6.0)
                    };
                    ToolTip.SetTip(syncUpdateUrlBorder, "The suffix for the Azure Function App that handles outgoing co-op sync operations.\nThis will be appended to the Function App Base.");
                    setToolTipShowDuration(syncUpdateUrlBorder, 10000);
                    sp.Children.Add(syncUpdateUrlBorder);

                    sp.Children.Add(new DockPanel { Height = 10.0 });  // spacer before ID fields


                    sp.Children.Add(new TextBox { Text = "Console ID (This instance):", IsReadOnly = true });
                    var myIdBox = new TextBox(); myIdBox.Foreground = Brushes.Orange; myIdBox.Background = Brushes.Black; myIdBox.CaretBrush = Brushes.Orange;
                    myIdBox.Text = TrackerModelOptions.CoopSyncOptions.MyConsoleId;
                    myIdBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                    myIdBox.TextAlignment = TextAlignment.Center;

                    var myIdBorder = new Border
                    {
                        Child = myIdBox,
                        BorderBrush = Brushes.Orange,
                        BorderThickness = new Thickness(2.0),
                        Margin = new Thickness(0.0, 2.0, 0.0, 6.0)
                    };
                    ToolTip.SetTip(myIdBorder, "The unique ID for this console instance.\nThis is used to identify this console in co-op sync operations.");
                    setToolTipShowDuration(myIdBorder, 10000);
                    sp.Children.Add(myIdBorder);

                    var guidButton = Graphics.makeButton("Generate GUID", null, null);
                    guidButton.Click += (_, _) =>
                    {
                        var newGuid = Guid.NewGuid().ToString();
                        myIdBox.Text = newGuid;
                    };
                    ToolTip.SetTip(guidButton, "Generate a random ID for this console instance.\nThis will be used as the unique ID for this console in co-op sync operations.");
                    setToolTipShowDuration(guidButton, 10000);
                    sp.Children.Add(guidButton);

                    sp.Children.Add(new DockPanel { Height = 10.0 });

                    sp.Children.Add(new TextBox { Text = "Target Console ID (to sync with):", IsReadOnly = true });
                    var targetIdBox = new TextBox(); targetIdBox.Foreground = Brushes.Orange; targetIdBox.Background = Brushes.Black; targetIdBox.CaretBrush = Brushes.Orange;
                    targetIdBox.Text = TrackerModelOptions.CoopSyncOptions.TargetConsoleId;
                    targetIdBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                    targetIdBox.TextAlignment = TextAlignment.Center;

                    var targetIdBorder = new Border
                    {
                        Child = targetIdBox,
                        BorderBrush = Brushes.Orange,
                        BorderThickness = new Thickness(2.0),
                        Margin = new Thickness(0.0, 2.0, 0.0, 6.0)
                    };
                    ToolTip.SetTip(targetIdBorder, "The unique ID for the target console instance.\nThis is used to identify the target console in co-op sync operations.");
                    sp.Children.Add(targetIdBorder);

                    sp.Children.Add(new DockPanel { Height = 10.0 });

                    var saveButton = Graphics.makeButton("Save", null, null);
                    saveButton.Click += (_, _) =>
                    {
                        TrackerModelOptions.CoopSyncOptions.MyConsoleId = myIdBox.Text;
                        TrackerModelOptions.CoopSyncOptions.TargetConsoleId = targetIdBox.Text;

                        TrackerModelOptions.CoopSyncOptions.FunctionAppBase = functionAppBaseBox.Text;
                        TrackerModelOptions.CoopSyncOptions.baseNegotiateUrl = negotiateUrlBox.Text;
                        TrackerModelOptions.CoopSyncOptions.baseSyncUpdateUrl = syncUpdateUrlBox.Text;

                        TrackerModelOptions.CoopSyncOptions.SetEnableCoop(
                            (bool)enableCoopCheckbox.IsChecked
                            );
                        TrackerModelOptions.DebugConfig.DebugMode =
                            (bool)debugModeCheckbox.IsChecked;

                        TrackerModelOptions.writeSettings();
                        wh.Set();
                    };
                    ToolTip.SetTip(saveButton, "Save the current settings for co-op sync operations.\nThis will update the settings.");
                    sp.Children.Add(saveButton);

                    CustomComboBoxes.StartImmediate(((Func<Task>)(async () =>
                    {
                        await CustomComboBoxes.DoModalDocked(cm, wh, Dock.Bottom,
                            new Border
                            {
                                Child = sp,
                                BorderBrush = Brushes.Gray,
                                BorderThickness = new Thickness(3.0),
                                Background = Brushes.Black,
                                Width = 400.0,
                                HorizontalAlignment = HorizontalAlignment.Center
                            }
                        );
                        popupIsActive = false;
                        if (changedGlobal)
                            CustomComboBoxes.GlobalFlag.popupIsActive = false;
                    }))());
                }
            };
        }
        // Co-op buttons moved to the "Other" column below




        optionsAllsp.Children.Add(new DockPanel { Width = 2.0, Background = Brushes.Gray });
        optionsAllsp.Children.Add(options2sp);
        optionsAllsp.Children.Add(new DockPanel { Width = 2.0, Background = Brushes.Gray });

        var options3sp = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(10.0, 2.0, 0.0, 0.0) };
        tb = header(new TextBox { Text = "Other", IsReadOnly = true, FontWeight = FontWeight.Bold });
        options3sp.Children.Add(tb);

        var cb3 = new CheckBox { Content = new TextBox { Text = "Animate Tile Changes", IsReadOnly = true } };
        cb3.IsChecked = TrackerModelOptions.AnimateTileChanges.Value;
        Graphics.onChecked(cb3, () => TrackerModelOptions.AnimateTileChanges.Value = true);
        Graphics.onUnchecked(cb3, () => TrackerModelOptions.AnimateTileChanges.Value = false);
        ToolTip.SetTip(cb3, "When you change an overworld map spot or a dungeon room type, briefly animate the rectangle to highlight what changed");
        setToolTipShowDuration(cb3, 10000);
        options3sp.Children.Add(cb3);

        cb3 = new CheckBox { Content = new TextBox { Text = "Animate Shop Highlights", IsReadOnly = true } };
        cb3.IsChecked = TrackerModelOptions.AnimateShopHighlights.Value;
        Graphics.onChecked(cb3, () => TrackerModelOptions.AnimateShopHighlights.Value = true);
        Graphics.onUnchecked(cb3, () => TrackerModelOptions.AnimateShopHighlights.Value = false);
        ToolTip.SetTip(cb3, "When you mouse hover certain icons to highlight corresponding shops on the map, briefly animate the rectangle");
        setToolTipShowDuration(cb3, 10000);
        options3sp.Children.Add(cb3);

        cb3 = new CheckBox { Content = new TextBox { Text = "Remember Startup Selection", IsReadOnly = true } };
        cb3.IsChecked = TrackerModelOptions.RememberStartupSelection.Value;
        Graphics.onChecked(cb3, () => TrackerModelOptions.RememberStartupSelection.Value = true);
        Graphics.onUnchecked(cb3, () => TrackerModelOptions.RememberStartupSelection.Value = false);
        ToolTip.SetTip(cb3, "When checked, the boxes you check under Startup Options and ZORA Extras on the start screen are remembered\nand come back checked the next time the tracker starts");
        setToolTipShowDuration(cb3, 10000);
        options3sp.Children.Add(cb3);

        cb3 = new CheckBox { Content = new TextBox { Text = "Save On Completion", IsReadOnly = true } };
        cb3.IsChecked = TrackerModelOptions.SaveOnCompletion.Value;
        Graphics.onChecked(cb3, () => TrackerModelOptions.SaveOnCompletion.Value = true);
        Graphics.onUnchecked(cb3, () => TrackerModelOptions.SaveOnCompletion.Value = false);
        ToolTip.SetTip(cb3, "When you click Zelda to complete the seed, automatically save the full tracker state to a file");
        setToolTipShowDuration(cb3, 10000);
        options3sp.Children.Add(cb3);

        if (!Platform.isBrowser)   // (the web version is always the latest, and never checks for updates)
        {
            cb3 = new CheckBox { Content = new TextBox { Text = "Check For Updates On Startup", IsReadOnly = true } };
            cb3.IsChecked = TrackerModelOptions.CheckForUpdates.Value;
            Graphics.onChecked(cb3, () => TrackerModelOptions.CheckForUpdates.Value = true);
            Graphics.onUnchecked(cb3, () => TrackerModelOptions.CheckForUpdates.Value = false);
            ToolTip.SetTip(cb3, "When the tracker starts, look on GitHub for a newer release.\nIf there is one, the version button glows green; click it for the download link.");
            setToolTipShowDuration(cb3, 10000);
            options3sp.Children.Add(cb3);
        }

        cb3 = new CheckBox { Content = new TextBox { Text = "Archipelago Support", IsReadOnly = true } };
        cb3.IsChecked = TrackerModelOptions.ArchipelagoSupport.Value;
        Graphics.onChecked(cb3, () => { TrackerModelOptions.ArchipelagoSupport.Value = true; DrawingLayer.applyArchipelagoSupport(); });
        Graphics.onUnchecked(cb3, () => { TrackerModelOptions.ArchipelagoSupport.Value = false; DrawingLayer.applyArchipelagoSupport(); });
        var apOptionCB = cb3;
        DrawingLayer.ArchipelagoSupportChanged += () => { if (apOptionCB.IsChecked != TrackerModelOptions.ArchipelagoSupport.Value) apOptionCB.IsChecked = TrackerModelOptions.ArchipelagoSupport.Value; };
        ToolTip.SetTip(cb3, "For Archipelago multiworld games: adds the AP Item, a gem for an item that belongs to another world,\nto the item-box menus and the overworld tile menu. Place it as many times as you need to mark checked locations.");
        setToolTipShowDuration(cb3, 10000);
        options3sp.Children.Add(cb3);

        cb3 = new CheckBox { Content = new TextBox { Text = "Use old sword icons", IsReadOnly = true } };
        cb3.IsChecked = TrackerModelOptions.UseOldSwordIcons.Value;
        Graphics.onChecked(cb3, () => TrackerModelOptions.UseOldSwordIcons.Value = true);
        Graphics.onUnchecked(cb3, () => TrackerModelOptions.UseOldSwordIcons.Value = false);
        ToolTip.SetTip(cb3, "Draw the swords as the plain old sword pictures (the 4th sword has a checkered blue handle)\ninstead of the numbered sword icons. The progressive sword icon in the menu stays the same.\nTakes effect the next time the tracker starts.");
        setToolTipShowDuration(cb3, 10000);
        options3sp.Children.Add(cb3);

        // one checkbox does both jobs: look for the seed and flags in other windows' titles (e.g. fceux), and show them in the bottom corner of the Notes box
        TrackerModelOptions.DisplaySeedAndFlags.Value = TrackerModelOptions.SnoopSeedAndFlags.Value;   // (older settings had these two apart: the one checkbox now sets both)
        cb3 = new CheckBox { Content = new TextBox { Text = "Display Seed + Flags", IsReadOnly = true } };
        cb3.IsChecked = TrackerModelOptions.SnoopSeedAndFlags.Value;
        Graphics.onChecked(cb3, () =>
        {
            TrackerModelOptions.SnoopSeedAndFlags.Value = true;
            TrackerModelOptions.DisplaySeedAndFlags.Value = true;
            SaveAndLoad.MaybePollSeedAndFlags();
            SaveAndLoad.seedAndFlagsUpdated.Trigger(null);
        });
        Graphics.onUnchecked(cb3, () =>
        {
            TrackerModelOptions.SnoopSeedAndFlags.Value = false;
            TrackerModelOptions.DisplaySeedAndFlags.Value = false;
            SaveAndLoad.seedAndFlagsUpdated.Trigger(null);
        });
        ToolTip.SetTip(cb3, "Periodically check for other system windows (e.g. fceux)\nthat appear to have a seed and flag in the title, to\ninclude with save data, and display them in the\nbottom corner of the Notes box");
        setToolTipShowDuration(cb3, 10000);
        options3sp.Children.Add(cb3);

        cb3 = new CheckBox { Content = new TextBox { Text = "Listen For Speech", IsReadOnly = true } };
        if (microphoneFailedToInitialize)
        {
            cb3.IsEnabled = false;
            cb3.IsChecked = false;
            ToolTip.SetTip(cb3, "Disabled (microphone was not initialized properly during startup)");
            setToolTipShowDuration(cb3, 10000);
            ToolTip.SetShowOnDisabled(cb3, true);
        }
        else
        {
            ToolTip.SetTip(cb3, "Use the microphone to listen for spoken map update commands\nExample: say 'tracker set bomb shop' while hovering an unmarked map tile");
            setToolTipShowDuration(cb3, 10000);
            link(cb3, TrackerModelOptions.ListenForSpeech, false, () => { });
        }
        options3sp.Children.Add(cb3);

        cb3 = new CheckBox { Content = new TextBox { Text = "Confirmation Sound", IsReadOnly = true }, Margin = new Thickness(20.0, 0.0, 0.0, 0.0) };
        if (microphoneFailedToInitialize)
        {
            cb3.IsEnabled = false;
            cb3.IsChecked = false;
            ToolTip.SetTip(cb3, "Disabled (microphone was not initialized properly during startup)");
            setToolTipShowDuration(cb3, 10000);
            ToolTip.SetShowOnDisabled(cb3, true);
        }
        else
        {
            ToolTip.SetTip(cb3, "Play a confirmation sound whenever speech recognition is used to make an update to the tracker");
            setToolTipShowDuration(cb3, 10000);
            link(cb3, TrackerModelOptions.PlaySoundWhenUseSpeech, false, () => { });
        }
        options3sp.Children.Add(cb3);

/*  // this is not (yet) a fully supported feature, so don't publish it on the options menu
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
*/

        cb3 = new CheckBox { Content = new TextBox { Text = "Broadcast Window", IsReadOnly = true } };
        cb3.IsChecked = TrackerModelOptions.ShowBroadcastWindow.Value;
        Graphics.onChecked(cb3, () => { TrackerModelOptions.ShowBroadcastWindow.Value = true; triggerBroadcastWindowOptionChanged(); });
        Graphics.onUnchecked(cb3, () => { TrackerModelOptions.ShowBroadcastWindow.Value = false; triggerBroadcastWindowOptionChanged(); });
        ToolTip.SetTip(cb3, "Open a separate, smaller window, for stream capture.\nYou still interact with the original large window,\nbut the smaller window will focus the view on either the overworld or\nthe dungeon tabs, based on your mouse position.");
        setToolTipShowDuration(cb3, 10000);
        options3sp.Children.Add(cb3);


        cb3 = new CheckBox { Content = new TextBox { Text = "Include Overworld Magnifier", IsReadOnly = true }, Margin = new Thickness(14.0, 0.0, 0.0, 0.0) };
        cb3.IsChecked = TrackerModelOptions.BroadcastWindowIncludesOverworldMagnifier.Value;
        Graphics.onChecked(cb3, () => { TrackerModelOptions.BroadcastWindowIncludesOverworldMagnifier.Value = true; triggerBroadcastWindowOptionChanged(); });
        Graphics.onUnchecked(cb3, () => { TrackerModelOptions.BroadcastWindowIncludesOverworldMagnifier.Value = false; triggerBroadcastWindowOptionChanged(); });
        ToolTip.SetTip(cb3, "Whether to include the overworld magnifier when it is on-screen, which will obscure some other elements");
        setToolTipShowDuration(cb3, 10000);
        options3sp.Children.Add(cb3);

        cb3 = new CheckBox { Content = new TextBox { Text = "Mouse Magnifier Window", IsReadOnly = true } };
        cb3.IsChecked = TrackerModelOptions.ShowMouseMagnifierWindow.Value;
        Graphics.onChecked(cb3, () => { TrackerModelOptions.ShowMouseMagnifierWindow.Value = true; triggerMouseMagnifierWindowOptionChanged(); });
        Graphics.onUnchecked(cb3, () => { TrackerModelOptions.ShowMouseMagnifierWindow.Value = false; triggerMouseMagnifierWindowOptionChanged(); });
        // (closing the magnifier with its own X turns the option off: untick this box to match)
        var magnifierCB = cb3;
        Layout.MagnifierClosedByUser += () => Dispatcher.UIThread.Post(() => { if (magnifierCB.IsChecked != false) magnifierCB.IsChecked = false; });
        ToolTip.SetTip(cb3, "Open a separate, resizable window that shows a\nzoomed-in area around the mouse.\nCan be useful when using the app on a small screen\nwhere precise mouse targetting is hard to see.");
        setToolTipShowDuration(cb3, 10000);
        options3sp.Children.Add(cb3);

        cb3 = new CheckBox { Content = new TextBox { Text = "Hide Timer", IsReadOnly = true } };
        cb3.IsChecked = TrackerModelOptions.HideTimer.Value;
        Graphics.onChecked(cb3, () => { TrackerModelOptions.HideTimer.Value = true; triggerHideTimerChanged(); });
        Graphics.onUnchecked(cb3, () => { TrackerModelOptions.HideTimer.Value = false; triggerHideTimerChanged(); });
        ToolTip.SetTip(cb3, "Don't display the bright green timer in the upper right corner.");
        setToolTipShowDuration(cb3, 10000);
        options3sp.Children.Add(cb3);

        cb3 = new CheckBox { Content = new TextBox { Text = "Race Mode", IsReadOnly = true } };
        cb3.IsChecked = TrackerModelOptions.RaceMode.Value;
        Graphics.onChecked(cb3, () => { TrackerModelOptions.RaceMode.Value = true; triggerRaceModeChanged(); });
        Graphics.onUnchecked(cb3, () => { TrackerModelOptions.RaceMode.Value = false; triggerRaceModeChanged(); });
        ToolTip.SetTip(cb3, "Disable many features that could be deemed an unfair advantage during a race.");
        setToolTipShowDuration(cb3, 10000);
        options3sp.Children.Add(cb3);

        // Co-op buttons (moved here from Reminders column to reduce its height)
        options3sp.Children.Add(new DockPanel { Height = 6.0 });  // small spacer
        // the two co-op buttons are the same size (the widest label sets it), with a couple of pixels between them
        coopSettingsButton.Width = 182.0;
        coopSettingsButton.Margin = new Thickness(0.0, 0.0, 0.0, 3.0);
        options3sp.Children.Add(coopSettingsButton);
        var coopHostSettingsButton = Graphics.makeButton("Co-Op Host Settings", null, null);
        coopHostSettingsButton.HorizontalAlignment = HorizontalAlignment.Left;
        coopHostSettingsButton.Width = 182.0;
        coopHostSettingsButton.Click += (_, _) => showCoopHostSettingsWindow(cm);
        options3sp.Children.Add(coopHostSettingsButton);
        options3sp.Children.Add(crashLogButton);
        options3sp.Children.Add(makeGuidedTourButton(cm, includePopupExplainer));   // (the start screen also has its own Guided Tour button in the main section)

        optionsAllsp.Children.Add(options3sp);

        var total = new StackPanel { Orientation = Orientation.Vertical, Width = width, Background = Brushes.Black };
        if (includePopupExplainer)
        {
            var tb1 = new TextBox { Text = "Options Menu", IsReadOnly = true, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center };
            total.Children.Add(tb1);
            total.Children.Add(new DockPanel { Height = 2.0, Background = Brushes.Gray });
        }

        // some spare black space at the bottom of every column (so the dividers between the columns run all the way down), for future options
        foreach (var col in new[] { options1sp, options2sp, options3sp })
            col.Children.Add(new DockPanel { Height = 8.0, Background = Brushes.Black });
        total.Children.Add(new Viewbox { Child = optionsAllsp, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = width, HorizontalAlignment = HorizontalAlignment.Left });

        all.Child = total;
        if (includePopupExplainer)
        {
            // Escape closes the in-app Options popup. The app-wide Escape handler listens on the window, but in the browser build a key only passes
            // through the window when focus is inside it, so the panel takes focus when it opens and closes the top popup itself if nothing else did.
            all.Focusable = true;
            all.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => { if (all.IsEffectivelyVisible) all.Focus(); }, DispatcherPriority.Background);
            all.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, new EventHandler<Avalonia.Input.KeyEventArgs>((_, ea) =>
            {
                if (ea.Key == Avalonia.Input.Key.Escape && !ea.Handled && CustomComboBoxes.DismissTopModal())
                    ea.Handled = true;
            }), Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble);
        }

        raceModeChanged += () =>
        {
            if (TrackerModelOptions.RaceMode.Value)
            {
                // Mute audio, same as checking the box (remember the user's own choice, once: this handler runs for both options canvases, and the second one
                // would otherwise save the mute the first one just forced on)
                if (!raceModeWasOn)
                {
                    raceModeSavedMuted = TrackerModelOptions.IsMuted;
                    writeRaceModeSavedMuted(raceModeSavedMuted);
                }
                raceModeWasOn = true;
                TrackerModelOptions.IsMuted = true;
                voice.Volume = 0;
                muteCB.IsChecked = true;
            }
            else
            {
                // Restore checkbox to what user had previously selected
                // (Race Mode on since an earlier run: the choice was kept in a small file, as memory starts empty)
                if (!raceModeWasOn && readRaceModeSavedMuted() is bool fromFile)
                    raceModeSavedMuted = fromFile;
                deleteRaceModeSavedMuted();
                raceModeWasOn = false;
                TrackerModelOptions.IsMuted = raceModeSavedMuted;
                voice.Volume = raceModeSavedMuted ? 0 : TrackerModelOptions.Volume;
                muteCB.IsChecked = raceModeSavedMuted;   // (this raises the checkbox's own handler, which sets IsMuted from it: it must say the same thing)
            }
        };






        return all;
    }
}
