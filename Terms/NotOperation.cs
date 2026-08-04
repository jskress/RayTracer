using Lex.Parser;
using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the negation of a true/false value, written <c>!</c> or <c>¬</c>.
/// </summary>
public class NotOperation : UnaryOperation
{
    public NotOperation(Term operand) : base(operand) {}

    /// <summary>
    /// This method is used to evaluate this term to produce the negative of a value.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        object operand = Operand.GetValue(variables);

        if (operand is bool value)
            return !value;

        string given = operand is null ? "null" : FunctionSignature.DslNameFor(operand.GetType());

        throw new TokenException($"Only true or false can be negated, not {given}.")
        {
            Token = ErrorToken
        };
    }
}
