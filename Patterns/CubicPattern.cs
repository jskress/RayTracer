using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the cubic pattern.
/// </summary>
public class CubicPattern : Pattern
{
    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports.  In
    /// this case, the <see cref="Evaluate"/> method will return the index of the pigment
    /// to use.  If this is zero, then this pattern will return a number in the [0, 1]
    /// interval.
    /// </summary>
    public override int DiscretePigmentsNeeded => 6;

    /// <summary>
    /// This method is used to determine an appropriate value, typically between 0 and 1,
    /// for the given point.
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        double x = point.X, y = point.Y, z = point.Z;
        double ax = Math.Abs(x), ay = Math.Abs(y), az = Math.Abs(z);

        if (x >= 0 && x >= ay && x >= az)
            return 0;

        if (y >= 0 && y >= ax && y >= az)
            return 1;

        if (z >= 0 && z >= ax && z >= ay)
            return 2;

        if (x < 0 && x <= -ay && x <= -az)
            return 3;

        if (y < 0 && y <= -ax && y <= -az)
            return 4;

        return 5;
    }
}
