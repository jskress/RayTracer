using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to produce a literal, or constant, value.
/// </summary>
public class LiteralResolver<TValue> : Resolver<TValue>
{
    /// <summary>
    /// This property holds the constant value we will always report.
    /// </summary>
    public TValue Value { get; init; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override TValue Resolve(RenderContext context, Variables variables)
    {
        return Value;
    }
}
