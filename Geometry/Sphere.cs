using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a sphere.  By definition, it is a unit sphere located at the
/// origin.
/// </summary>
public class Sphere : Surface
{
    /// <summary>
    /// This method returns the box a sphere sits in, which is the unit cube about the origin: a
    /// sphere is always the unit sphere here, and whatever size and place a scene gives it is carried
    /// by its transform rather than by the shape.
    /// </summary>
    /// <returns>The box this sphere sits in.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        return new BoundingBox()
            .Add(new Point(-1, -1, -1))
            .Add(new Point(1, 1, 1));
    }

    /// <summary>
    /// This method returns the box a sphere lies within, marked as holding the ball touching its faces: the
    /// ball itself is the tightest thing round a sphere, where the ball round its box fills three times as much of
    /// the view.
    /// </summary>
    /// <returns>The box.</returns>
    public override IEnumerable<Casing> Casings() =>
        [new Casing(new Point(-1, -1, -1), new Vector(2, 0, 0), new Vector(0, 2, 0), new Vector(0, 0, 2), true)];

    /// <summary>
    /// This method is used to determine whether the given ray intersects the sphere and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        Vector sphereToRay = ray.Origin - Point.Zero;
        double a = ray.Direction.Dot(ray.Direction);
        double b = 2 * ray.Direction.Dot(sphereToRay);
        double c = sphereToRay.Dot(sphereToRay) - 1;
        double discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
            return;

        discriminant = Math.Sqrt(discriminant);
        a *= 2;
        b = -b;

        double t1 = (b - discriminant) / a;
        double t2 = (b + discriminant) / a;

        if (t1 > t2)
            (t1, t2) = (t2, t1);

        intersections.Add(new Intersection(this, t1));
        intersections.Add(new Intersection(this, t2));
    }

    /// <summary>
    /// This method calculates the normal for the sphere at the specified point.  It is
    /// assumed that the point will have been transformed to surface-space coordinates.
    /// The vector returned will also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        return point - Point.Zero;
    }
}
