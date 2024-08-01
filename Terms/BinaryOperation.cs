namespace RayTracer.Terms;

/// <summary>
/// This class represents a binary operation on a pair of values.
/// </summary>
public abstract class BinaryOperation : Term
{
    /// <summary>
    /// This holds the left operand for the operation.
    /// </summary>
    protected readonly Term Left;

    /// <summary>
    /// This holds the right operand for the operation.
    /// </summary>
    protected readonly Term Right;

    protected BinaryOperation(Term left, Term right) : base(left.ErrorToken)
    {
        Left = left;
        Right = right;
    }

    /// <summary>
    /// This is a helper method for formatting a type error for the binary operation.
    /// </summary>
    /// <param name="verb">The verb being attempted.</param>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns></returns>
    protected static string GetTypeError(string verb, object left, object right)
    {
        string leftText = left?.GetType().Name ?? "<null>";
        string rightText = right?.GetType().Name ?? "<null>";

        return $"Cannot {verb} items of type {leftText} to those of type {rightText}.";
    }
}
