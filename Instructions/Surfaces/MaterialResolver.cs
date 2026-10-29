using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Instructions.Pigments;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a material value.
/// </summary>
public class MaterialResolver : ObjectResolver<Material>, ICloneable, IValidatable
{
    /// <summary>
    /// This property notes that the material is <c>inherited</c>: the surface is to take whatever
    /// material is handed down to it, rather than one of its own.  With decals, it takes that one
    /// with the decals painted on top; see <see cref="Material.InheritsAppearance"/>.
    /// </summary>
    public bool SetToNull { get; init; }

    /// <summary>
    /// This property holds the resolver for the pigment property of the material.
    /// </summary>
    public IPigmentResolver PigmentResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the ambient property of the material.
    /// </summary>
    public Resolver<double> AmbientResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the light the material gives off of its own.
    /// </summary>
    public Resolver<Color> EmissionResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the diffuse property of the material.
    /// </summary>
    public Resolver<double> DiffuseResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the specular property of the material.
    /// </summary>
    public Resolver<double> SpecularResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the shininess property of the material.
    /// </summary>
    public Resolver<double> ShininessResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the reflective property of the material.
    /// </summary>
    public Resolver<double> ReflectiveResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for whether what the material mirrors follows Fresnel.
    /// </summary>
    public Resolver<bool> FresnelResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how rough the material is.
    /// </summary>
    public Resolver<double> RoughnessResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how sharply diffuse lighting falls away.
    /// </summary>
    public Resolver<double> BrillianceResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how much fine speckle is taken out of the lighting.
    /// </summary>
    public Resolver<double> GrainResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the metallic property of the material.
    /// </summary>
    public Resolver<double> MetallicResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the metal the material is, if it is one.
    /// </summary>
    public MetalResolver MetalResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how much of each melanin hair wearing the material holds.
    /// </summary>
    public Resolver<Melanin?> MelaninResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the transparency property of the material.
    /// </summary>
    public Resolver<double> TransparencyResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the interior of the material.
    /// </summary>
    public InteriorResolver InteriorResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how the material's surface is roughened.
    /// </summary>
    public SurfaceNormalResolver SurfaceNormalResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the material's film, if it has one.
    /// </summary>
    public ThinFilmResolver FilmResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how light wanders under the material's surface, if it does.
    /// </summary>
    public SubsurfaceResolver SubsurfaceResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the clear coat over the material's surface, if it has one.
    /// </summary>
    public CoatResolver CoatResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the sheen of cloth on the material's surface, if it has one.
    /// </summary>
    public SheenResolver SheenResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how much the material's surface is brushed.
    /// </summary>
    public Resolver<double> AnisotropyResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for which way a brushed surface's grooves run.
    /// </summary>
    public Resolver<Vector> BrushedAlongResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the axis a brushed surface's grooves run round.
    /// </summary>
    public Resolver<Vector> BrushedAroundResolver { get; set; }

    /// <summary>
    /// This property holds the resolvers for the decals painted over the pigment, in the order they
    /// were written.
    /// </summary>
    public List<DecalResolver> DecalResolvers { get; private set; } = [];

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override Material Resolve(RenderContext context, Variables variables)
    {
        if (!SetToNull)
            return base.Resolve(context, variables);

        // Inherited and no more is no material at all, so the one above is handed down as ever.
        // Inherited with decals is a material of decals alone, which the one above is handed down
        // under rather than in place of.
        return DecalResolvers.Count == 0
            ? null
            : new Material
            {
                InheritsAppearance = true,
                Decals = DecalResolvers.Select(resolver => resolver.Resolve(context, variables)).ToList()
            };
    }

