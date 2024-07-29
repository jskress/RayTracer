using RayTracer.General;

namespace RayTracer.Terms;

public class NotOperation : UnaryOperation<bool>
{
    public NotOperation(Term operand) : base(operand) {}

    /// <summary>
    /// This method is used to negate (flip) the given boolean value value.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="value">The value to operate on.</param>
    /// <returns>The updated value.</returns>
    protected override bool Apply(Variables variables, bool value)
    {
        return !value;
    }
}
