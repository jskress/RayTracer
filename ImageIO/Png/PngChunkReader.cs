using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class provides support for reading chunks from a PNG image file.
/// </summary>
public class PngChunkReader
{
    /// <summary>
    /// This field holds the list of chunk types that may appear once, and only once in a
    /// PNG file.
    /// </summary>
    private static readonly string[] Singles =
    [
        ChunkTypes.HeaderChunk,
        ChunkTypes.PaletteChunk,
        ChunkTypes.EndChunk,

        ChunkTypes.ChromaticitiesChunk,
        ChunkTypes.GammaChunk,
        ChunkTypes.EmbeddedIccProfileChunk,
        ChunkTypes.SignificantBitsChunk,
        ChunkTypes.StandardRgbChunk,
        ChunkTypes.BackgroundChunk,
        ChunkTypes.PaletteHistogramChunk,
        ChunkTypes.TransparencyChunk,
        ChunkTypes.PhysicalPixelChunk,
        ChunkTypes.LastModifiedTimeChunk
    ];

    /// <summary>
    /// This field holds the list of chunk types that should be cached.
    /// </summary>
    private static readonly string[] TypesToCache =
    [
        ChunkTypes.HeaderChunk,
        ChunkTypes.PaletteChunk,
        ChunkTypes.EndChunk,

        ChunkTypes.TransparencyChunk
    ];

    /// <summary>
    /// This property reports the header chunk that we carry.
    /// </summary>
    internal PngHeaderChunk HeaderChunk => GetChunk<PngHeaderChunk>(ChunkTypes.HeaderChunk);

    /// <summary>
    /// This property reports the palette chunk that we carry.
    /// </summary>
    internal PngPaletteChunk PaletteChunk => GetChunk<PngPaletteChunk>(ChunkTypes.PaletteChunk);

    /// <summary>
    /// This property reports the palette chunk that we carry.
    /// </summary>
    private PngEndChunk EndChunk => GetChunk<PngEndChunk>(ChunkTypes.EndChunk);

    private readonly RenderContext _context;
    private readonly Stream _stream;
    private readonly Dictionary<string, PngChunk> _chunks;
    private readonly HashSet<string> _seenChunks;

    private PngChunk _lastReadChunk;

    public PngChunkReader(RenderContext context, Stream stream)
    {
        byte[] header = ImageFileIo.ReadBytes(stream, 8);

        if (!PngCodec.FileHeader.SequenceEqual(header))
            throw new Exception("File does not look like a PNG file or it is corrupted.");

        _context = context;
        _stream = stream;
        _chunks = new Dictionary<string, PngChunk>();
        _seenChunks = [];
        _lastReadChunk = null;

        ReadHeaderChunk();
    }

    /// <summary>
    /// This is a helper method for accessing a chunk that we have read by its type.
    /// </summary>
    /// <param name="type">The type of chunk to get from our cache.</param>
    /// <typeparam name="TChunk">The expected class of the chunk.</typeparam>
    /// <returns>The requested chunk or <c>null</c>.</returns>
    private TChunk GetChunk<TChunk>(string type)
        where TChunk : PngChunk
    {
        return _chunks.TryGetValue(type, out PngChunk chunk)
            ? chunk as TChunk
            : null;
    }

    /// <summary>
    /// This method is used to read the image from our underlying stream.
    /// </summary>
    /// <returns>The image we read.</returns>
    public Canvas Read()
    {
        Canvas canvas = new (HeaderChunk.ImageWidth, HeaderChunk.ImageHeight);
        using PngImageStream imageStream = new PngImageStream(_context, this);
        using InflaterInputStream decompressor = new (imageStream);
        ScanLine previous = new ScanLine(_context, HeaderChunk);
        ScanLine current = new ScanLine(_context, HeaderChunk);

        for (int y = 0; y < canvas.Height; y++)
        {
            current.ReadAndFilter(decompressor, previous);
            current.WriteToCanvas(this, canvas, y);

            (current, previous) = (previous, current);
        }

        decompressor.Close();

        VerifyFileTrailer();

        return canvas;
    }

