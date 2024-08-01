using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a triangle.  It is defined by three points.
/// </summary>
public class Triangle : Surface
{
    /// <summary>
    /// This property provides the first point of the triangle.
    /// </summary>
    public Point Point1 { get; }

    /// <summary>
    /// This property provides the second point of the triangle.
    /// </summary>
    public Point Point2 { get; }

    /// <summary>
    /// This property provides the third point of the triangle.
    /// </summary>
    public Point Point3 { get; }

    private readonly Vector _e1;
    private readonly Vector _e2;
    private readonly Vector _normal;

    public Triangle()
    {
        // This constructor is present to satisfy the type system but should never
        // be used, so...
        throw new Exception("Internal error: cannot create triangles this way.");
    }

    public Triangle(Point point1, Point point2, Point point3)
    {
        _e1 = point2 - point1;
        _e2 = point3 - point1;
        _normal = _e2.Cross(_e1).Unit;

        Point1 = point1;
        Point2 = point2;
        Point3 = point3;
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the cube and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        Vector dirCrossE2 = ray.Direction.Cross(_e2);
        double determinant = _e1.Dot(dirCrossE2);

        if (determinant.Near(0))
            return;

        double f = 1 / determinant;
        Vector p1ToOrigin = ray.Origin - Point1;
        double u = f * p1ToOrigin.Dot(dirCrossE2);

        if (u is < 0 or > 1)
            return;

        Vector originCrossE1 = p1ToOrigin.Cross(_e1);
        double v = f * ray.Direction.Dot(originCrossE1);

        if (v < 0 || u + v > 1)
            return;

        double t = f * _e2.Dot(originCrossE1);

        intersections.Add(CreateIntersection(t, u, v));
    }

    /// <summary>
    /// This is a helper method for creating an intersection.  It's overridable since
    /// smooth triangles the the u/v pair.
    /// </summary>
    /// <param name="distance">The distance along the ray where the intersection occurred.</param>
    /// <param name="u">The U value for the intersection with the triangle.</param>
    /// <param name="v">The V value for the intersection with the triangle.</param>
    /// <returns>The appropriate intersection object.</returns>
    protected virtual Intersection CreateIntersection(double distance, double u, double v)
    {
        return new Intersection(this, distance);
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
        return _normal;
    }
}
