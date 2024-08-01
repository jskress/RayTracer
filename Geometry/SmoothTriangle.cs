using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a smooth triangle.  It is defined by three points and the normals
/// at those points.
/// </summary>
public class SmoothTriangle : Triangle
{
    /// <summary>
    /// This property provides the normal at the first point of the triangle.
    /// </summary>
    public Vector Normal1 { get; }

    /// <summary>
    /// This property provides the normal at the second point of the triangle.
    /// </summary>
    public Vector Normal2 { get; }

    /// <summary>
    /// This property provides the normal at the third point of the triangle.
    /// </summary>
    public Vector Normal3 { get; }

    public SmoothTriangle()
    {
        // This constructor is present to satisfy the type system but should never
        // be used, so...
        throw new Exception("Internal error: cannot create smooth triangles this way.");
    }

    public SmoothTriangle(
        Point point1, Point point2, Point point3,
        Vector normal1, Vector normal2, Vector normal3) : base(point1, point2, point3)
    {
        Normal1 = normal1;
        Normal2 = normal2;
        Normal3 = normal3;
    }

    /// <summary>
    /// This is a helper method for creating an intersection.  It's overridable since
    /// smooth triangles the the u/v pair.
    /// </summary>
    /// <param name="distance">The distance along the ray where the intersection occurred.</param>
    /// <param name="u">The U value for the intersection with the triangle.</param>
    /// <param name="v">The V value for the intersection with the triangle.</param>
    /// <returns>The appropriate intersection object.</returns>
    protected override Intersection CreateIntersection(double distance, double u, double v)
    {
        return new SmoothTriangleIntersection(this, distance, u, v);
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
        SmoothTriangleIntersection smoothTriangleIntersection = (SmoothTriangleIntersection) intersection;
        double u = smoothTriangleIntersection.U;
        double v = smoothTriangleIntersection.V;

        return Normal2 * u + Normal3 * v + Normal1 * (1 - u - v);
    }
}
