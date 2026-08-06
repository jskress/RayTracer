using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Instructions.Patterns;
using RayTracer.Instructions.Transforms;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to resolve the shaping of a medium's density by a pattern: which pattern it is
/// and how it is placed inside whatever the medium fills.
/// </summary>
public class DensityShapeResolver : ObjectResolver<DensityShape>
{
    /// <summary>
    /// This property holds the resolver for the pattern the density is shaped by.
    /// </summary>
    public IPatternResolver PatternResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how the pattern is placed.
    /// </summary>
    public TransformResolver TransformResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a density shape.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(
        RenderContext context, Variables variables, DensityShape value)
    {
        value.Pattern = PatternResolver?.ResolveToPattern(context, variables);

        if (TransformResolver is not null)
            value.Transform = TransformResolver.Resolve(context, variables) ?? Matrix.Identity;
    }
}
