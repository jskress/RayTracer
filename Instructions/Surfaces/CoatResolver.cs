using RayTracer.Core;
using RayTracer.General;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a material's clear coat: how rough it is, its index and its tint.
/// </summary>
public class CoatResolver : ObjectResolver<Coat>
{
    /// <summary>
    /// This property holds the resolver for how rough the coat's own surface is.
    /// </summary>
    public Resolver<double> RoughnessResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the coat's index of refraction.
    /// </summary>
    public Resolver<double> IndexOfRefractionResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the color the coat gives what lies beneath it.
    /// </summary>
    public Resolver<Graphics.Color> TintResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a coat.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The coat to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Coat value)
    {
        RoughnessResolver.AssignTo(value, target => target.Roughness, context, variables);
        IndexOfRefractionResolver.AssignTo(value, target => target.IndexOfRefraction, context, variables);
        TintResolver.AssignTo(value, target => target.Tint, context, variables);
    }
}
