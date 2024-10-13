using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;
using SkiaSharp;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents a path made up of lines and curves.
/// </summary>
public class GeneralPath
{
    /// <summary>
    /// This property holds the current set of segments in the general path.
    /// </summary>
    public List<PathSegment> Segments { get; } = [];

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

    private readonly SKPath _skPath = new ();

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
 
        _skPath.MoveTo(point.ToSkPoint());

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
        return LineTo(_cp with { X = _cp.Y + y });
    }

    /// <summary>
    /// This method is used to draw a line from the current point to a new point.
    /// </summary>
    /// <param name="point">The point to draw a line to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath LineTo(TwoDPoint point)
    {
        Segments.Add(new LinearPathSegment(_cp, point));
        Add(point);

        _cp = point;

        _skPath.LineTo(point.ToSkPoint());

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
        if (Segments.Last() is not QuadPathSegment)
            throw new Exception("A smooth quad path must follow a previous quad path.");

        TwoDPoint previousControlPoint = Segments.Last().Points[1];
        TwoDPoint delta = _cp - previousControlPoint;

        return QuadTo(_cp + delta, point);
    }

    /// <summary>
    /// This method is used to add a quadratic Bézier curve to the current point to the path.
    /// </summary>
    /// <param name="controlPoint">The control point that governs the curve.</param>
    /// <param name="point">The point to add a quadratic curve to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath QuadTo(TwoDPoint controlPoint, TwoDPoint point)
    {
        Segments.Add(new QuadPathSegment(_cp, controlPoint, point));
        Add(controlPoint);
        Add(point);

        _cp = point;

        _skPath.QuadTo(controlPoint.ToSkPoint(), point.ToSkPoint());

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
        if (Segments.Last() is not CubicPathSegment)
            throw new Exception("A smooth cubic path must follow a previous cubic path.");

        TwoDPoint previousControlPoint = Segments.Last().Points[1];
        TwoDPoint delta = _cp - previousControlPoint;

        return CubicTo(_cp + delta, controlPoint2, point);
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
        Segments.Add(new CubicPathSegment(_cp, controlPoint1, controlPoint2, point));
        Add(controlPoint1);
        Add(controlPoint2);
        Add(point);

        _cp = point;

        _skPath.CubicTo(controlPoint1.ToSkPoint(), controlPoint2.ToSkPoint(), point.ToSkPoint());

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

        _skPath.Close();

        return this;
    }

    /// <summary>
    /// This method is used to test whether the given point is inside the path.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c>, if the path contains the point, or <c>false</c>, if not.</returns>
    public bool Contains(TwoDPoint point)
    {
        return _skPath.Contains((float) point.X, (float) point.Y);
    }

    /// <summary>
    /// This method is used to add the given point to our 2D bounding box.
    /// </summary>
    /// <param name="point">The point to add.</param>
    private void Add(TwoDPoint point)
    {
        MinX = Math.Min(MinX, point.X);
        MinY = Math.Min(MinY, point.Y);
        MaxX = Math.Max(MaxX, point.X);
        MaxY = Math.Max(MaxY, point.Y);
    }

    /// <summary>
    /// This method is used to reverse the order of the segments in this path.
    /// </summary>
    /// <returns>This object, for fluency.</returns>
    internal GeneralPath Reverse()
    {
        Segments.Reverse();

        foreach (PathSegment segment in Segments)
            segment.Reverse();

        return this;
    }
}
