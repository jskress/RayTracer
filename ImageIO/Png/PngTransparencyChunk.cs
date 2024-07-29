using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class represents the transparency chunk in a PNG file.
/// </summary>
public class PngTransparencyChunk : PngChunk
{
    /// <summary>
    /// This property reports the color that should be considered transparent.
    /// </summary>
    public Color TransparentColor { get; private set; }

    public PngTransparencyChunk(RenderContext context) : base(context, ChunkTypes.TransparencyChunk) {}

    /// <summary>
    /// This method is used to serialize our specific data into the given stream.  The
    /// ray tracer will never emit this so we don't do anything.
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
        switch (reader.HeaderChunk.ColorType)
        {
            case PngColorType.IndexedColor:
                AddAlphaInfoToPalette(reader.PaletteChunk, data);
                break;
            case PngColorType.Grayscale:
                SetTransparentGray(reader.HeaderChunk, stream);
                break;
            case PngColorType.TrueColor:
                SetTransparentColor(reader.HeaderChunk, stream);
                break;
            case PngColorType.GrayscaleWithAlpha:
            case PngColorType.TrueColorWithAlpha:
            default:
                throw new Exception("PNG image file format is incorrect.  Transparency information not allowed for image color type.");
        }
    }

    /// <summary>
    /// This method is used to update the given color palette with alpha information from
    /// our data block.
    /// </summary>
    /// <param name="paletteChunk">The chunk that carries the palette to update.</param>
    /// <param name="data">The raw data to initialize from.</param>
    private static void AddAlphaInfoToPalette(PngPaletteChunk paletteChunk, byte[] data)
    {
        if (paletteChunk == null)
            throw new Exception("PNG image file format is incorrect.  Found transparency information but no palette to apply it to.");

        if (data.Length > paletteChunk.Palette.Length)
            throw new Exception("PNG image file format is incorrect.  More transparency entries than palette entries.");

        for (int index = 0; index < data.Length; index++)
        {
            paletteChunk.Palette[index] = paletteChunk.Palette[index]
                .WithAlpha(data[index] * 1.0d / 255);
        }
    }

    /// <summary>
    /// This method is used to set the transparent color for grayscale images.
    /// </summary>
    /// <param name="headerChunk">The PNG header chunk for the image.</param>
    /// <param name="stream">The stream to read from.</param>
    private void SetTransparentGray(PngHeaderChunk headerChunk, Stream stream)
    {
        int maxValue = (1 << headerChunk.BitDepth) - 1;
        int gray = ImageFileIo.ReadInt(stream, 2) ?? 0;

        TransparentColor = Color.FromChannelValues(gray, gray, gray, maxValue);
    }

    /// <summary>
    /// This method is used to set the transparent color for true-color images.
    /// </summary>
    /// <param name="headerChunk">The PNG header chunk for the image.</param>
    /// <param name="stream">The stream to read from.</param>
    private void SetTransparentColor(PngHeaderChunk headerChunk, Stream stream)
    {
        int maxValue = (1 << headerChunk.BitDepth) - 1;
        int red = ImageFileIo.ReadInt(stream, 2) ?? 0;
        int green = ImageFileIo.ReadInt(stream, 2) ?? 0;
        int blue = ImageFileIo.ReadInt(stream, 2) ?? 0;

        TransparentColor = Color.FromChannelValues(red, green, blue, maxValue);
    }
}
