using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a torus value.
/// </summary>
public class TorusResolver : SurfaceResolver<Torus>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the major radius property of our torus.
    /// </summary>
    public Resolver<double> MajorRadiusResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the minor radius property of our torus.
    /// </summary>
    public Resolver<double> MinorRadiusResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a torus.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Torus value)
    {
        MajorRadiusResolver.AssignTo(value, target => target.MajorRadius, context, variables);
        MinorRadiusResolver.AssignTo(value, target => target.MinorRadius, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        return MajorRadiusResolver is null || MinorRadiusResolver is null
            ? "The \"radii\" property is required."
            : null;
    }
}
