using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class resolves a span written as two ends, such as the range one of a parametric surface's
/// parameters runs over.
/// </summary>
public class IntervalResolver : Resolver<Interval>
{
    /// <summary>
    /// This property holds the term for where the span starts.
    /// </summary>
    public Term StartTerm { get; init; }

    /// <summary>
    /// This property holds the term for where it ends.
    /// </summary>
    public Term EndTerm { get; init; }

    /// <summary>
    /// This method works out the span the two ends describe.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The span.</returns>
    public override Interval Resolve(RenderContext context, Variables variables)
    {
        return new Interval
        {
            Start = StartTerm.GetValue<double>(variables),
            End = EndTerm.GetValue<double>(variables)
        };
    }
}
