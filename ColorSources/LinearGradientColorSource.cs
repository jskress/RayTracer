using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.ColorSources;

/// <summary>
/// This class provides a color source that returns a color from a gradient between two
/// colors, based on the X component of a point.
/// </summary>
public class LinearGradientColorSource : ColorSource
{
    private readonly Color _firstColor;
    private readonly Color _secondColor;
    private readonly Color _difference;

    public LinearGradientColorSource(Color firstColor, Color secondColor)
    {
        _firstColor = firstColor;
        _secondColor = secondColor;
        _difference = secondColor - firstColor;
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
        double fraction = point.X - Math.Floor(point.X);

        return _firstColor + _difference * fraction;
    }

    /// <summary>
    /// This method returns whether the given color source matches this one.
    /// </summary>
    /// <param name="other">The color source to compare to.</param>
    /// <returns><c>true</c>, if the two color sources match, or <c>false</c>, if not.</returns>
    public override bool Matches(ColorSource other)
    {
        return other is LinearGradientColorSource source &&
               _firstColor.Matches(source._firstColor) &&
               _secondColor.Matches(source._secondColor);
    }
}
