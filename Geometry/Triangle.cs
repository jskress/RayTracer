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
    public Point Point1
    {
        get => _point1;
        set
        {
            _point1 = value;

            PointChanged(value, _point2, _point3);
        }
    }

    /// <summary>
    /// This property provides the second point of the triangle.
    /// </summary>
    public Point Point2
    {
        get => _point2;
        set
        {
            _point2 = value;

            PointChanged(_point1, value, _point3);
        }
    }

    /// <summary>
    /// This property provides the third point of the triangle.
    /// </summary>
    public Point Point3
    {
        get => _point3;
        set
        {
            _point3 = value;

            PointChanged(_point1, _point2, value);
        }
    }

    private Point _point1;
    private Point _point2;
    private Point _point3;
    private Vector _e1;
    private Vector _e2;
    private Vector _normal;

    /// <summary>
    /// This method is used to reset our control information when one of our points change.
    /// If any of the points is <c>null</c> (as will be during initial creation), we
    /// silently no-op.
    /// </summary>
    /// <param name="point1">Point 1 of the triangle</param>
    /// <param name="point2">Point 2 of the triangle</param>
    /// <param name="point3">Point 3 of the triangle</param>
    private void PointChanged(Point point1, Point point2, Point point3)
    {
        if (point1 is not null && point2 is not null && point3 is not null)
        {
            _e1 = point2 - point1;
            _e2 = point3 - point1;
            _normal = _e2.Cross(_e1).Unit;
        }
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the triangle and,
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
    /// This method returns the normal for the triangle.  It is assumed that the point will
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
