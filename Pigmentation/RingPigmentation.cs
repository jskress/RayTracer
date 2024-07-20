using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Pigmentation;

/// <summary>
/// This class provides a pigmentation that returns a color from alternating, concentric
/// rings based on a point's position in the X/Z plane.
/// </summary>
public class RingPigmentation : Pigmentation
{
    private readonly Pigmentation _evenPigmentation;
    private readonly Pigmentation _oddPigmentation;

    public RingPigmentation(Pigmentation evenPigmentation, Pigmentation oddPigmentation)
    {
        _evenPigmentation = evenPigmentation;
        _oddPigmentation = oddPigmentation;
    }

    /// <summary>
    /// This method accepts a point and produces a color for that point.  The color we
    /// return is based on the parity of the given point's location in the XZ plane.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    public override Color GetColorFor(Point point)
    {
        return Math.Floor(Math.Sqrt(point.X * point.X + point.Z * point.Z)) % 2 == 0
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
        return other is RingPigmentation pigmentation &&
               _evenPigmentation.Matches(pigmentation._evenPigmentation) &&
               _oddPigmentation.Matches(pigmentation._oddPigmentation);
    }
}
