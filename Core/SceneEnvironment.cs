namespace RayTracer.Core;

/// <summary>
/// This class holds what is true of the space a scene's objects sit in, rather than of any of the
/// objects themselves.
/// <para>
/// It exists as a thing of its own for two reasons.  A scene may be written without a <c>scene</c>
/// block at all, in which case its properties arrive as top-level items and are sorted out by type, so
/// they need a type to be sorted by.  And there is more than one property of the surrounding space
/// worth naming -- what fills it is the obvious next one -- so a home for them is better than a
/// scattering of loose settings.
/// </para>
/// </summary>
public class SceneEnvironment
{
    /// <summary>
    /// This property holds the index of refraction of the space between a scene's objects.  It is one
    /// by default, which is a vacuum; air is very slightly more (1.000293), and a scene set in water
    /// would want more again.
    /// </summary>
    public double IndexOfRefraction { get; set; } = 1;
}
