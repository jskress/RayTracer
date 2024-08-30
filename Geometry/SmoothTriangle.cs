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
    public Vector Normal1 { get; set; }

    /// <summary>
    /// This property provides the normal at the second point of the triangle.
    /// </summary>
    public Vector Normal2 { get; set; }

    /// <summary>
    /// This property provides the normal at the third point of the triangle.
    /// </summary>
    public Vector Normal3 { get; set; }

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
