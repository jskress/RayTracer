using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a parametric surface.
/// </summary>
public class ParametricResolver : SurfaceResolver<Parametric>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the arithmetic giving a point's X.
    /// </summary>
    public ParametricExpressionResolver XResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the arithmetic giving a point's Y.
    /// </summary>
    public ParametricExpressionResolver YResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the arithmetic giving a point's Z.
    /// </summary>
    public ParametricExpressionResolver ZResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the span of the first parameter.
    /// </summary>
    public Resolver<Interval> UDomainResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the span of the second.
    /// </summary>
    public Resolver<Interval> VDomainResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how closely a crossing is to be pinned down.
    /// </summary>
    public Resolver<double> AccuracyResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of the surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Parametric value)
    {
        XResolver.AssignTo(value, target => target.X, context, variables);
        YResolver.AssignTo(value, target => target.Y, context, variables);
        ZResolver.AssignTo(value, target => target.Z, context, variables);
        UDomainResolver.AssignTo(value, target => target.UDomain, context, variables);
        VDomainResolver.AssignTo(value, target => target.VDomain, context, variables);
        AccuracyResolver.AssignTo(value, target => target.Accuracy, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method reports anything missing.  All three coordinates are required, and so are both
    /// spans: a sheet with no <c>u</c> to run along is not a sheet, and defaulting either to
    /// nought-to-one would draw a shape nobody asked for rather than saying what was left out.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (XResolver is null || YResolver is null || ZResolver is null)
            return "A parametric surface needs all three of \"x\", \"y\" and \"z\".";

        return UDomainResolver is null || VDomainResolver is null
            ? "A parametric surface needs both \"u\" and \"v\", which say how far the two parameters run."
            : null;
    }
}
