// Ported from Z1R_WPF/Timeline.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
// (F#: `module Timeline` holding `type Timeline`. As with CustomMessageBox, C# doesn't allow a nested type with the same name as its
// enclosing class (CS0542), so the module becomes a namespace: callers still write `Timeline.TimelineID.X`, `new Timeline.TimelineItem(...)`
// and `new Timeline.Timeline(...)`. The module's own values (isCurrentlyLoadingASave, canvasAdd, TLC, ...) are static members of
// `partial class Timeline`, split around TimelineID/TimelineItem to keep the file order, so WPF's `Timeline.isCurrentlyLoadingASave`
// becomes `Timeline.Timeline.isCurrentlyLoadingASave`.)
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;

namespace Z1R_Avalonia.Timeline
{
    public partial class Timeline
    {
        public static bool isCurrentlyLoadingASave = false;

        public static void canvasAdd(Canvas c, Control item, double left, double top) => Graphics.canvasAdd(c, item, left, top);
    }

    // (F#: a union, some of whose cases carry data. Here one class shaped like a compiled F# union: Tag / Tags.X, a static instance per
    // plain case (`TimelineID.MagicalSword`), a static method per data case (`TimelineID.LevelBox(i,j)`, as in F#), and structural
    // equality, so `tid == TimelineID.MagicalSword` and dictionary keys behave as in F#. Case data: LevelBox(Item1, Item2),
    // OWHeart(Item1), CustomIcon(Item1), UserCustom(Text). An F# match `| LevelBox(i,_) ->` becomes `case TimelineID.Tags.LevelBox: ... tid.Item1`.)
    public sealed class TimelineID : IEquatable<TimelineID>
    {
        public static class Tags
        {
            // "items"
            public const int WoodSword = 0;
            public const int WhiteSword = 1;             // the starting item (which is always a sword)   (Note that e.g. LevelBox(3,1) might me WHITESWORD which might be a BU)
            public const int MagicalSword = 2;           // the shopping area box (which might be a BU)
            public const int StartingMagicalSword = 3;   // the starting item (which is always a sword)
            public const int Boomerang = 4;
            public const int MagicBoomerang = 5;
            public const int WoodArrow = 6;
            public const int SilverArrow = 7;
            public const int BlueCandle = 8;
            public const int BlueRing = 9;
            public const int RedCandle = 10;
            public const int RedRing = 11;
            public const int BoomstickBook = 12;
            public const int Book = 13;
            public const int Gannon = 14;
            public const int Zelda = 15;
            public const int Bow = 16;
            public const int Wand = 17;
            public const int PowerBracelet = 18;
            public const int Raft = 19;
            public const int Recorder = 20;
            public const int AnyKey = 21;
            public const int Ladder = 22;
            // box locations
            public const int LadderBox = 23;
            public const int ArmosBox = 24;
            public const int MagicalSwordCaveBox = 25;
            public const int LetterCaveBox = 26;
            public const int WhiteSwordBox = 27;
            public const int Level1or4Box3 = 28;
            public const int LevelBox = 29;            // of int * int
            // triforces
            public const int Triforce1 = 30;
            public const int Triforce2 = 31;
            public const int Triforce3 = 32;
            public const int Triforce4 = 33;
            public const int Triforce5 = 34;
            public const int Triforce6 = 35;
            public const int Triforce7 = 36;
            public const int Triforce8 = 37;
            // take any
            public const int TakeAnyHeart1 = 38;
            public const int TakeAnyHeart2 = 39;
            public const int TakeAnyHeart3 = 40;
            public const int TakeAnyHeart4 = 41;
            // checked off on the overworld map
            public const int Sword4 = 42;
            public const int OWHeart = 43;             // of int
            public const int CustomIcon = 44;          // of int   // a custom icon (1-16) checked off on the overworld map
            // custom
            public const int UserCustom = 45;          // of string
        }
        private readonly int tag;
        private readonly int item1;
        private readonly int item2;
        private readonly string text;
        private TimelineID(int tag, int item1 = 0, int item2 = 0, string text = null) { this.tag = tag; this.item1 = item1; this.item2 = item2; this.text = text; }
        public int Tag => tag;
        public int Item1 => item1;
        public int Item2 => item2;
        public string Text => text;

