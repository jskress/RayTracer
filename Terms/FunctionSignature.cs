using System.Reflection;
using System.Runtime.ExceptionServices;
using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Terms;

/// <summary>
/// This class describes one form of one of the DSL's functions: its name, what it takes and what
/// it gives back.  A name with more than one of these is an overloaded function, and which form a
/// call means is settled by the types of the values it is given.
/// <para>
/// The description is the method itself rather than a hand-written copy of its shape, so the two
/// cannot disagree.  That one <see cref="MethodInfo"/> also serves both ways a function is called:
/// a scene evaluates it through <see cref="Invoke"/>, which happens once per instruction and so can
/// afford reflection, while a compiled field function will build a direct call to the same method
/// and never come through here at all.
/// </para>
/// </summary>
public class FunctionSignature
{
    /// <summary>
    /// This property holds the name a scene calls this form of the function by.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// This property holds the method that implements this form of the function.
    /// </summary>
    public MethodInfo Method { get; }

    /// <summary>
    /// This property holds the types this form of the function takes, in order.
    /// </summary>
    public Type[] ParameterTypes { get; }

    /// <summary>
    /// This property holds the type this form of the function gives back.
    /// </summary>
    public Type ReturnType => Method.ReturnType;

    /// <summary>
    /// This property reports how many values this form of the function takes.
    /// </summary>
    public int ParameterCount => ParameterTypes.Length;

    /// <summary>
    /// This property notes whether the last thing this takes soaks up every value left over -- a
    /// C# <c>params</c> array.  A form written that way takes any number of values from its fixed
    /// ones upward, which is what lets a function like <c>list</c> be written at all.
    /// </summary>
    public bool TakesAnyNumber { get; }

    /// <summary>
    /// This property holds how many values a call must supply at the least.
    /// </summary>
    public int LeastCount => TakesAnyNumber ? ParameterCount - 1 : ParameterCount;

    /// <summary>
    /// This property notes that the function has no place in a field: not that it merely lacks a rule
    /// for its slope, but that asking it about a place in space means nothing.
    /// </summary>
    public bool NotInAField { get; init; }

    internal FunctionSignature(string name, MethodInfo method)
    {
        Name = name;
        Method = method;
        ParameterInfo[] parameters = method.GetParameters();

        ParameterTypes = parameters
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        TakesAnyNumber = parameters.Length > 0 &&
                         parameters[^1].IsDefined(typeof(ParamArrayAttribute), false);
    }

    /// <summary>
    /// This method binds a call to a form whose last parameter soaks up whatever is left.  The fixed
    /// values are matched one for one as they always are; everything after them is gathered into the
    /// array the method wants, each element converted the same way a fixed value would be.
    /// </summary>
    /// <param name="arguments">The values the call supplied.</param>
    /// <param name="exactly">Whether the values must already be of the types wanted.</param>
    /// <param name="bound">The values to hand the method, when this returns <c>true</c>.</param>
    /// <returns><c>true</c>, if the call fits this form.</returns>
    private bool TryBindAnyNumber(object[] arguments, bool exactly, out object[] bound)
    {
        bound = null;

        if (arguments.Length < LeastCount)
            return false;

        object[] values = new object[ParameterCount];

        for (int index = 0; index < LeastCount; index++)
        {
            if (!TryConvert(arguments[index], ParameterTypes[index], exactly, out values[index]))
                return false;
        }

        Type elementType = ParameterTypes[^1].GetElementType()!;
        Array rest = Array.CreateInstance(elementType, arguments.Length - LeastCount);

        for (int index = LeastCount; index < arguments.Length; index++)
        {
            if (!TryConvert(arguments[index], elementType, exactly, out object value))
                return false;

            rest.SetValue(value, index - LeastCount);
        }

        values[^1] = rest;
        bound = values;

        return true;
    }

