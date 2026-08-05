using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to resolve a rate at which a medium does something to light for each unit of
/// distance -- color by color, since a haze that dims red more than blue is the whole reason these
/// are colors at all.
/// <para>
/// A scene that means the same rate in every color may simply write the one number, which reads
/// better than saying it three times and matches how the plain fade through a substance has always
/// been written.
/// </para>
/// </summary>
public class CoefficientResolver : TermResolver<Color>
{
    public CoefficientResolver()
    {
        PossibleTypes = [typeof(Color), typeof(double)];
    }

    /// <summary>
    /// This method is used to coerce the result of evaluating our term to a color.  A number stands
    /// for the same rate in every color.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="value">The value to coerce.</param>
    /// <returns>The coerced value.</returns>
    protected override Color Coerce(RenderContext context, object value)
    {
        return value is double number
            ? new Color(number, number, number)
            : (Color) value;
    }
}
