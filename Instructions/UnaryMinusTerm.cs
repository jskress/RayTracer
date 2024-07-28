using Lex.Tokens;
using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class represents the unary minus operation.
/// </summary>
public class UnaryMinusTerm : Term
{
    private readonly Term _operand;

    public UnaryMinusTerm(Term operand) : base(operand.ErrorToken)
    {
        _operand = operand;
    }

    /// <summary>
    /// This method is used to evaluate this term to produce the negative of a value. 
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        double value = (double) _operand.GetValue(variables, typeof(double));

        return -value;
    }
}
