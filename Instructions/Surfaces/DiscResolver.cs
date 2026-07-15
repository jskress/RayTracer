using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a disc value.
/// </summary>
public class DiscResolver : SurfaceResolver<Disc>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the center property on a disc.
    /// </summary>
    public Resolver<Point> CenterResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the normal property on a disc.
    /// </summary>
    public Resolver<Vector> NormalResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the radius property on a disc.
    /// </summary>
    public Resolver<double> RadiusResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the inner radius property on a disc.
    /// </summary>
    public Resolver<double> InnerRadiusResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a disc.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Disc value)
    {
        CenterResolver.AssignTo(value, target => target.Center, context, variables);
        NormalResolver.AssignTo(value, target => target.Normal, context, variables);
        RadiusResolver.AssignTo(value, target => target.Radius, context, variables);
        InnerRadiusResolver.AssignTo(value, target => target.InnerRadius, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (CenterResolver is null)
            return "The \"center\" property is required.";
        if (NormalResolver is null)
            return "The \"normal\" property is required.";
        return RadiusResolver is null
            ? "The \"radius\" property is required."
            : null;
    }
}
