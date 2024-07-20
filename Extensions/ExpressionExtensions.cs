using System.Linq.Expressions;
using System.Reflection;

namespace RayTracer.Extensions;

/// <summary>
/// This class provides some support functionality for expressions.
/// </summary>
public static class ExpressionExtensions
{
    /// <summary>
    /// This method is used to report the property information about the property referenced
    /// in the given lambda expression.
    /// </summary>
    /// <param name="propertyLambda">The lambda expression that refers to a property.</param>
    /// <returns>The information about the referenced property.</returns>
    public static PropertyInfo GetPropertyInfo<TSource, TProperty>(
        this Expression<Func<TSource, TProperty>> propertyLambda)
    {
        Type type = typeof(TSource);

        if (propertyLambda.Body is not MemberExpression member)
            throw new ArgumentException($"Expression '{propertyLambda}' refers to a method, not a property.");

        PropertyInfo propInfo = member.Member as PropertyInfo;
        if (propInfo == null)
            throw new ArgumentException($"Expression '{propertyLambda}' refers to a field, not a property.");

        // This is the type I want - send back the prop info
        if (propInfo.ReflectedType == type || type.IsSubclassOf(propInfo.ReflectedType!))
            return propInfo;

        // It's through an interface or cast
        if (propInfo.ReflectedType.IsAssignableFrom(type))
        {
            // If it can be cast, get the information from the ACTUAL type with the same property name
            PropertyInfo targetPropInfo = type.GetProperty(propInfo.Name);

            if (targetPropInfo != null)
                return targetPropInfo;
        }

        // If I get here there's a type mismatch
        throw new ArgumentException($"Expression '{propertyLambda}' refers to a property that is not from or assignable to type {type}.");
    }
}
