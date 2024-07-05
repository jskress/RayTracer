using RayTracer.Graphics;

namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class represents a scan line of pixel data.
/// </summary>
internal class ScanLine
{
    private readonly byte[] _pixelData;
    private readonly byte[] _filteredData;
    private readonly int _bytesPerPixel;
    private readonly bool _twoBytes;
    private readonly bool _grayscale;
    private readonly bool _includeAlpha;

    internal ScanLine(PngHeaderChunk headerChunk)
    {
        _pixelData = new byte[headerChunk.ScanlineByteCount];
        _filteredData = new byte[headerChunk.ScanlineByteCount];
        _bytesPerPixel = headerChunk.ScanlineBytesPerPixel;
        _twoBytes = headerChunk.BitDepth > 8;
        _grayscale = headerChunk.ColorType is
            PngColorType.Grayscale or PngColorType.GrayscaleWithAlpha;
        _includeAlpha = headerChunk.ColorType is
            PngColorType.GrayscaleWithAlpha or PngColorType.TrueColorWithAlpha;
    }

    /// <summary>
    /// This method is used to populate our scan line byte data from the indicated line
    /// of the canvas.
    /// </summary>
    /// <param name="canvas">The canvas to pull pixel color information from.</param>
    /// <param name="y">The index of the line to pull from.</param>
    internal void ReadFromCanvas(Canvas canvas, int y)
    {
        int cp = 0;

        for (int x = 0; x < canvas.Width; x++)
        {
            cp = _grayscale
                ? AddGrayColor(canvas.GetPixel(x, y), cp)
                : AddTrueColor(canvas.GetPixel(x, y), cp);
        }

        if (cp != _pixelData.Length)
            throw new Exception("Internal error: wrote different number of bytes than expected to our scanline.");
    }

    /// <summary>
    /// This method is used to add the raw bytes for the given color as a grayscale value
    /// to our scanline data.
    /// </summary>
    /// <param name="color">The color to convert to gray and add to the scanline.</param>
    /// <param name="cp">The current point in the scanline to store the bytes.</param>
    /// <returns>The updated point in the scanline after the bytes we just added.</returns>
    private int AddGrayColor(Color color, int cp)
    {
        (int gray, int alpha) = color.ToGrayValue();

        cp = WriteSample(gray, cp);

        if (_includeAlpha)
            cp = WriteSample(alpha, cp);

        return cp;
    }

    /// <summary>
    /// This method is used to add the raw bytes for the given color to our scanline data.
    /// </summary>
    /// <param name="color">The color to add to the scanline.</param>
    /// <param name="cp">The current point in the scanline to store the bytes.</param>
    /// <returns>The updated point in the scanline after the bytes we just added.</returns>
    private int AddTrueColor(Color color, int cp)
    {
        (int red, int green, int blue, int alpha) = color.ToChannelValues();

        cp = WriteSample(red, cp);
        cp = WriteSample(green, cp);
        cp = WriteSample(blue, cp);

        if (_includeAlpha)
            cp = WriteSample(alpha, cp);

        return cp;
    }

    /// <summary>
    /// THis is a helper method for writing a binary integer to our scanline data.
    /// </summary>
    /// <param name="number">The number to write out.</param>
    /// <param name="cp">The current point in the scanline to store the bytes.</param>
    /// <returns>The updated point in the scanline after the bytes we just added.</returns>
    private int WriteSample(int number, int cp)
    {
        if (_twoBytes)
        {
            _pixelData[cp++] = (byte)(number >> 8);
            number &= 0x000000FF;
        }

        _pixelData[cp++] = (byte) number;

        return cp;
    }

    /// <summary>
    /// This method is used to apply the specified filter to this scan line and then write
    /// the filtered bytes to the given stream.
    /// </summary>
    /// <param name="filterType">The type of filter to apply</param>
    /// <param name="previous">The scan line above us</param>
    /// <param name="stream">The stream to write to.</param>
    internal void FilterAndWrite(PngFilterType filterType, ScanLine previous, Stream stream)
    {
        filterType.ApplyFilter(_pixelData, previous._pixelData, _filteredData, _bytesPerPixel);

        ImageFileIo.WriteByte(stream, (byte) filterType);
        ImageFileIo.WriteBytes(stream, _filteredData);
    }

    /// <summary>
    /// This method is used to accumulate the Adler32 checksum for this scan line.
    /// </summary>
    /// <param name="checksum">The Adler32 checksum accumulator.</param>
    internal void AddToChecksum(Adler32 checksum)
    {
        checksum.Add(_pixelData);
    }

    /// <summary>
    /// This method is used to apply the given filter type and return a sum of the resulting
    /// filter bytes.  This is used to help decide which filter type to ultimately use.
    /// </summary>
    /// <param name="filterType">The filter type to apply.</param>
    /// <param name="previous">The scan line above us</param>
    /// <returns>The sum of the resulting filter.</returns>
    internal int GetFilterSum(PngFilterType filterType, ScanLine previous)
    {
        filterType.ApplyFilter(_pixelData, previous._pixelData, _filteredData, _bytesPerPixel);

        return _filteredData
            .Select(data =>
            {
                int value = unchecked((sbyte) data);

                return Math.Abs(value);
            })
            .Sum();
    }

    /// <summary>
    /// This method is used to read the next scan line and apply the appropriate filter to
    /// what was read to produce our scan line pixel data.
    /// </summary>
    /// <param name="previous">The scan line above us</param>
    /// <param name="stream">The stream to write to.</param>
    internal void ReadAndFilter(Stream stream, ScanLine previous)
    {
        PngFilterType filterType = (PngFilterType) (ImageFileIo.ReadByte(stream) ?? 0);

        ImageFileIo.ReadBytes(stream, _filteredData);

        filterType.UndoFilter(_filteredData, _pixelData, previous._pixelData, _bytesPerPixel);
    }

    /// <summary>
    /// This method is used to populate the specified line of the given canvas with our
    /// scan line byte data.
    /// </summary>
    /// <param name="canvas">The canvas to push pixel color information to.</param>
    /// <param name="y">The index of the line to push to.</param>
    internal void WriteToCanvas(Canvas canvas, int y)
    {
    }
}
