namespace RayTracer.Core;

/// <summary>
/// This class describes the substance inside a surface, as opposed to the surface itself: what
/// light does once it has crossed the boundary rather than what happens at the boundary.  It is
/// kept apart from <see cref="Material"/>'s surface properties for the same reason POV-Ray keeps
/// its own <c>interior</c> apart from its textures -- an index of refraction, or how far light
/// gets before being absorbed, is a property of the stuff, not of its skin.
/// </summary>
public class Interior
{
    /// <summary>
    /// This property holds the substance's index of refraction, which governs how sharply light
    /// bends as it crosses into or out of the surface.  <see cref="IndicesOfRefraction"/> names
    /// the usual ones.
    /// </summary>
    public double IndexOfRefraction { get; set; } = IndicesOfRefraction.Vacuum;

    /// <summary>
    /// This property holds how much of the light passing through takes on the surface's own
    /// colour, between 0 and 1.
    /// <para>
    /// At 0 -- the default, and the only behaviour available before this existed -- light passes
    /// through unaltered, however the surface is coloured: a red pane merely dims what is behind
    /// it rather than reddening it.  That is a real effect, and it is what thin gauze or dust on
    /// glass does, but it is not what glass does.  Raising this toward 1 filters what passes
    /// through by the surface's colour, so that a red pane makes what is behind it red.  It is the
    /// difference between POV-Ray's <c>transmit</c> and its <c>filter</c>, and the reason stained
    /// glass stains.
    /// </para>
    /// </summary>
    public double Filter { get; set; }
}
