using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a ribbon value.
/// </summary>
public class RibbonResolver : SurfaceResolver<Ribbon>, IValidatable
{
    /// <summary>
    /// This property holds the resolvers for the ribbon's points, each a width and a place.
    /// </summary>
    public List<(Resolver<double> Width, Resolver<Point> Position)> PointResolvers
    {
        get;
        private set;
    } = [];

    /// <summary>
    /// This property holds the resolver for how far the ribbon turns about its own length.
    /// </summary>
    public Resolver<double> TwistResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a ribbon.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Ribbon value)
    {
        // Added to rather than replacing what is there, and that is not a detail.  A call of a
        // primitive that gives back a ribbon may carry a block of its own -- `object Blade(0.4)
        // { translate ... }` -- and that block is read as a ribbon too, holding the transform and no
        // points at all.  Clearing here would let that empty one wipe the points the primitive just
        // worked out, which is what it did until a patch of grass showed it.
        foreach ((Resolver<double> width, Resolver<Point> position) in PointResolvers)
        {
            value.Points.Add(new RibbonControlPoint
            {
                Width = width.Resolve(context, variables),
                Position = position.Resolve(context, variables)
            });
        }

        TwistResolver.AssignTo(value, target => target.Twist, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method creates a copy of this resolver.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public override object Clone()
    {
        RibbonResolver resolver = (RibbonResolver) base.Clone();

        // Force the list to be physically different, but with the same content.  Without this every
        // copy shares the one list, which a primitive called in a loop notices at once.
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
            ? "A ribbon needs at least two points to run between."
            : null;
    }
}
