using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents a unary operation on a value.
/// </summary>
public abstract class UnaryOperation : Term
{
    /// <summary>
    /// This holds the operand for the operation.
    /// </summary>
    protected readonly Term Operand;

    protected UnaryOperation(Term operand) : base(operand.ErrorToken)
    {
        Operand = operand;
    }
}
