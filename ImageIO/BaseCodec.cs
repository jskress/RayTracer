using System.Text;
using RayTracer.Graphics;

namespace RayTracer.ImageIO;

public abstract class BaseCodec : IImageCodec
{
    /// <summary>
    /// This method is used to encode the given canvas to the specified stream.
    /// </summary>
    /// <param name="canvas">The canvas being encoded and written.</param>
    /// <param name="stream">The stream to encode</param>
    public abstract void Encode(Canvas canvas, Stream stream);

    /// <summary>
    /// This method is used to decode the given screen into one or more canvases, one
    /// canvas per image found in the stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>The canvases that hold the images found in the stream.</returns>
    public abstract Canvas[] Decode(Stream stream);

    /// <summary>
    /// This is a helper method for writing the given string to the specified stream.  The
    /// text is converted to bytes by applying ASCII encoding.
    /// </summary>
    /// <param name="stream">The stream to write the text to.</param>
    /// <param name="text">The text to write.</param>
    /// <param name="encoding">The encoding to use.  This defaults to ASCII encoding.</param>
    protected static void WriteText(Stream stream, string text, Encoding encoding = null)
    {
        encoding ??= Encoding.ASCII;

        stream.Write(encoding.GetBytes(text));
        stream.Flush();
    }

    /// <summary>
    /// This method is used to read a line of text from the given stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="encoding">The encoding to use.  This defaults to ASCII encoding.</param>
    /// <returns>The string that was read.</returns>
    protected static string ReadLine(Stream stream, Encoding encoding = null)
    {
        encoding ??= Encoding.ASCII;

        List<byte> bytes = [];
        
        int data = stream.ReadByte();

        while (data >= 0 && data != '\n')
        {
            bytes.Add((byte) data);

            data = stream.ReadByte();
        }

        return data < 0 && bytes.Count == 0 ? null : encoding.GetString(bytes.ToArray());
    }
}
