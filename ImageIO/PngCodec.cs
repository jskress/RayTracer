using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.ImageIO.Png;

namespace RayTracer.ImageIO;

/// <summary>
/// This class provides a codec for PNG image files.
/// </summary>
public class PngCodec : BaseCodec
{
    /// <summary>
    /// This field holds the first 8 bytes of a PNG file.
    /// </summary>
    public static readonly byte[] FileHeader =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
    ];

    /// <summary>
    /// This method is used to encode the given canvas to the specified stream.
    /// </summary>
    /// <param name="context">The current rendering context.</param>
    /// <param name="canvas">The canvas being encoded and written.</param>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="info">Metadata about the image.</param>
    public override void Encode(RenderContext context, Canvas canvas, Stream stream, ImageInformation info)
    {
        new PngChunkWriter(context, stream, canvas, info).Write();
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
        return [new PngChunkReader(context, stream).Read()];
    }
}
