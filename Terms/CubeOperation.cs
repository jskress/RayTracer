using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the unary cube operation.
/// </summary>
public class CubeOperation : UnaryDoubleOperation
{
    public CubeOperation(Term operand) : base(operand) {}

    /// <summary>
    /// This method is used to cube the given value.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="value">The value to operate on.</param>
    /// <returns>The updated value.</returns>
    protected override double Apply(Variables variables, double value)
    {
        return value * value * value;
    }
}
