using RayTracer.Graphics;

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

    /// <summary>
    /// This property holds how far light gets into the substance before most of it is gone,
    /// measured in the scene's own units.
    /// <para>
    /// Where <see cref="Filter"/> is charged once at each surface crossed, this is charged by the
    /// distance travelled between them, following Beer's law, so that thick glass is darker than
    /// thin glass of the very same stuff.  A clarity of 2 means light has faded to about a third of
    /// itself after travelling 2 units, and to a ninth after 4.  The default is infinite: light
    /// crosses any distance untouched, which is what every scene written before this existed
    /// assumes.  A clarity of 0 is the other extreme and lets nothing through at all.
    /// </para>
    /// <para>
    /// This is deliberately colourless, unlike POV-Ray's <c>fade_colour</c>: what colour deep
    /// substance takes on is <see cref="Filter"/>'s business, and keeping the two apart means the
    /// tint can be set once and the depth tuned without disturbing it.
    /// </para>
    /// <para>
    /// The distance travelled is taken to be the distance between the surface a ray entered by and
    /// the one it leaves by, which is true of a closed solid and is what this is for.  Setting it
    /// on something with no inside -- a plane, a disc, a lone triangle -- has nothing sensible to
    /// measure, and on solids that overlap or nest it measures only the nearest one.
    /// </para>
    /// </summary>
    public double Clarity { get; set; } = double.PositiveInfinity;

    /// <summary>
    /// This method returns the amount by which light passing through a surface is tinted by that
    /// surface's own colour, as a factor to multiply the light by.
    /// </summary>
    /// <param name="surfaceColor">The colour of the surface at the point light crossed it.</param>
    /// <returns>The tint to apply, which is white when no filtering is called for.</returns>
    public Color GetFilterTint(Color surfaceColor)
    {
        // A lerp from white -- light through untouched -- toward the surface's colour.  This is
        // the same shape as Material.GetMetallicTint() on purpose, so that the two properties that
        // tint by a surface's own colour read alike.
        return Filter <= 0
            ? Colors.White
            : Colors.White + (surfaceColor - Colors.White) * Filter;
    }

    /// <summary>
    /// This method returns the amount by which light fades over the given distance travelled
    /// through the substance, as a factor to multiply the light by.
    /// </summary>
    /// <param name="distance">How far the light travelled through the substance.</param>
    /// <returns>The fade to apply, which is 1 when the substance does not fade light at all.</returns>
    public double GetFadeOver(double distance)
    {
        // Beer's law.  The infinite default falls out of the arithmetic on its own -- exp(-d/inf)
        // is 1 -- but it is worth short-circuiting, since most surfaces in most scenes never fade
        // anything and would otherwise pay for an exponential at every hit.
        return double.IsPositiveInfinity(Clarity)
            ? 1
            : Math.Exp(-distance / Clarity);
    }
}
