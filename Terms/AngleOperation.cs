using Lex.Parser;
using Lex.Tokens;
using RayTracer.Extensions;
using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents one of the postfix operators that says what unit an angle was written in:
/// <c>90°</c>, <c>90 degrees</c> or <c>1.5 radians</c>.
/// <para>
/// Radians are what the trigonometric functions take, so <c>degrees</c> and <c>°</c> convert while
/// <c>radians</c> converts nothing.  That the one does nothing is the point of having it: it says
/// out loud what was already true, the way <c>angles are degrees</c> does in a context block, and it
/// still insists on being handed a number.
/// </para>
/// <para>
/// There is no function that goes this way, deliberately -- <see cref="MathFunctions.ToDegrees"/>
/// explains why -- so unlike the root and product operators this one is not sugar for a call.
/// </para>
/// </summary>
public class AngleOperation : UnaryOperation
{
    private readonly bool _isInDegrees;

    internal AngleOperation(Term operand, bool isInDegrees) : base(operand)
    {
        _isInDegrees = isInDegrees;
    }

    /// <summary>
    /// This method is used to evaluate this term, giving the angle in radians however it was
    /// written.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        object operand = Operand.GetValue(variables);

        if (operand is not double angle)
        {
            string given = operand is null ? "null" : FunctionSignature.DslNameFor(operand.GetType());

            throw new TokenException($"An angle must be a number, not {given}.")
            {
                Token = ErrorToken
            };
        }

        return _isInDegrees ? angle.ToRadians() : angle;
    }
}
