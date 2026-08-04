using Lex.Parser;
using Lex.Tokens;
using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the conditional, <c>test ? whenTrue : whenFalse</c>: the one operator that
/// chooses between two values rather than combining them.
/// <para>
/// Only the side that is chosen is evaluated, which matters for the same reason it matters to
/// <see cref="LogicalOperation"/>: the test is often there to make the choice safe to evaluate at
/// all.  The two sides need not be of the same type, since a scene may perfectly well pick between
/// two different sorts of thing, and whatever is asking for the value will say what it wanted.
/// </para>
/// </summary>
public class ConditionalOperation : Term
{
    private readonly Term _test;
    private readonly Term _whenTrue;
    private readonly Term _whenFalse;

    internal ConditionalOperation(Token errorToken, Term test, Term whenTrue, Term whenFalse)
        : base(errorToken)
    {
        _test = test;
        _whenTrue = whenTrue;
        _whenFalse = whenFalse;
    }

    /// <summary>
    /// This method is used to evaluate this term by evaluating the test and then whichever of the two
    /// values it chose.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        object test = _test.GetValue(variables);

        if (test is not bool choice)
        {
            string given = test is null ? "null" : FunctionSignature.DslNameFor(test.GetType());

            throw new TokenException($"A condition must be true or false, not {given}.")
            {
                Token = ErrorToken
            };
        }

        return choice
            ? _whenTrue.GetValue(variables, targetTypes)
            : _whenFalse.GetValue(variables, targetTypes);
    }
}
