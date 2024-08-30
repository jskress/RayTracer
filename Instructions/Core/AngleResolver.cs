using RayTracer.Extensions;
using RayTracer.General;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class is used to resolve a value that is an angle, which is converted, as needed,
/// to radians.
/// </summary>
public class AngleResolver : TermResolver<double>
{
    /// <summary>
    /// This method is used to ensure the result of the term evaluation is of the proper
    /// (declared) type.  In this case, the type will be correct, but we take the opportunity
    /// to ensure that the value is an angle in radians.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="value">The value to coerce.</param>
    /// <returns>The coerced value.</returns>
    protected override double Coerce(RenderContext context, object value)
    {
        double angle = (double) value;

        return context.AnglesAreRadians ? angle : angle.ToRadians();
    }
}
