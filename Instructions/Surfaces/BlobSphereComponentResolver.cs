using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a blob sphere component value.
/// </summary>
public class BlobSphereComponentResolver : BlobComponentResolver<BlobSphereComponent>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the center property of our sphere component.
    /// </summary>
    public Resolver<Point> CenterResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the radius property of our sphere component.
    /// </summary>
    public Resolver<double> RadiusResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a
    /// sphere component.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetComponentProperties(
        RenderContext context, Variables variables, BlobSphereComponent value)
    {
        CenterResolver.AssignTo(value, target => target.Center, context, variables);
        RadiusResolver.AssignTo(value, target => target.Radius, context, variables);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error message, or
    /// <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        return RadiusResolver is null
            ? "A blob's sphere component needs a radius."
            : null;
    }
}
