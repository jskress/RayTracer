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
