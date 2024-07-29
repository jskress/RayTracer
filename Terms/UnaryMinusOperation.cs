using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the unary minus operation.
/// </summary>
public class UnaryMinusOperation : UnaryDoubleOperation
{
    public UnaryMinusOperation(Term operand) : base(operand) {}

    /// <summary>
    /// This method is used to negate the given value.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="value">The value to operate on.</param>
    /// <returns>The updated value.</returns>
    protected override double Apply(Variables variables, double value)
    {
        return -value;
    }
}
