using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents a path made up of lines and curves.
/// </summary>
public class GeneralPath
{
    /// <summary>
    /// This property holds the current set of segments in the general path.
    /// </summary>
    public List<IPathSegment> Segments { get; } = [];

    /// <summary>
    /// This property reports the minimum value for X that has been encountered during path
    /// construction.
    /// </summary>
    internal double MinX { get; private set; } = double.MaxValue;

    /// <summary>
    /// This property reports the minimum value for Y that has been encountered during path
    /// construction.
    /// </summary>
    internal double MinY { get; private set; } = double.MaxValue;

    /// <summary>
    /// This property reports the maximum value for X that has been encountered during path
    /// construction.
    /// </summary>
    internal double MaxX { get; private set; } = double.MinValue;

    /// <summary>
    /// This property reports the maximum value for Y that has been encountered during path
    /// construction.
    /// </summary>
    internal double MaxY { get; private set; } = double.MinValue;

    private TwoDPoint _subPathStart = TwoDPoint.Zero;
    private TwoDPoint _cp = TwoDPoint.Zero;

    /// <summary>
    /// This method is used to move the current point to a new location.
    /// </summary>
    /// <param name="x">The X coordinate to move to.</param>
    /// <param name="y">The Y coordinate to move to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath MoveTo(double x, double y)
    {
        return MoveTo(new TwoDPoint(x, y));
    }

    /// <summary>
    /// This method is used to move the current point to a new location relative to the
    /// current point.
    /// </summary>
    /// <param name="x">The relative X coordinate to move to.</param>
    /// <param name="y">The relative Y coordinate to move to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath RelativeMoveTo(double x, double y)
    {
        return RelativeMoveTo(new TwoDPoint(x, y));
    }

    /// <summary>
    /// This method is used to move the current point to a new location relative to the
    /// current point.
    /// </summary>
    /// <param name="point">The point to add to the current location to make the final
    /// location to move to.</param>
    /// <returns>This object, for fluency.</returns>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public GeneralPath RelativeMoveTo(TwoDPoint point)
    {
        return MoveTo(_cp + point);
    }

    /// <summary>
    /// This method is used to move the current point to the one provided.
    /// </summary>
    /// <param name="point">The point to move to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath MoveTo(TwoDPoint point)
    {
        Add(point);

        _subPathStart = _cp = point;

        return this;
    }

    /// <summary>
    /// This method is used to draw a line from the current point to a new location.
    /// </summary>
    /// <param name="x">The X coordinate to draw a line to.</param>
    /// <param name="y">The Y coordinate to draw a line to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath LineTo(double x, double y)
    {
        return LineTo(new TwoDPoint(x, y));
    }

    /// <summary>
    /// This method is used to draw a line from the current point to a new location
    /// relative to the current point.
    /// </summary>
    /// <param name="x">The relative X coordinate to draw a line to.</param>
    /// <param name="y">The relative Y coordinate to draw a line to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath RelativeLineTo(double x, double y)
    {
        return RelativeLineTo(new TwoDPoint(x, y));
    }

    /// <summary>
    /// This method is used to draw a line from the current point to a new location
    /// relative to the current point.
    /// </summary>
    /// <param name="point">The point to add to the current location to make the final
    /// location to draw a line to.</param>
    /// <returns>This object, for fluency.</returns>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public GeneralPath RelativeLineTo(TwoDPoint point)
    {
        return LineTo(_cp + point);
    }

    /// <summary>
    /// This method is used to draw a horizontal line from the current point to a new
    /// location.
    /// </summary>
    /// <param name="x">The X coordinate to draw a line to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath HorizontalLineTo(double x)
    {
        return LineTo(_cp with { X = x });
    }

    /// <summary>
    /// This method is used to draw a horizontal line from the current point to a new
    /// location.
    /// </summary>
    /// <param name="x">The X coordinate to draw a line to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath RelativeHorizontalLineTo(double x)
    {
        return LineTo(_cp with { X = _cp.X + x });
    }

