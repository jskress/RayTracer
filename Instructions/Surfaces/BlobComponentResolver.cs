using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Instructions.Pigments;
using RayTracer.Instructions.Transforms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve what every blob component has in common: its strength, a pigment of
/// its own and a transform of its own.
/// </summary>
/// <typeparam name="TComponent">The type of component being resolved.</typeparam>
public abstract class BlobComponentResolver<TComponent> : ObjectResolver<TComponent>
    where TComponent : BlobComponent, new()
{
    /// <summary>
    /// This property holds the resolver for the strength property of our component.
    /// </summary>
    public Resolver<double> StrengthResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the pigment of our component, if it has one.
    /// </summary>
    public IPigmentResolver PigmentResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the transform of our component, if it has one.
    /// </summary>
    public TransformResolver TransformResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a component.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TComponent value)
    {
        StrengthResolver.AssignTo(value, target => target.Strength, context, variables);

        if (PigmentResolver != null)
            value.Pigment = PigmentResolver.ResolveToPigment(context, variables);

        if (TransformResolver != null)
            value.Transform = TransformResolver.Resolve(context, variables);

        SetComponentProperties(context, variables, value);
    }

    /// <summary>
    /// This method must be provided by subclasses to apply the resolvers for whatever the particular
    /// sort of component adds to the common set.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected abstract void SetComponentProperties(
        RenderContext context, Variables variables, TComponent value);
}
