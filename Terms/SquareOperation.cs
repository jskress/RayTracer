using Lex.Parser;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the unary square operation.
/// </summary>
public class SquareOperation : UnaryOperation
{
    public SquareOperation(Term operand) : base(operand) {}

    /// <summary>
    /// This method is used to evaluate this term to produce the square of a value. 
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        object operand = Operand.GetValue(variables);

        return operand switch
        {
            Color colorValue => colorValue * colorValue,
            Matrix matrixValue => matrixValue * matrixValue,
            double doubleValue => doubleValue * doubleValue,
            _ => throw new TokenException(
                $"Cannot square items of type {operand?.GetType().Name ?? "<null>"}.")
            {
                Token = ErrorToken
            }
        };
    }
}
