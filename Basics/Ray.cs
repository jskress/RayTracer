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
}
