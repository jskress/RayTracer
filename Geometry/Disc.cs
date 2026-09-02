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
    /// <summary>
    /// This method returns the box the disc lies in, worked out from where it is, which way it faces
    /// and how big it is.
    /// <para>
    /// A circle of radius <c>r</c> facing <c>n</c> reaches <c>r * sqrt(1 - n.i^2)</c> along each axis
    /// <c>i</c>: none at all along the way it faces, the whole radius across it.  That is exact rather
    /// than a cube of side <c>2r</c>, which for a disc lying flat would be a box mostly full of
    /// nothing.
    /// </para>
    /// </summary>
    /// <returns>The box the disc lies in.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        Vector facing = Normal;
        Vector reach = new (
            Radius * Math.Sqrt(Math.Max(0, 1 - facing.X * facing.X)),
            Radius * Math.Sqrt(Math.Max(0, 1 - facing.Y * facing.Y)),
            Radius * Math.Sqrt(Math.Max(0, 1 - facing.Z * facing.Z)));

        return new BoundingBox()
            .Add(new Point(Center.X - reach.X, Center.Y - reach.Y, Center.Z - reach.Z))
            .Add(new Point(Center.X + reach.X, Center.Y + reach.Y, Center.Z + reach.Z));
    }
}
