using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;

namespace RayTracer.Core;

/// <summary>
/// This class represents a spotlight: a point light that shines only within a cone, so that it
/// throws a pool of light with a soft edge rather than filling the room.
/// <para>
/// It is a point light in every way but one -- it stands somewhere and its rays fan out from there,
/// so it inherits how far and which way it lies from a point.  What it adds is an aim and a cone
/// about that aim, and a reckoning of how much of the light is pointed at a given place: full down
/// the middle, nothing outside the cone, and a smooth fall between.  The reckoning is POV-Ray's,
/// down to the cubic ease across the rim.
/// </para>
/// </summary>
public class Spotlight : PointLight
{
    /// <summary>
    /// This property notes the point the spotlight is aimed at, which together with its location
    /// sets the axis of the cone.
    /// </summary>
    [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
    public Point PointAt { get; set; } = Point.Zero;

    /// <summary>
    /// This property notes the half-angle, in degrees, of the fully-lit inner cone: out to here
    /// from the axis the light is at full strength, and past here it begins to fall away.  Zero
    /// leaves no flat core at all, so the light is brightest exactly on the axis and softening
    /// everywhere off it.
    /// </summary>
    public double Radius { get; set; }

    /// <summary>
    /// This property notes the half-angle, in degrees, at which the light has fallen to nothing.
    /// Between the radius and here the light eases down; beyond here it is dark.
    /// </summary>
    public double Falloff { get; set; } = 30;

    /// <summary>
    /// This property notes how sharply the light gathers toward the axis within the cone, as an
    /// exponent on the cosine of the angle off it.  Zero spreads the light evenly across the cone;
    /// larger numbers pull it into a tighter, brighter core.  It is POV-Ray's <c>tightness</c>.
    /// </summary>
    public double Tightness { get; set; }

    /// <summary>
    /// This method works out how much of the light is aimed at the given point: how far off the
    /// cone's axis the point lies, turned into a strength between one on the axis and nothing at
    /// the rim.
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <returns>The fraction of the light aimed at the point.</returns>
    public override double IntensityToward(Point point)
    {
        Vector axis = (PointAt - Location).Unit;
        Vector toPoint = (point - Location).Unit;

        // The cosine of the angle between the cone's axis and the way to the point: one when the
        // point is straight down the axis, falling as it swings aside, and negative behind.
        double cosine = toPoint.Dot(axis);

        if (cosine <= 0)
            return 0;

        // Angles are given in degrees but compared as cosines, since that is what the dot product
        // hands back.  A wider angle has a smaller cosine, so the fully-lit core is where the
        // cosine is *above* the radius cosine, and the dark beyond is where it is below the
        // falloff cosine.
        double cosineRadius = Math.Cos(Radius * Math.PI / 180);
        double cosineFalloff = Math.Cos(Falloff * Math.PI / 180);

        double intensity = Tightness == 0 ? 1 : Math.Pow(cosine, Tightness);

        if (cosine < cosineRadius)
            intensity *= CubicEase(cosineFalloff, cosineRadius, cosine);

        return intensity;
    }

    /// <summary>
    /// This method eases smoothly from zero to one across a band, which is what softens the rim of
    /// the cone.  It is POV-Ray's own cubic, <c>(3 - 2t)t²</c>: flat where it meets zero and one,
    /// so the light neither starts nor stops abruptly.
    /// </summary>
    /// <param name="low">Where the ease begins, giving zero.</param>
    /// <param name="high">Where the ease ends, giving one.</param>
    /// <param name="position">The value to ease.</param>
    /// <returns>The eased value, between zero and one.</returns>
    private static double CubicEase(double low, double high, double position)
    {
        if (position < low)
            return 0;

        if (position >= high || high <= low)
            return 1;

        double t = (position - low) / (high - low);

        return (3 - 2 * t) * t * t;
    }
}
