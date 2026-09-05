using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a quaternion Julia set.
/// <para>
/// It needs no <c>bounded by</c>: every set of this form lies inside a ball of radius two, so the
/// shape knows its own extent.
/// </para>
/// </summary>
public class JuliaFractalResolver : SurfaceResolver<JuliaFractal>
{
    /// <summary>
    /// This property holds the resolver for the quaternion the iteration adds each time round.  It
    /// is written as four numbers, which is what a tuple of four already is here.
    /// </summary>
    public Resolver<NumberTuple> CResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many times the iteration is applied.
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
    protected override void SetProperties(
        RenderContext context, Variables variables, JuliaFractal value)
    {
        if (CResolver is not null)
        {
            NumberTuple c = CResolver.Resolve(context, variables);

            // A tuple of three leaves the fourth as not-a-number rather than nought, which would
            // carry straight into the iteration and turn the whole shape into nothing.
            value.C = [c.X, c.Y, c.Z, double.IsNaN(c.W) ? 0 : c.W];
        }

        IterationsResolver.AssignTo(value, target => target.Iterations, context, variables);
        AccuracyResolver.AssignTo(value, target => target.Accuracy, context, variables);

        base.SetProperties(context, variables, value);
    }
}
