using RayTracer.Extensions;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the gradient pattern.  See the <see cref="BandType"/> enum
/// for the list of supported gradient types.
/// </summary>
public class GradientPattern : BandPattern
{
    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports.  In
    /// this case, the <c>Evaluate</c> method will return the index of the pigment to use.
    /// If this is zero, then this pattern will return a number in the [0, 1] interval.
    /// </summary>
    public override int DiscretePigmentsNeeded => 0;

    /// <summary>
    /// This property notes whether the gradient value bounces.
    /// </summary>
    public bool Bouncing { get; set; }

    /// <summary>
    /// This method is used to adjust the value determined by the band type.
    /// </summary>
    /// <param name="value">The value to adjust.</param>
    /// <returns>The adjusted value.</returns>
    protected override double Adjust(double value)
    {
        bool isOdd = Math.Floor(value) % 2 != 0;

        value = InvertedClip(value.Fraction());

        if (Bouncing && isOdd)
            value = 1 - value;

        return value;
    }

    /// <summary>
    /// This method is used to decide whether our details match those of the given pattern.
    ///
    /// Subclasses must call this class's <c>DetailsMatch</c> method.
    /// </summary>
    /// <param name="pattern">The pattern to match against.</param>
    /// <returns><c>true</c>, if the given pattern's details match this one, or <c>false</c>,
    /// if not.</returns>
    protected override bool DetailsMatch(Pattern pattern)
    {
        GradientPattern other = (GradientPattern) pattern;

        return base.DetailsMatch(pattern) && Bouncing == other.Bouncing;
    }
}
