using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents an extrusion.  It is defined by one or more closed paths in 2D
/// that are extruded along the Y axis.
/// </summary>
public class Extrusion : ExtrudedSurface, IDisposable
{
    /// <summary>
    /// This attribute holds the path that represents the outline of the extrusion in the
    /// X/Z plane.
    /// </summary>
    public GeneralPath Path { get; set; }

    private readonly List<PathSurface> _surfaces = [];

    private Parallelogram _top;
    private Parallelogram _bottom;

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        _surfaces.Clear();
        _surfaces.AddRange(Path.Segments.Select(ToSurface));

        if (Closed)
        {
            _top = new Parallelogram
            {
                Point = new Point(Path.MinX, MaximumY, Path.MinY),
                Side1 = new Vector(Path.MaxX - Path.MinX, 0, 0),
                Side2 = new Vector(0, 0, Path.MaxY - Path.MinY)
            };
            _bottom = new Parallelogram
            {
                Point = new Point(Path.MinX, MinimumY, Path.MinY),
                Side1 = new Vector(Path.MaxX - Path.MinX, 0, 0),
                Side2 = new Vector(0, 0, Path.MaxY - Path.MinY)
            };
        }
    }

    /// <summary>
    /// This method is used to produce a default bounding box for this shape.
    /// </summary>
    /// <returns>A default bounding box, if any, for the surface.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        return new BoundingBox()
            .Add(new Point(Path.MinX, MinimumY, Path.MinY))
            .Add(new Point(Path.MaxX, MaximumY, Path.MaxY));
    }

    /// <summary>
    /// This method is used to convert a path segment into its representative surface.
    /// </summary>
    /// <param name="segment">The segment to convert.</param>
    /// <returns>The segment as a surface.</returns>
    private PathSurface ToSurface(PathSegment segment)
    {
        return segment switch
        {
            LinearPathSegment line => new LinearPathSurface(line, MinimumY, MaximumY),
            QuadPathSegment quad => new QuadPathSurface(quad, MinimumY, MaximumY),
            CubicPathSegment cubic => new CubicPathSurface(cubic, MinimumY, MaximumY),
            _ => throw new NotSupportedException()
        };
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the cube and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        if (Closed)
        {
            AddCapIntersections(ray, intersections, _top);
            AddCapIntersections(ray, intersections, _bottom);
        }

        intersections.AddRange(_surfaces.Select(surface => surface.GetIntersection(ray))
            .Where(intersectionData => intersectionData != null)
            .SelectMany(intersectionData => intersectionData)
            .Where(intersectionData => intersectionData != null)
            .Select(data => data.AsIntersection(this)));
    }

    /// <summary>
    /// This method is used to add any intersections our ray has with the cap at the given
    /// height.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add intersections to.</param>
    /// <param name="cap">The parallelogram we are suing for a cap.</param>
    private void AddCapIntersections(Ray ray, List<Intersection> intersections, Parallelogram cap)
    {
        double intersection = cap.GetIntersection(ray);

        if (!double.IsNaN(intersection))
        {
            Point point = ray.At(intersection);
            TwoDPoint twoDPoint = new TwoDPoint(point.X, point.Z);

            if (Path.Contains(twoDPoint))
                intersections.Add(new Intersection(this, intersection));
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
        return intersection is PrecomputedNormalIntersection precomputed
            ? precomputed.PrecomputedNormal
            : point.Y < MinimumY + (MaximumY - MinimumY) / 2
                ? Directions.Down
                : Directions.Up;
    }

    /// <summary>
    /// This method is used to properly clean up our resources.
    /// </summary>
    public void Dispose()
    {
        Path?.Dispose();
        Path = null;

        GC.SuppressFinalize(this);
    }
}
