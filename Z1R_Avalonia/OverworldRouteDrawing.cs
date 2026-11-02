// Ported from Z1R_WPF/OverworldRouteDrawing.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
// (OverworldRouting is Gob's F# module from Z1R_Tracker_Core: Vertex, screenTypes, adjacencyDict, ... keep their F# types.)
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using static OverworldRouting;

namespace Z1R_Avalonia;

public static class OverworldRouteDrawing
{
    public static readonly double OMTW = Graphics.OMTW;
    public static void canvasAdd(Canvas c, Control item, double left, double top) => Graphics.canvasAdd(c, item, left, top);

    public class RouteDrawingLayer
    {
        private readonly Canvas routeDrawingCanvas = new Canvas { Width = 16.0 * OMTW, Height = (double)(8 * 11 * 3), IsHitTestVisible = false };
        private readonly Graphics.TileHighlightRectangle[,] highlightTiles = make_highlightTiles();
        private static Graphics.TileHighlightRectangle[,] make_highlightTiles()
        {
            var a = new Graphics.TileHighlightRectangle[16, 8];
            for (int i = 0; i < 16; i++)
                for (int j = 0; j < 8; j++)
                    a[i, j] = new Graphics.TileHighlightRectangle();
            return a;
        }
        private readonly Canvas routingMarksCanvas = new Canvas { Width = 16.0 * OMTW, Height = (double)(8 * 11 * 3) };
        private Canvas routeMarksHolder = null;  // Clear()ing hundreds of marks is expensive; instead we store them all in a holder, and then just discard the holder
        public RouteDrawingLayer()
        {
            // these permanently live in the routeDrawingCanvas
            for (int i = 0; i <= 15; i++)
                for (int j = 0; j <= 7; j++)
                    canvasAdd(routeDrawingCanvas, highlightTiles[i, j].Shape, OMTW * (double)i, (double)(j * 11 * 3));
            canvasAdd(routeDrawingCanvas, routingMarksCanvas, 0.0, 0.0);    // we want the lines drawn atop everything else
        }
        public void ClearHighlights()
        {
            for (int i = 0; i <= 15; i++)
                for (int j = 0; j <= 7; j++)
                    highlightTiles[i, j].Hide();
        }
        public void ClearRouteMarks()
        {
            routingMarksCanvas.Children.Clear();
            routeMarksHolder = null;
        }
        public void Clear() { this.ClearHighlights(); this.ClearRouteMarks(); }
        public void AddRouteMark(Control m)
        {
            if (routeMarksHolder == null)
            {
                routeMarksHolder = new Canvas();
                routingMarksCanvas.Children.Add(routeMarksHolder);
            }
            routeMarksHolder.Children.Add(m);
        }
        public Graphics.TileHighlightRectangle GetHighlightTile(int i, int j) => highlightTiles[i, j];
        public void AddSelfTo(Canvas c) => canvasAdd(c, routeDrawingCanvas, 0.0, 0.0);
    }
    public static readonly RouteDrawingLayer routeDrawingLayer = new RouteDrawingLayer();

    public static void doComputedDrawing(IEnumerable<(int, int, bool)> toHighlight, Func<int, int, bool> whatToCyan, IEnumerable<Control> accumulatedLines)
    {
        foreach (var (i, j, bright) in toHighlight)
        {
            var thr = routeDrawingLayer.GetHighlightTile(i, j);
            var cur = TrackerModel.overworldMapMarks[i, j].Current();
            if (cur >= 0 && cur <= 7) // most callers pass in owUnmarked equal to TrackerModel.overworldMapMarks, but some pass dungeon letters A-H as unmarked too, color them accessible
            {
                if (bright)
                    thr.MakeGreen();
                else
                    thr.MakePaleGreen();
            }
            else if (!TrackerModel.mapStateSummary.OwGettableLocations[i, j])
            {
                if (bright)
                    thr.MakeRed();  // many callers pass in routeworthy meaning 'acccesible & interesting', but some just pass 'interesting' and here is how we display 'inaccesible'
                else
                    thr.MakePaleRed();
            }
            else if (TrackerModel.owInstance.SometimesEmpty(i, j))
            {
                if (bright)
                    thr.MakeYellow();
                else
                    thr.MakePaleYellow();
            }
            else
            {
                if (bright)
                    thr.MakeGreen();
                else
                    thr.MakePaleGreen();
            }
        }
        // cyan overrides all
        for (int i = 0; i <= 15; i++)
            for (int j = 0; j <= 7; j++)
                if (whatToCyan(i, j))
                    routeDrawingLayer.GetHighlightTile(i, j).MakeCyan();
        foreach (var line in accumulatedLines)
            routeDrawingLayer.AddRouteMark(line);
    }

