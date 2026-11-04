using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Patterns;

namespace RayTracer.Instructions.Patterns;

/// <summary>
/// This class is used to resolve a pattern reading one of a height field's erosion maps.
/// </summary>
public class ErosionPatternResolver : PatternResolver<ErosionPattern>
{
    /// <summary>
    /// This property holds which map the pattern reads.
    /// </summary>
    public ErosionMap Map { get; init; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of the pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, ErosionPattern value)
    {
        value.Map = Map;

        base.SetProperties(context, variables, value);
    }
}
