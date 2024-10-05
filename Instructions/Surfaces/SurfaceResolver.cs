using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Instructions.Core;
using RayTracer.Instructions.Transforms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a value that is a surface.
/// </summary>
public class SurfaceResolver<TValue> : NamedObjectResolver<TValue>, ISurfaceResolver
    where TValue : Surface, new()
{
    /// <summary>
    /// This property holds the resolver for the material property of the surface.
    /// </summary>
    public Resolver<Material> MaterialResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the no shadow property of the surface.
    /// </summary>
    public Resolver<bool> NoShadowResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the bounding box property of our group.
    /// </summary>
    public BoundingBoxResolver BoundingBoxResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for our surface's transform.
    /// </summary>
    public TransformResolver TransformResolver { get; set; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value as a surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public Surface ResolveToSurface(RenderContext context, Variables variables)
    {
        return Resolve(context, variables);
    }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TValue value)
    {
        MaterialResolver.AssignTo(value, target => target.Material, context, variables);
        NoShadowResolver.AssignTo(value, target => target.NoShadow, context, variables);
        BoundingBoxResolver.AssignTo(value, target => target.BoundingBox, context, variables);
        TransformResolver.AssignTo(value, target => target.Transform, context, variables);

        value.NoShadow |= context.SuppressAllShadows;

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method creates a copy of this resolver.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
