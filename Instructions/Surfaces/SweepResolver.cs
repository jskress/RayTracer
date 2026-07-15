using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Instructions.Surfaces.Extrusions;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a sweep value.
/// </summary>
public class SweepResolver : SurfaceResolver<Sweep>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the sweep's 2D cross-section.
    /// </summary>
    public GeneralPathResolver ProfileResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the 3D path the profile is lofted along.
    /// </summary>
    public SplineResolver SplineResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the number of tessellation steps to take
    /// across each segment of the spline.
    /// </summary>
    public Resolver<int> StepsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for whether the sweep should stay uncapped even if
    /// its profile is closed.
    /// </summary>
    public Resolver<bool> OpenResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a
    /// sweep.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Sweep value)
    {
        value.Profile = ProfileResolver.Resolve(context, variables);
        value.Spline = SplineResolver.Resolve(context, variables);

        StepsResolver.AssignTo(value, target => target.Steps, context, variables);
        OpenResolver.AssignTo(value, target => target.Open, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (ProfileResolver is null)
            return "A sweep needs a profile.";

        return SplineResolver is null ? "A sweep needs a spline." : null;
    }
}
