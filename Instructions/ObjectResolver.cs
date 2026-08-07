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
    /// This method applies what this resolver says to something already made, rather than to
    /// something new.
    /// <para>
    /// It exists for one case and is worth explaining, since nothing else needs it.  When a scene
    /// calls a primitive of its own and adds a block of its own after the call, two sets of names are
    /// in play at once: the primitive's body belongs to where the primitive was written, and the
    /// block belongs to where the call was written.  A loop placing a row of them will say
    /// <c>translate X step</c> in that block, and <c>step</c> is the caller's.  So the body is made
    /// first, in its own scope, and then the block is laid over it in the caller's -- which cannot be
    /// done by making a second thing and merging them, and can be done exactly by this.
    /// </para>
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The thing already made.</param>
    public void ApplyTo(RenderContext context, Variables variables, TValue value)
    {
        SetProperties(context, variables, value);
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
