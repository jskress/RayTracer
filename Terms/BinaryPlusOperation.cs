using Lex.Parser;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the binary addition operation.
/// </summary>
public class BinaryPlusOperation : BinaryOperation
{
    public BinaryPlusOperation(Term left, Term right) : base(left, right) {}

    /// <summary>
    /// This method is used to evaluate this term to produce the sum of two values. 
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
            Point pointLeft when right is Vector vectorRight => pointLeft + vectorRight,
            Vector vectorLeft when right is Point pointRight => vectorLeft + pointRight,
            Vector vectorLeft when right is Vector vectorRight => vectorLeft + vectorRight,
            Color colorLeft when right is Color colorRight => colorLeft + colorRight,
            double doubleLeft when right is double doubleRight => doubleLeft + doubleRight,
            string stringLeft => stringLeft + right,
            _ => throw new TokenException(GetTypeError("add", left, right))
            {
                Token = ErrorToken
            }
        };
    }
}
