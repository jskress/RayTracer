using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class holds a set of surfaces arranged so that a ray need not be shown every last one of
/// them: a tree of nested boxes over those that can say where they are, and a short list of those
/// that cannot.
/// <para>
/// It is shared by <see cref="Group"/> and by the scene itself, and that sharing is the point.  The
/// arrangement was a group's alone for a long time, so the surfaces standing at the top of a scene
/// were walked one by one for every ray -- which cost nothing when a scene held a dozen things and a
/// great deal when it held thousands.  Measured on four thousand grass blades written at the top of
/// a scene: 43 seconds walked, 0.7 seconds arranged.
/// </para>
/// </summary>
internal sealed class ArrangedSurfaces
{
    private BoundingVolumeHierarchy _hierarchy;
    private List<Surface> _walked;
    private List<Surface> _unbounded;

    /// <summary>
    /// This property notes whether anything has been arranged yet.  Nothing has, in a test that
    /// builds geometry and asks it about a ray without preparing it first.
    /// </summary>
    internal bool IsArranged => _hierarchy is not null || _walked is not null;

    /// <summary>
    /// This method sorts the given surfaces into a tree of boxes, keeping aside those with no box to
    /// be ruled out by.
    /// <para>
    /// The surfaces must already be prepared and placed: a surface cannot say where it is until it
    /// knows its own shape, and one put in terms of its neighbours does not stand where it will until
    /// those have been settled.
    /// </para>
    /// </summary>
    /// <param name="surfaces">The surfaces to arrange.</param>
    internal void Arrange(List<Surface> surfaces)
    {
        List<(Surface Surface, BoundingBox Box)> placed = [];

        _unbounded = null;

        foreach (Surface surface in surfaces)
        {
            BoundingBox box = Surface.BoxAround(surface);

            if (box is null)
                (_unbounded ??= []).Add(surface);
            else if (!box.IsEmpty)
                placed.Add((surface, box));

            // A surface with an empty box occupies no region at all -- an empty group -- so it can be
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

    /// <summary>
    /// This method adds every crossing the ray makes with what has been arranged.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    /// <param name="unarranged">What to walk if nothing has been arranged yet.</param>
    internal void Intersect(Ray ray, List<Intersection> intersections, List<Surface> unarranged)
    {
        if (_hierarchy is not null)
            _hierarchy.Intersect(ray, intersections);
        else
        {
            foreach (Surface surface in _walked ?? unarranged)
                surface.Intersect(ray, intersections);
        }

        if (_unbounded is null)
            return;

        foreach (Surface surface in _unbounded)
            surface.Intersect(ray, intersections);
    }

    /// <summary>
    /// This method adds the crossings a shadow query wants, which differs from an ordinary one in
    /// what it may throw away rather than in how far it looks.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    /// <param name="maxDistance">How far along the ray to care about.</param>
    /// <param name="unarranged">What to walk if nothing has been arranged yet.</param>
    internal void IntersectWithin(
        Ray ray, List<Intersection> intersections, double maxDistance, List<Surface> unarranged)
    {
        if (_hierarchy is not null)
            _hierarchy.IntersectWithin(ray, intersections, maxDistance);
        else
        {
            foreach (Surface surface in _walked ?? unarranged)
                surface.IntersectWithin(ray, intersections, maxDistance);
        }

        if (_unbounded is null)
            return;

        // The planes and endless cylinders, which have no box to be ruled out by, so they are asked
        // about every ray however short a stretch of it is wanted.  About half the crossings a shadow
        // ray throws away come from exactly here, and that is the ceiling on what any amount of box
        // pruning can save.
        foreach (Surface surface in _unbounded)
            surface.IntersectWithin(ray, intersections, maxDistance);
    }
}
