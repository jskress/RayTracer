using RayTracer.General;

namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class represents the header chunk in a PNG file.
/// </summary>
public class PngHeaderChunk : PngChunk
{
    /// <summary>
    /// This property holds the width of the image.
    /// </summary>
    public int ImageWidth { get; set; }

    /// <summary>
    /// This property holds the height of the image.
    /// </summary>
    public int ImageHeight { get; set; }

    /// <summary>
    /// This property holds the bit depth of the image.  It will/must be 1, 2, 4, 8 or 16.
    /// </summary>
    public byte BitDepth { get; set; }

    /// <summary>
    /// This property notes the color type of the image.
    /// </summary>
    public PngColorType ColorType { get; set; }

    /// <summary>
    /// This property holds the compression method used in the image.
    /// </summary>
    public byte CompressionMethod { get; set; }

    /// <summary>
    /// This property holds the filter method used in the image.
    /// </summary>
    public byte FilterMethod { get; set; }

    /// <summary>
    /// This property indicates whether the image data is interlaced or not.
    /// </summary>
    public bool Interlaced { get; set; }

    /// <summary>
    /// This property reports the number of bytes required per pixel in a scanline for the
    /// purposes of PNG's scanline filtering algorithms.  This is based on a combination of
    /// <see cref="ColorType"/> and <see cref="BitDepth"/>.  Bit depths requiring less than
    /// a byte are rounded up to 1.
    /// </summary>
    public int ScanlineBytesPerPixel => GetScanlineBytesPerPixel();

    /// <summary>
    /// This property reports the number of bytes required per scanline for the image.  This
    /// does not include the leading filter type byte.  It is based on a combination of
    /// <see cref="ImageWidth"/>, <see cref="ColorType"/> and <see cref="BitDepth"/>.
    /// </summary>
    public int ScanlineByteCount => GetScanlineByteCount();

    public PngHeaderChunk(RenderContext context) : base(context, ChunkTypes.HeaderChunk) {}

    /// <summary>
    /// This method examines <see cref="ColorType"/> and <see cref="BitDepth"/> to determine
    /// the bytes per pixel the PNG filtering algorithms use in their calculations.
    /// </summary>
    /// <remarks>Note that this is different than the number of bytes a pixel may occupy.
    /// Even though we will never write out images with multiple pixels per byte, we should
    /// still be able to read such images.</remarks>
    /// <returns>The number of bytes required per pixel for the sake of filtering.</returns>
    private int GetScanlineBytesPerPixel()
    {
        return ColorType switch
        {
            PngColorType.Grayscale when BitDepth is 1 or 2 or 4 or 8 => 1,
            PngColorType.Grayscale when BitDepth is 16 => 2,
            PngColorType.TrueColor when BitDepth is 8 => 3,
            PngColorType.TrueColor when BitDepth is 16 => 6,
            PngColorType.IndexedColor when BitDepth is 1 or 2 or 4 or 8 => 1,
            PngColorType.GrayscaleWithAlpha when BitDepth is 8 => 2,
            PngColorType.GrayscaleWithAlpha when BitDepth is 16 => 4,
            PngColorType.TrueColorWithAlpha when BitDepth is 8 => 4,
            PngColorType.TrueColorWithAlpha when BitDepth is 16 => 8,
            _ => throw new Exception($"PNG image file format is incorrect.  Color type {ColorType} cannot have a bit depth of {BitDepth}.")
        };
    }

    /// <summary>
    /// This method examines <see cref="ImageWidth"/>, <see cref="ColorType"/> and
    /// <see cref="BitDepth"/> to determine the number of bytes needed to hold one scanline
    /// in the image.
    /// </summary>
    /// <remarks>This does not include the filter type byte that precedes each scanline in
    /// the image data, just the bytes for pixel data.</remarks>
    /// <returns>The number of bytes required per scanline.</returns>
    private int GetScanlineByteCount()
    {
        int factor = 8 / BitDepth;
        int round = factor > 0
            ? ImageWidth > ImageWidth / factor * factor ? 1 : 0
            : 0;

        return ColorType switch
        {
            PngColorType.Grayscale when BitDepth is 1 or 2 or 4 or 8 => ImageWidth / factor + round,
            PngColorType.Grayscale when BitDepth is 16 => ImageWidth * 2,
            PngColorType.TrueColor when BitDepth is 8 => ImageWidth * 3,
            PngColorType.TrueColor when BitDepth is 16 => ImageWidth * 6,
            PngColorType.IndexedColor when BitDepth is 1 or 2 or 4 or 8 => ImageWidth / factor + round,
            PngColorType.GrayscaleWithAlpha when BitDepth is 8 => ImageWidth * 2,
            PngColorType.GrayscaleWithAlpha when BitDepth is 16 => ImageWidth * 4,
            PngColorType.TrueColorWithAlpha when BitDepth is 8 => ImageWidth * 4,
            PngColorType.TrueColorWithAlpha when BitDepth is 16 => ImageWidth * 8,
            _ => throw new Exception($"PNG image file format is incorrect.  Color type {ColorType} cannot have a bit depth of {BitDepth}.")
        };
    }

    /// <summary>
    /// This method is used to serialize our specific data into the given stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    protected override void WriteData(Stream stream)
    {
        ImageFileIo.WriteInt(stream, ImageWidth, 4);
        ImageFileIo.WriteInt(stream, ImageHeight, 4);
        ImageFileIo.WriteByte(stream, BitDepth);
        ImageFileIo.WriteByte(stream, (byte) ColorType);
        ImageFileIo.WriteByte(stream, CompressionMethod);
        ImageFileIo.WriteByte(stream, FilterMethod);
        ImageFileIo.WriteByte(stream, (byte) (Interlaced ? 1 : 0));
    }

    /// <summary>
    /// This method is used to deserialize our specific data from the given stream.
    /// </summary>
    /// <param name="reader">The chunk reader that's doing all the work.</param>
    /// <param name="data">The raw data to initialize from.</param>
    /// <param name="stream">The raw data as a stream.</param>
    protected override void ReadData(PngChunkReader reader, byte[] data, Stream stream)
    {
        ImageWidth = ImageFileIo.ReadInt(stream, 4) ?? 0;
        ImageHeight = ImageFileIo.ReadInt(stream, 4) ?? 0;
        BitDepth = ImageFileIo.ReadByte(stream) ?? 0;
        ColorType = (PngColorType) (ImageFileIo.ReadByte(stream) ?? 0);
        CompressionMethod = ImageFileIo.ReadByte(stream) ?? 0;
        FilterMethod = ImageFileIo.ReadByte(stream) ?? 0;
        Interlaced = ImageFileIo.ReadByte(stream) > 0;
    }

    /// <summary>
    /// This method dumps the contents of this chunk to the terminal.
    /// </summary>
    protected override void DumpDetails()
    {
        Terminal.Out($"---->  Dimension: ({ImageWidth}, {ImageHeight})", OutputLevel.Verbose);
        Terminal.Out($"---->  Bit depth: {BitDepth}", OutputLevel.Verbose);
        Terminal.Out($"----> Color type: {ColorType}", OutputLevel.Verbose);
    }
}
