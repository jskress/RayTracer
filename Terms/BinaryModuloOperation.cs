using Lex.Parser;
using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the binary modulo operation.
/// </summary>
public class BinaryModuloOperation : BinaryOperation
{
    public BinaryModuloOperation(Term left, Term right) : base(left, right) {}

    /// <summary>
    /// This method is used to evaluate this term to produce the remainder of the division
    /// of two values. 
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        object left = Left.GetValue(variables);
        object right = Right.GetValue(variables);

        return left switch
        {
            double doubleLeft when right is double doubleRight => doubleLeft % doubleRight,
            _ => throw new TokenException(GetTypeError("get remainder", left, right))
            {
                Token = ErrorToken
            }
        };
    }
}
