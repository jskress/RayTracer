using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class represents a pixel that is to be rendered.
/// </summary>
public class Pixel
{
    public int X { get; init; }
    public int Y { get; init; }
    internal Color Color => GetFinalColor();

    internal readonly List<Color> Samples = new ();

    /// <summary>
    /// This method is used to derive the average color from all our color samples.
    /// </summary>
    /// <returns>The final color to use for the pixel.</returns>
    private Color GetFinalColor()
    {
        switch (Samples.Count)
        {
            case 1:
                return Samples.First();
            default:
                Color result = Samples.Aggregate(
                    new Color(), (accumulator, next) => accumulator + next
                );
                return result / Samples.Count;
        }
    }
}
