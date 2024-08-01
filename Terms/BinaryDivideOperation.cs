using Lex.Parser;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the binary division operation.
/// </summary>
public class BinaryDivideOperation : BinaryOperation
{
    public BinaryDivideOperation(Term left, Term right) : base(left, right) {}

    /// <summary>
    /// This method is used to evaluate this term to produce the division of two values. 
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
            Vector vectorLeft when right is double doubleRight => vectorLeft / doubleRight,
            Color colorLeft when right is double doubleRight => colorLeft / doubleRight,
            double doubleLeft when right is double doubleRight => doubleLeft / doubleRight,
            _ => throw new TokenException(GetTypeError("divide", left, right))
            {
                Token = ErrorToken
            }
        };
    }
}
