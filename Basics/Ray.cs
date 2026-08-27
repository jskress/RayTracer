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

    /// <summary>
    /// This property notes how fast this ray widens as it goes, in radians -- the angle one pixel
    /// subtends at the camera.  It is what lets a pattern know how much of a surface a ray is
    /// answerable for, instead of sampling it as though a ray were a line.
    /// <para>
    /// A ray is really a thin cone: it leaves the camera covering one pixel's worth of angle and
    /// covers more and more of the world the further it goes.  A pattern sampled at the single point
    /// where that cone lands, with no idea how wide the cone had grown, is the whole cause of a
    /// distant brick wall coming back as curved bands rather than as brick -- see
    /// <see cref="Footprint"/>.
    /// </para>
    /// <para>
    /// **Nought means "not known", and is the default on purpose.** A ray nobody has told about
    /// spreading -- a shadow ray, a ray a test made by hand -- gets a footprint of nothing, and a
    /// pattern handed a footprint of nothing samples at a point exactly as it always did.  So this
    /// costs nothing and changes nothing until a camera says otherwise.
    /// </para>
    /// </summary>
    public double Spread { get; }

    /// <summary>
    /// This property notes how far light had already come before this ray started, which for a ray
    /// straight out of the camera is nothing.  A cone does not stop widening when it bounces, so a
    /// reflected ray has to go on from the width its parent had reached rather than from nothing.
    /// </summary>
    public double Travelled { get; }

    /// <summary>
    /// This property notes how wide this ray is to begin with, before any spreading.
    /// <para>
    /// It is what an orthographic camera needs and a perspective one does not.  Orthographic rays
    /// run parallel, so a pixel covers the same patch of world however far off it is -- there is no
    /// angle to widen by, only a width it has all along.  A perspective ray is the other way about:
    /// no width at the lens, and all of it from spreading.
    /// </para>
    /// </summary>
    public double BaseRadius { get; }

    public Ray(Point origin, Vector direction, int timeIndex = 0, double spread = 0,
        double travelled = 0, double baseRadius = 0)
    {
        Origin = origin;
        Direction = direction;
        TimeIndex = timeIndex;
        Spread = spread;
        Travelled = travelled;
        BaseRadius = baseRadius;
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
    /// <summary>
    /// This method works out how wide this ray's cone has grown by the time it has gone the given
    /// distance -- the radius of the disc it covers there.
    /// </summary>
    /// <param name="distance">How far along the ray to measure at.</param>
    /// <returns>The radius the cone has reached, or nought if this ray has no spread.</returns>
    public double RadiusAt(double distance)
    {
        return BaseRadius + (Spread <= 0 ? 0 : (Travelled + distance) * Spread * 0.5);
    }

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
