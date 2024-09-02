using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a bounding box value.
/// </summary>
public class BoundingBoxResolver : ObjectResolver<BoundingBox>
{
    /// <summary>
    /// This property holds the resolver for the first point of our bounding box.
    /// </summary>
    public Resolver<Point> FirstPointResolver { get; init; }

    /// <summary>
    /// This property holds the resolver for the second point of our bounding box.
    /// </summary>
    public Resolver<Point> SecondPointResolver { get; init; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a torus.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, BoundingBox value)
    {
        value.Add(FirstPointResolver.Resolve(context, variables));
        value.Add(SecondPointResolver.Resolve(context, variables));
    }
}
