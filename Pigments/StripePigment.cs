using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class provides a pigmentation that returns one of two colors, based on the X
/// component of a point.
/// </summary>
public class StripePigment : Pigment
{
    private readonly Pigment _evenPigment;
    private readonly Pigment _oddPigment;

    public StripePigment(Pigment evenPigment, Pigment oddPigment)
    {
        _evenPigment = evenPigment;
        _oddPigment = oddPigment;
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
            ? _evenPigment.GetColorFor(point)
            : _oddPigment.GetColorFor(point);
    }

    /// <summary>
    /// This method returns whether the given pigmentation matches this one.
    /// </summary>
    /// <param name="other">The pigmentation to compare to.</param>
    /// <returns><c>true</c>, if the two pigmentations match, or <c>false</c>, if not.</returns>
    public override bool Matches(Pigment other)
    {
        return other is StripePigment pigmentation &&
               _evenPigment.Matches(pigmentation._evenPigment) &&
               _oddPigment.Matches(pigmentation._oddPigment);
    }
}