        // "items"
        public static readonly TimelineID WoodSword = new TimelineID(Tags.WoodSword);
        public static readonly TimelineID WhiteSword = new TimelineID(Tags.WhiteSword);
        public static readonly TimelineID MagicalSword = new TimelineID(Tags.MagicalSword);
        public static readonly TimelineID StartingMagicalSword = new TimelineID(Tags.StartingMagicalSword);
        public static readonly TimelineID Boomerang = new TimelineID(Tags.Boomerang);
        public static readonly TimelineID MagicBoomerang = new TimelineID(Tags.MagicBoomerang);
        public static readonly TimelineID WoodArrow = new TimelineID(Tags.WoodArrow);
        public static readonly TimelineID SilverArrow = new TimelineID(Tags.SilverArrow);
        public static readonly TimelineID BlueCandle = new TimelineID(Tags.BlueCandle);
        public static readonly TimelineID BlueRing = new TimelineID(Tags.BlueRing);
        public static readonly TimelineID RedCandle = new TimelineID(Tags.RedCandle);
        public static readonly TimelineID RedRing = new TimelineID(Tags.RedRing);
        public static readonly TimelineID BoomstickBook = new TimelineID(Tags.BoomstickBook);
        public static readonly TimelineID Book = new TimelineID(Tags.Book);
        public static readonly TimelineID Gannon = new TimelineID(Tags.Gannon);
        public static readonly TimelineID Zelda = new TimelineID(Tags.Zelda);
        public static readonly TimelineID Bow = new TimelineID(Tags.Bow);
        public static readonly TimelineID Wand = new TimelineID(Tags.Wand);
        public static readonly TimelineID PowerBracelet = new TimelineID(Tags.PowerBracelet);
        public static readonly TimelineID Raft = new TimelineID(Tags.Raft);
        public static readonly TimelineID Recorder = new TimelineID(Tags.Recorder);
        public static readonly TimelineID AnyKey = new TimelineID(Tags.AnyKey);
        public static readonly TimelineID Ladder = new TimelineID(Tags.Ladder);
        // box locations
        public static readonly TimelineID LadderBox = new TimelineID(Tags.LadderBox);
        public static readonly TimelineID ArmosBox = new TimelineID(Tags.ArmosBox);
        public static readonly TimelineID MagicalSwordCaveBox = new TimelineID(Tags.MagicalSwordCaveBox);
        public static readonly TimelineID LetterCaveBox = new TimelineID(Tags.LetterCaveBox);
        public static readonly TimelineID WhiteSwordBox = new TimelineID(Tags.WhiteSwordBox);
        public static readonly TimelineID Level1or4Box3 = new TimelineID(Tags.Level1or4Box3);
        public static TimelineID LevelBox(int item1, int item2) => new TimelineID(Tags.LevelBox, item1, item2);
        // triforces
        public static readonly TimelineID Triforce1 = new TimelineID(Tags.Triforce1);
        public static readonly TimelineID Triforce2 = new TimelineID(Tags.Triforce2);
        public static readonly TimelineID Triforce3 = new TimelineID(Tags.Triforce3);
        public static readonly TimelineID Triforce4 = new TimelineID(Tags.Triforce4);
        public static readonly TimelineID Triforce5 = new TimelineID(Tags.Triforce5);
        public static readonly TimelineID Triforce6 = new TimelineID(Tags.Triforce6);
        public static readonly TimelineID Triforce7 = new TimelineID(Tags.Triforce7);
        public static readonly TimelineID Triforce8 = new TimelineID(Tags.Triforce8);
        // take any
        public static readonly TimelineID TakeAnyHeart1 = new TimelineID(Tags.TakeAnyHeart1);
        public static readonly TimelineID TakeAnyHeart2 = new TimelineID(Tags.TakeAnyHeart2);
        public static readonly TimelineID TakeAnyHeart3 = new TimelineID(Tags.TakeAnyHeart3);
        public static readonly TimelineID TakeAnyHeart4 = new TimelineID(Tags.TakeAnyHeart4);
        // checked off on the overworld map
        public static readonly TimelineID Sword4 = new TimelineID(Tags.Sword4);
        public static TimelineID OWHeart(int item) => new TimelineID(Tags.OWHeart, item);
        public static TimelineID CustomIcon(int item) => new TimelineID(Tags.CustomIcon, item);   // a custom icon (1-16) checked off on the overworld map
        // custom
        public static TimelineID UserCustom(string item) => new TimelineID(Tags.UserCustom, text: item);

