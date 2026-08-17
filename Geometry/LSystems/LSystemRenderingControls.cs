using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class holds the set of controls that dictate how an L-system production is to be
/// rendered.
/// </summary>
public class LSystemRenderingControls
{
    /// <summary>
    /// This property holds the type of renderer to use when converting an L-system
    /// production into geometry.
    /// </summary>
    public LSystemRendererType RendererType { get; set; } = LSystemRendererType.Pipes;

    /// <summary>
    /// This property carries the global angle to use in rendering the surface. 
    /// </summary>
    public double Angle { get; set; } = 90.0.ToRadians();

    /// <summary>
    /// This property carries the segment length the turtle is to use for each move and
    /// lind drawing in rendering the surface. 
    /// </summary>
    public double Length { get; set; } = 1;

    /// <summary>
    /// This property carries the starting diameter of segments that the turtle is to
    /// use for each line drawing in rendering the surface.
    /// </summary>
    public double Diameter { get; set; } = 1;

    /// <summary>
    /// This property carries the decrease factor for the diameter of a segment.  When the
    /// diameter is to be decreased, it is multiplied by this factor.
    /// </summary>
    public double Factor { get; set; } = 0.9;

    /// <summary>
    /// This property carries the direction that segments bend toward as they are drawn.  It is
    /// a force rather than a heading: straight down is gravity, and something with a sideways
    /// lean is a prevailing wind.  It does nothing on its own; <see cref="Susceptibility"/> is
    /// what says how far the plant gives way to it.
    /// </summary>
    public Vector Tropism { get; set; } = Directions.Down;

    /// <summary>
    /// This property carries how readily a segment bends toward the tropism direction, which is
    /// the <c>e</c> of Prusinkiewicz and Lindenmayer's formula.  Nought, the default, leaves the
    /// turtle exactly as it was, so an L-system written before any of this existed draws the
    /// same way it always did.
    /// </summary>
    public double Susceptibility { get; set; }

    /// <summary>
    /// This method creates an appropriately configured renderer based on the information
    /// we carry.
    /// </summary>
    /// <param name="production">The production that the renderer is to render.</param>
    /// <returns>An appropriately configured production renderer.</returns>
    internal LSystemShapeRenderer CreateRenderer(string production)
    {
        LSystemShapeRenderer renderer = RendererType.GetRenderer(production);

        renderer.RenderingControls = this;

        return renderer;
    }
}
