using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a surface made of strands -- a single <see cref="Strand"/>, or hair grown over
/// another surface -- hit and shaded through the <see cref="StrandSet"/> that holds them.
/// </summary>
public abstract class StrandSurface : Surface
{
    /// <summary>
    /// This property holds the strands, once they are made.
    /// </summary>
    protected StrandSet Strands { get; set; }

    /// <summary>
    /// This method adds every crossing of the given ray with the strands, behind its start as well as
    /// ahead, as every surface owes an ordinary query.
    /// </summary>
    /// <param name="ray">The ray, in the surface's own space.</param>
    /// <param name="intersections">The list to add to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        Strands?.Intersect(ray, this, intersections);
    }

    /// <summary>
    /// This method adds the crossings a shadow ray needs: those between its start and the light.
    /// </summary>
    protected override void AddIntersectionsWithin(Ray ray, List<Intersection> intersections, double maxDistance)
    {
        Strands?.Intersect(ray, this, intersections, 0, maxDistance);
    }

    /// <summary>
    /// This method returns the surface a crossing of the given strand is reported on, which is the one
    /// whose material it is shaded with: this one, unless its strands wear more than one.
    /// </summary>
    /// <param name="strand">Which strand was crossed.</param>
    /// <returns>The surface to report the crossing on.</returns>
    internal virtual Surface WearerOf(int strand) => this;

    /// <summary>
    /// This method returns the normal the strand is shaded with where the ray crossed it, which the
    /// crossing carries; see <see cref="HairIntersection"/>.
    /// </summary>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        return ((HairIntersection) intersection).StrandNormal;
    }

    /// <summary>
    /// This method returns the box around the strands.
    /// </summary>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        BoundingBox box = new ();

        if (Strands is not null)
            box.Add(Strands.Bounds);

        return box;
    }
}
