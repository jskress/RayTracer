using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class provides a pigmentation that returns a color from a gradient between concentric
/// rings based on a point's position in the X/Z plane.
/// </summary>
public class RadialGradientPigment : GradientPigment
{
    private readonly Pigment _firstPigment;
    private readonly Pigment _secondPigment;

    public RadialGradientPigment(Pigment firstPigment, Pigment secondPigment)
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
        double number = Math.Sqrt(point.X * point.X + point.Z * point.Z);
        double fraction = number - Math.Floor(number);
        Color firstColor = _firstPigment.GetTransformedColorFor(point);
        Color secondColor = _secondPigment.GetTransformedColorFor(point);

        if (Bounces)
        {
            if (Math.Floor(number) % 2 == 0)
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
        return other is RadialGradientPigment pigmentation &&
               _firstPigment.Matches(pigmentation._firstPigment) &&
               _secondPigment.Matches(pigmentation._secondPigment);
    }
}