    public static (double, double) coords(Vertex v)
    {
        var (x, y, p) = (v.Item1, v.Item2, v.Item3);
        var cx = (double)(x * (int)OMTW + 8 * 3);
        var cy = (double)(y * 11 * 3 + 5 * 3);
        return p.Tag switch
        {
            OverworldScreenPortion.Tags.FULL => (cx, cy),
            OverworldScreenPortion.Tags.NORTH => (cx + 4.0, cy - 8.0),  // north and west
            OverworldScreenPortion.Tags.SOUTH => (cx - 6.0, cy + 8.0),  // south and east
            OverworldScreenPortion.Tags.EAST => (cx + 12.0, cy),
            OverworldScreenPortion.Tags.WEST => (cx - 12.0, cy),
            _ => throw new Exception("impossible portion"),
        };
    }

    public static Avalonia.Controls.Shapes.Line makeLine(Vertex v1, Vertex v2, IBrush color)
    {
        var (x1, y1) = coords(v1);
        var (x2, y2) = coords(v2);
        var line = new Avalonia.Controls.Shapes.Line { StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2), Stroke = color, StrokeThickness = 3.0, IsHitTestVisible = false };
        return line;
    }
    public static Avalonia.Controls.Shapes.Path makeCurve(Vertex v1, Vertex v2, IBrush color)
    {
        var (x1, y1) = coords(v1);
        var (x2, y2) = coords(v2);
        var segments = new PathSegments();
        segments.Add(new BezierSegment { Point1 = new Point(x1, y1 - 12.0), Point2 = new Point(x2, y2 - 12.0), Point3 = new Point(x2, y2), IsStroked = true });
        var pf = new PathFigure { StartPoint = new Point(x1, y1), Segments = segments, IsClosed = false };
        var figures = new PathFigures();
        figures.Add(pf);
        var curve = new Avalonia.Controls.Shapes.Path { Stroke = color, StrokeThickness = 3.0, IsHitTestVisible = false, Data = new PathGeometry { Figures = figures } };
        return curve;
    }

    public static readonly int MaxGYR = 12; // default
    public static readonly int All = 128;
    public static readonly SolidColorBrush color1    = Graphics.freeze(new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)));
    public static readonly SolidColorBrush color2    = Graphics.freeze(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)));
    public static readonly SolidColorBrush color3    = Graphics.freeze(new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)));
    public static readonly SolidColorBrush color4    = Graphics.freeze(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)));
    public static readonly SolidColorBrush color5    = Graphics.freeze(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)));
    public static readonly SolidColorBrush color6    = Graphics.freeze(new SolidColorBrush(Color.FromArgb( 85, 255, 255, 255)));
    // when routing to a specific chosen destination (via the Link icon), draw the path in hot pink instead of white so it's easy to see
    public static bool specificTargetMode = false;
    public static readonly SolidColorBrush pink2 = Graphics.freeze(new SolidColorBrush(Color.FromArgb(235, 255, 20, 147)));
    public static readonly SolidColorBrush pink3 = Graphics.freeze(new SolidColorBrush(Color.FromArgb(225, 255, 20, 147)));
    public static readonly SolidColorBrush pink4 = Graphics.freeze(new SolidColorBrush(Color.FromArgb(210, 255, 20, 147)));
    public static readonly SolidColorBrush pink5 = Graphics.freeze(new SolidColorBrush(Color.FromArgb(195, 255, 20, 147)));
    public static readonly SolidColorBrush pink6 = Graphics.freeze(new SolidColorBrush(Color.FromArgb(180, 255, 20, 147)));
    public static readonly SolidColorBrush colorAlt1 = Graphics.freeze(new SolidColorBrush(Color.FromArgb(230, 160, 220, 255)));
    public static readonly SolidColorBrush colorAlt2 = Graphics.freeze(new SolidColorBrush(Color.FromArgb(200, 160, 220, 255)));
    public static readonly SolidColorBrush colorAlt3 = Graphics.freeze(new SolidColorBrush(Color.FromArgb(175, 160, 220, 255)));
    public static readonly SolidColorBrush colorAlt4 = Graphics.freeze(new SolidColorBrush(Color.FromArgb(150, 160, 220, 255)));
    public static readonly SolidColorBrush colorAlt5 = Graphics.freeze(new SolidColorBrush(Color.FromArgb(125, 160, 220, 255)));
    public static readonly SolidColorBrush colorAlt6 = Graphics.freeze(new SolidColorBrush(Color.FromArgb(100, 160, 220, 255)));
    // (WPF: mousePos is a System.Windows.Point; whatToCyan is a function of (i,j))
    public static void drawPathsImpl(bool[,] owRouteworthySpots, bool[,] owUnmarked, Point mousePos, int i, int j, bool drawRouteMarks, bool fadeOut, int maxBoldGYR, bool showPale, Func<int, int, bool> whatToCyan)
    {
//    printfn "ORD:drawPathsImpl"
        routeDrawingLayer.Clear();
        var ok = screenTypes.TryGetValue(Tuple.Create(i, j), out var st);
        if (!ok)
        {
            throw new Exception("missing st");
        }
        else if (drawRouteMarks == false && maxBoldGYR == 0)
        {
            if (showPale)
            {
                // if we are not drawing routes and are not highlighting nearby, then we can do the cheap thing:
                var toHighlight = new List<(int, int, bool)>();
                var accumulatedLines = new List<Control>();
                for (int i_ = 0; i_ <= 15; i_++)   // (C#: i_, j_ because the loop can't shadow the parameters i, j)
                    for (int j_ = 0; j_ <= 7; j_++)
                        if (owRouteworthySpots[i_, j_] && owUnmarked[i_, j_])
                            toHighlight.Add((i_, j_, false));
                doComputedDrawing(toHighlight, whatToCyan, accumulatedLines);
            }
        }
        else
        {
            var v = st.Tag switch
            {
                ScreenType.Tags.WHOLE => Vertex.NewVertex(i, j, OverworldScreenPortion.FULL),
                ScreenType.Tags.NS => Vertex.NewVertex(i, j, mousePos.Y > (double)(11 * 3 / 2) ? OverworldScreenPortion.SOUTH : OverworldScreenPortion.NORTH),
                ScreenType.Tags.EW => Vertex.NewVertex(i, j, mousePos.X > (double)((int)OMTW / 2) ? OverworldScreenPortion.EAST : OverworldScreenPortion.WEST),
                _ => throw new Exception("impossible screen type"),
            };
            var goal = Vertex.NewVertex(-1, -1, OverworldScreenPortion.FULL); // non-existent goal will map all reachable locations
            SolidColorBrush color(int cost)
            {
                // white path that is bright near the cursor, but opacity falls off quickly as you get more cost-distance away
                //if   cost <=  4 then color1   // too bright
                if (specificTargetMode)
                {
                    if      (cost <=  8) return pink2;
                    else if (cost <= 14) return pink3;
                    else if (cost <= 22) return pink4;
                    else if (cost <= 30) return pink5;
                    else                 return pink6;
                }
                else
                {
                    if      (cost <=  8) return color2;
                    else if (cost <= 14) return color3;
                    else if (cost <= 22) return color4;
                    else if (cost <= 30) return color5;
                    else                 return color6;
                }
            }
            SolidColorBrush colorAlt(int cost)
            {
                // white path that is bright near the cursor, but opacity falls off quickly as you get more cost-distance away
                //if   cost <=  4 then colorAlt1   // too bright
                if      (cost <=  8) return colorAlt2;
                else if (cost <= 14) return colorAlt3;
                else if (cost <= 22) return colorAlt4;
                else if (cost <= 30) return colorAlt5;
                else                 return colorAlt6;
            }
            var d = findAllBestPaths(adjacencyDict, v, goal);
            var visited = new HashSet<Vertex>();
            var accumulatedLines = new List<Avalonia.Controls.Shapes.Shape>();
            void draw(Vertex g)
            {
                if (visited.Add(g))
                {
                    var ok = d.TryGetValue(g, out var r);
                    if (ok)
                    {
                        var (cost, preds) = (r.Item1, r.Item2);
                        foreach (var p in preds)
                        {
                            bool isRecorderWarpDestination(Vertex vtx)
                            {
                                var (x, y) = (vtx.Item1, vtx.Item2);
                                var r = false;
                                foreach (var ij in recorderDests)
                                    if (x == ij.Item1 && y == ij.Item2)
                                        r = true;
                                return r;
                            }
                            bool isWarpDestination(Vertex vtx)
                            {
                                var (x, y) = (vtx.Item1, vtx.Item2);
                                var r = false;
                                foreach (var ij in anyRoads)
                                    if (x == ij.Item1 && y == ij.Item2)
                                        r = true;
                                return r || isRecorderWarpDestination(vtx);
                            }
                            bool make_canWalk()
                            {
                                var ok = adjacencyDictSansWarps.TryGetValue(p, out var xs);
                                if (ok)
                                    return xs.Any(vc => vc.Item1.Equals(g));
                                else
                                    return false;
                            }
                            var canWalk = make_canWalk();
                            var preferLadder =
                                TrackerModel.playerComputedStateSummary.HaveLadder && (
                                    (p.Item1 == 7 && p.Item2 == 1 && g.Item1 == 7 && g.Item2 == 1) ? true :
                                    (p.Item1 == 7 && p.Item2 == 2 && g.Item1 == 7 && g.Item2 == 2) ? true :
                                    false
                                    );
                            if (drawRouteMarks)
                            {
                                if (isRecorderWarpDestination(g) && !canWalk)
                                {
                                    // don't bother drawing lines to every recorder warp destination - is patently obvious and just clutters screen
                                }
                                else if ((isWarpDestination(p) || isWarpDestination(g)) && !canWalk)
                                {
                                    // non-walkable any road warps get a light dashed line
                                    var (x1, y1) = coords(g);
                                    var (x2, y2) = coords(p);
                                    var line = new Avalonia.Controls.Shapes.Line { StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2), Stroke = color(999), StrokeThickness = 3.0, IsHitTestVisible = false };
                                    line.StrokeDashArray = new AvaloniaList<double>();  // (Avalonia: null until set)
                                    line.StrokeDashArray.Add(1.0);  // number of thicknesses on...
                                    line.StrokeDashArray.Add(1.0);  // number of thicknesses off...
                                    accumulatedLines.Add(line);
                                }
                                else if (allPossibleScreenScrolls.Contains(Tuple.Create(p, g)) && !(preferLadder))
                                {
                                    if (p.Equals(Vertex.NewVertex(0, 6, OverworldScreenPortion.FULL)) && g.Equals(Vertex.NewVertex(15, 5, OverworldScreenPortion.FULL)))   // world wrap
                                    {
                                        accumulatedLines.Add(makeCurve(Vertex.NewVertex(-1, 6, OverworldScreenPortion.FULL), Vertex.NewVertex(0, 6, OverworldScreenPortion.FULL), fadeOut ? colorAlt(cost) : colorAlt(0)));
                                        accumulatedLines.Add(makeCurve(Vertex.NewVertex(15, 5, OverworldScreenPortion.FULL), Vertex.NewVertex(16, 5, OverworldScreenPortion.FULL), fadeOut ? colorAlt(cost) : colorAlt(0)));
                                    }
                                    else
                                        accumulatedLines.Add(makeCurve(g, p, fadeOut ? colorAlt(cost) : colorAlt(0)));
                                }
                                else
                                {
                                    // normal walk is solid line with fading color based on cost
                                    accumulatedLines.Add(makeLine(g, p, fadeOut ? color(cost) : color(0)));
                                }
                            }
                            draw(p);
                        }
                    }
                }
            }
            // draw routes to everywhere
            //for v in adjacencyDict.Keys do
            //    draw(v)
#pragma warning disable CS0162   // (unreachable code, as in WPF)
            if (false)   // just turn on if want to visualize full map topography
            {
                // draw all possible routes
                accumulatedLines.Clear();
                var (preferLadder, cost) = (false, 0);
                foreach (var p in adjacencyDict.Keys)
                    foreach (var gc in adjacencyDict[p])
                    {
                        var g = gc.Item1;
                        if (allPossibleScreenScrolls.Contains(Tuple.Create(p, g)) && !(preferLadder))
                        {
                            if (p.Equals(Vertex.NewVertex(0, 6, OverworldScreenPortion.FULL)) && g.Equals(Vertex.NewVertex(15, 5, OverworldScreenPortion.FULL)))   // world wrap
                            {
                                accumulatedLines.Add(makeCurve(Vertex.NewVertex(-1, 6, OverworldScreenPortion.FULL), Vertex.NewVertex(0, 6, OverworldScreenPortion.FULL), fadeOut ? colorAlt(cost) : colorAlt(0)));
                                accumulatedLines.Add(makeCurve(Vertex.NewVertex(15, 5, OverworldScreenPortion.FULL), Vertex.NewVertex(16, 5, OverworldScreenPortion.FULL), fadeOut ? colorAlt(cost) : colorAlt(0)));
                            }
                            else
                                accumulatedLines.Add(makeCurve(g, p, fadeOut ? colorAlt(cost) : colorAlt(0)));
                        }
                        else
                        {
                            // normal walk is solid line with fading color based on cost
                            accumulatedLines.Add(makeLine(g, p, fadeOut ? color(cost) : color(0)));
                        }
                    }
            }
#pragma warning restore CS0162
            var pq = new OverworldRouting.PriorityQueue<Tuple<int, int>>();
            // draw routes only to routeworthy (accessible and interesting) spots
            for (int i_ = 0; i_ <= 15; i_++)   // (C#: i_, j_ because the loop can't shadow the parameters i, j)
                for (int j_ = 0; j_ <= 7; j_++)
                    if (owRouteworthySpots[i_, j_])
                    {
                        var goal_ = convertToCanonicalVertex(i_, j_, screenTypes, PortionDetail.STAIRS);
                        draw(goal_);
                        // track costs to each unmarked spot
                        if (owUnmarked[i_, j_])
                        {
                            var ok_ = d.TryGetValue(goal_, out var r);
                            if (ok_)
                            {
                                var cost = r.Item1;
                                pq.Enqueue(cost, Tuple.Create(i_, j_));
                            }
                        }
                    }
            var N = maxBoldGYR;
            // bold highlight cheapest N unmarked, pale highlight rest
            var toHighlight = new List<(int, int, bool)>();
            if (N > 0 || showPale)
            {
                void iterate(int N, int recentCost)
                {
                    if (!pq.IsEmpty)
                    {
                        var (nextCost, ij) = pq.Dequeue();
                        var (i, j) = ij;
                        if (maxBoldGYR > 0 && (N > 0 || nextCost == recentCost))
                        {
                            toHighlight.Add((i, j, true));
                            iterate(N - 1, nextCost);
                        }
                        else
                        {
                            toHighlight.Add((i, j, false));
                            iterate(N - 1, 999999);
                        }
                    }
                }
                if (!pq.IsEmpty)
                {
                    var (recentCost, ij) = pq.Dequeue();
                    var (i_, j_) = ij;
                    toHighlight.Add((i_, j_, N > 0));
                    iterate(N - 1, recentCost);
                }
            }
            doComputedDrawing(toHighlight, whatToCyan, accumulatedLines);
        }
    }
}
