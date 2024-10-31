using System.Text;

namespace RayTracer.Geometry.LSystems;

/// <summary>
/// This class represents a mapping of a character to an L-system render command.
/// </summary>
public class LSystemRenderCommandMapping
{
    /// <summary>
    /// This property holds the command character that the command should be mapped to.
    /// </summary>
    public Rune CommandCharacter { get; set; }

    /// <summary>
    /// This property holds the command that the command character should be mapped to.
    /// </summary>
    public TurtleCommand TurtleCommand { get; set; }
}
