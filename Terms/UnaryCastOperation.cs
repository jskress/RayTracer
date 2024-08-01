using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the unary cast operation.
/// </summary>
public class UnaryCastOperation<TObject> : UnaryOperation
{
    public UnaryCastOperation(Term operand) : base(operand) {}

    /// <summary>
    /// This method is used to evaluate this term to cast a value to a particular type. 
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        return Operand.GetValue(variables, typeof(TObject));
    }
}
