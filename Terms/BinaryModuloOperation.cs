using Lex.Parser;
using RayTracer.General;
using RayTracer.Fields;

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

    /// <summary>
    /// This method is used to lower this operation into a field expression, which it cannot be.
    /// <para>
    /// The remainder operator is not the same thing as the <c>mod</c> function -- its sign follows the
    /// number being divided, so it mirrors about the origin where <c>mod</c> repeats -- and a field can
    /// have the one whose slope and range are known.  Since a field is very often written to tile
    /// something, saying which to reach for is more use than refusing in general terms.
    /// </para>
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <returns>Nothing; this always reports the problem.</returns>
    public override FieldExpression ToField(Variables variables)
    {
        throw new TokenException(
            "A function cannot use the % operator; write mod(value, divisor) instead, which repeats " +
            "either side of the origin rather than mirroring about it.")
        {
            Token = ErrorToken
        };
    }
}
