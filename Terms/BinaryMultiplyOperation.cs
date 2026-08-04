using System.Text;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Fields;

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
        return Multiply(Left.GetValue(variables), Right.GetValue(variables), ErrorToken);
    }

    /// <summary>
    /// This method is used to multiply two values, whatever they turn out to be.  It is here rather
    /// than inline above because the <c>⋅</c> and <c>×</c> operators fall back on it: each means a
    /// product of vectors when given two vectors and plain multiplication otherwise, which is what a
    /// pasted formula using them for scalars expects.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <param name="errorToken">The token to report a type error against.</param>
    /// <returns>The product of the two values.</returns>
    internal static object Multiply(object left, object right, Token errorToken)
    {
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
                Token = errorToken
            }
        };
    }

    /// <summary>
    /// This method is used to lower this operation into a field expression.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <returns>This term, as a field expression.</returns>
    public override FieldExpression ToField(Variables variables)
    {
        return FieldArithmetic.Of(
            FieldOperator.Multiply, Left.ToField(variables), Right.ToField(variables));
    }
}
