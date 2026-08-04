using Lex.Tokens;

namespace RayTracer.Terms;

/// <summary>
/// This class represents a unary operation on a value.
/// </summary>
public abstract class UnaryOperation : Term
{
    /// <summary>
    /// This holds the operand for the operation.
    /// </summary>
    protected readonly Term Operand;

    protected UnaryOperation(Term operand) : base(operand.ErrorToken)
    {
        Operand = operand;
    }

    /// <summary>
    /// This constructor reports errors against the operator itself rather than against what it
    /// acts on.  The powers want that: where the operator sits is what tells one power written on
    /// top of another from one written around it.
    /// </summary>
    /// <param name="operand">The operand for the operation.</param>
    /// <param name="errorToken">The token to report errors against.</param>
    protected UnaryOperation(Term operand, Token errorToken) : base(errorToken)
    {
        Operand = operand;
    }
}
