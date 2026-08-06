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

    /// <summary>
    /// This property holds what fills the space between a scene's objects, if anything does.  It is
    /// nothing by default, which is to say the space is empty and a ray crosses it untouched.
    /// <para>
    /// A medium named here fills exactly the space this index of refraction governs -- everywhere a
    /// ray is inside none of the scene's objects -- which is the same rule read twice rather than two
    /// rules.  Being the space that has no end, it is also the one place a medium may be asked to act
    /// over an endless span, and the one place it matters that a medium which emits without absorbing
    /// has no answer over such a span.
    /// </para>
    /// </summary>
    public Medium Medium { get; set; }
}
