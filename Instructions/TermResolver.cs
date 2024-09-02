using Lex.Parser;
using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to resolve a value from a term.
/// </summary>
public class TermResolver<TValue> : Resolver<TValue>
{
    /// <summary>
    /// This property holds the term we will evaluate to produce our value.
    /// </summary>
    public Term Term { get; init; }

    /// <summary>
    /// This property holds the validator, if any, that will be used for checking values
    /// as we resolve them.
    /// </summary>
    public Func<TValue, string> Validator { get; init; }

    /// <summary>
    /// This field holds the list of possible types our term may evaluate to.
    /// </summary>
    protected Type[] PossibleTypes = [typeof(TValue)];

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override TValue Resolve(RenderContext context, Variables variables)
    {
        TValue value = Coerce(context, Term.GetValue(variables, PossibleTypes));
        string message = Validator?.Invoke(value);

        if (message != null)
            throw new TokenException(message) { Token = Term.ErrorToken };

        return value;
    }

    /// <summary>
    /// This method is used to ensure the result of the term evaluation is of the proper
    /// (declared) type.  By default, a simple cast is done.  Override this method when
    /// you may have more than one possible type from evaluating the term so that you can
    /// coerce it to the type we are declared with.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="value">The value to coerce.</param>
    /// <returns>The coerced value.</returns>
    protected virtual TValue Coerce(RenderContext context, object value)
    {
        return (TValue) value;
    }
}
