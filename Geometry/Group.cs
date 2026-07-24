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
        BoundingBox box = new BoundingBox();

        foreach (Surface surface in Surfaces)
        {
            if (surface.BoundingBox == null && surface is not Triangle)
            {
                // This child has no way to report a bounding box of its own (e.g. a Disc,
                // Parallelogram, or any other surface that's unbounded by default) -- since
                // its region can't be safely excluded from ray testing, the group as a
                // whole must be unbounded too, rather than silently building an aggregate
                // box that's too small to include it (which would cull rays aimed at this
                // child before Group.AddIntersections ever got a chance to test it).
                return null;
            }

            // A child that moves is taken in every place it stands while the shutter is open, not
            // merely where it starts.  A box drawn around its first position alone would have this
            // group turn away rays that ought to have found it further along its travels, and the
            // thing would be cut off part way through its own blur.  Since a ray only ever sees one
            // of the instants sampled, gathering exactly those is no approximation of the path
            // swept -- it is the whole of what any ray can find.
            foreach (Matrix transform in surface.TransformsThroughShutter)
            {
                if (surface.BoundingBox != null)
                    box.Add(surface.BoundingBox.TransformedBy(transform));
                else if (surface is Triangle triangle)
                {
                    box.Add(transform * triangle.Point1);
                    box.Add(transform * triangle.Point2);
                    box.Add(transform * triangle.Point3);
                }
            }
        }

        return box.IsEmpty ? null : box;
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
