using RayTracer.Basics;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace RayTracer.Instructions;

/// <summary>
/// This class contains all our supported type conversions.
/// </summary>
internal static class TypeConversions
{
    /// <summary>
    /// This method is used to coerce the given value to the specified target type.
    /// </summary>
    /// <param name="value">The value to coerce.</param>
    /// <param name="targetTypes">The types to try for the coercion.</param>
    /// <returns>A result describing whether the value could be coerced and the value.</returns>
    internal static (CoercionResult, object) Coerce(object value, params Type[] targetTypes)
    {
        // Can't coerce when we don't know the target type, so just use what we've got.
        if (targetTypes == null || targetTypes.Length == 0)
            return (CoercionResult.NotCoerced, value);

        foreach (Type type in targetTypes)
        {
            (CoercionResult, object) coercion = CoerceToType(value, type);

            if (coercion.Item1 == CoercionResult.OfProperType)
                return coercion;
        }

        return (CoercionResult.CouldNotCoerce, value);
    }

    /// <summary>
    /// This method is used to attempt to coerce the given value to the indicated type.
    /// </summary>
    /// <param name="value">The value to coerce.</param>
    /// <param name="targetType">The type to coerce it to.</param>
    /// <returns>A result describing whether the value could be coerced and the value.</returns>
    private static (CoercionResult, object) CoerceToType(object value, Type targetType)
    {
        // Can't coerce null to a value type.
        if (targetType.IsValueType && value == null)
            return (CoercionResult.CouldNotCoerce, null);

        // If the value is already of the right type, we're done.
        if (value == null || value.GetType() == targetType || value.GetType().IsSubclassOf(targetType))
            return (CoercionResult.OfProperType, value);

        // Handle going to some form of tuple.
        if (targetType == typeof(Point) || targetType == typeof(Vector) ||
            targetType == typeof(Color))
            return CoerceTuples(value, targetType);
        
        // Handle going to a pigment.
        if (targetType == typeof(Pigment))
            return CoerceToPigment(value);

        return targetType == typeof(string)
            ? (CoercionResult.OfProperType, value.ToString())
            : (CoercionResult.CouldNotCoerce, value);
    }

    /// <summary>
    /// This method is used to coerce a number tuple into an appropriate target type.
    /// </summary>
    /// <param name="value">The value to coerce.</param>
    /// <param name="targetType">The type to coerce it to.</param>
    /// <returns>A result describing whether the value could be coerced and the value.</returns>
    private static (CoercionResult, object) CoerceTuples(object value, Type targetType)
    {
        if (value is not NumberTuple tuple)
            return (CoercionResult.CouldNotCoerce, value);

        CoercionResult coercionResult = CoercionResult.OfProperType;
        bool hasW = !double.IsNaN(tuple.W);

        if (targetType == typeof(Color))
        {
            value = hasW
                ? new Color(tuple.X, tuple.Y, tuple.Z, tuple.W)
                : new Color(tuple.X, tuple.Y, tuple.Z);
        }
        else if (targetType == typeof(Point))
        {
            value = hasW
                ? new Point(tuple.X, tuple.Y, tuple.Z, tuple.W)
                : new Point(tuple.X, tuple.Y, tuple.Z);
        }
        else if (targetType == typeof(Vector))
        {
            value = hasW
                ? new Vector(tuple.X, tuple.Y, tuple.Z, tuple.W)
                : new Vector(tuple.X, tuple.Y, tuple.Z);
        }
        else
            coercionResult = CoercionResult.CouldNotCoerce;

        return (coercionResult, value);
    }

    /// <summary>
    /// This method is used to coerce a number tuple into a pigment.
    /// </summary>
    /// <param name="value">The value to coerce.</param>
    /// <returns>A result describing whether the value could be coerced and the value.</returns>
    private static (CoercionResult, object) CoerceToPigment(object value)
    {
        CoercionResult result = CoercionResult.OfProperType;

        if (value is NumberTuple numberTuple)
            (result, value) = CoerceTuples(value, typeof(Color));

        if (value.GetType() == typeof(Color))
            value = new SolidPigment((Color)value);
        else
            result = CoercionResult.CouldNotCoerce;

        return (result, value);
    }
}
