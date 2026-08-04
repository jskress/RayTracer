using System.Linq.Expressions;

namespace RayTracer.Fields;

/// <summary>
/// This enumeration names the four ways two numbers may be combined in a field function.
/// </summary>
public enum FieldOperator
{
    Add,
    Subtract,
    Multiply,
    Divide
}

/// <summary>
/// This class represents one piece of arithmetic in a field function.
/// </summary>
public class FieldArithmetic : FieldExpression
{
    /// <summary>
    /// This property holds which piece of arithmetic this is.
    /// </summary>
    public FieldOperator Operator { get; }

    /// <summary>
    /// This property holds the left operand.
    /// </summary>
    public FieldExpression Left { get; }

    /// <summary>
    /// This property holds the right operand.
    /// </summary>
    public FieldExpression Right { get; }

    private FieldArithmetic(FieldOperator fieldOperator, FieldExpression left, FieldExpression right)
    {
        Operator = fieldOperator;
        Left = left;
        Right = right;
    }

    /// <summary>
    /// This method is used to combine two expressions, doing the work now wherever it can be done
    /// now.  Two constants become one, and the terms that change nothing -- adding nought,
    /// multiplying by one -- are dropped rather than emitted.
    /// <para>
    /// This matters more than tidiness: differentiating a tree makes a great deal of arithmetic on
    /// nought and one, and folding it as the derivative is built is what keeps a gradient from being
    /// several times the size of the function it came from.
    /// </para>
    /// </summary>
    /// <param name="fieldOperator">The arithmetic to do.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The combined expression.</returns>
    public static FieldExpression Of(
        FieldOperator fieldOperator, FieldExpression left, FieldExpression right)
    {
        if (left.ConstantValue is { } leftValue && right.ConstantValue is { } rightValue)
        {
            return new FieldConstant(fieldOperator switch
            {
                FieldOperator.Add => leftValue + rightValue,
                FieldOperator.Subtract => leftValue - rightValue,
                FieldOperator.Multiply => leftValue * rightValue,
                FieldOperator.Divide => leftValue / rightValue,
                _ => throw new ArgumentOutOfRangeException(nameof(fieldOperator))
            });
        }

        FieldExpression simplified = Simplify(fieldOperator, left, right);

        return simplified ?? new FieldArithmetic(fieldOperator, left, right);
    }

    /// <summary>
    /// This method is used to spot the cases where one side of the arithmetic makes the other side
    /// the whole answer.  Division by a variable is deliberately left alone: nought over something
    /// that may itself be nought is not nought.
    /// </summary>
    /// <param name="fieldOperator">The arithmetic to do.</param>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>The simpler form, or <c>null</c> if there is not one.</returns>
    private static FieldExpression Simplify(
        FieldOperator fieldOperator, FieldExpression left, FieldExpression right)
    {
        FieldConstant leftConstant = left as FieldConstant;
        FieldConstant rightConstant = right as FieldConstant;

        switch (fieldOperator)
        {
            case FieldOperator.Add:
                if (leftConstant is not null && leftConstant.Is(0)) return right;
                if (rightConstant is not null && rightConstant.Is(0)) return left;
                break;

            case FieldOperator.Subtract:
                if (rightConstant is not null && rightConstant.Is(0)) return left;
                if (leftConstant is not null && leftConstant.Is(0)) return FieldNegation.Of(right);
                break;

            case FieldOperator.Multiply:
                if (leftConstant is not null && leftConstant.Is(0)) return FieldConstant.Zero;
                if (rightConstant is not null && rightConstant.Is(0)) return FieldConstant.Zero;
                if (leftConstant is not null && leftConstant.Is(1)) return right;
                if (rightConstant is not null && rightConstant.Is(1)) return left;
                if (leftConstant is not null && leftConstant.Is(-1)) return FieldNegation.Of(right);
                if (rightConstant is not null && rightConstant.Is(-1)) return FieldNegation.Of(left);
                break;

            case FieldOperator.Divide:
                if (rightConstant is not null && rightConstant.Is(1)) return left;
                if (rightConstant is not null && rightConstant.Is(-1)) return FieldNegation.Of(left);
                break;
        }

        return null;
    }

    /// <summary>
    /// This method is used to emit this arithmetic as a .NET expression.
    /// </summary>
    internal override Expression ToDotNet(
        ParameterExpression x, ParameterExpression y, ParameterExpression z)
    {
        Expression left = Left.ToDotNet(x, y, z);
        Expression right = Right.ToDotNet(x, y, z);

        return Operator switch
        {
            FieldOperator.Add => Expression.Add(left, right),
            FieldOperator.Subtract => Expression.Subtract(left, right),
            FieldOperator.Multiply => Expression.Multiply(left, right),
            FieldOperator.Divide => Expression.Divide(left, right),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public override string ToString()
    {
        string symbol = Operator switch
        {
            FieldOperator.Add => "+",
            FieldOperator.Subtract => "-",
            FieldOperator.Multiply => "*",
            FieldOperator.Divide => "/",
            _ => "?"
        };

        return $"({Left} {symbol} {Right})";
    }

    /// <summary>
    /// This method is used to differentiate this arithmetic: the sum and difference rules, and the
    /// product and quotient rules.
    /// </summary>
    public override FieldExpression Differentiate(FieldAxis axis)
    {
        FieldExpression left = Left.Differentiate(axis);
        FieldExpression right = Right.Differentiate(axis);

        switch (Operator)
        {
            case FieldOperator.Add:
            case FieldOperator.Subtract:
                return Of(Operator, left, right);

            case FieldOperator.Multiply:
                // (fg)' = f'g + fg'
                return Of(FieldOperator.Add,
                    Of(FieldOperator.Multiply, left, Right),
                    Of(FieldOperator.Multiply, Left, right));

            case FieldOperator.Divide:
                // (f/g)' = (f'g - fg') / g²
                return Of(FieldOperator.Divide,
                    Of(FieldOperator.Subtract,
                        Of(FieldOperator.Multiply, left, Right),
                        Of(FieldOperator.Multiply, Left, right)),
                    Of(FieldOperator.Multiply, Right, Right));

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// This method is used to work out the range of this arithmetic, which is that arithmetic done on
    /// the ranges of the two sides.
    /// </summary>
    public override FieldRange Bound(FieldRange x, FieldRange y, FieldRange z)
    {
        FieldRange left = Left.Bound(x, y, z);
        FieldRange right = Right.Bound(x, y, z);

        return Operator switch
        {
            FieldOperator.Add => left + right,
            FieldOperator.Subtract => left - right,
            FieldOperator.Multiply => left * right,
            FieldOperator.Divide => left / right,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
