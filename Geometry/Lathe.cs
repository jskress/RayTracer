using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a "lathe".  It is defined by one or more closed paths in 2D
/// that are rotated around the Y axis.
/// </summary>
public class Lathe : Surface
{
    /// <summary>
    /// This property holds the path that represents the outline of the shape in the
    /// X/Y plane that will be rotated to create the surface.
    /// </summary>
    public GeneralPath Path { get; set; }

    private readonly List<LathePathSurface> _surfaces = [];

    private Cylinder _bounds;
    private double _radius;

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        _surfaces.Clear();
        _surfaces.AddRange(Path.Segments
            .Select(segment => new LathePathSurface(segment)));

        _radius = Math.Max(Math.Abs(Path.MinX), Math.Abs(Path.MaxX));

        _bounds = new Cylinder
        {
            MinimumY = Path.MinY - DoubleExtensions.Epsilon,
            MaximumY = Path.MaxY + DoubleExtensions.Epsilon,
            Transform = Basics.Transforms.Scale(
                _radius + DoubleExtensions.Epsilon, 1, _radius + DoubleExtensions.Epsilon)
        };
    }

    /// <summary>
    /// This method is used to produce a default bounding box for this shape.  Since
    /// revolving the profile around the Y axis sweeps its radius through every angle, the
    /// box must span the full radius in both X and Z, not just the profile's own (possibly
    /// one-sided) X extent.
    /// </summary>
    /// <returns>A default bounding box, if any, for the surface.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        return new BoundingBox()
            .Add(new Point(-_radius, Path.MinY, -_radius))
            .Add(new Point(_radius, Path.MaxY, _radius));
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the cube and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        if (Misses(ray))
            return;

        intersections.AddRange(_surfaces
            .SelectMany(surface => surface.GetIntersections(this, ray)));
    }

    private bool Misses(Ray ray)
    {
        List<Intersection> intersections = [];

        _bounds.Intersect(ray, intersections);

        return intersections.Count == 0;
    }

    /// <summary>
    /// This method returns the normal for the lathe.  It is assumed that the point will
    /// have been transformed to surface-space coordinates.  The vector returned will
    /// also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point, Intersection intersection)
    {
        return ((PrecomputedNormalIntersection) intersection).PrecomputedNormal;
    }
}
