using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a smooth triangle.  It is defined by three points and the normals
/// at those points.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public class SmoothTriangle : Triangle
{
    /// <summary>
    /// This property provides the normal at the first point of the triangle.
    /// </summary>
    public Vector Normal1 { get; set; }

    /// <summary>
    /// This property provides the normal at the second point of the triangle.
    /// </summary>
    public Vector Normal2 { get; set; }

    /// <summary>
    /// This property provides the normal at the third point of the triangle.
    /// </summary>
    public Vector Normal3 { get; set; }

    /// <summary>
    /// This is a helper method for creating an intersection.  It's overridable since
    /// smooth triangles the u/v pair.
    /// </summary>
    /// <param name="distance">The distance along the ray where the intersection occurred.</param>
    /// <param name="u">The U value for the intersection with the triangle.</param>
    /// <param name="v">The V value for the intersection with the triangle.</param>
    /// <returns>The appropriate intersection object.</returns>
    protected override Intersection CreateIntersection(double distance, double u, double v)
    {
        return new SmoothTriangleIntersection(this, distance, u, v);
    }

    /// <summary>
    /// This method returns the normal for the cube.  It is assumed that the point will
    /// have been transformed to surface-space coordinates.  The vector returned will
    /// also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        SmoothTriangleIntersection smoothTriangleIntersection = (SmoothTriangleIntersection) intersection;
        double u = smoothTriangleIntersection.U;
        double v = smoothTriangleIntersection.V;

        return Normal2 * u + Normal3 * v + Normal1 * (1 - u - v);
    }

    /// <summary>
    /// This method returns how far above the flat triangle a ray leaving it, on the side its normals face, sets off:
    /// up to about where the round surface its normals describe would be (Hanika 2021, "Hacking the Shadow
    /// Terminator").
    /// <para>
    /// **A smooth triangle is shaded as round and shadowed as flat.**  Where the light grazes it, its normal faces
    /// the light while the flat triangle, or the next one along, stands in the way of its own shadow ray; so the
    /// edge of a shadow across a coarse mesh is a staircase of whole facets, and a smooth height field's hills
    /// show their triangles where the light leaves them.  Set off instead from the place met carried up onto the
    /// plane each corner's normal stands square to, weighed as much as that corner counts there, a ray starts
    /// where the rounded surface would be and clears the facets beside it.
    /// </para>
    /// <para>
    /// **Only ever up.**  A place already above a corner's plane, where the mesh bends the other way, is not
    /// pulled down to it, so a hollow keeps its rays where they were; and a triangle whose corners all face as it
    /// does rises nothing at all.  Nor does a place rise along a corner's normal that faces behind the flat
    /// triangle, as a corner on a crest too sharp for the mesh may: that way lies under the triangle itself.
    /// </para>
    /// <para>
    /// **In the world, not in the triangle's own space.**  A corner's plane is carried by any transform, but how
    /// far a place is from it, and the way straight up to it, are not.  A height field is drawn in a unit square
    /// and stretched: the dunes' 1400 across and 26 up made the sand 54 times as steep in its own space as in the
    /// world, where a crest's normal lay nearly flat; risen along it there, a place slid sideways under the slope
    /// beside it, and the crests were edged with black.
    /// </para>
    /// </summary>
    /// <param name="intersection">Where the ray met the triangle.</param>
    /// <returns>How far to rise, in the world, or <c>null</c> for not at all.</returns>
    internal override Vector RiseAt(Intersection intersection)
    {
        if (intersection is not SmoothTriangleIntersection smooth)
            return null;

        double u = smooth.U;
        double v = smooth.V;
        int time = intersection.TimeIndex;
        Portals portals = intersection.Portals;
        Point place = Point1 + (Point2 - Point1) * u + (Point3 - Point1) * v;
        Vector flat = (Point2 - Point1).Cross(Point3 - Point1);

        // The flat triangle faces whichever way its normals do.
        if (flat.Dot(SurfaceNormalAt(place, intersection)) < 0)
            flat = -flat;

        flat = NormalToWorld(flat, time, portals);

        Vector rise = RiseTo(Point1, Normal1, 1 - u - v) + RiseTo(Point2, Normal2, u) + RiseTo(Point3, Normal3, v);

        return rise.X == 0 && rise.Y == 0 && rise.Z == 0 ? null : rise;

        // How far, in the world, the place must rise to stand on the plane one corner's normal is square to, if it
        // lies below it, weighed by how much that corner counts there.
        Vector RiseTo(Point corner, Vector normal, double weight)
        {
            Vector unit = NormalToWorld(normal, time, portals);
            double below = DirectionToWorld(corner - place, time, portals).Dot(unit);

            return below > 0 && unit.Dot(flat) > 0 ? unit * (below * weight) : new Vector(0, 0, 0);
        }
    }
}
