using RayTracer.Extensions;

namespace RayTracer.Basics;

/// <summary>
/// This class represents a ray in the ray tracer.
/// </summary>
public class Ray
{
    /// <summary>
    /// This is the origin point of the ray.
    /// </summary>
    public Point Origin { get; }

    /// <summary>
    /// This vector notes the direction of the ray.
    /// </summary>
    public Vector Direction { get; }

    public Ray(Point origin, Vector direction)
    {
        Origin = origin;
        Direction = direction;
    }

    /// <summary>
    /// This method returns a point at some distance along the ray.
    /// </summary>
    /// <param name="distance"></param>
    /// <returns></returns>
    public Point At(double distance)
    {
        return Origin + Direction * distance;
    }

    /// <summary>
    /// This method is used to determine whether the given point lies on the ray.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c>, if the point is on the ray, or <c>false</c>, if not.</returns>
    public bool Contains(Point point)
    {
        Vector vector = point - Origin;
        double t = vector.Dot(Direction);

        if (t < 0)
            return false;
        
        double distance = (At(t) - point).Magnitude;

        return distance < 0.15;
    }
}
