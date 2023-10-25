using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a bounding box and provides the means to decide whether a ray
/// intersects it.
/// </summary>
public class BoundingBox
{
    private readonly Point _point1;
    private readonly Point _point2;

    public BoundingBox(Point point1, Point point2)
    {
        _point1 = point1;
        _point2 = point2;
    }

    /// <summary>
    /// This method handles finding intersection points for a ray with the box.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns>The min and max intersection points for the axis being tested..</returns>
    internal (double min, double max) GetIntersections(Ray ray)
    {
        // First, find the min/max for each axis.
        double xMin = Math.Min(_point1.X, _point2.X);
        double xMax = Math.Max(_point1.X, _point2.X);
        double yMin = Math.Min(_point1.Y, _point2.Y);
        double yMax = Math.Max(_point1.Y, _point2.Y);
        double zMin = Math.Min(_point1.Z, _point2.Z);
        double zMax = Math.Max(_point1.Z, _point2.Z);

        // Next, find the points of intersection on each axis.
        (xMin, xMax) = CheckAxis(ray.Origin.X, ray.Direction.X, xMin, xMax);
        (yMin, yMax) = CheckAxis(ray.Origin.Y, ray.Direction.Y, yMin, yMax);
        (zMin, zMax) = CheckAxis(ray.Origin.Z, ray.Direction.Z, zMin, zMax);

        // Last, find the intersections.
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
