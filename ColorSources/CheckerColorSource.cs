using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.ColorSources;

/// <summary>
/// This class provides a color source that returns a color from a 3 checkerboard pattern
/// in all 3 dimensions.
/// </summary>
public class CheckerColorSource : ColorSource
{
    private readonly ColorSource _evenColorSource;
    private readonly ColorSource _oddColorSource;

    public CheckerColorSource(ColorSource evenColorSource, ColorSource oddColorSource)
    {
        _evenColorSource = evenColorSource;
        _oddColorSource = oddColorSource;
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
        double sum = Math.Floor(point.X) + Math.Floor(point.Y) + Math.Floor(point.Z);

        return sum % 2 == 0
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
        return other is CheckerColorSource source &&
               _evenColorSource.Matches(source._evenColorSource) &&
               _oddColorSource.Matches(source._oddColorSource);
    }
}
