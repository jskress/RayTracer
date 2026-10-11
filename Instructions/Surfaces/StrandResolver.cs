using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a strand value.
/// </summary>
public class StrandResolver : SurfaceResolver<Strand>, IValidatable
{
    /// <summary>
    /// This property holds the resolvers for the strand's points, each a width and a place.
    /// </summary>
    public List<(Resolver<double> Width, Resolver<Point> Position)> PointResolvers
    {
        get;
        private set;
    } = [];

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a strand.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Strand value)
    {
        // Added to rather than replacing what is there, for the reason a ribbon's are: a primitive's
        // strand given a block of its own at the call is read as a strand too, holding no points.
        foreach ((Resolver<double> width, Resolver<Point> position) in PointResolvers)
        {
            value.Points.Add(new RibbonControlPoint
            {
                Width = width.Resolve(context, variables),
                Position = position.Resolve(context, variables)
            });
        }

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method creates a copy of this resolver.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public override object Clone()
    {
        StrandResolver resolver = (StrandResolver) base.Clone();

        // Force the list to be physically different, but with the same content; see RibbonResolver.
        resolver.PointResolvers = [..resolver.PointResolvers];

        return resolver;
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        return PointResolvers.Count < 2
            ? "A strand needs at least two points to run between."
            : null;
    }
}
