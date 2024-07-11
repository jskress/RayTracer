using System.Text;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.ImageIO;

/// <summary>
/// This class provides a codec for plain PPM image files.
/// </summary>
public class Ppm3Codec : PpmCodec
{
    /// <summary>
    /// This property reports the specific value of the magic number to put, or expect, in
    /// the image file's marker.
    /// </summary>
    protected override int MagicNumber => 3;

    /// <summary>
    /// This method is used to write the appropriate pixel data in PPM format to the
    /// file.
    /// </summary>
    /// <param name="canvas">The canvas being encoded and written.</param>
    /// <param name="stream">The stream to write to.</param>
    protected override void WritePixels(Canvas canvas, Stream stream)
    {
        StringBuilder builder = new ();

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                Color color = canvas.GetPixel(x, y);
                (int red, int green, int blue) = ToChannelValues(color);
                string text = $"{red} {green} {blue}";

                if (builder.Length + text.Length + 1 <= 70)
                {
                    if (builder.Length > 0)
                        builder.Append(' ');

                    builder.Append(text);
                }
                else
                {
                    ImageFileIo.WriteText(stream, builder.Append('\n').ToString());

                    builder.Length = 0;
                    builder.Append(text);
                }
            }

            if (builder.Length > 0)
            {
                ImageFileIo.WriteText(stream, builder.Append('\n').ToString());
                builder.Length = 0;
            }
        }
    }

    /// <summary>
    /// This method is used to decode the given screen into one or more canvases, one
    /// canvas per image found in the stream.
    /// </summary>
    /// <param name="context">The current rendering context.</param>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>The canvases that hold the images found in the stream.</returns>
    public override Canvas[] Decode(RenderContext context, Stream stream)
    {
        (Canvas canvas, int maxColorValue) = ReadHeader(stream);
        string[] words = null;

        if (canvas == null)
            throw new Exception("The image file is empty.");

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                (string redText, words) = NextWord(stream, words);
                (string greenText, words) = NextWord(stream, words);
                (string blueText, words) = NextWord(stream, words);
                int red = ToSafeInt(redText);
                int green = ToSafeInt(greenText);
                int blue = ToSafeInt(blueText);

                if (red < 0 || red > maxColorValue ||
                    green < 0 || green > maxColorValue ||
                    blue < 0 || blue > maxColorValue)
                    throw new Exception("File does not look like a PPM file or it is corrupted.");

                Color color = Color.FromChannelValues(red, green, blue, maxColorValue);

                canvas.SetColor(color, x, y);
            }
        }

        return [canvas];
    }
}
