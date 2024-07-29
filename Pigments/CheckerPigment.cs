using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class provides a pigmentation that returns a color from a 2x2 checkerboard pattern
/// in all 3 dimensions.
/// </summary>
public class CheckerPigment : Pigment
{
    private readonly Pigment _evenPigment;
    private readonly Pigment _oddPigment;

    public CheckerPigment(Pigment evenPigment, Pigment oddPigment)
    {
        _evenPigment = evenPigment;
        _oddPigment = oddPigment;
    }

    /// <summary>
    /// This method accepts a point and produces a color for that point.  The color we
    /// return is based on The parity of the sum of the point's coordinates.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    public override Color GetColorFor(Point point)
    {
        double sum = Math.Floor(point.X) + Math.Floor(point.Y) + Math.Floor(point.Z);

        return sum % 2 == 0
            ? _evenPigment.GetTransformedColorFor(point)
            : _oddPigment.GetTransformedColorFor(point);
    }

    /// <summary>
    /// This method returns whether the given pigmentation matches this one.
    /// </summary>
    /// <param name="other">The pigmentation to compare to.</param>
    /// <returns><c>true</c>, if the two pigmentations match, or <c>false</c>, if not.</returns>
    public override bool Matches(Pigment other)
    {
        return other is CheckerPigment pigmentation &&
               _evenPigment.Matches(pigmentation._evenPigment) &&
               _oddPigment.Matches(pigmentation._oddPigment);
    }
}