        public static TimelineID Triforce(int n) =>
            n switch
            {
                1 => Triforce1,
                2 => Triforce2,
                3 => Triforce3,
                4 => Triforce4,
                5 => Triforce5,
                6 => Triforce6,
                7 => Triforce7,
                8 => Triforce8,
                _ => throw new Exception("bad Triforce(n)"),
            };
        public string Identifier =>
            this.Tag switch
            {
                Tags.WoodSword => "WoodSword",
                Tags.WhiteSword => "WhiteSword",
                Tags.MagicalSword => "MagicalSword",
                Tags.StartingMagicalSword => "StartingMagicalSword",
                Tags.Boomerang => "Boomerang",
                Tags.MagicBoomerang => "MagicBoomerang",
                Tags.WoodArrow => "WoodArrow",
                Tags.SilverArrow => "SilverArrow",
                Tags.BlueCandle => "BlueCandle",
                Tags.BlueRing => "BlueRing",
                Tags.RedCandle => "RedCandle",
                Tags.RedRing => "RedRing",
                Tags.BoomstickBook => "BoomstickBook",
                Tags.Book => "Book",
                Tags.Gannon => "Gannon",
                Tags.Zelda => "Zelda",
                Tags.Bow => "Bow",
                Tags.Wand => "Wand",
                Tags.PowerBracelet => "PowerBracelet",
                Tags.Raft => "Raft",
                Tags.Recorder => "Recorder",
                Tags.AnyKey => "AnyKey",
                Tags.Ladder => "Ladder",
                Tags.LadderBox => "LadderBox",
                Tags.ArmosBox => "ArmosBox",
                Tags.MagicalSwordCaveBox => "MagicalSwordCaveBox",
                Tags.LetterCaveBox => "LetterCaveBox",
                Tags.WhiteSwordBox => "WhiteSwordBox",
                Tags.Level1or4Box3 => "Level1or4Box3",
                Tags.LevelBox => $"Level{item1}Box{item2}",
                Tags.Triforce1 => "Triforce1",
                Tags.Triforce2 => "Triforce2",
                Tags.Triforce3 => "Triforce3",
                Tags.Triforce4 => "Triforce4",
                Tags.Triforce5 => "Triforce5",
                Tags.Triforce6 => "Triforce6",
                Tags.Triforce7 => "Triforce7",
                Tags.Triforce8 => "Triforce8",
                Tags.TakeAnyHeart1 => "TakeAnyHeart1",
                Tags.TakeAnyHeart2 => "TakeAnyHeart2",
                Tags.TakeAnyHeart3 => "TakeAnyHeart3",
                Tags.TakeAnyHeart4 => "TakeAnyHeart4",
                Tags.Sword4 => "Sword4",
                Tags.OWHeart => $"OWHeart{item1}",
                Tags.CustomIcon => $"CustomIcon{item1}",
                Tags.UserCustom => "UserCustom_" + text,
                _ => throw new Exception("impossible TimelineID"),
            };

        // (structural equality and printing, as an F# union has)
        public bool Equals(TimelineID other) => other is not null && tag == other.tag && item1 == other.item1 && item2 == other.item2 && text == other.text;
        public override bool Equals(object obj) => Equals(obj as TimelineID);
        public override int GetHashCode() => HashCode.Combine(tag, item1, item2, text);
        public static bool operator ==(TimelineID a, TimelineID b) => a is null ? b is null : a.Equals(b);
        public static bool operator !=(TimelineID a, TimelineID b) => !(a == b);
        public override string ToString() =>
            tag switch
            {
                Tags.LevelBox => $"LevelBox ({item1}, {item2})",
                Tags.OWHeart => $"OWHeart {item1}",
                Tags.CustomIcon => $"CustomIcon {item1}",
                Tags.UserCustom => $"UserCustom \"{text}\"",
                _ => Identifier,
            };
    }

