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
    /// This property holds the resolver for the transparency property of the material.
    /// </summary>
    public Resolver<double> TransparencyResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the index of refraction property of the material.
    /// </summary>
    public Resolver<double> IndexOfRefractionResolver { get; set; }

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
        
        AmbientResolver.AssignTo(value, target => target.Ambient, context, variables);
        DiffuseResolver.AssignTo(value, target => target.Diffuse, context, variables);
        SpecularResolver.AssignTo(value, target => target.Specular, context, variables);
        ShininessResolver.AssignTo(value, target => target.Shininess, context, variables);
        ReflectiveResolver.AssignTo(value, target => target.Reflective, context, variables);
        TransparencyResolver.AssignTo(value, target => target.Transparency, context, variables);
        IndexOfRefractionResolver.AssignTo(value, target => target.IndexOfRefraction, context, variables);
    }

    /// <summary>
    /// This method creates a copy of this resolver.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public object Clone()
    {
        return MemberwiseClone();
    }
}
