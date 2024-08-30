namespace RayTracer.Patterns;

/// <summary>
/// This class provides the striped pattern.  See the <see cref="BandType"/> enum
/// for the list of supported stripe types.
/// </summary>
public class StripedPattern : BandPattern
{
    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports.  In
    /// this case, the <c>Evaluate</c> method will return the index of the pigment to use.
    /// If this is zero, then this pattern will return a number in the [0, 1] interval.
    /// </summary>
    public override int DiscretePigmentsNeeded => 2;

    /// <summary>
    /// This method is used to adjust the value determined by the band type.
    /// </summary>
    /// <param name="value">The value to adjust.</param>
    /// <returns>The adjusted value.</returns>
    protected override double Adjust(double value)
    {
        return Math.Floor(value) % 2 == 0 ? 0 : 1;
    }
}
