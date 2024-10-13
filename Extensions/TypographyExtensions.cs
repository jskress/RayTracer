using System.Reflection;
using Typography.OpenFont;

namespace RayTracer.Extensions;

/// <summary>
/// This class gives us some extra things for our typography library.
/// </summary>
public static class TypographyExtensions
{
    public static readonly PropertyInfo KernProperty = typeof(Typeface)
        .GetProperty("KernTable", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// This is a helper method for telling us whether the given typeface has a kerning
    /// table.
    /// </summary>
    /// <param name="typeface">The typeface to check.</param>
    /// <returns><c>true</c>, if the typeface has a kerning table, or <c>false</c>, if not.</returns>
    public static bool HasKernTable(this Typeface typeface)
    {
        return KernProperty.GetValue(typeface) != null;
    }
}
