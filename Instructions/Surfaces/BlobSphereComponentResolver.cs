using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a blob sphere component value.
/// </summary>
public class BlobSphereComponentResolver : ObjectResolver<BlobSphereComponent>
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
    /// This property holds the resolver for the strength property of our sphere component.
    /// </summary>
    public Resolver<double> StrengthResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a
    /// sphere component.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, BlobSphereComponent value)
    {
        CenterResolver.AssignTo(value, target => target.Center, context, variables);
        RadiusResolver.AssignTo(value, target => target.Radius, context, variables);
        StrengthResolver.AssignTo(value, target => target.Strength, context, variables);
    }
}
