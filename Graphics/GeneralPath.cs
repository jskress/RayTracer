using System.Diagnostics.CodeAnalysis;

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
    /// This method is used to test whether the given point is inside the path, using the
    /// standard even/odd (crossing-number) rule: cast a test line from the point off to the
    /// right and count how many times the path's boundary crosses it, treating an odd count
    /// as "inside".  Unlike tessellating the path into a polyline first, this asks each
    /// segment directly (via its own <see cref="IPathSegment.GetIntersections"/>, the same
    /// line-intersection math <see cref="ExtrusionPathSurface"/> already relies on for
    /// lateral-surface hit testing) where a rightward horizontal line crosses it, so curved
    /// segments are tested exactly rather than approximated.
    /// <para>
    /// A segment only gets asked for its crossings at all if its defining points -- for a
    /// line, its two endpoints; for a curve, its endpoints *and* control points -- aren't
    /// all on the same side of the test point's Y (comparing with strict "greater than" on
    /// one side and "at or below" on the other, the usual even/odd tie-breaker).  A Bezier
    /// curve never leaves the convex hull of its own defining points, so if every one of
    /// them is on the same side, the curve provably can't cross the test line at all and is
    /// safe to skip.  Using only the two endpoints for this check (rather than every defining
    /// point) would be wrong for a curve: a segment whose endpoints happen to sit on the same
    /// side can still bulge across the test line and back through its interior, contributing
    /// crossings that must be counted, not skipped.  Skipping is still necessary in the cases
    /// it does apply to, since two segments that merely touch the test line at a shared
    /// vertex (without the path actually crossing from one side to the other there, e.g. a
    /// flat-topped notch) would otherwise each report that shared point as its own crossing,
    /// double-counting a single non-crossing touch as two.
    /// </para>
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c>, if the path contains the point, or <c>false</c>, if not.</returns>
    public bool Contains(TwoDPoint point)
    {
        TwoDRay testRay = new () { Origin = point, Direction = new TwoDVector(1, 0) };
        int crossingCount = 0;

        foreach (IPathSegment segment in Segments)
        {
            bool anyAbove = false;
            bool anyAtOrBelow = false;

            foreach (TwoDPoint definingPoint in segment.Points)
            {
                if (definingPoint.Y > point.Y)
                    anyAbove = true;
                else
                    anyAtOrBelow = true;
            }

            if (!anyAbove || !anyAtOrBelow)
                continue;

            foreach (TwoDIntersection intersection in segment.GetIntersections(testRay))
            {
                if (intersection.Point.X > point.X)
                    crossingCount++;
            }
        }

        return crossingCount % 2 == 1;
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
    /// This method is used to reverse the order of the segments in this path.
    /// Each segment is also reversed.
    /// </summary>
    /// <returns>This object, for fluency.</returns>
    internal GeneralPath Reverse()
    {
        Segments.Reverse();

        foreach (IPathSegment segment in Segments)
            segment.Reverse();

        return this;
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
