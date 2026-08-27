using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Patterns;

namespace RayTracer.Pigments;

/// <summary>
/// This class provides a pigment that returns a color based on a pattern.
/// </summary>
public class PatternPigment : Pigment
{
    /// <summary>
    /// This property holds the pattern that will drive the pigment.
    /// </summary>
    public Pattern Pattern { get; init; }

    /// <summary>
    /// This property holds the pigment set from which we will source the actual color for
    /// a point.
    /// </summary>
    public PigmentSet PigmentSet { get; init; }

    /// <summary>
    /// This method is used to push any random number generator seeds throughout the pigment
    /// tree.
    /// </summary>
    /// <param name="seed">The seed value to set.</param>
    public override void SetSeed(int seed)
    {
        Pattern.SetSeed(seed);
        PigmentSet.SetSeed(seed);
    }

    /// <summary>
    /// This method passes the chance to get ready along to the pigments we choose between.  A pattern
    /// itself has nothing to prepare; it is the colors it chooses between that may.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="surface">The surface that this pigment is set on.</param>
    protected override void PrepareForRendering(RenderContext context, Surface surface)
    {
        PigmentSet.PrepareForRendering(context, surface);
    }

    /// <summary>
    /// This method accepts a point and produces a color for that point.  The color we
    /// return is based on a blend of colors from our child pigments at the given point.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    /// <summary>
    /// A pattern is only as transmitting as the colors it chooses between.
    /// </summary>
    public override bool MayTransmit => PigmentSet?.MayTransmit ?? false;

    public override Color GetColorFor(Point point)
    {
        return GetColorFor(point, Footprint.None);
    }

    /// <summary>
    /// This method produces the color for a patch of the pattern rather than for a point of it.
    /// <para>
    /// A pattern that hands back a *number* is filtered by the pattern itself and the number is
    /// looked up as usual.  A pattern that picks *which pigment* -- a brick choosing brick or mortar
    /// -- cannot be: an averaged index means nothing, since half way between the first pigment and
    /// the second is not a color, it is a pigment that does not exist.  Such a pattern hands back a
    /// fractional index instead, and the pigment set mixes the two it lies between in that
    /// proportion, which is what a half-brick-half-mortar patch really looks like.
    /// </para>
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <param name="footprint">How much of the surface the ray covers there.</param>
    /// <returns>The appropriate color.</returns>
    public override Color GetColorFor(Point point, Footprint footprint)
    {
        double value = Pattern.ValueFor(point, footprint);

        return Pattern.DiscretePigmentsNeeded > 0
            ? PigmentSet.GetBlendedColorFor(point, value, footprint)
            : PigmentSet.GetColorFor(point, value, footprint);
    }

    /// <summary>
    /// This method returns whether the given pigment matches this one.
    /// </summary>
    /// <param name="other">The pigment to compare to.</param>
    /// <returns><c>true</c>, if the two pigments match, or <c>false</c>, if not.</returns>
    public override bool Matches(Pigment other)
    {
        return other is PatternPigment patternPigment &&
               GetType() == patternPigment.GetType() &&
               Pattern.Matches(patternPigment.Pattern) &&
               PigmentSet.Matches(patternPigment.PigmentSet);
    }
}
