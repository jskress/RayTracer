namespace RayTracer.Patterns;

/// <summary>
/// This enumeration notes all the supported band types.
/// </summary>
public enum BandType
{
    /// <summary>
    /// Defines a linear band along the X axis.
    /// </summary>
    LinearX,

    /// <summary>
    /// Defines a linear band along the Y axis.
    /// </summary>
    LinearY,

    /// <summary>
    /// Defines a linear band along the Z axis.
    /// </summary>
    LinearZ,

    /// <summary>
    /// Defines a radial band in the X/Z plane.
    /// </summary>
    Cylindrical,

    /// <summary>
    /// Defines a radial band in all directions.
    /// </summary>
    Spherical
}

/// <summary>
/// This class provides some useful additions to the <see cref="BandType"/> enumeration.
/// </summary>
internal static class BandTypeExtensions
{
    /// <summary>
    /// This method tells us whether the given band type is linear.
    /// </summary>
    /// <param name="bandType">The band type to test.</param>
    /// <returns><c>true</c>, if the band type is one of the linear ones, or <c>false</c>,
    /// if not.</returns>
    internal static bool IsLinear(this BandType bandType)
    {
        return bandType is BandType.LinearX or BandType.LinearY or BandType.LinearZ;
    }
}
