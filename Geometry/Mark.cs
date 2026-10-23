using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a mark: a named place on a thing and nothing else, carried wherever the thing goes, which
/// joints aim at and reach for and spans are stretched between -- a crank pin on a wheel, the tip of an arm.  It is
/// seen by no ray and casts no shadow, and its box is empty, so it is left out of whatever holds it when that is
/// arranged for searching.
/// </summary>
public class Mark : Surface
{
    /// <summary>
    /// This method returns the mark's box, which is empty: it occupies no room at all.
    /// </summary>
    /// <returns>An empty box.</returns>
    protected override BoundingBox GetDefaultBoundingBox() => new ();

    /// <summary>
    /// This method adds nothing, since no ray meets a mark.
    /// </summary>
    /// <param name="ray">The ray.</param>
    /// <param name="intersections">The list nothing is added to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
    }

    /// <summary>
    /// This method is never asked, since no ray meets a mark.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="intersection">The intersection.</param>
    /// <returns>Nothing.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        throw new InvalidOperationException("A mark has no surface for a ray to meet.");
    }
}
