using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using RayTracer.General;

namespace RayTracer.Core;

/// <summary>
/// This class defines some well-known indices of refraction.
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static class IndicesOfRefraction
{
    public const double Vacuum = 1;
    public const double Air = 1.000293;
    public const double CarbonDioxide = 1.00045;
    public const double Helium = 1.000036;
    public const double Hydrogen = 1.000132;
    public const double Water = 1.333;
    public const double Kerosene = 1.39;
    public const double Glass = 1.52;
    public const double Amber = 1.55;
    public const double Diamond = 2.417;

    /// <summary>
    /// This field holds our cache of known, named colors.
    /// </summary>
    private static readonly Lazy<Dictionary<string, double>> LazyNamedColors = new (GetNamedIoRs);

    /// <summary>
    /// This is a helper method for building a dictionary of all known indices of refraction,
    /// keyed by their names.
    /// </summary>
    /// <returns>The dictionary of known indices of refraction.</returns>
    private static Dictionary<string, double> GetNamedIoRs()
    {
        return typeof(IndicesOfRefraction)
            .GetFields(BindingFlags.Static | BindingFlags.Public)
            .Where(fieldInfo => fieldInfo.IsLiteral && !fieldInfo.IsInitOnly &&
                                fieldInfo.FieldType == typeof(double))
            .ToDictionary(info => info.Name, info => (double) info.GetValue(null)!);
    }

    /// <summary>
    /// This method is used to register all our known indices of refraction as variables
    /// in the given variable pool.
    /// </summary>
    /// <param name="variables">The variable pool to add the colors to.</param>
    public static void AddToVariables(Variables variables)
    {
        foreach (KeyValuePair<string, double> pair in LazyNamedColors.Value)
            variables.SetValue(pair.Key, pair.Value);
    }
}
