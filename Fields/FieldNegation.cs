using System.Linq.Expressions;

namespace RayTracer.Fields;

/// <summary>
/// This class represents the negation of a field expression.  A negation is arithmetic like any
/// other and could be written as a subtraction from nought or a multiplication by minus one, but it
/// is worth a node of its own: negating a negation cancels, and both the differentiation and the
/// bounding to come read more plainly for having it.
/// </summary>
public class FieldNegation : FieldExpression
{
    /// <summary>
    /// This property holds the expression being negated.
    /// </summary>
    public FieldExpression Operand { get; }

    private FieldNegation(FieldExpression operand)
    {
        Operand = operand;
    }

    /// <summary>
    /// This method is used to negate an expression, doing it now where it can be done now.
    /// </summary>
    /// <param name="operand">The expression to negate.</param>
    /// <returns>The negated expression.</returns>
    public static FieldExpression Of(FieldExpression operand)
    {
        if (operand.ConstantValue is { } value)
            return new FieldConstant(-value);

        // Two negations are none.
        return operand is FieldNegation negation
            ? negation.Operand
            : new FieldNegation(operand);
    }

    /// <summary>
    /// This method is used to emit this negation as a .NET expression.
    /// </summary>
    internal override Expression ToDotNet(
        ParameterExpression x, ParameterExpression y, ParameterExpression z)
    {
        return Expression.Negate(Operand.ToDotNet(x, y, z));
    }

    public override string ToString()
    {
        return $"-{Operand}";
    }

    /// <summary>
    /// The derivative of a negation is the negation of the derivative.
    /// </summary>
    public override FieldExpression Differentiate(FieldAxis axis)
    {
        return Of(Operand.Differentiate(axis));
    }

    /// <summary>
    /// Negating a range turns it end for end.
    /// </summary>
    public override FieldRange Bound(FieldRange x, FieldRange y, FieldRange z)
    {
        return -Operand.Bound(x, y, z);
    }
}
