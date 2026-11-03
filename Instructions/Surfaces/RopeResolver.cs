using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a laid rope along a spline.
/// </summary>
public class RopeResolver : SurfaceResolver<Rope>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the spline the rope runs along.
    /// </summary>
    public Resolver<Spline> SplineResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how thick the rope is.
    /// </summary>
    public Resolver<double> RadiusResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many strands it is laid from.
    /// </summary>
    public Resolver<int> StrandsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far along it a strand goes in one turn.
    /// </summary>
    public Resolver<double> LayResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a rope.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Rope value)
    {
        // Only a resolver that says something replaces what is there, so the block on a call of a primitive
        // giving back a rope -- read as another rope, holding a transform -- leaves the primitive's be.
        if (SplineResolver is not null)
            value.Spline = SplineResolver.Resolve(context, variables);

        RadiusResolver.AssignTo(value, target => target.Radius, context, variables);
        StrandsResolver.AssignTo(value, target => target.Strands, context, variables);

        if (LayResolver is not null)
            value.Lay = LayResolver.Resolve(context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method makes sure the rope says what it runs along.
    /// </summary>
    /// <returns>The text of an error message, or <c>null</c>, if all is well.</returns>
    public string Validate() => SplineResolver is null ? "A rope needs a spline to run along." : null;
}
