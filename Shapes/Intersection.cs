using RayTracer.Graphics;
using RayTracer.Materials;

namespace RayTracer.Shapes;

/// <summary>
/// This class represents an intersection between a ray and a shape.
/// </summary>
public sealed class Intersection
{
    /// <summary>
    /// This holds the point of intersection.
    /// </summary>
    public Point Point { get; init; }

    /// <summary>
    /// This property holds the surface normal at this point of intersection.
    /// </summary>
    public Vector Normal { get; init; }

    /// <summary>
    /// This holds the distance along the ray where the intersection happened.
    /// </summary>
    public double Distance { get; init; }

    /// <summary>
    /// This property holds a reference to the material involved at the point of
    /// intersection.
    /// </summary>
    public Material Material { get; init; }

    /// <summary>
    /// This property notes whether the ray crosses into the shape at this point
    /// of intersection (<c>true</c>) or crosses out of it (<c>false</c>).
    /// </summary>
    public bool FromOutside { get; init; }

    public Intersection(Ray ray, Point point, Vector normal, double distance, Material material)
    {
        Point = point;
        FromOutside = ray.Direction.Dot(normal) < 0;
        Normal = FromOutside ? normal : -normal;
        Distance = distance;
        Material = material;
    }
}
