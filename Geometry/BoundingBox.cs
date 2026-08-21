using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a bounding box and provides the means to decide whether a ray
/// intersects it.
/// </summary>
public class BoundingBox
{
    private const double Padding = 0.0001;

    /// <summary>
    /// This property notes whether the bounding box is empty or not.
    /// </summary>
    internal bool IsEmpty { get; private set; } = true;

    private double _xMin = double.MaxValue;
    private double _yMin = double.MaxValue;
    private double _zMin = double.MaxValue;
    private double _xMax = double.MinValue;
    private double _yMax = double.MinValue;
    private double _zMax = double.MinValue;

    /// <summary>
    /// This property reports the corner of the box with the smallest coordinates.
    /// </summary>
    public Point Minimum => new (_xMin, _yMin, _zMin);

    /// <summary>
    /// This property reports the corner with the largest, so that something needing to spread points
    /// through the box -- a light sampling the stuff inside a surface, say -- can say where it is.
    /// </summary>
    public Point Maximum => new (_xMax, _yMax, _zMax);

    /// <summary>
    /// This method is used to add the point to the bounding box, expanding it as necessary.
    /// </summary>
    /// <param name="point">The point to add.</param>
    public BoundingBox Add(Point point)
    {
        _xMin = Math.Min(_xMin, point.X);
        _yMin = Math.Min(_yMin, point.Y);
        _zMin = Math.Min(_zMin, point.Z);
        _xMax = Math.Max(_xMax, point.X);
        _yMax = Math.Max(_yMax, point.Y);
        _zMax = Math.Max(_zMax, point.Z);

        IsEmpty = false;

        return this;
    }

    /// <summary>
    /// This method is used to add the other bounding box to this one, expanding it as
    /// necessary.  This one is <c>null</c>-safe.
    /// </summary>
    /// <param name="other">The bounding box to add.</param>
    public void Add(BoundingBox other)
    {
        // An empty box holds nothing, so adding one must change nothing -- including whether this box
        // is still empty.  The element-wise arithmetic below already leaves the extents alone, since
        // an empty box carries its minima at the largest possible number and its maxima at the
        // smallest, but it would otherwise leave this box marked as no longer empty.
        if (other is { IsEmpty: false })
        {
            _xMin = Math.Min(_xMin, other._xMin);
            _yMin = Math.Min(_yMin, other._yMin);
            _zMin = Math.Min(_zMin, other._zMin);
            _xMax = Math.Max(_xMax, other._xMax);
            _yMax = Math.Max(_yMax, other._yMax);
            _zMax = Math.Max(_zMax, other._zMax);

            IsEmpty = false;
        }
    }

    /// <summary>
    /// This method returns a new bounding box that encloses this one after being
    /// transformed by the given matrix.  A transform (e.g. rotation) can turn an
    /// axis-aligned box into one that is no longer axis-aligned, so the result is the
    /// axis-aligned box that encloses all 8 of this box's corners once transformed.
    /// </summary>
    /// <param name="matrix">The matrix to transform this bounding box by.</param>
    /// <returns>The transformed, axis-aligned bounding box.</returns>
    internal BoundingBox TransformedBy(Matrix matrix)
    {
        BoundingBox box = new ();

        // Nothing anywhere, moved anywhere, is still nothing anywhere.  Left to run, the loop below
        // would put the largest and smallest numbers a double can hold through a matrix and hand back
        // whatever came of that, which is a box that holds everything -- the exact opposite.
        if (IsEmpty)
            return box;

        foreach (double x in new[] { _xMin, _xMax })
        foreach (double y in new[] { _yMin, _yMax })
        foreach (double z in new[] { _zMin, _zMax })
            box.Add(matrix * new Point(x, y, z));

        return box;
    }

