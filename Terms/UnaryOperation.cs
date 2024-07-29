using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents a unary operation on a value of a particular type.
/// </summary>
public abstract class UnaryOperation<TValue> : Term
{
    private readonly Term _operand;

    protected UnaryOperation(Term operand) : base(operand.ErrorToken)
    {
        _operand = operand;
    }

    /// <summary>
    /// This method is used to evaluate this term to produce the negative of a value. 
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        TValue value = (TValue) _operand.GetValue(variables, typeof(TValue));

        return Apply(variables, value);
    }

    /// <summary>
    /// This method must be overridden by subclasses to perform the actual operation.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="value">The value to operate on.</param>
    /// <returns>The updated value.</returns>
    protected abstract TValue Apply(Variables variables, TValue value);
}
