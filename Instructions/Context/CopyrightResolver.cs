using RayTracer.General;

namespace RayTracer.Instructions.Context;

/// <summary>
/// This class is used to resolve a value from a term to a copyright string.
/// </summary>
public class CopyrightResolver : TermResolver<string>
{
    public CopyrightResolver()
    {
        PossibleTypes = [typeof(string), typeof(bool)];
    }

    /// <summary>
    /// This method is used to coerce the result of evaluating our term to a string.  The
    /// value will be either a string or a boolean.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="value">The value to coerce.</param>
    /// <returns>The coerced value.</returns>
    protected override string Coerce(RenderContext context, object value)
    {
        return value is bool booleanValue
            ? booleanValue
                ? $"Copyright \u00a9 {DateTime.Now.Year}"
                : null
            : value.ToString();
    }
}
