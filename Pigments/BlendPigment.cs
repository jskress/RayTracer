using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class provides a pigment that returns a color that is a blend of two others.
/// </summary>
public class BlendPigment : Pigment
{
    private readonly Pigment _firstPigment;
    private readonly Pigment _secondPigment;

    public BlendPigment(Pigment firstPigment, Pigment secondPigment)
    {
        _firstPigment = firstPigment;
        _secondPigment = secondPigment;
    }

    /// <summary>
    /// This method accepts a point and produces a color for that point.  The color we
    /// return is based on a blend of colors from our child pigments at the given point.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    public override Color GetColorFor(Point point)
    {
        Color firstColor = _firstPigment.GetTransformedColorFor(point);
        Color secondColor = _secondPigment.GetTransformedColorFor(point);

        return (firstColor + secondColor) / 2;
    }

    /// <summary>
    /// This method returns whether the given pigmentation matches this one.
    /// </summary>
    /// <param name="other">The pigmentation to compare to.</param>
    /// <returns><c>true</c>, if the two pigmentations match, or <c>false</c>, if not.</returns>
    public override bool Matches(Pigment other)
    {
        return other is BlendPigment pigmentation &&
               _firstPigment.Matches(pigmentation._firstPigment) &&
               _secondPigment.Matches(pigmentation._secondPigment);
    }
}
