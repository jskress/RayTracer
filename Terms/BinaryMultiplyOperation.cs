using System.Text;
using Lex.Parser;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the binary multiplication operation.
/// </summary>
public class BinaryMultiplyOperation : BinaryOperation
{
    public BinaryMultiplyOperation(Term left, Term right) : base(left, right) {}

    /// <summary>
    /// This method is used to evaluate this term to produce the product of two values. 
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
            Vector vectorLeft when right is double doubleRight => vectorLeft * doubleRight,
            double doubleLeft when right is Vector vectorRight => doubleLeft * vectorRight,
            Color colorLeft when right is Color colorRight => colorLeft * colorRight,
            Color colorLeft when right is double doubleRight => colorLeft * doubleRight,
            double doubleLeft when right is Color colorRight => doubleLeft * colorRight,
            Matrix matrixLeft when right is Matrix matrixRight => matrixLeft * matrixRight,
            Matrix matrixLeft when right is Point pointRight => matrixLeft * pointRight,
            Point pointLeft when right is Matrix matrixRight => pointLeft * matrixRight,
            Matrix matrixLeft when right is Vector vectorRight => matrixLeft * vectorRight,
            Vector vectorLeft when right is Matrix matrixRight => vectorLeft * matrixRight,
            double doubleLeft when right is double doubleRight => doubleLeft * doubleRight,
            string stringLeft when right is double doubleRight =>
                new StringBuilder(stringLeft.Length * (int) doubleRight)
                    .Insert(0, stringLeft, (int) doubleRight)
                    .ToString(),
            _ => throw new TokenException(GetTypeError("multiply", left, right))
            {
                Token = ErrorToken
            }
        };
    }
}
