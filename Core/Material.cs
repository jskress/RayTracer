using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;
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
    /// This property holds how sharply the material's diffuse lighting falls away as a surface
    /// turns from the light.
    /// <para>
    /// At 1 -- the default, and what every surface did before this existed -- brightness falls off
    /// with the cosine of the angle, which is Lambert's law and the right answer for a matte
    /// surface.  Raising it makes the lit face hold its brightness longer and then fall away
    /// abruptly at the edge, which is how a burnished metal reads; POV-Ray's own metal textures run
    /// between 2 and 6.  It is a shaping of the falloff rather than a physical quantity, and it
    /// only ever darkens: at nothing but a graze, every value gives the same nothing.
    /// </para>
    /// </summary>
    public double Brilliance { get; set; } = 1;

    /// <summary>
    /// This property holds how much fine speckle is taken out of the material's diffuse lighting,
    /// giving it the look of sand, unglazed clay or rough concrete.
    /// <para>
    /// This is POV-Ray's <c>crand</c>, and it only ever darkens -- it takes light away in flecks
    /// and never adds any.  Where it differs from POV is what the flecks are keyed on: POV draws
    /// from a random number generator, so the speckle belongs to the ray rather than to the
    /// surface, which makes it crawl when the camera moves and wash out as sampling rises, a
    /// liability POV's own documentation warns about.  Here it is keyed on the point being lit, so
    /// it stays where it is put.
    /// </para>
    /// <para>
    /// It is deliberately not the same thing as a mottled pigment.  That varies smoothly, in
    /// blotches, because it is built from coherent noise; this varies from point to neighbouring
    /// point, which is what makes it read as grain rather than as cloud.
    /// </para>
    /// </summary>
    public double Grain { get; set; }

    /// <summary>
    /// This property holds the amount of transparency for the material.
    /// </summary>
    public double Transparency { get; set; }

    /// <summary>
    /// This property holds the substance inside the surface -- its index of refraction, and how
    /// far it colours the light passing through it.  See <see cref="Core.Interior"/>.
    /// </summary>
    public Interior Interior { get; set; } = new ();

    /// <summary>
    /// This method returns how much light the grain takes away at one particular point, between
    /// nothing and <see cref="Grain"/>.
    /// <para>
    /// The value is hashed from the point rather than drawn from a random number generator, which
    /// is what makes the speckle stay put: the same point gives the same fleck however many rays
    /// find it, from wherever they come.  Hashing the bits of the coordinates rather than
    /// smoothing between them is equally deliberate -- neighbouring points must land on unrelated
    /// values, or the result reads as cloud rather than as grit.
    /// </para>
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <returns>How much to take out of the diffuse light there.</returns>
    public double GrainAt(Point point)
    {
        if (Grain <= 0)
            return 0;

        ulong hash = 14695981039346656037;

        foreach (double coordinate in new[] { point.X, point.Y, point.Z })
        {
            ulong bits = (ulong) BitConverter.DoubleToInt64Bits(coordinate);

            // Fowler-Noll-Vo, a byte at a time: cheap, and it scatters neighbouring inputs across
            // the whole range rather than leaving them near one another, which is the property that
            // matters here.
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (bits >> shift) & 0xFF;
                hash *= 1099511628211;
            }
        }

        // The top bits are the best mixed, so the fraction is taken from those.
        return Grain * ((hash >> 11) / (double) (1UL << 53));
    }

    /// <summary>
    /// This property reports whether this material's pigment might let light through of its own
    /// accord, and so whether it is worth sampling to find out.  See
    /// <see cref="Pigments.Pigment.MayTransmit"/>.
    /// </summary>
    public bool PigmentMayTransmit => Pigment?.MayTransmit ?? false;

    /// <summary>
    /// This method returns how transparent this material is at one particular point, which may
    /// differ from point to point where the pigment says it should.
    /// <para>
    /// <see cref="Transparency"/> is a property of the whole surface: it makes a thing uniformly
    /// see-through.  A pigment may additionally say, colour by colour, how much light gets past it,
    /// which is what POV-Ray's fourth colour channel does and what lets one pattern be a window in
    /// some places and a wall in others -- a stencil, or the clear panes of a stained-glass design.
    /// </para>
    /// <para>
    /// The two compose as two things blocking light in series: what stops light is the product of
    /// what each lets by.  A pigment that stops nothing therefore leaves the material's own
    /// transparency exactly as it was, which is what keeps every scene written before this
    /// unchanged.
    /// </para>
    /// </summary>
    /// <param name="surfaceColor">The colour the pigment gave at the point in question.</param>
    /// <returns>How transparent the material is there, between 0 and 1.</returns>
    public double TransparencyFor(Color surfaceColor)
    {
        return 1 - (1 - Transparency) * surfaceColor.Alpha;
    }

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
