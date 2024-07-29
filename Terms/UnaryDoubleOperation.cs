namespace RayTracer.Terms;

/// <summary>
/// This class represents a unary operation on a double value.
/// </summary>
public abstract class UnaryDoubleOperation : UnaryOperation<double>
{
    protected UnaryDoubleOperation(Term operand) : base(operand) {}
}
