using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Patterns;

namespace RayTracer.Instructions.Patterns;

/// <summary>
/// This class is used to resolve a crevices pattern: how far about a place to look.
/// </summary>
public class CrevicesPatternResolver : PatternResolver<CrevicesPattern>
{
    /// <summary>
    /// This property holds the resolver for how far about the place to look.
    /// </summary>
    public Resolver<double> RadiusResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of the pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, CrevicesPattern value)
    {
        RadiusResolver.AssignTo(value, target => target.Radius, context, variables);

        base.SetProperties(context, variables, value);
    }
}

/// <summary>
/// This class is used to resolve an edges pattern: how far into the solid to look.
/// </summary>
public class EdgesPatternResolver : PatternResolver<EdgesPattern>
{
    /// <summary>
    /// This property holds the resolver for how far into the solid to look.
    /// </summary>
    public Resolver<double> RadiusResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of the pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, EdgesPattern value)
    {
        RadiusResolver.AssignTo(value, target => target.Radius, context, variables);

        base.SetProperties(context, variables, value);
    }
}

/// <summary>
/// This class is used to resolve a facing pattern: which way, in the world, a surface is asked to face.
/// </summary>
public class FacingPatternResolver : PatternResolver<FacingPattern>
{
    /// <summary>
    /// This property holds the resolver for the direction a surface facing gives the pattern its most.
    /// </summary>
    public Resolver<Vector> TowardResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of the pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, FacingPattern value)
    {
        TowardResolver.AssignTo(value, target => target.Toward, context, variables);

        base.SetProperties(context, variables, value);
    }
}
