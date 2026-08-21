using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a group of other surfaces that make a single surface that can
/// be treated as, well, a group.
/// </summary>
public class Group : Surface
{
    /// <summary>
    /// This property holds the list of child surfaces we carry.  Do not add surfaces to
    /// this list directly; use the <c>Add()</c> method instead.
    /// </summary>
    public List<Surface> Surfaces { get; } = [];

    /// <summary>
    /// This method is used to add a surface to the group.
    /// </summary>
    /// <param name="surface">The surface to add.</param>
    /// <returns>This object, for fluency.</returns>
    public Group Add(Surface surface)
    {
        Surfaces.Add(surface);

        surface.Parent = this;

        return this;
    }

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        foreach (Surface surface in Surfaces)
            surface.PrepareForRendering(SampleTimes);

        if (Material is not null)
        {
            foreach (Surface surface in new SurfaceIterator(Surfaces).Surfaces)
                surface.Material ??= Material;
        }
    }

    /// <summary>
    /// This method is used to produce a default bounding box for this shape.
    /// </summary>
    /// <returns>A default bounding box, if any, for the surface.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        BoundingBox box = new ();

        foreach (Surface surface in Surfaces)
        {
            BoundingBox child = BoxAround(surface);

            if (child is null)
            {
                // This child has no way to report a box of its own -- an endless cylinder, say, or a
                // plane -- so its region cannot safely be ruled out, and the group as a whole must be
                // unbounded too.  Quietly building an aggregate box without it would be a box too
                // small to hold the group, and rays aimed at that child would be turned away before
                // Group.AddIntersections ever got a chance to test it.
                return null;
            }

            box.Add(child);
        }

        // An empty box is returned rather than none at all, and the difference is the whole of this
        // fix.  A group with nothing in it can be hit by nothing, so its box should turn every ray
        // away; saying "no box" instead says the opposite -- come in and test everything -- and worse,
        // a group is unbounded whenever any child is, so one empty group robbed every group above it
        // of its box too.  In a-stand-of-trees that was 328 empty groups poisoning 447 more.
        return box;
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the cube and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        List<Intersection> ours = [];

        foreach (Surface surface in Surfaces)
            surface.Intersect(ray, ours);

        ours.Sort();

        intersections.AddRange(ours);
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
        throw new Exception("This method should never be called!");
    }
}
