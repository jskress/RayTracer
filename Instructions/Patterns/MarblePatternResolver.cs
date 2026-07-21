using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Patterns;

namespace RayTracer.Instructions.Patterns;

/// <summary>
/// This class is used to resolve a marble pattern value.
/// </summary>
public class MarblePatternResolver : PatternResolver<MarblePattern>
{

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a marble
    /// pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, MarblePattern value)
    {
        base.SetProperties(context, variables, value);
    }
}
