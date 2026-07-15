using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a spline value.
/// </summary>
public class SplineResolver : ObjectResolver<Spline>
{
    /// <summary>
    /// This property holds the list of commands to apply to a spline when creating it.
    /// </summary>
    public List<SplineCommand> SplineCommands { get; } = [];

    /// <summary>
    /// This property holds the resolver for whether to suppress the tangent-continuity
    /// check on this spline.
    /// </summary>
    public Resolver<bool> DiscontinuousResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a
    /// spline.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Spline value)
    {
        foreach (SplineCommand command in SplineCommands)
            command.Apply(variables, value);

        DiscontinuousResolver.AssignTo(value, target => target.Discontinuous, context, variables);
    }
}
