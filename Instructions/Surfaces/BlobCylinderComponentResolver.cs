using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a blob cylinder component value.
/// </summary>
public class BlobCylinderComponentResolver : BlobComponentResolver<BlobCylinderComponent>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the start property of our cylinder component.
    /// </summary>
    public Resolver<Point> StartResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the end property of our cylinder component.
    /// </summary>
    public Resolver<Point> EndResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the radius property of our cylinder component.
    /// </summary>
    public Resolver<double> RadiusResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a
    /// cylinder component.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetComponentProperties(
        RenderContext context, Variables variables, BlobCylinderComponent value)
    {
        StartResolver.AssignTo(value, target => target.Start, context, variables);
        EndResolver.AssignTo(value, target => target.End, context, variables);
        RadiusResolver.AssignTo(value, target => target.Radius, context, variables);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error message, or
    /// <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (StartResolver is null || EndResolver is null)
            return "A blob's cylinder component needs both a \"from\" and a \"to\" point.";

        return RadiusResolver is null
            ? "A blob's cylinder component needs a radius."
            : null;
    }
}
