using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.ColorSources;

/// <summary>
/// This class provides a color source that returns a color from alternating, concentric
/// rings based on a point's position in the X/Z plane.
/// </summary>
public class RingColorSource : ColorSource
{
    private readonly Color _evenColor;
    private readonly Color _oddColor;

    public RingColorSource(Color evenColor, Color oddColor)
    {
        _evenColor = evenColor;
        _oddColor = oddColor;
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
        return Math.Floor(Math.Sqrt(point.X * point.X + point.Z * point.Z)) % 2 == 0
            ? _evenColor
            : _oddColor;
    }

    /// <summary>
    /// This method returns whether the given color source matches this one.
    /// </summary>
    /// <param name="other">The color source to compare to.</param>
    /// <returns><c>true</c>, if the two color sources match, or <c>false</c>, if not.</returns>
    public override bool Matches(ColorSource other)
    {
        return other is RingColorSource source &&
               _evenColor.Matches(source._evenColor) &&
               _oddColor.Matches(source._oddColor);
    }
}
