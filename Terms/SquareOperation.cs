using Lex.Parser;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Fields;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the unary square operation.
/// </summary>
public class SquareOperation : UnaryOperation
{
    public SquareOperation(Term operand) : base(operand) {}

    internal SquareOperation(Term operand, Token errorToken) : base(operand, errorToken) {}

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

    /// <summary>
    /// This method is used to lower this operation into a field expression: a multiplication rather
    /// than a call to <c>pow</c>, since multiplying is the quicker of the two and <c>x²</c> is about
    /// the most common thing a field function says.  The operand is emitted more than once to do it,
    /// which costs something when the operand is itself expensive; the superscripts above three are
    /// already calls to <c>pow</c> and so pay nothing twice.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <returns>This term, as a field expression.</returns>
    public override FieldExpression ToField(Variables variables)
    {
        FieldExpression operand = Operand.ToField(variables);

        return FieldArithmetic.Of(FieldOperator.Multiply, operand, operand);
    }
}
