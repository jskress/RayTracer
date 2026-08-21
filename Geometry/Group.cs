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

        ArrangeChildren();
    }

    /// <summary>
    /// This method sorts the children into a tree of nested boxes, so that a ray reaching this group
    /// need not be shown every last thing in it.
    /// <para>
    /// The children are prepared first, just above, and that order is not incidental: a child cannot
    /// say what box it occupies until it has worked one out for itself.
    /// </para>
    /// <para>
    /// A child that cannot say where it is -- a plane, an endless cylinder -- has no place in a tree
    /// of boxes, so those are kept aside and handed every ray.  There are only ever a few of them, and
    /// a group holding one is unbounded anyway.
    /// </para>
    /// </summary>
    private void ArrangeChildren()
    {
        List<(Surface Surface, BoundingBox Box)> placed = [];

        _unbounded = null;

        foreach (Surface surface in Surfaces)
        {
            BoundingBox box = BoxAround(surface);

            if (box is null)
                (_unbounded ??= []).Add(surface);
            else if (!box.IsEmpty)
                placed.Add((surface, box));

            // A child with an empty box occupies no region at all -- an empty group -- so it can be
            // hit by nothing and is left out of both lists.
        }

        _hierarchy = BoundingVolumeHierarchy.Build(placed);

        // Whatever was not worth arranging is walked the old way.  Below the threshold the walk is
        // faster than any search of it, and this is the common case by count: most groups an author
        // writes hold two or three things.
        _walked = _hierarchy is null
            ? placed.Select(entry => entry.Surface).ToList()
            : null;
    }

    private BoundingVolumeHierarchy _hierarchy;
    private List<Surface> _walked;
    private List<Surface> _unbounded;

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
    /// <summary>
    /// This method answers a shadow query, which differs from an ordinary one in what it may throw
    /// away rather than in how far it looks.  The two are kept as separate paths on purpose: sharing
    /// one, with an infinite bound standing for "unbounded", conflates a sky light -- whose distance
    /// really is infinite -- with an ordinary intersection that must keep everything.
    /// </summary>
    protected override void AddIntersectionsWithin(
        Ray ray, List<Intersection> intersections, double maxDistance)
    {
        List<Intersection> ours = [];

        if (_hierarchy is not null)
            _hierarchy.IntersectWithin(ray, ours, maxDistance);
        else
        {
            foreach (Surface surface in _walked ?? Surfaces)
                surface.IntersectWithin(ray, ours, maxDistance);
        }

        if (_unbounded is not null)
        {
            // The planes and endless cylinders, which have no box to be ruled out by, so they are
            // asked about every ray however short a stretch of it is wanted.  About half the crossings
            // a shadow ray throws away come from exactly here, and that is the ceiling on what any
            // amount of box pruning can save.
            foreach (Surface surface in _unbounded)
                surface.IntersectWithin(ray, ours, maxDistance);
        }

        intersections.AddRange(ours);
    }

    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        List<Intersection> ours = [];

        if (_hierarchy is not null)
            _hierarchy.Intersect(ray, ours);
        else
        {
            // With nothing arranged, this group was never prepared for rendering -- as happens in a
            // test that builds a group and asks it about a ray directly.  Walking what is there is
            // the honest answer.
            foreach (Surface surface in _walked ?? Surfaces)
                surface.Intersect(ray, ours);
        }

        if (_unbounded is not null)
        {
            foreach (Surface surface in _unbounded)
                surface.Intersect(ray, ours);
        }

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
