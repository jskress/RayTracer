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
    /// <para>
    /// **Worked out so that nothing large is taken from anything nearly as large** (Haines et al., "Precision
    /// Improvements for Ray/Sphere Intersection", Ray Tracing Gems, 2019).  The book's discriminant, b^2 - 4ac,
    /// subtracts two numbers each about the square of the ray's distance from the sphere in radii, so its error
    /// grows as that square: seen from the Moon, the Earth's crossings came out a few millionths of a meter
    /// off -- more than the millionth a ray sets off itself by -- and half its shadow rays began inside it, a
    /// speckle all over the planet.  The same quarter discriminant is a(r^2 - l^2), l the ray's nearest
    /// approach to the middle, and l is found directly, with nothing cancelling.  The roots are then taken one
    /// by a sum of like signs and the other from their product, so that the one near nought -- a ray leaving
    /// the sphere's own surface -- is not a difference of near-equal numbers either.
    /// </para>
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        Vector sphereToRay = ray.Origin - Point.Zero;
        double a = ray.Direction.Dot(ray.Direction);
        double halfB = ray.Direction.Dot(sphereToRay);
        double c = sphereToRay.Dot(sphereToRay) - 1;
        Vector nearest = sphereToRay - ray.Direction * (halfB / a);
        double discriminant = a * (1 - nearest.Dot(nearest));

        if (discriminant < 0)
            return;

        double q = -(halfB + Math.CopySign(Math.Sqrt(discriminant), halfB));
        double t1 = q == 0 ? 0 : c / q;
        double t2 = q / a;

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
