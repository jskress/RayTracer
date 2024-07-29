using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the unary square operation.
/// </summary>
public class SquareOperation : UnaryDoubleOperation
{
    public SquareOperation(Term operand) : base(operand) {}

    /// <summary>
    /// This method is used to square the given value.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="value">The value to operate on.</param>
    /// <returns>The updated value.</returns>
    protected override double Apply(Variables variables, double value)
    {
        return value * value;
    }
}
