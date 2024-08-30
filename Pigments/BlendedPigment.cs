using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class provides a pigment that returns a color that is a blend of other colors.
/// </summary>
public class BlendedPigment : Pigment
{
    /// <summary>
    /// This property holds the list of pigments to blend or layer.
    /// </summary>
    public List<Pigment> Pigments { get; set; } = [];

    /// <summary>
    /// This property indicates that we will blend by layering (combining based on alpha)
    /// as opposed to averaging.
    /// </summary>
    public bool Layer { get; set; }

    /// <summary>
    /// This method accepts a point and produces a color for that point.  The color we
    /// return is based on a blend of colors from our child pigments at the given point.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    public override Color GetColorFor(Point point)
    {
        List<Color> colors = Pigments
            .Select(p => p.GetColorFor(point))
            .ToList();

        return Layer ? Colors.Layer(colors) : Colors.Average(colors);
    }

    /// <summary>
    /// This method returns whether the given pigmentation matches this one.
    /// </summary>
    /// <param name="other">The pigmentation to compare to.</param>
    /// <returns><c>true</c>, if the two pigmentations match, or <c>false</c>, if not.</returns>
    public override bool Matches(Pigment other)
    {
        if (other is not BlendedPigment blendedPigment ||
            Pigments.Count != blendedPigment.Pigments.Count ||
            Layer != blendedPigment.Layer)
            return false;

        return !Pigments.Where((pigment, index) => !pigment.Matches(blendedPigment.Pigments[index]))
            .Any();
    }
}
