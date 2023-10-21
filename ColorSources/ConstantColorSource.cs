using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.ColorSources;

/// <summary>
/// This class provides a color source that always returns the same color.
/// </summary>
public class ConstantColorSource : ColorSource
{
    public static readonly ConstantColorSource White = new (Color.White);

    private readonly Color _color;

    public ConstantColorSource(Color color)
    {
        _color = color;
    }

    /// <summary>
    /// This method accepts a point and produces a color for that point.  We always return
    /// the same color, no matter the point.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    public override Color GetColorFor(Point point)
    {
        return _color;
    }

    /// <summary>
    /// This method returns whether the given color source matches this one.
    /// </summary>
    /// <param name="other">The color source to compare to.</param>
    /// <returns><c>true</c>, if the two color sources match, or <c>false</c>, if not.</returns>
    public override bool Matches(ColorSource other)
    {
        return other is ConstantColorSource source &&
               _color.Matches(source._color);
    }
}
