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
    public ColorSource ColorSource { get; set; } = ConstantColorSource.White;

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
