using RayTracer.ColorSources;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class represents the material properties for a surface.
/// </summary>
public class Material
{
    /// <summary>
    /// This property holds the source of color for the material.
    /// </summary>
    public ColorSource ColorSource { get; set; } = SolidColorSource.White;

    /// <summary>
    /// The amount of ambient light for the material.
    /// </summary>
    public double Ambient { get; set; } = 0.1;

    /// <summary>
    /// The diffuse factor for the material.
    /// </summary>
    public double Diffuse { get; set; } = 0.9;

    /// <summary>
    /// The specular factor for the material.
    /// </summary>
    public double Specular { get; set; } = 0.9;

    /// <summary>
    /// The Shininess factor for the material.
    /// </summary>
    public double Shininess { get; set; } = 200.0;

    /// <summary>
    /// This property holds how reflective the material is.  Typical range is between 0
    /// and 1.
    /// </summary>
    public double Reflective { get; set; }

    /// <summary>
    /// This property holds the amount of transparency for the material.
    /// </summary>
    public double Transparency { get; set; }

    /// <summary>
    /// This property holds the material's index of refraction.
    /// </summary>
    public double IndexOfRefraction { get; set; } = IndicesOfRefraction.Vacuum;

    /// <summary>
    /// This method returns whether this material matches the given one.  This will be
    /// true if the colors match and all the other properties match as well.
    /// </summary>
    /// <param name="other">The material to compare to.</param>
    /// <returns><c>true</c>if this matrix matches the given one.</returns>
    public bool Matches(Material other)
    {
        return ColorSource.Matches(other.ColorSource) &&
               Ambient.Near(other.Ambient) &&
               Diffuse.Near(other.Diffuse) &&
               Specular.Near(other.Specular) &&
               Shininess.Near(other.Shininess);
    }
}
