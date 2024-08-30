using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the square pattern.
/// </summary>
public class SquarePattern : Pattern
{
    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports.  In
    /// this case, the <see cref="Evaluate"/> method will return the index of the pigment
    /// to use.  If this is zero, then this pattern will return a number in the [0, 1]
    /// interval.
    /// </summary>
    public override int DiscretePigmentsNeeded => 4;

    /// <summary>
    /// This method is used to determine an appropriate value, typically between 0 and 1,
    /// for the given point.
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        bool xIsOdd = (int) Math.Floor(point.X) % 2 != 0;
        bool zIsOdd = (int) Math.Floor(point.Z) % 2 != 0;

        if (xIsOdd)
            return zIsOdd ? 2 : 3;

        return zIsOdd ? 1 : 0;
    }
}
