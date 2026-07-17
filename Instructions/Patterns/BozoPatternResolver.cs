using RayTracer.General;
using RayTracer.Patterns;

namespace RayTracer.Instructions.Patterns;

/// <summary>
/// This class is used to resolve a bozo pattern value.
/// </summary>
public class BozoPatternResolver : PatternResolver<BozoPattern>
{
    /// <summary>
    /// This property holds the resolver for our pattern's seed property.
    /// </summary>
    public Resolver<int?> SeedResolver { get; init; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a
    /// bozo pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, BozoPattern value)
    {
        SeedResolver.AssignTo(value, target => target.Seed, context, variables);

        base.SetProperties(context, variables, value);
    }
}