    // (F#: f is a unit -> Bitmap function)
    public class TimelineItem
    {
        private readonly TimelineID tid;
        private readonly Func<System.Drawing.Bitmap> f;
        private readonly TrackerModel.TimelineItemModel model;
        public TimelineItem(TimelineID tid, Func<System.Drawing.Bitmap> f)
        {
            this.tid = tid;
            this.f = f;
            this.model = TrackerModel.TimelineItemModel.All[tid.Identifier];
        }
        public bool IsDone => model.FinishedTotalSeconds != -1;
        public int FinishedTotalSeconds => model.FinishedTotalSeconds;
        public System.Drawing.Bitmap Bmp => f();
        public TrackerModel.PlayerHas Has => model.Has;
        public TimelineID TID => tid;
    }

    public partial class Timeline
    {
        public static readonly IImmutableSolidColorBrush TLC = Brushes.SandyBrown;   // timeline color
        public static readonly double ICON_SPACING = 6.0;
        public static readonly double BIG_HASH = 15.0;
        public static readonly double LINE_THICKNESS = 3.0;
        private static readonly (int, int) _numTicks = (60, 5);
        public static readonly int numTicks = _numTicks.Item1;
        public static readonly int ticksPerHash = _numTicks.Item2;

        ////////////////////////////////////////////////////////////
        // (F#: type Timeline(iconSize, numRows, lineWidth, minutesPerTick, sevenTexts:string[], moreOptionsButton:Button, _drawButton:Button))

