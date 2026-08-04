using System.Linq.Expressions;
using RayTracer.Extensions;

namespace RayTracer.Fields;

/// <summary>
/// This class represents a number in a field function -- one a scene wrote, or one arrived at by
/// doing the arithmetic of other constants while the tree was being built.
/// </summary>
public class FieldConstant : FieldExpression
{
    /// <summary>
    /// Nought and one turn up in nearly every derivative, so they are worth not making twice.
    /// </summary>
    public static readonly FieldConstant Zero = new (0);
    public static readonly FieldConstant One = new (1);

    /// <summary>
    /// This property holds the number.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// A constant is, unsurprisingly, constant.
    /// </summary>
    public override double? ConstantValue => Value;

    public FieldConstant(double value)
    {
        Value = value;
    }

    /// <summary>
    /// This method is used to emit this constant as a .NET expression.
    /// </summary>
    internal override Expression ToDotNet(
        ParameterExpression x, ParameterExpression y, ParameterExpression z)
    {
        return Expression.Constant(Value);
    }

    /// <summary>
    /// This method reports whether this constant is the given number, which the arithmetic uses to
    /// spot the terms that need not be emitted at all.
    /// </summary>
    /// <param name="value">The number to test for.</param>
    /// <returns><c>true</c>, if this constant is that number.</returns>
    internal bool Is(double value)
    {
        return Value.Near(value);
    }

    public override string ToString()
    {
        return Value.ToString("R");
    }

    /// <summary>
    /// A number does not change, so its derivative is nought.
    /// </summary>
    public override FieldExpression Differentiate(FieldAxis axis)
    {
        return Zero;
    }

    /// <summary>
    /// A number is only ever itself.
    /// </summary>
    public override FieldRange Bound(FieldRange x, FieldRange y, FieldRange z)
    {
        return FieldRange.Just(Value);
    }
}
