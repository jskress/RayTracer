using Lex.Parser;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Terms;

/// <summary>
/// This enumeration notes the ways two values may be compared.
/// </summary>
public enum Comparison
{
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
    Equal,
    NotEqual
}

/// <summary>
/// This class represents a comparison of two values, producing true or false.
/// <para>
/// Numbers and text may be compared any of the six ways; anything else may only be asked whether it
/// equals something, since there is no order to put two colors or two vectors in.
/// </para>
/// <para>
/// Two numbers count as equal when they are near enough to each other, rather than when their last
/// bits match, which is the same rule <see cref="NumberTuple.Matches"/> and the geometry all through
/// this ray tracer already use.  Exact equality of two computed numbers is almost never what is
/// meant, and a scene that asks whether a number it worked out equals 0.3 should not have the answer
/// turn on the arithmetic it happened to be worked out by.  The orderings are exact, since an
/// epsilon there would only move the boundary rather than remove it.
/// </para>
/// </summary>
public class ComparisonOperation : BinaryOperation
{
    private readonly Comparison _comparison;

    internal ComparisonOperation(Term left, Term right, Comparison comparison) : base(left, right)
    {
        _comparison = comparison;
    }

    /// <summary>
    /// This method is used to evaluate this term to produce the result of the comparison.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        object left = Left.GetValue(variables);
        object right = Right.GetValue(variables);

        if (left is double leftNumber && right is double rightNumber)
            return CompareNumbers(leftNumber, rightNumber);

        if (left is string leftText && right is string rightText)
            return Order(string.CompareOrdinal(leftText, rightText));

        if (_comparison is Comparison.Equal or Comparison.NotEqual)
            return AreEqual(left, right) == (_comparison == Comparison.Equal);

        throw new TokenException(GetTypeError("order", left, right))
        {
            Token = ErrorToken
        };
    }

    /// <summary>
    /// This method is used to compare two numbers.
    /// </summary>
    /// <param name="left">The left number.</param>
    /// <param name="right">The right number.</param>
    /// <returns>The result of the comparison.</returns>
    private bool CompareNumbers(double left, double right)
    {
        return _comparison switch
        {
            Comparison.Equal => left.Near(right),
            Comparison.NotEqual => !left.Near(right),
            Comparison.Less => left < right,
            Comparison.LessOrEqual => left <= right,
            Comparison.Greater => left > right,
            Comparison.GreaterOrEqual => left >= right,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// This method is used to read the result of an ordering comparison, where a negative number
    /// means the left value came first and a positive one means it came second.
    /// </summary>
    /// <param name="order">Where the left value sorts against the right one.</param>
    /// <returns>The result of the comparison.</returns>
    private bool Order(int order)
    {
        return _comparison switch
        {
            Comparison.Equal => order == 0,
            Comparison.NotEqual => order != 0,
            Comparison.Less => order < 0,
            Comparison.LessOrEqual => order <= 0,
            Comparison.Greater => order > 0,
            Comparison.GreaterOrEqual => order >= 0,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <summary>
    /// This method is used to test two values of any sort for equality.  Tuples -- and so the
    /// points, vectors and colors built on them -- compare near enough as equal, and only against
    /// their own kind, so that a vector and a point holding the same three numbers are still not the
    /// same thing.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><c>true</c>, if the two values are equal.</returns>
    private static bool AreEqual(object left, object right)
    {
        // A color is not a tuple and carries no equality of its own, so two of them written the same
        // way are two different objects; asked whether they are equal, it has to be their channels
        // that answer rather than which object each happens to be.
        if (left is Color leftColor && right is Color rightColor)
            return leftColor.Matches(rightColor);

        if (left is not NumberTuple leftTuple || right is not NumberTuple rightTuple)
            return Equals(left, right);

        return leftTuple.GetType() == rightTuple.GetType() &&
               IsSame(leftTuple.X, rightTuple.X) && IsSame(leftTuple.Y, rightTuple.Y) &&
               IsSame(leftTuple.Z, rightTuple.Z) && IsSame(leftTuple.W, rightTuple.W);
    }

    /// <summary>
    /// This method is used to test two of a tuple's numbers for equality.
    /// <para>
    /// This does not simply lean on <see cref="NumberTuple.Matches"/>, because a tuple written with
    /// three numbers has no fourth at all, and that absence is carried as NaN.  Nothing is near NaN,
    /// not even NaN, so a three-number tuple compared that way would not equal itself.  Two absences
    /// have to be allowed to agree.
    /// </para>
    /// </summary>
    /// <param name="left">The left number.</param>
    /// <param name="right">The right number.</param>
    /// <returns><c>true</c>, if the two numbers are the same.</returns>
    private static bool IsSame(double left, double right)
    {
        return double.IsNaN(left) && double.IsNaN(right) || left.Near(right);
    }
}
