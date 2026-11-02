// Ported from Z1R_WPF/DungeonHighlightsUI.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using Z1R_Tracker.Models.Z1R_TrackerInterop;

namespace Z1R_Avalonia;

public static class DungeonHighlightsUI
{
    public static void canvasAdd(Canvas c, Control item, double left, double top) => Graphics.canvasAdd(c, item, left, top);

    // (port helper: WPF's repeating `Animation.DoubleAnimation`, applied with `BeginAnimation(prop, anim)` and removed with `BeginAnimation(prop, null)`.
    // Avalonia's animation system can't be started/stopped per property like that, so, as Graphics.TileHighlightRectangle and Views' glow do, it's
    // tweened by hand on a 16 ms DispatcherTimer: linear (WPF's default), `from` -> `to` over `duration`, then back if `autoReverse`, forever.
    // One timer per animation drives every property it's begun on; removing a property sets it back to its base value, as WPF's does.)
    private sealed class RepeatingDoubleAnimation
    {
        private readonly double from, to, duration;
        private readonly bool autoReverse;
        private readonly List<Action<double>> targets = new List<Action<double>>();
        private readonly DispatcherTimer timer;
        private DateTime start;
        public RepeatingDoubleAnimation(double from, double to, TimeSpan duration, bool autoReverse)
        {
            this.from = from; this.to = to; this.duration = duration.TotalSeconds; this.autoReverse = autoReverse;
            timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16.0), DispatcherPriority.Render, (_, _) => tick());
            timer.Stop();
        }
        private double valueAt(double t)
        {
            var period = autoReverse ? 2.0 * duration : duration;
            var p = t % period;
            var f = p < duration ? p / duration : (period - p) / duration;
            return from + (to - from) * f;
        }
        private void tick()
        {
            var v = valueAt((DateTime.Now - start).TotalSeconds);
            foreach (var set in targets) set(v);
        }
        public void Begin(Action<double> set)
        {
            if (targets.Count == 0)
            {
                start = DateTime.Now;
                timer.Start();
            }
            targets.Add(set);
            set(from);
        }
        public void Remove(Action<double> set, double baseValue)
        {
            if (targets.Remove(set))
            {
                set(baseValue);
                if (targets.Count == 0) timer.Stop();
            }
        }
    }

    // (blockersHoverEvent: WPF passes WPFUI's `Event<bool>`; see Graphics.SharedEvent)
    public static void makeHighlights(int level, TabControl dungeonTabs, Canvas dungeonBodyHighlightCanvas, Canvas rightwardCanvas,
                            DungeonRoomState[,] roomStates, int[] usedTransports, int[] currentOutlineDisplayState,
                            Dungeon.Door[,] horizontalDoors, Dungeon.Door[,] verticalDoors, Graphics.SharedEvent<bool> blockersHoverEvent)
    {
        var startAnimationFuncs = new List<Action>();
        var endAnimationFuncs = new List<Action>();

        var setupCanvas = new Canvas();
        var finishedSetup = false;
        var anim = new RepeatingDoubleAnimation(1.0, 1.3, TimeSpan.FromSeconds(0.75), autoReverse: true);   // (WPF: RepeatBehavior.Forever, AutoReverse = true)
        // horizontal doors
        var horizontalDoorHighlights = new Rectangle[7, 8];
        for (int i = 0; i <= 6; i++)
        {
            for (int j = 0; j <= 7; j++)
            {
                var d = new Canvas { Width = 12.0, Height = 16.0, IsHitTestVisible = false };
                var rect = new Rectangle { Width = 12.0, Height = 16.0, Stroke = Brushes.DarkTurquoise, StrokeThickness = 2.0, Fill = Dungeon.unknown, Opacity = 0.0 };
                // (WPF: ScaleTransform's CenterX/CenterY; Avalonia's ScaleTransform has none, the centre is the element's RenderTransformOrigin)
                var st = new ScaleTransform(1.0, 1.0);
                rect.RenderTransformOrigin = new RelativePoint(d.Width / 2.0, d.Height / 2.0, RelativeUnit.Absolute);
                rect.RenderTransform = st;
                Action<double> setScaleX = v => st.ScaleX = v;
                Action<double> setScaleY = v => st.ScaleY = v;
                startAnimationFuncs.Add(() => anim.Begin(setScaleX));
                endAnimationFuncs.Add(() => anim.Remove(setScaleX, 1.0));
                startAnimationFuncs.Add(() => anim.Begin(setScaleY));
                endAnimationFuncs.Add(() => anim.Remove(setScaleY, 1.0));
                d.Children.Add(rect);
                horizontalDoorHighlights[i, j] = rect;
                canvasAdd(setupCanvas, d, (double)(i * (39 + 12) + 39), (double)(j * (27 + 12) + 6));
            }
        }
        // vertical doors
        var verticalDoorHighlights = new Rectangle[8, 7];
        for (int i = 0; i <= 7; i++)
        {
            for (int j = 0; j <= 6; j++)
            {
                var d = new Canvas { Width = 24.0, Height = 12.0, IsHitTestVisible = false };
                var rect = new Rectangle { Width = 24.0, Height = 12.0, Stroke = Brushes.DarkTurquoise, StrokeThickness = 2.0, Fill = Dungeon.unknown, Opacity = 0.0 };
                var st = new ScaleTransform(1.0, 1.0);
                rect.RenderTransformOrigin = new RelativePoint(d.Width / 2.0, d.Height / 2.0, RelativeUnit.Absolute);
                rect.RenderTransform = st;
                Action<double> setScaleX = v => st.ScaleX = v;
                Action<double> setScaleY = v => st.ScaleY = v;
                startAnimationFuncs.Add(() => anim.Begin(setScaleX));
                endAnimationFuncs.Add(() => anim.Remove(setScaleX, 1.0));
                startAnimationFuncs.Add(() => anim.Begin(setScaleY));
                endAnimationFuncs.Add(() => anim.Remove(setScaleY, 1.0));
                d.Children.Add(rect);
                verticalDoorHighlights[i, j] = rect;
                canvasAdd(setupCanvas, d, (double)(i * (39 + 12) + 8), (double)(j * (27 + 12) + 27));
            }
        }

        var len = 2.2;
        var anim2 = new RepeatingDoubleAnimation(0.0, len + len, TimeSpan.FromSeconds(0.75), autoReverse: false);   // (F# shadows `anim`; RepeatBehavior.Forever)
        // rooms
        var roomHighlights = new Ellipse[8, 8];
        for (int i = 0; i <= 7; i++)
        {
            for (int j = 0; j <= 7; j++)
            {
                var brush = Brushes.Magenta;
                var extra = 6;
                var ellipse = new Ellipse { Width = (double)(13 * 3 + 12 + 2 * extra), Height = (double)(9 * 3 + 12 + 2 * extra), Stroke = brush, StrokeThickness = 6.0, IsHitTestVisible = false, Opacity = 0.0 };
                ellipse.StrokeDashArray = new AvaloniaList<double>(new[] { len, len });
                Action<double> setDashOffset = v => ellipse.StrokeDashOffset = v;
                startAnimationFuncs.Add(() => anim2.Begin(setDashOffset));
                endAnimationFuncs.Add(() => anim2.Remove(setDashOffset, 0.0));
//            let canim = new Animation.ColorAnimation(Colors.Magenta, Colors.Cyan, new Duration(System.TimeSpan.FromSeconds(0.75)))
//            canim.AutoReverse <- true
//            canim.RepeatBehavior <- Animation.RepeatBehavior.Forever
//            brush.BeginAnimation(SolidColorBrush.ColorProperty, canim)
                canvasAdd(setupCanvas, ellipse, (double)(i * 51 - 12 / 2 - extra), (double)(j * 39 - 12 / 2 - extra));
                roomHighlights[i, j] = ellipse;
            }
        }
        int isThereARoom(int x, int y)  // 0=no, 1=yes, 2=maybe
        {
            if (roomStates[x, y].RoomType == RoomType.OffTheMap)
                return 0;
            else if (roomStates[x, y].RoomType != RoomType.Unmarked)
                return 1;
            else  // is Unmarked, use other context
            {
                // never a room behind lobby arrow
                if (y < 7 && roomStates[x, y + 1].RoomType == RoomType.StartEnterFromN)
                    return 0;
                else if (y > 0 && roomStates[x, y - 1].RoomType == RoomType.StartEnterFromS)
                    return 0;
                else if (x < 7 && roomStates[x + 1, y].RoomType == RoomType.StartEnterFromW)
                    return 0;
                else if (x > 0 && roomStates[x - 1, y].RoomType == RoomType.StartEnterFromE)
                    return 0;
                else
                {
                    // use the vanilla map outline the user has chosen, if any
                    var cur = currentOutlineDisplayState[level - 1];
                    if (cur == 0)
                        return 2;
                    else
                    {
                        var data =
                            cur <= 9
                                ? DungeonData.firstQuest[cur - 1]    // 1-9 is 1Q
                                : DungeonData.secondQuest[cur - 10]; // 10-18 is 2Q
                        if (data[y][x] == 'X')
                            return 2; // there is a room there, but the player has not marked the room as existing on map, and so we want to make it a possible bomb target to get into, so report 'maybe'
                        else
                            return 0;
                    }
                }
            }
        }
        int isThereANonZeldaRoom(int x, int y) => roomStates[x, y].RoomType == RoomType.Zelda ? 0 : isThereARoom(x, y);
        var isCurrentlyHighlighting = false;
        void highlight()
        {
            if (isCurrentlyHighlighting)
                return;
            isCurrentlyHighlighting = true;
            var anyFound = false;
            // possible bomb walls
            for (int i = 0; i <= 6; i++)
                for (int j = 0; j <= 7; j++)
                    if (horizontalDoors[i, j].State == Dungeon.DoorState.UNKNOWN)
                        if (isThereANonZeldaRoom(i, j) + isThereANonZeldaRoom(i + 1, j) == 3) // one yes and one maybe
                        {
                            if (level == 9 && (roomStates[i, j].RoomType == RoomType.StartEnterFromS || roomStates[i + 1, j].RoomType == RoomType.StartEnterFromS))
                            { } // do nothing, left/right walls of L9 lobby unbombable
                            else
                            {
                                horizontalDoorHighlights[i, j].Opacity = 1.0;
                                anyFound = true;
                            }
                        }
            for (int i = 0; i <= 7; i++)
                for (int j = 0; j <= 6; j++)
                    if (verticalDoors[i, j].State == Dungeon.DoorState.UNKNOWN)
                        if (isThereANonZeldaRoom(i, j) + isThereANonZeldaRoom(i, j + 1) == 3) // one yes and one maybe
                        {
                            // npc hints & bomb upgrades never can bomb north
                            if (roomStates[i, j + 1].RoomType != RoomType.OldManHint && roomStates[i, j + 1].RoomType != RoomType.BombUpgrade)
                            {
                                verticalDoorHighlights[i, j].Opacity = 1.0;
                                anyFound = true;
                            }
                        }
            // rooms with possible continuations
            for (int i = 0; i <= 7; i++)
            {
                for (int j = 0; j <= 7; j++)
                {
                    // an incomplete room that might have a push-block may yet reveal a transport
                    if (roomStates[i, j].RoomType == RoomType.MaybePushBlock && !(roomStates[i, j].IsComplete))
                    {
                        roomHighlights[i, j].Opacity = 1.0;
                        anyFound = true;
                    }
                    // a '?' stair may have been forgotten to traverse
                    if (roomStates[i, j].RoomType == RoomType.StaircaseToUnknown)
                    {
                        roomHighlights[i, j].Opacity = 1.0;
                        anyFound = true;
                    }
                    // only marked one of a transport pair
                    switch (roomStates[i, j].RoomType.KnownTransportNumber())
                    {
                        case int n when usedTransports[n] == 1:
                            roomHighlights[i, j].Opacity = 1.0;
                            anyFound = true;
                            break;
                        default: break;
                    }


                    // blocked? un-traversed doorway (could be key, moat, just forgotten, ...)
                    if (isThereARoom(i, j) == 2)
                    {
                        if (i > 0 && horizontalDoors[i - 1, j].IsTraversible ||
                                i < 7 && horizontalDoors[i, j].IsTraversible ||
                                j > 0 && verticalDoors[i, j - 1].IsTraversible ||
                                j < 7 && verticalDoors[i, j].IsTraversible)
                        {
                            roomHighlights[i, j].Opacity = 1.0;
                            anyFound = true;
                        }
                    }
                }
            }
            // Note: ladder blocks are kind of implicit, you either mark a door behind a moat, or it would show as a potential bomb wall
            if (anyFound)
            {
                foreach (var f in startAnimationFuncs)
                    f();
                rightwardCanvas.Children.Clear();
                var blockersOutline = new Rectangle { Width = 106.0, Height = 38.0, Stroke = Brushes.DarkCyan, StrokeThickness = 3.0, IsHitTestVisible = false };
                rightwardCanvas.Children.Add(blockersOutline);
                var explain = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap, FontSize = 14.0, Foreground = Brushes.Gray, Background = Brushes.Black, IsHitTestVisible = false, Margin = new Thickness(3.0),
                    Text = @"Where else can I go?

Mouse-hovering BLOCKERS for a few seconds displays these ""possible continuations"" in the dungeon.

Walls you might still need to try bombing are marked Cyan.

Rooms that might contain un-taken transport stairs, or that you have not visited, are circled Magenta."
                };
                var b = new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(3.0), Child = explain, Width = 310.0, Background = Brushes.Black };
                Canvas.SetBottom(b, 0.0);
                rightwardCanvas.Children.Add(b);
            }
        }
        void unhighlight()
        {
            if (isCurrentlyHighlighting)
            {
                isCurrentlyHighlighting = false;
                for (int i = 0; i <= 6; i++)
                    for (int j = 0; j <= 7; j++)
                        horizontalDoorHighlights[i, j].Opacity = 0.0;
                for (int i = 0; i <= 7; i++)
                    for (int j = 0; j <= 6; j++)
                        verticalDoorHighlights[i, j].Opacity = 0.0;
                for (int i = 0; i <= 7; i++)
                    for (int j = 0; j <= 7; j++)
                        roomHighlights[i, j].Opacity = 0.0;
                foreach (var f in endAnimationFuncs)
                    f();
                rightwardCanvas.Children.Clear();
            }
        }
        blockersHoverEvent.Fired += b =>
        {
            if (level - 1 == dungeonTabs.SelectedIndex)
            {
                if (b)
                {
                    if (!TrackerModelOptions.RaceMode.Value)
                    {
                        if (!finishedSetup)
                        {
                            finishedSetup = true;
                            dungeonBodyHighlightCanvas.Children.Add(setupCanvas);
                        }
                        highlight();
                    }

                }
                else
                    unhighlight();
            }
        };
        dungeonTabs.SelectionChanged += (_, _) => unhighlight();   // so that we stop running animation if they hover BLOCKERS and then e.g. hotkey to switch tabs
    }
}
