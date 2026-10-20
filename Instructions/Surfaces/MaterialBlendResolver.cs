using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Instructions.Patterns;
using RayTracer.Instructions.Transforms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a blend of whole materials by a pattern.  What it resolves to is its first
/// material, carrying the blend; see <see cref="MaterialBlend"/>.
/// </summary>
public class MaterialBlendResolver : MaterialResolver
{
    /// <summary>
    /// This property holds the resolver for the pattern the materials are blended by.
    /// </summary>
    public IPatternResolver PatternResolver { get; init; }

    /// <summary>
    /// This property holds the resolvers for the pattern's values at which each material stands whole, or none
    /// for a pattern that picks among a fixed number of things.
    /// </summary>
    public List<Resolver<double>> BreakResolvers { get; init; } = [];

    /// <summary>
    /// This property holds the resolvers for the materials.
    /// </summary>
    public List<Resolver<Material>> MaterialResolvers { get; init; } = [];

    /// <summary>
    /// This property holds the resolver for where the pattern stands.
    /// </summary>
    public TransformResolver PatternTransformResolver { get; set; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override Material Resolve(RenderContext context, Variables variables)
    {
        Material[] materials = MaterialResolvers
            .Select(resolver => resolver.Resolve(context, variables) ?? Material.Default)
            .ToArray();
        double[] breaks = BreakResolvers.Select(resolver => resolver.Resolve(context, variables)).ToArray();

        for (int index = 1; index < breaks.Length; index++)
        {
            if (breaks[index] < breaks[index - 1])
                throw new Exception("The values a blend's materials stand at must not run backwards.");
        }

        Matrix transform = PatternTransformResolver?.Resolve(context, variables);
        Material blended = materials[0].CopiedForBlending();

        blended.Blend = new MaterialBlend
        {
            Pattern = PatternResolver.ResolveToPattern(context, variables),
            Transform = transform is null || transform.Matches(Matrix.Identity) ? null : transform,
            Breaks = breaks,
            Materials = materials
        };

        return blended;
    }
}