    /// <summary>
    /// This method is used to draw a vertical line from the current point to a new
    /// location.
    /// </summary>
    /// <param name="y">The Y coordinate to draw a line to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath VerticalLineTo(double y)
    {
        return LineTo(_cp with { Y = y });
    }

    /// <summary>
    /// This method is used to draw a vertical line from the current point to a new
    /// location.
    /// </summary>
    /// <param name="y">The Y coordinate to draw a line to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath RelativeVerticalLineTo(double y)
    {
        return LineTo(_cp with { Y = _cp.Y + y });
    }

    /// <summary>
    /// This method is used to draw a line from the current point to a new point.
    /// </summary>
    /// <param name="point">The point to draw a line to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath LineTo(TwoDPoint point)
    {
        Segments.Add(new Line(_cp, point));
        Add(point);

        _cp = point;

        return this;
    }

    /// <summary>
    /// This method is used to draw a quadratic Bézier curve from the current point to a new
    /// point.
    /// </summary>
    /// <param name="controlPointX">The X coordinate of the control point that governs the curve.</param>
    /// <param name="controlPointY">The Y coordinate of the control point that governs the curve.</param>
    /// <param name="x">The X coordinate to draw a quad curve to.</param>
    /// <param name="y">The Y coordinate to draw a quad curve to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath QuadTo(double controlPointX, double controlPointY, double x, double y)
    {
        return QuadTo(new TwoDPoint(controlPointX, controlPointY), new TwoDPoint(x, y));
    }

    /// <summary>
    /// This method is used to draw a quadratic Bézier curve from the current point to a new
    /// point.
    /// </summary>
    /// <param name="controlPointX">The X coordinate of the control point that governs the curve.</param>
    /// <param name="controlPointY">The Y coordinate of the control point that governs the curve.</param>
    /// <param name="x">The relative X coordinate to draw a quad curve to.</param>
    /// <param name="y">The relative Y coordinate to draw a quad curve to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath RelativeQuadTo(double controlPointX, double controlPointY, double x, double y)
    {
        return RelativeQuadTo(new TwoDPoint(controlPointX, controlPointY), new TwoDPoint(x, y));
    }

    /// <summary>
    /// This method is used to draw a quadratic Bézier curve from the current point to a new
    /// point.
    /// </summary>
    /// <param name="controlPoint">The control point that governs the curve.</param>
    /// <param name="point">The point to add a quadratic curve to.</param>
    /// <returns>This object, for fluency.</returns>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public GeneralPath RelativeQuadTo(TwoDPoint controlPoint, TwoDPoint point)
    {
        return QuadTo(_cp + controlPoint, _cp + point);
    }

    /// <summary>
    /// This method is used to draw a quadratic Bézier curve from the current point to a new
    /// point, deriving the control point from the previous segment, which must be a
    /// quad segment.
    /// </summary>
    /// <param name="x">The X coordinate to draw a smooth quad curve to.</param>
    /// <param name="y">The Y coordinate to draw a smooth quad to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath SmoothQuadTo(double x, double y)
    {
        return SmoothQuadTo(new TwoDPoint(x, y));
    }

    /// <summary>
    /// This method is used to draw a quadratic Bézier curve from the current point to a new
    /// point, deriving the control point from the previous segment, which must be a
    /// quad segment.
    /// </summary>
    /// <param name="x">The relative X coordinate to draw a smooth quad curve to.</param>
    /// <param name="y">The relative Y coordinate to draw a smooth quad to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath RelativeSmoothQuadTo(double x, double y)
    {
        return RelativeSmoothQuadTo(new TwoDPoint(x, y));
    }

    /// <summary>
    /// This method is used to draw a quadratic Bézier curve from the current point to a new
    /// point, deriving the control point from the previous segment, which must be a
    /// quad segment.
    /// </summary>
    /// <param name="point">The point to add a smooth quadratic curve to.</param>
    /// <returns>This object, for fluency.</returns>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public GeneralPath RelativeSmoothQuadTo(TwoDPoint point)
    {
        return SmoothQuadTo(_cp + point);
    }

