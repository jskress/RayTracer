using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a bounding box and provides the means to decide whether a ray
/// intersects it.
/// </summary>
public class BoundingBox
{
    private double _xMin;
    private double _yMin;
    private double _zMin;
    private double _xMax;
    private double _yMax;
    private double _zMax;

    [SuppressMessage("ReSharper", "SuggestBaseTypeForParameterInConstructor")]
    public BoundingBox(Point point1, Point point2)
    {
        _xMin = Math.Min(point1.X, point2.X);
        _yMin = Math.Min(point1.Y, point2.Y);
        _zMin = Math.Min(point1.Z, point2.Z);
        _xMax = Math.Max(point1.X, point2.X);
        _yMax = Math.Max(point1.Y, point2.Y);
        _zMax = Math.Max(point1.Z, point2.Z);
    }

    /// <summary>
    /// This method is used to add the point to the bounding box, expanding it as necessary.
    /// </summary>
    /// <param name="point">The point to add.</param>
    public void Add(Point point)
    {
        _xMin = Math.Min(_xMin, point.X);
        _yMin = Math.Min(_yMin, point.Y);
        _zMin = Math.Min(_zMin, point.Z);
        _xMax = Math.Max(_xMax, point.X);
        _yMax = Math.Max(_yMax, point.Y);
        _zMax = Math.Max(_zMax, point.Z);
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
        }
    }

    /// <summary>
    /// This method adjusts the extents of the bounding box by <c>Epsilon</c> to help
    /// make sure we don't miss any intersections.
    /// </summary>
    public void Adjust()
    {
        _xMin -= DoubleExtensions.Epsilon;
        _yMin -= DoubleExtensions.Epsilon;
        _zMin -= DoubleExtensions.Epsilon;
        _xMax += DoubleExtensions.Epsilon;
        _yMax += DoubleExtensions.Epsilon;
        _zMax += DoubleExtensions.Epsilon;
    }

    /// <summary>
    /// This method handles finding intersection points for a ray with the box.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns>The min and max intersection points for the axis being tested..</returns>
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
    /// <returns>The min and max intersection points for the axis being tested..</returns>
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
