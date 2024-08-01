using Lex.Parser;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the unary cube operation.
/// </summary>
public class CubeOperation : UnaryOperation
{
    public CubeOperation(Term operand) : base(operand) {}

    /// <summary>
    /// This method is used to evaluate this term to produce the cube of a value. 
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        object operand = Operand.GetValue(variables);

        return operand switch
        {
            Color colorValue => colorValue * colorValue * colorValue,
            Matrix matrixValue => matrixValue * matrixValue * matrixValue,
            double doubleValue => doubleValue * doubleValue * doubleValue,
            _ => throw new TokenException(
                $"Cannot cube items of type {operand?.GetType().Name ?? "<null>"}.")
            {
                Token = ErrorToken
            }
        };
    }
}
