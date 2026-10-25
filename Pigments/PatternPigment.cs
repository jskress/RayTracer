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
    /// A pattern is only as transmitting as the colors it chooses between.
    /// </summary>
    public override bool MayTransmit => PigmentSet?.MayTransmit ?? false;

    /// <summary>
    /// This method accepts a point and produces a color for that point.  The color we
    /// return is based on a blend of colors from our child pigments at the given point.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
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
    /// proportion, which is what a half-brick-half-mortar patch really looks like.  One that picks
    /// among three or more hands back each pigment's share of the patch, since an index averaged
    /// between the first and the third is the second, which may cover none of it.
    /// </para>
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <param name="footprint">How much of the surface the ray covers there.</param>
    /// <returns>The appropriate color.</returns>
    public override Color GetColorFor(Point point, Footprint footprint)
    {
        if (Pattern.DiscretePigmentsNeeded > 2)
        {
            Span<double> shares = stackalloc double[Pattern.DiscretePigmentsNeeded];

            if (Pattern.SharesOver(point, footprint, shares))
                return PigmentSet.GetMixedColorFor(point, shares, footprint);
        }

        if (Pattern.DiscretePigmentsNeeded == 0 && Pattern.MayWrap && FromItsSamples(point, footprint, default) is { } color)
            return color;

        double value = Pattern.ValueFor(point, footprint);

        return Pattern.DiscretePigmentsNeeded > 0
            ? PigmentSet.GetBlendedColorFor(point, value, footprint)
            : PigmentSet.GetColorFor(point, value, footprint);
    }

    /// <summary>
    /// This method returns the color of a patch of a pattern whose value may wrap -- a gradient's repeat, say -- from
    /// one sampling of it: for a patch that straddles the wrap, the colors its values pick, mixed, and for one that
    /// does not, the color of its average value, as for any pattern; or <c>null</c> for a patch too small to sample.
    /// Averaged across the wrap, the values came to the middle of the range: a seam of a color the patch holds none
    /// of, down every repeat of a gradient seen at a distance.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <param name="footprint">How much of the surface the ray covers there.</param>
    /// <param name="viewing">The directions the surface is being shaded between, if any.</param>
    /// <returns>The color, or <c>null</c>.</returns>
    private Color FromItsSamples(Point point, Footprint footprint, Viewing viewing)
    {
        Span<double> values = stackalloc double[96];
        int count = Pattern.SamplesOver(point, footprint, values, out double value);

        if (count == 0)
            return null;

        double low = values[0], high = values[0];

        for (int index = 1; index < count; index++)
        {
            low = Math.Min(low, values[index]);
            high = Math.Max(high, values[index]);
        }

        // Half the range apart: no smooth stretch of a pattern changes so fast within one patch, and a wrap always does.
        if (high - low <= 0.5)
        {
            return viewing.IsEmpty
                ? PigmentSet.GetColorFor(point, value, footprint)
                : PigmentSet.GetColorFor(point, value, footprint, viewing);
        }

        Color sum = Colors.Black;
        double alpha = 0;

        for (int index = 0; index < count; index++)
        {
            Color color = PigmentSet.GetColorFor(point, values[index], footprint, viewing);

            sum += color;
            alpha += color.Alpha;
        }

        return (sum * (1.0 / count)).WithAlpha(alpha / count);
    }

    /// <summary>
    /// This property reports whether this pigment depends on the directions a surface is seen and lit
    /// from: whether its pattern does, or any of the pigments it maps to.
    /// </summary>
    public override bool SeesTheViewer => Pattern is FlopPattern || (PigmentSet?.SeesTheViewer ?? false);

    /// <summary>
    /// This property reports whether this pigment's pattern, or any pigment it maps to, sees what stands round
    /// the place it is asked about.
    /// </summary>
    public override bool SeesTheSurroundings =>
        Pattern.SeesTheSurroundings || (PigmentSet?.SeesTheSurroundings ?? false);

    /// <summary>
    /// This method produces the color at a point, told the directions the surface is being shaded
    /// between: the pattern is asked with them, and so is whatever it maps to.
    /// </summary>
    /// <param name="point">The point, in the pigment's own space.</param>
    /// <param name="footprint">How much of the surface the ray covers there.</param>
    /// <param name="viewing">The directions to the eye and the light, and the way the surface faces.</param>
    /// <returns>The color there.</returns>
    public override Color GetColorFor(Point point, Footprint footprint, Viewing viewing)
    {
        if (viewing.IsEmpty)
            return base.GetColorFor(point, footprint, viewing);

        if (Pattern.DiscretePigmentsNeeded > 2)
        {
            Span<double> shares = stackalloc double[Pattern.DiscretePigmentsNeeded];

            if (Pattern.SharesOver(point, footprint, shares))
                return PigmentSet.GetMixedColorFor(point, shares, footprint, viewing);
        }

        if (Pattern.DiscretePigmentsNeeded == 0 && Pattern.MayWrap && FromItsSamples(point, footprint, viewing) is { } color)
            return color;

        double value = Pattern.ValueFor(point, footprint, viewing);

        return Pattern.DiscretePigmentsNeeded > 0
            ? PigmentSet.GetBlendedColorFor(point, value, footprint, viewing)
            : PigmentSet.GetColorFor(point, value, footprint, viewing);
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
