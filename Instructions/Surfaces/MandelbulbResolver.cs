using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a Mandelbulb.  It needs no <c>bounded by</c>: the bulb lies within a ball of
/// radius two, whatever its power.
/// </summary>
public class MandelbulbResolver : SurfaceResolver<Mandelbulb>
{
    /// <summary>
    /// This property holds the resolver for the power the point is raised to each round.
    /// </summary>
    public Resolver<double> PowerResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many rounds are run.
    /// </summary>
    public Resolver<int> IterationsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the finest detail the surface shows.
    /// </summary>
    public Resolver<double> DetailResolver { get; set; }

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
    protected override void SetProperties(RenderContext context, Variables variables, Mandelbulb value)
    {
        PowerResolver.AssignTo(value, target => target.Power, context, variables);
        IterationsResolver.AssignTo(value, target => target.Iterations, context, variables);
        DetailResolver.AssignTo(value, target => target.Detail, context, variables);
        AccuracyResolver.AssignTo(value, target => target.Accuracy, context, variables);

        base.SetProperties(context, variables, value);
    }
}
