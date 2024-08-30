using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the brick pattern.
/// </summary>
public class BrickPattern : Pattern
{
    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports.  In
    /// this case, the <see cref="Evaluate"/> method will return the index of the pigment
    /// to use.  If this is zero, then this pattern will return a number in the [0, 1]
    /// interval.
    /// </summary>
    public override int DiscretePigmentsNeeded => 2;

    /// <summary>
    /// This property specifies the size of the brick.
    /// </summary>
    public Vector BrickSize { get; set; } = new (8, 3, 4.5);

    /// <summary>
    /// This property specifies the size of the mortar.
    /// </summary>
    public double MortarSize { get; set; } = 0.5;

    /// <summary>
    /// This method is used to determine an appropriate value, typically between 0 and 1,
    /// for the given point.
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        double fudgeIt = MortarSize + DoubleExtensions.Epsilon;
        double x = point.X + fudgeIt;
        double y = point.Y + fudgeIt;
        double z = point.Z + fudgeIt;
        double brickWidth = BrickSize.X;
        double brickHeight = BrickSize.Y;
        double brickDepth = BrickSize.Z;
        double mortarWidth = MortarSize / brickWidth;
        double mortarHeight = MortarSize / brickHeight;
        double mortarDepth = MortarSize / brickDepth;

        // 1) Check mortar layers in the X-Z plane (ie: top view)

        double brickY = ClippedFraction(y / brickHeight);

        if (brickY <= mortarHeight)
            return 0;

        brickY = ClippedFraction(y / brickHeight * 0.5);

        // 2) Check ODD mortar layers in the Y-Z plane (ends)

        double brickX = ClippedFraction(x / brickWidth);

        if (brickX <= mortarWidth && brickY <= 0.5)
            return 0;

        // 3) Check EVEN mortar layers in the Y-Z plane (ends)

        brickX = ClippedFraction(x / brickWidth + 0.5);

        if (brickX <= mortarWidth && brickY > 0.5)
            return 0;

        // 4) Check ODD mortar layers in the Y-X plane (facing)

        double brickZ = ClippedFraction(z / brickDepth);

        if (brickZ <= mortarDepth && brickY > 0.5)
            return 0;

        // 5) Check EVEN mortar layers in the X-Y plane (facing)

        brickZ = ClippedFraction(z / brickDepth + 0.5);

        if (brickZ <= mortarDepth && brickY <= 0.5)
            return 0;

        // If we've gotten this far, color me brick.

        return 1;
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
        BrickPattern other = (BrickPattern) pattern;

        return base.DetailsMatch(pattern) &&
               BrickSize == other.BrickSize &&
               MortarSize.Near(other.MortarSize);
    }

    /// <summary>
    /// This is a helper method that takes the fraction of the given number.  If the result
    /// false below 0, it is shifted above it.
    /// </summary>
    /// <param name="number">The number to get the clipped fraction of.</param>
    /// <returns>The clipped fraction of the number.</returns>
    private static double ClippedFraction(double number)
    {
        number = number.Fraction();

        if (number < 0)
            number += 1;

        return number;
    }
}
