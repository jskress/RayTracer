using Lex.Parser;
using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents <c>and</c> and <c>or</c> over a pair of true/false values, written
/// <c>&amp;&amp;</c> and <c>||</c>, or as <c>∧</c> and <c>∨</c>.
/// <para>
/// The right side is only looked at when the left side leaves the answer open: <c>false &amp;&amp;</c>
/// anything is false and <c>true ||</c> anything is true, whatever the other side would have said.
/// That is not merely a saving -- it is what lets a test guard the thing it is written beside, so
/// that <c>size != 0 &amp;&amp; 10 / size &lt; 1</c> is safe to write.
/// </para>
/// </summary>
public class LogicalOperation : BinaryOperation
{
    private readonly bool _isAnd;

    public LogicalOperation(Term left, Term right, bool isAnd) : base(left, right)
    {
        _isAnd = isAnd;
    }

    /// <summary>
    /// This method is used to evaluate this term to produce the result of the operation.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        bool left = AsBoolean(Left.GetValue(variables));

        // An "and" is settled by a false on the left, and an "or" by a true.
        if (left != _isAnd)
            return left;

        return AsBoolean(Right.GetValue(variables));
    }

    /// <summary>
    /// This method is used to insist that a value is true or false.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>The value, as a boolean.</returns>
    private bool AsBoolean(object value)
    {
        if (value is bool result)
            return result;

        string given = value is null ? "null" : FunctionSignature.DslNameFor(value.GetType());

        throw new TokenException(
            $"The {(_isAnd ? "and" : "or")} operator needs true or false, not {given}.")
        {
            Token = ErrorToken
        };
    }
}
