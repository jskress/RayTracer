using System.Text;
using RayTracer.Core;
using RayTracer.Graphics;

namespace RayTracer.ImageIO;

/// <summary>
/// This class provides a codec for PPM image files.
/// </summary>
public class PpmCodec : IImageCodec
{
    private StreamWriter _writer = null!;

        /// <summary>
        /// This method is used to set the output stream when writing an image
        /// </summary>
        /// <param name="stream"></param>
    public void SetStream(Stream stream)
    {
        _writer = new StreamWriter(stream, Encoding.Default);
    }

    /// <summary>
    /// This method is used to emit the appropriate header for the given image to the
    /// stream previously set.
    /// </summary>
    /// <param name="image">The image being encoded and written.</param>
    public void WriteHeader(Image image)
    {
        _writer.WriteLine($"""
            P3
            {image.Width} {image.Height}
            255
            """);
    }

    /// <summary>
    /// This method is used to emit the appropriate pixel data for the given
    /// image to the stream previously set.
    /// </summary>
    /// <param name="image">The image being encoded and written.</param>
    public void WritePixels(Image image)
    {
        Interval interval = new (0, 1);

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                Color color = image.GetPixel(x, y);

                // Apply gamma correction.
                double r = Math.Sqrt(color.Red);
                double g = Math.Sqrt(color.Green);
                double b = Math.Sqrt(color.Blue);

                // Now write out the color.
                int red = Convert.ToInt32(interval.Clamp(r) * 255);
                int green = Convert.ToInt32(interval.Clamp(g) * 255);
                int blue = Convert.ToInt32(interval.Clamp(b) * 255);

                _writer.WriteLine($"{red} {green} {blue}");
            }
        }
    }

    /// <summary>
    /// This method is used to emit the appropriate footer for the given image to the
    /// stream previously set.  We don't have a footer so we just do some cleanup.
    /// </summary>
    /// <param name="image">The image being encoded and written.</param>
    public void WriteFooter(Image image)
    {
        // No footer in a PPM file, so just do some cleanup.
        _writer.Close();
    }
}
