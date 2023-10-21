using System.Text;
using RayTracer.Graphics;

namespace RayTracer.ImageIO;

/// <summary>
/// This class provides a codec for PPM image files.
/// </summary>
public class PpmCodec : IImageCodec
{
    /// <summary>
    /// This method is used to encode the given canvas to the specified stream.
    /// </summary>
    /// <param name="canvas">The canvas being encoded and written.</param>
    /// <param name="stream">The stream to write to.</param>
    public void Encode(Canvas canvas, Stream stream)
    {
        using StreamWriter writer = new (stream, Encoding.Default);

        WriteHeader(canvas, writer);
        WritePixels(canvas, writer);
    }

    /// <summary>
    /// This method is used to write the PPM header to the file for the given canvas.
    /// </summary>
    /// <param name="canvas">The canvas being encoded and written.</param>
    /// <param name="writer">The stream to write to.</param>
    private static void WriteHeader(Canvas canvas, StreamWriter writer)
    {
        writer.WriteLine($"""
            P3
            {canvas.Width} {canvas.Height}
            255
            """);
    }

    /// <summary>
    /// This method is used to write the appropriate pixel data in PPM format to the
    /// file.
    /// </summary>
    /// <param name="canvas">The canvas being encoded and written.</param>
    /// <param name="writer">The stream to write to.</param>
    private static void WritePixels(Canvas canvas, StreamWriter writer)
    {
        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                Color color = canvas.GetPixel(x, y);

                // Apply gamma correction.
                // double r = Math.Sqrt(color.Red);
                // double g = Math.Sqrt(color.Green);
                // double b = Math.Sqrt(color.Blue);
                double r = Math.Clamp(color.Red, 0, 1);
                double g = Math.Clamp(color.Green, 0, 1);
                double b = Math.Clamp(color.Blue, 0, 1);

                // Now write out the color.
                int red = Convert.ToInt32(r * 255);
                int green = Convert.ToInt32(g * 255);
                int blue = Convert.ToInt32(b * 255);

                writer.WriteLine($"{red} {green} {blue}");
            }
        }
    }
}
