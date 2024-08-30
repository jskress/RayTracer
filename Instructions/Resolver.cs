using System.Linq.Expressions;
using System.Reflection;
using RayTracer.Extensions;
using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This is the base class for all instructions that produce a value.
/// </summary>
public abstract class Resolver<TValue>
{
    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public abstract TValue Resolve(RenderContext context, Variables variables);
}

/// <summary>
/// Some useful support things for resolvers.  Note that they are done as extensions to
/// make them more naturally <c>null</c>-safe.
/// </summary>
internal static class ResolverExtensions
{
    /// <summary>
    /// This method is used to resolve a value, if the resolve is present, and assign it
    /// to a property
    /// </summary>
    /// <param name="resolver">The resolver to source the value from.</param>
    /// <param name="target">The object that contains the property to set.</param>
    /// <param name="propertyLambda">The lambda that identifies the property.</param>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    internal static void AssignTo<TObject, TValue>(this Resolver<TValue> resolver,
        TObject target,
        Expression<Func<TObject, TValue>> propertyLambda, RenderContext context, Variables variables)
    {
        if (resolver is not null)
        {
            PropertyInfo propertyInfo = propertyLambda.GetPropertyInfo();
            MethodInfo setMethod = propertyInfo.GetSetMethod();

            setMethod!.Invoke(target, [resolver.Resolve(context, variables)]);
        }
    }
}