    /// <summary>
    /// This method is used to draw a quadratic Bézier curve from the current point to a new
    /// point, deriving the control point from the previous segment, which must be a
    /// quad segment.
    /// </summary>
    /// <param name="point">The point to add a quadratic curve to.</param>
    /// <returns>This object, for fluency.</returns>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public GeneralPath SmoothQuadTo(TwoDPoint point)
    {
        if (Segments.Last() is QuadCurve previousCurve)
        {
            TwoDPoint previousControlPoint = previousCurve.Points[1];
            TwoDVector delta = _cp - previousControlPoint;

            return QuadTo(_cp + delta, point);
        }

        throw new Exception("A smooth quad path must follow a previous quad path.");
    }

    /// <summary>
    /// This method is used to add a quadratic Bézier curve to the current point to the path.
    /// </summary>
    /// <param name="controlPoint">The control point that governs the curve.</param>
    /// <param name="point">The point to add a quadratic curve to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath QuadTo(TwoDPoint controlPoint, TwoDPoint point)
    {
        Segments.Add(new QuadCurve(_cp, controlPoint, point));
        Add(controlPoint);
        Add(point);

        _cp = point;

        return this;
    }

    /// <summary>
    /// This method is used to draw a cubic Bézier curve from the current point to a new
    /// point.
    /// </summary>
    /// <param name="controlPoint1X">The X coordinate of the first control point that governs
    /// the curve.</param>
    /// <param name="controlPoint1Y">The Y coordinate of the first control point that governs
    /// the curve.</param>
    /// <param name="controlPoint2X">The X coordinate of the second control point that governs
    /// the curve.</param>
    /// <param name="controlPoint2Y">The Y coordinate of the second control point that governs
    /// the curve.</param>
    /// <param name="x">The X coordinate to draw a cubic curve to.</param>
    /// <param name="y">The Y coordinate to draw a cubic curve to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath CubicTo(
        double controlPoint1X, double controlPoint1Y, double controlPoint2X, double controlPoint2Y,
        double x, double y)
    {
        return CubicTo(
            new TwoDPoint(controlPoint1X, controlPoint1Y), new TwoDPoint(controlPoint2X, controlPoint2Y),
            new TwoDPoint(x, y));
    }

    /// <summary>
    /// This method is used to draw a cubic Bézier curve from the current point to a new
    /// point.
    /// </summary>
    /// <param name="controlPoint1X">The X coordinate of the first control point that governs
    /// the curve.</param>
    /// <param name="controlPoint1Y">The Y coordinate of the first control point that governs
    /// the curve.</param>
    /// <param name="controlPoint2X">The X coordinate of the second control point that governs
    /// the curve.</param>
    /// <param name="controlPoint2Y">The Y coordinate of the second control point that governs
    /// the curve.</param>
    /// <param name="x">The relative X coordinate to draw a cubic curve to.</param>
    /// <param name="y">The relative Y coordinate to draw a cubic curve to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath RelativeCubicTo(
        double controlPoint1X, double controlPoint1Y, double controlPoint2X, double controlPoint2Y,
        double x, double y)
    {
        return RelativeCubicTo(
            new TwoDPoint(controlPoint1X, controlPoint1Y), new TwoDPoint(controlPoint2X, controlPoint2Y),
            new TwoDPoint(x, y));
    }

    /// <summary>
    /// This method is used to draw a cubic Bézier curve from the current point to a new
    /// point.
    /// </summary>
    /// <param name="controlPoint1">The first control point that governs the curve.</param>
    /// <param name="controlPoint2">The second control point that governs the curve.</param>
    /// <param name="point">The point to add a cubic curve to.</param>
    /// <returns>This object, for fluency.</returns>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public GeneralPath RelativeCubicTo(TwoDPoint controlPoint1, TwoDPoint controlPoint2, TwoDPoint point)
    {
        return CubicTo(_cp + controlPoint1, _cp + controlPoint2, _cp + point);
    }

    /// <summary>
    /// This method is used to draw a cubic Bézier curve from the current point to a new
    /// point, deriving the control point from the previous segment, which must be a
    /// cubic segment.
    /// </summary>
    /// <param name="controlPoint2X">The X coordinate of the second control point that governs
    /// the curve.</param>
    /// <param name="controlPoint2Y">The Y coordinate of the second control point that governs
    /// the curve.</param>
    /// <param name="x">The X coordinate to draw a smooth cubic curve to.</param>
    /// <param name="y">The Y coordinate to draw a smooth cubic to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath SmoothCubicTo(double controlPoint2X, double controlPoint2Y, double x, double y)
    {
        return SmoothCubicTo(new TwoDPoint(controlPoint2X, controlPoint2Y), new TwoDPoint(x, y));
    }

    /// <summary>
    /// This method is used to draw a cubic Bézier curve from the current point to a new
    /// point, deriving the control point from the previous segment, which must be a
    /// cubic segment.
    /// </summary>
    /// <param name="controlPoint2X">The X coordinate of the second control point that governs
    /// the curve.</param>
    /// <param name="controlPoint2Y">The Y coordinate of the second control point that governs
    /// the curve.</param>
    /// <param name="x">The relative X coordinate to draw a smooth cubic curve to.</param>
    /// <param name="y">The relative Y coordinate to draw a smooth cubic to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath RelativeSmoothCubicTo(double controlPoint2X, double controlPoint2Y, double x, double y)
    {
        return RelativeSmoothCubicTo(new TwoDPoint(controlPoint2X, controlPoint2Y), new TwoDPoint(x, y));
    }

    /// <summary>
    /// This method is used to draw a cubic Bézier curve from the current point to a new
    /// point, deriving the control point from the previous segment, which must be a
    /// cubic segment.
    /// </summary>
    /// <param name="controlPoint2">The second control point that governs the curve.</param>
    /// <param name="point">The point to add a smooth cubic curve to.</param>
    /// <returns>This object, for fluency.</returns>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public GeneralPath RelativeSmoothCubicTo(TwoDPoint controlPoint2, TwoDPoint point)
    {
        return SmoothCubicTo(_cp + controlPoint2, _cp + point);
    }

    /// <summary>
    /// This method is used to draw a cubic Bézier curve from the current point to a new
    /// point, deriving the control point from the previous segment, which must be a
    /// cubic segment.
    /// </summary>
    /// <param name="controlPoint2">The second control point that governs the curve.</param>
    /// <param name="point">The point to add a cubic curve to.</param>
    /// <returns>This object, for fluency.</returns>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public GeneralPath SmoothCubicTo(TwoDPoint controlPoint2, TwoDPoint point)
    {
        if (Segments.Last() is CubicCurve previousCurve)
        {
            TwoDPoint previousControlPoint = previousCurve.Points[2];
            TwoDVector delta = _cp - previousControlPoint;

            return CubicTo(_cp + delta, controlPoint2, point);
        }

        throw new Exception("A smooth cubic path must follow a previous cubic path.");
    }

    /// <summary>
    /// This method is used to add a cubic Bézier curve to the current point to the path.
    /// </summary>
    /// <param name="controlPoint1">The first control point that governs the curve.</param>
    /// <param name="controlPoint2">The second control point that governs the curve.</param>
    /// <param name="point">The point to add a quadratic curve to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath CubicTo(TwoDPoint controlPoint1, TwoDPoint controlPoint2, TwoDPoint point)
    {
        Segments.Add(new CubicCurve(_cp, controlPoint1, controlPoint2, point));
        Add(controlPoint1);
        Add(controlPoint2);
        Add(point);

        _cp = point;

        return this;
    }

    /// <summary>
    /// This method is used to close the current sub-path if it isn't already closed.
    /// </summary>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath ClosePath()
    {
        if (_cp != _subPathStart)
        {
            LineTo(_subPathStart);

            _subPathStart = _cp;
        }

        return this;
    }

    /// <summary>
    /// This method tessellates this path into an ordered polyline of points, sampling each
    /// segment at the given resolution.  Consecutive segments share their boundary point, so
    /// only the first segment contributes its starting point; every other segment
    /// contributes only its own interior and ending samples.
    /// </summary>
    /// <param name="stepsPerSegment">The number of samples to take across each segment.</param>
    /// <returns>The tessellated points, in path order.</returns>
    public List<TwoDPoint> Sample(int stepsPerSegment)
    {
        List<TwoDPoint> points = [];

        for (int segmentIndex = 0; segmentIndex < Segments.Count; segmentIndex++)
        {
            IPathSegment segment = Segments[segmentIndex];
            int startStep = segmentIndex == 0 ? 0 : 1;

            for (int step = startStep; step <= stepsPerSegment; step++)
                points.Add(segment.GetPoint((double) step / stepsPerSegment));
        }

        return points;
    }

    /// <summary>
    /// This method is used to add the given points to our 2D bounding box.
    /// </summary>
    /// <param name="points">The points to add.</param>
    private void Add(params TwoDPoint[] points)
    {
        foreach (TwoDPoint point in points)
        {
            MinX = Math.Min(MinX, point.X);
            MinY = Math.Min(MinY, point.Y);
            MaxX = Math.Max(MaxX, point.X);
            MaxY = Math.Max(MaxY, point.Y);
        }
    }

    /// <summary>
    /// This method is used to test whether the given point is inside the path, using the
    /// standard even/odd (crossing-number) rule: cast a test line from the point off to the
    /// right and count how many times the path's boundary crosses it, treating an odd count
    /// as "inside".  Unlike tessellating the path into a polyline first, this asks each
    /// segment directly (via its own <see cref="IPathSegment.CountCrossingsToTheRight"/>) how
    /// often a rightward horizontal line crosses it, so curved segments are tested exactly
    /// rather than approximated.
    /// <para>
    /// This method runs for every ray that reaches a flat surface's plane, so it is deliberately
    /// allocation-free: each segment reports a count rather than handing back intersection
    /// objects in a collection.  The convex-hull straddle rejection and the even/odd tie-breaking
    /// that go with it live in the segments themselves for the same reason -- checking them here
    /// would mean asking each segment for its defining points, and building that array is itself
    /// an allocation.  <see cref="IPathSegment.CountCrossingsToTheRight"/> documents both rules
    /// and why they matter.
    /// </para>
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c>, if the path contains the point, or <c>false</c>, if not.</returns>
    public bool Contains(TwoDPoint point)
    {
        int crossingCount = 0;

        foreach (IPathSegment segment in Segments)
            crossingCount += segment.CountCrossingsToTheRight(point);

        return crossingCount % 2 == 1;
    }

    /// <summary>
    /// This method is used to verify that the given path is contained by this one.  It's a
    /// bit brute-force, but we do this by asking where the midpoint of each of the path's
    /// segments falls and taking the majority verdict.
    /// <para>
    /// Sampling the middle of each segment rather than its endpoints matters: where two runs
    /// of an outline touch, they touch at a vertex, and a point sitting on this path's own
    /// boundary is neither in nor out -- the even/odd rule has to call it one way or the
    /// other.  A FontAwesome outline icon has exactly that, one vertex of the hole meeting the
    /// outer run, so a single endpoint reporting itself outside was enough to hide a hole from
    /// its parent.  Taking the majority rather than insisting on unanimity covers the same
    /// ground from the other side.
    /// </para>
    /// <remarks>
    /// Note: this works for a very specific kind of general path and is easy to fool if the
    /// path is not well-behaved; there must not be any crossovers between the two paths.
    /// </remarks>
    /// </summary>
    /// <param name="path">The path to test.</param>
    /// <returns><code>true</code>, if the path is contained by this one, or <code>false</code>,
    /// if not.</returns>
    public bool Contains(GeneralPath path)
    {
        int inside = path.Segments.Count(segment => Contains(segment.GetPoint(0.5)));

        return inside * 2 > path.Segments.Count;
    }

    /// <summary>
    /// This method folds another path's segments into this one, run for run, and grows this
    /// path's 2D bounding box to take in the other's.  It is how several separate outlines --
    /// the glyphs of a run of text, say -- become one path.  Growing the bounding box is the
    /// point of having this rather than adding the segments directly: the flat end caps of an
    /// extrusion are sized from that box, so a path whose box was left untouched would extrude
    /// with no caps at all.
    /// </summary>
    /// <param name="other">The path whose segments to fold in.</param>
    /// <returns>This object, for fluency.</returns>
    internal GeneralPath Append(GeneralPath other)
    {
        if (other.Segments.Count == 0)
            return this;

        Segments.AddRange(other.Segments);

        MinX = Math.Min(MinX, other.MinX);
        MinY = Math.Min(MinY, other.MinY);
        MaxX = Math.Max(MaxX, other.MaxX);
        MaxY = Math.Max(MaxY, other.MaxY);

        return this;
    }

    /// <summary>
    /// This method applies a 2D transform to every point of the path, in place, and rebuilds
    /// its bounding box to match.  The path's points live in the X/Y plane, so the transform is
    /// applied to each as <c>(x, y, 0)</c> and the resulting X and Y kept.  This is the 2D
    /// counterpart of transforming a surface in 3D: build the outline, then move, turn or resize
    /// it as a whole before it is given depth.  Each run is rebuilt through the same drawing
    /// methods that built it, so the bounding box the flat end caps rely on stays correct.
    /// </summary>
    /// <param name="matrix">The transform to apply.</param>
    /// <returns>This object, for fluency.</returns>
    internal GeneralPath Transform(Matrix matrix)
    {
        List<IPathSegment> original = [.. Segments];

        Segments.Clear();

        MinX = double.MaxValue;
        MinY = double.MaxValue;
        MaxX = double.MinValue;
        MaxY = double.MinValue;

        TwoDPoint current = null;

        foreach (IPathSegment segment in original)
        {
            TwoDPoint[] points = segment.Points;
            TwoDPoint start = Apply(matrix, points[0]);

            // Consecutive segments share their boundary point, so a fresh "move to" is needed
            // only where one run ends and the next begins.
            if (current is null || current != start)
                MoveTo(start);

            switch (points.Length)
            {
                case 2:
                    LineTo(Apply(matrix, points[1]));
                    break;
                case 3:
                    QuadTo(Apply(matrix, points[1]), Apply(matrix, points[2]));
                    break;
                case 4:
                    CubicTo(Apply(matrix, points[1]), Apply(matrix, points[2]), Apply(matrix, points[3]));
                    break;
            }

            current = Apply(matrix, points[^1]);
        }

        return this;
    }

    /// <summary>
    /// This method applies a transform to a single 2D point, treating it as lying in the X/Y
    /// plane at Z = 0.
    /// </summary>
    /// <param name="matrix">The transform to apply.</param>
    /// <param name="point">The 2D point to transform.</param>
    /// <returns>The transformed 2D point.</returns>
    private static TwoDPoint Apply(Matrix matrix, TwoDPoint point)
    {
        Point transformed = matrix * new Point(point.X, point.Y, 0);

        return new TwoDPoint(transformed.X, transformed.Y);
    }

    /// <summary>
    /// This method is used to reverse the order of the segments in this path.
    /// Each segment is also reversed.
    /// </summary>
    /// <returns>This object, for fluency.</returns>
    private GeneralPath Reverse()
    {
        Segments.Reverse();

        foreach (IPathSegment segment in Segments)
            segment.Reverse();

        return this;
    }

    /// <summary>
    /// This method is used to normalize the path for projecting into 3D.  This means
    /// determining the direction of travel of all subpaths, making sure the outermost
    /// paths run counter-clockwise and contained subpaths run opposite to their parents'
    /// direction.  Since this only reorders segments and points, it does not affect the
    /// path's overall bounding box.  As such, we don't touch that.
    /// </summary>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath NormalizeFor3D()
    {
        List<SubPath> subPaths = FindAllSubPaths();

        ArrangeSubPaths(subPaths);
        Normalize(subPaths, true);

        Segments.Clear();
        
        FlattenSubPaths(subPaths);

        return this;
    }

    /// <summary>
    /// This method is used to return the now-normalized subpaths back to our main segments
    /// list.
    /// </summary>
    /// <param name="subPaths">The subpath to flatten into.</param>
    private void FlattenSubPaths(List<SubPath> subPaths)
    {
        foreach (SubPath subPath in subPaths)
        {
            Segments.AddRange(subPath.Path.Segments);

            FlattenSubPaths(subPath.ContainedPaths);
        }
    }

    /// <summary>
    /// This method is used to create a list of subpaths of this path, one per run.  A run
    /// ends where it returns to the point it started from, closing itself off.  A run that
    /// was never closed ends where the next one is moved to; since consecutive segments of
    /// a run share their boundary point, that shows up as a break in the chain.
    /// </summary>
    /// <returns>the list of subpaths of this one.</returns>
    public List<SubPath> FindAllSubPaths()
    {
        List<SubPath> result = [];
        int start = -1;

        for (int i = 0; i < Segments.Count; i++)
        {
            if (start >= 0 && Segments[i].Points[0] != Segments[i - 1].Points[^1])
            {
                result.Add(SubPathFor(start, i));

                start = -1;
            }

            if (start < 0)
                start = i;

            if (Segments[i].Points[^1] == Segments[start].Points[0])
            {
                result.Add(SubPathFor(start, i + 1));

                start = -1;
            }
        }

        if (start >= 0)
            result.Add(SubPathFor(start, Segments.Count));

        return result;
    }

    /// <summary>
    /// This method is used to wrap the given range of our segments as a subpath.  The
    /// segments themselves are shared with this path rather than copied, so reversing the
    /// subpath reverses them here too; the list that holds them is the subpath's own,
    /// though, so the run can be reordered without disturbing this path.
    /// </summary>
    /// <param name="start">The index of the run's first segment.</param>
    /// <param name="end">The index just past the run's last segment.</param>
    /// <returns>The range of segments, as a subpath.</returns>
    private SubPath SubPathFor(int start, int end)
    {
        GeneralPath path = new GeneralPath();

        path.Segments.AddRange(Segments[start..end]);

        return new SubPath { Path = path };
    }

    /// <summary>
    /// This method returns whether the path proceeds counter-clockwise (<code>true</code>)
    /// or clockwise (<code>false</code>).  This is intended to be used on a single-run,
    /// closed path.
    /// </summary>
    /// <returns><code>true</code>, if the path progresses in a counter-clockwise direction,
    /// or <code>false</code>, if it goes in a clockwise direction.</returns>
    /// <exception cref="InvalidOperationException">If the path holds no segments at all, since
    /// there is then no direction to report.  Code that has to cope with such a path (a space
    /// in a run of text gives one) should ask <see cref="SignedArea"/> instead, which reports
    /// no area rather than failing.</exception>
    public bool IsCounterClockwise()
    {
        if (Segments.Count == 0)
            throw new InvalidOperationException("Not enough segments to determine direction.");

        return SignedArea() > 0;
    }

    /// <summary>
    /// This method returns the area the path encloses, signed: positive when the path runs
    /// counter-clockwise and negative when it runs clockwise.  A run left open is treated as
    /// closed by a straight line from where it ends back to where it began, which is what
    /// the even/odd fill rule assumes of it anyway.
    /// <para>
    /// The area is exact, curves included, rather than taken from the polygon the segments'
    /// endpoints make: by Green's theorem the enclosed area is half of the contour integral
    /// of <c>x·dy - y·dx</c>, and for a Bézier segment that integral has a closed form in the
    /// segment's own defining points (see <see cref="SignedAreaTermFor"/>).  Ignoring the
    /// control points would not merely blur the number, it would get the *sign* wrong for
    /// runs whose endpoints alone enclose nothing -- a circle drawn as two half-circle
    /// curves, say, whose three endpoints are collinear.
    /// </para>
    /// </summary>
    /// <returns>The signed area the path encloses.</returns>
    public double SignedArea()
    {
        if (Segments.Count == 0)
            return 0;

        double sum = Segments.Sum(SignedAreaTermFor);

        return (sum + Cross(Segments[^1].Points[^1], Segments[0].Points[0])) / 2;
    }

    /// <summary>
    /// This method returns one segment's contribution to the contour integral that
    /// <see cref="SignedArea"/> halves; that is, the integral of <c>x·y' - y·x'</c> over the
    /// segment.  Writing the segment as a Bézier in its defining points and integrating term
    /// by term gives a weighted sum of the cross products of those points, one weight per
    /// pair, with the weights depending only on the segment's degree.
    /// </summary>
    /// <param name="segment">The segment to measure.</param>
    /// <returns>The segment's contribution to the contour integral.</returns>
    private static double SignedAreaTermFor(IPathSegment segment)
    {
        TwoDPoint[] points = segment.Points;

        return points.Length switch
        {
            2 => Cross(points[0], points[1]),
            3 => (2 * Cross(points[0], points[1]) +
                      Cross(points[0], points[2]) +
                  2 * Cross(points[1], points[2])) / 3,
            4 => (6 * Cross(points[0], points[1]) +
                  3 * Cross(points[0], points[2]) +
                      Cross(points[0], points[3]) +
                  3 * Cross(points[1], points[2]) +
                  3 * Cross(points[1], points[3]) +
                  6 * Cross(points[2], points[3])) / 10,
            _ => throw new NotSupportedException(
                $"A path segment with {points.Length} control points has no known area.")
        };
    }

    /// <summary>
    /// This method returns the 2D cross product of two points, taken as vectors from the
    /// origin.
    /// </summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns>The cross product of the two points.</returns>
    private static double Cross(TwoDPoint left, TwoDPoint right)
    {
        return left.X * right.Y - right.X * left.Y;
    }

    /// <summary>
    /// This method is used to make the path directions for the given subpaths and all their
    /// children match the proper direction in preparation for 3D rendering.
    /// </summary>
    /// <param name="subPaths">The subpaths to normalize</param>
    /// <param name="shouldBeCounterClockwise">Whether the given subpaths should run
    /// counter-clockwise.</param>
    public static void Normalize(List<SubPath> subPaths, bool shouldBeCounterClockwise)
    {
        foreach (SubPath subPath in subPaths)
        {
            double area = subPath.Path.SignedArea();

            // A run that encloses no area at all -- the empty path a space in a run of text
            // gives, say -- has no direction to correct.
            if (area != 0 && area > 0 != shouldBeCounterClockwise)
                subPath.Path.Reverse();

            Normalize(subPath.ContainedPaths, !shouldBeCounterClockwise);
        }
    }

    /// <summary>
    /// This method is used to take a list of subpaths and arrange them into a tree of
    /// containment.  The given list is mutated in place so that, upon return, it will
    /// contain only top-level subpaths.
    /// </summary>
    /// <param name="subPaths">The list of subpaths to arrange.</param>
    public static void ArrangeSubPaths(List<SubPath> subPaths)
    {
        if (subPaths.Count < 2)
            return;

        for (int parent = 0; parent < subPaths.Count; parent++)
        {
            for (int child = 0; child < subPaths.Count; child++)
            {
                if (parent != child && subPaths[parent].Contains(subPaths[child]))
                {
                    subPaths[parent].ContainedPaths.Add(subPaths[child]);
                    subPaths.RemoveAt(child);

                    if (child < parent)
                        parent--;

                    child--;
                }
            }
        }

        foreach (SubPath subPath in subPaths)
            ArrangeSubPaths(subPath.ContainedPaths);
    }

    /// <summary>
    /// This method is used to add a preconfigured segment to the path.
    /// </summary>
    /// <param name="segment">The segment to add.</param>
    private void AddSegment(IPathSegment segment)
    {
        if (segment.Points[0] != _cp)
            MoveTo(segment.Points[0]);

        switch (segment)
        {
            case Line line:
                LineTo(line.Points[1]);
                break;

            case QuadCurve quadCurve:
                QuadTo(quadCurve.Points[1], quadCurve.Points[2]);
                break;

            case CubicCurve cubicCurve:
                CubicTo(cubicCurve.Points[1], cubicCurve.Points[2], cubicCurve.Points[3]);
                break;
        }
    }
}
