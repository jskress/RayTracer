using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.ColorSources;

/// <summary>
/// This class provides a color source that returns one of two colors, based on the X
/// component of a point.
/// </summary>
public class StripeColorSource : ColorSource
{
    private readonly ColorSource _evenColorSource;
    private readonly ColorSource _oddColorSource;

    public StripeColorSource(ColorSource evenColorSource, ColorSource oddColorSource)
    {
        _evenColorSource = evenColorSource;
        _oddColorSource = oddColorSource;
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
            ? _evenColorSource.GetColorFor(point)
            : _oddColorSource.GetColorFor(point);
    }

    /// <summary>
    /// This method returns whether the given color source matches this one.
    /// </summary>
    /// <param name="other">The color source to compare to.</param>
    /// <returns><c>true</c>, if the two color sources match, or <c>false</c>, if not.</returns>
    public override bool Matches(ColorSource other)
    {
        return other is StripeColorSource source &&
               _evenColorSource.Matches(source._evenColorSource) &&
               _oddColorSource.Matches(source._oddColorSource);
    }
}
