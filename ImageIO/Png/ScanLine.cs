using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class represents a scan line of pixel data.
/// </summary>
internal class ScanLine
{
    private readonly RenderContext _context;
    private readonly byte[] _pixelData;
    private readonly byte[] _filteredData;
    private readonly int _bitDepth;
    private readonly int _pixelsPerByte;
    private readonly int _maxSampleValue;
    private readonly int _bytesPerPixel;
    private readonly bool _twoBytes;
    private readonly bool _grayscale;
    private readonly bool _includeAlpha;

    internal ScanLine(RenderContext context, PngHeaderChunk headerChunk)
    {
        _context = context;
        _pixelData = new byte[headerChunk.ScanlineByteCount];
        _filteredData = new byte[headerChunk.ScanlineByteCount];
        _bitDepth = headerChunk.BitDepth;
        _pixelsPerByte = _bitDepth < 8 ? 8 / _bitDepth : 0;
        _twoBytes = _bitDepth > 8;
        _maxSampleValue = _pixelsPerByte == 0
            ? _twoBytes ? 65535 : 255
            : (1 << _bitDepth) - 1;
        _bytesPerPixel = headerChunk.ScanlineBytesPerPixel;
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
        (int gray, int alpha) = color.ToGrayValue(_context);

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
        (int red, int green, int blue, int alpha) = color.ToChannelValues(_context);

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
    /// <param name="reader">The controlling image reader.</param>
    /// <param name="canvas">The canvas to push pixel color information to.</param>
    /// <param name="y">The index of the line to push to.</param>
    internal void WriteToCanvas(PngChunkReader reader, Canvas canvas, int y)
    {
        PngColorType colorType = reader.HeaderChunk.ColorType;
        Color[] palette = reader.PaletteChunk?.Palette;
        List<Color> colors = [];
        int cp = 0;

        for (int x = 0; x < canvas.Width; x++)
        {
            if (colors.Count == 0)
                cp = GetColorsAt(colorType, palette, colors, cp);

            canvas.SetColor(colors[0], x, y);

            colors.RemoveRange(0, 1);
        }
    }

    private int GetColorsAt(PngColorType colorType, Color[] palette, List<Color> colors, int cp)
    {
        return colorType switch
        {
            PngColorType.Grayscale => _pixelsPerByte == 0
                ? GetSingleGrayColor(colors, cp)
                : GetMultipleGrays(colors, cp),
            PngColorType.TrueColor => GetTrueColor(colors, cp),
            PngColorType.IndexedColor => GetIndexedColor(colors, palette, cp),
            PngColorType.GrayscaleWithAlpha => GetSingleGrayColor(colors, cp),
            PngColorType.TrueColorWithAlpha => GetTrueColor(colors, cp),
            _ => throw new ArgumentOutOfRangeException(nameof(colorType), $"Internal error: Unknown color type found, {colors}")
        };
    }

    /// <summary>
    /// This method is used to gather the list of gray colors from a single byte of our
    /// scan data.
    /// </summary>
    /// <param name="colors">The list to put discovered colors in.</param>
    /// <param name="cp">The current point in the scan line where the color is to be found.</param>
    /// <returns>The updated location in the scan line.</returns>
    private int GetMultipleGrays(List<Color> colors, int cp)
    {
        int samples = _pixelData[cp++];

        for (int index = 0; index < _pixelsPerByte; index++)
        {
            int gray = samples & _maxSampleValue;

            colors.Add(Color.FromChannelValues(gray, gray, gray, _maxSampleValue));

            samples >>= _bitDepth;
        }

        colors.Reverse();

        return cp;
    }

    /// <summary>
    /// This method converts a one- or two-byte gray value, with or without an alpha channel,
    /// into a color.
    /// </summary>
    /// <param name="colors">The list to put discovered colors in.</param>
    /// <param name="cp">The current point in the scan line where the color is to be found.</param>
    /// <returns>The updated location in the scan line.</returns>
    private int GetSingleGrayColor(List<Color> colors, int cp)
    {
        (int gray, cp) = ReadSample(cp);

        if (_includeAlpha)
        {
            (int alpha, cp) = ReadSample(cp);

            colors.Add(Color.FromChannelValues(gray, gray, gray, alpha, _maxSampleValue));            
        }
        else
            colors.Add(Color.FromChannelValues(gray, gray, gray, _maxSampleValue));

        return cp;
    }

    /// <summary>
    /// This method converts a one-byte index to a color by selecting the color from the
    /// given palette that the index points to.
    /// </summary>
    /// <param name="colors">The list to put discovered colors in.</param>
    /// <param name="palette">The palette of colors to select from.</param>
    /// <param name="cp">The current point in the scan line where the color is to be found.</param>
    /// <returns>The updated location in the scan line.</returns>
    private int GetIndexedColor(List<Color> colors, Color[] palette, int cp)
    {
        int index = _pixelData[cp++];

        colors.Add(palette[index]);

        return cp;
    }

    /// <summary>
    /// This method converts a one- or two-byte-per-sample true color value, with or
    /// without an alpha channel, into a color.
    /// </summary>
    /// <param name="colors">The list to put discovered colors in.</param>
    /// <param name="cp">The current point in the scan line where the color is to be found.</param>
    /// <returns>The updated location in the scan line.</returns>
    private int GetTrueColor(List<Color> colors, int cp)
    {
        (int red, cp) = ReadSample(cp);
        (int green, cp) = ReadSample(cp);
        (int blue, cp) = ReadSample(cp);

        if (_includeAlpha)
        {
            (int alpha, cp) = ReadSample(cp);

            colors.Add(Color.FromChannelValues(red, green, blue, alpha, _maxSampleValue));
        }
        else
            colors.Add(Color.FromChannelValues(red, green, blue, _maxSampleValue));

        return cp;
    }

    /// <summary>
    /// THis is a helper method for reading a binary integer from our scanline data.
    /// </summary>
    /// <param name="cp">The current point in the scanline to store the bytes.</param>
    /// <returns>A tuple containing the sample value and the updated point in the scanline
    /// after the bytes we just added.</returns>
    private (int, int) ReadSample(int cp)
    {
        int number = 0;

        if (_twoBytes)
            number = _pixelData[cp++] << 8;

        number += _pixelData[cp++];

        return (number, cp);
    }
}
