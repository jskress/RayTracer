using RayTracer.General;
using RayTracer.Patterns;

namespace RayTracer.Instructions.Patterns;

/// <summary>
/// This class is used to resolve a pattern of wrinkles.
/// </summary>
public class WrinklesPatternResolver : PatternResolver<WrinklesPattern>
{
    /// <summary>
    /// This property holds the resolver for our pattern's seed property.
    /// </summary>
    public Resolver<int?> SeedResolver { get; init; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties for a
    /// pattern of wrinkles.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, WrinklesPattern value)
    {
        SeedResolver.AssignTo(value, target => target.Seed, context, variables);
    }
}
