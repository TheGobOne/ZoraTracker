// Ported from Z1R_WPF/Reminders.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// The reminder agent (F#: a MailboxProcessor) is an unbounded Channel of ReminderMessage: `reminderAgent.Post(...)` → `reminderAgent.Writer.TryWrite(...)`.
// As in WPF, the initial agent is never read, so reminders posted before SetupReminderDisplayAndProcessing are dropped. The message loop is an
// `async` method started on the UI thread: awaits resume on the UI thread (as WPF's `Async.SwitchToContext(ctxt)`), and the speak/sleep part
// runs in `Task.Run` (as WPF's `Async.SwitchToThreadPool()`). Voice and sounds go through Platform (OptionsMenu.voice, Graphics.PlaySoundForReminder,
// Platform.SystemBeep). WPF's FrameworkElement is Control; `Async<unit>` values (started later) are `Func<Task>`.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using static Z1R_Avalonia.OverworldItemGridUI;
using static Z1R_Avalonia.CustomComboBoxes.GlobalFlag;

namespace Z1R_Avalonia;

public static class Reminders
{
    public static readonly Platform.SpeechSynthesizer voice = OptionsMenu.voice;
    public static Control upcb(System.Drawing.Bitmap bmp) => Graphics.BMPtoImage(bmp);
    // (F#: the MailboxProcessor's message tuple)
    public readonly record struct ReminderMessage(string text, TrackerModel.ReminderCategory category, bool wasMuted, bool shouldRemindVoice, IEnumerable<Control> icons,
                                                  bool shouldRemindVisual, Func<Task> visualUpdateToSynchronizeWithReminder);
    public static Channel<ReminderMessage> reminderAgent = Channel.CreateUnbounded<ReminderMessage>();
    public static readonly StackPanel reminderLogSP = new StackPanel { Orientation = Orientation.Vertical };
    public static bool anyReminderGrayedOut = false;
    public static void SendReminderImpl(TrackerModel.ReminderCategory category, string text, IEnumerable<Control> icons, Func<Task> visualUpdateToSynchronizeWithReminder)
    {
        if (!(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Value()))  // if won the game, quit sending reminders
        {
            bool shouldRemindVoice, shouldRemindVisual;
            switch (category.Tag)
            {
                case TrackerModel.ReminderCategory.Tags.Blockers:        (shouldRemindVoice, shouldRemindVisual) = (TrackerModelOptions.VoiceReminders.Blockers.Value,        TrackerModelOptions.VisualReminders.Blockers.Value); break;
                case TrackerModel.ReminderCategory.Tags.CoastItem:       (shouldRemindVoice, shouldRemindVisual) = (TrackerModelOptions.VoiceReminders.CoastItem.Value,       TrackerModelOptions.VisualReminders.CoastItem.Value); break;
                case TrackerModel.ReminderCategory.Tags.DungeonFeedback: (shouldRemindVoice, shouldRemindVisual) = (TrackerModelOptions.VoiceReminders.DungeonFeedback.Value, TrackerModelOptions.VisualReminders.DungeonFeedback.Value); break;
                case TrackerModel.ReminderCategory.Tags.HaveKeyLadder:   (shouldRemindVoice, shouldRemindVisual) = (TrackerModelOptions.VoiceReminders.HaveKeyLadder.Value,   TrackerModelOptions.VisualReminders.HaveKeyLadder.Value); break;   // legacy
                case TrackerModel.ReminderCategory.Tags.RecorderPBSpotsAndBoomstickBook: (shouldRemindVoice, shouldRemindVisual) = (TrackerModelOptions.VoiceReminders.RecorderPBSpotsAndBoomstickBook.Value, TrackerModelOptions.VisualReminders.RecorderPBSpotsAndBoomstickBook.Value); break;   // legacy
                case TrackerModel.ReminderCategory.Tags.RecorderSpots:      (shouldRemindVoice, shouldRemindVisual) = (TrackerModelOptions.VoiceReminders.RecorderSpots.Value,      TrackerModelOptions.VisualReminders.RecorderSpots.Value); break;
                case TrackerModel.ReminderCategory.Tags.PowerBraceletSpots: (shouldRemindVoice, shouldRemindVisual) = (TrackerModelOptions.VoiceReminders.PowerBraceletSpots.Value, TrackerModelOptions.VisualReminders.PowerBraceletSpots.Value); break;
                case TrackerModel.ReminderCategory.Tags.BoomstickBook:      (shouldRemindVoice, shouldRemindVisual) = (TrackerModelOptions.VoiceReminders.BoomstickBook.Value,      TrackerModelOptions.VisualReminders.BoomstickBook.Value); break;
                case TrackerModel.ReminderCategory.Tags.HaveMagicKey:       (shouldRemindVoice, shouldRemindVisual) = (TrackerModelOptions.VoiceReminders.HaveMagicKey.Value,       TrackerModelOptions.VisualReminders.HaveMagicKey.Value); break;
                case TrackerModel.ReminderCategory.Tags.HaveLadder:         (shouldRemindVoice, shouldRemindVisual) = (TrackerModelOptions.VoiceReminders.HaveLadder.Value,         TrackerModelOptions.VisualReminders.HaveLadder.Value); break;
                case TrackerModel.ReminderCategory.Tags.SwordHearts:     (shouldRemindVoice, shouldRemindVisual) = (TrackerModelOptions.VoiceReminders.SwordHearts.Value,     TrackerModelOptions.VisualReminders.SwordHearts.Value); break;
                case TrackerModel.ReminderCategory.Tags.DoorRepair:      (shouldRemindVoice, shouldRemindVisual) = (TrackerModelOptions.VoiceReminders.DoorRepair.Value,      TrackerModelOptions.VisualReminders.DoorRepair.Value); break;
                case TrackerModel.ReminderCategory.Tags.OverworldOverwrites: (shouldRemindVoice, shouldRemindVisual) = (TrackerModelOptions.VoiceReminders.OverworldOverwrites.Value, TrackerModelOptions.VisualReminders.OverworldOverwrites.Value); break;
                case TrackerModel.ReminderCategory.Tags.RandomExtras:    (shouldRemindVoice, shouldRemindVisual) = (TrackerModelOptions.VoiceReminders.RandomExtras.Value,    TrackerModelOptions.VisualReminders.RandomExtras.Value); break;
                case TrackerModel.ReminderCategory.Tags.Asterisk:        (shouldRemindVoice, shouldRemindVisual) = (false, false); break;
                default: throw new InvalidOperationException("unknown ReminderCategory");
            }
            if (!(Timeline.isCurrentlyLoadingASave))
                reminderAgent.Writer.TryWrite(new ReminderMessage(text, category, TrackerModelOptions.IsMuted, shouldRemindVoice, icons, shouldRemindVisual, visualUpdateToSynchronizeWithReminder));
        }
    }
    public static void SendReminder(TrackerModel.ReminderCategory category, string text, IEnumerable<Control> icons) =>
        SendReminderImpl(category, text, icons, null);
    public static Control ReminderTextBox(string txt) =>
        new TextBox { Text = txt, Foreground = Brushes.Orange, Background = Brushes.Black, FontSize = 20.0, FontWeight = FontWeight.Bold, IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0), TextAlignment = TextAlignment.Center };
    public static TextBox ReminderDescriptionTextBox(string txt) =>
        new TextBox { Text = txt, Foreground = Brushes.Orange, Background = Brushes.Black, FontSize = 12.0, IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, BorderThickness = new Thickness(0.0), TextAlignment = TextAlignment.Left };
    private static readonly bool _init1 = init1();
    private static bool init1()
    {
        Graphics.ErrorBeepWithReminderLogText = txt =>
        {
            Platform.SystemBeep();   // (WPF: System.Media.SystemSounds.Asterisk.Play())
            SendReminder(TrackerModel.ReminderCategory.Asterisk, txt, new Control[] { Graphics.BMPtoImage(Graphics.other_monster2), ReminderTextBox("Error beep") });
        };
        return true;
    }

    // the 'log' button now sits in the top-left button stack above Item Locations (the green reminder box itself still shows at the top right of the timeline)
    public static readonly Button reminderLogButton = Graphics.makeButton("Log", 12.0, Brushes.Orange);
    public static DockPanel SetupReminderDisplayAndProcessing(CustomComboBoxes.CanvasManager cm)
    {
        // (F#: `let ctxt = SynchronizationContext.Current`; the message loop below starts on this (UI) thread, so its awaits resume here)
        var reminderDisplayOuterDockPanel = new DockPanel { Width = OMTW * 16.0, Height = THRU_TIMELINE_H - START_TIMELINE_H, LastChildFill = false };
        // the idle 'log' button is a real button, same style and height as the 'Draw + Custom Icons' button (not hit-testable itself: the wrapper border below handles the mouse)
        var reminderLogTB = Graphics.makeButton("Log", 12.0, Brushes.Orange);
        reminderLogTB.MaxHeight = 22.0;
        reminderLogTB.VerticalAlignment = VerticalAlignment.Top;   // natural height, capped like the Draw button, so the two match exactly
        reminderLogTB.IsHitTestVisible = false;
        reminderLogTB.Focusable = false;
        var reminderDisplayInnerBorder = new Border { Child = reminderLogTB, BorderThickness = new Thickness(0.0), BorderBrush = Brushes.Gray, Background = Brushes.Black, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(reminderDisplayInnerBorder, Dock.Right);
        reminderDisplayInnerBorder.IsVisible = false;   // only shown while a reminder is displaying
        void openLog()
        {
            if (!popupIsActive)
            {
                popupIsActive = true;
                var wh = new ManualResetEvent(false);
                var sp = new StackPanel { Orientation = Orientation.Vertical };
                sp.Children.Add(new TextBox { Text = "Recent reminders log", Foreground = Brushes.Orange, Background = Brushes.Black, FontSize = 20.0, IsHitTestVisible = false,
                                              BorderThickness = new Thickness(0.0), Margin = new Thickness(6.0, 0.0, 6.0, 0.0), HorizontalAlignment = HorizontalAlignment.Center });
                if (anyReminderGrayedOut)
                {
                    sp.Children.Add(new TextBox { Text = "(Grayed-out reminders are those you disabled in the Options Menu)", Foreground = Brushes.Orange, Background = Brushes.Black, FontSize = 12.0,
                                                  IsHitTestVisible = false, BorderThickness = new Thickness(0.0), Margin = new Thickness(6.0, 0.0, 6.0, 0.0), HorizontalAlignment = HorizontalAlignment.Center });
                }
                sp.Children.Add(new DockPanel { Background = Brushes.Gray, Margin = new Thickness(6.0, 3.0, 6.0, 0.0), Height = 3.0 });
                var reminderView = new ScrollViewer { Content = reminderLogSP, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 340.0, Margin = new Thickness(3.0) };
                sp.Children.Add(reminderView);
                var b = new Border { BorderBrush = Brushes.Gray, Background = Brushes.Black, BorderThickness = new Thickness(3.0), Child = sp, MaxWidth = 748.0 };
                async Task run()
                {
                    await CustomComboBoxes.DoModal(cm, wh, 10.0, 60.0, b);
                    reminderView.Content = null; // deparent log for future reuse
                    popupIsActive = false;
                }
                CustomComboBoxes.StartImmediate(run());
            }
        }
        reminderDisplayInnerBorder.PointerPressed += (_, _) => openLog();   // (WPF: MouseDown)
        reminderLogButton.Click += (_, _) => openLog();
        reminderDisplayInnerBorder.PointerEntered += (_, _) => { if (!object.ReferenceEquals(reminderDisplayInnerBorder.Child, reminderLogTB)) reminderDisplayInnerBorder.BorderBrush = Brushes.Cyan; };
        reminderDisplayInnerBorder.PointerExited += (_, _) => reminderDisplayInnerBorder.BorderBrush = (object.ReferenceEquals(reminderDisplayInnerBorder.Child, reminderLogTB) ? Brushes.Gray : Brushes.Lime);
        DockPanel.SetDock(reminderDisplayInnerBorder, Dock.Top);
        reminderDisplayOuterDockPanel.Children.Add(reminderDisplayInnerBorder);
        var inbox = Channel.CreateUnbounded<ReminderMessage>();
        reminderAgent = inbox;
        async Task messageLoop()
        {
            while (true)
            {
                var (text, category, wasMuted, shouldRemindVoice, icons, shouldRemindVisual, visualUpdateToSynchronizeWithReminder) = await inbox.Reader.ReadAsync();
                // (back on the UI thread here: F# `do! Async.SwitchToContext(ctxt)`)
                var sp = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.Black, Margin = new Thickness(6.0) };
                foreach (var i in icons)
                {
                    i.Margin = new Thickness(3.0);
                    sp.Children.Add(i);
                }
                // since a lot of reminders can queue up, ensure muting applies to both Post time and Play time, as it's frustrating to hit 'Disable all' and still hear more play
                var muted = (wasMuted || TrackerModelOptions.IsMuted);
                if (!(muted))
                {
                    var iconCount = sp.Children.Count;
                    if (shouldRemindVisual && !TrackerModelOptions.SuppressAudioForSelfTest)
                        Graphics.PlaySoundForReminder();
                    if (shouldRemindVisual || category.Equals(TrackerModel.ReminderCategory.Asterisk))
                    {
                        reminderDisplayInnerBorder.Child = sp;
                        reminderDisplayInnerBorder.IsVisible = true;
                        reminderDisplayInnerBorder.BorderThickness = new Thickness(3.0);   // a reminder is showing: the green box
                        reminderDisplayInnerBorder.BorderBrush = Brushes.Lime;
                    }
                    if (visualUpdateToSynchronizeWithReminder != null)
                        CustomComboBoxes.StartImmediate(visualUpdateToSynchronizeWithReminder());
                    await Task.Run(async () =>   // (F#: `do! Async.SwitchToThreadPool()` ... `do! Async.SwitchToContext(ctxt)`)
                    {
                        if (shouldRemindVisual)
                            await Task.Delay(200); // give reminder clink sound time to play
                        var startSpeakTime = DateTime.Now;
                        if (shouldRemindVoice && voice.Volume != 0 && !TrackerModelOptions.SuppressAudioForSelfTest)
                            voice.Speak(text);
                        if (shouldRemindVisual || category.Equals(TrackerModel.ReminderCategory.Asterisk))
                        {
                            var minimumDuration = TimeSpan.FromSeconds((double)Math.Max(3, iconCount));  // ensure at least 3s, and at least 1s per icon
                            var elapsed = DateTime.Now - startSpeakTime;
                            if (elapsed < minimumDuration)
                            {
                                var ms = (int)(minimumDuration - elapsed).TotalMilliseconds;
                                await Task.Delay(ms);   // ensure ui displayed a minimum time
                            }
                        }
                    });
                }
                // once reminder no longer displayed, log it.
                var timeString = OverworldItemGridUI.hmsTimeTextBox.Text;
                reminderDisplayInnerBorder.Child = reminderLogTB;   // also deparents sp
                reminderDisplayInnerBorder.IsVisible = false;
                reminderDisplayInnerBorder.BorderThickness = new Thickness(0.0);   // back to the plain 'log' button look
                reminderDisplayInnerBorder.BorderBrush = Brushes.Gray;
                sp.Margin = new Thickness(0.0);
                sp.Children.Insert(0, new DockPanel { Background = Brushes.Gray, Width = 18.0, Height = 3.0, Margin = new Thickness(6.0, 0.0, 6.0, 0.0), VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Insert(0, ReminderTextBox(timeString));
                sp.Children.Add(ReminderDescriptionTextBox($"{text}\n({category.DisplayName})"));
                var hasAny = reminderLogSP.Children.Count > 0;
                var grayOut = muted || (!shouldRemindVisual && !shouldRemindVoice);
                grayOut = category.Equals(TrackerModel.ReminderCategory.Asterisk) ? false : grayOut;   // never gray out error beeps
                if (grayOut)
                    anyReminderGrayedOut = true;
                reminderLogSP.Children.Insert(0, new Border { Child = sp, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.0, 0.0, 0.0, hasAny ? 1.0 : 0.0), Opacity = grayOut ? 0.6 : 1.0 });
            }
        }
        CustomComboBoxes.StartImmediate(messageLoop());
        return reminderDisplayOuterDockPanel;
    }

    ////////////////////////////////////////////////////

    // (F#: `open OverworldMapTileCustomization`; only MapStateProxy is used from it)
    public static void RemindOverworldOverwrites(int i, int j, int originalState, int currentState, string[] spokenOWTiles, Func<Task> highlightAsyncComp)
    {
        // remind destructive changes
        if (originalState == TrackerModel.MapSquareChoiceDomainHelper.UNKNOWN_SECRET &&
            (currentState == TrackerModel.MapSquareChoiceDomainHelper.LARGE_SECRET ||
                currentState == TrackerModel.MapSquareChoiceDomainHelper.MEDIUM_SECRET || currentState == TrackerModel.MapSquareChoiceDomainHelper.SMALL_SECRET))
        {
            // do nothing, this is a 'destructive' change that is perfectly reasonable and doesn't need a warning reminder
        }
        else
        {
            var row = (i + 1);
            var col = (char)((int)'A' + j);
            var changedCoords = $"Changed {col}{row}:";
            string desc(int state) => state == -1 ? "Unmarked" : spokenOWTiles[state];
            System.Drawing.Bitmap bmp(int state)
            {
                if (state == -1) return Graphics.unmarkedBmp;
                else if (state == TrackerModel.MapSquareChoiceDomainHelper.DARK_X) return Graphics.dontCareBmp;
                else return new OverworldMapTileCustomization.MapStateProxy(state).DefaultInteriorBmp();
            }
            if (currentState != originalState)  // don't remind e.g. change from bomb shop to bomb+key shop
            {
                SendReminderImpl(TrackerModel.ReminderCategory.OverworldOverwrites, $"You changed {col} {row} from {desc(originalState)} to {desc(currentState)}",
                    new Control[] { ReminderTextBox(changedCoords), upcb(bmp(originalState)), upcb(Graphics.iconRightArrow_bmp), upcb(bmp(currentState)) }, highlightAsyncComp);
            }
        }
    }

    public static void RemindSword2()
    {
        var n = TrackerModel.sword2Box.CellCurrent();
        if (n == -1)
        {
            SendReminder(TrackerModel.ReminderCategory.SwordHearts, "Consider getting the white sword item",
                            new Control[] { upcb(Graphics.iconRightArrow_bmp), upcb(new OverworldMapTileCustomization.MapStateProxy(14).DefaultInteriorBmp()) });
        }
        else
        {
            SendReminder(TrackerModel.ReminderCategory.SwordHearts, $"Consider getting the {TrackerModel.ITEMS.AsPronounceString(n)} from the white sword cave",
                            new Control[] { upcb(Graphics.iconRightArrow_bmp), upcb(new OverworldMapTileCustomization.MapStateProxy(14).DefaultInteriorBmp()),
                                upcb(CustomComboBoxes.boxCurrentBMP(TrackerModel.sword2Box.CellCurrent(), null)) });
        }
    }

    public static void RemindFoundDungeonCount(int n)
    {
        var icons = new List<Control> { upcb(Graphics.genericDungeonInterior_bmp), ReminderTextBox($"{n}/9") };
        if (n == 1)
            SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, "You have located one dungeon", icons);
        else if (n == 9)
        {
            if (TrackerModel.mapStateSummary.Sword2Location.Equals(TrackerModel.NOTFOUND)   // if sword2 cave not found on overworld map,
                    && TrackerModel.sword2Box.CellCurrent() == -1)                        // and the tracker box is still empty (some people might not mark map, but will mark item)
            {
                var greyedSword2 = upcb(Graphics.greyscale(Graphics.theInteriorBmpTable[14][0]));
                SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, "Congratulations, you have located all 9 dungeons, but the white sword cave is still missing",
                                icons.Append(greyedSword2).ToList());
            }
            else
                SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, "Congratulations, you have located all 9 dungeons", icons.Append(upcb(Graphics.iconCheckMark_bmp)).ToList());
        }
        else
            SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, $"You have located {n} dungeons", icons);
    }

    // (F#: asyncBrieflyHighlightAnOverworldLocation : (int*int) -> Async<unit>; the locations are the model's F# tuples)
    public static void RemindTriforceCount(int n, Func<Tuple<int, int>, Func<Task>> asyncBrieflyHighlightAnOverworldLocation)
    {
        // the number of triforces needed to enter level 9 (8 by default; the user can change it, or pick an item instead, in which case this stays 8)
        var req = TrackerModel.level9CustomTriggerItem() != -1 ? 8 : TrackerModel.level9TriforceCount;
        var icons = new List<Control> { upcb(Graphics.fullOrangeTriforce_bmp), ReminderTextBox($"{n}/{req}") };
        if (n == 1)
            SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, "You now have one triforce", icons);
        else
            SendReminder(TrackerModel.ReminderCategory.DungeonFeedback, $"You now have {n} triforces", n == req ? icons.Append(upcb(Graphics.iconCheckMark_bmp)).ToList() : icons);
        if (n == req && !(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Value()))
        {
            SendReminderImpl(TrackerModel.ReminderCategory.DungeonFeedback, "Consider the magical sword before dungeon nine", new Control[] { upcb(Graphics.iconRightArrow_bmp), upcb(Graphics.ow_sword3_item_bmp) },
                                asyncBrieflyHighlightAnOverworldLocation(TrackerModel.mapStateSummary.Sword3Location));
        }
        if (n == req && (!TrackerModel.mapStateSummary.DungeonLocations[8].Equals(TrackerModel.NOTFOUND)))
        {
            SendReminderImpl(TrackerModel.ReminderCategory.DungeonFeedback, "Dungeon nine is open", new Control[] { upcb(Graphics.iconRightArrow_bmp), upcb(new OverworldMapTileCustomization.MapStateProxy(8).DefaultInteriorBmp()) },
                                asyncBrieflyHighlightAnOverworldLocation(TrackerModel.mapStateSummary.DungeonLocations[8]));
        }
    }

    //This appears to be where the periodic reminders are set up
    //Any controls regarding frequency of reminders should be added here
    public class PeriodicReminders
    {
        private static readonly TimeSpan recentlyAgo = TimeSpan.FromMinutes(3.0);
        private readonly TrackerModel.LastChangedTime ladderTime = new TrackerModel.LastChangedTime(recentlyAgo), recorderTime = new TrackerModel.LastChangedTime(recentlyAgo),
            powerBraceletTime = new TrackerModel.LastChangedTime(recentlyAgo), boomstickTime = new TrackerModel.LastChangedTime(recentlyAgo), whiteSwordTime = new TrackerModel.LastChangedTime(recentlyAgo);
        private int owPreviouslyAnnouncedWhistleSpotsRemain = 0, owPreviouslyAnnouncedPowerBraceletSpotsRemain = 0, owPreviouslyAnnounceDoorRepairCount = 0;
        public PeriodicReminders()
        {
            // When the overworld quest switches, reset cached spot counts so the new quest's count fires on the very next interval
            TrackerModel.overworldQuestChanged.Publish.AddHandler(new FSharpHandler<Unit>((_, _) =>
            {
                owPreviouslyAnnouncedWhistleSpotsRemain = 0;
                owPreviouslyAnnouncedPowerBraceletSpotsRemain = 0;
            }));
        }
        public void Check()
        {
            var interval = (double)TrackerModelOptions.ReminderIntervalMinutes;
            // remind ladder (fires at 60% of base interval)
            if ((DateTime.Now - ladderTime.Time).TotalMinutes >= interval * 0.6)
            {
                if (TrackerModel.playerComputedStateSummary.HaveLadder)
                {
                    if (!(TrackerModel.playerComputedStateSummary.HaveCoastItem))
                    {
                        var n = TrackerModel.ladderBox.CellCurrent();
                        if (n == -1)
                            SendReminder(TrackerModel.ReminderCategory.CoastItem, "Get the coast item with the ladder", new Control[] { upcb(Graphics.ladder_bmp), upcb(Graphics.iconRightArrow_bmp) });
                        else
                        {
                            if (n == TrackerModel.ITEMS.WHITESWORD && TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Value())
                            {
                                // silly to ask to grab white sword if already have mags (though note this reminder could be useful in swordless when both are bomb upgrades)
                            }
                            else
                                SendReminder(TrackerModel.ReminderCategory.CoastItem, $"Get the {TrackerModel.ITEMS.AsPronounceString(n)} off the coast",
                                                new Control[] { upcb(Graphics.ladder_bmp), upcb(Graphics.iconRightArrow_bmp), upcb(CustomComboBoxes.boxCurrentBMP(TrackerModel.ladderBox.CellCurrent(), null)) });
                        }
                        ladderTime.SetNow();
                    }
                }
            }
            // remind whistle spots (fires at base interval)
            if ((DateTime.Now - recorderTime.Time).TotalMinutes >= interval)
            {
                if (TrackerModel.playerComputedStateSummary.HaveRecorder)
                {
                    var owWhistleSpotsRemain = TrackerModel.mapStateSummary.OwWhistleSpotsRemain.Count;
                    if (owWhistleSpotsRemain >= owPreviouslyAnnouncedWhistleSpotsRemain && owWhistleSpotsRemain > 0)
                    {
                        var icons = new Control[] { upcb(Graphics.recorder_bmp), ReminderTextBox(owWhistleSpotsRemain.ToString()) };
                        if (owWhistleSpotsRemain == 1)
                            SendReminder(TrackerModel.ReminderCategory.RecorderSpots, "There is one recorder spot", icons);
                        else
                            SendReminder(TrackerModel.ReminderCategory.RecorderSpots, $"There are {owWhistleSpotsRemain} recorder spots", icons);
                    }
                    recorderTime.SetNow();
                    owPreviouslyAnnouncedWhistleSpotsRemain = owWhistleSpotsRemain;
                }
            }
            // remind power bracelet spots (fires at base interval)
            if ((DateTime.Now - powerBraceletTime.Time).TotalMinutes >= interval)
            {
                if (TrackerModel.playerComputedStateSummary.HavePowerBracelet)
                {
                    if (TrackerModel.mapStateSummary.OwPowerBraceletSpotsRemain >= owPreviouslyAnnouncedPowerBraceletSpotsRemain && TrackerModel.mapStateSummary.OwPowerBraceletSpotsRemain > 0)
                    {
                        var icons = new Control[] { upcb(Graphics.power_bracelet_bmp), ReminderTextBox(TrackerModel.mapStateSummary.OwPowerBraceletSpotsRemain.ToString()) };
                        if (TrackerModel.mapStateSummary.OwPowerBraceletSpotsRemain == 1)
                            SendReminder(TrackerModel.ReminderCategory.PowerBraceletSpots, "There is one power bracelet spot", icons);
                        else
                            SendReminder(TrackerModel.ReminderCategory.PowerBraceletSpots, $"There are {TrackerModel.mapStateSummary.OwPowerBraceletSpotsRemain} power bracelet spots", icons);
                    }
                    powerBraceletTime.SetNow();
                    owPreviouslyAnnouncedPowerBraceletSpotsRemain = TrackerModel.mapStateSummary.OwPowerBraceletSpotsRemain;
                }
            }
            // remind boomstick book (fires at base interval)
            if ((DateTime.Now - boomstickTime.Time).TotalMinutes >= interval)
            {
                if (TrackerModel.playerComputedStateSummary.HaveWand && !(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBoomBook.Value()))
                {
                    var boomShopFound = false;
                    for (int i = 0; i <= 15; i++)
                    {
                        for (int j = 0; j <= 7; j++)
                        {
                            var cur = TrackerModel.overworldMapMarks[i, j].Current();
                            if (TrackerModel.MapSquareChoiceDomainHelper.IsItem(cur))
                            {
                                if (cur == TrackerModel.MapSquareChoiceDomainHelper.BOOK ||
                                        TrackerModel.getShopItem2_1based(i, j) == TrackerModel.MapSquareChoiceDomainHelper.ToItem(TrackerModel.MapSquareChoiceDomainHelper.BOOK) ||
                                        TrackerModel.getShopItem3_1based(i, j) == TrackerModel.MapSquareChoiceDomainHelper.ToItem(TrackerModel.MapSquareChoiceDomainHelper.BOOK))
                                    boomShopFound = true;
                            }
                        }
                    }
                    if (boomShopFound)
                    {
                        SendReminder(TrackerModel.ReminderCategory.BoomstickBook, "Consider buying the boomstick book", new Control[] { upcb(Graphics.iconRightArrow_bmp), upcb(Graphics.boom_book_bmp) });
                        boomstickTime.SetNow();
                    }
                }
            }
            // remind white sword (fires at base interval) — triggers when white sword cave is unmarked or item not yet obtained
            if ((DateTime.Now - whiteSwordTime.Time).TotalMinutes >= interval)
            {
                if (TrackerModel.playerComputedStateSummary.SwordLevel < 2 && TrackerModel.playerComputedStateSummary.PlayerHearts >= 4)  // player doesn't have white or magic sword, and has at least the minimum 4 hearts to lift the white sword
                {
                    if (TrackerModel.sword2Box.PlayerHas().Equals(TrackerModel.PlayerHas.NO))  // not yet obtained
                        RemindSword2();
                }
                whiteSwordTime.SetNow();
            }
            // remind door repair spots
            if (TrackerModel.mapSquareChoiceDomain.NumUses(TrackerModel.MapSquareChoiceDomainHelper.DOOR_REPAIR_CHARGE) > owPreviouslyAnnounceDoorRepairCount)
            {
                var n = TrackerModel.mapSquareChoiceDomain.NumUses(TrackerModel.MapSquareChoiceDomainHelper.DOOR_REPAIR_CHARGE);
                var max = TrackerModel.mapSquareChoiceDomain.MaxUses(TrackerModel.MapSquareChoiceDomainHelper.DOOR_REPAIR_CHARGE);
                var icons = new Control[] { upcb(Graphics.theInteriorBmpTable[TrackerModel.MapSquareChoiceDomainHelper.DOOR_REPAIR_CHARGE][0]), ReminderTextBox($"{n}/{max}") };
                SendReminder(TrackerModel.ReminderCategory.DoorRepair, $"You found {(n == max ? "all " : "")}{n} of {max} door repairs", icons);
                owPreviouslyAnnounceDoorRepairCount = n;
            }
        }
    }
}
