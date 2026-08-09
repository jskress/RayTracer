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
    /// This property notes that the function has no place in a field: not that it merely lacks a rule
    /// for its slope, but that asking it about a place in space means nothing.
    /// </summary>
    public bool NotInAField { get; init; }

    internal FunctionSignature(string name, MethodInfo method)
    {
        Name = name;
        Method = method;
        ParameterTypes = method.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();
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