    /// <summary>
    /// This method returns the box where this one and another overlap, or <c>null</c> when they do not
    /// meet at all.
    /// <para>
    /// It is the counterpart of <see cref="Add(BoundingBox)"/>: that one grows to hold both, this one
    /// keeps only what they have in common.  Where a union of two solids needs the first, an
    /// intersection of two needs this -- what lies in both can lie no further out than either.
    /// </para>
    /// </summary>
    /// <param name="other">The box to overlap with this one.</param>
    /// <returns>The overlap, or <c>null</c> if there is none.</returns>
    internal BoundingBox Overlap(BoundingBox other)
    {
        double xMin = Math.Max(_xMin, other._xMin);
        double xMax = Math.Min(_xMax, other._xMax);
        double yMin = Math.Max(_yMin, other._yMin);
        double yMax = Math.Min(_yMax, other._yMax);
        double zMin = Math.Max(_zMin, other._zMin);
        double zMax = Math.Min(_zMax, other._zMax);

        if (xMin > xMax || yMin > yMax || zMin > zMax)
            return null;

        return new BoundingBox()
            .Add(new Point(xMin, yMin, zMin))
            .Add(new Point(xMax, yMax, zMax));
    }

    /// <summary>
    /// This method adjusts the extents of the bounding box by some amount.  The defailt is
    /// a small fraction to help make sure we don't miss any intersections.
    /// </summary>
    /// <param name="amount">The amount to pad by.</param>
    public void Expand(double amount = Padding)
    {
        // There is no surface here to be caught just outside of, and padding the inverted extents an
        // empty box carries would only make them more inverted.
        if (IsEmpty)
            return;

        _xMin -= amount;
        _yMin -= amount;
        _zMin -= amount;
        _xMax += amount;
        _yMax += amount;
        _zMax += amount;
    }

    /// <summary>
    /// This method is used to test whether the given ray intersects with this
    /// bounding box.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns><c>true</c>, if the ray intersects the bounding box, or <c>false</c>,
    /// if not.</returns>
    internal bool IsHitBy(Ray ray)
    {
        (double tMin, double tMax) = GetIntersections(ray);

        return tMin <= tMax;
    }

    /// <summary>
    /// This method handles finding intersection points for a ray with the box.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns>The min and max intersection points for the axis being tested.</returns>
    internal (double min, double max) GetIntersections(Ray ray)
    {
        // First, find the points of intersection on each axis.
        (double xMin, double xMax) = CheckAxis(ray.Origin.X, ray.Direction.X, _xMin, _xMax);
        (double yMin, double yMax) = CheckAxis(ray.Origin.Y, ray.Direction.Y, _yMin, _yMax);
        (double zMin, double zMax) = CheckAxis(ray.Origin.Z, ray.Direction.Z, _zMin, _zMax);

        // Then, find the intersections.
        double tMin = Math.Max(xMin, Math.Max(yMin, zMin));
        double tMax = Math.Min(xMax, Math.Min(yMax, zMax));

        return (tMin, tMax);
    }

    /// <summary>
    /// This method handles finding intersection points for a specific pair of axis planes.
    /// </summary>
    /// <param name="origin">The origin value for the axis.</param>
    /// <param name="direction">The direction value for the axis.</param>
    /// <param name="axisMin">The minimum allowed value for the axis.</param>
    /// <param name="axisMax">The maximum allowed value for the axis.</param>
    /// <returns>The min and max intersection points for the axis being tested.</returns>
    private static (double min, double max) CheckAxis(
        double origin, double direction, double axisMin, double axisMax)
    {
        double minNumerator = axisMin - origin;
        double maxNumerator = axisMax - origin;
        double min;
        double max;

        if (Math.Abs(direction) >= DoubleExtensions.Epsilon)
        {
            min = minNumerator / direction;
            max = maxNumerator / direction;
        }
        else
        {
            min = minNumerator * double.PositiveInfinity;
            max = maxNumerator * double.PositiveInfinity;
        }

        if (min > max)
            (min, max) = (max, min);

        return (min, max);
    }
}
