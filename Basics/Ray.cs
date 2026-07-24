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

    /// <summary>
    /// This property notes which of the instants the shutter is open for this ray sees the scene
    /// at.  It is an index rather than a time because the instants are settled once for the whole
    /// render, which is what lets a moving surface work out where it stands at each of them ahead
    /// of time rather than afresh for every ray.
    /// <para>
    /// Every ray born of this one -- toward a light, off a mirror, through glass -- must carry the
    /// same index, or a moving thing would be in one place to the eye and another in its own
    /// reflection.
    /// </para>
    /// </summary>
    public int TimeIndex { get; }

    public Ray(Point origin, Vector direction, int timeIndex = 0)
    {
        Origin = origin;
        Direction = direction;
        TimeIndex = timeIndex;
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
