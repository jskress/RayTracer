using Lex.Tokens;
using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents a term that is a literal.
/// </summary>
public class LiteralTerm : Term
{
    /// <summary>
    /// This method is used to create a literal based on the given token.
    /// </summary>
    /// <param name="token">The token to create a literal value for.</param>
    /// <returns>The relevant literal term.</returns>
    public static LiteralTerm CreateLiteralTerm(Token token)
    {
        object value = null;

        if (token is NumberToken numberToken)
        {
            value = numberToken.IsFloatingPoint
                ? numberToken.FloatingPointNumber
                : Convert.ToDouble(numberToken.IntegralNumber);
        }
        else if (token is StringToken stringToken)
            value = stringToken.Text;
        else if (token is KeywordToken keywordToken)
        {
            value = keywordToken.Text switch
            {
                "true" => true,
                "false" => false,
                "null" => null,
                _ => null
            };
        }

        return new LiteralTerm(token, value);
    }

    /// <summary>
    /// This method is used to create a literal holding a value that no token spells out.  An
    /// operator that stands for a function call needs this: the exponent of <c>x⁴</c> is a real
    /// argument to <c>pow</c>, but the only token behind it is the operator itself.
    /// </summary>
    /// <param name="errorToken">The token to report errors against.</param>
    /// <param name="value">The value the literal holds.</param>
    /// <returns>A literal term holding that value.</returns>
    internal static LiteralTerm Of(Token errorToken, object value)
    {
        return new LiteralTerm(errorToken, value);
    }

    private readonly object _value;

    private LiteralTerm(Token errorToken, object value) : base(errorToken)
    {
        _value = value;
    }

    /// <summary>
    /// This method is used to evaluate this term to produce our literal value. 
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        return _value;
    }
}
