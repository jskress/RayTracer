using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a span: the names of the two places a surface is stretched between.
/// </summary>
public class SpanResolver : Resolver<Span>
{
    /// <summary>
    /// This property holds the term for the name of the place the surface's origin stands at.
    /// </summary>
    public Term FromTerm { get; init; }

    /// <summary>
    /// This property holds the term for the name of the place its +X runs toward.
    /// </summary>
    public Term ToTerm { get; init; }

    /// <summary>
    /// This property notes whether its X is stretched by the distance between them.
    /// </summary>
    public bool Stretched { get; init; }

    public override Span Resolve(RenderContext context, Variables variables) =>
        new (FromTerm.GetValue<string>(variables), ToTerm.GetValue<string>(variables), Stretched);
}
