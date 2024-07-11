using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class represents the palette chunk in a PNG file.
/// </summary>
public class PngPaletteChunk : PngChunk
{
    /// <summary>
    /// This property holds the palette specified in the chunk.
    /// </summary>
    public Color[] Palette { get; set; }

    public PngPaletteChunk(RenderContext context) : base(context, ChunkTypes.PaletteChunk) {}

    /// <summary>
    /// This method is used to serialize our specific data into the given stream.  Since
    /// there's no real need for the ray tracer to emit a palette, this method does
    /// nothing.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    protected override void WriteData(Stream stream)
    {
        // No-op.
    }

    /// <summary>
    /// This method is used to deserialize our specific data from the given stream.
    /// </summary>
    /// <param name="reader">The chunk reader that's doing all the work.</param>
    /// <param name="data">The raw data to initialize from.</param>
    /// <param name="stream">The raw data as a stream.</param>
    protected override void ReadData(PngChunkReader reader, byte[] data, Stream stream)
    {
        PngHeaderChunk headerChunk = reader.HeaderChunk;

        if (headerChunk.ColorType is PngColorType.Grayscale or PngColorType.GrayscaleWithAlpha)
            throw new Exception($"Image file format is incorrect.  PNG palette chunk not allowed in grayscale images.");

        if (data.Length % 3 != 0)
            throw new Exception("PNG image file format is incorrect.  PNG palette chunk has a bad length.");

        int entries = data.Length / 3;

        Palette = new Color[entries];

        for (int index = 0; index < entries; index++)
        {
            int red = ImageFileIo.ReadInt(stream, 1) ?? 0;
            int green = ImageFileIo.ReadInt(stream, 1) ?? 0;
            int blue = ImageFileIo.ReadInt(stream, 1) ?? 0;

            Palette[index] = Color.FromChannelValues(red, green, blue, 255);
        }
    }
}
