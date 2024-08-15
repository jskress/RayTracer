using RayTracer.Basics;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents a path made up of lines and curves.
/// </summary>
public class GeneralPath
{
    private const double TwoThirds = 2.0 / 3.0;

    /// <summary>
    /// This property holds the current set of segments in the general path.
    /// </summary>
    public List<PathSegment> Segments { get; } = [];

    private TwoDPoint _subPathStart = TwoDPoint.Zero;
    private TwoDPoint _cp = TwoDPoint.Zero;

    /// <summary>
    /// This method is used to move the current point to the one provided.
    /// </summary>
    /// <param name="point">The point to move to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath MoveTo(TwoDPoint point)
    {
        ClosePath();

        _subPathStart = _cp = point;

        return this;
    }

    /// <summary>
    /// This method is used to add a line to the current point to the path.
    /// </summary>
    /// <param name="point">The point to add a line to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath LineTo(TwoDPoint point)
    {
        TwoDPoint c = new TwoDPoint(-_cp.X + point.X, -_cp.Y + point.Y);

        Segments.Add(new LinearPathSegment(c, point));

        _cp = point;

        return this;
    }

    /// <summary>
    /// This method is used to add a quadratic Bezier curve to the current point to the path.
    /// </summary>
    /// <param name="controlPoint">The control point that governs the curve.</param>
    /// <param name="point">The point to add a quadratic curve to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath QuadTo(TwoDPoint controlPoint, TwoDPoint point)
    {
        TwoDPoint controlPoint1 = _cp + TwoThirds * (controlPoint - _cp);
        TwoDPoint controlPoint2 = point + TwoThirds * (controlPoint - point);

        return CubicTo(controlPoint1, controlPoint2, point);
    }

    /// <summary>
    /// This method is used to add a cubic Bezier curve to the current point to the path.
    /// </summary>
    /// <param name="controlPoint1">The first control point that governs the curve.</param>
    /// <param name="controlPoint2">The second control point that governs the curve.</param>
    /// <param name="point">The point to add a quadratic curve to.</param>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath CubicTo(TwoDPoint controlPoint1, TwoDPoint controlPoint2, TwoDPoint point)
    {
        TwoDPoint a = new TwoDPoint(
            _cp.X - 3.0 * controlPoint1.X + 3.0 * controlPoint2.X - point.X,
            _cp.Y - 3.0 * controlPoint1.Y + 3.0 * controlPoint1.Y - point.Y
        );
        TwoDPoint b = new TwoDPoint(
            3.0 * controlPoint1.X - 6.0 * controlPoint2.X + 3.0 * point.X,
            3.0 * controlPoint1.Y - 6.0 * controlPoint2.Y + 3.0 * point.Y
        );
        TwoDPoint c = new TwoDPoint(
            3.0 * controlPoint2.X - 3.0 * point.X,
            3.0 * controlPoint2.Y - 3.0 * point.Y
        );

        Segments.Add(new BezierPathSegment(a, b, c, point));

        _cp = point;

        return this;
    }

    /// <summary>
    /// This method is used to close the current subpath if it isn't already closed.
    /// </summary>
    /// <returns>This object, for fluency.</returns>
    public GeneralPath ClosePath()
    {
        if (_cp != _subPathStart)
            LineTo(_subPathStart);

        return this;
    }

    /// <summary>
    /// This method is used to test whether the given point is inside the path.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c>, if the path contains the point, or <c>false</c>, if not.</returns>
    public bool Contains(TwoDPoint point)
    {
        int count = 0;

        foreach (PathSegment segment in Segments)
        {
            double[] coefficients = [
                segment.A.Y, segment.B.Y, segment.C.Y, segment.D.Y - point.Y
            ];
            double[] distances = Polynomials.Solve(coefficients);

            count += (from distance in distances ?? []
                where distance is >= 0 and <= 1
                select distance * (distance * (distance * segment.A.X + segment.B.X) + segment.C.X) + segment.D.X - point.X)
                .Count(k => k >= 0);
        }

        return (count & 1) == 1;
    }
}