        private readonly double iconSize;
        private readonly int numRows;
        private readonly double lineWidth;
        private readonly int minutesPerTick;
        private readonly double topRowReserveWidth;
        private readonly double iconAreaHeight;
        private readonly Canvas timelineCanvas;
        private readonly Canvas graphCanvas;
        private readonly TextBox owAxisLabel;
        private readonly TextBox seedLabel;
        private readonly TextBox flagsLabel;
        private readonly Canvas itemCanvas;
        private readonly TextBox timeToolTip;
        private readonly Avalonia.Controls.Shapes.Line line1;
        private readonly Avalonia.Controls.Shapes.Line curTime;
        private int[,] iconAreaFilled;
        private double xf(double i) =>      i*(lineWidth/(double)numTicks);
        private double x(int i) => (double)i*(lineWidth/(double)numTicks);
        public Timeline(double iconSize, int numRows, double lineWidth, int minutesPerTick, string[] sevenTexts, Button moreOptionsButton, Button _drawButton)
        {
            this.iconSize = iconSize;
            this.numRows = numRows;
            this.lineWidth = lineWidth;
            this.minutesPerTick = minutesPerTick;
            topRowReserveWidth = moreOptionsButton.DesiredSize.Width-24.0;
            iconAreaHeight = (double)numRows*(iconSize+ICON_SPACING);
            timelineCanvas = new Canvas { Height = iconAreaHeight+BIG_HASH, Width = lineWidth };
            graphCanvas = new Canvas { Height = iconAreaHeight+BIG_HASH, Width = lineWidth };
            owAxisLabel = new TextBox { Text = "Overworld and Collection Timeline", Foreground = Brushes.DarkCyan, Background = Brushes.Transparent, TextAlignment = TextAlignment.Center, BorderThickness = new Thickness(0.0), FontSize = 12.0, FontWeight = FontWeight.Bold, IsHitTestVisible = false, IsReadOnly = true };
            seedLabel = new TextBox { Foreground = Brushes.Orange, Background = Brushes.Transparent, BorderThickness = new Thickness(0.0), FontSize = 12.0, FontWeight = FontWeight.Bold, IsHitTestVisible = false, IsReadOnly = true };   // top left of the timeline: seed (all digits)
            flagsLabel = new TextBox { Foreground = Brushes.Orange, Background = Brushes.Transparent, BorderThickness = new Thickness(0.0), FontSize = 12.0, FontWeight = FontWeight.Bold, IsHitTestVisible = false, IsReadOnly = true, TextAlignment = TextAlignment.Right };   // top right of the timeline: flags (letters and digits)
            itemCanvas = new Canvas { Height = iconAreaHeight+BIG_HASH, Width = lineWidth };
            timeToolTip = new TextBox { Foreground = Brushes.Orange, Background = Brushes.Black, BorderThickness = new Thickness(3.0), FontSize = 16.0, IsHitTestVisible = false };
            line1 = new Avalonia.Controls.Shapes.Line { StartPoint = new Point(0.0, iconAreaHeight+BIG_HASH/2.0), EndPoint = new Point(lineWidth, iconAreaHeight+BIG_HASH/2.0), Stroke = TLC, StrokeThickness = LINE_THICKNESS };
            curTime = new Avalonia.Controls.Shapes.Line { StartPoint = new Point(0.0, iconAreaHeight), EndPoint = new Point(0.0, iconAreaHeight+BIG_HASH), Stroke = Brushes.White, StrokeThickness = LINE_THICKNESS };
            iconAreaFilled = new int[(int)lineWidth + 1, numRows];
            {
                var tb = new TextBlock { Text = "0h", FontSize = 12.0, Foreground = TLC, Background = Brushes.Black };
                // (Avalonia's FormattedText has no NumberSubstitution/TextFormattingMode/PixelsPerDip arguments; it measures in DIPs)
                FormattedText mkft(string t) => new FormattedText(t, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                                                    new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
                                                    tb.FontSize, tb.Foreground);
                var n = 0;
                void placeTextLabel(double x)
                {
                    var ft = mkft(sevenTexts[n]);
                    if (ft.Height > iconSize)
                        Console.WriteLine("bad formatted text");
                    var ypos = iconAreaHeight + 6.0;
                    var xpos = x - ft.Width/2.0;
                    canvasAdd(timelineCanvas, new TextBlock { Text = sevenTexts[n], FontSize = 12.0, Foreground = TLC, Background = Brushes.Black }, xpos, ypos);
                    n = n + 1;
                }
                canvasAdd(timelineCanvas, graphCanvas, 0.0, 0.0);
                owAxisLabel.Width = lineWidth;   // horizontal title, centered across the top of the timeline
                Canvas.SetLeft(owAxisLabel, 0.0);
                Canvas.SetTop(owAxisLabel, 6.0);
                graphCanvas.Children.Add(owAxisLabel);
                Canvas.SetLeft(seedLabel, 0.0);
                Canvas.SetTop(seedLabel, 6.0);
                Canvas.SetRight(flagsLabel, 0.0);
                Canvas.SetTop(flagsLabel, 6.0);
                graphCanvas.Children.Add(seedLabel);
                graphCanvas.Children.Add(flagsLabel);
                void refreshSeedAndFlags()
                {
                    var show = TrackerModelOptions.DisplaySeedAndFlags.Value;
                    seedLabel.Text = (show && SaveAndLoad.lastKnownSeed != "" ? "Seed: " + SaveAndLoad.lastKnownSeed : "");
                    flagsLabel.Text = (show && SaveAndLoad.lastKnownFlags != "" ? "Flags: " + SaveAndLoad.lastKnownFlags : "");
                    // the title is centered in the open space between the seed (left) and the flags (right)
                    double textWidth(string t)
                    {
                        if (t == "") return 0.0;
                        else return (new FormattedText(t, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                                                new Typeface(seedLabel.FontFamily, seedLabel.FontStyle, FontWeight.Bold, seedLabel.FontStretch),
                                                12.0, Brushes.Orange)).Width + 10.0;   // (+10: the text box's own padding)
                    }
                    var leftEdge = seedLabel.Text == "" ? 0.0 : textWidth(seedLabel.Text) + 10.0;
                    var rightEdge = flagsLabel.Text == "" ? 0.0 : textWidth(flagsLabel.Text) + 10.0;
                    Canvas.SetLeft(owAxisLabel, leftEdge);
                    owAxisLabel.Width = Math.Max(0.0, (lineWidth - leftEdge - rightEdge));
                }
                refreshSeedAndFlags();
                SaveAndLoad.seedAndFlagsUpdated.Publish.AddHandler(new FSharpHandler<Unit>((_, _) => refreshSeedAndFlags()));
                canvasAdd(timelineCanvas, itemCanvas, 0.0, 0.0);
                for (int i = 0; i <= numTicks; i++)
                {
                    if (i%(2*ticksPerHash) == 0)
                    {
                        var line = new Avalonia.Controls.Shapes.Line { StartPoint = new Point(x(i)+0.5, iconAreaHeight), EndPoint = new Point(x(i)+0.5, iconAreaHeight+BIG_HASH), Stroke = TLC, StrokeThickness = LINE_THICKNESS };
                        canvasAdd(timelineCanvas, line, 0.0, 0.0);
                        placeTextLabel(x(i));
                    }
                    else if (i%(ticksPerHash) == 0)
                    {
                        var line = new Avalonia.Controls.Shapes.Line { StartPoint = new Point(x(i)+0.5, iconAreaHeight+BIG_HASH/4.0), EndPoint = new Point(x(i)+0.5, iconAreaHeight+BIG_HASH*3.0/4.0), Stroke = TLC, StrokeThickness = LINE_THICKNESS };
                        canvasAdd(timelineCanvas, line, 0.0, 0.0);
                    }
                }
                canvasAdd(timelineCanvas, line1, 0.0, 0.0);
                canvasAdd(timelineCanvas, curTime, 0.0, 0.0);
                // text labels
                for (int x = 0; x <= (int)topRowReserveWidth; x++)
                    iconAreaFilled[x, 0] = 99;
            }
        }
        public Canvas Canvas => timelineCanvas;
        public void ThisIsNowTheVisibleTimeline()
        {
            // (the Options and Draw buttons now live in the top-left button stack above Item Locations; see Layout.AddTopLeftButtons)
        }
        public void Update(int minute, IEnumerable<TimelineItem> timelineItems, int maxOverworldRemain)
        {
            if (!isCurrentlyLoadingASave)
            {
                var tick = minute / minutesPerTick;
                if (tick < 0 || tick > numTicks)
                {
                }
                else
                {
                    this.DrawGraph(tick, maxOverworldRemain);
                    this.DrawItemsAndGuidelines(timelineItems);
                    curTime.StartPoint = new Point(xf((double)(minute / minutesPerTick)) + 0.5, curTime.StartPoint.Y);
                    curTime.EndPoint = new Point(xf((double)(minute / minutesPerTick)) + 0.5, curTime.EndPoint.Y);
                }
            }
        }
        private void DrawGraph(int curTick, int maxOverworldRemain)
        {
            if (TrackerModel.timelineDataOverworldSpotsRemain.Count > 0)
            {
                // populate data to graph
                // (F#: Array.sortBy fst, which is FSharp.Core's Array.Sort(keys, items); done the same way so equal keys end up in the same order)
                var sorted = TrackerModel.timelineDataOverworldSpotsRemain.ToArray();
                Array.Sort(sorted.Select(t => t.Item1).ToArray(), sorted);
                var remainPerTick = Enumerable.Repeat(-1, numTicks+1).ToArray();
                var maxRemain = sorted.Max(t => t.Item2);
                remainPerTick[0] = maxRemain;   // other buckets are populated with most-recent-value-achieved-in-prior-minute, but for 0th minute, we want max value
                var highestTickPopulated = -1;
                foreach (var (s, r) in sorted)
                {
                    var tickBucket = 1 + (s/60)/minutesPerTick;
                    if (tickBucket >= 0 && tickBucket <= numTicks)
                    {
                        if (tickBucket > highestTickPopulated)
                        {
                            for (int i = highestTickPopulated+1; i <= tickBucket-1; i++)
                                remainPerTick[i] = i == 0 ? maxRemain : remainPerTick[i-1];
                            remainPerTick[tickBucket] = r;
                            highestTickPopulated = tickBucket;
                        }
                        else
                        {
                            System.Diagnostics.Debug.Assert(tickBucket == highestTickPopulated);
                            remainPerTick[tickBucket] = r;
                        }
                    }
                }
                if (curTick > highestTickPopulated)
                {
                    var r = sorted[sorted.Length-1].Item2;
                    for (int i = highestTickPopulated+1; i <= curTick; i++)
                    {
                        remainPerTick[i] = r;
                        highestTickPopulated = curTick;
                    }
                }
                // draw it
                graphCanvas.Children.Clear();
                graphCanvas.Children.Add(owAxisLabel);
                graphCanvas.Children.Add(seedLabel);
                graphCanvas.Children.Add(flagsLabel);
                double y(int r) => (iconAreaHeight / (double)maxOverworldRemain) * (double)r;
                for (int i = 1; i <= curTick; i++)
                {
                    if (remainPerTick[i-1] != -1 && remainPerTick[i] != -1)
                    {
                        var (x1, x2) = (xf((double)(i-1)), xf((double)(i)));
                        var (y1, y2) = (y(remainPerTick[i-1]), y(remainPerTick[i]));
                        var segment = new Avalonia.Controls.Shapes.Line { StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2), Stroke = Brushes.DarkCyan, StrokeThickness = 2.0 };
                        canvasAdd(graphCanvas, segment, 0.0, 0.0);
                    }
                }
            }
        }
        private void DrawItemsAndGuidelines(IEnumerable<TimelineItem> timelineItems)
        {
            // redraw guidelines and items
            itemCanvas.Children.Clear();
            var iconAreaFilled = (int[,])this.iconAreaFilled.Clone();  // grab fresh copy of fixed timeline labels, to mutate each time we redraw
            var buckets = new Dictionary<int, List<(System.Drawing.Bitmap, int, TrackerModel.PlayerHas, TimelineID)>>();
            // only the highest sword collected is shown (wood < white < magical < 4th sword), not every sword along the way
            int swordRank(TimelineID tid) =>
                tid.Tag switch
                {
                    TimelineID.Tags.WoodSword => 1,
                    TimelineID.Tags.WhiteSword => 2,
                    TimelineID.Tags.MagicalSword or TimelineID.Tags.StartingMagicalSword => 3,
                    TimelineID.Tags.Sword4 => 4,
                    _ => 0,
                };
            var topSwordRank = timelineItems.Where(ti => ti.IsDone).Select(ti => swordRank(ti.TID)).Aggregate(0, Math.Max);
            // (one icon for the top sword: the starting-item flag and the cave's item box can both be stamped for the same sword, so show just the earliest)
            var topSwordItem = topSwordRank == 0 ? null : timelineItems.Where(ti => ti.IsDone && swordRank(ti.TID) == topSwordRank).OrderBy(ti => ti.FinishedTotalSeconds).FirstOrDefault();
            var swordBoxHoldsSword = TrackerModel.sword3Box.IsDone() && TrackerModel.sword3Box.CellCurrent() == TrackerModel.ITEMS.MAGICALSWORD;
            foreach (var ti in timelineItems)
            {
                // (the magical sword cave's item box already shows that sword on the timeline: its 'have the sword' flag would draw it a second time)
                var dupOfCaveBox = swordBoxHoldsSword && (ti.TID.Tag switch { TimelineID.Tags.MagicalSword or TimelineID.Tags.StartingMagicalSword => true, _ => false });
                if (ti.IsDone && !dupOfCaveBox && (swordRank(ti.TID) == 0 || (topSwordItem != null ? object.ReferenceEquals(topSwordItem, ti) : false)))
                {
                    var tick = (int)((double)ti.FinishedTotalSeconds/(60.0*(double)minutesPerTick));
                    if (!(buckets.ContainsKey(tick)))
                        buckets.Add(tick, new List<(System.Drawing.Bitmap, int, TrackerModel.PlayerHas, TimelineID)>());
                    buckets[tick].Add((ti.Bmp, ti.FinishedTotalSeconds, ti.Has, ti.TID));
                }
            }
            for (int tick = 0; tick <= numTicks; tick++)
            {
                if (buckets.ContainsKey(tick))
                {
                    var rowBmps = new List<(int, (System.Drawing.Bitmap, int, TrackerModel.PlayerHas, TimelineID))>();
                    var (xminOrig, xmaxOrig) = (x(tick)-iconSize/2.0, x(tick)+iconSize/2.0);
                    var (xmin, xmax) = (Math.Max(0, (int)xminOrig), Math.Min((int)xmaxOrig, (int)lineWidth));
                    foreach (var bmp in buckets[tick])
                    {
                        var (bestRow, bestPenalty) = (0, 99999);
                        for (int row = numRows-1; row >= 0; row--)
                        {
                            var penalty = 0;
                            for (int x = xmin; x <= xmax; x++)
                                penalty = penalty + iconAreaFilled[x, row];
                            if (penalty < bestPenalty)
                            {
                                bestRow = row;
                                bestPenalty = penalty;
                            }
                        }
                        for (int x = xmin; x <= xmax; x++)
                            iconAreaFilled[x, bestRow] = iconAreaFilled[x, bestRow] + 1;
                        rowBmps.Add((bestRow, bmp));
                    }
                    if (rowBmps.Count > 0)
                    {
                        // guideline
                        var bottomRow = rowBmps.Max(rb => rb.Item1);
                        var line = new Avalonia.Controls.Shapes.Line { StartPoint = new Point(x(tick)+0.5, (double)(bottomRow+1)*(iconSize+ICON_SPACING)-ICON_SPACING), EndPoint = new Point(x(tick)+0.5, iconAreaHeight+BIG_HASH/2.0), Stroke = Brushes.Gray, StrokeThickness = LINE_THICKNESS };
                        canvasAdd(itemCanvas, line, 0.0, 0.0);
                        // items
                        foreach (var (row, (bmp, totalSeconds, has, tid)) in rowBmps)
                        {
                            var c = new Canvas { Width = iconSize, Height = iconSize };
                            Image make_img()
                            {
                                try { return Graphics.BMPtoImage((has.Equals(TrackerModel.PlayerHas.NO) ? Graphics.greyscale(bmp) : bmp)); }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"Timeline icon for {tid.Identifier} failed: {e}");
                                    var blank = new System.Drawing.Bitmap(bmp.Width, bmp.Height);
                                    return Graphics.BMPtoImage((has.Equals(TrackerModel.PlayerHas.NO) ? Graphics.greyscale(blank) : blank));
                                }
                            }
                            var img = make_img();
                            if (img.Width == 30.0 && iconSize == 21.0)
                            {
                                // a kludge to not make overworld hearts look so awful when scaled
                                img.Width = 20.0;
                                img.Height = 20.0;
                            }
                            else
                            {
                                img.Width = iconSize;
                                img.Height = iconSize;
                            }
                            c.Children.Add(img);
                            if (has.Equals(TrackerModel.PlayerHas.SKIPPED)) Graphics.placeSkippedItemXDecorationImpl(c, iconSize);
                            c.PointerEntered += (_, _) =>
                            {
                                timelineCanvas.Children.Remove(timeToolTip);
                                timeToolTip.Text = TimeSpan.FromSeconds((double)totalSeconds).ToString(@"hh\:mm\:ss") + "\n" + tid.Identifier;
                                var x = xminOrig;
                                x = Math.Min(x, (double)(16*16*3 - 120));  // don't go off right screen edge
                                canvasAdd(timelineCanvas, timeToolTip, x, (double)row*(iconSize+ICON_SPACING)-40.0);
                            };
                            c.PointerExited += (_, _) =>
                            {
                                timelineCanvas.Children.Remove(timeToolTip);
                            };
                            canvasAdd(itemCanvas, c, Math.Ceiling(xminOrig), (double)row*(iconSize+ICON_SPACING));
                        }
                    }
                }
            }
        }
    }
}
