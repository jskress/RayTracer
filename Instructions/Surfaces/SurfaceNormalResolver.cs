using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Instructions.Patterns;
using RayTracer.Instructions.Transforms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve the roughening of a surface: the pattern whose slope tilts the
/// normal, how far it may tilt it, and how the pattern is placed.
/// </summary>
public class SurfaceNormalResolver : ObjectResolver<SurfaceNormal>, ICloneable
{
    /// <summary>
    /// This property holds the resolver for the pattern the surface is roughened by.
    /// </summary>
    public IPatternResolver PatternResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how deep the roughening reads.
    /// </summary>
    public Resolver<double> DepthResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how the pattern is placed over the surface.
    /// </summary>
    public TransformResolver TransformResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the seed the pattern's noise should use, where it uses
    /// any.  It is named as the pigment names its own, so that a scene may make a roughening
    /// repeat from one render to the next.
    /// </summary>
    public Resolver<int?> SeedResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a surface
    /// normal.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(
        RenderContext context, Variables variables, SurfaceNormal value)
    {
        value.Pattern = PatternResolver?.ResolveToPattern(context, variables);

        DepthResolver.AssignTo(value, target => target.Depth, context, variables);

        if (TransformResolver is not null)
            value.Transform = TransformResolver.Resolve(context, variables) ?? Matrix.Identity;

        if (SeedResolver?.Resolve(context, variables) is { } seed)
            value.SetSeed(seed);
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
