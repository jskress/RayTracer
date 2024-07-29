using System.Linq.Expressions;
using System.Reflection;
using RayTracer.Extensions;

namespace RayTracer.Instructions;

/// <summary>
/// This is a tagging interface that allows us to care a bit less about generics in some
/// places.
/// </summary>
public interface ITouchesProperty
{
    /// <summary>
    /// This property reports the name of the property we are going to affect.
    /// </summary>
    string PropertyName { get; }
}

/// <summary>
/// This class is the base class for instructions that need to affect the property of an
/// object.
/// </summary>
public abstract class AffectObjectPropertyInstruction<TObject, TValue>
    : ObjectInstruction<TObject>, ITouchesProperty
    where TObject : class
{
    /// <summary>
    /// This property reports the name of the property we are going to affect.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// This property holds the getter for the property we were constructed with.
    /// </summary>
    protected MethodInfo Getter { get; }

    /// <summary>
    /// This property holds the setter for the property we were constructed with.
    /// </summary>
    protected MethodInfo Setter { get; }

    protected AffectObjectPropertyInstruction(Expression<Func<TObject, TValue>> propertyLambda)
    {
        PropertyInfo propertyInfo = propertyLambda.GetPropertyInfo();

        PropertyName = propertyInfo.Name;
        Getter = propertyInfo.GetGetMethod();
        Setter = propertyInfo.GetSetMethod();
    }
}
