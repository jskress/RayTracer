namespace RayTracer.Core;

/// <summary>
/// This enumeration notes the sort of projection a camera uses -- the way it maps the world onto
/// the flat image.  Perspective is the ordinary sort, and the one a camera uses unless a scene
/// asks for another.
/// </summary>
public enum CameraProjectionType
{
    /// <summary>
    /// The ordinary projection a pinhole gives, where the world shrinks with distance.
    /// </summary>
    Perspective,

    /// <summary>
    /// A parallel projection, with no shrinking with distance, as a technical drawing has.
    /// </summary>
    Orthographic,

    /// <summary>
    /// A circular, very wide projection, up to and past a full half-circle across.
    /// </summary>
    Fisheye,

    /// <summary>
    /// A rectangular wide-angle projection, with less of the corner stretch a wide perspective has.
    /// </summary>
    UltraWide,

    /// <summary>
    /// A cylindrical projection, for a wide horizontal sweep.
    /// </summary>
    Panoramic,

    /// <summary>
    /// An equirectangular projection, for a full look all around, as an environment map wants.
    /// </summary>
    Spherical
}
