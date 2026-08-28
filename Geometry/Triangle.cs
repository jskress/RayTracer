using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a triangle.  It is defined by three points.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public class Triangle : Surface
{
    /// <summary>
    /// This property provides the first point of the triangle.
    /// </summary>
    public Point Point1
    {
        get => field;
        set
        {
            field = value;

            PointChanged(value, Point2, Point3);
        }
    }

    /// <summary>
    /// This property provides the second point of the triangle.
    /// </summary>
    public Point Point2
    {
        get => field;
        set
        {
            field = value;

            PointChanged(Point1, value, Point3);
        }
    }

    /// <summary>
    /// This property provides the third point of the triangle.
    /// </summary>
    public Point Point3
    {
        get => field;
        set
        {
            field = value;

            PointChanged(Point1, Point2, value);
        }
    }

    /// <summary>
    /// How nearly a ray must run along a triangle before it is treated as missing it: the cosine of
    /// the angle between them.
    /// </summary>
    private const double ParallelTolerance = 1e-12;

    private Vector _e1;
    private Vector _e2;

    /// <summary>
    /// The square of twice the triangle's area, which is what tells a ray running *along* the
    /// triangle from one merely meeting a small triangle.
    /// </summary>
    private double _crossSquared;
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
            Vector cross = _e2.Cross(_e1);

            _normal = cross.Unit;
            _crossSquared = cross.Dot(cross);
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

        // **This has to be judged against the size of the things going into it, not against a fixed
        // number.**  The determinant is the ray's direction times twice the triangle's area times the
        // cosine of the angle between them, so it goes small for three quite different reasons: the
        // ray running along the triangle -- which is the one worth refusing -- or the triangle being
        // small, or the ray's direction being short, which is what a surface scaled up does to it,
        // since the ray is carried into the surface's own space to be tested.
        //
        // Judged absolutely, a mesh quietly lost its triangles as it grew.  A height field's are a
        // fraction of a unit across to begin with: a 256-square one gives a determinant of 1.5e-5
        // against a threshold of 1e-6, so scaling the terrain by a hundred -- which every terrain
        // wants, being built in a unit cube -- pushed it under and the triangles stopped being hit
        // at all.  It showed as a mountain that went black in patches and then altogether, which
        // reads as a lighting fault rather than as missing geometry.  The same happened at any scale
        // once the image was fine enough: a 1024-square one is under the threshold on its own.
        if (determinant * determinant <=
            _crossSquared * ray.Direction.Dot(ray.Direction) * ParallelTolerance * ParallelTolerance)
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

        // A hit behind the ray's origin is reported, not dropped: a CSG operation needs every
        // crossing of a triangle mesh, in front and behind, to tell inside from outside for a ray
        // that starts within the solid (as a shadow or reflection ray, cast from a surface the
        // mesh rests on, does).  The shading code discards the negative ones itself, so a lone
        // triangle or an open mesh is unaffected.  This mirrors the analytic surfaces.
        intersections.Add(CreateIntersection(t, u, v));
    }

    /// <summary>
    /// This is a helper method for creating an intersection.  It's overridable since
    /// smooth triangles the u/v pair.
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
