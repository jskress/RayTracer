using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to resolve a value that is an object and creatable by the resolver.
/// </summary>
public abstract class ObjectResolver<TValue> : Resolver<TValue>, IObjectResolver
    where TValue : class, new()
{
    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override TValue Resolve(RenderContext context, Variables variables)
    {
        TValue value = new TValue();

        SetProperties(context, variables, value);

        return value;
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
    /// This method should be provided by subclasses to apply their resolvers to the
    /// appropriate properties.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected abstract void SetProperties(RenderContext context, Variables variables, TValue value);
}
