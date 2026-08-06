using RayTracer.Basics;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace RayTracer.Core;

/// <summary>
/// This class represents light arriving from every direction at once, as the sky does.
/// <para>
/// Every other light in this renderer is somewhere: a lamp at a point, a panel with a face, a sun in
/// one direction.  This one is nowhere in particular and everywhere at once, and that is what makes it
/// the light of the outdoors.  What it is worth from any given direction is whatever the sky is that
/// way, so it takes a pigment and reads it exactly as the background does -- painted on a sphere of
/// radius one, infinitely far off.  Left to itself it borrows the scene's own background, so that the
/// sky you look at is the sky that lights you.
/// </para>
/// <para>
/// It is also the honest form of the thing <c>ambient</c> has been standing in for.  Ambient is a flat
/// fudge added everywhere, shadow or no shadow, because the light bouncing about a scene was never
/// traced.  This is not that: it arrives from real directions, so a point that can only see a sliver of
/// sky gets a sliver of light, and a niche is darker than an open field for the reason a real one is.
/// That is why a scene with a sky light has no ambient by default.
/// </para>
/// <para>
/// Being spread over directions rather than gathered in a place, it is looked at from many samples like
/// an area light -- and, like an area light, it costs one shadow ray apiece.  Unlike the cloud-lighting
/// of multiple scattering, though, this is the kind case for sampling: the sky is large and smooth, so
/// there is no needle to find and tens of samples settle it rather than thousands.
/// </para>
/// </summary>
public class SkyLight : Light
{
    /// <summary>
    /// This property holds the sky this light carries, read by direction just as a background is.  It
    /// is the scene's own background when the scene names none of its own, which is the ordinary case
    /// and the one worth having: a sky that lights the scene differently from the sky it shows is a
    /// thing to reach for deliberately rather than by accident.
    /// </summary>
    public Pigment Pigment { get; set; }

    /// <summary>
    /// This property holds how many directions the sky is looked at from when a surface is shaded.
    /// </summary>
    public int Samples { get; set; } = 32;

    /// <summary>
    /// This property notes how many places this light is looked at from, which for the sky is however
    /// many directions it was asked to be sampled in.
    /// </summary>
    public override int SampleCount => Samples;

    /// <summary>
    /// This method works out which way the sky lies from a point, which has no one answer: it lies
    /// every way at once.  Straight up stands for it where a single direction must be given, which is
    /// only in the convenience form of shading that takes no sample of its own.
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <returns>Straight up, from infinitely far off.</returns>
    public override (Vector Direction, double Distance) TowardFrom(Point point)
    {
        return (Directions.Up, double.PositiveInfinity);
    }

    /// <summary>
    /// This method picks one of the directions the sky is looked at from.
    /// <para>
    /// Where there is a surface, the directions are spread over the half of the sky it faces, since the
    /// other half is behind it and would be thrown away after being paid for.  They are spread evenly
    /// over that half rather than gathered toward the normal: the shading already weighs each direction
    /// by how squarely it meets the surface, and drawing them toward the normal as well would count
    /// that twice.
    /// </para>
    /// <para>
    /// Where there is no surface -- a place in the middle of a medium, which faces no way at all -- the
    /// directions are spread over the whole sphere instead.
    /// </para>
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <param name="index">Which sample this is.</param>
    /// <param name="normal">The surface normal, or <c>null</c> in the middle of a medium.</param>
    /// <returns>The sample: which way the sky lies, how far off, and all of the light.</returns>
    public override LightSample SampleToward(Point point, int index, Vector normal = null)
    {
        // Two irrationals walked against each other, which spreads any number of samples about as
        // evenly over a surface as they can be spread, and needs nothing remembered between them.  The
        // same pattern is used for every point, which is what keeps a render the same twice over; that
        // it is the same pattern everywhere shows as no artifact, the shading averaging all of it.
        double alongOne = (index + 0.5) / Samples;
        double alongTwo = index * 0.7548776662466927 % 1;

        // What a sample is worth differs between the two cases, and it is the cosine that makes the
        // difference.  A surface weighs every direction by how squarely it meets it, and over the half
        // of the sky it faces that weighing averages a half -- so each of those samples must count
        // double, or a white surface under a white sky would come back half white instead of white.  A
        // place in a medium weighs by the scattering shape instead, which already averages to one over
        // the sphere it was spread across, so those samples count once.
        return normal is null
            ? new LightSample(OverTheWholeSphere(alongOne, alongTwo), double.PositiveInfinity, 1)
            : new LightSample(
                OverTheHalfFacing(normal, alongOne, alongTwo), double.PositiveInfinity, 2);
    }

    /// <summary>
    /// This method returns the color the sky is in the direction of the given sample.
    /// <para>
    /// The light's own color multiplies it rather than replacing it, so that a sky may be dimmed or
    /// tinted as a whole without giving up what it is a picture of.  Left alone it is white, and the
    /// sky is exactly what the pigment says.
    /// </para>
    /// </summary>
    /// <param name="sample">The sample being asked about.</param>
    /// <returns>The color of that piece of sky.</returns>
    public override Color ColorFor(LightSample sample)
    {
        if (Pigment is null)
            return Color;

        Vector heading = sample.Direction;

        return Pigment.GetTransformedColorFor(new Point(heading.X, heading.Y, heading.Z)) * Color;
    }

    /// <summary>
    /// This method spreads a sample evenly over the whole sphere of directions.
    /// </summary>
    /// <param name="alongOne">Where the sample falls, from nothing up to one.</param>
    /// <param name="alongTwo">The way round it falls, from nothing up to one.</param>
    /// <returns>The direction picked.</returns>
    private static Vector OverTheWholeSphere(double alongOne, double alongTwo)
    {
        // Even steps in the cosine are even steps in solid angle, which is what makes this an even
        // spread rather than one crowded at the poles.
        double cosine = 1 - 2 * alongOne;
        double sine = Math.Sqrt(Math.Max(0, 1 - cosine * cosine));
        double around = 2 * Math.PI * alongTwo;

        return new Vector(sine * Math.Cos(around), cosine, sine * Math.Sin(around));
    }

    /// <summary>
    /// This method spreads a sample evenly over the half of the sky a surface faces.
    /// </summary>
    /// <param name="normal">The way the surface faces.</param>
    /// <param name="alongOne">Where the sample falls, from nothing up to one.</param>
    /// <param name="alongTwo">The way round it falls, from nothing up to one.</param>
    /// <returns>The direction picked.</returns>
    private static Vector OverTheHalfFacing(Vector normal, double alongOne, double alongTwo)
    {
        double cosine = alongOne;
        double sine = Math.Sqrt(Math.Max(0, 1 - cosine * cosine));
        double around = 2 * Math.PI * alongTwo;

        // A pair of directions square to the normal and to each other, to swing the sample about it.
        // The one to build from must not be nearly the normal itself, or squaring it against it would
        // be all rounding error.
        Vector aside = Math.Abs(normal.X) < 0.9
            ? new Vector(1, 0, 0)
            : new Vector(0, 1, 0);
        Vector first = normal.Cross(aside).Unit;
        Vector second = normal.Cross(first);

        return (first * (sine * Math.Cos(around)) +
                second * (sine * Math.Sin(around)) +
                normal * cosine).Unit;
    }
}
