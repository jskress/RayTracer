using RayTracer.Core;
using RayTracer.General;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class is used to resolve a value that is an object and carries a name.
/// </summary>
public class NamedObjectResolver<TValue> : ObjectResolver<TValue>
    where TValue : NamedThing, new()
{
    /// <summary>
    /// This property holds the resolver for our thing's name.
    /// </summary>
    public Resolver<string> NameResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a named
    /// object.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TValue value)
    {
        NameResolver.AssignTo(value, target => target.Name, context, variables);
    }
}
