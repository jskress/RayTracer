using System.Reflection;
using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Core;

/// <summary>
/// This class defines some common direction vectors.  All are of unit length.
/// </summary>
public static class Directions
{
    public static readonly Vector Up = new (0, 1, 0);
    public static readonly Vector Down = new (0, -1, 0);
    public static readonly Vector Left = new (-1, 0, 0);
    public static readonly Vector Right = new (1, 0, 0);
    public static readonly Vector In = new (0, 0, 1);
    public static readonly Vector Out = new (0, 0, -1);

    /// <summary>
    /// This field holds our cache of known, named vectors.
    /// </summary>
    private static readonly Lazy<Dictionary<string, Vector>> LazyNamedVectors = new (GetNamedVectors);

    /// <summary>
    /// This is a helper method for building a dictionary of all known vectors, keyed by
    /// their names.
    /// </summary>
    /// <returns>The dictionary of known vectors.</returns>
    private static Dictionary<string, Vector> GetNamedVectors()
    {
        return typeof(Directions)
            .GetFields(BindingFlags.Static | BindingFlags.Public)
            .Where(fieldInfo => fieldInfo.FieldType == typeof(Vector))
            .ToDictionary(info => info.Name, info => info.GetValue(null) as Vector);
    }

    /// <summary>
    /// This method is used to register all our known vectors as variables in the given
    /// variable pool.
    /// </summary>
    /// <param name="variables">The variable pool to add the vectors to.</param>
    public static void AddToVariables(Variables variables)
    {
        foreach (KeyValuePair<string, Vector> pair in LazyNamedVectors.Value)
            variables.SetValue(pair.Key, pair.Value);
    }
}
