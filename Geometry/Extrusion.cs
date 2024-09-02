using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents an extrusion.  It is defined by one or more closed paths in 2D
/// that are extruded along the Y axis.
/// </summary>
public class Extrusion : ExtrudedSurface
{
    public GeneralPath Path { get; set; }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the cube and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        AddCapIntersections(ray, intersections, MinimumY);
        AddCapIntersections(ray, intersections, MaximumY);

        foreach (PathSegment segment in Path.Segments)
            segment.AddIntersections(this, ray, intersections);
    }

    /// <summary>
    /// This method is used to add any intersections our ray has with the cap at the given
    /// height.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add intersections to.</param>
    /// <param name="height">The height of the cap to test.</param>
    private void AddCapIntersections(Ray ray, List<Intersection> intersections, double height)
    {
        double k = (height - ray.Origin.Y) / ray.Direction.Y;
        TwoDPoint point = new TwoDPoint(
            ray.Origin.X + k * ray.Direction.X,
            ray.Origin.Z + k * ray.Direction.Z
        );

        if (Path.Contains(point))
            intersections.Add(new Intersection(this, k / ray.Direction.Magnitude));
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
        if (intersection is PathSegmentIntersection segmentIntersection)
        {
            PathSegment segment = segmentIntersection.Segment;
            double distance = intersection.Distance;

            return new Vector(
                distance * (3.0 * segment.A.Y * distance + 2.0 * segment.B.Y) + segment.C.Y,
                0,
                -(distance * (3.0 * segment.A.X * distance + 2.0 * segment.B.X) + segment.C.X)
            );
        }

        return point.Y < MinimumY + (MaximumY - MinimumY) / 2
            ? Directions.Up
            : Directions.Down;
    }
}
