using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Geometry;
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
    /// <summary>
    /// How lit the surface is regardless of any light reaching it.  It is <c>null</c> until the scene is
    /// whole, because what it ought to default to depends on the company it keeps: a scene with no sky
    /// light gets the tenth it has always had, standing in for the bounced light this renderer does not
    /// trace, while a scene that has one gets nothing, the sky being the real thing that fudge was
    /// imitating.  A material naming its own keeps it either way, which is the point of the distinction:
    /// without it there would be no telling "said nothing" from "said a tenth".
    /// </summary>
    public double? Ambient { get; set; }

    /// <summary>
    /// This property holds the ambient actually to be used.  Where nothing has settled it, it is the
    /// tenth it has always been -- so a material built in code and never shown to a scene behaves
    /// exactly as it did before any of this, and only a scene that has a sky light to put in ambient's
    /// place ever settles it to nothing.
    /// </summary>
    public double EffectiveAmbient => Ambient ?? 0.1;

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
    /// This property notes whether the material reflects more of its surroundings the more steeply
    /// it is seen at a slant, as every real surface does.
    /// <para>
    /// Left alone, a surface mirrors <see cref="Reflective"/> of what it faces at every angle.  No
    /// real one does: varnish, paint, wet asphalt and still water all give back a few percent of the
    /// light meeting them square-on and very nearly all of it at a graze, which is why a polished
    /// floor is dark at your feet and a mirror toward the far wall, and why a road shines toward
    /// the horizon.  With this set, <see cref="Reflective"/> is how much is mirrored square-on and
    /// the share rises from there, by Schlick's approximation to Fresnel's equations, the same one a
    /// transparent surface already follows.
    /// </para>
    /// <para>
    /// What a surface mirrors never reaches its pigment, so the light it shows of its own is turned
    /// down by as much: at a graze such a surface stops being a colored thing with a reflection on
    /// it and becomes a mirror.  The highlights are left alone.  A surface that lets light through
    /// already shares light this way between what it mirrors and what it lets by, by its index of
    /// refraction, and so is not affected; nor is one that reflects nothing.
    /// </para>
    /// </summary>
    public bool Fresnel { get; set; }

    /// <summary>
    /// This property holds how rough the surface is, from nothing -- a mirror -- to one, which is
    /// matte.  It is the scale every physically based renderer uses, so a value read off a chart of
    /// materials means here what it means there: polished wood is about 0.2, satin paint 0.4.
    /// <para>
    /// **A rough surface is one finish rather than two.**  A highlight is only the reflection of a
    /// lamp, and a rough surface blurs both the same way, so its highlight and what it mirrors come
    /// from one spread of microscopic facets (GGX's, by Trowbridge and Reitz) rather than from
    /// <see cref="Specular"/> and <see cref="Shininess"/>, which it does not use.  How much it gives
    /// back is <see cref="Reflective"/>, square-on, rising toward a graze as <see cref="Fresnel"/>
    /// describes, which every rough surface follows whether it says so or not.
    /// </para>
    /// <para>
    /// Nothing is rough unless it says so, and nothing that is not rough is touched.
    /// </para>
    /// </summary>
    public double Roughness { get; set; }

    /// <summary>
    /// This property reports whether the surface is rough at all, and so shades by its facets rather
    /// than by Phong's highlight and a perfect mirror.
    /// </summary>
    public bool IsRough => Roughness > 0;

    /// <summary>
    /// This property holds how many directions a rough surface looks in to see what it mirrors, the
    /// first time a ray meets one.  It is the scene's <c>reflection samples</c> unless set otherwise.
    /// </summary>
    public int ReflectionSamples { get; set; } = 16;

    /// <summary>
    /// This method returns how much of what a surface faces it mirrors when seen at a given angle.
    /// It is <see cref="Reflective"/> square-on and, where <see cref="Fresnel"/> applies, climbs to
    /// all of it at a graze.
    /// </summary>
    /// <param name="cosAngle">The cosine of the angle between the eye and the surface normal.</param>
    /// <returns>The share mirrored, between 0 and 1.</returns>
    public double ReflectanceAt(double cosAngle)
    {
        if (!FresnelApplies)
            return Reflective;

        double slant = 1 - Math.Clamp(cosAngle, 0, 1);

        return Reflective + (1 - Reflective) * Math.Pow(slant, 5);
    }

    /// <summary>
    /// This property reports whether <see cref="Fresnel"/> has anything to do here: the surface must
    /// ask for it, or be rough, which always follows it; it must mirror something; and it must not let
    /// light through, which has a Fresnel of its own.
    /// </summary>
    public bool FresnelApplies =>
        (Fresnel || IsRough) && Reflective > 0 && Transparency == 0 && !PigmentMayTransmit;

    /// <summary>
    /// This property holds how metallic the material is, between 0 and 1, and governs the color
    /// of what it reflects -- both its specular highlight and, where it is reflective, the scene
    /// mirrored in it.
    /// <para>
    /// At a dielectric surface -- plastic, glass, paint -- reflection happens before the pigment
    /// absorbs anything, so what bounces off keeps the color of the light: a white lamp makes a
    /// white highlight on red plastic.  A metal is a conductor, and its reflectance is itself
    /// wavelength-dependent, so what bounces off takes the color of the metal: the same lamp
    /// makes a gold highlight on gold.  Leaving this at 0 gives the dielectric behavior; raising
    /// it toward 1 tints reflections with the surface's own color.  Without it, gold renders as
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
    /// blotches, because it is built from coherent noise; this varies from point to neighboring
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
    /// far it colors the light passing through it.  See <see cref="Core.Interior"/>.
    /// </summary>
    public Interior Interior { get; set; } = new ();

    /// <summary>
    /// This property holds how the surface's skin is roughened, if it is: a pattern whose slope
    /// tilts the normal from point to point, so that a wall reads as stucco without gaining a
    /// single triangle.  See <see cref="Core.SurfaceNormal"/>.
    /// <para>
    /// It is kept apart from <see cref="Pigment"/>, as POV-Ray keeps its own two apart, because
    /// they are rarely the same field: a marble's veins and the roughness of its surface have
    /// nothing to do with one another, and a scene wants to scale and turn each without disturbing
    /// the other.
    /// </para>
    /// </summary>
    public SurfaceNormal SurfaceNormal { get; set; }

    /// <summary>
    /// This property holds the surface this material was written on: its <b>anchor</b>.
    /// <para>
    /// A group or a combination hands its material down to each of its parts that has none of its
    /// own, and it hands down the very same object, so one material may be worn by a dozen surfaces
    /// that each stand in a space of their own.  A pattern is read in the space of whichever part the
    /// ray met, which is what a brick on a gable wants.  A marking does not: one written on a whole
    /// saucer has to land in one place on it, not somewhere different on the lathe, the egg and the
    /// bridge, and at twice the size on whichever of them was scaled.  The anchor is the one space
    /// every part wearing the material agrees on.
    /// </para>
    /// <para>
    /// It is set where a material is written onto a surface, and never by the handing down: every
    /// surface a material clause dresses gets a material of its own, so each material has exactly
    /// one surface it was written on.  It is left empty for a material nothing wrote, such as the
    /// one a surface with none is given at the last moment, and such a material is read in the
    /// space of whichever surface wears it.
    /// </para>
    /// </summary>
    public Surface Anchor { get; set; }

    /// <summary>
    /// This method carries a point from the world into this material's anchor space, for a point
    /// found on the given surface.  See <see cref="Anchor"/>.
    /// </summary>
    /// <param name="surface">The surface the point was found on, which wears this material.</param>
    /// <param name="point">The point, in the world's coordinate system.</param>
    /// <param name="portal">The instance the point was found through, if it was.</param>
    /// <returns>The point, in the space of the surface this material was written on.</returns>
    public Point WorldToAnchor(Surface surface, Point point, Surface portal = null)
    {
        return ToAnchor(Anchor ?? surface, surface, point, portal);
    }

    /// <summary>
    /// This method carries a point from the world into the space of a given anchor, for a point found
    /// on the given surface.
    /// </summary>
    private static Point ToAnchor(Surface anchor, Surface surface, Point point, Surface portal)
    {
        return anchor.WorldToSurface(point, 0, PortalTo(anchor, surface, portal));
    }

    /// <summary>
    /// This method carries a footprint from the world into this material's anchor space, the same
    /// way <see cref="WorldToAnchor(Surface, Point, Surface)"/> carries a point.
    /// </summary>
    /// <param name="surface">The surface the footprint was found on, which wears this material.</param>
    /// <param name="footprint">The footprint, in the world's coordinate system.</param>
    /// <param name="portal">The instance it was found through, if it was.</param>
    /// <returns>The footprint, in the space of the surface this material was written on.</returns>
    public Footprint WorldToAnchor(Surface surface, Footprint footprint, Surface portal = null)
    {
        return ToAnchor(Anchor ?? surface, surface, footprint, portal);
    }

    /// <summary>
    /// The same, for a footprint.
    /// </summary>
    private static Footprint ToAnchor(Surface anchor, Surface surface, Footprint footprint, Surface portal)
    {
        return anchor.WorldToSurface(footprint, 0, PortalTo(anchor, surface, portal));
    }

    /// <summary>
    /// This method decides whether the walk into the anchor's space goes through the instance a
    /// point was found through.
    /// <para>
    /// **Only when the anchor stands inside the shared shape.**  A shared shape has no parent, so the
    /// walk from any surface inside it runs out at its top and carries on through the instance, which
    /// is what places it.  An anchor above the instance -- a group holding it, whose material was
    /// handed down into the shape -- stands in the scene's own chain and needs no instance to place
    /// it; carried through one anyway, the point would be moved by the instance's placing a second
    /// time.  The anchor is inside the shape exactly when it is the surface itself or one of the
    /// surfaces above it before the chain runs out.
    /// </para>
    /// </summary>
    /// <param name="anchor">The surface the material was written on.</param>
    /// <param name="surface">The surface the point was found on.</param>
    /// <param name="portal">The instance the point was found through, if any.</param>
    /// <returns>The instance to walk through, or <c>null</c>.</returns>
    private static Surface PortalTo(Surface anchor, Surface surface, Surface portal)
    {
        if (portal is null)
            return null;

        for (Surface walk = surface; walk is not null; walk = walk.Parent)
        {
            if (ReferenceEquals(walk, anchor))
                return portal;
        }

        return null;
    }

    /// <summary>
    /// This property holds the decals painted over the pigment, in the order they were written, each
    /// going on top of those before it.  It is <c>null</c> for a material with none, which is nearly
    /// every material, so that asking for a color costs nothing more than it ever did.
    /// </summary>
    public List<Decal> Decals { get; set; }

    /// <summary>
    /// This property notes that the material is only decals, to be painted over whatever material
    /// is handed down to the surface wearing it -- what <c>material inherited { decal { .. } }</c>
    /// makes.
    /// <para>
    /// A group hands its material down to every part with none of its own, which is how one
    /// material dresses a whole assembly, and how a scene repaints a library's model by writing a
    /// material on the object.  A part with a material of its own is passed over -- so markings
    /// written in a part's material would stop the scene repainting that part.  A material that
    /// inherits is not passed over: the part is given the material from above with these decals
    /// added on top, and a model can carry its markings and still be repainted.  See
    /// <see cref="HandedDown"/>.  Nothing else in it is ever used.
    /// </para>
    /// </summary>
    public bool InheritsAppearance { get; init; }

    /// <summary>
    /// This method returns the material a surface ends up with when a material is handed down to it.
    /// <para>
    /// A surface with no material takes the one handed down, the very same object, as it always has.
    /// A surface whose material inherits takes a copy of the one handed down with its own decals
    /// added on top, and each decal keeps the surface it was written on, so the two sets of markings
    /// land each in its own place.  Every surface handed the same inheriting material gets the same
    /// copy, kept in <paramref name="merged"/> for the length of one handing down, so that the parts
    /// of an assembly still share one material as they would have.  Any other material is kept.
    /// </para>
    /// <para>
    /// The material handed down may itself inherit, when it belongs to a group within a group that
    /// is yet to hand its own down; the copy then inherits too, and is settled in its turn.
    /// </para>
    /// </summary>
    /// <param name="current">The surface's material, or <c>null</c>.</param>
    /// <param name="from">The material being handed down.</param>
    /// <param name="merged">The copies made so far in this handing down, by the material each was
    /// made for.</param>
    /// <returns>The material the surface is to have.</returns>
    public static Material HandedDown(Material current, Material from, Dictionary<Material, Material> merged)
    {
        if (current is null)
            return from;

        if (!current.InheritsAppearance || from is null)
            return current;

        if (merged.TryGetValue(current, out Material result))
            return result;

        // A decal knows the surface it was written on from the moment that surface is made, but one
        // built in code may not; it belongs to the material it came in, so it keeps that one's.
        foreach (Decal decal in current.Decals ?? [])
            decal.Anchor ??= current.Anchor;

        result = (Material) from.MemberwiseClone();
        result.Decals = [..from.Decals ?? [], ..current.Decals ?? []];
        merged[current] = result;

        return result;
    }

    /// <summary>
    /// This method returns the color of this material where a ray met a surface wearing it.  See
    /// <see cref="GetColorFor(Surface, Point, Vector, Footprint, Surface)"/>.
    /// </summary>
    /// <param name="intersection">Where the ray met the surface, already prepared.</param>
    /// <returns>The color there.</returns>
    public Color GetColorFor(Intersection intersection)
    {
        return GetColorFor(
            intersection.Surface, intersection.Point, intersection.Normal, portal: intersection.Portal);
    }

    /// <summary>
    /// This method returns the color of this material at a point on a surface wearing it: the
    /// pigment's color, with any decals painted over it.
    /// <para>
    /// Everything that wants a surface's color asks here rather than of the pigment directly, so
    /// that lighting, shadows, reflection and refraction all see the same thing -- a decal on a
    /// window darkens the light through it as well as the window.  The pigment is read in the space
    /// of the surface the ray met, as it always has been; the decals are read in the anchor's.  See
    /// <see cref="Anchor"/>.
    /// </para>
    /// <para>
    /// The normal is for the decals alone, which fade out where the surface turns away from the
    /// way they are carried onto it.  A caller with none to give gets decals that do not fade.
    /// </para>
    /// </summary>
    /// <param name="surface">The surface the point was found on, which wears this material.</param>
    /// <param name="point">The point, in the world's coordinate system.</param>
    /// <param name="normal">The surface's normal there, in the world's coordinate system, or
    /// <c>null</c>.  Which way it points does not matter.</param>
    /// <param name="footprint">How much of the surface the ray covers there, so that a pattern or a
    /// decal's edge too fine to resolve is averaged rather than sampled, or <c>null</c>.</param>
    /// <param name="portal">The instance the point was found through, if it was.</param>
    /// <returns>The color there.</returns>
    public Color GetColorFor(
        Surface surface, Point point, Vector normal = null, Footprint footprint = null,
        Surface portal = null)
    {
        Color color = Pigment.GetColorFor(surface, point, footprint, portal);

        if (Decals is null)
            return color;

        // Each decal is read in the space of the surface it was written on.  Nearly always that is the
        // same surface for every decal a material has, so the point is carried there once and kept
        // for as long as the next decal shares it; a material handed down onto one that inherits it
        // holds decals from both, and only then is there a second road to take.
        Surface carriedTo = null;
        Point there = null;
        Footprint patch = null;
        Vector facing = null;

        foreach (Decal decal in Decals)
        {
            Surface anchor = decal.Anchor ?? Anchor ?? surface;

            if (!ReferenceEquals(anchor, carriedTo))
            {
                carriedTo = anchor;
                there = ToAnchor(anchor, surface, point, portal);
                patch = footprint is null || footprint.IsEmpty
                    ? null
                    : ToAnchor(anchor, surface, footprint, portal);
                facing = normal is null ? null : NormalToAnchor(anchor, surface, normal, portal);
            }

            color = decal.LayerOver(color, there, patch, facing);
        }

        return color;
    }

    /// <summary>
    /// This method carries a normal from the world into this material's anchor space.
    /// <para>
    /// A normal does not travel as a direction does: under a squash it tilts the other way.  So
    /// rather than carry it, two directions lying in the surface are carried, by the same road a
    /// footprint takes, and the normal is rebuilt across them on the far side, where it comes out
    /// square to the surface whatever was done to it on the way.  It also means the instance a
    /// point was found through is taken care of exactly as it is for the point.
    /// </para>
    /// </summary>
    /// <param name="anchor">The surface whose space to carry the normal into.</param>
    /// <param name="surface">The surface the normal was found on.</param>
    /// <param name="normal">The normal, in the world's coordinate system.</param>
    /// <param name="portal">The instance it was found through, if it was.</param>
    /// <returns>A normal in the anchor's space, of no particular length.</returns>
    private static Vector NormalToAnchor(Surface anchor, Surface surface, Vector normal, Surface portal)
    {
        Vector unit = normal.Unit;
        Vector aside = Math.Abs(unit.X) < 0.9
            ? unit.Cross(new Vector(1, 0, 0)).Unit
            : unit.Cross(new Vector(0, 1, 0)).Unit;
        Footprint frame = ToAnchor(anchor, surface, new Footprint(aside, unit.Cross(aside)), portal);

        return frame.Across.Cross(frame.Along);
    }

    /// <summary>
    /// This method returns how much light the grain takes away at one particular point, between
    /// nothing and <see cref="Grain"/>.
    /// <para>
    /// The value is hashed from the point rather than drawn from a random number generator, which
    /// is what makes the speckle stay put: the same point gives the same fleck however many rays
    /// find it, from wherever they come.  Hashing the bits of the coordinates rather than
    /// smoothing between them is equally deliberate -- neighboring points must land on unrelated
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

            // Fowler-Noll-Vo, a byte at a time: cheap, and it scatters neighboring inputs across
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
    /// see-through.  A pigment may additionally say, color by color, how much light gets past it,
    /// which is what POV-Ray's fourth color channel does and what lets one pattern be a window in
    /// some places and a wall in others -- a stencil, or the clear panes of a stained-glass design.
    /// </para>
    /// <para>
    /// The two compose as two things blocking light in series: what stops light is the product of
    /// what each lets by.  A pigment that stops nothing therefore leaves the material's own
    /// transparency exactly as it was, which is what keeps every scene written before this
    /// unchanged.
    /// </para>
    /// </summary>
    /// <param name="surfaceColor">The color the pigment gave at the point in question.</param>
    /// <returns>How transparent the material is there, between 0 and 1.</returns>
    public double TransparencyFor(Color surfaceColor)
    {
        return 1 - (1 - Transparency) * surfaceColor.Alpha;
    }

    /// <summary>
    /// This method returns the tint that <see cref="Metallic"/> puts on light this material
    /// reflects, to be multiplied into a highlight or a reflected color.  It interpolates
    /// between white -- leaving the light's own color alone, as a dielectric would -- and the
    /// surface's color, which is what a conductor does.
    /// <para>
    /// The interpolation is not flat across the surface: it is weighted by an empirical stand-in
    /// for Fresnel reflectivity, near 0 where the light meets the surface head on and rising to 1
    /// at grazing angles.  So the tint is close to full over most of a surface and falls away at
    /// its silhouette, which is the physical story -- at grazing incidence everything turns into a
    /// colorless mirror, metal included.  Both the curve and its constants are POV-Ray's, from
    /// <c>Trace::ComputeMetallic</c>; they are a fit rather than real Fresnel, as POV's own
    /// comment says.
    /// </para>
    /// </summary>
    /// <param name="surfaceColor">The material's own color at the point being lit, which must be
    /// the pigment's color alone and not already multiplied by the light's.</param>
    /// <param name="cosAngle">The cosine of the angle between the surface normal and the light.</param>
    /// <returns>The tint to multiply the reflected color by.</returns>
    public Color GetMetallicTint(Color surfaceColor, double cosAngle)
    {
        return GetMetallicTint(RgbSpectrum.From(surfaceColor), cosAngle).ToColor();
    }

    /// <summary>
    /// This method returns the tint a metal gives the light it reflects, band by band, in whatever
    /// bands the render is carrying light in.  See <see cref="GetMetallicTint(Color, double)"/>.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="surfaceColor">The share of each band the material gives back at the point being
    /// lit, which must be the pigment's alone and not already multiplied by the light's.</param>
    /// <param name="cosAngle">The cosine of the angle between the surface normal and the light.</param>
    /// <returns>The tint to multiply the reflected light by.</returns>
    public TS GetMetallicTint<TS>(TS surfaceColor, double cosAngle)
        where TS : struct, ISpectrum<TS>
    {
        double x = Math.Abs(Math.Acos(Math.Clamp(cosAngle, -1, 1))) / (Math.PI / 2);
        double fresnel = Math.Clamp(
            0.014567225 / ((x - 1.12) * (x - 1.12)) - 0.011612903, 0, 1);
        double weight = Metallic * (1 - fresnel);

        return TS.White + (surfaceColor - TS.White) * weight;
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
               EffectiveAmbient.Near(other.EffectiveAmbient) &&
               Diffuse.Near(other.Diffuse) &&
               Specular.Near(other.Specular) &&
               Shininess.Near(other.Shininess);
    }
}