    /// <summary>
    /// This method is used to read the PNG header chunk from the given stream.
    /// </summary>
    private void ReadHeaderChunk()
    {
        PngChunk chunk = GetNextChunk();

        if (chunk is PngHeaderChunk headerChunk)
        {
            if (headerChunk.CompressionMethod != 0)
                throw new Exception("The PNG image is encoded with an unsupported compression method.");

            if (headerChunk.FilterMethod != 0)
                throw new Exception("The PNG image uses an unsupported filter method.");

            if (headerChunk.Interlaced)
                throw new Exception("The ray tracer does not currently support interlaced PNG images.");

            // This will verify that the color type/bit depth combination is valid.
            _ = headerChunk.ScanlineBytesPerPixel;
        }
        else
            throw new Exception("PNG image file format is incorrect.  Header chunk is missing.");
    }

    /// <summary>
    /// This method gets the next image data chunk from our stream.
    /// </summary>
    /// <returns>The next data chunk, or <c>null</c>, if we've exhausted them.</returns>
    public PngImageDataChunk GetImageDataChunk()
    {
        bool inTheMiddleOfData = _seenChunks.Contains(ChunkTypes.ImageDataChunk);
        PngChunk chunk = GetNextChunk();

        while (chunk.Type != ChunkTypes.ImageDataChunk)
        {
            if (chunk is PngEndChunk) // We've hit the end
                return null;

            if (chunk.IsAncillary)
            {
                if (inTheMiddleOfData)
                {
                    _lastReadChunk = chunk;

                    return null;
                }
            }

            chunk = GetNextChunk();
        }

        return chunk as PngImageDataChunk;
    }

    /// <summary>
    /// This method is used to verify that the file has only ancillary chunks after the
    /// image data and that nothing follows the end chunk.
    /// </summary>
    private void VerifyFileTrailer()
    {
        while (EndChunk == null)
        {
            PngChunk chunk = GetNextChunk();

            if (chunk.IsCritical && EndChunk == null)
                throw new Exception($"PNG image file format is incorrect.  Found ${chunk.Type} after image data.");
        }
    }

    /// <summary>
    /// This method is used to get the next chunk that should be processed.
    /// </summary>
    /// <returns>The next chunk to process.</returns>
    private PngChunk GetNextChunk()
    {
        return _lastReadChunk ?? ReadChunk();
    }

    /// <summary>
    /// This method is used to read the next chunk from our stream.  If there are no more
    /// chunks left in the file, then <c>null</c> will be returned.
    /// </summary>
    /// <returns>The next chunk or <c>null</c>.</returns>
    private PngChunk ReadChunk()
    {
        (string chunkType, byte[] data) = ReadRawChunk(_stream);

        if (chunkType != null)
        {
            PngChunk chunk = CreateChunk(chunkType, data);

            _seenChunks.Add(chunkType);

            if (TypesToCache.Contains(chunkType))
                _chunks[chunkType] = chunk;

            return chunk;
        }

        return null;
    }

    /// <summary>
    /// This method reads and validates the raw data for a chunk from the given stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>A tuple containing the type and data for the chunk that was read.</returns>
    private (string Type, byte[] Data) ReadRawChunk(Stream stream)
    {
        int? length = ImageFileIo.ReadInt(stream, 4, isRequired: false);

        if (!length.HasValue)
            return (null, null);

        string chunkType = VerifyChunkType(ImageFileIo.ReadText(stream, 4));
        byte[] data = ImageFileIo.ReadBytes(stream, length.Value) ?? [];
        uint storedCrc = ImageFileIo.ReadUInt(stream, 4) ?? 0;
        uint calculatedCrc = new Crc32()
            .Append(chunkType)
            .Append(data)
            .Value;

        if (storedCrc != calculatedCrc)
            throw new Exception($"PNG image file is likely corrupted.  PNG {chunkType} chunk failed CRC check.");

        return (chunkType, data);
    }

