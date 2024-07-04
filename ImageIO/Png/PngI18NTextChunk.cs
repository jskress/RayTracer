using System.IO.Compression;
using System.Text;

namespace RayTracer.ImageIO.Png;

/// <summary>
/// This class represents the international text chunk in a PNG file.
/// </summary>
public class PngI18NTextChunk : PngChunk
{
    /// <summary>
    /// This property holds the keyword for the text chunk.
    /// </summary>
    public string Keyword { get; set; } = string.Empty;

    /// <summary>
    /// This property holds the language tag for the text chunk.
    /// </summary>
    public string LanguageTag { get; set; } = string.Empty;

    /// <summary>
    /// This property holds the translated keyword for the text chunk.
    /// </summary>
    public string TranslatedKeyword { get; set; } = string.Empty;

    /// <summary>
    /// This property holds the text of the text chunk.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    public PngI18NTextChunk() : base(ChunkTypes.InternationalTextChunk) {}

    /// <summary>
    /// This method is used to serialize our specific data into the given stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    protected override void WriteData(Stream stream)
    {
        ImageFileIo.WriteText(stream, Keyword, true);
        ImageFileIo.WriteByte(stream, 0); // Compression flag.
        ImageFileIo.WriteByte(stream, 0); // Compression method.
        ImageFileIo.WriteText(stream, LanguageTag, true);
        ImageFileIo.WriteText(stream, TranslatedKeyword, true);
        ImageFileIo.WriteText(stream, Text, false, Encoding.UTF8);
    }

    /// <summary>
    /// This method is used to deserialize our specific data from the given stream.
    /// </summary>
    /// <param name="reader">The chunk reader that's doing all the work.</param>
    /// <param name="data">The raw data to initialize from.</param>
    /// <param name="stream">The raw data as a stream.</param>
    protected override void ReadData(PngChunkReader reader, byte[] data, Stream stream)
    {
        Keyword = ImageFileIo.ReadText(stream);

        byte compressed = ImageFileIo.ReadByte(stream) ?? 0;

        _ = ImageFileIo.ReadByte(stream);
        LanguageTag = ImageFileIo.ReadText(stream);
        TranslatedKeyword = ImageFileIo.ReadText(stream);

        if (compressed == 0)
            Text = ImageFileIo.ReadText(stream, false, Encoding.UTF8);
        else
        {
            DeflateStream decompressor = new DeflateStream(
                stream, CompressionMode.Decompress, true);
            MemoryStream buffer = new MemoryStream();

            decompressor.CopyTo(buffer);

            Text = Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
}
