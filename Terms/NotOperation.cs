using RayTracer.General;

namespace RayTracer.Terms;

public class NotOperation : UnaryOperation
{
    public NotOperation(Term operand) : base(operand) {}

    /// <summary>
    /// This method is used to evaluate this term to produce the negative of a value. 
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        bool value = (bool) Operand.GetValue(variables);

        return !value;
    }
}
