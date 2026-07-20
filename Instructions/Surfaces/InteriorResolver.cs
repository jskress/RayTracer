using RayTracer.Core;
using RayTracer.General;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve the interior of a surface.
/// </summary>
public class InteriorResolver : ObjectResolver<Interior>, ICloneable
{
    /// <summary>
    /// This property holds the resolver for the index of refraction property of the interior.
    /// </summary>
    public Resolver<double> IndexOfRefractionResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the filter property of the interior.
    /// </summary>
    public Resolver<double> FilterResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the clarity property of the interior.
    /// </summary>
    public Resolver<double> ClarityResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an interior.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Interior value)
    {
        IndexOfRefractionResolver.AssignTo(value, target => target.IndexOfRefraction, context, variables);
        FilterResolver.AssignTo(value, target => target.Filter, context, variables);
        ClarityResolver.AssignTo(value, target => target.Clarity, context, variables);
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
