using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a cube.  It is defined as centered at the origin and extends from
/// <c>-1</c> to <c>1</c> along each axis.
/// </summary>
public class Cube : Surface
{
    /// <summary>
    /// This holds a bounding box that aligns with our own shape.  Our ray/intersection
    /// stuff is delegated to this.
    /// </summary>
    private static readonly BoundingBox OurShape = new BoundingBox()
        .Add(new Point(-1, -1, -1))
        .Add(new Point(1, 1, 1));

    /// <summary>
    /// This method returns the box a cube sits in, which is the cube itself -- the same extents
    /// <see cref="OurShape"/> already holds.
    /// <para>
    /// Two things about that are worth saying plainly.  A fresh box is handed back rather than
    /// <c>OurShape</c> itself because whoever asks for it pads it a little afterward, and padding the
    /// one every cube tests against would quietly grow every cube in every scene, a hundredth of a
    /// hair at a time.  And a cube gains nothing from having a box of its own, since testing the box
    /// is exactly the work of testing the cube: it is here so that a <see cref="Group"/> holding cubes
    /// can work out a box of its own, which it cannot do while any child has none.  That is worth one
    /// repeated slab test on the cheapest shape there is.
    /// </para>
    /// </summary>
    /// <returns>The box this cube sits in.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        return new BoundingBox()
            .Add(new Point(-1, -1, -1))
            .Add(new Point(1, 1, 1));
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the cube and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        (double tMin, double tMax) = OurShape.GetIntersections(ray);

        if (tMin <= tMax)
        {
            intersections.Add(new Intersection(this, tMin));
            intersections.Add(new Intersection(this, tMax));
        }
    }

    /// <summary>
    /// This method returns the normal for the cube.  It is assumed that the point will
    /// have been transformed to surface-space coordinates.  The vector returned will
    /// also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point, Intersection intersection)
    {
        double x = Math.Abs(point.X);
        double y = Math.Abs(point.Y);
        double z = Math.Abs(point.Z);
        double max = Math.Max(x, Math.Max(y, z));

        if (max.Near(x))
            return new Vector(point.X, 0, 0);

        return max.Near(y)
            ? new Vector(0, point.Y, 0)
            : new Vector(0, 0, point.Z);
    }
}
