using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class provides a pigment that always returns the same color.
/// </summary>
public class SolidPigment : Pigment
{
    /// <summary>
    /// Some useful constants.
    /// </summary>
    public static readonly SolidPigment White = new (Colors.White);
    public static readonly SolidPigment Black = new (Colors.Black);

    private readonly Color _color;

    public SolidPigment(Color color)
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
    /// This method returns whether the given pigmentation matches this one.
    /// </summary>
    /// <param name="other">The pigmentation to compare to.</param>
    /// <returns><c>true</c>, if the two pigmentations match, or <c>false</c>, if not.</returns>
    public override bool Matches(Pigment other)
    {
        return other is SolidPigment pigmentation &&
               _color.Matches(pigmentation._color);
    }
}
