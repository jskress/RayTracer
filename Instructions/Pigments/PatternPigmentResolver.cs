using RayTracer.General;
using RayTracer.Instructions.Patterns;
using RayTracer.Pigments;

namespace RayTracer.Instructions.Pigments;

/// <summary>
/// This class is used to resolve a pattern pigment value.
/// </summary>
public class PatternPigmentResolver : PigmentResolver<PatternPigment>
{
    /// <summary>
    /// This property holds the resolver for our pigment's pattern property.
    /// </summary>
    public IPatternResolver PatternResolver { get; init; }

    /// <summary>
    /// This property holds the resolver for our pigment's pigment set property.
    /// </summary>
    public Resolver<PigmentSet> PigmentSetResolver { get; init; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override PatternPigment Resolve(RenderContext context, Variables variables)
    {
        PatternPigment pigment = new ()
        {
            Pattern = PatternResolver.ResolveToPattern(context, variables),
            PigmentSet = PigmentSetResolver.Resolve(context, variables)
        };

        SetProperties(context, variables, pigment);

        return pigment;
    }
}
