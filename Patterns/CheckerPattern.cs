using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the checker pattern.
/// </summary>
public class CheckerPattern : Pattern
{
    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports.  In
    /// this case, the <see cref="Evaluate"/> method will return the index of the pigment
    /// to use.  If this is zero, then this pattern will return a number in the [0, 1]
    /// interval.
    /// </summary>
    public override int DiscretePigmentsNeeded => 2;

    /// <summary>
    /// This method is used to determine an appropriate value, typically between 0 and 1,
    /// for the given point.
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        double sum = Math.Floor(point.X) + Math.Floor(point.Y) + Math.Floor(point.Z);

        return sum % 2 == 0 ? 0 : 1;
    }
}
