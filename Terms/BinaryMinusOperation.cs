using Lex.Parser;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Fields;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the binary subtraction operation.
/// </summary>
public class BinaryMinusOperation : BinaryOperation
{
    public BinaryMinusOperation(Term left, Term right) : base(left, right) {}

    /// <summary>
    /// This method is used to evaluate this term to produce the difference of two values. 
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
            Point pointLeft when right is Vector vectorRight => pointLeft - vectorRight,
            Point pointLeft when right is Point pointRight => pointLeft - pointRight,
            Vector vectorLeft when right is Vector vectorRight => vectorLeft - vectorRight,
            Color colorLeft when right is Color colorRight => colorLeft - colorRight,
            double doubleLeft when right is double doubleRight => doubleLeft - doubleRight,
            _ => throw new TokenException(GetTypeError("subtract", left, right))
            {
                Token = ErrorToken
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
            FieldOperator.Subtract, Left.ToField(variables), Right.ToField(variables));
    }
}
