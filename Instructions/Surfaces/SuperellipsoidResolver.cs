using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a superellipsoid value.
/// </summary>
public class SuperellipsoidResolver : SurfaceResolver<Superellipsoid>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the east/west exponent property of our
    /// superellipsoid.
    /// </summary>
    public Resolver<double> EastResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the north/south exponent property of our
    /// superellipsoid.
    /// </summary>
    public Resolver<double> NorthResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a
    /// superellipsoid.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Superellipsoid value)
    {
        EastResolver.AssignTo(value, target => target.EastWest, context, variables);
        NorthResolver.AssignTo(value, target => target.NorthSouth, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        return EastResolver is null || NorthResolver is null
            ? "The \"east\" and \"north\" properties are required."
            : null;
    }
}
