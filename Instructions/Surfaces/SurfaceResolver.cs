using RayTracer.Basics;
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
    /// This property holds the resolver for our turbulence's seed property.
    /// </summary>
    public Resolver<int?> SeedResolver { get; set; }

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
    /// This property holds the resolver for how our surface moves while the shutter is open.
    /// </summary>
    public TransformResolver MotionResolver { get; set; }

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
        SeedResolver.AssignTo(value, target => target.Seed, context, variables);
        MaterialResolver.AssignTo(value, target => target.Material, context, variables);
        NoShadowResolver.AssignTo(value, target => target.NoShadow, context, variables);
        BoundingBoxResolver.AssignTo(value, target => target.BoundingBox, context, variables);
        TransformResolver.AssignTo(value, target => target.Transform, context, variables);

        // A motion is handed over as a recipe rather than a matrix, since how far through it to go
        // cannot be known until the camera says how many instants it will look at, which is settled
        // well after this.
        if (MotionResolver is not null)
            value.MotionAt = fraction => MotionResolver.ResolveAt(context, variables, fraction);

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

    /// <summary>
    /// This method lays what this resolver says over a surface already made.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="surface">The surface already made.</param>
    public void ApplyToSurface(RenderContext context, Variables variables, Surface surface)
    {
        if (surface is not TValue value)
            return;

        Matrix already = value.Transform;

        ApplyTo(context, variables, value);

        // What the call said belongs *outside* what the primitive already made.  The body's transform
        // is written in the primitive's own frame and the call's is written in the caller's, so the
        // two compose -- the call's last, since it is the outer one -- rather than the call's simply
        // replacing what the body said.
        //
        // Assigning over it is what used to happen, and it went unnoticed for as long as it did
        // because every primitive written so far hands back a group with no transform of its own,
        // keeping its transforms on the things inside.  The moment one puts a transform on the thing
        // it gives back -- a stone scaled to size, say -- a call that adds `translate` to place it
        // threw the size away.
        if (already is not null && TransformResolver is not null)
            value.Transform = value.Transform * already;
    }
}