    /// <summary>
    /// This is a helper method that will verify that the given chunk type is appearing in
    /// the right place in the PNG image file.
    /// </summary>
    /// <param name="chunkType">The chunk type to check.</param>
    /// <returns>The verified chunk type.</returns>
    private string VerifyChunkType(string chunkType)
    {
        if (Singles.Contains(chunkType) && _chunks.ContainsKey(chunkType))
            throw new Exception($"PNG image file format is incorrect.  Only one {chunkType} chunk is allowed.");

        bool valid = chunkType switch
        {
            ChunkTypes.PaletteChunk =>
                !_seenChunks.Contains(ChunkTypes.ImageDataChunk) &&
                !_seenChunks.Contains(ChunkTypes.BackgroundChunk) &&
                !_seenChunks.Contains(ChunkTypes.TransparencyChunk),
            ChunkTypes.ChromaticitiesChunk =>
                !_seenChunks.Contains(ChunkTypes.PaletteChunk) &&
                !_seenChunks.Contains(ChunkTypes.ImageDataChunk),
            ChunkTypes.GammaChunk =>
                !_seenChunks.Contains(ChunkTypes.PaletteChunk) &&
                !_seenChunks.Contains(ChunkTypes.ImageDataChunk),
            ChunkTypes.EmbeddedIccProfileChunk =>
                !_seenChunks.Contains(ChunkTypes.PaletteChunk) &&
                !_seenChunks.Contains(ChunkTypes.ImageDataChunk),
            ChunkTypes.SignificantBitsChunk =>
                !_seenChunks.Contains(ChunkTypes.PaletteChunk) &&
                !_seenChunks.Contains(ChunkTypes.ImageDataChunk),
            ChunkTypes.StandardRgbChunk =>
                !_seenChunks.Contains(ChunkTypes.PaletteChunk) &&
                !_seenChunks.Contains(ChunkTypes.ImageDataChunk),
            ChunkTypes.BackgroundChunk => !_seenChunks.Contains(ChunkTypes.ImageDataChunk),
            ChunkTypes.PaletteHistogramChunk =>
                _seenChunks.Contains(ChunkTypes.PaletteChunk) &&
                !_seenChunks.Contains(ChunkTypes.ImageDataChunk),
            ChunkTypes.PhysicalPixelChunk => !_seenChunks.Contains(ChunkTypes.ImageDataChunk),
            ChunkTypes.SuggestedPaletteChunk => !_seenChunks.Contains(ChunkTypes.ImageDataChunk),
            _ => true
        };

        if (!valid)
            throw new Exception($"PNG image file format is incorrect.  The {chunkType} chunk is not allowed where it was found.");

        return chunkType;
    }

    /// <summary>
    /// This is a helper method for creating a chunk of the appropriate concrete type.
    /// </summary>
    /// <param name="type">The type of chunk to create.</param>
    /// <param name="data">The raw data for the chunk.</param>
    /// <returns>The created chunk</returns>
    private PngChunk CreateChunk(string type, byte[] data)
    {
        PngChunk chunk = type switch
        {
            ChunkTypes.HeaderChunk => new PngHeaderChunk(_context),
            ChunkTypes.InternationalTextChunk => new PngI18NTextChunk(_context),
            ChunkTypes.PaletteChunk => new PngPaletteChunk(_context),
            ChunkTypes.GammaChunk => new PngGammaChunk(_context),
            ChunkTypes.ImageDataChunk => new PngImageDataChunk(_context),
            ChunkTypes.EndChunk => new PngEndChunk(_context),
            _ => new UnknownPngChunk(_context, type)
        };

        chunk.SetData(this, data);

        return chunk;
    }
}