    /// <summary>
    /// This method makes sure an inherited material says nothing it would not use.
    /// <para>
    /// Everything about an inherited material but its decals comes from the one handed down to it, so
    /// a pigment or a finish written in one would be quietly thrown away.  That is refused rather
    /// than allowed to mislead.
    /// </para>
    /// </summary>
    /// <returns>The text of an error message, or <c>null</c>, if all is well.</returns>
    public string Validate()
    {
        bool saysMore = PigmentResolver is not null || AmbientResolver is not null || EmissionResolver is not null ||
                        DiffuseResolver is not null || SpecularResolver is not null ||
                        ShininessResolver is not null || ReflectiveResolver is not null ||
                        FresnelResolver is not null || RoughnessResolver is not null ||
                        BrillianceResolver is not null || GrainResolver is not null ||
                        MetallicResolver is not null || MetalResolver is not null || TransparencyResolver is not null ||
                        MelaninResolver is not null || FilmResolver is not null || SubsurfaceResolver is not null ||
                        CoatResolver is not null || AnisotropyResolver is not null || SheenResolver is not null ||
                        InteriorResolver is not null || SurfaceNormalResolver is not null;

        if (SetToNull && saysMore)
        {
            return "An inherited material takes everything but its decals from the material handed down " +
                   "to it, so only decals may be written in one.";
        }

        if (MetalResolver is not null && TransparencyResolver is not null)
            return "A metal swallows whatever light gets into it, so a material may not be a metal and let " +
                   "light through.";

        if (FilmResolver is not null && CoatResolver is not null)
            return "A film and a coat are each a skin over the surface, so a material may have one or the other.";

        if (SheenResolver is not null && (FilmResolver is not null || CoatResolver is not null))
            return "A sheen is cloth's own skin, so a material with one may have no film or coat as well.";

        return FilmResolver is not null && RoughnessResolver is not null
            ? "A film's colors come of its being smooth, so a material with a film may not also be rough."
            : null;
    }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a material.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Material value)
    {
        if (PigmentResolver != null)
            value.Pigment = PigmentResolver.ResolveToPigment(context, variables);
        
        // Left alone when the scene said nothing, so that the scene as a whole may settle it later.
        if (AmbientResolver is not null)
            value.Ambient = AmbientResolver.Resolve(context, variables);
        EmissionResolver.AssignTo(value, target => target.Emission, context, variables);
        DiffuseResolver.AssignTo(value, target => target.Diffuse, context, variables);
        SpecularResolver.AssignTo(value, target => target.Specular, context, variables);
        ShininessResolver.AssignTo(value, target => target.Shininess, context, variables);
        ReflectiveResolver.AssignTo(value, target => target.Reflective, context, variables);
        FresnelResolver.AssignTo(value, target => target.Fresnel, context, variables);
        RoughnessResolver.AssignTo(value, target => target.Roughness, context, variables);
        AnisotropyResolver.AssignTo(value, target => target.Anisotropy, context, variables);

        // A surface is brushed one way or round an axis, so saying one is saying it is not the other.
        if (BrushedAlongResolver is not null)
        {
            value.BrushedAlong = BrushedAlongResolver.Resolve(context, variables);
            value.BrushedAround = null;
        }

        if (BrushedAroundResolver is not null)
        {
            value.BrushedAround = BrushedAroundResolver.Resolve(context, variables);
            value.BrushedAlong = null;
        }

        value.ReflectionSamples = context.ReflectionSamples;
        BrillianceResolver.AssignTo(value, target => target.Brilliance, context, variables);
        GrainResolver.AssignTo(value, target => target.Grain, context, variables);
        MetallicResolver.AssignTo(value, target => target.Metallic, context, variables);

        // A metal is bare metal all over, whatever metallic says; see Material.Metal.
        if (MetalResolver is not null)
        {
            value.Metal = MetalResolver.Resolve(context, variables);
            value.Metallic = 1;
        }
        MelaninResolver.AssignTo(value, target => target.Melanin, context, variables);
        TransparencyResolver.AssignTo(value, target => target.Transparency, context, variables);

        // A rough surface's highlight is what it mirrors, so one that named no reflectance would show
        // none at all.  An opaque one gets what nearly everything that is not a metal gives back
        // square-on -- paint, varnish, plastic -- so that `roughness` alone makes a satin finish.
        // Rough glass works out what it mirrors from its own index, facet by facet, so it is left to
        // give back all of that.
        if (RoughnessResolver is not null && ReflectiveResolver is null && value.IsRough)
            value.Reflective = value.Transparency > 0 || value.PigmentMayTransmit ? 1 : 0.04;
        if (InteriorResolver != null)
            value.Interior = InteriorResolver.Resolve(context, variables);

        if (SurfaceNormalResolver != null)
            value.SurfaceNormal = SurfaceNormalResolver.Resolve(context, variables);

        if (FilmResolver != null)
            value.Film = FilmResolver.Resolve(context, variables);

        if (SubsurfaceResolver != null)
            value.Subsurface = SubsurfaceResolver.Resolve(context, variables);

        if (CoatResolver != null)
            value.Coat = CoatResolver.Resolve(context, variables);

        if (SheenResolver != null)
            value.Sheen = SheenResolver.Resolve(context, variables);

        // Decals go on top of any the material already has rather than in place of them, the same as
        // writing a second one in the same block would, which is what a block laid over a material
        // made elsewhere wants.  A new list is made every time rather than the old one added to,
        // since the old one may well be another material's.
        if (DecalResolvers.Count > 0)
        {
            value.Decals =
            [
                ..value.Decals ?? [],
                ..DecalResolvers.Select(resolver => resolver.Resolve(context, variables))
            ];
        }
    }

    /// <summary>
    /// This method creates a copy of this resolver.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public object Clone()
    {
        MaterialResolver resolver = (MaterialResolver) MemberwiseClone();

        // A material named and then added to is cloned first and has the addition parsed into the
        // copy, so a list shared with the original would carry the copy's decals back into it.
        resolver.DecalResolvers = [..DecalResolvers];

        // And so with its interior, which an addition's `interior { ... }` is parsed into: shared, the copy's
        // index or filter became every user's of the name.
        resolver.InteriorResolver = (InteriorResolver) InteriorResolver?.Clone();

        return resolver;
    }
}