    /// <summary>
    /// This method converts one value to the type a parameter wants, either as it already is or
    /// through the DSL's conversions.
    /// </summary>
    /// <param name="argument">The value the call supplied.</param>
    /// <param name="parameterType">The type wanted.</param>
    /// <param name="exactly">Whether the value must already be of that type.</param>
    /// <param name="value">The converted value, when this returns <c>true</c>.</param>
    /// <returns><c>true</c>, if the value fits.</returns>
    private static bool TryConvert(
        object argument, Type parameterType, bool exactly, out object value)
    {
        value = null;

        if (argument is not null && parameterType.IsInstanceOfType(argument))
        {
            value = argument;

            return true;
        }

        if (exactly)
            return false;

        (CoercionResult coercion, object converted) = TypeConversions.Coerce(argument, parameterType);

        if (coercion != CoercionResult.OfProperType)
            return false;

        value = converted;

        return true;
    }

    /// <summary>
    /// This method is used to try to fit the given values to this form of the function.  When
    /// <c>exactly</c> is set, a value must already be of the type wanted; otherwise the DSL's own
    /// conversions get a say, so a tuple written in a scene satisfies a vector exactly as it does
    /// everywhere else in the language.
    /// </summary>
    /// <param name="arguments">The values the call supplies.</param>
    /// <param name="exactly">Whether the values must already be of the types wanted.</param>
    /// <param name="bound">The values, converted to the types this form takes, or <c>null</c> if
    /// they do not fit.</param>
    /// <returns><c>true</c>, if the values fit this form of the function.</returns>
    internal bool TryBind(object[] arguments, bool exactly, out object[] bound)
    {
        bound = null;

        if (TakesAnyNumber)
            return TryBindAnyNumber(arguments, exactly, out bound);

        if (arguments.Length != ParameterCount)
            return false;

        object[] values = new object[ParameterCount];

        for (int index = 0; index < ParameterCount; index++)
        {
            Type parameterType = ParameterTypes[index];
            object argument = arguments[index];

            if (argument is not null && parameterType.IsInstanceOfType(argument))
            {
                values[index] = argument;

                continue;
            }

            if (exactly)
                return false;

            (CoercionResult coercion, object value) = TypeConversions.Coerce(argument, parameterType);

            if (coercion != CoercionResult.OfProperType)
                return false;

            values[index] = value;
        }

        bound = values;

        return true;
    }

    /// <summary>
    /// This method is used to call this form of the function with the given values, which must
    /// already be of the types it takes (see <see cref="TryBind"/>).
    /// </summary>
    /// <param name="arguments">The values to call the function with.</param>
    /// <returns>The value the function produced.</returns>
    internal object Invoke(object[] arguments)
    {
        try
        {
            return Method.Invoke(null, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            // Reflection wraps whatever the function threw.  Rethrow the real thing, with its own
            // stack intact, so a fault inside a function reads as itself rather than as a
            // reflection failure.
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();

            throw;
        }
    }

    /// <summary>
    /// This method returns the function in the form a scene would write it, for error messages.
    /// </summary>
    /// <returns>The function's name and the types it takes.</returns>
    public override string ToString()
    {
        return $"{Name}({string.Join(", ", ParameterTypes.Select(DslNameFor))})";
    }

    /// <summary>
    /// This method returns the name the DSL knows a type by, since an error message should name
    /// the types a scene writes rather than the classes that implement them.
    /// </summary>
    /// <param name="type">The type to name.</param>
    /// <returns>The DSL's name for the type.</returns>
    public static string DslNameFor(Type type)
    {
        if (type == typeof(double) || type == typeof(int) || type == typeof(short))
            return "number";

        if (type == typeof(bool))
            return "boolean";

        if (type == typeof(string))
            return "string";

        if (type == typeof(Vector))
            return "vector";

        if (type == typeof(Point))
            return "point";

        if (type == typeof(Color))
            return "color";

        if (type == typeof(Matrix))
            return "matrix";

        return type == typeof(NumberTuple) ? "tuple" : type.Name;
    }
}
