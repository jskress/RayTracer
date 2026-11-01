using RayTracer.Basics;
using RayTracer.Patterns;

namespace RayTracer.Core;

/// <summary>
/// This class holds one of a material's numbers running from one value to another by a pattern across its surface:
/// its roughness by a texture set's roughness map, how much of it is bare metal by a metalness map.
/// <para>
/// It is read where a ray meets the surface, as a blend of materials is (see <see cref="Material.MappedAt"/>):
/// where the pattern stands at nought the number is its first value, where it stands at one its second, and between
/// them in proportion.
/// </para>
/// </summary>
public sealed class MaterialMap
{
    /// <summary>
    /// This property holds the value the number takes where the pattern stands at nought.
    /// </summary>
    public double From { get; init; }

    /// <summary>
    /// This property holds the value the number takes where the pattern stands at one.
    /// </summary>
    public double To { get; init; }

    /// <summary>
    /// This property holds the pattern the number runs by.
    /// </summary>
    public Pattern Pattern { get; init; }

    /// <summary>
    /// This property holds where the pattern stands in the surface's space; <c>null</c> for where it is.
    /// </summary>
    public Matrix Transform { get; init; }

    private Matrix _inverse;

    /// <summary>
    /// This method returns how far from its first value to its second the number stands where a ray met the surface.
    /// </summary>
    /// <param name="intersection">The hit.</param>
    /// <param name="scene">The scene being shaded.</param>
    /// <returns>The share, from nought to one.</returns>
    internal double ShareAt(Intersection intersection, Scene scene)
    {
        if (Transform is not null)
            _inverse ??= Transform.Invert();

        return Math.Clamp(MaterialBlend.PatternValueAt(Pattern, _inverse, intersection, scene), 0, 1);
    }

    /// <summary>
    /// This method returns the number at a share of the way from its first value to its second.
    /// </summary>
    /// <param name="share">The share, from nought to one.</param>
    /// <returns>The number there.</returns>
    internal double At(double share) => From + (To - From) * share;
}
