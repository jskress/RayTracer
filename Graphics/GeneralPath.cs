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

        return this;
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

        return this;
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
    /// This method is used to test whether the given point is inside the path.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c>, if the path contains the point, or <c>false</c>, if not.</returns>
    public bool Contains(TwoDPoint point)
    {
        int count = Segments
            .SelectMany(segment => segment.XIntersectionsWith(point.Y))
            .Count(x => point.X <= x);

        return count % 2 == 1;
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
}
