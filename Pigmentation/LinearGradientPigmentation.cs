using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Pigmentation;

/// <summary>
/// This class provides a pigmentation that returns a color from a gradient between two
/// colors, based on the X component of a point.
/// </summary>
public class LinearGradientPigmentation : Pigmentation
{
    private readonly Pigmentation _firstPigmentation;
    private readonly Pigmentation _secondPigmentation;

    public LinearGradientPigmentation(Pigmentation firstPigmentation, Pigmentation secondPigmentation)
    {
        _firstPigmentation = firstPigmentation;
        _secondPigmentation = secondPigmentation;
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
        Color firstColor = _firstPigmentation.GetColorFor(point);
        Color secondColor = _secondPigmentation.GetColorFor(point);
        Color difference = secondColor - firstColor;
        double fraction = point.X - Math.Floor(point.X);

        return firstColor + difference * fraction;
    }

    /// <summary>
    /// This method returns whether the given pigmentation matches this one.
    /// </summary>
    /// <param name="other">The pigmentation to compare to.</param>
    /// <returns><c>true</c>, if the two pigmentations match, or <c>false</c>, if not.</returns>
    public override bool Matches(Pigmentation other)
    {
        return other is LinearGradientPigmentation pigmentation &&
               _firstPigmentation.Matches(pigmentation._firstPigmentation) &&
               _secondPigmentation.Matches(pigmentation._secondPigmentation);
    }
}
