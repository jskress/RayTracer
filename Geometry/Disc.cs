using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a disc: a flat circle (or, with a non-zero inner radius, a ring)
/// defined by a center point, a normal, and a radius.
/// </summary>
public class Disc : FlatSurface
{
    /// <summary>
    /// This property provides the center point of the disc.
    /// </summary>
    public Point Center
    {
        get => field;
        set
        {
            field = value;

            DefinitionChanged(value, Normal);
        }
    }

    /// <summary>
    /// This property provides the normal of the disc's plane.  Unlike
    /// <see cref="Parallelogram"/>'s or <see cref="Triangle"/>'s normal, which are derived
    /// from the shape's other points, a disc's normal is one of its primary inputs, so
    /// (unlike the base class) it needs a public setter.
    /// </summary>
    public new Vector Normal
    {
        get => base.Normal;
        set
        {
            base.Normal = value?.Unit;

            DefinitionChanged(Center, base.Normal);
        }
    }

    /// <summary>
    /// This method is used to reset our plane constant when our center or normal changes.
    /// If either is <c>null</c> (as will be during initial creation), we silently no-op.
    /// </summary>
    /// <param name="center">The center point of the disc.</param>
    /// <param name="normal">The (already-normalized) normal of the disc's plane.</param>
    private void DefinitionChanged(Point center, Vector normal)
    {
        if (center is not null && normal is not null)
            PlaneConstant = normal.Dot(center);
    }

    /// <summary>
    /// This property provides the radius of the disc.
    /// </summary>
    public double Radius { get; set; }

    /// <summary>
    /// This property provides the inner radius of the disc.  When greater than zero, the
    /// disc becomes a ring, with the area within this radius excluded from the surface.
    /// </summary>
    public double InnerRadius { get; set; }

    /// <summary>
    /// This method is used to test whether the given point, already known to lie on the
    /// disc's plane, actually lies between its inner and outer radii.
    /// </summary>
    /// <param name="point">The point, on the disc's plane, to test.</param>
    /// <param name="distance">The distance along the ray where the point lies.</param>
    /// <returns>The appropriate intersection object, or <c>null</c> if the point is outside
    /// the disc.</returns>
    protected override Intersection TryCreateIntersection(Point point, double distance)
    {
        double radialDistance = (point - Center).Magnitude;

        return radialDistance >= InnerRadius && radialDistance <= Radius
            ? new Intersection(this, distance)
            : null;
    }
}
