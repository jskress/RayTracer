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
    public List<Surface> Surfaces { get; } = new ();

    /// <summary>
    /// This property holds an optional bounding box for the group.
    /// </summary>
    public BoundingBox BoundingBox { get; set; }

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
    /// This method is used to determine whether the given ray intersects the cube and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        List<Intersection> ours = new ();

        if (BoundingBox != null)
        {
            (double tMin, double tMax) = BoundingBox.GetIntersections(ray);

            // If we dont even intersect the bounding box, then don't try anything else.
            if (tMin > tMax)
                return;
        }

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
