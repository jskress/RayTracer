using System.IO.Compression;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class provides support for writing chunks to a PNG image file.
/// </summary>
public class PngChunkWriter
{
    private readonly Stream _stream;
    private readonly Canvas _canvas;
    private readonly ImageInformation _info;

    private PngHeaderChunk _headerChunk;

    public PngChunkWriter(Stream stream, Canvas canvas, ImageInformation info)
    {
        _stream = stream;
        _canvas = canvas;
        _info = info;
        _headerChunk = null;
    }

    /// <summary>
    /// This method is used to write out all the chunks needed to represent the canvas we
    /// are carrying.
    /// </summary>
    public void Write()
    {
        ImageFileIo.WriteBytes(_stream, PngCodec.FileHeader);

        WriteHeaderChunk();
        WriteImageInformation();

        // Let's add a gamma chunk.
        new PngGammaChunk().Write(_stream);

        WriteImageData();

        // Finally, write out an end chunk.
        new PngEndChunk().Write(_stream);
    }

    /// <summary>
    /// This method is used to create and write the PNG header chunk for the image we are
    /// to write out.
    /// </summary>
    private void WriteHeaderChunk()
    {
        bool needsAlphaChannel = _canvas.NeedsAlphaChannel;

        _headerChunk = new PngHeaderChunk
        {
            ImageWidth = _canvas.Width,
            ImageHeight = _canvas.Height,
            BitDepth = (byte) (ProgramOptions.Instance.BitsPerChannel == 8 ? 8 : 16),
            ColorType = ProgramOptions.Instance.Grayscale
                ? needsAlphaChannel
                    ? PngColorType.GrayscaleWithAlpha
                    : PngColorType.Grayscale
                : needsAlphaChannel
                    ? PngColorType.TrueColorWithAlpha
                    : PngColorType.TrueColor,
            Interlaced = false
        };

        _headerChunk.Write(_stream);
    }

    /// <summary>
    /// This method is used to write out the data we have in the image information object,
    /// if any.
    /// </summary>
    private void WriteImageInformation()
    {
        if (_info == null)
            return;

        WriteTextChunk(PredefinedTextKeywords.Title, _info.Title);
        WriteTextChunk(PredefinedTextKeywords.Author, _info.Author);
        WriteTextChunk(PredefinedTextKeywords.Description, _info.Description);
        WriteTextChunk(PredefinedTextKeywords.Copyright, _info.Copyright);
        WriteTextChunk(PredefinedTextKeywords.CreationTime, _info.CreationTime.ToString("r"));
        WriteTextChunk(PredefinedTextKeywords.Software, _info.Software);
        WriteTextChunk(PredefinedTextKeywords.Disclaimer, _info.Disclaimer);
        WriteTextChunk(PredefinedTextKeywords.Warning, _info.Warning);
        WriteTextChunk(PredefinedTextKeywords.Source, _info.Source);
        WriteTextChunk(PredefinedTextKeywords.Comment, _info.Comment);
    }

    /// <summary>
    /// This is a helper method for Formatting and emitting a text chunk that carries the
    /// given information.
    /// </summary>
    /// <param name="label">The label for the field.</param>
    /// <param name="value">The value of the field.</param>
    private void WriteTextChunk(string label, string value)
    {
        if (value != null && value.Trim().Length > 0)
        {
            PngI18NTextChunk chunk = new PngI18NTextChunk()
            {
                Keyword = label,
                Text = value
            };

            chunk.Write(_stream);
        }
    }

    /// <summary>
    /// This method is used to write out our image data.
    /// </summary>
    private void WriteImageData()
    {
        using MemoryStream memoryStream = new MemoryStream();
        using DeflateStream compressor = new DeflateStream(memoryStream, CompressionLevel.SmallestSize);
        ScanLine previous = new ScanLine(_headerChunk);
        ScanLine current = new ScanLine(_headerChunk);
        Adler32 checksum = new Adler32();

        for (int y = 0; y < _canvas.Height; y++)
        {
            PngFilterType filterType = DetermineFilterType(current, previous);

            current.ReadFromCanvas(_canvas, y);
            current.FilterAndWrite(filterType, previous, compressor);

            checksum.Add((byte) filterType);
            current.AddToChecksum(checksum);

            (current, previous) = (previous, current);
        }

        compressor.Flush();
        memoryStream.Seek(0, SeekOrigin.Begin);

        using PngImageStream imageStream = new PngImageStream(this);

        // Write out the ZLib header.
        imageStream.WriteByte(120);
        imageStream.WriteByte(1);

        memoryStream.CopyTo(imageStream);

        ImageFileIo.WriteInt(imageStream, checksum.Checksum, 4);

        compressor.Close();
        memoryStream.Close();
        imageStream.Close();
    }

    /// <summary>
    /// This method is used to pick the best filter type for the given current scan line by
    /// trying all the filter types and selecting the one with the smallest filtered sum.
    /// </summary>
    /// <param name="current">The current scan line.</param>
    /// <param name="previous">The scan line above the current one.</param>
    /// <returns></returns>
    private static PngFilterType DetermineFilterType(ScanLine current, ScanLine previous)
    {
        return Enum.GetValues<PngFilterType>()
            .Select(type => (type, current.GetFilterSum(type, previous)))
            .MinBy(pair => pair.Item2)
            .Item1;
    }

    /// <summary>
    /// This method is used to write the given data chunk to the underlying stream.
    /// </summary>
    /// <param name="imageDataChunk">The chunk to write.</param>
    internal void WriteImageDataChunk(PngImageDataChunk imageDataChunk)
    {
        imageDataChunk.Write(_stream);
    }
}
