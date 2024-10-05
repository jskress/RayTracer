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
        if (other != null)
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
    /// This method adjusts the extents of the bounding box by <c>Epsilon</c> to help
    /// make sure we don't miss any intersections.
    /// </summary>
    public void Expand()
    {
        _xMin -= Padding;
        _yMin -= Padding;
        _zMin -= Padding;
        _xMax += Padding;
        _yMax += Padding;
        _zMax += Padding;
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
