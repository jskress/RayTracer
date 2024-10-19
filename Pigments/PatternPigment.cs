using RayTracer.Basics;
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
    /// This method accepts a point and produces a color for that point.  The color we
    /// return is based on a blend of colors from our child pigments at the given point.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    public override Color GetColorFor(Point point)
    {
        double value = Pattern.Evaluate(point);

        return Pattern.DiscretePigmentsNeeded > 0
            ? PigmentSet.GetColorFor(point, (int) value)
            : PigmentSet.GetColorFor(point, value);
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
