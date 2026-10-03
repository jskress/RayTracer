using RayTracer.Core;
using RayTracer.General;
using RayTracer.Instructions.Pigments;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a material value.
/// </summary>
public class MaterialResolver : ObjectResolver<Material>, ICloneable
{
    /// <summary>
    /// This property notes whether we want to produce a <c>null</c> material or an actual
    /// one.
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
        return SetToNull ? null : base.Resolve(context, variables);
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
        DiffuseResolver.AssignTo(value, target => target.Diffuse, context, variables);
        SpecularResolver.AssignTo(value, target => target.Specular, context, variables);
        ShininessResolver.AssignTo(value, target => target.Shininess, context, variables);
        ReflectiveResolver.AssignTo(value, target => target.Reflective, context, variables);
        BrillianceResolver.AssignTo(value, target => target.Brilliance, context, variables);
        GrainResolver.AssignTo(value, target => target.Grain, context, variables);
        MetallicResolver.AssignTo(value, target => target.Metallic, context, variables);
        TransparencyResolver.AssignTo(value, target => target.Transparency, context, variables);
        if (InteriorResolver != null)
            value.Interior = InteriorResolver.Resolve(context, variables);

        if (SurfaceNormalResolver != null)
            value.SurfaceNormal = SurfaceNormalResolver.Resolve(context, variables);

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

        return resolver;
    }
}
