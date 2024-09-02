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
