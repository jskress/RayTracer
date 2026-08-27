using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a blob plane component value.
/// </summary>
public class BlobPlaneComponentResolver : BlobComponentResolver<BlobPlaneComponent>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the point property of our plane component.
    /// </summary>
    public Resolver<Point> PointResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the normal property of our plane component.
    /// </summary>
    public Resolver<Vector> NormalResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the radius property of our plane component.
    /// </summary>
    public Resolver<double> RadiusResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a plane
    /// component.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetComponentProperties(
        RenderContext context, Variables variables, BlobPlaneComponent value)
    {
        PointResolver.AssignTo(value, target => target.Point, context, variables);
        NormalResolver.AssignTo(value, target => target.Normal, context, variables);
        RadiusResolver.AssignTo(value, target => target.Radius, context, variables);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error message, or
    /// <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (NormalResolver is null)
            return "A blob's plane component needs a normal to say which way it faces.";

        return RadiusResolver is null
            ? "A blob's plane component needs a radius, which is how far its slab of field reaches."
            : null;
    }
}
