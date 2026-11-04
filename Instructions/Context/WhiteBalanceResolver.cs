using RayTracer.General;

namespace RayTracer.Instructions.Context;

/// <summary>
/// This class resolves what a picture is balanced for: a temperature, in kelvin, or the name of a lamp (see
/// <see cref="RayTracer.Core.Lamps"/>).
/// </summary>
public class WhiteBalanceResolver : TermResolver<object>
{
    public WhiteBalanceResolver()
    {
        PossibleTypes = [typeof(double), typeof(string)];
    }

    /// <summary>
    /// This method refuses a temperature at or below nothing and a name that is no lamp.
    /// </summary>
    /// <param name="value">What was said.</param>
    /// <returns>Why it is refused, or <c>null</c>.</returns>
    internal static string Refusal(object value)
    {
        return value switch
        {
            string name when !RayTracer.Core.Lamps.Knows(name) =>
                $"There is no lamp named \"{name}\" to balance for; the lamps known are {RayTracer.Core.Lamps.Listed}.",
            string => null,
            double kelvin when kelvin <= 0 =>
                "A white balance is a temperature in kelvin, and must be above nothing.",
            _ => null
        };
    }
}
