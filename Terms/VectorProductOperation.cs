using RayTracer.General;

namespace RayTracer.Terms;

/// <summary>
/// This class represents the <c>⋅</c> and <c>×</c> operators: the dot and cross products of two
/// vectors.
/// <para>
/// Given anything other than two vectors, each falls back on plain multiplication.  That is not a
/// convenience but the point of them: printed mathematics uses both symbols for scalar
/// multiplication far more often than for either vector product, so a formula pasted in with
/// <c>2 ⋅ 3</c> in it has to mean 6.  Which operation is meant follows from what the operands turn
/// out to be, exactly as it does for a call to an overloaded function.
/// </para>
/// </summary>
public class VectorProductOperation : BinaryOperation
{
    private readonly bool _isCrossProduct;

    internal VectorProductOperation(Term left, Term right, bool isCrossProduct) : base(left, right)
    {
        _isCrossProduct = isCrossProduct;
    }

    /// <summary>
    /// This method is used to evaluate this term to produce the product of two values.
    /// </summary>
    /// <param name="variables">The variables that are currently in scope.</param>
    /// <param name="targetTypes">The expected type of the evaluated value, if known.</param>
    /// <returns>The current value of this term.</returns>
    protected override object Evaluate(Variables variables, params Type[] targetTypes)
    {
        object left = Left.GetValue(variables);
        object right = Right.GetValue(variables);

        // Asking the catalog rather than testing the types here is what makes a tuple work: a
        // scene's [1, 2, 3] evaluates to a tuple and becomes a vector only when something wants
        // one, and the catalog applies exactly the conversions the rest of the language does.
        FunctionMatch match = FunctionCatalog.Instance.Match(
            _isCrossProduct ? "cross" : "dot", left, right);

        return match.IsMatch
            ? match.Invoke()
            : BinaryMultiplyOperation.Multiply(left, right, ErrorToken);
    }
}
