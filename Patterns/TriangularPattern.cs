using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the triangular pattern.
/// </summary>
public class TriangularPattern : Pattern
{
    private const double Sqrt3 = 1.7320508075688772935274463415059;
    private const double Sqrt3Over2 = 0.86602540378443864676372317075294;

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
        double x = point.X - 3 * Math.Floor(point.X / 3);
        double z = point.Z - Sqrt3 * Math.Floor(point.Z / Sqrt3);
        int a = (int) Math.Floor(x);

        x = x.Fraction();

        if (z >= Sqrt3Over2)
            z = Sqrt3 - z;

        double k = 1 - x;
        double answer = 0;

        if (!x.Near(0) && !k.Near(0))
        {
            double slope1 = z / x;
            double slope2 = z / k;

            answer = ((slope1 < Sqrt3 ? 1 : 0) + (slope2 < Sqrt3 ? 2 : 0)) switch
            {
                3 => 0,
                2 => 1,
                1 => 3,
                _ => answer
            };
        }
        else
            answer = 1;

        answer = double.IsOddInteger(answer)
            ? (answer + 2.0 * a) % 6
            : (6.0 + answer - 2.0 * a) % 6.0;

        if (z >= Sqrt3Over2)
            answer = 5.0 - answer;

        return answer;
    }
}
