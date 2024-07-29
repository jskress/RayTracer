using System.Linq.Expressions;
using RayTracer.Extensions;
using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This class represents the work of setting an angle property to a value.
/// </summary>
public class SetAnglePropertyInstruction<TObject> : SetObjectPropertyInstruction<TObject, double>
    where TObject : class
{
    public SetAnglePropertyInstruction(
        Expression<Func<TObject, double>> propertyLambda, Term term,
        Func<double, string> validator = null)
        : base(propertyLambda, term, validator) {}

    /// <summary>
    /// This method gives any subclasses the opportunity to adjust the given value before
    /// we actually store it.  By default, we do nothing to the value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="value">The value to adjust.</param>
    /// <returns>The (possibly) adjusted value.</returns>
    protected override double Adjust(RenderContext context, double value)
    {
        return context.AnglesAreRadians ? value : value.ToRadians();
    }
}
