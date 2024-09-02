using RayTracer.General;
using RayTracer.Instructions.Transforms;
using RayTracer.Pigments;

namespace RayTracer.Instructions.Pigments;

/// <summary>
/// This class is the base class for all pigment resolvers and is used to resolve a pigment
/// value.
/// </summary>
public abstract class PigmentResolver<TValue> : Resolver<TValue>, IPigmentResolver
    where TValue : Pigment
{
    /// <summary>
    /// This property holds the resolver for our pigment's transform.
    /// </summary>
    public TransformResolver TransformResolver { get; init; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value as a pigment.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public Pigment ResolveToPigment(RenderContext context, Variables variables)
    {
        return Resolve(context, variables);
    }

    /// <summary>
    /// This method is used to execute the resolver into a generic object.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns></returns>
    public object ResolveToObject(RenderContext context, Variables variables)
    {
        return Resolve(context, variables);
    }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a pigment.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected void SetProperties(RenderContext context, Variables variables, TValue value)
    {
        TransformResolver.AssignTo(value, target => target.Transform, context, variables);
    }
}
