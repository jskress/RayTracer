using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a parallelogram.  It is defined by one point, and two side vectors
/// emanating from that point.
/// </summary>
public class Parallelogram : FlatSurface
{
    /// <summary>
    /// This property provides the "anchor" point of the parallelogram.
    /// </summary>
    public Point Point
    {
        get => field;
        set
        {
            field = value;

            DefinitionChanged(value, Side1, Side2);
        }
    }

    /// <summary>
    /// This property provides the first side vector of the parallelogram.
    /// </summary>
    public Vector Side1
    {
        get => field;
        set
        {
            field = value;

            DefinitionChanged(Point, value, Side2);
        }
    }

    /// <summary>
    /// This property provides the second side vector of the parallelogram.
    /// </summary>
    public Vector Side2
    {
        get => field;
        set
        {
            field = value;

            DefinitionChanged(Point, Side1, value);
        }
    }

    private Vector _constantW;

    /// <summary>
    /// This method is used to reset our control information when our point, or either of
    /// our side vectors change.  If any of the information is <c>null</c> (as will be
    /// during initial creation), we silently no-op.
    /// </summary>
    /// <param name="point">The anchor point of the parallelogram.</param>
    /// <param name="side1">The first side of the parallelogram.</param>
    /// <param name="side2">The second side of the parallelogram.</param>
    private void DefinitionChanged(Point point, Vector side1, Vector side2)
    {
        if (point is not null && side1 is not null && side2 is not null)
        {
            Vector cross = side2.Cross(side1);

            Normal = cross.Unit;
            PlaneConstant = Normal.Dot(point);
            _constantW = cross / cross.Dot(cross);
        }
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the parallelogram
    /// and, if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns>The value of <c>t</c> along the ray where it intersects the parallelogram.</returns>
    public double GetIntersection(Ray ray)
    {
        double? t = GetPlaneDistance(ray);

        if (t is null)
            return double.NaN;

        return TryCreateIntersection(ray.At(t.Value), t.Value) is not null ? t.Value : double.NaN;
    }

    /// <summary>
    /// This method is used to test whether the given point, already known to lie on the
    /// parallelogram's plane, actually lies within its sides.
    /// </summary>
    /// <param name="point">The point, on the parallelogram's plane, to test.</param>
    /// <param name="distance">The distance along the ray where the point lies.</param>
    /// <returns>The appropriate intersection object, or <c>null</c> if the point is outside
    /// the parallelogram's sides.</returns>
    protected override Intersection TryCreateIntersection(Point point, double distance)
    {
        Vector vector = point - Point;
        double alpha = _constantW.Dot(vector.Cross(Side1));
        double beta = _constantW.Dot(Side2.Cross(vector));

        return alpha is >= 0 and <= 1 && beta is >= 0 and <= 1
            ? new Intersection(this, distance)
            : null;
    }
}
