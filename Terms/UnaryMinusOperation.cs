using Lex.Parser;
using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the unary minus operation.
/// </summary>
public class UnaryMinusOperation : UnaryOperation
{
    public UnaryMinusOperation(Term operand) : base(operand) {}

    /// <summary>
    /// This method is used to evaluate this term to produce the negative of a value. 
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        object operand = Operand.GetValue(variables);

        return operand switch
        {
            Vector vectorValue => -vectorValue,
            double doubleValue => -doubleValue,
            _ => throw new TokenException(
                $"Cannot produce the negative of items of type {operand?.GetType().Name ?? "<null>"}.")
            {
                Token = ErrorToken
            }
        };
    }
}
