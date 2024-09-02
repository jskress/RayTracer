using System.Diagnostics.CodeAnalysis;

namespace RayTracer.Core;

/// <summary>
/// This class provides a base class for things that can be named.
/// </summary>
public class NamedThing
{
    /// <summary>
    /// This property carries the name of the thing.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public string Name { get; set; }
}
