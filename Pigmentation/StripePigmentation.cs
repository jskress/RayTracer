using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Pigmentation;

/// <summary>
/// This class provides a pigmentation that returns one of two colors, based on the X
/// component of a point.
/// </summary>
public class StripePigmentation : Pigmentation
{
    private readonly Pigmentation _evenPigmentation;
    private readonly Pigmentation _oddPigmentation;

    public StripePigmentation(Pigmentation evenPigmentation, Pigmentation oddPigmentation)
    {
        _evenPigmentation = evenPigmentation;
        _oddPigmentation = oddPigmentation;
    }

    /// <summary>
    /// This method accepts a point and produces a color for that point.  The color we
    /// return is based on the X component of the given point being even or odd.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    public override Color GetColorFor(Point point)
    {
        return Math.Floor(point.X) % 2 == 0
            ? _evenPigmentation.GetColorFor(point)
            : _oddPigmentation.GetColorFor(point);
    }

    /// <summary>
    /// This method returns whether the given pigmentation matches this one.
    /// </summary>
    /// <param name="other">The pigmentation to compare to.</param>
    /// <returns><c>true</c>, if the two pigmentations match, or <c>false</c>, if not.</returns>
    public override bool Matches(Pigmentation other)
    {
        return other is StripePigmentation pigmentation &&
               _evenPigmentation.Matches(pigmentation._evenPigmentation) &&
               _oddPigmentation.Matches(pigmentation._oddPigmentation);
    }
}
