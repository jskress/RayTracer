using System.Diagnostics.CodeAnalysis;
using RayTracer.Extensions;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace RayTracer.Core;

/// <summary>
/// This class represents the material properties for a surface.
/// </summary>
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class Material
{
    /// <summary>
    /// This is the material to use for a surface that has no material of its own and
    /// hasn't inherited one from a parent group or CSG surface either.  A surface's
    /// <c>Material</c> property is deliberately left <c>null</c> until it is explicitly
    /// set, inherited, or finalized for rendering (see <c>RenderInstruction</c>) so that
    /// group/CSG material inheritance can tell "unset" apart from "explicitly set."  Code
    /// that reads material properties directly off a surface without going through that
    /// pipeline (e.g. isolated unit tests) should fall back to this default instead.
    /// </summary>
    public static readonly Material Default = new ();

    /// <summary>
    /// This property holds the source of color for the material.
    /// </summary>
    public Pigment Pigment { get; set; } = SolidPigment.White;

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
    /// This property holds how metallic the material is, between 0 and 1, and governs the colour
    /// of what it reflects -- both its specular highlight and, where it is reflective, the scene
    /// mirrored in it.
    /// <para>
    /// At a dielectric surface -- plastic, glass, paint -- reflection happens before the pigment
    /// absorbs anything, so what bounces off keeps the colour of the light: a white lamp makes a
    /// white highlight on red plastic.  A metal is a conductor, and its reflectance is itself
    /// wavelength-dependent, so what bounces off takes the colour of the metal: the same lamp
    /// makes a gold highlight on gold.  Leaving this at 0 gives the dielectric behaviour; raising
    /// it toward 1 tints reflections with the surface's own colour.  Without it, gold renders as
    /// yellow plastic -- a yellow surface wearing an incongruous white glint.
    /// </para>
    /// </summary>
    public double Metallic { get; set; }

    /// <summary>
    /// This property holds the amount of transparency for the material.
    /// </summary>
    public double Transparency { get; set; }

    /// <summary>
    /// This property holds the material's index of refraction.
    /// </summary>
    public double IndexOfRefraction { get; set; } = IndicesOfRefraction.Vacuum;

    /// <summary>
    /// This method returns the tint that <see cref="Metallic"/> puts on light this material
    /// reflects, to be multiplied into a highlight or a reflected colour.  It interpolates
    /// between white -- leaving the light's own colour alone, as a dielectric would -- and the
    /// surface's colour, which is what a conductor does.
    /// <para>
    /// The interpolation is not flat across the surface: it is weighted by an empirical stand-in
    /// for Fresnel reflectivity, near 0 where the light meets the surface head on and rising to 1
    /// at grazing angles.  So the tint is close to full over most of a surface and falls away at
    /// its silhouette, which is the physical story -- at grazing incidence everything turns into a
    /// colourless mirror, metal included.  Both the curve and its constants are POV-Ray's, from
    /// <c>Trace::ComputeMetallic</c>; they are a fit rather than real Fresnel, as POV's own
    /// comment says.
    /// </para>
    /// </summary>
    /// <param name="surfaceColor">The material's own colour at the point being lit, which must be
    /// the pigment's colour alone and not already multiplied by the light's.</param>
    /// <param name="cosAngle">The cosine of the angle between the surface normal and the light.</param>
    /// <returns>The tint to multiply the reflected colour by.</returns>
    public Color GetMetallicTint(Color surfaceColor, double cosAngle)
    {
        double x = Math.Abs(Math.Acos(Math.Clamp(cosAngle, -1, 1))) / (Math.PI / 2);
        double fresnel = Math.Clamp(
            0.014567225 / ((x - 1.12) * (x - 1.12)) - 0.011612903, 0, 1);
        double weight = Metallic * (1 - fresnel);

        return Colors.White + (surfaceColor - Colors.White) * weight;
    }

    /// <summary>
    /// This method returns whether this material matches the given one.  This will be
    /// true if the colors match and all the other properties match as well.
    /// </summary>
    /// <param name="other">The material to compare to.</param>
    /// <returns><c>true</c>if this matrix matches the given one.</returns>
    public bool Matches(Material other)
    {
        return Pigment.Matches(other.Pigment) &&
               Ambient.Near(other.Ambient) &&
               Diffuse.Near(other.Diffuse) &&
               Specular.Near(other.Specular) &&
               Shininess.Near(other.Shininess);
    }
}
