using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class provides a pigment that returns a color from a gradient between two colors,
/// based on the X component of a point.
/// </summary>
public class LinearGradientPigment : GradientPigment
{
    private readonly Pigment _firstPigment;
    private readonly Pigment _secondPigment;

    public LinearGradientPigment(Pigment firstPigment, Pigment secondPigment)
    {
        _firstPigment = firstPigment;
        _secondPigment = secondPigment;
    }

    /// <summary>
    /// This method accepts a point and produces a color for that point.  The color we
    /// return is based on a linearly interpolated (lerp) value between our two colors
    /// based on the X component of the given point.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    public override Color GetColorFor(Point point)
    {
        Color firstColor = _firstPigment.GetTransformedColorFor(point);
        Color secondColor = _secondPigment.GetTransformedColorFor(point);
        double fraction = point.X - Math.Floor(point.X);

        if (Bounces)
        {
            if (Math.Floor(point.X) % 2 == 0)
                (firstColor, secondColor) = (secondColor, firstColor);
        }

        return firstColor + (secondColor - firstColor) * fraction;
    }

    /// <summary>
    /// This method returns whether the given pigmentation matches this one.
    /// </summary>
    /// <param name="other">The pigmentation to compare to.</param>
    /// <returns><c>true</c>, if the two pigmentations match, or <c>false</c>, if not.</returns>
    public override bool Matches(Pigment other)
    {
        return other is LinearGradientPigment pigmentation &&
               _firstPigment.Matches(pigmentation._firstPigment) &&
               _secondPigment.Matches(pigmentation._secondPigment);
    }
}
