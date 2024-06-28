using RayTracer.Graphics;

namespace RayTracer.ImageIO;

/// <summary>
/// This class provides a codec for binary PPM image files.
/// </summary>
public class Ppm6Codec : PpmCodec
{
    /// <summary>
    /// This property reports the specific value of the magic number to put, or expect, in
    /// the image file's marker.
    /// </summary>
    protected override int MagicNumber => 6;

    /// <summary>
    /// This method is used to write the appropriate pixel data in PPM format to the
    /// file.
    /// </summary>
    /// <param name="canvas">The canvas being encoded and written.</param>
    /// <param name="stream">The stream to write to.</param>
    protected override void WritePixels(Canvas canvas, Stream stream)
    {
        bool twoBytes = ProgramOptions.Instance.MaxColorChannelValue > 255;

        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                Color color = canvas.GetPixel(x, y);
                (int red, int green, int blue, _) = color.ToChannelValues();

                if (twoBytes)
                {
                    stream.WriteByte((byte) (red >> 8));
                    stream.WriteByte((byte) (red & 0x000000FF));
                    stream.WriteByte((byte) (green >> 8));
                    stream.WriteByte((byte) (green & 0x000000FF));
                    stream.WriteByte((byte) (blue >> 8));
                    stream.WriteByte((byte) (blue & 0x000000FF));
                }
                else
                {
                    stream.WriteByte((byte) red);
                    stream.WriteByte((byte) green);
                    stream.WriteByte((byte) blue);
                }
            }

            stream.Flush();
        }
    }

    /// <summary>
    /// This method is used to decode the given screen into one or more canvases, one
    /// canvas per image found in the stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>The canvases that hold the images found in the stream.</returns>
    public override Canvas[] Decode(Stream stream)
    {
        (Canvas canvas, int maxColorValue) = ReadHeader(stream);
        List<Canvas> result = [];

        if (canvas == null)
            throw new Exception("The image file is empty.");

        while (canvas != null)
        {
            bool twoBytes = maxColorValue > 255;

            for (int y = 0; y < canvas.Height; y++)
            {
                for (int x = 0; x < canvas.Width; x++)
                {
                    int red = ReadChannelValue(stream, twoBytes);
                    int green = ReadChannelValue(stream, twoBytes);
                    int blue = ReadChannelValue(stream, twoBytes);

                    if (red < 0 || red > maxColorValue ||
                        green < 0 || green > maxColorValue ||
                        blue < 0 || blue > maxColorValue)
                        throw new Exception("File does not look like a PPM file or it is corrupted.");

                    Color color = Color.FromChannelValues(red, green, blue, maxColorValue);

                    canvas.SetColor(color, x, y);
                }
            }

            result.Add(canvas);

            (canvas, maxColorValue) = ReadHeader(stream);
        }

        return result.ToArray();
    }

    private static int ReadChannelValue(Stream stream, bool twoBytes)
    {
        int value = stream.ReadByte();

        if (twoBytes & value >= 0)
        {
            int low = stream.ReadByte();

            if (low < 0)
                value = -1;
            else
                value = ((value & 0x000000FF) << 8) | (low & 0x000000FF);
        }

        return value;
    }
}
