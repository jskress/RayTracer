using Lex.Expressions;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Fields;
using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class is the base class for all types of terms.
/// </summary>
public abstract class Term : IExpressionTerm
{
    /// <summary>
    /// This property exposes the token to report for this term when throwing token
    /// exceptions.
    /// </summary>
    public Token ErrorToken { get; }

    protected Term(Token errorToken)
    {
        ErrorToken = errorToken;
    }

    /// <summary>
    /// This method is used to get the current value of this term.  If a target type is
    /// provided, and the term's value is not of that type, a conversion will be attempted.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected types of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    public object GetValue(Variables variables, params Type[] targetTypes)
    {
        (CoercionResult coercionResult, object value) =
            TypeConversions.Coerce(Evaluate(variables, targetTypes), targetTypes);
        string message = coercionResult switch
        {
            CoercionResult.CouldNotCoerce => $"Could not convert {value} to any of the types, " +
                                             $"{string.Join(", ", targetTypes.Select(type => type.Name))}.",
            CoercionResult.NotCoerced => null,
            CoercionResult.OfProperType => null,
            _ => throw new Exception($"Internal error: unknown coercion result found: {coercionResult}.")
        };

        if (message != null)
            throw new TokenException(message) { Token = ErrorToken };

        return value;
    }

    /// <summary>
    /// This is a convenience method for getting a value of a specific type from this
    /// term.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="isRequired">A flag noting whether the value is required (i.e., cannot
    /// be <c>null</c>).</param>
    /// <returns>The current value of this term.</returns>
    public TValue GetValue<TValue>(Variables variables, bool isRequired = true)
    {
        TValue value = (TValue) GetValue(variables, typeof(TValue));

        if (isRequired && value == null)
        {
            throw new TokenException($"Could not resolve this to something of type {typeof(TValue).Name}")
            {
                Token = ErrorToken
            };
        }

        return value;
    }

    /// <summary>
    /// This method must be provided by subclasses to evaluate this term to produce a
    /// value.  If the type of value returned does not match the given target type, an
    /// attempt will be made to coerce the returned value to that type.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected abstract object Evaluate(Variables variables, params Type[] targetTypes);

    /// <summary>
    /// This method is used to lower this term into a <see cref="FieldExpression"/>: the arithmetic of
    /// one number, in the three variables a field function is a function of.  A term that cannot mean
    /// anything there says so, against the text that wrote it.
    /// <para>
    /// Most of what a scene may write is not arithmetic on numbers -- text, colors, tuples, the casts
    /// -- and a field has no use for any of it, so the answer here is "no" by default and each term
    /// that <i>can</i> be lowered says as much for itself.  Anything the scene already knows the value
    /// of becomes a number on the way through, since a field is compiled once and then asked millions
    /// of questions, and none of those need to look a variable up again.
    /// </para>
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <returns>This term, as a field expression.</returns>
    public virtual FieldExpression ToField(Variables variables)
    {
        throw new TokenException(
            "A function may hold only arithmetic on numbers, the variables x, y and z, and calls to " +
            "the functions that take and give back numbers.  This is none of those.")
        {
            Token = ErrorToken
        };
    }
}
