using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a Menger sponge.  It needs no <c>bounded by</c>: it is the cube from -1 to 1.
/// </summary>
public class MengerSpongeResolver : SurfaceResolver<MengerSponge>
{
    /// <summary>
    /// This property holds the resolver for how many times the cube is cut into twenty-seven.
    /// </summary>
    public Resolver<int> IterationsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how closely a crossing is pinned down.
    /// </summary>
    public Resolver<double> AccuracyResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of the fractal.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, MengerSponge value)
    {
        IterationsResolver.AssignTo(value, target => target.Iterations, context, variables);
        AccuracyResolver.AssignTo(value, target => target.Accuracy, context, variables);

        base.SetProperties(context, variables, value);
    }
}
