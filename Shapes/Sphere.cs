using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.Materials;

namespace RayTracer.Shapes;

/// <summary>
/// This class represents a sphere within a scene to be rendered.
/// </summary>
public class Sphere : Shape
{
    /// <summary>
    /// The material of the sphere.
    /// </summary>
    public Material Material { get; }

    private readonly double _radius;
    private readonly double _radiusSquared;

    public Sphere(Point location, double radius, Material material) : base(location)
    {
        _radius = radius;
        _radiusSquared = radius * radius;

        Material = material;
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the sphere.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="interval">The interval of acceptable values for distance.</param>
    /// <returns>A ray/shape intersection object describing the intersection, or
    /// <c>null</c>, if the ray missed us.</returns>
    public override Intersection? FindHit(Ray ray, Interval interval)
    {
        Vector location = ray.Origin - Location;
        double a = ray.Direction.LengthSquared;
        double halfB = location.Dot(ray.Direction);
        double c = location.LengthSquared - _radiusSquared;
        double discriminant = halfB * halfB - a * c;

        if (discriminant < 0)
            return null;

        double discriminantSquareRoot = Math.Sqrt(discriminant);
        double distance = (-halfB - discriminantSquareRoot) / a;

        if (!interval.Surrounds(distance))
            distance = (-halfB + discriminantSquareRoot) / a;

        if (!interval.Surrounds(distance))
            return null;

        Point point = ray.At(distance);
        Vector normal = (point - Location) / _radius;

        return new Intersection(ray, point, normal, distance, Material);
    }
}
